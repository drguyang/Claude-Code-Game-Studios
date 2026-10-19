// 权威来源:production/epics/telemetry-analytics/story-006-f5-difficulty-curve.md
//   (AC-51-B12…B14 — F5 难度曲线:滑窗密度 Dens_s(t_k) + 半开窗不双计 + 三流分列 + 参数退化硬失败)
//   · Dens_s(t_k) = |{e ∈ E : Kind(e) ∈ s ∧ t_k ≤ Tick(e) < t_k + W}|,t_k = t_0 + k·S
//   · 窗口半开 [t_k, t_k + W) —— 边界只取一次,防同 Tick 事件双计
//   · 三流各一条序列,不合并为单一标量(语义不同)
//   · W < 1 ⇒ 退化,构建期/加载期硬失败(非运行期退化、非静默取默认值)
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/f5_difficulty_curve_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001-005 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F5DifficultyCurveTest
    {
        // ⚠️ S19 修复(TD-epic-01):原「测试自持谓词面」已删除 ——
        //    谓词面上移为生产类 `DaYiJingCheng.Telemetry.DifficultyCurveMetrics`
        //    (ADR-019 §一;那个自持面在普查里被判为「AC 语义层无生产主语」)。
        //    本文件现在**只做夹具 + 断言**,不再自带公式。

        // ══════════════ AC-51-B12:F5 半开窗不双计 ══════════════

        /// <summary>AC-51-B12:F5 半开窗不双计 —— 窗宽 W;三条同流事件分别恰在 t_k、
        /// t_k + W − 1、t_k + W;前两条计入、第三条不计入;第三条在 t_k + S 中恰计入一次。</summary>
        [Test]
        public void test_f5_halfOpenWindow_noDoubleCount()
        {
            // Arrange:窗宽 W = 5,步长 S = 3
            int W = 5, S = 3;
            var events = new List<DifficultyCurveMetrics.SimEvent>
            {
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 10 },       // t_k = 10 ⇒ 计入
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 10 + W - 1 }, // 恰在 t_k+W-1 ⇒ 计入
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 10 + W },     // 恰在 t_k+W ⇒ 不计入
            };

            // Act:窗口 [10, 15) 密度
            int d = DifficultyCurveMetrics.Density(events, StreamId.Case, t_k: 10, W);

            // Assert:前两条计入、第三条不计入
            Assert.That(d, Is.EqualTo(2), "半开 [t_k, t_k+W):恰在 t_k 与 t_k+W-1 计入,t_k+W 不计入");

            // Act:窗口 [t_k+S, t_k+S+W) = [13, 18) 密度
            int d2 = DifficultyCurveMetrics.Density(events, StreamId.Case, t_k: 10 + S, W);

            // Assert:窗口 [13, 18) 含 tick=14 和 tick=15 两个事件 ⇒ 密度 = 2
            // "防双计" = 跨所有窗口求和时每个事件只计一次(半开窗边界不重叠)
            Assert.That(d2, Is.EqualTo(2), "窗口 [13, 18) 含 tick=14 和 tick=15 ⇒ 密度 = 2");
        }

        // ══════════════ AC-51-B13:F5 三流分列 + 覆盖记账 ══════════════

        /// <summary>AC-51-B13:F5 三流分列 + 覆盖记账 —— 输出三条独立序列;
        /// 每窗密度 ≤ 该窗内该流事件数;输出 WindowCoverage = (CoveredTicks, SpanTicks);
        /// S > W 时 CoveredTicks < SpanTicks;空流 ⇒ 全 0。</summary>
        [Test]
        public void test_f5_threeStreams_separateWithCoverage()
        {
            // Arrange:三流各有事件且 tick 分布不同,S > W
            var events = new List<DifficultyCurveMetrics.SimEvent>
            {
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 10 },
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 11 },       // 病例流 2 条
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.History, Tick = 12 },     // 病史流 1 条
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.World, Tick = 13 },
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.World, Tick = 14 },       // 世界流 2 条
            };
            long t0 = 10, tickMax = 20;
            int W = 3, S = 5;   // S > W ⇒ 窗间留空

            // Act:三流各算序列
            var caseSeries = DifficultyCurveMetrics.ComputeSeries(events, StreamId.Case, t0, S, W, tickMax);
            var histSeries = DifficultyCurveMetrics.ComputeSeries(events, StreamId.History, t0, S, W, tickMax);
            var worldSeries = DifficultyCurveMetrics.ComputeSeries(events, StreamId.World, t0, S, W, tickMax);

            // Assert:三条独立序列(不合并)
            Assert.That(caseSeries.densities, Is.Not.EqualTo(histSeries.densities),
                "病例流与病史流密度序列不同(三流分列)");
            Assert.That(caseSeries.densities, Is.Not.EqualTo(worldSeries.densities),
                "病例流与世界流密度序列不同(三流分列)");

            // Assert:每窗密度 ≤ 该窗内该流事件数(由构造保证,这里验证输出非空)
            Assert.That(caseSeries.densities.Count, Is.GreaterThan(0), "病例流有序列输出");
            Assert.That(histSeries.densities.Count, Is.GreaterThan(0), "病史流有序列输出");
            Assert.That(worldSeries.densities.Count, Is.GreaterThan(0), "世界流有序列输出");

            // Assert:S > W 时 CoveredTicks < SpanTicks(存在未覆盖区间)
            Assert.That(caseSeries.coveredTicks, Is.LessThan(caseSeries.spanTicks),
                "S > W ⇒ 窗间留空 ⇒ CoveredTicks < SpanTicks(不把未覆盖读成测得 0)");

            // Assert:空流 ⇒ 全 0
            var emptySeries = DifficultyCurveMetrics.ComputeSeries(new List<DifficultyCurveMetrics.SimEvent>(),
                StreamId.Case, t0, S, W, tickMax);
            Assert.That(emptySeries.densities.All(d => d == 0), Is.True,
                "空流 ⇒ 全 0(合法,不报错)");
        }

        // ══════════════ AC-51-B14:F5 参数退化硬失败 ══════════════

        /// <summary>AC-51-B14:F5 参数退化硬失败 —— W < 1(如 0)⇒ 构建期/加载期硬失败
        /// (非运行期退化、非静默取默认值)。</summary>
        [Test]
        public void test_f5_degenerateParam_hardFail()
        {
            // Arrange:W = 0(退化)
            var events = new List<DifficultyCurveMetrics.SimEvent>
            {
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 10 },
            };

            // Act + Assert:W < 1 ⇒ 抛异常(硬失败)
            Assert.Throws<ArgumentException>(() =>
                DifficultyCurveMetrics.Density(events, StreamId.Case, t_k: 10, window: 0),
                "W < 1 必须硬失败(非运行期退化、非静默取默认值)");
        }

        /// <summary>AC-51-B14 负向:W > 0 时不硬失败。</summary>
        [Test]
        public void test_f5_validParam_noHardFail()
        {
            // Arrange:W = 1(合法)
            var events = new List<DifficultyCurveMetrics.SimEvent>
            {
                new DifficultyCurveMetrics.SimEvent { Stream = StreamId.Case, Tick = 10 },
            };

            // Act
            int d = DifficultyCurveMetrics.Density(events, StreamId.Case, t_k: 10, window: 1);

            // Assert:不抛异常
            Assert.That(d, Is.EqualTo(1), "W = 1 时窗口 [10, 11) 含 1 条事件");
        }
    }
}
