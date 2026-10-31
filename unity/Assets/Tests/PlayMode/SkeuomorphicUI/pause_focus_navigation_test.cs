// AC-PS-02 / AC-PS-03 —— 暂停壳的**焦点落点与模态内导航**(机器判 · 2026-10-31)
//
// ▍为什么要有这个文件
//   走查三张截图**判不了**焦点:静态图里浅色纸带、世界物体和焦点环混在一起,
//   我上一轮据此报过「焦点逃出模态」,又用量测撤回 —— 那正说明**截图不是判据**。
//   本文件把 `pause-shell-42.md` 的两条 AC 改成机器判:
//     · AC-PS-02 开屏后焦点落在「继续」
//     · AC-PS-03 方向键在**两行之间**移动,且**不逃出模态**
//
// ▍驱动的是真链(照 u1_assump6_gamepad_chain_test 的配方)
//   虚拟 Gamepad → `InputSystem_Actions` 的 `UI/Navigate` → 生产 `EventSystem` 的
//   `InputSystemUIInputModule` → `PanelEventHandler.OnMove`(**public**)→
//   `m_Panel.SendEvent(NavigationMoveEvent)` → `FocusController` 默认动作阶段 → 焦点真的移动。
//   ⚠️ 不用 `VisualElement.SendEvent`(它只跑用户回调阶段,不触默认动作,见该文件头注)。
//
// ▍本文件的边界(先声明,防借绿)
//   · **无样式表**:`.ruled` 的 `min-height` 来自 USS,测试里不加载样式 ⇒ 行会塌成零高,
//     而**几何导航需要非零 worldBound** ⇒ 本文件给两行显式尺寸(见 `GiveRowsGeometry`)。
//     ⚠️ 这只替代布局,不改被测逻辑;但它也意味着「真样式下的行高」不在本判据面内。
//   · **不是手感**:移动是否跟手、焦点环好不好看,都不在面内(归人工腿)。
//   · **不是渲染**:`-nographics` 下没有像素,焦点环「看不看得见」不在面内。

using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>暂停壳:落焦(AC-PS-02)与模态内导航(AC-PS-03)。</summary>
    [TestFixture]
    public sealed class PauseFocusNavigationTest
    {
        private const float RootWaitSeconds = 3f;
        private const float CarrierWaitSeconds = 3f;
        private const float RowHeight = 44f;
        private const float RowWidth = 320f;

        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;
        private SkeuoRuntimeDriver _driver;
        private VisualElement _root;

        private GameObject _bootGo;
        private EventSystem _es;
        private PanelEventHandler _handler;
        private Gamepad _pad;
        private VisualElement _outsider;      // 模态外的可聚焦元素(模拟急救候选列表条目)

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_pad != null && _pad.deviceId != InputDevice.InvalidDeviceId &&
                InputApi.GetDevice<Gamepad>() != null)
                InputApi.RemoveDevice(_pad);
            _pad = null;

            _handler = null;
            if (_bootGo != null) Object.Destroy(_bootGo);
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _bootGo = null; _doc = null; _panelSettings = null; _go = null;
            _driver = null; _root = null; _outsider = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-PS-02 · 开屏落焦
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_acps02_openPause_focusLandsOnResumeRow()
        {
            yield return Assemble();

            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause);
            yield return null;

            VisualElement focused = Focused();
            Assert.That(focused, Is.SameAs(pause.RowAt(0)),
                "AC-PS-02:开屏后引擎焦点必须落在第一行「继续」—— 没有指针的游戏里,没落焦 = 手柄玩家走不动");
            Assert.That(((Label)pause.RowAt(0).Q<Label>()).text, Is.EqualTo("继续"),
                "第一行文案 = 继续(`pause-shell-42.md` §5)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-PS-03 · 模态内导航(两行之间),且不逃出模态
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_acps03_arrowDown_thenUp_movesBetweenTheTwoRows()
        {
            yield return Assemble();
            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause);
            yield return null;
            Assert.That(Focused(), Is.SameAs(pause.RowAt(0)), "[夹具前提] 起点在「继续」");

            yield return Press(new Vector2(0f, -1f));      // 下

            Assert.That(Focused(), Is.SameAs(pause.RowAt(1)),
                "AC-PS-03:方向键下 ⇒ 焦点到「存档位」。落不到 = 两行不在同一条导航链上");

            yield return Press(new Vector2(0f, 1f));       // 上

            Assert.That(Focused(), Is.SameAs(pause.RowAt(0)),
                "AC-PS-03:方向键上 ⇒ 焦点回「继续」");
        }

        [UnityTest]
        public IEnumerator test_acps03_focusNeverEscapesToFocusableOutsideTheModal()
        {
            // ★ 本条的靶 = **焦点单栈门**(ADR-013 §五)。面板下方放一个可聚焦的「外来元素」
            //   (模拟急救候选列表条目),连按多次下键 —— 焦点**不得**落到它身上。
            //   变异面:停用 `ApplyModalFocusScope` ⇒ 本测红(这就是「焦点逃出模态」的机器判)。
            yield return Assemble();

            _outsider = new VisualElement { name = "outside-focusable" };
            _outsider.focusable = true;
            _outsider.style.position = Position.Absolute;
            _outsider.style.left = 0f;
            _outsider.style.top = 200f;
            _outsider.style.width = RowWidth;
            _outsider.style.height = RowHeight;
            _root.Add(_outsider);
            yield return null;

            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause);
            yield return null;

            Assert.That(_outsider.focusable, Is.False,
                "模态开 ⇒ 模态外的可聚焦元素必须被摘掉焦点能力(焦点单栈门的实现面)");

            for (int i = 0; i < 4; i++)
            {
                yield return Press(new Vector2(0f, -1f));
                Assert.That(Focused(), Is.Not.SameAs(_outsider),
                    $"第 {i + 1} 次下键后焦点落到了模态外的元素上 —— ADR-013 §五「焦点单栈门」破了");
                Assert.That(Focused(), Is.SameAs(pause.RowAt(0)).Or.SameAs(pause.RowAt(1)),
                    $"第 {i + 1} 次下键后焦点离开了暂停壳的两行");
            }

            _driver.CloseModal();
            yield return null;

            Assert.That(_outsider.focusable, Is.True,
                "关屏 ⇒ 模态外元素的可聚焦能力必须还原(否则那条 UI 以后再也不能用手柄选中)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装置
        // ══════════════════════════════════════════════════════════════════

        private IEnumerator Assemble()
        {
            _go = new GameObject("PauseFocusNav_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitSeconds)
                {
                    Assert.Fail("[夹具红] 面板 3s 未就绪 —— panelSettings 未生效");
                    yield break;
                }
                yield return null;
            }
            _root = _doc.rootVisualElement;

            // 生产方法建官方桥(不跑 BootRoot.Start:它的启动序含 Addressables / world 加载)
            _bootGo = new GameObject("BootRoot_PauseFocusNav");
            var boot = _bootGo.AddComponent<DaYiJingCheng.Gameplay.Boot.BootRoot>();
            boot.enabled = false;
            _es = typeof(DaYiJingCheng.Gameplay.Boot.BootRoot)
                .GetMethod("EnsureEventSystem", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(boot, null) as EventSystem;
            Assert.IsNotNull(_es, "[夹具红] EnsureEventSystem 不可用或返回 null");

            GameObject carrier = null;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < CarrierWaitSeconds)
            {
                carrier = (_root.panel as IRuntimePanel)?.selectableGameObject;
                if (carrier != null) break;
                yield return null;
            }
            Assert.IsNotNull(carrier, "[夹具红] 面板载体 3s 未就绪(interop bridge 未建 GO)");
            _handler = carrier.GetComponent<PanelEventHandler>();
            Assert.IsNotNull(_handler, "[夹具红] 载体上没有 PanelEventHandler —— 导航事件无处落地");
            _es.SetSelectedGameObject(carrier);

            if (_pad == null)
            {
                _pad = InputApi.GetDevice<Gamepad>() ?? InputApi.AddDevice<Gamepad>();
            }
            yield return null;
            yield return null;   // 让布局算出 worldBound(几何序依赖它)
        }

        /// <summary>
        /// 给两行**显式尺寸**。
        /// <para>⚠️ 测试不加载 USS ⇒ `.ruled` 的 `min-height` 不生效 ⇒ 行会塌成零高,
        /// 而几何导航需要非零 `worldBound`。故此处补上布局;这不是「改被测逻辑」,
        /// 但「真样式下的行高」不在本判据面内(见文件头注)。</para>
        /// </summary>
        private static void GiveRowsGeometry(PauseScreen pause)
        {
            for (int i = 0; i < pause.RowCount; i++)
            {
                VisualElement row = pause.RowAt(i);
                row.style.width = RowWidth;
                row.style.height = RowHeight;
            }
        }

        private IEnumerator Press(Vector2 v)
        {
            InputApi.QueueStateEvent(_pad, new GamepadState { leftStick = v });
            yield return null;
            yield return null;

            InputApi.QueueStateEvent(_pad, new GamepadState { leftStick = Vector2.zero });
            yield return null;
            yield return null;
        }

        private VisualElement Focused()
            => _root?.panel?.focusController?.focusedElement as VisualElement;
    }
}
