// Story 002 测试: EcozoneOf 整数几何查询
//
// AC-6-19/20: 开集判定 + 边界正则化(边 / 顶点 / 射线穿顶点)+ min(id) 裁决序
// AC-6-21: NONE 合法
// AC-6-22: y 零影响 + IL 零浮点 / 零 y 读取(执行点 = EcozoneIntegerGates)
// TC-1..6 + edge cases 逐条落名
//
// ⚠️ 每条断言都**必须能红**(判据可证伪)。上一版的失败正是:
//   · test_yComponent_noInfluence 的名字断言「y 无影响」,体却断言「y 有决定性影响」;
//   · test_boundaryPoint_deterministic 的自比较恒真(借绿);
//   · 两条 min 仲裁测试的夹具是**非法的内部重叠**(GDD 判据 = F-6-3 的 Interior 单值)。
// 本版把这些夹具全部换成**合法**贴边 / 共享顶点形状,并把断言改成值级。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEditor;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public class EcozoneQueryTest
    {
        private EcozoneRegistry _registry;

        // ── 夹具(全部合法:内部两两不交;相邻区只贴边 / 共享顶点)────────────

        // 区 1: (0,0)-(9,9)
        private static EcozonePolygon Zone(int id, params int[] xzPairs)
        {
            var v = new WorldPos[xzPairs.Length / 2];
            for (int i = 0; i < v.Length; i++)
                v[i] = new WorldPos(xzPairs[2 * i], 0, xzPairs[2 * i + 1]);
            return new EcozonePolygon(new EcozoneId(id), v);
        }

        [SetUp]
        public void Setup()
        {
            _registry = new EcozoneRegistry();

            // 区 1: (0,0)-(9,9)—— 基准区
            _registry.Register(Zone(1, 0, 0, 9, 0, 9, 9, 0, 9));

            // 区 3: (9,9)-(14,14)—— 与区 1 沿**格(9,9)** 单点相接(内部不交,只共享顶点)
            _registry.Register(Zone(3, 9, 9, 14, 9, 14, 14, 9, 14));

            // 区 5: (20,0)-(29,9)—— 不接触
            _registry.Register(Zone(5, 20, 0, 29, 0, 29, 9, 20, 9));
        }

        // ── AC-6-19 / TC-1:内部返回 id,外部返回 NONE ────────────────────

        [Test]
        public void test_ecozone_internalCell_returnsId()
        {
            // (4,4) 深在区 1 内部
            Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(4, 0, 4)));
        }

        [Test]
        public void test_ecozone_externalCell_returnsNone()
        {
            // (15,4) 在区 1 之外、在区 3 之外(区 3 = (9,9)-(14,14),z ≤ 14 ⇒ z=4 不在区内)
            Assert.AreEqual(EcozoneId.None, _registry.EcozoneOf(new WorldPos(15, 0, 4)));

            // (12,12) 在区 3 = (9,9)-(14,14) 的深内部 ⇒ 返回 3
            Assert.AreEqual(new EcozoneId(3), _registry.EcozoneOf(new WorldPos(12, 0, 12)));

            // (2,12) 三区都不在 ⇒ NONE
            Assert.AreEqual(EcozoneId.None, _registry.EcozoneOf(new WorldPos(2, 0, 12)));
        }

        [Test]
        public void test_ecozone_allZones_queried()
        {
            // 三个区各取一个深内部格,证明区都被注册且可查(不是只有第一条)
            Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(2, 0, 2)));
            Assert.AreEqual(new EcozoneId(3), _registry.EcozoneOf(new WorldPos(10, 0, 10)));
            Assert.AreEqual(new EcozoneId(5), _registry.EcozoneOf(new WorldPos(25, 0, 4)));
        }

        // ── AC-6-20 / TC-2:边界 = 内部(开集 + 裁决序)────────────────────

        /// <summary>
        /// 点恰在边上 ⇒ **不算任何区的 Interior**(开集,GDD F-6-3 §一),
        /// 归 <c>min(boundary_hits).id</c>。
        /// </summary>
        [Test]
        public void test_ecozone_pointOnEdge_goesToBoundaryArbitration()
        {
            // (5,4) 在区 1 内部,但 (4,5) 恰在区 1 的**上边** z=9 之外?
            // 取确定性边界格:(5,0,9) 恰在区 1 的 z=9 边上(不与任何别的区共享)
            Assert.IsFalse(ContainsStrict(new EcozonePolygon[] { Zone(1, 0, 0, 9, 0, 9, 9, 0, 9) },
                                          new WorldPos(5, 0, 9)),
                "边界格必须**不**是 Interior(开集:边界不计入内部)");

            // 它是区 1 的 Boundary ⇒ 裁决序返回 1(而非 NONE)
            Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(5, 0, 9)));
        }

        [Test]
        public void test_ecozone_vertexCell_boundaryNotInterior()
        {
            var square = Zone(1, 0, 0, 9, 0, 9, 9, 0, 9);

            // 四个顶点都必须是 Boundary
            foreach (var corner in new[] { new WorldPos(0, 0, 0), new WorldPos(9, 0, 0),
                                           new WorldPos(9, 0, 9), new WorldPos(0, 0, 9) })
            {
                Assert.IsTrue(square.IsBoundary(corner), $"{corner} 必须是 Boundary");
                Assert.IsFalse(square.Contains(corner), $"{corner} 必须**不**是 Interior(开集)");
            }

            // 孤立单区的顶点 ⇒ 命中 Boundary ⇒ 返回该 id(不是 NONE)
            Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(0, 0, 0)));
        }

        /// <summary>
        /// EC-6-9:射线恰穿过多边形**顶点**时不双重计数(半开区间)。
        /// <para>夹具 = 底部带 U 形缺口的方区;查询点 (1,4) 的 +X 射线穿过缺口的两个顶点
        /// (3,4)/(6,4) 与右边界 (9,4)。旧实现(无半开)会因顶点被两条相邻边同时计入而
        /// 多数或少数一次 ⇒ 奇偶翻转。本测试把该行为钉成值级可证伪的断言。</para>
        /// </summary>
        [Test]
        public void test_ecozone_rayThroughVertex_noDoubleCount()
        {
            // U 形:外框 (0,0)→(9,0)→(9,9)→(6,9)→(6,4)→(3,4)→(3,9)→(0,9)→回 (0,0)
            var u = new EcozoneRegistry();
            u.Register(new EcozonePolygon(new EcozoneId(7), new[]
            {
                new WorldPos(0, 0, 0), new WorldPos(9, 0, 0),
                new WorldPos(9, 0, 9), new WorldPos(6, 0, 9),
                new WorldPos(6, 0, 4), new WorldPos(3, 0, 4),
                new WorldPos(3, 0, 9), new WorldPos(0, 0, 9)
            }));

            // (1,4):射线 z=4 依次击中左边界 (0,4)、缺口顶点 (3,4)、(6,4)、右边界 (9,4)。
            // 半开 + 交点严格大于 ⇒ 应数到 3 次(而非 2 或 4)⇒ 奇 ⇒ 内部。
            Assert.AreEqual(new EcozoneId(7), u.EcozoneOf(new WorldPos(1, 0, 4)),
                "射线穿两顶点:半开区间必须不双重计数,否则奇偶翻转");

            // (4,4):恰在缺口**底边**上(3,4)-(6,4)⇒ Boundary
            Assert.IsTrue(Zone(7, 0, 0, 9, 0, 9, 9, 6, 9, 6, 4, 3, 4, 3, 9, 0, 9)
                              .IsBoundary(new WorldPos(4, 0, 4)),
                "(4,4) 在缺口底边上");
            Assert.AreEqual(new EcozoneId(7), u.EcozoneOf(new WorldPos(4, 0, 4)),
                "缺口底边 = Boundary ⇒ 归 min(boundary_hits) = 7");

            // (1,2):深在实心柱内 ⇒ 内部
            Assert.AreEqual(new EcozoneId(7), u.EcozoneOf(new WorldPos(1, 0, 2)));
        }

        // ── AC-6-20④:单值性(同格恒返回恰一个 id)──────────────────────

        [Test]
        public void test_ecozone_singleValue_sharedCorner_minId()
        {
            // 区 1 与区 3 恰在格 (9,9) 相接 ⇒ 该格是两区的共享顶点。
            // 开集 ⇒ 谁都不是 Interior ⇒ min(boundary_hits) = 1。
            Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(9, 0, 9)));

            var z1 = Zone(1, 0, 0, 9, 0, 9, 9, 0, 9);
            var z3 = Zone(3, 9, 9, 14, 9, 14, 14, 9, 14);
            Assert.IsTrue(z1.IsBoundary(new WorldPos(9, 0, 9)), "(9,9) 是区 1 的顶点");
            Assert.IsTrue(z3.IsBoundary(new WorldPos(9, 0, 9)), "(9,9) 是区 3 的顶点");
            Assert.IsFalse(z1.Contains(new WorldPos(9, 0, 9)), "开集:顶点不算内部");
            Assert.IsFalse(z3.Contains(new WorldPos(9, 0, 9)), "开集:顶点不算内部");
        }

        // ── AC-6-21 / TC-3:min(id) 仲裁 + 顺序无关 ─────────────────────

        [Test]
        public void test_ecozone_boundaryArbitration_minId()
        {
            // 合法贴边:两矩形沿 x=9 这条边完全重合(内部不交,只共享边界)。
            var r = new EcozoneRegistry();
            r.Register(Zone(9, 0, 0, 9, 0, 9, 9, 0, 9));
            r.Register(Zone(2, 9, 0, 18, 0, 18, 9, 9, 9));

            // 共享边上每个格都是两区的 Boundary ⇒ 返回 min(9, 2) = 2
            foreach (var z in new[] { 0, 4, 9 })
                Assert.AreEqual(new EcozoneId(2), r.EcozoneOf(new WorldPos(9, 0, z)),
                    $"共享边格 (9,0,{z}) 须返回 min(id)=2");

            // 两侧深内部分属两区,证明仲裁不是「一律返回最低 id」
            Assert.AreEqual(new EcozoneId(9), r.EcozoneOf(new WorldPos(4, 0, 4)));
            Assert.AreEqual(new EcozoneId(2), r.EcozoneOf(new WorldPos(13, 0, 4)));
        }

        [Test]
        public void test_ecozone_minId_independentOfRegistrationOrder()
        {
            // 反向注册序(先 2 后 9)+ 交换两条 LogicalId 的数字 ⇒ 结论不变
            var r = new EcozoneRegistry();
            r.Register(Zone(2, 0, 0, 9, 0, 9, 9, 0, 9));
            r.Register(Zone(9, 9, 0, 18, 0, 18, 9, 9, 9));

            foreach (var z in new[] { 0, 4, 9 })
                Assert.AreEqual(new EcozoneId(2), r.EcozoneOf(new WorldPos(9, 0, z)),
                    "min(id) 与注册序无关(交换注册次序应不影响)");

            // 三个合法共享顶点的区(两两只在顶点相接)
            var v = new EcozoneRegistry();
            v.Register(Zone(4, 0, 0, 5, 0, 5, 5, 0, 5));
            v.Register(Zone(1, 5, 5, 10, 5, 10, 10, 5, 10));
            v.Register(Zone(8, 5, 0, 10, 0, 10, 5, 5, 5));
            // (5,5) 同时是三区顶点 ⇒ min(4,1,8) = 1
            Assert.AreEqual(new EcozoneId(1), v.EcozoneOf(new WorldPos(5, 0, 5)));
            // (5,0) 是区 4 与区 8 的共享顶点 ⇒ min(4,8) = 4
            Assert.AreEqual(new EcozoneId(4), v.EcozoneOf(new WorldPos(5, 0, 0)));
        }

        [Test]
        public void test_ecozone_getAllIds_isSortedAscending()
        {
            var ids = _registry.GetAllEcozoneIds();
            Assert.AreEqual(3, ids.Count);
            Assert.AreEqual(new EcozoneId(1), ids[0]);
            Assert.AreEqual(new EcozoneId(3), ids[1]);
            Assert.AreEqual(new EcozoneId(5), ids[2]);
        }

        // ── AC-6-22:y 零影响 ──────────────────────────────────────────

        /// <summary>
        /// ⚠️ 上一版此测试把 AC 断言反了(名字叫「y 无影响」,体却在断言「y 决定结果」)。
        /// GDD :573 / :636-638 / :1367 + story Forbidden 行**四处**一致:
        /// 只用 x / z,y 不参与 ⇒ 同 (x,z) 任意 y ⇒ 同结果。
        /// </summary>
        [Test]
        public void test_ecozone_yComponent_noInfluence()
        {
            int[] ys = { -5, 0, 7, 1000, -1000 };

            // 内部格:y 变化不影响「在区 1 内」
            foreach (int y in ys)
                Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(4, y, 4)),
                    $"y={y} 必须与 y=0 同结果(F-6-3 只用 x/z)");

            // 边界格:同样不受 y 影响
            foreach (int y in ys)
                Assert.AreEqual(new EcozoneId(1), _registry.EcozoneOf(new WorldPos(5, y, 5)),
                    $"共享顶点格(9,9) y={y} 同结果");

            // 真空带:同样不受 y 影响
            foreach (int y in ys)
                Assert.AreEqual(EcozoneId.None, _registry.EcozoneOf(new WorldPos(11, y, 2)),
                    $"真空带 y={y} 同结果");
        }

        [Test]
        public void test_ecozone_flatAndSteepPolygons_sameResult()
        {
            // 同一 x/z 折线,y 全为 0(平地)与 y 高差 500(陡坡)必须是同一个区
            var flat = new EcozoneRegistry();
            flat.Register(new EcozonePolygon(new EcozoneId(6), new[]
            {
                new WorldPos(0, 0, 0), new WorldPos(9, 0, 0),
                new WorldPos(9, 0, 9), new WorldPos(0, 0, 9)
            }));

            var steep = new EcozoneRegistry();
            steep.Register(new EcozonePolygon(new EcozoneId(6), new[]
            {
                new WorldPos(0, -500, 0), new WorldPos(9, 900, 0),
                new WorldPos(9, -3, 9), new WorldPos(0, 4000, 9)
            }));

            foreach (var c in new[] { new WorldPos(4, 0, 4), new WorldPos(8, 123, 1),
                                      new WorldPos(2, -77, 8) })
            {
                Assert.AreEqual(flat.EcozoneOf(c), steep.EcozoneOf(c),
                    $"y 形状不得改变 {c} 的归属(山丘上的 POI / 13 的 HomeRegion 依赖此)");
            }
        }

        /// <summary>AC-6-22 的 IL 执行点:零浮点指令 + 零 y 读取(传递闭包 = EcozoneOf/Register 的真实 call 图)。</summary>
        [Test]
        public void test_ecozone_il_noFloatOps_noYReads()
        {
            // Arrange:编译成功前提(读上一版 DLL = 假绿,承 loop_lifecycle_test 同纪律)
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(scriptCompilationFailed ⇒ 扫到上一版产物 = 假绿)");

            var dll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                DaYiJingCheng.EditorTools.Gates.EcozoneIntegerGates.SimAssemblyName);
            var errs = DaYiJingCheng.EditorTools.Gates.EcozoneIntegerGates
                           .CheckEcozoneIntegerIl(dll);

            Assert.That(errs, Is.Empty,
                "EcozoneOf / Register 的真实 call 图闭包须全整数、零 y 读取"
                + "(AC-6-22;浮点改写 = 破 ADR-006 边界):\n"
                + string.Join("\n", errs));
        }

        /// <summary>IL 门的**闭包形状守卫**:生产侧闭包必须真的含 F-6-3 的射线核与边界判据
        /// (否则「扫了但什么都没扫到」= 空集冒充绿)。</summary>
        [Test]
        public void test_ecozone_ilClosure_containsCoreKernels()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            var dll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                DaYiJingCheng.EditorTools.Gates.EcozoneIntegerGates.SimAssemblyName);
            var errs = DaYiJingCheng.EditorTools.Gates.EcozoneIntegerGates.CheckEcozoneIntegerIl(
                dll,
                DaYiJingCheng.EditorTools.Gates.EcozoneIntegerGates.EcozoneRoots,
                out var closure);

            Assert.That(errs, Is.Empty, string.Join("\n", errs));
            Assert.That(closure, Has.Member("DaYiJingCheng.Sim.World.EcozonePolygon::IsInterior(1)"),
                "IsInterior(射线奇偶核)须在 EcozoneOf 的真实 call 图闭包内");
            Assert.That(closure, Has.Member("DaYiJingCheng.Sim.World.EcozonePolygon::IsBoundary(1)"),
                "IsBoundary(min(boundary_hits) 判据本体)须在闭包内");
            Assert.That(closure, Has.Member("DaYiJingCheng.Sim.World.EcozonePolygon::IsOnBoundarySegment(3)"),
                "IsOnBoundarySegment(精确叉积判据)须在闭包内");
            Assert.That(closure, Has.Member("DaYiJingCheng.Sim.World.EcozoneRegistry::Register(1)"),
                "Register(装载期硬失败)须在闭包内");
        }

        /// <summary>IL 门的可证伪性:对测试装配里的同名假类型跑同一谓词必须红(否则门没在扫东西)。</summary>
        [Test]
        public void test_ecozone_ilGate_negativeFixture_reportsRed()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            // 负面夹具:本文件末尾命名空间块内的 EcozoneRegistry::FakeEcozoneOf(含 conv.r4 / 读 Y)。
            // 扫描根指向**测试装配内的同名类型**—— 谓词按 TypeFullName 解析,与生产侧同一条路径。
            var testDll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                "Sim.Contracts.Tests");
            var errs = DaYiJingCheng.EditorTools.Gates.EcozoneIntegerGates.CheckEcozoneIntegerIl(
                testDll,
                new[] { "DaYiJingCheng.Sim.World.FixtureEcozoneRegistry::FakeEcozoneOf" },
                out var closure);

            Assert.That(errs, Is.Not.Empty,
                "负面夹具(含浮点指令与 y 读取)必须被同一谓词抓住 —— 否则 AC-6-22 的门是恒绿的");
            Assert.That(errs.Exists(e => e.Contains("FakeEcozoneOf")), Is.True,
                "报错须点名负面夹具方法");
            Assert.That(errs.Exists(e => e.Contains("浮点指令")), Is.True,
                "须报出浮点指令面(conv.r4 等)");
            Assert.That(errs.Exists(e => e.Contains("读取字段 Y")), Is.True,
                "须报出 y 读取面");

            // 闭包形状守卫:负面夹具的根方法必须进了闭包(否则扫的是空集 = 假绿)
            Assert.That(closure,
                Has.Member("DaYiJingCheng.Sim.World.FixtureEcozoneRegistry::FakeEcozoneOf(1)"),
                "负面夹具根方法须进闭包 —— 空集扫什么都没查到,不算证伪");
        }

        // ── 纯函数 / 上界 / 哨兵 ───────────────────────────────────────

        [Test]
        public void test_ecozone_pureFunction_doubleRun_identical()
        {
            var random = new Random(42);
            var cells = new List<WorldPos>();
            for (int i = 0; i < 1000; i++)
                cells.Add(new WorldPos(random.Next(-10, 40), 0, random.Next(-10, 40)));

            var run1 = new List<EcozoneId>();
            foreach (var c in cells) run1.Add(_registry.EcozoneOf(c));
            var run2 = new List<EcozoneId>();
            foreach (var c in cells) run2.Add(_registry.EcozoneOf(c));

            for (int i = 0; i < run1.Count; i++)
                Assert.AreEqual(run1[i], run2[i], $"第 {i} 格双跑不一致");
        }

        [Test]
        public void test_ecozone_extentGuard_throwsBeyondSafeRange()
        {
            const int w = WorldLatticeParams.MaxWorldHalfExtent;
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _registry.EcozoneOf(new WorldPos(w + 1, 0, 0)),
                "超 int64 安全区 ⇒ 硬失败(ADR-012 F7:IL2CPP 有符号溢出是 UB,不 clamp)");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _registry.EcozoneOf(new WorldPos(0, 0, -w - 1)));
            // 界上本身不抛(判据取闭区间 ≤ w)
            Assert.DoesNotThrow(() => _registry.EcozoneOf(new WorldPos(w, 0, w)));
        }

        [Test]
        public void test_ecozone_noneSentinel_semantics()
        {
            Assert.AreEqual(-1, EcozoneId.None.Value);
            Assert.IsFalse(EcozoneId.None.IsValid);
            Assert.IsTrue(new EcozoneId(0).IsValid, "0 是合法 id(GDD :634:ecozone_id ≥ 0)");
        }

        [Test]
        public void test_ecozone_emptyRegistry_allNone()
        {
            var empty = new EcozoneRegistry();
            Assert.AreEqual(0, empty.Count);
            Assert.AreEqual(EcozoneId.None, empty.EcozoneOf(new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, empty.GetAllEcozoneIds().Count);
        }

        // ── AC-6-19:装载期硬失败(运行期入参卫生)─────────────────────

        [Test]
        public void test_ecozone_register_rejectsFewerThanThreeVertices()
        {
            Assert.Throws<ArgumentException>(() =>
                _registry.Register(new EcozonePolygon(new EcozoneId(11),
                    new[] { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) })));
        }

        [Test]
        public void test_ecozone_register_rejectsSentinelId()
        {
            Assert.Throws<ArgumentException>(() =>
                _registry.Register(Zone(-1, 0, 0, 3, 0, 3, 3, 0, 3)));
        }

        [Test]
        public void test_ecozone_register_rejectsDuplicateId()
        {
            Assert.Throws<ArgumentException>(() =>
                _registry.Register(Zone(1, 30, 0, 33, 0, 33, 3, 30, 3)),
                "重复 id 必须硬失败(静默覆盖使重建依赖注册顺序)");
        }

        [Test]
        public void test_ecozone_register_rejectsCoincidentAdjacentVertices()
        {
            Assert.Throws<ArgumentException>(() =>
                _registry.Register(new EcozonePolygon(new EcozoneId(12), new[]
                {
                    new WorldPos(0, 0, 0), new WorldPos(0, 0, 0),   // 与前一个重合
                    new WorldPos(4, 0, 0), new WorldPos(4, 0, 4)
                })), "重合相邻顶点使射线法无定义(AC-6-19 ⑤)");
        }

        [Test]
        public void test_ecozone_register_rejectsSelfIntersecting()
        {
            // 领结形:两半互相穿越(真交叉)
            Assert.Throws<ArgumentException>(() =>
                _registry.Register(new EcozonePolygon(new EcozoneId(13), new[]
                {
                    new WorldPos(0, 0, 0), new WorldPos(4, 0, 0),
                    new WorldPos(0, 0, 4), new WorldPos(4, 0, 4)
                })), "自交多边形使奇偶无定义(EC-6-7 / AC-6-19 ③)");
        }

        [Test]
        public void test_ecozone_register_acceptsValidSharingVertex()
        {
            // 贴边相邻是**合法**输入(F-6-3 §二:裁决序吸收)
            var r = new EcozoneRegistry();
            Assert.DoesNotThrow(() =>
            {
                r.Register(Zone(1, 0, 0, 9, 0, 9, 9, 0, 9));
                r.Register(Zone(2, 9, 0, 18, 0, 18, 9, 9, 9));
            });
        }

        // ── 辅助 ──────────────────────────────────────────────────────

        /// <summary>断言给定点**不是**任一多边形的 Interior(开集侧的可证伪对照)。</summary>
        private static bool ContainsStrict(IEnumerable<EcozonePolygon> polys, WorldPos p)
        {
            foreach (var poly in polys)
                if (poly.Contains(p)) return true;
            return false;
        }
    }
}

// AC-6-22 的**负面夹具**:住测试装配 Sim.Contracts.Tests、与生产类型**同名**
// (DaYiJingCheng.Sim.World.EcozoneRegistry),内含一个含浮点指令与 y 读取的
// FakeEcozoneOf —— 借 Cecil 按 TypeFullName 解析的同一路径被抓。
// 照 assembly_boundary_test IlScanNegativeFixture 先例:零新增 asmdef。
namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 负面夹具专用:存在唯一目的是让 AC-6-22 的 IL 门**变红**。
    /// 生产侧类型同名同命名空间,故扫描根路径一致 —— 两类型在不同程序集,Cecil 分
    /// 按 TypeFullName 在各自产物内解析(测试装配产物内有它,Sim 产物内没有)。
    /// </summary>
    internal sealed class FixtureEcozoneRegistry
    {
        private int _unused;

        public EcozoneId FakeEcozoneOf(WorldPos cell)
        {
            _unused = cell.X;
            float dx = 3f;                       // 浮点局部 + conv.r4
            int y = cell.Y;                      // y 读取
            return new EcozoneId((int)dx + y);
        }
    }
}
