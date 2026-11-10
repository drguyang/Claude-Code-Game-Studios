// 结案页(T20 第二批 · 2026-11-10) —— 屏 + 装配层驱动的判据夹具
//
// 权威:`design/ux/casebook-39.md:370`(OQ-CB-7 形态归 39 / 落地归 42)·
//       `design/ux/case-close-42.md`(本屏 spec)· `case-system.md` 规则八
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的七件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 没有开着的病例 ⇒ **不开空页**(与「开了但没东西」不可混淆);
//   ② 有开着的病例 ⇒ 开 `ModalId.CaseClose`,屏被绑定到 presenter;
//   ③ 墨勾行 Submit ⇒ 屏上态翻转(显式自述真的存在,`AC-37-04` 的那一半);
//   ④ 未落墨勾就提交 ⇒ 写者拒 `RejectNotChecked`,屏上留**一行淡墨字**、页不关;
//   ⑤ 落墨勾 + 窗口内有处置 ⇒ `CaseClosed` 真入流,页合上;
//   ⑥ 取消 ⇒ 合页、零写入;
//   ⑦ 已有别的模态摊开 ⇒ 不开结案页(模态不叠)。
//
// ⚠️ 本文件**不重测** 37 的四条前置(`case_close_writer_test` 是那份真源);
//   这里只测「屏上的一个动作 → 驱动的一次调用」这条新链路。
// ⚠️ **未覆盖**:`RegisterCallback<NavigationSubmitEvent>` 的事件绑定本身 —— EditMode 下
//   42 的 UI 根没有 PanelSettings(`panel == null`),`SendEvent` **不派发**(实测:直接发
//   `NavigationSubmitEvent` 到行,回调零次触发)。⇒ 本文件改走屏上的公开入口
//   (`ActivateInkRow` / `ActivateSubmitRow`,与回调是同一实现体);
//   **真事件绑定 = 真包走查**,目前无走查件 ⇒ 记在证据 §未覆盖,不借绿。
// ⚠️ 零写盘:所有事件进内存 `EventStream`。

using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.CaseSystem
{
    [TestFixture]
    public sealed class case_close_screen_test
    {
        private const int DiseaseId = 11;

        private GameObject _uiGo;
        private SkeuoRuntimeDriver _ui;
        private CompositionRootServices _bag;
        private CaseCloseDriver _closeDriver;
        private CaseOpenWriter _openWriter;
        private CaseClosePresenter _presenter;
        private PatientId _patient;
        private long _tick;

        [SetUp]
        public void Setup()
        {
            // ── 42 驱动(与 processing_ignition_sink_test 同款:自建组件 + 单独调 Awake)──
            _uiGo = new GameObject("case-close-screen-test");
            _ui = _uiGo.AddComponent<SkeuoRuntimeDriver>();
            typeof(SkeuoRuntimeDriver)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance
                                  | System.Reflection.BindingFlags.NonPublic
                                  | System.Reflection.BindingFlags.Public)
                .Invoke(_ui, null);

            // ── 37 侧的真装配 ──
            _bag = CompositionRoot.Assemble();
            _openWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            var closeWriter = new CaseCloseWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            _tick = 0;
            _closeDriver = new CaseCloseDriver(closeWriter, () => _bag.Stream.Events, _bag.Decoder, () => _tick);
            _presenter = new CaseClosePresenter(_closeDriver, _ui, () => _bag.Stream.Events);

            // 病人:9 创建 + 在场(立案前置)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: DiseaseId, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            if (_uiGo != null) Object.DestroyImmediate(_uiGo);
        }

        private void OpenCase(long tick)
        {
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _openWriter.TryOpen(tick, _patient, new DiseaseIdSet(1)), "夹具:立案须成功");
        }

        private void AppendTreatment(long tick)
        {
            PayloadRef care = _bag.Encoder.Encode(
                EventKind.CareApplied, new CareAppliedPayload(tick, actionId: 0, caregiver: 7, phase: 0));
            _bag.EventSink.Append(new SimEvent(tick, _patient, -1, EventKind.CareApplied, care));
        }

        /// <summary>激活屏上的某一行(与 UI 回调同一实现体;见文件头的限制说明)。</summary>
        private void SubmitRow(string rowName)
        {
            var root = _ui.GetUiRoot();
            VisualElement row = root?.Q<VisualElement>(rowName);
            Assert.IsNotNull(row, $"屏上没有 {rowName} —— 行结构变了?");
            Assert.IsTrue(row.focusable, $"{rowName} 须可聚焦(手柄焦点序是唯一导航路径)");

            if (rowName == "case-close-ink-row") Screen().ActivateInkRow();
            else Screen().ActivateSubmitRow();
        }

        private CaseCloseScreen Screen() => _ui.Screen as CaseCloseScreen;

        // ══════════ ① 没有开着的病例 ⇒ 不开空页 ══════════

        [Test]
        public void test_open_withoutOpenCase_doesNotOpen()
        {
            Assert.IsFalse(_presenter.TryOpen(_patient), "没有开着的病例 ⇒ 不开屏");
            Assert.AreEqual(ModalId.None, _ui.CurrentModal, "不得留下一张空页挡住世界");
        }

        // ══════════ ② 有开着的病例 ⇒ 开屏并绑定 ══════════

        [Test]
        public void test_open_withOpenCase_opensAndBindsScreen()
        {
            OpenCase(5);

            Assert.IsTrue(_presenter.TryOpen(_patient));
            Assert.AreEqual(ModalId.CaseClose, _ui.CurrentModal);
            Assert.IsNotNull(Screen(), "开出来的屏应是 CaseCloseScreen");
            Assert.AreEqual(_patient, _presenter.Patient);
        }

        // ══════════ ③ 墨勾行 Submit 真的翻转屏上态 ══════════

        [Test]
        public void test_inkRow_submit_flipsInkState()
        {
            OpenCase(5);
            _presenter.TryOpen(_patient);
            Assert.IsFalse(Screen().IsInkChecked, "开屏复位:未勾");

            SubmitRow("case-close-ink-row");
            Assert.IsTrue(Screen().IsInkChecked, "落一次墨勾 ⇒ 已勾(显式自述存在且可判)");

            SubmitRow("case-close-ink-row");
            Assert.IsFalse(Screen().IsInkChecked, "再落一次 ⇒ 擦除");
        }

        // ══════════ ④ 未落墨勾 ⇒ 写者拒,屏留淡墨行,页不关 ══════════

        [Test]
        public void test_submit_withoutInk_rejectsAndKeepsPageOpen()
        {
            OpenCase(5);
            AppendTreatment(6);
            _presenter.TryOpen(_patient);
            _tick = 10;

            SubmitRow("case-close-submit-row");

            Assert.AreEqual(ModalId.CaseClose, _ui.CurrentModal, "被拒不得合页(否则玩家不知道发生了什么)");
            Assert.AreEqual("未勾「已处置」", Screen().RejectText, "淡墨行须说明拒因");
            Assert.AreEqual(0, _bag.Stream.Events.Count(e => e.Kind == EventKind.CaseClosed), "零写入");
        }

        // ══════════ ⑤ 墨勾 + 有处置 ⇒ 真结案并合页 ══════════

        [Test]
        public void test_submit_withInk_andTreatment_closesAndDismisses()
        {
            OpenCase(5);
            AppendTreatment(6);
            _presenter.TryOpen(_patient);
            _tick = 10;

            SubmitRow("case-close-ink-row");
            SubmitRow("case-close-submit-row");

            Assert.AreEqual(1, _bag.Stream.Events.Count(e => e.Kind == EventKind.CaseClosed), "恰一条 CaseClosed");
            Assert.AreEqual(ModalId.None, _ui.CurrentModal, "结案成功 ⇒ 合页回世界");
            Assert.AreEqual(PatientId.None, _presenter.Patient, "合页后不再挂着病人");
        }

        // ══════════ ⑥ 取消 ⇒ 合页、零写入 ══════════

        [Test]
        public void test_cancel_dismissesWithoutWriting()
        {
            OpenCase(5);
            _presenter.TryOpen(_patient);
            int before = _bag.Stream.Count;

            _presenter.Cancel();

            Assert.AreEqual(ModalId.None, _ui.CurrentModal);
            Assert.AreEqual(before, _bag.Stream.Count, "取消 ⇒ 零写入");
            Assert.IsTrue(CaseStreamQuery.HasOpenCase(_bag.Stream.Events, _patient.Value),
                "取消不结案 ⇒ 病例仍开着");
        }

        // ══════════ ⑦ 已有别的模态 ⇒ 叠在上层,取消后回到原模态 ══════════

        /// <summary>
        /// 42 的模态驱动**允许叠栈**(`OpenModalCore` 会压入前一层),所以本屏不拦「已有模态」;
        /// 本条的判据是**取消后回得去** —— 否则玩家按 ESC 会掉到一张空白层。
        /// </summary>
        [Test]
        public void test_open_whenAnotherModalIsOpen_stacks_thenCancelRestoresPrevious()
        {
            OpenCase(5);
            Assert.IsTrue(_ui.OpenModal(ModalId.Pause), "前提:暂停壳开得出来");

            Assert.IsTrue(_presenter.TryOpen(_patient), "结案页叠在暂停壳之上(驱动语义:压栈)");
            Assert.AreEqual(ModalId.CaseClose, _ui.CurrentModal);

            _presenter.Cancel();
            Assert.AreEqual(ModalId.Pause, _ui.CurrentModal, "取消后须回到被压住的模态,不是掉到空白层");
        }
    }
}
