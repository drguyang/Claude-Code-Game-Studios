// 52 掷骰的**生产实装** —— 取代 `P0HostEventAuthority.Roll` 的抛形占位(2026-11-01 · S22-4)
//
// 权威来源:
//   ADR-007 §三 —— 掷骰的**每个输入必须可从事件流重构**;落流时 `EventRolled` 记
//     `(win, tier, ordinal, chosenKey)` 四项
//   ADR-005 —— 全案唯一整数哈希源 = `SplitMix64`(禁 `UnityEngine.Random`)
//   GDD random-events.md F1 第 1 步「时机轴 → 抽池(内容轴,按 key 过滤)」
//
// ▍与 `P0HostEventAuthority` 的关系
//   那个类**不删** —— 它自陈「P0 无掷骰需求,不得返回假值」,在本件落地前它是对的。
//   本件把「无掷骰实现」变成「有掷骰实现」,两者的分界 = **候选池是否为空**:
//   生产今日池为空(52 的池内容归 37 内容件)⇒ `Roll` 会**抛**(空池不得掷),
//   `RandomEventSpawnChain` 在调用前先判空并返回 `NoPool` —— **不抛到玩家面前**。
//   ⇒ 「通电待信」的形态不变,变的是:链路本身**已经存在并可被合成输入驱动**(本卡的判据面)。
//
// ⚠️ 本件**不做**:权重(等权候选集)· 历史标记乘子 · 密度预算 —— 归属见 `EventDraw` 头注。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>以世界种子驱动的确定性掷骰(P0 主机唯一权威)。
    /// <para>⚠️ 本类实现 <see cref="IP0AuthorityPlaceholder"/>:**它恒 <c>IsHost = true</c>,
    /// 没有任何联机 / 非主机语义** —— 与 `P0HostEventAuthority` 同一口径(该标记就是为
    /// 51 的 E3 哨兵设的:恒 host 的 P0 权威被扫到不得被读成「真联机就绪」)。
    /// 区别只在 `Roll`:那个类是抛形占位,**本类是真掷骰**,但**掷骰权 ≠ 联机权**。</para></summary>
    public sealed class SeedEventAuthority : IEventAuthority, IP0AuthorityPlaceholder
    {
        private readonly Func<int, IReadOnlyList<int>> _keysOfTier;
        private readonly ulong _worldSeed;

        /// <param name="keysOfTier">档 → 候选 key 集(有序)。生产今日返回空集(fail-safe)。</param>
        /// <param name="worldSeed">世界种子(存档头,ADR-007 §二)。</param>
        public SeedEventAuthority(Func<int, IReadOnlyList<int>> keysOfTier, ulong worldSeed)
        {
            _keysOfTier = keysOfTier ?? throw new ArgumentNullException(nameof(keysOfTier));
            _worldSeed = worldSeed;
        }

        /// <inheritdoc/>
        public bool IsAuthority => true;

        /// <inheritdoc/>
        public bool IsHost => true;

        /// <summary>
        /// 掷骰 —— `SplitMix64(WorldSeed, win, tier, ordinal) % 候选数`。
        /// <para>⚠️ 空档候选集 ⇒ **抛**(不返回假 key)。调用方须先判空。</para>
        /// </summary>
        public EventRollResult Roll(in RollRequest r)
        {
            IReadOnlyList<int> keys = _keysOfTier(r.Tier);
            if (keys == null || keys.Count == 0)
                throw new InvalidOperationException(
                    $"档 {r.Tier} 的候选集为空 —— 空池不得掷骰(会造出不存在的 event_key)。" +
                    "生产今日如此(52 的池内容归 37 内容件),调用方须先判空走 NoPool。");

            int key = EventDraw.Pick(keys, _worldSeed, r.Win, r.Tier, r.Ordinal);
            return new EventRollResult(r.Win, r.Tier, r.Ordinal, key);
        }
    }
}
