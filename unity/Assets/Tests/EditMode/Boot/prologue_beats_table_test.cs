// N2 剧情脚本事件夹具(2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` §1 N2(「一次性触发:条件 + 动作」)
//
// ═══ 它钉的两层 ════════════════════════════════════════════════════════════
//   ① **驱动的动作语义**:`PrologueBeat.Effect` 在标记触发**之前**执行、先于字幕、
//      抛异常 ⇒ 本拍不算触发(下个 tick 重试)。这三条决定了「字幕播了但事情没发生」
//      这种最坏的不一致**结构上不可能**。
//   ② **表里的接线**:P1-07 的人群动作只在 P1-06 之后、且只执行一次 ——
//      用计数动作验证,不碰任何渲染。
//
// ⚠️ 本件**不**验证人群的视觉(那是 `PrologueCrowd` 与真包走查的事)。
//    它验证的是「该不该调用」,不是「调用出来好不好看」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>N2:节拍动作(Effect)与 P1-07 接线。</summary>
    [TestFixture]
    public class prologue_beats_table_test
    {
        /// <summary>记录字幕的假 sink(42 侧 `SubtitleLineView` 的替身)。</summary>
        private sealed class FakeSubtitles : ISubtitleSink
        {
            public readonly List<string> Shown = new List<string>();
            public void Show(in SubtitleLineData line) => Shown.Add(line.Text);
            public void Hide() { }
        }

        private static PrologueBeatDriver MakeDriver(IReadOnlyList<PrologueBeat> beats,
                                                     Func<WorldPos> cell,
                                                     ISubtitleSink subs)
            => new PrologueBeatDriver(beats, cell, subs, narrationTicks: 1);

        // ── ① 驱动的动作语义 ────────────────────────────────────────────────

        /// <summary>动作只执行一次,且**先于**字幕(顺序可从共享日志读出)。</summary>
        [Test]
        public void test_effect_runsOnce_beforeNarration()
        {
            var log = new List<string>();
            int effectCalls = 0;

            var beats = new[]
            {
                new PrologueBeat("P", () => true, "台词",
                    effect: () => { effectCalls++; log.Add("effect"); }),
            };
            var driver = MakeDriver(beats, () => default, new LoggingSubtitles(log));

            driver.OnTickEdge(1);
            driver.OnTickEdge(2);

            Assert.That(effectCalls, Is.EqualTo(1), "一次性闩锁下动作只能执行一次");
            Assert.That(log, Is.EqualTo(new[] { "effect", "show:台词" }),
                "动作必须在字幕之前完成 —— 否则异常会让字幕先播出去");
        }

        /// <summary>动作抛异常 ⇒ 本拍**不标记触发**,下个 tick 重试(大声失败,不静默吞)。</summary>
        [Test]
        public void test_effectThrows_beatStaysUnfired_andRetries()
        {
            int attempts = 0;
            var driver = MakeDriver(
                new[]
                {
                    new PrologueBeat("P", () => true, "台词",
                        effect: () => { attempts++; throw new InvalidOperationException("路径不通"); }),
                },
                () => default, new FakeSubtitles());

            Assert.Throws<InvalidOperationException>(() => driver.OnTickEdge(1));
            Assert.That(driver.HasFired("P"), Is.False, "动作失败时不得标记已触发");
            Assert.That(attempts, Is.EqualTo(1));

            Assert.Throws<InvalidOperationException>(() => driver.OnTickEdge(2));
            Assert.That(attempts, Is.EqualTo(2), "下一 tick 必须重试 —— 否则该拍永久丢失");
        }

        /// <summary>无字幕的拍也能执行动作(「只有动作、没有台词」是合法形态)。</summary>
        [Test]
        public void test_effectWithEmptyNarration_stillRuns()
        {
            int calls = 0;
            var subs = new FakeSubtitles();
            var driver = MakeDriver(
                new[] { new PrologueBeat("P", () => true, null, effect: () => calls++) },
                () => default, subs);

            driver.OnTickEdge(1);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(subs.Shown, Is.Empty, "无台词的拍不得往字幕口塞空串");
            Assert.That(driver.HasFired("P"), Is.True);
        }

        // ── ② 表里的接线(P1-06 → P1-07)──────────────────────────────────

        /// <summary>
        /// 走完 P1-01…P1-07:P1-07 的「人群出现」动作在 P1-06 之后、且**只执行一次**。
        /// <para>⚠️ 这条同时钉住前置链:若有人把 P1-07 的 `requires` 摘掉,动作会提前执行,
        /// 本件当场红。</para>
        /// </summary>
        [Test]
        public void test_table_p1_07_runsArrivalEffect_onlyAfterP1_06()
        {
            var cell = new WorldPos(14, 0, 21);              // 石阶锚点格
            var regions = new RegionTriggerDriver(
                PrologueAnchors.BuildFirstActRegions(), () => cell);
            var subs = new FakeSubtitles();

            int arrivals = 0;
            var beats = PrologueBeats.BuildFirstAct(regions, () => arrivals++);
            var driver = MakeDriver(beats, () => cell, subs);

            // narrationTicks = 1 ⇒ 每两 tick 走一拍;逐 tick 推进并在关键点插入按 E。
            long tick = 1;
            void Step() { regions.OnTickEdge(tick); driver.OnTickEdge(tick); tick++; }

            Step();                                    // P1-01(无条件)
            Assert.That(arrivals, Is.EqualTo(0), "开场拍不得触发人群");

            Step();                                    // 清字幕
            cell = new WorldPos(14, 0, 21);
            driver.NotifyInteract(); Step();           // P1-02 石阶
            Assert.That(driver.HasFired("P1-02"), Is.True, "站在石阶格按 E 应触发 P1-02");

            Step();                                    // 清字幕
            cell = new WorldPos(17, 0, 23);
            driver.NotifyInteract(); Step();           // P1-03 告示
            Assert.That(driver.HasFired("P1-03"), Is.True);

            Step();                                    // 清字幕
            cell = new WorldPos(9, 0, 26);
            driver.NotifyInteract(); Step();           // P1-04 抬埋簿
            Assert.That(driver.HasFired("P1-04"), Is.True);

            Step();                                    // 清字幕
            Step();                                    // P1-05 神父走
            Assert.That(driver.HasFired("P1-05"), Is.True);
            Assert.That(arrivals, Is.EqualTo(0), "P1-05 时人群还不该出现");

            Step();                                    // 清字幕
            Step();                                    // P1-06 那一夜
            Assert.That(driver.HasFired("P1-06"), Is.True);
            Assert.That(arrivals, Is.EqualTo(0), "P1-06 当拍人群仍不该出现");

            Step();                                    // 清字幕
            Step();                                    // P1-07 人群出现
            Assert.That(driver.HasFired("P1-07"), Is.True);
            Assert.That(arrivals, Is.EqualTo(1), "P1-07 必须恰好执行一次动作");

            // 再推若干 tick:一次性闩锁下不得重放
            for (int i = 0; i < 5; i++) Step();
            Assert.That(arrivals, Is.EqualTo(1), "人群动作不得重复执行");
        }

        /// <summary>把字幕转成有序日志的 sink(只给顺序判据用)。</summary>
        private sealed class LoggingSubtitles : ISubtitleSink
        {
            private readonly List<string> _log;
            public LoggingSubtitles(List<string> log) { _log = log; }
            public void Show(in SubtitleLineData line) => _log.Add("show:" + line.Text);
            public void Hide() { }
        }
    }
}
