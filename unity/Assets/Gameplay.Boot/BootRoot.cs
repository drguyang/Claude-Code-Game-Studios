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
//   2.95 主菜单闸门(仅在 BootWithMainMenu 时):additive 载入 "mainmenu" → 摊 42 主菜单屏
//      → await 玩家确认 → additive 拆回该场景。⚠️ 位置**必须在 3 之前**:世界尚未装载时
//      没有 tick / 病人 / 「可被挡住的世界」⇒ 它不是 `ModalId` 成员(`main-menu-42.md §1`)。
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
using System.Collections.Generic;    // IReadOnlyList<string>(A-5 错误的返回形,TD-audio-032 ③ 拆分后进签名)
using System.IO;                     // Path.Combine(7a 存档目录,S19-1)
using System.Threading.Tasks;
using DaYiJingCheng.Gameplay.Interaction;          // ModalGate(规则七两向抑制)
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Audio;      // 44 运行期音频链(story-018 装配面)
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;   // DiagnosisReadFloorEvaluator.TierIndex(L-2 切档缝日志)
using DaYiJingCheng.Gameplay.Presentation.Input;      // DeviceSwitchTuningDataSet(第十一片取值缝)
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Gameplay.Presentation.Settings;   // AudioSettingsRegistry(S14-6 注入缝)
using DaYiJingCheng.Gameplay.Presentation.Telemetry;  // TelemetryAnalyticsTuning / DataSet(第十二片取值缝)
using DaYiJingCheng.Gameplay.Presentation.Tutorial;   // TutorialNarrationRow(走查仪表逐条复查口述)
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Tutorial;             // TutorialMediaIntent(同上)
using DaYiJingCheng.Sim.Codec;                        // SaveSlot(主菜单「继续最近档」读哪一档)
using DaYiJingCheng.Sim.Contracts;                    // IDataProvider(ApplyAudioSettingsProvider 形参)
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;                              // AudioMixer(story-018 装载缝形参)
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

        /// <summary>
        /// 5 时间参数(ADR-038 §D1)。<c>null</c> = 未预载 / 无数据面 ⇒ 时间未就绪:
        /// 存档册**不画**「第 N 日 / 昼夜」(<c>DayNumber = 0</c>),而不是拿默认值静默断言一个没算过的值。
        /// </summary>
        private Sim.TimeParams _timeParams;
        private PlayerController _player;
        private CameraRig _cameraRig;

        /// <summary>
        /// 39 的档位请求载体(`casebook/story-006`)。**也作 PlayMode 的观测面** ——
        /// 口径同 `_downedCameraRequester`:判据走行为(相机真的进 `Casebook`),本字段只用于定位。
        /// </summary>
        private DaYiJingCheng.Gameplay.Presentation.Casebook.CasebookCameraRequester
            _casebookCameraRequester;

        /// <summary>29 的档位请求器(story-007;装配序 4.3b 构造)。
        /// <para>⚠️ 保留字段引用是**有意**的:它是「这段接线存在」的唯一观测面 ——
        /// PlayMode 夹具反射它,断言生产启动序真的把机位绑了上去(S29-06)。
        /// 不留引用 ⇒ 删掉绑定后没有任何东西可查。</para></summary>
        private DaYiJingCheng.Gameplay.Presentation.DeathTransition.DownedCameraRequester
            _downedCameraRequester;
        private SkeuoRuntimeDriver _uiDriver;

        /// <summary>结案页的装配层驱动(T20 第二批 · 2026-11-10)。
        /// <para><c>null</c> = 未装配(无 UI 驱动 / 无 `CaseClosers`)⇒ 按键什么都不做。</para></summary>
        private CaseClosePresenter _caseClosePresenter;
        /// <summary>存档位载入路由(42 确认动作 → <c>LoadSequencer</c>;启动序内装配)。</summary>
        private SaveSlotCommandRouter _saveSlotCommands;
        /// <summary>模态开合驱动(键沿 → 42 的 OpenModal / CloseModal;启动序内装配)。</summary>
        private ModalInputDriver _modalInput;

        /// <summary>手册近景的开合驱动(`M` / 手柄 `select`;T18 甲 · 2026-11-10)。</summary>
        private PaperCloseupToggleDriver _manualToggle;

        /// <summary>
        /// 存档位清单(7b 界面画哪几个槽)。
        /// <para>⚠️ <b>数值出处 = 7b,不是本类</b>:本轮的目的是把载入路径**接通**,
        /// 故先落一个具名清单供界面画出;真值归 7b 的数值轮,届时只改这一处。</para>
        /// <para>🔁 <b>2026-11-03(`TD-persist-17` · 用户裁定)—— 本清单**不再是「手动槽清单」**</b>:
        /// 槽号语义已改为 **1 = 退出保存 · 2 = 自动存档 · 3+ = 手动存档**,故本清单 = **册上显示的三行**,
        /// 依次是「退出那一刻 / 最近一次自动存档 / 第一个手动槽」。</para>
        /// <para>⚠️ 槽 0 **不是合法槽号**(`SaveSlotLedger.EnsureWritable` 直接拒);本清单不含它。</para>
        /// <para>⚠️ 已知呈现缺口(不借绿):手动槽从 3 起且只进不退 ⇒ 第 2 次手动存档起落在 4、5…
        /// 而本清单固定三行 ⇒ 那些槽**存得进去、看不见**。归 7b「槽位列表须由台账派生」。</para>
        /// </summary>
        private static readonly uint[] DisplayedSaveSlots = { 1u, 2u, 3u };

        /// <summary>病人 cue 的非色相可见对应物容器(`AC-13-F1` 的 42 侧达成面 · 缺口 D)。
        /// <para>⚠️ 在 <see cref="_services"/> 装配**之前**构造(它是 `Assemble` 的入参),
        /// 装配完成后再挂到 UI 根上 —— 与 <see cref="CompositionRootServices.EmergencyCandidates"/>
        /// 的「先装配、后 MountTo」同序,只是本件的缝恰好是构造参数。</para></summary>
        private DaYiJingCheng.Gameplay.UI.Skeuomorphic.PatientCueMarkView _cueMarks;

        /// <summary>语声字幕元件(`AC-44-15` `[L]` 半 · 2026-10-29)。
        /// <para>⚠️ 与 <see cref="_cueMarks"/> 同序:装配前构造(它是 `Assemble` 的入参),
        /// 装配后挂 UI 根。**不挂**则「屏幕上有字幕」这条判据在运行期不存在。</para></summary>
        private DaYiJingCheng.Gameplay.UI.Skeuomorphic.SubtitleLineView _subtitle;
        private InteractionInputDriver _interactionDriver;
        /// <summary>E 按下沿的统一路由(2026-11-02)。观测面:夹具读 `LastRoutedKind`
        /// 判「这一下打给了谁」—— 世界的查体已移除,`LastExaminedPatient` 不再由按键写。</summary>
        private InteractionPressRouter _pressRouter;
        // 🔁 2026-11-04(`TD-skeuo-06`):炮制屏的意图出口(点火 / 关屏)。
        //    此前 `IProcessingCommandSink` 生产实现数为零 ⇒ 屏上点火是**静默空操作**。
        private DriverProcessingCommandSink _processingSink;
        private ModalInputFreeze _inputFreeze;      // 模态期输入冻结(移动轴 / 建造;2026-10-31)
        private BuildInputDriver _buildDriver;      // S20-19:建造输入驱动

        /// <summary>官方 UI 输入桥宿主(B-6-1)。<see cref="EnsureEventSystem"/> 建或复用;
        /// 面板 <c>selectableGameObject</c> 就绪后由 <see cref="BootstrapEventSystemSelection"/> 选中。</summary>
        private EventSystem _eventSystem;

        /// <summary>World 场景的加载句柄(Phase 2 拆序第 4 步 <c>UnloadSceneAsync</c> 所需;
        /// 本版默认 releaseMode = 场景卸载时自动释放,常驻不等于泄漏)。</summary>
        private AsyncOperationHandle<SceneInstance> _worldLoadHandle;

        /// <summary>主菜单场景的 Addressables 地址(与 <c>Default Local Group</c> 的条目同源)。</summary>
        /// <remarks>⚠️ 与 <c>"world"</c> 同级纪律:地址串写死在调用点、条目登记在 group 资产,
        /// 两侧不一致 = 运行期 <c>LoadSceneAsync</c> 硬失败(故有 EditMode 资产级门).</remarks>
        public const string MainMenuSceneAddress = "mainmenu";

        /// <summary>
        /// 启动时是否先摊主菜单再进世界(2026-11-01 · `design/ux/main-menu-42.md`)。
        /// <para><b>生产 = true</b>(Boot.unity 序列化为 1);**代码构造的测试夹具 = false**
        /// (默认值),故既有 PlayMode 夹具的启动序一字不改。</para>
        /// <para>⚠️ 这个开关**不是**「要不要主菜单」的功能旗 —— spec 不提供关掉它的产品形态;
        /// 它是装配层给夹具留的启动序切面(与 <see cref="SaveBasePathOverride"/> 同格)。</para>
        /// </summary>
        [SerializeField] private bool _bootWithMainMenu;

        /// <summary>存档目录覆盖(夹具用;空 ⇒ <c>persistentDataPath/saves</c>)。</summary>
        /// <remarks>⚠️ 存在理由同 <see cref="BootWithMainMenu"/>:夹具须能把「有档 / 无档 / 查询失败」
        /// 三态摆出来,而生产目录是**玩家数据**(夹具往里写 = 污染,且不可复现)。</remarks>
        [SerializeField] private string _saveBasePathOverride;

        /// <summary>
        /// 存档**根目录**覆盖(夹具用;空 ⇒ <c>persistentDataPath/saves</c>)。
        /// <para>⚠️ 与 <see cref="_saveBasePathOverride"/> 是**两个不同的缝**,别混:
        /// 那个把某个目录直接当**世界目录**(单世界形态);本缝把某个目录当**存档根**
        /// (多世界形态照常:根下是 <c>w1/</c>、<c>w2/</c>…)。
        /// 存在理由:真启动序的多世界路径要有夹具能跑,而生产根目录是玩家数据。
        /// </para>
        /// </summary>
        [SerializeField] private string _saveRootOverride;

        /// <summary>
        /// 多世界的目录与索引(ADR-040)。<c>null</c> = 走了 <see cref="_saveBasePathOverride"/>
        /// 的夹具缝 —— 那种情形下「覆盖目录」**直接就是世界目录**,不建子目录、不迁移。
        /// </summary>
        private WorldCatalog _worldCatalog;

        /// <summary>本次装配用的世界存档目录(= 7a 的 <c>basePath</c>)。</summary>
        private string _worldSaveDir;

        /// <summary>菜单里选了**另一个**世界 / 新建世界时记下的目标目录;空 = 不换。</summary>
        private string _pendingWorldSaveDir;

        /// <summary>装配参数缓存 —— 菜单换世界后要**整体重装配**,参数必须留得住。</summary>
        private InputActionAsset _bootInputActions;
        private DaYiJingCheng.Sim.World.World _bootWorld;
        private bool _bootPreloaded;

        /// <summary>主菜单的「继续诊治」信号(开屏后 <c>await</c> 它)。</summary>
        private TaskCompletionSource<bool> _mainMenuContinue;

        /// <summary>主菜单场景的加载句柄(关屏时按 ADR-023 口径 additive 拆回)。</summary>
        private AsyncOperationHandle<SceneInstance> _mainMenuLoadHandle;

        /// <summary>玩家在菜单上确认「继续」时要读的那一档(<c>null</c> = 开新局)。</summary>
        private SaveSlot? _continueSlot;

        /// <summary>启动时是否先摊主菜单(序列化旗的属性面 —— 夹具在 <c>Start</c> 之前赋值)。</summary>
        public bool BootWithMainMenu
        {
            get => _bootWithMainMenu;
            set => _bootWithMainMenu = value;
        }

        /// <summary>存档目录覆盖(夹具面;见 <see cref="_saveBasePathOverride"/> 注)。</summary>
        public string SaveBasePathOverride
        {
            get => _saveBasePathOverride;
            set => _saveBasePathOverride = value;
        }

        /// <summary>存档**根目录**覆盖(夹具面;见 <see cref="_saveRootOverride"/> 注)。</summary>
        public string SaveRootOverride
        {
            get => _saveRootOverride;
            set => _saveRootOverride = value;
        }

        /// <summary>主菜单屏当前是否摊开(观测面;开屏后 <c>true</c>,玩家确认后 <c>false</c>)。</summary>
        public bool IsMainMenuOpen { get; private set; }

        /// <summary>
        /// 本次启动**实际尝试继续**的那一档(<c>null</c> = 走了「开新局」分支)。
        /// <para>⚠️ 它是**事实出口**,不是判定:只陈述「按下了继续且目标是谁」;
        /// 读档成不成功看 <see cref="ContinueError"/>。</para>
        /// </summary>
        public SaveSlot? ContinuedFromSlot => _continueSlot;

        /// <summary>启动期「继续诊治」读档抛出的异常(<c>null</c> = 没走该分支 / 成功)。</summary>
        public System.Exception ContinueError { get; private set; }

        private bool _booted;

        /// <summary>
        /// 本启动序装载的世界是否是**最小合成世界**(S20-17 · 2026-10-28)。
        /// <para>真 ⇔ <see cref="MinimalWorldFactory.Create"/> 的产物(16×1×16 全 walkable +
        /// 空槽位目录),即「世界烘焙数据尚未产出」的状态。</para>
        /// <para>⚠️ <b>这是可观测的诊断位,不是游戏状态</b>(本类不持有 sim 真值,见类注):
        /// 它只陈述「装配从哪来」。世界数据落地后由装载路径替换 ⇒ 本属性应随之为 false。</para>
        /// </summary>
        public bool IsSyntheticWorld { get; private set; }

        /// <summary>设置链(42 设置壳读它;44 渲染它 —— 两侧都不持值)。
        /// <para>⚠️ 启动序 2.65 之前为 <c>null</c>(装配未跑)。</para></summary>
        public AudioSettingsRuntime AudioSettings => _audioSettings;

        /// <summary>44 侧装配缝(story-018)—— <see cref="BuildAudioSeams"/> 的产物。
        /// <para>本类只**持有引用**用于卸载期释放(<see cref="AudioClipLibrary.Dispose"/>);
        /// cue 语义 / 状态一律不在此(ADR-013 §9 C3 · ADR-018 §一)。</para></summary>
        private AudioAssemblySeams _audioSeams;

        /// <summary>设置链(2026-10-29 · `TD-audio-105` 缺口 A)。持有引用供运行期写入;
        /// 值真源在 <see cref="AudioSettingsRuntime.Store"/>,本类不镜像任何设置值。</summary>
        private AudioSettingsRuntime _audioSettings;

        /// <summary>3 的设备态(启动序 2.9 挂接;TD-epic-01 第八片 —— cause 判据的输入面)。</summary>
        private Input.DeviceStateManager _deviceState;

        /// <summary>
        /// 退出钩子回调(AC-7a-17 · `TD-persist-06`)。**必须缓存成字段**:
        /// <c>Application.wantsToQuit</c> 是**静态**事件,每次注册都要给同一个 delegate 实例
        /// 才能注销 —— 用 lambda 直接 `+=` 之后无法 `-=`(每次求值都是新对象),
        /// 会留下悬挂引用在下次 Play 里再触发一次。
        /// </summary>
        private Func<bool> _wantsToQuitHandler;

        /// <summary>
        /// 启动期读到的「上一次会话未完整保存」标志(AC-7a-17)。
        /// <para>⚠️ **只读位、不出提示** —— GDD `:586` 逐字「提示承载归 42 / 7b,
        /// 7a 只断言标志位置位」。本字段是**交给 42 / 7b 的交接点**,不是提示本身。
        /// 在 42 / 7b 接上之前,它没有任何消费者 —— 这是**如实登记的缺口**,不是静默满足。</para>
        /// </summary>
        private bool _sessionWasIncomplete;

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

            // ── 2.4 44 侧设置注册表注入缝(S14-6 · story-010 · 兑清 story-011 §登记项)──
            // story-011 的 `AudioSettingsRegistry` 立了 `Provider` 缝并逐字登记
            // 「运行期装载面归 story-010 的 Addressables 接线轮」。本处即该轮:
            // 预载成功 ⇒ 烘焙分区(AC-44-13 真源)成为 `Rows` 的供给;
            // 预载失败 ⇒ **不注入**,`Rows` 走 `FallbackRows`(与烘焙分区同值,
            // 由 AudioSettingsRegistryBinder 的构建期双向值对账钉住)。
            // ⚠️ 这里**刻意不抛**:设置值不是 sim 真值(端本地设备偏好,OQ-SS-5=甲),
            //    「回退集」是设计内的降级面,不是静默失败 —— 与 E-13 对**数据产物**的
            //    硬失败口径不冲突(那条管的是「产物不可读」,此处是「未装配」)。
            // ⚠️ 2026-10-17 修复:`DataCorePreloader.Provider` 是**抛形**属性(null 即抛),
            //    不能无条件求值再靠形参判 null —— 预载失败时求值本身就抛,「不注入」分支不可达
            //    (PlayMode 9 红实测:静态 `IsPreloaded` 跨 Play 轮残留 ⇒ TryPreload 短路返回 true
            //    而 `_provider` 已随上一轮失效)。改三元短路:只有真成功才取 Provider,
            //    且取到的 null 仍由 `ApplyAudioSettingsProvider` 的形参检查兜住。
            ApplyAudioSettingsProvider(preloaded, preloaded ? DataCorePreloader.Provider : null);

            // ── 2.6 44 运行期音频链装配(story-018 · A-1…A-7 的启动序半边)──
            // 解析混音器 → 赋值 `MixerResolver` → 应用 `FactoryDefaults`;同时建 clip 库 +
            // 总线解析器,交给组合根把「池 → 渲染目标 → sink」接起来(A-1/A-2/A-4)。
            // ⚠️ `PlayerBusVolumeInitializer.Bootstrap()` 是 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`
            //    —— 它在**场景加载前**就跑过一遍,那时 `MixerResolver` 尚未赋值 ⇒ 静默 return。
            //    故 `BuildAudioSeams` 内赋值后**显式**调 `ApplyDefaults`(不指望自动入口二次触发)。
            // ⚠️ 预载失败(provider == null)⇒ 缝不接:`Assemble` 走 `NullRenderTarget` 链,
            //    `AudioRenderAssembled = false` 可观测 —— 不是静默失败。
            _audioSeams = BuildAudioSeams(preloaded ? DataCorePreloader.Provider : null);

            // ── 2.65 设置链装配(2026-10-29 · `TD-audio-105` 缺口 A)──────────────
            // 病因:`AudioSettingsStore` 生产**零构造点**、`MixerPushSink` **零接线**、
            //   `mono_enabled` **零消费方** ⇒ 玩家在 42 设置壳拨任何东西都不产生声学变化。
            // 本段把三件接成一条链(store → push mixer / mono 设备级)。
            // ⚠️ **次序**:在 `BuildAudioSeams` 之后(它赋值 `MixerResolver`,本段要读它),
            //   在 `CompositionRoot.Assemble` 之前(装配只消费 seams,不消费 store)。
            // ⚠️ 失败**不阻断启动**(设置是偏好面,非核心循环)—— 逐条 `Debug.LogError` 登记。
            _audioSettings = new AudioSettingsRuntime(
                sidecarDirectory: null,                                  // 默认 = persistentDataPath/settings
                mixerResolver: PlayerBusVolumeInitializer.MixerResolver);
            foreach (string err in _audioSettings.Startup())
                Debug.LogError("[BootRoot][settings] " + err);

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

            // ── 2.8 World 场景装配轮(S20-17 · 2026-10-28)────────────────────────────
            // 病因:`CompositionRoot.cs:787-796` 逐字记着「23 的 `StructureInstanceRegistry`
            //   在生产树**零构造**(`new World` 的构造同样零构造)」,并指名「`BootRoot` 的 World
            //   场景装配轮」。`impl-s20-2a-building01-2026-10-25.md:176` 的 N5 把这件事记为
            //   「本件未做」,而 `TD-building-01` 已于 2026-10-25 结案 ⇒ 缺口零载体。
            //
            // ⚠️ **为什么是「合成」而不是「装载」** —— 世界几何的真源 = ADR-022 关卡工具产出
            //    → ADR-014 烘焙的 `world_*.cooked`。今日**三样全无**(`assets/data/` 零份
            //    `world_*` JSON、DataCooked 的 25 份产物里零份世界数据),而关卡工具已被裁定
            //    「不立 epic、不排期」⇒ 装配轮**没有可装载的输入**。用户 2026-10-28 裁定边界
            //    取「最小合成世界」。详见 `MinimalWorldFactory` 的文件头。
            //
            // ⚠️ **本装配解除了哪些、没解除哪些**:
            //    解除:① `World` 在生产路径上**存在**;② `StructureWriter` 有生产实例
            //          (`Place` / `Remove` 可被调用);③ `ClinicRoomMatching` 首次非 null。
            //    **没解除**:建造的**发起方**归 42 的建造交互(`O-23-6`),今日零调用点
            //          ⇒ 「写者存在」≠「玩家能建造」;`BuildSlotCatalog` 为空 ⇒
            //          `AC-23-08` 第三半边仍无载体。逐条登记在 sprint-20 §十的卡里。
            // ⚠️ 2026-10-28(S20-20 装载面补接):**必须**传 provider —— 此前无参调用使槽位骨架
            //    只能走 `DataCookedDisk` 的磁盘路径,而 `Assets/DataCooked` 不随玩家构建出货
            //    ⇒ 生产构建的槽位目录**恒空**(`PlaceableChecker` 恒 `SlotNotFound`)。
            //    预载失败(未重建 catalog / 组条目缺)⇒ 传 null 走磁盘兜底(编辑器可读,
            //    玩家构建必失败 ⇒ 空目录 + LogError,与接线前同形,不静默)。
            // ── ① 格边长换源(`TD-audio-035` ① · 2026-10-29)────────────────────────
            // 病因:44 的空间化换算此前读 `AudioTuning.LatticeSizePlaceholder`(44 自造的 1f),
            //   而 `LATTICE_SIZE` 归**系统 6** —— `LatticeToWorld` 文件头明令「44 不得自造比例」。
            //   6 侧的载体早已存在(关卡工具 story-002 起 `world_geometry` 带 `lattice_size_mm`,
            //   经 ADR-014 烘进 `world_geometry.cooked`),但**运行期无出口** ⇒ 装配层取不到。
            //   本处接上那条出口,44 侧从此只**消费** 6 的值。
            // ⚠️ 数值本身**未定**(`P0ClinicLevel.LatticeSizeMm` 自陈「占位值,归用户数值轮」)
            //   —— 本轮改的是**来源**,不是**数值**。数值轮改 6 侧一处,44 的米制尺度随之变。
            // ⚠️ 赋值**必须在 `Assemble` 之前**:组合根在装配期一次性把该值交给
            //   `SpatializationDirector` / `AudioSourceRenderTarget`,之后再写不生效。
            var world = MinimalWorldFactory.Create(preloaded ? DataCorePreloader.Provider : null,
                                                  out int latticeSizeMm);
            AudioAssemblySeams.ApplyLatticeFromWorldGeometry(_audioSeams, latticeSizeMm);
            // ── ② 遮挡链绑定(`TD-audio-035` ② · 2026-10-29 用户裁定甲案)──────────────
            // 缝 = `LatticeOcclusionProbe.IsBlocked`(整数格 Bresenham 视线走查,**不引物理射线**);
            // 输入 = 本世界几何的 `walkable`(P0 关卡的 16×1×16 外环墙**已存在** ⇒ 不需新数据)。
            // 听者格在启动序第 5.1 步(相机装好之后)注入 —— 那时 `_cameraRig` 才存在。
            AudioAssemblySeams.BindOcclusion(_audioSeams, world.Geometry);
            // 🔁 **2026-10-29(关卡工具 story-002)此处置 `false`,原文不删**:
            //    此前恒 `true` —— 世界几何由 `MinimalWorldFactory` **合成**(16×1×16 全 walkable),
            //    因为 `world_*.json` / `world_*.cooked` 三样全无。story-002 之后
            //    `MinimalWorldFactory` 是**纯装载器**(几何 / 可走性 / 速度档全部来自烘焙产物,
            //    产物缺失即启动期硬失败),合成支已删除 ⇒ 本属性在当前生产路径上**恒 false**。
            //    ⚠️ 属性本身保留:它是 PlayMode 可断言的可观测诊断位,且 AC-4 的判据援引它。
            //    若将来重新引入合成支(须另裁,理由见 `MinimalWorldFactory` 文件头 ①②③),
            //    这里必须重新如实赋值 —— 不得让它变成「写着 false 但可能合成」。
            IsSyntheticWorld = false;

            // 缺口 D:病人 cue 的非色相可见对应物 —— 元件先造,装配后再挂 UI 根(见字段注)。
            _cueMarks = new DaYiJingCheng.Gameplay.UI.Skeuomorphic.PatientCueMarkView();
            // AC-44-15 [L]:语声字幕元件 —— 同序(先造、后挂)。
            _subtitle = new DaYiJingCheng.Gameplay.UI.Skeuomorphic.SubtitleLineView();

            // 5 时间参数(ADR-038 §D1 · 2026-11-03):预载成功 ⇒ 烘焙表 + Validate;
            // 失败 / 未预载 ⇒ null(时间未就绪,存档册不画日与昼夜 —— 不拿默认值冒充)。
            _timeParams = ResolveTimeParams(preloaded ? DataCorePreloader.Provider : null);

            // ADR-038 §R2:52 的 `ROLL_INTERVAL` 与 5 的日长必须相容 —— 启动期校验一次。
            // 这同时是 **52 数据集今日唯一的生产消费点**(其余三值待「52 运行期消费」ADR,
            // 见 ADR-038 §D7);取不到 52(未预载 / 夹具)⇒ 不判 —— 「不知道」≠「不合规」。
            RequireCrossDatasetWindow(_timeParams, preloaded ? DataCorePreloader.Provider : null);

            // 🔁 2026-11-04(ADR-040 D1):装配参数先落字段 —— 菜单里选了**另一个世界**之后
            //    要整体重装配(见 `ReassembleForWorld`),那时 `inputActions` / `world` 这些
            //    局部量已经不在作用域里。
            _bootInputActions = inputActions;
            _bootWorld = world;
            _bootPreloaded = preloaded;

            // ── 1.9 世界目录解析(ADR-040 D1 / D7)──
            // 必须在 `Assemble` **之前**:7a 的 `SaveService` 是按 `basePath` 构造的。
            _worldSaveDir = ResolveWorldSaveDir();

            _services = AssembleServices();

            // ── 2.94 53 判定链的**惰性告警**(ADR-036 D3 · 2026-10-30)──
            // 「空表 / 无数据源 ⇒ 不发」这个语义必须**可观察**,否则它与「机制未启用」同形
            // (R-3:「惰性但不静默」)。装配层不打这条日志(那里是 EditMode 的 100+ 调用方),
            // 生产启动序在此打一次。
            if (_services.ConsequenceDriver == null)
            {
                Debug.LogWarning(
                    "[BootRoot][ADR-036 D3] 53 判定链**未装配**(data-core 预载失败或 provider 为空)" +
                    "⇒ 后果事件(`ConsequenceResolved`)不会产生。这是**惰性**,不是错误;" +
                    "但与「机制未启用」同形,故在此显式告警一次。");
            }

            // ── 2.95 退出保存接线 + 会话未完整标志位(AC-7a-17 · `TD-persist-06` · 2026-10-25)──
            // 病因:`SaveService.SaveOnExit` 方法**早已存在**(`SaveService.cs:356`)但
            //   **零生产调用点** —— `Application.wantsToQuit` 全仓零命中。⇒ AC-7a-17 的
            //   「退出保存失败 ⇒ 限时重试 + 下次启动提示」整条链在**形状上**不存在。
            //
            // ⚠️ **本轮只接「退出保存 + 标志位」,不接「提示」** —— GDD `:586` 逐字
            //   「提示承载归 42 / 7b,7a 只断言标志位置位」。本类读位后**存字段**,
            //   字段的消费者(42 / 7b 的提示 UI)**尚未存在** ⇒ 这是**如实登记的缺口**,
            //   不是静默满足(见 `_sessionWasIncomplete` 的 doc)。
            //
            // ⚠️ ~~今日 `Checkpoint` / `Load` 仍必然抛~~(**2026-10-25 生产接线轮过期**,原文不删):
            //    叙事上下文瞬时源已接通(`NoPendingMedicalActionQuery`,装配根 Bind)⇒
            //    `SaveOnExit` **不再**在 `RequireNarrativeContext` 处抛,而是**真写**
            //    `save_exit.bin`(独立文件名,不占玩家槽)。本 try/catch **保留** ——
            //    它兜的「驱动源缺失 = 编程错误」分支退为纵深,写盘侧异常本就不经此抛。
            //    (原记录:兜住后走「置位 + 记日志」路径,即接线前的生产真实行为。)
            // 🔁 2026-11-04(ADR-040 D1):抽成方法 —— 菜单选了**另一个世界**之后要整体重装配,
            //    退出钩子必须解绑旧的再绑新的(否则退出时写的是上一个世界的 `SaveService`)。
            WireExitSave();

            // ── 2.9 设备态挂接(TD-epic-01 第八片 · 2026-10-19)──
            // 病因:`DeviceStateManager`(story-008)此前**零生产实例**(零生产调用方,与
            // story-008 Handover 第 7 条「四件新 API 零生产调用点」登记吻合)—— 而 10 的
            // `cause`(GDD 规则十二:无模拟量通道 ⇒ 降级)要读它。本处即该接线的启动序半边:
            //   · `Attach()` 快照 `InputSystem.devices` 并按 onDeviceChange 维护在场集;
            //   · 用 3 的 `EmergencyDeviceReadiness` 把读数交给 `EmergencyManualDriver`
            //     (启动序 4.65 的跳过入口之后才有动作,故先于玩家生成即可)。
            // ⚠️ 迟滞六旋钮 = **data-core 烘焙表**(TD-epic-01 第十一片,2026-10-19)——
            //    此前是 `CompositionRoot.EmergencyDeviceSwitchSeed*` 六个手抄常量(与源 JSON
            //    同值但无机械对账 ⇒ 数值轮改一边不改另一边 = 静默分叉)。现行为:
            //      预载成功 ⇒ provider 读 `input_device_switch.cooked.bytes`(真源);
            //      预载失败 ⇒ 常量兜底(与 `AudioSettingsRegistry.Provider` 同款设计内降级面)。
            //    两条路径的等值由 **EditMode 对账夹具**钉住(种子常量 == 烘焙表,逐值),
            //    与 44 侧 `AudioSettingsRegistryBinder.ValidateValueReconciliation` 同构。
            // ⚠️ 未挂接 / 失败 ≠ 阻断启动:读数恒「无模拟量」⇒ 所有 cause 记降级(如实可见,
            //    不是静默记成「玩家选择」)。设备态是 P0 的展示 / 读数面,不是 sim 真值。
            try
            {
                (int axisThreshold, int axisTolerance, int axisDwell,
                 int pointerThreshold, int pointerTolerance, int pointerDwell) =
                    ResolveDeviceSwitchKnobs(preloaded ? DataCorePreloader.Provider : null);
                var devices = new Input.DeviceStateManager(
                    axisThreshold, axisTolerance, axisDwell,
                    pointerThreshold, pointerTolerance, pointerDwell);
                devices.Attach();
                _deviceState = devices;
                _services.EmergencyManualDriver?.BindDeviceReadiness(
                    new Input.EmergencyDeviceReadiness(devices));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BootRoot][TD-epic-01] 设备态挂接失败 ⇒ cause 记降级(无模拟量):" + e);
            }

            // ── 2.95. 主菜单闸门(2026-11-01 · `design/ux/main-menu-42.md`)─────────────
            // 位置**在 World 装载之前**是 spec 的结构前提(§4:世界尚未装载 ⇒ 没有 tick /
            // 没有病人 / 没有可被「挡住」的世界)。⚠️ 因此本段**不是** `ModalId` 的一条:
            // `BlocksPlay` / 规则七的门 / tick 冻结面全部以「世界已在跑」为前提,把它们
            // 套到本段等于冻结一个不存在的东西(`main-menu-42.md §1`)。
            // ⚠️ 本段之后 `Update()` 仍早退(`_booted == false`)⇒ 菜单期零 tick / 零输入泵。
            if (_bootWithMainMenu)
                await PresentMainMenuAsync();

            // ── 2.96 世界切换后的**整体重装配**(ADR-040 D5)──
            // 菜单之前装配用的是「默认世界」(最近游玩那个);玩家若选了别的世界 / 新起一局,
            // 7a 的 `basePath` 必须换成那个目录 —— 而 `SaveService` 的路径是构造期固定的,
            // 所以只能重建整个服务袋。
            // ⚠️ 只在**目录不同**时做;选默认世界(最常见的路径)零重装配。
            if (!string.IsNullOrEmpty(_pendingWorldSaveDir)
                && !string.Equals(_pendingWorldSaveDir, _worldSaveDir, StringComparison.Ordinal))
            {
                ReassembleForWorld(_pendingWorldSaveDir);
            }

            // ── 3. World 场景 additive 加载(ADR-023:永不用 LoadSceneMode.Single)──
            // ⚠️ 本版 `LoadSceneAsync` **没有 `autoReleaseHandle` 形参**(U1 spike 坑 A 的
            //    autoReleaseHandle 属 `UnloadSceneAsync`,不是 Load)。可用的等价语义 =
            //    默认 `SceneReleaseMode.ReleaseSceneWhenSceneUnloaded`:场景卸载时自动释放
            //    该 handle —— 既不泄漏(卸载即释放),又保住 ADR-023 §⑤ 拆序第 4 步
            //    `UnloadSceneAsync(handle)` 所需的句柄;Phase 1 不卸载 ⇒ handle 常驻是
            //    设计而非泄漏。**场景内实例不手动 Release**(坑 B:Addressables 自管)。
            // 🔁 2026-11-05:装载体抽成 `LoadWorldSceneAsync()` —— 「返回主菜单」回程走**同一段**
            //    (两处各写一份必然分叉;抽出后启动序与回程共用一个调用点)。
            await LoadWorldSceneAsync();

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
            // 🔁 2026-11-05:读档后要把玩家送回档内格,而那是**回调**(点击时)才跑
            //    ⇒ 本局部量必须留一个字段版(换成第二处 `NextPatientId()` = 第二个 id)。
            _playerActorId = actorId;

            // ── 4.4a. 技法起步集(2026-11-09 · ADR-043 §D9)──────────────────────
            //   判据 = **事件流为空**(新世界);读档 ⇒ 流非空 ⇒ no-op,不会重发。
            //   ⚠️ 必须在这里而不是 `CompositionRoot.Assemble`:起步集的 actor = 玩家,
            //      而玩家 id 是**本步**才铸造的(与 TeleportSink / MotorLease 的迟绑定同格)。
            //   ⚠️ 漏掉本行 = 新世界里**一个技法都没有** ⇒ T4 的病人池恒空 ——
            //      而那表现为「没有病人」,不是报错(静默空世界)。
            TechniqueUnlockBootstrap.Ensure(
                _services.Stream.Events, _services.TechniqueUnlockWriter,
                actorId, _tickDriver.CurrentTick);

            // ── 4.4b. 技法解锁的触发驱动(2026-11-09 · T9)──────────────────────
            //   每个 tick 边沿扫一遍事件流,把「奇遇 / Boss / 病例」翻成解锁请求。
            //   ⚠️ 必须在这里而不是 `CompositionRoot.Assemble`:驱动要**玩家 id**,而那是
            //      第 4 步才铸造的(与 4.4a 的起步集逐字同因)。
            //   ⚠️ 规则表今日**为空**(`TechniqueUnlockRules.Default`)⇒ 驱动零扫描、零写入。
            //      这不是「没接上」:接上了、也跑了,只是没有一行内容 —— 补一行即活,
            //      不需要改任何代码(见该常量的 remarks 与 T9 证据件)。
            //   🔁 2026-11-10(A5):规则表已有**三条 Event 源规则**,上句「为空」不再成立。
            //      驱动照常每 tick 扫描;但 52 的运行期链路被 `TD-randomevents-05` /
            //      ADR-039 §R3 挡着 ⇒ **今日仍不会写任何一条解锁事件**。
            //   🔁 **2026-11-10(T17)再订正** —— 上一行那句「仍不会写」也已不成立:
            //      两笔债都结案了(上下文门 2026-11-05 接真值 / 池表已落 10 条),
            //      端到端实测(`random_events_end_to_end_day_test`)里,第一天抽中 2004
            //      就写出了一条 `TechniqueUnlocked`(技法 11 毛地黄)。⇒ 本驱动今日
            //      **会**真的解锁,触发与否取决于当日掷骰与投放资格。
            _services.TechniqueUnlockDriver = new TechniqueUnlockDriver(
                _services.Stream, _services.Decoder,
                TechniqueUnlockRules.Default, _services.TechniqueUnlockWriter, actorId);

            // ── 4.3a0. 37 同源检测 + 章推进(T20 · 2026-11-10)─────────────────────
            // 装配点在 `CompositionRoot`(`dataProvider` 有 37 旋钮产物才建);本步只做
            // **未装配的显式日志** —— 否则与「机制未启用」同形。
            if (_services.PatternRecognition == null)
                Debug.LogWarning(
                    "[37] 同源检测未装配 —— 规则八(三例同源 ⇒ 世界级 PatternRecognized)在运行期不会运行," +
                    "章号恒 0(钩子配额 = 整局 1 例)。原因:无 dataProvider 或 case_pattern 产物未装载。");

            // ── 4.3a. 1 侧传送 sink 绑定(S23-7 · 2026-11-01)────────────────────
            // 构造在 CompositionRoot 死亡消费段(服务袋 TeleportSink);玩家此刻才存在
            // ⇒ 在此迟绑定。复活链(29 revive 回调 → issuer → 本 sink)自此全通。
            _services.TeleportSink.BindPlayer(() => _player);

            // ── 4.3a′. 移动压制租约绑定(story-011 R-11.2 · 2026-11-02)────────────
            // 租约的真源在 1(`PlayerController.MotorLease`),而玩家是**本步**才建出来的,
            // 驱动却在 `CompositionRoot.Assemble` 里早于玩家构造 ⇒ 只能后置绑定
            // (与 TeleportSink.BindPlayer 同一格、同一理由)。
            // ⚠️ 必须是**那一个**实例 —— 驱动侧再 new 一份就成了两个真源,4 / 25 / 10 会各压各的。
            // ⚠️ 漏掉本行 = 10 永远置不上位,玩家在急救动作期间**照样走**,且不崩不报错 ——
            //    由 PlayMode 夹具的 `HasMotorLease == true` 断言守着(S11-06)。
            _services.EmergencyChannel?.BindMotorLease(_player.MotorLease);

            // ── 4.3b. 死亡过渡呈现器(42 侧 · story-006 视觉装配 · 2026-11-01)────
            // 全屏水墨叠层(零交互,spec §5/§7);数据源 = 只读投影(零写路径);
            // 程序化水墨纹理,正式素材归美术资产批(ΦE 纯黑兜底已备)。
            // ⚠️ 远端变体与相机两段式未落 —— 分别挂 45 第二 QoS 轮与机位轮(如实登记)。
            var deathPresenterGo = new GameObject("DeathTransitionPresenter");
            var deathPresenter = deathPresenterGo.AddComponent<
                DaYiJingCheng.Gameplay.Presentation.DeathTransition.DeathTransitionPresenter>();
            // ⚠️ **空值保护(2026-11-01 回归修复)**:拆卸段会把 `_services` 置 null,而本闭包
            // 在 Presenter 的**下一帧 Update** 里才求值 ⇒ 直取 `_services.Stream` 会 NRE
            // (实测 94 条 PlayMode 红,堆栈 BootRoot:540 → Presenter.Update:58)。
            // ⇒ 取值处判空:服务袋已拆 ⇒ 事件源为 null ⇒ 呈现器按「无数据源」停更。
            // ⚠️ 装配在这里而非 `CompositionRoot`:机位是 `BootRoot` 私有字段(不在服务袋)。
            // ⚠️ 用 `Func` 懒求值 + 判空:`_cameraRig` 在拆卸段会被置 null,
            //    而本闭包可能在下一帧才求值(2026-11-01 的 94 条 PlayMode 红即此形态)。
            _downedCameraRequester = new DaYiJingCheng.Gameplay.Presentation.DeathTransition
                .DownedCameraRequester(() => _cameraRig);

            deathPresenter.Bind(
                () => _services != null ? _services.Stream.Events : null,
                _services.Decoder,
                () => actorId,
                // ── 相机两段式(spec §9「相机(Φ1→Φ2)」· 2026-11-01)────────────────
                // 呈现器只发**意图**,不持相机(ADR-020 §五「相机只读不持状态」)。
                // 🔁 **2026-11-02(story-007 R-7.1)**:原内联 lambda 提取为具名载体
                //    `DownedCameraRequester` —— 逻辑**逐条等价**(R-7.2 的四行对照),
                //    提取的理由是「没有任何东西能指向那段 lambda」:本 epic 内零 story 提它,
                //    改坏接线**没有一条 AC 会红**(缺口 A)。
                _downedCameraRequester.Request);
            _services.DeathPresenter = deathPresenter;   // 供测试与后续轮次定位(唯一实例)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                deathPresenterGo, gameObject.scene);

            // ── 4.4. 玩家 actor 登记(2026-10-24 story-004 装配轮)──
            // id 在**装配之后**才铸造 ⇒ 登记必然是后置动作(与 SetViewer / SetAuthor 同格)。
            // 后果:登记前 `ClassOfDeathCandidate` 恒 Patient ⇒ 死亡候选的类别闸不放行;
            // 登记后玩家才「是玩家」。⚠️ 只登记**本机玩家自己**的 id ——
            // 联机远端玩家的 id 归 45 的会话建立轮(ADR-001 第二 QoS 上行)。
            _services.PlayerActors.Register(actorId);

            // ── 4.45. 建造发起方接线(S20-19 · 2026-10-28)────────────────────────
            // 三条后置绑定(与 SetAuthor / SetViewer / BindPlayer 同格 —— actorId 在
            // `Assemble` 之后才铸造,装配期拿不到):
            //   ① 扣料归属 = 本机玩家(P0 甲案:建造扣料恒归唯一玩家);
            //   ② 取格委托 = `PlayerController.Cell`(**唯一**量化器 `CellFromPosition`);
            //   ③ 落流回调 = 重建库存投影(`StructurePlaced` 的扣减在 fold 里,不重建读不到新余量)。
            //
            // ⚠️ **默认幽灵模块 = 目录里 id 最小的那个** —— 玩家选料界面(42 的幽灵循环选择)
            //   本批**未落地**,`SelectedModuleId` 是留给它的接缝。此默认值只为让
            //   「按键 → 判定 → 落流」这条链在真实运行时可走通,不是设计决定。
            var build = _services.BuildController;
            if (build != null)
            {
                build.BindOwner(actorId);

                int defaultModule = int.MaxValue;
                foreach (int id in build.ModuleIds)
                    if (id < defaultModule) defaultModule = id;

                _buildDriver = new BuildInputDriver(
                    build,
                    () => _player != null ? _player.Cell : default,
                    onCommitted: () => _services.InventoryLoad?.Rebuild(),
                    // 🔁 2026-10-31:建造四动作入模态冻结面(此前 `Enter` 会被 UI Submit 与
                    //    建造放置**同时**消费)。lambda 晚绑定 —— `_inputFreeze` 在 4.5 段才建。
                    blocked: () => _inputFreeze != null && _inputFreeze.IsBlocked);
                _buildDriver.SelectedModuleId = defaultModule == int.MaxValue ? -1 : defaultModule;
            }

            // 52 出诊启动接线(2026-10-25 接线轮):玩家 id 铸造后晚绑定到导演主循环壳
            // (id 在装配之后才生成 ⇒ 后置形态,承 NoPendingMedicalActionQuery.Bind 先例;
            // 未绑定期的边沿驱动为 no-op fail-safe)。
            _services.RandomEventsDirector?.BindPlayer(actorId);

            _player.Initialize(
                SimAuthorityMode.Host,
                _services.EventSink,
                _tickDriver,
                _services.Encoder,
                actorId);

            // 落到世界原点上方 1 m(CharacterController 需落地;落地表现后续 PlayMode 验)。
            _player.Teleport(new Vector3(0f, 1f, 0f));

            // ── 4.5. 玩家交互输入驱动(sprint-07 S2):E 查体 / F 急救,每帧由 Update 转发 ──
            // 🔁 2026-10-31 规则七方向②接线:`ModalGate` 此前**生产零构造**(缺 42 侧那半个门)
            //    ⇒ 模态/Armed 期的交互抑制在运行期从未生效。此处两门齐:
            //    ① Armed = 10 的急救通道(`EmergencyChannelDriver` 即 `IArmedState`);
            //    ② 模态开集 = 42 驱动经 `ModalGateStateAdapter`(提供者形态 —— UI 驱动在本段**之后**才建)。
            //    ⚠️ 未接上时 `ModalGateStateAdapter.IsOpen` 返回 false ⇒ **放行**(诚实降级),
            //      但那是「无 UI」的合法形态,不是「抑制生效」。
            // 🔁 2026-10-31 收尾:同一个「模态开集」读面还要喂**输入冻结**(移动轴 + 建造)——
            //    单实例共用,避免三处各自演化出「什么算挡路」的第二份判据。
            var modalState = new ModalGateStateAdapter(() => _uiDriver);
            // ── 4.5a. E 按下沿的**统一路由**(2026-11-02 · 结清 `TD-interaction-01`)──
            //    旧形态是「拾取 claim → 采集桥 → 查体」三段外挂仲裁(`InteractClaimRoute`),
            //    其中「打给谁」由两处各算一遍(采集自选资源点 / 驱动自选最近病人)。
            //    现口径 = **一次 `InteractionSelector.Select` 定种类**(F-4.1 三键全序),
            //    再按 `InteractableKind` 分派 → `InteractionPressRouter`。
            //    PlayerCell 用经流确立格(ADR-020 §四),不再读 `transform.position`。
            // ── 4.5a-0. 门的开 / 合消费面(23 · 2026-11-02 · `AC-4-22`)──────────────
            // 门的状态**只有一处**:`StructureInstance.Variant`(0 = 关,1 = 开)。
            // 切换 = `StructureWriter.Modify` ⇒ `StructureModified` 进世界流(可重放 / 可存档),
            // 实例表随之更新 ⇒ `World.IsEffectivelyWalkable` 立刻反映新态。
            // 未接世界 / 未装配写者 ⇒ null ⇒ 路由对 Door 返回 false(按键落空,不静默改写别的结构)。
            // 门 / 开关共用同一机制(两态 `Variant` + `StructureModified`),只差下游效果:
            // 门影响可通行性;开关今日无读者(状态进流,供将来的电路 / 灯具系统消费)。
            Func<long> tickNow = () => _services.TickDriver != null ? _services.TickDriver.CurrentTick : 0L;
            StructureToggleDriver doorToggle =
                _services.World != null && _services.StructureWriter != null
                    ? new StructureToggleDriver(
                        DaYiJingCheng.Sim.World.SlotType.Door,
                        () => _services.World.Structures,
                        () => _services.World.BaselineModuleCatalog,
                        () => _services.StructureWriter, tickNow)
                    : null;
            StructureToggleDriver switchToggle =
                _services.World != null && _services.StructureWriter != null
                    ? new StructureToggleDriver(
                        DaYiJingCheng.Sim.World.SlotType.Switch,
                        () => _services.World.Structures,
                        () => _services.World.BaselineModuleCatalog,
                        () => _services.StructureWriter, tickNow)
                    : null;

            InteractionPressRouter pressRouter = null;
            if (_services.PressCandidates != null && _services.PressSelector != null
                && _services.PressRadius != null)
            {
                pressRouter = new InteractionPressRouter(
                    () => _player != null ? _player.LastCommittedCell : default,
                    () => _services.TickDriver != null ? _services.TickDriver.CurrentTick : 0L,
                    actorId,
                    _services.PressCandidates,
                    _services.PressSelector,
                    _services.PressRadius,
                    _services.DropClaimOrchestrator,
                    _services.HarvestDriver,
                    // ⚠️ 懒求值:患者在**本段之后**才构造(它要拿 `_interactionDriver` 的 Visit)。
                    patient => _interactionDriver?.Visit(patient),
                    // 🔁 2026-11-02(`TD-interaction-03`):E 打到建造槽位 ⇒ 23 进建造模式并对准该格。
                    //    未建建造驱动(无世界 / 夹具)⇒ null,路由对 BuildSlot 返回 false(按键落空)。
                    _buildDriver != null ? new Action<WorldPos>(_buildDriver.AimAt) : null,
                    // 门:`StructureModified`(23 唯一写者面)。
                    doorToggle != null ? new Func<long, bool>(doorToggle.Toggle) : null,
                    // 容器(柜):42 的库存容器屏。🔁 **2026-11-02 接通内容面**(`S23-25`)——
                    // 此前只打开空壳。现喂 `InventoryContainerDto`(器物格 + 载重对),
                    // 并把屏上的选中态转交装配层的 `ContainerSelectionStore`(料位供给)。
                    // ⚠️ 器物来源 = `InventoryProjection.InstancesOf(owner)`,**已按 instance_id 升序**
                    //    (`InstancesOf` 的契约)⇒ 42 侧不再排序(两处排序 = 第二真源)。
                    // ⚠️ 载重对与满档谓词**不在这里算**:两者都在 `BuildContainerDto` 内调 20 的
                    //    `CarryLoadSolver` 本体(`LoadOf` / `MinItemWeight` / `IsFullForAnyAddition`),
                    //    42 只拿结果。**在这里拼一个「行数 × 重量」就是第二容量真值。**
                    instanceId =>
                    {
                        if (_uiDriver == null) return;
                        if (!_uiDriver.OpenModal(UI.Skeuomorphic.ModalId.InventoryContainer)) return;
                        var screen = _uiDriver.Screen
                            as UI.Skeuomorphic.Screens.InventoryContainerScreen;
                        if (screen == null) return;
                        var store = _services.ContainerSelection;
                        if (store != null)
                        {
                            screen.BindSelectionSink(new ContainerSelectionBridge(store));
                        }
                        screen.Bind(BuildContainerDto(instanceId, actorId));
                    },
                    // 开关:与门同一机制,只换被允许的模块类型。
                    switchToggle != null ? new Func<long, bool>(switchToggle.Toggle) : null,
                    // 医馆面板(24):42 的屏已存在。
                    structureId =>
                    {
                        if (_uiDriver != null)
                            _uiDriver.OpenModal(UI.Skeuomorphic.ModalId.ClinicPanel);
                    },
                    // 器具(18):🔁 **2026-11-02 接通**(ADR-013 §十-E Amendment D 第九员 `Processing`)。
                    // 打开炮制屏并喂数据:配方全表 + 准入查询 + 玩家 + 品级集。
                    // ⚠️ 过滤归屏(它只问 `IsAdmissible`,不自己合取 —— AC-18-20 零第二实现)。
                    // ⚠️ 品级集 = 20 屏的选中态(经装配层的 `ContainerSelectionStore`);
                    //    **未选任何料 ⇒ 空集 ⇒ 屏上零行**。空集是合法输入
                    //    (`AdmissiblePredicate` 的 `NoConsumedInstances`),不是 null(fail-closed 会抛)。
                    //    ⇒ 零行是「玩家还没点料」的诚实形态,不是「屏坏了」。
                    structureId =>
                    {
                        if (_uiDriver == null) return false;
                        var recipeSource = _services.RecipeSource;
                        var admission = _services.ProcessingAdmission;
                        if (recipeSource == null || admission == null) return false;
                        if (!_uiDriver.OpenModal(UI.Skeuomorphic.ModalId.Processing)) return false;
                        var screen = _uiDriver.Screen as UI.Skeuomorphic.Screens.Processing18Screen;
                        if (screen == null) return false;
                        // 🔁 2026-11-04(`TD-skeuo-06`):接上意图出口。
                        //    器具格从结构注册表按 `structure_id` 解析(F-5.4 的 ToolCell 锚点),
                        //    **不用玩家格顶替** —— 起货溢出落点是它。
                        if (_processingSink != null)
                        {
                            _processingSink.SetTool(structureId);
                            screen.BindCommandSink(_processingSink);
                        }
                        // 惰性求值:只在屏真打开时读数据集(装配期零读取 —— 见 CompositionRoot 注)
                        // 🔁 **2026-11-02(料位供给)**:品级集不再传空集 —— 它来自 20 屏的选中态
                        // (`ContainerSelectionStore`)。空选中集 ⇒ 空品级集 ⇒ 零行(合法输入)。
                        var store = _services.ContainerSelection;
                        var qualities = store != null
                            ? store.ConsumedQualitiesOf(actorId)
                            : (System.Collections.Generic.IReadOnlyList<int>)System.Array.Empty<int>();
                        // 🔁 **2026-11-06(S25-3 · processing-18 §6.1)**:另传单炉查询 ——
                        //    **只给空态选词**(「炉上有药,火未熄」vs「无方可起」),不参与准入判定。
                        screen.Bind(recipeSource().Recipes, admission, actorId, qualities,
                                    _services.FurnaceBusy);
                        return true;
                    });
            }
            else
            {
                // 无 data-core(测试装配 / 数据未就绪)⇒ 不建路由。驱动退回「未接线」形态,
                // 与 `PressCandidates == null` 同源 —— 不是「静默降级成旧路由」。
                Debug.LogWarning("[BootRoot] PressCandidates / PressSelector / PressRadius 缺一 —— " +
                                 "E 键的 4 目标选择器未接线(交互路由不可用)。");
            }

            var interaction = new InteractionInputDriver(
                _services,
                () => _player != null ? _player.transform.position : Vector3.zero,
                actorId,
                new ModalGate(_services.EmergencyChannel, modalState),
                routeInteractPress: pressRouter != null ? (Func<bool>)pressRouter.OnPress : null,
                // ── E 键的就诊两半(2026-11-02 · 结清 `TD-casebook-01`)──
                //   ① 立案:37 的 `CaseOpened` 唯一生产入口。判定(在场 / 去重)在 writer 内,
                //      本缝只转交 —— 驱动侧不复制第二份判据。
                //   ② 开册:42 的 `OpenModal(ModalId.Casebook)`。`_uiDriver` 在本段**之后**才建
                //      ⇒ 必须懒求值(lambda 捕获字段),否则拿到 null 静默不开屏。
                caseOpen: (patient, tick) =>
                    _services.CaseOpenedDriver?.TryOpen(patient, tick),
                openCasebook: () =>
                    _uiDriver != null && _uiDriver.OpenModal(UI.Skeuomorphic.ModalId.Casebook),
                // 🔁 2026-11-02:急救 Arm 目标改走整数格(4 规则二「玩家格取路 (a)」)。
                playerCell: () => _player != null ? _player.LastCommittedCell : default,
                // ── C3-a(2026-11-10):R = 转诊 ──
                //   驱动只把「按键 + 最近在场病人」交过来;「他现在有没有开着的病例」
                //   由 `HookCaseReferDriver` 从病例流反查,判定仍归 37 的写者。
                //   ⚠️ 未装配(`null`)⇒ 按键什么都不做(与「没按」同形,不是静默降级)。
                routeReferPress: patient => _services.HookCaseRefers?.TryRefer(patient),
                // ── T19 甲(2026-11-10):C = 结案 ──
                //   改前 `CaseCloseWriter.TryClose` 的生产调用点 = 0 ⇒ 病例永不收口,
                //   三条 Case 源解锁规则(技法 2 / 3 / 10)全部不可达。
                //   「该不该结」全在写者里(四条前置);本缝只把「按键 + 最近在场病人」交过去。
                // ⚠️ 语句 lambda:`TryOpen` 返 bool(开没开成),这里**故意不看** ——
                //    「没开屏」= 没有开着的病例(不开空页),不是错误。
                routeClosePress: patient => { _caseClosePresenter?.TryOpen(patient); });
            _interactionDriver = interaction;
            _pressRouter = pressRouter;

            // ── 4 的定身写面后置绑定(`TD-interaction-04` · 2026-11-02)──────────────
            // 两个真源都晚于本类建成:租约归 1(玩家在本步之前才建),模态态归 42(UI 驱动更晚)。
            // ⚠️ 模态那一侧读 **`BlocksPlay`** 而非 `IsModalOpen`(后者含常驻屏 ⇒ 闸门常闭)。
            // ⚠️ 未绑定 ⇒ `OnTick` 不写租约(**不压制**,而不是压住不放)。
            _pressRouter?.BindMotorSuppression(
                _player.MotorLease,
                () => _uiDriver != null && _uiDriver.BlocksPlay);
            _inputFreeze = new ModalInputFreeze(() => modalState.IsOpen);

            // ── 4.55. 急救直读通道接线(story-009 · AC-9-01)──
            // Attach = 挂 onAfterUpdate + Emergency 动作解析 fail-loud(FindAction 落空 ⇒
            // 通道抛 InvalidOperationException)—— 启动期炸,不带病运行。
            _services.EmergencyChannel.Attach();

            // ── 4.6. 方笺页接线宿主(S2.3/S2.4):SetAuthor(玩家 actorId 已定)──
            // 落笔 / 开方的作者 = 玩家;至此 PrescriptionPageHost 可接动作。
            // ⚠️ 绑定病人由查体驱动(InteractionInputDriver 的 E 查体已切脉案绑定)
            //    —— 方笺页落笔 / 开方对象 = 当前脉案绑定病人(42 侧会话)。
            _services.PrescriptionPage?.SetAuthor(actorId);

            // ── 4.7. 48 教学链接线(S25-4c · 2026-11-06):玩家 id 已定 ⇒ 可 Attach ──
            // ⚠️ **`Attach` 内部立即播种**(按当时流投影一次,只置饱和不 Fire)——
            //    读档进来的已完成步因此不会被重播。放在「流已就绪 + 玩家已有 id」之后是硬要求,
            //    提前会让播种读到未装载的流(把整段教学重播一遍)。
            // ⚠️ 未装配(null)必须**显式告警**:它与「本局没有教学」在行为上不可区分。
            if (_services.TutorialSeams != null)
            {
                _services.TutorialChain = _services.TutorialSeams.Attach(_services.Stream, _services.Decoder, actorId);

                // 走查仪表:带 `-tutorialnarrcheck` 时把**全部四条口述**用生产解析面依次播一遍。
                // 用途 = 回答「其余三条 cue 在真包里到底解析得出、播得响吗」——
                // 它不替教学链发火(发火仍只由事件流驱动),只走同一条 翻译层 → 44 → 42 路径。
                if (WalkthroughDiagnostics.NarrationCheckEnabled)
                    StartCoroutine(PlayAllNarrationsForDiagnostics());
            }
            else
                Debug.LogWarning("[48][tutorial] 教学链未装配 —— 内容表/音频缝/字幕口 至少缺一," +
                                 "本局不会有任何教学口述与纸指引(不是「教学已完成」)。");

            // ── 4.65. 急救跳过入口写路径接线(story-009 · R-9.4)──
            // 候选列表「跳过」项 Submit → EmergencyChannelDriver.SkipAction
            //   → 先发 Explore 档位意图,再委派 EmergencyManualDriver.TryManual(method 1)。
            // 🔁 **2026-11-02(story-010 R-10.7 甲案)**:宿主从 EmergencyManualDriver 改为
            //    EmergencyChannelDriver —— 跳过必须回到相机链上,否则规则八的
            //    「跳过路径结束必发 Explore」没有落点(立件时实测:此前零落点)。
            // actorId 由 BootRoot 生成玩家后注入(SetAuthor 同款后置形态 —— 装配期未知)。
            _services.CasebookCommands?.BindEmergencySkip(_services.EmergencyChannel, actorId);

            // 🔁 2026-11-02(结清 `TD-diagnosis-01`):行级查体完成 ⇒ 「见过病 D」投影。
            //    actorId 只有这里有(装配期未知),故接在这里而非组合根 ——
            //    `CasebookCommandRouter` 只发 (病人, tick),玩家 id 由本处补齐。
            // 🔁 2026-11-11(`TD-skeuo-18` A):**同一个回调挂两个消费者** —— 病名册投影 +
            //    脉案值区重读。⚠️ 故意写成**一个** lambda 而不是两次 `+=`:
            //    `AfterExamined` 是 `{get;set;}` 属性(不是 event),谁后赋值谁覆盖 ⇒
            //    两个订阅点分散写,后来者会把前者**静默顶掉**(本轮开工前它就是单播)。
            //    合成一个 delegate ⇒ 「漏一个」与「全漏」在观感上不同,不会静默半坏。
            //    ⚠️ 重读**不能**靠 tick 边沿:脉案是挡路模态 ⇒ 世界冻结 ⇒ tick 不推进
            //    (见 `SkeuoRuntimeDriver.MarkCasebookDirty` 的说明)。
            if (_services.CasebookCommands != null)
            {
                _services.CasebookCommands.AfterExamined =
                    (patient, tick) =>
                    {
                        _services.SeenDisease?.OnExamined(patient, tick, actorId);
                        _uiDriver?.MarkCasebookDirty();
                    };
            }

            // ── 4.7. 病名册观看者(sprint-08 S1.1 件 E):SetViewer(玩家 actorId 已定)──
            // 病名册条目集按人分册(铁律⑤「每人一本」)—— 观看者 = 玩家。
            // 至此 42 脉案源可读病名册(未设 ⇒ 病名册空,反幻想二)。
            _services.CasebookSource?.SetViewer(actorId);

            // ── 5. 相机机位装配(sprint-05 T1.5 · 2026-10-10 AudioListener 泄漏修复)──
            // ADR-020:相机 = 自建机位,不引入 Cinemachine
            // 跟随 = 每帧把相机位置设到玩家位置 + 偏移
            //
            // ⚠️ **不自加 AudioListener**(ADR-020 §七 / AC-ADR020-10 单挂点铁律):
            //   T1.5 初版在此 `AddComponent<AudioListener>()`,而 Boot.unity 预摆的
            //   `Main Camera`(Transform + Camera + AudioListener + URP data)**已有一个**
            //   ⇒ 场景内恒 2 个 listener,Unity 每帧刷一条警告。
            //   实测代价(2026-10-10 人工 playtest):Editor.log 31868 条同文本,
            //   Console 被淹 → Editor IPC 管道超载 → `write EPIPE` → 进程异常终止,
            //   表现为「无法退出 Play / 点关闭无响应」。根因是日志洪泛,不是死循环。
            //   修复 ① = 单挂点:本节**禁用**预摆相机(它只持 listener 语义)。
            //   ⚠️ **修复 ②(2026-10-20)** —— 修复 ① 只做了一半:预摆相机被禁用后,
            //   运行时 CameraRig 相机**没有**补 listener ⇒ 运行时 active listener = 0,
            //   整个场景静音。Editor.log 实测 10845 条
            //   `There are no audio listeners in the scene`(首次出现于本次 Play 首帧)。
            //   正确形态 = listener 随「主相机」迁到 CameraRig 运行时相机(ADR-020 §七:
            //   单挂点就是主相机)。预摆相机上的那份**必须先摘除** —— 否则它会以 inactive
            //   形态留在挂载计数里,把「零 active」掩盖成「1 个挂载」(见 AssertSingleAudioListener)。
            // ⚠️ **必须含 inactive 地找预摆相机**(2026-10-20 全量 PlayMode 实测):
            //   `GameObject.Find` 只找 **active** 对象 ⇒ 若预摆相机此刻已 inactive,本节整个跳过
            //   ⇒ 它身上的 listener **摘不掉**,以「挂载但不活动」的形态留在计数里,
            //   与运行时 CameraRig 的那一个并成 **2 个挂载**,`AssertSingleAudioListener` 抛。
            //   实测路径:全量 run 里 `camera_follow_wiring_test` 自建的预摆相机被启动序禁用后
            //   再没被恢复(它的拆卸也用 `GameObject.Find` 恢复 —— 同一处死代码),
            //   后续测试经 `FindPresetCamera`(**含 inactive**)复用它并补 listener
            //   ⇒ 单跑绿、全量红。本件与「单件全绿 ≠ 全量全绿」同型。
            //   语义上本节的义务是「**无论预摆相机处于什么状态**,都不许它与运行时相机并存有 listener」
            //   —— 用 `FindObjectsByType(Camera, includeInactive)` 而非名字 Find 才对得上这句话。
            var presetCamera = FindInactiveInclusive("Main Camera");
            if (presetCamera != null)
            {
                var presetListener = presetCamera.GetComponent<UnityEngine.AudioListener>();
                if (presetListener != null)
                    UnityEngine.Object.DestroyImmediate(presetListener);
                presetCamera.SetActive(false);
            }

            var cameraObj = new GameObject("CameraRig");
            // ⚠️ **必须归 BootRoot**(2026-10-20 · 全量 PlayMode 实测出的一处真泄漏):
            //   `new GameObject` 落**当前活动场景**,而启动序第 3 步已 additive 载入 World
            //   ⇒ 不归 Boot 则 ① Phase 2 卸载 World 会带走相机;② **更早暴露的是测试间泄漏** ——
            //   CameraRig 是根级对象,`BootRoot.OnDestroy` 从不销毁它(它不在 BootRoot 层级下),
            //   每个 PlayMode 测试都留下一个。修前它无害(运行时相机**没有** listener,泄漏件不计数);
            //   本轮把 listener 迁到它身上之后,**每个残留 CameraRig 都带一个 listener**
            //   ⇒ `AssertSingleAudioListener` 的 active 计数逐测递增(2 → 3 → … → 7),全量 7 红。
            //   单件跑看不见(每测试基数都是 1)—— 这是「单件全绿 ≠ 全量全绿」的一次实证。
            //   与 `EnsureEventSystem`(:430)同一纪律,此处用 SetParent 更简
            //   (跨场景 reparent 自动随父进 Boot 场景;相机每帧赋**世界坐标**,reparent 不影响)。
            cameraObj.transform.SetParent(transform, worldPositionStays: false);
            _cameraRig = cameraObj.AddComponent<CameraRig>();
            cameraObj.AddComponent<UnityEngine.Camera>();
            cameraObj.AddComponent<UnityEngine.AudioListener>();   // 运行时主相机 = 唯一听音点
            _cameraRig.SetModeForTest(CameraMode.Explore);

            // ── 5.0b. 急救档位意图的相机绑定(story-010 R-10.2 · 2026-11-02)──────
            // 10 的档位意图(Treatment / Explore)住所 = EmergencyChannelDriver,而它在本步**之前**
            // 的 CompositionRoot.Assemble 里就构造好了 ⇒ 只能后置绑定(与 TeleportSink.BindPlayer /
            // SetAuthor / BuildOwner 同一约定)。
            // ⚠️ 用 `Func` 而非实例:`_cameraRig` 在拆卸段会被置 null,而闭包可能在下一帧才求值
            //    (2026-11-01 的 94 条 PlayMode 红就是这个形态)⇒ 驱动侧判 null 即静默跳过。
            // ⚠️ 漏掉本行 = 相机永远收不到 Treatment/Explore,且**不崩不报错** —— 由
            //    PlayMode 夹具的 `HasCameraRig == true` 断言守着(story-010 S10-06b)。
            _services.EmergencyChannel?.BindCameraRig(() => _cameraRig);

            // ── 5.1. 空间化听者锚点(S15-4 · TD-audio-028)────────────────────
            // 听者 = **主相机**(ADR-020 §七:`AudioListener` 的单挂点就是它)——
            // 用相机位置算距离,与 Unity 自身的距离衰减是**同一个参考点**;
            // 若改用玩家格,第三人称越肩视角下衰减中心会与耳朵错开。
            // ⚠️ 必须在**相机装好之后**注入(组合根建渲染目标时相机还不存在)。
            // ⚠️ 读的是**每帧实时**位置(委托),不是快照 —— 玩家移动时距离必须跟着变。
            if (_services.RenderTarget != null)
                _services.RenderTarget.SetListenerPosition(_cameraRig.Camera.transform);

            // ── 5.2. 遮挡的听者格(`TD-audio-035` ②)──────────────────────────
            // 与 5.1 同一个参考点(主相机)—— 但走**整数格**而不是 float:
            // ADR-015 §一 两层纪律要求遮挡输入是逻辑层格,44 不读表现态 float。
            // ⚠️ 委托**每帧实时读**(玩家移动时遮挡必须跟着变),不是快照。
            // ⚠️ 换算 = `FloorToInt(world / latticeSize)`,与 1 的 `GetCell()` 同一约定
            //    (`player-controller-and-movement.md:162` 的跨格判定式);`latticeSize` 已由
            //    上面 `ApplyLatticeFromWorldGeometry` 从 6 的产物取得。
            // 🔁 **2026-11-06(真包走查带出的缺陷修复)**:这条 λ 现在落**两个**读面。
            //    此前只给了渲染目标(`sources.SetListenerCell`),`AudioAssemblySeams.ListenerCell`
            //    全程为 null —— 而 `CompositionRoot.cs:1811` 的注释写的是「由 BootRoot 注入」,
            //    那句注入**不存在**。于是 `CompositionRoot.cs:2157` 的听者缝判定恒走
            //    `default(Int3)` 支,**教学口述的声源格永远是世界原点**。
            //    ⚠️ **这只修好了第一环** —— 光填缝**不生效**(实测:增益逐位不变)。第二环在
            //    `CompositionRoot` 那侧:它原来在**装配那一刻**就判空,而本步在装配之后才跑 ⇒
            //    判空结果被固化成 null 分支。两环都修才有效(延迟读 × 填缝)。
            //    ⚠️ 实测:填缝 + 延迟读之后,口述增益 0.0216 → **0.6631**(−33.3 → −3.6 dB,
            //    对 `paplay` 直放控制 0.8306 即净恢复约 30 dB)。
            //    证据 = `production/qa/evidence/walkthrough-s25-4c-tutorial-real-build-2026-11-06.md` §9。
            // ⚠️ 顺序:听者缝**不依赖**遮挡缝(`OccludedFor` 只是渲染目标那一半的前置)——
            //    之前把两者绑在同一个 `if` 里,世界几何退化 ⇒ 遮挡缝不接 ⇒ **听者缝也一起断**,
            //    而那两个量的语义没有依赖关系。
            // ⚠️ `LatticeSize == 0`(未供给)⇒ 不接:那是「未装配」,不是「听者在原点」。
            if (_audioSeams != null && _audioSeams.LatticeSize > 0f)
            {
                Transform cam = _cameraRig.Camera.transform;
                float lattice = _audioSeams.LatticeSize;
                Func<Int3> cameraCell = () =>
                {
                    Vector3 p = cam.position;
                    return new Int3(Mathf.FloorToInt(p.x / lattice),
                                    Mathf.FloorToInt(p.y / lattice),
                                    Mathf.FloorToInt(p.z / lattice));
                };

                _audioSeams.ListenerCell = cameraCell;

                if (_services.RenderTarget is AudioSourceRenderTarget sources
                    && _audioSeams.OccludedFor != null)
                {
                    sources.SetListenerCell(cameraCell);
                }
            }

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

            // ── 6.0a. 结案页(T20 第二批 · 2026-11-10)──────────────────────────
            // `C` 从「直接结案」改为「开结案页」:墨勾 = GDD 规则八的「显式勾选」,
            // 「结案」行才真写。⚠️ 判定仍全在 37 的写者里,本件只翻译屏上意图。
            if (_services.CaseClosers != null && _uiDriver != null)
                _caseClosePresenter = new CaseClosePresenter(
                    _services.CaseClosers, _uiDriver, () => _services.Stream.Events);

            // ── 6.0. 病人 cue 可见对应物挂载(`AC-13-F1` 达成面 · 2026-10-29 · 缺口 D)──
            // 挂载 = 纯渲染资源(42 C3)。无 UI 根(panel 未就绪 / 测试自建空壳)⇒ 不挂,
            // 逻辑树仍在(可见物照常写入,只是不渲染)—— 与 `MountTo` 同口径。
            if (_cueMarks != null)
            {
                UnityEngine.UIElements.VisualElement uiRoot = _uiDriver.GetUiRoot();
                if (uiRoot != null)
                {
                    uiRoot.Add(_cueMarks);
                    if (_subtitle != null) uiRoot.Add(_subtitle);
                }
            }
            // ── 6.1 写通道注入(sprint-08 S1.1 件 F1):病名栏 Submit → 装配层写者 ──
            // 42 只渲染不持状态(ADR-013 §9 C3)⇒ 病名栏 Submit 经命令面转发给装配层的
            // CasebookCommandRouter(实现 ICasebookCommandSink)。缺注入 ⇒ Submit 留空(不崩)。
            if (_services.CasebookCommands != null)
                _uiDriver.CommandSink = _services.CasebookCommands;

            // ── 6.1b 39 的档位请求接线(casebook/story-006 · 2026-11-02)────────────
            // 病因:`camera-and-viewpoint.md` 的 `R-2-5` 表写着「39 打开脉案 ⇒ 请求 Casebook」,
            // 而生产侧此前**零个请求点**(`CameraMode.Casebook` 只出现在相机内部的优先级表与
            // 冻结判据里)⇒ 「该发没发」而不被任何门抓到。
            // ⚠️ 用 `Func` 懒求值 + 判空:`_cameraRig` 在拆卸段会被置 null,而模态开关
            //    可能在下一帧才求值(承 2026-11-01 那 94 条 PlayMode 红的教训)。
            _casebookCameraRequester = new DaYiJingCheng.Gameplay.Presentation.Casebook
                .CasebookCameraRequester(() => _cameraRig);
            _uiDriver.CasebookCameraRequest = _casebookCameraRequester.Request;

            // ── 6.1c 手册近景的内容宿主(T10 · 2026-11-09)─────────────────────────
            //   补三处零消费者:`PaperCloseupScreen.Bind` · `SetMissingPaperIds` ·
            //   `TutorialPaperMissing.Compute` —— T6 会画、T8 会判、48 有表,三件之间
            //   此前**没有任何一根线**。本段把那条线接上。
            //   内容表取**教学链那一个实例**(`TutorialChainSeams.Content`),不另装载 ——
            //   同一张表两条来路迟早分叉。
            //   ⚠️ 教学链未装配(无 data provider / 无音频字幕口 ⇒ `TutorialSeams == null`)⇒
            //      宿主不建,`PaperCloseups` 留 null。语义 = **本局没有手册内容**,不是「宿主坏了」。
            //   ⚠️ ~~**今日零生产调用方**:「走近摊纸」的世界交互(4 + 关卡)不存在 ⇒
            //      玩家打不开这本手册。~~ 🔁 **2026-11-10(T18 甲)**:手册已有开屏入口 ——
            //      `M` / 手柄 `select` → `PaperCloseupToggleDriver` → `TryPresentManual()`(下方)。
            //      「走近摊纸」那个世界道具仍不存在(C1-a 等关卡工具),**但手册不再打不开**。
            if (_services.TutorialSeams != null && _services.TechniqueUnlocks != null)
            {
                _services.PaperCloseups = new PaperCloseupPresenter(
                    _services.TutorialSeams.Content.Papers,
                    _services.TechniqueUnlocks,
                    () => _playerActorId);
            }

            // ── 6.2 存档位载入接线(2026-10-31 · TD-persist-04 裁定②「通电待信」的收尾)──
            // 病因:`RequestLoad` 今日零调用方 ⇒ AC-7a-14 的比对执行体与 ADR-037 的逐表归因
            // **全部**停在「读档路径一旦被调用」这一层,从未有过生产可达性。
            // 本段把 42 存档位屏的确认动作接到 `LoadSequencer`(经 `SaveSlotCommandRouter`,
            // **不**直调 `SaveService.Load` —— 那会漏掉读档后的锚点复位 / 兜底复位 / 派生态重建)。
            // ⚠️ 未接存档目录(`LoadSequencer` 为 null)⇒ 路由仍挂上,但 `TryLoad` 恒 false
            //    (不崩、不造动作;与 42 侧「缺写通道 = 留空」同纪律)。
            // 🔁 2026-11-01 落笔轮:第三个形参 = 写半边(`TrySave` → `Checkpoint`)。
            //    未接存档目录 ⇒ `SaveService` 为 null ⇒ `TrySave` 恒 false(与新行照画同纪律)。
            _saveSlotCommands = new SaveSlotCommandRouter(
                _services.LoadSequencer,
                (slot, ex) => LogLoadFailure(slot, ex),
                _services.SaveService,
                // 🔁 2026-11-03(甲方案第一层):落笔受理 ⇒ 请求重画槽位行。
                //    不在此处重探(落盘异步),重探在 `SkeuoRuntimeDriver.OnTickEdge` 等队列排空后。
                () => _uiDriver?.MarkSaveSlotRowsDirty(),
                // 🔁 ADR-038 §D3(2026-11-03):读档成功 ⇒ 用该档记录的时间三参数覆盖运行时参数。
                //    ⚠️ 不覆盖 = 用当前构建的值重放旧档 ⇒ 日边界 / 昼夜 / 52 的 TODMult 全错位。
                // 🔁 2026-11-05:读档成功也请求重探 —— 读档会**置锁**(`AC-7a-19 (d)`),
                //    而「已锁」是靠行文字呈现的(`SaveSlotItem.RowText`)⇒ 不重探的话,
                //    摊开的册子上那一行要等玩家关掉再打开才变得出来。
                () =>
                {
                    HydrateTimeParamsAfterLoad();
                    RestorePlayerCellFromStream();
                    // 🔁 2026-11-05(用户实测「点已有存档,选择读取,没有重新进入游戏」):
                    //    读档成功后**一次关到世界** —— `save-slots-7b.md` §9 要求
                    //    「载入执行 → 画押笔迹 → **合册** → 拆序」。此前这里只重探槽位行、
                    //    **从不关屏** ⇒ 册子还摊着、画面没变 ⇒ 玩家只能读成「什么都没发生」。
                    _uiDriver?.CloseAllModals();
                    _uiDriver?.MarkSaveSlotRowsDirty();
                },
                // 🔁 2026-11-04(ADR-041 §D8):落笔失败走**自己的**记录面 ——
                //    共用 `LogLoadFailure` 会把「覆盖被拒」打成「[7b][载入] … 载入失败」。
                saveFailureSink: LogSaveFailure);
            _uiDriver.SaveSlotSink = _saveSlotCommands;

            // 🔁 2026-11-03(甲方案第一层):槽位行**每次开册重探磁盘**。
            //    病因:此前灌给 42 的只有 `ManualSaveSlots` 这个**常量** ⇒ 42 不知道
            //    哪一槽有档 ⇒ 空槽与有档槽长得一样(7b `OQ-SB-5` 登记的那个缺口),
            //    玩家看到的是「存了,什么都没发生」。
            //    ⚠️ 未接存档目录 ⇒ `SaveService` 为 null ⇒ 全体「未绑定」(零字),
            //       与今天的形态一致 —— 不冒充空槽。
            _uiDriver.SaveSlotViewProvider = BuildSaveSlotViews;
            // 写盘未落定时不重探(否则按了「留档」立刻重画会读到写之前的磁盘状态)。
            _uiDriver.SaveInFlight =
                () => _services.SaveService != null
                      && _services.SaveService.WriteQueue != null
                      && _services.SaveService.WriteQueue.Outstanding > 0;
            _uiDriver.SaveSlots = DisplayedSaveSlots;

            // ── 6.3 模态开合入口(2026-10-31 模态开启器轮;同夜 Amendment C 改接暂停壳)──
            // 病因:42 只有序列化的 `_screenName`,**零运行期换屏入口** ⇒ 存档位屏开不出来 ⇒
            // 上段接好的载入链在运行期仍不可达。本驱动 = 那条链的**最后一段**。
            // 🔁 2026-10-31 晚:开合对象由**存档位**改为**暂停壳**(ADR-013 §十-C 立 Pause 为
            //    第八员)—— 于是路径回到 7b 写的形状:Escape → 暂停壳 → 「存档位」行 → 存档位。
            //    `DriverPauseCommandSink` 就是暂停壳那两行的去向(继续 = 合壳 / 存档位 = 换屏)。
            // 🔁 2026-11-05(用户裁定):暂停壳第四行「退出游戏」。
            //    ⚠️ 与主菜单第四行**同一个出口**(`Application.Quit()`;退出保存走既有的
            //    `Application.wantsToQuit` 钩子,此处**不写盘** —— 写盘归 7a,两处各写一份必然分叉)。
            //    放进 `DriverPauseCommandSink` 会引入 `UnityEngine` 依赖到 42 侧程序集面,
            //    故以委托注入(ADR-025 §①)。
            // 🔁 2026-11-05 晚改判(用户裁定):第四行由「退出游戏」改为**「返回主菜单」**。
            //    「退出游戏」只留在主菜单那一行(第一层);局内点是**回第一层**,不是退进程。
            //    实现见 `ReturnToMainMenuAsync`(落盘 → 关世界 → 重开主菜单屏)。
            _uiDriver.PauseSink = new DriverPauseCommandSink(_uiDriver,
                returnToMainMenu: RequestReturnToMainMenu,
                // 🔁 2026-11-05:「保存」行一键落笔(参照英灵神殿第二层)。
                //    目标槽**不经 UI** —— 台账的 `NextWritableSeq` 是唯一宿主(ADR-013 §9 C3);
                //    返回 false = 门拒(叙事上下文 / 未接线),42 据此**如实显示**失败,不假装成功。
                requestSave: () => _saveSlotCommands != null && _saveSlotCommands.TrySave());

            // 🔁 2026-11-05:「上次保存：N 分钟前」的来源(参照英灵神殿第二层)。
            //    ⚠️ **墙钟只用于呈现** —— 不进存档头 / 三流 / sim(ADR-040 §D4 同一条边界)。
            //    读**当前世界目录**下最新一份 `save_*.bin` 的 mtime;无档 ⇒ -1(屏上写「尚未保存」)。
            _uiDriver.SaveAgeMinutes = () =>
            {
                string dir = _services?.SaveService?.BasePath;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return -1;
                DateTime newest = DateTime.MinValue;
                foreach (string f in Directory.EnumerateFiles(dir, "save_*.bin"))
                {
                    DateTime t = File.GetLastWriteTimeUtc(f);
                    if (t > newest) newest = t;
                }
                if (newest == DateTime.MinValue) return -1;
                double minutes = (DateTime.UtcNow - newest).TotalMinutes;
                return minutes < 0d ? 0 : (int)minutes;
            };

            // 🔁 2026-11-04(`TD-skeuo-06`):炮制屏点火的生产装配。
            // ⚠️ 任一件缺失 ⇒ **不构造**,并显式告警(与 `ConsequenceDriver` 同纪律:
            //    null 可观测,不是「静默降级成空转」)。
            if (_services.CraftCompletionSettler != null
                && _services.InventoryLoad != null
                && _services.RecipeSource != null
                && _services.ContainerSelection != null)
            {
                var craftWriter = new DaYiJingCheng.Sim.World.CraftEventWriter(
                    _services.EventSink, _services.Encoder, _services.IdAuthority);
                var worldForTool = _services.World;
                int craftActor = actorId;
                _processingSink = new DriverProcessingCommandSink(
                    _uiDriver,
                    _services.RecipeSource,
                    // 料位 = 20 屏的选中态,**对齐**的两份清单由 store 一趟产出。
                    () =>
                    {
                        _services.ContainerSelection.SnapshotSelection(out long[] ids, out int[] qs);
                        return new ProcessingMaterials(ids, qs);
                    },
                    // 单件重量:与起货结算**同一份**映射(`CraftCompletionSettler` 持有两张表)。
                    (ordinal, line) =>
                        _services.CraftCompletionSettler.UnitWeightOfOutputLine(ordinal, line, craftActor),
                    // 载重:20 的投影求和(每次现算,不持快照)。
                    () =>
                    {
                        DaYiJingCheng.Sim.InventoryProjection proj =
                            _services.InventoryLoad.Rebuild().Projection;
                        return proj != null ? _services.InventoryLoad.LoadOf(proj, craftActor) : 0L;
                    },
                    // 器具格:结构注册表按 `structure_id` 解析(F-5.4 的 ToolCell 锚点)。
                    id => worldForTool != null
                          && worldForTool.Structures.TryGet(
                                 id, out DaYiJingCheng.Sim.World.StructureInstance inst)
                        ? inst.Anchor
                        : (DaYiJingCheng.Sim.Contracts.WorldPos?)null,
                    new HarvestSkillLevelProjection(() => _services.Stream.Events, _services.Decoder),
                    _services.Stream.Events,
                    _services.Decoder,
                    _services.CarryCap.HasValue
                        ? new DaYiJingCheng.Sim.CarryCapacity(_services.CarryCap.Value)
                        : (DaYiJingCheng.Sim.CarryCapacity?)null,
                    () => _tickDriver != null ? _tickDriver.CurrentTick : 0L,
                    craftActor,
                    craftWriter,
                    // 🔁 2026-11-11(`TD-processing-09`):三个修正分量的真值面。
                    //    `null` ⇔ 装配层没接世界(24 的 memo / 6 的基线都要 `structures`)⇒
                    //    sink 退回中性 0 并告警(可观测降级)。
                    BuildProcessingEnvMods());
            }
            else
            {
                Debug.LogWarning("[BootRoot] 炮制点火未装配(缺 CraftCompletionSettler / InventoryLoad / " +
                                 "RecipeSource / ContainerSelection 之一)—— 屏上按配方会明确报「点火拒发」, " +
                                 "不是静默空操作(TD-skeuo-06)。");
            }
            EnsureModalInput();

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

            // fail-loud(AudioListener 单挂点 · AC-ADR020-10):>1 即抛。
            // 把「运行期每帧刷 3 万条日志 →  Editor 卡死」的失效模式提前成具名异常。
            AssertSingleAudioListener();

            _booted = true;

            // ── 8. 「继续诊治」读档(2026-11-01)→ 必须排在启动序**全部完成之后** ──
            // 理由:读档要换流并触发派生态重建(`LoadSequencer` 的第 2–5 步),而派生态
            // (在场视图 / 视觉侧 / 玩家 / 相机 / UI 根)在启动序里是**后置**建的 ——
            // 把读档提到 2.95(菜单闸门处)就等于在半个世界之上回滚。
            // ⚠️ 分支纪律:菜单上按下「继续」时才走;`_continueSlot == null`(无档)时
            // 世界已按新局装载,本段**不发生任何动作**(新局不覆盖任何文件)。
            if (_continueSlot.HasValue)
            {
                TryContinueFromSave(_continueSlot.Value);
            }
            else
            {
                // ── 8b. 第一份存档(2026-11-04 用户裁定「只做首份存档」)──
                // 为什么需要:规则六的三个触发点对新局一个都不覆盖(事件触发要先有叙事事件、
                //   兜底要等 6000 tick ≙ 5 分钟、退出要关窗、手动槽要玩家自己按)⇒ 开局前
                //   5 分钟内崩溃 / 强杀 = 盘上零存档。本处补的是那个窗口。
                // 归类:这是**定时兜底的 t = 0 实例**(同一个 `AutoSaveSlot`、同一个 `Checkpoint`
                //   入口),不新增第四类触发点 ⇒ `AC-7a-08`「恰三处」的计数不变。
                // 守卫:`_continueSlot == null` 有两种成因 —— 真没档,或**查询失败**。
                //   后者「有没有档」未知,写下去可能覆盖上去 ⇒ 由 `FirstSessionSave` 复查。
                // 失败方向:不阻塞启动序(玩家必须进得去),只记日志。
                if (FirstSessionSave.TryEstablish(_services.SaveService, out Exception firstSaveError))
                {
                    Debug.Log("[BootRoot][7a] 首次启动:已受理初始存档(自动槽,兜底 t=0 实例)");
                }
                else if (firstSaveError != null)
                {
                    Debug.LogWarning("[BootRoot][7a] 初始存档未写(启动继续,不阻塞):" + firstSaveError);
                }
            }
        }

        /// <summary>
        /// 摊主菜单屏并等玩家确认(`design/ux/main-menu-42.md`)。
        /// <para>三步:① additive 装载 `MainMenu` 场景(ADR-023 §① 的第二个场景);
        /// ② 向 7a 问「有没有可继续的档」(**7a 是所有者**,42 只消费那个布尔与那个槽);
        /// ③ 开屏并 await 确认信号,确认后按 ADR-023 口径 additive 拆回场景。</para>
        /// <para>⚠️ **开不出来 ⇒ 不等**:无面板 / 屏未登记时记 LogError 后直接进世界。
        /// 让启动序挂在一个永不落焦的画面上,比「没有菜单」更糟(玩家连进都进不去)。</para>
        /// </summary>
        // ══════════════════════════════════════════════════════════════════
        // 多世界:目录解析 / 重装配(ADR-040 D1 / D5 / D7)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>装配用的服务袋(参数来自启动序缓存字段 —— 换世界时要重跑一次)。</summary>
        private CompositionRootServices AssembleServices()
            => CompositionRoot.Assemble(
                registry: null,
                dataProvider: _bootPreloaded ? DataCorePreloader.Provider : null,
                inputActions: _bootInputActions,
                audio: _audioSeams,
                // 51 遥测六旋钮(TD-epic-01 第十二片):预载成功 ⇒ 烘焙表;失败 ⇒ null(报告维持「未给」)。
                telemetryKnobs: ResolveTelemetryKnobs(_bootPreloaded ? DataCorePreloader.Provider : null),
                // 7a 存档目录(S19-1)。⚠️ 只有本处(生产启动序)显式给目录 —— 109 处
                // EditMode 夹具不给 ⇒ 它们不构造 SaveService(否则各在用户数据目录造一份)。
                // 🔁 2026-11-04(ADR-040 D1):此处给的是**世界目录**,不是存档根。
                //    夹具缝(`_saveBasePathOverride` 非空)下它直接就是世界目录。
                saveBasePath: _worldSaveDir,
                // S20-17:世界交给装配根 —— 注册表从 `world.Structures` 取(不另传 structures,
                // 否则「同给且不同实例」的 fail-closed 断言会抛),`StructureWriter` 由它构造。
                world: _bootWorld,
                // 听测期切档缝(`TD-listen-01` L-2 · 2026-10-29)。见 `ResolveDiagnosisQueryLevelOverride`。
                diagnosisQueryLevelOverride: ResolveDiagnosisQueryLevelOverride(),
                // 听测期病种索引缝(`TD-listen-01` L-3 · 2026-10-29)。见 `ResolvePatientRegistryIndexOverride`。
                patientRegistryIndexOverride: ResolvePatientRegistryIndexOverride(),
                // 病人 cue 的非色相可见对应物(`AC-13-F1` 的 42 侧达成面 · 2026-10-29 · 缺口 D)。
                // ⚠️ **本处是唯一生产装配点** —— 不给 ⇒ `PatientCueDriver` 一路不投递,
                //    AC-13-F1 的「默认可见」在运行期就不存在(禁借绿:缝存在 ≠ 面上有东西)。
                cueMarkSink: _cueMarks,
                // 语声字幕(`AC-44-15` `[L]` 半 · 2026-10-29)。文本源由装配根从事件表解析,
                // 此处只给**呈现载体**;不给则运行期无字幕可看(判据只落在单元夹具里 = 借绿)。
                subtitleSink: _subtitle);

        /// <summary>
        /// 解析本次要用的**世界存档目录**(ADR-040 D1 / D7)。
        /// <para>夹具缝(<see cref="_saveBasePathOverride"/> 非空)⇒ 该目录**就是**世界目录:
        /// 不建 <c>wN/</c> 子目录、不迁移、不构造 <see cref="WorldCatalog"/> ——
        /// 既有 PlayMode 夹具按「文件直接落在这个目录」断言,那条语义保持不变。</para>
        /// <para>生产 ⇒ <c>persistentDataPath/saves</c> 作根:先做一次性迁移(D7),再取
        /// **最近游玩且确有存档**的世界;一个都没有 ⇒ 返回 <c>w1/</c>(目录尚不存在,
        /// `SaveService` 构造时会建)。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">旧存档迁移失败(E-13 口径,拒绝降级)。</exception>
        private string ResolveWorldSaveDir()
        {
            if (!string.IsNullOrEmpty(_saveBasePathOverride))
            {
                _worldCatalog = null;
                return _saveBasePathOverride;
            }

            // ⚠️ **只有「主菜单形态」才启用多世界**(2026-11-04)。
            // 两条理由:① 没有菜单就没有选世界的入口 ⇒ 建了目录也无人可达(「看起来完成、
            // 实际不可达」);② 更实际的:代码构造的 PlayMode 夹具 `_bootWithMainMenu = false`,
            // 它们会走**真实** `persistentDataPath` —— 若在此做一次性迁移,跑测试就会搬动
            // 开发机上的真存档。生产(Boot.unity)恒为 true,故生产行为不变。
            if (!_bootWithMainMenu)
            {
                _worldCatalog = null;
                return string.IsNullOrEmpty(_saveRootOverride)
                    ? Path.Combine(Application.persistentDataPath, "saves")
                    : _saveRootOverride;
            }

            string root = string.IsNullOrEmpty(_saveRootOverride)
                ? Path.Combine(Application.persistentDataPath, "saves")
                : _saveRootOverride;
            _worldCatalog = new WorldCatalog(root);

            try
            {
                int moved = _worldCatalog.MigrateLegacy();
                if (moved > 0)
                {
                    Debug.Log($"[BootRoot][7a][ADR-040 D7] 旧存档已搬进 w1/:{moved} 个文件" +
                              "(字节零改动,不走 ISaveMigration)");
                }
            }
            catch (Exception e)
            {
                // ⚠️ **必须失败**,不能降级成「空世界继续跑」:那会把玩家存档留在一个
                //    谁也读不到的位置,而玩家看到的是「档没了」。
                throw new InvalidOperationException(
                    "[BootRoot][E-13][7a] 旧存档迁移失败 —— 拒绝在多世界形态下启动:" + e.Message, e);
            }

            string chosen = ResolveDefaultWorldDir(_worldCatalog);
            TouchWorld(chosen);
            return chosen;
        }

        /// <summary>默认世界 = **最近游玩且有档**的那个;一个都没有 ⇒ <c>w1/</c>(目录尚不存在)。</summary>
        private static string ResolveDefaultWorldDir(WorldCatalog catalog)
        {
            IReadOnlyList<WorldEntry> worlds = catalog.ListWorlds();
            for (int i = 0; i < worlds.Count; i++)
                if (WorldHasSaves(worlds[i].Directory)) return worlds[i].Directory;

            return catalog.DirectoryOf(1);
        }

        /// <summary>
        /// 是否存在**任何一个**有档的世界 —— 主菜单第一行文案(`继续诊治` / `开始诊治`)的唯一输入。
        /// <para>🔁 2026-11-04:原判据是「世界列表行数 &gt; 0」,世界列表面删除后改为本方法;
        /// 语义**等价**(列表本就只收有档的世界),只是不再为渲染而构造行。</para>
        /// </summary>
        private bool AnyWorldHasSaves()
        {
            if (_worldCatalog == null) return false;
            IReadOnlyList<WorldEntry> worlds = _worldCatalog.ListWorlds();
            for (int i = 0; i < worlds.Count; i++)
                if (WorldHasSaves(worlds[i].Directory)) return true;
            return false;
        }

        // 🔁 **2026-11-04 已删除**(用户裁定「删除不留」):原「删一局」的装配层处理
        //    (`OnMainMenuDeleteWorld`,`ADR-040 D6` 的二次确认后半程)与它专用的
        //    `BuildWorldRows` 一并移除。
        //    ⚠️ **如实登记**:`WorldCatalog.DeleteWorld` 同时删除 ⇒ 游戏内**没有任何删除世界
        //    的路径**(只剩手动删 `~/.config/unity3d/DaYiJingCheng/大医精诚/saves/wN/`
        //    —— 该路径于 2026-11-05 随 `TD-build-02` ① 的 company/product 变更而变);
        //    世界**选择**入口也随主菜单固定行一起移除 ⇒ 只能进「最近游玩」的那个世界。

        /// <summary>
        /// 记「这个世界的最后游玩时刻」(ADR-040 D4 的选择面)。
        /// <para>⚠️ 墙钟**只用于选择**:不进存档体、不进三流、不进 sim。
        /// 写失败**不阻断启动**(选择顺序退化为创建序,而世界本身毫发无损)。</para>
        /// </summary>
        private void TouchWorld(string worldDir)
        {
            if (_worldCatalog == null) return;
            if (!WorldCatalog.TryParseWorldDir(Path.GetFileName(worldDir), out int seq)) return;
            // ⚠️ **还没成形的世界不记时刻**(2026-11-04 实测):全新档启动时默认世界是
            //    `w1/` 这个**预定路径**(目录由 `SaveService` 构造建出,里面空的)。
            //    若此刻写进索引,`CreateWorld` 的高水位会把它当已用序号 ⇒ 玩家的第一局
            //    被编号成「第 2 局」,而 `w1/` 永远不存在。⇒ 有内容了才算「游玩过」。
            if (!Directory.Exists(worldDir)) return;
            if (Directory.GetFiles(worldDir).Length == 0) return;
            try
            {
                _worldCatalog.Touch(seq, DateTime.UtcNow.Ticks);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BootRoot][7a][ADR-040 D4] 世界时刻写入失败(选择顺序退化,不阻断启动):" + e);
            }
        }

        /// <summary>这个世界目录里是否**已有存档**(空目录 = 新局尚未写出第一份档)。</summary>
        private static bool WorldHasSaves(string dir)
        {
            if (!Directory.Exists(dir)) return false;
            return Directory.GetFiles(dir, "save_*.bin").Length > 0;
        }

        /// <summary>
        /// 把退出钩子接到**当前**服务袋的 `SaveService`(AC-7a-17)。
        /// <para>⚠️ 必须先解绑旧的:换世界会重建服务袋,旧闭包捕获的是旧世界的
        /// `SaveService` —— 不解绑就会「退出时写上一个世界」。</para>
        /// </summary>
        private void WireExitSave()
        {
            if (_wantsToQuitHandler != null)
            {
                Application.wantsToQuit -= _wantsToQuitHandler;
                _wantsToQuitHandler = null;
            }

            SaveService saveService = _services?.SaveService;
            if (saveService == null) return;

            _sessionWasIncomplete = saveService.IsSessionIncomplete;

            _wantsToQuitHandler = () =>
            {
                try
                {
                    saveService.SaveOnExit();
                    // ⚠️ SaveOnExit **不抛**写盘失败(它自己置位 + 交观察者),
                    //    但**会抛**驱动源缺失(编程错误)。两者都在此兜住。
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        "[BootRoot][AC-7a-17] 退出保存失败(不阻塞退出):" + e);
                }
                return true;   // 恒放行退出 —— 「不卡退」是硬约束
            };
            Application.wantsToQuit += _wantsToQuitHandler;
        }

        /// <summary>
        /// 菜单选了**另一个世界** / 新起一局之后:换目录并整体重装配(ADR-040 D5)。
        /// <para>7a 的 `SaveService` 按构造期 `basePath` 工作 ⇒ 换世界只能重建服务袋。
        /// 代价 = 一次装配(发生在**世界装载之前**,没有正在跑的 sim,故无状态可丢)。</para>
        /// </summary>
        private void ReassembleForWorld(string worldDir)
        {
            Debug.Log($"[BootRoot][7a][ADR-040 D5] 切换世界目录:{_worldSaveDir} → {worldDir}");

            _services.Dispose();
            _worldSaveDir = worldDir;
            _services = AssembleServices();
            TouchWorld(worldDir);

            // 「继续到哪一档」跟着换 —— 否则启动序末会拿**上一个世界**的槽去读档。
            _continueSlot = null;
            if (_services.SaveService != null && _services.SaveService.TryGetMostRecentSave(out SaveSlot slot))
                _continueSlot = slot;

            // 换袋之后三处「绑在旧袋上」的接线必须重做(否则它们指向已 Dispose 的实例)。
            WireExitSave();
            RebindDeviceReadiness();
        }

        /// <summary>把设备态读数重新绑到新袋的 `EmergencyManualDriver`(2.9 的设备态挂接那一半)。</summary>
        private void RebindDeviceReadiness()
        {
            if (_deviceState == null) return;
            try
            {
                _services.EmergencyManualDriver?.BindDeviceReadiness(
                    new Input.EmergencyDeviceReadiness(_deviceState));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BootRoot][TD-epic-01] 设备态重绑失败 ⇒ cause 记降级:" + e);
            }
        }

        private async Task PresentMainMenuAsync()
        {
            // ① MainMenu 场景 additive 装载(E-13 同口径:Failed ⇒ 具名异常 + rethrow)
            try
            {
                _mainMenuLoadHandle = Addressables.LoadSceneAsync(MainMenuSceneAddress, LoadSceneMode.Additive);
                await _mainMenuLoadHandle.Task;
                if (_mainMenuLoadHandle.Status == AsyncOperationStatus.Failed)
                    throw new InvalidOperationException(
                        "LoadSceneAsync(mainmenu) 返回 Failed" +
                        (_mainMenuLoadHandle.OperationException != null
                            ? ":" + _mainMenuLoadHandle.OperationException.Message
                            : string.Empty));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootRoot][E-13] MainMenu 场景 additive 加载失败" +
                               $"(address = {MainMenuSceneAddress}):{e}");
                throw;
            }

            // ② 「有没有档」(**2026-11-04:主菜单不再显示世界列表**)
            //    ⚠️ 判据 = **任何一个**世界目录里有档 —— 空目录(新局尚未写出第一份档)不计,
            //       否则第一行会写成「继续诊治」而点进去什么都没有。
            bool hasSave = false;
            _continueSlot = null;
            if (_worldCatalog != null) hasSave = AnyWorldHasSaves();

            // 「继续到哪一档」仍由 7a 答(按 tick,世界内语义不变);世界由 `_worldSaveDir` 定。
            SaveService saves = _services.SaveService;
            if (saves != null)
            {
                bool hasSlot = saves.TryGetMostRecentSave(out SaveSlot recent);
                if (_worldCatalog == null) hasSave = hasSlot;   // 夹具缝:退回「目录里有没有档」
                if (hasSlot) _continueSlot = recent;
                // 门 A 下 7a 无日志面 ⇒ 失败事实在这里落下(不是静默吞掉)
                if (saves.LastSaveQueryError != null)
                    Debug.LogWarning("[BootRoot][主菜单] 存档目录查询失败,按「无存档」处理:" +
                                     saves.LastSaveQueryError.Message);
            }
            else
            {
                // 未指定存档目录 = 7a 未装配(合法形态,见 Assemble 的 saveBasePath 注)
                Debug.LogWarning("[BootRoot][主菜单] 无 7a 存档服务 ⇒ 按「无存档」处理(文案 = 开始诊治)");
            }

            // ③ 开屏 + 等确认
            // ⚠️ **官方 UI 输入桥必须先于开屏**(B-6-1 的官方桥在第 6.7 步才建,那一步
            //    在 World 装载之后 —— 菜单期若没有它,Enter / 空格 / 手柄南键只能落到引擎内置
            //    `DefaultEventSystem` 上,而「焦点在、按了没反应」正是 B-6-1 的失效形态)。
            //    `EnsureEventSystem` 幂等(按类型复用),6.7 再调一次拿到的是同一个实例。
            _eventSystem = EnsureEventSystem();

            _uiDriver = FindOrCreateSkeuoUi();
            _uiDriver.MainMenuSink = new DriverMainMenuCommandSink(
                OnMainMenuContinue,
                newWorld: () =>
                {
                    // 「新起一局」= 新建世界目录;第一份存档由启动序 8b 的 `FirstSessionSave` 写
                    // (它看到的是空目录 —— 守卫通过)。
                    if (_worldCatalog != null)
                    {
                        WorldEntry created = _worldCatalog.CreateWorld();
                        _pendingWorldSaveDir = created.Directory;
                    }
                    OnMainMenuContinue();
                },
                // 「设置」= 开设置壳(与暂停壳第三行同一条路:闭集第四员 `SettingsShell`,
                // 不新增模态成员)。世界装载前打开是安全的 —— `OpenModalCore` 的注入面全部判空。
                openSettings: () => _uiDriver?.OpenModal(ModalId.SettingsShell),
                // 「退出游戏」= 唯一的游戏内退出入口(2026-11-04:此前全仓 `Application.Quit` 零调用,
                // 玩家只能点窗口关闭按钮)。退出保存走既有的 `Application.wantsToQuit` 钩子,不在这里写盘。
                quit: QuitApplication);

            // 🔁 **2026-11-05 深夜(`TD-skeuo-17`)**:模态开合驱动必须在这里就装配好。
            //    病因:启动序 **2.95** 的主菜单闸门早于 **6** 的 UI 装配 ——
            //    若等 6.3 才建驱动,**首次启动**的第一层里 `_modalInput` 是 null,
            //    `Update` 的 `_modalInput?.OnFrame()` 静默空转 ⇒ 「设置」壳按 ESC 关不掉
            //    (回程的第一层因为驱动已存在,同一操作正常 —— 这正是上轮误判「已修」的原因)。
            EnsureModalInput();

            // ⚠️ 信号**先建、后开屏**:开屏即落焦,确认动作在同一个调用栈里就可能发生
            //    (手柄 Submit 在下一帧即可到达)⇒ 「先开屏后建 TCS」会丢那次确认,启动序挂死。
            _mainMenuContinue = new TaskCompletionSource<bool>();

            // `_sessionWasIncomplete` = 启动时 7a 读出的「上次会话未完整」标志位(`:457`);
            // 42 只呈现它(`AC-7a-17` 后半句),置位/清除全归 7a。
            // ⚠️ 2026-11-04:世界列表参数已移除 —— 主菜单行集恒定四行,与夹具缝 / 生产路径无关。
            if (!_uiDriver.ShowSceneLevelScreen("MainMenu", hasSave, _sessionWasIncomplete))
            {
                Debug.LogError("[BootRoot][主菜单] 主菜单屏开不出来(无面板 / 屏未登记)⇒ " +
                               "不阻塞启动,直接进世界。");
                await UnloadMainMenuSceneAsync();
                return;
            }

            IsMainMenuOpen = true;

            // ③-b **UI 输入桥的「初始选中」必须先就绪** —— 否则键盘 / 手柄的 Submit
            //      **永不派发**(`InputSystemUIInputModule.ProcessNavigation` 的 move 与 submit
            //      都 gate 于 `currentSelectedGameObject != null`,见 `BootstrapEventSystemSelection` 注)。
            //      生产里这步在 `Update` 每帧幂等跑,但 `Update` 在 `_booted` 前**早退** ——
            //      菜单期必须自己等它,否则「焦点在、按了没反应」。
            await EnsureUiInputSelectionAsync();

            await _mainMenuContinue.Task;
            IsMainMenuOpen = false;

            _uiDriver.DismissSceneLevelScreen();
            await UnloadMainMenuSceneAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 「返回主菜单」(2026-11-05 晚 · 用户裁定:暂停壳第四行由「退出游戏」改为此)
        //
        // 语义:**回第一层,不退进程**。世界的 sim 与玩家都不销毁 —— 只把帧泵闸关掉、
        // 把 World 场景卸掉、把主菜单屏重新摊开;玩家在第一层按「继续诊治」即回到原局。
        // ⚠️ **不是什么**:不是 `Application.Quit()`(那只留在主菜单那一行),也不是「重开一局」。
        // ⚠️ **ADR-023 §⑤ 拆序六步的落地范围**:本处只做第 1 步(停 Step)与第 5 步
        //   (`UnloadSceneAsync`) + 病人视觉显式销毁;第 2 步(落盘 flush)由
        //   `SaveSlotCommandRouter.TrySave` 的同步 `Checkpoint` 覆盖;第 3/4/6 步
        //   (`ReleaseInstance` / `Release` handle / 断言登记簿空)归 ADR-023 的
        //   V-1…V-7(未跑,禁借绿)—— **本处不声称拆序已按六步验过**。
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>世界是否被「返回主菜单」挂起(挂起期 `_booted == false`)。</summary>
        private bool _worldPaused;

        /// <summary>上一帧世界是否被挡路模态冻住(`ADR-013 §十-C Amendment D`)。
        /// <para>用途只有一处:识别**解冻的那一帧**,让教学链补跑一次边沿
        /// (冻结期间进流的 `CaseOpened` 没有 tick 边沿可走)。</para></summary>
        private bool _worldFrozenLastFrame;

        /// <summary>「返回主菜单」形态下,第一层选中后的放行信号(与启动序的 `_mainMenuContinue` 分开)。</summary>
        private TaskCompletionSource<bool> _returnMenuChoice;

        /// <summary>暂停壳第四行的意图入口(不 async void:异常在内部收敛并落日志)。</summary>
        private void RequestReturnToMainMenu()
        {
            Debug.Log("[BootRoot][暂停壳] 玩家选择返回主菜单");
            _ = ReturnToMainMenuAsync();
        }

        /// <summary>把「回第一层」跑完:落盘 → 闸门关 → 关模态 → 卸世界 → 摊开主菜单。</summary>
        private async Task ReturnToMainMenuAsync()
        {
            if (_worldPaused || !_booted) return;          // 幂等:重复按不叠拆

            // ① **先落盘再回**(玩家点「返回主菜单」不该丢这一段进度)。
            //    ⚠️ 必须在关闸**之前**:`_booted = false` 会停 tick,而写盘的重探挂在 tick 边沿上。
            if (_saveSlotCommands != null)
            {
                try { _saveSlotCommands.TrySave(); }
                catch (Exception e) { Debug.LogError("[BootRoot][返回主菜单] 落盘失败:" + e); }
            }

            // ② 关帧泵闸(Update 早退 ⇒ 停 tick、停输入泵)+ 合上所有模态
            _booted = false;
            _worldPaused = true;
            _uiDriver?.CloseAllModals();

            // ③ 卸世界场景(ADR-023 §⑤ 第 5 步;病人视觉由 spawner 自持,须显式销毁)
            await UnloadWorldSceneForMenuAsync();

            // ④ 重开第一层
            await PresentMainMenuForReturnAsync();
        }

        /// <summary>卸 World 场景 + 销毁非场景根的病人视觉实体。</summary>
        private async Task UnloadWorldSceneForMenuAsync()
        {
            try
            {
                _services?.PatientVisualSpawner?.DestroyAll();
                _services?.StructureVisualSpawner?.DestroyAll();   // T22 批一(同一次显式清理,见 OnDestroy 注)
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BootRoot][返回主菜单] 病人视觉销毁失败(继续拆场景):" + e);
            }

            if (!_worldLoadHandle.IsValid()) return;
            AsyncOperationHandle<SceneInstance> handle = _worldLoadHandle;
            _worldLoadHandle = default;
            await Addressables.UnloadSceneAsync(handle).Task;
        }

        /// <summary>
        /// 摊开主菜单屏并等玩家选(返回主菜单形态)。
        /// <para>与启动序的 `PresentMainMenuAsync` **共用行集与装配**,差别只有两处:
        /// ① 场景要**重新加载**(启动序那次已在 2.95 之后卸载);
        /// ② 等待的是 `_returnMenuChoice` 而不是启动序的 `_mainMenuContinue`。</para>
        /// </summary>
        private async Task PresentMainMenuForReturnAsync()
        {
            try
            {
                _mainMenuLoadHandle = Addressables.LoadSceneAsync(MainMenuSceneAddress, LoadSceneMode.Additive);
                await _mainMenuLoadHandle.Task;
                if (_mainMenuLoadHandle.Status == AsyncOperationStatus.Failed)
                    throw new InvalidOperationException("LoadSceneAsync(mainmenu) 返回 Failed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BootRoot][返回主菜单] 主菜单场景加载失败,停在黑屏而非静默降级:{e}");
                throw;
            }

            bool hasSave = _worldCatalog != null
                ? AnyWorldHasSaves()
                : _services?.SaveService?.TryGetMostRecentSave(out _) == true;

            _returnMenuChoice = new TaskCompletionSource<bool>();

            if (!_uiDriver.ShowSceneLevelScreen("MainMenu", hasSave, _sessionWasIncomplete))
            {
                // 开不出屏 ⇒ **不能**把玩家晾在黑屏上:直接放行(等价于「继续」),并记红。
                Debug.LogError("[BootRoot][返回主菜单] 主菜单屏开不出来 ⇒ 直接回到世界(不让玩家卡死)");
                _returnMenuChoice.TrySetResult(false);
            }
            else
            {
                // 🔁 2026-11-11(T22 批七 · `TD-skeuo-14` 解除动作 ③):**回程也要置位**。
                //    这个标志此前只在启动序 `PresentMainMenuAsync` 里被置 —— 回程把它漏了,
                //    于是「第一层是否摊开着」在公开面上**恒假**,装配级夹具无从判(只剩反射)。
                //    ⚠️ 不是纯观测字段:它是「第一层在、世界不在」的唯一公开读数,
                //    置位点必须与 `ShowSceneLevelScreen` 同进同出(下方 dismiss 处清)。
                IsMainMenuOpen = true;
            }

            await EnsureUiInputSelectionAsync();
            await _returnMenuChoice.Task;

            _uiDriver.DismissSceneLevelScreen();
            IsMainMenuOpen = false;
            await UnloadMainMenuSceneAsync();

            // ⑤ 关第一层,回世界(选了「新起一局 / 换世界」则先换袋)
            await ResumeWorldFromReturnMenuAsync();
        }

        /// <summary>从第一层回到世界:必要时换袋(新起一局 / 换世界),重载 World 场景,重新开闸。</summary>
        private async Task ResumeWorldFromReturnMenuAsync()
        {
            if (!string.IsNullOrEmpty(_pendingWorldSaveDir)
                && !string.Equals(_pendingWorldSaveDir, _worldSaveDir, StringComparison.Ordinal))
            {
                ReassembleForWorld(_pendingWorldSaveDir);
                _pendingWorldSaveDir = null;

                // ⚠️ **换袋之后必须补首份存档** —— 启动序里这件事由第 8b 步的
                //    `FirstSessionSave.TryEstablish` 做(其注释逐字写「第一份存档由启动序 8b 写」)。
                //    回程不走启动序 ⇒ 不补这一步,「新起一局」建出来的世界里**一份档都没有**
                //    (实测:世界目录建了、里面空的;再点「继续诊治」会写成「开始诊治」)。
                //    失败方向与启动序一致:**不阻塞**,只记日志(玩家必须回得去)。
                if (FirstSessionSave.TryEstablish(_services.SaveService, out Exception firstSaveError))
                    Debug.Log("[BootRoot][返回主菜单][7a] 新世界:已受理初始存档(自动槽,兜底 t=0 实例)");
                else if (firstSaveError != null)
                    Debug.LogWarning("[BootRoot][返回主菜单][7a] 新世界初始存档未写(不阻塞):" + firstSaveError);
            }

            await LoadWorldSceneAsync();

            _worldPaused = false;
            _booted = true;
            Debug.Log("[BootRoot][返回主菜单] 已回到世界(闸门重新打开)");
        }

        /// <summary>加载 World 场景(启动序第 3 步与「返回主菜单」回程共用同一段)。</summary>
        private async Task LoadWorldSceneAsync()
        {
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
        }

        /// <summary>退出进程(主菜单第四行)。</summary>
        /// <remarks>
        /// 🔁 2026-11-05:补**编辑器分支**。`Application.Quit()` 在编辑器 Play 模式下**什么都不做**
        /// —— 玩家在编辑器里点「退出游戏」会以为坏了(实测:日志打了、进程不退)。
        /// 编辑器里退出的正确形态 = 停止 Play 模式。
        /// </remarks>
        private static void QuitApplication()
        {
            Debug.Log("[BootRoot][主菜单] 玩家选择退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>主菜单「继续诊治 / 开始诊治」的确认回调(幂等:双击 / 两次输入只算一次)。</summary>
        private void OnMainMenuContinue()
        {
            // 🔁 2026-11-05:「返回主菜单」形态下,同一个确认要放行的是**回世界**那条 await;
            //    启动序那条 `_mainMenuContinue` 此时早已完成(启动期就等过了)。
            if (_worldPaused) { _returnMenuChoice?.TrySetResult(true); return; }
            _mainMenuContinue?.TrySetResult(true);
        }

        /// <summary>
        /// 菜单期等「UI 输入桥的初始选中」就绪(逐帧调 <see cref="BootstrapEventSystemSelection"/>,
        /// 直到 `currentSelectedGameObject` 非空或超时)。
        /// <para>⚠️ 不存在「另一种做法」:面板载体 GO 由 interop bridge 在面板 attach 后的某帧新建,
        /// 启动序第 2.95 步时它通常还没有 ⇒ 不逐帧等就只能赌时序。</para>
        /// <para>⚠️ **超时不抛**:UI 输入不可用是「这条路上按不动」,不是「游戏不能启动」——
        /// 记一条警告,启动序继续(玩家仍可用鼠标点击那一行,指针路径不经此门)。</para>
        /// </summary>
        private async Task EnsureUiInputSelectionAsync(float timeoutSeconds = 3f)
        {
            if (_eventSystem == null || _uiDriver == null) return;

            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                BootstrapEventSystemSelection();
                if (_eventSystem.currentSelectedGameObject != null) return;
                await Task.Yield();
            }

            Debug.LogWarning($"[BootRoot][主菜单] 面板载体 {timeoutSeconds}s 内未就绪 —— " +
                             "键盘 / 手柄的提交可能不派发(鼠标点击不受影响)。");
        }

        /// <summary>按 ADR-023 口径拆回 MainMenu 场景(additive 拆,不用 Single)。</summary>
        private async Task UnloadMainMenuSceneAsync()
        {
            if (!_mainMenuLoadHandle.IsValid()) return;
            AsyncOperationHandle<SceneInstance> handle = _mainMenuLoadHandle;
            _mainMenuLoadHandle = default;
            await Addressables.UnloadSceneAsync(handle).Task;
        }

        /// <summary>
        /// 「继续最近档」的执行体(启动序第 8 步)。
        /// <para>经 <see cref="LoadSequencer"/> 而不是直调 <c>SaveService.Load</c>
        /// —— 后者会漏掉锚点游标复位 / 兜底计数清零 / 派生态重建 / 配置版本比对
        /// (与存档位屏的载入路径**同一个入口**,不另开第二条)。</para>
        /// <para>⚠️ **失败方向**:读档抛 ⇒ 记 <see cref="ContinueError"/> + LogError,世界停在
        /// **新局**态继续跑。这是**有意的**:启动期把玩家卡在菜单上(或闪退)比「从新局开始」更糟,
        /// 且新局不写任何存档文件 ⇒ 不覆盖既有档。但**不是静默** —— 错误留在事实出口上。</para>
        /// </summary>
        private void TryContinueFromSave(SaveSlot slot)
        {
            try
            {
                if (_services.LoadSequencer == null)
                {
                    Debug.LogError($"[BootRoot][主菜单] 继续诊治:LoadSequencer 未装配 ⇒ slot={slot.SlotSeq} 未读。");
                    return;
                }
                _services.LoadSequencer.Load(slot);
            }
            catch (Exception e)
            {
                ContinueError = e;
                Debug.LogError($"[BootRoot][主菜单] 继续诊治读档失败(slot={slot.SlotSeq})—— " +
                               $"世界保持新局态,不覆盖任何存档文件:{e}");
            }
        }

        /// <summary>
        /// 取 / 建拟物 UI 运行宿主(场景预摆优先,缺则自建)。
        /// <para>预摆 = Boot.unity 的 <c>SkeuoUI</c>(持 UIDocument + PanelSettings);
        /// 缺失 = 测试里没造 fixture ⇒ 自建(此时无 PanelSettings ⇒ 逻辑树在、不渲染,
        /// 见 <see cref="AssertSingleAudioListener"/> 旁注的实测记录)。</para>
        /// <para><b>归属(2026-11-02 · `TD-skeuo-02`)</b>:预摆件与自建件**一律归
        /// <c>BootRoot</c> 层级**(<c>SetParent(transform)</c>,与
        /// <see cref="EnsureEventSystem"/> 同款)。不归位时的失效形态是**静默跨用例串场**:
        /// 预摆壳在 Boot.unity 是与本对象**平级的根对象** ⇒ 本对象销毁后它存活 ⇒
        /// 下一件 PlayMode 夹具经 <c>FindFirstObjectByType</c> **复用同一只驱动** ⇒
        /// 其 <see cref="SkeuoRuntimeDriver.CurrentModal"/> / 屏 / 注入面原样带入。
        /// 归位后复用退化为「**同一 `BootRoot` 内**幂等」,销毁即随之销毁。</para>
        /// </summary>
        /// <summary>装配模态开合驱动(**幂等**:已装配则原样返回)。
        /// <para>🔁 <b>2026-11-05 深夜(`TD-skeuo-17`)</b>:必须在**任何模态可能被摊开之前**装配。
        /// 启动序里主菜单闸门在 <b>2.95</b>、UI 装配在 <b>6</b> —— 若只在 6.3 建驱动,
        /// 「首次启动」的第一层里本字段是 <c>null</c>,`Update` 的
        /// <c>_modalInput?.OnFrame()</c> **静默空转** ⇒ 设置壳按 ESC 关不掉。
        /// 回程的第一层因为驱动已存在而正常 —— 这个不对称正是上轮把它误判成「已修」的原因。</para>
        /// <para>⚠️ `_uiDriver` 未就绪时不构造(返回而不抛):它由
        /// <see cref="FindOrCreateSkeuoUi"/> 在场景预摆缺失时自建,而自建路径本身可能失败
        /// (无 PanelSettings)。此时**不装配**比装一个指向 null 的驱动器更安全;
        /// 下一个调用点(6.3)会再试一次。</para>
        /// </summary>
        private void EnsureModalInput()
        {
            if (_uiDriver == null) return;

            if (_modalInput == null)
            {
                _modalInput = new ModalInputDriver(
                    PlayerInputReader.PauseTogglePressed,
                    new DriverModalOpener(_uiDriver),
                    // 第一层(主菜单)**只关不开** —— 世界还没起,暂停壳没有意义;
                    // 但菜单里打开的设置壳仍须能被 ESC 关掉。
                    canOpenPause: () => _booted);
            }

            // 🔁 2026-11-10(T18 甲):**手册的开屏入口**。与暂停壳分开一个驱动,因为判据不同:
            //    ESC = 「有模态就关」;M = 「无模态才开、手册开着则关、别的模态摊着就不抢」。
            //    把它并进 `ModalInputDriver` 会让那一处收敛好的单条规则又要长分支。
            if (_manualToggle == null)
            {
                _manualToggle = new PaperCloseupToggleDriver(
                    PlayerInputReader.ManualPressed,
                    () => _uiDriver.CurrentModal,
                    TryPresentManual,
                    _uiDriver.CloseModal);
            }
        }

        /// <summary>
        /// 打开手册近景并喂数据(T18 甲)——
        /// <see cref="PaperCloseupToggleDriver"/> 的「开」这一面。
        /// </summary>
        /// <returns><c>true</c> = 真的开出来了;<c>false</c> = 本局没有手册内容 / 尚未入世 / 开屏被拒。</returns>
        /// <remarks>
        /// <para>⚠️ <b>菜单期不开</b>(`_booted == false`):手册是玩家身上那本,第一层里他没有。
        /// 这与暂停壳的 `canOpenPause: () => _booted` **同一条理由**,但判据落点不同 ——
        /// 那边是驱动的形参,这里是闭包里的早退(驱动只管开合,不管「本局有没有这本东西」)。</para>
        /// <para>⚠️ `PaperCloseups == null` = 本局没有手册内容(教学链未装配),不是故障 ——
        /// 返回 <c>false</c> 让驱动如实不计开屏次数(与 42 侧「缺写通道 = 留空」同纪律)。</para>
        /// </remarks>
        private bool TryPresentManual()
        {
            if (!_booted) return false;
            if (_uiDriver == null || _services == null) return false;
            if (_services.PaperCloseups == null) return false;
            return _services.PaperCloseups.TryPresent(_uiDriver);
        }

        private SkeuoRuntimeDriver FindOrCreateSkeuoUi()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<SkeuoRuntimeDriver>();
            if (existing != null)
            {
                // 预摆件是场景根 ⇒ 首次取用时归位一次(幂等:已在本层级下则不动)。
                // ⚠️ 只归位、**不重建** —— 预摆件持有 UIDocument + PanelSettings(渲染资源的
                //    唯一来源),重建会得到一个 panel == null 的空壳(2026-10-10 实测)。
                if (!existing.transform.IsChildOf(transform))
                    existing.transform.SetParent(transform, worldPositionStays: false);
                return existing;
            }

            var uiObj = new GameObject("SkeuoUI");
            // ⚠️ 必须归 BootRoot:`new GameObject` 落**当前活动场景**,且不归位则本对象销毁后
            //    它存活 ⇒ 跨夹具按类型复用同一驱动(见上注的失效形态)。
            uiObj.transform.SetParent(transform, worldPositionStays: false);
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
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
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
        /// AudioListener 单挂点断言(ADR-020 §七 / AC-ADR020-10)—— **fail-loud**。
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
        /// <summary>装配 44 侧设置注册表的运行期供给(S14-6 · 兑清 story-011 §登记项)。
        /// <para>预载成功 ⇒ 烘焙分区(AC-44-13 真源)成为 <see cref="AudioSettingsRegistry.Rows"/> 的供给;
        /// 预载失败 / provider 为 null ⇒ <b>不注入</b>,`Rows` 走 `FallbackRows`(与烘焙分区同值,
        /// 由 `AudioSettingsRegistryBinder` 的构建期双向值对账钉住)。</para>
        /// <para><b>为何是静态可直驱的方法而不是内联在 <see cref="Start"/> 里</b>:内联后本接线
        /// 只能经完整启动序(Addressables catalog + 场景)才可达 ⇒ EditMode 无从断言,
        /// 「接线是否存在」退化为不可证伪的口头承诺 —— 正是本 sprint 要根治的假绿形态。
        /// 抽成静态后由 <c>pipeline_gate_test.test_bootroot_*</c> 直驱。</para>
        /// <para>⚠️ <b>第二个直接调用点(2026-10-17)</b>:<c>Tests/PlayMode/precheck_allKeys_test.cs</c>
        /// 用它验证「<paramref name="preloaded"/> = true 但 <paramref name="provider"/> = null ⇒ 不注入」——
        /// 这是把 <c>Start</c> 里抛形属性求值摘出接线后、钉住「null 仍不注入」这一半的唯一载体。</para>
        /// <para>⚠️ 这里**刻意不抛**:设置值不是 sim 真值(端本地设备偏好,OQ-SS-5=甲),
        /// 「回退集」是设计内的降级面,不是静默失败 —— 与 E-13 对**数据产物**的硬失败口径不冲突
        /// (那条管「产物不可读」,此处是「未装配」)。</para></summary>
        /// <param name="preloaded">data-core 预载是否成功。</param>
        /// <param name="provider">预载成功时的 provider(null ⇒ 不注入)。</param>
        public static void ApplyAudioSettingsProvider(bool preloaded, IDataProvider provider)
        {
            if (!preloaded || provider == null) return;
            AudioSettingsRegistry.Provider =
                () => provider.Load<AudioSettingsDataSet>().Rows;
        }

        /// <summary>遥测 F4–F7 六旋钮的**取值缝**(TD-epic-01 第十二片)。
        /// <para>provider 非空 ⇒ 取烘焙表(<c>telemetry_analytics.cooked.bytes</c>,真源,加载期已过范围门);
        /// provider 为 null(预载失败)⇒ **返回 null**,由 <see cref="TelemetryReporter"/> 维持「未给」
        /// 形态 —— <b>不</b>造第二份常量兜底(与第十一片的设备旋钮刻意不同:GDD「数值归用户」,
        /// 51 不替用户转旋钮;凭空兜底默认值 = 没告诉任何人就改了口径)。
        /// W &lt; 1 / S &lt; 1 在<b>装载期</b>已被 <c>TelemetryAnalyticsCookedCodec</c> 拒收 ⇒
        /// 此处拿到的值必然合法;到达本处的越界值 ⇒ startup hard failure 已先发生。</para></summary>
        /// <param name="provider">data-core provider(null ⇒ null)。</param>
        public static TelemetryAnalyticsTuning? ResolveTelemetryKnobs(IDataProvider provider)
        {
            return provider?.Load<TelemetryAnalyticsDataSet>().Tuning;
        }

        /// <summary>
        /// 读档成功后用**档内记录**的时间参数覆盖运行时参数(ADR-038 §D3)。
        /// </summary>
        /// <exception cref="InvalidOperationException">读档返回了但没有可用的时间参数
        /// (codec / 迁移链契约破坏)⇒ fail-loud,不得沿用旧参数继续跑。</exception>
        private void HydrateTimeParamsAfterLoad()
        {
            Sim.Codec.SaveTimeParams? loaded = _services?.SaveService?.LastLoadedTimeParams;
            if (!loaded.HasValue)
                throw new InvalidOperationException(
                    "读档已返回,但档头没有可用的时间参数 —— " +
                    "ADR-038 §D3 / Amendment A 要求 v5 档必带时间三值 + 季二值 + 天气二值;" +
                    "沿用旧参数会让重放出的日边界与玩家经历的不同(fail-loud)");

            // R2(ADR-038 §Risk R2)已并入纯决策函数 —— 判据与 hydrate 一起可直测,
            // 不再有「实例方法里多一行、夹具够不着」的死角。
            Sim.TimeParams hydrated = HydrateTimeParams(
                loaded.Value, ResolveRollInterval(DataCorePreloader.Provider));

            _timeParams = hydrated;
        }

        /// <summary>
        /// 纯决策:用**档内**三值 + 现有季参数拼出运行时 <see cref="Sim.TimeParams"/>(ADR-038 §D3 / V-7)。
        /// <para>抽出为静态纯函数是为了让判据可直测 —— 私有方法只能靠反射评,而反射会把
        /// 「方法叫什么」写进夹具(改名即红,与被测行为无关)。</para>
        /// </summary>
        /// <param name="loaded">档头七值(时间三 + 季二 + 天气二;ADR-038 Amendment A)。</param>
        /// <param name="rollInterval">
        /// 52 的 `ROLL_INTERVAL`(ADR-038 §R2)。<c>&lt;= 0</c> = 「不知道」(未预载 / 夹具)⇒ 跳过整除门;
        /// 调用方必须显式传 <c>0</c> 表示「不判」,**不得**用某个默认值冒充「合规」。
        /// </param>
        /// <exception cref="InvalidOperationException">档内三值不可用(fail-loud)。</exception>
        /// <exception cref="ArgumentException">三值关系不成立(与 `TimeParams.Validate` 同源)。</exception>
        /// <exception cref="InvalidDataException">档内日长不被 <paramref name="rollInterval"/> 整除(R2)。</exception>
        public static Sim.TimeParams HydrateTimeParams(
            Sim.Codec.SaveTimeParams loaded, long rollInterval = 0L)
        {
            if (!loaded.IsSpecified)
                throw new InvalidOperationException(
                    "档头没有可用的时间参数 —— 拒绝沿用旧参数(ADR-038 §D3)");

            var hydrated = new Sim.TimeParams
            {
                TICKS_PER_DAY = loaded.TicksPerDay,
                NIGHT_START = loaded.NightStart,
                NIGHT_SPAN = loaded.NightSpan,
                // 🔁 Amendment A:季参数**也取自档内**(v5 起),不再沿用当前构建值。
                TICKS_PER_SEASON = loaded.TicksPerSeason,
                SEASONS_PER_YEAR = loaded.SeasonsPerYear,
            };
            hydrated.Validate();
            hydrated.ValidateSeason();

            // R2:档内日长必须仍被 52 的 roll_interval 整除 —— 否则日边界与预算窗口错位。
            if (rollInterval > 0L)
                RequireDivisibleWindow(hydrated.TICKS_PER_DAY, rollInterval);

            return hydrated;
        }

        /// <summary>
        /// 启动期交叉校验(ADR-038 §R2):当前装载的 5 日长必须被 52 的 `ROLL_INTERVAL` 整除。
        /// <para><c>time == null</c>(未预载 / 夹具)或取不到 52 ⇒ **不判** —— 「不知道」不等于「不合规」,
        /// 但也不得被读成「已校验通过」。</para>
        /// </summary>
        /// <exception cref="InvalidDataException">不整除 ⇒ 启动期硬失败。</exception>
        public static void RequireCrossDatasetWindow(Sim.TimeParams time, IDataProvider provider)
        {
            if (time == null) return;
            long rollInterval = ResolveRollInterval(provider);
            if (rollInterval <= 0L) return;
            RequireDivisibleWindow(time.TICKS_PER_DAY, rollInterval);
        }

        /// <summary>读 52 的 `ROLL_INTERVAL`(provider 为 null ⇒ 0 = 「不知道」,调用方跳过检查)。</summary>
        public static long ResolveRollInterval(IDataProvider provider)
            => provider == null ? 0L : provider.Load<RandomEventsDataSet>().RollInterval;

        /// <summary>
        /// 档内日长须被 `ROLL_INTERVAL` 整除(ADR-038 §Risk R2 / V-9)。
        /// </summary>
        /// <exception cref="InvalidDataException">不整除 ⇒ 拒收该档。</exception>
        public static void RequireDivisibleWindow(long ticksPerDay, long rollInterval)
        {
            if (rollInterval <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(rollInterval), rollInterval, "ROLL_INTERVAL 须 > 0");
            if (ticksPerDay % rollInterval != 0)
                throw new InvalidDataException(
                    $"档内 TICKS_PER_DAY({ticksPerDay})不被 52 的 ROLL_INTERVAL({rollInterval})整除 —— " +
                    "日边界与子区间边界错位,预算重置点不唯一(ADR-038 §R2)⇒ 拒收该档");
        }

        /// <summary>
        /// 装载并校验 5 的时间参数(<c>5_time.cooked.bytes</c>;ADR-038 §D1)。
        /// <para><b>与 <see cref="ResolveTelemetryKnobs"/> 的差别</b>:这里**有硬校验** ——
        /// <see cref="Sim.TimeParams.Validate"/> 是 AC-5-11 的执行体,非法配置 ⇒ 抛(启动期硬失败),
        /// 不返回 null。理由:遥测旋钮缺失只是「少一个功能」,而日长 / 昼夜参数非法会让
        /// 每一 tick 的相位与 52 的 `TODMult` 都算错 ⇒ 静默腐蚀,**必须 fail-closed**。</para>
        /// <para>⚠️ <c>provider == null</c> 仍是**合法**返回 null 的唯一情形(未预载 / 测试假件):
        /// 那时根本没有产物可校验,不构成「配置非法」。</para>
        /// </summary>
        /// <exception cref="ArgumentException">产物里的三参数关系不成立(AC-5-11)。</exception>
        public static Sim.TimeParams ResolveTimeParams(IDataProvider provider)
        {
            if (provider == null) return null;

            TimeParamsDataSet data = provider.Load<TimeParamsDataSet>();
            var p = new Sim.TimeParams
            {
                TICKS_PER_DAY = data.TicksPerDay,
                NIGHT_START = data.NightStart,
                NIGHT_SPAN = data.NightSpan,
                // 🔁 Amendment A(2026-11-03):季参数已裁定并进 schema(schema 2)。
                TICKS_PER_SEASON = data.TicksPerSeason,
                SEASONS_PER_YEAR = data.SeasonsPerYear,
            };
            p.Validate();
            p.ValidateSeason();     // AC-5-11 的另两条:季长 > 0 · 一年 ≥ 2 季
            return p;
        }

        /// <summary>
        /// 装载并校验 5 的天气参数(<c>5_weather.cooked.bytes</c>;ADR-038 Amendment A)。
        /// <para>非法配置 ⇒ 抛(启动期硬失败),与 <see cref="ResolveTimeParams"/> 同口径。
        /// <c>provider == null</c> ⇒ null(未预载 / 夹具,不构成「配置非法」)。</para>
        /// </summary>
        /// <exception cref="ArgumentException">块长 / 强度上界关系不成立(AC-5-11)。</exception>
        public static Sim.WeatherParams ResolveWeatherParams(IDataProvider provider)
        {
            if (provider == null) return null;

            WeatherParamsDataSet data = provider.Load<WeatherParamsDataSet>();
            var p = new Sim.WeatherParams
            {
                WEATHER_BLOCK_TICKS = data.WeatherBlockTicks,
                INTENSITY_MAX = data.IntensityMax,
            };
            p.Validate();
            return p;
        }

        /// <summary>听测期诊断 / 听诊档位覆盖缝(`TD-listen-01` **L-2**,2026-10-29 用户裁定「修」)。
        /// <para><b>病因</b>:`CompositionRoot` 的 <c>queryLevel</c> 是局部常量 <c>1</c> 且全库零回写
        /// ⇒ <c>TierIndex(1) = 0</c>(<c>1 &lt; SLOT_BOUNDS[0] = 10</c>)⇒ **恒最低档**
        /// ⇒ `AC-44-02` 的三档 forced-choice **没有第二 / 第三档可听**。</para>
        /// <para><b>本缝的形状</b>:读环境变量 <c>DYJC_DIAG_QUERY_LEVEL</c>(**诊断技能等级**,非档序),
        /// 合法 ⇒ 返回该值;<b>未设 / 非法 ⇒ 返回 <c>null</c></b>(= 装配层回落常量 1,逐位不变)。</para>
        /// <para>⚠️ <b>传的是技能等级,不是档序</b>:档位由
        /// <see cref="DiagnosisReadFloorEvaluator.TierIndex"/> 按
        /// <c>SLOT_BOUNDS = {10, 20, 50}</c> 求出。要听三档,分别传
        /// <c>1</c>(→ 档 0 低)· <c>10</c>(→ 档 1 中)· <c>20</c>(→ 档 2 高)。
        /// 传 <c>0/1/2</c> 会**全部落在档 0**(那正是本债原来的故障形态)。</para>
        /// <para>⚠️ **非法值不静默兜底成合法值** —— 那会让「我设了 20 但听的是 1」成为静默故障;
        /// 非法即打一条 warning 并返回 null(可观测,不 fail-hard:听测是开发期动作,
        /// 不该因环境变量打错而阻断启动)。</para>
        /// <para><b>为何是环境变量而不是按键 dev 开关</b>:① 本仓**零 dev 开关先例**
        /// (`grep DevSwitch/DebugSwitch` 全仓无生产命中),新造一套交互 = 新立机制;
        /// ② 环境变量在**启动前**设定,天然不可在运行期被误改;③ 它对 109 处 EditMode 调用方
        /// **结构性不可达**(它们不经 <see cref="Start"/>)⇒ 零回归风险。</para>
        /// <para>⚠️ <b>不越界到 30 的生产契约</b>:<c>ISkillLevelQuery.QueryLevel</c> 仍是真等级的
        /// 唯一来源;本缝只是**听测期的受控注入**,不是等级系统(承债册 L-2 解除条件原文
        /// 「或在听测期提供受控注入缝」)。</para></summary>
        /// <returns>覆盖值(诊断技能等级,≥ 0);null = 未给 / 非法 ⇒ 装配层用默认常量 1。</returns>
        public static int? ResolveDiagnosisQueryLevelOverride()
        {
            string raw = Environment.GetEnvironmentVariable("DYJC_DIAG_QUERY_LEVEL");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!int.TryParse(raw.Trim(), out int level) || level < 0)
            {
                Debug.LogWarning($"[BootRoot] DYJC_DIAG_QUERY_LEVEL=\"{raw}\" 非法(须 ≥ 0 的整数技能等级)" +
                                 " ⇒ 忽略,诊断 / 听诊档位回落默认常量 1");
                return null;
            }
            int tier = DiagnosisReadFloorEvaluator.TierIndex(level);
            Debug.Log($"[BootRoot] 诊断 / 听诊档位覆盖:技能等级 {level} ⇒ 档 {tier}" +
                      "(DYJC_DIAG_QUERY_LEVEL;听测期专用)");
            return level;
        }

        /// <summary>听测期病人病种索引覆盖缝(`TD-listen-01` **L-3**,2026-10-29 用户裁定「修」)。
        /// <para><b>病因</b>:`CompositionRoot` 原用**编译期常量** `AuditionDiseaseForListening`
        /// 选注册表条目 ⇒ 听测须改代码重编,而「改完忘改回」会把听测用的长曲线漏进出货包(静默)。</para>
        /// <para><b>本缝的形状</b>:读环境变量 <c>DYJC_AUDITION_REGISTRY_INDEX</c>(整数索引),
        /// 合法 ⇒ 返回该值;<b>未设 / 非法 ⇒ 返回 <c>null</c></b>(= 装配层回落索引 0,逐位不变)。
        /// ⚠️ **越界由装配层 fail-loud**,不在本缝钳位 —— 见 `CompositionRoot` 的越界断言。</para>
        /// <para>⚠️ <b>为什么索引而不是布尔</b>:布尔只能表达「第 0 条 or 第 1 条」,将来若加第三条
        /// 听测曲线就要再改一次签名;索引把「选哪条」交回数据侧(注册表顺序),不写死条目数。</para></summary>
        /// <returns>覆盖索引(≥ 0);null = 未给 / 非法 ⇒ 装配层用索引 0。</returns>
        public static int? ResolvePatientRegistryIndexOverride()
        {
            string raw = Environment.GetEnvironmentVariable("DYJC_AUDITION_REGISTRY_INDEX");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!int.TryParse(raw.Trim(), out int index) || index < 0)
            {
                Debug.LogWarning($"[BootRoot] DYJC_AUDITION_REGISTRY_INDEX=\"{raw}\" 非法" +
                                 "(须 ≥ 0 的整数索引)⇒ 忽略,病人病种回落索引 0");
                return null;
            }
            Debug.Log($"[BootRoot] 病人病种索引覆盖 = {index}" +
                      "(DYJC_AUDITION_REGISTRY_INDEX;听测期专用)");
            return index;
        }

        /// <summary>设备切换迟滞六旋钮的**取值缝**(TD-epic-01 第十一片)。
        /// <para>预载成功 ⇒ 取烘焙表(<c>input_device_switch.cooked.bytes</c>,真源);
        /// 预载失败 / provider 为 null ⇒ 取 <see cref="CompositionRoot.EmergencyDeviceSwitchSeed*"/>
        /// 常量兜底(设计内降级面,与 <see cref="ApplyAudioSettingsProvider"/> 同款)。</para>
        /// <para><b>为何是静态可直驱的方法而不是内联在 <see cref="Start"/> 里</b>:承
        /// <see cref="ApplyAudioSettingsProvider"/> 同款理由 —— 内联后本接线只能经完整启动序
        /// (Addressables catalog + 场景)才可达 ⇒ EditMode 无从断言。</para>
        /// <para>返回序 = <c>DeviceStateManager</c> 构造序(轴列三 + 指针列三)。</para></summary>
        /// <param name="provider">data-core provider(null ⇒ 常量兜底)。</param>
        /// <returns>(axisThreshold, axisTolerance, axisDwell, pointerThreshold, pointerTolerance, pointerDwell)。</returns>
        public static (int axisThreshold, int axisTolerance, int axisDwell,
                       int pointerThreshold, int pointerTolerance, int pointerDwell)
            ResolveDeviceSwitchKnobs(IDataProvider provider)
        {
            if (provider != null)
            {
                var data = provider.Load<DeviceSwitchTuningDataSet>();
                // ⚠️ Fix.Raw 是 long,而 DeviceStateManager 收 Q16.16 **int**(Gameplay.Input
                //    不引 Sim.Contracts —— 见 DeviceSwitchTuning.cs 头注的落点注记)。
                //    窄化是**契约面**而非顺手转换:装载期 InputDeviceSwitchBinder 已断区间,
                //    越界值到不了这里;真越界 ⇒ 窄化抛 OverflowException(启动期硬失败,零静默截断)。
                return (checked((int)data.Tuning.Axis.DeviceSwitchThreshold.Raw),
                        checked((int)data.Tuning.Axis.DriftTolerance.Raw),
                        data.Tuning.Axis.DwellTicks,
                        checked((int)data.Tuning.Pointer.DeviceSwitchThreshold.Raw),
                        checked((int)data.Tuning.Pointer.DriftTolerance.Raw),
                        data.Tuning.Pointer.DwellTicks);
            }

            return (CompositionRoot.EmergencyDeviceSwitchSeedAxisThreshold,
                    CompositionRoot.EmergencyDeviceSwitchSeedAxisTolerance,
                    CompositionRoot.EmergencyDeviceSwitchSeedAxisDwell,
                    CompositionRoot.EmergencyDeviceSwitchSeedPointerThreshold,
                    CompositionRoot.EmergencyDeviceSwitchSeedPointerTolerance,
                    CompositionRoot.EmergencyDeviceSwitchSeedPointerDwell);
        }

        /// <summary>装配 44 运行期音频链的启动序半边(story-018 · A-1/A-2/A-4/A-5/A-7)。
        /// <para>职责五件:① 从 provider 取音频事件表;② 建 <see cref="AudioClipLibrary"/> 并
        /// 逐行预载 clip(A-2);③ 解析混音器 —— **解析不到即 fail-loud**(A-7);④ 建
        /// <see cref="AudioBusResolver"/>(A-6 的运行期半边);⑤ 赋值
        /// <see cref="PlayerBusVolumeInitializer.MixerResolver"/> 并**显式**应用
        /// <c>FactoryDefaults</c>(A-5)。</para>
        /// <para><b>为何是静态可直驱的方法而不是内联在 <see cref="Start"/> 里</b>:承
        /// <see cref="ApplyAudioSettingsProvider"/> 同款理由 —— 内联后本接线只能经完整启动序
        /// (Addressables catalog + 场景)才可达 ⇒ EditMode 无从断言,「接线是否存在」退化为
        /// 不可证伪的口头承诺。抽成静态后由 <c>runtime_assembly_test</c> 直驱(注入
        /// <paramref name="mixerLoader"/> 假件即可覆盖 fail-loud 分支,零 catalog 依赖)。</para>
        /// <para><b>为何 <paramref name="provider"/> 为 null 是「返回 null」而非抛</b>:
        /// 预载失败 = 本机未重建 catalog / 测试环境 ⇒ 组合根走 <c>NullRenderTarget</c> 链,
        /// <c>AudioRenderAssembled = false</c> **可观测**(SAC-018-3 ②「不静默」)。而
        /// **混音器**解析失败不是环境问题、是 A-7 未补(组未登记)⇒ 那一处必须抛。</para></summary>
        /// <param name="provider">data-core provider(null ⇒ 缝不接,返回 null)。</param>
        /// <param name="mixerLoader">混音器装载缝(null ⇒ 走生产路径
        /// <see cref="AudioClipLibrary.LoadMixer"/>)。测试注入假件以覆盖 fail-loud 分支。</param>
        /// <param name="clipLoader">clip 装载缝(null ⇒ 走生产路径 Addressables)。测试注入假件以
        /// **观测释放**:`TD-audio-031` ② 的判据是「抛之前已预载的 clip 被 Dispose」,而
        /// EditMode 无 catalog ⇒ 生产路径下 `_handles` 恒空、`Dispose` 是空操作、**零判别力**。
        /// 经缝装来的 clip 由缝方持有,释放走 <see cref="AudioClipLibrary.Preload"/> 的
        /// <c>internalRelease</c> 通道。</param>
        /// <param name="clipRelease">释放缝方 clip 的回调(null ⇒ 交由缝方自行释放)。</param>
        /// <param name="busResolverFactory">总线解析缝(null ⇒ 生产路径 `new AudioBusResolver(mixer)`)。
        /// <para>⚠️ **为什么需要这条缝**(`TD-audio-031` ②,2026-10-29):该子项的判据是
        /// 「抛之前已预载的 clip 被 Dispose」,而唯一生产可达的抛点是 `AudioBusResolver`
        /// 的**缺组**分支。EditMode 下**造不出缺组混音器** —— 实测 `AudioMixer` 不是
        /// `ScriptableObject`(不能 `CreateInstance`)、`AudioMixerController` 不在公共 API
        /// (改组必须动盘上的作者态资产,不可接受)。⇒ 只有经缝注入一个必抛的解析器,
        /// 才能让那条 catch 真的被跑到(否则该分支只有纸面存在,零判别力)。</para></param>
        /// <returns>装配缝(provider 为 null 时返回 null)。</returns>
        /// <exception cref="InvalidOperationException">混音器解析不到(A-7)· 玩家音量默认集
        /// 键覆盖校验失败(A-5)。两者都是装配错,不是环境降级面。</exception>
        public static AudioAssemblySeams BuildAudioSeams(IDataProvider provider,
                                                         Func<AudioMixer> mixerLoader = null,
                                                         Func<string, AudioClip> clipLoader = null,
                                                         Action<AudioClip> clipRelease = null,
                                                         Func<AudioMixer, AudioBusResolver> busResolverFactory = null)
        {
            if (provider == null) return null;   // 缝不接:NullRenderTarget 链,可观测

            AudioEventDataSet table = provider.Load<AudioEventDataSet>();
            var clips = new AudioClipLibrary(table);

            // ③ 混音器解析(A-7)—— **先于 clip 预载**:fail-loud 分支不该先走一遍 catalog。
            AudioMixer mixer = ResolveMixerOrFail(clips, mixerLoader);

            // ② clip 预载(A-2)—— 逐行按事件表 assets[] 的 bare 名装载。
            // ⚠️ 缺条目**不抛**(承 AudioClipLibrary 头注:素材存在性已由构建期 D6 门拦下,
            //    运行期 miss = 装配错,记数可观测);是否真齐由 PlayMode 断言 MissingCues 为空。
            clips.Preload(loader: clipLoader, internalRelease: clipRelease);

            // ④⑤ 总线解析 + 玩家音量 —— 失败即释放已预载 clip(`TD-audio-031` ②)。
            var buses = BuildBusFaceOrRelease(mixer, clips, busResolverFactory,
                                              out IReadOnlyList<string> errors);

            return new AudioAssemblySeams
            {
                Clips = clips,
                Buses = buses,
                BusVolumeErrors = errors,
                Table = table,
            };
        }

        /// <summary>④ 总线解析 + ⑤ 玩家总线音量;任一步抛 ⇒ **先释放已预载的 clip** 再重抛。
        /// <para>🔁 2026-10-29(`TD-audio-031` ②,新立):`Preload` 成功之后,`AudioBusResolver`
        /// 的构造(缺组)与 `ApplyDefaults`(errors)都可能抛 —— 而抛出去之后**没有任何人持有
        /// <paramref name="clips"/>** ⇒ ≤15 个 Addressables 句柄随异常一起悬挂(下次 Play 前
        /// 不会有人 Release)。</para>
        /// <para>⚠️ 用 try/catch 而不是「先判断再抛」:两处抛点的判据都由**被调方**给出,
        /// 在调用方复算一遍 = 第二真源。catch → Dispose → 重抛,异常类型与报文原样保留。</para></summary>
        /// <param name="mixer">已解析的混音器。</param>
        /// <param name="clips">已预载的 clip 库(抛路径上由本方法负责释放)。</param>
        /// <param name="busResolverFactory">总线解析缝(null ⇒ 生产路径)。</param>
        /// <param name="errors">出口:A-5 的错误列表(空 = 全部成功)。</param>
        /// <returns>已解析的总线面。</returns>
        /// <exception cref="InvalidOperationException">缺组(A-6)或音量默认集失败(A-5)。</exception>
        private static AudioBusResolver BuildBusFaceOrRelease(AudioMixer mixer, AudioClipLibrary clips,
                                                             Func<AudioMixer, AudioBusResolver> busResolverFactory,
                                                             out IReadOnlyList<string> errors)
        {
            try
            {
                // ④ 总线解析器(A-6 运行期半边):七总线名 → AudioMixerGroup。
                var buses = busResolverFactory != null
                    ? busResolverFactory(mixer)
                    : new AudioBusResolver(mixer);

                // ⑤ 玩家总线音量(A-5)。
                errors = ApplyPlayerBusDefaultsOrFail(mixer);
                return buses;
            }
            catch
            {
                clips.Dispose();   // 幂等(`_disposed` 守卫);重抛保留原异常类型与报文
                throw;
            }
        }

        /// <summary>③ 混音器解析(A-7)。解析不到 = **装配错**,不是环境降级面 ⇒ 抛,不得静默跳过。
        /// <para>🔁 2026-10-29(`TD-audio-032` ③ 收尾)从 `BuildAudioSeams` 抽出 —— 原文案与抛点一字未改。</para></summary>
        /// <param name="clips">clip 库(生产路径用它装载混音器)。</param>
        /// <param name="mixerLoader">测试注入缝(null ⇒ 走生产路径)。</param>
        /// <returns>已解析的混音器(非 null)。</returns>
        /// <exception cref="InvalidOperationException">解析不到。</exception>
        private static AudioMixer ResolveMixerOrFail(AudioClipLibrary clips, Func<AudioMixer> mixerLoader)
        {
            AudioMixer mixer = mixerLoader != null ? mixerLoader() : clips.LoadMixer();
            if (mixer == null)
            {
                throw new InvalidOperationException(
                    "[BootRoot][story-018][A-7] 混音器解析不到(address = " +
                    AudioClipLibrary.AudioMixerAddress + ")—— Addressables 组未登记或 catalog 未重建。" +
                    "修法:跑菜单「大医精诚/数据管线/确保 audio Addressables 组」再「构建 Addressables 内容」。" +
                    "⚠️ 不得静默跳过:混音器缺失 ⇒ 七总线拓扑与音量滑块全失效(A-5/A-6)。");
            }
            return mixer;
        }

        /// <summary>⑤ 玩家总线音量默认集(A-5),失败即抛(半初始化已由初始化器自行回退)。
        /// <para>⚠️ `Bootstrap()` 已在本场景加载前跑过(那时 resolver 还是 null)⇒ 此处赋值后
        /// 必须**显式** `ApplyDefaults`,不能等自动入口。</para>
        /// <para>🔁 2026-10-29(`TD-audio-032` ③ 收尾)从 `BuildAudioSeams` 抽出 —— 原文案与抛点一字未改。</para></summary>
        /// <param name="mixer">已解析的混音器。</param>
        /// <returns>错误列表(空 = 全部成功)。</returns>
        /// <exception cref="InvalidOperationException">存在失败项。</exception>
        private static IReadOnlyList<string> ApplyPlayerBusDefaultsOrFail(AudioMixer mixer)
        {
            PlayerBusVolumeInitializer.MixerResolver = () => mixer;
            var errors = PlayerBusVolumeInitializer.ApplyDefaults(
                mixer, PlayerBusVolumeInitializer.FactoryDefaults);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "[BootRoot][story-018][A-5] 玩家总线音量初始化失败(半初始化已回退,零 SetFloat):\n" +
                    string.Join("\n", errors));
            }
            return errors;
        }

        /// <summary>
        /// 按名字查相机,**含 inactive**(<see cref="GameObject.Find"/> 只找 active)。
        /// <para>预摆 <c>Main Camera</c> 在启动序里会被 <c>SetActive(false)</c>;若某条路径
        /// 让它带着 listener 留成 inactive 形态,再用 active-only 的 Find 去摘就会**整个跳过**。</para>
        /// </summary>
        private static GameObject FindInactiveInclusive(string objectName)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != null && cam.gameObject.name == objectName)
                    return cam.gameObject;
            }
            return null;
        }

        private static void AssertSingleAudioListener()
        {
            // **双判据,缺一不可**(2026-10-20 修):
            //  ① active = 1 —— Unity 是否认这个听音点。0 个 ⇒ 全场景静音,每帧刷
            //     `no audio listeners in the scene`(2026-10-20 实测 10845 条,Play 首帧起)。
            //  ② 挂载总数 = 1 —— 无残留挂载。≥2 ⇒ 每帧警告洪泛 → Console 管道超载 →
            //     Editor 进程 EPIPE 终止(2026-10-10 实测 31868 条)。
            // ⚠️ **旧实现只查 `Resources.FindObjectsOfTypeAll`(含 inactive)的总数 = 1**
            //    ⇒ 把「预摆已禁用 + CameraRig 未挂」判成正确形态,**永远抓不到零 active**。
            //    旧注释「用 active-only 得 0 ⇒ 断言把正确形态判成错误」正是把缺陷写成了规格。
            var active = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var all = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (active.Length != 1 || all.Length != 1)
            {
                throw new InvalidOperationException(
                    $"AC-ADR020-10 AudioListener 单挂点:active {active.Length} 个 / 挂载总数 {all.Length} 个(应各为 1)。" +
                    "0 个 active = 无听音点 ⇒ 整个场景静音(2026-10-20 实测 10845 条警告);" +
                    "≥2 个 = 每帧警告洪泛 → Console 管道超载 → Editor 进程 EPIPE 终止(2026-10-10 实测 31868 条)。" +
                    "正确形态:运行时 CameraRig 主相机持唯一 listener;预摆 Main Camera 禁用前已摘除其 listener。");
            }
        }

        /// <summary>
        /// 把 <see cref="DisplayedSaveSlots"/> 与本会话的磁盘状态合成 42 能画的槽位视图。
        /// <para>2026-11-03(甲方案第一层):屏上「有档 / 空槽」的差异**只有这一个来源**。
        /// 每次开册、每次写盘落定后各调一次 —— 常量清单不会自己更新。</para>
        /// <para>⚠️ 状态映射的纪律:<see cref="Sim.Codec.SaveSlotState.Occupied"/> ⇒ 有档;
        /// <see cref="Sim.Codec.SaveSlotState.Empty"/> ⇒ 空槽(零字);
        /// <b><see cref="Sim.Codec.SaveSlotState.Unknown"/> 与
        /// <see cref="Sim.Codec.SaveSlotState.Corrupt"/> 一律按「未绑定」画</b> ——
        /// 把「不知道」和「残缺」画成空槽,等于对玩家断言「这里什么都没写过」,而它可能只是读不出来。</para>
        /// </summary>
        private UI.Skeuomorphic.SaveSlotView[] BuildSaveSlotViews()
        {
            SaveService saves0 = _services?.SaveService;

            // 🔁 2026-11-03(`TD-persist-17` · 用户裁定):槽号语义 = 1 退出 · 2 自动 · 3+ 手动。
            //    清单**由台账派生**,不再是固定三行 —— 手动槽只进不退,第二次手动存档落在 4,
            //    固定清单会让它「存得进去、看不见」。
            //    范围 = [1, 2] + [3 .. next-1](无手动档时**只有 [1, 2]**)。
            //    🔁 **2026-11-06(用户裁定)**:「留档提示默认初始在 3 号位置,如果有手动存档记录,
            //    留档提示默认在最新手动存档记录的下一行」⇒ **不再单独画一格空手动槽**:
            //    那一格与 42 的「留档」新行是**同一个目标**(都落到 `NextWritableSeq`),
            //    并排会出现两行「按了就存到同一处」。空手动槽让位给「留档」行之后,
            //    它自然落在「退出 / 自动」之后的第 3 位、有档时落在最新手动档的下一行。
            //    ⚠️ 空槽行**没有消失** —— 退出 / 自动 两格在本局还没写过时仍是空槽,
            //    ADR-041 §D1 的「空槽即写它自己的 `slot_seq`」照旧可达。
            uint next = saves0?.Ledger?.NextWritableSeq ?? SaveService.MinManualSlotSeq;
            uint lastManual = next > SaveService.MinManualSlotSeq
                ? next - 1u
                : SaveService.MinManualSlotSeq - 1u;

            var display = new System.Collections.Generic.List<uint>
            {
                SaveService.ExitSaveSlot.SlotSeq,
                SaveService.AutoSaveSlot.SlotSeq,
            };
            for (uint s = SaveService.MinManualSlotSeq; s <= lastManual; s++) display.Add(s);

            var views = new UI.Skeuomorphic.SaveSlotView[display.Count];
            SaveService saves = saves0;
            for (int i = 0; i < display.Count; i++)
            {
                uint seq = display[i];
                if (saves == null)
                {
                    // 未接存档目录 = 7a 未装配(合法形态)· 42 画「未绑定」
                    views[i] = UI.Skeuomorphic.SaveSlotView.Unbound(seq);
                    continue;
                }

                Sim.Codec.SaveSlotSummary summary = Sim.Codec.SaveSlotProbe.Probe(saves.BasePath, seq);
                switch (summary.State)
                {
                    case Sim.Codec.SaveSlotState.Occupied:
                        // ADR-038 §D3:**用这一份档自己记录的时间参数**渲染(不是当前构建的)
                        // —— 否则改过日长后,旧档行会按新值画出与玩家当时不同的「第几日 / 昼夜」。
                        // v3 及更早的档没有这三值(0)⇒ 回落当前构建的参数并如实画(已知降级)。
                        Sim.TimeParams rowTime = summary.TimeParams.IsSpecified
                            ? new Sim.TimeParams
                            {
                                TICKS_PER_DAY = summary.TimeParams.TicksPerDay,
                                NIGHT_START = summary.TimeParams.NightStart,
                                NIGHT_SPAN = summary.TimeParams.NightSpan,
                            }
                            : _timeParams;

                        views[i] = new UI.Skeuomorphic.SaveSlotView(
                            seq, known: true, hasSave: true, tick: summary.Tick,
                            // 🔁 2026-11-03(甲方案第二层):tick → 世界第几日(1 起)。
                            // 算术与除数**唯一出处** = `Sim.World.TimeBase`(AC-5-05;有门守着);
                            // 42 只拿到算好的日子,不做除法。
                            // ⚠️ ADR-038:日长来自 `5_time` 数据(不再有 const)⇒ 参数未就绪时
                            //    传 0(「未定」,见 SaveSlotView.DayNumber 的定义),**不**拿默认值冒充。
                            dayNumber: rowTime == null
                                ? 0L
                                : Sim.World.TimeBase.DayNumberOf(summary.Tick, rowTime),
                            // 2026-11-03 F-5.1:同一 tick 的昼夜(FMod 环绕安全,
                            // 公式逐字照 time-and-weather.md:189-203)。
                            // ⚠️ 唯一实现 = `Sim.World.TimeBase`(全仓曾有两套,见其文件头)。
                            isNight: rowTime != null
                                && Sim.World.TimeBase.IsNight(summary.Tick, rowTime),
                            // 🔁 2026-11-04(ADR-041 §D5)/ 🔁 2026-11-06(锁搬到写侧):
                            //    锁态归 7a(会话内存),42 只呈现 —— 已锁行**整行灰底**,
                            //    二段态里「启封」可用、「重写」灰置(ADR-042 同批裁定)。
                            isLocked: saves.Ledger != null
                                      && saves.Ledger.IsLocked(new Sim.Codec.SaveSlot(seq)),
                            // 🔁 2026-11-05(`TD-persist-22` 册子半边):只剩备份 ⇒ 行上标「前版」。
                            //    ⚠️ 注意这里的 tick / 日子是**备份那一份**的(探针报的就是它)——
                            //    这正是要让玩家看见的:按下去读到的是旧的那一版。
                            fromBackup: summary.Source == Sim.Codec.SaveLoadSource.Bak,
                            // 🔁 **2026-11-06 ADR-042**:落笔时刻 —— 行末半字号显示。
                            //    ⚠️ 0 = 未知(v5 及更早的档)⇒ 42 不画时间,不拿 tick 顶替。
                            savedAtUtcTicks: summary.SavedAtUtcTicks);
                        break;
                    case Sim.Codec.SaveSlotState.Empty:
                        views[i] = new UI.Skeuomorphic.SaveSlotView(seq, known: true,
                                                                   hasSave: false, tick: 0L);
                        break;
                    case Sim.Codec.SaveSlotState.Corrupt:
                        // 🔁 2026-11-05(`TD-persist-22` 册子半边):**探到了、但这一页坏了**
                        //    ⇒ 与「未绑定」分开(后者是「我不知道」)。已损行按 `hasSave:false`
                        //    走空槽语义 ⇒ 按下去 = 落笔(那一页本就读不出来,复用它写入是正常出路)。
                        views[i] = new UI.Skeuomorphic.SaveSlotView(seq, known: true,
                                                                   hasSave: false, tick: 0L,
                                                                   damaged: true);
                        break;
                    default:
                        // Unknown(IO 错 / 路径失效)⇒ 未绑定(零字可聚焦),不冒充空槽,也不冒充已损
                        views[i] = UI.Skeuomorphic.SaveSlotView.Unbound(seq);
                        break;
                }
            }
            return views;
        }

        /// <summary>
        /// 生产读档触发点(TD-persist-04 义务② · Load 接线轮 2026-10-25 · 裁定②
        /// 「公开方法·通电待信」)—— 转发给 <see cref="LoadSequencer.Load"/>(调用序、
        /// 原子序与派生态未重建清单全在 <see cref="LoadSequencer"/> 类头注)。
        /// <para><b>通电待信</b>:本方法今日零调用方(UI 侧 SaveSlotItem 是纯视觉元件,
        /// 存档位界面尚未接线)—— 存档位 UI 接线轮落地即激活,零代码改动。</para>
        /// </summary>
        /// <param name="slot">读档槽位。</param>
        /// <exception cref="InvalidOperationException">装配未含读档序列器
        /// (saveBasePath 为 null 的部分装配 = 编程错误;显式请求被静默吞 = 更糟,故 fail-loud)。</exception>
        public void RequestLoad(Sim.Codec.SaveSlot slot)
        {
            var sequencer = _services?.LoadSequencer;
            if (sequencer == null)
                throw new InvalidOperationException(
                    "装配未含读档序列器(saveBasePath 为 null ⇒ 三件存档侧组件同 null);" +
                    "生产装配必须走完整 BootRoot 启动序,RequestLoad 不接受部分装配");
            sequencer.Load(slot);
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
        /// <summary>
        /// 读档失败的记录面(2026-10-31 用户裁定「甲」):**空槽 ≠ 损坏**,两级日志分开。
        /// <para>病因(走查实测):玩家点一个从没存过的空槽,控制台打红字「存档损坏且无法回退」
        /// ——拒绝载入是对的,话是错的,而且会让玩家以为自己丢了档。</para>
        /// <para>⚠️ 判据来源 = 7a 的**类型**(`SaveNotFoundException` vs `SaveCorruptException`),
        /// 本层不猜、不解字符串(ADR-013 §9 C3 同源纪律:呈现层不猜)。</para>
        /// </summary>
        private static void LogLoadFailure(uint slot, System.Exception ex)
        {
            if (ex is DaYiJingCheng.Sim.Codec.SaveNotFoundException)
            {
                UnityEngine.Debug.Log(
                    "[7b][载入] 槽位 " + slot + " 是空的(从未存过)—— 正常态,不是损坏:" + ex.Message);
                return;
            }
            UnityEngine.Debug.LogError(
                "[7b][载入] 槽位 " + slot + " 载入失败(界面已受理、判定归 7a):" + ex);
        }

        /// <summary>
        /// 落笔失败的记录面(ADR-041 §D8)。与 <see cref="LogLoadFailure"/> **刻意分开** ——
        /// 共用会让「覆写被拒」在日志里显示成「载入失败」。
        /// <para>受检形态:槽号 0 · 不在叙事上下文 · 组版返回 null · 写队列未注入。
        /// 报文**点名门名**(异常类型 + 首句),不只打整段异常。</para>
        /// </summary>
        /// <summary>玩家实体的 id(与花名册同一 id;`IIdAuthority` 机制 A)。</summary>
        private int _playerActorId = -1;

        /// <summary>
        /// 读档后把玩家**送回档内格**(🔁 2026-11-05 · 用户裁定「传送回档内格」)。
        /// <para>病因:读档只重建流 / 世界 / 库存(`CompositionRoot.afterRestore`),**从不碰玩家**
        /// ⇒ 世界回到档内状态,人还站在读档前的位置 —— 画面上就是「读完了但我怎么还在这」,
        /// 与用户报的「没有重新进入游戏」同一族观感。</para>
        /// <para>格的真源 = **世界流里最后一条属于本玩家的 `ActorCellEntered`**
        /// (单人只有一个玩家 id,加这一条是为与后续联机同形)。</para>
        /// <para>⚠️ 走 `ITeleportCommandSink`(ADR-025 §③ 的整数命令半),**不**直接写
        /// `transform.position`:后者是表现态,写它会让「读档落点」成为第四种重建源。</para>
        /// <para>⚠️ 找不到(档里从没跨过格)或未接线 ⇒ **什么都不做**(留空,不猜出生点)。</para>
        /// </summary>
        private void RestorePlayerCellFromStream()
        {
            if (_services?.Stream == null || _services.Decoder == null) return;
            if (_services.TeleportSink == null || _playerActorId < 0) return;

            System.Collections.Generic.IReadOnlyList<SimEvent> events = _services.Stream.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Kind != EventKind.ActorCellEntered) continue;

                DaYiJingCheng.Sim.Contracts.ActorCellEnteredPayload p =
                    _services.Decoder.Decode<DaYiJingCheng.Sim.Contracts.ActorCellEnteredPayload>(
                        events[i].Kind, events[i].Payload);
                if (p.ActorId != _playerActorId) continue;

                _services.TeleportSink.RequestTeleport(_playerActorId, p.Cell);
                Debug.Log("[BootRoot][读档] 玩家回到档内格 " + p.Cell + "(tick " + p.Tick + ")");
                return;
            }
        }

        private static void LogSaveFailure(uint slot, System.Exception ex)
        {
            string gate = ex is System.IO.InvalidDataException
                ? (ex.Message.StartsWith("槽号 0", System.StringComparison.Ordinal) ? "槽号非法" : "数据非法")
                : ex is System.InvalidOperationException
                    ? "叙事上下文 / 装配缺失"
                    : ex.GetType().Name;
            UnityEngine.Debug.LogError(
                "[7b][落笔] 槽位 " + slot + " 落笔被拒(门:" + gate + "):" + ex.Message);
        }

        /// <summary>
        /// 本帧移动轴 = 设备读面 → **模态冻结面**(全仓唯一折零点)。
        /// <para>⚠️ 单抽出这个方法是为了让「接线在不在」可被机械判据盯住:
        /// `Update` 里 `MovementInputReader.ReadMoveAxis()` 只许出现**一次**,且必须
        /// 作为本方法的实参(`modal_input_freeze_test` 的接线判据扫源码守此点)。
        /// 内联写回 `PumpFrame(...)` 的实参 = 那条判据转红。</para>
        /// <para>⚠️ 这证明的是**源码里接线在且唯一**,不证明「Unity 每帧调 <c>Update</c>」——
        /// 后者是引擎契约,不是本仓代码。</para>
        /// </summary>
        /// <param name="raw">设备读面给出的原始轴。</param>
        /// <param name="freeze">模态冻结面;为 <c>null</c> ⇒ 原样透传(诚实降级)。</param>
        public static Vector2 ResolveMoveAxis(Vector2 raw, ModalInputFreeze freeze)
            => freeze != null ? freeze.FilterMovement(raw) : raw;

        /// <summary>走查仪表:`-tutorialnarrcheck` 逐条播放 48 的全部口述(每条一次,条间留间隔便于录音切分)。</summary>
        /// <remarks>⚠️ **不替教学链发火** —— 发火仍只由事件流驱动。本方法只走同一条
        /// `TutorialMediaDriver → 44 cue + 42 字幕` 路径,用来回答「其余几条 cue 在真包里
        /// 解析得出、播得响吗」。默认关闭,生产零开销。</remarks>
        private System.Collections.IEnumerator PlayAllNarrationsForDiagnostics()
        {
            yield return new WaitForSeconds(3f);
            if (_services?.TutorialSeams == null) yield break;

            IReadOnlyList<TutorialNarrationRow> rows = _services.TutorialSeams.ContentForDiagnostics.Narration;
            for (int i = 0; i < rows.Count; i++)
            {
                TutorialNarrationRow row = rows[i];
                var intents = new[] { TutorialMediaIntent.Narration("diag", row.CueId, row.Text) };
                UnityEngine.Debug.Log($"[tutorialdiag] ▶口述复查 {i + 1}/{rows.Count} cue={row.CueId}");
                _services.TutorialSeams.MediaForDiagnostics.Play(
                    intents, _services.TutorialSeams.SourceCellForDiagnostics());
                yield return new WaitForSeconds(WalkthroughDiagnostics.NarrationCheckGapSeconds);
            }
            UnityEngine.Debug.Log("[tutorialdiag] ▶口述复查完毕");
        }

        private void Update()
        {
            // 🔁 2026-11-05(用户实测:「进入程序第一层的设置,点设置然后 ESC 后无法回到上一层」):
            //    模态开合**必须跑在 `_booted` 早退之前**。病因:菜单期 `_booted == false`
            //    (见 `:517`「菜单期零 tick / 零输入泵」)⇒ 早退把 `ModalInputDriver` 一起挡在门外
            //    ⇒ **第一层打开的设置壳没人接 ESC**,壳关不掉。
            //    ⚠️ 它本来就该在这儿:该驱动是**纯呈现层开关**(不读 tick、不写 sim),
            //    与「世界是否在跑」无关 —— 与 `OnFrameEdge` 同一条判据(2026-11-05 前者已搬)。
            //    ⚠️ `_modalInput` 在启动序 `:1143` 才构造;此前为 null ⇒ `?.` 兜住。
            _modalInput?.OnFrame();

            // 🔁 2026-11-10(T18 甲):手册的开合**同在 `_booted` 早退之前** —— 与上一行同因:
            //    它是纯呈现层开关(不读 tick、不写 sim),而「本局有没有这本手册」由
            //    `TryPresentManual()` 自己判(菜单期返回 false)。
            _manualToggle?.OnFrame();

            if (!_booted || _tickDriver == null || _player == null) return;

            // 🔁 2026-11-05(smoke-2026-11-05.md 的 B 条):存档槽位重探走**每帧**,不走 tick 边沿。
            //    病因:存档册是挡路模态 ⇒ 世界冻结 ⇒ `CurrentTick > tickBefore` 恒假 ⇒
            //    挂在 `OnTickEdge` 里的重探永不执行 ⇒ 写成功了而册子上那行还是空的。
            //    ⚠️ 位置:必须在 `_modalInput.OnFrame()` **之后**(开屏/换屏当帧就能重探一次)、
            //    且在 tick 泵**之前**(与世界是否在跑无关 —— 这正是本条的全部意义)。
            _uiDriver?.OnFrameEdge();

            // 走查仪表:焦点读数走**每帧**(周期读数挂在 tick 边沿上,而模态摊开时 tick 停
            // ⇒ 冻结期间一行都没有,恰好是「按了没反应」最需要看见的时候)。
            if (WalkthroughDiagnostics.Enabled && _uiDriver != null)
                WalkthroughDiagnostics.ReportFocusIfChanged(_uiDriver.GetUiRoot());

            // 走查仪表:手柄读数走**每帧**(同焦点读数理由 —— 模态摊开时 tick 停,
            // 而「按住 start 开/合册」恰恰发生在 tick 停的那段时间里)。
            if (WalkthroughDiagnostics.Enabled)
                WalkthroughDiagnostics.ReportGamepadIfChanged();

            // 走查仪表:模态 / 相机档位(**每帧、变化才打**)—— 同焦点读数理由:模态摊开时 tick 停。
            if (WalkthroughDiagnostics.Enabled)
                WalkthroughDiagnostics.ReportModalTierIfChanged(
                    _uiDriver != null ? _uiDriver.CurrentModal.ToString() : "?",
                    _cameraRig != null ? _cameraRig.Mode.ToString() : "?",
                    _tickDriver != null ? _tickDriver.CurrentTick : 0L);

            // 走查仪表:病名册三环读数(**每帧**,与焦点读数同理由 —— 脉案摊开时 tick 停)。
            // 只在数值变化时打一行,故常态零输出;查体 / 开册那一刻各出一行。
            if (WalkthroughDiagnostics.Enabled && _services != null
                && _services.CasebookSource != null)
            {
                _services.CasebookSource.GetLexiconRingCounts(
                    out int ringViewer, out int ringSeen, out int ringPointable,
                    out int ringRows, out int ringNovelty);
                WalkthroughDiagnostics.ReportLexiconRingsIfChanged(
                    ringViewer, ringSeen, ringPointable, ringRows, ringNovelty);
            }

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
            // 🔁 2026-10-31:移动轴过模态冻结面 —— 此前暂停壳摊开时玩家照样走得动
            //    (规则七只覆盖了 E/F 两条动作边,移动轴一直漏着)。
            //    ⚠️ 只折零**轴**,不跳过 `PumpFrame`:tick 泵仍须每帧推进(世界不冻)。
            Vector2 moveAxis = ResolveMoveAxis(MovementInputReader.ReadMoveAxis(), _inputFreeze);

            // 🔁 2026-11-01 用户裁定(ADR-013 §十-C **Amendment D**):挡路模态摊开 ⇒ **真停世界**。
            //    ↑ 上一条(2026-10-31)写「世界不冻」,那是**当时的裁定**;本日改判为冻结 sim tick,
            //      原文保留(承「原文不删、就地加注」)。
            //    口径(用户两问两答):**只停 sim tick**(不碰 `Time.timeScale` / 不碰表现层),
            //      **触发面 = 所有挡路模态**(与规则七同一个 `BlocksPlay` 读面 —— 不造第二个判据)。
            //    ⇒ 停的是 `PumpFrame`(它推进 `ITickProvider` 并步体征);下游一切
            //      `CurrentTick > tickBefore` 的分支随之自然跳过(病程 / 病人 / AI / 52 / 存档定时器)。
            //    ⚠️ **联机不得照此执行**:45 的暂停语义已裁「本地暂停 UI + 主机 tick 不停」
            //      (`accessibility-requirements.md:128`)。P0 无联机,故当前无冲突;
            //      P1b 接 45 时**必须**在此加会话判定,不得把本冻结带进网络会话。
            bool worldFrozen = _inputFreeze != null && _inputFreeze.IsBlocked;

            // 🔁 2026-11-06(用户裁定):记下**解冻的那一帧** —— 挡路模态关掉时世界恢复,
            //    但冻结期间进流的事实(如玩家立案产生的 `CaseOpened`)**没有 tick 边沿可走**,
            //    教学链要等下一个 tick 才看得到。真包实测:关掉脉案后师父的口述要延迟数千毫秒才出声,
            //    而教学想教的是「边看病人边听师父说」—— 那一步在体验上就错位了。
            //    ⚠️ 只对**教学链**补跑:其他 tick 边沿驱动(病人出现 / 急救 / 病程)的补跑
            //       未经裁定,不在本条面内 —— 别顺手推广。
            bool justThawed = _worldFrozenLastFrame && !worldFrozen;
            _worldFrozenLastFrame = worldFrozen;

            if (!worldFrozen)
            {
                MovementFeed.PumpFrame(_player, _tickDriver,
                                       moveAxis,
                                       delta,
                                       StepVitals);
            }

            // chunk 驻留驱动(ADR-023 ⑥ · 系统 6):读**最后一条已进流的 `ActorCellEntered`** 的格,
            // 纯函数重推激活集。⚠️ 刻意读 `LastCommittedCell` 而不是 `transform.position` ——
            // 后者是表现态,用它驱动会让驻留序列不可重放(ADR-016 §三)。
            // ⚠️ 放在 `PumpFrame` 之后:本帧提交发生在帧内 tick 边沿,出来后读到的已是最新格。
            if (_services != null && _services.World != null && _player != null)
                _services.World.OnActorCellEntered(_player.LastCommittedCell);

            // 病人出现驱动:仅在**本帧真推进了 tick** 时驱动(未推进 = 无 tick 边沿)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientAppearedDriver != null)
            {
                _services.PatientAppearedDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 🔁 2026-11-02 · 开册入口轮(结清 `TD-casebook-01`):**此处不再自动开案**。
            //   原为 `CaseOpenedDriver.OnTickEdge(tick)` —— 每 tick 对在场病人自动立案,
            //   与 `case-system.md:176`「立案是一次玩家动作」/ `:927`「玩家没去 ⇒ 不立案」相反。
            //   现立案唯一触发 = 玩家对病人的 `Interact`(见 4.5 段的 `caseOpen` 缝)。
            //   ⚠️ 删的只是**调用点**:驱动本身仍在(它现在的入口是 `TryOpen`,由就诊分支调)。

            // 急救链接线驱动(sprint-05 T1.3):在同一 tick 边沿序列内,病人出现驱动**之后**调用
            // 次序 = 病人先出现 → (立案归玩家动作,不在此处) → 最后触发急救(EmergencyAttempt + EmergencyTreatmentApplied 入流)
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

            // 52 出诊启动驱动(TD-randomevents-02 接线轮 · 2026-10-25 三裁全取推荐):
            // 同一 tick 边沿序列内,急救通道检查**之后**、定时兜底**之前** ——
            // 出诊启动锚点(HouseCallStarted)须在本边沿先落流,末段锚点触发器的
            // Drain 才能在同一边沿扫到(插在 Drain 之后 = 锚点观察晚一拍,缝)。
            // ⚠️ 生产「通电待信」态:出诊类 event_key 集与医馆地块集均为空(三裁②③)
            //    ⇒ 写入恒零;两处数据回填(37 key 集 / 23-24 地块)后自动激活。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.RandomEventsDirector?.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 30 技法解锁的触发扫描(T9 · 2026-11-09):同一 tick 边沿序列内,排在**本边沿所有
            // 事件生产者之后** —— 它读的是事件流,排前面会晚一拍看到本 tick 的 `EventArrived`
            // (与 48 教学链同一条理由)。
            // ⚠️ 规则表为空 ⇒ 内部直接 return,今日零开销、零写入。
            // ⚠️ 它**会**写事件(`TechniqueUnlocked` → 病史流),但写者是唯一落点且 D6 去重 ⇒
            //    同一份流重复扫描不产生重复行(读档后再扫一遍是安全的,且是设计意图:
            //    玩家 id 读档后重新铸造,历史解锁由重扫补写给他)。
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.TechniqueUnlockDriver != null)
            {
                _services.TechniqueUnlockDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 37 同源检测 + 章节推进(T20 · 2026-11-10):同一 tick 边沿序列内,排在
            // **本边沿所有事件生产者之后** —— 它读的是 `CaseClosed`(玩家按 `C` 产生,
            // 发生在 Update 的输入段),且写 `PatternRecognized` / `ChapterAdvanced`。
            // ⚠️ 没有新结案 ⇒ 内部零扫描(与"零规则零扫描"同款省法);首轮恒扫(读档自愈)。
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatternRecognition != null)
            {
                _services.PatternRecognition.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 48 教学链(S25-4c · 2026-11-06):同一 tick 边沿序列内,排在本边沿**所有事件生产者之后**
            // —— 教学完成度读的是事件流,排在前面会晚一拍看到本 tick 的 `CaseOpened` / `JudgmentRecorded`。
            // ⚠️ 本调用**可能投递音频与字幕**(44 / 42 的入向口),但它不写三流、不判 sim 状态。
            if (_tickDriver.CurrentTick > tickBefore && _services != null && _services.TutorialChain != null)
            {
                TutorialChainTick tut = _services.TutorialChain.OnTickEdge(_tickDriver.CurrentTick);
                // `-tutorialdiag`:发火瞬间单独一行(否则只能从「下一秒的读数」倒推)
                if (WalkthroughDiagnostics.Enabled && tut.AnyFired)
                    WalkthroughDiagnostics.ReportFired(_tickDriver.CurrentTick, tut.FiredStepIds, tut.NarrationsPlayed);
            }

            // 🔁 2026-11-06(用户裁定):**解冻补跑** —— 模态刚关掉的那一帧再推一次教学链边沿。
            //    为什么安全:`TutorialChainDriver.OnTickEdge` 按**事件游标**推进(同一 tick 二次调用
            //    看到的是零新增事件),而发火闸 `_trigger` 自身去重 ⇒ 重复调用幂等。
            //    这两条前提由 `tutorial_chain_driver_test.test_onTickEdge_sameTickTwice_firesOnce` 钉住 ——
            //    若哪天去掉了去重,那条夹具会红。
            //    ⚠️ 放在正常边沿块**之后**:解冻帧若同时推进了 tick,正常边沿已经跑过一次,
            //       这里只会是空转(幂等),不会双发。
            if (justThawed && _services != null && _services.TutorialChain != null)
            {
                if (WalkthroughDiagnostics.Enabled)
                    WalkthroughDiagnostics.ReportThaw(_tickDriver.CurrentTick, _services.TutorialChain.FactCount);
                TutorialChainTick thawed = _services.TutorialChain.OnTickEdge(_tickDriver.CurrentTick);
                if (WalkthroughDiagnostics.Enabled && thawed.AnyFired)
                    WalkthroughDiagnostics.ReportFired(_tickDriver.CurrentTick, thawed.FiredStepIds, thawed.NarrationsPlayed);
            }

            // `-tutorialdiag` 现场读数(每秒一行;不带旗标时零开销,见 WalkthroughDiagnostics 头注)。
            // 放在这里 = 教学链跑完之后 ⇒ 读到的是**本 tick 终态**的教学事实数。
            if (WalkthroughDiagnostics.Enabled && _tickDriver.CurrentTick > tickBefore
                && _tickDriver.CurrentTick % WalkthroughDiagnostics.ReportEveryTicks == 0)
            {
                WalkthroughDiagnostics.Report(
                    _tickDriver.CurrentTick,
                    // 玩家 actor id 取自行走链的归属(它由 Attach(actorId) 传入);
                    // `PlayerController` 的 `_actorId` 是私有的,不为一行调试日志去开口子。
                    _services?.TutorialChain?.PlayerId ?? -1,
                    _player != null ? _player.LastCommittedCell : default,
                    _services != null ? _services.Presence : null,
                    _services?.EmergencyChannel?.State.ToString(),
                    _services?.TutorialChain?.FactCount ?? 0,
                    _services?.TutorialChain?.SeededCompletedSteps ?? 0,
                    // 🔁 2026-11-07:模态与相机档位的只读读数(真包世界空间手柄判据;
                    //    不带 `-tutorialdiag` 时整段不进)。
                    _uiDriver != null ? _uiDriver.CurrentModal.ToString() : "?",
                    _cameraRig != null ? _cameraRig.Mode.ToString() : "?",
                    _pressRouter != null
                        ? (_pressRouter.LastRoutedKind.HasValue
                            ? _pressRouter.LastRoutedKind.Value.ToString() : "无")
                        : "未建");
            }

            // 定时兜底 checkpoint(AC-7a-20 定时半边 · TD-persist-05 动作④ · 2026-10-25 生产接线轮):
            // 同一 tick 边沿序列内,急救通道检查**之后**(兜底判定读的「未决医疗动作」Armed 态
            // 须是本 tick 终态;halt 期无 Advance ⇒ 无边沿 ⇒ 不驱动,承 S6 停机时钟语义)。
            // ⚠️ 按 **tick 数**驱动(`OnTicks(Δtick)`),不按帧 —— 一帧推进 N tick 时
            //    按帧驱动会把 N 个 tick 计成 1,interval(以 tick 计)被帧率稀释。
            //    闭包内异常已在装配根兜住(LogError,不杀帧)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.CheckpointFallback?.OnTicks(_tickDriver.CurrentTick - tickBefore);
            }

            // 事件触发 checkpoint(AC-7a-20 事件触发半边 · TD-persist-04 动作① · 2026-10-25 裁定轮):
            // 集中观察(裁定①)—— 本 tick 落流事件交触发器扫白名单,命中且瞬时源安全 ⇒
            // 调同一带门 Checkpoint 入口;成功 ⇒ 兜底计数清零(裁定④)。**在兜底之后**:
            // 兜底本轮若已触发,本 drain 不会重复触发(游标语义 + 同 tick 一次);halt 期
            // 无 Advance ⇒ 无新事件,不驱动(与兜底同款停机时钟语义)。读档换流
            // (`EventStream.Restore`)后游标由 Drain 的钳制对齐 —— 生产 Load 接线落地时
            // 须另调 `ResetCursor()`(证据件缺口登记)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null)
            {
                _services.AnchorCheckpointTrigger?.Drain(_services.Stream.Events);
            }

            // 持续交互窗口的推进(`TD-foraging-04` · 2026-11-02):
            // 17 采集是**持续交互**(`OQ-17-3`),窗口按 **tick** 推进;而 `OnPress` 只在
            // 按键**沿**上跑 ⇒ 没有这一段,窗口永远停在起表那一 tick(按下去什么都不发生)。
            // ⚠️ 只在窗口真的存在时付代价(`OnTick` 内部先问 `IsHolding`),常态零开销。
            // ⚠️ 与其它 `OnTickEdge` 同位置:本段**不写事件**(结算发生在窗口满时),故不参与入流次序。
            if (_tickDriver.CurrentTick > tickBefore && _pressRouter != null)
            {
                _pressRouter.OnTick();
            }

            // 病人视觉实体(sprint-05 T2.0):急救驱动**之后**调用 —— 视觉跟着在场集走,
            // 与写者次序解耦(视觉是派生态,不参与事件入流次序)
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientVisualSpawner != null)
            {
                _services.PatientVisualSpawner.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 一次性 cue 的归还泵(S15-5 · TD-audio-029):问 `isPlaying`,播完即把源还回池。
            // ⚠️ **挂在 tick 边沿上**(20 Hz),不是每帧 —— 与本文件其余驱动同一节奏,
            //    每帧一次会多出 2 倍的无收益遍历(素材最短也有数百毫秒)。
            //    挂在 `PatientCueDriver` **之前**:本拍新发的一次性 cue 不会在本拍被观测(它刚开播)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null && _services.AudioPool != null)
                OneShotReclaimPump.Pump(_services.AudioPool);

            // 病人 cue 驱动(story-018 · SAC-018-6):在场集 → 13 调度 → 44 发射。
            // 次序 = 视觉实体**之后**、玩家交互输入**之前**:两者都读本 tick 的在场集,
            // 但 cue 还要读体征 —— `StepVitals` 已在 `PumpFrame` 的边沿序列里跑完
            // (故新出现的病人在**本拍**已建档,`hasCourse` 门不会误跳)。
            // ⚠️ `PruneAbsent` 先于 `OnTickEdge`:离场病人的呼吸层句柄须先收,
            //    否则句柄残留在驱动内、`ActiveBreathCount` 永远不归零。
            if (_tickDriver.CurrentTick > tickBefore
                && _services != null && _services.PatientCueDriver != null)
            {
                _services.PatientCueDriver.PruneAbsent(_tickDriver.CurrentTick);
                _services.PatientCueDriver.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 炮制完成调度的发射侧(S24-1 · processing story-005 · AC-18-21):
            // 「本 tick 哪些炉到点」→ `sfx_process_complete`。
            // ⚠️ 判据在 `Sim.FurnaceCompletionSchedule`(纯函数,tick 等号);本块只驱动。
            // ⚠️ 驱动内部在 cue 未解析时 no-op(今日事件表无该行)⇒ 本块每 tick 零成本跳过。
            // ⚠️ 次序 = 病人 cue **之后**:两者互不依赖,摆在同一边沿段内便于「tick 边沿驱动」
            //    的口径集中(禁墙钟 —— GDD Visual/Audio 护栏 1)。
            if (_tickDriver.CurrentTick > tickBefore && _services != null
                && _services.ProcessingCompletion != null)
            {
                _services.ProcessingCompletion.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 起货溢出的**结算**(`TD-processing-08` · R-18-D[甲]② · 2026-11-03):
            // 「本 tick 到点的炉」→ 容量裁决 → 装不下则整批 `DropSpawned`(20 侧唯一写者)。
            // ⚠️ **次序**:必须在上面的 cue 发射**之后**同一 tick —— 两者都读同一份到点集,
            //    但本块会**写流**(DropSpawned),写入后本 tick 的后续读者(42 / 44)才看得到落地;
            //    摆在 cue 之前不会错,摆之后便于「先说一声、再落地」的可读性。
            // ⚠️ 本块**不重写「哪些炉到点」** —— 判据只有 `FurnaceCompletionSchedule` 一处。
            if (_tickDriver.CurrentTick > tickBefore && _services != null
                && _services.CraftCompletionSettler != null)
            {
                _services.CraftCompletionSettler.OnTickEdge(_tickDriver.CurrentTick);
            }

            // 玩家交互输入(sprint-07 S2):E 查体 / F 急救 —— **视觉实体更新之后**(读的是
            // 本 tick 的在场集+视觉位置;换热须已本 tick Step),拟物 UI 刷新之前
            // (查体后本帧即见值区有词)。
            _interactionDriver?.OnFrame();

            // 建造输入(S20-19 · 2026-10-28):B 开关 / Enter 放置 / R 旋转 / Delete 拆除。
            // ⚠️ 紧跟交互驱动:两者都读设备按下沿,互不依赖;建造在**非建造模式**下是纯 no-op。
            _buildDriver?.OnFrame();

            // 听诊精度档位(B 轮 2026-10-20):**按帧**推 ramp(档变才 SetTargets;未接 table ⇒ null)。
            // ⚠️ 不挂 tick 边沿 —— ramp 是逐帧平滑(GDD F-44.1 注 ≥ 50 ms),tick 边沿 20 Hz 会跳。
            _services?.AudioTierDriver?.Tick(UnityEngine.Time.deltaTime);

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
            if (_cameraRig != null)
            {
                // 🔴 **2026-11-02(story-010 · 用户裁定「本卡顺带接 Tick」)**:
                //   此前 `CameraRig.Tick` **零生产调用点** ⇒ `CameraModeMachine.Settle` 从不执行
                //   ⇒ 档位**永远锁死在 `Explore`**:29 的 `Downed`、10 的 `Treatment`、将来的 39
                //   `Casebook` 都只是入队,永不结算(实测见 `story-010` §Context 的勘察)。
                //   ⚠️ **跨 epic 后果**:接上本行即**同时**激活 29 的两段式倒下相机与 39 的档位转场
                //   —— 这是修复,不是新特性;但两处行为变化须在各自卡面/证据件登记,不得当作噪声。
                //   ⚠️ 相位:单一显式入口(AC-2-27③),用**渲染帧 dt**(档位转场是表现态,
                //   不是 tick 域;承 `CameraRig.Tick` 的 `<param>` 注)。
                _cameraRig.Tick(UnityEngine.Time.deltaTime);
            }

            if (_cameraRig != null && _player != null)
            {
                var cam = _cameraRig.Camera;
                if (cam != null)
                {
                    Vector3 target = _player.transform.position;
                    cam.transform.position = _cameraRig.GetCameraPosition(target);
                    // 🔁 **2026-11-01**:视线目标改走 `GetLookTarget`(倒下微倾的承载点)。
                    //   常态下它**恒等于 `target`**(`TiltFraction == 0` ⇒ 零偏移)⇒ 行为不变;
                    //   倒下时目标向下移 ⇒ 视线向下,**相机位置不动**。
                    //    ⚠️ 为什么不能直接用 `LookAt(target)`:微倾若做进 pitch,相机位置会
                    //    沿弧线**抬高**,与下沉反向抵消(图形走查实测,见 `GetLookTarget` 注)。
                    cam.transform.LookAt(_cameraRig.GetLookTarget(target));
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

            // 🔁 2026-10-30(ADR-035 §⑤):53 的禁忌结算紧随体征 Step —— 它只读**已进流**的
            //    `DrugTreatmentApplied`,与体征链无数据依赖;放在同一 tick 边沿是为了让伤害事件
            //    与用药事件落在**同一个 tick**(承 ADR-035 §④「伤害事件的时间 = 用药事件的时间」)。
            _services.ContraindicationSettler?.Step(tick);

            // 🔁 2026-10-30(`TD-sim-02` 结清后):过量结算。
            //   ⚠️ 次序**必须在 `VitalsService.OnTickEdge` 之后** —— 它读的是 9 在本 tick
            //   求出的 `VitalsDto.PeakDrugLoad`,不是在流里现算(53 不自算毒性,`AC-53-01`)。
            _services.OverdoseSettler?.Step(tick);

            // 🔁 2026-10-30(ADR-036 D2/S4):53 判定链驱动(每个结案病例的 `Aggregate → Judge → Emit`)。
            //   ⚠️ **次序是硬约束(R4)**:必须排在 `VitalsService.OnTickEdge` **之后** ——
            //   它经投影读 9 的 F4 态;排前面读到的是上一 tick 的态,**静默偏差一个 tick 且无报错**。
            //   排在两个 Settler 之后还有一条理由:它们在本 tick 写入的伤害事件先入流,
            //   判定读到的就是本 tick 的完整流(与「同 tick 同源」同口径)。
            //   `null` = 无数据源 ⇒ 未装配(惰性:不发,不是不判)—— 启动期已在 `Start` 打日志。
            _services.ConsequenceDriver?.OnTickEdge(tick);
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

            // 🔁 2026-11-10 T22 批一:结构体块同理 —— 根节点同样自持(`StructureVisuals`),
            //    不卸载就会在下次进入世界时**叠出第二套**(画面上只是变亮一点,是静默的)。
            _services?.StructureVisualSpawner?.DestroyAll();

            // 急救直读通道卸挂(story-009):onAfterUpdate 是全局静态回调,必须在
            // BootRoot 销毁时显式 Detach,否则域内悬挂委托引用已弃通道(下次 Play 泄漏)。
            _services?.EmergencyChannel?.Detach();

            // 设备态卸挂(TD-epic-01 第八片):onDeviceChange 同属全局静态回调,须显式摘。
            _deviceState?.Detach();

            // 退出钩子卸挂(AC-7a-17):`Application.wantsToQuit` 是**静态**事件,
            // 不摘会在下次 Play 里重复触发已弃的 SaveService(域内悬挂委托)。
            // ⚠️ 用**缓存字段**注销,不能用 lambda 重新 `-=`(每次求值都是新对象)。
            if (_wantsToQuitHandler != null)
            {
                Application.wantsToQuit -= _wantsToQuitHandler;
                _wantsToQuitHandler = null;
            }

            // 病人 cue 驱动收尾(story-018 · ADR-018 §一「句柄由唯一持有者负责收尾」)——
            // 本驱动即呼吸层句柄的持有者;不显式 EndLoop ⇒ 句柄跨 Play 轮残留。
            _services?.PatientCueDriver?.StopAll();

            // 44 装配缝释放(story-018):池根节点 + clip / mixer 的 Addressables 句柄。
            // ⚠️ 池根是**非 Boot 场景**内容(ADR-028 ② 零场景预摆:池根由 `new GameObject` 建,
            //    不挂 BootRoot 层级)⇒ 不靠层级继承销毁,必须显式 Dispose。
            // ⚠️ **不调 `Spatialization?.Dispose()`** —— 导演与渲染目标**共享**同一个池
            //    (SAC-018-4 单池不变量),池的所有者是本装配袋;导演只借用。
            //    若两处都 Dispose,第二个是空操作(池幂等)但语义上会造成「谁拥有池」的二义。
            _services?.AudioPool?.Dispose();
            if (_audioSeams?.Clips is AudioClipLibrary clipLibrary) clipLibrary.Dispose();
            _audioSeams = null;

            // 🔁 2026-10-29(`TD-audio-031` ①):**两条静态装配缝置 null**。
            // 病因:`AudioSettingsRegistry.Provider`(由 `ApplyAudioSettingsProvider` 赋值)与
            //   `PlayerBusVolumeInitializer.MixerResolver`(由 `ApplyPlayerBusDefaultsOrFail` 赋值)
            //   都是 `public static`,而本方法此前**只 dispose 五件、不置 null 两缝** ⇒
            //   它们跨 Play 轮悬垂指向已 `Addressables.Release` 的 provider / mixer。
            // ⚠️ **今天为什么没炸**:`EditorSettings.asset` 的 `m_EnterPlayModeOptionsEnabled: 1`
            //   + `m_EnterPlayModeOptions: 0` ⇒ **完整域重载**,静态每次 Play 复位。
            //   该设置一旦关掉(域重载关闭是常见的加速手段),即刻咬人 —— 故本债判「条件性」。
            // ⚠️ **顺序**:必须在 `_audioSeams = null` **之后**(上面那条 dispose 依赖 `_audioSeams`);
            //   两条缝与 `_audioSeams` 无引用关系,置 null 只是断静态根引用。
            AudioSettingsRegistry.Provider = null;
            PlayerBusVolumeInitializer.MixerResolver = null;

            _booted = false;
            _player = null;
            // CameraRig 的销毁**不在这里**:它已 SetParent(transform)(启动序第 5 步),
            // 随本对象的层级销毁一并走 —— 它身上挂着**唯一** AudioListener(AC-ADR020-10),
            // 显式再 Destroy 会与层级销毁重复、且若顺序在前会让本帧出现「零听音点」窗口。
            _cameraRig = null;
            _services = null;
            _tickDriver = null;
            _uiDriver = null;
            // EventSystem 与 SkeuoUI 都随 BootRoot 一同销毁(reparent 到 transform ⇒ 无需
            // 显式 Destroy);仅清引用。承 FindOrCreateSkeuoUi 同纪律(2026-11-02 `TD-skeuo-02`
            // 把 SkeuoUI 也纳入该纪律 —— 此前它不归位,跨 PlayMode 用例存活)。
            _eventSystem = null;
        }

        // ══════════════════════════════════════════════════════════════════
        // 容器屏的数据面(20 · 2026-11-02)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 把某容器实例投影成 42 的只读 DTO。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>器物来源</b> = `InventoryProjection.InstancesOf(owner)` —— 该方法的契约是
        /// **按 `instance_id` 升序**(`InventoryFold.cs:341-343`),正合 §5.2 的落位序
        /// ⇒ **42 侧不再排序**(两处排序 = 第二真源)。</para>
        /// <para>🔁 <b>载重对与满档谓词(2026-11-02 接真值)</b> —— 首版传 `(0,0)`,理由写的是
        /// 「`InventoryLoadService` 没有暴露载重读面」。**该理由是错的**:`CarryLoadSolver`
        /// 与其 `Sum` 早就在生产里跑(`HarvestWorldQuery` / `InventoryTransaction`),缺的是
        /// ①**按任意 owner** 的读面(此前只有 `InventoryLoadResult.UnclaimedLoad`,`NoOwner` 一侧)
        /// 与 ②容量真值进服务袋。两件本轮均已补(`LoadOf` / `MinItemWeight` / `Services.CarryCap`)。</para>
        /// <para>⚠️ <b>谓词只在 20 侧求值</b>(BL-27):本方法调 `CarryLoadSolver.IsFullForAnyAddition`
        /// **本体**,把**结果**放进 DTO;42 不做第二次求值(F8 的典型形态 —— 两份式子今天同值,
        /// 任一侧改口径即静默分叉,而夹具因行为一致抓不住)。</para>
        /// <para>⚠️ <b>容量真值缺席时整组不计算</b>(`CarryCap == null` ⇒ 夹具假 provider /
        /// 未装配)⇒ 传 `(0, 0, false)`,四档恒 `Empty`。`AC-20-13` 明令禁 null 兜底与默认容量
        /// ⇒ **不拿一个默认 cap 冒充**。这是诚实降级(与空箱视觉一致),不是假四档。</para>
        /// <para>⚠️ <b>owner 的取法</b>:容器 = 物品实例,其内容物的 `Owner` 是**玩家**而非容器 id。
        /// 今日投影里 `Owner` 是玩家号空间 ⇒ 传 `actorId`。⚠️ **「箱内」与「身上」今日不可分**
        /// (投影没有「在哪个容器里」这一维)—— 登记为残余,归 20 的容器语义轮(`OQ-20-11`)。</para>
        /// </remarks>
        private DaYiJingCheng.Sim.Contracts.InventoryContainerDto BuildContainerDto(
            long containerInstanceId, int actorId)
        {
            var inventoryLoad = _services != null ? _services.InventoryLoad : null;
            if (inventoryLoad == null)
                return new DaYiJingCheng.Sim.Contracts.InventoryContainerDto(
                    containerInstanceId,
                    System.Array.Empty<DaYiJingCheng.Sim.Contracts.ContainerItemDto>(), 0L, 0, false);

            var projection = inventoryLoad.Rebuild().Projection;
            var ids = projection.InstancesOf(actorId);
            var items = new DaYiJingCheng.Sim.Contracts.ContainerItemDto[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                projection.TryGet(ids[i], out DaYiJingCheng.Sim.InventoryRow row);
                items[i] = new DaYiJingCheng.Sim.Contracts.ContainerItemDto(
                    row.InstanceId, row.ItemOrdinal, row.Quality, row.Qty);
            }

            // ── 载重对 + 满档谓词(AC-20-18 / AC-20-24 / BL-27)──────────────────────
            // 谓词**在 20 侧求值** —— 调 `CarryLoadSolver` **本体**,不复制判据。
            // 42 只拿结果(`IsFull`)+ 读数对,余下三档由它分(BL-27:「20 出谓词、42 出分档」)。
            //
            // ⚠️ **容量真值缺席时整组不计算**(`CarryCap == null` ⇒ 夹具假 provider):
            //    `AC-20-13` 明令禁 null 兜底 / 默认容量 ⇒ 传 `(0, 0, false)`,四档恒 `Empty`。
            //    这是**诚实降级**(与空箱视觉一致),不是「假装装得下」。
            int cap = _services.CarryCap ?? 0;
            long load = 0L;
            bool isFull = false;
            if (cap > 0)
            {
                load = inventoryLoad.LoadOf(projection, actorId);
                isFull = DaYiJingCheng.Sim.CarryLoadSolver.IsFullForAnyAddition(
                    load, new DaYiJingCheng.Sim.CarryCapacity(cap), inventoryLoad.MinItemWeight());
            }

            return new DaYiJingCheng.Sim.Contracts.InventoryContainerDto(
                containerInstanceId, items, load, cap, isFull);
        }

        /// <summary>
        /// 构造「炮制三分量」的取值面(`TD-processing-09` 结案轮 · 2026-11-11)。
        /// <para><b>三分量各自来自各自的系统</b>:`EquipMod` / `EnvMod_clinic` 来自 24 的
        /// 求值 Memoize 交付面;`EnvMod_climate` 来自 5 的 `EnvMod.ComputeEnvModDelta`,
        /// 其生态区基线由 6 的 `world_ecozones` 表查得。</para>
        /// <para>⚠️ **返回 null ⇔ 装配层没接世界**(24 的 memo 与 6 的基线都要 `structures`):
        /// sink 据此退回增量中性 0 并告警 —— 降级可观测,不是静默。</para>
        /// <para>⚠️ <b>24 的缓存失效在取值前补齐</b>(EC-24-05):memo 的失效键是 `Structure*`
        /// 三支,而本工程的驱动不共享事件泵(各自扫流)⇒ 取值前把「键新于当前缓存」的
        /// 结构事件喂一遍。不补这一步,memo 会在**第一次**求值后永远命中 ——
        /// 玩家造完家具后炮制仍按旧布局算(静默陈旧值)。
        /// 代价 = 每次点火扫一遍事件表(点火是玩家动作,不在 tick 热路径上)。</para>
        /// </summary>
        private Func<DaYiJingCheng.Sim.Contracts.WorldPos, ProcessingEnvMods> BuildProcessingEnvMods()
        {
            DaYiJingCheng.Gameplay.Presentation.ClinicEnvironmentMemo memo = _services?.ClinicEnvironment;
            DaYiJingCheng.Gameplay.Presentation.EcozoneClimateBaseline climate = _services?.EcozoneClimate;
            if (memo == null || climate == null) return null;

            return cell =>
            {
                // ① 把缓存之后到达的 `Structure*` 事件补齐(单调失效;`Apply` 自带 Kind 闭集过滤)。
                System.Collections.Generic.IReadOnlyList<DaYiJingCheng.Sim.Contracts.SimEvent> events =
                    _services.Stream.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    DaYiJingCheng.Sim.Contracts.SimEvent e = events[i];
                    if (DaYiJingCheng.Sim.EventOrder.KeyOf(e).CompareTo(memo.CachedKey) > 0)
                        memo.Apply(e);
                }

                // ② 24 的两个分量(同一次求值一起产出,不可能漂移)。
                DaYiJingCheng.Sim.Contracts.ClinicEnvDto dto = memo.GetDto();

                // ③ 5 的环境分量 —— cell = **炮制发生格**(F-5.4 调用点表),不是玩家格。
                var input = new DaYiJingCheng.Sim.EnvModInput(climate.Of(cell), 0, 0, false, 0);

                return new ProcessingEnvMods(
                    dto.EquipMod,
                    DaYiJingCheng.Sim.EnvMod.ComputeEnvModDelta(input),
                    dto.EnvMod);
            };
        }
    }

    /// <summary>容器屏选中态 → 装配层 store 的转接(42 不持状态,只报意图)。</summary>
    internal sealed class ContainerSelectionBridge
        : DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens.IContainerSelectionSink
    {
        private readonly ContainerSelectionStore _store;

        public ContainerSelectionBridge(ContainerSelectionStore store) => _store = store;

        public void OnSelectionChanged(System.Collections.Generic.IReadOnlyList<long> selectedInstanceIds)
            => _store.Replace(selectedInstanceIds);
    }
}
