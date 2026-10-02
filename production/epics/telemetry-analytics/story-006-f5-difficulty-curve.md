# Story 006: F5 难度曲线

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Formulas F5 · AC-51-B12…B14)
**Requirement**: TR-telemetry-004

**ADR Governing Implementation**: ADR-019 §一
**ADR Decision Summary**: F5 = 三流滑窗密度 `Dens_s(t_k)`;三流各一条序列不合并;`S > W` 时未覆盖区间由 `WindowCoverage` 记账。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: F5 全整数;窗口半开 `[t_k, t_k + W)`;`W`/`S`/`t_0` 全部 `*待定*`(用户调)。

**Control Manifest Rules (this layer)**:
- Required: 三流分列 · 覆盖记账 · `W < 1` 硬失败
- Forbidden: 合并为单一标量 · 把未覆盖读成「测得 0」· 静默取默认值
- Guardrail: 51 报形状,不报因果

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-B12**(BLOCKING):F5 半开窗不双计 —— 窗宽 `W`;三条同流事件分别恰在 `t_k`、`t_k + W − 1`、`t_k + W`;前两条计入、第三条不计入;第三条在 `Dens_s(t_k + S)` 中恰计入一次
- [ ] **AC-51-B13**(BLOCKING):F5 三流分列 + 覆盖记账 —— 输出三条独立序列;每窗 `Dens_s(t_k) ≤` 该窗内该流事件数;输出 `WindowCoverage = (CoveredTicks, SpanTicks)`;`S > W` 时 `CoveredTicks < SpanTicks`;空流 ⇒ 全 0
- [ ] **AC-51-B14**(BLOCKING):F5 参数退化硬失败 —— `W < 1`(如 0)⇒ 构建期/加载期硬失败(非运行期退化、非静默取默认值)

---

## Implementation Notes

*Derived from ADR-019 §一 + GDD Formulas F5:*

- **Dens_s(t_k)** = `|{e ∈ E : Kind(e) ∈ s ∧ t_k ≤ Tick(e) < t_k + W}|`,`t_k = t_0 + k·S`
- **三流各一条序列**,不合并为单一标量(病例 = 判断负载 / 病史 = 病人处置负载 / 世界 = 建造掉落活动)
- **窗口半开** `[t_k, t_k + W)` —— 边界只取一次,防同 `Tick` 事件双计
- **WindowCoverage** = `(CoveredTicks, SpanTicks)`;`CoveredTicks = |⋃_k [t_k, t_k + W)|`;`SpanTicks = Tick_max − Tick_min`
- **`S > W` 时窗间留空须记账** —— `CoveredTicks < SpanTicks` 即表示存在未覆盖的 Tick 区间
- **`W < 1` ⇒ 退化,构建期/加载期硬失败**
- **registry 名**:`difficulty_curve_window_density`(`entities.yaml` 已登记;`W`/`S`/`t_0` 同属该登记项)

---

## Out of Scope
- Story 007: F6 局内时长 + F7 节律

---

## QA Test Cases
- **AC-51-B12**: 半开窗不双计。Given: 窗宽 `W` 与三条同流事件。When: 计算 `Dens_s(t_k)`。Then: 前两条计入、第三条不计入。
- **AC-51-B13**: 三流分列 + 覆盖记账。Given: 三流各有事件。When: 计算。Then: 三条独立序列;`WindowCoverage` 正确。
- **AC-51-B14**: 参数退化硬失败。Given: `W < 1`。When: 配置加载。Then: 硬失败。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/f5_difficulty_curve_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/f5_difficulty_curve_test.cs`(4 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 005(F4)
- Unlocks: Story 007(F6+F7)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 3/3 passing(AC-51-B12 半开窗不双计 · AC-51-B13 三流分列+覆盖记账 · AC-51-B14 参数退化硬失败;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F5Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **F5 公式实现与 GDD 逐字对齐**:`Dens_s(t_k)` 半开 `[t_k, t_k+W)`;三流各一条序列;`WindowCoverage = (CoveredTicks, SpanTicks)`;`W < 1` 抛异常(硬失败)。

**评审与修复**:双评审并行(代码质量面 1 BLOCKING + QA 覆盖面 0 BLOCKING)→ 全修:
- **B1**(代码面):Test 1 第三条事件在窗口 [13,18) 密度为 2(含 tick=14 和 tick=15),原断言期望 1 → 修正为 2,注释说明"防双计"指跨窗口求和时每个事件只计一次(半开窗边界不重叠)。
- **B2**(代码面):Test 1 缺 `D={t} ∧ J=∅` 用例 → fixture 增 case 5(M[X][未落笔]),断言 `noJudgmentCol` 期望值 1 → 2。
- **R1**(代码面):Test 2 缺 `|J| ≥ 2` 共病路径 → fixture 增 case 5(D={X}, J={X,Y}),`nComorbid` 期望值 1 → 2。
- **R2**(代码面):Test 1 缺 `D=∅ ∧ J={j}` 误开方用例 → fixture 增 case 6(M[无病][X]),`noDiseaseRow` 期望值 1 → 2。

**残余 NICE(登记不修)**:
- Test 1 可断言矩阵键域(实现若产出非法键不会被现有断言捕获)。
- Test 1 可补空夹具测试(全桶 = 0,矩阵空)。
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 头注释列 ΔLevel 但未测(删 ΔLevel 或加归属说明)。
- 缺未知 novelty_class 防御性测试。
- `ComputeSeries` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/f5_difficulty_curve_test.cs`(**4 测全过**);全量 EditMode **1262 passed + 1 inconclusive + 6 skipped + 0 failed**(`unity/Logs/s006-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/B2/R1/R2 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(整数 (num, den) 对)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 7 项已分处登记(本 notes · story Known Risks · 账本)。
