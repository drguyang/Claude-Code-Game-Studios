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
