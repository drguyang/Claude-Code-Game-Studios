// `world_buildslots.cooked.bytes`(建造槽位骨架)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头布局契约)
//   · ADR-022 §三(导出契约:整数格 + 槽位约束 + `BakedInitial`)· ADR-015 §三(整数格)
//   写方严格镜像:`Editor.Tools.Bake/WorldBuildSlotsCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_buildslots.cooked
//     i32 world_size_x; i32 world_size_y; i32 world_size_z;
//     i32 row_count;
//     row_count × { i32 x; i32 y; i32 z; i32 allowed_mask; i32 baked_initial }
//
// ⚠️ **行序 = (x,y,z) 升序**(写方已排序;读方不依赖读入序做任何计算)。
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:`BuildSlotCatalog` / `BuildSlot` 住 `Sim`
//   (门 A 侧),而读 cooked 字节需要 `BinaryReader` + 头校验工具(`CookedReadUtil`,
//   已在 `Gameplay.Presentation`)。`Sim` 引用集恰 = {BCL, Sim.Contracts}(ADR-025 §①),
//   **不能**把 codec 放进 `Sim`。本件只做「字节 → 行」,行到 `BuildSlot` 的映射由装配侧
//   (`MinimalWorldFactory`)承担。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 行数 < 1 ⇒ 显式 throw(启动期硬失败,E-13 口径)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>槽位骨架的一行(解码产物;**不是** `Sim` 侧的 `BuildSlot`)。</summary>
    public readonly struct WorldBuildSlotRow
    {
        /// <summary>锚点格(整数格,ADR-015 §三)。</summary>
        public readonly WorldPos Cell;

        /// <summary>允许的 `SlotType` 位掩码(`1 &lt;&lt; 枚举序数`)。</summary>
        public readonly int AllowedMask;

        /// <summary>初始占用标记(`BakedInitial`;ADR-022 §三)。
        /// <para>⚠️ `BuildSlotCatalog` / `BuildSlot` **今日无承载该标记的字段** ⇒ 装配侧
        /// (`MinimalWorldFactory`)遇 <c>true</c> 时打 LogWarning,不让「数据声明了、运行期没应用」静默。</para></summary>
        public readonly bool BakedInitial;

        /// <summary>构造一行。</summary>
        public WorldBuildSlotRow(WorldPos cell, int allowedMask, bool bakedInitial)
        {
            Cell = cell; AllowedMask = allowedMask; BakedInitial = bakedInitial;
        }
    }

    /// <summary>`world_buildslots.cooked.bytes` 的解码结果(行集 + 声明范围 + 头槽版本)。</summary>
    public readonly struct WorldBuildSlotsDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_buildslots.cooked.bytes";

        /// <summary>行集(行序 = `(x,y,z)` 升序)。</summary>
        public readonly IReadOnlyList<WorldBuildSlotRow> Rows;

        /// <summary>声明范围 X(格数)。</summary>
        public readonly int Width;
        /// <summary>声明范围 Y(格数)。</summary>
        public readonly int Height;
        /// <summary>声明范围 Z(格数)。</summary>
        public readonly int Depth;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldBuildSlotsDataSet(
            IReadOnlyList<WorldBuildSlotRow> rows, int width, int height, int depth,
            uint schemaVersion, uint configVersion)
        {
            Rows = rows;
            Width = width; Height = height; Depth = depth;
            SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }
    }

    /// <summary>建造槽位骨架(<c>world_buildslots.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldBuildSlotsCookedCodec
    {
        /// <summary>产物名(与写方 `WorldBuildSlotsBake.CookedFileName` 同值,单一字面量)。</summary>
        public const string ProductName = "world_buildslots";

        /// <summary>本产物的作者态 schema_version 期望值(per-product,照 `CookedFormat.SchemaVersion` 先例)。</summary>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>合法 <c>allowed_mask</c> 的上界(含):覆盖 `Sim.World.SlotType` 的**实际**最大位。</summary>
        /// <remarks>⚠️ 从 <c>SlotType</c> 派生而非写死 `127`(2026-10-28 评审 A-22):
        /// 写死常量与枚举之间会重新长出一条无门的耦合 —— 那正是 A-21 的病根。</remarks>
        public static readonly int MaxAllowedMask = ComputeMaxAllowedMask();

        /// <summary>按 <c>SlotType</c> 的最大枚举值算位域上界。</summary>
        /// <exception cref="InvalidOperationException">枚举值不在 [1,30] 内(掩码不可表示)。</exception>
        private static int ComputeMaxAllowedMask()
        {
            int top = 0;
            foreach (Sim.World.SlotType t in (Sim.World.SlotType[])Enum.GetValues(typeof(Sim.World.SlotType)))
                if ((int)t > top) top = (int)t;

            if (top < 1 || top > 30)
                throw new InvalidOperationException(
                    $"[cooked] SlotType 最大枚举值 = {top} 超出位掩码可用范围 [1,30] —— " +
                    "写入契约与读取校验须同时改,不得让读方静默放行越界位。");

            return (1 << (top + 1)) - 1;
        }

        /// <summary>读槽位骨架。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 行数 &lt; 1 / 字段非法 —— 启动期硬失败。</exception>
        public static WorldBuildSlotsDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                int width = r.ReadInt32();
                int height = r.ReadInt32();
                int depth = r.ReadInt32();
                if (width < 1 || height < 1 || depth < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} world_size = ({width},{height},{depth}) —— 三维须 ≥ 1");

                int rowCount = r.ReadInt32();
                if (rowCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} 行数 = {rowCount} —— 须 ≥ 1" +
                        "(空骨架让 PlaceableChecker 恒 SlotNotFound = 建造系统静默失效;E-13)");

                var rows = new WorldBuildSlotRow[rowCount];
                for (int i = 0; i < rowCount; i++)
                {
                    int x = r.ReadInt32();
                    int y = r.ReadInt32();
                    int z = r.ReadInt32();
                    int allowedMask = r.ReadInt32();
                    int bakedInitial = r.ReadInt32();
                    // ⚠️ 值域**双侧**收紧(2026-10-28 代码评审 A-22):此前只拒 `== 0`,
                    //    负值 / 超出 `SlotType` 最大位的掩码一律放行;`bakedInitial != 0`
                    //    又把 2 / -1 / 任意非零静默收成 true。结构字段严、值域松 = 同一缺口的两个半边。
                    if (allowedMask <= 0 || allowedMask > MaxAllowedMask)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].allowed_mask = {allowedMask} —— " +
                            $"必须在 [1, {MaxAllowedMask}] 内(位域只覆盖 `SlotType` 的实际位;" +
                            "0/负值/越界位意味着写读契约漂移或字节损坏)");
                    if (bakedInitial != 0 && bakedInitial != 1)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].baked_initial = {bakedInitial} —— " +
                            "写方契约只写 0/1;非 0/1 必然是损坏或 bool→int 编码漂移" +
                            "(曾考虑按「非零即真」宽容,但那会把 0b10 之类的位掩码静默读成 true)");
                    rows[i] = new WorldBuildSlotRow(
                        new WorldPos(x, y, z), allowedMask, bakedInitial != 0);
                }

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_buildslots");

                return new WorldBuildSlotsDataSet(rows, width, height, depth, schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
