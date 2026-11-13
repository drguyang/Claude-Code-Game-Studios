// 教堂内景的**地坪**(2026-11-13)。运行期生成,不预摆(ADR-023 §②)。
//
// 权威:`design/assets/specs/church-interior.md` §8.2
//       概念图 `approved/scene-interior-a.png` —— 地面是夯土 + 碎砖 + 光斑,不是一块纯色板。
//
// ═══ 为什么是「盖一层」而不是改灰盒 ═══════════════════════════════════════
//   灰盒地坪(`WorldWallGreybox.BuildFloor`)是**世界几何派生物**,按格铺、按格裁,
//   它负责「读得懂哪里是屋内地」;本节要的是**一块连续的土**。
//   ⇒ 在灰盒上盖一张 10 × 14 m 的单面片,比改灰盒的逐格拼法简单且不动世界层语义。
//   ⚠️ 抬升 1 cm(`Lift` 0.02 → 0.03):两层共面会 z-fighting;代价是脚底比可见地面低 3 cm,
//   与灰盒期已有的 2 cm 是同一量级,肉眼不可见。
//
// ⚠️ **不在世界几何层(Layer 8)**:那一层是相机摇臂的碰撞掩码(`CamCollideMask`),
//   地坪挂上去会把摇臂在地面附近顶住。灰盒地坪 likewise 留在 Default。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景地坪(夯土 + 碎砖,单张面片)。</summary>
    public static class InteriorFloor
    {
        /// <summary>`Resources` 下的地坪贴图路径(不含扩展名)。</summary>
        public const string AlbedoPath = "Art/church-interior/floor_dirt";

        /// <summary>贴图覆盖的真实边长(米)。</summary>
        public const float TileMeters = 2f;

        /// <summary>面片抬升(米)。高于灰盒地坪的 0.02。</summary>
        public const float Lift = 0.03f;

        private const float X0 = 5f, X1 = 15f, Z0 = 15f, Z1 = 29f;

        /// <summary>生成地坪并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        /// <returns>1 = 已生成;0 = 贴图缺席(维持灰盒地坪)。</returns>
        public static int Build(Transform parent = null)
        {
            Texture2D albedo = Resources.Load<Texture2D>(AlbedoPath);
            if (albedo == null)
            {
                Debug.Log($"[美术] 本地缺席:{AlbedoPath}(内景地坪)—— 地面维持灰盒纯色(不是接进来了)");
                return 0;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.Log("[美术] 内景地坪跳过:找不到 URP Lit shader");
                return 0;
            }

            var mat = new Material(shader) { name = "InteriorFloor_Mat" };
            mat.SetTexture("_BaseMap", albedo);
            // 压暗一档:概念图里地面**比墙面深**,而贴图原值偏亮(均值 132)⇒ 不压的话
            // 地面会把整间屋子往上抬一档,墙上的剥落反而看不见了。
            mat.SetColor("_BaseColor", new Color(0.70f, 0.68f, 0.63f));
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);

            float s = 1f / TileMeters;
            var mesh = new Mesh { name = "InteriorFloorMesh" };
            mesh.SetVertices(new[]
            {
                new Vector3(X0, Lift, Z0),
                new Vector3(X1, Lift, Z0),
                new Vector3(X1, Lift, Z1),
                new Vector3(X0, Lift, Z1),
            });
            // UV 用世界米数:两块地坪(现在是这一块)纹理尺度一致,不随房间尺寸拉伸。
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2((X1 - X0) * s, 0f),
                new Vector2((X1 - X0) * s, (Z1 - Z0) * s),
                new Vector2(0f, (Z1 - Z0) * s),
            });
            // 俯视逆时针 ⇒ 法线 +Y(与灰盒地坪同一绕序)。
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("InteriorFloor");
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;

            Debug.Log($"[美术] 内景地坪已生成:{(X1 - X0):0}×{(Z1 - Z0):0} m,贴图 {TileMeters:0} m 一铺");
            return 1;
        }
    }
}
