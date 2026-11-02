// S23-6 · sprint-23 —— 拾取三段式的**编排面**(ADR-009 §七 · 2026-11-01)
//
// ── 三段式(与 17 采集同构先例)───────────────────────────────────────────────
// ① **意图事件**:E 按下沿由 `InteractionInputDriver` 的 claim 钩子调到本类
//    (单一边沿所有者 —— 本类不持边沿位,与 S22-9 采集桥同格);
// ② **当下判距**:玩家经流确立格(`ActorCellEntered` 之 cell,ADR-020 §四 ——
//    调用方传一次,本类不重查流、禁读表现态连续位置);
// ③ **宽容半径**:注入的整数格 Chebyshev 半径(实值归装配期常量/数值轮)。
//
// ── 选中归 4,本类只结算「被指名的那一个」(🔁 2026-11-02 · `TD-interaction-02`)──
// 旧口径 = **第一可拾**(投影 `InstancesOf(NoOwner)` 的 id 升序扫到的第一个通过三段的候选)。
// 它当时的理由是「就近须再取每候选锚点 + 需打破同距平局」—— 两条现都有解:
// 锚点面已有唯一定义(`DeathDropReclaimPolicy.TryGetSpawnAnchor`),
// 同距平局由 F-4.1 第三键(`StableId`)定 —— 且这两件都是**4 的既有职责**。
// ⇒ 现口径:**「打给哪一件」归 4 的目标选择器**(`InteractionSelector`,全序 ⟨d∞, KindPriority, StableId⟩),
//   本类只结算**被指名**的 instance_id(仍串联三段闸:`CanClaim`)。
// ⚠️ 保留「第一可拾」= 在 4 之外再留一处判据 —— 正是 `TD-interaction-01/02` 要消灭的形态。
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
    /// 拾取结算:意图(E 按下沿)→ 判距(经流确立格)→ 宽容半径 → **被 4 指名的**地面实例
    /// 经 20 的写者 <see cref="DropEventWriter.RecordClaimed"/> 入流。
    /// <para>⚠️ <b>本类不选择</b> —— 「打给哪一件」归 4 的 `InteractionSelector`(F-4.1 全序)。
    /// 本类只回答「这一件能不能拾」,不能则零事件(调用方据此不消费按键)。</para>
    /// </summary>
    public sealed class DropClaimOrchestrator
    {
        private readonly Func<System.Collections.Generic.IReadOnlyList<SimEvent>> _eventsOf;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<InventoryProjection> _projectionOf;
        private readonly DropEventWriter _drops;
        private readonly int _tolerance;

        /// <summary>宽容半径(整数格,Chebyshev)———— **只读暴露**,供 4 在装载候选集时
        /// 用同一道门预裁(见 `CandidateSetLoader.LoadWithin` 的第三个形参)。
        /// <para>⚠️ 暴露的是**同一个数**,不是第二处声明:候选集若放行超出本半径的掉落,
        /// 它会赢得全序却又在结算时被拒 ⇒ 按键被静默吞掉(而旁边的采集点/病人明明更远)。</para></summary>
        public int Tolerance => _tolerance;

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
        /// 一次拾取结算:**结算被指名的实例**(选择归 4,见类注)。
        /// </summary>
        /// <param name="actorId">拾取者玩家 id(20 的归属面)。</param>
        /// <param name="tick">当前逻辑 tick(与 `DropClaimed` 事件头同 tick)。</param>
        /// <param name="actorCell">玩家**经流确立格**(ADR-020 §四;调用方算一次传入 ——
        /// 与 `DeathCellQuery` 生产 lambda 同源,禁表现态连续位置)。</param>
        /// <param name="instanceId">被 4 选中的掉落实例 id(21 的 `ItemInstanceId` 空间)。</param>
        /// <returns>true = 该实例被 claim(恰一条 `DropClaimed`);
        /// false = 拒(已归属 / 流里无出生锚 / 出宽容半径)且**零事件**。</returns>
        /// <exception cref="ArgumentOutOfRangeException">actorId / tick / instanceId 为负。</exception>
        public bool TryClaimInstance(int actorId, long tick, WorldPos actorCell, long instanceId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 走机制 A 计数器,不得为负");
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (instanceId < 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId), instanceId,
                    "实例 id 走 IIdAuthority 计数器,不得为负(畸形输入,不当「查无」处理)");

            // 三段闸 + 归属面 + 锚点面全在 `CanClaim` 里(唯一定义,不在此处复制)。
            if (!DeathDropReclaimPolicy.CanClaim(_eventsOf(), _decoder, _projectionOf(),
                    instanceId, actorCell, _tolerance))
                return false;

            // 裸 long 重载 —— 被选中行 id 原样(AC-17-05:Sim 内零 struct 构造点)。
            _drops.RecordClaimed(tick, instanceId, actorId);
            return true;
        }
    }
}
