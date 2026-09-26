// 权威来源:production/epics/audio-system/story-002-event-table-schema-gate.md(**编码防线 ①②**)
//   ① 运行期映射**收口唯一函数**(附加音层排程只准经此算偏移,禁各处自乘 INSPIRE_FRACTION)
//   ② EditMode 测映射(event_table_gate_test · test_inspirePhaseMapping_*)
// GDD:design/gdd/audio-system.md §Edge Cases 相位条(:576-584,2026-09-25 F5 重钉)
//   · 相位原点钉死:phase 0 = 吸气起点;周期 = 完整呼吸循环
//   · 窗口 = 吸气段末 20%:trigger_phase ∈ [0.8, 1.0] × **吸气段时长**
//   · 原 [0.6,0.95] × 基础周期 落呼气段(医学反相),已废
// ADR-018 §四 / §六(呼吸两层 · 相位锁定到基础循环)
//
// ⚠️ **编码决定(与 AudioEventTableGates 同一枚)**:事件表 `adventitious_policy.trigger_phase`
//    存的是**相对吸气段的分数**(0 = 吸气起点,1 = 吸气终点),**不是**全周期分数 ——
//    存全周期分数则每次调 `INSPIRE_FRACTION`(I:E 派生,用户旋钮)都要重签全表 = 假红源。
//    绝对相位只在**运行期**由本函数一次算出:offset = trigger_phase × INSPIRE_FRACTION × T。
// ⚠️ 单位:phase / inspireFraction = 无量纲分数;periodSeconds / 返回值 = 秒。
// ⚠️ Story 004(呼吸层排程)是本函数的消费方;**不得**在别处复制该乘法
//    (jitter 的秒幅值同样经 ToAbsoluteJitterSeconds,2026-09-26 审查 REC 补)。
// ⚠️ 2026-09-26 审查 REC:防线由纯 Debug.Assert 改为**显式 LogError + 安全值**
//    —— Assert 受 UNITY_ASSERTIONS 条件编译,release player 剥离后旋钮越界零日志。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>附加音层相位的**唯一**运行期映射(Story 002 编码防线 ①)。
    /// <para>门(AudioEventTableGates)只断言分数窗 ∈ [0.8, 1.0];把分数换算成
    /// 「完整呼吸周期起点起算的绝对偏移」只有本函数一条路。</para>
    /// <example>
    /// // 细湿啰音窗内一点(0.95)在静息 I:E ≈ 1:2(F = 1/3)下落吸气段后段:
    /// double offset = InspirePhaseMapping.ToAbsoluteOffsetSeconds(0.95, 1.0 / 3.0, 3.0); // ≈ 0.95 s
    /// </example></summary>
    public static class InspirePhaseMapping
    {
        /// <summary>把 <c>trigger_phase</c>(相对吸气段的分数)映射为绝对相位偏移(秒,
        /// 原点 = 完整呼吸周期的吸气起点)。
        /// <para>公式:<c>offset = trigger_phase × INSPIRE_FRACTION × T</c>;
        /// 窗内任一点必落 <c>[0.8×F×T, F×T]</c> ⊆ 吸气段(门的窗与本式同源)。</para></summary>
        /// <param name="triggerPhase">相对吸气段的分数(事件表原文;门已断言 ∈ [0.8, 1.0])。</param>
        /// <param name="inspireFraction"><c>INSPIRE_FRACTION</c> = 吸气段占完整周期比例
        /// (I:E 派生,用户旋钮,静息 ≈ 1/3;由调用方注入,本函数不读表)。</param>
        /// <param name="periodSeconds">完整呼吸周期时长 T(秒;调用方注入)。</param>
        /// <returns>绝对相位偏移(秒)。</returns>
        public static double ToAbsoluteOffsetSeconds(
            double triggerPhase, double inspireFraction, double periodSeconds)
        {
            // 2026-09-26 审查 REC:Debug.Assert 受 UNITY_ASSERTIONS 条件编译,release player
            // 整段剥离 —— 旋钮(F / T,门看不见)越界时原实现直接返回越界偏移且零日志。
            // 改为**显式防线**(LogError + 安全值 0:宁可本周期不触发,不落呼气段 = 医学反相)。
            if (!(inspireFraction > 0.0 && inspireFraction < 1.0))
            {
                Debug.LogError("[InspirePhaseMapping] INSPIRE_FRACTION 须 ∈ (0,1)" +
                               "(GDD §Tuning Knobs 安全范围)—— 返回 0(不触发,禁越界偏移)");
                return 0.0;
            }
            if (!(periodSeconds > 0.0))
            {
                Debug.LogError("[InspirePhaseMapping] 完整呼吸周期 T 须 > 0 —— 返回 0");
                return 0.0;
            }
            if (!(triggerPhase >= 0.0 && triggerPhase <= 1.0))
            {
                Debug.LogError("[InspirePhaseMapping] trigger_phase = 相对吸气段分数,须 ∈ [0,1]" +
                               "(门断言窗 [0.8,1.0];越界 = 上游写错编码)—— 返回 0");
                return 0.0;
            }

            double inspireSeconds = inspireFraction * periodSeconds;
            double offset = triggerPhase * inspireSeconds;

            // GDD §Edge Cases 相位条(:581):窗口须 ⊆ 吸气段 —— 绝对偏移不得越过吸气终点。
            // 窗为闭区间(trigger_phase = 1.0 = 吸气段终点合法),故用 ≤。
            if (offset > inspireSeconds)
            {
                Debug.LogError("[InspirePhaseMapping] 绝对偏移越出吸气段(GDD §Edge Cases 相位条)" +
                               "—— 钳回吸气终点");
                offset = inspireSeconds;
            }
            return offset;
        }

        /// <summary>把 <c>jitter</c>(相对吸气段的分数幅值)映射为**绝对秒幅值**(2026-09-26 审查 REC:
        /// GDD :581 的 ±jitter 同样要 ×F×T —— Story 004 排程**只准调本函数**,禁自乘
        /// <c>INSPIRE_FRACTION</c>,违本文件 :15 唯一收口纪律)。
        /// <para>非负入参;越界(负 / NaN / F·T 非法)⇒ LogError + 0,与主函数同防线。</para></summary>
        public static double ToAbsoluteJitterSeconds(
            double jitter, double inspireFraction, double periodSeconds)
        {
            if (!(jitter >= 0.0))
            {
                Debug.LogError("[InspirePhaseMapping] jitter 须 ≥ 0 —— 返回 0");
                return 0.0;
            }
            if (!(inspireFraction > 0.0 && inspireFraction < 1.0))
            {
                Debug.LogError("[InspirePhaseMapping] INSPIRE_FRACTION 须 ∈ (0,1) —— 返回 0");
                return 0.0;
            }
            if (!(periodSeconds > 0.0))
            {
                Debug.LogError("[InspirePhaseMapping] 完整呼吸周期 T 须 > 0 —— 返回 0");
                return 0.0;
            }
            return jitter * inspireFraction * periodSeconds;
        }
    }
}
