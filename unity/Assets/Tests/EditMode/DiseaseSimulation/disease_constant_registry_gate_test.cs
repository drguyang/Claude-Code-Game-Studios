// PRE-S18-B 机械半边 —— 9 常量登记门的判据测试(2026-10-22 立)。
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件要证明的三件事(缺一即「门存在」不成立)
// ══════════════════════════════════════════════════════════════════════════
//   ① **真工程绿** —— 对真 `design/registry/entities.yaml` 跑,零违例。
//   ② **门真会红**(非空转)—— 用临时 YAML 注入,要求 D1 / D2 **各自**被抓到。
//      ⚠️ 没有这一条,「零违例」与「正则写错所以永不命中」无法区分。
//   ③ **反向不误伤** —— 只被 `variables:` 列表**引用**的同名串、前缀更长的名字
//      (`DEATH_THRESHOLD_X`)都**不得**被当成块命中。
//      这一条针对 D1 的正则:锚串写成 `- name: DEATH_THRESHOLD` 而不锚行尾,
//      会把 `DEATH_THRESHOLD_X` 也认成 `DEATH_THRESHOLD` 的块 ⇒ **假绿**。
//
// ⚠️ 测试写盘一律落 `Path.GetTempPath()`(承 `.claude/rules/test-standards.md`
//    与 `FsWriteBoundaryGate` 的 R1 判据 —— 本文件写的是**临时 YAML**,不是仓库)。
//
// 权威:`production/sprint-status.yaml` PRE-S18-B ·
//      `design/gdd/disease-simulation.md` §五 ·
//      `unity/Assets/Editor.Tools.Gates/DiseaseConstantRegistryGate.cs`(被测对象)

using System;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>9 常量登记门:真 registry 绿 + D1/D2 各自转红 + 反向不误伤。</summary>
    [TestFixture]
    public sealed class DiseaseConstantRegistryGateTest
    {
        private string _tmpDir;

        [SetUp]
        public void Setup()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(),
                "disconst-probe-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tmpDir);
        }

        [TearDown]
        public void Teardown()
        {
            try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); }
            catch (IOException) { /* 临时目录清理失败不构成测试失败 */ }
        }

        /// <summary>写一份临时 YAML,返回其路径。</summary>
        private string WriteYaml(string content, string fileName = "entities.yaml")
        {
            string path = Path.Combine(_tmpDir, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>造一个字段齐全的合法块(缩进与真 registry 一致:两空格 + `- name:`)。</summary>
        private static string GoodBlock(string name) =>
            $"  - name: {name}\n" +
            "    status: decided\n" +
            "    source: \"test fixture\"\n" +
            "    referenced_by:\n" +
            "      - 9.疾病状态机\n" +
            "    value: \"待定\"\n" +
            "    unit: \"Q16.16 position\"\n" +
            "    constraint: \"test\"\n";

        /// <summary>造一份六个常量全齐的合法 YAML。</summary>
        private static string GoodYaml() =>
            "constants:\n" +
            string.Concat(DiseaseConstantRegistryGate.RequiredConstants.Select(GoodBlock));

        // ══════════════════════════════════════════════════════════════════════
        // ① 真工程绿
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void test_realRegistry_isClean()
        {
            var errs = DiseaseConstantRegistryGate.RunAll();
            Assert.That(errs, Is.Empty,
                "真 `entities.yaml` 有违例:\n  " + string.Join("\n  ", errs));
        }

        /// <summary>
        /// ⚠️ 守卫「扫描面非空」:若 `EntitiesYamlPath()` 推导错了(路径改版 / 仓库布局变动),
        /// 上面那条会因为「文件不存在」而红 —— 但若将来有人把「文件不存在」改成「跳过」,
        /// 这条仍能抓住。**空集冒充绿**是本项目抓过的形态,故显式钉住路径真实性。
        /// </summary>
        [Test]
        public void test_realRegistry_pathResolvesToAnExistingFile()
        {
            string path = DiseaseConstantRegistryGate.EntitiesYamlPath();
            Assert.That(File.Exists(path), Is.True,
                $"`EntitiesYamlPath()` 推导出的路径不存在:{path}\n" +
                "⇒ 判据面丢失(假绿面),不是「通过」");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 门真会红
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>D1:整个块缺失 ⇒ 红,且错误串点名该常量。</summary>
        [Test]
        public void test_D1_missingBlock_isRed()
        {
            // 合法 YAML 里挖掉 DEATH_THRESHOLD 整块
            string yaml = GoodYaml().Replace(GoodBlock("DEATH_THRESHOLD"), "");
            var errs = DiseaseConstantRegistryGate.RunAll(WriteYaml(yaml));

            Assert.That(errs.Count, Is.EqualTo(1), "应恰有 1 条违例(只缺 DEATH_THRESHOLD)");
            Assert.That(errs[0], Does.Contain("DEATH_THRESHOLD").And.Contain("/D1"));
        }

        /// <summary>
        /// D1 的**核心形态**(PRE-S18-B 的实际情形):名字只出现在 `variables:` 列表里,
        /// 没有自己的 `name:` 块 ⇒ 必须红。
        /// <para>这一条若不红,本门对**它要防的那个真实缺陷**就是失明的。</para>
        /// </summary>
        [Test]
        public void test_D1_nameOnlyReferencedInVariablesList_isRed()
        {
            string yaml =
                "constants:\n" +
                GoodBlock("RECOVER_THRESHOLD") +
                GoodBlock("CARE_GAP") +
                // 三阈值 + 干预窗口只以**引用**形态存在 —— PRE-S18-B 的原样
                "  - name: 9.疾病状态机\n" +
                "    variables:\n" +
                "      - CRITICAL_THRESHOLD\n" +
                "      - COMA_THRESHOLD\n" +
                "      - DEATH_THRESHOLD\n" +
                "      - TREATMENT_WINDOW\n";

            var errs = DiseaseConstantRegistryGate.RunAll(WriteYaml(yaml));

            Assert.That(errs.Count, Is.EqualTo(4),
                "四个只在 variables 列表里的量应各自报 D1:\n  " + string.Join("\n  ", errs));
            foreach (string n in new[] { "CRITICAL_THRESHOLD", "COMA_THRESHOLD",
                                         "DEATH_THRESHOLD", "TREATMENT_WINDOW" })
            {
                Assert.That(errs.Any(e => e.Contains(n) && e.Contains("/D1")), Is.True,
                    $"`{n}` 未被报出");
            }
        }

        /// <summary>D2:块在但缺 `value:` ⇒ 红(「没写 = 用默认值」是最危险的误读)。</summary>
        [Test]
        public void test_D2_missingValueField_isRed()
        {
            string yaml = GoodYaml().Replace(
                GoodBlock("COMA_THRESHOLD"),
                GoodBlock("COMA_THRESHOLD").Replace("    value: \"待定\"\n", ""));
            var errs = DiseaseConstantRegistryGate.RunAll(WriteYaml(yaml));

            Assert.That(errs.Count, Is.EqualTo(1));
            Assert.That(errs[0], Does.Contain("COMA_THRESHOLD").And.Contain("`value:`"));
        }

        /// <summary>D2:六项逐个缺失,每一项都要单独报出来(不能只报第一条)。</summary>
        [Test]
        public void test_D2_everyRequiredFieldIsChecked()
        {
            foreach (string field in DiseaseConstantRegistryGate.RequiredFields)
            {
                // 按字段名整行剔除 —— 不靠值字面量匹配(各字段的值形态不同:
                // `status: decided` 是标量、`referenced_by:` 是列表、`value:` 是带引号的「待定」)。
                string block = GoodBlock("DEATH_THRESHOLD");
                string stripped = string.Join("\n",
                    block.Split('\n').Where(l => !l.TrimStart().StartsWith(field + ":", StringComparison.Ordinal))
                ) + "\n";

                string yaml = GoodYaml().Replace(block, stripped);
                // 只查**该块**内是否真的没了这个字段 —— 查整份 YAML 会因别的块仍带该字段而误报。
                string probed = DiseaseConstantRegistryGate.ExtractBlock(yaml, "DEATH_THRESHOLD");
                Assert.That(probed, Is.Not.Null);
                Assert.That(probed, Does.Not.Contain($"    {field}:"),
                    $"夹具构造失败:`{field}:` 仍在该块里,本用例没测到 D2");

                var errs = DiseaseConstantRegistryGate.RunAll(WriteYaml(yaml));
                Assert.That(errs.Any(e => e.Contains("DEATH_THRESHOLD") && e.Contains($"`{field}:`")),
                    Is.True, $"缺 `{field}:` 未被 D2 报出(该字段的判据可能是空的)");
            }
        }

        /// <summary>文件不存在 ⇒ 红(不是静默跳过)。</summary>
        [Test]
        public void test_missingFile_isRed()
        {
            var errs = DiseaseConstantRegistryGate.RunAll(
                Path.Combine(_tmpDir, "definitely-not-here.yaml"));
            Assert.That(errs, Is.Not.Empty, "文件不存在必须转红 —— 空集冒充绿是本项目抓过的形态");
        }

        /// <summary>路径为空 ⇒ 红(判据不可执行 ≠ 通过)。</summary>
        [Test]
        public void test_emptyPath_isRed()
        {
            Assert.That(DiseaseConstantRegistryGate.RunAll(""), Is.Not.Empty);
            Assert.That(DiseaseConstantRegistryGate.RunAll((string)null), Is.Not.Empty);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 反向:不误伤
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// **前缀更长的名字不得冒充**:`DEATH_THRESHOLD_X` 有块,而 `DEATH_THRESHOLD` 没有
        /// ⇒ 仍须报 `DEATH_THRESHOLD` 缺块。
        /// <para>若锚串不锚行尾,这里会**假绿** —— 这是本门最容易写错的一处。</para>
        /// </summary>
        [Test]
        public void test_D1_longerPrefixName_doesNotSatisfyShorterName()
        {
            string yaml =
                "constants:\n" +
                GoodBlock("CRITICAL_THRESHOLD") +
                GoodBlock("COMA_THRESHOLD") +
                GoodBlock("DEATH_THRESHOLD_X") +     // ← 只是前缀相同的另一个名字
                GoodBlock("TREATMENT_WINDOW") +
                GoodBlock("RECOVER_THRESHOLD") +
                GoodBlock("CARE_GAP");

            var errs = DiseaseConstantRegistryGate.RunAll(WriteYaml(yaml));

            Assert.That(errs.Any(e => e.Contains("`DEATH_THRESHOLD`") && e.Contains("/D1")), Is.True,
                "`DEATH_THRESHOLD_X` 的块被误当成 `DEATH_THRESHOLD` 的块(锚串未锚行尾 ⇒ 假绿)");
        }

        /// <summary>合法 YAML(六块全齐)⇒ 零违例。防判据过宽。</summary>
        [Test]
        public void test_goodYaml_isClean()
        {
            var errs = DiseaseConstantRegistryGate.RunAll(WriteYaml(GoodYaml()));
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        /// <summary>
        /// 块边界正确性:`ExtractBlock` 取到的文本**不得**包含下一个块的内容。
        /// <para>若边界算错,`HasField` 会读到邻居的 `value:` ⇒ D2 假绿。</para>
        /// </summary>
        [Test]
        public void test_extractBlock_doesNotBleedIntoNextBlock()
        {
            string yaml =
                "constants:\n" +
                "  - name: FIRST\n" +
                "    status: decided\n" +
                "  - name: SECOND\n" +
                "    value: \"邻居的值\"\n";

            string block = DiseaseConstantRegistryGate.ExtractBlock(yaml, "FIRST");

            Assert.That(block, Does.Contain("FIRST"));
            Assert.That(block, Does.Not.Contain("SECOND"), "块边界越界,读到了下一个块");
            Assert.That(block, Does.Not.Contain("邻居的值"));
        }
    }
}
