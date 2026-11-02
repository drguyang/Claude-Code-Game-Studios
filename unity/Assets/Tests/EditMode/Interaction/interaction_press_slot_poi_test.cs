// 2026-11-02 · `TD-interaction-03` —— 第二源补齐后的两条**新接线**(POI / 建造槽位)
//
// 判据(逐条可证伪):
//   ① 站在 POI 格 ⇒ 种类 = `PoiCell`,按键**被消费**,且选择器向 `IDiscoveryReporter`
//      恰发**一条**请求(poi_id 正确)—— 4 的唯一出境面(AC-4-02/A-4-12);
//   ② 站在建造槽位 ⇒ 种类 = `BuildSlot`,消费面收到该格(`BuildInputDriver.AimAt`);
//   ③ 未接消费面 ⇒ 返回 false(按键落空,**不**静默吞掉);
//   ④ 同格「病人 + POI」⇒ `KindPriority` 定胜负(Patient=0 < PoiCell=1)。
//
// ⚠️ 本件**不**测 6 的状态机落流 —— 那是 `poi_discovery_reporter_test`(真 `PoiStateMachine`)的面。
//   这里只钉「4 该发的发了、该分的分了」。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>第二源(POI / 建造槽位)接进路由后的判据。</summary>
    public sealed class interaction_press_slot_poi_test
    {
        private static readonly WorldPos Player = new WorldPos(5, 0, 5);
        private const int Radius = 3;

        // ══════════════ ① POI:选中即出境(发现请求)══════════════

        [Test]
        public void test_poiOnCell_routesPoiCell_andEmitsOneDiscoveryRequest()
        {
            var reporting = new RecordingReporter();
            var router = Build(patientCell: null, patientId: 0,
                               poiCell: Player, poiId: 7,
                               slotCell: null, onAim: null, reporter: reporting);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.PoiCell, router.LastRoutedKind);
            Assert.IsTrue(consumed, "POI 的出境由 `Select` 内的 reporter 完成 ⇒ 按键算「已消费」");
            Assert.AreEqual(1, reporting.Requests.Count,
                "恰一条发现请求(一次交互一次请求;AC-4-13 的节奏面归 6)");
            Assert.AreEqual(7L, reporting.Requests[0].PoiId, "poi_id = 被选中的那一个");
            Assert.AreEqual(Player, reporting.Requests[0].EvidenceCell, "证据格 = 玩家经流确立格");
        }

        // ══════════════ ② 建造槽位:消费面收到该格 ══════════════

        [Test]
        public void test_buildSlotOnCell_routesBuildSlot_andAimsThatCell()
        {
            var aimed = new List<WorldPos>();
            var router = Build(null, 0, poiCell: null, poiId: 0,
                               slotCell: Player, onAim: aimed.Add, reporter: null);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.BuildSlot, router.LastRoutedKind);
            Assert.IsTrue(consumed, "槽位有消费面 ⇒ 消费");
            Assert.AreEqual(1, aimed.Count, "恰一次瞄准");
            Assert.AreEqual(Player, aimed[0], "瞄准格 = 被选中的槽位格(不是玩家格)");
        }

        // ══════════════ ③ 未接消费面 ⇒ 不消费(诚实降级)══════════════

        [Test]
        public void test_buildSlot_withoutConsumer_returnsFalse()
        {
            var router = Build(null, 0, poiCell: null, poiId: 0,
                               slotCell: Player, onAim: null, reporter: null);

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.BuildSlot, router.LastRoutedKind,
                "[前置] 种类确实选到了槽位");
            Assert.IsFalse(consumed,
                "没有消费面 ⇒ 返回 false(按键落空)。**不得**返回 true —— 那会把「没人做事」"
                + "伪装成「已处理」(静默吞键)");
        }

        // ══════════════ ④ 同格:病人压过 POI(KindPriority)══════════════

        [Test]
        public void test_sameCell_patientBeatsPoi_byKindPriority()
        {
            var visits = new List<PatientId>();
            var reporting = new RecordingReporter();
            var router = Build(patientCell: Player, patientId: 9,
                               poiCell: Player, poiId: 3,
                               slotCell: null, onAim: null, reporter: reporting,
                               onVisit: p => visits.Add(p));

            router.OnPress();

            Assert.AreEqual(InteractableKind.Patient, router.LastRoutedKind,
                "同格 ⇒ KindPriority(Patient=0 < PoiCell=1)判病人赢");
            Assert.AreEqual(1, visits.Count, "就诊被调用");
            Assert.AreEqual(0, reporting.Requests.Count, "病人赢 ⇒ POI **不得**被上报(一次交互一个动作)");
        }

        // ══════════════════════════ 辅助 ══════════════════════════

        private static InteractionPressRouter Build(
            WorldPos? patientCell, long patientId,
            WorldPos? poiCell, long poiId,
            WorldPos? slotCell,
            System.Action<WorldPos> onAim,
            RecordingReporter reporter,
            System.Action<PatientId> onVisit = null)
        {
            var presence = new PresenceRegistry();
            if (patientCell.HasValue)
                presence.AddPresent(new PatientId((int)patientId), patientCell.Value);

            var loader = new CandidateSetLoader(new CandidateSources(
                drops: null,
                worldBaked: new FakeWorldSource(poiCell, poiId, slotCell),
                patients: new PresencePatientSource(presence),
                bakedInitial: null));

            var radius = new InteractionRadius(Radius);
            var selector = new InteractionSelector(
                (IDiscoveryReporter)reporter ?? UnwiredDiscoveryReporter.Instance, radius);

            // 采集驱动 / 拾取编排传 null:本夹具断种类与消费面,不断 17 / 20 的结算。
            return new InteractionPressRouter(
                () => Player, () => 1L, actorId: 3,
                loader, selector, radius,
                drops: null, harvest: null,
                visit: onVisit,
                aimBuild: onAim);
        }

        private sealed class RecordingReporter : IDiscoveryReporter
        {
            public readonly List<DiscoveryRequest> Requests = new List<DiscoveryRequest>();
            public void Request(in DiscoveryRequest request) => Requests.Add(request);
        }

        private sealed class FakeWorldSource : IWorldBakedSource
        {
            private readonly PoiCellDto[] _pois;
            private readonly BuildSlotDto[] _slots;

            public FakeWorldSource(WorldPos? poiCell, long poiId, WorldPos? slotCell)
            {
                _pois = poiCell.HasValue
                    ? new[] { new PoiCellDto(poiCell.Value, poiId) }
                    : System.Array.Empty<PoiCellDto>();
                _slots = slotCell.HasValue
                    ? new[] { new BuildSlotDto(slotCell.Value, SlotKey(slotCell.Value)) }
                    : System.Array.Empty<BuildSlotDto>();
            }

            /// <summary>线性键用**本夹具自己的字面量公式**(W=16,H=1;不读生产实现)。</summary>
            private static long SlotKey(WorldPos c) => (long)c.X + 16L * (c.Y + 1L * c.Z);

            public IReadOnlyList<PoiCellDto> PoiCells => _pois;
            public IReadOnlyList<ForageSpotDto> ForageSpots => System.Array.Empty<ForageSpotDto>();
            public IReadOnlyList<BuildSlotDto> BuildSlots => _slots;
        }
    }
}
