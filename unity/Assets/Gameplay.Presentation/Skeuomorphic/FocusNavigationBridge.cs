namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;

    /// <summary>
    /// 焦点导航呈现桥——单栈门路由 + 过渡窗口协调 + 同键双触发构造断言 + 焦点悬空回退。
    /// <para>实现侧(MonoBehaviour),持有 Unity 引擎类型引用。</para>
    /// </summary>
    /// <remarks>
    /// 职责:
    /// <list type="number">
    ///   <item><description>持有 <see cref="FocusGateStateMachine"/> 并驱动三态切换(先关后开,过渡窗口 0 ≤ W_trans ≤ 1 frame)。</description></item>
    ///   <item><description>管理两栈的 <see cref="IPresentationRoot"/> 引用;过渡期间调用 <see cref="IPresentationRoot.SetFocusGate"/> 协调开关。</description></item>
    ///   <item><description>验证 EventSystem 共享前提(AC-42-A1)。</description></item>
    ///   <item><description>提供 <see cref="IFocusNavigationPresenter.IsFocusActive"/> 查询。</description></item>
    ///   <item><description>构造期断言:拒绝同一控件同时被官方 Navigate 与自建焦点动作绑定(AC-3-C2)。</description></item>
    ///   <item><description>焦点悬空回退:当前焦点控件被销毁/禁用时,自动回退到最近有效控件(AC-42-B7)。</description></item>
    ///   <item><description>降级路径桩(预先写死):若 Unity 6.3 焦点质量不达预期,降级 = 自实现焦点算法;接口不变,代码住本程序集。</description></item>
    /// </list>
    /// </remarks>
    public sealed class FocusNavigationBridge : MonoBehaviour, IFocusNavigationPresenter
    {
        /// <summary>过渡窗口最大长度(1 帧 @ 60 fps ≈ 16.67 ms)。</summary>
        public const float TransitionWindowMaxSeconds = 1f / 60f;

        /// <summary>共享 EventSystem(两栈共用,禁双 EventSystem)。</summary>
        [SerializeField]
        private EventSystem _sharedEventSystem;

        /// <summary>平面拟物 UI 栈(UI Toolkit)的呈现层根。</summary>
        [SerializeField]
        private IPresentationRoot _flatStack;

        /// <summary>世界空间 UI 栈(UGUI world canvas)的呈现层根。</summary>
        [SerializeField]
        private IPresentationRoot _worldStack;

        /// <summary>焦点门状态机。</summary>
        private FocusGateStateMachine _stateMachine;

        /// <summary>焦点导航当前是否活跃(有焦点可导航的界面摊开)。</summary>
        public bool IsFocusActive { get; private set; }

        /// <summary>降级模式是否激活。</summary>
        /// <remarks>
        /// spike 判定引擎焦点质量不达预期时,外部系统(如 42 初始化流程)将此置为 true,
        /// 触发降级路径(自实现焦点算法,接口签名不变)。
        /// </remarks>
        public bool IsDowngradeActive { get; private set; }

        /// <summary>过渡窗口剩余时间(秒)。</summary>
        public float TransitionRemaining { get; private set; }

        /// <summary>焦点当前是否处于悬空态(无有效控件可聚焦)。</summary>
        /// <remarks>
        /// true = 当前焦点控件被销毁/禁用,且无有效回退目标(全部控件均无效)。
        /// <para>焦点悬空态 ≠ 焦点卡在已销毁控件上 —— 本属性为 true 时,UI 应呈现"无焦点"视觉反馈。</para>
        /// </remarks>
        public bool IsFocusOrphaned { get; private set; }

        /// <summary>焦点悬空后的回退 rank(仅在 <see cref="IsFocusOrphaned"/> 为 true 时有效)。</summary>
        /// <remarks>
        /// 由 <see cref="FocusOrphanHandler"/> 计算得出;值 >= 1 表示有效回退 rank, -1 表示无回退。
        /// </remarks>
        public int FocusOrphanFallbackRank { get; private set; }

        /// <summary>全部可聚焦控件集合(用于焦点悬空回退)。</summary>
        /// <remarks>
        /// 外部系统(如 42 UI 初始化)在控件列表变更时更新本字段。
        /// <para>集合中的控件必须实现 <see cref="IFocusable"/>;</para>
        /// <para>rank 数据完整性由 <see cref="DaYiJingCheng.Gameplay.UI.Skeuomorphic.FocusBoundaryAssertions"/> 在装载时校验。</para>
        /// </remarks>
        public IEnumerable<IFocusable> AllFocusableControls
        {
            get => _allFocusableControls;
            set
            {
                _allFocusableControls = value ?? throw new ArgumentNullException(nameof(value));
                CheckFocusOrphan();
            }
        }

        /// <summary>当前焦点的 rank(-1 = 尚未设定)。</summary>
        private int _currentFocusRank = -1;

        private IEnumerable<IFocusable> _allFocusableControls;
        private float _transitionTotal;
        private bool _isInitialized;
        private bool _pendingTransition;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="eventSystem">共享 EventSystem(两栈共用)。</param>
        /// <param name="flatStack">平面拟物 UI 栈的呈现层根。</param>
        /// <param name="worldStack">世界空间 UI 栈的呈现层根。</param>
        public void Initialize(
            EventSystem eventSystem,
            IPresentationRoot flatStack,
            IPresentationRoot worldStack)
        {
            if (_isInitialized)
                throw new InvalidOperationException("[FocusNavigationBridge] 已初始化,不可重复调用。");

            _sharedEventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            _flatStack = flatStack ?? throw new ArgumentNullException(nameof(flatStack));
            _worldStack = worldStack ?? throw new ArgumentNullException(nameof(worldStack));

            // AC-42-A1: 验证两栈共享同一 EventSystem
            ValidateSharedEventSystem();

            _stateMachine = new FocusGateStateMachine();
            _stateMachine.StateChanged += OnStateMachineChanged;

            // 默认态 = 平面独占
            _flatStack.SetFocusGate(true);
            _worldStack.SetFocusGate(false);
            IsFocusActive = true;

            _isInitialized = true;
        }

        /// <summary>
        /// 请求切换到目标栈(焦点门状态机驱动)。
        /// <para>切换序列:当前态 → Transitioning(两门皆关) → 目标态。</para>
        /// </summary>
        /// <param name="targetState">目标状态(FlatActive / WorldActive)。</param>
        /// <returns>若状态变更实际发生则 true;幂等忽略时 false。</returns>
        public bool RequestTransition(FocusGateState targetState)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("[FocusNavigationBridge] 未初始化,请先调用 Initialize。");

            if (IsDowngradeActive)
            {
                // 降级路径:直接切换,跳过状态机
                ApplyDowngradeTransition(targetState);
                return true;
            }

            bool changed = _stateMachine.RequestTransition(targetState);
            if (changed)
            {
                // 进入过渡态:两门皆关
                _flatStack.SetFocusGate(false);
                _worldStack.SetFocusGate(false);
                IsFocusActive = false;

                // 启动过渡窗口计时(最大 1 frame)
                _transitionTotal = Math.Min(TransitionWindowMaxSeconds, Time.unscaledDeltaTime);
                TransitionRemaining = _transitionTotal;
                _pendingTransition = true;
            }

            return changed;
        }

        /// <summary>
        /// 每帧更新(由 MonoBehaviour Update 调用)。
        /// <para>处理过渡窗口计时与焦点悬空检测。</para>
        /// </summary>
        private void Tick()
        {
            if (!_isInitialized)
                return;

            // 过渡窗口计时
            if (_pendingTransition)
            {
                TransitionRemaining -= Time.unscaledDeltaTime;

                if (TransitionRemaining <= 0f)
                {
                    _pendingTransition = false;
                    TransitionRemaining = 0f;
                    _stateMachine.CompleteTransition();
                }
            }

            // 焦点悬空检测(每帧检查当前焦点是否仍有效)
            if (!IsDowngradeActive && _currentFocusRank >= 0)
            {
                CheckFocusOrphan();
            }
        }

        /// <summary>
        /// 激活降级路径(自实现焦点算法)。
        /// <para>接口签名不变;代码住 Gameplay.UI 程序集内,不新开程序集。</para>
        /// </summary>
        public void ActivateDowngrade()
        {
            IsDowngradeActive = true;
            // TODO(story-002): 降级路径 = 自实现焦点算法。
            //  触发条件:Unity 6.3 焦点质量 spike 不达预期。
            //  实现方案:自实现焦点邻居查找 + 方向投影 + 焦点移动。
            //  接口签名不变(IFocusNavigationPresenter 不增不减)。
            //  邻居枚举 / 方向投影 / 几何查询类必须标记为 private/internal(AC-42-B4)。
            //  触发后须另开 ADR 登记降级实现(不得以本 Story 直接补全)。
        }

        /// <summary>
        /// 构造期断言:拒绝同一控件同时被官方 Navigate 与自建焦点动作绑定。
        /// <para>触发条件:同一物理控件(D-pad / 左摇杆)同时绑 Input System 默认 UI action map 的 Navigate 与自建焦点动作。</para>
        /// </summary>
        /// <param name="controlName">控件名称(用于错误诊断;若元素可用则从元素取 name)。</param>
        /// <param name="hasOfficialNavigate">是否绑定了官方桥 Navigate 动作。</param>
        /// <param name="hasCustomFocusAction">是否绑定了自建焦点动作。</param>
        /// <exception cref="InvalidOperationException">同键双触发。</exception>
        public static void AssertNoDualBinding(
            string controlName,
            bool hasOfficialNavigate,
            bool hasCustomFocusAction)
        {
            if (hasOfficialNavigate && hasCustomFocusAction)
            {
                string name = string.IsNullOrEmpty(controlName) ? "(unnamed)" : controlName;
                throw new InvalidOperationException(
                    $"[FocusNavigationBridge] 同键双触发:控件「{name}」同时绑定了官方 Navigate 与自建焦点动作。"
                    + " 请移除其中一个绑定。");
            }
        }

        /// <summary>
        /// 验证两栈共享同一 EventSystem(AC-42-A1)。
        /// </summary>
        private void ValidateSharedEventSystem()
        {
            if (_sharedEventSystem == null)
                throw new InvalidOperationException(
                    "[FocusNavigationBridge] EventSystem 为空;两栈必须共享同一 EventSystem。");

            // 检查场景中是否存在第二个 EventSystem
            var allEventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(includeInactive: true);
            if (allEventSystems.Length > 1)
            {
                throw new InvalidOperationException(
                    $"[FocusNavigationBridge] 场景中发现 {allEventSystems.Length} 个 EventSystem;"
                    + "两栈必须共享同一 EventSystem(禁双 EventSystem)。");
            }
        }

        /// <summary>
        /// 检查当前焦点是否悬空;若悬空则触发回退(AC-42-B7)。
        /// <para>每帧由 <see cref="Tick()"/> 调用;控件列表变更时由 <see cref="AllFocusableControls"/> setter 调用。</para>
        /// </summary>
        private void CheckFocusOrphan()
        {
            if (_currentFocusRank < 0 || _allFocusableControls == null)
                return;

            IFocusable currentControl = null;
            foreach (var control in _allFocusableControls)
            {
                if (control != null && control.IsFocusEnabled && control.FocusRank == _currentFocusRank)
                {
                    currentControl = control;
                    break;
                }
            }

            if (currentControl != null)
            {
                IsFocusOrphaned = false;
                FocusOrphanFallbackRank = -1;
                return;
            }

            int fallbackRank = FocusOrphanHandler.HandleOrphan(_allFocusableControls, _currentFocusRank);
            if (fallbackRank >= 0)
            {
                _currentFocusRank = fallbackRank;
                IsFocusOrphaned = false;
                FocusOrphanFallbackRank = fallbackRank;
            }
            else
            {
                _currentFocusRank = -1;
                IsFocusOrphaned = true;
                FocusOrphanFallbackRank = -1;
            }
        }

        /// <summary>
        /// 状态机变更回调:协调两栈 SetFocusGate。
        /// </summary>
        private void OnStateMachineChanged(object sender, FocusGateStateChangedEventArgs e)
        {
            switch (e.CurrentState)
            {
                case FocusGateState.FlatActive:
                    _flatStack.SetFocusGate(true);
                    _worldStack.SetFocusGate(false);
                    IsFocusActive = true;
                    break;

                case FocusGateState.WorldActive:
                    _flatStack.SetFocusGate(false);
                    _worldStack.SetFocusGate(true);
                    IsFocusActive = true;
                    break;

                case FocusGateState.Transitioning:
                    // 两门皆关已在 RequestTransition 中处理
                    IsFocusActive = false;
                    break;

                default:
                    // 防御性:未知态回退至平面独占
                    _flatStack.SetFocusGate(true);
                    _worldStack.SetFocusGate(false);
                    IsFocusActive = true;
                    break;
            }
        }

        /// <summary>
        /// 降级路径过渡(直接切换,跳过状态机)。
        /// </summary>
        private void ApplyDowngradeTransition(FocusGateState targetState)
        {
            switch (targetState)
            {
                case FocusGateState.FlatActive:
                    _flatStack.SetFocusGate(true);
                    _worldStack.SetFocusGate(false);
                    IsFocusActive = true;
                    break;

                case FocusGateState.WorldActive:
                    _flatStack.SetFocusGate(false);
                    _worldStack.SetFocusGate(true);
                    IsFocusActive = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(targetState), targetState,
                        "[FocusNavigationBridge] 降级路径不接受 Transitioning 作为目标。");
            }
        }

        private void Update()
        {
            Tick();
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
                _stateMachine.StateChanged -= OnStateMachineChanged;
        }
    }
}
