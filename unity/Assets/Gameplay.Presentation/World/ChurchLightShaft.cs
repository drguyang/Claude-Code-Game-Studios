// 教堂内景的**光柱**(2026-11-13 · §8.7)。运行期生成的加法混合体块,不预摆(ADR-023 §②)。
//
// 权威:`design/assets/specs/church-interior.md` §8.7
//       概念图 `approved/scene-interior-a.png` —— 东侧塌口射进来的一道晨光,空气里有尘。
//
// ═══ 为什么是「一块几何」而不是体积光 ═════════════════════════════════════
//   URP 6.3 的体积光要走后处理 / 自定义 Renderer Feature,而 ADR-023 §⑧ 明写
//   **P0 零 custom Renderer Feature**(触发须另开 ADR)。⇒ 本件用**加法混合的锥台网格**:
//   几何是真几何,混合模式用 URP Unlit 自带的透明/加法档,**没有新 shader、没有新 Feature**。
//
// ═══ 形状 = 灯的锥体本身,不是「塌口投影」══════════════════════════════════
//   🔁 2026-11-13 首版把**整个塌口矩形**(3 × 4.25 m)沿光线投到地面 ⇒ 得到的是一个占半屏的
//   灰色大楔子,不像光柱。光柱的直径应当由**灯的锥角**决定:半径 = 沿轴距离 × tan(半角)。
//   ⇒ 现在按 (起点, 落点, 锥角) 算锥台:墙面上截面半径 ≈ 0.28 m,到地面 ≈ 2.5 m。
//
// ═══ 渐隐靠贴图的 alpha,不靠顶点色 ═══════════════════════════════════════
//   URP 的 `Unlit` **不读顶点色**(顶点 alpha 在它上面无效)。
//   ⇒ 运行时造一张 1×64 的竖向渐变当 `_BaseMap`,v=0(墙面)不透明 → v=1(落地)全透明。
//
// ⚠️ 硬边:加法体块的轮廓是**硬**的(没有深度感知的柔化)。低 alpha 下可接受;
//   要真正柔和得走后处理(另裁)。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>东侧塌口射入的晨光光柱(加法混合锥台 + 竖向 alpha 渐变)。</summary>
    public static class ChurchLightShaft
    {
        /// <summary>`Resources` 下的 shader 路径(不含扩展名)。
        /// <para>⚠️ 必须走 `Resources`:`Shader.Find` 在**打包版里找不到**没被任何材质引用的 shader
        /// (构建期被剥,`TD-build-03` 同一课;2026-11-13 首轮实测 URP Unlit 返回 null)。
        /// 自带 shader 还顺带解决「相机走进光柱里糊屏」——见 `LightShaft.shader` 的注释。</para></summary>
        public const string ShaderResourcePath = "LightShaft";

        /// <summary>体块颜色(暖白)。</summary>
        public static readonly Color ShaftColor = new Color(1.00f, 0.94f, 0.82f, 1f);

        /// <summary>墙面处的不透明度。高于 0.18 开始读作「一块发光的塑料」。</summary>
        public const float ApertureAlpha = 0.015f;

        /// <summary>近处淡出距离(米):相机贴到柱子身上时这一段直接归零,不糊屏。</summary>
        public const float NearFade = 3.0f;

        /// <summary>投影落点的高度(略高于地坪 0.03,避免与地面共面)。</summary>
        public const float FloorY = 0.05f;

        /// <summary>墙面所在的 x(东墙内表面)。光柱从这里起算。</summary>
        public const float WallX = 15f - 0.02f;

        /// <summary>锥台周向分段数。</summary>
        private const int Segments = 16;

        /// <summary>生成光柱。</summary>
        /// <param name="lightFrom">灯的起点(墙外)。</param>
        /// <param name="lightTo">灯的落点。</param>
        /// <param name="spotAngleDeg">灯的**全锥角**(与 `Light.spotAngle` 同值)。</param>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>1 = 已生成;0 = 方向退化或材质缺席。</returns>
        public static int Build(Vector3 lightFrom, Vector3 lightTo, float spotAngleDeg,
                                Transform parent = null)
        {
            Vector3 d = lightTo - lightFrom;
            if (Mathf.Abs(d.y) < 1e-4f || Mathf.Abs(d.x) < 1e-4f) return 0;
            float length = d.magnitude;
            d /= length;

            // 沿轴到墙面 / 到地面的距离
            float tWall = (WallX - lightFrom.x) / d.x;
            float tFloor = (FloorY - lightFrom.y) / d.y;
            if (tWall <= 0f || tFloor <= tWall) return 0;

            float tanHalf = Mathf.Tan(spotAngleDeg * 0.5f * Mathf.Deg2Rad);
            Vector3 cWall = lightFrom + d * tWall;
            Vector3 cFloor = lightFrom + d * tFloor;
            float rWall = tWall * tanHalf;
            float rFloor = tFloor * tanHalf;

            // 与轴垂直的一组正交基
            Vector3 u = Vector3.Cross(d, Vector3.up);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.Cross(d, Vector3.right);
            u.Normalize();
            Vector3 v = Vector3.Cross(d, u).normalized;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < Segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / Segments;
                float a1 = (i + 1) * Mathf.PI * 2f / Segments;

                Vector3 w0 = cWall + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * rWall;
                Vector3 w1 = cWall + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * rWall;
                Vector3 f0 = ClampAboveFloor(cFloor + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * rFloor);
                Vector3 f1 = ClampAboveFloor(cFloor + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * rFloor);

                int b = verts.Count;
                verts.Add(w0); verts.Add(w1); verts.Add(f1); verts.Add(f0);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);   // 双面由材质 `_Cull = Off` 提供
            }

            var mesh = new Mesh { name = "ChurchLightShaftMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                Debug.Log($"[光照] 光柱跳过:本地缺席 {ShaderResourcePath}.shader");
                return 0;
            }

            var mat = new Material(shader) { name = "ChurchLightShaft_Mat" };
            mat.SetTexture("_MainTex", GradientTexture());
            Color c = ShaftColor;
            c.a = ApertureAlpha;
            mat.SetColor("_Color", c);
            mat.SetFloat("_NearFade", NearFade);

            var go = new GameObject("ChurchLightShaft");
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.layer = 0;   // 光柱是光,不是世界几何(不进相机摇臂的碰撞层)

            Debug.Log($"[光照] 光柱已建:锥角 {spotAngleDeg:0}° ⇒ 墙面半径 {rWall:0.00} m / " +
                      $"地面半径 {rFloor:0.00} m(α {ApertureAlpha:0.000},加法混合)");
            return 1;
        }

        /// <summary>把落地环压到地面之上(环面垂直于光轴 ⇒ 下半圈本来会穿到地板以下)。</summary>
        private static Vector3 ClampAboveFloor(Vector3 p)
        {
            return p.y < FloorY ? new Vector3(p.x, FloorY, p.z) : p;
        }

        /// <summary>1×64 竖向渐变:v=0(墙面)不透明 → v=1(落地)全透明。</summary>
        private static Texture2D GradientTexture()
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "LightShaftGradient",
            };
            var px = new Color32[h];
            for (int i = 0; i < h; i++)
            {
                float v = i / (float)(h - 1);
                float a = Mathf.Clamp01(1f - v * v);   // 近墙最亮,落地归零
                px[i] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }
    }
}
