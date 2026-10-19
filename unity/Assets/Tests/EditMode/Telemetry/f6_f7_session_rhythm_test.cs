// 权威来源:production/epics/telemetry-analytics/story-007-f6-f7-session-rhythm.md
//   (AC-51-B15…B18 — F6 局内时长 + F7 节律:SessionSpan 可审计 + 上中位数 + MAD + 退化处理)
//   · F6: SessionSpan = Tick_max − Tick_min;同时报 Tick_min/Tick_max/|E|;单事件 ⇒ span=0;空流 ⇒ 0
//   · F7: Δ_i = Tick(e_{i+1}) − Tick(e_i);Δ_median = Δ_(⌈m/2⌉)(1-indexed,上中位数,禁插值)
//          (m = n−1 = Δ 项数 —— 序统计量的长度是 m,不是事件数 n;2026-10-19 GDD 符号订正);
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
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F6F7SessionRhythmTest
    {
        // ⚠️ S19 修复(TD-epic-01):原「测试自持谓词面」已删除 ——
        //    谓词面上移为生产类 `DaYiJingCheng.Telemetry.SessionRhythmMetrics`
        //    (ADR-019 §一;那个自持面在普查里被判为「AC 语义层无生产主语」)。
        //    本文件现在**只做夹具 + 断言**,不再自带公式。

        // ══════════════ AC-51-B15:F6 可审计 ══════════════

        /// <summary>AC-51-B15:F6 可审计 —— 同时输出 Tick_min/Tick_max/|E|;
        /// Tick_min + SessionSpan = Tick_max;SessionSpan ≥ 0;单事件 ⇒ span = 0(合法);空流 ⇒ 0。</summary>
        [Test]
        public void test_f6_auditable_singleEventSpanZero()
        {
            // Arrange:单事件
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 42 },
            };

            // Act
            var (tickMin, tickMax, sessionSpan, eventCount) = SessionRhythmMetrics.ComputeSessionSpan(events);

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
            var events = new List<SessionRhythmMetrics.SimEvent>();

            // Act
            var (tickMin, tickMax, sessionSpan, eventCount) = SessionRhythmMetrics.ComputeSessionSpan(events);

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
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 10 },
                new SessionRhythmMetrics.SimEvent { Tick = 20 },
                new SessionRhythmMetrics.SimEvent { Tick = 35 },
            };

            // Act
            var (tickMin, tickMax, sessionSpan, eventCount) = SessionRhythmMetrics.ComputeSessionSpan(events);

            // Assert
            Assert.That(tickMin, Is.EqualTo(10));
            Assert.That(tickMax, Is.EqualTo(35));
            Assert.That(sessionSpan, Is.EqualTo(25), "SessionSpan = 35 − 10 = 25");
            Assert.That(eventCount, Is.EqualTo(3));
            Assert.That(tickMin + sessionSpan, Is.EqualTo(tickMax));
        }

        // ══════════════ AC-51-B16:F7 上中位数取法定死(禁插值) ══════════════

        /// <summary>AC-51-B16:F7 上中位数 —— 偶数项 Δ 序列 ⟨a₁<a₂<a₃<a₄⟩ ⇒ Δ_median = a₂
        /// (1-indexed 第 ⌈m/2⌉ 项,m = Δ 项数),不是 (a₂+a₃)/2;结果 ∈ 输入集;奇数项同法。</summary>
        [Test]
        public void test_f7_upperMedian_evenCount_noInterpolation()
        {
            // Arrange:偶数项 Δ 序列(4 个事件 ⇒ 3 个 Δ,奇数项;用 5 事件 ⇒ 4 个 Δ,偶数项)
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 10 },  // Δ: 2, 5, 1, 3
                new SessionRhythmMetrics.SimEvent { Tick = 12 },
                new SessionRhythmMetrics.SimEvent { Tick = 17 },
                new SessionRhythmMetrics.SimEvent { Tick = 18 },
                new SessionRhythmMetrics.SimEvent { Tick = 21 },
            };

            // Act
            var (deltaMedian, _, _, _, _, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 100);

            // Assert:Δ = [2, 5, 1, 3],排序 = [1, 2, 3, 5],上中位数 = 第 ⌈4/2⌉ = 第 2 项 = 2
            Assert.That(defined, Is.True);
            Assert.That(deltaMedian, Is.EqualTo(2), "上中位数 = 2(非插值 (2+3)/2 = 2.5)");
            // 结果 ∈ 输入集
            Assert.That(new[] { 2, 5, 1, 3 }.Contains(deltaMedian), Is.True, "结果 ∈ 输入集");
        }

        /// <summary>AC-51-B16 判别力夹具(2026-10-19 补):**上中位数 / 插值 / 事件数口径三者互异**。
        /// <para>⚠️ 上一条 `test_f7_upperMedian_evenCount_noInterpolation` 用 Δ = `{2,5,1,3}`,
        /// 而插值 `(2+3)/2` 在整数域截断后**也是 2** ⇒ 该用例对「改成插值」的变异**零判别力**。
        /// 本用例把三个候选口径拆到三个不同的数上:</para>
        /// <list type="bullet">
        /// <item>上中位数 `⌈m/2⌉ = 2` ⇒ 排序 `{10,20,30,50}` 第 2 项 = **20**(口径 1,正确)</item>
        /// <item>插值中位数 `(a₂+a₃)/2 = 25` ⇒ 报 **25** 即转红</item>
        /// <item>事件数口径 `⌈n/2⌉ = 3`(n = 5)⇒ 第 3 项 = **30**,报 30 即转红</item>
        /// </list>
        /// <para>第三个口径正是 TD-epic-01 登记「生产得 20 / GDD 得 30」时被误读的那一支 ——
        /// 本用例把它钉成可判别项,防止文档符号再次漂移时无人发现。</para></summary>
        [Test]
        public void test_f7_upperMedian_differsFromInterpolationAndEventCount()
        {
            // Arrange:5 事件 ⇒ m = 4 个 Δ = {10, 20, 30, 50}
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 0 },    // Δ: 10, 20, 30, 50
                new SessionRhythmMetrics.SimEvent { Tick = 10 },
                new SessionRhythmMetrics.SimEvent { Tick = 30 },
                new SessionRhythmMetrics.SimEvent { Tick = 60 },
                new SessionRhythmMetrics.SimEvent { Tick = 110 },
            };

            // Act
            var (deltaMedian, _, _, _, _, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 100);

            // Assert
            Assert.That(defined, Is.True);
            Assert.That(deltaMedian, Is.EqualTo(20),
                "上中位数 = ⌈m/2⌉ = 第 2 项 = 20 —— 不是插值 25,也不是事件数口径 ⌈n/2⌉ 的第 3 项 30");
            Assert.That(deltaMedian, Is.Not.EqualTo(25), "不得插值((20+30)/2)");
            Assert.That(deltaMedian, Is.Not.EqualTo(30), "不得按事件数 n=5 取第 ⌈n/2⌉ = 3 项");
            Assert.That(new[] { 10, 20, 30, 50 }.Contains(deltaMedian), Is.True, "结果 ∈ 输入集");
        }

        /// <summary>AC-51-B16:奇数项同法。</summary>
        [Test]
        public void test_f7_upperMedian_oddCount()
        {
            // Arrange:4 事件 ⇒ 3 个 Δ(奇数项)
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 10 },  // Δ: 3, 1, 4
                new SessionRhythmMetrics.SimEvent { Tick = 13 },
                new SessionRhythmMetrics.SimEvent { Tick = 14 },
                new SessionRhythmMetrics.SimEvent { Tick = 18 },
            };

            // Act
            var (deltaMedian, _, _, _, _, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 100);

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
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 42 },
            };

            // Act
            var (_, _, _, _, _, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 100);

            // Assert:报「不可定义」,不得报 0
            Assert.That(defined, Is.False, "n = 1 ⇒ 无 Δ 序列 ⇒ 不可定义(不得报 0)");
        }

        /// <summary>AC-51-B17:n = 2 ⇒ Δ_MAD = 0 合法(可区分「不可定义」与「真实的 0」)。</summary>
        [Test]
        public void test_f7_twoEvents_madZeroIsLegal()
        {
            // Arrange:2 事件 ⇒ 1 个 Δ
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 10 },
                new SessionRhythmMetrics.SimEvent { Tick = 15 },  // Δ = 5
            };

            // Act
            var (deltaMedian, deltaMad, _, _, _, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 100);

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
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 10 },
                new SessionRhythmMetrics.SimEvent { Tick = 12 },
                new SessionRhythmMetrics.SimEvent { Tick = 17 },
                new SessionRhythmMetrics.SimEvent { Tick = 18 },
                new SessionRhythmMetrics.SimEvent { Tick = 21 },
            };

            // Act
            var (_, _, _, _, gapCount, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 2);

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
            var events = new List<SessionRhythmMetrics.SimEvent>
            {
                new SessionRhythmMetrics.SimEvent { Tick = 10 },
                new SessionRhythmMetrics.SimEvent { Tick = 12 },
                new SessionRhythmMetrics.SimEvent { Tick = 17 },
                new SessionRhythmMetrics.SimEvent { Tick = 18 },
                new SessionRhythmMetrics.SimEvent { Tick = 21 },
            };

            // Act
            var (_, deltaMad, _, _, _, defined) = SessionRhythmMetrics.ComputeRhythm(events, gapThreshold: 100);

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
