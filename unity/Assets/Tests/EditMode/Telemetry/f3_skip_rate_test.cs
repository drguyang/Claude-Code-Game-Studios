// 权威来源:production/epics/telemetry-analytics/story-004-f3-skip-rate.md
//   (AC-51-B8…B9 — F3 跳过率:Skip/Opportunity/SkipByChoice + NotOffered 不入分母)
//   · F3 = Skip / Opportunity;SkipByChoice 与降级强制分开报
//   · 分母 = 可跳过机会数(提供小游戏的实例数);NotOffered 不入分母
//   · Opportunity = 0 ⇒ 不可定义(报 (0,0));不抛除零
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/f3_skip_rate_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001-003 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F3SkipRateTest
    {
        // ══════════════ F3 公式(测试自持谓词面,零生产依赖)══════════════

        /// <summary>F3 公式谓词面(测试自持,纯函数)。
        /// 语义 = GDD Formulas F3:SkipRate = Skip / Opportunity;SkipByChoice 分开报。</summary>
        private static class F3Formula
        {
            /// <summary>一次动作实例(测试自持夹具)。</summary>
            public sealed class ActionInstance
            {
                public int InstanceId;
                public string Resolution;   // Played / Skipped / NotOffered
                public string Cause;        // 玩家选择 / 降级 / null
            }

            /// <summary>计算 F3 指标。
            /// 返回 (skipNum, skipDen, skipByChoiceNum, skipByChoiceDen, denominatorZero)。
            /// Opportunity = 提供小游戏的实例数(不含 NotOffered);分母 0 ⇒ 不可定义。</summary>
            public static (int skipNum, int skipDen, int choiceNum, int choiceDen, bool denominatorZero)
                Compute(IReadOnlyList<ActionInstance> instances)
            {
                int opportunity = instances.Count(i => i.Resolution != "NotOffered");
                if (opportunity == 0)
                    return (0, 0, 0, 0, true);   // 分母 0 ⇒ 不可定义

                int skip = instances.Count(i => i.Resolution == "Skipped");
                int choice = instances.Count(i => i.Resolution == "Skipped" && i.Cause == "玩家选择");
                return (skip, opportunity, choice, opportunity, false);
            }
        }

        // ══════════════ AC-51-B8:F3 分开报 + 分母口径 ══════════════

        /// <summary>AC-51-B8:F3 分开报 + 分母口径 ——
        /// Skipped ∧ cause=玩家选择 与 Skipped ∧ cause=降级 各 ≥ 1 例,另含 NotOffered 实例。
        /// 输出两个独立分子(Skip 与 SkipByChoice);NotOffered 不入分母。</summary>
        [Test]
        public void test_f3_separateNumerators_notOfferedExcluded()
        {
            // Arrange:含两类跳过原因 + NotOffered
            var instances = new List<F3Formula.ActionInstance>
            {
                new F3Formula.ActionInstance { InstanceId = 1, Resolution = "Skipped", Cause = "玩家选择" },
                new F3Formula.ActionInstance { InstanceId = 2, Resolution = "Skipped", Cause = "降级" },
                new F3Formula.ActionInstance { InstanceId = 3, Resolution = "Played", Cause = null },
                new F3Formula.ActionInstance { InstanceId = 4, Resolution = "NotOffered", Cause = null },
            };

            // Act
            var (skipNum, skipDen, choiceNum, choiceDen, denominatorZero) = F3Formula.Compute(instances);

            // Assert:两个独立分子(Skip = 2, SkipByChoice = 1)
            Assert.That(skipNum, Is.EqualTo(2), "Skip 分子 = 2(两类跳过)");
            Assert.That(skipDen, Is.EqualTo(3), "Opportunity 分母 = 3(不含 NotOffered)");
            Assert.That(choiceNum, Is.EqualTo(1), "SkipByChoice 分子 = 1(仅玩家选择)");
            Assert.That(choiceDen, Is.EqualTo(3), "SkipByChoice 分母 = 3");
            Assert.That(denominatorZero, Is.False, "分母 > 0 时不报分母 0");
            // AC-51-B8 负向:输出形状 = 两个独立分子,不存在单一 SkipRate 字段
            Assert.That(skipNum, Is.Not.EqualTo(choiceNum),
                "两个分子值不同 ⇒ 输出形状必然是两个独立字段,非合并单一 SkipRate");
        }

        /// <summary>AC-51-B8 负向:NotOffered 实例不得计入 Opportunity 分母。</summary>
        [Test]
        public void test_f3_notOfferedNotInDenominator()
        {
            // Arrange:仅 NotOffered + 一个 Played
            var instances = new List<F3Formula.ActionInstance>
            {
                new F3Formula.ActionInstance { InstanceId = 1, Resolution = "Played", Cause = null },
                new F3Formula.ActionInstance { InstanceId = 2, Resolution = "NotOffered", Cause = null },
            };

            // Act
            var (skipNum, skipDen, _, _, _) = F3Formula.Compute(instances);

            // Assert:NotOffered 不入分母
            Assert.That(skipDen, Is.EqualTo(1), "Opportunity = 1(仅 Played;NotOffered 不入)");
            Assert.That(skipNum, Is.EqualTo(0), "Skip = 0");
        }

        // ══════════════ AC-51-B4(F3 侧):分母 0 报 (0,0) ══════════════

        /// <summary>AC-51-B4(F3 侧):Opportunity = 0 ⇒ 输出 (0,0) + 显式「分母 0/未定义」标记。
        /// 不抛除零、不报 0/1、不报标量 0、不报 NaN。</summary>
        [Test]
        public void test_f3_denominatorZero_reports00()
        {
            // Arrange:空实例集
            var instances = new List<F3Formula.ActionInstance>();

            // Act
            var (skipNum, skipDen, choiceNum, choiceDen, denominatorZero) = F3Formula.Compute(instances);

            // Assert
            Assert.That(denominatorZero, Is.True, "Opportunity = 0 必须显式标记");
            Assert.That(skipNum, Is.EqualTo(0), "Skip 分子 = 0");
            Assert.That(skipDen, Is.EqualTo(0), "Opportunity 分母 = 0(非 1)");
            Assert.That(choiceNum, Is.EqualTo(0), "SkipByChoice 分子 = 0");
            Assert.That(choiceDen, Is.EqualTo(0), "SkipByChoice 分母 = 0(非 1)");
            // 不报标量 0(整数对,非浮点)
            Assert.That(skipNum, Is.EqualTo(0).And.TypeOf<int>(), "分子为整数 0,非浮点 0");
            Assert.That(skipDen, Is.EqualTo(0).And.TypeOf<int>(), "分母为整数 0,非浮点 0");
            // 不报 NaN(整数对不可能产生 NaN,显式断言以记录意图)
            Assert.That(double.IsNaN(skipNum) || double.IsNaN(skipDen), Is.False, "不报 NaN");
        }

        /// <summary>AC-51-B4(F3 侧)第二夹具:全 NotOffered 实例集同样触发分母 0。</summary>
        [Test]
        public void test_f3_allNotOffered_denominatorZero()
        {
            // Arrange:全 NotOffered
            var instances = new List<F3Formula.ActionInstance>
            {
                new F3Formula.ActionInstance { InstanceId = 1, Resolution = "NotOffered", Cause = null },
                new F3Formula.ActionInstance { InstanceId = 2, Resolution = "NotOffered", Cause = null },
            };

            // Act
            var (skipNum, skipDen, choiceNum, choiceDen, denominatorZero) = F3Formula.Compute(instances);

            // Assert:全 NotOffered ⇒ Opportunity = 0 ⇒ 分母 0
            Assert.That(denominatorZero, Is.True, "全 NotOffered ⇒ Opportunity = 0 ⇒ 分母 0");
            Assert.That(skipDen, Is.EqualTo(0), "分母 = 0");
            Assert.That(choiceDen, Is.EqualTo(0), "分母 = 0");
        }

        // ══════════════ AC-51-B9:F3 真实会话可得性(BLOCKED-BY-OQ-10-8)══════════════

        /// <summary>AC-51-B9(ADVISORY → BLOCKED-BY-`OQ-10-8`):F3 真实会话可得性 ——
        /// 集成测试目录 `tests/integration/51/` 未建 ⇒ 记 `NOT-RUN`,不得记绿(禁借绿)。
        /// `OQ-10-8` 未结前不代为判绿。</summary>
        [Test]
        public void test_f3_realSession_blockedByOQ108()
        {
            // ⚠️ 当前阶段 `tests/integration/51/` 未建 ⇒ 真实会话不可得
            // Act + Assert:标记为 NOT-RUN(禁借绿)
            Assert.Ignore("AC-51-B9:BLOCKED-BY-OQ-10-8 — 集成测试目录 tests/integration/51/ 未建,真实会话不可得,不得记绿");
        }
    }
}
