// 权威来源:
//   Story 001(production/epics/processing/story-001-recipe-subset-and-admissible-predicate.md)
//     · AC-18-18 —— 三子集 {process, craft, build} 的 **非空 / 并 = 全表 / 两两交 = ∅**
//     · 2026-10-20 订正(S-2):原判据「并 = 全表、两两交 = ∅」在空子集下**平凡为真**
//       ⇒ 增「非空前置」并拆「机制可证 / 真表覆盖度」两段记分
//   GDD:design/gdd/item-database.md §规则五(:152-159) · §Schema F(`owner` 字段表 :417)
//     · AC-21a-66(:1224,21a 侧断同一判据的装载期半边)
//   ADR-014 §Decision(构建期硬失败;本文件的装载期断言经 `ItemDatabaseBaker.BakeFromSourceText`)
//
// ⚠️ 落点:story 头登记的账本路径 = `unity/Assets/Tests/EditMode/Processing/recipe_owner_partition_test.cs`
//    (未登记账本别名 —— 本文件即真身)。Unity 只编译 `unity/Assets/` 树。
//
// ⚠️ **本文件为什么要存在(S-2 的病根)**:
//    `RecipeValidationGates.PartitionRecipeOwners` 的实现是 `if / else if / else` 三分支链 ——
//    **每条记录恰入一个子集**由**代码结构**保证,与数据无关 ⇒「并 = 全表」「两两交 = ∅」
//    这两条**无论怎么喂都恒真**。既有的 `test_ownerPartition_threeSubsets_unionEqualsFullTablePairwiseDisjoint`
//    测的是**算法自洽性**,不是 AC 的**数据覆盖**语义。
//    ⇒ 本文件补的正是那半边缺失的语义:**子集非空**(数据必须真的落在三个桶里),
//    以及**真表覆盖度**的独立断言(与合成夹具分开记分,禁合并成一条绿)。
//
// ⚠️ 数值纪律:本文件**零调参旋钮字面量**(AC-21a-48 同族)—— 常量一律取自
//    `recipes` JSON 的 `skill_gate` / `min_quality` / `duration_ticks` 原值,
//    或由测试自身构造的夹具值(明确标注「夹具值」)。
// ⚠️ 零 UnityEngine / 零随机种子 / 零时间依赖。唯一外部 I/O = 读真表三件(与
//    `data_pipeline_bake_test.cs` 同款先例:真种子是**被测对象**的一部分,不是环境依赖)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.EditMode.Processing
{
    /// <summary>
    /// AC-18-18 —— 配方三子集 `{process, craft, build}` 的覆盖判据。
    /// <para><b>三段判据</b>(缺一不可):① 非空 ② 并 = 全表 ③ 两两交 = ∅。</para>
    /// <para>⚠️ ①②③ 中只有 ① 有判别力 —— ②③ 由 <c>PartitionRecipeOwners</c> 的
    /// <c>if/else</c> 结构恒真。本夹具**保留** ②③ 作回归护栏(防实现被改成
    /// 「按 processing_state 猜归属」—— 那会立刻打破两两交),但**不把它们当作 AC 的主判据**。</para>
    /// </summary>
    [TestFixture]
    internal sealed class RecipeOwnerPartitionTest
    {
        // ══════════════ 三个 owner 字面量(闭集,与 RecipeOwner 枚举同序数)══════════════

        private const string ProcessLiteral = "process";
        private const string CraftLiteral = "craft";
        private const string BuildLiteral = "build";

        private static readonly string[] AllOwners =
            { ProcessLiteral, CraftLiteral, BuildLiteral };

        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string SeedDir = Path.Combine(RepoRoot, "assets", "data");

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        private static string ReadSeed(string fileName)
            => File.ReadAllText(Path.Combine(SeedDir, fileName));

        // ══════════════ 从作者态 JSON 抽取 owner 序列(零 Newtonsoft —— 与既有套件同款手写抽取)══════════════

        /// <summary>
        /// 从 <c>recipes</c> JSON 原文按**出现顺序**抽出全部 <c>"owner": "..."</c> 字面量。
        /// <para>⚠️ 只认紧跟在 <c>recipe_id</c> 之后的 owner 行 —— 本表的行序固定为
        /// <c>recipe_id</c> → <c>owner</c>(见真表),故按出现序即可与 recipes 数组下标对齐。
        /// 若将来行序变动,本方法**会静默错位** ⇒ 配套的
        /// <see cref="test_realSeed_ownerCountMatchesRecipeCount"/> 用「owner 条数 = 配方条数」
        /// 兜住这一假设,失效即红。</para>
        /// </summary>
        private static List<string> ExtractOwnerLiterals(string recipesJson)
        {
            var owners = new List<string>();
            const string marker = "\"owner\"";
            int cursor = 0;
            while (true)
            {
                int at = recipesJson.IndexOf(marker, cursor, StringComparison.Ordinal);
                if (at < 0) break;

                int colon = recipesJson.IndexOf(':', at);
                int firstQuote = recipesJson.IndexOf('"', colon + 1);
                int secondQuote = recipesJson.IndexOf('"', firstQuote + 1);
                if (colon < 0 || firstQuote < 0 || secondQuote < 0)
                    throw new InvalidDataException("recipes JSON 的 owner 字段形态非法(缺引号)");

                owners.Add(recipesJson.Substring(firstQuote + 1, secondQuote - firstQuote - 1));
                cursor = secondQuote + 1;
            }
            return owners;
        }

        /// <summary>数 <c>"recipe_id"</c> 出现次数 = 配方条数(用于交叉校验 owner 抽取完整性)。</summary>
        private static int CountRecipeIds(string recipesJson)
        {
            const string marker = "\"recipe_id\"";
            int count = 0, cursor = 0;
            while (true)
            {
                int at = recipesJson.IndexOf(marker, cursor, StringComparison.Ordinal);
                if (at < 0) return count;
                count++;
                cursor = at + marker.Length;
            }
        }

        // ══════════════ 三段判据的公共断言体 ══════════════

        /// <summary>
        /// 对一组 owner 字面量跑**三段判据**,返回已划分好的三个下标集。
        /// <para>⚠️ 本方法是本夹具「三段」的**唯一定义处** —— 合成夹具与真表两条路径共用它,
        /// 避免「合成走一套、真表走另一套」的分叉(那正是 AC 语义层被顶替的经典形态)。</para>
        /// </summary>
        private static void AssertThreePartition(
            IReadOnlyList<string> ownerLiterals, string context)
        {
            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                ownerLiterals,
                out IReadOnlyList<int> process,
                out IReadOnlyList<int> craft,
                out IReadOnlyList<int> build);

            Assert.That(errors, Is.Empty,
                $"{context}: 全字面量合法 ⇒ 划分零错误;实测 {string.Join(" | ", errors)}");

            // ── ① 非空前置(S-2 增补:本段是**唯一**有判别力的一段)──
            foreach (string owner in AllOwners)
            {
                IReadOnlyList<int> subset = owner == ProcessLiteral ? process
                    : owner == CraftLiteral ? craft : build;

                Assert.That(subset.Count, Is.GreaterThan(0),
                    $"{context}: `{owner}` 子集为空 ⇒ 并/交判据无判别力 " +
                    "(并 = 全表在空集下恒真)。数据表须至少含 1 条该 owner 的配方 —— " +
                    "这正是 AC-18-18 从「算法自洽」升级为「数据覆盖」的那一条。");
            }

            // ── ② 并 = 全表(按**下标集合**逐项比,禁只比基数)──
            var union = new HashSet<int>(process);
            union.UnionWith(craft);
            union.UnionWith(build);

            var expected = new HashSet<int>();
            for (int i = 0; i < ownerLiterals.Count; i++) expected.Add(i);

            // ⚠️ 用 SetEquals 而非 Count 比较:基数相等 + 越界下标也能骗过 Count 形式的断言。
            Assert.That(union.SetEquals(expected), Is.True,
                $"{context}: 三子集之并 ≠ 全表 —— 缺 [{string.Join(",", Missing(expected, union))}]");

            // ── ③ 两两交 = ∅ ──
            AssertDisjoint(process, craft, context);
            AssertDisjoint(process, build, context);
            AssertDisjoint(craft, build, context);
        }

        private static IEnumerable<int> Missing(HashSet<int> expected, HashSet<int> actual)
        {
            foreach (int i in expected)
                if (!actual.Contains(i)) yield return i;
        }

        private static void AssertDisjoint(
            IReadOnlyList<int> a, IReadOnlyList<int> b, string context)
        {
            var set = new HashSet<int>(a);
            var overlap = new List<int>();
            foreach (int i in b)
                if (set.Contains(i)) overlap.Add(i);

            Assert.That(overlap, Is.Empty,
                $"{context}: 两子集相交于下标 [{string.Join(",", overlap)}] —— " +
                "每条配方必须恰归一个 owner 子集(GDD 规则五 `owner` 是子集划分**唯一**判据)");
        }

        // ══════════════ ① 合成夹具:机制可证(与真表分离记分)══════════════

        /// <summary>
        /// 合成夹具 —— 每个子集恰 1 条 ⇒ 三段全过。
        /// <para>⚠️ <b>合成数据,不证明真表已覆盖</b>。本测只证「判据机制能跑且能判非空」,
        /// 真表覆盖度由 <see cref="test_realSeed_allThreeOwnerSubsetsNonEmpty"/> 独立断言。</para>
        /// </summary>
        [Test]
        public void test_synthetic_onePerSubset_threePartitionPasses()
        {
            var owners = new[] { ProcessLiteral, CraftLiteral, BuildLiteral };
            AssertThreePartition(owners, "合成夹具(每子集 1 条)");
        }

        /// <summary>
        /// 合成夹具 —— 侧重分布(process×2, craft×1, build×2)仍成立。
        /// 用来钉住「非空判据不是『必须各恰 1 条』的误读」。
        /// </summary>
        [Test]
        public void test_synthetic_skewedDistribution_threePartitionPasses()
        {
            var owners = new[]
            {
                ProcessLiteral, CraftLiteral, BuildLiteral, ProcessLiteral, BuildLiteral,
            };
            AssertThreePartition(owners, "合成夹具(偏斜分布)");
        }

        // ══════════════ ① 的**负向**半边:空子集必须被判出 ══════════════

        /// <summary>
        /// <b>本夹具的核心判别力来源</b> —— 只喂 <c>process</c> 时,`craft` / `build` 为空,
        /// 三段判据的**第 ① 段必须失败**。
        /// <para>⚠️ 而若**只有** ②③ 两段,这组输入会**全绿**(并 = 全表 ✅,两两交 = ∅ ✅)
        /// —— 这正是 S-2 描述的「空集借绿」。本测钉住「① 段确实在起作用」。</para>
        /// <para>实现上不能直接调 <see cref="AssertThreePartition"/>(它会 Assert.Fail),
        /// 故此处**手工复算**①,并同时断言 ②③ 在该输入下**确实恒真** —— 把「为什么需要 ①」写进断言。</para>
        /// </summary>
        [Test]
        public void test_negative_onlyProcess_unionAndDisjointHoldButNonEmptyFails()
        {
            var owners = new[] { ProcessLiteral, ProcessLiteral };

            IReadOnlyList<string> errors = RecipeValidationGates.PartitionRecipeOwners(
                owners, out IReadOnlyList<int> process,
                out IReadOnlyList<int> craft, out IReadOnlyList<int> build);

            Assert.That(errors, Is.Empty, "字面量合法 ⇒ 划分器本身不报错");

            // ② ② 恒真 —— 证明「只有并/交两段挡不住空子集」
            var union = new HashSet<int>(process);
            union.UnionWith(craft);
            union.UnionWith(build);
            Assert.That(union.Count, Is.EqualTo(owners.Length),
                "反例自证:全 process 时『并 = 全表』**依然成立** ⇒ 该段无判别力");
            Assert.That(intersect(process, craft), Is.Empty, "反例自证:两两交**依然** = ∅");
            Assert.That(intersect(process, build), Is.Empty, "反例自证:两两交**依然** = ∅");

            // ① 是唯一能判出问题的段
            Assert.That(craft.Count, Is.EqualTo(0), "craft 空");
            Assert.That(build.Count, Is.EqualTo(0), "build 空");
            Assert.That(
                craft.Count > 0 && build.Count > 0, Is.False,
                "非空前置在此输入下必然失败 —— 若某天它通过了,说明划分器被改坏");
        }

        private static List<int> intersect(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            var set = new HashSet<int>(a);
            var both = new List<int>();
            foreach (int i in b)
                if (set.Contains(i)) both.Add(i);
            return both;
        }

        // ══════════════ ② 真表覆盖度(独立记分 —— 不得与上组合并成一条绿)══════════════

        /// <summary>
        /// owner 抽取完整性 —— <c>"owner"</c> 条数必须 = <c>"recipe_id"</c> 条数。
        /// <para>这是 <see cref="ExtractOwnerLiterals"/> 「按出现序对齐下标」这一假设的守卫:
        /// 一旦行序变动或有配方漏写 owner,本测先红,避免下游按错位下标得出假结论。</para>
        /// </summary>
        [Test]
        public void test_realSeed_ownerCountMatchesRecipeCount()
        {
            string recipesJson = ReadSeed(ItemDatabaseBaker.RecipesFileName);

            int ownerCount = ExtractOwnerLiterals(recipesJson).Count;
            int recipeCount = CountRecipeIds(recipesJson);

            Assert.That(recipeCount, Is.GreaterThan(0), "真表须至少 1 条配方");
            Assert.That(ownerCount, Is.EqualTo(recipeCount),
                $"owner 字段条数({ownerCount}) ≠ 配方条数({recipeCount})—— " +
                "有配方缺 owner(装载期本就该硬失败,AC-21a-66),或本抽取按出现序对齐的假设已失效");
        }

        /// <summary>
        /// <b>真表覆盖度</b> —— 真种子 `item_database_recipes.json` 的三个子集**均非空**。
        /// <para>⚠️ 本测是 AC-18-18 的**真表半边**。它与合成夹具测的
        /// <see cref="test_synthetic_onePerSubset_threePartitionPasses"/> **是两件事**:
        /// 合成绿只证「机制能判」;本测绿才证「**真数据真的分布在三个桶里**」。</para>
        /// <para>—— 2026-10-20 前置状态(如实登记):真表原为 **2 条配方、owner 全 `process`**
        /// ⇒ `craft` / `build` 空 ⇒ 本测**必红**。已补 `owner=craft` 与 `owner=build` 各 1 条
        /// 占位配方(数值为夹具值,非平衡值)。</para>
        /// </summary>
        [Test]
        public void test_realSeed_allThreeOwnerSubsetsNonEmpty()
        {
            string recipesJson = ReadSeed(ItemDatabaseBaker.RecipesFileName);
            List<string> owners = ExtractOwnerLiterals(recipesJson);

            Assert.That(owners, Is.Not.Empty, "真表须至少 1 条配方");

            // 复用同一套三段判据(单一定义处 —— 合成与真表不分叉)
            AssertThreePartition(owners, "真种子 item_database_recipes.json");
        }

        /// <summary>
        /// 真表经**完整烘焙管线**仍零错误 —— 证明新增的 craft / build 配方过了全部构建期门
        /// (守恒 AC-21a-65 / 外键 AC-21a-24 / qty AC-21a-16 / duration AC-21a-18 等)。
        /// <para>本测是「数据改动没把别的门踩红」的门闩;它**不**替代
        /// <see cref="test_realSeed_allThreeOwnerSubsetsNonEmpty"/> 的覆盖判据。</para>
        /// </summary>
        [Test]
        public void test_realSeed_bakesCleanAfterCraftAndBuildRecipesAdded()
        {
            BakeResult result = ItemDatabaseBaker.BakeFromRepo(RepoRoot);

            Assert.That(result.ItemsCooked, Is.Not.Empty, "items cooked 非空");
            Assert.That(result.RecipesCooked, Is.Not.Empty, "recipes cooked 非空");
        }
    }
}
