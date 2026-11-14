// 过场演出播放器(N3)夹具(2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` §1 N3 + `CutscenePlayer` 类首的三条分工
//
// ═══ 它钉的四件事 ═════════════════════════════════════════════════════════
//   ① 时序:按 tick 计,进出**成对**且**有序**;低帧率下一 tick 可跨多拍。
//   ② fail-closed:重叠开播 / 空表 / 零时长 / 负 tick 一律拒。
//   ③ 读档(tick 变小)⇒ 中断,而不是继续播 —— 否则「搭建演出」会在别人世界里重演。
//   ④ **冻结的接线**:把 `IsPlaying` 并进 `ModalInputFreeze` 后,移动轴在演出期折零。
//      这一条是本件与 N3 契约(「冻结输入」)之间唯一可自动化的接缝。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>过场演出:时序 / 成对 / fail-closed / 读档中断 / 冻结接线。</summary>
    [TestFixture]
    public class cutscene_player_test
    {
        private static CutsceneBeat Beat(string id, int ticks, List<string> log)
            => new CutsceneBeat(id, ticks,
                onEnter: () => log.Add("+" + id),
                onExit: () => log.Add("-" + id));

        // ── ① 时序 ──────────────────────────────────────────────────────────

        /// <summary>开播即进第一拍;之后按各拍时长依次推进,进出成对。</summary>
        [Test]
        public void test_advancesInOrder_byDuration()
        {
            var log = new List<string>();
            var player = new CutscenePlayer();
            player.Play(new[] { Beat("A", 10, log), Beat("B", 5, log) }, startTick: 100);

            Assert.That(log, Is.EqualTo(new[] { "+A" }), "开播即进第一拍");
            Assert.That(player.CurrentBeatId, Is.EqualTo("A"));

            player.OnTickEdge(105);
            Assert.That(log, Is.EqualTo(new[] { "+A" }), "未到期不得换拍");

            player.OnTickEdge(110);
            Assert.That(log, Is.EqualTo(new[] { "+A", "-A", "+B" }), "到期须先出后进");

            player.OnTickEdge(115);
            Assert.That(log, Is.EqualTo(new[] { "+A", "-A", "+B", "-B" }));
            Assert.IsFalse(player.IsPlaying, "播完须退出播放态");
            Assert.IsNull(player.CurrentBeatId);
            Assert.AreEqual(-1, player.BeatIndex);
        }

        /// <summary>低帧率:一个 tick 边沿跨过多拍(用 while 而不是 if)。</summary>
        [Test]
        public void test_lowFrameRate_crossesMultipleBeats()
        {
            var log = new List<string>();
            var player = new CutscenePlayer();
            player.Play(new[] { Beat("A", 1, log), Beat("B", 1, log), Beat("C", 1, log) }, startTick: 0);

            player.OnTickEdge(10);   // 一次跨过三拍

            Assert.That(log, Is.EqualTo(new[] { "+A", "-A", "+B", "-B", "+C", "-C" }));
            Assert.IsFalse(player.IsPlaying);
        }

        /// <summary>播完回调只执行一次。</summary>
        [Test]
        public void test_onComplete_firesOnce()
        {
            int done = 0;
            var player = new CutscenePlayer();
            player.Play(new[] { new CutsceneBeat("A", 2) }, startTick: 0, onComplete: () => done++);

            player.OnTickEdge(2);
            Assert.AreEqual(1, done);

            for (long t = 3; t < 10; t++)
            {
                player.OnTickEdge(t);
                Assert.AreEqual(1, done, "回调不得重复");
            }
        }

        // ── ② 成对与 fail-closed ─────────────────────────────────────────────

        /// <summary>`Abort` 会调当前拍的 `OnExit`,进出成对。</summary>
        [Test]
        public void test_abort_callsOnExit()
        {
            var log = new List<string>();
            var player = new CutscenePlayer();
            player.Play(new[] { Beat("A", 100, log), Beat("B", 100, log) }, startTick: 0);

            player.Abort();

            Assert.That(log, Is.EqualTo(new[] { "+A", "-A" }), "中断也须成对,否则世界停在半演状态");
            Assert.IsFalse(player.IsPlaying);
            player.Abort();   // 幂等
        }

        /// <summary>重叠开播:抛(不得静默替换 —— 替换会让前一段的 OnExit 永不执行)。</summary>
        [Test]
        public void test_playWhilePlaying_throws()
        {
            var player = new CutscenePlayer();
            player.Play(new[] { new CutsceneBeat("A", 100) }, startTick: 0);

            Assert.Throws<InvalidOperationException>(
                () => player.Play(new[] { new CutsceneBeat("B", 10) }, startTick: 0));
        }

        /// <summary>拍 / 表的构造期校验。</summary>
        [Test]
        public void test_validation()
        {
            Assert.Throws<ArgumentException>(() => new CutsceneBeat("", 1), "空 id 须拒");
            Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneBeat("A", 0), "零时长须拒");

            var player = new CutscenePlayer();
            Assert.Throws<ArgumentNullException>(() => player.Play(null, 0));
            Assert.Throws<ArgumentException>(() => player.Play(new CutsceneBeat[0], 0), "空表须拒");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => player.Play(new[] { new CutsceneBeat("A", 1) }, -1));
        }

        // ── ③ 读档 ──────────────────────────────────────────────────────────

        /// <summary>tick 变小 = 读档 ⇒ 中断(不抛、不继续播)。</summary>
        [Test]
        public void test_tickRollback_aborts()
        {
            var log = new List<string>();
            var player = new CutscenePlayer();
            player.Play(new[] { Beat("A", 1000, log) }, startTick: 500);
            Assert.IsTrue(player.IsPlaying);

            Assert.DoesNotThrow(() => player.OnTickEdge(21), "读档不是错误");
            Assert.IsFalse(player.IsPlaying, "跨界演出须中断");
            Assert.That(log, Is.EqualTo(new[] { "+A", "-A" }));
        }

        /// <summary>同 tick 重复调用幂等。</summary>
        [Test]
        public void test_sameTick_idempotent()
        {
            var log = new List<string>();
            var player = new CutscenePlayer();
            player.Play(new[] { Beat("A", 5, log) }, startTick: 0);

            player.OnTickEdge(5);
            player.OnTickEdge(5);
            Assert.That(log, Is.EqualTo(new[] { "+A", "-A" }), "同一 tick 不得推进两次");
        }

        // ── ④ 冻结接线 ──────────────────────────────────────────────────────

        /// <summary>
        /// 把 `IsPlaying` 并进 `ModalInputFreeze` 之后:演出期移动轴折零,播完恢复。
        /// <para>⚠️ 这条钉的是**装配契约**,不是 `CutscenePlayer` 自己 —— 冻结的唯一判定点
        /// 仍是 `ModalInputFreeze`(两处各写一份「冻结中」= 第二真源)。</para>
        /// </summary>
        [Test]
        public void test_freeze_blocksMovement_whilePlaying()
        {
            var player = new CutscenePlayer();
            var freeze = new ModalInputFreeze(() => player.IsPlaying);
            var raw = new Vector2(1f, 0f);

            Assert.That(BootRoot.ResolveMoveAxis(raw, freeze), Is.EqualTo(raw), "未开播时须透传");

            player.Play(new[] { new CutsceneBeat("A", 10) }, startTick: 0);
            Assert.That(BootRoot.ResolveMoveAxis(raw, freeze), Is.EqualTo(Vector2.zero),
                "演出期移动轴须折零");

            player.OnTickEdge(10);
            Assert.That(BootRoot.ResolveMoveAxis(raw, freeze), Is.EqualTo(raw), "播完须恢复");
        }
    }
}
