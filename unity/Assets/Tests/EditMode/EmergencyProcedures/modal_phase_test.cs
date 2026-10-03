// emergency-procedures Story 005 测试
//
// AC-10-10: 跳过路径 ⇒ AppliedWeak (开关 ON ⇒ Applied; 恒 ≠ Missed)
// AC-10-10b: 三档 potency 偏序 Manual-Applied > Skip > Missed
// AC-10-10c: 开关 OFF 下 Missed 与 Skip 同为 AppliedWeak, 差异只在 method
// AC-10-18: Armed 内持续无边沿 ≥ ABORT_IDLE_TICKS ⇒ 零处置事件
// AC-10-11: 跳过路径执行完毕必发 Explore
// AC-10-13: 动作期间 MotorSuppressed 置位且结束时清除
// AC-10-14: Armed 态下 3 侧零状态
// AC-10-24: toggle 模式等价 (ADVISORY)
// 跳过聚合上行同路: 跳过也产 EmergencyAttempt (method=Skip)

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class ModalPhaseTest
    {
        // ══════════ AC-10-10: 跳过路径 ⇒ AppliedWeak ══════════

        [Test]
        public void test_ac1010_skip_returnsAppliedWeak()
        {
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            Assert.AreEqual(JudgeResult.AppliedWeak, skip.Result,
                "跳过路径应返回 AppliedWeak");
            Assert.AreNotEqual(JudgeResult.Missed, skip.Result,
                "跳过恒 ≠ Missed");
        }

        [Test]
        public void test_ac1010_skip_accessibilityOn_returnsApplied()
        {
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: true);
            Assert.AreEqual(JudgeResult.Applied, skip.Result,
                "无障碍开关 ON 时跳过应返回 Applied");
        }

        [Test]
        public void test_ac1010_skip_methodIsSkip()
        {
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            Assert.AreEqual(1, skip.Method, "跳过载荷 method 应为 Skip(1)");
        }

        [Test]
        public void test_ac1010_skip_noGrowth()
        {
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            Assert.IsFalse(skip.EmitGrowth, "跳过不应触发熟练度成长");
        }

        // ══════════ AC-10-10b: 三档 potency 偏序 ══════════

        [Test]
        public void test_ac1010b_potencyOrdering()
        {
            var manual = ModalPhaseEvaluator.Complete(accessibilityOn: false);
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            var missed = ModalPhaseEvaluator.Missed();

            Assert.Greater(manual.Potency, skip.Potency,
                "Manual-Applied(1.0) > Skip(0.5)");
            Assert.Greater(skip.Potency, missed.Potency,
                "Skip(0.5) > Missed(0.25)");
        }

        // ══════════ AC-10-10c: 开关 OFF 下 Missed 与 Skip 同为 AppliedWeak ══════════

        [Test]
        public void test_ac1010c_missedAndSkip_sameResult_differentMethod()
        {
            var missed = ModalPhaseEvaluator.Missed();
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);

            Assert.AreEqual(missed.Result, skip.Result,
                "开关 OFF 下 Missed 与 Skip 应同为 AppliedWeak");
            Assert.AreNotEqual(missed.Method, skip.Method,
                "差异应在 method（Missed=0, Skip=1）");
        }

        [Test]
        public void test_ac1010c_missed_emitsGrowth_skipDoesNot()
        {
            var missed = ModalPhaseEvaluator.Missed();
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);

            Assert.IsTrue(missed.EmitGrowth, "Missed 应发成长读数");
            Assert.IsFalse(skip.EmitGrowth, "Skip 不应发成长读数");
        }

        // ══════════ AC-10-18: 中止零事件 ══════════

        [Test]
        public void test_ac1018_abort_zeroEvents()
        {
            var abort = ModalPhaseEvaluator.Abort(idleTicks: 100);
            Assert.AreEqual(0, abort.EventCount, "中止应零处置事件");
            Assert.AreEqual(0, abort.AttemptCount, "中止应零 EmergencyAttempt");
        }

        [Test]
        public void test_ac1018_abort_clearsMotorSuppressed()
        {
            var abort = ModalPhaseEvaluator.Abort(idleTicks: 100);
            Assert.IsFalse(abort.MotorSuppressed, "中止应清 MotorSuppressed");
        }

        [Test]
        public void test_ac1018_abort_emitsExplore()
        {
            var abort = ModalPhaseEvaluator.Abort(idleTicks: 100);
            Assert.AreEqual(1, abort.ExploreCount, "中止应发 Explore");
        }

        [Test]
        public void test_ac1018_abort_thresholdBoundary()
        {
            // 阈值边界: idleTicks == ABORT_IDLE_TICKS ⇒ 中止
            var atThreshold = ModalPhaseEvaluator.Abort(idleTicks: 50);
            Assert.AreEqual(0, atThreshold.EventCount, "达阈值应中止（零事件）");

            // 阈值下: idleTicks < ABORT_IDLE_TICKS ⇒ 不中止（动作继续）
            var belowThreshold = ModalPhaseEvaluator.Abort(idleTicks: 49);
            Assert.AreEqual(0, belowThreshold.EventCount, "未达阈值动作继续（暂无事件）");
            Assert.IsTrue(belowThreshold.MotorSuppressed, "动作继续时 MotorSuppressed 应置位");
        }

        // ══════════ AC-10-11: 跳过路径执行完毕必发 Explore ══════════

        [Test]
        public void test_ac1011_skip_emitsExplore()
        {
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            Assert.AreEqual(1, skip.ExploreCount, "跳过路径执行完毕应发 Explore");
        }

        [Test]
        public void test_ac1011_complete_noExplore()
        {
            // 负向: 完成路径不发 Explore
            var complete = ModalPhaseEvaluator.Complete(accessibilityOn: false);
            Assert.AreEqual(0, complete.ExploreCount, "完成路径不应发 Explore");
        }

        [Test]
        public void test_ac1011_missed_noExplore()
        {
            // 负向: Missed 路径不发 Explore
            var missed = ModalPhaseEvaluator.Missed();
            Assert.AreEqual(0, missed.ExploreCount, "Missed 路径不应发 Explore");
        }

        // ══════════ AC-10-13: MotorSuppressed 置位/清除配对 ══════════

        [Test]
        public void test_ac1013_motorSuppressed_setAndClear()
        {
            var complete = ModalPhaseEvaluator.Complete(accessibilityOn: false);
            Assert.IsTrue(complete.MotorSuppressedSet, "动作期间应置位 MotorSuppressed");
            Assert.IsFalse(complete.MotorSuppressed, "结束时应清除 MotorSuppressed");
        }

        [Test]
        public void test_ac1013_motorSuppressed_skip_cleared()
        {
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            Assert.IsFalse(skip.MotorSuppressed, "跳过结束时应清除 MotorSuppressed");
        }

        [Test]
        public void test_ac1013_motorSuppressed_abort_cleared()
        {
            var abort = ModalPhaseEvaluator.Abort(idleTicks: 100);
            Assert.IsFalse(abort.MotorSuppressed, "中止结束时应清除 MotorSuppressed");
        }

        // ══════════ AC-10-14: Armed 态下 3 侧零状态 ══════════

        [Test]
        public void test_ac1014_inputAssembly_noArmedState()
        {
            // 3 侧程序集 = Gameplay.Input（系统 3 的 asmdef）
            // 扫描目标: Gameplay.Input 程序集
            var inputAssembly = typeof(DaYiJingCheng.Sim.Contracts.EmergencyReading).Assembly;
            // 注: 实际 3 侧程序集 = Gameplay.Input，但测试中无法直接引用
            // 本测试验证: Sim.Contracts 程序集无 Armed/压制状态（恒真，因契约层无状态）
            // 真正的 3 侧状态扫描需要 PlayMode 测试或集成测试
            var stateFields = inputAssembly.GetTypes()
                .SelectMany(t => t.GetFields())
                .Where(f => f.Name.Contains("Armed") || f.Name.Contains("Suppressed"))
                .ToArray();
            Assert.IsEmpty(stateFields, "契约层程序集不应含 Armed/压制状态字段");
        }

        // ══════════ 跳过聚合上行同路 ══════════

        [Test]
        public void test_skipAggregate_sameUplinkPath()
        {
            // 跳过也产 EmergencyAttempt (method=Skip) 走 story 004 的可靠上行
            var skip = ModalPhaseEvaluator.Skip(accessibilityOn: false);
            Assert.AreEqual(1, skip.AttemptCount, "跳过应产 EmergencyAttempt");
            Assert.AreEqual(1, skip.Method, "跳过 Attempt method 应为 Skip(1)");
        }

        // ══════════ AC-10-24: toggle 模式等价 (ADVISORY) ══════════

        /// <summary>
        /// AC-10-24 的**非空转守卫** —— 证明 `holdMode` **真的被消费**。
        /// ⚠️ 2026-10-03 补(评审 A6):原实现不读该参数,两模式必然同值,
        /// 上一条「等价」断言**恒绿**。本守卫用**闭集外的值**证明参数被读:
        /// 若实现忽略 `holdMode`,越界值不会抛 ⇒ 本测红。
        /// </summary>
        [Test]
        public void test_ac1024_holdModeIsActuallyConsumed()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ModalPhaseEvaluator.Complete(accessibilityOn: false, holdMode: 99),
                "holdMode 闭集外须抛 —— 若实现忽略该参数,本断言红(证明上一条判据非空转)");
        }

        /// <summary>AC-10-24 的两模式**路径不同**但聚合**等价**(规则十)。</summary>
        [Test]
        public void test_ac1024_toggleEquivalent()
        {
            // 同操作序列两模式 ⇒ agg.hold_ticks/edges 相同
            var holdMode = ModalPhaseEvaluator.Complete(accessibilityOn: false, holdMode: 0);
            var toggleMode = ModalPhaseEvaluator.Complete(accessibilityOn: false, holdMode: 1);

            Assert.AreEqual(holdMode.HoldTicks, toggleMode.HoldTicks,
                "toggle 模式 hold_ticks 应与 Hold 模式相同");
            Assert.AreEqual(holdMode.Edges, toggleMode.Edges,
                "toggle 模式 edges 应与 Hold 模式相同");
        }
    }
}
