# Story 004: 数据边界守卫(DTO Guard · 符号禁令 · 调试视图白名单 · IModalState)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-006` (PresentationDtoGuard 递归反射扫描), `TR-skeuoui-007` (符号禁令:禁 IEventSink.Append 等写入口), `TR-skeuoui-009` (IModalState 闭集 7 员)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架 (primary); ADR-005: 确定性模拟 (secondary); ADR-008: 病例事件流 (tertiary)
**ADR Decision Summary**: disease_id 不进呈现层;PresentationDtoGuard 递归反射扫描;42 只渲染不持状态(§9 C3);IModalState + ModalId 闭集 7 员;VitalsDto 是 42 唯一 float 出口

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— 运行时反射扫描 / 递归访问 private / 继承字段须 spike。ADR-005 Knowledge Risk MEDIUM —— IL2CPP 逐位性须实测;但本件刻意不用 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: PresentationDtoGuard 须递归(List<DTO> 元素、私有/继承字段);42 只读 DTO(IVitalsQuery → VitalsDto);零模拟写;42 内不存在任何写三流 / 写存档 / 写 assets/data/ 的代码路径
- Forbidden: 42 持有 DTO 副本(缓存 = 第二份真相);42 持有设置值;42 直连 9 的模拟;仅扫顶层的浅扫描
- Guardrail: 扫描须有 visited 集防循环引用;调试视图只读白名单(枚举 / bool / 计数字段)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-D1**: UI DTO 类型树经 PresentationDtoGuard 扫描:① 递归带 visited 集(循环引用须终止);② 反例覆盖 List<DTO> / array / 泛型集合 / 委托与事件签名 / 私有字段 / 继承字段;③ 断言字段名与类型名两面;任带 disease_id ⇒ 失败
- [ ] **AC-42-D2**: 42 的代码中不存在写三流 / 写存档 / 写 assets/data/ 的调用(禁 IEventSink.Append 等写入口 · 禁 File.Write* / AssetDatabase.*)
- [ ] **AC-42-D3**: 42 的类型树不持有 DTO 副本、不持有设置值 —— 这是「只渲染不持状态」的唯一机械形态
- [ ] **AC-42-D4**: 42 的调试视图(#if UNITY_EDITOR || DEVELOPMENT_BUILD)读到的 DTO 成员 ⊆ 白名单(枚举/状态字段 / bool / 计数字段);不读任何量纲为游戏量的字段(float / int 型的生命 · 伤情 · 乘子 · 置信度等)
- [ ] **AC-42-D5**: DtoRoot 注册表覆盖全部 UI DTO 根类型;新 DTO 须登记(构建期断言)
- [ ] **AC-37-15**: disease_id 不进呈现层;唯一落点 = DTO 静态检查(PresentationDtoGuard 递归扫 List<DTO> 元素 / 私有 / 继承字段 —— 仅扫顶层会漏)
- [ ] **TR-skeuoui-009**: IModalState.Modal 返回 ModalId 闭集(7 员)或 None;闭集成员 = ①脉案页 ②存档位界面 ③库存容器 ④设置界面壳 ⑤教学界面 ⑥医馆面板 ⑦教学纸近景

---

## Implementation Notes

*Derived from ADR-013 / ADR-005 / ADR-008 Implementation Guidelines:*

1. **PresentationDtoGuard 类**: 静态工具类,递归扫描 DTO 类型树;使用 visited 集防循环引用
2. **覆盖范围**: 字段(public / private / protected / inherited)、属性、List<T> / T[] / 泛型集合元素、委托与事件签名
3. **断言两面**: 字段名(field name)与类型名(type name)均须检查是否包含 disease_id
4. **拒绝语义**: 构建 / 装载失败(不是运行时降级)
5. **DtoRoot 注册表**: 维护「UI DTO 根类型清单」,新 DTO 须登记(AC-42-D5)
6. **符号级禁令**: AC-42-D2 须 Roslyn 分析器 / 编译期断言守门(禁 IEventSink.Append / File.Write* / AssetDatabase.*)
7. **类型树断言**: AC-42-D3 断言 42 的类型树内不存在 DTO 缓存字段 / 设置值字段
8. **调试视图白名单**: AC-42-D4 断调试视图只读白名单成员(枚举 / bool / 计数字段),禁读游戏量字段
9. **IModalState 接口**: `public interface IModalState { ModalId Modal { get; } }`
10. **ModalId 枚举**: ①Casebook ②SaveSlots ③Inventory ④Settings ⑤Tutorial ⑥ClinicPanel ⑦PaperCloseup48 + None = 0
11. **判据形态**: AC-42-D4 = 正面白名单(不是「找数值」这种可绕的否定式)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 焦点门状态机 + 焦点导航呈现桥(本 story 依赖其就绪)
- Story 005: 世界空间锚点面片
- Story 006: 医馆面板渲染 + 刷新延迟契约

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-D1**:
- Given: 含 disease_id 的 DTO 类型树(List<DTO> / 继承 / 私有字段 / 泛型集合)
- When: PresentationDtoGuard 扫描
- Then: ① 递归带 visited 集;② 覆盖全部字段类型;③ 任带 disease_id(字段名或类型名) ⇒ 构建/装载失败
- Edge cases: 循环引用类型树;空 DTO;嵌套三层以上的泛型集合

**AC-42-D2**:
- Given: 42 程序集的全部代码
- When: 符号级校验(Roslyn banned members)
- Then: 不存在写三流 / 写存档 / 写 assets/data/ 的调用;禁 IEventSink.Append / File.Write* / AssetDatabase.*
- Edge cases: 42 引用 Sim.Contracts(只读)因而能看见 IEventSink;实质判据是符号级调用点,不是引用集

**AC-42-D3**:
- Given: 42 的类型树
- When: 检查字段 / 属性
- Then: 不持有 DTO 副本字段(如 private VitalsDto _cachedVitals);不持有设置值字段
- Edge cases: 42 的 Presenter 基类若含缓存字段 => 失败

**AC-42-D4**:
- Given: 42 的调试视图代码(#if UNITY_EDITOR || DEVELOPMENT_BUILD)
- When: 符号级校验
- Then: 读到的 DTO 成员 ⊆ 白名单(枚举 / bool / 计数字段);不读 float / int 游戏量字段
- Edge cases: 调试视图读取 VitalsDto 的 float 字段(生命值等) => 失败

**AC-42-D5**:
- Given: 全案 DTO 根类型清单
- When: 新增 DTO 类型
- Then: DtoRoot 注册表已登记;构建期断言覆盖
- Edge cases: 遗漏登记的新 DTO => 构建期失败

**AC-37-15 / TR-skeuoui-009**:
- Given: IModalState 接口
- When: 读取 Modal 属性
- Then: 返回 ModalId 闭集成员或 None;闭集 = 7 员 + None
- Edge cases: 新增界面时闭集必须同步更新

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skeuomorphic-ui/dto_boundary_and_modal_test.cs` — must exist and pass

**Status**: [x] VERIFIED — EditMode `dto_boundary_and_modal_test.cs` 12/12 passed (2026-09-29 r3)
**Result**: `TestResults-639262397348624940.xml` — passed=12, failed=0

---

## Dependencies

- Depends on: None
- Unlocks: Story 005 (世界空间锚点面片), Story 006 (医馆面板渲染), Story 007 (数据边界收尾)
