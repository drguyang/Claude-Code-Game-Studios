// 2026-11-02 · `TD-interaction-02` —— 第一源(`GroundDropSource`)的生产实装判据
//
// 判据(可证伪):
//   ① 地面未归属实例 ⇒ 出 (锚格 = 最近一次 `DropSpawned.spawn_anchor`, instance_id);
//   ② 被拾取后 ⇒ **立刻**从 `Rows` 消失(不缓存 —— 候选集是派生态,ADR-009 §四);
//   ③ 同一实例**再次落地**(消耗 → 再出生)⇒ 锚取**最新**那一条(与 `CanClaim` 同一真源)。
//
// ⚠️ 断言**不复制** `TryGetSpawnAnchor` 的扫描逻辑:期望值由夹具自己记的播种格给出,
//   而不是「再跑一遍生产实现」(那是自指判据,变异两边一起动)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>第一源:`GroundDropSource`(未归属地面实例 → 候选行)。</summary>
    public sealed class ground_drop_source_test
    {
        private CompositionRootServices _bag;
        private DropEventWriter _drops;
        private GroundDropSource _source;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            var writer = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
            _drops = writer;
            _source = new GroundDropSource(
                () => _bag.Stream.Events, _bag.Decoder,
                () => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder,
                                         InventoryFoldContext.None).Projection);
        }

        [TearDown]
        public void TearDown() => _bag?.Dispose();

        [Test]
        public void test_rows_carrySpawnAnchorAndInstanceId()
        {
            long a = _drops.RecordSpawned(0, new WorldPos(1, 0, 2), itemKey: 3, qty: 1).Value;
            long b = _drops.RecordSpawned(0, new WorldPos(-4, 1, 7), itemKey: 5, qty: 2).Value;

            IReadOnlyList<DropDto> rows = _source.Rows;

            Assert.AreEqual(2, rows.Count, "两件地面掉落 ⇒ 两行");
            var byId = new Dictionary<long, WorldPos>();
            foreach (DropDto d in rows) byId[d.InstanceId] = d.Anchor;
            Assert.AreEqual(new WorldPos(1, 0, 2), byId[a], "锚格 = 播种时给的整数格");
            Assert.AreEqual(new WorldPos(-4, 1, 7), byId[b]);
        }

        [Test]
        public void test_claimedInstance_disappearsImmediately_noCache()
        {
            long id = _drops.RecordSpawned(0, new WorldPos(3, 0, 3), itemKey: 3, qty: 1).Value;
            Assert.AreEqual(1, _source.Rows.Count, "[前置] 一件在地上");

            _drops.RecordClaimed(1, id, claimer: 9);

            Assert.AreEqual(0, _source.Rows.Count,
                "已归属 ⇒ 必须**立刻**从候选集消失 —— 缓存即第二真源" +
                "(否则玩家会在空地上再按一次、拿同一个 id)");
        }

        [Test]
        public void test_reSpawnedSameInstance_anchorIsLatest()
        {
            long id = _drops.RecordSpawned(0, new WorldPos(5, 0, 5), itemKey: 3, qty: 1).Value;
            // 「消耗 → 再落地」:先归零(否则同 id 再出生是 `DuplicateInstanceId` 违规 ——
            // 机制 A 号不回收),再以**同一个 id**在别处落地(死亡散落/拆除返还的形态)。
            _drops.RecordDespawned(1, id, qty: 1, reason: 0);
            _drops.RecordSpawnedWithId(2, id, new WorldPos(8, 0, 8), itemKey: 3, qty: 1);

            IReadOnlyList<DropDto> rows = _source.Rows;

            Assert.AreEqual(1, rows.Count, "[前置] 重新落地后又是地面候选");
            Assert.AreEqual(new WorldPos(8, 0, 8), rows[0].Anchor,
                "锚 = **最近一次**出生格(与 CanClaim 的倒序扫同一真源;" +
                "写成「第一次」= 拾取点与看不见的旧位置对齐)");
        }
    }
}
