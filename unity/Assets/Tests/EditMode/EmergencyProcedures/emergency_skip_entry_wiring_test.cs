// story-009 · 跳过入口写路径 + ICasebookCommandSink 增员(2026-10-14)
//
// 判据(可证伪):
//   ① AC-9-05 skip 路径:TryManual(method 1)⇒ 载荷 Method=1 + DrugPotency=32768
//      (AppliedWeak 档,F-10.4:BasePotency 1.0 × ResultMul[AppliedWeak] 0.5)+
//      零 SkillGrown(emitGrowth=false;非空转守卫 = 双事件确实入流);
//   ② AC-9-05 强度判别:同 Patient 同表,method 0(满幅聚合)⇒ 65536;
//      method 1 ⇒ 32768 —— 两档在流上可判别;
//   ③ 路由 fail-loud:未接 EmergencyManualDriver ⇒ SubmitEmergencySkip 抛
//      (装配遗漏同口径);未绑病人 ⇒ 静默零事件(合法玩家路径,与 SubmitDose 的
//      fail-loud 刻意不同 —— 见路由头注);
//   ④ 绑定 + 接线 + 不在场 ⇒ 零事件(presence 门,零提示纪律);
//   ⑤ 绑定 + 接线 + 在场 ⇒ 双事件 + 归因绑定病人;
//   ⑥ AC-9-07 focusable:跳过入口 focusable = true(候选列表零交互绑定缺口闭合);
//   ⑦ AC-9-02/03 候选列表结构面:挂载 + 标签 + SetVisible 透传。
//
// ⚠️ PlayMode 装配(CompositionRoot)在 EditMode 可直跑(纯 C# 装配,既有
//   emergency_manual_driver_test 同款先例);本测住 EditMode/EmergencyProcedures
//   —— 与判域测试(applied_payload_settlement_test 等)同目录。

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    /// <summary>story-009 跳过入口写路径判据(AC-9-05 / AC-9-07 / 路由形态)。</summary>
    public sealed class EmergencySkipEntryWiringTest
    {
        private CompositionRootServices _bag;
        private EmergencyManualDriver _driver;
        private PatientId _patient;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _driver = _bag.EmergencyManualDriver;

            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
        }

        [Test]
        public void test_ac905_skipMethod_appliedWeakPotency_andZeroGrowth()
        {
            // 前置守卫(非空转):断言「双事件入流」证明路径真的走通,再断零成长。
            int before = _bag.Stream.Count;
            int grownBefore = _bag.Stream.Events.Count(e => e.Kind == EventKind.SkillGrown);

            var result = _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 5);

            Assert.IsTrue(result.Applied, "skip 走完 Process(双事件入流)");
            Assert.AreEqual(before + 2, _bag.Stream.Count, "恰两条流事件(非空转守卫)");

            Assert.IsTrue(PayloadCodec.TryGetPayload(_bag.Stream.Events[before + 1], _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload applied));
            Assert.AreEqual(1, applied.Method, "Method = Skip(流上可判别)");
            Assert.AreEqual(32768L, applied.DrugPotency.Raw,
                "AC-9-05:Skip ⇒ AppliedWeak 档 = 32768(F-10.4:1.0 × 0.5)");

            // 零 SkillGrown(跳过不发熟练度成长 —— 规则六;emitGrowth = false):
            // 断「成长数不变式」(新增事件里零 SkillGrown),不断全流恒 0(不依赖 SetUp 形态)。
            int grownAfter = _bag.Stream.Events.Count(e => e.Kind == EventKind.SkillGrown);
            Assert.AreEqual(grownBefore, grownAfter,
                "skip 路径零 SkillGrown(emitGrowth=false)");
        }

        [Test]
        public void test_ac905_manualVersusSkip_potencyDiscriminable()
        {
            // 强度判别面:同病人同表,两 method 的 DrugPotency 在流上可判别。
            // ⚠️ 相对下标(SetUp 已有 DiseaseOnset —— 绝对下标会撞流头)。
            int before = _bag.Stream.Count;

            _driver.TryManual(_patient, method: 0, actorId: 9001, tick: 5);
            Assert.IsTrue(PayloadCodec.TryGetPayload(_bag.Stream.Events[before + 1], _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload manual));

            _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 6);
            Assert.IsTrue(PayloadCodec.TryGetPayload(_bag.Stream.Events[before + 3], _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload skip));

            Assert.AreEqual(0, manual.Method);
            Assert.AreEqual(1, skip.Method);
            Assert.AreEqual(32768L, skip.DrugPotency.Raw, "Skip 档 = 32768");
            Assert.Greater(manual.DrugPotency.Raw, skip.DrugPotency.Raw,
                "Manual(满幅单沿 ⇒ Applied)强度 > Skip(AppliedWeak)—— 两档判别");
        }

        [Test]
        public void test_router_unboundPatient_skipIsSilentNoEvent()
        {
            // 未绑病人 = 合法玩家路径(未查体就点跳过)⇒ 静默返回,零事件零提示。
            // ⚠️ 与 SubmitDose 的「未绑 ⇒ fail-loud」刻意不同(见路由 SubmitEmergencySkip 头注)。
            var router = new CasebookCommandRouter(_bag.PrescriptionPage.Writer,
                () => _bag.TickDriver.CurrentTick, _bag.PrescriptionPage);
            router.BindEmergencySkip(_driver, actorId: 9001);
            int before = _bag.Stream.Count;

            Assert.DoesNotThrow(() => router.SubmitEmergencySkip(),
                "未绑病人 ⇒ 静默(不抛 —— 合法玩家路径)");
            Assert.AreEqual(before, _bag.Stream.Count, "零事件");
        }

        [Test]
        public void test_router_notWired_skipFailsLoud()
        {
            // 装配遗漏(未接 EmergencyManualDriver)⇒ fail-loud(与未接方笺页同口径)。
            var router = new CasebookCommandRouter(_bag.PrescriptionPage.Writer,
                () => _bag.TickDriver.CurrentTick, _bag.PrescriptionPage);
            router.BindPatient(_patient);   // 绑了病人但没接线

            Assert.Throws<InvalidOperationException>(() => router.SubmitEmergencySkip(),
                "未接线 ⇒ fail-loud(装配遗漏,不静默吞)");
        }

        [Test]
        public void test_router_boundAndWired_skipWritesBothEventsAtBoundPatient()
        {
            var router = new CasebookCommandRouter(_bag.PrescriptionPage.Writer,
                () => _bag.TickDriver.CurrentTick, _bag.PrescriptionPage);
            router.BindEmergencySkip(_driver, actorId: 9001);
            router.BindPatient(_patient);
            int before = _bag.Stream.Count;

            router.SubmitEmergencySkip();

            Assert.AreEqual(before + 2, _bag.Stream.Count, "跳过 ⇒ 双事件入流");
            Assert.AreEqual(_patient, _bag.Stream.Events[before].Patient,
                "归因 = 绑定病人(与 SubmitDose 同构)");
            Assert.IsTrue(PayloadCodec.TryGetPayload(_bag.Stream.Events[before + 1], _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload applied));
            Assert.AreEqual(1, applied.Method, "路由入口 ⇒ method = 1");
        }

        [Test]
        public void test_router_absentPatient_skipSilentZeroEvents()
        {
            // 绑定了但已不在场 ⇒ presence 门静默(零提示纪律 AC-11-12)。
            var absent = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            // 不 AddPresent —— 未出现
            var router = new CasebookCommandRouter(_bag.PrescriptionPage.Writer,
                () => _bag.TickDriver.CurrentTick, _bag.PrescriptionPage);
            router.BindEmergencySkip(_driver, actorId: 9001);
            router.BindPatient(absent);
            int before = _bag.Stream.Count;

            router.SubmitEmergencySkip();
            Assert.AreEqual(before, _bag.Stream.Count, "不在场 ⇒ 零事件(零提示)");
        }

        [Test]
        public void test_ac907_skipEntry_focusable_true()
        {
            // AC-9-07:候选列表「零交互绑定」缺口的闭合面 —— 跳过入口可获焦点
            // (FocusController 导航落点;NavigationSubmitEvent / ClickEvent 在 Build 内注册)。
            var runtime = _bag.EmergencyCandidates;
            Assert.IsTrue(runtime.HasSkipEntry, "前置:跳过入口已实例化");
            Assert.IsTrue(runtime.SkipFocusable,
                "AC-9-07:跳过入口 focusable = true(可经手柄导航获焦)");
        }

        [Test]
        public void test_candidateRuntime_setVisible_passthrough()
        {
            // AC-9-02/03 结构面 + R-9.3 Idle 门透传:挂载 + SetVisible 切换可见。
            var runtime = _bag.EmergencyCandidates;
            var root = new VisualElement();
            runtime.MountTo(root);

            Assert.IsTrue(runtime.EntryCount == 2, "前置:两候选项(止血/通气)");

            runtime.SetVisible(false);
            Assert.IsFalse(runtime.IsVisible, "SetVisible(false) ⇒ 隐藏(Idle 门 armed 侧)");

            runtime.SetVisible(true);
            Assert.IsTrue(runtime.IsVisible, "SetVisible(true) ⇒ 复现(Idle 门 idle 侧)");
        }

        [Test]
        public void test_candidateRuntime_skipCallback_invokedThroughRouter()
        {
            // 端到端接线形态:UI 回调 → 路由 → 写者(整链在同一服务袋内闭环)。
            var runtime = _bag.EmergencyCandidates;
            var router = new CasebookCommandRouter(_bag.PrescriptionPage.Writer,
                () => _bag.TickDriver.CurrentTick, _bag.PrescriptionPage);
            router.BindEmergencySkip(_driver, actorId: 9001);
            router.BindPatient(_patient);
            runtime.SetSkipCallback(router.SubmitEmergencySkip);
            int before = _bag.Stream.Count;

            // 模拟 UI 层 Submit(生产 = NavigationSubmitEvent / ClickEvent 回调体):
            // ⚠️ 无类型反射 —— 测试装配刻意不引 Gameplay.UI(EmergencyCandidateRuntime
            //   头注纪律:不跨装配边界暴露该类型),回调链仍可经 object 反射触达。
            var listObj = typeof(EmergencyCandidateRuntime).GetField("_list",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(runtime);
            Assert.IsNotNull(listObj, "前置:宿主持列表载体实例");
            var cbField = listObj.GetType().GetField("_onSkipSubmit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(cbField, "前置:回调字段存在");
            var cb = (System.Action)cbField.GetValue(listObj);
            Assert.IsNotNull(cb, "前置:回调已注入(SetSkipCallback 接线面)");

            cb();
            Assert.AreEqual(before + 2, _bag.Stream.Count,
                "UI Submit → 路由 → 写者:双事件入流(接线闭环)");
        }
    }
}
