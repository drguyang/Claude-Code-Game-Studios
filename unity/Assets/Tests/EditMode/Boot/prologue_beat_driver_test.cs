// 序章节拍驱动(N1/N2 的落地件)夹具。
//
// 权威来源:`design/levels/prologue-beat-script.md` §第一段 ·
//          `unity/Assets/Gameplay.Boot/PrologueBeatDriver.cs` 的边界三条
//
// ⚠️ 本件钉的是**驱动语义**(串行 / 一次性 / 前置 / 不叠加),不是具体文案 ——
//    文案归 writer,改字不该让本件红。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>序章节拍驱动:条件 / 串行 / 前置 / 幂等。</summary>
    [TestFixture]
    public class prologue_beat_driver_test
    {
        /// <summary>记录字幕显示的假 sink(42 侧 `SubtitleLineView` 的替身)。</summary>
        private sealed class FakeSubtitles : ISubtitleSink
        {
            public readonly List<string> Shown = new List<string>();
            public int HideCount;

            public void Show(in SubtitleLineData line) => Shown.Add(line.Text);
            public void Hide() => HideCount++;
        }

        private static PrologueBeatDriver MakeDriver(
            IReadOnlyList<PrologueBeat> beats, Func<WorldPos> cell, FakeSubtitles subs, int ticks = 10)
            => new PrologueBeatDriver(beats, cell, subs, narrationTicks: ticks);

        /// <summary>无条件拍在首个 tick 就播,且只播一次。</summary>
        [Test]
        public void test_unconditionalBeat_firesOnFirstTick_andOnlyOnce()
        {
            var subs = new FakeSubtitles();
            var driver = MakeDriver(
                new[] { new PrologueBeat("P1-01", () => true, "开场") },
                () => default, subs);

            driver.OnTickEdge(1);
            Assert.That(subs.Shown, Is.EqualTo(new[] { "开场" }));
            Assert.That(driver.HasFired("P1-01"), Is.True);

            // 同一个 tick 重复调用不得二次触发(幂等)
            driver.OnTickEdge(1);
            Assert.That(subs.Shown.Count, Is.EqualTo(1), "同一 tick 重复调用不得再播");
        }

        /// <summary>一条在播时不触发下一拍;停留期满后才轮到下一拍(串行)。</summary>
        [Test]
        public void test_noOverlap_nextBeatWaitsForCurrentToExpire()
        {
            var subs = new FakeSubtitles();
            var driver = MakeDriver(
                new[]
                {
                    new PrologueBeat("A", () => true, "第一句"),
                    new PrologueBeat("B", () => true, "第二句"),
                },
                () => default, subs, ticks: 10);

            driver.OnTickEdge(1);           // A 显示,hide 于 tick 11
            Assert.That(driver.ShowingId, Is.EqualTo("A"));

            driver.OnTickEdge(5);           // 仍在 A 的停留期内
            Assert.That(subs.Shown.Count, Is.EqualTo(1), "一条在播时不得叠加下一拍");
            Assert.That(driver.HasFired("B"), Is.False);

            driver.OnTickEdge(11);          // 到点:清 A
            Assert.That(driver.ShowingId, Is.Null);
            Assert.That(subs.HideCount, Is.EqualTo(1));

            driver.OnTickEdge(12);          // 下一拍
            Assert.That(subs.Shown, Is.EqualTo(new[] { "第一句", "第二句" }));
        }

        /// <summary>区域拍:玩家不在区域内不触发;进入后才触发。</summary>
        [Test]
        public void test_zoneBeat_requiresPlayerInside()
        {
            var subs = new FakeSubtitles();
            WorldPos cell = new WorldPos(50, 0, 20);
            bool inZone = false;

            var driver = MakeDriver(
                new[] { new PrologueBeat("Z", () => inZone, "区域内") },
                () => cell, subs);

            driver.OnTickEdge(1);
            Assert.That(driver.HasFired("Z"), Is.False, "不在区域内不得触发");

            inZone = true;
            driver.OnTickEdge(2);
            Assert.That(driver.HasFired("Z"), Is.True, "进入区域后须触发");
            Assert.That(subs.Shown, Is.EqualTo(new[] { "区域内" }));
        }

        /// <summary>前置未完成时,即使条件恒真也不触发。</summary>
        [Test]
        public void test_requires_unmetBlocksFiring()
        {
            var subs = new FakeSubtitles();
            var driver = MakeDriver(
                new[]
                {
                    new PrologueBeat("前", () => false, "前一拍"),
                    new PrologueBeat("后", () => true, "后一拍", requires: "前"),
                },
                () => default, subs);

            for (int t = 1; t <= 30; t++) driver.OnTickEdge(t);

            Assert.That(driver.HasFired("后"), Is.False,
                "前置未完成时后一拍不得触发 —— 否则「读完登记簿才发生」这类顺序约束失效");
            Assert.That(subs.Shown, Is.Empty);
        }

        /// <summary>前置完成后,后一拍在下一 tick 触发。</summary>
        [Test]
        public void test_requires_metLetsNextFire()
        {
            var subs = new FakeSubtitles();
            bool gate = false;
            var driver = MakeDriver(
                new[]
                {
                    new PrologueBeat("前", () => gate, "前一拍"),
                    new PrologueBeat("后", () => true, "后一拍", requires: "前"),
                },
                () => default, subs, ticks: 5);

            gate = true;
            driver.OnTickEdge(1);           // 前 触发
            driver.OnTickEdge(2);           // 仍在停留
            driver.OnTickEdge(7);           // 到点清
            driver.OnTickEdge(8);           // 后 触发

            Assert.That(driver.HasFired("前"), Is.True);
            Assert.That(driver.HasFired("后"), Is.True);
            Assert.That(subs.Shown, Is.EqualTo(new[] { "前一拍", "后一拍" }));
        }

        /// <summary>空字幕的拍也算触发(只有状态、不显示)。</summary>
        [Test]
        public void test_emptyNarration_stillFiresWithoutSubtitle()
        {
            var subs = new FakeSubtitles();
            var driver = MakeDriver(
                new[] { new PrologueBeat("quiet", () => true, "") },
                () => default, subs);

            driver.OnTickEdge(1);

            Assert.That(driver.HasFired("quiet"), Is.True);
            Assert.That(subs.Shown, Is.Empty, "空字幕不得调 Show");
            Assert.That(driver.ShowingId, Is.Null);
        }

        /// <summary>停留时长 ≤ 0 在构造期拒收(否则字幕同一 tick 显示又清空)。</summary>
        [Test]
        public void test_zeroNarrationTicks_isRejected()
        {
            var subs = new FakeSubtitles();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PrologueBeatDriver(Array.Empty<PrologueBeat>(), () => default, subs, narrationTicks: 0));
        }

        /// <summary>诊断读数:已触发数 / 总拍数 / 在播拍 id(走查时唯一的「看到过哪一句」证据面)。</summary>
        [Test]
        public void test_summary_reportsFiredTotalAndShowing()
        {
            var subs = new FakeSubtitles();
            var driver = MakeDriver(
                new[]
                {
                    new PrologueBeat("P1-01", () => true, "第一句"),
                    new PrologueBeat("P1-02", () => true, "第二句"),
                    new PrologueBeat("P1-03", () => false, "第三句"),
                },
                () => default, subs, ticks: 10);

            Assert.That(driver.TotalCount, Is.EqualTo(3));
            Assert.That(driver.Summary, Is.EqualTo("0/3 show=-"), "首 tick 之前无触发、无在播");

            driver.OnTickEdge(1);
            Assert.That(driver.Summary, Is.EqualTo("1/3 show=P1-01"));

            driver.OnTickEdge(11);   // 到点清字幕
            Assert.That(driver.Summary, Is.EqualTo("1/3 show=-"), "清屏后不得仍报在播");
        }
    }
}
