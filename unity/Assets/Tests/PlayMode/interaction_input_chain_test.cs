// sprint-07 S2.2/S2.5 · 玩家输入链 PlayMode 验证(2026-10-11)
//
// 判据(可证伪):
//   ① 注入虚拟 E(Interact)→ InteractionInputDriver.OnFrame ⇒ **零查体**(S-8.4 路线甲:
//     世界 Interact 只到就诊;查体归脉案行级 Submit,判据见 `casebook_command_router_test`);
//   ② 注入虚拟 F(Emergency)→ OnFrame ⇒ 急救手动链:病史流出现
//     `EmergencyAttempt` + `EmergencyTreatmentApplied` 双事件(S2.5 判据);
//   ③ 持键不重复触发:恒按住 E 只就诊一次(按下沿语义)。
//   🔁 2026-11-02(`TD-diagnosis-01`):①②③ 的观测面由「分册快照 / LastExaminedPatient」
//     改为 `caseOpen` 接缝的记录 —— 世界 E 已不查体,旧判据在任何实现下都成立(空洞)。
//
// ⚠️ 时序前置(BCD-码-3):急救事件须在病人出现(DiseaseOnset)之后 → 测前须
//   `Advance` 推 tick 使当前 tick > 出现 tick(≤ 同 tick 入流会触发「乱序入流」红)。
// ⚠️ 探针:每测按下后先断言读回(区分「键位注入不上」与「链断」两因 —— 承
//   movement_virtual_key_test 的键盘位怪癖实证:同环境 W 位有效而部分键位不上)。

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;   // ModalGate / IArmedState / IModalGateState
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
        private int _actorId;

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
            _actorId = actorId;
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
        public IEnumerator test_virtualE_keyboardRead_and_doesNotExamineInWorld()
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

            // 🔁 2026-11-02(结清 `TD-diagnosis-01`):`S-8.4` 路线甲逐字 ——
            //   「世界空间裸 `Interact` = 就诊(单义);**查体 = 脉案模态行级动作**」。
            //   ⇒ 世界 E **不再查体**:分册零快照、`LastExaminedPatient` 恒 None、病史流零新增。
            //   (查体的正面判据移到 `casebook_command_router_test` 的行级 Submit 链。)
            Assert.AreEqual(PatientId.None.Value, _driver.LastExaminedPatient.Value,
                "世界 E **不得**再触发查体(旧判据「按 E 查体中最近在场病人」已作废)");
            Assert.IsFalse(_bag.ExaminationLedger.HasExamined(pid),
                "分册不得有该病人的任何快照 —— 查体入口只剩脉案行级 Submit");
            Assert.AreEqual(streamCountBefore, _bag.Stream.Count,
                "世界 E 零写流(旧行为是 +1 条 SkillGrown;那正是要移除的「按键即查体」)");

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

        // ══════════════════════════════════════════════════════════════════
        // 规则七方向② · 模态开 ⇒ 交互意图被丢弃(2026-10-31 补 · 生产执行行)
        //
        // 补的是一条**此前只能靠读代码守**的线:`InteractionInputDriver.OnFrame` 里
        // 「门闭 ⇒ 丢弃 E」那两行。EditMode 造不出按键,只能在 PlayMode 用
        // `QueueStateEvent` 真注入(与本文件其余用例同一形态)。
        // ══════════════════════════════════════════════════════════════════

        private static ModalGate Gate(bool modalOpen)
            => new ModalGate(new FixedArmed(), new FixedModalState(modalOpen));

        [UnityTest]
        public IEnumerator test_modalOpen_discardsE_noVisit()
        {
            AssembleChain();
            int pid = _bag.Presence.PresentPatientIds().First();
            var visits = new List<PatientId>();
            var gated = new InteractionInputDriver(_bag, () => _player.transform.position,
                                                   _actorId, Gate(modalOpen: true),
                                                   caseOpen: (p, t) => visits.Add(p));

            Press(Key.E);
            if (!PlayerInputReader.InteractPressed())
                PressGamepadButton(GamepadButton.North);
            Assert.IsTrue(PlayerInputReader.InteractPressed(),
                "探针:注入生效 —— 若此条红,键位怪癖而非门的问题");

            int before = _bag.Stream.Count;
            gated.OnFrame();
            ReleaseAll();

            // 🔁 2026-11-02(`TD-diagnosis-01`):世界 E 已不查体 ⇒ 旧判据(分册无快照 / 零 SkillGrown)
            //   在任何实现下都成立 = 空洞负控。改为**直接观测就诊回调**是否被调用。
            Assert.AreEqual(0, visits.Count,
                "模态开 ⇒ E 必须被丢弃(否则「暂停时还能就诊」)");
            Assert.That(gated.LastExaminedPatient.Value, Is.EqualTo(PatientId.None.Value),
                "丢弃时不得记下任何目标");
            Assert.AreEqual(before, _bag.Stream.Count, "丢弃 ⇒ 零事件");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_modalClosed_acceptsE_visits()
        {
            // **正控**:同一条构造路径、同一次按键注入,只把门换成「模态关」——
            // 若这条也红,上一条的绿就是假的(驱动根本没工作)。
            AssembleChain();
            int pid = _bag.Presence.PresentPatientIds().First();
            var visits = new List<PatientId>();
            var gated = new InteractionInputDriver(_bag, () => _player.transform.position,
                                                   _actorId, Gate(modalOpen: false),
                                                   caseOpen: (p, t) => visits.Add(p));

            Press(Key.E);
            if (!PlayerInputReader.InteractPressed())
                PressGamepadButton(GamepadButton.North);
            Assert.IsTrue(PlayerInputReader.InteractPressed(), "探针:注入生效");

            gated.OnFrame();
            ReleaseAll();

            Assert.AreEqual(1, visits.Count,
                "门放行时 E 必须照常就诊(正控 —— 让上一条有判别力)");
            Assert.AreEqual(pid, visits[0].Value, "就诊对象 = 最近的在场病人");
            Assert.IsFalse(_bag.ExaminationLedger.HasExamined(pid),
                "就诊**不等于**查体 —— 查体归脉案行级 Submit(`TD-diagnosis-01`)");

            yield return null;
        }

        private sealed class FixedArmed : IArmedState
        {
            public bool IsArmed => false;
        }

        private sealed class FixedModalState : IModalGateState
        {
            private readonly bool _open;
            public FixedModalState(bool open) => _open = open;
            public bool IsOpen => _open;
        }

        [UnityTest]
        public IEnumerator test_heldE_noRepeat_visitOnce()
        {
            AssembleChain();
            int pid = _bag.Presence.PresentPatientIds().First();

            // 恒按住(不抬):连续 3 帧按下沿应只触发**第一帧**一次。
            // 🔁 2026-11-02(`TD-diagnosis-01`):观测面从「分册快照 tick」换成
            //   **就诊回调的调用次数**(世界 E 已不查体,旧的快照 tick 判据恒绿 = 空洞).
            var visits = new List<long>();
            var probed = new InteractionInputDriver(
                _bag, () => _player.transform.position, _actorId,
                caseOpen: (p, t) => { if (p.Value == pid) visits.Add(t); });

            Press(Key.E);
            if (!PlayerInputReader.InteractPressed())
                PressGamepadButton(GamepadButton.North);
            probed.OnFrame();                        // 沿 1:就诊
            Assert.AreEqual(1, visits.Count, "沿 1 应已就诊(前置)");

            _bag.TickDriver.Advance(0.05);           // → tick+1
            probed.OnFrame();                        // 沿 2:仍按住 ⇒ 不得重复
            _bag.TickDriver.Advance(0.05);           // → tick+2
            probed.OnFrame();                        // 沿 3:仍按住 ⇒ 不得重复
            ReleaseAll();

            Assert.AreEqual(1, visits.Count,
                "恒按住 E 只就诊一次(按下沿判据 —— 持键不重复触发)");

            yield return null;
        }
    }
}