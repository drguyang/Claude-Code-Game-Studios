# Story 009: 焦点导航意图视图与单一真源

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(规则十 / 规则十一 / 规则十二)
**Requirement**: TR-input-009(焦点导航意图 = 类型化只读视图,单向、不持焦点状态)· TR-input-010(`Navigate` 单一真源 + 单栈门,控件不喂第二个焦点动作)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构 · ADR-013(次): 拟物 UI 框架
**ADR Decision Summary**: ADR-011 §三 —— 焦点导航走**官方桥**(`NavigationMoveEvent` + `FocusController` 引擎自动焦点移动)为唯一真源;**禁自实现焦点算法**(与 ADR-013 RC-1 同源,manifest Forbidden 表保留该条但注记两件未结矛盾);3 的 `FocusNavigationIntent` = 类型化**只读视图**(消费者:42 单栈路由 + 3 调试视图),**不驱动移动**。ADR-013 §十/Amendment —— **焦点单栈门**(同一时刻仅一栈接收导航意图流)归 42;`NavigationMoveEvent` / `FocusController` 符号归 ADR-013 spike 册页。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 本故事判据 = 结构断言(无焦点状态字段、无 `FocusController` 调用、结构性构造断言 C2)—— Roslyn 非 grep,类型断言为纯 C#。MEDIUM:`Navigate` 官方桥本体的符号(`NavigationMoveEvent`/`FocusController`)归 ADR-013 §6.6 假设 6 spike(P0 焦点载体前置门三件事归 42),本故事不断言桥可用性 —— 只断 3 侧**不越位**。

**Control Manifest Rules (this layer)**:
- Required: `Navigate` = UI map 唯一 `InputAction` 实例;官方桥唯一焦点真源;`FocusNavigationIntent` 单向只读(manifest Core · 输入(ADR-011);GDD 规则十)
- Forbidden: **自实现焦点算法而不禁官方桥**(manifest Forbidden 表 —— ⚠️ 与 ADR-013 RC-1 存在未结矛盾注记,承原表原文)· 3 内出现 `FocusController` 调用或焦点状态字段(AC-C1/C3/C4)
- Guardrail: 焦点载体前置门(①载体是谁 ②谁断言接通 ③spike 降级)归 42 —— 本故事的 C2 结构性断言**不依赖**载体类型(承 AC-C2 补注)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] **AC-3-C1(BLOCKING)**: **意图单向只读** —— 不返回值、不驱动焦点移动(D-A:唯一真源 = 官方桥)· 3 内**无焦点状态字段**、**无他系统状态字段**(= I2 机械身,与 A6 同构引用集断言)
- [ ] **AC-3-C2(BLOCKING · 结构性构造断言)**: **单一真源** —— 喂 `Navigate` 的物理控件**不得喂任何第二个焦点动作**;依赖 A1 **前半条**(单实例,不受桥类型影响)。初稿「禁自实现路径」是死条款(3 侧不存在自实现路径)—— 本条以**构造断言**落地:扫描绑重表,焦点控件的物理路径 ∉ 第二焦点动作的绑重路径
- [ ] **AC-3-C3(BLOCKING)**: **不感知持栈者** —— 意图类型**不携带目标栈字段**;3 内**无**「哪一栈持焦点」读取(类型断言)
- [ ] **AC-3-C4(BLOCKING)**: **不实现焦点移动** —— 3 内**无** `FocusController` 调用、**无焦点算法**(Roslyn,非 grep)

---

## Implementation Notes

*Derived from ADR-011 §三 / §Implementation Guidelines 7 + ADR-013 §9/§十 + GDD 规则十~十二:*

- **`FocusNavigationIntent` 形状**:类型化只读视图 —— 方向枚举(上下左右/页翻)+ 时间戳(tick),**单向**(只读,无返回值);**不驱动移动**(官方桥才是真源);消费者 = 42 的单栈路由(读它做呈现/路由)+ 3 自己的调试视图(读它显示)。I2 纪律:意图内**除自身设备态/通道态外无任何外部系统状态**
- **C1 / C3 / C4 是三道同构断言**:C1 断「无意图外状态」(asmdef + 字段扫描)· C3 断「不持栈感知」(类型字段断言)· C4 断「无焦点算法」(Roslyn 拒 `FocusController` 调用)。三者与 A6 同族 —— **结构/编译期**,不依赖运行期桥是否可用
- **C2 = 构造断言而非行为断言**:扫 `.inputactions` 绑重数据 —— 喂 `Navigate` 的物理控件路径,不得同时出现在任何第二个焦点动作的绑重里。这条**在数据层可静态验证**,无需跑游戏、无需知道焦点载体是谁(四轮补注:C2 依赖 A1 前半条单实例,不受桥类型影响 ⇒ P0 焦点载体 spike 不阻塞本判据)
- **规则十二边界**:**单栈门归 42**(同一时刻仅一栈接收导航意图流)—— 3 不知道栈存在(C3),42 负责路由;本故事不实现单栈门本身
- **官方桥 spike 归属**:`NavigationMoveEvent` / `FocusController` 可用性 = ADR-013 §6.6 假设 6 spike(P0 焦点载体前置门三件事归 42)—— 本故事**不把桥可用性当 AC**,只断 3 侧纪律
- **规则十一 = C4 的另一面**:3 不实现焦点;官方桥由呈现栈(UGUI 侧 `InputSystemUIInputModule` 或 UI Toolkit 的对应桥,由 42 定)持有

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:动作资产单实例(C2 的前置依赖 A1 前半条在 001 签核)
- Story 008:设备态(`KbmOnly`/`PadOnly`)—— 设备态 ≠ 焦点态,勿混
- 单栈门实现、焦点载体选型、`NavigationMoveEvent` 桥接 —— 42 的 GDD 与 ADR-013 spike(P0 焦点载体前置门)
- 焦点呈现样式/焦点框渲染 —— 42 / ADR-013 元件库

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-C1**: 意图单向只读,无外部状态字段。
  - Given: `FocusNavigationIntent` 类型定义与 3 的输入程序集。
  - When: 字段反射扫描 + asmdef 引用集扫描。
  - Then: 无方法返回焦点结果(只读视图,无 setter/驱动入口);字段全属意图自身(方向/时间戳),**无他系统状态字段**(引用集与 A6 白名单同构)。
  - Edge cases: `struct` vs `class` 实现差异(断言按字段不按引用类型);`static` 缓存了他栈状态(扫描含静态字段)。
  - Negative fixture: 夹具类型加 `object focusStack` 字段 ⇒ 红。
- **AC-3-C2**: 构造断言 —— `Navigate` 控件 ∩ 第二焦点动作控件 = ∅。
  - Given: `.inputactions` 全绑重数据(UI map + 其他 map)。
  - When: 取喂 `Navigate` 的物理路径集合;对每个「第二焦点动作」候选求交。
  - Then: 交集为空;`Navigate` 在 UI map 内**恰一个** `InputAction` 实例(规则十)。
  - Edge cases: 同一物理键被 `Navigate` 与**非焦点**动作(如 `Submit`)共享 —— 合法,只拦「焦点动作」;复合绑重的 part 路径展平后参与比较。
  - Negative fixture: 把 `Move` 的 W/A/S/D 也绑到一个假「第二焦点动作」⇒ 交集非空 ⇒ 红。
- **AC-3-C3**: 意图类型不持栈感知。
  - Given: `FocusNavigationIntent` 类型。
  - When: 字段反射断言 + 3 内「读焦点栈」符号扫描。
  - Then: 无 `stack`/`focusedStack`/`持栈者` 类字段;3 内无读取「哪一栈持焦点」的调用点。
  - Edge cases: 经字符串/枚举间接表示栈 id(同义词表收紧);`switch` 于栈枚举 ⇒ 红。
  - Negative fixture: 夹具字段 `int activeStackId` ⇒ 红。
- **AC-3-C4**: 3 内无 `FocusController` 调用、无焦点算法。
  - Given: 3 的输入程序集源码。
  - When: Roslyn 分析(非 grep)。
  - Then: `FocusController`(及同族焦点移动 API)调用零出现;自实现焦点移动逻辑(改变焦点对象的代码路径)零出现。
  - Edge cases: 经接口间接调用(符号可达性收紧);反射字符串 `"FocusController"` 调用(静态面拒,运行期反射超范围 —— 说明写入测试注释)。
  - Negative fixture: 夹具一行 `focusController.MoveFocus(...)` ⇒ Roslyn 红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input_system/focus_navigation_intent_test.cs` — must exist and pass

**Status**: [ ] Not yet created
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/focus_navigation_intent_test.cs`,文档路径 `tests/unit/input_system/` 为登记口径

---

## Dependencies

- Depends on: Story 001(单实例资产是 C2 构造断言的数据源)
- Unlocks: 42(消费 `FocusNavigationIntent` 做单栈路由)· Story 012(调试视图显示导航意图)
