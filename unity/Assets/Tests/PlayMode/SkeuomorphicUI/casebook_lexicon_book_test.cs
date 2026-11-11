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

            /// <summary>活跃体征数(0 = 全空行;>0 才放行 <see cref="Rows"/> 进值区)。</summary>
            public int ActiveCount;

            /// <summary>已查词条(2026-11-11 · ⑧d 用真 `ApplyData` 路径造「非空行」)。</summary>
            public readonly List<SignLexemeRow> Rows = new List<SignLexemeRow>();
            public byte Pending;
            public int DetentCount;   // 方笺页戥子档数(S2)

            public int ActiveSignCount => ActiveCount;
            public IReadOnlyList<SignLexemeRow> RevealedRows => Rows;
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0;
            public IReadOnlyList<int> SeenLexiconIds => Seen;
            public byte PendingConfidence => Pending;
            public int DoseDetentCount => DetentCount;
            public string TextOfLexicon(int id) => Texts.TryGetValue(id, out var t) ? t : null;
        }

        private sealed class FakeSink : ICasebookCommandSink
        {
            public readonly List<int> Points = new List<int>();
            public readonly List<int> Doses = new List<int>();
            public int ConfidenceCycles;
            public int PageTurns;
            public int Folds;

            // 2026-11-02(`TD-diagnosis-01`):行级查体接口增员;本测不涉及查体路径 ⇒ 留空。
            public void ExamineChannel(DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel channel) { }
            public void SubmitDiseaseName(int lexiconId) => Points.Add(lexiconId);
            public void SubmitConfidence() => ConfidenceCycles++;
            public void SubmitDose(int doseOrdinal) => Doses.Add(doseOrdinal);
            public void SubmitPageTurn() => PageTurns++;
            public void SubmitFold() => Folds++;
            public void SubmitEmergencySkip() { }   // story-009 接口增员(本测不涉及跳过路径)
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
            // ⚠️ 2026-10-14:档位循环**只在已落笔(非空栏)时可达** —— 空栏 Submit 一律摊册
            // (防每帧异常洪泛杀编辑器)。本测主旨是「已摊开 ⇒ 循环」⇒ 夹具须显式置已落笔档,
            // 而不是靠 `Pending` 默认值 0 蒙过去(那样测的是「空栏」场景,与本测主旨相反)。
            src.Pending = 1;                // 已落笔(疑似档)

            SubmitDiseaseNameBar(screen);   // 摊开(不计档位)
            SubmitDiseaseNameBar(screen);   // 循环 1
            SubmitDiseaseNameBar(screen);   // 循环 2

            Assert.AreEqual(2, sink.ConfidenceCycles,
                "已摊开时病名栏 Submit = 档位循环(42 不持档位,交装配层)");
            Assert.AreEqual(0, sink.Points.Count, "档位循环**不**再指认(不写第二笔词)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④b 空栏时病名栏 Submit ⇒ **摊开册子**,不碰档位循环(2026-10-14 卡死回归)
        //
        // 病根:旧实现已摊开即直调 `SubmitConfidence()` ⇒ 装配层 `_currentLexiconId <= 0`
        // fail-loud 抛 InvalidOperationException;而本方法是 ClickEvent / NavigationSubmitEvent
        // 回调 ⇒ 异常冲进 InputSystemUIInputModule 派发栈**每帧刷** ⇒ Console 管道超载 ⇒
        // `write EPIPE` ⇒ 编辑器进程被杀(用户实测「准备退出关掉 play 卡死」;
        // 与 2026-10-10 AudioListener 洪泛 31868 条同一死法)。
        // 判据:空栏 Submit **不得抛**、**不得调 SubmitConfidence**,而是摊开册子。
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_emptyBar_submitOpensBook_doesNotCycleConfidence()
        {
            var (screen, _, src, sink) = Build();
            src.Seen.Add(7); src.Texts[7] = "伤寒";
            src.Pending = 0;                     // 空栏:未指认任何词

            SubmitDiseaseNameBar(screen);        // 首次 ⇒ 摊开
            Assert.IsTrue(screen.IsLexiconBookOpen, "首次 Submit ⇒ 摊开");

            Assert.DoesNotThrow(() => SubmitDiseaseNameBar(screen),
                "空栏(已摊开)再 Submit 须**不抛** —— 旧实现会抛 ⇒ 每帧刷 ⇒ EPIPE 杀编辑器");
            Assert.AreEqual(0, sink.ConfidenceCycles,
                "空栏 ⇒ 无词可循环 ⇒ 不调 SubmitConfidence(改回旧实现 ⇒ 恰红)");
            Assert.IsTrue(screen.IsLexiconBookOpen, "空栏 Submit ⇒ 仍保持摊开(让你选词)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④c 已落笔(非空栏)时病名栏 Submit ⇒ 照常循环档位(反向判据:门不得过宽)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_filledBar_submitCyclesConfidence()
        {
            var (screen, _, src, sink) = Build();
            src.Seen.Add(7); src.Texts[7] = "伤寒";
            src.Pending = 1;                     // 已落笔(疑似档)

            SubmitDiseaseNameBar(screen);        // 摊开
            SubmitDiseaseNameBar(screen);        // 已落笔 ⇒ 循环 1

            Assert.AreEqual(1, sink.ConfidenceCycles,
                "非空栏 ⇒ 档位循环照常可达(守护门不得把已落笔路径一并挡掉)");
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

        /// <summary>
        /// ⑦d:**开册即落焦** —— 只按玩家的真实动作序(落焦病名栏 → 提交),**不预先给条目 `Focus()`**。
        /// <para><b>病因(2026-11-06 真包走查)</b>:病名栏 Submit 原只做「开册 + 建条目」而**不移焦**;
        /// 册子容器又是 append 到 `right-column` **末尾**,树序排在「折」(`fold-edge`)之后
        /// ⇒ 玩家按一次 <c>Tab</c> 落在「折」上,而「折」的 Submit 是**右页折回** ——
        /// 看到的是「界面变了」,不会想到「我跳过了册子」。真包实测两次误入。</para>
        /// <para>⚠️ 与 ⑦b 的分工:⑦b 用 `DispatchSubmit(entries[i])` 里自带的 `Focus()`
        /// **强设**焦点再提交 ⇒ 它证的是「条目收到 Submit 会指认」,**跳过了可达性**。
        /// 本测补的就是那一步:开册之后焦点是否**已经**在第一条条目上。</para>
        /// <para>⚠️ 判别力:删掉 `FocusFirstLexiconEntry()` 的调用 ⇒ 焦点仍停在病名栏 ⇒ 本测红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_openBook_movesFocusToFirstEntry()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.AddRange(new[] { 7, 9 });
            src.Texts[7] = "伤寒"; src.Texts[9] = "温病";
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(src);

            VisualElement row = root.Q<VisualElement>("channel-disease-name");
            Assert.IsNotNull(row, "前置:病名栏存在");

            // 玩家的真实动作序:导航到病名栏(落焦)→ 按提交。此后**不再**给任何元素 Focus()。
            DispatchSubmit(row);

            // 🔁 2026-11-07:落焦**推迟一帧**(见 `CasebookScreen.FocusFirstLexiconEntry` 的注)——
            //    在 `NavigationSubmitEvent` 派发过程中直接 `Focus()` 会被随后的输入更新清掉
            //    (手柄路径实测;键鼠此处是直接 SendEvent,故旧断言同步读也能过)。
            yield return null;

            Assert.IsTrue(screen.IsLexiconBookOpen, "前置:册子已摊开");
            var entries = CollectEntryRows(root);
            Assert.AreEqual(2, entries.Count, "前置:两条条目");

            Assert.AreEqual(entries[0], root.panel.focusController.focusedElement,
                "开册后焦点须在**第一条条目**上 —— 否则玩家要 Tab 过「折」才能进册子," +
                "而在「折」上提交会把右页折回(真包实测的误入形态)");
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

        // ══════════════════════════════════════════════════════════════════════
        // ⑧ T22 批十一 —— `TD-casebook-04` 的两个焦点坑 + `AC-39-12` 的反馈声
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ⑧a:落笔一次之后,焦点**留在刚用的条目**(`TD-casebook-04` 坑①)。
        /// <para><b>病因</b>:`PointLexicon` → `RefreshAfterAction` → `ApplyData` →
        /// `RefreshLexiconBook` 的 `Clear()` 把焦点元素摘出层级 ⇒ 引擎焦点变 null ⇒
        /// 想改判的玩家「没有焦点」,只能从头 Tab 一遍。</para>
        /// <para>判别力:把 `RestoreLexiconFocus` 的调用删掉 ⇒ 本条红(而 ⑦d「开册落焦」仍绿;
        /// 两者测的是不同时刻 —— 开册时 vs 写后重建时)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_pointIdentify_keepsFocusOnUsedEntry()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.AddRange(new[] { 7, 9 });
            src.Texts[7] = "伤寒"; src.Texts[9] = "温病";
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(src);

            DispatchSubmit(root.Q<VisualElement>("channel-disease-name"));   // 摊开
            yield return null;                                               // 等开册落焦

            var entries = CollectEntryRows(root);
            Assert.AreEqual(2, entries.Count, "前置:两条条目");

            // 玩家动作:导航到第二条条目 + 提交(= 指认 lexicon_id 9)
            DispatchSubmit(entries[1]);
            yield return null;                                               // 等重建后的落焦

            Assert.IsTrue(screen.IsLexiconBookOpen, "落笔后册子仍摊开(档位循环的入口)");
            VisualElement focused = root.panel.focusController.focusedElement as VisualElement;
            Assert.IsNotNull(focused,
                "落笔后焦点必须**仍在册内**(旧实现被 `Clear()` 摘掉 ⇒ 无焦点,改判无从下手)");
            Assert.IsTrue(focused.userData is int, "焦点元素须是条目行(userData = lexicon_id)");
            Assert.AreEqual(9, (int)focused.userData,
                "焦点须回到**刚用的那一条**(9),不是重建后的第一条(7)");
        }

        /// <summary>
        /// ⑧b:册已摊开时,病名栏的下一格焦点是**册条目**,不是「折」(`TD-casebook-04` 坑②)。
        /// <para>判据 = 树序即焦点序(ADR-013 §五 不自实现导航算法):取全树 focusable 序,
        /// 断言 `channel-disease-name` 的下一项是条目(`userData` 为 int)、且条目之后才是
        /// `fold-edge`。判别力:把容器改回 `host.Add`(排在折边之后)⇒ 本条红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_openBook_entriesPrecedeFoldEdge_inFocusOrder()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var src = new FakeSource();
            src.Seen.AddRange(new[] { 7, 9 });
            src.Texts[7] = "伤寒"; src.Texts[9] = "温病";
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(src);

            DispatchSubmit(root.Q<VisualElement>("channel-disease-name"));
            yield return null;

            var order = new List<VisualElement>(
                root.Query<VisualElement>().Where(e => e.focusable).ToList());
            int iName = order.FindIndex(e => e.name == "channel-disease-name");
            int iFold = order.FindIndex(e => e.name == "fold-edge");
            Assert.GreaterOrEqual(iName, 0, "前置:病名栏可聚焦");
            Assert.GreaterOrEqual(iFold, 0, "前置:折边可聚焦");

            VisualElement next = order[iName + 1];
            Assert.IsTrue(next.userData is int,
                $"病名栏的下一格须是**册条目**(userData = lexicon_id);实得 {next.name}。" +
                "落在折边上 ⇒ 再按 Submit 会把右页折回(玩家以为「界面变了」)");

            int iEntry = order.FindIndex(iName + 1, e => e.userData is int);
            Assert.GreaterOrEqual(iEntry, 0, "前置:册条目在焦点序里");
            Assert.Less(iEntry, iFold,
                "册条目须排在折边**之前**(病名栏出发一次 Down 一格可达 —— TD-casebook-04 坑②)");
        }

        /// <summary>
        /// ⑧c:空行落焦 ⇒ **请求**一次纸面压痕声(`AC-39-12` 第一半 · GDD `casebook.md:224`)。
        /// <para>42 只请求不发声(ADR-018 §一):本测断言请求次数,不断言音频。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_emptyRowFocus_requestsPaperIndentOnce()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var cues = new FakeCueSink();
            screen.BindCueSink(cues);
            var src = new FakeSource();          // ActiveSignCount = 0 ⇒ 六行全空
            screen.ApplyData(src);

            VisualElement row = root.Q<VisualElement>("channel-complexion");
            Assert.IsNotNull(row, "前置:面色行存在");
            row.Focus();
            yield return null;

            Assert.AreEqual(1, cues.Indents,
                "空行获得焦点 ⇒ 请求一次压痕声(零提示文字,反馈只有声 + 高亮)");
        }

        /// <summary>
        /// ⑧d:非空行(有读数格)落焦 ⇒ **不**请求压痕声。
        /// <para>数据面走真 `RevealedRows`(生产同一条 `ApplyData` 路径),不在夹具里
        /// 手造 `.channel-reading` 元素 —— 否则测的是夹具自己的类名。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_nonEmptyRowFocus_doesNotRequestIndent()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var cues = new FakeCueSink();
            screen.BindCueSink(cues);
            var src = new FakeSource { ActiveCount = 1 };
            src.Rows.Add(new SignLexemeRow(
                "sign_pallor", new[] { "面色苍白", null, null },
                SignChannel.FaceColor, new[] { RevealMethod.Inspection }, 1,
                SignPolarity.Positive, null));
            screen.ApplyData(src);

            VisualElement row = root.Q<VisualElement>("channel-complexion");
            Assert.IsNotNull(row, "前置:面色行存在");
            Assert.IsNotNull(row.Query<VisualElement>(className: "channel-reading").First(),
                "前置:该行已有读数格(数据面驱动,非夹具手造)");

            row.Focus();
            yield return null;

            Assert.AreEqual(0, cues.Indents,
                "有读数的行不请求压痕声 —— 压痕声只在「空行」上(否则翻册时连续压痕盖住呼吸层)");
        }

        /// <summary>⑧e:翻页(前案 / 后案)⇒ 各请求一次翻页声;逻辑切换仍即时(display 立即切)。</summary>
        [UnityTest]
        public IEnumerator test_pageTurn_requestsCue_andSwitchesImmediately()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var cues = new FakeCueSink();
            screen.BindCueSink(cues);
            screen.ApplyData(new FakeSource());

            screen.SubmitPageNext();
            VisualElement rx = root.Q<VisualElement>("prescription-page");
            Assert.AreEqual(DisplayStyle.Flex, rx.style.display.value,
                "翻页的**逻辑切换**须即时(淡入只是视觉,不延迟状态)");
            Assert.AreEqual(1, cues.PageTurns, "翻页 ⇒ 请求一次翻页声");

            screen.SubmitPagePrev();
            Assert.AreEqual(2, cues.PageTurns, "回翻 ⇒ 再请求一次");
        }

        /// <summary>⑧f:翻页淡入时长受动效缩放约束(`AC-42-G4`:Scale=0 ⇒ 瞬切且完全可用)。</summary>
        [UnityTest]
        public IEnumerator test_pageTurn_fadeDuration_scalesAndDegradesToInstant()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.ApplyData(new FakeSource());

            VisualElement rx = root.Q<VisualElement>("prescription-page");

            screen.SubmitPageNext();
            yield return null;                    // 等淡入的调度步执行
            Assert.Greater(rx.style.transitionDuration.value[0].value, 0f,
                "Scale = 1 ⇒ 翻页有可见淡入(不是瞬切)");
            Assert.AreEqual(1f, rx.style.opacity.value, 1e-3f,
                "淡入目标 = 完全可见");

            // 无障碍:Scale = 0 ⇒ 无动效,新页立即全可见
            screen.SubmitPagePrev();              // 回到首页
            screen.Motion.Scale = 0f;
            screen.SubmitPageNext();
            yield return null;
            Assert.AreEqual(1f, rx.style.opacity.value, 1e-3f,
                "Scale = 0 ⇒ 不设透明起点(瞬切且完全可用,AC-42-G4 后半)");
        }

        /// <summary>测试用反馈声接缝(记录请求次数;不发任何真实音频)。</summary>
        private sealed class FakeCueSink : ICasebookCueSink
        {
            public int Indents;
            public int PageTurns;
            public void RequestPaperIndent() => Indents++;
            public void RequestPageTurn() => PageTurns++;
        }

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
