// ADR-005 / ADR-007 / ADR-009 —— F2 密度预算与 DeferredThreatSlot。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 确定性模拟
//   ADR-007 §三 —— 掷骰输入必须可从事件流重构(**承重条款**:DeferredThreatSlot = 状态,进流;
//     留在可变对象里 ⇒ 主机迁移后静默丢失 / 重复补发,AC-52-46 直接守这条)
//   ADR-009 §六 —— 世界流有界性
//   GDD random-events.md F2 —— 密度预算 · DeferredThreatSlot 唯一例外
//   GDD random-events.md 规则六之三(:284-305)—— 入槽 / 当日内离开补发 / 跨日作废三语义
//   entities.yaml ThreatDeferred / ThreatDeferralCleared constraint ——
//     「槽集状态由本支 + ThreatDeferralCleared 的全序流重构(**派生,不另存**)」
//   ADR-031 §② —— 载荷解码抽象点(本折叠器 = Sim 侧合法消费点:解码经 IPayloadDecoder,
//     实现住 Sim.Codec,经构造/形参注入 —— Sim 引用集不变)
//   S19-4 / TD-randomevents-01 —— 本文件当前形态的兑现轮(2026-10-25)
//
// 核心机制:
//   - EventBudgetPerDay = clamp(BASE + ΣMod, 0, BUDGET_MAX)
//   - 超限弃置:不写流(无队列;规则六之三「医馆内压制」是唯一例外)
//   - DeferredThreatSlot:威胁档 ∧ 在医馆 ∧ 同日 ⇒ 入槽(**经 ThreatDeferred 事件入流**,
//     写者 = DeferredThreatSlotWriter;52 不直接改内存槽表)
//   - 补发 / 跨日作废(**经 ThreatDeferralCleared 事件入流**,reason ∈ {补发=0, 作废=1}):
//     内存清槽不是直接调用,而是折叠该事件的结果。⚠️ 折叠器对两 reason **不分支**
//     (都清槽)—— 「补发 vs 作废」是 51 的回放读数(ADR-019 §一),不影响槽集形状;
//     reason 值域校验是写入期义务(写者侧),折叠期不重复。
//   - 冷却去重:同 key 在 COOLDOWN_TICKS 内不再被抽
//
// ⚠️ **DeferSlots 的唯一合法写入路径 = FoldDeferEvent(从流折叠)**。
//   直接改内存的 AddToDefer / ClearDefer 已删(S19-4 判据①「不保留第二真源」)。
//   同理 ResetForNewDay 不再碰槽:跨日作废 = 52 生产侧对每个挂起槽发
//   ThreatDeferralCleared(reason=作废) → 折叠自动清(AC-52-48 的「清空」由流承载,
//   不由本方法承载 —— 本方法只管预算两数)。
//
// ⚠️ 折叠期的三类流损坏 = fail-loud 抛(不静默忽略 —— 静默 = 迁移后槽集漂移,更糟):
//   ① slotIndex 越界 [0, deferMax) ② 同槽二次挂起(一占一释配对,entities.yaml constraint)
//   ③ 清未挂起的槽(同上)。写者侧已做同域校验 ⇒ 折叠再遇 = 流损坏 / deferMax 配置漂移。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 预算参数（从烘焙数据装载）。
    /// </summary>
    public sealed class BudgetParams
    {
        public int BASE;
        public int BUDGET_MAX;
        public int DEFER_MAX;
        public int COOLDOWN_TICKS;

        public void Validate()
        {
            if (BASE < 0)
                throw new ArgumentException("BASE 必须 >= 0", nameof(BASE));
            if (BUDGET_MAX <= 0)
                throw new ArgumentException("BUDGET_MAX 必须 > 0", nameof(BUDGET_MAX));
            if (DEFER_MAX <= 0)
                throw new ArgumentException("DEFER_MAX 必须 > 0", nameof(DEFER_MAX));
            if (COOLDOWN_TICKS <= 0)
                throw new ArgumentException("COOLDOWN_TICKS 必须 > 0", nameof(COOLDOWN_TICKS));
        }
    }

    /// <summary>
    /// 预算状态（派生态，从流重构 —— ADR-007 §三）。
    /// </summary>
    /// <remarks>
    /// <see cref="DeferSlots"/> = 挂起威胁槽(slotIndex → eventKey);
    /// **唯一写入路径** = <see cref="EventBudget.FoldDeferEvent"/>(从流折叠)。
    /// 不设 AddToDefer / ClearDefer 之类的直改方法 —— 那是第二真源
    /// (TD-randomevents-01 / ADR-007 §三 承重条款)。
    /// </remarks>
    public sealed class BudgetState
    {
        public int TodayBudget;
        public int TodayUsed;

        /// <summary>挂起威胁槽(slotIndex → eventKey;规则六之三,当日有效、有界 DEFER_MAX)。</summary>
        /// <remarks>
        /// ⚠️ **2026-10-25 W1 修正(代码评审必修)**:原为 `public readonly Dictionary<int,int>` ——
        /// `readonly` 只禁重绑,`state.DeferSlots[k] = v` / `.Remove(k)` / `.Clear()` 全合法
        /// ⇒ 「唯一写入路径 = <see cref="EventBudget.FoldDeferEvent"/>」在**机械面上不成立**。
        /// 现收为 `private` + 只读视图:外部**物理上无法**直改,第二真源由「纪律」升为「编译期不可表达」。
        /// </remarks>
        private readonly Dictionary<int, int> _deferSlots = new Dictionary<int, int>();

        /// <summary>挂起威胁槽的**只读**视图(slotIndex → eventKey)。写入唯一路径 = <see cref="EventBudget.FoldDeferEvent"/>。</summary>
        public IReadOnlyDictionary<int, int> DeferSlots => _deferSlots;

        /// <summary>唯一写入面 —— 仅供 <see cref="EventBudget.FoldDeferEvent"/> / <see cref="EventBudget.ResetForNewDay"/> 使用。</summary>
        internal Dictionary<int, int> DeferSlotsMutable => _deferSlots;

        public readonly Dictionary<int, long> CooldownTable = new Dictionary<int, long>();

        public bool IsBudgetExceeded => TodayUsed >= TodayBudget;

        public bool TryUseBudget()
        {
            if (IsBudgetExceeded) return false;
            TodayUsed++;
            return true;
        }

        public bool IsInCooldown(int eventKey, long currentTick, int cooldownTicks)
        {
            if (CooldownTable.TryGetValue(eventKey, out long lastTick))
            {
                return currentTick - lastTick < cooldownTicks;
            }
            return false;
        }

        public void MarkCooldown(int eventKey, long currentTick)
        {
            CooldownTable[eventKey] = currentTick;
        }
    }

    /// <summary>
    /// F2 密度预算与 DeferredThreatSlot。
    /// </summary>
    public static class EventBudget
    {
        /// <summary>
        /// 计算当日预算。
        /// </summary>
        public static int ComputeDailyBudget(int baseValue, int[] mods, int budgetMax)
        {
            int sum = baseValue;
            if (mods != null)
            {
                foreach (var m in mods) sum += m;
            }
            return Math.Clamp(sum, 0, budgetMax);
        }

        /// <summary>
        /// 判断是否应入槽（三条件门;入槽动作本身经 DeferredThreatSlotWriter 写流）。
        /// </summary>
        public static bool ShouldDefer(EventTier tier, bool isInClinic, bool isSameDay)
        {
            return tier == EventTier.Threat && isInClinic && isSameDay;
        }

        /// <summary>
        /// 执行预算检查。
        /// </summary>
        public static bool TryConsumeBudget(BudgetState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.TryUseBudget();
        }

        /// <summary>
        /// 跨日重置(只管预算两数)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **不清槽**(S19-4 起):跨日作废 = 52 生产侧对每个挂起槽发
        /// <c>ThreatDeferralCleared(reason=作废)</c> → <see cref="FoldDeferEvent"/> 自动清
        /// (AC-52-48 的「DeferredThreatSlot 清空(不结转)」由流承载;内存直清 = 第二真源)。
        /// </remarks>
        public static void ResetForNewDay(BudgetState state, int dailyBudget)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.TodayBudget = dailyBudget;
            state.TodayUsed = 0;
        }

        /// <summary>
        /// 折叠单条事件到槽集(全序流重构的步进算子;两 Kind 之外的事件**忽略**)。
        /// </summary>
        /// <param name="state">槽集宿主(派生态)。</param>
        /// <param name="e">流内事件(全序 —— 调用方保证按跨流全序键排序后喂入)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 抽象点;实现住 Sim.Codec,注入)。</param>
        /// <param name="deferMax">挂起槽上界(DEFER_MAX;来自烘焙配置)。</param>
        /// <exception cref="InvalidDataException">流损坏:槽越界 / 同槽双占 / 清未挂起槽
        /// (头注「三类 fail-loud」—— 静默 = 迁移后槽集漂移)。</exception>
        public static void FoldDeferEvent(BudgetState state, in SimEvent e, IPayloadDecoder decoder, int deferMax)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (deferMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(deferMax), deferMax,
                    "DEFER_MAX 必须 > 0(GDD :1096 界校验;BudgetParams.Validate 已保证,本参数独立复核)");

            switch (e.Kind)
            {
                case EventKind.ThreatDeferred:
                {
                    var p = decoder.Decode<ThreatDeferredPayload>(e.Kind, in e.Payload);
                    if (p.SlotIndex < 0 || p.SlotIndex >= deferMax)
                        throw new InvalidDataException(
                            $"ThreatDeferred: slotIndex {p.SlotIndex} 越界 [0,{deferMax})(有界性:超界拒收,不静默)");
                    if (p.EventKey < 0)
                        throw new InvalidDataException($"ThreatDeferred: eventKey {p.EventKey} 非法(< 0)");
                    if (state.DeferSlots.ContainsKey(p.SlotIndex))
                        throw new InvalidDataException(
                            $"ThreatDeferred: slot {p.SlotIndex} 已占用(一占一释配对,entities.yaml constraint ⇒ 流损坏)");
                    state.DeferSlotsMutable[p.SlotIndex] = p.EventKey;
                    break;
                }
                case EventKind.ThreatDeferralCleared:
                {
                    var p = decoder.Decode<ThreatDeferralClearedPayload>(e.Kind, in e.Payload);
                    // reason(0=补发 / 1=作废)不参与折叠分支(两因都清槽);值域校验在写者侧。
                    if (!state.DeferSlotsMutable.Remove(p.SlotIndex))
                        throw new InvalidDataException(
                            $"ThreatDeferralCleared: slot {p.SlotIndex} 未挂起(一占一释配对被破坏 ⇒ 流损坏)");
                    break;
                }
                // 其他 Kind:忽略(流里还有抽取记录 / 旗标 / 病史事件 —— 本折叠器只管槽集)。
            }
        }

        /// <summary>
        /// 从全序事件流重构挂起槽集(ADR-007 §三:状态唯一真源 = 流)。
        /// </summary>
        /// <returns>只填 DeferSlots 的 <see cref="BudgetState"/>(预算两数由抽取记录重构,不归本件)。</returns>
        public static BudgetState ReconstructDeferSlots(
            IReadOnlyList<SimEvent> orderedEvents, IPayloadDecoder decoder, int deferMax)
        {
            if (orderedEvents == null) throw new ArgumentNullException(nameof(orderedEvents));
            var state = new BudgetState();
            for (int i = 0; i < orderedEvents.Count; i++)
                FoldDeferEvent(state, orderedEvents[i], decoder, deferMax);
            return state;
        }

        /// <summary>
        /// 验证槽集状态可从流重构(S19-4 判据②的**真判据**)。
        /// </summary>
        /// <returns>true 当且仅当 <paramref name="state"/>.DeferSlots 与
        /// 「<paramref name="orderedEvents"/> 重构的结果」**逐元素相同**(槽位与 eventKey 双比)。</returns>
        /// <remarks>
        /// 原 <c>ValidateReconstructable(BudgetState)</c>(「验证非空」= <c>state != null</c>)
        /// 是假断言 —— 判不了任何重构性质(TD-randomevents-01),已于本批删除并换成本判据。
        /// </remarks>
        public static bool ValidateReconstructable(
            BudgetState state, IReadOnlyList<SimEvent> orderedEvents, IPayloadDecoder decoder, int deferMax)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var rebuilt = ReconstructDeferSlots(orderedEvents, decoder, deferMax);
            if (rebuilt.DeferSlots.Count != state.DeferSlots.Count)
                return false;
            foreach (var kv in state.DeferSlots)
            {
                if (!rebuilt.DeferSlots.TryGetValue(kv.Key, out int v) || v != kv.Value)
                    return false;
            }
            return true;
        }
    }
}
