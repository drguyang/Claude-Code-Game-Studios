// `build_modules.cooked.bytes`(23 模块目录)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(20 字节头布局契约)
//   写方严格镜像:`Editor.Tools.Bake/BuildModulesCookedWriter.cs`(字段序逐字对齐)
//   表构造 / 结构性拒收的**单一出处**:本件把行喂给 `IModuleCatalog` 的实现,
//   不在解码期重做语义校验(与 `ClinicFurnitureCookedCodec` 同款分工)。
//
// ⚠️ 载荷布局(全部 little-endian;字符串 = i32 字节长 + UTF-8,长度 -1 = null):
//   build_modules.cooked
//     i32 row_count
//     row_count × { i32 module_id; i32 module_type; str module_type_name; str furn_type;
//                   i32 occupancy_count; occupancy_count × { i32 x; i32 y; i32 z };
//                   i32 cost; i32 refundable;
//                   i32 build_item_key }        // ← schema 2 追加在**行尾**(2026-10-26)
//
// ⚠️ **行序 = `module_id` 升序**(写方已排序;读方**不依赖**读入序做任何计算,
//   与写方注释「行序一律全序」同口径)。
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:`ModuleDefinition` / `IModuleCatalog`
//   住 `Sim`(门 A 侧),而读 cooked 字节需要 `BinaryReader` + 头校验工具
//   (`CookedReadUtil`,已在 `Gameplay.Presentation`)。`Sim` 引用集恰 = {BCL, Sim.Contracts}
//   (ADR-025 §①),**不能**把 codec 放进 `Sim`。本件只做「字节 → 行」,
//   行到 `ModuleDefinition` 的映射由 `BuildModulesCatalog` 承担(同装配)。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>模块目录的一行(解码产物;**不是** `Sim` 侧的 `ModuleDefinition`)。</summary>
    /// <remarks>
    /// 分两层是刻意的:本结构承载 cooked 里的**全部**字段(含 `ModuleTypeName` / `FurnType` /
    /// `Cost`),而 `ModuleDefinition` 是 23 的放置判定输入(六个字段,含 `Cost` ——
    /// 2026-10-25 TD-building-01 (b) 起;`ModuleTypeName` / `FurnType` 仍只留档于本结构)。
    /// 映射在 <see cref="BuildModulesCatalog"/> 里显式做,
    /// 让「哪些字段被 23 消费、哪些只是留档」可读。
    /// </remarks>
    public readonly struct BuildModuleRow
    {
        /// <summary>模块 id(`build_modules.json` 的 `module_id`)。</summary>
        public readonly int ModuleId;

        /// <summary>槽位类型枚举值(`BuildSlotCatalog.SlotType`;由作者态字符串名映射而来)。</summary>
        public readonly int ModuleType;

        /// <summary>槽位类型名(错误报文与跨侧对账用;不参与判定)。</summary>
        public readonly string ModuleTypeName;

        /// <summary>家具类型(24 词汇;`null` = 非家具)。⚠️ 与 <see cref="ModuleType"/> **同名不同物**。</summary>
        public readonly string FurnType;

        /// <summary>相对锚点的占用格(本地坐标)。</summary>
        public readonly IReadOnlyList<WorldPos> OccupancyLocal;

        /// <summary>建造消耗的物品(21a 物品表 ordinal;`category = build_part`)。
        /// <para>🔁 2026-10-26 新增(schema 2)。数量 = <see cref="Cost"/>,不另存。</para></summary>
        public readonly int BuildItemKey;

        /// <summary>建造成本(`cost`)。</summary>
        public readonly int Cost;

        /// <summary>是否可返还(1 = 是)。</summary>
        public readonly int Refundable;

        /// <summary>构造一行。</summary>
        public BuildModuleRow(int moduleId, int moduleType, string moduleTypeName, string furnType,
                              IReadOnlyList<WorldPos> occupancyLocal, int cost, int refundable,
                              int buildItemKey)
        {
            ModuleId = moduleId;
            ModuleType = moduleType;
            ModuleTypeName = moduleTypeName;
            FurnType = furnType;
            OccupancyLocal = occupancyLocal;
            Cost = cost;
            Refundable = refundable;
            BuildItemKey = buildItemKey;
        }
    }

    /// <summary>`build_modules.cooked.bytes` 的解码结果(行集 + 头槽 schema 版本)。</summary>
    public readonly struct BuildModulesDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "build_modules.cooked.bytes";

        /// <summary>行集(行序 = `module_id` 升序)。</summary>
        public readonly IReadOnlyList<BuildModuleRow> Rows;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五;与 ADR-010 §七 存档头比对)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public BuildModulesDataSet(IReadOnlyList<BuildModuleRow> rows, uint schemaVersion, uint configVersion)
        {
            Rows = rows;
            SchemaVersion = schemaVersion;
            ConfigVersion = configVersion;
        }
    }

    /// <summary>模块目录(<c>build_modules.cooked.bytes</c>)的运行期解码器。</summary>
    public static class BuildModulesCookedCodec
    {
        /// <summary>产物名(与写方 <c>BuildModulesBake.CookedFileName</c> 同值,单一字面量)。</summary>
        public const string ProductName = "build_modules";

        /// <summary>本产物的作者态 schema_version 期望值(per-product,照
        /// <see cref="CookedCodec.ExpectedItemSchemaVersion"/> 的先例)。
        /// <para>🔁 <b>2026-10-26:1 → 2</b> —— 行尾追加 <c>build_item_key</c>。
        /// 2 之前的产物**没有**该字段,按旧布局读会把行尾当成下一个字段 ⇒ 必须硬拒而非容忍。</para></summary>
        public const uint ExpectedSchemaVersion = 2u;

        /// <summary>读模块目录。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 行数 &lt; 1 / 字段非法 —— 启动期硬失败。</exception>
        public static BuildModulesDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                // 头槽的 schema 版本由 OpenChecked 校验魔数/长度,版本号本身由调用方比对;
                // 本 codec 只把 ConfigVersion 传出去(ADR-014 §五 的存档头比对输入)。
                int rowCount = r.ReadInt32();
                if (rowCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} 行数 = {rowCount} —— 须 ≥ 1" +
                        "(空目录让放置判定恒失败 = 建造系统静默失效;E-13)");

                var rows = new BuildModuleRow[rowCount];
                for (int i = 0; i < rowCount; i++)
                {
                    int moduleId = r.ReadInt32();
                    int moduleType = r.ReadInt32();
                    string moduleTypeName = CookedReadUtil.ReadString32(r, $"{ProductName} rows[{i}].module_type");
                    string furnType = CookedReadUtil.ReadString32(r, $"{ProductName} rows[{i}].furn_type");

                    int occupancyCount = r.ReadInt32();
                    if (occupancyCount < 1)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].occupancy_count = {occupancyCount} —— 须 ≥ 1" +
                            "(空足迹让放置判定 ② 恒失败;写方 Binder 应已拦)");

                    var occupancy = new WorldPos[occupancyCount];
                    for (int c = 0; c < occupancyCount; c++)
                    {
                        int x = r.ReadInt32();
                        int y = r.ReadInt32();
                        int z = r.ReadInt32();
                        occupancy[c] = new WorldPos(x, y, z);
                    }

                    int cost = r.ReadInt32();
                    int refundable = r.ReadInt32();
                    // 2026-10-26 追加在行尾(见写方同款注);schema 2 起存在。
                    int buildItemKey = r.ReadInt32();
                    rows[i] = new BuildModuleRow(moduleId, moduleType, moduleTypeName, furnType,
                                                 occupancy, cost, refundable, buildItemKey);
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                // schema 版本:写方把 `bound.SchemaVersion` 写进头槽 offset 8(CookedFormat 的
                // 「作者态 schema_version 的回声」)。`OpenChecked` 只校验魔数 / formatVersion /
                // 载荷长度,**不读** offset 8 ⇒ 本处自取。
                uint schemaVersion = ReadU32Le(bytes, 8);

                // 🔁 2026-10-26 补:**此前只把 schemaVersion 读出来、从不比对**(「有字段无门」)。
                // 那不致命当且仅当布局永不演进;而本次恰恰演进(行尾加了 build_item_key)——
                // 旧产物按新布局读会把最后一个字段读成垃圾且**不报错**。此门把该形态变成启动期硬失败。
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 build_modules");

                return new BuildModulesDataSet(rows, schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        /// <remarks>⚠️ 只读**已校验过**的字节(调用点必在 `OpenChecked` 之后);本方法不做长度守卫。</remarks>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
