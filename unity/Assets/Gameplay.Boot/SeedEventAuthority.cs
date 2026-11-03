// 权威来源:ADR-007 §三(掷骰输入可从事件流重构)· ADR-005(SplitMix64)· GDD F1 步骤②(加权抽取)
//
// 🔁 **2026-11-03(ADR-039 后续轮):候选集从「等权 key 列表」改为「加权候选列表」。**
//    病因:原实现调 `EventDraw.Pick`(均匀抽取),`W_base` **完全没被消费** ——
//    而 GDD `:672` 逐字警告「一个无视权重的实现能通过 AC-52-06」。
//    现改为 `EventDraw.TryPick`(CDF walk),权重由**调用方**按 F1 步骤② 算好
//    (`W_i = W_base × TODMult × HistoryMult`,已取整)再交进来。
//
// ⚠️ **本类不计算权重** —— 它只掷骰。权重是「档内候选」的属性,由装配层按
//    「池表 + 当前昼夜 + 玩家历史」算出。这样切的好处:`Roll` 仍然是
//    `(WorldSeed, win, tier, ordinal) + 候选集` 的纯函数,而候选集的构成可单独测。
//
// ⚠️ `ΣW = 0` ⇒ **返回 `ZeroWeight` 结果而不是抛**(GDD Edge Cases `:1065`:
//    「该档跳过本轮配额,不掷骰;不报错」)。调用方见 `ChosenKey == 0` 即知本档跳过。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>P0 掷骰实现 —— 确定性加权抽取(输入全可从 `EventRolled` 载荷重建)。</summary>
    public sealed class SeedEventAuthority : IEventAuthority, IP0AuthorityPlaceholder
    {
        private readonly Func<int, long, IReadOnlyList<WeightedKey>> _candidatesOfTier;
        private readonly ulong _worldSeed;

        /// <param name="candidatesOfTier">(档, roll tick) → **加权**候选集(已按 key 升序;权重已取整)。
        /// <para>⚠️ 第二个参数是 **roll tick**,不是窗口起点 —— `TODMult` 是昼夜的函数,
        /// 而昼夜在一天之内会翻转(AC-52-45)。</para></param>
        /// <param name="worldSeed">世界种子(存档头,ADR-007 §二)。</param>
        public SeedEventAuthority(Func<int, long, IReadOnlyList<WeightedKey>> candidatesOfTier, ulong worldSeed)
        {
            _candidatesOfTier = candidatesOfTier ?? throw new ArgumentNullException(nameof(candidatesOfTier));
            _worldSeed = worldSeed;
        }

        /// <inheritdoc/>
        public bool IsAuthority => true;

        /// <inheritdoc/>
        public bool IsHost => true;

        /// <summary>
        /// 掷骰 —— `SplitMix64(WorldSeed, win, tier, ordinal)` 上的**加权** CDF walk。
        /// <para>⚠️ 空档候选集 ⇒ **抛**(不返回假 key)。调用方须先判空。
        /// ⚠️ `ΣW = 0` ⇒ 返回 `ChosenKey = 0`(**不抛** —— 该档跳过本轮,不报错)。</para>
        /// </summary>
        public EventRollResult Roll(in RollRequest r)
        {
            IReadOnlyList<WeightedKey> candidates = _candidatesOfTier(r.Tier, r.Tick);
            if (candidates == null || candidates.Count == 0)
                throw new InvalidOperationException(
                    $"档 {r.Tier} 的候选集为空 —— 空池不得掷骰(会造出不存在的 event_key)。" +
                    "调用方须先判空走 NoPool。");

            if (!EventDraw.TryPick(candidates, _worldSeed, r.Win, r.Tier, r.Ordinal, out int key))
                return new EventRollResult(r.Win, r.Tier, r.Ordinal, 0);   // ΣW = 0 ⇒ 本档跳过

            return new EventRollResult(r.Win, r.Tier, r.Ordinal, key);
        }
    }
}
