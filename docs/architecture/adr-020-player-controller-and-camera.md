# ADR-020: 玩家控制器与摄像机(CharacterController · 自建相机机位 · 平面第三人称 / VR 第一人称)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 三项核心裁定已锁(用户 2026-09-15 均照准):
> ① **移动模型 = `CharacterController`**(玩家位移 = **纯表现态**);
> ② **相机 = 自建机位**(**不引入 Cinemachine**,与 ADR-014/016/018/019 的「运行期零第三方」同向);
> ③ **视角 = 平面第三人称(越肩)+ VR 独立第一人称**(VR 站定式急救,不承担开放世界移动)。
> **本 ADR 是 R-13 的落点,并由此结清架构复核的全部 R-1…R-15 缺口。**
> **Cinemachine 的知识盲区(见 Engine Compatibility)因本裁决「不使用它」而不构成风险面。**

> **Amendment A(2026-09-16,由系统 1 的 GDD 撰写时提出)。**
> **Engine Compatibility 的『Verification Required』判据 ② 原文自相矛盾,就地订正。**
> **原文**:「② **玩家位移不进 sim**:玩家控制器程序集**不出现于任何 `IEventSink.Append` 调用点**,
> 且**不引用 sim 程序集**(门 A 方向)」。
> **问题**:该句与**本 ADR §四及 §Architecture 图**直接冲突 —— §四 明写玩家控制器**负责跨格检测与事件发出**,
> §Architecture 图画出「跨格检测 ──▶ 世界流 `ActorCellEntered`」,
> `architecture.yaml` 的 `player_cell_crossing_event` 契约更明写「跨格瞬间经 `IEventSink.Append` 发一条世界流事件」。
> 按判据 ② 的字面执行,等于**禁止玩家控制器做它被本 ADR 指定要做的事**。
> **根因**:判据 ② 的措辞与 **AC-20-05(系统 2 相机)** 逐字同构 —— 系从相机条目**复制而来**,
> 未按「1 发事件 / 2 不发事件」的差异改写。
> **订正**:判据 ② 改为「**连续位置**不出现在任何 `IEventSink.Append` 调用点;
> **跨格事件必须**出现在 `IEventSink.Append` 调用点。控制器**只**引用**边界程序集**(见下),
> **不**引用 sim 实现程序集」。
> **附注**:「不引用 sim 程序集」这一措辞在本 ADR §五(相机侧)原文已正确地写作
> 「不引用 sim **内部**类型」—— ① 门 A(`noEngineReferences: true`)是**单向**约束
> (sim 不得依赖引擎),不禁止表现层引用**契约类型**;② ADR-011 已确立 3 / 4 / 10 的输入层
> 「P0 本地 = `IEventSink` 直接落」的同型先例,ADR-021 的 6 亦然。**判据 ② 的初稿是这条既有惯例的唯一例外。**
> **本 Amendment 只订正措辞,不改任何裁决**(§一 / §四 / §五 均不动)。
>
> **Amendment A 补注(2026-09-16,同日第二项裁决 —— 边界程序集)**:
> 订正后的措辞依赖一个**此前未定义的概念**「边界程序集」。初稿(**ADR-015 §Implementation Guidelines 2** +
> ADR-005 §二)把 `WorldPos` 定为**住 sim 程序集**,而同时要求表现层**不**引用 sim 程序集
> ⇒ **表现层物理上拿不到 `WorldPos`**,与本 ADR §四 要求它发 `ActorCellEntered{cell(WorldPos)}` 直接冲突。
> 这不是措辞问题,是**程序集划分缺失**。**用户裁定(2026-09-16):开边界程序集**:
> 「边界层」**从「某个特定文件目录 / 某个特定系统的领域」重新定义为「任意两个住门 A 内外两侧的系统之间的通信契约」** ——
> **边界程序集** = `WorldPos` + 六个 P0 抽象点(ADR-005 **§Key Interfaces**:`ITickProvider` / `IEventSink` /
> `IIdAuthority` / `IVitalsQuery` + ADR-007 的 `IEventAuthority`,以及 `SimEvent` 值类型 +
> `PatientId` / `StreamId` 等整数枚举),**零 `UnityEngine` 引用**;
> **sim 实现程序集与表现层都引用它**,门 A 的 `"noEngineReferences": true` **仅约束 sim 实现程序集**
> (门 A 是**单向**约束这一点的正式落地)。⇒ 见 **ADR-005 Amendment F**。
> **本补注改的是程序集归属,不改门 A、不改 §四**。

> **Amendment B(2026-09-16,由系统 1 首轮 `/design-review` 提出)。**
> 两件事,**正文分别在 §四 与 §Key Interfaces**(就地修订,不另起节):
> ① **跨格事件的 Append 权 = 主机唯一**(客户端经第二 QoS 上行其格;
>    `ActorCellEntered` **移出可靠通道**)—— 补上原文「谁 Append」的空洞,与 ADR-005 的主机唯一性对齐;
> ② **新增 `ICameraRig.YawBasis`**(水平化正交基)供系统 1 把二维 `MoveInput` 投影成世界方向
>    (系统 1 GDD 的 F-1-8)—— **单向**:1 读 2 的基,2 读 1 的位置,无环。
> **不改 §一 / §四 的核心**(连续位置永不写流 · 跨格是唯一投影 · 上界 = tick 频率)。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 三项核心裁定**)· technical-director(起草与裁决)
· unity-specialist(引擎侧 · `CharacterController` / 相机机位 / 渲染)· ux-designer(越肩取景 · VR 舒适度)
· 系统 3 输入(`ADR-011` 动作映射上游)· 8 诊断(镜头效果消费方)· 44 音频(`AudioListener` 挂点)

## Summary

**系统 1 玩家控制器与移动 · 系统 2 摄像机与视角**是全案 **Core 层最底层**的两项
(`systems-index.md:34-35`,均 P0 / 未开始),`systems-index.md:270-271` 的依赖是
`1 ← 3`(输入)· `2 ← 1`(相机跟玩家)。架构复核把二者合并为 **R-13**
(`architecture-review-2026-09-15.md:467`),记 **GDD ❌ / 架构覆盖零 · Core · 风险 MEDIUM**,
唯一具体引擎件是「**CharacterController vs kinematic**」与「**Cinemachine 3.0 是 post-cutoff 大改版**」。

本 ADR 裁决:

① **移动模型 = `CharacterController`** —— 直接驱动胶囊位移,**不参与 PhysX 求解**;
② **相机 = 自建机位** —— 跟随 / 越肩 rig + 视角状态机,**零包依赖**,不引入 Cinemachine;
③ **视角 = 平面第三人称(越肩)+ VR 独立第一人称** —— VR 是**站定式急救**,
与平面世界移动是**两条独立路径**(`technical-preferences.md:34`)。
**并登记**:玩家位移是**纯表现态**(承 ADR-009 三态分类 / ADR-016 §三),
其在 sim 中的唯一投影是**跨格世界流事件**(`ActorCellEntered` 家族)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(玩家控制 / 表现层相机) |
| **Knowledge Risk** | **LOW** —— 本裁决**不引入任何 post-cutoff API**:`CharacterController` 是长期稳定的引擎件;自建相机只用 `Transform` / `Vector3` / 主相机 API。**Cinemachine 3.0 的知识盲区经「不使用它」而非「赌 API 名」消解** |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`(:9 LLM 知识截止 **May 2025**;`:32` URP significant upgrades)· `docs/engine-reference/unity/PLUGINS.md:21-25`(**Cinemachine** = `com.unity.cinemachine`,「3rd person games」)· `docs/engine-reference/unity/plugins/cinemachine.md:21`(**⚠️ Knowledge Gap:3.0 是 2.x 的大改写,大量 API 改名**)、`:305-323`(迁移表:`CinemachineVirtualCamera` → `CinemachineCamera` · `m_Follow/m_LookAt` → `Follow/LookAt`)· `docs/engine-reference/unity/modules/rendering.md:204-212`(Custom Camera Rendering) · `docs/architecture/adr-011-input-architecture.md`(输入上游)· `adr-016-ai-architecture.md:181-190`(§三 粗粒度整数格)· `adr-009-world-state-event-boundary.md`(三态分类)· `adr-018-audio-architecture.md:295`(`AudioListener` 挂点)· `design/gdd/systems-index.md:34-35,270-271,473` · `design/gdd/game-concept.md:32,100,757` · `design/gdd/diagnosis-system.md:1519,1641` |
| **Post-Cutoff APIs Used** | **None** —— 本裁决是「用稳定的旧件 + 自建」。**这正是选它的理由之一**:R-13 的引擎风险全部来自 Cinemachine 与被放弃的旧稿 |
| **Verification Required** | ① **相机包依赖断言**:`packages/manifest.json` **无** `com.unity.cinemachine`(与 ADR-019 的零依赖断言同法);② **玩家位移不进 sim**(**2026-09-16 Amendment A 订正** —— 原文误写为「控制器不出现于任何 `IEventSink.Append` 调用点」,与本 ADR §四 直接冲突):**连续位置 / 速度 / 朝向**不出现在任何 `IEventSink.Append` 调用点;而**跨格事件 `ActorCellEntered` 必须**出现在该调用点。控制器**只**引用**边界程序集**(见 Status 的 **Amendment A 补注**;归属裁决见 **ADR-005 Amendment F**),**不**引用 sim **实现**程序集(门 A 是**单向**约束);③ **跨格事件单一路径**:连续位置**不写流**,跨格才写 `ActorCellEntered`(grep 断言);④ VR 相机 = **世界空间第一人称**,**不接收第三人称机位**(`technical-preferences.md:34` 的自述判据) |

> **Note**:Knowledge Risk **LOW**。升级引擎版本时**只需**复核 `CharacterController` 与主相机 API
> (均是长稳件),**无需**重读 Cinemachine 面。
> **注**:本 ADR vs 报告 R-13 的措辞差异 —— 报告写「**Cinemachine 3.0 是 post-cutoff 大改版**」
> 作**风险**;本裁决把它转成**被排除项**(不采用 ⇒ 风险面清零),并如实登记其 API 差异供未来参考。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-011**(Accepted —— 输入动作映射;`CharacterController` 的移动轴与相机旋转轴由它供给)· **ADR-009**(Accepted —— 三态分类;玩家位移 = 表现态)· **ADR-016 §三**(Accepted —— 粗粒度整数格感知;玩家跨格写世界流事件,本 ADR 与它**共用同一条边界**)· **ADR-015**(Accepted —— `WorldPos` 整数格;跨格判定的格定义)· **ADR-018**(Accepted —— 44 音频 `AudioListener` 单挂点约束,§七 明确「VR 头显 vs 平面摄像机」留白由本 ADR 落定) |
| **Enables** | **系统 1 / 2 的实现**(本 ADR 是其权威件)· **`systems-index.md:473` 的设计次序第 11 步**(1 / 2 / 4)可开工 · **8 诊断的「主角视野边缘」呈现**(`diagnosis-system.md:1641` 记为 `2 摄像机 + shader`)有确定归属 · **VR 急救的相机形态**有确定路径 · **Cinemachine 从候选清单移除** |
| **Blocks** | **1 / 2 的实现** · 4 交互系统(依赖 1)· 8 的视野呈现实现。**不阻塞** 9 / 13 / 27 / 52(它们只经**世界流事件**间接依赖玩家位置) |
| **Ordering Note** | 本 ADR 是 **R-13 的落点**,**并由此结清复核的全部 R-1…R-15**。先 Accepted 本 ADR,再写 1 / 2 的 GDD 与实现。**不新增第三方依赖**,故无需 `Allowed Libraries` 登记(Cinemachine 明确**不登记**)。 |

## Context

### Problem Statement

系统 1 / 2 处于一个**与 ADR-010(7a 持久化)同构**的处境:**位于依赖图最底层,却被下游默认为已定**。

- `systems-index.md:34-35` —— 系统 **1 玩家控制器与移动** · **2 摄像机与视角**,Core / P0 / 未开始;
- `systems-index.md:270-271` —— `1 ← 3`(输入)· `2 ← 1`;
- `architecture-review-2026-09-15.md:60-61` —— 二者 **GDD ❌ / 架构覆盖「零」**;
- `architecture-review-2026-09-15.md:467` —— **R-13**,Core · **MEDIUM** · 阻塞 **1 / 2**;
- **全案零 TR**:`tr-registry.yaml` 的前缀(`TR-case / TR-concept / TR-diag / TR-disease /
  TR-itemdb / TR-randomevents / TR-skill`)**不含 player / camera / movement**。

**不裁决的代价** —— 三条:

1. **玩家位置可能被悄悄写进 sim**。ADR-016 §三 已为**敌人感知**裁决「粗粒度整数格 + 跨格写
   世界流事件,禁读表现态位置」(`adr-016:181-190`),但**玩家侧的同一条边界从未成文** ——
   若 1 的实现把连续位移写进事件流(「反正要同步」),就会造出**高频真源**,
   违反 ADR-009 的有界性论证,并让 ADR-016 §三 的单向链失效。
2. **Cinemachine 会以「官方包」为名溜进运行期**。R-13 把它记为「**post-cutoff 大改版**」= 风险;
   而本项目的 `PLUGINS.md:21-25` 把它列为生产就绪 —— **它会被当成默认选择**。
   但 ADR-014 / 016 / 018 / 019 已连续四次裁定「运行期零第三方」,**相机是第五处同类裁决**;
   若不显式裁定,**一致性检查会在这里首次失败**。
3. **视角模式有内部矛盾未收口**。`game-concept.md:32/757` 锁「**全程第三人称(越肩)**」,
   而 VR 模式**本质上是第一人称头显**;`technical-preferences.md:34` 又写明「VR 不承担开放世界移动」。
   三者并存时,**1 / 2 的实现者无法知道 VR 到底该挂哪种相机**。

### Current State

- **两项均零 GDD、零 ADR、零 TR**,`未开始`;`systems-index.md:473` 把它们排在设计次序**第 11 步**(Core · M)。
  > ⚠️ **2026-09-21 历史注**:「零 GDD」是本 ADR(2026-09-15)时的状态;1 / 2 后已立
  > `player-controller-and-movement.md` / `camera-and-viewpoint.md`(git 落盘 2026-09-19)。
  > 本段是立约前状态记录,不删;「零 ADR」自本 ADR Accepted 起即不再成立,同此。
- **§8 高风险表未列 1 / 2**(`:509-522` 只列 9 / 25 / 8 / 42 / 44 / 53 与 P0 整体)——
  即:风险被**低估**了(它们既不在高风险表,也不在任何 ADR)。
- **视角已由 `game-concept` 锁定**:`:32` `| **视角** | **全程第三人称**(越肩) |`;`:757` 同口径;
  `:100` 急救动作「(第三人称;**VR 可选**)」。
- **旧稿的第一人称微观已被推翻**:`design/archive/大医精诚-初稿-GDD.md:13/53/54/124/125`
  曾定「越肩 + 第一人称微观(手术 / 制药)」,`:124-125` 记录用户改口为「两条成长线共用一套操作」
  「急救动作改为可选」⇒ **P0 只有一种视角形态(第三人称)**,第一人称**不用于平面模式**。
- **Cinemachine 的参考件已在库**(`plugins/cinemachine.md`),但 `:21` 自陈
  「**3.0 是 2.x 的大改写,大量 API 改名**」;`Allowed Libraries` 段仍为
  「**[None configured yet]**」—— **Cinemachine 从未被批准**。
- **相机已被两个下游系统默认存在**:
  `diagnosis-system.md:1641`「主角视野边缘 | **2 摄像机 + shader**」·
  `diagnosis-system.md:1519`「**VR 下任何镜头效果**(9 已禁)」·
  `adr-018:295`「单 `AudioListener` 挂点(**VR 头显** vs 平面**摄像机**)」。
- **`docs/registry/architecture.yaml:297`** 已把「表现层运动」列入表现层产物
  (`NavMeshAgent` · `Animator` · 音频)—— **暗示玩家运动同属表现态,但未成文**。

### Constraints

- **运行期零第三方**(ADR-014 / 015 / 016 / 017 / 018 / 019 六连取向)—— 本 ADR 是**第七处一致性检查**。
- **门 A**:sim 程序集 `"noEngineReferences": true`(ADR-005)—— 玩家控制器**必须住表现层**,
  不得进 sim 程序集。
- **ADR-016 §三 已成文**:敌人感知 = 粗粒度整数格;玩家侧**必须对称**,否则单向链断裂
  (`adr-016:38-39`:「玩家跨格写世界流事件,禁读表现态位置」)**—— 这条边界是既有裁决,
  本 ADR 只负责在玩家侧落实,不是新立**。
- **`technical-preferences.md:34`**:「**VR 模式不承担开放世界移动(晕动症),只做站定式急救操作**」。
- **`game-concept.md:32`**:「全程第三人称(越肩)」—— 平面模式视角已定。
- **数值由用户手调**(`feedback-user-owns-balance-values`)—— 相机阻尼 / 越肩偏移 / 移动速度
  **全部留白**,本 ADR 只给**形状**。
- **P0 工期基线 6–9 个月**(已钉死,不再重算)—— 自建相机的成本必须落在这个窗口内(见 Risks)。

### Requirements

- 1 / 2 必须有**权威件**定义:移动模型、视角形态、相机边界。
- 玩家位移必须**明确是表现态还是模拟态**,且**不得悄悄扩大确定性面**。
- 相机必须**不持有游戏状态**(承 C3 / ADR-013 / ADR-018 的「只渲染」体例)。
- 必须给出**VR 与平面两条相机路径**的确定答案(消解「全程第三人称 vs 头显第一人称」)。
- 必须**显式裁定 Cinemachine** —— 要么采纳并登记依赖 + 冻结 API 面,要么排除并说明理由。
- **不引入运行期第三方**;**不破坏门 A**;**不改 ADR-009 / 016 的裁决**。

## Decision

**裁决:移动 = `CharacterController`(纯表现态位移);相机 = 自建机位(零包依赖,不引入 Cinemachine);
视角 = 平面第三人称越肩 + VR 独立第一人称;玩家在 sim 中的唯一投影 = 跨格世界流事件。**

### 一、移动模型 = `CharacterController`

**玩家位置由 `CharacterController` 直接驱动,不参与 PhysX 求解。**

| 项 | 裁决 |
|----|------|
| 载体 | **`CharacterController`**(胶囊直接位移) |
| 物理 | **不参与** PhysX 求解(无 Rigidbody 拖拽);与物理世界交互(推箱 / 被击退)**按需单独处理**,不靠刚体传动 |
| 手感 | 接地 / 斜坡 / 台阶内置;速度 / 加速度 / 转向速率**全部是旋钮**,数值留白 |
| 确定性 | **无关联** —— 玩家位移不进 sim(见 §四) |

**为什么不是 kinematic Rigidbody**:kinematic 的价值是**与物理世界自然交互**(推箱 / 被击退 /
载具)。但本作 P0 的玩家移动**不与物理世界耦合**(建造是整数格邻接判定,ADR-015 §五;
医馆「红石逻辑」非物理),引入刚体只会换来**更难调的阻尼与固定步长**。
`CharacterController` 是开放世界角色移动的标准解,且**最容易给出「手稳」的移动手感** ——
与 ADR-011 的 `<50 ms` 输入预算同向。

### 二、相机 = 自建机位(不引入 Cinemachine)

**相机由项目自建:跟随 / 越肩 rig + 视角状态机。零包依赖。**

| 项 | 裁决 |
|----|------|
| 制品 | **自建** `FollowRig`(带阻尼的跟随锚)+ `ShoulderRig`(越肩偏移)+ **视角状态机** |
| 依赖 | **零** —— `packages/manifest.json` **不含** `com.unity.cinemachine` |
| 与 Cinemachine 的关系 | **不采用**。理由见下 |

**为什么排除 Cinemachine** —— 四条,任一条单独成立:

1. **一致性**:ADR-014(零 JSON 解析器)· ADR-015(零第三方几何工具)· ADR-016(零第三方行为树 /
   寻路库)· ADR-017(零 DOTS)· ADR-018(零音频中间件)· ADR-019(零分析 SDK)——
   **六连取向**。相机是第七处。**「官方包」不是豁免理由**(DOTS 也是官方的,同样被结构性排除)。
2. **知识风险**:`plugins/cinemachine.md:21` 自陈 **3.0 是 post-cutoff 大改写,大量 API 改名**
   (`:305-323`:`CinemachineVirtualCamera` → `CinemachineCamera` · `m_Follow/m_LookAt` → `Follow/LookAt`)。
   引入它 = 把 MEDIUM 知识风险搬进**最底层的 Core 系统**,而**收益只是一个跟随相机**。
3. **收益/成本**:本项目需要的相机行为是 **跟随 + 越肩 + 若干状态切换**(行走 / 急救 / 脉案)。
   这是**几百行**的量级;而引入包要付:依赖登记 · API 冻结 · post-cutoff 实测 ·
   与 VR/URP 的交互验证。**成本高于自建**。
4. **VR 面**:VR 的头显相机**不能**由第三人称机位驱动(§三),Cinemachine 的第三人称件
   在 VR 侧**用不上** —— 引入它为平面模式服务,却给 VR 侧带来一条**必须绕开的旁路**。

> **登记**:`technical-preferences.md` 的「候选但未采纳」栏应记
> 「Camera Cinemachine → **不引入**(ADR-020:自建机位;运行期零第三方第七处一致性)」。
> **这不是「暂缓」,是排除** —— 触发复评的条件应由用户显式提出(与 ADR-017 的复评门不同,
> 本项**不预设复评门**)。

### 三、视角 = 平面第三人称(越肩)+ VR 独立第一人称

| 模式 | 视角 | 移动 | 依据 |
|------|------|------|------|
| **平面(P0 主形态)** | **第三人称越肩** | 开放世界移动(§一) | `game-concept.md:32/757` 已锁 |
| **VR(急救可选模式)** | **第一人称(头显)** | **站定式,不移动** | `technical-preferences.md:34` 已锁 |

**这两条是独立路径,不互相迁就** —— 消解了「全程第三人称 vs 头显第一人称」的表面矛盾:
`game-concept.md:32` 的「全程第三人称」**限定平面模式**(旧稿的「越肩 + 第一人称微观」
已被推翻,见 Current State);VR 侧的「第一人称」**不是视角选项,是头显的物理事实**。
**P0 不实现 VR**(`game-concept.md:682`),故本表右列在 P0 只落**接口**,实现推 **P1a**
(与 ADR-013 §二 · ADR-018 §七「VR 音频推 P1a」**同批**)。

> **⚠️ 2026-09-16 层级消歧**:本段的「P1a」指 **VR 相机路径的适配层级**;
> **VR 这个功能本身在 P1b**(`game-concept.md:720` 范围阶梯)—— 两者不同。
> 原引「ADR-013 的『VR / world-space 推 P1a』」**已随 ADR-013 §二 同批修订**
> (其 world-space **最小面**已提到 P0)。

**视角状态机(P0)**:`探索(越肩) → 急救(近景) → 脉案(俯视/固定) → 探索`。
状态名与切换条件是形状;转场时长 / 阻尼 / 偏移量**全部留白**(数值用户调)。

### 四、玩家位移的确定性边界(核心)

**玩家位移是纯表现态;它在 sim 中的唯一投影是「跨格」。**

```
   玩家控制器(表现层)                        sim(门 A)
  ┌────────────────────────┐              ┌──────────────────────────────┐
  │ 连续位置 · 速度 · 朝向  │   ✗ 不写流   │                              │
  │ (CharacterController)  │──────────────▶│  (sim 不见这些量)            │
  │                        │              │                              │
  │ 格坐标 (WorldPos)      │   ✓ 跨格才写 │  世界流:ActorCellEntered      │
  │ 跨格检测               │──────────────▶│  {actor_id, cell, tick}      │
  └────────────────────────┘              └──────────────────────────────┘
        │                                            │
        │ 连续位置走 ADR-001 第二 QoS                │ 13 / 27 读粗粒度整数格
        │ (unreliable latest-value,表现层插值用)     │ 作感知输入(ADR-016 §三)
        ▼                                            ▼
   表现层平滑 / 相机跟随                        敌人决策(确定性函数)
```

**这不是新裁决,是 ADR-016 §三 在玩家侧的对称落实。** `adr-016:38-39` 已写:
「**感知输入是粗粒度整数格**(玩家跨格写世界流事件,**禁读表现态位置**)」。
本 ADR 补的是**发出侧的定义**:**玩家控制器负责跨格检测与事件发出**,
且**连续位置永不写流**。

三条后果:

1. **事件率上界 = tick 频率**,与帧率 / 移动速度**无关**(`adr-016:184-186` 同口径)⇒
   世界流的**有界性论证**(ADR-009)不被破坏。
   ⚠️ **2026-09-16 口径订正(系统 1 的 GDD 落盘时)**:原写「事件频率 = **格穿越率**」是**等值**断言,
   **不可证伪**且与归并算符相抵 —— 跨格检测**每帧**跑,而提交**每 tick**一次,
   tick 内后续跨格**覆盖**待发值 ⇒ 帧率 < tick 频率时同 tick 两次跨格被归并,等值不成立。
   **正确口径 = 上界**(每 tick 至多 Append 一条),判据分两层(`player-controller-and-movement.md`
   **F-1-1b** / `AC-1-03`):①单元 —— 同 tick 喂 N ∈ {1,8,64} 样本断言 `Append == 1`
   且载荷格 == 第 N 样本的格;②集成 —— `Append 总数 ≤ tick 数` ∧ **逐 tick** 核对
   「`Append` 的格 == 该 tick 边沿的 `pending_cell`」。
   ⚠️ **2026-09-16 二次订正**(系统 1 复审):原 ② 的合取项 `≤ 相异格数` **已删除** ——
   与 EC-3(回访已到过的格**再发一条**)直接矛盾,以集合大小为界会把正确实现判失败;
   正确的不变量是**序列**性质的(相邻不同格的转移数)。
   ⇒ **有界性论证依赖的正是这条上界**(不是等值);归并算符一旦被移除,上界退化为帧率,论证即**静默**失效。
2. **连续位置走第二 QoS 通道**(ADR-001)—— 它是**表现层**事实,到达时序不确定,
   **禁用于任何 sim 决策**(`adr-016:187-190`)。
3. **单向**:玩家控制器**写事件**,13 / 27 **读事件** —— 控制器**不引用** AI,
   AI 也**不引用**控制器(它们与 ADR-016 §六 的 `IPresentPatients` 并列,都是**只写 / 只读**关系)。

> **Amendment B(2026-09-16,由系统 1 首轮 `/design-review` 提出)。**
> 本节原文只说「玩家控制器**负责跨格检测与事件发出**」,却**从未指定联机时由哪台机器 Append** ——
> 这与 ADR-005「**主机唯一执行 `Step` / `CatchUp`**」冲突(事件流的唯一写入者只能是主机),
> 与本 ADR 的「跨格检测每台机器都在跑」也不自洽。**四处裁决,均经用户 2026-09-16 照准**:
>
> ① **Append 权 = 主机唯一。** 本地玩家在主机上 ⇒ 主机本地控制器直接 `Append`;
>    本地玩家在客户端上 ⇒ 客户端**不** `Append`,把自己的格经 **ADR-001 第二 QoS**
>    (unreliable latest-value)上行,由**主机的权威控制器**统一 `Append`。
> ② **`ActorCellEntered` 移出「可靠通道」。** 它是**最新值语义**(只需最后一个格对),
>    与 reliable-ordered 语义冲突;且可靠通道被「急救 < 50 ms」(`TR-concept-007`)占用,
>    跨格事件(每 tick 至多一条 × 四名玩家)不值得抢。**世界流的可靠有序由主机 Append 之后天然获得**。
> ③ **远端队友的格**走同一条上行通道 ⇒ 主机 Append 后**经世界流回播**给全员。
>    ⇒ 13 / 27 在**所有机器**上读到**同一条事件序列**(ADR-016 §一「第四来源」禁则不被破);
>    系统 1 的 `OQ-1-9` 就此结案(落点 = 45 的 GDD,登记义务 `O-4`)。
> ④ **本地预测与主机权威有迟滞差** —— 那是表现态的**正常代价**(与连续位置同源),**不写流**。
>
> **本节裁决 ①②③ 不改 §四 的核心**(连续位置永不写流、跨格是唯一投影、频率上界 = tick 频率);
> **只补上了原文留下的「谁 Append」空洞**。⇒ 系统 1 GDD 的 **R5** / **EC-16** / **`AC-1-30`** 据此而定。
>
> **本 Amendment 同时新立一条接口**(Key Interfaces 已就地补):**`ICameraRig.YawBasis`** ——
> 系统 1 把二维 `MoveInput` 投影成世界方向所必需的**水平化正交基**。
> **方向是单向的**:1 读 2 的 yaw basis(上游值),2 读 1 的 `Position`(下游值)——
> **两条边方向相反,不构成类型环**。原文 §四 的「控制器**不引用** AI」纪律不涉及 2;
> 本节明确:**1 ↔ 2 之间无循环依赖**(系统 1 GDD 的 **F-1-8** / **`AC-1-35`** ③)。

### 五、相机是表现层 —— 只读,永不持有游戏状态

**承 C3(`systems-index.md:531` · 42 只渲染)· ADR-013 · ADR-018 §一 的同构体例。**

- 相机**只读**呈现所需的数据(`VitalsDto` 等),**不持有**任何游戏状态;
- 相机**不写三流**、**不引用 sim 内部类型**(同 ADR-019 §三 对 51 的约束);
- 相机的状态(当前视角档 / 阻尼值 / 偏移)**是表现态**,崩溃 / 重启不影响任何游戏事实。

**AC-20-05**:相机程序集**不出现于任何 `IEventSink.Append` 调用点**,且**不引用 sim 程序集**。

### 六、镜头效果与诊断的边界(结清 8 的既有引用)

`diagnosis-system.md:1641` 已把「**主角视野边缘**」记为「**2 摄像机 + shader**」的产物;
`:1519` 又写「**VR 下任何镜头效果**(9 已禁)」。本 ADR 落定归属:

| 项 | 裁决 |
|----|------|
| **镜头效果的归属** | **8 诊断拥有的「语义」 + 2 摄像机/shaders 的「实现」** —— 与 8 / 42 的既有分工同构(V-8.1 词表归 8,渲染归 42) |
| **平面模式** | 允许**低强度**视野效果(边缘收窄等);强度 / 阈值**留白**(数值用户调) |
| **VR 模式** | **禁用**任何镜头效果(V-8.7 / `:1519`)—— VR 的舒适度优先于呈现;`VRComfort` 快照承载(与 ADR-018 §七 同批,P1a) |
| **无提示音铁律的视觉对应** | 镜头效果**不得**用于「报」状态(如「病人转危」时闪一下)—— 与 ADR-018 §六 的无提示音铁律**同源**:只允许**行为反馈**,不允许**状态播报** |

### 七、与既有 ADR 的关系(无修订,纯承接)

| 既有 ADR | 关系 |
|----------|------|
| **ADR-011**(输入) | **上游** —— 移动轴 / 相机旋转轴由 ADR-011 的动作映射供给;本 ADR **不改**其任何裁决 |
| **ADR-016 §三**(感知) | **同一边界的两侧** —— 16 定义**读方**(敌人读粗粒度格),本 ADR 定义**写方**(玩家跨格发事件)。**双向不冲突,互为充要** |
| **ADR-009**(三态分类) | **承接** —— 玩家位移 = **表现态**;跨格事件 = **模拟态**;两者分属不同态是 ADR-009 判据的直接应用 |
| **ADR-015**(`WorldPos`) | **承接** —— 跨格判定的「格」= ADR-015 §三 的单一整数格 |
| **ADR-018**(音频) | **下游** —— `adr-018:295` 的「单 `AudioListener` 挂点(VR 头显 vs 平面摄像机)」由本 ADR **落定**:平面挂**主相机**,VR 挂**头显** |
| **ADR-013**(UI) | **并列** —— 13 定 UI 栈,本 ADR 定相机;**共用「只渲染不持状态」体例** |
| **ADR-017**(DOTS) | **无交互** —— 相机是表现层;ADR-017 §三 的表现层复评门**不覆盖**相机(相机不涉及群集 / 批处理) |

**⚠️ 本 ADR 不修订任何既有 ADR。** 它填补的是一个**从未成文的位置** —— 依赖图最底层的两项。

### Architecture

```text
                    ┌──────────────────────────────────────────┐
   系统 3 输入      │  系统 1 玩家控制器与移动(表现层)        │
   (ADR-011)  ────▶│   CharacterController · 移动轴 / 旋转轴   │
   动作映射         │   ✗ 不参与 PhysX   ✗ 不进 sim             │
                    └───────────┬───────────────────┬──────────┘
                                │                   │
              连续位置 / 速度  │                   │  跨格(WorldPos)
                                ▼                   ▼
                    ┌───────────────────┐   ┌──────────────────────────┐
                    │ 系统 2 摄像机     │   │ 世界流(ADR-009)          │
                    │ 自建机位(非      │   │ ActorCellEntered         │
                    │ Cinemachine)      │   │ (低频:上界 = tick 频率) │
                    │ 视角状态机        │   └───────────┬──────────────┘
                    │ ✗ 不持游戏状态    │               │ 只读
                    └────────┬──────────┘               ▼
                             │                  ┌──────────────────────┐
                             │ 挂 AudioListener │ 13 / 27 AI(ADR-016)  │
                             ▼                  │ 读粗粒度整数格作感知 │
                    ┌──────────────────┐        └──────────────────────┘
                    │ 44 音频(ADR-018)│
                    │ 主相机 / VR 头显 │
                    └──────────────────┘

   平面模式:第三人称越肩(移动)      VR 模式(P1a):第一人称头显,站定不移动
   ────────────────────────────────  ──────────────────────────────────────
   两条独立路径;P0 只实现平面;VR 只落接口
```

### Key Interfaces

```csharp
// ── 系统 1:移动(表现层;不引用 sim,不进流) ─────────────────────────
public interface IPlayerMotor
{
    void Tick(float deltaTime, in MoveInput input);   // 由 ADR-011 的动作映射供给
    WorldPos Cell { get; }                            // ADR-015 整数格(跨格判定的格)
    Vector3  Position { get; }                        // 连续位置 —— 表现态,永不写流
    bool     ConsumeCellChanged();                    // 跨格检测(真值时发一条世界流事件)
}

// ── 跨格事件:玩家在 sim 中的唯一投影(承 ADR-016 §三) ──────────────
// 事件形状沿用世界流的既有 Kind 家族;此处固定其**触发点与频率契约**:
//   · 触发 = 玩家 Cell 变化(上界 = 每 tick 一条,与帧率 / 速度无关 —— F-1-1b 归并算子)
//   · 载荷 = { actor_id, cell(WorldPos), tick }
//   · ✗ 连续位置 / 速度 / 朝向 一律不进流

// ── 系统 2:相机(表现层;只读,不持游戏状态 —— §五) ──────────────────
public enum CameraMode { Explore, Treatment, Casebook, /* VR: FirstPerson(P1a) */ }

public interface ICameraRig
{
    CameraMode Mode { get; }
    void SetMode(CameraMode mode);        // 状态机;转场参数留白(数值用户调)
    void Tick(float deltaTime);           // 跟随 / 越肩阻尼
    Camera Camera { get; }                // ← 44 的 AudioListener 挂点(平面);VR 挂头显

    // ↓ 2026-09-16 Amendment B 新增:系统 1 的移动基(单向只读消费方)
    //   把二维 MoveInput 投影成世界方向所必需的基;1 只读、2 不读 1 的移动意图。
    //   见系统 1 GDD 的 F-1-8 / AC-1-31 / AC-1-35。
    (Vector3 fwd, Vector3 right) YawBasis { get; }   // 水平化并正交归一;y 分量恒为 0
}

// ── 视角状态机(P0 形状;切换条件与转场时长留白) ─────────────────────
//   Explore(越肩) ──▶ Treatment(近景) ──▶ Casebook(固定) ──▶ Explore
```

### Implementation Guidelines

1. **不要给玩家位移加 `Rigidbody`。** 见 §一;若日后需要「被击退 / 推箱」,单独处理该交互,
   不把整条移动路径改成物理驱动。
2. **不要引入 Cinemachine。** `packages/manifest.json` 出现 `com.unity.cinemachine` 即 AC-20-02 失败。
3. **相机不进 sim、不写三流。** 它是表现层件(§五)。
4. **连续位置永不写流。** 只有**跨格**才发事件;这是 §四 的核心,也是 ADR-009 有界性的前提。
5. **VR 不用平面机位。** 头显第一人称是物理事实,不由第三人称相机驱动(§三)。
6. **镜头效果不得用于报状态。** 与 ADR-018 的无提示音铁律同源(§六)。
7. **全部数值留白。** 移速 / 阻尼 / 越肩偏移 / 转场时长 —— 本 ADR 只给形状。

## Alternatives Considered

### Alternative 1: `CharacterController` + 自建机位(本裁决)

- **Description**:胶囊直驱移动(不参与物理);自建跟随 / 越肩 rig + 视角状态机。
- **Pros**:零包依赖(第七处一致性成立);无 post-cutoff 风险;成本可控(相机行为量级小);
  与 ADR-016 §三 的粗粒度格边界天然契合(我们本来就控制位移的每一处)。
- **Cons**:自建相机的**打磨**是手工活(舒适度 / 碰撞回避 / 越肩遮挡)—— 需 `ux-designer` 介入。
- **Estimated Effort**:小–中(M 档,`systems-index.md:473` 已如此估)。
- **Rejection Reason**:**采纳**。

### Alternative 2: kinematic Rigidbody + Cinemachine 3.0

- **Description**:走官方包,用 `CinemachineCamera` + Third Person Follow / FreeLook。
- **Pros**:开箱即用;`PLUGINS.md:21-25` 列其为生产就绪;Community 大,调参文档多。
- **Cons**:① **破六连取向**(引入运行期第三方);② **Cinemachine 3.0 是 post-cutoff 大改写**
  (`plugins/cinemachine.md:21`),把 MEDIUM 知识风险搬进最底层 Core;③ kinematic Rigidbody 的
  价值(物理交互)在本作 P0 用不上,却换来更难调的手感;④ VR 侧用不上其第三人称件。
- **Estimated Effort**:小(接入)但**中**(API 冻结 + 实测 + 依赖登记 + VR 旁路)。
- **Rejection Reason**:**收益是一个跟随相机,成本是一处结构性与一致性的破坏**。

### Alternative 3: `CharacterController` + Cinemachine 3.0

- **Description**:移动自建,只借用官方相机包。
- **Pros**:移动手感与 §一 同;相机免自建。
- **Cons**:仍破零第三方取向;仍需冻结 post-cutoff API 面。**「只借相机」不改变取向问题** ——
  ADR-017 已就 DOTS 裁定「只引 Jobs / Burst」同样不成立(门 A 不看规模只看引用集)。
- **Rejection Reason**:**与 Alt 2 同类,只是拆得更小**。

### Alternative 4: 第一人称(平面模式)

- **Description**:平面模式也走第一人称(旧稿曾含「第一人称微观」)。
- **Pros**:沉浸感;VR 侧可复用同一套相机。
- **Cons**:① **直接违反** `game-concept.md:32` 已锁的「全程第三人称(越肩)」;
  ② 旧稿的第一人称微观**已被用户推翻**(`design/archive/大医精诚-初稿-GDD.md:124-125`);
  ③ 无血条 + 拟物 UI 的设计在第三人称下更易读(能看见自己的姿态与器械)。
- **Rejection Reason**:**违反已锁的 `game-concept` 裁决,且旧稿已推翻**。

### Alternative 5: 玩家位移进 sim(高频真源)

- **Description**:把玩家连续位置作为模拟事实写进世界流,「反正要同步」。
- **Pros**:客户端与服务端的玩家位置数据完全一致,无需第二 QoS 通道。
- **Cons**:① **违反 ADR-009 的有界性论证** —— 世界流体积随**帧率 × 玩家数**增长;
  ② **破坏 ADR-016 §三** —— 敌人感知被定义为「读粗粒度整数格」,若位置进流则该定义失去意义;
  ③ 高频事件让 ADR-012 的黄金夹具与存档体积失控。
- **Rejection Reason**:**ADR-016 §三 已反向否决同一件事**(`adr-016:333`,报告 Alternatives 同款),
  本 ADR 只是把玩家侧补成对称。

## Consequences

### Positive

- **R-1…R-15 全部结清** —— 架构复核的缺口清单归零。
- **依赖图最底层有了权威件** —— 1 / 2 的实现(与 4 交互系统)不再建立在默认假设上。
- **零第三方取向的第七处一致性成立** —— 相机不破例,「官方包」不是豁免理由。
- **确定性边界对称** —— 玩家侧(写方)与敌人侧(读方)现在成文地配套(§四)。
- **VR / 平面的矛盾消解** —— 「全程第三人称」限定平面;头显第一人称是物理事实。
- **Cinemachine 的知识风险**从 MEDIUM 降为**零**(不采用)。

### Negative

- **自建相机的打磨是手工活** —— 舒适度 / 遮挡回避 / 越肩调参需 `ux-designer` 与实测。
  缓解:相机行为量级小(跟随 + 越肩 + 状态机),且 `systems-index.md:473` 已按 M 档估。
- **放弃 Cinemachine 的成熟件** —— 若日后相机行为变复杂(过场 / Timeline 混镜),
  自建成本上升。缓解:本作 P0 无过场需求;若 P1 需要,**另开 ADR** 评估。
- **§四 的跨格事件是新增的写入点** —— 它给世界流加了一类事件(格穿越)。
  缓解:事件率**上界 = tick 频率**(远低于帧率,承 F-1-1b 的归并算符),有界性论证仍成立;
  且 **ADR-016 §三 早已假设它存在**。

### Neutral

- **`systems-index.md:473` 的设计次序**不变(1 / 2 / 4 仍在第 11 步);
  但开工前**不再欠权威件**。
- **VR 相机路径 P0 只落接口** —— 与 ADR-013 / ADR-018 §七 的 VR 延后**同批**(P1a 落地)。
- **Cinemachine 参考件留在库中** —— `plugins/cinemachine.md` **不移除**(它仍是引擎知识的一部分,
  只是本项目不采用),不构成依据。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 实现期有人「顺手」加 Cinemachine | 中 | 中(破一致性 + post-cutoff 面) | AC-20-02 包依赖断言;§二 显式登记为**排除**而非暂缓 |
| 玩家连续位移被写进事件流 | 中 | 高(破坏 ADR-009 有界性 + 016 感知定义) | AC-20-03(跨格单一路径 grep 断言)+ §四 成文 |
| 自建相机手感 / 舒适度不达标 | 中 | 中 | `ux-designer` 评审 + 实测;数值留白供调参;**第 1 个月即可做相机 spike**(与 §8 的 42 焦点导航 spike 同批) |
| VR 与平面相机共用一条路径,互相迁就 | 低 | 中 | §三 明写**两条独立路径**;VR 推 P1a,接口分层 |
| 相机持有游戏状态(违反 C3 体例) | 低 | 中 | AC-20-05(不引用 sim / 不写流) |
| 镜头效果被用作状态播报 | 低 | 中 | §六 与 ADR-018 §六 无提示音铁律同源;VR 全禁 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 移动 + 相机 rig ≈ **可忽略**(单角色 + 单相机) | 16.6 ms 平面 / 11.1 ms VR |
| Memory | — | **可忽略**(无第三方包 ⇒ 无额外运行时内存面) | 待定 |
| Draw Calls | — | 相机本身零 draw;镜头效果走 shader(不计入相机) | 待定 |
| Network | — | 连续位置 = **第二 QoS**(既有,ADR-001);跨格事件 = **低频** | — |

> **相机不在热路径的瓶颈上** —— 单相机 + 单角色是 Unity 最成熟的一类负载。
> 本裁决的性能意义是**负的**(不引入包 ⟹ 不引入额外内存 / 初始化面)。

## Migration Plan

**本项目尚无 1 / 2 的实现,故无迁移 —— 本 ADR 是这两项的「第一次就做对」。**

1. **记录裁决到 technical-preferences** —— 「候选未采纳」栏新增 Cinemachine 行(§二 登记)。
   *验证:该栏含 Cinemachine = 不引入。*
2. **1 / 2 的 GDD(或直接实现)** —— 以本 ADR 为权威件;数值旋钮按 GDD 体例留白。
   *验证:GDD 的每个机制可追溯到本 ADR 的某个 §。*
3. **包依赖断言** —— AC-20-02 进 CI(manifest 无 cinemachine)。
   *验证:故意加包时断言失败。*
4. **跨格单一路径断言** —— AC-20-03(grep:连续位置不出现在 `Append` 调用点)。
   *验证:断言生效。*
5. **相机 spike** —— 与 §8 的 42 焦点导航 spike 同批(第 1 个月);越肩遮挡 / 舒适度实测。
   *验证:spike 报告 + `ux-designer` 签署。*

**Rollback plan**:§一 / §二 / §三 均可回退(换移动载体 / 换相机实现 / 加视角,**不动其余架构**);
**§四 不可静默回退** —— 若玩家位移进流,须**另开 ADR** 并先修订 ADR-009 的有界性论证与
ADR-016 §三 的感知定义。§五 的「相机不持状态」同理。

## Validation Criteria

- [ ] **AC-20-01(BLOCKING)**:移动 = `CharacterController`;玩家控制器**不参与 PhysX 求解**
      (`packages/manifest.json` 无相机 / 物理第三方包;玩家路径无 Rigidbody 驱动)
- [ ] **AC-20-02(BLOCKING)**:`packages/manifest.json` **无** `com.unity.cinemachine`
      (自建机位;零第三方取向第七处一致性)
- [ ] **AC-20-03(BLOCKING)**:**连续位置 / 速度 / 朝向永不写流**;玩家在 sim 中的唯一投影 =
      **跨格事件**。**判据 = 反射断言**(`ActorCellEntered` 的字段类型集合 ⊆ `{int32, int64, 整数枚举}`),
      **不是 grep** —— ⚠️ **2026-09-16 订正**:原稿写「grep:连续位置不出现在任何 `IEventSink.Append` 调用点」
      是**假阳性机器** —— 跨格检测**必须**读 `Vector3` 才能算 `FloorToInt(p / LATTICE_SIZE)`(F-1-6),
      该 grep 会**误杀正确实现**;真正的判据只能是**载荷的字段类型**。(系统 1 的 `AC-1-02` 同此订正)
- [ ] **AC-20-04**:跨格事件率**上界 = tick 频率**(与帧率 / 移动速度无关);世界流有界性论证仍成立。
      ⚠️ **2026-09-16 订正**:原写「频率 = 格穿越率」是**等值**断言,**不可证伪**
      (F-1-1b 只给上界;帧率 < tick 频率时同 tick 两次跨格被归并)⇒ 改**上界**口径,
      判据分两层见系统 1 的 `AC-1-03`
- [ ] **AC-20-05**:相机**不引用 sim 程序集**、**不写三流**、**不持有游戏状态**(承 C3 体例)
- [ ] **AC-20-06**:平面 = **第三人称越肩**;VR = **第一人称头显**;两条路径**独立**,不互相迁就
- [ ] **AC-20-07**:VR 相机 = **世界空间第一人称**,**不接收第三人称机位**;
      且 VR **不承担开放世界移动**(`technical-preferences.md:34`)
- [ ] **AC-20-08**:VR 模式**禁用**任何镜头效果(`diagnosis-system.md:1519`);`VRComfort` 快照承载(P1a)
- [ ] **AC-20-09**:镜头效果**不得用于状态播报**(与 ADR-018 §六 无提示音铁律同源);归属 = 8 语义 + 2 实现
- [ ] **AC-20-10**:44 的 `AudioListener` 挂点落定 —— 平面**主相机** / VR **头显**(结清 `adr-018:295` 留白)
- [ ] **AC-20-11**:全部数值旋钮(移速 / 阻尼 / 越肩偏移 / 转场时长)**留白**,不在 ADR 内定值
- [ ] **AC-20-12**:相机 spike 已跑(越肩遮挡 / 舒适度),并与 §8 的 42 焦点导航 spike 同批
- [ ] **AC-20-13(BLOCKING · 2026-09-16 Amendment B 新增)**:跨格事件的 **`Append` 权 = 主机唯一** ——
      客户端模式下 `IEventSink.Append` 调用点数为 0(`ActorCellEntered` 经**第二 QoS** 上行其格);
      **提交态归主机** —— 客户端只上行 `pending_cell`(不上行 `last_committed_cell`),
      主机的 `last_committed_cell` 只由主机自己的 `Append` 推进(与权威格相同的上行值须**丢弃**)。
      *验证:系统 1 的 `AC-1-30`(①②③)同判据。*
- [ ] **AC-20-14(BLOCKING · 2026-09-16 Amendment B 新增)**:`ICameraRig.YawBasis` **只读** ——
      系统 1 的调用点零写入相机状态 / 变换;基**水平化且正交归一**(`y == 0` ∧ `f̂ ⟂ r̂`)。
      *验证:系统 1 的 `AC-1-31` / `AC-1-35` ④ 同判据。*
- [ ] **零第三方依赖**:`packages/manifest.json` 无新增相机 / 物理 / 输入第三方包

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/systems-index.md` | 1 玩家控制器与移动 | `:34` Core / P0;`:270` 依赖 3 | §一 移动模型 + §四 位移边界;实现有权威件 |
| `design/gdd/systems-index.md` | 2 摄像机与视角 | `:35` Core / P0;`:271` 依赖 1 | §二 自建机位 + §三 视角双路径 + §五 只读边界 |
| `design/gdd/systems-index.md` | 1 / 2 / 4 设计次序 | `:473` 第 11 步(Core · M) | §二 的成本评估使 M 档成立;开工不再欠权威件 |
| `design/gdd/game-concept.md` | 全局 | `:32/757`「全程第三人称(越肩)」 | §三 限定平面模式并保留;VR 侧以「头显物理事实」区分,不违反该裁决 |
| `design/gdd/game-concept.md` | 全局 | `:100` 急救动作「(第三人称;VR 可选)」 | §三 状态机含 `Treatment`;VR 推 P1a |
| `design/gdd/diagnosis-system.md` | 8 诊断 | `:1641`「主角视野边缘 = 2 摄像机 + shader」 | §六 落定归属(8 语义 + 2 实现);`AC-20-09` |
| `design/gdd/diagnosis-system.md` | 8 诊断 | `:1519`「VR 下任何镜头效果(9 已禁)」 | §六 VR 全禁镜头效果;`AC-20-08` |
| `docs/architecture/adr-016-ai-architecture.md` | 13 / 27 感知 | `:181-190` §三 粗粒度整数格(玩家跨格写世界流) | §四 补**写方**定义,与读方对称成文;`AC-20-03` |
| `docs/architecture/adr-018-audio-architecture.md` | 44 音频 | `:295` 单 `AudioListener` 挂点留白 | §七 / `AC-20-10` 落定:平面主相机 / VR 头显 |

> **Foundational decision** —— 本 ADR 无独立 GDD 需求(1 / 2 均无 GDD;它是 R-13 的落点)。
> ⚠️ 2026-09-21 历史注:「1 / 2 均无 GDD」为 2026-09-15 时状态(见 Current State 注);
> 「无独立 GDD 需求」作为本 ADR 的**入向需求口径**(R-13 落点)不变。
> **Enables**:1 / 2 / 4 的实现;8 的视野呈现;**R-1…R-15 缺口清单归零**;
> Cinemachine 从候选清单移除。

## Related

- **ADR-011**(`adr-011-input-architecture.md`)—— **上游**:动作映射供给移动 / 旋转轴;
  本 ADR 不改其裁决。`<50 ms` 输入预算与移动手感同向。
- **ADR-016**(`adr-016-ai-architecture.md`)—— **§三 是本 ADR §四 的另一侧**:
  它定义敌人如何**读**(粗粒度整数格),本 ADR 定义玩家如何**写**(跨格事件)。
  `adr-016:38-39/181-190/333` 已为玩家侧预留口径,本 ADR 将其落实为发出侧契约。
- **ADR-009**(`adr-009-world-state-event-boundary.md`)—— 三态分类:玩家位移 = 表现态,
  跨格事件 = 模拟态;世界流有界性论证是本 ADR §四 的约束来源。
- **ADR-015**(`adr-015-world-geometry-fixed-world-lattice.md`)—— `WorldPos` 单一整数格:
  跨格判定的「格」即此。
- **ADR-018**(`adr-018-audio-architecture.md`)—— `:295` 的 `AudioListener` 挂点留白由本 ADR 落定;
  无提示音铁律的**视觉对应**见本 ADR §六。
- **ADR-013**(`adr-013-skeuomorphic-ui-framework.md`)—— 并列的表现层框架:
  13 定 UI 栈,本 ADR 定相机;共用「只渲染不持状态」体例(C3)。
- **ADR-017**(`adr-017-dots-decision.md`)—— 零第三方取向的上一处;其表现层复评门**不覆盖**相机。
- **ADR-014 / 015 / 016 / 017 / 018 / 019** —— **六连的「运行期零第三方」**;本 ADR 是**第七处一致性检查**。
- `docs/engine-reference/unity/plugins/cinemachine.md:21,305-323` —— Cinemachine 3.0 知识盲区与
  迁移表(**不作为依据;本 ADR 裁定不采用**)。
- `docs/engine-reference/unity/PLUGINS.md:21-25` —— Cinemachine 的引擎侧定位(生产就绪 / 3rd person)。
- `docs/architecture/architecture-review-2026-09-15.md:60-61,467` —— R-13 的来源条目。
