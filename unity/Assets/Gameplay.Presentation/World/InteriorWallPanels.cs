// 教堂内景的**成片墙面板组**(2026-11-13 起,由 `BackWallPanel` 扩成四面)。
//
// 权威:
//   `design/assets/specs/church-interior.md` §7(投影 → PBR → 合成 → 加脏调色 → 小砖 → 烘焙)
//   ADR-023 §②(运行期生成)· ADR-009 §四(视觉是派生态)
//   `asset_home: local-only`(AI 生成件,条款未核 ⇒ 文件不入库)
//
// ═══ 为什么是「一块贴图铺四面」而不是四面各做一张 ═══════════════════════
//   剥落图案本身来自概念图的后墙(§7.6),只有那一面有依据。其余三面**没有独立依据**,
//   硬造四张不同的剥落图案 = 编造不存在的东西(比「同一张铺四面」更不诚实)。
//   ⇒ 本版共用同一张 albedo,面上按**砖的物理尺度**重算 UV(见 `BrickTileMeters`)。
//   ⚠️ 代价:同一处剥落会在四面各出现一次。这是**已知的临时态**,不是最终形态。
//
// ═══ 摆位取值 ═══════════════════════════════════════════════════════════
//   `world_geometry.json` 实测:教堂 x∈[4,15] / z∈[14,29];墙体各占一格(1 m 厚),
//   内表面 = 内边界:x=5 / x=15 / z=15 / z=29。面板一律**向内缩 2 cm**,
//   压在灰盒墙前面,避免与灰盒面 z-fighting。
//   东墙 z∈[20,23] 是塌口(该段 walkable)⇒ 东墙分成两段,缺口留空。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景的四面墙面板(运行期生成)。</summary>
    public static class InteriorWallPanels
    {
        /// <summary>`Resources` 下的 albedo 路径(不含扩展名)。</summary>
        public const string AlbedoPath = "Art/church-interior/wall_back_albedo";

        /// <summary>贴图覆盖的真实尺寸(米):14 m 宽 × 5.5 m 高(烘焙时定的,见 §7.9)。</summary>
        public const float TextureWidthM = 14f;

        /// <summary>见 <see cref="TextureWidthM"/>。</summary>
        public const float TextureHeightM = 5.5f;

        /// <summary>房间净高(米)。</summary>
        public const float Height = 5.5f;

        /// <summary>面板距墙内表面的内缩(米)。</summary>
        public const float Inset = 0.02f;

        private struct Face
        {
            public string Name;
            public Vector3 A, B;   // 底边两点(世界),A→B 为 U 方向
            public Vector3 Normal;
            public float UFrom, UTo;   // 贴图 U 采样区间
        }

        /// <summary>生成四面墙并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>生成的副数;贴图缺席 ⇒ 0。</returns>
        public static int Build(Transform parent = null)
        {
            Texture2D albedo = Resources.Load<Texture2D>(AlbedoPath);
            if (albedo == null)
            {
                Debug.Log($"[美术] 本地缺席:{AlbedoPath}(内景墙贴图)—— local-only 资产不在本机," +
                          "四壁维持灰盒(不是接进来了)");
                return 0;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.Log("[美术] 内景墙面板跳过:找不到 URP Lit shader");
                return 0;
            }

            var mat = new Material(shader) { name = "InteriorWall_Mat" };
            mat.SetTexture("_BaseMap", albedo);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);

            var root = new GameObject("InteriorWallPanels");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            List<Face> faces = BuildFaces();
            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];
                var go = new GameObject("Wall_" + f.Name);
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad(f);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                go.layer = WorldWallGreybox.WorldGeometryLayer;
            }

            Debug.Log($"[美术] 内景墙面板 {faces.Count} 副已生成(4 面 / 东墙塌口留空)");
            return faces.Count;
        }

        /// <summary>四面墙(东墙拆成塌口两侧的两段)。</summary>
        private static List<Face> BuildFaces()
        {
            const float x0 = 5f, x1 = 15f, z0 = 15f, z1 = 29f;
            // 每米对应的贴图 U 增量(砖的物理尺度不变)
            float uPerM = 1f / TextureWidthM;

            var list = new List<Face>
            {
                // 西墙(后墙):x=5 内表面,朝向 +X,沿 +Z
                new Face { Name = "West", A = new Vector3(x0 + Inset, 0f, z0),
                    B = new Vector3(x0 + Inset, 0f, z1), Normal = Vector3.right,
                    UFrom = 0f, UTo = (z1 - z0) * uPerM },

                // 东墙南段 / 北段:塌口 z∈[20,23] 留空
                new Face { Name = "EastA", A = new Vector3(x1 - Inset, 0f, z1),
                    B = new Vector3(x1 - Inset, 0f, 23f), Normal = Vector3.left,
                    UFrom = 0f, UTo = (z1 - 23f) * uPerM },
                new Face { Name = "EastB", A = new Vector3(x1 - Inset, 0f, 20f),
                    B = new Vector3(x1 - Inset, 0f, z0), Normal = Vector3.left,
                    UFrom = (z1 - z0) * uPerM - (20f - z0) * uPerM, UTo = (z1 - z0) * uPerM },

                // 北墙(z=15,朝 +Z)/ 南墙(z=29,朝 −Z)
                new Face { Name = "North", A = new Vector3(x0, 0f, z0 + Inset),
                    B = new Vector3(x1, 0f, z0 + Inset), Normal = Vector3.forward,
                    UFrom = 0f, UTo = (x1 - x0) * uPerM },
                new Face { Name = "South", A = new Vector3(x1, 0f, z1 - Inset),
                    B = new Vector3(x0, 0f, z1 - Inset), Normal = Vector3.back,
                    UFrom = 0f, UTo = (x1 - x0) * uPerM },
            };
            return list;
        }

        /// <summary>按底边 A→B 与法线生成一张 0..H 的矩形。</summary>
        private static Mesh Quad(Face f)
        {
            Vector3 up = Vector3.up * Height;
            var mesh = new Mesh { name = "Wall_" + f.Name };
            mesh.SetVertices(new[] { f.A, f.B, f.B + up, f.A + up });
            mesh.SetUVs(0, new[]
            {
                new Vector2(f.UFrom, 0f), new Vector2(f.UTo, 0f),
                new Vector2(f.UTo, 1f), new Vector2(f.UFrom, 1f),
            });
            // 绕序:让法线朝 f.Normal(朝屋内)。(0,2,1)/(0,3,2) 对 A→B 方向为逆时针时朝上;
            // 四面朝向不同 ⇒ 用点积自检,反了就翻 —— 不靠肉眼记四面各自的绕序。
            Vector3 n = Vector3.Cross(f.B - f.A, up);
            int[] tris = Vector3.Dot(n, f.Normal) >= 0f
                ? new[] { 0, 1, 2, 0, 2, 3 }
                : new[] { 0, 2, 1, 0, 3, 2 };
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
