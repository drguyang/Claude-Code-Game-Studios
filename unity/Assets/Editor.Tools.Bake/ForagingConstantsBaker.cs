// S20-3 · story-001 —— `foraging_constants.json` 的**烘焙入口**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
// 评审 W-3 甲案:补齐 ADR-014 链路里缺失的「Baker 写 *.cooked」一环。
//
// 权威来源:
//   ADR-014 §二/§三/§五(两阶段烘焙 · 白名单/FixParse · ConfigVersion 内容哈希派生)
//   `JudgmentLexiconBaker` 同款结构(本仓最小的 Baker 先例)
//
// ⚠️ **不碰 UnityEngine / Addressables** —— 菜单与 EditMode 测试驱动同一台机器
//   (承 ItemDatabaseFurnitureBake / DelayTable53Bake / BuildModulesBake 同款头注)。
//
// ⚠️ **`maxQuality` 从 21a 读,不在本表写死**(D-21-7 双向耦合)。
//   本 Baker 因此要**同时**绑 21a 的 constants 段 —— 这使「17 的表 × 21a 的常量」
//   这一对在**生产烘焙路径**上被联合校验,而不是各测各的。
//   ⚠️ 这是本 Baker 与 `JudgmentLexiconBaker` 唯一的结构差异(后者无跨表依赖)。
//
// ⚠️ **本类不做写盘** —— 落盘与 Addressables 组注册归菜单层(`DataBakeMenu`),
//   与全部既有 Baker 一致。EditMode 测试可直驱 `BakeFromRepo` 而不触盘。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`foraging_constants.json` 两阶段烘焙的仓根装载器。</summary>
    public static class ForagingConstantsBaker
    {
        /// <summary>作者态源文件名(单一出处;与 Schema / Binder 同源)。</summary>
        public const string FileName = ForagingConstantsSchema.FileName;

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>烘焙产物名(`Assets/DataCooked/` 下;Addressables 地址同此名)。</summary>
        public const string CookedFileName = "foraging_constants.cooked.bytes";

        /// <summary>烘焙输出。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(已校验;测试可据此断形状而不必反解字节)。</summary>
            public readonly ForagingConstantsBinder.BindResult Bound;

            /// <summary>本次烘焙用的 21a `MAX_QUALITY`(供菜单日志与测试归因)。</summary>
            public readonly int MaxQuality;

            /// <summary>构造输出。</summary>
            public BakeOutput(
                byte[] cooked, uint configVersion,
                ForagingConstantsBinder.BindResult bound, int maxQuality)
            {
                Cooked = cooked;
                ConfigVersion = configVersion;
                Bound = bound;
                MaxQuality = maxQuality;
            }
        }

        /// <summary>从仓根烘焙 <c>assets/data/foraging_constants.json</c>。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的那一层)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        /// <exception cref="BakeValidationException">
        /// 源文件缺失 / 词法 / 绑定 / 21a 常量读不出 —— 任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, FileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);

            // ── 跨表前置:21a 的 MAX_QUALITY(不在本表写死 —— D-21-7)──
            int maxQuality = Read21aMaxQuality(repoRoot);

            ForagingConstantsBinder.BindResult bound = ForagingConstantsBinder.Bind(json, maxQuality);

            // ConfigVersion = 本表内容的哈希。**刻意不含** 21a 的 constants:
            // 21a 有它自己的 ConfigVersion,两者各自由 ADR-010 §七 比对;
            // 混入会把「21a 改了一个无关常量」变成「17 的表版本变了」= 假失效。
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(FileName, json),
            });

            byte[] cooked = ForagingConstantsCookedWriter.Write(
                bound, bound.SchemaVersion, configVersion);

            return new BakeOutput(cooked, configVersion, bound, maxQuality);
        }

        /// <summary>
        /// 经 21a 的**真 binder**读 <c>max_quality</c>(不另写解析 —— 复刻即第二真源)。
        /// <para>与测试侧 <c>foraging_data_load_test.Read21aMaxQuality</c> 同一路径;
        /// 两处都走 <see cref="ItemDatabaseBinder.BindAll"/>,故「生产烘焙」与「测试」
        /// 对 21a 常量的读法**不可能分叉**。</para>
        /// </summary>
        private static int Read21aMaxQuality(string repoRoot)
        {
            string itemsPath = Path.Combine(repoRoot, DataDirName, ItemDatabaseBaker.ItemsFileName);
            string recipesPath = Path.Combine(repoRoot, DataDirName, ItemDatabaseBaker.RecipesFileName);
            string constantsPath = Path.Combine(repoRoot, DataDirName, ItemDatabaseBaker.ConstantsFileName);

            var errors = new List<string>();
            foreach (string p in new[] { itemsPath, recipesPath, constantsPath })
                if (!File.Exists(p))
                    errors.Add($"21a 源文件不存在(MAX_QUALITY 无从读取):{p}");
            if (errors.Count > 0) throw new BakeValidationException(errors);

            JsonNode items = LexOrThrow(File.ReadAllText(itemsPath), ItemDatabaseBaker.ItemsFileName);
            JsonNode recipes = LexOrThrow(File.ReadAllText(recipesPath), ItemDatabaseBaker.RecipesFileName);
            JsonNode constants = LexOrThrow(File.ReadAllText(constantsPath), ItemDatabaseBaker.ConstantsFileName);

            BindPackage pkg = ItemDatabaseBinder.BindAll(items, recipes, constants);
            if (pkg.Errors.Count > 0)
            {
                var all = new List<string> { "21a 三表绑定失败(MAX_QUALITY 无从读取):" };
                all.AddRange(pkg.Errors);
                throw new BakeValidationException(all);
            }
            if (pkg.Constants == null)
                throw new BakeValidationException(
                    new List<string> { "21a constants 段缺失(MAX_QUALITY 无从读取)" });

            string raw = pkg.Constants.RawMaxQuality;
            if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out int maxQuality))
                throw new BakeValidationException(
                    new List<string> { $"21a max_quality = \"{raw}\" 不是整数" });
            if (maxQuality < 1)
                throw new BakeValidationException(
                    new List<string> { $"21a max_quality = {maxQuality} < 1(品级上界非法)" });

            return maxQuality;
        }

        private static JsonNode LexOrThrow(string json, string fileName)
        {
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string err))
                throw new BakeValidationException(
                    new List<string> { $"{fileName} 阶段1 词法失败:{err}" });
            return root;
        }
    }
}
