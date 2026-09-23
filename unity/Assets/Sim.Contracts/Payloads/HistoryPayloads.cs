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

    /// <summary>8(判断链)。一条事件表示「起」或「止」;写入期校验 t(止) ≥ t(起)。
    /// 放医馆本身不计数,EnvMod 不得推出本事件。</summary>
    public readonly struct CareAppliedPayload
    {
        public readonly long Tick;
        public readonly int ActionId;        // 枚举:护理动作表 ordinal,K ≤ |表|
        public readonly int Caregiver;
        public readonly int Phase;           // 枚举:起 | 止

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
