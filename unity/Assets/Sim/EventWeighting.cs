// 52 F1 步骤② 的**权重构成** —— `W_i = W_base_i × TODMult_i × HistoryMult_i`
// 权威来源:GDD random-events.md F1 步骤②(`:839` / `:878`)· 规则九(`:900-907`)
//          · AC-52-45(`:1308`,夜间只抬威胁档)· DC-2(`:718-730`,`Fix` 与整数口径)
//          · ADR-006(`ROUND_HALF_AWAY_FROM_ZERO` 唯一舍入模式)
//
// ▍为什么单独成件
//   权重是**确定性判据的一部分**:GDD `:694` 要求「`W_i` 的构成输入同样必须能从流重构,
//   否则主机迁移会静默改变分布」。把它做成纯函数 ⇒ 输入 = (池表, 档, 是否夜间, 历史),
//   输出 = 候选集;可单测、可证伪,且**不散在装配层**。
//
// ▍P0 的边界(逐条具名,不借绿)
//   · `TODMult` **只有夜间 · 威胁档**一项(AC-52-45)。其余档恒 1。
//   · `HistoryMult ≡ 1` —— 规则九的 `HIST_W` 表在 P0 **不存在**(无历史标记输入源)。
//     本件为此留了 `historyMult` 形参(默认 null),**但没有任何生产调用方会传非 null**;
//     落表时改的是调用点,不是本件。
//   · `KeyGate` **P0 无输入源**(GDD `:237`:声誉 / 救人数 / 生态区全属 P1a)⇒ 恒放行。
//
// ⚠️ **`W_base` 是 int,`TODMult` 是 Fix(分子/分母)** —— 乘积用整数交叉乘后再取整,
//    走 `FixParse.RoundHalfAwayFromZero`(禁 `Math.Round` 默认 ties-to-even,ADR-006)。
//    例:`W_base = 100`,`TODMult = 3/2` ⇒ `100 × 3 / 2 = 150`(整除,无舍入)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>F1 步骤② 的权重构成(纯函数)。</summary>
    public static class EventWeighting
    {
        /// <summary>触发方式 = 随机(0)—— 脚本条目(1)**不进抽池**(规则二)。</summary>
        private const int TriggerRandom = 0;

        /// <summary>威胁档 id(0)—— 唯一受夜间乘子影响的档(AC-52-45)。</summary>
        public const int ThreatTier = 0;

        /// <summary>
        /// 某档的**加权候选集**(已按 key 升序;调用方直接交给 `EventDraw.TryPick`)。
        /// </summary>
        /// <param name="pool">池表(条目已按 key 升序)。</param>
        /// <param name="tier">档 id(0..3)。</param>
        /// <param name="isNight">是否夜间(5 的 `TimeBase.IsNight` 求出)。</param>
        /// <param name="historyMult">
        /// 规则九的历史乘子(**P0 恒 null**)。留形参是为了「落表时只改调用点」——
        /// 传 null ⇒ 全部条目 `HistoryMult ≡ 1`。非 null 时按 `causeFlagKey` 查。
        /// </param>
        /// <returns>该档全部**随机**条目的加权候选(无候选 ⇒ 空集,调用方走 `NoPool`)。</returns>
        public static IReadOnlyList<WeightedKey> CandidatesOfTier(
            EventPoolDataSet pool, int tier, bool isNight,
            IReadOnlyDictionary<int, Fix> historyMult = null)
        {
            if (pool.Entries == null) return Array.Empty<WeightedKey>();

            // 夜间乘子只对威胁档生效(AC-52-45 的第二半:其余档 ≡ 1)
            bool applyNight = isNight && tier == ThreatTier;
            long nightNum = pool.NightThreatMultNum;
            long nightDen = pool.NightThreatMultDen > 0 ? pool.NightThreatMultDen : 1L;

            var result = new List<WeightedKey>();
            for (int i = 0; i < pool.Entries.Count; i++)
            {
                EventPoolEntryData e = pool.Entries[i];
                if (e.Tier != tier) continue;
                if (e.TriggerMode != TriggerRandom) continue;   // 脚本条目不进抽池

                long w = e.WBase;

                // ① TODMult(逐条;夜间 · 威胁档)。整数交叉乘 + 唯一舍入模式。
                if (applyNight)
                    w = FixParse.RoundHalfAwayFromZero(w * nightNum, nightDen);

                // ② HistoryMult(逐条;按 cause_flag 查一次表,不连乘 —— 规则九 `:904`)
                if (historyMult != null && e.CauseFlagKey != 0
                    && historyMult.TryGetValue(e.CauseFlagKey, out Fix hist))
                {
                    w = FixParse.RoundHalfAwayFromZero(w * hist.Raw, Fix.OneRaw);
                }

                result.Add(new WeightedKey(e.Key, w));
            }

            // 数据集已按 key 升序 ⇒ 过滤保持序。此处不再排序(排序会掩盖上游的规范化缺陷)。
            return result;
        }
    }
}
