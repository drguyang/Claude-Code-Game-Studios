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
