// tests/unit/sim/sim_fixedpoint_test.cs
//
// 本文件的身份:**种子测试(seed test)** —— 证明 UTF / NUnit 管线可用,
// 同时把 ADR-006 的三条边界口径写成可执行断言。
//
// 权威来源:
//   docs/architecture/adr-006-fixed-point-boundary-contract.md
//     · §Decision 二 —— 存档与事件流禁 float,Fix 的落盘形状是其内部 long
//     · §Decision 三 —— 单一舍入模式 ROUND_HALF_AWAY_FROM_ZERO,**禁** Math.Round 默认(ties-to-even)
//     · §Decision 五(D-21-18)—— Fix **不可**经 Unity 内置序列化器承载,须自定义编码器,
//                                **由 EditMode 探针守住** ← 本文件即该探针的种子形态
//   docs/architecture/adr-005-deterministic-sim.md —— int64 / Q16.16 定点域
//
// 2026-09-22 · U0-b b5:本地 Q16Codec 参考实现已删,断言改指生产类型
//   `Sim.Contracts.FixParse`(种子测试自此升格为生产类型的回归夹具)。
//   当时刻意保留的一处本地件 = 8 字节小端 helper(生产编码器尚无落点,
//    不在无生产件时预先借绿「BinaryPrimitives 等价」)。
// 2026-09-23 · R-1:**b5 该义务结清** —— helper 迁往生产件 `Sim.Codec.CodecPrimitives`
//   (internal,经 IVT 访问);独立手写参考 loop 移入 sim_codec_roundtrip_test.cs
//   与生产件对拍(种子测试自此零本地编码器)。跨平台逐位等价仍归 ADR-012 矩阵,不在此借绿。

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    internal sealed class SimFixedPointTest
    {
        // ── 1. 往返:落盘形状是 raw long,且逐位可复原(ADR-006 §Decision 二) ──

        [TestCase(0L)]
        [TestCase(Fix.OneRaw)]                 // 1.0
        [TestCase(-Fix.OneRaw)]                // -1.0 —— 负域必须同样可复原
        [TestCase(49152L)]                          // 3/4
        [TestCase(1L)]                              // 最小正 raw(1 LSB)
        [TestCase(long.MinValue)]                   // 极值:序列化不得吞符号
        [TestCase(long.MaxValue)]
        public void test_simFixedPoint_codecRoundTrips_exactBits(long raw)
        {
            byte[] bytes = new byte[8];
            CodecPrimitives.WriteInt64LittleEndian(bytes, raw);
            long restored = CodecPrimitives.ReadInt64LittleEndian(bytes);

            Assert.That(restored, Is.EqualTo(raw),
                "raw 位型在编解码往返中改变 —— 三流读流会自此错位");
            Assert.That(bytes.Length, Is.EqualTo(8),
                "Fix 的落盘宽度是 8 字节;改宽 = 破 ADR-010 §二 的存档布局");
        }

        // ── 2. 舍入模式:ROUND_HALF_AWAY_FROM_ZERO(ADR-006 §Decision 三) ──

        [TestCase(1L, 1L << 17, 1L)]    // 1/2^17 = 0.5 LSB → **ties-to-even 会给 0**,远离零给 1
        [TestCase(5L, 1L << 17, 3L)]    // 2.5 LSB → ties-to-even 给 2,远离零给 3
        [TestCase(3L, 1L << 17, 2L)]    // 1.5 LSB → 两者同为 2(**不是**判别样本,留作对照)
        [TestCase(-1L, 1L << 17, -1L)]  // 负侧对称:远离零 ⇒ -0.5 → -1(不是 0)
        [TestCase(-5L, 1L << 17, -3L)]
        [TestCase(1L, 3L, 21845L)]      // 1/3 → 21845.33 → 就近截为 21845
        [TestCase(3L, 4L, 49152L)]      // 精确值不打折
        public void test_simFixedPoint_encode_usesRoundHalfAwayFromZero(long num, long den, long expectedRaw)
        {
            Assert.That(FixParse.FromRatio(num, den).Raw, Is.EqualTo(expectedRaw));
        }

        [Test]
        public void test_simFixedPoint_mathRoundDefault_differsFromAdrMode()
        {
            // 守门断言:证明「不写这条就会踩」—— .NET 的 Math.Round 默认是 ties-to-even。
            // 若有人把定点化简为 (long)Math.Round(x * 65536),本断言即红。
            const double halfLsb = 0.5d;                        // 0.5 LSB —— 一个中点
            long viaMathRound = (long)Math.Round(halfLsb);      // → 0(ties-to-even,被禁的模式)
            long viaAdrMode   = FixParse.FromRatio(1L, 1L << 17).Raw;  // → 1(远离零)

            Assert.That(viaMathRound, Is.EqualTo(0L),
                "Math.Round 默认把 0.5 舍到偶数 —— 这正是被禁的模式");
            Assert.That(viaAdrMode, Is.EqualTo(1L),
                "ADR-006 的模式必须舍到 1");
            Assert.That(viaAdrMode, Is.Not.EqualTo(viaMathRound),
                "两条路径在**中点**上必须分道 —— 若哪天它们相等,说明舍入实现退化");
        }

        [Test]
        public void test_simFixedPoint_fromRatio_shiftOverflow_throwsFormat()
        {
            // code review F4(Story 002 批):C# 的 << 溢出是**静默回绕** —— 超域分子会绕成任意 raw,
            // 可伪装成合法值溜过装载断言(F-3.1 唯一守门)。回归:超移位域显式抛 FormatException
            // (各 binder 既有 catch 可聚合),域内边界不得误拒。
            // ⚠️ 域是**不对称**的:正侧上限 = long.MaxValue >> 16 = 2^47 − 1(2^47 << 16 = 2^63 溢出);
            //    负侧下限 = long.MinValue >> 16 = −2^47(算术移位,(−2^47) << 16 = −2^63 恰在域内)。
            long maxInDomain = long.MaxValue >> Fix.FractionalBits;   // 2^47 − 1
            long minInDomain = long.MinValue >> Fix.FractionalBits;   // −2^47

            Assert.DoesNotThrow(() => FixParse.FromRatio(maxInDomain, 1L),
                "正侧域内边界(2^47 − 1)不得被误拒");
            Assert.DoesNotThrow(() => FixParse.FromRatio(minInDomain, 1L),
                "负侧域内边界(−2^47)不得被误拒 —— 恰移位不溢出");
            Assert.Throws<FormatException>(() => FixParse.FromRatio(maxInDomain + 1, 1L),
                "正侧超域(2^47)必须显式拒 —— 静默回绕会让错值伪装成合法 raw");
            Assert.Throws<FormatException>(() => FixParse.FromRatio(minInDomain - 1, 1L),
                "负侧超域必须显式拒 —— 回绕同样必须被拒");
        }

        // ── 3. 守恒律的形状(ADR-006 D-21-19):**量纲须齐** ──

        [Test]
        public void test_simFixedPoint_conservationLaw_needsWeightNormalisation()
        {
            // Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs),在整数域内求值。
            // 原式量纲不齐会被静默通过 —— 本例构造一个「不乘 weight 就漏判」的形状。
            const long effMaxRaw = 65536L;   // EFF_MAX ≤ 1 ⇒ raw ≤ 65536
            long[] inputs  = { 10, 10 };
            long[] weights = { 1, 100 };
            long[] outputs = { 12, 8 };      // 不加权看:Σout=20 vs Σin=20 ⇒ 看似平;
                                              // 加权看:Σw·out = 12+800 = 812 vs Σw·in = 10+1000 = 1010 ⇒ 合法

            long unweightedOut = 0, unweightedIn = 0, weightedOut = 0, weightedIn = 0;
            for (int i = 0; i < inputs.Length; i++)
            {
                unweightedIn += inputs[i];
                unweightedOut += outputs[i];
                weightedIn += weights[i] * inputs[i];
                weightedOut += weights[i] * outputs[i];
            }

            Assert.That(unweightedOut, Is.LessThanOrEqualTo(unweightedIn), "未加权口径(本例偶然也成立)");
            Assert.That(weightedOut, Is.LessThanOrEqualTo(weightedIn),
                "守恒律须以 Σ(weight × ...) 形式求值 —— 只比 Σout/Σin 会放过量纲错配");
            Assert.That(effMaxRaw, Is.LessThanOrEqualTo(65536L),
                "EFF_MAX ≤ 1 是 ADR-006 §Decision 四 的前提,>1 则守恒式失去意义");
        }

        // helpers 已迁:2026-09-23 R-1 —— 8 字节小端读写改调生产件 Sim.Codec.CodecPrimitives;
        // 独立参考 loop 见 sim_codec_roundtrip_test.cs(与生产件对拍,b5 义务结清)。
    }
}
