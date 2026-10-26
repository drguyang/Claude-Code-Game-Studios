// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析、零 FixParse)· CookedFormat.cs(布局契约)
//          · Story 008(读方住边界程序集 Gameplay.Presentation;与 Editor.Tools.Bake.CookedWriter 严格镜像)
//
// ⚠️ 任何字段缺失/魔数不符/长度不符 ⇒ 显式 throw(启动期硬失败,E-13 口径),绝不返回默认值。

using System;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>cooked 字节 → 领域 struct 的读方(与 CookedWriter 镜像)。
    /// <para>只用 BCL(BinaryReader, little-endian);Fix 字段 = raw i64,不经任何解析器。</para>
    /// <example><code>ItemDataSet data = CookedCodec.ReadItemDataSet(bytes); // 失败即 throw</code></example>
    /// </summary>
    public static class CookedCodec
    {
        /// <summary>读物品种子表数据集。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 版本不匹配 / 载荷长度不符 —— 启动期硬失败。</exception>
        public static ItemDataSet ReadItemDataSet(byte[] bytes)
        {
            using (BinaryReader r = OpenChecked(bytes, ExpectedItemSchemaVersion, out uint configVersion))
            {
                int count = r.ReadInt32();
                if (count < 0)
                    throw new InvalidDataException("[cooked] items 计数为 null 标记 —— 产物损坏,启动期硬失败(E-13)");

                var items = new ItemDef[count];
                for (int i = 0; i < count; i++)
                    items[i] = ReadItemDef(r);

                return new ItemDataSet { Items = items, ConfigVersion = configVersion };
            }
        }

        /// <summary>读配方表 + 结算常量数据集。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 版本不匹配 / 载荷长度不符 —— 启动期硬失败。</exception>
        public static RecipeDataSet ReadRecipeDataSet(byte[] bytes)
        {
            using (BinaryReader r = OpenChecked(bytes, ExpectedRecipeSchemaVersion, out uint configVersion))
            {
                int count = r.ReadInt32();
                if (count < 0)
                    throw new InvalidDataException("[cooked] recipes 计数为 null 标记 —— 产物损坏,启动期硬失败(E-13)");

                var recipes = new Recipe[count];
                for (int i = 0; i < count; i++)
                    recipes[i] = ReadRecipe(r);

                // 结算常量(11× i64 + 2× i32,构造函数顺序)
                var settlement = new RecipeSettlementConstants(
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    new Fix(r.ReadInt64()),
                    r.ReadInt32(),
                    r.ReadInt32());

                return new RecipeDataSet
                {
                    Recipes = recipes,
                    Settlement = settlement,
                    ConfigVersion = configVersion,
                };
            }
        }

        // ─── 头校验 ───

        /// <summary><c>item_database_items.cooked.bytes</c> 的作者态 schema_version 期望值。
        /// <para><b>per-product,不抬全局常量</b>(照 <see cref="DiseaseRegistryCookedCodec.ExpectedSchemaVersion"/>
        /// 的既有先例):2026-10-26 给 <c>ItemDef</c> 加 <c>Ordinal</c>(ADR-006 D-21-13)是一次 schema 变更,
        /// 但它只影响 <b>items</b> 这一份产物 —— 抬 <c>CookedFormat.SchemaVersion</c> 会连带要求全仓
        /// 28 份 JSON 改版本 + 25 份产物重烘,而 <c>CookedFormat.SchemaVersion</c> 只是
        /// 「未自带版本的产物恰好沿用的默认值」。</para>
        /// <para>⚠️ 为什么**不**与 recipes 共用:recipes 的载荷布局本批**一字未改**,
        /// 把它的版本号一起抬 = 对「recipe schema 变了」说一句假话。版本号按产物各自演化。</para></summary>
        public const uint ExpectedItemSchemaVersion = 2u;

        /// <summary><c>item_database_recipes.cooked.bytes</c>(含结算常量)的 schema_version 期望值。
        /// <para>仍是 1 —— 2026-10-26 的 <c>Ordinal</c> 变更只落在 <see cref="ItemDef"/>,未触及 recipe 载荷。
        /// 与 <see cref="ExpectedItemSchemaVersion"/> 分列即「按产物演化」的落地形态。</para></summary>
        public const uint ExpectedRecipeSchemaVersion = 1u;

        private static BinaryReader OpenChecked(byte[] bytes, uint requiredSchemaVersion, out uint configVersion)
        {
            if (bytes == null || bytes.Length < CookedFormat.HeaderSize)
                throw new InvalidDataException(
                    $"[cooked] 输入短于头部({CookedFormat.HeaderSize} B)—— 产物损坏,启动期硬失败(E-13)");

            if (bytes[0] != (byte)'D' || bytes[1] != (byte)'Y' || bytes[2] != (byte)'J' || bytes[3] != (byte)'C')
                throw new InvalidDataException("[cooked] 魔数非 DYJC —— 非本工程产物,启动期硬失败(E-13)");

            uint formatVersion = ReadU32Le(bytes, 4);
            uint schemaVersion = ReadU32Le(bytes, 8);
            configVersion = ReadU32Le(bytes, 12);
            uint payloadLength = ReadU32Le(bytes, 16);

            if (formatVersion > CookedFormat.FormatVersion)
                throw new InvalidDataException(
                    $"[cooked] formatVersion = {formatVersion} > 读方支持的 {CookedFormat.FormatVersion} —— " +
                    "产物新于读方,启动期硬失败(E-13)");

            if (schemaVersion != requiredSchemaVersion)
                throw new InvalidDataException(
                    $"[cooked] schemaVersion = {schemaVersion},期望 {requiredSchemaVersion} —— " +
                    "schema 不可读即致命(ADR-010 §七),启动期硬失败(E-13)");

            if (payloadLength != bytes.Length - CookedFormat.HeaderSize)
                throw new InvalidDataException(
                    $"[cooked] 载荷长度声明 {payloadLength} ≠ 实际 {bytes.Length - CookedFormat.HeaderSize} —— " +
                    "产物截断/损坏,启动期硬失败(E-13)");

            return new BinaryReader(new MemoryStream(bytes, CookedFormat.HeaderSize, (int)payloadLength), Encoding.UTF8);
        }

        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        // ─── 载荷读方(镜像 CookedWriter) ───

        private static ItemDef ReadItemDef(BinaryReader r)
        {
            var def = new ItemDef
            {
                // ⚠️ 首字段,严格镜像 CookedWriter.WriteItemDef(ADR-006 D-21-13)
                Ordinal = r.ReadInt32(),
                BaseId = ReadString(r),
                ProcessingState = (ProcessingState)r.ReadInt32(),
                DisplayName = ReadString(r),
                Category = (ItemCategory)r.ReadInt32(),
                StackMax = r.ReadInt32(),
                Weight = r.ReadInt32(),
                Deprecated = r.ReadByte() != 0,
                LegalTransitions = ReadStringArray(r),
            };

            if (r.ReadByte() != 0)
            {
                var drug = new DrugProfile
                {
                    Indications = ReadStringArray(r),
                    Contraindications = ReadStringArray(r),
                };
                if (r.ReadByte() != 0)
                    drug.DoseRange = new DoseRange(r.ReadInt32(), r.ReadInt32());
                drug.DrugPotency = ReadNullableFix(r);
                drug.Onset = ReadNullableFix(r);
                drug.Peak = ReadNullableFix(r);
                drug.HalfLife = ReadNullableFix(r);
                drug.Elimination = ReadNullableFix(r);
                if (r.ReadByte() != 0)
                    drug.QualityAxis = (QualityAxis)r.ReadInt32();
                int offsetCount = r.ReadInt32();
                if (offsetCount >= 0)
                {
                    var offsets = new Fix[offsetCount];
                    for (int i = 0; i < offsetCount; i++)
                        offsets[i] = new Fix(r.ReadInt64());
                    drug.AxisOffsetByQuality = offsets;
                }
                drug.DrugQualityCharacter = ReadStringArray(r);
                def.DrugProfile = drug;
            }

            if (r.ReadByte() != 0)
            {
                def.GatherProfile = new GatherProfile
                {
                    Ecosystem = ReadString(r),
                    Parts = ReadStringArray(r),
                    QtyPerNode = r.ReadInt32(),
                    QualityCharacter = ReadStringArray(r),
                };
            }

            if (r.ReadByte() != 0)
                def.TcmProfile = new TcmProfile();

            def.InflictsInjury = ReadStringArray(r);
            return def;
        }

        private static Recipe ReadRecipe(BinaryReader r)
        {
            var recipe = new Recipe
            {
                RecipeId = ReadString(r),
                Owner = (RecipeOwner)r.ReadInt32(),
                Inputs = ReadRecipeEntries(r),
                Outputs = ReadRecipeEntries(r),
                DurationTicks = r.ReadInt32(),
                SkillGate = r.ReadInt32(),
                MinQuality = r.ReadInt32(),
            };

            int boundaryCount = r.ReadInt32();
            if (boundaryCount >= 0)
            {
                var boundary = new ProcessingTransition[boundaryCount];
                for (int i = 0; i < boundaryCount; i++)
                    boundary[i] = new ProcessingTransition((ProcessingState)r.ReadInt32(), (ProcessingState)r.ReadInt32());
                recipe.BoundaryState = boundary;
            }
            return recipe;
        }

        private static RecipeEntry[] ReadRecipeEntries(BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count < 0)
                return null;
            var entries = new RecipeEntry[count];
            for (int i = 0; i < count; i++)
            {
                string baseId = ReadString(r);
                var state = (ProcessingState)r.ReadInt32();
                int qty = r.ReadInt32();
                entries[i] = new RecipeEntry(new ItemKey(baseId, state), qty);
            }
            return entries;
        }

        private static string ReadString(BinaryReader r)
        {
            int len = r.ReadInt32();
            if (len < 0)
                return null;
            byte[] bytes = r.ReadBytes(len);
            if (bytes.Length != len)
                throw new InvalidDataException("[cooked] 字符串截断 —— 产物损坏,启动期硬失败(E-13)");
            return Encoding.UTF8.GetString(bytes);
        }

        private static string[] ReadStringArray(BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count < 0)
                return null;
            var arr = new string[count];
            for (int i = 0; i < count; i++)
                arr[i] = ReadString(r);
            return arr;
        }

        private static Fix? ReadNullableFix(BinaryReader r)
        {
            byte flag = r.ReadByte();
            if (flag == 0)
                return null;
            return new Fix(r.ReadInt64());
        }
    }
}
