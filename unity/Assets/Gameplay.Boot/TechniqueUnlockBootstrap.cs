// 技法**起步集** —— 建世界时一次性写入(ADR-043 §D9 · 2026-11-09 · T3)
//
// 权威:docs/architecture/adr-043-technique-unlock-kind.md §D9
//   · 判据 = **事件流为空**(新世界)。写完流非空 ⇒ 读档不会重发 ⇒ 幂等
//   · 内容 = 「你怀里那本手册教的三样」(campaign-arc.md:42-51 的 止血 / 固定 / 通气)
//   · **不做成数据里的 `starting: true` 标志** —— 那会造第二真源(同一事实既在烘焙数据、
//     又在事件流,可分叉)。事件流第一天就是唯一真源。
//
// ⚠️ **值归内容轮**:下面的清单是 T3 落地所需的最小可用值,不是内容定稿。
//    换成正式内容时**只改这张表**,机制不动。归属 = T6/T7(手册页与内容轮)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>新世界的技法起步集写入器。</summary>
    public static class TechniqueUnlockBootstrap
    {
        /// <summary>来源 id = 序章《应急救护手册》。**内容值**,归内容轮。</summary>
        public const int SourcePrologueManual = 1;

        /// <summary>
        /// 起步技法 id(= 处置轴 action id)。
        /// <para>0 = 止血包扎(`OQ-10-4` 已裁的 P0 四动作之一)。</para>
        /// <para>⚠️ **2026-11-10(A3-b,用户裁定「从起步集移出」)**:原为 <c>{0, 1}</c>,
        /// 本批移除 <b>1 = 节奏型通气</b>。理由:它在投放面上**覆盖 0 行**(T11 实测 ——
        /// 没有任何病种把它列进 <c>treatable_by</c>),玩家开局拿到的是一个**永远用不上的动作**,
        /// 属纯负体验。移出后它成为待解锁技法。</para>
        /// <para>⚠️ <b>移出起步集**不解决**那条零覆盖缺口</b> —— 缺口门的判据是
        /// 「零覆盖集 == 豁免键集」(<c>technique_side_gap_gate_test</c> ①),与起步集无关;
        /// 技法 1 仍须留在 <see cref="TechniqueCoverageExemptions"/> 里,直到 A2 给它挂上病种。
        /// 原豁免理由里「把它从起步集移出 ⇒ 落地 ⇒ 删本行」那句是错的,已同批订正。</para>
        /// <para>⚠️ <b>P0 后果(如实登记)</b>:A5 的解锁规则行尚未落地 ⇒ 技法 1 在 P0 <b>不可获得</b>。
        /// 这是本裁定的直接代价,不是缺陷;A5 落地后由 Case / Event 源规则授予。</para>
        /// <para>⚠️ **内容值,归内容轮** —— 改它不影响任何机制判据。</para>
        /// </summary>
        public static readonly int[] StartingTechniques = { 0 };

        /// <summary>若这是新世界(流为空)则写起步集;**否则 no-op**。</summary>
        /// <param name="events">事件流只读视图(判「新世界」的唯一依据)。</param>
        /// <param name="writer">解锁写者(唯一 Append 落点)。</param>
        /// <param name="actorId">玩家 id(玩家是建世界后才有的 —— 故本方法在 BootRoot 调用)。</param>
        /// <param name="tick">当前 tick。</param>
        /// <returns>本次写出的事件条数(读档 / 已写过 ⇒ 0)。</returns>
        /// <exception cref="ArgumentNullException">events / writer 为 null。</exception>
        public static int Ensure(IReadOnlyList<SimEvent> events, TechniqueUnlockWriter writer,
                                 int actorId, long tick)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            // ── D9 判据:流非空 ⇒ 不是新世界 ⇒ 什么都不做 ──
            if (events.Count != 0) return 0;

            int written = 0;
            for (int i = 0; i < StartingTechniques.Length; i++)
                if (writer.TryUnlock(actorId, StartingTechniques[i], SourcePrologueManual, tick))
                    written++;
            return written;
        }
    }
}
