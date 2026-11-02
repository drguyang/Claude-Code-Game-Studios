// 2026-11-02 · 结清 `TD-interaction-01`(4 的确定性全序目标选择落成**唯一一处**)
//
// 债册的三条消解判据:
//   ① 病人的 `ICandidateSource` 有生产实装 —— `PresencePatientSource`
//      (夹具见 §三 `test_patientSource_*`)
//   ② `InteractClaimRoute` 的比距仲裁被删除,由**一次 `Select`** 直接决定种类
//      —— 本文件 §一 / §二 直接对 `InteractionPressRouter` 断「种类 → 分派」
//   ③ 夹具证明「同格病人 + 采集点 ⇒ 按 `KindPriority` 表定胜负」—— §一 `test_ir_01`
//
// ⚠️ **本夹具刻意让 `StableId` 与胜负相反**(病人 id 99 > 资源点 id 1):
//    若第二键 `KindPriority` 没接线,第三键会让资源点胜 ⇒ 红。这是 §一 的判别力来源。
//    仅「病人赢」一句在两边 id 巧合时也会绿 —— 那是本仓点名的空洞正控形态。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Player;   // MotorLease / LeaseSource(TD-interaction-04)
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>E 按下沿的统一路由(`InteractionPressRouter`)与病人类候选源。</summary>
    public sealed class interaction_press_router_test
    {
        private static readonly WorldPos Player = new WorldPos(10, 0, 10);

        // ══════════════ 一、同格:tie 由 KindPriority 定,不是 StableId ══════════════

        [Test]
        public void test_ir_01_sameCell_patientWinsByKindPriority_notByStableId()
        {
            // Arrange:病人与资源点**同格**(第一键相等)。
            //   病人 id = 99、资源点 index = 1 —— 第三键(StableId)会判资源点胜。
            //   只有第二键(KindPriority: Patient=0 < ForageSpot=6)能让病人胜。
            var visit = new List<PatientId>();
            var router = Build(
                patientCell: Player,
                patientId: 99,
                forageCell: Player,
                forageIndex: 1,
                onVisit: p => visit.Add(p));

            // Act
            bool consumed = router.OnPress();

            // Assert
            Assert.AreEqual(InteractableKind.Patient, router.LastRoutedKind,
                "同格 ⇒ 第二键 KindPriority 定胜负(Patient=0 < ForageSpot=6);" +
                "若读到 ForageSpot,说明第二键没接线、由第三键 StableId 代判了");
            Assert.IsTrue(consumed, "选中病人 ⇒ 按键被消费");
            Assert.AreEqual(1, visit.Count, "恰一次就诊回调");
            Assert.AreEqual(99, visit[0].Value, "就诊对象 = 选择器给出的那个病人");
        }

        // ══════════════ 二、距离优先:采集更近就归采集 ══════════════

        [Test]
        public void test_ir_02_forageCloser_kindIsForage_andVisitNotCalled()
        {
            var visit = new List<PatientId>();
            var router = Build(
                patientCell: new WorldPos(Player.X + 2, Player.Y, Player.Z),
                patientId: 7,
                forageCell: Player,                      // d∞ = 0
                forageIndex: 1,
                onVisit: p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.AreEqual(InteractableKind.ForageSpot, router.LastRoutedKind,
                "第一键 d∞ 压过第二键:采集点更近 ⇒ 种类 = ForageSpot");
            Assert.IsFalse(consumed,
                "种类 = ForageSpot 而采集驱动未接线 ⇒ **合规但不消费**(诚实降级);" +
                "⚠️ 本条断的是「种类归属」,不是 17 的结算 —— 非 false 才说明它偷偷触发了别的动作");
            Assert.AreEqual(0, visit.Count, "打给采集点时**不得**同时触发就诊(一次按键一个动作)");
        }

        [Test]
        public void test_ir_03_patientCloser_kindIsPatient()
        {
            var visit = new List<PatientId>();
            var router = Build(
                patientCell: Player,                     // d∞ = 0
                patientId: 7,
                forageCell: new WorldPos(Player.X + 1, Player.Y, Player.Z),
                forageIndex: 1,
                onVisit: p => visit.Add(p));

            router.OnPress();

            Assert.AreEqual(InteractableKind.Patient, router.LastRoutedKind,
                "病人更近 ⇒ 种类 = Patient(距离先于种类)");
            Assert.AreEqual(1, visit.Count, "就诊被调用");
        }

        // ══════════════ 三、无候选 / 出半径 ══════════════

        [Test]
        public void test_ir_04_noCandidate_false_noVisit()
        {
            var visit = new List<PatientId>();
            var router = Build(
                patientCell: null,
                patientId: 0,
                forageCell: null,
                forageIndex: 0,
                onVisit: p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.IsFalse(consumed, "无候选 ⇒ 不消费(拾取编排也未接线 ⇒ false)");
            Assert.IsNull(router.LastRoutedKind, "无选中种类");
            Assert.AreEqual(0, visit.Count, "无候选不得触发任何动作");
        }

        [Test]
        public void test_ir_05_outsideRadius_ignored()
        {
            // 半径 = 3;病人与资源点都在 d∞ = 4 处 ⇒ 裁剪后为空集。
            var far = new WorldPos(Player.X + 4, Player.Y, Player.Z);
            var visit = new List<PatientId>();
            var router = Build(
                patientCell: far, patientId: 7,
                forageCell: far, forageIndex: 1,
                onVisit: p => visit.Add(p));

            bool consumed = router.OnPress();

            Assert.IsFalse(consumed,
                "半径外的候选**必须**被裁掉(裁剪发生在 argmin 之前);" +
                "非 false = 裁剪没生效,远处目标会被隔墙选中");
            Assert.IsNull(router.LastRoutedKind);
            Assert.AreEqual(0, visit.Count);
        }

        // ══════════════ 四、病人类候选源的生产实装 ══════════════

        [Test]
        public void test_patientSource_exposesPresentPatientsWithCells()
        {
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(5), new WorldPos(1, 2, 3));
            presence.AddPresent(new PatientId(8), new WorldPos(4, 5, 6));

            IPatientSource source = new PresencePatientSource(presence);
            IReadOnlyList<PatientDto> rows = source.Rows;

            Assert.AreEqual(2, rows.Count, "两个在场病人 ⇒ 两行");

            var byId = new Dictionary<long, WorldPos>();
            foreach (var r in rows) byId[r.PatientId] = r.Cell;
            Assert.AreEqual(new WorldPos(1, 2, 3), byId[5], "patient 5 的整数格");
            Assert.AreEqual(new WorldPos(4, 5, 6), byId[8], "patient 8 的整数格");
        }

        [Test]
        public void test_patientSource_reflectsRemoval_noCache()
        {
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(5), new WorldPos(1, 2, 3));

            IPatientSource source = new PresencePatientSource(presence);
            Assert.AreEqual(1, source.Rows.Count, "[前置] 一个在场病人");

            presence.RemovePresent(new PatientId(5));

            Assert.AreEqual(0, source.Rows.Count,
                "离场后候选必须**立刻**消失 —— 候选集是派生态(ADR-009 §四),缓存即第二真源");
        }

        // ══════════════════════════ 辅助 ══════════════════════════

        /// <summary>半径 = 3(与 `interaction_kinds.json` 的 `r_interact` 同值,便于对读)。</summary>
        private const int Radius = 3;

        /// <param name="patientCell"><c>null</c> = 该源无行。</param>
        /// <param name="forageCell"><c>null</c> = 该源无行。</param>
        // ══════════════ 三、4 的定身写面(`TD-interaction-04` · 2026-11-02)══════════════
        //
        // 病因:`AC-1-23` 的白名单把 `Gameplay.Boot` 挡在外面(门按**调用点所在程序集**判),
        // 而 4 的写面 `ModalGate.SetMotorSuppression` 自 story-005 起**零生产调用方** ⇒
        // 「模态打开 ⇒ 不能走」在 GDD 规则八里被声称、在代码里**不存在**。
        // 现口径:Boot 每 tick 算一个**布尔**,交给 Presentation 的写面 ⇒ 白名单不动。
        //
        // ⚠️ 本组钉的是**合成判据 + 写面接线**两件。第三件(采集窗口在途 ⇒ 置位)
        //    的输入 `harvest.IsHolding` 由 `TD-foraging-04` 的夹具自己钉;
        //    这里钉「它被合进同一个布尔」的那一半(真值表第 4 行)。

        /// <summary>合成判据的真值表 —— 四种组合逐一断言(不靠「两个 true 就够」)。</summary>
        [Test]
        public void test_tdi04_shouldSuppressMotor_truthTable()
        {
            Assert.IsFalse(InteractionPressRouter.ShouldSuppressMotor(false, false),
                "模态关 + 无采集窗口 ⇒ 不压制");
            Assert.IsTrue(InteractionPressRouter.ShouldSuppressMotor(true, false),
                "模态开 ⇒ 压制");
            Assert.IsTrue(InteractionPressRouter.ShouldSuppressMotor(false, true),
                "采集窗口在途 ⇒ 压制");
            Assert.IsTrue(InteractionPressRouter.ShouldSuppressMotor(true, true),
                "两者皆真 ⇒ 压制");
        }

        /// <summary>`OnTick` 把模态开合**如实**反映到 4 的位上(置 / 清双向)。</summary>
        [Test]
        public void test_tdi04_onTick_bindsModalStateToSelfLease()
        {
            var lease = new MotorLease();
            bool modalOpen = false;
            InteractionPressRouter router = Build(null, 0L, null, 0L, _ => { });
            router.BindMotorSuppression(lease, () => modalOpen);

            router.OnTick();
            Assert.IsFalse(lease.HasLease(LeaseSource.Self), "[前置] 模态关 ⇒ 4 的位置空");

            modalOpen = true;
            router.OnTick();
            Assert.IsTrue(lease.HasLease(LeaseSource.Self), "模态开 ⇒ 4 的位被置");

            modalOpen = false;
            router.OnTick();
            Assert.IsFalse(lease.HasLease(LeaseSource.Self),
                "模态关 ⇒ 4 的位**被清**(每 tick 重算 ⇒ 不存在「漏释放」)");
        }

        /// <summary>未绑定 ⇒ **不写租约**(诚实降级:不压制,而不是压住不放)。</summary>
        [Test]
        public void test_tdi04_onTick_unboundWritesNothing()
        {
            var lease = new MotorLease();
            InteractionPressRouter router = Build(null, 0L, null, 0L, _ => { });

            router.OnTick();   // 未 BindMotorSuppression

            Assert.IsFalse(lease.IsSuppressed, "未绑定 ⇒ 租约纹丝不动(不得凭空置位)");
        }

        /// <summary>只碰 4 那一位 —— 不得顺手清掉 10 的压制(`OQ-4-13` 纪律①)。</summary>
        [Test]
        public void test_tdi04_onTick_doesNotTouchOtherSources()
        {
            var lease = new MotorLease();
            lease.Acquire(LeaseSource.Emergency);      // 10 持有
            InteractionPressRouter router = Build(null, 0L, null, 0L, _ => { });
            router.BindMotorSuppression(lease, () => false);

            router.OnTick();                            // 4 判「不压制」

            Assert.IsTrue(lease.HasLease(LeaseSource.Emergency),
                "10 的位不得被 4 的同步顺手清掉(单 bool + 多写者 = 丢失更新,OQ-4-13)");
            Assert.IsFalse(lease.HasLease(LeaseSource.Self), "4 自己那一位置空");
        }

        private static InteractionPressRouter Build(
            WorldPos? patientCell, long patientId,
            WorldPos? forageCell, long forageIndex,
            System.Action<PatientId> onVisit)
        {
            var patients = new FakePatientSource(patientCell, patientId);
            var world = new FakeWorldSource(forageCell, forageIndex);

            var loader = new CandidateSetLoader(new CandidateSources(
                drops: null,
                worldBaked: world,
                patients: patients,
                bakedInitial: null));

            var radius = new InteractionRadius(Radius);
            var selector = new InteractionSelector(UnwiredDiscoveryReporter.Instance, radius);

            // ⚠️ 采集驱动 / 拾取编排传 null:本夹具断的是**种类分派与选择**,
            //    不是 17 / 20 的结算(那两条各有自己的夹具)。
            //    种类 = ForageSpot 时 OnPress 会因 `_harvest == null` 返回 false —— 这是
            //    「合规但不消费」的诚实降级,§二 的断言据此写。
            return new InteractionPressRouter(
                () => Player,
                () => 1L,
                actorId: 3,
                loader, selector, radius,
                drops: null,
                harvest: null,
                visit: onVisit);
        }

        private sealed class FakePatientSource : IPatientSource
        {
            private readonly WorldPos? _cell;
            private readonly long _id;

            public FakePatientSource(WorldPos? cell, long id) { _cell = cell; _id = id; }

            public IReadOnlyList<PatientDto> Rows
                => _cell.HasValue
                    ? new List<PatientDto> { new PatientDto(_cell.Value, _id) }
                    : new List<PatientDto>();
        }

        private sealed class FakeWorldSource : IWorldBakedSource
        {
            private readonly ForageSpotDto[] _forage;

            public FakeWorldSource(WorldPos? forageCell, long index)
                => _forage = forageCell.HasValue
                    ? new[] { new ForageSpotDto(forageCell.Value, index) }
                    : new ForageSpotDto[0];

            public IReadOnlyList<PoiCellDto> PoiCells => new List<PoiCellDto>();
            public IReadOnlyList<ForageSpotDto> ForageSpots => _forage;
            public IReadOnlyList<BuildSlotDto> BuildSlots => new List<BuildSlotDto>();
        }
    }
}
