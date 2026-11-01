// S23-2 · story-002 —— 死亡掉落的**回捡约束**(20 R7 的 Sim 面;2026-11-01)
//
// ── 它解的是什么 ────────────────────────────────────────────────────────────
// AC-29-17(20 的 R7 兑现):「回捡必须走到死亡格,**禁远程拾取**」。判据 = **整数格**
// (Chebyshev ≤ 宽容半径),不是连续坐标 —— 所以它住得进 `Sim`(门 A),
// 与 `DropEventWriter` 头注登记项 ①「不判拾取距离 —— 判距吃表现态位置,归 4」**不冲突**:
// 那条说的是 4 的**交互判距**(float);本类是 29 死亡掉落的**格级回捡约束**(int),
// 两层各管各的 —— 4 的管线将来落地时,格级闸(本类)与判距(4)串联。
//
// ── 锚点真源 ────────────────────────────────────────────────────────────────
// 实例的「在地上哪格」= 世界流里该实例**最近一次** `DropSpawned` 的 `spawn_anchor`
// (派生态,重放可建 —— 不持任何注册表/快照)。倒序扫 = 取流序最后一条;同 id 的
// 「消耗 → 再落地」序列(死亡散落)天然取到最新锚。实例已归属(owner ≠ NoOwner)⇒
// 拒(幂等:同实例二次 claim 的拒收半边 —— fold 的 DuplicateClaim 缺口是机械兜底,
// 本类是行为前置);流里查无出生 ⇒ 拒(fold 会报 DanglingInstanceReference,此处零动作)。
//
// ⚠️ **宽容半径是注入形参,不是常量** —— 实值归 4 的装配轮 / 数值轮(承 AC「实值归数值轮」);
// 测试用符号化值。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 死亡掉落回捡判据:实例未归属 ∧ 流里有出生锚 ∧ 玩家格在锚点宽容半径内。
    /// </summary>
    public static class DeathDropReclaimPolicy
    {
        /// <summary>
        /// 该玩家当前格能否拾取该死亡掉落实例。
        /// </summary>
        /// <param name="events">事件流只读视图(锚点真源;**可为三流并集**,域外 Kind 自动跳过)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="projection">库存投影(归属面;派生态,调用方重取)。</param>
        /// <param name="instanceId">掉落实例 id。</param>
        /// <param name="actorCell">玩家**经流确立格**(`ActorCellEntered` 之 cell,ADR-020 §四 ——
        /// 禁读表现态连续位置)。</param>
        /// <param name="tolerance">宽容半径(整数格,Chebyshev;实值归 4 装配轮/数值轮)。</param>
        /// <returns>true = 允许 claim(调用方经 <c>DropEventWriter.RecordClaimed</c> 入流);
        /// false = 拒(**零事件** —— 已归属 / 无出生锚 / 出半径)。</returns>
        public static bool CanClaim(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
            InventoryProjection projection, long instanceId, WorldPos actorCell, int tolerance)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (tolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance,
                    "宽容半径 ≥ 0(负值属畸形输入)");

            // ① 归属面:已归属(或已归零出投影)⇒ 拒 —— 幂等半边(二次 claim 拒收)。
            if (!projection.TryGet(instanceId, out InventoryRow row)
                || row.Owner != InventoryProjection.NoOwner)
                return false;

            // ② 锚点面:该实例最近一次 `DropSpawned`(倒序取流序最后一条)。
            //    有界性 = 每次调用至多扫一遍流(与 17 的 `NodeRemaining.ScanNode` 同口径;
            //    4 的拾取管线接线后由其自身节流,本类不自持缓存)。
            bool found = false;
            WorldPos anchor = default;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.DropSpawned) continue;
                DropSpawnedPayload p = decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
                if (p.InstanceId != instanceId) continue;
                anchor = p.SpawnAnchor;
                found = true;
                break;
            }
            if (!found)
                return false;   // 流里查无出生(fold 报 DanglingInstanceReference;此处零动作)

            // ③ 回捡约束(20 R7):走到锚点格(整数 Chebyshev ≤ 宽容半径)。
            return Chebyshev(in actorCell, in anchor) <= tolerance;
        }

        /// <summary>两格 Chebyshev 距离(先拓宽 int64 再取绝对值 —— 承 F-4.1 d∞ 陷阱;
        /// 私有副本是既有先例,`InteractionSelector` / `NeighbourhoodReporter` 同款)。</summary>
        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            long dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }
    }
}
