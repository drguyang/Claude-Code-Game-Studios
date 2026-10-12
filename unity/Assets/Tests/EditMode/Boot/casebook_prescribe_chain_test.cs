// sprint-08 S2.2/S2.3 · 戥子选档确认 → 开方 → 病史流 全链判据(EditMode · 2026-10-12)
//
// 判据(可证伪):
//   ① 全链:42 方笺页档位行 Submit ⇒ 装配层命令路由 `SubmitDose` ⇒ `PrescriptionPageHost`
//      ⇒ `PrescribeRequestDriver.TryPrescribe` ⇒ 11 五步 ⇒ `DrugTreatmentApplied` 入病史流;
//   ② 档序数经**唯一量化点**(`DentchDoseSelector.DoseValueOf`)换算为 11 整数档;
//   ③ 无货拒开 ⇒ **零新事件**(AC-11-12 零提示);
//   ④ 库存充足 ⇒ 可重复开方(2026-10-12 裁定 甲:删原接线期限 1,拒绝权归库存);
//   ⑤ **变异:删接线 ⇒ 零事件恰红**(命令路由的 SubmitDose 未接宿主时 fail-loud)。
//
// 全生产路径:真组合根 + 真 PatientSpawner + 真 CaseOpenedDriver + 真 PrescribeFlow。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>戥子选档 → 开方全链判据(sprint-08 S2.2)。</summary>
    internal sealed class casebook_prescribe_chain_test
    {
        private const int PlayerId = 9101;

        private CompositionRootServices _bag;
        private PatientId _patient;

        /// <summary>建档 + 在场 + 开案 + 作者 + 病人绑定(开方前置齐)。
        /// <para>⚠️ **份数 = 档值 × <c>portions_per_dose</c>**(PrescribeFlow ①b:乘法住 11)
        /// —— 最小表 <c>portions_per_dose = 1</c> ⇒ 档 3 需 3 份。种子须按本测用到的档位给足。</para></summary>
        /// <param name="portions">种子份数(默认 4:够 档3(3 份)或 档1+档2(3 份))。</param>
        private void AssembleReady(int portions = 4)
        {
            _bag = CompositionRoot.Assemble();
            _bag.PortionsStore.Grant(PlayerId, MinimalDrugCatalog.DrugKey, portions);
            _bag.PrescriptionPage.SetAuthor(PlayerId);

            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            _bag.TickDriver.Advance(0.05d);                                  // → tick 1
            _bag.CaseOpenedDriver.OnTickEdge(_bag.TickDriver.CurrentTick);   // 开案

            _bag.CasebookSource.ChangePatient(_patient);   // 两处病人同步(含命令路由)
        }

        private int DrugEventCount()
        {
            int n = 0;
            foreach (var e in _bag.Stream.Events)
                if (e.Kind == EventKind.DrugTreatmentApplied) n++;
            return n;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 全链:档位行落点 ⇒ DrugTreatmentApplied 入病史流
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_dentchSelectConfirm_prescribesDrugTreatmentApplied()
        {
            AssembleReady();
            Assert.AreEqual(0, DrugEventCount(), "前置:零药疗事件");

            _bag.CasebookCommands.SubmitDose(doseOrdinal: 3);   // 第三档

            Assert.AreEqual(1, DrugEventCount(),
                "戥子落点确认 ⇒ DrugTreatmentApplied 入病史流(S2.2 全链)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 档序数经唯一量化点换算:ordinal 1 vs 3 ⇒ 载荷 drug_potency 不同
        //
        // ⚠️ 载荷无 SelectedDose 字段(它只有 drug_potency)—— 档序数**不是**直接落进载荷,
        //   而是经唯一量化点(ordinal → dose)→ F-11.1 dose_potency = potency × dose / DOSE_BASE。
        //   故可证伪面 = **同药两档 ⇒ 两个不同的 drug_potency**(档真的参与了计算)。
        //   DOSE_BASE = 65536 ⇒ dose_potency 数值 = dose × 0.5(Q16.16) ⇒ 档 1 = 2^15,档 3 = 2^16。
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_doseOrdinal_mapsThroughSelectorQuantizationPoint()
        {
            AssembleReady();
            _bag.CasebookCommands.SubmitDose(doseOrdinal: 1);
            _bag.CasebookCommands.SubmitDose(doseOrdinal: 3);

            var potencies = new List<long>();
            foreach (var e in _bag.Stream.Events)
            {
                if (e.Kind != EventKind.DrugTreatmentApplied) continue;
                Assert.IsTrue(PayloadCodec.TryGetPayload(in e, _bag.BlobPool,
                        out DrugTreatmentAppliedPayload p), "载荷可解码");
                potencies.Add(p.DrugPotency.Raw);
            }

            Assert.AreEqual(2, potencies.Count, "两次落点 ⇒ 两条药疗事件");
            Assert.AreNotEqual(potencies[0], potencies[1],
                "档 1 vs 档 3 ⇒ **不同 dose_potency**(档序数经唯一量化点真参与计算," +
                "非恒定占位 —— 变异:若绕过 DoseValueOf 恒传 1,本测恰红)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 无货拒开 ⇒ 零新事件(AC-11-12 零提示)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_noStock_refuses_zeroEvents()
        {
            AssembleReady(portions: 2);   // 档 1 耗 1 份 ⇒ 两次用光
            _bag.CasebookCommands.SubmitDose(1);
            _bag.CasebookCommands.SubmitDose(1);
            int before = DrugEventCount();
            Assert.AreEqual(2, before, "前置:两次开方已用光 2 份");

            _bag.CasebookCommands.SubmitDose(1);   // 第三次:库存耗尽

            Assert.AreEqual(before, DrugEventCount(),
                "无货拒开 ⇒ **零新事件**(AC-11-12:失败不回传原因、不报错、不落流)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 库存充足 ⇒ 可重复开方(删接线期限 1 的可证伪面)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_withStock_repeatable_noWiringDeadline()
        {
            AssembleReady();   // 4 份
            _bag.CasebookCommands.SubmitDose(1);   // 耗 1 份
            _bag.CasebookCommands.SubmitDose(2);   // 耗 2 份

            Assert.AreEqual(2, DrugEventCount(),
                "库存充足 ⇒ 两次落点两次开方(接线层零单次闸门 —— 2026-10-12 裁定 甲)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 变异面:未接宿主 ⇒ fail-loud(不静默吞 = 不假绿)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_routerWithoutHost_submitDose_failLoud()
        {
            _bag = CompositionRoot.Assemble();
            _bag.PrescriptionPage.SetAuthor(PlayerId);
            var router = new CasebookCommandRouter(
                _bag.PrescriptionPage.Writer, () => 0L, prescriptionPage: null);

            Assert.Throws<System.InvalidOperationException>(
                () => router.SubmitDose(1),
                "命令路由未接方笺页宿主 ⇒ 开方动作无落点 = 装配遗漏,fail-loud(不静默吞)");
        }
    }
}
