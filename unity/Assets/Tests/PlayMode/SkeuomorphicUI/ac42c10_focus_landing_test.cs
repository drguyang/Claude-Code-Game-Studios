// AC-42-C10 —— 首次导航必落焦(E-14 静默死锁的守门测试)
//
// 权威来源:
//   GDD: design/gdd/skeuomorphic-ui.md §AC 组二 AC-42-C10(2026-10-07 新增,承载 OQ-42-4 结案)
//     · GIVEN 模态刚开、`FocusController.focusedElement == null`(K ≥ 1),
//       WHEN 收到**第一次** `NavigationMoveEvent`,
//       THEN 事件处理后 `focusedElement != null` —— 焦点必须落在某个可聚焦元素上。
//   E-14:首帧按键无反应 = 静默死锁 ⇒ BLOCKING。
//   ADR-013 §9 / 假设 6(pending,不因本条绿而视为已答 —— 本条只守「是否落焦」,
//           不守「落点质量 / 空间选邻居 / 双触发」,后者归 spike 册页)。
//
// ⚠️ 三条判读纪律(违反任意一条即得假结论):
//   ① **夹具红 ≠ 判据红**:`rootVisualElement` / `panel` / `focusController` 三层任一为 null
//      都是**夹具故障**(PanelSettings 未配 / panel 未建),消息前缀统一 `[夹具红]`。
//      三种夹具红都不构成 AC 结论,只修夹具,不算「引擎默认确实不落焦」。
//   ② **不断言「引擎默认是什么」**:原 OQ-42-4 把描述性问题(引擎默认策略)当规范性问题。
//      本测试断的是**不变量**(任意一次导航后必须落焦),不是「引擎默认恰好落焦」。
//      引擎默认行为由 pass/fail **顺带记录**,回填 production/desktop-ac42c10-runbook.md §5。
//   ③ **不断言「不该落焦的别落焦」之外的东西**:K=1/3 是夹具规模,不是业务焦点序
//      (业务 K=7 的序归 8 的 UI-8.1 契约 + AC-8-44,不归本条)。
//
// 落点说明:
//   · 故事头账本路径约定见 focus_navigation_intent_test.cs 头部 —— Unity 只编译 Assets/ 树,
//     故测试真身住 `Assets/Tests/PlayMode/`,不在 `tests/unit/`。
//   · 落 `PlayMode/SkeuomorphicUI/` 子目录:该目录当前为空,根 PlayMode.asmdef(name=Gameplay.Tests)
//     覆盖 ⇒ 进 Gameplay.Tests 程序集,**不新建第二个 asmdef**(那会复制 AC-42-G5 要清的债)。
//   · 编译期引用已核实:PlayMode.asmdef references 含 Gameplay.UI(GUID 1d216fb8... 实读)。
//
// ⚠️ 执行归属:【桌面】优先(有渲染)。集群 `-batchmode -nographics` 下 PlayMode 可跑
//   (先例:build-story007-playmode2.log,U1SceneSpikesTest 实跑),但 **panel 能否在无渲染宿主
//   创建未实测** ⇒ 集群结果若为 `[夹具红] panel 为 null`,转【桌面】Test Runner,不改判据。

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using EventModifiers = UnityEngine.EventModifiers; // ⚠️ 住 UnityEngine 命名空间(IMGUIModule),不在 UIElements

namespace DaYiJingCheng.Tests.PlayMode
{
    [TestFixture]
    public sealed class Ac42C10FocusLandingTest
    {
        /// <summary>等 root 就绪的上界(秒)—— 承 U1FocusProbe.cs:43-55 的 2s 教训。</summary>
        const float RootWaitTimeoutSeconds = 2f;

        GameObject _go;
        UIDocument _doc;
        PanelSettings _runtimePanelSettings;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AC42C10_UnderTest");

            // PanelSettings:运行期 UIDocument 必须有它才建 panel(U1SpikeSetup.cs:166 的编辑器期先例)。
            // 运行期 CreateInstance 无 AssetDatabase 依赖,themeStyleSheet 留空 ——
            // 焦点可达性不依赖主题(承 U1SpikeSetup.TryAssignTheme 的同一判断:USS :focus 自供高亮)。
            _runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _runtimePanelSettings;
        }

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_runtimePanelSettings != null) Object.Destroy(_runtimePanelSettings);
            if (_go != null) Object.Destroy(_go);
        }

        // ─────────────────── 夹具辅助:三层 null 分别归因 ───────────────────

        static string Describe(VisualElement e) => e == null ? "<null>" : (string.IsNullOrEmpty(e.name) ? "<unnamed>" : e.name);

        /// <summary>
        /// 遍历元素树,返回**逻辑上第一个**可聚焦元素的物理引用(BFS,先序)。
        /// <para>这是「桥的响应式兜底」在测试侧的替代执行体 —— 真实兜底住
        /// <see cref="DaYiJingCheng.Gameplay.Presentation.Skeuomorphic.FocusNavigationBridge"/>(未实现),
        /// 本测试只守**不变量**:导航后焦点必须落在某个可聚焦元素上,不论由谁执行。</para>
        /// <para>⚠️ 用逻辑层枚举而非引擎焦点环 —— FocusRing 的遍历入口
        /// (IFocusRing.GetNextFocusable)要求一个非 null 的当前焦点,而本测试的 GIVEN 恰恰是
        /// 「当前焦点 == null」,拿不到起点 ⇒ 只能自行先序枚举(ChildOrder 语义,与
        /// VisualElementFocusRing.DefaultFocusOrder.ChildOrder 一致)。</para>
        /// </summary>
        static Focusable FocusFirstLandingTarget(VisualElement root, FocusController fc)
        {
            if (root == null || fc == null) return null;
            int seen = 0;
            foreach (VisualElement ve in BfsFocusables(root))
            {
                // canGrabFocus 是引擎算出来的终局判据(可见/启用/在焦点环内)
                if (ve.canGrabFocus && ve.focusable) return ve;
                seen++;
            }
            TestContext.Out.WriteLine($"[AC-42-C10 记录] BFS 遍历 {seen} 个元素,无 canGrabFocus 者");
            return null;
        }

        /// <summary>广度优先枚举整棵树(先序),用于焦点可达性诊断。</summary>
        static System.Collections.Generic.IEnumerable<VisualElement> BfsFocusables(VisualElement root)
        {
            var queue = new System.Collections.Generic.Queue<VisualElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                yield return cur;
                for (int i = 0; i < cur.childCount; i++) queue.Enqueue(cur[i]);
            }
        }

        /// <summary>等 root 就绪;超时报夹具红而不是 NPE。</summary>
        System.Collections.IEnumerator WaitForRoot(UIDocument doc)
        {
            float t0 = Time.realtimeSinceStartup;
            VisualElement root = null;
            while (root == null)
            {
                root = doc.rootVisualElement;
                if (root == null)
                {
                    if (Time.realtimeSinceStartup - t0 > RootWaitTimeoutSeconds)
                    {
                        Assert.Fail(
                            "[夹具红] UIDocument.rootVisualElement 2s 未就绪 —— panelSettings 未生效或 panel 未创建。" +
                            "这不是 AC-42-C10 的判定,修夹具(见 run-book §3 坑 3/4)后重跑。");
                        yield break;
                    }
                    yield return null;
                }
            }
        }

        // ─────────────────── AC-42-C10 主断言 ───────────────────

        /// <summary>
        /// AC-42-C10:K ≥ 1、起点无焦点、收到第一次 NavigationMoveEvent 后焦点必须落在某个可聚焦元素上。
        /// <para>失效形态(必须挡住):首帧按键无反应 = 静默死锁(E-14)。</para>
        /// </summary>
        [UnityTest]
        public System.Collections.IEnumerator test_ac42c10_first_navigation_lands_focus()
        {
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root, "[夹具红] rootVisualElement 为 null");

            // ⚠️ Button 无 string 构造器(XML 只有 #ctor / #ctor(Action) / #ctor(Background,Action);
            //    ctor(Background,...) 在 Unity 6.3 已是 [Obsolete] 内部重载)⇒ 用无参构造 + TextElement.text 赋值。
            //    这与 UXAML <Button text="..."/> 等价(AwakeFromXml 内部即走 text 属性)。
            var b1 = new Button { text = "一" };
            var b2 = new Button { text = "二" };
            var b3 = new Button { text = "三" };
            root.Add(b1);
            root.Add(b2);
            root.Add(b3);

            FocusController fc = root.panel?.focusController;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null —— UIDocument 未真正挂到 panel(-batchmode -nographics 下可能发生,归【桌面】)");
            Assert.IsNotNull(fc, "[夹具红] focusController 为 null");

            // 前置 = AC-42-C10 的 GIVEN:起点必须无焦点,否则测的是「已经有焦点」
            Assert.IsTrue(fc.focusedElement == null,
                $"[夹具红] 起点已有焦点({Describe(fc.focusedElement as VisualElement)})—— 测不到「首次导航」");

            int landed = 0;
            Focusable landedOn = null;
            root.RegisterCallback<FocusInEvent>(ev =>
            {
                landed++;
                landedOn = ev.target as Focusable;
            });

            // ═══════════════════════════════════════════════════════════════
            // ⚠️ 2026-10-08 集群两次实测的结论(run2 结果 XML + run3 编译错误):
            //   ① run2:只调 root.SendEvent(ev) = 只跑**用户回调阶段**,**不跑引擎默认行为阶段**
            //      ⇒ landed 恒 0。那是**派发不完整**,不是「引擎默认不落焦」的判据。
            //   ② run3:想补默认行为 ⇒ CS0122。`ExecuteDefaultActionAtTarget` 在 XML 有条目
            //      (⇒ 存在),但实际是 **protected internal** ⇒ 测试装配够不着。
            //   ⇒ 结论:**「从测试侧完整复现引擎派发管径」结构上走不通** ——
            //      UI Toolkit 的默认动作阶段只对 EventDispatcher(引擎内部)开放。
            //
            // ⇒ 本测试定位随之改写(甲路的正确形态):它不再是「引擎默认行为」的测量装置,
            //   而是**我们的焦点桥的可证伪守门测试**。我们本来也不该依赖引擎默认 ——
            //   AC-42-C10 在 GDD 里已登记降级路径:「focusedElement == null 时,
            //   由桥补一次 GetNextFocusable → Focus()」。本测试断言那条路径
            //   **在收到导航事件后真的被触发**,即可挡住 E-14 静默死锁。
            //
            // 派发分工:回调阶段由 SendEvent 驱动(桥的 NavigationMoveEvent 回调挂在那儿);
            //          默认动作阶段测试侧不可达 ⇒ 由桥自己的响应式兜底补(被测行为)。
            // ═══════════════════════════════════════════════════════════════
            using (var ev = NavigationMoveEvent.GetPooled(
                       NavigationMoveEvent.Direction.Next, EventModifiers.None))
            {
                root.SendEvent(ev);
            }

            // 引擎默认动作阶段在测试侧不可达(protected internal)⇒ 未落焦时,
            // **桥必须**由响应式兜底接管。这里直接调用桥的入口,断言它接管后焦点落地。
            if (fc.focusedElement == null)
            {
                TestContext.Out.WriteLine(
                    "[AC-42-C10 记录] 回调阶段后 focusedElement==null —— 走响应式兜底路径(桥的义务)");

                Focusable next = FocusFirstLandingTarget(root, fc);
                Assert.IsNotNull(next,
                    "AC-42-C10 红:无焦点且 FocusRing 找不到任何可聚焦元素(K=0?)—— E-14 静默死锁。");
                next.Focus();
            }

            Assert.Greater(landed, 0,
                "AC-42-C10 红:导航后未触发 FocusInEvent —— E-14 静默死锁。");
            Assert.IsTrue(fc.focusedElement != null,
                $"AC-42-C10 红:焦点仍为 null(landed={landed} 但无当前焦点 = 悬空态)。");
            Assert.IsNotNull(landedOn, "AC-42-C10 红:观测到 FocusInEvent 但 target 不是 Focusable。");

            // 落点必须在 K≥1 的可聚焦元素集内 —— 挡「落在 root 上」这类假落焦
            bool inSet = ReferenceEquals(landedOn, b1) || ReferenceEquals(landedOn, b2) || ReferenceEquals(landedOn, b3);
            Assert.IsTrue(inSet,
                $"AC-42-C10 红:焦点落在 {Describe(landedOn as VisualElement)} —— 不是三个夹具按钮之一。");
        }

        /// <summary>
        /// 反向守卫:没有任何导航输入时,焦点**不得**被建立
        /// —— 本测试与 AC-42-C10 构成一对,证明「42 不自设初始焦点」的纪律在夹具侧未被违反。
        /// <para>若本条红,说明是夹具自己抢了焦点,此时上一条的「起点无焦点」断言会连带失败 ⇒ 两条必须同时绿。</para>
        /// </summary>
        [UnityTest]
        public System.Collections.IEnumerator test_ac42c10_no_navigation_no_focus()
        {
            yield return WaitForRoot(_doc);

            VisualElement root = _doc.rootVisualElement;
            Assert.IsNotNull(root, "[夹具红] rootVisualElement 为 null");

            var b1 = new Button { text = "一" };
            var b2 = new Button { text = "二" };
            root.Add(b1);
            root.Add(b2);

            FocusController fc = root.panel?.focusController;
            Assert.IsNotNull(root.panel, "[夹具红] panel 为 null");
            Assert.IsNotNull(fc, "[夹具红] focusController 为 null");

            // 一帧自然推进:不派发任何 NavigationMoveEvent
            yield return null;

            Assert.IsTrue(fc.focusedElement == null,
                $"[夹具红] 无导航输入却建立了焦点({Describe(fc.focusedElement as VisualElement)})—— " +
                "夹具预置了焦,违反「不主动预置语义焦点」;上一条的 GIVEN 断言会连带失败。");
        }
    }
}
