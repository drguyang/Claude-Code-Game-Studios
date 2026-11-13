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

            BuildEastTear(root.transform);
            Debug.Log($"[美术] 内景墙 {created} 面已生成(每面一张 albedo;东墙塌口上方的墙按裂口轮廓下落)");
            BuildWestWindowFrame(root.transform, shader);
            BuildNorthWindowFrame(root.transform, shader);
            return created;
        }

        /// <summary>
        /// 北墙拱窗的木框(2026-11-13 · §8.12)。
        /// <para>拱券做成**环带**(半径 r0..r0+0.10 的 180° 环),不是一排小方块 ——
        /// 小方块拼拱在斜段上会露出台阶,读作「砌歪的砖」而不是木券。</para>
        /// </summary>
        private static void BuildNorthWindowFrame(Transform parent, Shader shader)
        {
            InteriorWindows.Opening w = InteriorWindows.North;
            const float barHalf = 0.05f;
            const float depth = 0.16f;
            float zFront = InteriorWindows.NorthFaceZ - Inset - depth;   // 屋内侧那一面
            float zBack = zFront + depth;
            float zMid = zFront + depth * 0.5f;
            float cx = (w.A0 + w.A1) * 0.5f;
            // ⚠️ 框料必须**压进洞口**:首版把环带放在 r0..r0+0.10(全在洞外),
            //    而它在面板**后面** ⇒ 从屋里什么也看不见,拱边光秃秃的(2026-11-13 实测)。
            //    改成 r−0.10 .. r+0.02:十厘米在洞内(看得见),两厘米压住边(不露缝)。
            float rHole = w.ArchRadius;
            float r0 = rHole - 0.10f, r1 = rHole + 0.02f;

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();

            // 窗台 + 左右梃(轴对齐盒体;同样压进洞口一半)
            Box(v, t, uv, new Vector3(cx, w.Y0 + barHalf, zMid),
                new Vector3(w.Width * 0.5f + barHalf, barHalf, depth * 0.5f));
            Box(v, t, uv, new Vector3(w.A0 + barHalf, (w.Y0 + w.ArchY) * 0.5f, zMid),
                new Vector3(barHalf, (w.ArchY - w.Y0) * 0.5f, depth * 0.5f));
            Box(v, t, uv, new Vector3(w.A1 - barHalf, (w.Y0 + w.ArchY) * 0.5f, zMid),
                new Vector3(barHalf, (w.ArchY - w.Y0) * 0.5f, depth * 0.5f));

            // 拱券环带
            const int segments = 14;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * i / segments;
                float a1 = Mathf.PI * (i + 1) / segments;
                float am = (a0 + a1) * 0.5f;
                Vector3 radial = new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f);

                Vector3 P(float r, float ang, float z)
                    => new Vector3(cx + Mathf.Cos(ang) * r, w.ArchY + Mathf.Sin(ang) * r, z);

                // 屋里那一面(朝 +z)、墙里那一面(朝 −z)、外缘、内缘
                Quad(v, t, uv, P(r0, a0, zFront), P(r1, a0, zFront),
                                  P(r1, a1, zFront), P(r0, a1, zFront), Vector3.forward);
                Quad(v, t, uv, P(r0, a0, zBack), P(r0, a1, zBack),
                                  P(r1, a1, zBack), P(r1, a0, zBack), Vector3.back);
                Quad(v, t, uv, P(r1, a0, zFront), P(r1, a0, zBack),
                                  P(r1, a1, zBack), P(r1, a1, zFront), radial);
                Quad(v, t, uv, P(r0, a0, zFront), P(r0, a1, zFront),
                                  P(r0, a1, zBack), P(r0, a0, zBack), -radial);
            }

            var mesh = new Mesh { name = "Wall_NorthWindowFrame" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Wall_NorthWindowFrame");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MakeWoodMaterial(shader);
            go.layer = WorldWallGreybox.WorldGeometryLayer;

            Debug.Log($"[美术] 北墙拱窗已开:{w.Width:0.0} m 宽,窗台 {w.Y0:0.00} / 起拱 {w.ArchY:0.00} / " +
                      $"拱顶 {w.Y1:0.00} m(带木券)");
        }

        /// <summary>窗框用的木材质(与屋顶同一张木纹)。</summary>
        private static Material MakeWoodMaterial(Shader shader)
        {
            var mat = new Material(shader) { name = "InteriorWall_WindowFrame" };
            Texture2D wood = Resources.Load<Texture2D>(InteriorRoofFrame.WoodPath);
            if (wood != null) mat.SetTexture("_BaseMap", wood);
            mat.SetColor("_BaseColor", new Color(0.44f, 0.37f, 0.31f));
            mat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            mat.SetFloat("_Smoothness", 0.06f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        /// <summary>四边形(顶点顺序任意):按 <paramref name="wantedNormal"/> 自动定绕序。</summary>
        private static void Quad(List<Vector3> v, List<int> t, List<Vector2> uv,
                                 Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 wantedNormal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), wantedNormal) < 0f)
            {
                (b, d) = (d, b);   // 反序 ⇒ 法线翻到想要的一侧
            }

            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(Vector3.Distance(a, b), 0f));
            uv.Add(new Vector2(Vector3.Distance(a, b), Vector3.Distance(b, c)));
            uv.Add(new Vector2(0f, Vector3.Distance(b, c)));
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
            t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
        }

        /// <summary>
        /// 西墙窗洞的木框(2026-11-13 · §8.11)。
        /// <para>没有框,墙上就只是一个**方洞**;概念图的窗有框、有中挺,正是它读作「窗」的原因。
        /// 框落在面板与灰盒墙之间的空档里(x≈4.86),不挡视线也不参与通行。</para>
        /// </summary>
        private static void BuildWestWindowFrame(Transform parent, Shader shader)
        {
            InteriorWindows.Opening w = InteriorWindows.West;
            const float barHalf = 0.06f;    // 框料半宽
            const float depthHalf = 0.08f;  // 进深半厚
            float zMid = (w.A0 + w.A1) * 0.5f;
            float yMid = (w.Y0 + w.Y1) * 0.5f;
            float x = InteriorWindows.WestFaceX - 0.16f;

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();

            void Bar(float cy, float cz, float hy, float hz)
                => Box(v, t, uv, new Vector3(x, cy, cz), new Vector3(depthHalf, hy, hz));

            float halfSpan = (w.A1 - w.A0) * 0.5f + barHalf;
            float halfJamb = (w.Y1 - w.Y0) * 0.5f;
            Bar(w.Y0 - barHalf, zMid, barHalf, halfSpan);          // 下框
            Bar(w.Y1 + barHalf, zMid, barHalf, halfSpan);          // 上框
            Bar(yMid, w.A0 - barHalf, halfJamb, barHalf);          // 左梃
            Bar(yMid, w.A1 + barHalf, halfJamb, barHalf);          // 右梃
            Bar(yMid, zMid, halfJamb, 0.035f);                      // 竖中挺
            Bar(yMid, zMid, 0.035f, (w.A1 - w.A0) * 0.5f);          // 横中挺

            var mesh = new Mesh { name = "Wall_WestWindowFrame" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mat = MakeWoodMaterial(shader);

            var go = new GameObject("Wall_WestWindowFrame");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = WorldWallGreybox.WorldGeometryLayer;

            Debug.Log($"[美术] 西墙窗洞已开:{(w.A1 - w.A0):0.0} × {(w.Y1 - w.Y0):0.0} m,窗台 {w.Y0:0.00} m(带木框)");
        }

        /// <summary>轴对齐盒体(窗框用);UV 取米数,与木纹尺度一致。</summary>
        private static void Box(List<Vector3> v, List<int> t, List<Vector2> uv,
                                Vector3 c, Vector3 h)
        {
            void Face(Vector3 center, Vector3 uDir, Vector3 vDir, float hu, float hv)
            {
                int b = v.Count;
                Vector3 du = uDir * hu, dv = vDir * hv;
                Vector3[] p =
                {
                    center - du - dv, center + du - dv, center + du + dv, center - du + dv,
                };
                for (int i = 0; i < 4; i++) v.Add(p[i]);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(hu * 2f, 0f));
                uv.Add(new Vector2(hu * 2f, hv * 2f));
                uv.Add(new Vector2(0f, hv * 2f));
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

        // ── 三个整面(0..H)──────────────────────────────────────────────────

        private static Face WestFace()
        {
            const float x = X0 + Inset;
            InteriorWindows.Opening w = InteriorWindows.West;
            return new Face
            {
                Name = "West",
                TexturePath = "Art/church-interior/wall_west",
                WidthM = Z1 - Z0,
                UAlongX = false,
                UOrigin = Z0,
                UScale = 1f / (Z1 - Z0),
                Normal = Vector3.right,
                // 🔁 2026-11-13(§8.11 开窗):由「一整块」改成「洞口四周四块」。
                //    UV 是**按位置**算的(见 BuildFace),所以拆开之后贴图在洞口四周连续,
                //    不需要另作 UV 校正 —— 这也是这里敢直接切四块的原因。
                Quads = new List<Vector3[]>
                {
                    RectZ(x, Z0, Z1, 0f, w.Y0),            // 窗下
                    RectZ(x, Z0, Z1, w.Y1, Height),        // 窗上
                    RectZ(x, Z0, w.A0, w.Y0, w.Y1),        // 窗左(靠 Z0 一侧)
                    RectZ(x, w.A1, Z1, w.Y0, w.Y1),        // 窗右
                },
            };
        }

        /// <summary>东西向的墙面上的一块矩形:x 固定,z / y 给区间。角序与其它面一致(左下起逆时针)。</summary>
        private static Vector3[] RectZ(float x, float z0, float z1, float y0, float y1) => new[]
        {
            new Vector3(x, y0, z0), new Vector3(x, y0, z1),
            new Vector3(x, y1, z1), new Vector3(x, y1, z0),
        };

        /// <summary>
        /// 北墙:**拱窗**(§8.12)。
        /// <para>洞口 = 矩形(窗台→起拱)+ 半圆(起拱→拱顶,半径 = 宽/2)。
        /// 拆法是「下 / 左 / 右 + 拱上方的扇形」四组三角形 —— 扇形按弧线采样,
        /// 每一段从弧上两点垂直拉到墙顶,恰好铺满「矩形挖掉半圆」的那块(即拱肩)。</para>
        /// </summary>
        private static Face NorthFace()
        {
            const float z = Z0 + Inset;
            InteriorWindows.Opening w = InteriorWindows.North;
            var quads = new List<Vector3[]>
            {
                RectX(z, X0, X1, 0f, w.Y0),            // 窗下
                RectX(z, X0, w.A0, w.Y0, Height),      // 左(靠 X0 一侧)
                RectX(z, w.A1, X1, w.Y0, Height),      // 右
            };

            // 拱肩:沿半圆采样,每段两点拉到墙顶
            float cx = (w.A0 + w.A1) * 0.5f;
            float r = w.ArchRadius;
            const int segments = 14;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * i / segments;
                float a1 = Mathf.PI * (i + 1) / segments;
                float x0 = cx + Mathf.Cos(a0) * r;
                float x1 = cx + Mathf.Cos(a1) * r;
                float y0 = w.ArchY + Mathf.Sin(a0) * r;
                float y1 = w.ArchY + Mathf.Sin(a1) * r;
                quads.Add(new[]
                {
                    new Vector3(x0, y0, z), new Vector3(x1, y1, z),
                    new Vector3(x1, Height, z), new Vector3(x0, Height, z),
                });
            }

            // 山墙:墙顶以上到屋面下皮 —— 不收口就是一个三角天窗(2026-11-13 补)
            AppendGable(quads, z);

            return new Face
            {
                Name = "North",
                TexturePath = "Art/church-interior/wall_north",
                WidthM = X1 - X0,
                UAlongX = true,
                UOrigin = X0,
                UScale = 1f / (X1 - X0),
                Normal = Vector3.forward,
                Quads = quads,
            };
        }

        /// <summary>南北向的墙面上的一块矩形:z 固定,x / y 给区间。</summary>
        private static Vector3[] RectX(float z, float x0, float x1, float y0, float y1) => new[]
        {
            new Vector3(x0, y0, z), new Vector3(x1, y0, z),
            new Vector3(x1, y1, z), new Vector3(x0, y1, z),
        };

        // ── 山墙(2026-11-13 · 补「漏天」)──────────────────────────────────────
        // 病因(实测):墙只砌到 `Height`(5.5 m),而屋面从檐口 5.5 **升到屋脊 7.3** ——
        // 南北两端墙顶之上因此留出一个高 1.8 m 的**三角豁口**,抬头就能从天上看出去。
        // 那不是"破口",是**没砌山墙**:概念图里两端是实心的(上半部只是被光打亮)。
        // ⇒ 每端补一片三角墙,顶边贴着屋面下皮。
        //
        // ⚠️ 顶边**故意过顶 0.30 m**:贴图/几何不保证屋面与墙严丝合缝,差几厘米就又是一条
        //    细长的天窗(比整块漏天更难发现)。多出来的部分藏在瓦垄里,看不见。
        private const float RoofEaveY = 5.50f;
        private const float RoofRidgeY = 7.30f;
        private const float GableOvershoot = 0.30f;

        private static float RoofUndersideY(float x)
        {
            float half = (X1 - X0) * 0.5f;
            float cx = (X0 + X1) * 0.5f;
            float t = 1f - Mathf.Abs(x - cx) / half;      // 0 = 檐口,1 = 屋脊
            return RoofEaveY + (RoofRidgeY - RoofEaveY) * Mathf.Clamp01(t) + GableOvershoot;
        }

        /// <summary>一端山墙:沿 x 切成 <paramref name="segments"/> 条竖带,顶边跟着屋面走。</summary>
        private static void AppendGable(List<Vector3[]> quads, float z, int segments = 20)
        {
            float step = (X1 - X0) / segments;
            for (int i = 0; i < segments; i++)
            {
                float x0 = X0 + step * i;
                float x1 = x0 + step;
                quads.Add(new[]
                {
                    new Vector3(x0, Height, z), new Vector3(x1, Height, z),
                    new Vector3(x1, RoofUndersideY(x1), z), new Vector3(x0, RoofUndersideY(x0), z),
                });
            }
        }

        private static Face SouthFace()
        {
            const float z = Z1 - Inset;
            var quads = new List<Vector3[]>
            {
                new[]
                {
                    new Vector3(X1, 0f, z), new Vector3(X0, 0f, z),
                    new Vector3(X0, Height, z), new Vector3(X1, Height, z),
                },
            };
            // 山墙(与北端同一处置)
            AppendGable(quads, z);

            return new Face
            {
                Name = "South",
                TexturePath = "Art/church-interior/wall_south",
                WidthM = X1 - X0,
                UAlongX = true,
                UOrigin = X1,
                UScale = -1f / (X1 - X0),
                Normal = Vector3.back,
                Quads = quads,
            };
        }

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

        /// <summary>
        /// 东墙塌口的**厚度**(2026-11-13 补「边缘读不出断裂」)。
        /// <para>病因:整面墙是**一张零厚度的面片** —— 塌口的断口因此是一条数学意义上的线,
        /// 从屋里斜看过去读作「墙到这儿就没了」,没有厚度、没有阴影、没有断面。
        /// 概念图里那圈断口是有厚度的砖砌(能看到 40~50 cm 宽的墙芯与投在它上面的暗面)。</para>
        /// <para>做法:沿 profile 每段加一片**墙顶面**(从屋内皮 x≈14.98 到墙外皮 15.50),
        /// 再在塌口两端各补一片**竖向断面**(jamb)。不做外墙面 —— 从屋里看不到它,
        /// 多做一层反而在窗外挡住天光。</para>
        /// </summary>
        private static void BuildEastTear(Transform parent)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            const float xIn = X1 - Inset;
            const float xOut = X1 + 0.50f;

            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(a.z * 0.25f, a.y * 0.25f));
                uv.Add(new Vector2(b.z * 0.25f, b.y * 0.25f));
                uv.Add(new Vector2(c.z * 0.25f, c.y * 0.25f));
                uv.Add(new Vector2(d.z * 0.25f, d.y * 0.25f));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            void Cap(Vector2[] profile)
            {
                for (int i = 0; i < profile.Length - 1; i++)
                {
                    Vector2 a = profile[i], b = profile[i + 1];
                    // 顶面:法线 +Y(朝上),角序从屋内往外
                    Quad(new Vector3(xIn, a.y, a.x), new Vector3(xOut, a.y, a.x),
                         new Vector3(xOut, b.y, b.x), new Vector3(xIn, b.y, b.x));
                }
            }

            void Jamb(float z, float yTop)
            {
                // 断口立面:朝塌口那一侧,法线 ±Z
                Quad(new Vector3(xIn, 0f, z), new Vector3(xOut, 0f, z),
                     new Vector3(xOut, yTop, z), new Vector3(xIn, yTop, z));
            }

            Cap(EastSouthProfile);
            Cap(EastNorthProfile);
            Jamb(EastSouthProfile[EastSouthProfile.Length - 1].x,
                 EastSouthProfile[EastSouthProfile.Length - 1].y);
            Jamb(EastNorthProfile[0].x, EastNorthProfile[0].y);

            var mesh = new Mesh { name = "Wall_EastTearMesh" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Wall_EastTear");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mat = new Material(shader) { name = "InteriorWall_EastTear" };
            Texture2D tex = Resources.Load<Texture2D>("Art/church-interior/wall_easts");
            if (tex != null) { mat.SetTexture("_BaseMap", tex); }
            mat.SetColor("_BaseColor", new Color(0.72f, 0.70f, 0.67f));
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = WorldWallGreybox.WorldGeometryLayer;
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

            // ── 法线贴图(2026-11-13 · §8.10 批 2)──────────────────────────
            // 病因:五面材质此前**只挂 `_BaseMap`** ⇒ 墙没有任何表面朝向变化,
            //   再好的 albedo 也只是一张贴纸 —— 抹灰的起伏、砖缝的凹陷全丢。
            // `*_normal` 与 albedo 同源(同一张图当高度场算的),面与面之间不会串。
            // 缺席 ⇒ 不挂(不报错):贴图是 local-only 资产,别机上本来就没有。
            Texture2D bump = Resources.Load<Texture2D>(f.TexturePath + "_normal");
            if (bump != null)
            {
                mat.SetTexture("_BumpMap", bump);
                mat.SetFloat("_BumpScale", 1.6f);
            }

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

    }
}
