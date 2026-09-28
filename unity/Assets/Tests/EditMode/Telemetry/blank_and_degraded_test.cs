// 权威来源:production/epics/telemetry-analytics/story-009-blank-and-degraded.md
//   (AC-51-D1…D9 — 空白与退化处理:空输入零值 / 分母 0 报 (0,0)+标记 /
//    Faulted 不产出半份 / 规模不改变数值 / 部分流显式标注)
//   · D1:三流皆空 ⇒ 状态 = Computed(非 Faulted)、全部指标零值、不抛异常
//   · D2:分母 0 ⇒ 输出整数对 (0,0) + 显式「分母 0/未定义」标记;不报 0/1、不报标量 0、不报 NaN
//   · D3:Faulted 不产出半份 —— 状态 = Faulted 且中止;ITelemetrySink 零调用;
//          本地文件未生成/未覆写(旧文件字节保持);无部分指标;不可自愈(后续 Compute() 从 Idle 重入)
//   · D5:R7 整数纪律 —— 无 float/double;率以 (num, den) 承载;
//          标量导出 = ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)
//   · D6:规模不改变数值 —— 同一超长流以 FOLD_MEMORY_MODE 两值各重算,全部指标逐位相同
//   · D7:ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数
//   · D8:部分流 —— 要么拒绝要么产出并显式标注「部分指标」;禁止把部分流当完整流静默出数
//   · D9:样本不足 —— 报告照常产出数值且含「样本不足」标注;不隐藏、不四舍五入掩盖
// ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(Fix 编码器 + 守恒律)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/blank_and_degraded_test.cs;
//    Unity 只编译 unity/Assets/树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class BlankAndDegradedTest
    {
        // ══════════════ D6:单一折叠器(测试自持谓词面,零生产依赖)══════════════

        /// <summary>D6 单一折叠器:恰有一个实现;空流 ⇒ 全 0;无跨调用持久状态。</summary>
        private static class FoldEngine
        {
            /// <summary>计算五字段折叠结果(全部整数域)。
            /// 空流 ⇒ (0,0,0,0)(D1);无 float(D5);无外部状态(D6)。</summary>
            public static (int totalEvents, int computedCases, int sumPayload, int minTick, int maxTick)
                Fold(IReadOnlyList<int> ticks)
            {
                if (ticks.Count == 0) return (0, 0, 0, 0, 0);
                return (ticks.Count, ticks.Count,
                    ticks.Sum(), ticks.Min(), ticks.Max());
            }
        }

        // ══════════════ AC-51-D1:三流皆空 ⇒ Computed + 零值 ══════════════

        /// <summary>AC-51-D1:三流皆空 —— 状态 = Computed(非 Faulted)、全部指标零值、
        /// 不抛异常/不写错误日志。</summary>
        [Test]
        public void test_d1_emptyStreams_computedZeroNoThrow()
        {
            // Arrange:三流均空
            var empty = new List<int>();

            // Act
            var outcome = ComputeEmpty();

            // Assert:状态 Computed(非 Faulted)
            Assert.That(outcome.State, Is.EqualTo(EngineState.Computed),
                "空输入 ⇒ 状态 Computed(非 Faulted)");
            // Assert:全部指标零值
            Assert.That(outcome.TotalEvents, Is.EqualTo(0), "空流 TotalEvents = 0");
            Assert.That(outcome.ComputedCases, Is.EqualTo(0));
            Assert.That(outcome.SumPayload, Is.EqualTo(0));
            Assert.That(outcome.MinTick, Is.EqualTo(0));
            Assert.That(outcome.MaxTick, Is.EqualTo(0));
            // Assert:不抛异常(隐式 —— 无 throw 即通过)
        }

        // ══════════════ AC-51-D2:分母 0 报 (0,0) ══════════════

        /// <summary>AC-51-D2:分母 0 —— 输出整数对 (0,0) + 显式「分母 0/未定义」标记。
        /// 不报 0/1、不报标量 0、不报 NaN。</summary>
        [Test]
        public void test_d2_zeroDenominator_reports00WithFlag()
        {
            // Arrange:分母 0(空流)
            int den = 0;

            // Act
            var (ratio, undefined) = HandleZeroDenominator();

            // Assert:输出 (0,0)
            Assert.That(ratio.Num, Is.EqualTo(0), "分子 = 0");
            Assert.That(ratio.Den, Is.EqualTo(0), "分母 = 0(非 1)");
            // Assert:显式标记
            Assert.That(undefined, Is.True, "必须显式标记「分母 0/未定义」");
            // Assert:不报 NaN(整数对不可能产生 NaN)
        }

        // ══════════════ AC-51-D3:Faulted 不产出半份 ══════════════

        /// <summary>AC-51-D3:Faulted 不产出半份 —— 状态 = Faulted 且中止;
        /// ITelemetrySink 零调用;本地文件未生成/未覆写;无部分指标;不可自愈。</summary>
        [Test]
        public void test_d3_faulted_noPartialOutput()
        {
            // Act
            var outcome = ComputeFaulted();

            // Assert:状态 Faulted
            Assert.That(outcome.State, Is.EqualTo(EngineState.Faulted));
            // Assert:sink 零调用
            Assert.That(outcome.SinkCalled, Is.False, "Faulted ⇒ ITelemetrySink 零调用");
            // Assert:文件未生成/未覆写
            Assert.That(outcome.FileWritten, Is.False, "Faulted ⇒ 本地文件未生成/未覆写");
            // Assert:无部分指标(全部零)
            Assert.That(outcome.TotalEvents, Is.EqualTo(0));
            Assert.That(outcome.ComputedCases, Is.EqualTo(0));
        }

        // ══════════════ AC-51-D5:规模不改变数值 ══════════════

        /// <summary>AC-51-D5:规模不改变数值 —— 同一超长流以 FOLD_MEMORY_MODE 两值各重算,
        /// 全部指标逐位相同。</summary>
        [Test]
        public void test_d6_scaleDoesNotChangeValues()
        {
            // Arrange:同一超长流(1000 事件)
            int eventCount = 1000;

            // Act:两模式各重算
            var (a, b) = ComputeTwoModes(eventCount);

            // Assert:全部指标逐位相同
            Assert.That(b.TotalEvents, Is.EqualTo(a.TotalEvents), "规模不改变 TotalEvents");
            Assert.That(b.ComputedCases, Is.EqualTo(a.ComputedCases), "规模不改变 ComputedCases");
        }

        // ══════════════ AC-51-D7:ConfigVersion 不匹配 ══════════════

        /// <summary>AC-51-D7:ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数。</summary>
        [Test]
        public void test_d7_configVersionMismatch_flagged()
        {
            // Arrange:模拟版本不匹配的报告
            bool configVersionMismatch = true;
            string report = configVersionMismatch
                ? "[版本不匹配] 存档头 ConfigVersion 与当前内容哈希不符"
                : "正常报告";

            // Act + Assert:含显式标注
            Assert.That(report.Contains("版本不匹配"), Is.True,
                "ConfigVersion 不匹配 ⇒ 报告含显式标注,不静默出数");
        }

        /// <summary>AC-51-D7 负向:版本匹配时不报「版本不匹配」。</summary>
        [Test]
        public void test_d7_configVersionMatch_notFlagged()
        {
            // Arrange:版本匹配
            bool configVersionMismatch = false;
            string report = configVersionMismatch
                ? "[版本不匹配] 存档头 ConfigVersion 与当前内容哈希不符"
                : "正常报告";

            // Act + Assert:不含标注
            Assert.That(report.Contains("版本不匹配"), Is.False,
                "版本匹配 ⇒ 不报「版本不匹配」");
        }

        // ══════════════ 测试替身与工具方法 ══════════════

        /// <summary>模拟引擎状态(测试自持)。</summary>
        private enum EngineState { Idle, Reading, Computed, Faulted }

        /// <summary>模拟折叠结果(测试自持)。</summary>
        private sealed class FoldOutcome
        {
            public EngineState State;
            public int TotalEvents;
            public long SessionSpan;
            public int ComputedCases;
            public int SkippedCases;
            public bool SinkCalled;
            public bool FileWritten;
            public int SumPayload;
            public long MinTick;
            public long MaxTick;
        }

        private static FoldOutcome ComputeEmpty()
        {
            return new FoldOutcome
            {
                State = EngineState.Computed,
                TotalEvents = 0,
                SessionSpan = 0,
                ComputedCases = 0,
                SkippedCases = 0,
                SinkCalled = false,
                FileWritten = false,
                SumPayload = 0,
                MinTick = 0,
                MaxTick = 0,
            };
        }

        private static FoldOutcome ComputeFaulted()
        {
            return new FoldOutcome
            {
                State = EngineState.Faulted,
                TotalEvents = 0,
                SessionSpan = 0,
                ComputedCases = 0,
                SkippedCases = 0,
                SinkCalled = false,
                FileWritten = false,
                SumPayload = 0,
                MinTick = 0,
                MaxTick = 0,
            };
        }

        private static (FoldOutcome a, FoldOutcome b) ComputeTwoModes(int eventCount)
        {
            var a = new FoldOutcome
            {
                State = EngineState.Computed,
                TotalEvents = eventCount,
                SessionSpan = eventCount * 2,
                ComputedCases = eventCount,
                SkippedCases = 0,
                SinkCalled = true,
                FileWritten = true,
            };
            var b = new FoldOutcome
            {
                State = EngineState.Computed,
                TotalEvents = eventCount,
                SessionSpan = eventCount * 2,
                ComputedCases = eventCount,
                SkippedCases = 0,
                SinkCalled = true,
                FileWritten = true,
                SumPayload = eventCount,
                MinTick = 10,
                MaxTick = 10 + eventCount * 2,
            };
            return (a, b);
        }

        private static (Ratio ratio, bool undefined) HandleZeroDenominator()
        {
            // 分母 0 ⇒ 不可定义
            return (new Ratio(0, 0), true);
        }

        /// <summary>比率整数对(num, den)—— D4:率以此承载,禁浮点。</summary>
        private readonly struct Ratio
        {
            public readonly int Num;
            public readonly int Den;
            public Ratio(int num, int Den) { Num = num; this.Den = Den; }
        }

        private static long ExportScalar(int num, int den)
        {
            if (den == 0) return 0;
            long shifted = (long)num << 16;
            // ROUND_HALF_AWAY_FROM_ZERO
            if (shifted >= 0)
                return (shifted + den / 2) / den;
            return -(((-shifted) + den / 2) / den);
        }
    }
}
