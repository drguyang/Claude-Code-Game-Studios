// 模态端到端装配夹具(2026-11-01)—— **从生产装配**证明规则七的三条抑制真的接上了
//
// ▍病根(2026-11-01 用户指出)
//   规则七的三面此前各自有「零件级」证据,但**没有任何一条**从 `BootRoot` 的装配出发:
//     · `ModalGate` 有单元测试(`modal_gate_test`)—— 但门是**测试自己 new 的**;
//     · 交互链有 PlayMode 测试(`interaction_input_chain_test`)—— 门同样是 `new ModalGate(FixedArmed, FixedModalState)`;
//     · 移动轴 / 建造冻结有单元测试 —— 但没人证明 `BootRoot` 里那两个 lambda 真的接到了**同一个**模态读面。
//   ⇒ **装配断了不会红**。本夹具补的就是这一层:真启动序 → 用**生产 42 驱动**开暂停壳 →
//     观察 E / W / B 三条输入在运行期是否真的被抑制。
//
// ▍判据(可证伪)
//   A 交互(E):模态开时按 E ⇒ `InteractionInputDriver.LastExaminedPatient` 保持 `None`;
//               关屏后按 E ⇒ 变非 None(**正控** —— 证明靶子在场、链路本身是通的)。
//   B 移动(W):模态开时按住 W ⇒ 玩家 `transform.position` 不动;
//               关屏后按住 W ⇒ 位置改变(**正控**:证明 W 注入本身生效,不是「没按下去」)。
//   C 建造(B):模态开时按 B ⇒ `BuildInputDriver.IsBuildMode` 保持 false;
//               关屏后按 B ⇒ 变 true(**正控**)。
//        ⚠️ 只测 B(toggle)不测 Enter(place):Enter 同时是 UI 的 Submit,
//           暂停壳开着时会被官方焦点桥消费成「确认第一行」⇒ 会把屏关掉,污染判据。
//           「四个动作共用同一处 `_blocked` 短路」由 `BuildInputDriver.OnFrameCore` 的
//           首段 `if (_blocked != null && _blocked()) { …四边沿一起更新…; return; }` 保证。
//
// ▍形态
//   真启动序(`new GameObject + BootRoot`,与 `patient_appeared_wiring_test` 同源),
//   键盘注入走 `KeyboardState.Press` + 两条编辑器输入设置改写(与 `pause_focus_navigation_test`
//   同一条实测结论:不改时键盘事件在普通帧被编辑器丢掉,且**不报错**)。
//
//   D 世界 tick:模态开时 tick **不推进**(ADR-013 §十-C **Amendment D**,2026-11-01:冻结深度 =
//               只停 sim tick;触发 = 所有挡路模态)。
//        🔁 原文此处写「世界是否冻结不测 —— ADR-013 §十-C 明写『世界不冻』」:该口径已被
//           **Amendment D** 推翻,本夹具第 4 条即其执行体。原文留档于此,不再成立。
//
// ▍⚠️ 本夹具**不测**:焦点落点(`focus_on_open_test` / `modal_focus_wiring_test`)、
//   UI 开合本身(`pause_focus_navigation_test`)、联机时主机是否仍 tick(归 45,P1b)。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
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
    /// <summary>规则七抑制的**装配级**判据(真启动序 + 生产驱动开屏)。</summary>
    public sealed class modal_gate_wiring_test
    {
        private const float BootTimeoutSeconds = 30f;
        private const int HoldFrames = 20;

        private GameObject _root;
        private BootRoot _boot;
        private bool _captureActive;
        private Keyboard _kb;
        private bool _kbAdded;

        private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private InputSettings.BackgroundBehavior _savedBg;
        private bool _inputEnvSaved;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_captureActive)
            {
                Time.captureDeltaTime = 0f;
                _captureActive = false;
            }

            if (_inputEnvSaved)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                InputSystem.settings.backgroundBehavior = _savedBg;
                _inputEnvSaved = false;
            }

            if (_kbAdded && _kb != null && _kb.added)
                InputSystem.RemoveDevice(_kb);
            _kb = null;
            _kbAdded = false;

            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid = false;
            if (_boot != null)
            {
                try
                {
                    var field = typeof(BootRoot).GetField("_worldLoadHandle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    handle = (AsyncOperationHandle<SceneInstance>)field.GetValue(_boot);
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

            if (_root != null) UnityEngine.Object.Destroy(_root);

            // ⚠️ **必须连 UI 壳一起清**:`BootRoot.FindOrCreateSkeuoUi` 是
            //    `FindFirstObjectByType<SkeuoRuntimeDriver>()` **按类型复用**,
            //    而它建出来的 `SkeuoUI` 对象**不是 `_root` 的子物体** ⇒ 只 Destroy(_root)
            //    会把上一用例的驱动(连同**仍开着的模态**)留给下一个用例。
            //    2026-11-01 实测:上一用例在暂停壳开着时结束 ⇒ 下一用例开局
            //    `CurrentModal=Pause` / `IsBlocked=true`,移动判据被上一轮的冻结顶掉(假红)。
            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(
                         FindObjectsSortMode.None))
            {
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);
            }

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // A · 交互意图(E)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_productionAssembly_modalOpen_discardsInteract()
        {
            yield return BootRealGame();
            var interaction = ReadField<InteractionInputDriver>("_interactionDriver");
            Assert.IsNotNull(interaction, "BootRoot._interactionDriver 应已装配");

            // S22-9(2026-11-01):E 按下沿先问采集 —— 出生格 (0,0,0) 距最近烘焙药丛
            // (2,0,2) 的 d∞ = 2 ≤ r_interact(3) ⇒ 按键会被采集桥**合法消费**,本测的
            // 正控(关屏后按 E 必须查体)会假红。本测的靶是**模态门**不是 E 路由
            // ⇒ 挪到无药丛格 (0,0,50)(最近药丛 d∞ = 41 > 3;查体的最近病人判定无半径门,
            //   挪走不影响查体可达性)。多等几帧让 `LastCommittedCell` 随 tick 提交。
            ReadField<PlayerController>("_player").Teleport(new Vector3(0f, 1f, 50f));
            for (int i = 0; i < 5; i++) yield return null;

            Assert.That(interaction.LastExaminedPatient.Value, Is.LessThan(0),
                "[前置] 开局不该已查过体");

            var ui = ReadField<SkeuoRuntimeDriver>("_uiDriver");
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应已装配");
            Assert.IsTrue(ui.OpenModal(ModalId.Pause), "生产驱动应能打开暂停壳");
            yield return null;
            Assert.IsTrue(ui.BlocksPlay, "暂停壳应处于**挡路**态(BlocksPlay)");

            // Act 1:模态开时按 E
            yield return HoldKey(Key.E, HoldFrames);

            // 🔁 2026-11-02(结清 `TD-diagnosis-01`):世界 E **不再查体**,它做的是「立案 + 开册」。
            //   故判据换成**路由观测位** `LastRoutedKind`:模态期按键被丢弃 ⇒ 路由根本没跑 ⇒ 仍为 null。
            var router = ReadField<InteractionPressRouter>("_pressRouter");
            Assert.IsNotNull(router, "BootRoot 应接出 E 路由(组根无 data-core 时为 null)");

            // Assert 1:意图被丢弃
            Assert.IsNull(router.LastRoutedKind,
                "模态开着时按 E **不得**发生任何路由(规则七 F-4.2:模态期按下的交互 = 没发生)");

            // Act 2(正控):关屏后再按 E
            ui.CloseModal();
            yield return null;
            Assert.IsFalse(ui.BlocksPlay, "关屏后不应再挡路");

            // 🔁 2026-11-02(`TD-interaction-01`):E 现在经 4 的 `InteractionSelector` 按
            //   `R_INTERACT` 选目标 —— **半径外的病人不再被选中**(旧的「最近病人」兜底无半径门)。
            //   ⇒ 正控前先把玩家送到病人格,否则「关屏后按 E」合法地什么都不做,本测会假红。
            var services = ReadField<CompositionRootServices>("_services");
            Assert.IsNotNull(services, "BootRoot._services 应已装配");
            int pid = 0;
            foreach (int id in services.Presence.PresentPatientIds()) { pid = id; break; }
            Assert.IsTrue(services.Presence.TryGetCell(
                    new DaYiJingCheng.Sim.Contracts.PatientId(pid), out var patientCell),
                "[前置] 在场病人应有整数格");
            ReadField<PlayerController>("_player").Teleport(new Vector3(patientCell.X, 1f, patientCell.Z));
            for (int i = 0; i < 5; i++) yield return null;

            yield return HoldKey(Key.E, HoldFrames);

            Assert.AreEqual(DaYiJingCheng.Gameplay.Interaction.InteractableKind.Patient,
                router.LastRoutedKind,
                "关屏后按 E **必须**真的路由到病人(立案 + 开册)—— 否则上一条是空洞负控" +
                "(可能是靶子不在场 / 注入没生效)");
            Assert.AreEqual(ModalId.Casebook, ui.CurrentModal,
                "就诊后应开册(世界 E 的终点 = 开册;查体已移到脉案行级 Submit)");
        }

        // ══════════════════════════════════════════════════════════════════
        // B · 移动轴(W)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_productionAssembly_modalOpen_zeroesMoveAxis()
        {
            yield return BootRealGame();
            var player = ReadField<PlayerController>("_player");
            Assert.IsNotNull(player, "BootRoot._player 应已装配");

            var ui = ReadField<SkeuoRuntimeDriver>("_uiDriver");
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应已装配");

            // [前置] 开局不得已被冻结:否则下面「按住 W 不走」是**上一轮残留**,不是门生效
            var freeze = ReadField<ModalInputFreeze>("_inputFreeze");
            Assert.IsNotNull(freeze, "BootRoot._inputFreeze 应已装配");
            Assert.IsFalse(freeze.IsBlocked,
                $"[前置] 开局 `ModalInputFreeze.IsBlocked` 应为 false(实为 true)—— " +
                $"说明上一次用例把模态状态带进了本轮(BlocksPlay={ui.BlocksPlay}, CurrentModal={ui.CurrentModal})");

            var samples = new System.Collections.Generic.List<Vector2>();

            // ── 正控先做:不开模态按 W 必须走 ──
            Vector3 before = player.transform.position;
            yield return HoldW(HoldFrames, samples);
            Vector3 afterOpen = player.transform.position;

            Assert.That(MaxAxis(samples), Is.GreaterThan(0.1f),
                "[前置] 按住 W 时 `MovementInputReader.ReadMoveAxis()` 必须非零 —— " +
                "为零说明**注入没生效**(编辑器输入策略 / 设备),不是产品红;" +
                "样本全零时下面的位移判据无意义");
            Assert.That((afterOpen - before).magnitude, Is.GreaterThan(0.05f),
                $"模态未开时按住 W 玩家应移动(位移 {(afterOpen - before).magnitude:F3},轴峰值 {MaxAxis(samples):F2})");

            // ── 模态开:同样按 W,不得移动 ──
            Assert.IsTrue(ui.OpenModal(ModalId.Pause), "生产驱动应能打开暂停壳");
            yield return null;
            Assert.IsTrue(ui.BlocksPlay, "暂停壳应处于挡路态");

            Vector3 beforeFrozen = player.transform.position;
            samples.Clear();
            yield return HoldW(HoldFrames, samples);
            Vector3 afterFrozen = player.transform.position;

            Assert.That(MaxAxis(samples), Is.GreaterThan(0.1f),
                "[前置] 冻结窗里 W **仍须被读到**(轴非零)—— 否则「不动」是因为没按,不是门生效");
            Assert.That((afterFrozen - beforeFrozen).magnitude, Is.LessThan(0.01f),
                $"模态开着时按住 W **不得**移动(实际位移 {(afterFrozen - beforeFrozen).magnitude:F4})—— " +
                "生产面 = `BootRoot:1267` 的 `ResolveMoveAxis(…, _inputFreeze)`");

            // ── 关屏后恢复(防「一开模态就再也走不动」) ──
            ui.CloseModal();
            yield return null;
            Vector3 beforeResume = player.transform.position;
            samples.Clear();
            yield return HoldW(HoldFrames, samples);
            Assert.That((player.transform.position - beforeResume).magnitude, Is.GreaterThan(0.05f),
                "关屏后按 W 必须恢复移动(否则冻结是单向的)");
        }

        // ══════════════════════════════════════════════════════════════════
        // C · 建造(B)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_productionAssembly_modalOpen_blocksBuild()
        {
            yield return BootRealGame();
            var build = ReadField<BuildInputDriver>("_buildDriver");
            Assert.IsNotNull(build, "BootRoot._buildDriver 应已装配(建造接线轮)");
            Assert.IsFalse(build.IsBuildMode, "[前置] 开局不该在建造模式");

            var ui = ReadField<SkeuoRuntimeDriver>("_uiDriver");
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应已装配");
            Assert.IsTrue(ui.OpenModal(ModalId.Pause), "生产驱动应能打开暂停壳");
            yield return null;
            Assert.IsTrue(ui.BlocksPlay, "暂停壳应处于挡路态");

            // Act 1:模态开时按 B
            yield return HoldKey(Key.B, HoldFrames);
            Assert.IsFalse(build.IsBuildMode,
                "模态开着时按 B **不得**进入建造模式(四个建造动作共用同一 `_blocked` 短路)");

            // Act 2(正控):关屏后按 B
            ui.CloseModal();
            yield return null;
            yield return HoldKey(Key.B, HoldFrames);
            Assert.IsTrue(build.IsBuildMode,
                "关屏后按 B **必须**进入建造模式 —— 否则上一条是空洞负控");
        }

        // ══════════════════════════════════════════════════════════════════
        // D · 世界冻结(sim tick 停)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 挡路模态摊开 ⇒ `ITickProvider` **不再推进**(ADR-013 §十-C Amendment D,2026-11-01 用户裁定)。
        /// <para>判据三段:① 模态未开时 tick **必须**会推进(正控 —— 不推进则下面的「不推进」是空洞负控);
        /// ② 模态开着时 tick 一位不动;③ 关屏后**恢复**推进(冻结不是单向的)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_productionAssembly_modalOpen_freezesWorldTick()
        {
            yield return BootRealGame();
            var tick = ReadField<SimTickDriver>("_tickDriver");
            Assert.IsNotNull(tick, "BootRoot._tickDriver 应已装配");
            var ui = ReadField<SkeuoRuntimeDriver>("_uiDriver");
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应已装配");

            // ① 正控:没开模态时 tick 必须推进
            long t0 = tick.CurrentTick;
            for (int i = 0; i < 40; i++) yield return null;
            long t1 = tick.CurrentTick;
            Assert.That(t1, Is.GreaterThan(t0),
                $"[正控] 模态未开时 tick 应推进({t0} → {t1})—— " +
                "不推进 ⇒ 下面的「不推进」是空洞负控(与冻结无关)");

            // ② 模态开:tick 不得推进
            Assert.IsTrue(ui.OpenModal(ModalId.Pause), "生产驱动应能打开暂停壳");
            yield return null;
            Assert.IsTrue(ui.BlocksPlay, "暂停壳应处于挡路态");

            long f0 = tick.CurrentTick;
            for (int i = 0; i < 60; i++) yield return null;
            long f1 = tick.CurrentTick;

            Assert.That(f1, Is.EqualTo(f0),
                $"挡路模态摊开时 tick **不得**推进(实际 {f0} → {f1},+{f1 - f0})—— " +
                "生产面 = `BootRoot` 帧泵里的 `worldFrozen` 闸(ADR-013 §十-C Amendment D)");

            // ③ 关屏后恢复
            ui.CloseModal();
            yield return null;
            Assert.IsFalse(ui.BlocksPlay, "关屏后不应再挡路");

            long r0 = tick.CurrentTick;
            for (int i = 0; i < 40; i++) yield return null;
            Assert.That(tick.CurrentTick, Is.GreaterThan(r0),
                $"关屏后 tick 必须恢复推进(实际 {r0} → {tick.CurrentTick})—— 冻结不得是单向的");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装配 / 注入
        // ══════════════════════════════════════════════════════════════════

        /// <summary>真启动序 + 输入环境改写 + 等一帧站稳。</summary>
        private IEnumerator BootRealGame()
        {
            SetupKeyboard();
            SetupInputPolicy();

            _root = new GameObject("BootRoot_ModalGateWiring");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), $"启动序须在 {BootTimeoutSeconds}s 内完成");

            Time.captureDeltaTime = 1f / 60f;
            _captureActive = true;

            // 让体征链 / 在场视图站稳(与 patient_appeared_wiring_test 同一采样窗)
            for (int i = 0; i < 30; i++) yield return null;

            // [前置] 开局必须是**干净模态态** —— 复用上一用例遗留的 UI 壳会带进它的模态。
            // 🔁 2026-11-02:默认屏已退役(`_screenName` 默认空)⇒ 开局 `CurrentModal == None`。
            //    判据**仍**用 `BlocksPlay`:它才是「挡不挡世界」的真判据,不随启动形态漂移
            //    (该判据当初就是为「常驻屏存在但不算玩家摊开」写的,形态改了仍成立)。
            //    只有经 `OpenModal` 显式打开的屏才 `BlocksPlay`(与 `ModalGateStateAdapter` 同源)。
            var ui = ReadField<SkeuoRuntimeDriver>("_uiDriver");
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应已装配");
            Assert.IsFalse(ui.BlocksPlay,
                $"[前置] 开局 `BlocksPlay` 应为 false(实为 true,CurrentModal={ui.CurrentModal})—— " +
                "说明本轮复用了上一用例的 UI 壳(TearDown 未清干净)");
        }

        private void SetupKeyboard()
        {
            if (Keyboard.current == null)
            {
                _kb = InputSystem.AddDevice<Keyboard>();
                _kbAdded = true;
            }
            else
            {
                _kb = Keyboard.current;
                if (!_kb.enabled) InputSystem.EnableDevice(_kb);
            }
        }

        /// <summary>
        /// 编辑器默认会把**键盘/指针**只喂给有焦点的 Game View,并在失去焦点时停用设备 ——
        /// batch / background 下表现为「键按了但 `isPressed` 恒 false」,**且不报错**。
        /// 实测结论见 `pause_focus_navigation_test.cs:565-580`。
        /// </summary>
        private void SetupInputPolicy()
        {
            if (!_inputEnvSaved)
            {
                _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                _savedBg = InputSystem.settings.backgroundBehavior;
                _inputEnvSaved = true;
            }
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }

        /// <summary>按住某键 N 帧再松开 —— 边沿由生产 `Update` 逐帧比对,不在此处模拟。</summary>
        private IEnumerator HoldKey(Key key, int frames)
        {
            var down = new KeyboardState();
            down.Press(key);
            InputSystem.QueueStateEvent(_kb, down);
            for (int i = 0; i < frames; i++) yield return null;

            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>按住 W,逐帧采样**生产读面**的轴值(用来把「没按到」与「被折零」分开)。</summary>
        private IEnumerator HoldW(int frames, System.Collections.Generic.List<Vector2> samples)
        {
            var down = new KeyboardState();
            down.Press(Key.W);
            InputSystem.QueueStateEvent(_kb, down);
            for (int i = 0; i < frames; i++)
            {
                samples.Add(MovementInputReader.ReadMoveAxis());
                yield return null;
            }

            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            for (int i = 0; i < 3; i++) yield return null;
        }

        private static float MaxAxis(System.Collections.Generic.List<Vector2> samples)
        {
            float max = 0f;
            for (int i = 0; i < samples.Count; i++) max = Mathf.Max(max, samples[i].magnitude);
            return max;
        }

        private bool ReadBooted()
        {
            var f = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }

        private T ReadField<T>(string name) where T : class
        {
            var f = typeof(BootRoot).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"BootRoot.{name} 字段应存在(装配面变了 ⇒ 本夹具须同步)");
            return f.GetValue(_boot) as T;
        }
    }
}
