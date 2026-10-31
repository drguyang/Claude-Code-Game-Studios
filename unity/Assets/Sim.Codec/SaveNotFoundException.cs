// 7a · 「这个槽根本没有存档」≠「这个槽的存档坏了」—— 两个态的类型边界。
//
// 权威来源:
//   · design/gdd/persistence-service.md AC-7a-15(b)(final 与 bak **都坏** ⇒ Corrupt 态)
//   · design/gdd/save-slot-ui.md 规则三 / 规则四(7b 只呈现,判定在 7a)
//   · ADR-010 §四(损坏恢复:两级都坏 ⇒ 提示损坏)
//
// ── 为什么必须把这两件事分开(2026-10-31 走查实测)──────────────────
// 首版 `LoadBytes` 把「**final 与 bak 文件都不存在**」和「**都校验失败**」并成一类,
// 统一抛 `SaveCorruptException`。后果在真实走查里发生了:玩家点了一个**从没存过**的空槽,
// 控制台打出红色的「存档损坏且无法回退」——**拒绝载入是对的,话是错的**,
// 而且这句话会让玩家以为自己丢了档。
//
// ⇒ 两条判据的语义本就不同:
//   · 「没有」= 正常态(新游戏每个槽都空)—— 呈现应是「此页空白」,日志级别 = 信息;
//   · 「坏了」= 异常态(AC-7a-15(b))—— 呈现应是报损坏,日志级别 = 错误。
//   态的边界在**类型上**可表达,下游就不必靠字符串猜(与 `SaveCorruptException` 头注同一纪律)。
//
// ⚠️ **本类不改变 AC-7a-15(b) 的任何行为** ——「两级都坏」仍走 `SaveCorruptException`;
//    本类只接管**两级都不存在**那一支。两者都**零 I/O**:不建空档、不删残档。
//
// ⚠️ **父类 = `Exception`**,理由与 `SaveCorruptException` 逐字相同(不选 `IOException`:
//    它会被可重试兜底静默吞掉)。两个异常**互不继承** —— 「没有」不是「坏了」的一种。

using System;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 目标槽**没有任何存档**:<c>final</c> 与 <c>bak</c> 两个文件都不存在。
    /// <para>这是**正常态**,不是异常态 —— 新游戏每个槽都是这个状态。调用方应据此
    /// 呈现「此页空白」,**不得**按损坏报错(那会让玩家以为自己的档坏了)。</para>
    /// <para>⚠️ 与 <see cref="SaveCorruptException"/> 的区别 = **磁盘上有没有东西**:
    /// 至少一个文件存在但校验不过 ⇒ 那个是损坏;两个都不存在 ⇒ 本异常。</para>
    /// </summary>
    public sealed class SaveNotFoundException : Exception
    {
        /// <summary>没有存档的那个槽位序号。</summary>
        public uint SlotSeq { get; }

        /// <summary>构造。</summary>
        /// <param name="slot">两级文件均不存在的槽位。</param>
        public SaveNotFoundException(SaveSlot slot)
            : base($"该槽没有任何存档:final 与 bak 均不存在(slot={slot.SlotSeq})——" +
                   "正常态(空槽),不是损坏;不建空档、不改磁盘")
        {
            SlotSeq = slot.SlotSeq;
        }
    }
}
