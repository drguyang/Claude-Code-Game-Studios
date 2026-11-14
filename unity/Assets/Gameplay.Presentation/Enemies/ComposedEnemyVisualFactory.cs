// 敌人视觉的组合体 factory(2026-11-14 · 「完整可见」轮)
//
// 权威:
//   ADR-013 §9 C3(呈现层只渲染)
//   `production/milestones/p0-sweep-2026-10-29.md:308`(「一个单色人形 proxy 灰盒即可,
//     不需要真美术资产」—— 敌人占位同款口径)
//   GDD enemy-ai.md §Visual 二①(2026-11-14 裁定:倒地野兽 = 墨色轮廓微光)
//   TD-build-03(材质必须经**资产引用**进包,不得 `Shader.Find` 现找 —— 否则 player 构建剥离)
//
// ═══ 它建什么 ═══════════════════════════════════════════════════════════
//   人形(兵痞)/ 野兽(野狗)两套 primitive 组合体,每部件:
//     · 本体:共享的 `PatientPlaceholderMaterial`(中性灰;颜色由 `MaterialPropertyBlock._BaseColor` 逐件覆盖)
//     · 轮廓:同 shader 的**运行时材质**,`_Cull = Front`(倒置外壳)⇒ 只露背面 = 墨色轮廓
//   ⚠️ 轮廓是**倒置外壳**,不是自定义 shader:URP Lit 自带 `_Cull` 材质属性,
//      设置它即可,零 Renderer Feature(ADR-023 ⑧)。缺 `_Cull` ⇒ **构造期抛**(不留品红/不静默降级)。
//
// ⚠️ 真资产兑换 = 在 `CompositionRoot` 换掉 `new ComposedEnemyVisualFactory()` 一行;
//    spawner / 位姿桥 / 读数条 / 夹具**一字不动**。
// ⚠️ **无 Collider**:敌人视觉不吃物理(与 `CapsulePatientVisualFactory` 同纪律)。
// ⚠️ **不设位置**:位置由 spawner 每帧经 `EnemyPoseRig.Apply` 写入(接口契约)。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DaYiJingCheng.Gameplay.Presentation.Enemies
{
    /// <summary>primitive 组合体敌人 factory(占位;真资产来了整体退役)。</summary>
    public sealed class ComposedEnemyVisualFactory : IEnemyVisualFactory, IDisposable
    {
        /// <summary>野狗原型 id(与 `EnemyPrototypes.WildDog` 同值;呈现侧只做「人形/野兽」二分)。</summary>
        public const int WildDogProtoId = 1;

        /// <summary>轮廓外壳相对本体的放大系数(小一点 = 细边)。</summary>
        private const float OutlineScale = 1.07f;

        // 部件色(零语义:只区分部件,不映射伤情/病种 —— 那是 9 / 8 的词表)。
        private static readonly Color ClothColor = new Color(0.36f, 0.34f, 0.30f, 1f);
        private static readonly Color SkinColor = new Color(0.62f, 0.50f, 0.40f, 1f);
        private static readonly Color BootColor = new Color(0.20f, 0.18f, 0.16f, 1f);
        private static readonly Color FurColor = new Color(0.31f, 0.27f, 0.22f, 1f);
        private static readonly Color InkColor = new Color(0.16f, 0.14f, 0.12f, 1f);

        private readonly Material _bodyMaterial;
        private readonly Material _outlineMaterial;

        /// <summary>构造并加载资产(缺资产 ⇒ 抛,见 TD-build-03)。</summary>
        public ComposedEnemyVisualFactory()
        {
            _bodyMaterial = Resources.Load<Material>("PatientPlaceholderMaterial");
            if (_bodyMaterial == null)
                throw new InvalidOperationException(
                    "[enemy-visual] 加载不到 Resources/PatientPlaceholderMaterial.mat —— " +
                    "占位材质资产缺失(它与病人占位共用)。继续下去 Unity 会走品红错误着色器。");

            _outlineMaterial = new Material(_bodyMaterial.shader)
            {
                name = "EnemySalvageInk_Runtime",
            };
            if (!_outlineMaterial.HasProperty("_Cull"))
                throw new InvalidOperationException(
                    "[enemy-visual] 占位 shader 没有 `_Cull` 属性 —— 倒置外壳无法只露背面;" +
                    "继续下去轮廓会盖住本体(视觉错误但无报错)。");
            _outlineMaterial.SetFloat("_Cull", (float)CullMode.Front);
            _outlineMaterial.SetColor("_BaseColor", InkColor);
        }

        /// <inheritdoc />
        public GameObject Create(in EnemyPose pose)
        {
            var root = new GameObject($"EnemyVisual_{pose.ActorId}");
            var rig = root.AddComponent<EnemyPoseRig>();

            var poseRoot = new GameObject("PoseRoot").transform;
            poseRoot.SetParent(root.transform, false);

            var body = new List<Renderer>(8);
            var colors = new List<Color>(8);
            var outlines = new List<Renderer>(8);

            bool isBeast = pose.ProtoId == WildDogProtoId;

            Transform torso, head, armL, armR, legL, legR, tail = null;
            if (isBeast)
            {
                torso = AddPart(poseRoot, PrimitiveType.Capsule, "Body",
                    new Vector3(0f, 0.55f, 0f), new Vector3(0.32f, 0.50f, 0.30f),
                    Quaternion.Euler(90f, 0f, 0f), FurColor, body, colors, outlines);
                head = AddPart(poseRoot, PrimitiveType.Sphere, "Head",
                    new Vector3(0f, 0.62f, 0.55f), new Vector3(0.28f, 0.28f, 0.28f),
                    Quaternion.identity, FurColor, body, colors, outlines);
                tail = AddPart(poseRoot, PrimitiveType.Capsule, "Tail",
                    new Vector3(0f, 0.62f, -0.55f), new Vector3(0.05f, 0.25f, 0.05f),
                    Quaternion.Euler(-30f, 0f, 0f), FurColor, body, colors, outlines);
                legL = AddPart(poseRoot, PrimitiveType.Capsule, "LegFL",
                    new Vector3(0.18f, 0.25f, 0.32f), new Vector3(0.08f, 0.25f, 0.08f),
                    Quaternion.identity, BootColor, body, colors, outlines);
                legR = AddPart(poseRoot, PrimitiveType.Capsule, "LegFR",
                    new Vector3(-0.18f, 0.25f, 0.32f), new Vector3(0.08f, 0.25f, 0.08f),
                    Quaternion.identity, BootColor, body, colors, outlines);
                AddPart(poseRoot, PrimitiveType.Capsule, "LegBL",
                    new Vector3(0.18f, 0.25f, -0.32f), new Vector3(0.08f, 0.25f, 0.08f),
                    Quaternion.identity, BootColor, body, colors, outlines);
                AddPart(poseRoot, PrimitiveType.Capsule, "LegBR",
                    new Vector3(-0.18f, 0.25f, -0.32f), new Vector3(0.08f, 0.25f, 0.08f),
                    Quaternion.identity, BootColor, body, colors, outlines);

                // 野兽的四条腿都归 rig 的 legL/legR 两个槽位?不 —— 只给前两条,
                // 后两条由「倒地侧翻」整体旋转带走(见 `EnemyPoseRig.ApplyStatePose`)。
                armL = null; armR = null;
            }
            else
            {
                torso = AddPart(poseRoot, PrimitiveType.Capsule, "Torso",
                    new Vector3(0f, 1.00f, 0f), new Vector3(0.42f, 0.35f, 0.28f),
                    Quaternion.identity, ClothColor, body, colors, outlines);
                head = AddPart(poseRoot, PrimitiveType.Sphere, "Head",
                    new Vector3(0f, 1.45f, 0f), new Vector3(0.26f, 0.26f, 0.26f),
                    Quaternion.identity, SkinColor, body, colors, outlines);
                armL = AddPart(poseRoot, PrimitiveType.Capsule, "ArmL",
                    new Vector3(-0.30f, 1.05f, 0f), new Vector3(0.09f, 0.28f, 0.09f),
                    Quaternion.identity, ClothColor, body, colors, outlines);
                armR = AddPart(poseRoot, PrimitiveType.Capsule, "ArmR",
                    new Vector3(0.30f, 1.05f, 0f), new Vector3(0.09f, 0.28f, 0.09f),
                    Quaternion.identity, ClothColor, body, colors, outlines);
                legL = AddPart(poseRoot, PrimitiveType.Capsule, "LegL",
                    new Vector3(-0.12f, 0.45f, 0f), new Vector3(0.11f, 0.35f, 0.11f),
                    Quaternion.identity, BootColor, body, colors, outlines);
                legR = AddPart(poseRoot, PrimitiveType.Capsule, "LegR",
                    new Vector3(0.12f, 0.45f, 0f), new Vector3(0.11f, 0.35f, 0.11f),
                    Quaternion.identity, BootColor, body, colors, outlines);
            }

            rig.Configure(isBeast, poseRoot, torso, head, armL, armR, legL, legR, tail,
                          body.ToArray(), colors.ToArray(), outlines.ToArray());
            return root;
        }

        /// <inheritdoc />
        public void Destroy(GameObject visual)
        {
            if (visual == null) return;
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(visual); return; }
            UnityEngine.Object.Destroy(visual);
        }

        /// <summary>销毁运行时轮廓材质(共享的本体材质是资产,不销毁)。</summary>
        public void Dispose()
        {
            if (_outlineMaterial != null)
            {
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(_outlineMaterial);
                else UnityEngine.Object.Destroy(_outlineMaterial);
            }
        }

        // ── primitive 装配 ────────────────────────────────────────────────
        private Transform AddPart(Transform parent, PrimitiveType type, string name,
                                  Vector3 localPos, Vector3 localScale, Quaternion localRotation,
                                  Color color, List<Renderer> body, List<Color> colors,
                                  List<Renderer> outlines)
        {
            Transform part = CreatePart(parent, type, name, localPos, localScale, localRotation,
                                        _bodyMaterial, color, body, colors);

            // 轮廓 = 同形放大一圈(倒置外壳;默认关,仅倒地野兽开)
            CreatePart(parent, type, name + "_Ink", localPos, localScale * OutlineScale, localRotation,
                       _outlineMaterial, InkColor, null, null, outlines);

            return part;
        }

        private static Transform CreatePart(Transform parent, PrimitiveType type, string name,
                                            Vector3 localPos, Vector3 localScale, Quaternion localRotation,
                                            Material material, Color color,
                                            List<Renderer> body, List<Color> colors,
                                            List<Renderer> outlines = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRotation;

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            if (outlines != null) // 轮廓件
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                outlines.Add(renderer);
            }
            else if (body != null) // 本体件
            {
                body.Add(renderer);
                colors?.Add(color);
            }

            return go.transform;
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(collider);
            else UnityEngine.Object.Destroy(collider);
        }
    }
}
