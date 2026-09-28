# Epic: 采集

> **Layer**: Core
> **GDD**: design/gdd/foraging.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 世界流写入者
> **Status**: Ready
> **Stories**: 5 stories — see table below

## Overview

采集(17)把「采什么、什么时候采、采到的成色」变成一种判断玩法:玩家在世界整数格上的资源节点(柳树皮 / 毛地黄 / 金鸡纳树皮 / 止血草四品种 + 火堆灰烬的草木灰节点)发起采集,系统经 `SplitMix64(WorldSeed, "gather", node_id, gather_seq)` + `CDFWalk(quality_distribution, U)` 抽出原始品级,再按采集技能档位表 `QualityCap = CapTable[QueryLevel(采集)]` 钳制成 `out_quality`;节点的剩余可采量 `remaining(node, t)` 是**世界流前缀的整数纯函数**(F-17-3:季节乘子 × 产量衰减),采前闸「余量不足 ⇒ 零事件」;每次成功采集恰好发三条**既有**世界流 Kind(`ResourceHarvested` + `DropSpawned` + `DropClaimed`,零新增 Kind,ADR-009 Amendment K)并恰好一条 `SkillGrown`(对象 = 品种)。全部数学在整数定点域(Q16.16,零 float),`gather_seq` / 余量 / 掷骰输入均可从事件流重构(ADR-007 三源不变量)。承重 ADR:ADR-005/006/007/009/014/024。逐条数值(`GATHER_MUL_CAP` / `DECAY_RATE` / `SEASON_MULT[]` / `quality_distribution` 权重)归用户数值轮 —— 机制与值域冻结,epic 不因此阻塞;`AC-17-02` 跨平台逐位重放挂 ADR-012 三格矩阵(EXTERNAL,不得记绿)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟与状态同步模型 | 全部模拟数学在整数定点域(int64 / Q16.16);事件流唯一真源;主机唯一执行 Step / Append | HIGH |
| ADR-006: 定点域边界数据契约 | FixParse 唯一解析入口;ROUND_HALF_AWAY_FROM_ZERO 单一舍入;weight/stack_max 是 int 非 Fix | MEDIUM |
| ADR-007: 事件权威与掷骰状态 | 掷骰每个输入必须可从事件流重构;SplitMix64;IEventAuthority = 第六抽象点 | LOW |
| ADR-009: 世界状态事件化边界 | 三流全序键;Amendment K 定 `ResourceHarvested` 载荷归 17 GDD;掉落身份进流 | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | §五 机制 A `ItemInstanceId.Next()`;义务 9/10 ItemInstance 序列化 | MEDIUM |
| ADR-014: 数据管线与 JSON 解析器 | 作者态 JSON → 构建期烘 *.cooked;Fix 字段写字符串;装载期硬失败 | MEDIUM |
| ADR-024: 三流 Kind 单一登记真源 | 真源 = entities.yaml;kindgen 断言 A1–A5;17 零新增 Kind(复用三支) | LOW |

**Engine Risk**: **HIGH**(ADR-005 域)。17 侧判据本体为纯整数 C#(LOW~MEDIUM),但 `AC-17-02` 的跨平台逐位重放依赖 ADR-012 三格黄金夹具矩阵 —— 矩阵尚不存在且 F7(`SplitMix64` 在 IL2CPP 的有符号溢出 UB)spike 未跑 ⇒ 该 AC 为 **EXTERNAL · BLOCKED-BY-ADR-012**,story 不得记绿。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-foraging-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-28 实测):**8 条 = 5 covered + 2 partial + 1 gap**。
> partial:`TR-foraging-004`(CDF walk 夹具算子未入 ADR-012 清单,OQ-17-10)、
> `TR-foraging-006`(OQ-17-5 缓存键实现落点未裁)、`TR-foraging-007`(OQ-17-7 驻留/切片未裁)。
> gap:`TR-foraging-008`(联机意图上行须 ADR-001 窄修订,P1b 前硬前置,与 10/39/48 同簇)。
> 数值(`GATHER_MUL_CAP` / 半衰期 / 权重表)归用户数值轮 —— 机制值域冻结。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-foraging-001 | 采集只发三个世界流 Kind(不多发不漏发;节点状态即流内派生态) | ADR-009 ✅ covered |
| TR-foraging-002 | gather_seq = 事件流内采集计数(可重构,非运行时计数器) | ADR-009 ✅ covered |
| TR-foraging-003 | 品级掷骰的全部输入可从事件流重构(零隐藏随机态) | ADR-007 ✅ covered |
| TR-foraging-004 | 采集散布的 CDF walk 算子以定点实现并绑 ADR-012 黄金夹具 | ADR-012+006 ⚠️ partial |
| TR-foraging-005 | 采集掉落 instance_id 由主机经 IIdAuthority 铸造(客户端不铸) | ADR-007 ✅ covered |
| TR-foraging-006 | 资源节点余量 = 前缀函数(历史采集事件序列的整数纯函数) | ADR-009 ⚠️ partial |
| TR-foraging-007 | 采集资源点数据的 IDataProvider 驻留/切片策略 | ADR-014 ⚠️ partial |
| TR-foraging-008 | 联机采集意图的上行通道须 ADR-001 一次窄修订 | ADR-001 ❌ gap |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/foraging.md` (AC-17-01…17) are verified
- All Logic and Integration stories have passing test files
- All [L](人工走查)AC 有人工走查/听测证据(标注 EXTERNAL / BLOCKED-BY 者按判据原文处置,不得记绿)
- 数值轮交付 `quality_distribution` / `SEASON_MULT` / `CapTable` / `GATHER_MUL_CAP` 等实际值后,装载校验与差分夹具全绿

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 资源点与调参表装载 + 装载期硬失败 | Logic | Ready | ADR-014/006 |
| 002 | 品级抽取管线 F-17-1(掷骰 + CDFWalk + QualityCap) | Logic | Ready | ADR-007/005/006 |
| 003 | 三条世界流事件 + 主机铸造 + 恰好一条 SkillGrown | Integration | Ready | ADR-009/010/024 |
| 004 | 节点余量前缀函数 F-17-3 + 采前闸 | Logic | Ready | ADR-009/005 |
| 005 | 动作原子性、失败收敛与呈现/浮点反射门 | Integration | Ready | ADR-011/018/006 |
