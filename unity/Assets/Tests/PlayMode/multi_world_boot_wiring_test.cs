// 多世界存档 · **真启动序**装配级夹具(ADR-040 · 2026-11-04)。
//
// ▍它补的是哪一跳
//   `world_catalog_test` 证目录契约,`main_menu_worlds_test` 证屏的行与路由,
//   `world_switch_isolation_test` 证两个世界互不影响 —— 但它们都**绕开了启动序**。
//   本件从 `BootRoot` 的真启动序出发,走**多世界形态**(不给 `SaveBasePathOverride`),
//   回答那条此前无人回答的问题:「玩家在真机点得到吗」。
//
// ▍怎么做到「真形态」又不碰玩家数据
//   新缝 `SaveRootOverride`:把**存档根**指到临时目录(根下照常是 `w1/`、`w2/`)。
//   ⚠️ 它与 `SaveBasePathOverride` 不是一回事:后者把某个目录**直接当世界目录**
//      (单世界形态);本件刻意**不设**后者,否则测的仍是单世界路径。
//
// ▍两条判据
//   A. 种一个世界 → 启动序列出它 → **点「新起一局」** → 新世界目录出现,
//      且它的首份存档真的落在**自己**目录里;旧世界**逐字节不变**。
//   B. 种两个世界 → 点「删一局」→ 两次确认 → **只删选中的那个**,另一个完好,
//      菜单行数当场回落。
//
// ⚠️ 本件**不**断存档字节级正确(那归 7a 的 EditMode 夹具);种子档是**故意非法**的字节,
//   只用来让「这个世界有档」成立。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    /// <summary>多世界的真启动序判据(ADR-040 D5 / D6 / D9)。</summary>
    public sealed class multi_world_boot_wiring_test
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
            // ⚠️ 不清 `GameObject.Find` —— 它只查 active,而启动序会 `SetActive(false)` 预摆件
            //    ⇒ 清场静默失效、同名件跨测累积(`pm-teardown-find` 门点名的就是这形态)。
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
        // A. 列表 + 「新起一局」 ⇒ 新世界有自己的首份存档
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_newWorld_createsOwnDirectoryAndSave_oldWorldUntouched()
        {
            NewRoot();
            var catalog = new WorldCatalog(_saveRoot);
            Directory.CreateDirectory(catalog.DirectoryOf(1));
            string seeded = Path.Combine(catalog.DirectoryOf(1), "save_3.bin");
            File.WriteAllBytes(seeded, new byte[] { 1, 2, 3, 4 });
            catalog.Touch(1, 100L);
            string before = Sha256Of(seeded);
            // ⚠️ **存档文件集**快照,不是「那一份档的哈希」:只哈希种下去的文件,抓不到
            //    「新局往旧世界目录里多写了一份档」—— 而这正是 D5/D6 的承诺本体。
            string w1Before = SnapshotSaveFiles(catalog.DirectoryOf(1));

            yield return BootUntilMenuAppears();

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "启动序应摊开主菜单");
            Assert.AreEqual(3, menu.RowCount,
                "一个世界 ⇒ 「继续诊治」+「新起一局」+「删一局」");
            Assert.AreEqual(MainMenuScreen.ContinueText, menu.RowTextAt(0));
            Assert.AreEqual(MainMenuScreen.NewWorldText, menu.RowTextAt(1));

            menu.ActivateRow(1);                       // 「新起一局」
            yield return WaitForBoot();

            string w2 = catalog.DirectoryOf(2);
            Assert.IsTrue(Directory.Exists(w2), "新世界目录必须真的建出来(启动序按选择去装配)");

            // ⚠️ **7a 的写盘是异步的**(`Checkpoint` 入队后由写线程落盘),`_booted` 不代表
            //    文件已在盘上 —— 所以这里等**文件**而不是等帧,否则会偶发假红。
            string firstSave = Path.Combine(w2, SaveService.AutoSaveFileName);
            yield return WaitForFile(firstSave);
            Assert.IsTrue(File.Exists(firstSave),
                "新世界的首份存档必须落在**它自己**的目录里(`FirstSessionSave` 的必然结果)");
            Assert.AreEqual(before, Sha256Of(seeded),
                "旧世界的档必须逐字节不变 —— 新起一局不得触碰它");
            Assert.AreEqual(w1Before, SnapshotSaveFiles(catalog.DirectoryOf(1)),
                "旧世界的**存档文件集**必须完全不变(文件名 + 每份字节)—— 多写一份、少一份、改一份都算触碰");
        }

        // ══════════════════════════════════════════════════════════════════
        // B. 「删一局」⇒ 只删选中的那个
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_deleteWorld_throughBoot_removesOnlyChosenWorld()
        {
            NewRoot();
            var catalog = new WorldCatalog(_saveRoot);
            Directory.CreateDirectory(catalog.DirectoryOf(1));
            Directory.CreateDirectory(catalog.DirectoryOf(2));
            File.WriteAllBytes(Path.Combine(catalog.DirectoryOf(1), "save_3.bin"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(catalog.DirectoryOf(2), "save_3.bin"), new byte[] { 2 });
            catalog.Touch(1, 100L);
            catalog.Touch(2, 200L);                    // w2 更新 ⇒ 列表第一行是它
            string w1Before = SnapshotSaveFiles(catalog.DirectoryOf(1));

            yield return BootUntilMenuAppears();

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu);
            Assert.AreEqual(4, menu.RowCount, "两个世界 +「新起一局」+「删一局」");
            Assert.AreEqual("main-menu-world-2", menu.RowAt(0).name, "最近打开的世界排第一");

            menu.ActivateRow(3);                       // 「删一局」
            Assert.AreEqual(MainMenuMode.DeleteSelect, menu.Mode);
            Assert.AreEqual(3, menu.RowCount, "删除态:两个世界 + 「返回」");
            yield return null;
            // ⚠️ 无指针形态:Submit 依赖焦点 ⇒ 重建后焦点悬空 = 手柄/键盘一步也走不了。
            //    视觉走查先看到「环没了」,这条把它变成机械判据。
            Assert.AreEqual("main-menu-delete-2", FocusedName(),
                "进删除态后焦点必须落在**第一个世界**上(它是下一步要选的目标)");

            menu.ActivateRow(0);                       // 选 w2(第一次)
            Assert.AreEqual(MainMenuMode.DeleteConfirm, menu.Mode);
            Assert.IsTrue(Directory.Exists(catalog.DirectoryOf(2)), "第一次确认**不得**删除");
            yield return null;
            Assert.AreEqual("main-menu-delete-2", FocusedName(),
                "进待确认后焦点必须**留在同一个世界**上(两次确认都落在这里)");

            menu.ActivateRow(0);                       // 同一行再按(第二次)
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsFalse(Directory.Exists(catalog.DirectoryOf(2)), "第二次确认应真的删掉 w2");
            Assert.IsTrue(Directory.Exists(catalog.DirectoryOf(1)), "另一个世界必须完好");
            Assert.AreEqual(w1Before, SnapshotSaveFiles(catalog.DirectoryOf(1)),
                "删 w2 不得动 w1 的任何一份档(删整个世界是显式动作,不是「顺手清理」)");

            // 菜单被回推了新列表(屏自动退出删除态)。
            Assert.AreEqual(MainMenuMode.Normal, menu.Mode, "装配层回推列表 ⇒ 删除态作废");
            Assert.AreEqual(3, menu.RowCount, "剩一个世界 ⇒ 3 行");
            Assert.AreEqual("main-menu-world-1", FocusedName(),
                "删完回推列表后焦点也不得悬空(否则玩家按完删除就被卡在菜单上)");
        }

        // ══════════════════════════════════════════════════════════════════
        // C. 全新档:第一局必须是 w1(不是 w2)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 空根首次启动 ⇒ 第一局落在 <c>w1/</c>。
        /// <para>⚠️ 这条守的是一个**只在全新档出现**的缺陷(2026-11-04 视觉走查抓到):
        /// 启动序 1.9 的 `SaveService` 构造会建出默认世界目录 `w1/`(空),而 `TouchWorld`
        /// 又给这个还不存在的世界写索引条目 ⇒ `CreateWorld` 的高水位把 `w1` 当已用序号,
        /// 玩家的**唯一**世界被编号成「第 2 局」,且 `w1/` 永远不存在。
        /// 老档看不出来(它们本来就有 `w1/`),屏级夹具也看不出来(它们自己喂世界表)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_firstRun_newWorldLandsInW1_notW2()
        {
            NewRoot();                                  // 空根:一个世界都没有

            yield return BootUntilMenuAppears();

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "启动序应摊开主菜单");
            Assert.AreEqual(1, menu.RowCount, "没有世界 ⇒ 单行");
            Assert.AreEqual(MainMenuScreen.StartText, menu.RowTextAt(0), "无档 ⇒「开始诊治」");

            menu.ActivateRow(0);                        // 「开始诊治」= 新起一局
            yield return WaitForBoot();

            var catalog = new WorldCatalog(_saveRoot);
            Assert.IsTrue(Directory.Exists(catalog.DirectoryOf(1)),
                "全新档的第一局必须落在 w1/(幽灵序号会把玩家唯一的局编号成「第 2 局」)");
            Assert.IsFalse(Directory.Exists(catalog.DirectoryOf(2)),
                "不得跳到 w2 —— 那是启动序自己的空目录 + 幽灵索引条目合起来顶出来的号");

            string firstSave = Path.Combine(catalog.DirectoryOf(1), SaveService.AutoSaveFileName);
            yield return WaitForFile(firstSave);
            Assert.IsTrue(File.Exists(firstSave), "首份存档必须落在 w1/ 里");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装置
        // ══════════════════════════════════════════════════════════════════

        private void NewRoot()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "dyjq_mwboot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        private static string Sha256Of(string path)
        {
            using (var sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs));
        }

        /// <summary>世界目录里**存档文件**的快照(文件名 = SHA-256,按文件名排序)。</summary>
        /// <remarks>
        /// ⚠️ 只取 `save_*.bin` —— 世界目录里还有 `session_incomplete.flag`,它在「这个世界
        /// 被选中装配」时由 7a 读写,那属于**这个世界自己的状态**。本判据守的是
        /// 「另一个世界的操作**改写 / 增删了这里的存档**」,不是「目录里一个字节都不许变」。
        /// ⚠️ 用**文件集**而不是单份哈希:只哈希种下去那一份,抓不到「新局把首份存档
        /// 写进了旧世界」这种形态。
        /// </remarks>
        private static string SnapshotSaveFiles(string dir)
        {
            if (!Directory.Exists(dir)) return "<absent>";

            string[] files = Directory.GetFiles(dir, "save_*.bin");
            Array.Sort(files, StringComparer.Ordinal);

            var sb = new StringBuilder();
            for (int i = 0; i < files.Length; i++)
                sb.Append(Path.GetFileName(files[i])).Append('=').Append(Sha256Of(files[i])).Append('\n');
            return sb.ToString();
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

            _root = new GameObject("BootRoot_MultiWorld");
            _boot = _root.AddComponent<BootRoot>();
            // ⚠️ 必须在 Start 之前赋值(Awake 已跑;Start 在下一帧)
            _boot.BootWithMainMenu = true;
            _boot.SaveRootOverride = _saveRoot;          // **根**目录:多世界形态
            // 刻意**不设** SaveBasePathOverride —— 那是单世界形态的缝。

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
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>等一个文件真的落盘(7a 的写盘走异步队列,不随 `_booted` 就绪)。</summary>
        private static IEnumerator WaitForFile(string path, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>当前焦点元件的名字(<c>"&lt;null&gt;"</c> = 焦点悬空)。</summary>
        private string FocusedName()
        {
            var focused = _doc.rootVisualElement.focusController?.focusedElement as VisualElement;
            return focused != null ? focused.name : "<null>";
        }

        private bool ReadBooted()
            => _boot != null
               && (bool)typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance)
                                      .GetValue(_boot);

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

            var unload = AddressablesUnload(handle);
            yield return unload;
        }

        private static IEnumerator AddressablesUnload(AsyncOperationHandle<SceneInstance> handle)
        {
            var op = UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(handle);
            yield return op;
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
