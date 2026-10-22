// 病程终态写者 —— `PatientOutcomeChanged` 的生产写入通道(9 的 F4 终态落流)。
//
// 权威来源:
//   design/registry/entities.yaml:3264-3278 —— SimEvent.Kind.PatientOutcomeChanged:
//     stream: history · author: "9 疾病与伤情(终态判定方 = F4 九态求值方 = Step 执行方 = 主机)"
//     payload_schema: "patient_id: i32 · disease_id: i32(argmax d)· outcome: i32
//                      (PatientOutcome:Recovered=1 / Died=2;Unspecified=0 解码拒收)· seq: i64。
//                      **t_end 不进载荷** —— 它就是事件头部的 Tick"
//     constraint: "⚠️ 原子性义务(硬):9 须在**判出终态的那一 tick** 写出本事件"
//   docs/architecture/adr-032-patient-outcome-kind.md —— Kind 归属 / 流别 / 写者 / 载荷 / 有界性
//   design/gdd/persistence-service.md:316 —— `Folded(p)` 条件 ①「终态」的唯一事件载体
//   design/gdd/disease-simulation.md:1113-1125 —— F4 痊愈支 / 死亡支(终态的两个来源)
//   ADR-005(主机唯一 Append;Seq 由发号器给出 ⇒ header Seq = -1 哨兵)
//   ADR-008 §一(按 Kind 纯函数路由 ⇒ 本 Kind 落病史流)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder;本类内禁 `new PayloadRef(`)
//
// ── 本类存在的理由(为什么不能「直接 Append」)────────────────────────────────
// 载荷有四个字段,其中 `Outcome` 的合法域只有 {Recovered=1, Died=2}(0 = 漏填哨兵,
// 解码期 mask 校验会拒收)。若让每个调用点自己装载荷,则「九态里的 Coma / Severe …
// 被当成终态写进流」是**可写出的**——那种事件一旦入流,`Folded(p)` 条件 ① 就为真,
// 病人会被错误折叠,**且没有任何门能扫出来**(流里确实有一条「终态」事件)。
// ⇒ 本类把「哪些九态才是终态」这条判据收在一处,调用方只能给九态,不能给载荷。
//
// ⚠️ **有写者 ≠ 已接线**:生产触发点(F4 判出终态的那一 tick 调本类)**尚未接线** ——
//   与 `CaseOpenWriter` / `PlayerDiedWriter` 同格。本类只保证「谁调用它,流里就有一条
//   合规的 `PatientOutcomeChanged`」;终态折叠在游戏内**并未因此可用**。
//   接线归属 = 9 的 F4 装配轮(即 `DiseaseVitalsService` 那一侧 —— 见下方「⚠️ 装配冲突」)。
//
// ⚠️ **装配冲突(必须显式登记,不得由实现方自行消解)**
//   `DiseaseVitalsService` 的类头明写「**不写流** —— 本类只读 `EventStream`,零 `IEventSink.Append`」,
//   而本写者的生产调用点恰好应在那里(F4 求值就在 `StepAll` 里)。
//   ⇒ 接线轮必须**先裁**「`DiseaseVitalsService` 是持有写者还是把终态交出去让外部写」。
//   本类**不预设**该裁决:它只是一个被动写者,两个方向都接得上。
//   在此之前,**终态折叠仍不可用**,本条不得被读成「折叠已接通」。
//
// ⚠️ 登记项(本批不裁):
//   - **幂等**:同 (patient, disease) 的第二条终态**本类不拒**。理由是终态在 F4 九态里是
//     吸收态(痊愈 / 死亡无出边)⇒ 正常路径下不会发生;若真发生,那是 F4 侧的状态回退,
//     属**上游缺陷**,在写者里静默吞掉只会掩盖它。⇒ 由调用方 / 上游负责,本类不代裁。
//     (与 `CaseCloseWriter` 的「幂等拒收」**刻意不同**:那里 GDD 规则六-4 明文要求拒收,
//     这里 registry 只说「至多一条」是有界性**结论**,不是写入期**判据**。)
//   - `IEventAuthority.IsHost` 门不在本件:与既有写者同格 —— 主机唯一由「谁调用写者」保证
//     (ADR-005),客户端写通道的统一门禁归 45 轮。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="PatientOutcomeChangedWriter.TryWrite"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum PatientOutcomeWriteResult
    {
        /// <summary>已写入:一条 <see cref="EventKind.PatientOutcomeChanged"/> 入病史流(本次真正写入)。</summary>
        Written = 0,

        /// <summary>不写入 —— 该九态**不是终态**(只有 <see cref="InjuryState.Recovered"/> /
        /// <see cref="InjuryState.Deceased"/> 是)。
        /// <para>⚠️ 这是本类最关键的拒收分支:放行它 ⇒ 把「昏迷 / 危殆 / 轻症」写成终态
        /// ⇒ `Folded(p)` 条件 ① 假真 ⇒ 病人被错误折叠,且无门可扫。</para></summary>
        NotTerminal = 1,

        /// <summary>不写入 —— 病人 id 非法(`PatientId.None` / 负值)。哨兵入病史流会污染
        /// `max(patient_id)` 高水位(ADR-006 Amendment B / ADR-007 §四),且折叠谓词按病人
        /// 切分时会误吞。</summary>
        NoPatient = 2,

        /// <summary>不写入 —— `diseaseId` 为负(它必须指向一个真实病种;F4 的 `d` 是 argmax 结果,
        /// 负值只可能来自调用方的编码错误)。</summary>
        InvalidDiseaseId = 3,
    }

    /// <summary>
    /// 病程终态写者 —— <c>PatientOutcomeChanged</c> 的生产写入通道(9 · 主机侧 · 病史流)。
    /// </summary>
    /// <remarks>
    /// <para><b>只写 <c>PatientOutcomeChanged</c></b>:F4 的其余八态是**过程态**,不落流
    /// (它们可由 F1/F4 从流重放求得,进流即第二真源 —— ADR-016 §一)。</para>
    /// <para><b>为什么收 <see cref="InjuryState"/> 而不收 <see cref="PatientOutcome"/></b>:
    /// 调用方手里拿的是 F4 的求值结果(九态);让它自己把九态映射成三值枚举,就是把
    /// 「哪些态算终态」这条判据复制到每个调用点。本类做这一次映射,映射不可表达的状态
    /// (Coma / Severe / …)在类型上就进不来 —— 它们会被拒收,而不是被静默映射。</para>
    /// </remarks>
    public sealed class PatientOutcomeChangedWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;

        /// <summary>
        /// 构造终态写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public PatientOutcomeChangedWriter(IEventSink eventSink, IPayloadEncoder encoder)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <summary>
        /// 把一个 F4 九态写成终态事件 —— **仅当**它确是终态(痊愈 / 死亡)。
        /// </summary>
        /// <param name="tick">判出终态的那一 tick。它就是事件头部的 <c>Tick</c>,
        /// 也是折叠行的 <c>t_end</c>(registry:**t_end 不进载荷**)。
        /// <para>⚠️ registry 的「原子性义务」要求它**恰是判出终态的那一 tick** —— 本类不校验
        /// 这一点(它无从知道 F4 何时判出),由调用方保证。</para></param>
        /// <param name="patient">病人 id(事件头 <c>Patient</c> 与载荷 <c>patient_id</c> 同值)。</param>
        /// <param name="diseaseId">取得 <c>position_agg</c> 的那个病种(F4 的 argmax <c>d</c>)。
        /// <para>⚠️ 终态是**逐病种**的结论,不是病人级读法 —— 故本参数必填,无缺省。</para></param>
        /// <param name="outcome">F4 求值出的九态;只有 <see cref="InjuryState.Recovered"/> /
        /// <see cref="InjuryState.Deceased"/> 会被写入,其余一律
        /// <see cref="PatientOutcomeWriteResult.NotTerminal"/>。</param>
        /// <returns>结果码;仅 <see cref="PatientOutcomeWriteResult.Written"/> 意味着真的写入了一条。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// (tick 域与既有写者同口径:入参非法 = 编程错误,fail-loud)。</exception>
        public PatientOutcomeWriteResult TryWrite(
            long tick, PatientId patient, int diseaseId, InjuryState outcome)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            // ── 前置 ① 病人合法 ──
            // 在终态判定**之前**判:哨兵 id 的入参非法性与「这个态算不算终态」是两个正交问题,
            // 先报更基础的那个(与 `CareAppliedWriter` 的「前置 ① 照护对象合法」同序)。
            if (patient.IsNone)
                return PatientOutcomeWriteResult.NoPatient;

            // ── 前置 ② disease_id 非负 ──
            if (diseaseId < 0)
                return PatientOutcomeWriteResult.InvalidDiseaseId;

            // ── 前置 ③ 终态映射(本类的核心判据)──
            // ⚠️ 用显式 switch 而不是 `(int)outcome` 直传:直传会把 Coma(5)/ Severe(3) 等
            //    过程态的枚举值原样写进 `outcome` 字段,而 codec 的解码期校验只认 {1,2}
            //    ⇒ 那种事件**写得出去、读不回来**,是比拒收更坏的形态(流里有一条永远解不开的事件)。
            PatientOutcome terminal;
            switch (outcome)
            {
                case InjuryState.Recovered:
                    terminal = PatientOutcome.Recovered;
                    break;
                case InjuryState.Deceased:
                    terminal = PatientOutcome.Died;
                    break;
                default:
                    return PatientOutcomeWriteResult.NotTerminal;
            }

            // ── 载荷:registry payload_schema 四字段(全整数域)──
            //   ⚠️ `t_end` **不在此**:它就是事件头部的 Tick(registry 原文)。
            //   `Seq` 传 0 = 占位;header Seq 由 `EventStream.Append` 发号(与
            //   `PatientSpawner` 的 DiseaseOnset 载荷同口径 —— 载荷 Seq 是占位,
            //   真号在 header)。承 ADR-006 Amendment A。
            var payload = new PatientOutcomeChangedPayload(
                patientId: patient.Value,
                diseaseId: diseaseId,
                outcome: (int)terminal,
                seq: 0);

            var payloadRef = _encoder.Encode(EventKind.PatientOutcomeChanged, payload);

            // ── 事件头:Patient = 病人(非 None —— 折叠与全序键都依赖它);
            //            Seq = -1 未发号哨兵(O-1),由 EventStream 发号 ──
            _eventSink.Append(new SimEvent(
                tick, patient, -1, EventKind.PatientOutcomeChanged, payloadRef));

            return PatientOutcomeWriteResult.Written;
        }
    }
}
