// 权威来源:ADR-019 §一(回放即数据记录)· GDD F1(真值 = `CaseClosed.disease_set`;
//   J(c) = 全序最后一条判断)· R2(零埋点)· R8(纯函数:同流同值)。
//
// ⚠️ 本件是 TD-epic-01 的还债件(2026-10-19 第四片)夹具:`JudgmentStreamReducer` 的判据。

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

            public bool Invoke(PayloadRef payloadRef, EventKind kind,
                               out JudgmentStreamReducer.DecodedJudgment decoded)
            {
                int cid = (int)payloadRef.Length;
                if (kind == EventKind.CaseClosed)
                {
                    if (!_truth.TryGetValue(cid, out List<int> t)) { decoded = default; return false; }
                    decoded = new JudgmentStreamReducer.DecodedJudgment(cid, t, false);
                    return true;
                }
                if (!_judgedSeq.TryGetValue(cid, out Queue<List<int>> q) || q.Count == 0)
                {
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
                dec.Invoke, out int skipped);

            Assert.That(records.Count, Is.EqualTo(1), "一案一记录");
            Assert.That(skipped, Is.EqualTo(0));
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
                dec.Invoke, out int skipped);

            Assert.That(records.Count, Is.EqualTo(1), "三条事件仍归一案一记录");
            Assert.That(skipped, Is.EqualTo(0));
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
                new[] { Ev(EventKind.CaseClosed, 1) }, dec.Invoke, out int skipped);
            Assert.That(records.Count, Is.EqualTo(0), "无判断 ⇒ 不可打分,不进分母");
            Assert.That(skipped, Is.EqualTo(0));
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
                dec.Invoke, out int skipped);

            Assert.That(skipped, Is.EqualTo(1), "解码失败须计数(可观测)");
            Assert.That(records.Count, Is.EqualTo(1), "失败的 case 不进结果");
        }

        /// <summary>空流 ⇒ 空序列、0 跳过(不抛)。</summary>
        [Test]
        public void test_reduce_emptyStream_emptyResult()
        {
            var dec = new FakeDecode(new Dictionary<int, List<int>>(),
                                     new Dictionary<int, List<int>>(), new HashSet<int>());
            var records = JudgmentStreamReducer.Reduce(
                new List<SimEvent>(), dec.Invoke, out int skipped);
            Assert.That(records.Count, Is.EqualTo(0));
            Assert.That(skipped, Is.EqualTo(0));
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
                dec.Invoke, out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            JudgmentMetrics.Result r = JudgmentMetrics.Compute(records);
            Assert.That(r.F1aNum, Is.EqualTo(1), "一案判对");
            Assert.That(r.F1aDen, Is.EqualTo(2), "两案可打分");
            Assert.That(r.DenominatorZero, Is.False);
        }
    }
}
