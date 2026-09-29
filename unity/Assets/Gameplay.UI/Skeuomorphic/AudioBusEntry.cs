// 权威来源:design/gdd/skeuomorphic-ui.md 规则八 + AC-42-G2(条目语义归 44)
//   · design/ux/settings-shell-42.md
//   · production/epics/skeuomorphic-ui/story-014-settings-shell.md
//
// 设计说明:
//   · 42 只提供壳与焦点;音量/mono 的读写总线参数归 44。
//   · 禁在 42 内缓存音量值(缓存 = 第二份真相,破规则六)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using UnityEngine.UIElements;

    /// <summary>设置壳音频总线条目(AC-42-G2)—— 只持壳,不持值。</summary>
    public sealed class AudioBusEntry : VisualElement
    {
        /// <summary>总线语义标识(标签展示用;值读取归 44)。</summary>
        public string BusLabel { get; }

        /// <summary>创建总线条目。</summary>
        public AudioBusEntry(string busLabel)
        {
            BusLabel = busLabel ?? string.Empty;
            AddToClassList("audio-bus-entry");
        }

        // 刻意无 volume/mono 值字段 —— AC-42-G2:42 不缓存他系统状态
    }
}
