# EditMode 全量失败原文摘录(8 条)

- **来源 XML**: `unity/Logs/s005-final2.xml`
- **全量结果**: testcasecount=921, passed=913, **failed=8**, skipped=0
- **摘录时间**: 2026-09-27
- **用途**: 云端修复的输入;每条含 fullname + 原始 message + stack-trace

---

## 1. `DaYiJingCheng.Tests.Unit.InputSystem.DirectReadChannelTest.test_direct_read_channel_idle_callback_does_not_advance_hold_ticks`

**message**:
```
Armed 期回调恰推进 _holdTicks 1(每帧一次)
  Expected: 1
  But was:  0
```

**stack-trace**:
```
<![CDATA[at DaYiJingCheng.Tests.Unit.InputSystem.DirectReadChannelTest.test_direct_read_channel_idle_callback_does_not_advance_hold_ticks () [0x00091] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/direct_read_channel_test.cs:754
]]>
```

---

## 2. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e1_hot_path_methods_no_prohibited_tokens`

**message**:
```
热路径集合应恰好 14 个方法名
  Expected: 14
  But was:  15
```

**stack-trace**:
```
<![CDATA[at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e1_hot_path_methods_no_prohibited_tokens () [0x00005] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:78
]]>
```

---

## 3. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_abort_to_idle_callback_count_resets_to_zero`

**message**:
```
Expected: 1
  But was:  0
```

**stack-trace**:
```
<![CDATA[at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_abort_to_idle_callback_count_resets_to_zero () [0x00025] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:148
]]>
```

---

## 4. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_armed_to_idle_callback_count_resets_to_zero`

**message**:
```
System.NullReferenceException : Object reference not set to an instance of an object
```

**stack-trace**:
```
<![CDATA[  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.ReadEmergency () [0x00000] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:326 
  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.OnAfterUpdate () [0x00040] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:317 
  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.NotifyAfterUpdateForTest (System.Int32 frameOverride) [0x00007] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:300 
  at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_armed_to_idle_callback_count_resets_to_zero () [0x0003f] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:129 
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x0006a] in <1d26c8856bcb48938402e0be7f5cc174>:0 ]]>
```

---

## 5. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_disable_emergency_action_resets_callback_count`

**message**:
```
System.NullReferenceException : Object reference not set to an instance of an object
```

**stack-trace**:
```
<![CDATA[  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.ReadEmergency () [0x00000] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:326 
  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.OnAfterUpdate () [0x00040] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:317 
  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.NotifyAfterUpdateForTest (System.Int32 frameOverride) [0x00007] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:300 
  at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_disable_emergency_action_resets_callback_count () [0x00025] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:165 
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x0006a] in <1d26c8856bcb48938402e0be7f5cc174>:0 ]]>
```

---

## 6. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_multiple_callbacks_accumulate_then_reset`

**message**:
```
System.NullReferenceException : Object reference not set to an instance of an object
```

**stack-trace**:
```
<![CDATA[  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.ReadEmergency () [0x00000] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:326 
  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.OnAfterUpdate () [0x00040] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:317 
  at DaYiJingCheng.Gameplay.Input.EmergencyDirectReadChannel.NotifyAfterUpdateForTest (System.Int32 frameOverride) [0x00007] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Gameplay.Input/EmergencyDirectReadChannel.cs:300 
  at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_multiple_callbacks_accumulate_then_reset () [0x00023] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:199 
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
  at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) [0x0006a] in <1d26c8856bcb48938402e0be7f5cc174>:0 ]]>
```

---

## 7. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_suspended_to_idle_callback_count_resets`

**message**:
```
Suspended 期 wired 回调仍应计数(enabled=true)
  Expected: 2
  But was:  0
```

**stack-trace**:
```
<![CDATA[at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e4_suspended_to_idle_callback_count_resets () [0x0004a] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:273
]]>
```

---

## 8. `DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e5_armed_read_path_traversed_sample_increments`

**message**:
```
Armed 期 wired 回调应触发一次
  Expected: 1
  But was:  0
```

**stack-trace**:
```
<![CDATA[at DaYiJingCheng.Tests.Unit.InputSystem.HotPathZeroCostTest.test_e5_armed_read_path_traversed_sample_increments () [0x00025] in /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs:321
]]>
```

---
