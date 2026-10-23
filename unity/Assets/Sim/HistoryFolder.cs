// GDD persistence-service.md F-7a-4 / ADR-010 §二 —— 病史流折叠动作(折叠行产出 + 行抹除)。
//
// 权威来源:
//   ADR-010 §二                        —— 病史流 = 序列化记录序列 + 终态折叠;折叠行六字段
//   GDD persistence-service.md:314-330 —— F-7a-4「输出:布尔。真 ⇒ 该病史行进折叠(折叠行保留 patient_id,规则八)」
//   ADR-026 §⑦                         —— 条件 ④:**`SkillGrown` 行不随病人折叠被抹除**(案 1)
//   ADR-032 ④a / 附录 A                —— 折叠行 outcome / t_end / 病种_id 的取法
//   ADR-005                            —— 病史流是唯一真源(折叠不得制造第二真源)
//
// 本件与 `FoldedPredicate` 的分工:
//   谓词 = **判定**(可不可折);本件 = **动作**(真折时产什么行、留什么行)。
//   条件 ④ 落在本件而非布尔 —— ADR-026 §⑦ 的判据是「折叠之后 `Level(s)@t` 逐位一致」,
//   那是对**产物**的断言,不是对返回值的断言。
//
// ⚠️ 折叠的抹除面 = 该病人的**全部**病史行,减去 `SkillGrown` 行。
//   为何不保留「无终态病程」的行:折叠谓词按**病人整体**判定(GDD F-7a-4 `:328`
//   「输出:布尔」),而终态是 F4 九态的**吸收态**(ADR-032 ⑤)⇒ 判成终态的病人不存在
//   「仍在演进的病程」。此外,除 onset / outcome 之外的病史行**不携带病种 id**
//   (`CareApplied` 等),按病种保留会是**任意切分**。
//   ⚠️ 条件 ① 的**存在性查询是病人级、行是按病种产**(ADR-032 ⑥ vs ④b),两者的缝
//   登记为 **OQ-7a-10**(`design/gdd/persistence-service.md` §Open Questions),本件按
//   权威字面实现,不自行扩张语义。
//   🔁🔁 **2026-10-24 `OQ-7a-10` 已裁(裁定 A)—— 原文不删,上面的处置已变**:
//   条件 ① 收紧为**全称**(`∀ d ∈ D_p`),故「本件被调用的前提」比原文更严 ——
//   只有当该病人的**每个**病种都终态时才会进到 `Erase`。原文「按权威字面实现」的
//   处置**仍然成立**(本件不重复判定,谓词说了算),但那条「终态 = 吸收态 ⇒ 不存在
//   仍在演进的病程」的理据**在收紧前为假** —— 正是「A 已终态、B 仍在演进」这个反例
//   促成了收紧。落点 = `FoldedPredicate.Terminal()`;详见该文件头注与 GDD `OQ-7a-10` 行。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>折叠结果:产出的折叠行 + 折叠后保留的事件序列(原序)。</summary>
    public readonly struct FoldResult
    {
        /// <summary>折叠行(按 <c>(Onset, DiseaseId)</c> 升序,确定性)。</summary>
        public readonly IReadOnlyList<FoldRow> Rows;

        /// <summary>折叠后的流(其他病人全部行 + 被折病人的 <c>SkillGrown</c> 行,保持原序)。</summary>
        public readonly IReadOnlyList<SimEvent> Retained;

        public FoldResult(IReadOnlyList<FoldRow> rows, IReadOnlyList<SimEvent> retained)
        {
            Rows = rows;
            Retained = retained;
        }

        /// <summary>是否未发生折叠(谓词为假)。</summary>
        public bool IsIdentity => Rows.Count == 0;
    }

    /// <summary>
    /// 病史流折叠 —— 折叠行产出 + 被折行的抹除(纯函数,无 I/O)。
    /// </summary>
    public static class HistoryFolder
    {
        /// <summary>
        /// 折叠病人 <paramref name="patientId"/> 的病史行。
        /// </summary>
        /// <param name="history">病史流(全序;本方法不改动入参)。</param>
        /// <param name="patientId">被折叠的病人。</param>
        /// <param name="folded">折叠谓词(判定归它,本方法不重复判)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <returns>
        /// 谓词为假 ⇒ <c>Rows</c> 空、<c>Retained</c> 与入参**逐条相同**(恒等);
        /// 谓词为真 ⇒ 产行 + 抹除该病人的非 <c>SkillGrown</c> 行。
        /// </returns>
        /// <exception cref="ArgumentNullException">任一入参为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="patientId"/> 为负。</exception>
        /// <exception cref="InvalidDataException">流损坏:同病种两条终态 / 有终态无 onset。</exception>
        public static FoldResult Fold(
            IReadOnlyList<SimEvent> history,
            int patientId,
            IFoldedPredicate folded,
            IPayloadDecoder decoder)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (folded == null) throw new ArgumentNullException(nameof(folded));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (patientId < 0)
                throw new ArgumentOutOfRangeException(nameof(patientId), patientId,
                    "病人 id 不得为负 —— PatientId.None(-1)不是可折叠对象");

            if (!folded.Folded(patientId))
                return new FoldResult(Array.Empty<FoldRow>(), history);

            (Dictionary<int, DiseaseOnsetPayload> onsets, Dictionary<int, OutcomeRecord> outcomes) =
                Collect(history, patientId, decoder);

            return new FoldResult(BuildRows(onsets, outcomes, patientId), Erase(history, patientId));
        }

        /// <summary>
        /// 收 onset(逐病种首条)与 outcome(逐病种至多一条)。
        /// </summary>
        private static (Dictionary<int, DiseaseOnsetPayload> Onsets, Dictionary<int, OutcomeRecord> Outcomes)
            Collect(IReadOnlyList<SimEvent> history, int patientId, IPayloadDecoder decoder)
        {
            var onsets = new Dictionary<int, DiseaseOnsetPayload>();
            var outcomes = new Dictionary<int, OutcomeRecord>();

            for (int i = 0; i < history.Count; i++)
            {
                SimEvent e = history[i];
                if (e.Patient.Value != patientId) continue;

                if (e.Kind == EventKind.DiseaseOnset)
                {
                    DiseaseOnsetPayload p =
                        decoder.Decode<DiseaseOnsetPayload>(EventKind.DiseaseOnset, e.Payload);
                    // 同病种重发 onset = 重传,取首条(去重键 (OnsetTick, PatientId) 由 EventStream 守)。
                    if (!onsets.ContainsKey(p.DiseaseId))
                        onsets[p.DiseaseId] = p;
                }
                else if (e.Kind == EventKind.PatientOutcomeChanged)
                {
                    PatientOutcomeChangedPayload p =
                        decoder.Decode<PatientOutcomeChangedPayload>(
                            EventKind.PatientOutcomeChanged, e.Payload);

                    if (outcomes.ContainsKey(p.DiseaseId))
                        throw new InvalidDataException(
                            $"病人 {patientId} 病种 {p.DiseaseId} 出现第二条终态事件 —— " +
                            "ADR-032 ⑤ 保证每病人每病种至多一条(终态 = 吸收态);重复即流损坏");

                    outcomes[p.DiseaseId] = new OutcomeRecord(p.Outcome, e.Tick);
                }
            }

            return (onsets, outcomes);
        }

        /// <summary>
        /// 逐终态产折叠行;每个终态都必须能在 onset 里找到来源(找不到即流损坏,不猜)。
        /// 顺序 = <c>(Onset, DiseaseId)</c> 升序 —— 字节级夹具依赖它。
        /// </summary>
        private static IReadOnlyList<FoldRow> BuildRows(
            Dictionary<int, DiseaseOnsetPayload> onsets,
            Dictionary<int, OutcomeRecord> outcomes,
            int patientId)
        {
            var rows = new List<FoldRow>(outcomes.Count);
            foreach (KeyValuePair<int, OutcomeRecord> kv in outcomes)
            {
                if (!onsets.TryGetValue(kv.Key, out DiseaseOnsetPayload onset))
                    throw new InvalidDataException(
                        $"病人 {patientId} 病种 {kv.Key} 有终态事件但无 DiseaseOnset —— " +
                        "折叠行 onset / patient_seed 无来源(流损坏,不猜)");

                rows.Add(new FoldRow(
                    onset.OnsetTick,   // onset
                    kv.Key,            // 病种_id
                    patientId,         // patient_id —— 必留(ADR-006 Amendment B)
                    onset.PatientSeed, // patient_seed —— 取自 onset 载荷,不重算(规则六 :164)
                    kv.Value.Outcome,  // outcome —— Recovered / Died
                    kv.Value.TEnd));   // t_end —— 终态事件头部 Tick(ADR-032 ④a)
            }

            rows.Sort((a, b) =>
                a.Onset != b.Onset ? a.Onset.CompareTo(b.Onset) : a.DiseaseId.CompareTo(b.DiseaseId));

            return rows;
        }

        /// <summary>
        /// 抹除该病人的全部病史行,减去 <c>SkillGrown</c>(ADR-026 §⑦ 条件 ④)。其余保持原序。
        /// </summary>
        private static IReadOnlyList<SimEvent> Erase(IReadOnlyList<SimEvent> history, int patientId)
        {
            var retained = new List<SimEvent>(history.Count);
            for (int i = 0; i < history.Count; i++)
            {
                SimEvent e = history[i];
                bool belongsToFolded = e.Patient.Value == patientId;
                if (belongsToFolded && e.Kind != EventKind.SkillGrown)
                    continue;
                retained.Add(e);
            }

            return retained;
        }

        /// <summary>终态记录(私有传递形状;不进任何流 / 存档)。</summary>
        private readonly struct OutcomeRecord
        {
            public readonly int Outcome;
            public readonly long TEnd;

            public OutcomeRecord(int outcome, long tEnd)
            {
                Outcome = outcome;
                TEnd = tEnd;
            }
        }
    }
}
