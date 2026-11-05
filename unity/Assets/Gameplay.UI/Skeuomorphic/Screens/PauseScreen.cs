// 暂停壳(系统 42 自身)—— 存档位界面的**正式入口**。
//
// 权威来源:
//   · `design/ux/save-slots-7b.md:189`(存档位正式入口 = 暂停壳 / 主菜单「继续诊治」)
//   · ADR-013 **§十-C Amendment C**(2026-10-31 用户裁定 A:`ModalId.Pause` 第八员)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发**意图**,不判定
//
// ⚠️ 形状 = **最小可操作壳**:四行(继续 / 存档位 / 调治 / 退出游戏),零文字排版细节、零存档元数据。
//   🔁 **2026-11-04 用户裁定**:第三行「调治」= `ModalId.SettingsShell` 的**唯一入口**。
//   此前该屏**生产上打不开**(`TD-skeuo-04`),而 `settings-shell-42.md:54` 原写的两个入口
//   (主菜单「调治」/ 暂停壳内项)已被更晚的 Approved 件各自排除 —— 主菜单 `main-menu-42.md:85-86`
//   明写「不提供设置」,`OQ-PS-3`(2026-10-31)明写「只放继续和存档位」。
//   ⇒ 用户在此二选一中裁定取**暂停壳加第三行**,`OQ-PS-3` 自此被本裁定覆盖(原文留档)。
//   🔁 **2026-11-05 用户裁定(本轮)**:第四行「退出游戏」。
//   ↑ 上一版此处写「『退出』等其余项仍归 42 的暂停壳 UX 轮」—— 原文留档,本轮由用户裁定提前兑现。
//   语义**与主菜单第四行逐字同构**:`Application.Quit()`,退出保存走既有
//   `Application.wantsToQuit` 钩子(`BootRoot`),本屏与出口实现都**不写盘**。
//   触发缘由:玩家实测「按 ESC 只有继续/存档/调治,没有退出」—— 局内唯一退出手段是关窗口。
//   ⚠️ 退出是**不可逆的破坏性动作**,故它排在最后一行(焦点默认落第一行「继续」,不落它)。
//
// ⚠️ 行用 `.ruled` 类:与脉案行同一声明块 ⇒ 44px 下限 + 格线,不需要新样式表规则
//   (AB-3 的焦点落点高半已由该块保证)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>暂停壳的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IPauseCommandSink
    {
        /// <summary>继续(合上暂停壳)。</summary>
        void Resume();

        /// <summary>打开存档位界面(7b 的正式入口)。</summary>
        void OpenSaveSlots();

        /// <summary>打开设置壳(`ModalId.SettingsShell` 的**唯一入口**,2026-11-04 用户裁定)。</summary>
        void OpenSettings();

        /// <summary>
        /// 一键落笔(2026-11-05 用户裁定 —— 参照英灵神殿第二层「保存」行)。
        /// <para>返回 <c>true</c> = **已受理**(已入写队列),<c>false</c> = 门拒了 / 未接线。</para>
        /// <para>⚠️ 目标槽**不由 42 决定**(台账的 `NextWritableSeq`);屏只发意图。</para>
        /// </summary>
        bool RequestSave();

        /// <summary>
        /// 退出游戏(2026-11-05 用户裁定 —— 暂停壳第四行)。
        /// <para>⚠️ **不可逆**:实现方须先落盘再退,但**写盘归 7a**(`Application.wantsToQuit`
        /// 钩子),本接口只表达意图。与主菜单第四行**同一个出口语义**。</para>
        /// </summary>
        void Quit();
    }

    /// <summary>暂停壳(ModalId.Pause)—— 平面拟物 UI,手柄走查。</summary>
    public sealed class PauseScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly VisualElement _host;
        private readonly VisualElement[] _rows;

        /// <summary>创建暂停壳。</summary>
        public PauseScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            _host = new VisualElement { name = "pause-list" };
            _rows = new VisualElement[5];
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Pause;

        /// <summary>注入意图出口(未注入 ⇒ 行照画、按了留空:未接线的诚实形态)。</summary>
        public void BindCommandSink(IPauseCommandSink sink) => _sink = sink;

        private IPauseCommandSink _sink;

        /// <summary>打开时应落焦的第一行。</summary>
        public VisualElement FirstFocusable => _rows[0];

        /// <summary>行数(观测面)。</summary>
        public int RowCount => _rows.Length;

        /// <summary>取某一行(观测面;越界 ⇒ null)。</summary>
        public VisualElement RowAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rows.Length ? _rows[rowIndex] : null;

        /// <summary>确认某一行(两条输入路径的唯一汇合点)。</summary>
        /// <param name="rowIndex">0 = 继续,1 = 保存,2 = 存档位,3 = 调治,4 = 退出游戏;越界 ⇒ 忽略。</param>
        public void Activate(int rowIndex)
        {
            if (rowIndex == 0) _sink?.Resume();
            else if (rowIndex == SaveRowIndex)
            {
                bool accepted = _sink != null && _sink.RequestSave();
                if (accepted) { _saveHint.text = "已落笔"; _savePending = true; }
                else ReportSaveRejected();
            }
            else if (rowIndex == 2) _sink?.OpenSaveSlots();
            else if (rowIndex == 3) _sink?.OpenSettings();
            else if (rowIndex == 4) _sink?.Quit();
        }

        /// <summary>落笔待落定(驱动在写盘落定后调 <see cref="RefreshSaveHint"/> 前用)。</summary>
        private bool _savePending;

        /// <summary>是否有一次「保存」在等落定(观测面)。</summary>
        public bool SavePending => _savePending;

        /// <summary>落笔已落定(由驱动在写队列排空后调;下一句是重读年龄)。</summary>
        public void CompletePendingSave() => _savePending = false;

        /// <summary>该行的文案(<c>null</c> = 越界)。文案权威 = `pause-shell-42.md` §5。</summary>
        private static readonly string[] RowLabels = { "继续", "保存", "存档位", "调治", "退出游戏" };

        /// <summary>该行的元件名(观测面 / 夹具定位;与行序一一对应)。</summary>
        private static readonly string[] RowNames =
            { "pause-resume", "pause-save", "pause-save-slots", "pause-settings", "pause-quit" };

        /// <summary>「保存」行号(0 起)。</summary>
        private const int SaveRowIndex = 1;

        /// <summary>
        /// 「上次保存」的年龄(分钟;null 或负数 = 不知道)。
        /// <para>🔁 **2026-11-05(参照英灵神殿第二层「保存」行)**:本值**只用于呈现** ——
        /// 墙钟不进存档头 / 三流 / sim(与 ADR-040 §D4「墙钟只用于选择面」同一条边界)。</para>
        /// </summary>
        public Func<int> LastSaveAgeMinutes { get; set; }

        /// <summary>「保存」行的副标题(观测面;主标题在同行的 Label 里)。</summary>
        public string SaveHintText => _saveHint?.text ?? string.Empty;

        private void BuildUI()
        {
            // ── 纸面 = 行的**容器**(不是兄弟)—— 兑现 `pause-shell-42.md` §5 的 ASCII:
            //    ┌──────┐ 纸面
            //    │ 继续  │
            //    └──────┘
            //    ⚠️ 首版把纸面当**流内兄弟**加在行前面 ⇒ 空元素高度只有 padding+border(实测 40px),
            //    两行则透明地压在世界暗面上(字与底同色系,几乎读不出)。2026-10-31 走查实拍缺陷。
            // ── 列宽:行若铺满整个面板,贴在行内的焦点环就会被拉成横贯全屏的铜带
            //    (运行时 dump:行 1680×57,环同尺寸)。窄列 + 居中由 USS `.pause-column` 锁。
            var paper = _library.Create(SkeuoElement.Paper);
            paper.name = "pause-paper";
            _host.AddToClassList("pause-column");
            _host.Add(paper);
            _root.Add(_host);

            for (int i = 0; i < _rows.Length; i++)
            {
                int rowIndex = i;
                var row = new VisualElement { name = RowNames[i] };
                row.AddToClassList("ruled");
                row.focusable = true;
                row.tabIndex = i;
                // ⚠️ **文字必须由本类写**(2026-10-31 走查实测的缺陷):首版两行只挂了 `.ruled`
                //    的空元素,**没有任何 Label** ⇒ 行在、可点、可获焦,但玩家**什么都看不见**
                //    (走查三张截图里一次都没看见暂停壳,只看见急救候选列表)。
                //    文案权威 = `design/ux/pause-shell-42.md` §5
                //    (继续 / 保存 / 存档位 / 调治 / 退出游戏),不可在别处改写。
                row.Add(new Label(RowLabels[i]));
                // 🔁 2026-11-05:「保存」行多一枚副标题(上次保存多久之前)。
                //    参照英灵神殿第二层 —— 玩家在按之前就该知道「我上次存是多久前」。
                if (i == SaveRowIndex)
                {
                    _saveHint = new Label { name = "pause-save-hint" };
                    _saveHint.AddToClassList("pause-save-hint");
                    row.Add(_saveHint);
                }
                // 手柄经官方焦点桥给 NavigationSubmitEvent;指针给 ClickEvent
                // (ADR-011:不自实现焦点算法)。
                row.RegisterCallback<NavigationSubmitEvent>(_ => Activate(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => Activate(rowIndex));
                _rows[i] = row;
                paper.Add(row);
            }

            RefreshSaveHint();
        }

        private Label _saveHint;

        /// <summary>
        /// 重读「上次保存」年龄并重写副标题(由驱动在开壳 / 落笔落定后调用)。
        /// <para>⚠️ 与「存档册重探」同一条教训:它**不得**挂在 tick 边沿上 —— 暂停壳摊开时
        /// 世界是冻结的(tick 不推进),挂在 tick 上的刷新永远等不到。</para>
        /// </summary>
        public void RefreshSaveHint()
        {
            if (_saveHint == null) return;
            int age = LastSaveAgeMinutes != null ? LastSaveAgeMinutes() : -1;
            _saveHint.text = age < 0
                ? "尚未保存"
                : age == 0 ? "刚刚保存" : $"上次保存：{age} 分钟前";
        }

        /// <summary>
        /// 落笔被拒时的如实回报(返回 <c>false</c> = 门拒了,**不是**「还没落盘」)。
        /// <para>🔁 2026-11-05:与存档册「点了没反应」同源的缺陷形态 —— 失败必须说出来,
        /// 否则玩家区分不了「按了没生效」与「界面坏了」。</para>
        /// </summary>
        public void ReportSaveRejected()
        {
            if (_saveHint != null) _saveHint.text = "未能落笔（当前不宜存盘）";
        }
    }
}
