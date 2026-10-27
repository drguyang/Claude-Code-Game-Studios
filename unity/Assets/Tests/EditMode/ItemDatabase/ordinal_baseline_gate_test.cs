// 权威来源:ADR-014 §五「D-21-13 收窄的承接」承接条 ③(ordinal 映射表 append-only 硬门)
//          · ADR-006 §Decision 二 D-21-13(载荷内枚举 = 版本化整数 ordinal,`ItemId` 点名)
//          · assets/data/_ordinal_baseline.json(判据文件)
//
// ⚠️ 本套件覆盖**两半**:
//    段① 表自洽(`ValidateTableOrdinals`)—— 号/键各自唯一、号非负;无基线也跑。
//    段② append-only(`Validate`)—— 对基线不改义 / 不改号 / 不复用。
//    「只测段②」会漏掉生产路径的另一半(夹具注入无基线,但段① 仍须生效)。
//
// ⚠️ **本门买的是静默错读,不是崩溃**:改一个 ordinal 不会有任何异常,只会让旧存档把
//    「麻黄汤」解成「桂枝汤」。⇒ 每条反例都断言**错误报文点名了具体号与具体键**,
//    而不只是「列表非空」——「非空」对任何一道无关的门也成立,是假绿入口。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    /// <summary>ordinal 表自洽 + append-only 门(ADR-014 §五 承接条③)。</summary>
    [TestFixture]
    internal sealed class OrdinalBaselineGateTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string SeedDir = Path.Combine(RepoRoot, "assets", "data");

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        private static OrdinalBaselineGate.Entry E(int ordinal, string key)
            => new OrdinalBaselineGate.Entry(ordinal, key);

        // ══════════ 段① 表自洽(不需要基线)══════════

        /// <summary>合法表(号唯一、键唯一)⇒ 零错误。</summary>
        [Test]
        public void test_tableOrdinals_uniqueAndNonNegative_passes()
        {
            var table = new[] { E(0, "a"), E(1, "b"), E(2, "c") };

            List<string> errors = OrdinalBaselineGate.ValidateTableOrdinals(table, "t");

            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>两个条目抢同一个号 ⇒ 红,且报文点名该号与两个键。</summary>
        [Test]
        public void test_tableOrdinals_duplicateOrdinal_reportsBothKeys()
        {
            var table = new[] { E(0, "a"), E(0, "b") };

            List<string> errors = OrdinalBaselineGate.ValidateTableOrdinals(table, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("0"), "报文须点名冲突的号");
            Assert.That(errors[0], Does.Contain("a"), "报文须点名先占者");
            Assert.That(errors[0], Does.Contain("b"), "报文须点名后到者");
        }

        /// <summary>一个键占两个号 ⇒ 红(反方向唯一性,不复用上面那条的判据)。</summary>
        [Test]
        public void test_tableOrdinals_duplicateKey_reportsBothOrdinals()
        {
            var table = new[] { E(0, "a"), E(1, "a") };

            List<string> errors = OrdinalBaselineGate.ValidateTableOrdinals(table, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("0"));
            Assert.That(errors[0], Does.Contain("1"));
            Assert.That(errors[0], Does.Contain("a"));
        }

        /// <summary>负号(未赋值的默认值)⇒ 红;且**不得**再刷一条重复号噪声。</summary>
        [Test]
        public void test_tableOrdinals_negativeOrdinal_reportsOnceAndDoesNotCascade()
        {
            var table = new[] { E(-1, "a"), E(-1, "b") };

            List<string> errors = OrdinalBaselineGate.ValidateTableOrdinals(table, "t");

            Assert.That(errors.Count, Is.EqualTo(2), "两个条目各一条,不应再叠加重复号噪声:" + string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("-1"));
            Assert.That(errors[1], Does.Contain("-1"));
        }

        /// <summary>空键 ⇒ 红(空键会让 join 出的键变成前缀碰撞)。</summary>
        [Test]
        public void test_tableOrdinals_emptyKey_reportsRed()
        {
            var table = new[] { E(0, ""), E(1, "b") };

            List<string> errors = OrdinalBaselineGate.ValidateTableOrdinals(table, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("键为空"));
        }

        // ══════════ 段② append-only 对基线 ══════════

        /// <summary>合法追加(新键取新号)⇒ 零错误。这是唯一合法的变更形态。</summary>
        [Test]
        public void test_appendOnly_newKeyWithNewOrdinal_passes()
        {
            var baseline = new[] { E(0, "a"), E(1, "b") };
            var current = new[] { E(0, "a"), E(1, "b"), E(2, "c") };

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors, Is.Empty, () => string.Join("\n", errors));
        }

        /// <summary>同号换名(改义)⇒ 红,报文须点名该号、旧名、新名三个要素。</summary>
        [Test]
        public void test_appendOnly_sameOrdinalDifferentKey_reportsRepurposing()
        {
            var baseline = new[] { E(0, "a") };
            var current = new[] { E(0, "b") };

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("0"));
            Assert.That(errors[0], Does.Contain("a"), "报文须点名基线旧名");
            Assert.That(errors[0], Does.Contain("b"), "报文须点名当前新名");
            Assert.That(errors[0], Does.Contain("改义"));
        }

        /// <summary>同键换号(改号)⇒ 红,报文须点名旧号与新号。</summary>
        [Test]
        public void test_appendOnly_sameKeyDifferentOrdinal_reportsRenumber()
        {
            var baseline = new[] { E(0, "a") };
            var current = new[] { E(5, "a") };

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("0"), "报文须点名基线旧号");
            Assert.That(errors[0], Does.Contain("5"), "报文须点名当前新号");
            Assert.That(errors[0], Does.Contain("号变了"));
        }

        /// <summary>条目消失 ⇒ 红(号是永久占用的,弃用不等于释放)。</summary>
        [Test]
        public void test_appendOnly_entryRemoved_reportsDisappearance()
        {
            var baseline = new[] { E(0, "a"), E(1, "b") };
            var current = new[] { E(0, "a") };

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("1"));
            Assert.That(errors[0], Does.Contain("b"));
            Assert.That(errors[0], Does.Contain("消失"));
        }

        /// <summary>整表重排(看起来更整齐)⇒ 红。<b>这是本门存在的核心理由</b>:
        /// 重排不改任何条目的语义,代码审查极难发现,却让每个旧存档静默解错。</summary>
        [Test]
        public void test_appendOnly_tableReordered_reportsEveryMovedEntry()
        {
            var baseline = new[] { E(0, "a"), E(1, "b") };
            var current = new[] { E(0, "b"), E(1, "a") };

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors.Count, Is.EqualTo(2),
                "两条都被换名 ⇒ 两条各报:" + string.Join("\n", errors));
        }

        // ══════════ 基线自身完整性(基线坏了会掩盖真违例)══════════

        /// <summary>基线自身同号重复 ⇒ 红,且**不得**把它当成「表违规」误报。</summary>
        [Test]
        public void test_baseline_duplicateOrdinalInBaselineItself_reportsRed()
        {
            var baseline = new[] { E(0, "a"), E(0, "b") };
            var current = new[] { E(0, "a"), E(1, "b") };

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors.Count, Is.EqualTo(1), () => string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("基线自身"));
        }

        /// <summary>表自洽已红时 ⇒ 短路返回,只报表自身错,**不**级联出一堆假「改义」。</summary>
        [Test]
        public void test_validate_tableBroken_shortCircuitsBeforeBaselineCompare()
        {
            var baseline = new[] { E(0, "a"), E(1, "b") };
            var current = new[] { E(0, "a"), E(0, "b") };   // 表内重号

            List<string> errors = OrdinalBaselineGate.Validate(baseline, current, "t");

            Assert.That(errors.Count, Is.EqualTo(1),
                "表自洽红即返回 —— 否则重号会伪装成一串「改义」噪声:" + string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain("被两个键占用"));
        }

        // ══════════ 真实基线与真实数据表(生产路径的对照面)══════════

        /// <summary>仓库基线可解析,且本表条目在基线里存在。</summary>
        [Test]
        public void test_repoBaseline_parsesAndContainsItemDatabaseRegistry()
        {
            var parseErrors = new List<string>();
            List<OrdinalBaselineRegistry> regs = OrdinalBaselineReader.ReadFromRepo(RepoRoot, parseErrors);

            Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));
            OrdinalBaselineRegistry reg = OrdinalBaselineReader.Find(regs, ItemDatabaseBaker.BaselineRegistryId);
            Assert.That(reg, Is.Not.Null, "基线须含 item_database 注册表");
            Assert.That(reg.Entries.Count, Is.GreaterThan(0), "条目不得为空,否则门无判据");
            Assert.That(reg.KeyFields, Is.EqualTo(new[] { "base_id", "processing_state" }),
                "key_fields 须与生产拼键一致(改它 = 让既有基线的键全部对不上)");
        }

        /// <summary><b>正向对照</b>:真实数据表对真实基线 ⇒ 零错误。
        /// <para>这条同时守着「基线与数据表同步」—— 任何一边单独改动都会让它变红。</para></summary>
        [Test]
        public void test_repoData_baselineMatchesCurrentTable_exactly()
        {
            var parseErrors = new List<string>();
            OrdinalBaselineRegistry reg = OrdinalBaselineReader.Find(
                OrdinalBaselineReader.ReadFromRepo(RepoRoot, parseErrors), ItemDatabaseBaker.BaselineRegistryId);
            Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));
            Assert.That(reg, Is.Not.Null);

            // 从生产链路取当前表(现烘,不读盘上产物 —— 漂移门另管那一面)
            Assert.DoesNotThrow(() => ItemDatabaseBaker.BakeFromRepo(RepoRoot),
                "生产路径烘焙须全程零错误(段① 表自洽 + 段② append-only 均已过)");
        }

        /// <summary><b>反向证伪</b>:把数据表里两个条目的 ordinal 互换 ⇒ 段② 必红。
        /// <para>⚠️ 这条**必须**走 <c>BakeFromSourceText(baseline: …)</c> 而不是只调门 ——
        /// 它同时证明「baker 真的把基线传给了门」。只调门的测试无法发现「接线漏了」。</para></summary>
        [Test]
        public void test_falsification_swapOrdinalsInSource_bakeFailsCitingRepurposing()
        {
            string itemsJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.ItemsFileName));
            string recipesJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.RecipesFileName));
            string constantsJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.ConstantsFileName));

            // 把 willow_bark/raw(ordinal 0)与 willow_bark/dried(ordinal 1)的号互换。
            // ⚠️ 每一步都先自证源文本里该串**唯一**,否则 Replace 会打错靶(同值行很多:
            //    stack_max / weight / qty_per_node 都可能是 0 或 1)。
            //    用 \u0000 哨兵做三路替换,避免「先改 0→1 再改 1→0」把两处都改成 0。
            StringAssert.Contains("\"ordinal\": 0,", itemsJson, "夹具自证:ordinal 0 须存在");
            StringAssert.Contains("\"ordinal\": 1,", itemsJson, "夹具自证:ordinal 1 须存在");
            string mutated = itemsJson
                .Replace("\"ordinal\": 0,", "\"ordinal\": \u0000SWAP\u0000,")
                .Replace("\"ordinal\": 1,", "\"ordinal\": 0,")
                .Replace("\"ordinal\": \u0000SWAP\u0000,", "\"ordinal\": 1,");
            Assert.That(mutated, Is.Not.EqualTo(itemsJson), "变异须真的改动源文本(否则本条无判别力)");

            var parseErrors = new List<string>();
            OrdinalBaselineRegistry baseline = OrdinalBaselineReader.Find(
                OrdinalBaselineReader.ReadFromRepo(RepoRoot, parseErrors),
                ItemDatabaseBaker.BaselineRegistryId);
            Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(mutated, recipesJson, constantsJson, null, baseline));

            Assert.That(ex.Message, Does.Contain("改义"), "须点名改义,而不只是任意一条无关错误");
        }

        /// <summary><b>反向证伪</b>:生产路径的基线缺失 ⇒ 硬失败(不是「门静默不跑」)。
        /// <para>造一个只有三份数据、没有基线的临时仓库根,调 <c>BakeFromRepo</c>。
        /// 这是本条门最该被钉住的形态 —— 「文件不在 ⇒ 全绿」正是要消灭的失败模式。</para></summary>
        [Test]
        public void test_falsification_baselineFileMissing_bakeFromRepoFailsLoudly()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "ordinal_baseline_missing_" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);
            try
            {
                foreach (string name in new[] {
                    ItemDatabaseBaker.ItemsFileName,
                    ItemDatabaseBaker.RecipesFileName,
                    ItemDatabaseBaker.ConstantsFileName })
                {
                    File.Copy(Path.Combine(SeedDir, name), Path.Combine(dataDir, name));
                }

                BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                    ItemDatabaseBaker.BakeFromRepo(tempRoot));

                Assert.That(ex.Message, Does.Contain(OrdinalBaselineReader.FileName),
                    "报文须点名缺失的基线文件名 —— 否则维护者不知道该建什么");
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }

        /// <summary><b>反向证伪</b>:夹具注入路径(无基线)仍须跑段① ——
        /// 合成表里重号必须红,不得因「没基线」而整条跳过。</summary>
        [Test]
        public void test_falsification_duplicateOrdinalWithoutBaseline_stillFailsViaTableGate()
        {
            string itemsJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.ItemsFileName));
            string recipesJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.RecipesFileName));
            string constantsJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.ConstantsFileName));

            // 把最后一条(ordinal 8,wallen_post)的号改成 0 ⇒ 表内重号。
            // ⚠️ 用 ordinal 8 而不是 1:`8` 在源里**唯一次**,而 `1` 会连带命中 stack_max /
            //    weight / qty_per_node 等一堆同值行(变异打错靶 ⇒ 断言在测别的东西)。
            StringAssert.Contains("\"ordinal\": 8,", itemsJson, "夹具自证:ordinal 8 须唯一次");
            string mutated = itemsJson.Replace("\"ordinal\": 8,", "\"ordinal\": 0,");
            Assert.That(mutated, Is.Not.EqualTo(itemsJson), "变异须真的改动源文本");

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(mutated, recipesJson, constantsJson));

            Assert.That(ex.Message, Does.Contain("被两个键占用"),
                "无基线的夹具路径仍须跑段①(表自洽),不得整条跳过");
        }

        // ══════════ 配方表那一半(2026-10-27 · R15 根因修复)══════════
        //
        // 物品表的两半早已有门,配方表此前**一条都没有** —— 而 CraftPayload.recipe_id
        // 的注释自 ADR-014 §五 起就自称「21a 表 ordinal」,却与全仓无映射。
        // 下面四条钉住:① 基线里真有这张表;② 生产数据对得上;③ 改号必红;
        // ④ 缺这份基线同样硬失败(不因「新加的」而降级为跳过)。

        /// <summary>基线里须有 <c>recipe_database</c> 注册表,且 key_fields = ["recipe_id"]。</summary>
        [Test]
        public void test_repoBaseline_containsRecipeDatabaseRegistry()
        {
            var parseErrors = new List<string>();
            List<OrdinalBaselineRegistry> regs = OrdinalBaselineReader.ReadFromRepo(RepoRoot, parseErrors);
            Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));

            OrdinalBaselineRegistry reg = OrdinalBaselineReader.Find(
                regs, ItemDatabaseBaker.RecipeBaselineRegistryId);
            Assert.That(reg, Is.Not.Null, "基线须含 recipe_database 注册表(否则配方改号静默通过)");
            Assert.That(reg.Entries.Count, Is.GreaterThan(0), "条目不得为空,否则门无判据");
            Assert.That(reg.KeyFields, Is.EqualTo(new[] { "recipe_id" }),
                "key_fields 须与生产拼键一致(配方表的键是单段 recipe_id)");
        }

        /// <summary><b>正向对照</b>:真实配方表对真实基线 ⇒ 零错误。</summary>
        [Test]
        public void test_repoRecipes_baselineMatchesCurrentTable_exactly()
        {
            var parseErrors = new List<string>();
            OrdinalBaselineRegistry reg = OrdinalBaselineReader.Find(
                OrdinalBaselineReader.ReadFromRepo(RepoRoot, parseErrors),
                ItemDatabaseBaker.RecipeBaselineRegistryId);
            Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));
            Assert.That(reg, Is.Not.Null);

            Assert.DoesNotThrow(() => ItemDatabaseBaker.BakeFromRepo(RepoRoot),
                "生产路径烘焙须全程零错误(配方表段① + 段② 均已过)");
        }

        /// <summary><b>反向证伪</b>:配方 ordinal 互换 ⇒ 必红,且报文点名「改义」。
        /// <para>⚠️ 必须走 <c>BakeFromSourceText(recipeBaseline: …)</c> —— 只调门发现不了
        /// 「baker 忘了把基线传给门」这层接线漏。</para></summary>
        [Test]
        public void test_falsification_swapRecipeOrdinals_bakeFailsCitingRepurposing()
        {
            string itemsJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.ItemsFileName));
            string recipesJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.RecipesFileName));
            string constantsJson = File.ReadAllText(Path.Combine(SeedDir, ItemDatabaseBaker.ConstantsFileName));

            // dry_willow_bark(0) ⟷ extract_salicylic(1)。
            // ⚠️ 三路替换带 \u0000 哨兵 —— 与物品表那条同款,防「先改 0→1 再改 1→0」双改成 0。
            StringAssert.Contains("\"ordinal\": 0,", recipesJson, "夹具自证:ordinal 0 须存在");
            StringAssert.Contains("\"ordinal\": 1,", recipesJson, "夹具自证:ordinal 1 须存在");
            string mutated = recipesJson
                .Replace("\"ordinal\": 0,", "\"ordinal\": \u0000SWAP\u0000,")
                .Replace("\"ordinal\": 1,", "\"ordinal\": 0,")
                .Replace("\"ordinal\": \u0000SWAP\u0000,", "\"ordinal\": 1,");
            Assert.That(mutated, Is.Not.EqualTo(recipesJson), "变异须真的改动源文本(否则本条无判别力)");

            var parseErrors = new List<string>();
            OrdinalBaselineRegistry recipeBaseline = OrdinalBaselineReader.Find(
                OrdinalBaselineReader.ReadFromRepo(RepoRoot, parseErrors),
                ItemDatabaseBaker.RecipeBaselineRegistryId);
            Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));
            Assert.That(recipeBaseline, Is.Not.Null);

            BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                ItemDatabaseBaker.BakeFromSourceText(
                    itemsJson, mutated, constantsJson, null, null, recipeBaseline));

            Assert.That(ex.Message, Does.Contain("改义"), "须点名改义,而不只是任意一条无关错误");
            Assert.That(ex.Message, Does.Contain(ItemDatabaseBaker.RecipeBaselineRegistryId),
                "报文须点名 recipe_database —— 否则与物品表那道的红无法区分");
        }

        /// <summary><b>反向证伪</b>:临时仓库里三份数据齐、基线缺 recipe_database 那一条 ⇒ 硬失败。
        /// <para>与「基线文件整个缺失」不同:文件在、只是少了一个注册表 —— 这正是
        /// 「门静默跳过新表」最可能的形态。</para></summary>
        [Test]
        public void test_falsification_recipeRegistryMissingFromBaseline_bakeFromRepoFailsLoudly()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(),
                "recipe_registry_missing_" + Guid.NewGuid().ToString("N"));
            string dataDir = Path.Combine(tempRoot, "assets", "data");
            Directory.CreateDirectory(dataDir);
            try
            {
                foreach (string name in new[] {
                    ItemDatabaseBaker.ItemsFileName,
                    ItemDatabaseBaker.RecipesFileName,
                    ItemDatabaseBaker.ConstantsFileName })
                {
                    File.Copy(Path.Combine(SeedDir, name), Path.Combine(dataDir, name));
                }

                // 基线:保留 item_database,砍掉 recipe_database 那一段。
                string baselineJson = File.ReadAllText(
                    Path.Combine(SeedDir, OrdinalBaselineReader.FileName));
                int cut = baselineJson.IndexOf("\"id\": \"recipe_database\"", StringComparison.Ordinal);
                Assert.That(cut, Is.GreaterThan(0), "夹具自证:基线里须能定位到 recipe_database 段");
                // 回退到该段前面的 "{",再删到条目数组闭合处 —— 用解析结果重建更稳,避免手数括号。
                var parseErrors = new List<string>();
                List<OrdinalBaselineRegistry> regs =
                    OrdinalBaselineReader.Parse(baselineJson, parseErrors);
                Assert.That(parseErrors, Is.Empty, () => string.Join("\n", parseErrors));
                Assert.That(OrdinalBaselineReader.Find(regs, ItemDatabaseBaker.RecipeBaselineRegistryId),
                    Is.Not.Null, "夹具自证:原基线里须有 recipe_database");

                // 重建一份「只有 item_database」的基线(经同一 Reader 的 Parse 反向验证形状)。
                OrdinalBaselineRegistry items = OrdinalBaselineReader.Find(
                    regs, ItemDatabaseBaker.BaselineRegistryId);
                Assert.That(items, Is.Not.Null);
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"schema_version\":1,\"_note\":\"fixture\",\"registries\":[");
                sb.Append("{\"id\":\"item_database\",\"source\":\"")
                  .Append(items.Source).Append("\",\"field\":\"ordinal\",\"key_fields\":[\"base_id\",\"processing_state\"],")
                  .Append("\"note\":\"fixture\",\"entries\":[");
                for (int i = 0; i < items.Entries.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    string key = items.Entries[i].Key;
                    string[] parts = key.Split(OrdinalBaselineReader.KeySeparator);
                    sb.Append("{\"ordinal\":").Append(items.Entries[i].Ordinal)
                      .Append(",\"base_id\":\"").Append(parts[0])
                      .Append("\",\"processing_state\":\"").Append(parts[1]).Append("\"}");
                }
                sb.Append("]}]}");
                File.WriteAllText(Path.Combine(dataDir, OrdinalBaselineReader.FileName), sb.ToString());

                BakeValidationException ex = Assert.Throws<BakeValidationException>(() =>
                    ItemDatabaseBaker.BakeFromRepo(tempRoot));

                Assert.That(ex.Message, Does.Contain(ItemDatabaseBaker.RecipeBaselineRegistryId),
                    "报文须点名缺失的注册表 id —— 「反正是新加的所以跳过」正是要消灭的形态");
            }
            finally
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }
    }
}
