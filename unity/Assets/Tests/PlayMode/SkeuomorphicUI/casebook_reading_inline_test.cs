// 值区横排回归门(PlayMode · 2026-10-14 · 布局乙案)
//
// ▍病因(探针实测 `unity/Logs/layout_probe.log`):通道行原为默认**竖排**,
//   查体后值区词条逐个往下堆 ⇒ 每词 +59px,六行案面 +352px ⇒ 总自然高超出面板
//   ⇒ flex 默认 `flexShrink=1` 把尾部功能行(前案 / 后案 / 急救候选)压扁。
//   用户真机:「前案后案被挤在问诊和止血之间」。
//
// ▍修法(用户裁定「按乙」):通道行改**横排折行**(`flexDirection=Row` +
//   `flexWrap=Wrap`),题签居左、词条向右并列 —— 判据权威 =
//   diagnosis-system Edge Cases「同一通道多条体征**全部并列显示**」+
//   UI-8.1 ASCII「面色 │ ____」题签|值区同行。
//
// ▍回归门(可证伪):
//   ① 通道行内多词条**并列不增高**:一行放得下时,4 词条行高 ≈ 1 词条行高;
//   ② **尾部功能行不被压**:出词后 `page-edge-prev` 高 ≈ 空行态高(不缩水)。
//   删 `flexDirection=Row` ⇒ ①② 双红(词条回退竖排堆叠)。
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
    internal sealed class casebook_reading_inline_test
    {
        private sealed class FakeSource : ICasebookDtoSource
        {
            public IReadOnlyList<SignLexemeRow> Rows = new List<SignLexemeRow>();
            public int ActiveSignCount => Rows.Count;
            public IReadOnlyList<SignLexemeRow> RevealedRows => Rows;
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0L;
            public IReadOnlyList<int> SeenLexiconIds => new List<int>();
            public byte PendingConfidence => 0;
            public int DoseDetentCount => 0;
            public string TextOfLexicon(int lexiconId) => null;
        }

        private static readonly string[] UssPaths =
        {
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoMarks.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoImplement.uss",
        };

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _prodPs;

        [SetUp]
        public void SetUpPanel()
        {
            _go = new GameObject("ReadingInlineTest");
            _doc = _go.AddComponent<UIDocument>();
            _prodPs = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
            _doc.panelSettings = _prodPs;
        }

        [TearDown]
        public void TearDownPanel()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _go = null; _prodPs = null;
        }

        private IEnumerator WaitForRoot()
        {
            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 3f)
                {
                    Assert.Fail("[夹具红] root 未就绪");
                    yield break;
                }
                yield return null;
            }
        }

        private static SignLexemeRow Row(string id, string word, SignChannel ch) =>
            new SignLexemeRow(id, new[] { word }, ch,
                new[] { RevealMethod.Inspection }, 1, SignPolarity.Positive, null);

        [UnityTest]
        public IEnumerator test_reading_inlineFlow_and_pageEdge_notSqueezed()
        {
            yield return WaitForRoot();
            var root = _doc.rootVisualElement;
            foreach (var p in UssPaths)
                root.styleSheets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(p));
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(null);

            yield return null;
            yield return null;

            // 空行态:记 page-edge-prev 高
            var prev = root.Q<VisualElement>("page-edge-prev");
            Assert.IsNotNull(prev, "页缘「前案」应存在");
            float prevEmptyH = prev.worldBound.height;

            // 出词态:六通道各 2 词条(近真机密度)
            var rows = new List<SignLexemeRow>
            {
                Row("s1", "面色㿠白", SignChannel.FaceColor),
                Row("s2", "面色晦暗", SignChannel.FaceColor),
                Row("s3", "声音低微", SignChannel.Voice),
                Row("s4", "语声断续", SignChannel.Voice),
                Row("s5", "倚卧难立", SignChannel.Posture),
                Row("s6", "步态蹒跚", SignChannel.Posture),
                Row("s7", "呼吸浅促", SignChannel.Breathing),
                Row("s8", "喉中痰鸣", SignChannel.Breathing),
                Row("s9", "四肢厥冷", SignChannel.Touch),
                Row("s10", "皮肤湿润", SignChannel.Touch),
                Row("s11", "寒战畏寒", SignChannel.History),
                Row("s12", "口中和", SignChannel.History),
            };
            screen.ApplyData(new FakeSource { Rows = rows });

            yield return null;
            yield return null;

            // ① 通道行并列不增高:面色行 2 词条,高应 ≈ 空行态高(不随词条数线性膨胀)。
            //    竖排回退时每行 +59px ⇒ 明显高于空行态 ⇒ 红。
            var complexion = root.Q<VisualElement>("channel-complexion");
            float filledH = complexion.worldBound.height;
            // 横排:2 词条同行,行高 ≈ 单行;留 20% 容差(底衬 border + padding 抖动)。
            Assert.Less(filledH, prevEmptyH * 1.25f,
                $"出词后通道行高 {filledH:F1} 应 ≈ 空行行高 {prevEmptyH:F1}(横排列内并列;" +
                $"若 ≫ 说明词条仍在竖排堆叠 —— 回退 flex 方向即红)。");

            // ② 尾部功能行不被压:page-edge-prev 高不应缩水(竖排回退时会被压到 ~68%)。
            float prevFilledH = prev.worldBound.height;
            Assert.Greater(prevFilledH, prevEmptyH * 0.9f,
                $"出词后「前案」行高 {prevFilledH:F1} 不应低于空行态 {prevEmptyH:F1} 的 90%" +
                $"(被压 = 案面竖排膨胀挤占;横排修后不再溢出)。");

            Object.Destroy(_doc);
            Object.Destroy(_go);
            yield break;
        }
    }
}
