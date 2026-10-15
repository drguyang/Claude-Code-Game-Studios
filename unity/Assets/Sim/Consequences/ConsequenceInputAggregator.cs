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
// ⚠️ **零载荷解码(门 A 的结构性后果)**:`Sim` 的引用集恰 = {BCL, `Sim.Contracts`}
//    ⇒ 够不着 `Sim.Codec` ⇒ **本件只操作 `SimEvent` 头部**(Tick / Patient / Seq / Kind),
//    **从不读 `Payload` 内容**。P / C 的**内容**(病名 / 置信度 / 处置适切性)归 story-002。
//    这与卡面「本 story 只出聚合后的输入组」一致,**不是范围裁剪**。
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

        /// <summary>构造投影快照。</summary>
        public DiseaseProjectionSnapshot(long tick) { Tick = tick; }
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
        /// (长度 0 或 1)。<para>空 = 「结案无判断」,是**合法输入** —— 交 story-002 判 missed,
        /// 不是错误。</para></summary>
        public readonly IReadOnlyList<SimEvent> JudgmentRecords;

        /// <summary>④ `C` 投影@结案 —— 该病人 `Tick ≤ 结案 tick` 的全部处置事件
        /// (Kind 集由 `careKinds` 参数决定 —— 见文件头张力 ①)。</summary>
        public readonly IReadOnlyList<SimEvent> CareRecords;

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
                             IReadOnlyList<SimEvent> judgmentRecords,
                             IReadOnlyList<SimEvent> careRecords,
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
        /// 按**跨流全序键**聚合,产出每结案病例一组输入。
        /// <para>⚠️ **按 Tick 聚合,不按到达序**(GDD 规则四)—— 前向悬垂实测形:
        /// 哨兵 `PatternRecognized(Patient = -1)` 排在**同 tick 病人事件之前**。
        /// **置换不变**(AC-53-04)由「先复制 → 全排序 → 后处理」**结构性**保证 ——
        /// 输入的任何置换都归约到同一有序序列。</para>
        /// </summary>
        /// <param name="stream">事件流快照(**任意到达序** —— 本函数自行排序)。</param>
        /// <param name="diseaseProjection">9 的病程投影边界(可为 <c>null</c> ⇒ 第五路为 <c>default</c>)。</param>
        /// <param name="careKinds">算作「处置记录」的 Kind 集(见文件头张力 ①)。</param>
        /// <returns>按结案事件的全序排序的输入组;空流 ⇒ 空。</returns>
        public static IReadOnlyList<Inputs53> Aggregate(
            IReadOnlyList<SimEvent> stream,
            IDiseaseProjection diseaseProjection,
            IReadOnlyList<EventKind> careKinds)
        {
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

                List<SimEvent> judgments = null;   // 覆盖式:只留**最后一笔**
                List<SimEvent> cares = null;

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
                        if (judgments == null) judgments = new List<SimEvent>(1);
                        else judgments.Clear();
                        judgments.Add(e);
                    }
                    else if (IsCareKind(e.Kind, careKinds))
                    {
                        if (cares == null) cares = new List<SimEvent>();
                        cares.Add(e);
                    }
                }

                DiseaseProjectionSnapshot d = default;
                if (diseaseProjection != null)
                    diseaseProjection.TryProject(close.Patient, close.Tick, out d);

                result.Add(new Inputs53(
                    close,
                    patterns,
                    judgments ?? (IReadOnlyList<SimEvent>)Array.Empty<SimEvent>(),
                    cares ?? (IReadOnlyList<SimEvent>)Array.Empty<SimEvent>(),
                    d));
            }

            return result;
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
