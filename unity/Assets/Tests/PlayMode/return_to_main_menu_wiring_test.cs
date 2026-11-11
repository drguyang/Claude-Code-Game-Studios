// 「返回主菜单」· **真启动序**装配级判据(2026-11-11 · T22 批七 · `TD-skeuo-14` 解除动作 ③)
//
// ▍它补的是哪一跳
//   实现在 2026-11-05 就落了(`BootRoot.ReturnToMainMenuAsync` → 落盘 → 关闸 → 卸世界 →
//   重载主菜单 → 继续诊治 → 重载世界),并做过**人工**走查。缺的是**自动化**判据 ——
//   于是债只能记 `partial`(解除动作 ③)。本件就是那一条。
//
// ▍判据(可证伪)
//   A. 进世界后调 `IPauseCommandSink.ReturnToMainMenu()` ⇒
//      ① World 场景**真的卸了**;② `_booted` 关(帧泵闸);③ 第一层摊开(`IsMainMenuOpen`);
//      ④ **落盘先发生**(世界目录里出现存档文件)。
//   B. 在第一层按「继续诊治」⇒ World 重新装入、`_booted` 重开、第一层收掉。
//   C. **负控**:第一层摊着时再调一次 `ReturnToMainMenu` ⇒ 不得重复拆 / 不得卡死,
//      之后「继续诊治」仍然有效 —— 幂等性是实现里 `_worldPaused` 那一行的判据。
//
// ▍为什么用 `SaveRootOverride` 而不是 `SaveBasePathOverride`
//   前者把**存档根**指到临时目录(根下照常 `w1/`),走的是**多世界**形态 —— 与生产同形;
//   后者是单世界夹具缝,会绕开本债真正关心的那条路径。
//
// ⚠️ 本件**不**验 ADR-023 §⑤ 的拆序六步全量(V-1…V-7 仍 `NOT-RUN`):它只钉
//   「世界卸了 / 第一层开了 / 继续能回去」这三件事。不得据本件为绿称拆序已按六步验过。

using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>`TD-skeuo-14` 解除动作 ③:返回主菜单的真启动序判据。</summary>
    public sealed class return_to_main_menu_wiring_test
    {
        private const float BootTimeoutSeconds = 60f;
        private const float PanelTimeoutSeconds = 10f;

        private GameObject _root;
        private GameObject _uiGo;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private SkeuoRuntimeDriver _driver;
        private BootRoot _boot;
        private string _saveRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // ⚠️ 不清 `GameObject.Find` —— 启动序会 `SetActive(false)` 预摆件(与
            //    `multi_world_boot_wiring_test` 同一条教训:`pm-teardown-find` 门点名的形态)。
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == "Player") UnityEngine.Object.Destroy(go);

            yield return UnloadSceneIfLoaded("_mainMenuLoadHandle");
            yield return UnloadSceneIfLoaded("_worldLoadHandle");

            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_uiGo != null) UnityEngine.Object.Destroy(_uiGo);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(FindObjectsSortMode.None))
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);

            if (!string.IsNullOrEmpty(_saveRoot))
            {
                try { if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true); }
                catch (Exception) { /* 清场失败不影响判定 */ }
            }

            _doc = null; _panelSettings = null; _uiGo = null; _driver = null; _boot = null; _root = null;
            _saveRoot = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // A + B:回第一层(卸世界 + 落盘 + 第一层摊开)⇒ 继续诊治(装回世界)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_returnToMainMenu_unloadsWorld_andContinueResumes()
        {
            NewRoot();
            yield return BootIntoWorld();

            Assert.IsTrue(WorldSceneLoaded(), "前置:已在世界里(World 场景 additive 装着)");
            Assert.IsTrue(ReadBooted(), "前置:启动序已完成(帧泵闸开着)");

            // ⚠️ 「回程落盘」的判据是**世界目录里有档变新**,不是「盘上有档」——
            //    启动序 8b 早就写过第一份自动档,所以「有档」在返回**之前**就为真。
            //    首版就是这么写的,证伪轮 M1(去掉 `TrySave()`)当场 **0 红** ⇒ 空洞正控(F4)。
            //    第二版只盯 `save_auto.bin`,仍然错:暂停壳的 `TrySave()` 写的是
            //    **手动槽** `save_<NextWritableSeq>.bin`,自动档的 mtime 根本不变。
            //    ⇒ 最终判据 = **目录快照**:返回后须有任一 `save_*.bin` 新出现或变新。
            string worldDir = new WorldCatalog(_saveRoot).DirectoryOf(1);
            yield return WaitForFile(Path.Combine(worldDir, SaveService.AutoSaveFileName), 10f);
            Dictionary<string, DateTime> stampsBefore = SaveStamps(worldDir);
            Assert.IsNotEmpty(stampsBefore, "前置:启动序 8b 应已写出首份存档");

            // ① 走暂停壳的**生产意图出口**(不是反射私有方法):整条链都过一遍。
            Assert.IsNotNull(_driver.PauseSink, "暂停壳意图出口应已注入(装配层)");
            _driver.PauseSink.ReturnToMainMenu();

            yield return WaitUntil(() => _boot.IsMainMenuOpen,
                BootTimeoutSeconds, "返回主菜单后第一层须摊开(IsMainMenuOpen)");

            Assert.IsFalse(WorldSceneLoaded(), "回第一层必须**真的卸掉 World 场景**(否则世界还在画面上)");
            Assert.IsFalse(ReadBooted(), "回第一层必须关帧泵闸(否则世界在后台继续跑)");

            // ② 落盘**先于**关闸 —— 写盘是异步的,故等**某个档变新**,不等帧。
            yield return WaitUntil(() => AnySaveAdvanced(worldDir, stampsBefore),
                10f, "回第一层前必须落一次盘(世界目录里须有 save_*.bin 新出现或写入时刻变新)");

            // ③ 第一层按「继续诊治」
            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "第一层应是主菜单屏");
            menu.ActivateRow(0);

            yield return WaitUntil(() => ReadBooted(), BootTimeoutSeconds, "「继续诊治」须把世界装回来并开闸");

            Assert.IsTrue(WorldSceneLoaded(), "继续诊治后 World 场景须重新装入");
            Assert.IsFalse(_boot.IsMainMenuOpen, "回到世界后第一层须收掉");
        }

        // ══════════════════════════════════════════════════════════════════
        // C. 负控:第一层摊着时再调一次 ⇒ 幂等(不重复拆、不卡死、继续仍有效)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_returnTwice_secondCallIsNoOp_continueStillWorks()
        {
            NewRoot();
            yield return BootIntoWorld();

            _driver.PauseSink.ReturnToMainMenu();
            yield return WaitUntil(() => _boot.IsMainMenuOpen, BootTimeoutSeconds,
                "第一次返回须摊开第一层");
            Assert.IsFalse(WorldSceneLoaded(), "前置:世界已卸");

            // 第二次调(此时 `_booted == false`)⇒ 实现里 `if (_worldPaused || !_booted) return;` 兜住。
            _driver.PauseSink.ReturnToMainMenu();
            for (int i = 0; i < 10; i++) yield return null;

            Assert.IsTrue(_boot.IsMainMenuOpen, "重复调用不得把第一层收掉");
            Assert.IsFalse(WorldSceneLoaded(), "重复调用不得把世界装回来");
            Assert.IsFalse(ReadBooted(), "重复调用不得开闸");

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "第一层仍应是主菜单屏(重复调用没把它拆坏)");
            menu.ActivateRow(0);

            yield return WaitUntil(() => ReadBooted(), BootTimeoutSeconds,
                "重复调用后「继续诊治」仍须有效(幂等 = 可继续,不是卡死)");
            Assert.IsTrue(WorldSceneLoaded(), "继续后世界须重新装入");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装置
        // ══════════════════════════════════════════════════════════════════

        private void NewRoot()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "dyjq_rtmn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        /// <summary>启动到第一层 → 按「开始诊治」→ 等世界闸开。</summary>
        private IEnumerator BootIntoWorld()
        {
            yield return BootUntilMenuAppears();

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "启动序应摊开主菜单");
            menu.ActivateRow(0);                       // 「开始诊治 / 继续诊治」
            yield return WaitUntil(() => ReadBooted(), BootTimeoutSeconds, "启动序第 3 步须完成装配");
            yield return WaitUntil(WorldSceneLoaded, BootTimeoutSeconds, "World 场景须装入");
        }

        private IEnumerator BootUntilMenuAppears()
        {
            _uiGo = new GameObject("SkeuoUI");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _uiGo.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _uiGo.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > PanelTimeoutSeconds)
                    Assert.Fail($"{PanelTimeoutSeconds}s 内面板未 attach —— 本门要的是**真面板**");
                yield return null;
            }

#if UNITY_EDITOR
            AttachProductionStyleSheets(_doc.rootVisualElement);
#endif

            _root = new GameObject("BootRoot_ReturnToMenu");
            _boot = _root.AddComponent<BootRoot>();
            // ⚠️ 必须在 Start 之前赋值(Awake 已跑;Start 在下一帧)
            _boot.BootWithMainMenu = true;
            _boot.SaveRootOverride = _saveRoot;

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!_boot.IsMainMenuOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(_boot.IsMainMenuOpen,
                $"{BootTimeoutSeconds}s 内主菜单未摊开 —— 启动序第 2.95 步没走到");

            for (int i = 0; i < 5; i++) yield return null;
        }

        private IEnumerator WaitUntil(Func<bool> condition, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(condition(), reason + $"(等了 {seconds}s)");
        }

        private static IEnumerator WaitForFile(string path, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>世界目录里 <c>save_*.bin</c> 的快照(文件名 → 写入时刻)。</summary>
        private static Dictionary<string, DateTime> SaveStamps(string dir)
        {
            var map = new Dictionary<string, DateTime>();
            if (!Directory.Exists(dir)) return map;
            foreach (string file in Directory.GetFiles(dir, "save_*.bin"))
                map[Path.GetFileName(file)] = File.GetLastWriteTimeUtc(file);
            return map;
        }

        /// <summary>相对快照,是否有**任一**存档新出现或写入时刻变新。</summary>
        private static bool AnySaveAdvanced(string dir, Dictionary<string, DateTime> before)
        {
            if (!Directory.Exists(dir)) return false;
            foreach (string file in Directory.GetFiles(dir, "save_*.bin"))
            {
                DateTime now = File.GetLastWriteTimeUtc(file);
                if (!before.TryGetValue(Path.GetFileName(file), out DateTime then) || now > then)
                    return true;
            }
            return false;
        }

        private bool ReadBooted()
            => _boot != null
               && (bool)typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance)
                                      .GetValue(_boot);

        /// <summary>World 场景是否真的装着(读装配层的加载句柄,不猜场景名)。</summary>
        private bool WorldSceneLoaded()
        {
            if (_boot == null) return false;
            var f = typeof(BootRoot).GetField("_worldLoadHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            var handle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
            return handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded
                   && handle.Result.Scene.isLoaded;
        }

        private IEnumerator UnloadSceneIfLoaded(string fieldName)
        {
            if (_boot == null) yield break;

            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                var f = typeof(BootRoot).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                handle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
            }
            catch (Exception) { yield break; }

            if (!handle.IsValid()) yield break;
            var op = Addressables.UnloadSceneAsync(handle);
            if (op.IsValid()) yield return op.Task;
        }

        private static readonly string[] StyleSheetPaths =
        {
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoMarks.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoImplement.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss",
        };

#if UNITY_EDITOR
        private static void AttachProductionStyleSheets(VisualElement panelRoot)
        {
            foreach (string path in StyleSheetPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !panelRoot.styleSheets.Contains(sheet))
                    panelRoot.styleSheets.Add(sheet);
            }
        }
#endif
    }
}
