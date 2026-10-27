// ADR-013 假设 6 · 手柄焦点导航 —— **整链腿**(PlayMode · 2026-10-27)
//
// ▍为什么这个文件存在
//   `u1_assump6_gamepad_focus_spike_test.cs`(同目录)只验到「引擎具备几何排序能力」
//   (判据② 的**引擎侧前提**)与「无指针可达」(判据③)。判据 ①② 的**行为面**当时记
//   `NOT-RUN`,理由是「进入引擎默认动作阶段的公开入口不存在」。
//
//   🔁 **该理由对本条路径不成立,本文件是对它的订正。**
//   2026-10-08 的结论(`ac42c10_focus_landing_test.cs:175-192`)证的是
//   **`VisualElement.SendEvent(ev)` 只跑用户回调阶段** —— 那条路的默认动作阶段
//   (`VisualElement.ExecuteDefaultAction*`)是 `protected internal`,够不着。
//   但**手柄导航不走那条路**。生产链是:
//
//     InputSystemUIInputModule.ProcessNavigation      ← Process() public override(:2382)
//       → ExecuteEvents.Execute(selectedGO, …, moveHandler)
//         → PanelEventHandler.OnMove(AxisEventData)    ← **public**(ugui PanelEventHandler.cs:249)
//           → m_Panel.SendEvent(NavigationMoveEvent)   ← **面板级**派发(不是 VisualElement.SendEvent)
//             → EventDispatcher → FocusController.SwitchFocusOnEvent(默认动作阶段)
//               → 焦点真的移动
//
//   `PanelEventHandler.OnMove` 与 `InputSystemUIInputModule.Process` 都是 public ⇒
//   整链**在测试侧可完整驱动**,无需反射、无需 internal。
//
// ▍判据(照抄检查卡 §5.2)
//   ① 每次输入**恰好一次**焦点移动(无双触发)—— `test_…_judgement1_…`
//   ② 引擎自动完成空间选邻居 —— `test_…_judgement2_…`
//
// ▍能验什么 / 不能验什么(先声明边界,防借绿)
//   本文件驱动的是 **虚拟 Gamepad(`InputSystem.AddDevice<Gamepad>`)→ 项目真动作资产
//   `InputSystem_Actions` 的 `UI/Navigate` → 生产装配的 EventSystem**。
//   ⇒ 它覆盖「手柄输入 → 焦点移动」的**整条代码链**,但它**不是**:
//     · ❌ 不是真实手柄硬件(轴死区 / 驱动 / 平台映射差异不在面内);
//     · ❌ 不是真实显示(仍 `-nographics`,高亮**看得见看不见**不在面内 —— 那是手感判断);
//     · ❌ 不是人眼对「移动是否跟手 / 是否跳格」的主观判断。
//   ⇒ **判据 ①② 的行为面**本文件可以结;检查卡 §5.2 的**手感判断**仍归人工。
//
// ⚠️ 判读纪律(承 `ac42c10_focus_landing_test`):
//   ① 夹具红 ≠ 判据红 —— panel / carrier / handler / 动作资产任一不就绪,消息前缀 `[夹具红]`。
//   ② 本文件断言的是**生产装配下的实际行为**,不是「引擎理应如此」。落点与几何邻居不符
//      ⇒ **判据② 真红**,不得改判据迁就实现。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>
    /// ADR-013 §6.6 假设 6 的**整链腿**:虚拟手柄 → 生产 EventSystem 装配 → 焦点移动。
    /// <para>与同目录 <c>U1Assump6GamepadFocusSpikeTest</c> 互补:那条验引擎能力前提,
    /// 本条验「生产装配下真的按几何位置移动」。</para>
    /// </summary>
    [TestFixture]
    public sealed class U1Assump6GamepadChainTest
    {
        const float RootWaitTimeoutSeconds = 2f;
        const float CarrierWaitTimeoutSeconds = 3f;

        // 与检查卡 §5.1 的最小装置一致:3×3 可聚焦按钮。
        const int Columns = 3;
        const int Rows = 3;
        const float CellSize = 100f;

        GameObject _go;
        UIDocument _doc;
        PanelSettings _panelSettings;
        GameObject _bootGo;
        EventSystem _es;
        InputSystemUIInputModule _module;
        PanelEventHandler _handler;
        Gamepad _pad;
        VisualElement _root;

        readonly List<Button> _grid = new List<Button>();

        // 观测面(每次 Press 前清零)
        int _navEvents;
        int _focusGained;
        readonly List<string> _focusTrail = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _grid.Clear();
            _focusTrail.Clear();
            _navEvents = 0;
            _focusGained = 0;

            _go = new GameObject("U1Assump6Chain_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 虚拟手柄自造自清(承 device_and_presentation_test 纪律)
            if (_pad != null && _pad.deviceId != InputDevice.InvalidDeviceId &&
                InputApi.GetDevice<Gamepad>() != null)
                InputApi.RemoveDevice(_pad);
            _pad = null;

            if (_root != null) _root.UnregisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);
            _root = null;
            _handler = null;
            _module = null;
            _es = null;

            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_bootGo != null) Object.Destroy(_bootGo);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _bootGo = null; _go = null;

            yield return null;
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ① —— 每次输入恰好一次焦点移动(无双触发)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 判据 ①:一次手柄方向输入 ⇒ **恰好一条** <c>NavigationMoveEvent</c> 且**恰好一次**焦点跳变。
        /// <para>「同键双触发」是 ADR-013 登记的头号失败形态。本条的判别面 = 计数,
        /// 与落点无关(落点归判据②)。</para>
        /// <para>变异:若 `FocusController` 之外再有第二条派发路径(或模块重复 Process),
        /// 计数 &gt; 1 ⇒ 本测红。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_u1assump6_judgement1_oneMovePerInput()
        {
            yield return AssembleProductionChain();

            // 每次 Press 前后各读一次计数 —— 逐次结账,不累计
            var dirs = new[]
            {
                (name: "right", v: new Vector2(1f, 0f)),
                (name: "down",  v: new Vector2(0f, -1f)),
                (name: "left",  v: new Vector2(-1f, 0f)),
                (name: "up",    v: new Vector2(0f, 1f)),
            };

            var report = new System.Text.StringBuilder();
            int offences = 0;

            foreach (var d in dirs)
            {
                int navBefore = _navEvents;
                int focusBefore = _focusGained;
                string from = CurrentFocusName();

                yield return Press(d.v);

                int navDelta = _navEvents - navBefore;
                int focusDelta = _focusGained - focusBefore;
                string to = CurrentFocusName();

                report.AppendLine(
                    $"[U1-A6 整链] {d.name,-5} {from,-10} → {to,-10} " +
                    $"nav事件={navDelta} 焦点跳变={focusDelta}");

                if (navDelta != 1) offences++;
                if (focusDelta != 1) offences++;
            }

            TestContext.Out.WriteLine(report.ToString());

            Assert.AreEqual(0, offences,
                "判据① 红:存在「一次输入 ≠ 一次移动」的轮次 —— 见上方逐行结账。" +
                "nav事件 > 1 = 同键双触发;焦点跳变 ≠ 1 = 移动次数与输入次数不符。" +
                "失败动作:记录复现步骤;走「冻单一来源 + 焦点单栈门」复核(检查卡 §5.2)。");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ② —— 引擎自动完成空间选邻居
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 判据 ②:从中心格出发,四向导航必须落到**几何相邻格**(不是树序下一格)。
        /// <para>期望:right → btn-1-2 · down → btn-2-1 · left → btn-1-0 · up → btn-0-1。</para>
        /// <para>⚠️ 本条**同时**判「落点对不对」与「落点是不是树序」:3×3 按行主序建,
        /// 树序的下一格恒为 btn-1-2,**down / left / up 三向若也落 btn-1-2 即证几何序未生效**。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_u1assump6_judgement2_directionalNeighborIsGeometric()
        {
            yield return AssembleProductionChain();

            var cases = new[]
            {
                (dir: "right", v: new Vector2(1f, 0f),  expect: "btn-1-2"),
                (dir: "down",  v: new Vector2(0f, -1f), expect: "btn-2-1"),
                (dir: "left",  v: new Vector2(-1f, 0f), expect: "btn-1-0"),
                (dir: "up",    v: new Vector2(0f, 1f),  expect: "btn-0-1"),
            };

            var report = new System.Text.StringBuilder();
            var failures = new List<string>();

            foreach (var c in cases)
            {
                FocusCentre();
                yield return null;

                yield return Press(c.v);

                string actual = CurrentFocusName();
                report.AppendLine($"[U1-A6 整链] {c.dir,-5} 期望={c.expect,-8} 实得={actual,-8} " +
                                  $"{(actual == c.expect ? "✅" : "❌")}");
                if (actual != c.expect) failures.Add($"{c.dir}: 期望 {c.expect} 实得 {actual}");
            }

            TestContext.Out.WriteLine(report.ToString());

            Assert.IsEmpty(failures,
                "判据② 红:四向导航未全部落到几何相邻格 —— " + string.Join(" · ", failures) +
                "。失败动作:质量不达预期 ⇒ 走「自实现焦点算法」(接口不变,ADR-013 已登记回退)。");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据② 的「边界行为可预期」切片 —— 只记录事实,不判红
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 边界行为**记录**(检查卡 §5.2 判据② 的「边界环绕/截断行为可预期」一半)。
        /// <para>⚠️ 本条**只记录不判红**,且这是刻意的:检查卡要求的是「**可预期**」,
        /// 而「可预期」是好是坏需人来定 —— 引擎可能环绕、可能截断、可能停在原地,
        /// 三种都算「可预期」,取决于产品意图。把它写成断言 = 替产品拍板。</para>
        /// <para>⇒ 本条的交付物是**四角八向的实测落点表**,供人工腿据此判断是否可接受。
        /// 断言只有一条:实验本身跑完了(每向都产生了落点读数)。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_u1assump6_judgement2b_edgeBehaviour_recorded()
        {
            yield return AssembleProductionChain();

            // 四角 × 朝外的方向:这是「环绕 vs 截断」唯一能暴露的分歧点
            var corners = new[]
            {
                (cell: (r: 0, c: 0), name: "btn-0-0 左上角"),
                (cell: (r: 0, c: 2), name: "btn-0-2 右上角"),
                (cell: (r: 2, c: 0), name: "btn-2-0 左下角"),
                (cell: (r: 2, c: 2), name: "btn-2-2 右下角"),
            };
            var outward = new[]
            {
                (dir: "left",  v: new Vector2(-1f, 0f)),
                (dir: "right", v: new Vector2(1f, 0f)),
                (dir: "up",    v: new Vector2(0f, 1f)),
                (dir: "down",  v: new Vector2(0f, -1f)),
            };

            var report = new System.Text.StringBuilder();
            report.AppendLine("[U1-A6 整链·边界] 角落朝外按键的实测落点(供人工判断「是否可预期」):");
            int readings = 0;

            foreach (var corner in corners)
            {
                foreach (var d in outward)
                {
                    At(corner.cell.r, corner.cell.c).Focus();
                    yield return null;

                    yield return Press(d.v);

                    string to = CurrentFocusName();
                    string from = $"btn-{corner.cell.r}-{corner.cell.c}";

                    // 三分类:停在原地 = 截断;落在**格内**的几何相邻 = 正常;其余 = 环绕
                    int nr = corner.cell.r, nc = corner.cell.c;
                    switch (d.dir)
                    {
                        case "left":  nc--; break;
                        case "right": nc++; break;
                        case "up":    nr--; break;
                        case "down":  nr++; break;
                    }
                    bool inGrid = nr >= 0 && nr < Rows && nc >= 0 && nc < Columns;
                    string neighbour = inGrid ? $"btn-{nr}-{nc}" : "<界外>";

                    string kind;
                    if (to == from) kind = "停在原地 = 截断";
                    else if (inGrid && to == neighbour) kind = "落在几何相邻格 = 正常";
                    else kind = "跨到别处 = 环绕";

                    report.AppendLine($"   {corner.name,-16} 按 {d.dir,-5} → {to,-8} ({kind})");
                    if (!string.IsNullOrEmpty(to) && to != "<none>") readings++;
                }
            }

            TestContext.Out.WriteLine(report.ToString());

            Assert.AreEqual(corners.Length * outward.Length, readings,
                "[夹具红] 有轮次没读到落点 —— 实验未跑完,记录不完整(这不是判据结论)。");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据④ —— 单一 UI/Navigate 来源(无同键双绑)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 判据④:焦点导航的**驱动源恰好一处** = <c>InputSystemUIInputModule.move</c> → <c>UI/Navigate</c>。
        /// <para><b>判据形态为什么是「谁能驱动焦点」而不是「同键路径求交」</b> —— 后者我试过两版,
        /// 都被实测证伪:</para>
        /// <list type="bullet">
        /// <item>版本 1「UI map 里 PassThrough+Vector2 即红」⇒ 抓到 <c>UI/Point</c> / <c>UI/ScrollWheel</c>,
        /// 而它们是**指针 / 滚轮**,不移动焦点 ⇒ 误报。</item>
        /// <item>版本 2「与 UI/Navigate 绑定路径求交」⇒ 抓到 <c>Player/Move ← 方向键</c> 与
        /// <c>UI/ScrollWheel ← dpad</c>。前者是 <c>Value</c> 型且属 <c>Player</c> map,
        /// **不喂 UI 模块**;后者滚轮不驱动焦点 ⇒ 仍是误报。</item>
        /// </list>
        /// <para>⇒ 两版的共同错误:把「同键」当成「双喂」。ADR-011 §Constraints 禁的是
        /// **Navigation 动作重复绑定**,即**第二个能产生 <c>NavigationMoveEvent</c> 的来源**。
        /// 那样的来源在本架构里只有一处可能 —— UI 输入模块的 <c>move</c> 引用。
        /// 故判据 = 「恰一个 EventSystem · 恰一个 UI 模块 · 其 move 恰指 UI/Navigate」。</para>
        /// <para><b>同键重叠仍要登记</b>(不判红):它是真实存在的事实,若将来有人把
        /// <c>Player/Move</c> 改成 PassThrough 或把 ScrollWheel 接进焦点路径,风险才成立。
        /// 本条把它们打进输出供人复核,而不是替人拍板。</para>
        /// <para><b>动态腿</b>由判据① 兜:双喂一旦发生,表现即「一次按键两次移动」
        /// (本轮变异 B 实测:nav事件=2 ⇒ 判据① 红)。静态 + 动态两条腿互为补强。</para>
        /// </summary>
        [UnityTest]
        public IEnumerator test_u1assump6_judgement4_singleNavigateSource()
        {
            yield return AssembleProductionChain();

            var report = new System.Text.StringBuilder();

            // ── 1. 结构腿:焦点导航的驱动源恰一处 ──
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            var uiModules = Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None);

            report.AppendLine($"[U1-A6 判据④] EventSystem={systems.Length} · " +
                              $"InputSystemUIInputModule={uiModules.Length}");

            Assert.AreEqual(1, systems.Length,
                $"判据④ 红:场景内 EventSystem 有 {systems.Length} 个(应为 1)—— " +
                "双 EventSystem 会各自派发导航(ADR-013 §F4)。");
            Assert.AreEqual(1, uiModules.Length,
                $"判据④ 红:场景内 InputSystemUIInputModule 有 {uiModules.Length} 个(应为 1)—— " +
                "双输入模块 = 双导航来源(ADR-013 §F4)。");

            var moveAction = uiModules[0].move?.action;
            Assert.IsNotNull(moveAction,
                "判据④ 红:UI 模块的 move 引用为 null —— 手柄导航链断在起点。");

            report.AppendLine($"[U1-A6 判据④] 焦点导航唯一驱动源 = " +
                              $"{moveAction.actionMap?.name}/{moveAction.name}");

            Assert.AreEqual("Navigate", moveAction.name,
                $"判据④ 红:UI 模块的 move 指向 {moveAction.name}(应为 Navigate)—— " +
                "焦点导航驱动源不是官方 UI/Navigate。");
            Assert.AreEqual("UI", moveAction.actionMap?.name,
                $"判据④ 红:move 指向的 action 属 {moveAction.actionMap?.name} map(应为 UI)。");

            // 官方桥禁 StandaloneInputModule 并存(ADR-013 §F4)
            var legacy = uiModules[0].gameObject.GetComponent<StandaloneInputModule>();
            Assert.IsNull(legacy,
                "判据④ 红:官方桥上并存 StandaloneInputModule —— ADR-013 §F4 明禁。");

            // ── 2. 事实登记(不判红):同键重叠清单 ──
            var navPaths = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var b in moveAction.bindings)
                if (!string.IsNullOrEmpty(b.path)) navPaths.Add(b.path);

            var overlaps = new List<string>();
            foreach (var map in InputSystem.actions.actionMaps)
                foreach (var a in map.actions)
                {
                    if (ReferenceEquals(a, moveAction)) continue;
                    foreach (var b in a.bindings)
                        if (!string.IsNullOrEmpty(b.path) && navPaths.Contains(b.path))
                        {
                            overlaps.Add($"{map.name}/{a.name}({a.type}) ← {b.path}");
                            break;
                        }
                }

            report.AppendLine("[U1-A6 判据④] 事实登记(不判红)同键重叠 —— 需人确认其是否会驱动焦点:");
            foreach (var o in overlaps) report.AppendLine("     " + o);
            if (overlaps.Count == 0) report.AppendLine("     (无)");

            TestContext.Out.WriteLine(report.ToString());
        }

        // ══════════════════════════════════════════════════════════════════
        // 装配
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 装配**生产同款**链:UIDocument 面板 → BootRoot.EnsureEventSystem(生产方法)→
        /// 等 interop bridge 建面板载体 → 选中载体 → 聚焦中心格 → 加虚拟手柄。
        /// </summary>
        IEnumerator AssembleProductionChain()
        {
            // 1. 面板就绪
            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitTimeoutSeconds)
                {
                    Assert.Fail("[夹具红] UIDocument.rootVisualElement 2s 未就绪 —— panelSettings 未生效。");
                    yield break;
                }
                yield return null;
            }
            _root = _doc.rootVisualElement;

            BuildGrid(_root);
            _root.RegisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);
            _root.RegisterCallback<FocusInEvent>(_ => { _focusGained++; }, TrickleDown.TrickleDown);
            yield return null;
            yield return null;   // 让 layout 算出 worldBound(几何序依赖它)

            if (At(1, 1).worldBound.width <= 0f)
            {
                Assert.Fail("[夹具红] worldBound 全零 —— 布局未计算,几何序判据不可测;" +
                            "转【桌面】Play 腿,不改判据。");
                yield break;
            }

            // 2. 生产方法建官方桥(承 ui_input_module_wiring_test 同纪律:
            //    BootRoot 禁 Start —— 启动序含 Addressables / world 加载,与本测无关)
            _bootGo = new GameObject("BootRoot_Assump6Chain");
            var boot = _bootGo.AddComponent<BootRoot>();
            boot.enabled = false;
            _es = InvokePrivate(boot, "EnsureEventSystem") as EventSystem;
            Assert.IsNotNull(_es, "[夹具红] EnsureEventSystem 返回 null");
            _module = _es.GetComponent<InputSystemUIInputModule>();
            Assert.IsNotNull(_module, "[夹具红] EventSystem 未挂 InputSystemUIInputModule");

            // 3. interop bridge 建面板载体 GO(它含 PanelEventHandler + PanelRaycaster)
            GameObject carrier = null;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < CarrierWaitTimeoutSeconds)
            {
                carrier = (_root.panel as IRuntimePanel)?.selectableGameObject;
                if (carrier != null) break;
                yield return null;
            }
            Assert.IsNotNull(carrier,
                "[夹具红] 面板载体 selectableGameObject 3s 未就绪 —— interop bridge 未建面板 GO。");
            _handler = carrier.GetComponent<PanelEventHandler>();
            Assert.IsNotNull(_handler,
                "[夹具红] 面板载体上没有 PanelEventHandler —— 手柄导航事件无处落地。");

            // 4. 选中载体(生产 bootstrap 的同一动作)
            _es.SetSelectedGameObject(carrier);
            var module = _es.currentInputModule as InputSystemUIInputModule;
            Assert.IsNotNull(module,
                "[夹具红] currentInputModule 不是 InputSystemUIInputModule —— EventSystem 未 tick 过。");
            _module = module;

            // 5. 起点:中心格聚焦
            FocusCentre();
            yield return null;

            Assert.IsNotNull(_root.panel?.focusController?.focusedElement,
                "[夹具红] 起点无焦点 —— Focus() 未生效。");

            // 6. 虚拟手柄(项目既有用法:AddDevice + QueueStateEvent)
            _pad = InputApi.AddDevice<Gamepad>();
            Assert.IsNotNull(_pad, "[夹具红] 虚拟 Gamepad 未建成。");
            yield return null;
        }

        /// <summary>一次「按下 → 松开」:状态事件全量替换,故松开必须显式归零。</summary>
        IEnumerator Press(Vector2 v)
        {
            InputApi.QueueStateEvent(_pad, new GamepadState { leftStick = v });
            // 输入更新 → EventSystem.Update → module.Process → OnMove → panel.SendEvent。
            // 两帧留够:首帧处理输入与派发,次帧让焦点 / 布局稳定。
            yield return null;
            yield return null;

            InputApi.QueueStateEvent(_pad, new GamepadState { leftStick = Vector2.zero });
            yield return null;
            yield return null;
        }

        void FocusCentre()
        {
            At(1, 1).Focus();
        }

        string CurrentFocusName()
        {
            var fe = _root?.panel?.focusController?.focusedElement as VisualElement;
            return fe == null ? "<none>" : (string.IsNullOrEmpty(fe.name) ? "<unnamed>" : fe.name);
        }

        void OnNavMove(NavigationMoveEvent e)
        {
            _navEvents++;
            _focusTrail.Add(CurrentFocusName());
        }

        /// <summary>与检查卡 §5.1 同装置:3×3 绝对定位(几何序按 worldBound 算,不能走 flex)。</summary>
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

        static object InvokePrivate(object target, string method)
        {
            var mi = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(mi, $"[夹具红] {target.GetType().Name}.{method} 未找到(私有方法名漂移)");
            return mi.Invoke(target, null);
        }
    }
}
