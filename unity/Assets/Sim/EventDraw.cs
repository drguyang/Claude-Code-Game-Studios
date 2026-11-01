// 52 抽池命中 —— **确定性纯函数**(GDD random-events.md F1「抽池」· ADR-007 §三 骰子可重构)
//
// ▍为什么单独成件
//   「档配额 → 掷骰 → 命中 key」这一跳此前**全库不存在**(`EventPool` 只有 schema 与拒收表,
//   `P0HostEventAuthority.Roll` 是抛形占位)。抽池命中必须是可重放的纯函数:
//   输入 = `(WorldSeed, win, tier, ordinal)`,这四项**全部**记在落流的 `EventRolled` 载荷里
//   (`EventRolledPayload`:win / tier / ordinal / chosenKey)⇒ 读档后从流重建掷骰**无需别的来源**。
//
// ▍为什么不用 `UnityEngine.Random` / BCL 默认哈希
//   AC-52-06 / AC-52-07:全案唯一整数哈希源 = `SplitMix64`(ADR-005)。本件只调它。
//
// ⚠️ **本件不做**的事(各有归属,不在此处装样子):
//   · **权重** —— F1 的 `W_i` 调制(GDD F1 第 2 步)未接入:今日候选集 = 已按档过滤的**等权**列表。
//     接入权重须先有 52 的池内容(37 内容件),届时改的是**本件的选择规则**,不是签名。
//   · **历史标记乘子**(AC-52-17)· **密度预算**(GDD 规则十)—— 分别归 52 的后续轮。
//   · 池为空时的兜底 —— **刻意抛**:空池掷骰等于「造一个不存在的 key」。调用方须**先**判空
//     (fail-safe 在 `RandomEventSpawnChain`,那里返回 `NoPool` 而不是抛)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>抽池命中(确定性纯函数;输入三元组可从 `EventRolled` 重建)。</summary>
    public static class EventDraw
    {
        /// <summary>
        /// 从候选集里按 `(WorldSeed, win, tier, ordinal)` 取一个 key。
        /// </summary>
        /// <param name="keys">该档候选 key 集(**有序**;序即数据序,不得依赖字典序以外的不稳定序)。</param>
        /// <param name="worldSeed">世界种子(存档头,ADR-007 §二)。</param>
        /// <param name="win">窗口起点 tick。</param>
        /// <param name="tier">档 id。</param>
        /// <param name="ordinal">本窗口本档第 n 次。</param>
        /// <exception cref="ArgumentNullException"><paramref name="keys"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="keys"/> 为空(空池掷骰 = 造不存在的 key)。</exception>
        public static int Pick(IReadOnlyList<int> keys, ulong worldSeed, long win, int tier, int ordinal)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys.Count == 0)
                throw new ArgumentException(
                    "抽池为空 —— 调用方须先判空并走 fail-safe(NoPool),不得在空池上掷骰",
                    nameof(keys));

            ulong h = SplitMix64.Hash(unchecked((long)worldSeed), win, tier, ordinal);
            return keys[(int)(h % (ulong)keys.Count)];
        }
    }
}
