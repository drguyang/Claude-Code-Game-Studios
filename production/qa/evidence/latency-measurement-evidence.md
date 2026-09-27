# Story 011 QA Evidence —— L_input→pixel 延迟实测与 L_render/L_poll 分解

- **Story**: Story 011: L_input→pixel 延迟实测与 L_render/L_poll 分解
- **Status**: 设计阶段完成，待桌面硬件实测
- **Date**: 2026-09-27
- **Hardware**: GTX 1050 + 1080p 60Hz（用户选定最低目标硬件）
- **Evidence type**: Visual/Feel (ADVISORY) + 方法学文档 + 采集脚本

---

## AC-3-B1b① 均值 ≤ 50 ms（设计阶段 ADVISORY）

**当前状态**: ⏳ 待桌面实测

**最低目标硬件**: GTX 1050 + 1080p 60Hz（已定）

**实测前置条件**（桌面操作）:
1. 启用 Frame Timing Stats（Project Settings → Player → Other Settings）
2. Development Build 编译
3. 挂载 `InputLatencyProbe` 到 Boot 场景 Camera
4. 按 **一次 E 键** 开始采样；通道 Armed 期间每帧自动采集，满 1000 样本后自动导出
5. 导出 Debug.Log 结果

**交付物状态**:
- ✅ `InputLatencyProbe.cs` 已移入 `unity/Assets/Gameplay.Input/`（同程序集）
- ✅ `EmergencyDirectReadChannel.OnAfterUpdate()` 已加 `#if DEVELOPMENT_BUILD` 时间戳注入
- ✅ 采集脚本可编译、可运行

---

## AC-3-B1b② 方法学四要素

| 要素 | 内容 |
|------|------|
| 统计量/分位 | 均值 + p95 + max + stddev |
| 采样数 | 1000 次（建议值，用户可调） |
| 剔除规则 | 预热 60 帧；GC 暂停整段标注；Alt-Tab 整段丢弃 |
| 工具 | `InputLatencyProbe`（Stopwatch）+ `FrameTimingManager`（可选） |

**状态**: ✅ 方法学已定稿，待实测填入数值

---

## AC-3-B1b③ L_render / L_poll 分解

**分解方法**:
- 直接法（优先）: `L_render ≈ cpuPresentFrameTime − cpuMainThreadFrameTime`
- 分解法（备选）: 三时间戳 `T_device` / `T_input` / `T_present`

**状态**: ✅ 方法已定稿，待实测填入数值

---

## 报告模板

```
=== L_input→pixel 延迟测量报告 ===
日期: [YYYY-MM-DD]
硬件: GTX 1050 + 1080p 60Hz
Unity: 6000.3.24f1 / 脚本后端: [Mono / IL2CPP]
INPUT_UPDATE_MODE: Dynamic

统计量:
  样本数: [N ≥ 1000?]
  均值:   [X.XX ms]
  p95:    [X.XX ms]
  max:    [X.XX ms]
  stddev: [X.XX ms]

L_render / L_poll 分解:
  L_render: [X.XX ms] (≈ [N] 帧 @ [Hz])
  L_poll:   [X.XX ms]
  残差:     [X.XX ms]

判定:
  [ ] 均值 ≤ 50 ms → [PASS / FAIL]
```

---

## 签核

- [x] 方法学文档定稿
- [x] 采集脚本交付
- [x] 接线侧时间戳注入完成
- [ ] 桌面实测（待执行）
- [ ] 报告归档 + 签核（待实测后）
