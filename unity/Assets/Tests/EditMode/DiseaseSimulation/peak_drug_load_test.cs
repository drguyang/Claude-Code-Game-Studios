// `TD-sim-02` 的消解判据 —— 9 侧「同一味药的最大当前体内药量」。
//
// 权威来源:
//   `TD-sim-02` 原文:「9 的配方 / 数值轮给 F1 增一条毒性项,**或**另立一个可读的
//     『累积过量』状态量 …… 该量的语义**不得与 `Progress` 单值纠缠**」
//   用户 2026-10-30 两项裁定:① 走**独立状态量**(不进 `Progress`);
//     ② 「过量」按**同一味药**(`处置_id`)累积 —— P0 无「槽」结构(12 药物槽是 P1a)。
//   `ADRs-035 §⑥` 的解除条件由此条与 `TD-prescription-09` 共同满足。
//
// ⚠️ 本文件只证**这个量本身**;「9 → 53 的交付」由 `VitalsDto.PeakDrugLoad` 的接线与
//   53 侧夹具覆盖。分开的理由:本量是纯函数,可以在无装配的条件下逐条钉死。

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>`ProgressionEvaluator.ComputePeakDrugLoad` 的判据。</summary>
    [TestFixture]
    internal sealed class PeakDrugLoadTest
    {
        private const int DrugA = 7;
        private const int DrugB = 9;

        /// <summary>`Fix` 从「整数分子 / 分母」构造,避免浮点字面量进夹具。</summary>
        private static Fix Ratio(int num, int den) => new Fix((long)num * Fix.OneRaw / den);

        private static TreatmentDose Dose(long tick, int treatmentId, int num, int den, long halfLife)
            => new TreatmentDose(tick, treatmentId, polarity: 1, Ratio(num, den), halfLife);

        // ══════════════════ 空集 ══════════════════

        [Test]
        public void test_noDoses_isZero()
        {
            Assert.That(ProgressionEvaluator.ComputePeakDrugLoad(null, 100).Raw, Is.Zero);
            Assert.That(ProgressionEvaluator.ComputePeakDrugLoad(new List<TreatmentDose>(), 100).Raw, Is.Zero);
        }

        // ══════════════════ 单剂 ══════════════════

        [Test]
        public void test_singleDose_atItsOwnTick_equalsAbsolutePotency()
        {
            // Decay(Δ=0) = 1 ⇒ load = |potency|
            var doses = new List<TreatmentDose> { Dose(tick: 50, DrugA, 1, 2, halfLife: 600) };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 2).Raw), "Δ=0 时衰减为 1,载量应恰等于 |potency|");
        }

        [Test]
        public void test_negativePotency_usesAbsoluteValue()
        {
            // 对症药 potency 为负(降 Progress)——「体内积了多少药」与方向无关。
            var doses = new List<TreatmentDose> { Dose(tick: 50, DrugA, -1, 2, halfLife: 600) };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 2).Raw), "负 potency 须取绝对值");
        }

        [Test]
        public void test_decaysByHalfAtOneHalfLife()
        {
            // Decay(half_life, half_life) = e^-1 ≈ 0.3679,不是 0.5 ——
            // `halfLife` 在这个模型里是**指数时间常数**,不是「半衰期」的字面语义。
            // 本测只钉「严格单调下降且落在 (0, |potency|)」,不钉具体常数(那是 Fix.Exp 的事)。
            var doses = new List<TreatmentDose> { Dose(tick: 0, DrugA, 1, 1, halfLife: 600) };
            Fix at0 = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 0);
            Fix at600 = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 600);
            Fix at1200 = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 1200);

            Assert.That(at600.Raw, Is.LessThan(at0.Raw), "过一个时间常数后载量须下降");
            Assert.That(at600.Raw, Is.GreaterThan(0), "尚未衰减到零");
            Assert.That(at1200.Raw, Is.LessThan(at600.Raw), "继续单调下降");
        }

        // ══════════════════ 分组语义(本件的核心) ══════════════════

        [Test]
        public void test_sameDrug_accumulates()
        {
            // 同一味药两剂(同 tick)⇒ 叠加。
            var doses = new List<TreatmentDose>
            {
                Dose(tick: 50, DrugA, 1, 4, halfLife: 600),
                Dose(tick: 50, DrugA, 1, 4, halfLife: 600),
            };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 2).Raw), "同药两剂 1/4 + 1/4 应叠成 1/2");
        }

        [Test]
        public void test_differentDrugs_takesMaxNotSum()
        {
            // 不同药不叠加 —— 取最大。「吃两味不同的药各一剂」不是「同一味药灌两剂」。
            // 这是用户 2026-10-30 裁定的「同类药」粒度:
            // 若改成任意药累加,本断言会红(1/2 + 1/4 = 3/4)。
            var doses = new List<TreatmentDose>
            {
                Dose(tick: 50, DrugA, 1, 2, halfLife: 600),
                Dose(tick: 50, DrugB, 1, 4, halfLife: 600),
            };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 2).Raw), "不同药取最大(1/2),不得求和(3/4)");
        }

        [Test]
        public void test_threeDrugs_peakIsTheLargestGroup()
        {
            // A 两剂(1/4 + 1/4 = 1/2)· B 一剂(1/2)· C 一剂(1/8)⇒ 峰值 = 1/2
            var doses = new List<TreatmentDose>
            {
                Dose(tick: 10, DrugA, 1, 4, halfLife: 600),
                Dose(tick: 10, DrugA, 1, 4, halfLife: 600),
                Dose(tick: 10, DrugB, 1, 2, halfLife: 600),
                Dose(tick: 10, 11, 1, 8, halfLife: 600),
            };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 10);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 2).Raw), "三组各自求和后取最大 ⇒ 1/2");
        }

        // ══════════════════ 边界 ══════════════════

        [Test]
        public void test_futureDose_ignored()
        {
            // 未来剂量不计 —— 否则读档重放会「预支」尚未发生的用药。
            var doses = new List<TreatmentDose>
            {
                Dose(tick: 50, DrugA, 1, 4, halfLife: 600),
                Dose(tick: 900, DrugA, 3, 4, halfLife: 600),   // 未来
            };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 4).Raw), "未来那一剂不得计入");
        }

        [Test]
        public void test_nonPositiveHalfLife_skipped()
        {
            // half_life ≤ 0 ⇒ Decay 除零;写入期 AC-28 已拒,此处是纵深防御(跳过而非抛)。
            var doses = new List<TreatmentDose>
            {
                Dose(tick: 50, DrugA, 1, 2, halfLife: 0),
                Dose(tick: 50, DrugB, 1, 4, halfLife: 600),
            };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 4).Raw), "half_life = 0 的那剂应被跳过,不炸整帧");
        }

        [Test]
        public void test_emptyTreatmentIdGrouping_isNotSpecialCased()
        {
            // 分组键是 TreatmentId 原值 —— 不因它「看起来像哨兵」就特殊处理。
            var doses = new List<TreatmentDose>
            {
                Dose(tick: 50, -1, 1, 4, halfLife: 600),
                Dose(tick: 50, -1, 1, 4, halfLife: 600),
            };
            Fix load = ProgressionEvaluator.ComputePeakDrugLoad(doses, currentTick: 50);
            Assert.That(load.Raw, Is.EqualTo(Ratio(1, 2).Raw), "同 id(哪怕为负)照样同组累加");
        }
    }
}
