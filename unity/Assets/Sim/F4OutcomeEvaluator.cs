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
//   | 职责 | **纯阈值分级**(九态里的 6 档) | **四条件合取 + 两门 + 窗口 + 痊愈支** |
//   | 死亡判定 | 无条件 `position ≥ DEATH` ⇒ Deceased | **须过两门与窗口**才 Deceased |
//   ⚠️ 二者**不是**「新旧版本」,是**两个不同的判据面**。`ComputeTransition` 保持原样,
//   它的调用点(`InjuryStateChangedWriter`)不受本类影响。
//
// ── 规则九遵守 ────────────────────────────────────────────────────────────────
//   本类**零** `if kind == injury` 式的类别分支。`Kind` 只经 `EntityDeathGates`
//   的数据驱动成员判定参与(与 `EntityDeathGates` 同法)。
//
// ── 🔁 2026-10-22 第二轮:补痊愈支与迁延支(此前登记为「未实现」)──────────────
//   本轮补齐 GDD `:1113-1120` 的痊愈支(六条件合取)与 `:1125` 的迁延支。
//   **W 与 TREND_EPS 仍待数值轮裁定** —— 本类的做法是把它们作为**显式入参**
//   (`RecoveryWindowInput`),不写死任何数值。数值轮只需填数据,不改代码。
//   见 `RecoveryWindowInput` 的注释与文件末尾「数值轮待填」一节。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 痊愈支 / 迁延支所需的**趋势与稳定窗**输入 —— W 与 TREND_EPS 的载体。
    ///
    /// <para><b>⚠️ 为什么是入参而不是常量</b>:GDD Tuning `:1743-1744` 把
    /// <c>W</c>(trend 差分窗口)与 <c>TREND_EPS</c>(趋势死区)双双标 <c>*待定*</c>,
    /// 归**数值轮**由用户裁定。把待定值写进 <c>Sim</c> 常量就是**代拍**
    /// —— 一旦被当真值引用即是借绿。⇒ 本类收它们作为调用方传入的量,
    /// 结构完整落盘,数值由数据面供给。</para>
    ///
    /// <para><b>⚠️ 传 <c>default</c>(全零)时痊愈支与迁延支<b>不求值</b></b> ——
    /// <see cref="RecoveryWindowInput.IsConfigured"/> 为 <c>false</c>,
    /// <see cref="F4OutcomeEvaluator.Evaluate"/> 退回「纯阈值分级」的旧口径。
    /// 这样「数值未定」在类型上可判,而不是静默用 0 当窗口(0 会让「稳定 0 tick」
    /// 恒成立 ⇒ 痊愈支被无条件放行 ⇒ 静默假绿)。</para>
    /// </summary>
    public readonly struct RecoveryWindowInput
    {
        /// <summary>
        /// <c>W</c> —— trend 差分窗口(tick,GDD `:1743`)。**须为偶数且 &gt; 0**
        /// (取 <c>W/2</c> 做中心差分);本 struct 不校验,由调用方的数据面保证。
        /// </summary>
        public readonly long WindowTicks;

        /// <summary>
        /// <c>TREND_EPS</c> —— 趋势死区(Q16.16 raw,GDD `:1744`)。
        /// <c>|trend| &lt; TREND_EPS</c> ⇒ 判「持平」(归零)。
        /// </summary>
        public readonly long TrendEpsRaw;

        /// <summary>
        /// 低位已稳定持续的 tick 数 —— 「<c>position_agg &lt; RECOVER_THRESHOLD</c> 已连续保持多久」。
        /// <para>⚠️ 这是**状态**,不是常量:调用方须从病程档案累计(何时首次跌破痊愈门),
        /// 本 struct 只承载它。归零 = 本 tick 刚跌破。</para>
        /// </summary>
        public readonly long LowPositionHeldTicks;

        public RecoveryWindowInput(long windowTicks, long trendEpsRaw, long lowPositionHeldTicks)
        {
            WindowTicks = windowTicks;
            TrendEpsRaw = trendEpsRaw;
            LowPositionHeldTicks = lowPositionHeldTicks;
        }

        /// <summary>
        /// 是否已配置(W &gt; 0 且 TREND_EPS ≥ 0)。
        /// <para><c>W == 0</c> ⇒ 未配置 ⇒ 痊愈支 / 迁延支不求值(见类注释)。</para>
        /// </summary>
        public bool IsConfigured => WindowTicks > 0 && TrendEpsRaw >= 0;
    }

    /// <summary>
    /// F4 九态判定的求值体 —— GDD `:1080-1125` 的唯一落点。
    ///
    /// <para><b>为什么是纯函数</b>:承 ADR-016 §一 —— 全部输入为值类型 / 不可变入参,
    /// 不读墙钟、不读表现态、不读全局可变状态 ⇒ 可重放、可逐位对拍。</para>
    ///
    /// <para><b>⚠️ 本类不计算 `position_agg`</b>:那是 F5 的职责
    /// (见 <see cref="DiseaseAggregator"/>)。本类收**已聚合好的** <c>position_agg</c>
    /// 与它的 argmax <c>d</c> 的条目 —— 选择归聚合器,不归本类。</para>
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
        /// <param name="trendBackwardAgg">合成后向趋势(F5;痊愈支「非上升期门」与迁延支分段用)。</param>
        /// <param name="onsetTick">病程起点(`last_intervention` 的哨兵值)。</param>
        /// <param name="currentTick">求值时刻 t。</param>
        /// <param name="treatment">有效对因处置通道(已筛选,见聚合器)。</param>
        /// <param name="care">照护通道(已归一化)。</param>
        /// <param name="window">W / TREND_EPS / 低位稳定时长;<c>default</c> ⇒ 痊愈支与迁延支不求值。</param>
        /// <returns>
        /// <see cref="InjuryState.Deceased"/> / <see cref="InjuryState.Coma"/> /
        /// <see cref="InjuryState.Critical"/> / <see cref="InjuryState.Severe"/> /
        /// <see cref="InjuryState.Moderate"/> / <see cref="InjuryState.Mild"/> /
        /// <see cref="InjuryState.Healthy"/> / <see cref="InjuryState.Recovered"/> /
        /// <see cref="InjuryState.Chronic"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorClass"/> 为
        /// <see cref="ActorClass.Unspecified"/>(由 <see cref="EntityDeathGates"/> 抛出)。</exception>
        /// <remarks>
        /// <para><b>判定次序(严重度降序,顺序是规格)</b>:
        /// 死亡 → 昏迷 → 危殆 → <b>痊愈</b> → 迁延 → 轻症分级。</para>
        /// <para><b>⚠️ 痊愈支与迁延支的位置</b>:GDD `:1082-1085` 把「危殆」列在
        /// 「潜伏 / 痊愈」之前,故痊愈只在 <c>position &lt; CRITICAL</c> 之后才可能命中 ——
        /// 这不是新裁决,是严重度降序的直接推论。</para>
        /// <para><b>⚠️ 本轮仍未实现</b>:<b>潜伏</b>态(<c>τ &lt; incubation</c>)——
        /// 它由 <see cref="ProgressionEvaluator"/> 在求值时把 <c>Progress</c> 压成 0 体现
        /// (AC-26),本类看到的已是 0,无法也不应反推潜伏。九态里的「潜伏」由
        /// **调用方**按 <c>τ &lt; incubation</c> 单独判,不归本类。</para>
        /// </remarks>
        public static InjuryState Evaluate(
            ActorClass actorClass,
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            Fix trendBackwardAgg,
            long onsetTick,
            long currentTick,
            LastInterventionAggregator.TreatmentChannelInput treatment,
            LastInterventionAggregator.CareChannelInput care,
            RecoveryWindowInput window)
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

            // ══ 危殆 ══
            if (positionAgg.Raw >= StateMachine.CRITICAL_THRESHOLD)
                return InjuryState.Critical;

            // ══ 痊愈支 —— GDD :1113-1120 六条件合取(2026-10-22 补)══
            //   ① position_agg < RECOVER_THRESHOLD
            //   ② 无 relapse.* 块
            //   ③ ¬豁免类(plateau ∧ ¬lethal)
            //   ④ 可愈性谓词(self_limit 型 ∨ 豁免类下界)  —— 校验 16
            //   ⑤ 非上升期门(trend_backward_agg ≤ 0 或 τ > τ_peak)
            //   ⑥ 低位稳定 ≥ W
            //   ⑥ 与 ⑤ 需要 W / TREND_EPS / 低位持续时长 ⇒ window 未配置时两支都不判。
            if (window.IsConfigured && positionAgg.Raw < StateMachine.RECOVER_THRESHOLD)
            {
                InjuryState? recovered = TryEvaluateRecovered(
                    entry, positionAgg, trendBackwardAgg, onsetTick, currentTick, window);
                if (recovered.HasValue) return recovered.Value;
            }

            // ══ 迁延支 —— GDD :1125「其余 ← 迁延,由 sign(trend_backward_agg) 分段」══
            //   ⚠️ 迁延的判据**只有**「未达任何阈值 ∧ 未达痊愈」+ 趋势非上升。
            //      GDD 原文「由 sign(trend_backward_agg) 分段」—— 分段的是
            //      **迁延内部**的呈现(进展 / 好转),九态里只有「迁延」一个态。
            //      故本支只判「是否迁延」,不产出子状态(九态无对应值)。
            if (window.IsConfigured && positionAgg.Raw < StateMachine.CRITICAL_THRESHOLD)
            {
                // 未达痊愈且趋势不再上升 ⇒ 迁延(慢性带病)。
                // 趋势 > 0(仍在上升)⇒ 落回下面的阈值分级(发病 / 进展中)。
                long trendEps = window.TrendEpsRaw;
                if (trendBackwardAgg.Raw <= trendEps)
                    return InjuryState.Chronic;
            }

            // ══ 轻症分级 —— 纯阈值(与 ComputeTransition 同口径)══
            if (positionAgg.Raw >= StateMachine.SEVERE_THRESHOLD)
                return InjuryState.Severe;
            if (positionAgg.Raw >= StateMachine.MODERATE_THRESHOLD)
                return InjuryState.Moderate;
            if (positionAgg.Raw > 0)
                return InjuryState.Mild;
            return InjuryState.Healthy;
        }

        /// <summary>
        /// 痊愈支的六条件合取 —— GDD `:1113-1120`。
        /// </summary>
        /// <returns>
        /// <see cref="InjuryState.Recovered"/>(六条件全成立);
        /// <c>null</c>(任一不成立 —— 调用方继续走迁延 / 轻症分级)。
        /// </returns>
        /// <remarks>
        /// <para><b>为什么返回 <c>InjuryState?</c> 而不是 <c>bool</c></b>:
        /// 调用点写成 <c>if (IsRecovered(...)) return Recovered;</c> 时,
        /// 「判为痊愈」与「返回哪个状态」是两处可独立改错的表达;
        /// 返回可空状态让二者合一,<c>null</c> 在类型上就表示「不成立」。</para>
        /// </remarks>
        private static InjuryState? TryEvaluateRecovered(
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            Fix trendBackwardAgg,
            long onsetTick,
            long currentTick,
            RecoveryWindowInput window)
        {
            // ① position_agg < RECOVER_THRESHOLD —— 调用方已判,此处复查是防御
            //    (本方法若被单独调用,不得依赖调用方的先行判据)。
            if (positionAgg.Raw >= StateMachine.RECOVER_THRESHOLD) return null;

            // ② 无 relapse.* 块 —— 带复发块的病种**永不入痊愈**(GDD :1119 / R3.3)。
            //    理由:复发型在脉冲间谷底 position 也低,判愈会让「好转 → 复发」
            //    的医学链路消失。
            if (entry.Relapse != null) return null;

            // ③ ¬豁免类 —— 豁免类 = plateau ∧ ¬lethal(GDD :1109)。
            //    豁免类**永不入痊愈**(R3.6):它的空体征语义要求「他还在病着」为真。
            bool plateau = entry.Curve != null && entry.Curve.Plateau;
            bool lethal = entry.Lethality > 0;
            if (plateau && !lethal) return null;

            // ④ 可愈性谓词 —— 校验 16:self_limit 型 ∨ 豁免类下界。
            //    ⚠️ ③ 已把豁免类排除,故此处只剩「self_limit 型」一支。
            //    这条门防的是「噪声 / 负 bonus 把不可愈病种压进痊愈」——
            //    position 低**不等于**病好了。
            bool selfLimit = entry.Curve != null && entry.Curve.SelfLimit;
            if (!selfLimit) return null;

            // ⑤ 非上升期门 —— `trend_backward_agg ≤ 0` 或 `τ > τ_peak`(四轮 K5)。
            //    缺门则「病刚发(τ<incubation)就判愈」:自限型上升段 position 仍低。
            long tau = currentTick - onsetTick;
            long tauPeak = entry.Curve != null ? entry.Curve.TauPeak : 0;
            bool rising = trendBackwardAgg.Raw > window.TrendEpsRaw;
            if (rising && tau <= tauPeak) return null;

            // ⑥ 低位稳定 ≥ W —— 防 ±σ 抖动把终态判成随机(B5)。
            if (window.LowPositionHeldTicks < window.WindowTicks) return null;

            return InjuryState.Recovered;
        }

        /// <summary>
        /// 便捷重载 —— 有处置 / 照护通道,但未配置稳定窗。
        /// <para>等价于传 <c>trendBackwardAgg = 0</c> + <c>default(RecoveryWindowInput)</c>
        /// ⇒ 痊愈支与迁延支**不求值**(W / TREND_EPS 未定,见 <see cref="RecoveryWindowInput"/>)。
        /// 死亡支 / 昏迷支 / 危殆 / 轻症分级**不受影响**。</para>
        /// </summary>
        public static InjuryState Evaluate(
            ActorClass actorClass,
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            long onsetTick,
            long currentTick,
            LastInterventionAggregator.TreatmentChannelInput treatment,
            LastInterventionAggregator.CareChannelInput care)
        {
            return Evaluate(actorClass, entry, positionAgg, Fix.Zero, onsetTick, currentTick,
                treatment, care, default(RecoveryWindowInput));
        }

        /// <summary>
        /// 便捷重载 —— 无处置、无照护、未配置稳定窗(「从没治过」且数值未定)。
        /// <para>等价于传 <c>TreatmentChannelInput.None</c> + <c>CareChannelInput.None</c>
        /// + <c>default(RecoveryWindowInput)</c> ⇒ 痊愈支与迁延支**不求值**。</para>
        /// </summary>
        public static InjuryState Evaluate(
            ActorClass actorClass,
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            long onsetTick,
            long currentTick)
        {
            return Evaluate(actorClass, entry, positionAgg, Fix.Zero, onsetTick, currentTick,
                LastInterventionAggregator.TreatmentChannelInput.None,
                LastInterventionAggregator.CareChannelInput.None,
                default(RecoveryWindowInput));
        }

        /// <summary>
        /// 便捷重载 —— 有趋势与稳定窗,无处置 / 无照护。
        /// </summary>
        public static InjuryState Evaluate(
            ActorClass actorClass,
            DiseaseRegistryEntry entry,
            Fix positionAgg,
            Fix trendBackwardAgg,
            long onsetTick,
            long currentTick,
            RecoveryWindowInput window)
        {
            return Evaluate(actorClass, entry, positionAgg, trendBackwardAgg, onsetTick, currentTick,
                LastInterventionAggregator.TreatmentChannelInput.None,
                LastInterventionAggregator.CareChannelInput.None,
                window);
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
