// 照护事件写者 —— `CareApplied` 的生产写入通道(24 医馆与案头 / 8 判断链的护理动作入向)。
//
// 权威来源:
//   design/registry/entities.yaml —— SimEvent.Kind.CareApplied:
//     stream: history · author: "24 医馆与案头(以入向事件写入;disease-simulation.md …)"
//     payload_schema: "tick: i64; action_id: i32(护理动作表 ordinal,K ≤ |护理动作表|);
//                      caregiver: i32; phase: 枚举(起 止)。一条事件表示起或止,
//                      写入期校验 t(止) ≥ t(起);语义 = 玩家主动护理动作;
//                      放医馆本身不计数,EnvMod 不得推出本事件"
//   design/gdd/disease-simulation.md:207-228 —— 照护事件化(用户裁定 2026-09-16 三轮):
//     照护 = **玩家主动护理动作**的**起止事件**;「放医馆」本身**不是**照护事件;
//     **`EnvMod` ≠ 照护事件**(不得读取、不得用于照护判定)。
//   design/gdd/disease-simulation.md:1126-1133 —— `last_intervention` 的照护支
//     (派生量:max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP))
//     —— 消费者侧,归 9 的 F4;本类只负责把「起 / 止」写成流里可被它读到的形状。
//   design/gdd/disease-simulation.md:344 —— 校验 13 ②:护理动作事件须满足
//     `t(止) ≥ t(起)` 且 `K ≤ |护理动作表|`(种类界;条数界见下条)。
//   design/gdd/disease-simulation.md:1784 + entities.yaml CARE_EVENTS_PER_TICK ——
//     每病人每 tick 护理动作事件数上界,超界**拒收(丢弃 + 记账),不静默**;值 *待定*。
//   ADR-005(主机唯一 Append;Seq 由发号器给出 ⇒ header Seq = -1 哨兵)
//   ADR-008 §一(按 Kind 纯函数路由)· ADR-009 §二(进流充要条件 = 模拟态)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder;本类内禁 `new PayloadRef(`)
//   ADR-031 §②(读侧对称抽象点 IPayloadDecoder —— 「止」须与流里的「起」配对,须读载荷内容)
//
// 核心机制:
//   - 前置条件在**写者内**按 GDD 复核(不信任调用方转述),任一不满足 ⇒ **零写入**并返回
//     具名结果码(调用方可区分,不静默):
//       ① 病人合法 —— `PatientId.None` / 负 id 不得入病史流(污染 `max(patient_id)` 高水位,
//          ADR-006 Amendment B / ADR-007 §四);
//       ② `phase` ∈ {起, 止}(序数见下「phase 序数」注)—— 越界拒,不静默取值;
//       ③ `actionId` ≥ 0(种类界 `K ≤ |护理动作表|` 的**下界**侧;上界见「登记项」);
//       ④ `caregiver` ≥ 0(全库无定义 ⇒ 只做非负校验,不编造上界);
//       ⑤ 密度界 —— 同 (tick, 病人) 的护理动作事件数 < `CARE_EVENTS_PER_TICK`
//          (GDD :1784「超界拒收」);该旋钮值 *待定* ⇒ 由装配方注入,`NoDensityCap` = 未标定;
//       ⑥ `止` 须有可配对的 `起` —— 流内同 (病人, 动作) 存在 tick ≤ 本次 tick 的「起」
//          (GDD :344 的 `t(止) ≥ t(起)` 的可判定形式:无起可配 ⇒ 止的 tick 无从比较 ⇒ 拒)。
//          ⚠️ **本前置隐含一个未裁决前提**:24 的护理动作**一定**先发「起」。
//             「重连补记 / 玩家中途离场」两场景是否允许**只发止**,已登记
//             `clinic-machine.md` 的 **`OQ-24-8`**(2026-10-22 用户裁定「先不裁,我去查 24 的生命周期」)
//             ⇒ **现状 = 保持拒收**(保守方向:少写一条,不会伪造「止」)。
//             若 OQ-24-8 裁出「允许只发止」,本前置与 `test_careApplied_stopWithoutStart_noWrite`
//             须同批改,不得只改一边。
//   - 载荷按 registry payload_schema 四字段经 `IPayloadEncoder.Encode`。
//   - 事件头:`Patient = 照护对象`(非 None —— 折叠与全序键都依赖它);`Seq = -1` 未发号哨兵,
//     由 `EventStream` 在 Append 时发号(O-1)。
//
// ⚠️ phase 序数(必读):`0 = 起 / 1 = 止` 有**三处权威**,序数不可能再分叉
//   (2026-10-22 用户裁定「甲+乙 都做」,承 `PatientOutcome` 的「三处齐备」先例):
//     ① `entities.yaml:2186` 写死 `phase: 枚举(起=0, 止=1)`(权威件);
//     ② `Sim.Contracts.CarePhase`(唯一 C# 定义);
//     ③ 机械断言钉住两者一致(`care_applied_writer_test.cs` 的
//        `test_carePhase_ordinalsMatchRegistry` / `test_carePhase_enumMatchesForwardingConstants`)。
//   本类**直接引用 `DiseaseCourse.CarePhaseStart` / `CarePhaseStop`,不复制第二份序数** ——
//   而那两个常量现在是 `(int)CarePhase.X` 的**转发**,不是独立硬编码。
//   🔁 原文写「`entities.yaml` 只写『phase: 枚举(起 止)』,**全库没有序数定义**;`0 = 起 / 1 = 止`
//      是落地约定;若将来 registry 补出序数定义,须同时改 `DiseaseCourse` 与本注释」——
//      **已失效**(原文不删):registry 已于同日补出序数,`DiseaseCourse` 与本注释亦已同步。
//
// ⚠️ **有写者 ≠ 已接线**:生产触发点(24 的护理交互 / 医馆护理台 → 本写者 / 4 的交互装配)
//   **尚未接线** —— 与 `CaseOpenWriter` 同格。本类只保证「谁调用它,流里就有一条合规的
//   `CareApplied`」;照护功能在游戏内**并未因此可用**。
//
// ⚠️ 登记项(本批不裁):
//   - `actionId` 的**上界**无从校验:`K ≤ |护理动作表|` 只界种类,而全库**没有**护理动作表的
//     表体(24 的 GDD 未见该表,entities.yaml 只引用「护理动作表」名)。⇒ 本类只做 `≥ 0`;
//     上界校验归 24 的表成文轮(届时经 ADR-014 烘焙,可在此处加一道上界门)。
//   - `caregiver` 的合法域:**全库无权威定义**(无枚举、无表、无上界)。⇒ 只做非负校验。
//     「caregiver 是不是玩家 id / 是否须与 `player_id` 共用 id 空间」**未查证**,不在此断言。
//   - `止` 的配对键 = `(病人, actionId)`;**不含 caregiver**(GDD 未把照护者列入配对语义,
//     加入 = 自行发明规则)。同一 `(病人, actionId)` 在一条「起」之后连发两条「止」**不拒** ——
//     GDD 无「止不得重复」的裁决,本类不自造。
//   - `IEventAuthority.IsHost` 门不在本件:与既有写者(CaseOpenWriter / CaseCloseWriter /
//     PatientSpawner / StructureKinds / HostEmergencyProcessor)同格 —— 主机唯一由「谁调用写者」
//     保证(ADR-005),客户端写通道的统一门禁归 45 轮。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;   // DiseaseCourse.CarePhaseStart / CarePhaseStop(→ Sim.Contracts.CarePhase 的转发)

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="CareAppliedWriter.TryApply"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum CareAppliedWriteResult
    {
        /// <summary>已写入:一条 <see cref="EventKind.CareApplied"/> 入病史流(本次真正写入)。</summary>
        Applied = 0,

        /// <summary>不写入 —— 照护对象非法(`PatientId.None` / 负 id)。哨兵入病史流会污染
        /// `max(patient_id)` 高水位(ADR-006 Amendment B),且折叠谓词按病人切分时会误吞。</summary>
        NoPatient = 1,

        /// <summary>不写入 —— `phase` 越界(只接受 0 = 起 / 1 = 止)。
        /// <para>⚠️ 越界**必须拒**:静默取默认值会让「止」被当成「起」写进流 ⇒ 照护支永不记录
        /// 「止」⇒ `last_intervention` 的照护支退化为「永不中止」(GDD :1126-1133)。</para></summary>
        InvalidPhase = 2,

        /// <summary>不写入 —— `actionId` 为负(种类界 `K ≤ |护理动作表|` 的 ordinal 不得为负;
        /// 上界无从校验,见类头登记项)。</summary>
        InvalidActionId = 3,

        /// <summary>不写入 —— `caregiver` 为负(全库无合法域定义 ⇒ 仅非负校验)。</summary>
        InvalidCaregiver = 4,

        /// <summary>不写入 —— 「止」无可配对的「起」:流内同 (病人, actionId) 无 tick ≤ 本次 tick
        /// 的「起」事件 ⇒ GDD :344 的 `t(止) ≥ t(起)` 无从求值,按拒收处理(不猜)。</summary>
        StopWithoutStart = 5,

        /// <summary>不写入 —— 同 (tick, 病人) 的护理动作事件数已达 `CARE_EVENTS_PER_TICK`
        /// (GDD :1784「超界拒收(丢弃 + 记账),不静默」)。仅当注入的上界 ≥ 0 时可达。</summary>
        RateCapExceeded = 6,
    }

    /// <summary>
    /// 照护事件写者 —— <c>CareApplied</c> 的生产写入通道(24 以入向事件写入 · 主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>只写 <c>CareApplied</c></b>:处置类(`EmergencyTreatmentApplied` / `DrugTreatmentApplied`)
    /// 与急救意图(`EmergencyAttempt`)各有写者,不归本件。</para>
    /// <para><b>「起」与「止」同一入口</b>:GDD 的语义是「一条事件表示起**或**止」,故用
    /// <c>phase</c> 形参区分,不拆两个方法(拆开会让「越界 phase」变成编译期不可表达,却也让
    /// 调用方无法把 24 的单一动作生命周期映射成一次调用)。</para>
    /// <para><b>密度界由装配方注入</b>:<c>CARE_EVENTS_PER_TICK</c> 的值仍是 *待定*
    /// (GDD :1784 / entities.yaml)⇒ 本类不写死数字,未标定时传
    /// <see cref="NoDensityCap"/>(= 不设界,并在文档里保持可见)。</para>
    /// </remarks>
    public sealed class CareAppliedWriter
    {
        /// <summary><c>CARE_EVENTS_PER_TICK</c> 未标定哨兵 —— 传此值 = 本次不设密度界。
        /// <para>⚠️ 用 `-1` 而不是 `0`:0 是**合法**的旋钮值(GDD :1769 同族旋钮明写「0 表示…」),
        /// 拿 0 当「不设界」会让「零事件允许」与「不设界」不可区分。</para></summary>
        public const int NoDensityCap = -1;

        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IPayloadDecoder _decoder;
        private readonly IReadOnlyList<SimEvent> _streamEvents;
        private readonly int _careEventsPerTickCap;

        /// <summary>
        /// 构造照护事件写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)—— 前置条件 ⑥「止须有可配对的起」
        /// 须读流内历史事件的 <c>Phase</c> / <c>ActionId</c>,而 <c>Sim</c> 够不着 <c>PayloadCodec</c>。</param>
        /// <param name="streamEvents">
        /// 事件流的**只读视图**(生产 = <c>bag.Stream.Events</c>)—— 前置 ⑤(密度界)与
        /// ⑥(起止配对)在此视图上求值;其余 Kind 天然被过滤掉。
        /// </param>
        /// <param name="careEventsPerTickCap"><c>CARE_EVENTS_PER_TICK</c> 的装配值
        /// (GDD :1784);未标定传 <see cref="NoDensityCap"/>。负值(除哨兵)视为编程错误。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="careEventsPerTickCap"/>
        /// 既非 <see cref="NoDensityCap"/> 又为负。</exception>
        public CareAppliedWriter(IEventSink eventSink, IPayloadEncoder encoder, IPayloadDecoder decoder,
                                 IReadOnlyList<SimEvent> streamEvents,
                                 int careEventsPerTickCap = NoDensityCap)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _streamEvents = streamEvents ?? throw new ArgumentNullException(nameof(streamEvents));
            if (careEventsPerTickCap < NoDensityCap)
                throw new ArgumentOutOfRangeException(nameof(careEventsPerTickCap), careEventsPerTickCap,
                    "CARE_EVENTS_PER_TICK 只接受 ≥ 0 的值或 NoDensityCap(-1)哨兵");
            _careEventsPerTickCap = careEventsPerTickCap;
        }

        /// <summary>
        /// 写一条照护动作事件(GDD :207-228 照护事件化):前置条件全过则写
        /// <see cref="EventKind.CareApplied"/>;任一不满足 ⇒ 零写入 + 具名结果码。
        /// </summary>
        /// <param name="tick">护理动作发生 / 结束的那一 tick —— 它同时是本事件的
        /// <c>SimEvent.Tick</c>;载荷的 <c>Tick</c> 字段与之**同值**(registry 把 tick 列在
        /// payload_schema 内,故照 registry 落字段,与 `PatientOutcomeChanged` 的「t_end 不落载荷」
        /// 不同 —— 那里 registry 未列该字段,此处列了)。</param>
        /// <param name="patient">照护对象(事件头 Patient)。</param>
        /// <param name="actionId">护理动作表 ordinal(registry `action_id: i32`);≥ 0。</param>
        /// <param name="caregiver">照护者(registry `caregiver: i32`);≥ 0,上界无权威定义。</param>
        /// <param name="phase">相位:<c>DiseaseCourse.CarePhaseStart</c>(0 = 起)/
        /// <c>DiseaseCourse.CarePhaseStop</c>(1 = 止);其余值 ⇒ <see cref="CareAppliedWriteResult.InvalidPhase"/>。</param>
        /// <returns>结果码;仅 <see cref="CareAppliedWriteResult.Applied"/> 意味着真的写入了一条。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// (tick 域与 <c>CaseOpenWriter</c> / <c>PatientSpawner</c> 同口径:入参非法 = 编程错误,fail-loud)。</exception>
        /// <exception cref="InvalidOperationException">池未驻留 / 引用越界 —— 由 <see cref="IPayloadDecoder.Decode{T}"/>
        /// 抛出(ADR-031 §②「查无此数据」层);本类不吞、不降级。</exception>
        public CareAppliedWriteResult TryApply(long tick, PatientId patient, int actionId,
                                              int caregiver, int phase)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            // ── 前置 ① 照护对象合法 ──
            // PatientId.None 入病史流会污染 max(patient_id) 高水位(ADR-006 Amendment B / ADR-007 §四),
            // 属**输入非法**(不是「该不该写」),故在其余判定之前独立拒。
            if (patient.IsNone)
                return CareAppliedWriteResult.NoPatient;

            // ── 前置 ② phase 值域(0 = 起 / 1 = 止;序数出处见类头注)──
            if (phase != DiseaseCourse.CarePhaseStart && phase != DiseaseCourse.CarePhaseStop)
                return CareAppliedWriteResult.InvalidPhase;

            // ── 前置 ③ ④ 动作 / 照护者非负(上界无权威定义,见类头登记项)──
            if (actionId < 0)
                return CareAppliedWriteResult.InvalidActionId;
            if (caregiver < 0)
                return CareAppliedWriteResult.InvalidCaregiver;

            // ── 前置 ⑤ 密度界(GDD :1784「超界拒收,不静默」;值 *待定* ⇒ 装配注入)──
            // 计数口径 = 流内同 (tick, 病人) 的 CareApplied **事件条数**(不区分起止 ——
            // 旋钮的原文是「护理动作事件数」,不是「动作数」)。
            if (_careEventsPerTickCap != NoDensityCap)
            {
                int sameTickCount = 0;
                for (int i = 0; i < _streamEvents.Count; i++)
                {
                    var e = _streamEvents[i];
                    if (e.Kind == EventKind.CareApplied && e.Patient == patient && e.Tick == tick)
                        sameTickCount++;
                }
                if (sameTickCount >= _careEventsPerTickCap)
                    return CareAppliedWriteResult.RateCapExceeded;
            }

            // ── 前置 ⑥ 「止」须有可配对的「起」(GDD :344 `t(止) ≥ t(起)`)──
            // 可判定形式:流内同 (病人, actionId) 存在 tick ≤ 本次 tick 的「起」。
            // 找不到 ⇒ 比较的两端不齐 ⇒ 拒(不猜「也许调用方忘了先发起」)。
            if (phase == DiseaseCourse.CarePhaseStop && !HasPriorStart(patient, actionId, tick))
                return CareAppliedWriteResult.StopWithoutStart;

            // ── 载荷:registry payload_schema 四字段(tick / action_id / caregiver / phase)──
            var payload = new CareAppliedPayload(tick, actionId, caregiver, phase);
            var payloadRef = _encoder.Encode(EventKind.CareApplied, payload);

            // ── 事件头:Patient = 照护对象;Seq = -1 未发号哨兵(O-1),由发号器给出 ──
            _eventSink.Append(new SimEvent(
                tick, patient, -1, EventKind.CareApplied, payloadRef));

            return CareAppliedWriteResult.Applied;
        }

        /// <summary>
        /// 流前缀里是否存在可配对的「起」:同 (病人, actionId) 且 <c>Phase == 起</c> 且
        /// <c>tick ≤ 本次 tick</c>。
        /// </summary>
        /// <remarks>
        /// <para>配对键**不含 caregiver**(GDD 未把照护者列入照护支语义;加入 = 自行发明规则)。</para>
        /// <para>只扫 <see cref="EventKind.CareApplied"/> 一种 Kind;其余 Kind 不触发解码
        /// (故不会因别的载荷未驻留而抛)。</para>
        /// </remarks>
        private bool HasPriorStart(PatientId patient, int actionId, long tick)
        {
            for (int i = 0; i < _streamEvents.Count; i++)
            {
                var e = _streamEvents[i];
                if (e.Kind != EventKind.CareApplied) continue;
                if (e.Patient != patient) continue;
                if (e.Tick > tick) continue;
                var p = _decoder.Decode<CareAppliedPayload>(EventKind.CareApplied, e.Payload);
                if (p.Phase == DiseaseCourse.CarePhaseStart && p.ActionId == actionId)
                    return true;
            }
            return false;
        }
    }
}
