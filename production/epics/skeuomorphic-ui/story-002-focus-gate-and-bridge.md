# Story 002: 焦点门状态机 + 焦点导航呈现桥

> **Epic**: 拟物 UI 框架
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4-5 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-001` (EventSystem 单件), `TR-skeuoui-003` (焦点单栈门), `TR-skeuoui-004` (官方桥焦点移动), `TR-input-009` (3 侧接口:动作 → FocusNavigationIntent)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架 (primary); ADR-011: 输入架构 (secondary)
**ADR Decision Summary**: UI Toolkit 为主 + UGUI 补 world/XR;焦点单栈门 + 官方桥 NavigationMoveEvent(禁同键双触发);焦点导航接口归 3(action → FocusNavigationIntent),呈现归 42;IModalState + ModalId 闭集(2026-09-21 扩 7 员)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 6.3 运行时焦点导航 / 官方桥 / 自定义材质 post-cutoff,须 spike(§6.6 假设 6「运行时手柄焦点导航可用」半可信)。

**Control Manifest Rules (this layer)**:
- Required: 两栈共用同一 EventSystem;焦点单栈门 = 同一时刻仅一栈接收导航意图流;切换走先关后开,过渡窗口 0 ≤ W_trans ≤ 1 frame;官方桥 NavigationMoveEvent + FocusController 为焦点移动唯一真源
- Forbidden: 双 EventSystem / 双输入模块;自实现焦点算法(除非 spike 失败降级);同一控件同时被官方 Navigate 与自建焦点动作绑上
- Guardrail: 过渡窗口内不可重入;降级代码必须住在 42 自己的程序集内,不新开程序集

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-A1**: 两栈共用同一 EventSystem + InputSystemUIInputModule;Navigate 映射存在且唯一
- [ ] **AC-42-A3a**: 42 的焦点契约侧编译时不引用任何桥专有类型(桥符号只许出现于实现侧);降级前后契约签名不变
- [ ] **AC-42-A5**: 两栈的 IPresentationRoot 实现均经同一 SetFocusGate 入口,无旁路开关
- [ ] **AC-42-B3a**: 任意模式切换后,不存在任一采样点为「两门皆开」;切换期间可观测到的「两门皆关」采样点至多一个;过渡态内不可重入
- [ ] **AC-42-B4**: 42 的焦点契约导出面(IFocusNavigationPresenter 及其全部 public/internal 类型)不导出「邻居枚举 / 方向投影 / 几何查询」类入口
- [ ] **AC-3-C1**: 3 侧导航动作 → FocusNavigationIntent 单向只读视图;3 不实现焦点移动(归 42)
- [ ] **AC-3-C2**: 无同键双触发 —— 同一物理输入不得同时绑官方桥 Navigation 与自实现路径

---

## Implementation Notes

*Derived from ADR-013 / ADR-011 Implementation Guidelines:*

1. **FocusGateStateMachine**: 实现三态(平面独占 / 世界空间独占 / 过渡两门皆关),切换走先关后开
2. **IModalState 接口**: `public interface IModalState { ModalId Modal { get; } }`,ModalId 闭集 7 员 + None
3. **ModalId 枚举**: ①Casebook ②SaveSlots ③Inventory ④Settings ⑤Tutorial ⑥ClinicPanel ⑦PaperCloseup48 + None = 0
5. **官方桥接线**: `InputSystemUIInputModule` 消费 `Navigate` action → `NavigationMoveEvent` → `FocusController`
6. **单栈门路由**: 焦点导航意图流经焦点门状态机路由到当前活跃栈
7. **降级路径**(预先写死):若 spike 判定引擎焦点质量不达预期,降级 = 自实现焦点算法;接口不变,代码住在 42 程序集内
8. **同键双触发禁令**: 构造期断言拒绝同一控件同时被官方 Navigate 与自建焦点动作绑定
9. **IFocusNavigationPresenter 接口**: 定义在 `Gameplay.UI`,42 实现
10. **判据形态**: 只断性质,不引 post-cutoff 符号名

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: 拟物元件库基础
- Story 003: 焦点导航边界(rank 数据 / 焦点悬空 / 空行反馈)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-A1**:
- Given: 两栈场景
- When: 检查 EventSystem + InputSystemUIInputModule
- Then: 同一 EventSystem;Navigate 映射存在且唯一
- Edge cases: 世界空间 UGUI Canvas 不挂 EventSystem

**AC-42-A3a**:
- Given: 42 的焦点契约侧代码
- When: 编译 / Roslyn 分析
- Then: 不引用任何桥专有类型;桥符号只出现在实现侧
- Edge cases: 降级实现引入邻居枚举类时,该类不得为 public / internal

**AC-42-A5**:
- Given: 两栈的 IPresentationRoot 实现
- When: 检查数据读写路径
- Then: 均经同一 SetFocusGate 入口,无旁路开关
- Edge cases: 直接实例化不带 SetFocusGate 的 IPresentationRoot

**AC-42-B3a**:
- Given: FocusGateStateMachine 初始态 = 平面独占
- When: 执行模式切换(平面 → 世界空间)
- Then: ① 不存在任一采样点为两门皆开;② 两门皆关采样点至多一个;③ 过渡窗口内再次请求切换,不产生第二次关/开序列
- Edge cases: 快速连续切换;切换请求落在过渡窗口内

**AC-42-B4**:
- Given: IFocusNavigationPresenter 及其全部 public/internal 类型
- When: 检查导出面
- Then: 不存在「邻居枚举 / 方向投影 / 几何查询」类入口
- Edge cases: 降级代码在 42 内部引入这些类但标记为 private

**AC-3-C1**:
- Given: 3 的 FocusNavigationIntent 接口
- When: 检查 42 的消费方式
- Then: 42 通过单栈门路由消费;3 不驱动焦点移动
- Edge cases: 3 调试视图读取焦点信息(应经 42 的调试视图,不在 3 侧)

**AC-3-C2**:
- Given: 全部 UI 控件绑定
- When: 构造期检查
- Then: 同一控件不同时被官方 Navigate 与自建焦点动作绑定
- Edge cases: 动态添加控件时重复检查

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skeuomorphic-ui/focus_gate_and_bridge_test.cs` — must exist and pass

**Status**: [x] Created + VERIFIED — 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_gate_and_bridge_test.cs`(8 测全过)

---

## Dependencies

- Depends on: None
- Unlocks: Story 003 (焦点导航边界), Story 011 (脉案页焦点导航), Story 012 (存档位界面焦点), Story 013 (库存容器焦点)

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 7/7 passing
**Deviations**: None
**Test Evidence**: Logic — test file at `tests/unit/skeuomorphic-ui/focus_gate_and_bridge_test.cs` (8 test functions, all passing on Unity 6.3 EditMode)
**Code Review**: Complete — APPROVED WITH SUGGESTIONS (2 non-blocking test robustness suggestions)
**Desktop Review Items** (require desktop Unity editor):
- Verify `FocusNavigationBridge.Update()` transition window timing in actual Editor
- Spike Unity 6.3 official focus quality to decide if downgrade path should activate
