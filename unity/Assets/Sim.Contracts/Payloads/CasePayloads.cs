// 病例流 6 支载荷。转录源 = registry payload_schema · ADR-008 §三(抄本已 2026-09-17 三轮订正)。
//   第 6 支 = `CaseReferred`(ADR-044,2026-11-09)。
// 口径提醒(文件头三条偏离,同 PayloadCommon.cs):id 用 int · PlayerId 类型不存在故用 int ·
// judgment 只落 sim 消费面三字段(freehand_text 住呈现装配)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>ADR-008 §三。立案 tick = 本事件自身的 SimEvent.Tick
    /// (`opened_tick` 字段 2026-09-17 已删除 —— 重复携带即同一事实两份真相)。
    /// case_id 只读派生,不落字段。</summary>
    public readonly struct CaseOpenedPayload
    {
        public readonly int PatientId;
        public readonly DiseaseIdSet DiseaseSnapshot;

        public CaseOpenedPayload(int patientId, DiseaseIdSet diseaseSnapshot)
        {
            PatientId = patientId; DiseaseSnapshot = diseaseSnapshot;
        }
    }

    /// <summary>ADR-008 §三 / §四。treated = 处置证据快照(须落 [CaseOpened.Tick, CloseTick] 窗口,
    /// 2026-09-17 方向订正后式)。幂等拒收(复核 #10)。</summary>
    public readonly struct CaseClosedPayload
    {
        public readonly int PatientId;
        public readonly CaseId CaseId;
        public readonly DiseaseIdSet DiseaseSet;
        public readonly bool Treated;

        public CaseClosedPayload(int patientId, CaseId caseId, DiseaseIdSet diseaseSet, bool treated)
        {
            PatientId = patientId; CaseId = caseId; DiseaseSet = diseaseSet; Treated = treated;
        }
    }

    /// <summary>ADR-008 §三(订正②补 salted_key)。世界级:PatientId 恒 PatientId.None = -1。
    /// member_set = **冻结三元组(anchor_case + disease_set + 版本)**(registry 2026-09-20 口径,
    /// 即下三字段;ADR-008 旧抄本 `CaseId[]` 与 registry 不符 —— ADR-024 §② 以 registry 为准,
    /// 抄本降级为路由注记,登记于文件头)。</summary>
    public readonly struct PatternRecognizedPayload
    {
        public readonly int PatientId;
        public readonly CaseId AnchorCase;       // 冻结三元组 · 1/3
        public readonly ulong SaltedKey;         // = WorldSeed 派生盐(ADR-008 §三 盐契约)
        public readonly DiseaseIdSet MemberDiseaseSet; // 冻结三元组 · 2/3
        public readonly int Version;             // 冻结三元组 · 3/3(病种映射表版本,append-only)

        public PatternRecognizedPayload(int patientId, CaseId anchorCase, ulong saltedKey,
            DiseaseIdSet memberDiseaseSet, int version)
        {
            PatientId = patientId; AnchorCase = anchorCase; SaltedKey = saltedKey;
            MemberDiseaseSet = memberDiseaseSet; Version = version;
        }
    }

    /// <summary>ADR-008 §三 + 2026-09-19 补 author_player_id(39 首轮评审)。</summary>
    public readonly struct JudgmentRecordedPayload
    {
        public readonly int PatientId;
        public readonly CaseId CaseId;
        public readonly int AuthorPlayerId;   // 机制 A 复用 IIdAuthority(ADR-006 第二十六批)
        public readonly int LexiconId;        // u16 值域;枚举 = case_judgment_lexicon 版本化 ordinal
        public readonly byte Confidence;      // u8 值域(档位语义 = OQ-CB-5 甲,落笔循环)

        public JudgmentRecordedPayload(int patientId, CaseId caseId, int authorPlayerId,
            int lexiconId, byte confidence)
        {
            PatientId = patientId; CaseId = caseId; AuthorPlayerId = authorPlayerId;
            LexiconId = lexiconId; Confidence = confidence;
        }
    }

    /// <summary>ADR-008 §三。语义 = **存新值,禁差分**(AC-51-B3 / OQ-51-8)。</summary>
    public readonly struct JudgmentRevisedPayload
    {
        public readonly int PatientId;
        public readonly CaseId CaseId;
        public readonly int AuthorPlayerId;
        public readonly int LexiconId;
        public readonly byte Confidence;

        public JudgmentRevisedPayload(int patientId, CaseId caseId, int authorPlayerId,
            int lexiconId, byte confidence)
        {
            PatientId = patientId; CaseId = caseId; AuthorPlayerId = authorPlayerId;
            LexiconId = lexiconId; Confidence = confidence;
        }
    }

    /// <summary>
    /// 钩子病例的**转诊 / 送医**结论文档(ADR-044 §D3)。
    /// <para><b>为什么不是病程终态</b>:F4 九态说的是「病怎么走」,而转诊说的是
    /// 「这个病人不再由我处置」—— 两者正交。`PatientOutcome` 的词表**不扩**
    /// (它的 `{1,2}` 在折叠行里是损坏哨兵,扩枚举会让旧档的坏字节变成合法数据,
    /// ADR-044 §1.2)。</para>
    /// <para><b>不与 <see cref="CaseClosedPayload"/> 互推</b>:转诊后病例仍可开着,直到有人结案
    /// (ADR-044 §D5)。</para>
    /// </summary>
    public readonly struct CaseReferredPayload
    {
        /// <summary>病人 id(非哨兵 —— 转诊是病人级事实)。</summary>
        public readonly int PatientId;

        /// <summary>被转诊的病例(与 `CaseClosed` / `JudgmentRecorded` 同形三元组)。</summary>
        public readonly CaseId CaseId;

        /// <summary>转诊理由闭集:<see cref="ReasonTransferred"/> / <see cref="ReasonHospitalized"/>。
        /// 解码期拒收其它值。</summary>
        public readonly int Reason;

        /// <summary>转诊 —— 转介他处(镇上药铺 / 同行)。</summary>
        public const int ReasonTransferred = 0;

        /// <summary>送医 —— 送入医院。</summary>
        public const int ReasonHospitalized = 1;

        public CaseReferredPayload(int patientId, CaseId caseId, int reason)
        {
            PatientId = patientId; CaseId = caseId; Reason = reason;
        }
    }
}
