# Story 007: F6 局内时长 + F7 节律

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Formulas F6/F7 · AC-51-B15…B18)
**Requirement**: TR-telemetry-004 · TR-telemetry-006

**ADR Governing Implementation**: ADR-019 §一 + ADR-006 §五
**ADR Decision Summary**: F6 = `SessionSpan = Tick_max − Tick_min`(可审计);F7 = 整数序统计量(上中位数 + MAD + min/max + GapCount),禁方差/标准差。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM(ADR-006 IL2CPP 整数转译)
**Engine Notes**: F6/F7 全整数;F7 禁浮点统计量;上中位数取法定死(禁插值)。

**Control Manifest Rules (this layer)**:
- Required: F6 可审计(同时输出 `Tick_min`/`Tick_max`/`|E|`)· F7 上中位数取法定死 · 退化 = 不可定义 ≠ 0
- Forbidden: 方差/标准差/`Math.Sqrt`/浮点除法 · 报 0 代替「不可定义」· 插值中位数
- Guardrail: 51 报测量结果,不判后果

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-B15**(BLOCKING):F6 可审计 —— 同时输出 `Tick_min`/`Tick_max`/`|E|`;`Tick_min + SessionSpan = Tick_max`;`SessionSpan ≥ 0`;单事件 ⇒ `span = 0`(合法);空流 ⇒ 0
- [ ] **AC-51-B16**(BLOCKING):F7 上中位数取法定死(禁插值) —— 偶数项 Δ 序列 `⟨a₁<a₂<a₃<a₄⟩` ⇒ `Δ_median = a₂`(1-indexed 第 `⌈n/2⌉` 项),不是 `(a₂+a₃)/2`;结果 ∈ 输入集;奇数项同法
- [ ] **AC-51-B17**(BLOCKING):F7 退化 = 不可定义 ≠ 0 —— `n = 1`(零个 Δ)⇒ 全部五个字段报「不可定义」,不得报 0;`n = 2` ⇒ `Δ_MAD = 0` 合法;断言输出可区分「不可定义」与「真实的 0」
- [ ] **AC-51-B18**(BLOCKING):F7 禁浮点统计量 —— 无方差/标准差/`Math.Sqrt`/浮点除法的离散度实现;节律字段类型全为 `Tick`/`int`

---

## Implementation Notes

*Derived from ADR-019 §一 + ADR-006 §五 + GDD Formulas F6/F7:*

- **F6**:`SessionSpan = Tick_max − Tick_min`;同时报 `Tick_min`/`Tick_max`/`|E|`;单事件 ⇒ `span = 0`(合法);空流 ⇒ 0
- **F7**:`Δ_i = Tick(e_{i+1}) − Tick(e_i)`,`i = 1…n−1`;`Δ_median = Δ_(⌈n/2⌉)`(1-indexed,上中位数,禁插值);`Δ_MAD = median(|Δ_i − Δ_median|)`;`GapCount = |{i : Δ_i > GAP_THRESHOLD}|`
- **禁方差/标准差**(`sqrt` + 除法 ⇒ 浮点);离散度只用整数序统计量
- **上中位数**(`⌈n/2⌉`)而非插值 —— 两个实现者必须得同一个数
- **边界**:`n < 2` ⇒ 无 Δ 序列 ⇒ 全部字段报「不可定义」;`n = 1` 时 `Δ_MAD` 不是 0,是「无意义」;`n = 2` ⇒ `Δ_MAD = 0`(合法)

---

## Out of Scope
- Story 008: 纯函数与可复算

---

## QA Test Cases
- **AC-51-B15**: F6 可审计。Given: 任一夹具。When: 计算 `SessionSpan`。Then: 同时输出 `Tick_min`/`Tick_max`/`|E|`;`Tick_min + SessionSpan = Tick_max`。
- **AC-51-B16**: F7 上中位数。Given: 偶数项 Δ 序列。When: 计算 `Δ_median`。Then: = `a₂`(1-indexed 第 `⌈n/2⌉` 项),不是插值。
- **AC-51-B17**: F7 退化。Given: `n = 1`。When: 计算节律。Then: 全部五个字段报「不可定义」,不得报 0。
- **AC-51-B18**: F7 禁浮点。Given: 51 的程序集。When: grep。Then: 无方差/标准差/`Math.Sqrt`/浮点除法。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/f6_f7_session_rhythm_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/f6_f7_session_rhythm_test.cs`(10 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 006(F5)
- Unlocks: Story 008(纯函数与可复算)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-51-B15 F6 可审计 · AC-51-B16 F7 上中位数禁插值 · AC-51-B17 F7 退化=不可定义≠0 · AC-51-B18 F7 禁浮点统计量;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F6F7Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **Δ_min/Δ_max 补入谓词面**:QA 评审 R2 发现 GDD F7 公式定义了五个字段(⟨Δ_median, Δ_MAD, Δ_min, Δ_max, GapCount⟩),但测试谓词面只返回三个。补入 `deltaMin`/`deltaMax` 并更新所有调用处(4→6 元组)。
3. **GapCount 补断言**:QA 评审 R3 发现 GapCount 从未被断言。已有 `test_f7_gapCount_nonZero` 覆盖,确认后无需新增。

**评审与修复**:双评审并行(代码质量面 1 BLOCKING + QA 覆盖面 0 BLOCKING)→ 全修:
- **B1**(代码面 + QA R2/R3 同一根因):测试谓词面缺 `deltaMin`/`deltaMax`,GDD F7 公式定义五字段但测试只返回三 → 谓词面补 `deltaMin`/`deltaMax`,所有调用处 4→6 元组。
- **B2**(代码面):行 126 恒真断言 `telemetryTypes Is.Empty`(实现后必炸)→ 删除,保留 `violations Is.Empty` 断言。
- **R1**(代码面):B11 测试当前阶段 vacuous → 加注释标注「实现后转为有效扫描断言」。
- **R4**(代码面):中位数索引注释与代码不一致(`n/2-1` 等价于 `⌈(n-1)/2⌉`,但注释写"第 ⌈n/2⌉ 项")→ 修正注释为 `⌈(n-1)/2⌉`。

**残余 NICE(登记不修)**:
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 头注释列 ΔLevel 但未测(删 ΔLevel 或加归属说明)。
- 缺未知 novelty_class 防御性测试。
- `ComputeSeries` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/f6_f7_session_rhythm_test.c`(**10 测:9 passed + 1 skipped**);全量 EditMode **1271 passed + 1 inconclusive + 7 skipped + 0 failed**(`unity/Logs/s007-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/B2/R1/R4 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(整数 (num, den) 对)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。
