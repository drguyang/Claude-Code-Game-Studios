// 端点 spike 探针(EditMode · 2026-10-13)—— UI Toolkit 文字渲染路径的**实证**回答。
//
// 背景:脉案「字糊」根治需把 otf(动态字体)转 TMP SDF FontAsset。转是 Editor 工具的事
// (`Editor.Tools.Font.SdfFontAssetTool`),本探针**不测工具**,测的是**岔路判据**:
//   UI Toolkit 的 PanelSettings 吃 FontAsset 时,文字渲染走哪条路径 / 用哪个 shader?
// 这个答案决定「补 TMP runtime shader」是否有必要 ——
//   · 若 UI Toolkit 用自己的 SDF shader(PanelSettings.m_SDFShader = builtin 19011),
//     则 TMP shader 缺失只挡**生成**,不挡**渲染** ⇒ 补 shader 仅让 CreateFontAsset 跑完;
//   · 若 UI Toolkit 复用 FontAsset.material(TMP shader),则 shader 是**渲染必经** ⇒ 必须补。
//
// 已知硬事实(2026-10-13 实证,非推断):
//   · com.unity.ugui 2.0.0 包内 **零** `TextMeshPro/Mobile/Distance Field` shader 资产
//     —— 全盘仅 TMP 自己两处源码文本引用;GraphicsSettings Always Included 亦无 TMP 项;
//   · 该 shader 由 `ShaderUtilities.ShaderRef_MobileSDF`(运行时 null)承载,
//     `TMP_FontAsset.CreateFontAssetInstance` 建 material 时读它 ⇒ 抛
//     "Value cannot be null. Parameter name: shader"。
//
// ⚠️ 本探针的诚实边界:EditMode batch 宿主**无渲染帧**(ScreenCapture 抛异常、PNG 零字节
// —— S3 已实测)。故本探针只取**可静态/反射确证的事实**(shader 存在性 / 属性面 /
// 引擎版本),像素级「哪个 shader 画出字」须人工 Spike 台验证,结论逐条标 [实测]/[待人工]。

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    /// <summary>UI Toolkit 文字渲染路径端点 spike(静态事实 + 反射面)。</summary>
    internal sealed class font_rendering_path_spike_test
    {
        private const string TmpMobileSdfShaderName = "TextMeshPro/Mobile/Distance Field";

        // ──────────────────────────────────────────────────────────────────────
        // ① PanelSettings 的 SDF shader 引用(UI Toolkit 自己的文字 shader)
        // ──────────────────────────────────────────────────────────────────────
        [Test]
        public void spike_a_panelSettings_sdfShader_reference()
        {
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            var t = typeof(PanelSettings);

            // m_SDFShader 是私有序列化字段 ⇒ 反射读(公开 API 无 getter)
            var f = t.GetField("m_SDFShader",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, "PanelSettings.m_SDFShader 字段应存在(反射面)");
            var shader = f?.GetValue(ps) as Shader;

            UnityEngine.Debug.Log(
                $"[SPIKE-A] PanelSettings.m_SDFShader = {(shader == null ? "null" : shader.name)}");

            // [实测] 该字段非 null ⇒ UI Toolkit 自带文字 SDF shader 引用(不依赖 TMP 的)
            Assert.IsNotNull(shader,
                "[SPIKE-A] PanelSettings 应自带 SDF shader —— 若为 null,UI Toolkit 文字渲染" +
                "将无从取 shader(本轮待证:该引用是否被 FontAsset 路径复用)");
        }

        // ──────────────────────────────────────────────────────────────────────
        // ② TMP Mobile SDF shader 在**本工程运行期**的可达性
        // ──────────────────────────────────────────────────────────────────────
        [Test]
        public void spike_b_tmpMobileSdfShader_availability()
        {
            var s = Shader.Find(TmpMobileSdfShaderName);
            UnityEngine.Debug.Log(
                $"[SPIKE-B] Shader.Find(\"{TmpMobileSdfShaderName}\") = {(s == null ? "null" : s.name)}");

            // [实测] 记录**事实**:null = `CreateFontAssetInstance` 必抛 shader NRE。
            // 断言方向 = 把「引擎包内容」钉成可证伪判据:若未来 Unity 版本补回该 shader,
            // 本断言红 ⇒ 那正是「可去掉补 shader 权宜」的信号,不是误报。
            Assert.IsNull(s,
                "[SPIKE-B] com.unity.ugui 2.0.0(Unity 6.3.24f1)运行期**不含**该 shader —— " +
                "若本条红了,说明该版本已随包发布 TMP mobile SDF shader," +
                "则 SdfFontAssetTool 的兜底(见 C 组)可简化为直接依赖引擎。");
        }

        // ──────────────────────────────────────────────────────────────────────
        // ③ FontAsset 侧 material/shader 依赖面(有没有替代入口可绕开 TMP shader)
        // ──────────────────────────────────────────────────────────────────────
        [Test]
        public void spike_c_fontAssetMaterial_surface()
        {
            // TMP 程序集(运行时,非 Editor)—— FontAsset 类型在 Unity.TextMeshPro.dll
            var tmp = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Unity.TextMeshPro");
            Assert.IsNotNull(tmp, "[SPIKE-C] Unity.TextMeshPro 程序集应已加载(ugui 2.0.0 传递依赖)");

            Type fa = tmp?.GetType("TMPro.TMP_FontAsset");
            Type settings = tmp?.GetType("TMPro.TMP_Settings");

            UnityEngine.Debug.Log(
                $"[SPIKE-C] TMP_FontAsset = {fa?.FullName ?? "(null)"}; TMP_Settings = {settings?.FullName ?? "(null)"}");

            if (fa != null)
            {
                foreach (var p in fa.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                   .Where(p => p.Name.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0
                                             || p.Name.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0))
                    UnityEngine.Debug.Log($"[SPIKE-C]   TMP_FontAsset.{p.Name} : {p.PropertyType.Name} " +
                                          $"(canRead={p.CanRead}, canWrite={p.CanWrite})");

                // atlas 尺寸/填充/采样面(编辑期可设 ⇒ 不靠 CreateFontAsset 默认值)
                foreach (var p in fa.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                   .Where(p => p.Name.IndexOf("atlas", StringComparison.OrdinalIgnoreCase) >= 0))
                    UnityEngine.Debug.Log($"[SPIKE-C]   TMP_FontAsset.{p.Name} : {p.PropertyType.Name}");
            }

            if (settings != null)
            {
                foreach (var p in settings.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                          .Where(p => p.Name.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0))
                    UnityEngine.Debug.Log($"[SPIKE-C]   TMP_Settings.{p.Name}(static) : {p.PropertyType.Name}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // ④ [待人工] 像素路径判据的**登记**(不改判据,只声明边界)
        // ──────────────────────────────────────────────────────────────────────
        [Test]
        public void spike_d_pixelPath_boundary_declaration()
        {
            // batch 宿主的硬边界(2026-10-13 实测:S3 三轮;ScreenCapture 抛异常、PNG 零字节)
            int w = Screen.width, h = Screen.height;
            UnityEngine.Debug.Log($"[SPIKE-D] batch 宿主 Screen = {w}×{h}(无有效帧 ⇒ 无像素证据)");

            Assert.Pass(
                "[SPIKE-D] 无像素级断言(禁借绿):「UI Toolkit 渲染 FontAsset 时实际调用哪个 shader」" +
                "须人工 Spike 台(Frame Debugger 抓一次绘制,看 bound shader)或在真机 Play 复测。" +
                "本探针只交付 A/B/C 三条**静态可证**事实。");
        }
    }
}
