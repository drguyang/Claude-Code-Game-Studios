// 42 侧的存档行视图模型(2026-11-03 甲方案第一层)。
//
// 权威来源:
//   design/ux/save-slots-7b.md §6 状态表(「未绑定」= `SaveSlotInfo` 列表未到达)
//   design/gdd/skeuomorphic-ui.md `AC-42-B5`(空位**可聚焦但零文本**)
//   story-012-save-slots.md
//
// 为什么是 42 自己的类型而不是直接吃 `Sim.Codec.SaveSlotSummary`:
//   42 的 `Gameplay.UI` 装配**不引用 `Sim.Codec`**(它只认 `Sim.Contracts` 与
//   `Gameplay.Presentation`)。装配层(`Gameplay.Boot`)两边都看得见,由它做映射 ——
//   与 `ISaveSlotCommandSink` 的注入方向同构(42 不反向引用装配层)。
//
// ⚠️ 本类型**只承载呈现量**,不进确定性字节面,不落流(ADR-013 §9 C3)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>存档册里一行的呈现状态。</summary>
    public readonly struct SaveSlotView
    {
        /// <summary>槽位号(1 起;「留档」新行不在此类型里)。</summary>
        public readonly uint SlotSeq;

        /// <summary>
        /// 装配层是否**真的探到**了这个槽的状态。
        /// <para><c>false</c> = 未绑定(7b §6 第一行):行照画、纸线格完整、**行字空白**。
        /// 探测失败(IO 错)与「忘了注入」在这个字段上同形 —— 两者都**不得**冒充空槽。</para>
        /// </summary>
        public readonly bool Known;

        /// <summary>这一槽里有没有档(<see cref="Known"/> 为 <c>false</c> 时无意义)。</summary>
        public readonly bool HasSave;

        /// <summary>存档头里的 tick(<see cref="HasSave"/> 为 <c>false</c> 时恒 0)。</summary>
        public readonly long Tick;

        /// <summary>
        /// 世界内第几日(<b>1 起</b>;<c>0</c> = 未定 / 无档)。
        /// <para>🔁 2026-11-03(甲方案第二层):由装配层从 <see cref="Tick"/> 算出 ——
        /// **42 不做除法**,除数与「+1」只有一个出处(系统 5 的 `Sim.World.TimeBase`,住 `Sim` 装配)。
        /// 42 只认这个已算好的值,因而不需要引用 `Sim`(它只引用 `Sim.Contracts`)。</para>
        /// <para>⚠️ 这是「册内时刻」的**降级形态**:spec §5.4 要的是「世界内日期 + 时段」,
        /// 而日→月/年 的历法映射归系统 5,今日 5 尚无代码 ⇒ 先只给「第 N 日」。</para>
        /// </summary>
        public readonly long DayNumber;

        /// <summary>
        /// 存档时刻是否在世界之**夜**(F-5.1 的 `isNight`;由装配层算,42 只呈现)。
        /// <para>⚠️ 今日「时段」只有**昼夜两档** —— 十二时辰在设计里不是机制读数
        /// (GDD `:90` 把历书/时辰一类归「史实层呈现」),故不在此发明。</para>
        /// </summary>
        public readonly bool IsNight;

        /// <summary>
        /// 该槽是否**已被读档锁住**(ADR-041 §D5 的「已锁槽行」)。
        /// <para>锁态归 7a(`SaveSlotLedger.IsLocked`,会话内存);42 只**呈现**。</para>
        /// <para>🔁 <b>2026-11-06(ADR-042 同批,用户裁定)锁改到写侧</b>:已锁 ⇒
        /// <b>不能覆写、仍可读</b>。呈现 = 整行**灰底** + 二段态里「启封」**墨字可用**、
        /// 「重写」**灰置不可获焦**。旧形态(启封灰、重写可用 = 读侧锁)已作废。</para>
        /// </summary>
        public readonly bool IsLocked;

        /// <summary>
        /// 这一页**只有备份**(主档没了或读不出)⇒ 行上标「前版」。
        /// <para>🔁 **2026-11-05(`TD-persist-22` 册子半边)**:7a 的 `SaveSlotSummary.Source` 分清
        /// 「主档在」与「只剩备份」,本字段只是把它搬到呈现面。**读者要知道的**是「我按下去
        /// 读到的是旧的那一份」—— 而不是文件叫什么名字。</para>
        /// </summary>
        public readonly bool FromBackup;

        /// <summary>
        /// 这一页**读不出来了**(主档与备份都解不出)⇒ 行上标「此页已损」。
        /// <para>与「未绑定」`Known=false` **不是一回事**:未绑定是「我不知道」(探测失败),
        /// 已损是「我探到了,这一页坏了」。两者在纸面上曾经同形(零字),现在分开。</para>
        /// <para>⚠️ 已损行按下去 = **落笔**(`HasSave=false` 的空槽语义):那一页已经读不出来,
        /// 复用它写入是正常出路;不把它做成「按下去必然失败」的死行。</para>
        /// </summary>
        public readonly bool Damaged;

        /// <summary>
        /// 该档**落笔那一刻**的真实世界时刻(UTC ticks;ADR-042 · v6 起)。
        /// <para>⚠️ <c>0</c> = <b>未知</b>(v5 及更早的档迁移而来)⇒ 行上**不显示时间**。
        /// 不得拿 tick / 文件时间顶替 —— 那是另一个量。</para>
        /// <para>呈现归 42(行末半字号),格式化也在 42 侧(本地时区转换只影响显示)。</para>
        /// </summary>
        public readonly long SavedAtUtcTicks;

        /// <summary>构造。</summary>
        public SaveSlotView(uint slotSeq, bool known, bool hasSave, long tick,
                            long dayNumber = 0L, bool isNight = false, bool isLocked = false,
                            bool fromBackup = false, bool damaged = false,
                            long savedAtUtcTicks = 0L)
        {
            SlotSeq = slotSeq;
            Known = known;
            HasSave = hasSave;
            Tick = tick;
            DayNumber = dayNumber;
            IsNight = isNight;
            IsLocked = isLocked;
            FromBackup = fromBackup;
            Damaged = damaged;
            SavedAtUtcTicks = savedAtUtcTicks;
        }

        /// <summary>未绑定形态(装配层没给状态,或探测失败)。</summary>
        public static SaveSlotView Unbound(uint slotSeq)
            => new SaveSlotView(slotSeq, known: false, hasSave: false, tick: 0L);
    }
}
