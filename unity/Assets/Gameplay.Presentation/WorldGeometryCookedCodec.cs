// `world_geometry.cooked.bytes`(世界几何)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物)· `CookedFormat.cs`(20 字节头布局契约)
//   · ADR-022 §三(导出契约)· ADR-015 §三(整数格;格边长以整数毫米表达)
//   写方严格镜像:`Editor.Tools.Bake/WorldGeometryCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_geometry.cooked
//     i32 world_size_x; i32 world_size_y; i32 world_size_z;
//     i32 lattice_size_mm;
//     i32 cell_count;
//     cell_count × i32 walkable(0/1)
//
// ⚠️ **本文件为什么住在 `Gameplay.Presentation`**:`WorldGeometry` 住 `Sim`(门 A 侧),
//   而读 cooked 字节需要 `BinaryReader` + 头校验工具(`CookedReadUtil`,在本程序集)。
//   `Sim` 引用集恰 = {BCL, Sim.Contracts}(ADR-025 §①),**不能**把 codec 放进 `Sim`。
//   本件只做「字节 → 数据」,数据到 `WorldGeometry` 的映射由装配侧(`MinimalWorldFactory`)承担。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 尺寸非法 / 全不可走 ⇒ 显式 throw(启动期硬失败,E-13)。

using System;
using System.IO;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`world_geometry.cooked.bytes` 的解码结果(可走性 + 尺寸 + `LATTICE_SIZE`)。</summary>
    public readonly struct WorldGeometryDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_geometry.cooked.bytes";

        /// <summary>声明范围 X(格数)。</summary>
        public readonly int SizeX;
        /// <summary>声明范围 Y(格数)。</summary>
        public readonly int SizeY;
        /// <summary>声明范围 Z(格数)。</summary>
        public readonly int SizeZ;

        /// <summary>格边长 `LATTICE_SIZE`(整数毫米;GDD F-6-1 / ADR-015 §三)。</summary>
        /// <remarks>⚠️ **今日无消费者构造 `WorldLatticeParams`**:它的 6 个数值里只有
        /// `LatticeSizeMm` 与 K_speed 表来自产物,其余四个(`SPEED_MODE_MAX` / `K_CONTEXT_MAX` /
        /// `MAX_DT` / `SAFETY_MARGIN`)**全归用户数值轮** ⇒ 装配期拼不出完整参数对象。
        /// 本字段由 codec **校验**(须 &gt; 0)并如实交出来,接线归数值轮之后的装配轮。</remarks>
        public readonly int LatticeSizeMm;

        /// <summary>每格可走性(0/1;索引 = `(x*SizeY + y)*SizeZ + z`)。</summary>
        public readonly int[] Walkable;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldGeometryDataSet(int sizeX, int sizeY, int sizeZ, int latticeSizeMm,
                                    int[] walkable, uint schemaVersion, uint configVersion)
        {
            SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ;
            LatticeSizeMm = latticeSizeMm; Walkable = walkable;
            SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }

        /// <summary>世界总格数。</summary>
        public int TotalCells => SizeX * SizeY * SizeZ;
    }

    /// <summary>世界几何(<c>world_geometry.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldGeometryCookedCodec
    {
        /// <summary>产物名(与写方 `WorldGeometryBake.CookedFileName` 同值,单一字面量)。</summary>
        public const string ProductName = "world_geometry";

        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.GeometrySchemaVersion` **必须同值** ——
        /// 两者不可互相引用(编辑期程序集不进构建),但绑定器的 `ReadSchemaVersion` 以**本常量**
        /// 为期望值 ⇒ 导出侧写成 2 而这里还是 1 时,烘焙**当场**报 `schema_version ≠ 期望`。
        /// 那是这对「两份字面量」的机械守护。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读世界几何。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 长度不符 / 尺寸非法 / 全不可走 —— 启动期硬失败。</exception>
        public static WorldGeometryDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                // ⚠️ 版本门排在读载荷**之前**(承 WorldBuildSlotsCookedCodec 的教训):
                //    否则旧布局产物会先在某个 ReadInt32 撞 EndOfStreamException,
                //    运维看到的是「流意外结束」而不是「版本不符,请重烘」。
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_geometry");

                int sizeX = r.ReadInt32();
                int sizeY = r.ReadInt32();
                int sizeZ = r.ReadInt32();
                if (sizeX < 1 || sizeY < 1 || sizeZ < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} world_size = ({sizeX},{sizeY},{sizeZ}) —— 三维须 ≥ 1");
                long total = (long)sizeX * sizeY * sizeZ;
                if (total > int.MaxValue)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} 格总数 {total} 超出 int 域 —— 一维索引不可表示");

                int latticeSizeMm = r.ReadInt32();
                if (latticeSizeMm <= 0)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} lattice_size_mm = {latticeSizeMm} —— 须 > 0" +
                        "(GDD F-6-1:SPEED_MAX × MAX_DT × SAFETY_MARGIN ≤ LATTICE_SIZE," +
                        "右端为 0 时不等式恒假但无人求值 ⇒ 静默失效)");

                int cellCount = r.ReadInt32();
                if (cellCount != total)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} cell_count = {cellCount},与 world_size 的格总数 {total} 不符 —— " +
                        "长度字段与尺寸字段对不上 = 字节损坏或写读契约漂移");

                var walkable = new int[cellCount];
                int walkableCount = 0;
                for (int i = 0; i < cellCount; i++)
                {
                    int v = r.ReadInt32();
                    if (v != 0 && v != 1)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} walkable[{i}] = {v} —— 写方契约只写 0/1;" +
                            "非 0/1 必然是损坏或 bool→int 编码漂移(按「非零即真」宽容会把 0b10 读成 true)");
                    walkable[i] = v;
                    if (v == 1) walkableCount++;
                }

                if (walkableCount == 0)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} 全部 {cellCount} 格不可通行 —— " +
                        "「哪里走不了」这条判据会零观察面,而症状(玩家卡在出生点)会被误诊为控制器故障;E-13");

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                return new WorldGeometryDataSet(
                    sizeX, sizeY, sizeZ, latticeSizeMm, walkable, schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
