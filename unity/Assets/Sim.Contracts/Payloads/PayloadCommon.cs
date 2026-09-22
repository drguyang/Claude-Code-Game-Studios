// 权威来源:registry design/registry/entities.yaml 各 Kind 的 payload_schema(ADR-024 §① 真源)
//          · 支 0 裁定(2026-09-22 批):载荷联合形态 = 甲「header/blob 分家」——
//            SimEvent 降为纯 header,Payload 字段 = 全整数 PayloadRef,
//            载荷字节住流持有的不可变 blob;强类型访问由 Sim.Codec 按 registry schema 解码
//            (访问签名登记 = codec 轮义务,见卡 u0-b-b1b §支6-1)。
//          · 支 1-a 裁定:「i64(Fix raw)」类字段在内存形状用 Fix(long 是落盘形状)。
//          · 支 1-b 裁定:「枚举」类字段一律 int + 注释标 ordinal 来源(contracts 不造枚举,
//            防 ordinal 表第二真源;定义住各系统烘焙数据)。
//          · 支 1-c 裁定:DiseaseIdSet = 定长 bitmask(承 append-only ordinal 门,
//            case-system.md 2026-09-17 类型订正 FixSet→DiseaseIdSet)。
//
// ⚠️ 与 ADR-008 §三 C# 抄本的一处刻意偏离(实现被迫修正类,登记):
//   抄本写 `JudgmentRecordedPayload { PatientId patient_id; ... PlayerId author_player_id; ... }`,
//   而 **`PlayerId` 类型全库不存在**(registry 明写 author_player_id: i32),
//   且 registry 所有 *_id 字段一律 i32。转录一律以 **registry 为准**:id 用 int,
//   仅 SimEvent.Patient 保 PatientId 强类型(它是全序键成员,ADR-006 点名)。
//
// ⚠️ JudgmentRecordedPayload / JudgmentRevisedPayload **不带 judgment 子 struct**(登记残留):
//   registry 例外裁定 freehand_text = string「仅呈现层可读」⇒ sim 消费面只有
//   lexicon_id/confidence;「sim 读它就是违例」纪律下,sim 侧访问 = 这三字段 + SimEvent.Patient。
//   完整 Judgment(含自由文本)住呈现装配(8 / 37 / 42 侧),非本契约层类型。

// ⚠️ Payloads/ 下的 34 支 per-Kind payload struct **刻意不写构造函数**:
//   其唯一构造入口 = Sim.Codec 的 blob 解码(codec 轮落地)。b1b 的硬验收 = 编译通过,
//   无消费者 ⇒ readonly 字段无 ctor 不影响编译(readonly 仅可在 ctor / 变量初始化器赋值,
//   若日后确有直接构造需求,归首个消费代码批补 ctor,不预先扩 API 面)。
//   本文件的 PayloadRef / CaseId / DiseaseIdSet 带 ctor(header 与键型在 codec 之外有组装需求)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 载荷引用(支 0 · 甲)。全整数,住 SimEvent header。
    /// BlobId 指向流实现的不可变 blob 池;Offset/Length 界定本事件载荷段。
    /// 零载荷 Kind(如 ThreatDeferred)允许 Length = 0 —— 但本批 34 支均有载荷,预留语义。
    /// </summary>
    public readonly struct PayloadRef
    {
        /// <summary>blob 池内句柄(流实现分配;跨进程传输时的重映射归 45,ADR-001 轮)。</summary>
        public readonly int BlobId;
        /// <summary>载荷段起始偏移(字节)。</summary>
        public readonly int Offset;
        /// <summary>载荷段长度(字节)。codec 按 Kind 的 schema 校验之。</summary>
        public readonly int Length;

        public PayloadRef(int blobId, int offset, int length) { BlobId = blobId; Offset = offset; Length = length; }
    }

    /// <summary>
    /// 病例锚点三元组 = 全序键 (Tick, Patient, Seq)(ADR-008 复核 #1:全序键补 Patient)。
    /// registry 五处「case_id: 三元组」/「anchor_case」的转录形。
    /// </summary>
    public readonly struct CaseId
    {
        public readonly long Tick;
        public readonly int Patient;   // registry: i32(同 SimEvent.Patient 的值域;世界级 = None = -1)
        public readonly long Seq;

        public CaseId(long tick, int patient, long seq) { Tick = tick; Patient = patient; Seq = seq; }
    }

    /// <summary>
    /// 病种 ordinal 集合(支 1-c · bitmask)。
    /// ordinal 映射表住 9 的病种注册表(ADR-014 烘焙,append-only:既有条目号永不重用、永不改义,
    /// case-system.md 2026-09-17 裁定)。**上限 = 64 病种**;超界 = 数据层构建失败,非本 struct 职责。
    /// 禁 Fix(塞 Q16.16 = D-21-17 同类错,ADR-008 订正①点名)。
    /// </summary>
    public readonly struct DiseaseIdSet
    {
        public readonly ulong Bits;

        public DiseaseIdSet(ulong bits) { Bits = bits; }
    }
}
