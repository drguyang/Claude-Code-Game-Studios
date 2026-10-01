// Story 001 测试: 世界格与可走性基础 —— LATTICE_SIZE / K_TERRAIN_MAX / WorldGeometry
//
// AC-6-01: 载荷字段类型集合 ⊆ { WorldPos(三 i32), i32/int64, 整数枚举 } —— 反射扫描已登记类型
// AC-6-04: F-6-1 防隧穿硬断言 + 四因子前置守卫(任一 ≤ 0 / SAFETY_MARGIN ≤ 1 ⇒ throw)
// AC-6-05: SAFETY_MARGIN 取等号即失败
// AC-6-06: K_speed 表为空/null ⇒ throw
// AC-6-07: K_TERRAIN_MAX 派生而非手填(改表 ⇒ 随动)
// AC-6-08: 每行 K_speed > 0;K_speed == 1 恰好一行
// AC-6-09: slopeLimit/stepOffset 量化一致 —— 归 Story 005/关卡工具,本 story 只登记整数边界(无 float 字段)
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
        /// <summary>基准参数。SPEED_MAX = SPEED_MODE_MAX(10) × K_TERRAIN_MAX(5) × K_CONTEXT_MAX(2) = 100 格/s;
        /// MAX_DT = 1000 ms ⇒ F-6-1 下界 = 100 × 1000 / 1000 / MARGIN(2) = 50 格。</summary>
        private const int BaseLattice = 400;
        private const int BaseSpeedModeMax = 10;
        private const int BaseKContextMax = 2;
        private const int BaseMaxDtMs = 1000;
        private const int BaseSafetyMargin = 2;

        /// <summary>含基准地貌(1)与最高档 5 的合法表 ⇒ K_TERRAIN_MAX = 5。</summary>
        private static readonly int[] BaseTable = { 1, 2, 5 };

        private static WorldLatticeParams MakeValid(int[] table, int latticeSize = BaseLattice)
        {
            return new WorldLatticeParams(
                latticeSize, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, table);
        }

        // ── AC-6-04 / F-6-1 ────────────────────────────────────────────

        // F-6-1 happy path:基准参数余量充足,过
        [Test]
        public void test_worldLattice_validParams_constructs()
        {
            Assert.DoesNotThrow(() => MakeValid(BaseTable));
        }

        [Test]
        public void test_worldLattice_boundaryEquality_passes()
        {
            // SPEED_MAX=100 格/s;100 × 1000 ms / 1000 × MARGIN(2) = 200 格 ⇒ LATTICE_SIZE=200 恰取下界(过,取等号仍有余量)
            Assert.DoesNotThrow(() => MakeValid(BaseTable, latticeSize: 200));
        }

        [Test]
        public void test_worldLattice_latticeBelowBound_throws()
        {
            // 下界 = 200;199 差一 ⇒ 硬失败,且消息点名四个因子
            var ex = Assert.Throws<ArgumentException>(
                () => MakeValid(BaseTable, latticeSize: 199));
            StringAssert.Contains("LATTICE_SIZE", ex.Message);
            StringAssert.Contains("SPEED_MAX", ex.Message);
            StringAssert.Contains("MAX_DT", ex.Message);
            StringAssert.Contains("SAFETY_MARGIN", ex.Message);
        }

        [Test]
        public void test_worldLattice_zeroFactors_throw()
        {
            // F-6-1 的 B2 静默失败面:任一因子 0 ⇒ 右端 0 ⇒ 不等式真空 ⇒ 必须守卫
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(0, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLattice, 0, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLattice, BaseSpeedModeMax, 0, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLattice, BaseSpeedModeMax, BaseKContextMax, 0, BaseSafetyMargin, new[] { 1 }));
        }

        [Test]
        public void test_worldLattice_negativeFactors_throw()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(-1, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLattice, -2, BaseKContextMax, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
        }

        // AC-6-05:SAFETY_MARGIN 取等号即失败(零余量点是 1,不是取等号)
        [Test]
        public void test_worldLattice_safetyMarginEqualsOne_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(BaseLattice, BaseSpeedModeMax, BaseKContextMax, BaseMaxDtMs, 1, new[] { 1 }));
            StringAssert.Contains("SAFETY_MARGIN", ex.Message);
        }

        // ── AC-6-07 / F-6-2:K_TERRAIN_MAX 派生 ──────────────────────────

        [Test]
        public void test_worldLattice_kTerrainMax_derivedFromTable()
        {
            Assert.AreEqual(5, MakeValid(BaseTable).KTerrainMax);
        }

        [Test]
        public void test_worldLattice_kTerrainMax_followsTableChange()
        {
            // 可证伪守卫(AC-6-07 值级判据):只改表的一行,K_TERRAIN_MAX 须随动
            Assert.AreEqual(7, MakeValid(new[] { 1, 3, 7 }).KTerrainMax);
            Assert.AreEqual(7, MakeValid(new[] { 7, 3, 1 }).KTerrainMax, "max 与行序无关");
            Assert.AreEqual(1, MakeValid(new[] { 1 }).KTerrainMax, "单行基准表 ⇒ max = 1");
        }

        [Test]
        public void test_worldLattice_speedMax_derivesFromExpansion()
        {
            // GDD :511 SPEED_MAX = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX
            var p = MakeValid(new[] { 1, 3 });
            Assert.AreEqual(3, p.KTerrainMax);
            Assert.AreEqual(BaseSpeedModeMax * 3 * BaseKContextMax, p.SpeedMax);
        }

        [Test]
        public void test_worldLattice_latticeBoundTracksTerrainTable()
        {
            // EC-6-14 的代数来源:抬 K_TERRAIN_MAX ⇒ 抬 LATTICE_SIZE 下界
            // 表 {1,2}:K_TERRAIN_MAX=2 ⇒ SPEED_MAX=10×2×2=40 格/s ⇒ 下界 = 40×1000/1000×2 = 80 格
            Assert.DoesNotThrow(() => MakeValid(new[] { 1, 2 }, latticeSize: 80));
            // 同尺寸下改表为 {1,5}:K_TERRAIN_MAX=5 ⇒ SPEED_MAX=100 ⇒ 下界 200 ⇒ 20 必须红
            Assert.Throws<ArgumentException>(() => MakeValid(BaseTable, latticeSize: 80));
            // 反证:K_CONTEXT_MAX 同样锁下界(24 的调参 ⇒ 同尺寸红)
            // 表 {1}:K_TERRAIN_MAX=1;K_CONTEXT_MAX=3 ⇒ SPEED_MAX=30 ⇒ 下界 = 30×1000/1000×2 = 60 格 ⇒ 80 仍过
            Assert.DoesNotThrow(() =>
                new WorldLatticeParams(80, BaseSpeedModeMax, 3, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
            // 同样 80 格,K_CONTEXT_MAX 抬到 4 ⇒ SPEED_MAX=40 ⇒ 下界升到 80 ⇒ 取等号仍过,差一(79)即红
            Assert.DoesNotThrow(() =>
                new WorldLatticeParams(80, BaseSpeedModeMax, 4, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(79, BaseSpeedModeMax, 4, BaseMaxDtMs, BaseSafetyMargin, new[] { 1 }));
        }

        // ── AC-6-06:空表 / null 表 ─────────────────────────────────────

        [Test]
        public void test_worldLattice_kSpeedTableEmpty_throws()
        {
            Assert.Throws<ArgumentException>(() => MakeValid(Array.Empty<int>()));
        }

        [Test]
        public void test_worldLattice_kSpeedTableNull_throws()
        {
            Assert.Throws<ArgumentNullException>(() => MakeValid(null));
        }

        // ── AC-6-08:每行 > 0 + 基准唯一 ────────────────────────────────

        [Test]
        public void test_worldLattice_kSpeedZeroRow_throws()
        {
            // 「不可通行走 walkable=false,不用速度 0」的机械执行点
            var ex = Assert.Throws<ArgumentException>(() => MakeValid(new[] { 1, 0, 3 }));
            StringAssert.Contains("walkable", ex.Message);
        }

        [Test]
        public void test_worldLattice_kSpeedNegativeRow_throws()
        {
            Assert.Throws<ArgumentException>(() => MakeValid(new[] { 1, -2 }));
        }

        [Test]
        public void test_worldLattice_baselineRowMissing_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => MakeValid(new[] { 2, 3 }));
            StringAssert.Contains("恰好一行", ex.Message);
        }

        [Test]
        public void test_worldLattice_baselineRowDuplicated_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => MakeValid(new[] { 1, 1, 2 }));
            StringAssert.Contains("恰好一行", ex.Message);
        }

        [Test]
        public void test_worldLattice_baselineRowUnique_passes()
        {
            Assert.DoesNotThrow(() => MakeValid(new[] { 1, 4, 9 }));
        }

        // ── 派生量隔离:struct 不共享调用方数组 ─────────────────────────

        [Test]
        public void test_worldLattice_kSpeedTable_notAliasedToCaller()
        {
            int[] table = { 1, 2 };
            var p = MakeValid(table);
            table[1] = 99;
            Assert.AreEqual(2, p.KTerrainMax, "改调用方数组不得动已构造参数的派生量");
            Assert.AreEqual(2, p.KSpeedTable[1]);
        }

        // ── 世界量程守卫(enemy-ai.md F-27-1 同型,断言对象 = 量程)───

        [Test]
        public void test_worldLattice_extentWithinSafeZone_passes()
        {
            Assert.DoesNotThrow(() =>
                WorldLatticeParams.ValidateWorldExtent(1000, 1000, 1000));
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
            // 构造路径本身用合法小世界确认不误红,用 null/维度错确认守卫先于装载。
            var terrain = new TerrainCell[4, 4, 4];
            var grid = new int[64];
            Assert.DoesNotThrow(() => new WorldGeometry(terrain, grid, grid));

            // 守卫先于地形数组索引:null 数组 ⇒ 装载期硬失败(不是 NullReference 静默进 Step)
            Assert.Throws<ArgumentNullException>(() => new WorldGeometry(null, grid, grid));
        }

        // ── AC-6-01 / AC-6-08 / AC-6-09:反射扫描(正面白名单)───

        /// <summary>递归收集类型图里出现的全部字段类型(含 struct 嵌套,一深度封顶防环)。</summary>
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
        public void test_worldTypes_noFloatDoubleVector()
        {
            // AC-6-01/08/09:已登记的逻辑层类型集合无 float/double/任何 Vector*
            Type[] registered =
            {
                typeof(WorldPos),
                typeof(TerrainCell),
                typeof(WorldLatticeParams),
                typeof(WorldGeometry)
            };

            foreach (Type t in registered)
            {
                foreach (Type ft in AllFieldTypes(t))
                {
                    Assert.IsFalse(ft == typeof(float) || ft == typeof(double),
                        $"{t.Name} 的类型图含浮点字段类型 {ft}");
                    Assert.IsFalse(ft.Name.StartsWith("Vector", StringComparison.Ordinal),
                        $"{t.Name} 的类型图含 Vector 类型 {ft}");
                }
            }
        }

        [Test]
        public void test_worldPos_fieldsAreInt32()
        {
            foreach (var f in typeof(WorldPos).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.AreEqual(typeof(int), f.FieldType, $"WorldPos 字段 {f.Name} 必须是 int");
        }

        // ── 装载签名面(不引用 UnityEngine,门 A)──────────────────────

        [Test]
        public void test_worldTypes_noUnityEngineReferences()
        {
            Type[] registered =
            {
                typeof(WorldPos),
                typeof(TerrainCell),
                typeof(WorldLatticeParams),
                typeof(WorldGeometry)
            };

            foreach (Type t in registered)
                foreach (Type ft in AllFieldTypes(t))
                    Assert.IsFalse(ft.Namespace != null && ft.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal),
                        $"{t.Name} 的类型图引用 UnityEngine 类型 {ft}");
        }
    }
}
