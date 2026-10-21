// 权威来源:ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(布局契约)
//          写方严格镜像:`Editor.Tools.Bake/ItemDatabaseFurnitureCookedWriter.cs`
//          表构造 / 结构性拒收**单一出处**:`DaYiJingCheng.Sim.Clinic.ClinicFurnitureCatalogue`
//          —— 本件**不重做**这些校验,零漂移。
//
// ⚠️ 载荷布局(全部 little-endian;字符串 = i32 字节长 + UTF-8,长度 -1 = null):
//    item_database_furniture: i64 tier_max; i32 row_count;
//                             row_count × { string type; i32 tier; i64 e_env_raw }
//
// ⚠️ 行序 = 写方的 ordinal 排序结果;读方**不依赖**读入序做任何计算。
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 构造拒收 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>21a 家具目录(<c>item_database_furniture.cooked.bytes</c>)的运行期解码器。
    /// <para>解码产物 = Sim 侧 <see cref="ClinicFurnitureCatalogue"/>(`FURN_TYPES` 闭集的运行期载体)。</para>
    /// </summary>
    public static class ClinicFurnitureCookedCodec
    {
        /// <summary>读家具目录。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 行数 &lt; 1 / 表构造拒收
        /// (type 空或重复 · tier 域外 · |e_env| 超 C_max · tier_max &lt; 1)—— 启动期硬失败。</exception>
        public static ClinicFurnitureDataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(
                       bytes, "item_database_furniture", out uint configVersion))
            {
                long tierMax = r.ReadInt64();
                int rowCount = r.ReadInt32();
                if (rowCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] item_database_furniture 行数 = {rowCount} —— 须 ≥ 1" +
                        "(空 FURN_TYPES 让全部房间谓词恒不匹配 = 静默失效;E-13)");

                var rows = new ClinicFurnitureEntry[rowCount];
                for (int i = 0; i < rowCount; i++)
                {
                    string type = CookedReadUtil.ReadString32(r, $"item_database_furniture rows[{i}].type");
                    int tier = r.ReadInt32();
                    long eEnvRaw = r.ReadInt64();
                    rows[i] = new ClinicFurnitureEntry(type, tier, new Fix(eEnvRaw));
                }

                CookedReadUtil.AssertFullyConsumed(r, "item_database_furniture");

                try
                {
                    var catalogue = new ClinicFurnitureCatalogue(rows, tierMax);
                    return new ClinicFurnitureDataSet(catalogue, configVersion);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"[cooked] item_database_furniture 表构造被拒:{ex.Message}", ex);
                }
            }
        }
    }

    /// <summary>家具目录烘焙产物的解码形。</summary>
    public struct ClinicFurnitureDataSet
    {
        /// <summary>目录(可直接交给 <see cref="ClinicFurnitureCatalogue.TryGet"/> 查询)。</summary>
        public ClinicFurnitureCatalogue Catalogue;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public ClinicFurnitureDataSet(ClinicFurnitureCatalogue catalogue, uint configVersion)
        {
            Catalogue = catalogue; ConfigVersion = configVersion;
        }
    }
}
