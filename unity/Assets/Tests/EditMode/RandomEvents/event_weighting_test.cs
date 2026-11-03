// 52 F1 步骤② 的权重构成 —— `W_i = W_base_i × TODMult_i × HistoryMult_i`(GDD `:839` / `:878`)
//
// 权威来源:
//   GDD random-events.md F1 步骤②(`:839`)· AC-52-45(`:1308` 夜间只抬威胁档)·
//   规则九 `HistoryMult`(`:900-907`)· DC-2 / D-21-17(`W_base` 是 int 不是 Fix)·
//   ADR-006(唯一舍入模式 ROUND_HALF_AWAY_FROM_ZERO)
//
// ▍本件补的是**一个真实缺陷**(2026-11-03)
//   2026-11-03 之前的 `EventDraw.Pick` 是 `h % keys.Count` —— **均匀抽取,`W_base` 完全没被用**。
//   GDD `:672` 逐字警告过这种形态:
//   > 一个 `argmax_i SplitMix64(t, k_i)` 的实现**能通过 AC-52-06 却完全无视权重**(不满足 F1)。
//   ⇒ 本件的判据不是「抽取是确定的」(那旧实现也满足),而是**「分布真的随权重变」**。
//
// ⚠️ **判据设计上的两处刻意选择**:
//   ① **不用统计近似**(跑 10000 次看频率)。那会让夹具对种子敏感、且慢;
//      改为**直接钉 `W_i` 的算术**(`EventWeighting.CandidatesOfTier` 的返回值),
//      再用**构造性**的方式证明 CDF walk 按权重分区(`test_cdfWalk_boundaries_matchWeights`)。
//   ② **必须有一条「权重改变命中」的对照**(`test_distribution_changesWithWeight`)——
//      否则「CDF walk 写对了但候选集权重恒等」会全绿(F8 行为等价掩盖分叉)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    /// <summary>F1 步骤②:权重的构成与加权游走。</summary>
    public sealed class EventWeightingTest
    {
        private const int Threat = 0;
        private const int Opportunity = 1;

        // ── 池夹具(合成;不读盘上真表 —— 真表的值会变,而本件测的是**算术**)──
        private static EventPoolDataSet Pool(int nightNum = 3, int nightDen = 2,
                                             int maxNum = 2, int maxDen = 1,
                                             params EventPoolEntryData[] entries)
            => new EventPoolDataSet(entries, nightNum, nightDen, maxNum, maxDen, 0u, 1u);

        private static EventPoolEntryData Entry(int key, int wBase, int tier,
                                                int trigger = 0, int causeFlag = 0)
            => new EventPoolEntryData(key, wBase, tier, spawnAnchor: 1, triggerMode: trigger,
                                      PoolHandler.Encounter, causeFlag);

        // ══════════════ 一、`W_i` 的算术 ══════════════

        [Test]
        public void test_dayTime_weightsAreWBaseVerbatim()
        {
            EventPoolDataSet pool = Pool(entries: new[]
            {
                Entry(1001, 100, Threat), Entry(1002, 40, Threat),
            });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Threat, isNight: false);

            Assert.That(c.Count, Is.EqualTo(2), "两条威胁档条目");
            Assert.That(c[0].Key, Is.EqualTo(1001), "序 = 池表序(已按 key 升序)");
            Assert.That(c[0].Weight, Is.EqualTo(100L), "白天:W_i = W_base(无乘子)");
            Assert.That(c[1].Weight, Is.EqualTo(40L), "白天:W_i = W_base(无乘子)");
        }

        [Test]
        public void test_night_multipliesThreatTierBy3over2()
        {
            EventPoolDataSet pool = Pool(entries: new[]
            {
                Entry(1001, 100, Threat),
            });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Threat, isNight: true);

            Assert.That(c[0].Weight, Is.EqualTo(150L),
                "夜间 · 威胁档:100 × 3/2 = 150(AC-52-45;用户 2026-11-03 裁定 3/2)");
        }

        [Test]
        public void test_night_doesNotAffectOtherTiers()
        {
            EventPoolDataSet pool = Pool(entries: new[]
            {
                Entry(2001, 100, Opportunity),
            });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Opportunity, isNight: true);

            Assert.That(c[0].Weight, Is.EqualTo(100L),
                "AC-52-45 第二半:机会 / 反应 / 灾难档 `TODMult ≡ 1`(夜间不抬)");
        }

        [Test]
        public void test_oddWeight_roundsHalfAwayFromZero()
        {
            // W_base = 1,3/2 ⇒ 1.5 ⇒ 舍入模式 ROUND_HALF_AWAY_FROM_ZERO ⇒ 2(不是 ties-to-even 的 2 ——
            // 这里两种模式同值,故再取一个能分开的点:W_base = 3 ⇒ 4.5 ⇒ away-from-zero = 5,even = 4)。
            EventPoolDataSet pool = Pool(entries: new[] { Entry(1001, 3, Threat) });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Threat, isNight: true);

            Assert.That(c[0].Weight, Is.EqualTo(5L),
                "3 × 3/2 = 4.5 ⇒ **5**(ROUND_HALF_AWAY_FROM_ZERO);ties-to-even 会给 4(ADR-006 禁)");
        }

        [Test]
        public void test_scriptEntries_areExcludedFromPool()
        {
            EventPoolDataSet pool = Pool(entries: new[]
            {
                Entry(1001, 100, Threat, trigger: 0),   // random
                Entry(1002, 100, Threat, trigger: 1),   // script
            });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Threat, isNight: false);

            Assert.That(c.Count, Is.EqualTo(1), "规则二:`触发方式 = 脚本` 的条目**不进抽池**");
            Assert.That(c[0].Key, Is.EqualTo(1001));
        }

        [Test]
        public void test_otherTiers_areFilteredOut()
        {
            EventPoolDataSet pool = Pool(entries: new[]
            {
                Entry(1001, 100, Threat), Entry(2001, 100, Opportunity),
            });

            Assert.That(EventWeighting.CandidatesOfTier(pool, Threat, false).Count, Is.EqualTo(1),
                "抽威胁档只取威胁档条目");
            Assert.That(EventWeighting.CandidatesOfTier(pool, Opportunity, false).Count, Is.EqualTo(1));
        }

        [Test]
        public void test_allZeroWeights_yieldEmptySum_notException()
        {
            EventPoolDataSet pool = Pool(entries: new[] { Entry(1001, 0, Threat) });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Threat, isNight: false);

            Assert.That(c.Count, Is.EqualTo(1), "条目仍在候选集里(权重 0)");
            Assert.That(c[0].Weight, Is.EqualTo(0L));
        }

        [Test]
        public void test_historyMult_defaultNull_isIdentity()
        {
            EventPoolDataSet pool = Pool(entries: new[] { Entry(1001, 100, Threat, causeFlag: 7) });

            IReadOnlyList<WeightedKey> c = EventWeighting.CandidatesOfTier(pool, Threat, false);

            Assert.That(c[0].Weight, Is.EqualTo(100L),
                "P0 无 HIST_W 表 ⇒ HistoryMult ≡ 1,即使条目带了 cause_flag");
        }

        // ══════════════ 二、CDF walk 真的按权重分区 ══════════════

        [Test]
        public void test_cdfWalk_boundariesMatchWeights()
        {
            // 两条等权候选 ⇒ 命中集合按 [0,1) / [1,2) 分区(ΣW = 2)。
            // 用「穷举种子」的方式证明分区边界 —— 不靠统计。
            var candidates = new[]
            {
                new WeightedKey(10, 1), new WeightedKey(20, 1),
            };
            var hits = new Dictionary<int, int>();
            for (long seed = 0; seed < 64; seed++)
            {
                Assert.That(EventDraw.TryPick(candidates, (ulong)seed, win: 0, tier: 0, ordinal: 0,
                        out int key), Is.True);
                hits[key] = (hits.TryGetValue(key, out int n) ? n : 0) + 1;
            }
            Assert.That(hits.Count, Is.EqualTo(2), "两条都该被命中过(否则分区坏了)");
            // ⚠️ 期望值 = **离线复算**所得(64 个种子跑同一 SplitMix64 折叠),不是「看起来应该是一半」:
            //    h % 2 在 0..63 上并不均匀(36/28)。写成 32/32 会让一个**正确**的实现转红。
            Assert.That(hits[10], Is.EqualTo(36), "等权:36/64(离线复算,非 32)");
            Assert.That(hits[20], Is.EqualTo(28));
        }

        [Test]
        public void test_distribution_changesWithWeight()
        {
            // ⚠️ 本件最关键的一条:证明「权重真的影响命中」。
            // 3:1 ⇒ 权重大的应被命中约 3/4(64 个种子里 48 个)。
            var skewed = new[] { new WeightedKey(10, 3), new WeightedKey(20, 1) };
            var hits = new Dictionary<int, int>();
            for (long seed = 0; seed < 64; seed++)
            {
                EventDraw.TryPick(skewed, (ulong)seed, win: 0, tier: 0, ordinal: 0, out int key);
                hits[key] = (hits.TryGetValue(key, out int n) ? n : 0) + 1;
            }
            // ⚠️ 48/16 = 离线复算所得(3:1 在 mod 4 上恰好均匀,与 1:1 不同)
            Assert.That(hits[10], Is.EqualTo(48), "W=3 / ΣW=4 ⇒ 48/64");
            Assert.That(hits[20], Is.EqualTo(16), "W=1 / ΣW=4 ⇒ 16/64");

            // 对照:把权重换成 1:3,分布必须**反过来** —— 否则说明命中与权重无关(F8)。
            var flipped = new[] { new WeightedKey(10, 1), new WeightedKey(20, 3) };
            var hits2 = new Dictionary<int, int>();
            for (long seed = 0; seed < 64; seed++)
            {
                EventDraw.TryPick(flipped, (ulong)seed, win: 0, tier: 0, ordinal: 0, out int key);
                hits2[key] = (hits2.TryGetValue(key, out int n) ? n : 0) + 1;
            }
            Assert.That(hits2[10], Is.EqualTo(18), "权重翻转 ⇒ 命中数**大致**翻转(18/64,离线复算)");
            Assert.That(hits2[20], Is.EqualTo(46), "⚠️ 不是精确的 16/48 —— 64 个种子上 mod 4 的分布本身不均匀");
        }

        [Test]
        public void test_zeroTotalWeight_returnsFalse_noThrow()
        {
            var candidates = new[] { new WeightedKey(10, 0), new WeightedKey(20, 0) };

            Assert.That(EventDraw.TryPick(candidates, 1UL, 0, 0, 0, out int key), Is.False,
                "ΣW = 0 ⇒ 返回 false(**不抛**)—— GDD Edge Cases `:1065`「跳过本轮,不报错」");
            Assert.That(key, Is.EqualTo(0));
        }

        [Test]
        public void test_sameInputs_bitIdentical()
        {
            var candidates = new[] { new WeightedKey(10, 7), new WeightedKey(20, 3), new WeightedKey(30, 5) };

            EventDraw.TryPick(candidates, 0xBEEFUL, win: 100, tier: 0, ordinal: 3, out int a);
            EventDraw.TryPick(candidates, 0xBEEFUL, win: 100, tier: 0, ordinal: 3, out int b);

            Assert.That(a, Is.EqualTo(b), "同输入 ⇒ 逐位相同(AC-52-06)");
        }

        [Test]
        public void test_ordinalChangesResult()
        {
            var candidates = new[] { new WeightedKey(10, 1), new WeightedKey(20, 1) };
            var seen = new HashSet<int>();
            for (int ordinal = 0; ordinal < 8; ordinal++)
            {
                EventDraw.TryPick(candidates, 0xC0FFEEUL, win: 0, tier: 0, ordinal, out int key);
                seen.Add(key);
            }
            Assert.That(seen.Count, Is.EqualTo(2), "不同 ordinal ⇒ 覆盖到两条(否则序号没进哈希)");
        }
    }
}
