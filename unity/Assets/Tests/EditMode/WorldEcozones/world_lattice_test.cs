// Story 001 测试: 世界格与可走性基础 —— LATTICE_SIZE / K_TERRAIN_MAX / WorldGeometry
//
// AC-6-01: 事件载荷 / POI_DEF / 导航格 / 建造槽 / 生态区多边形的字段类型集合
//          ⊆ { WorldPos(三 i32), i32/int64, 整数枚举 } —— 反射扫描**正面白名单** +
//          反面夹具(扫描器本身须可证伪,否则永假绿)
// AC-6-02: 逻辑层零浮点 —— 归烘焙 schema 校验器(ADR-014 阶段 2),本 story 只登记载荷面
// AC-6-03: 视觉层不入 sim —— 门 A(程序集引用集白名单)是结构保证,由 AssemblyBoundaryTest 覆盖
// AC-6-04: F-6-1 防隧穿硬断言 + 四因子前置守卫(任一 ≤ 0 / SAFETY_MARGIN ≤ 1 ⇒ throw)
// AC-6-05: SAFETY_MARGIN 取等号即失败(零余量点是 1,不是取等号)
// AC-6-06: 逻辑层原点 = WorldPos(0,0,0) 与 LATTICE_SIZE 同源(两侧读同一实体)
// AC-6-07: K_TERRAIN_MAX 派生而非手填(改表 ⇒ 随动)
// AC-6-08: 每行 K_speed / K_accel / K_decel > 0;K_speed == 1 恰好一行
// AC-6-09: slopeLimit/stepOffset 量化一致 —— 归 Story 005/关卡工具(见 Out of Scope)
//
// ⚠️ 单位口径(承 2026-10-01 评审后就地重订):LATTICE_SIZE = mm · SPEED_* = m/s ·
//    MAX_DT = ms · K_* = 无量纲。量纲恒等式 m/s × ms = mm ⇒ 断言内**无换算常数**。
//
// 命名承接 .claude/rules/test-standards.md: test_[system]_[scenario]_[expected]

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public class WorldLatticeTest
    {
        /// <summary>
        /// 基准参数。SPEED_MAX = SPEED_MODE_MAX(5 m/s) × K_TERRAIN_MAX(5) × K_CONTEXT_MAX(2) = 50 m/s;
        /// MAX_DT = 100 ms,SAFETY_MARGIN = 2 ⇒ F-6-1 下界 = 50 × 100 × 2 = 10000 mm = 10 m。
        /// </summary>
        private const int BaseLatticeMm = 20000;
        private const int BaseSpeedModeMax = 5;
        private const int BaseKContextMax = 2;
        private const int BaseMaxDtMs = 100;
        private const int BaseSafetyMargin = 2;

        /// <summary>F-6-1 对基准表的下界(手工算出的独立期望值,不用被测代码复算)。</summary>
        private const int BaseBoundMm = 10000;

        /// <summary>含基准地貌(1)与最高档 5 的合法表 ⇒ K_TERRAIN_MAX = 5。</summary>
        private static TerrainSpeedRow[] BaseTable()
        {
            return new[]
            {
                new TerrainSpeedRow(1, Fix.FromRational(3, 2), Fix.FromRational(3, 2)),
                new TerrainSpeedRow(2, Fix.One, Fix.One),
                new TerrainSpeedRow(5, Fix.FromRational(4, 3), Fix.One)
            };
        }

        /// <summary>造一行;KAccel/KDecel 取 1(默认合法),便于只测 K_speed 的夹具复用。</summary>
        private static TerrainSpeedRow Row(int kSpeed)
        {
            return new TerrainSpeedRow(kSpeed, Fix.One, Fix.One);
        }

        private static WorldLatticeParams MakeValid(
            TerrainSpeedRow[] table, int latticeSizeMm = BaseLatticeMm)
        {
            return new WorldLatticeParams(
                latticeSizeMm, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, table);
        }

        // ── AC-6-04 / F-6-1 ────────────────────────────────────────────

        // F-6-1 happy path:基准参数余量充足,过
        [Test]
        public void test_worldLattice_validParams_constructs()
        {
            Assert.DoesNotThrow(() => MakeValid(BaseTable()));
        }

        [Test]
        public void test_worldLattice_boundaryEquality_passes()
        {
            // F-6-1 取等号通过(GDD :506:取等号仍有 (M−1) 倍余量,不是零余量点)
            Assert.DoesNotThrow(() => MakeValid(BaseTable(), latticeSizeMm: BaseBoundMm));
        }

        [Test]
        public void test_worldLattice_latticeBelowBound_throws()
        {
            // 下界 = BaseBoundMm;差一 ⇒ 硬失败,且消息点名四个因子与单位
            var ex = Assert.Throws<ArgumentException>(
                () => MakeValid(BaseTable(), latticeSizeMm: BaseBoundMm - 1));
            StringAssert.Contains("LATTICE_SIZE", ex.Message);
            StringAssert.Contains("SPEED_MAX", ex.Message);
            StringAssert.Contains("MAX_DT", ex.Message);
            StringAssert.Contains("SAFETY_MARGIN", ex.Message);
            StringAssert.Contains("mm", ex.Message);
        }

        [Test]
        public void test_worldLattice_dimensionIsInexpensiveToGetWrong_guardedByBoundaryFixture()
        {
            // 单位口径的可证伪守卫:若有人把「m/s × ms = mm」的量纲桥改错
            // (把 MAX_DT 读成 s ⇒ 差 10×;把 LATTICE_SIZE 读成 m ⇒ 差 1000×),
            // 基准参数必须在下界两侧翻红/翻绿。
            Assert.DoesNotThrow(() =>
                new WorldLatticeParams(BaseBoundMm, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, BaseTable()),
                "恰取下界必须过(若过 ⇒ 右端不是偏松 10×/1000×)");
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseBoundMm / 10, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, BaseTable()),
                "差 10 倍(MAX_DT 被读成秒)必须红");
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseBoundMm / 1000, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, BaseTable()),
                "差 1000 倍(LATTICE_SIZE 被读成米)必须红");
        }

        [Test]
        public void test_worldLattice_zeroFactors_throw()
        {
            // F-6-1 的 B2 静默失败面:任一因子 0 ⇒ 右端 0 ⇒ 不等式真空 ⇒ 必须守卫
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(0, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLatticeMm, 0, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLatticeMm, BaseSpeedModeMax, 0, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLatticeMm, BaseSpeedModeMax, BaseKContextMax, 0, BaseSafetyMargin, new[] { Row(1) }));
        }

        [Test]
        public void test_worldLattice_negativeFactors_throw()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(-1, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLatticeMm, -2, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
        }

        // AC-6-05:SAFETY_MARGIN 取等号即失败(零余量点是 1,不是取等号)
        [Test]
        public void test_worldLattice_safetyMarginEqualsOne_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLatticeMm, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, 1, new[] { Row(1) }));
            StringAssert.Contains("SAFETY_MARGIN", ex.Message);
        }

        // ── AC-6-07 / F-6-2:K_TERRAIN_MAX 派生 ──────────────────────────

        [Test]
        public void test_worldLattice_kTerrainMax_derivedFromTable()
        {
            Assert.AreEqual(5, MakeValid(BaseTable()).KTerrainMax);
        }

        [Test]
        public void test_worldLattice_kTerrainMax_followsTableChange()
        {
            // 可证伪守卫(AC-6-07 值级判据):只改表的一行,K_TERRAIN_MAX 须随动
            Assert.AreEqual(7, MakeValid(new[] { Row(1), Row(3), Row(7) }).KTerrainMax);
            Assert.AreEqual(7, MakeValid(new[] { Row(7), Row(3), Row(1) }).KTerrainMax, "max 与行序无关");
            Assert.AreEqual(1, MakeValid(new[] { Row(1) }).KTerrainMax, "单行基准表 ⇒ max = 1");
        }

        [Test]
        public void test_worldLattice_speedMax_derivesFromExpansion()
        {
            // GDD :511 SPEED_MAX = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX
            var p = MakeValid(new[] { Row(1), Row(3) });
            Assert.AreEqual(3, p.KTerrainMax);
            Assert.AreEqual(BaseSpeedModeMax * 3 * BaseKContextMax, p.SpeedMax);
        }

        [Test]
        public void test_worldLattice_latticeBoundTracksTerrainTable()
        {
            // EC-6-14 的代数来源:抬 K_TERRAIN_MAX ⇒ 抬 LATTICE_SIZE 下界
            // 表 {1,2}:K_TERRAIN_MAX=2 ⇒ SPEED_MAX=5×2×2=20 m/s ⇒ 下界 = 20×100×2 = 4000 mm
            Assert.DoesNotThrow(() => MakeValid(new[] { Row(1), Row(2) }, latticeSizeMm: 4000));
            // 同尺寸下改表为 {1,3,7}:K_TERRAIN_MAX=7 ⇒ SPEED_MAX=70 ⇒ 下界 14000 ⇒ 4000 必须红
            Assert.Throws<ArgumentException>(() => MakeValid(new[] { Row(1), Row(3), Row(7) }, latticeSizeMm: 4000));
            // 反证:K_CONTEXT_MAX 同样锁下界(24 的调参 ⇒ 同尺寸红)
            // 表 {1}:K_TERRAIN_MAX=1;K_CONTEXT_MAX=3 ⇒ SPEED_MAX=15 ⇒ 下界 = 15×100×2 = 3000 mm ⇒ 4000 仍过
            Assert.DoesNotThrow(() =>
                new WorldLatticeParams(4000, BaseSpeedModeMax, 3, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
            // 同样 4000 mm,K_CONTEXT_MAX 抬到 6 ⇒ SPEED_MAX=30 ⇒ 下界升到 6000 ⇒ 4000 即红
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(4000, BaseSpeedModeMax, 6, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1) }));
        }

        // ── AC-6-06:空表 / null 表 ─────────────────────────────────────

        [Test]
        public void test_worldLattice_kSpeedTableEmpty_throws()
        {
            Assert.Throws<ArgumentException>(() => MakeValid(Array.Empty<TerrainSpeedRow>()));
        }

        [Test]
        public void test_worldLattice_kSpeedTableNull_throws()
        {
            Assert.Throws<ArgumentNullException>(() => MakeValid(null));
        }

        [Test]
        public void test_worldLattice_kSpeedTableReadonlyExposure_doesNotAliasCaller()
        {
            // readonly 只锁字段不锁元素 ⇒ 暴露面必须是**下游不可改**的形状(IReadOnlyList),
            // 且底层数组不得与调用方共享(调用方持有原数组,改之不得动派生量)。
            TerrainSpeedRow[] caller = { Row(1), Row(5) };
            var p = MakeValid(caller);
            caller[1] = Row(99);

            Assert.AreEqual(5, p.KTerrainMax, "改调用方数组不得动已构造参数的派生量");
            Assert.AreEqual(5, p.KSpeedTable[1].KSpeed, "已构造参数持有的表不得被调用方改元素");

            // 暴露面必须是**下游不可改**的封装。NUnit 的 IsAssignableFrom<IReadOnlyList<>>
            // 在 Unity Mono 的 apiCompatibilityLevel=6(netstandard2.1)下对
            // Array.AsReadOnly 的返回类型判 red(ReadOnlyCollection<T> 的接口表该配置下不含它)
            // ⇒ 改用行为判据:任何写接口都不应该在可编译的调用面上。
            Assert.That(p.KSpeedTable, Is.Not.InstanceOf<TerrainSpeedRow[]>(),
                "暴露面不得是数组本身(数组元素可改 ⇒ 派生量与表脱钩)");
            Assert.That(p.KSpeedTable.GetType().Name,
                Does.Contain("ReadOnly").IgnoreCase,
                "暴露面须为只读封装(Array.AsReadOnly),实际=" + p.KSpeedTable.GetType().Name);
        }

        // ── AC-6-08:每行 > 0 + 基准唯一 ────────────────────────────────

        [Test]
        public void test_worldLattice_kSpeedZeroRow_throws()
        {
            // 「不可通行走 walkable=false,不用速度 0」的机械执行点
            var ex = Assert.Throws<ArgumentException>(() => MakeValid(new[] { Row(1), Row(0), Row(3) }));
            StringAssert.Contains("walkable", ex.Message);
        }

        [Test]
        public void test_worldLattice_kSpeedNegativeRow_throws()
        {
            Assert.Throws<ArgumentException>(() => MakeValid(new[] { Row(1), Row(-2) }));
        }

        [Test]
        public void test_worldLattice_kAccelOrKDecelNonPositive_throws()
        {
            // AC-6-08 的三列口径:K_accel / K_decel 各 > 0(K_speed 之外的两列)
            Assert.Throws<ArgumentException>(() => MakeValid(new[]
            {
                Row(1),
                new TerrainSpeedRow(2, new Fix(0), Fix.One)
            }));
            Assert.Throws<ArgumentException>(() => MakeValid(new[]
            {
                Row(1),
                new TerrainSpeedRow(2, Fix.One, new Fix(-1))
            }));
        }

        [Test]
        public void test_worldLattice_baselineRowMissing_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => MakeValid(new[] { Row(2), Row(3) }));
            StringAssert.Contains("恰好一行", ex.Message);
        }

        [Test]
        public void test_worldLattice_baselineRowDuplicated_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => MakeValid(new[] { Row(1), Row(1), Row(2) }));
            StringAssert.Contains("恰好一行", ex.Message);
        }

        [Test]
        public void test_worldLattice_baselineRowUnique_passes()
        {
            Assert.DoesNotThrow(() => MakeValid(new[] { Row(1), Row(4), Row(9) }));
        }

        [Test]
        public void test_worldLattice_storyTc3Table_isRejectedByBaselineRule()
        {
            // Story TC-3 原写表 {2,5,3} —— 无基准行 ⇒ AC-6-08(BLOCKING)拒收。
            // 本测把该冲突钉死:规格(AC-6-08)优先于用例表,合式表 = {1,5,3}。
            Assert.Throws<ArgumentException>(() => MakeValid(new[] { Row(2), Row(5), Row(3) }));
            Assert.AreEqual(5, MakeValid(new[] { Row(1), Row(5), Row(3) }).KTerrainMax,
                "合式替代表 {1,5,3} 给出 TC-3 所期望的 K_TERRAIN_MAX = 5");
        }

        // ── AC-6-06:原点同源(R-6-12 / EC-6-13)───────────────────────

        [Test]
        public void test_logicalLayerOrigin_isSingleDefinedConstant()
        {
            // 「同源」= 两侧读同一实体 ⇒ 基准实体必须是唯一实例,不是每处一份字面量
            Assert.AreEqual(new WorldPos(0, 0, 0), WorldLatticeParams.LogicalLayerOrigin);
            Assert.AreSame(
                typeof(WorldLatticeParams).GetField(
                    nameof(WorldLatticeParams.LogicalLayerOrigin), BindingFlags.Public | BindingFlags.Static),
                typeof(WorldLatticeParams).GetField(
                    nameof(WorldLatticeParams.LogicalLayerOrigin), BindingFlags.Public | BindingFlags.Static));
        }

        [Test]
        public void test_originConsistency_bakedOriginOffByOne_throws()
        {
            // 差一格 = 硬失败(不静默降级 —— 静默降级使整个世界相对玩家感知整体偏移)
            var ex = Assert.Throws<ArgumentException>(() =>
                WorldLatticeParams.ValidateOriginConsistency(new WorldPos(0, 0, 1)));
            StringAssert.Contains("原点", ex.Message);

            Assert.DoesNotThrow(() =>
                WorldLatticeParams.ValidateOriginConsistency(new WorldPos(0, 0, 0)));
        }

        // ── SPEED_MAX 派生溢出守卫 ──────────────────────────────────────

        [Test]
        public void test_worldLattice_speedMaxProduct_overflowsInt_throws()
        {
            // modeCtx = SPEED_MODE_MAX × K_CONTEXT_MAX > int.MaxValue / K_TERRAIN_MAX ⇒ 配置错误(非钳位)
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLatticeMm, int.MaxValue, 2, BaseMaxDtMs, BaseSafetyMargin, new[] { Row(1), Row(5) }));
            // 紧邻边界:modeCtx = int.MaxValue / 5 恰不溢(K_TERRAIN_MAX=1 ⇒ SpeedMax = modeCtx 本身也在 int 域),
            // F-6-1 取 SAFETY_MARGIN=5 恰过(429496729 × 1 = 429496729 ≤ 2147483647/5)⇒ 证明守卫不误红。
            int tight = int.MaxValue / 5;
            Assert.DoesNotThrow(() =>
                new WorldLatticeParams(int.MaxValue, tight, 1, 1, 5, new[] { Row(1) }));
        }

        // ── 世界量程守卫(enemy-ai.md F-27-1 同型,断言对象 = 量程)───

        [Test]
        public void test_worldLattice_extentWithinSafeZone_passes()
        {
            Assert.DoesNotThrow(() =>
                WorldLatticeParams.ValidateWorldExtent(1000, 1000, 1000));
        }

        [Test]
        public void test_worldLattice_extentConstant_isBoundaryValue()
        {
            // 差一夹具:把 MaxWorldHalfExtent 改回旧的失效值(1<<40)或在任一方向放宽,
            // 本测必红 —— 否则该常量无人看守(魔法数假绿)。
            Assert.DoesNotThrow(() =>
                WorldLatticeParams.ValidateWorldExtent(
                    WorldLatticeParams.MaxWorldHalfExtent - 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldLatticeParams.ValidateWorldExtent(
                    WorldLatticeParams.MaxWorldHalfExtent + 1, 0, 0));

            // 常量值本身须满足判据 12 × W² < 2^63(≡ 3 × (2W)²)。
            // 写成 W² ≤ (2^63−1)/12:两侧均在 long 域内 ⇒ 无回绕、无编译期溢出
            // (直接写 3 × (2^31) × (2^31) 会常量折叠溢出 = CS0220,守门测自编译失败)。
            long maxSquare = long.MaxValue / 12;
            long w = WorldLatticeParams.MaxWorldHalfExtent;
            Assert.That(w * w, Is.LessThanOrEqualTo(maxSquare), "2^29 须满足 12 × W² < 2^63");
            long doubled = w * 2;
            Assert.That(doubled * doubled, Is.GreaterThan(maxSquare),
                "2^30 不满足 ⇒ 2^29 确为最大的可行 2 的幂(否则本常量过松)");
        }

        [Test]
        public void test_worldLattice_extentAtBoundary_passes()
        {
            Assert.DoesNotThrow(() =>
                WorldLatticeParams.ValidateWorldExtent(
                    WorldLatticeParams.MaxWorldHalfExtent,
                    WorldLatticeParams.MaxWorldHalfExtent,
                    WorldLatticeParams.MaxWorldHalfExtent));
        }

        [Test]
        public void test_worldLattice_extentBeyondBoundary_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldLatticeParams.ValidateWorldExtent(
                    WorldLatticeParams.MaxWorldHalfExtent + 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldLatticeParams.ValidateWorldExtent(0, -1, 0), "负坐标同样越界");
        }

        [Test]
        public void test_worldLattice_extentMaxInt_throws()
        {
            // TC-6:极端坐标参与量程断言 ⇒ 构建期 throw(不得回绕后假绿)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldLatticeParams.ValidateWorldExtent(int.MaxValue, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldLatticeParams.ValidateWorldExtent(int.MinValue, 0, 0));
        }

        // ── WorldGeometry(烘焙装载结果)────────────────────────────────

        /// <summary>造一个 3×2×4 的空白烘焙世界(全可走、无 POI/资源)。</summary>
        private static (WorldGeometry geo, TerrainCell[,,] terrain) MakeGeometry(int sx = 3, int sy = 2, int sz = 4)
        {
            var terrain = new TerrainCell[sx, sy, sz];
            long total = (long)sx * sy * sz;
            var poi = new int[total];
            var resource = new int[total];
            for (int i = 0; i < total; i++)
            {
                poi[i] = -1;
                resource[i] = -1;
            }
            for (int x = 0; x < sx; x++)
                for (int y = 0; y < sy; y++)
                    for (int z = 0; z < sz; z++)
                        terrain[x, y, z] = new TerrainCell(true, 0);
            var geo = new WorldGeometry(terrain, poi, resource);
            return (geo, terrain);
        }

        [Test]
        public void test_worldGeometry_constructedFromBaked_preservesSizes()
        {
            var (geo, _) = MakeGeometry();
            Assert.AreEqual(3, geo.SizeX);
            Assert.AreEqual(2, geo.SizeY);
            Assert.AreEqual(4, geo.SizeZ);
            Assert.AreEqual(24, geo.TotalCells);
        }

        [Test]
        public void test_worldGeometry_gridLengthMismatch_throws()
        {
            var (geo, terrain) = MakeGeometry();
            Assert.Throws<ArgumentException>(() =>
                new WorldGeometry(terrain, new[] { -1, -1 }, new[] { -1, -1, -1 }));
            Assert.Throws<ArgumentException>(() =>
                new WorldGeometry(terrain, new int[geo.TotalCells - 1], new int[geo.TotalCells]));
            Assert.Throws<ArgumentNullException>(() =>
                new WorldGeometry(null, new int[geo.TotalCells], new int[geo.TotalCells]));
        }

        [Test]
        public void test_worldGeometry_outOfBounds_isNotWalkable()
        {
            var (geo, _) = MakeGeometry();
            Assert.IsFalse(geo.GetTerrain(new WorldPos(999, 0, 0)).Walkable);
            Assert.IsFalse(geo.GetTerrain(new WorldPos(-1, 0, 0)).Walkable);
        }

        [Test]
        public void test_worldGeometry_toIndex_outOfBoundsReturnsMinusOne()
        {
            var (geo, _) = MakeGeometry(3, 1, 4);
            // 越界 ⇒ -1;不得回绕成别的格
            Assert.AreEqual(-1, geo.ToIndex(new WorldPos(-1, 0, 0)));
            Assert.AreEqual(-1, geo.ToIndex(new WorldPos(3, 0, 0)));
            Assert.AreEqual(-1, geo.ToIndex(new WorldPos(0, 0, 4)));
            // 非负界内:单调、域内
            Assert.AreEqual(0, geo.ToIndex(new WorldPos(0, 0, 0)));
            Assert.AreEqual(11, geo.ToIndex(new WorldPos(2, 0, 3)));
        }

        [Test]
        public void test_worldGeometry_toIndex_noAliasingAcrossCells()
        {
            // 回归守卫:旧实现无先验越界检查,大坐标算术回绕后可落到别的格 ⇒ 静默串格
            var (geo, _) = MakeGeometry(3, 1, 4);
            var far = new WorldPos(int.MaxValue / 2 + 1, 0, 0);
            Assert.AreEqual(-1, geo.ToIndex(far));
            Assert.AreEqual(-1, geo.GetPoiId(far));
            Assert.AreEqual(-1, geo.GetResourceId(far));
        }

        [Test]
        public void test_worldGeometry_poiAndResource_defaultMinusOne()
        {
            var (geo, _) = MakeGeometry();
            Assert.AreEqual(-1, geo.GetPoiId(new WorldPos(0, 0, 0)));
            Assert.AreEqual(-1, geo.GetResourceId(new WorldPos(0, 0, 0)));
            Assert.AreEqual(-1, geo.GetPoiId(new WorldPos(99, 99, 99)));
        }

        [Test]
        public void test_worldGeometry_walkabilityFromBakedData_isRespected()
        {
            // 可走性须来自烘焙数据本身 —— 装载不得把不可走格改成可走(fail-open)
            var terrain = new TerrainCell[2, 1, 1];
            terrain[0, 0, 0] = new TerrainCell(true, 0);
            terrain[1, 0, 0] = new TerrainCell(false, 0);
            var geo = new WorldGeometry(terrain, new[] { -1, -1 }, new[] { -1, -1 });

            Assert.IsTrue(geo.GetTerrain(new WorldPos(0, 0, 0)).Walkable);
            Assert.IsFalse(geo.GetTerrain(new WorldPos(1, 0, 0)).Walkable);
        }

        [Test]
        public void test_worldGeometry_extentGuard_onLoad()
        {
            // Guardrail:装载入口即跑量程守卫(超界世界由 ValidateWorldExtent 直测登记)。
            // 构造路径本身用合法小世界确认不误红,用 null 确认守卫先于装载。
            var terrain = new TerrainCell[4, 4, 4];
            var grid = new int[64];
            Assert.DoesNotThrow(() => new WorldGeometry(terrain, grid, grid));

            // 守卫先于地形数组索引:null 数组 ⇒ 装载期硬失败(不是 NullReference 静默进 Step)
            Assert.Throws<ArgumentNullException>(() => new WorldGeometry(null, grid, grid));
        }

        [Test]
        public void test_worldGeometry_zeroSizeAxis_throws()
        {
            // 零尺寸轴经 sizeN-1 = -1 被量程守卫拒掉(fail-closed:不得造出「空世界」静默可走)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldGeometry(new TerrainCell[0, 1, 1], Array.Empty<int>(), Array.Empty<int>()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldGeometry(new TerrainCell[1, 0, 1], Array.Empty<int>(), Array.Empty<int>()));
        }

        // ── AC-6-01:逻辑层载荷面白名单(正面枚举 + 反面夹具)──────

        /// <summary>
        /// AC-6-01 点名的已登记类型集合:事件载荷 / 导航格 / 建造槽 / 生态区多边形 / POI 状态。
        /// 判据 = 「**枚举的都合格**」(负存在断言不可判定,GDD :1140);扩面时按需增补。
        /// </summary>
        private static IEnumerable<Type> RegisteredLogicLayerTypes()
        {
            Type[] worldPosShaped =
            {
                typeof(WorldPos),
                typeof(TerrainCell),
                typeof(TerrainSpeedRow),
                typeof(WorldLatticeParams),
                typeof(WorldGeometry),
                typeof(BuildSlot),
                typeof(EcozonePolygon)
            };

            // 35 支 per-Kind payload 的真源 = Sim.Contracts 程序集(不是 .Payloads 子命名空间
            // —— 实际 namespace 是 DaYiJingCheng.Sim.Contracts,子目录只是文件组织)。
            // 判据取「全集里具名 *Payload 的值类型」,与 entities.yaml 的 registry 逐支对应。
            IEnumerable<Type> payloads = typeof(SimEvent).Assembly
                .GetTypes()
                .Where(t => t.IsValueType
                            && t.Name.EndsWith("Payload", StringComparison.Ordinal)
                            && t.Namespace == "DaYiJingCheng.Sim.Contracts");

            return worldPosShaped.Concat(payloads)
                .Concat(new[]
                {
                    typeof(SimEvent),
                    typeof(PayloadRef),
                    typeof(CaseId),
                    typeof(DiseaseIdSet)
                });
        }

        /// <summary>递归收集类型图里出现的全部字段类型(BFS + seen 集合:防环靠去重,不靠深度上限)。</summary>
        private static IEnumerable<Type> AllFieldTypes(Type root)
        {
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Type t = queue.Dequeue();
                if (!seen.Add(t))
                    continue;
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    Type ft = f.FieldType;
                    if (ft.IsByRef) ft = ft.GetElementType();
                    if (ft.IsArray) ft = ft.GetElementType();
                    yield return ft;
                    if (!ft.IsPrimitive && !ft.IsEnum && ft.Namespace != null
                        && ft.Namespace.StartsWith("DaYiJingCheng", StringComparison.Ordinal))
                        queue.Enqueue(ft);
                }
            }
        }

        [Test]
        public void test_logicLayerTypes_noFloatDoubleVectorOrLocalFrame()
        {
            foreach (Type t in RegisteredLogicLayerTypes())
            {
                foreach (Type ft in AllFieldTypes(t))
                {
                    Assert.IsFalse(IsFloatLike(ft), $"{t.Name} 的类型图含浮点字段类型 {ft}");
                    Assert.IsFalse(IsVectorLike(ft), $"{t.Name} 的类型图含 Vector 类型 {ft}");
                    Assert.IsFalse(IsUnityEngine(ft), $"{t.Name} 的类型图引用 UnityEngine 类型 {ft}");
                }
            }
        }

        [Test]
        public void test_worldPos_fieldsAreInt32()
        {
            foreach (var f in typeof(WorldPos).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.AreEqual(typeof(int), f.FieldType, $"WorldPos 字段 {f.Name} 必须是 int");
        }

        // ── 扫描器的可证伪性(负面夹具:证明门能红)───────────────

        /// <summary>负面夹具:一个含 float 的「假载荷」。扫描器对它必须红。</summary>
        private readonly struct FloatFieldFixture
        {
            public readonly float Dummy;
        }

        /// <summary>负面夹具:含 chunk 局部坐标形状的「假类型」。</summary>
        private readonly struct VectorFieldFixture
        {
            /// <summary>字段名与所在夹具名都**不含** "Vector",只有**字段类型名**含 ——
            /// 考的正是「按字段类型名判」而非按字段名 / 宿主名判。</summary>
            public readonly VectorChunkOffsetFixture Dummy;
        }

        private struct VectorChunkOffsetFixture
        {
            public readonly int Dummy;
        }

        /// <summary>对照夹具:全整数,判据须判干净(证明判据不是恒真)。</summary>
        private readonly struct UintFieldFixture
        {
            public readonly uint Dummy;
        }

        /// <summary>仅作判据本体自证用的 Vector 状类型。</summary>
        private readonly struct VectorLike
        {
            public readonly int Dummy;
        }

        // ── 判据本体(白名单断言与负面夹具共用同一组函数,否则负面夹具测的是「复读一遍」)───

        private static bool IsFloatLike(Type ft) => ft == typeof(float) || ft == typeof(double);

        private static bool IsVectorLike(Type ft) => ft.Name.StartsWith("Vector", StringComparison.Ordinal);

        private static bool IsUnityEngine(Type ft) =>
            ft.Namespace != null && ft.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal);

        private static bool TypeGraphContains(Type root, Func<Type, bool> predicate)
        {
            foreach (Type ft in AllFieldTypes(root))
                if (predicate(ft))
                    return true;
            return false;
        }

        [Test]
        public void test_typeGraphScan_negativeFixtures_reportRed()
        {
            // 白名单断言若无反面夹具 = 永假绿(没人能证明它真的在扫)。
            // 本测把**判据本体**变成被测对象:它必须能在**已知脏**的类型上红 ——
            // 若将来有人把判据改成恒 false(或把 AllFieldTypes 改成不 yield),本测即红。
            Assert.IsTrue(TypeGraphContains(typeof(FloatFieldFixture), IsFloatLike),
                "含 float 的夹具必须被判为脏");
            Assert.IsFalse(TypeGraphContains(typeof(UintFieldFixture), IsFloatLike),
                "对照:全整数夹具必须被判为干净(证明判据不是恒真)");
            Assert.IsTrue(TypeGraphContains(typeof(VectorFieldFixture), IsVectorLike),
                "需有 Vector**形状**的脏夹具在扫描集内 —— 扫描器递归展开具体字段类型");
            // 判据本体的另一半:直接喂一个 Vector 状类型名给判据,它必须判真
            Assert.IsTrue(IsVectorLike(typeof(VectorLike)), "判据本体对 Vector 状类型名必须判真");
            Assert.IsFalse(IsVectorLike(typeof(int)), "判据本体对普通类型必须判假");
        }

        [Test]
        public void test_typeGraphScan_registeredSet_coversAc601NamedSurface()
        {
            // AC-6-01 点名的五类面必须**真的**在扫描集内(自证扫描面非空知己):
            // 事件载荷 / POI / 导航格 / 建造槽 / 生态区多边形。
            Type[] registered = RegisteredLogicLayerTypes().ToArray();

            Assert.That(registered, Has.Member(typeof(BuildSlot)), "建造槽位在扫描集内");
            Assert.That(registered, Has.Member(typeof(EcozonePolygon)), "生态区多边形在扫描集内");
            Assert.That(registered, Has.Member(typeof(SimEvent)), "事件头部在扫描集内");
            Assert.That(registered, Has.Member(typeof(TerrainCell)), "地形格在扫描集内");

            var payloadCount = registered.Count(t =>
                t.Name.EndsWith("Payload", StringComparison.Ordinal));
            Assert.Greater(payloadCount, 30,
                $"35 支 per-Kind payload 须全在扫描集内,实际 {payloadCount}");
        }
    }
}
