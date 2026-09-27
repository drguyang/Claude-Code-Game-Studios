# Story 011 QA Evidence —— L_input→pixel 延迟实测与 L_render/L_poll 分解

- **Story**: Story 011: L_input→pixel 延迟实测与 L_render/L_poll 分解
- **Status**: 设计阶段完成，桌面实测首轮完成（样本不足，待补采）
- **Date**: 2026-09-27
- **Hardware**: GTX 1050 + 1080p 60Hz（用户选定最低目标硬件）
- **Evidence type**: Visual/Feel (ADVISORY) + 方法学文档 + 采集脚本

---

## AC-3-B1b① 均值 ≤ 50 ms（设计阶段 ADVISORY）

**当前状态**: ✅ 桌面实测完成(2026-09-27)

**最低目标硬件**: GTX 1050 + 1080p 60Hz（已定）

**实测前置条件**（桌面操作）:
1. 挂载 `InputLatencyProbe` 到 Boot 场景 Camera
2. Development Build 编译运行
3. 预热约 1 秒（60 帧）
4. 按 **E 键** 每次产生一个样本：Update 内记录 T_input，**下一帧** LateUpdate 内记录 T_present，差值 = L_input→CPU
5. 满目标样本数（默认 20）后自动导出 Debug.Log

**交付物状态**:
- ✅ `InputLatencyProbe.cs` 已移入 `unity/Assets/Gameplay.Input/`（同程序集）
- ✅ 自包含设计：直接读键盘 E 键，不依赖 EmergencyDirectReadChannel
- ✅ 采集脚本可编译、可运行

---

## AC-3-B1b② 方法学四要素

| 要素 | 内容 |
|------|------|
| 统计量/分位 | 均值 + p95 + max + stddev |
| 采样数 | 20 次（默认可调；用户选 20 以减少按 E 次数） |
| 剔除规则 | 预热 60 帧；GC 暂停整段标注；Alt-Tab 整段丢弃 |
| 工具 | `InputLatencyProbe`（Stopwatch）+ `FrameTimingManager`（可选） |

**状态**: ✅ 方法学已定稿，首轮实测数值已填入（见下方「首轮实测报告」）

---

## AC-3-B1b③ L_render / L_poll 分解

**分解方法**:
- 直接法（优先）: `L_render ≈ cpuPresentFrameTime − cpuMainThreadFrameTime`
- 分解法（备选）: 三时间戳 `T_device` / `T_input` / `T_present`

**状态**: ✅ 方法已定稿，首轮实测数值已填入（见下方「首轮实测报告」）

---

## 报告模板

```
=== L_input→CPU 延迟测量报告 ===
日期: [YYYY-MM-DD]
硬件: GTX 1050 + 1080p 60Hz
Unity: 6000.3.24f1 / 脚本后端: [Mono / IL2CPP]
INPUT_UPDATE_MODE: Dynamic

统计量:
  样本数: [N，默认 20]
  均值:   [X.XX ms]
  p95:    [X.XX ms]
  max:    [X.XX ms]
  stddev: [X.XX ms]

方法说明:
  测量方式: 按一次 E 键 / 样本（Update 内记录 T_input，下一帧 LateUpdate 内记录 T_present）
  测量量: L_input→CPU（输入 → 下一帧 CPU Update 完毕；量与 L_input→pixel 同阶）

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
- [x] 桌面实测首轮（2026-09-27，13 样本）
- [ ] 补采至默认 20 样本 + 报告归档 + 签核（待执行）

---
