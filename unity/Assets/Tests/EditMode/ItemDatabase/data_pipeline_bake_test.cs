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

        // ══════════ 产物漂移门(改源必重烘)══════════
        //
        // 权威来源:ADR-014 两阶段烘焙(`*.cooked` 是**入 git 的出货产物**)
        //         · 先例 = `Boot/examine_driver_test.cs:92-108` 对 diagnosis_* 两份的同款断言
        //
        // ⚠️ **本条门的由来(2026-10-20,实证发现)**:
        //   本套件其余全部测试都走 `BakeFromSourceText` / `BakeFromRepo`(**现烘**),
        //   没有一条读 `unity/Assets/DataCooked/` 下那份**已提交的**字节 ⇒
        //   **改了源 JSON 而忘记重烘,全量 EditMode 照样全绿**。
        //   该缺口在 S-2(processing/story-001 AC-18-18 补口)被实测暴露:
        //   当时盘上 `item_database_*.cooked.bytes` 是**改动前**烘的陈旧产物,
        //   而全量套件 `3951/3915/0/36` 全绿。⇒ 漂移通道无门覆盖 = 借绿家族的
        //   「该断言的通道没被覆盖」形态。
        //
        // 判据:baker 现烘输出 == 盘上已提交产物(**逐字节**)。不等 ⇒ 红,报文点名该重烘。
        // 覆盖:items + recipes 两份;它们共用同一 `ConfigVersion`(同一命名源集)。

        /// <summary>盘上 <c>Assets/DataCooked/</c> 下某产物的绝对路径。</summary>
        private static string CookedPath(string assetName)
            => Path.Combine(UnityAssetsRoot, "DataCooked", assetName);

        /// <summary>
        /// <b>产物漂移门 · items</b> —— 库内 <c>item_database_items.cooked.bytes</c>
        /// 须 = baker 现烘输出。改源不重烘 ⇒ 红。
        /// </summary>
        [Test]
        public void test_drift_itemsCooked_matchesBakerFreshOutput()
        {
            const string assetName = "item_database_items.cooked.bytes";

            BakeResult fresh = ItemDatabaseBaker.BakeFromRepo(RepoRoot);
            byte[] onDisk = File.ReadAllBytes(CookedPath(assetName));

            Assert.That(onDisk, Is.EqualTo(fresh.ItemsCooked),
                $"库内 {assetName} ≠ baker 现烘输出 —— 改了 assets/data/item_database_*.json 却未重烘。" +
                "重烘:unity build <unity/> --target StandaloneLinux64 " +
                "--execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeItemDatabase" +
                "(`*.cooked` 是入 git 的出货产物,ADR-014;漂移 = 运行期读陈旧数据而测试全绿)");
        }

        /// <summary>
        /// <b>产物漂移门 · recipes</b> —— 对称半边。
        /// </summary>
        [Test]
        public void test_drift_recipesCooked_matchesBakerFreshOutput()
        {
            const string assetName = "item_database_recipes.cooked.bytes";

            BakeResult fresh = ItemDatabaseBaker.BakeFromRepo(RepoRoot);
            byte[] onDisk = File.ReadAllBytes(CookedPath(assetName));

            Assert.That(onDisk, Is.EqualTo(fresh.RecipesCooked),
                $"库内 {assetName} ≠ baker 现烘输出 —— 改了源 JSON 却未重烘(见 items 侧报文的重烘命令)");
        }

        /// <summary>
        /// 两份产物头的 <c>ConfigVersion</c> 须彼此一致,且 = 由现源复算的值。
        /// <para>⚠️ 本测**不是**漂移门的替代 —— 它查的是「两产物内部一致 + 与源派生值一致」,
        /// 但**无法**发现「两者一起陈旧」(两份同源同旧 ⇒ 头值仍互相一致)。
        /// 漂移只能由上面两条**逐字节比对**兜住。</para>
        /// </summary>
        [Test]
        public void test_drift_configVersion_onDisk_matchesFreshlyDerived()
        {
            BakeResult fresh = ItemDatabaseBaker.BakeFromRepo(RepoRoot);

            byte[] itemsOnDisk = File.ReadAllBytes(CookedPath("item_database_items.cooked.bytes"));
            byte[] recipesOnDisk = File.ReadAllBytes(CookedPath("item_database_recipes.cooked.bytes"));

            Assert.That(ReadHeaderConfigVersion(itemsOnDisk), Is.EqualTo(fresh.ConfigVersion),
                "盘上 items 产物头的 ConfigVersion ≠ 由现源派生的值");
            Assert.That(ReadHeaderConfigVersion(recipesOnDisk), Is.EqualTo(fresh.ConfigVersion),
                "盘上 recipes 产物头的 ConfigVersion ≠ 由现源派生的值");
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

        /// <summary>schema_version 改成错误值 ⇒ 拒并点名 schema_version(ADR-010 §七 致命面)。</summary>
        /// <remarks>
        /// ⚠️ <b>2026-10-27 订正 —— 本测曾因写死字面量而失去判别力</b>:原实现写
        /// <c>Replace("\"schema_version\": 1", …)</c>,把「正确值」硬编码成 1。
        /// recipes 的 schema_version 抬到 2 后 <c>Replace</c> **匹配不到任何东西**,
        /// 源文本原样通过 ⇒ 断言「必抛」当场红。它今天红是因为方向是「必抛」;
        /// 若同样的写法用在「必不抛」上,就会**无声地绿**。
        /// ⇒ 正确值一律从 <see cref="DaYiJingCheng.Gameplay.Presentation.CookedCodec.ExpectedRecipeSchemaVersion"/>
        /// 派生,并先断言「种子与常量一致」,使前提失效时红在**前提**而不是红在断言。
        /// </remarks>
        [Test]
        public void test_binding_schemaVersionMismatch_bakeFails()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);

            string current = "\"schema_version\": " +
                DaYiJingCheng.Gameplay.Presentation.CookedCodec.ExpectedRecipeSchemaVersion;
            Assert.That(recipesJson, Does.Contain(current),
                "种子 recipes 的 schema_version 与读方常量不一致 —— 本测前提失效;" +
                "先对齐种子,不要把断言放宽成「随便改改就红」");

            string mutated = recipesJson.Replace(current, "\"schema_version\": 99");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(itemsJson, mutated, constantsJson));
            Assert.That(ex.Message, Does.Contain("schema_version"));
        }

        /// <summary><b>Recipe.Ordinal 的烘焙往返</b>:写到 cooked 里的号读回来必须逐条等。
        /// <para>⚠️ 本条补的是一个真实的**判据空洞**:<c>Recipe.Ordinal</c> 于 2026-10-27 加入
        /// (InventoryFold R15 的映射键),但全仓 <c>ReadRecipeDataSet</c> 的测试调用点**为零**
        /// ⇒ 写端 <c>CookedWriter.WriteRecipe</c> 与读端 <c>CookedCodec.ReadRecipe</c>
        /// 的字段序此前**没有任何东西钉住**。两边的 int 位宽一样,写读错位**不会崩**,
        /// 只会静默换值 —— 与 ADR-014 承接条 ③ 要消灭的是同一类失败。</para>
        /// <para>判据不是「往返非空」,而是**逐条等号 + 与源 JSON 的 ordinal 一致 +
        /// 表内互不相同**(后者防「读端恒写 0」这种全零仍能自洽的形态)。</para>
        /// </summary>
        [Test]
        public void test_recipeOrdinal_survivesCookedRoundTrip()
        {
            ReadLegalTriple(out string itemsJson, out string recipesJson, out string constantsJson);

            BakeResult baked = ItemDatabaseBaker.BakeFromSourceText(itemsJson, recipesJson, constantsJson);
            // ⚠️ RecipeDataSet 住 DaYiJingCheng.Sim(unity/Assets/Sim/ItemDatabase/RecipeDataSet.cs:9),
            //    **不在** Sim.Contracts —— 它含 Settlement 常量,按 ADR-025 §① 属 Sim 侧。
            DaYiJingCheng.Sim.RecipeDataSet decoded =
                DaYiJingCheng.Gameplay.Presentation.CookedCodec.ReadRecipeDataSet(baked.RecipesCooked);

            Assert.That(decoded.Recipes, Is.Not.Null.And.Length.GreaterThan(0),
                "空配方集会让下面对 count 的断言平凡通过");

            // ① 与源 JSON 的 ordinal 逐条等(顺序 = 源文件行序,写端不重排)。
            //    解析走 JsonStage1Lexer —— **与烘焙门同一套词法器**(装配内 internal,经
            //    Editor.Tools.Bake/AssemblyInfo.cs 的 InternalsVisibleTo 可见),不另抄解析器。
            Assert.That(JsonStage1Lexer.TryParse(recipesJson, out JsonNode srcRoot, out string lexErr), Is.True,
                $"源词法失败:{lexErr}");
            Assert.That(srcRoot.TryGet("recipes", out JsonNode srcRows) && srcRows?.Items != null, Is.True,
                "源缺 recipes[] —— 本测前提失效");

            List<int> sourceOrdinals = new List<int>();
            foreach (JsonNode row in srcRows.Items)
            {
                Assert.That(row.TryGet("ordinal", out JsonNode ordNode) && ordNode.Kind == JsonNodeKind.Integer,
                    Is.True, "源每条配方须有整数 ordinal 字段");
                sourceOrdinals.Add((int)ordNode.Int);
            }

            Assert.That(sourceOrdinals.Count, Is.EqualTo(decoded.Recipes.Length),
                "源与产物的条目数须相等");
            for (int i = 0; i < sourceOrdinals.Count; i++)
                Assert.That(decoded.Recipes[i].Ordinal, Is.EqualTo(sourceOrdinals[i]),
                    $"第 {i} 条({decoded.Recipes[i].RecipeId})的 ordinal 在 cooked 往返后变了 —— " +
                    "写读字段序不一致或漏写");

            // ② 表内互不相同 —— 这一条才挡得住「读端恒返回 0」:全零也能让 ① 在
            //    源也全零时通过,但那样表自洽门早该红;这里独立再挡一次。
            var seen = new HashSet<int>();
            foreach (DaYiJingCheng.Sim.Contracts.Recipe r in decoded.Recipes)
                Assert.That(seen.Add(r.Ordinal), Is.True,
                    "ordinal 在产物里重复 ⇒ 载荷侧 CraftPayload.RecipeId 无法唯一定位配方");
        }
    }
}
