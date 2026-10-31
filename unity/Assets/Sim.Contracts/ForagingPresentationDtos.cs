// story-005 · AC-17-09 —— 17 的呈现层 DTO(反幻想面)。
//
// 权威来源:
//   design/gdd/foraging.md 规则四 注 · §Edge Cases「`QualityCap` 截断后 `raw_quality` 丢失」:
//     「17 **不呈现**『你本可采到更高档』」—— 把被截断的信息还给玩家 = 拆掉反幻想约束
//   design/gdd/foraging.md AC-17-09(BLOCKING):递归 type-surface 反射扫描,无「原始品级 / 本可采到」字段
//   story production/epics/foraging/story-005-action-atomicity-failure-convergence.md
//     Implementation Note 4:`GatherAffordanceDto`(可采提示,整数格邻域布尔 / 节点类,**无余量数字**)
//   ADR-013(42 只渲染、永不持状态)· ADR-015 §三(单一整数格)· ADR-009 §四(派生态)
//
// ── 三个刻意不存在的字段(每一条都有理由)──
// 1. **余量数字**:余量是**主机裁决的输入**(F-17-3),不是呈现物。给出去等于把
//    「这株还能采几次」变成客户端可读的第二个真源,且直接破坏反幻想(玩家会算期望)。
//    呈现只需要一个布尔:「现在能不能采」。
// 2. **原始品级 / 本可采到**:`QualityCap` 截断是设计的一部分,不呈现。
// 3. **时间戳 / 冷却剩余**:同上 —— 那是把主机的判定条件抄给玩家。
//
// ⚠️ 本 DTO 是 **17 侧的呈现契约**;42 如何渲染它归 42(ADR-013「只渲染,永不持状态」)。
//    在 42 的 UX Flag ③ 交付之前,本件是**契约先行**,不是「已呈现」。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 「这个节点现在能不能采」—— 17 交给呈现层的**唯一**采集提示(整数域,无余量数字)。
    /// </summary>
    /// <remarks>
    /// <para><b>纯布尔,不给数字</b>:见文件头「三个刻意不存在的字段」①。玩家拿到的信息
    /// 是「能 / 不能」,不是「还剩 2.5 份」。</para>
    /// <para><b>`IsHarvestable` 由主机在裁决时求值</b>(`NodeRemaining.CanHarvest`),
    /// 客户端只读结果 —— 客户端自判余量 = 第二真源(GDD F-17-3 求值口径 ④)。</para>
    /// </remarks>
    public readonly struct GatherAffordanceDto
    {
        /// <summary>资源节点 id(整数稳定标识,非格坐标;ADR-015 §三)。</summary>
        public readonly int NodeId;

        /// <summary>节点所在整数格(呈现侧用来定位提示)。</summary>
        public readonly Int3 Cell;

        /// <summary>**主机裁决后**的「此刻可采」布尔(不足 / 枯节点 ⇒ <c>false</c>)。</summary>
        public readonly bool IsHarvestable;

        /// <summary>节点种类 ordinal(三品种集,规则一;呈现侧据此选素材)。</summary>
        public readonly int NodeKindOrdinal;

        /// <summary>构造提示。</summary>
        public GatherAffordanceDto(int nodeId, Int3 cell, bool isHarvestable, int nodeKindOrdinal)
        {
            NodeId = nodeId; Cell = cell; IsHarvestable = isHarvestable;
            NodeKindOrdinal = nodeKindOrdinal;
        }
    }
}
