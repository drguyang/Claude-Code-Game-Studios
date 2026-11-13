// 教堂内景的**碎石与杂物**(2026-11-13 · §8.8)。运行期生成,不预摆(ADR-023 §②)。
//
// 权威:`design/assets/specs/church-interior.md` §8.8
//       概念图 `approved/scene-interior-a.png` —— 墙脚成堆的碎砖、散落的砖块、断木。
//
// ═══ 为什么是「堆」而不是「几块砖」═══════════════════════════════════════
//   §8.4 那版只有 10 块砖,量级与概念图差一档。真实塌落是**墙脚一条堆**:
//   越靠墙越高、越往外越散,大块与碎屑混在一起。这里按「极点散布 + 山丘高度」生成:
//   r = sqrt(i/N) 保证点在圆内**均匀**,高度随 r 线性衰减 ⇒ 自然的锥形堆。
//
// ═══ 确定性:不用 `UnityEngine.Random` ═════════════════════════════════════
//   视觉是派生态(ADR-009 §四),每块砖的位置必须是**可重放的** —— 否则每次编译出来的
//   废墟都不一样,截图无法逐次比对。⇒ 用自己的整数 hash(`Hash(i, salt)`),
//   同一份代码在同一台机器上永远给出同一堆。它**不参与任何判定**,只决定画面。
//
// ⚠️ **没有碰撞体**(与家具同一条纪律):通行由 `world_geometry` 定(ADR-015 / ADR-022),
//   在这里加 Collider = 给「哪里走得通」造第二真源。⇒ 玩家会穿过砖堆。
//   位置因此全部**贴着墙脚**(x≈5.7 / z≈15.4 / z≈28.4 / 东墙垛),避开从塌口到案头的主路径。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>墙脚砖石堆 + 散落砖块 + 断木板。</summary>
    public static class InteriorRubble
    {
        public const string BrickPath = "Art/church-interior/brick_rubble";
        public const string WoodPath = "Art/church-interior/roof_wood";

        /// <summary>砖贴图覆盖的真实边长(米):与 §7.9 的「2 m 一铺」同尺度。</summary>
        public const float BrickTileMeters = 2f;

        private struct Pile
        {
            public float X, Z;
            public float Radius, Height;
            public int Count;
        }

        /// <summary>墙脚砖堆(位置全部贴墙,避开主路径)。</summary>
        private static readonly Pile[] Piles =
        {
            new Pile { X = 5.78f, Z = 21.3f, Radius = 1.05f, Height = 0.34f, Count = 70 },  // 西墙中段
            new Pile { X = 5.80f, Z = 16.4f, Radius = 0.80f, Height = 0.26f, Count = 40 },  // 西北角
            new Pile { X = 9.60f, Z = 28.4f, Radius = 1.00f, Height = 0.28f, Count = 52 },  // 南墙
            new Pile { X = 14.7f, Z = 19.2f, Radius = 0.85f, Height = 0.30f, Count = 44 },  // 东墙北垛
            new Pile { X = 14.7f, Z = 23.8f, Radius = 0.90f, Height = 0.32f, Count = 48 },  // 东墙南垛
            new Pile { X = 12.6f, Z = 15.5f, Radius = 0.75f, Height = 0.24f, Count = 34 },  // 北墙东段
            // 2026-11-13 补:东墙塌口脚下**最大的一堆**(概念图右侧墙脚那条塌方带)。
            // 原来塌口下面只有两处 0.85-0.90 m 的小堆,断口与地面之间是空的 ——
            // 读作"墙凭空断了",而不是"塌下来的砖在地上"。
            new Pile { X = 13.9f, Z = 21.4f, Radius = 1.65f, Height = 0.62f, Count = 150 },
        };

        /// <summary>断木板(手写:躺在地上,长短与朝向各不相同)。</summary>
        private struct Plank
        {
            public float X, Z;
            public float Length, Yaw;
        }

        private static readonly Plank[] Planks =
        {
            new Plank { X = 6.30f, Z = 19.10f, Length = 1.70f, Yaw = 22f },
            new Plank { X = 10.80f, Z = 27.60f, Length = 1.35f, Yaw = -38f },
            new Plank { X = 13.10f, Z = 25.40f, Length = 1.55f, Yaw = 74f },
            new Plank { X = 7.60f, Z = 15.90f, Length = 1.20f, Yaw = 8f },
        };

        /// <summary>散落砖块的块数。</summary>
        public const int LooseBricks = 20;

        /// <summary>生成砖石与杂物。</summary>
        /// <returns>生成的网格批数(0~2)。</returns>
        public static int Build(Transform parent = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.Log("[美术] 砖石杂物跳过:找不到 URP Lit shader");
                return 0;
            }

            var root = new GameObject("InteriorRubble");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            int created = 0;

            Texture2D brick = Resources.Load<Texture2D>(BrickPath);
            if (brick == null)
            {
                Debug.Log($"[美术] 本地缺席:{BrickPath}(砖石堆)—— 砖石跳过");
            }
            else
            {
                var v = new List<Vector3>();
                var t = new List<int>();
                var uv = new List<Vector2>();
                for (int i = 0; i < Piles.Length; i++) AddPile(v, t, uv, Piles[i]);
                AddLooseBricks(v, t, uv);
                created += Flush(root.transform, "InteriorRubble_BrickMesh", v, t, uv,
                                 MakeMaterial(shader, "Rubble_Brick", brick, 0.08f, 1f / BrickTileMeters));
            }

            Texture2D wood = Resources.Load<Texture2D>(WoodPath);
            if (wood == null)
            {
                Debug.Log($"[美术] 本地缺席:{WoodPath}(断木板)—— 木板跳过");
            }
            else
            {
                var v = new List<Vector3>();
                var t = new List<int>();
                var uv = new List<Vector2>();
                AddPlanks(v, t, uv);
                created += Flush(root.transform, "InteriorRubble_WoodMesh", v, t, uv,
                                 MakeMaterial(shader, "Rubble_Wood", wood, 0.10f, 1f));
            }

            int chunks = 0;
            for (int i = 0; i < Piles.Length; i++) chunks += Piles[i].Count;
            Debug.Log($"[美术] 砖石杂物已建:砖堆 {Piles.Length} 处 / {chunks + LooseBricks} 块 + 断木 {Planks.Length} 条");
            return created;
        }

        // ═══ 生成 ═══════════════════════════════════════════════════════════

        /// <summary>一堆:按极点散布铺满圆面,高度向圆心堆高 ⇒ 锥形。</summary>
        private static void AddPile(List<Vector3> v, List<int> t, List<Vector2> uv, Pile p)
        {
            for (int i = 0; i < p.Count; i++)
            {
                float angle = i * 2.39996323f;                 // 黄金角 ⇒ 点不呈放射状
                float rn = Mathf.Sqrt((i + 0.5f) / p.Count);   // 圆内均匀
                float dist = rn * p.Radius;
                float x = p.X + Mathf.Cos(angle) * dist;
                float z = p.Z + Mathf.Sin(angle) * dist;

                // 🔁 2026-11-13 首版半尺寸 0.09~0.20 ⇒ 砖有 20~40 cm,读作**纸箱**不是碎砖
                //    (实测截图里是一地方块)。青砖实测 24 × 6 × 12 cm ⇒ 半尺寸 0.12 / 0.03 / 0.06。
                float s = 0.55f + 0.85f * Hash(i, 11);
                Vector3 half = new Vector3(
                    0.085f * s + 0.035f * Hash(i, 13),
                    0.030f * s + 0.012f * Hash(i, 17),
                    0.055f * s + 0.025f * Hash(i, 19));

                // 越靠圆心堆得越高;外圈贴地。高度按 (1−rn)² 衰减 ⇒ 顶部收得更快,像堆不像台。
                float mound = p.Height * (1f - rn) * (1f - rn) * (0.55f + 0.45f * Hash(i, 7));
                float y = Mathf.Min(p.Height, Mathf.Max(half.y, mound + half.y));

                Quaternion q = Quaternion.Euler(
                    (Hash(i, 29) - 0.5f) * 22f,
                    Hash(i, 23) * 360f,
                    (Hash(i, 31) - 0.5f) * 22f);
                AppendBox(v, t, uv, new Vector3(x, y, z), half, q);
            }
        }

        /// <summary>散落在屋内地面上的单块砖(不堆,平躺或微翘)。</summary>
        private static void AddLooseBricks(List<Vector3> v, List<int> t, List<Vector2> uv)
        {
            const float x0 = 5.7f, x1 = 14.3f, z0 = 15.7f, z1 = 28.3f;
            for (int i = 0; i < LooseBricks; i++)
            {
                float x = Mathf.Lerp(x0, x1, Hash(i, 101));
                float z = Mathf.Lerp(z0, z1, Hash(i, 103));
                float s = 0.6f + 0.7f * Hash(i, 107);
                Vector3 half = new Vector3(0.095f * s, 0.032f * s,
                                           0.06f * s * (0.7f + 0.6f * Hash(i, 109)));
                Quaternion q = Quaternion.Euler(
                    (Hash(i, 113) - 0.5f) * 30f,
                    Hash(i, 127) * 360f,
                    (Hash(i, 131) - 0.5f) * 30f);
                AppendBox(v, t, uv, new Vector3(x, half.y, z), half, q);
            }
        }

        /// <summary>断木板:一块躺着的板 + 一端略翘,再带一小截断头。</summary>
        private static void AddPlanks(List<Vector3> v, List<int> t, List<Vector2> uv)
        {
            for (int i = 0; i < Planks.Length; i++)
            {
                Plank p = Planks[i];
                Quaternion yaw = Quaternion.Euler(0f, p.Yaw, 0f);
                Vector3 dir = yaw * Vector3.right;
                Vector3 mid = new Vector3(p.X, 0.035f, p.Z);
                // 主板:长 × 0.06 厚 × 0.21 宽
                AppendBox(v, t, uv, mid, new Vector3(p.Length * 0.5f, 0.03f, 0.105f),
                          Quaternion.Euler(0f, p.Yaw, 3f));
                // 断头:从板的一端再斜伸一小截,证明它不是完整的板
                Vector3 tip = mid + dir * (p.Length * 0.5f + 0.16f);
                AppendBox(v, t, uv, tip + new Vector3(0f, 0.012f, 0f),
                          new Vector3(0.16f, 0.022f, 0.085f),
                          Quaternion.Euler(0f, p.Yaw + 24f, 9f));
            }
        }

        // ═══ 落成网格 ═══════════════════════════════════════════════════════

        private static int Flush(Transform parent, string name, List<Vector3> v, List<int> t,
                                 List<Vector2> uv, Material mat)
        {
            if (v.Count == 0) return 0;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = WorldWallGreybox.WorldGeometryLayer;
            return 1;
        }

        private static Material MakeMaterial(Shader shader, string name, Texture2D tex,
                                             float smoothness, float metersPerTile)
        {
            var mat = new Material(shader) { name = name };
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(1f / metersPerTile, 1f / metersPerTile));
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        /// <summary>一块歪放的碎砖(盒体)。UV 取**米数** —— 与墙面砖的物理尺度一致。</summary>
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
                float s = 1f / BrickTileMeters;
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

        /// <summary>确定性整数 hash → [0,1)。**只决定画面**,不参与任何判定。</summary>
        private static float Hash(int i, int salt)
        {
            uint x = (uint)(i * 73856093) ^ (uint)(salt * 19349663);
            x ^= x >> 13;
            x *= 0x5bd1e995u;
            x ^= x >> 15;
            return (x & 0xFFFFFFu) / 16777216f;
        }
    }
}
