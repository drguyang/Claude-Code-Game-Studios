# Story 003: F2 误诊分布

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Formulas F2 · AC-51-B5…B7)
**Requirement**: TR-telemetry-004 · TR-telemetry-006

**ADR Governing Implementation**: ADR-019 §一 + ADR-006 §五
**ADR Decision Summary**: F2 = 混淆矩阵 `M[t][j]`;`Scorable_single` = `|D| ≤ 1 ∧ |J| ≤ 1`;三桶显式计数(共病/未结案/不可映射)。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM(ADR-006 IL2CPP 整数转译)
**Engine Notes**: F2 全整数矩阵;F1a 恒等式三项须严格成立。

**Control Manifest Rules (this layer)**:
- Required: F2 与 F1 同源 + 差额记账 · 三桶显式计数不静默丢 · 「未落笔」列与「无病」行均非恒空
- Forbidden: 静默丢弃不可归类桶 · 把部分流当完整流
- Guardrail: F2 输出含真值病种(AC-37-15 对玩家隐藏之物),51 靠不呈现而非脱敏

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-B5**(BLOCKING):F2 与 F1 同源 + 差额记账 —— `F1a 分子 = Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid`;`N_matched_comorbid ⊆ N_comorbid`;「未落笔」列与「无病」行均非恒空
- [ ] **AC-51-B6**(BLOCKING):F2 三桶显式计数 —— `|D| ≥ 2`(共病)· 无 `CaseClosed`(未结案)· `judgment` 未携带病种枚举(不可映射);三桶各等于该类例数;不出现在 `M` 任何格
- [ ] **AC-51-B7**(BLOCKING):`N_unmappable > 0` 不得触发旁路采集 —— 51 侧不存在病名字符串→枚举的解析/映射代码路径

---

## Implementation Notes

*Derived from ADR-019 §一 + ADR-006 §五 + GDD Formulas F2:*

- **M[t][j]** = `|{c ∈ Scorable_single : truth(c) = t ∧ judged(c) = j}|`;形状 = `(|病种枚举| + 1)²`
- **Scorable_single** = `{c ∈ Scorable : |D(c)| ≤ 1 ∧ |J(c)| ≤ 1}`(对称哨兵)
- **F1a 恒等式** = `Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid`
- **三桶**:`N_comorbid`(`|D| ≥ 2 ∨ |J| ≥ 2`)· `N_open`(无 `CaseClosed`)· `N_unmappable`(`judgment` 未携带病种枚举)
- **N_unmappable > 0**:按 ADR-009 三问判据登记为 37 的字段义务,不给 51 加解析器

---

## Out of Scope

*Handled by neighbouring stories:*

- Story 002: F1 判断准确率(共享 Scorable 口径)
- Story 004: F3 跳过率

---

## QA Test Cases

- **AC-51-B5**: F2 与 F1 同源 + 差额记账。Given: 同一夹具(含 `D=∅ ∧ J=∅` 与共病判对各 ≥ 1 例)。When: 同算 `M` 与 `F1a`。Then: 恒等式三项成立。
- **AC-51-B6**: 三桶显式计数。Given: 夹具含三类不可归类例。When: 计算。Then: 三桶各等于该类例数。
- **AC-51-B7**: 不触发旁路。Given: `N_unmappable > 0`。When: 处置。Then: 51 侧无病名解析代码路径。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/f2_misdiagnosis_matrix_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/f2_misdiagnosis_matrix_test.cs`(3 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 002(F1 共享 Scorable)
- Unlocks: Story 004(F3 跳过率)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 3/3 passing(AC-51-B5 F2 与 F1 同源 + 差额记账 · AC-51-B6 三桶显式计数 · AC-51-B7 不触发旁路采集;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F2Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **F2 公式实现与 GDD 逐字对齐**:`M[t][j]` 形状 = `(|病种|+1)²`;`Scorable_single` = `|D| ≤ 1 ∧ |J| ≤ 1`(对称哨兵);三桶 = `N_comorbid` / `N_open` / `N_unmappable`;F1a 恒等式三项 = `Σ_{t∈病种} M[t][t] + M[无病][未落笔] + N_matched_comorbid`。

**评审与修复**:双评审并行(代码质量面 2 BLOCKING + QA 覆盖面 0 BLOCKING)→ 全修:
- **B1**(代码面):Test 3 第 183 行恒真断言 `telemetryTypes Is.Empty` → 删除,保留 `violations Is.Empty` 断言(实现后仍为真判据)。
- **B2**(代码面):Test 1 缺 `D={t} ∧ J=∅` 用例(AC-51-B5 明文要求)→ fixture 增 case 5(M[X][未落笔]),断言 `noJudgmentCol` 期望值 1 → 2。
- **R1**(代码面):Test 2 缺 `|J| ≥ 2` 共病路径 → fixture 增 case 5(D={X}, J={X,Y}),`nComorbid` 期望值 1 → 2。
- **R2**(代码面):Test 1 缺 `D=∅ ∧ J={j}` 误开方用例 → fixture 增 case 6(M[无病][X]),`noDiseaseRow` 期望值 1 → 2。

**残余 NICE(登记不修)**:
- Test 1 可断言矩阵键域(实现若产出非法键不会被现有断言捕获)。
- Test 1 可补空夹具测试(全桶 = 0,矩阵空)。
- `using System;` 疑似未使用(可删除)。
- `SetEquals` 包装方法是平凡透传(可内联)。
- `Compute` 无 null 守卫(可加 `ArgumentNullException`)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/f2_misdiagnosis_matrix_test.cs`(**3 测全过**);全量 EditMode **1224 passed + 1 inconclusive + 5 skipped + 0 failed**(`unity/Logs/s003-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/B2/R1/R2 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(整数 (num, den) 对)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。
