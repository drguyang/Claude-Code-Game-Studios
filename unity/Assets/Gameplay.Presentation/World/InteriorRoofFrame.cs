// 教堂内景的**屋顶木构**(2026-11-13)。运行期生成,不预摆(ADR-023 §②)。
//
// 权威:`design/assets/specs/church-interior.md` §8(内景补件:屋架 / 地面 / 家具)
//       概念图 `approved/scene-interior-a.png` —— 顶部是**残存的梁椽**,不是「没有顶」。
//
// ═══ 为什么需要它 ═════════════════════════════════════════════════════════
//   2026-11-13 比对:真包内景与定稿概念图**不像**。七项差异里排第一的是屋架 ——
//   概念图的顶部有五根横梁 + 一片残椽,天空从断口漏下来;真包顶部**什么都没有**,
//   读作「站在露天旷野的一圈矮墙里」。四面墙做得再好,顶上空着就仍是旷野。
//
// ═══ 形态取值(全部整数/定值,可复算)══════════════════════════════════════
//   室内净空:x∈[5,15] / z∈[15,29] / 高 5.5 m(与 `InteriorWallPanels` 同一组边界)。
//   · 横梁 ×5:沿 X 跨 10 m,断面 0.28 × 0.34 m,梁顶 y = 5.28;
//     z 取 17.0 / 19.6 / 22.2 / 24.8 / 27.4(等距 2.6 m)。
//   · 椽子 ×13:沿 Z 跨 14 m,断面 0.14 × 0.16 m,坐在梁上(底面 y = 5.28);
//     x 从 5.7 起每 0.7 m 一根。
//   · **战损**:椽子按一张**手写表**断掉或在塌口附近折半 —— 不用随机数
//     (`UnityEngine.Random` 会破坏可重放,且每次编译出来的残破不同,无法比对截图)。
//     缺 3 根、断 3 根,其余完整;断口集中在东侧(x 大)与塌口 z∈[20,23] 一带。
//
// ⚠️ **不做屋面望板/瓦** —— 概念图那里本来就是漏天的,补上瓦反而背离定稿。
//
// ⚠️ **本件不参与通行判定**:椽子在 5 m 高处,只是画面;没有碰撞体。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景屋顶的残存梁椽(运行期生成,零 3D 资产)。</summary>
    public static class InteriorRoofFrame
    {
        /// <summary>`Resources` 下的木纹路径(不含扩展名)。缺席 ⇒ 用素色,不跳过几何。</summary>
        public const string WoodPath = "Art/church-interior/roof_wood";

        /// <summary>木纹贴图覆盖的真实边长(米)。缺席时无意义。</summary>
        public const float WoodTileMeters = 1f;

        // ── 室内净空(与 `InteriorWallPanels` 同一组边界)
        private const float X0 = 5f, X1 = 15f, Z0 = 15f, Z1 = 29f;
        private const float Height = 5.5f;

        // ── 横梁:z 位置、断面(沿 z 宽 / 沿 y 高)、梁顶高度
        private static readonly float[] BeamZ = { 17.0f, 19.6f, 22.2f, 24.8f, 27.4f };
        private const float BeamWidth = 0.28f;   // 沿 z
        private const float BeamDepth = 0.34f;   // 沿 y
        private const float BeamTopY = 5.28f;

        // ── 椽子:起止 x、间距、断面
        private const float RafterX0 = 5.7f;
        private const float RafterStep = 0.7f;
        private const int RafterCount = 13;
        private const float RafterWidth = 0.14f;   // 沿 x
        private const float RafterDepth = 0.16f;   // 沿 y

        /// <summary>一根椽子的残存段(沿 z 的一段区间)。</summary>
        private struct Rafter
        {
            public int Index;      // 0..12,决定 x
            public float From;     // z 起点
            public float To;       // z 终点
            public bool Broken;    // 断口 ⇒ 端头做斜切,不做方头
        }

        /// <summary>椽子的战损表(手写,确定性)。</summary>
        private static readonly Rafter[] Rafters =
        {
            new Rafter { Index = 0,  From = Z0, To = Z1 },                    // x 5.7 完整
            new Rafter { Index = 1,  From = Z0, To = Z1 },                    // x 6.4 完整
            new Rafter { Index = 2,  From = Z0, To = 16.4f, Broken = true },  // x 7.1 只剩北头一截
            new Rafter { Index = 3,  From = Z0, To = Z1 },                    // x 7.8 完整
            new Rafter { Index = 4,  From = Z0, To = Z1 },                    // x 8.5 完整
            new Rafter { Index = 5,  From = Z0, To = Z1 },                    // x 9.2 完整
            new Rafter { Index = 6,  From = Z0, To = 19.3f, Broken = true },  // x 9.9 塌口北侧折断
            new Rafter { Index = 6,  From = 24.8f, To = Z1, Broken = true },  // x 9.9 塌口南侧残段
            new Rafter { Index = 7,  From = Z0, To = Z1 },                    // x 10.6 完整
            new Rafter { Index = 8,  From = 27.6f, To = Z1, Broken = true },  // x 11.3 只剩南头
            new Rafter { Index = 9,  From = Z0, To = 20.1f, Broken = true },  // x 12.0 塌口北侧折断
            new Rafter { Index = 9,  From = 25.4f, To = Z1, Broken = true },  // x 12.0 塌口南侧残段
            new Rafter { Index = 10, From = Z0, To = Z1 },                    // x 12.7 完整
            new Rafter { Index = 11, From = Z0, To = 17.3f, Broken = true },  // x 13.4 只剩北头
            // Index = 12(x 14.1)整根没有 —— 东侧塌得最狠
        };

        /// <summary>生成屋架并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>生成的构件数(梁 + 椽段)。</returns>
        public static int Build(Transform parent = null)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();

            float beamHalfW = BeamWidth * 0.5f;
            float beamHalfD = BeamDepth * 0.5f;
            float beamCenterY = BeamTopY - beamHalfD;
            for (int i = 0; i < BeamZ.Length; i++)
            {
                AppendBox(verts, tris, uvs,
                          new Vector3((X0 + X1) * 0.5f, beamCenterY, BeamZ[i]),
                          new Vector3((X1 - X0) * 0.5f, beamHalfD, beamHalfW));
            }

            float rafHalfW = RafterWidth * 0.5f;
            float rafHalfD = RafterDepth * 0.5f;
            float rafCenterY = BeamTopY + rafHalfD;
            for (int i = 0; i < Rafters.Length; i++)
            {
                Rafter r = Rafters[i];
                float x = RafterX0 + r.Index * RafterStep;
                float zMid = (r.From + r.To) * 0.5f;
                float zHalf = (r.To - r.From) * 0.5f;
                AppendBox(verts, tris, uvs,
                          new Vector3(x, rafCenterY, zMid),
                          new Vector3(rafHalfW, rafHalfD, zHalf));

                // 断口:在端头补一小块**斜切**的残木,让端面不是一刀平。
                if (r.Broken)
                {
                    float zEnd = r.From > Z0 ? r.From : r.To;   // 断的那一头
                    float dir = r.From > Z0 ? 1f : -1f;         // 断口朝室内
                    AppendBrokenTip(verts, tris, uvs,
                                    new Vector3(x, rafCenterY, zEnd),
                                    new Vector3(rafHalfW, rafHalfD, 0.18f), dir);
                }
            }

            var mesh = new Mesh { name = "ChurchRoofFrame" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var root = new GameObject("InteriorRoofFrame");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeWoodMaterial();
            root.layer = WorldWallGreybox.WorldGeometryLayer;

            Debug.Log($"[美术] 屋架已建:横梁 {BeamZ.Length} 根 + 椽段 {Rafters.Length} 段" +
                      "(手写战损表;东侧与塌口一带残缺)");
            return BeamZ.Length + Rafters.Length;
        }

        /// <summary>木料材质:有 `Resources` 木纹就贴,没有就素色(不跳过几何)。</summary>
        private static Material MakeWoodMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "InteriorRoofWood_Mat" };

            Texture2D tex = Resources.Load<Texture2D>(WoodPath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap",
                    new Vector2(1f / WoodTileMeters, 1f / WoodTileMeters));
                mat.SetColor("_BaseColor", new Color(0.82f, 0.78f, 0.72f));
            }
            else
            {
                mat.SetColor("_BaseColor", new Color(0.24f, 0.19f, 0.15f));
                Debug.Log($"[美术] 本地缺席:{WoodPath}(屋架木纹)—— 屋架用素色深木");
            }

            mat.SetFloat("_Smoothness", 0.06f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        /// <summary>断口残木:一块沿 z 向收细的楔块,避免「一刀平」的锯口。</summary>
        private static void AppendBrokenTip(List<Vector3> v, List<int> t, List<Vector2> uv,
                                            Vector3 atEnd, Vector3 half, float dir)
        {
            // 沿 dir 方向再伸出 0.18 m,断面在末端缩到 55% —— 视觉上像撕裂/折断。
            var center = atEnd + new Vector3(0f, -0.01f, dir * half.z * 0.55f);
            var tipHalf = new Vector3(half.x * 0.55f, half.y * 0.62f, half.z);
            AppendBox(v, t, uv, center, tipHalf);
        }

        // ═══ 盒体拼装(带按米计的 UV)═══════════════════════════════════════
        // 六个面各 4 顶点 2 三角;UV 用该面两个**世界轴**的米数除以贴图边长,
        // 这样每根梁上的木纹尺度一致,不随梁长拉伸。

        private static void AppendBox(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
                                      Vector3 c, Vector3 h)
        {
            // +X
            AppendFace(verts, tris, uvs, c + new Vector3(h.x, 0, 0), Vector3.forward, Vector3.up, h.z, h.y);
            // -X
            AppendFace(verts, tris, uvs, c + new Vector3(-h.x, 0, 0), Vector3.back, Vector3.up, h.z, h.y);
            // +Y
            AppendFace(verts, tris, uvs, c + new Vector3(0, h.y, 0), Vector3.right, Vector3.forward, h.x, h.z);
            // -Y
            AppendFace(verts, tris, uvs, c + new Vector3(0, -h.y, 0), Vector3.right, Vector3.back, h.x, h.z);
            // +Z
            AppendFace(verts, tris, uvs, c + new Vector3(0, 0, h.z), Vector3.right, Vector3.up, h.x, h.y);
            // -Z
            AppendFace(verts, tris, uvs, c + new Vector3(0, 0, -h.z), Vector3.back, Vector3.up, h.x, h.y);
        }

        /// <summary>一张面:中心、两个切线(决定 u/v 方向与绕序)、半宽、半高。</summary>
        private static void AppendFace(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
                                       Vector3 center, Vector3 uDir, Vector3 vDir,
                                       float halfU, float halfV)
        {
            int b = verts.Count;
            Vector3 du = uDir * halfU;
            Vector3 dv = vDir * halfV;

            verts.Add(center - du - dv);
            verts.Add(center + du - dv);
            verts.Add(center + du + dv);
            verts.Add(center - du + dv);

            float s = 1f / WoodTileMeters;
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(halfU * 2f * s, 0f));
            uvs.Add(new Vector2(halfU * 2f * s, halfV * 2f * s));
            uvs.Add(new Vector2(0f, halfV * 2f * s));

            // u × v = 外法线 ⇒ 该序为逆时针(Clockwise = false)。
            tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);
        }
    }
}
