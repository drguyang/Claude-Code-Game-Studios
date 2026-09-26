# Story 005: schema hash 失配优雅清空与跨版本备份

> **Epic**: 输入与设备
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3h
> **Last Updated**: 2026-09-26
> **Manifest Version**: 2026-09-21

## Context

**GDD**: `design/gdd/input-system.md`(规则五 · Edge Cases 一 / 二)
**Requirement**: TR-input-004(资产重建致 GUID 失效 → schema hash 失配 → 优雅清空,**不静默采用错位 overrides**)—— 本故事承载其**失配后果半边**(Story 004 承稳定性半边)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构
**ADR Decision Summary**: ADR-011 §一 / Amendment A ③ —— schema hash 必须含 `bindingId`,失配 = 「资产被重建/跨版本升级」的可检测信号;规则五四步防御:`Disable()` → `RemoveAllBindingOverrides()` → 头部存 slot-signature hash → **失配则改名备份 + 载入默认 + 日志**,不静默、不崩溃、不弹窗(P0 无改键 UI,弹窗无处去)。失效是**响的**,不是消失的。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 夹具法依赖内存构造 `InputActionAsset` / 改 `bindingId`(Input System 运行期 API,ADR-011 整体 Knowledge Risk HIGH 的一部分);MEDIUM —— 主路径(读文件、改名、写日志)是稳定 BCL,仅夹具构造涉及未实测的引擎行为,且失败是「响的」。

**Control Manifest Rules (this layer)**:
- Required: 失配路径 = 改名备份 + 载入默认 + 日志(manifest Core · 输入(ADR-011);GDD 规则五 step 4)
- Forbidden: 失配时静默沿用旧 overrides / 抛未捕获异常终止启动 / 弹模态窗(GDD 规则五明禁三态)
- Guardrail: 备份文件名须可诊断(含时间戳或版本后缀,不覆盖前次备份 —— GDD Edge Cases 二)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [x] **AC-3-A3(BLOCKING)**: hash 失配 ⇒ **改名备份 + 载入默认 + 日志**;**不静默、不崩溃、不弹窗**;须覆盖「重建后失配」场景。夹具法 = 独立测试资产副本(内存构造 `InputActionAsset`,改 `bindingId` 或前缀替换合成 id)⇒ 断言:**备份已生成** ∧ **生效绑定 = 默认** ∧ **未抛异常**
- [x] **AC-3-A8(BLOCKING)**: **跨版本升级失配即备份** —— 资产重建(`bindingId` 全变)⇒ hash 变 ⇒ 陈旧文件**改名备份** + **日志可诊断** + **载入默认**;overrides 必然失效,本 AC 保「**失效是响的**」(备份 + 日志在,玩家/开发者能定位,而非静默清空无痕)
- [x] **AC-3-E3③(BLOCKING)**: **`bindingId` 变化 ⇒ hash 必变** —— 构造两条 R,唯一差异 = `bindingId`,断言 hash **不同**(这是失配检测的**使能性质**:若 hash 对 `bindingId` 盲,重建永远不会被检测到,A3/A8 无从触发)

---

## Implementation Notes

*Derived from ADR-011 §一 / Amendment A ③ + GDD 规则五 / Edge Cases 一·二:*

- **四步防御**(规则五,顺序不可换):① `asset.Disable()` ② `asset.RemoveAllBindingOverrides()`(Disable 不清 override —— 单独① = 旧 override 静默叠加)③ 头部存 slot-signature schema hash ④ **失配** ⇒ 改名备份 + 载入默认 + 日志
- **三态禁令**:**不静默**(静默清空 = 玩家丢改键无痕)· **不崩溃**(失配是正常演进,非异常)· **不弹窗**(P0 无改键 UI,模态无处去 —— 且违 ADR-013 模态闭集纪律)
- **两条失败模式分开测**:① **改 hash 值**(合成失配)→ A3 断言;② **`bindingId` 全变**(真实重建)→ A8 断言。E3③ 证明 ② 必然触发 ① 的检测路径
- **备份命名**:改名而非删除(GDD Edge Cases 二:连续多次失配不得互相覆盖 ⇒ 备份名带时间戳/序号)
- **日志可诊断**:日志行须含旧 hash / 新 hash / 备份文件路径三要素(A8「可诊断」的最低口径)
- 本故事只做**失配分支**;「匹配则喂字节」的正向半边在 Story 003

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:两文件布局、正向装载(匹配分支)、字节往返(E3⑥)
- Story 004:hash 的计算与稳定性(①②④⑤⑦)—— 本故事只**消费** hash 比对结果
- 改键 UI(`OQ-3-1`)与「失配后引导玩家重新改键」的交互 —— P0 无 UI,载入默认即终点

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-A3**: 合成失配 ⇒ 备份已生成 + 生效绑定 = 默认 + 未抛异常。
  - Given: 合法 sidecar(overrides + 头部);独立测试资产副本(内存构造,改 `bindingId` 或前缀替换合成 id)使 hash 与头部不一致。
  - When: 走规则五四步防御装载。
  - Then: ① 旧文件**已改名备份**(存在,非删除);② 生效绑定全部回默认(override 未被采用);③ 无异常抛出;④ 有日志行。
  - Edge cases: 连续两次失配 ⇒ 两份备份并存(不覆盖);备份目录不可写 ⇒ 记日志跳过备份但**仍**载入默认、仍不崩(GDD Edge Cases 二)。
  - Negative fixture: 把失配分支改成「直接抛异常」⇒ ③ 红;改成「静默删文件」⇒ ① 红。
- **AC-3-A8**: 跨版本升级(`bindingId` 全变)⇒ 备份 + 可诊断日志 + 载入默认。
  - Given: 旧版资产 + 其 overrides 文件(有效);新版资产重建后 `bindingId` 全部不同。
  - When: 新版启动装载。
  - Then: 头部 hash 与文件失配 ⇒ 备份生成;日志含旧 hash / 新 hash / 备份路径;生效绑定 = 新版默认;**失效是响的**(三要素齐,非无痕清空)。
  - Edge cases: 旧文件根本不存在(首次启动)⇒ 走正常首载,不触发备份分支;文件损坏(hash 读不出)⇒ 同样走失配分支(视为失配)。
  - Negative fixture: 实现只清空不改名备份 ⇒ ①红;日志不含 hash ⇒ ④「可诊断」红。
- **AC-3-E3③**: `bindingId` 变化 ⇒ hash 必变。
  - Given: 两条 R,唯一差异 = `bindingId`。
  - When: 分别计算 `SchemaHash`。
  - Then: 两 hash **不同**。
  - Edge cases: 只改一个 binding 的 id(非全变)⇒ 亦不同;`bindingId` 相同但顺序变 ⇒ hash 相同(排序吸收)。
  - Negative fixture: 实现把 `bindingId` 排除出 R(回到 Amendment A ③ 之前的盲态)⇒ 本测红(负例自证)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input_system/hash_mismatch_recovery_test.cs` — must exist and pass

**Status**: [x] Created — `unity/Assets/Tests/EditMode/InputSystem/hash_mismatch_recovery_test.cs`(**17 测**)+ 协调点 `overrides_sidecar_test.cs`(写面计数 2→4 + hashMismatch/headerMissingSchemaHash/halfSidecar/headerOnly 备份断言)
- 验证:EditMode **585/585 全绿 exit 0**(log `unity/Logs/build-story005-reviewfix.log`,XML 已移 /tmp);本故事 17/17(含 2 失败修复轮:正则 `备份路径` 前空格 + `Distinct` 去重断言)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/hash_mismatch_recovery_test.cs`,文档路径 `tests/integration/input_system/` 为登记口径

---

## Completion Notes
**Completed**: 2026-09-26
**Criteria**: 3/3 passing(AC-3-A3 / AC-3-A8 / AC-3-E3③,零 UNTESTED)
**Deviations**: None
**Test Evidence**: EditMode 真身 `unity/Assets/Tests/EditMode/InputSystem/hash_mismatch_recovery_test.cs`(17 测)+ 协调点 `overrides_sidecar_test.cs`;合并树 **585/585 全绿 exit 0**(log `unity/Logs/build-story005-reviewfix.log`)
**Code Review**: Complete —— 会话内 /code-review 双代理并行:unity-specialist **APPROVED WITH SUGGESTIONS**(S1/S2)+ qa-tester **TESTABLE**(GAPS #1–4,无 BLOCKING);**四项 GAPS + S1/S2 全修**(含次要),复跑全绿后收口
**Review Fix Batch**:
- GAPS #1(A8 端到端):测试改真实重建 hash 链 —— `Save(ComputeSchemaHash(_shared))` → 构造重建资产 → **断言重建 hash ≠ 旧 hash** → 喂真实重建 hash 给 `Load`;不再把字面 `HashB` 直喂
- GAPS #2(scope 日志真实 bug):`BackupSidecar` 的备份范围改由**实际移动结果**派生(载荷成头部败 ⇒ 只点名「overrides」侧,不再称「overrides 与头部」)+ 回归测 `test_hashMismatch_partialBackup_scopeLogNamesOnlyMovedSide`
- GAPS #3(序号耗尽静默):两个 `TryMove*ToBackup` 循环耗尽(999 全占用)补 `载荷备份跳过` / `头部备份跳过` Warning 日志 + 测 `test_hashMismatch_backupSerialExhaust_skipsWithLogDefaultsNoCrash`
- GAPS #4(读失败出口 / 孤文件备份):目录占用法直测 `headerWriteFailure_payloadAlone` 孤载荷下一载备份(`_backedUp` 变体);三个 half-sidecar 测试补 备份路径/恢复完成 Expect + `.bak-001` 断言;两条读失败 catch 出口**无直测**为文档化偏差 —— ① 同 `BackupSidecar` 代码已被 headerMissingSchemaHash 与主失配形状双重覆盖;② 触发需平台私有故障注入,`SetUnixFileMode`(.NET 7+)不在 Unity 6.3 netstandard2.1 API 面,目录占用法永远先走 half-sidecar 分支、到不了 catch;③ catch 契约 = Story 003 既有 fail-safe
- S1(=GAPS #3 已修)· S2(half-sidecar `备份路径` Expect = 协调点扩展已修)
**Traceability**: AC-3-A3 → `test_hashMismatch_syntheticMismatch_backupCreatedDefaultsLoaded_logged` + `_doubleMismatch_twoBackupsCoexist_noOverwrite` + `_backupDirUnwritable_skipsBackupStillDefaults_noCrash` + `_neg_clearWithoutRename_stillBackedUp` · AC-3-A8 → `test_hashMismatch_crossVersionRebuild_allBindingIdsChanged_backupDiagnosticLogDefault` + `_neg_logContainsAllThreeDiagnosticElements` + `_corruptHeaderReadsAsMismatch_backupCreated` + `_firstLaunch_noFiles_noBackupCreated` · AC-3-E3③ → `test_schemaHash_bindingIdDiffers_hashDiffers` + `_singleBindingIdChange_hashDiffers`(E3③ 第一/第二子句)+ `_sameBindingIds_orderChanged_hashSame`(排序吸收边界)+ `_pinnedGoldenFixtures_guardCanonStability`(钉值回归) —— **0/3 UNTESTED**
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
**ADVISORY(未结,非本故事缺陷)**:① GDD 规则五「Load → Enable」的 Enable 归调用方(Boot 装配流),本故事只交付契约(Story 003 同口径)② P0 无改键 UI ⇒ 失配后「引导重新改键」交互不存在,载入默认即终点(`OQ-3-1`)

---

## Dependencies

- Depends on: Story 004(hash 与其两条不变量就位,失配才可被检测)
- Unlocks: None(本故事是 overrides 持久化线的收口)
