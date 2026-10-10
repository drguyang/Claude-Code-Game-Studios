// M2 接线轮阶段 2 · 批次 C(体征链核心)—— 病人病程档案与处置剂量记录。
//
// 权威来源:
//   GDD disease-simulation.md 规则六(:160-170)—— 「存档存病史事件流,不存逐帧状态」:
//     病情与体征都是病史的**函数**;本档案只是该函数的**派生态缓存**(ADR-009 §一 Q1),
//     由事件流重建、不独立快照(ADR-010 §三 折叠口径)。
//   GDD disease-simulation.md F1 —— 「时间原点约定」:`τ = t − onset_tick`、`Δᵢ = t − tickᵢ`。
//   ADR-030 ④ —— `DiseaseOnset` 落病史流,载荷 `(onset_tick, disease_id, patient_id, patient_seed, seq)`。
//   ADR-009 Amendment I —— `DrugTreatmentApplied` / `EmergencyTreatmentApplied` / `EmergencyAttempt`
//     三 Kind 落病史流并归属具体病人;9 消费其 `polarity` / `drug_potency` / `half_life` 三量。
//   ADR-006 Amendment G-2 / ADR-029 —— 载荷字节住 blob 池;本文件只承接**解码后**的强类型形状。
//
// 落点理由(为什么状态在 Sim、解码不在 Sim):
//   `Sim` 只引用 `Sim.Contracts`(ADR-025 §① / b2 门),**物理上够不着 `Sim.Codec.PayloadCodec`**
//   ⇒ 「从 `PayloadRef` 解出 struct」这一步只能发生在同时可见 Sim 与 Sim.Codec 的装配
//   (= `Gameplay.Boot` 组合根,案 A)。解出之后的**纯整数域状态**归本文件(Sim 内,门 A 内侧)。
//   这与 ADR-029 ① 的分工同构:契约住 Contracts、编解码住 Codec、状态住 Sim、装配住 Boot。
//
// ⚠️ 派生态纪律:本档案**不是第二真源**。真源 = 病史事件流(ADR-005);
//   `DiseaseVitalsService` 只用流推进它(游标单向前进),存档 / 回放路径须能从流重建它。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.DiseaseSimulation
{
    /// <summary>
    /// 一次处置的求值三元组(载荷解码后的**纯整数域**截面)。
    /// </summary>
    /// <remarks>
    /// <para><b>字段与事件载荷逐字段同名同义</b>(AC-28:处置事件必须携带
    /// `polarity` / `drug_potency` / `half_life`;缺任一量则 F1 / F4 不可求值)——
    /// 本 struct 是把三条 Kind(<c>DrugTreatmentApplied</c> / <c>EmergencyTreatmentApplied</c>)
    /// 归一成 F1 和式的同一条输入面,不是第三套数据。</para>
    /// <para><b>Tick 不取自载荷字段而取自载荷 `Tick`</b>(两者本就同值;`TreatmentId` 同)。</para>
    /// </remarks>
    public readonly struct TreatmentDose
    {
        /// <summary>处置发生 tick(F1 的 `tickᵢ`;`Δᵢ = t − tickᵢ`)。</summary>
        public readonly long Tick;

        /// <summary>处置 id(动作表 / 处方表 ordinal;<c>treatable_by(d)</c> 门的查表键)。</summary>
        public readonly int TreatmentId;

        /// <summary>极性序数(载荷 `polarity`;`0 = 对症 · 1 = 对因`,见
        /// <see cref="ProgressionEvaluator.PolarityCausal"/>)。</summary>
        public readonly int Polarity;

        /// <summary>药效幅值(Q16.16)。**符号随数据走** —— F1 式体是 `+ Σ drug_potency×Decay`,
        /// 「对因与 Base 异号」由 11 / 10 的数据声明保证(取舍 2:9 不承担符号语义)。</summary>
        public readonly Fix DrugPotency;

        /// <summary>半衰期(tick;来自 21a `drug_profile` / 10 动作数据表)。
        /// `≤ 0` 会触发 F1 `Decay` 除零 ⇒ AC-28 写入期拒收,求值侧另见
        /// <c>ProgressionEvaluator.Decay</c> 的防御归零。</summary>
        public readonly long HalfLife;

        public TreatmentDose(long tick, int treatmentId, int polarity, Fix drugPotency, long halfLife)
        {
            Tick = tick;
            TreatmentId = treatmentId;
            Polarity = polarity;
            DrugPotency = drugPotency;
            HalfLife = halfLife;
        }
    }

    /// <summary>
    /// 单病人的病程档案 —— `DiseaseOnset` 建档、处置与急救判定输入追加;
    /// 由它提供 F1 求值所需的 `onset_tick` / 病种 / `patient_seed` 与处置和式输入。
    /// </summary>
    /// <remarks>
    /// <para><b>只增不改</b>:字段一旦由建档事件确定即不可变;处置只追加(F1 和式可交换 ⇒
    /// 顺序无关,AC-16 乱序重放的前提)。急救判定输入只计数、不改病程(判定的**结果**
    /// 由 `EmergencyTreatmentApplied` 落流,本档案不复算判定)。</para>
    /// <para><b>无病名 / 无身份泄漏</b>:只持有整数 id(AC-20 的 DTO 白名单精神同源)。</para>
    /// </remarks>
    public sealed class DiseaseCourse
    {
        private readonly List<TreatmentDose> _doses = new List<TreatmentDose>();

        /// <summary>病人 id(机制 A,ADR-006 Amendment B)。</summary>
        public PatientId Patient { get; }

        /// <summary>病种 id(须能查到注册表条目 —— 查不到由消费方 fail-loud)。</summary>
        public int DiseaseId { get; }

        /// <summary>病程起点 tick(F1 的 `onset_tick`,`τ = t − onset_tick` 的原点)。</summary>
        public long OnsetTick { get; }

        /// <summary>病人种子 = <c>hash(world_seed, patient_id)</c>(规则六 :164,禁 `Random.Range`)。
        /// 供 Noise / 逐病人纯函数使用 —— Noise 本身仍是 tripwire(恒 0),本字段先建档备取。</summary>
        public long PatientSeed { get; }

        /// <summary>已登记的判定输入条数(只计数,不参与 F1 求值)。</summary>
        public int EmergencyAttemptCount { get; private set; }

        /// <summary>最近一次判定输入的 action id(无 = -1 哨兵)。</summary>
        public int LastEmergencyActionId { get; private set; } = -1;

        internal DiseaseCourse(PatientId patient, int diseaseId, long onsetTick, long patientSeed)
        {
            Patient = patient;
            DiseaseId = diseaseId;
            OnsetTick = onsetTick;
            PatientSeed = patientSeed;
        }

        /// <summary>处置和式输入(F1 `Σ drug_potency×Decay` 的遍历面;追加序 = 入流序)。</summary>
        public IReadOnlyList<TreatmentDose> Doses => _doses;

        internal void AddDose(in TreatmentDose dose) => _doses.Add(dose);

        internal void NoteEmergencyAttempt(int actionId)
        {
            EmergencyAttemptCount++;
            LastEmergencyActionId = actionId;
        }
    }
}
