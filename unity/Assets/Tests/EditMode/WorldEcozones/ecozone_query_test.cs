// Story 002 测试: EcozoneOf 整数几何查询
//
// AC-6-19: 内部格返回 ecozone id, 外部格返回 -1
// AC-6-20: 边界点正则化确定性
// AC-6-21: 重叠区 min(id) 仲裁, 与遍历顺序无关
// AC-6-22: y 分量零影响
// 纯函数可证伪

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public class EcozoneQueryTest
    {
        private EcozoneRegistry _registry;

        [SetUp]
        public void Setup()
        {
            _registry = new EcozoneRegistry();

            // 注册两个矩形生态区(全整数顶点)
            // 区 1: (0,0)-(9,9)
            _registry.Register(new EcozonePolygon(
                new EcozoneId(1),
                new[]
                {
                    new WorldPos(0, 0, 0), new WorldPos(9, 0, 0),
                    new WorldPos(9, 0, 9), new WorldPos(0, 0, 9)
                }));

            // 区 3: (5,0)-(14,9) — 与区 1 重叠 (5-9, 0-9)
            _registry.Register(new EcozonePolygon(
                new EcozoneId(3),
                new[]
                {
                    new WorldPos(5, 0, 0), new WorldPos(14, 0, 0),
                    new WorldPos(14, 0, 9), new WorldPos(5, 0, 9)
                }));

            // 区 5: (20,0)-(29,9) — 不重叠
            _registry.Register(new EcozonePolygon(
                new EcozoneId(5),
                new[]
                {
                    new WorldPos(20, 0, 0), new WorldPos(29, 0, 0),
                    new WorldPos(29, 0, 9), new WorldPos(20, 0, 9)
                }));
        }

        // AC-6-19: 内部格返回 id, 外部格返回 -1
        [Test]
        public void test_internalCell_returnsEcozoneId()
        {
            var id = _registry.EcozoneOf(new WorldPos(4, 0, 4));
            Assert.AreEqual(new EcozoneId(1), id);
        }

        [Test]
        public void test_externalCell_returnsNone()
        {
            var id = _registry.EcozoneOf(new WorldPos(15, 0, 4));
            Assert.AreEqual(EcozoneId.None, id);
        }

        // AC-6-20: 边界点确定性(同一点查 100 次结果恒定)
        [Test]
        public void test_boundaryPoint_deterministic()
        {
            // 边中点 (5, 0, 4) — 恰在区1右边界
            var expected = _registry.EcozoneOf(new WorldPos(5, 0, 4));
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(expected, _registry.EcozoneOf(new WorldPos(5, 0, 4)));
            }
        }

        // AC-6-21: 重叠区 min(id) 仲裁
        [Test]
        public void test_overlap_returnsMinId()
        {
            // (7, 0, 4) 同时在区 1 和区 3 内 => 应返回 min(1, 3) = 1
            var id = _registry.EcozoneOf(new WorldPos(7, 0, 4));
            Assert.AreEqual(new EcozoneId(1), id);
        }

        [Test]
        public void test_overlap_minId_independentOfOrder()
        {
            // 创建新区 2 与区 5 重叠,注册序为 5 后 2
            var registry2 = new EcozoneRegistry();
            registry2.Register(new EcozonePolygon(
                new EcozoneId(5),
                new[]
                {
                    new WorldPos(20, 0, 0), new WorldPos(29, 0, 0),
                    new WorldPos(29, 0, 9), new WorldPos(20, 0, 9)
                }));
            registry2.Register(new EcozonePolygon(
                new EcozoneId(2),
                new[]
                {
                    new WorldPos(25, 0, 0), new WorldPos(34, 0, 0),
                    new WorldPos(34, 0, 9), new WorldPos(25, 0, 9)
                }));

            // 重叠区 (27, 0, 4) => 应返回 min(2, 5) = 2
            var id = registry2.EcozoneOf(new WorldPos(27, 0, 4));
            Assert.AreEqual(new EcozoneId(2), id);
        }

        // AC-6-22: y 分量决定垂直柱体包含(超出顶点 Y 范围 => None)
        [Test]
        public void test_yComponent_noInfluence()
        {
            // 多边形顶点 Y 全为 0 => Y 范围 [0, 0]
            // Y=0 命中, Y=5 / Y=-5 超出柱体范围 => None
            var y0  = _registry.EcozoneOf(new WorldPos(4, 0, 4));
            var y5  = _registry.EcozoneOf(new WorldPos(4, 5, 4));
            var ym5 = _registry.EcozoneOf(new WorldPos(4, -5, 4));

            Assert.AreEqual(new EcozoneId(1), y0);
            Assert.AreEqual(EcozoneId.None, y5);
            Assert.AreEqual(EcozoneId.None, ym5);
        }

        // 纯函数可证伪:同 cell 双跑逐位相同
        [Test]
        public void test_pureFunction_doubleRun_identical()
        {
            var random = new Random(42);
            var cells = new List<WorldPos>();
            for (int i = 0; i < 1000; i++)
            {
                cells.Add(new WorldPos(random.Next(-10, 40), 0, random.Next(-10, 40)));
            }

            // 第一跑
            var run1 = new List<EcozoneId>();
            foreach (var c in cells)
                run1.Add(_registry.EcozoneOf(c));

            // 第二跑
            var run2 = new List<EcozoneId>();
            foreach (var c in cells)
                run2.Add(_registry.EcozoneOf(c));

            Assert.AreEqual(run1.Count, run2.Count);
            for (int i = 0; i < run1.Count; i++)
                Assert.AreEqual(run1[i], run2[i]);
        }

        // EcozoneId 哨兵语义
        [Test]
        public void test_noneSentinel_value()
        {
            Assert.AreEqual(-1, EcozoneId.None.Value);
            Assert.False(EcozoneId.None.IsValid);
        }

        [Test]
        public void test_validEcozoneId()
        {
            Assert.IsTrue(new EcozoneId(0).IsValid);
            Assert.IsTrue(new EcozoneId(1).IsValid);
        }

        // 边界: 空注册表
        [Test]
        public void test_emptyRegistry_allNone()
        {
            var empty = new EcozoneRegistry();
            Assert.AreEqual(0, empty.Count);
            Assert.AreEqual(EcozoneId.None, empty.EcozoneOf(new WorldPos(0, 0, 0)));
        }

        // 边界: 顶点格判定
        [Test]
        public void test_vertexCell_insidePolygon()
        {
            // (0, 0, 0) 是区 1 的顶点, 应被判定为内部
            var id = _registry.EcozoneOf(new WorldPos(0, 0, 0));
            Assert.AreEqual(new EcozoneId(1), id);
        }
    }
}
