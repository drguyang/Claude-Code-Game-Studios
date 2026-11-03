// story-007 —— 29 的档位请求的**真机层**(L2):生产启动序真的接上了 + 边沿不重复。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**(启动序走生产路径;S29-07 用合成 `SimEvent` 直接驱动呈现器)。
// 写:**零写盘**。
//
// ── 两条测什么 ───────────────────────────────────────────────────────────────
// · **S29-06**:`BootRoot` 真的构造了 `DownedCameraRequester` 并绑上机位 ——
//   删掉那两行不崩也不报错(`_downedCameraRequester` 只是没人用),**只有本条会红**。
//   判据走**行为**而不是「字段非 null」:对它发一条 `Request(true)`,真相机须真的进 `Downed`。
// · **S29-07**:呈现器的 `_downedRequested` 边沿 —— 持续倒下期间**只登记一次**。
//   若每帧都请求,`CameraModeMachine._pending` 会无限增长(意图制只需要状态变化)。
//   ⚠️ 本条**必须**在本文件里,因为它要驱动 `DeathTransitionPresenter.Update`(PlayMode 才有帧循环);
//      在 EditMode 里重写一份边沿逻辑 = F1(自造断言顶替被测语义)。
//
// 权威来源:
//   `design/ux/death-transition-29.md` §9(`:188` 两段式 / `:133` VR 全禁)
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ①②
//   `production/epics/death-respawn/story-007-downed-camera-mode-request.md` S29-06 / S29-07

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.DeathTransition;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.DeathRespawn
{
    /// <summary>story-007 L2:生产接线与呈现器边沿。</summary>
    public class DownedCameraWiringTest
    {
        private const float BootTimeoutSeconds = 20f;
        private const string PresetCameraName = "Main Camera";

        private GameObject _root;
        private BootRoot _boot;
        private CompositionRootServices _services;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;

            // ① 卸载加性世界场景(承 story-010 / story-011 同款纪律;否则跨测试累积)。
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

            // ② 经服务袋引用销毁呈现器(不按名字找 —— 同名残留会误删别人的实例)。
            var presenter = _services?.DeathPresenter;
            if (presenter != null) UnityEngine.Object.Destroy(presenter.gameObject);

            // ③ 拆启动序产物。CameraRig 已 `SetParent(BootRoot)` ⇒ 随 `_root` 层级销毁。
            DestroyAllNamed("Player");
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _boot = null;
            }
            _services = null;

            // ④ 让 Destroy 生效**之后**再动预摆(否则同帧两个 AudioListener 并存 ⇒ 断言洪泛)。
            yield return null;

            // ⑤ 拆本件自造的预摆(它带 AudioListener,留着会污染后续 PlayMode 测试)。
            DestroyAllNamed(PresetCameraName);
            yield return null;
        }

        // ══════════════ S29-06 生产启动序真的接上了 ══════════════

        [UnityTest]
        public IEnumerator test_s29_06_productionBoot_wiresDownedCameraRequester()
        {
            yield return Boot();

            // 观测面:具名载体确实由启动序构造(缺口 A「引用却无登记」的补位)。
            var requester = ReadDownedCameraRequester();
            Assert.IsNotNull(requester,
                "启动序须构造 `DownedCameraRequester`(缺口 A 的具名载体)");
            Assert.IsTrue(requester.HasCameraRig, "机位访问器须已装");

            var rig = ReadCameraRig();
            Assert.IsNotNull(rig, "启动序应建 CameraRig(第 5 步)");
            Assert.AreEqual(CameraMode.Explore, rig.Mode, "前置:启动档 = Explore");

            // ── 判据走**真链**(2026-11-02 证伪轮 M5 修正)──────────────────────
            // ⚠️ **初版这里是错的**:它直接 `requester.Request(true)`,于是把
            //    `BootRoot` 传给呈现器的第 4 实参换成 `null`(即「根本没接线」)时,
            //    请求器对象仍在、相机照样进 Downed ⇒ **全绿**。那是「接线在」与
            //    「接线上有行为」的混淆(F1 型:断言绕开了被测的那条缝)。
            // ⇒ 现在走**真链**:真启动序 + 真 `PlayerDied` 落流 ⇒ 呈现器 `Update` ⇒
            //    装配注入的请求缝 ⇒ 请求器 ⇒ 真相机。删掉绑定 ⇒ 缝为 null ⇒ 零请求 ⇒ 红。
            int playerId = -1;
            for (int id = 0; id < 64 && playerId < 0; id++)
                if (_services.PlayerActors.IsPlayer(id)) playerId = id;
            Assert.GreaterOrEqual(playerId, 0, "本地玩家 id 应已在花名册");

            long tick = ReadCurrentTick() + 1;
            _services.EventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.PlayerDied,
                _services.Encoder.Encode(EventKind.PlayerDied,
                    new PlayerDiedPayload(playerId, new WorldPos(5, 0, 3), tick))));

            yield return WaitForMode(rig, CameraMode.Downed, 240);

            Assert.AreEqual(CameraMode.Downed, rig.Mode,
                "「PlayerDied 落流 → 呈现器 → 请求缝 → 29 的请求 → 相机进 Downed」整条链须真的通。\n" +
                "⚠️ 本条的判别点是**装配注入**(`BootRoot` 把 `_downedCameraRequester.Request` 传进\n" +
                "   `DeathTransitionPresenter.Bind` 的第 4 实参)—— 把它换成 null 即红。\n" +
                "⚠️ 若 L1 绿而本条红,另查 `BootRoot.Update` 里的 `_cameraRig.Tick(...)`:\n" +
                "   **请求发了但档位机不结算**是同一种「看起来接好了其实没生效」的失败。");
        }

        // ══════════════ S29-07 边沿不重复 ══════════════

        [UnityTest]
        public IEnumerator test_s29_07_presenterEdge_requestsOnceWhileDowned()
        {
            // Arrange:独立呈现器 + 合成「已死亡」事件(不启真启动序 —— 只测边沿)。
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            const int actorId = 42;
            var events = new List<SimEvent>
            {
                new SimEvent(10, PatientId.None, -1, EventKind.PlayerDied,
                    encoder.Encode(EventKind.PlayerDied,
                        new PlayerDiedPayload(actorId, new WorldPos(5, 0, 3), 10))),
            };

            var go = new GameObject("DeathTransitionPresenter_EdgeTest");
            var presenter = go.AddComponent<DeathTransitionPresenter>();   // Awake 建叠层

            var calls = new List<bool>();
            presenter.Bind(() => events, decoder, () => actorId, downed => calls.Add(downed));

            Time.captureDeltaTime = 1f / 60f;

            // Act:跑足够多帧(相位机走到 Falling → 1.2 s 后 Lying 并停在那里 ——
            //      没有 `ActorCellEntered` ⇒ 永不转 Waking)。
            for (int i = 0; i < 120; i++)
                yield return null;

            // Assert:只登记**一次** `true`。
            int trueCount = 0, falseCount = 0;
            foreach (bool b in calls) { if (b) trueCount++; else falseCount++; }

            Assert.GreaterOrEqual(trueCount, 1,
                $"呈现器应至少发一次 downed=true(实测调用序列 [{string.Join(",", calls)}])—— " +
                "若为 0,先查事件是否真的让相位机进了 Falling");
            Assert.AreEqual(1, trueCount,
                $"持续倒下期间**只应登记一次** downed=true(实测 {trueCount} 次,序列 [{string.Join(",", calls)}])—— " +
                "每帧入队会让 `CameraModeMachine._pending` 无限增长(意图制只需边沿)");
            Assert.AreEqual(0, falseCount,
                $"未醒来期间不应出现 downed=false(实测 {falseCount} 次)");
        }

        // ══════════════ 辅助 ══════════════

        private IEnumerator Boot()
        {
            // 清场:前序测试可能留下 Camera / AudioListener / CameraRig ⇒ 启动序读到 ≥2 个
            // 会 fail-loud(`AC-ADR020-10` fail-loud;全量串跑红、定向单跑不暴露)。
            foreach (var stale in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            }
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name == "CameraRig")
                    UnityEngine.Object.Destroy(t.gameObject);
            }
            DestroyAllNamed(PresetCameraName);
            yield return null;
            yield return null;   // 两帧:Destroy 在帧末生效,再等一帧确保 active 集已刷新

            // 预摆 Main Camera(启动序断言「恰 1 AudioListener」的 fixture)。
            var preset = new GameObject(PresetCameraName);
            preset.AddComponent<Camera>();
            preset.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_DownedCamera");
            _boot = _root.AddComponent<BootRoot>();

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");

            _services = ReadServices();
            Assert.IsNotNull(_services, "服务袋经反射可读");

            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 3; i++)
                yield return null;
        }

        private static IEnumerator WaitForMode(CameraRig rig, CameraMode target, int maxFrames)
        {
            for (int i = 0; i < maxFrames && rig.Mode != target; i++)
                yield return null;
        }

        /// <summary>拆掉所有同名对象(**含未激活**)——
        /// 用 `FindObjectsByType` 而非 `GameObject.Find`:后者只查激活对象,
        /// 被启动序 `SetActive(false)` 的预摆会留在场上(2026-11-01 全量串跑偶发红的根因)。</summary>
        private static void DestroyAllNamed(string objectName)
        {
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (go != null && go.name == objectName)
                    UnityEngine.Object.Destroy(go);
            }
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var f = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && (bool)f.GetValue(_boot);
        }

        private CompositionRootServices ReadServices()
        {
            if (_boot == null) return null;
            return typeof(BootRoot).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot)
                as CompositionRootServices;
        }

        /// <summary>当前逻辑 tick(经 `BootRoot._tickDriver` 反射读 `CurrentTick`)。</summary>
        private long ReadCurrentTick()
        {
            if (_boot == null) return 0;
            var field = typeof(BootRoot).GetField("_tickDriver",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var driver = field?.GetValue(_boot);
            if (driver == null) return 0;
            var prop = driver.GetType().GetProperty("CurrentTick");
            return prop != null ? (long)prop.GetValue(driver) : 0;
        }

        private CameraRig ReadCameraRig()
        {
            if (_boot == null) return null;
            return typeof(BootRoot).GetField("_cameraRig",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot) as CameraRig;
        }

        private DownedCameraRequester ReadDownedCameraRequester()
        {
            if (_boot == null) return null;
            return typeof(BootRoot).GetField("_downedCameraRequester",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot)
                as DownedCameraRequester;
        }
    }
}
