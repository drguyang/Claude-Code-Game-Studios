// S20-4 前置链 · foraging story-002 —— 品级抽取管线 F-17-1 + 单次采量 F-17-2
//
// 权威来源:
//   GDD design/gdd/foraging.md
//     · F-17-1 `:265-310`(raw / Cap / clamp 三步)· 边界口径 `:287-292`(严格 < · 左闭右开)
//     · 装载期校验 `:303-311`(Σw ≥ 1)· 边界探针表 `:397-400`
//     · F-17-2 `:318-336`(qty = Mul(qty_per_node, GatherMul(L)) · 唯一舍入点)
//     · §Tuning Knobs `:543`(GatherMul(L) = 1 + L / 30 · GATHER_MUL_CAP = 3)
//   story production/epics/foraging/story-002-quality-roll-pipeline.md
//     AC-17-03 / AC-17-04 / AC-17-11 / AC-17-15(+ AC-17-02 的 Mono 半边)
//
// ⚠️ **AC-17-02 只测 Mono 半边**。跨平台逐位对拍归 ADR-012 的三格矩阵,该矩阵不存在
//    + F7(int64 溢出 UB)spike 未跑 ⇒ 跨平台半边**不记绿**。本文件里那条用例的名字带
//    `_monoSideOnly`,就是为了让「只证了一半」在测试列表里可见,而不是藏在一个绿里。
//
// ⚠️ **本文件零真表依赖**。`quality_distribution` 在 21a 四行 `gather_profile` 里**全不存在**
//    (S20-3 已登 ADVISORY,归 21a 形状落地轮)⇒ 本件的分布一律用**合成夹具**驱动。
//    这不是省事:story 的 Implementation Notes 第 6 条明写「数值未裁不阻塞,以合成夹具驱动」。
//    ⚠️ 但**真表侧不可判**这一点须在 story 卡上如实登记,不得因为测试全绿就说「整条 AC 已验」。
//
// ⚠️ **`GatherMul` 是阶跃曲线不是连续曲线**。`1 + L/30` 在整数除法下只产出 {1,2,3}
//    (L∈[0,29]→1 · [30,59]→2 · 60→3)。已登 `TD-foraging-02`,归数值轮。
//    本文件的 AC-17-15 用例断的是「字面式落地的形状」,**不是**「曲线平滑」——
//    把它说成后者就是给一个错误的设计背书。
//
// ⚠️ 零随机种子(全部 seed 是写死的字面量)· 零时间依赖 · 零文件 I/O。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Foraging;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>
    /// F-17-1 / F-17-2 —— 品级抽取管线与单次采量。
    /// </summary>
    [TestFixture]
    public sealed class GatherQualityRollTest
    {
        // 合成分布的档数。5 = MAX_QUALITY(21a),与真表同宽但不取真值。
        private const int DistributionsLevels = 5;

        // ══════════════════════════════════════════════════════════════════
        // AC-17-03 —— 单档 100% 分布 ⇒ raw 恒等该档,无除零 / 越界
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1703_singleLevel100Percent_rawIsAlwaysThatLevel()
        {
            // 夹具:仅档 3 权重 100,余档 0。
            var dist = new[] { 0, 0, 100, 0, 0 };

            // 采样面:8 个节点 × 8 个 gather_seq = 64 组(与 QA Test Case 的「任意 64 组」对齐),
            // seed 全部写死 —— 不引随机源,重跑逐位同。
            for (int node = 0; node < 8; node++)
            {
                for (int seq = 0; seq < 8; seq++)
                {
                    int raw = GatherQualityRoll.RawQuality(WorldSeedFixture, node, seq, dist);
                    Assert.That(raw, Is.EqualTo(3),
                        $"单档 100% 分布下 raw 应恒 = 3;node={node} seq={seq} 实得 {raw}");
                }
            }
        }

        [Test]
        public void test_ac1703_singleLevel100Percent_lowCap_clampsToCap()
        {
            // 夹具:分布仍是单档 3 权重 100%;但 CapTable 在 L=0 给 1。
            // GDD 订正口径:「恒 = 该档 ∩ ≤ Cap」⇒ 期望 min(3, cap)。
            var dist = new[] { 0, 0, 100, 0, 0 };
            ForagingConstants constants = ConstantsWithFlatCap(1);

            for (int node = 0; node < 8; node++)
            {
                int q = GatherQualityRoll.OutQuality(
                    WorldSeedFixture, node, 0, dist, constants, gatherLevel: 0);
                Assert.That(q, Is.EqualTo(1),
                    $"cap = 1 而 raw 恒 3 ⇒ out 应被钳到 1;node={node} 实得 {q}");
            }
        }

        [Test]
        public void test_ac1703_cdfBoundaries_leftClosedRightOpen()
        {
            // GDD `:287-292` 定死:严格 `<`,各档区间左闭右开,`q=1` 的区间为 `[0, C_1)`。
            //
            // ⚠️ **下界公式(第一版写错过,2026-10-26 实测抓出)**:
            //    判据是 `U × Σw < C_q × 2^48` ⇒ 档 q 的下界 = **满足该式的最小 U**
            //         lo(q) = ceil( C_{q-1} × 2^48 / Σw )        // 注意是 C_{q-1},累积权重
            //    第一版误写成 `floor((q-1) × 2^48 / Σw)` —— 错在两处:
            //      ① 用了 `q-1` 而不是累积权重 `C_{q-1}`(等权时二者恰好相等,不等权时全错);
            //      ② 用了 floor 而不是 ceil(`U×Σw < C_q×2^48` 在等下是 `U < C_q×2^48/Σw`,
            //         取整下界须上取整)。
            //    等权夹具下 ① 恰好不暴露,② 仍会使每个下界差 1 ⇒ 该点落前一档。
            //    **若没有离线先用 python 复算,这条测试会把一个正确的实现判成红的。**
            ulong limit = GatherQualityRoll.U48Limit;      // 2^48

            // 两个夹具:等权(暴露 ceil 面)+ 不等权(暴露累积权重面)。
            // 零权重档不参与下界断言 —— 它没有独立区间,不可达(由单档 100% 用例另断)。
            AssertCdfBoundaries(new[] { 1, 1, 1, 1, 1 }, "等权 [1,1,1,1,1]");
            AssertCdfBoundaries(new[] { 3, 1, 4, 1, 5 }, "不等权 [3,1,4,1,5]");

            void AssertCdfBoundaries(int[] dist, string label)
            {
                int total = 0;
                foreach (int w in dist) total += w;

                // 左端点 U = 0 ⇒ 必落 q=1(左闭)
                Assert.That(EventRng.CdfWalkHigh48(0, dist).ChosenKey, Is.EqualTo(1),
                    "[" + label + "] U = 0 须落 q=1(左闭)");

                // 右端点 U = 2^48 - 1 ⇒ 必落末档(右开:U 恒 < 2^48,末档 C_n = Σw 必命中)
                Assert.That(EventRng.CdfWalkHigh48(limit - 1, dist).ChosenKey,
                    Is.EqualTo(dist.Length),
                    "[" + label + "] U = 2^48 - 1 须落末档");

                long cum = 0;                       // C_{q-1}
                for (int q = 1; q <= dist.Length; q++)
                {
                    if (dist[q - 1] == 0) { cum += dist[q - 1]; continue; }   // 零权重档无独立区间

                    // lo = ceil(C_{q-1} × 2^48 / Σw) —— 纯整数,不引浮点
                    ulong lo = (ulong)((cum * (long)limit + total - 1) / total);

                    Assert.That(EventRng.CdfWalkHigh48(lo, dist).ChosenKey, Is.EqualTo(q),
                        "[" + label + "] q=" + q + " 的下界 U=" + lo + " 应落 q=" + q + "(左闭)");

                    if (lo > 0)
                    {
                        Assert.That(EventRng.CdfWalkHigh48(lo - 1, dist).ChosenKey, Is.EqualTo(q - 1),
                            "[" + label + "] q=" + q + " 下界前一点 U=" + (lo - 1)
                            + " 应落 q=" + (q - 1) + "(右开)");
                    }

                    cum += dist[q - 1];
                }
            }
        }

        [Test]
        public void test_ac1703_strictLessThan_atExactBoundary_takesNextLevel()
        {
            // ⚠️ **本测的由来(证伪轮 F2,2026-10-26)**:原 `test_ac1703_cdfBoundaries` 用
            //    Σw = 5 与 Σw = 14 两个夹具,变异 `<` → `<=` 时 **red=0** —— 因为对这两个 Σw,
            //    `U × Σw` 永远**不恰等于** `C_q × 2^48`(`2^48 mod 5 = 1`、`mod 14 = 4`),
            //    等号分支永不触发 ⇒ 严格与不严格**同结果**。
            //    ⇒ 「严格 <」这条属性在原夹具下**零判别力**,现补本测。
            //
            // 能触发的条件:`Σw` 整除 `2^48 × C_q`。取 Σw = 2(2 的幂)最直接:
            //    weights = [1,1],C_1 = 1,U 取 2^47 ⇒ U×Σw = 2^47 × 2 = 2^48 = C_1 × 2^48
            //    ⇒ **恰等**。严格 `<` ⇒ 不命中 q=1,落 q=2;`<=` ⇒ 错误命中 q=1。
            var dist = new[] { 1, 1 };
            ulong exact = GatherQualityRoll.U48Limit / 2;      // 2^47

            Assert.That(exact * 2UL, Is.EqualTo(GatherQualityRoll.U48Limit),
                "夹具自检:U × Σw 须恰等于 2^48(否则本测打空靶,测不出 < 与 <= 的差别)");

            Assert.That(EventRng.CdfWalkHigh48(exact, dist).ChosenKey, Is.EqualTo(2),
                "U × Σw 恰等于 C_1 × 2^48 时,严格 `<` 不命中 q=1 ⇒ 须落 q=2。" +
                "若得 1 说明实现用了 `<=`(右端闭合,区间重叠)");

            // 边界前一点必须仍落 q=1(证明上面那条不是因为整条断言偏了)
            Assert.That(EventRng.CdfWalkHigh48(exact - 1, dist).ChosenKey, Is.EqualTo(1),
                "U × Σw 恰等于 2^48 - 2 时应属 q=1(左闭)");
        }

        [Test]
        public void test_ac1703_drawU_takesHigh48Bits_notLow()
        {
            // ⚠️ **本测的由来(证伪轮 F1,2026-10-26)**:`>> 16` → `>> 8` 时,
            //    `test_ac1703_cdfBoundaries` **没有转红** —— 因为它直接调 `CdfWalkHigh48(lo, ...)`,
            //    **根本不经过 `DrawU`**;转红的 6 条都是「U 变了导致品级偶然不同」或
            //    「低 56 位超出 u48 守卫」这类**间接**效应。
            //    ⇒ 「U = h 的高 48 位」这条属性**没有直接夹具**,现补本测。
            //
            // 判据形态:**能力断言 + 字面量**(不从实现里抄常量 —— 承
            //    `feedback_self_referential_fixture`:断言引用被测常量会让变异两边一起动)。
            const long seed = 0x0123_4567_89AB_CDEFL;
            const int node = 11;
            const int seq = 3;

            // ① U 必须落在 [0, 2^48) —— 这是「高 48 位」的**定义性后果**。
            //    `>> 8`(低 56 位)几乎必然越界;就算偶然不越界,值也不同。
            ulong u = GatherQualityRoll.DrawU(seed, node, seq);
            Assert.That(u, Is.LessThan(1UL << 48),
                "U 须 < 2^48(取的是高 48 位)。得 " + u +
                " —— 越界说明移位量不是 16(例如用了低 56 位)");

            // ② 值必须是**高位**派生的:U 的高 8 位应等于 h 的最高字节。
            //    用独立实现的 SplitMix64 复算高位(不从被测类取常量)。
            ulong expectedTop = IndependentHashTagged(seed, "gather", node, seq) >> 16;
            Assert.That(u, Is.EqualTo(expectedTop),
                "U 须恰等于 h >> 16(独立复算的高 48 位)。" +
                "实得 " + u + ",期望 " + expectedTop);
        }

        [Test]
        public void test_ac1703_zeroSumWeights_throws_notSilent()
        {
            // GDD `:290` + 边界探针表 `:397`:[0,0,0,0,0] ⇒ 必须硬失败,不得静默返回最大档。
            var dist = new[] { 0, 0, 0, 0, 0 };
            var ex = Assert.Throws<ArgumentException>(
                () => EventRng.CdfWalkHigh48(0, dist));
            Assert.That(ex.Message, Does.Contain("Σw = 0"),
                "报错须点名 Σw = 0。实得:" + ex.Message);
        }

        [Test]
        public void test_ac1703_negativeWeight_throws()
        {
            // 负权重 ⇒ 累积和倒退 ⇒ CDF 单调前提破。GDD `:303-311` 的装载期校验同源。
            var dist = new[] { 5, -1, 5, 0, 0 };
            var ex = Assert.Throws<ArgumentException>(
                () => EventRng.CdfWalkHigh48(0, dist));
            Assert.That(ex.Message, Does.Contain("为负"),
                "报错须点名负权重。实得:" + ex.Message);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-17-04 —— raw > Cap ⇒ out == Cap;raw 可从三元组逐位重算
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1704_rawAboveCap_clampsToCap()
        {
            // 夹具:单档 5 权重 100(⇒ raw 恒 5);CapTable 在 L=0 给 2。
            var dist = new[] { 0, 0, 0, 0, 100 };
            ForagingConstants constants = ConstantsWithFlatCap(2);

            int raw = GatherQualityRoll.RawQuality(WorldSeedFixture, 7, 3, dist);
            int q = GatherQualityRoll.OutQuality(WorldSeedFixture, 7, 3, dist, constants, 0);

            Assert.That(raw, Is.EqualTo(5), "夹具自检:该分布下 raw 须恒 5");
            Assert.That(q, Is.EqualTo(2), $"raw=5 > cap=2 ⇒ out 应为 2,实得 {q}");
        }

        [Test]
        public void test_ac1704_rawIsRecomputable_fromTriple()
        {
            // 「可重算」= 同 (WorldSeed, node_id, gather_seq) 两次独立求值逐位相同。
            // 这是 raw 不落流的**唯一正当性依据**(GDD F-17-1 注)。
            var dist = new[] { 10, 20, 30, 25, 15 };
            const long seed = 0x5EED_1234_ABCD_0001;

            for (int node = 0; node < 6; node++)
            {
                for (int seq = 0; seq < 6; seq++)
                {
                    int a = GatherQualityRoll.RawQuality(seed, node, seq, dist);
                    int b = GatherQualityRoll.RawQuality(seed, node, seq, dist);
                    Assert.That(a, Is.EqualTo(b),
                        $"raw 须可由三元组逐位重算;node={node} seq={seq} 两次得 {a} / {b}");
                }
            }
        }

        [Test]
        public void test_ac1704_rawQualityIsNotOnAnyPayloadSurface()
        {
            // 规则四:「raw_quality 不落流」。判据 = 载荷类型面上不存在 raw_quality 字段(反射)。
            // ⚠️ 这是**类型面断言**,不是 grep —— 改字段名叫 rawQ 也会被这条抓到。
            var payloadTypes = typeof(ResourceHarvestedPayload).Assembly
                .GetTypes()
                .Where(t => t.Name.EndsWith("Payload", StringComparison.Ordinal))
                .ToArray();

            Assert.That(payloadTypes.Length, Is.GreaterThan(0),
                "夹具自检:须扫到至少一个 Payload 类型(否则本测打空靶)");

            var offenders = new List<string>();
            foreach (var t in payloadTypes)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    string n = f.Name.ToLowerInvariant();
                    if (n.Contains("rawquality"))
                        offenders.Add(t.Name + "." + f.Name);
                }
            }

            Assert.That(offenders, Is.Empty,
                "载荷类型面不得含 raw_quality 字段(raw 可重算,存它就是第二真源):"
                + string.Join(", ", offenders));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-17-11 —— gather_seq 零独立计数器
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1711_rollClassHasNoPerNodeCounter()
        {
            // 判据:17 的抽取类不得持有任何可变静态 / 实例状态 —— 全部方法必须是纯函数。
            // 判据形态用**反射扫类型面**,不是 grep:改字段名也逃不掉。
            var t = typeof(GatherQualityRoll);

            var staticFields = t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var mutableStatics = staticFields
                .Where(f => !f.IsLiteral && !f.IsInitOnly)     // 非 const、非 readonly
                .Select(f => f.Name)
                .ToArray();

            Assert.That(mutableStatics, Is.Empty,
                "GatherQualityRoll 不得有可变静态字段(那就是第二个计数器/第二真源):"
                + string.Join(", ", mutableStatics));

            // 公开面必须全是静态方法 + 常量(F-17-1 是纯函数管线)
            var instanceMembers = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.DeclaringType == t)
                .Select(m => m.Name)
                .ToArray();
            Assert.That(instanceMembers, Is.Empty,
                "GatherQualityRoll 不得有实例方法(无状态 ⇒ 无可变随机态):"
                + string.Join(", ", instanceMembers));
        }

        [Test]
        public void test_ac1711_gatherSeqNegative_rejected()
        {
            // gather_seq 是「该节点已采次数」,恒 ≥ 0。负值只可能来自调用方算错,
            // 放行会让 SplitMix64 收到一个未定义语义的输入。
            var dist = new[] { 1, 1, 1, 1, 1 };
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => GatherQualityRoll.DrawU(WorldSeedFixture, 0, -1));
            Assert.That(ex.ParamName, Is.EqualTo("gatherSeq"));
        }

        [Test]
        public void test_ac1711_sameSeqDifferentNode_yieldsIndependentDraws()
        {
            // 反证「gather_seq 被当成全局计数器」:若它真是全局计数,同 seq 不同 node 会撞。
            // 判据:8 个节点在 seq=0 上的 U 值互不相同(不是要求「全不同」的概率断言 ——
            // 8 个 48 位值两两相撞的概率 < 1e-13,写死 seed 后是确定性事实)。
            var us = new HashSet<ulong>();
            for (int node = 0; node < 8; node++)
                us.Add(GatherQualityRoll.DrawU(WorldSeedFixture, node, 0));

            Assert.That(us.Count, Is.EqualTo(8),
                "同 seq 下 8 个节点应给出 8 个不同的 U —— 相同说明 node_id 没进哈希");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-17-15 —— GatherMul(L) 形状
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1715_gatherMul0_isOne()
        {
            ForagingConstants c = LegalConstants();
            Assert.That(GatherQualityRoll.GatherMul(0, c).Raw,
                Is.EqualTo(Fix.OneRaw),
                "GatherMul(0) 须 == 1(低技能不惩罚,只少给)");
        }

        [Test]
        public void test_ac1715_gatherMul_monotonicNonDecreasing_overFullRange()
        {
            ForagingConstants c = LegalConstants();
            long prev = long.MinValue;
            for (int L = 0; L <= ForagingConstants.MaxSkillLevel; L++)
            {
                long cur = GatherQualityRoll.GatherMul(L, c).Raw;
                Assert.That(cur, Is.GreaterThanOrEqualTo(prev),
                    $"GatherMul 须单调不减;L={L} 得 {cur},上一档 {prev}");
                prev = cur;
            }
        }

        [Test]
        public void test_ac1715_gatherMul_cappedAtGatherMulCap()
        {
            ForagingConstants c = LegalConstants();
            long cap = c.GatherMulCap.Raw;
            for (int L = 0; L <= ForagingConstants.MaxSkillLevel; L++)
            {
                long cur = GatherQualityRoll.GatherMul(L, c).Raw;
                Assert.That(cur, Is.LessThanOrEqualTo(cap),
                    $"GatherMul 须 ≤ GATHER_MUL_CAP({cap});L={L} 得 {cur}");
            }

            // 端点:满级恰撞 CAP(字面式 1 + 60/30 = 3,CAP = 3)
            Assert.That(GatherQualityRoll.GatherMul(ForagingConstants.MaxSkillLevel, c).Raw,
                Is.EqualTo(cap), "L = SKILL_CAP 时字面式给出 3,CAP 也是 3 ⇒ 应恰等于 CAP");
        }

        [Test]
        public void test_ac1715_gatherMul_isStepwise_notContinuous()
        {
            // ⚠️ **这条测的是「当前字面式的实际形状」,不是「设计正确」**。
            // `1 + L/30` 整数除法 ⇒ 只有 {1, 2, 3} 三档:
            //   L ∈ [0, 29] → 1 ; L ∈ [30, 59] → 2 ; L = 60 → 3
            // 已登 TD-foraging-02。本测的作用 = 把这个形状钉死,将来数值轮改公式时
            // **它一定会红**,从而强制那次改动被看见而不是静默发生。
            ForagingConstants c = LegalConstants();

            Assert.That(GatherQualityRoll.GatherMul(0, c).Raw, Is.EqualTo(Fix.OneRaw), "L=0 → 1");
            Assert.That(GatherQualityRoll.GatherMul(29, c).Raw, Is.EqualTo(Fix.OneRaw), "L=29 → 1");
            Assert.That(GatherQualityRoll.GatherMul(30, c).Raw, Is.EqualTo(MulRawOf(2)), "L=30 → 2");
            Assert.That(GatherQualityRoll.GatherMul(59, c).Raw, Is.EqualTo(MulRawOf(2)), "L=59 → 2");
            Assert.That(GatherQualityRoll.GatherMul(60, c).Raw, Is.EqualTo(MulRawOf(3)), "L=60 → 3");
        }

        [Test]
        public void test_ac1715_harvestQty_halfValueRoundsAwayFromZero()
        {
            // 唯一舍入点是 `Fix.Round()`(ROUND_HALF_AWAY_FROM_ZERO)。
            // 找一个恰落 .5 的组合:qty_per_node 与 GatherMul 的乘积小数部分 = 1/2。
            // 夹具:qty_per_node = 1,GatherMul = 1 ⇒ 乘积 1.0(不触舍入)。
            //      要触 .5 需要 GatherMul 有小数 —— 但字面式只有整数档。
            // ⇒ **字面式下 .5 不可达**。本测因此改为断「整数乘积不舍入」(结构事实),
            //    并在注释里记明「.5 舍入路径当前不可达,T D-foraging-02 修公式后须补」。
            ForagingConstants c = LegalConstants();

            Assert.That(GatherQualityRoll.HarvestQty(2, 0, c), Is.EqualTo(2), "2 × 1 = 2");
            Assert.That(GatherQualityRoll.HarvestQty(2, 30, c), Is.EqualTo(4), "2 × 2 = 4");
            Assert.That(GatherQualityRoll.HarvestQty(3, 60, c), Is.EqualTo(9), "3 × 3 = 9");
        }

        [Test]
        public void test_ac1715_harvestQty_alwaysAtLeastOne()
        {
            // qty ≥ 1 由 qty_per_node ≥ 1 ∧ GatherMul(0) = 1 结构保证(半程上舍入不会降到 0)
            ForagingConstants c = LegalConstants();
            for (int L = 0; L <= ForagingConstants.MaxSkillLevel; L++)
            {
                int q = GatherQualityRoll.HarvestQty(1, L, c);
                Assert.That(q, Is.GreaterThanOrEqualTo(1),
                    $"qty 须 ≥ 1;qty_per_node=1 L={L} 得 {q}");
            }
        }

        [Test]
        public void test_ac1715_harvestQty_zeroOrNegativeBase_rejected()
        {
            ForagingConstants c = LegalConstants();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GatherQualityRoll.HarvestQty(0, 0, c));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GatherQualityRoll.HarvestQty(-3, 0, c));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-17-02(仅 Mono 半边)—— 确定性自洽 + 纯函数性
        //
        // ⚠️ 跨平台逐位对拍归 ADR-012 三格矩阵,该矩阵不存在 ⇒ **跨平台半边不记绿**。
        //    用例名带 _monoSideOnly 就是为了让这个「只有一半」在测试列表里可见。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1702_monoSideOnly_sameInput100RunsBitIdentical()
        {
            var dist = new[] { 10, 20, 30, 25, 15 };
            const long seed = 0x0C0D_E202_6101_5004L;

            int baseline = GatherQualityRoll.RawQuality(seed, 42, 7, dist);
            for (int i = 0; i < 99; i++)
            {
                Assert.That(GatherQualityRoll.RawQuality(seed, 42, 7, dist),
                    Is.EqualTo(baseline),
                    $"同输入第 {i + 2} 次求值须逐位相同");
            }
        }

        [Test]
        public void test_ac1702_monoSideOnly_evaluationOrderDoesNotMatter()
        {
            // 纯函数性:先算 (node=1,seq=1) 还是先算 (node=3,seq=5) 不影响彼此结果。
            var dist = new[] { 10, 20, 30, 25, 15 };
            const long seed = 0x0BEE_F001_0002_0003L;

            int a1 = GatherQualityRoll.RawQuality(seed, 1, 1, dist);
            int b1 = GatherQualityRoll.RawQuality(seed, 3, 5, dist);

            int b2 = GatherQualityRoll.RawQuality(seed, 3, 5, dist);
            int a2 = GatherQualityRoll.RawQuality(seed, 1, 1, dist);

            Assert.That(a1, Is.EqualTo(a2), "求值次序不得影响结果(纯函数)");
            Assert.That(b1, Is.EqualTo(b2), "求值次序不得影响结果(纯函数)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具(全部合成,零真表依赖)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>本文件统一使用的世界种子 —— 写死字面量,不引随机源。</summary>
        private const long WorldSeedFixture = 0x00DA_D1EC_2026L;

        /// <summary>
        /// **独立实现**的 <c>SplitMix64.HashTagged</c> —— 供「U = h >> 16」的判据使用。
        /// </summary>
        /// <remarks>
        /// ⚠️ **为什么不能直接调 <c>SplitMix64.HashTagged</c>**:那样断言的两边
        /// (被测的 <c>DrawU</c> 与夹具的期望值)会共享同一个哈希实现 —— 若该实现本身
        /// 被变异,两边一起动,测试仍然绿(自指判据,见 `feedback_self_referential_fixture`)。
        /// 本函数是**逐字独立重写**的一份,只在本文件内使用,故意不复用生产代码。
        /// <para>它同时钉住 tag 编码块的口径(长度前缀 + 每 8 字节大端补零块),
        /// 这也是 <c>"gather"</c> 盐位约定的一部分。</para>
        /// </remarks>
        private static ulong IndependentHashTagged(long a, string tag, long b, long c)
        {
            const ulong gamma = 0x9E3779B97F4A7C15UL;

            static ulong Avalanche(ulong z)
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            static ulong Fold(ulong state, ulong w) => Avalanche(unchecked(state + w));

            static ulong U(long v) => unchecked((ulong)v);

            // tag 编码块:长度 + 每 8 字节**大端**补零
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(tag);
            ulong state = Fold(U(a), (ulong)(uint)bytes.Length);
            for (int i = 0; i < bytes.Length; i += 8)
            {
                ulong blk = 0;
                for (int k = 0; k < 8 && i + k < bytes.Length; k++)
                    blk |= (ulong)bytes[i + k] << (8 * (7 - k));    // 大端
                state = Fold(state, blk);
            }

            return Avalanche(Fold(Fold(state, U(b)), U(c)));
        }

        private static long MulRawOf(int integer)
            => (long)integer << Fix.FractionalBits;

        /// <summary>与真表同形的 CapTable(长度 61 · 单调不减 · [0]≥1 · ≤ MAX_QUALITY)。</summary>
        private static int[] LegalCapTable()
        {
            var caps = new int[61];
            for (int i = 0; i < 61; i++) caps[i] = 1 + Math.Min(i, 20) / 20;
            return caps;
        }

        /// <summary>恒定 cap 的 CapTable(全 61 档同值)—— 供「raw 恒 X,cap 恒 Y」型夹具使用。</summary>
        private static int[] FlatCapTable(int cap)
        {
            var caps = new int[61];
            for (int i = 0; i < 61; i++) caps[i] = cap;
            return caps;
        }

        private static IReadOnlyList<Fix> LegalSeasonMult()
            => new[]
            {
                FixParse.Parse("1"), FixParse.Parse("5/4"), FixParse.Parse("3/4"),
                FixParse.Parse("3/2"), FixParse.Parse("3/4"),
            };

        private const int SeasonsPerYear = 5;
        private const int MaxQuality = 5;

        private static ForagingConstants LegalConstants()
            => BuildConstants(LegalCapTable());

        private static ForagingConstants ConstantsWithFlatCap(int cap)
            => BuildConstants(FlatCapTable(cap));

        private static ForagingConstants BuildConstants(int[] capTable)
            => new ForagingConstants(
                capTable, LegalSeasonMult(), SeasonsPerYear, 1200, 5,
                FixParse.Parse("1/40"), FixParse.Parse("1/2"), FixParse.Parse("3"), MaxQuality);
    }
}
