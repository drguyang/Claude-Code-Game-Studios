// diagnosis-system Story 005 —— 快照冻结与窗口陈旧(PlayMode 集成编排)。
//
// Required evidence 第二件:与 EditMode `reading_state_test` 分工 —— 那边是状态机本体
// 单测;这边以 **fake casebook**(39 未实现的承接,Ready)编排「8 持规则 / 39 存值」
// 的协作:每 (patient, channel) 存读数 + 两个 tick 值,8 的 FSM 只被喂参数。
//
// 覆盖:AC-8-25 快照冻结 vs 持续刷新 · AC-8-48 完成 tick 采样 · S-8.2 两窗口托底
//   · AC-8-27 幂等旧态的 ledger 半边(状态只旧一次)
//
// ⚠️ 两个 tick 值住**测试侧 ledger** —— 这正是 Control Manifest Forbidden
//    「8 自己存两个 tick 值(归 39)」的可执行体现:生产四类型零字段,存取全在本文件。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PlayMode.DiagnosisSystem
{
    internal sealed class SnapshotStalenessTest
    {
        private const int RecheckWindow = 5;
        private const int StaleWindow = 100;

        /// <summary>fake 39:读数 + 两个 tick 值的承接(39 casebook 未实现,测试侧 fake)。</summary>
        private sealed class FakeCasebook
        {
            private readonly Dictionary<(int patient, int channel), (SignReadState state,
                long? snapshotTick, long? lastRecheckTick)> _rows
                = new Dictionary<(int, int), (SignReadState, long?, long?)>();

            public SignReadState StateOf(int p, int c)
                => _rows.TryGetValue((p, c), out var r) ? r.state : SignReadState.Blank;

            public long? SnapshotTickOf(int p, int c)
                => _rows.TryGetValue((p, c), out var r) ? r.snapshotTick : null;

            public long? LastRecheckTickOf(int p, int c)
                => _rows.TryGetValue((p, c), out var r) ? r.lastRecheckTick : null;

            /// <summary>写入读数行(39 持值;调用方给 tick)。</summary>
            public void Write(int p, int c, SignReadState state, long? snapshotTick, long? lastRecheckTick)
                => _rows[(p, c)] = (state, snapshotTick, lastRecheckTick);
        }

        private static DiagnosisReadFloorEvaluator.Outcome Positive(string word)
            => new DiagnosisReadFloorEvaluator.Outcome(SignReadState.Positive, word, true);

        private static DiagnosisReadFloorEvaluator.Outcome Negative(string word)
            => new DiagnosisReadFloorEvaluator.Outcome(SignReadState.Negative, word, true);

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-25:快照冻结 vs 持续刷新(ledger 编排)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_25_snapshotRow_frozenAfterCompletion_continuousRowFollowsTick()
        {
            var book = new FakeCasebook();
            // Progress 随 tick 变化:tick ≥ 20 体征翻阳性
            DiagnosisReadFloorEvaluator.Outcome Source(long t)
                => t < 20 ? Negative("胸清") : Positive("红疹");

            // 快照型:动作在 tick 10 完成 ⇒ 按完成 tick 采样,写入 39
            long completion = 10;
            ReadingSnapshot snap = DiagnosisSnapshotSampler.SampleOnCompletion(completion, Source);
            book.Write(p: 1, c: 2, snap.State, snap.SampledAt, snap.SampledAt);

            // 9 的 Progress 在快照之后变化(tick 30)
            DiagnosisReadFloorEvaluator.Outcome now = DiagnosisSnapshotSampler.RefreshContinuous(30, Source);

            // 断言 1:快照行逐字不变(词/态/采样 tick 都锚在 completion)
            Assert.AreEqual("胸清", snap.DisplayWord, "快照词冻结(Progress 变了也不变)");
            Assert.AreEqual(SignReadState.Negative, snap.State, "快照态冻结");
            Assert.AreEqual(completion, snap.SampledAt, "锚 = completion_tick");
            Assert.AreEqual(completion, book.SnapshotTickOf(1, 2), "39 存的快照 tick 不漂移");

            // 断言 2:持续型行跟当前 tick
            Assert.AreEqual(SignReadState.Positive, now.State, "持续型跟 tick 刷新");
            Assert.AreEqual("红疹", now.DisplayWord, "持续型词随 Progress 变");

            // 断言 3:冻结是结构性的 —— 快照型路径**没有** currentTick 入参(反射)
            var sampleParams = typeof(DiagnosisSnapshotSampler)
                .GetMethod(nameof(DiagnosisSnapshotSampler.SampleOnCompletion))!
                .GetParameters();
            Assert.IsFalse(Array.Exists(sampleParams, p => p.Name == "currentTick"),
                "SampleOnCompletion 无 currentTick 参数 —— 结构上无法「猜当前 tick」(AC-8-48)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-48:完成 tick 采样(动作中段 Progress 变化 ⇒ 两跑仍逐位一致)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_48_completionTickSampling_midActionProgressChange_stillBitIdentical()
        {
            // 动作 start=10、中段 20 时 Progress 变一次、完成 30 前又变一次(三段源)——
            // 中段词与完成词必须**不同**,否则「≠ 中段」断言恒真比较同值而失真
            DiagnosisReadFloorEvaluator.Outcome Source(long t)
                => t < 20 ? Negative("初词") : t < 30 ? Positive("变词") : Negative("终词");

            long start = 10, mid = 20, completion = 30;

            ReadingSnapshot runA = DiagnosisSnapshotSampler.SampleOnCompletion(completion, Source);
            ReadingSnapshot runB = DiagnosisSnapshotSampler.SampleOnCompletion(completion, Source);

            Assert.AreEqual(runA.DisplayWord, runB.DisplayWord, "两跑词逐位相同");
            Assert.AreEqual(Source(completion).DisplayWord, runA.DisplayWord,
                "≡ 完成 tick 的值(中段变化不进快照)");
            Assert.AreNotEqual(Source(mid).DisplayWord, runA.DisplayWord,
                "≠ 中段 tick 值(若按中段/当前采样即红)");
            Assert.AreNotEqual(Source(start).DisplayWord, runA.DisplayWord,
                "≠ 开始 tick 值(采样点须钉在完成)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // S-8.2:两窗口托底(ledger 编排 + 合成窗口值)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s82_windows_recheckSuppressionAndStaleExpiry_throughLedger()
        {
            var book = new FakeCasebook();

            // 查体完成于 tick 0(首次,lastRecheck = null ⇒ 必产读数)
            ReadingTransition first = DiagnosisReadingFsm.OnExaminationCompleted(
                book.StateOf(7, 0), Positive("红疹"), channelAccessible: true,
                0, book.LastRecheckTickOf(7, 0), RecheckWindow);
            Assert.IsTrue(first.ProducedNewOutcome, "首查必产读数");
            book.Write(7, 0, first.State, snapshotTick: 0, lastRecheckTick: 0);

            // tick 4 / 5(窗口内)复查 ⇒ 抑制
            ReadingTransition r4 = DiagnosisReadingFsm.OnExaminationCompleted(
                book.StateOf(7, 0), Negative("胸清"), channelAccessible: true,
                4, book.LastRecheckTickOf(7, 0), RecheckWindow);
            Assert.IsFalse(r4.ProducedNewOutcome, "4 tick 复查抑制");
            ReadingTransition r5 = DiagnosisReadingFsm.OnExaminationCompleted(
                book.StateOf(7, 0), Negative("胸清"), channelAccessible: true,
                5, book.LastRecheckTickOf(7, 0), RecheckWindow);
            Assert.IsFalse(r5.ProducedNewOutcome, "5 tick 复查抑制(边界含)");

            // tick 6 托底:距上次重查 > 5 ⇒ 标旧
            ReadingTransition w6 = DiagnosisReadingFsm.EvaluateWindows(
                book.StateOf(7, 0), 6, book.SnapshotTickOf(7, 0), book.LastRecheckTickOf(7, 0),
                StaleWindow, RecheckWindow);
            Assert.AreEqual(SignReadState.Stale, w6.State, "6 tick 标旧");
            book.Write(7, 0, w6.State, book.SnapshotTickOf(7, 0), book.LastRecheckTickOf(7, 0));

            // 旧态幂等:再托底不重复标(ledger 半边 —— 状态只「旧」一次)
            ReadingTransition w6b = DiagnosisReadingFsm.EvaluateWindows(
                book.StateOf(7, 0), 7, book.SnapshotTickOf(7, 0), book.LastRecheckTickOf(7, 0),
                StaleWindow, RecheckWindow);
            Assert.IsFalse(w6b.StaleApplied, "已旧再托底:幂等");
            Assert.AreEqual(SignReadState.Stale, book.StateOf(7, 0), "ledger 状态仍旧");

            // STALE_WINDOW 到期(快照过期)⇒ 旧:另起一行,快照锚 0,当前 101
            book.Write(7, 1, SignReadState.Positive, snapshotTick: 0, lastRecheckTick: null);
            ReadingTransition st = DiagnosisReadingFsm.EvaluateWindows(
                book.StateOf(7, 1), 101, book.SnapshotTickOf(7, 1), book.LastRecheckTickOf(7, 1),
                StaleWindow, RecheckWindow);
            Assert.AreEqual(SignReadState.Stale, st.State, "快照过 STALE_WINDOW ⇒ 旧");
            book.Write(7, 1, st.State, book.SnapshotTickOf(7, 1), book.LastRecheckTickOf(7, 1));

            // 旧后重查(转移图「旧─查─►新读数」优先于抑制):产新读数、重置两 tick
            ReadingTransition recheck = DiagnosisReadingFsm.OnExaminationCompleted(
                book.StateOf(7, 1), Positive("红疹"), channelAccessible: true,
                102, book.LastRecheckTickOf(7, 1), RecheckWindow);
            Assert.IsTrue(recheck.ProducedNewOutcome, "旧态复查必产新读数(不被旧抑制)");
            book.Write(7, 1, recheck.State, snapshotTick: 102, lastRecheckTick: 102);
            Assert.AreEqual(SignReadState.Positive, book.StateOf(7, 1), "新读数落账");
        }
    }
}
