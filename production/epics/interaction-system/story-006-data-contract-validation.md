# Story 006: 数据契约构建期校验与呈现/手柄验收面 —— `4-DC-1…6` 夹具矩阵 / 身位即光标走查 / 零播报音 / 手柄无指针可选出

> **Epic**: 交互系统
> **Status**: Complete
> **Layer**: Feature
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-04

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-014(四级消歧 + `KindPriority` 全序烘焙表)· TR-interaction-015(病人四路由语义归 8/10 词表)
**AC**: AC-4-07 · AC-4-08 · AC-4-15 · AC-4-21

**ADR Governing Implementation**: ADR-014(`interaction_kinds.json` 两阶段烘焙;`4-DC-1…6` = 阶段 2 白名单/区间/闭集位点)· ADR-018(无提示音铁律)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(构建期校验 + 文档判据)

---

## Acceptance Criteria

- [x] **AC-4-07([L])** —— ⚠️ **NOT-RUN**(人工走查,EditMode 不可自动化;证据文件不存在,显式登记禁借绿) —— `GIVEN` 玩家在世界中靠近多个目标,`WHEN` 人工走查,`THEN` **零交互提示浮层 / 零目标框 / 零可交互清单**;指示由**站位 / 身形 / 手位**可读
- [x] **AC-4-08([L])** —— ⚠️ **NOT-RUN**(人工听测,同上) —— `GIVEN` 全部交互音效,`WHEN` 人工听测,`THEN` **零状态播报音**(无可交互 ding / 拾取 jingle)—— 与 AC-44-09 联合
- [x] **AC-4-15([A])** —— `GIVEN` `4-DC-1…6` 的**每一条违例各一个夹具**(`R_INTERACT = 0` / kind 缺项 / priority 平局 / `StableIdSource` 非法字符串 / `RoutesTo` 未登记 / `SuppressesMotor=true` 而 `DurationOwner` 为 `None`),`WHEN` **阶段 2 烘焙**,`THEN` **构建期硬失败**
- [x] **AC-4-21([L])** —— 🔶 **机器代理半边 SIGN**(无指针/悬停:真表 Patient 压过同格 Drop,`test_ac421_patientBeatsSameCellDrop_withoutAnyPointerOrHover`);**手柄走查半边 NOT-RUN** —— `GIVEN` **仅手柄**(无指针),`WHEN` 玩家站在含 `Patient` 类候选的格上,`THEN` **仍能选出该候选**(不被同格的 `Drop` 抢走 ⇒ 依赖 `KindPriority`,而**非**依赖任何指针 / 悬停)

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

**Status**: [x] Done —— `unity/Logs/interaction-s006-final.xml` = **116/113/0/3**(3 skipped = story-004 NOT-RUN 机检)
⚠️ **登记实际夹具数**:6 条 DC / **13 个夹具**(QA F-6:此前字面「6」会让后续读表者按 6 核数)
⚠️ **三份 `[L]` 证据文件不存在**(`ac-4-07-walkthrough.md` / `ac-4-08-listen.md` / `ac-4-21-gamepad.md`)—— 显式登记 **NOT-RUN / 待可玩构建**,禁借绿

---

## Dependencies

- Depends on: Story 001(边界)/ Story 002(选择)/ Story 003(候选集)/ Story 004(自报)/ Story 005(模态门)
- Unlocks: 无(本 Epic 最后一个 story)

---

## Completion Notes

**Completed**: 2026-10-04
**Criteria**:
- **AC-4-15 ✅** —— 六条 `4-DC` 各具**承重**夹具(共 13 个)。修复轮闭合三处承重缺口:
  ① `4-DC-4` 的 `SlotLinearKey ⇒ W/H/D` 半边此前**未实现**(doc 宣称与实现对不上)⇒ 已实现(MUT-A 证可红);
  ② `4-DC-3` 此前只查行内互异,而生产的第二键是 `(int)kind` 占位 ⇒ 与表**无机械连接**(「两机器」)
  ⇒ 抽出 `KindPriorityTable` 唯一真源,选择器与校验器读**同一张表**(MUT-C 证可红);
  ③ `4-DC-6` 此前只查「字段非空」,而归属于哪个系统**不可表达**(`DurationOwner` 有损压成哨兵 `-1`)
  ⇒ 拆为 `DurationOwnerKind` + `DurationOwnerSystemId` 并断「∈ 已登记系统集」(MUT-B 证可红)。
  ⚠️ `4-DC-4` 的枚举值域半边在**类型面恒真**(强类型字段),已保留为显式兜底并在源码注明**不承重**。
- **AC-4-07 / AC-4-08 ⬜ NOT-RUN** —— 人工走查 / 听测,EditMode 不可自动化;三份 `[L]` 证据文件
  **不存在**(显式登记,禁借绿)。
- **AC-4-21 🔶 部分** —— **机器代理半边 SIGN**(无指针/悬停,真表 Patient 压过同格 Drop);手柄走查半边 NOT-RUN。
**Deviations**:
- **`DurationOwner` 形状变更**:二值枚举 `{None, RoutedSystem=-1}` → `DurationOwnerKind` + `DurationOwnerSystemId`。
  依据:story-006 评审 F-3(有损编码使 GDD 的「须已登记」判据**无从表达**)。
- **真表接线(跨故事影响)**:新增 `KindPriorityTable` 作为第二键唯一真源,`InteractionSelector` 接线读它。
  依据:评审 F-2(此前表与选择器无机械连接)。**取值 = GDD §F-4.1b 演示序**(逐字照录),**真表待数值轮**。
  ⚠️ **连带定向修复 story-002**:接线真表后,`target_selection_test` 有 **4 条**断言(枚举序占位下 `Drop` 胜)
  **方向相反** ⇒ 已按真表更新期望(患者优先,与 AC-4-21 同向)。MUT-7 先复现(恰那 4 条红)证接缝真实。
- **`RegisteredSystems` 单一来源**:抽出 `RoutedSystems.Registered`,`KindRouteTable` 与校验器共用(评审 F-4/F-5)。
**Test Evidence**: `unity/Logs/interaction-s006-final.xml` = **116/113/0/3**(`data_contract_validation_test.cs` 13 条为主体;3 skipped = story-004 NOT-RUN)
**Code Review**: 双代理单轮评审 + 主会话独立变异复核 —— 原件 `production/qa/evidence/review-interaction-story-006-2026-10-04.md`
(结构侧 **CHANGES REQUIRED** F-1…F-9 · QA 侧 **REJECT(AC-4-15)** F-0…F-7)。**变异证明 10 项落盘**(`unity/Logs/s006-mut*.xml`)。
**未闭登记(NOT-RUN,禁借绿)**:NR-1 装载器/烘焙接线(归 **story 007**)· NR-2 `KindPriorityTable` 真表(数值轮)·
NR-3/4/5 三条 `[L]` 走查(可玩构建)· NR-6 4-DC-6 对 17/20 的真实时长登记(`OQ-17-3`)
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
