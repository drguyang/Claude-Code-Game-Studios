namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using System;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;

    /// <summary>
    /// 焦点门状态机 + 焦点导航呈现桥 单元测试。
    /// <para>覆盖 AC-42-A1 / AC-42-A3a / AC-42-A5 / AC-42-B3a / AC-42-B4 / AC-3-C1 / AC-3-C2。</para>
    /// </summary>
    [TestFixture]
    public class focus_gate_and_bridge_test
    {
        // ── AC-42-B3a: 焦点门三态切换 + 过渡窗口防止重入 ──

        [Test]
        public void test_focus_gate_three_state_transition()
        {
            // Arrange
            var sm = new FocusGateStateMachine();
            Assert.AreEqual(FocusGateState.FlatActive, sm.CurrentState);

            // Act: 切换到世界空间
            bool result = sm.RequestTransition(FocusGateState.WorldActive);

            // Assert: 进入过渡态
            Assert.IsTrue(result, "切换请求应被接受。");
            Assert.AreEqual(FocusGateState.Transitioning, sm.CurrentState);
            Assert.AreEqual(FocusGateState.WorldActive, sm.TargetState);

            // Act: 过渡窗口内再次请求同一目标(幂等)
            bool reentrant = sm.RequestTransition(FocusGateState.WorldActive);
            Assert.IsFalse(reentrant, "过渡窗口内幂等请求应被忽略。");

            // Act: 完成过渡
            bool completed = sm.CompleteTransition();
            Assert.IsTrue(completed, "过渡应完成。");
            Assert.AreEqual(FocusGateState.WorldActive, sm.CurrentState);
            Assert.IsNull(sm.TargetState);
        }

        [Test]
        public void test_no_two_gates_open_at_any_point()
        {
            // Arrange
            var sm = new FocusGateStateMachine();
            var observedStates = new System.Collections.Generic.List<FocusGateState>();

            // 订阅全部状态变更,记录中间态序列
            sm.StateChanged += (_, e) =>
            {
                observedStates.Add(e.CurrentState);
            };

            // Act: FlatActive → WorldActive 完整切换
            sm.RequestTransition(FocusGateState.WorldActive);
            sm.CompleteTransition();

            // Assert: FlatActive 与 WorldActive 互斥——切换序列中两者不得同时出现
            bool sawFlat = false, sawWorld = false;
            foreach (var state in observedStates)
            {
                if (state == FocusGateState.FlatActive) sawFlat = true;
                if (state == FocusGateState.WorldActive) sawWorld = true;
                if (state == FocusGateState.Transitioning)
                {
                    Assert.IsFalse(sawFlat && sawWorld,
                        "过渡期间 FlatActive 与 WorldActive 不得同时出现(两门互斥)。");
                }
            }

            // 最终态 = WorldActive(世界空间独占)
            Assert.IsTrue(sawWorld, "切换序列应以 WorldActive 结束。");
            Assert.AreEqual(FocusGateState.WorldActive, sm.CurrentState);
        }

        [Test]
        public void test_transition_cancel_rolls_back()
        {
            // Arrange
            var sm = new FocusGateStateMachine();
            sm.RequestTransition(FocusGateState.WorldActive);

            // Act: 取消过渡
            bool cancelled = sm.CancelTransition();

            // Assert: 保守回退至 FlatActive
            Assert.IsTrue(cancelled);
            Assert.AreEqual(FocusGateState.FlatActive, sm.CurrentState);
            Assert.IsNull(sm.TargetState);
        }

        [Test]
        public void test_complete_transition_when_not_transitioning_returns_false()
        {
            // Arrange
            var sm = new FocusGateStateMachine();

            // Act: 非过渡态调用 CompleteTransition
            bool result = sm.CompleteTransition();

            // Assert
            Assert.IsFalse(result, "非过渡态调用 CompleteTransition 应返回 false。");
        }

        [Test]
        public void test_request_transition_to_current_state_is_noop()
        {
            // Arrange
            var sm = new FocusGateStateMachine();
            int eventCount = 0;
            sm.StateChanged += (_, __) => eventCount++;

            // Act: 请求切换到当前态(FlatActive)
            bool result = sm.RequestTransition(FocusGateState.FlatActive);

            // Assert
            Assert.IsFalse(result, "请求切换到当前态应幂等忽略。");
            Assert.AreEqual(0, eventCount, "幂等忽略不应触发 StateChanged。");
            Assert.AreEqual(FocusGateState.FlatActive, sm.CurrentState);
        }

        // ── AC-42-A5: 两栈 IPresentationRoot 均经同一 SetFocusGate 入口 ──

        [Test]
        public void test_both_stacks_via_set_focus_gate()
        {
            // Arrange: 模拟两栈实现
            var flatStack = new MockPresentationRoot();
            var worldStack = new MockPresentationRoot();

            // 初始态:平面独占
            flatStack.SetFocusGate(true);
            worldStack.SetFocusGate(false);
            Assert.IsTrue(flatStack.Active);
            Assert.IsFalse(worldStack.Active);

            // Act: 切换至世界空间(模拟状态机完成过渡)
            flatStack.SetFocusGate(false);
            worldStack.SetFocusGate(true);

            // Assert: 状态变更经 SetFocusGate 路由,无旁路
            Assert.IsFalse(flatStack.Active, "平面栈应关闭。");
            Assert.IsTrue(worldStack.Active, "世界栈应开启。");
        }

        // ── AC-3-C1: FocusNavigationIntent 单向只读 ──

        [Test]
        public void test_input_system_exposes_intent_only()
        {
            // Arrange
            var intent = new FocusNavigationIntent(
                NavigationDirection.Right,
                InputDeviceType.Gamepad,
                tickTimestamp: 100,
                isAccelerated: true);

            // Act: 读取全部属性(只读视图)
            var direction = intent.Direction;
            var deviceType = intent.DeviceType;
            var tick = intent.TickTimestamp;
            var accelerated = intent.IsAccelerated;

            // Assert: 全部 getter 返回预期值
            Assert.AreEqual(NavigationDirection.Right, direction);
            Assert.AreEqual(InputDeviceType.Gamepad, deviceType);
            Assert.AreEqual(100, tick);
            Assert.IsTrue(accelerated);

            // None 意图是默认值
            var none = FocusNavigationIntent.None;
            Assert.AreEqual(NavigationDirection.Left, none.Direction);
            Assert.AreEqual(0, none.TickTimestamp);
        }

        [Test]
        public void test_focus_navigation_intent_equality()
        {
            // Arrange
            var a = new FocusNavigationIntent(NavigationDirection.Up, InputDeviceType.KeyboardMouse, 50, false);
            var b = new FocusNavigationIntent(NavigationDirection.Up, InputDeviceType.KeyboardMouse, 50, false);
            var c = new FocusNavigationIntent(NavigationDirection.Down, InputDeviceType.KeyboardMouse, 50, false);

            // Assert: 字段级相等
            Assert.AreEqual(a, b);  // value-type equality
            Assert.AreNotEqual(a, c);
        }

        // ── AC-3-C2: 构造期同键双触发断言 ──

        [Test]
        public void test_no_dual_binding_same_control()
        {
            // Arrange
            var controlName = "test-control";

            // Act + Assert: 同键双触发应抛 InvalidOperationException
            var ex = Assert.Throws<InvalidOperationException>(() =>
                FocusNavigationBridge.AssertNoDualBinding(
                    controlName: controlName,
                    hasOfficialNavigate: true,
                    hasCustomFocusAction: true));

            StringAssert.Contains("同键双触发", ex.Message);
            StringAssert.Contains("test-control", ex.Message);
        }

        [Test]
        public void test_single_binding_is_allowed()
        {
            // Arrange
            var controlName = "safe-control";

            // Act: 仅官方 Navigate 绑定(无自建焦点动作)
            Assert.DoesNotThrow(() =>
                FocusNavigationBridge.AssertNoDualBinding(
                    controlName: controlName,
                    hasOfficialNavigate: true,
                    hasCustomFocusAction: false));

            // Act: 仅自建焦点动作(无官方 Navigate)
            Assert.DoesNotThrow(() =>
                FocusNavigationBridge.AssertNoDualBinding(
                    controlName: controlName,
                    hasOfficialNavigate: false,
                    hasCustomFocusAction: true));
        }

        // ── AC-42-B4: IFocusNavigationPresenter 导出面不包含邻居/投影/几何查询类型 ──

        [Test]
        public void test_presenter_export_surface_clean()
        {
            // Arrange: 反射扫描 IFocusNavigationPresenter 及其所在程序集的所有 public 类型
            var presenterType = typeof(IFocusNavigationPresenter);
            var assembly = presenterType.Assembly;
            var publicTypes = assembly.GetTypes()
                .Where(t => t.IsPublic || t.IsNestedPublic)
                .ToList();

            // 查找是否存在「邻居枚举 / 方向投影 / 几何查询」类入口
            // 这些类的特征是:包含 NavigationDirection / Neighbor / Projection / Geometry 等名称
            var forbiddenNames = new[] { "Neighbor", "Projection", "Geometry", "DirectionalProjection", "NeighborEnum" };

            var violations = publicTypes
                .Where(t => t.IsClass && !t.IsInterface)
                .Where(t => forbiddenNames.Any(fn => t.Name.Contains(fn, StringComparison.Ordinal)))
                .ToList();

            Assert.IsEmpty(violations,
                $"导出面发现禁入类: {string.Join(", ", violations.Select(v => v.FullName))}。"
                + " AC-42-B4: IFocusNavigationPresenter 导出面不得包含「邻居枚举 / 方向投影 / 几何查询」类入口。");
        }

        // ── AC-42-A3a: 契约侧不引用桥专有类型 ──

        [Test]
        public void test_contract_side_no_bridge_types()
        {
            // Arrange: 契约类型清单
            var contractTypes = new[]
            {
                typeof(IFocusNavigationPresenter),
                typeof(FocusNavigationIntent),
                typeof(IPresentationRoot),
                typeof(IModalState),
                typeof(ModalId),
                typeof(FocusGateState),
                typeof(FocusGateStateMachine)
            };

            // Act: 检查每个契约类型的 Assembly 引用集
            // Gameplay.Presentation 程序集引用 UnityEngine 是合法的(含实现侧 FocusNavigationBridge)
            // 本测试验证的是:契约接口的公开方法签名不强制消费者引用桥专有类型
            var contractAssembly = typeof(FocusGateState).Assembly;
            var publicInterfaces = contractAssembly.GetTypes()
                .Where(t => t.IsInterface && t.IsPublic)
                .ToList();

            foreach (var iface in publicInterfaces)
            {
                foreach (var method in iface.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    // 契约方法返回类型 / 参数类型不得引用 FocusNavigationBridge
                    if (method.ReturnType != null && method.ReturnType.Name.Contains("FocusNavigationBridge"))
                    {
                        Assert.Fail($"契约接口 {iface.FullName} 的方法 {method.Name} 返回类型引用了桥专有类型。");
                    }

                    foreach (var param in method.GetParameters())
                    {
                        if (param.ParameterType.Name.Contains("FocusNavigationBridge"))
                        {
                            Assert.Fail($"契约接口 {iface.FullName} 的方法 {method.Name} 的参数引用了桥专有类型。");
                        }
                    }
                }
            }
        }

        // ── AC-42-A1: 两栈共享同一 EventSystem ──
        // (运行时验证,此处仅做接口存在性检查)

        [Test]
        public void test_two_stacks_share_single_event_system_interface()
        {
            // Arrange
            var bridgeType = typeof(FocusNavigationBridge);

            // Act: 验证桥接线持有共享 EventSystem 字段
            var eventSystemField = bridgeType.GetField("_sharedEventSystem",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // Assert: 桥接线设计为接收单一 EventSystem 引用
            Assert.IsNotNull(eventSystemField,
                "FocusNavigationBridge 必须持有共享 EventSystem 引用(AC-42-A1)。");

            // 字段名与类型名一致即可;EditMode 测试环境不加载 UnityEngine.EventSystems 程序集,
            // 故不做 Type.GetType 解析(会返回 null)。
            Assert.AreEqual("_sharedEventSystem", eventSystemField.Name);
            Assert.AreEqual("UnityEngine.EventSystems.EventSystem", eventSystemField.FieldType.FullName);
        }

        // ── Mock 工具 ──

        /// <summary>模拟 IPresentationRoot(测试用)。</summary>
        private sealed class MockPresentationRoot : IPresentationRoot
        {
            public bool Active { get; private set; }

            public void SetFocusGate(bool active)
            {
                Active = active;
            }

            public void Bind(DaYiJingCheng.Sim.Contracts.IDtoSource source)
            {
                // 测试用空实现
            }
        }
    }
}
