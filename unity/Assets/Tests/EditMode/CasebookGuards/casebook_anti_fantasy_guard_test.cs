// S26-3 · casebook-004 —— 反幻想三条「绝不」的能力面守卫判据。
//
// 权威:design/gdd/casebook.md 规则四 · **AC-39-05** / **AC-39-06a** / **AC-39-08**
//       门本体:unity/Assets/Editor.Tools.Gates/CasebookAntiFantasyGate.cs
//       production/qa/qa-plan-sprint-26-2026-11-08.md §S26-3
//
// ⚠️ 本文件住 `CasebookGuards/`(**大装配** `Sim.Contracts.Tests`)而不是 `Casebook/`:
//    后者有自己的 asmdef(`Casebook.Tests`),只引 `Sim.Contracts` + `Gameplay.UI`,
//    **够不着 `Editor.Tools.Gates`** ⇒ 在那里调不到门本体。这是落点事实,不是偏好。
//
// ⚠️ 假绿防线:
//   · **F4**:每条「零命中」前先断扫描面非空;
//   · **F3**:词表期望值写**字面量**,不引被测常量;
//   · **F6**:本门有 `file:line` + 有能证明它会红的负夹具(下面四条)。
//
// ⚠️ 读真实资产(`Casebook39.uxml` / `assets/data/casebook_text.json`)的具名声明(承 `.claude/rules/test-standards.md`):
//    **只读**这两件,不写;之所以读真表而非内联字面量 —— 要验的正是**盘上那份**资产里有没有
//    反幻想词族、以及 UXML 的每个 `text=` 是否真的都在闭集里。内联一份副本会让
//    「资产被改坏 / 有人偷偷加了一段字」完全不可见。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.CasebookGuards
{
    [TestFixture]
    public class casebook_anti_fantasy_guard_test
    {
        // ══════════════ 正控:真 39 公开面零违例 ══════════════

        [Test]
        public void test_ac39_05_realCasebookSurface_hasNoDiseaseOrAggregationApi()
        {
            List<string> errs = CasebookAntiFantasyGate.RunAll();

            Assert.That(errs, Is.Empty,
                "39 公开面出现病种维度 / 聚合催促 API —— AC-39-05 / AC-39-08:\n"
                + string.Join("\n", errs));
        }

        [Test]
        public void test_ac39_05_scanSurface_isNotEmpty()
        {
            // 空扫描面会让上面那条恒绿(F4)—— 先证「真的扫到了东西」
            var asm = typeof(DaYiJingCheng.Gameplay.UI.Casebook.CaseListOrdering).Assembly;
            Type[] types = asm.GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Gameplay.UI.Casebook")
                .ToArray();

            Assert.That(types.Length, Is.GreaterThan(0), "39 命名空间零类型 —— 扫描面丢失");
            Assert.That(types.Any(t => t.Name == "CaseListOrdering"), Is.True,
                "扫描的不是 39 这一支");
        }

        // ══════════════ 负夹具:证明门有牙齿 ══════════════

        private sealed class LeakyDiseaseSurface
        {
            public int CountByDisease(int diseaseId) => 0;
        }

        [Test]
        public void test_ac39_05_fixture_countByDisease_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanPublicSurface(
                new[] { typeof(LeakyDiseaseSurface) }, "fixture");

            Assert.That(errs, Is.Not.Empty, "`CountByDisease` 未被抓住 —— 门无牙齿");
            StringAssert.Contains("CountByDisease", string.Join("\n", errs));
            StringAssert.Contains("AC-39-05", string.Join("\n", errs));
        }

        private readonly struct FakeDiseaseSnapshot { }

        private sealed class LeakyReturnTypeSurface
        {
            // 方法名干干净净,但返回的类型本身带病种维度 —— 改名旁路
            public FakeDiseaseSnapshot Get() => default;
        }

        [Test]
        public void test_ac39_05_fixture_cleanNameButDiseaseReturnType_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanPublicSurface(
                new[] { typeof(LeakyReturnTypeSurface) }, "fixture");

            Assert.That(errs, Is.Not.Empty,
                "返回类型里的病种维度没被抓住 —— 「改个名字就绕过去」的门等于没有");
            // 报文点的是「返回类型」这一面 + 命中的词面(而不是类型名全串)——
            // 断言跟着**实际契约**写,不跟着我一厢情愿的措辞写。
            StringAssert.Contains("返回类型", string.Join("\n", errs));
            StringAssert.Contains("diseas", string.Join("\n", errs).ToLowerInvariant());
        }

        private sealed class LeakyAggregationSurface
        {
            public int CountUnopened => 0;
        }

        [Test]
        public void test_ac39_08_fixture_unopenedCount_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanPublicSurface(
                new[] { typeof(LeakyAggregationSurface) }, "fixture");

            Assert.That(errs, Is.Not.Empty, "`CountUnopened`(未结计数)未被抓住");
            StringAssert.Contains("AC-39-08", string.Join("\n", errs));
        }

        // ══════════════ 正控(合成):合法用法不得误判 ══════════════

        private sealed class CleanSurface
        {
            // 这三个词**刻意**在合法用法里出现 —— 门若把它们也拉黑,就成了永远红的噪声
            public int Count => 0;
            public string Group => "bundle";
            public string SortKey => "k";
        }

        [Test]
        public void test_cleanSurface_withLegalSortGroupCount_isGreen()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanPublicSurface(
                new[] { typeof(CleanSurface) }, "fixture");

            Assert.That(errs, Is.Empty,
                "合法用法被误判 —— 上面四条负夹具可能是「永远红」而失去判别力:\n"
                + string.Join("\n", errs));
        }

        [Test]
        public void test_emptyScanSurface_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanPublicSurface(new Type[0], "fixture");
            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("0 个类型", errs[0]);
        }

        // ══════════════ 类型面:39 的呈现 DTO 过 PresentationDtoGuard(AC-39-01 的本 story 侧)══════════════

        [Test]
        public void test_ac39_01_casebookPresentationDtos_passDtoGuard()
        {
            // AC-39-01 自 2026-09-19 起是 ADVISORY(它是 AC-37-15 的真子集),但**登记义务仍在** ——
            // 39 的姿态是「向 37 的闭集登记**消费的类型名**」,登记物就是下面这四个。
            Type[] roots =
            {
                typeof(DaYiJingCheng.Gameplay.UI.Casebook.ProjectedVolume),
                typeof(DaYiJingCheng.Gameplay.UI.Casebook.VolumePage),
                typeof(DaYiJingCheng.Gameplay.UI.Casebook.JudgmentTrace),
                typeof(DaYiJingCheng.Sim.Contracts.CaseId),
            };
            Assert.That(roots.Length, Is.EqualTo(4), "扫描面须非空且恰为本件声明的四处(F4)");

            var errs = new List<string>();
            foreach (Type t in roots) errs.AddRange(DaYiJingCheng.EditorTools.Gates.PresentationDtoGuard.Scan(t));

            Assert.That(errs, Is.Empty,
                "39 的呈现 DTO 出现病种维度字段 —— AC-39-01 / AC-37-15:\n" + string.Join("\n", errs));
        }

        private sealed class LeakyVolumeDto
        {
            // ReSharper disable once NotAccessedField.Local
            public int diseaseId;
        }

        [Test]
        public void test_ac39_01_fixture_leakyDto_isRed()
        {
            // 负控:证明上面那条的扫描器在本扫描面上真的会红(F4)
            var errs = DaYiJingCheng.EditorTools.Gates.PresentationDtoGuard.Scan(typeof(LeakyVolumeDto));
            Assert.That(errs, Is.Not.Empty, "DtoGuard 抓不住 diseaseId ⇒ 上一条无判别力");
        }

        // ══════════════ AC-39-06a 键闭集(2026-11-08 补:此前「没有宾语」)══════════════

        [Test]
        public void test_ac39_06a_realTextKeySet_isClosedAndClean()
        {
            // 真数据集走一遍完整链路(读盘 → 词法 → 闭集判据),不内联副本
            string path = Path.Combine(RepoRoot(), CasebookAntiFantasyGate.CasebookTextRelativePath);
            Assert.That(File.Exists(path), Is.True, $"缺文案键闭集:`{path}`");

            var errs = new List<string>();
            var entries = CasebookAntiFantasyGate.ParseTextKeys(File.ReadAllText(path), errs);
            errs.AddRange(CasebookAntiFantasyGate.ScanTextKeySet(entries, "casebook_text.json"));

            Assert.That(entries.Count, Is.GreaterThan(0), "闭集抽不出条目 —— 判据空转(F4)");
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac39_06a_uxmlTexts_allComeFromClosedSet()
        {
            // 这一条才是「闭集」的牙齿:UXML 里自由新增的字面量绕不过去
            string repo = RepoRoot();
            string textPath = Path.Combine(repo, CasebookAntiFantasyGate.CasebookTextRelativePath);
            string uxmlPath = Path.Combine(repo, CasebookAntiFantasyGate.CasebookUxmlRelativePath);

            var sink = new List<string>();
            var entries = CasebookAntiFantasyGate.ParseTextKeys(File.ReadAllText(textPath), sink);
            var texts = CasebookAntiFantasyGate.ExtractUxmlTexts(File.ReadAllText(uxmlPath));

            Assert.That(texts.Count, Is.GreaterThan(0), "抽不到 text= —— 判据空转");
            List<string> errs = CasebookAntiFantasyGate.ScanUxmlAgainstClosedSet(
                texts, entries.Select(e => e.text).ToList(), "Casebook39.uxml");
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac39_06a_fixture_textOutsideClosedSet_isRed()
        {
            var texts = new List<(string, string)> { ("fixture", "读数") };
            List<string> errs = CasebookAntiFantasyGate.ScanUxmlAgainstClosedSet(
                texts, new List<string> { "脉案" }, "fixture");

            Assert.That(errs, Has.Count.EqualTo(1), string.Join("\n", errs));
            StringAssert.Contains("不在键闭集里", errs[0]);
        }

        [Test]
        public void test_ac39_06a_fixture_badKeyShape_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanTextKeySet(
                new List<(string, string)> { ("Disease_Name", "病名") }, "fixture");

            Assert.That(errs, Is.Not.Empty, "键形不合法未被抓住");
            StringAssert.Contains("形状不合法", string.Join("\n", errs));
        }

        [Test]
        public void test_ac39_06a_fixture_duplicateKey_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanTextKeySet(
                new List<(string, string)> { ("cbTitle", "脉案"), ("cbTitle", "脉案") }, "fixture");

            Assert.That(errs.Count, Is.EqualTo(1), string.Join("\n", errs));
            StringAssert.Contains("重复", errs[0]);
        }

        [Test]
        public void test_ac39_06a_fixture_emptyText_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanTextKeySet(
                new List<(string, string)> { ("cbTitle", "   ") }, "fixture");

            Assert.That(errs.Count, Is.EqualTo(1), string.Join("\n", errs));
            StringAssert.Contains("文案为空", errs[0]);
        }

        [Test]
        public void test_ac39_06a_emptyKeySet_isRed()
        {
            // F4:空闭集会让「UXML ∈ 闭集」恒假,也会让门看起来全绿
            List<string> errs = CasebookAntiFantasyGate.ScanTextKeySet(
                new List<(string, string)>(), "fixture");

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("0 条", errs[0]);
        }

        [Test]
        public void test_ac39_06a_emptyUxmlTextSurface_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanUxmlAgainstClosedSet(
                new List<(string, string)>(), new List<string> { "脉案" }, "fixture");

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("0 条", errs[0]);
        }

        [Test]
        public void test_ac39_06a_malformedJson_isRedNotSilent()
        {
            var errs = new List<string>();
            var entries = CasebookAntiFantasyGate.ParseTextKeys("{ this is not json", errs);

            Assert.That(entries, Is.Empty);
            Assert.That(errs.Count, Is.EqualTo(1), "坏 JSON 必须报错,不得静默退化成空集");
            StringAssert.Contains("解析失败", errs[0]);
        }

        [Test]
        public void test_ac39_06a_jsonWithoutKeysArray_isRedNotSilent()
        {
            var errs = new List<string>();
            var entries = CasebookAntiFantasyGate.ParseTextKeys("{\"schemaVersion\":1}", errs);

            Assert.That(entries, Is.Empty);
            Assert.That(errs.Count, Is.EqualTo(1), "缺 keys 数组必须报错");
            StringAssert.Contains("keys", errs[0]);
        }

        // ══════════════ AC-39-06a 文本面(黑名单半边)══════════════

        [Test]
        public void test_ac39_06a_realUxml_hasNoAntiFantasyWords()
        {
            string path = Path.Combine(RepoRoot(), CasebookAntiFantasyGate.CasebookUxmlRelativePath);

            // 先证资产**存在且抽得到文本**(否则下面的零命中是空转)
            Assert.That(File.Exists(path), Is.True, $"缺 39 的 UXML:`{path}`");
            List<(string where, string text)> texts =
                CasebookAntiFantasyGate.ExtractUxmlTexts(File.ReadAllText(path));
            Assert.That(texts.Count, Is.GreaterThan(0), "UXML 里抽不到任何 text= —— 判据空转");

            List<string> errs = CasebookAntiFantasyGate.ScanUxmlFile(path, "Casebook39.uxml");
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac39_06a_fixture_antiFantasyWord_isRed()
        {
            var texts = new List<(string where, string text)>
            {
                ("fixture", "这些病例同源"),
                ("fixture", "未结 3 例"),
            };

            List<string> errs = CasebookAntiFantasyGate.ScanTexts(texts, "fixture");

            Assert.That(errs.Count, Is.EqualTo(2), string.Join("\n", errs));
            StringAssert.Contains("同源", string.Join("\n", errs));
            StringAssert.Contains("未结", string.Join("\n", errs));
        }

        [Test]
        public void test_ac39_06a_emptyTextSurface_isRed()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanTexts(
                new List<(string, string)>(), "fixture");

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("0 条", errs[0]);
        }

        [Test]
        public void test_ac39_06a_missingUxml_isRedNotSilent()
        {
            List<string> errs = CasebookAntiFantasyGate.ScanUxmlFile(
                Path.Combine(Path.GetTempPath(), "no-such-casebook-uxml.uxml"), "fixture");

            Assert.That(errs, Has.Count.EqualTo(1), "缺资产必须报错,不得静默通过");
            StringAssert.Contains("找不到", errs[0]);
        }

        // ── 仓库根(用 [CallerFilePath] 向上找,不开「仓库锚点 + 写盘」的声明口子)──

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
