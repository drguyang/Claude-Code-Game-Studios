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

        /// <summary>要实例化的 Screen 名(闭集见 <see cref="CreateScreen"/>)。</summary>
        [SerializeField] private string _screenName = "Casebook";

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

        private VisualElement _panel;
        private IPresentationRoot _screen;

        /// <summary>屏幕名 → Screen 查表(七个具名载体;名字是**序列化契约**,
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
            _screen = CreateScreen(_screenName, _panel);
            if (_screen == null)
            {
                Debug.LogError($"[SkeuoRuntimeDriver] Screen '{_screenName}' not found.");
                return;
            }

            // ── T2.1:DTO 绑定(标记接口 IDtoSource 的 42 侧实现) ──
            // 写通道若先于 Awake 赋值(场景序列化顺序不定),此处补绑一次(幂等)
            if (_commandSink != null && _screen is CasebookScreen preBound)
                preBound.BindCommandSink(_commandSink);

            if (Source != null)
            {
                _screen.Bind(Source);
                RefreshFromSource();
            }
            else
            {
                // 缺源 = 留空(空行),**不造假数据**(ADR-013 C3 + story-024 边界③)。
                Debug.LogWarning("[SkeuoRuntimeDriver] ICasebookDtoSource 未注入 —— " +
                                 "脉案值区留空(未查 = 空行,未编造读数)。");
            }

            Debug.Log($"[SkeuoRuntimeDriver] Screen '{_screenName}' instantiated and bound.");
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

        /// <summary>已实例化的 Screen(测试观测面)。</summary>
        public IPresentationRoot Screen => _screen;

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
        }

        /// <summary>
        /// 挂载拟物样式表(编辑器清单 ⇒ <c>rootVisualElement.styleSheets</c>)。
        /// <para>零清单 = LogWarning 后继续(逻辑树仍可用 —— 测试面);但**渲染缺全部形态**。</para>
        /// <para>面板未建(panel == null)时跳过:无面板则样式表无处挂,不是错误态。</para>
        /// </summary>
        private void AttachStyleSheets()
        {
            if (_panel == null || _panel.panel == null)
                return;   // 无渲染面板(测试环境)—— 逻辑树不需要样式表

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

            // ── 根级字体默认(2026-10-13 · B-6-4 字体糊根治)──
            // 运行期 C# 建的裸 `new Label(text)` **零类** ⇒ 落 Unity 默认字体(中文 fallback
            // = 糊);只有挂 `.channel-label` 的题签清晰(实测 13 Label: 7 清晰 / 6 糊)。
            // UI Toolkit 的 `-unity-font-definition` **可继承**(探针实测:祖先设一次 ⇒
            // 13/13 全清晰)⇒ 在 panel root 挂 `.skeuo-root`,该类在 SkeuoThemeVariables.uss
            // 设 SDF 字体,全体后代继承 —— 一处生效,未来新文本自动覆盖,不可能再漏挂。
            _panel?.AddToClassList(SkeuoRootClass);
        }

        /// <summary>根级默认字体类(承 <c>SkeuoThemeVariables.uss</c> 的 <c>.skeuo-root</c> 规则)。
        /// <para>由 <see cref="EnsureDocument"/> 挂在 panel root 上;字体经 UI Toolkit 的
        /// <c>-unity-font-definition</c> 继承下传,覆盖全部裸文本(2026-10-13 B-6-4)。</para></summary>
        public const string SkeuoRootClass = "skeuo-root";

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
