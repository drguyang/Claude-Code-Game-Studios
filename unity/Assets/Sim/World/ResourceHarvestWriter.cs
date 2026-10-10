// M2 接线轮阶段 2 · 批次 D —— ResourceHarvested 写者(17 采集,主机侧生产写入通道)。
//
// 权威来源:
//   design/registry/entities.yaml —— SimEvent.Kind.ResourceHarvested:
//     stream: world · author: 17 采集(主机裁决;ADR-009 §七 Guidelines 4)
//     payload_schema: "instance_id: i64; node_id: i32; gather_seq: i32; qty: i32;
//                      out_quality: 枚举(品级档,经 QualityCap 截断);raw_quality 刻意不落流"
//   design/gdd/foraging.md 规则二(采集事实走 ResourceHarvested · Amendment K)·
//     规则三(instance_id 铸造权 = 主机唯一,铸造点 = DropSpawned)·
//     规则四(品级抽取 + QualityCap 截断须落流)· 规则六(动作原子性:完整完成才发)·
//     库存满载 ⇒ 采集失败零事件(:411)
//   ADR-009 §二 / §三(世界流三态 · 模拟态进流)· §七 Guidelines 4(写者 = 17)
//   ADR-009 §七(拾取三段式:意图事件 + 当下判距 + 宽容半径 —— **不在本件**)
//   ADR-015 §七 / OQ-4-11(node_id = 逻辑层节点稳定标识,非 spawn_anchor 格坐标)
//   ADR-005(主机唯一 Append)· ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder)
//
// 职责界定(勘察结论,交付报告同文):
//   - **本写者只管「采集成功」那一跳** —— 一次完整采集动作里属于 17 的那条采集事实。
//     三段式(意图 → 当下判距 → 效果进流)的**判距不在本件**:17 的采集意图 / 判距 /
//     目标选择归 4 的交互判距 + 17 装配层(尚未落),本件是判距通过之后的**效果写入**。
//   - **同一 tick 连发三条的另两条不在本件**:`DropSpawned`(身份出生 = instance_id 的
//     **铸造点**,规则三)与 `DropClaimed`(立即归属)的 author = 20 库存与物品,
//     写者未落(豁免表登记)。三者同 tick 连发的**编排**归 17 的装配轮(登记项)。
//   - **instance_id 不在本件铸造**:规则三明定铸造点 = DropSpawned;本件收下已铸 id
//     (形参 <c>ItemInstanceId</c>,类型本身即「机制 A 发号」的记号)。
//   - **gather_seq 由调用方给出**:它的计源 = 本 Kind 的节点计数(foraging F-17-1),
//     而「数世界流」需解码(PayloadCodec 住 Sim.Codec,门 A 下本类够不着)—— 与
//     `PoiStateMachine` 读侧「由调用方喂已解码字段」的甲案同构。
//   - **库存满载 / 动作被打断 ⇒ 采集失败零事件**:那是 20 的容量预算与 17 动作层的
//     前置,本件无从复核(登记项);本件只做**入参合法性** fail-loud。
//
// ⚠️ 登记项(本批不裁):生产触发点(采集动作完成 → 本写者)未接线;三条一组的同 tick
//    编排、库存满载前置、QualityCap 截断的调用面均归 17/20 的装配轮。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 采集事实写者 —— 17 的 <c>ResourceHarvested</c> 生产写入通道(主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>世界流事件头</b>:<c>Patient = PatientId.None</c>(ADR-007 §四 —— 不污染
    /// <c>max(patient_id)</c> 高水位;与 <c>PoiStateChanged</c> / <c>StructurePlaced</c> 同格);
    /// <c>Seq = -1</c> 未发号哨兵,由 <c>EventStream</c> 发号(O-1)。</para>
    /// <para><b>只写 <c>ResourceHarvested</c></b>:同一动作的身份两条归 20 的写者
    /// (见类头职责界定)。</para>
    /// </remarks>
    public sealed class ResourceHarvestWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;

        /// <summary>
        /// 构造采集事实写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public ResourceHarvestWriter(IEventSink eventSink, IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>
        /// 记一条采集事实(GDD foraging 规则二):一次**成功且完整**的采集动作完成后调用。
        /// </summary>
        /// <param name="tick">动作完成的 tick(主机裁决通过、铸 id 之后同一 tick)。</param>
        /// <param name="instanceId">
        /// 已铸造的实例 id(规则三:铸造点 = <c>DropSpawned</c>,本件只引用同一 id)。
        /// </param>
        /// <param name="nodeId">逻辑层节点稳定标识(ADR-015 / OQ-4-11 —— **非**格坐标)。</param>
        /// <param name="gatherSeq">该节点已发出的 <c>ResourceHarvested</c> 计数(F-17-1;调用方求值)。</param>
        /// <param name="qty">本次采量(GDD: <c>qty ≥ 1</c>)。</param>
        /// <param name="outQuality">品级档,已过 <c>QualityCap</c> 截断(GDD: <c>∈ [1, MAX_QUALITY]</c>;
        /// 上界为数据驱动,本件只校验下界)。</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 任一入参出界(<paramref name="tick"/> &lt; 0 / <paramref name="instanceId"/> &lt; 0 /
        /// <paramref name="nodeId"/> &lt; 0 / <paramref name="gatherSeq"/> &lt; 0 /
        /// <paramref name="qty"/> &lt; 1 / <paramref name="outQuality"/> &lt; 1)
        /// —— 入参非法 = 编程错误,fail-loud;「采集失败」不是本异常的语义(失败 = 调用方不调本方法)。
        /// </exception>
        public void Record(long tick, ItemInstanceId instanceId, int nodeId,
                           int gatherSeq, int qty, int outQuality)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (instanceId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId), instanceId.Value,
                    "instance_id 走 ADR-006 Amendment B 机制 A 计数器,不得为负");
            if (nodeId < 0)
                throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId,
                    "node_id 须为逻辑层稳定标识(OQ-4-11)");
            if (gatherSeq < 0)
                throw new ArgumentOutOfRangeException(nameof(gatherSeq), gatherSeq,
                    "gather_seq 为该节点已发计数,不得为负(F-17-1)");
            if (qty < 1)
                throw new ArgumentOutOfRangeException(nameof(qty), qty, "GDD foraging: qty ≥ 1");
            if (outQuality < 1)
                throw new ArgumentOutOfRangeException(nameof(outQuality), outQuality,
                    "GDD foraging: out_quality ∈ [1, MAX_QUALITY]");

            // 载荷:registry payload_schema 五字段全整数;raw_quality **刻意不落流**
            // (可由 SplitMix64(WorldSeed, "gather", node_id, gather_seq) 三项重算 —— 存它 = 第二真源)。
            var payload = new ResourceHarvestedPayload(
                instanceId.Value, nodeId, gatherSeq, qty, outQuality);
            var payloadRef = _encoder.Encode(EventKind.ResourceHarvested, payload);

            // 事件头:Patient = None(世界流,不污染高水位 —— 与 PoiStateChanged 同格);
            // Seq = -1 未发号哨兵(O-1),由发号器给出。
            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.ResourceHarvested, payloadRef));
        }
    }
}
