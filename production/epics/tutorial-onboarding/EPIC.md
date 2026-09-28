# Epic: 教学与引导

> **Layer**: Presentation(呈现层第四件,与 42 / 44 / 2 同构 —— 只渲染,永不持有游戏状态)
> **GDD**: design/gdd/tutorial-and-onboarding.md
> **Architecture Module**: L5 Presentation(`Gameplay.UI`)+ 边界层只读投影(Proj)+ Tooling 校验(ADR-014 阶段 2)
> **Status**: Ready
> **Stories**: 5 stories created(2026-09-28 `/create-stories`)

## Overview

教学与引导(48)让新手学会**方法**而不是背**答案**:六步(观察→判断→手段→收尾→册子→经营)
以三种媒体(NPC 口述 + 字幕 / 13 示范一次性片段 / 世界内的纸)传达,进度**从事件流读时投影**
—— 48 零持久化(派生不存储,ADR-009 Q1)、零设门(教学期不锁任何玩法,AC-48-17 反射断言)、
**零按键提示叠加层**(第一铁律:禁止形态表全项构建期扫描,教学期豁免已被用户裁定否决)。
完成判定 = 谓词(投影)单调读时求值 + 边沿闩(F-48.1);冷启动闩从已加载流播种,**不补播**。
可跳步可回头不补播(F-48.2)。纸近景 = `ModalId.PaperCloseup48`(闭集第七员,ADR-013 §十-B 已注册)。
内容债 P0 种子 = 6 步 + 4 口述 + 6 示范 + 1 纸 = 17 条作者态条目(规则八,`48_tutorial_content.json`)。
三条 gap(TR-tutorial-002/006/007)均「登记不立件」或归 45 轮,不阻塞 P0 单机面。
逐条数值(播报节奏 / 三媒体时序)归用户手感轮 —— 机制与契约已冻结。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-009: 世界状态事件化边界 | 教学进度 = **派生态**,不进三流、不存档(零落盘,Q1 判据);投影由事件流纯函数重建 | MEDIUM |
| ADR-013: 拟物 UI 框架 | 纸近景走 42 近景模态;`ModalId` 闭集第七员 `PaperCloseup48` 已注册(§十-B);焦点单栈门 | HIGH |
| ADR-014: 数据管线与 JSON 解析器 | 作者态 `assets/data/48_tutorial_content.json` → 两阶段烘焙;6 项构建期硬失败校验(引用完整性 / 长度 / 病名词表 / 显式指令扫描 / predicateKind 白名单) | MEDIUM |
| ADR-018: 音频架构 | 口述 = 44 触发 cue(白名单内);**教学口述零字幕是新禁项** —— 字幕必过 AC-44-15 管线;零「学会了 X」提示音(联合 AC-44-09) | MEDIUM |
| ADR-019: 遥测与隐私 §三 | 48 的 Proj = 边界层只读投影,与 AC-51-A2 / 51「只读消费者不写流」同构 | LOW |
| ADR-025: 契约程序集清单 | 48 交付物不得引用 `SimEvent` / `PatientState` / `Fix`(递归反射 + asmdef 引用白名单双守) | LOW |
| ADR-024: 三流 Kind 单一登记真源 | 谓词 `predicateKind` ∈ `entities.yaml` registry(archive 步例外 = 字面量 `Checkpoint`,白名单例外已在规则八钉死) | LOW |
| ADR-001: 联机 pipe 抽象 | 联机演出仲裁 / 每人一份进度 = P1b(归 45 GDD 轮,TR-tutorial-006/008) | HIGH |

**Engine Risk**: **HIGH**(ADR-013 近景模态与手柄焦点桥 + ADR-001 联机面)。
48 自身的机制面(两相触发 + 只读投影 + JSON 校验)= 纯 C#(**LOW**);
抬到 HIGH 的是纸近景模态(依赖 42 的 `PaperCloseup` 呈现与焦点单栈)与联机仲裁(45 无 GDD)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-tutorial-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-21 D-R3 回填实测):**8 条 = 3 covered + 3 partial + 2 gap**;
> **GDD Requirements Covered by ADRs = 6 / 8**(covered + partial);**Untraced = 2**。
> ⚠️ gap 不阻塞 story 的 P0 单机面:`TR-tutorial-002`(Anchor/Completion 两相 = 48 自述机制,
> 登记不立件)、`TR-tutorial-006`(联机仲裁 → 45 轮)、`TR-tutorial-007`(零设门反射断言,
> 登记不立件 —— 载体在 AC-48-17)。`TR-tutorial-003`(Proj 落哪个 asmdef)= partial,
> story 按 ADR-025 清单就近落 `Gameplay.UI` 侧边界投影,**不得记绿**。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-tutorial-001 | 教学进度零落盘(派生态) | ADR-009 + ADR-010 ✅ |
| TR-tutorial-002 | Anchor / Completion 两相触发 | — ❌ gap(登记不立件) |
| TR-tutorial-003 | Proj 只读投影的 asmdef 落点 | ADR-025 ⚠️ partial |
| TR-tutorial-004 | 作者态内容 JSON 全项构建期校验 | ADR-014 ✅ |
| TR-tutorial-005 | 纸近景 = `ModalId` 闭集成员 | ADR-013 ⚠️ partial |
| TR-tutorial-006 | 联机教学演出仲裁 | ADR-001 ❌ gap(45 轮) |
| TR-tutorial-007 | 零设门反射断言 | — ❌ gap(登记不立件) |
| TR-tutorial-008 | 每人一份进度(联机) | ADR-007 ⚠️ partial(待 45 轮) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/tutorial-and-onboarding.md`(AC-48-01 … AC-48-17)are verified
- All Logic and Integration stories have passing test files in `tests/` / `unity/Assets/Tests/`
- All UI and Visual/Feel stories have evidence docs with sign-off in `production/qa/evidence/`
- ⚠️ `[L]` 类 AC(05 / 06 / 07 / 12)**不得由自动化绿替代** —— 须人工走查 + 主创签核
- ⚠️ 联机子项(AC-48-09 联机半 / TR-tutorial-006)记 **BLOCKED-BY-45**,**禁借绿**
- ⚠️ UX Flag ①:三媒体节奏规格仍缺(OQ-C4,归 48 实现轮补 spec)—— 走查 story 前须有该 spec

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 作者态教学内容 schema 与烘焙校验 | Logic | Ready | ADR-014 + ADR-024 |
| 002 | 只读投影 Proj 与装配护栏 | Logic | Ready | ADR-009 + ADR-025 + ADR-019 |
| 003 | Anchor/Completion 两相触发引擎 | Logic | Ready | ADR-009(自述机制,TR-002) |
| 004 | 三媒体调度与纸近景模态 | UI | Ready | ADR-013 + ADR-018 |
| 005 | 零设门与零按键提示守卫及走查 | Integration | Ready | ADR-013 + ADR-014 |
