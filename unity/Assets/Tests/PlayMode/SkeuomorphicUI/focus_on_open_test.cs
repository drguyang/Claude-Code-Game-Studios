// 开屏落焦(PlayMode · 2026-10-31)
//
// ▍病因(本轮补的可观测洞)
//   `SkeuoRuntimeDriver.OpenModal` 开屏后有一行「把焦点落到第一行」——
//   但 EditMode 夹具里 `rootVisualElement.panel == null`(合法测试形态),那行**被跳过**
//   且无法观测。已证的只有「该落焦的那一行是谁」(`FirstFocusable`),**没有**证据证明
//   `Focus()` 真的被调用过。而游戏没有指针:**没落焦 = 手柄玩家一步也走不了**。
//
// ▍判据(可证伪)
//   ① 真面板下 `OpenModal(Pause)` ⇒ 引擎 `focusController.focusedElement` **就是**第一行;
//   ② 换屏 ⇒ 焦点随屏迁移到新屏第一行(不留在旧屏、不悬空);
//   ③ 正控:开屏前无元素获焦 —— 否则 ① 无从判别。
//      变异面:删 `OpenModal` 里那两行 `first.Focus()` ⇒ ①② 全红。
//
// ⚠️ 不加 BootRoot / 不建 EventSystem —— 本门只测 driver 的开屏落焦;焦点用引擎
//   `FocusController` 真源查询(不反射、不走旁路),与 `focus_ring_applier_test` 同形态。

using System.Collections;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>开屏后焦点真的落到第一行(生产执行行,不是「该落谁」的静态断言)。</summary>
    internal sealed class focus_on_open_test
    {
        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;
        private SkeuoRuntimeDriver _driver;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null; _driver = null;
        }

        private IEnumerator SetUp()
        {
            _go = new GameObject("FocusOnOpen_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();

            // Awake 建常驻屏;等面板真正 attach(与 focus_ring_applier_test 同款超时)
            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > 5f)
                    Assert.Fail("面板 5 s 内未 attach —— 本门要的是**真面板**(panel == null 即无焦点可言)");
                yield return null;
            }
        }

        /// <summary>引擎焦点真源(`Focusable` → `VisualElement`;非元素 ⇒ null)。</summary>
        private VisualElement Focused()
            => _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;

        [UnityTest]
        public IEnumerator test_openPause_focusesFirstRow()
        {
            yield return SetUp();

            Assert.That(Focused(), Is.Null,
                "正控:开屏前不应有元素获焦 —— 否则下面那条无从判别");

            Assert.IsTrue(_driver.OpenModal(ModalId.Pause), "前提:暂停壳开得出来");
            var pause = (PauseScreen)_driver.Screen;

            Assert.That(Focused(), Is.SameAs(pause.RowAt(0)),
                "开屏后引擎焦点必须落在第一行(继续)—— 没有指针的游戏里,没落焦 = 手柄玩家走不动");
        }

        [UnityTest]
        public IEnumerator test_switchingModal_movesFocusToNewScreensFirstRow()
        {
            yield return SetUp();
            _driver.SaveSlots = new uint[] { 1u, 2u, 3u };

            _driver.OpenModal(ModalId.Pause);
            var pauseRow = ((PauseScreen)_driver.Screen).RowAt(0);
            Assert.That(Focused(), Is.SameAs(pauseRow), "前提:暂停壳已落焦");

            _driver.OpenModal(ModalId.SaveSlots);
            var saveSlots = (SaveSlotsScreen)_driver.Screen;

            Assert.That(Focused(), Is.Not.SameAs(pauseRow), "焦点不得留在**已被移除**的旧屏元素上");
            Assert.That(Focused(), Is.SameAs(saveSlots.RowAt(0)),
                "换屏后焦点必须迁到新屏第一行(载入入口)");
        }

        [UnityTest]
        public IEnumerator test_openSaveSlots_withSlots_focusesFirstRow()
        {
            yield return SetUp();

            _driver.SaveSlots = new uint[] { 1u, 2u, 3u };
            Assert.IsTrue(_driver.OpenModal(ModalId.SaveSlots));
            var saveSlots = (SaveSlotsScreen)_driver.Screen;

            Assert.That(saveSlots.SlotRowCount, Is.EqualTo(3), "前提:行已建出");
            Assert.That(Focused(), Is.SameAs(saveSlots.RowAt(0)),
                "存档位屏开屏后焦点必须落在第 0 行(载入入口)");
        }
    }
}
