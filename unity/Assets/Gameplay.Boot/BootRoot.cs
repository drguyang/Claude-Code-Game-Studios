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
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;               // EventSystem(B-6-1:官方 UI 输入桥宿主)
using UnityEngine.InputSystem;                 // InputSystem.actions(project-wide 动作资产)
using UnityEngine.InputSystem.UI;              // InputSystemUIInputModule(ADR-013 §F1 官方桥)
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;                 // VisualElement(面板 root 取用)

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
        private SkeuoRuntimeDriver _uiDriver;
        private InteractionInputDriver _interactionDriver;

        /// <summary>官方 UI 输入桥宿主(B-6-1)。<see cref="EnsureEventSystem"/> 建或复用;
        /// 面板 <c>selectableGameObject</c> 就绪后由 <see cref="BootstrapEventSystemSelection"/> 选中。</summary>
        private EventSystem _eventSystem;

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
            // ⚠️ 2026-10-12 C3-B1:data-core 预载 + 带 provider 装配 —— 原裸
            //   `Assemble()` 使 S1.2 的 Addressables 生产分支成为死代码。
            //   现生产恒走乙案(Addressables 装载同一份 cooked);测试/未重建 catalog
            //   环境 TryPreload 失败 ⇒ 组合根磁盘兜底(同一份字节,真装载)。
            //   生产构建乙案失败 ⇒ 磁盘兜底路径不存在(DataCooked 不进构建)⇒ 仍 E-13 硬失败。
            bool preloaded = DataCorePreloader.TryPreload();   // 首次 Step 之前(ADR-014 §五)

            // ── 2.5 项目级动作资产 fail-loud(story-009 R-9.5 · AC-9-01)──
            // 通道装载 = InputSystem.actions(原稿 Addressables 路径被实测推翻:该资产是
            // 项目级设置、git-tracked;Addressables 装载同资产 = 第二实例,违 AC-3-A1)。
            // 缺失即抛(E-13 同口径),不静默降级为零急救输入 + 零 UI 输入。
            var inputActions = InputSystem.actions;
            if (inputActions == null)
                throw new InvalidOperationException(
                    "[BootRoot][story-009] InputSystem.actions 为 null —— 项目级动作资产未注册。" +
                    "检查 ProjectSettings/EditorBuildSettings.asset 的 com.unity.input.settings.actions" +
                    "(期望 guid 052faaac586de48259a63d0c4782560b)。急救直读通道与 UI 输入将不可用。");

            _services = CompositionRoot.Assemble(
                registry: null,
                dataProvider: preloaded ? DataCorePreloader.Provider : null,
                inputActions: inputActions);

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
            // tick 驱动由组合根创建(ADR-005 抽象点 · G2.2 起墨龄同源;纯 C# 装配件,
            // 非异步;每帧由 Update 泵)。⚠️ 不再本处 new —— 服务袋是 tick 唯一出处。
            _tickDriver = _services.TickDriver;

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

            // ── 4.5. 玩家交互输入驱动(sprint-07 S2):E 查体 / F 急救,每帧由 Update 转发 ──
            var interaction = new InteractionInputDriver(
                _services,
                () => _player != null ? _player.transform.position : Vector3.zero,
                actorId);
            _interactionDriver = interaction;

            // ── 4.55. 急救直读通道接线(story-009 · AC-9-01)──
            // Attach = 挂 onAfterUpdate + Emergency 动作解析 fail-loud(FindAction 落空 ⇒
            // 通道抛 InvalidOperationException)—— 启动期炸,不带病运行。
            _services.EmergencyChannel.Attach();

            // ── 4.6. 方笺页接线宿主(S2.3/S2.4):SetAuthor(玩家 actorId 已定)──
            // 落笔 / 开方的作者 = 玩家;至此 PrescriptionPageHost 可接动作。
            // ⚠️ 绑定病人由查体驱动(InteractionInputDriver 的 E 查体已切脉案绑定)
            //    —— 方笺页落笔 / 开方对象 = 当前脉案绑定病人(42 侧会话)。
            _services.PrescriptionPage?.SetAuthor(actorId);

            // ── 4.65. 急救跳过入口写路径接线(story-009 · R-9.4)──
            // 候选列表「跳过」项 Submit → EmergencyManualDriver.TryManual(method 1)。
            // actorId 由 BootRoot 生成玩家后注入(SetAuthor 同款后置形态 —— 装配期未知)。
            _services.CasebookCommands?.BindEmergencySkip(_services.EmergencyManualDriver, actorId);

            // ── 4.7. 病名册观看者(sprint-08 S1.1 件 E):SetViewer(玩家 actorId 已定)──
            // 病名册条目集按人分册(铁律⑤「每人一本」)—— 观看者 = 玩家。
            // 至此 42 脉案源可读病名册(未设 ⇒ 病名册空,反幻想二)。
            _services.CasebookSource?.SetViewer(actorId);

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

            // ── 6. 拟物 UI 运行时装配(sprint-05 T2.1 / T2.2)──
            // ADR-023 §①:UI 载体由运行期生成。**例外(prefab 空壳)**:Boot.unity 预摆的
            //    `SkeuoUI`(Transform + UIDocument + SkeuoRuntimeDriver)只持**渲染资源**
            //    (PanelSettings),与预摆 Main Camera 只持 AudioListener 同型 —— 零游戏状态、
            //    零 Screen 实例(BuildUI 在 Awake 里由 driver 跑)。
            //    ⚠️ 为何必须有预摆:2026-10-10 实测(PlayMode 探针)UIDocument 无 PanelSettings
            //       ⇒ `rootVisualElement.panel == null` ⇒ **逻辑树在但不渲染**。因此预摆不是
            //       「第二真源」,是让 panel 非 null 的唯一途径(运行时脚本无法凭空造图标资源);
            //    真 Screen / 真绑定 / 真值区仍在运行期,预摆不参与任何一态。
            _uiDriver = FindOrCreateSkeuoUi();
            if (_services.CasebookSource != null)
                _uiDriver.Source = _services.CasebookSource;
            // ── 6.1 写通道注入(sprint-08 S1.1 件 F1):病名栏 Submit → 装配层写者 ──
            // 42 只渲染不持状态(ADR-013 §9 C3)⇒ 病名栏 Submit 经命令面转发给装配层的
            // CasebookCommandRouter(实现 ICasebookCommandSink)。缺注入 ⇒ Submit 留空(不崩)。
            if (_services.CasebookCommands != null)
                _uiDriver.CommandSink = _services.CasebookCommands;

            // ── 6.5. 急救候选列表挂载(S3.1 · 2026-10-11)──
            // 形态件③「运行期有载体」半边:经组合根的 EmergencyCandidateRuntime 把候选
            // 列表(止血/通气 + 跳过入口)挂到 UI 根。挂载 = 纯渲染资源(42 C3)。
            // ⚠️ 42 侧 future 若把候选列表并入某个 Screen,此处只改挂点,载体语义不变。
            if (_services.EmergencyCandidates != null)
                _services.EmergencyCandidates.MountTo(_uiDriver.GetUiRoot());

            // ── 6.6. 急救候选列表交互接线(story-009 · R-9.4)──
            // 「跳过」项 Submit(键盘 / 鼠标双路径,42 侧 Guarded 回调)→ 命令路由。
            // 候选项动作语义归 21a/10 真动作表轮 —— 本 story 只接跳过。
            if (_services.EmergencyCandidates != null && _services.CasebookCommands != null)
                _services.EmergencyCandidates.SetSkipCallback(
                    () => _services.CasebookCommands.SubmitEmergencySkip());

            // ── 6.7. 官方 UI 输入桥装配(B-6-1 · 2026-10-13)──
            // ADR-013 §F1/F4 + ADR-011 §三:UI Toolkit 平面栈的指针/导航事件经**官方桥**
            // (`EventSystem` + `InputSystemUIInputModule`)—— 此前启动序从未装配它,导致
            // 脉案交互面(病名栏 Submit / 页缘翻页 / 戥子选档)**鼠标与键盘双路径全断**
            // (第六轮 playtest B-6-1)。缺 EventSystem 时 UI Toolkit 会退回引擎内置
            // `DefaultEventSystem`,但 ADR-013 明文要官方桥 ⇒ 本步把它立起来。
            _eventSystem = EnsureEventSystem();

            // fail-loud(AudioListener 单挂点 · AC-20-10):>1 即抛。
            // 把「运行期每帧刷 3 万条日志 →  Editor 卡死」的失效模式提前成具名异常。
            AssertSingleAudioListener();

            _booted = true;
        }

        /// <summary>
        /// 取 / 建拟物 UI 运行宿主(场景预摆优先,缺则自建)。
        /// <para>预摆 = Boot.unity 的 <c>SkeuoUI</c>(持 UIDocument + PanelSettings);
        /// 缺失 = 测试里没造 fixture ⇒ 自建(此时无 PanelSettings ⇒ 逻辑树在、不渲染,
        /// 见 <see cref="AssertSingleAudioListener"/> 旁注的实测记录)。</para>
        /// </summary>
        private SkeuoRuntimeDriver FindOrCreateSkeuoUi()
        {
            var existing = UnityEngine.Object.FindObjectOfType<SkeuoRuntimeDriver>();
            if (existing != null)
                return existing;

            var uiObj = new GameObject("SkeuoUI");
            UnityEngine.Debug.LogWarning(
                "[BootRoot] Boot.unity 未预摆 SkeuoUI(UIDocument + PanelSettings)—— " +
                "自建空壳 ⇒ UI 逻辑树在但不渲染(panel == null,2026-10-10 实测)。" +
                "生产场景应保留预摆;测试环境可忽略本警告。");
            return uiObj.AddComponent<SkeuoRuntimeDriver>();
        }

        /// <summary>
        /// 取 / 建官方 UI 输入桥(<c>EventSystem</c> + <c>InputSystemUIInputModule</c>)—— B-6-1。
        /// <para>预摆优先(测试 / 未来场景),缺则运行期自建(承 <see cref="FindOrCreateSkeuoUi"/> 先例)。
        /// 只挂官方桥,**禁 <c>StandaloneInputModule</c>**(ADR-013 §F4:双输入模块 = 真冲突)。</para>
        /// <para><b>fail-loud</b>(承 <see cref="AssertSingleAudioListener"/> 口径):动作资产缺失或
        /// 引用不全即抛 —— **不静默降级为「零 UI 输入」**(那正是 B-6-1 的失效形态)。</para>
        /// </summary>
        private EventSystem EnsureEventSystem()
        {
            // 含 inactive:预摆件可能被禁用,仍是「唯一合法挂载点」(承 AudioListener 断言的 IncludeInactive 纪律)。
            var existing = UnityEngine.Object.FindObjectOfType<EventSystem>(includeInactive: true);
            if (existing != null)
            {
                var presetModule = existing.GetComponent<InputSystemUIInputModule>();
                if (presetModule == null)
                    throw new InvalidOperationException(
                        "[BootRoot][B-6-1] 场景内 EventSystem 未挂 InputSystemUIInputModule —— " +
                        "UI Toolkit 面板将收不到任何指针/导航事件(鼠标键盘全断)。" +
                        "ADR-013 §F4 禁 StandaloneInputModule 与官方桥并存/替代。");
                AssertUiActionsWired(presetModule, "预摆");
                return existing;
            }

            var go = new GameObject("EventSystem");
            // ⚠️ 必须归 BootRoot:new GameObject 落**当前活动场景**(启动序第 3 步 additive
            //    载入 World 后可能成为活动场景)⇒ 不归 Boot 则 Phase 2 卸载 World 会带走它;
            //    reparent 后随 BootRoot 一同销毁 ⇒ 免测试间泄漏(承 Player 的 MoveGameObjectToScene
            //    同纪律,此处用 SetParent 更简 —— 跨场景 reparent 自动随父进 Boot 场景)。
            go.transform.SetParent(transform, worldPositionStays: false);

            var es = go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();

            var actions = InputSystem.actions;
            if (actions == null)
                throw new InvalidOperationException(
                    "[BootRoot][B-6-1] InputSystem.actions 为 null —— 项目级动作资产未注册。" +
                    "检查 ProjectSettings/EditorBuildSettings.asset 的 com.unity.input.settings.actions" +
                    "(期望 guid 052faaac586de48259a63d0c4782560b)。UI 输入将不可用。");

            // ⚠️ **顺序敏感**:AddComponent 在 active GO 上会**立即**触发 OnEnable →
            //    HasNoActions()==true → AssignDefaultActions() 填好 8 个引用;此后本赋值才能经
            //    UpdateReferenceForNewAsset 按**同名**重指向到项目资产(它只重指向已有引用、
            //    **不填充空引用**:oldAction==null ⇒ return null)⇒ 若先赋值后 AddComponent(或
            //    inactive 上建),8 引用全 null = **静默零输入**。故下面必须断言,不得只断 actionsAsset。
            module.actionsAsset = actions;
            AssertUiActionsWired(module, "自建");
            return es;
        }

        /// <summary>UI 动作引用完整性(fail-loud):8 个引用缺任一 ⇒ UI 输入静默失效。</summary>
        private static void AssertUiActionsWired(InputSystemUIInputModule module, string origin)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (module.point?.action == null)       missing.Add("Point");
            if (module.move?.action == null)        missing.Add("Navigate");
            if (module.submit?.action == null)      missing.Add("Submit");
            if (module.cancel?.action == null)      missing.Add("Cancel");
            if (module.leftClick?.action == null)   missing.Add("Click");
            if (module.middleClick?.action == null) missing.Add("MiddleClick");
            if (module.rightClick?.action == null)  missing.Add("RightClick");
            if (module.scrollWheel?.action == null) missing.Add("ScrollWheel");

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"[BootRoot][B-6-1] {origin} InputSystemUIInputModule 动作引用不齐,缺:{string.Join(", ", missing)}。" +
                    "InputSystemUIInputModule.actionsAsset setter 只**重指向已有引用**、**不填充空引用**" +
                    "(UpdateReferenceForNewAsset: oldAction==null ⇒ return null)⇒ 引用为空 = 该动作永不派发。" +
                    "正确形态:自建时确保 AddComponent 先于 actionsAsset 赋值(OnEnable 的 AssignDefaultActions 打底);" +
                    "或预摆时在 Inspector 填好 8 引用(照 SpikeAssump6.unity)。");
        }

        /// <summary>
        /// 面板 <c>selectableGameObject</c> 就绪后把它选为当前 UI 对象(B-6-1 · 守「零初始选中」死锁)。
        /// <para><b>为何必需</b>:<c>InputSystemUIInputModule.ProcessNavigation</c> 的 move 与 submit
        /// **都 gate 于 <c>currentSelectedGameObject != null</c>**,而它只在元素**已获焦点**时由
        /// <c>PanelEventHandler.OnElementFocus</c> 设 ⇒ 无初始选中 = 键盘导航/提交**永不派发**。
        /// 面板载体 GO 由 interop bridge **运行期**新建(Boot 时不存在)⇒ <c>firstSelectedGameObject</c>
        /// 无法预摆,只能在面板出现后 bootstrap 选中。</para>
        /// <para>⚠️ **不违反 AC-42-C10**(42 不自设初始焦点):本步选的是**面板载体**,
        /// <c>PanelEventHandler.OnSelect</c> 只做面板级 <c>panel.Focus()</c>(<c>focusedElement</c> 仍为 null),
        /// 首次 <c>NavigationMoveEvent</c> 才落焦到具体控件。</para>
        /// <para><b>鼠标路径不依赖本步</b>:指针事件经 <c>PanelRaycaster</c> 直接命中,不经此门
        /// ⇒ 即便本步未及时执行,鼠标点击仍可用。</para>
        /// </summary>
        private void BootstrapEventSystemSelection()
        {
            if (_eventSystem == null || _uiDriver == null)
                return;

            var root = _uiDriver.GetUiRoot();
            // ⚠️ 必须转 **`IRuntimePanel`**(public):`VisualElement.panel` 声明为 `IPanel`,
            //    而 `selectableGameObject` 只在 `IRuntimePanel`(public 接口,运行期面板实现它)
            //    与 `BaseRuntimePanel`(**internal**,够不着)上 —— 用 `BaseRuntimePanel`
            //    会 CS0122(inaccessible due to its protection level,2026-10-13 实测编译红)。
            //    面板未 attach(自建空壳 / 未渲染)⇒ 转型失败 ⇒ 跳过。
            if (!(root?.panel is IRuntimePanel panel))
                return;

            var selectable = panel.selectableGameObject;
            if (selectable == null)
                return;   // bridge 尚未建面板 GO(下一帧 Update 才建)⇒ 下次调用再试

            if (_eventSystem.currentSelectedGameObject != selectable)
                _eventSystem.SetSelectedGameObject(selectable);
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

            // 急救直读通道中止判据(story-009 · 规则六之甲):同一 tick 边沿序列内检查
            // Armed 闲置 —— 按住语义下即「按住超阈值 ⇒ 中止,零事件」(R-9.3)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.EmergencyChannel?.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 病人视觉实体(sprint-05 T2.0):急救驱动**之后**调用 —— 视觉跟着在场集走,
            // 与写者次序解耦(视觉是派生态,不参与事件入流次序)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientVisualSpawner != null)
            {
                _services.PatientVisualSpawner.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 玩家交互输入(sprint-07 S2):E 查体 / F 急救 —— **视觉实体更新之后**(读的是
            // 本 tick 的在场集+视觉位置;换热须已本 tick Step),拟物 UI 刷新之前
            // (查体后本帧即见值区有词)。
            _interactionDriver?.OnFrame();

            // 拟物 UI 值区(sprint-05 T2.1/T2.3):视觉实体**之后**调用 —— UI 读的是
            // **本 tick 已推进完**的体征(Driver.Bind 的源零缓存,每次读都过体征链)。
            // 次序不可反:若在体征 Step 前刷新,同一 tick 里 UI 显示的是上一 tick 的读数。
            if (_tickDriver.CurrentTick > tickBefore && _uiDriver != null)
            {
                _uiDriver.OnTickEdge();
            }

            // UI 输入桥初始选中(B-6-1):面板 selectableGameObject 由 interop bridge 在
            // 面板 attach 后的某帧新建 ⇒ 每帧幂等尝试,直到选中成功(成功即 no-op)。
            // ⚠️ 必须在面板就绪后 —— 故放 Update 而非启动序(启动序时面板 GO 可能未建)。
            BootstrapEventSystemSelection();

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

            // 急救直读通道卸挂(story-009):onAfterUpdate 是全局静态回调,必须在
            // BootRoot 销毁时显式 Detach,否则域内悬挂委托引用已弃通道(下次 Play 泄漏)。
            _services?.EmergencyChannel?.Detach();

            _booted = false;
            _player = null;
            _services = null;
            _tickDriver = null;
            _uiDriver = null;
            // EventSystem 随 BootRoot 一同销毁(reparent 到 transform ⇒ 无需显式 Destroy);
            // 仅清引用。承 FindOrCreateSkeuoUi 同纪律。
            _eventSystem = null;
        }
    }
}
