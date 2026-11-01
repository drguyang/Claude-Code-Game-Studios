// casebook/story-006 —— 39 的档位请求的**真机层**(L2)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**(启动序走生产路径;模态经生产 `SkeuoRuntimeDriver` 开合)。
// 写:**零写盘**。
//
// ── 🔁 启动形态(2026-11-02 改):**无常驻屏** ────────────────────────────────
// 本条夹具初版写的是「启动即常驻脉案屏」—— 那是 `_screenName` 默认 `"Casebook"` 的**占位**,
// 不是设计。`design/ux/casebook-39.md` §入口逐字写「世界空间对病人裸 `Interact` = 就诊/立案
// → **立案即开册**」⇒ 脉案是**按需打开**的模态。
//
// 2026-11-02 该占位已退役(`_screenName` 默认改为空)。自此:
//   · 启动 `CurrentModal == None`、相机 = `Explore`(两者一致,不再有「摊着的册子配世界机位」);
//   · `OpenModal(Casebook)` 是**真迁移**,第一次就产生意图。
// ⇒ 夹具不再需要「先 CloseModal 走到 None 再开」的热身段。
//
// ⚠️ **仍未闭**:生产侧**没有任何代码打开脉案**(`OpenModal` 的生产调用点只有 Pause / SaveSlots)
//    ⇒ 本夹具证的是「模态一旦打开,意图会发」;「谁来打开」是 37 + 3 的入口缺口,不在本卡。
//
// ── 两条测什么 ───────────────────────────────────────────────────────────────
// · **S39-06**:生产启动序真的把请求器接上了 —— 真 `BootRoot` → 真 `CloseModal()` /
//   `OpenModal(Casebook)` ⇒ 真相机在 `Explore` / `Casebook` 之间真的换档。
//   删 `BootRoot` 的绑定 ⇒ 本条红。同时断 `AC-39-10`(开合册零写事件流)。
// · **S39-07**:驱动的**边沿** —— 同态换屏不产生意图、中间态不产生多余 `Explore`。
//   ⚠️ 本条的被测对象就是 `SkeuoRuntimeDriver.NotifyCasebookCamera`,故把请求缝**换成间谍**
//   再驱动模态序列;不这样做就只能观测 `rig.Mode`,而「重复请求同档」在行为上与
//   「没请求」不可区分(`CameraModeMachine` 幂等)⇒ 会退化成空洞正控。
//
// 权威来源:
//   `design/gdd/casebook.md` 规则五
//   `design/gdd/camera-and-viewpoint.md` `R-2-5`
//   `docs/architecture/adr-020-player-controller-and-camera.md` §五 / Amendment C ①②
//   `production/epics/casebook/story-006-camera-mode-intent.md`

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.Casebook
{
    /// <summary>casebook/story-006 L2:生产接线与模态边沿。</summary>
    public class CasebookCameraWiringTest
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

        // ══════════════ S39-06 生产启动序真的接上了 ══════════════

        [UnityTest]
        public IEnumerator test_s39_06_productionBoot_openCloseDrivesRealCamera()
        {
            yield return Boot();

            var requester = ReadCasebookRequester();
            Assert.IsNotNull(requester,
                "启动序须构造 `CasebookCameraRequester`(39 的具名请求载体)");
            Assert.IsTrue(requester.HasCameraRig, "机位访问器须已装");

            var rig = ReadCameraRig();
            Assert.IsNotNull(rig, "启动序应建 CameraRig");

            var driver = ReadUiDriver();
            Assert.IsNotNull(driver, "启动序应建 SkeuoRuntimeDriver");

            // 前置:启动**无常驻屏**(见头注),相机 = Explore —— 两者一致。
            Assert.AreEqual(ModalId.None, driver.CurrentModal,
                "[前置] 启动应无模态(常驻屏占位已于 2026-11-02 退役)");
            Assert.AreEqual(CameraMode.Explore, rig.Mode,
                "[前置] 启动相机 = Explore —— 与「无模态」一致");

            int eventsBefore = EventCount();

            // Act ①:开册 —— **这是判别步**:没有接线时相机停在 Explore
            Assert.IsTrue(driver.OpenModal(ModalId.Casebook),
                "脉案屏应能打开(闭集成员 + ScreenFactories 登记;开不出来说明装配面变了)");
            yield return WaitForMode(rig, CameraMode.Casebook, 240);
            Assert.AreEqual(CameraMode.Casebook, rig.Mode,
                "「开册 → 驱动边沿 → 请求缝 → 39 的请求 → 相机进 Casebook」整条链须真的通。\n" +
                "⚠️ 判别点是 `BootRoot` 把 `_casebookCameraRequester.Request` 赋给\n" +
                "   `SkeuoRuntimeDriver.CasebookCameraRequest` —— 删掉那两行即红。\n" +
                "⚠️ 若红而 L1 绿,再查 `BootRoot.Update` 的 `_cameraRig.Tick(...)`:\n" +
                "   **请求发了但档位机不结算**是同一类「看着接好了其实没生效」。");

            // Act ②:合册 ⇒ 回 Explore(**第二个判别步**:没有接线时会卡在 Casebook)
            driver.CloseModal();
            yield return WaitForMode(rig, CameraMode.Explore, 240);
            Assert.AreEqual(CameraMode.Explore, rig.Mode,
                "合册须回 Explore —— 否则相机会卡在脉案档(甲案「同一请求方可降档」)");

            // AC-39-10:开合册全程零写事件流
            Assert.AreEqual(eventsBefore, EventCount(),
                "开册 / 合册**不得**写事件流(AC-39-10;相机模式是本地表现态,不进三流)");
        }

        // ══════════════ S39-07 边沿:同态换屏不产生意图 ══════════════

        [UnityTest]
        public IEnumerator test_s39_07_edge_noDuplicateNoSpuriousExplore()
        {
            yield return Boot();

            var driver = ReadUiDriver();
            Assert.IsNotNull(driver, "启动序应建 SkeuoRuntimeDriver");
            Assert.AreEqual(ModalId.None, driver.CurrentModal, "[前置] 启动应无模态(常驻屏占位已退役)");

            // 把请求缝换成间谍 —— 被测对象是驱动的边沿判据,不是相机的接受逻辑。
            var calls = new List<bool>();
            driver.CasebookCameraRequest = calls.Add;

            // ① 先开一次(启动无模态 ⇒ 这是**真迁移**)
            Assert.IsTrue(driver.OpenModal(ModalId.Casebook), "开册应成功");
            Assert.AreEqual(1, calls.Count, $"开册应恰一条意图(实测 [{string.Join(",", calls)}])");
            Assert.AreEqual(true, calls[0], "开册意图 = true(进 Casebook)");

            // ② 已经开着脉案,再「开」一次 ⇒ **同态**,零新增意图。
            //    ⚠️ 这一步是 `CloseModalCore` 拆分的判别点:若换屏路径走的是会发意图的
            //    `CloseModal()`,这里会再冒出 [false, true] 两条(中间态 Explore + 重进 Casebook)。
            Assert.IsTrue(driver.OpenModal(ModalId.Casebook), "同态换屏应仍成功");
            Assert.AreEqual(1, calls.Count,
                $"同态换屏**不得**产生新意图(实测 [{string.Join(",", calls)}])");

            // ③ 合册 ⇒ 离开脉案 ⇒ 恰一条 false
            driver.CloseModal();
            Assert.AreEqual(2, calls.Count, $"合册应恰一条意图(实测 [{string.Join(",", calls)}])");
            Assert.AreEqual(false, calls[1], "合册意图 = false(回 Explore)");

            // ④ 已无模态,再「合」一次 ⇒ 同态,零意图
            driver.CloseModal();
            Assert.AreEqual(2, calls.Count, "无模态时合册应为空操作");

            // ⑤ 开册 ⇒ 恰一条 true
            Assert.IsTrue(driver.OpenModal(ModalId.Casebook), "开册应成功");
            Assert.AreEqual(3, calls.Count, $"开册应恰一条意图(实测 [{string.Join(",", calls)}])");
            Assert.AreEqual(true, calls[2], "开册意图 = true(进 Casebook)");

            // ⑥ 从脉案直接换到别的模态 ⇒ 恰一条 false(离开脉案),**不得**多出中间态意图
            Assert.IsTrue(driver.OpenModal(ModalId.InventoryContainer), "库存屏应能打开");
            Assert.AreEqual(4, calls.Count,
                $"脉案 → 库存应恰一条意图(实测 [{string.Join(",", calls)}])——\n" +
                "多出来的那条通常是「关旧屏」那一步发的:换屏路径不得经会发意图的 `CloseModal()`");
            Assert.AreEqual(false, calls[3], "离开脉案意图 = false");

            // ⑦ 关掉库存 ⇒ 本来就不在脉案 ⇒ 零意图
            driver.CloseModal();
            Assert.AreEqual(4, calls.Count, "从非脉案模态关屏**不得**产生意图");

            yield return null;
        }

        // ══════════════ 辅助 ══════════════

        private IEnumerator Boot()
        {
            foreach (var stale in UnityEngine.Object.FindObjectsOfType<AudioListener>(true))
            {
                if (stale != null) UnityEngine.Object.Destroy(stale.gameObject);
            }
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (t != null && t.name == "CameraRig")
                    UnityEngine.Object.Destroy(t.gameObject);
            }
            // ⚠️ **`SkeuoUI` 不随 BootRoot 销毁**(`FindOrCreateSkeuoUi` 用
            // `new GameObject("SkeuoUI")` 落当前场景,且会 `FindFirstObjectByType` 复用已有实例)
            // ⇒ 上一件测试的驱动(含 `_currentModal`)会原样活到下一件。
            // 2026-11-02 实测:S39-06 的 TearDown 把模态关成 `None` 之后,S39-07 复用到同一实例
            // ⇒ 前置「启动态 = 常驻脉案屏」变 `None`,红了一次。⇒ 每件测试**必须**先拆掉它。
            DestroyAllNamed("SkeuoUI");
            DestroyAllNamed(PresetCameraName);
            yield return null;
            yield return null;

            var preset = new GameObject(PresetCameraName);
            preset.AddComponent<Camera>();
            preset.AddComponent<AudioListener>();

            _root = new GameObject("BootRoot_CasebookCamera");
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

        private int EventCount() => _services?.Stream?.Events?.Count ?? 0;

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

        private object ReadField(string name)
        {
            if (_boot == null) return null;
            return typeof(BootRoot).GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_boot);
        }

        private CameraRig ReadCameraRig() => ReadField("_cameraRig") as CameraRig;

        private SkeuoRuntimeDriver ReadUiDriver() => ReadField("_uiDriver") as SkeuoRuntimeDriver;

        private CasebookCameraRequester ReadCasebookRequester() =>
            ReadField("_casebookCameraRequester") as CasebookCameraRequester;
    }
}
