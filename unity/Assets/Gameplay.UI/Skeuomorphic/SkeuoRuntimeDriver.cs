// sprint-05 T2.1 / T2.2 · 42 运行时装配器(2026-10-10 两处 TODO 落地)
//
// 权威来源:
//   ADR-013 §四 Key Interfaces + §9 C3 —— 42 只渲染 / 只驱动,不持有游戏状态
//   ADR-023 §① 三场景制 —— UI 载体由运行期生成(**不在 Boot.unity 预摆**:
//     预摆 = 第二真源;ADR-023 ②「World.unity 零 gameplay GameObject」同纪律)
//   ADR-025 §① 具名六装配清单 —— `Gameplay.UI` 引用集 = {Sim.Contracts, Gameplay.Presentation}
//     ⇒ 本装配**不得**引用 `Gameplay.Boot`(装配方向不可倒),数据经场景注入进来
//   story-011 双真源声明 —— Screen 由 `BuildUI()` 建树,name 结构序 ≡ UXML spec 面
//
// 设计说明(T2.2 Screen 实例化):
//   · 七个 Screen 构造签名全同形 `(VisualElement root, SkeuoElementLibrary library)`
//     ⇒ 单一工厂方法 + 名字查找表;**零反射**(反射 = 改构造签名静默失配)。
//   · Screen 名不在表内 = fail-loud(具名异常),不 fallback(静默换屏 = 玩家看到错的东西)。
//
// 设计说明(T2.1 DTO 绑定):
//   · `ICasebookDtoSource`(本装配的新契约)由**场景注入**实现方(生产 = 装配层提供者;
//     42 不反向引用装配层)。零实现 = LogError 并**不绑**(禁造假数据源 ——
//     「假数据」在本项目指编造读数,不含「因缺源而留空」)。
//   · 绑定后每 tick 边沿刷新值区(读的是本 tick 的真实体征;**不缓存**)。
//
// ⚠️ PanelSettings 不在预摆面:本装配允许 `_panelAsset == null`(此时 UI 不渲染,
//    仅逻辑树在 —— 正是单元测试要的形态);真渲染由编辑器里挂好 PanelSettings 的
//    UIDocument 提供。两者都不改「谁持状态」的答案:都不是本类。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// 42 运行时装配器 —— 挂 Boot 场景(运行期生成)或测试宿主。
    /// <para>职责:建 UIDocument 载体 → 按名实例化 Screen → Bind 只读 DTO 源 →
    /// tick 边沿刷新值区。**四步全在运行期,零场景预摆**(ADR-023 ①)。</para>
    /// <para>不持有游戏状态(§9 C3):本类的字段只有**装配产物**与**脏标**,
    /// 零体征 / 零病程 / 零词条副本。</para>
    /// </summary>
    public sealed class SkeuoRuntimeDriver : MonoBehaviour
    {
        /// <summary>可由编辑器赋值的 UIDocument 载体(为空则本类自建)。</summary>
        [SerializeField] private UIDocument _uiDocument;

        /// <summary>UI 面板资源(为空 = 不渲染,仅逻辑树;见文件头注)。
        /// <para>⚠️ 命名避「Settings」:AC-42-D3 反射面把 `*settings*` 当设置值字段扫
        /// (42 零设置值)—— 本字段是**渲染资源**,不是设置值,故用 `_panelAsset`。</para>
        /// </summary>
        [SerializeField] private PanelSettings _panelAsset;

        /// <summary>
        /// 启动时**常驻**的屏名(闭集见 <see cref="CreateScreen"/>);**空 = 无常驻屏**(2026-11-02 起的默认)。
        /// <para>⚠️ 历史:默认值曾是 `"Casebook"` —— 那是 sprint-05 的**占位**,不是设计。
        /// `design/ux/casebook-39.md` §入口逐字写:「世界空间对病人裸 `Interact` = 就诊/立案
        /// → **立案即开册**」⇒ 脉案是**按需打开**的模态,不该在启动时就摊在屏上。
        /// 占位形态的后果:开局「脉案屏摊着 + 相机在 `Explore`」—— 一个谁也说不清对错的组合
        /// (`S23-11` 收尾时记为 B-1「未裁」,2026-11-02 按本节判定退役)。</para>
        /// </summary>
        [SerializeField] private string _screenName = "";

        /// <summary>拟物样式表(九份 Skeuo USS 的**挂载清单**)。
        /// <para>为何存在:Boot.unity 预摆只给 PanelSettings ⇒ panel 非 null,但
        /// **USS 类全库零引用**(2026-10-10 实测:PanelSettings.themeUss 只含 Unity 默认主题,
        /// 无一份 Skeuo USS 被任何 PanelSettings / UXML / tss 引用)⇒ 树会以**无样式文本**
        /// 形态渲染 —— 纸纹 / 线格 / 值区形态对全部不可见,而 story-021/022/023/024 的
        /// 判据全在 USS 里。故渲染资源的两半(PanelSettings + 样式表)都归编辑器赋值。</para>
        /// <para>⚠️ **加载序即 story-021 边界⑥ 的未裁项**:`.focus-visible` / `.ink-wet` /
        /// `.ruled` 都写同名属性,单元素双类时胜负由加载序决定。此处**不擅自裁** ——
        /// 按元素的引用次序加,与编辑期拖入次序一致;裁决项留待焦点接线轮(今日零焦点施加者,
        /// 冲突不可观测 = 非缺陷)。</para></summary>
        [SerializeField] private List<StyleSheet> _styleSheets = new List<StyleSheet>();

        private ICasebookDtoSource _source;
        private ICasebookCommandSink _commandSink;
        // 7b 存档位:载入意图出口 + 槽位清单(均由装配层注入 —— 42 不发明槽位数)
        private ISaveSlotCommandSink _saveSlotSink;
        private IReadOnlyList<uint> _saveSlots;
        private IReadOnlyList<SaveSlotView> _saveSlotViews;
        private bool _saveSlotsDirty;
        // 暂停壳:继续 / 进存档位(2026-10-31 Amendment C)——
        // ⚠️ 两个动作都由装配层实现:42 **不**自己决定「继续」要把世界恢复成什么,
        //    也不自己调 `OpenModal`(那是换屏,属装配层的时序决定)。
        private IPauseCommandSink _pauseSink;
        // 主菜单(2026-11-01 · `main-menu-42.md`):**场景级屏,不是 ModalId 成员**。
        // ⚠️ 意图出口同样由装配层实现(42 不判「有没有档」「继续到哪一份」)。
        private IMainMenuCommandSink _mainMenuSink;

        private VisualElement _panel;
        private IPresentationRoot _screen;
        // 🔁 2026-10-31 模态开启器:当前模态 + 它独占的容器。
        // ⚠️ 换屏**不**清 `_panel`（面板上还挂着 BootRoot 的 cue / 字幕 / 候选列表）——
        //    每个模态独占一个子容器，开关只动自己的容器。
        private VisualElement _modalContainer;
        private ModalId _currentModal = ModalId.None;
        /// <summary>
        /// 当前屏是否由 <see cref="OpenModal"/> **显式打开**(启动即建的常驻屏为 <c>false</c>)。
        /// <para>⚠️ 不能用「id 是否等于常驻屏」来判:`Casebook` 既可能被场景配成常驻屏,也能被玩家经
        /// `OpenModal` 打开 —— 用 id 比对会把「玩家开的脉案」误判成「常驻屏不挡路」。
        /// (🔁 2026-11-02 起**启动默认 = 无常驻屏**,故今日生产里 `Casebook` 只走后者;
        /// 判据仍不依赖启动形态,配成常驻屏的场景照样正确。)</para>
        /// </summary>
        private bool _modalOpenedExplicitly;
        /// <summary>
        /// 39 的档位请求缝(2026-11-02 · `casebook/story-006`)。
        /// <para>参数 = 「脉案是否摊开」;由装配层接到 `CasebookCameraRequester.Request`。
        /// `null` = 未接线(纯逻辑装配 / 无相机路径)⇒ 跳过请求,不抛。</para>
        /// <para>⚠️ **只发意图,不持相机** —— 与 `CommandSink` 等同款委托缝,承 ADR-020 §五。</para>
        /// </summary>
        private Action<bool> _casebookCameraRequest;
        /// <summary>模态打开期间被临时摘掉焦点能力的元素(关闭时逐个还原)。</summary>
        /// <remarks>⚠️ 只记「原本 focusable 且被我们关掉」的那些 —— 还原时不得把
        /// 本来就不 focusable 的元素变成可聚焦。</remarks>
        private readonly List<VisualElement> _inertedByModal = new List<VisualElement>();

        /// <summary>主菜单独占的容器(<c>null</c> = 主菜单没开)。</summary>
        /// <remarks>⚠️ 与 <see cref="_modalContainer"/> **分开**:主菜单不是模态,开它不得动
        /// `_currentModal`(否则规则七的门与 `BlocksPlay` 会读到一个「世界装载之前就摊开的模态」,
        /// 而「挡住世界」的语义此时不成立 —— `main-menu-42.md §1`)。</remarks>
        private VisualElement _mainMenuContainer;
        /// <summary>当前摊开的场景级屏实例(<c>null</c> = 没开)。</summary>
        private IPresentationRoot _currentSceneScreen;
        /// <summary>主菜单打开期间被摘掉焦点能力的元素(关闭时逐个还原)。</summary>
        /// <remarks>与 <see cref="_inertedByModal"/> 同机理、**不同记录**:两套开屏期不会重叠,
        /// 但混用同一份列表会让「谁该还原」依赖开合次序 —— 那正是将来分叉的地方。</remarks>
        private readonly List<VisualElement> _inertedByMainMenu = new List<VisualElement>();

        /// <summary>
        /// `ModalId` 闭集 → 屏名（屏名是 <see cref="ScreenFactories"/> 的键，也是序列化契约）。
        /// <para>⚠️ 本表是**闭集成员**与**屏**的唯一对应处：新增模态须同时改
        /// `ModalId`（ADR-013 §十 才可增员）、`ScreenFactories` 与本表 —— 三处缺一即开不出来。</para>
        /// </summary>
        private static readonly Dictionary<ModalId, string> ModalScreenNames =
            new Dictionary<ModalId, string>
            {
                [ModalId.Casebook] = "Casebook",
                [ModalId.SaveSlots] = "SaveSlots",
                [ModalId.InventoryContainer] = "Inventory",
                [ModalId.SettingsShell] = "Settings",
                [ModalId.Tutorial] = "Tutorial",
                [ModalId.ClinicPanel] = "ClinicPanel",
                [ModalId.PaperCloseup48] = "PaperCloseup",
                [ModalId.Pause] = "Pause",          // 🔁 2026-10-31 Amendment C 第八员
                [ModalId.Processing] = "Processing", // 🔁 2026-11-02 Amendment D 第九员
            };

        /// <summary>屏幕名 → Screen 查表(八个具名载体;名字是**序列化契约**,
        /// 场景 / Prefab 里写死的字符串,不可改成别的写法)。</summary>
        private static readonly Dictionary<string, Func<VisualElement, SkeuoElementLibrary, IPresentationRoot>>
            ScreenFactories = new Dictionary<string, Func<VisualElement, SkeuoElementLibrary, IPresentationRoot>>
            {
                ["Casebook"] = (root, lib) => new CasebookScreen(root, lib),
                ["ClinicPanel"] = (root, lib) => new ClinicPanelScreen(root, lib),
                ["Inventory"] = (root, lib) => new InventoryContainerScreen(root, lib),
                ["SaveSlots"] = (root, lib) => new SaveSlotsScreen(root, lib),
                ["Settings"] = (root, lib) => new SettingsScreen(root, lib),
                ["Tutorial"] = (root, lib) => new TutorialScreen(root, lib),
                ["PaperCloseup"] = (root, lib) => new PaperCloseupScreen(root, lib),
                ["Pause"] = (root, lib) => new PauseScreen(root, lib),
                ["Processing"] = (root, lib) => new Screens.Processing18Screen(root, lib),
                // 🔁 2026-11-01:主菜单 = **场景级屏**,不是 `ModalId` 成员(不进 `ModalScreenNames`)。
                //    它出现在世界装载之前 ⇒ 不参与「挡住世界」那套读面(`main-menu-42.md §1`)。
                ["MainMenu"] = (root, lib) => new MainMenuScreen(root, lib),
            };

        private void Awake()
        {
            EnsureDocument();

            if (_panel == null)
            {
                Debug.LogError("[SkeuoRuntimeDriver] UIDocument / Panel 缺失 —— UI 载体不可用。");
                return;
            }

            // 拟物样式表挂载(九份 Skeuo USS;见 _styleSheets 头注的加载序说明)
            AttachStyleSheets();

            // ── T2.2:Screen 实例化(名字 → 工厂;未知名 fail-loud) ──
            // 🔁 2026-10-31:改走容器 + 记 `_currentModal`（此前只建屏、从不记「开的是哪一个」⇒
            // 模态门拿不到真源,也没有任何运行期换屏入口）。
            if (string.IsNullOrEmpty(_screenName))
            {
                // 🔁 2026-11-02:**无常驻屏** = 合法启动形态(默认)。见 `_screenName` 的头注。
                // ⚠️ 这里**不能** `return` —— 早退会让样式表之后的焦点环施加者、注入面补绑
                //    全部跳过,驱动变成一个「活着但没接线」的壳。
                _screen = null;
                _modalContainer = null;
                _currentModal = ModalId.None;
                _modalOpenedExplicitly = false;
            }
            else
            {
                _screen = InstantiateScreen(_screenName, out _modalContainer);
                if (_screen == null)
                {
                    Debug.LogError($"[SkeuoRuntimeDriver] Screen '{_screenName}' not found.");
                    return;
                }
                _currentModal = _screen is IModalState openState ? openState.Modal : ModalId.None;
                _modalOpenedExplicitly = false;   // 启动即建的屏 = 常驻屏,不算「玩家摊开的模态」
            }

            // ── T2.1:DTO 绑定(标记接口 IDtoSource 的 42 侧实现) ──
            // 写通道若先于 Awake 赋值(场景序列化顺序不定),此处补绑一次(幂等)
            if (_commandSink != null && _screen is CasebookScreen preBound)
                preBound.BindCommandSink(_commandSink);

            // 🔁 2026-10-31 载入接线轮:存档位屏同款补绑(幂等)。缺注入 ⇒ 行照画、按了留空。
            if (_screen is SaveSlotsScreen saveSlots)
            {
                saveSlots.BindCommandSink(_saveSlotSink);
                SetSaveSlotRows(null);
            }
            if (_screen is PauseScreen pause) pause.BindCommandSink(_pauseSink);

            if (Source != null)
            {
                if (_screen != null) _screen.Bind(Source);   // 无常驻屏 ⇒ 无屏可绑(刷新仍做:绷注入面)
                RefreshFromSource();
            }
            else
            {
                // 缺源 = 留空(空行),**不造假数据**(ADR-013 C3 + story-024 边界③)。
                Debug.LogWarning("[SkeuoRuntimeDriver] ICasebookDtoSource 未注入 —— " +
                                 "脉案值区留空(未查 = 空行,未编造读数)。");
            }

            Debug.Log(_screen != null
                ? $"[SkeuoRuntimeDriver] Screen '{_screenName}' instantiated and bound."
                : "[SkeuoRuntimeDriver] 无常驻屏(启动形态 = 无模态;脉案等按需经 OpenModal 打开)。");

            // ── B-6-2:焦点可见施加者(story-009 契约的运行期兑现)──
            // 病因(第六轮 playtest 追问 A「看不到焦点在哪」):焦点环样式与契约 story-009
            // 已全做完,但「焦点转移 → 施加/移除 .focus-visible」的施加者**从未接线**
            // (story-009 注记明写「接线留待焦点接线轮」)。引擎 FocusController 一直在
            // 移动焦点(B-6-1 接通导航后方向键已在移动它),只是没人把焦点态翻译成呈现态。
            // ⚠️ **甲案(用户裁定 2026-10-14):环走 overlay 子元素,不直接挂类** ——
            //   ① `.focus-visible` × `.ruled` 的 border-bottom 加载序冲突(story-021 登记
            //      的「接线前置裁定项」)**结构性消失**(overlay 自带层,不与行类 border 级联,
            //      加载序裁定项随之注销);
            //   ② 单槽铁律(`background-image` 整槽替换 ⇒ 带纸纹元素获焦瞬间纸纹被顶掉)
            //      **结构性消失**(overlay 在上层,行自身纹理不动)。
            //   ⚠️ 不自实现焦点算法(ADR-013 §五):FocusController 是焦点真源,本施加者
            //   只把引擎焦点态翻译成呈现态 —— 监听面板根的 FocusIn/FocusOut 冒泡。
            AttachFocusRingApplier();
        }

        /// <summary>焦点环施加者(B-6-2 · 甲案 overlay):获焦目标 append 环子元素,失焦移除。</summary>
        /// <remarks>⚠️ 监听挂**面板根**(冒泡收全树),每个目标自己的 FocusIn/FocusOut
        /// 不另挂 —— 目标是动态建的(病名册条目 / 戥子档运行期建),逐目标挂会漏。
        /// ring `pickingMode = Ignore`:环不参与拾取,点环不挡下面的行。</remarks>
        /// <summary>
        /// 焦点环贴图(黄铜 2px 环)。**必须走代码灌注**,不能只靠 USS。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ 2026-10-31 实测定因:USS 的 <c>url("guid:…")</c> 把这张图解析成 **Texture2D**
        /// (<c>resolvedStyle.backgroundImage.sprite == null</c>,tex 32×32),sprite 的九宫格边框
        /// 随之丢失 ⇒ 环被画成一整块黄色图形(把「继续」盖住)。
        /// 同一张图、同一 slice、同一尺寸,**代码 <c>StyleBackground(Sprite)</c> 路径渲染出的是
        /// 干净的 2px 框**(四格隔离探针实测)。</para>
        /// <para>slice(2px)仍以 USS 为**单一出处** —— 本字段只覆盖图,不覆盖几何。</para>
        /// <para>缺省(null)⇒ 回落到 USS 取图(旧行为,视觉上有缺陷但不崩)。</para>
        /// </remarks>
        [SerializeField] private Sprite _focusRingSprite;

        /// <summary>焦点环贴图(观测 / 夹具注入面)。</summary>
        public Sprite FocusRingSprite
        {
            get => _focusRingSprite;
            set => _focusRingSprite = value;
        }

        private void AttachFocusRingApplier()
        {
            _panel.RegisterCallback<FocusInEvent>(OnFocusIn);
            _panel.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }

        /// <summary>环子元素构造(甲案 overlay):匿名、不拾取、四向负 offset 溢出环厚。</summary>
        /// <remarks>图由 <see cref="FocusRingSprite"/> **代码灌注**(见该字段 remarks);
        /// 未注入 ⇒ 回落 USS 取图。slice 与几何一律仍由 USS 那条规则负责。</remarks>
        private VisualElement CreateFocusRing()
        {
            var ring = new VisualElement();
            ring.AddToClassList(FocusVisibleStyle.ClassName);
            ring.pickingMode = PickingMode.Ignore;
            ring.focusable = false;
            if (_focusRingSprite != null)
            {
                // 🔁 **2026-11-02(`TD-skeuo-01` 丁案)—— 代码灌注 sprite 的 fail-loud 守卫**。
                // 实测归因(`nineslice-probe-2026-10-31`):**代码灌注 sprite** 路径上,
                // 当元素**两轴都**小于 sprite 的 border 之和时,渲染成**纯色黑块** ——
                // 零报错、零夹具转红(元素尺寸不在任何断言面上)。
                // ⚠️ USS 路径**结构性免疫**(`url("guid:…")` 解析成 Texture2D、sprite=null)。
                // ⇒ 生产里**唯一**的代码灌注点就是这里(border = 2 ⇒ 和 = 4),离界一个数量级;
                //   守卫把「将来有人调大 slice 或缩小环」的静默黑块变成**抛**。
                // ⚠️ 静态门在这里不可靠(元素尺寸由布局决定,编译期读不到)⇒ 只能运行期判。
                // `Sprite.border` = Vector4(left, bottom, right, top),单位 px(float)。
                // ⇒ 横轴 = x+z,纵轴 = y+w;两轴**同时**越界才黑块,故取两轴最大值判。
                var border = _focusRingSprite.border;
                float borderSum = Mathf.Max(border.x + border.z, border.y + border.w);
                if (borderSum > 0f && borderSum > FocusRingMinSide())
                {
                    throw new InvalidOperationException(
                        $"[SkeuoRuntimeDriver] 焦点环 sprite 的 border 之和 = {borderSum}px " +
                        $"超过环的最小边长 {FocusRingMinSide()}px —— 代码灌注 sprite 会渲染成" +
                        "纯色黑块(实测归因,TD-skeuo-01)。须减小 slice 或加大环尺寸。");
                }
                ring.style.backgroundImage = new StyleBackground(_focusRingSprite);
            }
            return ring;
        }

        /// <summary>
        /// 焦点环的**最小边长**(px)—— 代码灌注 sprite 的守卫下界(`TD-skeuo-01`)。
        /// </summary>
        /// <remarks>
        /// <para>值 = **44**,出处 = AB-3「焦点落点 ≥ 44×44」,与 USS 变量
        /// <c>--skeuo-shared-row-height: 44px</c>(`SkeuoThemeVariables.uss:149`)同源。
        /// 环是 <c>position: absolute</c> + 四向 0 的 overlay ⇒ 它**恰等于目标边界**
        /// ⇒ 目标的最小边即环的最小边。</para>
        /// <para>⚠️ 这里**读不到** USS 自定义属性(UI Toolkit 运行期无公开 API),
        /// 故以常量承载同一数值。两处若分叉,USS 侧改 `--skeuo-shared-row-height` 时
        /// 本常量须同改 —— 但**只会让守卫更保守或更松**,不会造成黑块。</para>
        /// </remarks>
        private const int FocusRingMinSidePx = 44;

        private static int FocusRingMinSide() => FocusRingMinSidePx;

        /// <summary>查找目标上已挂的环(失焦移除 / 防重入用)。</summary>
        private static VisualElement FindFocusRing(VisualElement target)
        {
            for (int i = 0; i < target.childCount; i++)
            {
                var child = target[i];
                if (child.ClassListContains(FocusVisibleStyle.ClassName))
                    return child;
            }
            return null;
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            var target = evt.target as VisualElement;
            if (target == null) return;
            // 防重入:已挂环不重复 append(同目标 FocusIn 重发)
            if (FindFocusRing(target) != null) return;
            target.Add(CreateFocusRing());
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            // 只摘**本目标的**环;新目标的环由它的 FocusIn 挂(FocusOut 先于下一 FocusIn,
            // 摘完再挂 = 环随焦点转移,不多不少)。
            if (evt.target is VisualElement target)
            {
                var ring = FindFocusRing(target);
                ring?.RemoveFromHierarchy();
            }
        }

        /// <summary>屏外注入的只读 DTO 源(42 不反向引用装配层 —— 由场景 / 测试赋值)。</summary>
        public ICasebookDtoSource Source
        {
            get => _source;
            set
            {
                _source = value;
                if (_screen != null && _source != null)
                {
                    _screen.Bind(_source);
                    RefreshFromSource();
                }
            }
        }

        /// <summary>
        /// 屏外注入的**写通道**(sprint-08 S1.1 件 F1 —— 42 UI 事件 → 装配层写者)。
        /// <para>实现方住装配层(<c>CasebookCommandRouter</c>);42 不反向引用装配层
        /// (ADR-025 §① 装配方向不可倒),故经此属性注入(与 <see cref="Source"/> 同款)。</para>
        /// <para>⚠️ 未注入 ⇒ 病名栏 Submit **留空**(不崩、不造动作):与「缺源 = 空行」同纪律
        /// —— 缺写通道不是错误态,是未接线的诚实形态(测试 / 无命令面装配)。</para>
        /// </summary>
        public ICasebookCommandSink CommandSink
        {
            get => _commandSink;
            set
            {
                _commandSink = value;
                if (_screen is CasebookScreen casebook)
                    casebook.BindCommandSink(_commandSink);
            }
        }

        /// <summary>
        /// 存档位屏的**载入意图**出口(TD-persist-04 裁定② 的「通电待信」收尾)。
        /// <para>实现方住装配层(<c>SaveSlotCommandRouter</c>);42 不反向引用装配层
        /// (ADR-025 §①),故经此属性注入(与 <see cref="CommandSink"/> 同款)。
        /// 未注入 ⇒ 行照常画、确认动作留空(不崩、不造动作)。</para>
        /// </summary>
        public ISaveSlotCommandSink SaveSlotSink
        {
            get => _saveSlotSink;
            set
            {
                _saveSlotSink = value;
                if (_screen is SaveSlotsScreen saveSlots) saveSlots.BindCommandSink(_saveSlotSink);
            }
        }

        /// <summary>
        /// 存档位清单(由装配层给 —— 42 不自己发明槽位数)。
        /// <para>⚠️ 数值出处:7b 的槽位集归 7b GDD / 数值轮;装配层今日按
        /// `BootRoot` 的具名常量给,本类只负责画。</para>
        /// </summary>
        public IReadOnlyList<uint> SaveSlots
        {
            get => _saveSlots;
            set
            {
                _saveSlots = value;
                // 只有槽号 ⇒ 全部按「未绑定」画(7b §6:纸线格完整、行字空白)。
                if (value == null) { _saveSlotViews = null; }
                else
                {
                    var views = new List<SaveSlotView>(value.Count);
                    for (int i = 0; i < value.Count; i++) views.Add(SaveSlotView.Unbound(value[i]));
                    _saveSlotViews = views;
                }
                SetSaveSlotRows(null);
            }
        }

        /// <summary>
        /// 带状态的槽位清单来源(装配层每次开册 / 每次写盘落定后调一次)。
        /// <para>⚠️ 未注入 ⇒ 退回 <see cref="SaveSlots"/> 的「未绑定」形态,
        /// 而**不是**画成全体空槽 —— 「我不知道」与「这里没写过」在纸面上都是没写字的一格,
        /// 但在语义上不能混(混了就等于对玩家断言「你的档没了」)。</para>
        /// </summary>
        public Func<IReadOnlyList<SaveSlotView>> SaveSlotViewProvider { get; set; }

        /// <summary>
        /// 存档写入**是否尚未落定**(由装配层接 <c>SaveService.WriteQueue.Outstanding</c>)。
        /// <para>写是异步的 ⇒ 按了「留档」之后立刻重探会读到**写之前**的磁盘状态,
        /// 屏上看起来「按了没反应」。本驱动因此把重探推迟到队列排空之后。</para>
        /// </summary>
        public Func<bool> SaveInFlight { get; set; }

        /// <summary>请求在写入落定后重画槽位行(装配层在「落笔已受理」时调)。</summary>
        public void MarkSaveSlotRowsDirty() => _saveSlotsDirty = true;

        /// <summary>按 <see cref="SaveSlotViewProvider"/> 重取并重画(未注入 ⇒ 用给定清单)。</summary>
        private void SetSaveSlotRows(IReadOnlyList<SaveSlotView> fallback)
        {
            IReadOnlyList<SaveSlotView> views = null;
            if (SaveSlotViewProvider != null) views = SaveSlotViewProvider();
            if (views == null) views = fallback;
            if (views != null) _saveSlotViews = views;

            if (_screen is SaveSlotsScreen saveSlots) saveSlots.SetSlotViews(_saveSlotViews);
        }

        /// <summary>已实例化的 Screen(测试观测面)。</summary>
        public IPresentationRoot Screen => _screen;

        /// <summary>
        /// 暂停壳的**意图**出口(2026-10-31 ADR-013 §十-C Amendment C)。
        /// <para>实现方住装配层(<c>PauseCommandRouter</c>);42 不反向引用装配层
        /// (ADR-025 §①)。未注入 ⇒ 行照常画、按了留空(不崩、不造动作)。</para>
        /// </summary>
        public IPauseCommandSink PauseSink
        {
            get => _pauseSink;
            set
            {
                _pauseSink = value;
                if (_screen is PauseScreen pause) pause.BindCommandSink(_pauseSink);
            }
        }

        /// <summary>
        /// 主菜单的**意图**出口(2026-11-01 · `main-menu-42.md §7`)。
        /// <para>实现方住装配层(42 不反向引用 Boot);未注入 ⇒ 行照常画、按了留空。
        /// 「有没有档」「继续到哪一份」不在这里判 —— 本驱动只转发那一次确认。</para>
        /// </summary>
        public IMainMenuCommandSink MainMenuSink
        {
            get => _mainMenuSink;
            set
            {
                _mainMenuSink = value;
                if (_mainMenuContainer != null && CurrentScreen is MainMenuScreen menu)
                    menu.BindCommandSink(value);
            }
        }

        /// <summary>
        /// 当前摊开的**场景级屏**(今日唯一成员 = 主菜单;<c>null</c> = 没开)。
        /// <para>⚠️ 与 <see cref="Screen"/> 不同:后者是「常驻 / 模态屏」(脉案 / 暂停壳 /
        /// 存档位…),本属性只指场景级屏 —— 两者可以同时存在(主菜单压在世界装载之前,
        /// 常驻脉案屏仍在树里)。</para>
        /// </summary>
        public IPresentationRoot CurrentScreen => _currentSceneScreen;

        /// <summary>
        /// 打开**场景级屏**(非 `ModalId` 成员;今日唯一成员 = 主菜单)。
        /// <para>开屏动作 = 建独占容器 + 落焦第一落点 + 施加焦点单栈门(把面板上其余的
        /// 可聚焦元素临时摘掉,与模态同机理、不同记录)。</para>
        /// <para>⚠️ **不动** <see cref="CurrentModal"/>:场景级屏不参与规则七的门
        /// (它出现时世界尚未装载,「挡住世界」的语义不成立 —— `main-menu-42.md §1`)。
        /// 也**不动** <see cref="_modalContainer"/>:常驻脉案屏在它自己那格里,主菜单关掉后原样还在。</para>
        /// </summary>
        /// <returns><c>true</c> = 屏已建并摊开;<c>false</c> = 无面板 / 屏名未登记(不崩)。</returns>
        public bool ShowSceneLevelScreen(string screenName, bool hasContinueSave)
        {
            if (_panel == null)
            {
                Debug.LogError("[SkeuoRuntimeDriver] 无 UIDocument 面板 —— 场景级屏开不出来(非崩溃态)。");
                return false;
            }
            if (!ScreenFactories.ContainsKey(screenName))
            {
                Debug.LogError($"[SkeuoRuntimeDriver] 场景级屏 '{screenName}' 未在 ScreenFactories 登记 —— " +
                               "拒绝静默无操作(屏名三处不一致)。");
                return false;
            }

            DismissSceneLevelScreen();

            IPresentationRoot created = InstantiateScreen(screenName, out VisualElement container);
            if (created is not MainMenuScreen menu)
            {
                // 屏建出来但不是主菜单形态 ⇒ 拆掉,不留半个旁路(闭集外的名字不得借用本入口)。
                if (created != null) container.RemoveFromHierarchy();
                Debug.LogError($"[SkeuoRuntimeDriver] 屏 '{screenName}' 不是场景级屏形态 —— 拒绝摊开。");
                return false;
            }

            // 容器铺满面板(USS `.scene-host`):不铺满则 `flex-grow` 无空间可分配,
            // 「纵向居中」退化为「挤在常驻屏下面的剩余空间里」(2026-11-01 实拍实证)。
            container.AddToClassList("scene-host");

            // 常驻屏**暂隐**:世界尚未装载时它不该被看到(实拍里它整屏透出来 ——
            // 脉案的八个值区摊在菜单纸面周围,与 spec §5「纸面下是纯色底」相悖)。
            // ⚠️ 用 `display: none` 而不是给它叠一层底色:后者要在 42 侧发明一个色值,
            //    而色值归美术轮(spec §5 末「刻意留白」)。
            if (_modalContainer != null) _modalContainer.style.display = DisplayStyle.None;

            _currentSceneScreen = menu;
            _mainMenuContainer = container;
            menu.BindCommandSink(_mainMenuSink);
            menu.SetHasContinueSave(hasContinueSave);

            ApplySceneLevelFocusScope();
            VisualElement first = menu.FirstFocusable;
            if (first != null && first.panel != null) first.Focus();
            return true;
        }

        /// <summary>收起场景级屏(没开 ⇒ 空操作)。撤焦点门,常驻屏原样留在自己的容器里。</summary>
        public void DismissSceneLevelScreen()
        {
            ReleaseSceneLevelFocusScope();

            // 常驻屏恢复可见(与开屏时的暂隐成对;无场景级屏时是空操作)
            if (_modalContainer != null) _modalContainer.style.display = DisplayStyle.Flex;

            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.RemoveFromHierarchy();
                _mainMenuContainer = null;
            }
            _currentSceneScreen = null;
        }

        /// <summary>
        /// UI 根(UIDocument.rootVisualElement)—— 42 载体挂载面(BootRoot 挂候选列表)。
        /// <para><c>Awake</c> 后非 null(<see cref="EnsureDocument"/> 已建);
        /// <c>panel == null</c> = 逻辑树在、不渲染(缺 PanelSettings 是合法的测试形态)。</para>
        /// </summary>
        public VisualElement GetUiRoot() => _panel;

        /// <summary>
        /// tick 边沿刷新(由驱动方调用):重读源 → 走 <see cref="CasebookScreen.ApplyData"/>。
        /// <para>只对脉案页生效(其余 Screen 的值区归各自 story / 数据绑定轮)。</para>
        /// </summary>
        public void OnTickEdge()
        {
            if (_screen is CasebookScreen casebook && _source != null)
                casebook.ApplyData(_source);

            // 🔁 2026-11-03:落笔已受理 ⇒ 等写队列排空再重探。
            //    写是异步的(`SaveService.Checkpoint` 把落盘排进 `SaveWriteQueue`),
            //    按下去就重探会读到写之前的磁盘状态 —— 那就是「按了没反应」的复现。
            if (_saveSlotsDirty && _screen is SaveSlotsScreen saveSlots)
            {
                bool pending = SaveInFlight != null && SaveInFlight();
                if (!pending)
                {
                    _saveSlotsDirty = false;
                    SetSaveSlotRows(null);
                    // 重画会替换行元件 ⇒ 焦点原本落在旧元件上,须重新落一次,
                    // 否则手柄玩家在存档后**焦点悬空**(一步也走不了)。
                    if (saveSlots.WritableRow != null && saveSlots.WritableRow.panel != null)
                        saveSlots.WritableRow.Focus();
                }
            }
        }

        /// <summary>
        /// 挂载拟物样式表(编辑器清单 ⇒ <c>rootVisualElement.styleSheets</c>)。
        /// <para>零清单 = LogWarning 后继续(逻辑树仍可用 —— 测试面);但**渲染缺全部形态**。</para>
        /// <para>⚠️ **2026-10-13 修脆弱点**:原判据 <c>_panel.panel == null ⇒ return</c> 依赖
        /// 「<c>Awake</c> 时 panel 已 attach」的时序假设 —— 若 <c>Awake</c> 早于 <c>UIDocument</c>
        /// 的 panel attach,则**静默跳过挂载且永不重试**(无 OnEnable/Start 补挂)⇒ 九份 USS
        /// 全不挂 ⇒ 全部文本回退默认字体 = 糊(探针 v1 实证:挂载数 0)。而
        /// <c>VisualElement.styleSheets</c> 在元素**未 attach 时也能添加**,attach 后自动生效
        /// ⇒ 该守卫**有害无益**,已移除。本方法**幂等**(<c>Contains</c> 去重),
        /// 由 <see cref="Awake"/> 与 <see cref="OnEnable"/> 双调 —— 兜底 <c>_panel</c> 就绪较晚的路径。</para>
        /// </summary>
        private void AttachStyleSheets()
        {
            if (_panel == null)
                return;   // 无 UI 根(UIDocument 未建)—— 无处可挂,不是错误态

            if (_styleSheets == null || _styleSheets.Count == 0)
            {
                Debug.LogWarning("[SkeuoRuntimeDriver] 未配置拟物样式表(_styleSheets 为空)—— " +
                                 "UI 将以无样式文本渲染(纸纹 / 线格 / 值区形态对全部不可见)。" +
                                 "请在 Boot.unity 的 SkeuoUI 预摆里拖入九份 Skeuo*.uss。");
                return;
            }

            foreach (StyleSheet sheet in _styleSheets)
            {
                if (sheet == null) continue;
                if (!_panel.styleSheets.Contains(sheet))
                    _panel.styleSheets.Add(sheet);
            }
        }

        /// <summary>
        /// 补挂兜底(2026-10-13):若 <see cref="Awake"/> 时 <c>_panel</c> 尚未就绪
        /// (UIDocument 的 root 晚于本组件 Awake 建),此处重试一次 —— 幂等,零副作用。
        /// </summary>
        private void OnEnable()
        {
            if (_panel == null)
                EnsureDocument();
            AttachStyleSheets();
        }

        /// <summary>
        /// 当前摊开的模态(`None` = 没有)。
        /// <para>⚠️ 这是 42 侧的**唯一真源**:4 的模态门只读它的布尔投影(AC-4-09 引用而非复制)。</para>
        /// </summary>
        public ModalId CurrentModal => _currentModal;

        /// <summary>
        /// 39 的档位请求缝(`casebook/story-006` · 2026-11-02)。
        /// <para>参数 = 「脉案是否摊开」;装配层接到 `CasebookCameraRequester.Request`。
        /// `null` = 未接线 ⇒ 跳过请求,不抛(纯逻辑装配 / 无相机路径)。</para>
        /// <para>⚠️ 本类**只在模态边沿**调它(见 <see cref="NotifyCasebookCamera"/>),
        /// 不在这里做任何相机判定 —— ADR-020 §五「相机只读不持状态」。</para>
        /// </summary>
        public Action<bool> CasebookCameraRequest
        {
            get => _casebookCameraRequest;
            set => _casebookCameraRequest = value;
        }

        /// <summary>是否有模态摊开(任一屏在,含常驻屏)。</summary>
        public bool IsModalOpen => _currentModal != ModalId.None;

        /// <summary>
        /// **是否有一个玩家摊开的模态在挡着世界**(规则七方向②的真正判据)。
        /// <para>判据 = 当前屏 ≠ None **且** ≠ 常驻屏。两处刻意分开:</para>
        /// <list type="bullet">
        ///   <item><see cref="IsModalOpen"/> 答「有没有屏」—— 启动即真的,不能喂闸门;</item>
        ///   <item>本属性答「有没有**挡路**的屏」—— 启动即建的常驻脉案屏不挡(E 查体在它开着时
        ///         仍可用),经 <see cref="OpenModal"/> 打开的屏(存档位 / 库存 / 玩家重开的脉案)才挡。</item>
        /// </list>
        /// <para>⚠️ 若将来启动形态改成「无屏」(常驻世界视图),本属性与
        /// <see cref="IsModalOpen"/> 仍然一致 —— 判据不依赖启动形态。</para>
        /// </summary>
        public bool BlocksPlay => _currentModal != ModalId.None && _modalOpenedExplicitly;

        /// <summary>
        /// 打开某个模态(运行期换屏的唯一入口)。
        /// <para>已开着别的模态 ⇒ 先关再开(同一时刻至多一个 —— ADR-013「焦点单栈」的结构前提)。</para>
        /// <para>打开后重放注入面(源 / 写通道 / 存档位),与 Awake 的补绑同款 —— 否则新屏拿到的是空绑定。</para>
        /// </summary>
        /// <returns><c>true</c> = 该模态确有对应的屏并已打开;闭集外的 id / 无面板 ⇒ <c>false</c>(不崩)。</returns>
        public bool OpenModal(ModalId modal)
        {
            if (modal == ModalId.None) return false;
            if (_panel == null)
            {
                Debug.LogError("[SkeuoRuntimeDriver] 无 UIDocument 面板 —— 模态开不出来(非崩溃态)。");
                return false;
            }
            if (!ModalScreenNames.TryGetValue(modal, out string screenName))
            {
                Debug.LogError($"[SkeuoRuntimeDriver] 模态 {modal} 没有对应的屏 —— " +
                               "闭集表 / ScreenFactories / ModalId 三处不一致(拒绝静默无操作)。");
                return false;
            }

            // ⚠️ 记下**终态前**的模态:末尾按 (before, after) 发一次边沿。
            ModalId before = _currentModal;

            CloseModalCore();

            _screen = InstantiateScreen(screenName, out _modalContainer);
            if (_screen == null)
            {
                Debug.LogError($"[SkeuoRuntimeDriver] Screen '{screenName}' not found.");
                return false;
            }
            _currentModal = modal;
            _modalOpenedExplicitly = true;
            // 🔁 2026-10-31 走查实测缺陷:模态打开后,方向键把焦点**移出了模态**
            //    (落到了同一面板上的急救候选列表「跳过」行)⇒ ADR-013 §五 的「焦点单栈门」
            //    在运行期不成立。UI Toolkit 没有内建焦点陷阱,故在此显式施加。
            ApplyModalFocusScope();

            // 注入面重放(与 Awake 的补绑同款,幂等)
            if (_source != null) _screen.Bind(_source);
            if (_commandSink != null && _screen is CasebookScreen casebook)
                casebook.BindCommandSink(_commandSink);
            if (_screen is SaveSlotsScreen saveSlots)
            {
                saveSlots.BindCommandSink(_saveSlotSink);
                // 🔁 2026-11-03:**每次开册重探一次** —— 上一次会话写下的档,
                //    以及本次会话在别的路径(自动 checkpoint / 退出保存)写下的档,
                //    都要在摊开时是新的。常量清单不会自己更新。
                SetSaveSlotRows(null);
                // 🔁 2026-10-31:开屏后把焦点落到第一行 —— 否则手柄玩家无处可走
                //    (游戏没有指针)。⚠️ 无 panel 时 `Focus()` 会报错 ⇒ 先判,
                //    无 panel 是合法的测试形态(逻辑树在、不渲染)。
                VisualElement first = saveSlots.FirstFocusable;
                if (first != null && first.panel != null) first.Focus();
            }
            if (_screen is PauseScreen pauseScreen)
            {
                pauseScreen.BindCommandSink(_pauseSink);
                VisualElement first = pauseScreen.FirstFocusable;
                if (first != null && first.panel != null) first.Focus();
            }
            if (_source != null) RefreshFromSource();

            // 39 的档位意图:按 (开屏前, 开屏后) 的**终态**发一条边沿。
            // ⚠️ 必须在 `_currentModal = modal` **之后** —— 中途那次 CloseModalCore 不发音,
            //    否则「脉案 → 库存」会多发一条 Explore(中间态),违反 AC-39-07 的「转换恰 1 次」。
            NotifyCasebookCamera(before, _currentModal);
            return true;
        }

        /// <summary>关闭当前模态(无模态 ⇒ 空操作)。</summary>
        public void CloseModal()
        {
            ModalId before = _currentModal;
            CloseModalCore();
            NotifyCasebookCamera(before, _currentModal);
        }

        /// <summary>
        /// 关屏的**实现体**(不发 39 的档位意图)。
        /// <para>⚠️ 为什么与 <see cref="CloseModal"/> 分开:<see cref="OpenModal"/> 换屏时会先关旧屏,
        /// 那一步若发意图,「脉案 → 库存」就会在中间态多发一条 `Explore`。
        /// ⇒ 换屏路径只调本方法,由 <see cref="OpenModal"/> 末尾按终态发**一次**。</para>
        /// </summary>
        private void CloseModalCore()
        {
            ReleaseModalFocusScope();
            if (_modalContainer != null)
            {
                _modalContainer.RemoveFromHierarchy();
                _modalContainer = null;
            }
            _screen = null;
            _currentModal = ModalId.None;
            // ⚠️ 这里**不**复位 `_modalOpenedExplicitly`:证伪轮实测该行不可反证
            //    (`BlocksPlay` 先判 `_currentModal != None`,关屏后恒 false)⇒ 留着就是死代码。
            //    下次 `OpenModal` 会把它置 true,语义不需要「关屏即清零」。
        }

        /// <summary>
        /// 39 的档位意图**边沿**(`casebook/story-006` · 2026-11-02)。
        /// <para>判据 = 「换屏前是不是脉案」与「换屏后是不是脉案」**不等** ⇒ 发一条:
        /// `true`(进脉案)⇒ 请求 `Casebook`;`false`(离开脉案)⇒ 请求 `Explore`。</para>
        /// <para>⚠️ **同态不发**:重复打开同一个模态、或从非脉案换到另一个非脉案,
        /// 都不产生请求 —— 意图制只需状态变化,每帧/每次换屏都入队会让
        /// `CameraModeMachine._pending` 无谓增长。</para>
        /// <para>⚠️ 未接线(`CasebookCameraRequest == null`)⇒ 静默跳过,不抛
        /// (纯逻辑装配 / 无相机路径是合法形态)。</para>
        /// </summary>
        private void NotifyCasebookCamera(ModalId before, ModalId after)
        {
            if (_casebookCameraRequest == null) return;

            bool wasCasebook = before == ModalId.Casebook;
            bool nowCasebook = after == ModalId.Casebook;
            if (wasCasebook == nowCasebook) return;

            _casebookCameraRequest(nowCasebook);
        }

        /// <summary>
        /// 模态期**焦点单栈门**的施加者(ADR-013 §五)。
        /// <para>做法:把面板上除模态容器外的所有**原本可聚焦**的元素临时摘掉焦点能力,
        /// 关闭时逐个还原。UI Toolkit 没有内建焦点陷阱,`tabIndex` 也不限制导航范围 ——
        /// 不施加则方向键会走遍整棵树(走查实测:焦点逃到请求列表的「跳过」行)。</para>
        /// <para>⚠️ 只摘 `focusable` 为真的元素并记录;还原按记录回放,不猜「应该能聚焦」。</para>
        /// <para>⚠️ **本法的边界**:模态打开**之后**才在面板上新建的可聚焦元素不会被摘
        /// (它不在这次快照里)。今日无此路径;若将来出现,须改为「每次导航前重算」。</para>
        /// </summary>
        private void ApplyModalFocusScope()
        {
            _inertedByModal.Clear();
            ApplyFocusScope(_modalContainer, _inertedByModal);
        }

        /// <summary>
        /// 场景级屏期**焦点单栈门**(2026-11-01 · `main-menu-42.md §7` P-01)。
        /// <para>与模态同机理、**不同记录**:主菜单压在世界装载之前,常驻脉案屏仍在树里,
        /// 不摘焦点则方向键会走到它上面去。</para>
        /// </summary>
        private void ApplySceneLevelFocusScope()
        {
            _inertedByMainMenu.Clear();
            ApplyFocusScope(_mainMenuContainer, _inertedByMainMenu);
        }

        /// <summary>还原 <see cref="ApplySceneLevelFocusScope"/> 摘掉的那些元素。</summary>
        private void ReleaseSceneLevelFocusScope()
        {
            for (int i = 0; i < _inertedByMainMenu.Count; i++)
                _inertedByMainMenu[i].focusable = true;
            _inertedByMainMenu.Clear();
        }

        /// <summary>
        /// 焦点单栈门的**唯一实现**:把面板上除 <paramref name="keep"/> 外的所有原本可聚焦元素
        /// 摘掉焦点能力,记进 <paramref name="record"/> 以便还原。
        /// <para>⚠️ 模态与场景级屏各用一份 <c>record</c>(谁摘的谁还原),但**走同一个实现** ——
        /// 两份实现将来必然漂移,而漂移是静默的(一处会还原、另一处忘了)。</para>
        /// </summary>
        private void ApplyFocusScope(VisualElement keep, List<VisualElement> record)
        {
            if (keep == null || _panel == null) return;
            foreach (VisualElement child in _panel.Children())
                InertFocusableDescendants(child, keep, record);
        }

        /// <summary>把子树里所有可聚焦元素摘掉焦点能力,并记下来以便还原。</summary>
        private static void InertFocusableDescendants(VisualElement root, VisualElement keep, List<VisualElement> record)
        {
            if (root == null || root == keep) return;
            if (root.focusable)
            {
                record.Add(root);
                root.focusable = false;
            }
            foreach (VisualElement child in root.Children())
                InertFocusableDescendants(child, keep, record);
        }

        /// <summary>还原 <see cref="ApplyModalFocusScope"/> 摘掉的那些元素。</summary>
        private void ReleaseModalFocusScope()
        {
            for (int i = 0; i < _inertedByModal.Count; i++)
                _inertedByModal[i].focusable = true;   // 逐个还原(记录里只有原本可聚焦的)
            _inertedByModal.Clear();
        }

        /// <summary>
        /// 建屏并给它一个**独占子容器**(开关只动自己的容器 —— 面板上还挂着 cue / 字幕 / 候选列表,
        /// 不能整面板清空)。屏名未知 ⇒ fail-loud(不 fallback 到别的屏)。
        /// </summary>
        private IPresentationRoot InstantiateScreen(string name, out VisualElement container)
        {
            container = new VisualElement { name = "modal-" + name };
            _panel.Add(container);

            IPresentationRoot created = CreateScreen(name, container);
            if (created == null) container.RemoveFromHierarchy();
            return created;
        }

        /// <summary>按名实例化 Screen(闭集;零反射)。</summary>
        private static IPresentationRoot CreateScreen(string name, VisualElement panel)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!ScreenFactories.TryGetValue(name, out var factory))
                throw new InvalidOperationException(
                    $"[SkeuoRuntimeDriver] 未知 Screen 名「{name}」—— 闭集 = " +
                    string.Join(" / ", ScreenFactories.Keys) +
                    "(fail-loud:不 fallback 到别的屏,静默换屏 = 玩家看到错的东西)。");

            return factory(panel, new SkeuoElementLibrary());
        }

        /// <summary>确保 UIDocument 存在(场景未挂则运行期自建 —— ADR-023 ①)。
        /// <para>⚠️ **无 PanelSettings ⇒ 逻辑树在、渲染不在**(2026-10-10 实测:
        /// <c>rootVisualElement</c> 非 null 且可用,<c>rootVisualElement.panel == null</c>
        /// ⇒ 不画屏但树完整)。这正是单元测试要的形态;真渲染由编辑器给
        /// <see cref="_panelAsset"/> 赋值提供。<b>缺 PanelSettings 不是错误态</b>,
        /// 故此处不抛 —— 42 的呈现纪律与测试可达性各取所需。</para></summary>
        private void EnsureDocument()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null)
            {
                _uiDocument = gameObject.AddComponent<UIDocument>();
                if (_panelAsset != null)
                    _uiDocument.panelSettings = _panelAsset;
            }

            _panel = _uiDocument != null ? _uiDocument.rootVisualElement : null;
        }

        /// <summary>初始刷新(绑定当刻即读一次,不留「上一帧的空白」)。</summary>
        private void RefreshFromSource()
        {
            if (_screen is CasebookScreen casebook && _source != null)
                casebook.ApplyData(_source);
        }

        private void OnDestroy()
        {
            _screen = null;
            _panel = null;
            _source = null;
        }
    }
}
