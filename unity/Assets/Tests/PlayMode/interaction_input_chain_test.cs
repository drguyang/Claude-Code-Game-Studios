// sprint-07 S2.2/S2.5 · 玩家输入链 PlayMode 验证(2026-10-11)
//
// 判据(可证伪):
//   ① 注入虚拟 E(Interact)→ InteractionInputDriver.OnFrame ⇒ 查体:ledger 有快照 +
//     LastExaminedPatient = 该病人(值区有词的前提 = 查体落地);
//   ② 注入虚拟 F(Emergency)→ OnFrame ⇒ 急救手动链:病史流出现
//     `EmergencyAttempt` + `EmergencyTreatmentApplied` 双事件(S2.5 判据);
//   ③ 持键不重复触发:恒按住 E 只查体一次(按下沿语义)。
//
// ⚠️ 时序前置(BCD-码-3):急救事件须在病人出现(DiseaseOnset)之后 → 测前须
//   `Advance` 推 tick 使当前 tick > 出现 tick(≤ 同 tick 入流会触发「乱序入流」红)。
// ⚠️ 探针:每测按下后先断言读回(区分「键位注入不上」与「链断」两因 —— 承
//   movement_virtual_key_test 的键盘位怪癖实证:同环境 W 位有效而部分键位不上)。

using System.Collections;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>虚拟 E/F 按键 → 查体 / 急救的真链验证(sprint-07 S2)。</summary>
    public sealed class interaction_input_chain_test
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private bool _keyboardAdded;
        private bool _gamepadAdded;
        private PlayerController _player;
        private CompositionRootServices _bag;
        private InteractionInputDriver _driver;

        private object _manager;
        private System.Reflection.FieldInfo _maskField;
        private InputUpdateType _savedMask;
        private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private InputSettings.BackgroundBehavior _savedBg;
        private bool _savedRunInBackground;
        private bool _envSaved;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RestoreEnvironmentIfNeeded();
            Time.captureDeltaTime = 0f;

            if (_keyboard != null && _keyboardAdded)
            {
                InputSystem.RemoveDevice(_keyboard);
                _keyboardAdded = false;
            }
            if (_gamepad != null && _gamepadAdded)
            {
                InputSystem.RemoveDevice(_gamepad);
                _gamepadAdded = false;
            }
            _keyboard = null;
            _gamepad = null;

            _bag?.PatientVisualSpawner?.DestroyAll();
            if (_player != null)
            {
                Object.Destroy(_player.gameObject);
                _player = null;
            }
            _bag = null;
            _driver = null;
            yield return null;
        }

        // ── 三件套环境(照 movement_virtual_key_test 同型)──
        private void SaveEnvironment()
        {
            _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _savedBg = InputSystem.settings.backgroundBehavior;
            _savedRunInBackground = Application.runInBackground;
            _manager = typeof(InputSystem).GetProperty("manager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public).GetValue(null);
            _maskField = typeof(InputSystem).Assembly
                .GetType("UnityEngine.InputSystem.InputManager")
                ?.GetField("m_UpdateMask", System.Reflection.BindingFlags.NonPublic
                                            | System.Reflection.BindingFlags.Instance);
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

        private void AssembleChain()
        {
            SaveEnvironment();
            Time.captureDeltaTime = 1f / 60f;

            _bag = CompositionRoot.Assemble();

            _keyboard = Keyboard.current;
            if (_keyboard == null)
            {
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _keyboardAdded = true;
            }

            var go = new GameObject("Player");
            _player = go.AddComponent<PlayerController>();
            int actorId = _bag.IdAuthority.NextPatientId().Value;
            _player.Initialize(SimAuthorityMode.Host, _bag.EventSink, _bag.TickDriver,
                               _bag.Encoder, actorId);
            _player.Teleport(new Vector3(0f, 1f, 0f));

            _driver = new InteractionInputDriver(
                _bag, () => _player.transform.position, actorId);

            // 病人出现(tick 1)+ 视觉 + 体征求值
            _bag.PatientAppearedDriver.OnTickEdge(1);
            _bag.PatientVisualSpawner.OnTickEdge(1);
            _bag.VitalsService.OnTickEdge(1);

            // 推 tick 至 2:DiseaseOnset 已入流(tick1),后续事件须 > 1(BCD-码-3)
            _bag.TickDriver.Advance(0.05);
            _bag.TickDriver.Advance(0.05);
        }

        // ── 键位注入:键盘 E/F(并集读)—— 若键盘位不上则回落手柄 ──
        // (键盘位注入怪癖:virtual_key 三件套中某环境按键位不触发,手柄浮点是
        //   device_and_presentation_test 早已实证的可靠注入形态 —— 此处并集提高判据抗性)

        private void Press(Key key)
        {
            var state = new KeyboardState();
            state.Press(key);
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }

        /// <summary>手柄 buttonNorth / buttonWest 注入(键盘 E/F 位不上的回落)。</summary>
        private void PressGamepadButton(GamepadButton button)
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
                new GamepadState { buttons = (ushort)(1 << ((int)button)) });
            InputSystem.Update();
        }

        private void ReleaseAll()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            if (_gamepad != null)
                InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        private static bool StreamHas(EventStream stream, EventKind kind)
            => stream.Events.Any(e => e.Kind == kind);

        [UnityTest]
        public IEnumerator test_virtualE_readsAnd_triggersExamine()
        {
            AssembleChain();
            int pid = _bag.Presence.PresentPatientIds().First();
            int streamCountBefore = _bag.Stream.Count;

            // 注入 E:键盘优先,键盘位不上则回落手柄 buttonNorth(并集;手柄浮点注入实证可靠)
            Press(Key.E);
            if (!PlayerInputReader.InteractPressed())
                PressGamepadButton(GamepadButton.North);
            Assert.IsTrue(PlayerInputReader.InteractPressed(),
                "探针:E/buttonNorth 注入后 Interact 读回 = true(键位注入生效 —— 若此条红,键位怪癖非链断)");
            _driver.OnFrame();
            ReleaseAll();

            Assert.AreEqual(pid, _driver.LastExaminedPatient.Value,
                "按 E 应查体中最近在场病人(判据①)");
            Assert.IsTrue(_bag.ExaminationLedger.HasExamined(pid),
                "按 E → 查体分册有该病人快照");
            // ⚠️ 2026-10-12 改判(sprint-08 S1.1 件 B):原判据「查体 = 派生态,病史流零新事件」
            //   **已作废** —— 查体现在**写一条** SkillGrown(诊断技能,objectId = 病种 D)入病史流,
            //   这是病名册「已见集」的真源(ADR-026 ⑥「不独立快照,从流重构」)。
            //   新判据 = 恰一条,且类型 = SkillGrown(不多不少 —— 变异敏感)。
            Assert.AreEqual(streamCountBefore + 1, _bag.Stream.Count,
                "查体 ⇒ 病史流恰 +1 条(SkillGrown;件 B 改判 —— 旧「零新事件」已作废)");
            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.SkillGrown),
                "新增那条 = SkillGrown(诊断成长,病名册已见集真源;ADR-026 ⑥)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_virtualF_readsAnd_triggersEmergency_doubleEvent()
        {
            AssembleChain();

            // ⚠️ story-009 改判(2026-10-14 用户裁定「按住 = 一次动作」):F 按下沿 =
            //   直读通道 Arm(**零事件** —— 事件在松开沿聚合上行);本测按住期经
            //   FeedForTest 喂样本(逻辑层模式,与 story-007 phase spike 同款),松开沿
            //   再 OnFrame 触发 EndAction ⇒ 双事件。旧「按下沿直发」已作废。
            int streamCountBeforeF = _bag.Stream.Count;

            Press(Key.F);
            if (!PlayerInputReader.EmergencyPressed())
                PressGamepadButton(GamepadButton.West);
            Assert.IsTrue(PlayerInputReader.EmergencyPressed(),
                "探针:F/buttonWest 注入后 Emergency 读回 = true(键位注入生效)");
            _driver.OnFrame();                       // 按下沿:Arm + 锁定目标(零事件)

            Assert.AreEqual(DirectChannelState.Armed, _bag.EmergencyChannel.State,
                "按下沿 ⇒ 通道 Armed(story-009 按住语义)");
            _bag.EmergencyChannel.FeedForTest(0.8f, newlyPressed: true);   // 按住期采样
            _bag.EmergencyChannel.FeedForTest(0.6f, newlyPressed: false);

            ReleaseAll();
            _driver.OnFrame();                       // 松开沿:EndAction ⇒ 聚合上行

            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.EmergencyAttempt),
                "按住后松开 ⇒ EmergencyAttempt 入病史流(S2.5 · story-009 按住语义)");
            Assert.IsTrue(StreamHas(_bag.Stream, EventKind.EmergencyTreatmentApplied),
                "按住后松开 ⇒ EmergencyTreatmentApplied 入病史流(S2.5)");
            Assert.AreEqual(_driver.LastEmergencyPatient.Value,
                            _bag.EmergencyChannel.Target.Value,
                "施救对象 = Arm 时锁定的最近在场病人(判据②)");
            Assert.Greater(_bag.Stream.Count, streamCountBeforeF, "事件确有新增(前置自证)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_heldE_noRepeat_examineOnce()
        {
            AssembleChain();
            int pid = _bag.Presence.PresentPatientIds().First();

            // 恒按住(不抬):连续 3 帧按下沿应只触发**第一帧**一次。
            // 判据 = 快照 tick 不因重复覆盖而推高(每帧 Advance 推 tick)。
            Press(Key.E);
            if (!PlayerInputReader.InteractPressed())
                PressGamepadButton(GamepadButton.North);
            _driver.OnFrame();                       // 沿 1:查体(快照 tick = 当前)
            long firstTick = _bag.ExaminationLedger.GetSnapshotTick(pid);
            Assert.GreaterOrEqual(firstTick, 0L, "沿 1 应已查体(前置)");

            _bag.TickDriver.Advance(0.05);           // → tick+1
            _driver.OnFrame();                       // 沿 2:仍按住 ⇒ 不得重查
            _bag.TickDriver.Advance(0.05);           // → tick+2
            _driver.OnFrame();                       // 沿 3:仍按住 ⇒ 不得重查
            ReleaseAll();

            Assert.AreEqual(firstTick, _bag.ExaminationLedger.GetSnapshotTick(pid),
                "恒按住 E 只查体一次(按下沿判据 —— 持键不重复触发;" +
                "重复覆盖会把快照 tick 推高而判红)");

            yield return null;
        }
    }
}