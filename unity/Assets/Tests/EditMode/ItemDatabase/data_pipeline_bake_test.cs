// 权威来源:Story 008 QA Test Cases(AC-21a-26 / AC-21a-48 / TR-027 夹具经管线执行 / 确定性 / ConfigVersion)
//          · ADR-014(两阶段烘焙 · 内容哈希)· ADR-010 §七(ConfigVersion 比对非致命)
//          · 约定与 item_validation_fixtures_test.cs 同形:repoRoot 经 [CallerFilePath] 上溯 5 级
//
// ⚠️ 本工程测试不装 Newtonsoft 之外的依赖;经公开烘焙 API(BakeFromSourceText)注入源文本。
// ⚠️ 无计时断言(AC-47 <1ms 归 production/qa 冒烟,ADVISORY,不入本确定性套件)。
// ⚠️ invalid_quality_dist.json 不在本套件扫描面 —— quality_distribution 形状归系统 17 的 GDD
//    (P0 空 struct,GatherProfile 注),本故事无绑定形可喂;理由见 SkippedFixture / FixtureNames 注释。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    /// <summary>Story 008 数据管线烘焙集成测试(词法 → 绑定 → 门 → cooked / 扫描器 / ConfigVersion)。</summary>
    [TestFixture]
    internal sealed class DataPipelineBakeTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string FixtureDir = Path.Combine(RepoRoot, "tests", "unit", "item_database", "fixtures");
        private static readonly string SeedDir = Path.Combine(RepoRoot, "assets", "data");
        private static readonly string UnityAssetsRoot = Path.Combine(RepoRoot, "unity", "Assets");

        /// <summary>本套件明确跳过的夹具(理由:见文件头)。</summary>
        private const string SkippedFixture = "invalid_quality_dist.json";

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        private static string ReadSeed(string fileName)
            => File.ReadAllText(Path.Combine(SeedDir, fileName));

        private static string ReadFixture(string fileName)
            => File.ReadAllText(Path.Combine(FixtureDir, fileName));

        private static void ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson)
        {
            itemsJson = ReadSeed(ItemDatabaseBaker.ItemsFileName);
            recipesJson = ReadSeed(ItemDatabaseBaker.RecipesFileName);
            constantsJson = ReadSeed(ItemDatabaseBaker.ConstantsFileName);
        }

        private static uint ReadHeaderConfigVersion(byte[] cooked)
            => (uint)(cooked[12] | (cooked[13] << 8) | (cooked[14] << 16) | (cooked[15] << 24));

        // ══════════ TR-027:全部负向夹具经烘焙管线硬失败 ══════════

        /// <summary>负向夹具清单(全部 *.json,除 SkippedFixture)。NUnit TestCaseSource 经反射取源 —— 公开静态最稳妥。</summary>
        public static IEnumerable<string> FixtureNames()
        {
            foreach (string path in Directory.GetFiles(FixtureDir, "*.json"))
            {
                string name = Path.GetFileName(path);
                if (string.Equals(name, SkippedFixture, StringComparison.Ordinal))
                    continue; // 形状归 17,P0 无绑定形 —— 见 SkippedFixture 文档注
                yield return name;
            }
        }

        /// <summary>每个既有负向夹具作为 items 源喂入烘焙 ⇒ 必抛 BakeValidationException 且错误列表非空
        /// (facet 形夹具经白名单/缺键拒;invalid_state_int 经 AC-26 拒 —— 判据不同,均硬失败)。</summary>
        [TestCaseSource(nameof(FixtureNames))]
        public void test_itemDatabase_negativeFixture_bakeFailsWithAggregatedErrors(string fixtureName)
        {
            // Arrange
            string fixture = ReadFixture(fixtureName);
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            _ = itemsJson; // 夹具替代 items 源

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(fixture, recipesJson, constantsJson));

            // Assert
            Assert.That(ex.Errors, Is.Not.Empty, $"{fixtureName} 应产生至少 1 条烘焙错误");
            Assert.That(ex.Message, Does.Contain("烘焙校验失败"));
        }

        /// <summary>跳过的夹具登记为可证伪:确实存在于夹具目录且确实被排除(换人扩形时先读理由)。</summary>
        [Test]
        public void test_itemDatabase_qualityDistributionFixture_documentedAsSkipped()
        {
            // Arrange & Act
            bool exists = File.Exists(Path.Combine(FixtureDir, SkippedFixture));

            // Assert
            Assert.That(exists, Is.True, "invalid_quality_dist.json 应存在(否则跳过说明失效)");
            bool included = false;
            foreach (string name in FixtureNames())
                if (string.Equals(name, SkippedFixture, StringComparison.Ordinal)) included = true;
            Assert.That(included, Is.False, "quality_distribution 形状归 17 —— 本套件必须排除该夹具");
        }

        // ══════════ AC-21a-26:int 编码 processing_state ══════════

        /// <summary>负向夹具 invalid_state_int.json 经烘焙 ⇒ 硬失败且点名 AC-21a-26。</summary>
        [Test]
        public void test_ac26_stateIntFixture_bakeFailsCitingAc26()
        {
            // Arrange
            string fixture = ReadFixture("invalid_state_int.json");
            ReadLegalTriple(out _, out string recipesJson, out string constantsJson);

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(fixture, recipesJson, constantsJson));

            // Assert
            Assert.That(ex.Errors, Is.Not.Empty);
            Assert.That(ex.Message, Does.Contain("AC-21a-26"));
        }

        /// <summary>合法 items 源(state 全为明文字符串)⇒ 扫描器零命中。</summary>
        [Test]
        public void test_ac26_legalJsonSource_scannerReportsNoViolation()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out _, out _);

            // Act
            IReadOnlyList<string> violations = StateEncodingScanner.ValidateNoIntEncodedState(itemsJson);

            // Assert
            Assert.That(violations, Is.Empty);
        }

        /// <summary>合成 .asset/YAML 内容(processing_state: 3)⇒ 扫描器命中并点名 AC-21a-26
        /// (扫描面 = 数据产物层,不只查代码字段类型)。</summary>
        [Test]
        public void test_ac26_syntheticAssetYaml_scannerFlagsViolation()
        {
            // Arrange
            const string syntheticAsset =
                "m_Script: {fileID: 11500000}\nprocessing_state: 3\nm_Name: legacy_item\n";

            // Act
            IReadOnlyList<string> violations = StateEncodingScanner.ValidateNoIntEncodedState(syntheticAsset);

            // Assert
            Assert.That(violations.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(violations[0], Does.Contain("AC-21a-26"));
        }

        /// <summary>JSON 数字 token 形("processing_state": 3)⇒ 扫描器命中。</summary>
        [Test]
        public void test_ac26_jsonIntToken_scannerFlagsViolation()
        {
            // Arrange
            const string syntheticJson = "{ \"items\": [ { \"base_id\": \"x\", \"processing_state\": 3 } ] }";

            // Act
            IReadOnlyList<string> violations = StateEncodingScanner.ValidateNoIntEncodedState(syntheticJson);

            // Assert
            Assert.That(violations.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(violations[0], Does.Contain("AC-21a-26"));
        }

        /// <summary>合法种子中 state 改写为数字 token ⇒ 烘焙硬失败且点名 AC-21a-26(绑定层执法面)。</summary>
        [Test]
        public void test_ac26_intStateInLegalSource_bakeFailsCitingAc26()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = itemsJson.Replace("\"processing_state\": \"raw\"", "\"processing_state\": 3");

            // Act
            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(mutated, recipesJson, constantsJson));

            // Assert
            Assert.That(ex.Message, Does.Contain("AC-21a-26"));
        }

        // ══════════ AC-21a-48:运行期结构守卫 ══════════

        /// <summary>真实运行期源码树(Sim / Sim.Contracts / Sim.Codec / Gameplay.*)⇒ 守卫零违例
        /// (玩家构建零 JSON 解析器、零 FixParse、零 assets/data 直读)。</summary>
        [Test]
        public void test_ac48_runtimeSources_guardReportsNoViolation()
        {
            // Act
            IReadOnlyList<string> violations = RuntimeSourceGuard.Scan(UnityAssetsRoot);

            // Assert
            Assert.That(violations, Is.Empty,
                () => string.Join("\n", violations));
        }

        /// <summary>合成运行期源码含 JsonConvert.DeserializeObject 落临时目录 ⇒ 守卫命中并点名 AC-21a-48(阳性对照)。</summary>
        [Test]
        public void test_ac48_syntheticRuntimeSource_guardFlagsViolation()
        {
            // Arrange —— 合成运行期源码落临时目录(路径无 Tests / Editor.* 段 ⇒ 不豁免)
            string tempRoot = Path.Combine(Path.GetTempPath(), "ac48_synth_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                File.WriteAllText(Path.Combine(tempRoot, "SynthRuntime.cs"),
                    "class Synth { void F(string s) { var o = JsonConvert.DeserializeObject<object>(s); } }");

                // Act
                IReadOnlyList<string> violations = RuntimeSourceGuard.Scan(tempRoot);

                // Assert —— 阳性对照:守卫必须命中并点名 AC-21a-48
                Assert.That(violations.Count, Is.GreaterThanOrEqualTo(1),
                    "含 JsonConvert 的合成运行期源码必须被守卫命中");
                Assert.That(violations[0], Does.Contain("AC-21a-48"));
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        // ══════════ 确定性:同源两次烘焙逐位一致 ══════════

        /// <summary>同三源文本烘焙两次 ⇒ 两产物均逐位相同(确定性 cooked)。</summary>
        [Test]
        public void test_determinism_sameSourceTwice_byteIdentical()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);

            // Act
            BakeResult first = ItemDatabaseBaker.BakeFromSourceText(itemsJson, recipesJson, constantsJson);
            BakeResult second = ItemDatabaseBaker.BakeFromSourceText(itemsJson, recipesJson, constantsJson);

            // Assert
            CollectionAssert.AreEqual(first.ItemsCooked, second.ItemsCooked, "items 产物须逐位一致");
            CollectionAssert.AreEqual(first.RecipesCooked, second.RecipesCooked, "recipes 产物须逐位一致");
            Assert.That(first.ConfigVersion, Is.EqualTo(second.ConfigVersion));
        }

        // ══════════ ConfigVersion(ADR-014 §五 / ADR-010 §七)══════════

        /// <summary>相同输入 ⇒ ConfigVersion 相同。</summary>
        [Test]
        public void test_configVersion_identicalInputs_equalHash()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            var sources = ItemDatabaseBaker.NamedSources(itemsJson, recipesJson, constantsJson);

            // Act
            uint a = ConfigVersionUtility.DeriveConfigVersion(sources);
            uint b = ConfigVersionUtility.DeriveConfigVersion(sources);

            // Assert
            Assert.That(a, Is.EqualTo(b));
        }

        /// <summary>任一字节改写 ⇒ ConfigVersion 必变(内容哈希随数据变)。</summary>
        [Test]
        public void test_configVersion_mutatedByte_differs()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = itemsJson.Replace("\"stack_max\": 99", "\"stack_max\": 98");

            // Act
            uint before = ConfigVersionUtility.DeriveConfigVersion(
                ItemDatabaseBaker.NamedSources(itemsJson, recipesJson, constantsJson));
            uint after = ConfigVersionUtility.DeriveConfigVersion(
                ItemDatabaseBaker.NamedSources(mutated, recipesJson, constantsJson));

            // Assert
            Assert.That(after, Is.Not.EqualTo(before), "改一个字节必须改哈希(否则存档头联动失效)");
        }

        /// <summary>产物头 offset 12 的 ConfigVersion == 烘焙结果 ConfigVersion(头载荷一致性)。</summary>
        [Test]
        public void test_configVersion_cookedHeaderMatchesResult()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);

            // Act
            BakeResult result = ItemDatabaseBaker.BakeFromSourceText(itemsJson, recipesJson, constantsJson);

            // Assert
            Assert.That(ReadHeaderConfigVersion(result.ItemsCooked), Is.EqualTo(result.ConfigVersion));
            Assert.That(ReadHeaderConfigVersion(result.RecipesCooked), Is.EqualTo(result.ConfigVersion));
        }

        /// <summary>比对助手:不匹配 ⇒ 非致命(Fatal=false,ADR-010 §七);产物不可读 ⇒ 致命;匹配 ⇒ Match。</summary>
        [Test]
        public void test_configVersion_compareHelper_mismatchNonFatalUnreadableFatal()
        {
            // Act & Assert —— 不匹配非致命
            ConfigVersionComparison mismatch = ConfigVersionUtility.CompareConfigVersion(0xDEADBEEFu, 0xCAFEBABEu);
            Assert.That(mismatch.Match, Is.False);
            Assert.That(mismatch.Fatal, Is.False, "ConfigVersion 不匹配必须非致命(ADR-010 §七,措辞不得改成致命)");

            // 不可读致命
            ConfigVersionComparison unreadable = ConfigVersionUtility.CompareConfigVersion(null, 1u);
            Assert.That(unreadable.Fatal, Is.True);

            // 匹配
            ConfigVersionComparison match = ConfigVersionUtility.CompareConfigVersion(42u, 42u);
            Assert.That(match.Match, Is.True);
            Assert.That(match.Fatal, Is.False);
        }

        // ══════════ 合法对照:种子源全量烘焙通过 ══════════

        /// <summary>三个种子源烘焙 ⇒ 零错误、两产物字节非空、魔数 DYJC、头 ConfigVersion == Derive(三文本)。</summary>
        [Test]
        public void test_legalControl_seedSources_bakeCleanWithValidHeaders()
        {
            // Arrange
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);

            // Act
            BakeResult result = ItemDatabaseBaker.BakeFromSourceText(itemsJson, recipesJson, constantsJson);

            // Assert —— 到达此处即未抛;再钉产物形状
            Assert.That(result.ItemsCooked, Is.Not.Empty);
            Assert.That(result.RecipesCooked, Is.Not.Empty);
            Assert.That(System.Text.Encoding.ASCII.GetString(result.ItemsCooked, 0, 4), Is.EqualTo("DYJC"));
            Assert.That(System.Text.Encoding.ASCII.GetString(result.RecipesCooked, 0, 4), Is.EqualTo("DYJC"));

            uint derived = ConfigVersionUtility.DeriveConfigVersion(
                ItemDatabaseBaker.NamedSources(itemsJson, recipesJson, constantsJson));
            Assert.That(result.ConfigVersion, Is.EqualTo(derived));
            Assert.That(ReadHeaderConfigVersion(result.ItemsCooked), Is.EqualTo(derived));
            Assert.That(ReadHeaderConfigVersion(result.RecipesCooked), Is.EqualTo(derived));
        }

        /// <summary>种子源经 BakeFromRepo 同样通过(仓库路径入口)。</summary>
        [Test]
        public void test_legalControl_bakeFromRepo_clean()
        {
            // Act
            BakeResult result = ItemDatabaseBaker.BakeFromRepo(RepoRoot);

            // Assert
            Assert.That(result.ItemsCooked, Is.Not.Empty);
            Assert.That(result.RecipesCooked, Is.Not.Empty);
        }

        // ══════════ 门接线抽查(绑定成功后由门拒 —— 证明 B2 执行而非只靠白名单)══════════

        /// <summary>stack_max 改 0(结构合法 int)⇒ 门拒并点名 AC-21a-15。</summary>
        [Test]
        public void test_gateWiring_stackMaxZero_bakeFailsCitingAc15()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = itemsJson.Replace("\"stack_max\": 99", "\"stack_max\": 0");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(mutated, recipesJson, constantsJson));
            Assert.That(ex.Message, Does.Contain("AC-21a-15"));
        }

        /// <summary>weight 改 Fix 字符串形(结构放行 raw 交门)⇒ 门拒并点名 AC-21a-15。</summary>
        [Test]
        public void test_gateWiring_weightAsFixString_bakeFailsCitingAc15()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = itemsJson.Replace("\"weight\": 2", "\"weight\": \"1/2\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(mutated, recipesJson, constantsJson));
            Assert.That(ex.Message, Does.Contain("AC-21a-15"));
        }

        /// <summary>eff_max 抬到 3/2(>1)⇒ 守恒门拒并点名 AC-21a-40。</summary>
        [Test]
        public void test_gateWiring_effMaxAboveOne_bakeFailsCitingAc40()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = constantsJson.Replace("\"eff_max\": \"1\"", "\"eff_max\": \"3/2\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(itemsJson, recipesJson, mutated));
            Assert.That(ex.Message, Does.Contain("AC-21a-40"));
        }

        /// <summary>owner 大小写改写(Process)⇒ 分区门拒并点名 AC-21a-66。</summary>
        [Test]
        public void test_gateWiring_ownerCaseChanged_bakeFailsCitingAc66()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = recipesJson.Replace("\"owner\": \"process\"", "\"owner\": \"Process\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(itemsJson, mutated, constantsJson));
            Assert.That(ex.Message, Does.Contain("AC-21a-66"));
        }

        /// <summary>boundary_state 指向未声明通路 ⇒ 门拒并点名 AC-21a-24。</summary>
        [Test]
        public void test_gateWiring_undeclaredBoundary_bakeFailsCitingAc24()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = recipesJson.Replace("\"raw>dried\"", "\"raw>extracted\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(itemsJson, mutated, constantsJson));
            Assert.That(ex.Message, Does.Contain("AC-21a-24"));
        }

        /// <summary>白名单外未知键 ⇒ 绑定层拒并点名「未知键」。</summary>
        [Test]
        public void test_binding_unknownKey_bakeFailsCitingUnknownKey()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = itemsJson.Replace("\"base_id\": \"willow_bark\"", "\"bogus_key\": 1, \"base_id\": \"willow_bark\"");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(mutated, recipesJson, constantsJson));
            Assert.That(ex.Message, Does.Contain("未知键"));
        }

        /// <summary>schema_version 改 99 ⇒ 拒并点名 schema_version(ADR-010 §七 致命面)。</summary>
        [Test]
        public void test_binding_schemaVersionMismatch_bakeFails()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);
            string mutated = recipesJson.Replace("\"schema_version\": 1", "\"schema_version\": 99");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(itemsJson, mutated, constantsJson));
            Assert.That(ex.Message, Does.Contain("schema_version"));
        }
    }
}
