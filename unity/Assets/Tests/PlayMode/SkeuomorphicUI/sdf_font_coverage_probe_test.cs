// SDF 字体覆盖回归门(PlayMode · 2026-10-13 · B-6-4)
//
// ▍根因(已结案 · 三轮实证 · **两层**):
//   ① **字体引用层**:运行期 C# 建的裸 `new Label(text)` **零类** ⇒ 无字体 ⇒ 落 Unity
//      内置默认字体(中文 fallback = 糊)。且「在共同祖先设字体靠继承下传」**真机不生效**
//      —— CSS 级联铁律:**元素上的直接声明恒压继承值**(与特异性无关),主题对文本元素
//      有直接字体规则。⇒ 修法:`SkeuoThemeVariables.uss` 用 **`.unity-text-element`**
//      (UI Toolkit 给每个 TextElement/Label/Button/TextField 自动挂的类)**直接匹配**。
//   ② **字号层(2026-10-13 用户实证)**:SDF 资产采样点 = 32px(`m_PointSize`);裸 Label
//      无字号 ⇒ 落主题默认 ≈14px **远小于采样点** ⇒ SDF 缩采样不足 = 糊。用户实验:
//      抬到 40px ⇒ 字变大且变清晰 ⇒ 根因 = 字号过小(非渲染)。定稿 28px(近采样点、
//      与 `.channel-label` 同档、已被验证清晰)。
//
// ▍本测 = **回归门**(防回退 · 双判据):断言真实脉案屏(含 `.channel-label` + 裸 Label +
//   动态文本)全部文本元素 ① 解析到 SDF 字体;② 字号 ≥24px(无障碍下限,`SkeuoThemeVariables.uss:165`)。
//   用**生产 PanelSettings**(带主题),排除主题规则压过的生产风险。
//   ⚠️ `.unity-text-element` 是**直接匹配**(非继承)⇒ batch 探针可信(继承类判据 batch 不可信,
//   承 2026-10-13 教训:nographics 不加载主题 ⇒ 继承侥幸生效 ⇒ 假绿)。

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
            // Label 字体规则在级联顺序 / 特异性上压过逐元素规则」的生产专属风险。
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

            var lib = new SkeuoElementLibrary();
            _ = new CasebookScreen(root, lib);

            yield return null;
            yield return null;

            int total = 0;
            float minFs = float.MaxValue, maxFs = 0f;
            var blurry = new List<string>();
            foreach (var el in Dfs(root))
            {
                if (!(el is Label lbl)) continue;
                total++;
                float fs = el.resolvedStyle.fontSize;
                if (fs < minFs) minFs = fs;
                if (fs > maxFs) maxFs = fs;
                string fa = ResolveFontAssetName(el);
                // 双判据:① 字体解析到 SDF(第一根因);② 字号 ≥24px 无障碍下限(第二根因
                // —— SDF 采样点 32px,字号过小则缩采样不足 = 糊;B-6-4 用户实证)。
                if (fa == null || !fa.Contains("SDF") || fs < 24f)
                    blurry.Add($"name='{el.name}' class=[{string.Join(",", el.GetClasses())}] " +
                               $"text='{Trunc(lbl.text)}' font='{fa}' fontSize={fs}");
            }

            Assert.Greater(total, 0, "脉案屏应含文本元素(前置:BuildUI 未建文本 = 夹具失效)。");
            Assert.IsEmpty(blurry,
                $"脉案屏全部文本须解析到 SDF 字体且字号 ≥24px(B-6-4 根治);以下 {blurry.Count}/{total} 个不合格:\n" +
                string.Join("\n", blurry) +
                "\n⇒ 检查:① `.unity-text-element` 规则在 SkeuoThemeVariables.uss;② 字体用 resource(\"Assets/…\");" +
                "③ 该 USS 在挂载清单内;④ 字号变量 `--skeuo-font-size-default` ≥24px。");
            Debug.Log($"[COV-GATE] 脉案屏 {total} 个文本元素全部解析到 SDF 字体且字号合格 ✓ " +
                      $"fontSize 范围 [{minFs},{maxFs}]");
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
