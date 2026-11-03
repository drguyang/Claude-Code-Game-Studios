// camera-viewpoint/story-007 —— 档位请求**覆盖矩阵**(表 → 代码 → 行为)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**`docs/architecture/camera-mode-requests.yaml`**(档位表真源)。
//   为什么必须读真表而不是内联常量:本夹具的判据就是「**表里的每一行都真的被发过**」——
//   把表抄进夹具,表改了夹具不动 ⇒ 判据立刻失效,而且失效时**看起来还是绿的**。
// 写:**零写盘**。
//
// ── 这一层为什么不可省 ───────────────────────────────────────────────────────
// 静态门(`CameraSetModeRequesterGate.RunModeRequestTable`)只能证明「**有人声明要发**」:
// `EmergencyChannelDriver` 的调用是 `rig.SetMode(mode, RequesterId)`,**mode 是形参**,
// 字面量在调用方 ⇒ 静态扫描抓不到「真的发没发」。
// 本文件是「该发没发」的唯一判据 —— 每条表行一个 `[UnityTest]`,**行 id 写在方法名里**。
//
// ── 与 S23-10 的分工(别混读)──────────────────────────────────────────────
// `casebook/story-006` 修的是「39 那两行没有请求点」这个**实例**;
// 本件修的是「这种缺口**没有判据**」这**一类**。两者不互相替代。
//
// 权威:`production/epics/camera-viewpoint/story-007-mode-request-coverage-matrix.md`
//       · `docs/architecture/camera-mode-requests.yaml` · ADR-020 Amendment C ①② · ADR-024

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Diagnostics;   // CameraModeRequestRecorder(story-007 采集面)
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.CameraMatrix
// ⚠️ 命名空间**不得**叫 `...PlayMode.Camera` —— 它会在 `DaYiJingCheng.Tests.PlayMode.*` 里
//    遮蔽 `UnityEngine.Camera`,使同装配其它夹具的 `Camera` 类型解析失败(2026-11-02 实测,
//    一次红 10 处既有夹具)。目录名 `Camera/` 无此影响 —— 起作用的是命名空间。
{
    /// <summary>
    /// story-007 L2:档位表逐行覆盖 —— 每一行都必须在一个真实启动序里**真的被发过**。
    /// </summary>
    public class CameraModeRequestMatrixTest
    {
        private const float BootTimeoutSeconds = 20f;
        private const int MaxWaitFrames = 240;
        private const int ActionOrdinal = 0;
        private const string PresetCameraName = "Main Camera";

        private GameObject _root;
        private BootRoot _boot;
        private CompositionRootServices _services;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            try { ReadUiDriver()?.CloseModal(); } catch (Exception) { /* 拆卸段不抛 */ }

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
                catch (Exception) { valid = false; }
            }

            if (valid)
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone) yield return null;
            }

            var presenter = _services?.DeathPresenter;
            if (presenter != null) UnityEngine.Object.Destroy(presenter.gameObject);

            DestroyAllNamed("Player");
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _boot = null;
            }
            _services = null;
            yield return null;

            DestroyAllNamed(PresetCameraName);
            DestroyAllNamed("SkeuoUI");
            yield return null;
        }

        // ══════════════ CMR-001 / 002 —— 10 急救动作 ══════════════

        [UnityTest]
        public IEnumerator test_cmr_001_emergencyArm_requestsTreatment()
        {
            yield return Boot();
            CameraModeRequestRecorder.Clear();

            var driver = _services.EmergencyChannel;
            Assert.IsNotNull(driver, "服务袋须暴露 EmergencyChannel");

            driver.Arm(ActionOrdinal, new PatientId(ReadFirstPresentId()));
            yield return WaitForRequest(CameraMode.Treatment, 10);

            AssertRecorded("CMR-001", CameraMode.Treatment, 10);
        }

        [UnityTest]
        public IEnumerator test_cmr_002_emergencyEnd_requestsExplore()
        {
            yield return Boot();
            CameraModeRequestRecorder.Clear();

            var driver = _services.EmergencyChannel;
            Assert.IsNotNull(driver, "服务袋须暴露 EmergencyChannel");

            driver.Arm(ActionOrdinal, new PatientId(ReadFirstPresentId()));
            yield return WaitForRequest(CameraMode.Treatment, 10);

            driver.EndAction(ReadPlayerActorId());
            yield return WaitForRequest(CameraMode.Explore, 10);

            AssertRecorded("CMR-002", CameraMode.Explore, 10);
        }

        // ══════════════ CMR-003 / 004 —— 39 脉案模态边沿 ══════════════

        [UnityTest]
        public IEnumerator test_cmr_003_casebookOpen_requestsCasebook()
        {
            yield return Boot();

            var ui = ReadUiDriver();
            Assert.IsNotNull(ui, "启动序应建 SkeuoRuntimeDriver");
            // 🔁 2026-11-02:常驻屏占位退役 ⇒ 启动即 None,直接开就是**真迁移**(见 story-006 头注)

            CameraModeRequestRecorder.Clear();
            Assert.IsTrue(ui.OpenModal(ModalId.Casebook), "脉案屏应能打开");
            yield return WaitForRequest(CameraMode.Casebook, 39);

            AssertRecorded("CMR-003", CameraMode.Casebook, 39);
        }

        [UnityTest]
        public IEnumerator test_cmr_004_casebookClose_requestsExplore()
        {
            yield return Boot();

            var ui = ReadUiDriver();
            Assert.IsNotNull(ui, "启动序应建 SkeuoRuntimeDriver");
            Assert.IsTrue(ui.OpenModal(ModalId.Casebook), "脉案屏应能打开");

            CameraModeRequestRecorder.Clear();
            ui.CloseModal();
            yield return WaitForRequest(CameraMode.Explore, 39);

            AssertRecorded("CMR-004", CameraMode.Explore, 39);
        }

        // ══════════════ CMR-005 / 006 —— 29 死亡过渡两段 ══════════════

        [UnityTest]
        public IEnumerator test_cmr_005_playerDied_requestsDowned()
        {
            yield return Boot();

            int playerId = ReadPlayerActorId();
            CameraModeRequestRecorder.Clear();

            long tick = ReadCurrentTick() + 1;
            _services.EventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.PlayerDied,
                _services.Encoder.Encode(EventKind.PlayerDied,
                    new PlayerDiedPayload(playerId, new WorldPos(5, 0, 3), tick))));

            yield return WaitForRequest(CameraMode.Downed, 29);
            AssertRecorded("CMR-005", CameraMode.Downed, 29);
        }

        [UnityTest]
        public IEnumerator test_cmr_006_playerRevive_requestsExplore()
        {
            yield return Boot();

            int playerId = ReadPlayerActorId();
            long deathTick = ReadCurrentTick() + 1;

            _services.EventSink.Append(new SimEvent(
                deathTick, PatientId.None, -1, EventKind.PlayerDied,
                _services.Encoder.Encode(EventKind.PlayerDied,
                    new PlayerDiedPayload(playerId, new WorldPos(5, 0, 3), deathTick))));

            // 先确保真的进过 Downed(否则「回 Explore」这条是空转)
            yield return WaitForRequest(CameraMode.Downed, 29);
            AssertRecorded("CMR-005[前置]", CameraMode.Downed, 29);

            CameraModeRequestRecorder.Clear();

            // 复活格事件:严格晚于 deathTick(`DeathTransitionQuery` 的判据是 `e.Tick > deathTick`)
            long reviveTick = deathTick + 1;
            _services.EventSink.Append(new SimEvent(
                reviveTick, PatientId.None, -1, EventKind.ActorCellEntered,
                _services.Encoder.Encode(EventKind.ActorCellEntered,
                    new ActorCellEnteredPayload(playerId, new WorldPos(6, 0, 3), reviveTick))));

            yield return WaitForRequest(CameraMode.Explore, 29);
            AssertRecorded("CMR-006", CameraMode.Explore, 29);
        }

        // ══════════════ S7-05 记录 ⊆ 表(双向判据的另一半)══════════════

        /// <summary>
        /// 一次启动序 + 一串混合驱动之后,**记录到的每一条 `(mode, requester)` 都必须在表里**。
        /// <para>⚠️ 与逐行夹具的分工:逐行那六条答「表 ⊆ 记录」(该发没发),
        /// 本条答「记录 ⊆ 表」(发了没登记 = 第二真源)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_s7_05_recordedRequestsAreAllInTable()
        {
            yield return Boot();
            CameraModeRequestRecorder.Clear();

            var ui = ReadUiDriver();
            ui.OpenModal(ModalId.Casebook);
            yield return null;
            ui.CloseModal();
            yield return null;

            var driver = _services.EmergencyChannel;
            driver.Arm(ActionOrdinal, new PatientId(ReadFirstPresentId()));
            yield return WaitForRequest(CameraMode.Treatment, 10);
            driver.EndAction(ReadPlayerActorId());
            yield return null;

            var table = LoadTable();
            Assert.That(table.Count, Is.GreaterThan(0), "档位表解析出 0 行 —— 判据没有宾语");

            // ⚠️ **正控**(2026-11-02 证伪轮 P1 抓出):记录器若**停写**,违规集合恒为空
            // ⇒ 下面的断言恒真(空洞正控)。必须先证明「这一次真的记录到了东西」。
            var recorded = CameraModeRequestRecorder.Snapshot();
            Assert.That(recorded.Count, Is.GreaterThan(0),
                "记录器一条都没采到 —— 本次驱动根本没经过 `CameraModeMachine.SetMode`。\n" +
                "⚠️ 缺这个正控时,「记录 ⊆ 表」会在**什么都没记录**的情况下恒绿(空洞正控)。");

            var offenders = new List<string>();
            foreach (var r in recorded)
            {
                // 启动默认档的 `-1`(无请求方)不是任何系统的意图,不属表的判据面
                if (r.RequesterId < 0) continue;
                if (!table.Contains((r.Mode, r.RequesterId)))
                    offenders.Add($"({r.Mode}, {r.RequesterId})");
            }

            Assert.That(offenders, Is.Empty,
                "记录到了**表里没有**的档位请求 —— 那是第二真源(某个系统擅自发了一个没登记的档)。\n" +
                "要么把它登记进 `docs/architecture/camera-mode-requests.yaml`,要么删掉那条发送。\n" +
                string.Join("\n", offenders));
        }

        // ══════════════ 辅助 ══════════════

        private void AssertRecorded(string rowId, CameraMode mode, int requester)
        {
            var recorded = CameraModeRequestRecorder.Snapshot();
            bool hit = false;
            foreach (var r in recorded)
                if (r.Mode == mode && r.RequesterId == requester) { hit = true; break; }

            Assert.IsTrue(hit,
                $"{rowId}:表声明 (requester {requester}, mode {mode}) 必须真的被发过,实测记录 " +
                $"[{Describe(recorded)}]。\n" +
                "⚠️ 这就是「该发没发」:静态门只能证明**有人声明要发**,证不了它真会发。\n" +
                "若本行原本有夹具而这里是红的,先查该请求方的生产接线是否被删。");

            // 记录 ⊆ 表:顺带把「发了没登记」也钉在每一条上
            var table = LoadTable();
            Assert.That(table.Contains((mode, requester)), Is.True,
                $"{rowId}:({mode}, {requester}) 不在 `camera-mode-requests.yaml` 里 —— 发了没登记");
        }

        private static string Describe(List<CameraModeRequest> list)
        {
            var parts = new List<string>(list.Count);
            foreach (var r in list) parts.Add($"({r.Mode},{r.RequesterId})");
            return string.Join(",", parts);
        }

        private static IEnumerator WaitForRequest(CameraMode mode, int requester)
        {
            for (int i = 0; i < MaxWaitFrames; i++)
            {
                foreach (var r in CameraModeRequestRecorder.Snapshot())
                    if (r.Mode == mode && r.RequesterId == requester) yield break;
                yield return null;
            }
        }

        /// <summary>读档位表真源(只读仓库文件;具名声明见文件头)。</summary>
        private static HashSet<(CameraMode, int)> LoadTable()
        {
            var set = new HashSet<(CameraMode, int)>();
            string root = FindRepoRoot();
            if (root == null) return set;

            string path = Path.Combine(root, "docs", "architecture", "camera-mode-requests.yaml");
            if (!File.Exists(path)) return set;

            int requester = -1;
            string mode = "", status = "";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("- id:"))
                {
                    requester = -1; mode = ""; status = "";
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim().Trim('"');

                if (key == "requester") int.TryParse(value, out requester);
                else if (key == "mode") mode = value;
                else if (key == "status")
                {
                    status = value;
                    if (status == "active" && requester >= 0 && Enum.TryParse(mode, out CameraMode m))
                        set.Add((m, requester));
                }
            }
            return set;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docs", "architecture", "camera-mode-requests.yaml")))
                    return dir.FullName;
            }
            return null;
        }

        private IEnumerator Boot()
        {
            foreach (var stale in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == "CameraRig") UnityEngine.Object.Destroy(t.gameObject);

            // ⚠️ `SkeuoUI` 不随 `BootRoot` 销毁(`FindOrCreateSkeuoUi` 会复用已有实例)
            //    ⇒ 上一件的驱动(含 `_currentModal`)会活到本件。story-006 实测踩过一次。
            DestroyAllNamed("SkeuoUI");
            DestroyAllNamed(PresetCameraName);
            yield return null;
            yield return null;

            var preset = new GameObject(PresetCameraName);
            preset.AddComponent<Camera>();
            preset.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_ModeMatrix");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            _services = ReadServices();
            Assert.IsNotNull(_services, "服务袋经反射可读");

            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 3; i++) yield return null;
        }

        private static void DestroyAllNamed(string objectName)
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
                if (go != null && go.name == objectName) UnityEngine.Object.Destroy(go);
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var f = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }

        private CompositionRootServices ReadServices()
        {
            if (_boot == null) return null;
            return typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot)
                as CompositionRootServices;
        }

        private SkeuoRuntimeDriver ReadUiDriver()
        {
            if (_boot == null) return null;
            return typeof(BootRoot).GetField("_uiDriver",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot)
                as SkeuoRuntimeDriver;
        }

        private long ReadCurrentTick()
        {
            if (_boot == null) return 0;
            var driver = typeof(BootRoot).GetField("_tickDriver",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot);
            if (driver == null) return 0;
            var prop = driver.GetType().GetProperty("CurrentTick");
            return prop != null ? (long)prop.GetValue(driver) : 0;
        }

        private int ReadPlayerActorId()
        {
            for (int id = 0; id < 64; id++)
                if (_services.PlayerActors.IsPlayer(id)) return id;
            Assert.Fail("本地玩家 id 应已在花名册");
            return -1;
        }

        private int ReadFirstPresentId()
        {
            var presence = _services.GetType().GetProperty("Presence")?.GetValue(_services);
            var ids = presence?.GetType().GetMethod("PresentPatientIds")?.Invoke(presence, null)
                      as IReadOnlyCollection<int>;
            if (ids != null)
                foreach (int id in ids) return id;
            return 0;
        }
    }
}
