// 7b「存盘(落笔)」的**装配级**判据(2026-11-01)—— 从生产装配证明「按下新行 ⇒ 真的落一个档」
//
// ▍病根(与 `save_slot_load_wiring_test` 的载入半边对称)
//   42 侧有「新行在、按了会调 `TrySave`」的判据(EditMode),但那条用的是**测试替身**。
//   「按下之后生产链真的走到 `SaveService.Checkpoint` 并写出文件」—— 只能从 `BootRoot`
//   的真启动序出发才成立:42 行 → `SaveSlotCommandRouter.TrySave` → `Checkpoint`
//   → 写队列 → 磁盘。
//
// ▍形态
//   真启动序(`new GameObject + BootRoot`),`SaveBasePathOverride` 指向**临时目录**
//   (夹具绝不往玩家 `persistentDataPath` 里写)。开暂停壳 → 开存档位屏 → 按新行。
//
// ▍判据(可证伪)
//   A `newRow_writesSlotFile`:按下后 `save_0.bin` 出现在目录里,且 `Ledger.NextWritableSeq`
//      由 0 推进到 1(高水位只在**写成功之后**推进 ⇒ 这条同时证明「真的落盘了」,不只是入队)
//   B `router_recordsAcceptedSlot`:`LastSavedSlot == 0`(受理事实出口)
//
// ▍⚠️ 本夹具**不测**:写盘失败重试 / 退出保存 / 折叠段字节布局(归 7a 的 EditMode 夹具);
//   也**不测**「落笔后新行即第 1 焦点」—— 那条要槽位清单在落笔后重取,清单源今日是
//   `BootRoot.ManualSaveSlots` 常量(归 7b 数值轮),**未做,不借绿**。

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using UnityEngine.UIElements;          // UIDocument / PanelSettings

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>存档位「落笔」端到端:42 新行 → Checkpoint → 磁盘。</summary>
    public sealed class save_slot_save_wiring_test
    {
        private const float BootTimeoutSeconds = 60f;
        private const float PanelTimeoutSeconds = 10f;
        private const float SaveTimeoutSeconds = 10f;

        private GameObject _root;
        private GameObject _uiGo;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private BootRoot _boot;
        private string _saveDir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // ⚠️ **拆净含 inactive 的 Player**(2026-11-01):`GameObject.Find` 只查 active。
            DestroyAllNamed("Player");

            // ⚠️ `yield` 不能出现在带 catch 的 try 里(CS1626)⇒ 先取值、后等待,
            //    两段分开(与 `modal_focus_wiring_test` 同一写法)。
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
                catch (Exception) { valid = false; }
            }

            if (valid)
            {
                var unload = Addressables.UnloadSceneAsync(handle);
                while (!unload.IsDone) yield return null;
            }

            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_uiGo != null) UnityEngine.Object.Destroy(_uiGo);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(FindObjectsSortMode.None))
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);

            if (_saveDir != null && Directory.Exists(_saveDir))
            {
                try { Directory.Delete(_saveDir, true); } catch (Exception) { /* 清场失败不影响判定 */ }
            }

            _doc = null; _panelSettings = null; _uiGo = null; _boot = null; _root = null; _saveDir = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_newRow_requestsSave_andActuallyWritesSlotFile()
        {
            yield return Boot();

            var driver = ReadDriver();
            Assert.IsNotNull(driver, "[前置] UI 驱动必须在");

            Assert.IsTrue(driver.OpenModal(ModalId.SaveSlots), "存档位屏应能打开(暂停壳 → 存档位 同一条链)");
            var screen = driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(screen, "[前置] 当前屏应为存档位屏");
            Assert.IsNotNull(screen.WritableRow, "[前置] 新行必须在");

            var saves = ReadSaveService();
            Assert.IsNotNull(saves, "[前置] 7a 应已装配(夹具给了临时存档目录)");
            uint before = saves.Ledger.NextWritableSeq;
            Assert.AreEqual(0u, before, "[前置] 本会话从未写过 ⇒ 高水位 = 0");

            // Act:**真派发**提交事件到新行(不是直调 `ActivateWritable`)
            //   承 `casebook_lexicon_book_test.DispatchSubmit` 的实测纪律:
            //   `NavigationSubmitEvent` 属 `NavigationEventBase` 族,派发目标由
            //   `FocusController.focusedElement` 决定 —— **必须先 `Focus()`**,否则事件被丢弃。
            //   ⇒ 本行钉的是「handler 挂上了」,直调语义方法证明不了这一点(先例:同文件 ⑦)。
            DispatchSubmit(screen.WritableRow);

            var router = ReadRouter();
            Assert.IsNotNull(router, "[前置] 路由应已装配");
            Assert.IsTrue(router.IsSaveWired, "落笔半边必须已接线(本夹具的关键前提)");
            Assert.AreEqual(before, router.LastSavedSlot, "受理事实出口应记下目标槽");

            // Assert:等写队列收尾。
            // ⚠️ **不能等文件出现**:`WriteAtomic`(tmp → rename)让文件先可见,
            //    `OnCheckpointCommitted` 在同一 worker 里**之后**才推高水位 ⇒ 文件可见那一刻
            //    台账可能仍是旧值(2026-11-01 首跑实测:文件已在、`Expected 1 But was 0`)。
            //    ⇒ 等**更晚的那个信号**(高水位),再回头断言文件 —— 两个都要。
            float deadline = Time.realtimeSinceStartup + SaveTimeoutSeconds;
            while (saves.Ledger.NextWritableSeq <= before && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(before + 1, saves.Ledger.NextWritableSeq,
                $"{SaveTimeoutSeconds}s 内高水位未推进 ⇒ 按下新行没有真的写成功");

            string expected = Path.Combine(_saveDir, $"save_{before}.bin");
            Assert.IsTrue(File.Exists(expected), $"高水位已推进 ⇒ 文件必须在:{expected}");
        }

        // ══════════════════════════════════════════════════════════════════

        private IEnumerator Boot()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "dayijing_saveslot_save_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);

            _uiGo = new GameObject("SkeuoUI");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _uiGo.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _uiGo.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > PanelTimeoutSeconds)
                    Assert.Fail($"{PanelTimeoutSeconds}s 内面板未 attach");
                yield return null;
            }

            _root = new GameObject("BootRoot_SaveSlotSave");
            _boot = _root.AddComponent<BootRoot>();
            _boot.SaveBasePathOverride = _saveDir;

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), $"启动序须在 {BootTimeoutSeconds}s 内完成");

            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>把提交事件真发给元素(引擎派发管径;先落焦,理由见上书)。</summary>
        private static void DispatchSubmit(VisualElement target)
        {
            target.Focus();
            using (var ev = NavigationSubmitEvent.GetPooled(EventModifiers.None))
                target.SendEvent(ev);
        }

        private SkeuoRuntimeDriver ReadDriver()
            => typeof(BootRoot).GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                   ?.GetValue(_boot) as SkeuoRuntimeDriver;

        private SaveSlotCommandRouter ReadRouter()
            => typeof(BootRoot).GetField("_saveSlotCommands", BindingFlags.NonPublic | BindingFlags.Instance)
                   ?.GetValue(_boot) as SaveSlotCommandRouter;

        private SaveService ReadSaveService()
        {
            object services = typeof(BootRoot).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            return services?.GetType().GetProperty("SaveService")?.GetValue(services) as SaveService;
        }

        private bool ReadBooted()
            => (bool)(typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance)
                          ?.GetValue(_boot) ?? false);
    
        /// <summary>
        /// 销毁场景内所有同名的 GameObject(含 **inactive**)。
        /// <para>⚠️ **不能用 `GameObject.Find`** —— 它只查 active 对象,而被启动序
        /// `SetActive(false)` 的预摆恰是 inactive ⇒ 清场静默失效 ⇒ 同名件跨测累积
        /// ⇒ 后续 `BootRoot.FindInactiveInclusive` 按不稳定枚举序摘错 listener
        /// (`AC-ADR020-10` 偶发红,2026-11-01 实测)。</para>
        /// <para>`Resources.FindObjectsOfTypeAll` 连未激活的一起收;`go.scene.IsValid()`
        /// 排除资产(非场景对象 —— 销毁它们会破坏工程资源)。</para>
        /// </summary>
        private static void DestroyAllNamed(string name)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go != null && go.name == name && go.scene.IsValid())
                    UnityEngine.Object.Destroy(go);
            }
        }
}
}