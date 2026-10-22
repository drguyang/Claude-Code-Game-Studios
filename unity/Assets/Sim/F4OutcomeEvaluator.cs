// ADR-005 / GDD disease-simulation.md §F4 —— 九态判定求值体(2026-10-22 立)。
//
// 权威来源:
//   GDD disease-simulation.md:1080-1112 —— F4 判定顺序(严重度降序,**顺序是规格的一部分**)
//     死亡 ← LethalFor ∧ ¬SelfLimited ∧ position_agg ≥ DEATH_THRESHOLD
//            ∧ (t − last_intervention) > TREATMENT_WINDOW
//     昏迷 ← position_agg ≥ COMA_THRESHOLD ∧ 存活        // 🔁 无谓词门(2026-10-21 订正)
//     危殆 ← position_agg ≥ CRITICAL_THRESHOLD
//   GDD disease-simulation.md:1113-1125 —— 痊愈支(四条件合取,含可愈性门与上升期门)
//   GDD disease-simulation.md:1126-1140 —— `last_intervention`(由 LastInterventionAggregator 提供)
//   ADR-016 §二 —— combatant 归零 = 昏迷(由 EntityDeathGates 提供)
//   ADR-016 §一 —— 三源不变量:本求值体全部输入 ∈ {事件流, 烘焙数据, 二者纯函数}
//
// ── 本类补的是什么(三件缺口之一)──────────────────────────────────────────────
//   此前 `EntityDeathGates` 只出三个门、`StateMachine.ComputeTransition` 只做纯阈值分级
//   ⇒ 「越阈 ∧ 过窗口」那两半**没有落点**,且两门从未被任何求值体消费。
//   本类是把三者**合取**成 GDD 判据的唯一落点。
//
// ── 与 ComputeTransition 的分工(刻意分家,不是重复)──────────────────────────
//   | | `StateMachine.ComputeTransition` | 本类 |
//   |---|---|---|
//   | 输入 | 5 个标量(positionAgg + tick + diseaseKey) | 实体类别 + 注册表条目 + 三通道 |
//   | 实体集 | 受伤实体 {敌人, 玩家}(世界流) | 病人侧病史流(F4) |
//   | 职责 | **纯阈值分级**(九态里的 6 档) | **四条件合取 + 两门 + 窗口** |
//   | 死亡判定 | 无条件 `position ≥ DEATH` ⇒ Deceased | **须过两门与窗口**才 Deceased |
//   ⚠️ 二者**不是**「新旧版本」,是**两个不同的判据面**。`ComputeTransition` 保持原样,
//   它的调用点(`InjuryStateChangedWriter`)不受本类影响。
//
// ── 规则九遵守 ────────────────────────────────────────────────────────────────
//   本类**零** `if kind == injury` 式的类别分支。`Kind` 只经 `EntityDeathGates`
//   的数据驱动成员判定参与(与 `EntityDeathGates` 同法)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// F4 九态判定的求值体 —— GDD `:1080-1125` 的唯一落点。
    ///
    /// <para><b>为什么是纯函数</b>:承 ADR-016 §一 —— 全部输入为值类型 / 不可变入参,
    /// 不读墙钟、不读表现态、不读全局可变状态 ⇒ 可重放、可逐位对拍。</para>
    ///
    /// <para><b>⚠️ 本类不计算 `position_agg`</b>:那是 F1/F5 的职责
    /// (见 <see cref="ProgressionEvaluator"/>)。本类收**已求好的** `positionAgg`,
    /// 与 `ComputeTransition` 同法。调用方须按 GDD `:1111-1119` 的 argmax 语义
    /// (并列取 `DiseaseId` 最小者)选定 `d` 后再取它的 position —— 本类不承担该选择。</para>
    /// </summary>
    public static class F4OutcomeEvaluator
    {
        /// <summary>
        /// 求值九态 —— 判定顺序严格按 GDD `:1082`「严重度降序」。
        /// </summary>
        /// <param name="actorClass">实体类别(判死谓词第一入参;`Unspecified` 由
        /// <see cref="EntityDeathGates"/> fail-loud)。</param>
        /// <param name="entry">取得 `position_agg` 的那个病种条目(argmax 结果)。</param>
        /// <param name="positionAgg">病情累计量(Q16.16)。</param>
        /// <param name="onsetTick">病程起点(`last_intervention` 的哨兵值)。</param>
        /// <param name="currentTick">求值时刻 t。</param>
        /// <param name="treatment">有效对因处置通道(已筛选,见聚合器)。</param>
        /// <param name="care">照护通道(已归一化)。</param>
        /// <returns>
        /// <see cref="InjuryState.Deceased"/> / <see cref="InjuryState.Coma"/> /
        /// <see cref="InjuryState.Critical"/> / <see cref="InjuryState.Severe"/> /
        /// <see cref="InjuryState.Moderate"/> / <see cref="InjuryState.Mild"/> /
        /// <see cref="InjuryState.Healthy"/>。
        /// <para><b>⚠️ 本批不判</b> <see cref="InjuryState.Recovered"/> /
        /// <see cref="InjuryState.Chronic"/> —— 见下方「未实现登记」。</para>
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorClass"/> 为
        /// <see cref="ActorClass.Unspecified"/>(由 <see cref="EntityDeathGates"/> 抛出)。</exception>
        /// <remarks>
        /// <para><b>⚠️ 未实现登记(禁借绿,2026-10-22)</b>:</para>
        /// <list type="number">
        /// <item><b>痊愈支</b>(GDD `:1113-1120` 四条件合取:position &lt; RECOVER ∧ 无 relapse ∧
        /// ¬豁免类 ∧ 可愈性谓词 ∧ 非上升期门)与 <b>迁延支</b>(`Chronic`)**本批未实现** ——
        /// 前者需要 `trend_backward_agg`(F5)与 `relapse` 块状态,后者需要趋势分段,
        /// 二者都超出「F4 死亡/昏迷两支」的裁定面。本批**只交付死亡支与昏迷支**。
        /// 求值落在 `[0, CRITICAL)` 时返回 `Severe`/`Moderate`/`Mild`/`Healthy` 的**纯阈值分级**,
        /// 与 `ComputeTransition` 同口径 —— **不得把这段读成「痊愈/迁延已实现」**。</item>
        /// <item><b>`position_agg` 的 argmax 选择</b>(GDD `:1111-1119`)归调用方 —— 见类注释。</item>
        /// <item><b>照护杠杆适用集</b>({伤寒/痢疾/心衰},破伤风不在内)归调用方 ——
        /// 见 <see cref="LastInterventionAggregator"/> 头注。</item>
        /// </list>
        /// </remarks>
        public static InjuryState Evaluate(
            ActorClass actorClass,
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            long onsetTick,
            long currentTick,
            LastInterventionAggregator.TreatmentChannelInput treatment,
            LastInterventionAggregator.CareChannelInput care)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            // ══ 死亡支 —— 四条件合取(GDD :1086-1090),顺序即规格 ══
            //   ① LethalFor(entity, d)      —— 实体门(lethal ∧ ¬combatant)
            //   ② ¬SelfLimited(entity, d)   —— 实体门(关死亡支)
            //   ③ position_agg ≥ DEATH      —— 阈值
            //   ④ (t − last_intervention) > TREATMENT_WINDOW —— 窗口
            //
            // ⚠️ 短路次序刻意:先判两门(纯数据、无副作用),再判阈值,最后才算
            //    last_intervention(需要遍历处置 tick 数组)。这不改变语义 —— 四条件
            //    是**合取**,次序只影响开销。但**两个门必须先于阈值**,理由是
            //    `IsCombatant(Unspecified)` 要 fail-loud:若阈值先短路,一个
            //    Unspecified 实体在低 position 下会静默返回非 Deceased(与门契约相反)。
            if (EntityDeathGates.LethalFor(actorClass, entry)
                && !EntityDeathGates.SelfLimited(actorClass, entry))
            {
                if (positionAgg.Raw >= StateMachine.DEATH_THRESHOLD)
                {
                    long lastIntervention = LastInterventionAggregator.Evaluate(
                        onsetTick, treatment, care);

                    // 窗口条件:严格大于(「后多久算放弃」—— 恰好在窗口边界上仍算「未过」)。
                    if (currentTick - lastIntervention > StateMachine.TREATMENT_WINDOW)
                        return InjuryState.Deceased;
                }
            }

            // ══ 昏迷支 —— 无谓词门(GDD :1091,2026-10-21 两次订正后的定式)══
            //   走到这里 ⇒ 死亡支不成立 ⇒ 人还活着 ⇒ ≥COMA 即昏迷。
            //   ⚠️ **不得**在此加回 LethalFor / SelfLimited —— 那正是 GDD 点名推翻的两种写法:
            //      加 LethalFor ⇒ 敌人/兽永远进不了昏迷(违 ADR-016 §二);
            //      加 SelfLimited ⇒ 玩家被白名单伤情打到 ≥COMA 时跳过昏迷直奔死亡。
            if (positionAgg.Raw >= StateMachine.COMA_THRESHOLD)
                return InjuryState.Coma;

            // ══ 危殆及以下 —— 纯阈值分级(与 ComputeTransition 同口径)══
            if (positionAgg.Raw >= StateMachine.CRITICAL_THRESHOLD)
                return InjuryState.Critical;
            if (positionAgg.Raw >= StateMachine.SEVERE_THRESHOLD)
                return InjuryState.Severe;
            if (positionAgg.Raw >= StateMachine.MODERATE_THRESHOLD)
                return InjuryState.Moderate;
            if (positionAgg.Raw > 0)
                return InjuryState.Mild;
            return InjuryState.Healthy;
        }

        /// <summary>
        /// 便捷重载 —— 无处置、无照护(「从没治过」,F4 最需要覆盖的情形)。
        /// <para>等价于传 <c>TreatmentChannelInput.None</c> + <c>CareChannelInput.None</c>。</para>
        /// </summary>
        public static InjuryState Evaluate(
            ActorClass actorClass,
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            long onsetTick,
            long currentTick)
        {
            return Evaluate(actorClass, entry, positionAgg, onsetTick, currentTick,
                LastInterventionAggregator.TreatmentChannelInput.None,
                LastInterventionAggregator.CareChannelInput.None);
        }

        /// <summary>
        /// GDD 校验 17 的排序约束 `TREATMENT_WINDOW ≥ CARE_GAP` —— 静态可判,测试直接断言。
        /// <para>⚠️ 本属性存在即为了让该排序**有具名落点**;若将来数值轮改动其中一个常量
        /// 使排序失效,`f4_outcome_evaluator_test` 会转红。</para>
        /// </summary>
        public static bool TreatmentWindowNotBelowCareGap
            => StateMachine.TREATMENT_WINDOW >= StateMachine.CARE_GAP;
    }
}
