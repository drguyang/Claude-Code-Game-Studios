// 2026-11-07 · 世界空间手柄路径(走近 → 就诊 → 急救档位)的**真启动序**验证
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:**本夹具零仓库数据文件直读** —— 启动序自己经 Addressables 载入 World 场景与烘焙产物
//   (生产路径,不是本夹具挑的表)。
// 写:**零写盘**;存档根经 `SaveRootOverride` 指到临时目录(`PlayModeTempSaveRoot`)。
//
// ── 为什么需要这个文件(与既有夹具的分工,逐条对过)───────────────────────────
// 既有覆盖是**分开的**,没有一处是「手柄 → 生产帧泵 → 世界空间结果」:
//   · `movement_virtual_key_test` —— 摇杆能推移 transform,但**手动装配**、无启动序、
//     不读世界流(它测的是「输入 → 位移」那一条单帧链);
//   · `casebook_entry_wiring_test` —— 真启动序 + 立案/开册,但**先把玩家传送**到病人身边,
//     且键位是**键盘 E 优先、手柄 North 仅回落**;
//   · `emergency_camera_mode_intent_wiring_test` —— 真档位机,但**直接调 `driver.Arm`**,
//     玩家按键不在链上;
//   · `casebook_gamepad_navigation_test` —— 手柄只到 42 面板内部,世界侧零覆盖。
//
// ── 本文件的判据(逐条可证伪;全部走真启动序 + 虚拟手柄,**零键盘注入**)──────────
//   ① `test_wg_01_...` 左摇杆前推 ⇒ 玩家 transform 位移 **且** 跨格提交
//      (`ActorCellEntered` 进世界流、`LastCommittedCell` 变格)⇒「走近」;
//   ② `test_wg_02_...` 沿**同一条走出来的路**走到交互半径内,只按手柄 North ⇒
//      恰 1 条 `CaseOpened` + 脉案模态真的打开 ⇒「就诊」;
//   ③ `test_wg_03_...` 手柄 West 按下沿 ⇒ 相机档位真的进 `Treatment`;按住 3 s 仍 Armed
//      (采样存活,没被看门狗中止);松开 ⇒ 回 `Explore` ⇒「急救档位」。
//
// ⚠️ 三条都必须走**真 `BootRoot.Update`**(不手调 `InteractionInputDriver.OnFrame` /
//   不直接调 `EmergencyChannelDriver.Arm`)—— 手调就是 F1 型旁路:测的是夹具自己,
//   不是生产帧泵。生产次序见 `BootRoot.Update`:`MovementFeed.PumpFrame` →
//   `_interactionDriver.OnFrame()` → … → `_cameraRig.Tick`。
//
// ⚠️ `Time.captureDeltaTime` 是全局态,[UnityTearDown] 恢复 0,防污染同套件其他测试。
// ⚠️ 预摆 `Main Camera` / 运行期 `CameraRig` / `SkeuoUI` **必须拆** —— 前两者带
//   `AudioListener`,留一个就撞下一件的 `AC-ADR020-10` 单挂点断言(实测会带红无关夹具)。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Player;
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

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>世界空间手柄路径:走近 / 就诊 / 急救档位(真启动序 + 虚拟手柄)。</summary>
    public sealed class world_gamepad_path_test
    {
        private const float BootTimeoutSeconds = 20f;

        /// <summary>预摆相机名 —— 场景里的 `Main Camera`(不是 `CameraRig`)。</summary>
        private const string PresetCameraName = "Main Camera";
        /// <summary>`BootRoot` 运行期建的主相机机位名。</summary>
        private const string RuntimeRigName = "CameraRig";

        /// <summary>等病人出现的最长帧数(1/60 下 = 4 s;20 Hz tick ⇒ 80 tick)。</summary>
        private const int PatientWaitFrames = 240;
        /// <summary>走近的最长帧数(1/60 下 = 7 s;5 m/s 走 14 m 约需 3 s + 加速)。</summary>
        private const int WalkFrames = 420;
        /// <summary>档位转场等待(0.3 s ⇒ 18 帧;留 4× 余量)。</summary>
        private const int ModeWaitFrames = 120;
        /// <summary>按住帧数:> `AbortIdleTicks`(50 tick = 150 帧)⇒ 能区分「采样存活」与「看门狗中止」。</summary>
        private const int HoldFrames = 180;

        private GameObject _root;
        private BootRoot _boot;
        private CompositionRootServices _services;

        private Gamepad _gamepad;
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
                if (UiDriver != null && UiDriver.CurrentModal != ModalId.None) UiDriver.CloseModal();
            }
            catch (Exception) { /* 拆卸段不抛 */ }

            RestoreEnvironment();

            if (_gamepad != null && _gamepadAdded)
            {
                InputSystem.RemoveDevice(_gamepad);
                _gamepadAdded = false;
            }
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
                catch (Exception)
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
            Time.captureDeltaTime = 0f;
            yield return null;

            DestroyAllNamed("SkeuoUI");
            DestroyAllNamed(PresetCameraName);
            DestroyAllNamed(RuntimeRigName);
            yield return null;
        }

        // ══════════════ ① 走近:左摇杆前推 ⇒ 位移 + 跨格提交 ══════════════

        [UnityTest]
        public IEnumerator test_wg_01_leftStick_walksPlayer_andCommitsActorCellEntered()
        {
            yield return BootAndWaitForPatient();

            PlayerController player = Player;
            Assert.IsNotNull(player, "[前置] 启动序应建出 PlayerController");

            // 让启动帧的格提交走完(玩家在 (0,1,0) 出生,首个 tick 边沿提交 (0,0,0))
            for (int i = 0; i < 6; i++) yield return null;

            WorldPos cellBefore = player.LastCommittedCell;
            Vector3 posBefore = player.transform.position;
            int cellEventsBefore = CountEvents(EventKind.ActorCellEntered);

            Assert.GreaterOrEqual(cellBefore.Z, 0,
                "[前置] 玩家须已提交过一格(否则下面的「跨格」判据没有起点)");

            // ── Act:左摇杆前推(y = +1 ⇒ +z;`MovementInputReader` 的轴约定)──
            InjectStick(new Vector2(0f, 1f));

            // 探针:注入后**读回**非零轴 —— 区分「摇杆注入不上」与「链断」两因
            Assert.AreNotEqual(Vector2.zero, MovementInputReader.ReadMoveAxis(),
                "探针:虚拟手柄左摇杆注入后 `ReadMoveAxis` 须非零(否则是注入失败,不是链断)");

            int frames = 0;
            while (frames < WalkFrames && player.transform.position.z < posBefore.z + 4f)
            {
                frames++;
                yield return null;
            }
            ReleaseGamepad();
            for (int i = 0; i < 8; i++) yield return null;   // 让 pending 格在 tick 边沿提交

            // ── Assert:表现态位移 ──
            Assert.Greater(player.transform.position.z, posBefore.z + 4f,
                $"左摇杆前推 4 s 应把玩家推向 +z(实测 {posBefore.z:F2} → {player.transform.position.z:F2})。" +
                "红则先查 `MovementInputReader.ReadMoveAxis` 的手柄支与 `MovementFeed.PumpFrame` 的接线。");

            // ── Assert:sim 投影真的进了世界流(ADR-020 §四:位移的唯一 sim 投影)──
            WorldPos cellAfter = player.LastCommittedCell;
            Assert.Greater(cellAfter.Z, cellBefore.Z,
                $"走过 ≥4 m ⇒ 已提交格须变号(z {cellBefore.Z} → {cellAfter.Z});" +
                "不变 = 位置采样 / 跨格提交断了(此时表现态在动,但流里没有玩家走过)");

            Assert.Greater(CountEvents(EventKind.ActorCellEntered), cellEventsBefore,
                "世界流须新增 `ActorCellEntered`(生产里该 Kind 的唯一写者是 PlayerController)");

            Assert.IsTrue(AnyActorCellBeyond(cellBefore.Z),
                $"须有一条载荷格 z > {cellBefore.Z} 的 `ActorCellEntered` —— " +
                "只数条数可能被同格重复提交蒙混,载荷格才是「真的走过去了」");
        }

        // ══════════ ② 就诊:沿走出来的路到半径内,只按手柄 North ══════════

        [UnityTest]
        public IEnumerator test_wg_02_walkToPatient_thenGamepadNorth_opensCaseAndCasebook()
        {
            yield return BootAndWaitForPatient();

            Assert.GreaterOrEqual(PresentCount(), 1, "[前置] 启动序后应有在场病人");
            int patientId = PresentIds()[0];
            Assert.IsTrue(_services.Presence.TryGetCell(new PatientId(patientId), out WorldPos patientCell),
                "[前置] 在场病人须有整数格(否则交互半径无从谈起)");

            Assert.AreEqual(ModalId.None, UiDriver.CurrentModal, "[前置] 启动态应无模态");

            // ── Act 1:走近(与 ① 同一条手柄路径,不传送)──
            PlayerController player = Player;
            Assert.IsNotNull(player, "[前置] 启动序应建出 PlayerController");

            InjectStick(new Vector2(0f, 1f));
            int walkFrames = 0;
            while (walkFrames < WalkFrames
                   && Chebyshev(player.LastCommittedCell, patientCell) > 2)
            {
                walkFrames++;
                yield return null;
            }
            ReleaseGamepad();
            for (int i = 0; i < 8; i++) yield return null;

            long distance = Chebyshev(player.LastCommittedCell, patientCell);
            Assert.LessOrEqual(distance, 2,
                $"[前置自证] 须真的走进交互半径(r_interact = 3;实测 d∞ = {distance}," +
                $"玩家格 {player.LastCommittedCell.X},{player.LastCommittedCell.Y},{player.LastCommittedCell.Z}" +
                $" / 病人格 {patientCell.X},{patientCell.Y},{patientCell.Z})—— " +
                "走不到就不是就诊判据的问题,是 ① 那条路径的问题");

            // ── Act 2:只按手柄 North(`PlayerInputReader.InteractPressed` 的手柄支)──
            InjectButton(GamepadButton.North);
            Assert.IsTrue(PlayerInputReader.InteractPressed(),
                "探针:手柄 North 注入后 `InteractPressed` 须为 true(否则是注入失败,不是链断)");

            yield return null;          // 让真 `BootRoot.Update` 泵一帧(不手调驱动)
            ReleaseGamepad();
            yield return null;

            // ── Assert:立案 + 开册(两个判别步缺一不可)──
            Assert.AreEqual(1, CountEvents(EventKind.CaseOpened),
                "手柄 North 打到病人 ⇒ 恰 1 条 `CaseOpened`(规则二-A:立案是一次玩家动作)");
            Assert.AreEqual(patientId, FirstCaseOpenedPatient(),
                "立案对象 = 被就诊的病人");
            Assert.AreEqual(ModalId.Casebook, UiDriver.CurrentModal,
                "就诊后脉案必须真的打开 —— 非 Casebook = 生产侧仍无 `OpenModal(Casebook)` 路径");
        }

        // ══════════ ③ 急救档位:West 按下 ⇒ Treatment;按住 3 s 仍 Armed;松开 ⇒ Explore ══════════

        [UnityTest]
        public IEnumerator test_wg_03_gamepadWest_armsTreatmentTier_andReleaseReturnsExplore()
        {
            yield return BootAndWaitForPatient();

            var rig = ReadField("_cameraRig") as CameraRig;
            Assert.IsNotNull(rig, "[前置] 启动序应建 CameraRig(第 5 步)");
            for (int i = 0; i < 10 && rig.Mode != CameraMode.Explore; i++) yield return null;
            Assert.AreEqual(CameraMode.Explore, rig.Mode, "[前置] 启动档 = Explore");

            Assert.IsTrue(_services.EmergencyChannel.HasCameraRig,
                "[前置] 生产启动序须已把 `CameraRig` 绑给急救通道(漏绑是静默失败:永不发 Treatment)");
            Assert.IsFalse(_services.EmergencyChannel.IsArmed, "[前置] 未按键前通道须为 Idle");

            // ── Act 1:手柄 West 按下(按住,不松开)──
            InjectButton(GamepadButton.West);
            Assert.IsTrue(PlayerInputReader.EmergencyPressed(),
                "探针:手柄 West 注入后 `EmergencyPressed` 须为 true(否则是注入失败,不是链断)");

            for (int i = 0; i < 12 && !_services.EmergencyChannel.IsArmed; i++) yield return null;
            Assert.IsTrue(_services.EmergencyChannel.IsArmed,
                "按下沿 ⇒ 直读通道 Armed(事件在松开沿才聚合上行;按下沿本身零事件)");

            int samplesAtArm = _services.EmergencyChannel.SampleCount;

            yield return WaitForMode(rig, CameraMode.Treatment);
            Assert.AreEqual(CameraMode.Treatment, rig.Mode,
                "动作开始 ⇒ 相机须真的进 `Treatment`(AC-10-11 的前半)。\n" +
                "⚠️ 若本条红,先查 `BootRoot.Update` 的 `_cameraRig.Tick(...)` 与通道的 `BindCameraRig` —— " +
                "「请求发了但档位机不结算」是同一种「看起来接好了其实没生效」的失败");

            // ── Act 2:按住 > `AbortIdleTicks`(50 tick = 150 帧)──
            // 判据 = 采样存活:直读通道每帧从 `Emergency` 动作取样本,看门狗据此推基准。
            // 若动作未 enable / 绑重不上 ⇒ 采样停 ⇒ 50 tick 后 `AbortAction` ⇒ 回 Explore。
            for (int i = 0; i < HoldFrames; i++) yield return null;

            int samplesHeld = _services.EmergencyChannel.SampleCount;
            bool stillArmed = _services.EmergencyChannel.IsArmed;

            // 判别步(先分开两因,再判「动作还在不在」):
            //   samples 未涨 ⇒ 接线回调没跑(环境/接线),与看门狗无关;
            //   samples 涨了却掉 Armed ⇒ 看门狗基准没被采样推进 —— 那是**生产缺陷**。
            Assert.Greater(samplesHeld, samplesAtArm,
                $"按住 {HoldFrames} 帧期间直读通道的采样计数必须增长(实测 {samplesAtArm} → {samplesHeld})。" +
                "不增长 = `InputSystem.onAfterUpdate` 回调没跑(批次环境下手动 `Update()` 不触发它," +
                "见 `EmergencyDirectReadChannel` 的 spike 注)或 `Emergency` 动作没 enable");

            Assert.IsTrue(stillArmed,
                $"按住 {HoldFrames} 帧(≈3 s,已越过 `AbortIdleTicks` = 50 tick)后仍须 Armed。" +
                $"实测:采样 {samplesAtArm} → {samplesHeld}(样本在进),Armed = {stillArmed} ⇒ " +
                "**看门狗基准 `_lastActiveTick` 没有被接线采样推进** —— 这是生产缺陷,不是夹具问题");
            Assert.AreEqual(CameraMode.Treatment, rig.Mode,
                "按住期间档位须保持在 `Treatment`(被中止就会掉回 Explore)");

            // ── Act 3:松开 ⇒ 动作结束 ⇒ Explore ──
            ReleaseGamepad();
            for (int i = 0; i < 12 && _services.EmergencyChannel.IsArmed; i++) yield return null;
            Assert.IsFalse(_services.EmergencyChannel.IsArmed,
                "松开沿 ⇒ 通道回 Idle(零样本签约也是合法的一次起止)");

            yield return WaitForMode(rig, CameraMode.Explore);
            Assert.AreEqual(CameraMode.Explore, rig.Mode,
                "动作结束 ⇒ 相机须回 `Explore` —— 这是 AC-10-11 的目的从句「**相机不卡在近景**」。" +
                "红 = 松开了但档位停在近景,玩家回不到世界");
        }

        // ══════════════════════════ 辅助:启动序 ══════════════════════════

        private SkeuoRuntimeDriver UiDriver
        {
            get
            {
                var drv = ReadField("_uiDriver") as SkeuoRuntimeDriver;
                return drv;
            }
        }

        private PlayerController Player => ReadField("_player") as PlayerController;

        private IEnumerator BootAndWaitForPatient()
        {
            SaveEnvironment();

            foreach (var stale in UnityEngine.Object.FindObjectsByType<AudioListener>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
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

            _root = new GameObject("BootRoot_WorldGamepadPath");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("world_gamepad_path_test");

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

        // ── 虚拟手柄(既有形态:`InteractionInputDriver`/`casebook_entry_wiring_test` 同款)──

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

            EnsureGamepad();
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

        private void EnsureGamepad()
        {
            if (_gamepad != null) return;
            _gamepad = Gamepad.current;
            if (_gamepad == null)
            {
                _gamepad = InputSystem.AddDevice<Gamepad>();
                _gamepadAdded = true;
            }
        }

        /// <summary>注入左摇杆值(状态保持到下一次注入;`ReleaseGamepad` 清零)。</summary>
        private void InjectStick(Vector2 stick)
        {
            EnsureGamepad();
            InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = stick });
            InputSystem.Update();
        }

        /// <summary>注入单个手柄按键按下(按住;`ReleaseGamepad` 释放)。</summary>
        private void InjectButton(GamepadButton button)
        {
            EnsureGamepad();
            InputSystem.QueueStateEvent(_gamepad,
                new GamepadState { buttons = (ushort)(1 << (int)button) });
            InputSystem.Update();
        }

        /// <summary>松开全部手柄状态(摇杆归零 + 所有键抬起)。</summary>
        private void ReleaseGamepad()
        {
            if (_gamepad == null) return;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        private static IEnumerator WaitForMode(CameraRig rig, CameraMode target)
        {
            for (int i = 0; i < ModeWaitFrames && rig.Mode != target; i++)
                yield return null;
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

        /// <summary>世界流里是否有「载荷格 z &gt; <paramref name="z"/>」的 `ActorCellEntered`。</summary>
        private bool AnyActorCellBeyond(int z)
        {
            var evts = _services?.Stream?.Events;
            if (evts == null) return false;
            foreach (var e in evts)
            {
                if (e.Kind != EventKind.ActorCellEntered) continue;
                var p = _services.Decoder.Decode<ActorCellEnteredPayload>(e.Kind, e.Payload);
                if (p.Cell.Z > z) return true;
            }
            return false;
        }

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

        /// <summary>F-4.1 第一键 `d∞`(先拓宽到 int64 再取绝对值,与选择器同口径)。</summary>
        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            long dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
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
