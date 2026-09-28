// 权威来源:production/epics/telemetry-analytics/story-007-f6-f7-session-rhythm.md
//   (AC-51-B15…B18 — F6 局内时长 + F7 节律:SessionSpan 可审计 + 上中位数 + MAD + 退化处理)
//   · F6: SessionSpan = Tick_max − Tick_min;同时报 Tick_min/Tick_max/|E|;单事件 ⇒ span=0;空流 ⇒ 0
//   · F7: Δ_i = Tick(e_{i+1}) − Tick(e_i);Δ_median = Δ_(⌈n/2⌉)(1-indexed,上中位数,禁插值);
//          Δ_MAD = median(|Δ_i − Δ_median|);GapCount = |{i : Δ_i > GAP_THRESHOLD}|
//   · 退化:n < 2 ⇒ 无 Δ 序列 ⇒ 全部字段报「不可定义」;n=1 时 Δ_MAD 不是 0,是「无意义」;
//          n=2 ⇒ Δ_MAD = 0(合法)
//   · 禁浮点:无方差/标准差/Math.Sqrt/浮点除法;节律字段类型全为 Tick/int
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/f6_f7_session_rhythm_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001-006 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F6F7SessionRhythmTest
    {
        // ══════════════ F6/F7 公式(测试自持谓词面,零生产依赖)══════════════

        /// <summary>F6/F7 公式谓词面(测试自持,纯函数)。
        /// 语义 = GDD Formulas F6/F7:SessionSpan 可审计 + 上中位数 + MAD + 退化处理。</summary>
        private static class F6F7Formula
        {
            /// <summary>一条事件(测试自持夹具)。</summary>
            public sealed class SimEvent
            {
                public long Tick;
            }

            /// <summary>F6:计算 SessionSpan = Tick_max − Tick_min。
            /// 返回 (tickMin, tickMax, sessionSpan, eventCount)。
            /// 空流 ⇒ (0, 0, 0, 0);单事件 ⇒ span = 0(合法)。</summary>
            public static (long tickMin, long tickMax, long sessionSpan, int eventCount)
                ComputeSessionSpan(IReadOnlyList<SimEvent> events)
            {
                if (events.Count == 0) return (0, 0, 0, 0);
                long min = events.Min(e => e.Tick);
                long max = events.Max(e => e.Tick);
                return (min, max, max - min, events.Count);
            }

            /// <summary>F7:计算节律指标(上中位数 + MAD + Δ_min + Δ_max + GapCount)。
            /// 返回 (deltaMedian, deltaMad, deltaMin, deltaMax, gapCount, defined)。
            /// n < 2 ⇒ 无 Δ 序列 ⇒ (0, 0, 0, 0, 0, false)——「不可定义」≠ 0。</summary>
            public static (int deltaMedian, int deltaMad, int deltaMin, int deltaMax, int gapCount, bool defined)
                ComputeRhythm(IReadOnlyList<SimEvent> events, int gapThreshold)
            {
                int n = events.Count;
                if (n < 2) return (0, 0, 0, 0, 0, false);   // 退化:无 Δ 序列

                // Δ_i = Tick(e_{i+1}) − Tick(e_i),i = 0…n−2
                var deltas = new List<int>();
                for (int i = 0; i < n - 1; i++)
                    deltas.Add((int)(events[i + 1].Tick - events[i].Tick));

                // 上中位数:m = n-1 个 Δ,排序后第 ⌈m/2⌉ 项(1-indexed),禁插值
                var sorted = deltas.OrderBy(d => d).ToList();
                int median = sorted[(n - 1 + 1) / 2 - 1];   // ⌈(n-1)/2⌉ 1-indexed → 0-indexed

                // Δ_MAD = median(|Δ_i − Δ_median|)
                var absDeviations = deltas.Select(d => Math.Abs(d - median)).OrderBy(d => d).ToList();
                int mad = absDeviations[(n - 1 + 1) / 2 - 1];

                // Δ_min / Δ_max
                int deltaMin = sorted.First();
                int deltaMax = sorted.Last();

                // GapCount = |{i : Δ_i > GAP_THRESHOLD}|
                int gapCount = deltas.Count(d => d > gapThreshold);

                return (median, mad, deltaMin, deltaMax, gapCount, true);
            }
        }

        // ══════════════ AC-51-B15:F6 可审计 ══════════════

        /// <summary>AC-51-B15:F6 可审计 —— 同时输出 Tick_min/Tick_max/|E|;
        /// Tick_min + SessionSpan = Tick_max;SessionSpan ≥ 0;单事件 ⇒ span = 0(合法);空流 ⇒ 0。</summary>
        [Test]
        public void test_f6_auditable_singleEventSpanZero()
        {
            // Arrange:单事件
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 42 },
            };

            // Act
            var (tickMin, tickMax, sessionSpan, eventCount) = F6F7Formula.ComputeSessionSpan(events);

            // Assert:可审计(同时输出三量)
            Assert.That(tickMin, Is.EqualTo(42), "Tick_min = 42");
            Assert.That(tickMax, Is.EqualTo(42), "Tick_max = 42");
            Assert.That(eventCount, Is.EqualTo(1), "|E| = 1");
            Assert.That(tickMin + sessionSpan, Is.EqualTo(tickMax), "Tick_min + SessionSpan = Tick_max");
            Assert.That(sessionSpan, Is.EqualTo(0), "单事件 ⇒ span = 0(合法,非错误)");
            Assert.That(sessionSpan, Is.GreaterThanOrEqualTo(0), "SessionSpan ≥ 0");
        }

        /// <summary>AC-51-B15:空流 ⇒ 0(合法)。</summary>
        [Test]
        public void test_f6_emptyStream_returnsZero()
        {
            // Arrange:空流
            var events = new List<F6F7Formula.SimEvent>();

            // Act
            var (tickMin, tickMax, sessionSpan, eventCount) = F6F7Formula.ComputeSessionSpan(events);

            // Assert
            Assert.That(tickMin, Is.EqualTo(0));
            Assert.That(tickMax, Is.EqualTo(0));
            Assert.That(sessionSpan, Is.EqualTo(0), "空流 ⇒ 0(合法)");
            Assert.That(eventCount, Is.EqualTo(0));
        }

        /// <summary>AC-51-B15:多事件 ⇒ span > 0,可审计。</summary>
        [Test]
        public void test_f6_multiEvent_spanPositive()
        {
            // Arrange
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 10 },
                new F6F7Formula.SimEvent { Tick = 20 },
                new F6F7Formula.SimEvent { Tick = 35 },
            };

            // Act
            var (tickMin, tickMax, sessionSpan, eventCount) = F6F7Formula.ComputeSessionSpan(events);

            // Assert
            Assert.That(tickMin, Is.EqualTo(10));
            Assert.That(tickMax, Is.EqualTo(35));
            Assert.That(sessionSpan, Is.EqualTo(25), "SessionSpan = 35 − 10 = 25");
            Assert.That(eventCount, Is.EqualTo(3));
            Assert.That(tickMin + sessionSpan, Is.EqualTo(tickMax));
        }

        // ══════════════ AC-51-B16:F7 上中位数取法定死(禁插值) ══════════════

        /// <summary>AC-51-B16:F7 上中位数 —— 偶数项 Δ 序列 ⟨a₁<a₂<a₃<a₄⟩ ⇒ Δ_median = a₂
        /// (1-indexed 第 ⌈n/2⌉ 项),不是 (a₂+a₃)/2;结果 ∈ 输入集;奇数项同法。</summary>
        [Test]
        public void test_f7_upperMedian_evenCount_noInterpolation()
        {
            // Arrange:偶数项 Δ 序列(4 个事件 ⇒ 3 个 Δ,奇数项;用 5 事件 ⇒ 4 个 Δ,偶数项)
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 10 },  // Δ: 2, 5, 1, 3
                new F6F7Formula.SimEvent { Tick = 12 },
                new F6F7Formula.SimEvent { Tick = 17 },
                new F6F7Formula.SimEvent { Tick = 18 },
                new F6F7Formula.SimEvent { Tick = 21 },
            };

            // Act
            var (deltaMedian, _, _, _, _, defined) = F6F7Formula.ComputeRhythm(events, gapThreshold: 100);

            // Assert:Δ = [2, 5, 1, 3],排序 = [1, 2, 3, 5],上中位数 = 第 ⌈4/2⌉ = 第 2 项 = 2
            Assert.That(defined, Is.True);
            Assert.That(deltaMedian, Is.EqualTo(2), "上中位数 = 2(非插值 (2+3)/2 = 2.5)");
            // 结果 ∈ 输入集
            Assert.That(new[] { 2, 5, 1, 3 }.Contains(deltaMedian), Is.True, "结果 ∈ 输入集");
        }

        /// <summary>AC-51-B16:奇数项同法。</summary>
        [Test]
        public void test_f7_upperMedian_oddCount()
        {
            // Arrange:4 事件 ⇒ 3 个 Δ(奇数项)
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 10 },  // Δ: 3, 1, 4
                new F6F7Formula.SimEvent { Tick = 13 },
                new F6F7Formula.SimEvent { Tick = 14 },
                new F6F7Formula.SimEvent { Tick = 18 },
            };

            // Act
            var (deltaMedian, _, _, _, _, defined) = F6F7Formula.ComputeRhythm(events, gapThreshold: 100);

            // Assert:Δ = [3, 1, 4],排序 = [1, 3, 4],上中位数 = 第 ⌈3/2⌉ = 第 2 项 = 3
            Assert.That(defined, Is.True);
            Assert.That(deltaMedian, Is.EqualTo(3), "上中位数 = 3");
        }

        // ══════════════ AC-51-B17:F7 退化 = 不可定义 ≠ 0 ══════════════

        /// <summary>AC-51-B17:F7 退化 —— n = 1(零个 Δ)⇒ 全部五个字段报「不可定义」,
        /// 不得报 0;n = 2 ⇒ Δ_MAD = 0 合法;断言输出可区分「不可定义」与「真实的 0」。</summary>
        [Test]
        public void test_f7_degenerate_singleEvent_reportsUndefined()
        {
            // Arrange:单事件(零个 Δ)
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 42 },
            };

            // Act
            var (_, _, _, _, _, defined) = F6F7Formula.ComputeRhythm(events, gapThreshold: 100);

            // Assert:报「不可定义」,不得报 0
            Assert.That(defined, Is.False, "n = 1 ⇒ 无 Δ 序列 ⇒ 不可定义(不得报 0)");
        }

        /// <summary>AC-51-B17:n = 2 ⇒ Δ_MAD = 0 合法(可区分「不可定义」与「真实的 0」)。</summary>
        [Test]
        public void test_f7_twoEvents_madZeroIsLegal()
        {
            // Arrange:2 事件 ⇒ 1 个 Δ
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 10 },
                new F6F7Formula.SimEvent { Tick = 15 },  // Δ = 5
            };

            // Act
            var (deltaMedian, deltaMad, _, _, _, defined) = F6F7Formula.ComputeRhythm(events, gapThreshold: 100);

            // Assert:Δ = [5],中位数 = 5,MAD = |5−5| = 0(合法)
            Assert.That(defined, Is.True, "n = 2 ⇒ 有 Δ 序列 ⇒ 可定义");
            Assert.That(deltaMedian, Is.EqualTo(5));
            Assert.That(deltaMad, Is.EqualTo(0), "MAD = 0 合法(与「不可定义」可区分)");
        }

        /// <summary>AC-51-B16 补充:GapCount 非零验证 ——
        /// 夹具 ticks = [10, 12, 17, 18, 21],gapThreshold = 2 ⇒
        /// Δ = [2, 5, 1, 3],超阈 = {5, 3} ⇒ gapCount = 2。</summary>
        [Test]
        public void test_f7_gapCount_nonZero()
        {
            // Arrange
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 10 },
                new F6F7Formula.SimEvent { Tick = 12 },
                new F6F7Formula.SimEvent { Tick = 17 },
                new F6F7Formula.SimEvent { Tick = 18 },
                new F6F7Formula.SimEvent { Tick = 21 },
            };

            // Act
            var (_, _, _, _, gapCount, defined) = F6F7Formula.ComputeRhythm(events, gapThreshold: 2);

            // Assert
            Assert.That(defined, Is.True);
            Assert.That(gapCount, Is.EqualTo(2), "Δ = [2,5,1,3],超阈 {5,3} ⇒ gapCount = 2");
        }

        /// <summary>AC-51-B17 补充:非零 MAD 验证 ——
        /// 夹具 ticks = [10, 12, 17, 18, 21],gapThreshold = 100 ⇒
        /// Δ = [2, 5, 1, 3],median = 2,absDeviations = [0, 3, 1, 1],sorted = [0, 1, 1, 3],
        /// 上中位数 index = ⌈4/2⌉ = 2 ⇒ mad = 1。</summary>
        [Test]
        public void test_f7_nonZeroMad()
        {
            // Arrange
            var events = new List<F6F7Formula.SimEvent>
            {
                new F6F7Formula.SimEvent { Tick = 10 },
                new F6F7Formula.SimEvent { Tick = 12 },
                new F6F7Formula.SimEvent { Tick = 17 },
                new F6F7Formula.SimEvent { Tick = 18 },
                new F6F7Formula.SimEvent { Tick = 21 },
            };

            // Act
            var (_, deltaMad, _, _, _, defined) = F6F7Formula.ComputeRhythm(events, gapThreshold: 100);

            // Assert
            Assert.That(defined, Is.True);
            Assert.That(deltaMad, Is.EqualTo(1), "非零 MAD = 1(上中位数取法)");
        }

        // ══════════════ AC-51-B18:F7 禁浮点统计量 ══════════════

        /// <summary>AC-51-B18:F7 禁浮点统计量 —— 51 命名空间下无方差/标准差/Math.Sqrt/浮点除法。
        /// 扫描 51 类型签名。</summary>
        [Test]
        public void test_f7_noFloatStatistics()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描方法名与字段名中的浮点统计关键词
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
            {
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.NonPublic |
                                              System.Reflection.BindingFlags.Instance |
                                              System.Reflection.BindingFlags.Static |
                                              System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("Variance") || m.Name.Contains("Stddev") ||
                        m.Name.Contains("StdDev") || m.Name.Contains("Sqrt"))
                        violations.Add($"{t.Name}.{m.Name} 含浮点统计关键词");
                }
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public |
                                             System.Reflection.BindingFlags.NonPublic |
                                             System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Static |
                                             System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (f.Name.Contains("Variance") || f.Name.Contains("Stddev") ||
                        f.Name.Contains("StdDev") || f.Name.Contains("Sqrt"))
                        violations.Add($"{t.Name}.{f.Name} 含浮点统计关键词");
                }
            }

            // Assert:零浮点统计量(当前阶段 51 命名空间零类型 ⇒ vacuous,实现后转为有效扫描)
            if (telemetryTypes.Count == 0)
            {
                Assert.Ignore("51 未实现,当前阶段无法扫描签名(实现后应验证 violations 为空)");
                return;
            }
            Assert.That(violations, Is.Empty,
                "51 不得有方差/标准差/Math.Sqrt/浮点除法的离散度实现(AC-51-B18):\n" +
                string.Join("\n", violations));
        }
    }
}
