// 敌人世界空间读数面片(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   design/gdd/skeuomorphic-ui.md V-10(敌人读数:黄铜侧 · 蚀刻刻度)
//   production/epics/skeuomorphic-ui/story-017-enemy-vitals-bar.md
//   AC-42-F3(黄铜侧刻度合法;禁传统血条形态)
//   design/gdd/enemy-ai.md 规则二十二(值读既有 VitalsDto;42 只画材质与位置)
//
// 与 `EnemyVitalsBar` 的关系:
//   后者是**纯数据骨架**(可见性 / 六态 / 读数入口),本件是它的**世界空间渲染体**:
//   · 黄铜面片 + 一排蚀刻刻度(离散刻痕,**不是**连续填充)
//   · 档位文案由 27 的词汇表给出(42 不比较阈值)
//   · 尺寸恒定:按 `WorldBillboard.ComputeWorldScale` 逐帧求缩放(OQ-42-1 裁定甲)
//
// ⚠️ 42 不持状态:本件只保存「怎么画」的引用;值每次 `Apply` 传入,不落字段。
// ⚠️ 禁传统血条形态:刻痕是**离散的錾刻槽**,没有连续填充条。

using System;
using UnityEngine;
using UnityEngine.UI;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>敌人世界空间读数面片(黄铜侧 · 蚀刻刻度)。</summary>
    public sealed class EnemyVitalsBillboard
    {
        /// <summary>面片逻辑尺寸(像素;世界缩放按恒定屏幕尺寸求)。</summary>
        private static readonly Vector2 PanelSize = new Vector2(120f, 24f);

        /// <summary>蚀刻刻度槽数(离散;值归 V-10 的刻度政策)。</summary>
        public const int TickSlots = 9;

        private static readonly Color BrassColor = new Color(0.72f, 0.55f, 0.30f, 0.92f);
        private static readonly Color EtchOnColor = new Color(0.14f, 0.11f, 0.07f, 0.95f);
        private static readonly Color EtchOffColor = new Color(0.30f, 0.24f, 0.16f, 0.35f);
        private static readonly Color WordColor = new Color(0.10f, 0.08f, 0.06f, 1f);

        private readonly GameObject _root;
        private readonly RectTransform _rect;
        private readonly CanvasGroup _group;
        private readonly Image[] _etches;
        private readonly Text _word;
        private readonly WorldBillboard _sizing = new WorldBillboard();

        /// <param name="actorId">实体 id(仅用于命名,便于走查)。</param>
        public EnemyVitalsBillboard(int actorId)
        {
            _root = new GameObject($"EnemyVitals_{actorId}", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            _rect = (RectTransform)_root.transform;
            _rect.sizeDelta = PanelSize;
            _rect.localScale = Vector3.one * 0.005f; // 首帧前的一个合理猜测;FaceCamera 会覆盖

            _group = _root.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            Image plate = CreateImage(_root.transform, "BrassPlate", BrassColor);
            Stretch(plate.rectTransform);

            _etches = new Image[TickSlots];
            const float step = 11f;
            float startX = -(TickSlots - 1) * step / 2f;
            for (int i = 0; i < TickSlots; i++)
            {
                Image etch = CreateImage(_root.transform, $"Etch_{i}", EtchOffColor);
                etch.rectTransform.sizeDelta = new Vector2(3f, 8f);
                etch.rectTransform.anchoredPosition = new Vector2(startX + i * step, -6f);
                _etches[i] = etch;
            }

            _word = CreateWord(_root.transform);
        }

        /// <summary>写入一帧渲染数据(读即用,不落字段)。</summary>
        /// <param name="bandText">档位文案(27 的词汇;null ⇒ 空串)。</param>
        /// <param name="level">读数 0–1(来自 `VitalsDto.Position`)。</param>
        /// <param name="dimmed">是否压暗(倒地;仍必须可见 —— 归零 ≠ 已清除)。</param>
        public void Apply(string bandText, float level, bool dimmed)
        {
            if (level < 0f) level = 0f;
            if (level > 1f) level = 1f;

            int lit = Mathf.CeilToInt(level * TickSlots);
            if (lit > TickSlots) lit = TickSlots;

            for (int i = 0; i < _etches.Length; i++)
                _etches[i].color = i < lit ? EtchOnColor : EtchOffColor;

            if (_word != null) _word.text = bandText ?? string.Empty;
            if (_group != null) _group.alpha = dimmed ? 0.45f : 1f;
        }

        /// <summary>面向相机并求恒定屏幕尺寸缩放(F6 / OQ-42-1)。</summary>
        /// <param name="camera">当前渲染相机(可为 null ⇒ 保持上一帧姿态)。</param>
        public void FaceCamera(Camera camera)
        {
            if (camera == null || _root == null) return;

            Vector3 toCamera = camera.transform.position - _root.transform.position;
            if (toCamera.sqrMagnitude < 1e-6f) return;

            _root.transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);

            float distance = toCamera.magnitude;
            double worldWidth = _sizing.ComputeWorldScale(distance, camera.fieldOfView);
            if (worldWidth <= 0d) return;

            float scale = (float)(worldWidth / PanelSize.x);
            _root.transform.localScale = Vector3.one * scale;
        }

        /// <summary>世界坐标(由驱动逐帧写入)。</summary>
        public Vector3 WorldPosition
        {
            get => _root != null ? _root.transform.position : Vector3.zero;
            set { if (_root != null) _root.transform.position = value; }
        }

        /// <summary>销毁(幂等;编辑器非播放态走 immediate)。</summary>
        public void Destroy()
        {
            if (_root == null) return;
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(_root);
            else UnityEngine.Object.Destroy(_root);
        }

        // ── 构造助手 ─────────────────────────────────────────────────────
        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateWord(Transform parent)
        {
            var go = new GameObject("BandWord", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = WordColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(112f, 14f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font BuiltinFont()
        {
            // Unity 6 起 Arial 内置字体改名为 LegacyRuntime;两步都试,失败返回 null(文字不显示但不抛)。
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }
}
