// 教堂内景的**成片墙面板组**(2026-11-13 起,由 `BackWallPanel` 扩成四面)。
//
// 权威:
//   `design/assets/specs/church-interior.md` §7(投影 → PBR → 合成 → 加脏调色 → 小砖 → 烘焙)
//                                          §8.4(东墙塌口的形态)
//                                          §8.5(每面一张 albedo)
//   ADR-023 §②(运行期生成)· ADR-009 §四(视觉是派生态)
//   `asset_home: local-only`(AI 生成件,条款未核 ⇒ 文件不入库)
//
// ═══ 每面一张 albedo(§8.5)════════════════════════════════════════════════
//   首版四面共用一张 ⇒ **同一处剥落在四面各出现一次**,一眼就看出是同一张图。
//   现在五张(西 / 北 / 南 / 东-南段 / 东-北段),由 Blender 按面各烘一张:
//   各自一张 mask 窗口(28 m 镜像拼幅上截取)+ 各自的脏污偏移 ⇒ 形状与脏度都不一样。
//   ⚠️ 窗口是从**同一张概念图 mask** 截的,所以五面仍共享同一套「剥落语言」——
//   这是有意的:多出来的「不同」若凭空生成,就是编造不存在的依据。
//
// ═══ 东墙塌口:为什么不是一块方洞(2026-11-13 · §8.4)════════════════════
//   概念图里东墙是**整面塌掉**的。数据只给了 3 m 宽的地面开口(z∈[20,23] 可走)
//   ⇒ **地面开口不能画大**,画大了玩家会去走一处走不通的地方(通行由 `world_geometry` 定)。
//   ⇒ 本版改的是**开口上方**:两段墙的顶边按手写裂口轮廓逐段下落,到塌口边缘降到 2.6 m
//   (灰盒墙高),于是「门上那一大片墙塌了、露出天」,而地面通行仍与数据一致。
//
// ═══ 摆位取值 ═══════════════════════════════════════════════════════════
//   `world_geometry.json` 实测:教堂 x∈[4,15] / z∈[14,29];墙体各占一格(1 m 厚),
//   内表面 = 内边界:x=5 / x=15 / z=15 / z=29。面板一律**向内缩 2 cm**,
//   压在灰盒墙前面,避免与灰盒面 z-fighting。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景的四面墙面板(运行期生成,每面一张贴图)。</summary>
    public static class InteriorWallPanels
    {
        /// <summary>回落贴图(五张面贴图缺席时用)。</summary>
        public const string AlbedoPath = "Art/church-interior/wall_back_albedo";

        /// <summary>塌落砖石用的贴图路径(不含扩展名)。</summary>
        public const string RubblePath = "Art/church-interior/brick_rubble";

        /// <summary>回落贴图覆盖的真实尺寸(米):14 m 宽 × 5.5 m 高(§7.9)。</summary>
        public const float TextureWidthM = 14f;

        /// <summary>见 <see cref="TextureWidthM"/>。</summary>
        public const float TextureHeightM = 5.5f;

        /// <summary>房间净高(米)。</summary>
        public const float Height = 5.5f;

        /// <summary>面板距墙内表面的内缩(米)。</summary>
        public const float Inset = 0.02f;

        private const float X0 = 5f, X1 = 15f, Z0 = 15f, Z1 = 29f;

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

        /// <summary>一面墙的取样口径。</summary>
        private struct Face
        {
            public string Name;
            public string TexturePath;
            public float WidthM;        // 沿墙长度(米)
            public bool UAlongX;        // true = U 沿 X(南北墙);false = 沿 Z(东西墙)
            public float UOrigin;       // 平米数换算的起点
            public float UScale;        // 每米 U 增量(可为负 —— 南墙与东墙方向相反)
            public List<Vector3[]> Quads;   // 每块四边形:左下 / 右下 / 右上 / 左上
            public Vector3 Normal;
        }

        /// <summary>生成四面墙并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>生成的网格数(墙 5 + 塌落砖石 0/1)。</returns>
        public static int Build(Transform parent = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.Log("[美术] 内景墙面板跳过:找不到 URP Lit shader");
                return 0;
            }

            Texture2D fallback = Resources.Load<Texture2D>(AlbedoPath);

            var root = new GameObject("InteriorWallPanels");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            int created = 0;
            created += BuildFace(root.transform, shader, fallback, WestFace());
            created += BuildFace(root.transform, shader, fallback, NorthFace());
            created += BuildFace(root.transform, shader, fallback, SouthFace());
            created += BuildFace(root.transform, shader, fallback, EastFace("EastSouth",
                "Art/church-interior/wall_easts", 6f, 29f, -1f / 6f, EastSouthProfile));
            created += BuildFace(root.transform, shader, fallback, EastFace("EastNorth",
                "Art/church-interior/wall_eastn", 5f, 15f, 1f / 5f, EastNorthProfile));

            if (created == 0)
            {
                Debug.Log($"[美术] 本地缺席:{AlbedoPath}(内景墙贴图)—— local-only 资产不在本机," +
                          "四壁维持灰盒(不是接进来了)");
                return 0;
            }

            created += BuildRubble(root.transform, shader);
            Debug.Log($"[美术] 内景墙 {created} 面已生成(每面一张 albedo;东墙塌口上方的墙按裂口轮廓下落)");
            return created;
        }

        // ── 三个整面(0..H)──────────────────────────────────────────────────

        private static Face WestFace() => new Face
        {
            Name = "West",
            TexturePath = "Art/church-interior/wall_west",
            WidthM = Z1 - Z0,
            UAlongX = false,
            UOrigin = Z0,
            UScale = 1f / (Z1 - Z0),
            Normal = Vector3.right,
            Quads = new List<Vector3[]>
            {
                new[]
                {
                    new Vector3(X0 + Inset, 0f, Z0), new Vector3(X0 + Inset, 0f, Z1),
                    new Vector3(X0 + Inset, Height, Z1), new Vector3(X0 + Inset, Height, Z0),
                },
            },
        };

        private static Face NorthFace() => new Face
        {
            Name = "North",
            TexturePath = "Art/church-interior/wall_north",
            WidthM = X1 - X0,
            UAlongX = true,
            UOrigin = X0,
            UScale = 1f / (X1 - X0),
            Normal = Vector3.forward,
            Quads = new List<Vector3[]>
            {
                new[]
                {
                    new Vector3(X0, 0f, Z0 + Inset), new Vector3(X1, 0f, Z0 + Inset),
                    new Vector3(X1, Height, Z0 + Inset), new Vector3(X0, Height, Z0 + Inset),
                },
            },
        };

        private static Face SouthFace() => new Face
        {
            Name = "South",
            TexturePath = "Art/church-interior/wall_south",
            WidthM = X1 - X0,
            UAlongX = true,
            UOrigin = X1,
            UScale = -1f / (X1 - X0),
            Normal = Vector3.back,
            Quads = new List<Vector3[]>
            {
                new[]
                {
                    new Vector3(X1, 0f, Z1 - Inset), new Vector3(X0, 0f, Z1 - Inset),
                    new Vector3(X0, Height, Z1 - Inset), new Vector3(X1, Height, Z1 - Inset),
                },
            },
        };

        /// <summary>东墙的一段:底边沿 z 走,顶边按 <paramref name="profile"/> 逐段下落。</summary>
        private static Face EastFace(string name, string texturePath, float widthM,
                                     float uOrigin, float uScale, Vector2[] profile)
        {
            var quads = new List<Vector3[]>();
            const float x = X1 - Inset;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                quads.Add(new[]
                {
                    new Vector3(x, 0f, a.x), new Vector3(x, 0f, b.x),
                    new Vector3(x, b.y, b.x), new Vector3(x, a.y, a.x),
                });
            }
            return new Face
            {
                Name = name,
                TexturePath = texturePath,
                WidthM = widthM,
                UAlongX = false,
                UOrigin = uOrigin,
                UScale = uScale,
                Normal = Vector3.left,
                Quads = quads,
            };
        }

        /// <summary>一批网格 + 一张材质。面贴图缺席时落到 <paramref name="fallback"/> 并按米取样。</summary>
        private static int BuildFace(Transform parent, Shader shader, Texture2D fallback, Face f)
        {
            Texture2D tex = Resources.Load<Texture2D>(f.TexturePath);
            bool usingFallback = tex == null;
            if (usingFallback) tex = fallback;
            if (tex == null) return 0;

            var mat = new Material(shader) { name = "InteriorWall_" + f.Name };
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);

            float uPerM = usingFallback ? 1f / TextureWidthM : 0f;
            float vPerM = usingFallback ? 1f / TextureHeightM : 1f / Height;

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();
            for (int q = 0; q < f.Quads.Count; q++)
            {
                Vector3[] p = f.Quads[q];
                int b = v.Count;
                for (int i = 0; i < 4; i++) v.Add(p[i]);
                for (int i = 0; i < 4; i++)
                {
                    float along = f.UAlongX ? p[i].x : p[i].z;
                    float u = usingFallback ? along * uPerM : (along - f.UOrigin) * f.UScale;
                    uv.Add(new Vector2(u, p[i].y * vPerM));
                }

                Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                if (Vector3.Dot(n, f.Normal) >= 0f)
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

            var mesh = new Mesh { name = "Wall_" + f.Name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Wall_" + f.Name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = WorldWallGreybox.WorldGeometryLayer;

            if (usingFallback)
                Debug.Log($"[美术] 本地缺席:{f.TexturePath} —— {f.Name} 面回落用的整墙贴图({AlbedoPath})");
            return 1;
        }

        // ═══ 塌落的砖石(§8.4)══════════════════════════════════════════════
        //   两堆:南北两个墙垛的墙脚。贴图用 §7.9 自造的小砖,不用墙面 albedo
        //   (那张带白灰剥落,拿来做碎块会像「一块块墙皮」)。

        private struct Rock
        {
            public Vector3 Center, Half;
            public float Yaw;
        }

        private static readonly Rock[] Rocks =
        {
            new Rock { Center = new Vector3(14.72f, 0.16f, 23.36f), Half = new Vector3(0.30f, 0.16f, 0.26f), Yaw = 18f },
            new Rock { Center = new Vector3(15.06f, 0.10f, 23.64f), Half = new Vector3(0.22f, 0.10f, 0.18f), Yaw = -27f },
            new Rock { Center = new Vector3(14.60f, 0.09f, 23.04f), Half = new Vector3(0.20f, 0.09f, 0.16f), Yaw = 42f },
            new Rock { Center = new Vector3(15.32f, 0.13f, 23.20f), Half = new Vector3(0.24f, 0.13f, 0.22f), Yaw = -12f },
            new Rock { Center = new Vector3(14.94f, 0.07f, 22.84f), Half = new Vector3(0.16f, 0.07f, 0.14f), Yaw = 63f },
            new Rock { Center = new Vector3(14.78f, 0.15f, 19.74f), Half = new Vector3(0.28f, 0.15f, 0.24f), Yaw = -22f },
            new Rock { Center = new Vector3(15.10f, 0.09f, 19.44f), Half = new Vector3(0.20f, 0.09f, 0.17f), Yaw = 33f },
            new Rock { Center = new Vector3(14.58f, 0.08f, 20.04f), Half = new Vector3(0.18f, 0.08f, 0.15f), Yaw = 11f },
            new Rock { Center = new Vector3(15.26f, 0.12f, 19.90f), Half = new Vector3(0.22f, 0.12f, 0.20f), Yaw = 50f },
            new Rock { Center = new Vector3(14.88f, 0.06f, 20.26f), Half = new Vector3(0.15f, 0.06f, 0.13f), Yaw = -41f },
        };

        /// <summary>塌落砖石:一批网格。<returns>1 = 已生成;0 = 无贴图(跳过,不影响墙面)。</returns></summary>
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
                AppendBox(v, t, uv, r.Center, r.Half, Quaternion.Euler(0f, r.Yaw, 0f));
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

        /// <summary>一块歪放的碎砖(盒体)。UV 按 2 m 一铺,与墙面砖的物理尺度一致。</summary>
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
                const float s = 1f / 2f;
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
