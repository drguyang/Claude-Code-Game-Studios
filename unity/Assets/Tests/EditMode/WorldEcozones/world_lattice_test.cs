// Story 001 测试: 世界格与可走性基础 —— LATTICE_SIZE / K_TERRAIN_MAX / WorldGeometry
//
// AC-6-01: WorldPos 唯一正确定义在 Sim.Contracts
// AC-6-04: 防隧穿硬断言(F-6-1)
// AC-6-05: K_TERRAIN_MAX 由 K_speed 表派生(F-6-2)
// AC-6-06: K_speed 表为空 => throw
// AC-6-07: 几何数据来自烘焙产物,运行期零生成
// AC-6-08/09: 全静态断言 WorldPos / 几何 struct 无 float/double

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public class WorldLatticeTest
    {
        // AC-6-01: WorldPos 分量全是 int
        [Test]
        public void test_worldPos_fields_are_int32()
        {
            var fields = typeof(WorldPos).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                Assert.AreEqual(typeof(int), f.FieldType, $"WorldPos 字段 {f.Name} 必须是 int,实际={f.FieldType}");
            }
        }

        // AC-6-04: 防隧穿关系断言
        [Test]
        public void test_antiTunneling_validParams_passes()
        {
            // LATTICE(4) >= 2 * 3 * 2 = 12 => false => throw
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(latticeSize: 4, speedMax: 2, maxDt: 3, safetyMargin: 2, kSpeedTable: new[] { 2, 5 }));
        }

        [Test]
        public void test_antiTunneling_safetyMarginEquals1_throws()
        {
            // SAFETY_MARGIN=1 不满足 >1
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(100, 2, 3, 1, new[] { 2, 5 }));
        }

        [Test]
        public void test_antiTunneling_validParams_succeeds()
        {
            // LATTICE(100) >= 2*3*2 = 12 => true
            Assert.DoesNotThrow(() =>
                new WorldLatticeParams(100, 2, 3, 2, new[] { 2, 5 }));
        }

        // AC-6-05: K_TERRAIN_MAX 派生而非手填
        [Test]
        public void test_kTerrainMax_derivedFromTable()
        {
            var p = new WorldLatticeParams(100, 2, 3, 2, new[] { 2, 3, 5 });
            Assert.AreEqual(5, p.KTerrainMax);
        }

        [Test]
        public void test_kTerrainMax_updatesWhenTableGrows()
        {
            // 塞入更大的值,K_TERRAIN_MAX 自动跟随
            var p = new WorldLatticeParams(100, 2, 3, 2, new[] { 2, 3, 7 });
            Assert.AreEqual(7, p.KTerrainMax);
        }

        // AC-6-06: K_speed 表为空 => throw
        [Test]
        public void test_kSpeedTable_empty_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(100, 2, 3, 2, Array.Empty<int>()));
        }

        [Test]
        public void test_kSpeedTable_null_throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new WorldLatticeParams(100, 2, 3, 2, null));
        }

        // AC-6-07: WorldGeometry 装载(运行期零生成,但数据结构可用)
        [Test]
        public void test_worldGeometry_construction()
        {
            var geo = new WorldGeometry(10, 5, 3);
            Assert.AreEqual(10, geo.SizeX);
            Assert.AreEqual(5, geo.SizeY);
            Assert.AreEqual(3, geo.SizeZ);
            Assert.AreEqual(150, geo.Terrain.GetLength(0) * geo.Terrain.GetLength(1) * geo.Terrain.GetLength(2));
        }

        [Test]
        public void test_worldGeometry_defaultTerrain_isNotWalkable()
        {
            var geo = new WorldGeometry(10, 5, 3);
            var cell = geo.GetTerrain(new WorldPos(0, 0, 0));
            Assert.False(cell.Walkable);
            Assert.AreEqual(0, cell.KSpeedIdx);
        }

        [Test]
        public void test_worldGeometry_outOfBounds_safeReturn()
        {
            var geo = new WorldGeometry(10, 5, 3);
            var cell = geo.GetTerrain(new WorldPos(999, 0, 0));
            Assert.False(cell.Walkable);
        }

        [Test]
        public void test_worldGeometry_poiGrid_defaultMinusOne()
        {
            var geo = new WorldGeometry(10, 5, 3);
            Assert.AreEqual(-1, geo.GetPoiId(new WorldPos(0, 0, 0)));
            Assert.AreEqual(-1, geo.GetResourceId(new WorldPos(0, 0, 0)));
        }

        // AC-6-08/09: 反射扫描断言 WorldPos / TerrainCell / WorldGeometry 无 float/double
        [Test]
        public void test_noFloatDouble_inWorldTypes()
        {
            var types = new[]
            {
                typeof(WorldPos),
                typeof(TerrainCell),
                typeof(WorldLatticeParams),
                typeof(WorldGeometry)
            };

            foreach (var t in types)
            {
                var floatFields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(double));
                Assert.IsEmpty(floatFields, $"{t.Name} 含 float/double 字段: {string.Join(", ", floatFields.Select(f => f.Name))}");
            }
        }

        // 边界: 极端坐标量程
        [Test]
        public void test_worldExtent_validate()
        {
            // 安全区内 => 不 throw
            Assert.DoesNotThrow(() => WorldLatticeParams.ValidateWorldExtent(1000, 1000, 1000));

            // 超安全区 => throw
            Assert.Throws<ArgumentException>(() =>
                WorldLatticeParams.ValidateWorldExtent(100000, 100000, 100000));
        }
    }
}
