// S20-14 · inventory story-005 —— 装卸原子性 / 整件拒绝 / 零负确认 端到端测试
// (Integration · PlayMode · **真装配**,禁 Fake)
//
// 权威来源:
//   design/gdd/inventory-and-items.md
//     规则六(:348-372)存取原子性 —— 原子单位四行表;「一次装卸要么全成,要么全不成」
//     规则六 BL-23④ 消耗意图点名 `[(instance_id, qty)]`
//     规则九(:529-560)跨栈 = N 条同 tick 相邻 `Seq`;任一不成立 ⇒ **一条都不发**
//     Edge Cases(:613-621)超容量整件拒 / 中途失败整体回滚 / `qty` 畸形 ⇒ 零事件
//   design/registry/entities.yaml
//     `SimEvent.Kind.{DropSpawned, DropClaimed, DropDespawned}` —— 全 `stream: world` / `author: 20`
//   story production/epics/inventory-items/story-005-atomicity-consumption-recovery-no-ack.md
//     AC-20-06(超容量整件拒)· AC-20-07(中途失败整体回滚)
//     AC-20-17(零 `ItemConsumed`/`ItemTransferred` 追加 Kind)
//     AC-20-25(无负确认:被拒 ⇒ 世界流零新增;禁向 45 索 ack)
//     AC-20-19 / AC-20-14 —— **BLOCKED**,见文件末「AC 记分」。
//   ADR-005(主机唯一 Append / 发号)· ADR-006(载荷 ⊆ 整数域)
//   ADR-009 §二/§五/§七(世界流 · 身份进流 / 位置表现 · 拾取三段式)
//   ADR-024(Kind 单一真源)· ADR-029 §③(唯一编码路径)
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条自我约束(承 S20-13 `craft_event_payload_test.cs` 同款)
// ─────────────────────────────────────────────────────────────────────────────
// ① **零字面量调参** —— 常量一律夹具自建并标注「夹具值」,不抄真表数值。
// ② **判据不引用被测常量** —— 期望值一律是夹具输入的直接推论。
// ③ **「零事件」类断言必须配正控** —— 只断「没写」而不证明「本来会写」,与
//    「走错路 / 没接线」逐位不可区分。每条零断言旁边都有一条同构造的**会写**用例。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(不触 `FsWriteBoundaryGate`)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PlayMode.Inventory
{
    /// <summary>装卸原子性的端到端测试(真装配 <see cref="CompositionRoot"/>,真 <see cref="EventStream"/>,禁 Fake)。</summary>
    [TestFixture]
    public sealed class InventoryAtomicityTest
    {
        // ── 夹具值(全部写死,零随机)──────────────────────────────────────
        private const long Tick = 9100L;
        private const int Actor = 3;
        private const int OtherActor = 4;
        private const int ItemOrdinal = 21;
        private const int ItemQuality = 3;
        private const int UnitWeight = 2;
        private const int Cap = 20;

        private static readonly WorldPos Anchor = new WorldPos(7, 0, -3);

        private CompositionRootServices _bag;
        private DropEventWriter _writer;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
        }

        [TearDown]
        public void Teardown()
        {
            _bag?.Dispose();
            _bag = null;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-06 —— 超容量拾取:整件拒绝,`InventoryOf` 零变化,地面实例留存
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2006_overCapacityPickup_leavesStreamAndProjectionBitIdentical()
        {
            // 地面上一件 qty=3、单件重 2 的实例 ⇒ 候选重量 = 6。
            long groundId = SeedGroundInstance(qty: 3);
            string before = FoldCanonicalForm();
            int beforeCount = _bag.Stream.Count;
            int beforeMints = MintsSoFar();

            // 载重 15 + 候选 6 = 21 > cap 20 ⇒ 整件拒。
            InventoryRejection verdict = InventoryTransaction.ValidatePickup(
                groundId, Actor, currentCarryLoad: 15L, unitWeight: UnitWeight,
                capacity: new CarryCapacity(Cap), projection: Projection());

            Assert.AreEqual(InventoryRejection.CapacityInsufficient, verdict,
                "15 + 2×3 = 21 > 20 ⇒ 整件拒(不拆分)");

            // 拒绝路径的调用方**不落流** —— 校验是纯函数,写与不写由调用方按裁决决定。
            Assert.AreEqual(beforeCount, _bag.Stream.Count,
                "AC-20-25:被拒意图 ⇒ 世界流**零新增**");
            Assert.AreEqual(before, FoldCanonicalForm(),
                "AC-20-06:`InventoryOf` 前后**逐位相同**(地面实例原样留存)");
            Assert.AreEqual(beforeMints, MintsSoFar(), "被拒路径零铸造");

            // 地面实例仍在(且 owner 仍是 NoOwner)。
            Assert.IsTrue(Projection().TryGet(groundId, out InventoryRow row));
            Assert.AreEqual(InventoryProjection.NoOwner, row.Owner, "它还在原地,没被人捡走");
            Assert.AreEqual(3, row.Qty, "份数一份没少");
        }

        [Test]
        public void test_ac2006_exactlyAtCapacityPickup_isAcceptedAndWritesExactlyOneEvent()
        {
            // 正控(承上条):同一实例、载重 14 + 6 = 20 = cap ⇒ **可捡**。
            long groundId = SeedGroundInstance(qty: 3);
            int beforeCount = _bag.Stream.Count;

            InventoryRejection verdict = InventoryTransaction.ValidatePickup(
                groundId, Actor, currentCarryLoad: 14L, unitWeight: UnitWeight,
                capacity: new CarryCapacity(Cap), projection: Projection());
            Assert.AreEqual(InventoryRejection.None, verdict, "恰等容量合法(`≤` 而非 `<`)");

            _writer.RecordClaimed(Tick + 1, new ItemInstanceId(groundId), Actor);

            Assert.AreEqual(beforeCount + 1, _bag.Stream.Count, "接受路径**恰写 1 条**");
            Assert.IsTrue(Projection().TryGet(groundId, out InventoryRow row));
            Assert.AreEqual(Actor, row.Owner, "归属迁到拾取者");
            Assert.AreEqual(3, row.Qty, "拾取**不改份数**(只改归属)");
        }

        [Test]
        public void test_ac2006_multiQuantityLargerThanOne_multipliesBeforeComparing()
        {
            // ⚠️ 「整件」= 整条实例(含其全部份数),不是「一件一份」。
            //    单件重 2、qty 3 ⇒ 候选 6;载重 15 ⇒ 21 > 20 拒。
            //    若把 qty 忘了乘,会算成 15 + 2 = 17 ≤ 20 ⇒ 错误放行。
            long groundId = SeedGroundInstance(qty: 3);

            Assert.AreEqual(InventoryRejection.CapacityInsufficient,
                InventoryTransaction.ValidatePickup(
                    groundId, Actor, currentCarryLoad: 15L, unitWeight: UnitWeight,
                    capacity: new CarryCapacity(Cap), projection: Projection()),
                "整件 = 全部份数一次搬;漏乘 qty 会静默放行超载");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-07 —— 炮制中途失败:整体回滚(料不扣、货不出)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2007_consumeValidationFailure_batchIsRejectedWhole_notPartially()
        {
            // 跨栈:栈 A 够、栈 B 不够 —— 逐条校验各自都会「过」到很后面,
            // 但整请求必须一条都不发。
            long stackA = SeedOwnedInstance(qty: 10, quality: 2);
            long stackB = SeedOwnedInstance(qty: 2, quality: 2);

            string before = FoldCanonicalForm();
            int beforeCount = _bag.Stream.Count;

            var lines = new[]
            {
                new ConsumeLine(stackA, 5),   // 单独看:合法
                new ConsumeLine(stackB, 7),   // 超量 ⇒ 整请求拒
            };

            Assert.AreEqual(InventoryRejection.QuantityExceedsAvailable,
                InventoryTransaction.ValidateConsumption(lines, Actor, Projection()));

            Assert.AreEqual(beforeCount, _bag.Stream.Count, "整请求拒 ⇒ **零事件**(栈 A 也不扣)");
            Assert.AreEqual(before, FoldCanonicalForm(), "投影前后逐位相同 = 「料不扣」");

            // 精确点:栈 A 的余量没被动过 —— 「先扣够的栈」正是本 AC 要禁的半途形态。
            Assert.IsTrue(Projection().TryGet(stackA, out InventoryRow rowA));
            Assert.AreEqual(10, rowA.Qty, "栈 A 的 10 份一份没少");
        }

        [Test]
        public void test_ac2007_acceptedBatch_writesEveryLineAtTheSameTickWithAdjacentSeq()
        {
            // 正控:同样的两栈,把超量那条改成合法 ⇒ 恰好 N 条 `DropDespawned`,
            // 同 tick、`Seq` 相邻(OQ-20-10 裁 ①)。
            long stackA = SeedOwnedInstance(qty: 10, quality: 2);
            long stackB = SeedOwnedInstance(qty: 2, quality: 2);
            int beforeCount = _bag.Stream.Count;

            var lines = new[]
            {
                new ConsumeLine(stackA, 5),
                new ConsumeLine(stackB, 2),
            };
            Assert.AreEqual(InventoryRejection.None,
                InventoryTransaction.ValidateConsumption(lines, Actor, Projection()));

            const long consumeTick = Tick + 50;
            var batch = new List<SimEvent>(lines.Length);
            foreach (ConsumeLine line in lines)
            {
                // 先造事件后交给 Commit —— 这样「构造」与「落流」在测试里也分开,
                // 与生产路径(先全验、再整批 Commit)同形。
                var payload = new DropDespawnedPayload(line.InstanceId, line.Qty, reason: 1);
                batch.Add(new SimEvent(consumeTick, PatientId.None, -1, EventKind.DropDespawned,
                    _bag.Encoder.Encode(EventKind.DropDespawned, payload)));
            }

            Assert.IsTrue(InventoryTransaction.TicksAreAppendable(batch, LastAppendedTick()),
                "同 tick 批 ⇒ 批内无回退,可整批写");
            InventoryTransaction.Commit(batch, _bag.EventSink);

            Assert.AreEqual(beforeCount + 2, _bag.Stream.Count, "N = 2 条,一条不少");

            SimEvent[] written = _bag.Stream.Events
                .Skip(beforeCount)
                .Where(e => e.Kind == EventKind.DropDespawned)
                .ToArray();
            Assert.AreEqual(2, written.Length);
            Assert.AreEqual(consumeTick, written[0].Tick);
            Assert.AreEqual(consumeTick, written[1].Tick, "同 tick(规则九)");
            Assert.AreEqual(written[0].Seq + 1, written[1].Seq,
                "`Seq` 相邻 —— 分组键 = `(tick)`,不新造计数器(GDD 规则九)");

            // 投影真的扣了(不是「写了事件但没生效」)。
            InventoryProjection after = Projection();
            Assert.IsTrue(after.TryGet(stackA, out InventoryRow rowA));
            Assert.AreEqual(5, rowA.Qty, "10 − 5 = 5");

            // ⚠️ 归零的实例**出投影**(`InventoryProjection` 只收 `Qty > 0` 的行)——
            //    所以这里只能断「查不到」,**不能**断「查得到且 Qty == 0」(那条永远为假)。
            Assert.IsFalse(after.TryGet(stackB, out _),
                "qty 归零 ⇒ 该实例出投影(规则一 `DropDespawned` 行)");

            // 正控:同一次折叠里**另一条**实例仍在 —— 证明「查不到」是归零导致,
            // 不是整个投影空了 / fold 根本没跑。
            Assert.AreEqual(1, after.InstanceCount, "恰剩 1 行(栈 A)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-17 —— 零 `ItemConsumed` / `ItemTransferred` 追加 Kind
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2017_noConsumptionKindAppearsInTheRun()
        {
            // 跑一遍「出生 → 归属 → 消耗」的完整写入路径,再扫全流:
            // 扣减只由既有三支表达,不得冒出独立的「消耗 / 转移」事件。
            long id = SeedOwnedInstance(qty: 4, quality: 2);
            _writer.RecordDespawned(Tick + 10, new ItemInstanceId(id), qty: 1, reason: 1);

            var kinds = _bag.Stream.Events.Select(e => e.Kind).Distinct().ToArray();
            Assert.IsFalse(kinds.Select(k => k.ToString()).Any(n =>
                    n == "ItemConsumed" || n == "ItemTransferred"),
                "AC-20-17:全流零 `ItemConsumed` / `ItemTransferred`");

            // 正控:扣减本身**确实**发生了(否则上一条是空断言)。
            Assert.IsTrue(kinds.Contains(EventKind.DropDespawned), "扣减走的是 `DropDespawned`");
            Assert.IsTrue(Projection().TryGet(id, out InventoryRow row));
            Assert.AreEqual(3, row.Qty, "4 − 1 = 3");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-20-25 —— 无负确认:被拒 ⇒ 零新增;枚举面零否定 Kind
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac2025_repeatedRejections_neverAccumulateAnyEvent()
        {
            // 连续被拒 N 次 ⇒ 流仍零事件(禁「累计告警」Kind 私设)。
            long groundId = SeedGroundInstance(qty: 3);
            int beforeCount = _bag.Stream.Count;
            string before = FoldCanonicalForm();

            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(InventoryRejection.CapacityInsufficient,
                    InventoryTransaction.ValidatePickup(
                        groundId, Actor, currentCarryLoad: 20L, unitWeight: UnitWeight,
                        capacity: new CarryCapacity(Cap), projection: Projection()));
            }

            Assert.AreEqual(beforeCount, _bag.Stream.Count, "N 次被拒 ⇒ 仍零事件");
            Assert.AreEqual(before, FoldCanonicalForm(), "投影逐位相同");
        }

        [Test]
        public void test_ac2025_entitiesRegistry_hasNoNegativeSignalKind()
        {
            // ⚠️ 读的是**真 registry 文件**(不是自造的样例 YAML)—— 这条才是
            //    「20 的代码路径与 `entities.yaml` Kind 闭集」里的闭集半边。
            string path = EntitiesYamlPath();
            Assert.IsTrue(System.IO.File.Exists(path), "registry 真源须存在:" + path);

            string text = System.IO.File.ReadAllText(path);
            foreach (string forbidden in new[] { "PickupRejected", "IntentAck", "ConsumeFailed" })
            {
                Assert.IsFalse(text.Contains("SimEvent.Kind." + forbidden),
                    $"`entities.yaml` 不得登记否定信令 Kind `{forbidden}`(AC-20-25)");
            }

            // 正控:三支 Drop 确实登记着(否则上一条是「文件没读到」的空断言)。
            foreach (string required in new[] { "DropSpawned", "DropClaimed", "DropDespawned" })
            {
                Assert.IsTrue(text.Contains("SimEvent.Kind." + required),
                    $"`entities.yaml` 须登记 `{required}` —— 正控,证明上一条不是空过");
            }
        }

        [Test]
        public void test_ac2025_dropEventsCarryNoAckCorrelationField()
        {
            // 「禁向 45 索 ack」的**形状面**:三支载荷没有 correlation / request / ack 位。
            // ⚠️ 反射扫 `DropSpawnedPayload` 的字段名 —— 名字是契约的一部分
            //    (`entities.yaml` 的 payload_schema 逐字对齐,ADR-024)。
            Type[] payloads =
            {
                typeof(DropSpawnedPayload), typeof(DropClaimedPayload), typeof(DropDespawnedPayload),
            };

            foreach (Type t in payloads)
            {
                string[] names = t.GetFields().Select(f => f.Name.ToLowerInvariant()).ToArray();
                foreach (string banned in new[] { "correlation", "requestid", "ack" })
                {
                    Assert.IsFalse(names.Any(n => n.Contains(banned)),
                        $"{t.Name} 不得含 `{banned}` 类字段 —— 45 是纯管道,不做回执(ADR-001 §一)");
                }
                // 正控:字段确实读到了(空类型会让上一条空过)。
                Assert.IsNotEmpty(names, $"{t.Name} 须有字段 —— 正控");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造一件**在地上**的实例(出生 + 品级绑定),返回其 id。</summary>
        private long SeedGroundInstance(int qty, int quality = ItemQuality)
        {
            ItemInstanceId id = _writer.RecordSpawned(Tick, Anchor, ItemOrdinal, qty);

            // `DropSpawned` 无 quality 位(R18 已登记)⇒ 品级由 `ResourceHarvested.out_quality` 绑。
            var harvest = new ResourceHarvestedPayload(
                id.Value, nodeId: 0, gatherSeq: 0, qty: qty, outQuality: quality);
            _bag.EventSink.Append(new SimEvent(Tick, PatientId.None, -1,
                EventKind.ResourceHarvested,
                _bag.Encoder.Encode(EventKind.ResourceHarvested, harvest)));

            return id.Value;
        }

        /// <summary>造一件**已归 <see cref="Actor"/>** 的实例,返回其 id。</summary>
        private long SeedOwnedInstance(int qty, int quality = ItemQuality)
        {
            long id = SeedGroundInstance(qty, quality);
            _writer.RecordClaimed(Tick, new ItemInstanceId(id), Actor);
            return id;
        }

        private InventoryProjection Projection()
            => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder).RequireComplete();

        private string FoldCanonicalForm() => Projection().CanonicalForm();

        private long LastAppendedTick()
            => _bag.Stream.Events.Count == 0
                ? long.MinValue
                : _bag.Stream.Events[_bag.Stream.Events.Count - 1].Tick;

        private int MintsSoFar()
        {
            // 铸造次数的可观测形式 = 已入流事件里出现过的最大 instance_id + 1
            // (IdAuthority 计数器不对外暴露)。⚠️ 只在「本夹具没有空号」时成立 ——
            // 本夹具的所有铸造都紧随一条 `DropSpawned`,故成立。
            long max = -1L;
            foreach (SimEvent e in _bag.Stream.Events)
            {
                if (e.Kind != EventKind.DropSpawned) continue;
                DropSpawnedPayload p = _bag.Decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload);
                if (p.InstanceId > max) max = p.InstanceId;
            }
            return (int)(max + 1L);
        }

        /// <summary>
        /// `design/registry/entities.yaml` 的绝对路径 —— 经 `Application.dataPath` 上溯仓库根
        /// (与 `Editor.Tools.Gates` 的 `DiseaseConstantRegistryGate.EntitiesYamlPath` 同法)。
        /// </summary>
        private static string EntitiesYamlPath()
        {
            string assetsDir = UnityEngine.Application.dataPath;        // …/unity/Assets
            string unityDir = System.IO.Path.GetDirectoryName(assetsDir);   // …/unity
            string repoRoot = System.IO.Path.GetDirectoryName(unityDir);    // 仓库根
            return System.IO.Path.Combine(repoRoot, "design", "registry", "entities.yaml");
        }
    }
}
