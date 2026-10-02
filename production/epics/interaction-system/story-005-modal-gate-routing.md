# Story 005: 模态门与路由边沿 —— `Accept` 两方向 / `ModalId` 引用非复制 / 丢弃不排队 / 路由表 / `Acquire/Release(Self)` / 上行缺口登记

> **Epic**: 交互系统
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-009(模态抑制方向①:10 `Armed` 压制)· TR-interaction-010(方向②:读 `IModalState.Modal ≠ None`)· TR-interaction-011(4 为 `MotorSuppressed` 合法调用者)
**AC**: AC-4-09 · AC-4-10 · AC-4-19

**ADR Governing Implementation**: ADR-013 §十 Amendment A(`IModalState` / `ModalId` 闭集)· ADR-011(`InteractIntent` 由 3 的动作映射供给)· ADR-020 §四(玩家位移 = 纯表现态)· ADR-005(输入是意图源)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯 C# 契约 + 反射断言)

---

## Acceptance Criteria

- [ ] **AC-4-09([A])** —— `GIVEN` `IModalState.Modal` ∈ ADR-013 §十 模态闭集(引用该枚举,**不在 4 内复制清单**),`WHEN` 按交互键,`THEN` 意图**丢弃**(不排队、不缓存)。**并**:`WHEN` 未来闭集新增第七屏,`THEN` **本 AC 自动覆盖**(无 4 侧改动)—— 这是「引用而非复制」的判据。
- [ ] **AC-4-10([A])** —— `GIVEN` 10 的 `Armed` 态,`WHEN` 按交互键,`THEN` **意图被压制**,**3 侧零状态**(规则七 · 与 AC-10-14 联合)
- [ ] **AC-4-19([A])** —— `GIVEN` 4 与 10(或 25)**同时**持有移动压制,`WHEN` **4 先 Release**,`THEN` 压制**仍生效**(10 的位仍置)。⇒ 证伪「单 bool + 三个互不知晓的写者 = 丢失更新」。**4 的调用面 = `Acquire/Release(LeaseSource.Self)`,无 `SetSuppressed(bool)`**

---

## Implementation Notes

- **模态门 = 布尔合取**:`Accept(intent) ⟺ ¬Armed(10) ∧ ¬ModalOpen(42)`。两个输入都是**只读**的外部状态,4 不持有任何一份。
- **`ModalId` 引用而非复制**:4 侧代码**不得**出现 `ModalId` 枚举的成员名清单;只允许 `IModalState.Modal ≠ ModalId.None` 这一个比较。闭集新增时 4 侧零改动。
- **丢弃而非排队**:模态期间玩家按下的交互 = 没发生。不缓存、不重放、不倾泻。
- **`Acquire/Release(LeaseSource.Self)`**:4 的移动压制调用面 = per-source 位图的自持租约。`LeaseSource` 枚举归 1(玩家控制器),4 只传 `Self`。
- **上行缺口登记**:`OQ-4-10` 铁律 —— 4 **不得自行选一个通道填上**。本故事只登记缺口,不实现上行。

---

## Out of Scope

- Story 001:边界纪律与程序集归属
- Story 002:确定性全序目标选择
- Story 003:候选集四源构造
- Story 004:POI 自报链路
- Story 006:数据契约构建期校验与呈现/手柄验收面
- 系统 10 的 `Armed` 状态本体(10 自己持有)
- 系统 42 的 `IModalState` 实现(42 自己持有)

---

## QA Test Cases

- **AC-4-09**: 模态门方向②
  - Given: `IModalState.Modal` 分别设为 `None` / `Casebook` / `SaveSlots` / `InventoryContainer` / `SettingsShell` / `Tutorial` / `ClinicPanel`
  - When: 按交互键
  - Then: `None` → 意图通过;其余 → 意图丢弃
  - Edge cases: 闭集新增第七屏(`PaperCloseup48`)→ 自动覆盖,4 侧零改动

- **AC-4-10**: 模态门方向①
  - Given: 10 的 `Armed` 态 = true
  - When: 按交互键
  - Then: 意图被压制;3 侧零状态(无焦点状态字段)

- **AC-4-19**: per-source lease
  - Given: 4 与 10 同时持有移动压制
  - When: 4 先 `Release(LeaseSource.Self)`
  - Then: 压制仍生效(10 的位仍置)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/interaction/modal_gate_test.cs` — must exist and pass(AC-4-09/10/19 三条;EditMode)

**Status**: [ ] Pending

---

## Dependencies

- Depends on: Story 001(边界纪律)/ Story 002(目标选择)/ Story 003(候选集)/ Story 004(POI 自报)
- Unlocks: Story 006(数据契约校验)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
