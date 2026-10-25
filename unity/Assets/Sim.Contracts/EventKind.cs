// 权威来源:ADR-024 §① 真源 = design/registry/entities.yaml(37 支,stream 分布 15/5/17;
//          2026-10-25 `HouseCallStarted` 入表 36 → 37 —— TD-persist-04 动作② 结构裁定)
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
    /// <summary>三逻辑流全部 37 支具名事件 Kind(2026-10-25 起;原 36 支见追加区注)。</summary>
    public enum EventKind : int
    {
        // ── 病史流 History(15)────────────────────────────────────────────
        InjuryOnset,                 // 25 直登(ADR-006)
        DiseaseOnset,                // ADR-030(病程 onset / 病人出现 · 写者 = 9)
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

        // ── ⚠️ 追加区:新成员一律加在此处,禁止插入上方任何位置 ──────────────
        // 理由:本枚举是**隐式值**(无显式 `= N`),成员值 = 声明序号。
        //   序列化按 int 落盘(`SimEventCodec` / 黄金字节夹具),故**插入中间成员
        //   = 其后全部成员值 +1 = 历史存档读错 Kind = 静默数据损坏**。
        //   实测(2026-10-22):把 `PatientOutcomeChanged` 插在 `DiseaseOnset` 之后,
        //   致 `SkillGrown` 由 8 变 9,`simEventHeader_matchesGoldenBytes` 与
        //   `B03_sim_event_header_bytes` 两条黄金夹具**同时转红** —— 那两条是
        //   ADR-012 的确定性回归门,红了先查实现、**禁改期望值**(ADR-012 裁决③)。
        //   ⇒ 追加在末尾 = 既有成员值零位移,黄金字节不变,无需重签金标准。
        // ⚠️ 追加区成员仍须先改 registry 再重跑 kindgen(ADR-024 §①)。
        PatientOutcomeChanged,       // ADR-032(病程终态 · 写者 = 9 · Folded 条件 ① 的载体)
        HouseCallStarted,            // 52 出诊启动(2026-10-25 TD-persist-04 动作② 结构裁定;
                                     // 锚点白名单第三员 —— 规则六事件触发 checkpoint)
    }
}
