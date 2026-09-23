// 权威来源:
//   Story 003(production/epics/item-database/story-003-recipe-settlement-solver.md)
//     · AC-21a-1 —— OutputQty_i = max(1, Round(outputs_i.qty × QtyMultiplier)) ≥ 1 结构性非零
//     · AC-21a-2 —— OutputQuality ≤ InputQuality(clamp 上界 = InputQuality 非 MAX_QUALITY)
//     · AC-21a-3 —— 常量表自洽性 Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1
//     · AC-21a-4 —— ΣM 施加顺序无关,结果 Fix raw 逐位相同
//     · AC-21a-47 —— 单次 F1+F2 求解 < 1 ms(ADVISORY 冒烟,不入 BLOCKING 确定性套件)
//   GDD:design/gdd/item-database.md §Formulas F1(:468-578)/ F2(:582-620)· §Tuning Knobs(:892-949)
//   ADR-005(主):确定性模拟 —— 纯函数、整数定点域、可逐位重放
//   ADR-005 Amendment G:128 位中间结果唯一类型 = 手工 hi/lo 两 ulong(本文件不重造,经 Fix.MulRaw)
//   ADR-006(次):单一舍入 ROUND_HALF_AWAY_FROM_ZERO、守恒律整数域求值
//   ADR-025 §①:纯 sim 逻辑 → 装配 `Sim`(引用集 {BCL, Sim.Contracts})
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/recipe_settlement_solver_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001/002 同一先例;
//    账本与夹具住 tests/unit/item_database/,README 已记落点表)。
//
// ⚠️ 范围(Out of Scope 硬边界):F4 堆叠重量 / F5 品级→时间轴归 Story 004;
//    构建期守恒律门(AC-8/39/40/56/65)归 Story 005(本文件只测 AC-3 的**常量表自洽性**);
//    跨平台黄金哈希(AC-28/29)与 F1–F5 全量静态扫描(AC-30)归 Story 011;
//    ActualConsumed 写事件流归 Story 009(本文件只测实耗的**计算**);
//    跨系统「同参数集走同一函数」(AC-5/6)归 PlayMode 集成文件。
//
// ⚠️ 数值纪律:以下常量**全部是测试夹具值,不是游戏平衡值**(GDD §Tuning Knobs「默认」列留空,
//    数值待用户)。夹具取**数学上可判定**的小值,使边界可精确断言。生产代码零字面量(AC-21a-48)。
//
// 测试纪律:test_[scenario]_[expected];无随机种子(枚举式遍历,非 RNG)、无时间依赖;
//    无 external I/O;边界值字面量按 coding-standards 例外。

using System;
using System.Diagnostics;
using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class RecipeSettlementSolverTest
    {
        // ══════════════ 常量表夹具(数值待用户 —— 此处为测试夹具值)══════════════
        // Given 用合成小值:每种修正 = 1/10,3 项 cap 和 = 3/10,加 ENV_MOD_MAX 1/2 = 8/10,
        // 而 QTY_MULT_MAX = 2 ⇒ 2 − 1 = 1 ≥ 8/10 ⇒ 自洽(AC-3 边界可精确断言)。
        private const int FixtureMaxQuality = 5;
        private const int FixtureSkillCap = 60;

        /// <summary>自洽夹具(caps 各 1/10 · ENV_MOD_MAX 1/2 · QTY_MULT_MAX 2 ⇒ 左侧 9/10 ≤ 右侧 1)。</summary>
        private static RecipeSettlementConstants fixture() => new RecipeSettlementConstants(
            qtyMultMin: FixParse.Parse("1/4"),
            qtyMultMax: FixParse.Parse("2"),
            skillModCap: FixParse.Parse("1/10"),
            qualModCap: FixParse.Parse("1/10"),
            equipModCap: FixParse.Parse("1/10"),
            envModMin: FixParse.Parse("-1"),
            envModMax: FixParse.Parse("1/2"),
            retainMin: FixParse.Parse("1/2"),
            retainMax: FixParse.Parse("1"),
            effMin: FixParse.Parse("1/2"),
            effMax: FixParse.Parse("1"),
            maxQuality: FixtureMaxQuality,
            skillCap: FixtureSkillCap);

        private static RecipeEntry[] outputs(params int[] qtys)
        {
            var entries = new RecipeEntry[qtys.Length];
            for (int i = 0; i < qtys.Length; i++)
                entries[i] = new RecipeEntry(new ItemKey("out_" + i, ProcessingState.Raw), qtys[i]);
            return entries;
        }

        private static RecipeEntry[] inputs(params int[] qtys)
        {
            var entries = new RecipeEntry[qtys.Length];
            for (int i = 0; i < qtys.Length; i++)
                entries[i] = new RecipeEntry(new ItemKey("in_" + i, ProcessingState.Raw), qtys[i]);
            return entries;
        }

        // ══════════════ AC-21a-1:产出非零地板 ══════════════

        [Test]
        public void test_outputQty_extremeNegativeModifiers_everyOutputAtLeastOne()
        {
            // Given:全部修正取最小 + ENV_MOD 停在 ENV_MOD_MIN 的**极端负**组合
            RecipeSettlementConstants c = fixture();
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs(1, 2, 3),
                inputs(1),
                craftSkillLevel: 0,
                inputQuality: 1,
                equipMod: FixParse.Parse("-10"),           // 未钳制入参 ⇒ 拉至 QTY_MULT_MIN
                envModClimate: FixParse.Parse("-10"),
                envModClinic: FixParse.Parse("-10"));

            // When:F1 逐条求解
            RecipeSettlementResult result = RecipeSettlementSolver.Solve(request, c);

            // Then:每条 ≥ 1(max(1,·) 地板生效),且乘子被钳在 QTY_MULT_MIN
            Assert.That(result.QtyMultiplier.Raw, Is.EqualTo(c.QtyMultMin.Raw),
                "极端负 ΣM ⇒ QtyMultiplier 必须被钳至 QTY_MULT_MIN(否则测试未触到边界)");
            foreach (int qty in result.OutputQty)
                Assert.That(qty, Is.GreaterThanOrEqualTo(1), "产出零 = 结构性不可能(AC-21a-1)");
        }

        [Test]
        public void test_outputQty_baseOneWithQtyMultMinRoundingToZero_stillOne()
        {
            // Given:夹具 QTY_MULT_MIN = 1/4 ⇒ Round(1 × 1/4) = Round(0.25) = 0
            //   —— 这正是 GDD 二轮 blocking #1 的实测反例形(原稿断言 QTY_MULT_MIN > 0 即够,为假)
            RecipeSettlementConstants c = fixture();
            Assert.That((new Fix((long)1 * Fix.OneRaw) * c.QtyMultMin).Round(), Is.EqualTo(0L),
                "夹具自证:Round(1 × QTY_MULT_MIN) 必须恰为 0,否则本条不测地板");

            // When:单条 qty = 1,乘子被钳至 QTY_MULT_MIN
            RecipeSettlementResult result = RecipeSettlementSolver.Solve(
                new RecipeSettlementRequest(outputs(1), inputs(1), 0, 1,
                    FixParse.Parse("-10"), FixParse.Parse("-10"), FixParse.Parse("-10")),
                c);

            // Then:max(1, 0) = 1 —— 非零由公式结构兜住,不依赖 QTY_MULT_MIN 取值
            Assert.That(result.OutputQty, Is.EqualTo(new[] { 1 }),
                "Round(·) 落到 0 时 max(1,·) 必须救回 —— AC-21a-1 的核心断言");
        }

        [Test]
        public void test_outputQty_qtyMultMinAnyPositiveValue_neverBreachesFloor()
        {
            // Given/When/Then:QTY_MULT_MIN 取任意正值(含极小)都不得击穿下界(AC-21a-1 Edge)
            foreach (string raw in new[] { "0", "1/10000", "1/4", "1/2" })
            {
                RecipeSettlementConstants c = new RecipeSettlementConstants(
                    qtyMultMin: FixParse.Parse(raw),
                    qtyMultMax: FixParse.Parse("2"),
                    skillModCap: FixParse.Parse("1/10"),
                    qualModCap: FixParse.Parse("1/10"),
                    equipModCap: FixParse.Parse("1/10"),
                    envModMin: FixParse.Parse("-1"),
                    envModMax: FixParse.Parse("1/2"),
                    retainMin: FixParse.Parse("1/2"),
                    retainMax: FixParse.Parse("1"),
                    effMin: FixParse.Parse("1/2"),
                    effMax: FixParse.Parse("1"),
                    maxQuality: FixtureMaxQuality,
                    skillCap: FixtureSkillCap);

                RecipeSettlementResult result = RecipeSettlementSolver.Solve(
                    new RecipeSettlementRequest(outputs(1, 1, 7), inputs(1), 0, 1,
                        FixParse.Parse("-10"), FixParse.Parse("-10"), FixParse.Parse("-10")),
                    c);

                foreach (int qty in result.OutputQty)
                    Assert.That(qty, Is.GreaterThanOrEqualTo(1),
                        $"QTY_MULT_MIN = {raw} 时下界仍须 ≥ 1");
            }
        }

        // ══════════════ AC-21a-2:品级永不被抬高 ══════════════

        [Test]
        public void test_outputQuality_anyInputQualityAndSkill_neverExceedsInput()
        {
            // Given:InputQuality 遍历 {1 … MAX_QUALITY};CraftSkill 遍历 {0, 中点, SKILL_CAP}
            RecipeSettlementConstants c = fixture();
            foreach (int inputQuality in new[] { 1, 2, 3, 4, FixtureMaxQuality })
            {
                foreach (int skill in new[] { 0, FixtureSkillCap / 2, FixtureSkillCap })
                {
                    // When:调用 F2
                    int outputQuality = RecipeSettlementSolver.OutputQuality(c, inputQuality, skill);

                    // Then:1 ≤ OutputQuality ≤ InputQuality(clamp 上界是 InputQuality,不是 MAX_QUALITY)
                    Assert.That(outputQuality, Is.LessThanOrEqualTo(inputQuality),
                        $"InputQuality={inputQuality}, skill={skill} ⇒ 品级被抬高 = 刷品级路径存在(AC-21a-2)");
                    Assert.That(outputQuality, Is.GreaterThanOrEqualTo(1),
                        $"InputQuality={inputQuality}, skill={skill} ⇒ 品级归零");
                }
            }
        }

        [Test]
        public void test_outputQuality_inputOne_identityClampAtEverySkill()
        {
            // Given:InputQuality = 1 ⇒ clamp(·, 1, 1) 恒等(AC-21a-2 Edge)
            RecipeSettlementConstants c = fixture();

            // When/Then:全部技能档都必须是 1
            foreach (int skill in new[] { 0, 1, FixtureSkillCap - 1, FixtureSkillCap })
                Assert.That(RecipeSettlementSolver.OutputQuality(c, 1, skill), Is.EqualTo(1),
                    $"inputQuality=1 恒等 clamp(·,1,1) —— skill={skill}");
        }

        [Test]
        public void test_outputQuality_fullRetainMaxSkill_equalsInput()
        {
            // Given:夹具 RETAIN_MAX = 1(满技能完全保值)+ skill = SKILL_CAP
            RecipeSettlementConstants c = fixture();

            // When/Then:Retain = 1 ⇒ 取等 OutputQuality = InputQuality(AC-21a-2 Edge)
            Assert.That(RecipeSettlementSolver.Retain(c, FixtureSkillCap).Raw, Is.EqualTo(c.RetainMax.Raw));
            Assert.That(RecipeSettlementSolver.OutputQuality(c, 4, FixtureSkillCap), Is.EqualTo(4),
                "Retain = 1 + 满技能 ⇒ 取等");
        }

        [Test]
        public void test_retain_zeroSkill_equalsRetainMin_neverZero()
        {
            // Given/When:skill = 0 ⇒ Retain = RETAIN_MIN > 0(AC-21a-2 Edge:「保住部分不归零」)
            RecipeSettlementConstants c = fixture();

            // Then
            Assert.That(RecipeSettlementSolver.Retain(c, 0).Raw, Is.EqualTo(c.RetainMin.Raw));
            Assert.That(c.RetainMin.Raw, Is.GreaterThan(0L), "夹具自证:RETAIN_MIN > 0(GDD 硬约束)");
            Assert.That(RecipeSettlementSolver.OutputQuality(c, FixtureMaxQuality, 0),
                Is.GreaterThanOrEqualTo(1), "零技能也不得把品级归零");
        }

        // ══════════════ AC-21a-3:常量表自洽性(非求解器行为)══════════════

        [Test]
        public void test_constantTable_capSumWithinBudget_accepted()
        {
            // Given:Σ(正的 cap) = 3/10,ENV_MOD_MAX = 1/2 ⇒ 左侧 8/10;QTY_MULT_MAX − 1 = 1
            RecipeSettlementConstants c = fixture();

            // When/Then:通过(不等式成立)
            Assert.That(RecipeSettlementConstantTableValidator.IsSelfConsistent(c), Is.True,
                "3/10 + 1/2 = 0.8 ≤ 2 − 1 = 1 ⇒ 自洽");
            Assert.DoesNotThrow(() => RecipeSettlementConstantTableValidator.ThrowIfInconsistent(c));
        }

        [Test]
        public void test_constantTable_capSumOverBudget_rejectedWithNamedValues()
        {
            // Given:caps 拉到 1/2 各一 ⇒ Σ = 1.5,加 ENV_MOD_MAX 1/2 = 2.0 > QTY_MULT_MAX − 1 = 1
            //   —— 与 AC-21a-9 共用夹具 invalid_cap_sum.json(执行体归 Story 005)
            RecipeSettlementConstants c = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/4"),
                qtyMultMax: FixParse.Parse("2"),
                skillModCap: FixParse.Parse("1/2"),
                qualModCap: FixParse.Parse("1/2"),
                equipModCap: FixParse.Parse("1/2"),
                envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("1/2"),
                retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"),
                effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"),
                maxQuality: FixtureMaxQuality,
                skillCap: FixtureSkillCap);

            // When/Then:硬失败,错误文本点名两边 raw 值(便于数值轮定位)
            Assert.That(RecipeSettlementConstantTableValidator.IsSelfConsistent(c), Is.False,
                "1.5 + 0.5 = 2.0 > 1 ⇒ 不自洽");
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => RecipeSettlementConstantTableValidator.ThrowIfInconsistent(c));
            Assert.That(ex.Message, Does.Contain("AC-21a-3"));
            Assert.That(ex.Message, Does.Contain("QTY_MULT_MAX"),
                "错误须点名判据项 —— 静默失败不可接受");
        }

        [Test]
        public void test_constantTable_negativeEnvModMax_dropsTheTerm()
        {
            // Given:ENV_MOD_MAX ≤ 0 ⇒ max(0, ·) = 0,该项从不等式消失(AC-21a-3 Edge)
            RecipeSettlementConstants c = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/2"),
                qtyMultMax: FixParse.Parse("3/2"),     // − 1 = 1/2
                skillModCap: FixParse.Parse("1/2"),    // Σcap = 1/2 恰等
                qualModCap: new Fix(0L),
                equipModCap: new Fix(0L),
                envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("-1/10"),    // 负 ⇒ 不计入
                retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"),
                effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"),
                maxQuality: FixtureMaxQuality,
                skillCap: FixtureSkillCap);

            // When/Then:恰等成立(过)—— 恰使等式成立 = AC-21a-3 Edge 明写的边界
            Assert.That(RecipeSettlementConstantTableValidator.IsSelfConsistent(c), Is.True,
                "ENV_MOD_MAX ≤ 0 时该项消失,1/2 ≤ 1/2 恰等 ⇒ 过");
        }

        [Test]
        public void test_constantTable_allCapsZero_passesInequality()
        {
            // Given:全部 cap = 0(死值旋钮 —— AC-21a-3 Edge:过不等式,死值风险归数值轮)
            RecipeSettlementConstants c = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/2"),
                qtyMultMax: FixParse.Parse("1"),       // − 1 = 0
                skillModCap: new Fix(0L),
                qualModCap: new Fix(0L),
                equipModCap: new Fix(0L),
                envModMin: FixParse.Parse("-1"),
                envModMax: new Fix(0L),
                retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"),
                effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"),
                maxQuality: FixtureMaxQuality,
                skillCap: FixtureSkillCap);

            // When/Then:0 + 0 = 0 ≤ 0 ⇒ 过(对称另一侧死值风险由数值轮处理,不在本函数判据内)
            Assert.That(RecipeSettlementConstantTableValidator.IsSelfConsistent(c), Is.True);
        }

        // ══════════════ AC-21a-4:ΣM 顺序无关性(逐位)══════════════

        [Test]
        public void test_sumOfModifiers_allFourPermutations_bitIdenticalRaw()
        {
            // Given:非平凡四项(含两项恰好抵消,含负项)
            Fix a = FixParse.Parse("1/3");
            Fix b = FixParse.Parse("1/7");
            Fix negThird = FixParse.Parse("-1/3");
            Fix d = FixParse.Parse("2/5");
            Fix[] terms = { a, b, negThird, d };

            // When:枚举全部 4! = 24 种施加顺序(枚举式,非随机 —— 测试须确定性),
            //   每种顺序都**经求解器的公开具名量 `SumOfModifiers`**(非测试体内自行相加 ——
            //   否则只验 C# 整数加法可交换,求解器改分组/插舍入不会变红)
            long baseline = 0L;
            bool first = true;
            foreach (int[] order in permutations4())
            {
                Fix sum = RecipeSettlementSolver.SumOfModifiers(
                    terms[order[0]], terms[order[1]], terms[order[2]], terms[order[3]]);

                // Then:各排列结果 Fix raw 逐位相等(断言在整数域做,禁 float 中转)
                if (first) { baseline = sum.Raw; first = false; }
                else Assert.That(sum.Raw, Is.EqualTo(baseline),
                    "排列 " + string.Join("", order) + " 与基线 raw 不等 —— 定点域顺序无关性失效");
            }
            Assert.That(first, Is.False, "排列枚举器必须产出至少一种顺序");

            // 且 Solve 正文的 ΣM 必须等于「把各项经公开具名量求出后喂 SumOfModifiers」——
            // 若 Solve 另写一份分组/漏一项,这里立刻分叉(锚在求解器输出上,非测试自算)
            RecipeSettlementConstants c = fixture();
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs(2, 3), inputs(1, 4), craftSkillLevel: 30, inputQuality: 3,
                equipMod: FixParse.Parse("1/20"),
                envModClimate: FixParse.Parse("2/5"),
                envModClinic: FixParse.Parse("-1/10"));
            Fix viaHelper = RecipeSettlementSolver.SumOfModifiers(
                RecipeSettlementSolver.SkillMod(c, request.CraftSkillLevel),
                RecipeSettlementSolver.QualityMod(c, request.InputQuality),
                request.EquipMod,
                RecipeSettlementSolver.EnvModTotal(c, request.EnvModClimate, request.EnvModClinic));
            Assert.That(RecipeSettlementSolver.Solve(request, c).SumOfModifiers.Raw,
                Is.EqualTo(viaHelper.Raw),
                "Solve 正文的 ΣM 须等于具名量 `SumOfModifiers` 的分组形");
        }

        [Test]
        public void test_solverFormula_sumOfModifiers_equalsSumOfModifiersHelper()
        {
            // Given:同参数集,分别经「求解器正文的 ΣM」与「公开的具名量 ΣM」两条路径
            RecipeSettlementConstants c = fixture();
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs(2, 3), inputs(1, 4), craftSkillLevel: 30, inputQuality: 3,
                equipMod: FixParse.Parse("1/20"),
                envModClimate: FixParse.Parse("2/5"),
                envModClinic: FixParse.Parse("-1/10"));

            // When
            RecipeSettlementResult viaSolve = RecipeSettlementSolver.Solve(request, c);
            Fix envModTotal = RecipeSettlementSolver.EnvModTotal(
                c, request.EnvModClimate, request.EnvModClinic);
            Fix viaHelper = RecipeSettlementSolver.SumOfModifiers(
                RecipeSettlementSolver.SkillMod(c, request.CraftSkillLevel),
                RecipeSettlementSolver.QualityMod(c, request.InputQuality),
                request.EquipMod,
                envModTotal);

            // Then:逐位相同 —— 证明 Solve 未另写一份公式体
            Assert.That(viaSolve.SumOfModifiers.Raw, Is.EqualTo(viaHelper.Raw));
            Assert.That(viaSolve.EnvModTotal.Raw, Is.EqualTo(envModTotal.Raw));
        }

        [Test]
        public void test_solverFormula_permutedModifierGroups_bitIdenticalOutputs()
        {
            // Given:同一物理参数,但四项分组的**相加顺序**打乱(含 0 项与钳界排列)
            RecipeSettlementConstants c = fixture();
            Fix skillMod = RecipeSettlementSolver.SkillMod(c, 60);
            Fix qualityMod = RecipeSettlementSolver.QualityMod(c, FixtureMaxQuality);
            Fix equipMod = FixParse.Parse("0");
            Fix envModTotal = RecipeSettlementSolver.EnvModTotal(c, FixParse.Parse("-10"), FixParse.Parse("10"));

            Fix[] terms = { skillMod, qualityMod, equipMod, envModTotal };
            long baseline = 0L;
            bool first = true;

            // When:24 种分组顺序都经求解器的公开具名量 `SumOfModifiers`(非测试体内自行相加)
            foreach (int[] order in permutations4())
            {
                Fix sum = RecipeSettlementSolver.SumOfModifiers(
                    terms[order[0]], terms[order[1]], terms[order[2]], terms[order[3]]);

                // Then:QtyMultiplier 与逐条 OutputQty 都逐位相同
                Fix qtyMultiplier = RecipeSettlementSolver.QtyMultiplier(c, sum);
                int[] outputQty = { RecipeSettlementSolver.OutputQty(1, qtyMultiplier),
                                    RecipeSettlementSolver.OutputQty(2, qtyMultiplier) };
                long packed = qtyMultiplier.Raw * 31L + outputQty[0] * 7L + outputQty[1];
                if (first) { baseline = packed; first = false; }
                else Assert.That(packed, Is.EqualTo(baseline),
                    "排列 " + string.Join("", order) + " 的 (QtyMultiplier, OutputQty) 与基线不等");
            }
            Assert.That(first, Is.False, "排列枚举器必须产出至少一种顺序");
        }

        /// <summary>4 项的 24 种排列(静态表,确定性 —— 禁 RNG/打乱)。</summary>
        private static int[][] permutations4() => new[]
        {
            new[]{0,1,2,3}, new[]{0,1,3,2}, new[]{0,2,1,3}, new[]{0,2,3,1}, new[]{0,3,1,2}, new[]{0,3,2,1},
            new[]{1,0,2,3}, new[]{1,0,3,2}, new[]{1,2,0,3}, new[]{1,2,3,0}, new[]{1,3,0,2}, new[]{1,3,2,0},
            new[]{2,0,1,3}, new[]{2,0,3,1}, new[]{2,1,0,3}, new[]{2,1,3,0}, new[]{2,3,0,1}, new[]{2,3,1,0},
            new[]{3,0,1,2}, new[]{3,0,2,1}, new[]{3,1,0,2}, new[]{3,1,2,0}, new[]{3,2,0,1}, new[]{3,2,1,0},
        };

        // ══════════════ F1 其余具名量:钳制与实耗 ══════════════

        [Test]
        public void test_envModTotal_unboundedComponents_clampedOnceInsideFormula()
        {
            // Given:两个未钳制分量各自出域(D-21-31:源系统只供未钳制分量)
            RecipeSettlementConstants c = fixture();

            // When/Then:求和 + 唯一钳制在 F1 正文执行 → 结果落在 [ENV_MOD_MIN, ENV_MOD_MAX]
            Assert.That(RecipeSettlementSolver.EnvModTotal(c, FixParse.Parse("10"), FixParse.Parse("10")).Raw,
                Is.EqualTo(c.EnvModMax.Raw), "正溢出 ⇒ 停 ENV_MOD_MAX");
            Assert.That(RecipeSettlementSolver.EnvModTotal(c, FixParse.Parse("-10"), FixParse.Parse("10")).Raw,
                Is.EqualTo(FixParse.Parse("0").Raw),
                "两分量相消 ⇒ 落在带内原值(0),不停在任一带边");
            Assert.That(RecipeSettlementSolver.EnvModTotal(c, FixParse.Parse("1/10"), FixParse.Parse("1/5")).Raw,
                Is.EqualTo(FixParse.Parse("3/10").Raw), "带内 ⇒ 原值透传");
        }

        [Test]
        public void test_skillMod_curveContinuous_notTwoStep()
        {
            // Given:GDD :508-514 🔑 —— 曲线必须在 Q16.16 raw 域求值;先按 int 算则 Level < SKILL_CAP 恒 0
            RecipeSettlementConstants c = fixture();

            // When/Then:中段必须给出严格介于两端的值(曲线未退化成 0 / 满值两段)
            long zero = RecipeSettlementSolver.SkillMod(c, 0).Raw;
            long full = RecipeSettlementSolver.SkillMod(c, FixtureSkillCap).Raw;
            long mid = RecipeSettlementSolver.SkillMod(c, FixtureSkillCap / 2).Raw;
            Assert.That(zero, Is.EqualTo(0L));
            Assert.That(full, Is.EqualTo(c.SkillModCap.Raw));
            Assert.That(mid, Is.GreaterThan(zero), "中段 > 0 —— int 域先算会给出 0(曲线死亡)");
            Assert.That(mid, Is.LessThan(full), "中段 < 满值 —— 非两段阶跃");
        }

        [Test]
        public void test_actualConsumed_efficiencyAtMostOne_neverBelowBase()
        {
            // Given:EFF ≤ 1 ⇒ ActualConsumed = Ceil(base / EFF) ≥ base(满技能取等 = 零损耗)
            RecipeSettlementConstants c = fixture();

            // When/Then
            Assert.That(RecipeSettlementSolver.ActualConsumed(3, FixParse.Parse("1")), Is.EqualTo(3),
                "EFF = 1(满技能) ⇒ 取等,零损耗");
            Assert.That(RecipeSettlementSolver.ActualConsumed(3, FixParse.Parse("1/2")), Is.EqualTo(6),
                "EFF = 1/2 ⇒ 实耗翻倍(低技能烧料凶)");
            Assert.That(RecipeSettlementSolver.ActualConsumed(3, FixParse.Parse("1/3")), Is.EqualTo(9));
            Assert.That(RecipeSettlementSolver.Efficiency(c, FixtureSkillCap).Raw, Is.EqualTo(c.EffMax.Raw));
            Assert.That(RecipeSettlementSolver.Efficiency(c, 0).Raw, Is.EqualTo(c.EffMin.Raw));
        }

        [Test]
        public void test_actualConsumed_nonPositiveEfficiency_throwsInsteadOfDivideByZero()
        {
            // Given:EFF ≤ 0 是构建期硬门的下游;运行期必须显式拒,不静默除零(GDD §Tuning Knobs 🔑)
            // When/Then
            Assert.Throws<InvalidOperationException>(
                () => RecipeSettlementSolver.ActualConsumed(3, new Fix(0L)));
            Assert.Throws<InvalidOperationException>(
                () => RecipeSettlementSolver.ActualConsumed(3, FixParse.Parse("-1/2")));
        }

        [Test]
        public void test_solve_gddWorkedExample_shapeMatchesQuantityContract()
        {
            // Given:GDD F1 示范形状(数值是**示范**非平衡值):m = 2,outputs = [{qty:1},{qty:2}],
            //   ΣM = 1/2 ⇒ QtyMultiplier = 3/2 ⇒ 逐条 Round(1×1.5)=2 · Round(2×1.5)=3
            RecipeSettlementConstants c = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/4"),
                qtyMultMax: FixParse.Parse("2"),
                skillModCap: new Fix(0L),
                qualModCap: new Fix(0L),
                equipModCap: new Fix(0L),
                envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("1"),
                retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"),
                effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"),
                maxQuality: FixtureMaxQuality,
                skillCap: FixtureSkillCap);
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs(1, 2), inputs(1), craftSkillLevel: 0, inputQuality: 3,
                equipMod: new Fix(0L),
                envModClimate: FixParse.Parse("1/2"),
                envModClinic: new Fix(0L));

            // When
            RecipeSettlementResult result = RecipeSettlementSolver.Solve(request, c);

            // Then:配方级乘子 = 3/2;逐条 max(1, Round(qty × 3/2))
            Assert.That(result.QtyMultiplier.Raw, Is.EqualTo(FixParse.Parse("3/2").Raw));
            Assert.That(result.OutputQty, Is.EqualTo(new[] { 2, 3 }),
                "GDD F1 Example:QtyMultiplier 是配方级、max(1,·) 是逐条");
            Assert.That(result.OutputQty.Length, Is.EqualTo(request.Outputs.Length));
            Assert.That(result.ActualConsumed.Length, Is.EqualTo(request.Inputs.Length));
        }

        [Test]
        public void test_solve_emptyEntryArrays_produceEmptyOutputsNotThrow()
        {
            // Given:空投入 / 空产出(非 schema 合法配方,但求解器不得因此崩 —— 非空性归校验层)
            RecipeSettlementConstants c = fixture();

            // When
            RecipeSettlementResult result = RecipeSettlementSolver.Solve(
                new RecipeSettlementRequest(new RecipeEntry[0], new RecipeEntry[0], 0, 1,
                    new Fix(0L), new Fix(0L), new Fix(0L)),
                c);

            // Then
            Assert.That(result.OutputQty, Is.Empty);
            Assert.That(result.ActualConsumed, Is.Empty);
        }

        [Test]
        public void test_solve_nullEntryArrays_throwsArgumentNullException()
        {
            // Given:null 数组是调用方错误,不静默当空表
            RecipeSettlementConstants c = fixture();

            // When/Then
            Assert.Throws<ArgumentNullException>(() => RecipeSettlementSolver.Solve(
                new RecipeSettlementRequest(null, inputs(1), 0, 1, new Fix(0L), new Fix(0L), new Fix(0L)), c));
            Assert.Throws<ArgumentNullException>(() => RecipeSettlementSolver.Solve(
                new RecipeSettlementRequest(outputs(1), null, 0, 1, new Fix(0L), new Fix(0L), new Fix(0L)), c));
        }

        // ══════════════ 舍入模式可分辨(AC-21a-2 / AC-42 交叉 Edge)══════════════

        [Test]
        public void test_outputQuality_midpointRounding_goesAwayFromZero_notTiesToEven()
        {
            // Given:夹具 RETAIN_MIN = 1/2,InputQuality = 5,skill = 0 ⇒ 5 × 1/2 = 2.5(恰中点)
            RecipeSettlementConstants c = fixture();

            // When
            int outputQuality = RecipeSettlementSolver.OutputQuality(c, 5, 0);

            // Then:ROUND_HALF_AWAY_FROM_ZERO ⇒ 3;ties-to-even 会给 2 ⇒ 本断言可分辨两种模式
            //   (原 AC-21a-2 的单边不等式 ≤ InputQuality 两种模式都过 —— 无分辨力)
            Assert.That(outputQuality, Is.EqualTo(3),
                "中点 2.5 必须远离零到 3 —— ties-to-even 的 2 在这里立刻变红(ADR-006 §三)");
        }

        // ══════════════ 曲线分母 / 域外输入的守卫(QA 复核 §4 新增)══════════════

        [Test]
        public void test_curveSlope_zeroSkillCap_throwsNamedNotDivideByZero()
        {
            // Given:SKILL_CAP = 0 是域错误(数值轮),但裸除零不可诊断
            RecipeSettlementConstants c = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/4"), qtyMultMax: FixParse.Parse("2"),
                skillModCap: FixParse.Parse("1/10"), qualModCap: FixParse.Parse("1/10"),
                equipModCap: FixParse.Parse("1/10"), envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("1/2"), retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"), effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"), maxQuality: FixtureMaxQuality, skillCap: 0);

            // When/Then:显式命名抛(含分量名),非 DivideByZeroException
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => RecipeSettlementSolver.SkillMod(c, 0));
            Assert.That(ex.Message, Does.Contain("SKILL_CAP"));
            Assert.Throws<InvalidOperationException>(() => RecipeSettlementSolver.Efficiency(c, 0));
        }

        [Test]
        public void test_curveSlope_maxQualityOne_throwsNamedNotDivideByZero()
        {
            // Given:MAX_QUALITY = 1 ⇒ MAX_QUALITY − 1 = 0(域错误:须 ≥ 2)
            RecipeSettlementConstants c = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/4"), qtyMultMax: FixParse.Parse("2"),
                skillModCap: FixParse.Parse("1/10"), qualModCap: FixParse.Parse("1/10"),
                equipModCap: FixParse.Parse("1/10"), envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("1/2"), retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"), effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"), maxQuality: 1, skillCap: FixtureSkillCap);

            // When/Then:命名抛(含判据项),不静默除零
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => RecipeSettlementSolver.QualityMod(c, 1));
            Assert.That(ex.Message, Does.Contain("MAX_QUALITY"));
        }

        [Test]
        public void test_efficiency_skillLevelAboveCap_clampedNeverExceedsEffMax()
        {
            // Given:CraftSkillLevel > SKILL_CAP(域外)—— 若不钳,插值外推 ⇒ EFF > 1 ⇒ 实耗 < 基数
            RecipeSettlementConstants c = fixture();

            // When/Then:曲线钳在 SKILL_CAP ⇒ EFF = EFF_MAX(≤ 1),实耗取等不凭空造料
            Assert.That(RecipeSettlementSolver.Efficiency(c, FixtureSkillCap * 2).Raw,
                Is.EqualTo(c.EffMax.Raw), "越域技能不得把 EFF 推过 EFF_MAX(否则违反守恒律)");
            Assert.That(RecipeSettlementSolver.Retain(c, FixtureSkillCap * 2).Raw,
                Is.EqualTo(c.RetainMax.Raw), "越域技能不得把 Retain 推过 RETAIN_MAX");
            Fix eff = RecipeSettlementSolver.Efficiency(c, FixtureSkillCap * 2);
            Assert.That(RecipeSettlementSolver.ActualConsumed(3, eff), Is.EqualTo(3),
                "EFF ≤ 1 ⇒ 实耗 ≥ 基数(凭空造料是守恒律违反;QA 复核 §4d)");
        }

        [Test]
        public void test_qualityMod_inputQualityAboveMaxQuality_clampedToCap()
        {
            // Given:InputQuality > MAX_QUALITY(域外)—— 若不钳,加分超 QUAL_MOD_CAP ⇒ ΣM 静默放大
            RecipeSettlementConstants c = fixture();

            // When/Then:钳在 MAX_QUALITY ⇒ QualityMod = QUAL_MOD_CAP(上界)
            Assert.That(RecipeSettlementSolver.QualityMod(c, FixtureMaxQuality * 3).Raw,
                Is.EqualTo(c.QualModCap.Raw),
                "越域 InputQuality 不得把 QualityMod 推过 QUAL_MOD_CAP");
        }

        [Test]
        public void test_outputQuality_inputQualityBelowOne_throws()
        {
            // Given:InputQuality = 0(域外)—— 下钳 1 会越过上钳输入品级,产出反高于输入(AC-21a-2 禁止)
            RecipeSettlementConstants c = fixture();

            // When/Then:显式拒,而非产出高于输入的品级
            Assert.Throws<InvalidOperationException>(
                () => RecipeSettlementSolver.OutputQuality(c, 0, FixtureSkillCap));
        }

        [Test]
        public void test_actualConsumed_guardHoldsForEmptyInputArray()
        {
            // Given:空投入数组 + EFF_MIN ≤ 0 的坏常量表 —— 循环体守卫不会被走到
            RecipeSettlementConstants bad = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/4"), qtyMultMax: FixParse.Parse("2"),
                skillModCap: FixParse.Parse("1/10"), qualModCap: FixParse.Parse("1/10"),
                equipModCap: FixParse.Parse("1/10"), envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("1/2"), retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"), effMin: new Fix(0L),   // EFF_MIN = 0 域错误
                effMax: FixParse.Parse("1"), maxQuality: FixtureMaxQuality, skillCap: FixtureSkillCap);

            // When/Then:坏常量表在**进入计量前**被显式拒(不因输入长度分叉 —— QA 复核 §4f)
            Assert.Throws<InvalidOperationException>(() => RecipeSettlementSolver.Solve(
                new RecipeSettlementRequest(outputs(1), inputs(1), 0, 1,
                    new Fix(0L), new Fix(0L), new Fix(0L)), bad),
                "EFF_MIN ≤ 0 的常量表 + 非空投入 ⇒ 抛");
            Assert.Throws<InvalidOperationException>(() => RecipeSettlementSolver.Solve(
                new RecipeSettlementRequest(outputs(1), new RecipeEntry[0], 0, 1,
                    new Fix(0L), new Fix(0L), new Fix(0L)), bad),
                "同一坏常量表 + 空投入 ⇒ 仍抛(行为不随输入长度分叉)");
        }

        [Test]
        public void test_toInt32Checked_overflowThrowsInsteadOfWrapping()
        {
            // Given:基数 × clamp 后的乘子被推过 int 域(QTY_MULT_MAX 极大 ⇒ 产出量巨大)
            RecipeSettlementConstants huge = new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/4"), qtyMultMax: new Fix((long)int.MaxValue * Fix.OneRaw),
                skillModCap: FixParse.Parse("1/10"), qualModCap: FixParse.Parse("1/10"),
                equipModCap: FixParse.Parse("1/10"), envModMin: FixParse.Parse("-1"),
                envModMax: FixParse.Parse("1/2"), retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"), effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"), maxQuality: FixtureMaxQuality, skillCap: FixtureSkillCap);

            // When/Then:超 int 域 ⇒ OverflowException,不回绕(QA 复核 §4e:守卫视零覆盖的变异存活体)
            Assert.Throws<OverflowException>(
                () => RecipeSettlementSolver.OutputQty(int.MaxValue, huge.QtyMultMax),
                "超 int 出参域必须显式抛,不静默回绕(GDD D-21-9)");
        }

        // ══════════════ AC-21a-47:性能冒烟(ADVISORY,不入 BLOCKING)══════════════

        [Test]
        [Category("Advisory")]
        public void test_timing_singleSolveUnderOneMillisecond_advisorySmoke()
        {
            // Given:全量形状的配方(最大 outputs 条数夹具 = 8 条产出 + 8 条投入)
            RecipeSettlementConstants c = fixture();
            RecipeSettlementRequest request = new RecipeSettlementRequest(
                outputs(1, 2, 3, 4, 5, 6, 7, 8),
                inputs(1, 2, 3, 4, 5, 6, 7, 8),
                craftSkillLevel: FixtureSkillCap, inputQuality: FixtureMaxQuality,
                equipMod: FixParse.Parse("1/20"),
                envModClimate: FixParse.Parse("-1/5"),
                envModClinic: FixParse.Parse("1/10"));

            // 冷一次(排除首次 JIT 噪声)+ 热 N 次(取**均值** —— 单次 < 1 ms 判据是均值口径;
            //   峰值只作诊断输出,不参与判据)
            RecipeSettlementSolver.Solve(request, c);
            var stopwatch = Stopwatch.StartNew();
            RecipeSettlementResult last = default;
            for (int i = 0; i < 1000; i++)
                last = RecipeSettlementSolver.Solve(request, c);
            stopwatch.Stop();

            double microsecondsPerSolve = stopwatch.Elapsed.TotalMilliseconds * 1000.0 / 1000.0;

            // Then:GDD 原文阈值 < 1 ms;超阈**不红**(计时非确定性 ⇒ 按 ADVISORY 记录)
            TestContext.Out.WriteLine(
                $"[AC-21a-47 ADVISORY] 单次 F1+F2 求解 = {microsecondsPerSolve:F3} µs(阈值 1000 µs);" +
                $"最后结果 OutputQty[0]={last.OutputQty[0]}");
            Assert.That(last.OutputQty.Length, Is.EqualTo(8),
                "冒烟须至少证求解真的跑完(非仅计时)");
        }
    }
}
