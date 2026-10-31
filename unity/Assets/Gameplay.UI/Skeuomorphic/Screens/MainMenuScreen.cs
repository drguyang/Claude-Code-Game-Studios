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

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly Label _label;
        private readonly VisualElement _row;
        private IMainMenuCommandSink _sink;
        private bool _hasContinueSave;

        /// <summary>创建主菜单屏。</summary>
        public MainMenuScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;

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
        }

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

        /// <summary>确认这一行(手柄 Submit / 指针 Click 的唯一汇合点)。</summary>
        public void Activate() => _sink?.Continue();
    }
}
