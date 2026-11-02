// 2026-11-02 · 结清 `TD-interaction-02` —— 掉落进候选集,由 4 的全序一次选中
//
// 判据(逐条可证伪):
//   ① 半径内有掉落 ⇒ `Select` 选中 Drop ⇒ 交 20 结算**被指名的那一件**(恰一条 `DropClaimed`);
//   ② 同格「病人 + 掉落」⇒ 第二键 `KindPriority` 定胜负(Patient=0 < Drop=9)⇒ 病人赢,**零**拾取;
//   ③ **掉落更近就归掉落**(d=0 的掉落 vs d=1 的病人)——
//      旧形态(候选人只看得见病人 + 「选择器空 ⇒ 拾取兜底」)在这条上会**就诊**而不拾取;
//   ④ 掉落「在交互半径内、但在 20 R7 回捡半径外」⇒ **不进候选集**:
//      这一条防的是「它赢得全序、结算时又被拒 ⇒ 按键被静默吞掉」;
//   ⑤ 无候选 ⇒ 返回 false —— **不再**偷偷去摸一件掉落(兜底段已删)。
//
// ⚠️ 用**真**拾取编排(`_bag.DropClaimOrchestrator`,容差 = 装配常量 1)与**真**
//   `GroundDropSource`(锚点读真流)—— 本类要证的正是「生产装配下」这条链,
//   替身会把要证的东西换成自己。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>掉落作为候选源后的路由判据(`TD-interaction-02`)。</summary>
    public sealed class interaction_press_drop_test
    {
        private static readonly WorldPos Player = new WorldPos(10, 0, 10);
        private const int Radius = 3;      // 与 interaction_kinds.json 的 r_interact 同值
        private const int ActorId = 7;
        private const long Tick = 1;

        private CompositionRootServices _bag;
        private DropEventWriter _drops;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _drops = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
        }

        [TearDown]
        public void TearDown() => _bag?.Dispose();

        /// <summary>在地上放一件掉落物(真流事件;保持 NoOwner)。</summary>
        private long SeedDrop(WorldPos cell) => _drops.RecordSpawned(0, cell, itemKey: 3, qty: 1).Value;

        private InventoryProjection Projection()
            => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder, InventoryFoldContext.None).Projection;

        private int CountOf(EventKind kind)
        {
            int n = 0;
            foreach (SimEvent e in _bag.Stream.Events) if (e.Kind == kind) n++;
            return n;
        }

        // ══════════════ ① 掉落进候选集 ⇒ 被选中 ⇒ 结算被指名的那一件 ══════════════

        [Test]
        public void test_dropInRange_routedToDrop_andClaimed()
        {
            long dropId = SeedDrop(Player);
            Assert.AreEqual(InventoryProjection.NoOwner, Projection().TryGet(dropId, out InventoryRow row)
                ? row.Owner : -2, "[前置] 掉落在地上(未归属)");
            int before = CountOf(EventKind.DropClaimed);

            var visit = new List<PatientId>();
            InteractionPressRouter router = Build(patientCell: null, patientId: 0, forageCell: null,
                                                  forageIndex: 0, onVisit: p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Drop, router.LastRoutedKind,
                "掉落现在**在候选集里**(旧形态:Drop 源为 null ⇒ 它从不参与全序)");
            Assert.IsTrue(consumed, "选中掉落且结算通过 ⇒ 按键被消费");
            Assert.AreEqual(before + 1, CountOf(EventKind.DropClaimed), "恰一条 DropClaimed");
            Assert.IsTrue(Projection().TryGet(dropId, out InventoryRow after)
                && after.Owner == ActorId, "被指名的那一件归拾取者");
            Assert.AreEqual(0, visit.Count, "拾取不得同时触发就诊");
        }

        // ══════════════ ①b 两件掉落:结算**被选中的那件**,不是 id 最小者 ══════════════

        [Test]
        public void test_twoDrops_claimsTheSelectedOne_notTheLowestId()
        {
            // 先播远的(d=1,先铸 ⇒ 小 id),再播近的(d=0,大 id)。
            // 「第一可拾(id 升序)」会拾远的;全序 argmin 必须拾近的。
            long far = SeedDrop(new WorldPos(Player.X + 1, Player.Y, Player.Z));
            long near = SeedDrop(Player);
            Assert.Less(far, near, "[前置] 小 id 属于**远**的那件(判别力来源)");

            InteractionPressRouter router = Build(null, 0, null, 0, _ => { });
            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Drop, router.LastRoutedKind);
            Assert.IsTrue(consumed, "[前置] 有可拾掉落 ⇒ 按键被消费");
            Assert.IsTrue(Projection().TryGet(near, out InventoryRow n) && n.Owner == ActorId,
                "拾到的是**全序选中的**那件(d∞ = 0)");
            Assert.AreEqual(InventoryProjection.NoOwner,
                Projection().TryGet(far, out InventoryRow f) ? f.Owner : -2,
                "远的那件仍在地上 —— 若它被拾走,说明结算按 id 序而非选择结果(「第一可拾」复发)");
        }

        // ══════════════ ② 同格:KindPriority 定胜负 ══════════════

        [Test]
        public void test_sameCell_patientBeatsDrop_byKindPriority()
        {
            long dropId = SeedDrop(Player);
            var visit = new List<PatientId>();
            InteractionPressRouter router = Build(Player, patientId: 99, forageCell: null,
                                                  forageIndex: 0, onVisit: p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Patient, router.LastRoutedKind,
                "同格 ⇒ 第二键 KindPriority(Patient=0 < Drop=9)判病人赢;读到 Drop 说明第二键没接线");
            Assert.IsTrue(consumed);
            Assert.AreEqual(1, visit.Count, "恰一次就诊");
            Assert.AreEqual(0, CountOf(EventKind.DropClaimed), "病人赢 ⇒ 掉落**不**被拾(一次按键一个动作)");
        }

        // ══════════════ ③ 掉落更近 ⇒ 归掉落(旧形态会就诊)══════════════

        [Test]
        public void test_dropCloserThanPatient_dropWinsByDistance()
        {
            long dropId = SeedDrop(Player);                                        // d∞ = 0
            var visit = new List<PatientId>();
            InteractionPressRouter router = Build(
                new WorldPos(Player.X + 1, Player.Y, Player.Z), patientId: 7,
                forageCell: null, forageIndex: 0, onVisit: p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.Drop, router.LastRoutedKind,
                "第一键 d∞ 压过种类:脚下的掉落必须赢过一格外的病人 —— " +
                "旧形态(病人独享候选集 + 拾取兜底)在这条上会去就诊,掉落永远捡不到");
            Assert.IsTrue(consumed);
            Assert.AreEqual(0, visit.Count, "打给掉落时不得同时就诊");
            Assert.IsTrue(Projection().TryGet(dropId, out InventoryRow r) && r.Owner == ActorId);
        }

        // ══════════════ ④ 交互半径内、回捡半径外 ⇒ 不进候选集 ══════════════

        [Test]
        public void test_dropBeyondClaimTolerance_excludedFromCandidates_notSwallowingPress()
        {
            SeedDrop(new WorldPos(Player.X + 2, Player.Y, Player.Z));   // d∞ = 2 > 容差 1,但 ≤ R=3
            var visit = new List<PatientId>();
            InteractionPressRouter router = Build(
                patientCell: null, patientId: 0,
                forageCell: new WorldPos(Player.X + 3, Player.Y, Player.Z), forageIndex: 1,
                onVisit: p => visit.Add(p));

            router.OnPress();

            Assert.AreEqual(InteractableKind.ForageSpot, router.LastRoutedKind,
                "回捡半径(20 R7)外的掉落**不得**参与全序 —— 否则它会赢(d=2)、结算被拒, " +
                "把这一下按键静默吞掉(而更远的采集点明明可用)");
            Assert.AreEqual(0, CountOf(EventKind.DropClaimed), "被排除 ⇒ 零拾取");
        }

        // ══════════════ ⑤ 无候选 ⇒ 不消费、零拾取 ══════════════

        [Test]
        public void test_noCandidates_false_noDropFallback()
        {
            // ⚠️ **本条的判别力有限,如实登记**:「选择器空 ⇒ 拾取兜底」这一段的对立面
            //    在本夹具里**观测不到** —— 只要 Drop 进了候选集,「选择器空」与「有可拾掉落」
            //    就不再同时成立。兜底段被删的最强证据是**结构性**的:
            //    `DropClaimOrchestrator` 已**没有** `TryClaim`(「第一可拾」入口整体不存在),
            //    路由里唯一入口是 `case Drop:` 内的 `TryClaimInstance`。
            //    本条只证「无候选时按键落空且零事件」。
            SeedDrop(new WorldPos(Player.X + 5, Player.Y, Player.Z));

            int before = CountOf(EventKind.DropClaimed);
            var visit = new List<PatientId>();
            InteractionPressRouter router = Build(null, 0, null, 0, p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.IsFalse(consumed, "无候选 ⇒ 不消费");
            Assert.IsNull(router.LastRoutedKind, "无选中种类");
            Assert.AreEqual(before, CountOf(EventKind.DropClaimed), "零拾取(不得偷偷去摸一件掉落)");
            Assert.AreEqual(0, visit.Count);
        }

        // ══════════════════════════ 辅助 ══════════════════════════

        /// <param name="patientCell"><c>null</c> = 无在场病人。</param>
        /// <param name="forageCell"><c>null</c> = 无资源点。</param>
        private InteractionPressRouter Build(WorldPos? patientCell, long patientId,
                                             WorldPos? forageCell, long forageIndex,
                                             Action<PatientId> onVisit)
        {
            var presence = new PresenceRegistry();
            if (patientCell.HasValue)
                presence.AddPresent(new PatientId((int)patientId), patientCell.Value);

            var drops = new GroundDropSource(
                () => _bag.Stream.Events, _bag.Decoder,
                () => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder,
                                         InventoryFoldContext.None).Projection);

            var loader = new CandidateSetLoader(new CandidateSources(
                drops: drops,
                worldBaked: new FakeWorldSource(forageCell, forageIndex),
                patients: new PresencePatientSource(presence),
                bakedInitial: null));

            var radius = new InteractionRadius(Radius);
            var selector = new InteractionSelector(UnwiredDiscoveryReporter.Instance, radius);

            // 采集驱动传 null(本夹具断种类与拾取,不断 17 的结算)⇒ 种类 = ForageSpot 时
            // `OnPress` 返回 false 属**合规但不消费**。
            return new InteractionPressRouter(
                () => Player, () => Tick, ActorId,
                loader, selector, radius,
                drops: _bag.DropClaimOrchestrator,   // 真编排(容差 = 装配常量)
                harvest: null,
                visit: onVisit);
        }

        private sealed class FakeWorldSource : IWorldBakedSource
        {
            private readonly ForageSpotDto[] _forage;

            public FakeWorldSource(WorldPos? cell, long index)
                => _forage = cell.HasValue
                    ? new[] { new ForageSpotDto(cell.Value, index) }
                    : Array.Empty<ForageSpotDto>();

            public IReadOnlyList<PoiCellDto> PoiCells => Array.Empty<PoiCellDto>();
            public IReadOnlyList<ForageSpotDto> ForageSpots => _forage;
            public IReadOnlyList<BuildSlotDto> BuildSlots => Array.Empty<BuildSlotDto>();
        }
    }
}
