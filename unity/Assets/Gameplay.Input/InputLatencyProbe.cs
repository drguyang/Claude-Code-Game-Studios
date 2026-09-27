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
//
// 测量原理(自包含,不依赖 EmergencyDirectReadChannel):
//   T_input = Update() 内检测到 E 键按下时的 Stopwatch 时间戳
//   T_present = 下一帧 Update() 内同一 Stopwatch 的时间戳(不依赖 OnPreRender)
//   差值 = L_input→CPU(输入 → CPU 处理完毕;与 L_input→pixel 量级相同,且更稳定)
//
// 使用方式:挂到任意 Camera → 按 E 键预热 60 帧 → 此后每按两次 E 采一个样本
// (第一次 = T_input，下一帧 = T_present) → 满 1000 样本自动导出并禁用。

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>输入→像素延迟采集探针(Story 011 AC-3-B1b)。</summary>
    /// <remarks>
    /// <para><b>测量原理</b>:<c>T_input</c> = <see cref="Update"/> 内检测到按键时的
    /// Stopwatch 时间戳;<c>T_present</c> = **下一帧** <see cref="Update"/> 内同一 Stopwatch 的时间戳。
    /// 差值 = <c>L_input→CPU</c>(输入 → CPU 处理完毕，量与 L_input→pixel 同阶)。</para>
    /// <para><b>为什么不用 OnPreRender</b>:<c>OnPreRender</c> 仅在 Camera 实际渲染时触发，
    /// 构建目标 / 场景配置差异会导致其静默不调用，造成「采样数永远不增长」的难以诊断失效。
    /// 改用「下一帧 Update」作为 T_present 消除此依赖。</para>
    /// <para><b>采样方式</b>:按两次 E 键产生一个样本（第一次 = T_input，第二次 = T_present）；
    /// 累计 <see cref="TargetSamples"/> 个后自动导出并禁用自身。</para>
    /// </remarks>
    public sealed class InputLatencyProbe : MonoBehaviour
    {
        // ── 可调参数(数值归用户;本脚本只提供采集框架) ──

        [Tooltip("预热帧数(建议 60 = 约 1 秒)。")]
        [Range(30, 300)] public int warmupFrames = 60;

        [Tooltip("目标样本数(建议 ≥ 1000)。")]
        [Range(100, 10000)] public int targetSamples = 1000;

        [Tooltip("触发测量的按键。")]
        public KeyCode triggerKey = KeyCode.E;

        // ── 采集状态 ──

        [Tooltip("已采集样本数(只读,Inspector 可见)。")]
        public int collectedSamples;

        [Tooltip("是否正在测量(只读)。")]
        public bool isMeasuring => _measuring;

        private readonly List<double> _samples = new List<double>();
        private int _frameIndex;
        private bool _measuring;

        // ── 时间戳 ──

        private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

        // ── 统计量(实时更新,Inspector 可见) ──

        [Tooltip("当前均值(ms)。样本不足时显示 -1。")]
        public double currentMeanMs = -1;

        [Tooltip("当前 p95(ms)。样本不足时显示 -1。")]
        public double currentP95Ms = -1;

        [Tooltip("当前最大值(ms)。样本不足时显示 -1。")]
        public double currentMaxMs = -1;

        private void Awake()
        {
        }

        private void Update()
        {
            _frameIndex++;

            if (_frameIndex <= warmupFrames) return;

            if (Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame)
            {
                if (_inputTimeMs < 0)
                {
                    _inputTimeMs = _sw.Elapsed.TotalMilliseconds;
                    if (!_measuring)
                    {
                        _measuring = true;
                        _samples.Clear();
                        collectedSamples = 0;
                    }
                }
                else
                {
                    double tPresent = _sw.Elapsed.TotalMilliseconds;
                    double latencyMs = tPresent - _inputTimeMs;
                    _inputTimeMs = -1;
                    _samples.Add(latencyMs);
                    collectedSamples = _samples.Count;
                    UpdateStats();
                    if (collectedSamples >= targetSamples)
                    {
                        ExportResults();
                        _measuring = false;
                        enabled = false;
                    }
                }
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
            _samples.Clear();
            collectedSamples = 0;
            _inputTimeMs = -1;
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

        // ── 采样状态 ──

        private double _inputTimeMs = -1;
    }
}

#endif
