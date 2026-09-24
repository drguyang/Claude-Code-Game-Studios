// 权威来源:ADR-014 §二(确定性 *.cooked;Fix = raw long;同源两次烘焙逐位一致)
//          · CookedFormat.cs(头部与载荷布局的唯一共参照 —— 本文件必须与之严格镜像)

using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>确定性 cooked 写入器(little-endian、固定字段顺序、零字典迭代)。
    /// <para>同一批 struct 输入两次 ⇒ 逐位一致字节流(确定性由「固定顺序 + 无随机 + 无本地时间」保证)。</para>
    /// <example><code>byte[] bytes = CookedWriter.WriteItems(items, schemaVersion, configVersion);</code></example>
    /// </summary>
    internal static class CookedWriter
    {
        /// <summary>编码物品种子表数据集(含 20 字节头)。</summary>
        public static byte[] WriteItems(ItemDef[] items, uint schemaVersion, uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                WriteInt(w, items?.Length ?? -1);
                if (items != null)
                {
                    for (int i = 0; i < items.Length; i++)
                        WriteItemDef(w, items[i]);
                }
                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        /// <summary>编码配方表 + 结算常量数据集(含 20 字节头)。</summary>
        public static byte[] WriteRecipes(
            Recipe[] recipes,
            in RecipeSettlementConstants settlement,
            uint schemaVersion,
            uint configVersion)
        {
            using (var payload = new MemoryStream())
            using (var w = new BinaryWriter(payload, Encoding.UTF8))
            {
                WriteInt(w, recipes?.Length ?? -1);
                if (recipes != null)
                {
                    for (int i = 0; i < recipes.Length; i++)
                        WriteRecipe(w, recipes[i]);
                }

                // 结算常量(构造函数顺序,11× i64 + 2× i32)。
                w.Write(settlement.QtyMultMin.Raw);
                w.Write(settlement.QtyMultMax.Raw);
                w.Write(settlement.SkillModCap.Raw);
                w.Write(settlement.QualModCap.Raw);
                w.Write(settlement.EquipModCap.Raw);
                w.Write(settlement.EnvModMin.Raw);
                w.Write(settlement.EnvModMax.Raw);
                w.Write(settlement.RetainMin.Raw);
                w.Write(settlement.RetainMax.Raw);
                w.Write(settlement.EffMin.Raw);
                w.Write(settlement.EffMax.Raw);
                w.Write(settlement.MaxQuality);
                w.Write(settlement.SkillCap);

                w.Flush();
                return FinishHeader(payload.ToArray(), schemaVersion, configVersion);
            }
        }

        /// <summary>头(magic + 三 u32 + 长度)+ 载荷 拼装。</summary>
        private static byte[] FinishHeader(byte[] payload, uint schemaVersion, uint configVersion)
        {
            var output = new byte[CookedFormat.HeaderSize + payload.Length];
            // magic "DYJC"(ASCII)
            output[0] = (byte)'D';
            output[1] = (byte)'Y';
            output[2] = (byte)'J';
            output[3] = (byte)'C';
            WriteU32Le(output, 4, CookedFormat.FormatVersion);
            WriteU32Le(output, 8, schemaVersion);
            WriteU32Le(output, 12, configVersion);
            WriteU32Le(output, 16, (uint)payload.Length);
            System.Buffer.BlockCopy(payload, 0, output, CookedFormat.HeaderSize, payload.Length);
            return output;
        }

        private static void WriteU32Le(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        // ─── 载荷小件(BinaryWriter = little-endian,规格钉死) ───

        private static void WriteInt(BinaryWriter w, int value) => w.Write(value);

        private static void WriteString(BinaryWriter w, string value)
        {
            if (value == null)
            {
                w.Write(-1);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        private static void WriteStringArray(BinaryWriter w, string[] values)
        {
            if (values == null)
            {
                w.Write(-1);
                return;
            }
            w.Write(values.Length);
            for (int i = 0; i < values.Length; i++)
                WriteString(w, values[i]);
        }

        private static void WriteBool(BinaryWriter w, bool value) => w.Write((byte)(value ? 1 : 0));

        private static void WriteNullableFix(BinaryWriter w, Fix? value)
        {
            if (value.HasValue)
            {
                w.Write((byte)1);
                w.Write(value.Value.Raw);
            }
            else
            {
                w.Write((byte)0);
            }
        }

        private static void WriteItemDef(BinaryWriter w, ItemDef def)
        {
            WriteString(w, def.BaseId);
            w.Write((int)def.ProcessingState);
            WriteString(w, def.DisplayName);
            w.Write((int)def.Category);
            w.Write(def.StackMax);
            w.Write(def.Weight);
            WriteBool(w, def.Deprecated);
            WriteStringArray(w, def.LegalTransitions);

            // drug_profile
            if (def.DrugProfile.HasValue)
            {
                DrugProfile drug = def.DrugProfile.Value;
                WriteBool(w, true);
                WriteStringArray(w, drug.Indications);
                WriteStringArray(w, drug.Contraindications);
                if (drug.DoseRange.HasValue)
                {
                    WriteBool(w, true);
                    w.Write(drug.DoseRange.Value.Min);
                    w.Write(drug.DoseRange.Value.Max);
                }
                else
                {
                    WriteBool(w, false);
                }
                WriteNullableFix(w, drug.DrugPotency);
                WriteNullableFix(w, drug.Onset);
                WriteNullableFix(w, drug.Peak);
                WriteNullableFix(w, drug.HalfLife);
                WriteNullableFix(w, drug.Elimination);
                if (drug.QualityAxis.HasValue)
                {
                    WriteBool(w, true);
                    w.Write((int)drug.QualityAxis.Value);
                }
                else
                {
                    WriteBool(w, false);
                }
                if (drug.AxisOffsetByQuality == null)
                {
                    w.Write(-1);
                }
                else
                {
                    w.Write(drug.AxisOffsetByQuality.Length);
                    for (int i = 0; i < drug.AxisOffsetByQuality.Length; i++)
                        w.Write(drug.AxisOffsetByQuality[i].Raw);
                }
                WriteStringArray(w, drug.DrugQualityCharacter);
            }
            else
            {
                WriteBool(w, false);
            }

            // gather_profile
            if (def.GatherProfile.HasValue)
            {
                GatherProfile gather = def.GatherProfile.Value;
                WriteBool(w, true);
                WriteString(w, gather.Ecosystem);
                WriteStringArray(w, gather.Parts);
                w.Write(gather.QtyPerNode);
                WriteStringArray(w, gather.QualityCharacter);
            }
            else
            {
                WriteBool(w, false);
            }

            // tcm_profile(P0 恒空;有值即 1,无 body)
            WriteBool(w, def.TcmProfile.HasValue);

            WriteStringArray(w, def.InflictsInjury);
        }

        private static void WriteRecipe(BinaryWriter w, Recipe recipe)
        {
            WriteString(w, recipe.RecipeId);
            w.Write((int)recipe.Owner);
            WriteRecipeEntries(w, recipe.Inputs);
            WriteRecipeEntries(w, recipe.Outputs);
            w.Write(recipe.DurationTicks);
            w.Write(recipe.SkillGate);
            w.Write(recipe.MinQuality);
            if (recipe.BoundaryState == null)
            {
                w.Write(-1);
            }
            else
            {
                w.Write(recipe.BoundaryState.Length);
                for (int i = 0; i < recipe.BoundaryState.Length; i++)
                {
                    w.Write((int)recipe.BoundaryState[i].From);
                    w.Write((int)recipe.BoundaryState[i].To);
                }
            }
        }

        private static void WriteRecipeEntries(BinaryWriter w, RecipeEntry[] entries)
        {
            if (entries == null)
            {
                w.Write(-1);
                return;
            }
            w.Write(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                WriteString(w, entries[i].Key.BaseId);
                w.Write((int)entries[i].Key.State);
                w.Write(entries[i].Qty);
            }
        }
    }
}
