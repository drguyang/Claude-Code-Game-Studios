// random-events Story 003 测试
//
// AC-52-17: Hamilton 拆分
// AC-52-20: ContextMult 只在配额层
// AC-52-21: 强度轴输入闭集
// AC-52-22: EcoTier/Reputation 权重 ≡ 0
// AC-52-38: 先舍入后钳制
// AC-52-36/37: 五边界

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class EventF1PipelineTest
    {
        // AC-52-17: Hamilton 拆分
        [Test]
        public void test_hamiltonSplit_deterministic()
        {
            var quotas1 = EventF1Pipeline.HamiltonSplit(10);
            var quotas2 = EventF1Pipeline.HamiltonSplit(10);

            Assert.AreEqual(quotas1.Length, quotas2.Length);
            for (int i = 0; i < quotas1.Length; i++)
            {
                Assert.AreEqual(quotas1[i], quotas2[i], "两次运行应逐位同");
            }
        }

        // AC-52-17: Hamilton 拆分总和 = windowSize
        [Test]
        public void test_hamiltonSplit_sumEqualsWindowSize()
        {
            long windowSize = 10;
            var quotas = EventF1Pipeline.HamiltonSplit(windowSize);

            long sum = 0;
            foreach (var q in quotas) sum += q;
            Assert.AreEqual(windowSize, sum, "配额总和应 = windowSize");
        }

        // AC-52-17: 并列余额按档序打破
        [Test]
        public void test_hamiltonSplit_tieBreakByTierOrder()
        {
            // windowSize=8, 占比 {3,3,1,1}/8 → 配额 {3,3,1,1}
            var quotas = EventF1Pipeline.HamiltonSplit(8);
            Assert.AreEqual(3, quotas[0], "威胁档配额 = 3");
            Assert.AreEqual(3, quotas[1], "机会档配额 = 3");
            Assert.AreEqual(1, quotas[2], "反应档配额 = 1");
            Assert.AreEqual(1, quotas[3], "灾难档配额 = 1");
        }

        // AC-52-29(BLOCKING) + AC-52-20: ContextGate 只判「全档归零」
        //
        // 🔁 2026-11-05(`TD-randomevents-05`)改写。改前本用例断言
        //   `clinicInput ⇒ EvaluateContextGate == false` —— 那是**全档门**,
        //   与 AC-52-29 逐字相反:「玩家在医馆地块内,威胁档被 ContextMult=0 压制;
        //   **机会 / 反应档照常生成**」。旧断言把 AC 的反面写成了判据,故一并订正。
        [Test]
        public void test_contextGate_onlyAllTierZeroBlocks()
        {
            var input = new F1PipelineInput(0, 10, false, true, false, false, 0);
            Assert.IsTrue(EventF1Pipeline.EvaluateContextGate(input), "出诊中应通过 ContextGate");

            // AC-52-29:在医馆**仍通过**门 —— 被压的是威胁档,不是整条链。
            var clinicInput = new F1PipelineInput(0, 10, false, false, true, false, 0);
            Assert.IsTrue(EventF1Pipeline.EvaluateContextGate(clinicInput),
                "在医馆必须通过 ContextGate —— 机会 / 反应档照常生成(AC-52-29);" +
                "威胁档由配额层压成 0(NoQuota),不是 NoContext");

            var combatInput = new F1PipelineInput(0, 10, false, true, false, true, 0);
            Assert.IsFalse(EventF1Pipeline.EvaluateContextGate(combatInput), "战斗冷却应不通过");
        }

        // AC-52-20: ContextMult **必须真的进配额层**
        //
        // 🔁 2026-11-05(`TD-randomevents-05`)新增。旧夹具只测 `ComputeContextMult` 的
        //   返回值,**从不测它是否被 `Execute` 使用** —— 而改前 `Execute` 调的是无参
        //   `HamiltonSplit(windowSize)` ⇒ 该函数零生产调用方、医馆对配额零影响,夹具全绿。
        //   那是「自造断言顶替 AC 语义层」:断言的是被测函数的输出,不是 AC 要求的落点。
        //   本用例钉的是**管线出口**,不是中间函数。
        [Test]
        public void test_execute_clinic_zeroesThreatQuota_keepsOthers()
        {
            var clinic = new F1PipelineInput(0, 10, false, false, true, false, 0);
            var output = EventF1Pipeline.Execute(clinic, 0, 3);

            Assert.IsTrue(output.ContextGatePassed, "医馆门应通过(AC-52-29)");
            Assert.AreEqual(0, output.Quotas[0], "医馆内威胁档配额必须 = 0(否则 ComputeContextMult 没进管线)");
            Assert.Greater(output.Quotas[1], 0, "机会档照常");
            Assert.Greater(output.Quotas[2], 0, "反应档照常");
            Assert.Greater(output.Quotas[3], 0, "灾难档照常");

            long sum = 0;
            foreach (int q in output.Quotas) sum += q;
            Assert.AreEqual(10, sum, "被压掉的威胁档配额按原比例让给其余档(GDD 规则六之二),总数不丢");
        }

        // AC-52-20: 战斗冷却 ⇒ 全档归零 + 门不过
        [Test]
        public void test_execute_combatCooldown_allQuotasZero_gateFails()
        {
            var combat = new F1PipelineInput(0, 10, false, true, false, true, 0);
            var output = EventF1Pipeline.Execute(combat, 0, 3);

            Assert.IsFalse(output.ContextGatePassed, "战斗冷却门不过");
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(0, output.Quotas[i], $"战斗冷却档 {i} 配额应 = 0");
        }

        // 对照组:不施加任何上下文修饰 ⇒ 配额回到 {3,3,1,1}/8 的原始比例
        [Test]
        public void test_execute_noContext_quotaIsOriginalRatio()
        {
            var plain = new F1PipelineInput(0, 8, false, false, false, false, 0);
            var output = EventF1Pipeline.Execute(plain, 0, 3);

            Assert.AreEqual(3, output.Quotas[0]);
            Assert.AreEqual(3, output.Quotas[1]);
            Assert.AreEqual(1, output.Quotas[2]);
            Assert.AreEqual(1, output.Quotas[3]);
        }

        // AC-52-20: ContextMult 只在配额层进入
        [Test]
        public void test_contextMult_onlyInQuotaLayer()
        {
            // 医馆：威胁档 ×0，机会/反应保留
            var clinicInput = new F1PipelineInput(0, 10, false, false, true, false, 0);
            var mult = EventF1Pipeline.ComputeContextMult(clinicInput);

            Assert.AreEqual(0, mult[0], "医馆威胁档应 ×0");
            Assert.AreEqual(1, mult[1], "医馆机会档应保留");
            Assert.AreEqual(1, mult[2], "医馆反应档应保留");
            Assert.AreEqual(1, mult[3], "医馆灾难档应保留");
        }

        // AC-52-20 / 规则六之二第一行:出诊路径抬威胁 / 机会档
        //
        // 🔁 2026-11-05(`TD-randomevents-05`)新增。改前 `ComputeContextMult` **完全不读
        //   `IsOnExpedition`** ⇒ 那个旗标求值出来没有任何消费方(「零消费方 = AC 未落地」)。
        //   本条钉的是**消费关系**:旗标一真,前两档乘子就动。
        [Test]
        public void test_contextMult_onExpedition_liftsThreatAndOpportunity()
        {
            var onExpedition = new F1PipelineInput(0, 8, false, true, false, false, 0);

            int[] mult = EventF1Pipeline.ComputeContextMult(onExpedition, travelMult: 2);

            Assert.AreEqual(2, mult[0], "威胁档 ×CONTEXT_TRAVEL");
            Assert.AreEqual(2, mult[1], "机会档 ×CONTEXT_TRAVEL");
            Assert.AreEqual(1, mult[2], "反应档不动(GDD 只点名前两档)");
            Assert.AreEqual(1, mult[3], "灾难档不动");
        }

        // 规则六之二:`CONTEXT_TRAVEL` 的 `num ≥ den` ⇒ 整数倍须 ≥ 1
        [Test]
        public void test_contextMult_travelBelowOne_throws()
        {
            var onExpedition = new F1PipelineInput(0, 8, false, true, false, false, 0);

            // ⚠️ `Assert.Throws<T>` 是**精确类型**匹配(`ArgumentOutOfRangeException` 虽派生自
            //    `ArgumentException`,断言 `ArgumentException` 会红)—— 本行钉的就是精确类型。
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EventF1Pipeline.ComputeContextMult(onExpedition, travelMult: 0),
                "0 会把整档压没 —— 那是冷却 / 医馆的语义,不是「出诊」的");
        }

        // AC-52-20: 战斗冷却全档 ×0
        [Test]
        public void test_contextMult_combatCooldown_allZero()
        {
            var combatInput = new F1PipelineInput(0, 10, false, true, false, true, 0);
            var mult = EventF1Pipeline.ComputeContextMult(combatInput);

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(0, mult[i], $"战斗冷却档 {i} 应 ×0");
            }
        }

        // AC-52-38: 先舍入后钳制
        [Test]
        public void test_strengthTier_roundThenClamp()
        {
            // raw 出界 → 钳值
            Assert.AreEqual(1, EventF1Pipeline.ComputeStrengthTier(new Fix(0), 1, 5), "raw=0 应钳到 MIN=1");
            // Fix(99999) ≈ 1.526, Round() = 2, clamp → 2
            Assert.AreEqual(2, EventF1Pipeline.ComputeStrengthTier(new Fix(99999), 1, 5), "raw≈1.526 应舍入到 2");
            // 大值 → 钳到 MAX
            Assert.AreEqual(5, EventF1Pipeline.ComputeStrengthTier(new Fix(1000000), 1, 5), "raw=1000000 应钳到 MAX=5");

            // raw 半分位 → away from zero
            // Fix(32768) = 0.5, Round() = 1 (away from zero)
            Assert.AreEqual(1, EventF1Pipeline.ComputeStrengthTier(new Fix(32768), 1, 5), "raw=0.5 应舍入到 1");
            // Fix(98304) = 1.5, Round() = 2 (away from zero)
            Assert.AreEqual(2, EventF1Pipeline.ComputeStrengthTier(new Fix(98304), 1, 5), "raw=1.5 应舍入到 2");
        }

        // AC-52-36: windowSize=0 ⇒ 构建期硬失败
        [Test]
        public void test_zeroQuota_zeroEvents()
        {
            // windowSize=0 应抛异常（构建期已拒）
            Assert.Throws<ArgumentException>(() => EventF1Pipeline.HamiltonSplit(0));
        }

        // AC-52-37: 四档全零 ⇒ 0 条
        [Test]
        public void test_allTiersZero_zeroEvents()
        {
            // 四档全零 = 0 条
            var quotas = EventF1Pipeline.HamiltonSplit(10, new int[] { 0, 0, 0, 0 }, null);
            long sum = 0;
            foreach (var q in quotas) sum += q;
            Assert.AreEqual(0, sum, "四档全零应产生 0 条");
        }

        // AC-52-37: 单档 ΣW=0 ⇒ 跳过并同窗再分
        [Test]
        public void test_singleTierZero_skipsAndRedistributes()
        {
            // 威胁档 ×0，其他档保留
            var quotas = EventF1Pipeline.HamiltonSplit(10, new int[] { 0, 1, 1, 1 }, null);
            Assert.AreEqual(0, quotas[0], "威胁档应 = 0");
            // 其他档应分配剩余配额（总和 = 10 - 0 = 10）
            long sum = 0;
            for (int i = 1; i < 4; i++) sum += quotas[i];
            Assert.AreEqual(10, sum, "其他档应分配全部 10 条");
        }

        // AC-52-37: MIN=MAX ⇒ 恒该档
        [Test]
        public void test_minEqualsMax_constantTier()
        {
            var input = new F1PipelineInput(0, 10, false, true, false, false, 0);
            var output = EventF1Pipeline.Execute(input, 5, 5);
            Assert.AreEqual(5, output.StrengthTier, "MIN=MAX 时应恒为 5");
        }

        // AC-52-37: 夜+医馆叠加
        [Test]
        public void test_nightAndClinic_combined()
        {
            // 夜 + 医馆：威胁档 ×0，机会/反应保留
            var nightClinicInput = new F1PipelineInput(0, 10, true, false, true, false, 0);
            var mult = EventF1Pipeline.ComputeContextMult(nightClinicInput);
            Assert.AreEqual(0, mult[0], "夜+医馆威胁档应 ×0");
            Assert.AreEqual(1, mult[1], "夜+医馆机会档应保留");
        }
    }
}
