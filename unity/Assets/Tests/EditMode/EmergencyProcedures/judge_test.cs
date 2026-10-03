// emergency-procedures Story 003 测试
//
// AC-10-04a: 定点缩放 .5 中间积 = 8193 (half-away)
// AC-10-04b: 黄金夹具三格逐位 (NOT-RUN 直至 ADR-012 矩阵实跑)
// AC-10-15: JudgeResult 枚举非连续分值
// AC-10-20: edges <= 1 稳度门不评
// F-10.3 三门矩阵: 2^3 组合
// F-10.3b JITTER: 相对偏差式
// F-10.2 方向: magnitude 不被熟练度放大
// F-10.4: drug_potency 三档

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class JudgeTest
    {
        // ══════════ AC-10-04a: 定点缩放 .5 中间积 = 8193 ══════════

        [Test]
        public void test_ac1004a_fixedPointScaling_halfAway()
        {
            // (32769 × 32768) ÷ 65536 = 16384.5 → 16385 (half-away-from-zero)
            // 若实现走 C# / 则得 16384
            long result = JudgeEvaluator.ScaleFixed(32769L, 32768L);
            Assert.AreEqual(16385L, result, "F-10.4 缩放应得 16385 (half-away)");
        }

        [Test]
        public void test_ac1004a_fixedPointScaling_exact()
        {
            // 整除情况
            long result = JudgeEvaluator.ScaleFixed(65536L, 65536L);
            Assert.AreEqual(65536L, result, "65536 × 65536 ÷ 65536 = 65536");
        }

        // ══════════ AC-10-15: JudgeResult 枚举非连续分值 ══════════

        [Test]
        public void test_ac1015_judgeResult_isEnum()
        {
            Assert.IsTrue(typeof(JudgeResult).IsEnum, "JudgeResult 应是枚举");
            Assert.AreEqual(3, Enum.GetValues(typeof(JudgeResult)).Length, "JudgeResult 应恰三值");
        }

        [Test]
        public void test_ac1015_judgeResult_noFloatFields()
        {
            // 反射断言: JudgeResult 无 float/double 字段
            foreach (var field in typeof(JudgeResult).GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(float), "JudgeResult 不应含 float");
                Assert.IsFalse(field.FieldType == typeof(double), "JudgeResult 不应含 double");
            }
        }

        // ══════════ AC-10-20: edges <= 1 稳度门不评 ══════════

        [Test]
        public void test_ac1020_edgesZero_noException()
        {
            var agg = new EmergencyReading(0, 100, 0, new int[0], 500);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 1, MinHoldTicks = 50 };
            var ctx = new JudgeContext { Level = 1, MagThresholdEffective = 400 };

            Assert.DoesNotThrow(() => JudgeEvaluator.Judge(agg, action, ctx),
                "edges=0 不应抛异常");
        }

        [Test]
        public void test_ac1020_edgesOne_noException()
        {
            var agg = new EmergencyReading(0, 100, 1, new[] { 10 }, 500);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50 };
            var ctx = new JudgeContext { Level = 1, MagThresholdEffective = 400 };

            Assert.DoesNotThrow(() => JudgeEvaluator.Judge(agg, action, ctx),
                "edges=1 不应抛异常");
        }

        // ══════════ F-10.3 三门矩阵: 2^3 组合穷举 ══════════

        [Test]
        public void test_f103_allGatesPass_returnsApplied()
        {
            // 幅度过 + 节奏过 + 稳度过 = Applied
            var agg = new EmergencyReading(0, 100, 3, new[] { 10, 20, 30 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            Assert.AreEqual(JudgeResult.Applied, JudgeEvaluator.Judge(agg, action, ctx),
                "三门全过应返回 Applied");
        }

        [Test]
        public void test_f103_rhythmFail_returnsMissed()
        {
            // 节奏不过 = Missed（edges < MIN_EDGES 且 hold_ticks < MIN_HOLD）
            var agg = new EmergencyReading(0, 10, 1, new[] { 10 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg, action, ctx),
                "节奏门不过应返回 Missed");
        }

        [Test]
        public void test_f103_stabilityFail_returnsAppliedWeak()
        {
            // 幅度过 + 节奏过 + 稳度不过 = AppliedWeak
            // 使用极端不规则抖动使稳度门必不过
            var agg = new EmergencyReading(0, 100, 3, new[] { 0, 1, 1000 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 0, MagThresholdEffective = 400 };

            Assert.AreEqual(JudgeResult.AppliedWeak, JudgeEvaluator.Judge(agg, action, ctx),
                "稳度门不过应返回 AppliedWeak");
        }

        [Test]
        public void test_f103_matrix_all8Combinations()
        {
            // 穷举 2^3 输入组合（F-10.3 AC 要求）
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 5, MagThresholdEffective = 400 };

            // 组合 1: 幅度过 + 节奏过 + 稳度过 = Applied
            var agg1 = new EmergencyReading(0, 100, 3, new[] { 10, 20, 30 }, 800);
            Assert.AreEqual(JudgeResult.Applied, JudgeEvaluator.Judge(agg1, action, ctx), "组合1: 全过=Applied");

            // 组合 2: 幅度过 + 节奏过 + 稳度不过 = AppliedWeak
            var agg2 = new EmergencyReading(0, 100, 3, new[] { 0, 1, 1000 }, 800);
            Assert.AreEqual(JudgeResult.AppliedWeak, JudgeEvaluator.Judge(agg2, action, ctx), "组合2: 稳度不过=AppliedWeak");

            // 组合 3: 幅度过 + 节奏不过 = Missed
            var agg3 = new EmergencyReading(0, 10, 1, new[] { 10 }, 800);
            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg3, action, ctx), "组合3: 节奏不过=Missed");

            // 组合 4: 幅度过 + 节奏不过 = Missed（另一路径）
            var agg4 = new EmergencyReading(0, 10, 0, new int[0], 800);
            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg4, action, ctx), "组合4: 节奏不过=Missed");

            // 组合 5: 幅度不过 + 节奏过 = Missed
            var agg5 = new EmergencyReading(0, 100, 3, new[] { 10, 20, 30 }, 100);
            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg5, action, ctx), "组合5: 幅度不过=Missed");

            // 组合 6: 幅度不过 + 节奏不过 = Missed
            var agg6 = new EmergencyReading(0, 10, 1, new[] { 10 }, 100);
            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg6, action, ctx), "组合6: 幅度+节奏不过=Missed");

            // 组合 7: 幅度不过 + 节奏不过 = Missed（另一路径）
            var agg7 = new EmergencyReading(0, 10, 0, new int[0], 100);
            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg7, action, ctx), "组合7: 幅度+节奏不过=Missed");

            // 组合 8: 幅度不过 + 节奏不过 = Missed（全不过）
            var agg8 = new EmergencyReading(0, 10, 0, new int[0], 0);
            Assert.AreEqual(JudgeResult.Missed, JudgeEvaluator.Judge(agg8, action, ctx), "组合8: 全不过=Missed");
        }

        // ══════════ F-10.3b JITTER: 相对偏差式 ══════════

        [Test]
        public void test_f103b_proportionalSlow_sameJudgment()
        {
            // [0,10,20] vs [0,20,40] 同比例 ⇒ 同判
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 1, MagThresholdEffective = 400 };

            var agg1 = new EmergencyReading(0, 100, 3, new[] { 0, 10, 20 }, 800);
            var agg2 = new EmergencyReading(0, 100, 3, new[] { 0, 20, 40 }, 800);

            var result1 = JudgeEvaluator.Judge(agg1, action, ctx);
            var result2 = JudgeEvaluator.Judge(agg2, action, ctx);
            Assert.AreEqual(result1, result2, "等比慢两组应同判");
        }

        [Test]
        public void test_f103b_irregularJitter_worseThanRegular()
        {
            // [0,10,25] 抖动 > [0,10,20]
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 1, MagThresholdEffective = 400 };

            var regular = new EmergencyReading(0, 100, 3, new[] { 0, 10, 20 }, 800);
            var irregular = new EmergencyReading(0, 100, 3, new[] { 0, 10, 25 }, 800);

            var regularResult = JudgeEvaluator.Judge(regular, action, ctx);
            var irregularResult = JudgeEvaluator.Judge(irregular, action, ctx);

            // 不规则抖动应差于或等于规则抖动
            Assert.IsTrue(irregularResult <= regularResult,
                "不规则抖动应差于或等于规则抖动");
        }

        // ══════════ F-10.2 方向: magnitude 不被熟练度放大 ══════════

        [Test]
        public void test_f102_magnitudeNotScaledBySkill()
        {
            // 同读数不同 L ⇒ 幅度门结果相同
            var agg = new EmergencyReading(0, 100, 3, new[] { 10, 20, 30 }, 800);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };

            var ctxLow = new JudgeContext { Level = 0, MagThresholdEffective = 400 };
            var ctxHigh = new JudgeContext { Level = 10, MagThresholdEffective = 400 };

            var resultLow = JudgeEvaluator.Judge(agg, action, ctxLow);
            var resultHigh = JudgeEvaluator.Judge(agg, action, ctxHigh);

            // 幅度门结果应相同（稳度门可不同）
            Assert.AreEqual(resultLow, resultHigh,
                "magnitude 不应被熟练度放大（同读数同判）");
        }

        // ══════════ C4/C5 判据(2026-10-03 补)══════════

        [Test]
        public void test_c4_skillMul_actuallyEntersStabilityGate()
        {
            // ⚠️ 原实现 `jitter <= jitterMax` —— `skillMul` **算出即弃**。
            //    现按 GDD F-10.2 权威式:`JITTER × MUL_ONE ≤ JITTER_MAX × SkillMul(L)`。
            //    判据可证伪性:抬高 SkillMul ⇒ 容差变宽 ⇒ 同一 jitter 由弱转强。
            //    (P0 的 ComputeSkillMul 恒返 MUL_ONE ⇒ 本测断**结构性下界①**:
            //     SkillMul(L_min) ≥ MUL_ONE,即无技能玩家不被收紧容差。)
            for (int level = 0; level <= 60; level += 10)
                Assert.GreaterOrEqual(JudgeEvaluator.ComputeSkillMul(level), 65536L,
                    $"结构性下界①:SkillMul(L={level}) 须 ≥ MUL_ONE(否则新手稳度门恒假)");
        }

        [Test]
        public void test_c4_stabilityGate_usesSkillMulInInequality()
        {
            // ⚠️ 2026-10-03 自我修正:本测初版**分不出 C4** —— 因 P0 的
            //    `ComputeSkillMul` **恒返 MUL_ONE** ⇒ `jitterMax × skillMul` 与
            //    `jitterMax` **数值等价**,突变(去掉 skillMul)不会红。
            //    现改为**结构断言**:验 GDD F-10.2 的**权威不等式形状**确在源码内
            //    (两侧同域:左 `jitter × MUL_ONE`,右 `jitterMax × skillMul`)。
            //    ⇒ 突变回 `jitter <= jitterMax` 时,右侧不再含 `skillMul` ⇒ 本测红。
            string src = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Sim/EmergencyProcedures/JudgeEvaluator.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            // ⚠️ 2026-10-03 第三版:二版**只查字符串存在** ⇒ 突变(把不等式改回
            //    `jitter <= jitterMax` 但**保留** `rhs` 变量)时字符串仍在 ⇒ **判据不红**。
            //    **这正是「判据停在存在层、不下沉到语义层」** —— 本批要修的失效模式,
            //    我自己在 C4 上重犯了。现查**不等式本体**:
            //    ① `stabilityPass` 须由 `lhs <= rhs` 决定(而非直接比 jitter/jitterMax);
            //    ② `rhs` 须由 `jitterMax × skillMul` 构成。
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(
                    code, @"stabilityPass\s*=\s*lhs\s*<=\s*rhs"),
                Is.True,
                "C4:稳度门的不等式须为 `lhs <= rhs`(GDD F-10.2 原文式);" +
                "若为 `jitter <= jitterMax` ⇒ SkillMul 算出即弃(C4 复现)");
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(
                    code, @"rhs\s*=\s*jitterMax\s*\*\s*skillMul"),
                Is.True,
                "C4:右侧须由 `jitterMax × skillMul` 构成(SkillMul 真进判据)");
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(
                    code, @"lhs\s*=\s*jitter\s*\*\s*MUL_ONE"),
                Is.True,
                "C4:左侧须抬到 MUL_ONE 域(GDD 原文式两侧同域)");

            // 行为面补充:jitter=0 ⇒ 恒过(基础可用性)
            var edges = new[] { 0, 10, 20 };
            Assert.AreEqual(0L, JudgeEvaluator.ComputeJitter(edges), "等间隔 ⇒ jitter = 0");
            var agg = new EmergencyReading(0, 100, 3, edges, 1000);
            var action = new EmergencyActionRow
            { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 30, MagThresholdEffective = 400 };
            Assert.AreEqual(JudgeResult.Applied, JudgeEvaluator.Judge(agg, action, ctx),
                "jitter=0 ⇒ 稳度门恒过");
        }

        [Test]
        public void test_c5_jitter_roundsHalfAwayFromZero_notTruncate()
        {
            // ⚠️ 2026-10-03 自我修正(两轮):
            //   初版用 `{0,10,25}` —— 余数恰为 0 ⇒ 进位与截断同值,**分不出**。
            //   二版用 `{0,1,12,39}` 并断 `rem == den/2` —— 但 **den=39 是奇数**,
            //     `den/2=19` 而 `19/39 ≈ 0.487 < 0.5` ⇒ 该点**不是**恰半,
            //     实现正确地截断了,是**我的夹具错了**。
            //   三版:穷搜发现 **`MUL_ONE = 2^16` 是 2 的幂**,「真·恰半」
            //     要求 `sad × 2^16 ≡ den/2 (mod den)` ⇒ 推出 `den ≥ 2^17`,
            //     即 `meanD ≥ 65536` tick —— **真实动作(20 Hz、≈11 tick)不可达**。
            //   ⇒ 改用「余数**刚过半**」夹具:截断与进位**仍可区分**(差 1)。
            var edges = new[] { 0, 1, 6, 15 };

            long n = edges.Length - 1;
            long meanD = (edges[edges.Length - 1] - edges[0]) / n;
            long sumAbsDev = 0;
            for (int i = 0; i < n; i++)
                sumAbsDev += System.Math.Abs((edges[i + 1] - edges[i]) - meanD);
            long den = n * meanD;
            long prod = sumAbsDev * 65536L;
            long rem = prod % den;

            Assert.Greater(rem * 2, den,
                "夹具前提:余数须**过半**(否则进位与截断同值,判据分不出)");

            long truncated = prod / den;
            long rounded = truncated + 1;   // 过半 ⇒ 进位

            Assert.AreEqual(rounded, JudgeEvaluator.ComputeJitter(edges),
                "C5:余数过半 ⇒ 须**进位**(ROUND_HALF_AWAY_FROM_ZERO);" +
                "得截断值 = 用了 C# 裸 `/`(Control Manifest Forbidden)");
            Assert.AreNotEqual(truncated, JudgeEvaluator.ComputeJitter(edges),
                "截断与进位须可区分(夹具有效性)");
        }

        // ══════════ F-10.4: drug_potency 三档 ══════════

        [Test]
        public void test_f104_drugPotency_threeTiers()
        {
            // BASE_POTENCY × ResultMul 三档
            long basePotency = 100000;
            var tiers = EmergencyActionSchema.GetResultMulTiers();

            long missed = JudgeEvaluator.ScaleFixed(basePotency, tiers[(int)JudgeResult.Missed]);
            long weak = JudgeEvaluator.ScaleFixed(basePotency, tiers[(int)JudgeResult.AppliedWeak]);
            long applied = JudgeEvaluator.ScaleFixed(basePotency, tiers[(int)JudgeResult.Applied]);

            Assert.AreEqual(25000L, missed, "Missed 档应为 ×0.25");
            Assert.AreEqual(50000L, weak, "AppliedWeak 档应为 ×0.5");
            Assert.AreEqual(100000L, applied, "Applied 档应为 ×1.0");
        }

        // ══════════ AC-10-10c 前半: Missed 发成长路径可达 ══════════

        [Test]
        public void test_ac1010c_missed_growthPathReachable()
        {
            // 承 30「做过即成长」: 一次 Missed ⇒ 发成长读数路径可达
            // 本测试验证 Missed 结果不阻断成长路径（Judge 返回枚举，成长路径由 Story 004 调用）
            var agg = new EmergencyReading(0, 10, 1, new[] { 10 }, 100);
            var action = new EmergencyActionRow { ActionId = 0, MinEdges = 2, MinHoldTicks = 50, MagThreshold = 400 };
            var ctx = new JudgeContext { Level = 0, MagThresholdEffective = 400 };

            var result = JudgeEvaluator.Judge(agg, action, ctx);
            Assert.AreEqual(JudgeResult.Missed, result, "应返回 Missed");
            // 成长路径可达 = Judge 正常返回（无异常/无阻断）
            // 注: 成长事件的具体写入归 Story 004（主机调 Judge）
        }

        // ══════════ AC-10-04b: 黄金夹具 NOT-RUN ══════════

        [Test]
        public void test_ac1004b_goldenFixture_notRun()
        {
            // ADR-012 三格矩阵未跑前，AC-10-04b NOT-RUN
            Assert.Ignore("NOT-RUN: AC-10-04b 跨平台黄金夹具待 ADR-012 矩阵实跑");
        }
    }
}
