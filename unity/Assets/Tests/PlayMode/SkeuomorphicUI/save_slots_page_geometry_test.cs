// 存档册**纸页几何**(PlayMode · 2026-11-04 · `TD-skeuo-07` 的解除动作 ③)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么要有这一件(与既有 7b 夹具的分工)
// ─────────────────────────────────────────────────────────────────────────────
// 既有 7b 夹具判的全是**结构**:行数 / 行序 / 动作去向 / 页容量 / 页码文本。
// 没有一条判**形态** —— 所以「纸页塌成顶部一条细带、行浮在无底色的世界上」这个
// 形态活了整整一轮没被发现(`TD-skeuo-07`)。
// 本件补的就是那一面:用**引擎真算出来的布局框**判「行在不在纸里、纸包不包得住行」,
// 而不是判「某个 class 挂没挂上」。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据(每条都可证伪)
// ─────────────────────────────────────────────────────────────────────────────
// ① `row.parent` 必须是**纸页**(挂 `paper` class 的那一层)—— 这正是旧形态的反面:
//    旧实现里纸页是行的**兄弟**,行的父亲是屏根 ⇒ ① 直接红。
// ② 纸页的 worldBound **包住**每一行的 worldBound(几何包含,不是「父级关系」的等价说法:
//    父级可以 height=0,包不住)。
// ③ 纸页高 ≥ 行高之和 —— 防「纸页 = 一条细带」复发(旧形态实测就是这条不成立)。
// ④ 页码在纸页**之内**且在其**下半部** —— 「页脚」不是「左上角」。
// ⑤ 正控前置:行高 > 0、纸页高 > 0 —— 否则 ②③ 在退化尺寸上恒真(空洞正控)。
//
// ⚠️ 本件只判**几何**,不判观感(颜色 / 纸纹 / 焦点环长相是 `[L]` 人工面)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// **读**:九份生产 USS(`Assets/Gameplay.UI/Skeuomorphic/*.uss`)+ 生产 `PanelSettings.asset`。
//   **为什么必须读真件**:几何全部来自 USS 的 `padding` / `min-height` / `border-width` /
//   `-unity-slice-*`。把这几个值内联进夹具 ⇒ 判的就不是「生产 USS 算出来的布局」,
//   而是夹具自己写的数字(自指判据)。USS 改坏了夹具仍绿 —— 那正是本件要防的形态。
// **写**:零写盘。

#if UNITY_EDITOR   // 生产 USS / PanelSettings 是编辑器资产(`AssetDatabase` 取);player 里整类不存在
using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>存档册的纸页必须**真的**把行包在里面(几何判据,不是 class 判据)。</summary>
    [TestFixture]
    public sealed class save_slots_page_geometry_test
    {
        private const float RootWaitSeconds = 5f;

        /// <summary>九份生产拟物样式表(与 `Boot.unity` 预摆里拖的那一组同名同序)。</summary>
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

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private SkeuoRuntimeDriver _driver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("SaveSlotsGeometry_UnderTest");

            var production = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
            Assert.IsNotNull(production,
                "[前置] 生产 PanelSettings 必须存在 —— 否则量出来的布局与生产无关");

            // 运行时副本 + 1:1 像素:生产面板是 `ScaleWithScreenSize`(参考 1920),
            // 测试 Game 视图远比它小 ⇒ 布局被整体缩放,几何判据会引入一个与屏无关的倍率。
            _panelSettings = Object.Instantiate(production);
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = 1f;

            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitSeconds)
                {
                    Assert.Fail("[夹具红] 面板 5s 未就绪 —— 没有面板就没有布局,几何判据无从成立");
                    yield break;
                }
                yield return null;
            }

            foreach (string path in StyleSheetPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                Assert.IsNotNull(sheet, $"[前置] 生产样式表缺失:{path}");
                if (!_doc.rootVisualElement.styleSheets.Contains(sheet))
                    _doc.rootVisualElement.styleSheets.Add(sheet);
            }

            // 10 行 ⇒ 2 页(第 1 页 6 行 + 页码)。行要有档,否则行字为空但几何仍在。
            var views = new List<SaveSlotView>();
            for (uint seq = 1; seq <= 10; seq++)
                views.Add(new SaveSlotView(seq, known: true, hasSave: true,
                                           tick: 36000L * seq, dayNumber: seq, isNight: seq % 2 == 1));
            IReadOnlyList<SaveSlotView> fixedViews = views;
            _driver.SaveSlotViewProvider = () => fixedViews;

            Assert.IsTrue(_driver.OpenModal(ModalId.SaveSlots), "[前置] 存档册须开得出来");

            // 布局在下一帧才结算(本帧读到的是上一轮)
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
        }

        /// <summary>当前开着的存档册屏(经 driver 的公开观测面 <c>Screen</c> 取,不反射私有字段)。</summary>
        private SaveSlotsScreen Screen()
        {
            Assert.AreEqual(ModalId.SaveSlots, _driver.CurrentModal, "[前置] 册须是当前模态");
            var screen = _driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(screen, "[前置] driver 当前屏须是存档册(`OpenModalCore` 把 `_screen` 换成模态屏)");
            return screen;
        }

        [UnityTest]
        public IEnumerator test_rows_areChildrenOfThePaperPage()
        {
            SaveSlotsScreen screen = Screen();
            int rows = screen.SlotRowCount;
            Assert.Greater(rows, 0, "[正控] 本页须有行 —— 空页上「行在纸里」恒真(空洞正控)");

            for (int i = 0; i < rows; i++)
            {
                VisualElement row = screen.RowAt(i);
                Assert.IsNotNull(row, $"第 {i} 行须存在");
                Assert.IsNotNull(row.parent, $"第 {i} 行须有父级");
                Assert.IsTrue(row.parent.ClassListContains("paper"),
                    $"第 {i} 行的父级必须是**纸页**(挂 `paper`)。" +
                    $"实测父级 class = [{string.Join(".", row.parent.GetClasses())}] —— " +
                    "旧形态(`TD-skeuo-07`)正是行的父亲为屏根、纸页只是兄弟");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_paperPageBounds_coverEveryRow()
        {
            SaveSlotsScreen screen = Screen();
            int rows = screen.SlotRowCount;
            Assert.Greater(rows, 0, "[正控] 本页须有行");

            Rect page = screen.RowAt(0).parent.worldBound;
            Assert.Greater(page.height, 0f, "[正控] 纸页高须 > 0 —— 否则包含判据在退化尺寸上恒真");

            for (int i = 0; i < rows; i++)
            {
                Rect row = screen.RowAt(i).worldBound;
                Assert.Greater(row.height, 0f, $"[正控] 第 {i} 行高须 > 0");
                Assert.IsTrue(page.Contains(row.min) && page.Contains(row.max),
                    $"纸页 {page} 必须**包住**第 {i} 行 {row}(几何包含,不是父级关系)");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_paperPageHeight_isNotAThinBand()
        {
            SaveSlotsScreen screen = Screen();
            int rows = screen.SlotRowCount;
            Assert.Greater(rows, 0, "[正控] 本页须有行");

            float rowSum = 0f;
            for (int i = 0; i < rows; i++) rowSum += screen.RowAt(i).worldBound.height;

            float pageHeight = screen.RowAt(0).parent.worldBound.height;
            Assert.GreaterOrEqual(pageHeight, rowSum,
                $"纸页高 {pageHeight} 必须 ≥ 行高之和 {rowSum} —— " +
                "旧形态下纸页高只等于自身内边距(塌成顶部一条细带)");
            Assert.Greater(pageHeight, rowSum,
                $"纸页高 {pageHeight} 必须**严格大于**行高之和 {rowSum} —— " +
                "差值 = 纸的上下内边距;等于行高之和说明行顶到纸边、纸没有页边(§5.2 页缘形态)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_pageMark_sitsInsideThePage_atItsFoot()
        {
            SaveSlotsScreen screen = Screen();
            Label mark = screen.PageMark;
            Assert.IsNotNull(mark, "[前置] 页码元件须存在");

            Rect page = screen.RowAt(0).parent.worldBound;
            Rect m = mark.worldBound;
            Assert.Greater(m.height, 0f, "[正控] 页码高须 > 0");
            Assert.IsTrue(page.Contains(m.min) && page.Contains(m.max),
                $"页码 {m} 必须落在纸页 {page} 之内 —— 旧形态里它贴在屏根左上角");
            Assert.Greater(m.center.y, page.center.y,
                $"页码必须在纸页的**下半部**(页脚):页码中心 y={m.center.y},纸页中线 y={page.center.y}");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_nextPageEdge_isLaidOut_onlyWhenThereIsANextPage()
        {
            SaveSlotsScreen screen = Screen();
            Assert.IsTrue(screen.HasNextPage, "[前提] 10 行 / 每页 6 行 ⇒ 第一页必有下一页");
            Assert.IsTrue(screen.HasPreviousPage == false, "[前提] 第一页没有上一页");

            // 「还有下一页」由**纸边厚度**传达(§5.2)—— 所以它不是 display 的措辞问题:
            // 不占位时 worldBound 必须是零尺寸,占位时必须有可见宽度。
            Assert.Greater(screen.NextEdge.worldBound.width, 0f,
                "第一页有下一页 ⇒ 右侧页缘须占位(纸边厚度)");
            Assert.AreEqual(0f, screen.PreviousEdge.worldBound.width,
                "第一页没有上一页 ⇒ 左侧页缘**不占位**");

            yield return null;
        }
    }
}
#endif
