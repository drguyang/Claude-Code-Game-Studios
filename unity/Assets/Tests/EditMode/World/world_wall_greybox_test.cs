// 世界墙体灰盒(丙 · D2 最小美术)夹具。
//
// 权威:`unity/Assets/Gameplay.Presentation/World/WorldWallGreybox.cs` 头注的三条纪律。
//
// ⚠️ 本件钉的是**派生规则**(哪些格出体块 / 网格规模 / 幂等 / 材质缺失不半铺),
//   不是画面效果 —— 好不好看只有真包截图能判(见 `production/qa/evidence/`)。
// ⚠️ 材质用 `Shader.Find("Unlit/Color")` 现造:夹具**不读** `Resources/PatientPlaceholderMaterial`
//   (那张资产要不要在、在不在,是生产路径的事;夹具读它会让「资产改名」变成「墙的夹具红」,
//   而两者没有因果关系)。

using DaYiJingCheng.Gameplay.Presentation.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.World
{
    /// <summary>墙体灰盒:纯派生 / 只画壳 / 幂等 / 材质缺失时的行为。</summary>
    [TestFixture]
    public class world_wall_greybox_test
    {
        private Material _mat;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Unlit/Color");
            if (shader != null) _mat = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            if (_mat != null)
            {
                Object.DestroyImmediate(_mat);
                _mat = null;
            }
        }

        /// <summary>构造一个 `sx × 1 × sz` 的可走性数组;`open` 里的格标 1。</summary>
        private static int[] Grid(int sx, int sz, params (int x, int z)[] open)
        {
            var w = new int[sx * sz];
            foreach (var (x, z) in open) w[x * sz + z] = 1;
            return w;
        }

        /// <summary>
        /// **只画壳**:3×3 里只有中心可走 ⇒ 只有它的**正交**四邻出体块(对角四格不补角)。
        /// </summary>
        [Test]
        public void test_boundary_onlyOrthogonalNeighbours_growWalls()
        {
            var walls = new WorldWallGreybox(_mat);
            int cells = walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));

            Assert.That(cells, Is.EqualTo(4),
                "中心可走 ⇒ 上下左右四格是墙,对角四格不是(补角会让转角变成 2×2 厚块)");
            walls.DestroyAll();
        }

        /// <summary>全不可走 / 全可走 ⇒ 一块都不出(没有「边界」这回事)。</summary>
        [Test]
        public void test_noBoundary_noWalls()
        {
            var walls = new WorldWallGreybox(_mat);

            Assert.That(walls.BuildWalls(3, 1, 3, new int[9]), Is.EqualTo(0),
                "全不可走 ⇒ 没有开放邻格,不该铺墙(否则整块地图变成实心体)");

            var allOpen = new int[9];
            for (int i = 0; i < allOpen.Length; i++) allOpen[i] = 1;
            Assert.That(walls.BuildWalls(3, 1, 3, allOpen), Is.EqualTo(0),
                "全可走 ⇒ 没有边界");

            walls.DestroyAll();
        }

        /// <summary>每个边界格 24 顶点 / 36 索引(6 面 × 4 顶点 × 2 三角)。</summary>
        [Test]
        public void test_meshSize_isTightlyDerivedFromCellCount()
        {
            var walls = new WorldWallGreybox(_mat);
            int cells = walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));
            Assert.That(cells, Is.EqualTo(4));

            GameObject root = GameObject.Find(WorldWallGreybox.RootName);
            Assert.That(root, Is.Not.Null, "根节点须以 RootName 命名 —— 走查与后续清理都靠它定位");
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(cells * 24));
            Assert.That(mesh.triangles.Length, Is.EqualTo(cells * 36));
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), "每个顶点都要有法线");

            walls.DestroyAll();
        }

        /// <summary>墙贴地:最低点 y = 0、最高点 y = WallHeight(否则墙体要么悬空要么陷进地里)。</summary>
        [Test]
        public void test_wallsSitOnGround()
        {
            var walls = new WorldWallGreybox(_mat);
            walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));

            Bounds b = GameObject.Find(WorldWallGreybox.RootName)
                .GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(b.min.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(b.max.y, Is.EqualTo(WorldWallGreybox.WallHeight).Within(1e-4f));

            walls.DestroyAll();
        }

        /// <summary>幂等:连铺两次不得叠出第二套墙(叠出来是静默的,画面只是变厚)。</summary>
        [Test]
        public void test_buildTwice_doesNotStackSecondWallSet()
        {
            var first = new WorldWallGreybox(_mat);
            first.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));

            var second = new WorldWallGreybox(_mat);   // 模拟世界切换后的新装配根
            second.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));

            int roots = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == WorldWallGreybox.RootName) roots++;

            Assert.That(roots, Is.EqualTo(1), "同一时刻只允许一个墙体根(残留根须在铺之前清掉)");

            first.DestroyAll();
            second.DestroyAll();
        }

        /// <summary>**让位**:被真件覆盖的格不铺灰盒(否则真件被灰盒挡住)。
        /// <para>🗝 立此条缘由(2026-11-13 实测):`wall-window` 摆在北墙 z=29,与灰盒同格,
        /// 画面上只剩一坨 2.6 m 高的灰墙 —— 走查者会据此判「美术不好用」。</para></summary>
        [Test]
        public void test_suppressedCells_yieldToArt()
        {
            var walls = new WorldWallGreybox(_mat);
            Assert.That(walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1))), Is.EqualTo(4));

            int suppressed = walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)),
                                              (x, z) => x == 1 && z == 0);
            Assert.That(suppressed, Is.EqualTo(3), "被覆盖的格必须让位,其余照铺");

            walls.DestroyAll();
        }

        /// <summary>入参非法(空数组 / 尺寸为 0 / 长度不够)⇒ 返回 0 且**不留节点**。</summary>
        [Test]
        public void test_invalidInput_buildsNothing()
        {
            var walls = new WorldWallGreybox(_mat);

            Assert.That(walls.BuildWalls(3, 1, 3, null), Is.EqualTo(0), "空数组");
            Assert.That(walls.BuildWalls(0, 1, 0, new int[0]), Is.EqualTo(0), "尺寸为 0");
            Assert.That(walls.BuildWalls(3, 1, 3, new int[4]), Is.EqualTo(0),
                "长度不足 ⇒ 拒(越界下标会读到别的关卡的可走性)");

            Assert.That(GameObject.Find(WorldWallGreybox.RootName), Is.Null,
                "任何一种非法入参都不得留下节点");
        }

        /// <summary>墙体必须带碰撞体 —— 「看得见的墙」与「走不过去的墙」必须是同一个东西。
        /// <para>🗝 立此条缘由(2026-11-13 真包实测):首版只画不挡,按住 A 从 (18,21)
        /// 穿过西墙走到 (-5,-4,21) 掉出世界。移动层没有任何可走性查询 ⇒ 碰撞体是唯一约束。</para></summary>
        [Test]
        public void test_wallsCarryCollider()
        {
            var walls = new WorldWallGreybox(_mat);
            walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));

            GameObject root = GameObject.Find(WorldWallGreybox.RootName);
            var collider = root.GetComponent<MeshCollider>();
            Assert.That(collider, Is.Not.Null, "没有碰撞体 = 玩家可以穿墙,那比不画墙更糟");
            Assert.That(collider.sharedMesh, Is.SameAs(root.GetComponent<MeshFilter>().sharedMesh),
                "碰撞体与渲染须用同一张网格(否则「看到的」与「挡住的」分叉)");

            walls.DestroyAll();
        }

        /// <summary>**窗洞**:命中格由一整块拆成「窗下墙」,洞口高度以上不再有几何。
        /// <para>🗝 立此条缘由(2026-11-13 · §8.11 开窗):面板上开洞后,若灰盒仍是整格通高,
        /// 透过去看到的是 0.5 m 外那块 2.6 m 灰板 —— 窗读作「墙外一堵灰墙」。
        /// 而窗下那块**必须留着**:它才是「走不过去」的那一半。</para>
        /// <para>⚠️ 断言手法:用 `suppressedCells` 把**其余格全让位**,只留被命中的那一格 ——
        /// 否则相邻格的角点会与这一格共用坐标,任何「按包围盒筛顶点」的写法都会把它们算进来
        /// (首版就是这么绿的假象:其实测到的是邻格的 2.6 m)。</para></summary>
        [Test]
        public void test_cellCut_leavesSillBlock_andClearsAboveIt()
        {
            const float sill = 1.10f, head = 2.90f;   // 与 `InteriorWindows.West` 同量级
            var walls = new WorldWallGreybox(_mat);
            int cells = walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)),
                                         suppressedCells: (x, z) => !(x == 1 && z == 0),
                                         cellCut: (x, z) => (x == 1 && z == 0)
                                             ? new Vector2(sill, head)
                                             : (Vector2?)null);

            Assert.That(cells, Is.EqualTo(1), "只留被命中的那一格(其余让位)");

            var mesh = GameObject.Find(WorldWallGreybox.RootName).GetComponent<MeshFilter>().sharedMesh;
            float maxY = float.NegativeInfinity;
            bool hasSill = false;
            foreach (Vector3 p in mesh.vertices)
            {
                if (p.y > maxY) maxY = p.y;
                if (Mathf.Abs(p.y - sill) < 1e-3f) hasSill = true;
            }

            Assert.That(hasSill, Is.True, "窗下墙必须在(0..窗台)—— 它是挡住通行的那一半");
            Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(1e-3f), "窗下墙从地面起");
            Assert.That(maxY, Is.LessThanOrEqualTo(sill + 1e-3f),
                "窗台以上不得再有几何,否则窗口后面仍是灰板");
            Assert.That(head, Is.GreaterThan(WorldWallGreybox.WallHeight),
                "本关的窗顶(2.9)高于灰盒墙高(2.6)⇒ 洞口上界截在墙顶,不生成「窗上墙」");

            walls.DestroyAll();
        }

        /// <summary>窗顶**低于**墙高时,那一格必须拆成两块(窗下 + 窗上),否则洞上方会开天窗。</summary>
        [Test]
        public void test_cellCut_belowWallTop_producesTwoPieces()
        {
            const float sill = 1.10f, head = 1.80f;
            var walls = new WorldWallGreybox(_mat);
            int cells = walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)),
                                         suppressedCells: (x, z) => !(x == 1 && z == 0),
                                         cellCut: (x, z) => (x == 1 && z == 0)
                                             ? new Vector2(sill, head)
                                             : (Vector2?)null);
            Assert.That(cells, Is.EqualTo(1));

            var mesh = GameObject.Find(WorldWallGreybox.RootName).GetComponent<MeshFilter>().sharedMesh;
            bool atSill = false, atHead = false, atTop = false;
            foreach (Vector3 p in mesh.vertices)
            {
                if (Mathf.Abs(p.y - sill) < 1e-3f) atSill = true;
                if (Mathf.Abs(p.y - head) < 1e-3f) atHead = true;
                if (Mathf.Abs(p.y - WorldWallGreybox.WallHeight) < 1e-3f) atTop = true;
            }

            Assert.That(atSill, Is.True, "窗台那条边");
            Assert.That(atHead, Is.True, "窗顶那条边");
            Assert.That(atTop, Is.True, "窗上墙必须补回墙顶(否则洞上方开天窗)");

            walls.DestroyAll();
        }

        /// <summary>窗洞作者表 ↔ 灰盒格:每扇窗的每一格都命中,窗外一格不命中,洞口落在开口之内。
        /// <para>这条钉的是**对应关系**,不是画面。拱窗另验「拱顶 = 起拱 + 半径」。</para></summary>
        [Test]
        public void test_windowTable_everyOpeningMapsToItsCells()
        {
            Assert.That(InteriorWindows.All.Length, Is.GreaterThanOrEqualTo(2),
                "西墙大窗 + 北墙拱窗");

            foreach (InteriorWindows.Opening w in InteriorWindows.All)
            {
                for (int c = w.CellAlong0; c <= w.CellAlong1; c++)
                {
                    int x = w.Axis == InteriorWindows.WallAxis.AlongZ ? w.CellFixed : c;
                    int z = w.Axis == InteriorWindows.WallAxis.AlongZ ? c : w.CellFixed;

                    Assert.That(InteriorWindows.TryGetCellCut(x, z, out float y0, out float y1),
                        Is.True, $"{w.Name}:格 ({x},{z}) 应命中");
                    Assert.That(y0, Is.EqualTo(w.Y0).Within(1e-4f), $"{w.Name}:窗台");
                    Assert.That(y1, Is.EqualTo(w.Y1).Within(1e-4f), $"{w.Name}:洞顶");
                }

                // 面板洞口必须落在灰盒开口**之内**(否则窗角会露出灰板)
                Assert.That(w.A0, Is.GreaterThan(w.CellAlong0 - 0.5f), $"{w.Name}:起点");
                Assert.That(w.A1, Is.LessThan(w.CellAlong1 + 0.5f), $"{w.Name}:终点");

                // 窗外那一格不命中
                int ox = w.Axis == InteriorWindows.WallAxis.AlongZ ? w.CellFixed : w.CellAlong0 - 1;
                int oz = w.Axis == InteriorWindows.WallAxis.AlongZ ? w.CellAlong0 - 1 : w.CellFixed;
                Assert.That(InteriorWindows.TryGetCellCut(ox, oz, out _, out _), Is.False,
                    $"{w.Name}:窗外的格不得命中");

                if (w.IsArched)
                {
                    Assert.That(w.ArchY, Is.GreaterThan(w.Y0), $"{w.Name}:起拱须高于窗台");
                    Assert.That(w.Y1, Is.EqualTo(w.ArchY + w.ArchRadius).Within(1e-3f),
                        $"{w.Name}:拱顶 = 起拱 + 半径(半圆)");
                }
            }
        }

        /// <summary>地坪:闭区间矩形 ⇒ 半格内缩覆盖整格,且挂在墙体根之下。</summary>
        [Test]
        public void test_floor_coversInclusiveCellRect()
        {
            var walls = new WorldWallGreybox(_mat);
            walls.BuildWalls(3, 1, 3, Grid(3, 3, (1, 1)));
            Assert.That(walls.BuildFloor(0, 0, 2, 2), Is.EqualTo(1));

            GameObject root = GameObject.Find(WorldWallGreybox.RootName);
            Transform floor = root.transform.Find("WorldFloor");
            Assert.That(floor, Is.Not.Null, "地坪须挂在墙体根之下(统一清理)");

            Bounds b = floor.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(b.min.x, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(b.max.x, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(b.min.z, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(b.max.z, Is.EqualTo(2.5f).Within(1e-4f));

            walls.DestroyAll();
        }
    }
}
