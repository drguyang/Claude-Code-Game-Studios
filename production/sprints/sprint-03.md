# Sprint 03 Plan

> **Status (2026-10-02 完成)**: **17/17 story Complete** ✅ —
> 全部 6 个 story 经双代理评审修复后测试全绿。
> 详见 `production/epics/emergency-procedures/EPIC.md`。
>
> ⚠️ **「17/17」= story 数,≠ sprint 级 AC 全满足**。**AC-S03-5(跨平台黄金夹具)未兑现** ——
> emergency 半边 = `Assert.Ignore` NOT-RUN,combat 半边 = 零存在(根因 = ADR-012 CI 矩阵未激活)。
> 明细见下方 §Acceptance Criteria。本 sprint **不因 AC-S03-5 未兑现而回退 story 完成度**,
> 但该缺口**不得被「17/17」掩盖**(2026-10-02 状态回填轮实测登记)。

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
| 1 | combat-weapons | Story 001 — 动作表 schema 与烘焙构建门 | 2 | — | Complete ✅ 2026-10-01 |
| 2 | combat-weapons | Story 002 — 攻击求值点:意图通道 · 占用门 · tick 内次序 | 2 | 1 | Complete ✅ 2026-10-01 |
| 3 | combat-weapons | Story 003 — 命中判定 F-25-1(整数格距离 · 白名单 · Down 查询) | 2 | 2 | Complete ✅ 2026-10-01 |
| 4 | combat-weapons | Story 004 — magnitude 装配 F-25-2 与 CombatPower 交接 | 1 | 3 | Complete ✅ 2026-10-01 |
| 5 | combat-weapons | Story 005 — 冷却 / 切换 / 压制(S-1/S-2/S-3 与 IsSuppressed) | 1 | 4 | Complete ✅ 2026-10-01 |
| 6 | combat-weapons | Story 006 — onset 事件流 · dose_seq 派生 · F-25-8 平衡门 | 1 | 5 | Complete ✅ 2026-10-01 |
| 7 | enemy-ai | Story 001 — 行为程序载体:ai_enemy.json 烘焙 schema 与整数决策器 | 2 | — | Complete ✅ 2026-10-01 |
| 8 | enemy-ai | Story 002 — 感知与目标:三源白名单 · Band 分档互斥 · 整数视线与 Target(e) | 2 | 7 | Complete ✅ 2026-10-01 |
| 9 | enemy-ai | Story 003 — 敌意状态机:六态转移 · 士气/脱离单一出处与 LOD 节流 | 2 | 8 | Complete ✅ 2026-10-01 |
| 10 | enemy-ai | Story 004 — 确定性移动与寻路:定点累加器步进 · 整数 A* 与路径重规划 | 2 | 9 | Complete ✅ 2026-10-01 |
| 11 | enemy-ai | Story 005 — 遭遇生命周期 · 伤情真值路由与呈现信号契约 | 1 | 10 | Complete ✅ 2026-10-01 |
| 12 | emergency-procedures | Story 001 — EmergencyReading 读数与直读通道契约 | 2 | — | Complete ✅ 2026-10-02 |
| 13 | emergency-procedures | Story 002 — 动作表、熟练度表与 result_mul 烘焙 | 2 | 12 | Complete ✅ 2026-10-02 |
| 14 | emergency-procedures | Story 003 — Judge 三扇门定点纯函数 | 2 | 13 | Complete ✅ 2026-10-02 |
| 15 | emergency-procedures | Story 004 — Aggregate、可靠上行与主机落流 | 2 | 14 | Complete ✅ 2026-10-02 |
| 16 | emergency-procedures | Story 005 — 模态期:跳过、中止、档位意图与输入压制 | 1 | 15 | Complete ✅ 2026-10-02 |
| 17 | emergency-procedures | Story 006 — 手感、预表现与键鼠回退 | 1 | 16 | Complete ✅ 2026-10-02 |

> **⚠️ 本表已于 2026-10-02 按 story 真件重写(状态回填轮)**。原表的 story 标题与真实 story 件**系统性不符** ——
> 它按一份**旧故事计划**写成,从未回填:例如原 `combat-weapons #001` 写「战斗基础与伤害公式」而真件是
> 「动作表 schema 与烘焙构建门」;原 `enemy-ai #003` 写「寻路与移动」而真件是「敌意状态机…」;
> 原 `combat-weapons #006` 写「跨平台黄金夹具」而真件是「onset 事件流…」。上表标题**逐条取自各 story 件的 `# ` 行**。

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

| AC | 判据 | 状态 |
|----|------|------|
| AC-S03-1 | 17 个 story 的单元测试全部通过（EditMode） | ✅ 坐实(2026-10-02 batchmode `total 2022 · passed 1989 · failed 0`) |
| AC-S03-2 | combat-weapons 伤害公式 + 武器线 + 压制 + 战斗效能 | ✅ 6/6 story Complete |
| AC-S03-3 | enemy-ai 状态机 + 感知 + 寻路 + 战斗行为 + 冻结 | ✅ 5/5 story Complete |
| AC-S03-4 | emergency-procedures CPR + 止血 + 判定 + 音频 | ✅ 6/6 story Complete |
| AC-S03-5 | 跨平台黄金夹具入库（combat + emergency） | 🔴 **未兑现**(见下) |

### 🔴 AC-S03-5 未兑现 —— 登记(2026-10-02 状态回填轮实测)

**「17/17 Complete」指的是 story 数,不等于 5 条 sprint 级 AC 全部满足。** AC-S03-5 实测**未兑现**:

| 半边 | 实测 | 证据 |
|---|---|---|
| **emergency** | **NOT-RUN** | `Tests/EditMode/EmergencyProcedures/judge_test.cs:256` 的 `test_ac1004b_goldenFixture_notRun` 全文 = `Assert.Ignore("NOT-RUN: AC-10-04b 跨平台黄金夹具待 ADR-012 矩阵实跑")` ⇒ 夹具**未入库** |
| **combat** | **零存在** | `Tests/EditMode/Combat/` 6 个 fixture **无一**含 golden 夹具;`combat-weapons/story-006` 真件标题为「onset 事件流 · dose_seq 派生 · F-25-8 平衡门」,**不含**黄金夹具职责 |

**仓内唯一的黄金夹具与二者无关**:`tests/unit/item_database/golden/golden-v1.txt`(16 场景 S01–S16)属 **21a item-database**,由 `golden_v1_reference.py` 独立产出(ADR-012 双级夹具的**单元级**半边)。**ADR-012 的三格常驻矩阵本身尚未激活**(需 `UNITY_LICENSE` secret,见 `sprint-04.md` 风险登记),故 combat / emergency 的集成级字节夹具**结构性无法产出**。

**结论**:AC-S03-5 的根因 = **ADR-012 CI 矩阵未激活**,非 story 实现缺失。本行**不划结** —— 待矩阵激活后另开轮次补夹具。原表把本 AC 与 story 完成度混同,已在 2026-10-02 拆开。

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
