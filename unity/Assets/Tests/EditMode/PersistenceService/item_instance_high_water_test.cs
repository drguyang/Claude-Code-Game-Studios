// AC-7a-13 测试 —— ItemInstanceId 高水位(扫描域 = 三流并集,零复用)。
//
// 权威来源:
//   design/gdd/persistence-service.md:556-562 —— AC-7a-13(BLOCKING)
//   ADR-010 §五 —— 机制 A:next = max(已见) + 1 由三流并集重构;计数器永不复位 0
//   ADR-006 Amendment B —— 机制 A 三条不变量
//   design/registry/entities.yaml —— 五支含 instance_id 的 Kind(真源)
//
// 被测件:Sim/ItemDatabase/ItemInstanceHighWaterCollector.cs
//   (补的是「收集」这一环 —— 实测 ReconstructItemHighWater 生产调用点 = 0)
//
// 无文件 I/O,无 Unity API ⇒ 不触 FsWriteBoundaryGate。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class ItemInstanceHighWaterTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具:把一条强类型载荷编进池,再包成一个 SimEvent
        // ══════════════════════════════════════════════════════════════════

        private SimEvent Event<T>(EventKind kind, T payload, long tick = 0L) where T : struct
            => new SimEvent(tick, PatientId.None, -1, kind, _encoder.Encode(kind, payload));

        private SimEvent DropSpawned(long instanceId, long tick = 0L)
            => Event(EventKind.DropSpawned,
                     new DropSpawnedPayload(instanceId, new WorldPos(0, 0, 0), 1, 1), tick);

        private SimEvent DropClaimed(long instanceId, long tick = 0L)
            => Event(EventKind.DropClaimed, new DropClaimedPayload(instanceId, 3, tick), tick);

        private SimEvent DropDespawned(long instanceId, long tick = 0L)
            => Event(EventKind.DropDespawned, new DropDespawnedPayload(instanceId, 1, 0), tick);

        private SimEvent ResourceHarvested(long instanceId, long tick = 0L)
            => Event(EventKind.ResourceHarvested,
                     new ResourceHarvestedPayload(instanceId, 9, 1, 1, 0), tick);

        private SimEvent Craft(long[] inputs, long[] outputs, long tick = 0L)
            => Event(EventKind.Craft,
                     new CraftPayload(1, 2, tick, 4L,
                                      inputs, inputs.Select(_ => 1).ToArray(),
                                      outputs.Select(_ => 1).ToArray(), outputs.Select(_ => 0).ToArray(),
                                      outputs, new WorldPos(0, 0, 0)), tick);

        private static IdAuthority Authority() => new IdAuthority();

        // ══════════════════════════════════════════════════════════════════
        // AC-7a-13 主判据:五支全部计入(任一出现都计入)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a13_allFiveKindsContribute()
        {
            var events = new List<SimEvent>
            {
                ResourceHarvested(instanceId: 10),
                DropSpawned(instanceId: 11),
                DropClaimed(instanceId: 12),
                DropDespawned(instanceId: 13),
                Craft(inputs: new long[] { 14 }, outputs: new long[] { 15 }),
            };

            var ids = ItemInstanceHighWaterCollector.Collect(events, _decoder);

            CollectionAssert.AreEquivalent(new long[] { 10, 11, 12, 13, 14, 15 }, ids,
                "五支 Kind 的 instance_id 都须计入(漏任一支 ⇒ 该 id 可被重发)");
        }

        [Test]
        public void test_ac7a13_reconstructsNextToMaxPlusOne()
        {
            var events = new List<SimEvent> { DropSpawned(instanceId: 5), DropSpawned(instanceId: 41) };
            var authority = Authority();   // 初值 next = 0

            ItemInstanceHighWaterCollector.CollectAndReconstruct(events, _decoder, authority);

            // 重构后首个新号须是 max + 1 = 42(若还发 0/5 就是重号)
            Assert.AreEqual(42L, authority.NextItemInstanceId().Value,
                "重构后 next 须 = max(已见) + 1(AC-7a-13「> 当前已用最大值」)");
        }

        [Test]
        public void test_ac7a13_zeroReuse_afterReconstruct()
        {
            // 「零复用」的直接可观测形态:重构后连续发放的号都不得等于任何已用号
            var used = new long[] { 3, 7, 11, 100 };
            var events = used.Select((id, i) => DropSpawned(id, tick: i)).ToList();
            var authority = Authority();

            ItemInstanceHighWaterCollector.CollectAndReconstruct(events, _decoder, authority);

            for (int i = 0; i < 5; i++)
            {
                long issued = authority.NextItemInstanceId().Value;
                CollectionAssert.DoesNotContain(used, issued,
                    $"重构后发放的号 {issued} 撞上已用号 ⇒ 物品实例会在 fold 里静默合并");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 哨兵口径:负值原样交出,由 IdAuthority 按「忽略」处理(不双实现)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a13_negativeSentinelNotFilteredButIgnoredOnReconstruct()
        {
            // 收集器**不过滤**(过滤在 IdAuthority 一处);但重构结果不得被污染
            var events = new List<SimEvent> { DropSpawned(instanceId: -1), DropSpawned(instanceId: 8) };

            var ids = ItemInstanceHighWaterCollector.Collect(events, _decoder);
            CollectionAssert.Contains(ids, -1L,
                "收集器不承担过滤职责(过滤 = IdAuthority.ReconstructItemHighWater 一处,禁双实现)");

            var authority = Authority();
            authority.ReconstructItemHighWater(ids);
            Assert.AreEqual(9L, authority.NextItemInstanceId().Value,
                "负哨兵不得把高水位拉向 0 方向(ADR-007 §四)");
        }

        // ══════════════════════════════════════════════════════════════════
        // fail-closed:不注入解码器 ⇒ 抛,禁静默返回空集
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a13_nullDecoder_failsClosed()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => ItemInstanceHighWaterCollector.Collect(new List<SimEvent>(), null),
                "未注入解码器 ⇒ 必须抛(空集静默返回 ⇒ 高水位偏低 ⇒ 读档后发重号)");
            StringAssert.Contains("重号", ex.Message, "报文须点名后果");
        }

        [Test]
        public void test_ac7a13_nullEvents_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => ItemInstanceHighWaterCollector.Collect(null, _decoder));
        }

        [Test]
        public void test_ac7a13_collectAndReconstruct_nullAuthority_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => ItemInstanceHighWaterCollector.CollectAndReconstruct(
                    new List<SimEvent>(), _decoder, null));
        }

        // ══════════════════════════════════════════════════════════════════
        // 边界:空流 / 无 instance_id 的 Kind 不干扰
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a13_emptyStream_leavesCounterAtInitialValue()
        {
            var authority = Authority();
            ItemInstanceHighWaterCollector.CollectAndReconstruct(new List<SimEvent>(), _decoder, authority);
            Assert.AreEqual(0L, authority.NextItemInstanceId().Value,
                "空流 ⇒ next 维持初值 0(不得因『没有 id』而误升或误降)");
        }

        [Test]
        public void test_ac7a13_kindsWithoutInstanceIdAreIgnored()
        {
            // ActorCellEntered 的载荷无 instance_id;它不得让收集器去解错类型/抛
            var events = new List<SimEvent>
            {
                Event(EventKind.ActorCellEntered, new ActorCellEnteredPayload(1, new WorldPos(1, 0, 0), 0L)),
                DropSpawned(instanceId: 6),
            };
            var ids = ItemInstanceHighWaterCollector.Collect(events, _decoder);
            CollectionAssert.AreEquivalent(new long[] { 6 }, ids);
        }

        // ══════════════════════════════════════════════════════════════════
        // 覆盖完备性(机械判据):registry 里带 instance_id 的 Kind 恰 = 本类处理的五支
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a13_collectorCoversExactlyTheRegistryInstanceIdKinds()
        {
            // 判据来源 = design/registry/entities.yaml 的 payload_schema(ADR-024 §① 真源)。
            // 本断言把它固化成**可执行的常量**:registry 将来新增带 instance_id 的 Kind,
            // 必须同时改这里与收集器 —— 否则下面的「逐 Kind 往返」抓不到新支。
            var expected = new[]
            {
                EventKind.ResourceHarvested,
                EventKind.DropSpawned,
                EventKind.DropClaimed,
                EventKind.DropDespawned,
                EventKind.Craft,
            };

            // 逐支单独收集,任一支不产出 id ⇒ 该支未被处理
            foreach (var kind in expected)
            {
                SimEvent e = kind == EventKind.Craft
                    ? Craft(new long[] { 77 }, new long[] { 78 })
                    : kind == EventKind.ResourceHarvested ? ResourceHarvested(77)
                    : kind == EventKind.DropSpawned ? DropSpawned(77)
                    : kind == EventKind.DropClaimed ? DropClaimed(77)
                    : DropDespawned(77);

                var ids = ItemInstanceHighWaterCollector.Collect(new[] { e }, _decoder);
                Assert.IsNotEmpty(ids, $"{kind} 带 instance_id,收集器却零产出 ⇒ 该支未处理");
            }

            Assert.AreEqual(5, expected.Length,
                "registry 实测带 instance_id 的 Kind = 5 支;" +
                "改这个数前须重跑 design/registry/entities.yaml 的 payload_schema 扫描");
        }
    }
}
