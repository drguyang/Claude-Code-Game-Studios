// 权威来源:design/gdd/skeuomorphic-ui.md AC-42-F1②(存档位界面手柄走查)
//   · design/ux/save-slots-7b.md §5.3 / §5.4(登记行元件 · 行内字段)
//   · production/epics/skeuomorphic-ui/story-012-save-slots.md
//
// 设计说明:
//   · 存档槽位项 = 拟物元件(纸面);空槽 = 空行有格线无字(AC-42-B5)。
//   · 存档元数据归 7a/7b;42 只渲染。
//
// 🔁 **2026-11-03 行内文字轮**(甲方案第一层):
//   此前本元件是**裸 `VisualElement`**,一个字都没有 ⇒ 屏上四条行长得一样,
//   用户 smoke 实测「存档和读档看不出差异」。本轮补三件:
//   ① 槽位行有**行号**(§5.4 字段一,汉字序);
//   ② 空槽与未绑定**零字**(AC-42-B5 逐字);有档的行才有字 —— 这就是可辨识的那一面;
//   ③ 「留档」新行自带字样,与槽位行**形态可分**(玩家要能看出这一行是「再写一行」)。
//   ⚠️ 仍未做:册内时刻 / 摘要(§5.4 字段二/三)—— 那是第二层,要 tick→历法映射。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using UnityEngine.UIElements;

    /// <summary>存档槽位项(AC-42-F1②)—— 逐槽渲染单元。</summary>
    public sealed class SaveSlotItem : VisualElement
    {
        /// <summary>行内文字的元件名(观测面)。</summary>
        public const string MarkElementName = "slot-mark";

        private readonly Label _mark;

        /// <summary>槽位序号(1 起;「留档」新行 = -1)。</summary>
        public int SlotIndex { get; }

        /// <summary>是否为空槽(空槽渲染格线无字)。</summary>
        public bool IsEmpty { get; set; } = true;

        /// <summary>行内文字(观测面;空串 = 零字)。</summary>
        public string MarkText => _mark.text;

        /// <summary>创建存档槽位项。</summary>
        /// <remarks>
        /// 🔁 2026-10-31:补 `focusable = true` —— 此前行**不可获焦**,
        /// 于是 42 的焦点桥(官方 `FocusController`,ADR-011/ADR-013)根本走不到存档位,
        /// 手柄玩家按不动任何一行。<c>tabIndex</c> 由 <c>SaveSlotsScreen</c> 按行序赋。
        /// ⚠️ 尺寸不在这里给:`min-height` 归 USS(`.empty-row` / `.save-slot-item`
        /// 同一声明块,AB-3 的 44 下限)—— 零高的元素即使可获焦也点不中。
        /// </remarks>
        public SaveSlotItem(int slotIndex)
        {
            SlotIndex = slotIndex;
            AddToClassList(IsEmpty ? "empty-row" : "save-slot-item");
            focusable = true;
            _mark = new Label { name = MarkElementName };
            _mark.AddToClassList("save-slot-mark");
            Add(_mark);
        }

        /// <summary>
        /// 按装配层给的视图刷新这一行。
        /// <para>⚠️ 未绑定(<see cref="SaveSlotView.Known"/> = false)与空槽**都**画零字 ——
        /// 前者是「我不知道」,后者是「这一槽没写过」,两者在纸面上都是**没写字的一格**;
        /// 分别在语义,不在墨迹(AC-42-B5「可聚焦但零文本」)。</para>
        /// </summary>
        public void Apply(in SaveSlotView view)
        {
            // ⚠️ 行被重新填充 ⇒ 二段态作废(两枚墨字所指的是**上一份**行文字的槽,
            //   留着会指向一个已经变了的页)。
            ExitChoice();
            // ⚠️ 已损行**不是**空行:它有字(「此页已损」),用有档的墨色通道画,
            //    否则玩家看到的是「格线 + 一句话」的组合,比空槽还怪。
            IsEmpty = !(view.Known && view.HasSave) && !view.Damaged;
            // class 是**墨色**通道(空行格线 vs 有档墨迹),与文字通道相互独立 ——
            // 只有一条通道时,「格线色差」在低对比度设置下会单独失效。
            EnableInClassList("empty-row", IsEmpty);
            EnableInClassList("save-slot-item", !IsEmpty);
            _mark.text = RowText(view);
            _noticeShown = false;   // 重探 = 提示作废(与二段态同一条纪律)
        }

        /// <summary>
        /// 有档行的行内文字:`行号 · 第N日`(§5.4 字段一 + 字段二的**降级形态**)。
        /// <para>⚠️ `DayNumber` 为 0(装配层没给 / 历法未就绪)⇒ 只写行号 ——
        /// 宁可少一个字段,也不拿 `Tick` 直接当日子显示(那会让玩家看到「第 1200 日」)。</para>
        /// </summary>
        private static string RowText(in SaveSlotView view)
        {
            // 🔁 2026-11-05(`TD-persist-22` 册子半边):**已损**行有字(它既不是空槽,
            //    也不是「我不知道」)。行号 + 一句状态 —— 玩家据此知道这一页要重写。
            if (view.Damaged) return Numeral(view.SlotSeq) + " · 此页已损";

            if (!view.Known || !view.HasSave) return string.Empty;
            // 时段只在装配层**真的给了日子**时才画 —— `DayNumber == 0` 表示时间信息未就绪,
            // 此时若仍写「昼」就会凭空断言一个没算过的值(默认 false 正好等于「昼」,
            // 那是**静默**的错)。宁可不写。
            string text = view.DayNumber <= 0L
                ? Numeral(view.SlotSeq)
                : Numeral(view.SlotSeq) + " · 第" + Numeral(view.DayNumber) + "日" +
                  (view.IsNight ? " 夜" : " 昼");

            // 只剩备份 ⇒ 尾缀「前版」。玩家要看清按下去读到的是**旧的那一份**。
            // 🔁 **2026-11-05(用户裁定甲案)**:已锁槽尾缀「已锁」——
            //    此前锁只影响「启封」可不可按(`ArmRow` 的 `openDisabled`),**零文字面**
            //    ⇒ 玩家点了没反应、屏上什么都不说、只有日志里有半句
            //    (`槽位 slot=N 已锁(AC-7a-19 (d) 读档即锁)`,实测一局连撞五次)。
            //    锁是**会自解**的(下一次叙事锚点),所以文案只陈述状态,不写「永不可读」。
            // ⚠️ 文案(`前版` / `此页已损` / `已锁`)归 narrative 内容轮,此处只定「有三处口子」。
            if (view.IsLocked) text += " · 已锁";
            return view.FromBackup ? text + " · 前版" : text;
        }

        private bool _noticeShown;

        /// <summary>
        /// 已锁行的**一次性提示**(🔁 2026-11-05 用户实测:「点已有存档 → 选读取 → 没有重新进入游戏」)。
        /// <para>⚠️ 真因不是「没反应」,是更坏的形态:已锁槽的「启封」被灰置并退出焦点序
        /// ⇒ 二段态的焦点落到**「重写」** ⇒ **一次确认就覆写这个槽**。
        /// ⇒ 已锁行**不再进二段态**(`SaveSlotsScreen.Activate` 在进入前就拦),
        /// 改为在行上说明原因;本方法只负责把那句话写出来。</para>
        /// <para>提示是**一次性的**:下一次重探(`Apply`)自动作废,不会越叠越长。</para>
        /// </summary>
        public void ShowLockedNotice(string notice)
        {
            if (_noticeShown || _mark == null || string.IsNullOrEmpty(notice)) return;
            _noticeShown = true;
            _mark.text = _mark.text + " · " + notice;
        }

        /// <summary>「启封」墨字的元件名(观测面)。</summary>
        public const string OpenChoiceName = "slot-open";

        /// <summary>「重写」墨字的元件名(观测面)。</summary>
        public const string RewriteChoiceName = "slot-rewrite";

        private VisualElement _choiceHost;
        private VisualElement _openChoice;
        private VisualElement _rewriteChoice;
        private System.Action _onOpen;
        private System.Action _onRewrite;

        /// <summary>本行是否正处于「二段态」(行内已出示两枚墨字)。</summary>
        public bool IsArmed => _choiceHost != null;

        /// <summary>「启封」墨字(<c>null</c> = 未进入二段态;观测面)。</summary>
        public VisualElement OpenChoice => _openChoice;

        /// <summary>「重写」墨字(<c>null</c> = 未进入二段态;观测面)。</summary>
        public VisualElement RewriteChoice => _rewriteChoice;

        /// <summary>
        /// 进入**行内二段态**(ADR-041 §D5):两枚可焦点墨字「启封」/「重写」**与行文字同行靠右**。
        /// **零系统弹窗** —— 这是 7b 的幻想纪律(`AC-7b-07`),先例 = 主菜单删除世界的行内二次确认。
        /// <para>🔁 **2026-11-04 走查修订(用户指令「三个都改」)**:首版在此把行文字
        /// `display: none` 抹掉 —— 实拍(`wb_02_armed` / `wb_03_locked`)暴露出真正的问题:
        /// **玩家在按下不可逆的「重写」时,屏上已看不到要盖的是哪一页**。改法 = 行文字保留,
        /// 两枚墨字压到同一行的右侧(`SkeuoPaper.uss` 的 `.save-slot-choice` 一族)。
        /// 副作用(顺带解掉)= 二段态**不再让行高增长**,满页容量与行文字都不受它影响。</para>
        /// </summary>
        /// <param name="onOpen">「启封」= 请求载入(<c>TryLoad</c>)。</param>
        /// <param name="onRewrite">「重写」= 请求覆写(<c>TrySaveAt</c>)。</param>
        /// <param name="openDisabled">该槽已锁(读档即锁)⇒ 「启封」灰置且**不可获焦**
        /// —— 焦点必须落到仍可执行的那一枚上,否则手柄玩家按下去又是「没反应」。</param>
        public void EnterChoice(System.Action onOpen, System.Action onRewrite, bool openDisabled)
        {
            ExitChoice();

            _onOpen = onOpen;
            _onRewrite = onRewrite;

            _choiceHost = new VisualElement { name = "slot-choice" };
            _choiceHost.AddToClassList("save-slot-choice");

            _openChoice = MakeChoice(OpenChoiceName, "启封", () => ActivateOpen());
            _rewriteChoice = MakeChoice(RewriteChoiceName, "重写", () => ActivateRewrite());

            // 灰置 = 墨色通道(`.save-slot-choice-disabled` 走 `--skeuo-ink-faded`)
            //   + **退出焦点序**(关闭模式的手柄玩家不得停在一条按不出结果的墨字上)。
            // ⚠️ 2026-11-04 走查修订前这个类名是**死类**(USS 零规则,灰只有引擎默认半透明),
            //   现已在 `SkeuoPaper.uss` 补齐 —— 类名与其执行体同批存在。
            _openChoice.SetEnabled(!openDisabled);
            _openChoice.focusable = !openDisabled;
            if (openDisabled) _openChoice.AddToClassList("save-slot-choice-disabled");

            _choiceHost.Add(_openChoice);
            _choiceHost.Add(_rewriteChoice);
            Add(_choiceHost);
        }

        /// <summary>「启封」的**执行体**(事件与夹具共用的唯一出口)。</summary>
        public void ActivateOpen() => _onOpen?.Invoke();

        /// <summary>「重写」的**执行体**(事件与夹具共用的唯一出口)。</summary>
        public void ActivateRewrite() => _onRewrite?.Invoke();

        /// <summary>退出二段态(墨字合回,行复原)。幂等。</summary>
        public void ExitChoice()
        {
            if (_choiceHost == null) return;
            _choiceHost.RemoveFromHierarchy();
            _choiceHost = null;
            _openChoice = null;
            _rewriteChoice = null;
            _onOpen = null;
            _onRewrite = null;
        }

        /// <summary>建一枚墨字(两条输入路径 —— 手柄 Submit / 指针 Click —— 汇到同一出口)。</summary>
        private static VisualElement MakeChoice(string name, string text, System.Action activate)
        {
            var element = new VisualElement { name = name };
            element.AddToClassList("save-slot-choice-glyph");
            element.focusable = true;
            var label = new Label(text);
            element.Add(label);
            element.RegisterCallback<NavigationSubmitEvent>(_ => activate?.Invoke());
            element.RegisterCallback<ClickEvent>(_ => activate?.Invoke());
            return element;
        }

        /// <summary>
        /// 把「留档」新行标成与槽位行可分的形态(字样 + 独立 class)。
        /// <para>⚠️ 它**不是**槽位行:没有槽号(<see cref="SlotIndex"/> = -1),不计入行数。</para>
        /// </summary>
        public SaveSlotItem AsWritableRow()
        {
            IsEmpty = false;
            EnableInClassList("empty-row", false);
            EnableInClassList("save-slot-item", false);
            AddToClassList("save-slot-writable");
            _mark.text = "留档";
            return this;
        }

        /// <summary>
        /// 槽位号的汉字序(§5.4 字段一:「行号 = 汉字序(陆/柒…),`slot_seq` 的呈现映射」)。
        /// <para>1–10 用大写数字;<b>超过 10 退回十进制串</b> —— 大写数词的十位写法
        /// (拾壹 / 贰拾…)是中文数字方案问题,归本地化素材轮,本类不擅自发明。
        /// 今日屏上只有 1–3。</para>
        /// </summary>
        public static string Numeral(uint slotSeq) => Numeral((long)slotSeq);

        /// <summary>同上,long 重载(世界日数用它)。</summary>
        public static string Numeral(long value)
        {
            switch (value)
            {
                case 1: return "壹";
                case 2: return "贰";
                case 3: return "叁";
                case 4: return "肆";
                case 5: return "伍";
                case 6: return "陆";
                case 7: return "柒";
                case 8: return "捌";
                case 9: return "玖";
                case 10: return "拾";
                default: return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
