// Story 002 · clinic-machine —— 房间析出与连通规则(AC-24-10 · 4 邻接 · 壳格 · 开顶 · 房间标识稳定 · 只读输入)
//
// 卡面:production/epics/clinic-machine/story-002-room-extraction-connectivity.md
//   (§Test Evidence 逐字点名本路径 —— 真身 = 本文件;含 🔁 QA plan 回填 5 条新增判据)
// 权威来源:
//   GDD design/gdd/clinic-machine.md: 规则一(房间 = 连通家具格集 · 4 邻接 · 非格分类 · 单连通块前提)
//     · EC-24-01(空布局 = ROOM_NONE,合法)· EC-24-02(未匹配房间类型 = 中性)· EC-24-04(壳格 / 边界 /
//     角落)· EC-24-07(≥ 2 簇 ⇒ 整盘 ROOM_NONE)
//   ADR-015 §三(单一整数格)· §五(4 邻接判定,非物理 / 非 NavMesh)
//   ADR-005(纯函数派生 / 无状态 / 可重放)
//   control-manifest §Core Layer Rules:Required / Forbidden / Guardrail 三条(见卡面)
//
// ⚠️ 被测对象 = **生产代码**(`DaYiJingCheng.Sim.Clinic.ClinicRoomExtraction` / `IPlacedStructureReadView`),
//   不是本文件的副本。本文件只含夹具(测试内局部替身视图)与断言。
//
// ⚠️ 测试纪律:零写盘 · 无随机种子 · 无时间依赖 · 无文件 I/O
//   (`.claude/rules/test-standards.md`;亦避 FsWriteBoundaryGate 的 R1/R2 面)。
//
// ⚠️ **甲案代理判据**(卡面 §Implementation Notes 7,用户 2026-10-21 裁定):
//   生产代码的「家具格」= **被 `StructurePlaced` 占用的格**,是 `type(c) ∈ FURN_TYPES` 的**代理**,
//   **代理更宽**(任何放置件都算)。真闭集(O-24-1)落地时,若与代理不等价 ⇒ 本文件相关测**转红**
//   是**被要求的行为**。闭集缺席由缺口哨兵 `test_furn_type_closure_absent_sentinel_false` 钉住。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>24 房间析出的 EditMode 测试。</summary>
    [TestFixture]
    public sealed class ClinicRoomExtractionTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 夹具基建 —— 只读视图的测试内局部替身
        //   ⚠️ 替身实现 `IPlacedStructureReadView`(**没有**任何写成员:接口在类型层面
        //      不含 Place/Remove/Modify/Register/Append/Occupy/Free ⇒ 「24 调不到 23 的写路径」)。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>把「实例 → 完整占用格」直接摊开的只读替身(协作对象,不是生产实现)。</summary>
        private sealed class FakeStructureReadView : IPlacedStructureReadView
        {
            private readonly List<IReadOnlyList<WorldPos>> _instances;
            private readonly string[] _moduleIds;

            public FakeStructureReadView(params IReadOnlyList<WorldPos>[] instances)
                : this(null, instances)
            {
            }

            /// <summary>带模块 id 的构造(真判据 `type(c) ∈ FURN_TYPES` 的测试面)。</summary>
            public FakeStructureReadView(string[] moduleIds, params IReadOnlyList<WorldPos>[] instances)
            {
                _moduleIds = moduleIds;
                _instances = new List<IReadOnlyList<WorldPos>>(
                    instances ?? Array.Empty<IReadOnlyList<WorldPos>>());
            }

            public int StructureCount => _instances.Count;

            public IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex)
                => _instances[structureIndex];

            public string GetModuleId(int structureIndex)
                => _moduleIds == null ? null : _moduleIds[structureIndex];
        }

        /// <summary>单格棋子的便捷构造(<paramref name="z"/> 区分层)。</summary>
        private static WorldPos Cell(int x, int y, int z = 0) => new WorldPos(x, y, z);

        /// <summary>每格一件**单格**结构(最常见的散件布局)。</summary>
        private static FakeStructureReadView SingleCellInstances(params WorldPos[] cells)
            => new FakeStructureReadView(cells.Select(c => (IReadOnlyList<WorldPos>)new[] { c }).ToArray());

        /// <summary>「每格一件单格结构」⇒ 布局的格集(与 <see cref="SingleCellInstances"/> 配对使用)。</summary>
        private static HashSet<WorldPos> CellSet(params WorldPos[] cells) => new HashSet<WorldPos>(cells);

        /// <summary>结果里的成员格,规范化成 <c>(x,y,z)</c> 元组集(消除顺序依赖的比较)。</summary>
        private static HashSet<(int, int, int)> AsTuples(IReadOnlyList<WorldPos> cells)
            => new HashSet<(int, int, int)>(cells.Select(c => (c.X, c.Y, c.Z)));

        // ════════════════════════════════════════════════════════════════════
        // AC 第 1 条 / AC-24-10 —— ≥ 2 个 4-不连通簇 ⇒ **整盘** ROOM_NONE(EC-24-07)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_two_disconnected_clusters_whole_board_returns_room_none()
        {
            // Arrange:命名用例 = 「便器离病房 2 格」—— 簇 A 两格,簇 B 单格,横向相距 2 格
            //   (x=0,1 与 x=3)。x=2 处没有任何格 ⇒ 两簇 4-不连通。
            FakeStructureReadView view = SingleCellInstances(Cell(0, 0), Cell(1, 0), Cell(3, 0));

            // Act
            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            // Assert:整盘 ROOM_NONE,且**不提供部分成员**(防「只评最大块」)
            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None),
                "EC-24-07:≥ 2 簇必须整盘 ROOM_NONE,不得取最大簇或就近簇");
            Assert.That(result.HasRoom, Is.False);
            Assert.That(result.RoomCells, Is.Empty, "ROOM_NONE 时不得残留任何房间条目 / 部分成员");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(2), "失败态须可诊断:簇数 = 2");
            Assert.That(result.CellCount, Is.EqualTo(3), "失败态须可诊断:|S| 仍如实报告");
        }

        [Test]
        public void test_three_clusters_whole_board_returns_room_none()
        {
            // 卡面 QA「Edge cases:三簇同样 ROOM_NONE」—— 不是「2 簇特例」。
            FakeStructureReadView view = SingleCellInstances(Cell(0, 0), Cell(5, 0), Cell(0, 5));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None));
            Assert.That(result.RoomCells, Is.Empty);
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(3));
        }

        [Test]
        public void test_two_clusters_with_shell_bridge_still_two_clusters()
        {
            // 卡面 AC-1 Edge「两簇 + 壳格桥 = 仍 2 簇(壳格不传导)」。
            //
            // 🔁 2026-10-21 修正 —— **原夹具无判别力**(评审 A3-3:`review-sprint17-batch1-2026-10-21.md`)。
            //   原版只是 `SingleCellInstances((0,0), (2,0))` —— 一个中间**什么都不报**的普通两簇布局,
            //   与 `test_two_disconnected_clusters_whole_board_returns_room_none` 同形,把连通判据改坏
            //   (F1:加对角并查)它照样绿。它断言的「壳格不传导」在夹具里**根本没有壳格**,语义空转。
            //
            //   现夹具补上「壳格在本视图里的**唯一可表达形态**」:契约声明外壳格不是 `StructurePlaced`
            //   的实例(`IPlacedStructureReadView.StructureCount`),故真实现不该报它;万一 23 违约上报,
            //   其足迹也只能是**空集**(契约:返回空集 = 该实例不贡献家具格)。
            //   ⇒ 本测钉的不变量 = **空足迹实例既不入 |S|、也不传导连通**。
            //
            //   判别力来源:把 `CollectFurnitureCells` 改成「每个实例至少贡献一格」(instance-as-cell
            //   混淆)⇒ 壳格实例会在 (1,0,0) 造出一格,把两簇桥成 1 簇 ⇒ 本测红(2026-10-21 实测)。
            //   ⚠️ 边界如实登记:若 23 违规上报**非空**的外壳格,24 在类型层面无从识别它是壳格 ——
            //   那一半是**提供方契约**,不是本系统的判据(见接口注释第 41 行)。
            FakeStructureReadView withoutShell = SingleCellInstances(Cell(0, 0), Cell(2, 0));
            var withShell = new FakeStructureReadView(
                new[] { Cell(0, 0) },
                Array.Empty<WorldPos>(),      // ← 壳格桥:空足迹实例
                new[] { Cell(2, 0) });

            ClinicRoomExtractionResult baseline = ClinicRoomExtraction.Extract(withoutShell);
            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(withShell);

            Assert.That(result.ConnectedClusterCount, Is.EqualTo(2),
                "壳格不得传导连通(空足迹不贡献格)");
            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None));
            Assert.That(result.CellCount, Is.EqualTo(2), "壳格不入 |S|");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(baseline.ConnectedClusterCount),
                "空足迹实例不得改变簇数");
            Assert.That(result.CellCount, Is.EqualTo(baseline.CellCount),
                "空足迹实例不得改变 |S| —— instance-as-cell 混淆的直接判据");
            Assert.That(result.RoomId, Is.EqualTo(baseline.RoomId),
                "空足迹实例不得改变结果");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC 第 2 条 —— 4 邻接判据:对角接触不连通(**判别力来源见下方注释**)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_single_l_formed_cluster_forms_room()
        {
            // L 形 (0,0),(1,0),(1,1):每对相邻格的 `|dx|+|dy| == 1` ⇒ 4 连通 ⇒ 1 簇。
            FakeStructureReadView view = SingleCellInstances(Cell(0, 0), Cell(1, 0), Cell(1, 1));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.HasRoom, Is.True, "单簇 L 形必须正常析出房间");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(1));
            Assert.That(AsTuples(result.RoomCells), Is.EquivalentTo(new[]
            {
                (0, 0, 0), (1, 0, 0), (1, 1, 0),
            }), "成员格 = S 全集(不多不少)");
        }

        [Test]
        public void test_diagonal_pair_returns_room_none()
        {
            // 🔑 **判别力来源(显式登记,承 🔁 回填 #1)**
            //   这对夹具 —— `(0,0),(1,0),(1,1)` L 形 vs `(0,0),(1,1)` 对角 —— 在 **8 邻接**下
            //   两者都连成 **1 簇**(对角 `|dx|==1 ∧ |dy|==1` 也算相连)⇒ 若被测实现误用 8 邻接,
            //   **本测与上一个测会给出同样的「1 簇」结论,只有这一个测转红**。
            //   所以它区分的是 **4 邻接 vs 8 邻接**,**不是**「恰取到空集」——
            //   空集分支另有 `test_empty_layout_returns_room_none_not_error` 负责。
            //   夹具刻意只含两格(非空),使「空集」与「2 簇」两条判据互不掩盖。
            FakeStructureReadView view = SingleCellInstances(Cell(0, 0), Cell(1, 1));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.ConnectedClusterCount, Is.EqualTo(2),
                "对角 `|dx|==1 ∧ |dy|==1` 不是 4 邻接(`ABS(dx)+ABS(dy)==1` 不成立)");
            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None));
        }

        [Test]
        public void test_cross_floor_same_xy_not_connected()
        {
            // 4 邻接要求**同层**(z 相等)—— 同 (x,y) 不同 z 的两件家具不连通(ADR-015 单一整数格)。
            FakeStructureReadView view = SingleCellInstances(
                Cell(0, 0, z: 0), Cell(0, 0, z: 1));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.ConnectedClusterCount, Is.EqualTo(2), "跨层不连通(z 不相等)");
            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None));
        }

        [Test]
        public void test_single_cell_forms_trivial_room()
        {
            // 卡面 QA「Edge cases:单格家具 = 平凡 1 簇房间」。
            FakeStructureReadView view = SingleCellInstances(Cell(7, -3));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.HasRoom, Is.True);
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(1));
            Assert.That(result.CellCount, Is.EqualTo(1));
            Assert.That(AsTuples(result.RoomCells), Is.EquivalentTo(new[] { (7, -3, 0) }));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC 第 3 条 —— 开顶(无围合)家具簇照样成房间;无封闭性检查(行为 + 反射双查)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_open_top_line_cluster_forms_room()
        {
            // 一排直线家具格,四边没有任何墙 / 封闭结构 —— GDD 规则一:P0 不要求围合。
            FakeStructureReadView view = SingleCellInstances(
                Cell(0, 0), Cell(1, 0), Cell(2, 0), Cell(3, 0), Cell(4, 0));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.HasRoom, Is.True, "敞开空间的家具簇仍是合法房间");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(1));
        }

        [Test]
        public void test_open_top_concave_and_convex_layouts_both_form_room()
        {
            // 卡面 QA「Edge cases:凹形(有缺口)与凸形结果相同」。
            //   凸形 = 实心 3×3;凹形 = 同一 3×3 去掉 (1,1) —— 开口朝内,仍是 4 连通环。
            //   若实现含任何「内部 / 外部」概念,缺心凹形会被判成「不围合 ⇒ 无房间」而分叉。
            WorldPos[] convex =
            {
                Cell(0, 0), Cell(1, 0), Cell(2, 0),
                Cell(0, 1), Cell(1, 1), Cell(2, 1),
                Cell(0, 2), Cell(1, 2), Cell(2, 2),
            };
            WorldPos[] concave = convex.Where(c => !(c.X == 1 && c.Y == 1)).ToArray();

            ClinicRoomExtractionResult convexResult = ClinicRoomExtraction.Extract(SingleCellInstances(convex));
            ClinicRoomExtractionResult concaveResult = ClinicRoomExtraction.Extract(SingleCellInstances(concave));

            Assert.That(convexResult.HasRoom, Is.True);
            Assert.That(concaveResult.HasRoom, Is.True, "凹形(缺口)不得因「未围合」被拒");
            Assert.That(convexResult.ConnectedClusterCount, Is.EqualTo(1));
            Assert.That(concaveResult.ConnectedClusterCount, Is.EqualTo(1));
            Assert.That(concaveResult.CellCount, Is.EqualTo(convex.Length - 1),
                "凹形只少一格 —— 成员序不因内部空缺而变");
        }

        [Test]
        public void test_open_cluster_code_path_has_no_enclosure_check_reflection()
        {
            // 卡面 QA AC-3「断言代码路径中不存在围合检查(反射/行为双查)」的**反射半边**;
            // 行为半边 = 上面两个测(开顶直线 / 凹形)。
            //
            // 判据 = 生产类型 `ClinicRoomExtraction` 的**全部**成员名(方法 / 字段,含 private)
            // 不含围合语义词。词表刻意**不含** "Closure" —— `FurnTypeClosureEnforced` 的 "Closure"
            // 指 `FURN_TYPES` 闭集,与几何封闭无关(两者混读会得到假红)。
            string[] enclosureTokens =
            {
                "Enclos", "Flood", "Perimeter", "Sealed", "IsClosed", "Boundary", "Interior",
            };

            Type type = typeof(ClinicRoomExtraction);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                                   | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            var memberNames = type.GetMethods(all).Select(m => m.Name)
                .Concat(type.GetFields(all).Select(f => f.Name))
                .ToArray();

            Assert.That(memberNames, Is.Not.Empty, "反空转守卫:成员必须真扫到,否则「零命中」是对空集平凡成立");

            foreach (string token in enclosureTokens)
                Assert.That(memberNames.Where(n => n.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty, $"AC-3:GDD 规则一明令禁围合检查,成员名出现「{token}」");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC 第 3 条补 · 🔁 回填 #2 —— 壳格**内嵌家具** ⇒ 该家具照常析出
        //   (壳格本身仍是「非格」;上列壳格桥测只证「不传导」,**未测**嵌家具)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_furniture_placed_inside_shell_footprint_still_extracts()
        {
            // 夹具:一格家具**落在**医馆外壳足迹的范围内(即「墙里 / 地基上嵌了一件家具」),
            //   而外壳格本身在视图里**不存在**(不是 `StructurePlaced` 的产物 ⇒ 非格)。
            //   ⇒ 该家具照常进 S 并析出;壳格的存在既不加分也不阻断(EC-24-04 / 规则一格分类)。
            FakeStructureReadView view = SingleCellInstances(Cell(2, 2));

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.HasRoom, Is.True, "壳格内嵌的家具必须照常析出");
            Assert.That(result.CellCount, Is.EqualTo(1), "壳格本身不入 |S|(仍是「非格」)");
            Assert.That(AsTuples(result.RoomCells), Is.EquivalentTo(new[] { (2, 2, 0) }));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC 第 4 条 · 🔁 回填 #3 —— 房间标识稳定(纯函数,同一布局两次求值同一 id)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_same_layout_twice_returns_same_room_id()
        {
            // 两个**独立构造**的视图(实例序 / 格序都不同)⇒ 同一布局 ⇒ 同一 room id。
            //   viewA 的实例序 = [(0,0),(1,0),(1,1)];viewB 的实例序 = 逆序,且其中一个实例
            //   一次报两格(多格结构) —— 派生不得依赖实例划分、写入顺序或枚举序。
            FakeStructureReadView viewA = SingleCellInstances(Cell(0, 0), Cell(1, 0), Cell(1, 1));
            var viewB = new FakeStructureReadView(
                new[] { Cell(1, 1) },
                new[] { Cell(1, 0), Cell(0, 0) });

            ClinicRoomExtractionResult first = ClinicRoomExtraction.Extract(viewA);
            ClinicRoomExtractionResult second = ClinicRoomExtraction.Extract(viewB);
            ClinicRoomExtractionResult third = ClinicRoomExtraction.Extract(viewA);

            Assert.That(first.HasRoom, Is.True);
            Assert.That(second.RoomId, Is.EqualTo(first.RoomId),
                "AC 第 5 条:同一布局 ⇒ 同一 room id(与实例划分 / 报告序无关,纯函数)");
            Assert.That(third.RoomId, Is.EqualTo(first.RoomId), "同进程重放逐位一致(ADR-005)");
            Assert.That(AsTuples(second.RoomCells), Is.EquivalentTo(AsTuples(first.RoomCells)));
        }

        [Test]
        public void test_room_id_never_collides_with_room_none_sentinel()
        {
            // `ROOM_NONE = (0,0)` 与真实 room id 空间不交(卡面实施约束)。
            //   世界原点上的单格房间是**最容易撞哨兵**的布局 —— 若 id 取「最小角格」或「锚点」,
            //   此测必红。
            ClinicRoomExtractionResult origin = ClinicRoomExtraction.Extract(SingleCellInstances(Cell(0, 0)));

            Assert.That(origin.HasRoom, Is.True);
            Assert.That(origin.RoomId.IsNone, Is.False,
                "位于世界原点的真实房间不得与 ROOM_NONE (0,0) 混淆");
        }

        [Test]
        public void test_different_layouts_return_different_room_ids()
        {
            // 派生须**对布局敏感**(否则「稳定」会退化为「恒等」的平凡成立)。
            ClinicRoomId a = ClinicRoomExtraction.Extract(SingleCellInstances(Cell(0, 0))).RoomId;
            ClinicRoomId b = ClinicRoomExtraction.Extract(SingleCellInstances(Cell(1, 0))).RoomId;
            ClinicRoomId c = ClinicRoomExtraction.Extract(SingleCellInstances(Cell(0, 0), Cell(1, 0))).RoomId;

            Assert.That(b, Is.Not.EqualTo(a));
            Assert.That(c, Is.Not.EqualTo(a));
            Assert.That(c, Is.Not.EqualTo(b));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC 第 5 条 · 🔁 回填 #4 —— 输入仅只读视图:写路径在**类型层面**不可达
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_read_view_exposes_no_write_path()
        {
            // 判据三层(全为反射 / 类型层,非源文本 grep):
            //   ① 只读视图接口的成员 = {StructureCount getter, GetOccupiedCells, GetModuleId} —— 恰三个;
            //   ② 成员名不含任何写语义词(Place/Remove/Modify/Register/Append/Occupy/Free/Clear/Write);
            //   ③ 析出器**不持状态**(零静态字段)且**只收**该只读视图(参数类型封闭)。
            //
            // 🔁 **2026-10-21 改判(O-24-1 兑现轮 —— 此红是被要求的行为,非回归)**:
            //   原判据钉「恰两个」,`GetModuleId` 加入后本测按设计转红。**改判理由**:AC 第 6 条的
            //   真意是「视图**只读**、无写通道」(本测 ②③ 两层仍在守它);「恰两个」是当时的**代理**,
            //   因为当时 `module_id` 尚未被要求。S17-2 起它是 `FURN_TYPES` 成员判据 + 家具属性源的
            //   必需输入(见 `IPlacedStructureReadView.GetModuleId` 的放行注释)⇒ 闭集表面扩到三个**只读**
            //   成员,写语义断言零放松。**不是**「测试放宽到能过」:若新成员带任何写语义,② 仍会当场红。
            Type viewType = typeof(IPlacedStructureReadView);

            // 属性访问器(get_X / set_X)不单列 —— 由属性名代表;`set_X` 若出现则被下面的
            // 写语义词表捕获(「Set」),故过滤 accessor 不会削弱判据。
            var viewMembers = viewType.GetMethods()
                .Where(m => !m.IsSpecialName)
                .Select(m => m.Name)
                .Concat(viewType.GetProperties().Select(p => p.Name))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.That(viewMembers,
                Is.EquivalentTo(new[] { "GetModuleId", "GetOccupiedCells", "StructureCount" }),
                "AC 第 6 条:输入视图的公开面须恰为这三个**只读**成员(多一个 = 多一条写/状态通道);" +
                "新增成员须先判「是否只读」并在本表登记,不得静默放行");

            string[] writeTokens =
            {
                "Place", "Remove", "Modify", "Register", "Append", "Occupy", "Free", "Clear",
                "Write", "Update", "Set",
            };
            foreach (string token in writeTokens)
                Assert.That(viewMembers.Where(n => n.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty, $"AC 第 6 条:只读视图不得暴露写语义成员「{token}」");

            // ③ 析出器零**可变**状态:静态字段中除编译期常量(IsLiteral)外必须为零
            //    ⇒ 不缓存布局 / 不持建造状态(卡面 §Notes 6 明令不缓存于 24 之外)。
            //    允许的 literal 只有 FNV 混合常量(算法常数,不是可调数值 —— GDD 对析出无调参旋钮);
            //    任何 `static readonly` / 可变静态字段都算状态入口,一律红。
            FieldInfo[] stateFields = typeof(ClinicRoomExtraction).GetFields(
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsLiteral)
                .ToArray();
            Assert.That(stateFields, Is.Empty,
                "析出器须无状态;非 const 静态字段 = 持有建造状态 / 缓存的入口");

            // ③b 参数类型封闭:公有入参只能是**只读抽象** ——
            //     `IPlacedStructureReadView`(结构视图)或 `IReadOnlyCollection<string>`(FURN_TYPES 闭集,
            //     2026-10-21 随真判据入口加入)。不接受 `IEventSink` / `IWorldOccupancy` / 注册表 / 任何写通道。
            //    ⚠️ 白名单是**穷举**的:新增入参类型须先判「是否只读」并在此登记,否则本测红。
            var allowedParams = new[]
            {
                typeof(IPlacedStructureReadView),
                typeof(IReadOnlyCollection<string>),
            };
            foreach (MethodInfo m in typeof(ClinicRoomExtraction).GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (ParameterInfo p in m.GetParameters())
                    Assert.That(allowedParams, Does.Contain(p.ParameterType),
                        $"{m.Name} 的入参 {p.Name} 不在只读白名单内 —— 24 不得接收写通道" +
                        "(新增只读入参须在本表登记)");
            }
        }

        [Test]
        public void test_extraction_does_not_cache_between_calls()
        {
            // 无状态的行为侧:同一视图对象两帧之间布局变化(模拟 23 拆除一件)⇒ 第二次求值必须反映新布局。
            //   若析出器缓存了上次结果,第二断言会读到旧的 1 簇。
            var cellsA = new[] { Cell(0, 0), Cell(1, 0) };
            var view = new MutableView(cellsA);

            Assert.That(ClinicRoomExtraction.Extract(view).ConnectedClusterCount, Is.EqualTo(1));

            view.Cells = new[] { Cell(0, 0), Cell(9, 9) }; // 拆掉 (1,0) 后放一件远处的

            Assert.That(ClinicRoomExtraction.Extract(view).ConnectedClusterCount, Is.EqualTo(2),
                "析出须逐次由输入重算(无跨调用缓存,EC-24-05 的 24 侧)");
        }

        /// <summary>格集可替换的单实例视图(用于「两次求值之间布局变了」)。</summary>
        private sealed class MutableView : IPlacedStructureReadView
        {
            public MutableView(IReadOnlyList<WorldPos> cells) { Cells = cells; }

            public IReadOnlyList<WorldPos> Cells { get; set; }

            public int StructureCount => Cells.Count;

            public IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex) => new[] { Cells[structureIndex] };

            /// <summary>无模块标识 —— 本夹具只服务代理判据。</summary>
            public string GetModuleId(int structureIndex) => null;
        }

        // ════════════════════════════════════════════════════════════════════
        // 🔁 回填 #5 —— 空布局 ⇒ ROOM_NONE,**不是错误**(EC-24-01)
        //   同 (EC-24-02 的中性面在此 story 的边界 = 本 story 不含类型匹配,S17-2 承接)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_empty_layout_returns_room_none_not_error()
        {
            // 开局空医馆:视图零实例。不抛异常 = EC-24-01「不是错误,是合法状态」。
            var empty = new FakeStructureReadView();

            ClinicRoomExtractionResult result = default;
            Assert.DoesNotThrow(() => result = ClinicRoomExtraction.Extract(empty),
                "EC-24-01:空布局是合法状态,不得抛");

            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None));
            Assert.That(result.HasRoom, Is.False);
            Assert.That(result.RoomCells, Is.Empty);
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(0), "0 簇(区别于 2 簇的失败态)");
            Assert.That(result.CellCount, Is.EqualTo(0));
        }

        [Test]
        public void test_view_reporting_empty_footprints_returns_room_none()
        {
            // 退化形态:视图报「有实例但零占用格」(合法契约面:该实例不贡献家具格)。
            //   ⇒ 与空布局同解(ROOM_NONE),不因「有实例」而伪造一个房间。
            var view = new FakeStructureReadView(
                Array.Empty<WorldPos>(), Array.Empty<WorldPos>());

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.RoomId, Is.EqualTo(ClinicRoomId.None));
            Assert.That(result.CellCount, Is.EqualTo(0));
        }

        // ════════════════════════════════════════════════════════════════════
        // 陷阱 ①(卡面实测)—— 多格结构的**非锚点格**必须被遍历
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_multi_cell_structure_non_anchor_cells_are_traversed()
        {
            // 判别力:两件**各占 2 格**的结构 ——
            //   A 佔 {(0,0),(1,0)}(锚点 (0,0)),B 佔 {(2,0),(3,0)}(锚点 (2,0))。
            //   · 正确(遍历 `OccupiedCells`)⇒ S = 4 格,4 连通 ⇒ 1 簇 ⇒ **成房间**;
            //   · 踩陷阱(只比 Anchor / 退化成逐格 `GetInstanceAt`)⇒ S = {(0,0),(2,0)} ⇒ **2 簇 ⇒ ROOM_NONE**。
            //   两种解释给出**相反**结论,故本测真的在钉这条陷阱。
            var view = new FakeStructureReadView(
                new[] { Cell(0, 0), Cell(1, 0) },
                new[] { Cell(2, 0), Cell(3, 0) });

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.CellCount, Is.EqualTo(4), "|S| 须含非锚点足迹格");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(1),
                "只取锚点会把一件连续家具拆成两簇 —— 陷阱 ① 的复现形态");
            Assert.That(result.HasRoom, Is.True);
            Assert.That(AsTuples(result.RoomCells), Is.EquivalentTo(new[]
            {
                (0, 0, 0), (1, 0, 0), (2, 0, 0), (3, 0, 0),
            }));
        }

        [Test]
        public void test_overlapping_occupied_cells_counted_once()
        {
            // 防御面:两件结构的足迹格重叠(同一格被登记两次)⇒ |S| 去重,不重复计入。
            var view = new FakeStructureReadView(
                new[] { Cell(0, 0), Cell(1, 0) },
                new[] { Cell(1, 0) });

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.CellCount, Is.EqualTo(2), "重叠格只算一件家具格");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(1));
            Assert.That(AsTuples(result.RoomCells), Is.EquivalentTo(new[] { (0, 0, 0), (1, 0, 0) }));
        }

        // ════════════════════════════════════════════════════════════════════
        // `int.MaxValue` 邻格溢出守卫 —— 本层是**唯一变异可证**的载体
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 并查集枚举的溢出守卫(生产 `CountComponents` 的 `c.X &lt; int.MaxValue`):
        /// 边界格**不得**把 `int.MinValue` 当成它的 +x 邻居并进同一簇。
        /// <para>⚠️ <b>立本测缘由(2026-10-21 补齐轮 · 变异可证性缺口)</b>:同一形态的守卫在工程里有
        /// <b>两处</b> —— 本层 `CountComponents` 与 `ClinicRoomMatcher.HasNeighbourOfType`。
        /// 匹配期那处**结构上不可触发**:`typeByCell` 的键 ⊆ <see cref="ClinicRoomExtractionResult.RoomCells"/>,
        /// 而单簇不可能同时含 `int.MaxValue` 与 `int.MinValue`(相距 2³²−1,4-连通路径在 int 域内不存在)
        /// ⇒ 删掉它**没有任何测能转红**。本层不同:<see cref="IPlacedStructureReadView.GetOccupiedCells"/>
        /// **不要求连通**,单个实例可以同时占这两格 ⇒ 守卫删掉即可观测。</para>
        /// <para><b>判别力</b>:删掉 `c.X &lt; int.MaxValue` ⇒ 两格被并成 1 簇 ⇒ `ConnectedClusterCount`
        /// 由 2 变 1 且 `HasRoom` 由 false 变 true ⇒ 本测红。</para>
        /// </summary>
        [Test]
        public void test_countComponents_xAtIntMax_noPhantomUnionFromWraparound()
        {
            // 同一件结构同时占两格(跨整数的两端)。二者在整数格上相距 2³²−1 ⇒ **不相邻**。
            var maxCell = Cell(int.MaxValue, 0);
            var minCell = Cell(int.MinValue, 0);
            var view = new FakeStructureReadView(new[] { maxCell, minCell });

            ClinicRoomExtractionResult result = ClinicRoomExtraction.Extract(view);

            Assert.That(result.CellCount, Is.EqualTo(2), "前提:两格都进了成员集");
            Assert.That(result.ConnectedClusterCount, Is.EqualTo(2),
                "int.MaxValue 的 +x 邻格不存在 ⇒ 不得因回绕把它与 int.MinValue 并成一簇。" +
                "删掉 `c.X < int.MaxValue` 守卫,本测转红");
            Assert.That(result.HasRoom, Is.False, "2 簇 ⇒ EC-24-07 整盘 ROOM_NONE");
            Assert.That(result.RoomCells, Is.Empty, "ROOM_NONE 不提供部分成员");
        }

        // ════════════════════════════════════════════════════════════════════
        // 缺口哨兵 —— `FURN_TYPES` 闭集缺席**可观测而非静默**(承 clinic-001 先例)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_furn_type_closure_absent_sentinel_false()
        {
            // **缺口哨兵**(卡面 §Notes 7 实施约束 ② / 承 `clinic-001` 的
            //   `test_room_furn_membership_absent_sentinel_false` 口径)。
            //
            // 被自陈的事实:生产路径**没有** `FURN_TYPES` 闭集(`O-24-1` 未兑现 / `TD-clinic-01`),
            //   析出用的是**代理判据**(被 `StructurePlaced` 占用的格)—— **代理更宽**,不是等价物。
            //   `FurnTypeClosureEnforced == false` 使这一缺席**可观测**:任何读者都能在类型面上
            //   看出「成员判据未按 GDD F-24 的闭集强制」。
            //
            // ⚠️ `O-24-1` 落地后本测**转红是被要求的行为**,不得当回归修掉:
            //   届时须同时改判据(§Notes 7 实施约束 ③),并如实登记既有测试因不等价而转红的部分。
            Assert.That(ClinicRoomExtraction.FurnTypeClosureEnforced, Is.False,
                "缺口哨兵:FURN_TYPES 闭集在生产路径缺席 —— 当前判据是代理(更宽),不是等价物");
        }

        [Test]
        public void test_furn_type_closure_absent_isDetectable_notJustObservable()
        {
            // 🔁 2026-10-21(评审 C11):`FurnTypeClosureEnforced => false` 是**编译期常量**,
            //   它让缺席「可观测」,但不让缺席「可检测」——**有人落地了 FURN_TYPES 却忘改哨兵,
            //   没有任何测试会红**。本测把「可检测」补上:`Sim` 程序集里一旦出现 FurnType 痕迹
            //   (类型名 / 成员名含 `FurnType`,**哨兵自身除外**),哨兵就必须为 true,否则本测硬失败。
            //
            // ⚠️ **判据的前提如实登记**:检测靠**命名**。闭集若以不含 `FurnType` 的名字落地
            //   (如 `FurnitureKind`),本测检测不到 —— 那是本判据的已知边界,不是「已覆盖」。
            //   该约定须在落地方遵守:新增闭集类型/成员时名字里带 `FurnType`。
            Assembly asm = typeof(ClinicRoomExtraction).Assembly;
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                Assert.Fail("程序集类型加载失败 ⇒ 本检测无从判定,不得当作通过");
                return;
            }

            var signals = new List<string>();
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].Name.IndexOf("FurnType", StringComparison.OrdinalIgnoreCase) >= 0)
                    signals.Add("类型 " + types[i].Name);

                MemberInfo[] members = types[i].GetMembers(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int m = 0; m < members.Length; m++)
                {
                    string name = members[m].Name;
                    if (name.IndexOf("FurnType", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // 哨兵自身不算「闭集落地的痕迹」(否则本测自己把自己判红)。
                    // ⚠️ 反射给出的是**访问器名**(`get_FurnTypeClosureEnforced`),不是属性名 ——
                    //   首版按 `== nameof(...)` 排除,漏掉访问器 ⇒ 本测自己转红(实测,已修)。
                    //   先剥 `get_` / `set_` 前缀再比。
                    string bare = name.StartsWith("get_", StringComparison.Ordinal) ||
                                  name.StartsWith("set_", StringComparison.Ordinal)
                        ? name.Substring(4) : name;
                    if (string.Equals(bare, nameof(ClinicRoomExtraction.FurnTypeClosureEnforced),
                                      StringComparison.Ordinal)) continue;

                    signals.Add(types[i].Name + "." + name);
                }
            }

            if (signals.Count == 0)
            {
                Assert.That(ClinicRoomExtraction.FurnTypeClosureEnforced, Is.False,
                    "无 FurnType 痕迹 ⇒ 哨兵须为 false(否则哨兵在撒谎)");
                return;
            }

            // 🔁 **2026-10-21 改判(O-24-1 兑现轮 —— 承卡面实施约束 ③「届时改判据并如实登记,
            //    不得静默改绿」)**:旧判据 = 「有痕迹 ⇒ 哨兵必须 true」。它**与本轮的真实状态冲突**:
            //    闭集已落地(有痕迹),但**析出的默认单参入口仍走代理判据** —— `FurnTypeClosureEnforced`
            //    的真值此时**就是 false**,把它翻成 true 才是撒谎。
            //    ⇒ 改为**更锐利**的判据:痕迹存在时,「哨兵仍 false」只有在下面两条**同时机器可查**时
            //    才合法 —— 否则它退化成了口头承诺:
            //      ① 生产属性源已就位(`FurnTraitsProductionReady == true`)⇒ 闭集在**评分链**上真被强制;
            //      ② 真判据入口**存在**(`Extract(view, IReadOnlyCollection<string>)` 重载)⇒ 装配层有路可选。
            //    ⚠️ 将来若把默认入口也改成真判据 ⇒ 把哨兵改 true,**并删掉本段豁免**。
            Assert.That(ClinicModifierEvaluator.FurnTraitsProductionReady, Is.True,
                "检测到 FURN_TYPES 闭集痕迹,但生产属性源未就绪(FurnTraitsProductionReady != true)" +
                " ⇒ 闭集只在烘焙期强制、运行期无人消费。痕迹:" + string.Join(", ", signals));

            bool hasTruePredicateOverload = false;
            foreach (MethodInfo m in typeof(ClinicRoomExtraction).GetMethods(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != nameof(ClinicRoomExtraction.Extract)) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 2 &&
                    ps[0].ParameterType == typeof(IPlacedStructureReadView) &&
                    ps[1].ParameterType == typeof(IReadOnlyCollection<string>))
                {
                    hasTruePredicateOverload = true;
                    break;
                }
            }
            Assert.That(hasTruePredicateOverload, Is.True,
                "哨兵仍为 false 的前提 = 真判据入口存在(Extract(view, furnTypes));" +
                "两者都不成立时哨兵即失去含义 —— 要么补重载,要么把哨兵改 true");
        }
    }
}
