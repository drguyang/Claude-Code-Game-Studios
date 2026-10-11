// sprint-07 S2.3/S2.4 · 方笺页接线宿主判据(2026-10-11)
//
// 判据(可证伪):
//   ① 组合根装配 ⇒ 服务袋带 `PrescriptionPageHost`(生产接线宿主存在);
//   ② 落笔(BindPatient + SetAuthor 后)→ JudgmentRecorded 入病例流 + 墨龄 > 0(S2.3);
//   ③ 开方(同上)→ DrugTreatmentApplied 入病史流 → 体征链 apply(S2.4 的管道);
//   ④ fail-loud:未 SetAuthor / 未 BindPatient 即动作 → 抛(装配遗漏不静默);
//   ⑤ 开方接线期限 1:第二次 Prescribe 返回 false(不自动重复行使 —— 假绿根因)。
//
// 全生产路径:真 EventStream + 真 PatientSpawner + 真 CaseOpenWriter + 真组合根
// (照 ink_age_test 先例;开方前置 = 在场病人已建档 + 开案)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="PrescriptionPageHost"/> 接线判据(sprint-07 S2.3/S2.4)。</summary>
    internal sealed class prescription_page_host_test
    {
        private CompositionRootServices _bag;
        private PrescriptionPageHost _page;
        private PatientId _patient;

        private void AssembleReady()
        {
            _bag = CompositionRoot.Assemble();
            // ⚠️ C3-M3:种子 = 装配期一次 Grant 玩家 0;测试 author 9001 须自 Grant
            //   (生产 = BootRoot 恒 0 消费装配种子;测试 author 可变 ⇒ 夹具自给)。
            _bag.PortionsStore.Grant(9001, MinimalDrugCatalog.DrugKey, 1);
            _page = _bag.PrescriptionPage;
            _page.SetAuthor(9001);

            // 建档 + 在场 + 开案(全前置齐)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
            var caseWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            Assert.AreEqual(CaseOpenWriteResult.Opened, caseWriter.TryOpen(1, _patient, default));

            _page.BindPatient(_patient);
            _bag.CasebookSource.ChangePatient(_patient);
        }

        [Test]
        public void test_s23_assemble_createsHost()
        {
            var bag = CompositionRoot.Assemble();
            Assert.IsNotNull(bag.PrescriptionPage,
                "组合根须装配方笺页接线宿主(S2.3/S2.4 —— 生产接线面存在)");
        }

        [Test]
        public void test_s23_writeJudgment_recordsEvent_inkAgeGrows()
        {
            AssembleReady();
            _bag.TickDriver.Advance(0.05);   // → tick 1(落笔于 1)
            _bag.VitalsService.OnTickEdge(_bag.TickDriver.CurrentTick);

            var result = _page.WriteJudgment(lexiconId: 7, confidence: 2);
            Assert.AreEqual(JudgmentWriteResult.Recorded, result,
                "落笔 ⇒ JudgmentRecorded 写入(有开案前置通过)");
            Assert.IsTrue(StreamHas(EventKind.JudgmentRecorded),
                "JudgmentRecorded 入病例流(S2.3 判据)");

            _bag.TickDriver.Advance(0.05);   // → tick 2
            Assert.AreEqual(1L, _bag.CasebookSource.InkAgeTicks,
                "墨龄 = 当前tick(2) − 最近Judgment(1) = 1(S2.3 有内容的墨龄)");
        }

        [Test]
        public void test_s24_prescribe_recordsDrugEvent()
        {
            AssembleReady();
            _bag.TickDriver.Advance(0.05);   // → tick 1
            _bag.VitalsService.OnTickEdge(_bag.TickDriver.CurrentTick);

            bool applied = _page.Prescribe(MinimalDrugCatalog.DrugKey, doseOrdinal: 1);
            Assert.IsTrue(applied, "开方 ⇒ PrescribeFlow 走完并落流(S2.4 判据)");
            Assert.IsTrue(StreamHas(EventKind.DrugTreatmentApplied),
                "DrugTreatmentApplied 入病史流(S2.4 —— 有内容的管道终点)");
        }

        [Test]
        public void test_s24_prescribe_onceOnly_notAutoRepeat()
        {
            AssembleReady();
            _bag.TickDriver.Advance(0.05);
            _bag.VitalsService.OnTickEdge(_bag.TickDriver.CurrentTick);

            Assert.IsTrue(_page.Prescribe(MinimalDrugCatalog.DrugKey, 1),
                "首次开方成功");
            Assert.IsFalse(_page.Prescribe(MinimalDrugCatalog.DrugKey, 1),
                "接线期限 1:不自动重复开方(玩家零输入刷事件 = 假绿,承「灰盒测不对」根因)");
            Assert.AreEqual(1, CountOf(EventKind.DrugTreatmentApplied),
                "DrugTreatmentApplied 恰 1 条(重复行使被拒)");
        }

        [Test]
        public void test_s24_prescribe_outOfStock_rejects()
        {
            // C3-M3 宿主级负测:种子只发一次(组合根装配期 Grant 1)⇒ 第二次开方
            //   **耗尽**库存返回 false(原逐次 Grant 恒自证,此测恒 true 永不可红;
            //   删种子/改真库存时这条红 = 删除时机信号)。
            AssembleReady();
            _bag.TickDriver.Advance(0.05);
            _bag.VitalsService.OnTickEdge(_bag.TickDriver.CurrentTick);

            Assert.IsTrue(_page.Prescribe(MinimalDrugCatalog.DrugKey, 1),
                "首次开方消耗种子 1 份(成功)");
            Assert.IsFalse(_page.Prescribe(MinimalDrugCatalog.DrugKey, 1),
                "种子 1 份耗尽 ⇒ 第二次 false(接线期限 + 库存前置双重拒绝;非自证库存)");
        }

        [Test]
        public void test_s23_failLoud_authorNotSet()
        {
            var bag = CompositionRoot.Assemble();
            var page = new PrescriptionPageHost(bag.EventSink, bag.Encoder, bag.Stream.Events,
                bag.PrescribeDriver, bag.PortionsStore, bag.TickDriver, initialAuthorId: -1);   // 未 SetAuthor
            // ⚠️ 直构实例 = 刻意不测装配袋内实例(隔离:fail-loud 是宿主自持行为,
            //    与组合根的 SetAuthor 时机解耦 —— 防误读为「袋内实例也 fail-loud」)。

            Assert.Throws<InvalidOperationException>(
                () => page.WriteJudgment(1, 2),
                "未 SetAuthor 即落笔 ⇒ fail-loud(装配遗漏不静默)");
            Assert.Throws<InvalidOperationException>(
                () => page.Prescribe(MinimalDrugCatalog.DrugKey, 1),
                "未 SetAuthor 即开方 ⇒ fail-loud");
        }

        [Test]
        public void test_s23_failLoud_noBoundPatient()
        {
            var bag = CompositionRoot.Assemble();
            var page = new PrescriptionPageHost(bag.EventSink, bag.Encoder, bag.Stream.Events,
                bag.PrescribeDriver, bag.PortionsStore, bag.TickDriver, initialAuthorId: 9001);
            // 未 BindPatient(对象缺失);直构实例同上注(隔离,防误读)。

            Assert.Throws<InvalidOperationException>(
                () => page.WriteJudgment(1, 2),
                "未绑定病人即落笔 ⇒ fail-loud(无对象无从落笔)");
            Assert.Throws<InvalidOperationException>(
                () => page.Prescribe(MinimalDrugCatalog.DrugKey, 1),
                "未绑定病人即开方 ⇒ fail-loud");
        }

        private bool StreamHas(EventKind kind)
            => CountOf(kind) > 0;

        private int CountOf(EventKind kind)
        {
            int n = 0;
            foreach (var e in _bag.Stream.Events)
                if (e.Kind == kind) n++;
            return n;
        }
    }
}