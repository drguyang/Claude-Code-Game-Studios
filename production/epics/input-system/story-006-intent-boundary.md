# Story 006: 意图边界与交出物(零 SimEvent · 全整数 · 聚合上行)

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

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

- [ ] **AC-3-A6(BLOCKING)**: **零 `SimEvent`** —— 输入程序集 asmdef **引用集白名单断言**:不引用任何声明 `IEventSink` / `SimEvent` 的程序集(构建失败);3 **只产意图**(`InteractIntent` / `EmergencyIntent` / `FocusNavigationIntent`)与 `EmergencyReading`
- [ ] **AC-3-A7(BLOCKING)**: **载荷可达闭包零浮点** —— 递归扫描 `SimEvent` 载荷可达类型闭包,断言无 `float`/`double`(与 `PresentationDtoGuard` 同构;**B3 只看顶层抓不到结构体字段** —— 必须递归进字段/元素类型)
- [ ] **AC-3-B3(BLOCKING)**: **判定结果全整数** —— `JudgeResult` 字段全 `int`/`Fix`;SimEvent 由 10 **直接构造**,运行期**不经** `FixParse.Parse(string)`;3 只交 `EmergencyReading`(全整数:tick 时长 + edge + 枚举序号)
- [ ] **AC-3-B4(BLOCKING,承 Amendment B)**: **联机 C 路** —— 客户端把意图**聚合为一条**全整数 `EmergencyAttempt` 上行 → 主机执行 `Judge` + `Append` + 发号 `Seq`;**本地判定仅预表现**(不写流、不参与权威);「不逐帧同步输入」保留

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

**Status**: [ ] Not yet created
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/intent_boundary_test.cs`,文档路径 `tests/unit/input_system/` 为登记口径

---

## Dependencies

- Depends on: Story 001(意图类型住唯一动作资产所在的输入程序集)
- Unlocks: Story 007(直读通道读出的正是本故事定义的 `EmergencyReading`)
