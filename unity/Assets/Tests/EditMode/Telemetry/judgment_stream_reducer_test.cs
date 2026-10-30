// 权威来源:ADR-019 §一(回放即数据记录)· GDD F1(真值 = `CaseClosed.disease_set`;
//   J(c) = 全序最后一条判断)· R2(零埋点)· R8(纯函数:同流同值)。
//
// ⚠️ 本件是 TD-epic-01 的还债件(2026-10-19 第四片)夹具:`JudgmentStreamReducer` 的判据。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class JudgmentStreamReducerTest
    {
        /// <summary>测试用的解码替身。
        /// <para>⚠️ **支持「同 case 多次判断给不同集合」**:按调用次序返回
        /// <see cref="_judgedSeq"/> 里的下一条,否则无法区分「取首条 / 取末条」
        /// (两条给同一集合 ⇒ 该变异不可观测 —— S19 证伪轮实测到的弱面)。</para></summary>
        private sealed class FakeDecode
        {
            private readonly Dictionary<int, List<int>> _truth;
            private readonly Dictionary<int, Queue<List<int>>> _judgedSeq;
            private readonly HashSet<int> _revised;

            public FakeDecode(Dictionary<int, List<int>> truth,
                              Dictionary<int, List<int>> judged,
                              HashSet<int> revised)
            {
                _truth = truth;
                _judgedSeq = new Dictionary<int, Queue<List<int>>>();
                foreach (var kv in judged)
                    _judgedSeq[kv.Key] = new Queue<List<int>>(new[] { kv.Value });
                _revised = revised;
            }

            /// <summary>给某 case 追加第二条判断集合(用于「取末条」判据)。</summary>
            public void EnqueueJudgment(int caseId, List<int> set)
            {
                if (!_judgedSeq.TryGetValue(caseId, out Queue<List<int>> q))
                    _judgedSeq[caseId] = q = new Queue<List<int>>();
                q.Enqueue(set);
            }

            /// <remarks>
            /// ⚠️ **两处 `false` 都填 `Missing`**(`TD-epic-01-b` A 方案,2026-10-30)——
            /// 本替身是「按 caseId 查字典」,查不到 = **查无此数据**(与 `PayloadDecoder`
            /// 抛 `InvalidOperationException` 同一语义),**不是**载荷损坏。
            /// 填 `Corrupt` 会让「未驻留」与「损坏」的计数互换而夹具仍绿。
            /// </remarks>
            public bool Invoke(PayloadRef payloadRef, EventKind kind,
                               out JudgmentStreamReducer.DecodedJudgment decoded,
                               out DecodeFailureKind failure)
            {
                failure = DecodeFailureKind.None;
                int cid = (int)payloadRef.Length;
                if (kind == EventKind.CaseClosed)
                {
                    if (!_truth.TryGetValue(cid, out List<int> t))
                    {
                        failure = DecodeFailureKind.Missing;   // 查无此数据
                        decoded = default;
                        return false;
                    }
                    decoded = new JudgmentStreamReducer.DecodedJudgment(cid, t, false);
                    return true;
                }
                if (!_judgedSeq.TryGetValue(cid, out Queue<List<int>> q) || q.Count == 0)
                {
                    failure = DecodeFailureKind.Missing;       // 查无此数据
                    decoded = default;
                    return false;
                }
                List<int> set = q.Dequeue();
                bool rev = kind == EventKind.JudgmentRevised || _revised.Contains(cid);
                decoded = new JudgmentStreamReducer.DecodedJudgment(cid, set, rev);
                return true;
            }
        }

        private static SimEvent Ev(EventKind kind, int caseId)
            => new SimEvent(100, new PatientId(caseId), 0, kind, new PayloadRef(0, 0, caseId));

        // ══════════════ 还原:真值 + 最后一条判断 ══════════════

        /// <summary>基本还原:CaseClosed 出真值,JudgmentRecorded 出 J(c),一对一进 Scorable。</summary>
        [Test]
        public void test_reduce_truthAndLastJudgment_paired()
        {
            var truth = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var judged = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var dec = new FakeDecode(truth, judged, new HashSet<int>());

            var records = JudgmentStreamReducer.Reduce(
                new[] { Ev(EventKind.CaseClosed, 1), Ev(EventKind.JudgmentRecorded, 1) },
                dec.Invoke, out SkipBreakdown skips);

            Assert.That(records.Count, Is.EqualTo(1), "一案一记录");
            Assert.That(skips.Total, Is.EqualTo(0));
            Assert.That(records[0].Judged.SetEquals(new HashSet<string> { "7" }), Is.True, "J(c) = {7}");
            Assert.That(records[0].Truth.SetEquals(new HashSet<string> { "7" }), Is.True, "D(c) = {7}");
        }

        /// <summary>**全序取值**(核心判据):同 case 多条判断 ⇒ 取**最后一条**。
        /// <para>夹具给两条**不同**集合:先判 {7}、后判 {9} ⇒ J(c) 必须是 {9}。
        /// 若实现取首条 / 求和 / 取并集,本条红。</para>
        /// <para>⚠️ 首版夹具两条给同一集合 ⇒ 「取首条」变异**不可观测**(证伪轮实测弱面);
        /// 故改为不同集合 —— 这是修夹具,不是迁就变异。</para></summary>
        [Test]
        public void test_reduce_multipleJudgments_takesLastInOrder()
        {
            var truth = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var judged = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var dec = new FakeDecode(truth, judged, new HashSet<int>());
            dec.EnqueueJudgment(1, new List<int> { 9 });   // 第二条判断 = {9}(全序在后)

            var records = JudgmentStreamReducer.Reduce(
                new[]
                {
                    Ev(EventKind.CaseClosed, 1),
                    Ev(EventKind.JudgmentRecorded, 1),   // 第一条 ⇒ {7}
                    Ev(EventKind.JudgmentRevised, 1),    // 第二条 ⇒ {9}(全序在后,应胜出)
                },
                dec.Invoke, out SkipBreakdown skips);

            Assert.That(records.Count, Is.EqualTo(1), "三条事件仍归一案一记录");
            Assert.That(skips.Total, Is.EqualTo(0));
            Assert.That(records[0].Judged.Count, Is.EqualTo(1), "J(c) 单条");
            Assert.That(records[0].Judged.Contains("9"), Is.True,
                "J(c) 须取**全序最后一条** = {9}(取首条会得 {7} ⇒ 红)");
            Assert.That(records[0].HasRevision, Is.True, "末条是 JudgmentRevised ⇒ 标记改写");
        }

        /// <summary>**Scorable 语义**:只有真值无判断的 case **不进分母**(未落笔不可打分)。</summary>
        [Test]
        public void test_reduce_noJudgment_notScorable()
        {
            var truth = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var dec = new FakeDecode(truth, new Dictionary<int, List<int>>(), new HashSet<int>());

            var records = JudgmentStreamReducer.Reduce(
                new[] { Ev(EventKind.CaseClosed, 1) }, dec.Invoke, out SkipBreakdown skips);
            Assert.That(records.Count, Is.EqualTo(0), "无判断 ⇒ 不可打分,不进分母");
            Assert.That(skips.Total, Is.EqualTo(0));
        }

        /// <summary>有真值、判断为空集 ⇒ **仍进分母且 J = ∅**(判错,不是不可打分)。
        /// 与上一条合起来钉死「未落笔」与「落笔判错」是两件不同的事。</summary>
        [Test]
        public void test_reduce_emptyJudgment_isScorableAndWrong()
        {
            var truth = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var judged = new Dictionary<int, List<int>> { { 1, new List<int>() } };
            var dec = new FakeDecode(truth, judged, new HashSet<int>());

            var records = JudgmentStreamReducer.Reduce(
                new[] { Ev(EventKind.CaseClosed, 1), Ev(EventKind.JudgmentRecorded, 1) },
                dec.Invoke, out _);

            Assert.That(records.Count, Is.EqualTo(1), "落笔(空集)仍可打分");
            Assert.That(records[0].Judged.Count, Is.EqualTo(0), "J(c) = ∅");
            Assert.That(records[0].Truth.Count, Is.EqualTo(1), "D(c) = {7}");
        }

        /// <summary>解码失败 ⇒ 计数且跳过(**不静默吞**;ADR-019 §一 的「重算可审计」要求)。</summary>
        [Test]
        public void test_reduce_decodeFailure_countedAndSkipped()
        {
            var truth = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var judged = new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } };
            var dec = new FakeDecode(truth, judged, new HashSet<int>());

            // case 2 无载荷 ⇒ 解码失败
            var records = JudgmentStreamReducer.Reduce(
                new[]
                {
                    Ev(EventKind.CaseClosed, 1), Ev(EventKind.JudgmentRecorded, 1),
                    Ev(EventKind.CaseClosed, 2),
                },
                dec.Invoke, out SkipBreakdown skips);

            Assert.That(skips.Total, Is.EqualTo(1), "解码失败须计数(可观测)");
            Assert.That(records.Count, Is.EqualTo(1), "失败的 case 不进结果");
            // `TD-epic-01-b` A 方案:替身的两处 false 都是「查无」⇒ 须落 Missing,不是 Corrupt。
            Assert.That(skips.Missing, Is.EqualTo(1), "查无此数据 ⇒ Missing(不是 Corrupt)");
            Assert.That(skips.Corrupt, Is.EqualTo(0));
        }

        /// <summary>
        /// **`TD-epic-01-b` 的核心判据**:跳过**按类别分开计** —— 「未驻留」与「损坏」不再是同一个数。
        /// <para>⚠️ <b>为什么这条必须有</b>:本条债的原判定逐字是「`skipped` 只在报告里给个数,
        /// **不区分**『载荷未驻留 / 损坏 / 类型错配』」。若只加字段不加这条判据,
        /// 「分类」就只是**一个没被验证的字段** —— 与「自述顶替门」同型。</para>
        /// <para><b>判别力</b>:让替身对两种失败各报一类,断言两个计数**各自**为 1。
        /// 若还原器把 `why` 忽略(全按 `Plus(None)` 或全按一类),两个计数会失衡 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_reduce_skipBreakdown_separatesMissingFromCorrupt()
        {
            // 两案:一案「查无」(Missing)、一案「损坏」(Corrupt)。
            var dec = new TwoFailureDecode(missingCaseId: 1, corruptCaseId: 2);

            var records = JudgmentStreamReducer.Reduce(
                new[]
                {
                    Ev(EventKind.CaseClosed, 1),        // ⇒ Missing
                    Ev(EventKind.CaseClosed, 2),        // ⇒ Corrupt
                    Ev(EventKind.CaseClosed, 3),        // ⇒ 正常解出(真值)
                    Ev(EventKind.JudgmentRecorded, 3),  // ⇒ 正常解出(判断)
                },
                dec.Invoke, out SkipBreakdown skips);

            Assert.That(skips.Missing, Is.EqualTo(1), "一案未驻留 ⇒ Missing = 1");
            Assert.That(skips.Corrupt, Is.EqualTo(1), "一案损坏 ⇒ Corrupt = 1");
            Assert.That(skips.Total, Is.EqualTo(2), "总数 = 两类之和");
            // ⚠️ Scorable = 有真值 **且** 有判断(GDD F1 逐字)⇒ 第三案须两件都入流才进结果。
            Assert.That(records.Count, Is.EqualTo(1), "只有第三案进结果");
        }

        /// <summary>`Describe()` 的文案 —— 零跳过时不渲染两个 0(理由见 `SkipBreakdown.Describe`)。</summary>
        [Test]
        public void test_skipBreakdown_describe_zeroIsNotRenderedAsTwoZeros()
        {
            Assert.That(default(SkipBreakdown).Describe(), Is.EqualTo("无跳过"),
                "零跳过 ⇒ 「无跳过」,不是「跳过 0(未驻留 0 / 损坏 0)」");
            Assert.That(new SkipBreakdown(2, 3).Describe(),
                Is.EqualTo("跳过 5(未驻留 2 / 损坏 3)"), "非零 ⇒ 逐类点名");
        }

        /// <summary>`DecodeFailure.Classify` 的映射 —— 唯一映射处,必须钉住。</summary>
        [Test]
        public void test_decodeFailure_classify_operationIsMissing_othersAreCorrupt()
        {
            Assert.That(DecodeFailure.Classify(new InvalidOperationException("x")),
                Is.EqualTo(DecodeFailureKind.Missing), "未驻留/越界 ⇒ Missing");
            Assert.That(DecodeFailure.Classify(new System.IO.InvalidDataException("x")),
                Is.EqualTo(DecodeFailureKind.Corrupt), "schema 损坏 ⇒ Corrupt");
            Assert.That(DecodeFailure.Classify(new ArgumentNullException("x")),
                Is.EqualTo(DecodeFailureKind.Corrupt), "其它异常兜底 ⇒ Corrupt(宁可多报可疑)");
            Assert.That(DecodeFailure.Classify(null),
                Is.EqualTo(DecodeFailureKind.Corrupt), "null 不该发生,但按可疑计");
        }

        /// <summary>「一案 Missing / 一案 Corrupt」的替身(`TD-epic-01-b` 分类判据专用)。</summary>
        private sealed class TwoFailureDecode
        {
            private readonly int _missing;
            private readonly int _corrupt;

            public TwoFailureDecode(int missingCaseId, int corruptCaseId)
            {
                _missing = missingCaseId; _corrupt = corruptCaseId;
            }

            public bool Invoke(PayloadRef payloadRef, EventKind kind,
                               out JudgmentStreamReducer.DecodedJudgment decoded,
                               out DecodeFailureKind failure)
            {
                failure = DecodeFailureKind.None;
                int cid = (int)payloadRef.Length;
                if (cid == _missing)
                {
                    failure = DecodeFailureKind.Missing;
                    decoded = default;
                    return false;
                }
                if (cid == _corrupt)
                {
                    failure = DecodeFailureKind.Corrupt;
                    decoded = default;
                    return false;
                }
                decoded = new JudgmentStreamReducer.DecodedJudgment(
                    cid, new List<int> { 7 }, false);
                return true;
            }
        }

        /// <summary>空流 ⇒ 空序列、0 跳过(不抛)。</summary>
        [Test]
        public void test_reduce_emptyStream_emptyResult()
        {
            var dec = new FakeDecode(new Dictionary<int, List<int>>(),
                                     new Dictionary<int, List<int>>(), new HashSet<int>());
            var records = JudgmentStreamReducer.Reduce(
                new List<SimEvent>(), dec.Invoke, out SkipBreakdown skips);
            Assert.That(records.Count, Is.EqualTo(0));
            Assert.That(skips.Total, Is.EqualTo(0));
        }

        /// <summary>**端到端**:还原器 + `JudgmentMetrics` 合起来算 F1a(51 的完整链路自此通)。</summary>
        [Test]
        public void test_reduce_thenMetrics_f1aEndToEnd()
        {
            // 一案判对({7}=={7})、一案判错({9} vs {7})⇒ F1a = 1/2
            var truth = new Dictionary<int, List<int>>
                { { 1, new List<int> { 7 } }, { 2, new List<int> { 7 } } };
            var judged = new Dictionary<int, List<int>>
                { { 1, new List<int> { 7 } }, { 2, new List<int> { 9 } } };
            var dec = new FakeDecode(truth, judged, new HashSet<int>());

            var records = JudgmentStreamReducer.Reduce(
                new[]
                {
                    Ev(EventKind.CaseClosed, 1), Ev(EventKind.JudgmentRecorded, 1),
                    Ev(EventKind.CaseClosed, 2), Ev(EventKind.JudgmentRecorded, 2),
                },
                dec.Invoke, out SkipBreakdown skips);
            Assert.That(skips.Total, Is.EqualTo(0));

            JudgmentMetrics.Result r = JudgmentMetrics.Compute(records);
            Assert.That(r.F1aNum, Is.EqualTo(1), "一案判对");
            Assert.That(r.F1aDen, Is.EqualTo(2), "两案可打分");
            Assert.That(r.DenominatorZero, Is.False);
        }
    }
}
