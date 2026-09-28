# Epic: 炮制

> **Layer**: Core
> **GDD**: design/gdd/processing.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 世界流写入者(Craft)
> **Status**: Ready
> **Stories**: 5 stories — see table below

## Overview

炮制(18)把「药材 → 成药」做成一条**不发明结算**的流水线:F1/F2 唯一配方求解器住 21a(item-database),18 只做四件事 —— ① 筛 `Recipe.owner = process` 子集;② 供四个修正项输入(`SkillMod` 传等级 int 不传结果、`QualityMod` = 被点名实例集取 `min(quality)`、`EquipMod` 读 24、`EnvMod` 透传 `EnvMod_climate` + `EnvMod_clinic` 两个未钳制分量);③ 调 F1 **一次**拿三出参(`ActualConsumed` / `OutputQty` / `OutputQuality`);④ 交 20 原子 `Apply`。时序 = 「发起即落流点火 tick」:`Craft` 世界流事件载荷十位(含 `output_instance_ids[]` 于点火 tick 由主机依全序键铸造),完成 tick = `start + duration_ticks` 纯 tick 运算(零墙钟零浮点),**无进程态可存**(18 程序集零 `[Serializable]`,重放等值)。准入两门 `Admissible ⟺ QueryLevel ≥ skill_gate ∧ min(quality) ≥ min_quality`,主机终裁、客户端预答仅呈现、同一谓词零第二实现。单炉不可取消、跨玩家并发允许;起货溢出走「当场本格 `DropSpawned`」[甲]② 裁决。承重 ADR:ADR-014(唯一求解器管线)/ ADR-005/006(定点与守恒)/ ADR-009+Amendment J(`Craft` 落世界流与全序键)/ ADR-010(事件流即全部持久化)/ ADR-011 Amendment B(判定归主机)。`duration_ticks` 等逐条数值归用户数值轮;`AC-18-15` 跨平台逐位重放 EXTERNAL·BLOCKED-BY-ADR-012;`AC-18-18/19` EXTERNAL·BLOCKED-BY-21a 回写义务(O-18-R1/R3)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟与状态同步模型 | 全数学整数定点;事件流唯一真源;主机唯一 Step/Append;完成=派生 ⇒ 无进程态 | HIGH |
| ADR-006: 定点域边界数据契约 | 守恒律整数域求值;单一舍入;18 侧零 Ceil/Round/Clamp 结算路径 | MEDIUM |
| ADR-009: 世界状态事件化边界 | `Craft` 属世界流;Amendment J 定三流全序键与载荷纪律 | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | 全二进制 codec;「发起即落流」= 7a 只存事件流,无 18 段 | MEDIUM |
| ADR-011: 输入架构(Amendment B) | 客户端聚合意图上行 → 主机终裁 Judge+Append+发号;本地=预表现 | HIGH |
| ADR-014: 数据管线与 JSON 解析器 | F1/F2 住 21a;18 供料、21 定曲线;装载期硬失败 | MEDIUM |
| ADR-018: 音频架构 | 18 = cue 发射方;sfx_process_*;炉火声床禁 sting/倒数感 | MEDIUM |
| ADR-024: 三流 Kind 单一登记真源 | `Craft` 已在 registry;载荷字段 ⊆ 整数域(kindgen A2) | LOW |

**Engine Risk**: **HIGH**(挂 ADR-005/011/012 域)。18 侧判据本体为纯 C# 整数契约(LOW~MEDIUM);`AC-18-15` 跨平台逐位重放依赖 ADR-012 三格矩阵(不存在)+ F7 spike(未跑)⇒ EXTERNAL 不得记绿;`AC-18-18/19` 依赖 21a 回写(O-18-R1/R3 未落)⇒ EXTERNAL;数值(`duration_ticks` / 四 cap / `min_quality` 值)归用户数值轮。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-processing-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-28 实测):**18 条 = 10 covered + 5 partial + 3 gap**。
> gap:`TR-processing-008`(F-18.1 调用契约形状无 ADR)、`TR-processing-010`(产出非零结构性)、
> `TR-processing-013`(18 不认识药)。partial:`TR-processing-001`(唯一求解器跨 GDD 纪律无 ADR 层裁决)、
> `TR-processing-003`(newtype `SkillLevel` = D-21-33 前置未落)、`TR-processing-005`(准入谓词无 ADR 承接)、
> `TR-processing-014`(21a 侧执行 AC 未写)、`TR-processing-015`(OQ-18-8 器具互斥归 24)。
> ⚠️ 2026-09-28 实测口径与 GDD 自述计数若有出入,以 registry 现值为准(tr 复核轮义务)。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-processing-001 | 18 不发明结算 —— F1 唯一求解器住 21a,18 是调用方,零第二实现 | ADR-014 ⚠️ partial |
| TR-processing-002 | 18 代码路径不得求值 Ceil/Round/clamp 结算算式 | ADR-006 ✅ covered |
| TR-processing-003 | SkillMod 传 Level 不传结果(前置 = 21a D-21-33 具名 newtype) | — ⚠️ partial |
| TR-processing-004 | 炮制子集判据 = Recipe.owner 显式字段(21a D-21-30) | ADR-014 ✅ covered |
| TR-processing-005 | 准入两道门:skill_gate 门槛 + min_quality 准入闸(不改药效) | — ⚠️ partial |
| TR-processing-006 | 四修正项输入侧:18 供料,21a 定曲线 | ADR-014 ✅ covered |
| TR-processing-007 | EnvMod 可为负,18 原样透传,不得 clamp 到 0 | ADR-006 ✅ covered |
| TR-processing-008 | 调 F1 一次拿三出参(F-18.1 调用契约,零新数学) | — ❌ gap |
| TR-processing-009 | ActualConsumed 随 Craft 事件落世界流;18 是唯一产生者 | ADR-009 ✅ covered |
| TR-processing-010 | 产出非零是结构(max(1,·)),18 不得加失败判定 | — ❌ gap |
| TR-processing-011 | 与 20 的原子性:容量不足整体拒绝,不得先扣后补 | ADR-005 ✅ covered |
| TR-processing-012 | 完成时点 = Tick(start)+duration_ticks,零墙钟零浮点 | ADR-005 ✅ covered |
| TR-processing-013 | 18 不认识药 —— 零 drug_profile/polarity/treatable_by 引用 | — ❌ gap |
| TR-processing-014 | 四 cap ↔ QTY_MULT_MAX 构建期校验,执行方归 18(防御半边) | ADR-014 ⚠️ partial |
| TR-processing-015 | 并发模型:每玩家单炉、不可取消、拒绝零事件、跨玩家并发 | — ⚠️ partial |
| TR-processing-016 | 炮制中途存档语义:发起即落流,完成=派生,无进程态 | ADR-010 ✅ covered |
| TR-processing-017 | 同参数跨平台重放 ⇒ 三出参逐位相同 | ADR-012 ✅ covered(执行载体未建,AC EXTERNAL) |
| TR-processing-018 | 参数装载期硬失败,禁 null 兜底 | ADR-014 ✅ covered |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/processing.md`(AC-18-01…25)are verified
- All Logic/Integration stories have passing tests;[L] 走查/听测项有 `production/qa/evidence/` 归档
- EXTERNAL 项(AC-18-15 / 18 / 19 / 10b)按其判据原文处置,矩阵/回写/42 落地前**不得记绿**
- 数值轮交付 `duration_ticks` / 四 cap / `min_quality` / `skill_gate` 实际值后,装载校验与配方表全绿

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 配方子集筛选与准入谓词 Admissible | Logic | Ready | ADR-014/011 |
| 002 | F1 调用契约与四修正项供料(spy 可证) | Logic | Ready | ADR-014/006 |
| 003 | Craft 事件载荷十字 + 点火即落流 + 序键铸造 | Integration | Ready | ADR-009/010/024/006 |
| 004 | 时序执行:单炉并发模型、无进程态、重放等值 | Logic | Ready | ADR-005/010 |
| 005 | 完成调度、溢出落地与等待期音频护栏 | Integration | Ready | ADR-018/013 |
