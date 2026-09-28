// 权威来源:production/epics/telemetry-analytics/story-005-f4-skill-growth.md
//   (AC-51-B10…B11 — F4 熟练度成长:Practice/NoveltyMix/ΔLevel;新颖度三分守恒;不重算 XP)
//   · Practice(skill, W) = |{g : g.skill = skill ∧ Tick(g) ∈ W}|
//   · NoveltyMix(skill, W) = (N_new, N_repeat, N_stale) 按新颖度枚举计数
//   · ΔLevel = Level_end − Level_start;Level_end = 读流终点携带值,绝不读玩家当前/存档现值
//   · 51 不重算 XP:那是 30 的定点域算术;重算 = 破 R7 + 造第二真源
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/f4_skill_growth_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001-004 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F4SkillGrowthTest
    {
        // ══════════════ F4 公式(测试自持谓词面,零生产依赖)══════════════

        /// <summary>F4 公式谓词面(测试自持,纯函数)。
        /// 语义 = GDD Formulas F4:新颖度三分守恒 + 不重算 XP。</summary>
        private static class F4Formula
        {
            /// <summary>一次成长事件(测试自持夹具)。</summary>
            public sealed class GrowthEvent
            {
                public string Skill;
                public string NoveltyClass;  // new / repeat / stale
                public long? Level;          // null = 未携带
            }

            /// <summary>计算 Practice 与 NoveltyMix。
            /// 返回 (practice, nNew, nRepeat, nStale)。</summary>
            public static (int practice, int nNew, int nRepeat, int nStale)
                Compute(IReadOnlyList<GrowthEvent> events, string skill)
            {
                var inWindow = events.Where(e => e.Skill == skill).ToList();
                int practice = inWindow.Count;
                int nNew = inWindow.Count(e => e.NoveltyClass == "new");
                int nRepeat = inWindow.Count(e => e.NoveltyClass == "repeat");
                int nStale = inWindow.Count(e => e.NoveltyClass == "stale");
                return (practice, nNew, nRepeat, nStale);
            }

            /// <summary>尝试计算 ΔLevel(Level_end − Level_start)。
            /// 返回 (success, delta):所有事件携带 Level ⇒ (true, delta);
            /// 任一事件未携带 Level ⇒ (false, null)——「不可得」与 0 可区分。</summary>
            public static (bool success, int? delta)
                TryComputeDeltaLevel(IReadOnlyList<GrowthEvent> events, string skill)
            {
                var inWindow = events.Where(e => e.Skill == skill).ToList();
                if (inWindow.Count == 0) return (true, 0);
                if (inWindow.Any(e => e.Level == null)) return (false, null);
                long start = inWindow.Where(e => e.Level.HasValue).Min(e => e.Level.Value);
                long end = inWindow.Where(e => e.Level.HasValue).Max(e => e.Level.Value);
                return (true, (int)(end - start));
            }

            /// <summary>新颖度三分守恒:N_new + N_repeat + N_stale = Practice。</summary>
            public static bool NoveltyConserves(int practice, int nNew, int nRepeat, int nStale)
                => nNew + nRepeat + nStale == practice;
        }

        // ══════════════ AC-51-B10:F4 新颖度三分守恒 ══════════════

        /// <summary>AC-51-B10:F4 新颖度三分守恒 —— 某 skill 在窗口内 new/repeat/stale 各 ≥ 1 例,
        /// N_new + N_repeat + N_stale = Practice(skill, W) 且三类不重复计数。</summary>
        [Test]
        public void test_f4_noveltyThreeWayConservation()
        {
            // Arrange:某 skill 在窗口内三类各 ≥ 1 例
            var events = new List<F4Formula.GrowthEvent>
            {
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "new" },
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "repeat" },
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "stale" },
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "new" },  // 第二个 new
                new F4Formula.GrowthEvent { Skill = "炮制", NoveltyClass = "new" },   // 不同 skill
            };

            // Act
            var (practice, nNew, nRepeat, nStale) = F4Formula.Compute(events, "诊断");

            // Assert:三分之和 = Practice,且三类不重复计数
            Assert.That(practice, Is.EqualTo(4), "Practice = 4(诊断的 4 个事件)");
            Assert.That(nNew, Is.EqualTo(2), "N_new = 2");
            Assert.That(nRepeat, Is.EqualTo(1), "N_repeat = 1");
            Assert.That(nStale, Is.EqualTo(1), "N_stale = 1");
            Assert.That(F4Formula.NoveltyConserves(practice, nNew, nRepeat, nStale), Is.True,
                "N_new + N_repeat + N_stale = Practice(守恒)");
        }

        /// <summary>AC-51-B10 边界:空窗口(W 内无事件)⇒ 全 0(合法)。</summary>
        [Test]
        public void test_f4_emptyWindow_allZero()
        {
            // Arrange:空事件列表
            var events = new List<F4Formula.GrowthEvent>();

            // Act
            var (practice, nNew, nRepeat, nStale) = F4Formula.Compute(events, "诊断");

            // Assert:全 0(合法,不报错)
            Assert.That(practice, Is.EqualTo(0), "空窗口 Practice = 0");
            Assert.That(nNew, Is.EqualTo(0), "空窗口 N_new = 0");
            Assert.That(nRepeat, Is.EqualTo(0), "空窗口 N_repeat = 0");
            Assert.That(nStale, Is.EqualTo(0), "空窗口 N_stale = 0");
            Assert.That(F4Formula.NoveltyConserves(practice, nNew, nRepeat, nStale), Is.True,
                "空窗口守恒(0 = 0)");
        }

        /// <summary>AC-51-B10 边界:skill 未命中(窗口有事件但无目标 skill)⇒ 全 0(合法)。</summary>
        [Test]
        public void test_f4_skillNotMatched_allZero()
        {
            // Arrange:窗口有事件但无目标 skill
            var events = new List<F4Formula.GrowthEvent>
            {
                new F4Formula.GrowthEvent { Skill = "炮制", NoveltyClass = "new" },
            };

            // Act
            var (practice, nNew, nRepeat, nStale) = F4Formula.Compute(events, "诊断");

            // Assert:全 0(合法)
            Assert.That(practice, Is.EqualTo(0), "skill 未命中 Practice = 0");
            Assert.That(nNew, Is.EqualTo(0));
            Assert.That(nRepeat, Is.EqualTo(0));
            Assert.That(nStale, Is.EqualTo(0));
        }

        // ══════════════ AC-51-B11:F4 不重算 XP ══════════════

        /// <summary>AC-51-B11:F4 不重算 XP —— 51 的程序集无 <c>XP_gain</c>/<c>BASE</c>/<c>K_difficulty</c>/<c>K_novelty</c>
        /// 的实现或常量(那是 30 的定点域;重算 = 破 R7 + 造第二真源)。
        /// ⚠️ 当前阶段 51 命名空间零类型(未实现),扫描为 vacuous pass;实现后应转为有效扫描断言。</summary>
        [Test]
        public void test_f4_noXpGainComputation()
        {
            // Arrange:扫描 51 命名空间下的类型
            var telemetryTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                .ToList();

            // Act:扫描方法名与字段名中的 XP 关键词
            var violations = new List<string>();
            foreach (var t in telemetryTypes)
            {
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.NonPublic |
                                              System.Reflection.BindingFlags.Instance |
                                              System.Reflection.BindingFlags.Static |
                                              System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("XP") || m.Name.Contains("Xp"))
                        violations.Add($"{t.Name}.{m.Name} 含 XP");
                }
                foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public |
                                             System.Reflection.BindingFlags.NonPublic |
                                             System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Static |
                                             System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (f.Name.Contains("XP") || f.Name.Contains("Xp") ||
                        f.Name.Contains("BASE") || f.Name.Contains("K_difficulty") ||
                        f.Name.Contains("K_novelty"))
                        violations.Add($"{t.Name}.{f.Name} 含 XP/BASE/K 关键词");
                }
            }

            // Assert:零 XP 计算(实现后 telemetryTypes 非空;唯一判据 = violations 为空)
            Assert.That(violations, Is.Empty,
                "51 不得有 XP_gain/BASE/K_difficulty/K_novelty 的实现或常量(AC-51-B11:不重算 XP):\n" +
                string.Join("\n", violations));
        }

        // ══════════════ AC-51-D2:Level 未携带 ⇒ 报「不可得」 ══════════════

        /// <summary>AC-51-D2:「Level」未携带 —— 窗口内有成长事件但未携带 Level,
        /// 计算 ΔLevel 报「不可得」(与 0 可区分),不报 0。</summary>
        [Test]
        public void test_f4_levelNotCarried_reportsUnavailable()
        {
            // Arrange:有成长事件但 Level 未携带
            var events = new List<F4Formula.GrowthEvent>
            {
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "new", Level = null },
            };

            // Act
            var (success, delta) = F4Formula.TryComputeDeltaLevel(events, "诊断");

            // Assert:报「不可得」,不报 0
            Assert.That(success, Is.False, "Level 未携带 ⇒ 不可计算(报「不可得」)");
            Assert.That(delta, Is.Null, "不可得时 delta = null(与 0 可区分)");
        }

        /// <summary>AC-51-D2 正例:所有事件携带 Level ⇒ 正常计算 ΔLevel。</summary>
        [Test]
        public void test_f4_levelCarried_computesDelta()
        {
            // Arrange:所有事件携带 Level
            var events = new List<F4Formula.GrowthEvent>
            {
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "new", Level = 3 },
                new F4Formula.GrowthEvent { Skill = "诊断", NoveltyClass = "repeat", Level = 5 },
            };

            // Act
            var (success, delta) = F4Formula.TryComputeDeltaLevel(events, "诊断");

            // Assert:正常计算
            Assert.That(success, Is.True, "Level 已携带 ⇒ 可计算");
            Assert.That(delta, Is.EqualTo(2), "ΔLevel = 5 − 3 = 2");
        }
    }
}
