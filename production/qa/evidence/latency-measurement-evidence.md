# Story 011 · L_input→pixel 延迟实测与 L_render/L_poll 分解

> **AC-3-B1b**: 方法学四要素 + L_render/L_poll 分解
> **Status**: 设计阶段方法学已定稿,数值待 `/test-setup` 轮补实测
> **Target Hardware**: 待 `/test-setup` 轮定稿(最低目标机型)

---

## 1. 验收线定义

### 1.1 单条硬预算

```
判据: L_input→pixel ≤ 50 ms
     (覆盖:输入采样 → 判定 → 提交 → 像素点亮 的全链)
```

**不得**把 `L_eval`(tick 求值次序,9 的节奏)并进本式。两预算切分口径见 `emergency-procedures.md` F-10.6。

### 1.2 分项分解表

| 分项 | 含义 | 测量方式 | 备注 |
|------|------|----------|------|
| `L_poll` | 输入设备 → 动作值对 3 可见 | `L_input→pixel` − `L_render` − 残差;或直接测 `onAfterUpdate` 与输入事件入队的间隔 | 随 `INPUT_UPDATE_MODE` 变化;本项目已钉死 = `Dynamic` |
| `L_axis` | F-3.1 曲线求值 | 计入 `L_input` 残差(3 内部,O(1),可忽略) | 不单独测量 |
| `L_judge` | 10 的判定求值 | 计入 `L_input` 残差 | 纯逻辑,无 I/O |
| `L_schedule` | 帧内调度抖动(若走 UI 栈) | 本条不适用(直读通道已消灭此源) | 诊断用 |
| `L_render` | 提交 → 像素点亮 | `FrameTimingManager` 或 `PresentCall` 帧时间差 | ≈ 2 帧(60 fps ≈ 33 ms;90 fps ≈ 22 ms);**单独可能吃掉全部预算** |
| `L_eval` | tick 求值 + 呈现刷新 | **不在本条验收面**(9 的节奏;玩家对体征延迟一个 tick 是预期) | 禁止并进 `L_input→pixel` |

> **定性**:若 `L_render ≥ 50 ms`,则 `L_input→pixel ≤ 50 ms` **在结构上不可满足**,与 3 无关。
> 降级路径(设计阶段预案):锁定刷新率 + 独占全屏。⚠️ 定性 = **节奏/一致性** 措施,不是延迟措施;
> 144 Hz 面板锁 60 Hz **会抬高**均值延迟,买到的是**刷新相位稳定性**(方差),不是更低均值。
> 不得当作「降延迟」写进任何报告。

---

## 2. 方法学四要素

### 2.1 统计量 / 分位

| 指标 | 定义 | 用途 |
|------|------|------|
| 均值 | 全部采样算术平均 | 主验收指标(≤ 50 ms) |
| p95 | 第 95 百分位 | 方差诊断(抖动带宽度) |
| max | 最大值 | 尾延迟诊断 |
| stddev | 标准差 | 方差量级 |

**阈值**:
- 均值 ≤ 50 ms → **ADVISORY 通过 / BLOCKING 通过**(发版前)
- p95 ≤ 70 ms → 记录但**不归本故事签核**(归 10 的 F-10.3b)
- max 无上界 → 只记录,用于诊断

### 2.2 采样数

| 参数 | 值 | 理由 |
|------|-----|------|
| `MIN_SAMPLES` | 1000 | 建议值(用户可调);p95 估计需要 ≥ 1000 次独立按压才有统计意义 |
| `WARMUP_FRAMES` | 60 | 约 1 秒(60 fps);JIT / 静态初始化分配已在故事 010 的 E5 中验证为预热段后归零 |
| `MEASUREMENT_FRAMES` | 300 | 约 5 秒(60 fps);覆盖 ≈ 9 个完整 CPR 周期 |

**采样独立性保证**:每次按压从 Idle → Armed → EndAction 完整走一遍,不计半途丢弃的样本。
连续 5 秒内的多次按压自然产生独立样本。

### 2.3 剔除规则(事前声明)

| 分配源 | 处理方式 | 备注 |
|--------|----------|------|
| 前 60 帧(预热) | 丢弃 | JIT / 首次激活分配 |
| 中途触发 GC 的帧 | 保留,单独标注 | GC 暂停 ≠ `L_input` 本身;但会污染 `L_render` 测量,需在报告中注明 |
| Profiler 开销 | 忽略(Development Build 下 ProfilerRecorder 开销 < 0.1 ms/frame) | 工具面已知量 |
| 窗口外 Alt-Tab / 失焦 | 整段丢弃(若发生) | 失焦改变 `INPUT_UPDATE_MODE` 行为 |
| `NotifyAfterUpdateForTest` 测试缝 | 测量路径不走测试缝 | 测量走接线侧 `OnAfterUpdate` → `ReadEmergency` |

**关键**:剔除规则须在测量**前**写进报告,不允许事后挑选「好看的数字」。

### 2.4 工具

#### 2.4.1 输入采样时间戳

**手段**:`System.Diagnostics.Stopwatch` 或 `UnityEngine.Profiling.Recorder`。

- `Stopwatch` 精度:CPU 时钟周期(~1 μs),适合 `L_poll` 与 `L_input` 的细粒度测量
- **注意**:Stopwatch 测的是 CPU 时间,不是显示刷新时间;`L_render` 须用 `FrameTimingManager`

**测量点**:
```
T_input:   InputSystem.onAfterUpdate 回调入口(Input 已更新,Emergency 值可见)
T_present: FrameTimingManager.GetFrameTiming() 的 presentTime 或 CPU present 标记
```

```
L_input→pixel = T_present − T_input
L_render       = T_present − T_input − L_judge − L_axis  (残差法)
L_poll         = L_input→pixel − L_render                          (分解法)
```

#### 2.4.2 渲染完成时间戳

**手段**:`UnityEngine.FrameTimingManager`(需在 Project Settings → Player → Other Settings 启用 `Frame Timing Stats`)。

```csharp
// 采集帧的 FrameTiming 数据
FrameTiming[] timings = new FrameTiming[1];
FrameTimingManager.GetLatestTimings(1, timings);
long cpuPresentTimeMs = timings[0].cpuPresentFrameTime; // CPU 提交到 present 的时间(ms)
long gpuFrameTimeMs   = timings[0].gpuFrameTime;        // GPU 耗时(ms)
```

**替代手段**(FrameTimingManager 不可用时):
- `MonoBehaviour.OnPostRender()` / `CommandBuffer` 注入时间戳
- `Graphics.DrawTexture` 后回调的时间差

#### 2.4.3 Development Build 要求

`ProfilerRecorder` + `FrameTimingManager` 在非 Development Build 中行为不同/不可用。
**测量必须在 Development Build 下执行**,Release Build 仅用于验证性能预算未破。

---

## 3. 测量流程

### 3.1 采集脚本(设计阶段定稿,待桌面执行)

```csharp
// File: tools/latency-measurement/InputLatencyProbe.cs
// 运行期采集脚本(Development Build only)
// 挂载到 Boot 场景的 Camera 上,或通过 Addressables 预载后注入
#if DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace DaYiJingCheng.EditorTools.Latency
{
    /// <summary>输入→像素延迟采集探针(Story 011 AC-3-B1b)。</summary>
    /// <remarks>
    /// 测量 `L_input→pixel` = 输入采样点(T_onAfterUpdate)到像素点亮(T_present)的间隔。
    /// 须与 <see cref="EmergencyDirectReadChannel"/> 的接线侧配合使用:
    /// 通道在 OnAfterUpdate 内记录 T_input,本探针在同一帧的 present 时刻记录 T_present。
    /// </remarks>
    public sealed class InputLatencyProbe : MonoBehaviour
    {
        // ── 可调参数(数值归用户;本脚本只提供采集框架) ──
        [Range(60, 600)] public int warmupFrames = 60;
        [Range(100, 5000)] public int targetSamples = 1000;
        public KeyCode triggerKey = KeyCode.E;

        // ── 采集状态 ──
        private readonly List<double> _samples = new List<double>();
        private int _frameIndex;
        private bool _measuring;
        private bool _armed;

        // ── 与 EmergencyDirectReadChannel 共享的时间戳 ──
        /// <summary>3 侧在 OnAfterUpdate 入口写入的本帧采样时间戳(ms,Stopwatch)。</summary>
        public static double CurrentFrameInputTimeMs { get; private set; } = -1;

        private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        private void Update()
        {
            _frameIndex++;

            // 预热段不采集
            if (_frameIndex <= warmupFrames) return;

            // 触发:按下 triggerKey 且通道未 Armed → 开始一次测量
            if (Input.GetKeyDown(triggerKey) && !_armed)
            {
                _armed = true;
                // 通知 EmergencyDirectReadChannel 开始采样(经其公开 Arm 方法)
                // 实际接线由 10 的装配层完成,本探针只负责时间戳采集
            }
        }

        /// <summary>由 EmergencyDirectReadChannel 的接线侧 OnAfterUpdate 在采样时调用。</summary>
        public static void MarkInputTime()
        {
            CurrentFrameInputTimeMs = GetElapsedMs();
        }

        /// <summary>在帧 present 前读取 T_present 并计算 L_input→pixel。</summary>
        private void OnPreRender()
        {
            if (!_armed || CurrentFrameInputTimeMs < 0) return;

            double tPresent = GetElapsedMs();
            double latencyMs = tPresent - CurrentFrameInputTimeMs;

            _samples.Add(latencyMs);
            _armed = false;
            CurrentFrameInputTimeMs = -1;

            if (_samples.Count >= targetSamples)
            {
                ExportResults();
                enabled = false;
            }
        }

        private static double GetElapsedMs() => System.Diagnostics.Stopwatch.GetTimestamp() /
            (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;

        private void ExportResults()
        {
            // 输出到 ProfilerLog / 文件 —— 具体路径归工具层
            Debug.Log($"[LatencyProbe] Collected {_samples.Count} samples. " +
                      $"Mean={Mean(_samples):F2}ms, p95={Percentile(_samples, 95):F2}ms, " +
                      $"Max={Max(_samples):F2}ms");
        }

        private static double Mean(List<double> samples)
        {
            double sum = 0;
            for (int i = 0; i < samples.Count; i++) sum += samples[i];
            return sum / samples.Count;
        }

        private static double Percentile(List<double> samples, double p)
        {
            var sorted = new List<double>(samples);
            sorted.Sort();
            int idx = (int)Math.Ceiling(sorted.Count * p / 100.0) - 1;
            return sorted[Math.Max(0, idx)];
        }

        private static double Max(List<double> samples)
        {
            double m = 0;
            for (int i = 0; i < samples.Count; i++) if (samples[i] > m) m = samples[i];
            return m;
        }
    }
}
#endif
```

**设计说明**:
- `MarkInputTime()` 须由 `EmergencyDirectReadChannel` 的接线侧 `OnAfterUpdate` 调用(见下方 §3.2)
- `OnPreRender()` 在 present 前捕获 `T_present`;若平台无 `OnPreRender`,替换为 `OnPostRender` + 1 帧偏移校准
- `targetSamples` = 1000 是**建议值**,用户可在 Inspector 调整
- 本脚本只采集,不做判定 —— 判定在报告阶段进行

### 3.2 接线侧改动(3 的实现义务)

`EmergencyDirectReadChannel.OnAfterUpdate()` 须在采样点注入时间戳:

```csharp
// EmergencyDirectReadChannel.OnAfterUpdate() 的 Armed 分支内,ReadEmergency() 之前:
#if DEVELOPMENT_BUILD
DaYiJingCheng.EditorTools.Latency.InputLatencyProbe.MarkInputTime();
#endif
```

**注意**:该注入只在 Development Build 生效;Release Build 零开销(条件编译剔除)。
`/test-setup` 轮的硬件实测需要先在 Development Build 下跑一次采集,Release Build 验证为独立的后续步骤。

### 3.3 帧计时采集补充

`FrameTimingManager` 数据须在测量同一帧内采集:

```csharp
// 挂在同一 GameObject 上的 FrameTiming 采集组件
FrameTiming[] timings = new FrameTiming[1];
bool available = FrameTimingManager.GetLatestTimings(1, timings);
if (available)
{
    // timings[0].cpuPresentFrameTime 单位 = ms
    // 与 InputLatencyProbe 的时间戳差值 = L_input→pixel
}
```

**⚠️ `FrameTimingManager` 前置**:须在 Project Settings → Player → Other Settings 启用 `Frame Timing Stats`。
若目标平台不支持,降级为 `OnPreRender`/`OnPostRender` + `Stopwatch` 双时间戳差值法(精度略低,但结构相同)。

---

## 4. L_render / L_poll 分解方法

### 4.1 直接法(优先)

```
L_input→pixel = T_present − T_input          (FrameTimingManager cpuPresentFrameTime − Stopwatch)
L_render       = L_input→pixel − L_poll_est   (残差,或直接读 cpuPresentFrameTime − cpuMainThreadFrameTime)
L_poll         = T_input − T_device           (须设备层配合,见下)
```

**`L_render` 直接读法**:`FrameTimingManager.GetLatestTimings()` 返回的:
- `cpuMainThreadFrameTime` = CPU 主线程帧耗时(含逻辑 + 渲染)
- `cpuPresentFrameTime` = CPU 提交 present 的时间戳

`L_render ≈ cpuPresentFrameTime − cpuMainThreadFrameTime`(简化近似,忽略 GPU 耗时差异)。

### 4.2 分解法(备选)

若 `FrameTimingManager` 不可用,用三时间戳分解:

```
T_device: 输入设备事件入队(须平台原生插件或 InputDevice 的 onEvent 回调)
T_input:   OnAfterUpdate 采样点(Stopwatch)
T_present: OnPreRender 或 GPU fence(Stopwatch)

L_poll   = T_input  − T_device   (设备 → 3 可见)
L_render = T_present − T_input    (3 采样 → 像素)
L_input→pixel = T_present − T_device
```

**`T_device` 的获取**:
- K&M: `InputSystem.onEvent` 回调中记录原始事件时间戳(Unity 不直接暴露,须 `InputEventTrace` 或原生插件)
- Gamepad: 同 K&M
- **备选**:若 `T_device` 不可得,用 `L_poll = L_input→pixel − L_render_est` 倒推,并在报告中注明「L_poll 为估计值」

---

## 5. 报告格式

测量完成后,报告须包含以下字段(缺一 = 不可签核):

```
=== L_input→pixel 延迟测量报告 ===
日期: [YYYY-MM-DD]
硬件: [机型 / OS / 刷新率]
Unity: [版本 / 脚本后端]
INPUT_UPDATE_MODE: Dynamic(已钉死)

统计量:
  样本数: [N ≥ 1000?]
  均值:   [X.XX ms]
  p95:    [X.XX ms]
  max:    [X.XX ms]
  stddev: [X.XX ms]

L_render / L_poll 分解:
  L_render: [X.XX ms] (≈ [N] 帧 @ [Hz])
  L_poll:   [X.XX ms]
  残差(L_axis + L_judge + L_schedule): [X.XX ms]
  验证: L_render + L_poll + 残差 ≈ L_input→pixel (允许 ±2 ms 测量误差)

剔除规则:
  预热帧: [N 帧]
  剔除样本数: [N]
  剔除原因: [GC 暂停 / Alt-Tab / ...]

工具:
  帧捕获: [FrameTimingManager / OnPreRender + Stopwatch]
  输入时间戳: [InputLatencyProbe.MarkInputTime]
  后端: [Mono / IL2CPP]

判定:
  [ ] 均值 ≤ 50 ms → [PASS / FAIL]
  [ ] 方法学四要素齐全 → [PASS / FAIL]
  [ ] L_render / L_poll 分解自洽 → [PASS / FAIL]
  [ ] 无「端到端 = L_input + L_eval」型合并表述 → [PASS / FAIL]
```

---

## 6. 当前状态

| 步骤 | 状态 | 备注 |
|------|------|------|
| 方法学文档 + 四要素 | ✅ 已定稿 | 本文件 |
| 采集脚本(InputLatencyProbe) | ✅ 已定稿 | 待接线侧注入 `MarkInputTime()` |
| 接线侧时间戳注入 | ✅ 已定稿 | `EmergencyDirectReadChannel.OnAfterUpdate()` 加 `#if DEVELOPMENT_BUILD` 块 |
| L_render/L_poll 分解方法 | ✅ 已定稿 | 直接法(优先) + 分解法(备选) |
| 最低目标硬件定稿 | ⏳ 挂 `/test-setup` 轮 | 硬件未定 → 设计阶段不跑实测 |
| 桌面实测(Mono + IL2CPP) | ⏳ 待桌面 | Development Build + ProfilerRecorder + 真实硬件 |
| 报告归档 + 签核 | ⏳ 待实测后 | 设计阶段标 ADVISORY;发版前翻 BLOCKING |

---

## 7. 风险与限制

| 风险 | 影响 | 缓解 |
|------|------|------|
| `FrameTimingManager` 平台支持不完整 | `L_render` 无法精确测量 | 降级为 `OnPreRender` + Stopwatch 双时间戳 |
| `T_device` 不可得(Unity 不暴露原生事件时间戳) | `L_poll` 只能估计 | 用 `L_input→pixel − L_render` 倒推;报告注明 |
| 144 Hz 面板锁 60 Hz 抬高均值 | 均值延迟增加,但方差降低 | **定性**:这是节奏措施,不是延迟措施;报告中须写清 |
| IL2CPP vs Mono 帧计时差异 | 双后端结果不一致 | 双后端各出一行结论;不一致时逐方法下钻 |
| Alt-Tab / 失焦改变 `INPUT_UPDATE_MODE` | 采样污染 | 剔除规则:失帧段整段丢弃 |
| Development Build 本身引入开销 | 测量值偏高(≠ 出货形态) | Release Build 独立验证;开发与出货各出一份结论 |

---

## 8. 与 Story 010 的关系

Story 010(E5)测的是 **`GC.Alloc == 0`**(分配侧,Development Build 探针)。
本故事测的是 **`L_input→pixel ≤ 50 ms`**(延迟侧,Stopwatch + FrameTiming)。

两条判据**正交**:
- E5 通过 ≠ 延迟达标(零分配但每帧延迟 60 ms = 手感断裂)
- 本故事通过 ≠ 零分配(低延迟但每帧分配 200 bytes = GC 抖动杀帧率)

**须双过**才能签核 `TR-concept-007`。

---

## 9. 后续步骤

1. **`/test-setup` 轮**:定稿最低目标硬件 → 绑定本报告的硬件行
2. **桌面实测**:Development Build + 目标硬件 + 双后端 → 填入 §5 报告模板
3. **发版前签核**:B1b① 均值 ≤ 50 ms 翻 BLOCKING,挂 release checklist
4. **若均值不达标**:按 ADR-011 降级路径(锁定刷新率 + 独占全屏)重测;仍不达标 → 须重新与用户裁定 `TR-concept-007` 可行性
