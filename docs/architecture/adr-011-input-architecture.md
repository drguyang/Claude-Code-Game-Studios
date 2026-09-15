# ADR-011: 输入架构(Input System 动作映射与急救延迟路径)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 三条用户裁定已锁:① **action-based 官方路线**(Input System 动作资产,
> 动作映射 K&M + Gamepad + OpenXR 三套绑重 + bindings overrides 持久化;Legacy Input Manager 已弃用);
> ② **急救动作独立直读通道**(<50 ms 延迟直读 Input System 动作值,不穿过 42 UI 事件栈);
> ③ **焦点导航接口归 R-4 · 呈现归 R-6**(本 ADR 定义导航动作映射 + 焦点导航机制接口;
> R-6 定义 UI Toolkit 呈现与焦点路径实现)。
> 引擎侧经 unity-specialist lean 复核(2026-09-15):**无引擎侧 blocker**;结论并入 §Risks
> (F1 联机判定归表现层本地即时 · F2 双导航风险 + 意图事件流 · F3 绑重 GUID 稳定性 ·
> F4 float 边界 · F5 OpenXR 通用绑定 + 渲染延迟 · F6 预缓存 action 引用)。
> 独立评审由下一轮 `/architecture-review` 进行。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 三条裁定,均照准**)· technical-director(起草与裁决)
· network-programmer(急救延迟路径与 45 同步)· unity-specialist(引擎复核,2026-09-15)
· 系统 3 输入与设备 / 4 交互 / 10 急救动作 / 42 拟物 UI(P0)

## Summary

系统 3 输入与设备是**架构复核 §2 记为零覆盖的系统**(GDD ❌ / 架构覆盖 ❌ 零),
`TR-concept-007`(急救 <50 ms)与 `TR-concept-008`(手柄焦点导航)均为 gap,
`design/ux/` 不存在。本 ADR 裁决:**Input System action-based 官方路线** —— 动作资产
(动作映射 K&M + Gamepad + OpenXR 三套绑重)+ `bindings overrides` 持久化;
**急救动作独立直读通道**(<50 ms 延迟路径不穿过 42 UI 栈);**焦点导航接口归本 ADR,
呈现归 R-6**(接口 / 呈现分离)。系统 3 / 4 / 10 / 42 的输入层契约由此落定。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(输入 / 交互 / 延迟) |
| **Knowledge Risk** | **HIGH** —— Input System 在 6.3 的具体行为(action-based / 绑重持久化 / OpenXR 输入 / `NavigationMoveEvent`)均属 post-cutoff 知识,须实测;本裁决的**接口层**(动作映射契约 / 直读通道)为纯 C# 契约,不受影响 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `architecture-review-2026-09-15.md`(R-4 · §2 · E-16) · `design/gdd/systems-index.md` · `design/gdd/game-concept.md` · `technical-preferences.md` |
| **Post-Cutoff APIs Used** | **None** —— 动作资产与 Input System 均为 Unity 内置稳定 API;本裁决不承诺任何 post-cutoff API |
| **Verification Required** | ① 急救直读通道延迟实测(<50 ms 在 K&M / Gamepad / VR 三端);② 绑重持久化往返单测(bindings overrides 序列化 → 重载 → 逐键一致);③ OpenXR 输入动作映射 spike(VR 站定式急救);④ 焦点导航接口单测(动作映射 → 导航意图,呈现归 R-6) |

> **Note**: Knowledge Risk HIGH —— Input System / UI Toolkit 的 6.3 具体行为升级时须重读;接口层不受影响。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 确定性模拟 / 输入是意图源,不是模拟输入)· **ADR-009**(Accepted —— 表现态与模拟态分离;拾取意图 = 45 意图事件)· **ADR-001**(Accepted —— 表现态走 45;急救输入精度网络同步难 ⇒ 意图化) |
| **Enables** | **系统 3 / 4 / 10 / 42 的实现**(输入层契约)· `TR-concept-007`(gap → covered)· 急救动作小游戏(<50 ms 路径) |
| **Blocks** | 系统 3 / 4 / 10 / 42 的实现;凡依赖输入的 AC(P0)|
| **Ordering Note** | 本 ADR 是 **R-4 的落点**。**先 Accepted 本 ADR,再写 3 / 4 / 10 / 42 的输入层代码**。**不阻塞** 21a / 52 / 9 / 37 的既有实现(输入层是它们的消费者,不是上游)。R-6 呈现侧后续承接 |

## Context

### Problem Statement

系统 3 输入与设备在架构复核中**零覆盖** —— 无 GDD、无 ADR,`TR-concept-007` / 008 均 gap,
`design/ux/` 不存在。但约束已经很多且相互交叉:

1. **拟物 UI 必须同时支持键鼠与手柄焦点导航**(technical-preferences 硬约束):手柄没有指针,
   脉案 / 出诊箱 / 纸质地图全部交互都需要焦点导航路径;
2. **急救动作输入延迟 < 50 ms**(Performance Budgets 硬预算):「手稳」是急救小游戏的核心,
   延迟直接杀死它;
3. **VR(OpenXR)只做急救动作**(站定式):每个动作要做两遍(平面 + VR),输入栈归属未定(E-16);
4. **急救动作输入精度在网络上难同步**(game-concept 风险)—— 意图化(45)是出路,但输入层契约未定。

**不裁决的代价**:三套绑重(K&M + Gamepad + OpenXR)各写各的,急救延迟路径穿过 42 渲染栈,
焦点导航与 UI 事件栈耦合 —— P0 末期发现 <50 ms 路径被 UI 栈吃掉,重写输入层 = 三个月级灾难。

### Current State

- `systems-index.md`:3 输入与设备 | Core | P0 | 未开始;4 交互 | Core | P0;10 急救动作 | Core | P0;
  42 拟物 UI | Foundation | P0。
- `technical-preferences.md` Input & Platform:Primary = Keyboard/Mouse;Gamepad「完整可用但不作一等公民」;
  拟物 UI 必须同时支持键鼠与手柄导航。
- `tr-registry.yaml`:`TR-concept-007`(急救 <50 ms)· Performance · **gap** · suggested R-4;
  `TR-concept-008`(拟物 UI 手柄焦点导航)· **gap** · suggested R-6。
- `design/ux/` **不存在**。

### Constraints

- 不得破坏 ADR-005:输入是**意图源**(不直接驱动模拟),确定性模拟在主机执行。
- 不得破坏 ADR-009 / 001:拾取 / 交互意图 = 45 意图事件;急救输入精度网络难同步 ⇒ 意图化。
- 不得破坏 ADR-010:输入层零存档职责(绑重持久化除外)。
- **Legacy Input Manager 已弃用**(VERSION.md):用 Input System。
- 手柄不作一等公民,但**必须完整可用**(焦点导航路径是硬约束)。

### Requirements

- 三套绑重(K&M + Gamepad + OpenXR)统一组织,一套动作映射契约。
- 急救动作输入延迟 < 50 ms(K&M / Gamepad / VR 三端)。
- 拟物 UI 的焦点导航:接口(动作映射 → 导航意图)在本 ADR,呈现(R-6)。
- 输入是意图源:3 / 4 / 10 的输入层不直接写模拟域;意图经 45(ADR-001)或 `IEventSink`(P0 本地)。
- 满足 `TR-concept-007` / 008(gap → covered 或转 R-6)。

## Decision

**裁决:Input System action-based 官方路线(动作资产 + bindings overrides 持久化)·
急救动作独立直读通道(<50 ms 不穿 UI 栈)· 焦点导航接口归 R-4、呈现归 R-6。**

### 一、动作映射与绑重(action-based 官方路线)

- **一套动作资产**(Input Action Asset):动作映射(K&M + Gamepad + OpenXR 三套绑重)统一组织。
  P0 动作集:`Move` / `Interact` / `Emergency`(急救专用)等;具体动作清单归 3 / 4 / 10 各自 GDD。
- **绑重持久化**:`bindings overrides`(Input System 内置)序列化 + 重载;
  玩家改键 → override 写盘 → 重载逐键一致。绑重文件 = 输入层唯一存档职责(ADR-010 之外)。
  > **⚠️ 引擎复核 F3(bindings overrides GUID 稳定性)**:overrides 按 action 资产内 binding
  > GUID 匹配,**资产重建即静默失效**(无报错)。须:重载前 `asset.Disable()`;
  > overrides JSON 头存动作结构 schema hash,失配时**优雅清空**而非静默;
  > 1-4 人同机共进程时须每玩家 `Clone` 资产(纯联机单人每机则低危)。
- **Legacy Input Manager 禁用**:新输入系统是默认(6.3);输入层零旧输入依赖。
- **意图化**:3 / 4 / 10 的输入层产出**意图**(`InteractIntent` / `EmergencyIntent` 等),
  不直接写模拟域;P0 本地 = `IEventSink` 直接落,联机 = 45(ADR-001 pipe)。

### 二、急救动作独立直读通道(<50 ms 延迟路径)

- **急救动作小游戏的输入直读 Input System 动作值**(`action.ReadValue<>()` 在 Update 或
  专用回调),**不穿过 42 UI 事件栈**(UI Toolkit 事件系统有排队 / 焦点路由延迟,会吃掉 <50 ms 预算)。
- **判定归属 —— 单机 vs 联机(引擎复核 F1)**:
  - **单机**:判定走 9 / 10 的逻辑域(判断 + 熟练度),直读通道只做「手感」;
  - **联机**:判定**归表现层本地即时出结果保手感**(ADR-009 表现层),**仅结果事件**
    (成功 + 熟练度)进流给主机 —— 若判定走主机权威(ADR-005),联机判定反馈 = RTT,
    **必然破 <50 ms 预算**,急救手感断裂。
- **float 边界(引擎复核 F4)**:急救直读值是 float(手感层,OK,**不进流**);
  **判定结果进流前必须 `FixParse`**(ADR-006)—— 明示「意图非 SimEvent,仅判定结果为 SimEvent」。
- **三端验证**:K&M / Gamepad / VR(OpenXR 动作映射)三端各实测 <50 ms;
  若某端超预算,该端降级(VR 站定式)或意图化(联机,45)。
- **输入精度网络难同步的出路**:急救输入**意图化**(玩家意图 → 本地即时判定 → 结果事件进流),
  不做逐帧输入同步 —— 与 ADR-009 拾取同理(意图进流,判定主机当下做,但急救判定**本地做**保手感)。

### 三、焦点导航:接口归 R-4、呈现归 R-6

- **本 ADR(R-4)**:定义**导航动作映射 + 焦点导航机制接口** ——
  输入层把导航动作(Gamepad D-pad / 左摇杆 / 键鼠 Tab / Enter)映射为 `FocusDirection`
  **意图事件流**(单向,不返回值 —— 引擎复核 F2)。
- **双导航风险(引擎复核 F2)**:官方 UI Toolkit 已有「action → `NavigationMoveEvent`」桥
  (InputSystemUIInputModule 的 UI 动作映射)。若 R-6 用官方桥,R-4 接口**冗余**;
  若自实现焦点路径,**必须禁官方桥的 Navigation 动作**,否则同键双触发。
  本 ADR 裁决:**R-6 用官方桥**(`NavigationMoveEvent`),R-4 接口只定义动作 → 导航意图的
  意图事件流,**不重复实现焦点移动**。
- **R-6(拟物 UI 框架)**:定义 UI Toolkit 呈现 —— 焦点路径实现、`NavigationMoveEvent` spike、
  世界空间 / XR 两套栈(如 E-16 需早决)。接口不变,呈现侧 R-6 承接。
- **边界**:本 ADR 只定「动作 → 导航意图」;R-6 定「导航意图 → UI 焦点移动」。

### Architecture Diagram

```
   K&M ─┐
  Gamepad ┼──▶ Input System 动作资产(action-based)──▶ 意图(Interact / Emergency / FocusDir)
   OpenXR ┘         │
                    ├── 急救动作直读通道(Update 直读,<50ms,不穿 42 UI 栈)──▶ 急救判定(纯逻辑)
                    │         └── 意图化(联机,45 pipe;P0 本地 IEventSink)
                    ├── 焦点导航意图(FocusDirection)──▶ R-6 UI 呈现(焦点路径实现)
                    └── 绑重持久化(bindings overrides → 文件)──▶ 输入层唯一存档职责
```

### Key Interfaces

```csharp
// ── 焦点导航:动作 → 导航意图事件流(单向,不返回值;呈现归 R-6)──
struct FocusNavigationIntent { FocusDirection Direction; bool Activate; }

enum FocusDirection { Up, Down, Left, Right, Previous, Next }

// ── 急救直读通道(独立于 42 UI 栈)──
interface IEmergencyInput
{
    InputAction GetAction(EmergencyAction a); // 预缓存 InputAction 引用(引擎复核 F5/F6,禁每帧字符串查找)
    float ReadValue(EmergencyAction a);       // 直读动作值(Update/回调)
    bool  IsPressed(EmergencyAction a);       // 手感层:不做判定
}

// ── 意图(输入层产出,不写模拟域)──
struct InteractIntent  { /* 交互对象 / 时机 */ }
struct EmergencyIntent { /* 急救动作类型 / 方向 */ }
```

### Implementation Guidelines

1. **先写动作资产 + 绑重持久化**:动作映射契约是输入层的锚点。
2. **急救直读通道与 42 解耦**:Update 直读,不穿 UI 事件栈;三端实测 <50 ms。
3. **意图化**:3 / 4 / 10 输入层产出意图,不直接写模拟域;P0 本地 = `IEventSink`,
   联机 = 45(ADR-001 pipe)。
4. **焦点导航 = 意图事件流(单向)**:动作 → `FocusNavigationIntent`;R-6 用官方桥
   (`NavigationMoveEvent`),禁官方桥 Navigation 动作重复绑定(引擎复核 F2)。
5. **绑重持久化**:`bindings overrides` 序列化 + 重载逐键一致;`asset.Disable()` 后重载 +
   schema hash 失配优雅清空(引擎复核 F3)。
6. **禁用 Legacy Input Manager**(6.3 默认新输入系统)。
7. **急救直读预缓存 `InputAction` 引用**(枚举索引,禁每帧字符串查找 —— 引擎复核 F6)。
8. **OpenXR 绑通用 `XRController` 布局**(勿绑 OculusTouch/Index 专属)· 启用 Simple Controller
   profile;触诊手柄震动走 OpenXRInput haptics 须单独 spike(引擎复核 F5)。

## Alternatives Considered

### Alternative 1: 自研薄层再包装(action-based 之上)

- **Pros**:未来主机 / 体感设备扩展点
- **Cons**:Input System 已是动作映射标准;自研层是「抽象灾难」的起点(未兑现的抽象)
- **Rejection Reason**:action-based 官方路线满足全部约束;自研层是过度工程(用户裁定①)

### Alternative 2: 急救输入统一经 UI 事件栈

- **Pros**:单一输入路径,架构简单
- **Cons**:UI Toolkit 事件排队 / 焦点路由延迟吃 <50 ms 预算;「手稳」核心被延迟杀死
- **Rejection Reason**:<50 ms 硬预算与 UI 事件栈不相容(用户裁定②)

### Alternative 3: 焦点导航全部归 R-6

- **Pros**:R-4 只管动作映射,职责更窄
- **Cons**:导航动作映射是输入层契约(本 ADR),UI 呈现是 UI 层(R-6);全归 R-6 =
  把输入层契约塞进 UI 框架
- **Rejection Reason**:接口 / 呈现分离:动作映射归输入层,焦点移动归 UI(用户裁定③)

## Consequences

### Positive

- **系统 3 / 4 / 10 / 42 输入层契约落定**(零覆盖 → 有权威件)
- **急救 <50 ms 路径有架构保证**(直读通道,不穿 UI 栈)
- **焦点导航接口 / 呈现分离**(R-4 / R-6 各自承重,不互相阻塞)
- **意图化**:输入层与模拟域解耦,联机(45)与 P0 本地同构
- **绑重持久化**单一职责(输入层唯一存档)

### Negative

- Input System 的 6.3 具体行为须 spike(动作资产 / 绑重 / OpenXR 输入)
- 急救直读通道是三端各一套手感(K&M / Gamepad / VR)—— 重复成本
- 焦点导航呈现(UI 侧)仍待 R-6;接口先行,呈现后置

### Neutral

- 意图化使输入层成为纯意图源,不直接写模拟域
- 绑重文件是输入层唯一存档职责,不进入 ADR-010 存档

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **急救延迟超 <50 ms**(UI 栈 / 排队 / 焦点路由吃掉预算) | 中 | **高** | 直读通道 + 三端实测;单机判定本地即时,联机判定归表现层(结果事件进流)—— F1 |
| **联机判定反馈 = RTT 破预算**(判定走主机权威) | 中 | **高** | 急救判定归 ADR-009 表现层本地即时出结果保手感;仅结果事件(成功 + 熟练度)进流给主机(F1) |
| **绑重持久化往返失真**(overrides GUID 失配 → 静默失效) | 中 | 中 | 重载前 `asset.Disable()`;overrides JSON 头存 schema hash,失配优雅清空(F3);往返单测 |
| **双导航同键双触发**(R-4 接口 + 官方桥重复绑定) | 中 | 中 | R-6 用官方桥(`NavigationMoveEvent`);R-4 接口 = 单向意图事件流,禁官方桥 Navigation 重复绑定(F2) |
| **OpenXR 输入动作映射不达预期**(VR 站定式急救) | 中 | 中 | 绑通用 `XRController` 布局 + Simple Controller profile;触诊震动 spike(F5);三端实测 |
| **float 泄漏进流**(急救直读值是 float) | 中 | **高** | 直读值 = 手感层不进流;判定结果进流前 `FixParse`(ADR-006)—— F4 |
| **Legacy Input Manager 泄漏**(旧输入依赖混入) | 低 | 高 | 输入层零旧输入依赖;EditMode 探针(禁 Legacy Input Manager 引用) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 直读通道 O(1) / 帧;动作映射 O(n 动作) | 16.6 ms 平面 / 11.1 ms VR |
| Latency | — | 急救直读通道 <50 ms(K&M / Gamepad / VR 三端实测) | 急救操作 <50 ms(手感预算) |
| Memory | — | 绑重 overrides 文件 + 动作资产 | 待定 |
| Network | — | 意图化(45 pipe);不逐帧同步输入 | 待定 |

## Migration Plan

**本项目尚无输入实现,故无迁移 —— 本 ADR 是「第一次就做对」。(同 ADR-005 / 010 / 001)**

1. **写动作资产 + 绑重持久化**(动作映射契约锚点)。
2. **写急救直读通道**(Update 直读,不穿 42 UI 栈)+ 三端实测 <50 ms。
3. **写意图层**(3 / 4 / 10 输入层产出意图;P0 本地 `IEventSink`,联机 45)。
4. **写焦点导航接口**(动作 → 导航意图);R-6 呈现侧承接。
5. **撰写 3 / 4 / 10 / 42 GDD 时**,输入动作清单以本 ADR §一 为准。

**Rollback plan**:若急救直读通道实测超预算,降级路径 = VR 站定式或意图化(45),直读通道本身
**可回退**(退回 UI 栈)。但 **动作映射契约 / 意图化 / 焦点导航接口** 已进输入层,**不可回退**
(改契约 = 重写 3 / 4 / 10 输入层)。

## Validation Criteria

- [ ] **急救延迟实测**:K&M / Gamepad / VR 三端各 <50 ms(直读通道,不穿 UI 栈;**含渲染延迟 ≈2 帧**,F5)
- [ ] **联机急救手感**:丢包 / RTT 下判定本地即时出结果,结果事件进流(主机结算一致)—— F1
- [ ] **绑重持久化往返**:overrides 序列化 → 重载 → 逐键一致;schema hash 失配 → 优雅清空(F3)
- [ ] **意图化单测**:输入层产出意图,不直接写模拟域(P0 本地 `IEventSink` 落)
- [ ] **焦点导航单测**:导航动作 → `FocusNavigationIntent` 单向事件流;无同键双触发(F2)
- [ ] **float 边界单测**:急救直读值不进流;判定结果进流前 `FixParse`(F4)
- [ ] **Legacy Input Manager 零引用**:EditMode 探针(输入层禁旧输入依赖)
- [ ] `TR-concept-007`(急救 <50 ms)覆盖 —— registry 状态更新为 covered
- [ ] `TR-concept-008`(拟物 UI 手柄焦点导航)转 R-6 —— registry note 更新

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/systems-index.md` | 3 输入与设备 | 系统 3(P0 · 键鼠/手柄/VR) | 本 ADR 即其输入层权威件;意图化 + 动作映射 |
| `design/gdd/systems-index.md` | 4 交互系统 | 系统 4(P0 · 依赖 1, 3) | 意图层(`InteractIntent`);45 意图事件(联机) |
| `design/gdd/systems-index.md` | 10 急救动作 | 系统 10(P0 · <50 ms 预算) | 直读通道 + 意图化;三端实测 |
| `design/gdd/systems-index.md` | 42 拟物 UI 框架 | 系统 42(P0 · 手柄无指针) | 焦点导航接口(R-4);呈现归 R-6 |
| `design/gdd/game-concept.md` | 急救动作 | 急救小游戏可跳过 · 手感小游戏 | 直读通道;VR 站定式降级 |
| `design/gdd/disease-simulation.md` | 9 | 脉案是焦点导航界面 · VR 触诊手柄震动 | 焦点导航接口;OpenXR 输入 spike |

## Related

- **ADR-005 确定性模拟与状态同步模型**(Accepted)—— 输入是意图源,不直接驱动模拟
- **ADR-001 联机选型:pipe 抽象**(Accepted)—— 急救输入意图化走 45;表现态走 45
- **ADR-009 世界状态的事件化边界**(Accepted)—— 意图进流、判定主机当下做(拾取同理)
- **ADR-010 持久化与存档格式**(Accepted)—— 绑重持久化 = 输入层唯一存档职责(不入三流存档)
- **R-4 输入架构**(architecture-review 2026-09-15)—— 本 ADR 是其落点
- **R-6 拟物 UI 框架**(待撰写)—— 焦点导航呈现侧;UI Toolkit 焦点路径 spike
- `design/registry/entities.yaml` —— 待登记输入常量(急救直读通道频率等)
