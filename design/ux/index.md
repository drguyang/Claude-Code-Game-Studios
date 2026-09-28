# UX Design Index

> **Status**: Approved(2026-09-28 `/ux-review` 轮评审通过;⚠️ 限定:评审通过 ≠ 走查执行 —— UX-01 全族 AC 仍逐条 `[ ]` NOT-RUN,残余 = 45 联机夹具 + 各屏走查执行)
> **Last Updated**: 2026-09-28
> **Purpose**: UX 设计的单一入口;每份 spec 的元信息、状态、关联文档在一处可查。

---

## Screens at a Glance

| # | ModalId | Screen | Spec | Status | Template |
|---|---------|--------|------|--------|----------|
| ① | Casebook | 脉案页 | `casebook-39.md` | Approved | 族首件 |
| ② | SaveSlots | 存档位 | `save-slots-7b.md` | Approved | 对齐族 |
| ③ | InventoryContainer | 库存容器(药箱) | `inventory-container-20.md` | Approved | 对齐族 |
| ④ | SettingsShell | 设置界面壳 | `settings-shell-42.md` | Approved | 对齐族 |
| ⑤ | Tutorial | 教学界面 | `tutorial-48.md` | Approved | 对齐族 |
| ⑥ | ClinicPanel | 医馆面板 | `clinic-panel-24.md` | Approved | 对齐族 |
| ⑦ | PaperCloseup48 | 教学纸近景 | `paper-closeup-48.md` | Approved | 族首件 |

> **ModalId 闭集来源**: `ADR-013 §十-B`(6 员,2026-09-21 第二十八批) → 2026-09-21 Amendment B 扩为 **7 员**(⑦ `PaperCloseup48`)。

---

## Interaction Pattern Library

| Pattern | 来源文档 | 状态 |
|---------|----------|------|
| 键鼠焦点导航 | `interaction-patterns.md` P-01 | In Design |
| Gamepad 焦点导航 | `interaction-patterns.md` P-02 | In Design |
| 焦点单栈门 | `interaction-patterns.md` P-03 | In Design |
| 模态开集只读契约 `IModalState` | `interaction-patterns.md` P-04 | In Design |
| 纸面近景渲染形态 | `interaction-patterns.md` P-05 | In Design |
| 无提示音 / 无提示色报状态 | `interaction-patterns.md` P-06 | In Design(2026-09-21 确认) |

> **状态说明**: Interaction Pattern Library 只登记**已由 Accepted ADR / Approved GDD 裁出**的模式;新屏新模态逐轮补。

---

## Cross-References

| 关联系统 | 关联文档 | 关系 |
|----------|----------|------|
| 42 UI 框架 | `skeuomorphic-ui.md` | 42 实现;本目录只定呈现层 spec |
| 13 病人 AI | `patient-ai.md` | 13 读 `VitalsDto` → 表现姿态;本目录定义脉案如何呈现 |
| 10 急救动作 | `emergency-procedures.md` | 10 产出 `EmergencyAttempt` → 脉案落笔 |
| 37 病例系统 | `case-system.md` | 37 供给病例数据 → 脉案显示 |
| 11 处方系统 | `prescription-system.md` | 方笺落在脉案同一本书 |
| ADR-013 | `docs/architecture/adr-013-skeuomorphic-ui-framework.md` | UI Toolkit + UGUI 双栈裁决 |
| ADR-011 | `docs/architecture/adr-011-input-architecture.md` | 焦点导航接口归属 |
| AD-ART-BIBLE | `design/art/art-bible.md` | 拟物视觉材质/色彩/形状的视觉来源 |

---

## Spec 结构模板(族规)

每份 UX Spec 对齐以下结构(族首件 = `paper-closeup-48.md`):

```
1. Overview
2. Player Fantasy / 玩家感受
3. Layout & Components
4. Interaction Flow
5. Focus Navigation Path(手柄焦点可达性)
6. Visual Direction(承 art-bible.md)
7. Edge Cases
8. Dependencies
9. Tuning Knobs
10. Acceptance Criteria
```

> 新屏新建 spec 时须遵守本模板;偏离须在 `/ux-review` 中说明理由。

---

## 未结项

| # | 事项 | 说明 |
|---|------|------|
| UX-01 | 全族 AC 仍逐条 `[ ]` NOT-RUN | R8 已解除(2026-09-25);残余 = 45 联机夹具 + 各屏走查执行(归实现轮) |
| UX-02 | 手柄焦点导航 P1a 集中调试 | 不零散插入实现;桌面一轮解决(承 `feedback-gamepad-deferred.md`) |
| UX-03 | VR 版本全部不做(P1a) | VR 急救不在 P0 关键路径;VR 版脉案/出诊箱推 P1a |
