// 权威来源:ADR-024 §① 真源 = design/registry/entities.yaml(34 支,stream 分布 13/5/16)
//          · 成员级出处见各支 registry 条目的 source 字段(ADR-007 §三 / ADR-008 §三 /
//            ADR-009 Amendment F–I / ADR-016 §二 / ADR-021 §三 等)
//
// ⚠️ 本枚举与 kindgen 生成的 StreamRouting.g.cs 之间由 ADR-024 断言 A3(无重名)/
//    A5(双向差集归零)锁死;增删 Kind 的合法顺序 = 先改 registry,再重跑 kindgen。
// ⚠️ **成员序不承载语义**:跨流全序靠 (Tick, StreamPriority, Patient, Seq),不靠枚举值;
//    但按 stream 分组排列是 registry 的镜像,便于人查。
// ⚠️ 命名口径:类型住命名空间作用域而非嵌套于 SimEvent —— 理由见 SimEvent.cs 文件头。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>三逻辑流全部 34 支具名事件 Kind。</summary>
    public enum EventKind : int
    {
        // ── 病史流 History(13)────────────────────────────────────────────
        InjuryOnset,                 // 25 直登(ADR-006)
        CompoundTriggered,           // 9(ADR-009 §三 骨架)
        CompoundExpired,             // 9
        CareApplied,                 // 8(判断链)
        EmergencyAttempt,            // ADR-009 Amendment I(10 产出 / 主机物化)
        EmergencyTreatmentApplied,   // Amendment I(10 写)
        DrugTreatmentApplied,        // Amendment I(11 写)
        SkillGrown,                  // ADR-024 补齐轮(暴露 OQ-7a-9 折叠丢成长)
        EventRolled,                 // ADR-007 §三
        EventArrived,                // ADR-007 §三
        ThreatDeferred,              // ADR-007 §三
        ThreatDeferralCleared,       // ADR-007 §三
        HistoryFlagChanged,          // ADR-007 §三

        // ── 病例流 Case(5)───────────────────────────────────────────────
        CaseOpened,                  // ADR-008 §三
        CaseClosed,                  // ADR-008 §三
        PatternRecognized,           // ADR-008 §三
        JudgmentRecorded,            // ADR-008 §三(读数 / 落笔)
        JudgmentRevised,             // ADR-008 §三(改写史)

        // ── 世界流 World(16)─────────────────────────────────────────────
        ActorCellEntered,            // ADR-016 §三 / ADR-020 §四(玩家 / AI 跨格)
        ResourceHarvested,           // 17 采集
        DropSpawned,                 // ADR-009 §七(身份进流 / 位置表现)
        DropClaimed,                 // ADR-009 §七
        DropDespawned,               // ADR-009 §七
        Craft,                       // ADR-024 补齐轮(21a)
        StructurePlaced,             // ADR-015 §五 建造
        StructureModified,           // ADR-015 §五
        StructureRemoved,            // ADR-015 §五
        PoiStateChanged,             // ADR-021 §三(6 = 唯一写者)
        EnemyInjuryOnset,            // ADR-016 §二(敌人复用 25 伤情模型)
        InjuryStateChanged,          // 25(combat-and-weapon-lines.md)
        EncounterStarted,            // 52 奇遇
        EncounterEnded,              // 52 奇遇
        ConsequenceResolved,         // ADR-024 §④ 幽灵引据订正后的具名出处
        PlayerDied,                  // 26 death-and-respawn
    }
}
