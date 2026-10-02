// emergency-procedures Story 002 测试
//
// DC-1: half_life_ticks >= 1
// DC-2: 1 <= mag_threshold <= MAG_MAX
// DC-3: jitter_relax_mul >= MUL_ONE（熟练度表）
// DC-4: action_id 闭集 = EmergencyAction 枚举全值（OQ-10-6 已裁决: 归系统 10）
// DC-5: Kind 白名单含三 Kind（引用 disease-simulation story 002/003 的 kindgen 断言）
// result_mul 三档: 按 JudgeResult 序数索引 {16384, 32768, 65536}
// F-10.2 形状: MAG_CAP(L) 档位表
// 表外字段零泄漏
//
// ⚠️ DC-4 NOT-RUN 注记: 枚举内容（P0 = 2 动作）的验证依赖 OQ-10-4 冻结清单，
//    本测试只验证机制（Enum.IsDefined），不验证枚举内容。
// ⚠️ DC-5 NOT-RUN 注记: kindgen 差集断言归 disease-simulation story 002/003，
//    本测试只验证白名单字符串存在，不验证构建期联动。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class ActionTablesBakeTest
    {
        // ══════════ DC-1: half_life_ticks >= 1 ══════════

        [Test]
        public void test_dc1_halfLifeTicks_zeroFails()
        {
            var row = new EmergencyActionRow { ActionId = 0, HalfLifeTicks = 0 };
            Assert.IsFalse(EmergencyActionSchema.ValidateHalfLifeTicks(row),
                "half_life_ticks=0 应构建失败");
        }

        [Test]
        public void test_dc1_halfLifeTicks_positivePasses()
        {
            var row = new EmergencyActionRow { ActionId = 0, HalfLifeTicks = 1 };
            Assert.IsTrue(EmergencyActionSchema.ValidateHalfLifeTicks(row),
                "half_life_ticks=1 应通过");
        }

        // ══════════ DC-2: 1 <= mag_threshold <= MAG_MAX ══════════

        [Test]
        public void test_dc2_magThreshold_belowMinFails()
        {
            var row = new EmergencyActionRow { ActionId = 0, MagThreshold = 0 };
            Assert.IsFalse(EmergencyActionSchema.ValidateMagThreshold(row, 1000),
                "mag_threshold=0 应构建失败");
        }

        [Test]
        public void test_dc2_magThreshold_aboveMaxFails()
        {
            var row = new EmergencyActionRow { ActionId = 0, MagThreshold = 1500 };
            Assert.IsFalse(EmergencyActionSchema.ValidateMagThreshold(row, 1000),
                "mag_threshold=1500 > MAG_MAX=1000 应构建失败");
        }

        [Test]
        public void test_dc2_magThreshold_withinBoundsPasses()
        {
            var row = new EmergencyActionRow { ActionId = 0, MagThreshold = 500 };
            Assert.IsTrue(EmergencyActionSchema.ValidateMagThreshold(row, 1000),
                "mag_threshold=500 应在 [1, 1000] 内");
        }

        // ══════════ DC-3: jitter_relax_mul >= MUL_ONE（熟练度表） ══════════

        [Test]
        public void test_dc3_jitterRelaxMul_belowOneFails()
        {
            var row = new EmergencySkillRow { Level = 1, JitterRelaxMul = 32768 }; // 0.5
            Assert.IsFalse(EmergencyActionSchema.ValidateJitterRelaxMul(row),
                "jitter_relax_mul=0.5 < MUL_ONE=1.0 应构建失败");
        }

        [Test]
        public void test_dc3_jitterRelaxMul_atOnePasses()
        {
            var row = new EmergencySkillRow { Level = 1, JitterRelaxMul = 65536 }; // 1.0
            Assert.IsTrue(EmergencyActionSchema.ValidateJitterRelaxMul(row),
                "jitter_relax_mul=1.0 应通过");
        }

        // ══════════ DC-4: action_id 闭集 = EmergencyAction 枚举全值 ══════════

        [Test]
        public void test_dc4_actionId_inEnumPasses()
        {
            foreach (EmergencyAction action in Enum.GetValues(typeof(EmergencyAction)))
            {
                Assert.IsTrue(EmergencyActionSchema.ValidateActionId((int)action),
                    $"action_id={(int)action} ({action}) 应在枚举内");
            }
        }

        [Test]
        public void test_dc4_actionId_outOfEnumFails()
        {
            int invalidId = 999;
            Assert.IsFalse(EmergencyActionSchema.ValidateActionId(invalidId),
                $"action_id={invalidId} 不在枚举内应构建失败");
        }

        [Test]
        public void test_dc4_enumContent_p0TwoActions()
        {
            // OQ-10-4 冻结: P0 = 2 个急救动作
            var values = Enum.GetValues(typeof(EmergencyAction));
            Assert.AreEqual(2, values.Length, "P0 应恰有 2 个急救动作");
            Assert.IsTrue(Enum.IsDefined(typeof(EmergencyAction), "HemostasisBandage"),
                "应含 止血包扎");
            Assert.IsTrue(Enum.IsDefined(typeof(EmergencyAction), "RhythmVentilation"),
                "应含 节奏型通气");
        }

        // ══════════ DC-5: Kind 白名单含三 Kind ══════════

        [Test]
        public void test_dc5_kindWhitelist_containsThreeKinds()
        {
            var kinds = EmergencyActionSchema.GetRequiredKindWhitelist();
            Assert.IsTrue(kinds.Contains("EmergencyAttempt"), "白名单应含 EmergencyAttempt");
            Assert.IsTrue(kinds.Contains("EmergencyTreatmentApplied"), "白名单应含 EmergencyTreatmentApplied");
            Assert.IsTrue(kinds.Contains("DrugTreatmentApplied"), "白名单应含 DrugTreatmentApplied");
        }

        // ══════════ result_mul 三档（按 JudgeResult 序数索引） ══════════

        [Test]
        public void test_resultMul_threeTiersExact()
        {
            var expected = new long[] { 16384, 32768, 65536 };
            var actual = EmergencyActionSchema.GetResultMulTiers();
            Assert.AreEqual(expected, actual, "result_mul 应按 JudgeResult 序数索引为 {16384, 32768, 65536}");
        }

        [Test]
        public void test_resultMul_indexedByJudgeResult()
        {
            var tiers = EmergencyActionSchema.GetResultMulTiers();
            Assert.AreEqual(16384L, tiers[(int)JudgeResult.Missed], "Missed 档应为 16384 (0.25)");
            Assert.AreEqual(32768L, tiers[(int)JudgeResult.AppliedWeak], "AppliedWeak 档应为 32768 (0.5)");
            Assert.AreEqual(65536L, tiers[(int)JudgeResult.Applied], "Applied 档应为 65536 (1.0)");
        }

        // ══════════ DC-6: result_mul 恰三档(2026-10-03 补)══════════

        [Test]
        public void test_dc6_resultMul_exactlyThreeTiers()
        {
            var ok = new EmergencyActionRow { ActionId = 0, ResultMul = EmergencyActionSchema.GetResultMulTiers() };
            Assert.IsTrue(EmergencyActionSchema.ValidateResultMul(ok), "三档 ⇒ 过");
        }

        [Test]
        public void test_dc6_resultMul_null_rejected()
        {
            // 🔴 这是实测暴露的形态:漏填 result_mul 的 row 此前能过全部烘焙门,
            //    直到 Process 读 ResultMul[(int)result] NRE(PlayMode 两例即此形态)
            var bad = new EmergencyActionRow { ActionId = 0, ResultMul = null };
            Assert.IsFalse(EmergencyActionSchema.ValidateResultMul(bad),
                "null ⇒ 拒(DC-6);否则 NRE 延后到运行期");
        }

        [Test]
        public void test_dc6_resultMul_wrongArity_rejected()
        {
            Assert.IsFalse(EmergencyActionSchema.ValidateResultMul(
                new EmergencyActionRow { ActionId = 0, ResultMul = new long[] { 65536L, 65536L } }),
                "两档 ⇒ 拒(Applied=2 会越界)");
            Assert.IsFalse(EmergencyActionSchema.ValidateResultMul(
                new EmergencyActionRow { ActionId = 0, ResultMul = new long[4] }),
                "四档 ⇒ 拒(超出 JudgeResult 闭集)");
            Assert.IsFalse(EmergencyActionSchema.ValidateResultMul(null), "null row ⇒ 拒");
        }

        [Test]
        public void test_resultMul_thirdTierNonZero()
        {
            var tiers = EmergencyActionSchema.GetResultMulTiers();
            Assert.AreEqual(3, tiers.Length, "result_mul 应恰三档");
            Assert.AreNotEqual(0, tiers[2], "第三档应 ≠ 0");
        }

        // ══════════ F-10.2 形状: MAG_CAP(L) 档位表 ══════════

        [Test]
        public void test_f102_magCapTable_exists()
        {
            var table = EmergencyActionSchema.GetMagCapTable();
            Assert.IsNotNull(table, "MAG_CAP(L) 档位表应存在");
            Assert.IsNotEmpty(table, "MAG_CAP(L) 档位表应非空");
        }

        [Test]
        public void test_f102_magCapTable_allowsAllSame()
        {
            // P0 效应关闭，允许全档相同
            var table = EmergencyActionSchema.GetMagCapTable();
            bool allSame = table.All(v => v == table[0]);
            Assert.IsTrue(allSame, "P0 应允许全档相同");
        }

        // ══════════ 表外字段零泄漏 ══════════

        [Test]
        public void test_noExtraFields_inActionRow()
        {
            var expectedFields = new[] { "ActionId", "Polarity", "BasePotency", "MagThreshold", "MinEdges", "MinHoldTicks", "HalfLifeTicks", "DurationTicks", "ResultMul" };
            var actualFields = typeof(EmergencyActionRow)
                .GetFields()
                .Select(f => f.Name)
                .OrderBy(n => n)
                .ToArray();
            Assert.AreEqual(expectedFields.OrderBy(n => n).ToArray(), actualFields,
                "EmergencyActionRow 字段集应恰为预期");
        }

        [Test]
        public void test_noExtraFields_inSkillRow()
        {
            var expectedFields = new[] { "Level", "JitterRelaxMul", "MagCapMul" };
            var actualFields = typeof(EmergencySkillRow)
                .GetFields()
                .Select(f => f.Name)
                .OrderBy(n => n)
                .ToArray();
            Assert.AreEqual(expectedFields.OrderBy(n => n).ToArray(), actualFields,
                "EmergencySkillRow 字段集应恰为预期");
        }

        // ══════════ 字符串 Fix 解析 ══════════

        [Test]
        public void test_fixParse_stringFraction()
        {
            Assert.AreEqual(49152L, FixParse.Parse("3/4").Raw, "\"3/4\" 应解析为 49152");
            Assert.AreEqual(65536L, FixParse.Parse("1/1").Raw, "\"1/1\" 应解析为 65536");
            Assert.AreEqual(32768L, FixParse.Parse("1/2").Raw, "\"1/2\" 应解析为 32768");
        }

        [Test]
        public void test_fixParse_decimalString()
        {
            // ADR-014: 浮点字面量被拒，只允许整数或 分子/分母
            Assert.Throws<FormatException>(() => FixParse.Parse("0.75"));
        }

        [Test]
        public void test_fixParse_invalidThrows()
        {
            Assert.Throws<FormatException>(() => FixParse.Parse("abc"));
            Assert.Throws<FormatException>(() => FixParse.Parse(""));
        }
    }
}
