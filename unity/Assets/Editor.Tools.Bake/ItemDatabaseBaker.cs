// 权威来源:ADR-014 §三/§五(两阶段烘焙入口)· ADR-010 §七(ConfigVersion 比对)
//          · Story 008(门接线 + 聚合 throw —— 既有门零 throw,唯一抛出点在此)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>烘焙校验失败(聚合)。**本工程烘焙管线唯一的 throw 类** —— 门本身保持零 throw。
    /// <example><code>try { ItemDatabaseBaker.BakeFromRepo(root); }
    /// catch (BakeValidationException ex) { foreach (string e in ex.Errors) Debug.LogError(e); }</code></example>
    /// </summary>
    public sealed class BakeValidationException : Exception
    {
        /// <summary>全部错误(词法 + 绑定 + 门,按执行序聚合)。</summary>
        public IReadOnlyList<string> Errors { get; }

        public BakeValidationException(IReadOnlyList<string> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors;
        }

        private static string BuildMessage(IReadOnlyList<string> errors)
        {
            int count = errors?.Count ?? 0;
            if (count == 0) return "烘焙校验失败(0 条 —— 程序错误:空错误列表不应抛出)";
            var sb = new StringBuilder();
            sb.Append("烘焙校验失败(").Append(count).Append(" 条):");
            for (int i = 0; i < count; i++)
                sb.AppendLine().Append("  ").Append(i + 1).Append(". ").Append(errors[i]);
            return sb.ToString();
        }
    }

    /// <summary>烘焙成功产物。</summary>
    public readonly struct BakeResult
    {
        /// <summary>物品种子表 cooked 字节(item_database_items.cooked.bytes 内容)。</summary>
        public readonly byte[] ItemsCooked;

        /// <summary>配方表 + 结算常量 cooked 字节(item_database_recipes.cooked.bytes 内容)。</summary>
        public readonly byte[] RecipesCooked;

        /// <summary>源数据集内容哈希派生的 ConfigVersion(u32;两产物头同值)。</summary>
        public readonly uint ConfigVersion;

        public BakeResult(byte[] itemsCooked, byte[] recipesCooked, uint configVersion)
        {
            ItemsCooked = itemsCooked;
            RecipesCooked = recipesCooked;
            ConfigVersion = configVersion;
        }
    }

    /// <summary>item-database 烘焙入口(ADR-014 两阶段:阶段1 词法 → 阶段2 绑定 → 全部门 → cooked)。
    /// <para><b>聚合 throw</b>:错误列表非空即 <see cref="BakeValidationException"/> 一次性抛出 ——
    /// 校验失败绝不降级为警告(Story 008 Control Manifest)。</para>
    /// <example>
    /// <code>
    /// BakeResult r = ItemDatabaseBaker.BakeFromRepo(repoRoot);      // 菜单 / CI
    /// BakeResult r = ItemDatabaseBaker.BakeFromSourceText(j1, j2, j3); // 夹具注入
    /// </code>
    /// </example>
    /// </summary>
    public static class ItemDatabaseBaker
    {
        /// <summary>三个作者态源文件名(assets/data/ 下;ConfigVersion 哈希的命名键)。</summary>
        public const string ItemsFileName = "item_database_items.json";
        public const string RecipesFileName = "item_database_recipes.json";
        public const string ConstantsFileName = "item_database_constants.json";

        /// <summary>从三段源文本烘焙(测试可对任意文本注入,如负向夹具)。
        /// <para><paramref name="mapsToInjury"/> = 25 侧 maps_to_injury 并集注入点(系统 25 未建 ⇒ 生产路径传 null,
        /// 注入时才执行 AC-25b 集合漂移门)。</para></summary>
        /// <exception cref="BakeValidationException">词法 / 绑定 / 任一门失败(聚合)。</exception>
        public static BakeResult BakeFromSourceText(
            string itemsJson, string recipesJson, string constantsJson, ISet<string> mapsToInjury = null)
        {
            // ── 阶段1:词法(JsonTextReader 仅词法;错误即抛,不再绑定)──
            var errors = new List<string>();
            if (!JsonStage1Lexer.TryParse(itemsJson, out JsonNode itemsRoot, out string eItems))
                errors.Add($"阶段1 {ItemsFileName}:{eItems}");
            if (!JsonStage1Lexer.TryParse(recipesJson, out JsonNode recipesRoot, out string eRecipes))
                errors.Add($"阶段1 {RecipesFileName}:{eRecipes}");
            if (!JsonStage1Lexer.TryParse(constantsJson, out JsonNode constantsRoot, out string eConstants))
                errors.Add($"阶段1 {ConstantsFileName}:{eConstants}");
            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            // ── 阶段2:绑定(白名单 / 类型 / FixParse / schema_version)──
            BindPackage pkg = ItemDatabaseBinder.BindAll(itemsRoot, recipesRoot, constantsRoot);
            if (pkg.Errors.Count > 0)
                throw new BakeValidationException(pkg.Errors);

            // ── 全部门(绑定零错误才跑 —— 防残缺记录制造噪声)──
            RunGates(pkg, pkg.Errors, mapsToInjury);
            if (pkg.Errors.Count > 0)
                throw new BakeValidationException(pkg.Errors);

            // ── 确定性编码 ──
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(
                NamedSources(itemsJson, recipesJson, constantsJson));

            var defs = new ItemDef[pkg.Items.Count];
            for (int i = 0; i < pkg.Items.Count; i++)
                defs[i] = pkg.Items[i].Def;
            var recipes = new Recipe[pkg.Recipes.Count];
            for (int i = 0; i < pkg.Recipes.Count; i++)
                recipes[i] = pkg.Recipes[i].Rec;

            byte[] itemsCooked = CookedWriter.WriteItems(defs, CookedFormat.SchemaVersion, configVersion);
            byte[] recipesCooked = CookedWriter.WriteRecipes(
                recipes, pkg.Constants.Constants, CookedFormat.SchemaVersion, configVersion);

            return new BakeResult(itemsCooked, recipesCooked, configVersion);
        }

        /// <summary>从仓库 <c>assets/data/</c> 读三个源文件烘焙(菜单 / 桌面入口)。</summary>
        /// <param name="repoRoot">仓库根(含 assets/ 的目录)。</param>
        /// <exception cref="BakeValidationException">校验失败(聚合)。</exception>
        public static BakeResult BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));
            string dataDir = Path.Combine(repoRoot, "assets", "data");
            string itemsJson = File.ReadAllText(Path.Combine(dataDir, ItemsFileName), Encoding.UTF8);
            string recipesJson = File.ReadAllText(Path.Combine(dataDir, RecipesFileName), Encoding.UTF8);
            string constantsJson = File.ReadAllText(Path.Combine(dataDir, ConstantsFileName), Encoding.UTF8);
            return BakeFromSourceText(itemsJson, recipesJson, constantsJson);
        }

        /// <summary>ConfigVersion 哈希的命名源(键 = 固定文件名,与磁盘发现一致)。
        /// <para>公开:测试套件 data_pipeline_bake_test 须以同名源独立复算 DeriveConfigVersion 对拍。</para>
        /// <example><code>var src = ItemDatabaseBaker.NamedSources(itemsJson, recipesJson, constantsJson);
        /// uint v = ConfigVersionUtility.DeriveConfigVersion(src);</code></example></summary>
        public static KeyValuePair<string, string>[] NamedSources(
            string itemsJson, string recipesJson, string constantsJson)
        {
            return new[]
            {
                new KeyValuePair<string, string>(ItemsFileName, itemsJson ?? string.Empty),
                new KeyValuePair<string, string>(RecipesFileName, recipesJson ?? string.Empty),
                new KeyValuePair<string, string>(ConstantsFileName, constantsJson ?? string.Empty),
            };
        }

        // ══════════ 门接线(B2;输入保证绑定零错误)══════════

        private static void RunGates(BindPackage pkg, List<string> errors, ISet<string> mapsToInjury)
        {
            // ── 1. 常量表门 ──
            if (pkg.Constants == null)
            {
                errors.Add("常量表未绑定(内部错误:绑定零错误却缺常量)");
                return;
            }
            RecipeSettlementConstants constants = pkg.Constants.Constants;

            AddAll(errors, ItemValidationGates.ValidateMaxQuality(pkg.Constants.RawMaxQuality, "constants"));
            AddAll(errors, RecipeValidationGates.ValidateQtyMultRange(in constants, "constants"));
            AddAll(errors, RecipeValidationGates.ValidateRetainRange(in constants, "constants"));
            AddAll(errors, RecipeValidationGates.ValidateEnvModRange(in constants, "constants"));
            AddAll(errors, RecipeValidationGates.ValidateConstantCapSum(in constants, "constants"));
            AddAll(errors, ConservationGates.ValidateEffMaxRange(in constants, "constants"));
            AddAll(errors, ConservationGates.ValidateEffMinRange(in constants, "constants"));

            // ── 2. 逐物品门 ──
            var defs = new ItemDef[pkg.Items.Count];
            var knownKeys = new List<ItemKey>(pkg.Items.Count);
            var weightMap = new Dictionary<ItemKey, int>();
            var itemByKey = new Dictionary<ItemKey, BoundItem>();
            var inflictsUnion = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < pkg.Items.Count; i++)
            {
                BoundItem bi = pkg.Items[i];
                defs[i] = bi.Def;
                ItemKey key = new ItemKey(bi.Def.BaseId, bi.Def.ProcessingState);
                knownKeys.Add(key);
                weightMap[key] = bi.Def.Weight;                 // 重复键:后写覆盖;唯一性由 dup 门负责
                if (!itemByKey.ContainsKey(key)) itemByKey[key] = bi;

                string[] inflicts = bi.Def.InflictsInjury ?? Array.Empty<string>();
                for (int k = 0; k < inflicts.Length; k++)
                    inflictsUnion.Add(inflicts[k]);

                AddAll(errors, ItemDbValidation.ValidateEnumClosure(bi.RawCategory, bi.RawState));
                AddAll(errors, ItemValidationGates.ValidateP0Narrowing(bi.RawState, bi.RawTcmBody, bi.Label));
                AddAll(errors, ItemValidationGates.ValidateStackWeight(bi.RawStackMax, bi.RawWeight, bi.Label));
                AddAll(errors, ItemValidationGates.ValidateInjuryBinding(
                    bi.Def.Category, inflicts, pkg.KnownInjuryIds, bi.Label));

                if (bi.Def.DrugProfile.HasValue)
                {
                    DrugProfile drug = bi.Def.DrugProfile.Value;
                    AddAll(errors, DrugProfileGates.ValidateP0QualityAxis(drug.QualityAxis, bi.Label));
                    AddAll(errors, DrugProfileGates.ValidateAxisOffsetLength(
                        drug.AxisOffsetByQuality, constants.MaxQuality, bi.Label));
                    if (drug.AxisOffsetByQuality != null)
                        AddAll(errors, DrugProfileGates.ValidatePerceptibleFloor(
                            drug.AxisOffsetByQuality, pkg.Constants.PerceptibleFloor, bi.Label));
                    if (drug.DrugQualityCharacter != null)
                        AddAll(errors, DrugProfileGates.ValidateDrugQualityCharacterLength(
                            drug.DrugQualityCharacter, constants.MaxQuality, bi.Label));
                }

                if (bi.Def.GatherProfile.HasValue && bi.Def.GatherProfile.Value.QualityCharacter != null)
                    AddAll(errors, DrugProfileGates.ValidateGatherQualityCharacterLength(
                        bi.Def.GatherProfile.Value.QualityCharacter, constants.MaxQuality, bi.Label));
            }

            // ── 3. 跨物品门(复合主键唯一)──
            AddAll(errors, ItemDbValidation.FindDuplicateCompositeKeys(defs));

            // ── 4. owner 三子集划分(AC-21a-66)──
            AddAll(errors, RecipeValidationGates.PartitionRecipeOwners(
                pkg.RecipeRawOwners, out _, out _, out _));

            // ── 5. 逐配方门 ──
            for (int i = 0; i < pkg.Recipes.Count; i++)
            {
                BoundRecipe br = pkg.Recipes[i];
                Recipe recipe = br.Rec;

                AddAll(errors, RecipeValidationGates.ValidateOutputQty(recipe.Outputs, br.Label));
                AddAll(errors, RecipeValidationGates.ValidateRecipeEntryQuantities(
                    recipe.Inputs, recipe.Outputs, br.Label));
                AddAll(errors, RecipeValidationGates.ValidateDurationTicks(recipe.DurationTicks, br.Label));
                AddAll(errors, RecipeValidationGates.ValidateSkillGate(
                    recipe.SkillGate, in constants, br.Label));
                AddAll(errors, RecipeValidationGates.ValidateMinQuality(
                    recipe.MinQuality, in constants, br.Label));
                AddAll(errors, RecipeValidationGates.ValidateRecipeForeignKeys(
                    recipe.Inputs, recipe.Outputs, knownKeys, br.Label));

                // AC-24:边界对 ∈ 输入侧[0] 物品声明的 legal_transitions(跨 base 配方查输入侧)。
                if (recipe.Inputs != null && recipe.Inputs.Length > 0 &&
                    itemByKey.TryGetValue(recipe.Inputs[0].Key, out BoundItem sourceItem))
                {
                    AddAll(errors, ItemValidationGates.ValidateTransition(
                        sourceItem.Def.LegalTransitions ?? Array.Empty<string>(),
                        recipe.BoundaryState ?? Array.Empty<ProcessingTransition>(),
                        br.Label));
                }
            }

            // ── 6. 守恒门(逐配方,AC-8 / AC-65)──
            Func<ItemKey, int> lookupWeight = k => weightMap.TryGetValue(k, out int w) ? w : 0;
            for (int i = 0; i < pkg.Recipes.Count; i++)
            {
                BoundRecipe br = pkg.Recipes[i];
                AddAll(errors, ConservationGates.ValidateAggregateConservation(
                    br.Rec.Outputs, br.Rec.Inputs, lookupWeight, in constants, br.Label));
                AddAll(errors, ConservationGates.ValidatePerLineExtremeConservation(
                    br.Rec.Outputs, br.Rec.Inputs, lookupWeight, in constants, br.Label));
            }

            // ── 7. AC-25b 集合漂移门(25 侧数据注入时才执行;生产路径 25 未建 ⇒ null)──
            if (mapsToInjury != null)
                AddAll(errors, ItemValidationGates.ValidateInjurySetSuperset(inflictsUnion, mapsToInjury, "items"));
        }

        private static void AddAll(List<string> errors, IReadOnlyList<string> more)
        {
            if (more == null) return;
            for (int i = 0; i < more.Count; i++)
                errors.Add(more[i]);
        }
    }
}
