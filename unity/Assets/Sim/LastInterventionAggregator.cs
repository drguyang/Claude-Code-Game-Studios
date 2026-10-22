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
//   GDD 集群 2 的修正原写:`max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP)`。
//   🔁 **2026-10-22 第六轮裁定:该 max 式已作废**(它使「止」项恒冗余,与散文冲突)。
//   现口径 = 按「最后一次动作是不是止」分叉:
//     最后动作 = 止 ⇒ 该 tick;否则 ⇒ 该 tick + CARE_GAP。
//   GDD 侧文本尚未同步(待用户批),见 `Evaluate` 的 remarks。
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
        /// <para><b>照护支(2026-10-22 第六轮裁定后)</b>:
        /// <code>
        /// 最后动作是「止」 ⇒ last_care = 最后动作 tick
        /// 否则(最后动作是「起」/ 走开) ⇒ last_care = 最后动作 tick + CARE_GAP
        /// </code></para>
        ///
        /// <para><b>🔁 原文(第五轮)写 <c>max(最后止, 最后动作 + CARE_GAP)</c> —— 已作废(原文不删)</b>:
        /// 该式使「止」项**恒冗余**(「止」本身是护理动作 ⇒ <c>最后动作 ≥ 最后止</c>
        /// ⇒ <c>最后动作 + CARE_GAP &gt; 最后止</c> 恒成立),与 GDD <c>:226</c> 散文
        /// 「『止』存在则从止起计时」正面冲突。</para>
        ///
        /// <para><b>⚠️ 两案都不取,取散文的<em>原意</em>(2026-10-22 第六轮 · 主对话裁定)</b>:
        /// 把两案各自推到反例就看出它们都错 ——</para>
        /// <list type="table">
        /// <item><term>起 1000 → 止 2000(显式停)</term><description>
        /// 公式给 <c>max(2000, 2600) = 2600</c> —— 玩家明明点了止,还要再送 30 秒;散文给 2000 ✓</description></item>
        /// <item><term>起 1000 → 走开(无止)</term><description>两案都给 1600 ✓</description></item>
        /// <item><term>起 1000 → 止 2000 → 起 3000(停后再起)</term><description>
        /// 公式给 <c>max(2000, 3600) = 3600</c> ✓;散文「『止』存在」⇒ 给 2000 ✗
        /// —— 玩家正在护理,倒计时却从 2000 起算</description></item>
        /// </list>
        /// <para>⇒ GDD <c>:226</c> 那段本身就是**两个互斥情形**的分叉(「未出现新动作」vs
        /// 「『止』存在」),<c>max()</c> 把它们压成一条式子,于是第二情形永不可达。
        /// 散文的「『止』存在」在该段语境里指的是**最后一次动作是止**,
        /// 不是「历史上出现过止」。</para>
        ///
        /// <para><b>判据落在已有两个字段上,无需新增</b>:<c>LastCareStopTick ≤ LastCareActionTick</c>
        /// 恒成立(两者都取大,且止必是一条动作)⇒「最后动作是止」⟺ 两值相等。</para>
        ///
        /// <para><b>⚠️ GDD 文本尚未同步</b>:GDD <c>:1127</c> 的 <c>max</c> 公式与 <c>:226</c>
        /// 的散文都须按本裁定改写(公式作废 · 散文补「最后」二字)。
        /// 代码先落,<b>GDD 改动待用户批</b> —— 不在实现轮自行改设计文档。</para>
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

            // ② 照护支(2026-10-22 第六轮裁定):按**最后一次动作是不是「止」**分叉。
            //    · 最后动作 = 止  ⇒ last_care = 该 tick(玩家显式停了,立刻起算)
            //    · 否则(起 / 走开)⇒ last_care = 该 tick + CARE_GAP(宽限)
            //    「最后动作是止」⟺ 两值相等(止必是一条动作,且两者都取大)。
            if (care.LastCareActionTick.HasValue)
            {
                long lastAction = care.LastCareActionTick.Value;
                long derived = care.LastCareStopTick.HasValue
                               && care.LastCareStopTick.Value == lastAction
                    ? lastAction                                      // 最后动作是止 ⇒ 即刻
                    // ⚠️ 加法不溢出:两值均为 tick(20 Hz 量纲),实践中远小于 long 上界。
                    : lastAction + StateMachine.CARE_GAP;             // 起 / 走开 ⇒ 宽限
                if (!any || derived > result) { result = derived; any = true; }
            }
            else if (care.LastCareStopTick.HasValue)
            {
                // 只有止、没有动作 —— 结构上不可能(止必是一条动作事件)。
                // 仍收:调用方若只喂了止,按止本身计入,不静默丢弃。
                long stop = care.LastCareStopTick.Value;
                if (!any || stop > result) { result = stop; any = true; }
            }

            // ③ 全空 ⇒ onset_tick 哨兵(显式,不靠 result 的初值巧合)。
            return any ? result : onsetTick;
        }
    }
}
