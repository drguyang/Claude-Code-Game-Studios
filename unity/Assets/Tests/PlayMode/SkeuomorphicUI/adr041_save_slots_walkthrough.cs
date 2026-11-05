// 存档册(7b)覆盖语义的**视觉走查**(2026-11-04 · 用户指令「存档册图形走查,三帧」)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:九份生产 USS + 生产 `PanelSettings.asset`(`Assets/Gameplay.UI/Skeuomorphic/`),
//     全部经 `AssetDatabase`(编辑器腿)—— 否则量出来的形态与生产无关。
// 写:**仅图形模式写截图** ⇒ `unity/Logs/shots/wb_*.png`(gitignored 指定暂存区)。
//     ⚠️ batch 结构性零写盘(`WaitForEndOfFrame` 在无渲染帧的 batch 下永不触发)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 四帧(ADR-041 §D5 的三种行形态 + 满页复验)
// ─────────────────────────────────────────────────────────────────────────────
//   wb_01_empty_slot   空槽行 —— 格线无字(按下去 = 落笔,不再走载入死胡同)
//   wb_02_armed        已有档行第一次 Submit ⇒ 行内两枚墨字「启封 / 重写」(默认焦点在启封)
//   wb_03_locked       已锁行(读档即锁)⇒ 同上,但「启封」灰置、「重写」仍可用
//   wb_04_full_page    满页(6 槽 + 留档)+ 已锁行二段态 —— 2026-11-04 修订后的复验:
//                      行文字须**仍在**(看得出盖的是哪一页)、两枚墨字与它**同行靠右**、
//                      页高与未二段态时**一致**(二段态不再多吃一行)
//
// 不判:观感(纸纹 / 字号 / 墨色)—— 那是 `[L]` 人工面。本件负责**把画面留下来**。

using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>存档册覆盖语义的三帧走查(只留画面,不判观感)。</summary>
    public sealed class Adr041SaveSlotsWalkthrough
    {
        private const float RootWaitSeconds = 5f;

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private GameObject _cameraGo;
        private RenderTexture _target;
        private SkeuoRuntimeDriver _driver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("Adr041SaveSlotsWalkthrough");

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

            // ⚠️ 相机必须指向同一张 RT,否则 UI 逐帧累积(2026-11-04 踩过);
            //    batch 下没有图形设备 ⇒ 只在图形模式挂。
            _cameraGo = new GameObject("WalkthroughCamera");
            var camera = _cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            camera.cullingMask = 0;
            if (!Application.isBatchMode) camera.targetTexture = _target;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_cameraGo != null) UnityEngine.Object.Destroy(_cameraGo);
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);
            _doc = null; _panelSettings = null; _go = null; _cameraGo = null; _target = null; _driver = null;
            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // 三帧
        // ══════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator walkthrough_threeRowForms_empty_armed_locked()
        {
            // ── ① 空槽行 ───────────────────────────────────────────────
            var fresh = new List<SaveSlotView>
            {
                new SaveSlotView(1, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(2, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(3, known: true, hasSave: false, tick: 0L),
            };
            SaveSlotsScreen book = OpenBook(fresh);
            yield return null; yield return null;
            Debug.Log($"[WB] 空槽帧 rows={book.SlotRowCount} 第三行空={book.RowAt(2)?.IsEmpty}");
            yield return Shot("wb_01_empty_slot");

            // ── ② 二段态(已有档行第一次 Submit)────────────────────────────
            var occupied = new List<SaveSlotView>
            {
                new SaveSlotView(1, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(2, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(3, known: true, hasSave: true, tick: 10800L, dayNumber: 2),
                new SaveSlotView(4, known: true, hasSave: false, tick: 0L),
            };
            book.SetSlotViews(occupied);
            yield return null;
            book.Activate(2);                       // 第三行 = 槽 3(有档)⇒ 展开二段态
            Assert.That(book.ArmedRowIndex, Is.EqualTo(2), "[前置] 须进入二段态");
            yield return null; yield return null;
            Debug.Log($"[WB] 二段态帧 armed={book.ArmedRowIndex}");
            yield return Shot("wb_02_armed");

            // ── ③ 已锁行 ───────────────────────────────────────────────
            var locked = new List<SaveSlotView>
            {
                new SaveSlotView(1, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(2, known: true, hasSave: true, tick: 6000L, dayNumber: 1, isNight: true),
                new SaveSlotView(3, known: true, hasSave: true, tick: 10800L, dayNumber: 2, isLocked: true),
                new SaveSlotView(4, known: true, hasSave: false, tick: 0L),
            };
            book.SetSlotViews(locked);
            yield return null;
            book.Activate(2);                       // 槽 3 = 有档且已锁
            // 🔁 **2026-11-06(用户裁定「锁搬到写侧」+「已锁换灰底、不写字」)**:
            //    已锁行**照常进二段态** —— 「启封」可用、「重写」灰置且退出焦点序。
            //    旧形态(不进二段态 + 页角注 + 行末「· 已锁」)三处全部作废。
            Assert.That(book.ArmedRowIndex, Is.EqualTo(2),
                "[判据] 已锁行也要进二段态(锁挡写,不挡读)");
            Assert.That(book.RowAt(2).OpenChoice, Is.Not.Null, "[前置] 两枚墨字须出现");
            Assert.That(book.RowAt(2).RewriteChoice, Is.Not.Null, "[前置] 两枚墨字须出现");
            Assert.That(book.RowAt(2).OpenChoice.enabledSelf, Is.True, "[判据] 「启封」可用");
            Assert.That(book.RowAt(2).RewriteChoice.enabledSelf, Is.False, "[判据] 「重写」灰置");
            Assert.That(book.RowAt(2).RewriteChoice.focusable, Is.False,
                "[判据] 灰置须退出焦点序 —— 否则手柄停在按不出结果的墨字上");
            Assert.That(book.RowAt(2).ClassListContains("save-slot-locked"), Is.True,
                "[判据] 已锁 ⇒ 整行灰底(唯一状态通道)");
            Assert.That(book.NoticeText, Is.Empty, "[判据] 不再出页角注");
            Assert.That(book.RowAt(2).MarkText, Does.Not.Contain("已锁"), "[判据] 行上不再写状态字样");
            yield return null; yield return null;
            Debug.Log($"[WB] 已锁帧 armed={book.ArmedRowIndex} 行文字「{book.RowAt(2).MarkText}」" +
                      $" grayClass={book.RowAt(2).ClassListContains("save-slot-locked")}");
            yield return Shot("wb_03_locked");

            // ── ④ 满页 + 二段态(2026-11-04 修订复验)───────────────────────
            //   判据三件:① 行文字仍在(「叁 · 第贰日」不该被墨字挤掉);
            //   ② 两枚墨字与该行文字**同一行**(纵向不另起一行 ⇒ 页高不变);
            //   ③ 满页 6 槽 + 留档仍放得下(二段态不吃容量)。
            var fullPage = new List<SaveSlotView>();
            for (int i = 1; i <= 6; i++)
            {
                fullPage.Add(new SaveSlotView(
                    (uint)i, known: true, hasSave: true,
                    tick: 6000L * i, dayNumber: i, isNight: (i % 2) == 1));
            }
            // ⚠️ 2026-11-06:本段仍用**未锁**行测「二段态不得抹掉行文字」;
            //    已锁行的二段态另由上面的 `wb_03_locked` 段覆盖(它现在也进二段态了)。
            fullPage[2] = new SaveSlotView(3, known: true, hasSave: true,
                tick: 18000L, dayNumber: 3, isNight: true);

            book.SetSlotViews(fullPage);
            yield return null;
            float heightBeforeArm = book.RowAt(2).resolvedStyle.height;
            book.Activate(2);
            yield return null; yield return null;

            SaveSlotItem armedRow = book.RowAt(2);
            // 🔁 2026-11-05:期望值加「已锁」—— 本轮让锁**有文字面**(`SaveSlotItem.RowText`)。
            //    本条断的是「二段态不得抹掉行文字」,加尾缀不改变这个意图,只是把新形态钉住。
            Assert.That(armedRow.MarkText, Is.EqualTo("第叁日 夜"),
                "[前置] 二段态不得抹掉行文字 —— 玩家要看得见盖的是哪一页");
            float heightAfterArm = armedRow.resolvedStyle.height;
            // 量尺(常驻观测面):二段态的四个盒子高度 + 行高的前后值。
            var markEl = armedRow.Q(SaveSlotItem.MarkElementName);
            var host = armedRow.OpenChoice?.parent;
            Debug.Log($"[WB][量尺] row={heightAfterArm:F1}(二段态前 {heightBeforeArm:F1}) " +
                      $"mark={markEl?.resolvedStyle.height:F1} host={host?.resolvedStyle.height:F1} " +
                      $"open={armedRow.OpenChoice?.resolvedStyle.height:F1} " +
                      $"rewrite={armedRow.RewriteChoice?.resolvedStyle.height:F1} " +
                      $"openLabel={armedRow.OpenChoice?.Q<Label>()?.resolvedStyle.height:F1} " +
                      // 🔁 2026-11-05:补**解析后的字号** —— 只量盒子高不足以判「字号有没有生效」
                      //    (15px 与 18px 的字盒高实测同为 35,若只看 35 会误判成「CSS 没吃到」)。
                      $"openFontSize={armedRow.OpenChoice?.Q<Label>()?.resolvedStyle.fontSize:F1} " +
                      $"markFontSize={markEl?.resolvedStyle.fontSize:F1} " +
                      $"flexDir={armedRow.resolvedStyle.flexDirection}");
            // 判据 = 「二段态不多占一行」,**不是**「一格像素都不许动」:
            //   实测残余 1px(row 的 border-bottom 落在两态的盒模型口径差上,
            //   二段态实测 45.0 vs 44.0)。多占**一行**的量级是 +44 ⇒ 2px 容差不会放过它。
            Assert.That(heightAfterArm, Is.EqualTo(heightBeforeArm).Within(2f),
                $"[前置] 二段态不得改变行高(前 {heightBeforeArm:F1} / 后 {heightAfterArm:F1})" +
                " —— 否则满页多一行会挤出页外");
            Debug.Log($"[WB] 满页帧 armed={book.ArmedRowIndex} 行高 {heightBeforeArm:F1}⇒{heightAfterArm:F1} " +
                      $"行文字「{armedRow.MarkText}」");
            yield return Shot("wb_04_full_page");
        }

        // ── 装置 ────────────────────────────────────────────────────────

        private SaveSlotsScreen OpenBook(IReadOnlyList<SaveSlotView> views)
        {
            _driver.SaveSlotViewProvider = () => views;
            Assert.That(_driver.OpenModal(ModalId.SaveSlots), Is.True, "[前置] 存档册须开得出来");
            var book = _driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(book, "[前置] 当前屏须是存档册");
            book.SetSlotViews(views);
            return book;
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
            Debug.Log($"[WB] 已存帧 {_target.width}x{_target.height} ⇒ {path}");
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
