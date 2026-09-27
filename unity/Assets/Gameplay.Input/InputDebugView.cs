// Story 012 · 开发者调试视图(仅 Development Build / Editor 可见;AC-3-E2 / AC-3-UI-2)
//
// 权威来源:
//   GDD input-system.md §UI Requirements 二 · AC-3-E2 · AC-3-UI-2
//   ADR-013 §9 C3(42 只渲染,不读焦点栈) · ADR-019 §五(51 住边界层不进 sim)
//
// 条件:
//   ① 仅 Development Build / Editor 编译(#if UNITY_EDITOR || DEVELOPMENT_BUILD)
//   ② 玩家构建中不存在此代码路径(条件编译保证)
//   ③ 不显示 raw 轴值数值(显示语义值或状态,不显示浮点原始值)
//   ④ 不读 42 焦点栈(ADR-013 §9 C3;焦点信息在 42 自有调试视图)

using UnityEngine;
using UnityEngine.InputSystem;
using DaYiJingCheng.Gameplay.Input.Intents;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>开发者调试视图(Development Build / Editor 可见;玩家构建零存在)。
    /// 附着于 InputService 的 GameObject,OnGUI 内绘制调试信息。</summary>
    /// <remarks>
    /// <para><b>四条件</b>:① 条件编译 ② 玩家构建零代码路径 ③ 不显示 raw 轴值 ④ 不读焦点栈。</para>
    /// <para><b>显示内容</b>:设备态 · 直读通道态 · 最近一次意图 · overrides 装载结果。</para>
    /// </remarks>
    public sealed class InputDebugView : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("字体大小(像素)")]
        [Range(10, 24)] public int fontSize = 14;

        [Tooltip("面板背景透明度(0 = 透明, 1 = 不透明)")]
        [Range(0f, 1f)] public float backgroundAlpha = 0.7f;

        /// <summary>当前设备态(由 InputService 更新)。</summary>
        public DeviceState DeviceState { get; set; }

        /// <summary>直读通道当前态(由 EmergencyDirectReadChannel 更新)。</summary>
        public DirectChannelState ChannelState { get; set; }

        /// <summary>最近一次导航意图(由 InputService 更新);null = 无。</summary>
        public FocusNavigationIntent? LastIntent { get; set; }

        /// <summary>overrides 装载结果(hit / mismatch-cleared / none)。</summary>
        public string OverridesStatus { get; set; } = "none";

        /// <summary>通道采样计数(由 EmergencyDirectReadChannel 更新;0 = 未知/未接线)。</summary>
        public int ChannelSampleCount { get; set; }

        /// <summary>调试视图开关(运行时可按 F6 切换)。</summary>
        public static bool IsVisible { get; set; } = true;

        private const int PanelWidth = 280;
        private const int LineHeight = 20;
        private const int Padding = 8;

        private void OnGUI()
        {
            if (!IsVisible) return;

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F6)
            {
                IsVisible = !IsVisible;
                e.Use();
                return;
            }

            GUI.depth = -1000;
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 2,
                fontStyle = FontStyle.Bold
            };

            int lineCount = 6; // 标题 + 5 行数据
            int height = lineCount * LineHeight + Padding * 2;
            Rect panelRect = new Rect(Padding, Padding, PanelWidth, height);

            Color bg = new Color(0.1f, 0.1f, 0.1f, backgroundAlpha);
            GUI.backgroundColor = bg;
            GUI.Box(panelRect, GUIContent.none);
            GUI.backgroundColor = Color.white;

            GUI.Label(new Rect(Padding + 4, Padding + 2, PanelWidth - 8, LineHeight),
                "[Input Debug] F6 切换", titleStyle);

            float y = Padding + LineHeight + 2;
            GUI.Label(new Rect(Padding + 4, y, PanelWidth - 8, LineHeight),
                $"设备: {DeviceState}");
            y += LineHeight;

            GUI.Label(new Rect(Padding + 4, y, PanelWidth - 8, LineHeight),
                $"通道: {ChannelState}");
            y += LineHeight;

            if (LastIntent.HasValue)
            {
                var intent = LastIntent.Value;
                // 只显示方向枚举 + tick,不显示 raw axis 数值
                GUI.Label(new Rect(Padding + 4, y, PanelWidth - 8, LineHeight),
                    $"意图: {intent.Direction}(tick={intent.Tick})");
            }
            else
            {
                GUI.Label(new Rect(Padding + 4, y, PanelWidth - 8, LineHeight),
                    "意图: 无");
            }
            y += LineHeight;

            GUI.Label(new Rect(Padding + 4, y, PanelWidth - 8, LineHeight),
                $"Overrides: {OverridesStatus}");
            y += LineHeight;

            GUI.Label(new Rect(Padding + 4, y, PanelWidth - 8, LineHeight),
                $"采样: {ChannelSampleCount}");
        }
#endif
    }
}
