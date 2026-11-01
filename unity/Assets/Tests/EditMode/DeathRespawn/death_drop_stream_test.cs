// S23-2 · story-002 掉落清单流派生与回捡 —— 直接夹具(2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**(事件流 / 投影 / 写者全内存真装配;InventoryLoadService 读烘焙产物
//   —— 真表,由它自己的构造期守卫负责)。
// 写:**零写盘**。
//
// ── 流形状(被测契约,唯一出处 = DeathDropSettlement 文件头)────────────────────
//   逐件:`DropDespawned(持有消耗, reason=DeathDrop)` → `DropSpawned(同 id, death_cell)`
//   —— GDD 规则三伪码(`:142`)的机械补全:不先消耗,fold 报 DuplicateInstanceId
//   且库存/地面双份。
//
// ── 与既有件的分工 ────────────────────────────────────────────────────────────
//   `InventoryFold` / 投影的 fold 语义 = ItemDatabase 侧夹具;`DropEventWriter` 三支
//   写者 = S20-15 夹具;本件只测 **29 侧新增面**:结算编排、零铸造、回捡策略、消费者接线。
//
// ── 本文件记分面 ──────────────────────────────────────────────────────────────
//   ✅ AC-29-02(半边):逐件 DropSpawned,anchor = death_cell(非实时物理 —— 夹具给脏值)
//   ✅ 空背包 ⇒ 零 DropSpawned 零 DropDespawned(零事件)
//   ✅ 清单派生:结算后重 fold,CanonicalForm 逐位重建(玩家清空、地面 NoOwner)
//   ✅ instance_id:29 侧零铸造(源码负断言);载荷 id 与库存行一致
//   ✅ 回捡约束(AC-29-17 / 20 R7):出半径拒、入半径许、二次 claim 拒(幂等)
//   ✅ 位置非快照:DropSpawnedPayload 四字段全整数域(反射)
//   ✅ 消费者接线:PlayerDied 真写后才散落;未接线(001 形态)⇒ 零散落
//   ⚠️ **不证**:4 的拾取意图管线(未接线 —— 策略由其轮次消费);读档位置 = 格锚 +
//      确定性偏移(ADR-023 ⑦,表现态,归 7a/23 面);quality 不在载荷(R18 账)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.DeathRespawn
{
    /// <summary>死亡散落结算 + 回捡约束(29 侧)。</summary>
    [TestFixture]
    public sealed class DeathDropStreamTest
    {
        private CompositionRootServices _bag;
        private DropEventWriter _drops;
        private DeathDropSettlement _settlement;

        private const int PlayerId = 7;
        private const long Tick = 100;
        private static readonly WorldPos DeathCell = new WorldPos(5, 0, 3);

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _drops = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
            _settlement = new DeathDropSettlement(_drops);
            _settlement.BindProjectionSource(BagProjection);   // 生产装配的绑定形态
        }

        /// <summary>投影 = 真 fold(与生产同源;派生态每次重取)。</summary>
        private InventoryProjection BagProjection()
            => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder, InventoryFoldContext.None).Projection;

        /// <summary>给玩家塞 n 件持有物(走真流事件:DropSpawned → DropClaimed;
        /// id 取写者返回值 —— `RecordSpawned` 自铸,勿另发一号)。</summary>
        private void GivePlayer(int n, int itemKey = 3, int qty = 2)
        {
            for (int i = 0; i < n; i++)
            {
                ItemInstanceId id = _drops.RecordSpawned(Tick - 10, new WorldPos(0, 0, 0), itemKey, qty);
                _drops.RecordClaimed(Tick - 10, id, PlayerId);
            }
        }

        private int CountOf(EventKind kind) => _bag.Stream.Events.Count(e => e.Kind == kind);

        // ══════════════════ AC-29-02(半边)—— 逐件落地 ══════════════════

        [Test]
        public void test_settle_emitsDespawnThenSpawnPerItem_anchorIsDeathCell()
        {
            GivePlayer(2);
            int beforeSpawn = CountOf(EventKind.DropSpawned);

            int dropped = _settlement.Settle(Tick, PlayerId, DeathCell);

            Assert.AreEqual(2, dropped, "携带两件 ⇒ 散落两件");
            Assert.AreEqual(beforeSpawn + 2, CountOf(EventKind.DropSpawned), "逐件一条 DropSpawned");
            Assert.AreEqual(2, CountOf(EventKind.DropDespawned), "逐件一条 DropDespawned(先消耗)");

            // 事件序:每件 = Despawn → Spawn 相邻;实例 id 与库存行一致(GDD :142 i.id 原样)。
            var tail = _bag.Stream.Events.Skip(Math.Max(0, _bag.Stream.Events.Count - 4)).ToList();
            Assert.AreEqual(new[] { EventKind.DropDespawned, EventKind.DropSpawned,
                                    EventKind.DropDespawned, EventKind.DropSpawned },
                tail.Select(e => e.Kind).ToArray(), "结算段序 = 每件「消耗 → 落地」相邻");

            foreach (SimEvent e in tail.Where(x => x.Kind == EventKind.DropSpawned))
            {
                DropSpawnedPayload p = _bag.Decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
                Assert.AreEqual(DeathCell.X, p.SpawnAnchor.X);
                Assert.AreEqual(DeathCell.Z, p.SpawnAnchor.Z, "spawn_anchor = death_cell 整数格");
            }
        }

        /// <summary>anchor 不读实时物理:投影/写者面不存在任何连续坐标入参(结构性)。</summary>
        [Test]
        public void test_settle_anchorTakesCellNotContinuousPosition()
        {
            // 结算签名里没有 float/Vector3 形参 —— 连续位置物理上进不了这条路径(门 A 同法)。
            foreach (MethodInfo m in typeof(DeathDropSettlement).GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (ParameterInfo p in m.GetParameters())
                    Assert.That(p.ParameterType,
                        Is.Not.EqualTo(typeof(float)).And.Not.EqualTo(typeof(Vector3)),
                        "结算面收整数格,不收表现态连续位置(ADR-020 §四 / B4 订正)");
        }

        // ══════════════════ 空背包 ⇒ 零事件 ══════════════════

        [Test]
        public void test_emptyInventory_settleWritesNothing()
        {
            int before = _bag.Stream.Events.Count;

            int dropped = _settlement.Settle(Tick, PlayerId, DeathCell);

            Assert.AreEqual(0, dropped);
            Assert.AreEqual(before, _bag.Stream.Events.Count,
                "空背包 ⇒ 零 DropSpawned / 零 DropDespawned(EC 表:PlayerDied 照发是 001 的面,结算面零事件)");
        }

        // ══════════════════ 清单派生 —— 重放逐位重建 ══════════════════

        /// <summary>结算后重 fold:玩家清空、地面拿到同 id 同 qty(无独立掉落清单字段)。</summary>
        [Test]
        public void test_afterSettle_rebuiltProjection_playerEmpty_groundNoOwner()
        {
            GivePlayer(2);
            _settlement.Settle(Tick, PlayerId, DeathCell);

            InventoryProjection rebuilt = BagProjection();

            Assert.AreEqual(0, rebuilt.InstancesOf(PlayerId).Count, "玩家库存清空(先消耗)");
            Assert.AreEqual(2, rebuilt.InstanceCount, "两件在地上(同 id 再出生)");
            foreach (long id in rebuilt.AllInstanceIds)
            {
                rebuilt.TryGet(id, out InventoryRow row);
                Assert.AreEqual(InventoryProjection.NoOwner, row.Owner, "散落件无主(留待回捡)");
            }
        }

        /// <summary>结算前后两台「主机」重放同一事件序列 ⇒ 投影规范串逐位同。</summary>
        [Test]
        public void test_replay_sameSequence_identicalCanonicalForm()
        {
            GivePlayer(2);
            _settlement.Settle(Tick, PlayerId, DeathCell);

            var bagB = CompositionRoot.Assemble();
            var writerB = new DropEventWriter(bagB.EventSink, bagB.Encoder, bagB.IdAuthority);
            // 重放:把 A 流的掉落类事件按序原样重写进 B(同 id —— 非重铸)。
            foreach (SimEvent e in _bag.Stream.Events)
            {
                if (e.Kind == EventKind.DropSpawned)
                {
                    DropSpawnedPayload p = _bag.Decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
                    writerB.RecordSpawnedWithId(e.Tick, new ItemInstanceId(p.InstanceId),
                        p.SpawnAnchor, p.ItemKey, p.Qty);
                }
                else if (e.Kind == EventKind.DropClaimed)
                {
                    DropClaimedPayload p = _bag.Decoder.Decode<DropClaimedPayload>(e.Kind, e.Payload);
                    writerB.RecordClaimed(e.Tick, new ItemInstanceId(p.InstanceId), p.Claimer);
                }
                else if (e.Kind == EventKind.DropDespawned)
                {
                    DropDespawnedPayload p = _bag.Decoder.Decode<DropDespawnedPayload>(e.Kind, e.Payload);
                    writerB.RecordDespawned(e.Tick, new ItemInstanceId(p.InstanceId), p.Qty, p.Reason);
                }
            }

            string a = BagProjection().CanonicalForm();
            string b = InventoryFold.Fold(bagB.Stream.Events, bagB.Decoder,
                InventoryFoldContext.None).Projection.CanonicalForm();
            Assert.AreEqual(a, b, "重放逐位重建(F-29-3 / AC-20-03 同构判据)");
        }

        // ══════════════════ instance_id:29 侧零铸造 ══════════════════

        /// <summary>29 的结算面零 `IIdAuthority` 引用(载荷 id = 库存行 id 原样)。</summary>
        [Test]
        public void test_settlement_zeroMinting()
        {
            string src = File.ReadAllText(Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                "Assets/Sim/DeathDropSettlement.cs"));
            // ⚠️ 剥注释再扫 —— 文件头的「本类不触 IIdAuthority」注记自己就是该词;
            //    不剥 = 注释即接线(F6 同族)。
            string code = System.Text.RegularExpressions.Regex.Replace(
                src, @"//[^\n]*", " ");
            code = System.Text.RegularExpressions.Regex.Replace(
                code, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.IsFalse(code.Contains("IIdAuthority"),
                "29 结算面不得触 IIdAuthority —— 死亡掉落复用已铸 id(AC:零 Next() 调用;GDD :142)");
        }

        // ══════════════════ 位置非快照:载荷全整数域 ══════════════════

        [Test]
        public void test_dropSpawnedPayload_integerOnly()
        {
            var fields = typeof(DropSpawnedPayload).GetFields();
            Assert.AreEqual(4, fields.Length, "instance_id / spawn_anchor / item_key / qty");
            foreach (var f in fields)
                Assert.That(f.FieldType == typeof(long) || f.FieldType == typeof(WorldPos)
                            || f.FieldType == typeof(int),
                    Is.True, $"{f.Name}({f.FieldType}) 越出整数域 —— 位置/轨迹不进流(§五)");
        }

        // ══════════════════ 回捡约束(AC-29-17 / 20 R7)══════════════════

        private (InventoryProjection projection, ItemInstanceId id) DropOne()
        {
            GivePlayer(1);
            _settlement.Settle(Tick, PlayerId, DeathCell);
            long id = BagProjection().AllInstanceIds.Single();
            return (BagProjection(), new ItemInstanceId(id));
        }

        [Test]
        public void test_reclaim_offCell_isRejected()
        {
            var (projection, id) = DropOne();

            bool allowed = DeathDropReclaimPolicy.CanClaim(
                _bag.Stream.Events, _bag.Decoder, projection, id.Value,
                new WorldPos(DeathCell.X + 2, DeathCell.Y, DeathCell.Z), tolerance: 1);

            Assert.IsFalse(allowed, "出半径 ⇒ 拒(禁远程拾取 —— 20 R7)");
        }

        [Test]
        public void test_reclaim_atCell_withinTolerance_isAllowed()
        {
            var (projection, id) = DropOne();

            Assert.IsTrue(DeathDropReclaimPolicy.CanClaim(
                _bag.Stream.Events, _bag.Decoder, projection, id.Value,
                DeathCell, tolerance: 1), "站在锚点格 ⇒ 允许");

            // 宽容半径边界:d∞ == tolerance 仍在半径内(≤;与 17 采集同口径)。
            Assert.IsTrue(DeathDropReclaimPolicy.CanClaim(
                _bag.Stream.Events, _bag.Decoder, projection, id.Value,
                new WorldPos(DeathCell.X + 1, DeathCell.Y, DeathCell.Z), tolerance: 1),
                "d∞ == R 是半径内(≤)");
        }

        /// <summary>幂等:claim 入流后,同实例二次 claim ⇒ 策略拒(owner 已变更)。</summary>
        [Test]
        public void test_reclaim_secondClaim_rejected()
        {
            var (projection, id) = DropOne();
            _drops.RecordClaimed(Tick + 1, id, PlayerId);   // 玩家回捡成功(经 20 写者)
            var rebuilt = BagProjection();

            Assert.IsFalse(DeathDropReclaimPolicy.CanClaim(
                _bag.Stream.Events, _bag.Decoder, rebuilt, id.Value,
                DeathCell, tolerance: 1), "已归属 ⇒ 二次 claim 拒(幂等半边;fold 的缺口是机械兜底)");
            Assert.AreEqual(PlayerId, rebuilt.TryGet(id.Value, out InventoryRow r) ? r.Owner : -999,
                "claim 后库存恢复(owner = 玩家,经 20)");
        }

        // ══════════════════ 消费者接线(S23-2 × S23-1)══════════════════

        private static DiseaseRegistryEntry LethalEntry() => new DiseaseRegistryEntry
        {
            DiseaseId = 1, DiseaseKey = "DIS_PLAGUE", Kind = DiseaseKind.Disease, Lethality = 3,
        };

        [Test]
        public void test_consumerWired_settlementRunsAfterPlayerDied()
        {
            GivePlayer(2);
            var consumer = new DeathCandidateConsumer(
                new PlayerDiedWriter(_bag.EventSink, _bag.Encoder),
                deathAllowed: (_, __) => true,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => LethalEntry(),
                deathCellOf: _ => DeathCell,
                // 转序适配:回调 =(playerId, cell, tick),Settle =(tick, id, cell)。
                dropSettlement: (playerId, cell, tick) => _settlement.Settle(tick, playerId, cell));

            int playerDiedBefore = CountOf(EventKind.PlayerDied);
            int spawnBefore = CountOf(EventKind.DropSpawned);
            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(playerDiedBefore + 1, CountOf(EventKind.PlayerDied), "[前置] 死亡已入流");
            Assert.AreEqual(spawnBefore + 2, CountOf(EventKind.DropSpawned),
                "真写后才散落(结算段次位;增量 2 —— 播种的 2 条不计)");
            // 散落用的 death_cell 与载荷同格(算一次,不重查流)。
            var spawn = _bag.Stream.Events.Last(x => x.Kind == EventKind.DropSpawned);
            var died = _bag.Stream.Events.Last(x => x.Kind == EventKind.PlayerDied);
            Assert.AreEqual(
                _bag.Decoder.Decode<PlayerDiedPayload>(EventKind.PlayerDied, died.Payload).DeathCell.X,
                _bag.Decoder.Decode<DropSpawnedPayload>(EventKind.DropSpawned, spawn.Payload).SpawnAnchor.X,
                "散落锚 = PlayerDied 载荷的同一格");
        }

        [Test]
        public void test_consumerUnwired_zeroSettlement_001BehaviorPreserved()
        {
            GivePlayer(2);
            var consumer = new DeathCandidateConsumer(
                new PlayerDiedWriter(_bag.EventSink, _bag.Encoder),
                deathAllowed: (_, __) => true,
                actorClassOf: _ => ActorClass.Player,
                entryOf: _ => LethalEntry(),
                deathCellOf: _ => DeathCell);   // 未传结算回调(001 形态)

            int spawnBefore = CountOf(EventKind.DropSpawned);
            consumer.OnDeathCandidate(PlayerId, Tick);

            Assert.AreEqual(1, CountOf(EventKind.PlayerDied), "死亡照常入流");
            Assert.AreEqual(spawnBefore, CountOf(EventKind.DropSpawned),
                "未接线 ⇒ 零散落(可选参语义;增量 0 —— 播种的 2 条不计)");
        }
    }
}
