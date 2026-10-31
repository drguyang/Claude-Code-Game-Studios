// story-004 —— F-17-3 节点余量(派生态前缀函数)与采前闸。
//
// 权威来源:
//   design/gdd/foraging.md
//     · F-17-3 `:341-360` ——
//         EffCapacity(node,t) = BaseCapacity × SEASON_MULT[season_index(t)] / MUL_ONE
//                               × YieldDecay(Σ_all qty @ node)
//         remaining(node,t)   = max(0, EffCapacity − Fix(Σ_window qty @ node))
//         yieldDecay(n)       = max(DECAY_FLOOR, MUL_ONE − Mul(n, DECAY_RATE))
//         求值口径 ②:采前闸是 `remaining ≥ Fix(qty)`,**不是** `remaining > 0`
//     · §边界探针 `:398-410` —— `remaining = 0.5` 且 `qty = 1` ⇒ **拒**(原式会「采穿」)
//     · §Edge Cases `:412-427` —— 满载 / 余量不足 = **零事件、零消耗**
//   ADR-009 §四 —— 余量是**派生态**:事件流前缀 ∪ 版本化烘焙数据的纯函数,不进流、不存档
//   ADR-005 —— 主机唯一求值;ADR-006 —— 定点域、唯一舍入 `ROUND_HALF_AWAY_FROM_ZERO`
//   story production/epics/foraging/story-004-node-remaining-prefix.md
//     AC-17-06(采前闸)· AC-17-17(YieldDecay 三性质)· AC-17-13(形状半边)· 前缀一致性
//
// ── 为什么是一个静态纯函数类(而不是服务) ──────────────────────────────────
// 与 `DeathCellQuery` / `FoldedPredicate` 同族:**只读流、零状态、零缓存**。
// 余量的定义就是「流前缀的纯函数」,若引入一个带缓存的服务,`remaining` 就有了
// 第二个真源(缓存与现算可能分叉)。OQ-17-5(现算 vs 缓存)**未裁** ⇒ 取**最小正确**的现算,
// 缓存化留给将来(其约束已写进 GDD:`键 = 纯 (流前缀, tick)` 且从空重建等值)。
//
// ── 一次扫描,三处消费(AC-17-14/15 的「零分叉」要求)─────────────────────
// 采集计数 / `Σ_all` / `Σ_window` **必须来自同一次遍历**。分三次扫 = 三个可能被
// 各自改动的地方,而它们本应永远同步。
//
// ── 空集语义 ──────────────────────────────────────────────────────────────
// 从未被采过的节点:`GatherCount = 0`、两个 Σ 都是 0 ⇒ `remaining = EffCapacity`(满)。
// 这是**合法结论**,不是「未找到」—— 节点定义来自烘焙数据,不来自事件流(ADR-009 §四)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Foraging
{
    /// <summary>
    /// F-17-3 —— 节点余量(`ResourceHarvested` 前缀的纯函数)与采前闸 `remaining ≥ qty`。
    /// </summary>
    /// <remarks>
    /// <para><b>派生态纪律</b>(ADR-009 §四):本类不写流、不缓存、不持有跨调用状态。
    /// 同一 `(流前缀, tick)` 必然给出同一结果。</para>
    /// <para><b>主机唯一</b>(ADR-005):只由主机在裁决时求值;客户端不得自判
    /// (GDD F-17-3 求值口径 ④)。</para>
    /// </remarks>
    public static class NodeRemaining
    {
        /// <summary>
        /// 单个节点的一次前缀扫描结果 —— <b>计数与两个 Σ 出自同一次遍历</b>。
        /// </summary>
        public readonly struct PrefixScan
        {
            /// <summary>该节点 `ResourceHarvested` 的**条数**(= 下一次发号的基数;AC-17-11)。</summary>
            public readonly int GatherCount;

            /// <summary>`Σ_all qty`:全时段累计(永不回退),喂 `YieldDecay`。</summary>
            public readonly long SumAllQty;

            /// <summary>`Σ_window qty`:`t − Tick(e) &lt; RegrowWindow` 的部分,喂再生项。</summary>
            public readonly long SumWindowQty;

            /// <summary>构造扫描结果。</summary>
            public PrefixScan(int gatherCount, long sumAllQty, long sumWindowQty)
            {
                if (gatherCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(gatherCount), gatherCount,
                        "采集条数不得为负");
                if (sumAllQty < 0 || sumWindowQty < 0)
                    throw new ArgumentOutOfRangeException(nameof(sumAllQty), sumAllQty,
                        "采集量之和不得为负(`qty ≥ 1` 由 F-17-2 保证)");

                GatherCount = gatherCount;
                SumAllQty = sumAllQty;
                SumWindowQty = sumWindowQty;
            }
        }

        /// <summary>
        /// 扫出某节点在 `tick` 处的采集前缀:条数 + `Σ_all` + `Σ_window`。
        /// </summary>
        /// <param name="events">事件流只读视图(可传全流 —— 本方法按 <c>Kind</c> 自筛)。</param>
        /// <param name="decoder">载荷解码器(<c>Sim</c> 读载荷的唯一合法路径,ADR-031)。</param>
        /// <param name="nodeId">资源节点 id(非负)。</param>
        /// <param name="tick">求值时刻。**只计 `Tick(e) ≤ tick` 的事件**(前缀语义)。</param>
        /// <param name="regrowWindowTicks">`RegrowWindow`(&gt; 0;装载期已校验,此处再守一次)。</param>
        /// <remarks>
        /// <para><b>窗口判据是严格小于</b>:`t − Tick(e) &lt; RegrowWindow`(GDD 逐字)。
        /// 恰好等于窗口的事件**已在窗外** —— 取 `&lt;=` 会让再生慢一拍,且与 GDD 不符。</para>
        /// <para><b>为什么排除未来事件</b>:余量是「**已确立前缀**的纯函数」(GDD 求值口径 ④)。
        /// 若把 `Tick(e) &gt; tick` 的也计入,同一 tick 的两次求值会因调用时机不同而不同 ——
        /// 那是把「求值时刻」偷换成「流当时有多长」。</para>
        /// </remarks>
        public static PrefixScan ScanNode(
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            int nodeId,
            long tick,
            long regrowWindowTicks)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (nodeId < 0)
                throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId,
                    "资源节点 id 不得为负");
            if (regrowWindowTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(regrowWindowTicks), regrowWindowTicks,
                    "RegrowWindow 须 > 0 —— 否则「无再生」退化为「无限采集」(GDD F-17-3 求值口径 ③)");

            int count = 0;
            long sumAll = 0, sumWindow = 0;
            if (events == null) return new PrefixScan(0, 0, 0);

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ResourceHarvested)
                    continue;
                if (e.Tick > tick)
                    continue;

                ResourceHarvestedPayload payload =
                    decoder.Decode<ResourceHarvestedPayload>(EventKind.ResourceHarvested, e.Payload);

                if (payload.NodeId != nodeId)
                    continue;

                count++;
                sumAll += payload.Qty;
                if (tick - e.Tick < regrowWindowTicks)
                    sumWindow += payload.Qty;
            }

            return new PrefixScan(count, sumAll, sumWindow);
        }

        /// <summary>
        /// `yieldDecay(n) = max(DECAY_FLOOR, MUL_ONE − Mul(n, DECAY_RATE))` —— 单调不增、有下限。
        /// </summary>
        /// <param name="constants">调参表(取 `DECAY_RATE` / `DECAY_FLOOR`)。</param>
        /// <param name="sumAllQty">`Σ_all qty`(全时段累计,永不回退)。</param>
        /// <remarks>
        /// <para><b>为什么全时段而不只窗口</b>:窗口项是「短周期恢复」,衰减项是「越采越难」。
        /// 只用窗口 ⇒ 窗口一过就完全恢复,深水线(`concept-benchmark.md:82`)承诺的形状**产不出来**
        /// —— 那是 GDD 2026-09-19 的形状改判(根因 P0-4)。</para>
        /// <para><b>`n` 的量纲是「份数」不是「次数」</b>:`Mul(n, DECAY_RATE)` 里的 `n` = `Σ qty`。
        /// GDD 表头写 `YieldDecay(n)`,`:355` 的调用点是 `YieldDecay(Σ_all qty @ node)`。</para>
        /// </remarks>
        public static Fix YieldDecay(ForagingConstants constants, long sumAllQty)
        {
            if (constants == null) throw new ArgumentNullException(nameof(constants));
            if (sumAllQty < 0)
                throw new ArgumentOutOfRangeException(nameof(sumAllQty), sumAllQty,
                    "Σ_all qty 不得为负");

            Fix linear = Fix.One - new Fix(sumAllQty * Fix.OneRaw) * constants.DecayRate;
            return linear.Raw < constants.DecayFloor.Raw ? constants.DecayFloor : linear;
        }

        /// <summary>
        /// `EffCapacity(node,t) = BaseCapacity × SEASON_MULT[season_index(t)] / MUL_ONE × YieldDecay(Σ_all)`。
        /// </summary>
        /// <param name="constants">调参表。</param>
        /// <param name="seasonIndex">`season_index(t)` —— **由 5 的 F-5.2 提供**,本类不自行推导。</param>
        /// <param name="sumAllQty">`Σ_all qty @ node`。</param>
        /// <remarks>
        /// <para><b>运算次序照 GDD 字面</b>:`(Base × SEASON_MULT) / MUL_ONE` **先**做,再乘衰减。
        /// 交换次序会改变中间舍入点 —— 那是另一条曲线,不是这条。</para>
        /// <para><b>为什么不在本类算 `season_index`</b>:它是 **5 的唯一交付物**(`OQ-5-1` 2026-09-18);
        /// 17 只查表做乘法。在本类再算一遍 = 同一事实的第二份实现。</para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">`seasonIndex` 越出 `SEASON_MULT` 表。</exception>
        public static Fix EffCapacity(ForagingConstants constants, long seasonIndex, long sumAllQty)
        {
            if (constants == null) throw new ArgumentNullException(nameof(constants));
            if (seasonIndex < 0 || seasonIndex >= constants.SeasonMult.Count)
                throw new ArgumentOutOfRangeException(nameof(seasonIndex), seasonIndex,
                    $"season_index 越出 SEASON_MULT 表(长度 {constants.SeasonMult.Count})—— " +
                    "表长校验归 story-001,此处只守下标");

            Fix baseWithSeason =
                new Fix((long)constants.BaseCapacity * Fix.OneRaw)
                * constants.SeasonMult[(int)seasonIndex] / Fix.One;

            return baseWithSeason * YieldDecay(constants, sumAllQty);
        }

        /// <summary>
        /// `remaining(node,t) = max(0, EffCapacity − Fix(Σ_window qty))`。
        /// </summary>
        /// <param name="constants">调参表。</param>
        /// <param name="seasonIndex">`season_index(t)`(5 提供)。</param>
        /// <param name="scan">同一前缀的扫描结果(<see cref="ScanNode"/>)。</param>
        /// <remarks>
        /// <para><b>量纲齐一</b>(GDD 求值口径 ①):`Σ_window qty` 是 **int 计数**,须先提升为 `Fix`
        /// 再相减。`Fix − int` 是 D-21-19 同类的量纲不齐。</para>
        /// <para><b>`Σ_window ≥ 0` ⇒ `remaining ≤ EffCapacity` 恒成立</b>,无需额外 `min` 封顶。</para>
        /// </remarks>
        public static Fix Remaining(ForagingConstants constants, long seasonIndex, in PrefixScan scan)
        {
            Fix eff = EffCapacity(constants, seasonIndex, scan.SumAllQty);
            Fix consumed = new Fix(scan.SumWindowQty * Fix.OneRaw);
            Fix remaining = eff - consumed;
            return remaining.Raw < 0 ? Fix.Zero : remaining;
        }

        /// <summary>
        /// 采前闸:`remaining(node,t) ≥ Fix(qty)` 才允许采集。
        /// </summary>
        /// <param name="constants">调参表。</param>
        /// <param name="seasonIndex">`season_index(t)`(5 提供)。</param>
        /// <param name="scan">同一前缀的扫描结果。</param>
        /// <param name="qty">本次想采的量(≥ 1,F-17-2 保证)。</param>
        /// <returns>可采 ⇒ <c>true</c>;余量不足 ⇒ <c>false</c>(调用方须**零事件**)。</returns>
        /// <remarks>
        /// <para><b>判据是 `≥` 不是 `&gt; 0`</b>(GDD 求值口径 ②):`remaining = 1/2` 而 `qty = 1` 时,
        /// 「`remaining &gt; 0` 就放行」会把节点**采穿**(边界探针表 `:404`)。取等 = 刚好采完,合法。</para>
        /// <para><b>本方法只判「够不够」,不判「库存满不满」</b> —— 满载归 20 的 `CanCarry`
        /// (story-005 的 AC-17-07),17 不自判容量(GDD 规则六)。</para>
        /// </remarks>
        public static bool CanHarvest(
            ForagingConstants constants, long seasonIndex, in PrefixScan scan, int qty)
        {
            if (qty < 1)
                throw new ArgumentOutOfRangeException(nameof(qty), qty,
                    "qty 须 ≥ 1(F-17-2 保证:qty_per_node ≥ 1 ∧ GatherMul(0) = 1)");

            Fix remaining = Remaining(constants, seasonIndex, scan);
            return remaining.Raw >= qty * Fix.OneRaw;
        }
    }
}
