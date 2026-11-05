// `TD-skeuo-19` 判据(焦点层 · PlayMode)—— 滚轮 / 上下键翻页时,焦点**真的**落在哪一行。
//
// ▍为什么 EditMode 判不了这一半
//   `SaveSlotsScreen.FocusRow` 走的是 `VisualElement.Focus()`,而**没有面板**的元素上它是
//   空操作(屏单元夹具即此形态)。⇒ 「一格滚轮 = 下一行」这条判据在 EditMode 里恒真也恒假,
//   只能靠真面板。逻辑层(页号变不变 / 消不消费)在 EditMode 的 `save_book_paging_test`。
//
// ▍本文件判的四件事
//   ① 中间行滚动 ⇒ 焦点移一行,页号**不动**;
//   ② 末行再向下 ⇒ 翻到后一页,焦点落**该页首行**;
//   ③ 首行再向上 ⇒ 翻回前一页,焦点落**该页末行** —— 这是「册子是一条连续竖轴」的关键:
//      若落首行,再往上滚一格就立刻翻回去,册子会卡在两页之间来回跳,永远滚不进第二页;
//   ④ `NavigationMoveEvent` 落到行上 ⇒ 边界行触发翻页(`SendEvent` 走用户回调阶段,
//      与生产 TrickleDown 注册同一条路径)。
//
// ▍本文件**不**判(先声明,防借绿)
//   · 默认焦点动作阶段那一半:本文件用 `VisualElement.SendEvent` 只跑用户回调;
//     「生产 `Panel` 的 `FocusController` 默认动作会不会连同发生 / `PreventDefault` 挡不挡得住」
//     归**真包走查**(`production/qa/evidence/` 的实拍 + 按键注入),不在本判据面内。
//   · 手感(滚一格走一行跟不跟手)、滚轮的物理方向是否符合玩家直觉 —— 归人工腿。
//
// ▍读哪些仓库文件(test-standards 具名声明义务)
//   **读**:生产 `PanelSettings.asset` + 九份生产 USS(`Assets/Gameplay.UI/Skeuomorphic/*.uss`)。
//     **为什么必须读真件**:行高 / 页容量都由 USS 的 `min-height` / `padding` 决定,
//     焦点落点是**布局算出来的**。把尺寸内联进夹具 ⇒ 判的是夹具自己的数字(自指判据)。
//   **写**:零写盘。

#if UNITY_EDITOR
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
    /// <summary>`TD-skeuo-19`:滚轮 / 上下键翻页的**焦点落点**。</summary>
    [TestFixture]
    public sealed class save_book_paging_focus_test
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
            _go = new GameObject("SaveBookPaging_UnderTest");

            var production = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
            Assert.IsNotNull(production,
                "[前置] 生产 PanelSettings 必须存在 —— 否则焦点 / 布局与生产无关");

            // 运行时副本 + 1:1 像素(与 `save_slots_page_geometry_test` 同一理由:
            // 参考分辨率的整体缩放会引入一个与屏无关的倍率)。
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
                    Assert.Fail("[夹具红] 面板 5s 未就绪 —— 没有面板就没有焦点,本判据无从成立");
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

            // 8 个槽位 ⇒ `PageSize` = 6 时两页(6 行 + 2 行)。
            // ⚠️ 全画**空槽**:开册落页 = 高水位行所在页(§7.1),全都有档会直接开到末页。
            var views = new List<SaveSlotView>();
            for (uint seq = 1; seq <= 8; seq++)
                views.Add(new SaveSlotView(seq, known: true, hasSave: false, tick: 0L));
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

        private SaveSlotsScreen Screen()
        {
            Assert.AreEqual(ModalId.SaveSlots, _driver.CurrentModal, "[前置] 册须是当前模态");
            var screen = _driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(screen, "[前置] driver 当前屏须是存档册");
            return screen;
        }

        /// <summary>把焦点显式落到第 <paramref name="rowIndex"/> 行并核实(否则后续判据在游离焦点上恒真)。</summary>
        private static void FocusRow(SaveSlotsScreen screen, int rowIndex)
        {
            VisualElement row = screen.RowAt(rowIndex);
            Assert.IsNotNull(row, $"[前置] 第 {rowIndex} 行须存在(本页 {screen.SlotRowCount} 行)");
            row.Focus();
            Assert.AreEqual(rowIndex, screen.FocusedRowIndex,
                $"[前置] 焦点须真的落在第 {rowIndex} 行上 —— 否则「滚一格到下一行」无从成立");
        }

        [UnityTest]
        public IEnumerator test_wheel_midPage_movesFocusOneRow_pageUnchanged()
        {
            SaveSlotsScreen screen = Screen();
            Assert.AreEqual(2, screen.PageCount, "[前置] 8 槽 ⇒ 两页");
            FocusRow(screen, 0);

            Assert.IsTrue(screen.HandleWheel(-1f), "中间行滚动须被消费");
            Assert.AreEqual(1, screen.FocusedRowIndex, "向下滚一格 = 下一行");
            Assert.AreEqual(0, screen.CurrentPage, "中间行不得翻页");

            Assert.IsTrue(screen.HandleWheel(-1f));
            Assert.AreEqual(2, screen.FocusedRowIndex);
            Assert.AreEqual(0, screen.CurrentPage);

            Assert.IsTrue(screen.HandleWheel(+1f), "向上滚一格 = 上一行");
            Assert.AreEqual(1, screen.FocusedRowIndex);
            Assert.AreEqual(0, screen.CurrentPage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_wheel_atLastRow_turnsPage_andLandsOnNextPageTop()
        {
            SaveSlotsScreen screen = Screen();
            int lastRow = screen.SlotRowCount - 1;
            FocusRow(screen, lastRow);

            Assert.IsTrue(screen.HandleWheel(-1f), "末行再向下 = 翻页");
            Assert.AreEqual(1, screen.CurrentPage, "须翻到后一页");
            Assert.AreEqual(0, screen.FocusedRowIndex, "落点 = 新页首行(继续向下走的自然位置)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_wheel_atFirstRow_turnsBack_andLandsOnPreviousPageBottom()
        {
            SaveSlotsScreen screen = Screen();

            // 先下到第二页
            FocusRow(screen, screen.SlotRowCount - 1);
            Assert.IsTrue(screen.HandleWheel(-1f));
            Assert.AreEqual(1, screen.CurrentPage);

            // 第二页首行再往上滚:翻回第一页,**焦点落末行**
            FocusRow(screen, 0);
            Assert.IsTrue(screen.HandleWheel(+1f), "首行再向上 = 翻回前一页");
            Assert.AreEqual(0, screen.CurrentPage);
            Assert.AreEqual(screen.SlotRowCount - 1, screen.FocusedRowIndex,
                "落点 = 前一页**末行** —— 若落首行,再往上滚一格就立刻翻回去,册子会在两页之间死循环");

            // 这一条就是「连续竖轴」的判据:落末行后继续向上滚,焦点在页内上行,不再翻页
            Assert.IsTrue(screen.HandleWheel(+1f));
            Assert.AreEqual(0, screen.CurrentPage, "末行再向上 = 页内上行,不得翻页");
            Assert.AreEqual(screen.SlotRowCount - 2, screen.FocusedRowIndex);

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_navigationMoveOnEdgeRow_turnsPage()
        {
            SaveSlotsScreen screen = Screen();
            int lastRow = screen.SlotRowCount - 1;
            FocusRow(screen, lastRow);

            using (var ev = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Down,
                                                          EventModifiers.None))
            {
                screen.RowAt(lastRow).SendEvent(ev);
            }

            // 翻页**排在本轮派发之后**(拆掉收事件的行会丢焦点)⇒ 等一帧再判
            yield return null;
            Assert.AreEqual(1, screen.CurrentPage, "边界行上的向下导航须翻页");
            Assert.AreEqual(0, screen.FocusedRowIndex, "落点 = 新页首行");

            // 反向:新页首行上向上导航 ⇒ 翻回第一页并落末行
            FocusRow(screen, 0);
            using (var ev = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Up,
                                                          EventModifiers.None))
            {
                screen.RowAt(0).SendEvent(ev);
            }

            yield return null;
            Assert.AreEqual(0, screen.CurrentPage, "首行上的向上导航须翻回前一页");
            Assert.AreEqual(screen.SlotRowCount - 1, screen.FocusedRowIndex, "落点 = 前一页末行");

            yield return null;
        }

        [UnityTest]
        public IEnumerator test_wheel_onSinglePageBook_doesNotTurn()
        {
            SaveSlotsScreen screen = Screen();
            // 收窄到 3 槽 ⇒ 一页,任何方向都翻不动
            var views = new List<SaveSlotView>();
            for (uint seq = 1; seq <= 3; seq++)
                views.Add(new SaveSlotView(seq, known: true, hasSave: false, tick: 0L));
            screen.SetSlotViews(views);   // 与装配层调的是同一个入口(driver 只是它的调用方)
            yield return null;

            Assert.AreEqual(1, screen.PageCount, "[前置] 三槽 ⇒ 一页");
            FocusRow(screen, 0);

            Assert.IsFalse(screen.HandleWheel(+1f), "单页册首行再向上 = 没有动作,不消费");
            Assert.AreEqual(0, screen.CurrentPage);
            Assert.AreEqual(0, screen.FocusedRowIndex, "焦点不得动");

            yield return null;
        }
    }
}
#endif
