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
> ⚠️ **F1 已于 2026-09-18 被 Amendment B 改判**(承系统 10 的 GDD 首轮评审 + 用户裁定 C 路):
> 本地即时的是**预表现**,**判定与 `Append` 在主机**,上行的是**聚合意图事件**
> (`EmergencyAttempt`)而非结果事件。其余五项不受影响。
> 独立评审由下一轮 `/architecture-review` 进行。
>
> **⚠️ 2026-09-15 Amendment A(六项口径修正)** —— 由系统 3 输入与设备 GDD 的首轮
> `/design-review`(**NEEDS REVISION**)连带触发:该 GDD 的严重缺陷多数是**忠实继承本 ADR 的
> 未推演断言**。六项 = ① `FixParse` 范畴错误 · ② `Instantiate()` + `RemoveAllBindingOverrides()` ·
> ③ hash 须含 `bindingId` · ④ 直读通道理由改为**抖动(方差)** · ⑤ 回调 ≠ 轮询 · ⑥ VR 归 P1b。
> **不推翻三条核心裁决;不加宽 `IEmergencyInput`**(模拟量通道缺口登记为 `OQ-3-5`,系统 10 的前置)。
> 详见 §Decision 的 **### Amendment A**。

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
| **Verification Required** | ① 急救直读通道延迟实测(<50 ms;**Amendment A:仅 K&M / Gamepad 两端**,VR 归 P1b);② 绑重持久化往返单测(bindings overrides 序列化 → 重载 → 逐键一致;**含 `RemoveAllBindingOverrides()` 与 hash 含 `bindingId`**);③ OpenXR 输入动作映射 spike(**P1b**,VR 站定式急救);④ 焦点导航接口单测(动作映射 → 导航意图,呈现归 R-6) |

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
> ⚠️ **2026-09-21 历史注**:「无 GDD / `design/ux/` 不存在」是本 ADR(2026-09-15)时的状态 ——
> 3 后已立 `input-system.md`,`design/ux/` 与 `design/accessibility-requirements.md` 现已存在。
> 本段是立约动机记录,不删;`TR-concept-008` 的 gap 处置以 registry 现值为准(非本注)。

1. **拟物 UI 必须同时支持键鼠与手柄焦点导航**(technical-preferences 硬约束):手柄没有指针,
   脉案 / 出诊箱 / 纸质地图全部交互都需要焦点导航路径;
2. **急救动作输入延迟 < 50 ms**(Performance Budgets 硬预算):「手稳」是急救小游戏的核心,
   延迟直接杀死它;
3. **VR(OpenXR)只做急救动作**(站定式):每个动作要做两遍(平面 + VR),输入栈归属未定(E-16);
4. **急救动作输入精度在网络上难同步**(game-concept 风险)—— 意图化(45)是出路,但输入层契约未定。

**不裁决的代价**:三套绑重(K&M + Gamepad + OpenXR)各写各的,急救延迟路径穿过 42 渲染栈,
焦点导航与 UI 事件栈耦合 —— P0 末期发现 <50 ms 路径**被 UI 路由的帧内调度排序引入不可控方差**
(注:`⚠️ 2026-09-16` —— 初稿此处写「被 UI 栈**吃掉预算**」,是**算术错误**,见 Amendment A 第 4 行;
失败模式是**方差**不是均值),重写输入层 = 三个月级灾难。

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
- 急救动作输入延迟 < 50 ms(**P0 = K&M / Gamepad 两端**;VR 归 P1b —— `⚠️ 2026-09-16`)。
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
  > GUID 匹配,**资产重建即静默失效**(无报错)。须:重载前 `asset.Disable()` **且
  > `asset.RemoveAllBindingOverrides()`**(`Disable()` **不移除已载入的 override** —— override 是叠加的);
  > overrides JSON 头存动作结构 schema hash,失配时**优雅清空**而非静默;
  > 1-4 人同机共进程时须每玩家 `Instantiate` 资产(纯联机单人每机则低危)。
  > ⚠️ **须 spike**:`Instantiate()` 与 `Clone()` 在引擎参考库中**同样零覆盖** ——
  > 二者的取舍**不能由权威件证实**(见 §Risks-A S2);**驳回「改回 `Clone()`」的回退建议**(换名不减少风险)。
  > **⚠️ 2026-09-15 Amendment A 修正四处**:① `Clone()` → **`Instantiate()`**;
  > ② 补 `RemoveAllBindingOverrides()` 一步;③ schema hash **须含 `bindingId`**,
  > 否则对「重建后静默失效」**完全盲**(那正是 F3 要防的);④ 克隆**不得启用 UI map**。
- **Legacy Input Manager 禁用**:新输入系统是默认(6.3);输入层零旧输入依赖。
- **意图化**:3 / 4 / 10 的输入层产出**意图**(`InteractIntent` / `EmergencyIntent` 等),
  不直接写模拟域;P0 本地 = `IEventSink` 直接落,联机 = 45(ADR-001 pipe)。

### 二、急救动作独立直读通道(<50 ms 延迟路径)

- **急救动作小游戏的输入直读 Input System 动作值**(`action.ReadValue<>()`),**不穿过 42 UI 事件栈**。
  > **⚠️ 2026-09-15 Amendment A 修正两处**:
  > ① **采样点 = `InputSystem.onAfterUpdate` 回调**,**不是 `Update()` 轮询** ——
  > 二者**不等价**:回调严格发生在 `InputSystem.Update()` 之后、`Update()` 之前的**固定相位**,
  > 轮询的相位则取决于脚本执行顺序。初稿的「Update **或**专用回调」把它们当等价,**错**。
  > ② **绕过 UI 栈的理由 = 抖动(方差),不是预算算术** —— 初稿写「UI Toolkit 事件系统有排队 /
  > 焦点路由延迟,**会吃掉 <50 ms 预算**」。这是**假的**:UI 路由是**帧内调度成本**,
  > 不改变 `L_render`,也无法在算术上腾出预算。**正确理由**:UI 路由使这一跳的延迟
  > **逐帧变化(方差不可控)**,而「手稳」小游戏练的是**可重复的时机感** ——
  > 均值够低但方差大的链**不可练**。**结论(直读通道)保留,理由更换。**
- **判定归属 —— 单机 vs 联机(引擎复核 F1)**:
  > **⚠️ 2026-09-18 Amendment B(承系统 10 的 GDD 首轮评审 · 用户裁定取 C 路)—— 修正本节裁决**
  > **本节原裁决「联机急救判定归表现层本地即时,**仅结果事件进流**」已被改判。**
  > 改判理由(三条,任一独立成立即须改):
  > ① **「仅结果事件进流」使判定输入不在流里** ⇒ 重放时主机没有 `Judge` 的输入,
  >   直接违反 **ADR-016 §一 三源不变量**(决策全部输入 ∈ {流, 版本化烘焙数据, 二者的纯函数})
  >   与 **ADR-005**「病史事件流是唯一真源」—— 后果不是「不够纯」,是**重放不出同样的世界**
  >   (10 的 `AC-10-05` 跨平台逐位对拍不可实现)。
  > ② 客户端上报的 `JudgeResult` 把**流的内容**交给客户端诚实性做保 —— 伪造成本 = 改一个枚举值,
  >   收益 = 满 `potency`,且主机连事后审计都做不到(读数不在流里)。
  > ③ **原裁决的前提「判定走主机权威必然破 <50 ms」是一个范畴错误**:`L_input < 50 ms`
  >   是**「输入 → 呈现」预算,不是「权威归属」预算**。C 路下**预表现即时**(本地判定立刻驱动
  >   手 / 音 / 病人反应),**只有进流那一步**等主机 —— 而玩家对「事件何时进流」不可感知,
  >   对「体征何时变」**本来就延迟一个 tick**(9 的求值节奏)。两预算的分离见 10 的 F-10.6。
  > **现口径(权威,与 `emergency-procedures.md` 规则十一 / `adr-009` Amendment I 一致)**:
  > 客户端每完成一个动作**聚合为一条** `EmergencyAttempt` 全整数事件上行走 ADR-001 第二 QoS 通道
  > → **主机执行 `Judge`**(`ctx` 取主机 `Append` tick 上的施予者本人档 + 当下 `patient_ctx`)
  > → **主机 `Append` `EmergencyTreatmentApplied` 并发号 `Seq`**;客户端本地判定**降级为预表现**,
  > 不写流、不参与权威。**不逐帧同步输入**这一条**保留不变**(原裁决里对的那一半)。
  > **连带修订**:本节末「输入精度网络难同步的出路」中的「但急救判定**本地做**保手感」子句作废,
  > 改为「急救**预表现**本地做保手感,**判定**主机做保真源」。
  > **已结**(2026-09-23,ADR-001 窄修订·同族三项):`OQ-10-9` 由 **ADR-001 §一之三 裁决一**
  > 结清 —— `EmergencyAttempt` 判定输入类走**可靠通道上行**(不走第二 QoS);上行按语义二分
  > 「最新值 / 自愈类 vs 判定输入类」,第二 QoS 语义不变。原「归 45 的 GDD 轮,P1b 前」提前执行
  > (用户裁定)。
  - **单机**:判定走 9 / 10 的逻辑域(判断 + 熟练度),直读通道只做「手感」;**联机下形状不变**
    (主机 = 自己时 `EmergencyAttempt` 仍然落流 —— 10 规则十一的刻意选择,杜绝「单机走另一条路、
    联机才发现」的失效模式);
  - ~~**联机**:判定**归表现层本地即时出结果保手感**(ADR-009 表现层),**仅结果事件**
    (成功 + 熟练度)进流给主机 —— 若判定走主机权威(ADR-005),联机判定反馈 = RTT,
    **必然破 <50 ms 预算**,急救手感断裂。~~
    **【Amendment B 作废,2026-09-18】** 现口径见上:本地即时的是**预表现**,
    **判定与 `Append` 在主机**;上行的是**聚合意图事件**,不是结果事件。
- **float 边界(引擎复核 F4)**:急救直读值是 float(手感层,OK,**不进流**);
  **判定结果进流前必须 `FixParse`**(ADR-006)—— 明示「意图非 SimEvent,仅判定结果为 SimEvent」。
  > **⚠️ 2026-09-15 Amendment A 修正(范畴错误)**:`FixParse.Parse(string)` 是**导入期**的
  > **字符串 → `Fix`** 入口(`adr-006:105-122`);**运行期不存在 `float → Fix` 的路径 ——
  > 编译期就不可表达**。故「判定结果进流前必须 `FixParse`」**不可执行**。
  > **正确口径**:`JudgeResult` 的字段**本身全整数**(`int` 计数 + `Fix`),
  > **直接构造 `SimEvent`**,**运行期不经任何解析** —— `FixParse` 只在**装载作者态数据**时出现。
  > 判据另见系统 3 GDD 的 **AC-3-A6 / A7 / B3**(A7 = 载荷可达闭包零 `float` / `double` 字段)。
- **三端验证(Amendment A 修正层级)**:~~K&M / Gamepad / VR(OpenXR 动作映射)三端各实测 <50 ms~~
  → **P0 = K&M / Gamepad 两端实测**;**VR(OpenXR 动作映射)归 P1b**(承 ADR-013 §二
  「VR / world-space 实现推 P1a」与 `game-concept.md:720`「VR 急救在 P1b」),
  **P0 不验收 VR 延迟**。初稿把 VR 写进 P0 判据属**层级错位**。
  若某端超预算,该端降级(锁定刷新率 / 独占全屏)或意图化(联机,45)。
- **输入精度网络难同步的出路**:急救输入**意图化**(玩家意图 → 本地即时判定 → 结果事件进流),
  不做逐帧输入同步 —— 与 ADR-009 拾取同理(意图进流,判定主机当下做,但急救判定**本地做**保手感)。
  > **⚠️ 2026-09-18 Amendment B 连带修订**:上句的括号与后半读起来仍像「本地做判定」——
  > 准确口径:**急救的「预表现」本地做保手感,急救的「判定」主机做保真源**。
  > 破折号后「意图进流,判定主机当下做」**本来就是 ADR-009 §七 拾取的原口径**,
  > 急救侧此前是该口径的**唯一例外**,Amendment B 取消了这个例外 —— 于是本条回到与拾取**完全同构**。

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
                    ├── 急救动作直读通道(onAfterUpdate 回调直读,<50ms,不穿 42 UI 栈)──▶ 急救判定(纯逻辑)
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
    float ReadValue(EmergencyAction a);       // 直读动作值(onAfterUpdate 回调)
    bool  IsPressed(EmergencyAction a);       // 手感层:不做判定
}

// ── 意图(输入层产出,不写模拟域)──
struct InteractIntent  { /* 交互对象 / 时机 */ }
struct EmergencyIntent { /* 急救动作类型 / 方向 */ }
```

> **⚠️ 2026-09-15 Amendment A:本轮明确不加宽 `IEmergencyInput`。**
> 系统 3 GDD 评审指出 `Emergency = Button(0/1)` **表达不了** `game-concept.md:693` 点名的
> 两个动作(心肺复苏 = **节奏** / 止血包扎 = **顺序与力道**)—— 缺模拟量通道。
> **但本轮不修**:在系统 10(急救动作)GDD 未成文时加宽接口 = **过度设计**。
> **已登记为系统 10 的硬前置义务**(`OQ-3-5`),由 10 的 GDD 裁定采集形态后**另开修正案**。

### Implementation Guidelines

1. **先写动作资产 + 绑重持久化**:动作映射契约是输入层的锚点。
2. **急救直读通道与 42 解耦**:在 **`InputSystem.onAfterUpdate` 回调**内直读(**非 `Update()` 轮询**),
   不穿 UI 事件栈;**P0 在 K&M / Gamepad 两端实测 <50 ms**(VR 归 P1b)。
   ⚠️ **须 spike**(`onAfterUpdate` 相位在引擎参考库零覆盖 —— 见 §Risks-A S3)。
3. **意图化**:3 / 4 / 10 输入层产出意图,不直接写模拟域;P0 本地 = `IEventSink`,
   联机 = 45(ADR-001 pipe)。
4. **焦点导航 = 单向只读意图视图(不驱动移动)**:动作 → `FocusNavigationIntent`;
   **焦点移动的唯一真源 = 官方桥**(R-6 用官方桥 `NavigationMoveEvent`),禁官方桥 Navigation 动作重复绑定(引擎复核 F2)。
   ⚠️ **须 spike**(桥的符号名在引擎参考库零覆盖 —— 册页归 **ADR-013** §Verification Required;
   **判据层只断性质**:唯一真源 · 无双触发 · 3 不实现焦点算法 —— 见 §Risks-A 分层的说明)。
5. **绑重持久化**:`bindings overrides` 序列化 + 重载逐键一致;**`asset.Disable()` +
   `asset.RemoveAllBindingOverrides()` 后重载**(override 叠加,`Disable()` 单独不移除)+
   schema hash 失配优雅清空(**hash 须含 `bindingId`**;引擎复核 F3 / Amendment A)。
6. **禁用 Legacy Input Manager**(6.3 默认新输入系统)。
7. **急救直读预缓存 `InputAction` 引用**(枚举索引,禁每帧字符串查找 —— 引擎复核 F6)。
8. **OpenXR 绑通用 `XRController` 布局**(勿绑 OculusTouch/Index 专属)· 启用 Simple Controller
   profile;触诊手柄震动走 OpenXRInput haptics 须单独 spike(引擎复核 F5)。

### Amendment A —— 六项口径修正(2026-09-15 · 系统 3 GDD `/design-review` 的连带裁定)

**起因**:系统 3 输入与设备 GDD 的首轮 `/design-review` 判 **NEEDS REVISION**。
**根因诊断**:该 GDD 的多数严重缺陷**不是自创**,而是**忠实继承**了本 ADR 的
未尽推演断言 —— 修正必须落到本 ADR,否则下游会把错误契约继续传下去。
**用户裁定**:本改动集**一并出 ADR-011 修订块**;**明确不加宽 `IEmergencyInput`**。

修正**不推翻**本 ADR 的三条核心裁决(action-based 官方路线 / 急救独立直读通道 / 焦点接口与呈现分离);
六项均为**口径与实现细节**的更正:

| # | 原口径 | 修正后 | 性质 |
|---|--------|--------|------|
| 1 | 「判定结果进流前必须 `FixParse`」 | `JudgeResult` **本身全整数**,**直接构造 `SimEvent`**,运行期**无解析** —— `FixParse.Parse(string)` 是**导入期**入口,运行期不存在 `float → Fix` 路径(`adr-006:105-122`) | **范畴错误**(不可执行) |
| 2 | 「每玩家 `Clone` 资产」+ 重载前 `Disable()` | **`Instantiate()`**;**重载前 `Disable()` + `RemoveAllBindingOverrides()`**(override 叠加,`Disable()` 单独不移除);**克隆不得启用 UI map** | **会静默失效**(override 残留 / 双触发) |
| 3 | 「overrides JSON 头存动作**结构** schema hash」 | hash **须含 `bindingId`**(及 `processors` / `interactions` / `groups`);否则对「重建 ⇒ GUID 重生成 ⇒ overrides 静默丢失」**完全盲** —— 而那正是 F3 要防的 | **判据盲区** |
| 4 | 「UI 事件栈会**吃掉 <50 ms 预算**」 | 理由改为 **抖动(方差)**:UI 路由使延迟逐帧变化,「手稳」练的是可重复时机感 ⇒ 方差不可控的链**不可练**。**结论(直读通道)保留,理由更换** | **理由错、结论对** |
| 5 | 「`Update` **或**专用回调」直读 | **`InputSystem.onAfterUpdate` 回调**(固定相位),**轮询不等价**(相位取决于脚本执行顺序) | **伪等价** |
| 6 | 「三端验证 K&M / Gamepad / **VR**」 | **P0 = K&M / Gamepad**;**VR 归 P1b**(承 ADR-013 §二 · `game-concept.md:720`);补**降级路径**(锁定刷新率 / 独占全屏,用于 `L_render ≥ 50 ms`) | **层级错位** |

**本修正案的边界**:
- **不新增依赖、不改接口形状**(除上述细节)、**不动 §Alternatives 的被否决项**;
- **`IEmergencyInput` 保持原样** —— 模拟量通道缺口登记为 **`OQ-3-5`**(系统 10 的硬前置),
  **不在本轮加宽**(避免在 10 未成文时过度设计);
- 本 ADR **保留 Accepted 状态**。
  **⚠️ 2026-09-15 Amendment A**(六项)已并入 §Decision / §Risks / §Performance / §Migration / §Validation。
  **⚠️ 2026-09-16 三轮评审又就地覆写四处残留** —— Amendment A 原在此句写「正文各处已加前向指针」,
  **该自述当时为不实**(正文尚有未改句子:§Problem `:84` · §Consequences 两处(Vision / `:306`)·
  §Alt 2 · §Performance Latency `:331` · §Migration 步骤 2 `:340`)。**现已逐处覆写,并撤下「已扫全文」的宣称**;
  本轮不再自称「各处已加」——**以实际就地覆写为准,不以自我声称为准**。

**验证义务(新增,承系统 3 GDD 的 AC)**:
`AC-3-A8`(重建后 hash 变化 ⇒ 备份而非静默丢)· `AC-3-A7`(载荷可达闭包零 `float`/`double`)·
`AC-3-B1a`(零硬件合成注入 ≤1 帧)· `AC-3-B2`(**asmdef 引用断言 + Roslyn 分析器**,非 grep)。

## Alternatives Considered

### Alternative 1: 自研薄层再包装(action-based 之上)

- **Pros**:未来主机 / 体感设备扩展点
- **Cons**:Input System 已是动作映射标准;自研层是「抽象灾难」的起点(未兑现的抽象)
- **Rejection Reason**:action-based 官方路线满足全部约束;自研层是过度工程(用户裁定①)

### Alternative 2: 急救输入统一经 UI 事件栈

- **Pros**:单一输入路径,架构简单
- **Cons**:**抖动(方差)不可控** —— UI 事件排队 / 焦点路由使延迟成为 `EventSystem`
  帧内**调度顺序**的函数(逐帧变化),而「手稳」练的是**可重复的时机感**,方差大的链**不可练**
- **Rejection Reason**:**方差论** —— 恒定延迟可学,不可预测的误差不可学(用户裁定②)

> **⚠️ 2026-09-16 修订理由**:本条的 **Cons / Rejection Reason 原写「UI 栈延迟吃掉 <50 ms 预算」**
> —— 那是**算术错误**(UI 路由是帧内调度成本,**不改变 `L_render`**,故改变不了 `L_input→pixel`)。
> **结论(直读通道)保留,理由换成方差** —— 与 Amendment A 第 4 行、系统 3 GDD 规则七同源。

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
- 急救直读通道是**两端各一套手感**(P0:K&M / Gamepad;**VR 归 P1b**)—— 重复成本
- 焦点导航呈现(UI 侧)仍待 R-6;接口先行,呈现后置

### Neutral

- 意图化使输入层成为纯意图源,不直接写模拟域
- 绑重文件是输入层唯一存档职责,不进入 ADR-010 存档

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **急救延迟超 <50 ms**(排队 / 焦点路由引入**方差**) | 中 | **高** | 直读通道 + **两端**(K&M / Gamepad)实测;单机判定本地即时,联机判定归表现层(结果事件进流)—— F1 |
| ~~**联机判定反馈 = RTT 破预算**(判定走主机权威)~~ | 中 | ~~**高**~~ | ~~急救判定归 ADR-009 表现层本地即时出结果保手感;仅结果事件进流给主机(F1)~~ **⚠️ 缓解措施已被 `Amendment B`(2026-09-18)作废**:破预算的前提是「把「判定何时进流」当成玩家可感」—— 实际**不可感**(玩家对 `Append` 无感知,对「体征何时变」本就延迟一个 tick)。现缓解 = **预表现本地即时 + 判定/`Append` 主机**;残余风险改记为 `OQ-10-9`(**判定输入走第二 QoS 通道会丢**)—— 该残余比原风险**更实际**:原风险是假的,这个是会静默丢事件的 **→ ✅ 已结(2026-09-23,ADR-001 §一之三 裁决一:`EmergencyAttempt` 判定输入类改走可靠通道,该残余消解)** |
| **绑重持久化往返失真**(overrides GUID 失配 → 静默失效) | 中 | 中 | 重载前 `asset.Disable()`;overrides JSON 头存 schema hash,失配优雅清空(F3);往返单测 |
| **双导航同键双触发**(R-4 接口 + 官方桥重复绑定) | 中 | 中 | **`⚠️ 2026-09-16` 收窄**:官方桥为**焦点移动唯一真源**;R-4 侧**不喂第二个焦点动作**(`AC-3-C2` 结构性构造断言;初稿「R-4 接口 = 单向意图事件流,禁官方桥重复绑定」的措辞**方向反了** —— 被禁的是自建路径,不是官方桥) |
| **OpenXR 输入动作映射不达预期**(VR 站定式急救) | 中 | 中 | 绑通用 `XRController` 布局 + Simple Controller profile;触诊震动 spike(F5);**实测归 P1b**(P0 不验收) |
| **float 泄漏进流**(急救直读值是 float) | 中 | **高** | 直读值 = 手感层不进流;判定结果**全整数直接构造 `SimEvent`**(**Amendment A 修正**:初稿「进流前 `FixParse`」不可执行)—— F4;守门 = 系统 3 GDD 的 `AC-3-A7`(载荷可达闭包零 `float`/`double`) |
| **Legacy Input Manager 泄漏**(旧输入依赖混入) | 低 | 高 | 输入层零旧输入依赖;EditMode 探针(禁 Legacy Input Manager 引用) |
| **post-cutoff 引擎符号未经权威件核实**（输入更新相位 / 资产实例化 / 回调相位） | **高** | 中 | 见下 **§Risks-A spike 册页** —— **判据层零未核验符号**,符号名只留在规则 / 实现指引层并带「须 spike」标记 |

### §Risks-A spike 册页（唯一出处 · 2026-09-16 四轮建 · 系统 3 GDD 的未核验符号**集中登记于此**）

> **建册理由**：系统 3 GDD 的第四轮评审发现**三处引擎符号在
> `docs/engine-reference/unity/modules/input.md`（356 行）中零覆盖** ——
> 该册页只含 `SaveBindingOverridesAsJson` / `LoadBindingOverridesFromJson`（`:269-274`）与
> `PlayerPrefs` 用法，**对其余输入 API 无覆盖**。同时发现**一条同类缺陷由三轮修订自己产出**：
> `AC-3-B2③` 曾把未核验的**枚举成员名**写进 BLOCKING 判据（而同一 GDD 的 `AC-3-A4①`
> 已立下「判据层不得引用未核验符号」的相反纪律）。
> **用户裁定（2026-09-16）＝ 分层处置**，三层的落点即本节：
> ① **判据层（BLOCKING AC）零未核验符号** —— 只断**性质**；② **规则 / 实现指引层保留符号名**，
> 但**逐处带「须 spike」标记并集中登记于本册页**（不散落）；③ 凡 spike 未过的符号，
> 其**承载的性质**必须有**不依赖该符号形状**的降级路径。
> **执行**：与 **ADR-012 的 CI 门**同批（`unity-builder` 出 player + EditMode 探针）。

| # | 未核验符号 | 承载的**性质**（判据层照此写） | 系统 3 GDD 落点 | spike 验收对象 | 降级路径（spike 失败时） |
|---|-----------|--------------------------|----------------|---------------|----------------------|
| S1 | `InputSystem.settings.updateMode` 及其**枚举成员名**（`Dynamic` / `Fixed` / `Manual`） | **输入更新相位 = 渲染帧相位，且每帧恰被更新一次** | §Tuning Knobs 一之三（符号名）· `AC-3-B2③`（**只断性质**） | 同一渲染帧内输入采样计数恰 +1；相位与 `onAfterUpdate` 同相 | 判据**不改**（性质不依赖 API 形状）；仅实现指引改写 —— 若 API 改名 / 改型，本 ADR 与 GDD 的符号名就地覆写 |
| S2 | 资产副本实例化：`Instantiate()` 传副本 vs `Clone()` | **每个动作资产在输入服务内恰有一个实例**（同 `AC-3-A1`） | 规则三 / §Implementation（符号名）· `AC-3-A1`（**只断实例同一性**） | 运行时资产实例数 = 1；副本不参与 `UI` map 启用 | 判据**不改**；**驳回「`Instantiate()` → `Clone()`」的回退建议** —— 二者在仓内**同样零覆盖**，换名不减少风险 |
| S3 | `InputSystem.onAfterUpdate` 回调相位 | **采样点固定在更新相位之后、且在脚本执行顺序中不漂移**（性质：每帧恰一次且相位确定） | 规则七 · `AC-3-B1b` 的归属段（符号名） | 回调每帧恰一次；相位不随脚本顺序变 | 判据退为「采样点每帧恰一次」；若回调相位不可保证，则 `L_input→pixel` 的**相位确定性**部分**并入 10 的误差带**，并登记为本 ADR 的修订项 |

> **不在此册的符号（分层说明，防止误用）**：
> - `PlayerSettings.GetPropertyInt("activeInputHandler")`（`AC-3-A4①`）—— 引擎库零覆盖，**但其失败是「响的」**（编译 / 断言期即暴露），且本 ADR 已登记为**稳定路径**以规避枚举成员名 ⇒ **不升为 spike 项**。
> - `GetInstanceID()`（`AC-3-A1`）—— **cut-off 前长期稳定 API**，与 post-cutoff 符号**分层不同**（参 ADR-020 以「不使用 Cinemachine」消解知识风险的先例）。
> - 焦点桥本体（`InputSystemUIInputModule` / `PanelEventHandler` / `NavigationMoveEvent` / `FocusController`）—— 其 spike 册页归 **ADR-013**（`adr-013:48/51/267/368` 已登记「焦点导航原型 spike」，且已标该假设为「⚠️ 半可信」）；**本 ADR 只登记接口侧**。

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 直读通道 O(1) / 帧;动作映射 O(n 动作) | 16.6 ms 平面 / 11.1 ms VR |
| Latency | — | 急救直读通道 <50 ms(**K&M / Gamepad 两端实测** —— `⚠️ 2026-09-16` 去除 VR,承 ADR-013 §二 · P0 不做 VR) | 急救操作 <50 ms(手感预算) |
| Memory | — | 绑重 overrides 文件 + 动作资产 | 待定 |
| Network | — | 意图化(45 pipe);不逐帧同步输入 | 待定 |

## Migration Plan

**本项目尚无输入实现,故无迁移 —— 本 ADR 是「第一次就做对」。(同 ADR-005 / 010 / 001)**

1. **写动作资产 + 绑重持久化**(动作映射契约锚点)。
2. **写急救直读通道**(`InputSystem.onAfterUpdate` 回调直读,不穿 42 UI 栈)+ 两端实测 <50 ms
   (`⚠️ 2026-09-16`:`Update` 轮询**不等价**、VR 归 P1b —— 承 Amendment A 第 5 / 6 行)。
3. **写意图层**(3 / 4 / 10 输入层产出意图;P0 本地 `IEventSink`,联机 45)。
4. **写焦点导航接口**(动作 → 导航意图);R-6 呈现侧承接。
5. **撰写 3 / 4 / 10 / 42 GDD 时**,输入动作清单以本 ADR §一 为准。

**Rollback plan**:若急救直读通道实测超预算,降级路径 = **意图化(45)** 或**锁定刷新率 / 独占全屏**
(VR 站定式**不在 P0** —— VR 归 P1b),直读通道本身
**可回退**(退回 UI 栈)。但 **动作映射契约 / 意图化 / 焦点导航接口** 已进输入层,**不可回退**
(改契约 = 重写 3 / 4 / 10 输入层)。

## Validation Criteria

- [ ] **急救延迟实测**:K&M / Gamepad **两端**各 <50 ms(直读通道,不穿 UI 栈;**含渲染延迟 ≈2 帧**,F5)。
      **Amendment A**:VR **移出 P0** 判据(归 P1b);硬件实测项的签核前置 = **最低目标硬件定稿**
      (现为临时值,见 `technical-preferences.md`)。
- [ ] **联机急救手感**:丢包 / RTT 下**预表现本地即时出结果**(不等待主机),**判定与 `Append` 在主机**,
  上行**聚合意图事件 `EmergencyAttempt`**(主机结算一致)—— ~~F1~~ **Amendment B**(2026-09-18)
  ⚠️ 判据须分两预算测:`L_input`(输入→预表现,< 50 ms)与 `L_eval`(主机 `Append`→体征可见,≤ `TICK_PERIOD`),
  **不得**用「RTT 内出结果」作为验收 —— 那会把已作废的 F1 口径藏进判据(10 的 F-10.6)。
- [ ] **绑重持久化往返**:overrides 序列化 → 重载 → 逐键一致;schema hash 失配 → 优雅清空(F3)
- [ ] **意图化单测**:输入层产出意图,不直接写模拟域(P0 本地 `IEventSink` 落)
- [ ] **焦点导航单测**:导航动作 → `FocusNavigationIntent` **单向只读视图**(**不驱动焦点移动** —— 唯一真源 = 官方桥);无同键双触发(F2);**判据只断性质,不引 post-cutoff 符号名**(见 §Risks-A)
- [ ] **float 边界单测**:急救直读值不进流;判定结果**全整数直接构造 `SimEvent`**(F4;**Amendment A** —— 初稿「进流前 `FixParse`」范畴错误,已改)
- [ ] **Legacy Input Manager 零引用**:EditMode 探针(输入层禁旧输入依赖)
- [ ] `TR-concept-007`(急救 <50 ms)覆盖 —— registry 状态更新为 covered
- [ ] `TR-concept-008`(拟物 UI 手柄焦点导航)转 R-6 —— registry note 更新

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/systems-index.md` | 3 输入与设备 | 系统 3(P0 · 键鼠/手柄/VR) | 本 ADR 即其输入层权威件;意图化 + 动作映射 |
| `design/gdd/systems-index.md` | 4 交互系统 | 系统 4(P0 · 依赖 1, 3) | 意图层(`InteractIntent`);45 意图事件(联机) |
| `design/gdd/systems-index.md` | 10 急救动作 | 系统 10(P0 · <50 ms 预算) | 直读通道 + 意图化;**两端**(K&M / Gamepad)实测 |
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
