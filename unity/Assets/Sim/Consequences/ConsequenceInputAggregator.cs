// 权威来源:GDD `design/gdd/medical-consequences.md`
//   规则四(按 Tick 聚合,不按到达序 —— 前向悬垂引用)· 规则五(入向闭集 = 恰五路)
//   · 规则二(53 是唯一判「对错」的系统)· AC-53-04(置换不变)· AC-53-05(零改写计数)
//   · ADR-008 §跨流全序键 `(Tick, StreamPriority, Patient, Seq)`
//   · ADR-005(纯函数 · 主机唯一 Step)· ADR-016 §一(三源不变量)
// 卡面:`production/epics/medical-consequences/story-001-input-closure-and-stream-aggregation.md`
//
// 本件 = 53 的**输入聚合器**,住 `Sim`(门 A)。**不触 `IEventSink` / 不发号 `Seq`** ——
// 只产出「每结案病例一组输入」,不产出任何事件(Judge 求值归 story-002)。
//
// ✅ **P / C 内容已解码并承载内容行**(2026-10-15 接线轮 · 承 ADR-031 V-8 ②「退」,用户裁定
//    「6 处全做,含改 Inputs53」):原稿自陈「`Sim` 够不着 `Sim.Codec` ⇒ 只操作事件头」——
//    该前提在 ADR-031 后**不成立**(`IPayloadDecoder` 住 `Sim.Contracts`)。现本件持解码器,
//    把 P / C 的**内容**(载荷字段)解出来放进 `Inputs53`:P 路承载 `JudgmentRecordedPayload`
//    的 `LexiconId` / `Confidence`(Judge 真值源「病名 / 置信度」),C 路承载处置的
//    `ActionId` / `Polarity`(「哪些处置动作发生」)。**字段名与数量不变**(五路闭集 =
//    AC-53-05 反射断言面),只换**元素类型**:`SimEvent` → 解码行 struct(全整数域)。
//    ② 路(PatternRecognized)保持 `SimEvent` —— ADR-031 ② 的裁定面只点名 P/C;
//    其 `salted_key` 由 story-002 `Judge` 经解码器直读(Decoder 可传)。
//    D 侧仍走 9 的只读边界(病程投影,非事件载荷)。
//
// ⚠️ **两处张力如实登记(本件不自行裁定,归 53 的 GDD 轮)**:
//    ① **处置记录的 Kind 集** —— GDD 规则五 `:165` 的闭集**只列** `CareApplied`,
//       而卡面 §Implementation Notes 4 写「与 10/11 写入的 `EmergencyAttempt` /
//       `DrugTreatmentApplied` 的映射表按 GDD 规则五逐字实现」⇒ **四处口径不一致**。
//       本件**不猜**:以 `careKinds` **显式参数**由装配层传入,调用方决定。
//    ② **处置窗口的下界** —— 若要取 37 的处置证据窗口 `[CaseOpened.Tick, CloseTick]`,
//       则闭集须加第六路 `CaseOpened`,**与「恰五路」的反射断言直接冲突**。
//       本件取 **`Tick ≤ 结案 tick` 的全部处置**(无下界);扩张归 53 的 GDD 轮。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Consequences
{
    /// <summary>
    /// 9 侧**病程投影**的只读边界(卡面 §Implementation Notes 3)。
    /// <para>53 经本接口取 `D`,**不 import 9 的内部类型**(承 ADR-016 §一 三源不变量)。
    /// 实现者 = 9 的边界层;story-001 的夹具实现住测试装配。</para>
    /// </summary>
    public interface IDiseaseProjection
    {
        /// <summary>取该病人在 <paramref name="tick"/> 时刻的病程投影。</summary>
        /// <returns><c>true</c> = 有投影;<c>false</c> = 该处无投影(边界退化,非错误)。</returns>
        bool TryProject(PatientId patient, long tick, out DiseaseProjectionSnapshot snapshot);
    }

    /// <summary>
    /// 病程投影快照。⚠️ **形状归 9 修订轮** —— 本 story 只保证「经接口边界」,
    /// 不定义投影的内容(卡面 §Implementation Notes 3 逐字:「以接口占位 + 夹具实现」)。
    /// </summary>
    public readonly struct DiseaseProjectionSnapshot
    {
        /// <summary>投影覆盖的 tick。</summary>
        public readonly long Tick;

        /// <summary>
        /// **痊愈位**(D 路真值)—— 「该病人在 <see cref="Tick"/> 时刻**是否已痊愈**」。
        /// <para>🔁 **2026-10-30(ADR-036 D1 实现轮)**:此前本结构只有 <see cref="Tick"/>,
        /// D 路在 Sim 侧**没有任何真值来源** ⇒ 判定表 18 格里 `Recovered` 那 6 格永不可达。
        /// 现补此位;生产实现 = 9 的边界层(读 <c>DiseaseVitalsService.GetOutcome</c>)。</para>
        /// <para>⚠️ **它不是 `PatientOutcome`**(ADR-032 的病程终态)—— 语义是「**结案那一刻是否痊愈**」,
        /// 两者不恒等(ADR-036 D1 语义边界)。⚠️ 且今日该位**恒为 false**:9 的痊愈支受
        /// `trend_window_ticks = 0` 门控未求值(ADR-036 §Risks R1)——「接口通」≠「6 格活」。</para>
        /// </summary>
        public readonly bool Recovered;

        /// <summary>构造投影快照(痊愈位默认为 <c>false</c> —— 未接线 / 未求值时的保守值)。</summary>
        public DiseaseProjectionSnapshot(long tick) : this(tick, recovered: false) { }

        /// <summary>构造投影快照。</summary>
        /// <param name="tick">投影覆盖的 tick。</param>
        /// <param name="recovered">该时刻是否已痊愈(D 路真值)。</param>
        public DiseaseProjectionSnapshot(long tick, bool recovered)
        {
            Tick = tick;
            Recovered = recovered;
        }
    }

    /// <summary>P 路**解码行** —— `JudgmentRecorded` 载荷内容 + 事件头排序键。
    /// <para>⚠️ 2026-10-15 接线轮(承 ADR-031 V-8 ②「退」):P 路元素从 `SimEvent` 改为本行
    /// —— Judge 真值源(GDD 规则五 `:165`「P = 判断记录(病名 / 置信度)」)由
    /// `JudgmentRecordedPayload.LexiconId` / `.Confidence` 承载;`Tick` / `PatientId` / `Seq`
    /// 取**事件头**(全序键真源),载荷无这三者(JudgmentRecordedPayload 只有 CaseId)。
    /// 全整数域(ADR-024 A2)。</para></summary>
    public readonly struct JudgmentRow53
    {
        /// <summary>事件头 Tick(全序键第一分量)。</summary>
        public readonly long Tick;

        /// <summary>事件头 Patient(全序键第三分量)。</summary>
        public readonly int PatientId;

        /// <summary>事件头 Seq(全序键第四分量)。</summary>
        public readonly long Seq;

        /// <summary>载荷 `lexicon_id`(病名册 ordinal;0 = 空 —— Judge 的 P 真值)。</summary>
        public readonly int LexiconId;

        /// <summary>载荷 `confidence`(落笔档位;Judge 的 P 真值)。</summary>
        public readonly byte Confidence;

        /// <summary>构造解码行。</summary>
        public JudgmentRow53(long tick, int patientId, long seq, int lexiconId, byte confidence)
        {
            Tick = tick; PatientId = patientId; Seq = seq;
            LexiconId = lexiconId; Confidence = confidence;
        }
    }

    /// <summary>C 路**解码行** —— 处置载荷内容 + 事件头排序键。
    /// <para>⚠️ 2026-10-15 接线轮(承 ADR-031 V-8 ②「退」):C 路元素从 `SimEvent` 改为本行。
    /// 三种处置 Kind **异构载荷**在此归一:`ActionId` = `CareAppliedPayload.ActionId`
    /// 或 `Emergency/DrugTreatmentAppliedPayload.TreatmentId`;`Polarity` = 急救 / 用药的
    /// `polarity`(`CareApplied` 无极性 ⇒ 恒 0)。`Kind` 保留(`EventKind` = 整数枚举)——
    /// 异构归一的**判别键**(Judge 的 C 真值「哪些处置动作发生」须知道是哪类处置)。
    /// 全整数域(ADR-024 A2;`Fix DrugPotency` 刻意**不承载** —— 数值面归数值轮)。</para></summary>
    public readonly struct CareRow53
    {
        /// <summary>事件头 Tick(全序键第一分量)。</summary>
        public readonly long Tick;

        /// <summary>事件头 Patient(全序键第三分量)。</summary>
        public readonly int PatientId;

        /// <summary>事件头 Seq(全序键第四分量)。</summary>
        public readonly long Seq;

        /// <summary>处置 Kind(CareApplied / EmergencyTreatmentApplied / DrugTreatmentApplied
        /// —— 由 `careKinds` 参数决定,见文件头张力 ①)。</summary>
        public readonly EventKind Kind;

        /// <summary>载荷动作标识:`CareApplied.ActionId` 或 `*.TreatmentId`(归一)。</summary>
        public readonly int ActionId;

        /// <summary>载荷极性(急救 / 用药;`CareApplied` 恒 0 —— 无极性维度)。</summary>
        public readonly int Polarity;

        /// <summary>构造解码行。</summary>
        public CareRow53(long tick, int patientId, long seq, EventKind kind,
                         int actionId, int polarity)
        {
            Tick = tick; PatientId = patientId; Seq = seq;
            Kind = kind; ActionId = actionId; Polarity = polarity;
        }
    }

    /// <summary>
    /// 53 的入向闭集 —— **每结案病例一组**,恰五路(GDD 规则五 `:165`)。
    /// <para>⚠️ **字段数 = 5 本身是本 story 的 AC**:第六路存在 ⇒ `AC-53-05` 的反射断言红。
    /// 增删字段须同时改测试与 GDD。</para>
    /// <para>⚠️ **命名口径**:本件取 **`Inputs53`** —— 卡面 AC 第一条**逐字**用此名
    /// (「`Inputs53` 结构恰含五路」)。卡面 §Implementation Notes 1 同一物写作
    /// `PerCaseInputs`(异名);**AC 是判据** ⇒ 以 AC 名为准,不复名(避免第二真源)。</para>
    /// </summary>
    public readonly struct Inputs53
    {
        /// <summary>① 触发 —— 该病例的 `CaseClosed` 事件。</summary>
        public readonly SimEvent CaseClosed;

        /// <summary>② 世界级加速器 —— 流中全部 `PatternRecognized`(哨兵 <see cref="PatientId.None"/>)。
        /// <para>⚠️ 它们是**世界级**事件,不属于任何病人 ⇒ 本路**逐病例重复装同一集**
        /// (共享引用,非复制数据)。匹配口径(`salted_key`)归 story-002。</para></summary>
        public readonly IReadOnlyList<SimEvent> PatternRecognized;

        /// <summary>③ `P` 投影@结案 —— 该病人 `Tick ≤ 结案 tick` 的**最后一笔** `JudgmentRecorded`
        /// (长度 0 或 1;**解码行** —— 载荷 `LexiconId` / `Confidence` 已解出,见
        /// <see cref="JudgmentRow53"/>)。<para>空 = 「结案无判断」,是**合法输入** ——
        /// 交 story-002 判 missed,不是错误。</para></summary>
        public readonly IReadOnlyList<JudgmentRow53> JudgmentRecords;

        /// <summary>④ `C` 投影@结案 —— 该病人 `Tick ≤ 结案 tick` 的全部处置
        /// (**解码行** —— 载荷 `ActionId` / `Polarity` 已解出,见 <see cref="CareRow53"/>;
        /// Kind 集由 `careKinds` 参数决定 —— 见文件头张力 ①)。</summary>
        public readonly IReadOnlyList<CareRow53> CareRecords;

        /// <summary>⑤ `D` —— 9 的病程投影快照(经只读边界;窗口口径见文件头张力 ②)。</summary>
        public readonly DiseaseProjectionSnapshot DiseaseProjection;

        /// <summary>构造一组入向闭集。</summary>
        /// <param name="caseClosed">该病例的结案事件。</param>
        /// <param name="patternRecognized">世界级模式识别集。</param>
        /// <param name="judgmentRecords">P 投影(0 或 1 条)。</param>
        /// <param name="careRecords">C 投影(全量)。</param>
        /// <param name="diseaseProjection">D 快照。</param>
        public Inputs53(SimEvent caseClosed,
                             IReadOnlyList<SimEvent> patternRecognized,
                             IReadOnlyList<JudgmentRow53> judgmentRecords,
                             IReadOnlyList<CareRow53> careRecords,
                             DiseaseProjectionSnapshot diseaseProjection)
        {
            CaseClosed = caseClosed;
            PatternRecognized = patternRecognized;
            JudgmentRecords = judgmentRecords;
            CareRecords = careRecords;
            DiseaseProjection = diseaseProjection;
        }
    }

    /// <summary>
    /// 53 的输入聚合器(卡面 story-001)。**纯函数** —— 无状态、无副作用、**不产事件**。
    /// </summary>
    public static class ConsequenceInputAggregator
    {
        /// <summary>
        /// **生产口径**的「处置记录」Kind 集(GDD 规则五 · ADR-036 D4)。
        /// <para>与 <see cref="CareRowOf"/> 的三臂 `switch` 一一对应 —— 少一支 = 该支的处置在生产上
        /// **不计入 C 路**(静默,无报错)。</para>
        /// <para>🔁 **2026-10-30(ADR-036 D4)**:此前只存在于夹具
        /// (`CareKindsPerGddRule5 = { CareApplied }`,仅一支)。生产口径比测试宽 **三倍** ——
        /// 这份差集是**新增覆盖面**,不得拿既有夹具当「三支都已证」。</para>
        /// </summary>
        public static readonly IReadOnlyList<EventKind> ProductionCareKinds = Array.AsReadOnly(new[]
        {
            EventKind.CareApplied,               // 4 的护理动作
            EventKind.EmergencyTreatmentApplied, // 10 的急救处置(ADR-009 Amendment I)
            EventKind.DrugTreatmentApplied,      // 11 的用药处置(ADR-009 Amendment I)
        });
        /// <summary>
        /// 按**跨流全序键**聚合,产出每结案病例一组输入。
        /// <para>⚠️ **按 Tick 聚合,不按到达序**(GDD 规则四)—— 前向悬垂实测形:
        /// 哨兵 `PatternRecognized(Patient = -1)` 排在**同 tick 病人事件之前**。
        /// **置换不变**(AC-53-04)由「先复制 → 全排序 → 后处理」**结构性**保证 ——
        /// 输入的任何置换都归约到同一有序序列。</para>
        /// </summary>
        /// <param name="stream">事件流快照(**任意到达序** —— 本函数自行排序)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 §① 第八抽象点)—— P/C 路载荷内容的
        /// 唯一读取路径(2026-10-15 接线轮,V-8 ②「退」)。</param>
        /// <param name="diseaseProjection">9 的病程投影边界(可为 <c>null</c> ⇒ 第五路为 <c>default</c>)。</param>
        /// <param name="careKinds">算作「处置记录」的 Kind 集(见文件头张力 ①)。</param>
        /// <returns>按结案事件的全序排序的输入组;空流 ⇒ 空。</returns>
        public static IReadOnlyList<Inputs53> Aggregate(
            IReadOnlyList<SimEvent> stream,
            IPayloadDecoder decoder,
            IDiseaseProjection diseaseProjection,
            IReadOnlyList<EventKind> careKinds)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (stream == null || stream.Count == 0) return Array.Empty<Inputs53>();

            // ── ① 复制 + 全序排序 ────────────────────────────────────────────
            // 置换不变的结构性来源:输入的任何置换 ⇒ 同一有序序列。
            // 键 = (Tick, StreamPriority, Patient, Seq) **全序无并列**
            //   (registry 保证 Seq 在 (Tick, Patient) 内唯一)⇒ 排序算法的稳定性无关。
            // ⚠️ 比较体**不自实现减法**(卡面 §Implementation Notes 2:防 int64 溢出)——
            //    住 `EventOrderKey.CompareTo`。
            var ordered = new List<SimEvent>(stream.Count);
            for (int i = 0; i < stream.Count; i++) ordered.Add(stream[i]);
            ordered.Sort((a, b) => EventOrder.Compare(in a, in b));

            // ── ② 世界级加速器集(一次性收齐,逐病例共享引用)────────────────
            List<SimEvent> worldPatterns = null;
            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent e = ordered[i];
                if (e.Kind == EventKind.PatternRecognized && e.Patient.Value == PatientId.None.Value)
                {
                    if (worldPatterns == null) worldPatterns = new List<SimEvent>();
                    worldPatterns.Add(e);
                }
            }
            IReadOnlyList<SimEvent> patterns =
                worldPatterns ?? (IReadOnlyList<SimEvent>)Array.Empty<SimEvent>();

            // ── ③ 逐结案病例投影 ────────────────────────────────────────────
            var result = new List<Inputs53>();
            for (int i = 0; i < ordered.Count; i++)
            {
                SimEvent close = ordered[i];
                if (close.Kind != EventKind.CaseClosed) continue;

                List<JudgmentRow53> judgments = null;   // 覆盖式:只留**最后一笔**
                List<CareRow53> cares = null;

                for (int j = 0; j < ordered.Count; j++)
                {
                    SimEvent e = ordered[j];
                    // 有序 ⇒ 越过结案 tick 即可停;`>` 而非 `>=`:
                    // **同 tick** 病人事件算在投影内(它们在结案之前发生,由 Seq 定序)。
                    if (e.Tick > close.Tick) break;
                    if (e.Patient.Value != close.Patient.Value) continue;

                    if (e.Kind == EventKind.JudgmentRecorded)
                    {
                        // 「P 投影@结案 = 该病例最后一笔判断记录」(卡面 §Implementation
                        // Notes 1)—— 有序扫描 ⇒ 逐条覆盖即得最后一笔。
                        // ⚠️ 2026-10-15 接线轮:解码归本层(V-8 ②「退」),不再由调用方自解。
                        var jp = decoder.Decode<JudgmentRecordedPayload>(
                            EventKind.JudgmentRecorded, e.Payload);
                        var row = new JudgmentRow53(
                            e.Tick, e.Patient.Value, e.Seq, jp.LexiconId, jp.Confidence);
                        if (judgments == null) judgments = new List<JudgmentRow53>(1);
                        else judgments.Clear();
                        judgments.Add(row);
                    }
                    else if (IsCareKind(e.Kind, careKinds))
                    {
                        cares = cares ?? new List<CareRow53>();
                        cares.Add(CareRowOf(e, decoder));
                    }
                }

                DiseaseProjectionSnapshot d = default;
                if (diseaseProjection != null)
                    diseaseProjection.TryProject(close.Patient, close.Tick, out d);

                result.Add(new Inputs53(
                    close,
                    patterns,
                    judgments ?? (IReadOnlyList<JudgmentRow53>)Array.Empty<JudgmentRow53>(),
                    cares ?? (IReadOnlyList<CareRow53>)Array.Empty<CareRow53>(),
                    d));
            }

            return result;
        }

        /// <summary>处置事件 → 解码行(三种异构载荷归一:ActionId = ActionId|TreatmentId;
        /// Polarity = 急救/用药极性,CareApplied 恒 0)。</summary>
        private static CareRow53 CareRowOf(SimEvent e, IPayloadDecoder decoder)
        {
            long tick = e.Tick;
            int patient = e.Patient.Value;
            long seq = e.Seq;

            switch (e.Kind)
            {
                case EventKind.CareApplied:
                {
                    var p = decoder.Decode<CareAppliedPayload>(EventKind.CareApplied, e.Payload);
                    return new CareRow53(tick, patient, seq, e.Kind, p.ActionId, polarity: 0);
                }
                case EventKind.EmergencyTreatmentApplied:
                {
                    var p = decoder.Decode<EmergencyTreatmentAppliedPayload>(
                        EventKind.EmergencyTreatmentApplied, e.Payload);
                    return new CareRow53(tick, patient, seq, e.Kind, p.TreatmentId, p.Polarity);
                }
                case EventKind.DrugTreatmentApplied:
                {
                    var p = decoder.Decode<DrugTreatmentAppliedPayload>(
                        EventKind.DrugTreatmentApplied, e.Payload);
                    return new CareRow53(tick, patient, seq, e.Kind, p.TreatmentId, p.Polarity);
                }
                default:
                    // careKinds 白名单外的 Kind 不会走到此(fail-loud,不静默);
                    // 新处置 Kind 入集须同步本分派(ADR-024 registry 单一真源)。
                    throw new InvalidOperationException(
                        $"careKinds 收入了未实现载荷分派的处置 Kind:{e.Kind}");
            }
        }

        /// <summary>该 Kind 是否算「处置记录」(见文件头张力 ①)。</summary>
        private static bool IsCareKind(EventKind kind, IReadOnlyList<EventKind> careKinds)
        {
            if (careKinds == null) return false;
            for (int i = 0; i < careKinds.Count; i++)
                if (careKinds[i] == kind) return true;
            return false;
        }
    }
}
