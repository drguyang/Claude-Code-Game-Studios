// diagnosis-system Story 005 —— 读数/判断状态机测试(QA Test Cases 10 条 + AC 状态值层)。
//
// 覆盖:AC-8-21 三态可分 · AC-8-22 不改口 · AC-8-24 恰四态/恰三态 · AC-8-25 快照冻结
//   · AC-8-48 完成 tick · AC-8-27 幂等旧态(零重复 cue)· S-8.2 窗口托底 · AC-8-28 不踩刹车
//   · AC-8-29 痕不计分(反射)· AC-8-30 零反馈(反射)· AC-8-31 无法配合 · AC-8-47 关病例
//   · AC-8-49 潜伏期合法输出 · 铁律④ 成长门控 · AC-8-45 双端不回写(结构面)· AC-8-46 无 Skill 参数
//
// ⚠️ AC-8-45 **网络传输子句 NOT-RUN(BLOCKED-BY-45 epic)**:本测是 in-process 双端
//    fake 结构判据,不冒充真联机。
// ⚠️ 反空转:扫描机器与负夹具共用同一台机器(影子类型注入必红,点名)。
// ⚠️ M9 登记:参数名 token 扫描(AC-8-28/30/31/46)是**命名面防线** —— 参数改名即可绕过;
//    类型 / IL 面守卫另属 DiagnosisBoundaryGates([D-*] 谓词族),两层各守一面。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    internal sealed class ReadingStateTest
    {
        // ── 合成窗口值(Guardrail:STALE/RECHECK 值归用户 ⇒ 测试以合成 tick 注入)──────
        private const int RecheckWindow = 5;
        private const int StaleWindow = 100;

        // ── 求值结果夹具(story 003 求值器的输出形状)────────────────────────────────
        private static DiagnosisReadFloorEvaluator.Outcome Positive(string word = "红疹")
            => new DiagnosisReadFloorEvaluator.Outcome(SignReadState.Positive, word, true);

        private static DiagnosisReadFloorEvaluator.Outcome Negative(string word = "胸清")
            => new DiagnosisReadFloorEvaluator.Outcome(SignReadState.Negative, word, true);

        private static DiagnosisReadFloorEvaluator.Outcome Unreadable(string word = null)
            => new DiagnosisReadFloorEvaluator.Outcome(SignReadState.UnreadableNegative, word, false);

        // ── 本 story 四类型(扫描种子)───────────────────────────────────────────────
        private static readonly Type[] FourTypes =
        {
            typeof(DiagnosisReadingFsm),
            typeof(DiagnosisJudgmentFsm),
            typeof(DiagnosisSnapshotSampler),
            typeof(DiagnosisGrowthGate),
        };

        private static readonly Type[] ContractTypes =
        {
            typeof(ReadingTransition),
            typeof(JudgmentTransition),
            typeof(CaseCloseResult),
            typeof(ReadingSnapshot),
        };

        /// <summary>共用扫描机器:字段 / 属性 / 事件名中命中任一禁 token 的成员名列表。</summary>
        private static List<string> ScanMemberNamesForTokens(IEnumerable<Type> types, params string[] tokens)
        {
            var hits = new List<string>();
            foreach (Type t in types)
            {
                const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
                                      | BindingFlags.Instance | BindingFlags.Static
                                      | BindingFlags.DeclaredOnly;
                foreach (MemberInfo m in t.GetMembers(F))
                    foreach (string tok in tokens)
                        if (m.Name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                            hits.Add($"{t.Name}.{m.Name}~{tok}");
            }
            return hits;
        }

        /// <summary>共用扫描机器:指定方法的参数名中命中禁 token。</summary>
        private static List<string> ScanMethodParametersForTokens(Type type, params string[] tokens)
        {
            var hits = new List<string>();
            foreach (MethodInfo mi in type.GetMethods(BindingFlags.Public | BindingFlags.Static
                                                      | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo p in mi.GetParameters())
                    foreach (string tok in tokens)
                        if (p.Name != null && p.Name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                            hits.Add($"{type.Name}.{mi.Name}({p.Name})~{tok}");
            return hits;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:三态可分(AC-8-21)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_21_threeReadingForms_pairwiseDistinguishable()
        {
            // Arrange:三个病灶夹具 —— 空行 / 阳性 / 阴性(数据面各不同)
            var blankState = SignReadState.Blank;                       // (a) 未做手段 = 空行
            var positiveState = Positive().State;                       // (b) 读到体征
            var negativeState = Negative().State;                       // (c) 做了无体征
            var unreadableState = Unreadable().State;                   // (附)已查读不出(数据可分)

            // Act + Assert:数据面三 + 变体四值两两不同
            var distinct = new[] { blankState, positiveState, negativeState, unreadableState }
                .Distinct().Count();
            Assert.AreEqual(4, distinct, "空行/阳性/阴性/已查读不出四值须两两可分(数据面)");

            // 形态面:三态可分(空行 Pending / 阳性 Positive / 阴性与读不出同形态)
            Assert.AreEqual(ReadingForm.Pending, DiagnosisReadingFsm.FormOf(blankState));
            Assert.AreEqual(ReadingForm.Positive, DiagnosisReadingFsm.FormOf(positiveState));
            Assert.AreEqual(ReadingForm.Negative, DiagnosisReadingFsm.FormOf(negativeState));
            Assert.AreNotEqual(
                DiagnosisReadingFsm.FormOf(blankState),
                DiagnosisReadingFsm.FormOf(positiveState),
                "空行与阳性形态不可重合");
            Assert.AreNotEqual(
                DiagnosisReadingFsm.FormOf(blankState),
                DiagnosisReadingFsm.FormOf(negativeState),
                "空行与阴性形态不可重合");

            // 空行夹具:无任何标记字段被置(不可得 ⇒ 强制空行,三标记全 false)
            ReadingTransition t = DiagnosisReadingFsm.OnExaminationCompleted(
                blankState, Positive(), channelAccessible: false, 0, null, RecheckWindow);
            Assert.AreEqual(SignReadState.Blank, t.State, "空行态");
            Assert.IsFalse(t.Changed, "空行无变化标记");
            Assert.IsFalse(t.StaleApplied, "空行无旧标记");
            Assert.IsFalse(t.ProducedNewOutcome, "空行无新读数标记 —— 无 badge/占位符对应物");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:不改口(AC-8-22)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_22_unreadableBelowFloor_isNegativeForm_neverRevertsToUnchecked()
        {
            // Arrange:Sign < READ_FLOOR ⇒ 求值器产出 UnreadableNegative(已查读不出)
            var outcome = Unreadable();

            // Act:通道可得(玩家做了手段)⇒ 状态 = 已查
            ReadingTransition t = DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Blank, outcome, channelAccessible: true, 0, null, RecheckWindow);

            // Assert:态 = 已查·阴性形态,绝不是 Blank(系统不得改口「未查」)
            Assert.AreEqual(SignReadState.UnreadableNegative, t.State, "已查读不出 ≠ 未查");
            Assert.AreEqual(ReadingForm.Negative, DiagnosisReadingFsm.FormOf(t.State),
                "呈现为阴性形态(与真阴性玩家眼里不可分是有意的)");
            Assert.AreNotEqual(SignReadState.Blank, t.State, "不可改口为未查/空行");
            Assert.IsTrue(t.ProducedNewOutcome, "这是一次完成的查(产读数),不是未查");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-24:恰四态 / 恰三态,无第四态
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_24_readingFormExactlyFour_judgmentExactlyThree_noFourthState()
        {
            // 体征行恰四态(呈现形态面)
            Assert.AreEqual(4, Enum.GetValues(typeof(ReadingForm)).Length, "ReadingForm 恰四态");
            // 病名栏恰三态(空/疑似/确定)
            Assert.AreEqual(3, Enum.GetValues(typeof(JudgmentState)).Length, "JudgmentState 恰三态");
            Assert.AreEqual(JudgmentState.Empty, (JudgmentState)0, "空 = 首值(合法终态)");
            // 无「不可用/把握不足/第三档置信度」形态成员
            string[] names = Enum.GetNames(typeof(ReadingForm))
                .Concat(Enum.GetNames(typeof(JudgmentState))).ToArray();
            Assert.IsFalse(names.Any(n =>
                    n.Contains("Unsure") || n.Contains("Unavailable") || n.Contains("Confidence")
                    || n.Contains("Locked") || n.Contains("Unchecked")),
                "不存在第四态/不可用态/置信度档(裁定⑨)");
            // 五值数据面完整投影到四形态(无未定义值)
            foreach (SignReadState s in Enum.GetValues(typeof(SignReadState)))
                Assert.DoesNotThrow(() => DiagnosisReadingFsm.FormOf(s), $"投影须全覆盖:{s}");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:快照冻结 + 持续刷新(AC-8-25)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_25_snapshotFrozen_continuousFollowsCurrentTick()
        {
            // Arrange:Progress 随 tick 变化 —— 取数口是 tick 的函数
            ReadingAtTick source = t => t < 20 ? Negative("胸清") : Positive("红疹");
            long completion = 10; // 动作在 tick 10 完成

            // Act:取快照后,9 的 Progress 变(tick 20 起阳性)
            ReadingSnapshot snap = DiagnosisSnapshotSampler.SampleOnCompletion(completion, source);
            DiagnosisReadFloorEvaluator.Outcome later = DiagnosisSnapshotSampler.RefreshContinuous(30, source);

            // Assert:快照逐字不变(词/态/采样 tick),持续型跟当前 tick
            Assert.AreEqual(Negative().DisplayWord, snap.DisplayWord, "快照词冻结");
            Assert.AreEqual(SignReadState.Negative, snap.State, "快照态冻结");
            Assert.AreEqual(completion, snap.SampledAt, "快照锚在完成 tick");
            Assert.AreEqual(SignReadState.Positive, later.State, "持续型跟随当前 tick");
            Assert.AreEqual(Positive().DisplayWord, later.DisplayWord, "持续型词随 Progress 变");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:完成 tick 逐位一致(AC-8-48)——「两进程」结构面
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_48_sampleAtCompletionTick_twoIndependentRuns_bitIdentical()
        {
            // Arrange:动作 start=10、中段 20 变一次、完成 25 又变一次(三段源)——
            // completion 落在段边界,中段词与完成词**必不同**(M-5:两段源对「≠ 中段」零判别)
            ReadingAtTick source = t => t < 20 ? Negative("初词") : t < 25 ? Positive("变词") : Negative("终词");
            long start = 10, mid = 22, completion = 25;

            // Act:两个独立「进程」(独立调用)各自完成采样
            ReadingSnapshot runA = DiagnosisSnapshotSampler.SampleOnCompletion(completion, source);
            ReadingSnapshot runB = DiagnosisSnapshotSampler.SampleOnCompletion(completion, source);

            // Assert:两跑逐字相同,且 ≡ 完成 tick 的值(≠ 开始、≠ 中段、≠ 完成前一刻)
            Assert.AreEqual(runA.DisplayWord, runB.DisplayWord, "两进程词逐位相同");
            Assert.AreEqual(runA.State, runB.State, "两进程态相同");
            Assert.AreEqual(source(completion).DisplayWord, runA.DisplayWord,
                "快照 ≡ 完成 tick 值(非开始 tick、非中段变化后值)");
            Assert.AreEqual(completion, runA.SampledAt, "采样时刻 = completion_tick");
            Assert.AreNotEqual(source(start).DisplayWord, runA.DisplayWord,
                "不是按开始 tick 采样(采样时刻不定死即红)");
            Assert.AreNotEqual(source(mid).DisplayWord, runA.DisplayWord,
                "不是按中段 tick 采样(三段源:中段词必异于完成词)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:幂等旧态(AC-8-27)—— 3 个 transition ⇒ 一个旧,零重复提示
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_27_threeTransitions_staleAppliedOnce_cueGateFiresOnce()
        {
            // Arrange
            var state = SignReadState.Positive;

            // Act:连续投递 3 个 threshold_transition
            ReadingTransition t1 = DiagnosisReadingFsm.OnThresholdTransition(state);
            ReadingTransition t2 = DiagnosisReadingFsm.OnThresholdTransition(t1.State);
            ReadingTransition t3 = DiagnosisReadingFsm.OnThresholdTransition(t2.State);

            // Assert:仍是一个「旧」
            Assert.AreEqual(SignReadState.Stale, t3.State, "三投后仍 Stale(无第四态)");
            int staleCount = new[] { t1, t2, t3 }.Count(x => x.StaleApplied);
            Assert.AreEqual(1, staleCount, "旧计数恰 1(不叠加标记)");
            int cueGateCount = new[] { t1, t2, t3 }.Count(x => x.Changed);
            Assert.AreEqual(1, cueGateCount,
                "cue/音效触发门恰 1 次(AC-8-27 不重复播放 —— 联动 AC-44-09 零 sting)");

            // 零音频成员(8 侧无 audio/sting 可调 —— 机器侧断言)
            List<string> audioHits = ScanMemberNamesForTokens(FourTypes, "audio", "sting", "clip");
            Assert.IsEmpty(audioHits, "8 的状态机类型零音频成员(音效触发只经 Changed 门交 44)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:窗口托底(S-8.2)—— 合成 RECHECK_WINDOW=5:4/5 抑制、6 标旧
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_s82_recheckWindow_suppressAt4And5_expireAt6()
        {
            long lastRecheck = 0; // 上次重查 tick(39 传入 —— 8 不存)
            var settled = SignReadState.Positive;

            // 托底:当前 − 上次重查 > 5 ⇒ 标旧
            ReadingTransition w4 = DiagnosisReadingFsm.EvaluateWindows(
                settled, 4, snapshotTick: 0, lastRecheck, StaleWindow, RecheckWindow);
            ReadingTransition w5 = DiagnosisReadingFsm.EvaluateWindows(
                settled, 5, snapshotTick: 0, lastRecheck, StaleWindow, RecheckWindow);
            ReadingTransition w6 = DiagnosisReadingFsm.EvaluateWindows(
                settled, 6, snapshotTick: 0, lastRecheck, StaleWindow, RecheckWindow);
            Assert.AreEqual(SignReadState.Positive, w4.State, "4 tick(≤5):未到期,保持");
            Assert.IsFalse(w4.Changed);
            Assert.AreEqual(SignReadState.Positive, w5.State, "5 tick(=窗口,边界含):保持");
            Assert.IsFalse(w5.Changed);
            Assert.AreEqual(SignReadState.Stale, w6.State, "6 tick(>5):标旧");
            Assert.IsTrue(w6.StaleApplied, "标旧恰一次");

            // 复查抑制:窗口内(4/5)复查不产出新读数;窗口过(6)解除
            ReadingTransition e4 = DiagnosisReadingFsm.OnExaminationCompleted(
                settled, Positive(), channelAccessible: true, completionTick: 4, lastRecheck, RecheckWindow);
            ReadingTransition e5 = DiagnosisReadingFsm.OnExaminationCompleted(
                settled, Positive(), channelAccessible: true, completionTick: 5, lastRecheck, RecheckWindow);
            ReadingTransition e6 = DiagnosisReadingFsm.OnExaminationCompleted(
                settled, Positive(), channelAccessible: true, completionTick: 6, lastRecheck, RecheckWindow);
            Assert.IsFalse(e4.ProducedNewOutcome, "4 tick 复查:无新读数");
            Assert.AreEqual(settled, e4.State, "4 tick 复查:读数原样");
            Assert.IsFalse(e5.ProducedNewOutcome, "5 tick 复查:无新读数(边界含)");
            Assert.IsTrue(e6.ProducedNewOutcome, "6 tick 复查:抑制解除,产新读数");

            // 首查(lastRecheck = null)永不被抑制
            ReadingTransition first = DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Blank, Positive(), channelAccessible: true,
                completionTick: 3, lastRecheckTick: null, RecheckWindow);
            Assert.IsTrue(first.ProducedNewOutcome, "首查无上次重查 ⇒ 必产读数");

            // STALE_WINDOW 到期(快照过期)⇒ 旧
            ReadingTransition st = DiagnosisReadingFsm.EvaluateWindows(
                settled, currentTick: 101, snapshotTick: 0, lastRecheckTick: null,
                staleWindow: StaleWindow, recheckWindow: RecheckWindow);
            Assert.AreEqual(SignReadState.Stale, st.State, "快照过 STALE_WINDOW ⇒ 旧");
            // 已旧再托底 ⇒ 幂等(不重复标)
            ReadingTransition stAgain = DiagnosisReadingFsm.EvaluateWindows(
                SignReadState.Stale, 200, 0, null, StaleWindow, RecheckWindow);
            Assert.IsFalse(stAgain.StaleApplied, "已旧再托底:幂等");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:不踩刹车(AC-8-28)+ 零反馈(AC-8-30)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_28_noBrake_bothPathsAllowed_zeroFeedbackMembers()
        {
            // 路径 1:零查体直接落笔(空 → 确定)—— 不校验「查够」
            JudgmentTransition direct = DiagnosisJudgmentFsm.OnWriteIntent(
                JudgmentCursor.Fresh, JudgmentState.Confirmed);
            Assert.IsTrue(direct.Changed, "零查体直接落笔:允许");
            Assert.IsFalse(direct.Rejected, "无拒绝 = 无刹车");

            // 路径 2:全查体不落笔 —— 「不校验查够」的**签名面**(M-4:原恒真断言废除):
            // 判断 FSM 公开 API 恰三(OnWriteIntent/Freeze/CloseCase)⇒ 结构上不存在
            // 「查够校验 / 催办 / 自动填写」入口;零查体直接落笔即合法(空 = 合法终态)。
            var apiNames = typeof(DiagnosisJudgmentFsm)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "OnWriteIntent", "Freeze", "CloseCase" }, apiNames,
                "判断 FSM 公开 API 恰三 —— 零催办 / 零自动填写 / 零查够校验入口");
            // ⚠️ token 集**不含 "reading"**:CloseCase(judgment, reading) 的 reading 是
            // 关病例时刻的**状态入参**(SignReadState),非查体计数 —— 语义正当,不入刹车 token。
            List<string> brakeHits = ScanMethodParametersForTokens(
                typeof(DiagnosisJudgmentFsm), "exam", "count", "checked", "enough", "progress");
            Assert.IsEmpty(brakeHits, "落笔意图零查体计数参数(不校验「查够」——签名面)");

            // 零提示事件:意图/结果结构零 feedback/toast/prompt/hint 字段(共用机器)
            List<string> feedbackHits = ScanMemberNamesForTokens(
                ContractTypes, "feedback", "toast", "prompt", "hint", "warn", "error");
            Assert.IsEmpty(feedbackHits, "AC-8-28/30:契约结构零提示字段(8 不催、不报错)");

            // S-5 补强:AC-8-30 参数面 —— 判断/读数 FSM 零「对错真源」参数
            // (防未来给 OnWriteIntent 增 isCorrect 类参数 ⇒ 现扫描不红的缺口)
            var truthHits = ScanMethodParametersForTokens(typeof(DiagnosisJudgmentFsm),
                "correct", "truth", "answer", "match", "truedisease")
                .Concat(ScanMethodParametersForTokens(typeof(DiagnosisReadingFsm),
                "correct", "truth", "answer", "match", "truedisease")).ToList();
            Assert.IsEmpty(truthHits, "AC-8-30:两 FSM 零对错真源参数(误诊报不了错 —— 结构排除)");
            var shadowTruth = ScanMethodParametersForTokens(
                typeof(ShadowJudgeWithTruthInput), "correct");
            Assert.IsNotEmpty(shadowTruth, "负夹具须命中(真源参数扫描判别力自证)");
            StringAssert.Contains("correct", string.Join(",", shadowTruth));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-29:改写留痕且痕不计分(反射 —— 契约无「改写次数」字段)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_29_rewriteLeavesMark_butContractHasNoRewriteCountField()
        {
            // 改写留痕:已落笔改口 ⇒ Rewritten = true(痕的触发标记)
            JudgmentTransition rewrite = DiagnosisJudgmentFsm.OnWriteIntent(
                new JudgmentCursor(JudgmentState.Suspected, false), JudgmentState.Confirmed);
            Assert.IsTrue(rewrite.Rewritten, "改写留痕(Rewritten 触发标记)");
            Assert.IsTrue(rewrite.Changed);

            // 首落笔不是改写
            JudgmentTransition firstWrite = DiagnosisJudgmentFsm.OnWriteIntent(
                JudgmentCursor.Fresh, JudgmentState.Suspected);
            Assert.IsFalse(firstWrite.Rewritten, "首落笔无痕");

            // 痕不计分:契约结构零改写次数字段(共用扫描机器)
            List<string> countHits = ScanMemberNamesForTokens(
                ContractTypes, "rewritecount", "times", "attempts", "score", "grade");
            Assert.IsEmpty(countHits, "AC-8-29:8 与 #53 的输入契约无「改写次数」字段");

            // 负夹具:影子契约注入 RewriteCount ⇒ 同一台机器必红(点名)
            var shadowHits = ScanMemberNamesForTokens(
                new[] { typeof(ShadowJudgmentContractWithRewriteCount) }, "rewritecount");
            Assert.IsNotEmpty(shadowHits, "负夹具须命中(机器判别力自证)");
            StringAssert.Contains("RewriteCount", string.Join(",", shadowHits));
        }

        private sealed class ShadowJudgmentContractWithRewriteCount
        {
#pragma warning disable 0649 // 影子字段仅反射扫描用
            private int RewriteCount; // 负夹具注入形态
#pragma warning restore 0649
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:关病例(AC-8-47)—— 全态清除 + 冻结 + 交 #53 + 次序
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_47_caseClosed_clearsReading_freezesJudgment_handoffThenWriteRejected()
        {
            // Arrange:读数在阳性、判断在疑似
            var reading = SignReadState.Positive;
            var cursor = new JudgmentCursor(JudgmentState.Suspected, false);

            // Act:关病例(三步次序物化:清读数 → 冻判断 → 交 #53)
            CaseCloseResult r = DiagnosisJudgmentFsm.CloseCase(cursor, reading);

            // Assert 三半边
            Assert.AreEqual(SignReadState.Blank, r.ClearedState,
                "① 读数全状态清除(任意状态 ⇒ 空行)");
            Assert.IsTrue(r.FrozenJudgment.Frozen, "② 判断冻结");
            Assert.AreEqual(JudgmentState.Suspected, r.FrozenJudgment.State,
                "冻结保留状态(交 #53 前不丢)");
            Assert.AreEqual(JudgmentState.Suspected, r.HandoffTo53,
                "③ 交 #53 = 冻结时刻的判断状态");

            // 冻结后仍可写 ⇒ 竞态断言:落笔必拒(次序的可执行半边)
            JudgmentTransition after = DiagnosisJudgmentFsm.OnWriteIntent(
                r.FrozenJudgment, JudgmentState.Confirmed);
            Assert.IsTrue(after.Rejected, "冻结后落笔被拒(避免「冻结后仍可写」)");
            Assert.AreEqual(JudgmentState.Suspected, after.State, "被拒不改状态");

            // 读数清除对任意状态成立
            foreach (SignReadState s in Enum.GetValues(typeof(SignReadState)))
                Assert.AreEqual(SignReadState.Blank, DiagnosisReadingFsm.OnCaseClosed(s),
                    $"任意读数状态关病例 ⇒ 清除({s})");

            // 8 不记录死亡原因、不持死亡标志(反射:本 story 类型零死亡成员)
            List<string> deathHits = ScanMemberNamesForTokens(FourTypes, "death", "dead", "died", "cause");
            Assert.IsEmpty(deathHits, "AC-8-47:8 不持死亡标志/死亡原因");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:成长门控(铁律④)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_iron4_growthGate_clientRejectedZeroSideEffect_hostEmitsExactlyOnce()
        {
            var client = new FakeAuthority(isHost: false);
            var host = new FakeAuthority(isHost: true);

            // 客户端:拒绝 + 零副作用。判别力:用**非法参数**(出口会 throw)——
            // 若实现放过客户端走到出口,这里必抛异常 ⇒ 不抛 = 真没调出口
            bool clientOk = false;
            SkillGrownPayload clientPayload = default;
            Assert.DoesNotThrow(() =>
            {
                clientOk = DiagnosisGrowthGate.TryEmitGrowth(
                    client, actorId: -1, skillId: 0, objectId: 0,
                    NoveltyClass.First, level: null, currentTick: -1, PatientId.None,
                    out clientPayload);
            }, "客户端路径不得触达出口(出口会因非法参数抛)");
            Assert.IsFalse(clientOk, "客户端被拒");
            Assert.AreEqual(default(SkillGrownPayload), clientPayload,
                "客户端 payload = default(零副作用)");

            // 主机:恰一次放行,载荷与唯一出口逐字一致。
            // M-10 登记:「恰一次」以「载荷 = 直调出口」代理 —— 出口是纯函数(零字段,
            // 反射已守),双调在载荷上不可观测;真调用计数归 IL 侧 [D-EXIT-GATE](唯一调用方)。
            bool hostOk = DiagnosisGrowthGate.TryEmitGrowth(
                host, actorId: 0, skillId: 3, objectId: 1,
                NoveltyClass.Normal, level: 7, currentTick: 42, PatientId.None,
                out SkillGrownPayload payload);
            Assert.IsTrue(hostOk, "主机放行");
            SkillGrownPayload expected = DiagnosisGrowthExit.EmitGrowth(
                0, 3, 1, NoveltyClass.Normal, 7, 42, PatientId.None);
            Assert.AreEqual(expected, payload, "主机载荷 = 唯一出口直调(逐字一致)");

            // null authority ⇒ 拒
            Assert.IsFalse(DiagnosisGrowthGate.TryEmitGrowth(
                null, 0, 0, 0, NoveltyClass.First, null, 0, PatientId.None, out _),
                "无 authority ⇒ 拒");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:联机不回写(AC-8-45,in-process 结构面;网络子句 NOT-RUN)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_45_dualClient_differentSkill_snapshotsDiffer_zeroWriteback()
        {
            // 双端 Skill 5 / 50 ⇒ 同一病人同动作读到精度不同(读数是个人的)
            ReadingAtTick lowSkill = _ => new DiagnosisReadFloorEvaluator.Outcome(
                SignReadState.UnreadableNegative, "说不清", false);
            ReadingAtTick highSkill = _ => new DiagnosisReadFloorEvaluator.Outcome(
                SignReadState.Positive, "细湿啰音", true);

            ReadingSnapshot snapLow = DiagnosisSnapshotSampler.SampleOnCompletion(10, lowSkill);
            ReadingSnapshot snapHigh = DiagnosisSnapshotSampler.SampleOnCompletion(10, highSkill);

            Assert.AreNotEqual(snapLow.DisplayWord, snapHigh.DisplayWord,
                "两端快照不同(Skill 5/50 精度差)");
            Assert.AreNotEqual(snapLow.State, snapHigh.State, "两端读数态可不同");

            // 读数分歧不回写(铁律②)—— M-1 修复:原「双端 FakeEventLog 哈希」从未接给
            // 被测代码(恒真死胡同),改**类型面扫描**(共用机器 + 影子负夹具):
            // 四类型零写流入口名;真守门 = [D-TREF]/[D-PUB] IL 门按 8 前缀自动覆盖(149 绿即证)。
            List<string> writeHits = ScanMemberNamesForTokens(
                FourTypes, "append", "publish", "sink", "eventstream");
            Assert.IsEmpty(writeHits, "8 的四类型零写流入口名(读数分歧不回写;IL 门另守调用面)");
            List<string> shadowWrite = ScanMemberNamesForTokens(
                new[] { typeof(ShadowWithWriteApi) }, "append");
            Assert.IsNotEmpty(shadowWrite, "负夹具须命中(写流名字扫描判别力自证)");
            StringAssert.Contains("Append", string.Join(",", shadowWrite));

            // 双端各跑完成 + 落笔流程 —— 产意图不产写入(意图为返回值,无 sink 可写)
            DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Blank, Positive(), channelAccessible: true, 10, null, RecheckWindow);
            DiagnosisJudgmentFsm.OnWriteIntent(JudgmentCursor.Fresh, JudgmentState.Confirmed);
            DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Blank, highSkill(10), channelAccessible: true, 10, null, RecheckWindow);
            DiagnosisJudgmentFsm.OnWriteIntent(JudgmentCursor.Fresh, JudgmentState.Suspected);
            // ⚠️ 真网络传输子句 NOT-RUN(BLOCKED-BY-45 epic,卡面登记)—— 本测不冒充。
        }

        private sealed class ShadowWithWriteApi
        {
            public void Append(string e) { } // 影子注入形态(负夹具)
        }

        // ══════════════════════════════════════════════════════════════════════════
        // AC-8-46:已写病名不回退 —— 写路径签名零 Skill 参数
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_46_writePath_neverTakesSkill_parameterScan()
        {
            // 共用机器:判断 FSM 公开方法参数零 skill/token —— 掉级在结构上进不来
            List<string> hits = ScanMethodParametersForTokens(typeof(DiagnosisJudgmentFsm),
                "skill", "level", "rank", "grade");
            Assert.IsEmpty(hits, "AC-8-46:落笔/冻结/关病例签名零 Skill 参数(不回退已写病名)");

            // 负夹具:影子**静态**方法带 skill 参数 ⇒ **同一台共用机器**命中(点名)——
            // M-2 修复:原手搓循环与机器脱钩,机器被改坏(恒空)时正负双绿、判别力归零。
            List<string> shadowHits = ScanMethodParametersForTokens(typeof(ShadowWriter), "skill");
            Assert.IsNotEmpty(shadowHits, "负夹具须命中(机器判别力自证 —— 同机)");
            StringAssert.Contains("skill", string.Join(",", shadowHits));
        }

        private sealed class ShadowWriter
        {
            public static void Write(int skill, string diagnosis) { } // static:匹配机器 BindingFlags
        }

        private sealed class ShadowJudgeWithTruthInput
        {
            // static:匹配共用机器 BindingFlags(Public|Static|DeclaredOnly)—— M-2 同根
            public static void OnWrite(bool isCorrect, string diagnosis) { }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QA:无法配合(AC-8-31)+ 潜伏期合法输出(AC-8-49)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_ac8_31_inaccessibleChannel_forcedBlank_accessNeverSkillLocked()
        {
            // 无法配合(昏迷/剧痛/小儿抗拒 —— 9/13 标志)⇒ 通道恒空行(回溯抹除,S-2 已登记)
            ReadingTransition blocked = DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Positive, Positive(), channelAccessible: false, 0, null, RecheckWindow);
            Assert.AreEqual(SignReadState.Blank, blocked.State, "无法配合 ⇒ 未查(空行)");
            Assert.AreEqual(ReadingForm.Pending, DiagnosisReadingFsm.FormOf(blocked.State));
            Assert.IsFalse(blocked.ProducedNewOutcome,
                "不可得优先于窗口与读数 —— S-1 单一入口:不产读数、不看 outcome");

            // 可得 ⇒ 求值结果原样(含已查读不出 —— 不是「查不了」)
            ReadingTransition ok = DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Blank, Unreadable(), channelAccessible: true, 0, null, RecheckWindow);
            Assert.AreEqual(SignReadState.UnreadableNegative, ok.State, "读不出 ≠ 查不了");

            // 任何通道不因等级而不可用:完成入口签名零 skill 参数(**共用机器**,
            // M-2 同批收编原内联扫描)—— AC-8-9 两向互证:手段永不上锁
            List<string> skillHits = ScanMethodParametersForTokens(typeof(DiagnosisReadingFsm), "skill");
            Assert.IsEmpty(skillHits, "读数 FSM 全公开方法零 Skill 参数(等级锁不进通道)");
        }

        [Test]
        public void test_ac8_49_latencyNegativeOutput_isLegalNotErrorState()
        {
            // 潜伏期病人:8 侧全通道「已查 · 无异常」= 合法输出,非错误态
            ReadingTransition t = DiagnosisReadingFsm.OnExaminationCompleted(
                SignReadState.Blank, Negative("无异常"), channelAccessible: true, 0, null, RecheckWindow);
            Assert.AreEqual(SignReadState.Negative, t.State, "「已查 · 无异常」是合法态");
            Assert.IsTrue(t.ProducedNewOutcome, "产读数(不是错误分支)");
            Assert.AreNotEqual(SignReadState.Blank, t.State, "已查(不是未查)");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 机器自卫(承 story-004 教训:种子非空须本文件自持)
        // ══════════════════════════════════════════════════════════════════════════
        [Test]
        public void test_scanMachines_seedsNonEmpty_selfGuard()
        {
            Assert.AreEqual(4, FourTypes.Length, "扫描种子恰四类型");
            Assert.IsTrue(FourTypes.Contains(typeof(DiagnosisReadingFsm)), "种子含 ReadingFsm");
            Assert.IsNotEmpty(ContractTypes, "契约扫描种子非空");

            // 正测:干净基线(生产四类型 + 契约结构零反馈/零音频/零死亡 token)
            Assert.IsEmpty(
                ScanMemberNamesForTokens(FourTypes.Concat(ContractTypes),
                    "audio", "sting", "feedback", "toast", "death", "rewritecount"),
                "干净基线零禁 token");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // fakes
        // ══════════════════════════════════════════════════════════════════════════
        private sealed class FakeAuthority : IEventAuthority
        {
            private readonly bool _isHost;
            public FakeAuthority(bool isHost) => _isHost = isHost;

            public bool IsAuthority => _isHost;
            public bool IsHost => _isHost;
            public EventRollResult Roll(in RollRequest r) => default;
        }


    }
}
