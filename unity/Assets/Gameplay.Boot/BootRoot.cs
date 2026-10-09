// M2 接线轮阶段 1 装配轮 · 组合根案 A(2026-10-09)
//
// 权威来源:
//   ADR-023 §① 三场景制(Boot 常驻,永不用 LoadSceneMode.Single)· §② Boot 可有相机 / AudioListener
//     · §⑤ 拆序六步(本阶段只做「加载」半边;卸载 / flush 归 Phase 2)
//   ADR-014 §五(Addressables 首次 Step 前预载;E-13 启动期硬失败 = 抛,不 null 解引用降级)
//   ADR-025 §①(Gameplay.Boot 装配行,2026-10-09 增补)
//   OQ-25-8(TICK_SECONDS = 0.05;1 tick ≈ 3 帧不整除是刻意设计,步相位由 ITickProvider 驱动)
//
// 启动序(顺序不可改):
//   1. Addressables.InitializeAsync(autoReleaseHandle: false)(显式 init;await Task;
//      失败 = LogError + rethrow,E-13 硬失败;成功后自己 Release init 句柄)
//   2. CompositionRoot.Assemble()(生产装配,失败即异常向上传播)
//   3. Addressables.LoadSceneAsync("world", Additive) —— 永不 Single。
//      ⚠️ 本版(2.10.3)的 LoadSceneAsync **无 autoReleaseHandle 形参**,可用等价语义 =
//      默认 SceneReleaseMode.ReleaseSceneWhenSceneUnloaded(卸载时自动释放 handle);
//      handle 存 _worldLoadHandle 供 Phase 2 ADR-023 §⑤ 拆序第 4 步使用 —— 非泄漏。
//   4. 生成玩家(PlayerController)并 Initialize(Host / 流 / tick 驱动 / 编码器 / actorId)
//
// ⚠️ 刻意不抽 ISceneRouter:Phase 1 只有一个 World 场景,路由器是第二个场景(MainMenu)
//    出现时才有的真实复杂度 —— 现在抽 = 为不存在的调用方立接口(与 ADR-023 §⑤「拆序
//    六步」只实现当下半边同纪律)。届时再抽,本注即登记点。

using System;
using System.Threading.Tasks;
using DaYiJingCheng.Gameplay.Presentation.Player;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 启动序宿主(挂 Boot.unity)—— 启动序与每帧 tick 泵的唯一承载。
    /// <para><b>只渲染 / 只驱动,不持有游戏状态</b>(与 42 / 44 / 2 相机同纪律,ADR-013 §9 C3):
    /// 本类只把墙钟折成 tick 并转发给已装配的订阅者,不保存任何 sim 真值。</para>
    /// </summary>
    public sealed class BootRoot : MonoBehaviour
    {
        private SimTickDriver _tickDriver;
        private CompositionRootServices _services;
        private PlayerController _player;

        /// <summary>World 场景的加载句柄(Phase 2 拆序第 4 步 <c>UnloadSceneAsync</c> 所需;
        /// 本版默认 releaseMode = 场景卸载时自动释放,常驻不等于泄漏)。</summary>
        private AsyncOperationHandle<SceneInstance> _worldLoadHandle;

        private bool _booted;

        /// <summary>
        /// 只缓存引用 / 初始化字段 —— **零异步、零装配**(装配严格在 <see cref="Start"/> 启动序第 2 步)。
        /// </summary>
        private void Awake()
        {
            // 字段默认值即所需;此处刻意不 Assemble(启动序顺序不可改)。
            _booted = false;
        }

        /// <summary>
        /// 启动序(见文件头四步;顺序不可改)。失败一律 rethrow —— 停在错误态,
        /// 不降级为 null 解引用(E-13 启动期硬失败)。
        /// </summary>
        private async void Start()
        {
            // ── 1. Addressables 显式初始化(E-13 硬失败)──
            // ⚠️ 本工程 Addressables 2.10.3 的 handle **不可直接 `await`**(无 GetAwaiter),
            //    await 面 = `handle.Task`(AsyncOperationHandle.Task 属性);且无参
            //    `InitializeAsync()` 默认 autoReleaseHandle = true ⇒ 完成后 handle 即失效,
            //    再读 Status 会抛「invalid operation handle」(U1 spike 坑 A 同源)。
            //    故:显式 autoReleaseHandle: false + await Task + 读 Status + 成功后自己 Release。
            var initHandle = Addressables.InitializeAsync(autoReleaseHandle: false);
            try
            {
                await initHandle.Task;
                if (initHandle.Status == AsyncOperationStatus.Failed)
                    throw new InvalidOperationException(
                        "Addressables.InitializeAsync() 返回 Failed" +
                        (initHandle.OperationException != null
                            ? ":" + initHandle.OperationException.Message
                            : string.Empty));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootRoot][E-13] Addressables 初始化失败 —— 启动终止,不降级:{e}");
                throw;
            }
            finally
            {
                // 我们持有的 init 句柄,用完即释(防泄漏)—— 放 finally:成功与失败路径都释放
                // (评审代码面 F1:原裸 Release 在 try/catch 之外,失败路径泄漏句柄)。
                Addressables.Release(initHandle);
            }

            // ── 2. 生产装配(fail-loud:任一依赖缺失 = 具名异常,无半装配袋)──
            _services = CompositionRoot.Assemble();

            // ── 3. World 场景 additive 加载(ADR-023:永不用 LoadSceneMode.Single)──
            // ⚠️ 本版 `LoadSceneAsync` **没有 `autoReleaseHandle` 形参**(U1 spike 坑 A 的
            //    autoReleaseHandle 属 `UnloadSceneAsync`,不是 Load)。可用的等价语义 =
            //    默认 `SceneReleaseMode.ReleaseSceneWhenSceneUnloaded`:场景卸载时自动释放
            //    该 handle —— 既不泄漏(卸载即释放),又保住 ADR-023 §⑤ 拆序第 4 步
            //    `UnloadSceneAsync(handle)` 所需的句柄;Phase 1 不卸载 ⇒ handle 常驻是
            //    设计而非泄漏。**场景内实例不手动 Release**(坑 B:Addressables 自管)。
            try
            {
                _worldLoadHandle = Addressables.LoadSceneAsync("world", LoadSceneMode.Additive);
                await _worldLoadHandle.Task;
                if (_worldLoadHandle.Status == AsyncOperationStatus.Failed)
                    throw new InvalidOperationException(
                        "LoadSceneAsync(world) 返回 Failed" +
                        (_worldLoadHandle.OperationException != null
                            ? ":" + _worldLoadHandle.OperationException.Message
                            : string.Empty));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootRoot][E-13] World 场景 additive 加载失败(address = world):{e}");
                throw;
            }

            // ── 4. 玩家生成 ──
            // tick 驱动在此构造(纯 C# 装配件,非异步;每帧由 Update 泵)。
            _tickDriver = new SimTickDriver();

            var playerObject = new GameObject("Player");
            _player = playerObject.AddComponent<PlayerController>(); // RequireComponent 自动补 CharacterController

            // 玩家场景归属钉死:玩家常驻 Boot 场景 —— Phase 2 拆序第 5 步
            // `UnloadSceneAsync(World)` 不得波及玩家(ADR-023 §⑤;`new GameObject` 默认落在
            // 当前活动场景,World 刚 additive 加载后可能成为活动场景 ⇒ 须显式搬回 Boot)。
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                playerObject, gameObject.scene);

            // actorId:玩家 id 走 IIdAuthority 机制 A —— 受伤实体计数器「病人 / 敌人 / 玩家
            // 共用同一空间」(IdAuthority 头注 + ADR-006 Amendment B 第二十六批:不新开第二计数器;
            // entities.yaml 的 next_player_id 是**存档头字段位**,不新增第三个 C# 计数器)。
            int actorId = _services.IdAuthority.NextPatientId().Value;

            _player.Initialize(
                SimAuthorityMode.Host,
                _services.EventSink,
                _tickDriver,
                _services.Encoder,
                actorId);

            // 落到世界原点上方 1 m(CharacterController 需落地;落地表现后续 PlayMode 验)。
            _player.Teleport(new Vector3(0f, 1f, 0f));

            _booted = true;
        }

        /// <summary>
        /// 每帧 tick 泵:把墙钟折成整 tick,并按实际发生的 tick 数逐个触发订阅者的 tick 边沿。
        /// <para>Phase 1 只接 PlayerController(其公开每 tick 入口 = <c>OnTickEdge</c>);
        /// 其余已装配订阅者(sim Step / 疾病求值 / 体征查询)归 Phase 2 —— 本阶段刻意不接。</para>
        /// <para><b>已登记缺口(不静默)</b>:生产代码 <c>OnPositionSample</c> / <c>OnUplinkSample</c>
        /// <b>零调用点</b> ⇒ <c>ActorCellEntered</c> 写者(PlayerController / CellTransitionDetector)
        /// 运行期恒不发事件;采样喂入(输入接线)归 Phase 1 尾 / Phase 2 —— 此处登记,
        /// 不作为「已接线」读。</para>
        /// </summary>
        private void Update()
        {
            if (!_booted || _tickDriver == null || _player == null) return;

            int steps = _tickDriver.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < steps; i++)
            {
                _player.OnTickEdge();
            }
        }

        /// <summary>
        /// 退出清理:Phase 1 **不做存档、不 flush、不卸载场景**(ADR-023 §⑤ 拆序六步与
        /// ADR-010 §六 定期 checkpoint 归 Phase 2)。
        /// </summary>
        private void OnDestroy()
        {
            _booted = false;
            _player = null;
            _services = null;
            _tickDriver = null;
        }
    }
}
