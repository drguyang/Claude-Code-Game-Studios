# Story 006: 意图边界与交出物(零 SimEvent · 全整数 · 聚合上行)

> **Epic**: 输入与设备
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/gdd/input-system.md`(§Dependencies 三 · States I2)
**Requirement**: TR-input-007(直读层 float = 手感层,永不进流;判定结果全整数直接构造 SimEvent)· TR-input-008(联机 C 路:客户端聚合 `EmergencyAttempt` 全整数意图上行,主机 `Judge` + `Append` + `Seq`)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构 · ADR-006(次): 定点域边界数据契约 · ADR-009(次): 世界状态的事件化边界
**ADR Decision Summary**: ADR-011 Amendment B(C 路改判)—— 客户端聚合**一条全整数 `EmergencyAttempt`** 上行 → 主机执行 `Judge` + `Append` + 发号 `Seq`;本地判定降级为预表现(**不写流、不参与权威**);「不逐帧同步输入」保留。Amendment A —— `JudgeResult` 全整数(int/Fix),SimEvent 由 10 **直接构造**,运行期不经 `FixParse.Parse(string)`。ADR-006 —— 载荷整数域纪律;ADR-009 §七 —— 意图事件三段式(意图 → 主机当下判距 → 效果进流),输入系统只产第一段。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 本故事判据全部是**结构/类型断言**(asmdef 引用集、递归类型扫描、字段类型)—— 纯 C# 与编译期检查,不触 post-cutoff 运行期 API;MEDIUM 承三件 ADR 的整体评级,单一风险点 = 递归闭包扫描对泛型/接口类型的处理需与 `PresentationDtoGuard`(AC-37-15)同构实现并测试。

**Control Manifest Rules (this layer)**:
- Required: 载荷可达类型闭包递归扫描零 `float`/`double`(与 `PresentationDtoGuard` 同构 —— AC-A7;顶层扫描抓不到结构体字段)
- Forbidden: 输入程序集引用声明 `IEventSink` / `SimEvent` 的程序集(AC-A6,构建失败)· 运行期 `FixParse.Parse(string)` 出现在判定结果构造路径(ADR-011 Amendment A)
- Guardrail: 3 的交出物闭集 = `{InteractIntent, EmergencyIntent, FocusNavigationIntent, EmergencyReading}` —— 白名单外新增类型须过本故事的结构断言(AC-A6 原文点名)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [x] **AC-3-A6(BLOCKING)**: **零 `SimEvent`** —— 输入程序集 asmdef **引用集白名单断言**:不引用任何声明 `IEventSink` / `SimEvent` 的程序集(构建失败);3 **只产意图**(`InteractIntent` / `EmergencyIntent` / `FocusNavigationIntent`)与 `EmergencyReading`
- [x] **AC-3-A7(BLOCKING)**: **载荷可达闭包零浮点** —— 递归扫描 `SimEvent` 载荷可达类型闭包,断言无 `float`/`double`(与 `PresentationDtoGuard` 同构;**B3 只看顶层抓不到结构体字段** —— 必须递归进字段/元素类型)
- [x] **AC-3-B3(BLOCKING)**: **判定结果全整数** —— `JudgeResult` 字段全 `int`/`Fix`;SimEvent 由 10 **直接构造**,运行期**不经** `FixParse.Parse(string)`;3 只交 `EmergencyReading`(全整数:tick 时长 + edge + 枚举序号)
- [x] **AC-3-B4(BLOCKING,承 Amendment B)**: **联机 C 路** —— 客户端把意图**聚合为一条**全整数 `EmergencyAttempt` 上行 → 主机执行 `Judge` + `Append` + 发号 `Seq`;**本地判定仅预表现**(不写流、不参与权威);「不逐帧同步输入」保留

---

## Implementation Notes

*Derived from ADR-011 §二 / Amendment A / Amendment B + ADR-006 / ADR-009 §七:*

- **I2「无外部状态意图源」**(States)—— 意图类型不携带自身之外的任何系统状态;豁免面(自身设备态 + 通道态)由 D4 / E4 守非 I2。机械判据 = asmdef 白名单断言,与 AC-A6 同构
- **三段式在急救侧的落地**(ADR-009 §七,承 ADR-011 Amendment B 后与拾取同构):① 3 产**全整数意图**(`EmergencyAttempt`)② **主机**执行 `Judge`(判定输入不在客户端,伪造成本 = 改枚举)③ 效果由 10 写流。3 **永远停在第 ① 段**
- **全整数口径**(ADR-011 Amendment A):`EmergencyReading` = tick 时长(tick 计数,int)+ edge 标志 + 动作枚举序号 —— **无 `float`、无 `Fix → float` 出口**;`Fix` 若出现则是整数定点表示,合法
- **A6 与 A7 是两道不同的门**:A6 断**程序集引用**(输入侧不持有写流能力);A7 断**类型闭包**(全案任何 `SimEvent` 载荷可达处无浮点 —— 含其他系统定义、被输入意图间接引用的类型)。A7 的实现可复用/扩展 `PresentationDtoGuard` 的递归反射扫描
- **B4 联机形态**:`EmergencyAttempt` 走 ADR-001 **可靠通道**(§一之三 裁决一:判定输入类 ≠ 第二 QoS 表现态);本地 `Judge` 预表现结果**不进任何通道**

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 007:直读通道的相位与不穿 UI 栈(B1a / B2)—— 通道怎么读归 007,读出什么交出物归本故事
- 主机侧 `Judge` 实现、`SimEvent` 构造、`Append` 写入 —— 归 9 / 10 的 GDD(3 只产意图,判据只断 3 侧)
- ADR-001 网络层 pipe / 可靠通道实现 —— 45 的 P1b 轮,本故事只定交出物形状

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-A6**: asmdef 引用集白名单 —— 不引用声明 `IEventSink`/`SimEvent` 的程序集;交出物闭集。
  - Given: 输入程序集的 `.asmdef`。
  - When: 解析 `references` 列表,与「声明 `IEventSink` / `SimEvent` 的程序集」集合求交。
  - Then: 交集为空(否则构建失败);3 公开的交出物类型 ∈ {`InteractIntent`, `EmergencyIntent`, `FocusNavigationIntent`, `EmergencyReading`}(白名单断言)。
  - Edge cases: 通过中间程序集**间接**引用(调用链可达性收紧,同 AC-A4② 分析器口径);夹具 asmdef 加一条 `Sim` 引用 ⇒ 红。
  - Negative fixture: 夹具源码出现 `IEventSink.Append` 调用 ⇒ 红。
- **AC-3-A7**: 载荷可达类型闭包递归零浮点。
  - Given: 全部 `SimEvent` 载荷类型(含本故事的意图类型)。
  - When: 从每个载荷根做**递归**字段/数组元素/泛型参数扫描。
  - Then: 可达闭包内 `float`/`double` 字段零出现;**嵌套结构体**内的 `float`(顶层看不见)被抓住(负例自证:夹具 `struct Outer { Inner i; } struct Inner { float x; }` ⇒ 红)。
  - Edge cases: 接口字段/多态载荷;`List<T>` / 数组元素类型;继承链基类字段。
  - Negative fixture: 上述嵌套结构体夹具。
- **AC-3-B3**: `JudgeResult` / `EmergencyReading` 全整数,运行期不经 `FixParse.Parse(string)`。
  - Given: `EmergencyReading` 类型与 3 侧构造路径。
  - When: 字段类型断言 + 调用点扫描。
  - Then: 字段全 `int`/`Fix`/`bool`/枚举/`long`(tick 计数);3 侧无 `FixParse.Parse(` 调用(判定结果由 10 直接构造,不经字符串)。
  - Edge cases: `float` 经别名(`System.Single`)出现 ⇒ 同拒;`EmergencyReading` 内嵌结构体字段同样递归(承 A7 纪律)。
  - Negative fixture: 夹具字段 `float magnitude` ⇒ 红。
- **AC-3-B4**: C 路聚合上行 —— 一条 `EmergencyAttempt`,本地判定仅预表现。
  - Given: 模拟一次急救动作(含多子操作)的输入周期。
  - When: 汇总本周期上行载荷。
  - Then: **恰一条** `EmergencyAttempt`(全整数)被构造 —— 非逐帧一条(「不逐帧同步输入」);本地 `Judge` 调用**不产生**任何流写入(无 `Append` 调用点可达)。
  - Edge cases: 单周期 0 次动作 ⇒ 0 条上行(不发空帧);失败动作(判定前中止)⇒ 仍聚合(判定输入语义,主机才有权判失败)。
  - Negative fixture: 实现改成逐帧上行 ⇒ 「恰一条」红;本地 `Judge` 写流 ⇒ 「无 `Append` 可达」红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input_system/intent_boundary_test.cs` — must exist and pass

**Status**: [x] Created — EditMode 真身 77 测全绿(见 Completion Notes)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/intent_boundary_test.cs`,文档路径 `tests/unit/input_system/` 为登记口径

---

## Dependencies

- Depends on: Story 001(意图类型住唯一动作资产所在的输入程序集)
- Unlocks: Story 007(直读通道读出的正是本故事定义的 `EmergencyReading`)

---

## Completion Notes
**Completed**: 2026-09-26
**Criteria**: 4/4 passing(AC-3-A6 / AC-3-A7 / AC-3-B3 / AC-3-B4,零 UNTESTED)
**Deviations**: None
**Test Evidence**: EditMode 真身 `unity/Assets/Tests/EditMode/InputSystem/intent_boundary_test.cs`(77 测)+ 落点 `unity/Assets/Gameplay.Input/IntentTypes.cs`(四意图 + `EmergencyReading`)· `unity/Assets/Gameplay.Input/EmergencyAggregator.cs`(C 路聚合器);合并树 **724/724 全绿 exit 0**(log `unity/Logs/build-story006-merged.log`)
**Code Review**: Complete —— 会话内 `/code-review` 双代理并行:unity-specialist **S1–S12 全修**(含 **S1 BLOCKING**)+ qa-tester **G1–G5 / G7 / G9 全修**;复跑全绿后收口
**Review Fix Batch(两批)**:
- *qa-tester 批(G1–G5 / G7 / G9)*:A6 传递闭包(G3 间接引用)— 新块 `BuildProjectReferenceGraph` / `ReferenceClosure` / `ReferenceClosureViolations` + 测;sink-but-no-SimEvent-literal 负例;raw-output-vs-synthetic 双测试(真树零错 + 纯谓词负例);交出物闭集精确集断言;**B3 白名单闭合成文**(G5);`CheckInputSourceText` 负例转直驱;IL 面 `matched==0` 红。
- *unity-specialist 批(S1–S11)*:**S1(BLOCKING)B3 构建期强制点** —— 反射面读不到 Gameplay.Input 类型(门不引被门对象)⇒ B3 只由测试驱动,把 `EmergencyReading.Magnitude` 改 float,build 照样绿;改走 **Cecil IL 面** `CheckReadingFieldsIl`:读产物元数据、不产生程序集引用 ⇒ A6 编译期边不破(门读 DLL = 结构自证闭环);on-build 接线入 `BuildGate : IPreprocessBuildWithReport`。**S6** A7 扫描根原按 `*Payload` 命名约定派生(entities.yaml 的影子)⇒ 收成「Sim.Contracts **全部 struct** 减显式豁免」+ 交出根集,集合断言替掉 `roots ≥ 30`(差集显漏/显多)。**S7** A7 跳过 `const` 字段(编译期字面量不该让整族载荷恒红;static 可变仍扫)。**S8** B3 白名单口径补注(闭合:无符号窄化 / `char` / `Nullable<T>` 均不在内)。**S9** `IsBclRef` 的 `System` 侧收成**点前缀**(原无点泛化放行 `SystemFoo` 一族)+ 口径诚实化注记(零第三方真闭合 = b3 manifest 封闭性 + 引用集登记表,非 BCL 面)。**S10** asmdef / 产物缺失各自专属红行,替掉 `<产物缺失>` 哨兵。**S11** `EmergencyAggregator.Reset()` 收 `private`(不留中途静默清空入口)。**S2** `EmergencyReading` ctor 增边沿单调非递减断言。**S3** `AggregatedEmergency` ctor 镜像 codec 解码侧三条不变量(null / Edges==沿数 / 单调)。**S5** `Sample` 显红「累计视图契约违例」(原静默丢沿、`Edges` 偏小而**全绿** —— 最难查的静默失真面,前置断)。
**Traceability**: AC-3-A6 → 源文本零事件面 `test_inputSourceText_iEventSinkAppendCall_reportsRed` + `_sinkAppendWithoutSimEventLiteral_reportsRed` + `_fixParseCall_reportsRed` + `_commentMention_notFlagged` · asmdef 引用集 `test_inputReferenceSet_*` 族 · 传递闭包 `test_referenceClosure_*` 族 · 交出物闭集 `test_deliveredIntentClosure_*` 族 · AC-3-A7 → `test_payloadClosure_allRoots_currentTree_zeroFloats` + `_roots_coverEverySimContractsStruct`(S6 集合断言)+ `_nestedStructFloat_*` / `_arrayFloat_*` / `_listFloat_*` / `_derivedBaseFloat_*` / `_interfaceField_*` / `_concreteField_*` / `_constFloat_*` / `_staticMutableFloat_*` · AC-3-B3 → `test_readingFields_*` 族(负数/别名/数组元素/嵌套/string/object)+ **IL 面** `test_readingFieldsIl_realProduct_zeroErrors` / `_missingProduct_reportsRed` / `_emptyTypeNameSet_reportsRed` · AC-3-B4 → `test_aggregator_*` 族(EndAttempt 恰一条 / 零样本 null / Abort 零条 / 动作身份中途变更红 / 沿单调红 / 累计视图契约红 / Reset 不可达 / 载荷形状跨 `EmergencyAttemptPayload` 真断言)—— **0/4 UNTESTED**
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
**ADVISORY(未结,非本故事缺陷)**:① `EmergencyAction` 枚举仍 **OQ-10-6 未定** ⇒ 三类型 `Action` 字段一律 int ordinal(承支 1-b 枚举纪律;表归 10 / 21a 的烘焙数据)② **S6 根集判据的余留观察**:根 = 「Sim.Contracts 全部 struct」= 依赖契约层的**结构布局**;若未来登记**非 struct** 载荷(类 / 接口形态),判据面自动收窄 —— 属契约层设计变化,触发时由承载轮裁定(本批不立 OQ,先留注)③ G9 移交已落 Story 007(见该文件 Implementation Notes:「EdgeTicks 无防御性拷贝」—— 通道不得跨帧持有数组)
