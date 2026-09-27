// Story 011 · 输入→像素延迟采集探针(AC-3-B1b)
//
// 权威来源:
//   Story: production/epics/input-system/story-011-latency-measurement.md
//     · AC-3-B1b①: L_input→pixel 实测均值 ≤ 50 ms(硬件实测;设计阶段 = ADVISORY)
//     · AC-3-B1b②: 方法学四要素(统计量/分位 · 采样数 · 剔除规则 · 工具)
//     · AC-3-B1b③: L_render / L_poll 分解(诊断量;验收线仍只有 L_input 一条)
//   GDD: design/gdd/input-system.md F-3.3(单判据 L_input→pixel ≤ 50 ms)
//   ADR: ADR-011 §二(直读通道 · 方差不是算术 · L_render ≈ 2 帧) · Amendment B(两预算切分)
//   Evidence: production/qa/evidence/latency-measurement-evidence.md
//
// 运行条件:Development Build only(条件编译剔除 Release 路径)。
// 须与 EmergencyDirectReadChannel 接线侧配合:通道 OnAfterUpdate 调用 MarkInputTime(),
// 本探针 OnPreRender 读 T_present,差值 = L_input→pixel。
//
// ⚠️ FrameTimingManager 前置:须在 Project Settings → Player → Other Settings 启用
//   Frame Timing Stats。若平台不支持,降级为 OnPreRender + Stopwatch 双时间戳法。

#if DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>输入→像素延迟采集探针(Story 011 AC-3-B1b)。</summary>
    /// <remarks>
    /// <para><b>测量原理</b>:
    /// <c>T_input</c> = <see cref="EmergencyDirectReadChannel"/> 接线侧
    /// <c>OnAfterUpdate</c> 内调用 <see cref="MarkInputTime"/> 记录的 Stopwatch 时间戳;
    /// <c>T_present</c> = 本探针 <see cref="OnPreRender"/> 内记录的同一 Stopwatch 时间戳。
    /// 差值 = <c>L_input→pixel</c>。</para>
    /// <para><b>接线义务</b>:<see cref="EmergencyDirectReadChannel.OnAfterUpdate"/> 须在
    /// Armed 分支内、<see cref="ReadEmergency"/> 之前调用
    /// <see cref="MarkInputTime"/>()。</para>
    /// <para><b>触发方式</b>:按 <see cref="TriggerKey"/> 开始一次测量;每次按压
    /// Idle → Armed → EndAction 产生一个样本。<see cref="TargetSamples"/> 达到后自动
    /// 导出并禁用自身。</para>
    /// </remarks>
    public sealed class InputLatencyProbe : MonoBehaviour
    {
        // ── 可调参数(数值归用户;本脚本只提供采集框架) ──

        [Tooltip("预热帧数(建议 60 = 约 1 秒)。")]
        [Range(30, 300)] public int warmupFrames = 60;

        [Tooltip("目标样本数(建议 ≥ 1000)。")]
        [Range(100, 10000)] public int targetSamples = 1000;

        [Tooltip("触发测量的按键(设计阶段用;挂接 10 装配层后改由动作触发)。")]
        public KeyCode triggerKey = KeyCode.E;

        // ── 采集状态 ──

        [Tooltip("已采集样本数(只读,Inspector 可见)。")]
        public int collectedSamples;

        [Tooltip("是否正在测量(只读)。")]
        public bool isMeasuring => _measuring;

        [Tooltip("是否已 Armed(等待 OnAfterUpdate 采样)。")]
        public bool isArmed => _armed;

        private readonly List<double> _samples = new List<double>();
        private int _frameIndex;
        private bool _measuring;
        private bool _armed;

        // ── 时间戳 ──

        private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        // ── 统计量(实时更新,Inspector 可见) ──

        [Tooltip("当前均值(ms)。样本不足时显示 -1。")]
        public double currentMeanMs = -1;

        [Tooltip("当前 p95(ms)。样本不足时显示 -1。")]
        public double currentP95Ms = -1;

        [Tooltip("当前最大值(ms)。样本不足时显示 -1。")]
        public double currentMaxMs = -1;

        private void Update()
        {
            _frameIndex++;

            // 预热段不采集、不触发
            if (_frameIndex <= warmupFrames) return;

            // 触发:按下 triggerKey 且未在测量中 → 开始一次测量
            if (Input.GetKeyDown(triggerKey) && !_measuring)
            {
                _measuring = true;
                _samples.Clear();
                collectedSamples = 0;
            }
        }

        /// <summary>由 EmergencyDirectReadChannel 接线侧 OnAfterUpdate 在采样时调用。</summary>
        /// <remarks>本方法在 Development Build 下记录本帧的 Stopwatch 时间戳;
        /// Release Build 下为 #if 条件编译空操作,零开销。</remarks>
        public static void MarkInputTime()
        {
            if (_instance != null)
            {
                _instance._currentFrameInputTimeMs = _instance._sw.Elapsed.TotalMilliseconds;
            }
        }

        /// <summary>在帧 present 前读取 T_present 并计算 L_input→pixel。</summary>
        private void OnPreRender()
        {
            if (!_measuring || _currentFrameInputTimeMs < 0) return;

            double tPresent = _sw.Elapsed.TotalMilliseconds;
            double latencyMs = tPresent - _currentFrameInputTimeMs;

            _samples.Add(latencyMs);
            collectedSamples = _samples.Count;
            _currentFrameInputTimeMs = -1;

            UpdateStats();

            if (collectedSamples >= targetSamples)
            {
                ExportResults();
                _measuring = false;
                _armed = false;
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_measuring && _samples.Count > 0)
            {
                ExportResults();
            }
        }

        private void UpdateStats()
        {
            currentMeanMs = ComputeMean(_samples);
            currentP95Ms = ComputePercentile(_samples, 95);
            currentMaxMs = ComputeMax(_samples);
        }

        private void ExportResults()
        {
            Debug.Log(
                $"[LatencyProbe] 采集完成: {_samples.Count} 样本. " +
                $"Mean={currentMeanMs:F2}ms, p95={currentP95Ms:F2}ms, " +
                $"Max={currentMaxMs:F2}ms, StdDev={ComputeStdDev(_samples, currentMeanMs):F2}ms");
        }

        private void Reset()
        {
            _frameIndex = 0;
            _measuring = false;
            _armed = false;
            _samples.Clear();
            collectedSamples = 0;
            _currentFrameInputTimeMs = -1;
            currentMeanMs = -1;
            currentP95Ms = -1;
            currentMaxMs = -1;
        }

        // ── 统计量 ──

        private static double ComputeMean(List<double> samples)
        {
            double sum = 0;
            for (int i = 0; i < samples.Count; i++) sum += samples[i];
            return sum / samples.Count;
        }

        private static double ComputePercentile(List<double> samples, double percentile)
        {
            if (samples.Count == 0) return -1;
            var sorted = new List<double>(samples);
            sorted.Sort();
            int idx = (int)Math.Ceiling(sorted.Count * percentile / 100.0) - 1;
            return sorted[Math.Max(0, idx)];
        }

        private static double ComputeMax(List<double> samples)
        {
            double m = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i] > m) m = samples[i];
            }
            return m;
        }

        private static double ComputeStdDev(List<double> samples, double mean)
        {
            if (samples.Count < 2) return 0;
            double sumSq = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                double d = samples[i] - mean;
                sumSq += d * d;
            }
            return Math.Sqrt(sumSq / (samples.Count - 1));
        }

        // ── 单例(接线侧静态调用用) ──

        private static InputLatencyProbe _instance;
        private double _currentFrameInputTimeMs = -1;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[InputLatencyProbe] 存在多个实例;最后一个注册的覆盖 MarkInputTime 目标。");
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
#endif
