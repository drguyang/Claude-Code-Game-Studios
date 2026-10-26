// S20-4 前置链 · foraging story-002 —— 品级抽取管线 F-17-1(掷骰 + CDFWalk + QualityCap)
//                                       + 单次采量 F-17-2(GatherMul)
//
// 权威来源:
//   GDD design/gdd/foraging.md
//     · F-17-1 `:265-310`(raw_quality = CDFWalk(quality_distribution, U);
//                          QualityCap = CapTable[QueryLevel(采集)];
//                          out_quality = clamp(raw, 1, QualityCap))
//     · F-17-1 边界口径 `:287-292`(累积权重整数比较,**严格 <**,左闭右开)
//     · F-17-1 装载期校验 `:303-311`(Σw ≥ 1 硬失败 —— CDFWalk 除零 / 静默返回最大档)
//     · F-17-2 `:318-336`(qty = Mul(qty_per_node, GatherMul(L));唯一舍入点
//                          ROUND_HALF_AWAY_FROM_ZERO)
//     · §Tuning Knobs `:543`(GatherMul(L) = 1 + L / 30,已裁 2026-10-06;GATHER_MUL_CAP = 3)
//   story production/epics/foraging/story-002-quality-roll-pipeline.md
//     AC-17-03 / AC-17-04 / AC-17-11 / AC-17-15(AC-17-02 跨平台面归 ADR-012,本件不记绿)
//   ADR-007 §Decision ③(掷骰的每个输入必须可从事件流重构 —— 核心不变量)
//   ADR-005 / ADR-006(定点域 · 单一舍入 ROUND_HALF_AWAY_FROM_ZERO)
//   ADR-016 §一(三源不变量:事件流 ∪ 版本化烘焙数据 ∪ 二者的纯函数)
//
// ⚠️ **本类不持有状态** —— 全部方法是 `(WorldSeed, node_id, gather_seq, 表) → 整数` 的纯函数。
//    这是 ADR-007 ③ 的结构性落实:没有可变的掷骰状态,「隐藏随机态」这一整类 bug 不可表达。
//
// ⚠️ **gather_seq 不由本类产生**。它的计源 = 世界流 `ResourceHarvested` 的节点计数,
//    实现已在 `Sim/World/ResourceHarvestWriter.cs` 的 `CountHarvestsOfNode`(私有,写者自算)。
//    本类只**消费**它 —— 若在此再开一个计数器,就是全案第二个真源(AC-17-11 直接禁)。
//
// ⚠️ **CDFWalk 用高 48 位形**(`EventRng.CdfWalkHigh48`),不是 `EventRng.CdfWalk`。
//    两形统计同构、逐点不同(实测逐点一致率 20.1%);GDD `:279-285` 定死的是前者。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Foraging
{
    /// <summary>
    /// 品级抽取管线(F-17-1)与单次采量(F-17-2)。
    /// <para><b>纯函数</b>:全部输入的取值面 = {WorldSeed(存档头), node_id(烘焙逻辑层),
    /// gather_seq(世界流纯函数), quality_distribution(烘焙数据), CapTable(烘焙数据),
    /// 采集技能等级(30 的 QueryLevel)} —— 三源不变量(ADR-016 §一)逐项满足。</para>
    /// </summary>
    public static class GatherQualityRoll
    {
        /// <summary>`SplitMix64` 的盐位。逐字取自 GDD F-17-1 <c>:276</c> —— 不得改名。</summary>
        /// <remarks>
        /// ⚠️ 改名 = 全仓同一 <c>(WorldSeed, node_id, gather_seq)</c> 产出<b>不同品级</b>,
        /// 且不报错。该串是**数据契约位**,不是自由命名。
        /// </remarks>
        public const string GatherSalt = "gather";

        /// <summary>`GatherMul` 的分母(F-17-2 · §Tuning Knobs <c>:543</c>:`GatherMul(L) = 1 + L / 30`)。</summary>
        /// <remarks>
        /// ⚠️ **整数除法**:`L / 30` 在 <c>L ∈ [0, 29]</c> 恒 0、<c>[30, 59]</c> 恒 1、<c>60</c> 为 2
        /// ⇒ `GatherMul` 实际取值只有 <b>{1, 2, 3}</b> 三个阶跃值,<b>不是连续曲线</b>。
        /// 这与散文暗示的「随等级平滑增长」不符 —— 已登 `TD-foraging-02`,归数值轮。
        /// 本实现按 GDD <b>字面式</b>落地(与 `TD-foraging-01` 的 `CapTable` 同一处置口径)。
        /// </remarks>
        public const int GatherMulDenominator = 30;

        /// <summary>`U = h >> 16` 的移位量(GDD F-17-1 <c>:277</c>:取<b>高 48 位</b>)。</summary>
        public const int U48Shift = 16;

        /// <summary>`U` 的值域上界(不含):`2^48`。</summary>
        public const ulong U48Limit = 1UL << 48;

        /// <summary>
        /// 掷骰源的高 48 位投影 <c>U</c>(GDD F-17-1 <c>:276-277</c>)。
        /// </summary>
        /// <param name="worldSeed">世界种子(ADR-007 ②:住存档头,非 <c>SimEvent</c>)。</param>
        /// <param name="nodeId">资源节点的稳定标识(ADR-015 逻辑层整数格)。</param>
        /// <param name="gatherSeq">该节点**此前**已发出的 <c>ResourceHarvested</c> 条数(0 起)。</param>
        /// <returns><c>U ∈ [0, 2^48)</c>。</returns>
        public static ulong DrawU(long worldSeed, int nodeId, int gatherSeq)
        {
            if (gatherSeq < 0)
                throw new ArgumentOutOfRangeException(nameof(gatherSeq), gatherSeq,
                    "gather_seq 是该节点已采次数,恒 ≥ 0(0 = 首次采集)");

            ulong h = SplitMix64.HashTagged(worldSeed, GatherSalt, nodeId, gatherSeq);
            return h >> U48Shift;
        }

        /// <summary>
        /// `raw_quality` —— 不经 <c>QualityCap</c> 钳制的原始品级(F-17-1 第三步)。
        /// <para>⚠️ <b>刻意不落流</b>(GDD F-17-1 注 / 规则四):它可由
        /// <c>(WorldSeed, node_id, gather_seq)</c> 逐位重算,存它就是第二真源。
        /// 本方法存在的意义 = 让「可重算」这条判据(AC-17-04)有一个可调用的验证点。</para>
        /// </summary>
        /// <param name="worldSeed">世界种子(存档头)。</param>
        /// <param name="nodeId">节点标识。</param>
        /// <param name="gatherSeq">该节点已采次数。</param>
        /// <param name="qualityDistribution">各档权重(<c>int</c>,索引 <c>i</c> ⇒ 品级 <c>i+1</c>)。</param>
        /// <returns>原始品级 ∈ <c>[1, qualityDistribution.Count]</c>。</returns>
        /// <exception cref="ArgumentException"><c>qualityDistribution</c> 非法
        /// (null / 长度 0 / 含负权重 / <c>Σw = 0</c>)—— 见 <see cref="EventRng.CdfWalkHigh48"/>。</exception>
        public static int RawQuality(long worldSeed, int nodeId, int gatherSeq,
                                     IReadOnlyList<int> qualityDistribution)
        {
            ulong u = DrawU(worldSeed, nodeId, gatherSeq);
            return EventRng.CdfWalkHigh48(u, qualityDistribution).ChosenKey;
        }

        /// <summary>
        /// `out_quality` —— 完整 F-17-1 四步(掷骰 → raw → Cap → clamp)。
        /// </summary>
        /// <param name="worldSeed">世界种子(存档头)。</param>
        /// <param name="nodeId">节点标识。</param>
        /// <param name="gatherSeq">该节点已采次数。</param>
        /// <param name="qualityDistribution">各档权重(<c>int</c>)。</param>
        /// <param name="constants">已装载的 17 调参表(提供 <c>CapTable</c> 与 <c>MAX_QUALITY</c>)。</param>
        /// <param name="gatherLevel">采集技能等级 ∈ <c>[0, SKILL_CAP]</c>(30 的 <c>QueryLevel(采集)</c>)。</param>
        /// <returns>最终品级 ∈ <c>[1, MAX_QUALITY]</c>,已过 <c>QualityCap</c> 截断。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="constants"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="gatherLevel"/> 越界
        /// (由 <see cref="ForagingConstants.QualityCapAt"/> 的索引前守卫抛出)。</exception>
        public static int OutQuality(long worldSeed, int nodeId, int gatherSeq,
                                     IReadOnlyList<int> qualityDistribution,
                                     ForagingConstants constants, int gatherLevel)
        {
            if (constants == null)
                throw new ArgumentNullException(nameof(constants));

            int raw = RawQuality(worldSeed, nodeId, gatherSeq, qualityDistribution);
            int cap = constants.QualityCapAt(gatherLevel);

            // GDD F-17-1 `:270`:out = clamp(raw, 1, cap)。
            // ⚠️ lower = 1(不是 0)—— cap 恒 ≥ 1 由 ForagingConstants 构造期守卫
            //    (AC-17-14「CapTable[0] ≥ 1」)保证,故 min > max 不可达。
            if (raw < 1) return 1;
            if (raw > cap) return cap;
            return raw;
        }

        /// <summary>
        /// `GatherMul(L)`(F-17-2 · GDD <c>:543` 已裁式 `1 + L / 30`)。
        /// <para>⚠️ <b>整数除法</b> ⇒ 只有 {1, 2, 3} 三个阶跃值,不是连续曲线。见
        /// <see cref="GatherMulDenominator"/> 的 remarks 与 `TD-foraging-02`。</para>
        /// </summary>
        /// <param name="level">采集技能等级 ∈ <c>[0, SKILL_CAP]</c>。</param>
        /// <param name="constants">已装载的 17 调参表(提供 <c>GATHER_MUL_CAP</c>)。</param>
        /// <returns><c>GatherMul(L) ∈ [1, GATHER_MUL_CAP]</c>,<c>Fix</c> 域。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="constants"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 越出
        /// <c>[0, SKILL_CAP]</c>。</exception>
        public static Fix GatherMul(int level, ForagingConstants constants)
        {
            if (constants == null)
                throw new ArgumentNullException(nameof(constants));
            if (level < 0 || level > ForagingConstants.MaxSkillLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    "采集技能等级须 ∈ [0, " + ForagingConstants.MaxSkillLevel + "](= SKILL_CAP)");

            // GatherMul(L) = 1 + L / 30 —— 字面式,整数除法。
            // ⚠️ 结果封顶到 GATHER_MUL_CAP:字面式在 L = SKILL_CAP(60) 给出 3,
            //    而 CAP 已裁为 3 ⇒ 两者恰相遇。封顶是**保险**而非修正:若将来 CAP 被调低
            //    (数值轮),字面式会越界 —— 此处拦下,不让越界值流出去。
            int mulInt = 1 + level / GatherMulDenominator;

            var mul = new Fix((long)mulInt << Fix.FractionalBits);
            return mul.Raw > constants.GatherMulCap.Raw ? constants.GatherMulCap : mul;
        }

        /// <summary>
        /// 单次采量 `qty = Mul(qty_per_node, GatherMul(L))`(F-17-2)。
        /// <para><b>唯一舍入点</b> = <c>Fix.Round()</c>,模式
        /// <c>ROUND_HALF_AWAY_FROM_ZERO</c>(ADR-006;禁 <c>Math.Round</c> 默认 ties-to-even)。</para>
        /// </summary>
        /// <param name="qtyPerNode">采集基数(21a <c>gather_profile.qty_per_node</c>,须 &gt; 0)。</param>
        /// <param name="level">采集技能等级 ∈ <c>[0, SKILL_CAP]</c>。</param>
        /// <param name="constants">已装载的 17 调参表。</param>
        /// <returns><c>qty ≥ 1</c>(由 <c>qty_per_node ≥ 1</c> ∧ <c>GatherMul(0) = 1</c> 结构保证,
        /// 半程上舍入不会降到 0)。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="qtyPerNode"/> ≤ 0。</exception>
        public static int HarvestQty(int qtyPerNode, int level, ForagingConstants constants)
        {
            if (qtyPerNode <= 0)
                throw new ArgumentOutOfRangeException(nameof(qtyPerNode), qtyPerNode,
                    "qty_per_node 是采集基数,须 > 0(21a gather_profile 约束)");

            Fix baseQty = new Fix((long)qtyPerNode << Fix.FractionalBits);
            Fix product = baseQty * GatherMul(level, constants);
            return (int)product.Round();          // ROUND_HALF_AWAY_FROM_ZERO,唯一舍入点
        }
    }
}
