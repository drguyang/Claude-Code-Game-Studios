# EditMode 全量失败原文摘录 —— 云端修复输入(4 条)

- **基线 XML**: `unity/Logs/pre-cloud.xml` — testcasecount=**921**, passed=**913**, **failed=8**, skipped=0
- **本次摘录 XML**: `unity/Logs/s005-final2.xml` — testcasecount=**921**, passed=**917**, **failed=4**, skipped=0
- **进展**: 云端首轮修 8→4;**以下 4 条为残余**
- **摘录时间**: 2026-09-27

> 说明:每条含 fullname + 原始 message + stack-trace,未加工。

---

## 1. `DaYiJingCheng.Tests.Unit.Audio.AssemblyBoundaryTest.test_entryPoints_production44_noUnknownEntry`

**message**:
```
[B3] DaYiJingCheng.Gameplay.Presentation.Audio.VoiceVariantLib.ComputeBucket 签名出现非白名单项目类型「DaYiJingCheng.Gameplay.Presentation.Audio.VoiceBucket」—— 状态入口仅 AudioCueDto,渲染参数 = TierSource/AudioCueHandle/IAudioCueSink/IPositionalChannel(VitalsDto 族 2026-09-18 已删;AC-44-B3 / ADR-018 §一)
[B3] DaYiJingCheng.Gameplay.Presentation.Audio.VoiceVariantLib.SelectVariant 签名出现非白名单项目类型「DaYiJingCheng.Gameplay.Presentation.Audio.VoiceVariantLib+VoiceVariantTable」—— 状态入口仅 AudioCueDto,渲染参数 = TierSource/AudioCueHandle/IAudioCueSink/IPositionalChannel(VitalsDto 族 2026-09-18 已删;AC-44-B3 / ADR-018 §一)
```

**stack-trace**:
```
at DaYiJingCheng.Tests.Unit.Audio.AssemblyBoundaryTest.test_entryPoints_production44_noUnknownEntry () [0x00016] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs:438
```

**归因**: Story 006 新增 `VoiceBucket` 枚举与嵌套 `VoiceVariantTable` 类型,未登记进 B3 签名白名单。

---

## 2. `DaYiJingCheng.Tests.Unit.InputSystem.DeveloperDebugViewTest.test_e2a_ongui_method_body_conditionally_compiled`

**message**:
```
E2①:OnGUI 方法体内必须含 #if UNITY_EDITOR || DEVELOPMENT_BUILD 包裹(玩家构建零代码路径)
  Expected: True
  But was:  False
```

**stack-trace**:
```
at DaYiJingCheng.Tests.Unit.InputSystem.DeveloperDebugViewTest.test_e2a_ongui_method_body_conditionally_compiled () [0x00004e] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/developer_debug_view_test.cs:66
```

**归因**: 远程 Story 012 的 developer debug view 实现,`OnGUI` 方法体缺少 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 条件编译包裹。

---

## 3. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_suspended_to_idle_callback_count_resets_to_zero`

**message**:
```
Suspended 期 wired 回调仍应计数(enabled=true)
  Expected: 2
  But was:  0
```

**stack-trace**:
```
at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_suspended_to_idle_callback_count_resets_to_zero () [0x00004a] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:273
```

**归因**: 同 e4 族(与云端已修的 6 个同源)。Suspended 态下 wired 回调计数未生效。

---

## 4. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e5_armed_read_path_traversed_sample_increments`

**message**:
```
Armed 期 wired 回调应触发一次
  Expected: 1
  But was:  0
```

**stack-trace**:
```
at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e5_armed_read_path_traversed_sample_increments () [0x000045] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:323
```

**归因**: 同 e4/e5 族。Armed 态读路径未产生样本计数。

---

## 分布

| 系统 | 条数 | 涉及 |
|---|---:|---|
| audio | 2 | Story 006 新增 `VoiceBucket` / 嵌套 `VoiceVariantTable` 未登记 B3 签名白名单 |
| input-system | 2 | Story 012 `OnGUI` 缺条件编译包裹(1)+ e4 回调计数(1) |

**未提交**: 本文件为唯一改动。

---

# 附录:EditMode 残余红色 4 条(云端修复后)

- **来源 XML**: `unity/Logs/s005-final2.xml` — testcasecount=**921**, passed=**913**, **failed=4**, skipped=0
- **云端修复进展**: 8 → 4(首轮修 6 个,残余 2 个 + 新增 2 个)
- **摘录时间**: 2026-09-27

> 说明:以下 4 条为**修复后仍红**的残余,与上方 15 条(已定性、全部保留)分开记录。

---

## 逐条

### 1
```
[B3] DaYiJingCheng.Gameplay.Presentation.Audio.VoiceVariantLib.ComputeBucket 签名出现非白名单项目类型「DaYiJingCheng.Gameplay.Presentation.Audio.VoiceBucket」—— 状态入口仅 AudioCueDto,渲染参数 = TierSource/AudioCueHandle/IAudioCueSink/IPositionalChannel(VitalsDto 族 2026-09-18 已删;AC-44-B3 / ADR-018 §一)
```
归属测试:`test_entryPoints_production44_noUnknownEntry`
文件:`unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs:438`

### 2
```
[B3] DaYiJingCheng.Gameplay.Presentation.Audio.VoiceVariantLib.SelectVariant 签名出现非白名单项目类型「DaYiJingCheng.Gameplay.Presentation.Audio.VoiceVariantLib+VoiceVariantTable」—— 状态入口仅 AudioCueDto,渲染参数 = TierSource/AudioCueHandle/IAudioCueSink/IPositionalChannel(VitalsDto 族 2026-09-18 已删;AC-44-B3 / ADR-018 §一)
```
归属测试:同上

### 3
```
E2①:OnGUI 方法体内必须含 #if UNITY_EDITOR || DEVELOPMENT_BUILD 包裹(玩家构建零代码路径)
  Expected: True
  But was:  False
```
归属测试:`test_e2a_onguiMethodBody_conditionallyCompiled`
文件:`unity/Assets/Tests/EditMode/InputSystem/developer_debug_view_test.cs:66`

### 4
```
Suspended 期 wired 回调仍应计数(enabled=true)
  Expected: 2
  But was:  0
```
归属测试:`test_e4_suspended_to_idle_callback_count_resets_to_zero`
文件:`unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:273`

---

## 残余 4 条归因(供云端参考,未经我复跑验证)

| # | 测试 | 可能原因 |
|---|---|---|
| 1–2 | `test_entryPoints_production44_noUnknownEntry` | Story 006 新增 `VoiceBucket` 枚举与嵌套 `VoiceVariantTable` 类型,未登记进 B3 签名白名单 |
| 3 | `test_e2a_onguiMethodBody_conditionallyCompiled` | Story 012 的 developer debug view 实现,`OnGUI` 方法体缺少 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 条件编译包裹 |
| 4 | `test_e4_suspended_to_idle_callback_count_resets_to_zero` | 同 e4 族(与云端已修的 6 个同源),Suspended 态下 wired 回调计数未生效 |

**未提交**: 本文件为唯一改动。
