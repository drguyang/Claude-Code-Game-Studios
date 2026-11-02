// 2026-11-02 · `TD-diagnosis-01` 收口件 —— 脉案六行的**行级查体**接线判据(PlayMode)
//
// 判据(可证伪):
//   ① 六个通道行各自落焦 + Submit ⇒ 写通道收到**该行对应的通道键**,顺序与派发序一致;
//   ② 行级 Submit **只**发查体 —— 不翻页、不折页、不提交档位(不误触发邻行的动作);
//   ③ 未注入写通道 ⇒ Submit 不抛(与其余 Submit 同款 null 安全)。
//
// 为什么单独立件:
//   `TD-diagnosis-01` 之前,查体挂在世界 E 键上;本轮把它搬进脉案六行。
//   `CasebookCommandRouter` 那一侧已有 3 条夹具(`casebook_command_router_test`),
//   但**屏幕 → 写通道**这一段(六行各绑哪个通道键)此前**零断言** ——
//   接口成员被各 `FakeSink` 实现只是为了过编译(F2 形态:实现 ≠ 被断言)。
//   若把 `BindChannelExamine` 的通道键写错(例如六行全绑 FaceColor),
//   路由侧夹具仍全绿,而玩家按「语声」会查出「面色」。
//
// ⚠️ 与 `casebook_page_turn_test` 同纪律:真 `PanelSettings` + `UIDocument`
//   (`NavigationSubmitEvent` 的派发目标由 `FocusController.focusedElement` 决定,
//   无 panel 时 `Focus()` 落空 ⇒ 必须走真 panel 路径)。
// ⚠️ 42 屏相关判据住 PlayMode `Gameplay.Tests`(它引用 `Gameplay.UI`;
//   EditMode `Sim.Contracts.Tests` 的引用集够不着 42 屏)。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using DiagnosisSignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>脉案六行 ⇒ 行级查体(通道键正确性 + 零误触发)。</summary>
    internal sealed class casebook_row_examine_test
    {
        private sealed class FakeSource : ICasebookDtoSource
        {
            public int ActiveSignCount => 0;
            public IReadOnlyList<SignLexemeRow> RevealedRows => new List<SignLexemeRow>();
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0L;
            public IReadOnlyList<int> SeenLexiconIds => new List<int>();
            public byte PendingConfidence => 0;
            public int DoseDetentCount => 5;
            public string TextOfLexicon(int lexiconId) => null;
        }

        private sealed class RecordingSink : ICasebookCommandSink
        {
            public readonly List<DiagnosisSignChannel> Examined = new List<DiagnosisSignChannel>();
            public int PageTurns;
            public int Folds;
            public readonly List<int> Doses = new List<int>();

            public void ExamineChannel(DiagnosisSignChannel channel) => Examined.Add(channel);
            public void SubmitDiseaseName(int lexiconId) { }
            public void SubmitConfidence() { }
            public void SubmitDose(int doseOrdinal) => Doses.Add(doseOrdinal);
            public void SubmitPageTurn() => PageTurns++;
            public void SubmitFold() => Folds++;
            public void SubmitEmergencySkip() { }
        }

        // ── panel 夹具(照 `casebook_page_turn_test` 的 ⑧ 面)──────────────────

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;

        private void SetUpPanel()
        {
            _go = new GameObject("CasebookRowExamine_UnderTest");
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

        /// <summary>把提交事件真发给元素(引擎派发管径;先落焦)。</summary>
        private static void DispatchSubmit(VisualElement target)
        {
            Assert.IsNotNull(target, "[夹具红] 目标行不存在 —— 版面改坏,先修版面");
            target.Focus();
            using (var ev = NavigationSubmitEvent.GetPooled(EventModifiers.None))
                target.SendEvent(ev);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 六行各发各的通道(顺序与派发序一致)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_sixChannelRows_submit_examineMatchingChannel()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var sink = new RecordingSink();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(sink);
            screen.ApplyData(new FakeSource());

            // 行名 → 期望通道(权威 = `disease-symptom` 六通道;`channel-inquiry` = 问诊/病史)
            var expected = new (string row, DiagnosisSignChannel channel)[]
            {
                ("channel-complexion", DiagnosisSignChannel.FaceColor),
                ("channel-voice",      DiagnosisSignChannel.Voice),
                ("channel-posture",    DiagnosisSignChannel.Posture),
                ("channel-breathing",  DiagnosisSignChannel.Breathing),
                ("channel-palpation",  DiagnosisSignChannel.Touch),
                ("channel-inquiry",    DiagnosisSignChannel.History),
            };

            foreach ((string row, DiagnosisSignChannel channel) in expected)
            {
                DispatchSubmit(root.Q<VisualElement>(row));
                Assert.AreEqual(1, sink.Examined.Count,
                    $"[夹具红/{row}] 落焦 + 真派发 ⇒ 应恰发一条查体(handler 未接?Focus 落空?)");
                Assert.AreEqual(channel, sink.Examined[sink.Examined.Count - 1],
                    $"{row} 的行级查体必须提交**该行自己的**通道键 —— " +
                    "通道键写错/写成行索引时,玩家按「语声」会查出别的通道");
                sink.Examined.Clear();
            }

            // 判别力前置:六个通道**互不相同**(否则上例逐行相等在「全绑同一通道」下也会绿)
            var distinct = new HashSet<DiagnosisSignChannel>();
            foreach ((string _, DiagnosisSignChannel ch) in expected) distinct.Add(ch);
            Assert.AreEqual(6, distinct.Count,
                "夹具不变量:六个期望通道必须两两不同 —— 否则本测对「全绑一个通道」零判别力");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 行级 Submit 只发查体 —— 不误触发邻行动作
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_channelRowSubmit_onlyExamines_noNeighbourAction()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var sink = new RecordingSink();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(sink);
            screen.ApplyData(new FakeSource());

            foreach (string row in new[]
                     { "channel-complexion", "channel-voice", "channel-posture",
                       "channel-breathing", "channel-palpation", "channel-inquiry" })
                DispatchSubmit(root.Q<VisualElement>(row));

            Assert.AreEqual(6, sink.Examined.Count, "六行各一次");
            Assert.AreEqual(0, sink.PageTurns, "行级查体**不**翻页(翻页归页缘两行)");
            Assert.AreEqual(0, sink.Folds, "行级查体**不**折页");
            Assert.AreEqual(0, sink.Doses.Count, "行级查体**不**提交戥子档位");
            Assert.IsFalse(screen.IsOnPrescriptionPage, "查体后仍停在脉案页");

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 未注入写通道 ⇒ 不抛(null 安全,与其余 Submit 同款)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_noSinkBound_rowSubmit_doesNotThrow()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.ApplyData(new FakeSource());   // 刻意不 BindCommandSink

            Assert.DoesNotThrow(() => DispatchSubmit(root.Q<VisualElement>("channel-complexion")),
                "未接写通道 ⇒ 行级查体留空,不得抛(42 只渲染,装配缺失不在屏内炸)");

            yield return null;
        }
    }
}
