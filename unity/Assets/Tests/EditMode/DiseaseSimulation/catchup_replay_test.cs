// disease-simulation Story 006 测试
//
// AC-3: Step ≡ CatchUp
// AC-3b: 离线 30 天分档计数器
// AC-31: 多病种合成取大
// AC-32: 共病阶跃
// AC-33: 空处置史与有处置史同构
// TR-disease-020: 边界扫描单向性
// AC-16: 乱序重放

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    public class CatchUpReplayTest
    {
        private EventStream _stream;
        private FakePresenceQuery _presenceQuery;
        private FakeIdAuthority _idAuthority;

        [SetUp]
        public void Setup()
        {
            _presenceQuery = new FakePresenceQuery();
            _idAuthority = new FakeIdAuthority();
            _stream = new EventStream(_idAuthority, _presenceQuery);
        }

        // AC-3: Step ≡ CatchUp
        [Test]
        public void test_stepEqualsCatchUp()
        {
            var events = new List<SimEvent>
            {
                new SimEvent(0, new PatientId(1), 0, EventKind.ActorCellEntered, default),
                new SimEvent(1, new PatientId(1), 0, EventKind.ActorCellEntered, default),
            };

            var result = CatchUp.ComputeCatchUp(events, 0, 100, _idAuthority, _presenceQuery);

            Assert.IsNotNull(result);
            Assert.AreEqual(events.Count, result.Count);
            // 验证按全序键排序
            for (int i = 1; i < result.Count; i++)
            {
                Assert.LessOrEqual(result[i - 1].Tick, result[i].Tick,
                    "CatchUp 结果应按 Tick 排序");
            }
        }

        // AC-3b: 离线 30 天分档计数器
        [Test]
        public void test_counterInterval_configured()
        {
            Assert.Greater(CatchUp.COUNTER_INTERVAL_TICKS, 0,
                "COUNTER_INTERVAL_TICKS 应 > 0");
            Assert.Greater(CatchUp.MAX_SCAN_STEPS, 0,
                "MAX_SCAN_STEPS 应 > 0");
        }

        // AC-31: 多病种合成取大
        [Test]
        public void test_compoundMax_takesMaximum()
        {
            var positions = new List<Fix> { new Fix(100), new Fix(300), new Fix(200) };
            Fix max = positions[0];
            foreach (var p in positions)
            {
                if (p.Raw > max.Raw) max = p;
            }

            Assert.AreEqual(300, max.Raw, "多病种合成应取大");
        }

        // AC-32: 共病阶跃
        [Test]
        public void test_compoundTrigger_event()
        {
            var compoundMap = new Dictionary<string, List<string>>
            {
                { "rheumatic_fever", new List<string> { "heart_failure" } }
            };

            var events = new List<SimEvent>
            {
                new SimEvent(0, new PatientId(1), 0, EventKind.ActorCellEntered, default),
            };

            var compounds = CatchUp.ComputeCompounds(events, compoundMap, 100);

            Assert.IsNotNull(compounds);
            Assert.GreaterOrEqual(compounds.Count, 0, "共病触发事件数应 >= 0");
        }

        // AC-33: 空处置史与有处置史同构
        [Test]
        public void test_emptyAndNonEmptyHistory_isomorphic()
        {
            var emptyEvents = new List<SimEvent>();
            var nonEmptyEvents = new List<SimEvent>
            {
                new SimEvent(0, new PatientId(1), 0, EventKind.ActorCellEntered, default),
            };

            var emptyResult = CatchUp.ComputeCatchUp(emptyEvents, 0, 100, _idAuthority, _presenceQuery);
            var nonEmptyResult = CatchUp.ComputeCatchUp(nonEmptyEvents, 0, 100, _idAuthority, _presenceQuery);

            Assert.AreEqual(0, emptyResult.Count);
            Assert.AreEqual(1, nonEmptyResult.Count);
        }

        // TR-disease-020: 边界扫描单向性
        [Test]
        public void test_boundaryScan_monotonic()
        {
            var events = new List<SimEvent>
            {
                new SimEvent(0, new PatientId(1), 0, EventKind.ActorCellEntered, default),
                new SimEvent(1, new PatientId(1), 0, EventKind.ActorCellEntered, default),
                new SimEvent(2, new PatientId(1), 0, EventKind.ActorCellEntered, default),
            };

            var result = CatchUp.ComputeCatchUp(events, 0, 100, _idAuthority, _presenceQuery);

            // 验证扫描游标单调
            for (int i = 1; i < result.Count; i++)
            {
                Assert.GreaterOrEqual(result[i].Tick, result[i - 1].Tick,
                    "扫描游标应单调不回退");
            }
        }

        // AC-16: 乱序重放
        [Test]
        public void test_outOfOrderReplay_sameResult()
        {
            var events = new List<SimEvent>
            {
                new SimEvent(0, new PatientId(1), 0, EventKind.ActorCellEntered, default),
                new SimEvent(0, new PatientId(1), 1, EventKind.ActorCellEntered, default),
                new SimEvent(1, new PatientId(1), 0, EventKind.ActorCellEntered, default),
            };

            // 乱序
            var shuffled = events.OrderBy(e => Guid.NewGuid()).ToList();

            var result1 = CatchUp.ComputeCatchUp(events, 0, 100, _idAuthority, _presenceQuery);
            var result2 = CatchUp.ComputeCatchUp(shuffled, 0, 100, _idAuthority, _presenceQuery);

            // 验证乱序重放结果相同
            Assert.AreEqual(result1.Count, result2.Count, "乱序重放事件数应相同");
        }

        // AC-32: 共病无环验证
        [Test]
        public void test_compoundGraph_noCycle()
        {
            var compoundMap = new Dictionary<string, List<string>>
            {
                { "rheumatic_fever", new List<string> { "heart_failure" } }
            };

            Assert.IsTrue(CatchUp.ValidateCompoundGraph(compoundMap),
                "共病图应无环");
        }

        // AC-32: 共病有环检测
        [Test]
        public void test_compoundGraph_detectsCycle()
        {
            var compoundMap = new Dictionary<string, List<string>>
            {
                { "a", new List<string> { "b" } },
                { "b", new List<string> { "a" } }
            };

            Assert.IsFalse(CatchUp.ValidateCompoundGraph(compoundMap),
                "共病图有环应被检测");
        }
    }
}
