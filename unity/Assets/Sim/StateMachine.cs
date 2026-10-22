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
        // ✅ **以下三个值已于 2026-10-22 由用户裁定(档 C)**,不再是占位值。
        //   原「实现期占位值」注保留在下方(原文不删)。
        //
        //   裁定记录:GDD `design/gdd/disease-simulation.md` §五(:1676-1679);
        //   `design/registry/entities.yaml` 同名三块 value 同批改为裁定值。
        //   量纲:Q16.16 raw(65536 = 1.0)。
        //     CRITICAL = 19661 raw ≈ 0.3000
        //     COMA     = 29491 raw ≈ 0.4500
        //     DEATH    = 38011 raw ≈ 0.5800
        //   约束:`CRITICAL < COMA < DEATH < 1`(校验 9)—— 三条均满足。
        //
        //   ⚠️ **裁定档 C 的关键取舍**:DEATH 取 0.58(高于原现网 0.1373),
        //   使 R2 五个伤情行产生真实梯度 —— `INJ_LACERATION` 的 `A_peak/Scale = 0.60`
        //   仅比死线高 **0.02**(勉强致死),`INJ_STAB` 的 1.00 高出 0.42(几乎必死)。
        //   ⇒ 伤情的「致命性分档」首次在数值上可观测,而非仅靠 `lethal` 布尔。
        //
        //   ⚠️ 连带(仍有效):`DEATH_THRESHOLD` 同时是注册表写入期两条下界的量纲来源
        //   (校验 4b / 14:`plateau ∧ lethal` 与急性保持型须 `A_peak/SCALE ≥ DEATH_THRESHOLD`)。
        //   本值一动,伤情 / 病种数据行的 `A_peak` / `Scale` 须同批复核
        //   —— 2026-10-22 已复核 13 行(5 伤情 + 8 病种),全部满足。
        //
        //   ── 以下为原文,保留作闭环记录 ──────────────────────────────────
        //   ⚠️⚠️ **以下三个值是实现期占位值,不是裁定值**(2026-10-22 补注,承 PRE-S18-B)。
        //   权威:`design/gdd/disease-simulation.md` §五 三行 default 全为 `*待定*`;
        //   `design/registry/entities.yaml` 的 `CRITICAL_THRESHOLD` / `COMA_THRESHOLD` /
        //   `DEATH_THRESHOLD` 三块 value 亦写「待定」并把本处值标注为「现网实现值,非裁定值」。
        //   数值归用户数值轮 —— **改它们须先有裁定记录**,不得由实现侧自行调。
        //   (↑ 上述状态已于 2026-10-22 结清。)
        public const int CRITICAL_THRESHOLD = 19661;   // 0.30 × 65536
        public const int COMA_THRESHOLD = 29491;       // 0.45 × 65536
        public const int DEATH_THRESHOLD = 38011;      // 0.58 × 65536

        // AC-30: 照护杠杆适用集
        public static readonly HashSet<string> CareApplicableDiseases = new HashSet<string>
        {
            "typhoid",    // 伤寒
            "dysentery",  // 痢疾
            "heart_failure" // 心衰
        };

        /// <summary>
        /// 计算状态迁移。
        ///
        /// <para><b>⚠️ 本方法只做「纯阈值分级」,<u>不</u>承担 F4 的四条件合取(2026-10-22 裁定)</b>。</para>
        ///
        /// <para>GDD <c>:1086-1090</c> 的死亡支是<b>四条件合取</b>:
        /// <c>LethalFor ∧ ¬SelfLimited ∧ position_agg ≥ DEATH_THRESHOLD ∧ (t − last_intervention) &gt; TREATMENT_WINDOW</c>。
        /// 本方法<b>只实现第三个</b>(阈值)。另三条件(<b>两门 + 窗口</b>)的求值体
        /// <b>全库不存在</b> —— 见 <see cref="EntityDeathGates"/> 的 2026-10-22 补注。
        /// 两门在 <see cref="EntityDeathGates"/>(有独立测试),窗口与 <c>last_intervention</c> 无落点。</para>
        ///
        /// <para><b>⚠️ 为什么两门不塞进来(而不是「还没塞」)</b> —— 三条结构性理由,不是排期:</para>
        /// <list type="number">
        /// <item><b>签名不足</b>:两门要 <c>(ActorClass, DiseaseRegistryEntry)</c>,本方法只有一个
        /// <c>string diseaseKey</c> —— 一个都给不出。<c>ActorClass</c> 是判死谓词的<b>第一入参</b>
        /// (敌人/兽 vs 玩家/病人),靠 <c>diseaseKey</c> 永远推不出来。</item>
        /// <item><b>实体集不同</b>:本方法现被 <see cref="InjuryStateChangedWriter"/> 调用,
        /// 那是<b>受伤实体集 {敌人, 玩家}</b>、落世界流;而 F4 死亡支是<b>病人侧</b>病史流判据(9 的 <c>Step</c>)。
        /// 两者合流 ⇒ 只能按实体类别分叉控制流,正是<b>规则九</b>要防的形态。</item>
        /// <item><b>纯度</b>:两门各自已有独立可证伪的判据(<c>entity_death_gates_test</c>)。
        /// 合成一个函数会让「门错」与「窗口错」在同一个红里,分不开。</item>
        /// </list>
        ///
        /// <para><b>⇒ 归属</b>:F4 求值体另立纯函数(裁定名 <c>F4OutcomeEvaluator.Evaluate</c>),
        /// 与 <c>last_intervention</c> 聚合器<b>同批</b>实现 —— 否则 F4 只能拿外部灌进来的数,
        /// 无法从三源重算,违 ADR-016 §一 三源不变量。</para>
        ///
        /// <para><b>⚠️ 三个形参当前<b>函数体一个都不读</b></b>(<c>lastInterventionTick</c> /
        /// <c>currentTick</c> / <c>diseaseKey</c>)—— 这不是笔误,是上面的缺口的<b>直接症状</b>:
        /// 窗口条件与两门都还没实现,它们暂时无用武之地。保留签名是为了让
        /// <see cref="InjuryStateChangedWriter"/> 的调用点不必随 F4 落地而改。
        /// <b>不得</b>因为「读了也没用」就把它们删掉。</para>
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
