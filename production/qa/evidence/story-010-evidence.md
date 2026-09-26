# Story 010 · E5 零分配方法学证据

> **AC-3-E5(BLOCKING)**: Armed 读路径 `GC.Alloc == 0`
> **Methodology**: Development Build + ProfilerRecorder · Mono / IL2CPP 双后端
> **Status**: 方法学已定稿,数值待桌面实测

---

## 1. 统计量

| 指标 | 定义 | 单位 |
|------|------|------|
| `GC.Alloc` | `UnityEngine.Profiling.ProfilerRecorder` 获取的每帧托管分配字节数 | bytes/frame |
| `SampleCount` | `EmergencyDirectReadChannel` 内部采样计数(行为仪表,用于关联帧号) | count |
| `FrameCount` | `UnityEngine.Time.frameCount` | frame |

**选型理由**:`GC.Alloc` 是 Unity 内置的托管堆分配探针,精确到每帧;`ProfilerRecorder` 是唯一可在 Development Build 中脚本化访问的探针 API(Player Connection + ProfilerRecorder 组合)。

---

## 2. 窗口

| 参数 | 值 | 说明 |
|------|-----|------|
| `WarmupFrames` | 60 | 丢弃前 60 帧(约 1 秒),避免 JIT / 静态初始化噪声 |
| `MeasurementFrames` | 300 | 测量窗口约 5 秒(60fps 下),足以覆盖典型动作周期 |
| `DiscardStrategy` | 全有或全无 | 窗口内任何一帧 `GC.Alloc > 0` 即视为失败(不允许平均掩盖尖峰) |

**理由**:急救动作最长 ≈ 550 ms(CPR 周期),5 秒窗口覆盖 9 个完整周期;60 帧预热确保 JIT / `InputAction` 首次激活等一次性分配已在测量前完成。

---

## 3. 剔除

| 分配源 | 处理方式 | 说明 |
|--------|----------|------|
| JIT 编译 | 60 帧预热剔除 | `[MethodImpl(MethodImplOptions.NoInlining)]` + 预热帧确保 JIT 不在测量窗内触发 |
| `InputAction` 首次激活 | 构造期显式 `Enable()` | `EmergencyDirectReadChannel` 构造后立即 `EnableEmergencyAction()`,将首次激活分配到预热段 |
| `_edgeTicks.ToArray()` | 已知分配,记录但纳入测量 | G9 已登记的结构性分配;E5 测量的是**除 G9 外**是否还有隐藏分配 |
| 测试缝代码 | 测量路径不经过 `FeedForTest` | `FeedForTest` 含 `ToArray`,测量走接线侧 `OnAfterUpdate` → `ReadEmergency` |

**关键**:E5 不要求 `GC.Alloc == 0` 绝对零分配(那会与 G9 冲突),要求的是**除已登记结构性分配外无隐藏分配**。

---

## 4. 工具

### 4.1 ProfilerRecorder 探针代码

```csharp
// 测量框架(Development Build only)
#if DEVELOPMENT_BUILD
var gcAllocRecorder = ProfilerRecorder.Start("GC.Alloc", 300);
var frameCountRecorder = ProfilerRecorder.Start("Frame Count", 300);

// 每帧采样
for (int frame = 0; frame < MeasurementFrames + WarmupFrames; frame++)
{
    // 驱动输入系统
    InputSystem.Update();

    if (frame >= WarmupFrames)
    {
        long alloc = gcAllocRecorder.LastValue;
        if (alloc > 0)
        {
            Debug.LogError($"[E5] Frame {frame}: GC.Alloc = {alloc} bytes (FAIL > 0)");
            // 失败 = 非零
        }
    }
}

// 清理
gcAllocRecorder.Dispose();
frameCountRecorder.Dispose();
#endif
```

### 4.2 双后端矩阵

| 后端 | 构建目标 | Unity 脚本后端 | 运行环境 |
|------|----------|----------------|----------|
| Mono | StandaloneLinux64 | Mono | 超算 / 桌面 |
| IL2CPP | StandaloneLinux64 | IL2CPP | 超算 / 桌面 |

**注意**:IL2CPP 的托管堆行为与 Mono 不同(值类型拷贝、泛型特化差异),必须双后端均绿。

### 4.3 判定规则

```
PASS: 测量窗口内每一帧 GC.Alloc == 0(除 G9 ToArray 外无其他分配)
FAIL: 测量窗口内任意帧 GC.Alloc > 0(非 G9 来源)
```

### 4.4 已知 G9 分配

`_edgeTicks.ToArray()` 每帧产生一个 `int[]` 副本:
- **大小**:`edgeTicks.Count × 4 bytes`(max 24 edges × 4 = 96 bytes/frame)
- **归属**:Story 007 已登记(G9),**不归 E5 追责**
- **E5 额外要求**:除 G9 外无其他 `GC.Alloc` 来源

---

## 5. 测量步骤

### 5.1 准备工作

1. 切换到 Development Build:
   - `File → Build Settings → Development Build` 勾选
   - `Scripting Backend = Mono`(第一轮) / `IL2CPP`(第二轮)
2. 构建 StandaloneLinux64 player
3. 启动 player,附加 Profiler(Editor → Profiler → Active Player)

### 5.2 测量流程

1. **预热**:运行 60 帧(约 1 秒),不做任何操作
2. **触发动作**:通过键盘/手柄触发 `Emergency` 动作(模拟按压)
3. **测量**:记录接下来 300 帧(约 5 秒)的 `GC.Alloc`
4. **触发 EndAction**:动作完成后调用 `EndAction()` 回 Idle
5. **分析**:检查测量窗口内每帧 `GC.Alloc` 值

### 5.3 通过判据

- 测量窗口内每帧 `GC.Alloc == 0`(除 G9 ToArray 外)
- 或:总 `GC.Alloc` = 预期 G9 分配(`edgeTicks.Count × 4 × frameCount`),且无其他来源

---

## 6. 当前状态

| 步骤 | 状态 | 备注 |
|------|------|------|
| 方法学文档 | ✅ 已定稿 | 本文件 |
| 统计量/窗口/剔除/工具 | ✅ 已定义 | 见上方 §1–§4 |
| Mono 后端实测 | ⏳ 待桌面 | 超算无 Unity 编辑器,无法跑 Development Build |
| IL2CPP 后端实测 | ⏳ 待桌面 | 同上 |
| 证据归档 | ⏳ 待实测后补录 | 截图 + 数值填入本文件 |

---

## 7. 代码结构保证(EditMode 可验证)

虽然 `GC.Alloc` 实测须 Development Build,但 EditMode 可通过结构断言验证**零分配路径可达**:

| 断言 | 验证方式 |
|------|----------|
| `EnableEmergencyAction()` / `DisableEmergencyAction()` 存在且可调用 | EditMode 反射/直接调用 |
| `OnAfterUpdate` 在 `enabled == false` 时提前 return(不进入采样) | EditMode 模拟回调 + `EmergencyCallbackCount` 断言 |
| `ReadEmergency` 无可见分配源(无 `new` / `ToList` / `ToArray` 除 G9) | EditMode Roslyn 源码扫描(见 E1) |
| `_edgeTicks.ToArray()` 仅在 `ReadEmergency` 和 `FeedForTest` 中出现 | EditMode Roslyn 扫描(已知结构性分配) |

这些结构断言由 `hot_path_zero_cost_test.cs` 覆盖(见 `test_e5_armed_path_structure_no_obvious_alloc_sources`)。

---

## 8. 风险与限制

| 风险 | 缓解 |
|------|------|
| IL2CPP 泛型特化引入隐藏分配 | 双后端实测;IL2CPP 失败时逐方法 Profiler 下钻 |
| `InputAction.ReadValue<float>()` 内部分配 | 实测暴露;若存在则登记为结构性分配或优化路径 |
| `EmergencyReading` 构造栈分配转堆 | `struct` 类型保证栈分配;若 IL2CPP 装箱则实测暴露 |
