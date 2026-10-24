// story-004 装配轮 · 2026-10-24 —— `death_cell` 的流前缀查询。
//
// 权威来源:
//   design/registry/entities.yaml:2169 —— `PlayerDied` 的 payload_schema 逐字:
//     「death_cell = 最后一条 `ActorCellEntered` 之 cell 原样携带(派生态,禁读实时物理位置)」
//   design/gdd/death-and-respawn.md —— F-29 系列(死亡结算的格锚点)
//   ADR-020 §四 —— 玩家位移 = 纯表现态;`death_cell` **禁读实时物理位置**
//   ADR-016 §三 —— 感知输入 = 粗粒度整数格(读方对称)
//   ADR-015 §三 —— `WorldPos` 单一整数格
//
// ── 为什么需要这个类型 ────────────────────────────────────────────────────
// `PlayerDiedWriter.TryWrite` 的 `deathCell` 形参**由调用方给出**,而它自己
// 自陈「本类不反查流」。`DeathCandidateConsumer` 也只收一个 `Func<int, WorldPos>`。
// ⇒ 「从流里取最后一条 `ActorCellEntered` 的 cell」这件事实**没有落点** ——
// 装配层若内联手写,就是同一判据的第二份实现(而它是要被 story-005 复活传送
// 与 story-002 掉落锚点共用的)。本类把它收成**唯一的流前缀纯函数**。
//
// ── 空集语义:返回「未找到」,不返回哨兵坐标 ───────────────────────────────
// 玩家若从未跨过格(`ActorCellEntered` 零条),就没有可原样携带的格。
// 此时**不**返回 `WorldPos(0,0,0)` —— 那是一个真实存在的格(世界原点),
// 会伪造一个「死在那里」的结论。⇒ 返回 `false`,由调用方决定(装配层在未找到时
// 退回落点 = 该 actor 的 `DiseaseOnset`? 不 —— 玩家没有 onset;
// 见 `TryLastCellOf` 的 remarks 与装配层注释)。
// 🔁 2026-10-24 订正:`<see cref="DeathCellQuery.LastCellOf"/>` 指向一个**不存在**的成员
// (本类只有 `TryLastCellOf`)—— 编译期只是 CS1574 警告,不拦构建 ⇒ 改为纯文本引用。
//
// ⚠️ 本类**只读流**,零状态、零缓存(与 <see cref="CaseStreamQuery"/> /
//    <see cref="FoldedPredicate"/> 同族)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// `death_cell` 的流前缀查询 —— 「该 actor 最后一条 <c>ActorCellEntered</c> 的格」。
    /// </summary>
    public static class DeathCellQuery
    {
        /// <summary>
        /// 取该 actor 在世界流里**最后一条** <c>ActorCellEntered</c> 的格。
        /// </summary>
        /// <param name="events">事件流只读视图(可传全流 —— 本方法按 <c>Kind</c> 自筛;
        /// <c>Kind → StreamId</c> 是纯函数且 A1 断言唯一流别,ADR-024)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点 —— <c>Sim</c> 读载荷的唯一合法路径)。</param>
        /// <param name="actorId">受伤实体 id(非负)。</param>
        /// <param name="cell">出口:最后一条跨格事件的格;**未找到时不动它**(调用方不得依赖)。</param>
        /// <returns>该 actor 是否存在至少一条 <c>ActorCellEntered</c>。</returns>
        /// <remarks>
        /// <para><b>「最后一条」按流序,不按 tick 排序</b>:流本身按 <c>Tick</c> 非降是写入期
        /// 硬不变量(<c>EventStream</c> 的 BCD-码-3 断言)⇒ 流序最后一条即 tick 最大者。
        /// 另排一次 = 同一事实的第二份判据。</para>
        /// <para><b>为什么归到 <c>Sim</c></b>:它是 ADR-005「状态从流重算」的纯谓词,
        /// 与冷却门(<see cref="DeathCooldownGate"/>)成对;引用集仍恰 = {BCL, <c>Sim.Contracts</c>}。</para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorId"/> &lt; 0。</exception>
        public static bool TryLastCellOf(
            IReadOnlyList<SimEvent> events, IPayloadDecoder decoder, int actorId, out WorldPos cell)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "跨格查询的 actor 不得为负 —— PatientId.None(-1)是「世界级事件」哨兵,不是可定位实体");

            cell = default;
            if (events == null) return false;

            bool found = false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ActorCellEntered)
                    continue;

                ActorCellEnteredPayload payload =
                    decoder.Decode<ActorCellEnteredPayload>(EventKind.ActorCellEntered, e.Payload);

                if (payload.ActorId != actorId)
                    continue;

                cell = payload.Cell;   // 流序最后一条胜(流按 Tick 非降)
                found = true;
            }

            return found;
        }
    }
}
