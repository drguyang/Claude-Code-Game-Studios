// 权威来源:ADR-015 §三(单一整数格)· ADR-022(关卡工具)· Story 001(世界格与可走性基础)
//          GDD world-and-ecozones.md F-6-1 / F-6-2 · AC-6-01…09
//
// 核心机制:
//   - F-6-1 防隧穿硬断言: LATTICE_SIZE ≥ SPEED_MAX × MAX_DT × SAFETY_MARGIN
//     SPEED_MAX **不另收裸值**,按 GDD :511 的现裁展开式派生
//       = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX
//     —— 另收一个裸 SPEED_MAX 会把「6 自己的表锁着 6 自己的几何常量」这条耦合藏起来,
//     而那正是 EC-6-14(调 K_speed 抬高 LATTICE_SIZE 下界)的代数来源。
//   - F-6-2: K_TERRAIN_MAX = max(K_speed 表),非手填;空表 = 装载期硬失败
//   - AC-6-08: 每行 K_speed > 0(不可通行走 walkable=false,不用速度 0)
//              + K_speed == 1 恰好一行(基准地貌存在且唯一)
//   - 世界坐标量程守卫(承 enemy-ai.md F-27-1:断言对象 = 坐标量程 W,不是半径)
//
// ⚠️ 单位约定:本结构全长度量以「格」计(ADR-015 §三 单一整数格)⇒
//    LATTICE_SIZE = 格 · SPEED_MAX = 格/s · MAX_DT = ms · K_* = 无量纲整数乘数。
//    GDD F-6-1 表头的 float(m)/(m/s)/(s) 是同一等式的浮点表达,此处是它的整数量子化;
//    ms 与 /s 的桥 = <see cref="MsPerSecond"/>,不可省略(省略即右端缩小 1000 倍 = 静默失守)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>世界格参数(装载期校验)。长度单位一律「格」。</summary>
    public readonly struct WorldLatticeParams
    {
        /// <summary>ms/s 单位桥。<see cref="MaxDtMs"/> 与 <see cref="SpeedMax"/> 差 1000 倍,不可省。</summary>
        public const int MsPerSecond = 1000;

        /// <summary>每格边长(格,> 0;下界由 F-6-1 给出)。</summary>
        public readonly int LatticeSize;

        /// <summary>速度档上限(无量纲整数乘数,> 0;来自 1 玩家控制器的声明)。</summary>
        public readonly int SpeedModeMax;

        /// <summary>语境速度乘数上限(无量纲整数乘数,> 0;来自 24)。</summary>
        public readonly int KContextMax;

        /// <summary>单帧 dt 钳位上限(ms,> 0;来自 1)。</summary>
        public readonly int MaxDtMs;

        /// <summary>安全系数(整数,> 1;取等号即失败,AC-6-05)。</summary>
        public readonly int SafetyMargin;

        /// <summary>= max(K_speed 表),由 F-6-2 派生,非手填。</summary>
        public readonly int KTerrainMax;

        /// <summary>= <see cref="SpeedModeMax"/> × <see cref="KTerrainMax"/> × <see cref="KContextMax"/>(格/s,GDD :511)。</summary>
        public readonly int SpeedMax;

        /// <summary>地形速度档位表(K_speed 各行)。构造时复制,不与调用方共享数组。</summary>
        public readonly int[] KSpeedTable;

        /// <summary>
        /// 构造世界格参数并执行全部装载期硬断言。
        /// </summary>
        /// <param name="latticeSize">每格边长(格,> 0)</param>
        /// <param name="speedModeMax">速度档上限(> 0;来自 1)</param>
        /// <param name="kContextMax">语境乘数上限(> 0;来自 24)</param>
        /// <param name="maxDtMs">单帧 dt 钳位上限(ms,> 0;来自 1)</param>
        /// <param name="safetyMargin">安全系数(> 1)</param>
        /// <param name="kSpeedTable">K_speed 表(非空;每行 > 0;恰好一行 == 1)</param>
        /// <exception cref="ArgumentNullException">表为 null。</exception>
        /// <exception cref="ArgumentException">任一装载期断言失败。</exception>
        public WorldLatticeParams(
            int latticeSize,
            int speedModeMax,
            int kContextMax,
            int maxDtMs,
            int safetyMargin,
            int[] kSpeedTable)
        {
            // F-6-1 前置守卫(GDD :492-500 的 B2 静默失败面):
            // 任一因子 ≤ 0(或 SAFETY_MARGIN ≤ 1)⇒ 右端退化 ⇒ 不等式变真空命题。
            if (latticeSize <= 0)
                throw new ArgumentException($"LATTICE_SIZE 必须 > 0,实际={latticeSize}", nameof(latticeSize));
            if (speedModeMax <= 0)
                throw new ArgumentException($"SPEED_MODE_MAX 必须 > 0,实际={speedModeMax}", nameof(speedModeMax));
            if (kContextMax <= 0)
                throw new ArgumentException($"K_CONTEXT_MAX 必须 > 0,实际={kContextMax}", nameof(kContextMax));
            if (maxDtMs <= 0)
                throw new ArgumentException($"MAX_DT 必须 > 0,实际={maxDtMs}", nameof(maxDtMs));
            if (safetyMargin <= 1)
                throw new ArgumentException($"SAFETY_MARGIN 必须 > 1(取等号即失败),实际={safetyMargin}", nameof(safetyMargin));

            if (kSpeedTable == null)
                throw new ArgumentNullException(nameof(kSpeedTable), "K_speed 表不可为 null");
            if (kSpeedTable.Length == 0)
                throw new ArgumentException("K_speed 表不能为空(max over 空集无定义,AC-6-06)", nameof(kSpeedTable));

            // AC-6-08:每行 K_speed > 0 + 基准地貌恰好一行。
            // K_speed ≤ 0 不是「很慢」而是「走不动」⇒ 静默绕过 1 的隧穿检测(速度 0 ⇒ 永不跨格 ⇒ 断言恒绿)。
            int baselineRows = 0;
            for (int i = 0; i < kSpeedTable.Length; i++)
            {
                if (kSpeedTable[i] <= 0)
                    throw new ArgumentException(
                        $"K_speed 表第 {i} 行必须 > 0(不可通行用 walkable=false 表达,不用速度 0),实际={kSpeedTable[i]}",
                        nameof(kSpeedTable));
                if (kSpeedTable[i] == 1)
                    baselineRows++;
            }
            if (baselineRows != 1)
                throw new ArgumentException(
                    $"K_speed == 1(基准地貌)必须恰好一行,实际={baselineRows}", nameof(kSpeedTable));

            // 复制入自有数组:readonly struct 持引用字段,与调用方共享即外部可改 ⊢ 派生量失效。
            var table = new int[kSpeedTable.Length];
            Array.Copy(kSpeedTable, table, kSpeedTable.Length);

            // F-6-2: K_TERRAIN_MAX = max over rows(AC-6-07:派生而非手填)
            int kMax = table[0];
            for (int i = 1; i < table.Length; i++)
            {
                if (table[i] > kMax)
                    kMax = table[i];
            }

            // GDD :511 展开式。三个乘数各自已验 > 0;乘积超 int 域 ⇒ 不是「钳位」而是配置错误。
            long modeCtx = (long)speedModeMax * kContextMax;   // ≤ 2^62,无溢
            if (modeCtx > int.MaxValue / kMax)
                throw new ArgumentException(
                    $"SPEED_MAX 派生值 SPEED_MODE_MAX({speedModeMax}) × K_TERRAIN_MAX({kMax}) × K_CONTEXT_MAX({kContextMax}) 超出 int 域",
                    nameof(speedModeMax));

            LatticeSize = latticeSize;
            SpeedModeMax = speedModeMax;
            KContextMax = kContextMax;
            MaxDtMs = maxDtMs;
            SafetyMargin = safetyMargin;
            KTerrainMax = kMax;
            SpeedMax = (int)(modeCtx * kMax);
            KSpeedTable = table;

            // F-6-1: LATTICE_SIZE ≥ SPEED_MAX × MAX_DT × SAFETY_MARGIN
            // 判定写成 ceil(SPEED_MAX × MAX_DT_ms × MARGIN / 1000) ≤ LATTICE_SIZE,
            // 两侧 ×1000 移项成整数恒等式 —— 免去 float,也免去「先乘出 2^93 再回绕」的假绿。
            // 位移分子(格·ms/s)与阈值各自单独无溢:SPEED_MAX×MAX_DT ≤ 2^62;LATTICE_SIZE×1000 ≤ 2^41。
            long displacement = (long)SpeedMax * MaxDtMs;
            long allowed = (long)LatticeSize * MsPerSecond;
            if (displacement > allowed / SafetyMargin)
                throw new ArgumentException(
                    $"F-6-1 防隧穿失败: LATTICE_SIZE({LatticeSize}) < "
                    + $"SPEED_MAX({SpeedMax} 格/s) × MAX_DT({MaxDtMs} ms) × SAFETY_MARGIN({SafetyMargin})",
                    nameof(latticeSize));
        }

        /// <summary>
        /// 世界坐标量程守卫:每轴最大绝对坐标(世界半径 W)须使 <c>3 × (2W)² &lt; 2^63</c>
        /// —— <c>d2</c> 三项各可达 <c>(2W)²</c>(enemy-ai.md F-27-1 的同型断言,断言对象 = 量程非半径)。
        /// <para>判据实现为逐轴 <c>|坐标| ≤ <see cref="MaxWorldHalfExtent"/></c>:取满足
        /// <c>3 × (2W)² &lt; 2^63</c> 的最大 2 的幂 —— <c>W = 2^29</c> 时三项和上界
        /// <c>3 × 2^60 ≈ 3.46e18 &lt; 9.22e18 = 2^63</c>(GDD 实数解 <c>W &lt; 7.6e8</c>,取 2 的幂便于整数比较)。</para>
        /// <para>⚠️ 刻意**不**实现成「先算 <c>3 × (2W)²</c> 再比较」:该乘积在 W 略超 2^31.5 时回绕成
        /// 小值 ⇒ 守卫**假绿**,与守卫本意相反。逐轴比界无任何中间乘积。</para>
        /// </summary>
        public const int MaxWorldHalfExtent = 1 << 29;

        /// <summary>
        /// 校验世界坐标量程在 int64 安全区内。超出即硬失败,不在运行期 clamp
        /// (clamp 会把溢出变成静默的错误格距)。
        /// </summary>
        /// <param name="maxAbsX">X 轴最大绝对坐标</param>
        /// <param name="maxAbsY">Y 轴最大绝对坐标</param>
        /// <param name="maxAbsZ">Z 轴最大绝对坐标</param>
        /// <exception cref="ArgumentOutOfRangeException">任一轴超 <see cref="MaxWorldHalfExtent"/>。</exception>
        public static void ValidateWorldExtent(int maxAbsX, int maxAbsY, int maxAbsZ)
        {
            ValidateAxis(nameof(maxAbsX), maxAbsX);
            ValidateAxis(nameof(maxAbsY), maxAbsY);
            ValidateAxis(nameof(maxAbsZ), maxAbsZ);
        }

        private static void ValidateAxis(string paramName, int maxAbs)
        {
            if (maxAbs < 0 || maxAbs > MaxWorldHalfExtent)
                throw new ArgumentOutOfRangeException(paramName,
                    $"世界坐标量程超 int64 安全区: |坐标| = {maxAbs} > W 上界 {MaxWorldHalfExtent}"
                    + $"(判据 3 × (2W)² < 2^63)");
        }
    }

    /// <summary>地形格数据。</summary>
    public readonly struct TerrainCell
    {
        /// <summary>可走性。不可通行用本字段表达,**不用**速度档 0(AC-6-08)。</summary>
        public readonly bool Walkable;

        /// <summary>速度档位索引(>= 0,&lt; K_speed 表长度)。</summary>
        public readonly int KSpeedIdx;

        public TerrainCell(bool walkable, int kSpeedIdx)
        {
            Walkable = walkable;
            KSpeedIdx = kSpeedIdx;
        }
    }

    /// <summary>
    /// 世界几何数据结构 = 烘焙产物(<c>world_geometry.cooked</c> / <c>world_terrain.cooked</c>)的装载结果。
    /// <para>本类型**只承载已解析数据,零几何生成** —— 装载(含 Addressables)发生在边界程序集
    /// (承 <see cref="IDataProvider"/> 的门 A 纪律),sim 侧只见成形数组。</para>
    /// <para>⚠️ 刻意**没有**「按尺寸造一个空白世界」的入口:未装载的世界若默认可走,烘焙缺数据
    /// 就从「启动期硬失败」退化成「全世界默认可走」的静默失败。空白夹具由测试自建数组提供。</para>
    /// </summary>
    public readonly struct WorldGeometry
    {
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly int SizeZ;

        /// <summary>地形格三维数组(尺寸 = SizeX × SizeY × SizeZ)。</summary>
        public readonly TerrainCell[,,] Terrain;

        /// <summary>一维索引: POI id 或 -1(无 POI)。长度 = SizeX × SizeY × SizeZ。</summary>
        public readonly int[] PoiGrid;

        /// <summary>一维索引: 资源 id 或 -1(无资源)。长度 = SizeX × SizeY × SizeZ。</summary>
        public readonly int[] ResourceGrid;

        /// <summary>用烘焙产物构造世界几何,并执行装载期一致性校验。</summary>
        /// <param name="bakedTerrain">地形格三维数组(决定世界尺寸)</param>
        /// <param name="bakedPoiGrid">POI 一维网格(-1 = 无)</param>
        /// <param name="bakedResourceGrid">资源一维网格(-1 = 无)</param>
        /// <exception cref="ArgumentNullException">任一数组为 null。</exception>
        /// <exception cref="ArgumentException">维度不匹配 / 网格长度不齐 / 量程超安全区。</exception>
        public WorldGeometry(TerrainCell[,,] bakedTerrain, int[] bakedPoiGrid, int[] bakedResourceGrid)
        {
            if (bakedTerrain == null)
                throw new ArgumentNullException(nameof(bakedTerrain));
            if (bakedTerrain.Rank != 3)
                throw new ArgumentException($"地形数组必须是三维,实际 Rank={bakedTerrain.Rank}", nameof(bakedTerrain));
            if (bakedPoiGrid == null)
                throw new ArgumentNullException(nameof(bakedPoiGrid));
            if (bakedResourceGrid == null)
                throw new ArgumentNullException(nameof(bakedResourceGrid));

            int sizeX = bakedTerrain.GetLength(0);
            int sizeY = bakedTerrain.GetLength(1);
            int sizeZ = bakedTerrain.GetLength(2);
            long total = (long)sizeX * sizeY * sizeZ;

            if (bakedPoiGrid.Length != total || bakedResourceGrid.Length != total)
                throw new ArgumentException(
                    $"一维网格长度与地形尺寸不齐: terrain={sizeX}×{sizeY}×{sizeZ}={total}, "
                    + $"poi={bakedPoiGrid.Length}, resource={bakedResourceGrid.Length}");

            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            Terrain = bakedTerrain;
            PoiGrid = bakedPoiGrid;
            ResourceGrid = bakedResourceGrid;

            // 量程守卫进装载入口(story Guardrail:世界坐标量程 W 超安全区 ⇒ 构建期 throw)。
            // 本世界坐标域取 [0, SizeN)(ADR-015 §三 单一整数格,最小角为原点)。
            WorldLatticeParams.ValidateWorldExtent(sizeX - 1, sizeY - 1, sizeZ - 1);
        }

        /// <summary>世界总格数。</summary>
        public long TotalCells => (long)SizeX * SizeY * SizeZ;

        /// <summary>
        /// <see cref="WorldPos"/> → 一维索引(<see cref="PoiGrid"/> / <see cref="ResourceGrid"/> 用)。
        /// 越界返回 -1 —— **不在未越界检查前做算术**:<c>(X × SizeY + Y) × SizeZ + Z</c>
        /// 在 X/SizeY 量级大时先回绕,回绕值可能落回数组长度内 ⇒ 打到**别的格**(静默串格)。
        /// </summary>
        public int ToIndex(WorldPos p)
        {
            if (p.X < 0 || p.X >= SizeX || p.Y < 0 || p.Y >= SizeY || p.Z < 0 || p.Z >= SizeZ)
                return -1;
            return (p.X * SizeY + p.Y) * SizeZ + p.Z;
        }

        /// <summary>
        /// 安全读取地形格。越界返回 <c>Walkable = false</c>(fail-closed:未驻留 chunk 视为全 block,
        /// 承 ADR-014 §五 / <see cref="IDataProvider"/> 注)。
        /// </summary>
        public TerrainCell GetTerrain(WorldPos p)
        {
            int idx = ToIndex(p);
            if (idx < 0)
                return new TerrainCell(false, 0);
            int z = idx % SizeZ;
            int y = (idx / SizeZ) % SizeY;
            int x = idx / (SizeY * SizeZ);
            return Terrain[x, y, z];
        }

        /// <summary>查询 POI id(-1 = 无或越界)。</summary>
        public int GetPoiId(WorldPos p)
        {
            int idx = ToIndex(p);
            return idx < 0 ? -1 : PoiGrid[idx];
        }

        /// <summary>查询资源 id(-1 = 无或越界)。</summary>
        public int GetResourceId(WorldPos p)
        {
            int idx = ToIndex(p);
            return idx < 0 ? -1 : ResourceGrid[idx];
        }
    }
}
