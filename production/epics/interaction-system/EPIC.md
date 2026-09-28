# Epic: 交互系统

> **Layer**: 边界层(呈现侧)—— **不是 Core / 门 A 程序集**(GDD Overview 首轮改判:归属判据 = 引用集/读侧,4 读 13 的 `IPresentPatients` / 42 的模态开集 / 10 的 `Armed`,门 A 引用不到任何一项;承 ADR-017 §二 判据)
> **GDD**: design/gdd/interaction-system.md
> **Architecture Module**: 纯目标选择器 `(玩家格, 世界状态, 输入意图) → 目标(Kind, id) | None` + 按种类路由 + 模态门(两方向)+ POI 自报(→6,非写)
> **Status**: In Progress
> **Stories**: 6 stories created (2026-09-28)

## Overview

交互系统(系统 4)是**全案最上游的意图汇聚点**:它是一个**纯函数**(零可变字段,清空重建结果相等)、**只选目标不结算**(路由给拥有方 20/17/37/8/10/11/6/23/18/24)、**不写三流**(POI「已发现」走 `IDiscoveryReporter.Request` → 6 校验/判距复验/per-tick latch → 6 唯一 `Append`)。候选集 = 派生态,**四源全部整数格**(世界流锚格 · 6 烘焙逻辑层 · 13 只读视图 · **23 的 `BakedInitial` 第四源** —— 漏则开档自带的门/柜不可交互);目标选择 = **确定性全序**三键(`d∞` 切比雪夫 int64 → `KindPriority` 十项互异 → `StableId` 标量),禁列表序/哈希序/朝向项(「身位即光标」)。模态门 `Accept ⟺ ¬Armed(10) ∧ ¬ModalOpen(42)`,读 **`IModalState.Modal` 闭集枚举本身(引用非复制)**,拒收 = 丢弃不排队。数据契约 `4-DC-1…6` 构建期硬失败闭集校验。**客户端意图上行通道不存在**(`OQ-4-10`,归 ADR-001 窄修订 / 45 轮,P1b 前)—— 4 侧只登记不得自选通道。**全部数值留白归用户**(`R_INTERACT` / `KindPriority` 十项序 / 各时长),「取值一旦存在即被守住」口径。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-016 §三 | **禁读表现态位置**构造/过滤候选集(第二 QoS 到达时序不定 ⇒ 决策非确定性函数);4 是「读方(13/27 读整数格)+ 写方(玩家跨格事件)」两侧的汇合点 | LOW(判据纯 C#) |
| ADR-020 §四 | 玩家位移 = 纯表现态,唯一 sim 投影 = `ActorCellEntered`;4 读「经流确立的格」(规则二 (a) 路),`pending_cell` 只用于本地预表现 | LOW |
| ADR-021 | **POI 状态唯一写者 = 6**;4 = 合法自报方之一 `{4,25,37}`,「自报 ≠ 写」;`PoiStateChanged` 载荷全整数 | LOW |
| ADR-013 §十 Amendment A/B | `IModalState.Modal : ModalId`(权威枚举,现 **7 员**,`None = 0` 哨兵);4 **引用不复制**;42 只渲染不持状态 ⇒ `ModalOpen` **不得进流**(伪解否决) | HIGH(部分须 spike;4 只消费只读面) |
| ADR-014 | `interaction_kinds.json` 两阶段烘焙;`4-DC-1…6` = 阶段 2 白名单/区间/闭集位点,**构建期硬失败非运行期告警** | MEDIUM |
| ADR-015 §三 | 单一整数格:`d∞` 与 1 同源;`R_INTERACT` 单位 = 格数,上界 `min(W,H,D)−1`(三审订正方向) | LOW |
| ADR-006 | 载荷全整数域(`DiscoveryRequest{poi_id:i64, evidence_cell:WorldPos, tick:i64}`);「逐位」措辞保留给 ADR-012 字节对拍,4 的重放断言 = 枚举/整数 id 相等 | MEDIUM |
| ADR-009 | 候选集 = 世界流派生(Q1 判据);世界流有界性论证的扩展:自报率 ≤ 邻域格上界/tick/玩家,**与帧率无关** | MEDIUM |
| ADR-005 | 输入是意图源,不直接驱动模拟(规则一 = 同一裁决在选择层的落实) | LOW |
| ADR-011 | `InteractIntent` 由 3 的动作映射供给(意图非 SimEvent);急救直读通道与 4 的世界路由是两条独立路(规则七 方向①) | HIGH(接口层纯 C#) |
| ADR-001 | **缺口,非覆盖** —— 客户端→主机意图上行无归口(`OQ-4-10`);列入仅供边界核对,4 侧零网络实现 | — |
| ADR-017 §二 | 程序集归属判据 = 引用集(门 A 恰 BCL)⇒ 4 判归边界层(Overview 改判的机械依据) | LOW |

**Engine Risk**: **LOW 为主** —— 4 的判据本体(反射类型闭包 / 运行时 spy / AST / 纯整数算法)刻意不触任何 post-cutoff API;`IEventSink`/`IModalState` 消费面为纯 C# 契约。须实测面仅有:`SphereCast` 无关(不属 4)、真机手柄走查(`AC-4-21 [L]`)与烘焙校验的 CI 载体(归 ADR-012 轮)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-interaction-*`)。
> 计数(实测):**15 条 = covered 13 · partial 2 · Untraced = None**。
> ⚠️ `TR-interaction-014` partial = 消歧顺序/KindPriority 取值属 **GDD 内部 schema,无 ADR 承接**(结构性,非待办);`TR-interaction-015` partial = 2026-09-20 由 covered **降级**(TD C4「借绿」:裁定≠验收,`blocked_by: 实现轮`)+ QQ-11 补引据(`ADR-011 + ADR-013`)**不撤销 blocked_by** —— 本 Epic 交付形状与判据,**不得记该条转绿**。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-interaction-001 | 4 = 纯目标选择器,输出 `(目标, 种类)`,零 gameplay 结算 | ADR-005 ✅(AC-4-01) |
| TR-interaction-002 | 候选集 = 四源(含 23 `BakedInitial` 第四源)整数格 | ADR-016 + ADR-020 ✅(AC-4-03/22) |
| TR-interaction-003 | 禁读表现态位置;判据 = 反射字段类型非 grep | ADR-016 + ADR-020 ✅(AC-4-03) |
| TR-interaction-004 | 确定性全序三键;禁遍历序/哈希序决胜 | ADR-006 + ADR-015 ✅(AC-4-06) |
| TR-interaction-005 | `d∞` 切比雪夫纯整数,与 1 格语义同源 | ADR-015 + ADR-006 ✅(F-4.1) |
| TR-interaction-006 | 4 不写三流;`Request` → 6 唯一 `Append` | ADR-021 ✅(AC-4-02) |
| TR-interaction-007 | 幂等由 6 保证;4 零「报过了」记账 | ADR-021 ✅(AC-4-13) |
| TR-interaction-008 | 4 不发相机档位意图 | ADR-020 ✅(AC-4-11) |
| TR-interaction-009 | 模态抑制方向①:10 `Armed` 压制,3 零状态 | ADR-011 + ADR-013 ✅(AC-4-10) |
| TR-interaction-010 | 方向②:读 `IModalState.Modal ≠ None`,不自建布尔 | ADR-013 ✅(AC-4-09) |
| TR-interaction-011 | 4 为 `MotorSuppressed` 合法调用者(位图口径 `OQ-4-13` 已裁) | ADR-020 ✅(AC-4-19) |
| TR-interaction-012 | 4 无状态,清空重建结果相等 | ADR-005 ✅(AC-4-04) |
| TR-interaction-013 | 4 不接触玩法数值(零 `VitalsDto`/`disease_id`/…) | ADR-013 ✅(AC-4-05) |
| TR-interaction-014 | 四级消歧 + `KindPriority` 全序烘焙表,缺项构建期硬失败 | ADR-014 ⚠️ partial(GDD 内部 schema 无 ADR 半边) |
| TR-interaction-015 | 病人四路由语义归 8/10 词表,不归 4 | ADR-011 + ADR-013 ⚠️ partial(blocked_by 实现轮,**禁借绿**) |

## Stories

| # | Story | Type | Status | Layer | ADR |
|---|-------|------|--------|-------|-----|
| 001 | 边界纪律与程序集归属(零结算类型 / 零 `Append` 结构不可达 / 玩法数值隔离 / 无状态纯函数 / 不发档位) | Logic | Ready | Foundation | ADR-005 + ADR-021 + ADR-017 §二 |
| 002 | 确定性全序目标选择(F-4.1 三键 / `d∞` int64 加宽 / 等距必有一个胜者 / 重建幂等) | Logic | Ready | Core | ADR-006 + ADR-015 |
| 003 | 候选集四源构造与整数格输入(禁读表现态 / 经流确立格 / 第四源 `BakedInitial` / 格来源可追) | Integration | Ready | Core | ADR-016 + ADR-020 |
| 004 | POI 自报链路(`IDiscoveryReporter.Request` / 广播式非 argmin / 主动交互非碰撞 / 有界性 / `R_INTERACT` 单源) | Integration | Ready | Core | ADR-021 + ADR-009 |
| 005 | 模态门与路由边沿(`Accept` 两方向 / `ModalId` 引用非复制 / 丢弃不排队 / 路由表 / `Acquire/Release(Self)` / 上行缺口登记) | Logic | Ready | Feature | ADR-013 + ADR-011 |
| 006 | 数据契约构建期校验与呈现/手柄验收面(`4-DC-1…6` 夹具矩阵 / 身位即光标走查 / 零播报音 / 手柄无指针可选出) | Config-Data | Ready | Feature | ADR-014 + ADR-018 |

Counts: 3 Logic · 2 Integration · 1 Config-Data = 6 total(注:story 006 含 `[L]` 走查面,类型按主载体 Config-Data 登记;`AC-4-07/08/21` 的 Visual/Feel 属性在故事内显式标 `[L]`)。
**22 个 AC 全覆盖**:001 = `AC-4-01/02/04/05/11/12`;002 = `AC-4-06/14/16`;003 = `AC-4-03/20/22`;004 = `AC-4-13/17/18`;005 = `AC-4-09/10/19`;006 = `AC-4-07/08/15/21`。
**NOT-RUN/挂账硬清单**:`AC-4-06`(前置 = `4-DC-3` 构建期校验落地,归 story 006)/ `AC-4-15` ②(`ForageSpot`/`Container` 时长待 17 `OQ-17-3` / 20 登记;`PoiCell` 已结构性消解**不得并列记同一种红**)/ `AC-4-19`(载体 = 1 的位图,玩家 Epic Story 006;测试目录未建前记 NOT-RUN)/ `OQ-4-10` 上行通道**不存在** ⇒ 联机出境全链路 Out of Scope(归 45 轮 + ADR-001 窄修订,与 `OQ-10-9`/BL-4 同批,P1b 前)。

## Epic Status

**In Progress** — 6 stories created 2026-09-28;零 ADR-blocked story(`OQ-4-10` 是**登记缺口**而非裁决缺口,GDD 已裁「不打补丁、不自造通道」)。

## Key Cross-References

- **跨 Epic 硬依赖**:story 005 的 `AC-4-19` 依赖**玩家控制器 Epic Story 006**(`MotorSuppressed` per-source 位图,`LeaseSource.Self`;`OQ-4-13` 已裁归 1);反向解锁 = 1 的 `AC-1-23` 注「4 的 AC-4-19 随位图转可运行」
- **`OQ-4-10` 铁律**:4 **不得自行选一个通道填上**(「那会让『无』变成假 ✅」)—— 各 story 的上行面一律 Out of Scope 并指向 45 轮
- **禁借绿**:`TR-interaction-015` 的 `blocked_by: 实现轮` 不因本 Epic 的判据形状落地而撤销;`AC-4-06` 前置未落 ⇒ NOT-RUN 不得标 ✅(GDD 原文)
- **`R_INTERACT` 单源**:`AC-4-17` 同时锁 4 的选择与 6 的「已发现」触发于**同一烘焙字段**(两处各填一个数 = 静默脱钩)
- **广播式自报 ≠ argmin**(F-4.3b):POI 不因落选被吞;`T1 自报` 与 `定身` 是两条互不蕴含的路径(二轮解耦)
- **数值冻结**:全部取值(`R_INTERACT` / `KindPriority` 十项 / 时长)归用户;本 Epic 交付区间 + 注入假表夹具
- **载体纪律**:`tests/` CI 归 ADR-012 轮;文档路径 `tests/unit/interaction/` 为登记口径,真身落 `unity/Assets/Tests/`

## Next Step

Story 001(边界)与 006(`4-DC` 校验)先行 —— 006 是 `AC-4-06` 的硬前置;002/003/004 依「选择 → 输入 → 出境」链推进;005 待 1 侧位图落地后补 `AC-4-19`;`[L]` 三项待可玩构建的走查场次。
