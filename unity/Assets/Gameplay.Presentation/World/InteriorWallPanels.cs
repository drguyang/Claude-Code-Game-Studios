// 教堂内景的**成片墙面板组**(2026-11-13 起,由 `BackWallPanel` 扩成四面)。
//
// 权威:
//   `design/assets/specs/church-interior.md` §7(投影 → PBR → 合成 → 加脏调色 → 小砖 → 烘焙)
//                                          §8.4(东墙塌口的形态)
//   ADR-023 §②(运行期生成)· ADR-009 §四(视觉是派生态)
//   `asset_home: local-only`(AI 生成件,条款未核 ⇒ 文件不入库)
//
// ═══ 为什么是「一块贴图铺四面」而不是四面各做一张 ═══════════════════════
//   剥落图案本身来自概念图的后墙(§7.6),只有那一面有依据。其余三面**没有独立依据**,
//   硬造四张不同的剥落图案 = 编造不存在的东西(比「同一张铺四面」更不诚实)。
//   ⇒ 本版共用同一张 albedo,面上按**砖的物理尺度**重算 UV(见 `TextureWidthM`)。
//   ⚠️ 代价:同一处剥落会在四面各出现一次。这是**已知的临时态**,§8.5 解。
//
// ═══ 东墙塌口:为什么不是一块方洞(2026-11-13 · §8.4)════════════════════
//   概念图里东墙是**整面塌掉**的,能看见外面的地平线。数据只给了 3 m 宽的地面开口
//   (`walkable` 里 z∈[20,23] 那段可走)⇒ **地面开口不能画大**,画大了玩家会去走一处
//   走不通的地方(通行由 `world_geometry` 定,不由这里定)。
//   ⇒ 本版改的是**开口上方**:两段墙的顶边按手写裂口轮廓逐段下落,到塌口边缘降到 2.6 m
//   (灰盒墙的高度),于是「门上那一大片墙塌了、露出天」,而地面通行仍与数据一致。
//
// ═══ 摆位取值 ═══════════════════════════════════════════════════════════
//   `world_geometry.json` 实测:教堂 x∈[4,15] / z∈[14,29];墙体各占一格(1 m 厚),
//   内表面 = 内边界:x=5 / x=15 / z=15 / z=29。面板一律**向内缩 2 cm**,
//   压在灰盒墙前面,避免与灰盒面 z-fighting。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景的四面墙面板(运行期生成)。</summary>
    public static class InteriorWallPanels
    {
        /// <summary>`Resources` 下的 albedo 路径(不含扩展名)。</summary>
        public const string AlbedoPath = "Art/church-interior/wall_back_albedo";

        /// <summary>塌落砖石用的贴图路径(不含扩展名)。缺席 ⇒ 落到 albedo。</summary>
        public const string RubblePath = "Art/church-interior/brick_rubble";

        /// <summary>贴图覆盖的真实尺寸(米):14 m 宽 × 5.5 m 高(烘焙时定的,见 §7.9)。</summary>
        public const float TextureWidthM = 14f;

        /// <summary>见 <see cref="TextureWidthM"/>。</summary>
        public const float TextureHeightM = 5.5f;

        /// <summary>房间净高(米)。</summary>
        public const float Height = 5.5f;

        /// <summary>面板距墙内表面的内缩(米)。</summary>
        public const float Inset = 0.02f;

        // ── 东墙裂口轮廓:(z, 该处的墙顶高)。手写,不用随机数(截图要能逐次比对)。
        //    两段都在**塌口一侧**降到 2.6 m(灰盒墙高),越往房间深处越高。
        //    🔁 2026-11-13 实测修正:首版起伏 ±1.2 m ⇒ 剪影是**一排锯齿尖**,读作装饰女墙,
        //    不像塌了口子。现改为**单调下落 + 0.1~0.2 m 的小起伏**,缺口处才收口。
        private static readonly Vector2[] EastSouthProfile =
        {
            new Vector2(29.0f, 5.50f), new Vector2(28.4f, 5.46f), new Vector2(27.8f, 5.10f),
            new Vector2(27.2f, 4.96f), new Vector2(26.6f, 4.62f), new Vector2(26.0f, 4.70f),
            new Vector2(25.4f, 4.18f), new Vector2(24.8f, 4.05f), new Vector2(24.2f, 3.62f),
            new Vector2(23.6f, 3.44f), new Vector2(23.0f, 2.62f),
        };

        private static readonly Vector2[] EastNorthProfile =
        {
            new Vector2(20.0f, 2.60f), new Vector2(19.6f, 3.40f), new Vector2(19.2f, 3.92f),
            new Vector2(18.6f, 4.10f), new Vector2(18.0f, 4.66f), new Vector2(17.4f, 4.55f),
            new Vector2(16.8f, 4.92f), new Vector2(16.2f, 5.06f), new Vector2(15.6f, 5.44f),
            new Vector2(15.0f, 5.50f),
        };

        private struct Rect
        {
            public Vector3 A, B;   // 底边两点(世界),A→B 为 U 方向
            public Vector3 Normal;
        }

        /// <summary>生成四面墙并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>生成的网格数(墙 1 + 塌落砖石 0/1);贴图缺席 ⇒ 0。</returns>
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

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();

            const float x0 = 5f, x1 = 15f, z0 = 15f, z1 = 29f;
            AppendRect(v, t, uv, new Rect
            {
                A = new Vector3(x0 + Inset, 0f, z0), B = new Vector3(x0 + Inset, 0f, z1),
                Normal = Vector3.right,
            }, uAlongX: false);
            AppendRect(v, t, uv, new Rect
            {
                A = new Vector3(x0, 0f, z0 + Inset), B = new Vector3(x1, 0f, z0 + Inset),
                Normal = Vector3.forward,
            }, uAlongX: true);
            AppendRect(v, t, uv, new Rect
            {
                A = new Vector3(x1, 0f, z1 - Inset), B = new Vector3(x0, 0f, z1 - Inset),
                Normal = Vector3.back,
            }, uAlongX: true);
            AppendJaggedEast(v, t, uv, EastSouthProfile);
            AppendJaggedEast(v, t, uv, EastNorthProfile);

            var mesh = new Mesh { name = "InteriorWallMesh" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Wall_FourSides");
            go.transform.SetParent(root.transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = WorldWallGreybox.WorldGeometryLayer;

            int created = 1 + BuildRubble(root.transform, shader);
            Debug.Log($"[美术] 内景墙 4 面已生成(东墙塌口上方的墙按裂口轮廓下落;塌落砖石 {created - 1} 批)");
            return created;
        }

        /// <summary>一整块矩形墙(0..H)。</summary>
        private static void AppendRect(List<Vector3> v, List<int> t, List<Vector2> uv, Rect r,
                                       bool uAlongX)
        {
            Vector3 up = Vector3.up * Height;
            AppendQuad(v, t, uv, new[] { r.A, r.B, r.B + up, r.A + up }, r.Normal, uAlongX);
        }

        /// <summary>东墙的一段:底边沿 z 走,顶边按 <paramref name="profile"/> 逐段下落。</summary>
        private static void AppendJaggedEast(List<Vector3> v, List<int> t, List<Vector2> uv,
                                             Vector2[] profile)
        {
            const float x = 15f - Inset;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                Vector3 bottomA = new Vector3(x, 0f, a.x);
                Vector3 bottomB = new Vector3(x, 0f, b.x);
                Vector3 topA = new Vector3(x, a.y, a.x);
                Vector3 topB = new Vector3(x, b.y, b.x);
                AppendQuad(v, t, uv, new[] { bottomA, bottomB, topB, topA }, Vector3.left, uAlongX: false);
            }
        }

        /// <summary>四个顶点 + 各自的 UV(按世界米数 / 贴图覆盖米数)。绕序按法线自检。</summary>
        private static void AppendQuad(List<Vector3> v, List<int> t, List<Vector2> uv,
                                       Vector3[] p, Vector3 normal, bool uAlongX)
        {
            int b = v.Count;
            float uPerM = 1f / TextureWidthM;
            float vPerM = 1f / TextureHeightM;
            for (int i = 0; i < 4; i++) v.Add(p[i]);
            // ⚠️ U 轴必须按墙的走向选:南北墙沿 X 铺,东西墙沿 Z 铺。
            // 一律用 z 会把南北墙的整面贴图压成一列(2026-11-13 起草时踩到过)。
            for (int i = 0; i < 4; i++)
                uv.Add(new Vector2((uAlongX ? p[i].x : p[i].z) * uPerM, p[i].y * vPerM));

            Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
            if (Vector3.Dot(n, normal) >= 0f)
            {
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
            else
            {
                t.Add(b); t.Add(b + 2); t.Add(b + 1);
                t.Add(b); t.Add(b + 3); t.Add(b + 2);
            }
        }

        // ═══ 塌落的砖石(§8.4)══════════════════════════════════════════════
        //   两堆:南北两个墙垛的墙脚。地块沿用 §7.9 自造的小砖贴图,不用墙面 albedo
        //   (那张带白灰剥落,拿来做碎块会像「一块块墙皮」)。

        private struct Rock
        {
            public Vector3 Center, Half;
            public float Yaw;
        }

        private static readonly Rock[] Rocks =
        {
            // 南垛(塌口 z=23 一侧)
            new Rock { Center = new Vector3(14.72f, 0.16f, 23.36f), Half = new Vector3(0.30f, 0.16f, 0.26f), Yaw = 18f },
            new Rock { Center = new Vector3(15.06f, 0.10f, 23.64f), Half = new Vector3(0.22f, 0.10f, 0.18f), Yaw = -27f },
            new Rock { Center = new Vector3(14.60f, 0.09f, 23.04f), Half = new Vector3(0.20f, 0.09f, 0.16f), Yaw = 42f },
            new Rock { Center = new Vector3(15.32f, 0.13f, 23.20f), Half = new Vector3(0.24f, 0.13f, 0.22f), Yaw = -12f },
            new Rock { Center = new Vector3(14.94f, 0.07f, 22.84f), Half = new Vector3(0.16f, 0.07f, 0.14f), Yaw = 63f },
            // 北垛(塌口 z=20 一侧)
            new Rock { Center = new Vector3(14.78f, 0.15f, 19.74f), Half = new Vector3(0.28f, 0.15f, 0.24f), Yaw = -22f },
            new Rock { Center = new Vector3(15.10f, 0.09f, 19.44f), Half = new Vector3(0.20f, 0.09f, 0.17f), Yaw = 33f },
            new Rock { Center = new Vector3(14.58f, 0.08f, 20.04f), Half = new Vector3(0.18f, 0.08f, 0.15f), Yaw = 11f },
            new Rock { Center = new Vector3(15.26f, 0.12f, 19.90f), Half = new Vector3(0.22f, 0.12f, 0.20f), Yaw = 50f },
            new Rock { Center = new Vector3(14.88f, 0.06f, 20.26f), Half = new Vector3(0.15f, 0.06f, 0.13f), Yaw = -41f },
        };

        /// <summary>塌落砖石:一批网格。<returns>1 = 已生成;0 = 无贴图可用。</returns></summary>
        private static int BuildRubble(Transform parent, Shader shader)
        {
            Texture2D brick = Resources.Load<Texture2D>(RubblePath);
            if (brick == null)
            {
                Debug.Log($"[美术] 本地缺席:{RubblePath}(塌落砖石)—— 砖石堆跳过,墙面照常");
                return 0;
            }

            var mat = new Material(shader) { name = "InteriorRubble_Mat" };
            mat.SetTexture("_BaseMap", brick);
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Metallic", 0f);

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();
            for (int i = 0; i < Rocks.Length; i++)
            {
                Rock r = Rocks[i];
                Quaternion q = Quaternion.Euler(0f, r.Yaw, 0f);
                AppendBox(v, t, uv, r.Center, r.Half, q);
            }

            var mesh = new Mesh { name = "InteriorRubbleMesh" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("InteriorRubble");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = WorldWallGreybox.WorldGeometryLayer;
            return 1;
        }

        /// <summary>一块歪放的碎砖(盒体)。UV 取**世界米数**,砖缝尺度与墙面一致。</summary>
        private static void AppendBox(List<Vector3> v, List<int> t, List<Vector2> uv,
                                      Vector3 c, Vector3 h, Quaternion q)
        {
            void Face(Vector3 center, Vector3 uDir, Vector3 vDir, float hu, float hv)
            {
                int b = v.Count;
                Vector3 du = uDir * hu, dv = vDir * hv;
                Vector3[] p =
                {
                    center - du - dv, center + du - dv, center + du + dv, center - du + dv,
                };
                float s = 1f / 2f;   // 与 §7.9 的「2 m 一铺」同尺度
                for (int i = 0; i < 4; i++) v.Add(q * (p[i] - c) + c);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(hu * 2f * s, 0f));
                uv.Add(new Vector2(hu * 2f * s, hv * 2f * s));
                uv.Add(new Vector2(0f, hv * 2f * s));
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }

            Face(c + new Vector3(h.x, 0, 0), Vector3.forward, Vector3.up, h.z, h.y);
            Face(c + new Vector3(-h.x, 0, 0), Vector3.back, Vector3.up, h.z, h.y);
            Face(c + new Vector3(0, h.y, 0), Vector3.right, Vector3.forward, h.x, h.z);
            Face(c + new Vector3(0, -h.y, 0), Vector3.right, Vector3.back, h.x, h.z);
            Face(c + new Vector3(0, 0, h.z), Vector3.right, Vector3.up, h.x, h.y);
            Face(c + new Vector3(0, 0, -h.z), Vector3.back, Vector3.up, h.x, h.y);
        }
    }
}
