namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    using System;

    /// <summary>输入设备类型枚举。</summary>
    public enum InputDeviceType : byte
    {
        /// <summary>键盘 + 鼠标。</summary>
        KeyboardMouse = 0,

        /// <summary>游戏手柄。</summary>
        Gamepad = 1,

        /// <summary>VR 控制器(仅 VR 急救模式)。</summary>
        VRController = 2
    }

    /// <summary>导航方向枚举。</summary>
    /// <remarks>
    /// 与 Unity 的 <see cref="UnityEngine.UIElements.NavigationMoveEvent"/> 方向语义对齐;
    /// 本枚举不引用引擎类型,纯 C# 契约面。
    /// </remarks>
    public enum NavigationDirection : byte
    {
        /// <summary>向左 / 上一位(水平或垂直方向的负方向)。</summary>
        Left = 0,

        /// <summary>向右 / 下一位(水平或垂直方向的正方向)。</summary>
        Right = 1,

        /// <summary>向上。</summary>
        Up = 2,

        /// <summary>向下。</summary>
        Down = 3,

        /// <summary>前一页 / 上一层级。</summary>
        Previous = 4,

        /// <summary>后一页 / 下一层级。</summary>
        Next = 5
    }

    /// <summary>
    /// 焦点导航意图——3 侧产出的单向只读视图。
    /// <para>3(输入系统)产出此 DTO;42(呈现层)消费;3 不驱动焦点移动。</para>
    /// </summary>
    /// <remarks>
    /// 此类型是纯数据载体,不持有任何方法。42 通过 <see cref="IFocusNavigationPresenter"/>
    /// 单栈门路由消费此意图。
    /// </remarks>
    public readonly struct FocusNavigationIntent
    {
        /// <summary>导航方向。</summary>
        public NavigationDirection Direction { get; }

        /// <summary>输入设备类型。</summary>
        public InputDeviceType DeviceType { get; }

        /// <summary>输入发生时的整数时间戳(由 ITickProvider 驱动,与 tick 对齐)。</summary>
        public long TickTimestamp { get; }

        /// <summary>是否为加速导航(如手柄长按摇杆 / 键盘按住方向键)。</summary>
        public bool IsAccelerated { get; }

        /// <summary>初始化导航意图。</summary>
        /// <param name="direction">导航方向。</param>
        /// <param name="deviceType">输入设备类型。</param>
        /// <param name="tickTimestamp">整数时间戳(tick 对齐)。</param>
        /// <param name="isAccelerated">是否为加速导航。</param>
        public FocusNavigationIntent(
            NavigationDirection direction,
            InputDeviceType deviceType,
            long tickTimestamp,
            bool isAccelerated)
        {
            Direction = direction;
            DeviceType = deviceType;
            TickTimestamp = tickTimestamp;
            IsAccelerated = isAccelerated;
        }

        /// <summary>空意图(无导航请求)。</summary>
        public static FocusNavigationIntent None { get; } = new FocusNavigationIntent(
            NavigationDirection.Left, InputDeviceType.KeyboardMouse, 0, false);
    }
}
