# Epic: 物品与配方数据库

> **Layer**: Foundation
> **GDD**: design/gdd/item-database.md
> **Architecture Module**: L1 契约 + L2 schema(门 A 侧 DATA / SIM)
> **Status**: Ready
> **Stories**: 12 stories created (2026-09-23)

## Overview

物品与配方数据库(21a)是全案唯一零工作流依赖的 Foundation 系统:它定义**世界上存在哪些东西**以及**它们如何互相转化**。两张表构成全部内容 —— **物品表**以 `(base_id, processing_state)` 为复合主键(同一株植物的生料 / 干燥 / 提取物 / 酊剂是同一条 base 的四个条目,P1a 加中药只扩枚举不动结构);**配方表**为通用形状 `inputs[] → outputs[]`,炮制(单品加工)是 `n=1, m=1` 特例、制作(多料合成)是 `n>1` 特例,一张表同时喂下游。玩家永远不打开这个数据库,但 17 采集 / 18 炮制 / 11 处方 / 12 药物槽 / 16 中药选项 / 19 制作 / 20 库存 / 42 拟物 UI / 7a 持久化九个系统共用它这一套词汇表。本 Epic 覆盖架构 §5.3 的 **L1 契约 + L2 schema(DATA / SIM)**两层:定点纪律、事件载荷形状、存档编码归承重 ADR,逐条 schema 与数值归 GDD(数值用户自己调,机制值域冻结)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟与状态同步模型 | 全部模拟数学在整数定点域;病史事件流是唯一真源;主机唯一执行 Step / CatchUp;五个 P0 抽象点 | HIGH |
| ADR-006: 定点域边界数据契约 | FixParse 为外部数据→Fix 唯一入口(拒浮点字面量);存档与事件流禁 float;单一舍入 ROUND_HALF_AWAY_FROM_ZERO;守恒律整数域求值 | MEDIUM |
| ADR-008: 病例事件流(第二条逻辑流) | 跨流全序键 (Tick, StreamPriority, Patient, Seq);处置事件为证;盐 = WorldSeed 派生;病例流不物理折叠 | HIGH |
| ADR-009: 世界状态事件化边界 | 三问判据 · 三态分类;新增第三条逻辑流「世界流」;拾取 = 意图事件 + 当下判距 + 宽容半径;世界流不折叠 | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | 全二进制 codec(禁 JSON / PlayerPrefs);校验和 + 自动回退;checkpoint + 退出保存;版本号 + 迁移协议;义务汇总表单一出处 | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | 双级黄金夹具(单元级哈希 + 集成级字节);三格全免费常驻矩阵;版本化刷新 golden-vN | HIGH |
| ADR-014: 数据管线与 JSON 解析器 | 作者态 = assets/data/*.json;构建期烘成 deterministic *.cooked(玩家构建零 JSON 解析器);Newtonsoft 仅词法 + 自研绑定 + FixParse;Fix 字段 JSON 写字符串 | MEDIUM |
| ADR-015: 世界几何(手工烘焙固定世界) | 手工烘焙固定世界;WorldPos = 单一整数格;建造模块化网格与地形共用同一格;第三方工具仅编辑期 | LOW |
| ADR-024: 三流 Kind 单一登记真源 | 真源 = entities.yaml(禁从散文解析);构建期生成器 tools/kindgen + 断言 A1–A5;Amendment 追加通道退役 | LOW |

**Engine Risk**: **HIGH**(ADR-005 / ADR-008 / ADR-012 并列最高)。三者均为文档裁决、刻意不依赖 post-cutoff API,但其验证义务(IL2CPP 逐位性 / 病例流折叠实现 / 黄金夹具矩阵)是 HIGH 域实测前置。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-itemdb-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-23 实测):**32 条 = 20 covered + 1 partial + 11 no-adr-by-design + 0 gap**;
> **GDD Requirements Covered by ADRs = 21 / 32**(covered+partial);**Untraced = None**。
> 11 条 ◆ 为范围 / 政策声明(2026-09-23 QQ-08 路线 [A] 逐簇裁定降级),结构上无裁决可挂,不计缺口、不阻塞 story。
> 0 gap —— 原 Foundation 唯一 gap `TR-itemdb-031` 已于 2026-09-23 ADR-009 TR 复核轮 gap→covered。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-itemdb-001 | 物品数值在整数定点域(Q16.16) | ADR-005 ✅ |
| TR-itemdb-002 | weight / stack_max 为 int 计数,移出 Fix 解析集(D-21-17) | ADR-006 ✅ |
| TR-itemdb-003 | Fix 不可经 Unity 内置序列化器承载(D-21-18) | ADR-006 ✅ |
| TR-itemdb-004 | 守恒律在整数域内求值,weight 归一(D-21-19) | ADR-006 ✅ |
| TR-itemdb-005 | FixParse 为外部数据 → Fix 的唯一入口,拒浮点字面量 | ADR-006 ✅ |
| TR-itemdb-006 | 单一舍入模式 ROUND_HALF_AWAY_FROM_ZERO | ADR-006 ✅ |
| TR-itemdb-007 | 配方 schema(输入/产出/工时) | ◆ no-adr-by-design |
| TR-itemdb-008 | 药材 schema(性味/归经/功效键) | ◆ no-adr-by-design |
| TR-itemdb-009 | 炮制方法 schema | ◆ no-adr-by-design |
| TR-itemdb-010 | 药物 potency / half_life 字段定义 | ADR-006 ⚠️ partial |
| TR-itemdb-011 | Schema E:库存槽形状 | ◆ no-adr-by-design |
| TR-itemdb-012 | ActualConsumed 仅派生自 Craft 事件 | ◆ no-adr-by-design |
| TR-itemdb-013 | 配方产出在定点域内确定性可重放 | ADR-005 ✅ |
| TR-itemdb-014 | Craft 事件的载荷形状 | ADR-009 + ADR-024 ✅ |
| TR-itemdb-015 | 可感知底线 vs 9 的噪声带对齐 | ◆ no-adr-by-design |
| TR-itemdb-016 | 品质分级与品质轴定义 | ◆ no-adr-by-design |
| TR-itemdb-017 | 21a 与 17 采集系统的数据边界 | ◆ no-adr-by-design |
| TR-itemdb-018 | 9 侧字段名对齐 drug_potency / half_life | ADR-006 ✅ |
| TR-itemdb-019 | instance_id 权威(IIdAuthority ItemInstanceId Next()) | ADR-010 ✅ |
| TR-itemdb-020 | D-21-27:仅主机铸币 | ADR-005 + ADR-009 ✅ |
| TR-itemdb-021 | D-21-28:Craft 事件的全序键 | ADR-008 ✅ |
| TR-itemdb-022 | AC-21a-29 跨平台确定性 | ADR-012 ✅ |
| TR-itemdb-023 | 物品数据在存档中的编码 | ADR-010 ✅ |
| TR-itemdb-024 | 配方解锁与技能门(与 30 的接口) | ◆ no-adr-by-design |
| TR-itemdb-025 | 数据文件格式与位置(assets/data/*.json) | ADR-014 ✅ |
| TR-itemdb-026 | Addressables 分组与预载 | ADR-014 ✅ |
| TR-itemdb-027 | 构建期 schema 校验 | ADR-014 ✅ |
| TR-itemdb-028 | axis_offset_by_quality[] 可为负 ⇒ 负数舍入方向须定义 | ADR-006 ✅ |
| TR-itemdb-029 | Σ(weight × InstanceWeight) 的 int64 溢出上限 | ◆ no-adr-by-design |
| TR-itemdb-030 | 21a 与 20 库存的所有权边界 | ◆ no-adr-by-design |
| TR-itemdb-031 | 掉落实体的世界状态事件化边界 | ADR-009 + ADR-015 ✅ |
| TR-itemdb-032 | 配置版本号与存档头联动 | ADR-010 + ADR-014 ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | FixParse 边界契约 | Logic | Complete | ADR-006 + ADR-005 |
| 002 | Schema 类型与复合主键 | Logic | Complete | ADR-014 + ADR-006 |
| 003 | 配方结算求解器 | Logic | Complete ✅ VERIFIED 2026-09-24 | ADR-005 + ADR-006 |
| 004 | F4 堆叠重量与 F5 品级时间轴 | Logic | Complete | ADR-006 |
| 005 | 守恒律构建期与运行期门 | Logic | Complete | ADR-006 |
| 006 | 配方表写入期校验套件 | Logic | Complete ✅ VERIFIED 2026-09-24 | ADR-014 |
| 007 | 物品表写入期校验套件 | Logic | Ready | ADR-014 |
| 008 | 数据管线烘焙与 Addressables 预载 | Integration | Ready | ADR-014 + ADR-010 |
| 009 | Craft 事件载荷与全序键 | Integration | Ready | ADR-009 + ADR-024 + ADR-008 |
| 010 | 实例权威与持久化往返 | Integration | Ready | ADR-010 + ADR-005 + ADR-006 |
| 011 | 跨平台确定性黄金夹具 | Logic | Ready | ADR-012 + ADR-005 |
| 012 | 呈现契约合规走查 | UI | Ready | ADR: N/A — 呈现归 ADR-013/42，非架构裁决 |

Counts: 8 Logic · 3 Integration · 1 UI = 12 total.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/item-database.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/story-readiness production/epics/item-database/story-001-fix-parse-boundary-contract.md` to begin implementation, or `/create-stories [next-epic-slug]` for the next Foundation epic.
