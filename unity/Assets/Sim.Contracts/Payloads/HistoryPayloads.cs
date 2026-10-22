// 病史流 13 支载荷。转录源 = registry payload_schema(逐字段),分类口径见 PayloadCommon.cs 文件头。
// 支 0 = 甲:本族 struct 是 **codec 解码后的强类型形状**;落盘 / 传输形状 = blob 内的定长段。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>25 直登(ADR-006)。去重键 = 五元组 (tick, actor_id, target_id, injury_id, dose_seq)。
    /// magnitude 单位 = PS;dose_seq 为事件流纯函数派生量(禁可变计数器,主机迁移后可重构)。
    /// 上界 ≤ tick 频率 × 攻击行动者数。</summary>
    public readonly struct InjuryOnsetPayload
    {
        public readonly int ActorId;
        public readonly int TargetId;
        public readonly int InjuryId;        // 枚举:INJ_* ordinal(住 25 表)
        public readonly Fix Magnitude;       // registry: i64(Fix raw)
        public readonly long Tick;
        public readonly int DoseSeq;

        public InjuryOnsetPayload(int actorId, int targetId, int injuryId, Fix magnitude, long tick, int doseSeq)
        {
            ActorId = actorId; TargetId = targetId; InjuryId = injuryId;
            Magnitude = magnitude; Tick = tick; DoseSeq = doseSeq;
        }
    }

    /// <summary>ADR-030(病程 onset / 病人出现 · 写者 = 9)。病人实体被创建、分配
    /// <c>patient_id</c> 的那一 tick 写出的**唯一**事件 —— 「病人出现」是它在在场视图 /
    /// 立案链上的**语义投影**,不立第二个 Kind(ADR-030 ① 甲案)。去重键 =
    /// (OnsetTick, PatientId)。<b>有界性</b> ≤ 病人创建率 ≤ <c>PATIENT_APPEARANCE_CAP</c>(24,
    /// ADR-008 §六),与 tick 频率无关。<para><c>PatientSeed</c> = <c>hash(world_seed, patient_id)</c>
    /// (禁 <c>Random.Range</c>,规则六 :164)—— 事件流纯函数派生量,主机迁移后可重构
    /// (ADR-007 核心不变量)。</para></summary>
    public readonly struct DiseaseOnsetPayload
    {
        public readonly long OnsetTick;      // 病人出现 / 病程开始的那一 tick
        public readonly int DiseaseId;       // 病种枚举(P0 · 8 项)
        public readonly int PatientId;       // IIdAuthority 机制 A(ADR-006 Amendment B 高水位重构依赖)
        public readonly long PatientSeed;    // = hash(world_seed, patient_id)
        public readonly long Seq;            // 主机 Append 时分配(ADR-006 Amendment A/C)

        public DiseaseOnsetPayload(long onsetTick, int diseaseId, int patientId,
            long patientSeed, long seq)
        {
            OnsetTick = onsetTick; DiseaseId = diseaseId; PatientId = patientId;
            PatientSeed = patientSeed; Seq = seq;
        }
    }

    /// <summary>
    /// 病程终态枚举(ADR-032 ②)。<b><c>Unspecified = 0</c> 是漏填哨兵,不是合法终态</b>。
    /// <para><b>零值方向为何必须留哨兵</b>:终态是 7a 折叠谓词 <c>Folded(p)</c> 的条件 ①
    /// (<c>persistence-service.md:316</c>)。若漏填默认落成 <c>Recovered</c>,
    /// 则死亡病人被**静默**标成痊愈 —— 折叠行 <c>outcome</c> 从此撒谎,而折叠**不可逆**
    /// (病史行已删)。故 0 必须是「未声明」,并由解码 mask 校验拒收。</para>
    /// </summary>
    public enum PatientOutcome : int
    {
        /// <summary>未声明 —— <b>不是合法终态</b>;解码期拒收。</summary>
        Unspecified = 0,

        /// <summary>痊愈(F4 痊愈支)。</summary>
        Recovered = 1,

        /// <summary>死亡(F4 死亡支)。</summary>
        Died = 2,
    }

    /// <summary>
    /// 病程终态(ADR-032 · 写者 = 9)—— 病人「痊愈 / 死亡」的**唯一事件载体**。
    /// <para><b>为什么需要它</b>:7a 的 <c>Folded(p)</c> 条件 ①「终态」
    /// (<c>persistence-service.md:316</c>)此前**零事件载体** —— 病史流 14 支 Kind 无一表示终态
    /// ⇒ 条件 ① 输入恒为空 ⇒ 折叠永不发生。本事件使其可从**流前缀**查询(存在性),
    /// **不需重放 F4**。</para>
    /// <para><b>⚠️ <c>t_end</c> 不在载荷里</b> —— 它就是事件头部的 <see cref="SimEvent.Tick"/>
    /// (判出终态的那一 tick 写出)。入载荷 = 同一量的第二处存储(第二真源,ADR-016 §一 同型病)。
    /// 折叠行 <c>t_end</c> 取 <c>event.Tick</c>。</para>
    /// <para><b>⚠️ <c>DiseaseId</c> 必带</b> —— F4 的 <c>d</c> 是 argmax
    /// (<c>disease-simulation.md:1120</c> 补注:取得 <c>position_agg</c> 的那个 <c>d</c>,
    /// 并列取 <c>DiseaseId</c> 最小者)⇒ 终态是**逐病种**的结论,不是「病人死于某个病」的病人级读法。
    /// 折叠行 <c>病种_id</c> 由此填。</para>
    /// <para><b>有界性</b>:终态在 F4 九态里是**吸收态**(痊愈 / 死亡无出边)⇒ 每病人每病种至多写一次
    /// ⇒ 上界 <c>Σ_p |D_p| ≤ PATIENT_APPEARANCE_CAP × |D_max|</c>,与 tick 频率无关
    /// (扩 ADR-008 §六,不重写)。</para>
    /// <para><b>⚠️ 原子性义务</b>:9 须在**判出终态的那一 tick**写出本事件。漏写 ⇒
    /// 「流里有终态」与「F4 重放说终态」漂移 ⇒ 该病人永不折叠,且无机械门可扫
    /// (唯一反查判据 = 「<c>Folded(p)=false</c> 而 F4 重放为终态」)。</para>
    /// </summary>
    public readonly struct PatientOutcomeChangedPayload
    {
        public readonly int PatientId;   // IIdAuthority 机制 A(折叠行 patient_id 必留)
        public readonly int DiseaseId;   // argmax d(disease-simulation.md:1120)
        public readonly int Outcome;     // PatientOutcome 枚举值(0 = 漏填哨兵,解码拒收)
        public readonly long Seq;        // 主机 Append 时分配(ADR-006 Amendment A/C)

        public PatientOutcomeChangedPayload(int patientId, int diseaseId, int outcome, long seq)
        {
            PatientId = patientId; DiseaseId = diseaseId; Outcome = outcome; Seq = seq;
        }
    }

    /// <summary>9(ADR-009 §三 骨架)。触发即落 —— 条件首次满足时写(给 F3 闭式求值一个锚点)。
    /// 触发次数 ≤ compound_max_triggers(每病人每规则)。</summary>
    public readonly struct CompoundTriggeredPayload
    {
        public readonly long Tick;
        public readonly int TargetId;
        public readonly int RuleId;
        public readonly int ComponentId;     // when.component_id

        public CompoundTriggeredPayload(long tick, int targetId, int ruleId, int componentId)
        {
            Tick = tick; TargetId = targetId; RuleId = ruleId; ComponentId = componentId;
        }
    }

    /// <summary>9。仅窗口型 bonus 落本事件(永久型无窗止)。窗止是阶跃,须入 F3 类 A 边界表。</summary>
    public readonly struct CompoundExpiredPayload
    {
        public readonly long Tick;
        public readonly int TargetId;
        public readonly int RuleId;

        public CompoundExpiredPayload(long tick, int targetId, int ruleId)
        {
            Tick = tick; TargetId = targetId; RuleId = ruleId;
        }
    }

    /// <summary>
    /// 护理动作相位(起 / 止)—— <c>CareAppliedPayload.Phase</c> 的序数**唯一 C# 定义**。
    ///
    /// <para><b>2026-10-22 立(用户裁定「甲+乙 都做」)</b>:此前(裁定前)<c>0 = 起 / 1 = 止</c>
    /// 只是 <c>DiseaseCourse.CarePhaseStart/Stop</c> 里的**落地约定**,**当时**全库无序数权威 ——
    /// <c>entities.yaml:2186</c> 只写「枚举(起 止)」,GDD <c>:164</c> 只给中文词「起/止」,
    /// author(24)的 <c>clinic-machine.md</c> 零定义。<b>现三处齐备</b>(承 <see cref="PatientOutcome"/> 先例):
    /// ① 本枚举(序数单一 C# 定义);② <c>entities.yaml:2186</c> 写死 `枚举(起=0, 止=1)`;
    /// ③ 机械断言钉住两者一致(<c>care_applied_writer_test.cs</c> 的
    /// <c>test_carePhase_ordinalsMatchRegistry</c> + <c>test_carePhase_enumMatchesForwardingConstants</c>)。</para>
    /// <para>⚠️ 上面「此前 … 全库无序数权威」是**历史陈述**,不是现状 —— 现状以本段末句为准。</para>
    ///
    /// <para><b>⚠️ 载荷字段仍是 <c>int</c>(<c>CareAppliedPayload.Phase</c>)</b> ——
    /// 与 <see cref="PatientOutcome"/> / <c>PoiState</c> 同构。把字段类型改成枚举会让
    /// <c>tools/kindgen/kindgen.py</c> 的 A2 判据转红(A2 只接受整数白名单与已登记 struct,
    /// 枚举类型名两者都不命中 ⇒ <c>fail</c>)。改类型前须先扩 kindgen。</para>
    ///
    /// <para><b>为什么不需要 <c>Unspecified = 0</c> 哨兵</b>(与 <see cref="PatientOutcome"/> 的差别):
    /// 终态的零值若被漏填会**静默撒谎**(死亡病人被标成痊愈),故那里必须留哨兵;
    /// 而本枚举的 0 是**合法值「起」** —— 一个漏填的 phase 会被读成「起」,
    /// 后果是照护支少记一个「止」,而**不会**产生「把 A 说成 B」的错误陈述。
    /// ⇒ 不设哨兵;越界值由写者前置 ②(写入期,具名结果码)与解码门(读取期)各自拒收。</para>
    /// </summary>
    public enum CarePhase : int
    {
        /// <summary>起 —— 玩家开始一个护理动作。</summary>
        Start = 0,

        /// <summary>止 —— 玩家结束该护理动作。</summary>
        Stop = 1,
    }

    /// <summary>8(判断链)。一条事件表示「起」或「止」;写入期校验 t(止) ≥ t(起)。
    /// 放医馆本身不计数,EnvMod 不得推出本事件。</summary>
    public readonly struct CareAppliedPayload
    {
        public readonly long Tick;
        public readonly int ActionId;        // 枚举:护理动作表 ordinal,K ≤ |表|
        public readonly int Caregiver;
        /// <summary>相位序数 —— 见 <see cref="CarePhase"/>(0 = 起 / 1 = 止)。
        /// 保持 <c>int</c> 不改成枚举:kindgen A2 只接受整数白名单。</summary>
        public readonly int Phase;

        public CareAppliedPayload(long tick, int actionId, int caregiver, int phase)
        {
            Tick = tick; ActionId = actionId; Caregiver = caregiver; Phase = phase;
        }
    }

    /// <summary>ADR-009 Amendment I(10 产出 / 主机物化)。语义 = **判定的输入**,不是结算输出。
    /// 中止发 0 条;有界性 = 每完成动作 ≤ 2 条(意图 + 结算),与帧率无关。
    /// edge_ticks 非递减;数组长度 = Edges(≤ 结构上界,超界 codec 拒收)。</summary>
    public readonly struct EmergencyAttemptPayload
    {
        public readonly int Action;
        public readonly int HoldTicks;
        public readonly int Edges;           // 沿计数 = EdgeTicks 的长度
        public readonly int MagPeak;
        public readonly int MagLast;
        public readonly int Method;          // 枚举:Manual | 跳过
        public readonly int ActorId;
        /// <summary>非递减的 press 沿 tick。本 struct 是 **codec 解码后的形状**(支 0 甲),
        /// 引用字段合法地只活在解码瞬间;落盘/传输 = blob 定长+变长段。
        /// 长度 = Edges(=0 时为空数组非 null);上界超界 codec 拒收。</summary>
        public readonly int[] EdgeTicks;

        public EmergencyAttemptPayload(int action, int holdTicks, int edges, int magPeak,
            int magLast, int method, int actorId, int[] edgeTicks)
        {
            Action = action; HoldTicks = holdTicks; Edges = edges; MagPeak = magPeak;
            MagLast = magLast; Method = method; ActorId = actorId; EdgeTicks = edgeTicks;
        }
    }

    /// <summary>ADR-009 Amendment I(10 写)。七项齐备;drug_potency 经 F-10.4 单一舍入
    /// (Q32.32 中间量,ADR-006 §三)。half_life 恒 0 会触发 9 的 Decay 除零 ⇒ AC-28 写入期拒收。</summary>
    public readonly struct EmergencyTreatmentAppliedPayload
    {
        public readonly long Tick;
        public readonly int TreatmentId;
        public readonly int ActorId;
        public readonly int Polarity;        // 枚举
        public readonly Fix DrugPotency;     // registry: i64(Fix raw)
        public readonly long HalfLife;       // half_life_ticks[action] 动作数据表
        public readonly int Method;          // 枚举:Manual | 跳过(使跳过在流上可判别)
        public readonly int Cause;           // 枚举
        public readonly long Seq;

        public EmergencyTreatmentAppliedPayload(long tick, int treatmentId, int actorId, int polarity,
            Fix drugPotency, long halfLife, int method, int cause, long seq)
        {
            Tick = tick; TreatmentId = treatmentId; ActorId = actorId; Polarity = polarity;
            DrugPotency = drugPotency; HalfLife = halfLife; Method = method; Cause = cause; Seq = seq;
        }
    }

    /// <summary>ADR-009 Amendment I(11 写)。形状与 EmergencyTreatmentApplied 一致但**不带
    /// method / cause**(药物类恒 Manual,携带即空字段)。不与动作类共用 Kind 的理由 =
    /// 写者结算来源不同 + 一个 Kind 无法同时落两条流。</summary>
    public readonly struct DrugTreatmentAppliedPayload
    {
        public readonly long Tick;
        public readonly int TreatmentId;
        public readonly int ActorId;
        public readonly int Polarity;        // 枚举
        public readonly Fix DrugPotency;     // 来自 21a drug_profile
        public readonly long HalfLife;       // 来自 21a drug_profile
        public readonly long Seq;

        public DrugTreatmentAppliedPayload(long tick, int treatmentId, int actorId, int polarity,
            Fix drugPotency, long halfLife, long seq)
        {
            Tick = tick; TreatmentId = treatmentId; ActorId = actorId; Polarity = polarity;
            DrugPotency = drugPotency; HalfLife = halfLife; Seq = seq;
        }
    }

    /// <summary>ADR-024 补齐轮(暴露 OQ-7a-9 折叠丢成长)。
    /// 无病例语境的动作类成长(采集 / 奔跑)取 Patient = PatientId.None = -1。
    /// Level 未升级 ⇒ 缺省 = 哨兵,**不写 0**。</summary>
    public readonly struct SkillGrownPayload
    {
        public const int LevelNotGrown = -1; // 未升级哨兵(非 0,零级是真实等级)
        public readonly int ActorId;         // 施予者 = 玩家,与 player_id 共用 id 空间(ADR-006 注记)
        public readonly int PatientId;       // 成长所依附的受伤实体;-1 = 无病例语境
        public readonly int SkillId;         // 枚举:技能表 ordinal
        public readonly int ObjectId;        // 枚举:对象表 ordinal(病种/品种/处方,由 SkillId 决定读哪张表)
        public readonly int NoveltyClass;    // 枚举:首次 | 冷却内 | 其它(三值)
        public readonly int Level;

        public SkillGrownPayload(int actorId, int patientId, int skillId, int objectId,
            int noveltyClass, int level)
        {
            ActorId = actorId; PatientId = patientId; SkillId = skillId; ObjectId = objectId;
            NoveltyClass = noveltyClass; Level = level;
        }
    }

    /// <summary>ADR-007 §三。ordinal = SplitMix64 消费序号。</summary>
    public readonly struct EventRolledPayload
    {
        public readonly int Win;             // 窗口索引
        public readonly int Tier;            // 枚举:档 ordinal
        public readonly long Ordinal;        // 抽取序号
        public readonly int ChosenKey;       // 枚举:事件表 ordinal

        public EventRolledPayload(int win, int tier, long ordinal, int chosenKey)
        {
            Win = win; Tier = tier; Ordinal = ordinal; ChosenKey = chosenKey;
        }
    }

    /// <summary>ADR-007 §三。CauseClueKey 缺省 = 哨兵 None(registry 的 "?" 记法 ⇒ 哨兵,非 nullable)。
    /// 世界级事件 Patient = PatientId.None(ADR-007 §四)。</summary>
    public readonly struct EventArrivedPayload
    {
        public readonly int EventKey;        // 枚举:事件表 ordinal
        public readonly int Tier;            // 枚举:档 ordinal
        public readonly WorldPos SpawnAnchor;
        public readonly int CauseClueKey;    // 哨兵 None = 缺省

        public EventArrivedPayload(int eventKey, int tier, WorldPos spawnAnchor, int causeClueKey)
        {
            EventKey = eventKey; Tier = tier; SpawnAnchor = spawnAnchor; CauseClueKey = causeClueKey;
        }
    }

    /// <summary>ADR-007 §三。</summary>
    public readonly struct ThreatDeferredPayload
    {
        public readonly int SlotIndex;       // 挂起槽位
        public readonly int EventKey;        // 枚举:事件表 ordinal

        public ThreatDeferredPayload(int slotIndex, int eventKey)
        {
            SlotIndex = slotIndex; EventKey = eventKey;
        }
    }

    /// <summary>ADR-007 §三。</summary>
    public readonly struct ThreatDeferralClearedPayload
    {
        public readonly int SlotIndex;
        public readonly int Reason;          // 枚举:补发 | 作废

        public ThreatDeferralClearedPayload(int slotIndex, int reason)
        {
            SlotIndex = slotIndex; Reason = reason;
        }
    }

    /// <summary>ADR-007 §三。</summary>
    public readonly struct HistoryFlagChangedPayload
    {
        public readonly int FlagId;          // 枚举:标记表 ordinal
        public readonly int NewValue;        // 整数标记值

        public HistoryFlagChangedPayload(int flagId, int newValue)
        {
            FlagId = flagId; NewValue = newValue;
        }
    }
}
