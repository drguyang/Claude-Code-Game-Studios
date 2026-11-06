// 炮制屏(第九员 `Processing`)的判据 —— `AC-18-10b` 的**呈现半边**机器判(2026-11-02)
//   🔁 2026-11-06(S25-3):按 `design/ux/processing-18.md` 扩到 spec §13 的 12 条 `[A]`。
//
// ▍为什么要有这个文件
//   `AC-18-10b` 逐字:「GIVEN 不可发起的配方,WHEN 走查面板,THEN **不出现(非灰置)**」。
//   spec 成文后,本件同时承载它的**机器可判半边**与 spec 的屏级判据(二段 / 空态 / 焦点)。
//
//   ⚠️ **人眼那半边仍 NOT-RUN** —— `-nographics` 下没有像素。本件测的是**结构**
//   (行建不建 / 文案是什么 / 焦点在哪 / 二段态是什么),**不是**「看起来对不对」;
//   `processing-18.md` §13 的 4 条 `[L]` 走查**不在本件的判据面内**。
//
// ▍判据(逐条可证伪)
//   ① 不可发起 ⇒ 不建行(且**不是灰置**)· ② 可发起 ⇒ 建行,文案 = `RecipeId`
//   ③ 全不可发起 ⇒ 零行 + 无焦点落点 · ④ 二段:第一次零意图、第二次恰一次
//   ⑤ 准入查询为 null ⇒ `Bind` 抛(fail-closed)
//   ⑥ 三种空态的注互斥且不可聚焦 · ⑦ 开屏落焦第一行、重绑按 ordinal 还原(`TD-skeuo-21`)
//   ⑧ 点火被接受 ⇒ 留在屏内 + 候选清零 + 注「炉上有药,火未熄」· ⑨ 被拒 ⇒ 屏上零变化
//
// ▍本件的边界(先声明,防借绿)
//   · **不测点火真的落流** —— 出口是测试内注入的 spy sink,只证「意图到了」。
//   · **不测手柄焦点导航** —— 只测「行可获焦」与「焦点落在谁身上」(`focusController`)。
//   · **不测空态文案的美术呈现** —— 那是 §13 的 `[L]`。

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

            var admission = new FixedAdmission(skillLevel: 1, qualities: new[] { 1 });
            _screen.Bind(recipes, admission, actorId: 7, consumedQualities: new[] { 1 });
            yield return null;

            Assert.That(_screen.RowCount, Is.EqualTo(1),
                "① 红:三条配方里只有 `adm_recipe` 可发起 ⇒ 只建 1 行。" +
                "**不可发起的不建行**(AC-18-10b 逐字「不出现(非灰置)」)");

            Assert.That(_screen.RecipeOrdinalAt(0), Is.EqualTo(0),
                "① 唯一那行须是 `adm_recipe`(ordinal 0)");

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
        // ③ 全不可发起 ⇒ 零行 + 无焦点落点 + 空态注
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
            _screen.Bind(recipes, admission, actorId: 7,
                         consumedQualities: System.Array.Empty<int>(), furnace: new FixedFurnace(false));
            yield return null;

            Assert.That(_screen.RowCount, Is.Zero,
                "③ 空集品级 ⇒ 全部不可发起 ⇒ 零行(这是 `AdmissiblePredicate` 的合法输入,不是抛)");
            Assert.That(_screen.FirstFocusable, Is.Null,
                "③ 零行 ⇒ 无可落焦点 —— 空屏**不得**假装有落点(否则开屏时焦点落到不存在的东西上)");
            Assert.That(_screen.StatusNoteText, Is.EqualTo(Processing18Screen.NoteNoMaterial),
                "③ 零行 + 无料 ⇒ 注「案上无料」");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 二段确认:第一次零意图、第二次恰一次
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_twoStage_secondSubmit_emitsOrdinalOnce()
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

            _screen.Activate(1);   // 第一段:只进落火态
            Assert.That(sink.IgnitedOrdinals, Is.Empty,
                "④ 红:第一次 Submit **不得**点火(料在点火 tick 即真实消耗,OQ-18-3 裁「不可取消」)");
            Assert.That(_screen.ArmedRecipeOrdinal, Is.EqualTo(5), "④ 第一段后须处于落火态(ordinal 5)");

            _screen.Activate(1);   // 第二段:落火
            Assert.That(sink.IgnitedOrdinals, Is.EqualTo(new[] { 5 }),
                "④ 第二次 Submit 须发 ordinal 5 到出口,**恰一次**");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 二段态的行内文案(可辨,不靠颜色)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_twoStage_armedRowShowsSuffix()
        {
            yield return SetUp();

            var recipes = new List<Recipe> { RecipeOf(0, "herb_soup", 1, 1) };
            var admission = new FixedAdmission(5, new[] { 3 });
            _screen.BindCommandSink(new SpySink());
            _screen.Bind(recipes, admission, 7, new[] { 3 });
            yield return null;

            VisualElement row = _screen.RowAt(0);
            string before = row.Q<Label>().text;
            _screen.Activate(0);

            Assert.That(row.Q<Label>().text, Is.EqualTo(before + Processing18Screen.ArmedSuffix),
                "⑤ 落火态须**行内可见**(文案变化是主通道;非颜色单通道,UI-18.2 / P-06)");
            Assert.That(row.Q<Label>().text.Contains("落火"), Is.True);
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 二段态:焦点移开即撤销(安全侧)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_twoStage_focusMove_disarms()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(0, "r0", 1, 1),
                RecipeOf(1, "r1", 1, 1),
            };
            var admission = new FixedAdmission(5, new[] { 3 });
            var sink = new SpySink();
            _screen.BindCommandSink(sink);
            _screen.Bind(recipes, admission, 7, new[] { 3 });
            yield return null;

            _screen.Activate(0);
            Assert.That(_screen.ArmedRowIndex, Is.EqualTo(0), "⑥ 前置:第 0 行处于落火态");

            _screen.RowAt(1).Focus();
            yield return null;

            Assert.That(_screen.ArmedRowIndex, Is.EqualTo(-1),
                "⑥ 焦点离开该行 ⇒ 二段态撤销(方向键 / 指针移开都走这条)");
            Assert.That(sink.IgnitedOrdinals, Is.Empty, "⑥ 撤销过程零意图");
            Assert.That(_screen.RowAt(0).Q<Label>().text, Is.EqualTo("r0"),
                "⑥ 行文案须回落火前形态");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑦ 点火被接受 ⇒ 留在屏内 + 候选清零 + 状态注
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_fired_staysInScreen_zeroRows_busyNote()
        {
            yield return SetUp();

            var recipes = new List<Recipe> { RecipeOf(0, "r0", 1, 1) };
            var admission = new FixedAdmission(5, new[] { 3 });
            _screen.BindCommandSink(new SpySink { Accept = true });
            _screen.Bind(recipes, admission, 7, new[] { 3 });
            yield return null;

            _screen.Activate(0);
            _screen.Activate(0);
            yield return null;

            Assert.That(_screen.RowCount, Is.Zero,
                "⑦ 落火后候选清零(用户 2026-11-06 裁定:留在屏内,不关屏)");
            Assert.That(_screen.StatusNoteText, Is.EqualTo(Processing18Screen.NoteFurnaceBusy),
                "⑦ 落火后注须为「炉上有药,火未熄」");
            Assert.That(_screen.FirstFocusable, Is.Null,
                "⑦ 清零后无落点 —— 不得悬空 Focus 到已移除的行");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑧ 被拒 ⇒ 屏上零变化
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_rejected_leavesScreenUnchanged()
        {
            yield return SetUp();

            var recipes = new List<Recipe> { RecipeOf(0, "r0", 1, 1) };
            var admission = new FixedAdmission(5, new[] { 3 });
            int before = -1;
            _screen.BindCommandSink(new SpySink { Accept = false });
            _screen.Bind(recipes, admission, 7, new[] { 3 });
            yield return null;
            before = _screen.RowCount;

            _screen.Activate(0);
            _screen.Activate(0);
            yield return null;

            Assert.That(_screen.RowCount, Is.EqualTo(before),
                "⑧ 被拒 ⇒ 行数不变(拒绝的可感面在世界侧,UI-18.2)");
            Assert.That(_screen.StatusNote, Is.Null, "⑧ 被拒 ⇒ 不出状态注(零弹窗 / 零文字报错)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑨ 三种空态的措辞(互斥且全覆盖)+ 不可聚焦
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_emptyStates_threeNotes()
        {
            yield return SetUp();

            var blocked = new List<Recipe> { RecipeOf(0, "r0", 9, 1) };
            var admission = new FixedAdmission(1, new[] { 3 });

            // 有料、无方
            _screen.Bind(blocked, admission, 7, new[] { 3 }, new FixedFurnace(false));
            yield return null;
            Assert.That(_screen.StatusNoteText, Is.EqualTo(Processing18Screen.NoteNoRecipe),
                "⑨a 有料但无方 ⇒ 「无方可起」");

            // 炉忙优先于「无料」
            _screen.Bind(blocked, admission, 7, System.Array.Empty<int>(), new FixedFurnace(true));
            yield return null;
            Assert.That(_screen.StatusNoteText, Is.EqualTo(Processing18Screen.NoteFurnaceBusy),
                "⑨b 炉忙优先于无料(§6.1 的优先序:炉忙 > 无料 > 无方)");

            // 无料
            _screen.Bind(blocked, admission, 7, System.Array.Empty<int>(), new FixedFurnace(false));
            yield return null;
            Assert.That(_screen.StatusNoteText, Is.EqualTo(Processing18Screen.NoteNoMaterial),
                "⑨c 无料 ⇒ 「案上无料」");

            Assert.That(_screen.StatusNote, Is.Not.Null);
            Assert.That(_screen.StatusNote.focusable, Is.False,
                "⑨d 状态注**不可聚焦** —— 它不陈述系统、也不是落点");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑩ 开屏落焦第一行(TD-skeuo-21)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_openFocus_firstRow()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(0, "r0", 1, 1),
                RecipeOf(1, "r1", 1, 1),
            };
            _screen.Bind(recipes, new FixedAdmission(5, new[] { 3 }), 7, new[] { 3 });
            yield return null;

            VisualElement first = _screen.FirstFocusable;
            Assert.That(first, Is.Not.Null, "⑩ 前置:有两行");
            Assert.That(first.panel, Is.Not.Null);

            Focusable focused = first.panel.focusController?.focusedElement;
            Assert.That(focused, Is.SameAs(first),
                "⑩ 红:开屏后**焦点须在第一行** —— 否则键盘 / 手柄的第一次 Submit 落空" +
                "(TD-skeuo-21;spec §7「开屏落焦」)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑪ 重绑按 ordinal 保持焦点(不按元素引用)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_rebind_keepsFocusByOrdinal()
        {
            yield return SetUp();

            var recipes = new List<Recipe>
            {
                RecipeOf(0, "r0", 1, 1),
                RecipeOf(1, "r1", 1, 1),
            };
            var admission = new FixedAdmission(5, new[] { 3 });
            _screen.Bind(recipes, admission, 7, new[] { 3 });
            yield return null;

            _screen.RowAt(1).Focus();
            yield return null;
            Assert.That(_screen.RowAt(1).panel.focusController.focusedElement, Is.SameAs(_screen.RowAt(1)),
                "⑪ 前置:焦点在第 2 行");

            _screen.Bind(recipes, admission, 7, new[] { 3 });   // 重建
            yield return null;

            Assert.That(_screen.RowAt(1).panel.focusController.focusedElement, Is.SameAs(_screen.RowAt(1)),
                "⑪ 重建后焦点须回到**同一 ordinal** 的新行(不回到第一行,也不留在已脱离 panel 的旧元素)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑫ spec 自有文案零数字(UI-18.2 的本屏执行体)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_spec_screenLiterals_haveNoDigits()
        {
            yield return SetUp();

            // 扫的是**屏自己的字面量**(状态注三句 + 二段后缀);方名由数据提供,另一条口径。
            string[] literals =
            {
                Processing18Screen.NoteFurnaceBusy,
                Processing18Screen.NoteNoMaterial,
                Processing18Screen.NoteNoRecipe,
                Processing18Screen.ArmedSuffix,
            };
            foreach (string text in literals)
            {
                foreach (char c in text)
                {
                    Assert.That(char.IsDigit(c) || c == '%', Is.False,
                        $"⑫ 红:屏自有文案「{text}」含数字 / 百分号 —— UI-18.2 零数字铁律");
                }
                Assert.That(text.Contains("秒") || text.Contains("还剩"), Is.False,
                    $"⑫ 红:屏自有文案「{text}」含倒计时词形 —— 禁倒计时(AC-44-09 同源)");
            }
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑬ 准入查询为 null ⇒ Bind 抛(fail-closed)
        // ══════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_ac1810b_bindWithoutAdmission_throws()
        {
            yield return SetUp();
            var recipes = new List<Recipe> { RecipeOf(0, "r0", 1, 1) };

            Assert.Throws<System.ArgumentNullException>(
                () => _screen.Bind(recipes, null, actorId: 7,
                                   consumedQualities: System.Array.Empty<int>()),
                "⑬ 准入查询为 null ⇒ 须抛。静默接受 = 把「全部配方都列出来」当可用" +
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

        /// <summary>按 `AdmissiblePredicate` 的两道门做准入(测试替身,**不是**第二实现)。</summary>
        /// <remarks>
        /// ⚠️ 替身让「屏问不问准入」与「准入怎么判」两件事可分离 —— 若直调生产谓词,
        /// 变异「屏自己合取」时两边一起动 ⇒ 零判别力(自指判据,项目已抓过)。
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

        /// <summary>单炉查询替身(只喂空态选词,不参与准入 —— 屏不自己合取)。</summary>
        private sealed class FixedFurnace : ISingleFurnaceQuery
        {
            private readonly bool _busy;
            public FixedFurnace(bool busy) => _busy = busy;
            public bool IsBusy(int actorId) => _busy;
        }

        private sealed class SpySink : IProcessingCommandSink
        {
            public readonly List<int> IgnitedOrdinals = new List<int>();
            public bool Accept = true;
            public int CloseCount;

            public bool Ignite(int recipeOrdinal)
            {
                IgnitedOrdinals.Add(recipeOrdinal);
                return Accept;
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
