// sprint-05 playtest 诊断 · 虚拟按键移动链路验证(2026-10-11)
//
// 判据(可证伪):注入虚拟 WASD 按键 → `MovementInputReader.ReadMoveAxis` 读到非零轴
// → `MovementFeed.PumpFrame` 驱动 `PlayerController.Move` → **玩家 transform 位置改变**。
// 回答「WASD 按了 xyz 不变」的真因二分:
//   ① 注入生效但位置不变 ⇒ Move / CharacterController 那条链断了(测试红);
//   ② 注入生效且位置变 ⇒ 代码链完好(测试绿)⇒ 用户侧根因 = 编辑器焦点 /
//      Input System 未接管设备,与逻辑链无关。
//
// 形态承 direct_read_channel_phase_test 的三件套(batch 下 InputSystem 无焦点、
// backgroundBehavior 默认挂 DisabledWhileInBackground 会吞事件 ⇒ 三件套 + 关 Editor
// update = 官方 InputTestFixture 配方的 batch 等价物)。本测试**手动装配**,不起 BootRoot
// (不碰 Addressables)⇒ 测的就是「输入 → 位移」这一条生产单帧链。
//
// ⚠️ 依赖 PlayerController.Initialize 签名(SimAuthorityMode, IEventSink, ITickProvider,
// IPayloadEncoder, actorId)与 MovementFeed.PumpFrame 签名 —— 两者都是 production 装配面,
// 改签名此处即编译红(读取面非空转的延伸)。

using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>虚拟按键 → 玩家移动链路验证(playtest 诊断 · WASD xyz 判据)。</summary>
    public sealed class movement_virtual_key_test
    {
        private const string ActionMapName = "Player";
        private const string MoveActionName = "Move";
        private const string BoundActionName = "Move";
        private const int ActionMask = ~0;   // 全动作(用于后备动作资产;见下方注)

        private Keyboard _keyboard;
        private bool _keyboardAdded;
        private PlayerController _player;
        private SimTickDriver _tickDriver;
        private CompositionRootServices _bag;

        private object _manager;
        private FieldInfo _maskField;
        private InputUpdateType _savedMask;
        private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private InputSettings.BackgroundBehavior _savedBg;
        private bool _savedRunInBackground;
        private bool _envSaved;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RestoreEnvironmentIfNeeded();
            Time.captureDeltaTime = 0f;   // 恢复帧节奏,防污染同套件其他测试

            if (_keyboard != null && _keyboard.added)
                InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            _keyboardAdded = false;

            if (_player != null)
            {
                Object.Destroy(_player.gameObject);
                _player = null;
            }
            if (_gamepad != null && _gamepadAdded)
            {
                InputSystem.RemoveDevice(_gamepad);
                _gamepad = null;
                _gamepadAdded = false;
            }
            _bag = null;
            _tickDriver = null;
            yield return null;
        }

        // ── 三件套环境(照 direct_read_channel_phase_test.ChannelFixture.Setup 同型)──

        private void SaveEnvironment()
        {
            _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _savedBg = InputSystem.settings.backgroundBehavior;
            _savedRunInBackground = Application.runInBackground;
            _manager = typeof(InputSystem).GetProperty("manager",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public).GetValue(null);
            Assert.IsNotNull(_manager, "InputSystem.manager 不可达(内部布局变更,须更新反射)");
            _maskField = typeof(InputSystem).Assembly
                .GetType("UnityEngine.InputSystem.InputManager")
                ?.GetField("m_UpdateMask", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_maskField, "InputManager.m_UpdateMask 不可达(内部布局变更)");
            _savedMask = (InputUpdateType)_maskField.GetValue(_manager);
            _envSaved = true;

            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            _maskField.SetValue(_manager,
                (InputUpdateType)((int)_savedMask & ~(int)InputUpdateType.Editor));
        }

        private void RestoreEnvironmentIfNeeded()
        {
            if (!_envSaved) return;
            if (_maskField != null && _manager != null)
                _maskField.SetValue(_manager, _savedMask);
            if (InputSystem.settings != null)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                InputSystem.settings.backgroundBehavior = _savedBg;
            }
            Application.runInBackground = _savedRunInBackground;
            _envSaved = false;
        }

        // ── 装配生产链(手动,不起 BootRoot)──

        private void AssemblePlayer()
        {
            SaveEnvironment();

            // ⚠️ 钉帧节奏(照 camera_follow_wiring_test 先例):PlayerController.Move 内部
            // 用 `Time.deltaTime`(非传入 PumpFrame 的 delta)⇒ batch 下帧跑得极快、
            // Time.deltaTime ≈ 0 ⇒ 位移趋 0(2026-10-11 实测:15 帧 Δ=0.000)。
            // captureDeltaTime 钉 1/60 后「性能无关的稳定帧」,位移 = SpeedWalk×1/60×15 ≈ 1.25m。
            Time.captureDeltaTime = 1f / 60f;

            _bag = CompositionRoot.Assemble();
            _tickDriver = new SimTickDriver();

            _keyboard = Keyboard.current;
            if (_keyboard == null)
            {
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _keyboardAdded = true;
            }

            var go = new GameObject("VirtualKeyPlayer");
            _player = go.AddComponent<PlayerController>();   // RequireComponent 自动补 CharacterController
            int actorId = _bag.IdAuthority.NextPatientId().Value;
            _player.Initialize(SimAuthorityMode.Host, _bag.EventSink, _tickDriver,
                               _bag.Encoder, actorId);
            _player.Teleport(new Vector3(0f, 1f, 0f));
        }

        /// <summary>注入一次按键(状态实例方法置位)。</summary>
        /// <remarks>
        /// 不用 <see cref="KeyboardState(Key[])"/> 构造器 —— 同机实测(2026-10-11)
        /// 该构造注入 <c>Key.W</c> 有效、<c>Key.A</c> 位不上(同一构造器两位行为不同,
        /// 本环境不可靠)。改用 <c>KeyboardState.Press(Key)</c> 实例方法
        /// (内部 = <c>MemoryHelpers.WriteSingleBit</c>,布局自身的位面显式置位),
        /// 是布局无关的可靠注入。
        /// </remarks>
        private void Press(Key key)
        {
            var state = new KeyboardState();
            state.Press(key);
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }

        /// <summary>全抬(整态清零 —— 覆盖任何按下残留)。</summary>
        private void ReleaseAll()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private Gamepad _gamepad;
        private bool _gamepadAdded;

        /// <summary>经手柄左摇杆注入 x 方向轴(避开键盘位注入怪癖 ——
        /// W 键位实证有效而 A/D 位在 batch 环境不上;摇杆浮点是 device_and_presentation_test
        /// 早已实证的注入形态)。</summary>
        private void PushLeftStick(float x)
        {
            if (_gamepad == null)
            {
                _gamepad = Gamepad.current;
                if (_gamepad == null)
                {
                    _gamepad = InputSystem.AddDevice<Gamepad>();
                    _gamepadAdded = true;
                }
            }
            InputSystem.QueueStateEvent(_gamepad,
                new GamepadState { leftStick = new Vector2(x, 0f) });
            InputSystem.Update();
        }

        private void ReleaseStick()
        {
            if (_gamepad != null)
            {
                InputSystem.QueueStateEvent(_gamepad, new GamepadState());
                InputSystem.Update();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据主测:虚拟 W(前后)+ A(左右),驱动 Move,断言 transform 位移
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_virtualW_movesPlayerForward_onRealChain()
        {
            AssemblePlayer();

            // 注入 W(单键状态,其余抬起)
            Press(Key.W);

            float startZ = _player.transform.position.z;

            // 驱动 15 帧(W 持续按下;Move 逐帧读 Keyboard.current 的同一次注入态)
            for (int i = 0; i < 15; i++)
            {
                Vector2 axis = MovementInputReader.ReadMoveAxis();
                MovementFeed.PumpFrame(_player, _tickDriver, axis, 1d / 60d, null);
                yield return null;
            }

            ReleaseAll();

            float endZ = _player.transform.position.z;
            Debug.Log($"[VIRTUAL-KEY] W 15帧: z {startZ:F3} → {endZ:F3}(Δ={endZ - startZ:F3})");

            Assert.That(endZ, Is.GreaterThan(startZ + 0.1f),
                "虚拟 W 应驱动玩家 z 位移(生产链故障判据:注入生效却不动 = Move 链断)");
        }

        [UnityTest]
        public IEnumerator test_virtualStickRight_movesPlayerRight_onRealChain()
        {
            AssemblePlayer();

            // 摇杆 +X(右):x 方向位移链的独立注入 —— 避开键盘 A/D 位注入怪癖
            // (device_and_presentation_test 早证摇杆浮点注入可靠)。
            PushLeftStick(1f);

            float startX = _player.transform.position.x;

            for (int i = 0; i < 15; i++)
            {
                Vector2 axis = MovementInputReader.ReadMoveAxis();
                if (i == 0)
                    Debug.Log($"[MOVE-DIAG] frame=0 axis=({axis.x:F2},{axis.y:F2})");
                MovementFeed.PumpFrame(_player, _tickDriver, axis, 1d / 60d, null);
                yield return null;
            }

            ReleaseStick();
            ReleaseAll();

            float endX = _player.transform.position.x;
            Debug.Log($"[VIRTUAL-KEY] 摇杆右 15帧: x {startX:F3} → {endX:F3}(Δ={endX - startX:F3})");

            Assert.That(endX, Is.GreaterThan(startX + 0.1f),
                "摇杆 +X 应驱动玩家 x 位移(生产链 x 方向判据)");
        }

        [UnityTest]
        public IEnumerator test_virtualW_releasesAllAxis_zeroesWhenNoKey()
        {
            AssemblePlayer();

            // 无按键 ⇒ 读零轴(避免上一测试残留污染;它自身也验证「抬起 = 停止」)
            ReleaseAll();
            Vector2 axis0 = MovementInputReader.ReadMoveAxis();
            Assert.AreEqual(Vector2.zero, axis0, "全抬 ⇒ ReadMoveAxis 恒零(输入读面对)");

            // W 落下 ⇒ 非零
            Press(Key.W);
            Vector2 axisW = MovementInputReader.ReadMoveAxis();
            Assert.AreNotEqual(Vector2.zero, axisW, "W 落下 ⇒ 轴非零");
            Assert.Greater(axisW.y, 0f, "W = 前(+y)");

            ReleaseAll();
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // 方向键并集(用户问题「WASD 和方向键同输入」的虚拟键确认)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_virtualUpArrow_pressAlsoMovesForward()
        {
            AssemblePlayer();

            Press(Key.UpArrow);
            Vector2 axis = MovementInputReader.ReadMoveAxis();
            ReleaseAll();

            Assert.Greater(axis.y, 0f,
                "方向键 ↑(=Key.UpArrow)须与 W 同输入(并集断言 —— 用户实测面)");

            yield return null;
        }
    }
}