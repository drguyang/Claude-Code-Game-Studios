// M2 接线轮阶段 1 装配轮 · SimTickDriver 的 EditMode 直测。
//
// 覆盖三条:
//   ① 累加精确性 —— 小数余量换算整 tick,CurrentTick 单调;
//   ② 死亡螺旋护栏 —— 单次超限打满 maxStepsPerFrame 即丢余量、告警恰一次、下次从 0 起算;
//   ③ S6 停机时钟 —— halt 期不调 Advance,余数保留,恢复不补跑墙钟。
//
// 判据纪律:确定性(无随机、无墙钟),每次运行同结果(Coding Standards)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="SimTickDriver"/> 单元测试。</summary>
    public class SimTickDriverTest
    {
        [Test]
        public void test_simTickDriver_accumulatesExactly()
        {
            // Arrange
            var driver = new SimTickDriver(); // 默认 0.05 s / tick,单次上限 5

            // Act + Assert:不足一个 tick ⇒ 0
            Assert.AreEqual(0, driver.Advance(0.04), "0.04 s < 0.05 s ⇒ 不发 tick");
            Assert.AreEqual(0L, driver.CurrentTick);

            // Act + Assert:补足 ⇒ 恰 1
            Assert.AreEqual(1, driver.Advance(0.02), "0.04 + 0.02 ≥ 0.05 ⇒ 恰 1 tick");
            Assert.AreEqual(1L, driver.CurrentTick, "CurrentTick 随 tick 递增");

            // Act + Assert:连续 40 次整 tick,每次都恰 1,且 CurrentTick 单调不减
            long prev = driver.CurrentTick;
            for (int i = 0; i < 40; i++)
            {
                Assert.AreEqual(1, driver.Advance(0.05), $"第 {i} 次整 tick 应恰推进 1");
                Assert.Greater(driver.CurrentTick, prev, "CurrentTick 必须严格单调");
                prev = driver.CurrentTick;
            }

            // Assert:合计精确 = 1 + 40
            Assert.AreEqual(41L, driver.CurrentTick, "累计 tick 数精确");
        }

        [Test]
        public void test_simTickDriver_deathSpiral_dropsRemainder()
        {
            // Arrange:注入告警回调,便于断言「告警恰一次」(不依赖 Console 抓取)
            var warnings = new List<string>();
            var driver = new SimTickDriver(onDroppedRemainder: warnings.Add);

            // Act:一次喂进 10 秒(= 200 tick 的量),远超 maxStepsPerFrame
            int steps = driver.Advance(10.0);

            // Assert:恰打满上限,不挂、不超发
            Assert.AreEqual(5, steps, "单次推进上限 = maxStepsPerFrame(默认 5)");
            Assert.AreEqual(5L, driver.CurrentTick);
            Assert.AreEqual(1, warnings.Count, "死亡螺旋告警只记一次(防日志刷屏)");
            Assert.AreEqual(0d, driver.RemainderSeconds, "余量已丢弃(否则下次从 9.75 s 起算)");

            // Act + Assert:下一次从 0 余量起算 ⇒ 0.01 s 不发 tick
            Assert.AreEqual(0, driver.Advance(0.01), "余量已清 ⇒ 从 0 起算");
            Assert.AreEqual(5L, driver.CurrentTick);

            // Act + Assert:再次超限仍各记 0 次新告警(一次性告警语义)
            Assert.AreEqual(5, driver.Advance(10.0));
            Assert.AreEqual(10L, driver.CurrentTick);
            Assert.AreEqual(1, warnings.Count, "第二次同型超限不再重复告警");
        }

        // 仅契约:余量跨调用保留;halt 调用方(BootRoot 暂停)归 Phase 2,本测不覆盖。
        // (2026-10-09 测试面 T6 改名 —— 原名 s6Halt_* 暗示覆盖 halt 调用方语义,超出本测实际面。)
        [Test]
        public void test_simTickDriver_remainderSurvivesAcrossCalls()
        {
            // Arrange
            var driver = new SimTickDriver();

            // Act:halt 前半段 —— 只走过 0.03 s,不足一个 tick
            Assert.AreEqual(0, driver.Advance(0.03), "0.03 s < 0.05 s ⇒ 0 tick");

            // halt:期间**不调用 Advance**(S6 停机时钟语义 —— halt 期墙钟不进入累加器);
            // 余数保留,不丢、不清零。

            // Act:恢复后再来 0.03 s
            int steps = driver.Advance(0.03);

            // Assert:0.03(保留)+ 0.03 = 0.06 ⇒ 恰 1 tick —— 不是补跑墙钟的 2 tick
            Assert.AreEqual(1, steps, "余数保留 ⇒ 合并后恰 1 tick(非补跑)");
            Assert.AreEqual(1L, driver.CurrentTick, "CurrentTick 只随实际 tick 增长");
            Assert.That(driver.RemainderSeconds, Is.EqualTo(0.01).Within(1e-9),
                "余数留在累加器,供下一次继续");
        }

        // ══════════ 测试面 T1:等值边界(封 `>=` → `>` 逃逸)════════════

        [Test]
        public void test_simTickDriver_advance_exactBoundary_firesOneTick()
        {
            // Arrange:新鲜驱动器,默认 _tickSeconds = 0.05(double 字面量,与入参**精确相等**)
            var driver = new SimTickDriver();

            // Act:余量恰好等于一个 tick(0.05 == 0.05,浮点精确相等,无 Within 容差)
            int steps = driver.Advance(0.05);

            // Assert:累加器判据是 `>=` 而非 `>` —— 恰等值必须发 tick
            // (本条即 `>=`→`>` 逃逸变异的判别力红点)
            Assert.AreEqual(1, steps, "余量恰等于 tick 周期(0.05 == 0.05)⇒ 恰 1 tick(判据须为 >=)");
            Assert.AreEqual(1L, driver.CurrentTick);
            Assert.AreEqual(0d, driver.RemainderSeconds, "整除后余量恰为 0");
        }

        // ══════════ 测试面 T2:三条 fail-loud(负参拒收,具名参数)════════════

        [Test]
        public void test_simTickDriver_ctorAndAdvance_failLoud_onBadArgs()
        {
            // ① ctor tickSeconds = 0 ⇒ ArgumentOutOfRangeException(具名 tickSeconds)
            var exTick = Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimTickDriver(tickSeconds: 0d));
            Assert.AreEqual("tickSeconds", exTick.ParamName, "ctor 须以具名参数拒 0 tick 时长");

            // ② ctor maxStepsPerFrame = 0 ⇒ ArgumentOutOfRangeException(具名 maxStepsPerFrame)
            var exSteps = Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimTickDriver(maxStepsPerFrame: 0));
            Assert.AreEqual("maxStepsPerFrame", exSteps.ParamName, "护栏上限至少为 1");

            // ③ Advance 负增量 ⇒ ArgumentOutOfRangeException(具名 deltaSeconds;S6 口径传 0 或不调)
            var driver = new SimTickDriver();
            var exDelta = Assert.Throws<ArgumentOutOfRangeException>(
                () => driver.Advance(-0.01));
            Assert.AreEqual("deltaSeconds", exDelta.ParamName, "负墙钟增量须拒收");
            Assert.AreEqual(0L, driver.CurrentTick, "拒收后不推进任何 tick");
        }
    }
}
