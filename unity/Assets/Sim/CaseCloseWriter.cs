// M2 接线轮阶段 2 · 批次 D —— CaseClosed 写者(37 病例系统,主机侧生产写入通道)。
//
// 权威来源:
//   design/registry/entities.yaml —— SimEvent.Kind.CaseClosed:
//     stream: case · author: 37 病例系统(主机唯一 Append,ADR-008 §一)
//     payload_schema: "patient_id: i32; case_id: 三元组(只读关联字段);
//                      disease_set: DiseaseIdSet(结案快照); treated: bool(处置证据快照)"
//   design/gdd/case-system.md 规则六 —— 结案唯一出口 · 已处置前置 · 单例 · 不可撤销 · 幂等拒收
//     · F-37.2(已处置谓词)· AC-37-03(幂等拒收)· EC-37-1(复诊 = 新病例)
//   ADR-008 §一(病例流路由)· §三(载荷字段)· §四(处置证据窗口化 + 快照;方向订正 2026-09-17)
//   ADR-005(主机唯一 Append;Seq 由发号器给出 ⇒ header Seq = -1 哨兵)
//   ADR-009 §二(进流充要条件 = 模拟态)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder;本类内禁 `new PayloadRef(`)
//
// 核心机制:
//   - `CaseCloseDecider`(纯函数:决策)· `CaseCloseQuery`(纯函数:流前缀谓词)负责「该不该结」;
//     本类负责「结了就写」—— 决策 / 查询 / 写入三层分离,与 `CaseOpenDecider` / `CaseOpenWriter`
//     的纯函数族并列(同 `CaseOpenWriter` 形状)。
//   - 前置条件在**写者内**按 GDD 复核(不信任调用方转述),且**统一经 `CaseCloseDecider.Decide`
//     裁决** —— 与决策纯函数**同源**,不另立一份判序(判序改动只在一处,ADR-008 §四闭环纪律):
//       ① 案件已结 —— `CaseCloseQuery.IsClosed`(流序扫描)⇒ **幂等拒收**;
//       ② 案件未开 —— `CaseCloseQuery.IsOpen`(= 开过 ∧ ¬已结)⇒ 拒绝;
//       ③ 未勾选「已处置」⇒ 拒绝;
//       ④ 窗口 [caseId.Tick, tick] 内无处置事件 —— `CaseCloseQuery.HasTreatmentInWindow` ⇒ 拒绝;
//     任一不满足 ⇒ **零写入**并返回具名结果码(调用方可区分,不静默)。
//   - 载荷按 registry payload_schema 全字段经 `IPayloadEncoder.Encode`;其中 `treated` 由本类
//     **自算**(= 窗口内真有无处置,写入分支上恒 true)—— 非调用方转述;`case_id` 落字段
//     (与 CaseOpened 不同:结案需精确关联被结的那一案,复诊场景下三元组不可由事件头唯一派生)。
//   - 事件头:`Patient = 病例所属病人`(取 caseId.Patient,非 None —— 高水位重构与排序键都依赖);
//     `Seq = -1` 未发号哨兵,由 EventStream 在 Append 时发号(O-1)。
//
// ⚠️ 登记项(本批不裁,GDD/装配未到位,见交付报告):
//   - `disease_set`(结案快照)由**调用方**给出 —— 与 `CaseOpenWriter.disease_snapshot` **同一口径**
//     (9 的病种状态在解码侧,Sim 侧够不着;已解码字段由调用方喂入)。
//   - 生产触发点(结案 UI「已处置」勾选 + 确认 → 本写者)尚未接线:归 37/42 的交互装配轮。
//   - `IEventAuthority.IsHost` 门不在本件:与既有写者(CaseOpenWriter / PatientSpawner /
//     PrescribeFlow / StructureKinds / HostEmergencyProcessor)同格 —— 主机唯一由「谁调用写者」
//     保证(ADR-005),客户端写通道的统一门禁归 45 轮。
//   - 在场(IPresenceQuery)前置**刻意未查**:GDD 规则六列出的结案前置(已处置 / 单例 / 不可撤销 /
//     幂等拒收)不含「病人在场」—— 结案是对**既有**病例的状态收口,不以病人在场的当下状态为条件
//     (与立案 GDD 规则二的「病人在场 + 就诊交互」不同)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="CaseCloseWriter.TryClose"/> 的结果码(零写入分支亦具名,不静默)。
    /// 与 <see cref="CaseCloseDecision"/> **一一对应**(写入成功分支除外)。</summary>
    public enum CaseCloseWriteResult
    {
        /// <summary>已结案:一条 <see cref="EventKind.CaseClosed"/> 入病例流(本次真正写入)。</summary>
        Closed = 0,

        /// <summary>拒绝 —— 案件已结:同 caseId 的第二条 <see cref="EventKind.CaseClosed"/>
        /// 不产生(GDD 规则六-4 幂等拒收 / AC-37-03);零写入。</summary>
        RejectAlreadyClosed = 1,

        /// <summary>拒绝 —— 案件未开:caseId 在流中从无开案(或三元组不匹配);
        /// 亦覆盖「从未开过该病人任何病例」、「三元组不吻合」、以及 caseId.Patient 为哨兵 /
        /// 负值的非法入参(该形参下无独立码,归本码);零写入。</summary>
        RejectNotOpen = 2,

        /// <summary>拒绝 —— 未勾选「已处置」(GDD 规则六 / 四-2);零写入。</summary>
        RejectNotChecked = 3,

        /// <summary>拒绝 —— 窗口 [caseId.Tick, tick] 内无处置事件(GDD F-37.2 / ADR-008 §四
        /// 2026-09-17 方向订正:处置 tick ∈ [立案 tick, 结案 tick]);零写入。</summary>
        RejectNoTreatment = 4,
    }

    /// <summary>
    /// 结案写者 —— 37 的 <c>CaseClosed</c> 生产写入通道(主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>与 <see cref="CaseCloseDecider"/> 的分工</b>:decider 是纯函数(输入布尔,输出决策),
    /// 供无 sink 的场景与测试复用;本类把同样的 GDD 前置条件对着**真流**复核(经
    /// <see cref="CaseCloseQuery"/> 求值布尔)后,交同一 decider 裁决,再写入。判序真源仍只有一处。</para>
    /// <para><b>只写 <c>CaseClosed</c></b>:立案 / 同源 / 判断记录三支各有写者,不归本件。</para>
    /// </remarks>
    public sealed class CaseCloseWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IPayloadDecoder _decoder;
        private readonly IReadOnlyList<SimEvent> _streamEvents;

        /// <summary>
        /// 构造结案写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 §① 第八抽象点)——
        /// <see cref="CaseCloseQuery.IsClosed"/> 已改为载荷解码口径(接线轮 2026-10-15),须传入。</param>
        /// <param name="streamEvents">
        /// 事件流的**只读视图**(生产 = <c>bag.Stream.Events</c>)—— 本类需要两条流的前缀:
        /// 病例流(判 <c>IsOpen</c> / <c>IsClosed</c>)与病史流(判窗口内处置证据)。
        /// <see cref="CaseCloseQuery"/> 的两个谓词各自按 Kind / Patient 过滤,其余 Kind 天然忽略,
        /// 故传入混流全集即可(同 <see cref="CaseOpenWriter"/> 的口径)。
        /// </param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public CaseCloseWriter(IEventSink eventSink, IPayloadEncoder encoder,
                               IPayloadDecoder decoder,
                               IReadOnlyList<SimEvent> streamEvents)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _streamEvents = streamEvents ?? throw new ArgumentNullException(nameof(streamEvents));
        }

        /// <summary>
        /// 结案(GDD 规则六):前置条件全过则写一条 <see cref="EventKind.CaseClosed"/>。
        /// </summary>
        /// <param name="tick">结案 tick —— 即玩家确认结案的那一 tick;它同时是本事件的
        /// <c>SimEvent.Tick</c>,也是处置证据窗口的上界(闭区间)。</param>
        /// <param name="caseId">被结案件的 <c>case_id</c> 三元组(由调用方持——通常取自
        /// <c>CaseStreamQuery.OpenCaseOf</c>)。它提供:① 事件头 <c>Patient</c>(= <c>caseId.Patient</c>);
        /// ② 窗口下界(= <c>caseId.Tick</c>);③ 幂等关联键(<c>IsClosed</c> 按精确三元组匹配,
        /// 故复诊新案不被前案已结所误判 —— EC-37-1)。</param>
        /// <param name="diseaseSet">
        /// 结案时刻的病种快照(registry: <c>disease_set: DiseaseIdSet</c>,版本化整数
        /// <b>位集</b>,位序 <c>bit index = DiseaseId − 1</c> —— 非 FixSet,承 D-21-17;
        /// 纯伤情案可为空集)。由调用方给出(见类头登记项)。
        /// </param>
        /// <param name="isChecked">玩家是否勾选「已处置」(GDD 规则六-4 / 四-2 的处置证据前提;
        /// 不勾选一律拒绝,即便窗口内确有处置 —— checkbox 是玩家的**自述**,证据是流的事实,
        /// 二者须同时成立)。</param>
        /// <returns>结果码;仅 <see cref="CaseCloseWriteResult.Closed"/> 意味着真的写入了一条。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// (tick 域与 <see cref="CaseOpenWriter"/> / <c>PatientSpawner</c> 同口径:
        /// 入参非法 = 编程错误,fail-loud)。</exception>
        public CaseCloseWriteResult TryClose(long tick, CaseId caseId, in DiseaseIdSet diseaseSet, bool isChecked)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            // ── 前置 ②(输入非法):病例所属病人为哨兵 / 负 id ⇒ 零写入 ──
            // 该形参下无独立结果码(GDD 规则六未列此形态),归 RejectNotOpen;
            // CaseOpened 恒携真病人(见 CaseOpenWriter),故负 id 必无匹配开案,本守卫与
            // decider 的自然结论一致 —— 显式短路只为显意,不改语义。
            if (caseId.Patient < 0)
                return CaseCloseWriteResult.RejectNotOpen;

            // ── 前置(四条):全部经流前缀谓词求值,交 CaseCloseDecider **同一判序**裁决 ──
            // 已结 / 未开的先后与 decider 内一致(具体者先判:「已结」蕴含「曾开」);
            // 本类不复制判序,只搬运布尔 —— 判序真源唯一(ADR-008 §四闭环纪律)。
            bool isClosed = CaseCloseQuery.IsClosed(_streamEvents, caseId, _decoder);
            bool isOpen = CaseCloseQuery.IsOpen(_streamEvents, caseId, _decoder);
            bool hasTreatmentInWindow = CaseCloseQuery.HasTreatmentInWindow(
                _streamEvents, caseId.Patient, caseId.Tick, tick);

            switch (CaseCloseDecider.Decide(isOpen, isClosed, hasTreatmentInWindow, isChecked))
            {
                case CaseCloseDecision.CanClose:
                    break; // 前置全过 ⇒ 落写入

                case CaseCloseDecision.RejectAlreadyClosed:
                    return CaseCloseWriteResult.RejectAlreadyClosed;
                case CaseCloseDecision.RejectNotOpen:
                    return CaseCloseWriteResult.RejectNotOpen;
                case CaseCloseDecision.RejectNotChecked:
                    return CaseCloseWriteResult.RejectNotChecked;
                case CaseCloseDecision.RejectNoTreatment:
                    return CaseCloseWriteResult.RejectNoTreatment;

                default:
                    // decider 的决策集是闭集;新增成员而未同步本 switch 属编程错误,fail-loud。
                    throw new InvalidOperationException(
                        "CaseCloseDecider 返回了未映射的决策 —— 新增决策枚举须同步本 switch");
            }

            // ── 载荷:registry payload_schema 四字段 ──
            // treated = **本类自算的窗口证据**(非调用方转述);走到此分支 hasTreatmentInWindow 恒 true。
            // case_id 落字段:复诊场景下「被结的是哪一案」不可由事件头唯一派生(同病人多案),须显式携带。
            var payload = new CaseClosedPayload(caseId.Patient, caseId, diseaseSet, hasTreatmentInWindow);
            var payloadRef = _encoder.Encode(EventKind.CaseClosed, payload);

            // ── 事件头:Patient = 病例所属病人(非 None);Seq = -1 未发号哨兵(O-1)──
            _eventSink.Append(new SimEvent(
                tick, new PatientId(caseId.Patient), -1, EventKind.CaseClosed, payloadRef));

            return CaseCloseWriteResult.Closed;
        }
    }
}
