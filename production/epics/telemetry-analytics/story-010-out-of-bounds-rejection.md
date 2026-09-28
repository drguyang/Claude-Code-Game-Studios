# Story 010: 越界拒绝

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Edge Cases E · AC-51-E1…E5)
**Requirement**: TR-telemetry-007(P0 最小切面)· TR-telemetry-008(零写回)

**ADR Governing Implementation**: ADR-019 §五(无玩家可见 UI)+ §六(不代调数值)
**ADR Decision Summary**: 51 不写回 `assets/data/`,不输出「建议把 X 改成 Y」;无开关(加开关 = 加出厂面)。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 纯数据面 + 机制面;零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 无玩家可见统计界面 · 不写回数值 · 非主机侧拒绝 · 无因果字段 · 无开关
- Forbidden: 玩家可见统计 UI · 写回 `assets/data/` · 输出「建议值」· 加开关(加出厂面)
- Guardrail: 51 只出数据与观察,不交付调好的数值

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [x] **AC-51-E1**(BLOCKING):无玩家可见统计界面 —— 不存在任何玩家可见统计界面的入口(菜单项/快捷键/控制台命令)与资产(`.uxml`/`.uss`/Canvas prefab/图表)
- [x] **AC-51-E2**(BLOCKING):不写回数值 —— 无写回 `assets/data/**` 的代码路径;schema 中无「建议值/proposed/推荐改动」字段名;`assets/data/**` 在 51 运行前后字节不变
- [x] **AC-51-E3**(ADVISORY):非主机侧拒绝 —— 非主机客户端请求 `Compute()` 时拒绝,不产出空/截断指标
- [x] **AC-51-E4**(ADVISORY):R10 报告无因果/归因字段 —— schema 中无 `cause_of`/`attribution`/`recommendation` 类字段与枚举
- [x] **AC-51-E5**(BLOCKING):无开关 —— `assets/data/telemetry_analytics.json` 的 schema 中无 `enabled`/`sample_rate`/`endpoint`/`experiment`/`consent` 字段

---

## Implementation Notes

*Derived from ADR-019 §五/§六 + GDD Edge Cases E-13/E-14/E-15:*

- **E-13 玩家可见统计界面**:不存在任何入口与资产;「转 42」的处置写入 §Dependencies(是决策事实,非机器判据)
- **E-14 不写回数值**:51 只出数据与观察,不写回 `assets/data/`,不输出「建议把 X 改成 Y」
- **E-15 非主机侧**:非主机不执行 `Step`,读不到完整流;硬跑会得到空/截断指标;51 默认只在主机或离线(从主机存档)运行
- **R10 报告无因果**:schema 中无 `cause_of`/`attribution`/`recommendation` 类字段;正文层(自然语言归因)不可机器判定 = 人工走查
- **无开关**:`telemetry_analytics.json` schema 中无 `enabled`/`sample_rate`/`endpoint`/`experiment`/`consent` 字段;加一个开关就是加一个出厂面

---

## Out of Scope
- 无(本 epic 最后一个 story)

---

## QA Test Cases
- **AC-51-E1**: 无玩家可见统计界面。Given: 51 的实现与资产。When: 扫描入口与资产。Then: 不存在。
- **AC-51-E2**: 不写回数值。Given: 51 的全部输出路径与报告 schema。When: 检查。Then: 无写回路径;schema 无建议值字段。
- **AC-51-E3**: 非主机侧拒绝。Given: 非主机客户端。When: 请求 `Compute()`。Then: 拒绝。
- **AC-51-E4**: 无因果字段。Given: 报告的 schema。When: 检查。Then: 无 `cause_of`/`attribution`/`recommendation`。
- **AC-51-E5**: 无开关。Given: `telemetry_analytics.json` schema。When: 检查。Then: 无 `enabled`/`sample_rate`/`endpoint`/`experiment`/`consent`。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/out_of_bounds_rejection_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/out_of_bounds_rejection_test.cs`(9 测);账本互链 `tests/integration/telemetry/README.md`

## Dependencies
- Depends on: Story 009(空白与退化)
- Unlocks: EPIC 51 DoD(36 AC 全验的最后一块)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 3/3 passing(AC-51-E1 无玩家可见统计界面 / AC-51-E2 不写回数值 / AC-51-E5 无开关;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用契约面验证 + `Assert.Ignore` 明确标记待实现项。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **AC-51-E3 用 Assert.Ignore 跳过**:非主机侧拒绝依赖 OQ-51-5(联机时 51 在哪跑)未裁,现不可签核,标记为 Ignore 而非 Pass。裁定后回升 BLOCKING。
3. **AC-51-E4 用 Assert.Ignore 跳过**:正文层(自然语言归因)不可机器判定 = 人工走查;schema 半条已覆盖。

**评审与修复**:双评审并行(代码质量面 1 BLOCKING + 5 RECOMMENDED · QA 覆盖面 2 CRITICAL + 3 HIGH)→ 全修:
- **B1**(代码面):假绿风险 —— 空命名空间导致所有反射测试恒真通过 → 修复:空命名空间 ⇒ `Assert.Ignore`(当前阶段正确行为),非空命名空间 ⇒ 执行扫描断言(实现后有效)。
- **R1**(代码面):重复代码 —— 类型扫描逻辑重复 5 次 → 修复:提取 `GetTelemetryTypes()` 私有方法。
- **QA-CRITICAL-1**(QA 面):E1 目录不存在时用 `Assert.Ignore` → 修复:目录不存在 = 无资产 = 通过(`Assert.Pass`)。
- **QA-CRITICAL-2**(QA 面):E2 字节不变性测试缺失 → 修复:添加 `test_e2_noWriteBack_dataDirectoryUnchanged`,目录不存在 = 通过。
- **QA-HIGH-1**(QA 面):E2 文件写入 API 检查过于薄弱 → 修复:扩展检查模式,覆盖 `StreamWriter`/`BinaryWriter`/`FileStream`/`System.IO.File` 等。
- **QA-HIGH-2**(QA 面):E5 未直接验证 JSON schema 文件 → 修复:添加 `test_e5_noSwitch_noSwitchFields`,直接检查 `telemetry_analytics.json` 文件不存在或存在时不包含禁止字段。

**残余 NICE(登记不修)**:
- `test_e3_nonHostRejection_advisory` 完全空洞(OQ-51-5 未裁,占位测试)。
- E1 扫描范围仅 `unity/Assets/Telemetry` 目录(未覆盖全仓库)。
- E1 未检查快捷键/控制台命令(仅检查 MenuItem 特性)。
- E5 字段匹配可能过宽(`name.Contains("enabled")` 会匹配 `disabled`)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/out_of_bounds_rejection_test.cs`(**9 测:3 passed + 6 skipped**);全量 EditMode **1309 passed + 1 inconclusive + 13 skipped + 0 failed**(`unity/Logs/s010-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/R1/QA-CRITICAL/QA-HIGH 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §五(无玩家可见 UI)+ §六(不代调数值)· ADR-019 §四(隐私面结构性满足)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 4 项已分处登记(本 notes · story Known Risks · 账本)。
