// 多世界 / 存档册 **视觉走查**(2026-11-04 · 用户指令「实际去看看目前的存档和启动界面是否符合视觉预期」)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:九份生产 USS + 生产 `PanelSettings.asset`(`Assets/Gameplay.UI/Skeuomorphic/`)。
//     全部经 `AssetDatabase`(编辑器腿)—— 否则量出来的形态与生产无关。
// 写:**仅图形模式写截图** ⇒ `unity/Logs/shots/mw_*.png`(gitignored 指定暂存区)。
//     ⚠️ batch 结构性零写盘(`WaitForEndOfFrame` 在无渲染帧的 batch 下永不触发)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件判什么 / 不判什么
// ─────────────────────────────────────────────────────────────────────────────
// 判:① 真启动序把**真目录**里的世界喂到主菜单上(行数 / 文案 / 删除态的形态);
//     ② 路径上**没有任何未处理错误日志**(Unity Test Framework 默认把 `Debug.LogError`
//        与未捕获异常判红 —— 这就是「console 有没有红错」的机械判据,不靠肉眼)。
// 不判:观感(纸纹 / 字号 / 墨色)—— 那是 `[L]` 人工面。本件负责**把画面留下来**。
//
// 七帧:
//   mw_01_menu_two_worlds   真启动序 · 两个世界:继续诊治 / 第 1 局 / 新起一局 / 删一局
//   mw_02_delete_select     删除态 · 选目标(每世界一行 + 返回)
//   mw_03_delete_confirm    删除态 · 待确认(选中那行变「再按一次确认 · 第 N 局」)
//   mw_04_saveslots_fresh   存档册 · 新局形态(固定视图:退出 / 自动两槽有档,手动槽空)
//   mw_05_saveslots_manual  存档册 · 手动留过一档(固定视图)
//   mw_06_menu_first_run    **空根**首次启动:单行「开始诊治」
//   mw_07_saveslots_realboot 存档册 · **真 7a 数据**(启动序 + 首份存档之后,生产 provider 开册)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>多世界主菜单 + 存档册的视觉走查(只留画面,不判观感)。</summary>
    public sealed class MultiWorldVisualWalkthrough
    {
        private const float RootWaitSeconds = 5f;
        private const float BootTimeoutSeconds = 60f;

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private GameObject _cameraGo;
        private RenderTexture _target;
        private SkeuoRuntimeDriver _driver;

        private GameObject _bootGo;
        private BootRoot _boot;
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("MultiWorldWalkthrough");

#if UNITY_EDITOR
            _panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
#endif
            if (_panelSettings == null) _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            else _panelSettings = UnityEngine.Object.Instantiate(_panelSettings);
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = 1f;

            _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _panelSettings.targetTexture = _target;

            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitSeconds)
                {
                    Assert.Fail("[夹具红] 面板 5s 未就绪");
                    yield break;
                }
                yield return null;
            }

            AttachProductionStyleSheets(_doc.rootVisualElement);

            _cameraGo = new GameObject("WalkthroughCamera");
            var camera = _cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            camera.cullingMask = 0;
            // ⚠️ 相机必须指向同一张 RT,否则 UI 逐帧累积(空环 2026-11-04 已踩过)。
            //    batch 下 RT 没有图形设备 ⇒ 只在图形模式挂。
            if (!Application.isBatchMode) camera.targetTexture = _target;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadBootScenes();

            if (_bootGo != null) UnityEngine.Object.Destroy(_bootGo);
            if (_cameraGo != null) UnityEngine.Object.Destroy(_cameraGo);
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            if (!string.IsNullOrEmpty(_saveRoot))
            {
                try { if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true); }
                catch (Exception) { /* 清场失败不影响判定 */ }
            }

            _boot = null; _bootGo = null; _driver = null; _saveRoot = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // ①–③ 真启动序 · 多世界主菜单 + 删除态
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator walkthrough_mainMenu_twoWorlds_and_deleteStates()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "dyjq_mwshot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);

            var catalog = new WorldCatalog(_saveRoot);
            SeedWorld(catalog, 1, new byte[] { 1, 2, 3 });
            SeedWorld(catalog, 2, new byte[] { 4, 5 });
            catalog.Touch(1, 100L);
            catalog.Touch(2, 200L);                     // w2 更新 ⇒ 菜单第一行是它

            _bootGo = new GameObject("BootRoot_MultiWorldWalkthrough");
            _boot = _bootGo.AddComponent<BootRoot>();
            _boot.BootWithMainMenu = true;
            _boot.SaveRootOverride = _saveRoot;           // **根**目录:多世界形态
            // 刻意不设 `SaveBasePathOverride` —— 那是单世界夹具缝。

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!_boot.IsMainMenuOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(_boot.IsMainMenuOpen,
                $"{BootTimeoutSeconds}s 内主菜单未摊开 —— 启动序第 2.95 步没走到");

            for (int i = 0; i < 5; i++) yield return null;

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "[前置] 当前场景级屏须是主菜单");
            Assert.That(menu.RowCount, Is.EqualTo(4), "[前置] 两个世界 + 新起一局 + 删一局");
            Debug.Log($"[MW] 主菜单 rows={menu.RowCount}: " +
                      $"0={menu.RowTextAt(0)} / 1={menu.RowTextAt(1)} / 2={menu.RowTextAt(2)} / 3={menu.RowTextAt(3)}");
            LogFocus("menu-shown");
            yield return Shot("mw_01_menu_two_worlds");

            // 删除态 · 选目标
            menu.ActivateRow(3);
            Assert.That(menu.Mode, Is.EqualTo(MainMenuMode.DeleteSelect), "[前置] 须进删除态");
            yield return null;
            yield return null;
            Debug.Log($"[MW] 删除态(选目标) rows={menu.RowCount}: {menu.RowTextAt(0)} / {menu.RowTextAt(1)} / {menu.RowTextAt(2)}");
            LogFocus("delete-select");
            yield return Shot("mw_02_delete_select");

            // 删除态 · 待确认(同一行再按 = 真删,所以这里只截「已待确认」那一步)
            menu.ActivateRow(0);
            Assert.That(menu.Mode, Is.EqualTo(MainMenuMode.DeleteConfirm), "[前置] 须进待确认");
            yield return null;
            yield return null;
            Debug.Log($"[MW] 删除态(待确认) rows={menu.RowCount}: {menu.RowTextAt(0)} / {menu.RowTextAt(1)} / {menu.RowTextAt(2)}");
            LogFocus("delete-confirm");
            yield return Shot("mw_03_delete_confirm");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④–⑤ 存档册(世界内那本册子;它的语义本轮未改,这里复核形态没退化)
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator walkthrough_saveSlots_freshAndManual()
        {
            var fresh = new List<SaveSlotView>
            {
                // 退出槽 / 自动槽有档,手动槽还空着 —— 「刚开一局、正常退出过一次」的形态。
                new SaveSlotView(1, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(2, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(3, known: true, hasSave: false, tick: 0L, dayNumber: 0, isNight: false),
            };
            _driver.SaveSlotViewProvider = () => fresh;

            Assert.That(_driver.OpenModal(ModalId.SaveSlots), Is.True, "[前置] 存档册须开得出来");
            var book = _driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(book, "[前置] 当前屏须是存档册");

            yield return null;
            yield return null;
            Debug.Log($"[MW] 存档册(新局)page={book.CurrentPage + 1}/{book.PageCount} rows={book.SlotRowCount}");
            yield return Shot("mw_04_saveslots_fresh");

            // 手动留过一档
            var manual = new List<SaveSlotView>
            {
                new SaveSlotView(1, known: true, hasSave: true, tick: 12000L, dayNumber: 2, isNight: false),
                new SaveSlotView(2, known: true, hasSave: true, tick: 12000L, dayNumber: 2, isNight: false),
                new SaveSlotView(3, known: true, hasSave: true, tick: 10800L, dayNumber: 2, isNight: false),
                new SaveSlotView(4, known: true, hasSave: false, tick: 0L, dayNumber: 0, isNight: false),
            };
            _driver.SaveSlotViewProvider = () => manual;
            book.SetSlotViews(manual);
            // ⚠️ 生产路径在 `SkeuoRuntimeDriver.OnTickEdge` 里重画后**会**落一次焦
            //    (`WritableRow.Focus()`)。走查若只调 `SetSlotViews` 就绕过了那一步,
            //    截出来的「环没了」是**夹具伪影**,不是缺陷(2026-11-04 实测分辨过)。
            //    ⇒ 这里补同一步,让这一帧等于玩家写完一档后真正看到的样子。
            book.WritableRow?.Focus();

            yield return null;
            yield return null;
            Debug.Log($"[MW] 存档册(含手动槽)page={book.CurrentPage + 1}/{book.PageCount} rows={book.SlotRowCount}");
            yield return Shot("mw_05_saveslots_manual");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥–⑦ 首次启动(空根)+ **真 7a 数据**的存档册
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator walkthrough_firstRun_menuAndRealSaveSlots()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "dyjq_mwshot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);         // 空根:一个世界都没有

            _bootGo = new GameObject("BootRoot_FirstRunWalkthrough");
            _boot = _bootGo.AddComponent<BootRoot>();
            _boot.BootWithMainMenu = true;
            _boot.SaveRootOverride = _saveRoot;

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!_boot.IsMainMenuOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(_boot.IsMainMenuOpen, "[前置] 主菜单须摊开");
            for (int i = 0; i < 5; i++) yield return null;

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "[前置] 当前场景级屏须是主菜单");
            Assert.That(menu.RowCount, Is.EqualTo(1), "[前置] 没有世界 ⇒ 单行");
            Debug.Log($"[MW] 首次启动 rows={menu.RowCount}: {menu.RowTextAt(0)}");
            LogFocus("first-run-menu");
            yield return Shot("mw_06_menu_first_run");

            menu.ActivateRow(0);                          // 「开始诊治」= 新起一局

            deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(ReadBooted(), $"{BootTimeoutSeconds}s 内启动序须完成");

            // 7a 的写盘是**异步**的 ⇒ 等文件真落盘(否则册子开出来是空的,归因会错)
            string autoSave = Path.Combine(_saveRoot, "w1", "save_auto.bin");
            deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(autoSave) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(File.Exists(autoSave),
                "新局的首份存档须落盘 —— 本节要看的就是**真数据**下的册子");

            // **不注入固定视图**:`BootRoot` 已在启动序里挂好 `SaveSlotViewProvider`(生产路径)
            Assert.That(_driver.OpenModal(ModalId.SaveSlots), Is.True, "[前置] 存档册须开得出来");
            var book = _driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(book, "[前置] 当前屏须是存档册");
            for (int i = 0; i < 3; i++) yield return null;

            Debug.Log($"[MW] 真档存档册 page={book.CurrentPage + 1}/{book.PageCount} rows={book.SlotRowCount}");
            LogFocus("saveslots-real");
            yield return Shot("mw_07_saveslots_realboot");
        }

        // ── 装置 ──────────────────────────────────────────────────────────

        /// <summary>启动序是否完成(`_booted` 是私有字段 —— 与本仓既有 PlayMode 夹具同法)。</summary>
        private bool ReadBooted()
            => _boot != null
               && (bool)typeof(BootRoot).GetField("_booted",
                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                   .GetValue(_boot);

        /// <summary>
        /// 打出当前**焦点对象**(诊断,不改行为)。
        /// <para>为什么要它:无指针 UI 上「焦点在不在」决定 Submit 是否派发;
        /// 图里看不到黄铜环可能是「没聚焦」,也可能是「有焦点但样式没画」——
        /// 这两者要求完全不同的修法,不能靠看图猜。</para>
        /// </summary>
        private void LogFocus(string tag)
        {
            var fc = _doc.rootVisualElement.focusController;
            var fe = fc?.focusedElement as VisualElement;
            if (fe == null) { Debug.Log($"[MW] focus/{tag} = <null>"); return; }
            var label = fe.Q<Label>();
            Debug.Log($"[MW] focus/{tag} = name={fe.name} text={(label != null ? label.text : "<no label>")}");
        }

        private static void SeedWorld(WorldCatalog catalog, int seq, byte[] saveBytes)
        {
            string dir = catalog.DirectoryOf(seq);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "save_3.bin"), saveBytes);
        }

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
                var f = typeof(BootRoot).GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

        /// <summary>把生产样式表挂到面板根(编辑器腿;player 里整段被 `#if` 排除)。</summary>
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

        private IEnumerator Shot(string name)
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            yield return null;

            string dir = Path.Combine(Application.dataPath, "..", "Logs", "shots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".png");

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            Debug.Log($"[MW] 已存帧 {_target.width}x{_target.height} ⇒ {path}");
        }
    }
}
