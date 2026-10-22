// GDD disease-simulation.md §F5 · `:1294-1340` —— 多病聚合(position_agg / trend_backward_agg)
// 与 argmax 的 `d` 选择(2026-10-22 立)。
//
// 权威来源:
//   GDD disease-simulation.md:1119-1125 —— `d` 的选取:`position_agg` 的 **argmax**,
//     「并列时取 `DiseaseId` 最小者,保证确定性」;并给出假绿反例:
//     病人患麻疹(position 0.9)+ 一道轻刺伤(position 0.2,lethal=true 急性保持型)
//     ⇒ position_agg = 0.9 ≥ DEATH ∧ LethalFor(刺伤) ⇒ **判死**,尽管刺伤只有 0.2。
//   GDD disease-simulation.md:1300-1303 —— F5 合成式:
//     `position_agg(t) = max_{d∈D} position_d(t)`
//     `trend_backward_agg(t) = max_{d∈D} trend_backward_d(t)`
//   GDD disease-simulation.md:1337 —— 「九态机判定(F4)吃的是 position_agg(t),
//     不是任一个 position_d(t)」。
//   ADR-016 §一 —— 三源不变量:合成是纯函数,输入只有各 d 的已求值量。
//
// ── 为什么单独立类(而不是塞进 F4OutcomeEvaluator)────────────────────────────
//   聚合是 **F5 的职责**,F4 只**消费**它的输出。两者的证伪面不同:
//     · 聚合错(选错 d / max 写错)⇒ 「该用哪个病的读数」错;
//     · 求值错(阈值 / 窗口)⇒ 「拿到读数后怎么判」错。
//   塞在一起 ⇒ 一个红分不清是哪一半。
//
// ── 为什么 argmax 必须**返回 d 本身**(而不是只返回最大值)──────────────────────
//   F4 死亡支要 `LethalFor(entity, d)` / `SelfLimited(entity, d)` —— 两门**吃单个 d**。
//   若只回传 `position_agg` 的数值而不同时回传「这个值来自哪个 d」,
//   调用方就得**再扫一遍**去找 d ⇒ 两次遍历可能因浮点/顺序差异选出不同的 d,
//   且那第二次遍历**没有 GDD 依据**(GDD 只定义了 argmax 这一处)。
//   ⇒ 本类把 `(positionAgg, d)` 作为**一个不可分的返回值**一起给出。
//
// ── 并列的确定性(2026-10-22 实现的关键点)────────────────────────────────────
//   GDD `:1120` 明写「并列时取 `DiseaseId` 最小者」。这不是优化,是**确定性要求**:
//   若并列时取「先到者」,则结果取决于 `D` 的遍历序,而遍历序来自事件流的入流序 ——
//   同一组病程在不同到达序下会选出**不同的 d** ⇒ F4 的两门取到不同条目 ⇒
//   逐位重放不一致(ADR-005 破)。
//   ⇒ 比较写成「严格大于才替换」**不够**(先到者胜,依赖顺序),
//     必须写成「大于 **或**(等于 ∧ DiseaseId 更小)才替换」。
//
// ── 与 DiseaseVitalsService 现状的关系 ────────────────────────────────────────
//   现状(`DiseaseVitalsService.StepAll`)对每个 course **独立**求值并各存一份
//   `ProgressionResult`,**没有**跨 d 的聚合 —— 即 `position_agg` 与 `d` 在全库
//   此前**零实现**(本轮之前 `grep position_agg` 只命中注释)。
//   本类是那个缺口的落点;接线由调用方决定(见该类的登记项)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 单病种在某一 tick 的已求值截面 —— 聚合的输入单元。
    /// </summary>
    /// <remarks>
    /// <para><b>字段全部来自已求值的 F1/F2 结果</b>,本 struct 不参与求值
    /// (承 ADR-016 §一:聚合是纯函数,输入只有各 d 的已求值量)。</para>
    /// <para><b>⚠️ 不持有 <c>DiseaseRegistryEntry</c></b>:条目是烘焙数据,可由
    /// <see cref="DiseaseId"/> 查得;把条目塞进来会让本 struct 在测试里难以构造,
    /// 且诱导调用方在聚合层做本该归 F4 的门判定。</para>
    /// </remarks>
    public readonly struct DiseasePosition
    {
        /// <summary>病种 id(注册表 <c>id</c>)。**argmax 并列时的裁决键**。</summary>
        public readonly int DiseaseId;

        /// <summary>该病种的 <c>position_d(t) = clamp(Progress_d(t) / SCALE_d, 0, 1)</c>。</summary>
        public readonly Fix Position;

        /// <summary>
        /// 该病种的 `trend_backward_d(t) = (Progress(t) − Progress(t−W)) / W`(F2)。
        /// <para>⚠️ 是 **<c>trend_backward</c>**(单侧、只读过去),<b>不是</b>展示用的
        /// <c>trend</c>(中心差分,读 t+W/2)。GDD `:1303` 明写合成的是前者。</para>
        /// </summary>
        public readonly Fix TrendBackward;

        public DiseasePosition(int diseaseId, Fix position, Fix trendBackward)
        {
            DiseaseId = diseaseId;
            Position = position;
            TrendBackward = trendBackward;
        }
    }

    /// <summary>
    /// 多病聚合结果 —— `position_agg` + 它的 argmax <c>d</c> + `trend_backward_agg`。
    /// </summary>
    /// <remarks>
    /// <para><b><c>DominantDiseaseId</c> 与 <c>PositionAgg</c> 是不可分的一对</b> ——
    /// 见类注释「为什么 argmax 必须返回 d 本身」。调用方**不得**只取 <c>PositionAgg</c>
    /// 再自己找 d。</para>
    /// </remarks>
    public readonly struct AggregatePosition
    {
        /// <summary><c>position_agg(t) = max_d position_d(t)</c>。</summary>
        public readonly Fix PositionAgg;

        /// <summary>
        /// 取得 <c>position_agg</c> 的那个 <c>d</c> 的 <c>DiseaseId</c>
        /// (并列时取最小者,见 <see cref="DiseaseAggregator.Aggregate"/>)。
        /// <para>⚠️ 空集合时 = <c>-1</c> 哨兵(见 <see cref="HasAny"/>)。</para>
        /// </summary>
        public readonly int DominantDiseaseId;

        /// <summary><c>trend_backward_agg(t) = max_d trend_backward_d(t)</c>(与 position 同源取大)。</summary>
        public readonly Fix TrendBackwardAgg;

        /// <summary>是否有任何 d(空集合 = 该病人身上无病程)。</summary>
        public readonly bool HasAny;

        internal AggregatePosition(Fix positionAgg, int dominantDiseaseId,
                                   Fix trendBackwardAgg, bool hasAny)
        {
            PositionAgg = positionAgg;
            DominantDiseaseId = dominantDiseaseId;
            TrendBackwardAgg = trendBackwardAgg;
            HasAny = hasAny;
        }
    }

    /// <summary>
    /// F5 多病聚合 —— <c>position_agg</c> / <c>trend_backward_agg</c> / argmax 的 <c>d</c>。
    /// </summary>
    public static class DiseaseAggregator
    {
        /// <summary>空集合的 <c>DominantDiseaseId</c> 哨兵。</summary>
        public const int NoDisease = -1;

        /// <summary>
        /// 聚合一组已求值的病程截面 —— GDD F5 `:1300-1303` + `:1119-1125`。
        /// </summary>
        /// <param name="positions">该病人身上全部 d 的已求值截面(可为 null / 空)。</param>
        /// <returns>
        /// <see cref="AggregatePosition"/>。<b>空集合 ⇒ <c>HasAny = false</c>,
        /// <c>PositionAgg = 0</c>,<c>DominantDiseaseId = -1</c></b> —— 调用方须先判
        /// <see cref="AggregatePosition.HasAny"/>,不得把 <c>0</c> 当作「健康」直接喂 F4。
        /// </returns>
        /// <remarks>
        /// <para><b>两个 max 是独立求的</b>(GDD `:1301-1303`):<c>position_agg</c> 与
        /// <c>trend_backward_agg</c> 各自取大,<b>不保证来自同一个 d</b>。
        /// 这不是笔误 —— GDD 明写两者各自 `max over d`。F4 的「非上升期门」读
        /// <c>trend_backward_agg</c>,死亡/昏迷支读 <c>position_agg</c> 与它的 d。</para>
        ///
        /// <para><b>并列裁决 = <c>DiseaseId</c> 最小者</b>(GDD `:1120`)。实现写成
        /// 「大于,或等于且 id 更小」才替换 —— 只写「大于」会让先到者胜,结果依赖遍历序,
        /// 破逐位重放。见类注释。</para>
        /// </remarks>
        public static AggregatePosition Aggregate(IReadOnlyList<DiseasePosition> positions)
        {
            if (positions == null || positions.Count == 0)
                return new AggregatePosition(Fix.Zero, NoDisease, Fix.Zero, false);

            long bestPositionRaw = positions[0].Position.Raw;
            int dominantId = positions[0].DiseaseId;
            long bestTrendRaw = positions[0].TrendBackward.Raw;

            // 从 1 起 —— 第 0 项已作为初值,重扫会白跑一次且让并列裁决多一个分支。
            for (int i = 1; i < positions.Count; i++)
            {
                DiseasePosition p = positions[i];

                // position_agg 的 argmax:严格更大才换;并列时 id 更小者胜。
                if (p.Position.Raw > bestPositionRaw
                    || (p.Position.Raw == bestPositionRaw && p.DiseaseId < dominantId))
                {
                    bestPositionRaw = p.Position.Raw;
                    dominantId = p.DiseaseId;
                }

                // trend_backward_agg:纯 max,无 argmax 语义(GDD 不要求它的 d)。
                if (p.TrendBackward.Raw > bestTrendRaw)
                    bestTrendRaw = p.TrendBackward.Raw;
            }

            return new AggregatePosition(
                new Fix(bestPositionRaw), dominantId, new Fix(bestTrendRaw), true);
        }
    }
}
