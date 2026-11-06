// 病人视觉锚点 = **在场登记簿里的那一格**(2026-11-06 · P0 空间解)。
//
// ── 为什么要换掉 `RingPatientAnchorSource` ───────────────────────────────────
// 环实现的`TryGetAnchorCell` 把病人撒在**玩家周围 2.5 m 的圈**上 —— 玩家走到哪,
// 病人就跟到哪。而 4 的候选集读的是 `PresenceRegistry` 的**整数格**。两者一旦不同源,
// 「看得到的病人」和「选得中的病人」就是两件东西 ⇒ 走查无法判定「E 为什么选中了它」。
//
// 本类让**视觉锚点直接取登记簿的格**:看得到的 = 选得中的。
//
// ⚠️ 环实现**保留不删**(`RingPatientAnchorSource`):它仍是「病人位置来源可替换」这条
//   接缝的参考实现,且既有夹具直接构造它。本类只替换**生产装配**那一行。
//
// ⚠️ 真位置(6 世界生态区 + 13 病人 AI 空间侧)到位后,两者一并退役。

using System;
using DaYiJingCheng.Sim.Contracts;   // IPatientVisualAnchorSource / WorldPos

namespace DaYiJingCheng.Gameplay.Presentation.Patients
{
    /// <summary>视觉锚点 = 在场登记簿的整数格(禁读表现态位置,承 ADR-016 §三)。</summary>
    public sealed class PresenceCellAnchorSource : IPatientVisualAnchorSource
    {
        private readonly Func<int, WorldPos?> _cellOf;

        /// <param name="cellOf">按病人 id 取登记簿的格;`null` 或无登记 ⇒ 返回 `null`(该病人无锚点)。
        /// <para>用委托而不是 `IPresenceQuery`:后者**没有** `TryGetCell`(`out` 形参,
        /// 只在 `PresenceRegistry` 具体类上)⇒ 传委托可避免为一条读取而扩契约面。</para></param>
        public PresenceCellAnchorSource(Func<int, WorldPos?> cellOf)
        {
            _cellOf = cellOf ?? throw new ArgumentNullException(nameof(cellOf));
        }

        /// <inheritdoc/>
        public bool TryGetAnchorCell(int patientId, out WorldPos cell)
        {
            WorldPos? found = _cellOf(patientId);
            if (!found.HasValue)
            {
                cell = default;
                return false;
            }
            cell = found.Value;
            return true;
        }
    }
}
