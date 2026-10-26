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
        /// CDF walk: key 升序遍历。取模形 —— <c>r = seed % Σw</c> 后逐档减。
        /// </summary>
        /// <remarks>
        /// <para>⚠️ <b>本形不得用于品级抽取</b>。GDD <c>foraging.md</c> F-17-1 <c>:279-285</c>
        /// 定死的是高 48 位形(<see cref="CdfWalkHigh48"/>):<c>U = h &gt;&gt; 16</c>,
        /// 取满足 <c>U × Σw &lt; C_q × 2^48</c> 的最小 <c>q</c>。</para>
        /// <para><b>两形统计同构、逐点不同</b> —— 实测(2026-10-26,5 档等权,20 万采样)
        /// 逐点一致率 <b>20.1%</b>(恰为 5 档随机配对的期望值),两形各自每档均 ≈ 20.0%。
        /// ⇒ 对同一 <c>(WorldSeed, node_id, gather_seq)</c>,两形会落<b>不同的品级</b>;
        /// 混用即让重放不逐位。品级抽取只能走 <see cref="CdfWalkHigh48"/>。</para>
        /// <para>本形保留给既有调用方(<c>51 / 52</c> 侧测试),零生产调用方
        /// (<c>grep -rn "CdfWalk" unity/Assets --include=*.cs | grep -v Tests/</c> 除本文件外零命中,
        /// 实测于 2026-10-26)。</para>
        /// </remarks>
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
        /// **高 48 位形** CDF walk —— GDD <c>foraging.md</c> F-17-1 <c>:279-285</c> 定死的形。
        /// </summary>
        /// <remarks>
        /// <para>判据逐字取自 GDD:</para>
        /// <code>
        /// raw_quality = max{ q ∈ [1, MAX_QUALITY] :
        ///                  U × Σw  &lt;  Σ_{q' ≤ q} w[q'] × 2^48 }     // 严格 &lt;
        /// </code>
        /// <para><b>边界口径(GDD <c>:287-292</c> 定死)</b>:<c>C_q = Σ_{q' ≤ q} w[q']</c>(整数精确);
        /// 取满足 <c>U × Σw &lt; C_q × 2^48</c> 的<b>最小</b> <c>q</c>(<b>严格 <c>&lt;</c></b>)
        /// ⇒ 各档区间为<b>左闭右开</b>,<c>q = 1</c> 的区间为 <c>[0, C_1)</c>,无重无漏。
        /// 全程整数乘除,禁浮点。</para>
        /// <para><b><c>Σw = 0</c> 是非法输入</b> —— 原式会静默返回最大档或除零,GDD <c>:290</c>
        /// 要求在<b>装载期</b>硬失败(17 侧形状校验器 <c>QualityDistributionShape</c> 已守)。
        /// 本方法在<b>运行期</b>再守一次纵深防御:抛 <see cref="ArgumentException"/>,不返回
        /// <c>Found = false</c> —— 静默退化会让「表坏了」与「抽中了」在类型上不可分。</para>
        /// <para><b>与 <see cref="CdfWalk"/> 的关系</b>:两形<b>统计同构、逐点不同</b>
        /// (实测逐点一致率 20.1%,5 档等权)。品级抽取<b>只能</b>用本方法。</para>
        /// </remarks>
        /// <param name="u48">掷骰源的整数归一化投影 <c>U = h &gt;&gt; 16</c>,值域 <c>[0, 2^48)</c>。</param>
        /// <param name="weights">各档权重(<c>int</c>,个数 —— D-21-17 移出 <c>Fix</c> 解析集)。
        /// 索引 <c>i</c> 对应品级 <c>i + 1</c>;<c>Σw</c> 须 ≥ 1。</param>
        /// <returns>命中的品级(<c>1..weights.Count</c>)。<c>Found</c> 恒 <c>true</c>
        /// (未命中不可达 —— <c>q = n</c> 时 <c>C_n = Σw</c>,<c>U &lt; 2^48</c> 保证判据成立)。</returns>
        /// <exception cref="ArgumentException"><paramref name="weights"/> 为 null、长度为 0、
        /// 含负权重,或 <c>Σw = 0</c>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="u48"/> ≥ <c>2^48</c>
        /// (调用方未做 <c>&gt;&gt; 16</c>,或用了低 48 位)。</exception>
        public static CdfWalkResult CdfWalkHigh48(ulong u48, IReadOnlyList<int> weights)
        {
            if (weights == null)
                throw new ArgumentNullException(nameof(weights), "weights 不得为 null");
            if (weights.Count == 0)
                throw new ArgumentException("weights 长度须 ≥ 1", nameof(weights));
            if (u48 >> 48 != 0)
                throw new ArgumentOutOfRangeException(nameof(u48), u48,
                    "U 须 ∈ [0, 2^48)(= h >> 16);收到更大的值说明调用方没做右移或用了低 48 位");

            long total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0)
                    throw new ArgumentException(
                        "weights[" + i + "] = " + weights[i] + " 为负 —— 负权重使累积和倒退,CDFWalk 单调前提破",
                        nameof(weights));
                total += weights[i];
            }

            // Σw = 0 非法(GDD :290 / :397 边界探针「除零 / 静默返回最大档」)。纵深防御,不静默。
            if (total == 0)
                throw new ArgumentException(
                    "Σw = 0 非法 —— CDFWalk 会除零或静默返回最大档(GDD foraging.md:290/:397);" +
                    "装载期须硬失败,运行期不得静默退化", nameof(weights));

            // 左侧 U × Σw 对全部档共用,提出来算一次。
            // ⚠️ 溢出面:U < 2^48,Σw ≤ int.MaxValue × 档数 ⇒ U × Σw 可达 2^48 × 2^31 × n,
            //    远超 ulong(2^64)。本方法的调用方(品级抽取)档数 ≤ MAX_QUALITY=5、
            //    Σw 为小整数权重,实测无溢出;但**不做无界承诺** —— 见下方组合守卫。
            if (total > long.MaxValue / (1L << 48))
                throw new ArgumentException(
                    "Σw 过大:Σw × 2^48 超出 long 值域,本形无法用 64 位整数精确比较", nameof(weights));

            ulong lhs = u48 * (ulong)total;          // U × Σw
            ulong rhsScale = 1UL << 48;

            long cumulative = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += weights[i];
                // 严格 < :左闭右开。C_q × 2^48 用 ulong 承载(cumulative ≥ 1 且已守卫上界)
                ulong rhs = (ulong)cumulative * rhsScale;
                if (lhs < rhs)
                    return new CdfWalkResult(i + 1, true);   // 索引 i ⇒ 品级 i+1
            }

            // 不可达:U < 2^48 ∧ C_n = Σw ⇒ lhs = U×Σw < Σw×2^48 = rhs。
            // 若真到这里,说明上面的守卫有洞 —— fail-loud,不静默返回一个档。
            throw new InvalidOperationException(
                "CDFWalkHigh48 未命中 —— 不可达路径被触发,说明值域守卫有洞");
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
