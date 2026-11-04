// 主菜单屏(42 · **场景级屏,不是 ModalId 成员**)—— 世界装载之前的唯一入口。
//
// 权威来源:
//   · `design/ux/main-menu-42.md`(2026-11-01 Approved;§1 逐字论证「为什么不得进闭集」)
//   · `design/ux/save-slots-7b.md:189`(「继续诊治」= 存档位的正式入口之一)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发意图,不判「能不能继续」
//   · ADR-023 §①(三场景制:MainMenu 是独立场景;本屏是该场景里的 42 UI 根)
//
// ⚠️ **它不是 `IModalState`**:意图上世界的装载先于本屏的消失,「挡住世界」这个语义不成立
//    (`BlocksPlay` / 规则七的门 / tick 冻结面全部以「世界已在跑」为前提)。
//
// ⚠️ **文案由**一个布尔**决定**(`SetHasContinueSave`)—— 有档「继续诊治」/ 无档「开始诊治」;
//    该布尔的所有者是 7a(装配层注入),本屏**不扫描存档目录**(spec §10 所有者纪律)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>主菜单的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IMainMenuCommandSink
    {
        /// <summary>玩家确认了唯一那一行:装载世界(有档继续 / 无档开新局,由装配层判定)。</summary>
        void Continue();
    }

    /// <summary>主菜单(场景级屏)—— 一张纸、一行。</summary>
    public sealed class MainMenuScreen : PresentationRoot
    {
        /// <summary>有存档时的文案(spec §5 / §6:有档 ⇒「继续诊治」)。</summary>
        public const string ContinueText = "继续诊治";

        /// <summary>无存档时的文案(无档 ⇒「开始诊治」,行仍可用仍获焦)。</summary>
        public const string StartText = "开始诊治";

        /// <summary>
        /// 「上次会话未完整保存」的文案(`AC-7a-17` 后半句的原文措辞,不另造词)。
        /// <para>⚠️ 这是**文本**提示,不是音频播报 —— ADR-018 §六 / 7b 规则六禁的是后者。</para>
        /// </summary>
        public const string SessionIncompleteText = "上次会话未完整保存";

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly Label _label;
        /// <summary>
        /// 「上次会话未完整保存」那一行 —— **按需创建**。
        /// <para>⚠️ 懒建不是性能考虑,是**不变量**:spec §5「一张纸、一行」的判据按
        /// 「子树内文本元素个数」数(`test_bootWithMenu_showsSingleRow_andLandsFocus`),
        /// 常驻一个隐藏 Label 会让它在**没有任何东西要报**的常态下也变成 2 个。</para>
        /// </summary>
        private Label _notice;
        private readonly VisualElement _paper;
        private readonly VisualElement _row;
        private readonly VisualElement _backdrop;
        private IMainMenuCommandSink _sink;
        private bool _hasContinueSave;

        /// <summary>创建主菜单屏。</summary>
        public MainMenuScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;

            // ── 纯色背板(spec §5:世界未装载 ⇒ 纸面下是**纯色底**,不是游戏画面)──
            // ⚠️ 走 **42 侧全屏背板**而不是相机 `clearFlags = SolidColor`:菜单期启动序第 5 步
            //    的那个相机**还没建出来**(它在 World 装载之后),此时渲染的是场景里预摆的
            //    占位相机 —— 改它要么改不到、要么在关屏后留下一台被改过清屏色的相机。
            // ⚠️ 色值 = 调色板既有 token `--skeuo-implement-bg`(2026-11-01 用户裁定复用,
            //    故**不新增任何字面色值**);背板因此对美术轮的调色板改动自动跟随。
            // ⚠️ `pickingMode = Ignore`:背板不参与拾取,点它不挡下面的行。
            var backdrop = new VisualElement { name = "main-menu-backdrop" };
            backdrop.AddToClassList("menu-backdrop");
            backdrop.pickingMode = PickingMode.Ignore;
            _root.Add(backdrop);
            _backdrop = backdrop;

            var host = new VisualElement { name = "main-menu-host" };
            host.AddToClassList("menu-host");

            var paper = _library.Create(SkeuoElement.Paper);
            paper.name = "main-menu-paper";
            paper.AddToClassList("menu-column");
            host.Add(paper);
            _root.Add(host);

            _row = new VisualElement { name = "main-menu-continue" };
            _row.AddToClassList("ruled");
            _row.focusable = true;
            _row.tabIndex = 0;

            _label = new Label(StartText);
            _row.Add(_label);

            // 两条输入路径汇到同一出口(与 PauseScreen.Activate 同款纪律)
            _row.RegisterCallback<NavigationSubmitEvent>(_ => Activate());
            _row.RegisterCallback<ClickEvent>(_ => Activate());
            paper.Add(_row);

            // 「上次会话未完整保存」那一行**不在这里建** —— 见 `EnsureNotice` 的注释:
            // 常态(标志位为假)下纸面上必须仍然只有一行。
            _paper = paper;
        }

        /// <summary>纯色背板(观测面:判据读它的不透明度 / 覆盖范围 / 与纸面的绘制先后)。</summary>
        public VisualElement Backdrop => _backdrop;

        /// <summary>唯一落点(开屏时装配层 / 驱动落焦于此)。</summary>
        public VisualElement FirstFocusable => _row;

        /// <summary>当前文案(观测面)。</summary>
        public string RowText => _label.text;

        /// <summary>注入意图出口(未注入 ⇒ 行照画、按了留空:未接线的诚实形态)。</summary>
        public void BindCommandSink(IMainMenuCommandSink sink) => _sink = sink;

        /// <summary>
        /// 设定「有没有可继续的存档」⇒ 切换文案。
        /// <para>⚠️ 只改文案,**不改可用性**:两种态下行都可用、都获焦(spec §6 已裁「无禁用态」)。</para>
        /// </summary>
        public void SetHasContinueSave(bool hasContinueSave)
        {
            _hasContinueSave = hasContinueSave;
            _label.text = hasContinueSave ? ContinueText : StartText;
        }

        /// <summary>当前是否认为「有可继续的存档」(观测面)。</summary>
        public bool HasContinueSave => _hasContinueSave;

        /// <summary>
        /// 按 7a 的标志位决定是否摊开「上次会话未完整保存」。
        /// <para>⚠️ **只呈现,不判断** —— 标志位的置位/清除全归 7a(`SaveIncompleteFlag`);
        /// 42 不扫描存档目录、不读标志文件(ADR-013 §9 C3)。</para>
        /// </summary>
        public void SetSessionIncompleteNotice(bool raised)
        {
            if (raised) EnsureNotice();

            if (_notice != null)
                _notice.style.display = raised ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 建出提示行(幂等)。三条纪律与「非阻塞」逐字对应:
        /// ① `focusable = false` ⇒ 不进焦点序,不抢 <see cref="FirstFocusable"/>;
        /// ② `pickingMode = Ignore` ⇒ 点它不挡下面的行;
        /// ③ 只有真有事要报时才存在 ⇒ 常态纸面上仍是「一行」。
        /// </summary>
        private void EnsureNotice()
        {
            if (_notice != null) return;

            _notice = new Label(SessionIncompleteText) { name = "main-menu-incomplete-notice" };
            _notice.AddToClassList("menu-notice");
            _notice.focusable = false;
            _notice.pickingMode = PickingMode.Ignore;
            _paper.Add(_notice);
        }

        /// <summary>提示当前是否摊开(观测面)。</summary>
        public bool SessionIncompleteNoticeShown
            => _notice != null && _notice.style.display.value == DisplayStyle.Flex;

        /// <summary>提示元件(观测面;<c>null</c> = 尚未建 —— 常态形态)。</summary>
        public Label SessionIncompleteNotice => _notice;

        /// <summary>确认这一行(手柄 Submit / 指针 Click 的唯一汇合点)。</summary>
        public void Activate() => _sink?.Continue();
    }
}
