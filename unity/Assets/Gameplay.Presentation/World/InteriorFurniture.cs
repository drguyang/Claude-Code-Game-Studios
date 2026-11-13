// 教堂内景的**家具与器物**(2026-11-13)。运行期生成,不预摆(ADR-023 §②)。
//
// 权威:`design/assets/specs/church-interior.md` §8.3
//       概念图 `approved/scene-interior-a.png` —— 案头 + 摊开的登记簿、陶罐、草席、翻倒的长椅。
//
// ═══ 案头的位置从哪来 ═════════════════════════════════════════════════════
//   **由调用方传进来**(`CompositionRoot` 传 `PrologueAnchors.Desk` 的格)。
//   本类**不得**自己写一份坐标:节拍判定与画面必须是同一处,写第二份就会分叉。
//   ⚠️ 也不能在这里 `using ...Gameplay.Boot`:程序集方向是 Boot → Presentation,
//   反向引用会成环(ADR-025 §① 的引用集)。
//
// ═══ 没有碰撞体(有意为之)═════════════════════════════════════════════════
//   通行由 `world_geometry` 的导航格决定(ADR-015 / ADR-022 关卡工具唯一作者)。
//   在这里加 Collider = 给「哪里走得通」造第二真源,而且不出现在任何几何产物里。
//   ⇒ 家具只是画面:玩家会**穿过**长椅与草席。已知代价,不当成疏漏。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景的案头 / 登记簿 / 陶罐 / 草席 / 翻倒的长椅。</summary>
    public static class InteriorFurniture
    {
        public const string WoodPath = "Art/church-interior/roof_wood";
        public const string StrawPath = "Art/church-interior/straw_mat";
        public const string ClayPath = "Art/church-interior/coarse_clay";
        /// <summary>告示纸(2026-11-13 补):概念图左墙那张「告示」。</summary>
        public const string NoticePath = "Art/church-interior/notice_paper";

        /// <summary>桌面高度(米)。与灰盒锚点 `Anchor_Desk` 的 0.78 同高 —— 真件要盖住它。</summary>
        public const float TableTopY = 0.78f;

        /// <summary>本类是否替换了某个灰盒锚点(替换 ⇒ 该锚点不再铺灰盒方块)。</summary>
        public static bool Covers(string markerName) => markerName == "Anchor_Desk";

        /// <summary>生成家具并挂到场景里。</summary>
        /// <param name="deskCellX">案头所在格 x(单一出处 = `PrologueAnchors.Desk`)。</param>
        /// <param name="deskCellZ">案头所在格 z。</param>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>生成的网格批数(4 = 木 / 纸 / 陶 / 草席)。</returns>
        public static int Build(float deskCellX, float deskCellZ, Transform parent = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.Log("[美术] 内景家具跳过:找不到 URP Lit shader");
                return 0;
            }

            Material wood = MakeMaterial(shader, "Furniture_Wood", WoodPath,
                                         new Color(0.78f, 0.74f, 0.68f), new Color(0.26f, 0.21f, 0.17f), 0.08f);
            Material paper = MakeFlat(shader, "Furniture_Paper", new Color(0.72f, 0.68f, 0.57f), 0.03f);
            Material clay = MakeMaterial(shader, "Furniture_Clay", ClayPath,
                                         new Color(0.70f, 0.67f, 0.63f), new Color(0.27f, 0.23f, 0.20f), 0.10f);
            // ⚠️ 2026-11-13:p87 实测草席是一块**过亮的金色板**(贴图 p50 149、暖度 +54,
            //    再乘 0.90 的白 tint ⇒ 在实景布光下直接过曝)。压到 0.55 并去掉暖偏。
            Material straw = MakeMaterial(shader, "Furniture_Straw", StrawPath,
                                          new Color(0.55f, 0.50f, 0.41f), new Color(0.45f, 0.39f, 0.27f), 0.05f);

            var root = new GameObject("InteriorFurniture");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            var woodBatch = new Batch(1f);
            var paperBatch = new Batch(1f);
            var clayBatch = new Batch(1f);
            var strawBatch = new Batch(2.4f);
            // 告示纸:UV 直接 0..1(一张图贴一次),所以 metersPerTile 不参与
            Material sign = MakeMaterial(shader, "Furniture_Notice", NoticePath, Color.white,
                                         new Color(0.72f, 0.68f, 0.57f), 0.05f);
            var signBatch = new Batch(1f);

            AddDesk(woodBatch, paperBatch, deskCellX, deskCellZ);

            // ⚠️ 2026-11-13 实测修正:这四件**本来就有**,但三件的落位离主镜头构图很远
            //   (陶罐在 (5.45, 27.2) = 镜头背后、草席在 (11.5, 25.5) = 镜头脚下),
            //   所以对照概念图时读成「道具全缺」—— 缺的是**摆位**,不是模型。
            AddPot(clayBatch, new Vector3(deskCellX + 1.9f, 0f, deskCellZ - 0.9f));
            AddMat(strawBatch, new Vector3(deskCellX + 2.9f, 0f, deskCellZ - 0.2f));
            AddFallenBench(woodBatch, new Vector3(11.3f, 0f, 17.0f));
            AddBench(woodBatch, new Vector3(5.42f, 0f, 21.0f), 2.2f);     // 西墙直立长椅
            AddNotice(signBatch, 5.42f, 1.95f, 23.6f, 0.78f, 1.10f);     // 西墙告示

            int count = 0;
            count += woodBatch.Flush("Furniture_WoodMesh", wood, root.transform);
            count += paperBatch.Flush("Furniture_PaperMesh", paper, root.transform);
            count += clayBatch.Flush("Furniture_ClayMesh", clay, root.transform);
            count += strawBatch.Flush("Furniture_StrawMesh", straw, root.transform);
            count += signBatch.Flush("Furniture_NoticeMesh", sign, root.transform);

            Debug.Log($"[美术] 内景家具已建:{count} 批网格(案头 + 登记簿 + 陶罐 + 草席 + 长椅 ×2 + 告示)");
            return count;
        }

        /// <summary>案头:桌面 + 四条腿;桌上摊开一本登记簿(两页沿 Z 斜成浅 V)。</summary>
        private static void AddDesk(Batch wood, Batch paper, float cx, float cz)
        {
            const float topHalfX = 0.75f, topHalfZ = 0.36f, topHalfY = 0.03f;
            wood.AddBox(new Vector3(cx, TableTopY - topHalfY, cz),
                        new Vector3(topHalfX, topHalfY, topHalfZ));

            const float legHalf = 0.035f;
            float legTop = TableTopY - topHalfY * 2f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    wood.AddBox(
                        new Vector3(cx + sx * (topHalfX - 0.07f), legTop * 0.5f, cz + sz * (topHalfZ - 0.07f)),
                        new Vector3(legHalf, legTop * 0.5f, legHalf));

            // 登记簿:封面两片(木色偏深的纸板,这里用纸材质画封面 + 内页)
            const float pageHalfX = 0.19f, pageHalfZ = 0.145f;
            const float tilt = 7f;
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, side * tilt);
                Vector3 pivot = new Vector3(cx, TableTopY + 0.012f, cz);
                paper.AddBox(new Vector3(cx + side * pageHalfX, TableTopY + 0.012f, cz),
                             new Vector3(pageHalfX, 0.006f, pageHalfZ), rot, pivot);
            }
        }

        /// <summary>小口鼓腹陶罐(三段圆柱:底腹 / 上腹 / 口沿)。</summary>
        private static void AddPot(Batch b, Vector3 at)
        {
            b.AddCylinder(at, 0.15f, 0.21f, 0.17f, 16);
            b.AddCylinder(at + new Vector3(0f, 0.17f, 0f), 0.21f, 0.14f, 0.17f, 16);
            b.AddCylinder(at + new Vector3(0f, 0.34f, 0f), 0.14f, 0.10f, 0.07f, 16);
        }

        /// <summary>草席:一块平铺的方席,贴在地坪上 4 mm。</summary>
        private static void AddMat(Batch b, Vector3 at)
        {
            b.AddQuadY(new Vector3(at.x, InteriorFloor.Lift + 0.004f, at.z),
                       new Vector3(1.2f, 0f, 0.75f));
        }

        /// <summary>翻倒的长椅:侧躺 —— 座板立着(高 = 原来的进深),两条腿横支出去。
        /// <para>🔁 2026-11-13 两次实测修正:① 绕 Z 转会把一条腿转到地下;
        /// ② 绕长轴转 + `DropToFloor` 归一,包围盒反而整体抬高 1 m(`Furniture_WoodMesh`
        /// 的 y 上界 1.78,而桌面只有 0.78)⇒ 画面里多出一根竖着的木棍。
        /// **不再靠旋转与补偿**:直接按「已经躺下」的坐标摆三个盒体,落地面就是 0。</para></summary>
        private static void AddFallenBench(Batch b, Vector3 at)
        {
            // 座板:1.8 长(X)× 0.34 高 × 0.07 厚(Z) —— 原来的 0.34 进深现在成了高度
            b.AddBox(at + new Vector3(0f, 0.17f, 0f), new Vector3(0.90f, 0.17f, 0.035f));
            // 两条腿:0.42 长,以前竖着,现在横着指向 +Z
            b.AddBox(at + new Vector3(-0.72f, 0.035f, 0.21f), new Vector3(0.035f, 0.035f, 0.21f));
            b.AddBox(at + new Vector3(0.72f, 0.035f, 0.21f), new Vector3(0.035f, 0.035f, 0.21f));
        }

        /// <summary>
        /// 靠墙的**直立长椅**(2026-11-13 补)。概念图左墙那张:座板高 0.45、靠背到 0.98,
        /// 沿 z 摆放,靠背贴墙。
        /// <para>⚠️ 不带碰撞体:本类的通行由导航格决定(类头注),别在这里造第二真源。</para>
        /// </summary>
        private static void AddBench(Batch b, Vector3 at, float lengthZ)
        {
            const float depth = 0.40f, seatY = 0.45f, backTop = 0.98f;
            float halfZ = lengthZ * 0.5f;

            // 座板
            b.AddBox(at + new Vector3(0f, seatY, 0f), new Vector3(depth * 0.5f, 0.025f, halfZ));
            // 靠背(贴墙那侧,-X 是墙)
            b.AddBox(at + new Vector3(-depth * 0.5f + 0.02f, (seatY + backTop) * 0.5f, 0f),
                     new Vector3(0.022f, (backTop - seatY) * 0.5f, halfZ));
            // 四条腿
            for (int sz = -1; sz <= 1; sz += 2)
                for (int sx = -1; sx <= 1; sx += 2)
                    b.AddBox(at + new Vector3(sx * (depth * 0.5f - 0.04f), seatY * 0.5f, sz * (halfZ - 0.06f)),
                             new Vector3(0.028f, seatY * 0.5f, 0.028f));
        }

        /// <summary>墙上贴的一张告示纸(单面片,UV 0..1,法线朝屋里 +X)。</summary>
        private static void AddNotice(Batch b, float x, float y, float z, float w, float h)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            // 角序:左下 → 左上 → 右上 → 右下 ⇒ 法线 +X(朝屋里)
            b.AddQuadUv01(new Vector3(x, y - hh, z - hw),
                          new Vector3(x, y + hh, z - hw),
                          new Vector3(x, y + hh, z + hw),
                          new Vector3(x, y - hh, z + hw));
        }

        // ═══ 材质 ═══════════════════════════════════════════════════════════

        private static Material MakeFlat(Shader shader, string name, Color color, float smoothness)
        {
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private static Material MakeMaterial(Shader shader, string name, string resourcePath,
                                             Color tintIfPresent, Color tintIfAbsent, float smoothness)
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
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // ═══ 合批:一个材质一批 ═══════════════════════════════════════════════

        private sealed class Batch
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<int> _t = new List<int>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly float _metersPerTile;

            public Batch(float metersPerTile) => _metersPerTile = metersPerTile;

            /// <summary>盒体(可绕 <paramref name="pivot"/> 旋转 —— 长椅翻倒用)。</summary>
            public void AddBox(Vector3 c, Vector3 h, Quaternion? rot = null, Vector3 pivot = default)
            {
                Quaternion q = rot ?? Quaternion.identity;

                void Face(Vector3 center, Vector3 uDir, Vector3 vDir, float hu, float hv)
                {
                    int b = _v.Count;
                    Vector3 du = uDir * hu, dv = vDir * hv;
                    Vector3[] p =
                    {
                        center - du - dv, center + du - dv, center + du + dv, center - du + dv,
                    };
                    float s = 1f / _metersPerTile;
                    for (int i = 0; i < 4; i++) _v.Add(q * (p[i] - pivot) + pivot);
                    _uv.Add(new Vector2(0f, 0f));
                    _uv.Add(new Vector2(hu * 2f * s, 0f));
                    _uv.Add(new Vector2(hu * 2f * s, hv * 2f * s));
                    _uv.Add(new Vector2(0f, hv * 2f * s));
                    _t.Add(b); _t.Add(b + 1); _t.Add(b + 2);
                    _t.Add(b); _t.Add(b + 2); _t.Add(b + 3);
                }

                Face(c + new Vector3(h.x, 0, 0), Vector3.forward, Vector3.up, h.z, h.y);
                Face(c + new Vector3(-h.x, 0, 0), Vector3.back, Vector3.up, h.z, h.y);
                Face(c + new Vector3(0, h.y, 0), Vector3.right, Vector3.forward, h.x, h.z);
                Face(c + new Vector3(0, -h.y, 0), Vector3.right, Vector3.back, h.x, h.z);
                Face(c + new Vector3(0, 0, h.z), Vector3.right, Vector3.up, h.x, h.y);
                Face(c + new Vector3(0, 0, -h.z), Vector3.back, Vector3.up, h.x, h.y);
            }

            /// <summary>沿 +Y 的圆柱(侧壁 + 顶盖;底面被地面挡住,不封)。</summary>
            public void AddCylinder(Vector3 bottomCenter, float rBottom, float rTop, float height, int segments)
            {
                float y0 = bottomCenter.y, y1 = bottomCenter.y + height;
                int ring = _v.Count;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float u = i / (float)segments;
                    _v.Add(new Vector3(bottomCenter.x + cs * rBottom, y0, bottomCenter.z + sn * rBottom));
                    _uv.Add(new Vector2(u, 0f));
                    _v.Add(new Vector3(bottomCenter.x + cs * rTop, y1, bottomCenter.z + sn * rTop));
                    _uv.Add(new Vector2(u, height / _metersPerTile));
                }
                for (int i = 0; i < segments; i++)
                {
                    int n = (i + 1) % segments;
                    int a0 = ring + i * 2, a1 = ring + n * 2;
                    // 外侧朝外:从 +Y 俯视逆时针对应 (a0, a1+1, a1) 一侧
                    _t.Add(a0); _t.Add(a1 + 1); _t.Add(a1);
                    _t.Add(a0); _t.Add(a0 + 1); _t.Add(a1 + 1);
                }
                int center = _v.Count;
                _v.Add(new Vector3(bottomCenter.x, y1, bottomCenter.z));
                _uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < segments; i++)
                {
                    int n = (i + 1) % segments;
                    _t.Add(center);
                    _t.Add(ring + i * 2 + 1);
                    _t.Add(ring + n * 2 + 1);
                }
            }

            /// <summary>水平方片(法线 +Y)。</summary>
            public void AddQuadY(Vector3 center, Vector3 half)
            {
                int b = _v.Count;
                _v.Add(new Vector3(center.x - half.x, center.y, center.z - half.z));
                _v.Add(new Vector3(center.x + half.x, center.y, center.z - half.z));
                _v.Add(new Vector3(center.x + half.x, center.y, center.z + half.z));
                _v.Add(new Vector3(center.x - half.x, center.y, center.z + half.z));
                float s = 1f / _metersPerTile;
                _uv.Add(new Vector2(0f, 0f));
                _uv.Add(new Vector2(half.x * 2f * s, 0f));
                _uv.Add(new Vector2(half.x * 2f * s, half.z * 2f * s));
                _uv.Add(new Vector2(0f, half.z * 2f * s));
                _t.Add(b); _t.Add(b + 2); _t.Add(b + 1);
                _t.Add(b); _t.Add(b + 3); _t.Add(b + 2);
            }

            /// <summary>竖方片,UV **直接 0..1** —— 用于「一张图贴一次」的告示纸等。
            /// <para>角序 a→b→c→d 逆时针(从 +X 方向看)⇒ 法线 +X。</para></summary>
            public void AddQuadUv01(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
                _uv.Add(new Vector2(0f, 0f));
                _uv.Add(new Vector2(0f, 1f));
                _uv.Add(new Vector2(1f, 1f));
                _uv.Add(new Vector2(1f, 0f));
                _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
                _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
            }

            /// <summary>落成一张网格;空批返回 0(不产生空 GameObject)。</summary>
            public int Flush(string meshName, Material mat, Transform parent)
            {
                if (_v.Count == 0) return 0;
                var mesh = new Mesh { name = meshName };
                mesh.SetVertices(_v);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(meshName);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                return 1;
            }
        }
    }
}
