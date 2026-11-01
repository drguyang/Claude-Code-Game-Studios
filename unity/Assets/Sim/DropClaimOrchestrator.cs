// S23-6 · sprint-23 —— 拾取三段式的**编排面**(ADR-009 §七 · 2026-11-01)
//
// ── 三段式(与 17 采集同构先例)───────────────────────────────────────────────
// ① **意图事件**:E 按下沿由 `InteractionInputDriver` 的 claim 钩子调到本类
//    (单一边沿所有者 —— 本类不持边沿位,与 S22-9 采集桥同格);
// ② **当下判距**:玩家经流确立格(`ActorCellEntered` 之 cell,ADR-020 §四 ——
//    调用方传一次,本类不重查流、禁读表现态连续位置);
// ③ **宽容半径**:注入的整数格 Chebyshev 半径(实值归装配期常量/数值轮)。
//
// ── 候选面与确定性 ───────────────────────────────────────────────────────────
// 地面候选 = 库存投影的 `InstancesOf(NoOwner)`(**instance_id 升序**,投影钉的序)。
// 选中规则 = **第一可拾**(升序扫到的第一个通过三段的候选)—— 刻意不用「就近」:
// 就近须再取每候选锚点(第二次扫流)且需打破同距平局;P0 口径 = id 序最小者得,
// 确定性由投影序直接保证。就近呈现归表现层/后续轮(需显式 tie-break,不破坏确定性)。
//
// ── 零铸造 / 拒收零事件 ─────────────────────────────────────────────────────
// claim 的 instance_id = 投影行 id **原样**(裸 long 重载 —— AC-17-05 IL 门:
// Sim 内 ItemInstanceId 构造点唯一落权威,同 S23-2 惯例)。
// **拒收零事件**:已归属 / 无出生锚 / 出半径(`DeathDropReclaimPolicy` 三段拒绝)或
// 地面无候选 ⇒ 返回 false 且流零增量 —— 调用方(输入驱动)把按键落回查体链。
//
// ── 幂等(二次 claim 拒)────────────────────────────────────────────────────
// policy ① 拒已归属 ⇒ 同一实例二次 claim 返回 false 零事件;库存恢复由 20 的
// fold 直接重建(`DropClaimed` 归属转移),本类不碰投影。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 拾取编排:意图(E 按下沿)→ 判距(经流确立格)→ 宽容半径 → 第一可拾地面实例
    /// 经 20 的写者 <see cref="DropEventWriter.RecordClaimed"/> 入流。
    /// </summary>
    public sealed class DropClaimOrchestrator
    {
        private readonly Func<System.Collections.Generic.IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<InventoryProjection> _projectionOf;
        private readonly DropEventWriter _drops;
        private readonly int _tolerance;

        /// <summary>构造拾取编排面。</summary>
        /// <param name="eventsOf">事件流只读视图来源(派生态每次重取,不持快照)。</param>
        /// <param name="decoder">载荷解码器(锚点面 / 归属面读真源)。</param>
        /// <param name="projectionOf">库存投影来源(候选 + 归属面;派生态每次重取)。</param>
        /// <param name="drops">20 的掉落写者(claim 唯一入流通道;本类零直接 Append)。</param>
        /// <param name="tolerance">宽容半径(整数格,Chebyshev;≥ 0)。</param>
        /// <exception cref="ArgumentNullException">任一委托 / 依赖为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">tolerance 为负。</exception>
        public DropClaimOrchestrator(
            Func<System.Collections.Generic.IReadOnlyList<SimEvent>> eventsOf,
            IPayloadDecoder decoder,
            Func<InventoryProjection> projectionOf,
            DropEventWriter drops,
            int tolerance)
        {
            _eventsOf = eventsOf ?? throw new ArgumentNullException(nameof(eventsOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _projectionOf = projectionOf ?? throw new ArgumentNullException(nameof(projectionOf));
            _drops = drops ?? throw new ArgumentNullException(nameof(drops));
            if (tolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance,
                    "宽容半径 ≥ 0(负值属畸形输入)");
            _tolerance = tolerance;
        }

        /// <summary>
        /// 一次拾取意图:三段裁决通过则 claim 第一可拾地面实例(零铸造)。
        /// </summary>
        /// <param name="actorId">拾取者玩家 id(20 的归属面)。</param>
        /// <param name="tick">当前逻辑 tick(与 `DropClaimed` 事件头同 tick)。</param>
        /// <param name="actorCell">玩家**经流确立格**(ADR-020 §四;调用方算一次传入 ——
        /// 与 `DeathCellQuery` 生产 lambda 同源,禁表现态连续位置)。</param>
        /// <returns>true = 有实例被 claim(恰一条 `DropClaimed`);
        /// false = 拒(无候选 / 三段任一不过)且**零事件** —— 按键由调用方落回查体链。</returns>
        /// <exception cref="ArgumentOutOfRangeException">actorId / tick 为负。</exception>
        public bool TryClaim(int actorId, long tick, WorldPos actorCell)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 走机制 A 计数器,不得为负");
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            InventoryProjection projection = _projectionOf();
            System.Collections.Generic.IReadOnlyList<long> ground =
                projection.InstancesOf(InventoryProjection.NoOwner);
            if (ground.Count == 0)
                return false;   // 地面无候选 —— 零动作(负例的零事件面)

            System.Collections.Generic.IReadOnlyList<SimEvent> events = _eventsOf();

            // instance_id 升序扫(投影钉的序)—— 第一可拾 = 确定性选中(见文件头)。
            for (int i = 0; i < ground.Count; i++)
            {
                long instanceId = ground[i];
                if (!DeathDropReclaimPolicy.CanClaim(events, _decoder, projection,
                        instanceId, actorCell, _tolerance))
                    continue;   // 三段任一不过 ⇒ 零事件,看下一候选

                // 裸 long 重载 —— 投影行 id 原样(AC-17-05:Sim 内零 struct 构造点)。
                _drops.RecordClaimed(tick, instanceId, actorId);
                return true;
            }
            return false;
        }
    }
}
