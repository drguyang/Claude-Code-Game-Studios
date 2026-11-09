// 手册近景内容宿主的**生产接线**验证(T10 · 2026-11-09)
//
// 判据(可证伪,五条):
//   ① 真启动序之后 `CompositionRootServices.PaperCloseups` **非 null**
//      —— 缺失 = 48 内容表或解锁查询没到位,「宿主已接」这句话没有主语;
//   ② `TryPresent` 返回 true 且 `CurrentModal == PaperCloseup48`
//      —— 开屏真的发生了,不是「算了但没开」;
//   ③ 屏是 `PaperCloseupScreen` 且**已绑定**纸条目(`PageCount ≥ 1` 且标题非空)
//      —— 开了屏但没喂数据 = 一本白纸(与「手册本来就是空的」不可区分);
//   ④ 缺页集合与 presenter 现算的一致
//      —— 屏上的缺页态不是第二套判据;
//   ⑤ `PaperCloseupScreen.IsCurrentPageMissing` 与快照一致
//      —— 渲染面真的消费了那句话(③④ 都绿而本句假 = 判据没落到屏上)。
//
// ⚠️ **本测不证明可达** —— 「走近摊纸」的世界交互不存在,玩家点不开这本手册。
//    本件证的是「宿主被调用时,屏的内容与缺页态是对的」。
//
// 形态承 `TechniqueUnlockWiringTest`(真启动序 + 反射观测 + Addressables 世界卸载)。

using System;
using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>手册近景内容宿主的接线验证(T10)。</summary>
    public class PaperCloseupWiringTest
    {
        private const float BootTimeoutSeconds = 15f;

        private GameObject _root;
        private BootRoot _boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var ui = typeof(BootRoot)
                .GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot) as SkeuoRuntimeDriver;
            if (ui != null) ui.CloseAllModals();

            // ⚠️ 不能用 `GameObject.Find` —— 它只查 active 对象,而启动序会把预摆件
            //    `SetActive(false)` ⇒ 清场静默失效 ⇒ 同名件跨测累积(assembly gate `pm-teardown-find`)。
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
        public IEnumerator test_paperCloseupPresenter_feedsScreenWithMissingSet()
        {
            _root = new GameObject("BootRoot_PaperCloseupWiring");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("paper_closeup_wiring_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");
            yield return null;

            object services = typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(services, "启动序后 _services 应非 null");

            // ── ① 宿主存在 ───────────────────────────────────────────────
            object presenter = services.GetType()
                .GetProperty("PaperCloseups")?.GetValue(services);
            Assert.IsNotNull(presenter,
                "PaperCloseups 为 null ⇒ 48 内容表或解锁查询未到位,手册近景无宿主");

            var ui = typeof(BootRoot)
                .GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot) as SkeuoRuntimeDriver;
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应存在(第 6 步建)");

            // ── ② 开屏 ──────────────────────────────────────────────────
            var tryPresent = presenter.GetType().GetMethod("TryPresent");
            Assert.IsNotNull(tryPresent, "PaperCloseupPresenter.TryPresent 应存在");
            bool opened = (bool)tryPresent.Invoke(presenter, new object[] { ui });

            Assert.IsTrue(opened, "TryPresent 返回 false ⇒ 开屏被拒(已有更高优先级模态)");
            Assert.AreEqual(ModalId.PaperCloseup48, ui.CurrentModal,
                "模态不是 PaperCloseup48 ⇒ 宿主要么没开,要么开了别的屏");

            // ── ③ 已绑定纸条目 ──────────────────────────────────────────
            var screen = ui.Screen as PaperCloseupScreen;
            Assert.IsNotNull(screen, "PaperCloseup48 开出来的屏应是 PaperCloseupScreen");

            Assert.That(screen.PageCount, Is.GreaterThanOrEqualTo(1),
                "PageCount = 0 ⇒ 开了屏但没喂数据(一本白纸,与「手册本来就是空的」不可区分)");
            Assert.That(screen.CurrentTitle, Is.Not.Empty, "当前页标题应来自 48 内容表");

            // ── ④ 缺页集合与 presenter 现算一致 ─────────────────────────
            object snapshot = presenter.GetType().GetMethod("Build")?.Invoke(presenter, null);
            Assert.IsNotNull(snapshot, "Build 应返回快照");

            var missing = (System.Collections.IEnumerable)snapshot.GetType()
                .GetField("MissingPaperIds")?.GetValue(snapshot);
            var expected = new System.Collections.Generic.List<string>();
            foreach (object id in missing) expected.Add((string)id);

            // 今日内容:唯一那张纸不教技法 ⇒ 缺页集为空
            Assert.That(expected, Is.Empty,
                "今日 48 只有一张**纯知识页**,缺页集必须为空 —— 非空说明内容表被改过,须同批核对");

            // ── ⑤ 渲染面真的消费了那句话 ────────────────────────────────
            Assert.That(screen.IsCurrentPageMissing, Is.False,
                "屏上的缺页态与快照不一致 ⇒ 判据没落到屏上");
        }

        private bool ReadBooted()
        {
            if (_boot == null) return false;
            var field = typeof(BootRoot).GetField("_booted",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null && (bool)field.GetValue(_boot);
        }
    }
}
