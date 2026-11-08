// S26-4 · casebook-005 —— 脉案页的**资产可扫面**(UI story 的自动化半边)。
//
// 权威:design/gdd/casebook.md 规则七(焦点序铁律 2026-10-06 订正面)· 规则八(零按键提示)
//       · AC-8-23 / AC-8-44 的 39 侧落点 · production/qa/qa-plan-sprint-26-2026-11-08.md §S26-4
//       门本体:unity/Assets/Editor.Tools.Gates/CasebookAntiFantasyGate.cs
//
// ⚠️ **本文件不重复已覆盖的判据**:焦点环的**运行时**顺序、五行固定成序、「?」位置、
//    UI-8.4 负向声明、UXML ↔ BuildUI 双真源等价 —— 已由 PlayMode 的
//    `Tests/PlayMode/DiagnosisSystem/casebook_render_test.cs`(diagnosis story-006)覆盖。
//    本件补的是它**看不见**的三块:
//      ① UXML 的**声明序**(树序的物理保证 —— PlayMode 断的是建出来的树,不是资产本身);
//      ② 规则八的**文案面**:一段写着「按 E 查看」的 Label 在元件名 token 与反幻想词表里都抓不到;
//      ③ 提示的**旁路**:`tooltip=` / `alt=` 属性。
//
// ⚠️ 读真实资产(`Casebook39.uxml` / `CasebookScreen.cs`)的具名声明(承 `.claude/rules/test-standards.md`):
//    **只读**,不写。读真表而非内联副本 —— 要验的正是**盘上那份**资产;内联副本会让「有人往纸上加了一行提示」完全不可见。
//
// ⚠️ 假绿防线:F4(每条零命中前先断扫描面非空)· F3(期望值写字面量,不引被测常量)

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.EditMode.CasebookGuards
{
    [TestFixture]
    public class casebook_uxml_scan_test
    {
        private const string UxmlRelative =
            "unity/Assets/Gameplay.UI/Skeuomorphic/Screens/Casebook39.uxml";
        private const string ScreenRelative =
            "unity/Assets/Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs";

        private static string Read(string relative)
        {
            string path = Path.Combine(RepoRoot(), relative);
            Assert.That(File.Exists(path), Is.True, $"缺资产:`{path}`");
            return File.ReadAllText(path);
        }

        // ══════════════ 规则七:焦点声明序 = 铁律 ══════════════

        [Test]
        public void test_ac8_23_realUxml_focusDeclarationOrder_matchesIronLaw()
        {
            string uxml = Read(UxmlRelative);

            // 先证扫描面非空(F4):抽不到 focusable 元素时下一条恒绿
            var focusables = CasebookAntiFantasyGate.ScanFocusDeclarationOrder(uxml, "Casebook39.uxml");
            Assert.That(focusables, Is.Empty, string.Join("\n", focusables));

            // 声明序里必须**真的**有前七项(否则上面那条是空转)
            Assert.That(CasebookAntiFantasyGate.FocusIronLaw, Has.Length.EqualTo(7),
                "铁律是七项(五通道 + 问诊栏 + 病名)");
        }

        [Test]
        public void test_ac8_23_fixture_outOfOrderFocusables_isRed()
        {
            string uxml = @"<ui:UXML><ui:VisualElement name=""channel-voice"" focusable=""true"" />
<ui:VisualElement name=""channel-complexion"" focusable=""true"" />
<ui:VisualElement name=""channel-posture"" focusable=""true"" />
<ui:VisualElement name=""channel-breathing"" focusable=""true"" />
<ui:VisualElement name=""channel-palpation"" focusable=""true"" />
<ui:VisualElement name=""channel-inquiry"" focusable=""true"" />
<ui:VisualElement name=""channel-disease-name"" focusable=""true"" /></ui:UXML>";

            var errs = CasebookAntiFantasyGate.ScanFocusDeclarationOrder(uxml, "fixture");

            Assert.That(errs, Is.Not.Empty, "换了两格顺序却没红 —— 门无牙齿");
            StringAssert.Contains("永不重排", string.Join("\n", errs));
        }

        [Test]
        public void test_ac8_23_fixture_tooFewFocusables_isRed()
        {
            string uxml = @"<ui:UXML><ui:VisualElement name=""channel-complexion"" focusable=""true"" /></ui:UXML>";
            var errs = CasebookAntiFantasyGate.ScanFocusDeclarationOrder(uxml, "fixture");

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("焦点环缺员", errs[0]);
        }

        [Test]
        public void test_ac8_23_noTabIndexAnywhere()
        {
            // 声明序即焦点序 —— 一旦有人显式写 tabIndex,这条物理保证就没了
            Assert.That(Read(UxmlRelative).Contains("tabIndex"), Is.False,
                "UXML 出现 tabIndex —— 焦点序不再是声明序(规则七的物理保证失效)");
            Assert.That(Read(ScreenRelative).Contains(".tabIndex"), Is.False,
                "CasebookScreen 代码里出现 tabIndex —— 同上,且会让走查的「序不重排」变成配置结果");
        }

        [Test]
        public void test_ac8_23_channelRows_allFocusable_andNeverHidden()
        {
            string uxml = Read(UxmlRelative);
            foreach (string row in new[]
                     {
                         "channel-complexion", "channel-voice", "channel-posture",
                         "channel-breathing", "channel-palpation", "channel-inquiry",
                     })
            {
                string tag = TagFor(uxml, row);
                Assert.That(tag, Is.Not.Null, $"{row} 不在 UXML 里(行在 = 永不留空)");
                StringAssert.Contains("focusable=\"true\"", tag, $"{row} 必须可聚焦(空行也在序中)");
            }
            Assert.That(uxml.Contains("display"), Is.False,
                "UXML 出现 display 覆写 —— 空行若被隐藏就不可聚焦(空行 = 显式「我还没做」)");
        }

        // ══════════════ 规则八:零按键提示 ══════════════

        [Test]
        public void test_ac8_44_realUxml_zeroHintTexts()
        {
            string uxml = Read(UxmlRelative);
            var texts = CasebookAntiFantasyGate.ExtractUxmlTexts(uxml);

            Assert.That(texts.Count, Is.GreaterThan(0), "抽不到 text= —— 判据空转(F4)");
            var errs = CasebookAntiFantasyGate.ScanHintTexts(texts, "Casebook39.uxml");
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac8_44_fixture_hintText_isRed()
        {
            var texts = new List<(string, string)> { ("fixture", "按 E 查看") };
            var errs = CasebookAntiFantasyGate.ScanHintTexts(texts, "fixture");

            Assert.That(errs, Is.Not.Empty, "一段「按 E 查看」没被抓住 —— 规则八在文案面没有执行体");
            StringAssert.Contains("AC-8-44", string.Join("\n", errs));
        }

        [Test]
        public void test_ac8_44_buildUiFace_zeroHintTexts()
        {
            // ⚠️ **两个面不是一回事**:`Casebook39.uxml` 是 **spec 面**(走查 / 交叉断言用),
            //    运行时 `SkeuoRuntimeDriver` 走的是 `new CasebookScreen(root, lib)` 的
            //    **代码构建面**(`BuildUI()`)。只扫 UXML 会漏掉真正跑的那份 ——
            //    往 `BuildUI()` 里加一句「按 E 查看」,上面几条测都发现不了。
            var root = new VisualElement();
            _ = new DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens.CasebookScreen(
                root, new DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoElementLibrary());

            var texts = new List<(string where, string text)>();
            foreach (Label l in root.Query<Label>().ToList())
            {
                if (string.IsNullOrEmpty(l.text)) continue;
                texts.Add((string.IsNullOrEmpty(l.name) ? "<匿名 Label>" : l.name, l.text));
            }

            Assert.That(texts.Count, Is.GreaterThan(0), "代码面抽不到文案 —— 判据空转(F4)");
            var errs = CasebookAntiFantasyGate.ScanHintTexts(texts, "BuildUI");
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac8_44_realUxml_noTooltipOrAlt()
        {
            string uxml = Read(UxmlRelative);
            var errs = CasebookAntiFantasyGate.ScanHintAttributes(uxml, "Casebook39.uxml");
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac8_44_fixture_tooltipAttribute_isRed()
        {
            string uxml = @"<ui:UXML><ui:VisualElement name=""x"" tooltip=""按 E 落笔"" /></ui:UXML>";
            var errs = CasebookAntiFantasyGate.ScanHintAttributes(uxml, "fixture");

            Assert.That(errs, Has.Count.EqualTo(1), "tooltip 是提示的旁路 —— 必须拒收");
            StringAssert.Contains("tooltip", errs[0]);
        }

        [Test]
        public void test_ac8_44_emptyUxml_isRedNotSilent()
        {
            var errs = CasebookAntiFantasyGate.ScanHintAttributes("", "fixture");
            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("不可执行", errs[0]);
        }

        // ══════════════ P0 无 disabled ══════════════

        [Test]
        public void test_p0_casebookScreen_neverDisablesAnything()
        {
            string src = Read(ScreenRelative);
            Assert.That(src.Contains("SetEnabled(false)"), Is.False,
                "脉案里出现 SetEnabled(false) —— P0 无灰掉的按钮(四法出生自带,规则七)");
            Assert.That(src.Contains("SetEnabled (false)"), Is.False);
        }

        // ── 工具 ──

        private static string TagFor(string uxml, string elementName)
        {
            string needle = $"name=\"{elementName}\"";
            int i = uxml.IndexOf(needle, System.StringComparison.Ordinal);
            if (i < 0) return null;
            int open = uxml.LastIndexOf('<', i);
            int close = uxml.IndexOf('>', i);
            return open < 0 || close < 0 ? null : uxml.Substring(open, close - open + 1);
        }

        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(thisFile));
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "unity", "Assets"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail($"找不到仓库根(自 `{thisFile}` 向上未见到 `unity/Assets`)");
            return null;
        }
    }
}
