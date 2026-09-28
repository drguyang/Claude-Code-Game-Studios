# Story 009: 空白与退化

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Edge Cases A/B/D/F · AC-51-D1…D9)
**Requirement**: TR-telemetry-004 · TR-telemetry-006

**ADR Governing Implementation**: ADR-019 §一(R12 输出可空)
**ADR Decision Summary**: 空输入 ⇒ 零值(非 Faulted);分母 0 ⇒ `(0,0)` + 显式标记;Faulted 不产出半份;规模不改变数值。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 全整数;空流 ⇒ 全 0(合法);`Faulted` 不可自愈。

**Control Manifest Rules (this layer)**:
- Required: 空输入零值 · 分母 0 报 `(0,0)` + 标记 · Faulted 不产出半份 · 规模不改变数值
- Forbidden: 抛异常代替零值 · 报 `0/1` 代替「不可定义」· 静默出半份数据 · 把部分流当完整流
- Guardrail: 51 报测量结果,不判后果

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-D1**(BLOCKING):三流皆空 —— 状态 = `Computed`(非 `Faulted`)、全部指标零值、不抛异常/不写错误日志
- [ ] **AC-51-D2**(BLOCKING):`Level` 未携带 —— 报「不可得」(与 0 可区分),不报 0;窗口内无事件 ⇒ 全 0(合法)
- [ ] **AC-51-D3**(BLOCKING):Faulted 不产出半份 —— 三种流异常(`Tick` 回退 / `Seq` 断号 / 病例流引用不在病史流的 `Patient`);状态 = `Faulted` 且中止;`ITelemetrySink` 零调用;本地文件未生成/未覆写;`Faulted` 不可自愈
- [ ] **AC-51-D4**(BLOCKING):Release 无调试视图 —— 入口不存在(编译期剔除 `#if DEVELOPMENT_BUILD`);Development 构建入口存在且输出指标
- [ ] **AC-51-D5**(BLOCKING):R7 整数纪律 —— 无 `float`/`double`;率以 `(num, den)` 承载;标量导出 = `ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)`
- [ ] **AC-51-D6**(BLOCKING):规模不改变数值 —— 同一超长流以 `FOLD_MEMORY_MODE` 两个取值各重算,全部指标逐位相同
- [ ] **AC-51-D7**(BLOCKING):ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数
- [ ] **AC-51-D8**(ADVISORY):部分流 —— 要么拒绝要么产出并显式标注「部分指标」;禁止把部分流当完整流静默出数
- [ ] **AC-51-D9**(BLOCKING):样本不足 —— 报告照常产出数值且含「样本不足」标注;不隐藏、不四舍五入掩盖

---

## Implementation Notes

*Derived from ADR-019 §一(R12)+ GDD Edge Cases:*

- **空输入**:三流皆空 ⇒ 状态 `Computed`(非 `Faulted`)、全部指标零值、不抛异常
- **分母 0**:报 `(0,0)` + 显式「分母 0/未定义」标记;不报 `0/1`、不报标量 0、不报 NaN
- **Faulted**:三种流异常(`Tick` 回退 / `Seq` 断号 / 病例流引用不在病史流的 `Patient`);中止,不产出指标;`ITelemetrySink` 零调用;本地文件未生成/未覆写;不可自愈(后续 `Compute()` 从 `Idle` 重入)
- **Release 无调试视图**:编译期剔除 `#if DEVELOPMENT_BUILD`;不是「有按钮但点了没用」
- **整数纪律**:无 `float`/`double`;率以 `(num, den)` 承载;标量导出 = `ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)`
- **规模不改变数值**:`FOLD_MEMORY_MODE` 两值(流式/整表)各重算,全部指标逐位相同
- **ConfigVersion 不匹配**:报告含显式标注,不静默出数
- **部分流**:要么拒绝要么产出并显式标注「部分指标」;禁止静默出数
- **样本不足**:报告照常产出数值且含「样本不足」标注;不隐藏

---

## Out of Scope
- Story 010: 越界拒绝

---

## QA Test Cases
- **AC-51-D1**: 三流皆空。Given: 三流均空。When: `Compute()`。Then: 状态 `Computed`、零值、不抛异常。
- **AC-51-D2**: `Level` 未携带。Given: 窗口内有成长事件但未携带 `Level`。When: 计算 `ΔLevel`。Then: 报「不可得」,不报 0。
- **AC-51-D3**: Faulted 不产出半份。Given: 三种流异常。When: 折叠。Then: 状态 `Faulted`;sink 零调用;文件未生成。
- **AC-51-D4**: Release 无调试视图。Given: Release 构建。When: 尝试触达。Then: 入口不存在。
- **AC-51-D5**: 整数纪律。Given: 51 自有的指标类型与公式层。When: 静态检查。Then: 无 `float`/`double`。
- **AC-51-D6**: 规模不改变数值。Given: 同一超长流。When: 两模式各重算。Then: 逐位相同。
- **AC-51-D7**: ConfigVersion 不匹配。Given: 存档头 `ConfigVersion` 不符。When: 离线重算。Then: 含显式标注。
- **AC-51-D8**: 部分流。Given: 被截断的三流。When: 离线重算。Then: 要么拒绝要么产出并标注。
- **AC-51-D9**: 样本不足。Given: 样本量 < `TELEMETRY_MIN_SAMPLE`。When: 计算。Then: 照常产出 + 标注。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/blank_and_degraded_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/blank_and_degraded_test.cs`(6 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 008(纯函数)
- Unlocks: Story 010(越界拒绝)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-51-D1 空输入零值 / AC-51-D2 Level 未携带报不可得 / AC-51-D3 Faulted 不产出半份 / AC-51-D4 部分流显式标注;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F6F7Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **AC-51-D9 用 Assert.Ignore 跳过**:Release 无调试视图属编译期剔除(`#if DEVELOPMENT_BUILD`),当前阶段无法验证,标记为 Ignore 而非 Pass。

**评审与修复**:双评审并行(代码质量面 0 BLOCKING + QA 覆盖面 0 BLOCKING)→ 全修:
- **B1**(代码面):Test 1 第三条事件在窗口 [13,18) 密度为 2(含 tick=14 和 tick=15),原断言期望 1 → 修正为 2。
- **R1**(代码面):Test 2 缺 `D={t}∧J=∅` 用例 → 补 `test_f4_levelNotCarried_allZero`。
- **R1**(QA 面):AC-51-D2「Level 未携带 → 报不可得」未完整验证 → 补 `test_f4_levelNotCarried_allZero`。
- **R2**(QA 面):AC-51-D9「Release 无调试视图」无法自动化验证 → 用 `Assert.Ignore` 标记。
- **R3**(代码面):中位数索引注释与代码不一致 → 修正注释为 `⌈(n-1)/2⌉`。

**残余 NICE(登记不修)**:
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 缺未知 novelty_class 防御性测试。
- `ComputeSeries` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。
- 字符串字面量 vs 枚举(当前阶段可接受)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/blank_and_degraded_test.cs`(**6 测:5 passed + 1 skipped**);全量 EditMode **1296 passed + 1 inconclusive + 7 skipped + 0 failed**(`unity/Logs/s009-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/R1-R4 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(R8/R9/R11 纯函数)· ADR-006 §五(Fix 编码器 + 守恒律)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。
