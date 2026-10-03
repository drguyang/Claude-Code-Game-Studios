// emergency-procedures Story 002 — EmergencyAction 枚举与动作表 schema
//
// 权威来源:
//   OQ-10-6 裁决: 归系统 10（急救动作是系统 10 核心职责）
//   OQ-10-4 裁决: P0 = 2 个急救动作（止血包扎 + 节奏型通气）
//   ADR-014: 两阶段烘焙
//   GDD emergency-procedures.md: 数据契约 10-DC

using System;
using DaYiJingCheng.Sim.Contracts;   // EventKind / StreamId(DC-5 校验用)

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 急救动作枚举（OQ-10-6 裁决: 归系统 10）。
    /// P0 动作清单（OQ-10-4 裁决）:
    ///   0 = 止血包扎（包扎→按压两拍，同一动作序列）
    ///   1 = 节奏型通气（总谱呈现「人工呼吸法」，内部映射 CPR 节奏动作集）
    /// </summary>
    public enum EmergencyAction : int
    {
        HemostasisBandage = 0,  // 止血包扎（包扎→按压）
        RhythmVentilation = 1  // 节奏型通气（CPR 节奏集）
    }

    /// <summary>
    /// 判定结果枚举（result_mul 三档索引）。
    /// </summary>
    public enum JudgeResult : int
    {
        Missed = 0,        // 未命中
        AppliedWeak = 1,   // 弱应用
        Applied = 2        // 完全应用
    }

    /// <summary>
    /// 动作表行（烘焙产物）。
    /// GDD 数据契约 10-DC: 动作表字段 = polarity / base_potency / mag_threshold / min_edges / min_hold_ticks / half_life_ticks / duration_ticks
    /// </summary>
    public sealed class EmergencyActionRow
    {
        public int ActionId;
        public int Polarity;           // 枚举序数
        public long BasePotency;       // Fix raw long
        public int MagThreshold;       // 幅度门阈值（Fix raw long）
        public int MinEdges;           // 最小边数
        public int MinHoldTicks;       // 最小保持 tick
        public int HalfLifeTicks;      // 衰减半衰期（tick）
        public int DurationTicks;      // 持续 tick
        public long[] ResultMul;       // result_mul 三档（Fix raw long，按 JudgeResult 序数索引）
    }

    /// <summary>
    /// 熟练度表行（烘焙产物）。
    /// GDD 数据契约 10-DC: 熟练度表字段 = jitter_relax_mul / mag_cap_mul
    /// </summary>
    public sealed class EmergencySkillRow
    {
        public int Level;              // 主键（QueryLevel 档）
        public int JitterRelaxMul;     // 抖动松弛乘子（Fix raw long）
        public int MagCapMul;          // 幅度上限乘子（Fix raw long）
    }

    /// <summary>
    /// 动作表 schema 校验器（DC-1…DC-6）。
    /// </summary>
    public static class EmergencyActionSchema
    {
        public const int MUL_ONE = 65536; // 1.0 in Q16.16

        /// <summary>DC-1: half_life_ticks >= 1</summary>
        public static bool ValidateHalfLifeTicks(EmergencyActionRow row)
        {
            return row.HalfLifeTicks >= 1;
        }

        /// <summary>DC-2: 1 <= mag_threshold <= MAG_MAX</summary>
        public static bool ValidateMagThreshold(EmergencyActionRow row, int magMax)
        {
            return row.MagThreshold >= 1 && row.MagThreshold <= magMax;
        }

        /// <summary>DC-3: jitter_relax_mul >= MUL_ONE（熟练度表约束）</summary>
        public static bool ValidateJitterRelaxMul(EmergencySkillRow row)
        {
            return row.JitterRelaxMul >= MUL_ONE;
        }

        /// <summary>DC-4: action_id 闭集 = EmergencyAction 枚举全值</summary>
        public static bool ValidateActionId(int actionId)
        {
            return Enum.IsDefined(typeof(EmergencyAction), actionId);
        }

        /// <summary>DC-5: Kind 白名单含三 Kind（引用 entities.yaml 注册表）</summary>
        public static string[] GetRequiredKindWhitelist()
        {
            return new[] { "EmergencyAttempt", "EmergencyTreatmentApplied", "DrugTreatmentApplied" };
        }

        /// <summary>
        /// **DC-5 的真校验**(2026-10-03 补,闭合评审 B4):三 Kind 须**确实在
        /// 9 侧的 Kind 白名单表内** —— 否则 9 构建期拒收 10 的每一笔写入(R-2 的原始症状)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 原实现**只返回字符串数组、无校验体** ⇒ AC-10-07b 的判据**空转**。
        /// <para><b>校验面</b>:9 侧的白名单真源 = kindgen 产物
        /// <c>Sim.StreamRouting</c>(ADR-024 §⑤ —— 由 <c>entities.yaml</c> 生成)。
        /// 本方法逐 Kind 断言其在路由表内(**能被路由 ⇒ 在白名单内**)。</para>
        /// <para>⚠️ <b>依赖方向</b>:本类住 <c>Sim</c>,<c>StreamRouting</c> 亦住 <c>Sim</c>
        /// ⇒ **同装配内调用,零新增依赖边**。</para>
        /// </remarks>
        /// <returns>错误列表(空 = 通过)。拒以空集冒充绿。</returns>
        public static System.Collections.Generic.List<string> ValidateRequiredKindsRoutable()
        {
            var errs = new System.Collections.Generic.List<string>();
            var required = GetRequiredKindWhitelist();
            if (required == null || required.Length == 0)
            {
                errs.Add("[DC-5] 必需 Kind 清单为空 —— 拒以空集冒充绿。");
                return errs;
            }

            int routable = 0;
            foreach (var name in required)
            {
                if (!System.Enum.TryParse<EventKind>(name, out var kind))
                {
                    errs.Add($"[DC-5] 必需 Kind「{name}」在 EventKind 枚举内**不存在** —— " +
                             "9 侧白名单不可能含它(AC-10-07b 会失败)。");
                    continue;
                }

                try
                {
                    // 能被路由 ⇒ 在 kindgen 白名单内(否则 switch 落 default 抛)
                    var stream = StreamRouting.Of(kind);
                    if (stream != StreamId.History)
                        errs.Add($"[DC-5] Kind「{name}」路由到 {stream},而 10 的三个 Kind " +
                                 "须全落**病史流**(ADR-009 Amendment I)。");
                    else
                        routable++;
                }
                catch (System.InvalidOperationException ex)
                {
                    errs.Add($"[DC-5] Kind「{name}」**不可路由** ⇒ 不在 9 的白名单内" +
                             $"(9 构建期会拒收 10 的每一笔写入,R-2 原始症状):{ex.Message}");
                }
            }

            if (routable == 0 && errs.Count == 0)
                errs.Add("[DC-5] 无任何 Kind 通过 —— 拒以空集冒充绿。");

            return errs;
        }

        /// <summary>
        /// DC-6: `result_mul` 恰三档(非 null,`Length == 3`,按 `JudgeResult` 序数索引)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **2026-10-03 补(GDD 数据契约 10-DC 的 DC-6)**。
        /// 缺此校验的后果(实测):漏填 `result_mul` 的 row **能过 DC-1…DC-5 全部烘焙门**,
        /// 直到 `HostEmergencyProcessor.Process` 读 `ResultMul[(int)result]` **NRE** 才暴露
        /// —— `Tests/PlayMode/EmergencyProcedures/host_authority_test.cs` 两例即此形态。
        /// 档数 ≠ 3 ⇒ 索引越界(`Applied = 2`)或静默取错档。
        /// **承载归属**:`result_mul` 是 **per-action 数据表字段**(内联于
        /// `emergency_action.json`,ADR-014 烘焙),**不得**改从代码直读常量。
        /// </remarks>
        public static bool ValidateResultMul(EmergencyActionRow row)
        {
            return row != null && row.ResultMul != null && row.ResultMul.Length == 3;
        }

        /// <summary>result_mul 三档（AC-10-16 已裁机制值，按 JudgeResult 序数索引）</summary>
        public static long[] GetResultMulTiers()
        {
            // 按 JudgeResult 序数索引: Missed=0, AppliedWeak=1, Applied=2
            // GDD F-10.4: Missed=0.25, AppliedWeak=0.5, Applied=1.0
            return new long[] { 16384, 32768, 65536 };
        }

        /// <summary>F-10.2: MAG_CAP(L) 档位表（P0 全档相同，值归用户数值轮）</summary>
        public static int[] GetMagCapTable()
        {
            // P0 效应关闭，允许全档相同
            // 值归用户数值轮，此处为占位
            return new int[] { 65536, 65536, 65536, 65536, 65536 };
        }
    }
}
