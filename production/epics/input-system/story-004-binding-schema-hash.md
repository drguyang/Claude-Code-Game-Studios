# Story 004: 绑重 schema hash 稳定性(F-3.5)

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/gdd/input-system.md`(§Formulas F-3.5 · §Tuning Knobs 无 —— hash 非旋钮)
**Requirement**: TR-input-004(资产重建致 GUID 失效 → schema hash 失配 → 优雅清空,不静默采用错位 overrides)—— 本故事承载其**稳定性半边**(改键不变 / 重建必变)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构
**ADR Decision Summary**: ADR-011 Amendment A ③ —— schema hash **须含 `bindingId`**(及 `processors` / `interactions` / `groups`),否则对「重建 ⇒ GUID 重生成 ⇒ overrides 静默丢失」完全盲,而那正是 F3 要防的。GDD F-3.5 把口径落成可实现规格:R = 记录元组(原属性集)· 规范化序列化 · 自实现 FNV-1a-64 · 两条不变量(改键不自我毁灭 / 重建必变)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 哈希**自实现于 `ulong`**(GDD F-3.5 定死)—— **禁 `string.GetHashCode()`**(BCL 默认哈希跨进程不稳)、**SplitMix64 已撤**(与 F7 有符号溢出 spike 无关,本地域 RECOMMENDED 重 IL2CPP 风险);不触任何 post-cutoff API,纯 C#。

**Control Manifest Rules (this layer)**:
- Required: hash 输入须含 `bindingId` 与 `processors`/`interactions`/`groups`(ADR-011 Amendment A ③);UI map **包含**在 R 内(GDD F-3.5)
- Forbidden: `string.GetHashCode()` 或任何 BCL 默认哈希(AC-E3④);`effective*` 属性与 `SaveBindingOverridesAsJson` 载荷进入 R 的读点(AC-E3⑤ 硬禁)
- Guardrail: 每集合**前置元素计数**入规范序列化(AC-E3⑦ —— I1 唯一软点,漏写则 `["a","b"]` 撞 `["ab"]` 而既有用例全过)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story(`AC-3-E3` ①②④⑤⑦;③ 归 Story 005、⑥ 归 Story 003):*

- [ ] **AC-3-E3①**: 同一资产**两次导出** hash **相同**(确定性)
- [ ] **AC-3-E3②**: **写一次 override 记录** ⇒ 重算 ⇒ hash **不变**(改键不自我毁灭)
- [ ] **AC-3-E3④**: **不使用** `string.GetHashCode()` 或任何 BCL 默认哈希
- [ ] **AC-3-E3⑤**: **R 的读点零 `effective*` / 零 `SaveBindingOverridesAsJson`** —— 构造一条测试 override(改 `path`),断言重算 hash **不变**(若读点用了 `effectivePath` 或 override 载荷,**必变** ⇒ 抓出)
- [ ] **AC-3-E3⑦**: **集合元素计数前缀生效** —— 构造两个元素数相同但切分不同的集合(夹具:`groups = []` 与 `groups = [""]`),断言二者 hash **不同**

---

## Implementation Notes

*Derived from GDD F-3.5 全文(本故事的直接规格)+ ADR-011 Amendment A ③:*

- **R = 记录元组**`(mapName, actionName, bindingIndex, bindingId, name, path, isComposite, isPartOfComposite, processors[], interactions[], groups[])` —— **仅原属性**;硬禁 `effective*` 与 override 载荷作为 R 的来源(AC-E3⑤)
- **规范化序列化 = 逐字段长度前缀 + 每集合前置元素计数 + `StringComparer.Ordinal` 排序**(集合序不随枚举顺序漂移)
- **`SchemaHash = FNV-1a-64(concat sort(R) canon(r))`** —— 自实现于 **`ulong`**(FNV-1a 是 XOR+乘,全程无符号,IL2CPP 无 UB 面);禁 `string.GetHashCode()`、禁 `SplitMix64`(GDD F-3.5 定死)
- **两条不变量**(F-3.5 核心):① **override 变化 ⇒ hash 不变**(hash 只读资产结构,不读玩家改键 —— 改键不自我毁灭);② **`bindingId` 变化 ⇒ hash 必变**(重建检测的使能性质 —— 其「失配后果」归 Story 005 / AC-E3③)
- **UI map 包含在 R 内**(F-3.5 明文)—— 否则 UI 绑重重建失配对 hash 盲
- **头部写点**:`bindings.schema.txt` 的 hash 字段由本故事的 `ComputeSchemaHash` 填(写文件动作归 Story 003)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:两个 sidecar 文件的落盘与字节往返(E3⑥ / A2 / A5)
- Story 005:失配检测后的清空、备份、日志(A3/A8/E3③)—— 本故事只保证 hash 性质使失配**可被检测**
- Story 002:F-3.1 轴常量(与 hash 无关)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-E3①**: 同一资产两次导出 hash 相同。
  - Given: 同一 `.inputactions` 资产。
  - When: 两次构造 R 并计算 `SchemaHash`。
  - Then: 两 hash 逐位相等;跨进程/跨会话重算亦相等(Ordinal 排序保证集合序稳定)。
  - Edge cases: 集合枚举顺序在两次构造间被打乱 ⇒ 仍相等(排序吸收);空 processors/interactions/groups。
  - Negative fixture: 无。
- **AC-3-E3②**: 写一次 override ⇒ hash 不变。
  - Given: 合法资产 + 一次 `path` override 写入。
  - When: 写 override 前后各算一次 hash。
  - Then: 两次相等(override 不入 R)。
  - Edge cases: 复合绑重 parts 被 override;override 后再删 override(回默认)⇒ 仍等于基线。
  - Negative fixture: 无。
- **AC-3-E3④**: 不用 `string.GetHashCode()` / BCL 默认哈希。
  - Given: 3 的输入程序集源码。
  - When: 静态扫描(Roslyn 或 grep 辅助)。
  - Then: `GetHashCode()` / `string.GetHashCode` / `EqualityComparer<string>.Default` 等默认哈希入口在 hash 路径零出现;hash 实现内仅自实现 FNV-1a-64。
  - Edge cases: 调用他处 `GetHashCode` 再传入 hash 聚合 ⇒ 同样拒绝(扫描按调用链可达性收紧)。
  - Negative fixture: 夹具源码 `return s.GetHashCode()` ⇒ 红。
- **AC-3-E3⑤**: R 读点零 `effective*` / 零 override 载荷。
  - Given: 含一条 `path` override 的资产。
  - When: 重算 hash。
  - Then: 与无 override 基线相等 —— 若读点用了 `effectivePath`,override 改变 `path` 生效值 ⇒ hash 必变 ⇒ 本测红(负例自证)。
  - Edge cases: `effectivePath` vs `path` 在无 override 时相等(正例无法区分)⇒ 必须用**有 override** 的夹具,测试体内明写。
  - Negative fixture: 故意把读点改为 `effectivePath` 的变体 ⇒ 红(探针)。
- **AC-3-E3⑦**: 集合计数前缀 —— `groups = []` vs `groups = [""]` hash 不同。
  - Given: 两条 R,唯一差异 = `groups` 空集 vs 单空串元素。
  - When: 分别规范化并求 hash。
  - Then: 两 hash **不同**(计数前缀 0 vs 1 参与);`["a","b"]` vs `["ab"]` 亦不同(逐元素长度前缀)。
  - Edge cases: 多集合(processors 同时参与);计数前缀漏写的实现变体 ⇒ 本测红(负例自证 I1 软点)。
  - Negative fixture: 实现去掉计数前缀的探针变体 ⇒ 红。

**实现期登记(2026-09-26 实现批;不改 spec,只把实际实现/用例面与原文的出入落账)**:
- **E3⑦ 计数前缀负例探针在 AC 夹具下不可构造(GDD/QA 口径出入 —— 牙齿改锚)**:GDD F-3.5 的规范序列化对集合元素用**逐元素 4 字节定长长度前缀**(`len ‖ utf8`)。在此编码下,QA Negative「实现去掉计数前缀 ⇒ 本测红」用 AC 给定的两对夹具**红不了**:`[]` 与 `[""]` 的字节流本就不同(前者计数 0 后无元素,后者计数 1 + `len=0` 的空元素头)—— GDD:652「单元素空串 = len=0 故撞」的论证在**定长**前缀下不成立(空元素仍发出 4 个零字节);`["a","b"]` vs `["ab"]` 同理由逐元素前缀区分。⇒ AC 两夹具**逐字断言保留**(test 7 前两支),计数前缀的**牙齿改锚**到相邻集合平移夹具:test 7 第三支 `processors=["clamp"],interactions=[]` vs `processors=[],interactions=["clamp"]` —— 无计数时两字段的字节流**逐位相同**(同为 `len=4 ‖ "clamp"`),计数参与则必须不同 ⇒ 去计数该断言红。元素长度前缀的牙齿另立 test 8:`("ab","c")` vs `("a","bc")`(计数同为 2,仅逐元素前缀可区分)。
- **测试函数 → AC 映射(8 测)**:E3① 2 测(`..._sameAsset_twiceCompute_identical` 含排序吸收 Edge + `..._buildRecords_coverAllBindingsWithIds` Manifest Required 实证)· E3② 1 测(`..._overridesWritten_recomputeUnchanged`,含复合 part 与删回 Edge)· E3④ 1 测(`..._sourceScan_noBclDefaultHash`)· E3⑤ 2 测(`..._withOverride_recomputeUnchanged_effectiveProbeDiffers` 运行时 + `..._schemaHashSource_hasNoEffectiveOrPayloadReads` 静态)· E3⑦ 2 测(`..._countPrefix_distinguishesEmptyAdjacentShift` + `..._elementLengthPrefix_sameCountSplit_differs`)。
- **静态扫描面**:E3④ 扫 `Gameplay.Input` **全目录**(该目录 grep 零 `GetHashCode`/`EqualityComparer<` 命中,含注释);E3⑤ 静态半边只扫 `SchemaHash.cs` 单文件 —— `BindingsStore.cs` 持有 `SaveBindingOverridesAsJson` 是 Story 003 的合法写点,不入本扫描面;`effective*` 判别探针**刻意住测试文件**(E3⑤ 扫描范围不含测试侧,防自红)。
- **全套测试基线核算(543 的来历)**:story-003 收批登记 534 + `test_legacy_input_gate_analyzer_platform_all_off`(7905b5c 分析器修复批新增常驻回归测;该测在 058dd13 树内,但 story-003 收批 run 的 XML 未收录 —— 疑与该测所防的「域重载下测试发现遗漏」同因)= **535**,+ 本故事 8 = **543** ✓(逐 fixture 集合差分核验,非估算)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input_system/binding_schema_hash_test.cs` — must exist and pass

**Status**: [x] Created + VERIFIED(2026-09-26 超算 batch)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/binding_schema_hash_test.cs`,文档路径 `tests/unit/input_system/` 为登记口径(README 落点说明同批)
- 执行 ✅ **VERIFIED 2026-09-26**:全套 EditMode **543/543 全绿 exit 0**(log `unity/Logs/build-story004-impl.log`);本故事 **8/8 Passed**(E3① 2 + E3② 1 + E3④ 1 + E3⑤ 2 + E3⑦ 2)
- 基线核算:534(story-003 登记)+ 1(`test_legacy_input_gate_analyzer_platform_all_off`,7905b5c 常驻回归,story-003 收批 XML 未收录)+ 8(本故事)= **543** ✓(逐 fixture 集合差分核验,详见上方实现期登记)

---

## Dependencies

- Depends on: Story 003(sidecar 布局与读写点就位,hash 有写入处)
- Unlocks: Story 005(失配清空消费本故事的 hash 与两条不变量)
