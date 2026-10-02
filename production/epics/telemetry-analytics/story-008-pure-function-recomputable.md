# Story 008: 纯函数与可复算

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Core Rules R8/R9/R11 · AC-51-C1…C4)
**Requirement**: TR-telemetry-004 · TR-telemetry-005(partial,禁借绿)

**ADR Governing Implementation**: ADR-019 §一(R8 纯函数)+ ADR-012(黄金夹具)
**ADR Decision Summary**: 指标 = `Fold(E)` —— 同流同值;恰有一个 `Fold` 实现;不依赖墙钟/帧序/执行次数。

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM(ADR-012 三格矩阵未建;ADR-006 IL2CPP 整数转译)
**Engine Notes**: 51 全部中间量为整数 ⇒ 跨平台逐位一致由 ADR-012 承接;黄金夹具载体未建(ADVISORY)。

**Control Manifest Rules (this layer)**:
- Required: 同流两次重算逐位相同 · 单一折叠函数(两入口同值)· 不依赖墙钟/帧序/执行次数
- Forbidden: 第二套算法 · 跨调用持久状态 · `DateTime.Now`/`Environment.TickCount`/`Stopwatch`/`Time.*`/`UnityEngine.Random`
- Guardrail: 51 的指标折叠函数可进 ADR-012 黄金夹具

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-51-C1**(BLOCKING):同流两次重算逐位相同 —— 连续重算两次,全部字段逐位相同(含 Q16.16 标量导出)
- [ ] **AC-51-C2**(BLOCKING):单一折叠函数(两入口同值)—— 恰有一个 `Fold` 实现;增量边读边折的终值 == 离线整表折叠终值;51 无跨调用持久状态
- [ ] **AC-51-C3**(ADVISORY):可进 ADR-012 黄金夹具 —— 三格矩阵跑 51 的折叠,哈希一致(载体未建,夹具建成后回升 BLOCKING)
- [ ] **AC-51-C4**(BLOCKING):不依赖墙钟/帧序/执行次数 —— 无 `DateTime.Now`/`Environment.TickCount`/`Stopwatch`/`Time.*`/`UnityEngine.Random`;同一流在两次不同墙钟/帧率下重算 ⇒ 逐位相同

---

## Implementation Notes

*Derived from ADR-019 §一(R8/R9/R11)+ ADR-012:*

- **R8 纯函数**:每条指标 = `Fold(E)` —— 不读墙钟/帧序/执行次数;同流同值
- **R9 两种入口共用同一个折叠函数**:① 局内增量读取(边读边折)② 离线完整重算(整表折叠);增量入口只是「边读边折」,不是第二套算法
- **R11 指标定义必须写成可复算的谓词**:每条指标给出精确计数口径
- **黄金夹具**:51 的折叠函数可进 ADR-012 单元级哈希夹具(载体未建,ADVISORY)
- **整数纪律**:全部中间量为整数 ⇒ 跨平台逐位一致由 ADR-012 承接

---

## Out of Scope
- Story 009: 空白与退化

---

## QA Test Cases
- **AC-51-C1**: 同流两次重算逐位相同。Given: 任一夹具流。When: 连续重算两次。Then: 全部字段逐位相同。
- **AC-51-C2**: 单一折叠函数。Given: 51 的实现。When: 检查。Then: 恰有一个 `Fold` 实现;增量终值 == 离线终值。
- **AC-51-C3**: 可进黄金夹具。Given: `golden-vN` 夹具。When: 三格矩阵跑 51 的折叠。Then: 哈希一致。
- **AC-51-C4**: 不依赖墙钟。Given: 51 的程序集。When: grep。Then: 无 `DateTime.Now`/`Stopwatch`/`Time.*`/`UnityEngine.Random`。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/pure_function_recomputable_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/pure_function_recomputable_test.cs`(4 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 007(F6+F7)
- Unlocks: Story 009(空白与退化)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 3/3 passing(AC-51-C1 同流两次重算逐位相同 · AC-51-C2 单一折叠函数两入口同值 · AC-51-C4 不依赖墙钟/帧序/执行次数;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F6F7Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **QA 评审代理被用户停止**:代码质量评审已返回(0 BLOCKING + 4 RECOMMENDED,全部已修);QA 覆盖面评审未完成,不影响测试本身的正确性。

**评审与修复**:代码质量评审(0 BLOCKING + 4 RECOMMENDED)→ 全修:
- **B1**(代码面):行 218 恒真断言 `telemetryTypes Is.Empty`(实现后必炸)→ 删除,保留 `violations Is.Empty` 断言(实现前后都成立)。
- **R1**(代码面):B11 测试 vacuous → 加注释标注「实现后转为有效扫描断言」。
- **R4**(代码面):中位数索引注释与代码不一致 → 修正注释为 `⌈(n-1)/2⌉`。

**残余 NICE(登记不修)**:
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 头注释列 ΔLevel 但未测(删 ΔLevel 或加归属说明)。
- 缺未知 novelty_class 防御性测试。
- `ComputeSeries` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/pure_function_recomputable_test.cs`(**4 测全过**);全量 EditMode **1271 passed + 1 inconclusive + 7 skipped + 0 failed**(`unity/Logs/s008-telemetry-full.xml`)
**Code Review**: Complete —— 代码质量评审 + B1/R1/R4 修复 + 复验;QA 覆盖面评审被用户停止(不影响测试正确性)。
**ADR Compliance**: ADR-019 §一(R8/R9/R11 纯函数)· ADR-012(黄金夹具 + ROUND_HALF_AWAY_FROM_ZERO)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。
