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
            IsEmpty = !(view.Known && view.HasSave);
            // class 是**墨色**通道(空行格线 vs 有档墨迹),与文字通道相互独立 ——
            // 只有一条通道时,「格线色差」在低对比度设置下会单独失效。
            EnableInClassList("empty-row", IsEmpty);
            EnableInClassList("save-slot-item", !IsEmpty);
            _mark.text = RowText(view);
        }

        /// <summary>
        /// 有档行的行内文字:`行号 · 第N日`(§5.4 字段一 + 字段二的**降级形态**)。
        /// <para>⚠️ `DayNumber` 为 0(装配层没给 / 历法未就绪)⇒ 只写行号 ——
        /// 宁可少一个字段,也不拿 `Tick` 直接当日子显示(那会让玩家看到「第 1200 日」)。</para>
        /// </summary>
        private static string RowText(in SaveSlotView view)
        {
            if (!view.Known || !view.HasSave) return string.Empty;
            if (view.DayNumber <= 0L) return Numeral(view.SlotSeq);
            return Numeral(view.SlotSeq) + " · 第" + Numeral(view.DayNumber) + "日";
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
