// 样式表挂载回归门(PlayMode · 2026-10-13)
//
// ▍背景:SkeuoRuntimeDriver.AttachStyleSheets 原判据 `_panel.panel == null ⇒ return`
//   依赖「Awake 时 panel 已 attach」的时序假设;若 Awake 早于 UIDocument 的 panel
//   attach,则静默跳过且永不重试 ⇒ 九份 USS 全不挂 ⇒ 全糊。2026-10-13 移除该守卫
//   + 加 OnEnable 幂等补挂。
//
// ▍本测 = **回归门**:走**真实 Unity 生命周期**(inactive GO + 预置字段 + 激活),
//   断言 driver 把**清单内全部**样式表挂到 rootVisualElement.styleSheets。
//   ⚠️ 关键:走真实生命周期(非手动调 Awake)—— 才能复现 panel attach 时序。

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    internal sealed class skeuo_stylesheet_mount_probe_test
    {
        private const string ProdPanelGuid = "d96a2bf6aafc22acb9f2ccd0a87069a1";

        private static readonly string[] UssGuids =
        {
            "ca39179f4f866f2759d6b5637f07dacf", "c60e5f406296604eabc8f7deb1426dae",
            "f08453c1e4ae14fe5912c78e513b632b", "daf4b8143e153169785081783cf15bcd",
            "fd5d857ce9818cbcd9a09bc8e475688f", "beeb45a2a6af0fed5b95180638547b91",
            "89d748b243d0862129d1e6a54198bb47", "8b30b969ba891efc39a31c5a5b8e9f60",
            "11da1384c7046db87b4ed672f6c181ee",
        };

        private GameObject _go;

        [UnityTest]
        public IEnumerator test_driver_mounts_all_stylesheets_real_lifecycle()
        {
            _go = new GameObject("MountProbeHost");
            _go.SetActive(false);   // 停用 ⇒ AddComponent 不触发 Awake(模拟真机预摆)

            var prodPs = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(ProdPanelGuid));
            Assert.IsNotNull(prodPs, "生产 PanelSettings 应可加载");

            var doc = _go.AddComponent<UIDocument>();
            doc.panelSettings = prodPs;

            var driver = _go.AddComponent<SkeuoRuntimeDriver>();
            SetField(driver, "_uiDocument", doc);
            SetField(driver, "_panelAsset", prodPs);
            SetField(driver, "_screenName", "Casebook");
            var sheets = new List<StyleSheet>();
            foreach (var g in UssGuids)
                sheets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(g)));
            SetField(driver, "_styleSheets", sheets);

            _go.SetActive(true);   // 激活 ⇒ Awake/OnEnable 按真机顺序
            yield return null;
            yield return null;

            var panel = (VisualElement)typeof(SkeuoRuntimeDriver)
                .GetField("_panel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            Assert.IsNotNull(panel, "driver panel 应已建");

            int mounted = panel.styleSheets.count;
            Debug.Log($"[MOUNT] 样式表挂载数={mounted}(应={UssGuids.Length})");
            Assert.AreEqual(UssGuids.Length, mounted,
                $"driver 应把清单内全部 {UssGuids.Length} 份样式表挂到 root —— 实测 {mounted}。" +
                "少挂 ⇒ 文本回退默认字体 = 糊(AttachStyleSheets 时序守卫回归?)。");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _go = null;
        }

        private static void SetField(object o, string name, object v) =>
            o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    }
}
