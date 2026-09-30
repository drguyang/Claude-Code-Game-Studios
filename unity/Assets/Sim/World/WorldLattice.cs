// 权威来源:ADR-015 §三(单一整数格)· ADR-022(关卡工具)· Story 001(世界格与可走性基础)
//
// 核心机制:
//   - LATTICE_SIZE / SPEED_MAX / MAX_DT / SAFETY_MARGIN 防隧穿硬断言(F-6-1)
//   - K_TERRAIN_MAX = max(K_speed 表),非手填(F-6-2)
//   - TerrainCell { walkable, kSpeedIdx }
//   - WorldGeometry 装载入口(来自世界_geometry / world_terrain 烘焙产物)
//   - 全量程安全检查(int64 安全区)

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>世界格参数(装载期校验)。</summary>
    public readonly struct WorldLatticeParams
    {
        public readonly int LatticeSize;       // 每格边长(整数, > 0)
        public readonly int SpeedMax;          // 全局速度上限(格/s, > 0)
        public readonly int MaxDt;             // 最大时间步长(ms, > 0)
        public readonly int SafetyMargin;      // 安全系数(> 1)
        public readonly int KTerrainMax;       // = max(K_speed 表), >= SpeedMax
        public readonly int[] KSpeedTable;     // 地形速度档位表(单调非递减)

        /// <summary>
        /// 构造世界格参数并执行全部装载期硬断言。
        /// </summary>
        /// <param name="latticeSize">每格边长(> 0)</param>
        /// <param name="speedMax">全局速度上限(> 0)</param>
        /// <param name="maxDt">最大时间步长(ms, > 0)</param>
        /// <param name="safetyMargin">安全系数(> 1)</param>
        /// <param name="kSpeedTable">地形速度档位表(非空,单调非递减)</param>
        /// <exception cref="ArgumentException">任一断言失败时 throw。</exception>
        public WorldLatticeParams(
            int latticeSize,
            int speedMax,
            int maxDt,
            int safetyMargin,
            int[] kSpeedTable)
        {
            if (latticeSize <= 0)
                throw new ArgumentException($"LATTICE_SIZE 必须 > 0,实际={latticeSize}", nameof(latticeSize));
            if (speedMax <= 0)
                throw new ArgumentException($"SPEED_MAX 必须 > 0,实际={speedMax}", nameof(speedMax));
            if (maxDt <= 0)
                throw new ArgumentException($"MAX_DT 必须 > 0,实际={maxDt}", nameof(maxDt));
            if (safetyMargin <= 1)
                throw new ArgumentException($"SAFETY_MARGIN 必须 > 1,实际={safetyMargin}", nameof(safetyMargin));

            if (kSpeedTable == null || kSpeedTable.Length == 0)
                throw new ArgumentException("K_speed 表不能为空(空表硬失败)", nameof(kSpeedTable));

            for (int i = 1; i < kSpeedTable.Length; i++)
            {
                if (kSpeedTable[i] < kSpeedTable[i - 1])
                    throw new ArgumentException($"K_speed 表必须单调非递减,索引 {i - 1}→{i} 违反({kSpeedTable[i - 1]} → {kSpeedTable[i]})", nameof(kSpeedTable));
            }

            LatticeSize = latticeSize;
            SpeedMax = speedMax;
            MaxDt = maxDt;
            SafetyMargin = safetyMargin;
            KSpeedTable = kSpeedTable;

            // F-6-1: 防隧穿关系 = LATTICE_SIZE >= SPEED_MAX × MAX_DT × SAFETY_MARGIN
            long minLattice = (long)speedMax * maxDt * safetyMargin;
            if (latticeSize < minLattice)
                throw new ArgumentException(
                    $"F-6-1 防隧穿失败: LATTICE_SIZE({latticeSize}) < SPEED_MAX({speedMax}) × MAX_DT({maxDt}) × SAFETY_MARGIN({safetyMargin}) = {minLattice}",
                    nameof(latticeSize));

            // F-6-2: K_TERRAIN_MAX = max(K_speed 表)
            int kMax = kSpeedTable[0];
            for (int i = 1; i < kSpeedTable.Length; i++)
            {
                if (kSpeedTable[i] > kMax)
                    kMax = kSpeedTable[i];
            }
            KTerrainMax = kMax;

            if (KTerrainMax < SpeedMax)
                throw new ArgumentException(
                    $"K_TERRAIN_MAX({KTerrainMax}) < SPEED_MAX({SpeedMax}):最高地形速度不能低于全局速度上限",
                    nameof(kSpeedTable));
        }

        /// <summary>
        /// 验证世界坐标量程是否在 int64 安全区内(无溢出域)。
        /// </summary>
        public static void ValidateWorldExtent(int worldSizeX, int worldSizeY, int worldSizeZ)
        {
            long extent = (long)worldSizeX * worldSizeY * worldSizeZ;
            const long safeZone = 1L << 40; // 约 1T 格
            if (extent > safeZone)
                throw new ArgumentException(
                    $"世界坐标量程超出 int64 安全区: extent={extent} > {safeZone}");
        }
    }

    /// <summary>地形格数据。</summary>
    public readonly struct TerrainCell
    {
        public readonly bool Walkable;       // 可走性
        public readonly int KSpeedIdx;       // 速度档位索引(>= 0, < K_speed 表长度)

        public TerrainCell(bool walkable, int kSpeedIdx)
        {
            Walkable = walkable;
            KSpeedIdx = kSpeedIdx;
        }
    }

    /// <summary>世界几何数据结构(烘焙产物装载结果)。</summary>
    public readonly struct WorldGeometry
    {
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly int SizeZ;
        public readonly TerrainCell[,,] Terrain;
        public readonly int[] PoiGrid;       // 一维索引: POI id 或 -1(无POI)
        public readonly int[] ResourceGrid;  // 一维索引: 资源 id 或 -1(无资源)

        public WorldGeometry(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            Terrain = new TerrainCell[sizeX, sizeY, sizeZ];
            PoiGrid = new int[sizeX * sizeY * sizeZ];
            ResourceGrid = new int[sizeX * sizeY * sizeZ];

            // 初始化无 POI / 无资源
            for (int i = 0; i < PoiGrid.Length; i++)
            {
                PoiGrid[i] = -1;
                ResourceGrid[i] = -1;
            }
        }

        /// <summary>
        /// WorldPos → 一维索引(PoiGrid / ResourceGrid 用)。
        /// </summary>
        public int ToIndex(WorldPos p)
        {
            return (p.X * SizeY + p.Y) * SizeZ + p.Z;
        }

        /// <summary>
        /// 安全读取地形格(越界返回 walkable=false)。
        /// </summary>
        public TerrainCell GetTerrain(WorldPos p)
        {
            if (p.X < 0 || p.X >= SizeX || p.Y < 0 || p.Y >= SizeY || p.Z < 0 || p.Z >= SizeZ)
                return new TerrainCell(false, 0);
            return Terrain[p.X, p.Y, p.Z];
        }

        /// <summary>
        /// 查询 POI id(-1 = 无)。
        /// </summary>
        public int GetPoiId(WorldPos p)
        {
            int idx = ToIndex(p);
            if (idx < 0 || idx >= PoiGrid.Length)
                return -1;
            return PoiGrid[idx];
        }

        /// <summary>
        /// 查询资源 id(-1 = 无)。
        /// </summary>
        public int GetResourceId(WorldPos p)
        {
            int idx = ToIndex(p);
            if (idx < 0 || idx >= ResourceGrid.Length)
                return -1;
            return ResourceGrid[idx];
        }
    }
}
