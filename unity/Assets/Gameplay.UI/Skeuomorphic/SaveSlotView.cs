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

        /// <summary>构造。</summary>
        public SaveSlotView(uint slotSeq, bool known, bool hasSave, long tick)
        {
            SlotSeq = slotSeq;
            Known = known;
            HasSave = hasSave;
            Tick = tick;
        }

        /// <summary>未绑定形态(装配层没给状态,或探测失败)。</summary>
        public static SaveSlotView Unbound(uint slotSeq)
            => new SaveSlotView(slotSeq, known: false, hasSave: false, tick: 0L);
    }
}
