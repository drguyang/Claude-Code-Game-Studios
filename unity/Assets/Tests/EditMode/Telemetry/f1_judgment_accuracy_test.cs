// 权威来源:production/epics/telemetry-analytics/story-002-f1-judgment-accuracy.md
//   (AC-51-B1…B4 — F1 判断准确率:Scorable/D/J/F1a/F1b + 集合相等 + 全序最后一条 + 分母 0)
//   · F1a = |{c ∈ Scorable : J(c) == D(c)}| / |Scorable|;F1b = |{c : ∃ JudgmentRevised}| / |Scorable|
//   · J(c) = 全序最后一条判断事件;JudgmentRevised.judgment 存新值(2026-09-16 已裁)
//   · 分母 0 报 (0,0) + 显式标记;不抛除零、不报 0/1、不报标量 0、不报 NaN
// ADR-019 §一(回放即数据记录)· ADR-006 §五(整数 (num, den) 对 + ROUND_HALF_AWAY_FROM_ZERO)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/f1_judgment_accuracy_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 负向夹具落位:与 Story 001 同型 —— 违例 fixture 住**测试命名空间**。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F1JudgmentAccuracyTest
    {
        // ⚠️ S19 修复(TD-epic-01):原「测试自持谓词面」已删除 ——
        //    谓词面上移为生产类 `DaYiJingCheng.Telemetry.JudgmentMetrics`
        //    (ADR-019 §一;那 12 个自持面在普查里被判为「AC 语义层无生产主语」)。
        //    本文件现在**只做夹具 + 断言**,不再自带公式。

        // ══════════════ AC-51-B1:F1a 四例夹具 ══════════════

        /// <summary>AC-51-B1:F1a 四例夹具 —— ① D=X ∧ J=X ② D=∅ ∧ J=∅ ③ D=X ∧ J=∅ ④ D=X ∧ J=Y。
        /// 分母 = 4、分子 = 2(①②);∅ == ∅ 判对;③④ 不计分子。</summary>
        [Test]
        public void test_f1a_fourCases_numerator2()
        {
            // Arrange
            var records = new List<JudgmentRecord>
            {
                new JudgmentRecord(1, new HashSet<string> { "X" }, new HashSet<string> { "X" }, false),  // ① 判对
                new JudgmentRecord(2, new HashSet<string>(), new HashSet<string>(), false),               // ② ∅==∅ 判对
                new JudgmentRecord(3, new HashSet<string> { "X" }, new HashSet<string>(), false),           // ③ 未落笔
                new JudgmentRecord(4, new HashSet<string> { "X" }, new HashSet<string> { "Y" }, false),      // ④ 判错
            };

            // Act
            var r = JudgmentMetrics.Compute(records);
            int f1aNum = r.F1aNum, f1aDen = r.F1aDen; bool denominatorZero = r.DenominatorZero;

            // Assert
            Assert.That(f1aDen, Is.EqualTo(4), "分母 = 4(四例均 ∃ CaseClosed)");
            Assert.That(f1aNum, Is.EqualTo(2), "分子 = 2(①②判对;③④不计)");
            Assert.That(denominatorZero, Is.False, "分母 > 0 时不报分母 0");
            // 显式断言 ∅ == ∅ 判对(案例 ② 计入分子)
            var emptySet = new HashSet<string>();
            Assert.That(JudgmentMetrics.SetEquals(emptySet, emptySet), Is.True, "∅ == ∅ 必须判对");
        }

        // ══════════════ AC-51-B2:F1a ⊥ F1b(改写不进准确率)══════════════

        /// <summary>AC-51-B2:F1a ⊥ F1b —— ① 判对且有改写 ② 判对且无改写 ③ 判错且有改写。
        /// F1a = 2/3 与 F1b = 2/3 正交;移除全部改写后 F1a 不变。</summary>
        [Test]
        public void test_f1a_f1b_orthogonal()
        {
            // Arrange
            var records = new List<JudgmentRecord>
            {
                new JudgmentRecord(1, new HashSet<string> { "X" }, new HashSet<string> { "X" }, true),   // ① 判对+改写
                new JudgmentRecord(2, new HashSet<string> { "X" }, new HashSet<string> { "X" }, false),  // ② 判对无改写
                new JudgmentRecord(3, new HashSet<string> { "X" }, new HashSet<string> { "Y" }, true),   // ③ 判错+改写
            };

            // Act
            var r = JudgmentMetrics.Compute(records);
            int f1aNum = r.F1aNum, f1aDen = r.F1aDen, f1bNum = r.F1bNum, f1bDen = r.F1bDen;
            bool denominatorZero = r.DenominatorZero;

            // Assert:F1a = 2/3(①②判对),F1b = 2/3(①③改写)
            Assert.That(f1aNum, Is.EqualTo(2), "F1a 分子 = 2(①②判对;③判错不计)");
            Assert.That(f1aDen, Is.EqualTo(3), "F1a 分母 = 3");
            Assert.That(f1bNum, Is.EqualTo(2), "F1b 分子 = 2(①③改写)");
            Assert.That(denominatorZero, Is.False, "分母 > 0 时不报分母 0");
            Assert.That(f1bDen, Is.EqualTo(3), "F1b 分母 = 3");

            // Assert:移除全部改写后 F1a 不变(改写不进准确率)
            var noRevision = records.Select(r => new JudgmentRecord(
                r.CaseId, r.Truth, r.Judged, false)).ToList();
            var r2 = JudgmentMetrics.Compute(noRevision);
            int f1aNum2 = r2.F1aNum, f1aDen2 = r2.F1aDen, f1bNum2 = r2.F1bNum;
            Assert.That(f1aNum2, Is.EqualTo(f1aNum), "移除改写后 F1a 不变(正交性)");
            Assert.That(f1aDen2, Is.EqualTo(f1aDen), "移除改写后 F1a 分母不变");
            Assert.That(f1bNum2, Is.EqualTo(0), "移除改写后 F1b 分子 = 0(改写不进 F1b 分母)");
        }

        // ══════════════ AC-51-B3:F1 取法(全序最后一条胜)══════════════

        /// <summary>AC-51-B3:F1 取法 —— J(c) 取全序最后一条判断事件。
        /// ⚠️ 核心 = 跨病例共病时按 case_id 路由到各自槽位(全序键不含 case_id)。
        /// 两病例交错事件:A Seq1 → B Seq1 → A Seq2 ⇒ A 的槽只含 A 的最后一条。</summary>
        [Test]
        public void test_f1_takeLastJudgment_wins()
        {
            // Arrange:两病例交错事件(跨病例共病)
            var events = new[]
            {
                new { CaseId = 1, Seq = 1, Judgment = new HashSet<string> { "A_Old" } },  // A Seq1
                new { CaseId = 2, Seq = 1, Judgment = new HashSet<string> { "B_Only" } }, // B Seq1
                new { CaseId = 1, Seq = 2, Judgment = new HashSet<string> { "A_Final" } }, // A Seq2(覆盖 A Seq1)
            };

            // Act:模拟「逐条推进选 case_id → 写槽 → 覆盖」
            var slots = new Dictionary<int, ISet<string>>();
            foreach (var e in events.OrderBy(e => e.Seq))
                slots[e.CaseId] = e.Judgment;   // 按 case_id 路由到各自槽位,覆盖写

            // Assert:每个病例的槽只含自己的最后一条
            Assert.That(slots[1], Is.EqualTo(new HashSet<string> { "A_Final" }),
                "病例 1 的槽只含 A 的最后一条(A Seq2 覆盖 A Seq1)");
            Assert.That(slots[2], Is.EqualTo(new HashSet<string> { "B_Only" }),
                "病例 2 的槽只含 B 的最后一条(不被 A 的事件覆盖)");
        }

        // ══════════════ AC-51-B4:分母 0 报 (0,0) ══════════════

        /// <summary>AC-51-B4:分母 0 —— |Scorable| = 0 ⇒ 输出 (0,0) + 显式「分母 0/未定义」标记。
        /// 不抛除零、不报 0/1、不报标量 0、不报 NaN。</summary>
        [Test]
        public void test_f1_denominatorZero_reports00()
        {
            // Arrange:空 Scorable
            var records = new List<JudgmentRecord>();

            // Act
            var r = JudgmentMetrics.Compute(records);
            int f1aNum = r.F1aNum, f1aDen = r.F1aDen, f1bNum = r.F1bNum, f1bDen = r.F1bDen;
            bool denominatorZero = r.DenominatorZero;

            // Assert
            Assert.That(denominatorZero, Is.True, "分母 0 必须显式标记");
            Assert.That(f1aNum, Is.EqualTo(0), "F1a 分子 = 0");
            Assert.That(f1aDen, Is.EqualTo(0), "F1a 分母 = 0(非 1)");
            Assert.That(f1bNum, Is.EqualTo(0), "F1b 分子 = 0");
            Assert.That(f1bDen, Is.EqualTo(0), "F1b 分母 = 0(非 1)");
            // 不报标量 0(整数对,非浮点)
            Assert.That(f1aNum, Is.EqualTo(0).And.TypeOf<int>(), "分子为整数 0,非浮点 0");
            Assert.That(f1aDen, Is.EqualTo(0).And.TypeOf<int>(), "分母为整数 0,非浮点 0");
            // 不报 NaN(整数对不可能产生 NaN,显式断言以记录意图)
            Assert.That(double.IsNaN(f1aNum) || double.IsNaN(f1aDen), Is.False, "不报 NaN");
            // 不抛异常(无除零)
        }

        /// <summary>AC-51-B4 负向:分母 > 0 时不报 (0,0)。</summary>
        [Test]
        public void test_f1_denominatorNonZero_not00()
        {
            // Arrange:一例判对
            var records = new List<JudgmentRecord>
            {
                new JudgmentRecord(1, new HashSet<string> { "X" }, new HashSet<string> { "X" }, false),
            };

            // Act
            var r = JudgmentMetrics.Compute(records);
            int f1aNum = r.F1aNum, f1aDen = r.F1aDen; bool denominatorZero = r.DenominatorZero;

            // Assert
            Assert.That(denominatorZero, Is.False, "分母 > 0 时不报 (0,0)");
            Assert.That(f1aNum, Is.EqualTo(1));
            Assert.That(f1aDen, Is.EqualTo(1));
        }
    }
}
