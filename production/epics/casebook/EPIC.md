# Epic: 脉案

> **Layer**: Presentation(呈现)+ 分册态层(2026-09-19 用户裁定 [C] 升格 —— 三流之外的第四类数据持有者)
> **GDD**: design/gdd/casebook.md
> **Architecture Module**: L5 Presentation(`Gameplay.UI`)+ 分册态持久化段(经 7a / ADR-010 义务 13)
> **Status**: Ready
> **Stories**: 6 stories created(2026-09-28 `/create-stories`)

## Overview

脉案(39)是**那本纸**。它拥有脉案页的**布局**、**分册态持久化**(两个 tick · 分册 · 页码/折叠状态)
与病例列表的**呈现组织**,但它**不诊断、不结算、不渲染** —— 8 给词与形态、37 给病例生命周期、
42 给渲染与焦点引擎,39 把它们装订成玩家可以用笔写、用手翻的纸。

39 的身份特殊:它与 42 / 44 **不同构**。42 / 44 是「只渲染 / 只读」件套,39 是**全案唯一
「带持久化的呈现系统」** —— 它持有的两个 tick(快照 / 上次重查)与分册键**影响诊断与结算**
却**不进三流**,故必须在 ADR-010 的存档布局中占一个独立槽位(**分册态段**)。

三条硬纪律贯穿全部 story:① **排序与改写痕都是读时计算,零落盘**(F-39.1 / F-39.2 ——
落盘会造出第二真源);② **三条「绝不」**(不显示未结计数 / 不按病种分组排序检索 / 不提示同源)
—— 39 是 `AC-37-15` 类型面守卫之外的**能力面**落点,两者缺一泄漏面就打开;
③ **归纳是玩家的动作,不是界面的功能**(`AC-39-08`)。方笺(11)= 39 脉案「同一本书」
(`ADR-013 §十-B`),复用 `Casebook` 档,不成新模态。逐条数值(纸页容量 / 动画时长 / 焦点重复率)
归用户手感轮 —— 机制与契约已冻结。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-008: 病例事件流(第二条逻辑流) | 病例流**独立于病史流**;跨流全序键 `(Tick, StreamPriority, Patient, Seq)`;判断记录 Kind 族(读数 / 落笔 / 改写史);`disease_id` 不进呈现层(AC-37-15) | HIGH |
| ADR-010: 7a 持久化与存档格式 | 全二进制 codec;§三 义务表 39 行 + §一 布局「分册态段」槽(义务 13 = 分册态持久化);锚点 ① 脉案落笔触发 checkpoint | MEDIUM |
| ADR-013: 拟物 UI 框架 | UI Toolkit 主 + UGUI 补 world/XR;焦点单栈门;两栈共用同一 EventSystem;§十-B **方笺 = 39 同一本书**(复用 `Casebook` 档,不成新模态);`ModalId` 闭集七员 | HIGH |
| ADR-011: 输入架构 | 焦点导航**接口**归本 ADR、**呈现**归 R-6(ADR-013);手柄需完整可用 | HIGH |
| ADR-016: AI 架构 §六 | `IPresentPatients` 只读在场视图 —— 39 读 13,13 不引用 37(单向无环) | MEDIUM |
| ADR-020: 玩家控制器与相机 | `ICameraRig.SetMode(Casebook/Explore)`;39 = 该两档意图的**唯一请求方**;相机只读不持状态 | LOW |
| ADR-025: 契约程序集清单 | 分册态的整数半住 `Sim.Contracts`(`player_id` / tick),呈现 DTO 住 `Gameplay.UI` | LOW |
| ADR-001: 联机 pipe 抽象 | **联机落笔 = 判定输入,走可靠上行**(§一之三 裁决一);`player_id` 发号复用 `IIdAuthority`(ADR-006 Amend. B) | HIGH |
| ADR-024: 三流 Kind 单一登记真源 | 真源 = `entities.yaml`;`JudgmentRecorded` / `JudgmentRevised` 已具名在册 | LOW |

**Engine Risk**: **HIGH**(ADR-013 焦点桥 + ADR-008/001 的联机面)。39 自身的公式面为纯整数
排序与键派生(**LOW**,零 post-cutoff API);抬到 HIGH 的是 ① UI Toolkit 运行时手柄焦点
(ADR-013 §6.6 假设 6,⚠️ 半可信,原型 spike 为前置)② 联机落笔的意图上行(45 无 GDD)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-casebook-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-21 D-R3 回填实测):**8 条 = 4 covered + 2 partial + 2 gap**;
> **GDD Requirements Covered by ADRs = 6 / 8**(covered + partial);**Untraced = 2**。
> ⚠️ 两条 gap **不阻塞本 Epic 的 story**(均已由用户裁定「登记不立件」,挂各系统实现 / 修订轮):
> `TR-casebook-004`(联机落笔 Seq 上行 —— 随 ADR-001 窄修订 + 45 GDD 轮,P1b 前)、
> `TR-casebook-008`(`CasesOf` 具名接口无契约件 —— 待 37 修订轮)。
> `TR-casebook-002`(`player_id` 发号权威)= partial:registry 侧 `next_player_id` 已增列(第二十七批),
> 残 ② 45 铸造契约仍 open ⇒ **不得记绿**。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-casebook-001 | 分册态持久化 = ADR-010 义务 13 的落实 | ADR-010 ✅ |
| TR-casebook-002 | `player_id` 的发号权威(复用 `IIdAuthority`) | ADR-006 ⚠️ partial |
| TR-casebook-003 | `Judgment` 作者轴字段(落笔者 / 改写史)进病例流 | ADR-008 ✅ |
| TR-casebook-004 | 联机落笔的 `Seq` 由主机发号(意图上行) | ADR-001 ❌ gap |
| TR-casebook-005 | `SortKey` 读时派生零落盘(排序不写存档) | ADR-010 ✅ |
| TR-casebook-006 | 防间接泄漏的**能力面**(只能检索自己病人的投影) | ADR-008 + ADR-013 ⚠️ partial |
| TR-casebook-007 | 脉案近景经 `ICameraRig.SetMode`(不新增相机状态持有者) | ADR-020 ✅ |
| TR-casebook-008 | 37→39 `CasesOf` 具名接口无契约件 | — ❌ gap |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/casebook.md`(AC-39-01 … AC-39-14)are verified
- All Logic and Integration stories have passing test files in `tests/`
- All UI and Visual/Feel stories have evidence docs with sign-off in `production/qa/evidence/`
- ⚠️ `[L]` 类 AC(06b / 09 / 11 / 12 / 14)**不得由自动化绿替代** —— 须人工走查 / 听测 + 主创签核
- ⚠️ 依赖 45 联机夹具的 AC(04 ③)记 **BLOCKED-BY-Rn**,**禁借绿**

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 分册态数据契约与存档段 | Logic | Ready | ADR-010 + ADR-025 |
| 002 | 病例列表排序纯函数(F-39.1) | Logic | Ready | ADR-008 + ADR-010 |
| 003 | 分册键与改写痕读时投影(F-39.2) | Logic | Ready | ADR-008 + ADR-006 |
| 004 | 反幻想三条「绝不」的能力面守卫 | Logic | Ready | ADR-008 + ADR-013 |
| 005 | 脉案页布局与无指针焦点导航 | UI | Ready | ADR-013 + ADR-011 |
| 006 | 相机档位意图与落笔锚点钩子 | Integration | Ready | ADR-020 + ADR-010 + ADR-001 |
