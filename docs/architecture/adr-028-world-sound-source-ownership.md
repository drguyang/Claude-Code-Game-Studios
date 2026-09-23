# ADR-028: 世界语境声源归属 (World Sound-Source Ownership)

## Status

Accepted

> **2026-09-23 起草并转 Accepted。** 用户裁定(2026-09-23,照准):**「011 现裁,012 归 45 轮」** ——
> `TR-audio-011`(世界语境呼吸:听诊主通道的呼吸声源于世界,非 UI 层常驻音)的归属缺口
> **现在立小 ADR 清掉,赶在 Foundation 层 `/create-epics` 到 Epic 44 之前**;
> `TR-audio-012`(EndLoop 兜底)**并入 45 的 GDD 轮**(与 `QQ-14` / `OQ-10-9` 同族 ——
> 三者都是 ADR-001 窄修订的候选面),随 P1b 前的 45 轮一并裁,**本轮不动**。
> 本 ADR 只结清 `TR-audio-011` 一条 `gap`;**`TR-audio-012` 维持 `gap`**。

## Date

2026-09-23

## Last Verified

2026-09-23

## Decision Makers

dr_guyang(用户 · **2026-09-23 裁定「011 现裁,012 归 45 轮」**)· technical-director(起草与裁决)
· 44 音频(呈现层边界所有者 · 声源池归属方)· audio-director(世界语境呼吸方向)·
sound-designer(事件表 / 空间接线)· unity-specialist(引擎侧 AudioSource / 空间接线)·
42 拟物 UI(同构参照 —— 只渲染不持状态)· 6 世界与生态区(World 场景内容约束)·
45 网络层(表现态位置来源,本 ADR 不动其裁决)

## Summary

`audio-system.md` F-44.7 定义了**世界语境呼吸**的声学形状(`worldBreath = Spatial(cue, Cell, occlusion)`,
带衰减 / 遮挡低通 / 多病人分混 / 总线归属),这是玩家幻想「门后的人还在喘气」的**主通道载体**;
但**全案 ADR 对这条声源的归属零字** —— ADR-018 只画了「空间化锚点」的**数据流箭头**
(谁提供 `Cell`),从没写过**谁拥有、谁生成、谁释放承载这条声音的 `AudioSource` 对象**。
这正是 `architecture.md` §ADR Audit 反复命名的失效模式「**引用却无登记**」
(与 ADR-022 关卡工具、`CompoundTriggered` / `ActorCellEntered` 同型):判据、AC、GDD 规则都写好了,
**没有一个具名系统被指定为落点**,实现期必然各家自猜。

本 ADR 裁决:**世界语境声源 = 44 拥有的运行期表现层声源池** —— 零场景预摆(承 ADR-023 ②
零 gameplay 对象铁律 + RC-6 扫描增列),位置只读已发布锚点(`AudioCueDto.Cell`,远端 =
ADR-001 `IPositionalChannel`,与 ADR-018 §五 同源),总线 = Ambience 世界语境子通道,
生命周期 = cue 驱动(同格多病人 = N 条独立空间化声源,不合并),存在性**不进三流**
(承 ADR-009 派生态判据:表现层渲染资源不是模拟态)。**`TR-audio-011` 由此 `gap` → `covered`。**

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Presentation(音频 · 声源对象生命周期) |
| **Knowledge Risk** | **LOW** —— 裁决内容 = 对象归属与场景内容约束,非具体引擎调用;`AudioSource` 的 3D 空间化 / 衰减 / `AudioLowPassFilter` 遮挡是长期稳定 API。池化 / 场景扫描的**实现细节**须实测,但**归属裁决本身**不依赖任何 post-cutoff 行为 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `docs/engine-reference/unity/modules/audio.md`(`AudioSource` 3D 空间化 · `AudioListener` 单挂点 · `AudioLowPassFilter`)· `docs/architecture/adr-018-audio-architecture.md`(呈现层边界 · `AudioCueDto` · §五 远端空间化)· `docs/architecture/adr-023-scene-lifecycle-rendering.md`(② 零 gameplay 铁律 · RC-6 扫描增列先例)· `docs/architecture/adr-015-world-geometry-fixed-world-lattice.md`(整数格 · 禁反推)· `docs/architecture/adr-001-networking-pipe-abstraction.md`(`IPositionalChannel`)· `docs/architecture/adr-009-world-state-event-boundary.md`(派生态判据)· `design/gdd/audio-system.md` F-44.7(:414-445)· F-44.4(:356)· `AC-44-D8`(:873) |
| **Post-Cutoff APIs Used** | **None** —— 本 ADR 只定归属,不引入新引擎 API |
| **Verification Required** | ① 构建期场景扫描能识别「`AudioSource` 作为非 Boot 场景内容」(S2 spike 扩列,见 Validation Criteria);② 声源池生命周期 EditMode 测试(cue 生 = 分配 / cue 亡 = 回收,不泄漏) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-018**(Accepted —— 44 只触发只渲染、`AudioCueDto` 形状、§五 远端空间化读锚点、总线拓扑;本 ADR 是其「声源对象归属」空白的补全,不改其任何裁决)· **ADR-023**(Accepted —— ② 零 gameplay 对象铁律 + 构建期扫描,本 ADR 的「零预摆」直接复用其执法面,属已裁判据的扩列)· **ADR-015**(Accepted —— 空间锚点 = 整数格,禁从视觉坐标反推逻辑格)· **ADR-001**(Accepted —— 表现态位置走第二 QoS `IPositionalChannel`,本 ADR 不新增位置通道)· **ADR-009**(Accepted —— 派生态判据:声源对象存在性不进流)· **ADR-013**(Accepted —— 42 只渲染不持状态,44 同构) |
| **Enables** | **Epic 44**(Foundation 层 `/create-epics` 的音频单元 —— 本 ADR 结清其最后一条 `gap`,Epic 44 的 GDD Requirements 表可全绿)· **44 的声源池实现故事**(归属已定,可开工写生命周期代码)· `TR-audio-011` `gap` → `covered` |
| **Blocks** | **无**(本 ADR 不阻塞任何系统;它清的是一条已裁定「非开工阻塞」的登记缺口的**对立面** —— 既然用户裁定「现裁」,就现在给它一个归属) |
| **Ordering Note** | **`TR-audio-012`(EndLoop 兜底)不在本 ADR 裁决面** —— 用户明确裁定归 45 的 GDD 轮,与 `QQ-14` / `OQ-10-9` 同族(ADR-001 窄修订),P1b 前;本 ADR **不得**顺手把 012 也判了 |

## Context

### Problem Statement

`TR-audio-011` 的需求文本是「世界语境呼吸:听诊主通道的呼吸声源于世界(非 UI 层常驻音)」——
`audio-system.md` F-44.7 把这条声音的**声学形状**定死了(3D 空间化、衰减、遮挡低通、
总线走 Ambience 世界语境子通道、同格 N 病人分混不合并),`AC-44-D8` 也把遮挡行为写成了可测判据。
但**这条声音由哪个对象承载、这个对象归谁生成和释放,全案零字**:
ADR-018 的架构图里「空间化锚点」只画了一条从「表现态位置」指向混音器的**数据流箭头**,
回答的是「`Cell` 这个数值从哪来」,**不是**「挂这个 `AudioSource` 组件的 GameObject 归谁」。
`grep -rn "AudioSource\|声源归属\|emitter" docs/architecture/` 在 018/023 两件里只找到
「零 gameplay 对象」的一般性禁令,**从未针对音频对象点名**。

### Current State

- `audio-system.md` F-44.7(:414-445):定义 `worldBreath = Spatial(cue, Cell, occlusion)`、
  `min/max_distance`、`occlusion_lowpass`、总线归属、多病人分混规则 —— **只讲声学,不讲对象**。
- `audio-system.md` F-44.4(:356):`Cell` = 已发布锚点(整数格)→ float —— **只讲数值换算**。
- ADR-018 §五:远端空间化读 `IPositionalChannel` 锚点,44 只读不自建通道 —— **只讲数据来源**。
- ADR-023 ②:`World.unity` 零 gameplay GameObject + 构建期扫描;RC-6 已增列「相机 / `AudioListener`
  在非 Boot 场景 = 构建失败」—— **禁令是一般性的,没有点名 `AudioSource`**。
- `tr-registry.yaml` `TR-audio-011`:`status: gap`,note 记「未来 ADR 候选(供用户处置)」,
  2026-09-21 第二十六批裁定「登记不立件」—— **2026-09-23 用户改判:现裁,立件**。

### Constraints

- **44 与 42 同构:只触发 / 只渲染,永不持有游戏状态**(ADR-018 §一 · ADR-013 §9 C3)——
  声源池是**渲染资源**,不是游戏状态;它的存在 / 位置**不构成 sim 真源**。
- **零 gameplay 对象铁律**(ADR-023 ②):`World.unity` 内任何 gameplay 对象 = 构建期硬失败;
  一切游戏对象经 Addressables 事件驱动生成。场景预摆 = 第二真源,结构性禁止。
- **空间锚点 = 整数格,禁反推**(ADR-015):表现层把 `Cell` 换算成 float,不得从视觉坐标
  反推逻辑格,不得回写 sim。
- **表现态位置走第二 QoS**(ADR-001 `IPositionalChannel`):远端实体的位置经既有通道到达,
  **不新增位置通道**,不为音频另开一路。
- **存在性不进三流**(ADR-009 派生态判据):声源对象的生灭是表现层渲染资源的生灭,
  不是模拟态变化 —— 不写 `IEventSink`,不进存档,重建期由 cue 重建。
- **值域冻结**:遮挡截止频率 / 衰减距离 / 总线增益全部 = **用户自己调**
  (`feedback-user-owns-balance-values`);本 ADR 只定**归属与结构**,不定数值。

### Requirements

- 指定世界语境声源(`AudioSource` 对象)的**单一所有者**。
- 回答「这个对象**从哪来、放哪、何时消失**」,不留实现期自猜空间。
- 与 ADR-023 ② 零 gameplay 铁律**不冲突**(声源若预摆在 `World.unity` 会直接违规)。
- 与 ADR-018「44 不持游戏状态」**不冲突**(声源池必须是渲染资源,不是权威状态)。
- 位置来源**复用** ADR-001 `IPositionalChannel`,不新增通道。
- 同格多病人的分混规则(F-44.7)在对象层面**可落地**(N 个独立声源,而非共享一个)。
- 存在性**不污染三流**(不新增 `Kind`,不动 `entities.yaml`)。

## Decision

### ① 世界语境声源 = 44 拥有的运行期表现层声源池

**裁决:承载 `worldBreath` 及一切世界语境 3D 声音的 `AudioSource` 对象,唯一所有者 = 系统 44 音频。**
声源以**池(Pool)**形式运行期持有,不经场景资产、不经 Addressables 实例化到场景层级
(它们不是内容资产,是渲染资源 —— 同相机 / `AudioListener` 的归宿,住 Boot 生命周期,见 ②)。

判据:承 ADR-018 §一「44 与 42 同构:只触发 / 只渲染」—— 声源对象是**渲染资源**,
与混音总线、快照、`AudioCueDto` 解析器同属 44 的呈现层职责边界;它不是游戏状态
(不构成真源、不可感知为权威、重建期由 cue 重建),故**不违「不持状态」**,恰是「只渲染」的物化形态。

| 项 | 所有者 | 角色 |
|---|---|---|
| 世界语境 3D `AudioSource` 对象(池) | **44** | 拥有 / 生成 / 定位 / 释放(渲染资源,非游戏状态) |
| `AudioCueDto.Cell`(位置数值) | 已发布锚点的产出方(9/13 的表现层投影 · 远端 = ADR-001 `IPositionalChannel`) | 44 **只读**来源 |
| 总线 / 快照 / 混音参数 | **44**(ADR-018) | 本 ADR 不改 |
| 遮挡 / 衰减 / 增益**数值** | **用户**(值域冻结) | 本 ADR 不定值 |

### ② 零场景预摆 —— 承 ADR-023 ② + RC-6 扫描增列

**`AudioSource`(或任何音频发射器组件)作为场景内容出现在 `World.unity` / `MainMenu.unity` = 构建失败。**
与「相机 / `AudioListener` 在非 Boot 场景 = 构建失败」(ADR-023 RC-6 收紧注)**同型同批扩列** ——
承「属已裁判据的扩列,非新裁决」先例(ADR-023 :156 原文):ADR-023 ② 已立「场景预摆 = 第二真源,
构建期硬失败」的一般禁令,本 ADR 把 `AudioSource` **点名**进这张禁令清单,补上原扫描判据
未覆盖的缺口(与 RC-6 补相机 / `AudioListener` 时的动机完全一致:原禁令存在,但没点名,
扫描就会放行)。

- 声源池根节点**运行期创建**,生命周期随 **Boot**(与 `AudioListener`、混音快照同居 ——
  承 ADR-023 ③「44 的总线快照随 Boot 常驻 ⇒ 换场不重置混音」),**不入任何场景资产**。
- 执法 = ADR-023 ② 的**同一张构建期扫描清单**增列一条,进 **S2** spike 验证面
  (不新立扫描器,不新立 Tooling 故事)。

### ③ 位置真源 = 已发布锚点(`AudioCueDto.Cell`;远端 = `IPositionalChannel`)

**44 只读锚点,不自建第二位置通道,不读 sim 真值,不反推逻辑格。**

- 本地实体(病人 / 受伤实体):`Cell` 来自其**已发布的表现态锚点**(F-44.4 定义的整数格 → float 换算)。
- 远端实体:经 ADR-001 §一之二 `IPositionalChannel`(per-`ActorId` latest-value 表)——
  **复用,不新增**(与 ADR-018 §五 同源;本 ADR 不改 ADR-001 的任何裁决)。
- 禁止:44 从视觉坐标反推逻辑格(违 ADR-015);44 把声源位置回写 sim 或三流(违 ADR-009);
  44 读表现态位置做**任何判定**(承 ADR-016 §三 / ADR-018 §五「禁读其做判定」——
  声源位置只用于**渲染**,不用于 gameplay 判据)。

### ④ 总线归属 = Ambience 世界语境子通道(非 UICue,非 Stethoscope)

**世界语境呼吸走 Ambience 总线下的世界语境子通道(或独立 send)—— 逐字承 F-44.7 已定的总线归属,
本 ADR 不改数值、不改拓扑,只确认这条声音在对象层面挂到哪条总线的规则有归属。**

- **不得**复用 `UICue` 总线 —— 需求原文即「非 UI 层常驻音」,挂 UICue = 直接违反 `TR-audio-011`。
- **不得**复用 `Stethoscope` 总线 —— 承 F-44.7 原文「后者是听诊器贴耳声」;
  贴耳后切 `StethoscopeFocus` 快照是**听诊层**的路径,与世界语境层是两条不同的声音形象
  (F-44.7「两层是不同的声音形象,不是同一条声的远近」)。
- 七总线拓扑本身 = ADR-018 §三 已裁,本 ADR **零改动**。

### ⑤ 生命周期 = cue 驱动;同格多病人 = N 条独立声源

**声源的生 / 定位 / 释全部由 `AudioCueDto` 驱动,不手摆、不预热常驻、不进三流:**

```
cue 到达(44 解析,含 Cell / loop / bus) 
  → 池分配一条空闲 AudioSource,定位到 Cell 换算出的 float 坐标
    → 播放 / 接总线
      → cue 结束(EndLoop 或正常播放完毕)
        → 释放回池(不销毁 GameObject,不进场景层级变更)
```

- **同格 N 个病人 → N 条独立空间化声源,不合并** —— 逐字承 F-44.7 多病人规则;
  对象层面 = 池里取 N 条,不是一条多层混音。
- **存在性不进三流**(ADR-009 派生态):声源的生灭**不是**模拟态变化 —— 它是渲染资源的
  生灭,与「掉落物的位置表现」同构(身份进流、位置表现;此处连身份都不进流,因为 cue
  本身就是表现层事件,不是 `SimEvent`)。**零新 `Kind`,不动 `entities.yaml`。**
- **与 012 的边界**:cue 结束信号**丢失**时的兜底(`EndLoop` 兜底)= `TR-audio-012`,
  **本 ADR 不裁**(用户裁定归 45 轮);本 ADR 只定**正常 cue 生命周期**下的归属。

### ⑥ 遮挡 / 距离 / 总线增益 = 44 的调参旋钮,数值归用户

**`occlusion_lowpass` / `min_distance` / `max_distance` / 世界语境子通道增益 —— 结构归 44,
数值归用户**(值域冻结,`feedback-user-owns-balance-values`)。本 ADR 只登记这些旋钮**存在**
且归属 44 的调参面,**不定任何具体值**;`AC-44-D8`(遮挡可测性)的判据结构已由 GDD 给出,
本 ADR 不重复、不改写。

### Key Interfaces

```
// 44 的声源池(归属件,非新契约 —— 形状承 F-44.7 + ADR-018 AudioCueDto,本 ADR 只定归属)
// 池根:Boot 生命周期,运行期创建,不入场景资产(ADR-023 ③ 同居)
// 分配:cue 到达(AudioCueDto 含 Cell / loop / bus)→ 取空闲槽 → 定位(Cell → float, F-44.4)
// 释放:cue 正常结束 → 归还池;EndLoop 兜底 = TR-audio-012,归 45 轮,本 ADR 不裁
// 多病人:同 Cell N cue → N 条独立槽,不合并(F-44.7)
// 禁止:回写 sim / 写三流 / 自建位置通道 / 读表现态位置做判定(ADR-015 / 009 / 001 / 016)
```

### Implementation Guidelines

1. **场景扫描清单点名 `AudioSource`** —— ADR-023 ② + RC-6 的构建期扫描增列一条:
   `AudioSource` 作为 `World.unity` / `MainMenu.unity` 场景内容 = 构建失败(进 S2 spike 验证面)。
2. **池根挂 Boot,不挂 World** —— 与 `AudioListener` / 混音快照同居(ADR-023 ③),
   换场不销毁、不重置。
3. **`Cell` 只读** —— 声源定位只吃 `AudioCueDto.Cell` 换算结果,不接触 sim 真值、
   不反推逻辑格、不回写。
4. **生命周期单测(EditMode)** —— cue 生 = 分配 / cue 亡 = 回收,断言池不泄漏
   (反复分配释放后空闲数回归初始值)。
5. **不实现 EndLoop 兜底** —— `TR-audio-012` 归 45 轮,本批零实现。

## Alternatives Considered

### Alternative 1: 世界语境声源预摆在 `World.unity` 场景里

- **Description**:美术在 `World.unity` 对应格手动摆 `AudioSource`,与摆放装饰物同流程。
- **Pros**:工具链现成,美术直观可控。
- **Cons**:直接违 ADR-023 ② 零 gameplay 对象铁律(场景预摆 = 第二真源);且病人位置是
  动态的(由 9/13 生成、会移动),静态预摆**根本追不上** F-44.7 要求的「已发布锚点」——
  声音会和病人脱节。与 RC-6 补相机 / `AudioListener` 禁令时发现的失败模式同型。
- **Estimated Effort**:低(实现快),高(后续每个新 POI / 新病人都要手动摆,不可维护)。
- **Rejection Reason**:违 ADR-023 ② 结构性禁令;且无法满足 F-44.7 的动态锚点要求。

### Alternative 2: 归 42 拟物 UI 层(「非 UI 层常驻音」= 不归 42,此处列出以证伪)

- **Description**:既然需求说「非 UI 层常驻音」,那是否意味着它**曾经**被考虑归 42?
- **Pros**:无(这条列出是为了显式排除字面误读)。
- **Cons**:需求原文「**非** UI 层常驻音」是对 42 的**排除**,不是对 42 的归属邀请;
  且 42 永不持有游戏状态(ADR-013 §9 C3),世界语境声源是**空间化的世界对象**,
  与拟物 UI 的纸纹 / 卷轴 / 焦点导航无交集。
- **Estimated Effort**:n/a。
- **Rejection Reason**:字面误读;需求是对 42 的排除声明,不是归属候选。

### Alternative 3: 44 持有声源为「权威状态」(声源注册表进 sim / 进流)

- **Description**:把「当前有哪些活跃声源」登记为 sim 可查询的状态,甚至写进事件流。
- **Pros**:若未来有系统需要「查询某格是否有声音」,直接可查。
- **Cons**:直接违 ADR-018 §一「44 永不持有游戏状态」+ ADR-013 §9 C3;声源存在性若进流,
  变成模拟态 ⇒ 须新 `Kind`、须折叠、须存档 —— 而它本质是渲染资源,重建期由 cue 重建即可,
  进流是**无收益的复杂度**(同 ADR-009 派生态判据:不可感知为权威、非真源 ⇒ 不进流)。
- **Estimated Effort**:高(新 `Kind` + kindgen + 折叠规则)。
- **Rejection Reason**:违 44 / 42 同构边界;进流无收益,派生态判据直接排除。

### Alternative 4: 每个病人实体自带 `AudioSource` 组件(13 或 9 持有)

- **Description**:病人 GameObject 上挂 `AudioSource`,呼吸声由病人对象自己播。
- **Pros**:对象跟人走,不会脱节;实现直觉。
- **Cons**:13 是**只读消费者**(ADR-016 §六),9 住门 A 程序集(零 `UnityEngine`,ADR-017)——
  二者**物理上都不能持有 `AudioSource`**(引擎组件)。且远端病人不在本地场景,没有实体可挂。
  分混规则(F-44.7「N 个独立空间化」)要求的恰恰是**解耦于实体的独立声源**——
  声源数可以 ≠ 病人数(如循环 cue 比病人实例长寿,或同一病人多条 cue)。
- **Estimated Effort**:高(须拆 13/9 的层边界,结构性冲突)。
- **Rejection Reason**:结构性排除 —— 13 只读、9 门 A,均不能持有引擎组件;且与远端实体
  及 F-44.7 分混规则不兼容。

## Consequences

### Positive

- `TR-audio-011` 结清(`gap` → `covered`)—— Foundation 层 Epic 44 的 GDD Requirements 表
  可全绿,`/create-epics layer: foundation` 走到 44 时无未追溯项。
- 「引用却无登记」失效模式少一处(F-44.7 / `AC-44-D8` 的落点有了具名归属)。
- 零场景预摆复用 ADR-023 ② 既有执法面,不新立扫描器、不新立 Tooling 故事。
- 与 44 / 42 同构边界、三流派生态判据、整数格锚点三处既有裁决**零冲突**。

### Negative

- 声源池的生命周期管理(分配 / 回收 / 泄漏防护)是 44 实现故事的一笔工作量 ——
  比「直接摆场景」多一层抽象。
- 构建期扫描须扩列(进 S2 spike 验证面)—— spike 范围比原计划多一条判据。

### Neutral

- **`TR-audio-012`(EndLoop 兜底)维持 `gap`** —— 本 ADR 不触碰,归 45 的 GDD 轮
  (与 `QQ-14` / `OQ-10-9` 同族 ADR-001 窄修订)。
- 总线拓扑、遮挡数值、衰减曲线全部不动(值域冻结;结构由 F-44.7 / ADR-018 已定)。
- 悄无声息地扩了 ADR-023 ② 的扫描清单(点名 `AudioSource`)—— 属已裁判据的扩列,
  不是新裁决,承 RC-6 先例。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 声源池泄漏(cue 未正常结束,槽位不归还) | 中 | 中 | EditMode 生命周期单测(反复分配释放,断言空闲数回归);012 的 EndLoop 兜底若最终判「需要」,归 45 轮补 |
| 构建期扫描漏判某种音频组件形态(不止 `AudioSource`) | 低 | 中 | S2 spike 扩列时一并枚举音频组件类型(不只 `AudioSource`,含自定义发射器组件) |
| 实现期有人把声源位置误读为「可用于判定」 | 低 | 高(违 ADR-016 §三) | Implementation Guidelines 第 3 条 + 与 ADR-018 §五「禁读其做判定」同一句措辞,复用既有断言口径 |
| 012 悬置过久,EndLoop 缺口在 45 轮之前被实现期撞上 | 中 | 中 | 本 ADR 已把 012 归 45 轮写进 Ordering Note;Epic 44 的 GDD Requirements 表会显式标 012 为 `gap`,提示实现期不要顺手实现兜底 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a | 声源池 = 固定容量预分配,分配/回收 O(1),无 GC 抖动(池化本身是性能**优化**手段) | 44 的调参旋钮,不设新预算 |
| Memory | n/a | 池容量 = 用户调参(同格最大病人并发数的函数,与 `PATIENT_APPEARANCE_CAP = 24` 同量级) | 归用户 |
| Load Time | n/a | 无变化(池根随 Boot 创建,不走 Addressables) | — |
| Network | n/a | 无变化(复用 `IPositionalChannel`,零新增通道) | — |

## Migration Plan

1. **本批**:ADR-028 落盘 Accepted;`TR-audio-011` 的 `adr` 字段填 `ADR-028`、`status` 转 `covered`;
   `TR-audio-012` `status` **维持 `gap`**,note 追加「2026-09-23 用户裁定归 45 轮」。
2. **指针回填**:ADR-018 加一行「声源对象归属见 ADR-028」;ADR-023 ② 扫描清单注追加
   `AudioSource` 点名;`audio-system.md` F-44.7 加归属指针。
3. **Epic 44**:Foundation 层 `/create-epics` 走到 44 时,GDD Requirements 表 011 全绿、
   012 显式标 `gap`(归 45,非本层阻塞)。
4. **44 的实现故事**:声源池生命周期 + 构建期扫描扩列,进 S2 spike 验证面。

**Rollback plan**:本 ADR 为纯归属裁决,回滚 = 撤销归属 + 恢复 `TR-audio-011` 为 `gap` +
撤销三处指针回填。**无代码 / 数据受影响**。

## Validation Criteria

- [ ] `TR-audio-011` 的 `adr` 字段 = `ADR-028`,`status` = `covered`
- [ ] `TR-audio-012` 的 `status` 仍 = `gap`(未被本 ADR 顺手翻转)
- [ ] 构建期扫描能识别「`AudioSource` 作为 `World.unity` / `MainMenu.unity` 场景内容」= 构建失败
      (进 ADR-023 S2 spike 验证面)
- [ ] 声源池生命周期 EditMode 测试通过(反复分配释放,空闲数回归初始值,不泄漏)
- [ ] 声源定位只吃 `AudioCueDto.Cell`,代码路径零 sim 真值访问、零回写三流
      (承 ADR-018 §五 / ADR-016 §三 断言口径)
- [ ] 同格多病人 → N 条独立声源(不合并)在实现中可观察(与 F-44.7 规则对拍)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/audio-system.md` | 44 音频 | **TR-audio-011** 世界语境呼吸:听诊主通道的呼吸声源于世界(非 UI 层常驻音) | 声源对象唯一所有者 = 44(①);零场景预摆、非 UI 层(② / ④);位置只读已发布锚点(③);cue 驱动生命周期、同格 N 病人分混(⑤) |
| `design/gdd/audio-system.md` | 44 音频 | F-44.7 世界语境呼吸路径的**对象载体**(GDD 只定声学形状,未定对象归属) | 本 ADR 补全该空白:池化 `AudioSource` 归 44,总线 = Ambience 世界语境子通道(① / ④ / ⑤) |
| `design/gdd/audio-system.md` | 44 音频 | F-44.4 空间化锚点(整数格 → float)的**消费方约束** | 44 只读 `Cell`,禁反推、禁回写、禁做判定(③) |
| `design/gdd/audio-system.md` | 44 音频 | AC-44-D8 遮挡低通可测性 | 遮挡参数归 44 调参旋钮,数值归用户(⑥);判据结构承 GDD,本 ADR 不改写 |

## Related

- **上游** `ADR-018`(呈现层边界 · `AudioCueDto` · §五 远端空间化 —— 本 ADR 是其「声源对象归属」空白的补全)·
  `ADR-023`(② 零 gameplay 铁律 + RC-6 扫描增列先例 —— 本 ADR 复用其执法面)·
  `ADR-015`(整数格锚点)· `ADR-001`(`IPositionalChannel`,复用不新增)·
  `ADR-009`(派生态判据 —— 存在性不进流)· `ADR-013` / `ADR-016`(只渲染 / 只读消费者边界)。
- **结清** `TR-audio-011`(`gap` → `covered`)。
- **不在本 ADR 裁决面**:`TR-audio-012`(EndLoop 兜底)—— **归 45 的 GDD 轮**
  (与 `QQ-14` / `OQ-10-9` 同族 ADR-001 窄修订,P1b 前),**维持 `gap`**。
- **同源失效模式**「引用却无登记」:与 ADR-022(关卡工具)、`CompoundTriggered` /
  `ActorCellEntered` 同型 —— 判据与 AC 先行、具名归属缺失,本 ADR 是该模式在音频域的补全。
