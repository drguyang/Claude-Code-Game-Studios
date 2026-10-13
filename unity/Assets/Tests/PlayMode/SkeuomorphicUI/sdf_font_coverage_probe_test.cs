// SDF 字体覆盖回归门(PlayMode · 2026-10-13 · B-6-4)
//
// ▍根因(已结案):运行期 C# 建的裸 `new Label(text)` **零类** ⇒ 落 Unity 默认主题
//   字体(中文 fallback = 糊);只有挂 `.channel-label` 的题签清晰。实测真实脉案屏
//   13 个 Label:7 清晰(挂类)/ 6 糊(裸)。用户所报「6 行清晰、其他糊」即此。
//
// ▍修法:UI Toolkit 的 `-unity-font-definition` **可继承** ⇒ 在 panel root 挂
//   `.skeuo-root`(SkeuoRuntimeDriver.EnsureDocument),该类在 SkeuoThemeVariables.uss
//   设 SDF 字体,全体后代继承(一处生效,新文本自动覆盖)。
//
// ▍本测 = **回归门**(防三类回退):
//   ① 字体语法回退(url(guid:…) 对字体恒不解析 ⇒ 糊);
//   ② `.skeuo-root` 规则被删 / 字体资产失联;
//   ③ 根类未挂(继承链断 ⇒ 裸文本回退默认字体)。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    internal sealed class sdf_font_coverage_probe_test
    {
        private static readonly string[] UssPaths =
        {
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoMarks.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoImplement.uss",
        };

        private GameObject _go;

        [UnityTest]
        public IEnumerator test_casebook_all_labels_resolve_to_sdf()
        {
            _go = new GameObject("CovProbeHost");
            var doc = _go.AddComponent<UIDocument>();
            // 用**生产 PanelSettings**(带 UnityDefaultRuntimeTheme)—— 排除「主题的
            // Label 字体规则在级联顺序 / 特异性上压过 .skeuo-root」的生产专属风险。
            // 主题资产路径 = Boot.unity 的 m_PanelSettings 引用(d96a2bf6…)。
            var prodPs = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
            Assert.IsNotNull(prodPs, "生产 PanelSettings 应可加载(前置:资产路径漂移即红)。");
            doc.panelSettings = prodPs;

            float t0 = Time.realtimeSinceStartup;
            while (doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 3f) { Assert.Fail("root 未就绪"); yield break; }
                yield return null;
            }
            var root = doc.rootVisualElement;
            foreach (var p in UssPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(p);
                Assert.IsNotNull(sheet, $"USS 应可加载: {p}");
                root.styleSheets.Add(sheet);
            }

            // 复现生产修法路径:根级默认字体类(承 SkeuoRuntimeDriver.SkeuoRootClass)
            root.AddToClassList(SkeuoRuntimeDriver.SkeuoRootClass);

            var lib = new SkeuoElementLibrary();
            _ = new CasebookScreen(root, lib);

            yield return null;
            yield return null;

            int total = 0;
            var blurry = new List<string>();
            foreach (var el in Dfs(root))
            {
                if (!(el is Label lbl)) continue;
                total++;
                string fa = ResolveFontAssetName(el);
                if (fa == null || !fa.Contains("SDF"))
                    blurry.Add($"name='{el.name}' class=[{string.Join(",", el.GetClasses())}] " +
                               $"text='{Trunc(lbl.text)}' font='{fa}'");
            }

            Assert.Greater(total, 0, "脉案屏应含文本元素(前置:BuildUI 未建文本 = 夹具失效)。");
            Assert.IsEmpty(blurry,
                $"脉案屏全部文本须解析到 SDF 字体(B-6-4 根治);以下 {blurry.Count}/{total} 个回退默认字体:\n" +
                string.Join("\n", blurry) +
                "\n⇒ 检查:① `.skeuo-root` 规则在 SkeuoThemeVariables.uss;② 字体用 resource(\"Assets/…\");" +
                "③ 根类已挂(SkeuoRuntimeDriver.EnsureDocument)。");
            Debug.Log($"[COV-GATE] 脉案屏 {total} 个文本元素全部解析到 SDF 字体 ✓");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _go = null;
        }

        private static IEnumerable<VisualElement> Dfs(VisualElement el)
        {
            yield return el;
            foreach (var c in el.Children())
                foreach (var d in Dfs(c))
                    yield return d;
        }

        private static string ResolveFontAssetName(VisualElement el)
        {
            FontDefinition fd = el.resolvedStyle.unityFontDefinition;
            if (fd.fontAsset != null) return fd.fontAsset.name;
            if (fd.font != null) return "Font:" + fd.font.name;
            return "<default>";
        }

        private static string Trunc(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > 8 ? s.Substring(0, 8) + "…" : s);
    }
}
