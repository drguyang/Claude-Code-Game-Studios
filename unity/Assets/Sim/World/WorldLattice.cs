// 权威来源:ADR-015 §三(单一整数格 · LATTICE_SIZE 取值归 6)· ADR-022(关卡工具)· Story 001
//          GDD world-and-ecozones.md F-6-1 / F-6-2 · AC-6-01…09
//
// 核心机制:
//   - F-6-1 防隧穿硬断言:每帧位移(含余量) ≤ 1 格
//   - SPEED_MAX **不另收裸值**,按 GDD :511 的现裁展开式派生
//       = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX
//     —— 另收一个裸 SPEED_MAX 会把「6 自己的表锁着 6 自己的几何常量」这条耦合藏起来,
//     而那正是 EC-6-14(调 K_speed 抬高 LATTICE_SIZE 下界)的代数来源。
//   - F-6-2: K_TERRAIN_MAX = max(K_speed 表),非手填;空表 = 装载期硬失败
//   - AC-6-06:逻辑层原点 = WorldPos(0,0,0) 与 LATTICE_SIZE 的**同源**一致性(R-6-12)
//   - AC-6-08:每行 K_speed / K_accel / K_decel 全 > 0 + K_speed == 1 恰好一行
//   - 世界坐标量程守卫(承 enemy-ai.md F-27-1:断言对象 = 坐标量程 W,不是半径)
//
// ⚠️⚠️ 单位口径(2026-10-01 评审后**就地重订** —— 原口径称「长度以格计」,量纲不成立):
//   「格边长以格计」是循环定义(1 世界格 = 1 格 ⇒ LATTICE_SIZE 恒 1);且
//   ADR-015 §三 的「空间位置一律整数格」约束的是**坐标**(WorldPos 分量),
//   不是**格边长**这个非空间配置量。现按 GDD F-6-1 表头的物理量纲取值:
//
//     LATTICE_SIZE  = 整数**毫米**(量子化取 mm;亚格精度由毫米表达,不引入 Fix 坐标)
//     SPEED_MODE_MAX = m/s(归 1 玩家控制器;承 ADR-015 §三 非空间量,故可为 Fix,见下)
//     MAX_DT        = ms
//     SAFETY_MARGIN = 无量纲
//     K_*           = 无量纲乘数
//
//   ⇒ 本式的整数形只需**一次量纲恒等式**:`m/s × ms = mm`(m·ms/s = 10⁻³ m)。
//     故 `SPEED_MAX(m/s) × MAX_DT(ms) × SAFETY_MARGIN ≤ LATTICE_SIZE(mm)`
//     中**没有任何换算常数** —— 这是刻意的:原实现有一个 `MsPerSecond = 1000` 桥,
//     它若被抄漏(或两侧单位各自漂移),右端就静默差 1000 倍 = 静默失守。
//     消除桥 = 消除该失效类。
//
// ⚠️ SPEED_MODE_MAX 的整数量子(登记为实现期义务,不是遗漏):
//   本字段取 `int` m/s ⇒ 量子 = 1 m/s。**调用方(1 / 数值轮)须向上取整**
//   (2.5 m/s ⇒ 3)使 F-6-1 下界偏**保守**(下界偏高),绝不可向下取整(那会让守卫偏松)。
//   若数值轮定的值需要亚 m/s 精度,须把 SpeedModeMax / SpeedMax 升为 `Fix`,
//   且中间乘按 ADR-005 Amendment G 的 hi/lo 手工 128 位执行(禁 Int128 / BigInteger)。
//   该升级**未发生前**,本结构按 1 m/s 量子 + 上取整口径执行。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 世界格参数(装载期校验)。
    /// <para>长度单位 = 毫米;速度单位 = m/s;时间单位 = ms;乘数无量纲。
    /// 坐标(<see cref="WorldPos"/>)单位 = 格索引,与上述单位正交,不在此结构中换算。</para>
    /// </summary>
    public readonly struct WorldLatticeParams
    {
        /// <summary>
        /// 逻辑层原点(R-6-12 / AC-6-06):全案空间基准的**唯一定义实体**。
        /// 烘焙侧与运行期两侧都读这一个常量 —— 「同源」的机械含义就是「读同一实体」,
        /// 不是两份相等字面量(相等字面量会各自漂移)。
        /// </summary>
        public static readonly WorldPos LogicalLayerOrigin = new WorldPos(0, 0, 0);

        /// <summary>格边长(整数**毫米**,&gt; 0;F-6-1 约束的对象,下界由 F-6-1 给出)。</summary>
        public readonly int LatticeSizeMm;

        /// <summary>运动档位速度上限的**最大值**(m/s,&gt; 0;P0 单档 = SPEED_WALK,归 1)。</summary>
        public readonly int SpeedModeMax;

        /// <summary>语境速度乘数上限(无量纲,&gt; 0;归 24 的 CONTEXT_TABLE)。</summary>
        public readonly int KContextMax;

        /// <summary>单帧 dt 钳位上限(ms,&gt; 0;归 1)。</summary>
        public readonly int MaxDtMs;

        /// <summary>安全系数(无量纲,&gt; 1;取等号即失败,AC-6-05)。</summary>
        public readonly int SafetyMargin;

        /// <summary>= max(K_speed 表),由 F-6-2 派生,非手填。</summary>
        public readonly int KTerrainMax;

        /// <summary>
        /// = <see cref="SpeedModeMax"/> × <see cref="KTerrainMax"/> × <see cref="KContextMax"/>(m/s,GDD :511)。
        /// </summary>
        public readonly int SpeedMax;

        /// <summary>
        /// 地形速度档位表(F-6-2 的 <c>terrain_id → {K_speed, K_accel, K_decel}</c>)。
        /// 构造时复制,不与调用方共享数组;经 <see cref="IReadOnlyList{T}"/> 暴露,下游不可改元素。
        /// </summary>
        public readonly IReadOnlyList<TerrainSpeedRow> KSpeedTable;

        /// <summary>
        /// 构造世界格参数并执行全部装载期硬断言。
        /// </summary>
        /// <param name="latticeSizeMm">格边长(整数毫米,&gt; 0)</param>
        /// <param name="speedModeMax">速度档上限(m/s,&gt; 0;归 1,**须向上取整到 1 m/s 量子**)</param>
        /// <param name="kContextMax">语境乘数上限(无量纲,&gt; 0;归 24)</param>
        /// <param name="maxDtMs">单帧 dt 钳位上限(ms,&gt; 0;归 1)</param>
        /// <param name="safetyMargin">安全系数(无量纲,&gt; 1)</param>
        /// <param name="kSpeedTable">K_speed 表(非空;每行 K_speed / K_accel / K_decel &gt; 0;恰好一行 K_speed == 1)</param>
        /// <exception cref="ArgumentNullException">表为 null。</exception>
        /// <exception cref="ArgumentException">任一装载期断言失败。</exception>
        public WorldLatticeParams(
            int latticeSizeMm,
            int speedModeMax,
            int kContextMax,
            int maxDtMs,
            int safetyMargin,
            IReadOnlyList<TerrainSpeedRow> kSpeedTable)
        {
            // F-6-1 前置守卫(GDD :492-500 的 B2 静默失败面):
            // 任一因子 ≤ 0(或 SAFETY_MARGIN ≤ 1)⇒ 右端退化 ⇒ 不等式变真空命题。
            if (latticeSizeMm <= 0)
                throw new ArgumentException($"LATTICE_SIZE 必须 > 0,实际={latticeSizeMm} mm", nameof(latticeSizeMm));
            if (speedModeMax <= 0)
                throw new ArgumentException($"SPEED_MODE_MAX 必须 > 0,实际={speedModeMax} m/s", nameof(speedModeMax));
            if (kContextMax <= 0)
                throw new ArgumentException($"K_CONTEXT_MAX 必须 > 0,实际={kContextMax}", nameof(kContextMax));
            if (maxDtMs <= 0)
                throw new ArgumentException($"MAX_DT 必须 > 0,实际={maxDtMs} ms", nameof(maxDtMs));
            if (safetyMargin <= 1)
                throw new ArgumentException($"SAFETY_MARGIN 必须 > 1(取等号即失败),实际={safetyMargin}", nameof(safetyMargin));

            if (kSpeedTable == null)
                throw new ArgumentNullException(nameof(kSpeedTable), "K_speed 表不可为 null");
            if (kSpeedTable.Count == 0)
                throw new ArgumentException("K_speed 表不能为空(max over 空集无定义,F-6-2)", nameof(kSpeedTable));

            // AC-6-08:每行三列全 > 0 + 基准地貌恰好一行。
            // K_speed ≤ 0 不是「很慢」而是「走不动」⇒ 静默绕过 1 的隧穿检测(速度 0 ⇒ 永不跨格 ⇒ 断言恒绿)。
            int baselineRows = 0;
            for (int i = 0; i < kSpeedTable.Count; i++)
            {
                TerrainSpeedRow row = kSpeedTable[i];
                if (row.KSpeed <= 0)
                    throw new ArgumentException(
                        $"K_speed 表第 {i} 行 K_speed 必须 > 0(不可通行用 walkable=false 表达,不用速度 0),实际={row.KSpeed}",
                        nameof(kSpeedTable));
                if (row.KAccel.Raw <= 0)
                    throw new ArgumentException(
                        $"K_speed 表第 {i} 行 K_accel 必须 > 0(AC-6-08),实际 raw={row.KAccel.Raw}",
                        nameof(kSpeedTable));
                if (row.KDecel.Raw <= 0)
                    throw new ArgumentException(
                        $"K_speed 表第 {i} 行 K_decel 必须 > 0(AC-6-08),实际 raw={row.KDecel.Raw}",
                        nameof(kSpeedTable));
                if (row.KSpeed == 1)
                    baselineRows++;
            }
            if (baselineRows != 1)
                throw new ArgumentException(
                    $"K_speed == 1(基准地貌)必须恰好一行,实际={baselineRows}", nameof(kSpeedTable));

            // 复制入自有数组,并**以只读封装暴露**:IReadOnlyList 声明 + 数组底层还不够 ——
            // 调用方拿到底层数组引用仍可改元素 ⇒ 派生量与表脱钩。Array.AsReadOnly 封住这一面。
            var table = new TerrainSpeedRow[kSpeedTable.Count];
            for (int i = 0; i < kSpeedTable.Count; i++)
                table[i] = kSpeedTable[i];

            // F-6-2: K_TERRAIN_MAX = max over rows(AC-6-07:派生而非手填)
            int kMax = table[0].KSpeed;
            for (int i = 1; i < table.Length; i++)
            {
                if (table[i].KSpeed > kMax)
                    kMax = table[i].KSpeed;
            }

            // GDD :511 展开式。三个乘数各自已验 > 0;乘积超 int 域 ⇒ 不是「钳位」而是配置错误。
            long modeCtx = (long)speedModeMax * kContextMax;   // ≤ 2^62,无溢
            if (modeCtx > int.MaxValue / kMax)
                throw new ArgumentException(
                    $"SPEED_MAX 派生值 SPEED_MODE_MAX({speedModeMax} m/s) × K_TERRAIN_MAX({kMax}) × K_CONTEXT_MAX({kContextMax}) 超出 int 域",
                    nameof(speedModeMax));

            LatticeSizeMm = latticeSizeMm;
            SpeedModeMax = speedModeMax;
            KContextMax = kContextMax;
            MaxDtMs = maxDtMs;
            SafetyMargin = safetyMargin;
            KTerrainMax = kMax;
            SpeedMax = (int)(modeCtx * kMax);
            KSpeedTable = Array.AsReadOnly(table);

            // F-6-1:SPEED_MAX(m/s) × MAX_DT(ms) × SAFETY_MARGIN ≤ LATTICE_SIZE(mm)
            // 量纲:m/s × ms = mm(见头注)——无换算常数。
            // 判定写成 `displacement > LATTICE_SIZE / SAFETY_MARGIN`:
            //   · LHS = SPEED_MAX × MAX_DT(≤ 2^62,int64 无溢);不预乘 SAFETY_MARGIN ⇒ 免 2^93 回绕成假绿;
            //   · RHS 整除向下,与「S×D×M > L」精确等价(LHS 为整数 ⇒ LHS > L/M ⟺ LHS > floor(L/M)),
            //     取等号处方向一致(GDD :506:取等号仍有 (M−1) 倍余量,应通过)。
            long displacement = (long)SpeedMax * MaxDtMs;
            if (displacement > LatticeSizeMm / SafetyMargin)
                throw new ArgumentException(
                    $"F-6-1 防隧穿失败: LATTICE_SIZE({LatticeSizeMm} mm) < "
                    + $"SPEED_MAX({SpeedMax} m/s) × MAX_DT({MaxDtMs} ms) × SAFETY_MARGIN({SafetyMargin})"
                    + $" ⟺ 每帧位移(含余量) > 1 格",
                    nameof(latticeSizeMm));
        }

        /// <summary>
        /// 校验逻辑层原点与 <see cref="LogicalLayerOrigin"/> **逐位一致**(AC-6-06 / R-6-12 / EC-6-13)。
        /// <para>两侧读同一常量实体 ⇒ 「同源」;不一致 = 装载期硬失败(不静默降级 ——
        /// 静默降级会让整个世界相对玩家感知整体偏移一格)。</para>
        /// </summary>
        /// <param name="bakedOrigin">烘焙产物声明的逻辑层原点</param>
        /// <exception cref="ArgumentException">原点不一致。</exception>
        public static void ValidateOriginConsistency(WorldPos bakedOrigin)
        {
            if (!bakedOrigin.Equals(LogicalLayerOrigin))
                throw new ArgumentException(
                    $"逻辑层原点不一致(AC-6-06 / R-6-12):烘焙侧={bakedOrigin} ≠ 运行期基准={LogicalLayerOrigin}",
                    nameof(bakedOrigin));
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

    /// <summary>
    /// <c>TERRAIN_TABLE</c> 的一行(GDD F-6-2:<c>terrain_id → {K_speed, K_accel, K_decel}</c>)。
    /// <para><see cref="KSpeed"/> = int(速度**档位**乘数,参与 F-6-1 展开式);
    /// <see cref="KAccel"/> / <see cref="KDecel"/> = <see cref="Fix"/>(无量纲连续乘数,归 1 的 F-1-3 消费,
    /// 本结构只按 AC-6-08 校验其 &gt; 0,不使用其值)。</para>
    /// </summary>
    public readonly struct TerrainSpeedRow
    {
        /// <summary>速度档位乘数(无量纲整数,&gt; 0;基准地貌 = 1)。</summary>
        public readonly int KSpeed;

        /// <summary>加速乘数(无量纲,&gt; 0;归 1 的 F-1-3 消费)。</summary>
        public readonly Fix KAccel;

        /// <summary>减速乘数(无量纲,&gt; 0;归 1 的 F-1-3 消费)。</summary>
        public readonly Fix KDecel;

        public TerrainSpeedRow(int kSpeed, Fix kAccel, Fix kDecel)
        {
            KSpeed = kSpeed;
            KAccel = kAccel;
            KDecel = kDecel;
        }
    }

    /// <summary>地形格数据。</summary>
    public readonly struct TerrainCell
    {
        /// <summary>可走性。不可通行用本字段表达,**不用**速度档 0(AC-6-08)。</summary>
        public readonly bool Walkable;

        /// <summary>
        /// 速度档位索引(>= 0,&lt; K_speed 表长度)。
        /// <para>⚠️ 本字段的上界由 **ADR-014 阶段 2 烘焙器白名单**强制(它是唯一同时看得见
        /// K_speed 表与地形数组的组件),不在 <see cref="WorldGeometry"/> 构造期 ——
        /// 几何装载入口收不到表。当前**全仓零消费者**(grep 可证)⇒ 无现役失效面;
        /// 首个消费者落地的同一批须把该白名单断言一并补齐。</para>
        /// </summary>
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
        /// <exception cref="ArgumentException">网格长度不齐 / 量程超安全区。</exception>
        public WorldGeometry(TerrainCell[,,] bakedTerrain, int[] bakedPoiGrid, int[] bakedResourceGrid)
        {
            if (bakedTerrain == null)
                throw new ArgumentNullException(nameof(bakedTerrain));
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
            // 零尺寸轴经 sizeN-1 = -1 被同一守卫拒掉(fail-closed,非 NullReference 静默进 Step)。
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
        /// <para>⚠️ 下述 <c>SizeY * SizeZ</c> 是 **int 乘**,其无溢依赖两条既存约束而非本处算术:
        /// ① 三维数组能存在 ⇒ <c>SizeY × SizeZ ≤ 总长 ≤ int.MaxValue</c>(.NET 分配上限);
        /// ② 量程守卫拒掉零尺寸轴(否则 <c>0 × 0 = 0</c> ⇒ 除零)。
        /// 两者当前成立;**若将来放宽 <see cref="WorldLatticeParams.MaxWorldHalfExtent"/> 或改并行数组结构,
        /// 须同步复核此处**(该处不变量是靠分配上限挡的,不是靠守卫的算术挡的)。</para>
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
