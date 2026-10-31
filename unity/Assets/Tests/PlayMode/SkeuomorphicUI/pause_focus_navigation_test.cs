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
//   · **装置侧改了两条 InputSystem 设置**(`Assemble()` 末尾,`TearDown` 还原):这两条都是
//     **编辑器 Play 模式独有**的策略,真机构建里根本不存在 ⇒ 改它们是把夹具还原成真机语义,
//     不是绕过被测行为。具体哪两条、为什么必需,见 `Assemble()` 里那段注释。
//
// ▍覆盖(machine-judged)
//   AC-PS-02 落焦「继续」· AC-PS-03a/03c 两行间移动 + 不逃出模态 · AC-PS-04 开合同键
//   (手柄 start 两半)· AC-PS-05 「存档位」→ 换屏且只留一个容器 · AC-PS-06 模态期
//   E/F 被拒 + 移动轴折零(via 生产 `ModalGateStateAdapter` / `ModalInputFreeze`)
//   · AC-PS-09 全手柄 + 全键盘各走完「开壳 → 存档位 → 返回」。

using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
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
        private Keyboard _kb;
        private bool _padAdded;
        private bool _settingsOverridden;
        private InputSettings.EditorInputBehaviorInPlayMode _savedEditorInputBehavior;
        private InputSettings.BackgroundBehavior _savedBackgroundBehavior;
        private bool _kbAdded;
        private bool _kbReenabled;
        private VisualElement _outsider;      // 模态外的可聚焦元素(模拟急救候选列表条目)

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // ⚠️ 只移除**我们自己加**的设备。无差别 `RemoveDevice` 会把编辑器自带的系统键盘/手柄
            //   从 `InputSystem` 里摘掉 —— 那不光影响后续测试,还会让 `Keyboard.current` 在
            //    重建后短暂悬空(本文件首跑即栽在这上面,见 `PumpToggleKey` 的 remarks)。
            if (_padAdded && _pad != null && _pad.deviceId != InputDevice.InvalidDeviceId)
                InputApi.RemoveDevice(_pad);
            if (_kbAdded && _kb != null && _kb.deviceId != InputDevice.InvalidDeviceId)
                InputApi.RemoveDevice(_kb);
            if (_kbReenabled && _kb != null) InputApi.DisableDevice(_kb);
            if (_settingsOverridden && InputApi.settings != null)
            {
                InputApi.settings.editorInputBehaviorInPlayMode = _savedEditorInputBehavior;
                InputApi.settings.backgroundBehavior = _savedBackgroundBehavior;
                _settingsOverridden = false;
            }
            _pad = null; _kb = null; _padAdded = false; _kbAdded = false; _kbReenabled = false;

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
        // AC-PS-04 · 开合同键(真键路径:手柄 start → ModalInputDriver → 42)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_acps04_startOpens_resumeCloses_thenStartReopens()
        {
            yield return Assemble();
            // 生产装配的开关链(不含 BootRoot.Update 那一行调用 —— 本装置不起 BootRoot):
            // 真键读面 → 边沿驱动 → 42 的 OpenModal/CloseModal
            var toggle = new ModalInputDriver(
                DaYiJingCheng.Gameplay.Boot.PlayerInputReader.PauseTogglePressed,
                new DriverModalOpener(_driver));
            _driver.PauseSink = new DriverPauseCommandSink(_driver);

            // ⚠️ 必须**每帧轮询**:生产里 `BootRoot.Update` 每帧调一次 `OnFrame`,`OnFrame` 读的是
            //    「此刻按住没有」。若像按键注射那样「按下→松手→再问」,问的时候早松了 ⇒ 读 false。
            yield return PumpToggle(toggle, GamepadButton.Start);

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause),
                "AC-PS-04:按 start ⇒ 暂停壳出现(开合同键的第一半)");
            var pause = _driver.Screen as PauseScreen;
            Assert.IsNotNull(pause);
            GiveRowsGeometry(pause);
            yield return null;

            // 「继续」= 行 0,焦点应已在它上面(开屏落焦)· 用真 Submit 触发
            Assert.That(Focused(), Is.SameAs(pause.RowAt(0)), "[夹具前提] 焦点在「继续」");
            yield return PressButton(GamepadButton.South);

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None),
                "AC-PS-04:「继续」被确认 ⇒ 回世界(壳合上)");

            // 再按一次 = 再次打开(开合同键的**第二半** —— 边沿化必须能被重复触发)
            yield return PumpToggle(toggle, GamepadButton.Start);

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause),
                "AC-PS-04:关掉之后再按 start ⇒ 壳必须能再开(否则玩家一关就再也打不开)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-PS-05 · 「存档位」行 ⇒ 换屏,且同一时刻只有一个模态容器
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_acps05_submitOnSaveSlotsRow_switchesScreen_leavingOneContainer()
        {
            yield return Assemble();
            _driver.SaveSlots = new uint[] { 1u, 2u, 3u };
            _driver.PauseSink = new DriverPauseCommandSink(_driver);

            _driver.OpenModal(ModalId.Pause);
            var pause = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause);
            yield return null;

            yield return Press(new Vector2(0f, -1f));      // 焦点 → 「存档位」
            Assert.That(Focused(), Is.SameAs(pause.RowAt(1)), "[夹具前提] 焦点在「存档位」");

            yield return PressButton(GamepadButton.South);

            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots),
                "AC-PS-05:确认「存档位」⇒ 换到存档位屏");
            Assert.That(_driver.Screen, Is.TypeOf<SaveSlotsScreen>());

            int containers = 0;
            foreach (VisualElement child in _root.Children())
                if (child.name != null && child.name.StartsWith("modal-")) containers++;

            Assert.That(containers, Is.EqualTo(1),
                "换屏后只允许剩一个模态容器 —— 两个 = 两个屏同时摊开,「焦点单栈」的结构前提破了");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-PS-06 · 模态期输入不生效(三个消费点共用同一读面)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_acps06_modalOpen_gateRejectsAndMovementIsFrozen()
        {
            yield return Assemble();
            _driver.OpenModal(ModalId.Pause);
            yield return null;

            // 42 的模态真源 → 4 侧的只读投影(生产用的是同一个 `ModalGateStateAdapter`)
            var state = new DaYiJingCheng.Gameplay.Boot.ModalGateStateAdapter(() => _driver);
            var gate = new DaYiJingCheng.Gameplay.Interaction.ModalGate(new NotArmed(), state);

            Assert.That(gate.Accept(DaYiJingCheng.Gameplay.Interaction.InteractIntent.None), Is.False,
                "AC-PS-06:壳开着 ⇒ E 查体 / F 急救的意图必须被丢掉(规则七方向②)");

            var freeze = new DaYiJingCheng.Gameplay.Boot.ModalInputFreeze(() => state.IsOpen);
            Assert.That(freeze.FilterMovement(new Vector2(0.8f, -0.6f)), Is.EqualTo(Vector2.zero),
                "AC-PS-06:壳开着 ⇒ 移动轴折零(暂停时还能走 = 「暂停」名不副实)");
            Assert.That(freeze.IsBlocked, Is.True,
                "AC-PS-06:建造四动作消费的正是这个布尔(其分支判据在 build_controller_test)");

            _driver.CloseModal();
            yield return null;

            Assert.That(gate.Accept(DaYiJingCheng.Gameplay.Interaction.InteractIntent.None), Is.True,
                "AC-PS-06:合壳 ⇒ 立刻放行(否则玩家回不到世界)");
            Assert.That(freeze.FilterMovement(new Vector2(0.8f, -0.6f)), Is.Not.EqualTo(Vector2.zero),
                "AC-PS-06:合壳 ⇒ 移动轴恢复");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-PS-09 · 全手柄 / 全键盘都能走完「开壳 → 存档位 → 返回」
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_acps09_fullLoop_gamepadOnly()
        {
            yield return Assemble();
            _driver.SaveSlots = new uint[] { 1u, 2u, 3u };
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            var toggle = new ModalInputDriver(
                DaYiJingCheng.Gameplay.Boot.PlayerInputReader.PauseTogglePressed,
                new DriverModalOpener(_driver));

            yield return PumpToggle(toggle, GamepadButton.Start);
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause), "① start 开壳");
            var pause = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause);
            yield return null;

            yield return Press(new Vector2(0f, -1f));                  // 左摇杆下
            Assert.That(Focused(), Is.SameAs(pause.RowAt(1)), "② 摇杆移到「存档位」");

            yield return PressButton(GamepadButton.South);             // 南键确认
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots), "③ 南键进存档位屏");

            yield return PumpToggle(toggle, GamepadButton.Start);      // 再按 start = 回暂停壳
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause),
                "④ 再按 start ⇒ 回暂停壳(OQ-PS-1 的现口径)");

            var pause2 = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause2);
            yield return null;
            yield return PressButton(GamepadButton.South);             // 「继续」
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None),
                "⑤ 确认「继续」⇒ 回世界。全程只用**手柄**,一次键鼠都没碰");
        }

        [UnityTest]
        public IEnumerator test_acps09_fullLoop_keyboardOnly()
        {
            yield return Assemble();
            _driver.SaveSlots = new uint[] { 1u, 2u, 3u };
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            var toggle = new ModalInputDriver(
                DaYiJingCheng.Gameplay.Boot.PlayerInputReader.PauseTogglePressed,
                new DriverModalOpener(_driver));

            yield return PumpToggleKey(toggle, Key.Escape);
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause), "① Escape 开壳");
            var pause = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause);
            yield return null;

            yield return PressKey(Key.DownArrow);
            Assert.That(Focused(), Is.SameAs(pause.RowAt(1)), "② 方向键下移到「存档位」");

            yield return PressKey(Key.Enter);
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots), "③ Enter 进存档位屏");

            yield return PumpToggleKey(toggle, Key.Escape);
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause), "④ Escape ⇒ 回暂停壳");

            var pause2 = (PauseScreen)_driver.Screen;
            GiveRowsGeometry(pause2);
            yield return null;
            yield return PressKey(Key.Enter);
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None),
                "⑤ Enter 确认「继续」⇒ 回世界。全程只用**键盘**");
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

            // 【实验】把两条「编辑器独有」的输入策略调成真机语义(teardown 还原):
            //   ① editorInputBehaviorInPlayMode:编辑器默认只把**键盘/指针**喂给有焦点的 Game View;
            //      batch 进程无焦点 ⇒ 键盘状态事件在普通帧里被丢。
            //   ② backgroundBehavior:默认在失去焦点时**复位并停用**非后台设备。
            if (!_settingsOverridden)
            {
                _savedEditorInputBehavior = InputApi.settings.editorInputBehaviorInPlayMode;
                _savedBackgroundBehavior = InputApi.settings.backgroundBehavior;
                _settingsOverridden = true;
            }
            InputApi.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputApi.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            if (_pad == null)
            {
                _pad = InputApi.GetDevice<Gamepad>();
                if (_pad == null) { _pad = InputApi.AddDevice<Gamepad>(); _padAdded = true; }
            }
            if (_kb == null)
            {
                _kb = InputApi.GetDevice<Keyboard>();
                if (_kb == null) { _kb = InputApi.AddDevice<Keyboard>(); _kbAdded = true; }
            }
            // 兜底:上面已把 `backgroundBehavior` 调成 `IgnoreFocus`,正常不会再出现停用态;
            // 但设备若在更早的用例里被停用过,这里显式启用一次(跑完仅还原**我们**启用的那台)。
            if (!_pad.enabled) InputApi.EnableDevice(_pad);
            if (!_kb.enabled) { InputApi.EnableDevice(_kb); _kbReenabled = true; }
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

        /// <summary>按住 → 每帧轮询 → 松开 → 每帧轮询(仿真生产 `Update` 的边沿语义)。</summary>
        private IEnumerator PumpToggle(ModalInputDriver toggle, GamepadButton button)
        {
            InputApi.QueueStateEvent(_pad, new GamepadState { buttons = (ushort)(1 << (int)button) });
            for (int i = 0; i < 3; i++) { yield return null; toggle.OnFrame(); }
            InputApi.QueueStateEvent(_pad, new GamepadState());
            for (int i = 0; i < 3; i++) { yield return null; toggle.OnFrame(); }
        }

        /// <summary>同上,键盘版(与手柄同构 —— 前提是 `Assemble()` 已把输入策略调成真机语义)。</summary>
        /// <remarks>
        /// <para>⚠️ 本条能成立**全靠 `Assemble()` 里那两条设置改写**(`editorInputBehaviorInPlayMode`
        /// + `backgroundBehavior`)。不改时:键盘状态事件在普通帧里被编辑器丢掉
        /// (`escape/e/f/enter/downArrow/space` 六键全读回 false,`anyKey` 亦 false),
        /// **手柄不受影响** —— 这就是 PS-02..06 手柄腿一直绿、而键盘腿首跑即红的原因。</para>
        /// <para>置位走 <c>KeyboardState.Press(Key)</c> 实例方法,**不用** <c>new KeyboardState(Key)</c>
        /// 构造器(同机实测该构造器键位不可靠,见 `movement_virtual_key_test.cs:152-158`)。</para>
        /// </remarks>
        private IEnumerator PumpToggleKey(ModalInputDriver toggle, Key key)
        {
            var down = new KeyboardState();
            down.Press(key);
            InputApi.QueueStateEvent(_kb, down);
            bool readDuringHold = false;
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                readDuringHold |= PlayerInputReader.PauseTogglePressed();
                toggle.OnFrame();
            }
            Assert.That(readDuringHold, Is.True,
                $"[夹具前提] 注入 {key} 后 `PauseTogglePressed()` 一次都没读回 true —— " +
                $"这是**注入没生效**,不是开关逻辑坏了。_kb={Describe(_kb)} · " +
                $"Keyboard.current={Describe(Keyboard.current)} · Gamepad.current={Describe(Gamepad.current)}");
            InputApi.QueueStateEvent(_kb, new KeyboardState());
            for (int i = 0; i < 3; i++) { yield return null; toggle.OnFrame(); }
        }

        /// <summary>键盘按下一次(供 UI 模块的 Navigate / Submit 用)。</summary>
        private IEnumerator PressKey(Key key)
        {
            var down = new KeyboardState();
            down.Press(key);
            InputApi.QueueStateEvent(_kb, down);
            yield return null;
            yield return null;
            InputApi.QueueStateEvent(_kb, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator PressButton(GamepadButton button)
        {
            InputApi.QueueStateEvent(_pad, new GamepadState { buttons = (ushort)(1 << (int)button) });
            yield return null;
            yield return null;

            InputApi.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            yield return null;
        }

        private static string Describe(InputDevice d)
            => d == null ? "null" : $"{d.name}#{d.deviceId}(enabled={d.enabled},added={d.added})";

        private VisualElement Focused()
            => _root?.panel?.focusController?.focusedElement as VisualElement;

        /// <summary>恒未 Armed(急救通道未启动的形态)。</summary>
        private sealed class NotArmed : DaYiJingCheng.Gameplay.Interaction.IArmedState
        {
            public bool IsArmed => false;
        }
    }
}
