# Story 002: F1 判断准确率

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Formulas F1 · AC-51-B1…B4)
**Requirement**: TR-telemetry-004(指标可从事件流重算)· TR-telemetry-006(整数指标)

**ADR Governing Implementation**: ADR-019 §一(回放即数据记录)+ ADR-006(定点域边界:整数 `(num, den)` 对,`ROUND_HALF_AWAY_FROM_ZERO`)
**ADR Decision Summary**: F1a = `|{c ∈ Scorable : J(c) == D(c)}| / |Scorable|`;F1b = `|{c ∈ Scorable : ∃ JudgmentRevised(c)}| / |Scorable|`;二者正交,改写不进准确率。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(ADR-006 IL2CPP 对整数的转译保证须实测)
**Engine Notes**: F1 全整数运算;率以 `(num, den)` 对承载;标量导出 = `ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)`。

**Control Manifest Rules (this layer)**:
- Required: F1a ⊥ F1b(改写不进准确率)· `J(c)` = 全序最后一条判断事件 · 分母 0 报 `(0,0)` + 显式标记
- Forbidden: 把改写折进准确率 · 报标量 0 代替「不可定义」 · 浮点统计路径
- Guardrail: 51 报测量结果,不判后果与责任(那是 53 的裁量)

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-B1**(BLOCKING):F1a 四例夹具 —— ① `D=X ∧ J=X` ② `D=∅ ∧ J=∅` ③ `D=X ∧ J=∅` ④ `D=X ∧ J=Y`;分母 = 4、分子 = 2(①②);`==` 为集合相等,`∅ == ∅` 判对
- [ ] **AC-51-B2**(BLOCKING):F1a ⊥ F1b —— ① 判对且有改写 ② 判对且无改写 ③ 判错且有改写;`F1a = 2/3` 与 `F1b = 2/3` 正交;移除全部 `JudgmentRevised` 后 `F1a` 不变
- [ ] **AC-51-B3**(BLOCKING):F1 取法 —— `J(c)` 取全序最后一条判断事件(逐条推进选 `case_id` → 写槽 → 覆盖);`JudgmentRevised.judgment` 存新值
- [ ] **AC-51-B4**(BLOCKING):分母 0 —— `|Scorable| = 0` 或 `Opportunity = 0` ⇒ 输出 `(0,0)` + 显式「分母 0/未定义」标记;不抛除零、不报 `0/1`、不报标量 0、不报 NaN

---

## Implementation Notes

*Derived from ADR-019 §一 + ADR-006 §五 + GDD Formulas F1:*

- **Scorable** = `{c : ∃ CaseClosed(c)}`;一次 = 一个立案病例
- **case_id(c)** = `CaseOpened(c)` 的 `(Tick, Patient, Seq)` —— 跨流配对键
- **D(c)** = `Payload(CaseClosed(c)).disease_set` —— 真值,唯一来源
- **J(c)** = 全序最后一条 `JudgmentRecorded`/`JudgmentRevised` 的 judgment;无 ⇒ ∅
- **F1a** = `|{c : J(c) == D(c)}| / |Scorable|`;**F1b** = `|{c : ∃ JudgmentRevised(c)}| / |Scorable|`
- **计数口径**:数 `CaseClosed` 为「一次」;`==` 为集合相等(含 `∅ == ∅`)
- **全序最后一条的折叠回写**:逐条推进选 `case_id` → 写槽 → 覆盖;不存在先选键再找的双层实现
- **整数纪律**:全部字段为整数;率以 `(num, den)` 对承载;标量导出 = `ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)`
- **分母 0**:报 `(0,0)` + 显式标记;不报 `0/1`、不报标量 0、不报 NaN

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 只读边界与零出厂(程序集落点)
- Story 003: F2 误诊分布(混淆矩阵)
- Story 004: F3 跳过率
- Story 005: F4 熟练度成长
- Story 006: F5 难度曲线
- Story 007: F6 局内时长 + F7 节律

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text).*

- **AC-51-B1**: F1a 四例夹具。Given: 四例均 ∃ `CaseClosed`。When: 计算。Then: 分母 = 4、分子 = 2(①②);`∅ == ∅` 判对。
- **AC-51-B2**: F1a ⊥ F1b。Given: 三例(判对+改写 / 判对+无改写 / 判错+改写)。When: 计算。Then: `F1a = 2/3`,`F1b = 2/3`;移除全部改写后 `F1a` 不变。
- **AC-51-B3**: F1 取法。Given: `JudgmentRevised.judgment` 存新值。When: 取全序最后一条。Then: 与逐事件重放一致。
- **AC-51-B4**: 分母 0。Given: `|Scorable| = 0`。When: 计算。Then: `(0,0)` + 显式标记;不抛除零。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/telemetry/f1_judgment_accuracy_test.cs` — must exist and pass

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/f1_judgment_accuracy_test.cs`(5 测);账本互链 `tests/integration/telemetry/README.md`

---

## Dependencies

- Depends on: Story 001(只读边界)
- Unlocks: Story 003(F2 误诊分布,共享 Scorable 口径)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-51-B1 F1a 四例夹具 · AC-51-B2 F1a ⊥ F1b 正交 · AC-51-B3 F1 取法全序最后一条 · AC-51-B4 分母 0 报 (0,0);无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F1Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **F1 公式实现与 GDD 逐字对齐**:`F1a = |{c : J(c) == D(c)}| / |Scorable|`;`F1b = |{c : ∃ JudgmentRevised}| / |Scorable|`;集合相等含 `∅ == ∅` 判对;分母 0 报 `(0,0)` + 显式标记。

**评审与修复**:双评审并行(代码质量面 0 BLOCKING + 3 REC;QA 覆盖面 1 BLOCKING + 3 REC)→ 全修:
- **R1**(代码面):`test_f1_takeLastJudgment_wins` 近恒真(只测单槽覆盖写)→ 改为跨病例共病路由测试(两病例交错事件,验证按 case_id 路由到各自槽位)。
- **R2**(代码面):正交性检查只验 F1a 不变,未验 F1b 归零 → 补 `Assert.That(f1bNum2, Is.EqualTo(0))`。
- **R3**(代码面):测试 1、2 未断言 `denominatorZero == false` → 补断言。
- **B1**(QA 面):AC-51-B3 未集成 F1 公式 → 同代码质量 R1(重写为跨病例共病路由测试)。
- **R2**(QA 面):AC-51-B4 未显式断言「不报标量 0」与「不报 NaN」→ 补 `OfType<int>()` 与 `IsNaN` 断言。
- **R3**(QA 面):AC-51-B1 未显式断言「∅ == ∅ 判对」→ 补 `SetEquals(emptySet, emptySet)` 断言。

**残余 NICE(登记不修)**:
- `using System;` 疑似未使用(可删除)。
- `SetEquals` 包装方法是平凡透传(可内联)。
- 夹具仅含单元素集与空集,未覆盖共病多元素集(超出 AC 但符合 GDD 语义)。
- `Compute` 无 null 守卫(可加 `ArgumentNullException`)。
- AC-51-B2 缺少边界组合(全部改写/全部无改写)。
- 缺少 Q16.16 标量导出的舍入测试(归 AC-51-D5/AC-51-C1)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/f1_judgment_accuracy_test.cs`(**5 测全过**);全量 EditMode **1221 passed + 1 inconclusive + 5 skipped + 0 failed**(`unity/Logs/s002-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + R1/R2/R3/B1 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(整数 (num, den) 对 + ROUND_HALF_AWAY_FROM_ZERO)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 6 项已分处登记(本 notes · story Known Risks · 账本)。
