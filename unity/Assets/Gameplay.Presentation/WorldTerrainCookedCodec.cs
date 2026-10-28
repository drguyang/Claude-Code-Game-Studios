// `world_terrain.cooked.bytes`(地形表 · `TERRAIN_TABLE`)的**运行期解码器**。
//
// 权威来源:
//   ADR-014 §二/§五(运行期读烘焙产物;`Fix` = raw `long`)· `CookedFormat.cs`(20 字节头契约)
//   · ADR-022 §三(导出契约)· GDD `world-and-ecozones.md` **F-6-2** / **AC-6-08**
//   · ADR-015 §一之补(`slopeLimit` / `stepOffset` 由关卡工具量化后落盘)
//   写方严格镜像:`Editor.Tools.Bake/WorldTerrainCookedWriter.cs`(字段序逐字对齐)
//
// ⚠️ 载荷布局(全部 little-endian;固定字段顺序):
//   world_terrain.cooked
//     i32 row_count
//     row_count × { i32 terrain_id; i32 k_speed; i64 k_accel_raw; i64 k_decel_raw }
//     i32 world_size_x; i32 world_size_y; i32 world_size_z
//     i32 cell_count
//     cell_count × i32 speed_index
//     i32 slope_limit_deg_x100
//     i32 step_offset_mm
//
// ⚠️ **本件是 AC-5(空表 = 装载期硬失败)的第二道门**:烘焙期 `WorldTerrainBinder` 已拒空表,
//   但字节可以被损坏 / 被手工替换 ⇒ 读侧必须**独立**拒 `row_count < 1`,
//   且**在分配 / 读任何行之前**就拒(否则空表会静默走成「零行地形」)。
//
// ⚠️ **`KTerrainMax` 是本解码器派生的**(F-6-2:`max over terrain_id of k_speed`),
//   **不是**读出来的 —— 产物里没有这个字段。「派生而非手填」是 AC-6-07 的口径。
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 值域越界 ⇒ 显式 throw(启动期硬失败,E-13)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;   // Fix / LatticeParamQuantization

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>`TERRAIN_TABLE` 的一行(解码产物;与 `Sim.World.TerrainSpeedRow` 同形但不同件 ——
    /// 本件还带 `TerrainId`,而 sim 侧只用表**下标**)。</summary>
    public readonly struct WorldTerrainRow
    {
        /// <summary>地貌材质 id(≥ 0;与表下标无必然关系 —— 但写方按 id 升序排,故本实现里二者相等)。</summary>
        public readonly int TerrainId;

        /// <summary>速度档位乘数(`> 0`;基准地貌 = 1)。</summary>
        public readonly int KSpeed;

        /// <summary>加速乘数(`> 0`;归 1 的 F-1-3 消费)。</summary>
        public readonly Fix KAccel;

        /// <summary>减速乘数(`> 0`;归 1 的 F-1-3 消费)。</summary>
        public readonly Fix KDecel;

        /// <summary>构造一行。</summary>
        public WorldTerrainRow(int terrainId, int kSpeed, Fix kAccel, Fix kDecel)
        {
            TerrainId = terrainId; KSpeed = kSpeed; KAccel = kAccel; KDecel = kDecel;
        }
    }

    /// <summary>`world_terrain.cooked.bytes` 的解码结果(表 + 每格下标 + 控制器参数)。</summary>
    public readonly struct WorldTerrainDataSet
    {
        /// <summary>cooked 产物名(Addressables 地址同此名)。</summary>
        public const string CookedFileName = "world_terrain.cooked.bytes";

        /// <summary>`TERRAIN_TABLE`(行序 = `terrain_id` 升序;**下标**即 `CellSpeedIndex` 的取值域)。</summary>
        public readonly IReadOnlyList<WorldTerrainRow> Rows;

        /// <summary>声明范围 X(格数;须与几何产物相等,由装配侧断言)。</summary>
        public readonly int SizeX;
        /// <summary>声明范围 Y(格数)。</summary>
        public readonly int SizeY;
        /// <summary>声明范围 Z(格数)。</summary>
        public readonly int SizeZ;

        /// <summary>每格的**表内下标**(写入 `TerrainCell.KSpeedIdx`)。</summary>
        public readonly int[] CellSpeedIndex;

        /// <summary>`slopeLimit` 的量化值(0.01 度;量子见 `LatticeParamQuantization`)。</summary>
        public readonly int SlopeLimitDegX100;

        /// <summary>`stepOffset` 的量化值(毫米)。</summary>
        public readonly int StepOffsetMm;

        /// <summary>产物头声明的 schema 版本(ADR-014 §五)。</summary>
        public readonly uint SchemaVersion;

        /// <summary>源内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public readonly uint ConfigVersion;

        /// <summary>构造。</summary>
        public WorldTerrainDataSet(
            IReadOnlyList<WorldTerrainRow> rows, int sizeX, int sizeY, int sizeZ,
            int[] cellSpeedIndex, int slopeLimitDegX100, int stepOffsetMm,
            uint schemaVersion, uint configVersion)
        {
            Rows = rows; SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ;
            CellSpeedIndex = cellSpeedIndex;
            SlopeLimitDegX100 = slopeLimitDegX100; StepOffsetMm = stepOffsetMm;
            SchemaVersion = schemaVersion; ConfigVersion = configVersion;
        }

        /// <summary>
        /// `K_TERRAIN_MAX := max over terrain_id of K_speed`(GDD **F-6-2**;**派生而非手填**,AC-6-07)。
        /// <para>消费者 = `WorldLatticeParams` 的 F-6-1 展开式
        /// `SPEED_MAX = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX`(装配归数值轮之后)。</para>
        /// </summary>
        public int KTerrainMax
        {
            get
            {
                int max = Rows[0].KSpeed;
                for (int i = 1; i < Rows.Count; i++)
                    if (Rows[i].KSpeed > max) max = Rows[i].KSpeed;
                return max;
            }
        }
    }

    /// <summary>地形表(<c>world_terrain.cooked.bytes</c>)的运行期解码器。</summary>
    public static class WorldTerrainCookedCodec
    {
        /// <summary>产物名(与写方 `WorldTerrainBake.CookedFileName` 同值,单一字面量)。</summary>
        public const string ProductName = "world_terrain";

        /// <summary>本产物的作者态 schema_version 期望值(per-product 口径)。</summary>
        /// <remarks>⚠️ 与 `Editor.Tools.Level.LevelExporter.TerrainSchemaVersion` **必须同值**
        /// —— 守护机制见 <see cref="WorldGeometryCookedCodec.ExpectedSchemaVersion"/> 的同款注。</remarks>
        public const uint ExpectedSchemaVersion = 1u;

        /// <summary>读地形表。</summary>
        /// <param name="bytes">cooked 字节(含 20 字节头)。</param>
        /// <exception cref="InvalidDataException">头部损坏 / 空表 / 长度不符 / 值域越界 —— 启动期硬失败。</exception>
        public static WorldTerrainDataSet Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, ProductName, out uint configVersion))
            {
                uint schemaVersion = ReadU32Le(bytes, 8);
                if (schemaVersion != ExpectedSchemaVersion)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} schemaVersion = {schemaVersion},期望 " +
                        $"{ExpectedSchemaVersion} —— 布局演进后不得按旧契约读(E-13)。" +
                        "重烘:大医精诚/数据管线/烘焙 world_terrain");

                // ── AC-5 的第二道门:空表硬失败,且**在任何行读取之前** ──────────────
                int rowCount = r.ReadInt32();
                if (rowCount < 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} row_count = {rowCount} —— TERRAIN_TABLE 不得为空" +
                        "(GDD F-6-2:`K_TERRAIN_MAX := max over terrain_id of k_speed`,max over 空集**无定义**)。" +
                        "拒绝降级为「全 walkable 无地形」——那会把「没有数据」伪装成「处处可走」;E-13");

                var rows = new WorldTerrainRow[rowCount];
                int baselineRows = 0;
                int prevTerrainId = -1;
                for (int i = 0; i < rowCount; i++)
                {
                    int terrainId = r.ReadInt32();
                    int kSpeed = r.ReadInt32();
                    long kAccelRaw = r.ReadInt64();
                    long kDecelRaw = r.ReadInt64();

                    if (terrainId < 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].terrain_id = {terrainId} —— 须 ≥ 0");
                    if (i > 0 && terrainId <= prevTerrainId)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].terrain_id = {terrainId} 未严格升序" +
                            $"(前行 {prevTerrainId}) —— 写方按 id 升序排;乱序即字节损坏或契约漂移");
                    prevTerrainId = terrainId;

                    if (kSpeed <= 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].k_speed = {kSpeed} —— 须 > 0(AC-6-08;" +
                            "不可通行用 walkable = 0 表达,速度 0 会让 F-6-1 的隧穿断言恒绿)");
                    if (kAccelRaw <= 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].k_accel raw = {kAccelRaw} —— 须 > 0(AC-6-08)");
                    if (kDecelRaw <= 0)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} rows[{i}].k_decel raw = {kDecelRaw} —— 须 > 0(AC-6-08)");

                    if (kSpeed == 1) baselineRows++;
                    rows[i] = new WorldTerrainRow(terrainId, kSpeed, new Fix(kAccelRaw), new Fix(kDecelRaw));
                }

                if (baselineRows != 1)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} 中 k_speed == 1(基准地貌)须**恰好一行**,实际 = {baselineRows} —— " +
                        "零行 ⇒ 没有基准可参照;多行 ⇒ 「哪一行是基准」成为隐式规则");

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

                int cellCount = r.ReadInt32();
                if (cellCount != total)
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} cell_count = {cellCount},与 world_size 的格总数 {total} 不符");

                var cellSpeedIndex = new int[cellCount];
                for (int i = 0; i < cellCount; i++)
                {
                    int idx = r.ReadInt32();
                    if (idx < 0 || idx >= rowCount)
                        throw new InvalidDataException(
                            $"[cooked] {ProductName} cell_speed_index[{i}] = {idx} —— " +
                            $"须在 [0, {rowCount}) 内(查表会取出越界行)");
                    cellSpeedIndex[i] = idx;
                }

                int slopeLimit = r.ReadInt32();
                if (!LatticeParamQuantization.IsValidSlopeLimit(slopeLimit))
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} slope_limit_deg_x100 = {slopeLimit} —— 须在 " +
                        $"(0, {LatticeParamQuantization.SlopeLimitMaxUnits}] 内(量子 0.01 度)");

                int stepOffset = r.ReadInt32();
                if (!LatticeParamQuantization.IsValidStepOffset(stepOffset))
                    throw new InvalidDataException(
                        $"[cooked] {ProductName} step_offset_mm = {stepOffset} —— 须在 " +
                        $"[0, {LatticeParamQuantization.StepOffsetMaxUnits}] 内(量子 1 mm)");

                CookedReadUtil.AssertFullyConsumed(r, ProductName);

                return new WorldTerrainDataSet(
                    Array.AsReadOnly(rows), sizeX, sizeY, sizeZ,
                    cellSpeedIndex, slopeLimit, stepOffset, schemaVersion, configVersion);
            }
        }

        /// <summary>读头部第 3 个 u32(offset 8 = 作者态 schema_version 的回声)。</summary>
        private static uint ReadU32Le(byte[] b, int offset) =>
            (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
