// 存档 / 设置 / 炮制 三块**视觉走查**(2026-11-04 · 用户指令「写脚本去实际看看效果」)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:九份生产 USS + 生产 `PanelSettings.asset`(`Assets/Gameplay.UI/Skeuomorphic/`)。
//     全部经 `AssetDatabase`(编辑器腿)—— 不走这两样,量出来的形态与生产无关。
// 写:**仅图形模式写截图** ⇒ `unity/Logs/shots/sv_*.png`(gitignored 指定暂存区)。
//     ⚠️ batch 结构性零写盘(`WaitForEndOfFrame` 在无渲染帧的 batch 下永不触发)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件判什么 / 不判什么
// ─────────────────────────────────────────────────────────────────────────────
// 判:**链跑通了**(每一步 `CurrentModal` / `CurrentPage` / 行数如实),
//     否则截出来的图无从归因(「图不对」到底是屏坏了还是根本没开)。
// 不判:观感(颜色 / 纸纹 / 字号)—— 那是 `[L]` 人工面。本件只负责**把画面留下来**。
//
// 七帧:
//   sv_01_pause             暂停壳三行(继续 / 存档位 / 调治)
//   sv_02_settings          从第三行开的设置壳
//   sv_03_saveslots_last    开册 = 高水位页(13 行 ⇒ 第 3 页)
//   sv_04_saveslots_first   翻回第 1 页
//   sv_05_saveslots_zoom150 文本缩放 150% ⇒ 每页 6 → 4 行(转册)
//   sv_06_mainmenu_notice   主菜单 + 「上次会话未完整保存」
//   sv_07_processing        炮制屏(三行候选)
//
// ⚠️ 生产相机清屏 + 1920×1080 RenderTexture:同 `ModalToggleVisualWalkthrough` 的四处坑
//    (裸 PanelSettings 全黑 / ScaleWithScreenSize 缩到 0.31 / Game 视图太小 / 无相机则重影)。

using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    [TestFixture]
    public sealed class SaveAndSettingsVisualWalkthrough
    {
        private const float RootWaitSeconds = 5f;

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private bool _panelSettingsOwned;
        private GameObject _cameraGo;
        private RenderTexture _target;
        private SkeuoRuntimeDriver _driver;
        private IReadOnlyList<SaveSlotView> _fixedViews;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("SaveAndSettingsWalkthrough");

#if UNITY_EDITOR
            _panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
#endif
            if (_panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettingsOwned = true;
            }
            else
            {
                _panelSettings = UnityEngine.Object.Instantiate(_panelSettings);
                _panelSettingsOwned = true;
            }
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
            // 🔴 **2026-11-04 第五处坑(本轮看图才发现的)**:上一版走查建了清屏相机却
            //    **没把它指向 RenderTexture** ⇒ 它清的是 Game 视图,RT 从头到尾没人清 ⇒
            //    UI 的绘制**逐帧累积**在 RT 上:第二帧起就能同时看到「暂停壳 + 设置带」,
            //    换页时看到「两张册页卡叠在一起」。逻辑断言全绿而图在骗人。
            //    ⇒ 相机必须 `targetTexture = RT`(与面板同一个目标),每帧先清再画。
            // ⚠️ batch 下 RT 没有图形设备:相机真去渲它 ⇒ `RenderTexture.Create failed`
            //    (实测:整条 PlayMode 变红)。截图路径本就由 `Shot` 的 batch 守卫短路,
            //    故这里也只在图形模式下挂。
            if (!Application.isBatchMode) camera.targetTexture = _target;

            // 13 行 ⇒ 3 页(每页 6);全部有档 ⇒ 高水位在第 13 行 ⇒ 开册落在末页(§7.1)。
            var views = new List<SaveSlotView>();
            for (uint seq = 1; seq <= 13; seq++)
            {
                views.Add(new SaveSlotView(seq, known: true, hasSave: true,
                                           tick: 36000L * seq, dayNumber: seq, isNight: seq % 2 == 1));
            }
            _fixedViews = views;
            _driver.SaveSlotViewProvider = () => _fixedViews;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_cameraGo != null) UnityEngine.Object.Destroy(_cameraGo);
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
            if (_panelSettingsOwned && _panelSettings != null)
                UnityEngine.Object.Destroy(_panelSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator walkthrough_pause_settings_saveslots_mainmenu_processing()
        {
            // ── ① 暂停壳三行 ──────────────────────────────────────────────
            Assert.That(_driver.OpenModal(ModalId.Pause), Is.True, "[前置] 暂停壳须开得出来");
            var pause = _driver.Screen as PauseScreen;
            Assert.That(pause, Is.Not.Null, "[前置] 当前屏须是暂停壳");
            Assert.That(pause.RowCount, Is.EqualTo(3), "[前置] 本轮改成三行");
            yield return null;
            DumpTree(_doc.rootVisualElement, 0, "pause");
            yield return Shot("sv_01_pause");

            // ── ② 第三行「调治」⇒ 设置壳 ─────────────────────────────────
            _driver.PauseSink = new DriverPauseCommandSink(_driver);
            pause.Activate(2);
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SettingsShell),
                "第三行必须真的换到设置壳");
            yield return null;
            yield return null;
            DumpTree(_doc.rootVisualElement, 0, "settings");
            yield return Shot("sv_02_settings");

            // 回世界(设置 → 暂停 → 世界)
            _driver.CloseModal();
            _driver.CloseModal();
            yield return null;
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None), "[前置] 已回世界");

            // ── ③ 存档册:开在高水位页(末页)────────────────────────────
            Assert.That(_driver.OpenModal(ModalId.SaveSlots), Is.True);
            var book = _driver.Screen as SaveSlotsScreen;
            Assert.That(book, Is.Not.Null, "[前置] 当前屏须是存档册");
            yield return null;
            yield return null;
            Debug.Log($"[SV] 存档册 page={book.CurrentPage + 1}/{book.PageCount} rows={book.SlotRowCount}");
            DumpTree(_doc.rootVisualElement, 0, "saveslots");
            yield return Shot("sv_03_saveslots_last");

            // ── ④ 翻回第 1 页 ────────────────────────────────────────────
            while (book.PreviousPage()) { }
            yield return null;
            yield return null;
            Assert.That(book.CurrentPage, Is.EqualTo(0), "[前置] 已翻到第一页");
            Debug.Log($"[SV] 翻回第 1 页 rows={book.SlotRowCount}");
            yield return Shot("sv_04_saveslots_first");

            // ── ⑤ 文本缩放 150% ⇒ 每页 6 → 4 行(转册)───────────────────
            book.TextScale = 1.5f;
            Assert.That(book.PageCapacity, Is.EqualTo(SaveSlotsScreen.PageSizeZoomed),
                "150% ⇒ 容量降档(§5.1)");
            // ⚠️ 改 `TextScale` 只改容量读数,**不会自动重画** —— 必须再喂一次视图,
            //    否则截图上是「页码 1/4 但每页仍 6 行」的自相矛盾形态(首轮实测)。
            book.SetSlotViews(_fixedViews);
            yield return null;
            yield return null;
            // `SetSlotViews` 会按高水位重算页码(§7.1)⇒ 落在末页;要看到「每页 4 行」
            // 的容量形态,翻回第 1 页再截。
            while (book.PreviousPage()) { }
            yield return null;
            yield return null;
            Debug.Log($"[SV] 150%: page={book.CurrentPage + 1}/{book.PageCount} rows={book.SlotRowCount}");
            DumpTree(_doc.rootVisualElement, 0, "zoom150");
            yield return Shot("sv_05_saveslots_zoom150");

            _driver.CloseModal();
            yield return null;

            // ── ⑥ 主菜单 + 「上次会话未完整保存」────────────────────────
            Assert.That(_driver.ShowSceneLevelScreen("MainMenu", true, true), Is.True,
                "[前置] 主菜单场景级屏须开得出来");
            // ⚠️ 场景级屏**不在** `Screen`(那是模态屏)—— 读 `CurrentScreen`。
            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.That(menu, Is.Not.Null,
                $"[前置] 场景级屏须是主菜单,实得 {_driver.CurrentScreen?.GetType().Name ?? "null"}");
            Assert.That(menu.SessionIncompleteNoticeShown, Is.True, "[前置] 提示须摊开");
            yield return null;
            yield return null;
            DumpTree(_doc.rootVisualElement, 0, "mainmenu");
            yield return Shot("sv_06_mainmenu_notice");

            // ── ⑦ 炮制屏:三行候选 ───────────────────────────────────────
            // ⚠️ 先收场景级屏 —— 生产上炮制屏是从世界里按 E 开的,主菜单早已收起;
            //    不收就截出「菜单还在背后」的假形态(本件第一版正是如此)。
            _driver.DismissSceneLevelScreen();
            yield return null;
            Assert.That(_driver.OpenModal(ModalId.Processing), Is.True);
            var craft = _driver.Screen as Processing18Screen;
            Assert.That(craft, Is.Not.Null,
                $"[前置] 当前屏须是炮制屏,实得 {_driver.Screen?.GetType().Name ?? "null"}");
            craft.Bind(new Recipe[]
            {
                new Recipe { Ordinal = 1, RecipeId = "herb_soup",   Owner = RecipeOwner.Process,
                             Inputs = new[] { new RecipeEntry(new ItemKey("herb_a", ProcessingState.Raw), 2) },
                             Outputs = new[] { new RecipeEntry(new ItemKey("soup", ProcessingState.Extracted), 1) },
                             DurationTicks = 20, SkillGate = 0, MinQuality = 1 },
                new Recipe { Ordinal = 2, RecipeId = "herb_pill",   Owner = RecipeOwner.Process,
                             Inputs = new[] { new RecipeEntry(new ItemKey("herb_b", ProcessingState.Raw), 2) },
                             Outputs = new[] { new RecipeEntry(new ItemKey("pill", ProcessingState.Extracted), 1) },
                             DurationTicks = 30, SkillGate = 0, MinQuality = 1 },
                new Recipe { Ordinal = 3, RecipeId = "herb_powder", Owner = RecipeOwner.Process,
                             Inputs = new[] { new RecipeEntry(new ItemKey("herb_c", ProcessingState.Raw), 2) },
                             Outputs = new[] { new RecipeEntry(new ItemKey("powder", ProcessingState.Extracted), 1) },
                             DurationTicks = 40, SkillGate = 0, MinQuality = 1 },
            }, new AlwaysAdmissible(), actorId: 7, consumedQualities: new[] { 3 });
            Assert.That(craft.RowCount, Is.EqualTo(3), "[前置] 三条候选须都出现");
            yield return null;
            yield return null;
            DumpTree(_doc.rootVisualElement, 0, "processing");
            yield return Shot("sv_07_processing");
        }

        // ── 装置 ──────────────────────────────────────────────────────────

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

        /// <summary>把树与**解析后**的样式打出来(诊断;截图只能看出「不对」,看不出为什么)。</summary>
        private static void DumpTree(VisualElement el, int depth, string tag)
        {
            // ⚠️ 诊断代码**不得让走查变红**:首轮实测在存档册子树上抛过
            //    `NullReferenceException`(解析后的背景对象里某个 Unity 对象是「假 null」),
            //    结果只存了前两帧就中断。诊断面一律吞异常 —— 它只负责「多一条线索」。
            try
            {
                var rs = el.resolvedStyle;
                string cls = el.ClassListContains("paper") ? "paper" : string.Join(".", el.GetClasses());
                var bi = rs.backgroundImage;
                string img = "none";
                if (bi.texture != null) img = bi.texture.name;
                else if (bi.sprite != null) { try { img = "sprite:" + bi.sprite.name; } catch { img = "sprite:<gone>"; } }
                else if (bi.renderTexture != null) img = "rt";
                Debug.Log($"[TREE/{tag}] {new string(' ', depth * 2)}{(string.IsNullOrEmpty(el.name) ? "(anon)" : el.name)}"
                          + $" [{cls}] bound={el.worldBound} bg={rs.backgroundColor} img={img}");
            }
            catch (System.Exception ex)
            {
                Debug.Log($"[TREE/{tag}] <dump failed: {ex.GetType().Name}>");
            }

            for (int i = 0; i < el.hierarchy.childCount; i++) DumpTree(el.hierarchy[i], depth + 1, tag);
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
            Debug.Log($"[SV] 已存帧 {_target.width}x{_target.height} ⇒ {path}");
        }

        /// <summary>准入桩:全部可发起(本件只看屏形态,不判准入)。</summary>
        private sealed class AlwaysAdmissible : IProcessingAdmissionQuery
        {
            public bool IsAdmissible(in Recipe recipe, int actorId, IReadOnlyList<int> consumedQualities)
                => true;
        }
    }
}
