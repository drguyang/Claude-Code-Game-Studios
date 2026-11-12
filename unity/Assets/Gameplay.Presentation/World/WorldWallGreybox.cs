// 世界墙体的**灰盒载体**(2026-11-13 · 丙 · D2 最小美术)
//
// 权威来源:
//   ADR-015 §一(两层世界:视觉层 float 任意,**采样禁入 sim**)
//   ADR-009 §四(**派生态**:由外生源确定性重建,不进流、不存档)
//   ADR-023 §②(`World.unity` **零 gameplay GameObject** —— 预摆 = 第二真源)
//
// ═══ 本类补的是什么 ═══════════════════════════════════════════════════════
//   真包走查(2026-11-13)实测:序章第一段的画面里**只有一块地面平板**
//   —— 教堂的墙在**逻辑层是存在的**(`world_geometry.cooked.bytes` 的
//   `walkable` 有 2555 格不可走,教堂轮廓与东墙塌口都在数据里),但**没有任何网格**
//   ⇒ 玩家站在教堂里看见的是旷野,而旁白在说「钟楼」「案头」「告示」。
//
//   ⇒ 本类把「已存在但看不见」的那份数据**画出来**:每个与可走格相邻的不可走格
//     出一块体块。这是**纯派生**(输入只有几何产物),没有第二份作者数据。
//
// ═══ 三条纪律 ═════════════════════════════════════════════════════════════
//   ① **不判可走性** —— 通行由 `PlayerController` / 导航格定;本类只画。
//      画出来的墙与「走不过去的地方」同源,但**它不参与判定**:改这里不会让墙变实。
//   ② **合并成一张网格** —— 边界格约 400 块,逐块 `CreatePrimitive` 会造 400 个
//      `GameObject`(每个带 Renderer + Collider);一张网格是一样的画面,零碰撞体。
//   ③ **只画壳** —— 不可走区的**内部**格不画(它们本来也看不见),故数量 = 边界格数,
//      与世界面积无关(128×40 下实测 406)。
//
// ⚠️ 底材质沿用占位那张 `Resources` 资产(与病人胶囊 / 结构体块同一张)——
//    运行期 `Shader.Find` 手搓材质在打包版会被当未使用剥离(`TD-build-03` 的教训)。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>把世界几何的**不可走边界**画成灰盒墙体(纯派生,零美术资产)。</summary>
    public sealed class WorldWallGreybox
    {
        /// <summary>根节点名(走查与夹具按名定位;与 `StructureVisuals` 并列)。</summary>
        public const string RootName = "WorldWalls";

        /// <summary>格边长(米/格)。占位期口径,与 `StructureVisualFactory.MetersPerCell` 同值。
        /// <para>⚠️ 三处必须同尺度(病人 / 结构 / 墙),否则「看得到的」与「走得通的」不是同一件事。</para></summary>
        public const float MetersPerCell = 1f;

        /// <summary>墙体高度(米)。灰盒期取 2.6 —— 高于角色(1.7)而低于相机高度,能挡视线不挡看路。</summary>
        public const float WallHeight = 2.6f;

        /// <summary>占位底材质的 Resources 名(与病人 / 结构共用同一张资产)。</summary>
        public const string BaseMaterialResourcePath = "PatientPlaceholderMaterial";

        /// <summary>墙的色调(土灰;与病人的中性灰、结构的草木绿拉开一档)。</summary>
        public static readonly Color WallTint = new Color(0.62f, 0.60f, 0.55f);

        /// <summary>地面的色调(比世界地面略深 —— 用来把「关内」与「旷野」分开)。</summary>
        public static readonly Color FloorTint = new Color(0.42f, 0.40f, 0.36f);

        private GameObject _root;
        private Material _base;
        private Material _wallMaterial;
        private Material _floorMaterial;

        /// <summary>构造。</summary>
        /// <param name="baseMaterial">底材质;**null = 按 <see cref="BaseMaterialResourcePath"/>
        /// 从 `Resources` 取**(生产路径)。夹具可注入自造材质以免依赖资产。</param>
        public WorldWallGreybox(Material baseMaterial = null)
        {
            _base = baseMaterial;
        }

        /// <summary>最近一次铺出的墙体格数(诊断读数用)。</summary>
        public int WallCells { get; private set; }

        /// <summary>最近一次铺出的地面块数(诊断读数用)。</summary>
        public int FloorTiles { get; private set; }

        /// <summary>
        /// 按几何产物的不可走边界铺墙体(幂等:先清后建)。
        /// </summary>
        /// <param name="sizeX">世界 X 格数。</param>
        /// <param name="sizeY">世界 Y 格数(当前关卡为 1;本类只画 `y = 0` 那一层)。</param>
        /// <param name="sizeZ">世界 Z 格数。</param>
        /// <param name="walkable">可走性(0/1;下标 = `(x * sizeY + y) * sizeZ + z`)。</param>
        /// <returns>铺出的墙体格数;材质取不到 ⇒ 0(不让半个世界凭空消失,由日志区分「没铺」)。</returns>
        public int BuildWalls(int sizeX, int sizeY, int sizeZ, int[] walkable)
        {
            DestroyAll();
            DestroyOrphanRoots();
            if (walkable == null || sizeX < 1 || sizeY < 1 || sizeZ < 1) return 0;
            if (walkable.Length < sizeX * sizeY * sizeZ) return 0;
            if (!EnsureMaterials()) return 0;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            int cells = 0;

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (IsWalkable(walkable, sizeX, sizeY, sizeZ, x, 0, z)) continue;
                    if (!TouchesOpenCell(walkable, sizeX, sizeY, sizeZ, x, z)) continue;

                    AddBox(verts, normals, tris,
                           new Vector3(x * MetersPerCell, WallHeight * 0.5f, z * MetersPerCell),
                           new Vector3(0.5f * MetersPerCell, WallHeight * 0.5f, 0.5f * MetersPerCell));
                    cells++;
                }
            }

            if (cells == 0) return 0;

            var mesh = new Mesh { name = "WorldWallsMesh" };
            // 边界格 × 24 顶点:大世界会越过 65k 顶点上限 ⇒ 显式 32 位索引(不靠尺寸假设)。
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(RootName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _wallMaterial;
            // ⚠️ **碰撞体不是可选项**(2026-11-13 实测):此前移动层**从不读可走性** ——
            //    `PlayerController` 里没有任何 `walkable` 查询,墙也只是没渲染的数据。
            //    首版只画不挡时,真包里按住 A 从 (18,21) 一路穿过西墙到 (-5,-4,21) 掉出世界。
            //    ⇒ 墙体带上 `MeshCollider`(与渲染同一张网格、同一份派生数据),
            //      「看得见的墙」与「走不过去的墙」才是同一个东西。
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            _root = go;
            WallCells = cells;
            return cells;
        }

        /// <summary>
        /// 在指定格矩形内铺一块地面(单张四边形;幂等 —— 与墙体共用根节点)。
        /// </summary>
        /// <param name="x0">起始格 X(闭区间)。</param>
        /// <param name="z0">起始格 Z(闭区间)。</param>
        /// <param name="x1">结束格 X(闭区间)。</param>
        /// <param name="z1">结束格 Z(闭区间)。</param>
        /// <param name="lift">抬离世界地面的高度(米;防腐面重叠闪烁)。</param>
        /// <returns>铺出的块数(0 或 1)。</returns>
        public int BuildFloor(int x0, int z0, int x1, int z1, float lift = 0.02f)
        {
            if (x1 < x0 || z1 < z0) return 0;
            if (!EnsureMaterials()) return 0;

            // 格中心在整数坐标 ⇒ 半格内缩出整格边界。
            float minX = (x0 - 0.5f) * MetersPerCell;
            float maxX = (x1 + 0.5f) * MetersPerCell;
            float minZ = (z0 - 0.5f) * MetersPerCell;
            float maxZ = (z1 + 0.5f) * MetersPerCell;

            var verts = new List<Vector3>
            {
                new Vector3(minX, lift, minZ),
                new Vector3(maxX, lift, minZ),
                new Vector3(maxX, lift, maxZ),
                new Vector3(minX, lift, maxZ),
            };
            var normals = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var tris = new List<int> { 0, 2, 1, 0, 3, 2 };   // 俯视 CCW ⇒ 法线 +Y

            var mesh = new Mesh { name = "WorldFloorMesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            if (_root == null) _root = new GameObject(RootName);
            var go = new GameObject("WorldFloor");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _floorMaterial;
            go.transform.SetParent(_root.transform, false);
            FloorTiles = 1;
            return 1;
        }

        /// <summary>销毁全部已铺视觉(幂等;世界切换 / 重装配时调)。</summary>
        public void DestroyAll()
        {
            if (_root != null)
            {
                if (Application.isPlaying) Object.Destroy(_root);
                else Object.DestroyImmediate(_root);
                _root = null;
            }
            WallCells = 0;
            FloorTiles = 0;
        }

        /// <summary>
        /// 清掉**不属于本实例**的残留根(世界切换 = 重建装配根 ⇒ 上一轮的实例没了簿记)。
        /// <para>⚠️ 叠出第二套墙是**静默**的:画面上只是墙体「变厚一点」,没有报错。</para>
        /// </summary>
        private void DestroyOrphanRoots()
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !string.Equals(all[i].name, RootName)) continue;
                if (Application.isPlaying) Object.Destroy(all[i]);
                else Object.DestroyImmediate(all[i]);
            }
        }

        private bool EnsureMaterials()
        {
            if (_wallMaterial != null && _floorMaterial != null) return true;
            Material baseMat = _base != null ? _base : Resources.Load<Material>(BaseMaterialResourcePath);
            if (baseMat == null) return false;
            if (_wallMaterial == null) _wallMaterial = new Material(baseMat) { color = WallTint };
            if (_floorMaterial == null) _floorMaterial = new Material(baseMat) { color = FloorTint };
            return true;
        }

        private static bool IsWalkable(int[] walkable, int sizeX, int sizeY, int sizeZ, int x, int y, int z)
        {
            if (x < 0 || x >= sizeX || y < 0 || y >= sizeY || z < 0 || z >= sizeZ) return false;
            return walkable[(x * sizeY + y) * sizeZ + z] != 0;
        }

        /// <summary>四邻域里有没有可走格(只判正交 —— 对角相邻不让墙“补角”)。</summary>
        private static bool TouchesOpenCell(int[] walkable, int sizeX, int sizeY, int sizeZ, int x, int z)
            => IsWalkable(walkable, sizeX, sizeY, sizeZ, x - 1, 0, z)
            || IsWalkable(walkable, sizeX, sizeY, sizeZ, x + 1, 0, z)
            || IsWalkable(walkable, sizeX, sizeY, sizeZ, x, 0, z - 1)
            || IsWalkable(walkable, sizeX, sizeY, sizeZ, x, 0, z + 1);

        /// <summary>把一个轴对齐盒并入网格(6 面 × 4 顶点;外向 CCW)。</summary>
        private static void AddBox(List<Vector3> verts, List<Vector3> normals, List<int> tris,
                                   Vector3 center, Vector3 half)
        {
            float hx = half.x, hy = half.y, hz = half.z;
            Vector3 c = center;

            AddFace(verts, normals, tris, c, new Vector3(1, 0, 0),
                    new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz),
                    new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz));
            AddFace(verts, normals, tris, c, new Vector3(-1, 0, 0),
                    new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy, hz),
                    new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz));
            AddFace(verts, normals, tris, c, new Vector3(0, 1, 0),
                    new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz),
                    new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz));
            AddFace(verts, normals, tris, c, new Vector3(0, -1, 0),
                    new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz),
                    new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz));
            AddFace(verts, normals, tris, c, new Vector3(0, 0, 1),
                    new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz),
                    new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz));
            AddFace(verts, normals, tris, c, new Vector3(0, 0, -1),
                    new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz),
                    new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz));
        }

        private static void AddFace(List<Vector3> verts, List<Vector3> normals, List<int> tris,
                                    Vector3 center, Vector3 normal,
                                    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int i0 = verts.Count;
            verts.Add(center + p0); normals.Add(normal);
            verts.Add(center + p1); normals.Add(normal);
            verts.Add(center + p2); normals.Add(normal);
            verts.Add(center + p3); normals.Add(normal);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }
    }
}
