// 炮制屏(第九员 `Processing`)的判据 —— `AC-18-10b` 的**呈现半边**机器判(2026-11-02)
//
// ▍为什么要有这个文件
//   `AC-18-10b` 逐字:「GIVEN 不可发起的配方,WHEN 走查面板,THEN **不出现(非灰置)**」。
//   它此前记 `EXTERNAL · BLOCKED-BY-42`(载体 = 42 屏清单未认领炮制交互,`O-18-R6`)。
//   本轮立了第九员 + 屏 + 准入接缝 ⇒ 该 AC 的**机器可判半边**首次有对象。
//
//   ⚠️ **人耳/人眼那半边仍 NOT-RUN** —— `-nographics` 下没有像素(项目已两次实测:
//   `ScreenCapture` 在 batch 抛异常 / UI Toolkit 纹理不进图集)。本件测的是**结构**
//   (行建不建 / 文案是什么 / 焦点在哪),**不是**「看起来对不对」。
//   `design/ux/processing-18.md` 尚不存在 ⇒ 视觉走查按 `AC-42-F1` 的纪律仍阻塞签核。
//
// ▍判据(逐条可证伪)
//   ① **不可发起 ⇒ 不建行**(`AC-18-10b` 的主判据)。且**不是灰置** ——
//      树上根本没有那个元素,不存在「存在但禁用」这个可表达状态。
//   ② **可发起 ⇒ 建行**,且文案 = 配方的 `RecipeId`(21a 表无 `DisplayName` —— 已知粗糙边)。
//   ③ **全不可发起 ⇒ 零行 + `FirstFocusable == null`**(空屏不假装有焦点落点)。
//   ④ **选中 ⇒ 意图出口收到该配方的 `Ordinal`**(42 只发意图,不判定 —— ADR-013 §9 C3)。
//   ⑤ **准入查询为 null ⇒ `Bind` 抛**(fail-closed:拒绝把「全部配方都列出来」当可用)。
//
// ▍本件的边界(先声明,防借绿)
//   · **不测点火真的落流** —— 那要 20 的 `Apply`(归 S20-14)+ 18 的五步事务接线轮。
//     本件的出口是**测试内注入的 spy sink**,只证「意图到了」,不证「事务跑通」。
//   · **不测料位** —— 品级集今日传空集(BootRoot 侧同款),料位选中态归 20 / 4 的交互轮。
//   · **不测手柄焦点导航** —— 那要真 `EventSystem` + 虚拟 Gamepad(照 `pause_focus_navigation_test`
//     的配方);本件只测「行可获焦(`focusable` 位)」与「`FirstFocusable` 指向首行」。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>炮制屏(第九员)的结构判据。</summary>
    internal sealed class processing18_screen_test
    {
        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;
        private Processing18Screen _screen;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null; _screen = null;
        }

        private IEnumerator SetUp()
        {
            _go = new GameObject("Processing18_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > 3f)
                {
                    Assert.Fail("[夹具红] rootVisualElement / panel 3s 未就绪");
                    yield break;
                }
                yield return null;
            }
            yield return null;

            var library = new SkeuoElementLibrary();
            _screen = new Processing18Screen(_doc.rootVisualElement, library);
        }

        // ══════════════════════════════════════════════════════════════════
        // ① AC-18-10b 主判据:不可发起 ⇒ 不建行(且非灰置)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac1810b_inadmissibleRecipe_hasNoRow()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(ordinal: 0, id: "adm_recipe",  skillGate: 1, minQuality: 1),
                RecipeOf(ordinal: 1, id: "blocked",    skillGate: 9, minQuality: 1),  // 技能门过不去
                RecipeOf(ordinal: 2, id: "lowqual",    skillGate: 1, minQuality: 9),  // 品质门过不去
            };

            // 准入查询 = 真实的 `AdmissiblePredicate` 语义(技能 1 级 / 品级 1)
            var admission = new FixedAdmission(skillLevel: 1, qualities: new[] { 1 });
            _screen.Bind(recipes, admission, actorId: 7, consumedQualities: new[] { 1 });
            yield return null;

            Assert.That(_screen.RowCount, Is.EqualTo(1),
                "① 红:三条配方里只有 `adm_recipe` 可发起 ⇒ 只建 1 行。" +
                "**不可发起的不建行**(AC-18-10b 逐字「不出现(非灰置)」)");

            Assert.That(_screen.RecipeOrdinalAt(0), Is.EqualTo(0),
                "① 唯一那行须是 `adm_recipe`(ordinal 0)");

            // **「非灰置」的机器判**:树上不得存在「存在但禁用」的中间态。
            // 遍历整棵树,凡 `name` 含 `processing-recipe-` 的元素都必须 `enabledSelf == true`
            // 且不是 `disabled` 伪类 —— 否则就是灰置形态。
            int recipeElements = 0;
            foreach (var el in Walk(_doc.rootVisualElement))
            {
                if (!el.name.StartsWith("processing-recipe-")) continue;
                recipeElements++;
                Assert.That(el.enabledSelf, Is.True,
                    $"① 红:`{el.name}` 存在但被禁用 —— 那是**灰置**,AC-18-10b 明令禁止");
                Assert.That(el.ClassListContains("disabled"), Is.False,
                    $"① 红:`{el.name}` 带 `disabled` 类 —— 灰置形态");
            }
            Assert.That(recipeElements, Is.EqualTo(1),
                "① 树上 `processing-recipe-*` 元素总数须恰 = 1(不是 3 个里禁用 2 个)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 可发起 ⇒ 建行,文案 = RecipeId
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac1810b_admissibleRecipe_rowShowsRecipeId()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(ordinal: 0, id: "herb_soup", skillGate: 1, minQuality: 1),
            };
            var admission = new FixedAdmission(skillLevel: 5, qualities: new[] { 3 });
            _screen.Bind(recipes, admission, actorId: 7, consumedQualities: new[] { 3 });
            yield return null;

            Assert.That(_screen.RowCount, Is.EqualTo(1), "② 前置:可发起 ⇒ 建 1 行");

            VisualElement row = _screen.RowAt(0);
            Assert.That(row, Is.Not.Null);
            Assert.That(row.focusable, Is.True,
                "② 行须可获焦(拟物 UI 无指针 ⇒ 手柄焦点序是唯一导航路径,TR-concept-008)");

            var label = row.Q<Label>();
            Assert.That(label, Is.Not.Null, "② 行须有文字(空元素 = 玩家什么都看不见)");
            Assert.That(label.text, Is.EqualTo("herb_soup"),
                "② 文案 = 配方的 `RecipeId`。⚠️ 21a 表**无** `DisplayName` ⇒ 显示的是键名不是「麻黄汤」" +
                "(已知粗糙边,归 21a 显示名轮 / 42 文案轮)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 全不可发起 ⇒ 零行 + 无焦点落点
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac1810b_allInadmissible_zeroRows_noFocusTarget()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(ordinal: 0, id: "r0", skillGate: 9, minQuality: 9),
                RecipeOf(ordinal: 1, id: "r1", skillGate: 9, minQuality: 9),
            };
            // 空集品级 ⇒ `AdmissiblePredicate` 的 `NoConsumedInstances` ⇒ 全部不可发起
            var admission = new FixedAdmission(skillLevel: 1, qualities: System.Array.Empty<int>());
            _screen.Bind(recipes, admission, actorId: 7, consumedQualities: System.Array.Empty<int>());
            yield return null;

            Assert.That(_screen.RowCount, Is.Zero,
                "③ 空集品级 ⇒ 全部不可发起 ⇒ 零行(这是 `AdmissiblePredicate` 的合法输入,不是抛)");
            Assert.That(_screen.FirstFocusable, Is.Null,
                "③ 零行 ⇒ 无可落焦点 —— 空屏**不得**假装有落点(否则开屏时焦点落到不存在的东西上)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 选中 ⇒ 意图出口收到 Ordinal
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac1810b_activate_emitsOrdinalToSink()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(ordinal: 3, id: "r3", skillGate: 1, minQuality: 1),
                RecipeOf(ordinal: 5, id: "r5", skillGate: 1, minQuality: 1),
            };
            var admission = new FixedAdmission(skillLevel: 5, qualities: new[] { 3 });
            var sink = new SpySink();
            _screen.BindCommandSink(sink);
            _screen.Bind(recipes, admission, actorId: 7, consumedQualities: new[] { 3 });
            yield return null;

            Assert.That(_screen.RowCount, Is.EqualTo(2), "④ 前置:两条都可发起 ⇒ 2 行");

            _screen.Activate(1);   // 第二行 = ordinal 5
            Assert.That(sink.IgnitedOrdinals, Is.EqualTo(new[] { 5 }),
                "④ 确认第二行须发 ordinal 5 到出口(42 只发意图,不判定 —— ADR-013 §9 C3)");
            Assert.That(_screen.SelectedRecipeOrdinal, Is.EqualTo(5), "④ 选中态须记下");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 准入查询为 null ⇒ Bind 抛(fail-closed)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac1810b_bindWithoutAdmission_throws()
        {
            yield return SetUp();
            var recipes = new List<Recipe> { RecipeOf(0, "r0", 1, 1) };

            Assert.Throws<System.ArgumentNullException>(
                () => _screen.Bind(recipes, null, actorId: 7,
                                   consumedQualities: System.Array.Empty<int>()),
                "⑤ 准入查询为 null ⇒ 须抛。静默接受 = 把「全部配方都列出来」当可用" +
                "(那正是 AC-18-10b 的反面)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 辅助
        // ══════════════════════════════════════════════════════════════════

        private static Recipe RecipeOf(int ordinal, string id, int skillGate, int minQuality)
            => new Recipe
            {
                Ordinal = ordinal,
                RecipeId = id,
                Owner = RecipeOwner.Process,
                SkillGate = skillGate,
                MinQuality = minQuality,
                DurationTicks = 10,
                Inputs = new[] { new RecipeEntry(new ItemKey("herb_a", ProcessingState.Raw), 1) },
                Outputs = new[] { new RecipeEntry(new ItemKey("herb_a", ProcessingState.Dried), 1) },
                BoundaryState = System.Array.Empty<ProcessingTransition>(),
            };

        /// <summary>按 `AdmissiblePredicate` 的两道门做准入(测试替身,**不是**第二实现 —— 见下)。</summary>
        /// <remarks>
        /// ⚠️ 这是**测试替身**,不是生产判据的拷贝。它的存在理由是:
        /// `AdmissiblePredicate` 住 `Sim`,而 `PlayMode.asmdef` 引 `Sim` ⇒ 本可以直调。
        /// **但**本件要测的是**屏**的过滤行为,若直调生产谓词,变异「屏自己合取」时
        /// 两边一起动 ⇒ 零判别力(自指判据,项目已抓过)。⇒ 替身让「屏问不问准入」
        /// 与「准入怎么判」两件事可分离。
        /// </remarks>
        private sealed class FixedAdmission : IProcessingAdmissionQuery
        {
            private readonly int _skillLevel;
            private readonly IReadOnlyList<int> _qualities;

            public FixedAdmission(int skillLevel, IReadOnlyList<int> qualities)
            {
                _skillLevel = skillLevel; _qualities = qualities;
            }

            public bool IsAdmissible(in Recipe recipe, int actorId,
                                     IReadOnlyList<int> consumedQualities)
            {
                if (_skillLevel < recipe.SkillGate) return false;
                if (_qualities.Count == 0) return false;
                int min = int.MaxValue;
                for (int i = 0; i < _qualities.Count; i++)
                    if (_qualities[i] < min) min = _qualities[i];
                return min >= recipe.MinQuality;
            }
        }

        private sealed class SpySink : IProcessingCommandSink
        {
            public readonly List<int> IgnitedOrdinals = new List<int>();
            public int CloseCount;

            public bool Ignite(int recipeOrdinal)
            {
                IgnitedOrdinals.Add(recipeOrdinal);
                return true;   // 本轮不测「被拒」—— 那要 20 的 Apply(S20-14)
            }

            public void Close() => CloseCount++;
        }

        private static IEnumerable<VisualElement> Walk(VisualElement root)
        {
            if (root == null) yield break;
            yield return root;
            for (int i = 0; i < root.childCount; i++)
                foreach (var d in Walk(root[i])) yield return d;
        }
    }
}
