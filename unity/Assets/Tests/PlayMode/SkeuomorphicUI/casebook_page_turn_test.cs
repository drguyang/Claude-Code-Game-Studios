// sprint-08 S2.1 · 方笺页翻页 / 折页 / 戥子档位判据(PlayMode · 2026-10-12)
//
// 判据(可证伪):
//   ① 初态 = 脉案页;页缘「后案」Submit ⇒ 翻至方笺续页;「前案」⇒ 翻回;
//   ② 两向**不循环**(首页无前案、末页无后案);
//   ③ 折边 Submit ⇒ 折叠态翻转;
//   ④ **翻页 / 折页零流事件**(`AC-39-10`):全流程 `IEventSink.Append` 调用数 = 0;
//   ⑤ 方笺页戥子档位行数 = DTO 的 `DoseDetentCount`(= hi − lo + 1);每行 userData = 显式 ordinal;
//   ⑥ 档位行 Submit ⇒ 写通道收到**显式 ordinal**(非焦点路径索引 —— TD 第五风险);
//   ⑦ 未接写通道 / 档数 0 ⇒ 不崩、不造档位(零假数据);
//   ⑧ **输入路径真派发**(真 `PanelSettings` + `UIDocument`):页缘 / 折边 / 档位行落焦 + 提交
//      ⇒ 引擎 EventDispatcher 实跑 ⇒ 语义动作真发生(接线可证伪)。
//
// ⚠️ ①–⑦ 与 ⑧ 的分工(sprint-08 §4 预裁「判据与输入路径解耦」):
//   · ①–⑦ = **语义判据** —— 逻辑树直调语义方法,零渲染面依赖;
//   · ⑧ = **接线判据** —— 只证「引擎派发 ⇒ 我们的 handler」这一段真的通。
//   **删掉 `RegisterCallback<NavigationSubmitEvent>` 行 ⇒ ⑧ 恰红,而 ①–⑦ 仍绿**。
//
// ⚠️ **必须先 `Focus()`**(2026-10-12 实测):`NavigationSubmitEvent` 的派发目标由
//   `FocusController.focusedElement` 决定 —— 无焦点时事件被丢弃。这不是夹具怪癖,
//   是引擎真实语义(导航提交永远落在当前焦点元素上),也正是玩家的真实动作序。
//
// ⚠️ 本件**兼语义 + 接线**(照 S1.1 `casebook_lexicon_book_test` 先例):EditMode
//   `Sim.Contracts.Tests` 不引用 `Gameplay.UI`(asmdef 引用集),而 42 屏在 `Gameplay.UI`
//   ⇒ 42 屏相关判据一律住 PlayMode `Gameplay.Tests`(它引 `Gameplay.UI`)。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>方笺页翻页 / 折页 / 戥子判据(sprint-08 S2.1)。</summary>
    internal sealed class casebook_page_turn_test
    {
        // ── 合成 fixture(42 侧只读源 / 写通道;零 sim 依赖)────────────────

        private sealed class FakeSource : ICasebookDtoSource
        {
            public int Detents;
            public int ActiveSignCount => 0;
            public IReadOnlyList<SignLexemeRow> RevealedRows => new List<SignLexemeRow>();
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0L;
            public IReadOnlyList<int> SeenLexiconIds => new List<int>();
            public byte PendingConfidence => 0;
            public int DoseDetentCount => Detents;
            public string TextOfLexicon(int lexiconId) => null;
        }

        private sealed class FakeSink : ICasebookCommandSink
        {
            public readonly List<int> Doses = new List<int>();
            public int PageTurns;
            public int Folds;
            public void SubmitDiseaseName(int lexiconId) { }
            public void SubmitConfidence() { }
            public void SubmitDose(int doseOrdinal) => Doses.Add(doseOrdinal);
            public void SubmitPageTurn() => PageTurns++;
            public void SubmitFold() => Folds++;
            public void SubmitEmergencySkip() { }   // story-009 接口增员(本测不涉及跳过路径)
        }

        /// <summary>逻辑树夹具(无 panel)—— ①–⑦ 的宿主。</summary>
        private static (CasebookScreen screen, VisualElement root) Build(
            ICasebookDtoSource src, ICasebookCommandSink sink)
        {
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(sink);
            screen.ApplyData(src);
            return (screen, root);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 后案 ⇒ 翻至方笺续页;前案 ⇒ 翻回
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_pageEdgeNext_flipsToPrescriptionPage_andPrev_flipsBack()
        {
            var (screen, _) = Build(new FakeSource { Detents = 5 }, new FakeSink());

            Assert.IsFalse(screen.IsOnPrescriptionPage, "初态 = 脉案页");

            screen.SubmitPageNext();
            Assert.IsTrue(screen.IsOnPrescriptionPage, "后案 Submit ⇒ 翻至方笺续页");

            screen.SubmitPagePrev();
            Assert.IsFalse(screen.IsOnPrescriptionPage, "前案 Submit ⇒ 翻回脉案页");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 两向不循环(首页无前案 / 末页无后案 —— 不是环绕)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_pageEdges_doNotWrap()
        {
            var (screen, _) = Build(new FakeSource { Detents = 5 }, new FakeSink());

            screen.SubmitPagePrev();   // 首页按前案
            Assert.IsFalse(screen.IsOnPrescriptionPage, "首页无上一页 ⇒ 停在脉案页(不环绕到末页)");

            screen.SubmitPageNext();   // → 方笺页
            screen.SubmitPageNext();   // 末页再按后案
            Assert.IsTrue(screen.IsOnPrescriptionPage, "末页无下一页 ⇒ 停在方笺页(不环绕到首页)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 折边 ⇒ 折叠态翻转
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_foldEdge_togglesFoldedState()
        {
            var (screen, _) = Build(new FakeSource { Detents = 5 }, new FakeSink());

            Assert.IsFalse(screen.IsFolded, "初态未折");
            screen.SubmitFold();
            Assert.IsTrue(screen.IsFolded, "折边 Submit ⇒ 折回");
            screen.SubmitFold();
            Assert.IsFalse(screen.IsFolded, "再 Submit ⇒ 展开");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 翻页 / 折页零流事件(AC-39-10)
        //
        // 真组合根 + 真 EventStream 计数 —— 全流程(翻过去 / 折 / 展开 / 翻回来)
        // 断言事件数不变。**这是纯呈现半条的可证伪面**。
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_pageTurnAndFold_zeroStreamEvents()
        {
            CompositionRootServices bag = CompositionRoot.Assemble();
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            // ⚠️ 写通道 = 真组合根的命令路由(其 SubmitPageTurn / SubmitFold 是 no-op)
            screen.BindCommandSink(bag.CasebookCommands);
            screen.ApplyData(new FakeSource { Detents = 5 });

            int before = bag.Stream.Count;

            screen.SubmitPageNext();
            screen.SubmitFold();
            screen.SubmitFold();
            screen.SubmitPagePrev();

            Assert.AreEqual(before, bag.Stream.Count,
                "翻页 / 折叠全流程 ⇒ 零流事件(AC-39-10:纯呈现量写分册态段,不进三流)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 戥子档位行数 = DTO 档数;userData = 显式 ordinal
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_dentchRows_matchDetentCount_withExplicitOrdinals()
        {
            var (screen, root) = Build(new FakeSource { Detents = 5 }, new FakeSink());

            screen.SubmitPageNext();   // 进方笺页 ⇒ 重建档位行

            List<VisualElement> rows = CollectDoseRows(root);
            Assert.AreEqual(5, rows.Count, "档位行数 = DoseDetentCount(= hi − lo + 1 = 5)");
            for (int i = 0; i < rows.Count; i++)
                Assert.AreEqual(i + 1, (int)rows[i].userData,
                    $"第 {i + 1} 行 userData = 显式 ordinal({i + 1}),非显示索引");
        }

        // ⑤b 翻页 ⇒ 焦点落进**新页**(B-6-2 人工复验红 · 2026-10-14)——
        //     **真 panel 判据,住 ⑧ 区**(逻辑树无 panel 持不了焦点状态,Focus() 落空)。

        // ══════════════════════════════════════════════════════════════════════
        // ⑥ 档位行 Submit ⇒ 写通道收到显式 ordinal
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_dentchRowSubmit_forwardsExplicitOrdinal()
        {
            var sink = new FakeSink();
            var (screen, root) = Build(new FakeSource { Detents = 5 }, sink);
            screen.SubmitPageNext();

            List<VisualElement> rows = CollectDoseRows(root);
            screen.SubmitDose((int)rows[2].userData);   // 第三档(ordinal = 3)

            Assert.AreEqual(new[] { 3 }, sink.Doses,
                "档位行 Submit ⇒ 转发**显式 ordinal**(3),非焦点路径索引");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑦ 档数 0 / 未接写通道 ⇒ 不崩、不造档位(零假数据)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_zeroDetents_noRows_zeroFakeData()
        {
            var (screen, root) = Build(new FakeSource { Detents = 0 }, new FakeSink());
            screen.SubmitPageNext();

            Assert.AreEqual(0, CollectDoseRows(root).Count,
                "档数 0(无方笺数据)⇒ 不造档位行(零假数据)");
            Assert.IsTrue(screen.IsOnPrescriptionPage, "书写面仍在(页翻了,只是无档)");
        }

        [Test]
        public void test_noCommandSink_pageTurnAndDose_noThrow()
        {
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.ApplyData(new FakeSource { Detents = 5 });   // 不 BindCommandSink

            Assert.DoesNotThrow(() =>
            {
                screen.SubmitPageNext();
                screen.SubmitFold();
                foreach (var r in CollectDoseRows(root)) screen.SubmitDose((int)r.userData);
                screen.SubmitPagePrev();
            }, "缺写通道 ⇒ 翻页 / 折页 / 落点留空(不崩、不造动作)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑧ 输入路径真派发(真 panel —— 引擎 EventDispatcher 实跑)
        // ══════════════════════════════════════════════════════════════════════

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;

        private void SetUpPanel()
        {
            _go = new GameObject("CasebookPageTurn_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
        }

        [TearDown]
        public void TearDownPanel()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null;
        }

        private IEnumerator WaitForRoot(UIDocument doc)
        {
            float t0 = Time.realtimeSinceStartup;
            while (doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 2f)
                {
                    Assert.Fail("[夹具红] UIDocument.rootVisualElement 2s 未就绪 —— " +
                                "panelSettings 未生效;这不是接线判据的结论,修夹具后重跑。");
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>把提交事件真发给元素(**引擎派发管径**;先落焦 —— 见文件头注)。</summary>
        private static void DispatchSubmit(VisualElement target)
        {
            target.Focus();
            using (var ev = NavigationSubmitEvent.GetPooled(EventModifiers.None))
                target.SendEvent(ev);
        }

        /// <summary>⑧a:页缘「后案」落焦 + 提交 ⇒ 翻至方笺续页。</summary>
        [UnityTest]
        public IEnumerator test_wiring_pageEdgeNextEvent_flipsToPrescriptionPage()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = 5 });
            Assert.IsFalse(screen.IsOnPrescriptionPage, "前置:脉案页");

            DispatchSubmit(root.Q<VisualElement>("page-edge-next"));

            Assert.IsTrue(screen.IsOnPrescriptionPage,
                "落焦「后案」+ 真派发 ⇒ 翻至方笺续页(接线缺失则本测红,而语义判据仍绿)");
        }

        /// <summary>
        /// ⑤b:翻页 ⇒ 焦点落进**新页**(B-6-2 人工复验红 · 2026-10-14)。
        /// <para>⚠️ **真 panel 判据**(逻辑树无 panel 持不了焦点状态,`Focus()` 落空
        /// —— 首版放逻辑树区即踩此坑:FocusController.focusedElement 恒 null ⇒ 恰红)。
        /// 病根:戥子行是翻页那一刻才建的,而焦点还停在旧页页缘;UI Toolkit 语义
        /// `display: None` 子树不参与焦点导航 ⇒ 焦点死在隐藏旧页,方向键走不到戥子行
        /// —— 焦点环忠实跟随引擎焦点,焦点没动环就不动,人工看到「环没跟过去」。
        /// 变异面:删 SubmitPageNext/Prev 里的 FocusFirstOf 调用 ⇒ 焦点仍在旧页 ⇒ 本测恰红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_pageTurn_focusLandsOnNewPage_firstFocusableRow()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = 5 });

            // 前置:脉案页获焦一行(模拟玩家翻页前的焦点态)
            var faceFirst = root.Q<VisualElement>("channel-complexion");
            Assert.IsNotNull(faceFirst, "[夹具红] 脉案页首行未找到");
            faceFirst.Focus();
            yield return null;
            Assert.IsTrue(screen.FocusedElementOfCasebookFace(),
                "[夹具红] 落焦未生效 —— 真 panel 下 Focus() 须可达");

            screen.SubmitPageNext();   // → 方笺页
            yield return null;

            // 判据:焦点在新页首个可焦后代(树序第一档位行),不在隐藏旧页
            Assert.IsTrue(screen.FocusedElementOfRxPage(),
                "翻页后焦点须落在方笺页首个可焦后代(第一档位行)—— " +
                "否则方向键死在隐藏旧页(人工复验红:B-6-2 「环没跟过去」的根因)");

            screen.SubmitPagePrev();   // ← 回脉案页
            yield return null;
            Assert.IsTrue(screen.FocusedElementOfCasebookFace(),
                "翻回后焦点须落回脉案页首个可焦后代(同纪律,双向都 bootstrap)");
        }

        /// <summary>
        /// ⑤c:【真机等价路径】**事件派发内**翻页 ⇒ 焦点落进新页(2026-10-14 第二轮人工复验红)。
        /// <para>⚠️ 与 ⑤b 的差异(即首修漏掉的面):⑤b 直调 <c>SubmitPageNext()</c>;
        /// 真机 = 玩家按 Enter ⇒ <c>NavigationSubmitEvent</c> 派发 ⇒ **handler 内部**
        /// 执行 display 切换 + 建行 + <c>Focus()</c>。「handler 内落焦」此前零测试覆盖 ——
        /// 若本测红 = 真机 bug 复现(事件派发中 focus 变更被引擎吞/延),修法须挪出派发期
        /// (如延迟一帧 / 调度到派发后);若本测绿 = 差异在别处(module / 导航起点),再深挖。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_wiring_pageTurnSubmit_focusLandsOnNewPage()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = 5 });

            // Act:真机等价 —— 落焦「后案」页缘 + 真派发 submit(翻页在其 handler 内发生)
            DispatchSubmit(root.Q<VisualElement>("page-edge-next"));
            yield return null;   // 真机会隔几帧才按方向键;给派发/布局一帧

            Assert.IsTrue(screen.IsOnPrescriptionPage, "[夹具红] 翻页未发生(submit 派发面失效)");
            Assert.IsTrue(screen.FocusedElementOfRxPage(),
                "⑤c 红:**事件派发内**翻页 ⇒ 焦点未落进新页 —— 真机路径(Enter 触发翻页)下 " +
                "handler 内 Focus() 未生效;与 ⑤b(直调)分叉即本判据的存在理由");
        }

        /// <summary>
        /// ⑤d:【tick 重建竞态 · 第二轮人工复验红的根因】翻页落焦后**每 tick ApplyData**
        /// ⇒ 焦点必须仍活(2026-10-14)。
        /// <para>⚠️ 真机差异面(测试此前测不到的原因):生产里 BootRoot 20 Hz tick 调
        /// <c>SkeuoRuntimeDriver.OnTickEdge</c> → <c>ApplyData</c> → 方笺页在场时
        /// <c>RefreshDentchDoses</c> **每 tick 全量摘掉重建档位行** ⇒ 翻页时落焦的第一行
        /// 在下一个 tick(50 ms 后)被移出层级 ⇒ 引擎**静默丢焦** ⇒ 方向键死在无焦点态。
        /// 本测手动模拟该 tick 流:翻页 → 落焦 → 连续多轮 ApplyData(同源同档数)→
        /// 断言焦点仍落在档位行上。修复 = 幂等短路(档数未变不拆建);
        /// **变异面:删 RefreshDentchDoses 的短路 ⇒ 本测恰红**(焦点在首轮重建即丢)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_tickRebuild_focusSurvivesRepeatedApplyData()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var source = new FakeSource { Detents = 5 };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(source);

            screen.SubmitPageNext();   // → 方笺页(handler 内建行 + FocusFirstOf)
            yield return null;
            Assert.IsTrue(screen.FocusedElementOfRxPage(), "[夹具红] 翻页落焦未生效(⑤b 面)");

            // Act:模拟真机 tick 流 —— 同源同档数连续 ApplyData(重建竞态面)
            for (int tick = 0; tick < 10; tick++)
            {
                screen.ApplyData(source);   // 真机每 tick 都调;源/档数不变
                yield return null;
            }

            // 判据:焦点仍在档位行上(未被 tick 重建摘掉)
            Assert.IsTrue(screen.FocusedElementOfRxPage(),
                "⑤d 红:每 tick ApplyData 全量重建档位行 ⇒ 持焦行被移出层级、引擎静默丢焦 " +
                "—— RefreshDentchDoses 须幂等短路(档数未变不拆建;变异:删短路 ⇒ 本测恰红)");
        }

        /// <summary>⑧b:页缘「前案」落焦 + 提交 ⇒ 翻回脉案页。</summary>
        [UnityTest]
        public IEnumerator test_wiring_pageEdgePrevEvent_flipsBack()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = 5 });
            screen.SubmitPageNext();
            Assert.IsTrue(screen.IsOnPrescriptionPage, "前置:方笺页");

            DispatchSubmit(root.Q<VisualElement>("page-edge-prev"));

            Assert.IsFalse(screen.IsOnPrescriptionPage, "落焦「前案」+ 真派发 ⇒ 翻回脉案页");
        }

        /// <summary>⑧c:折边落焦 + 提交 ⇒ 折叠态翻转。</summary>
        [UnityTest]
        public IEnumerator test_wiring_foldEdgeEvent_togglesFold()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = 5 });
            Assert.IsFalse(screen.IsFolded, "前置:未折");

            DispatchSubmit(root.Q<VisualElement>("fold-edge"));

            Assert.IsTrue(screen.IsFolded, "落焦折边 + 真派发 ⇒ 折回");
        }

        /// <summary>⑧d:戥子档位行落焦 + 提交 ⇒ 写通道收到显式 ordinal。</summary>
        [UnityTest]
        public IEnumerator test_wiring_dentchRowEvent_forwardsOrdinal()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var sink = new FakeSink();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(sink);
            screen.ApplyData(new FakeSource { Detents = 5 });
            screen.SubmitPageNext();   // 进方笺页 ⇒ 重建档位行

            List<VisualElement> rows = CollectDoseRows(root);
            Assert.AreEqual(5, rows.Count, "前置:五档行");

            DispatchSubmit(rows[3]);   // 第四档(ordinal = 4)

            Assert.AreEqual(new[] { 4 }, sink.Doses,
                "落焦档位行 + 真派发 ⇒ 转发显式 ordinal(4)");
        }

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>收集戥子档位行(方笺页内 userData 为 int 的行)。</summary>
        private static List<VisualElement> CollectDoseRows(VisualElement root)
        {
            var rows = new List<VisualElement>();
            var page = root.Q<VisualElement>("prescription-page");
            if (page == null) return rows;
            Walk(page);
            return rows;

            void Walk(VisualElement e)
            {
                if (e.userData is int) rows.Add(e);
                foreach (var c in e.Children()) Walk(c);
            }
        }
    }
}
