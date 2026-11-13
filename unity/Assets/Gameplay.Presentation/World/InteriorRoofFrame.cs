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

        // ── 檐口**外挑**(2026-11-13 绿底诊断实测):原来屋面正好从墙线起坡,
        //    而镜头往上斜看时,光线的仰角(30° + 2.6° 俯仰)比屋面坡度(19.8°)陡 ——
        //    于是**越过墙顶、又从瓦檐上方跑了出去**,画面左上角是天空。
        //    檐口外挑 0.5 m 并压低 0.10 m 之后,越过墙顶的光线一定先撞到瓦面。
        //    顺带也符合真实做法:屋面本来就要挑出墙外做滴水。
        private const float Overhang = 0.50f;
        private const float OverhangDrop = 0.10f;
        private const float RidgeX = 10.0f;
        private const float RidgeY = 7.30f;

        // ── 斜椽:间距、断面
        private const float RafterStepZ = 0.70f;
        private const float RafterThick = 0.14f;   // 沿坡面法向
        private const float RafterWidth = 0.08f;   // 沿 z

        // ── 望板:沿坡的条数步长、厚
        private const float DeckStepT = 0.26f;     // 沿坡每 0.26 m 一条(板宽 = 步长,对接无缝)
        private const float DeckThick = 0.020f;
        private const float DeckSegZ = 0.80f;      // 沿 z 的分段长(塌落断口的横向分辨率)

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
            // 东坡靠屋脊的一处(大片塌落之外的零碎)
            new Hole { East = true,  T0 = 0.62f, T1 = 0.92f, Z0 = 25.4f, Z1 = 28.2f },
            // 🔻 2026-11-13 **删除**:西坡 z 26.2~28.4 / t 0~0.22 的一小片破口。
            //    它离主镜头(拍在 z=25.5)只有一米出头,在画面左侧被透视放大成**一大块白**,
            //    读作"左墙没了"。破口的大小要按**在画面里的占比**判,不是按平米数。
        };

        // ═══ 大片塌落(2026-11-13 第三版)════════════════════════════════════
        //  上一版是**四个矩形洞**,画面读作「在瓦面上开了四个天窗」——洞边是直线,
        //  而且每片洞都完整地悬在屋面里,没有一处连到檐口(真实坍塌一定从檐口开始撕)。
        //  概念图是**整片没了**:东坡从檐口一路撕到坡中,边线是锯齿状的断口。
        //  ⇒ 改成「每一条 z 带一个自己的塌落深度」:深度随带抖动(整数哈希,确定性),
        //    两端逐渐收窄 —— 于是得到一个**连成一片、边缘锯齿**的大口子。
        //  ⚠️ 仍然**没有随机源**:哈希只吃带号,同一份代码每次得到同一条断口边线,
        //    截图能逐次比对(ADR-009 §四)。
        private const float CollapseZ0 = 19.6f, CollapseZ1 = 26.6f, CollapseBandZ = 0.80f;

        /// <summary>该 z 处**从哪一段开始**没有望板(坡向比例);`<= 0` = 这一带没塌。
        /// <para>⚠️ 2026-11-13 第四版:塌落位置由「从檐口起」改为「**从坡中起,直到屋脊**」。
        /// 实测从檐口起缺的一大片在画面里读成**左上角一块白**(天空从檐口边漏进来),
        /// 而概念图的破口在**屋脊一线**、檐口那圈望板还在。物理上也对:檐口有墙压着,
        /// 先断的总是中部与脊部。</para></summary>
        private static float EastCollapseFrom(float z)
        {
            if (z < CollapseZ0 || z > CollapseZ1) return -1f;
            int band = (int)((z - CollapseZ0) / CollapseBandZ);
            float span = (z - CollapseZ0) / (CollapseZ1 - CollapseZ0);
            float taper = Mathf.Min(1f, Mathf.Min(span, 1f - span) * 5f);   // 两端往回收
            return 1f - (0.55f + 0.30f * Hash01(band * 17 + 3)) * taper;    // 0.45..0.15 起塌
        }

        /// <summary>西坡:2026-11-13 第四版**取消**。西坡正对镜头左半,一塌就是一大片白,
        /// 概念图那一侧是**完整的屋面**(光是从东侧与屋顶缺口进来的)。</summary>
        private static float WestCollapseFrom(float z) => -1f;

        /// <summary>整数哈希 → [0,1)。乘性散列,无 `UnityEngine.Random`。</summary>
        private static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static bool InHole(bool east, float t, float z)
        {
            float from = east ? EastCollapseFrom(z) : WestCollapseFrom(z);
            if (from >= 0f && t >= from) return true;      // 坡中→屋脊那一段塌掉

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

            // ── 檐檩(2026-11-13 补「漏天」第二处):墙顶 5.5 / 屋面从 5.5 起坡,
            //    而望板坐在椽子上皮(**比坡线高约 9 cm**)⇒ 沿墙顶有一条 9 cm 的通长缝,
            //    抬头能从缝里看见外面的天。椽子每 0.7 m 才挡一次,挡不住。
            //    这根通长檐檩正好压在缝上,同时是真实屋架里本来就有的构件。
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? X0 : X1;
                // ⚠️ 尺寸与位置是**堵缝**用的(2026-11-13 绿底诊断实测):
                //   望板下皮在坡线以上约 0.09 m,墙顶正好在坡线上 ⇒ 两者之间有一条
                //   通长 9 cm 的**水平缝**,抬头看过去是一条天。檐檩必须把这条缝整个盖住:
                //   往屋里挪 6 cm(x = 墙线 + 0.06)、上下各 0.12 m(y 5.40–5.64)、
                //   沿 x 厚 0.20 —— 小一圈就又从旁边漏。
                Box(v, t, uv, new Vector3(x + (side == 0 ? 0.06f : -0.06f), EaveY + 0.02f, (Z0 + Z1) * 0.5f),
                    new Vector3(0.10f, 0.12f, (Z1 - Z0) * 0.5f));
                parts++;
            }

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
                MakeMaterial(shader, "RoofWood_Mat", WoodPath, new Color(0.42f, 0.37f, 0.31f),
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

            Debug.Log($"[美术] 屋架已建:横梁 {BeamZ.Length} 根 + 檐檩 2 根 + 两坡斜椽/望板 {parts - BeamZ.Length - 2} 件" +
                      $" + 筒瓦 {tiles} 段(坡屋面,东侧成片塌落 + 两处零碎破口)");
            return total;
        }

        // ═══ 一坡:斜椽 + 望板 ═══════════════════════════════════════════════

        private static int BuildSlope(List<Vector3> v, List<int> t, List<Vector2> uv, bool east)
        {
            Vector3 eave = new Vector3(east ? X1 + Overhang : X0 - Overhang, EaveY - OverhangDrop, 0f);
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
            // ⚠️ 沿 z 的分段在 2026-11-13 由「固定 3 段」改为 **0.8 m 一段**:3 段时每段 4.67 m,
            //    锯齿状的塌落边线会被量化成三块大方块 —— 那正是上一版「四个矩形洞」的观感来源。
            //    换成 18 段之后,断口才真的跟着 t 边界走。
            int zSegs = Mathf.CeilToInt((Z1 - Z0) / DeckSegZ);
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
                for (int s = 0; s < zSegs; s++)
                {
                    float za = Z0 + (Z1 - Z0) * s / zSegs;
                    float zb = Z0 + (Z1 - Z0) * (s + 1) / zSegs;
                    float zc = (za + zb) * 0.5f;
                    if (InHole(east, tMid, zc)) continue;
                    BoxOriented(v, t, uv,
                                new Vector3(mid.x, mid.y, zc),
                                new Vector3((zb - za) * 0.5f, DeckThick * 0.5f, half),
                                frame, shift);
                    count++;
                }
            }

            return count;
        }

        /// <summary>椽子是否缺:塌落深度超过四成坡长时,整根椽子一起落掉。</summary>
        private static bool BrokenRafter(bool east, float z)
        {
            float from = east ? EastCollapseFrom(z) : WestCollapseFrom(z);
            return from >= 0f && from < 0.42f;
        }

        // ═══ 一坡:筒瓦 ═══════════════════════════════════════════════════════

        private static int BuildTiles(List<Vector3> v, List<int> t, List<Vector2> uv, bool east)
        {
            Vector3 eave = new Vector3(east ? X1 + Overhang : X0 - Overhang, EaveY - OverhangDrop, 0f);
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
