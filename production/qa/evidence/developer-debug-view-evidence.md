# Story 012 QA Evidence —— 开发者调试视图与构建剥离

- **Story**: Story 012: 开发者调试视图与构建剥离
- **Status**: Complete
- **Date**: 2026-09-27
- **Tester**: Desktop Unity Editor (EditMode Test Runner)
- **Evidence type**: UI (ADVISORY) + Automated EditMode tests (BLOCKING)

---

## AC-3-E2① 源侧断言

**验证方式**: EditMode 自动化测试 `DeveloperDebugViewTest`（6 条，全部 Passed）
- `test_e2a_conditional_compile_wrapper_exists` —— `InputDebugView.cs` 含 `#if UNITY_EDITOR` + `DEVELOPMENT_BUILD`
- `test_e2a_ongui_method_body_conditionally_compiled` —— `OnGUI` 方法体紧接着 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- `test_e2a_conditional_block_has_substantive_content` —— `#if` 块内有实际绘制代码（`GUI.Label`/`GUI.Box`）

**结果**: ✅ Passed（6/6）

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
| ① 仅 Development Build 编译 | `OnGUI` 方法体第一行 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`；无此符号时方法体全被跳过 | ✅ |
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

- **EditMode Test Runner**: 6/6 Passed（桌面 Unity Editor 验证）
- **类型**: `DeveloperDebugViewTest`（6 tests）
- **测试文件**: `unity/Assets/Tests/EditMode/InputSystem/developer_debug_view_test.cs`

---

## 签核

- [x] 代码实现完成
- [x] 自动化测试全绿
- [x] 桌面手动验证完成
- [x] 工具本体交付（player-symbol-check）
