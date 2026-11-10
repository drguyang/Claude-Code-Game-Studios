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
// ⚠️ ~~**本测不证明可达** —— 「走近摊纸」的世界交互不存在,玩家点不开这本手册。~~
//    🔁 **2026-11-10(T18 甲)**:手册**已经有开屏入口**了 —— `M` / 手柄 `select`
//    → `PaperCloseupToggleDriver` → `BootRoot.TryPresentManual()`(本文件 §⑥)。
//    「走近摊纸」那个世界道具仍不存在(C1-a 等关卡工具),**但手册不再打不开**。
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

            // 🔁 2026-11-10(A4):48 由「一张纯知识页」变成「1 张望诊 + 6 张技法页」。
            //    开局只解锁技法 0(止血包扎)⇒ 六张技法页**全部缺**。原断言「缺页集必须为空」
            //    钉的是旧内容态,现按新内容态改写(并要求望诊那张**永不**出现在缺页集里)。
            // ⚠️ 本条钉的是**「缺页集 = 技法页 − 已解锁技法」这个关系**,不是一张固定的名单。
            //    2026-11-10 同日两次改动都落在它上面:
            //      · A4:48 由 1 张纯知识页变成 +6 张技法页 ⇒ 缺页集不再为空;
            //      · 起步集回补技法 1 ⇒ `p_tongqi`(教节奏型通气)**开局就有** ⇒ 不缺。
            //    ⇒ 六张技法页里缺 **5** 张。**改了 `StartingTechniques` 就要回来复核这一行。**
            Assert.That(expected, Is.EquivalentTo(new[]
                { "p_qingchuang", "p_sheshang", "p_liushupi", "p_maodihuang", "p_jinjina" }),
                "五张技法页缺(清创 / 蛇伤 / 柳树皮 / 毛地黄 / 金鸡纳);成员错了 ⇒ 绑定或解锁查询坏了");
            Assert.That(expected, Does.Not.Contain("p_tongqi"),
                "节奏型通气在**起步集**里 ⇒ 它那张纸开局就有,不该缺");
            Assert.That(expected, Does.Not.Contain("p_wangzhen"),
                "望诊四要不是技法页(teachTechnique 缺省)⇒ 它永远不该进缺页集");

            // ── ⑤ 渲染面真的消费了那句话 ────────────────────────────────
            Assert.That(screen.IsCurrentPageMissing, Is.False,
                "屏上的缺页态与快照不一致 ⇒ 判据没落到屏上");
        }

        /// <summary>
        /// **开屏入口真的接上了**(T18 甲):启动序后 `_manualToggle` 存在,且它注入的
        /// 「开」这一面(`TryPresentManual`)能把手册摊开。
        /// </summary>
        /// <remarks>
        /// <para>本条补的是 C1 的断点:`TryPresent` 此前**零生产调用方** ⇒ 手册在游戏里
        /// 一次都打不开,缺页机制「齐全但不触发」。驱动本身的开合判据(上升沿 / 只认无模态 /
        /// 手册开着则关)由 EditMode 的 `paper_closeup_toggle_test` 覆盖;**本条只证接线**。</para>
        /// <para>⚠️ 本条**不证按键能用**:`M` 的读面是 `PlayerInputReader.ManualPressed`,
        /// batch 下虚拟键盘注入需另开工程设置(见 `project_unity_keyboard_injection_batch`),
        /// 真包走查才是它的判据。</para>
        /// </remarks>
        [UnityTest]
        public IEnumerator test_manualToggle_isWiredToThePresenter()
        {
            _root = new GameObject("BootRoot_ManualToggle");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveRootOverride = PlayModeTempSaveRoot.New("paper_closeup_toggle_test");

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), "启动序须在超时内完成");
            yield return null;

            object toggle = typeof(BootRoot)
                .GetField("_manualToggle", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            Assert.IsNotNull(toggle,
                "`_manualToggle` 为 null ⇒ 手册没有开屏入口(C1 的断点仍在:缺页机制不可达)");

            var ui = typeof(BootRoot)
                .GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot) as SkeuoRuntimeDriver;
            Assert.IsNotNull(ui, "BootRoot._uiDriver 应存在(第 6 步建)");
            Assert.AreEqual(ModalId.None, ui.CurrentModal, "前置:开屏前无模态");

            MethodInfo tryPresentManual = typeof(BootRoot)
                .GetMethod("TryPresentManual", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(tryPresentManual, "`TryPresentManual` 应存在(驱动注入的那一面)");

            bool opened = (bool)tryPresentManual.Invoke(_boot, null);

            Assert.IsTrue(opened, "返回 false ⇒ 本局没有手册内容(教学链未装配)或开屏被拒");
            Assert.AreEqual(ModalId.PaperCloseup48, ui.CurrentModal,
                "模态不是 PaperCloseup48 ⇒ 接线接到了别的屏(或根本没开)");
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
