// sprint-08 S1.1 件 F1/G · 病名册交互面判据(PlayMode · 2026-10-12)
//
// 判据(可证伪):
//   ① 病名栏 Submit ⇒ 病名册摊开,条目 = 已见集经词表反查的 lexicon_id(升序);
//   ② 条目文本经 TextOfLexicon 取(42 不持词表);未见过的病**不在册**(反幻想二);
//   ③ 条目行 Submit ⇒ 写通道收到**显式 lexicon_id**(非册内显示索引 —— TD 第五风险);
//   ④ 已摊开时病名栏再 Submit ⇒ SubmitConfidence(档位循环交给装配层);
//   ⑤ 未注入写通道 ⇒ Submit 留空(不崩、不造动作 —— 缺通道 = 未接线的诚实形态);
//   ⑥ 病名册容器**不进 UXML spec 面 name 序**(交叉断言零回归的结构面);
//   ⑦ **输入路径真派发**:真 `PanelSettings` + `UIDocument` ⇒ 引擎 EventDispatcher 实跑,
//      `NavigationSubmitEvent` 打到病名栏行 / 条目行 ⇒ 语义动作真发生(接线可证伪)。
//
// ⚠️ ①–⑥ 与 ⑦ 的分工(sprint-08 §4 预裁「判据与输入路径解耦」):
//   · ①–⑥ = **语义判据** —— 逻辑树直调语义方法,零渲染面依赖,任何宿主都跑得动;
//   · ⑦ = **接线判据** —— 只证明「引擎派发 ⇒ 我们的 handler」这一段真的通。
//     二者缺一不可:①–⑥ 绿而 ⑦ 红 = 语义对但没接线(玩家按了没反应);
//     ⑦ 绿而 ①–⑥ 红 = 接线对但语义错。**删掉 `RegisterCallback` 行 ⇒ ⑦ 恰红**。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>病名册交互面判据(sprint-08 S1.1 件 F1)。</summary>
    internal sealed class casebook_lexicon_book_test
    {
        // ── 合成 fixture ────────────────────────────────────────────────────

        private sealed class FakeSource : ICasebookDtoSource
        {
            public readonly List<int> Seen = new List<int>();
            public readonly Dictionary<int, string> Texts = new Dictionary<int, string>();
            public byte Pending;

            public int ActiveSignCount => 0;
            public IReadOnlyList<SignLexemeRow> RevealedRows => new List<SignLexemeRow>();
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0;
            public IReadOnlyList<int> SeenLexiconIds => Seen;
            public byte PendingConfidence => Pending;
            public string TextOfLexicon(int id) => Texts.TryGetValue(id, out var t) ? t : null;
        }

        private sealed class FakeSink : ICasebookCommandSink
        {
            public readonly List<int> Points = new List<int>();
            public int ConfidenceCycles;

            public void SubmitDiseaseName(int lexiconId) => Points.Add(lexiconId);
            public void SubmitConfidence() => ConfidenceCycles++;
        }

        /// <summary>逻辑树夹具(无 panel)—— ①–⑥ 的宿主。</summary>
        private static (CasebookScreen screen, VisualElement root, FakeSource src, FakeSink sink) Build()
        {
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            var sink = new FakeSink();
            screen.BindCommandSink(sink);
            screen.ApplyData(src);
            return (screen, root, src, sink);
        }

        /// <summary>语义动作入口(①–⑥ 走此路 —— 判据不依赖渲染面)。</summary>
        private static void SubmitDiseaseNameBar(CasebookScreen s) => s.SubmitDiseaseNameBar();

        // ══════════════════════════════════════════════════════════════════════
        // ① 病名栏 Submit ⇒ 病名册摊开 + 条目按 lexicon_id
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_diseaseNameSubmit_opensBookWithSeenLexiconIds()
        {
            var (screen, root, src, _) = Build();
            src.Seen.AddRange(new[] { 7, 9 });
            src.Texts[7] = "伤寒";
            src.Texts[9] = "温病";

            Assert.IsFalse(screen.IsLexiconBookOpen, "初始未摊开");

            SubmitDiseaseNameBar(screen);

            Assert.IsTrue(screen.IsLexiconBookOpen, "病名栏 Submit ⇒ 病名册摊开");
            var entries = CollectEntryRows(root);
            Assert.AreEqual(2, entries.Count, "条目数 = 已见集(词表反查)");
            Assert.AreEqual("伤寒", ((Label)entries[0][0]).text, "条目文本经 TextOfLexicon 取");
            Assert.AreEqual("温病", ((Label)entries[1][0]).text, "序 = 词表序(lexicon_id 升序)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 未见过的病不在册(反幻想二)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_lexiconBook_unseenDisease_notPointable()
        {
            var (screen, root, src, _) = Build();
            src.Seen.Add(7);          // 只见过 7
            src.Texts[7] = "伤寒";
            src.Texts[9] = "温病";    // 9 有文本但未见过

            SubmitDiseaseNameBar(screen);

            var entries = CollectEntryRows(root);
            Assert.AreEqual(1, entries.Count, "未见过 ⇒ 不入册(AC-8-43/44)");
            Assert.AreEqual("伤寒", ((Label)entries[0][0]).text);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 条目 Submit ⇒ 写通道收到显式 lexicon_id(非显示索引)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_entrySubmit_forwardsExplicitLexiconId()
        {
            var (screen, root, src, sink) = Build();
            // ⚠️ 显示序(7, 9)与 id 值刻意不同 —— 若用显示索引,会写成 0/1
            src.Seen.AddRange(new[] { 7, 9 });
            src.Texts[7] = "伤寒"; src.Texts[9] = "温病";

            SubmitDiseaseNameBar(screen);
            var entries = CollectEntryRows(root);
            screen.PointLexicon((int)entries[1].userData);        // 第二条(显示索引 1,真实 lexicon_id = 9)

            Assert.AreEqual(new[] { 9 }, sink.Points,
                "指认转发**显式 lexicon_id**(9),非册内显示索引(1)—— TD 第五风险");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 已摊开再 Submit ⇒ 档位循环(交给装配层)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_secondDiseaseNameSubmit_cyclesConfidence()
        {
            var (screen, _, src, sink) = Build();
            src.Seen.Add(7); src.Texts[7] = "伤寒";

            SubmitDiseaseNameBar(screen);   // 摊开(不计档位)
            SubmitDiseaseNameBar(screen);   // 循环 1
            SubmitDiseaseNameBar(screen);   // 循环 2

            Assert.AreEqual(2, sink.ConfidenceCycles,
                "已摊开时病名栏 Submit = 档位循环(42 不持档位,交装配层)");
            Assert.AreEqual(0, sink.Points.Count, "档位循环**不**再指认(不写第二笔词)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 未注入写通道 ⇒ Submit 留空(不崩)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_noCommandSink_submitIsNoop()
        {
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.Add(7);
            screen.ApplyData(src);          // 不 BindCommandSink

            Assert.DoesNotThrow(() =>
            {
                SubmitDiseaseNameBar(screen);
                foreach (var e in CollectEntryRows(root)) screen.PointLexicon((int)e.userData);
            }, "缺写通道 ⇒ Submit 留空(不崩、不造动作)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑥ 病名册容器不进 spec 面 name 序(交叉断言零回归的结构面)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_bookContainer_isAnonymous_notInNamedSequence()
        {
            var (screen, root, src, _) = Build();
            src.Seen.Add(7); src.Texts[7] = "伤寒";
            SubmitDiseaseNameBar(screen);

            var named = new List<string>();
            CollectNamed(root, named);
            CollectionAssert.DoesNotContain(named, "lexicon-book",
                "病名册容器匿名(不进 UXML spec 面 name 序 ⇒ 交叉断言零回归)");
            // 既有 spec 面 name 仍在(容器不挤占)
            CollectionAssert.Contains(named, "channel-disease-name");
            CollectionAssert.Contains(named, "confidence-mark");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑦ 输入路径真派发(真 panel —— 引擎 EventDispatcher 实跑)
        //
        // 前六条直调语义方法,证明不了「handler 挂上了」。本条用真 PanelSettings +
        // UIDocument 建 panel,把 NavigationSubmitEvent **真发给行**,断言语义动作发生。
        // 变异:删 `RegisterCallback<NavigationSubmitEvent>` ⇒ 本条恰红(而 ①–⑥ 仍绿)。
        // ══════════════════════════════════════════════════════════════════════

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;

        /// <summary>建真 panel 宿主(承 `ac42c10_focus_landing_test` 同款三件套)。</summary>
        private void SetUpPanel()
        {
            _go = new GameObject("CasebookLexiconBook_UnderTest");
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

        /// <summary>等 root 就绪(超时报夹具红,不报判据红 —— 承 ac42c10 三条判读纪律)。</summary>
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

        /// <summary>
        /// 把提交事件真发给元素(**引擎派发管径**)。
        /// <para>⚠️ <b>必须先 <c>Focus()</c></b> —— 2026-10-12 实测(cluster batchmode,真 panel):
        /// <c>NavigationSubmitEvent</c> 属 <c>NavigationEventBase</c> 族,其派发目标由
        /// <c>FocusController.focusedElement</c> 决定;**无焦点时事件被丢弃**(裸
        /// <c>SendEvent</c> 探针命中 0;先 <c>Focus()</c> 再发 ⇒ 命中 1)。
        /// 这不是测试夹具的怪癖,而是**引擎的真实语义**:导航提交永远落在当前焦点元素上 ——
        /// 故「先落焦、再提交」正是玩家的真实动作序(导航到病名栏 → 按提交)。</para>
        /// </summary>
        private static void DispatchSubmit(VisualElement target)
        {
            target.Focus();
            using (var ev = NavigationSubmitEvent.GetPooled(EventModifiers.None))
                target.SendEvent(ev);
        }

        /// <summary>⑦a:病名栏行「落焦 + 提交」(真派发)⇒ 病名册摊开。</summary>
        [UnityTest]
        public IEnumerator test_submitWiring_diseaseNameRowEvent_opensBook()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel,
                "[夹具红] panel 为 null —— UIDocument 未真正挂到 panel,归【桌面】;不是接线判据的结论");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.Add(7); src.Texts[7] = "伤寒";
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(src);

            Assert.IsFalse(screen.IsLexiconBookOpen, "前置:未摊开");

            DispatchSubmit(root.Q<VisualElement>("channel-disease-name"));

            Assert.IsTrue(screen.IsLexiconBookOpen,
                "落焦病名栏 + 真派发 NavigationSubmitEvent ⇒ 摊开(接线缺失则本测红,而①–⑥仍绿)");
        }

        /// <summary>⑦b:条目行「落焦 + 提交」(真派发)⇒ 写通道收到显式 lexicon_id。</summary>
        [UnityTest]
        public IEnumerator test_submitWiring_entryRowEvent_forwardsLexiconId()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.AddRange(new[] { 7, 9 });       // 显示序 ≠ id 值(变异敏感)
            src.Texts[7] = "伤寒"; src.Texts[9] = "温病";
            var sink = new FakeSink();
            screen.BindCommandSink(sink);
            screen.ApplyData(src);

            DispatchSubmit(root.Q<VisualElement>("channel-disease-name"));   // 摊开
            var entries = CollectEntryRows(root);
            Assert.AreEqual(2, entries.Count, "前置:两条条目");

            DispatchSubmit(entries[1]);               // 落焦第二条(显示索引 1,id = 9)+ 提交

            Assert.AreEqual(new[] { 9 }, sink.Points,
                "条目行落焦 + 真派发 ⇒ 转发**显式 lexicon_id**(9),非显示索引(1)");
        }

        /// <summary>
        /// ⑦c:反向守卫 —— **无焦点时提交被引擎丢弃**(证明 ⑦a/⑦b 的 <c>Focus()</c> 是承重的)。
        /// <para>若删掉 <see cref="DispatchSubmit"/> 里的 <c>Focus()</c>,本测绿而 ⑦a/⑦b 恰红;
        /// 若引擎语义变了(无焦点也派发),本测恰红 ⇒ 提醒复核 ⑦a/⑦b 的判读。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_submitWiring_noFocus_eventDropped()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.Add(7); src.Texts[7] = "伤寒";
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(src);

            var row = root.Q<VisualElement>("channel-disease-name");
            Assert.IsNull(root.panel.focusController.focusedElement, "前置:无焦点");
            using (var ev = NavigationSubmitEvent.GetPooled(EventModifiers.None))
                row.SendEvent(ev);                    // 不 Focus,直接发

            Assert.IsFalse(screen.IsLexiconBookOpen,
                "无焦点 ⇒ NavigationSubmitEvent 被 FocusController 丢弃(引擎语义;承 ⑦a 的 Focus() 为承重)");
        }

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>收集病名册条目行(容器匿名;条目行 = 无 name 但 userData 为 int 的纸元件)。</summary>
        private static List<VisualElement> CollectEntryRows(VisualElement root)
        {
            var rows = new List<VisualElement>();
            Walk(root);
            return rows;

            void Walk(VisualElement e)
            {
                if (e.userData is int) rows.Add(e);
                foreach (var c in e.Children()) Walk(c);
            }
        }

        private static void CollectNamed(VisualElement e, List<string> acc)
        {
            if (!string.IsNullOrEmpty(e.name)) acc.Add(e.name);
            foreach (var c in e.Children()) CollectNamed(c, acc);
        }
    }
}
