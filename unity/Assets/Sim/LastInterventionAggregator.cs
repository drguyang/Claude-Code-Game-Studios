// ADR-005 / GDD disease-simulation.md §F4 —— `last_intervention` 聚合器(2026-10-22 立)。
//
// 权威来源:
//   GDD disease-simulation.md:1126-1133 —— `last_intervention = last of:` 三通道定义
//   GDD disease-simulation.md 规则六 · 集群 2 —— 照护支的**派生**定义
//     `last_intervention(照护) = max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP)`
//   GDD disease-simulation.md:1131-1140 —— 哨兵规则:`若两者均不存在 = onset_tick`,
//     「哨兵必须显式。缺了它…`(t − last_intervention)` 无定义 → 死亡判定整条短路」
//   GDD disease-simulation.md:1135-1140 —— 照护杠杆适用集(3 病种);破伤风不在其中
//   ADR-016 §一 —— 三源不变量:本聚合器的全部输入 ∈ {事件流, 版本化烘焙数据, 二者纯函数}
//
// ── 为什么单独一个类(而不是塞进 F4OutcomeEvaluator)──────────────────────────
//   `last_intervention` 有**自己的三条通道 + 一个哨兵 + 一条派生式**,是可以独立
//   证伪的规格(例如「从没被治过 ⇒ 返回 onset_tick」不需要 F4 的任何其他输入)。
//   塞进求值体 ⇒ 「窗口算错」与「阈值判错」会在同一个红里,分不开。
//   这与 `EntityDeathGates` 从 `ComputeTransition` 拆出来的理由同源(纯度)。
//
// ── 三条通道(取 max)────────────────────────────────────────────────────────
//   ① **有效对因处置** —— 处置 where `polarity = causal` ∧ `drug_potency 与 Base 异号`
//      ∧ `处置 ∈ treatable_by(d)`。⚠️ 三条件全部由**调用方**在构造输入时筛好
//      (见 `TreatmentChannelInput` 的注释:本类不复制 `ProgressionEvaluator` 的筛选逻辑)。
//   ② **持续照护的最后一刻** —— 派生量,见下。
//   ③ 两者均不存在 ⇒ `onset_tick` 哨兵。
//
// ── 照护支为什么是派生量(不是「最后一条止」直读)────────────────────────────
//   字面「最后一条护理动作止」在「起」后玩家走开(无「止」事件)时语义为空。
//   GDD 集群 2 的修正:`max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP)`。
//   语义:「起」之后 `CARE_GAP` 内**未出现新动作** ⇒ 中止时刻 = 最后动作 tick + CARE_GAP;
//   「止」存在则从止起计时。
//   ⇒ 本类收的是**已归一化的护理动作摘要**(`CareChannelInput`),不是原始事件流
//   (Sim 内无解码路径,承 b2 门 —— 与 `ProgressionEvaluator` 的剂量重载同法)。
//
// ── 照护杠杆适用集(2026-10-22 实现的边界)──────────────────────────────────
//   GDD `:1135-1140`:照护杠杆适用 = {伤寒 / 痢疾 / 心衰}(四轮 K8 收窄);
//   **破伤风不在其中** —— 破伤风护理**不刷新**死亡窗口。
//   ⇒ 本类**不**自行判断适用集(那需要病种身份,属 F4 的输入面),
//   而是由调用方只在适用病种上传入 `CareChannelInput`。本类对「传了就采信」负责,
//   对「该不该传」不负责 —— 后者是 `F4OutcomeEvaluator` 的职责边界。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// F4 的 `last_intervention` 聚合器 —— 三条通道取 max + `onset_tick` 哨兵。
    ///
    /// <para><b>为什么是纯函数</b>:承 ADR-016 §一 三源不变量 —— 输出只依赖入参,
    /// 不读墙钟 / 不读表现态 / 不读全局可变状态 ⇒ 可重放、可逐位对拍。</para>
    /// </summary>
    public static class LastInterventionAggregator
    {
        /// <summary>
        /// 有效对因处置通道的**已筛选**输入。
        ///
        /// <para><b>⚠️ 筛选责任在调用方</b>(2026-10-22 实现的刻意边界):
        /// GDD `:1126-1128` 要求「处置 where `polarity = causal` ∧ `drug_potency 与 Base 异号`
        /// ∧ `处置 ∈ treatable_by(d)`」。这三条**与 F1 的处置和式是同一组判据**
        /// (见 <see cref="ProgressionEvaluator"/> 的 `ComputeDrugContribution`)。
        /// 若本类再筛一遍 ⇒ 同一判据两份实现,改一处不改另一处即静默分叉。
        /// ⇒ 本类收**已通过筛选的处置 tick 集合**,只做 `max`。</para>
        /// </summary>
        public readonly struct TreatmentChannelInput
        {
            /// <summary>已通过「causal ∧ 异号 ∧ ∈ treatable_by」三条件的处置 tick(升序不必,本类取 max)。</summary>
            public readonly long[] EffectiveCausalTicks;

            public TreatmentChannelInput(long[] effectiveCausalTicks)
            {
                EffectiveCausalTicks = effectiveCausalTicks;
            }

            /// <summary>无有效对因处置(常态 —— 多数病种无对因手柄)。</summary>
            public static TreatmentChannelInput None => new TreatmentChannelInput(Array.Empty<long>());
        }

        /// <summary>
        /// 照护通道的**已归一化**输入(两条时刻)。
        ///
        /// <para>调用方须从 `CareApplied` 事件流算出这两个量;本类不碰事件流
        /// (Sim 内无解码路径,承 b2 门)。</para>
        /// </summary>
        public readonly struct CareChannelInput
        {
            /// <summary>最后一条护理动作**止**事件的 tick;无「止」事件 = <c>null</c>。</summary>
            public readonly long? LastCareStopTick;

            /// <summary>最后一条护理动作事件(起或止)的 tick;无任何护理动作 = <c>null</c>。</summary>
            public readonly long? LastCareActionTick;

            public CareChannelInput(long? lastCareStopTick, long? lastCareActionTick)
            {
                LastCareStopTick = lastCareStopTick;
                LastCareActionTick = lastCareActionTick;
            }

            /// <summary>无任何护理动作。</summary>
            public static CareChannelInput None => new CareChannelInput(null, null);
        }

        /// <summary>
        /// 求 `last_intervention` —— GDD `:1126-1133` 的三通道 max,空则 `onset_tick`。
        /// </summary>
        /// <param name="onsetTick">病程起点(F4 的哨兵值)。</param>
        /// <param name="treatment">有效对因处置通道(已筛选;可 <c>default</c> = 无)。</param>
        /// <param name="care">照护通道(已归一化;可 <c>default</c> = 无)。</param>
        /// <returns>
        /// 三条通道的最大 tick;**三条全空 ⇒ 返回 <paramref name="onsetTick"/>**。
        /// </returns>
        /// <remarks>
        /// <para><b>为什么哨兵必须是返回值而不是 <c>null</c></b>:GDD 明写「哨兵必须显式。
        /// 缺了它…`(t − last_intervention)` 无定义 → 死亡判定整条短路 —— 而『从没治过』
        /// 恰恰是 F4 最需要覆盖的情形」。返回 `long` 使「空」在类型上**不可能**漏判。</para>
        ///
        /// <para><b>照护支的派生式</b>:
        /// <c>max(LastCareStopTick, LastCareActionTick + CARE_GAP)</c> ——
        /// 两项各自可为空,只在存在的项上取 max。</para>
        ///
        /// <para><b>⚠️ 该式使「止」项恒冗余(2026-10-22 第五轮发现,GDD 内部冲突,待裁)</b>:
        /// 「止」本身就是一条护理动作事件 ⇒ <c>LastCareActionTick ≥ LastCareStopTick</c>
        /// ⇒ <c>LastCareActionTick + CARE_GAP &gt; LastCareStopTick</c> **恒成立**
        /// (CARE_GAP &gt; 0)⇒ 第二项永远覆盖第一项,<c>LastCareStopTick</c> 对
        /// <c>last_intervention</c> **零影响**。
        /// 而 GDD <c>:226</c> 的散文写「『止』存在则**从止起计时**」—— **公式做不到**。
        /// 两者不可能同时为真。本类**按公式实现**(公式是该段的正式定义),并把冲突登记为待裁;
        /// 判据见 <c>test_careStop_isRedundantUnderDerivedFormula</c>。
        /// ⚠️ 本类仍**收**该字段(不删参) —— 删掉会让「按散文修」变成签名变更;
        /// 保留使裁定任一侧都只需改这一段逻辑。</para>
        /// </remarks>
        public static long Evaluate(
            long onsetTick,
            TreatmentChannelInput treatment,
            CareChannelInput care)
        {
            long result = onsetTick;
            bool any = false;

            // ① 有效对因处置 —— 取最大 tick。
            long[] ticks = treatment.EffectiveCausalTicks;
            if (ticks != null)
            {
                for (int i = 0; i < ticks.Length; i++)
                {
                    if (!any || ticks[i] > result) { result = ticks[i]; any = true; }
                }
            }

            // ② 照护支 = max(最后止, 最后动作 + CARE_GAP) —— 两项各自可空。
            if (care.LastCareStopTick.HasValue)
            {
                long stop = care.LastCareStopTick.Value;
                if (!any || stop > result) { result = stop; any = true; }
            }
            if (care.LastCareActionTick.HasValue)
            {
                // ⚠️ 加法不溢出:两值均为 tick(20 Hz 量纲),实践中远小于 long 上界。
                long derived = care.LastCareActionTick.Value + StateMachine.CARE_GAP;
                if (!any || derived > result) { result = derived; any = true; }
            }

            // ③ 全空 ⇒ onset_tick 哨兵(显式,不靠 result 的初值巧合)。
            return any ? result : onsetTick;
        }
    }
}
