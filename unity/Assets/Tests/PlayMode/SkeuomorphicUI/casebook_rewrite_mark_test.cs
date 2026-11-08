// 39 · 改判留痕元件的**渲染判据**(2026-11-08 · S26-4 补件)
//
// 权威:
//   系统 39 `AC-39-12`「「旧」态灰度仍可辨(**非色单载**)」
//   design/gdd/casebook.md 规则六(改判留痕 · 痕不计分)
//   数据面判据 = `ICasebookRewriteMarkSource.HasRewriteMark`(EditMode
//   `casebook_rewrite_mark_test` 验其**生产实现**;本条验**屏怎么响应它**)
//
// ⚠️ 本文件用 FakeSource 是**正当的**:被测对象是 42 渲染,数据面本来就是注入的契约。
//    但 FakeSource **不实现** `ICasebookRewriteMarkSource` 的那一版会用来验「未提供 ⇒ 无痕」
//    —— 那是能力接口的**退化路径**,不是「测 Fake」。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>改判留痕元件:有痕 ⇒ 可见;无痕 / 源未实现该能力 ⇒ 不可见,且焦点环不变。</summary>
    internal sealed class casebook_rewrite_mark_test
    {
        private sealed class PlainSource : ICasebookDtoSource
        {
            public int ActiveSignCount => 0;
            public IReadOnlyList<SignLexemeRow> RevealedRows => new List<SignLexemeRow>();
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0L;
            public IReadOnlyList<int> SeenLexiconIds => new List<int>();
            public byte PendingConfidence => 0;
            public int DoseDetentCount => 0;
            public string TextOfLexicon(int lexiconId) => null;
        }

        private sealed class MarkingSource : ICasebookDtoSource, ICasebookRewriteMarkSource
        {
            public bool HasRewriteMark { get; set; }
            public int ActiveSignCount => 0;
            public IReadOnlyList<SignLexemeRow> RevealedRows => new List<SignLexemeRow>();
            public int PrecisionSlot => 0;
            public long InkAgeTicks => 0L;
            public IReadOnlyList<int> SeenLexiconIds => new List<int>();
            public byte PendingConfidence => 0;
            public int DoseDetentCount => 0;
            public string TextOfLexicon(int lexiconId) => null;
        }

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;

        private void SetUpPanel()
        {
            _go = new GameObject("CasebookRewriteMark_UnderTest");
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

        [UnityTest]
        public IEnumerator test_rewriteMarked_visible_thenCleared_hidden()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            var source = new MarkingSource { HasRewriteMark = false };

            // Arrange:先按「无痕」渲染一次(基线)
            screen.ApplyData(source);
            Assert.That(screen.RewriteMarkVisible, Is.False,
                "无痕时元件必须不可见(默认态;`Visibility.Hidden` 保布局不重排)");

            // Act 1:数据面翻到「有痕」
            source.HasRewriteMark = true;
            screen.ApplyData(source);
            Assert.That(screen.RewriteMarkVisible, Is.True,
                "改判后屏上必须出现留痕形状 —— 反证:`ApplyData` 里那段 visibility 切换删掉 ⇒ 本条红");

            // Act 2:翻回「无痕」(新起一笔 / 擦除 / 换病人)
            source.HasRewriteMark = false;
            screen.ApplyData(source);
            Assert.That(screen.RewriteMarkVisible, Is.False,
                "痕必须**跟着数据面走**(不是一次性置位)—— 反证:只置 true 不置 false ⇒ 本条红");
        }

        [UnityTest]
        public IEnumerator test_sourceWithoutCapability_noMark_noThrow()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());

            // 源**不**实现 `ICasebookRewriteMarkSource`(既有 7 个夹具全是这种)
            // ⇒ 能力接口的退化路径:**不崩、不造痕**。
            Assert.DoesNotThrow(() => screen.ApplyData(new PlainSource()),
                "未实现能力接口的源必须走「无痕」,不得抛(否则既有全部夹具连坐)");
            Assert.That(screen.RewriteMarkVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator test_rewriteMark_doesNotJoinFocusRing()
        {
            SetUpPanel();
            yield return WaitForRoot(_doc);
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");

            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.ApplyData(new MarkingSource { HasRewriteMark = true });

            // ① 元件确实建出来了(否则下面的「不可聚焦」是**空断言** —— 查无不存在的元素恒真)
            var mark = root.Query<VisualElement>(className: "rewrite-mark").First();
            Assert.IsNotNull(mark, "[夹具红] 找不到留痕元件 —— 版面被改坏,先修版面再谈判据");
            // 承载物必须是**形状**(对勾由两条边框画出),不是文字、也不是纯色块。
            // ⚠️ 2026-11-08 首版是 `Label("✓")` —— 被两条既有门各红一次
            // (`test_ac8_23_emptyRows_…` 禁右栏出额外文本 · `…all_labels_resolve_to_sdf` 禁 <24px 文本)
            // ⇒ 判据从「是 Label」改成「不是 Label」。
            Assert.That(mark, Is.Not.TypeOf<Label>(),
                "留痕不得用文字承载(右栏文本面受 AC-8-23 约束,字号受 24px 下限约束);用形状");
            Assert.That(mark.style.borderRightWidth.value, Is.GreaterThan(0f),
                "对勾须由边框笔画构成(形状承载 = 非色单载的前提)");

            // ② 它**不可聚焦**(铁律序的 K = 10 是入环元素数;装饰位不得占环)
            Assert.That(mark.focusable, Is.False,
                "留痕元件必须不可聚焦;可聚焦会让 K 从 10 变 11,铁律序被一个装饰位打乱");

            // ③ 全程可聚焦的**具名**元素仍是 10 个(与 `casebook_render_test` 的 K=10 同源口径)
            var focusable = new List<string>();
            foreach (var e in root.Query<VisualElement>().Build())
            {
                if (e.focusable && !string.IsNullOrEmpty(e.name)) focusable.Add(e.name);
            }
            Assert.That(focusable.Count, Is.EqualTo(10),
                $"入环元素数须仍为 10,实为 {focusable.Count}:{string.Join(",", focusable)}");
        }
    }
}
