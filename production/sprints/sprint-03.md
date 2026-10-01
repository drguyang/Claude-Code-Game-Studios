# Sprint 03 Plan

> **Status (2026-10-01 停刷)**: **11/17 Complete** ✅ (combat-weapons 6 + enemy-ai 5) —
> EditMode 全绿(【超算】batchmode:Combat 47 / EnemyAI 43),提行见
> `production/epics/{combat-weapons,enemy-ai}/EPIC.md`。**emergency-procedures 6 个 story
> 全部未实现**;其 Story 002(CPR)BLOCKED-BY-`OQ-10-6`,须先裁该 OQ 方可开工 —— 详见
> `production/epics/emergency-procedures/EPIC.md`。⚠️ 本 sprint **提前 2 周以上开工**
> (计划起 2026-11-02),属「逐个消耗」排序的执行结果,非计划漂移。

**Sprint**: 3
**Milestone**: Gameplay Core Complete
**Duration**: 2026-11-02 ~ 2026-11-15（2 周）
**Goal**: 完成 gameplay 层核心 — 战斗、敌人 AI、急救动作
**Capacity**: ~17 story points
**Dependency order**: 按「先战斗、再 AI、再急救」排序

---

## Stories

| # | Epic | Story | Story Points | Depends On | Status |
|---|------|-------|:------------:|------------|--------|
| 1 | combat-weapons | Story 001 — 战斗基础与伤害公式 | 2 | — | Complete ✅ 2026-10-01 |
| 2 | combat-weapons | Story 002 — 武器线与攻击动作 | 2 | 1 | Complete ✅ 2026-10-01 |
| 3 | combat-weapons | Story 003 — 压制与硬直 | 2 | 2 | Complete ✅ 2026-10-01 |
| 4 | combat-weapons | Story 004 — 战斗效能与医术修正 | 1 | 3 | Complete ✅ 2026-10-01 |
| 5 | combat-weapons | Story 005 — 战斗音频与反馈 | 1 | 4 | Complete ✅ 2026-10-01 |
| 6 | combat-weapons | Story 006 — 跨平台黄金夹具 | 1 | 5 | Complete ✅ 2026-10-01 |
| 7 | enemy-ai | Story 001 — 敌人 AI 基础与状态机 | 2 | — | Complete ✅ 2026-10-01 |
| 8 | enemy-ai | Story 002 — 感知与决策 | 2 | 7 | Complete ✅ 2026-10-01 |
| 9 | enemy-ai | Story 003 — 寻路与移动 | 2 | 8 | Complete ✅ 2026-10-01 |
| 10 | enemy-ai | Story 004 — 战斗行为 | 2 | 9 | Complete ✅ 2026-10-01 |
| 11 | enemy-ai | Story 005 — 冻结与 LOD | 1 | 10 | Complete ✅ 2026-10-01 |
| 12 | emergency-procedures | Story 001 — 急救动作基础 | 2 | — | Ready |
| 13 | emergency-procedures | Story 002 — CPR 与心肺复苏 | 2 | 12 | BLOCKED-BY-OQ-10-6 |
| 14 | emergency-procedures | Story 003 — 止血与包扎 | 2 | 13 | Ready |
| 15 | emergency-procedures | Story 004 — 急救判定与输入 | 2 | 14 | Ready |
| 16 | emergency-procedures | Story 005 — 急救音频与反馈 | 1 | 15 | Ready |
| 17 | emergency-procedures | Story 006 — 跨平台黄金夹具 | 1 | 16 | Ready |

---

## Dependency Graph（sprint 内）

```
无依赖层（并行）:
  [1] combat-weapons #001
  [7] enemy-ai #001
  [12] emergency-procedures #001

第二层:
  [2] combat-weapons #002 ← [1]
  [8] enemy-ai #002 ← [7]
  [13] emergency-procedures #002 ← [12]

第三层:
  [3] combat-weapons #003 ← [2]
  [9] enemy-ai #003 ← [8]
  [14] emergency-procedures #003 ← [13]

第四层:
  [4] combat-weapons #004 ← [3]
  [10] enemy-ai #004 ← [9]
  [15] emergency-procedures #004 ← [14]

第五层:
  [5] combat-weapons #005 ← [4]
  [11] enemy-ai #005 ← [10]
  [16] emergency-procedures #005 ← [15]

第六层:
  [6] combat-weapons #006 ← [5]
  [17] emergency-procedures #006 ← [16]
```

---

## Acceptance Criteria（sprint 级）

| AC | 判据 |
|----|------|
| AC-S03-1 | 17 个 story 的单元测试全部通过（EditMode） |
| AC-S03-2 | combat-weapons 伤害公式 + 武器线 + 压制 + 战斗效能 |
| AC-S03-3 | enemy-ai 状态机 + 感知 + 寻路 + 战斗行为 + 冻结 |
| AC-S03-4 | emergency-procedures CPR + 止血 + 判定 + 音频 |
| AC-S03-5 | 跨平台黄金夹具入库（combat + emergency） |

---

## Out of Scope（不进入本 sprint）

| Story | 原因 |
|-------|------|
| 45 联机实现 | P1b，非 gameplay 层 |
| 七屏走查 | 依赖 45 联机夹具 |
| 手柄走查 | P1a |
| 任何 P1a story | 不在 P0 范围内 |

---

## Risk & Notes

- **combat-weapons** 是 gameplay 层核心，F-25 公式必须严格按 GDD 实现
- **enemy-ai** 复用 9 的伤情模型（ADR-016 §二）
- **emergency-procedures** 是 P0 唯一需要「手感」的系统（L_input < 50 ms）
- 三个 epic 的跨平台黄金夹具是 ADR-012 CI 门的对拍面
