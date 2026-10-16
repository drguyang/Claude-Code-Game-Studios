// 权威来源:production/epics/audio-system/story-012-music-guardrails-perf-vr.md
//   (AC-44-E2 后半 —— 「AC-10-08 的测量点注册表不含 IAudioCueSink.Emit」的静态核对)
// GDD:design/gdd/audio-system.md AC-44-E2 :1123-1128
//   · :1125「60 s 采样窗,读 Audio.Process / DSP 回调占用,P95 ≤ AUDIO_BUDGET_MS」
//   · :1127「且 AC-10-08 的测量点注册表不含 IAudioCueSink.Emit(静态核对 = 「不在
//     <50 ms 路径内」的可测形态)」
// ADR-011 §二(急救直读通道 < 50 ms)· F-10.6(两个延迟预算的切分:AC-10-08 **只测 L_input**)
//   · ADR-018 §一(44 只触发 / 只渲染)
//
// ⚠️ **本类是 S14-4 的交付物之一,用户裁定 2026-10-16** —— 补评判定该 AC 的测试断言的宾语是
//    「AC 的判据散文存在于 GDD」,不是「某个注册表不含 Emit」(🟡 半真)。裁定取
//    **「44 侧生产闭集 + 反空转守卫」**:集合由 44 声明、判据由本类承载、空集即红。
// ⚠️ **归属诚实声明**:`AC-10-08` 是**系统 10** 的 AC(其测量点 = 直读回调 → 该帧呈现结束,
//    见 `emergency-procedures.md` F-10.6)。本类**不声称拥有** 10 的测量点定义 —— 它承载的是
//    44 侧**可静态核对的那一半**:「音频发射不在该路径上」。10 侧的真注册表落地时,
//    **本类应改为读它**(同 `MixerRegistry` ← `MixerTopologyGates` 的「引用禁另抄」纪律)。
// ⚠️ **集合非空是判据的一部分**(反空转守卫)—— 空集对「不含 X」平凡成立,
//    正是评审原件 §1.3 命名的「空转真扫」。与 `PresentationDtoGuard.Scan(null)` 同款:
//    **拒以空集冒充绿**。
// ⚠️ **本类不持游戏状态**(ADR-018 §一):持的是「哪些点在延迟测量路径上」的**静态名字闭集**。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>`AC-10-08` 延迟测量路径的**测量点名字闭集**(44 侧可静态核对的那一半)。
    /// <para>判据三条:① 集合**非空**(反空转);② 集合**不含**音频发射入口
    /// (<see cref="EmitSymbol"/>);③ 集合名 ∈ <see cref="Ac1008Points"/> 的闭集语义
    /// —— 扩点 = 改本类(与 `MixerRegistry` 的「名字闭集 = 代码常量」同纪律)。</para>
    /// <para>⚠️ **为什么是「不含」而不是「含」**:`AC-10-08` 的测量点 = **直读回调 → 该帧呈现结束**
    /// (F-10.6:`L_input` 只测预表现路径)。音频在预表现**之后**触发 ⇒ 它的发射入口
    /// **不在**该路径上。这条「不含」就是「音频不在急救 &lt;50 ms 预算内」的可测形态。</para></summary>
    public static class AudioMeasurementPoints
    {
        /// <summary>`AC-10-08` 的测量点(起点 / 终点;F-10.6 的「直读回调 → 该帧呈现结束」)。
        /// <para>⚠️ **这 2 个名字是 44 侧的核对基准,不是 10 侧注册表的权威定义** ——
        /// 10 侧真注册表落地时本列表应改为引用它(见文件头「归属诚实声明」)。</para></summary>
        public static readonly IReadOnlyList<string> Ac1008Points = new[]
        {
            "DirectReadCallback",
            "FramePresentationEnd",
        };

        /// <summary>**必须不在**测量点集内的音频发射入口符号 —— 绑定到真契约成员
        /// (<c>nameof</c> 编译期绑定 ⇒ 契约改名本常量随之改,不会静默失配)。</summary>
        public static readonly string EmitSymbol =
            nameof(IAudioCueSink) + "." + nameof(IAudioCueSink.Emit);

        /// <summary>校验一份测量点集(错误列表形态:空 = 通过)。
        /// <para>⚠️ `null` 与**空集**都是错误,且**错误文本不同** —— `null` = 输入缺失,
        /// 空集 = 反空转守卫(「不含 X」对空集平凡成立)。两者混为一谈会让
        /// 「注册表忘了填」与「注册表被清空」不可区分。</para></summary>
        /// <param name="points">待校验的测量点集。</param>
        /// <returns>错误列表(空 = 通过)。</returns>
        public static IReadOnlyList<string> Validate(IReadOnlyList<string> points)
        {
            var errors = new List<string>();

            if (points == null)
            {
                errors.Add(
                    "[NOT-RUN 守卫] 测量点集缺失(null)—— AC-44-E2 的静态核对不可执行" +
                    "(与「空集」区分:null = 输入缺失,空集 = 反空转违例)");
                return errors;
            }

            if (points.Count == 0)
            {
                errors.Add(
                    "[反空转守卫] 测量点集为**空** —— 「不含 " + EmitSymbol + "」对空集**平凡成立**," +
                    "零违例不等于通过(评审原件 §1.3「空转真扫」同型)。AC-44-E2");
                return errors;
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (string.Equals(points[i], EmitSymbol, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"测量点「{points[i]}」= 音频发射入口 —— AC-44-E2 逐字要求该注册表**不含**它" +
                        "(音频在预表现之后触发,不在急救 <50 ms 路径内;F-10.6 / ADR-011 §二)");
                }
            }

            return errors;
        }
    }
}
