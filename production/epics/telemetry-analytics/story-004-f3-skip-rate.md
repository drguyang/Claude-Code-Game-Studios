# Story 004: F3 跳过率

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Formulas F3 · AC-51-B8…B9)
**Requirement**: TR-telemetry-004

**ADR Governing Implementation**: ADR-019 §一
**ADR Decision Summary**: F3 = `Skip / Opportunity`;`SkipByChoice` 与降级强制分开报;`NotOffered` 不入分母。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: F3 全整数;分母 = 可跳过机会数(提供小游戏的实例数)。

**Control Manifest Rules (this layer)**:
- Required: 两个独立分子(`Skip` 与 `SkipByChoice`)· `NotOffered` 不入分母
- Forbidden: 只输出单一 `SkipRate`(合并 = 结构性失败)· 用病例数等代理凑分母
- Guardrail: 跳过率的分母与准确率的分母不共享

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-B8**(BLOCKING):F3 分开报 + 分母口径 —— 输出两个独立分子(`Skip` 与 `SkipByChoice`);`NotOffered` 不入分母;不存在只输出单一 `SkipRate` 的输出形状
- [ ] **AC-51-B9**(ADVISORY → BLOCKED-BY-`OQ-10-8`):F3 真实会话可得性 —— 集成测试目录 `tests/integration/51/` 未建 ⇒ 记 `NOT-RUN`,不得记绿;`OQ-10-8` 未结前不代为判绿

---

## Implementation Notes

*Derived from ADR-019 §一 + GDD Formulas F3:*

- **Opportunity** = `|{a ∈ 动作实例事件 : Payload(a).resolution ∈ {Played, Skipped}}|`
- **Skip** = `|{a : resolution = Skipped}|`;**SkipByChoice** = `|{a : resolution = Skipped ∧ cause = 玩家选择}|`
- **SkipRate** = `Skip / Opportunity`;选择跳过率 = `SkipByChoice / Opportunity`
- **分母** = 可跳过机会数(该动作族「提供了小游戏」的实例数);不是独立 skip 流;不许用病例数等代理
- **NotOffered**(无小游戏的动作)不入分母
- **分开报**:降级强制跳过不是支柱一的失败;合并会把网络差的会话误读成「小游戏没人玩」
- **边界**:`Opportunity = 0` ⇒ 不可定义(报 `(0,0)`)

---

## Out of Scope

- Story 002: F1 判断准确率(分母不共享)
- Story 003: F2 误诊分布

---

## QA Test Cases

- **AC-51-B8**: F3 分开报 + 分母口径。Given: `Skipped ∧ cause=玩家选择` 与 `Skipped ∧ cause=降级` 各 ≥ 1 例,另含 `NotOffered` 实例。When: 计算。Then: 输出两个独立分子;`NotOffered` 不入分母。
- **AC-51-B9**: 真实会话可得性。Given: 10 的动作事件族落地后的一次真实会话。When: 离线重算。Then: `Opportunity ≠ 0` 且两类 `cause` 均可见。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/f3_skip_rate_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/f3_skip_rate_test.cs`(5 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 002(F1)
- Unlocks: Story 005(F4 熟练度成长)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 2/2 passing(AC-51-B8 分开报 + 分母口径 · AC-51-B9 真实会话可得性 BLOCKED-BY-OQ-10-8;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F3Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **AC-51-B9 用 Assert.Ignore**:集成测试目录 `tests/integration/51/` 未建 ⇒ 真实会话不可得,记 NOT-RUN(禁借绿)。`OQ-10-8` 未结前不代为判绿。

**评审与修复**:双评审并行(代码质量面 1 BLOCKING + QA 覆盖面 1 BLOCKING)→ 全修:
- **B1**(代码面 + QA 面同一问题):缺 `Opportunity = 0` 分母零用例(AC-51-B4 明文要求 F3 侧)→ 补 `test_f3_denominatorZero_reports00` + `test_f3_allNotOffered_denominatorZero`。
- **R1**(代码面 + QA R1 同一问题):AC-51-B8「不存在单一 SkipRate 输出形状」未显式断言 → 补 `Assert.That(skipNum, Is.Not.EqualTo(choiceNum))`。
- **R3**(QA 面):`test_f3_realSession_blockedByOQ108` 注释承诺了未执行的目录检查 → 注释改为与代码一致。
- **R2**(QA 面):README 缺 Story 004 条目(交叉引用断裂)→ 补章节(AC→测映射 5 条)。

**残余 NICE(登记不修)**:
- `InstanceId` 赋值未断言(可加断言或删字段)。
- 缺少 `Cause` 非法值测试(可选新增)。
- 缺少 `choiceNum <= skipNum` 子集不变量断言。
- 字符串字面量 vs 枚举(当前阶段可接受)。
- `Compute` 无 null 守卫(可加 `ArgumentNullException`)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/f3_skip_rate_test.cs`(**5 测:4 passed + 1 skipped**);全量 EditMode **1228 passed + 1 inconclusive + 6 skipped + 0 failed**(`unity/Logs/s004-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/R1/R3/R2 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(整数 (num, den) 对)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。
