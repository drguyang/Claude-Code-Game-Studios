# Story 006: 数据契约构建期校验与呈现/手柄验收面 —— `4-DC-1…6` 夹具矩阵 / 身位即光标走查 / 零播报音 / 手柄无指针可选出

> **Epic**: 交互系统
> **Status**: Ready
> **Layer**: Feature
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-014(四级消歧 + `KindPriority` 全序烘焙表)· TR-interaction-015(病人四路由语义归 8/10 词表)
**AC**: AC-4-07 · AC-4-08 · AC-4-15 · AC-4-21

**ADR Governing Implementation**: ADR-014(`interaction_kinds.json` 两阶段烘焙;`4-DC-1…6` = 阶段 2 白名单/区间/闭集位点)· ADR-018(无提示音铁律)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(构建期校验 + 文档判据)

---

## Acceptance Criteria

- [ ] **AC-4-07([L])** —— `GIVEN` 玩家在世界中靠近多个目标,`WHEN` 人工走查,`THEN` **零交互提示浮层 / 零目标框 / 零可交互清单**;指示由**站位 / 身形 / 手位**可读
- [ ] **AC-4-08([L])** —— `GIVEN` 全部交互音效,`WHEN` 人工听测,`THEN` **零状态播报音**(无可交互 ding / 拾取 jingle)—— 与 AC-44-09 联合
- [ ] **AC-4-15([A])** —— `GIVEN` `4-DC-1…6` 的**每一条违例各一个夹具**(`R_INTERACT = 0` / kind 缺项 / priority 平局 / `StableIdSource` 非法字符串 / `RoutesTo` 未登记 / `SuppressesMotor=true` 而 `DurationOwner` 为 `None`),`WHEN` **阶段 2 烘焙**,`THEN` **构建期硬失败**
- [ ] **AC-4-21([L])** —— `GIVEN` **仅手柄**(无指针),`WHEN` 玩家站在含 `Patient` 类候选的格上,`THEN` **仍能选出该候选**(不被同格的 `Drop` 抢走 ⇒ 依赖 `KindPriority`,而**非**依赖任何指针 / 悬停)

---

## Implementation Notes

- **`4-DC-1…6` 校验 = 构建期硬失败**:每条违例一个夹具,阶段 2 烘焙时逐条跑,违则 `throw`。
- **`[L]` 三项待可玩构建**:AC-4-07/08/21 需人工走查/听测,EditMode 不可自动化。
- **零播报音**:与 ADR-018 §六 无提示音铁律同源 —— 4 不产任何「可交互」提示音。
- **手柄无指针**:AC-4-21 验证 `KindPriority` 决胜不依赖指针/悬停。

---

## Out of Scope

- Story 001-005:边界/选择/候选集/自报/模态门
- 系统 42 的呈现层实现(42 自己持有)
- 系统 44 的音频实现(44 自己持有)

---

## QA Test Cases

- **AC-4-15**: `4-DC-1…6` 夹具矩阵
  - Given: 6 个违例夹具(每条 DC 一个)
  - When: 阶段 2 烘焙
  - Then: 每条违例 → 构建期硬失败(throw)
  - Edge cases: `R_INTERACT = 0` / kind 缺项 / priority 平局 / `StableIdSource` 非法字符串 / `RoutesTo` 未登记 / `SuppressesMotor=true` 而 `DurationOwner = None`

- **AC-4-07/08/21**: `[L]` 走查面
  - Given: 可玩构建
  - When: 人工走查/听测
  - Then: 见 AC 正文

---

## Test Evidence

**Story Type**: Config-Data
**Required evidence**:
- Config/Data: `tests/unit/interaction/data_contract_validation_test.cs` — must exist and pass(AC-4-15 六条夹具;EditMode)
- Visual/Feel: `production/qa/evidence/ac-4-07-walkthrough.md` — AC-4-07 走查记录
- Visual/Feel: `production/qa/evidence/ac-4-08-listen.md` — AC-4-08 听测记录
- Visual/Feel: `production/qa/evidence/ac-4-21-gamepad.md` — AC-4-21 手柄走查记录

**Status**: [ ] Pending

---

## Dependencies

- Depends on: Story 001(边界)/ Story 002(选择)/ Story 003(候选集)/ Story 004(自报)/ Story 005(模态门)
- Unlocks: 无(本 Epic 最后一个 story)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
