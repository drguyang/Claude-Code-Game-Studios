// 主菜单 · **装配级**夹具(2026-11-01 · `design/ux/main-menu-42.md` AC-MM-01…09)
//
// ▍为什么是装配级
//   `MainMenuScreen` 单测只能证明「这个类画了一行、按了会调 sink」。
//   spec 的判据全在**启动序的形状**上:菜单出现在**世界装载之前**、落焦在开屏时发生、
//   两条输入腿汇到同一出口、确认后世界才来 —— 这些只有从 `BootRoot` 的**真启动序**出发才成立。
//
// ▍形态
//   预摆 UI 壳(UIDocument + PanelSettings + SkeuoRuntimeDriver)→ 起 `BootRoot`
//   (与 `Boot.unity` 同一条:`FindOrCreateSkeuoUi` 走「认领已有壳」分支),
//   并打开 `BootWithMainMenu` + 覆写 `SaveBasePathOverride`(存档三态摆得出来,
//   且**不碰玩家真实存档目录**)。
//
// ▍AC 对照
//   01 纸面 + 「继续诊治 / 开始诊治」可读   → `test_bootWithMenu_showsSingleRow_andLandsFocus`
//   02 开屏即落焦(无需输入)                → 同上
//   05 无「按 X 继续」一类提示浮层           → 同上(菜单子树内**恰一个**文本元素)
//   08-a 无存档 ⇒「开始诊治」,行仍可用      → 同上
//   09-a 无存档 ⇒ 开新局(不尝试读档)        → `test_newGameBranch_...`
//   03 键盘 Enter 激活                     → `test_keyboardSubmit_...`
//   04 手柄南键激活(与 03 同一出口)        → `test_gamepadSouth_...`
//   06 零转场                              → `test_menuHasNoTransition_...`
//   07 装载 additive / Boot 常驻件仍在      → 同上(+ 静态半在 EditMode `main_menu_static_test`)
//   08-b 有存档 ⇒「继续诊治」,行仍可用      → `test_hasSave_showsContinueText_...`
//   09-b 有存档 ⇒ 尝试读最近一档            → 同上
//   08-c 查询失败 ⇒ 按无存档处理            → `test_saveQueryFailure_...`
//
// ▍⚠️ 本件**不**主张「读档字节级正确」—— 夹具给的是**故意非法**的存档字节,
//   断言的是「启动序**尝试**了那一档且失败被记录」。字节级正确性归 7a 的 EditMode 夹具。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>主菜单的装配级判据(AC-MM-01…09 的运行期半边)。</summary>
    public sealed class main_menu_wiring_test
    {
        private const float BootTimeoutSeconds = 60f;
        private const float PanelTimeoutSeconds = 10f;

        private GameObject _root;
        private GameObject _uiGo;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private SkeuoRuntimeDriver _driver;
        private BootRoot _boot;
        private string _saveDir;

        private Keyboard _kb;
        private bool _kbAdded;
        private Gamepad _pad;
        private bool _padAdded;
        private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private InputSettings.BackgroundBehavior _savedBg;
        private bool _inputEnvSaved;
        private readonly List<string> _tempDirs = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_inputEnvSaved)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                InputSystem.settings.backgroundBehavior = _savedBg;
                _inputEnvSaved = false;
            }

            if (_kbAdded && _kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            _kb = null; _kbAdded = false;

            if (_padAdded && _pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
            _pad = null; _padAdded = false;

            var player = GameObject.Find("Player");
            if (player != null) UnityEngine.Object.Destroy(player);

            // 场景句柄兜底:菜单场景(未确认就结束的用例)与世界场景(已确认)。
            yield return UnloadSceneIfLoaded("_mainMenuLoadHandle");
            yield return UnloadSceneIfLoaded("_worldLoadHandle");

            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_uiGo != null) UnityEngine.Object.Destroy(_uiGo);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            // 按类型清残留驱动(`FindOrCreateSkeuoUi` 会按类型复用 —— 留一只下来就会串场)
            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(FindObjectsSortMode.None))
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);

            foreach (string dir in _tempDirs)
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
                catch (Exception) { /* 夹具清场失败不影响判定 */ }
            }
            _tempDirs.Clear();

            _doc = null; _panelSettings = null; _uiGo = null; _driver = null; _boot = null; _root = null; _saveDir = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-01 / 02 / 05 / 08-a
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_bootWithMenu_showsSingleRow_andLandsFocus()
        {
            yield return BootUntilMenuAppears();

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "启动序应摊开主菜单(场景级屏)");
            Assert.AreSame(_driver, ReadDriver(), "BootRoot 必须认领预摆壳(否则测的是另一只壳)");
            Assert.IsFalse(_driver.BlocksPlay,
                "主菜单**不得**是挡路模态 —— 世界尚未装载,`BlocksPlay` 只对「世界已在跑」有意义(spec §1)");
            // 🔁 2026-11-02:常驻屏占位已退役(`_screenName` 默认空)⇒ 启动时 `Screen` 为 null。
            // **要证的属性没变**:场景级屏与模态容器是**两个容器**,互不替换。
            // ⇒ 先开一个模态,再断它没被菜单顶掉 —— 这样测的仍是那条缝,且不再依赖启动形态。
            Assert.IsTrue(_driver.OpenModal(ModalId.Casebook), "前置:脉案模态应能打开");
            Assert.IsInstanceOf<CasebookScreen>(_driver.Screen,
                "场景级屏不得替换模态容器里的屏(它俩是两个容器,不是同一格)");
            Assert.IsInstanceOf<MainMenuScreen>(_driver.CurrentScreen,
                "反过来也成立:开模态不得顶掉场景级屏");
            _driver.CloseModal();

            // AC-MM-01:纸面 + 一行 + 文字**可读**(非空、可见)
            var paper = menu.Root.Q<VisualElement>("main-menu-paper");
            Assert.IsNotNull(paper, "纸面(`SkeuoElement.Paper`)必须在");
            var row = menu.Root.Q<VisualElement>("main-menu-continue");
            Assert.IsNotNull(row, "唯一那一行必须在");
            Assert.IsTrue(row.focusable && row.tabIndex == 0, "行必须可聚焦");

            var labels = new List<Label>();
            menu.Root.Query<Label>().ToList(labels);
            Assert.AreEqual(1, labels.Count,
                $"菜单子树内**恰一个**文本元素 —— 实得 {labels.Count} 个(spec §5「一张纸、一行」)");
            Assert.AreEqual(MainMenuScreen.StartText, labels[0].text,
                "AC-MM-08:无存档 ⇒ 文案「开始诊治」");
            Assert.IsNotEmpty(labels[0].text.Trim(), "文字必须可读(空串 = 画了个空行)");
            Assert.AreNotEqual(DisplayStyle.None, labels[0].resolvedStyle.display, "文字必须可见");

            // ⚠️ 结构前提(spec §4):菜单出现时**世界尚未装载** —— 没有 tick、没有病人。
            //    把菜单闸门挪到 World 装载之后 ⇒ 本条红(定序判据)。
            Assert.IsFalse(WorldSceneLoaded(),
                "主菜单必须出现在**世界装载之前**(`main-menu-42.md §4` 的结构前提)");

            // AC-MM-02:开屏即落焦,无需任何输入
            Assert.That(Focused(), Is.SameAs(row),
                "开屏后引擎焦点必须落在唯一那一行 —— 没有指针时,没落焦 = 手柄玩家按什么都走不动");

            // AC-MM-05:无「按 X 继续」一类按键提示浮层 —— 已由「恰一个文本元素且等于行文案」覆盖;
            // 再用词面复核一次(防将来以图标 / 文本混排的形式加回来)
            Assert.IsFalse(Regex.IsMatch(labels[0].text, @"按|Press|Enter|键"),
                "行文案不得含按键提示(spec §6「不制造第二层选择」)");
        }

        // ══════════════════════════════════════════════════════════════════
        // spec §5「纸面下是纯色底,不是游戏画面」(2026-11-01 补轮)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 背板判据:**在、不透明、铺满、绘制在纸面之前、不参与拾取**。
        /// <para>⚠️ 本判据**刻意不断言具体色值** —— 色值归美术轮(spec §5 留白),
        /// 断言字面量会把一次美术调色变成一次测试红。要保证的属性是「后面看不见游戏画面」,
        /// 那由**不透明 + 铺满**表达,不由某个十六进制数表达。</para>
        /// <para>变异面:删掉背板元素 ⇒ 本条红;把 USS 改成半透明 ⇒ 本条红;
        /// 把背板 `Add` 到 host **之后** ⇒ 先后断言红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_menuBackdrop_isOpaqueCoversPanel_andPaintsBehindPaper()
        {
            yield return BootUntilMenuAppears();

            var menu = (MainMenuScreen)_driver.CurrentScreen;
            Assert.IsNotNull(menu);
            VisualElement backdrop = menu.Backdrop;
            Assert.IsNotNull(backdrop, "spec §5:世界未装载时纸面下必须是**纯色底**(背板元素必须在)");

            // ① 不透明 —— 半透明会让 Boot 场景的天空盒 / 地面透出来,「不是游戏画面」即不成立
            Color bg = backdrop.resolvedStyle.backgroundColor;
            Assert.AreEqual(1f, bg.a, 0.001f,
                $"背板必须**不透明**(实际 alpha={bg.a})—— 半透明 ⇒ 天空盒透出 ⇒ spec §5 不成立");

            // ② 铺满面板(绝对定位 inset 0 的落点)
            VisualElement panel = _driver.GetUiRoot();
            Assert.IsNotNull(panel, "[前置] 面板必须在");
            Rect pb = panel.worldBound;
            Rect bb = backdrop.worldBound;
            Assert.GreaterOrEqual(bb.width, pb.width - 1f, $"背板宽度须铺满面板(背板 {bb.width:F0} / 面板 {pb.width:F0})");
            Assert.GreaterOrEqual(bb.height, pb.height - 1f, $"背板高度须铺满面板(背板 {bb.height:F0} / 面板 {pb.height:F0})");

            // ③ 绘制次序:纸面之后画 = 盖住纸面 ⇒ 必须排在 host **之前**
            VisualElement root = menu.Root;
            VisualElement host = root.Q<VisualElement>("main-menu-host");
            Assert.IsNotNull(host, "[前置] 纸面容器必须在");
            // ⚠️ 先钉「是 root 的儿子」:`IndexOf` 对非子元素返回 -1,而 `-1 < 任何下标`
            //    恒成立 ⇒ 只写 `Less` 的话,背板被挂到别的父节点下也会**假绿**。
            Assert.GreaterOrEqual(root.IndexOf(backdrop), 0,
                "背板必须是场景级屏容器的**直接子元素**(挂到别处 ⇒ 下面的次序判据无从谈起)");
            Assert.Less(root.IndexOf(backdrop), root.IndexOf(host),
                "背板必须绘制在纸面**之前**(兄弟次序 = 绘制次序)—— 排在后面会把纸面盖住");

            // ④ 不参与拾取(背板铺满全屏,若可拾取会吞掉行的点击)
            Assert.AreEqual(PickingMode.Ignore, backdrop.pickingMode,
                "背板必须 `pickingMode = Ignore`,否则它会吞掉那一行的点击(spec §7 指针路径)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-09-a 无存档分支
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_newGameBranch_continueLoadsWorldWithoutReadingAnySave()
        {
            yield return BootUntilMenuAppears();
            Assert.IsNull(_boot.ContinuedFromSlot, "[前置] 开屏时还没有确认过,不该有目标档");

            (_driver.CurrentScreen as MainMenuScreen)?.Activate();

            yield return WaitForBoot();

            Assert.IsFalse(_boot.IsMainMenuOpen, "确认后菜单必须收起");
            Assert.IsNull(_boot.ContinuedFromSlot, "AC-MM-09:无存档 ⇒ 走「开新局」分支,不尝试读任何档");
            Assert.IsNull(_boot.ContinueError, "新局分支不该产生读档异常");
            Assert.IsTrue(WorldSceneLoaded(), "确认后世界场景必须已 additive 装载");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-03 键盘腿
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_keyboardSubmit_activatesMenuExactlyOnce_andEntersWorld()
        {
            yield return BootUntilMenuAppears();

            var counting = InstallCountingSink();
            Assert.AreEqual(0, counting.Count, "[前置] 还没按键");

            yield return HoldKey(Key.Enter, 4);

            Assert.AreEqual(1, counting.Count,
                $"按一次 Enter 应**恰好**激活一次(实际 {counting.Count})—— 0 次 = 键没走到 UI 提交," +
                ">1 次 = 边沿化坏了");
            yield return WaitForBoot();
            Assert.IsTrue(WorldSceneLoaded(), "键盘确认后世界必须装载");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-04 手柄腿
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_gamepadSouth_activatesMenuExactlyOnce_andEntersWorld()
        {
            yield return BootUntilMenuAppears();

            var counting = InstallCountingSink();
            Assert.AreEqual(0, counting.Count, "[前置] 还没按键");

            yield return HoldButton(GamepadButton.South, 4);

            Assert.AreEqual(1, counting.Count,
                $"手柄南键按一次应**恰好**激活一次(实际 {counting.Count})—— " +
                $"Gamepad.current={(Gamepad.current == null ? "null" : Gamepad.current.name)}");
            yield return WaitForBoot();
            Assert.IsTrue(WorldSceneLoaded(), "手柄确认后世界必须装载");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-06 / 07 零转场 + additive
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_menuHasNoTransition_andBootSurvivesWorldLoad()
        {
            yield return BootUntilMenuAppears();

            // AC-MM-06:菜单子树内**没有任何**非零 transition(spec §9 零动画;
            // 出现与离屏瞬时 —— 引入转场 = 给启动期发明一套时间基准)
            var menu = (MainMenuScreen)_driver.CurrentScreen;
            var withTransition = new List<string>();
            menu.Root.Query<VisualElement>().ForEach(el =>
            {
                foreach (TimeValue duration in el.resolvedStyle.transitionDuration)
                {
                    if (duration.value != 0f)
                    {
                        withTransition.Add($"{el.name}({el.GetType().Name}) = {duration.value}");
                        break;
                    }
                }
            });
            Assert.IsEmpty(withTransition,
                "主菜单不得有任何过渡动画(AC-MM-06)—— 命中:" + string.Join(", ", withTransition));

            // AC-MM-07:确认后 Boot 场景与常驻件仍在(Single 会连根拔起)
            Scene bootSceneBefore = _root.scene;
            menu.Activate();
            yield return WaitForBoot();

            Assert.IsTrue(bootSceneBefore.IsValid() && bootSceneBefore.isLoaded,
                "Boot 场景必须仍然加载着(ADR-023 §① 永不用 Single)");
            Assert.IsTrue(_root != null && _root.scene == bootSceneBefore,
                "BootRoot 自身必须仍在 Boot 场景内");

            // ⚠️ 夹具里没有 `Boot.unity`,相机是启动序第 5 步**运行时建**的(`CameraRig`),
            //    没打 MainCamera 标签 ⇒ 不能读 `Camera.main`,要读装配位。
            var rig = ReadCameraRig();
            Assert.IsNotNull(rig, "相机(Boot 常驻件)必须在");
            Assert.IsNotNull(rig.Camera, "CameraRig 上必须挂着相机");
            Assert.IsNotNull(rig.Camera.GetComponent<AudioListener>(),
                "AudioListener 必须在相机上(ADR-020 §七 单挂点)");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length,
                "AudioListener 全局**恰一个**(Single 场景装载会把 Boot 那份一起换掉)");
            Assert.IsNotNull(ReadTickDriver(), "tick 驱动(Boot 常驻件)必须在");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-08-b / 09-b 有存档分支
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_hasSave_showsContinueText_andAttemptsMostRecentSave()
        {
            // 「有档」只要求**存在**,内容由读档侧校验 ⇒ 这里给的是故意非法字节
            _saveDir = NewSaveDir();
            File.WriteAllBytes(Path.Combine(_saveDir, SaveService.ExitSaveFileName), new byte[] { 1, 2, 3, 4 });

            yield return BootUntilMenuAppears();

            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu);
            var row = menu.Root.Q<VisualElement>("main-menu-continue");
            Assert.IsNotNull(row);

            Assert.AreEqual(MainMenuScreen.ContinueText, menu.RowText,
                "AC-MM-08:有存档 ⇒ 文案「继续诊治」");
            Assert.IsTrue(row.focusable, "有档态下行**仍须**可用(spec §6:不存在禁用态)");
            Assert.That(Focused(), Is.SameAs(row), "有档态下同样必须落焦");

            // AC-MM-09-b:确认 ⇒ 尝试读「最近一档」= 退出保存(退出钩子写的那份)
            LogAssert.Expect(LogType.Error, new Regex("继续诊治读档失败"));
            menu.Activate();
            yield return WaitForBoot();

            Assert.IsTrue(_boot.ContinuedFromSlot.HasValue,
                "有档 ⇒ 必须走「继续」分支(不得静默退化成开新局)");
            Assert.AreEqual(SaveService.ExitSaveSlot.SlotSeq, _boot.ContinuedFromSlot.Value.SlotSeq,
                "「最近一档」的判据 = 退出保存优先(`SaveService.TryGetMostRecentSave`)");
            Assert.IsNotNull(_boot.ContinueError,
                "夹具给的是非法字节 ⇒ 读档**必然失败**;失败被记录 = 确实走到了读档,不是空转");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-MM-08-c 查询失败
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator test_saveQueryFailure_fallsBackToNoSave()
        {
            _saveDir = NewSaveDir();
            // 目录存在但**不可读** ⇒ `Directory.EnumerateFiles` 抛 ⇒ 查得「否」而非崩
            Chmod(_saveDir, "000");
            if (CanStillEnumerate(_saveDir))
            {
                Chmod(_saveDir, "755");
                Assert.Ignore("本环境不实施目录权限(疑似 root)⇒ 无法注入查询失败;本条未运行,不计绿");
            }

            yield return BootUntilMenuAppears();

            var saves = ReadSaveService();
            Assert.IsNotNull(saves, "7a 存档服务应已装配");
            Assert.IsNotNull(saves.LastSaveQueryError,
                "查询失败必须被**记录**(门 A 下 7a 无日志面 ⇒ 事实出口在这里)");
            Assert.AreEqual(MainMenuScreen.StartText, ((MainMenuScreen)_driver.CurrentScreen).RowText,
                "AC-MM-08:查询失败按「无存档」处理(保守方向 = 开新局,不把空档当档读)");
            Assert.IsNull(_boot.ContinuedFromSlot, "查询失败不应产生「继续」目标");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装置
        // ══════════════════════════════════════════════════════════════════

        /// <summary>九份拟物样式表(与 `SkeuoRuntimeDriver._styleSheets` 的生产清单同源)。</summary>
        private static readonly string[] StyleSheetPaths =
        {
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoMarks.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoImplement.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss",
        };

        /// <summary>把生产样式表挂到面板根(编辑器腿;player 里由 `#if` 排除调用点)。</summary>
        private static void AttachProductionStyleSheets(VisualElement panelRoot)
        {
            // ⚠️ 方法体也要 `#if`:只守调用点不够 —— `UnityEditor` 命名空间在 player 里
            //    不存在,未守卫的方法体照样 CS0234(`casebook_reading_inline_test` 同一坑)。
#if UNITY_EDITOR
            foreach (string path in StyleSheetPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !panelRoot.styleSheets.Contains(sheet))
                    panelRoot.styleSheets.Add(sheet);
            }
#endif
        }

        /// <summary>预摆 UI 壳 → 起 BootRoot(开主菜单断言)→ 等菜单摊开。</summary>
        private IEnumerator BootUntilMenuAppears()
        {
            SetupKeyboard();
            SetupGamepad();
            SetupInputPolicy();

            _saveDir = _saveDir ?? NewSaveDir();

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

            // ⚠️ 2026-11-01:本夹具**挂真样式表**。此前不挂(自建空壳无 `_styleSheets`),
            //    于是 `.ruled` / `.menu-backdrop` 这类 USS 规则在夹具里根本不生效 ——
            //    「背板不透明」这类判据只能在真机上验。Boot.unity 的预摆壳是**带**九份
            //    USS 的,故挂上才是生产形状。
            //    `AssetDatabase` 在 player 里不存在 ⇒ 整段 `#if UNITY_EDITOR`
            //    (与 `casebook_reading_inline_test` 同款守卫,否则 IL2CPP 构建失败)。
#if UNITY_EDITOR
            AttachProductionStyleSheets(_doc.rootVisualElement);
#endif

            _root = new GameObject("BootRoot_MainMenuWiring");
            _boot = _root.AddComponent<BootRoot>();
            // ⚠️ 必须在 Start 之前赋值(Awake 已跑;Start 在下一帧)
            _boot.BootWithMainMenu = true;
            _boot.SaveBasePathOverride = _saveDir;

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!_boot.IsMainMenuOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(_boot.IsMainMenuOpen,
                $"{BootTimeoutSeconds}s 内主菜单未摊开 —— 启动序第 2.95 步没走到");

            for (int i = 0; i < 5; i++) yield return null;   // 让落焦 / 样式生效
        }

        private IEnumerator WaitForBoot()
        {
            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), $"启动序须在 {BootTimeoutSeconds}s 内完成");
            for (int i = 0; i < 3; i++) yield return null;
        }

        private IEnumerator UnloadSceneIfLoaded(string fieldName)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid = false;
            if (_boot != null)
            {
                try
                {
                    var f = typeof(BootRoot).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                    handle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
                    valid = handle.IsValid();
                }
                catch (Exception) { valid = false; }
            }
            if (!valid) yield break;

            var unload = Addressables.UnloadSceneAsync(handle);
            while (!unload.IsDone) yield return null;
        }

        /// <summary>
        /// 把生产 sink 包一层计数(委托回原 sink)。
        /// <para>为什么要包:AC-MM-03/04 的判据是「**恰好一次**」与「两条输入腿**同一出口**」。
        /// 只看「菜单关了」分不出「关了一次」和「关了两次」—— 而两条路径各写一份判定的
        /// 失效形态正是后者。</para>
        /// </summary>
        private CountingSink InstallCountingSink()
        {
            var original = _driver.MainMenuSink;
            Assert.IsNotNull(original, "生产启动序必须已注入主菜单 sink");
            var counting = new CountingSink(original);
            _driver.MainMenuSink = counting;   // setter 会把新 sink 重绑到已摊开的屏上
            return counting;
        }

        private sealed class CountingSink : IMainMenuCommandSink
        {
            private readonly IMainMenuCommandSink _inner;
            public CountingSink(IMainMenuCommandSink inner) { _inner = inner; }
            public int Count { get; private set; }
            public void Continue() { Count++; _inner?.Continue(); }
        }

        /// <summary>引擎焦点真源(`Focusable` → `VisualElement`;非元素 ⇒ null)。</summary>
        private VisualElement Focused()
            => _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;

        private bool WorldSceneLoaded()
        {
            Scene world = SceneManager.GetSceneByName("World");
            return world.IsValid() && world.isLoaded;
        }

        private SkeuoRuntimeDriver ReadDriver()
            => typeof(BootRoot).GetField("_uiDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                   ?.GetValue(_boot) as SkeuoRuntimeDriver;

        private DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig ReadCameraRig()
            => typeof(BootRoot).GetField("_cameraRig", BindingFlags.NonPublic | BindingFlags.Instance)
                   ?.GetValue(_boot) as DaYiJingCheng.Gameplay.Presentation.Camera.CameraRig;

        private SimTickDriver ReadTickDriver()
            => typeof(BootRoot).GetField("_tickDriver", BindingFlags.NonPublic | BindingFlags.Instance)
                   ?.GetValue(_boot) as SimTickDriver;

        private SaveService ReadSaveService()
        {
            object services = typeof(BootRoot).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_boot);
            return services?.GetType().GetProperty("SaveService")?.GetValue(services) as SaveService;
        }

        private bool ReadBooted()
            => (bool)(typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance)
                          ?.GetValue(_boot) ?? false);

        // ── 存档目录装置(全部落临时目录,绝不碰玩家数据)──

        private string NewSaveDir()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "dayijing_mainmenu_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        private static void Chmod(string dir, string mode)
        {
            try
            {
                using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod", Arguments = $"{mode} {dir}",
                    UseShellExecute = false, CreateNoWindow = true,
                });
                p?.WaitForExit();
            }
            catch (Exception) { /* 无 chmod 的平台:下面的 CanStillEnumerate 会走 Ignore */ }
        }

        private static bool CanStillEnumerate(string dir)
        {
            try
            {
                using var e = Directory.EnumerateFiles(dir).GetEnumerator();
                e.MoveNext();
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (Exception) { return false; }
        }

        // ── 输入注入(与 `modal_focus_wiring_test` 同一套)──

        private void SetupKeyboard()
        {
            if (Keyboard.current == null) { _kb = InputSystem.AddDevice<Keyboard>(); _kbAdded = true; }
            else { _kb = Keyboard.current; if (!_kb.enabled) InputSystem.EnableDevice(_kb); }
        }

        private void SetupGamepad()
        {
            if (Gamepad.current == null) { _pad = InputSystem.AddDevice<Gamepad>(); _padAdded = true; }
            else { _pad = Gamepad.current; if (!_pad.enabled) InputSystem.EnableDevice(_pad); }
        }

        private void SetupInputPolicy()
        {
            if (!_inputEnvSaved)
            {
                _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                _savedBg = InputSystem.settings.backgroundBehavior;
                _inputEnvSaved = true;
            }
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }

        /// <summary>按住 N 帧再松开。</summary>
        /// <remarks>⚠️ `GamepadButton` 是**位序号**枚举 ⇒ 置位要 `1 &lt;&lt; (int)button`。</remarks>
        private IEnumerator HoldButton(GamepadButton button, int frames)
        {
            InputSystem.QueueStateEvent(_pad, new GamepadState { buttons = (ushort)(1 << (int)button) });
            for (int i = 0; i < frames; i++) yield return null;
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            for (int i = 0; i < 3; i++) yield return null;
        }

        private IEnumerator HoldKey(Key key, int frames)
        {
            var down = new KeyboardState();
            down.Press(key);
            InputSystem.QueueStateEvent(_kb, down);
            for (int i = 0; i < frames; i++) yield return null;
            InputSystem.QueueStateEvent(_kb, new KeyboardState());
            for (int i = 0; i < 3; i++) yield return null;
        }
    }
}
