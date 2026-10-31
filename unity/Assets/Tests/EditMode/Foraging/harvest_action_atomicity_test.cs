// story-005 —— 采集动作原子性与失败收敛(AC-17-07 / 07c / 08)。EditMode,真装配,禁 Fake 生产件。
//
// 权威:
//   design/gdd/foraging.md §Edge Cases「失败收敛」`:429-450` · 规则六(原子性)·
//     F-17-3 求值口径 ②(采前闸 `remaining ≥ Fix(qty)`)
//   story production/epics/foraging/story-005-action-atomicity-failure-convergence.md
//     AC-17-07(满载零消耗)· AC-17-07c(三类具名分支)· AC-17-08(打断零效果)
//   ADR-009 §七 三段式(意图 + 当下判距 + 效果进流)
//
// ── 本件证的是什么(可判形态),不是什么(自述)──────────────────────────
// 「失败 ⇒ 零事件」**不靠回滚**,靠**顺序**:闸门排在铸号、`Query`、三条产出之前。
// 因此判据必须落在**副作用计数**上 —— 流条数、发号高水位、新颖度类别 —— 而不是
// 只看返回值。
//
// ⚠️ 本件**不**覆盖 `TimeoutUnresolved` 的行为:它依赖上行通道(`TR-foraging-008` 仍 gap,
//    `OQ-17-6` 未裁)。只验它作为**枚举成员存在**(AC-17-07c 的「各支具名」),不伪造计时。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>采集裁决闸门与失败收敛。</summary>
    [TestFixture]
    public sealed class HarvestActionAtomicityTest
    {
        private CompositionRootServices _bag;
        private SpyDropWriter _drop;
        private SpyGrowthEmitter _growth;
        private HarvestOrchestrator _orchestrator;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            var harvestWriter = new ResourceHarvestWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            _drop = new SpyDropWriter();
            _growth = new SpyGrowthEmitter();
            // 每测一个全新追踪器(它是有状态的;复用会造成跨测污染)
            _orchestrator = new HarvestOrchestrator(
                _bag.IdAuthority, harvestWriter, _drop, _growth, new NoveltyTracker());
        }

        // ══════════ AC-17-07 · 满载 ⇒ 具名失败 + 零消耗 ══════════

        [Test]
        public void test_ac1707_fullInventory_rejectsNamed_zeroEvents_zeroConsumption()
        {
            long idBefore = 0L;   // 本夹具内唯一的铸造者是 Resolve 自己 ⇒ 首个 id 必为 0
            int streamBefore = _bag.Stream.Count;

            HarvestOutcome o = _orchestrator.Resolve(Req(), new HarvestGate(
                hasCapacity: false, hasRemaining: true));

            Assert.That(o.Succeeded, Is.False, "满载 ⇒ 不得成功");
            Assert.That(o.Failure, Is.EqualTo(HarvestFailureKind.FullInventory),
                "失败类须**具名**为 FullInventory —— 不得压成一个 bool");

            // ① 零事件:三条产出 + 成长全都没发
            Assert.That(_bag.Stream.Count, Is.EqualTo(streamBefore), "满载 ⇒ 流里零事件");
            Assert.That(_drop.SpawnCount, Is.EqualTo(0), "满载 ⇒ 不得请求 DropSpawned");
            Assert.That(_drop.ClaimCount, Is.EqualTo(0), "满载 ⇒ 不得请求 DropClaimed");
            Assert.That(_growth.Calls.Count, Is.EqualTo(0), "满载 ⇒ 零成长");

            // ② 零消耗:**不发号**(闸门排在铸号之前 ⇒ 不得烧掉一个 instance_id)。
            //    `IIdAuthority` 没有 peek 面 ⇒ 用「下一次成功铸造拿到的值」观察:
            //    若被拒那次烧了号,这里会拿到 1 而不是 0。
            HarvestOutcome ok = _orchestrator.Resolve(Req(), HarvestGate.Allow);
            Assert.That(ok.Result.InstanceId.Value, Is.EqualTo(idBefore),
                "被拒的采集不得消耗 instance_id —— 否则连续失败会把 id 空间烧空");
        }

        [Test]
        public void test_ac1707_insufficientRemaining_rejectsNamed_zeroEvents()
        {
            int streamBefore = _bag.Stream.Count;

            HarvestOutcome o = _orchestrator.Resolve(Req(),
                new HarvestGate(hasCapacity: true, hasRemaining: false));

            Assert.That(o.Failure, Is.EqualTo(HarvestFailureKind.InsufficientRemaining),
                "余量不足须与满载**可区分** —— 两者给玩家的正确响应相反(换一株 vs 清箱子)");
            Assert.That(_bag.Stream.Count, Is.EqualTo(streamBefore), "余量不足 ⇒ 零事件");
            Assert.That(_growth.Calls.Count, Is.EqualTo(0));
        }

        [Test]
        public void test_ac1707_bothFalse_reportsFullInventory()
        {
            // 两支都拒时的次序是**明写的设计**(满载优先:那是玩家能立刻处理的)
            HarvestOutcome o = _orchestrator.Resolve(Req(),
                new HarvestGate(hasCapacity: false, hasRemaining: false));

            Assert.That(o.Failure, Is.EqualTo(HarvestFailureKind.FullInventory),
                "两支同时不满足 ⇒ 报满载(次序固定,不随实现偶然变化)");
        }

        // ══════════ 失败的**时序性质**:被拒不得烧掉首采奖励 ══════════

        [Test]
        public void test_rejectedHarvest_doesNotConsumeNoveltyFirstSpot()
        {
            // NoveltyTracker.Query 在首次时**有副作用**(推进 lastSeenTick)。
            // 若闸门排在 Query 之后,一次被拒的采集会把「首次」烧成「冷却期内」——
            // 而流里没有任何事件能解释玩家为何拿不到首采奖励。
            _orchestrator.Resolve(Req(objectId: 7),
                new HarvestGate(hasCapacity: false, hasRemaining: true));

            Assert.That(_growth.Calls.Count, Is.EqualTo(0), "前置:被拒时不该有成长调用");

            HarvestOutcome ok = _orchestrator.Resolve(Req(objectId: 7), HarvestGate.Allow);
            Assert.That(ok.Succeeded, Is.True);

            Assert.That(_growth.Calls.Count, Is.EqualTo(1));
            Assert.That(_growth.Calls[0].Novelty, Is.EqualTo(NoveltyClass.First),
                "被拒的那次不得把「首次」烧掉 —— 成功这次仍须是 First(×3.0)");
        }

        // ══════════ AC-17-07c · 三类各自具名 ══════════

        [Test]
        public void test_ac1707c_threeFailureKinds_areSeparatelyNamed()
        {
            var kinds = new[]
            {
                HarvestFailureKind.FullInventory,
                HarvestFailureKind.InsufficientRemaining,
                HarvestFailureKind.TimeoutUnresolved,
            };

            var seen = new HashSet<int>();
            foreach (HarvestFailureKind k in kinds)
            {
                Assert.That(k, Is.Not.EqualTo(HarvestFailureKind.None), "失败类不得是 None");
                Assert.That(seen.Add((int)k), Is.True, $"失败类 {k} 与已有成员撞值 —— 三支不可区分");
            }

            Assert.That(kinds.Length, Is.EqualTo(3), "GDD §失败收敛 逐字三支,不多不少");
        }

        [Test]
        public void test_ac1707c_rejectNone_isRejected()
        {
            // 「失败」的类别不得是 None —— 否则调用方会把失败读成成功
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => HarvestOutcome.Reject(HarvestFailureKind.None));
        }

        // ══════════ AC-17-08 · 打断零效果(结构保证)══════════

        [Test]
        public void test_ac1708_noCall_noEffect_andNextHarvestSeqStartsAtZero()
        {
            // 打断 = 动作在裁决前终止 ⇒ **根本不调用** Resolve。17 侧没有可回滚的中间态
            // (事件只在裁决成功时发),所以「零效果」是**结构性的**,不是靠补偿。
            // 判据:被拒/未调用之后,下一次成功采集的 gather_seq 仍从 0 起(没烧号)。
            _orchestrator.Resolve(Req(nodeId: 4), new HarvestGate(false, false));

            HarvestOutcome ok = _orchestrator.Resolve(Req(nodeId: 4), HarvestGate.Allow);

            Assert.That(ok.Succeeded, Is.True);
            Assert.That(ok.Result.GatherSeq, Is.EqualTo(0),
                "上一次被拒不得烧掉 gather_seq —— 该节点此前的采集计数仍是 0");
        }

        [Test]
        public void test_successPath_stillEmitsEverything()
        {
            // 负控的对照:闸门放行时三条产出 + 成长**都要**发生 ——
            // 否则「失败零事件」可以被「成功也零事件」这种坏实现骗过去。
            HarvestOutcome ok = _orchestrator.Resolve(Req(nodeId: 9), HarvestGate.Allow);

            Assert.That(ok.Succeeded, Is.True);
            Assert.That(_drop.SpawnCount, Is.EqualTo(1));
            Assert.That(_drop.ClaimCount, Is.EqualTo(1));
            Assert.That(_growth.Calls.Count, Is.EqualTo(1));
        }

        // ══════════ 辅助 ══════════

        private static HarvestRequest Req(
            long tick = 10, int actorId = 1, int nodeId = 0,
            int itemKey = 0, int qty = 1, int objectId = 0, Fix? xpGain = null)
            => new HarvestRequest(
                tick, actorId, nodeId, new WorldPos(3, 0, 7),
                itemKey, qty, outQuality: 2,
                skillId: (int)SkillId.采集, objectId, xpGain ?? Fix.One);

        /// <summary>只计数的 Drop 写入 spy(**不是** 20 的生产写者)。</summary>
        private sealed class SpyDropWriter : IDropWriter
        {
            public int SpawnCount;
            public int ClaimCount;

            public void Spawn(long tick, ItemInstanceId instanceId, WorldPos spawnAnchor,
                              int itemKey, int qty) => SpawnCount++;

            public void Claim(long tick, ItemInstanceId instanceId, int claimer) => ClaimCount++;
        }

        /// <summary>记录成长调用的 spy(捕获 <see cref="NoveltyClass"/> —— 判时序性质用)。</summary>
        private sealed class SpyGrowthEmitter : IGrowthEmitter
        {
            public readonly List<(long Tick, int ActorId, int ObjectId, NoveltyClass Novelty)> Calls
                = new List<(long, int, int, NoveltyClass)>();

            public void Emit(long tick, int actorId, int skillId, int objectId,
                             NoveltyClass novelty, int? level, Fix xpGain)
                => Calls.Add((tick, actorId, objectId, novelty));
        }
    }
}
