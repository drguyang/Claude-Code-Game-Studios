// 急救动作表 —— 判据夹具(T13 · 2026-11-09)
//
// 权威:`design/gdd/emergency-procedures.md` 10-DC · AC-10-16(`ResultMul` 三档)
//       · `technique-unlock-map-2026-11-09.md` §7 T7(动作 2/3 的 id 空间已开)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的六件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 行**按 `ActionId` 取**(不是「有一张表就行」);
//   ② 缺行 ⇒ **抛**,且报文点名该动作与已注册集合;
//   ③ 重复 / 负 id / null 行 ⇒ 构造期拒收(重复会让「用哪一行」取决于迭代序 = 不可重放);
//   ④ 生产默认表**只注册动作 0**,且 `ResultMul` 仍是 schema 默认表(不是副本);
//   ⑤ **端到端**:换一张表就换 potency —— 证明驱动真的按 `attempt.Action` 取行
//      (若驱动忽略表、用硬编码行,两个动作的 potency 会相同 ⇒ 红);
//   ⑥ **缺行时流零事件**(fail-fast 必须有副作用判据,只看异常类型不够)。
//
// ⚠️ 本文件零仓库文件读取、零写盘(全部内存流)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input.Intents;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    [TestFixture]
    public sealed class emergency_action_table_test
    {
        private const int Actor = 1;

        // ════════════════════════════════════════════════════════════════
        //  ① 按 id 取行
        // ════════════════════════════════════════════════════════════════

        /// <summary>同一张表里两行不同参数 ⇒ 各自取回自己那一行。</summary>
        [Test]
        public void test_require_returnsRowByActionId()
        {
            var table = new EmergencyActionTable(new[]
            {
                Row(actionId: 0, basePotency: 65536),
                Row(actionId: 9, basePotency: 32768),
            });

            Assert.That(table.Require(0).BasePotency, Is.EqualTo(65536));
            Assert.That(table.Require(9).BasePotency, Is.EqualTo(32768),
                "取行必须按 id —— 返回同一行(ex. 恒返回第一行)⇒ 红");
            Assert.That(table.RegisteredActionIds, Is.EqualTo(new[] { 0, 9 }), "id 集按升序");
            Assert.That(table.Count, Is.EqualTo(2));
        }

        /// <summary>`TryGet` 对缺失 id 返回 false 而不抛。</summary>
        [Test]
        public void test_tryGet_missingIsFalseNotThrow()
        {
            var table = new EmergencyActionTable(new[] { Row(0, 65536) });
            Assert.That(table.TryGet(2, out EmergencyActionRow row), Is.False);
            Assert.That(row, Is.Null);
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 缺行 ⇒ 抛
        // ════════════════════════════════════════════════════════════════

        /// <summary>生产默认表只有动作 0 ⇒ 动作 2 抛,报文点名动作号与已注册集合。</summary>
        [Test]
        public void test_require_missingAction_throwsNamingIt()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => EmergencyActionTable.ProductionDefault.Require(2));

            Assert.That(ex.Message, Does.Contain("动作 2"), "报文须点名是哪个动作");
            Assert.That(ex.Message, Does.Contain("已注册的动作"), "报文须给出已注册集合,便于判断是不是「忘了加」");
            Assert.That(ex.Message, Does.Contain("EmergencyActionRow"), "报文须指出下一步做什么");
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 构造期拒收
        // ════════════════════════════════════════════════════════════════

        /// <summary>同一 id 两行 ⇒ 构造期拒收(否则「用哪一行」取决于迭代序)。</summary>
        [Test]
        public void test_ctor_rejectsDuplicateActionId()
        {
            var ex = Assert.Throws<ArgumentException>(() => new EmergencyActionTable(new[]
            {
                Row(0, 65536),
                Row(0, 32768),
            }));
            Assert.That(ex.Message, Does.Contain("出现两次"));
        }

        /// <summary>负 id / null 行 ⇒ 构造期拒收。</summary>
        [Test]
        public void test_ctor_rejectsNegativeIdAndNullRow()
        {
            Assert.Throws<ArgumentException>(() => new EmergencyActionTable(new[] { Row(-1, 65536) }));
            Assert.Throws<ArgumentException>(() => new EmergencyActionTable(new EmergencyActionRow[] { null }));
            Assert.Throws<ArgumentNullException>(() => new EmergencyActionTable(null));
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 生产默认表的形状
        // ════════════════════════════════════════════════════════════════

        /// <summary>今日只注册动作 0 —— 动作 1/2/3 走到 `Require` 就是响亮的拒绝。</summary>
        [Test]
        public void test_productionDefault_registersOnlyBandage()
        {
            Assert.That(EmergencyActionTable.ProductionDefault.RegisteredActionIds,
                        Is.EqualTo(new[] { 0 }));
        }

        /// <summary>
        /// 生产行的 `ResultMul` 必须仍是 **schema 默认表**(逐值相等),不是手写副本。
        /// <para>副本曾漂移成 `{0, 32768, 65536}` 并让 Missed 档归零 —— 见
        /// `emergency_result_mul_production_drift_test`。</para>
        /// </summary>
        [Test]
        public void test_productionDefault_resultMulIsSchemaTiers()
        {
            long[] tiers = EmergencyActionTable.ProductionDefault.Require(0).ResultMul;
            Assert.That(tiers, Is.EqualTo(EmergencyActionSchema.GetResultMulTiers()),
                "生产行不得另写 ResultMul 副本(AC-10-16 / OQ-10-1)");
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 端到端:换表就换 potency
        // ════════════════════════════════════════════════════════════════

        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private HostEmergencyProcessor _processor;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new PresentPresence());
            _processor = new HostEmergencyProcessor(_stream, new SeqIdAuthority(),
                                                    new PayloadEncoder(_pool));
        }

        private long AppliedPotencyRaw()
        {
            foreach (SimEvent e in _stream.Events)
                if (e.Kind == EventKind.EmergencyTreatmentApplied)
                    return _decoder.Decode<EmergencyTreatmentAppliedPayload>(
                        EventKind.EmergencyTreatmentApplied, e.Payload).DrugPotency.Raw;
            Assert.Fail("流里没有 EmergencyTreatmentApplied");
            return -1;
        }

        private static AggregatedEmergency Agg(int action, long tick)
            => new AggregatedEmergency(action: action, holdTicks: 10, edges: 1,
                                       edgeTicks: new[] { (int)tick },
                                       magPeak: 100, magLast: 100);

        /// <summary>
        /// 同一份输入、两张不同的表 ⇒ 落流 potency 不同。
        /// <para>这一条是「驱动真的按 `attempt.Action` 取行」的**唯一**证据:
        /// 若驱动忽略表或用硬编码行,两个 action 会得到同一个 potency ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_driver_usesRowOfThatAction_notAHardcodedOne()
        {
            var twoRows = new EmergencyActionTable(new[]
            {
                Row(actionId: 0, basePotency: 65536),
                Row(actionId: 9, basePotency: 32768),
            });
            var driver = new EmergencyManualDriver(_processor, new PresentPresence(),
                                                   devices: null, actions: twoRows);

            Assert.That(driver.TryAggregated(Agg(0, 100), Actor, new PatientId(1), 100).Applied,
                        Is.True);
            long fromAction0 = AppliedPotencyRaw();

            // 新流,避免与上一笔混读
            _stream = new EventStream(new SeqIdAuthority(), new PresentPresence());
            _processor = new HostEmergencyProcessor(_stream, new SeqIdAuthority(),
                                                    new PayloadEncoder(_pool));
            var driver2 = new EmergencyManualDriver(_processor, new PresentPresence(),
                                                    devices: null, actions: twoRows);

            Assert.That(driver2.TryAggregated(Agg(9, 200), Actor, new PatientId(1), 200).Applied,
                        Is.True);
            long fromAction9 = AppliedPotencyRaw();

            Assert.That(fromAction0, Is.EqualTo(65536));
            Assert.That(fromAction9, Is.EqualTo(32768),
                "动作 9 的 basePotency = 0.5 ⇒ potency 必须减半。\n" +
                "两者相等 = 驱动没按 attempt.Action 取行(T13 之前的形态:恒用动作 0 那一行)");
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑥ 缺行 ⇒ 抛且流零事件
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 生产表没有动作 2 ⇒ 驱动抛,且**一个事件都没写**。
        /// <para>只看异常类型不够:下层也可能抛同类型异常,而那时流的污染已发生。
        /// 故断言抛之前下游零调用(流长为 0)。</para>
        /// </summary>
        [Test]
        public void test_driver_missingRow_throwsAndWritesNothing()
        {
            var driver = new EmergencyManualDriver(_processor, new PresentPresence());

            Assert.Throws<InvalidOperationException>(
                () => driver.TryAggregated(Agg(2, 100), Actor, new PatientId(1), 100));

            Assert.That(_stream.Events.Count, Is.EqualTo(0),
                "缺行必须在**写事件之前**被拦下 —— 流非空说明已经写了一部分");
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助
        // ════════════════════════════════════════════════════════════════

        private static EmergencyActionRow Row(int actionId, long basePotency) => new EmergencyActionRow
        {
            ActionId = actionId,
            Polarity = 2,
            BasePotency = basePotency,
            MagThreshold = 50,
            MinEdges = 1,
            MinHoldTicks = 5,
            HalfLifeTicks = 100,
            DurationTicks = 200,
            ResultMul = EmergencyActionSchema.GetResultMulTiers(),
        };

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class PresentPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 1;
            public bool IsPresentAt(WorldPos cell) => true;
            public IReadOnlyCollection<int> PresentPatientIds() => new[] { 1 };
        }
    }
}
