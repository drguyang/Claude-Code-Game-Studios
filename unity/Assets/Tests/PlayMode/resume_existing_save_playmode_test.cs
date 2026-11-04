// 「第二次进入已有存档」的**真启动序**夹具(2026-11-04 · 用户报「再进入已有存档,会出现 console 很多红色报错」)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 它补的是哪一跳
// ─────────────────────────────────────────────────────────────────────────────
// 此前所有 PlayMode 夹具只走「新局」:要么手搓一份**故意非法**的档(只证明「尝试了且失败被记录」),
// 要么根本不读档。于是**「档是真的、读回来也是真的」**这条路径没有任何自动判据 ——
// 恰恰是玩家每天走的那条。
//
// 本件两段式:
//   ① 空存档根 → 首次启动 → 「开始诊治」→ 启动序跑完 → 等**真**首份存档落盘(`FirstSessionSave`);
//   ② 拆掉整个世界(卸场景 / 销毁 BootRoot)→ **同一个存档根**再启动一次 →
//      主菜单第一行应是「继续诊治」→ 按下去 → 启动序须无**未处理错误日志**地读完档。
//
// ⚠️ Unity Test Framework 默认把「未处理 `LogError` / 未捕获异常」判红 ⇒ **本件绿 = 那段 console 是干净的**,
//    这是机械判据,不是我看日志没看见红。
// ⚠️ 本件只判「能读回来且不红」;读回来的**内容**对不对(事件流 / 折叠 / 结构)归 7a 的既有一致性夹具。

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
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>「第二次启动 · 继续已有存档」的真启动序判据。</summary>
    public sealed class resume_existing_save_playmode_test
    {
        private const float BootTimeoutSeconds = 60f;
        private const float PanelTimeoutSeconds = 10f;

        private GameObject _uiGo;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private SkeuoRuntimeDriver _driver;
        private GameObject _bootGo;
        private BootRoot _boot;
        private string _saveRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadBootScenes();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == "Player") UnityEngine.Object.Destroy(go);

            if (_bootGo != null) UnityEngine.Object.Destroy(_bootGo);
            if (_uiGo != null) UnityEngine.Object.Destroy(_uiGo);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(FindObjectsSortMode.None))
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);

            if (!string.IsNullOrEmpty(_saveRoot))
            {
                try { if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true); }
                catch (Exception) { /* 清场失败不影响判定 */ }
            }

            _boot = null; _bootGo = null; _driver = null; _doc = null;
            _panelSettings = null; _uiGo = null; _saveRoot = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator test_secondLaunch_continueExistingSave_readsBackWithoutErrorLogs()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "dyjq_resume_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);

            // ── ① 首次启动:空根 ⇒ 「开始诊治」⇒ 真实首份存档 ──────────────
            yield return BootUntilMenuAppears();
            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "启动序应摊开主菜单");
            Assert.AreEqual(1, menu.RowCount, "[第一段] 空根 ⇒ 单行");
            Debug.Log($"[RESUME] 第一段 rows={menu.RowCount}: {menu.RowTextAt(0)}");

            menu.ActivateRow(0);                       // 「开始诊治」
            yield return WaitForBoot();

            // ⚠️ **让世界真跑一段再存档**:首份存档写在 tick 0,拿它读回来证明不了「时钟定位」
            //    (0 与「没定位」在观测上同形)。这里等到 tick ≥ 20 再写自动档 ——
            //    读档后 tick 若仍停在个位数,后续每笔写入都会早于流尾(BCD-码-3)。
            yield return WaitForTickAtLeast(20L);
            long tickAtSave = CurrentTick();
            Assert.GreaterOrEqual(tickAtSave, 20L, "[第一段] 前置:tick 须已推进");

            SaveService saves = Services()?.SaveService;
            Assert.IsNotNull(saves, "[第一段] 7a 须已装配");
            saves.Checkpoint(SaveService.AutoSaveSlot);
            // ⚠️ 带超时的 `WaitForIdle(int)` 是 **internal**(拼写面只给 Sim.Codec 的测试装配),
            //    故这里轮询公开的 `Outstanding` —— PlayMode 夹具里不能用无限等待(挂住无输出)。
            yield return WaitForWriteQueueIdle(saves);
            Assert.IsNull(saves.WriteQueue.LastFailure, "[第一段] 写盘不得失败");
            Debug.Log($"[RESUME] 存档时刻 tick = {tickAtSave}");

            var catalog = new WorldCatalog(_saveRoot);
            string worldDir = catalog.DirectoryOf(1);
            string autoSave = Path.Combine(worldDir, SaveService.AutoSaveFileName);
            yield return WaitForFile(autoSave);
            Assert.IsTrue(File.Exists(autoSave), "[第一段] 首份存档须落盘,否则第二段无档可读");
            Debug.Log($"[RESUME] 首份存档 = {new FileInfo(autoSave).Length} B @ {worldDir}");

            // ── 拆掉整个世界(模拟关掉游戏再开)────────────────────────────
            yield return UnloadBootScenes();
            UnityEngine.Object.Destroy(_bootGo);
            UnityEngine.Object.Destroy(_uiGo);
            UnityEngine.Object.Destroy(_panelSettings);
            _boot = null; _bootGo = null; _uiGo = null; _panelSettings = null; _driver = null; _doc = null;
            for (int i = 0; i < 3; i++) yield return null;

            // ── ② 第二次启动:同一存档根 ⇒ 「继续诊治」⇒ 读回 ──────────────
            yield return BootUntilMenuAppears();

            var menu2 = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu2, "第二次启动也应摊开主菜单");
            // N 个世界 ⇒ N + 2 行(世界行 + 「新起一局」+「删一局」,AC-MM-11)
            Assert.AreEqual(3, menu2.RowCount, "[第二段] 一个世界 ⇒ 3 行");
            Assert.AreEqual(MainMenuScreen.ContinueText, menu2.RowTextAt(0),
                "[第二段] 有档 ⇒ 第一行必须是「继续诊治」(否则玩家点的是新局)");
            Debug.Log($"[RESUME] 第二段 rows={menu2.RowCount}: {menu2.RowTextAt(0)}");

            menu2.ActivateRow(0);                      // 「继续诊治」= 读最近档
            yield return WaitForBoot();

            long tickAfterLoad = CurrentTick();
            Debug.Log($"[RESUME] 读档后 tick = {tickAfterLoad}(存档时刻 = {tickAtSave})");
            Assert.GreaterOrEqual(tickAfterLoad, tickAtSave,
                "读档后逻辑时钟必须定位到存档 tick —— 否则之后每笔 sim 写入都早于流尾," +
                "`EventStream.Append` 以 BCD-码-3 逐条拒收(console 刷屏 + 世界形同停摆)");

            Debug.Log("[RESUME] 读档后启动序已完成 —— 若这段 console 有红,UF 会把本件判红");
        }

        // ── 装置(与 multi_world_boot_wiring_test 同款)────────────────────

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
                    Assert.Fail($"{PanelTimeoutSeconds}s 内面板未 attach");
                yield return null;
            }

#if UNITY_EDITOR
            AttachProductionStyleSheets(_doc.rootVisualElement);
#endif

            _bootGo = new GameObject("BootRoot_Resume");
            _boot = _bootGo.AddComponent<BootRoot>();
            _boot.BootWithMainMenu = true;
            _boot.SaveRootOverride = _saveRoot;

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!_boot.IsMainMenuOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(_boot.IsMainMenuOpen,
                $"{BootTimeoutSeconds}s 内主菜单未摊开 —— 启动序第 2.95 步没走到");

            for (int i = 0; i < 5; i++) yield return null;
        }

        private IEnumerator WaitForBoot()
        {
            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), $"启动序须在 {BootTimeoutSeconds}s 内完成");
            for (int i = 0; i < 5; i++) yield return null;
        }

        /// <summary>服务袋(反射;`BootRoot._services` 是私有字段)。</summary>
        private CompositionRootServices Services()
            => (CompositionRootServices)typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_boot);

        private long CurrentTick() => Services()?.TickDriver?.CurrentTick ?? -1L;

        /// <summary>等逻辑时钟推进到指定 tick(世界真的在跑)。</summary>
        private IEnumerator WaitForTickAtLeast(long tick, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (CurrentTick() < tick && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>等 7a 的写队列排空(轮询公开 `Outstanding`;带超时,不无限等待)。</summary>
        private static IEnumerator WaitForWriteQueueIdle(SaveService saves, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (saves.WriteQueue.Outstanding > 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static IEnumerator WaitForFile(string path, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private bool ReadBooted()
            => _boot != null
               && (bool)typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance)
                   .GetValue(_boot);

        private IEnumerator UnloadBootScenes()
        {
            if (_boot == null) yield break;
            yield return UnloadHandle("_mainMenuLoadHandle");
            yield return UnloadHandle("_worldLoadHandle");
        }

        private IEnumerator UnloadHandle(string fieldName)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid = false;
            try
            {
                var f = typeof(BootRoot).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                handle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
                valid = handle.IsValid();
            }
            catch (Exception) { valid = false; }

            if (!valid) yield break;

            var op = UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(handle);
            while (!op.IsDone) yield return null;
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

        private static void AttachProductionStyleSheets(VisualElement panelRoot)
        {
#if UNITY_EDITOR
            foreach (string path in StyleSheetPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !panelRoot.styleSheets.Contains(sheet))
                    panelRoot.styleSheets.Add(sheet);
            }
#endif
        }
    }
}
