# ADR-009: 世界状态的事件化边界 (World-State Event Boundary)

## Status

Accepted

> **2026-09-15 用户裁定转 Accepted。** 起草过程:四条用户裁定(三问判据 + 三态分类 · 第三条逻辑流
> 「世界流」 · 静态地形**确定性重建**(2026-09-15 **ADR-015 修订**:原「WorldSeed 逐位重建」
> → 烘焙逻辑层加载)· 掉落实体身份进流 / 位置表现)**均照准**;
> 引擎侧经 unity-specialist lean 复核(2026-09-15):**无引擎 API 依赖**,结论并入 §Risks。
> 涟漪修正(ADR-005 / 006 / 008 三流口径 + `architecture.yaml` 注册)随本 ADR 一并生效。
> 依赖本 ADR 的 TR(TR-itemdb-021 / 031 等)状态重裁与**全量复核**留给下一轮
> `/architecture-review` 独立进行 —— 本 ADR 不越权重裁。
>
> **2026-09-15 就地修订(Amendment F,承 ADR-021)**:三方复核登记洞 **H2(POI 状态无拥有者)**,
> 由 **ADR-021**(`adr-021-poi-state-ownership.md`)裁决 —— POI **定义** = 派生态 / POI **状态** = 模拟态,
> 写者 = 6 世界与生态区,新增 Kind `PoiStateChanged`。本 ADR **§二 / §三 / §六** 三处就地同步(见 Amendment F)。
>
> **2026-09-16 就地修订(Amendment G,承系统 1 的 GDD)**:追认 **`ActorCellEntered`** 为世界流 Kind
> —— 它此前**被 ADR-016 §三 / ADR-020 §四 / `architecture.yaml` 三处引用却无登记处**,
> 照 §三 白名单字面执行会被**构建期拒绝**(与 9 的 `CompoundTriggered` 同一失效模式)。
> 本 ADR **§三** 一处就地同步(9 → 10,见 Amendment G);`ADR-020` 同批出 **Amendment A**。
>
> **2026-09-17 就地修订(Amendment H,承系统 27 的 GDD)**:追加
> **`EncounterStarted` / `EncounterEnded`** 两个世界流 Kind —— 二者此前**已被系统 27 的 GDD
> (规则二十)· `entities.yaml:1948/1960` · ADR-016 §八 三处引用却未进本骨架**
> (与 `ActorCellEntered` 同型的「引用却无登记」失效模式,由 27 的首轮评审登记为 `O-27-5`)。
> 本 ADR **§三** 一处就地同步(10 → 12);**写者刻意拆成两个系统** —— 见 §三 的说明。
> **同批补注**(承系统 27 · `O-27-6`):`ActorCellEntered` 的 `Actor` 命名在 P0 只服务玩家,
> 敌人格是纯派生态不进流(同处有详述)。
>
> **2026-09-18 就地修订(Amendment I,承系统 10 的 GDD)**:追加 **三个病史流 Kind** ——
> `EmergencyAttempt` / `EmergencyTreatmentApplied`(10 写)· `DrugTreatmentApplied`(11 写)。
> **首次以「系统 GDD 追加 Kind」方式扩的是病史流**(此前六项追加全在世界流;`InjuryOnset`
> 是病史流首个非 9/52 写者,但由 25 的 GDD 登记、未走本 ADR 的 Amendment 通道)。
> 失效模式同 `ActorCellEntered`:10 的原稿称结算载荷为 `TreatmentEvent`,**那是内联元组不是具名
> Kind** ⇒ 照 9 的白名单字面执行,10 写的每一笔都被构建期拒收。**§二 域归属一处就地同步**。
>
> **2026-09-19 就地修订(Amendment J,承系统 18 的 GDD 首轮评审)**:`Craft` **不追加 Kind**
> (骨架既有),但三项就地同步:① §三 / §五 骨架注的**记源订正** ——「(21a)」→
> **「发起方 = 18 炮制 / 19 制作,求解 = 21a」**(18 才是写者;此前是「引用却无登记」的
> **归属变体**:载荷形状挂 R-2 悬置,而 R-2 的 21a 侧从未认领过 Craft 的调度语义);
> ② **载荷三位补齐已定稿**(`actor_id` / `output_instance_ids[]` / `tool_cell`,18 规则八
> R-18-A)⇒ 本 ADR 的「载荷归 R-2」对 `Craft` 一项**解除**;③ **`D-21-28` 全序键裁定 [甲] =
> 三流全序键 `(Tick, StreamPriority, Patient=None, Seq)`**(ADR-008 §一;`Seq` 主机 `Append`
> 发号,`Patient` 位取 `PatientId.None` 哨兵承 ADR-007 ④;原建议键的 `ActorId` 位被载荷字段吸收)。
> **§六 有界性不重写**:`Craft` 事件率 = 点火行为率 ≤ 每玩家每 tick 1(单炉,18 OQ-18-3 已裁 [甲]),
> 与帧率无关 —— 原论证结构自动覆盖。
>
> **2026-09-19 就地修订(Amendment K,承系统 17 的 GDD 首轮评审)**:**恢复 `ResourceHarvested`
> 的**生产者与载荷**。§二 三态表与 §七 Guidelines 4 早已把「资源点消耗」归类为模拟态 →
> 世界流 `ResourceHarvested` 且**明写写者 = 17**,但系统 17 的 GDD 首轮 `/design-review` 发现:
> 其规则二宣称「零新 Kind」、只发 `DropSpawned` + `DropClaimed` ⇒ **一个已注册的 Kind 无生产者,
> 而一份 GDD 单方改判了 Accepted ADR 的分类**(违 `coordination-rules` 5);
> 且 `DropSpawned.Payload`(`§五`)**无 `node_id` / 无 `quality`** ⇒ 17 自己的
> `gather_seq`(节点计数)与 F-17-3 的余量重算**不可实现**。
> 本 Amendment 载荷定稿(**写者 = 17 主机**)+ §六 有界性就地修正
> —— **§三 / §六 两处同步**(见 Amendment K)。
>
> **2026-09-19 就地修订(Amendment L,承系统 29 的 GDD 首轮评审)**:追加世界流 Kind
> **`PlayerDied`**(写者 = **9**,`OQ-25-1` 已裁路甲)。**失效模式 = 「引用却无登记的又一变体」**:
> 29 的死亡冷却依赖「世界流里的死亡事件」,而**全库无任何死亡 Kind** ⇒ 冷却静默失效
> (掉落经济漏洞)且空背包死亡零痕迹(掉级不可重建)。载荷 `{ actor_id, death_cell, tick }`;
> `death_cell` = 最后一条 `ActorCellEntered` 之 cell(**禁读实时物理** —— 修 29 原稿的 ADR-020 §四 违例)。
> **§二 三态表 / §三 骨架 / §六 有界性三处同步**(见 Amendment L)。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 四条裁定,均照准**)· technical-director(起草与裁决)
· network-programmer(拾取判定 / 表现态同步边界)· unity-specialist(引擎复核,2026-09-15)
· 系统 6 世界与生态区 / 17 采集 / 20 库存与物品 / 23 模块化建造 / 24 医馆即机器 / 25 格斗与武器线 GDD 作者

## Summary

ADR-005 锁定了**模拟层**的确定性(整数定点域 · 事件流唯一真源 · 主机唯一执行 Step),
ADR-008 把事件流扩为两条逻辑流(病史 / 病例)。但**开放世界本身**—— 地形、生态区、建造物、
物品掉落、拾取、资源点消耗 —— **哪些状态进流、哪些只做表现,从未被裁决**。
不裁决的代价:**「确定性」的范围在实现期被各系统各自扩大** —— 21a 已把 `IIdAuthority`
扩到 `ItemInstanceId`(D-21-26),世界状态正在悄悄进入事件流;地形 / 建造工具若被逐系统
各自要求确定性,第三方工具选型(ADR-002/003)将被迫出局或被迫确定性化,而这是
**决策空间最大、事后改最贵**的一道边界(B-7)。

本 ADR 裁决:**三问判据**(真源 / 权威 / 可感知)判定世界状态是否进流,三态分类
(**模拟态**进流 · **派生态**由外生源确定性重建 · **表现态**走网络层)划定边界;
**新增第三条逻辑流「世界流」**承载世界状态变更;静态地形 / 生态区布局 = 派生态,
**由确定性烘焙逻辑层加载重建**(**2026-09-15 ADR-015 修订** —— 原写「由 WorldSeed 逐位重建」);
掉落实体**身份进流、位置表现**。
并升格 ADR-006 Amendment C / D 至**三流口径**(Amendment E)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(确定性边界 / 事件流 / 世界状态) |
| **Knowledge Risk** | **MEDIUM** —— **2026-09-15 ADR-015 修订后降级**:静态几何改由烘焙逻辑层加载,不再经 Unity Terrain 运行期生成;**ADR-015 的几何面为 LOW**。本 ADR 残留的表现态项(PhysX 落点异机差)为 MEDIUM |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `docs/architecture/adr-005-deterministic-sim.md` · `docs/architecture/adr-006-fixed-point-boundary-contract.md` · `docs/architecture/adr-007-event-authority-and-roll-state.md` · `docs/architecture/adr-008-case-event-stream.md` · **`docs/architecture/adr-015-world-geometry-fixed-world-lattice.md`** |
| **Post-Cutoff APIs Used** | **None** —— 裁决全部为纯 C# 值类型 / 接口契约 |
| **Verification Required** | ① PhysX 落点的**异机可接受性实测**(位置为表现态,异机落点略异可接受 —— 网络层同步权威落点);② ~~地形 / 生态区布局的逐位重建实测~~ —— **2026-09-15 移交 ADR-015**(逻辑层为烘焙数据,不再有「生成」环节) |

> **Note**: Knowledge Risk **2026-09-15 由 HIGH 降为 MEDIUM**(ADR-015 落盘后)——
> 地形不再经 Unity Terrain 运行期生成,原「逐位重建」不透明性随之消除;
> 几何面的风险移至 ADR-015(LOW)。项目升级引擎版本时仍须重读本 ADR 的**三态判据**部分。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted 2026-09-13 —— 事件流唯一真源 · 主机唯一执行 Step · `SimEvent` / 抽象点)· **ADR-006**(Accepted 2026-09-14 —— `SimEvent` 形状 · Amendment C 跨流全序键 / Amendment D 真源 = 两流并集)· **ADR-007**(Accepted 2026-09-15 —— `PatientId.None` 哨兵 · `WorldSeed` 归 7a)· **ADR-008**(Accepted 2026-09-15 —— 第二条逻辑流 · 按 Kind 路由 · 跨流全序)—— 四者均须 Accepted |
| **Enables** | **R-2 / 7a 持久化**(世界流序列化 · 掉落清单重构 · 快照)· **R-9 / ADR-002 开放世界地形 → ✅ 已由 ADR-015 兑现**(世界几何定型)· **R-10 / ADR-003 建造系统网格 → ✅ 已由 ADR-015 兑现**(建造网格定型)· **R-3 / ADR-001 联机选型**(追加约束:承载**三条**逻辑流 + 表现态位置同步)· **R-14 / AI 架构**(导航格来源已定型)· 6 / 17 / 20 / 23 / 24 / 25 的实现决策 |
| **Blocks** | 6 世界与生态区 / 17 采集 / 20 库存与物品 / 23 模块化建造 / 24 医馆即机器 / 25 格斗与武器线的**世界状态部分**实现决策(掉落 / 建造 / 资源点 / 医馆状态的事件化边界) |
| **Ordering Note** | **2026-09-15 更新**:R-9 / R-10 已由 **ADR-015** 兑现(合并为一份 —— 世界几何);原「第三方工具逐位判据」随之作废(工具已不在运行期)。**不阻塞** 21a / 52 / 9 的既有实现 |

## Context

### Problem Statement

「确定性模拟」的范围是什么?ADR-005 答了**模拟层**的数学与权威模型;ADR-008 答了
**病例生命周期**的数据流。但开放世界的日常状态 —— 一棵采过的药草还在不在、掉落物
还在不在原地、谁捡走了它、医馆的炉子烧没烧、一块地板上放了什么 —— **没有权威件**。
各系统的 GDD 只能各自猜,而猜的结果是两类成本:

1. **过度确定性化**:地形 / 建造系统被迫逐位重放,第三方工具(GAIA / MapMagic 2)整体出局,
   自研成本爆炸 —— 而玩家根本不需要「两个存档的树长在逐位相同的位置」。
2. **过度放松**:位置、拾取、资源消耗全部只存本地 —— 主机迁移 / 中途加入后世界状态
   **静默分叉**,掉落物「悄悄合并 / 消失」(与 D-21-26 的 `instance_id` 漏洞同一类)。

**这是 ADR-005 系的下一个承重决策** —— 事后改最贵(报告 B-7 定为最高优先级)。

### Current State

- 21a 的 GDD 已登记 **D-21-26**(`instance_id` 权威缺契约 —— `IIdAuthority` 只有
  `PatientId Next()`,无物品 id 来源)、**D-21-27**(17 采集的 `instance_id` 铸造权)、
  **D-21-28**(`Craft` 事件全序键),均为 **NO-ADR**(TR-itemdb-019 / 020 / 021)。
- **TR-itemdb-031**(掉落实体的世界状态事件化边界)标「待 R-1 / B-7」—— 无载体。
- ADR-008 把路由扩到两流(病史 / 病例),`StreamPriority` 二值,`Seq` 发放域唯一
  `(Tick, Patient)`(ADR-006 Amendment C)。
- 掉落实体、资源点消耗、建造物状态的**边界在 GDD 与注册表中均无任何登记**。

### Constraints

- 不得破坏 ADR-005 的两项核心裁决:整数定点域、事件流唯一真源;
  不得破坏 ADR-006 Amendment B(高水位重构)与 Amendment C(跨流全序无平局)。
- `SimEvent` 形状 `{ Tick, Patient, Seq, Kind, Payload }`(ADR-006 Amendment A)**不可改动**。
- 模拟层零 `UnityEngine` 依赖(ADR-005 规则七);进流字段**禁 float**(ADR-006 §Decision 二)。
- 世界级事件用 `PatientId.None` 哨兵,不得污染 `max(patient_id)` 高水位(ADR-007 §四)。
- 对 ADR-001 的两个候选(Netcode for GameObjects / Photon Fusion)都成立。
- 事件流必须有界或有可论证的上界(ADR-005 动机;ADR-008 §六 的论证方法推广)。

### Requirements

- 必须裁决**哪些世界状态进流、哪些只做表现**,判据可辩护、可逐系统套用
- 世界状态变更必须可重放、可重构(否则权威迁移静默分叉)
- 中途加入的玩家能重建**掉落清单等模拟态**;位置等表现态从快照 / 网络层取
- ~~第三方地形工具选型(R-9)必须有**可执行的确定性判据**~~ →
  **2026-09-15 ADR-015 取代**:第三方工具移出运行期(仅编辑期辅助),该判据不再适用;
  R-9 的落点变为**世界几何定型的确定性判据**(逻辑层整数 · 视觉层不可反推 · 单格坐标)。
- 不得引入新依赖;进流坐标与载荷不得出现 `float`
- 世界流必须有界性论证(不折叠 ≠ 无界)

## Decision

**裁决:以「三问判据」判定世界状态是否进流,三态分类(模拟态 / 派生态 / 表现态)划定边界;
新增第三条逻辑流「世界流」承载世界状态变更;静态地形 / 生态区布局由烘焙逻辑层确定性重建
(派生态 —— **ADR-015 定型**);掉落实体身份进流、位置表现。以 Amendment E 升格 ADR-006
Amendment C / D 至三流口径。**

### 一、三问判据 —— 世界状态是否进流

对每一类世界状态,依次问三个问题:

| # | 判据 | 问题 | 回答「是」意味着 |
|---|------|------|-----------------|
| Q1 | **真源性** | 丢失这段历史后,能否从事件流重建?(或由**外生源** —— 种子纯函数**或版本化烘焙数据** —— 确定性重建?) | 是 ⇒ 进流(或派生态) |
| Q2 | **权威性** | 并发变更是否需要主机裁决?(两个玩家抢同一掉落 / 同一资源点) | 是 ⇒ 进流 |
| Q3 | **可感知性** | 所有客户端是否需要同一值?(决定游戏结果的归属) | 是 ⇒ 进流 |

**判定流程**:

```
Q1 可派生且不变更(纯函数,无状态)→ 派生态(不进流,加载期重建)
Q1 / Q2 / Q3 任一成立          → 模拟态(进流,主机模拟)
三者全否                       → 表现态(不进流,45 网络层同步)
```

> **为什么 Q1 单独通向「派生态」**:世界种子(WorldSeed)已是外生常数(ADR-007 §二),
> 若状态是 `(WorldSeed, 配置) → 布局` 的纯函数,它**没有历史**,无从也无需进流 ——
> 进流只会制造「与种子不一致」的第二份真相。
>
> **2026-09-15 ADR-015 修订(口径推广)**:**「派生态」的源有两类** ——
> ① 种子派生的纯函数(如 `patient_seed = hash(world_seed, patient_id)`);
> ② **版本化的烘焙数据**(世界几何 —— 见 ADR-015 §一 / §二)。
> 两者共同点:**由外生源确定性地重建,不进流,不制造第二份真相。**
> 静态地形 / 生态区属于第 ② 类(手工烘焙固定世界),**不再是「seed 的纯函数」**。

### 二、三态分类表(具名案例)

| 世界状态 | 三问 | 三态 | 承载 |
| --- | --- | --- | --- |
| 静态地形 / 生态区布局 / 植被点 / **POI 定义**(位置 / 守卫 / 类型) | 派生态 | **派生态** | **烘焙逻辑布局**(+ 动态量仍由 WorldSeed)→ 加载期重建(ADR-015 定型) |
| **POI 状态**(已发现 / 已清 / …) | Q1/Q2/Q3 | **模拟态** | 世界流(`PoiStateChanged`)—— 写者 = 6 世界与生态区(**ADR-021**);**POI 定义仍是派生态**(上行) |
| 资源点(可采集合)消耗状态 | Q1/Q2 | **模拟态** | 世界流(`ResourceHarvested`) |
| 建造物(结构落位 / 拆除 / 改造) | Q1/Q2 | **模拟态** | 世界流(`StructurePlaced/Removed/Modified`) |
| 物品实例(`instance_id` 身份集合) | Q1 | **模拟态** | 世界流(`DropSpawned/Claimed/Despawned`) |
| 掉落实体的**位置 / 物理轨迹** | 全否 | **表现态** | 45 网络层(§五) |
| 玩家 / NPC 位置与动画 | 全否 | **表现态** | 45 网络层 |
| 拾取归属(谁捡到了) | Q1/Q2 | **模拟态** | 世界流(`DropClaimed`,§七) |
| **玩家死亡**(死亡发生这一事实) | Q1/Q2 | **模拟态** | 世界流(`PlayerDied`,**Amendment L**)—— 写者 = 9(`OQ-25-1` 已裁路甲);**死亡点位置 `death_cell` 是派生态**(最后一条 `ActorCellEntered`),随事件原样携带 |
| 制作 / 炮制结果 | Q1 | **模拟态** | 世界流(`Craft`,载荷归 R-2) |

**域归属规则**(与「是否进流」正交):三问回答「是否进流」,域归属回答「落哪条流」:

- **病史类 Kind**(9 的模拟事件 + 52 的决策注记 `EventRolled` / `EventArrived` /
  `ThreatDeferred` / `ThreatDeferralCleared` / `HistoryFlagChanged`)→ **病史流**(ADR-007 既定,不改)
- **病例类 Kind**(37 的 `CaseOpened` / `CaseClosed` / `PatternRecognized` /
  `JudgmentRecorded` / `JudgmentRevised`)→ **病例流**(ADR-008 既定,不改)
- **世界状态类 Kind**(本 ADR 新增 8 个,§三;追加:`PoiStateChanged`(ADR-021)·
  `ActorCellEntered`(Amendment G)· `EncounterStarted` / `EncounterEnded`(2026-09-17,系统 27)·
  `EnemyInjuryOnset` / `InjuryStateChanged`(2026-09-17,系统 25)·
  `PlayerDied`(2026-09-19,**Amendment L**,系统 29)→ **共 15**)→ **世界流**
- **病史流的第 25 支**(2026-09-17 补):`InjuryOnset`(25 写,病人为目标;玩家亦是 PatientState)
  —— 它是**病史类**,但写者既不是 9 也不是 52:**写者 = 25**(伤害施加方),语义归 9。
  路由仍按 ADR-008 §一「Kind 纯函数」成立,不改本表纪律。
- **病史流的第 26~28 支**(2026-09-18 补,**Amendment I**):`EmergencyAttempt`(10 产出 /
  **主机物化**)· `EmergencyTreatmentApplied`(**10 写**)· `DrugTreatmentApplied`(**11 写**)
  —— 三者都是**病史类**,写者既不是 9 也不是 52,理由与 `InjuryOnset` 同(「内容归属 ≠
  `Append` 调用者」,`disease-simulation.md` 表头口径订正 2026-09-17)。
  ⚠️ **`EmergencyAttempt` 是「判定的输入」而非「结算的输出」** —— 一条动作落**两条**病史流
  事件(输入 + 结算),这在既往 Kind 里是**新形态**(§七 `PickupIntent → 主机判距 → 落流`
  三段式的对称落实:三段式此前只存在于**判据**层,从未在**流**层物化过两步)。
  路由仍按 ADR-008 §一 成立,**不改本表纪律**;有界性 = 每完成动作 ≤ 2 条,**与帧率无关**。

> 52 的世界级事件(Patient = None)**仍留病史流**:它们是**掷骰决策的注记**,不是**世界状态的
> 变更** —— 事件降临后世界如何响应(掉落 / 资源 / 剧情)才是世界流的事。二者不可混。
> 域归属一旦写错,与「病例事件落病史流」同类污染(注册表禁例 `case_events_in_history_stream`)。

### 三、第三条逻辑流「世界流」

**裁决:新增第三条逻辑流「世界流」,承载世界状态变更。`StreamPriority` 升格为三值,
`Seq` 发放域仍唯一 `(Tick, Patient)`,三流共享。**

```
enum StreamId { History, Case, World }   // StreamPriority: 病史 0 < 病例 1 < 世界 2
```

- **跨流全序键升格**:`sort(a, b) := (Tick asc) → (StreamPriority asc) → (Patient asc) → (Seq asc)`
  —— `StreamPriority` 三值化(`病史 < 病例 < 世界`),键的**形状不变**,只扩取值域
  (ADR-006 Amendment C 的既有判据自动扩展,见 Amendment E)。
- **`Seq` 发放域唯一 `(Tick, Patient)` 不变**:三流共享同一发放器;世界事件
  `Patient = PatientId.None`(ADR-007 §四),与病例流世界级事件(`PatternRecognized`)
  **共享同一 `(Tick, None)` 计数域** —— 干净扩展,不新造计数器。
- **`max(patient_id)` 重构扫三流并集**(Amendment E):世界流全部事件 `Patient = None`,
  显式排除,不污染高水位。
- **路由是纯函数**(扩展 ADR-008 §一):`Kind → StreamId`,三流白名单,主机在 Append 时执行。

**新增世界状态 Kind(骨架 —— 本 ADR 只定边界与路由,不定载荷)**:

```
StructurePlaced        // 建造物落位(23)—— 载荷归 R-10
StructureRemoved       // 拆除(23)—— 载荷归 R-10
StructureModified      // 改造 / 医馆即机器状态变更(23 / 24)—— 载荷归 R-10
DropSpawned            // 掉落实体出生:instance_id + spawn_anchor(20 / 25 / 死亡掉落)
DropClaimed            // 拾取:主机裁决的归属变更(20)
DropDespawned          // 消失 / 过期(20)
Craft                  // 制作 / 炮制 —— **发起方 = 18 炮制 / 19 制作,求解 = 21a**(Amendment J,
                       //   2026-09-19:原记「(21a)」为归属误记);载荷**已定稿**(18 规则八 R-18-A:
                       //   actor_id / output_instance_ids[] / tool_cell / start_tick / ActualConsumed[])
                       //   —— 本 Kind 的「载荷归 R-2」解除;全序键 = 三流键 [甲](D-21-28 已裁)
ResourceHarvested      // 资源点消耗(17)—— 主机裁决;载荷**已定稿**(Amendment K,2026-09-19:
                       //   instance_id / node_id / gather_seq / qty / out_quality —— 17 的 GDD)
PoiStateChanged        // POI 状态变更(6)—— 载荷 { poi_id, new_state } 归 **ADR-021**
ActorCellEntered       // 行动者跨入新整数格(1 玩家 / 27 敌人)—— 载荷 { actor_id, cell, tick } 归 **ADR-020 §四 + 系统 1 的 GDD**
EncounterStarted       // 遭遇开始(52 触发)—— 载荷 { encounter_id, proto_id, spawn_cell, actor_ids[] } 归 **系统 27 的 GDD**
EncounterEnded         // 遭遇结束(27 判定)—— 载荷 { encounter_id, reason(枚举) } 归 **系统 27 的 GDD**
EnemyInjuryOnset       // 敌人伤情发作(25 写)—— 载荷 { actor_id, target_id, injury_id, magnitude, tick, dose_seq } 归 **系统 25 的 GDD**(2026-09-17)
InjuryStateChanged     // 受伤实体状态转移(9 写;昏迷可逆)—— 载荷 { actor_id, new_state } 归 **系统 25 的 GDD 规则五命名,值域归 9**(2026-09-17)
PlayerDied             // 玩家死亡(9 写 —— OQ-25-1 已裁路甲,2026-09-19)—— 载荷 { actor_id, death_cell, tick } 归 **系统 29 的 GDD**(Amendment L)
```

> **为什么「骨架先行」**:路由与全序即时生效(三流从本 ADR 起成立),但各 Kind 的载荷
> 属于各系统的实现 ADR(R-2 / R-10)。**本 ADR 不定载荷,避免越权**(同 ADR-007 的教训)。
> **`PoiStateChanged` 是第一个以此方式追加的 Kind** —— 由 **ADR-021**(2026-09-15)补齐
> POI 状态所有权(见 **Amendment F**);原 8 个维持本 ADR 首版。
> **`ActorCellEntered` 是第二个** —— 见 **Amendment G**(**2026-09-16**):
> 它此前**已被 ADR-016 §三 / ADR-020 §四 / `architecture.yaml` 三处引用却无登记处**,
> 由系统 1 的 GDD 追认(命名用 `Actor` 而非 `Player`,为 27 敌人留口)。
> **`EncounterStarted` / `EncounterEnded` 是第三、四个** —— **2026-09-17 就地补齐**:
> 二者此前**已被系统 27 的 GDD(规则二十)· `entities.yaml:1948/1960` · ADR-016 §八
> 三处引用却未进本骨架**(与 `ActorCellEntered` 同型的「引用却无登记」失效模式;
> 27 首轮评审登记为 `O-27-5`,本轮兑现)。**写者被刻意拆成两个系统**
> (`EncounterStarted` = 52 · `EncounterEnded` = 27)—— 这是 ADR-021 §①
> 「状态所有权 = 写入权」判据在遭遇生命周期上的落实:52 决定**来不来**,
> 但「遭遇何时真的结束」是 27 的状态,52 不持有它、写不出它。
> **`EnemyInjuryOnset` / `InjuryStateChanged` 是第五、六个** —— **2026-09-17 由系统 25 的
> GDD 首轮评审修订追加**(承 ADR-016 §二「敌人伤情落世界流」—— 该条自 2026-09-15 起
> 引用这个落点却始终无 Kind 登记,同 `ActorCellEntered` 失效模式;25 的 R2/R3 兑现)。
> 与 `InjuryOnset`(病史流)拆开的原因 = ADR-008 §一:路由是 Kind 的纯函数,
> **一个 Kind 无法同时落两条流**。
> **`PlayerDied` 是第七个** —— **2026-09-19 由系统 29 的 GDD 首轮评审追加**
> (承 **Amendment L**;写者 = 9 —— `OQ-25-1` 已裁路甲。**失效模式同 `ActorCellEntered`**:
> 29 的死亡冷却依赖一条它自己假设存在、却**从未被认领**的世界流死亡事件)。

> **⚠️ 2026-09-17 补注(承系统 27 · `O-27-6`)**:`ActorCellEntered` 的 `Actor` 命名
> **在 P0 只服务玩家**。敌人的逻辑格是**纯派生态**(由三源逐位重建,**不进流**)——
> 若让敌人也发 `ActorCellEntered`,事件率上界从「玩家跨格率」变成
> 「玩家跨格率 × 敌人数」,**破坏本流 §六 的有界性论证**。命名留口 ≠ 现在就发;
> 27 若要发,须**另开 ADR**,不得以本 ADR 修订改判(同 ADR-017 §三 的复评门纪律)。

- **进流字段禁 float**:位置 / 坐标一律整数网格或 `Fix`(Q16.16);float 只在表现态
  (ADR-006 §Decision 二 的边界自动覆盖世界流)。

### 四、静态地形与生态区 = 派生态(确定性重建 —— 见 ADR-015)

> **2026-09-15 修订(ADR-015)。** 本节原写「静态地形 / 生态区布局是 `(WorldSeed, 配置) → 布局`
> 的纯函数」—— **此句已废止**:4 生态区是**手工内容**(`game-concept.md:574` /
> `concept-benchmark.md:80`),程序生成器无从「生成」手工内容。**修订为**:
> **静态地形 / 生态区布局由确定性烘焙的「逻辑整数层」在加载期重建;`WorldSeed` 只驱动动态量
> (病人种子 / 掷骰 / 采集散布 / 掉落表现)。** 世界坐标 / chunk / 建造网格的协变细节
> 由 **ADR-015** 定型。**本节下方的「生成数学必须由本工程定点域实现」判据仍然成立** ——
> 逻辑层是整数,只是它的来源从「纯函数」改为「烘焙数据」。

**裁决:静态地形 / 生态区布局由**烘焙逻辑布局**在加载期一次性重建,
不进流、不存档(7a 的缓存只是优化,不是真相)。中途加入的玩家无需传输地形 —— 重建即验。**

- **逻辑层的生成 / 编辑必须落在整数**:**逻辑几何**(生态区多边形 / POI / 资源点 / 导航格)
  一律整数,**不得含 `float`**;视觉地形(mesh / splat / 植被)是表现层,浮点合法(ADR-015 §一)。
  **第三方地形工具(GAIA / MapMagic 2)只能作编辑期素材 / 预览,不得承担运行期生成**(ADR-015 §六);
  其输出若经 Unity Terrain 内部算法(高度图 int16 量化 / splat 烘焙 / 散射)则对
  版本 / Mono-vs-IL2CPP / x86-vs-ARM 不透明 —— **逐位性不可信**。
- **R-9 判据(已被 ADR-015 取代)**:本节原预置「第三方地形工具的逐位复现能力必须实测,
  实测不过则整体出局」。**ADR-015 已把第三方工具移出运行期,该实测判据随之作废**
  (工具不再承担生成,无从「逐位」与否)。引擎复核原初步结论(MapMagic 2 可能逐位 · GAIA Pro 高概率出局)
  仅作**编辑期工具选型的背景**,不再承重。
- **Unity Terrain 是呈现载体,不是生成真相**:逻辑层是整数数据;交给 Terrain 时以定点整数写出;
  mesh 浮点是表现态,不违反确定性。**禁从视觉地形反推逻辑格**(ADR-015 §一 / Validation)。
- 生态区布局影响 52 的事件 `spawn_anchor` 解析(TR-randomevents-021)与 6 的生态区绑定
  (TR-randomevents-018) —— **判据先行**,落在**整数查表**上(满足 `random-events.md:438-439`
  的「禁 NavMesh 采样」)。

### 五、掉落实体 = 身份进流 / 位置表现

**裁决:掉落清单(身份)是模拟态,进世界流;掉落位置 / 物理轨迹是表现态,走 45 网络层。
PhysX 的非确定性因此不进入模拟域 —— 异机落点略异可接受。**

- `DropSpawned.Payload` = `{ instance_id, spawn_anchor, item_key, qty }`:
  - **`instance_id` 由 `IIdAuthority.ItemInstanceId.Next()` 发放**(确认发放点 = ADR-009;
    **机制归 R-2 / 7a** —— TR-itemdb-019);**不本地铸造**(D-21-27,17 采集同样禁止本地铸造)。
  - **`spawn_anchor` = 出生锚点,格坐标(`WorldPos`,i32 × 3),进流**(引擎复核补的边界):位置不进流 ⇒ 权威迁移
    重放时掉落物只有身份无位置 —— 锚点进流即解决「重放矛盾」。**物理轨迹 / 落点**是表现,
    从快照 / 网络层取。
    > **2026-09-15 ADR-015 §三 修订**:原文作「定点坐标」,与「空间位置一律整数格」相冲 ——
    > 空间位置**一律 `WorldPos` 整数格**;`Fix` 只用于非空间模拟量。锚点是与建筑 / 资源点 /
    > 导航格共用的同一套格。**修正在此原地完成,不单列 ADR-015 §七 清单**(该清单只列了 §一 / §二 / §四)。
- 死亡掉落(游戏概念「英灵神殿式掉落物」· skill-system 掉落物散落原地)= **表现态驱动模拟态**:
  死亡的物理表现走网络层,掉落清单经 `DropSpawned` 落地世界流 —— 玩家「回程捡尸体」时
  看到的是表现,归属记录的是模拟。
- 中途加入玩家:掉落**清单**从世界流重建,位置从 7a 快照取。

### 六、世界流不折叠 + 有界性

**裁决:世界流不物理折叠(与病例流同,ADR-008 §六)。**

- **为什么不折叠**:① 建造物的折叠行(结构类型 + 位置 + 配置)不比事件小多少;
  ② 掉落生命周期短(拾取 / 过期即 `DropClaimed` / `DropDespawned`),折叠省不下长期集合;
  ③ 医馆即机器的改写史类状态(24)与病例流的改写史同理,折叠 = 破坏回看。
- **有界性论证(与 ADR-008 §六 同构)**:
  - 世界流写入者 = 玩家动作 + 世界系统状态机;建造 / 拆除 / 制作率受**玩家操作速率**约束;
  - 掉落数 ≤ 玩家携带量上界(库存容量 —— 每实例一件,死亡一次性落地,随后只减不增);
  - 资源点 = **有限集合**(生态区布局固定),**但 `ResourceHarvested` 的条数不是**
    —— **⚠️ 2026-09-19 Amendment K 就地修正**:原写「消耗事件总数 ≤ 资源点数」是**陈旧**的
    (它隐含「采完即永久废弃」)。F-17-3 的 `RegrowWindow` 使同一节点**可再生** ⇒
    上界改为 **≤ 玩家采集动作率 × 会话时长**(每完成动作 ≤ 1 条,**与帧率无关**),
    归入「玩家有界流」项;`|资源点|` 只约束**同时可采的节点数**,不约束事件总数;
  - **POI 状态**(ADR-021 补):POI 集**有限**(手工烘焙逻辑层,ADR-015 §一)+ 状态枚举**有限**
    ⇒ 转移总数 ≤ `|POI| × |STATE|`;写者 = 6(同上「世界系统状态机」类);
  - **玩家死亡**(**Amendment L** 补,2026-09-19):`PlayerDied` 事件率 ≤ **玩家数 / `DEATH_COOLDOWN`**
    (F-29-2 的冷却使死亡离散化;**无条件发出**但受冷却闸)。归入「玩家有界流」;
    ⇒ §六 论证**扩展而非重写**(29 侧同时兑现了 `O-3` / `OQ-1-8` 的复活传送上界,见 `death-and-respawn.md` 规则五);
  - **论证链**:`世界流增长率 ≤ 玩家操作速率(建造 / 采集 / 制作)+ 有限 POI 状态数 + 库存上界
    + 玩家死亡率(≤ 玩家数 / DEATH_COOLDOWN)`,
    与病史流的病人自增率(9 配置)是**本质不同** —— 世界流是玩家有界流。
    (**⚠️ 2026-09-19 Amendment K**:原链含「有限资源点数」项 —— 再生使该项**不成立**,
    已并入「玩家操作速率」,见上。)
- **7a 定期快照 = 优化,不是真相**:快照用于加载加速与表现态位置恢复;真相永远是世界流。

### 七、拾取判定(意图事件 + 当下判距 + 宽容半径)

**裁决:拾取 = 玩家意图事件(45 网络层)+ 主机当下判距 + 宽容半径,结果确定性进流。**

- 拾取判定的**几何距离**吃的是物理位置(表现态)—— 这是引擎复核指出的**唯一模拟域污染点**:
  若判定在重放里进行,位置不可重放 ⇒ 判定不可重放。
- **解法**:拾取不重放判定。玩家发出**意图**(目标 `instance_id`,45 网络层);主机在
  **写事件当下**用实时物理位置判距(宽容半径,容忍异机落点差);通过 ⇒ 发 `DropClaimed`
  (归属变更,确定性进流)。**判定结果进流,判定过程不进流** ——
  当下裁决(权威性,符合 Q2)与确定性重放(结果可重构,符合 Q1)各归其位。
- **迁移语义**:迁移后掉落清单从流重构;位置从 7a 快照取 —— 宽容半径吸收锚点 / 落点差。

### Architecture Diagram

```
                    ┌──────────────────────────────────────────┐
                    │  7a 持久化                                │
                    │  存档头: WorldSeed · 配置版本号            │
                    │  世界流不折叠 · 定期快照 = 优化非真相(§六)   │
                    └───────────────┬──────────────────────────┘
                                    │
              ┌─────────────────────▼─────────────────────┐
              │  IEventSink.Append(in SimEvent)            │
              │  → 按 Kind 路由(三流纯函数白名单,§三)        │
              └───────┬───────────────┬───────────────────┘
                      │ 病史           │ 病例           │ 世界
                      ▼                ▼                 ▼
              ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐
              │  病史流      │  │  病例流       │  │  世界流(§三)      │
              │  9 模拟      │  │  37 病例      │  │  建造 / 掉落 /     │
              │  52 决策注记 │  │  不折叠       │  │  Craft / 资源     │
              │  终态折叠    │  └──────────────┘  │  不折叠(§六)      │
              └─────────────┘                    └──────────────────┘

  跨流全序键: (Tick, StreamPriority, Patient, Seq)  ← §三,三值 StreamPriority
  静态地形 / 生态区布局: 烘焙逻辑整数层 → 加载期重建,不进流(§四;ADR-015 定型)
  WorldSeed: 存档头外生常数,只驱动动态量(病人种子 / 掷骰 / 采集散布 / 掉落表现)
  掉落: 身份(DropSpawned/Claimed/Despawned)→ 世界流;位置 / 物理轨迹 → 45 网络层(§五)
```

### Key Interfaces

```csharp
// ── 三流路由(扩展 ADR-008 §一 / §二)──
enum StreamId { History, Case, World }   // StreamPriority: 病史 < 病例 < 世界

public interface IEventSink              // 路由纯函数:Kind → StreamId
{
    void Append(in SimEvent e);          // 病史类 → History · 病例类 → Case · 世界类 → World
}

// ── 物品实例 id(掉落 / 库存 / 建造件共用;发放机制归 R-2 / TR-itemdb-019)──
public readonly struct ItemInstanceId { public readonly long Value; }

public interface IIdAuthority            // 扩展(ADR-005 定义,TR-itemdb-019 落点)
{
    PatientId Next();
    ItemInstanceId Next();               // ← 新增:掉落 / 物品实例发放(机制归 R-2)
}

// ── 出生锚点(格坐标 WorldPos,进流;禁 float)──
public readonly struct WorldPos          // 整数格坐标(i32 × 3)—— 由 ADR-015 §三 定型
{
    public readonly int X, Y, Z;         // 单一世界格;chunk 不是坐标原点(ADR-015 §四)
}

// ── 新增世界状态 Kind(骨架,§三;载荷按系统归 R-2)──
// StructurePlaced / StructureRemoved / StructureModified   (23 / 24 → 载荷形状由 ADR-015 §五 定型)
// DropSpawned / DropClaimed / DropDespawned                 (20 / 25 → R-2 机制)
// Craft                                                   (发起/写 = 18/19,求解 = 21a —— 载荷已定稿,Amendment J)
// ResourceHarvested                                        (17)

// ── 拾取:意图(45)与结果(世界流)分离,§七 ──
// 意图:  PickupIntent { instance_id }                     (45 网络层,玩家输入)
// 结果:  DropClaimed   { instance_id, claimer, tick }     (世界流,主机裁决,确定性进流)
```

### Implementation Guidelines

1. **先判态再写系统**:每个世界系统的状态先过三问判据(§一),把结论登记进该系统 GDD 的
   §Dependencies 三态表 —— **判定留痕,实现照表**。
2. **派生态 = 由外生源确定性重建**:静态几何由**烘焙的逻辑整数层**在加载期重建(ADR-015);
   种子派生的纯函数(如 `patient_seed`)是另一类外生源;无论哪类,加载期一次性完成,
   中途加入**不传输**地形(重建即验)。
3. **世界几何的逻辑层数据化**:**逻辑几何**(生态区多边形 / POI / 资源点 / 导航格)一律整数,
   经 **ADR-015** 烘焙;视觉地形只作表现,运行期零第三方几何工具。
   ~~地形生成数学入定点域~~ → **ADR-015 修订**:不再是「生成」,而是「烘焙加载」。
4. **`DropSpawned.instance_id` 由 `IIdAuthority.ItemInstanceId.Next()` 发放**,
   **不本地铸造**(D-21-27);17 采集的 `ResourceHarvested` 同样归主机裁决。
5. **拾取走意图 + 当下判距 + 宽容半径**(§七):判定不重放,结果确定性进流。
6. **进流坐标一律整数网格 / `Fix`**:`float` 只在表现态;ADR-006 §五 的自定义编码器
   与门 B 探针**自动覆盖世界流**(无需新机制,重申纪律)。
7. **世界流不折叠**(§六);7a 定期快照 = 优化非真相。

### Amendment E —— 升格 ADR-006 Amendment C / D 至三流口径(2026-09-15)

ADR-006 Amendment C(跨流全序键)与 Amendment D(真源 = 两流并集)在 ADR-009 引入第三流后
需要**升格取值域,不改形状**:

1. **Amendment C 升格**:全序键 `(Tick, StreamPriority, Patient, Seq)` 的 `StreamPriority`
   由二值升为三值(病史 < 病例 < 世界);`Seq` 发放域唯一 `(Tick, Patient)` **三流共享不变**;
   世界事件 `Patient = PatientId.None`,与病例流世界级事件共享 `(Tick, None)` 计数域。
2. **Amendment D 升格**:真源 = **病史流 ∪ 病例流 ∪ 世界流**(三者均为权威、均可重放、
   均参与 `max(patient_id)` 重构 —— 世界流全部 `None` 事件显式排除);
   终态折叠仍只作用于病史流;病例流与世界流**永不物理折叠**。
3. **本修正案只改口径,不改任何数据形状**;ADR-006 的既有 Validation Criteria
   (发放域唯一 / 跨流全序无平局 / 扫两流并集)在语义上自动扩展为三流,取值域以本 ADR 为准。

### Amendment F —— POI 状态进世界流(ADR-021,2026-09-15)

**背景**:三方复核(2026-09-15)登记洞 **H2 —— POI 状态无拥有者**(`expansion-review-2026-09-15.md`
§五 接缝 8):本 ADR §二 三态表有「静态地形 / 生态区」行但**无 POI 状态行**,§三 Kind 骨架**不含 POI**,
`ADR-010 §三` 义务表**无对应行**。**ADR-021**(`adr-021-poi-state-ownership.md`)已裁决该洞。

**就地修订三处(本 ADR 全文已同步,此处只作索引)**:

1. **§二**(三态分类表):新增 **「POI 状态(已发现 / 已清 / …)= 模拟态 → 世界流」** 行;
   并把「静态地形」行改标为含 **「POI 定义」**(定义 = 派生态 / 状态 = 模拟态,**一分为二**)。
2. **§三**(世界流 Kind 骨架):追加 **`PoiStateChanged`**(8 → 9)—— **第一个以「系统 ADR 追加」
   方式扩骨架的 Kind**,载荷 `{ poi_id, new_state }` 归 ADR-021。
3. **§六**(有界性):追加 **POI 状态**项 —— 转移数 ≤ `|POI| × |STATE|`(POI 集有限 + 枚举有限),
   写者归入「世界系统状态机」类,**有界性论证扩展而非重写**。

**所有权**:POI 状态所有者与**唯一写者 = 6 世界与生态区**;主机唯一 `Append`(承 ADR-005)。

### Amendment G —— `ActorCellEntered` 追认为世界流 Kind(系统 1 的 GDD,2026-09-16)

**背景**:**一个被三方引用却无登记处的 Kind。** 撰写系统 1 的 GDD 时发现:

- **ADR-016 §三** 写「感知输入是粗粒度整数格(**玩家跨格写世界流事件**)」;
- **ADR-020 §四** 的图与正文两处写出 `ActorCellEntered{actor_id, cell, tick}`;
- **`docs/registry/architecture.yaml:397`** 以 `player_cell_crossing_event` 契约形式引用它。

**而它既不在本 ADR §三 的 Kind 骨架里,也不在 `design/registry/entities.yaml`。**
即 ADR-020 的**核心裁决**(§四)所依赖的那个 Kind,在登记面上**不存在** ——
若照 §三「路由 = `Kind → StreamId` 白名单纯函数」的字面执行,该事件会**被白名单拒绝**。
(与 9 的 `CompoundTriggered` 同一失效模式:被自家白名单构建期拒绝。见 `entities.yaml` 对应注。)

**就地修订一处**:

1. **§三**(世界流 Kind 骨架):追加 **`ActorCellEntered`**(9 → 10)—— 第二个以「系统 ADR 追加」
   方式扩骨架的 Kind。载荷 `{ actor_id, cell(WorldPos), tick }`(**三字段均整数**)归
   **ADR-020 §四 + 系统 1 的 GDD**(`player-controller-and-movement.md` R5 / R6)。

**三条立裁(系统 1 的 GDD 提出,随本 Amendment 一并登记)**:

1. **不另发 `Exited`** —— 下一条 `ActorCellEntered` 已隐含上一条离开。对称的 `Exited` 会让
   事件率翻倍,且需额外定义「退出后未进入」的边界态。
2. **命名用 `Actor` 而非 `Player`** —— 为 27 敌人 AI 留口(它同样需要「我的敌人在哪一格」的
   同型事件);`actor_id` 走 **ADR-006 Amendment B** 的 id 空间(`max(patient_id)+1` 高水位重构,
   承 ADR-016 §二 已把该空间扩到「受伤实体」),**玩家在开局经 `IIdAuthority` 分配一份**。
   事件的**排序键** `Patient` 仍用 `PatientId.None`(世界级事件,ADR-007 §四)—— **一个 id 空间、两个字段各司其职**。
3. **有界性**(以系统 1 的两条**分离的**不变量为准 —— **载体不同**:
   `F-1-1a` 是**构建期静态**断言,`F-1-1b` 是**运行期计数**不变量;混为一谈会各自失效):
   - **频率上界 = tick 频率**(`F-1-1b`)。跨格**检测**跑在每帧末,而**提交**跑在 tick 边沿:
     tick 内后续的跨格**覆盖**待发值,**不追加** ⇒ `Append` ≤ **1 条 / tick**。
     即本 ADR §六 的有界性论证**扩展而非重写**(与 Amendment F 同法)。
     ⚠️ **`tick` 字段的语义据此收窄** —— 它是「**观察到**跨格的那一 tick」,
     与「跨格**发生**的那一 tick」最多相差一个 tick。**订正前无此约束,须同步落地实现。**
   - **水平防隧穿**由 `F-1-1a` 的 `SPEED_MAX × MAX_DT ≤ LATTICE_SIZE` 保证 ——
     ⚠️ **2026-09-16 就地订正**(系统 1 的 GDD 首轮评审根因 4):原文写 `× TICK_PERIOD`
     是**用错变量** —— 跨格检测跑在**每帧**,而积分步长是 `dt`;`dt > TICK_PERIOD` 时
     (帧率 < tick 频率,**常态**)一帧可跨多格而该式仍成立 ⇒ **断言通过、隧穿照发生**(静默失败)。
     正确的不变量用 **`MAX_DT`**(EC-6 的钳位值);`TICK_PERIOD` 退居 `F-1-1b` 的**事件率**面。
     但那一条**推不出**「每 tick 至多一条」(tick 内折返不消耗位移预算),
     两条必须**分别断言**。
   - **y 轴显式豁免**(`F-1-1c`,用户裁定 2026-09-16):自由落体的竖直速度可远超 `SPEED_MAX`,
     竖直隧穿是**真实**的(玩家确实落在那里,与 EC-4 的传送同构);13 / 27 对玩家竖直位置
     只有「落点」语义,无连续性语义。⇒ **原稿此处的 `⌈JUMP_HEIGHT / LATTICE_SIZE⌉` 论证已删除**
     (它担心的是事件率,而事件率由 `F-1-1b` 管 —— 方向搞反了)。
     ⚠️ **豁免不解除计数上界**:竖直跨格仍走同一条归并路径,`≤ 1 / tick` 照旧成立。

**同批订正**:`ADR-020` 的 **Amendment A**(其 Engine Compatibility 判据 ② 原文与 §四 自相矛盾,
已就地订正)。

### Amendment I —— 追加三个**病史流** Kind(系统 10 的 GDD,2026-09-18)

> 编号说明:**Amendment H 在本 ADR 只有头部修订注、无正文小节**(2026-09-17 追加 `Encounter*`
> 时正文同步落在 §三 骨架内)。本 Amendment 沿用同一做法:正文写在这里,数据同步落在 §二
> 域归属表与 `entities.yaml`。

**背景(根因 R-2,10 的首轮 `/design-review`)**:10 的原稿把急救结算载荷称作
`TreatmentEvent`,并写「写入病史流」。但 `TreatmentEvent` **是一个内联元组,不是具名 `Kind`** ——
`entities.yaml` 原有 18 个 `SimEvent.Kind` 里没有它,§三 骨架里也没有,而
`disease-simulation.md:183-184` 明写「**9 侧 `Kind` 白名单随其表,列表外的 `Kind` 构建期拒绝**」
⇒ **照原稿实现,10 写的每一笔都会被 9 拒收**(静默失败:写路径在构建期才炸,不在评审期)。
这与 `ActorCellEntered`(Amendment G)· `Encounter*`(H)· `EnemyInjuryOnset` /
`InjuryStateChanged`(25 的 R2/R3)**同型** —— 「引用却无登记」这一失效模式在本 ADR 已**第 5 次**发生
(G · H · 25 三元组 · 本批;另有一次同型但表现为「被自家白名单拒收」的 `CompoundTriggered`,
只登记在 `entities.yaml`,未走本 ADR)。

> **⚠️ 计数不是重点 —— 重点是五个人独立踩到同一块石头,缺的是一道构建期前置校验,
> 不是第五份修订。** 现行防线只有**消费侧**(9 的白名单在构建期拒收未登记 Kind),它是
> **兜底不是前置**:它能保证「不会带着坏 Kind 出货」,但每次都让问题**迟到一轮评审**才被发现。
> **缺的前置校验**(登记为系统性防线议题,归下一次 `/architecture-review`,**不在本 ADR 打补丁**):
> **作者侧的 Kind 引用闭包检查** —— 扫描 `design/gdd/*.md` 与 `docs/architecture/*.md` 中出现
> 的 `SimEvent.Kind.*` 与大写驼峰 Kind 名,凡**未出现在 `entities.yaml` 注册表内**者**构建期失败**。
> 该检查是纯文本 / AST 级的工具活(不依赖引擎),与 ADR-012 的 CI 门同批落地。**五次同型失效
> = 该判据已具备升 BLOCKING 的经验依据。**

**裁决(承 ADR-021 §⑤ 的先例:系统 GDD 追加 Kind,骨架进本 ADR,载荷归系统 GDD)**:

| # | 事项 | 裁决 |
| --- | --- | --- |
| ① | 三 Kind 的路由即时生效 | `EmergencyAttempt` / `EmergencyTreatmentApplied` / `DrugTreatmentApplied` → **病史流**;**本 ADR 只定边界与路由,不定载荷字段语义**(同 §三「骨架先行」纪律) |
| ② | **首次以本通道扩病史流** | 前六项追加(`PoiStateChanged` / `ActorCellEntered` / `Encounter*` / 25 三元组)全在世界流。本批**不是新增流** —— 处置真源本就住病史流(§一 Q1 默认命中),只是**补登记**。`InjuryOnset` 虽属病史流,但由 25 的 GDD 直接登记、未走本 ADR 的 Amendment 通道,故本批是**第一条走该通道扩病史流的 ADR 修订** |
| ③ | **动作类 / 药物类拆两个 Kind** | 11 的 `AC-11-10` 禁「第二份 `SkillMul` / `ResultMul` / `JudgeResult`」⇒ 两条写者的结算来源不同;若共用一个 Kind,`method` 对药物类**恒 `Manual` 却仍要携带 = 空字段**。判据源 = ADR-008 §一(路由是 `Kind` 的纯函数)+ ADR-021(写者 = 所有者)。**二者仍写同一条病史流,除 `method`/`cause` 外形状一致 ⇒ 9 侧「只认三元组」的 C5 不破** |
| ④ | `EmergencyAttempt` 是**判定输入**,不是结算 | §七 的 `PickupIntent → 主机判距 → 落流` 三段式在急救侧的对称落实;**两条事件、两个写者语义**。⚠️ 这是本仓**第一次在流层同时物化三段式的两步**(此前 `PickupIntent` 只有判据、无第二条流上输入事件) |
| ⑤ | **有界性**(扩展 §六,不重写) | 每完成一次动作 ≤ **2 条**病史流事件(意图 + 结算),**与帧率无关**;`Armed` 内中止发 **0 条**(10 规则六之甲:中止 ≠ 跳过)。上界由「动作完成」这一玩家行为事件限定,不由采样率限定 ⇒ 不破坏 §六 的论证结构 |
| ⑥ | 客户端产出 / 主机物化 **不构成对 ADR-005 的豁免** | 10 规则十一的 C 路(客户端聚合上行 → 主机执行 `Judge` 并 `Append`、`Seq` 由主机发号)读起来像「客户端也能写流」,**不是**:客户端的载荷只有经主机 `Append` 才进入流的权威副本。这正是 ADR-005「主机唯一执行 `Step` / `Append`」的兑现,与 ADR-020 `Amendment B`(`ActorCellEntered` 的 `Append` 权 = 主机唯一)同构 |
| ⑦ | 7a 折叠**不受影响** | 三 Kind 均落病史流,终态折叠谓词 `Folded(p)` 的既有口径(ADR-008 残留义务 / ADR-010 §三)自动覆盖;⚠️ `EmergencyAttempt` 是**输入**事件,折叠窗口内若被折叠,其对应的结算事件必须同批折叠 —— 由 `(Tick, Patient, Seq)` 邻接性保证(两者同 tick 由主机连发)。**该推论须由 7a 的 EditMode 探针实测**,登记为 10 的 `OQ-10-8` |

**未结(不在本 ADR 裁决面上)**:`OQ-10-9` —— `EmergencyAttempt` 的**上行 QoS**。走
ADR-001 第二通道(latest-value / unreliable)会**丢**,而它是主机的判定输入 ⇒ 与
ADR-001「保序非必需」相容、与「判定输入不得丢」**不相容**。需 **ADR-001 的一次窄修订**,
归 45 的 GDD 轮(P1b 前),**不在本 ADR 打补丁**。

### Amendment J —— `Craft` 的归属订正 · 载荷定稿 · 全序键裁定(系统 18 的 GDD,2026-09-19)

> 编号说明:沿用 Amendment I 的做法 —— 正文写在这里,数据同步落在 §三 骨架注、
> `item-database.md` D-21-28(结案)、`entities.yaml:91`(注更新)、`processing.md` 注④。

**背景**:18 炮制的首轮 `/design-review`(2026-09-19)命中三处与本骨架相关的悬空:
① `Craft` 在 §三 的记源写「(21a)」—— 但 21a 只有静态 `Recipe` 表,点火 / 落流的调度语义
**全文无人认领**(「引用却无登记」的**归属变体**:Kind 有登记、写者无登记);
② 「载荷归 R-2」的 R-2 从未给出 Craft 载荷形状 ⇒ 18 的产出物在流里**没有身份**
(无 `actor_id` 归因、无产出实例 id、无器具位 —— BL-18-1/2 的同根「载荷三缺」);
③ `D-21-28`(Craft 总序键)待裁。

**裁决(2026-09-19 用户裁定 [甲],承 18 的 GDD)**:

| # | 事项 | 裁决 |
| --- | --- | --- |
| ① | **写者 = 18(炮制)/ 19(P1a 制作),求解 = 21a** | 21a 保持「一张表一个求解器」,不承担调度;§三 / §五 记源就地订正 |
| ② | **载荷定稿,「归 R-2」对 Craft 解除** | 三位补齐:`actor_id`(20 `InventoryOf` 归因)/ `output_instance_ids[]`(**主机于点火 tick 经 `ItemInstanceId.Next()` 铸造**,闭合 20 的前置 6 / R10)/ `tool_cell`(器具占用 = 派生态重建)。详 `processing.md` 规则八 |
| ③ | **全序键 = 三流键** `(Tick, StreamPriority, Patient, Seq)` | `Craft` 落世界流 ⇒ `StreamPriority` = 世界流位;`Patient = PatientId.None = -1`(ADR-007 ④,不污染 `max(patient_id)` 高水位);`Seq` 主机 `Append` 发号。21a 建议的 `(Tick, ActorId, Seq)` 中 `ActorId` 位**被载荷字段吸收** —— 键不必复制语义 |
| ④ | **起货溢出复用 `DropSpawned`(零新增 Kind)** | 18 在起货时点判容量,溢出量由 **20 从 `Craft` 事件派生折叠 `DropSpawned`**(医馆格);与 `OQ-20-1`「全部复用既有 Kind」同纪律;**18 不写该事件**(写者仍 20) |
| ⑤ | **有界性不重写**(§六 结构自动覆盖) | `Craft` 事件率 = 点火行为率 ≤ **每玩家每 tick 1**(单炉,OQ-18-3 [甲]);`DropSpawned` 派生增量 ≤ 每 `Craft` 1 条 ⇒ 总量纲仍是「玩家行为」,与帧率无关 |

**未结(不在本 ADR 裁决面上)**:`OQ-18-7`(炮制意图的上行通道,与 `OQ-10-9` / `OQ-4-10` /
20-BL-4 **同案**,归 45 的 GDD 轮 + ADR-001 窄修订,P1b 前)· `OQ-18-8`(跨玩家器具互斥,归 24)。

### Amendment K —— `ResourceHarvested` 载荷定稿 · 生产者恢复 · 有界性订正(系统 17 的 GDD,2026-09-19)

> 编号说明:沿用 Amendment I / J 的做法 —— 正文写在这里,数据同步落在 §三 骨架注、
> §六 有界性、`entities.yaml`、`systems-index.md` row 17。

**背景**:17 采集的首轮 `/design-review`(2026-09-19)裁 `MAJOR REVISION NEEDED`,
根因 **P0-1「避涟漪式改判」**(新变体)。17 的原稿规则二宣称「采集 = 即时拾取,
**零新 Kind**」,只发 `DropSpawned` + `DropClaimed`。但:

① **`ResourceHarvested` 是本 ADR 已注册的 Kind** —— §二 三态表把它列为「资源点(可采集合)
   消耗状态 = 模拟态 → 世界流」,§七 Guidelines 4 **明写写者 = 17**;
   17 从不发它 ⇒ **一个已注册 Kind 无生产者**,而**一份 GDD 单方改判了本 ADR 的分类**
   (违 `coordination-rules` 5「不得单方面改跨域」);
② **`DropSpawned.Payload` 承载不了 17 自己需要的事实** —— §五 定死
   `{ instance_id, spawn_anchor, item_key, qty }`,**无 `node_id`、无 `quality`**
   ⇒ 17 的 `gather_seq`(「该节点已发出的 `DropSpawned` 计数」)与 F-17-3 的余量重算
   (`Σ{ DropSpawned.e ∈ node }`)**均不可实现**;而 `spawn_anchor` 是**格坐标**,
   `OQ-4-11` 明许**同格同种多点** ⇒ 用锚点当节点键会撞;
③ **`gather_seq` 会被无关掉落污染** —— 死亡掉落 / 18 起货溢出(Amendment J ④)/
   52 的 `spawn_anchor` 点**都发 `DropSpawned`**,任何一条都平移该节点后续的全部品级序列;
   玩家还可在药丛上丢弃物品**盲刷**序列。

**裁决(2026-09-19 用户裁定 B,承 17 的 GDD)**:

| # | 事项 | 裁决 |
| --- | --- | --- |
| ① | **恢复 `ResourceHarvested` 为采集事实的载体** | 载荷定稿 = `{ instance_id, node_id, gather_seq, qty, out_quality }`(全整数);**写者 = 17 主机**;**17 的 GDD 为载荷形状的出处**(承 §三「骨架先行,载荷归系统 GDD」纪律)|
| ② | **一次成功采集落三条世界流事件** | `ResourceHarvested`(采集事实)+ `DropSpawned`(身份出生)+ `DropClaimed`(立即归属),**主机同一 tick 连发**。这是 Amendment I 已批准的「一次动作 = 多条流事件」形态在**世界流**的首次应用(此前只在病史流)|
| ③ | **`raw_quality` 不落流** | `raw_quality = CDFWalk(distribution, U)` 而 `U = f(WorldSeed, node_id, gather_seq)` —— **三项均在流里** ⇒ 可重算,存它 = 第二真源。`out_quality` **必须落流**:它经 `QualityCap` 截断,截断需采集者**当刻技能等级**(同 21a 已裁的「`qty` 写入时物化,非加载时重算」同一逻辑)|
| ④ | **`gather_seq` 的计源 = `ResourceHarvested`**(只数 17 自己的 Kind)| 消解 ③ 的污染路径;**零独立计数器**(可从世界流重算,承 §一 Q1 判据)|
| ⑤ | **§六 有界性就地订正** | 原「资源点消耗事件总数 ≤ 资源点数」**陈旧**(隐含「采完永久废弃」,而 F-17-3 的 `RegrowWindow` 使节点可再生)⇒ 改为**≤ 玩家采集动作率 × 会话时长**(每完成动作 ≤ 1 条 `ResourceHarvested`,**与帧率无关**);`|资源点|` 只约束**同时可采节点数** |

**涟漪(下游义务,须随各轮落地)**:
- **20 库存与物品**:`InventoryOf` 的 fold 谓词须**并入 `ResourceHarvested`** —— `quality`
  现落该事件,不并入则 `(item_key, quality)` 堆叠键重建不出 `quality`,`AC-20-03` 逐位重建不成立。
- **`entities.yaml`**:`SimEvent.Kind.ResourceHarvested` 须正式登记(此前「骨架在 ADR-009 §三
  即登记处,不在本表重复」,现由本 Amendment 给出载荷 ⇒ 与 `PoiStateChanged` / `ActorCellEntered`
  同规格入表)。
- **`architecture.yaml`**:三处 `ResourceHarvested` 路由字符串**已含**该 Kind(无需改);
  但 `world-stream` 契约注可补「载荷 = Amendment K」。

**未结(不在本 ADR 裁决面上)**:17 的 `OQ-17-6`(采集意图上行通道 —— 与 `OQ-10-9` / `OQ-4-10` /
`OQ-18-7` / 20-BL-4 **同案**,归 45 的 GDD 轮 + ADR-001 窄修订,P1b 前)· 17 的 `OQ-17-7`
(资源点数据驻留 / 切片,归 54 + ADR-014,承 `OQ-6-8` 先例)。

### Amendment L —— 追加世界流 Kind `PlayerDied`(系统 29 的 GDD 首轮评审,2026-09-19)

> 编号说明:沿用 Amendment I / J / K 的做法 —— 正文写在这里,数据同步落在 §三 骨架注、
> §二 三态表、`entities.yaml`、`systems-index.md` row 29。

**背景**:29 死亡与复活的首轮 `/design-review`(2026-09-19)裁 `MAJOR REVISION NEEDED`
(9 条阻断),**头号阻断 B1 = 死亡无流载体**。29 的 F-29-2「死亡冷却」把
`last_death_tick(actor)` 定义为「扫该 actor 在世界流的最后一条死亡事件」,而
**全库当时没有任何死亡 Kind**(`entities.yaml` 零登记 · ADR-009 §三 9-Kind 骨架里没有 ·
`architecture.yaml` 零路由)。两条静默失败:

① **冷却永远读不到值** ⇒ `DeathAllowed` 恒真 ⇒ 冷却**静默退化为不存在** ⇒
   29 自己的 `:239` 与 20 的 BL-7② 都点名的「自杀清负重」掉落经济漏洞**实际未堵**;
② **背包为空的死亡在整个三流零痕迹** ⇒ 该次死亡不可重建,而掉级(30 §4.3)是
   `Level` 的**函数**,`Level` 的重建需要「发生了几次死亡」这个计数 ⇒ **掉级也不可重建**。

这是「引用却无登记」失效模式的**又一变体**:29 的 GDD 引用了它自己假设存在的死亡事件,
而该事件**从未被任何系统认领**(与 `ActorCellEntered` / `CompoundTriggered` 同型)。

**裁决(2026-09-19 用户裁定,承 29 的 GDD 首轮评审)**:

| # | 事项 | 裁决 |
| --- | --- | --- |
| ① | **新增世界流 Kind `PlayerDied`** | 载荷 = `{ actor_id, death_cell(WorldPos), tick }`(**全整数**;无连续坐标 / 无死因文本 / 无伤害数值);`Patient = PatientId.None`(不污染 `max(patient_id)` 高水位);骨架先行,载荷归 29 的 GDD |
| ② | **写者 = 9** | 9 拥有 F4 致死判据(`OQ-25-1` **已裁路甲** 2026-09-19:9 的 F4 经逐实体 `SelfLimited(entity, d)` 门对玩家战伤判死)。**29 是消费者,对 `IEventSink.Append` 的调用点数 = 0**(「状态所有权 = 写入权」,ADR-021 §①) |
| ③ | **无条件发出** | **背包为空也发** —— 这是冷却与掉级重建**唯一**的真值来源;冷却期内(`DeathAllowed == false`)**不发**(不结算 ⇒ 无死亡"发生") |
| ④ | **`death_cell` = 派生态** | 取该玩家世界流中**最后一条 `ActorCellEntered` 之 cell 原样携带**,**禁止读实时物理位置**(ADR-020 §四:玩家连续位置 = 纯表现态)。29 原稿写「判定用当时的实时物理位置」**违反 ADR-020 §四 且破 ADR-012 逐位重放**,已就地废止 |
| ⑤ | **§六 有界性扩展** | `PlayerDied` 事件率 ≤ **玩家数 / `DEATH_COOLDOWN`**(冷却使死亡离散化)⇒ 归入「玩家有界流」,**与帧率无关**;§六 论证**扩展而非重写** |

**涟漪(下游义务,须随各轮落地)**:
- **`entities.yaml`**:`SimEvent.Kind.PlayerDied` 正式登记(**本轮已落** —— 与 `PoiStateChanged` /
  `ActorCellEntered` / `ResourceHarvested` 同规格入表)。
- **`architecture.yaml`**:`world-stream` 契约的 Kind 路由字符串须并入 `PlayerDied`
  (**本轮已落**);`signal_signature` 路由白名单同。
- **9 疾病与伤情模拟**:须补 `SelfLimited(entity, d)` 的实现落点(9 的 R12 注块已预留两种形态,
  取「自限门升为逐实体谓词」),并**反向列 29**。
- **30 技能与熟练度**:§4.3 的掉级公式须与 29 的 F-29-1 同源修正(整数截断除法,**非** `Fix` 域;
  见 29 的阻断 B2)—— 本条为**数值口径订正**,不改 30 的真源地位。

**未结(不在本 ADR 裁决面上)**:29 的 `OQ-29-4`(冷却期内再次致死的语义)· `OQ-29-5`
(`DEATH_COOLDOWN` 值)· `OQ-29-6`(回程载体的 P0 形态)· 24 的床格选定规则 · 9 的 `SelfLimited` 实现落点。

## Alternatives Considered

### Alternative 1: 全量进流(一切世界状态事件化)

- **Description**:地形、位置、掉落物理全部进事件流
- **Pros**:确定性最强;单一真相
- **Cons**:流体积无界(地形逐 tick 进流 = 天文数字);PhysX 位置逐位 = 不可达;
  第三方地形工具整体出局;对 1-4 人 co-op 的收益不成立
- **Rejection Reason**:确定性是**手段**不是**目的**;把表现态强行进流是把成本砸在不产生
  分歧的地方。报告 B-7 点名的「确定性范围被各系统各自扩大」正是此型

### Alternative 2: 快照旁路(模拟态进流 + 位置定期快照)

- **Description**:掉落清单进流,位置进 7a 定期快照,不做表现态网络同步
- **Pros**:流体积最小;实现直观
- **Cons**:快照 = **第二真相**(与 ADR-005 唯一真源口径冲突);联机时位置不同步
  ⇒ 玩家看到「他人捡走一团空气」;迁移后位置从快照回填,与清单失步
- **Rejection Reason**:位置是**会话内共享表现**,归网络层(45)才是正确边界;
  快照只作加载优化,不承担实时一致性

### Alternative 3: 世界事件并入病史流

- **Description**:世界状态变更事件追加进 ADR-005 的病史流
- **Pros**:不需要新流
- **Cons**:**污染 9 的真源所有权**(病史流是 9 的模拟真源,写入者非 9 即越权 —— 与
  注册表禁例 `case_events_in_history_stream` 同构);世界事件不折叠(§六)与病史流
  折叠规则直接冲突
- **Rejection Reason**:所有权语义混乱。用户的裁决明确取**第三条逻辑流**(2026-09-15)

### Alternative 4: 世界事件并入病例流

- **Description**:世界状态变更事件追加进 ADR-008 的病例流
- **Pros**:不需要新流;病例流也不折叠
- **Cons**:**跨域职责混乱**(病例流是 37 的生命周期流,世界状态与病例无关);
  病例流的「有界性论证 = 写入者只有玩家 + 立案率上限」**不适用于**世界流
  (资源点 / 生态再生是系统驱动,不是玩家驱动)
- **Rejection Reason**:两流各有其主,世界状态需要自己的所有权与有界性论证

### Alternative 5: 掉落位置也进流(整体进流)

- **Description**:`DropSpawned` 之后位置逐 tick 进流,拾取判定可逐位重放
- **Pros**:拾取判定完全确定性
- **Cons**:掉落物理 / 动画表现与流位置脱节(物理是表现态,逐 tick 采样无意义);
  流体积大;对 1-4 人 co-op 收益不成立
- **Rejection Reason**:§七 已给出更便宜的解法 —— **判定不重放,结果进流**;
  意图 + 当下判距 + 宽容半径同时满足确定性(结果)与实时性(位置)

## Consequences

### Positive

- **确定性边界可辩护、可逐系统套用**:三问判据写成数据(三态表),实现照表,不再各猜
- **第三方工具选型有判据**(R-9)—— **2026-09-15 ADR-015 更新**:工具已移出运行期,
  判据不再适用;R-9 的确定性收益改由「逻辑层整数 + 视觉层隔离」实现(ADR-015)
- **PhysX 不进入模拟域**:位置 = 表现态,异机落点略异可接受(1-4 人 co-op 完全够用)
- **掉落 / 拾取 / 资源 / 建造的归属变更全部可重放**:迁移 / 中途加入不静默分叉
- **动态量保留 `WorldSeed` 的效用**:病人种子 / 掷骰 / 采集散布 / 掉落表现仍由外生常数决定
  —— **2026-09-15 ADR-015 修订**:原「静态世界成为外生常数的纯函数」不再成立(静态几何改走烘焙数据);
  **中途加入零地形传输仍成立** —— 逻辑层随构建出货,不经网络传输

### Negative

- 三态边界需要**逐个系统过判据并留痕**(GDD §Dependencies 三态表)—— 设计工作量
- 世界流不折叠 ⇒ 世界流与病例流同属长期流,7a 快照与压缩策略要多管一条流
- 拾取判定是**当下裁决,不是确定性重放** —— 需要实现者理解「判定过程不进流」的边界,
  否则会写出「判距重放」的反模式(有 Guidelines 5 与风险 4 兜底)
- 世界几何的逻辑层编辑 = **自研关卡工具**(R-9 的确定性一面,**ADR-015** 定型);
  第三方工具最多省编辑期素材 —— **2026-09-15 修订**:原「地形生成数学入定点域」的成本
  改为「两层编辑 + 一致性检查」的成本

### Neutral

- 52 的世界级事件**仍留病史流**(决策注记 ≠ 状态变更)—— 边界澄清,不改归属
- `IIdAuthority` 增加 `ItemInstanceId Next()`,机制细节(计数器 + 高水位)归 R-2 / 7a

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **地形逐位重建对 Unity Terrain 内部算法不透明**(版本差异 / Mono vs IL2CPP / x86 vs ARM) | ~~**高**~~ **已消除** | ~~高~~ **无** | **2026-09-15 ADR-015**:地形改由烘焙逻辑层加载,不经 Unity Terrain 运行期生成 —— 该风险整条移除 |
| ~~**第三方地形工具不满足逐位复现**~~ | **已作废** | **无** | **2026-09-15 ADR-015 §六**:第三方工具移出运行期(仅编辑期辅助),逐位判据随之作废 |
| **掉落位置不进流与重放矛盾**(迁移后只有身份无位置) | 中 | 中 | **出生锚点格坐标进流**(§五,`WorldPos`),位置从 7a 快照取(引擎复核补的边界) |
| **拾取判定吃物理位置**(模拟域悄悄依赖表现态) | 中 | **高** | 意图事件 + 主机当下判距 + 宽容半径(§七);判定不重放,结果确定性进流 |
| **float 漏进流边界**(位置 / 载荷坐标) | 中 | **高** | ADR-006 §五 探针 + 门 B(IL 扫描)自动覆盖世界流;Guidelines 6 |
| **世界流被误当病史流折叠**(实现者沿用 ADR-005 折叠规则) | 中 | 中 | 注册表禁例 `world_state_stream_physical_fold` + 路由白名单单测 + 本 ADR §六 |
| **`instance_id` 机制悬空**(R-2 / 7a 未落,`Next()` 无处实现) | **高** | 中 | 本 ADR **只确认发放点**,机制由 R-2 / 7a 承接(登记为 R-2 的门控项);P0 占位实现 = 单调 long |

## Performance Implications

> **状态:临时值** —— 项目性能预算本身待定(`technical-preferences.md`)。

- **CPU**:三流 Append 均 O(1);逻辑层加载 = 加载期一次性 O(布局规模)(ADR-015)
- **Memory**:世界流 = 建造物数 + 掉落数 + 资源点数 × 事件数;掉落生命周期短(§六)
- **Load Time**:逻辑层加载(整数格)在加载期完成;视觉层 mesh 化(表现态)可异步 / 流式(ADR-015 §四)
- **Network**:三流传输(身份 + 归属)经自定义可靠有序消息流;位置 / 轨迹经表现态同步
  (高频率、非确定性、会话内)—— 两条通道语义分离

## Migration Plan

**本项目尚无世界系统的实现,故无迁移 —— 本 ADR 是「第一次就做对」。**

1. **ADR-005 / ADR-006 / ADR-008 就地修正**:三流口径前向指针(§Amendment E 落点),
   与 ADR-008 的 `StreamId` 扩为三值 —— 本 ADR 写盘时一并落(2026-09-15)。
2. **R-9(ADR-002 地形选型)→ ✅ 已由 ADR-015 兑现(2026-09-15)**:世界几何定型
   (手工烘焙固定世界 · 单格坐标 · 模块化建造 · 第三方仅编辑期)。**原「逐位复现 spike」取消** ——
   第三方工具已不在运行期。
3. **R-2 / 7a 持久化起草时**,承接:`IIdAuthority.ItemInstanceId.Next()` 机制 ·
   `DropSpawned/Claimed/Despawned` 载荷 · `Craft` 载荷与全序键 · 世界流序列化 ·
   定期快照(优化非真相) · 迁移时世界流逐位保留。
4. **45 网络层**:三流传输 + 合并排序遵循三值 `(Tick, StreamPriority, Patient, Seq)`;
   表现态位置同步与模拟态事件流**通道分离**。
5. **6 / 17 / 20 / 23 / 24 / 25 的 GDD** 各自登记 §Dependencies 三态表(§Guidelines 1)。

**Rollback plan**:若最终不做联机,表现态退化为本地表现(45 同步可删);
世界流的三流路由与不折叠**不可回退**(已进存档格式与 7a 快照)。若做联机,**本 ADR 不可回退**。

## Validation Criteria

- [ ] 单测:三流路由按 Kind 白名单分发(病史 / 病例 / 世界),无跨流错投
- [ ] 单测:三流全序 `(Tick, StreamPriority, Patient, Seq)` 无平局(`StreamPriority` 病史 < 病例 < 世界)
- [ ] 单测:`Seq` 发放域唯一,三流共享;世界事件 `(Tick, None)` 与病例流世界级事件连续无重号
- [ ] 单测:`max(patient_id)` 重构扫**三流并集**,`PatientId.None` 显式排除
- [ ] **派生态确定性(黄金对拍,ADR-015 §Validation)**:同一份**烘焙逻辑产物**两平台加载
      ⇒ 地形 / 生态区布局判定**逐位一致**(逻辑形状,整数域)。**原「同一 `(WorldSeed, 配置)`
      两平台重建」已废止** —— 地形不再由 seed 生成(ADR-015 §二)。
- [ ] 掉落:重放世界流可重建**掉落清单(身份)**;位置从 7a 快照取(表现态)
- [ ] 拾取:意图 + 主机当下判距 + 宽容半径 ⇒ `DropClaimed` 确定性进流;
      **判定过程不在重放中执行**(静态可验证:判距代码不进入模拟程序集)
- [ ] 模拟域零 float:世界流进流字段经 ADR-006 门 B / §五 探针断言(坐标一律整数网格 / `Fix`)
- [ ] 世界流不折叠:7a 快照往返后世界流**逐位不变**

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | TR-itemdb-031:掉落实体的世界状态事件化边界 | 掉落身份进流(`DropSpawned/Claimed/Despawned`)、位置表现(45 同步)—— §五 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-26(`instance_id` 权威缺契约)· D-21-27(仅主机铸币) | 确认发放点 = `IIdAuthority.ItemInstanceId.Next()`;拾取 / 采集主机裁决 —— §三 / §五 / §七 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | D-21-28(`Craft` 事件全序键)· TR-itemdb-021 | `Craft` Kind 骨架 + 三值全序键即时生效;载荷与全序键机制归 R-2 —— §三 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | 守恒律 / 定点纪律在掉落 / 消耗上的延伸 | 进流字段禁 float;`instance_id` 入快照(D-21-26 原条目)—— §三 / §五 |
| `design/gdd/systems-index.md` | 6 世界与生态区 | 静态地形 / 生态区布局(原写「WorldSeed 驱动」) | 派生态:**烘焙逻辑布局加载重建** + 动态量仍由 WorldSeed(ADR-015)—— §四 |
| `design/gdd/systems-index.md` | 17 采集 | 采集铸造权 / 资源点消耗 | `ResourceHarvested` 主机裁决进世界流 —— §三 |
| `design/gdd/systems-index.md` | 20 库存与物品 | 库存容量上界 ⇒ 掉落有界 | 掉落 ≤ 携带量上界,世界流有界性论证 —— §六 |
| `design/gdd/systems-index.md` | 23 模块化建造 / 24 医馆即机器 | 建造物落位 / 拆除 / 改造状态 | `StructurePlaced/Removed/Modified` 进世界流(载荷与网格坐标归 **ADR-015**)—— §三 |
| `design/gdd/game-concept.md` / `skill-system.md` | 死亡掉落(英灵神殿式) | 掉落物散落原地,回程捡拾 | 表现态驱动模拟态:物理走网络层,清单经 `DropSpawned` 落地 —— §五 |
| `design/gdd/systems-index.md` | — | §9 C6:状态从第一天起 authority-agnostic | 三态边界从 P0 起 authority-agnostic(模拟态进流 · 派生态纯函数 · 表现态可丢弃) |

## Related

- **ADR-005 确定性模拟与状态同步模型**(Accepted 2026-09-13)—— 本 ADR 是其确定性边界的
  **开放世界延伸**;`IIdAuthority` 扩展 `ItemInstanceId Next()`(TR-itemdb-019 落点)
- **ADR-006 定点域边界数据契约**(Accepted 2026-09-14)—— 本 ADR 的 Amendment E 升格其
  Amendment C / D 至三流口径;**不改形状**;进流字段禁 float 的既有判据自动覆盖世界流
- **ADR-007 事件权威与掷骰状态**(Accepted 2026-09-15)—— `PatientId.None` 哨兵是世界事件
  的 Patient 取值;`WorldSeed` 归 7a 是本 ADR 派生态判据的外生常数前提
- **ADR-008 病例事件流**(Accepted 2026-09-15)—— 本 ADR 扩展其 §一 路由至三流、
  §二 全序键至三值、§六 折叠豁免至世界流
- **R-2 / 7a 持久化**(待撰写)—— 承接 `ItemInstanceId.Next()` 机制 · `Drop*` / `Craft`
  载荷 · 世界流序列化 · 快照
- ~~**R-9 / ADR-002 开放世界地形**(待撰写)—— §四 判据预置:工具不能逐位重建则整体出局~~
  → ✅ **已由 ADR-015 兑现(2026-09-15)**:手工烘焙固定世界 · 单格坐标 · 模块化建造 · 第三方仅编辑期
- ~~**R-10 / ADR-003 建造系统网格**(待撰写)—— `Structure*` 载荷 · 网格坐标系(`WorldPos` 定型)~~
  → ✅ **已由 ADR-015 兑现(2026-09-15)**:建造网格与地形共用同一格;`WorldPos` = 整数格
- **R-3 / ADR-001 联机选型**(待撰写)—— 追加约束:承载**三条**逻辑流 + 表现态位置同步
- `design/registry/entities.yaml` —— 待登记 `WorldPos` 坐标系(ADR-015 §三 定型)与生态区 / 资源点常量
