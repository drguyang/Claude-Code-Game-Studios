// M2 接线轮阶段 2 · 批次 D —— CaseOpened 写者(37 病例系统,主机侧生产写入通道)。
//
// 权威来源:
//   design/registry/entities.yaml —— SimEvent.Kind.CaseOpened:
//     stream: case · author: 37 病例系统(主机唯一 Append,ADR-008 §一)
//     payload_schema: "patient_id: i32; disease_snapshot: DiseaseIdSet(...); case_id: 三元组
//                      只读派生,不落字段"
//   design/gdd/case-system.md 规则二 —— 立案 A/B 两路径 · 立案时刻 = 就诊交互的那一 tick
//     · 「同一病人同时至多一个开案」(2026-09-17,AC-37-26)· 药材短缺(无病人)不立案
//     · case_id = (Tick, Patient, Seq) 三元组,由事件头派生,不落载荷
//   ADR-008 §一(病例流路由)· §二(case_id 三元组)
//   ADR-005(主机唯一 Append;Seq 由发号器给出 ⇒ header Seq = -1 哨兵)
//   ADR-009 §二(进流充要条件 = 模拟态;A/B 两路径入流义务相同)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder;本类内禁 `new PayloadRef(`)
//
// 核心机制:
//   - `CaseOpenDecider`(纯函数)负责「该不该开」;本类负责「开了就写」—— 决策与写入分离,
//     与 `CaseOpenDecider` / `CaseCloseDecider` / `PatternDetector` 的纯函数族并列。
//   - 前置条件在**写者内**按 GDD 复核(不信任调用方转述):
//       ① 病人存在 —— `IPresenceQuery.IsPresent`(药材短缺 / 无病人 ⇒ 不立案);
//       ② 未已有开案 —— `CaseStreamQuery.HasOpenCase`(流前缀纯函数,AC-37-26);
//     两者任一不满足 ⇒ **零写入**并返回具名结果码(调用方可区分,不静默)。
//   - 载荷按 registry payload_schema 全字段经 `IPayloadEncoder.Encode`(patient_id +
//     disease_snapshot 两字段;case_id **不落字段** —— 由事件头 (Tick, Patient, Seq) 派生)。
//   - 事件头:`Patient = 病例所属病人`(非 None —— 高水位重构与排序键都依赖它);
//     `Seq = -1` 未发号哨兵,由 `EventStream` 在 Append 时发号(O-1)。
//
// ⚠️ 登记项(本批不裁,GDD/装配未到位,见交付报告):
//   - `disease_snapshot` 由**调用方**给出(9 的病种状态在解码侧,Sim 侧够不着;与
//     `PoiStateMachine` 「读侧由调用方喂已解码字段」的甲案同构)。
//   - 生产触发点(世界空间就诊交互 → 本写者)尚未接线:归 37/4/8 的交互装配轮。
//   - `IEventAuthority.IsHost` 门不在本件:与既有 8 个写者(PatientSpawner /
//     PrescribeFlow / StructureKinds / HostEmergencyProcessor)同格 —— 主机唯一由
//     「谁调用写者」保证(ADR-005),客户端写通道的统一门禁归 45 轮。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="CaseOpenWriter.TryOpen"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum CaseOpenWriteResult
    {
        /// <summary>已立案:一条 <see cref="EventKind.CaseOpened"/> 入病例流(本次真正写入)。</summary>
        Opened = 0,

        /// <summary>回到已开案 —— 同一病人已有一个开着的病例(GDD 规则二 / AC-37-26);
        /// 第二次交互不是新案,零写入。</summary>
        ReturnedExisting = 1,

        /// <summary>不立案 —— 无病人(药材短缺等 GDD 规则二注:37 注入的三个条目里只有两个立案);
        /// 亦覆盖 `PatientId.None` / 负 id 入参(哨兵不得开出病例),零写入。</summary>
        NoPatient = 2,

        /// <summary>不立案 —— 病人不在场(`IPresenceQuery.IsPresent` false)。
        /// GDD 规则二的两条路径都以「病人在场 + 就诊交互」为前提;零写入。</summary>
        PatientAbsent = 3,
    }

    /// <summary>
    /// 立案写者 —— 37 的 <c>CaseOpened</c> 生产写入通道(主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>与 <see cref="CaseOpenDecider"/> 的分工</b>:decider 是纯函数(输入布尔,输出决策),
    /// 供无 sink 的场景与测试复用;本类把同样的 GDD 前置条件对着**真流**复核后写入。
    /// 两条路径(A 事件注入 / B 自然就诊)在本类同一入口 —— GDD 规则二订正后的口径:
    /// 入流义务与触发者无关(ADR-009 §二),路径只作记录不改行为。</para>
    /// <para><b>只写 <c>CaseOpened</c></b>:结案 / 同源 / 判断记录三支各有写者,不归本件。</para>
    /// </remarks>
    public sealed class CaseOpenWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPresenceQuery _presence;
        private readonly IPayloadEncoder _encoder;
        private readonly IReadOnlyList<SimEvent> _streamEvents;

        /// <summary>
        /// 构造立案写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="presence">在场查询(前置条件 ①「病人存在」的真源)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <param name="streamEvents">
        /// 事件流的**只读视图**(生产 = <c>bag.Stream.Events</c>)—— 前置条件 ②
        /// 「未已有开案」由 <see cref="CaseStreamQuery.HasOpenCase"/> 在此视图上求值;
        /// 该查询只看 CaseOpened / CaseClosed 两类,故传入混流全集即可(其余 Kind 天然忽略)。
        /// </param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public CaseOpenWriter(IEventSink eventSink, IPresenceQuery presence,
                              IPayloadEncoder encoder, IReadOnlyList<SimEvent> streamEvents)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _streamEvents = streamEvents ?? throw new ArgumentNullException(nameof(streamEvents));
        }

        /// <summary>
        /// 立案(GDD 规则二):前置条件全过则写一条 <see cref="EventKind.CaseOpened"/>。
        /// </summary>
        /// <param name="tick">立案 tick —— 即就诊交互发生的那一 tick;它同时是本事件的
        /// <c>SimEvent.Tick</c>,因此就是 <c>case_id</c> 的第一元与立案时刻(载荷不落 opened_tick)。</param>
        /// <param name="patient">病例所属病人(事件头 Patient;哨兵 / 负值 ⇒ <see cref="CaseOpenWriteResult.NoPatient"/>)。</param>
        /// <param name="diseaseSnapshot">
        /// 立案时刻的病种快照(registry: <c>disease_snapshot: DiseaseIdSet</c>,版本化整数
        /// ordinal 位集 —— 非 FixSet,承 D-21-17)。由调用方给出(见类头登记项)。
        /// </param>
        /// <returns>结果码;仅 <see cref="CaseOpenWriteResult.Opened"/> 意味着真的写入了一条。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// (tick 域与 <c>PatientSpawner</c> 同口径:入参非法 = 编程错误,fail-loud)。</exception>
        public CaseOpenWriteResult TryOpen(long tick, PatientId patient, in DiseaseIdSet diseaseSnapshot)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            // ── 前置 ① 病人存在(GDD 规则二:药材短缺无病人 ⇒ 不立案)──
            // 哨兵 / 负 id 一并在此拒:PatientId.None 写进病例流会污染 case_id 三元组
            // 与 max(patient_id) 高水位(ADR-007 §四),属输入非法,零写入具名返回。
            if (patient.IsNone)
                return CaseOpenWriteResult.NoPatient;

            if (!_presence.IsPresent(patient))
                return CaseOpenWriteResult.PatientAbsent;

            // ── 前置 ② 同一病人同时至多一个开案(GDD 规则二 2026-09-17 / AC-37-26)──
            if (CaseStreamQuery.HasOpenCase(_streamEvents, patient.Value))
                return CaseOpenWriteResult.ReturnedExisting;

            // ── 载荷:registry payload_schema 两字段(case_id 不落字段)──
            var payload = new CaseOpenedPayload(patient.Value, diseaseSnapshot);
            var payloadRef = _encoder.Encode(EventKind.CaseOpened, payload);

            // ── 事件头:Patient = 病例所属病人;Seq = -1 未发号哨兵(O-1),由发号器给出 ──
            _eventSink.Append(new SimEvent(
                tick, patient, -1, EventKind.CaseOpened, payloadRef));

            return CaseOpenWriteResult.Opened;
        }
    }
}
