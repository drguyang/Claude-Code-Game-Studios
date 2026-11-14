// 帐篷搭建演出的**逐件生长视觉**(N3 · 2026-11-14)。
//
// 权威:
//   `design/levels/prologue-beat-script.md` P3-02b
//     (「逐件生长(五件组件依次出现)→ 镜头绕半圈 → 解冻」)
//   `docs/tech-debt-register.md` `TD-building-05`(占位灰盒即可;不要求真美术资产)
//
// ═══ 为什么是五个体块 ═══════════════════════════════════════════════════
//   帐篷的五个组件就是它的**材料清单**(门板 9 / 钉子 10 / 房梁 11 / 布 12 / 油布 13,
//   各 1 件;见 `multi_material_cost_test` 的帐篷行)。「逐件生长」若只画**一个**方块
//   变大,五件材料就退化成一件 —— 玩家看不到自己凑的东西一样一样立起来。
//   ⇒ 每件 1 组体块,**组内**可以是多个方块(四根柱子 = 房梁;四颗钉 = 钉子),
//     组的**出现顺序** = P3-02b 的动作行(房梁立起 → 门板斜靠 → 布蒙上),
//     其后接该行没点名的两件(油布 → 钉子)。
//
// ═══ 占位期形态(灰盒,不是美术)════════════════════════════════════════
//   体块排布只是「看得出来这是座帐篷」的最小几何:四角柱撑一块顶布,一块斜靠的门板,
//   一块地油布,四颗钉在柱顶。**真美术资产到位后本类整体替换**,排布不留遗产。
//
// ═══ 生长 ═══════════════════════════════════════════════════════════════
//   · 每件从**零尺寸**长到全尺寸(`smoothstep`,0.45 s),**底边贴地**
//     (按未长出的比例把中心下压,视觉上「从地里立起来」,不是从空中落下)。
//   · 推进按墙钟 `deltaTime`(补间归呈现层)⇒ 与帧率无关的**观感**;
//     「第几件该出现」的时序归 `CutscenePlayer`(tick),本类只回答「长到几分」。
//   · **播完不销毁**:它是这座帐篷在画面上的唯一载体 —— `StructureVisualSpawner`
//     只画 `baked_initial == true` 的行,运行期放置的结构没有体块。销毁只归装配层的
//     teardown(世界切换 / 退出)。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Cutscene
{
    /// <summary>帐篷五件组件的逐件生长占位体块。</summary>
    public sealed class TentAssemblyVisual : IDisposable
    {
        /// <summary>组件组数(= 帐篷五件材料)。</summary>
        public const int PieceCount = 5;

        /// <summary>根节点名(走查 / 夹具按名定位)。</summary>
        public const string RootName = "TentBuildVisual";

        /// <summary>每件从零长到全尺寸的时长(秒)。</summary>
        public const float GrowSeconds = 0.45f;

        /// <summary>一组组件(1…n 个方块,同时出现)。</summary>
        private sealed class Piece
        {
            public Transform[] Blocks;
            public Vector3[] FullScales;
            public float[] FullCenterY;
            public float Elapsed;
            public bool Revealed;

            public bool Grown => Elapsed >= GrowSeconds;
        }

        private readonly GameObject _root;
        private readonly List<Material> _materials = new List<Material>();
        private readonly Piece[] _pieces = new Piece[PieceCount];

        /// <summary>根节点(已销毁 ⇒ <c>null</c>)。</summary>
        public Transform Root => _root != null ? _root.transform : null;

        /// <summary>已出现的组数(诊断 / 夹具)。</summary>
        public int RevealedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pieces.Length; i++)
                    if (_pieces[i] != null && _pieces[i].Revealed) n++;
                return n;
            }
        }

        /// <summary>是否五件全部长完。</summary>
        public bool AllGrown
        {
            get
            {
                for (int i = 0; i < _pieces.Length; i++)
                    if (_pieces[i] == null || !_pieces[i].Grown) return false;
                return true;
            }
        }

        /// <summary>某件的生长进度(0 = 未出现,1 = 全尺寸)。</summary>
        /// <param name="piece">组下标(0 = 房梁 … 4 = 钉子)。</param>
        public float ProgressOf(int piece)
        {
            Piece p = _pieces[piece];
            if (p == null || !p.Revealed) return 0f;
            return Mathf.Clamp01(p.Elapsed / GrowSeconds);
        }

        /// <summary>构造五组体块(初始全为零尺寸,一件未出现)。</summary>
        /// <param name="focus">帐篷锚点格的世界坐标(格线交角;y = 地面高度)。</param>
        /// <param name="baseMaterial">底材质(<c>null</c> = 不造任何体块;演出仍可播,只是看不见)。</param>
        public TentAssemblyVisual(Vector3 focus, Material baseMaterial)
        {
            _root = new GameObject(RootName);
            _root.transform.position = focus;
            if (baseMaterial == null) return;   // 无材质 ⇒ 纯计时(不凭空造半套形状)

            // ⓪ 房梁 —— 四根角柱(「四根柱子」是 P3-02b 的原文)
            _pieces[0] = BuildPiece(0, baseMaterial, new Color(0.42f, 0.30f, 0.18f), Vector3.zero,
                (new Vector3(-0.95f, 1.00f, -0.95f), new Vector3(0.16f, 2.0f, 0.16f)),
                (new Vector3(0.95f, 1.00f, -0.95f), new Vector3(0.16f, 2.0f, 0.16f)),
                (new Vector3(-0.95f, 1.00f, 0.95f), new Vector3(0.16f, 2.0f, 0.16f)),
                (new Vector3(0.95f, 1.00f, 0.95f), new Vector3(0.16f, 2.0f, 0.16f)));

            // ① 门板 —— 斜靠在前面的板(绕 x 轴后仰)
            _pieces[1] = BuildPiece(1, baseMaterial, new Color(0.55f, 0.42f, 0.26f),
                new Vector3(-14f, 0f, 0f),
                (new Vector3(0f, 0.95f, 1.02f), new Vector3(1.90f, 1.80f, 0.08f)));

            // ② 布 —— 顶篷
            _pieces[2] = BuildPiece(2, baseMaterial, new Color(0.78f, 0.72f, 0.58f), Vector3.zero,
                (new Vector3(0f, 2.04f, 0f), new Vector3(2.40f, 0.08f, 2.40f)));

            // ③ 油布 —— 地布(压在最底下)
            _pieces[3] = BuildPiece(3, baseMaterial, new Color(0.32f, 0.36f, 0.26f), Vector3.zero,
                (new Vector3(0f, 0.03f, 0f), new Vector3(3.00f, 0.05f, 3.00f)));

            // ④ 钉子 —— 四颗,钉在柱顶
            _pieces[4] = BuildPiece(4, baseMaterial, new Color(0.35f, 0.35f, 0.38f), Vector3.zero,
                (new Vector3(-0.95f, 2.06f, -0.95f), new Vector3(0.09f, 0.09f, 0.09f)),
                (new Vector3(0.95f, 2.06f, -0.95f), new Vector3(0.09f, 0.09f, 0.09f)),
                (new Vector3(-0.95f, 2.06f, 0.95f), new Vector3(0.09f, 0.09f, 0.09f)),
                (new Vector3(0.95f, 2.06f, 0.95f), new Vector3(0.09f, 0.09f, 0.09f)));

            // 初始全零尺寸(未出现的组在画面上不存在)
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i] != null) ApplyGrowth(_pieces[i], 0f);
        }

        /// <summary>让第 <paramref name="piece"/> 组开始生长(幂等:重复调用不重置)。</summary>
        /// <param name="piece">组下标(0…<see cref="PieceCount"/>−1)。</param>
        public void Reveal(int piece)
        {
            if (piece < 0 || piece >= _pieces.Length) return;
            Piece p = _pieces[piece];
            if (p == null || p.Revealed) return;
            p.Revealed = true;
            p.Elapsed = 0f;
        }

        /// <summary>推进一帧(墙钟秒)。全部长完 ⇒ 纯 no-op(可以每帧无条件调)。</summary>
        /// <param name="deltaSeconds">本帧时长(秒;≤ 0 不推进)。</param>
        public void Step(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            for (int i = 0; i < _pieces.Length; i++)
            {
                Piece p = _pieces[i];
                if (p == null || !p.Revealed || p.Grown) continue;
                p.Elapsed += deltaSeconds;
                float t = Mathf.Clamp01(p.Elapsed / GrowSeconds);
                ApplyGrowth(p, t * t * (3f - 2f * t));   // smoothstep
            }
        }

        /// <summary>销毁全部体块与材质(幂等;双路径同全仓纪律)。</summary>
        public void Dispose()
        {
            for (int i = 0; i < _pieces.Length; i++)
                _pieces[i] = null;

            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(_materials[i]);
                else UnityEngine.Object.DestroyImmediate(_materials[i]);
            }
            _materials.Clear();

            if (_root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }

        private Piece BuildPiece(int pieceIndex, Material baseMaterial, Color tint, Vector3 euler,
                                 params (Vector3 Position, Vector3 Scale)[] blocks)
        {
            var piece = new Piece
            {
                Blocks = new Transform[blocks.Length],
                FullScales = new Vector3[blocks.Length],
                FullCenterY = new float[blocks.Length],
            };

            for (int i = 0; i < blocks.Length; i++)
            {
                var material = new Material(baseMaterial) { color = tint };
                _materials.Add(material);

                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"{RootName}_Piece{pieceIndex}_{i}";
                var collider = box.GetComponent<Collider>();
                if (collider != null)
                {
                    if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(collider);
                    else UnityEngine.Object.Destroy(collider);
                }

                box.transform.SetParent(_root.transform, false);
                box.transform.localPosition = blocks[i].Position;
                box.transform.localEulerAngles = euler;
                box.GetComponent<Renderer>().sharedMaterial = material;

                piece.Blocks[i] = box.transform;
                piece.FullScales[i] = blocks[i].Scale;
                piece.FullCenterY[i] = blocks[i].Position.y;
            }

            return piece;
        }

        /// <summary>按进度 <paramref name="t"/>(0…1)设尺寸;底边贴地(中心下压未长出的部分)。</summary>
        private static void ApplyGrowth(Piece piece, float t)
        {
            for (int i = 0; i < piece.Blocks.Length; i++)
            {
                Vector3 scale = piece.FullScales[i] * Mathf.Max(t, 0.0001f);   // 0 尺寸会让包裹盒退化
                Vector3 position = piece.Blocks[i].localPosition;
                position.y = piece.FullCenterY[i] - (piece.FullScales[i].y - scale.y) * 0.5f;
                piece.Blocks[i].localScale = scale;
                piece.Blocks[i].localPosition = position;
            }
        }
    }
}
