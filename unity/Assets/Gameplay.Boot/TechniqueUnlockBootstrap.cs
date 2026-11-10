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
        /// <para>0 = 止血包扎 · 1 = 节奏型通气。</para>
        /// <para>🔁 <b>2026-11-10 当日两次裁定,第二次推翻第一次 —— 因为第一次的理由被后来的裁定消掉了</b>:</para>
        /// <list type="number">
        /// <item><b>A3-b(移出技法 1)</b>:理由是它在投放面上**覆盖 0 行**(T11 实测),
        /// 玩家开局拿到一个永远用不上的动作。⇒ `{0,1}` → `{0}`。</item>
        /// <item><b>A2(给技法 1 挂病种)</b>:同批把 `DIS_FEBRILE_SEIZURE` / `DIS_HEART_FAILURE`
        /// 挂给 `action 1` ⇒ 它**不再零覆盖**。</item>
        /// <item><b>本条(回补技法 1)</b>:A2 让 A3-b 的**前提消失**了。留在 `{0}` 的后果是
        /// 「一门有 2 个病种的技法,P0 里永远学不到」—— 而唯一能解锁它的规则
        /// (`ForEvent(2003)`)压在 52 的死链后面(`TD-randomevents-05` / ADR-039 §R3)。
        /// 这不是「撤销 A3-b」,是**它赖以成立的那条事实已经不在**。</item>
        /// </list>
        /// <para><b>直接后果</b>:开局可投放病种由 **0** 变成 **2**(热性惊厥 / 心衰)——
        /// R1 的「≥3」仍差一门,但不再是「开局一个病都没有」。</para>
        /// <para>⚠️ **内容值,归内容轮** —— 改它不影响任何机制判据(它只影响开局给哪几门)。</para>
        /// </summary>
        public static readonly int[] StartingTechniques = { 0, 1 };

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
