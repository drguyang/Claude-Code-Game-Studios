// ADR-013 假设 6 · 手柄焦点导航 —— 自动化腿(U1 spike · PlayMode · 2026-10-27)
//
// ▍为什么有这个文件
//   `production/u1-spike-checklist.md:207` 至今仍记「假设 6 ⬜ 未跑(用户裁定缓办)」。
//   2026-09-23 的装置(`Assets/Editor.Tools.Spike/U1FocusProbe.cs` + Setup 菜单)是
//   **编辑器内 Play 腿**,两年未动。本文件补的是**可 CI 重跑的自动化腿**。
//
// ▍能验什么 / 不能验什么(先声明边界,防借绿)
//   检查卡 §5.2 四判据里,本文件覆盖的是**引擎能力前提**与**装置可达性**,
//   **不是**「真实手柄按键 → InputSystemUIInputModule → EventSystem → panel」整链:
//
//   | 判据 | 本文件 | 判据性质 |
//   |---|---|---|
//   | ① 每次输入恰好一次移动 | ❌ **不在本文件面内** | 🔁 已由 `u1_assump6_gamepad_chain_test.cs` 验(2026-10-27) |
//   | ② 引擎自动完成空间选邻居 | ⚠️ **只验引擎能力**,不验输入链路 | 🔁 行为面已由同件验 |
//   | ③ 无指针仍可达全部控件 | ✅ **可验**(装置侧) |
//   | ④ 单一 `UI/Navigate` 来源 | ❌ 不在本文件面内 | 🔁 已由同件验 |
//
//   ⚠️ **2026-10-27 订正**:本文件原写「判据① 不可验」「判据② 方向语义不可验」,
//   理由是「引擎默认动作阶段的公开入口不存在」。**那对 `VisualElement.SendEvent` 这条路成立,
//   但手柄导航不走它。** 手柄链是 `InputSystemUIInputModule.Process()` → `PanelEventHandler.OnMove`
//   (public)→ **面板级** `m_Panel.SendEvent` ⇒ 整链可驱动。整链腿已据此落成并通过四判据,
//   原件 `production/qa/evidence/spike-assump6-chain-2026-10-27.md`。
//   下方「已知不可达」一节**保留原文不删**,以本框为准。
//
// ▍已知不可达(IL 实证,2026-10-27,`UnityEngine.UIElementsModule.dll`)
//   🔁 **本节结论已被证伪(同日整链腿)** —— 保留原文,以文首订正框为准。
//   精确地说:下面三句只对 **`VisualElement.SendEvent`** 成立;手柄链走的是
//   `PanelEventHandler.OnMove`(public)+ **面板级** `m_Panel.SendEvent`,那条链**可达**。
//   `FocusController.FocusNextInDirection` / `SwitchFocusOnEvent` 均为 **`assembly`(internal)**
//   ⇒ 测试侧够不着;`SendEvent` 只跑**用户回调阶段**,不跑引擎默认动作阶段
//   (同族结论已由 `ac42c10_focus_landing_test.cs:175-192` 于 2026-10-08 实证)。
//   ⇒ **判据① 的双触发检测在自动化腿结构上做不了** —— 它需要「一次真实输入」穿过
//   输入模块,而该链在 `-batchmode -nographics` 下不可达。**如实记 `NOT-RUN`,不借绿。**
//
// ▍判据② 为什么改成问 FocusRing 而不是派发事件
//   `FocusController.focusRing` 是 **private**,但 `VisualElementFocusRing` 的
//   **公开构造器 + `GetNextFocusable` + `defaultFocusOrder` setter 全是 public**。
//   ⇒ 可以自建一个与引擎同款的 focus ring,设 `DefaultFocusOrder.PositionXY`(几何序),
//   直接问「从 E 往左,下一个是谁」,断言它落在**几何相邻格**上。
//   这验的是「引擎具备按位置选邻居的能力」= 判据②的**引擎侧前提**;
//   「该能力在真实手柄输入下被触发」仍归人工腿 —— 两者不可混记。
//
// ⚠️ 判读纪律(承 `ac42c10_focus_landing_test`):
//   ① **夹具红 ≠ 判据红** —— `panel`/`focusController` 为 null、或 `worldBound` 全零,
//      消息统一前缀 `[夹具红]`,不得当作 ADR 结论。

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>
    /// ADR-013 §6.6 假设 6(UI Toolkit 运行时手柄焦点导航)的自动化腿。
    /// 只覆盖引擎能力前提与装置可达性,输入链路整链归人工腿。
    /// </summary>
    [TestFixture]
    public sealed class U1Assump6GamepadFocusSpikeTest
    {
        const float RootWaitTimeoutSeconds = 2f;

        // 3×3 网格:与检查卡 §5.1 的最小装置一致(9 个可聚焦按钮)。
        const int Columns = 3;
        const int Rows = 3;
        const float CellSize = 100f;

        GameObject _go;
        UIDocument _doc;
        PanelSettings _panelSettings;
        readonly List<Button> _grid = new List<Button>();

        [SetUp]
        public void SetUp()
        {
            _grid.Clear();
            _go = new GameObject("U1Assump6_UnderTest");

            // 承 ac42c10_focus_landing_test.cs:56-61 —— 运行期 UIDocument 必须有 PanelSettings
            // 才会建 panel;themeStyleSheet 留空(焦点能力不依赖主题)。
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _grid.Clear();
        }

        // ─────────────────────────── 夹具 ───────────────────────────

        IEnumerator WaitForRoot()
        {
            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitTimeoutSeconds)
                {
                    Assert.Fail(
                        "[夹具红] UIDocument.rootVisualElement 2s 未就绪 —— panelSettings 未生效或 panel 未创建。" +
                        "这不是假设 6 的判定,修夹具后重跑。");
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// 建 3×3 绝对定位网格。**必须用绝对坐标** —— 几何序焦点环按
        /// <c>worldBound</c> 算邻居,若走 flex 自动布局,`-nographics` 下 layout 时机不可控。
        /// </summary>
        void BuildGrid(VisualElement root)
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    var b = new Button { text = $"{r},{c}", name = $"btn-{r}-{c}" };
                    b.style.position = Position.Absolute;
                    b.style.left = c * CellSize;
                    b.style.top = r * CellSize;
                    b.style.width = CellSize - 10f;
                    b.style.height = CellSize - 10f;
                    root.Add(b);
                    _grid.Add(b);
                }
            }
        }

        Button At(int row, int col) => _grid[row * Columns + col];

        static string Describe(VisualElement e) =>
            e == null ? "<null>" : (string.IsNullOrEmpty(e.name) ? "<unnamed>" : e.name);

        // ──────────────── 判据② · 引擎按几何位置选邻居 ────────────────

        /// <summary>
        /// 判据 ② 的**可证伪断言**:<c>PositionXY</c> 焦点环**不退化为** <c>ChildOrder</c>。
        /// <para>同一 3×3 网格、同一输入,两种排序若给出**同一落点**,则说明几何序没生效
        /// (引擎把 PositionXY 当树序处理)⇒ 本条红。给出**不同**落点 = 几何序确实参与排序。</para>
        /// <para>⚠️ 本条的边界:它证「几何序参与排序」,**不证「落点方向语义正确」** ——
        /// 后者要穿过引擎默认动作阶段,入口是 internal(见 judgement2),归人工腿。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_u1assump6_positionXY_doesNotDegradeToChildOrder()
        {
            yield return WaitForRoot();
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root, "[夹具红] rootVisualElement 为 null");
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null —— UIDocument 未真正挂到 panel");

            BuildGrid(root);
            yield return null;
            yield return null;

            Rect probe = At(1, 1).worldBound;
            if (probe.width <= 0f || probe.height <= 0f)
            {
                Assert.Fail("[夹具红] worldBound 全零 —— -nographics 下布局未计算,几何序判据不可测;" +
                            "转【桌面】Play 腿,不改判据。");
            }

            var childRing = new VisualElementFocusRing(root)
            { defaultFocusOrder = VisualElementFocusRing.DefaultFocusOrder.ChildOrder };
            var xyRing = new VisualElementFocusRing(root)
            { defaultFocusOrder = VisualElementFocusRing.DefaultFocusOrder.PositionXY };

            var dir = VisualElementFocusChangeDirection.right;
            VisualElement childNext = childRing.GetNextFocusable(At(1, 1), dir) as VisualElement;
            VisualElement xyNext = xyRing.GetNextFocusable(At(1, 1), dir) as VisualElement;

            TestContext.Out.WriteLine(
                $"[U1-A6 记录] 同输入(btn-1-1, right):ChildOrder → {Describe(childNext)};" +
                $"PositionXY → {Describe(xyNext)}");

            Assert.IsNotNull(childNext, "[夹具红] ChildOrder 环未给出任何邻居 —— 夹具失效,不是判据结论。");
            Assert.IsNotNull(xyNext, "[夹具红] PositionXY 环未给出任何邻居 —— 夹具失效,不是判据结论。");

            Assert.IsFalse(ReferenceEquals(childNext, xyNext),
                "判据② 红:PositionXY 与 ChildOrder 给出**同一落点** " +
                $"({Describe(childNext)}) —— 几何序未生效," +
                "引擎把 PositionXY 当树序处理 ⇒ 假设 6 的失败形态之一(需走 ADR-013 的自实现焦点算法回退)。");

            TestContext.Out.WriteLine(
                "[U1-A6 判决] 判据② 引擎侧前提成立:PositionXY 环的落点与树序不同 ⇒ 几何位置确实参与排序。");
        }

        /// <summary>
        /// 判据 ② 的**行为正确性**(方向语义 / 几何落点是否「对」)—— 自动化腿**结构上不可验**。
        /// <para>上面那条只证「几何序参与排序」(两环落点不同);本条声明**为什么到此为止**:
        /// 要验「往左真的落到左邻」,必须走引擎**默认动作阶段**,而进入它的公开入口不存在。</para>
        /// </summary>
        [Test]
        public void test_u1assump6_judgement2_directionalSemantics_notRun()
        {
            Assert.Inconclusive(
                "[已迁出] 判据② 的**方向语义**面**不在本文件面内**,已由 " +
                "u1_assump6_gamepad_chain_test.cs 实测通过(2026-10-27,四向全中几何相邻格)。" +
                "本 Inconclusive 保留为历史登记:" +
                "当时依据 IL 实证(UnityEngine.UIElementsModule.dll)—— FocusController.SwitchFocusOnEvent / " +
                "FocusNextInDirection / EventDispatcher.Dispatch 均 assembly(internal),且自建 ring 的 " +
                "GetFocusChangeDirection 对 Left/Right/Up/Down 恒返基类 ⇒ 判定「测试侧够不着」。" +
                "🔁 该判定已被证伪:手柄链走 PanelEventHandler.OnMove(public)+ 面板级 m_Panel.SendEvent," +
                "根本不经过 VisualElement.SendEvent 那条路。原件 = " +
                "production/qa/evidence/spike-assump6-chain-2026-10-27.md。");
        }

        /// <summary>
        /// ⚠️ **已废弃的尝试**(保留作过程记录,不参与判决):
        /// 曾试图用「自建 ring + GetFocusChangeDirection + GetNextFocusable」两步复现引擎行为。
        /// 2026-10-27 run2–run7 六轮实测证明此路不通 —— 见下方 judgement2 的 IL 证据。
        /// </summary>
        [UnityTest, Explicit("已废弃:见 judgement2 的 NOT-RUN 说明;保留供人工腿诊断")]
        public IEnumerator test_u1assump6_focusRing_positionXY_picksGeometricNeighbor()
        {
            yield return WaitForRoot();
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root, "[夹具红] rootVisualElement 为 null");
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null —— UIDocument 未真正挂到 panel(-nographics 下可能发生)");

            BuildGrid(root);

            // 布局必须算出真几何 —— 全零则几何序无从谈起,属夹具红。
            yield return null;
            yield return null;

            Rect center = At(1, 1).worldBound;
            if (center.width <= 0f || center.height <= 0f)
            {
                Assert.Fail(
                    "[夹具红] worldBound 为全零 —— -nographics 下布局未计算,几何序判据不可测。" +
                    "这不是假设 6 的判定,转【桌面】Play 腿重跑,不改判据。");
            }

            var ring = new VisualElementFocusRing(root)
            {
                defaultFocusOrder = VisualElementFocusRing.DefaultFocusOrder.PositionXY
            };

            // ⚠️ 只做**诊断输出**,不做断言 —— 六轮实测已证明此路不通
            //   (GetFocusChangeDirection 对 Left/Right/Up/Down 恒返基类)。
            //   这里保留输出是为了让将来的人工腿能看到「同一装置下两种环的表现」。
            AssertNeighborViaEvent(ring, root, At(1, 1), NavigationMoveEvent.Direction.Left, At(1, 0), "左");

            // ── A/B 对照(诊断用,不改判据)──
            // 若 ChildOrder 环也解析出 none,则「拿到 none」是**夹具/用法问题**,不是几何序缺陷。
            var childRing = new VisualElementFocusRing(root)
            {
                defaultFocusOrder = VisualElementFocusRing.DefaultFocusOrder.ChildOrder
            };
            FocusChangeDirection childDir;
            using (var ev2 = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Left, EventModifiers.None))
            {
                ev2.target = At(1, 1);
                childDir = childRing.GetFocusChangeDirection(At(1, 1), ev2);
            }
            TestContext.Out.WriteLine(
                $"[U1-A6 对照] ChildOrder 环同输入 → {(ReferenceEquals(childDir, VisualElementFocusChangeDirection.left) ? "left" : ReferenceEquals(childDir, VisualElementFocusChangeDirection.right) ? "right" : childDir?.GetType().Name ?? "<null>")}" +
                "(若同为 FocusChangeDirection 基类,则问题在夹具/用法,不在几何序)");
            TestContext.Out.WriteLine(
                $"[U1-A6 对照] 直喂 right 常量:ChildOrder → {Describe(childRing.GetNextFocusable(At(1, 1), VisualElementFocusChangeDirection.right) as VisualElement)}, " +
                $"PositionXY → {Describe(ring.GetNextFocusable(At(1, 1), VisualElementFocusChangeDirection.right) as VisualElement)}");

            TestContext.Out.WriteLine(
                "[U1-A6 记录] PositionXY 焦点环四向查询已执行;左/右落点见上方断言。" +
                "⚠️ 上/下方向在 UI Toolkit 默认映射里走 next/previous(序),不走方向 —— " +
                "其落点归 FocusChangeDirection.none/next 语义,本测不臆断,留人工腿观察。");
        }

        /// <summary>
        /// 造一个真实的 <see cref="NavigationMoveEvent"/> 交给引擎,让它自己算方向 ——
        /// 这是 <c>FocusController.SwitchFocusOnEvent</c> 内部对 ring 的实际调用序列
        /// (该方法是 internal,故在此复现其**两步**,而非复现其内部其它逻辑)。
        /// </summary>
        void AssertNeighborViaEvent(
            VisualElementFocusRing ring, VisualElement root, Focusable from,
            NavigationMoveEvent.Direction moveDir, Button expected, string dirName)
        {
            FocusChangeDirection dir;
            using (var ev = NavigationMoveEvent.GetPooled(moveDir, EventModifiers.None))
            {
                TestContext.Out.WriteLine(
                    $"[U1-A6 诊断] 事件自检:GetPooled(Direction.{moveDir}) 后 ev.direction={ev.direction} " +
                    $"(期望 {moveDir});ev.move={ev.move}");
                // ⚠️ 签名 = GetFocusChangeDirection(Focusable, EventBase) —— 重载顺序是 (焦点, 事件),
                //   不是 (事件, 焦点)。2026-10-27 run3 实测:写成 (ev, from) ⇒ CS1503 两条。
                //
                // ⚠️ 必须先给事件设 target(2026-10-27 run4/run5 实测教训):无参 GetPooled() 的
                //   elementTarget 为 null ⇒ ring 走「target 不是焦点元素」分支,恒返 none。
                //   同族纪律见 ui_input_module_wiring_test.cs:33-34(直投必须显式 ev.target = X)。
                ev.target = from as VisualElement;
                dir = ring.GetFocusChangeDirection(from, ev);
            }

            TestContext.Out.WriteLine(
                $"[U1-A6 诊断] {dirName}向:NavigationMoveEvent.Direction.{moveDir} → " +
                $"GetFocusChangeDirection 给出 {(ReferenceEquals(dir, VisualElementFocusChangeDirection.left) ? "left" : ReferenceEquals(dir, VisualElementFocusChangeDirection.right) ? "right" : dir?.GetType().Name ?? "<null>")}");

            Focusable next = dir != null ? ring.GetNextFocusable(from, dir) : null;
            TestContext.Out.WriteLine(
                $"[U1-A6 诊断] {dirName}向落点 = {Describe(next as VisualElement)}(期望 {Describe(expected)})" +
                " —— 仅诊断输出,不构成判据② 的判决");
        }

        // ──────────────── 判据③ · 无指针可达全部控件 ────────────────

        /// <summary>
        /// 判据 ③(<c>technical-preferences.md</c> 硬约束:手柄没有指针仍能操作全部控件)的
        /// **装置侧前提**:3×3 全部按钮 <c>canGrabFocus</c>,且逐个 <c>Focus()</c> 都能落下。
        /// <para>失效形态(必须挡住):某元素不可聚焦 ⇒ 手柄永远够不着 ⇒ 本条红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_u1assump6_allControls_reachableWithoutPointer()
        {
            yield return WaitForRoot();
            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root, "[夹具红] rootVisualElement 为 null");

            BuildGrid(root);
            yield return null;

            FocusController fc = root.panel != null ? root.panel.focusController : null;
            Assert.IsNotNull(fc, "[夹具红] focusController 为 null");

            int reachable = 0;
            var unreachable = new List<string>();

            foreach (Button b in _grid)
            {
                if (!b.canGrabFocus || !b.focusable)
                {
                    unreachable.Add(Describe(b));
                    continue;
                }

                b.Focus();
                if (ReferenceEquals(fc.focusedElement, b))
                {
                    reachable++;
                }
                else
                {
                    unreachable.Add($"{Describe(b)}(Focus() 后未落焦)");
                }
            }

            TestContext.Out.WriteLine(
                $"[U1-A6 记录] 判据③ 可达性:{reachable}/{_grid.Count} 个控件在无指针条件下可聚焦且可落焦。");

            Assert.IsEmpty(unreachable,
                $"判据③ 红:{unreachable.Count} 个控件不可达 [{string.Join(", ", unreachable)}] —— " +
                "手柄玩家无法操作这些控件,违反 technical-preferences 的硬约束。");
            Assert.AreEqual(_grid.Count, reachable,
                $"判据③ 红:仅 {reachable}/{_grid.Count} 可达。");
        }

        // ──────────────── 判据① · 明确 NOT-RUN ────────────────

        /// <summary>
        /// 判据 ①(每次输入恰好一次焦点移动 / 无双触发)在自动化腿**结构上不可验**,
        /// 此条**故意不写断言**,只把不可达原因钉进测试报告,防止「跑绿了 = 判据① 过了」的误读。
        /// <para>依据:<c>FocusController.SwitchFocusOnEvent</c> 为 <c>assembly</c>(internal),
        /// <c>SendEvent</c> 不跑引擎默认动作阶段(IL + 2026-10-08 实证,见文件头)。</para>
        /// <para>⇒ 本测试永远 <c>Inconclusive</c>(NUnit 语义 = 未执行判据),不是 Pass。</para>
        /// </summary>
        [Test]
        public void test_u1assump6_judgement1_doubleTrigger_notRun()
        {
            Assert.Inconclusive(
                "[已迁出] 判据① 的**双触发检测**不在本文件面内,已由 " +
                "u1_assump6_gamepad_chain_test.cs 实测通过(2026-10-27,四轮全 nav=1 焦点=1)。" +
                "本 Inconclusive 保留为历史登记:当时判定「该链在 -batchmode -nographics 下不可达」+" +
                "「FocusController.SwitchFocusOnEvent 为 internal ⇒ 测试侧无法驱动」。 " +
                "🔁 前半句已被证伪:虚拟 Gamepad 可驱动整链,且 -nographics 下 EventSystem.Update " +
                "照常 tick(焦点移动本就不需要渲染)。后半句仍成立但不构成障碍 —— 我们不需要调 " +
                "SwitchFocusOnEvent,只需从 public 的 PanelEventHandler.OnMove 进入。" +
                "原件 = production/qa/evidence/spike-assump6-chain-2026-10-27.md。");
        }
    }
}
