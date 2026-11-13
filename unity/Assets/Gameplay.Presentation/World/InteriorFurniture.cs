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
            Material straw = MakeMaterial(shader, "Furniture_Straw", StrawPath,
                                          new Color(0.90f, 0.85f, 0.76f), new Color(0.45f, 0.39f, 0.27f), 0.05f);

            var root = new GameObject("InteriorFurniture");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            var woodBatch = new Batch(1f);
            var paperBatch = new Batch(1f);
            var clayBatch = new Batch(1f);
            var strawBatch = new Batch(2.4f);

            AddDesk(woodBatch, paperBatch, deskCellX, deskCellZ);
            AddPot(clayBatch, new Vector3(5.45f, 0f, 27.2f));
            AddMat(strawBatch, new Vector3(11.5f, 0f, 25.5f));
            AddFallenBench(woodBatch, new Vector3(11.3f, 0f, 17.0f), 100f);

            int count = 0;
            count += woodBatch.Flush("Furniture_WoodMesh", wood, root.transform);
            count += paperBatch.Flush("Furniture_PaperMesh", paper, root.transform);
            count += clayBatch.Flush("Furniture_ClayMesh", clay, root.transform);
            count += strawBatch.Flush("Furniture_StrawMesh", straw, root.transform);

            Debug.Log($"[美术] 内景家具已建:{count} 批网格(案头 + 登记簿 + 陶罐 + 草席 + 翻倒长椅)");
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

        /// <summary>翻倒的长椅:座板 + 两条腿,绕**长轴(X)**翻 100° —— 读作「被掀翻」。
        /// <para>⚠️ 2026-11-13 实测修正:首版绕 **Z** 转 78°,一条腿转到地面以下
        /// (包围盒 y 下探 −0.79)⇒ 画面上只剩座板边缘与一条腿竖在地里,像一根歪插的木棍。
        /// 改为绕长轴翻倒,再把整件**下沉量归零**(`DropToFloor`),不靠手调枢轴猜高度。</para></summary>
        private static void AddFallenBench(Batch b, Vector3 at, float degrees)
        {
            int mark = b.Mark();
            Quaternion rot = Quaternion.Euler(degrees, 0f, 0f);
            b.AddBox(at + new Vector3(0f, 0.46f, 0f), new Vector3(0.90f, 0.035f, 0.17f), rot, at);
            b.AddBox(at + new Vector3(-0.72f, 0.23f, 0f), new Vector3(0.035f, 0.21f, 0.15f), rot, at);
            b.AddBox(at + new Vector3(0.72f, 0.23f, 0f), new Vector3(0.035f, 0.21f, 0.15f), rot, at);
            b.DropToFloor(mark, 0f);
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

            /// <summary>记下当前顶点数 —— 供 <see cref="DropToFloor"/> 只调整之后加进来的件。</summary>
            public int Mark() => _v.Count;

            /// <summary>把 <paramref name="fromIndex"/> 之后的顶点整体沿 Y 平移,
            /// 使这批件的最低点落在 <paramref name="floorY"/>。</summary>
            public void DropToFloor(int fromIndex, float floorY)
            {
                float min = float.MaxValue;
                for (int i = fromIndex; i < _v.Count; i++) min = Mathf.Min(min, _v[i].y);
                if (min == float.MaxValue) return;
                float dy = floorY - min;
                for (int i = fromIndex; i < _v.Count; i++)
                    _v[i] = new Vector3(_v[i].x, _v[i].y + dy, _v[i].z);
            }

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
