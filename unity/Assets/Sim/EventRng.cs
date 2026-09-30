// ADR-007 / ADR-012 —— 确定性掷骰与流登记。
//
// 权威来源:
//   ADR-007 §一 —— 掷骰走 IEventAuthority
//   ADR-012 §二 —— 双级黄金夹具 + 三格常驻矩阵
//   GDD random-events.md DC-1/DC-3 —— RNG 契约 / tick 契约
//
// 核心机制:
//   - SplitMix64 唯一 RNG
//   - EventRollSeed(win, tier, ordinal) 三整数可重算
//   - CDF walk: key 升序遍历
//   - 128 位中间结果 = 手工 hi/lo 两 ulong

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 事件掷骰种子。
    /// </summary>
    public readonly struct EventRollSeed
    {
        public readonly long Win;
        public readonly int Tier;
        public readonly long Ordinal;

        public EventRollSeed(long win, int tier, long ordinal)
        {
            Win = win;
            Tier = tier;
            Ordinal = ordinal;
        }

        /// <summary>
        /// 计算种子哈希。
        /// </summary>
        public ulong ComputeHash(ulong worldSeed)
        {
            // SplitMix64.Hash 只接受 3 个 long 参数
            // 先混合 Win 和 Tier，再与 Ordinal 混合
            ulong mixed = SplitMix64.Hash((long)worldSeed, Win, Tier);
            return SplitMix64.Hash((long)mixed, Ordinal, 0);
        }
    }

    /// <summary>
    /// CDF walk 结果。
    /// </summary>
    public readonly struct CdfWalkResult
    {
        public readonly int ChosenKey;
        public readonly bool Found;

        public CdfWalkResult(int chosenKey, bool found)
        {
            ChosenKey = chosenKey;
            Found = found;
        }
    }

    /// <summary>
    /// 确定性掷骰器。
    /// </summary>
    public static class EventRng
    {
        /// <summary>
        /// CDF walk: key 升序遍历。
        /// </summary>
        public static CdfWalkResult CdfWalk(ulong seed, List<int> keys, List<int> weights)
        {
            if (keys == null || weights == null || keys.Count != weights.Count)
                throw new ArgumentException("keys 和 weights 必须非空且长度相等");

            // 计算总权重 C
            long c = 0;
            foreach (var w in weights)
            {
                c += w;
            }

            // C=0 时不 mod，跳过
            if (c == 0)
            {
                return new CdfWalkResult(-1, false);
            }

            // r = S mod C
            long r = (long)(seed % (ulong)c);

            // key 升序遍历（创建索引数组并按 key 排序）
            var indices = new int[keys.Count];
            for (int i = 0; i < keys.Count; i++) indices[i] = i;
            Array.Sort(indices, (a, b) => keys[a].CompareTo(keys[b]));

            foreach (int i in indices)
            {
                r -= weights[i];
                if (r < 0)
                {
                    return new CdfWalkResult(keys[i], true);
                }
            }

            // 未命中（不应发生）
            return new CdfWalkResult(-1, false);
        }

        /// <summary>
        /// 计算窗口起点 tick。
        /// </summary>
        public static long ComputeWindowStart(long tick, long ticksPerDay)
        {
            return TimeBase.FDiv(tick, ticksPerDay) * ticksPerDay;
        }

        /// <summary>
        /// 验证种子可重算。
        /// </summary>
        public static bool ValidateSeedReproducible(ulong worldSeed, long win, int tier, long ordinal)
        {
            var seed1 = new EventRollSeed(win, tier, ordinal);
            var seed2 = new EventRollSeed(win, tier, ordinal);

            return seed1.ComputeHash(worldSeed) == seed2.ComputeHash(worldSeed);
        }
    }
}
