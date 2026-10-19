# Story 010: 越界拒绝

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-19

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
- [x] **AC-51-E3**(ADVISORY · **半边已验 · 另半登记 P1b**,见下):非主机侧拒绝 —— 非主机客户端请求 `Compute()` 时拒绝,不产出空/截断指标
  - **已验半边**:主机 / 非主机 / null 三态由真 `TelemetryReporter` 驱动,非主机三法全拒(`test_e3_nonHostAuthority_rejectsAllReports`)
  - **登记 P1b 的半边**(用户 2026-10-19 裁定「E3 半边推到 P1b」):**真联机环境**下真非主机进程被拒。P0 无网络层实装(`IEventAuthority` 零生产实装),该半边**不可构造**
  - **缺口哨兵**:`test_e3_realOnline_nonHostRejected_gapSentinel` 硬断言「`IEventAuthority` 在 `unity/Assets/` 下零生产实装」。缺口一闭合(45 联机轮落了实装)⇒ 该条**转红** ⇒ 须改为真驱动,不得继续以「环境不具备」留绿
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

**Status**: [x] 已扩扫描面(2026-10-19)—— 真身 `unity/Assets/Tests/EditMode/Telemetry/out_of_bounds_rejection_test.cs`
(**12 测:12 passed / 0 failed / 0 skipped** —— 第十六片把 E3 的 `Assert.Ignore` 换成缺口哨兵,不再有跳过件;
零跳过 = 没有第五形态的藏身处);账本互链 `tests/integration/telemetry/README.md`

| AC | 载体 | 结果 |
|---|---|---|
| AC-51-E1 | `test_e1_noPlayerUi_noMenuEntries` · `test_e1_noPlayerUi_noPresentationAssets` | ✅ 2 测 |
| AC-51-E2 | `test_e2_noWriteBack_noFileWriteApis` · `test_e2_noWriteBack_noProposedFields` · `test_e2_noWriteBack_dataDirectoryUnchanged` | ✅ 3 测 |
| AC-51-E3 | `test_e3_hostAuthority_producesReports` · `test_e3_nonHostAuthority_rejectsAllReports` · `test_e3_nullAuthority_isSinglePlayer_noGate` | ✅ 半边(3 测,真 `TelemetryReporter` 三态驱动) |
| AC-51-E3(真联机半边) | `test_e3_realOnline_nonHostRejected_gapSentinel` | ⚠️ **P1b 缺口哨兵(1 测绿)** —— 判据 = 零生产实装,闭环即红 |
| AC-51-E2(字节不变) | `test_e2_noWriteBack_dataDirectoryUnchanged` | ✅ 真 SHA-256 前后指纹(第十六片替掉原同义反复断言) |
| AC-51-E4 | `test_e4_noCausation_noCausalFields` | ✅ 1 测 |
| AC-51-E5 | `test_e5_noSwitch_noSwitchFields` · `test_e5_noSwitch_noSwitchFieldsInNamespace` | ✅ 2 测 |

**证伪轮**(`tools/falsify/specs/td-epic-01-s010-scan-surface.json`,**4 变异 4/4 恰如预期**):
在 `TelemetryReporter` 上分别注入 `_telemetryEnabled` / `_attributionCount` / `_suggestedValue`
⇒ E5 / E4 / E2 三条字段扫描**分别转红**(这三条在**扩面之前全部逃逸**);
第四变异在生产树放一个 `IEventAuthority` 实装 ⇒ E3 缺口哨兵**转红**(证明它不是恒绿空壳)。

## Dependencies
- Depends on: Story 009(空白与退化)
- Unlocks: EPIC 51 DoD(36 AC 全验的最后一块)

## Completion Notes
> ⚠️ **本节 2026-09-28 的原记录以下方「2026-10-19 收口」为准,原数字不删留作历史** ——
> 原「9 测:3 passed + 6 skipped」与「全量 1309」已被第十六片(扫描面扩面 + 零跳过)取代。

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

---

## 2026-10-19 · TD-epic-01 第十六片:扫描面失效(系统性假绿)

### 原判定为什么不能维持

本 story 的 E1/E2/E4/E5 四条判据全是**反射扫描**,而扫描集只有
`DaYiJingCheng.Telemetry` 命名空间 —— 但**真正的报告生产者**(`TelemetryReporter` /
`TelemetryDeveloperView`)住在 `Gameplay.Boot`。也就是说:**四条判据对出数路径视而不见**,
而它们此前全绿。同一个坑在 story-008 的 C4 里也存在。

更糟的是原 `Assert.Ignore` 守卫:`if (telemetryTypes.Count == 0) Assert.Ignore(...)` ——
它把「扫不到装配」这个**判据失效信号**当成了「跳过」。这与
`readonly_boundary_test.cs:412` 记录的「`GetReferencedAssemblies()` 可能空洞为真」同型。

### 落地

1. **扫描面扩到生产者**:`GetTelemetryTypes()` 现固定并入
   `Gameplay.Boot.TelemetryReporter` 与 `Gameplay.Boot.TelemetryDeveloperView` 两个 `FullName`,
   且**每个都必须命中**,否则直接红(改名 / 移出时夹具立刻报警)。
2. **`Assert.Ignore` 全部改为硬红**:新增 `RequireNonEmptyScan(types)`,空扫描集 ⇒ 断言失败并说明
   「不得以跳过冒充通过」。5 处 `Assert.Ignore` 守卫全替换。
3. **E2 的字节不变性测试原来是恒真的**:旧版只断言「目录可读」,不比对内容。现改为
   运行 51 前后对 `assets/data/**` 做 **SHA-256 逐文件指纹**比对(手写 hex 循环 ——
   `Convert.ToHexString` 是 .NET 5+ API,本项目 netstandard2.1 用不了)。

### 口径登记(禁借绿)

- **AC-51-E3 只有半边**:主机 / 非主机 / null 三态已由真 `TelemetryReporter` + 真
  `IEventAuthority` 覆盖(3 测绿);**真实联机环境下的非主机拒绝 = 推到 P1b**
  (`test_e3_realOnline_nonHostRejected_gapSentinel`,1 测绿 —— 判据是「`IEventAuthority` 在
  `unity/Assets/` 下零生产实装」,缺口闭合即转红)。
- **Story 转 `Complete`(2026-10-19 用户裁定)**:E3 的真实联机半边由上述缺口哨兵钉住,
  义务写入 AC 文本,不随 story 关闭而消失;该哨兵转红之日即须改为真驱动。
- **全量回归**:`3921 / 3882 / 1 / 38`(`unity/Logs/s19p-full.xml`);唯一红为既有 `TD-player-01` 间歇。

---

## 2026-10-19 · 收口(用户裁定「E3 半边推到 P1b」)

| 项 | 2026-09-28 原记录 | 收口时点 |
|---|---|---|
| 测试数 | 9 测:3 passed + 6 skipped | **12 测:12 passed / 0 failed / 0 skipped** |
| E3 | `Assert.Ignore` 跳过 | 半边 3 测绿(真 `TelemetryReporter` 三态)+ **P1b 缺口哨兵 1 测绿** |
| E4 | `Assert.Ignore` 跳过 | `test_e4_noCausation_noCausalFields` 1 测绿 |
| E2 字节不变 | 只断言「目录可读」(恒真) | 真 SHA-256 前后逐文件指纹比对 |
| 证伪 | 无 | `td-epic-01-s010-scan-surface.json` **4 变异 4/4 恰如预期** |
| Status | `In Review` | **`Complete`** |

**为什么可以转 `Complete`**:五条 AC 全部有自动化载体且全绿;E3 的「真实联机非主机被拒」
这半边在本机**结构性不可构造**(`IEventAuthority` 全仓零生产实装),用户 2026-10-19 裁定
推到 P1b 联机轮。不是丢弃 —— 义务已写进 AC 文本,并由缺口哨兵钉住(见上)。

