// story-025 戥子剂量读取形态元素 —— AC-025-1…6 夹具(EditMode · 2026-10-18)
//
// 权威来源:
//   · production/epics/skeuomorphic-ui/story-025-dentch-dose-form-element.md
//   · design/gdd/skeuomorphic-ui.md §7.9 戥子档位读数元件(黄铜侧)
//   · ADR-013 §十-B(戥子档位读数元件 = 黄铜侧)· AC-42-F3 材质分支
//
// 判据面(逐条可证伪):
//   AC-025-1  SetDetents 逐档形态 + 落焦档**几何可分**(非颜色)+ 定义域 fail-loud
//   AC-025-2  元件不持剂量状态 —— 无持久字段;同入参重复调用 ⇒ 同形态(幂等)
//   AC-025-3  SkeuoBrass.uss 逐档形态类(值全走 --skeuo-brass-* 变量;零裸色/零裸尺寸/零数字文本)
//   AC-025-4  RefreshDentchDoses 每行携带**该行自己的**档位形态(行序 = 档位序,提交链不变)
//   AC-025-5  零数字铁律(行内无十进制数字 · 元件零 string 数字读数 API · USS 零 content 数字)
//   AC-025-6  黄铜分支归属(新形态类全落 brass,墨侧不得误判)
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 夹具自持
//    (唯一文件读 = 生产 USS 文本 —— 与生产门 `SkeuomorphicUiGates` 同口径,非外部状态)。
// ⚠️ 本目录自有 asmdef `SkeuomorphicUI.Tests`(引用 Gameplay.UI)⇒ 可直接 new 42 侧类型,
//    不走反射。(`Sim.Contracts.Tests` 不引 Gameplay.UI,42 屏的**接线**判据仍住 PlayMode。)

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using DaYiJingCheng.EditorTools.Gates;
    using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Brass;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
    using NUnit.Framework;
    using UnityEngine.UIElements;

    /// <summary>戥子剂量读取形态元素夹具(story-025 · AC-025-1…6)。</summary>
    [TestFixture]
    public class dentch_dose_form_test
    {
        private const string DetentClass = "brass-detent";
        private const string FocusedClass = "brass-detent--focused";
        private const string BandClass = "brass-detent-band";

        /// <summary>主题表「黄铜层」注释块标记(全表恰一次 —— 2026-10-18 实测 `grep -c` = 1)。</summary>
        private const string BrassLayerMarker = "黄铜(Brass)";

        /// <summary>主题表「器具档」注释块标记 —— 黄铜层的**下界**(全表恰一次)。</summary>
        private const string ImplementLayerMarker = "器具档(Implement)";

        /// <summary>主题表「黄铜逐档形态」子块标记(在黄铜层区间内;全表恰一次)。</summary>
        private const string DetentBlockMarker = "黄铜逐档形态";

        // ══════════════ 基础设施 ══════════════

        private static string SkeuoDir => Path.Combine(
            TextureBindingGates.DefaultRepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

        private static string BrassUssPath => Path.Combine(SkeuoDir, "SkeuoBrass.uss");

        private static string ThemeUssPath => Path.Combine(SkeuoDir, "SkeuoThemeVariables.uss");

        /// <summary>剥注释后的 USS 文本(与生产门 `SkeuomorphicUiGates.UssCommentRegex` 同口径 ——
        /// 夹具比门松才可能假绿,比门严则假红,故必须同口径)。</summary>
        private static string BrassUssWithoutComments()
        {
            string raw = File.ReadAllText(BrassUssPath);
            return Regex.Replace(raw, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        }

        /// <summary>取某选择器规则的声明体(不含花括号)。选择器须逐字匹配
        /// (`".brass-detent {"` 不会命中 `.brass-detent-band {`)。</summary>
        private static string RuleBody(string ussText, string selector)
        {
            int at = ussText.IndexOf(selector + " {", StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"USS 应含规则 `{selector}`(AC-025-3)。");

            int open = ussText.IndexOf('{', at);
            int close = ussText.IndexOf('}', open);
            Assert.Greater(close, open, $"规则 `{selector}` 的花括号不闭合。");
            return ussText.Substring(open + 1, close - open - 1);
        }

        /// <summary>取声明体内某属性的字面值(按行首逐字匹配 —— `width:` 不会命中
        /// `border-top-width:`;`height:` 不会命中 `min-height:`)。未声明返回 null。</summary>
        private static string Declared(string body, string property)
        {
            foreach (string raw in body.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(property + ":", StringComparison.Ordinal))
                    return line.Substring(property.Length + 1).Trim().TrimEnd(';').Trim();
            }

            return null;
        }

        /// <summary>主题表某层的行区间 <c>[start, end)</c>(0 基;按层注释块定界)。
        /// <c>start</c> = 命中 <paramref name="startMarker"/> 的那一行,<c>end</c> = 命中
        /// <paramref name="endMarker"/> 的那一行(不含)。</summary>
        /// <remarks>⚠️ 2026-10-18 评审 R-6:层归属**必须**按行区间判定。
        /// 旧夹具用 `theme.Contains(varName + ":")` —— 那只证明该名在**整个文件里某处**
        /// 带冒号出现,与它落在哪个层注释块**无关**;变量名带 `--skeuo-brass-` 前缀
        /// 是命名约定,不是层归属证据(落 ink 层同样通过)。</remarks>
        private static (int start, int end) LayerRange(string themeText, string startMarker, string endMarker)
        {
            string[] lines = themeText.Split('\n');
            int start = Array.FindIndex(lines, l => l.Contains(startMarker, StringComparison.Ordinal));
            int end = Array.FindIndex(lines, l => l.Contains(endMarker, StringComparison.Ordinal));
            Assert.GreaterOrEqual(start, 0, $"主题表应含层注释块「{startMarker}」");
            Assert.Greater(end, start, $"主题表应含层注释块「{endMarker}」且位于「{startMarker}」之后");
            return (start, end);
        }

        /// <summary>断言某变量**声明在**指定层的行区间内(而非仅在文件里某处出现)。</summary>
        private static void AssertDeclaredInLayer(
            string themeText, string varName, string startMarker, string endMarker)
        {
            var (start, end) = LayerRange(themeText, startMarker, endMarker);
            string[] lines = themeText.Split('\n');
            for (int i = start; i < end; i++)
            {
                if (lines[i].Contains(varName + ":", StringComparison.Ordinal)) return;
            }

            Assert.Fail(
                $"变量 {varName} 须**声明在**层「{startMarker}」区间内(行 {start + 1}..{end})—— " +
                "全文件 `Contains` 不能证明层归属(评审 R-6)");
        }

        private static BrassScaleElement Loaded(int count, int focusedOrdinal)
        {
            var element = new BrassScaleElement();
            element.SetDetents(count, focusedOrdinal);
            return element;
        }

        /// <summary>取形态容器(按类名查,不依赖任何 test-only 生产 API)。</summary>
        private static VisualElement Band(BrassScaleElement element)
            => element.Q<VisualElement>(className: BandClass);

        // ══════════════ 合成 fixture(42 侧只读源 / 写通道;零 sim 依赖)══════════════

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
            public void SubmitDiseaseName(int lexiconId) { }
            public void SubmitConfidence() { }
            public void SubmitDose(int doseOrdinal) => Doses.Add(doseOrdinal);
            public void SubmitPageTurn() { }
            public void SubmitFold() { }
            public void SubmitEmergencySkip() { }
        }

        private static (CasebookScreen screen, VisualElement root) BuildOnDosePage(int detents)
        {
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = detents });
            screen.SubmitPageNext();   // → 方笺页 ⇒ 重建档位行
            return (screen, root);
        }

        /// <summary>收集档位行(方笺页内 `userData` 为 int 的元素)—— 与 PlayMode
        /// `casebook_page_turn_test.CollectDoseRows` 同判据。</summary>
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

        // ══════════════ AC-025-1 逐档形态 + 几何可分 ══════════════

        [Test]
        public void test_ac025_1_setDetents_createsOneFormPerDetent()
        {
            var element = Loaded(5, 3);

            Assert.AreEqual(5, element.DetentCount,
                "五档 ⇒ 五个形态子件(AC-025-1 ①)");
            Assert.AreEqual(5, Band(element).childCount,
                "形态子件须住在形态容器内");
        }

        [Test]
        public void test_ac025_1_setDetents_exactlyOneDetentIsFocused()
        {
            var element = Loaded(5, 3);

            int focused = Band(element).Children()
                .Count(c => c.ClassListContains(FocusedClass));

            Assert.AreEqual(1, focused, "恰一档落焦 —— 零档或多档都是形态歧义");
            Assert.AreEqual(3, element.FocusedDetentOrdinal, "落焦档 = 入参指定的那一档");
        }

        [Test]
        public void test_ac025_1_focusedForm_geometricallyDistinctFromNormalForm()
        {
            // AC-025-1 ②:落焦档与其余档**几何可分**(高度 / 边距 / 边框宽的整数差异,**非颜色**)。
            string uss = BrassUssWithoutComments();
            string normal = RuleBody(uss, "." + DetentClass);
            string focused = RuleBody(uss, "." + FocusedClass);

            string[] geometricAxes = { "width", "height", "margin-top", "border-top-width" };
            int differing = geometricAxes.Count(axis =>
            {
                string a = Declared(normal, axis);
                string b = Declared(focused, axis);
                return b != null && a != b;
            });

            Assert.GreaterOrEqual(differing, 3,
                "落焦档须在 ≥3 条几何轴上与常态档取值不同(AC-025-1 ② 的「几何可分」)");

            foreach (string axis in geometricAxes)
            {
                string b = Declared(focused, axis);
                if (b != null)
                {
                    Assert.AreNotEqual(Declared(normal, axis), b,
                        $"落焦档若声明了 `{axis}` 就须与常态档不同 —— 同值声明是空声明");
                }
            }
        }

        [Test]
        public void test_ac025_1_focusedForm_carriesNoColorDeclaration()
        {
            // AC-025-1 ② 的**反控**:可分性不得由颜色承载。
            // 落焦块内零颜色声明 ⇒ 颜色承常态 ⇒ 「非颜色」在 USS 文本层可判真。
            string focused = RuleBody(BrassUssWithoutComments(), "." + FocusedClass);

            Assert.IsFalse(focused.Contains("color"),
                "落焦档块内不得出现任何颜色声明(background-color / color / border-*-color)—— " +
                "可分性须全部由几何承载");
        }

        [Test]
        public void test_ac025_1_setDetents_countNotPositive_throws()
        {
            var element = new BrassScaleElement();

            // ⚠️ 判据须落在 **ParamName** 上,不能只断异常类型:
            //    档数 ≤ 0 时档序守卫(`focusedOrdinal > count`)也抛**同一类型** ⇒
            //    只断类型会被掩蔽(证伪轮 F1 首跑实测 GREEN-FAIL:撤掉档数守卫后本测仍绿,
            //    夹具对档数守卫零判别力)。断 ParamName ⇒ 两个守卫各自可观测。
            var atZero = Assert.Throws<ArgumentOutOfRangeException>(() => element.SetDetents(0, 1),
                "档数 0 ⇒ fail-loud(AC-025-1 ③)");
            Assert.AreEqual("count", atZero.ParamName,
                "须由**档数**守卫拒收 —— 否则是被档序守卫掩蔽(夹具假绿)");

            var atNegative = Assert.Throws<ArgumentOutOfRangeException>(() => element.SetDetents(-3, 1),
                "负档数 ⇒ fail-loud(AC-025-1 ③)");
            Assert.AreEqual("count", atNegative.ParamName,
                "须由**档数**守卫拒收 —— 否则是被档序守卫掩蔽(夹具假绿)");
        }

        [Test]
        public void test_ac025_1_setDetents_focusedOrdinalOutOfRange_throwsNotClamps()
        {
            var element = new BrassScaleElement();

            Assert.Throws<ArgumentOutOfRangeException>(() => element.SetDetents(5, 0),
                "档序 0 越界 ⇒ 抛(AC-025-1 ④)");
            Assert.Throws<ArgumentOutOfRangeException>(() => element.SetDetents(5, 6),
                "档序 count+1 越界 ⇒ 抛(AC-025-1 ④)");
            Assert.AreEqual(0, element.DetentCount,
                "抛后不得留下半成品形态(禁钳位 ⇒ 禁「钳到边界再画」)");
        }

        [Test]
        public void test_ac025_1_detentGeometry_comesFromBrassLayerVariables()
        {
            // AC-025-1 ③:几何值全来自主题变量,零内联字面量。
            string uss = BrassUssWithoutComments();
            string theme = File.ReadAllText(ThemeUssPath);
            string[] blocks =
            {
                RuleBody(uss, "." + BandClass),
                RuleBody(uss, "." + DetentClass),
                RuleBody(uss, "." + FocusedClass)
            };

            string[] geometricProps =
            {
                "width", "height", "min-height", "margin-top", "margin-bottom",
                "margin-left", "margin-right",
                "border-top-width", "border-right-width",
                "border-bottom-width", "border-left-width"
            };

            int declared = 0;
            foreach (string block in blocks)
            {
                foreach (string prop in geometricProps)
                {
                    string value = Declared(block, prop);
                    if (value == null) continue;
                    declared++;

                    Assert.IsTrue(value.StartsWith("var(--skeuo-brass-", StringComparison.Ordinal),
                        $"`{prop}: {value}` 须走 --skeuo-brass-* 变量(零内联字面量,AC-025-1 ③)");

                    string varName = value.Substring(4, value.IndexOf(')') - 4);
                    AssertDeclaredInLayer(theme, varName, BrassLayerMarker, ImplementLayerMarker);
                }
            }

            Assert.GreaterOrEqual(declared, 8,
                $"逐档形态的几何声明应有 ≥ 8 条(实得 {declared})—— 扫描面塌缩 ⇒ 本测假绿");
        }

        // ══════════════ AC-025-2 无剂量状态 ══════════════

        [Test]
        public void test_ac025_2_setDetents_doesNotCacheFocusedOrdinal()
        {
            // 反证面:若把 focusedOrdinal 存进字段并在下次调用时沿用旧值 ⇒ 本测恰红。
            var element = Loaded(5, 3);
            Assert.AreEqual(3, element.FocusedDetentOrdinal, "前置:首次落焦档 = 3");

            element.SetDetents(5, 1);

            Assert.AreEqual(1, element.FocusedDetentOrdinal,
                "第二次入参须**取代**第一次 —— 元件不得留存上一次的落焦档(AC-025-2)");
            Assert.AreEqual(5, element.DetentCount, "档数不变 ⇒ 形态数不变");
        }

        [Test]
        public void test_ac025_2_setDetents_sameArgs_sameForm()
        {
            var first = Loaded(4, 2);
            var second = Loaded(4, 2);

            Assert.AreEqual(first.DetentCount, second.DetentCount, "同入参 ⇒ 同档数");
            Assert.AreEqual(first.FocusedDetentOrdinal, second.FocusedDetentOrdinal,
                "同入参 ⇒ 同落焦档");
            Assert.AreEqual(ShapeSignature(first), ShapeSignature(second),
                "同入参 ⇒ 逐档类名序列逐位相同(幂等,AC-025-2)");
        }

        [Test]
        public void test_ac025_2_element_exposesNoMutableDoseState()
        {
            // 结构判据:元件上不得存在可写字段承载剂量 / 选中档(AC-025-2)。
            // ⚠️ 2026-10-18 评审 R-5:`TickCount` 是**自动属性**,其编译器生成字段名是
            //    `<TickCount>k__BackingField` —— 白名单写 `"TickCount"` **永不命中**
            //    ⇒ 旧白名单是**死码**,该测当时通过纯属巧合(该名恰好不含禁止子串)。
            //    现按 backing field 形状匹配,并加「白名单须真命中」守卫防再次静默失效。
            string[] allowed = { BackingFieldName("TickCount") };
            string[] forbidden = { "Dose", "Selected", "Current", "Ordinal", "Focus", "Detent" };

            var writable = typeof(BrassScaleElement)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .Select(f => f.Name)
                .ToList();

            int whitelisted = writable.RemoveAll(n => allowed.Contains(n));
            Assert.GreaterOrEqual(whitelisted, 1,
                "白名单须**真命中** `TickCount` 的 backing field —— 恒不生效的白名单是死码(评审 R-5)。" +
                $"实测可写字段:[{string.Join(", ", writable)}]");

            foreach (string name in writable)
            {
                Assert.IsFalse(forbidden.Any(t => name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0),
                    $"可写字段 `{name}` 疑似剂量状态 —— 元件不得持剂量(AC-025-2);" +
                    $"若为形态宿主请改用只读引用");
            }
        }

        /// <summary>C# 自动属性的编译器生成字段名(评审 R-5:白名单必须按此形状匹配)。</summary>
        private static string BackingFieldName(string property) => $"<{property}>k__BackingField";

        private static string ShapeSignature(BrassScaleElement element)
        {
            return string.Join("|", Band(element).Children()
                .Select(c => c.ClassListContains(FocusedClass) ? "F" : "n"));
        }

        // ══════════════ AC-025-3 USS 逐档形态类 ══════════════

        [Test]
        public void test_ac025_3_uss_declaresDetentClassesInBrassLayer()
        {
            string uss = BrassUssWithoutComments();

            Assert.IsTrue(uss.Contains("." + BandClass + " {"), "应含 `.brass-detent-band`");
            Assert.IsTrue(uss.Contains("." + DetentClass + " {"), "应含 `.brass-detent`");
            Assert.IsTrue(uss.Contains("." + FocusedClass + " {"), "应含 `.brass-detent--focused`");

            // 新变量须带 `brass` 层注释 —— 主题表内黄铜层注释块须仍存在,
            // 且「黄铜逐档形态」子块须**落在该层区间内**(不只是文件里某处,评审 R-6)。
            string theme = File.ReadAllText(ThemeUssPath);
            var (brassStart, brassEnd) = LayerRange(theme, BrassLayerMarker, ImplementLayerMarker);
            Assert.IsTrue(theme.Contains(BrassLayerMarker),
                "主题表黄铜层注释块应保留(新增变量须落在该层,AC-025-3)");

            string[] themeLines = theme.Split('\n');
            int detentBlock = Array.FindIndex(
                themeLines, l => l.Contains(DetentBlockMarker, StringComparison.Ordinal));
            Assert.GreaterOrEqual(detentBlock, 0, "逐档形态变量须带黄铜层注释(AC-025-3)");
            Assert.IsTrue(detentBlock >= brassStart && detentBlock < brassEnd,
                $"「{DetentBlockMarker}」子块须落在黄铜层区间(行 {brassStart + 1}..{brassEnd})内 —— " +
                $"实得行 {detentBlock + 1}(评审 R-6)");
        }

        [Test]
        public void test_ac025_3_detentBlocks_haveNoBareColorAndNoBareSize()
        {
            // 与生产门 C4 同口径:`(background-color|color|border-color): (rgb(|#)` 与 `\d+px`。
            var bareColor = new Regex(@"(background-color|color|border-color)\s*:\s*(rgb\(|#)");
            var bareSize = new Regex(
                @"(padding|margin|width|height|border-top-width|border-right-width|" +
                @"border-bottom-width|border-left-width)\s*:\s*\d+px");

            string uss = BrassUssWithoutComments();
            foreach (string selector in new[] { BandClass, DetentClass, FocusedClass })
            {
                string body = RuleBody(uss, "." + selector);

                Assert.IsFalse(bareColor.IsMatch(body),
                    $"`{selector}` 块内不得有裸色值(C4 / AC-025-3 ②)");
                Assert.IsFalse(bareSize.IsMatch(body),
                    $"`{selector}` 块内不得有裸尺寸(C4 / AC-025-3 ②)");
            }
        }

        [Test]
        public void test_ac025_3_detentBlocks_haveNoNumericText()
        {
            // AC-025-3 ③ / AC-025-5 ③:`content:` 不得承载数字(零数字铁律在 USS 侧的延伸)。
            var numericContent = new Regex(@"content\s*:\s*""[^""]*\d");
            var fraction = new Regex(@"\d+\s*/\s*\d+");

            string uss = BrassUssWithoutComments();
            foreach (string selector in new[] { BandClass, DetentClass, FocusedClass })
            {
                string body = RuleBody(uss, "." + selector);

                Assert.IsFalse(numericContent.IsMatch(body),
                    $"`{selector}` 块内不得有数字文本(AC-025-3 ③)");
                Assert.IsFalse(fraction.IsMatch(body),
                    $"`{selector}` 块内不得有 `N/M` 形态的读数(AC-025-5 ③)");
            }
        }

        // ══════════════ AC-025-4 每行携带该行自己的形态 ══════════════

        [Test]
        public void test_ac025_4_eachRowCarriesItsOwnOrdinalForm()
        {
            var (_, root) = BuildOnDosePage(5);
            List<VisualElement> rows = CollectDoseRows(root);

            Assert.AreEqual(5, rows.Count, "前置:五档行(AC-025-4 ①)");

            for (int k = 0; k < rows.Count; k++)
            {
                var form = rows[k].Q<BrassScaleElement>();
                Assert.IsNotNull(form, $"第 {k + 1} 行应携带自己的档位形态(AC-025-4)");
                Assert.AreEqual(5, form.DetentCount, "每行形态须覆盖全部档(档数 = 5)");
                Assert.AreEqual(k + 1, form.FocusedDetentOrdinal,
                    $"第 {k + 1} 行应显示「第 {k + 1} 档」形态 —— 行序 = 档位序(AC-025-4 ①)");
            }
        }

        [Test]
        public void test_ac025_4_rowsRemainTheOnlyIntUserDataCarriers()
        {
            // 行收集器按 `userData is int` 认行 ⇒ 形态子件**不得**带整数 userData,
            // 否则行集被撑大(提交链 / 翻页链连带错位)。
            var (_, root) = BuildOnDosePage(5);
            List<VisualElement> rows = CollectDoseRows(root);

            Assert.AreEqual(5, rows.Count, "行集恰 = 档数(子件未污染行集)");
            foreach (VisualElement row in rows)
            {
                Assert.IsNotNull(row.Q<BrassScaleElement>(),
                    "行内的形态元件本身不得带整数 userData(否则它会被当成行)");
                Assert.IsNull(row.Q<BrassScaleElement>().userData,
                    "形态元件的 userData 须为 null");
            }
        }

        [Test]
        public void test_ac025_4_rowSubmitStillForwardsExplicitOrdinal()
        {
            // AC-025-4 ④:提交链零改动 —— 形态换掉标签后,Submit 仍转发**显式 ordinal**。
            var root = new VisualElement { name = "casebook-root" };
            var sink = new FakeSink();
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(sink);
            screen.ApplyData(new FakeSource { Detents = 5 });
            screen.SubmitPageNext();

            List<VisualElement> rows = CollectDoseRows(root);
            Assert.AreEqual(5, rows.Count, "前置:五档行");

            screen.SubmitDose((int)rows[3].userData);

            Assert.AreEqual(new[] { 4 }, sink.Doses,
                "第四档行 ⇒ 转发显式 ordinal(4),非焦点路径索引(AC-025-4 ④)");
        }

        [Test]
        public void test_ac025_4_detentCountUnchanged_idempotentShortCircuitHolds()
        {
            // AC-025-4 ③:幂等短路 `_builtDetentCount` 保留 —— 档数未变 ⇒ 行对象不被重建
            // (否则真机 20 Hz tick 循环每 tick 摘掉重建 ⇒ 落焦行丢焦)。
            var (screen, root) = BuildOnDosePage(5);
            VisualElement firstRow = CollectDoseRows(root)[0];

            screen.ApplyData(new FakeSource { Detents = 5 });   // 同档数重入

            Assert.AreSame(firstRow, CollectDoseRows(root)[0],
                "同档数重入 ⇒ 行对象同一(短路生效,AC-025-4 ③)");
        }

        [Test]
        public void test_ac025_4_detentCountChanged_rowsRebuiltWithNewForms()
        {
            // 反向半边:档数**变了** ⇒ 必须真重建(短路不得吞掉变化)。
            var root = new VisualElement { name = "casebook-root" };
            var screen = new CasebookScreen(root, new SkeuoElementLibrary());
            screen.BindCommandSink(new FakeSink());
            screen.ApplyData(new FakeSource { Detents = 5 });
            screen.SubmitPageNext();

            screen.ApplyData(new FakeSource { Detents = 3 });

            List<VisualElement> rows = CollectDoseRows(root);
            Assert.AreEqual(3, rows.Count, "档数 5 → 3 ⇒ 行集重建为三行");
            for (int k = 0; k < rows.Count; k++)
            {
                var form = rows[k].Q<BrassScaleElement>();
                Assert.AreEqual(3, form.DetentCount, "新形态须覆盖新档数");
                Assert.AreEqual(k + 1, form.FocusedDetentOrdinal, "行序仍 = 档位序");
            }
        }

        // ══════════════ AC-025-5 零数字铁律 ══════════════

        [Test]
        public void test_ac025_5_dosePageText_carriesNoDecimalDigit()
        {
            // ⚠️ 2026-10-18 评审 R-7:扫描面 = **方笺页子树**,不是整棵脉案树。
            //    AC-025-5① 只要求「**行内** Label 文本不含十进制数字」;旧夹具扫
            //    `root.Query<Label>()`(含 `display:none` 的其它页 —— `ApplyPageVisibility`
            //    只切 display,节点仍在树内)⇒ 未来给病症册加序号 / 翻页指示器加「第 2 页」
            //    都会让本测**假红**,而它们并不违反 AC-025-5。
            //    本处取「方笺页」而非「档位行」⇒ 仍略强于 AC 原文(行内 ⊂ 页内)。
            var (_, root) = BuildOnDosePage(5);
            var decimalDigit = new Regex(@"\d");

            VisualElement page = root.Q<VisualElement>("prescription-page");
            Assert.IsNotNull(page, "前置:方笺页存在(扫描面锚点)");

            // 非空洞守卫:先证「形态真的渲染出来了」——
            // 否则「页内零文本」可由「页内什么都没有」平凡满足(假绿)。
            int forms = page.Query<VisualElement>(className: DetentClass).Build().Count();
            Assert.GreaterOrEqual(forms, 5,
                $"前置:方笺页应渲染出 ≥ 5 个档形态(实得 {forms})—— 否则本测空洞为真");

            foreach (Label label in page.Query<Label>().Build())
            {
                Assert.IsFalse(decimalDigit.IsMatch(label.text ?? string.Empty),
                    $"方笺页文本「{label.text}」含十进制数字 —— 档位读数须走形态(AC-025-5)");
            }
        }

        [Test]
        public void test_ac025_5_element_exposesNoStringNumericReadout()
        {
            // AC-025-5 ②:元件不暴露 string 数字读数 API。
            // ⚠️ **必须 DeclaredOnly** —— `VisualElement` 自带 `name` / `tooltip` /
            // `viewDataKey` / `ToString()` 等 string 面;不限定声明层会把基类面算进来,
            // 断言恒红(首跑实测即此)。
            var stringMembers = typeof(BrassScaleElement)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                            | BindingFlags.DeclaredOnly)
                .Where(m => m is PropertyInfo p && p.PropertyType == typeof(string)
                         || m is MethodInfo mi && mi.ReturnType == typeof(string))
                .Select(m => m.Name)
                .ToList();

            Assert.IsEmpty(stringMembers,
                $"元件不得暴露 string 读数面(实得:{string.Join(", ", stringMembers)})—— " +
                "档位读数一律走形态,零文本读数(AC-025-5)");
        }

        [Test]
        public void test_ac025_5_wholeBrassUss_hasNoNumericContent()
        {
            // ⚠️ 2026-10-18 评审 R-10:原测名 `..._hasNoFractionReadout` 断的是全域 `N/M`,
            //    而 AC-025-5③ 逐字要的是「USS 无 **`content:`** 数字」⇒ 断言面与 AC 文本错位。
            //    现对齐 AC 文本为主判据;全域 `N/M` 面**保留为附加判据**(同属零数字铁律,
            //    与 AC-023-2 的黄铜侧延伸面一致)。
            string uss = BrassUssWithoutComments();

            var numericContent = new Regex(@"content\s*:\s*""[^""]*\d");
            Assert.IsFalse(numericContent.IsMatch(uss),
                "SkeuoBrass.uss 全域不得出现 `content:` 数字(AC-025-5③)");

            var fraction = new Regex(@"\d+\s*/\s*\d+");
            Assert.IsFalse(fraction.IsMatch(uss),
                "SkeuoBrass.uss 全域不得出现 `N/M` 形态读数(附加面 · AC-023-2 黄铜侧延伸)");
        }

        // ══════════════ AC-025-6 黄铜分支归属 ══════════════

        [Test]
        public void test_ac025_6_formCarrier_staysInBrassBranch_notInk()
        {
            var element = Loaded(5, 1);

            // 真分类器(清债轮 · TD-prescription-07:原 ClassListContains("brass") 是
            // 自造断言顶替 AC 宾语 —— 分支归属的判定点 = SkeuoMaterialBranchClassifier)
            Assert.That(SkeuoMaterialBranchClassifier.Of(element),
                Is.EqualTo(SkeuoMaterialBranch.Brass), "形态载体须归黄铜分支(AC-42-F3)");
            Assert.That(SkeuoMaterialBranchClassifier.Of(element),
                Is.Not.EqualTo(SkeuoMaterialBranch.Ink), "形态载体不得归墨侧分支(反控)");

            foreach (VisualElement detent in Band(element).Children())
            {
                Assert.IsTrue(detent.ClassListContains(DetentClass),
                    "每个形态子件须带常态档类名");
                Assert.That(SkeuoMaterialBranchClassifier.Of(detent),
                    Is.EqualTo(SkeuoMaterialBranch.Brass),
                    "形态子件不得被误判为墨侧(AC-025-6)");
            }
        }

        [Test]
        public void test_ac025_6_detentClasses_areNotInkSideVocabulary()
        {
            // AC-025-6 负向:墨侧 USS 不得引逐档形态族类(否则黄铜/墨分支误判)。
            string inkPath = Path.Combine(SkeuoDir, "SkeuoInk.uss");
            if (!File.Exists(inkPath)) Assert.Ignore("SkeuoInk.uss 不在位 —— 墨侧分支判据跳过");

            string ink = File.ReadAllText(inkPath);
            Assert.IsFalse(ink.Contains("." + DetentClass),
                "墨侧 USS 不得引 `.brass-detent`(AC-025-6 分支误判)");
            Assert.IsFalse(ink.Contains("." + FocusedClass),
                "墨侧 USS 不得引 `.brass-detent--focused`(AC-025-6 分支误判)");
        }
    }
}
