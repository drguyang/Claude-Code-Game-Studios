// 玩家「本 tick 跨格」的**单一出处** —— 从事件流读出 (上一格 → 本格) 的配对(2026-11-11 · T22 批五)。
//
// ── 为什么单独成件 ──────────────────────────────────────────────────────────
// 这段判定原本**只**活在 `HouseCallStartedWriter.TryStartAtTick` 的 ②/③ 两步里。批五要把
// 「离开医馆」这一同一时刻复用给**延迟威胁的补发**(GDD 规则六之三「当玩家离开医馆地块
// (当日之内)→ 立即补发」;`TD-randomevents-08` 的解除动作 ② 逐字要求「与
// `HouseCallStartedWriter` **共用同一次跨格判定**」)。
//
// 照抄一遍会造出**第二份判据**:两处对「前一格怎么取 / 同 tick 多条怎么办」的写法
// 一旦分叉,同一次跨格会得到两种结论,而**两边各自的夹具都会绿**(假绿形态 F8:
// 行为等价掩盖结构分叉)。故抽成纯函数,两边各调它一次。
//
// ── 判据(与 `HouseCallStartedWriter` 原实现逐字同源)──────────────────────────
//   · 本格 = 本 tick 内**该玩家**最后一条 `ActorCellEntered`(每 tick 每玩家至多一条;
//     多条时**流序最后一条胜**,与原实现同)。
//   · 上一格 = **tick 严格小于本 tick** 的、该玩家的最后一条 `ActorCellEntered`
//     (流序最后一条胜 —— 流按 Tick 非降)。**无前序格 ⇒ 无从构成「跨出」**。
//   · 本件只回答「读到什么」;**「算不算跨出医馆」由调用方配 `IClinicCellSource` 判**
//     (`CrossedOutOf`)—— 判据源同源义务(三处绑同一实现)不在本件。
//
// ⚠️ **本件不持状态、不写流**:纯读。跨格事件本身由 1 侧按 ADR-020 §四 写(每跨一格一条)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>玩家在某个 tick 的跨格读数(上一格 / 本格)。</summary>
    public readonly struct PlayerCellCrossing
    {
        /// <summary>本 tick 是否有该玩家的跨格事件。</summary>
        public readonly bool HasCurrent;

        /// <summary>本格(仅 <see cref="HasCurrent"/> 为真时有意义)。</summary>
        public readonly WorldPos CurrentCell;

        /// <summary>是否存在**更早**的跨格事件(无 ⇒ 无从构成「跨出」)。</summary>
        public readonly bool HasPrevious;

        /// <summary>上一格(仅 <see cref="HasPrevious"/> 为真时有意义)。</summary>
        public readonly WorldPos PreviousCell;

        private PlayerCellCrossing(bool hasCurrent, in WorldPos current, bool hasPrevious, in WorldPos previous)
        {
            HasCurrent = hasCurrent;
            CurrentCell = current;
            HasPrevious = hasPrevious;
            PreviousCell = previous;
        }

        /// <summary>
        /// 本 tick 是否构成「**从医馆格跨出到非医馆格**」(GDD 规则十二 12.1 / 规则六之三)。
        /// </summary>
        /// <param name="clinicCells">医馆格判据源(**必须与服务袋同一实例** —— 同源义务)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="clinicCells"/> 为 null。</exception>
        public bool CrossedOutOf(IClinicCellSource clinicCells)
        {
            if (clinicCells == null) throw new ArgumentNullException(nameof(clinicCells));
            return HasCurrent && HasPrevious
                   && clinicCells.IsClinicCell(PreviousCell)
                   && !clinicCells.IsClinicCell(CurrentCell);
        }

        /// <summary>
        /// 从事件流读出该玩家在本 tick 的跨格读数。
        /// </summary>
        /// <param name="events">事件流只读视图(全序;追加型)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点)。</param>
        /// <param name="playerActorId">玩家实体 id(跨格事件按此筛)。</param>
        /// <param name="tick">本 tick。</param>
        /// <exception cref="ArgumentNullException">events / decoder 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public static PlayerCellCrossing At(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
                                            int playerActorId, long tick)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            WorldPos current = default;
            bool hasCurrent = false;
            WorldPos previous = default;
            bool hasPrevious = false;

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ActorCellEntered) continue;

                // 早于本 tick 的只可能是「上一格」候选;晚于本 tick 的尚未发生(防御性跳过)。
                if (e.Tick > tick) continue;

                ActorCellEnteredPayload p =
                    decoder.Decode<ActorCellEnteredPayload>(EventKind.ActorCellEntered, e.Payload);
                if (p.ActorId != playerActorId) continue;

                if (e.Tick == tick)
                {
                    current = p.Cell;      // 流序最后一条胜(与原实现逐字同)
                    hasCurrent = true;
                }
                else
                {
                    previous = p.Cell;     // 同理:更早的最后一条
                    hasPrevious = true;
                }
            }

            return new PlayerCellCrossing(hasCurrent, in current, hasPrevious, in previous);
        }
    }
}
