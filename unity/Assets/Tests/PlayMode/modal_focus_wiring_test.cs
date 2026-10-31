// 开屏落焦 · **装配级**夹具(2026-11-01)—— 从 `BootRoot` 的装配出发证明「开屏真的落焦」
//
// ▍病根(与 `modal_gate_wiring_test` 同一类)
//   `focus_on_open_test` 已证明 `SkeuoRuntimeDriver.OpenModal` 会落焦 —— 但它是**直建 driver**:
//   测试自己 `AddComponent<SkeuoRuntimeDriver>()`、自己接面板。
//   生产的形状不是这样:UI 壳在场景里**预摆**(`Boot.unity` 的 `SkeuoUI`),
//   `BootRoot.FindOrCreateSkeuoUi` 把它**按类型认领**后再注入 Source / 命令面 / 开合入口。
//   ⇒ 「认领之后开屏还落不落焦」「换屏 / 重开后焦点去哪」在生产装配下**没有证据**。
//
// ▍形态(生产形状)
//   预摆 UI 壳(UIDocument + PanelSettings + SkeuoRuntimeDriver)→ **再**起 `BootRoot`
//   ⇒ 走 `FindOrCreateSkeuoUi` 的「认领已有壳」分支(与 `Boot.unity` 同一条)。
//   判据读**引擎真源** `panel.focusController.focusedElement`,不反射、不看自陈位。
//
// ▍判据(可证伪)
//   A `pauseOpen_landsFocusOnFirstRow`:开屏前无元素获焦 → `OpenModal(Pause)` → 焦点 = `RowAt(0)`
//      变异面:删 `OpenModal` 里 `first.Focus()` 那行 ⇒ 本条红。
//   B `closeThenReopen_landsFocusAgain`:开 → 关(焦点清空)→ 再开(又落在第一行)
//      ⇒ 证明落焦不是**一次性**的(第二次开屏同样生效),且关屏不留悬空焦点。
//
// ▍⚠️ 本夹具**不测**:按键开屏(Escape 入口)、方向键换行(`pause_focus_navigation_test`)、
//   输入抑制(`modal_gate_wiring_test`)。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>开屏落焦的**装配级**判据(预摆 UI 壳 + 真启动序认领)。</summary>
    public sealed class modal_focus_wiring_test
    {
        private const float BootTimeoutSeconds = 30f;
        private const float PanelTimeoutSeconds = 10f;

        private GameObject _root;
        private GameObject _uiGo;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private SkeuoRuntimeDriver _driver;
        private BootRoot _boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid = false;
            if (_boot != null)
            {
                try
                {
                    var f = typeof(BootRoot).GetField("_worldLoadHandle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    handle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
                    valid = handle.IsValid();
                }
                catch (Exception)
                {
                    valid = false;
                }
            }

            if (valid)
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone) yield return null;
            }

            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_uiGo != null) UnityEngine.Object.Destroy(_uiGo);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            // ⚠️ 兜底:按类型清残留驱动(`FindOrCreateSkeuoUi` 会**按类型复用** ——
            //    留一只下来,下一条用例就会认领到上一轮的壳,连同它的模态状态)。
            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(
                         FindObjectsSortMode.None))
            {
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);
            }

            _doc = null; _panelSettings = null; _uiGo = null; _driver = null; _boot = null; _root = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_productionAssembly_pauseOpen_landsFocusOnFirstRow()
        {
            yield return BootWithPreplacedUi();

            Assert.That(Focused(), Is.Null,
                "正控:开屏前不应有元素获焦 —— 否则下面那条无从判别");
            Assert.IsFalse(_driver.BlocksPlay, "[前置] 开局不该有挡路模态");

            Assert.IsTrue(_driver.OpenModal(ModalId.Pause), "生产驱动应能打开暂停壳");
            var pause = _driver.Screen as PauseScreen;
            Assert.IsNotNull(pause, "开屏后 Screen 应为 PauseScreen");
            Assert.IsTrue(_driver.BlocksPlay, "暂停壳应处于挡路态");

            Assert.That(Focused(), Is.SameAs(pause.RowAt(0)),
                "开屏后引擎焦点必须落在第一行(继续)—— 没有指针的游戏里,没落焦 = 手柄玩家走不动;" +
                "本夹具走的是 `BootRoot` 认领预摆壳后的**生产**路径,不是测试直建 driver");
        }

        [UnityTest]
        public IEnumerator test_productionAssembly_closeThenReopen_landsFocusAgain()
        {
            yield return BootWithPreplacedUi();

            _driver.OpenModal(ModalId.Pause);
            var pause1 = _driver.Screen as PauseScreen;
            Assert.That(Focused(), Is.SameAs(pause1.RowAt(0)), "前提:首次开屏已落焦");

            _driver.CloseModal();
            yield return null;
            Assert.IsFalse(_driver.BlocksPlay, "关屏后不应再挡路");
            Assert.That(Focused(), Is.Null,
                "关屏后不得留悬空焦点(旧屏元素已被移除)");

            _driver.OpenModal(ModalId.Pause);
            var pause2 = _driver.Screen as PauseScreen;
            Assert.That(Focused(), Is.SameAs(pause2.RowAt(0)),
                "**第二次**开屏同样必须落焦 —— 落焦不是一次性动作");
            Assert.That(Focused(), Is.Not.SameAs(pause1.RowAt(0)),
                "第二次开屏是新屏实例 ⇒ 焦点应落在新行上(不是旧屏残留)");
        }

        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 预摆 UI 壳 —— **再**起 BootRoot。`Boot.unity` 的生产形状就是「壳在场景里,根来认领」,
        /// 而 `BootRoot.FindOrCreateSkeuoUi` 走 `FindFirstObjectByType` 的已有分支。
        /// </summary>
        private IEnumerator BootWithPreplacedUi()
        {
            _uiGo = new GameObject("SkeuoUI");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _uiGo.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _uiGo.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > PanelTimeoutSeconds)
                    Assert.Fail($"{PanelTimeoutSeconds}s 内面板未 attach —— 本门要的是**真面板**");
                yield return null;
            }

            _root = new GameObject("BootRoot_ModalFocusWiring");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), $"启动序须在 {BootTimeoutSeconds}s 内完成");

            var adopted = typeof(BootRoot).GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot) as SkeuoRuntimeDriver;
            Assert.IsNotNull(adopted, "BootRoot._uiDriver 应已装配");
            Assert.That(adopted, Is.SameAs(_driver),
                "BootRoot 必须**认领**预摆的 UI 壳(FindOrCreateSkeuoUi 的已有分支)—— " +
                "否则本夹具测的是另一只壳,不是生产形状");

            // 让 UI 绑定 / 命令面注入跑完
            for (int i = 0; i < 10; i++) yield return null;
        }

        /// <summary>引擎焦点真源(`Focusable` → `VisualElement`;非元素 ⇒ null)。</summary>
        private VisualElement Focused()
            => _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;

        private bool ReadBooted()
        {
            var f = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }
    }
}
