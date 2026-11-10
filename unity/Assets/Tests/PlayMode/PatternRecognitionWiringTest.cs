// 37 同源检测 + 章推进的**生产接线**验证(T20 · 2026-11-10)
//
// 判据(可证伪,两条):
//   ① 真启动序后 `CompositionRootServices.PatternRecognition` **非 null**
//      —— 缺失 = `PatternRecognitionWriter` / `ChapterAdvanceWriter` 仍零生产调用点,
//         规则八在运行期不跑、章号恒 0(钩子配额 = 整局 1 例);
//   ② 37 旋钮产物**真的装上了**:`AddressablesDataProvider.CasePattern.PatternThreshold`
//      等于源表值(GDD P0 = 3)—— 产物缺失会在 **Preload** 阶段响亮抛(E-13),
//      「装上了但值是默认值」这条断言让它可分。
//
// ⚠️ 本条**不证**规则八的判定正确性(`PatternDetector` 的纯函数与写者各由 EditMode 夹具覆盖,
//    驱动见 `pattern_recognition_driver_test`);本条只证**装配**。
// 形态承 `CaseCloseWiringTest`(真启动序 + 反射观测 + Addressables 世界卸载)。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>同源检测 / 章推进的装配面。</summary>
    public class PatternRecognitionWiringTest
    {
        private const float BootTimeoutSeconds = 15f;

        private GameObject _root;
        private BootRoot _boot;

        private bool ReadBooted()
        {
            FieldInfo f = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null && f.GetValue(_boot) is bool b && b;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var ui = typeof(BootRoot)
                .GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot) as SkeuoRuntimeDriver;
            if (ui != null) ui.CloseAllModals();

            foreach (var stale in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (stale.name == "Player") UnityEngine.Object.Destroy(stale);

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

            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator test_patternRecognition_isAssembled_withThresholdFromArtifact()
        {
            _root = new GameObject("BootRoot_PatternRecognition");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("pattern_recognition_wiring_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");
            yield return null;

            object services = typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 _services 应非 null");

            // ── ① 驱动存在 ──────────────────────────────────────────────
            object driver = services.GetType().GetProperty("PatternRecognition")?.GetValue(services);
            Assert.IsNotNull(driver,
                "`PatternRecognition` 为 null ⇒ 同源检测 / 章推进仍是零生产调用点" +
                "(规则八不跑,章号恒 0,钩子配额 = 整局 1 例)");

            // ── ② 阈值真的来自产物(反射进写者的私有字段)──────────────────
            // ⚠️ 驱动非 null 本身已经证明「provider 是 AddressablesDataProvider 且产物装载成功」
            //    (装配块的三重前置),但**不证明值是产物里的那个** —— 硬编码 3 也能过 ①。
            object recognizer = driver.GetType()
                .GetField("_recognizer", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(driver);
            Assert.IsNotNull(recognizer, "驱动应持一个 PatternRecognitionWriter");

            object threshold = recognizer.GetType()
                .GetField("_patternThreshold", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(recognizer);
            Assert.IsNotNull(threshold, "写者应持阈值字段");
            Assert.AreEqual(3, (int)threshold,
                "PATTERN_THRESHOLD 须 = 源表值(GDD P0 = 3);硬编码/产物没装上都会在这里失真");
        }
    }
}
