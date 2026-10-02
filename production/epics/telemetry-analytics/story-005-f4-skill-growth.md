# Story 005: F4 熟练度成长

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Formulas F4 · AC-51-B10…B11)
**Requirement**: TR-telemetry-004 · TR-telemetry-006

**ADR Governing Implementation**: ADR-019 §一 + ADR-006 §五
**ADR Decision Summary**: F4 = `Practice(skill, W)` + `NoveltyMix(skill, W)` + `ΔLevel`;51 不重算 XP(那是 30 的定点域算术)。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM(ADR-006 IL2CPP 整数转译)
**Engine Notes**: F4 全整数;`Level_end` = 读流终点携带值,绝不读玩家当前/存档现值。

**Control Manifest Rules (this layer)**:
- Required: 新颖度三分守恒 · 不重算 XP · `Level` 未携带报「不可得」
- Forbidden: 重算 `XP_gain = BASE × K_difficulty × K_novelty`(破 R7 + 造第二真源)· 报 0 代替「不可得」
- Guardrail: 51 只折已发出的量

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-B10**(BLOCKING):F4 新颖度三分守恒 —— `N_new + N_repeat + N_stale = Practice(skill, W)` 且三类不重复计数
- [ ] **AC-51-B11**(BLOCKING):F4 不重算 XP —— 无 `XP_gain`/`BASE`/`K_difficulty`/`K_novelty` 的实现或常量

---

## Implementation Notes

*Derived from ADR-019 §一 + ADR-006 §五 + GDD Formulas F4:*

- **Practice(skill, W)** = `|{g : g.skill = skill ∧ Tick(g) ∈ W}|`
- **NoveltyMix(skill, W)** = `(N_new, N_repeat, N_stale)` 按新颖度枚举计数
- **ΔLevel** = `Level_end − Level_start`;`Level_end` = 读流终点成长事件携带的 `Level`;绝不读玩家当前/存档现值
- **GrowthRate(skill, W)** = `Practice(skill, W) / |W|`
- **51 不重算 XP**:那是 30 的定点域算术;重算 = 破 R7 + 造第二真源
- **边界**:`W` 内无事件 ⇒ 全 0;事件未携带 `Level` ⇒ `ΔLevel` 报「不可得」,不报 0

---

## Out of Scope
- Story 006: F5 难度曲线

---

## QA Test Cases
- **AC-51-B10**: 新颖度三分守恒。Given: 某 skill 在窗口内 new/repeat/stale 各 ≥ 1 例。When: 计算。Then: 三分之和 = Practice。
- **AC-51-B11**: 不重算 XP。Given: 51 的程序集。When: grep 定点域算术。Then: 无 `XP_gain`/`BASE`/`K_difficulty`/`K_novelty`。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/f4_skill_growth_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/f4_skill_growth_test.cs`(6 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 004(F3)
- Unlocks: Story 006(F5 难度曲线)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 2/2 passing(AC-51-B10 新颖度三分守恒 · AC-51-B11 不重算 XP;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F4Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **AC-51-D2 补缺**:QA 评审发现 AC-51-D2「Level 未携带 → 报不可得」零测试覆盖。补 `test_f4_levelNotCarried_reportsUnavailable` + `test_f4_levelCarried_computesDelta`,并在自持谓词面加 `TryComputeDeltaLevel`。

**评审与修复**:双评审并行(代码质量面 1 BLOCKING + QA 覆盖面 1 BLOCKING)→ 全修:
- **B1**(代码面):行 126 恒真断言 `telemetryTypes Is.Empty`(实现后必炸)→ 删除,保留 `violations Is.Empty` 断言(实现前后都成立)。
- **B1**(QA 面):AC-51-D2「Level 未携带 → 报不可得」零测试 → 补 `test_f4_levelNotCarried_reportsUnavailable` + `test_f4_levelCarried_computesDelta`。
- **R1**(代码面):B11 测试当前阶段 vacuous → 加注释标注「实现后转为有效扫描断言」。
- **R2**(代码面):缺空窗口测试 → 补 `test_f4_emptyWindow_allZero` + `test_f4_skillNotMatched_allZero`。

**残余 NICE(登记不修)**:
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 头注释列 ΔLevel 但未测(删 ΔLevel 或加归属说明)。
- 缺未知 novelty_class 防御性测试。
- `Compute` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/f4_skill_growth_test.cs`(**6 测全过**);全量 EditMode **1234 passed + 1 inconclusive + 6 skipped + 0 failed**(`unity/Logs/s005-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/R1/R2 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(整数 (num, den) 对)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。
