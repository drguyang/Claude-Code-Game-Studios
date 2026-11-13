// 教堂内景的**灯光装置**(2026-11-13)。运行期生成,不预摆(ADR-023 §②)。
//
// 要复现的是定稿概念图 `approved/scene-interior-a.png` 的光:
//   室外是亮的、室内是阴的;一道晨光从**东墙塌口**斜射进来,在地面与西墙上留一块亮斑;
//   室内没有直射光源,却也不至于黑 —— 那是漫反射在托底。
//
// ═══ 为什么是「一盏聚光 + 两盏补光 + 环境光」而不是一盏平行光 ═══════════
//   四壁是**朝内**的单面面片:平行光从天上打,只会照到它们的背面 ⇒ 整间屋子仍黑。
//   所以必须 ① 用环境光把底色托起来,② 用一盏聚光从塌口方向切进来做「那道晨光」,
//   ③ 用低强度、不投影的点光模拟地面/墙面的漫反射。
//
// ⚠️ **环境光是全局的**(`RenderSettings`):改了会影响室外。当前 P0 只有教堂这一带,
//   可以接受;等营地/野外也进场后,这里要改成「按区域切换的 Lighting Settings 资产」
//   (或 URP 的 Local Volume),**不得**把全局值当永久方案。
//
// ⚠️ **本版没有体积光**(光柱里的飞尘)。它要 URP 的体积光/粒子,属于后处理层,
//   不在这次范围内;先只保证「光斑落点」与明暗关系对。

using UnityEngine;
using UnityEngine.Rendering;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>教堂内景灯光(环境光 + 塌口聚光 + 两盏补光)。</summary>
    public static class ChurchLighting
    {
        // ── 环境光:低、冷灰。太高会把剥落与砖缝冲平,太低墙会糊成一片黑。
        //    🔁 2026-11-13 调低一档(0.34 → 0.17):原来的值是给「没有主光」准备的;
        //    现在有了塌口晨光,环境光必须让位,否则四处一样亮 = 没有明暗关系(概念图的反面)。
        private static readonly Color Sky = new Color(0.26f, 0.27f, 0.31f);
        private static readonly Color Equator = new Color(0.21f, 0.20f, 0.19f);
        private static readonly Color Ground = new Color(0.10f, 0.10f, 0.09f);

        // ── 塌口晨光:暖白,从东侧墙外斜射进来
        //    🔁 位置必须在**塌口正中**(z 21.5):放到 z 19 会被 1 m 厚的东墙整块挡掉,
        //    灯再亮也是一片死黑(墙是实体,灯在墙里,锥体先打在自己身上)。
        private static readonly Vector3 ShaftFrom = new Vector3(16.5f, 5.2f, 21.5f);
        private static readonly Vector3 ShaftTo = new Vector3(7.0f, 0.0f, 24.0f);
        private static readonly Color ShaftColor = new Color(1.00f, 0.93f, 0.80f);
        private const float ShaftIntensity = 900f;
        private const float ShaftRange = 70f;
        private const float ShaftAngle = 14f;

        // ── 补光:不投影,只把四壁的明度托到「看得见」,不制造第二处光斑
        private static readonly Vector3 FillFrom = new Vector3(11.0f, 3.4f, 21.5f);
        private static readonly Color FillColor = new Color(0.96f, 0.94f, 0.90f);
        private const float FillIntensity = 8f;
        private const float FillRange = 18f;

        // ── 西墙脚补光:后墙离塌口最远,只靠一盏会在墙角糊成黑
        private static readonly Vector3 CornerFill = new Vector3(7.0f, 1.8f, 21.5f);
        private const float CornerFillIntensity = 5f;
        private const float CornerFillRange = 12f;

        /// <summary>建灯并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        public static void Build(Transform parent = null)
        {
            ApplyAmbient();

            var root = new GameObject("ChurchLighting");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            MakeSpot("ShaftLight", ShaftFrom, ShaftTo, ShaftColor, ShaftIntensity, ShaftRange,
                     ShaftAngle, root.transform);
            MakePoint("FillLight", FillFrom, FillColor, FillIntensity, FillRange, root.transform);
            MakePoint("CornerFillLight", CornerFill, FillColor, CornerFillIntensity,
                      CornerFillRange, root.transform);

            // 光柱的几何由**同一对**起止点算出 —— 改灯的角度,柱子自动跟着走。
            ChurchLightShaft.Build(ShaftFrom, ShaftTo, ShaftAngle, root.transform);

            Debug.Log($"[光照] 内景灯光已建:塌口聚光 {ShaftIntensity} + 补光 ×2 + 光柱;环境光已压到 0.17/0.12/0.05");
        }

        /// <summary>环境光:三色渐变的低值。**全局副作用**,见类头注。</summary>
        private static void ApplyAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Sky;
            RenderSettings.ambientEquatorColor = Equator;
            RenderSettings.ambientGroundColor = Ground;
            RenderSettings.ambientIntensity = 1f;
            // ⚠️ URP 下**必须**调这一句:环境光是从球谐(SH)提供的,
            // 只改 RenderSettings 的颜色不会重建 SH(2026-11-13 实测:改完仍旧全黑)。
            DynamicGI.UpdateEnvironment();
        }

        private static void MakeSpot(string name, Vector3 from, Vector3 to, Color color,
                                     float intensity, float range, float angle, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            go.transform.LookAt(to);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static void MakePoint(string name, Vector3 at, Color color, float intensity,
                                      float range, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            // 补光**不投影**:投影会在墙上留下三处假影,而屋里根本没有那三个光源。
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }
    }
}
