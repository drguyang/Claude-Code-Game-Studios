// 主菜单屏(42 · **场景级屏,不是 ModalId 成员**)—— 世界装载之前的唯一入口。
//
// 权威来源:
//   · `design/ux/main-menu-42.md`(2026-11-01 Approved;**2026-11-04 修订**:单行 → 世界列表 + 新起)
//   · `design/ux/save-slots-7b.md:189`(「继续诊治」= 存档位的正式入口之一)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发意图,不判「能不能继续」
//   · ADR-023 §①(三场景制:MainMenu 是独立场景;本屏是该场景里的 42 UI 根)
//   · ADR-040(多世界存档)—— 世界列表 = 本屏的行;**选择本身发生在装配之前**
//
// ⚠️ **它不是 `IModalState`**:意图上世界的装载先于本屏的消失,「挡住世界」这个语义不成立
//    (`BlocksPlay` / 规则七的门 / tick 冻结面全部以「世界已在跑」为前提)。
//
// 🔁 **2026-11-04 形态变更(ADR-040 D9)**:原「一张纸、一行」改为:
//    · **没有世界** ⇒ 一行「开始诊治」(= 新建第一个世界;与旧形态逐字相同);
//    · **有世界**   ⇒ 每个世界一行(最近游玩的那行文案「继续诊治」,其余「第 N 局」)
//                     + 末行「新起一局」。
//    行数因此**可变** —— 旧判据「子树内文本元素个数恰为 1」不再成立,已改为「行数与世界数一致」。
//    ⚠️ 「第 N 局」用的是**世界序号**,不是游玩时长/日期:存档摘要(`OQ-MM-6`)仍未裁,
//       本屏**不得**自造日期文案(那需要 7a 的槽位元数据源,归后续轮)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>主菜单上「选择一个世界」的呈现形(42 侧 DTO;装配层从 7a 的目录表映射过来)。</summary>
    public readonly struct MainMenuWorldView
    {
        /// <summary>世界序号(≥ 1;`WorldCatalog` 的目录名后缀)。</summary>
        public readonly int Seq;

        /// <summary>该行的文案(装配层给的**成品文案**,42 不拼字符串)。</summary>
        public readonly string Label;

        /// <summary>构造。</summary>
        public MainMenuWorldView(int seq, string label)
        {
            Seq = seq;
            Label = label;
        }
    }

    /// <summary>主菜单的形态(观测面 + 内部状态;ADR-040 D6 的删除态只用 Submit 一路输入)。</summary>
    public enum MainMenuMode
    {
        /// <summary>正常:世界行 + 新起一局 + 删一局。</summary>
        Normal = 0,

        /// <summary>删除态:选一个世界(第一次按 = 选目标)。</summary>
        DeleteSelect = 1,

        /// <summary>删除态:已选目标,同一行**再按一次**才真删。</summary>
        DeleteConfirm = 2,
    }

    /// <summary>主菜单的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IMainMenuCommandSink
    {
        /// <summary>玩家确认了第一行(= 最近游玩的世界;没有任何世界时 = 开新局)。</summary>
        void Continue();

        /// <summary>玩家选了某个具体世界(含第一行以外的世界)。</summary>
        /// <param name="seq">世界序号。</param>
        void SelectWorld(int seq);

        /// <summary>玩家选了「新起一局」。</summary>
        void NewWorld();

        /// <summary>玩家在删除态里**二次确认**了某个世界(ADR-040 D6:真删)。</summary>
        /// <param name="seq">世界序号。</param>
        void DeleteWorld(int seq);
    }

    /// <summary>主菜单(场景级屏)—— 一张纸,按世界数决定行数。</summary>
    public sealed class MainMenuScreen : PresentationRoot
    {
        /// <summary>有存档时的文案(spec §5 / §6:有档 ⇒「继续诊治」)。</summary>
        public const string ContinueText = "继续诊治";

        /// <summary>无存档时的文案(无档 ⇒「开始诊治」,行仍可用仍获焦)。</summary>
        public const string StartText = "开始诊治";

        /// <summary>「新起一局」的文案(ADR-040 D5 的入口)。</summary>
        public const string NewWorldText = "新起一局";

        /// <summary>「删一局」的文案(ADR-040 D6 的入口)。</summary>
        public const string DeleteWorldText = "删一局";

        /// <summary>删除态里「返回」的文案。</summary>
        public const string BackText = "返回";

        /// <summary>
        /// 「上次会话未完整保存」的文案(`AC-7a-17` 后半句的原文措辞,不另造词)。
        /// <para>⚠️ 这是**文本**提示,不是音频播报 —— ADR-018 §六 / 7b 规则六禁的是后者。</para>
        /// </summary>
        public const string SessionIncompleteText = "上次会话未完整保存";

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly Label _label;                    // 旧单行形态的标签(行 0 的 Label)
        private Label _notice;
        private readonly VisualElement _paper;
        private readonly VisualElement _row;              // 旧单行形态的行(行 0)
        private readonly VisualElement _backdrop;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<Label> _rowLabels = new List<Label>();
        private IMainMenuCommandSink _sink;
        private bool _hasContinueSave;

        /// <summary>装配层推来的世界表(重画时要用;删除态也在它之上重建)。</summary>
        private IReadOnlyList<MainMenuWorldView> _worlds = Array.Empty<MainMenuWorldView>();

        /// <summary>当前形态(正常 / 选删除目标 / 确认删除)。</summary>
        private MainMenuMode _mode = MainMenuMode.Normal;

        /// <summary>删除态里已选中的世界序号(<c>-1</c> = 未选)。</summary>
        private int _pendingDeleteSeq = -1;

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

            _row.RegisterCallback<NavigationSubmitEvent>(_ => ActivateRow(0));
            _row.RegisterCallback<ClickEvent>(_ => ActivateRow(0));
            // 旧单行形态(未调用 `SetWorlds` 的调用点)的确认动作 = Continue:
            // 「有档继续 / 无档开新局」由装配层判定(spec §6),42 只发意图。
            _row.userData = (Action)(() => _sink?.Continue());
            paper.Add(_row);

            _paper = paper;
            _rows.Add(_row);
            _rowLabels.Add(_label);
        }

        /// <summary>纯色背板(观测面:判据读它的不透明度 / 覆盖范围 / 与纸面的绘制先后)。</summary>
        public VisualElement Backdrop => _backdrop;

        /// <summary>唯一落点(开屏时装配层 / 驱动落焦于此)—— 恒为第一行。</summary>
        public VisualElement FirstFocusable => _rows.Count > 0 ? _rows[0] : null;

        /// <summary>第一行的文案(观测面;兼容旧单行形态)。</summary>
        public string RowText => _rowLabels.Count > 0 ? _rowLabels[0].text : string.Empty;

        /// <summary>当前行数(观测面:无世界 1 行;有 N 个世界 ⇒ N + 1 行)。</summary>
        public int RowCount => _rows.Count;

        /// <summary>第 <paramref name="index"/> 行的元件(观测面)。</summary>
        public VisualElement RowAt(int index) => _rows[index];

        /// <summary>第 <paramref name="index"/> 行的文案(观测面)。</summary>
        public string RowTextAt(int index) => _rowLabels[index].text;

        /// <summary>注入意图出口(未注入 ⇒ 行照画、按了留空:未接线的诚实形态)。</summary>
        public void BindCommandSink(IMainMenuCommandSink sink) => _sink = sink;

        /// <summary>
        /// 设定「有没有可继续的存档」⇒ 切换**第一行**文案(仅单行形态下可见)。
        /// <para>⚠️ 只改文案,**不改可用性**:两种态下行都可用、都获焦(spec §6 已裁「无禁用态」)。</para>
        /// </summary>
        public void SetHasContinueSave(bool hasContinueSave)
        {
            _hasContinueSave = hasContinueSave;
            if (_rows.Count == 1) _rowLabels[0].text = hasContinueSave ? ContinueText : StartText;
        }

        /// <summary>当前是否认为「有可继续的存档」(观测面)。</summary>
        public bool HasContinueSave => _hasContinueSave;

        /// <summary>
        /// 按世界列表重画行(ADR-040 D9)。
        /// <para>`worlds` 为空 ⇒ 退回单行「开始诊治」;非空 ⇒ 每个世界一行 + 末行「新起一局」。</para>
        /// <para>⚠️ 第一行文案恒为「继续诊治」(它是最近游玩的世界);其余为装配层给的
        /// <see cref="MainMenuWorldView.Label"/>。42 **不拼**这些文案。</para>
        /// </summary>
        public void SetWorlds(IReadOnlyList<MainMenuWorldView> worlds)
        {
            _worlds = worlds ?? Array.Empty<MainMenuWorldView>();
            // 世界列表一变,删除态就作废(列表可能已经没有那一行了)。
            _mode = MainMenuMode.Normal;
            _pendingDeleteSeq = -1;
            // 没有世界 ⇒ 一定没有可继续的存档(删光之后那句「继续诊治」必须跟着消失)。
            if (_worlds.Count == 0) _hasContinueSave = false;
            Rebuild();
        }

        /// <summary>当前形态(观测面)。</summary>
        public MainMenuMode Mode => _mode;

        /// <summary>删除态的待确认世界序号(<c>-1</c> = 无)。</summary>
        public int PendingDeleteSeq => _pendingDeleteSeq;

        /// <summary>进入删除态(观测面 + 装配层的兜底入口;正常由「删一局」行进入)。</summary>
        public void EnterDeleteMode()
        {
            if (_worlds.Count == 0) return;          // 没有世界可删
            _mode = MainMenuMode.DeleteSelect;
            _pendingDeleteSeq = -1;
            Rebuild();
        }

        /// <summary>退出删除态,回到正常列表。</summary>
        public void CancelDelete()
        {
            _mode = MainMenuMode.Normal;
            _pendingDeleteSeq = -1;
            Rebuild();
        }

        private void Rebuild()
        {
            // 拆掉旧行(连同它们的回调),只留背板 / 宿主 / 纸面。
            for (int i = 0; i < _rows.Count; i++) _rows[i].RemoveFromHierarchy();
            _rows.Clear();
            _rowLabels.Clear();

            if (_worlds.Count == 0)
            {
                // 单行形态:与旧版逐字相同(没有世界 ⇒ 开新局)。
                // ⚠️ 文案走 `SetHasContinueSave` 的同一套规则(无档「开始诊治」),
                //    但动作是 **NewWorld** —— 没有世界时「继续」无从谈起。
                NewRow("main-menu-continue", StartText, 0, () => _sink?.NewWorld());
                _rowLabels[0].text = _hasContinueSave ? ContinueText : StartText;
                return;
            }

            if (_mode != MainMenuMode.Normal)
            {
                // ── 删除态(ADR-040 D6)──────────────────────────────────────
                // 只用 Submit 一路输入:没有新增动作 / 没有新模态屏(闭集不动)。
                for (int i = 0; i < _worlds.Count; i++)
                {
                    MainMenuWorldView w = _worlds[i];
                    int seq = w.Seq;
                    bool armed = _mode == MainMenuMode.DeleteConfirm && _pendingDeleteSeq == seq;
                    string text = armed
                        ? "再按一次确认 · " + w.Label
                        : "删除 " + w.Label;
                    NewRow("main-menu-delete-" + seq, text, i, () => OnDeleteRowActivated(seq));
                }

                NewRow("main-menu-delete-back", BackText, _rows.Count, CancelDelete);
                return;
            }

            for (int i = 0; i < _worlds.Count; i++)
            {
                MainMenuWorldView w = _worlds[i];
                int seq = w.Seq;
                string text = i == 0 ? ContinueText : w.Label;
                NewRow("main-menu-world-" + seq, text, i,
                       i == 0 ? (Action)(() => _sink?.Continue()) : () => _sink?.SelectWorld(seq));
            }

            NewRow("main-menu-new", NewWorldText, _rows.Count, () => _sink?.NewWorld());
            // 「删一局」只在**有世界**时出现 —— 没有世界时这一行没有宾语。
            NewRow("main-menu-delete", DeleteWorldText, _rows.Count, EnterDeleteMode);
        }

        /// <summary>
        /// 删除态里按下某一行:第一次 = 选目标(进入待确认),**再按同一行** = 真删。
        /// <para>按别的行 = 换目标(重新进入待确认),不删任何东西 —— 这是「二次确认」的本体:
        /// 两次都得落在**同一个世界**上。</para>
        /// </summary>
        private void OnDeleteRowActivated(int seq)
        {
            if (_mode == MainMenuMode.DeleteConfirm && _pendingDeleteSeq == seq)
            {
                _sink?.DeleteWorld(seq);
                return;   // 装配层删完会回推新的世界列表(`SetWorlds`),届时自动回到正常态
            }

            _mode = MainMenuMode.DeleteConfirm;
            _pendingDeleteSeq = seq;
            Rebuild();
        }

        private VisualElement NewRow(string name, string text, int index, Action activate)
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
            if (_rows[index].userData is Action action) action();
        }
    }
}
