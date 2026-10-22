// ADR-010 §二 —— 折叠行(病史流折叠的产物)。
//
// 权威来源:
//   ADR-010 §二                      —— 折叠行 = (onset, 病种_id, patient_id, patient_seed, outcome, t_end)
//   ADR-005 §Implementation Guidelines 5 —— 同一形状的原始出处
//   ADR-006 Amendment B              —— **patient_id 必留**(高水位 `max(patient_id)` 重构依赖)
//   ADR-032 ④a / 附录 A(persistence-service.md:648)—— 各字段的来源
//
// 六字段来源逐条:
//   onset        ← `DiseaseOnsetPayload.OnsetTick`(病程开始的那一 tick,可能早于病人实体创建)
//   DiseaseId    ← `PatientOutcomeChangedPayload.DiseaseId`(终态是**逐病种**的结论,ADR-032 ④b)
//   PatientId    ← 被折叠的病人(折叠行必留 —— 折叠丢弃流位置,seed 又是单向哈希)
//   PatientSeed  ← `DiseaseOnsetPayload.PatientSeed`(= hash(world_seed, patient_id),规则六 :164)
//   Outcome      ← `PatientOutcomeChangedPayload.Outcome`(Recovered = 1 / Died = 2;0 解码期即拒)
//   TEnd         ← **该事件的头部 `Tick`**,不是载荷字段(ADR-032 ④a:同一量的第二处存储 = 第二真源)
//
// ⚠️ 字段**顺序** = ADR-010 §二 逐字次序。构造顺序改动会改变任何按位置读折叠行的下游
//   (本件尚无字节级消费者,但黄金夹具在 7a 存档往返轮会按此序钉)。

using System;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 折叠行 —— 病人 × 病种一行的病史摘要。
    ///
    /// <para><b>不可逆</b>:折叠会抹除被折叠的病史行,本行是它们唯一的残留记录
    /// ⇒ <c>Outcome</c> 写错即「折叠行撒谎且无法回退」(ADR-032 ② 的零值哨兵正是为此)。</para>
    /// </summary>
    public readonly struct FoldRow : IEquatable<FoldRow>
    {
        /// <summary>病程开始 tick。</summary>
        public readonly long Onset;

        /// <summary>病种 id(终态是逐病种的结论)。</summary>
        public readonly int DiseaseId;

        /// <summary>病人 id —— **必留**(ADR-006 Amendment B)。</summary>
        public readonly int PatientId;

        /// <summary>病人种子 = <c>hash(world_seed, patient_id)</c>;取自 onset 事件载荷,不重算。</summary>
        public readonly long PatientSeed;

        /// <summary>终态:<see cref="PatientOutcome.Recovered"/> 或 <see cref="PatientOutcome.Died"/>。</summary>
        public readonly int Outcome;

        /// <summary>终态发生 tick(= 终态事件头部 <c>Tick</c>)。</summary>
        public readonly long TEnd;

        public FoldRow(long onset, int diseaseId, int patientId, long patientSeed, int outcome, long tEnd)
        {
            Onset = onset;
            DiseaseId = diseaseId;
            PatientId = patientId;
            PatientSeed = patientSeed;
            Outcome = outcome;
            TEnd = tEnd;
        }

        /// <inheritdoc />
        public bool Equals(FoldRow other) =>
            Onset == other.Onset
            && DiseaseId == other.DiseaseId
            && PatientId == other.PatientId
            && PatientSeed == other.PatientSeed
            && Outcome == other.Outcome
            && TEnd == other.TEnd;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is FoldRow other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int h = Onset.GetHashCode();
                h = (h * 397) ^ DiseaseId;
                h = (h * 397) ^ PatientId;
                h = (h * 397) ^ PatientSeed.GetHashCode();
                h = (h * 397) ^ Outcome;
                h = (h * 397) ^ TEnd.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(FoldRow a, FoldRow b) => a.Equals(b);

        public static bool operator !=(FoldRow a, FoldRow b) => !a.Equals(b);

        /// <summary>排障用文本(不落盘)。</summary>
        public override string ToString() =>
            $"FoldRow(onset={Onset}, disease={DiseaseId}, patient={PatientId}, seed={PatientSeed}, " +
            $"outcome={Outcome}, tEnd={TEnd})";
    }
}
