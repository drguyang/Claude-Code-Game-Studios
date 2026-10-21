// SDF 字体解析路径回归门(PlayMode · 2026-10-13)
//
// ▍历史(根因诊断,已结案):TextCore 类型资产生成后,UI Toolkit 仍报
//   `MissingAssetReference` + `.channel-label` 解析为 `<default>`。二选一:
//   A. **USS 的字体引用写法对该字体没解析成功**(语法/机制问题)⇒ 改 USS;
//   B. **资产本身被 UI Toolkit 拒绝**(类型/导入/内容问题)⇒ 改资产。
//   ⇒ 本探针以三条路径对照裁决:**A 成立** —— `-unity-font-definition` 吃
//   `resource("Assets/…")`,**不吃 `url("guid:…")`**(对字体恒不解析)。全库 10 处已改。
//
// ▍本测现为**回归门**(诊断后固化,防语法回退):
//   ① 直读资产类型(证明它以 TextCore.Text.FontAsset 加载);
//   ② **绕开 USS**,C# 直接 `FontDefinition.FromSDFFont(资产)` 赋给 label.style ⇒
//      读回 resolvedStyle。② 绿 = 资产本身可用(排除 B);
//   ③ **真实 USS** `SkeuoPaper.uss` 的 `.channel-label` ⇒ 须解析到 SDF 资产。
//      ③ 绿 = `resource(...)` 写法生效;若有人改回 `url(guid:…)` ⇒ ③ 红(防回退)。

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    internal sealed class sdf_font_render_path_probe_test
    {
        private const string SdfAssetGuid = "6bfef9a30546ce4618445ebd7e0be0f5";
        private const string RealPanelGuid = "d96a2bf6aafc22acb9f2ccd0a87069a1";

        private GameObject _docGo;
        private UIDocument _doc;
        private PanelSettings _panel;

        [UnityTest]
        public IEnumerator test_directFontDefinitionAssignment_vs_uss()
        {
            // ⚠️ **player 里不可跑**(2026-10-21 立):本测全程经 `UnityEditor.AssetDatabase`
            // 按 GUID / 路径取资产 —— 该命名空间**在 player 里不存在**(编译期 CS0234)。
            // 本装配 `PlayMode.asmdef` 的 `includePlatforms: []` ⇒ 进 player ⇒ 无守卫时
            // **整个 IL2CPP player 构建失败**(2026-10-21 实测 `Build Finished, Result: Failure.`
            // + 48 errors,全部指向本文件与同目录三个姊妹件)。
            // 处置 = 方法级守卫:编辑器内行为**逐字不变**;player 里编成 Inconclusive 分支。
            // ⚠️ **这不是「player 里也验过了」** —— player 侧本测**未运行**,如实记 NOT-RUN。
#if !UNITY_EDITOR
            Assert.Inconclusive(
                "[NOT-RUN in player] 本测依赖 UnityEditor.AssetDatabase(按 GUID 取 PanelSettings / " +
                "FontAsset / StyleSheet),该命名空间在 player 里不存在 ⇒ 结构上不可运行。" +
                "编辑器中照常运行,结果不因本守卫改变。");
            yield break;
#else
            string sdfPath = UnityEditor.AssetDatabase.GUIDToAssetPath(SdfAssetGuid);

            // ① 资产类型
            var textCoreFa = UnityEditor.AssetDatabase.LoadAssetAtPath(sdfPath, typeof(FontAsset)) as FontAsset;
            var tmpFa = UnityEditor.AssetDatabase.LoadAssetAtPath(sdfPath,
                Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro"));
            Debug.Log($"[SDF-PROBE] 资产路径 = {sdfPath}");
            Debug.Log($"[SDF-PROBE] ① 以 TextCore.Text.FontAsset 加载 = {(textCoreFa != null ? "成功" : "失败")}");
            Debug.Log($"[SDF-PROBE] ① 以 TMPro.TMP_FontAsset 加载 = {(tmpFa != null ? "成功(说明仍是 TMP 类型)" : "失败(已非 TMP)")}");

            var realPs = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(RealPanelGuid));
            var go = new GameObject("SdfPathProbe_UnderTest");
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            if (realPs != null) { ps.themeStyleSheet = realPs.themeStyleSheet; ps.textSettings = realPs.textSettings; }
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            _docGo = go; _doc = doc; _panel = ps;

            float t0 = Time.realtimeSinceStartup;
            while (doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 3f) { Assert.Fail("[夹具红] root 未就绪"); yield break; }
                yield return null;
            }
            var root = doc.rootVisualElement;

            // ② 绕开 USS:C# 直接赋 FontDefinition.FromSDFFont
            var direct = new Label("脉案") { name = "probe-direct" };
            if (textCoreFa != null)
                direct.style.unityFontDefinition = FontDefinition.FromSDFFont(textCoreFa);
            root.Add(direct);

            // ③ 真实 USS(已改 resource 语法)的 .channel-label 解析
            var ussPaper = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss");
            if (ussPaper != null) root.styleSheets.Add(ussPaper);
            var classed = new Label("脉案") { name = "probe-classed" };
            classed.AddToClassList("channel-label");
            root.Add(classed);

            yield return null;
            yield return null;

            string directFa = ResolveFontAssetName(direct);
            string classedFa = ResolveFontAssetName(classed);
            Debug.Log($"[SDF-PROBE] ② C# FromSDFFont                     ⇒ {directFa}");
            Debug.Log($"[SDF-PROBE] ③ 真实 USS `.channel-label`(resource)⇒ {classedFa}");

            Assert.IsTrue(directFa != null && directFa.Contains("SDF"),
                $"[P-②] C# 直赋读回 = '{directFa}'(应含 SDF ⇒ 资产可用)");
            Assert.IsTrue(classedFa != null && classedFa.Contains("SDF"),
                $"[P-③] 真实 USS `.channel-label` 应解析到 SDF 资产 —— 实测 '{classedFa}'。" +
                "成功 = resource() 语法生效,糊的根因已修。");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) UnityEngine.Object.Destroy(_doc);
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            if (_docGo != null) UnityEngine.Object.Destroy(_docGo);
            _doc = null; _panel = null; _docGo = null;
        }

        private static string ResolveFontAssetName(VisualElement el)
        {
            FontDefinition fd = el.resolvedStyle.unityFontDefinition;
            if (fd.fontAsset != null) return fd.fontAsset.name;
            if (fd.font != null) return "Font:" + fd.font.name;
            return "<default: 未定义 unityFontDefinition>";
        }
    }
}