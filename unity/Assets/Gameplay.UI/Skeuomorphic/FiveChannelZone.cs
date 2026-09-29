// 权威来源:design/gdd/skeuomorphic-ui.md §7.2 / UI-8.1「五通道区 + 两栏」
//   · production/epics/skeuomorphic-ui/story-011-casebook-rendering.md
//
// 设计说明:
//   · 脉案页五通道:面色 / 语声 / 呼吸 / 触感 / 病名。
//   · 42 只画布局与焦点;通道语义归 8(诊断),值经 VitalsDto。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System.Collections.Generic;

    /// <summary>脉案五通道枚举(布局语义序 = 面色 → 语声 → 呼吸 → 触感 → 病名)。</summary>
    public enum FiveChannel
    {
        Complexion = 0,
        Voice = 1,
        Breathing = 2,
        Palpation = 3,
        DiseaseName = 4,
    }

    /// <summary>脉案页五通道区(布局载体)。</summary>
    public sealed class FiveChannelZone
    {
        /// <summary>通道数(恒 5)。</summary>
        public const int ChannelCount = 5;

        /// <summary>按语义序枚举通道(手柄走查序 = UI-8.2 语义序)。</summary>
        public static IEnumerable<FiveChannel> InSemanticOrder()
        {
            yield return FiveChannel.Complexion;
            yield return FiveChannel.Voice;
            yield return FiveChannel.Breathing;
            yield return FiveChannel.Palpation;
            yield return FiveChannel.DiseaseName;
        }
    }
}
