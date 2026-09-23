// 世界流 16 支载荷。转录源 = registry payload_schema(ADR-024 §① 真源;stream: world)。
// 支 0 = 甲:本族 struct 是 codec 解码后的强类型形状(数组字段 = 引用形态,只活在解码瞬间)。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>ADR-016 §三 / ADR-020 §四(玩家 / AI 跨格)。三字段均整数 —— 无连续坐标 / 速度 / 朝向;
    /// 不另发 Exited(下一条 Entered 隐含上一条离开)。上界 = tick 频率(非格穿越率,2026-09-16 订正)。</summary>
    public readonly struct ActorCellEnteredPayload
    {
        public readonly int ActorId;
        public readonly WorldPos Cell;
        public readonly long Tick;

        public ActorCellEnteredPayload(int actorId, WorldPos cell, long tick)
        {
            ActorId = actorId; Cell = cell; Tick = tick;
        }
    }

    /// <summary>17 采集。node_id = ADR-015 逻辑层节点稳定标识(非 spawn_anchor 格坐标,OQ-4-11);
    /// gather_seq 计源 = 本 Kind 的节点计数。raw_quality **刻意不落流**(可由 SplitMix64 三项重算);
    /// out_quality 截断需采集者当刻技能等级 ⇒ 须落流。</summary>
    public readonly struct ResourceHarvestedPayload
    {
        public readonly long InstanceId;
        public readonly int NodeId;
        public readonly int GatherSeq;
        public readonly int Qty;
        public readonly int OutQuality;      // 枚举:品级档(经 QualityCap 截断)

        public ResourceHarvestedPayload(long instanceId, int nodeId, int gatherSeq, int qty, int outQuality)
        {
            InstanceId = instanceId; NodeId = nodeId; GatherSeq = gatherSeq;
            Qty = qty; OutQuality = outQuality;
        }
    }

    /// <summary>ADR-009 §七(身份进流 / 位置表现)。</summary>
    public readonly struct DropSpawnedPayload
    {
        public readonly long InstanceId;
        public readonly WorldPos SpawnAnchor;
        public readonly int ItemKey;         // 枚举:物品表 ordinal
        public readonly int Qty;

        public DropSpawnedPayload(long instanceId, WorldPos spawnAnchor, int itemKey, int qty)
        {
            InstanceId = instanceId; SpawnAnchor = spawnAnchor; ItemKey = itemKey; Qty = qty;
        }
    }

    /// <summary>ADR-009 §七。拾取判定 = 意图事件 + 当下判距 + 宽容半径(ADR-009 §七)。</summary>
    public readonly struct DropClaimedPayload
    {
        public readonly long InstanceId;
        public readonly int Claimer;
        public readonly long Tick;

        public DropClaimedPayload(long instanceId, int claimer, long tick)
        {
            InstanceId = instanceId; Claimer = claimer; Tick = tick;
        }
    }

    /// <summary>ADR-009 §七。</summary>
    public readonly struct DropDespawnedPayload
    {
        public readonly long InstanceId;
        public readonly int Qty;
        public readonly int Reason;          // 枚举:过期 | 被消耗(档表归 R-2 定稿)

        public DropDespawnedPayload(long instanceId, int qty, int reason)
        {
            InstanceId = instanceId; Qty = qty; Reason = reason;
        }
    }

    /// <summary>ADR-024 补齐轮(21a)。**两组等长对位**(2026-09-23 订正 —— 原注误称「五数组等长」并误引
    /// registry constraint,该条目实无等长文字):
    /// 输入组 = InputInstanceIds ⟷ ActualConsumed 逐被点名实例对位(20 的 fold 据此逐实例扣);
    /// 产出组 = OutputQty ⟷ OutputQuality ⟷ OutputInstanceIds 逐 outputs 行对位;
    /// 组间 n ⊥ m(item-database D-21-5:炮制 = n=1,m=1 特例,制作 = n>1)。
    /// 出处 = processing.md R-18-A 载荷块 + item-database F1/F2;拒收面在 Sim.Codec 写/读侧。
    /// ActualConsumed 命名保留 registry 原大小写语义(实耗量,int)。</summary>
    public readonly struct CraftPayload
    {
        public readonly int ActorId;
        public readonly int RecipeId;        // 枚举:21a 表 ordinal
        public readonly long StartTick;
        public readonly long DurationTicks;
        public readonly long[] InputInstanceIds;
        public readonly int[] ActualConsumed;
        public readonly int[] OutputQty;
        public readonly int[] OutputQuality; // 枚举:品级档
        public readonly long[] OutputInstanceIds;
        public readonly WorldPos ToolCell;

        public CraftPayload(int actorId, int recipeId, long startTick, long durationTicks,
            long[] inputInstanceIds, int[] actualConsumed, int[] outputQty, int[] outputQuality,
            long[] outputInstanceIds, WorldPos toolCell)
        {
            ActorId = actorId; RecipeId = recipeId; StartTick = startTick; DurationTicks = durationTicks;
            InputInstanceIds = inputInstanceIds; ActualConsumed = actualConsumed; OutputQty = outputQty;
            OutputQuality = outputQuality; OutputInstanceIds = outputInstanceIds; ToolCell = toolCell;
        }
    }

    /// <summary>ADR-015 §五 建造。structure_id 与 ItemInstanceId 同模式(计数器 + 高水位可重构)。
    /// 折叠谓词不适用于结构行。</summary>
    public readonly struct StructurePlacedPayload
    {
        public readonly long StructureId;
        public readonly WorldPos Cell;
        public readonly int ModuleId;
        public readonly int Orientation;
        public readonly int Variant;

        public StructurePlacedPayload(long structureId, WorldPos cell, int moduleId, int orientation, int variant)
        {
            StructureId = structureId; Cell = cell; ModuleId = moduleId;
            Orientation = orientation; Variant = variant;
        }
    }

    /// <summary>ADR-015 §五。拆除判定拒绝(不存在 / 格上有实体,EC-23-02/03)时不 Append;
    /// 返还失败走 DropSpawned,不静默吞。</summary>
    public readonly struct StructureRemovedPayload
    {
        public readonly long StructureId;
        public readonly WorldPos Cell;
        public readonly int ModuleId;

        public StructureRemovedPayload(long structureId, WorldPos cell, int moduleId)
        {
            StructureId = structureId; Cell = cell; ModuleId = moduleId;
        }
    }

    /// <summary>ADR-015 §五。EC-23-10:只改形态不改模块 ⇒ 不发 Removed + Placed。
    /// modified_fields 位掩码只载物理形态位(朝向 / 变体 / 换模块);
    /// **不载任何数值结果** —— 数值 = 24 的派生,不进流。</summary>
    public readonly struct StructureModifiedPayload
    {
        public readonly long StructureId;
        public readonly WorldPos Cell;
        public readonly int ModuleId;
        public readonly int NewOrientation;
        public readonly int NewVariant;
        public readonly int ModifiedFields;  // 位掩码

        public StructureModifiedPayload(long structureId, WorldPos cell, int moduleId,
            int newOrientation, int newVariant, int modifiedFields)
        {
            StructureId = structureId; Cell = cell; ModuleId = moduleId;
            NewOrientation = newOrientation; NewVariant = newVariant; ModifiedFields = modifiedFields;
        }
    }

    /// <summary>ADR-021 §三(6 = 唯一写者)。Patient = PatientId.None(不污染高水位)。
    /// 三态单调不可逆:0 Undiscovered → 1 Discovered → 2 Resolved(constants.PoiState)。</summary>
    public readonly struct PoiStateChangedPayload
    {
        public readonly int PoiId;
        public readonly int NewState;        // 枚举:PoiState

        public PoiStateChangedPayload(int poiId, int newState)
        {
            PoiId = poiId; NewState = newState;
        }
    }

    /// <summary>ADR-016 §二(敌人复用 25 伤情模型)。id 与病人共用 IIdAuthority 同一空间。
    /// magnitude = MAG_FLOOR + base_step[act],CP 恒 0 退化式(不引入 enemy_cp)。
    /// 去重键 = 五元组(同 InjuryOnset);敌行不适用折叠谓词。</summary>
    public readonly struct EnemyInjuryOnsetPayload
    {
        public readonly int ActorId;
        public readonly int TargetId;
        public readonly int InjuryId;
        public readonly Fix Magnitude;       // registry: i64(Fix raw)
        public readonly long Tick;
        public readonly int DoseSeq;

        public EnemyInjuryOnsetPayload(int actorId, int targetId, int injuryId, Fix magnitude, long tick, int doseSeq)
        {
            ActorId = actorId; TargetId = targetId; InjuryId = injuryId;
            Magnitude = magnitude; Tick = tick; DoseSeq = doseSeq;
        }
    }

    /// <summary>25(combat-and-weapon-lines.md)。命名用 InjuryStateChanged 不用 EnemyDowned ——
    /// 昏迷可逆(危殆→昏迷→苏醒),双向往返带 new_state。服务受伤实体集(敌人 / 玩家),
    /// 病人侧由 9 的病史流既有 Kind 承载。离线 CatchUp 须产生逐条相同序列(R18)。
    /// 上界 ≤ 转移次数 × 实体数。</summary>
    public readonly struct InjuryStateChangedPayload
    {
        public readonly int ActorId;
        public readonly int NewState;        // 枚举

        public InjuryStateChangedPayload(int actorId, int newState)
        {
            ActorId = actorId; NewState = newState;
        }
    }

    /// <summary>52 奇遇。与 EventArrived 同 tick 派生(中间不隔 tick,否则「事件已降临但敌人未出现」
    /// 成可观察真空窗)。上界 ≤ 遭遇次数。</summary>
    public readonly struct EncounterStartedPayload
    {
        public readonly int EncounterId;
        public readonly int ProtoId;
        public readonly WorldPos SpawnCell;
        public readonly int[] ActorIds;      // 解码形状,长度有界(遭遇生成表)

        public EncounterStartedPayload(int encounterId, int protoId, WorldPos spawnCell, int[] actorIds)
        {
            EncounterId = encounterId; ProtoId = protoId; SpawnCell = spawnCell; ActorIds = actorIds;
        }
    }

    /// <summary>52 奇遇。reason 必须是枚举不是布尔(51 回放分析须区分「逃了」与「打完了」)。
    /// EncounterEnded ≠ 实体消失(倒地敌人仍可被 10 救治 / 28 取材)。上界 ≤ 遭遇次数。</summary>
    public readonly struct EncounterEndedPayload
    {
        public readonly int EncounterId;
        public readonly int Reason;          // 枚举:脱离 | 全倒地 | 超时

        public EncounterEndedPayload(int encounterId, int reason)
        {
            EncounterId = encounterId; Reason = reason;
        }
    }

    /// <summary>ADR-024 §④ 幽灵引据订正后的具名出处。无 case_id(AC-37-25 ② 不可归因,2026-09-19 裁定)。
    /// 上界 ≤ 病例流条数(逐例至多 1 条)。</summary>
    public readonly struct ConsequenceResolvedPayload
    {
        public readonly int Outcome;         // 枚举:53 F-53.1 双轴
        public readonly int PatientId;       // 逐例结算患者,可携带真实 id

        public ConsequenceResolvedPayload(int outcome, int patientId)
        {
            Outcome = outcome; PatientId = patientId;
        }
    }

    /// <summary>26 death-and-respawn。无条件发出(背包为空也发);冷却期内不发;
    /// death_cell = 最后一条 ActorCellEntered 之 cell 原样携带(派生态,禁读实时物理位置)。
    /// 上界 ≤ 玩家数 / DEATH_COOLDOWN。</summary>
    public readonly struct PlayerDiedPayload
    {
        public readonly int ActorId;
        public readonly WorldPos DeathCell;
        public readonly long Tick;

        public PlayerDiedPayload(int actorId, WorldPos deathCell, long tick)
        {
            ActorId = actorId; DeathCell = deathCell; Tick = tick;
        }
    }
}
