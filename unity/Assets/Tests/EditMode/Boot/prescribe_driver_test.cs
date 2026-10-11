// sprint-06 G3 · 处方驱动判据(EditMode · 2026-10-11)
//
// 判据(可证伪 · sprint-06 §2 G3.1):
//   ① 玩家选档 → 落笔 ⇒ `DrugTreatmentApplied` 入病史流(载荷:treatment_id / polarity /
//      drug_potency / half_life 齐 —— 五步走完,主机落流);
//   ② ≥2 档 ⇒ 不同 `dose` 载荷(判据「不同 dose」的剂量自由度半边);
//   ③ 不选档零事件(驱动零自持状态 —— 未落笔 ⇒ 病史流零 `DrugTreatmentApplied`);
//   ④ 无货不能开出(判据 G3.2 —— 删查库存前置 ⇒ 无货仍开出恰红);
//   ⑤ 未知药 / 档位越界 ⇒ 零事件(fail-safe / AC-11-18 ①);
//   ⑥ SkillGrown 生产装配落地(G3.2 · 30)—— 成长门命中 ⇒ SkillGrown 入病史流,
//      writer 不再挂 b7 豁免(豁免 = 红)。
//
// 纯逻辑 EditMode:真 CompositionRoot(生产装配)+ 真 PrescribeRequestDriver +
// 真 PrescribeFlow —— 全生产路径。照 JudgmentWriter 测试先例。

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="PrescribeRequestDriver"/> 判据(G3.1/G3.2)。</summary>
    internal sealed class PrescribeDriverTest
    {
        private CompositionRootServices _bag;
        private PrescribeRequestDriver _driver;
        private PatientId _patient;

        private static readonly ItemKey Drug = MinimalDrugCatalog.DrugKey;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _driver = _bag.PrescribeDriver;

            // 建档 + 在场 + 开案(处方前置:对象在场 + 有病例语境)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);

            // 处方药发放(20 最小库存面:玩家 9001 持 10 份 —— 够五档各开两次)
            _bag.PortionsStore.Grant(playerId: 9001, itemKey: Drug, portions: 10);
        }

        private PrescribeIntent Intent(int doseOrdinal)
            => new PrescribeIntent(actorId: 9001, _patient, Drug, doseOrdinal);

        [Test]
        public void test_prescribe_selectedDose_writesDrugTreatmentApplied_intoHistory()
        {
            int before = _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied);

            var result = _driver.TryPrescribe(Intent(doseOrdinal: 2), tick: 5);

            Assert.IsTrue(result.Applied, "主机上下文 + 在场 + 有货 ⇒ 五步走完");
            // 五步 ④⑤ ⇒ DrugTreatmentApplied + SkillGrown 两条;此处按 Kind 数断言(非总流数)
            Assert.AreEqual(before + 1,
                _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied),
                "DrugTreatmentApplied 多一条");
            SimEvent tail = _bag.Stream.Events
                .Last(e => e.Kind == EventKind.DrugTreatmentApplied);
            Assert.AreEqual(StreamId.History,
                StreamRouting.Of(tail.Kind), "DrugTreatmentApplied 落病史流(StreamRouting.g.cs 纯函数)");

            // 载荷五字段齐 + case 语义
            Assert.IsTrue(PayloadCodec.TryGetPayload(in tail, _bag.BlobPool,
                out DrugTreatmentAppliedPayload p), "载荷解码成功(roundtrip)");
            Assert.AreEqual(_patient.Value, tail.Patient.Value, "patient_id");
            Assert.AreEqual(10, p.TreatmentId, "treatment_id = 处方表 action_id(21a salicylic_acid)");
            Assert.AreEqual((int)PrescriptionPolarity.Symptomatic,
                p.Polarity, "polarity = 处方表条目");
            Assert.AreEqual(9001, p.ActorId, "actor_id = 施予者");
            // 载荷 drug_potency = 经 F-11.1 剂量换算后的 dose_potency(Q16.16):
            //   1/2 强度(2^31)× dose2 ÷ DOSE_BASE = 65536(= 1.0 Q16.16)
            Assert.AreEqual(65536L, p.DrugPotency.Raw,
                "drug_potency = F-11.1 换算(1/2 × dose2 ÷ DOSE_BASE = 1.0 Q16.16)");
            Assert.AreEqual(2L * 65536L, p.HalfLife, "half_life = 21a 档案 2(Q16.16)");
        }

        [Test]
        public void test_prescribe_twoDetents_differInDosePayload()
        {
            // 戥子档位集 = DoseRange(1..5) ⇒ 档序数 2 / 4 ⇒ dose = 2 / 4 ⇒ potency 不同。
            _driver.TryPrescribe(Intent(doseOrdinal: 2), tick: 6);
            _driver.TryPrescribe(Intent(doseOrdinal: 4), tick: 7);

            var doses = _bag.Stream.Events
                .Where(e => e.Kind == EventKind.DrugTreatmentApplied)
                .Select(e => PayloadCodec.TryGetPayload(e, _bag.BlobPool, out DrugTreatmentAppliedPayload p)
                             ? p.DrugPotency.Raw : long.MinValue)
                .ToList();

            Assert.AreEqual(2, doses.Count, "两次落笔 ⇒ 两条 DrugTreatmentApplied");
            Assert.AreNotEqual(doses[0], doses[1],
                "档序数 2 vs 4 ⇒ dose 2 vs 4 ⇒ dose_potency 不同(判据「≥2 档不同 dose」)");
            // 精确性:1/2 × 2 = 1.0 · 1/2 × 4 = 2.0(量纲自检 —— 不是只断言「不同」)
            Assert.AreEqual(65536L, doses[0], "档2 ⇒ dose_potency = 1.0(Q16.16)");
            Assert.AreEqual(2L * 65536L, doses[1], "档4 ⇒ dose_potency = 2.0(Q16.16)");
        }

        [Test]
        public void test_prescribe_noStroke_emitsNothing()
        {
            int before = _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied);

            // 不调用 TryPrescribe —— 驱动零自持状态,不自动产事件

            Assert.AreEqual(before, _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied),
                "未落笔 ⇒ 病史流零事件(判据「不选档零事件」)");
        }

        [Test]
        public void test_prescribe_outOfStock_rejectsWholly()
        {
            // 玩家 2(无库存)—— 处方其它前置全齐(在场 + 有病例)
            PatientId other = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(other, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            int before = _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied);

            var result = _driver.TryPrescribe(
                new PrescribeIntent(actorId: 2, other, Drug, doseOrdinal: 1), tick: 8);

            Assert.IsFalse(result.Applied, "无货 ⇒ 五步不进入(零事件零扣减零成长)");
            Assert.AreEqual(before, _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied),
                "零写入");
        }

        [Test]
        public void test_prescribe_unknownDrug_emitsNothing()
        {
            var unknown = new ItemKey("ghost_herb", ProcessingState.Raw);
            int before = _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied);

            var result = _driver.TryPrescribe(
                new PrescribeIntent(9001, _patient, unknown, doseOrdinal: 1), tick: 9);

            Assert.IsFalse(result.Applied, "未知药 ⇒ 零事件(零提示纪律,不抛)");
            Assert.AreEqual(before, _bag.Stream.Events.Count(e => e.Kind == EventKind.DrugTreatmentApplied));
        }

        [Test]
        public void test_prescribe_outOfRangeDetent_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryPrescribe(Intent(doseOrdinal: 99), tick: 10),
                "档序数越出戥子落点集合 ⇒ 如实抛出(AC-11-18 ①,不 clamp)");
        }

        [Test]
        public void test_prescribe_negativeTick_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryPrescribe(Intent(doseOrdinal: 1), tick: -1),
                "tick<0 = 编程错误,fail-loud");
        }

        [Test]
        public void test_prescribe_growthGateHit_emitsSkillGrown_intoHistory()
        {
            // 处方表 unlockLevel=0 + 装配 skillGrowthPort prescriptionLevel=0 ⇒ 0 ≥ 0 ⇒ GateHit。
            // 成长入口把 SkillGrown 编码 + Append(生产装配 · G3.2)。
            int before = _bag.Stream.Events.Count(e => e.Kind == EventKind.SkillGrown);

            _driver.TryPrescribe(Intent(doseOrdinal: 2), tick: 11);

            SimEvent grown = _bag.Stream.Events
                .LastOrDefault(e => e.Kind == EventKind.SkillGrown);
            Assert.IsNotNull(grown, "GateHit ⇒ SkillGrown 入病史流(G3.2 生产装配)");
            Assert.AreEqual(StreamId.History, StreamRouting.Of(grown.Kind), "SkillGrown 落病史流");
            Assert.IsTrue(PayloadCodec.TryGetPayload(grown, _bag.BlobPool, out SkillGrownPayload s),
                "SkillGrown 载荷解码成功");
            Assert.AreEqual((int)SkillId.处方用药,
                s.SkillId, "skill_id = 处方用药");
            Assert.AreEqual(10, s.ObjectId, "object_id = 处方表 action_id(30 的对象表 = 11 处方表)");
            Assert.AreEqual(SkillGrownPayload.LevelNotGrown, s.Level,
                "未升级 ⇒ Level 哨兵 -1(30 EmitGrowth 缺省)");
            Assert.AreEqual(before + 1, _bag.Stream.Events.Count(e => e.Kind == EventKind.SkillGrown),
                "一次落笔 ⇒ SkillGrown 恰一条(五步 ⑤;④ 为 DrugTreatmentApplied 另测)");
        }
    }
}
