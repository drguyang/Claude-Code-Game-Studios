// 52 抽池命中 —— **确定性纯函数**(GDD random-events.md F1「抽池」· ADR-007 §三 骰子可重构)
//
// ▍为什么单独成件
//   「档配额 → 掷骰 → 命中 key」这一跳此前**全库不存在**(`EventPool` 只有 schema 与拒收表,
//   `P0HostEventAuthority.Roll` 是抛形占位)。抽池命中必须是可重放的纯函数:
//   输入 = `(WorldSeed, win, tier, ordinal)`,这四项**全部**记在落流的 `EventRolled` 载荷里
//   (`EventRolledPayload`:win / tier / ordinal / chosenKey)⇒ 读档后从流重建掷骰**无需别的来源**。
//
// 🔁 **2026-11-03(ADR-039 后续轮):均匀抽取 → 加权 CDF walk。**
//   本件原先是 `h % keys.Count` —— **完全无视 `W_base`**。GDD F1 步骤② 要求
//   `W_i = W_base_i × TODMult_i × HistoryMult_i`,且 `:672` 逐字警告:
//   > 一个 `argmax_i SplitMix64(t, k_i)` 的实现**能通过 AC-52-06 却完全无视权重**(不满足 F1)。
//   本轮按 `:678-688` 的算法实现:
//       S = SplitMix64(seed);C = Σ W_i;r = S mod C;按 key 升序游走,r < W_i 即命中
//   ⚠️ **游走顺序由 key 升序钉死**(`:687`「否则两台机器用不同迭代序会抽出不同条目」)。
//
// ▍为什么不用 `UnityEngine.Random` / BCL 默认哈希
//   AC-52-06 / AC-52-07:全案唯一整数哈希源 = `SplitMix64`(ADR-005)。本件只调它。
//
// ▍权重口径(DC-2 / D-21-17)
//   `W_base` 是 **int(最小单位个数)**,不是 `Fix`。`TODMult` 是 `Fix(分子/分母)`。
//   故 `W_i` 的计算 = `W_base_i × TODMult_i` 后**取整** —— 走 `FixParse.RoundHalfAwayFromZero`
//   (`ROUND_HALF_AWAY_FROM_ZERO`,ADR-006 唯一舍入模式;禁 `Math.Round` 默认 ties-to-even)。
//
// ⚠️ **本件不做**的事(各有归属,不在此处装样子):
//   · **历史标记乘子**(`HistoryMult_i`,AC-52-17)—— 需要规则九的 `HIST_W` 表与玩家历史标记集,
//     两者 P0 均无输入源 ⇒ 本轮的 `HistoryMult ≡ 1`。**接入点在本件签名内预留**(见 `historyMult`)。
//   · **密度预算**(GDD F2)—— 在 `RandomEventSpawnChain`,不在本件。
//   · 池为空时的兜底 —— **刻意抛**:空池掷骰等于「造一个不存在的 key」。调用方须**先**判空
//     (fail-safe 在 `RandomEventSpawnChain`,那里返回 `NoPool` 而不是抛)。
//   · `ΣW = 0` 的退化 —— **刻意返回 false 而不是抛**(GDD Edge Cases `:1065`:
//     「该档跳过本轮配额,不掷骰;不报错」)。调用方据此走 `ZeroWeight` 零写入。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>抽池命中(确定性纯函数;输入三元组可从 `EventRolled` 重建)。</summary>
    public static class EventDraw
    {
        /// <summary>
        /// 从候选集里按 `(WorldSeed, win, tier, ordinal)` **加权**取一个 key。
        /// </summary>
        /// <param name="candidates">该档候选(**已按 key 升序**;本方法不再排序 —— 序即数据序)。</param>
        /// <param name="worldSeed">世界种子(存档头,ADR-007 §二)。</param>
        /// <param name="win">窗口起点 tick。</param>
        /// <param name="tier">档 id。</param>
        /// <param name="ordinal">本窗口本档第 n 次。</param>
        /// <param name="chosenKey">命中的 key(失败时为 0)。</param>
        /// <returns>
        /// <c>true</c> = 命中;<c>false</c> = `ΣW = 0`(该档全部条目权重归零 ⇒ 跳过本轮,**不掷骰**、
        /// **不报错**,GDD Edge Cases `:1065`)。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="candidates"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="candidates"/> 为空(空池掷骰 = 造不存在的 key)。</exception>
        public static bool TryPick(IReadOnlyList<WeightedKey> candidates, ulong worldSeed,
                                   long win, int tier, int ordinal, out int chosenKey)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0)
                throw new ArgumentException(
                    "抽池为空 —— 调用方须先判空并走 fail-safe(NoPool),不得在空池上掷骰",
                    nameof(candidates));

            // ① ΣW(整数域;权重已由调用方按 F1 步骤② 算好并取整)
            long total = 0L;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Weight < 0)
                    throw new ArgumentException(
                        $"条目 {candidates[i].Key} 的权重 {candidates[i].Weight} 为负 —— " +
                        "W_i 是整数个数,负值无意义(F1 步骤②)", nameof(candidates));
                total += candidates[i].Weight;
            }

            // ② ΣW = 0 ⇒ 该档跳过本轮(不掷骰、不报错;GDD Edge Cases :1065)
            if (total <= 0L)
            {
                chosenKey = 0;
                return false;
            }

            // ③ S = SplitMix64(WorldSeed, win, tier, ordinal);r = S mod C
            //    ⚠️ C 可能非 2 的幂 ⇒ 用 mod 而非位掩码(GDD :686)
            ulong h = SplitMix64.Hash(unchecked((long)worldSeed), win, tier, ordinal);
            long r = (long)(h % (ulong)total);

            // ④ CDF walk —— 按 **key 升序**(调用方保证);r < W_i 即命中
            for (int i = 0; i < candidates.Count; i++)
            {
                long w = candidates[i].Weight;
                if (w == 0L) continue;
                if (r < w)
                {
                    chosenKey = candidates[i].Key;
                    return true;
                }
                r -= w;
            }

            // ⑤ 不可达:r ∈ [0, total) 且 ΣW = total ⇒ 上面必命中。
            //    走到这里说明 candidates 在遍历中被改动过(并发),按损坏处理 —— fail-loud。
            throw new InvalidOperationException(
                $"加权游走未命中(ΣW={total} 但 r 走完仍剩 {r})—— 候选集在遍历期间被改动?");
        }

        /// <summary>
        /// 便捷形:等权候选(权重全 1)⇒ 退化为均匀抽取。
        /// <para>⚠️ **仅供夹具与向后兼容**;生产必须走 <see cref="TryPick(IReadOnlyList{WeightedKey},ulong,long,int,int,out int)"/>
        /// —— 等权就是「无视 W_base」,正是本件要消灭的形态。</para>
        /// </summary>
        public static int Pick(IReadOnlyList<int> keys, ulong worldSeed, long win, int tier, int ordinal)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0)
                throw new ArgumentException(
                    "抽池为空 —— 调用方须先判空并走 fail-safe(NoPool),不得在空池上掷骰",
                    nameof(keys));

            var weighted = new WeightedKey[keys.Count];
            for (int i = 0; i < keys.Count; i++) weighted[i] = new WeightedKey(keys[i], 1);
            if (!TryPick(weighted, worldSeed, win, tier, ordinal, out int key))
                throw new InvalidOperationException("等权候选的 ΣW 不可能为 0 —— 程序错误");
            return key;
        }
    }

    /// <summary>加权候选:一条池条目在本次抽取里的 key 与已算好的 `W_i`。</summary>
    public readonly struct WeightedKey
    {
        /// <summary>池条目 key。</summary>
        public readonly int Key;

        /// <summary>本次抽取的权重(`W_base × TODMult × HistoryMult`,已取整)。</summary>
        public readonly long Weight;

        public WeightedKey(int key, long w)
        {
            Key = key;
            Weight = w;
        }
    }
}
