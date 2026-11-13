// 正式美术件的**运行期摆放台**(2026-11-13 · 丙后续)。
//
// 权威:
//   ADR-023 §②(`World.unity` **零 gameplay GameObject** —— 预摆 = 第二真源)
//   ADR-009 §四(视觉是派生态:加载期重建,不进流)
//   `design/assets/specs/church-wall.md` 的 `asset_home: local-only`
//   `design/art/art-bible.md` §8.10.6(AI 生成 3D 政策)
//
// ═══ 为什么要有这一层 ═════════════════════════════════════════════════════
//   2026-11-12 跑通了整条 3D 管线,产出 `church-wall.fbx`(20k 面 / 2×4096 贴图 /
//   已清理闭合 / 按高 6 m 归一)。但查下来它**既不在 git、也没有被任何场景或代码引用**
//   ⇒ 这件 hero 资产到今天**在游戏里一次都没出现过**。灰盒再准,也回答不了
//   「真件的比例与观感对不对」—— 那正是本类要回答的问题。
//
// ═══ 缺席是**正常态**,不是错误 ═════════════════════════════════════════
//   资产 `local-only`(火山方舟 ToS 未定 + 仓库 PUBLIC)⇒ 别人的机器上**没有**
//   `Resources/Art/church-wall/`。那种情况下本类**跳过并打一行**,不抛、不红 ——
//   与「本次装配没装世界数据」同一条处置纪律(见 `CompositionRoot` 的结构视觉守卫)。
//   ⚠️ **不得**把它读成「美术已经接进来了」:日志里的缺席行就是判据。
//
// ═══ 摆放参数是**临时值** ════════════════════════════════════════════════
//   格坐标 / 朝向 / 缩放由下面的表给,取自**几何产物的可走格**(教堂东墙 x=15,
//   塌口在 z∈[20,23],北段 z∈[24,29])。真值的归宿是关卡工具的模块表(ADR-022),
//   本表只到「看一眼比例」为止。

using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>一件正式美术件的摆放(格坐标 + 朝向)。</summary>
    public readonly struct ArtPlacement
    {
        /// <summary>`Resources` 下的路径(不含扩展名)。</summary>
        public readonly string ResourcePath;
        /// <summary>落位格 X(件中心)。</summary>
        public readonly int CellX;
        /// <summary>落位格 Z(件中心)。</summary>
        public readonly int CellZ;
        /// <summary>绕 Y 轴旋转(度)。</summary>
        public readonly float YawDegrees;
        /// <summary>统一缩放(1 = 按资产自身米制)。</summary>
        public readonly float Scale;
        /// <summary>人读备注(只进日志)。</summary>
        public readonly string Note;

        /// <summary>覆盖半径(X 向,格;`0` = 只覆盖落位那一列)。</summary>
        public readonly int CoverHalfX;
        /// <summary>覆盖半径(Z 向,格;`0` = 只覆盖落位那一行)。</summary>
        public readonly int CoverHalfZ;

        /// <summary>构造。</summary>
        public ArtPlacement(string resourcePath, int cellX, int cellZ, float yawDegrees,
                            float scale, string note, int coverHalfX = 0, int coverHalfZ = 0)
        {
            ResourcePath = resourcePath;
            CellX = cellX;
            CellZ = cellZ;
            YawDegrees = yawDegrees;
            Scale = scale;
            Note = note;
            CoverHalfX = coverHalfX < 0 ? 0 : coverHalfX;
            CoverHalfZ = coverHalfZ < 0 ? 0 : coverHalfZ;
        }

        /// <summary>该格是否被本件覆盖(闭区间矩形)。</summary>
        public bool Covers(int cellX, int cellZ)
            => Mathf.Abs(cellX - CellX) <= CoverHalfX && Mathf.Abs(cellZ - CellZ) <= CoverHalfZ;
    }

    /// <summary>按表把正式美术件摆进世界(资源缺席时静默跳过并记一行)。</summary>
    public sealed class ArtOverlay
    {
        /// <summary>根节点名(走查按名定位)。</summary>
        public const string RootName = "ArtOverlay";

        /// <summary>
        /// 当前已接入的件。
        /// <para>🗝 教堂东墙:高 6 m × 宽 6 m × 厚 6 m(含塌落基座),枢轴在底面中心。
        /// 摆在 x=18 / z=17 / yaw 90° ⇒ 正面(w-西面)落在 x=15,正好是数据里的东墙线;
        /// 宽度沿 Z 覆盖 z∈[14,20],与塌口(z∈[20,23])南沿相接。</para>
        /// <para>⚠️ **为什么是南段不是北段**(2026-11-13 实测):首版摆 z=26(北段),
        /// 玩家出生点 (18,21) 的第三人称相机直接撞进这件 6 m 见方的块里,开局第一屏
        /// 是贴脸的砖墙。南段离出生点与告示锚点都更远,且与塌口连成一道完整的断面。</para>
        /// </summary>
        public static readonly ArtPlacement[] Placements =
        {
            // 6 m 见方 ⇒ 覆盖 x∈[15,21] / z∈[14,20];灰盒东墙 x=15 那一段整段让位。
            new ArtPlacement("Art/church-wall/church-wall", 18, 17, 90f, 1f, "教堂东墙南段",
                             coverHalfX: 3, coverHalfZ: 3),
            // 内景墙模块(2.0 宽 × 5.5 高 × 0.34 厚;厚是模型自然比例,未拉厚 —— 见 specs/church-interior.md)。
            // 正面朝 −Z ⇒ 摆在北墙 z=29、yaw 0 = 面朝室内;宽 2 m ⇒ 覆盖 x∈[8,10] 那一行。
            new ArtPlacement("Art/church-interior/wall-window", 9, 29, 0f, 1f, "内景·北墙尖拱窗段",
                             coverHalfX: 1, coverHalfZ: 0),
        };

        /// <summary>该格是否已被某件正式美术覆盖(**灰盒墙在这些格上必须让位**)。
        /// <para>⚠️ 不让位的后果是**静默**的:真件落在同一格里,画面上只是「灰盒后面好像有东西」,
        /// 而走查者会据此判「美术不好用」。2026-11-13 实测踩过(`wall-window` 被 2.6 m 灰盒挡住大半)。</para></summary>
        public static bool IsCoveredByArt(int cellX, int cellZ)
        {
            for (int i = 0; i < Placements.Length; i++)
                if (Placements[i].Covers(cellX, cellZ)) return true;
            return false;
        }

        private GameObject _root;
        private readonly ArtPlacement[] _placements;

        /// <summary>构造。</summary>
        /// <param name="placements">摆放表;**null = 用 <see cref="Placements"/>**(生产路径)。
        /// 夹具注入自造表以免依赖 27 MB 的本地资产。</param>
        public ArtOverlay(ArtPlacement[] placements = null)
        {
            _placements = placements ?? Placements;
        }

        /// <summary>最近一次成功摆放的件数。</summary>
        public int PlacedCount { get; private set; }

        /// <summary>最近一次缺席的件数(资源不在本机)。</summary>
        public int MissingCount { get; private set; }

        /// <summary>按表摆放(幂等:先清后建)。</summary>
        /// <returns>成功摆放的件数。</returns>
        public int Build()
        {
            DestroyAll();
            DestroyOrphanRoots();
            PlacedCount = 0;
            MissingCount = 0;

            for (int i = 0; i < _placements.Length; i++)
            {
                ArtPlacement p = _placements[i];
                GameObject asset = Resources.Load<GameObject>(p.ResourcePath);
                if (asset == null)
                {
                    MissingCount++;
                    Debug.Log($"[美术] 本地缺席:{p.ResourcePath}({p.Note})—— " +
                              "local-only 资产不在本机,按缺失跳过(不是接进来了)");
                    continue;
                }

                if (_root == null) _root = new GameObject(RootName);
                GameObject go = Object.Instantiate(asset, _root.transform);
                go.name = System.IO.Path.GetFileName(p.ResourcePath);
                go.transform.position = new Vector3(p.CellX, 0f, p.CellZ);
                go.transform.rotation = Quaternion.Euler(0f, p.YawDegrees, 0f);
                go.transform.localScale = Vector3.one * p.Scale;
                AddColliders(go);
                WorldWallGreybox.ApplyWorldGeometryLayer(go);   // 相机摇臂按层查,真件须同层
                PlacedCount++;
            }

            return PlacedCount;
        }

        /// <summary>
        /// 给真件补碰撞体 —— **必须做**,因为灰盒墙已经在这些格上让位了。
        /// <para>灰盒墙带 `MeshCollider` 是「看得见的墙 = 走不过去的墙」的临时实现;
        /// 真件接管该格之后若不带碰撞体,玩家就会**穿墙** —— 那是比没有墙更糟的形态
        /// (2026-11-13 实测过同款:首版灰盒只画不挡,按住 A 一路穿出西墙掉出世界)。</para>
        /// </summary>
        private static void AddColliders(GameObject instance)
        {
            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter f = filters[i];
                if (f.sharedMesh == null || f.GetComponent<MeshCollider>() != null) continue;
                f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
            }
        }

        /// <summary>销毁已摆的件(幂等)。</summary>
        public void DestroyAll()
        {
            if (_root != null)
            {
                if (Application.isPlaying) Object.Destroy(_root);
                else Object.DestroyImmediate(_root);
                _root = null;
            }
            PlacedCount = 0;
        }

        private void DestroyOrphanRoots()
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !string.Equals(all[i].name, RootName)) continue;
                if (Application.isPlaying) Object.Destroy(all[i]);
                else Object.DestroyImmediate(all[i]);
            }
        }
    }
}
