// emergency-procedures Story 006 — 手感/预表现/键鼠回退
//
// 权威来源:
//   GDD emergency-procedures.md: F-10.6 延迟预算切分 · 规则十二 · §Game Feel
//   ADR-011 §二 + Amendment B: 直读通道 <50 ms; 本地判定 = 预表现
//   ADR-018 §六: 无提示音铁律（不因 JudgeResult 改变音色/素材/强度）
//   ADR-013: 呈现 DTO 无评价字段; mag_last 仅表现
//
// 核心机制:
//   - 键鼠回退: magnitude ≡ MAG_MAX（幅度门恒过、节奏+稳度双门照评、cause = 降级）
//   - 预表现: 本地 Judge 不写流、不发成长
//   - F-10.6 口径: L_input（输入→表现呈现）与 L_eval（主机 Append→体征可见）不合并

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 手感/预表现/键鼠回退求值器。
    /// </summary>
    public static class FeelLatencyEvaluator
    {
        public const int MAG_MAX = 65536; // 1.0 in Q16.16（值归用户数值轮）

        /// <summary>
        /// 创建键鼠回退读数（规则十二 · Edge Cases）。
        /// 键鼠无模拟量通道 ⇒ magnitude ≡ MAG_MAX, cause = 降级
        /// </summary>
        public static EmergencyReading CreateKeyboardFallbackReading()
        {
            return new EmergencyReading(
                action: 0,
                holdTicks: 100,
                edges: 3,
                edgeTicks: new[] { 10, 20, 30 },
                magnitude: MAG_MAX);
        }

        /// <summary>
        /// 键鼠回退读数的**生产形状**(2026-10-19 · TD-epic-01 第九片 · AC-10-21 的幅度半边):
        /// 节奏 / 稳度两门的输入**原样取自本次动作**(GDD Edge Cases `:728`「只走节奏 + 稳度双门」),
        /// **只把幅度置为 <see cref="MAG_MAX"/>** ⇒ 幅度门恒过。
        /// </summary>
        /// <remarks>
        /// <para>与无参重载的区别:无参版是**形状示意**(夹具时代的固定样本 100/3/<c>{10,20,30}</c>),
        /// 计时与边沿是写死的假数;本重载把它们换成**真实采样**,只有幅度是回填值。
        /// 10 侧生产求值(<c>HostEmergencyProcessor</c>)走本重载。</para>
        /// <para>⚠️ 回填**不写回载荷**:载荷是判定输入(ADR-011 Amendment B C 路),
        /// 记录玩家真给了什么;回填只作用于本次 Judge 调用(F-10.4 `mag_peak` 的「判定量」语义不变)。</para>
        /// </remarks>
        /// <param name="action">动作 ordinal(原样透传)。</param>
        /// <param name="holdTicks">本次动作持时(原样透传)。</param>
        /// <param name="edges">本次动作 press 沿数(原样透传)。</param>
        /// <param name="edgeTicks">本次动作 press 沿时刻(原样透传;须非 null —— 载荷契约要求)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="edgeTicks"/> 为 null。</exception>
        public static EmergencyReading CreateKeyboardFallbackReading(
            int action, int holdTicks, int edges, int[] edgeTicks)
        {
            if (edgeTicks == null)
                throw new ArgumentNullException(nameof(edgeTicks),
                    "键鼠回退读数须原样透传边沿序列(载荷契约 Edges=0 时也要求空数组非 null)");
            return new EmergencyReading(
                action: action,
                holdTicks: holdTicks,
                edges: edges,
                edgeTicks: edgeTicks,
                magnitude: MAG_MAX);
        }

        /// <summary>
        /// 键鼠回退 cause = 降级（AC-10-21）。
        /// </summary>
        public static int GetKeyboardFallbackCause()
        {
            return 1; // 降级
        }

        /// <summary>
        /// 音频触发源白名单（ADR-18 §六: 无提示音铁律）。
        /// 触发源单值 = 行为反馈白名单（不因 JudgeResult 而异）
        /// </summary>
        public static string[] GetAudioTriggerSources()
        {
            return new[] { "action_feedback" };
        }

        /// <summary>
        /// 音频 cue（音色/素材/强度）不因 JudgeResult 而异（AC-10-17）。
        /// </summary>
        public static string GetAudioCueForResult(JudgeResult result)
        {
            // 所有 JudgeResult 共用同一音频 cue（差异只在动作本身的物理声）
            return "action_feedback";
        }

        /// <summary>
        /// 预表现本地 Judge（不写流、不发成长）。
        /// 复用 story 003 的 JudgeEvaluator，仅驱动预表现呈现。
        /// </summary>
        public static JudgeResult PrePresentationJudge(EmergencyReading agg, EmergencyActionRow action, JudgeContext ctx)
        {
            // 本地 Judge = 纯函数，不写流、不发成长
            // 主机结果到达后以主机为准（可见差异仅一个 tick 的 L_eval）
            return JudgeEvaluator.Judge(agg, action, ctx);
        }
    }
}
