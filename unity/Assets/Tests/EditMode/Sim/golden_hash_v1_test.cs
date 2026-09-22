// U1 spike 批 · F7 落地 + 黄金哈希 golden-v1 种子(ADR-012 §一 单元级第一组)
//
// 权威来源:
//   ADR-012 F7(2026-09-21 承 RC-4 降级)—— SplitMix64 / Fix 中间乘 = ulong 表示,
//     有符号溢出 UB 结构性消解;回绕用例 = 单元级黄金哈希第一条;
//   ADR-006 §Decision 三 —— 全部舍入 ROUND_HALF_AWAY_FROM_ZERO(FixMul 移位步同此,D-2 消解);
//   disease-simulation.md AC-4 —— 手工 hi/lo、全无符号、溢出即测试失败;
//   D-1 用户裁定(2026-09-22)—— 多输入折叠 = 乙案链式 avalanche,形状一经落码即锁。
//
// ⚠️ 期望值全部由【超算】侧 Python 独立参考实现算出(与 C# 实现零共享代码),
//    任一条变红 = 实现漂移或参考漂移,须双向核对,禁就地改期望值(ADR-012 版本化刷新纪律:
//    刷新 = 新 golden-vN + 变更日志,不是改老数)。
// ⚠️ 本文件只覆盖 Mono(EditMode)格。IL2CPP 格对拍走 ADR-012 §一 player bootstrap,
//    归 CI 故事 —— 此处**不借绿**(跨平台逐位性未跑就是未跑)。

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    internal sealed class GoldenHashV1Test
    {
        // ════════ 1. SplitMix64 · Avalanche 定值 ════════

        [TestCase(0x0000000000000000UL, 0x0000000000000000UL)]
        [TestCase(0x0000000000000001UL, 0x5692161D100B05E5UL)]
        [TestCase(0xDEADBEEFCAFEBABEUL, 0x7AD6664F09FFE52CUL)]
        [TestCase(0xFFFFFFFFFFFFFFFFUL, 0xB4D055FCF2CBBD7BUL)]
        [TestCase(0x9E3779B97F4A7C15UL, 0xE220A8397B1DCDAFUL)]
        public void test_splitmix64_avalanche_golden(ulong input, ulong expected)
        {
            Assert.AreEqual(expected, SplitMix64.Avalanche(input),
                $"Avalanche(0x{input:X16}) 漂移 —— 黄金向量 golden-v1");
        }

        // ════════ 2. SplitMix64 · 流形(ADR-007 §三 掷骰消费序号)════════

        [Test]
        public void test_splitmix64_stream_fromZero_golden()
        {
            ulong s = 0UL;
            ulong[] expected =
            {
                0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL,
                0xF88BB8A8724C81ECUL, 0x1B39896A51A8749BUL, 0x53CB9F0C747EA2EAUL,
            };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], SplitMix64.NextValue(ref s), $"流第 {i + 1} 步漂移");
        }

        [Test]
        public void test_splitmix64_stream_wrapAround_golden()
        {
            // F7 回绕用例:state = ulong.MaxValue → +Gamma 定义性回绕 mod 2^64
            // (无符号溢出在 C# / IL2CPP C++ 两侧同为定义性 —— 本断言即该结构性消解的探针)
            ulong s = ulong.MaxValue;
            Assert.AreEqual(0x9E3779B97F4A7C14UL, unchecked(s + SplitMix64.Gamma),
                "ulong 回绕步进漂移");
            Assert.AreEqual(0xE4D971771B652C20UL, SplitMix64.NextValue(ref s),
                "回绕后 avalanche 输出漂移");
        }

        // ════════ 3. SplitMix64 · 折叠形(D-1 乙案)════════

        [TestCase(0x000000000000ABCDL, 0x0000000000001234L, 0x1138E27CC34122C4UL)]
        [TestCase(0x0000000000001234L, 0x000000000000ABCDL, 0x1138E27CC34122C4UL)] // 首两参交换 → 等值(性质)
        public void test_splitmix64_fold2_golden(long a, long b, ulong expected)
        {
            Assert.AreEqual(expected, SplitMix64.Hash(a, b));
        }

        [Test]
        public void test_splitmix64_fold3_noneSentinel_golden()
        {
            // PatientId.None = -1 → 位型转 u64 全 1(ADR-009 §四 哨兵口径)
            Assert.AreEqual(0x7DD035FFF758317AUL, SplitMix64.Hash(0xABCDL, -1L, 5L));
        }

        [Test]
        public void test_splitmix64_fold3_orderMatters_fromThirdInput()
        {
            // 首两参可交换,第三参起有序:(a,b,c) ≠ (a,c,b) 必须为真
            Assert.AreEqual(0x6F71BBBB9313AA49UL, SplitMix64.Hash(0xABCDL, 3L, 11L));
            Assert.AreEqual(0xBF6AC3503A2186E1UL, SplitMix64.Hash(0xABCDL, 11L, 3L));
        }

        [Test]
        public void test_splitmix64_fold4_commutesFirstPairOnly()
        {
            Assert.AreEqual(0x45027AD9FE9AD5D1UL, SplitMix64.Hash(0xABCDL, 7L, 3L, 11L));
            Assert.AreEqual(0x45027AD9FE9AD5D1UL, SplitMix64.Hash(7L, 0xABCDL, 3L, 11L));
        }

        // ════════ 4. SplitMix64 · 加盐形(registry 字面调用形)════════

        [Test]
        public void test_splitmix64_hashTagged_golden()
        {
            // SplitMix64(WorldSeed, "case-salt", ordinal) — entities.yaml PatternRecognized 约束原文
            Assert.AreEqual(0x9104567A4D5A819CUL, SplitMix64.HashTagged(0xABCDL, "case-salt", 3L));
            // SplitMix64(WorldSeed, "gather", node_id, gather_seq) — entities.yaml Gathered 约束原文
            Assert.AreEqual(0x098CB659EB71A7AEUL, SplitMix64.HashTagged(0xABCDL, "gather", 17L, 2L));
            Assert.AreEqual(0x6B111E774C118AEFUL, SplitMix64.HashTagged(0xABCDL, "gather", 17L, 3L));
            // 空 tag 占位不撞:≠ 无 tag 形
            Assert.AreEqual(0xE60F854CB0FB0700UL, SplitMix64.HashTagged(0xABCDL, "", 0x1234L));
            Assert.AreNotEqual(SplitMix64.Hash(0xABCDL, 17L, 2L), SplitMix64.HashTagged(0xABCDL, "gather", 17L, 2L));
            // tag 差异必须敏感到第二个字节块(多块大端补零路径)
            Assert.AreEqual(0x948EFF63279DF9DFUL, SplitMix64.HashTagged(0xABCDL, "gatherer-tag-12345678", 42L));
            Assert.AreNotEqual(SplitMix64.HashTagged(0xABCDL, "gather", 17L, 2L),
                               SplitMix64.HashTagged(0xABCDL, "gatherx", 17L, 2L));
        }

        // ════════ 5. Fix 中间乘(AC-4 · F7 · golden-v1)════════

        [TestCase(65536L, 65536L, 65536L)]                  // 1.0 × 1.0
        [TestCase(-65536L, 65536L, -65536L)]                 // 负 × 正
        [TestCase(-65536L, -65536L, 65536L)]                 // 负 × 负
        [TestCase(49152L, 49152L, 36864L)]                   // 3/4 × 3/4 = 9/16
        [TestCase(21845L, 65536L, 21845L)]                   // 1/3 × 1
        [TestCase(21845L, 21845L, 7282L)]                    // (1/3)² —— 非对齐余数
        [TestCase(0L, -9223372036854775808L, 0L)]            // 零 × long.MinValue
        [TestCase(1L, 1L, 0L)]                               // 1 LSB 自乘 → 截向零
        [TestCase(1L, -1L, 0L)]
        [TestCase(4294967296L, 4294967296L, 281474976710656L)]   // 2^32 × 2^32 → hi 位携带
        [TestCase(2147483647L, 2147483647L, 70368744112128L)]
        [TestCase(-2147483647L, 2147483647L, -70368744112128L)]
        [TestCase(3L, 3L, 0L)]
        [TestCase(32768L, 32768L, 16384L)]                   // 0.5 × 0.5 = 0.25 精确
        [TestCase(32769L, 32769L, 16385L)]
        [TestCase(1000000007L, 65536L, 1000000007L)]         // × 1.0 恒等
        [TestCase(1L, 32768L, 1L)]                           // rem = 0x8000 → half-away 远离零
        [TestCase(-1L, 32768L, -1L)]
        [TestCase(3L, 32768L, 2L)]                           // 3/2 = 1.5 → 2
        [TestCase(5L, 32768L, 3L)]                           // 5/2 = 2.5 → 3(ties-to-even 会得 2)
        [TestCase(-4611686018427387904L, 1L, -70368744177664L)]
        [TestCase(-9223372036854775808L, 2L, -281474976710656L)]  // long.MinValue 量积(补码取负路径)
        [TestCase(9223372036854775807L, 1L, 140737488355328L)]
        [TestCase(-65536L, -1L, 1L)]
        [TestCase(49152L, 32768L, 24576L)]
        public void test_fixmul_golden(long a, long b, long expected)
        {
            Assert.AreEqual(expected, Fix.MulRaw(a, b),
                $"FixMul({a}, {b}) 漂移 —— 黄金向量 golden-v1");
        }

        [TestCase(140737488355328L, 4294967296L)]            // 2^47 × 2^32 → 结果 2^63,正侧不可表示
        [TestCase(1099511627776L, 1099511627776L)]           // 2^40 × 2^40 → 2^64,域外
        [TestCase(-9223372036854775808L, -9223372036854775808L)] // MinValue² → 域外
        public void test_fixmul_domainOverflow_throws(long a, long b)
        {
            // AC-4:「溢出即测试失败」的运行期形态 = 显式异常,非静默回绕
            Assert.Throws<OverflowException>(() => Fix.MulRaw(a, b),
                $"FixMul({a}, {b}) 应抛 OverflowException(域外),不得回绕");
        }

        [Test]
        public void test_fixmul_operator_matchesMulRaw()
        {
            var x = new Fix(49152L);
            var y = new Fix(49152L);
            Assert.AreEqual(Fix.MulRaw(49152L, 49152L), (x * y).Raw);
        }

        // ════════ 6. S7 确定性格内偏移(ADR-023 ⑦ 抽查夹具的哈希半边)════════

        [Test]
        public void test_s7_cellOffset_deterministic_golden()
        {
            // 派生形:Hash(actor, x, y, z) → 三段 u16 作 [0,1) 定点偏移分量。
            // 「无对象落入不可走格」的全夹具依赖关卡逻辑层(ADR-022 C2),归 S7 全夹具轮;
            // 本测只钉**哈希侧逐位确定**(同输入三次重建逐位一致)—— 即 spike 判据的可判半边。
            for (int run = 0; run < 3; run++)
            {
                ulong h1 = SplitMix64.Hash(42L, 1L, 2L, 3L);
                Assert.AreEqual(0xA247916DAFFF57C5UL, h1, $"第 {run + 1} 次重建哈希漂移");
                Assert.AreEqual(0x57C5, (int)(h1 & 0xFFFF));
                Assert.AreEqual(0xAFFF, (int)((h1 >> 16) & 0xFFFF));
                Assert.AreEqual(0x916D, (int)((h1 >> 32) & 0xFFFF));
            }
            Assert.AreNotEqual(SplitMix64.Hash(42L, 1L, 2L, 3L), SplitMix64.Hash(42L, 1L, 2L, 4L),
                "偏移必须对格坐标敏感");
        }
    }
}
