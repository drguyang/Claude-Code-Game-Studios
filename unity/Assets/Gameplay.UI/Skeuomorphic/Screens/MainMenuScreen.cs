// 主菜单屏(42 · **场景级屏,不是 ModalId 成员**)—— 世界装载之前的唯一入口。
//
// 权威来源:
//   · `design/ux/main-menu-42.md`(**2026-11-04 定稿形态:固定四行,与世界数无关**)
//   · `design/ux/save-slots-7b.md:189`(「继续诊治」= 存档位的正式入口之一)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发意图,不判「能不能继续」
//   · ADR-023 §①(三场景制:MainMenu 是独立场景;本屏是该场景里的 42 UI 根)
//   · ADR-040(多世界存档)—— 世界**目录**隔离仍在;**选择面**已按 2026-11-04 用户裁定收窄
//
// ⚠️ **它不是 `IModalState`**:意图上世界的装载先于本屏的消失,「挡住世界」这个语义不成立
//    (`BlocksPlay` / 规则七的门 / tick 冻结面全部以「世界已在跑」为前提)。
//
// 🔁 **2026-11-04 形态变更(用户裁定「无论多少世界,主菜单暂时固定三行,和世界多少无关」)**
//    —— 行集**恒定**,与世界数 / 存档数无关:
//      ① 继续诊治(无档 = 「开始诊治」)· ② 新起一局 · ③ 设置 · ④ 退出游戏
//    **已删除**:
//      · 世界列表行(`SetWorlds` / `MainMenuWorldView`)—— 旧形态「每个世界一行」;
//      · 「删一局」与整个删除态(`MainMenuMode` 三态 / 二次确认 / 返回行)。
//    ⚠️ **如实登记的后果**:游戏内**没有切换世界**与**删除世界**的入口 ⇒ 「继续诊治」恒进
//       **最近游玩的那个世界**,其余世界目录在游戏内**不可达**(见 ADR-040 的撤销注与债册)。
//       「多人局域网连接」行按同一条裁定**只登记位置、P0 不渲染**(联机归 P1b / 45)。
//    ⚠️ 行数是**契约**(`RowCount` 恒 4):屏级夹具与走查帧都按它断,不随世界数漂移。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>主菜单的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IMainMenuCommandSink
    {
        /// <summary>玩家确认了第一行(= 最近游玩的世界;没有任何世界时 = 开新局)。</summary>
        void Continue();

        /// <summary>玩家选了「新起一局」。</summary>
        void NewWorld();

        /// <summary>玩家选了「设置」—— 由装配层开设置壳(42 不自己换屏)。</summary>
        void OpenSettings();

        /// <summary>玩家选了「退出游戏」。</summary>
        void Quit();
    }

    /// <summary>主菜单(场景级屏)—— 一张纸,**固定四行**。</summary>
    public sealed class MainMenuScreen : PresentationRoot
    {
        /// <summary>有存档时的文案(spec §5 / §6:有档 ⇒「继续诊治」)。</summary>
        public const string ContinueText = "继续诊治";

        /// <summary>无存档时的文案(无档 ⇒「开始诊治」,行仍可用仍获焦)。</summary>
        public const string StartText = "开始诊治";

        /// <summary>「新起一局」的文案(ADR-040 D5 的入口)。</summary>
        public const string NewWorldText = "新起一局";

        /// <summary>「设置」的文案(开 `ModalId.SettingsShell`)。</summary>
        public const string ShellEntryText = "设置";

        /// <summary>「退出游戏」的文案。</summary>
        public const string QuitText = "退出游戏";

        /// <summary>行数(契约常值:与世界数 / 存档数无关)。</summary>
        public const int FixedRowCount = 4;

        /// <summary>
        /// 「上次会话未完整保存」的文案(`AC-7a-17` 后半句的原文措辞,不另造词)。
        /// <para>⚠️ 这是**文本**提示,不是音频播报 —— ADR-018 §六 / 7b 规则六禁的是后者。</para>
        /// </summary>
        public const string SessionIncompleteText = "上次会话未完整保存";

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private Label _notice;
        private readonly VisualElement _paper;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<Label> _rowLabels = new List<Label>();
        private IMainMenuCommandSink _sink;
        private bool _hasContinueSave;

        /// <summary>创建主菜单屏。</summary>
        public MainMenuScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;

            // ── 纯色背板(spec §5:世界未装载 ⇒ 纸面下是**纯色底**,不是游戏画面)──
            var backdrop = new VisualElement { name = "main-menu-backdrop" };
            backdrop.AddToClassList("menu-backdrop");
            backdrop.pickingMode = PickingMode.Ignore;
            _root.Add(backdrop);
            Backdrop = backdrop;

            var host = new VisualElement { name = "main-menu-host" };
            host.AddToClassList("menu-host");

            var paper = _library.Create(SkeuoElement.Paper);
            paper.name = "main-menu-paper";
            paper.AddToClassList("menu-column");
            host.Add(paper);
            _root.Add(host);

            _paper = paper;

            // ── 固定四行(2026-11-04 用户裁定)────────────────────────────
            // ⚠️ 行集恒定 ⇒ 本屏**没有重画路径**,焦点也不会因重建而悬空
            //    (旧世界列表/删除态那套 `_focusIndex` + `RestoreFocus` 随之删除)。
            NewRow("main-menu-continue", StartText, 0, () => _sink?.Continue());
            NewRow("main-menu-new", NewWorldText, 1, () => _sink?.NewWorld());
            NewRow("main-menu-settings", ShellEntryText, 2, () => _sink?.OpenSettings());
            NewRow("main-menu-quit", QuitText, 3, () => _sink?.Quit());
        }

        /// <summary>纯色背板(观测面:判据读它的不透明度 / 覆盖范围 / 与纸面的绘制先后)。</summary>
        public VisualElement Backdrop { get; }

        /// <summary>唯一落点(开屏时装配层 / 驱动落焦于此)—— 恒为第一行。</summary>
        public VisualElement FirstFocusable => _rows.Count > 0 ? _rows[0] : null;

        /// <summary>第一行的文案(观测面)。</summary>
        public string RowText => _rowLabels.Count > 0 ? _rowLabels[0].text : string.Empty;

        /// <summary>当前行数(观测面;**恒等于 <see cref="FixedRowCount"/>**)。</summary>
        public int RowCount => _rows.Count;

        /// <summary>第 <paramref name="index"/> 行的元件(观测面)。</summary>
        public VisualElement RowAt(int index) => _rows[index];

        /// <summary>第 <paramref name="index"/> 行的文案(观测面)。</summary>
        public string RowTextAt(int index) => _rowLabels[index].text;

        /// <summary>注入意图出口(未注入 ⇒ 行照画、按了留空:未接线的诚实形态)。</summary>
        public void BindCommandSink(IMainMenuCommandSink sink) => _sink = sink;

        /// <summary>
        /// 设定「有没有可继续的存档」⇒ 切换**第一行**文案。
        /// <para>⚠️ 只改文案,**不改可用性**:两种态下行都可用、都获焦(spec §6 已裁「无禁用态」)。</para>
        /// </summary>
        public void SetHasContinueSave(bool hasContinueSave)
        {
            _hasContinueSave = hasContinueSave;
            if (_rowLabels.Count > 0) _rowLabels[0].text = hasContinueSave ? ContinueText : StartText;
        }

        /// <summary>当前是否认为「有可继续的存档」(观测面)。</summary>
        public bool HasContinueSave => _hasContinueSave;

        private VisualElement NewRow(string name, string text, int index, System.Action activate)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("ruled");
            row.focusable = true;
            row.tabIndex = index;

            var label = new Label(text);
            row.Add(label);

            // 两条输入路径汇到同一出口(与 PauseScreen.Activate 同款纪律)
            row.RegisterCallback<NavigationSubmitEvent>(_ => ActivateRow(index));
            row.RegisterCallback<ClickEvent>(_ => ActivateRow(index));
            row.userData = activate;

            _paper.Add(row);
            _rows.Add(row);
            _rowLabels.Add(label);
            return row;
        }

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
        /// ③ 只有真有事要报时才存在。
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

        /// <summary>确认第一行(兼容旧调用点:手柄 Submit / 指针 Click 的汇合点)。</summary>
        public void Activate() => ActivateRow(0);

        /// <summary>确认第 <paramref name="index"/> 行(越界 = 无操作,不抛)。</summary>
        public void ActivateRow(int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            if (_rows[index].userData is System.Action action) action();
        }
    }
}
