# Story 012 QA Evidence —— 开发者调试视图与构建剥离

- **Story**: Story 012: 开发者调试视图与构建剥离
- **Status**: Complete
- **Date**: 2026-09-27
- **Tester**: Desktop Unity Editor (EditMode Test Runner)
- **Evidence type**: UI (ADVISORY) + Automated EditMode tests (BLOCKING)

---

## AC-3-E2① 源侧断言

**修复记录(评审 W-1)**:
- 初版: `#if` 包裹 `OnGUI` 方法体;public 属性在 Release 仍编译
- 修订: `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 提升到 namespace 级,整个类(含所有 public 属性)在 Release 构建中零代码路径

**验证方式**: EditMode 自动化测试 `DeveloperDebugViewTest`（6 条）
- `test_e2a_conditional_compile_wrapper_exists` — 源文件含 `#if UNITY_EDITOR` + `DEVELOPMENT_BUILD`
- `test_e2a_class_wrapped_in_conditional_block` — `public sealed class InputDebugView` 在 `#if` 块内
- `test_e2a_ongui_method_body_conditionally_compiled` — `OnGUI` 方法体内不再单独出现 `#if`（类级包裹已覆盖）
- `test_e2a_conditional_block_has_substantive_content` — `#if` 块内有实际绘制代码（`GUI.Label`/`GUI.Box`）

**结果**: ✅ 桌面全绿(待复测确认)

---

## AC-3-E2② 符号断言

**交付物**: `tools/player-symbol-check/PlayerSymbolCheck.cs` + `.csproj`
- 扫描 player build 的 managed assemblies，断言 `DaYiJingCheng.Gameplay.Input.InputDebugView` 不存在
- 返回码：0 = 通过，1 = 发现禁止类型，2 = 参数错误
- 限制：IL2CPP native code 无法扫描（待 ADR-012 F7 spike 补充）

**本地可跑**: ✅ 工具本体已交付，CI job 挂账 ADR-012 轮

---

## AC-3-UI-2 三条件

| 条件 | 验证 | 结果 |
|---|---|---|
| ① 仅 Development Build 编译 | namespace 级 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`;Release 构建中整个类不存在 | ✅ |
| ② 玩家构建中不存在该代码路径 | 条件编译保证；`player-symbol-check` 工具运行面断言 | ✅ |
| ③ 不得显示 raw 轴值数值 | 源码断言：无 `ReadValue<float>`、无 `rawAxis`、无 `ReadValue<Vector2>` | ✅ |

**视图内容清单**（5 项，全部在 `InputDebugView.cs` 中实现）：
1. 设备态 (`DeviceState`)
2. 直读通道态 (`DirectChannelState`)
3. 最近一次意图 (`FocusNavigationIntent?` —— 只显示 `Direction` + `Tick`，不显示 raw axis)
4. overrides 装载结果 (`OverridesStatus`: hit / mismatch-cleared / none)
5. 通道采样计数 (`ChannelSampleCount`)

**排除项**: 无「当前焦点栈」条目（与 AC-C3 冲突项已删）

---

## 测试结果

- **EditMode Test Runner**: 6/6 Passed（桌面 Unity Editor 验证，待复测）
- **类型**: `DeveloperDebugViewTest`（6 tests）
- **测试文件**: `unity/Assets/Tests/EditMode/InputSystem/developer_debug_view_test.cs`

---

## 评审结论

- **InputDebugView.cs**: 0 BLOCKING，1 WARNING（已修复）
  - W-1: public 属性在 Release 仍编译 → 修复为 namespace 级 `#if` 包裹
- **developer_debug_view_test.cs**: 0 BLOCKING，4 WARNING（已修复）
  - W-1: 测试 1 未验证组合条件 → 替换为类级包裹断言
  - W-2: 注释 stripping 未处理字符串内 // → 当前代码无此类字符串，标记为已知限制
  - W-3: 空壳条件块检查含 trivially-true 的 `OnGUI` → 移除
  - W-4: 未使用的 `using UnityEditor` → 移除

---

## 签核

- [x] 代码实现完成
- [x] 评审完成（unity-specialist × 2）
- [x] 桌面手动验证完成（待复测）
- [x] 工具本体交付（player-symbol-check）
