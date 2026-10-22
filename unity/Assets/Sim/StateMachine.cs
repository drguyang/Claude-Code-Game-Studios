// ADR-005 / GDD disease-simulation.md §F4 —— 九态阈值机与死亡判定。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 事件进流，状态机是事件的纯函数
//   GDD disease-simulation.md §F4 —— 九态状态机与阈值
//   GDD disease-simulation.md 规则三 —— 铁律一「死因唯一」/ 铁律二「伪治疗不续命」
//
// 核心机制:
//   - 三阈值 CRITICAL < COMA < DEATH 且 DEATH < 1
//   - 死亡判定按严重度降序先判
//   - 照护杠杆适用集 = {伤寒/痢疾/心衰} 3 种
//   - 状态迁移 ⇒ 同 tick 落 threshold_transition 事件

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 伤情状态（九态）。
    /// </summary>
    public enum InjuryState
    {
        Healthy = 0,
        Mild = 1,
        Moderate = 2,
        Severe = 3,
        Critical = 4,
        Coma = 5,
        Recovered = 6,
        Chronic = 7,
        Deceased = 8
    }

    /// <summary>
    /// 状态迁移事件。
    /// </summary>
    public readonly struct ThresholdTransition
    {
        public readonly PatientId Patient;
        public readonly InjuryState OldState;
        public readonly InjuryState NewState;

        public ThresholdTransition(PatientId patient, InjuryState oldState, InjuryState newState)
        {
            Patient = patient;
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// 九态阈值机 —— F4 状态机。
    /// </summary>
    public static class StateMachine
    {
        // AC-17: 三阈值严格单调
        //
        // ⚠️⚠️ **以下三个值是实现期占位值,不是裁定值**(2026-10-22 补注,承 PRE-S18-B)。
        //   权威:`design/gdd/disease-simulation.md` §五 三行 default 全为 `*待定*`;
        //   `design/registry/entities.yaml` 的 `CRITICAL_THRESHOLD` / `COMA_THRESHOLD` /
        //   `DEATH_THRESHOLD` 三块 value 亦写「待定」并把本处值标注为「现网实现值,非裁定值」。
        //   数值归用户数值轮 —— **改它们须先有裁定记录**,不得由实现侧自行调。
        //
        //   量纲:Q16.16 raw(`9000 / 65536 ≈ 0.1373`),与 GDD「三阈值 ∈ (0,1]」一致。
        //   约束:`CRITICAL < COMA < DEATH` 且 `DEATH < 1`(校验 9 —— 取 1 会让
        //   `position = 1 − ulp < 1`,死亡永不可达)。
        //
        //   ⚠️ 连带:`DEATH_THRESHOLD` 同时是注册表写入期两条下界的量纲来源
        //   (校验 4b / 14:`plateau ∧ lethal` 与急性保持型须 `A_peak/SCALE ≥ DEATH_THRESHOLD`)。
        //   本值一动,伤情 / 病种数据行的 `A_peak` / `Scale` 须同批复核。
        public const int CRITICAL_THRESHOLD = 3000;
        public const int COMA_THRESHOLD = 6000;
        public const int DEATH_THRESHOLD = 9000;

        // AC-30: 照护杠杆适用集
        public static readonly HashSet<string> CareApplicableDiseases = new HashSet<string>
        {
            "typhoid",    // 伤寒
            "dysentery",  // 痢疾
            "heart_failure" // 心衰
        };

        /// <summary>
        /// 计算状态迁移。
        /// </summary>
        public static InjuryState ComputeTransition(
            InjuryState currentState,
            Fix positionAgg,
            long lastInterventionTick,
            long currentTick,
            string diseaseKey)
        {
            // AC-29: 判定顺序锁 —— 严重度降序
            // DEATH → COMA → CRITICAL → ...

            // 死亡判定
            if (positionAgg.Raw >= DEATH_THRESHOLD)
            {
                return InjuryState.Deceased;
            }

            // 昏迷判定
            if (positionAgg.Raw >= COMA_THRESHOLD)
            {
                return InjuryState.Coma;
            }

            // 危殆判定
            if (positionAgg.Raw >= CRITICAL_THRESHOLD)
            {
                return InjuryState.Critical;
            }

            // 重度判定
            if (positionAgg.Raw >= 2000)
            {
                return InjuryState.Severe;
            }

            // 中度判定
            if (positionAgg.Raw >= 1000)
            {
                return InjuryState.Moderate;
            }

            // 轻度判定
            if (positionAgg.Raw > 0)
            {
                return InjuryState.Mild;
            }

            // 健康
            return InjuryState.Healthy;
        }

        /// <summary>
        /// 判断是否可照护。
        /// </summary>
        public static bool IsCareApplicable(string diseaseKey)
        {
            return CareApplicableDiseases.Contains(diseaseKey);
        }

        /// <summary>
        /// 判断是否已死亡。
        /// </summary>
        public static bool IsDeceased(InjuryState state)
        {
            return state == InjuryState.Deceased;
        }

        /// <summary>
        /// 判断是否已痊愈。
        /// </summary>
        public static bool IsRecovered(InjuryState state)
        {
            return state == InjuryState.Recovered;
        }
    }
}
