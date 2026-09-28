# Epic: 医疗后果与责任

> **Layer**: Core(sim 侧结算 · 门 A 程序集 `Sim`)+ Presentation(回响呈现,经 42/44/13)
> **GDD**: design/gdd/medical-consequences.md(GDD 状态:Draft,待结案模式收口 —— 机制已冻结部分可拆 story,承重未裁项逐条标注)
> **Architecture Module**: L2 Sim(单例后果结算纯函数)+ 世界流写入者(`ConsequenceResolved`)
> **Status**: Ready
> **Stories**: 4 stories created(2026-09-28 `/create-stories`)

## Overview

医疗后果与责任(53)是**水龙头**:单个病例结案后的即时后果由它结算,村落层面的长期回响(蓄水池)
是 31 的 P1a 订阅 —— 53 对 31 **零引用**(AC-53-12 asmdef 引用集断言)。53 是**全案唯一正确性判准**
(37 无 correctness 字段,AC-53-08):`Outcome = Judge(P, C, D, U)` —— P = 判断记录投影@结案、
C = 处置记录投影(病史流)、D = 9 的病程投影@Tick(结案)(对因手柄门命中)、U = 盐派生掷骰
(`SplitMix64(WorldSeed, "case-salt", salted_key)`,salted_key 来自 `PatternRecognizedPayload`,
**不含裸 disease_id**)。**双轴裁定**:结局轴(病人在/走)由 9 结算,53 只读、永不判死;
致死唯一路径 = 9 的对因手柄门(支柱二)。**误诊自愈 = 无痕即无回响**;回响轴 = 判断错误的物理痕迹
(后遗 / 复现 / 试药史)。Outcome 是**枚举不是分数**(AC-53-01);回响**延迟发出且不可归因**
(payload 零 case_id,AC-53-07);`ConsequenceResolved` 落**世界流**(OQ-53-7 已裁直登 registry)。
逐条数值(`DelayTable` / 试药史阈值等)归用户数值轮 —— 机制与值域冻结,`DelayTable[outcome] > 0`
是硬约束(OQ-53-2 值未裁,已登记)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟 | 结算 = 事件流的纯函数;禁浮点(门 A);跨流全序聚合 | HIGH |
| ADR-006: 定点域边界 | 存档/流禁 float;舍入单一模式 | MEDIUM |
| ADR-007: 事件权威与掷骰 | U 的每个输入可从流重构;`SplitMix64` 唯一 RNG;盐派生口径 | LOW |
| ADR-008: 病例事件流 | P/C 投影来源;跨流全序键 `(Tick, StreamPriority, Patient, Seq)`;`PatternRecognized` 具名 | HIGH |
| ADR-009: 世界状态事件化边界 | 后果状态 = 模拟态进世界流;`ConsequenceResolved` Kind(OQ-53-7 直登通道) | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | AC-53-02/15 黄金夹具(Outcome 逐位一致);SplitMix64 case-salt 单元夹具;F7 溢出 spike | HIGH |
| ADR-016: AI 架构 §一 | 三源不变量(结算输入 ∈ {流, 烘焙数据, 二者纯函数});53 零可变态 | MEDIUM |
| ADR-024: Kind 单一登记真源 | `ConsequenceResolved` 直登 `entities.yaml`(stream: world, author: 53,载荷 ∈ 整数域) | LOW |

**Engine Risk**: **HIGH**(ADR-012 实测域)。53 自身为纯 C# 整数结算(**LOW**,零 post-cutoff API);
承重 = IL2CPP 逐位一致黄金夹具(AC-53-02/15)—— **三格矩阵今日未跑(禁借绿)**。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-medcons-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-21 D-R3 回填实测):**8 条 = 4 covered + 1 partial + 3 gap**;
> **GDD Requirements Covered by ADRs = 5 / 8**;**Untraced(gap)= 3**。
> ⚠️ gap 中两条主照 P1a 登记(`TR-medcons-005` 入向闭集反渗 / `006` DelayTicks>0 反渗形状 ——
> P0 载体由 GDD 自身 AC 落地)、一条 P1a 主照登(`008` RegionOutcome 载体,F-53.3 整体 P1a,
> **不在本 epic 范围**)。`TR-medcons-007`(零可变态零订阅)= partial,绑 OQ-53-1(疫区持续状态,P1a)。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-medcons-001 | `ConsequenceResolved` 进世界流 | ADR-005+009 ✅ |
| TR-medcons-002 | 结算 = 三源纯函数可重建 | ADR-005+016 ✅ |
| TR-medcons-003 | U 自算(SplitMix64 盐派生) | ADR-007 ✅ |
| TR-medcons-004 | 跨流聚合按全序非到达序 | ADR-008 ✅ |
| TR-medcons-005 | 入向闭集反渗守卫 | — ❌ gap(P1a 主照登) |
| TR-medcons-006 | DelayTicks > 0 反渗形状 | — ❌ gap(P1a 注) |
| TR-medcons-007 | 零可变态零订阅(31) | ADR-016 ⚠️ partial(OQ-53-1) |
| TR-medcons-008 | RegionOutcome 载体 | — ❌ gap(P1a,F-53.3 整体延后) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All P0 acceptance criteria from `design/gdd/medical-consequences.md`(AC-53-01 … AC-53-15,除 P1a 项)are verified
- AC-53-02/15 黄金夹具在 ADR-012 三格矩阵刷新并常驻绿(实测未跑 ⇒ 禁借绿)
- AC-53-09 [L] 走查(延迟 + 不可归因 + 病人可见 + ≥1 在场者先见)有主创签核
- ⚠️ 未裁数值(`DelayTable` 具体值,OQ-53-2)与载体预裁(OQ-53-6 承载者)按登记口径:**机制实现照跑、值/文案留数值轮**,不阻塞 story
- F-53.3 RegionOutcome / 村落信任 / 31 订阅 = P1a,**不在** DoD

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 入向闭集与跨流聚合 | Logic | Ready | ADR-008 + ADR-005 |
| 002 | F-53.1 确定性结算(Outcome 枚举) | Logic | Ready | ADR-007 + ADR-012 |
| 003 | F-53.2 延迟发出与 ConsequenceResolved 世界流 | Logic | Ready | ADR-009 + ADR-024 |
| 004 | 回响呈现世界内与三案链集成走查 | Integration | Ready | ADR-013 + ADR-018 + ADR-016 |
