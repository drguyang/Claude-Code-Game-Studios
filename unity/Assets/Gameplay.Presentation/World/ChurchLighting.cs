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
        private static readonly Color Sky = new Color(0.035f, 0.037f, 0.042f);
        private static readonly Color Equator = new Color(0.027f, 0.026f, 0.024f);
        // ⚠️ 2026-11-13 由 0.025 提到 0.075:望板/椽子的**下表面朝下**,吃的是 ground 项 ——
        // 0.025 让整个屋顶读成一块黑,而概念图里屋顶是中间调(≈L100)。这不是几何问题。
        private static readonly Color Ground = new Color(0.030f, 0.029f, 0.027f);

        // ── 塌口晨光:暖白,从东侧墙外斜射进来
        //    🔁 位置必须在**塌口正中**(z 21.5):放到 z 19 会被 1 m 厚的东墙整块挡掉,
        //    灯再亮也是一片死黑(墙是实体,灯在墙里,锥体先打在自己身上)。
        private static readonly Vector3 ShaftFrom = new Vector3(16.5f, 5.2f, 21.5f);
        private static readonly Vector3 ShaftTo = new Vector3(7.0f, 0.0f, 24.0f);
        private static readonly Color ShaftColor = new Color(1.00f, 0.93f, 0.80f);
        private const float ShaftIntensity = 2600f;
        private const float ShaftRange = 70f;
        private const float ShaftAngle = 14f;

        // ── 西墙窗光(2026-11-13 · §8.11):概念图那束光就是从左墙的窗进来的
        //    位置在墙外(x=1.8),光线穿过窗洞落在屋内地面上。
        //    ⚠️ 必须**对准窗洞**:按 (1.8, 3.6, 18.5) → (9.0, 0.4, 20.0) 复算,
        //      过 x=5 时落在 y=2.18 / z=19.17,在窗洞(z 17.6..19.4 / y 1.10..2.90)之内。
        private static readonly Vector3 WindowFrom = new Vector3(1.8f, 3.6f, 18.5f);
        private static readonly Vector3 WindowTo = new Vector3(9.0f, 0.4f, 20.0f);
        private const float WindowIntensity = 520f;
        private const float WindowRange = 40f;
        private const float WindowAngle = 42f;

        // ── 北墙拱窗光(2026-11-13 · §8.12):第二扇窗,补一组**交叉光**
        //    ⚠️ 对准窗洞复算:从 (10.5, 3.0, 12.2) 到 (10.5, 0.6, 19.0),
        //      过 z=15.02(墙面)时 t=0.415 ⇒ y=3.0−0.415×2.4 = 2.00,
        //      在洞口(y 1.20..3.20)之内,x=10.5 也在(9.7..11.3)之内。
        private static readonly Vector3 NorthWindowFrom = new Vector3(10.5f, 3.0f, 12.2f);
        private static readonly Vector3 NorthWindowTo = new Vector3(10.5f, 0.6f, 19.0f);
        private const float NorthWindowIntensity = 700f;
        private const float NorthWindowRange = 40f;
        private const float NorthWindowAngle = 40f;

        // ── 补光:不投影,只把四壁的明度托到「看得见」,不制造第二处光斑
        private static readonly Vector3 FillFrom = new Vector3(11.0f, 3.4f, 21.5f);
        private static readonly Color FillColor = new Color(0.96f, 0.94f, 0.90f);
        private const float FillIntensity = 1.5f;
        private const float FillRange = 18f;

        // ── 西墙脚补光:后墙离塌口最远,只靠一盏会在墙角糊成黑
        private static readonly Vector3 CornerFill = new Vector3(7.0f, 1.8f, 21.5f);
        private const float CornerFillIntensity = 1.0f;
        private const float CornerFillRange = 12f;

        // ── 屋顶反光(2026-11-13):望板与椽子的**下表面朝下**,拿不到任何方向光,
        //    只吃 ground 环境项 ⇒ 整个屋顶读成一块黑,占了画面上三分之一。
        //    这不是几何问题,是缺一盏"地面把光弹回去"的托底灯。
        private static readonly Vector3 RoofBounceFrom = new Vector3(10f, 4.55f, 22.0f);
        private static readonly Color RoofBounceColor = new Color(0.95f, 0.94f, 0.92f);
        private const float RoofBounceIntensity = 7f;
        private const float RoofBounceRange = 26f;

        /// <summary>建灯并挂到场景里。</summary>
        /// <param name="parent">父节点(可为 null)。</param>
        public static void Build(Transform parent = null)
        {
            ApplySky();
            ApplyAmbient();

            var root = new GameObject("ChurchLighting");
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            MakeSpot("ShaftLight", ShaftFrom, ShaftTo, ShaftColor, ShaftIntensity, ShaftRange,
                     ShaftAngle, root.transform);
            MakePoint("FillLight", FillFrom, FillColor, FillIntensity, FillRange, root.transform);
            MakePoint("CornerFillLight", CornerFill, FillColor, CornerFillIntensity,
                      CornerFillRange, root.transform);
            MakeSpot("WindowLight", WindowFrom, WindowTo, ShaftColor, WindowIntensity,
                     WindowRange, WindowAngle, root.transform);
            MakeSpot("NorthWindowLight", NorthWindowFrom, NorthWindowTo, ShaftColor,
                     NorthWindowIntensity, NorthWindowRange, NorthWindowAngle, root.transform);
            MakePoint("RoofBounceLight", RoofBounceFrom, RoofBounceColor,
                      RoofBounceIntensity, RoofBounceRange, root.transform);

            // 光柱的几何由**同一对**起止点算出 —— 改灯的角度,柱子自动跟着走。
            ChurchLightShaft.Build(ShaftFrom, ShaftTo, ShaftAngle, root.transform);

            Debug.Log($"[光照] 内景灯光已建:塌口聚光 {ShaftIntensity} + 补光 ×2 + 光柱" +
                      $" + 窗光 ×2(西 {WindowIntensity} / 北 {NorthWindowIntensity});" +
                      "环境光 0.035/0.027/0.030");
        }

        /// <summary>天空:亮的薄雾暖白。**全局副作用**,见类头注。
        /// <para>病因(2026-11-13 实测):默认 procedural 天空是**冷蓝 + 深色地面**,
        /// 而内景的几个洞口(西窗 / 拱窗 / 屋顶塌落)都直接看天 —— 于是画面的最亮处
        /// 是一块灰蓝,窗外还能看见一条地平线。概念图里洞口后面是**过曝的白**,
        /// 所以这里把天空调成薄雾暖白、地面色也提亮(不留地平线)。</para></summary>
        private static void ApplySky()
        {
            // ⚠️ **不用天空盒**(2026-11-13 实测两轮):`Skybox/Procedural` 在 batch 渲染里
            //    换成高曝光薄雾参数之后仍然渲成**纯黑**(云层没被剔除、材质也设上了,
            //    日志可见 —— 但画面上洞里是黑的)。既然这几处必须「白得发亮」,
            //    就直接把天空**换成相机底色**:可控、无 shader、无 cubemap 依赖。
            //    底色见 <see cref="SkyBackground"/>;相机侧由 `BootRoot` 设为 SolidColor。
            RenderSettings.skybox = null;
        }

        /// <summary>相机底色(线性 HDR,> 1 由 ACES 压回白)。室内几处洞口看到的就是它。</summary>
        public static readonly Color SkyBackground = new Color(2.20f, 2.10f, 1.98f);

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
