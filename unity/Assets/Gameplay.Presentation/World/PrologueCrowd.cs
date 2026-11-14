// 序章 · 十七个难民(呈现层,零系统)—— N2 的第一个真实动作(2026-11-14)。
//
// 权威:
//   `design/gdd/content/chapter-plan.md` §0.1(用户 2026-11-14 裁定:**17 个难民不是病人**)
//   `design/levels/prologue-beat-script.md` P1-07(「前庭出现 17 个难民」· 载体 = 呈现场景件)
//
// ═══ 边界(三条,全部来自 §0.1 的裁定)═════════════════════════════════════
//   ① **不建 `PatientState`、不写 `DiseaseOnset`** —— 人群是**在场者**,不是病人。
//      若建病人,他们就会撞上「帐篷建成前不产病人」那道剧情门(序章 P1-07 在建帐篷之前)。
//   ② **不进三流**:没有任何事件、没有任何存档痕迹 —— 承 ADR-009 派生态。
//   ③ **不带碰撞体**:玩家必须能从人群里穿过去。挡住进门走廊会把序章锁死,
//      而灰盒期的人形本来也只是一根方柱(`WorldWallGreybox.BuildMarker` 同一条取舍)。
//
// ⚠️ **格坐标是写死的字面量,不是随机散布**:随机会让「同一个存档两次读出来的场面不同」,
//   而这是玩家会反复重看的一幕。17 个位置逐格列举,便于走查与夹具复核。
//
// ⚠️ **自己建根节点,不复用 `WorldWallGreybox`** —— 后者把锚点挂进 `WorldWalls` 根,
//   而墙体实例的 `DestroyAll()` / `DestroyOrphanRoots()` 会连同人群一起抹掉。
//   两个系统的生命周期不同(墙随世界重建,人群只在 P1-07 出现一次),必须各有根。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>序章 P1-07 的十七个难民(纯视觉,零系统状态)。</summary>
    public sealed class PrologueCrowd
    {
        /// <summary>根节点名(走查与夹具按名定位)。</summary>
        public const string RootName = "PrologueCrowd";

        /// <summary>占位底材质(与病人 / 结构 / 墙体共用同一张 `Resources` 资产)。</summary>
        public const string BaseMaterialResourcePath = "PatientPlaceholderMaterial";

        /// <summary>人形尺寸(米)。灰盒期 = 一根略高于角色的方柱。</summary>
        public static readonly Vector3 FigureSize = new Vector3(0.42f, 1.68f, 0.42f);

        /// <summary>人形色调(比墙体暖、比病人冷 —— 走查时能与两者区分)。</summary>
        public static readonly Color FigureTint = new Color(0.46f, 0.42f, 0.36f);

        /// <summary>
        /// 十七个格(前庭 A2 区,`chapter-1-tent-clinic.md` §3 的 x∈[16,21] × z∈[18,25])。
        /// <para>⚠️ 刻意避开告示锚点 `(17,23)` —— 那格上已经有实物,再站一个人会互相遮挡。</para>
        /// </summary>
        public static readonly int[,] Cells =
        {
            {16,19},{18,19},{20,19},
            {17,20},{19,20},{21,20},
            {16,21},{18,21},{20,21},
            {17,22},{19,22},{21,22},
            {16,24},{18,24},{20,24},
            {17,25},{19,25},
        };

        /// <summary>人数(= <see cref="Cells"/> 的行数;改了表本值自动跟)。</summary>
        public static int Count => Cells.GetLength(0);

        private readonly List<Material> _materials = new List<Material>();
        private GameObject _root;

        /// <summary>是否已经出现过(幂等;重复调用不再建第二套)。</summary>
        public bool Built => _root != null;

        /// <summary>
        /// 建出全部难民。
        /// </summary>
        /// <returns>实际建成的人形数(材质取不到时为 0)。</returns>
        /// <remarks>
        /// ⚠️ **先清残留再建**:世界切换 / 重装配会新建一整套装配根,上一轮的实例没了簿记,
        /// 而叠出第二套人群是**静默**的(画面上只是人变密)。承 `WorldWallGreybox.DestroyOrphanRoots`
        /// 同一条纪律。
        /// </remarks>
        public int Build()
        {
            DestroyOrphans();

            Material baseMat = Resources.Load<Material>(BaseMaterialResourcePath);
            if (baseMat == null) return 0;   // 资产缺席 = 本机没美术,正常态(不抛)

            _root = new GameObject(RootName);
            int built = 0;
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Refugee_" + i.ToString("00"));
                go.AddComponent<MeshFilter>().sharedMesh = MakeBoxMesh(FigureSize);
                var mat = new Material(baseMat) { color = FigureTint };
                _materials.Add(mat);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = new Vector3(
                    Cells[i, 0] * WorldWallGreybox.MetersPerCell,
                    FigureSize.y * 0.5f,
                    Cells[i, 1] * WorldWallGreybox.MetersPerCell);
                built++;
            }
            return built;
        }

        /// <summary>销毁全部人形(幂等)。</summary>
        public void DestroyAll()
        {
            if (_root != null)
            {
                if (Application.isPlaying) Object.Destroy(_root);
                else Object.DestroyImmediate(_root);
                _root = null;
            }
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_materials[i]);
                else Object.DestroyImmediate(_materials[i]);
            }
            _materials.Clear();
        }

        /// <summary>
        /// 清掉**不属于本实例**的同名根(上一轮装配留下的人群)。
        /// <para>⚠️ 只按根名清 —— 不碰 `WorldWalls` / 结构 / 病人视觉,那些各有各的根。</para>
        /// </summary>
        private void DestroyOrphans()
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go != null && go.name == RootName && go.scene.IsValid())
                {
                    if (Application.isPlaying) Object.Destroy(go);
                    else Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>一根居中的方柱(底面贴 y = 0)。</summary>
        private static Mesh MakeBoxMesh(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var verts = new Vector3[24];
            var normals = new Vector3[24];
            var tris = new int[36];
            // 六个面:每面 4 顶点 / 2 三角形
            Vector3[] normals6 =
            {
                Vector3.right, Vector3.left, Vector3.up,
                Vector3.down, Vector3.forward, Vector3.back,
            };
            Vector3[][] faceVerts =
            {
                new[] { new Vector3(h.x,-h.y,h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,h.y,-h.z), new Vector3(h.x,h.y,h.z) },
                new[] { new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x,-h.y,h.z), new Vector3(-h.x,h.y,h.z), new Vector3(-h.x,h.y,-h.z) },
                new[] { new Vector3(-h.x,h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z) },
                new[] { new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,-h.y,h.z), new Vector3(-h.x,-h.y,h.z) },
                new[] { new Vector3(-h.x,-h.y,h.z), new Vector3(h.x,-h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z) },
                new[] { new Vector3(h.x,-h.y,-h.z), new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x,h.y,-h.z), new Vector3(h.x,h.y,-h.z) },
            };

            for (int f = 0; f < 6; f++)
            {
                int v = f * 4;
                for (int k = 0; k < 4; k++)
                {
                    verts[v + k] = faceVerts[f][k];
                    normals[v + k] = normals6[f];
                }
                int t = f * 6;
                tris[t + 0] = v + 0; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 0; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "RefugeeFigure_Mesh" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
