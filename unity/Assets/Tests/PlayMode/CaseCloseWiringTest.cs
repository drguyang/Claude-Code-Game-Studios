// 结案键的**生产接线**验证(T19 甲 · 2026-11-10)
//
// 判据(可证伪,两条):
//   ① 真启动序之后 `CompositionRootServices.CaseClosers` **非 null**
//      —— 缺失 = `CaseCloseWriter.TryClose` 的生产调用点仍是 0,病例永不收口;
//   ② `BootRoot` 交给 `InteractionInputDriver` 的 `_routeClosePress` **非 null**
//      —— 有驱动但没接到按键上 = 「写者存在、玩家按了没反应」,与没接同形。
//
// ⚠️ 本条**不证按键能按**:`C` 的读面是 `PlayerInputReader.CloseCasePressed`,
//    batch 下虚拟键盘注入另需工程设置(见记忆 `project_unity_keyboard_injection_batch`,
//    与 T18 甲的 `M` 键同一处境),真包走查才是它的判据。
// ⚠️ 本条也**不证结案可用**:「该不该结」的四条前置在 `CaseCloseWriter` 里,
//    由 EditMode 的 `CaseCloseDriverTest` 与 `CaseCloseWriterTest` 覆盖。
//
// 形态承 `PaperCloseupWiringTest`(真启动序 + 反射观测 + Addressables 世界卸载)。

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
    /// <summary>结案键到 37 写者的那段接线。</summary>
    public class CaseCloseWiringTest
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
        public IEnumerator test_caseCloseDriver_isAssembledAndWiredToTheKey()
        {
            _root = new GameObject("BootRoot_CaseCloseWiring");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("case_close_wiring_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");
            yield return null;

            object services = typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 _services 应非 null");

            // ── ① 结案驱动存在 ──────────────────────────────────────────
            object closers = services.GetType().GetProperty("CaseClosers")?.GetValue(services);
            Assert.IsNotNull(closers,
                "`CaseClosers` 为 null ⇒ `CaseCloseWriter.TryClose` 仍无生产调用点" +
                "(病例永不收口,三条 Case 源解锁规则不可达)");

            // ── ② 按键那一面真的接到驱动上 ──────────────────────────────
            object interaction = typeof(BootRoot)
                .GetField("_interactionDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(interaction, "BootRoot._interactionDriver 应存在(交互输入驱动)");

            object route = interaction.GetType()
                .GetField("_routeClosePress", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(interaction);
            Assert.IsNotNull(route,
                "`_routeClosePress` 为 null ⇒ 驱动存在但按键没接上(按 C 什么也不会发生)");
        }
    }
}
