// 2026-11-02 · 开册入口轮(结清 `TD-casebook-01`)
//
// 债:脉案屏在生产上打不开 —— ① 生产侧零 `OpenModal(ModalId.Casebook)` 调用点;
//     ② 立案由 `CaseOpenedDriver.OnTickEdge` 每 tick 自动开案,与
//     `case-system.md:176`「立案是一次玩家动作」/ `:927`「玩家没去 ⇒ 不立案」方向相反。
//
// 本测的判据(逐条可证伪;全部走**真启动序**,不注入桩):
//   ① **无交互 ⇒ 零 `CaseOpened`** —— 病人已出现、tick 持续推进,病例流仍一条不开。
//      这条是「自动开案已退役」的**否定判据**,也是「玩家没去 ⇒ 不立案」的运行期证据。
//   ② **虚拟 E 打到病人 ⇒ 恰 1 条 `CaseOpened` + 脉案模态真的打开**
//      (`SkeuoRuntimeDriver.CurrentModal == Casebook`)—— 两个判别步缺一不可:
//      只断事件 = 立案通了但屏没开(本债的原状);只断屏 = 屏开了但没立案。
//   ③ **同一病人再次 E ⇒ 不新开案**(仍 1 条;`AC-37-26` / 规则二「回到那一例」),
//      但**仍开册**。
//   ④ **Escape 合册**(`casebook-39.md` §7「关册 | toggle」)—— 且**不得**在其上叠出暂停壳。
//
// ⚠️ 键位注入需要三件套(承 `interaction_input_chain_test` / `movement_virtual_key_test`):
//   编辑器输入行为 + 后台行为 + 更新掩码去掉 `Editor`。缺一则键盘位读不到。
// ⚠️ `SkeuoUI` 不随 `BootRoot` 销毁(`TD-skeuo-02`)⇒ 每件先拆。

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.CasebookEntry
{
    /// <summary>开册入口的真启动序验证(`TD-casebook-01`)。</summary>
    public sealed class casebook_entry_wiring_test
    {
        private const float BootTimeoutSeconds = 20f;
        /// <summary>预摆相机名 —— 与 `casebook_camera_wiring_test` 同款:**场景里的 `Main Camera`**
        /// (不是 `CameraRig`;`CameraRig` 是 `BootRoot` 运行期建的那台)。
        /// ⚠️ 命名错了会造出**第二台带 `AudioListener` 的相机** ⇒ 撞 `AC-ADR020-10` 单挂点断言,
        /// 连带污染同套件后续夹具(2026-11-02 实测:6 红,含无关的 `CompositionRootSmokeTest`)。</summary>
        private const string PresetCameraName = "Main Camera";
        /// <summary>`BootRoot` 运行期建的主相机机位名。</summary>
        private const string RuntimeRigName = "CameraRig";
        /// <summary>等病人出现的最长帧数(1/60 下 = 4s;20 Hz tick ⇒ 80 tick)。</summary>
        private const int PatientWaitFrames = 240;

        private GameObject _root;
        private BootRoot _boot;
        private CompositionRootServices _services;
        private SkeuoRuntimeDriver _ui;

        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private bool _keyboardAdded;
        private bool _gamepadAdded;

        private object _inputManager;
        private FieldInfo _maskField;
        private InputUpdateType _savedMask;
        private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private InputSettings.BackgroundBehavior _savedBg;
        private bool _savedRunInBackground;
        private bool _envSaved;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 拆卸段不抛:模态留着会带脏 `_currentModal` 进下一件(同 `TD-skeuo-02` 的教训)
            try
            {
                var ui = ReadField("_uiDriver") as SkeuoRuntimeDriver;
                if (ui != null && ui.CurrentModal != ModalId.None) ui.CloseModal();
            }
            catch (System.Exception) { /* 拆卸段不抛 */ }

            RestoreEnvironment();

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
                catch (System.Exception)
                {
                    valid = false;
                }
            }

            if (valid)
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone)
                    yield return null;
            }

            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            if (_root != null) UnityEngine.Object.Destroy(_root);
            _boot = null;
            _services = null;
            _ui = null;
            Time.captureDeltaTime = 0f;
            yield return null;

            // ⚠️ 预摆相机 / 运行期机位**必须拆**:它们带着 `AudioListener`,
            //    留一个就撞下一件的 `AC-ADR020-10` 单挂点断言(实测会带红无关夹具)。
            DestroyAllNamed("SkeuoUI");
            DestroyAllNamed(PresetCameraName);
            DestroyAllNamed(RuntimeRigName);
            yield return null;
        }

        // ══════════════════════ ① 无交互 ⇒ 零 CaseOpened ══════════════════════

        [UnityTest]
        public IEnumerator test_ce_01_noInteract_noCaseOpened()
        {
            yield return BootAndWaitForPatient();
            Assert.GreaterOrEqual(PresentCount(), 1, "[前置] 启动序后应有在场病人");

            // 前置自证:病人真的在(否则「零立案」是空话)
            int tickBefore = (int)ReadCurrentTick();

            // Act:再跑 60 帧(1s ⇒ 20 tick),全程**不按任何键**
            for (int i = 0; i < 60; i++) yield return null;

            int tickAfter = (int)ReadCurrentTick();
            Assert.Greater(tickAfter, tickBefore,
                $"[前置自证] 帧内 tick 必须真的推进({tickBefore} → {tickAfter}) —— " +
                "否则「零立案」可能只是时间没走");

            Assert.AreEqual(0, CountEvents(EventKind.CaseOpened),
                "病人出现 + tick 推进,**无任何玩家交互** ⇒ 病例流必须零 CaseOpened —— " +
                "非零 = 自动开案没退役(case-system.md:176「立案是一次玩家动作」)");
        }

        // ══════════════ ② 交互 ⇒ 恰 1 条 CaseOpened + 开册 ══════════════

        [UnityTest]
        public IEnumerator test_ce_02_interact_opensCaseAndCasebook()
        {
            yield return BootAndWaitForPatient();
            int patientId = PresentIds()[0];
            int streamBefore = StreamCount();
            yield return TeleportPlayerToPatient();

            Assert.AreEqual(ModalId.None, UiDriver.CurrentModal,
                "[前置] 启动态应无模态(启动占位已退役)");

            // Act:注入 E 并让**真启动序**的 Update 泵一帧(不手调驱动 —— 那是 F1 型旁路)
            yield return PressInteract();
            yield return null;

            Assert.IsTrue(StreamHas(EventKind.CaseOpened),
                "按 E 打到病人 ⇒ CaseOpened 入病例流(规则二-A 立案时刻 = 该 tick)");
            Assert.AreEqual(1, CountEvents(EventKind.CaseOpened),
                "同一次就诊恰开一案");
            Assert.AreEqual(patientId, FirstCaseOpenedPatient(),
                "开案对象 = 被就诊的病人");

            // 判别步 2:屏真的开了(本债的原状是「立案通了但屏开不出来」)
            Assert.AreEqual(ModalId.Casebook, UiDriver.CurrentModal,
                "就诊后脉案必须**真的打开** —— 非 Casebook = 生产侧仍无 OpenModal(Casebook) 路径");
            Assert.Greater(StreamCount(), streamBefore,
                "[前置自证] 确有新事件进流(否则上两条可能是空断言)");

            // 判别步 3(`TD-diagnosis-01`):世界 E **不查体** —— 分册零快照。
            // ⚠️ 必须配**同场正控**:若分册压根没接线,「零快照」在任何实现下都成立
            //    = 空洞负控(F4)。故紧接着走一次行级查体,要求分册**能**记下来。
            Assert.IsFalse(_services.ExaminationLedger.HasExamined(patientId),
                "世界 E = 立案 + 开册,**不**查体(`S-8.4` 路线甲:查体归脉案模态行级动作)");
            _services.CasebookCommands.ExamineChannel(
                DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel.FaceColor);
            Assert.IsTrue(_services.ExaminationLedger.HasExamined(patientId),
                "[正控] 行级查体必须能写进分册 —— 否则上一条的「零」是空洞的");
        }

        // ══════════ ③ 再次交互 ⇒ 不新开案,但仍开册(回到那一例) ══════════

        [UnityTest]
        public IEnumerator test_ce_03_secondInteract_noNewCaseButCasesbookReopens()
        {
            yield return BootAndWaitForPatient();

            yield return TeleportPlayerToPatient();
            yield return PressInteract();
            yield return null;
            Assert.AreEqual(1, CountEvents(EventKind.CaseOpened), "[前置] 首次就诊开一案");
            Assert.AreEqual(ModalId.Casebook, UiDriver.CurrentModal, "[前置] 首次就诊后已开册");

            // 合册(Escape toggle)—— 不能直接调 CloseModal,否则③测不到「再次」的世界入口
            yield return PressEscape();
            yield return null;
            Assert.AreEqual(ModalId.None, UiDriver.CurrentModal,
                "Escape 应合册(casebook-39.md §7「关册 | toggle」)—— 不是在上面叠暂停壳");

            // Act:同一病人再就诊一次
            yield return TeleportPlayerToPatient();
            yield return PressInteract();
            yield return null;

            Assert.AreEqual(1, CountEvents(EventKind.CaseOpened),
                "同一病人再次就诊**不得**新开案(AC-37-26「回到那一例」)");
            Assert.AreEqual(ModalId.Casebook, UiDriver.CurrentModal,
                "再次就诊仍应开册(回到存页的那一案)");
        }

        // ══════════ ④ 关册 toggle 不得叠出暂停壳 ══════════

        [UnityTest]
        public IEnumerator test_ce_04_escapeWhileCasebook_closesInsteadOfPause()
        {
            yield return BootAndWaitForPatient();
            yield return TeleportPlayerToPatient();
            yield return PressInteract();
            yield return null;
            Assert.AreEqual(ModalId.Casebook, UiDriver.CurrentModal, "[前置] 已开册");
            int streamBeforeClose = StreamCount();

            yield return PressEscape();
            yield return null;

            Assert.AreEqual(ModalId.None, UiDriver.CurrentModal,
                "脉案开着时 Escape = 合册;非 None ⇒ 要么没合上,要么被暂停壳顶替(玩家回不去册)");
            Assert.AreEqual(streamBeforeClose, StreamCount(),
                "合册本身**不得**写流(`casebook-39.md` §8:开 / 关册无 SimEvent;档位 = 本机表现态)");
        }

        // ══════════════════════════ 辅助 ══════════════════════════

        private SkeuoRuntimeDriver UiDriver
        {
            get
            {
                if (_ui == null) _ui = ReadField("_uiDriver") as SkeuoRuntimeDriver;
                return _ui;
            }
        }

        private IEnumerator BootAndWaitForPatient()
        {
            SaveEnvironment();

            foreach (var stale in UnityEngine.Object.FindObjectsOfType<AudioListener>(true))
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                if (t != null && t.name == RuntimeRigName)
                    UnityEngine.Object.Destroy(t.gameObject);
            DestroyAllNamed(PresetCameraName);
            // `SkeuoUI` 不随 BootRoot 销毁(TD-skeuo-02)⇒ 先拆,防止复用上一件的驱动与其模态态
            DestroyAllNamed("SkeuoUI");
            yield return null;
            yield return null;

            var preset = new GameObject(PresetCameraName);
            preset.AddComponent<Camera>();
            preset.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_CasebookEntry");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            _services = ReadField("_services") as CompositionRootServices;
            Assert.IsNotNull(_services, "服务袋经反射可读");

            Time.captureDeltaTime = 1f / 60f;

            for (int i = 0; i < PatientWaitFrames && PresentCount() < 1; i++)
                yield return null;
        }

        // ── 键位注入(三件套同 interaction_input_chain_test) ──

        private void SaveEnvironment()
        {
            _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _savedBg = InputSystem.settings.backgroundBehavior;
            _savedRunInBackground = Application.runInBackground;

            _inputManager = typeof(InputSystem).GetProperty("manager",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            _maskField = typeof(InputSystem).Assembly
                .GetType("UnityEngine.InputSystem.InputManager")
                ?.GetField("m_UpdateMask", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_maskField, "InputManager.m_UpdateMask 应存在(三件套前置)");
            _savedMask = (InputUpdateType)_maskField.GetValue(_inputManager);
            _envSaved = true;

            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            _maskField.SetValue(_inputManager,
                (InputUpdateType)((int)_savedMask & ~(int)InputUpdateType.Editor));

            _keyboard = Keyboard.current;
            if (_keyboard == null)
            {
                _keyboard = InputSystem.AddDevice<Keyboard>();
                _keyboardAdded = true;
            }
        }

        private void RestoreEnvironment()
        {
            if (!_envSaved) return;
            if (_maskField != null && _inputManager != null)
                _maskField.SetValue(_inputManager, _savedMask);
            if (InputSystem.settings != null)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                InputSystem.settings.backgroundBehavior = _savedBg;
            }
            Application.runInBackground = _savedRunInBackground;
            _envSaved = false;
        }

        /// <summary>
        /// 把玩家挪到病人身上(并让格提交走完)——
        /// ⚠️ **不是测试手法上的偷懒,是生产路由的前提**:E 按下沿的路由序是
        /// 「拾取 claim → **采集** → 就诊/查体」(`InteractClaimRoute`)。
        /// 玩家出生格 `(0,0,0)` 半径内**有资源点** ⇒ 原地按 E 被采集链消费,
        /// 根本走不到就诊分支(2026-11-02 实测:`pick=False harvest=True`)。
        /// 采集点的位置是烘焙数据,不由本测控制 ⇒ 靠挪位隔离,不靠放宽判据。
        /// </summary>
        private IEnumerator TeleportPlayerToPatient()
        {
            var player = ReadField("_player") as DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;
            Assert.IsNotNull(player, "[前置] BootRoot 应建出 PlayerController");

            int pid = PresentIds()[0];
            Assert.IsTrue(_services.PatientVisualSpawner.TryGetVisualPosition(pid, out Vector3 pos),
                $"[前置] 病人 {pid} 应有视觉实体位置(否则最近者判据无从谈起)");

            player.Teleport(pos);
            for (int i = 0; i < 4; i++) yield return null;   // 让格提交 / 视觉泵走完
        }

        /// <summary>按下 E(键盘不上则回落手柄 North),让真 Update 泵处理,再松开。</summary>
        private IEnumerator PressInteract()
        {
            var state = new KeyboardState();
            state.Press(Key.E);
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
            if (!PlayerInputReader.InteractPressed())
                PressGamepadButton(GamepadButton.North);

            Assert.IsTrue(PlayerInputReader.InteractPressed(),
                "探针:E/buttonNorth 注入后 Interact 读回须为 true(否则是键位注入失败,不是链断)");

            yield return null;          // 让 BootRoot.Update 跑一遍
            ReleaseAll();
            yield return null;
        }

        /// <summary>按下 Escape(键盘不上则回落手柄 Start)。</summary>
        private IEnumerator PressEscape()
        {
            var state = new KeyboardState();
            state.Press(Key.Escape);
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
            if (!PlayerInputReader.PauseTogglePressed())
                PressGamepadButton(GamepadButton.Start);

            Assert.IsTrue(PlayerInputReader.PauseTogglePressed(),
                "探针:Escape/Start 注入后 PauseToggle 读回须为 true");

            yield return null;
            ReleaseAll();
            yield return null;
        }

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
                new GamepadState { buttons = (ushort)(1 << (int)button) });
            InputSystem.Update();
        }

        private void ReleaseAll()
        {
            if (_keyboard != null) InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            if (_gamepad != null) InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        // ── 读数 ──

        private int StreamCount() => _services?.Stream?.Events?.Count ?? 0;

        private int CountEvents(EventKind kind)
        {
            var evts = _services?.Stream?.Events;
            if (evts == null) return 0;
            int n = 0;
            foreach (var e in evts) if (e.Kind == kind) n++;
            return n;
        }

        private bool StreamHas(EventKind kind) => CountEvents(kind) > 0;

        private int FirstCaseOpenedPatient()
        {
            var evts = _services?.Stream?.Events;
            if (evts == null) return -1;
            foreach (var e in evts)
                if (e.Kind == EventKind.CaseOpened) return e.Patient.Value;
            return -1;
        }

        private int PresentCount() => _services?.Presence?.PresentPatientIds()?.Count ?? 0;

        private List<int> PresentIds()
            => _services?.Presence?.PresentPatientIds()?.ToList() ?? new List<int>();

        private long ReadCurrentTick()
        {
            var d = ReadField("_tickDriver");
            var p = d?.GetType().GetProperty("CurrentTick");
            return p != null ? (long)p.GetValue(d) : -1L;
        }

        private object ReadField(string name)
            => _boot == null
                ? null
                : typeof(BootRoot).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(_boot);

        private bool ReadBooted()
            => ReadField("_booted") is bool b && b;

        private static void DestroyAllNamed(string objectName)
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
                if (go != null && go.name == objectName)
                    UnityEngine.Object.Destroy(go);
        }
    }
}
