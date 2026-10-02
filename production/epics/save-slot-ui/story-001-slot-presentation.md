# Story 001: 存档位 UI 基础框架（槽位呈现 + 焦点导航 + 翻页交互）

> **Epic**: 7b 存档位 UI
> **Status**: Ready
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 2h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-29

## Context

**GDD**: `design/gdd/save-slot-ui.md`
**Requirement**: AC-7b-01(唯一写入API) · AC-7b-02(零本地判定) · AC-7b-04(严格按slot_seq升序) · AC-7b-05(零落盘槽位镜像) · AC-7b-06(手柄灰置槽位仍可聚焦) · AC-7b-07(零弹窗/toast) · AC-7b-10(7b呈现=7a返回值的纯函数)

**ADR Governing Implementation**: ADR-013(main: UI Toolkit + 焦点导航) · ADR-010(次: 存档契约)
**ADR Decision Summary**: 7b = 42 屏幕空间模态（ModalId.SaveSlots）; 焦点导航走官方桥 NavigationMoveEvent + FocusController; 7b 不写三流、不判断能否覆盖、不落盘槽位镜像——纯投影层。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Engine Knowledge Risk HIGH —— 焦点桥 / world-space / UI Toolkit 自定义材质 / 图集 / 无障碍均须 spike; 本 story 仅触及焦点导航桥（已承 ADR-013 §六 假设6 缓办）。

**Control Manifest Rules (this layer)**:
- Required: 7b 不写三流（IEventSink.Append 零调用）; 7b 零本地判定（Writable/Loadable 全部来自 7a）; 槽位严格按 slot_seq 升序; 灰置槽位仍可聚焦且有反馈（压痕/纸钝响）
- Forbidden: 弹窗 / toast / 进度条 / 「存档成功」类文本; 自定义写入路径; 落盘槽位镜像; 键鼠指针依赖（手柄单机须可用）
- Guardrail: 7b 持有的界面 = 存档册页本身的 VisualElement / PanelSettings 子集（点名闭集）; asmdef 引用集 = 白名单（不引 sim 类型）

---

## Acceptance Criteria

*From GDD `design/gdd/save-slot-ui.md`, scoped to this story:*

- [ ] **AC-7b-01**: 检索 7b 全部代码路径，写入 API 唯一 = `ISaveService.Checkpoint(SaveSlot)` / `Load(SaveSlot)`，零自定义写入路径（BLOCKING）
- [ ] **AC-7b-02**: 检索 7b 全部判定，零自身「能否覆盖 / 能否载入」判定 —— Writable / Loadable 全部来自 7a（BLOCKING）
- [ ] **AC-7b-04**: 给定槽位列表，计算排序 ⇒ 严格按 slot_seq 升序（BLOCKING）
- [ ] **AC-7b-05**: 检索持久化，7b 零落盘槽位镜像 —— 读时投影自 7a（BLOCKING）
- [ ] **AC-7b-06**: 手柄单机走查存档册全部交互，灰置槽位仍可聚焦且有反馈（压痕/纸钝响），零按键提示浮层（ADVISORY）
- [ ] **AC-7b-07**: 检索 7b 持有的界面（VisualElement / PanelSettings 子集），零弹窗 / toast / 进度条 / 「存档成功」类文本（BLOCKING）
- [ ] **AC-7b-10**: 覆盖既有槽位 / 非叙事上下文 / 读档后同槽位，7b 呈现 = 7a 返回值的纯函数（灰置 / 页角小字 / 零弹窗）（BLOCKING）
