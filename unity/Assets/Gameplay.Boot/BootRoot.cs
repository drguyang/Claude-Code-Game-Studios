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
using DaYiJingCheng.Gameplay.Presentation.Camera;
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
        private CameraRig _cameraRig;

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

            // ── 5. 相机机位装配(sprint-05 T1.5 · 2026-10-10 AudioListener 泄漏修复)──
            // ADR-020:相机 = 自建机位,不引入 Cinemachine
            // 跟随 = 每帧把相机位置设到玩家位置 + 偏移
            //
            // ⚠️ **不自加 AudioListener**(ADR-020 §七 / AC-20-10 单挂点铁律):
            //   T1.5 初版在此 `AddComponent<AudioListener>()`,而 Boot.unity 预摆的
            //   `Main Camera`(Transform + Camera + AudioListener + URP data)**已有一个**
            //   ⇒ 场景内恒 2 个 listener,Unity 每帧刷一条警告。
            //   实测代价(2026-10-10 人工 playtest):Editor.log 31868 条同文本,
            //   Console 被淹 → Editor IPC 管道超载 → `write EPIPE` → 进程异常终止,
            //   表现为「无法退出 Play / 点关闭无响应」。根因是日志洪泛,不是死循环。
            //   修复 = 单挂点:本节**禁用**预摆相机(它只持 listener 语义),
            //   运行时相机由 CameraRig 自持;AudioListener 恒 1 个(预摆那个)。
            var presetCamera = GameObject.Find("Main Camera");
            if (presetCamera != null)
                presetCamera.SetActive(false);

            var cameraObj = new GameObject("CameraRig");
            _cameraRig = cameraObj.AddComponent<CameraRig>();
            cameraObj.AddComponent<UnityEngine.Camera>();
            _cameraRig.SetModeForTest(CameraMode.Explore);

            // fail-loud(AudioListener 单挂点 · AC-20-10):>1 即抛。
            // 把「运行期每帧刷 3 万条日志 →  Editor 卡死」的失效模式提前成具名异常。
            AssertSingleAudioListener();

            _booted = true;
        }

        /// <summary>
        /// AudioListener 单挂点断言(ADR-020 §七 / AC-20-10)—— **fail-loud**。
        /// <para><b>为何必须是断言而不是依赖 Unity 的警告</b>:Unity 对多 listener
        /// 只发一条**每帧重复**的警告(不抛、不阻断)。2026-10-10 实测:该警告刷满
        /// Editor.log 31868 条,Console 管道超载后 Editor 进程以 <c>write EPIPE</c>
        /// 异常终止 —— 人工表现为「无法退出 Play / 点关闭无响应」。
        /// 一条不可抛的警告足以杀死进程 ⇒ 必须由我方在启动序内把它变成具名异常。</para>
        /// <para><b>为何是启动序末而不是 Awake</b>:本章第 5 步才装配相机,
        /// Awake 时场景内的 listener 集尚未反映运行期装配结果。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">场景内 AudioListener ≠ 1 个。
        /// 含 0 个(无听音点)与 ≥2 个(多挂点)—— 两者都是配置错误。</exception>
        private static void AssertSingleAudioListener()
        {
            // ⚠️ 必须用 `FindObjectsInactive`(含 inactive),**不是** `FindObjectsOfType`(只查 active):
            //    启动序先 `presetCamera.SetActive(false)` 再断言 —— 预摆相机被禁用后,
            //    listener 仍应参与计数(它就是唯一合法的那个)。实测(2026-10-10):
            //    用含 active-only 的 FindObjectsOfType 得 0(预摆已禁用、CameraRig 不加)
            //    ⇒ 断言把**正确形态**判成错误。IncludeInactive 才是「挂载数」语义。
            var listeners = Resources.FindObjectsOfTypeAll<UnityEngine.AudioListener>();
            if (listeners == null || listeners.Length != 1)
            {
                int n = listeners?.Length ?? 0;
                throw new InvalidOperationException(
                    $"AC-20-10 AudioListener 单挂点:场景内应有**恰好 1 个**,实际 {n} 个。" +
                    "多挂点 = Unity 每帧刷警告 → Console 管道超载 → Editor 进程 EPIPE 终止" +
                    "(2026-10-10 实测 31868 条);零挂点 = 无听音点。" +
                    "正确形态:Boot.unity 预摆 Main Camera 持唯一 listener," +
                    "CameraRig 运行时相机**不加** listener。");
            }
        }

        /// <summary>
        /// 每帧帧泵(薄壳):读移动输入 → <see cref="MovementFeed.PumpFrame"/>(喂入 / 采样 /
        /// tick 推进 / 边沿提交的次序全在那)。
        /// <para>接线面 = 两个订阅者:① PlayerController(其公开每 tick 入口 = <c>OnTickEdge</c>);
        /// ② <c>DiseaseVitalsService.OnTickEdge</c>(批次 C · 体征链核心,2026-10-09 接入)——
        /// 每个 tick 边沿在 ① **之后**驱动 ②,语义 = <b>体征在 tick 边沿后可见</b>,
        /// 且本边沿写入的病史事件在**本 tick** 内进求值(次序与 tick 回推的可测身全在
        /// <see cref="MovementFeed.PumpFrame"/>,本类只做转发)。仍待 Phase 2:全局 sim Step(非疾病侧)。</para>
        /// <para><b>2026-10-09 采样缺口已闭</b>:原登记「<c>OnPositionSample</c> / <c>OnUplinkSample</c>
        /// 零生产调用点 ⇒ <c>ActorCellEntered</c> 运行期恒不发事件」—— 现由
        /// <see cref="MovementFeed.ProcessMovementFrame"/> 提供唯一生产调用点
        /// (Move 后立刻采样)。<c>OnUplinkSample</c> 仍零调用点是**设计**:它是 Client 模式的
        /// 上行缝,联机(P1b · 45)才接,单机 Host 模式永不走它。</para>
        /// <para>急救动作不在此读(ADR-011 §二 独立直读通道);本文件只接普通移动。</para>
        /// </summary>
        private void Update()
        {
            if (!_booted || _tickDriver == null || _player == null) return;

            // 先跑 tick 泵(Advance + 逐边沿),再按推进后的 CurrentTick 驱动病人出现 ——
            // 次序不可反:驱动读的是**本帧推进后**的 tick;若在 PumpFrame 之前调,
            // CurrentTick 恒 0(首帧未 Advance)⇒ 驱动把 _lastSpawnTick 钉在 0,
            // 之后每个新 tick 都满足 `tick - 0 < 间隔` ⇒ 永不 spawn(2026-10-10 实测红)。
            // 帧节奏注入(sprint-05 T1.1):captureDeltaTime 只影响 Time.deltaTime,
            // **不传导到 unscaledDeltaTime**(2026-10-10 实测:tick Δ=0 恒不推进)。
            // 而 tick 泵吃的是 unscaledDeltaTime ⇒ 需要时显式注入固定步长,令
            // 「batch 快帧」与「编辑器 16.7ms 真帧」在 tick 语义上等价。
            // ⚠️ 只在 capture 激活时注入(生产路径永远走真实墙钟,不受影响)。
            double delta = Time.unscaledDeltaTime;
            if (Time.captureDeltaTime > 0d)
                delta = Time.captureDeltaTime;

            long tickBefore = _tickDriver.CurrentTick;
            MovementFeed.PumpFrame(_player, _tickDriver,
                                   MovementInputReader.ReadMoveAxis(),
                                   delta,
                                   StepVitals);

            // 病人出现驱动:仅在**本帧真推进了 tick** 时驱动(未推进 = 无 tick 边沿)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientAppearedDriver != null)
            {
                _services.PatientAppearedDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 病例开账驱动(sprint-05 T1.2):在同一 tick 边沿序列内,病人出现驱动**之后**调用
            // 次序 = 病人先出现(DiseaseOnset 入流)→ 再开案(CaseOpened 入流)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.CaseOpenedDriver != null)
            {
                _services.CaseOpenedDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 急救链接线驱动(sprint-05 T1.3):在同一 tick 边沿序列内,开案驱动**之后**调用
            // 次序 = 病人先出现 → 再开案 → 最后触发急救(EmergencyAttempt + EmergencyTreatmentApplied 入流)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.EmergencyAttemptDriver != null)
            {
                _services.EmergencyAttemptDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 病人视觉实体(sprint-05 T2.0):急救驱动**之后**调用 —— 视觉跟着在场集走,
            // 与写者次序解耦(视觉是派生态,不参与事件入流次序)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientVisualSpawner != null)
            {
                _services.PatientVisualSpawner.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 相机跟随(sprint-05 T1.5):每帧把相机位置设到玩家位置 + 偏移
            // ADR-020:相机 = 自建机位,不引入 Cinemachine
            if (_cameraRig != null && _player != null)
            {
                var cam = _cameraRig.Camera;
                if (cam != null)
                {
                    Vector3 target = _player.transform.position;
                    cam.transform.position = _cameraRig.GetCameraPosition(target);
                    cam.transform.LookAt(target);
                }
            }
        }

        /// <summary>
        /// tick 边沿第二驱动口(批次 C):体征链 Step —— 在同边沿 <c>PlayerController.OnTickEdge</c>
        /// **之后**调用(见 <see cref="MovementFeed.PumpFrame"/> 次序头注)。
        /// </summary>
        /// <remarks>薄转发:本类不持游戏状态(ADR-013 §9 C3),病程真源在事件流。</remarks>
        /// <param name="tick">该边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        private void StepVitals(long tick)
        {
            if (_services == null || _services.VitalsService == null) return;
            _services.VitalsService.OnTickEdge(tick);
        }

        /// <summary>
        /// 退出清理:Phase 1 **不做存档、不 flush、不卸载场景**(ADR-023 §⑤ 拆序六步与
        /// ADR-010 §六 定期 checkpoint 归 Phase 2)。
        /// </summary>
        private void OnDestroy()
        {
            // 病人视觉实体清理(sprint-05 T2.0):视觉根节点由 PatientVisualSpawner 自持,
            // 不靠层级继承(Boot 场景常驻,看不见 World 卸载)⇒ 显式调用销毁面。
            _services?.PatientVisualSpawner?.DestroyAll();

            _booted = false;
            _player = null;
            _services = null;
            _tickDriver = null;
        }
    }
}
