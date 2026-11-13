// 教堂内景的**屋顶木构 + 望板 + 瓦**(2026-11-13)。运行期生成,不预摆(ADR-023 §②)。
//
// 权威:`design/assets/specs/church-interior.md` §8.9(坡屋面 / 望板 / 瓦)
//       概念图 `approved/scene-interior-a.png` —— 顶部是**坡屋顶**:椽子 + 残留的望板,
//       瓦大半已经掉了,天空从破口漏下来。
//
// ═══ 2026-11-13 第二版:为什么从「平铺梁椽」改成坡屋面 ═════════════════════
//   第一版是**水平**的梁 + 椽(§8.1),顶部读作「平屋顶的木格栅」;概念图是**两坡**的。
//   ⇒ 现在:五根横梁(拉杆)保留在 y≈5.3,其上起两坡屋面 ——
//   檐口在墙顶(±5.5 m / y=5.5),屋脊在 x=10 / y=7.3,坡度 atan(1.8/5) ≈ 19.8°。
//   屋面上依次是:斜椽 → 望板 → 筒瓦。
//
// ═══ 三层各是什么 ═══════════════════════════════════════════════════════
//   · **斜椽**:沿坡向,间距 0.7 m,断面 0.08 × 0.14,每坡 20 根。
//   · **望板**:横跨椽子的薄板(沿 z 方向),沿坡每 0.26 m 一条,厚 0.02,**首尾相接**。
//     ⚠️ 2026-11-13 实测:板与板之间留 2 cm 缝 ⇒ 从下面看是**一片筛子**(满顶针孔漏天),
//     读不出「望板」,只有均匀的麻点。⇒ 板必须**对接**(半尺寸取 `(下一行 − 本行) / 2`),
//     破口只由 `Holes` 那四处成片矩形给出,别让「缝」冒充「破」。
//   · **筒瓦**:沿坡向的半圆棱,半径 0.10、间距 0.24 m,坐在望板上 —— 中国传统瓦面
//     的凸棱就是顺着坡走的(不是平行屋脊),所以棱的走向 = 坡向。
//
// ═══ 破损:用「洞」而不是「随机掉块」══════════════════════════════════════
//   随机掉块会得到一片麻点,不像塌。概念图是**几处成片的破口**。
//   ⇒ 手写四个矩形洞(坡向 t × z),洞内的望板与瓦一律不生成,椽子按洞的 z 跨度缺失。
//   全部常量,无随机 —— 视觉是派生态,截图要能逐次比对(ADR-009 §四)。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景屋顶:横梁 + 两坡斜椽 + 望板 + 筒瓦(带成片破口)。</summary>
    public static class InteriorRoofFrame
    {
        /// <summary>`Resources` 下的木纹路径(不含扩展名)。缺席 ⇒ 用素色,不跳过几何。</summary>
        public const string WoodPath = "Art/church-interior/roof_wood";

        /// <summary>木纹贴图覆盖的真实边长(米)。</summary>
        public const float WoodTileMeters = 1f;

        /// <summary>`Resources` 下的瓦贴图路径(不含扩展名)。缺席 ⇒ 用素色瓦。</summary>
        public const string TilePath = "Art/church-interior/roof_tile";

        // ── 室内净空(与 `InteriorWallPanels` 同一组边界)
        private const float X0 = 5f, X1 = 15f, Z0 = 15f, Z1 = 29f;

        // ── 横梁(拉杆):z 位置、断面、梁顶高度
        private static readonly float[] BeamZ = { 17.0f, 19.6f, 22.2f, 24.8f, 27.4f };
        private const float BeamWidth = 0.28f;   // 沿 z
        private const float BeamDepth = 0.34f;   // 沿 y
        private const float BeamTopY = 5.28f;

        // ── 坡屋面:檐口(墙顶)→ 屋脊
        private const float EaveY = 5.50f;
        private const float RidgeX = 10.0f;
        private const float RidgeY = 7.30f;

        // ── 斜椽:间距、断面
        private const float RafterStepZ = 0.70f;
        private const float RafterThick = 0.14f;   // 沿坡面法向
        private const float RafterWidth = 0.08f;   // 沿 z

        // ── 望板:沿坡的条数步长、厚
        private const float DeckStepT = 0.26f;     // 沿坡每 0.26 m 一条(板宽 = 步长,对接无缝)
        private const float DeckThick = 0.020f;

        // ── 筒瓦:半径、沿 z 间距
        private const float TileRadius = 0.10f;
        private const float TileStepZ = 0.24f;
        private const int TileArcSegments = 8;

        /// <summary>一处破口:坡向比例 t∈[0,1](0=檐口,1=屋脊)× z 区间。</summary>
        private struct Hole
        {
            public bool East;
            public float T0, T1, Z0, Z1;
        }

        /// <summary>成片的破口(手写,确定性)。东侧靠塌口那片最大。</summary>
        private static readonly Hole[] Holes =
        {
            // 东坡靠檐口、正对塌口(z 20~23)—— 采光就是从这儿进来的
            new Hole { East = true,  T0 = 0.00f, T1 = 0.40f, Z0 = 19.4f, Z1 = 24.6f },
            // 东坡靠屋脊的一处
            new Hole { East = true,  T0 = 0.62f, T1 = 0.92f, Z0 = 25.4f, Z1 = 27.8f },
            // 西坡一处中等破口
            new Hole { East = false, T0 = 0.45f, T1 = 0.80f, Z0 = 16.0f, Z1 = 18.2f },
            // 西坡靠檐口的一小片
            new Hole { East = false, T0 = 0.00f, T1 = 0.22f, Z0 = 26.2f, Z1 = 28.4f },
        };

        private static bool InHole(bool east, float t, float z)
        {
            for (int i = 0; i < Holes.Length; i++)
            {
                Hole h = Holes[i];
                if (h.East == east && t >= h.T0 && t <= h.T1 && z >= h.Z0 && z <= h.Z1) return true;
            }
            return false;
        }

        /// <summary>生成屋架、望板与瓦,挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>生成的构件数(梁 + 椽 + 望板条 + 瓦棱段)。</returns>
        public static int Build(Transform parent = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.Log("[美术] 屋架跳过:找不到 URP Lit shader");
                return 0;
            }

            var root = new GameObject("InteriorRoofFrame");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();
            int parts = 0;

            // ── 横梁(拉杆)
            float beamHalfW = BeamWidth * 0.5f;
            float beamHalfD = BeamDepth * 0.5f;
            float beamCenterY = BeamTopY - beamHalfD;
            for (int i = 0; i < BeamZ.Length; i++)
            {
                Box(v, t, uv, new Vector3((X0 + X1) * 0.5f, beamCenterY, BeamZ[i]),
                    new Vector3((X1 - X0) * 0.5f, beamHalfD, beamHalfW));
                parts++;
            }

            // ── 两坡:椽子
            parts += BuildSlope(v, t, uv, east: false);
            parts += BuildSlope(v, t, uv, east: true);

            // ── 脊檩:两坡望板在屋脊处对接,这根通长梁既挡接缝,也给屋面一条读得出来的脊
            Box(v, t, uv, new Vector3(RidgeX, RidgeY - 0.17f, (Z0 + Z1) * 0.5f),
                new Vector3(0.09f, 0.09f, (Z1 - Z0) * 0.5f));
            parts++;

            var slabMesh = new Mesh { name = "ChurchRoofFrameMesh" };
            slabMesh.SetVertices(v);
            slabMesh.SetUVs(0, uv);
            slabMesh.SetTriangles(t, 0);
            slabMesh.RecalculateNormals();
            slabMesh.RecalculateBounds();

            var wood = new GameObject("ChurchRoofFrame");
            wood.transform.SetParent(root.transform, false);
            wood.AddComponent<MeshFilter>().sharedMesh = slabMesh;
            wood.AddComponent<MeshRenderer>().sharedMaterial =
                MakeMaterial(shader, "RoofWood_Mat", WoodPath, new Color(0.44f, 0.37f, 0.31f),
                             new Color(0.24f, 0.19f, 0.15f), 0.06f, WoodTileMeters);
            wood.layer = WorldWallGreybox.WorldGeometryLayer;

            // ── 瓦(单独一批,贴图不同)
            var tv = new List<Vector3>();
            var tt = new List<int>();
            var tuv = new List<Vector2>();
            int tiles = BuildTiles(tv, tt, tuv, east: false) + BuildTiles(tv, tt, tuv, east: true);

            int total = parts + tiles;
            if (tv.Count > 0)
            {
                var tileMesh = new Mesh { name = "ChurchRoofTilesMesh" };
                tileMesh.SetVertices(tv);
                tileMesh.SetUVs(0, tuv);
                tileMesh.SetTriangles(tt, 0);
                tileMesh.RecalculateNormals();
                tileMesh.RecalculateBounds();
                var go = new GameObject("ChurchRoofTiles");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = tileMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial =
                    MakeMaterial(shader, "RoofTile_Mat", TilePath, new Color(0.72f, 0.73f, 0.74f),
                                 new Color(0.26f, 0.27f, 0.28f), 0.14f, WoodTileMeters);
                go.layer = WorldWallGreybox.WorldGeometryLayer;
            }

            Debug.Log($"[美术] 屋架已建:横梁 {BeamZ.Length} 根 + 两坡斜椽/望板 {parts - BeamZ.Length} 件" +
                      $" + 筒瓦 {tiles} 段(坡屋面,四处成片破口)");
            return total;
        }

        // ═══ 一坡:斜椽 + 望板 ═══════════════════════════════════════════════

        private static int BuildSlope(List<Vector3> v, List<int> t, List<Vector2> uv, bool east)
        {
            Vector3 eave = new Vector3(east ? X1 : X0, EaveY, 0f);
            Vector3 ridge = new Vector3(RidgeX, RidgeY, 0f);
            Vector3 dir = (ridge - eave).normalized;
            Vector3 n = Vector3.Cross(dir, Vector3.forward);
            if (n.y < 0f) n = -n;
            n.Normalize();
            float slopeLen = (ridge - eave).magnitude;

            // 局部坐标系:x = +z(横跨), y = 法向, z = 坡向
            Quaternion frame = Quaternion.LookRotation(dir, n);

            int count = 0;

            // 斜椽
            for (float z = Z0 + 0.35f; z <= Z1 - 0.34f; z += RafterStepZ)
            {
                if (BrokenRafter(east, z)) continue;
                Vector3 mid = (eave + ridge) * 0.5f + new Vector3(0f, 0f, z);
                BoxOriented(v, t, uv, mid,
                            new Vector3(RafterWidth * 0.5f, RafterThick * 0.5f, slopeLen * 0.5f), frame);
                count++;
            }

            // ── 望板:沿坡一条一条,**首尾相接铺满檐口→屋脊**
            //   按「本行起点 / 下一行起点」算半尺寸,不按固定宽度 —— 这样最后一行会**伸到屋脊**,
            //   不会因为 rows = floor(坡长 / 步长) 在脊下留一条通长的天窗(2026-11-13 实测踩到)。
            int rows = Mathf.CeilToInt(slopeLen / DeckStepT);
            float segLen = (Z1 - Z0) / 3f;
            for (int r = 0; r < rows; r++)
            {
                float from = Mathf.Min(r * DeckStepT, slopeLen);
                float to = Mathf.Min(from + DeckStepT, slopeLen);
                float half = (to - from) * 0.5f;
                if (half < 0.001f) continue;

                float tMid = (from + to) * 0.5f / slopeLen;
                Vector3 mid = Vector3.Lerp(eave, ridge, tMid) + n * (RafterThick * 0.5f + DeckThick * 0.5f);
                // 每行错开贴图采样 ⇒ 相邻两条望板的木纹断开,读得出一条一条的板
                Vector2 shift = new Vector2(0f, r * 0.37f);
                for (int s = 0; s < 3; s++)
                {
                    float zc = Z0 + segLen * (s + 0.5f);
                    if (InHole(east, tMid, zc)) continue;
                    BoxOriented(v, t, uv,
                                new Vector3(mid.x, mid.y, zc),
                                new Vector3(segLen * 0.5f, DeckThick * 0.5f, half),
                                frame, shift);
                    count++;
                }
            }

            return count;
        }

        /// <summary>椽子是否缺:整根落在某个破口的 z 区间里就算断。</summary>
        private static bool BrokenRafter(bool east, float z)
        {
            for (int i = 0; i < Holes.Length; i++)
            {
                Hole h = Holes[i];
                if (!h.East) continue;
                if (h.East == east && z >= h.Z0 && z <= h.Z1 && h.T0 <= 0.02f) return true;
            }
            // 西坡:只在最大破口处缺一根
            if (!east && z > 16.0f && z < 16.8f) return true;
            return false;
        }

        // ═══ 一坡:筒瓦 ═══════════════════════════════════════════════════════

        private static int BuildTiles(List<Vector3> v, List<int> t, List<Vector2> uv, bool east)
        {
            Vector3 eave = new Vector3(east ? X1 : X0, EaveY, 0f);
            Vector3 ridge = new Vector3(RidgeX, RidgeY, 0f);
            Vector3 dir = (ridge - eave).normalized;
            Vector3 n = Vector3.Cross(dir, Vector3.forward);
            if (n.y < 0f) n = -n;
            n.Normalize();
            float slopeLen = (ridge - eave).magnitude;
            Vector3 baseLine = n * (RafterThick * 0.5f + DeckThick + TileRadius * 0.15f);

            int count = 0;
            for (float z = Z0 + 0.12f; z <= Z1 - 0.12f; z += TileStepZ)
            {
                // 一根瓦棱沿坡铺,遇到破口按段裁掉
                int segs = 12;
                float step = slopeLen / segs;
                for (int i = 0; i < segs; i++)
                {
                    float t0 = (i + 0.5f) / segs;
                    if (InHole(east, t0, z)) continue;
                    Vector3 a = eave + baseLine + dir * (i * step) + new Vector3(0f, 0f, z);
                    Vector3 b = a + dir * step;
                    AppendTile(v, t, uv, a, b, TileRadius, n, TileArcSegments);
                    count++;
                }
            }
            return count;
        }

        /// <summary>一段半圆瓦棱:从 a 到 b,弧面朝 +n。</summary>
        private static void AppendTile(List<Vector3> v, List<int> t, List<Vector2> uv,
                                       Vector3 a, Vector3 b, float radius, Vector3 n, int segments)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 right = Vector3.Cross(n, dir).normalized;

            int baseIdx = v.Count;
            for (int k = 0; k <= segments; k++)
            {
                float ang = Mathf.PI * k / segments;
                Vector3 off = (right * Mathf.Cos(ang) + n * Mathf.Sin(ang)) * radius;
                v.Add(a + off);
                v.Add(b + off);
                uv.Add(new Vector2(k / (float)segments, 0f));
                uv.Add(new Vector2(k / (float)segments, 1f));
            }
            for (int k = 0; k < segments; k++)
            {
                int i0 = baseIdx + k * 2;
                int i1 = baseIdx + (k + 1) * 2;
                t.Add(i0); t.Add(i1); t.Add(i1 + 1);
                t.Add(i0); t.Add(i1 + 1); t.Add(i0 + 1);
            }
        }

        // ═══ 材质 ═══════════════════════════════════════════════════════════

        private static Material MakeMaterial(Shader shader, string name, string resourcePath,
                                             Color tintIfPresent, Color tintIfAbsent,
                                             float smoothness, float metersPerTile)
        {
            var mat = new Material(shader) { name = name };
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", tintIfPresent);
            }
            else
            {
                mat.SetColor("_BaseColor", tintIfAbsent);
                Debug.Log($"[美术] 本地缺席:{resourcePath} —— {name} 用素色");
            }
            mat.SetTextureScale("_BaseMap", new Vector2(1f / metersPerTile, 1f / metersPerTile));
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // ═══ 盒体拼装 ═══════════════════════════════════════════════════════

        /// <summary>轴对齐盒体(横梁用)。</summary>
        private static void Box(List<Vector3> v, List<int> t, List<Vector2> uv,
                                Vector3 c, Vector3 h)
        {
            BoxOriented(v, t, uv, c, h, Quaternion.identity);
        }

        /// <summary>带朝向的盒体:<paramref name="h"/> 是**局部**半尺寸(x=右, y=上, z=前)。</summary>
        /// <param name="uvShift">贴图采样偏移(逐条望板错开木纹用,不影响几何)。</param>
        private static void BoxOriented(List<Vector3> v, List<int> t, List<Vector2> uv,
                                        Vector3 c, Vector3 h, Quaternion q, Vector2 uvShift = default)
        {
            void Face(Vector3 center, Vector3 uDir, Vector3 vDir, float hu, float hv)
            {
                int b = v.Count;
                Vector3 du = uDir * hu, dv = vDir * hv;
                Vector3[] p =
                {
                    center - du - dv, center + du - dv, center + du + dv, center - du + dv,
                };
                float s = 1f / WoodTileMeters;
                for (int i = 0; i < 4; i++) v.Add(c + q * (p[i] - c));
                uv.Add(uvShift + new Vector2(0f, 0f));
                uv.Add(uvShift + new Vector2(hu * 2f * s, 0f));
                uv.Add(uvShift + new Vector2(hu * 2f * s, hv * 2f * s));
                uv.Add(uvShift + new Vector2(0f, hv * 2f * s));
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
