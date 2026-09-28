# Epic: 库存与物品

> **Layer**: Core
> **GDD**: design/gdd/inventory-and-items.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 世界流纯投影消费者
> **Status**: Ready
> **Stories**: 6 stories — see table below

## Overview

库存与物品(20)是全案物品流转的**库房而非医生**:库存不是被存储的状态,而是**世界流的纯投影** —— `InventoryOf(player) = fold(世界流, {DropSpawned, DropClaimed, DropDespawned, StructureRemoved, Craft.ActualConsumed, StructurePlaced, ResourceHarvested, PoiLoot? 域=世界流})`(ResourceHarvested 并入为 17 R-注⑧ 义务,否则品级重建不出);内存索引 = 优化非真相,清空重建须与投影逐位相同(AC-20-03)。id 铸造权 = 主机唯一 `IIdAuthority.ItemInstanceId.Next()`(机制 A:计数器永不复位 + `next = max(id)+1` 由三流 ∪ `BakedInitial` 重构,ADR-006 Amendment B / ADR-010 §五),客户端零自铸;堆叠键 `(item_key, quality)` 唯一出处 21a;载重 `CarryLoad = Σ weight×qty`(int64 先乘后加,weight 是 int 非 Fix,D-21-17),`CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP` 配硬不变量 `∀p: CarryLoad ≤ CARRY_CAP`(52 归一化定义域);**20 不发明结算**(零 EFF/QualityMod/quality_distribution 求值)、**零追加 Kind**(消耗/转移全部复用既有,AC-20-17,OQ-20-1 已裁)、**无负确认**(被拒 = 流零事件,AC-20-25)。容器 = 药箱(UI Toolkit 平面模态 ③,ModalId 闭集含 `InventoryContainer`,承 ADR-013 Amendment B 七员;42 元件库「器具」第三材质档木/皮/铜已裁;P0 禁嵌套容器);摆放序 = `instance_id` 升序确定性函数(「入箱序」废止,BL-22);呈现 = 满溢四档定性(空/半满/近满/满合盖受阻)禁数字禁负重条,`CarryRatioDto = {carry_load:int, cap:int}` 单调契约「声明不加形」(BL-27)。承重 ADR:ADR-009/010/006/005/013/014/024。`CARRY_CAP` 等值归用户数值轮(未定时装载硬失败,AC-20-13);多条 R 前置(R2 `DropDespawned` 载荷、R3 高水位基线、R9 42 五子项等)以 BLOCKED-BY 原样保留在故事判据上,禁借绿。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-009: 世界状态事件化边界 | 库存 = 世界流投影(模拟态进流/派生态重建);三流全序键 | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | §五 机制 A `ItemInstanceId.Next()`;义务 9/10 ItemInstance 序列化;快照=优化非真相 | MEDIUM |
| ADR-006: 定点域边界数据契约 | weight/stack_max 是 int(D-21-17);禁 float 入存档与事件流;高水位扫三流并集 | MEDIUM |
| ADR-005: 确定性模拟与状态同步模型 | 事件流唯一真源;主机唯一 Append;整批原子性(部分成功中间态不可序列化) | HIGH |
| ADR-013: 拟物 UI 框架 | ModalId 闭集 7 员含 InventoryContainer;42 只渲染永不持状态;焦点单栈门 | HIGH |
| ADR-014: 数据管线与 JSON 解析器 | 物品定义烘焙;CARRY_CAP 未定值 ⇒ 装载硬失败(E-13) | MEDIUM |
| ADR-024: 三流 Kind 单一登记真源 | 消耗/转移复用既有 Kind 零追加(registry 无新增即证据) | LOW |
| ADR-001: 联机 pipe 抽象 | 意图上行通道(R1)未立 —— P0 单机不阻塞,P1b 前硬前置 | MEDIUM |

**Engine Risk**: **HIGH**(挂 ADR-005/013)。20 的 fold/载重/id 本体为门 A 纯 C#(LOW);呈现半边(药箱模态、四档、翻页)触 UI Toolkit 6.3 与焦点桥(须 spike,承 ADR-013 §6.6 假设 6);跨平台重建逐位归 ADR-012 矩阵(EXTERNAL)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-inventory-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-28 实测):**16 条 = 11 covered + 1 partial + 4 gap**。
> partial:`TR-inventory-010`(溢出界构建门值班方未裁)。
> gap:`TR-inventory-004`(堆叠键唯一出处无 ADR)、`TR-inventory-006`(CanCarry 硬不变量,CARRY_CAP 值未裁)、
> `TR-inventory-007`(20 不求值纪律无 ADR)、`TR-inventory-015`(容器折重 OQ-20-4 —— ✅ 注:GDD 侧已结清「空箱自重+子件逐件」,registry 状态待翻,登记层卫生义务)、`TR-inventory-016`(联机粒度 OQ-20-5,P1b)。
> 数值(`CARRY_CAP` / `weight` 表 / `stack_max`)归用户数值轮;`AC-20-13` 保证未定值期间装载硬失败。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-inventory-001 | 库存 = 世界流纯投影(fold);快照 = 优化非真相 | ADR-009+010 ✅ |
| TR-inventory-002 | instance_id 铸造权 = 主机唯一;客户端不自铸 | ADR-005+001 ✅ |
| TR-inventory-003 | 高水位重构 next = max(id)+1,扫三流并集 | ADR-006 ✅ |
| TR-inventory-004 | 堆叠键 = (item_key, quality) 唯一出处 21a;20 不改写 | — ❌ gap |
| TR-inventory-005 | CarryLoad = Σ weight×qty 整数先乘后加 | ADR-006 ✅ |
| TR-inventory-006 | CanCarry 谓词 + ∀p CarryLoad ≤ CARRY_CAP 硬不变量 | — ❌ gap |
| TR-inventory-007 | 20 不发明结算:零 EFF/QualityMod/分布求值 | — ❌ gap |
| TR-inventory-008 | 存取原子性:容量不足整体拒绝,无半途状态 | ADR-005 ✅ |
| TR-inventory-009 | 20 不接触战斗与病程,只交读数 | ADR-009 ✅ |
| TR-inventory-010 | 溢出界 < 2^31 烘焙期常量,构建期硬门 | ADR-014 ⚠️ partial |
| TR-inventory-011 | 载重呈现 = 满溢四档定性,零负重条/百分比/排序 | ADR-013 ✅ |
| TR-inventory-012 | 20 全部输出 DTO 递归反射无 float | ADR-006 ✅ |
| TR-inventory-013 | CARRY_CAP 未定值 ⇒ 装载期硬失败 | ADR-014 ✅ |
| TR-inventory-014 | 消耗/转移 Kind 全部复用(Craft/DropDespawned/Structure*),零追加 | ADR-009 ✅ |
| TR-inventory-015 | 容器折重语义(空箱自重 + 子件逐件,OQ-20-4 已结清) | — ❌ gap(登记待翻) |
| TR-inventory-016 | 联机库存同步粒度(P1b 归 45) | ADR-001 ❌ gap |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/inventory-and-items.md`(AC-20-01…25)are verified
- BLOCKED-BY 项(AC-20-03 / 16 / 20 等,R 前置清单)按判据原文处置,前置未回写前不得记绿
- Logic/Integration 测试全绿;[L] 走查(药箱满溢感、音效零播报、色盲非颜色通道)有证据归档
- 数值轮交付 `CARRY_CAP` 等实际值后,存在性门 AC-20-21 与不变量测试翻真绿

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | InventoryOf 世界流投影与清空重建等值 | Logic | Ready | ADR-009/005/010 |
| 002 | instance_id 铸造权威与高水位重构 | Logic | Ready | ADR-006/010 |
| 003 | 堆叠、容器折重与零追加 Kind | Logic | Ready | ADR-014/024/006 |
| 004 | 载重裁决 CanCarry 与硬不变量 | Logic | Ready | ADR-006/014 |
| 005 | 拾取/消耗原子性与无负确认 | Integration | Ready | ADR-005/011/024 |
| 006 | 药箱容器摆放、翻页遍历与满溢呈现 | UI | Ready | ADR-013/018 |
