// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本文件由 tools/kindgen/kindgen.py 生成 —— **勿手改**。
//    真源 = design/registry/entities.yaml(ADR-024 §①);改 Kind 的合法顺序 =
//    先改 registry,再重跑 kindgen。A1–A5 断言在生成前执行,产物存在即断言已过。
//
// 命名口径偏离登记(承 b1a 文件头):ADR-024 生成物形状原文写 `SimEvent.Kind.X`
//   前缀,但 SimEvent 是 struct,「结构体成员访问其嵌套类型」在 C# 非法(编译期),
//   且 b1a 已裁 EventKind 住命名空间作用域(CS0102 拆除,见 SimEvent.cs 头注)。
//   故本产物用 `EventKind.X` —— 成员名与 registry 的 A5 差集零不受影响。
//   `throw new BuildContractException(kind)` 的该类型全案无定义件可依,
//   以 InvalidOperationException 承位;若后续 ADR 具名该异常,替换属机械改。
//
// 生成参数: 13 history / 5 case / 16 world = 34 支
// ─────────────────────────────────────────────────────────────────────────────

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>Kind → StreamId 纯函数路由(ADR-008 §一 · ADR-024)。白名单穷举 ⇒ default 运行期不可达。</summary>
    public static class StreamRouting
    {
        public static StreamId Of(EventKind kind) => kind switch
        {
            EventKind.CareApplied => StreamId.History,
            EventKind.CompoundExpired => StreamId.History,
            EventKind.CompoundTriggered => StreamId.History,
            EventKind.DrugTreatmentApplied => StreamId.History,
            EventKind.EmergencyAttempt => StreamId.History,
            EventKind.EmergencyTreatmentApplied => StreamId.History,
            EventKind.EventArrived => StreamId.History,
            EventKind.EventRolled => StreamId.History,
            EventKind.HistoryFlagChanged => StreamId.History,
            EventKind.InjuryOnset => StreamId.History,
            EventKind.SkillGrown => StreamId.History,
            EventKind.ThreatDeferralCleared => StreamId.History,
            EventKind.ThreatDeferred => StreamId.History,
            EventKind.CaseClosed => StreamId.Case,
            EventKind.CaseOpened => StreamId.Case,
            EventKind.JudgmentRecorded => StreamId.Case,
            EventKind.JudgmentRevised => StreamId.Case,
            EventKind.PatternRecognized => StreamId.Case,
            EventKind.ActorCellEntered => StreamId.World,
            EventKind.ConsequenceResolved => StreamId.World,
            EventKind.Craft => StreamId.World,
            EventKind.DropClaimed => StreamId.World,
            EventKind.DropDespawned => StreamId.World,
            EventKind.DropSpawned => StreamId.World,
            EventKind.EncounterEnded => StreamId.World,
            EventKind.EncounterStarted => StreamId.World,
            EventKind.EnemyInjuryOnset => StreamId.World,
            EventKind.InjuryStateChanged => StreamId.World,
            EventKind.PlayerDied => StreamId.World,
            EventKind.PoiStateChanged => StreamId.World,
            EventKind.ResourceHarvested => StreamId.World,
            EventKind.StructureModified => StreamId.World,
            EventKind.StructurePlaced => StreamId.World,
            EventKind.StructureRemoved => StreamId.World,
            default => throw new System.InvalidOperationException(
                "不可达:Kind 白名单由 kindgen 穷举(ADR-024)。抵达即 registry 与产物脱钩 —— 重跑 kindgen。"),
        };
    }
}
