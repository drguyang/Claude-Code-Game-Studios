// Story 002 · F-3.1 轴处理算术性质与装载期断言(AC-3-A9 四条 BLOCKING 的全部测试真身)
//
// 权威来源:production/epics/input-system/story-002-axis-processing.md(判据权威,AC/QA 原文)
//   · AC-3-A9①(BLOCKING)NaN / ∞ 与零点防护 —— 输出恒 (0,0)、非 NaN、不抛异常
//   · AC-3-A9②(BLOCKING)满速可达 —— m ≥ DZ_OUTER ⇒ t = 1 ∧ g = 1 ∧ ‖out‖ = 1
//   · AC-3-A9③(BLOCKING)静止归零 —— raw = 0 ⇒ (0,0);否定 DZ_INNER < 0 型「静止自走」
//   · AC-3-A9④(BLOCKING)装载期断言 —— 四组反例须使装载硬失败(承 ADR-014)
//   · GDD input-system.md F-3.1 全文 + §Tuning Knobs 一(四条并列断言)
//   · ADR-006(唯一解析入口 FixParse)· ADR-014 §四(Fix 字段字符串 + 硬失败)
//
// 落点注记:故事 Test Evidence 登记口径 tests/unit/input_system/axis_processing_test.cs;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径;反例夹具在 tests/unit/input_system/fixtures/
//   (经 [CallerFilePath] 上溯仓库根直读)。
//
// 纪律(QA Test Cases 逐字):只断性质、零数值期望 —— 不写「输出 = 0.875」型期望值;
//   算术性质载体 = 示范常量(FromRatio 1/8 · 7/8 · 3/2,GDD F-3.1 例值),对一切合法常量成立,
//   故免疫用户改种子;种子文件只断「装载成功」不钉输出(数值归用户 —— 项目铁律);
//   负例 = 失效签名(内存直改绕过装载门 ⇒ 主断言必红,证明断言链闭合);
//   确定性 · 无随机 · 无时间依赖 · 每测试例自建常量表。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 002 · F-3.1 轴处理四条 BLOCKING AC 的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class AxisProcessingTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string FixtureDir =
            Path.Combine(RepoRoot, "tests", "unit", "input_system", "fixtures");
        private static readonly string SeedPath =
            Path.Combine(RepoRoot, "assets", "data", InputAxisTuningBinder.SourceFileName);

        // ±1 ulp 容差 = Q16.16 一格(2^-16 ≈ 1.526e-5)。论证(QA②「容差须在测试内明写并论证」):
        //   float32 相对精度 2^-24 ≈ 6e-8 ≪ 2^-16;且 t = 1 端 s'(t) = 6t − 6t² = 0(C1 软肩)
        //   ⇒ t 侧误差对 g 二次压制 —— 容差只吃浮点往返,不吃公式偏差;篡改负例(DZ_OUTER = 1.01)
        //   造成的偏差 ~2e-4 仍 ≫ 本容差 ⇒ 失效签名可靠可判。
        private const float UlpAtOne = 1f / 65536f;

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        /// <summary>示范常量表(GDD F-3.1 例值):性质测试的合法载体;Given 经 Validate 自证。</summary>
        private static AxisTuning CanonicalTuning()
        {
            var tuning = new AxisTuning(
                FixParse.FromRatio(1, 8),
                FixParse.FromRatio(7, 8),
                FixParse.FromRatio(3, 2));
            Assert.That(tuning.Validate("canonical"), Is.Empty, "Given:示范常量表须过全部四条装载期断言");
            return tuning;
        }

        /// <summary>满速三断言:t = 1 ∧ g = 1 ∧ ‖out‖ = 1(AC-3-A9② Then 原文)。</summary>
        private static void AssertFullSpeed(AxisEvaluation ev, string because)
        {
            Assert.That(ev.T, Is.EqualTo(1f).Within(UlpAtOne), $"t = 1 —— {because}");
            Assert.That(ev.G, Is.EqualTo(1f).Within(UlpAtOne), $"g = 1 —— {because}");
            Assert.That(ev.Out.magnitude, Is.EqualTo(1f).Within(UlpAtOne), $"‖out‖ = 1 —— {because}");
        }

        private static string LegalJson(string dzInner = "\"1/8\"", string dzOuter = "\"7/8\"",
            string curvePow = "\"3/2\"")
            => "{\"schema_version\": 1, \"_note\": \"inline boundary fixture\"," +
               $" \"dz_inner\": {dzInner}, \"dz_outer\": {dzOuter}, \"curve_pow\": {curvePow}}}";

        // ══════════ AC-3-A9① NaN / ∞ 与零点防护 ══════════

        private static readonly object[] NonFiniteAndZeroCases =
        {
            new object[] { float.NaN, 0f },
            new object[] { float.PositiveInfinity, 1f },
            new object[] { 1f, float.NegativeInfinity },
            new object[] { float.NaN, float.NaN },
            new object[] { 0f, 0f },
        };

        [Test, TestCaseSource(nameof(NonFiniteAndZeroCases))]
        public void test_axisProcessor_nonFiniteOrZeroRaw_outputsExactlyZero(float x, float y)
        {
            var ev = AxisProcessor.Evaluate(new Vector2(x, y), CanonicalTuning());
            Assert.That(ev.Out.x, Is.EqualTo(0f), $"x 分量恰 0(非 NaN)—— raw = ({x}, {y})");
            Assert.That(ev.Out.y, Is.EqualTo(0f), $"y 分量恰 0(非 NaN)—— raw = ({x}, {y})");
        }

        [Test]
        public void test_axisProcessor_tinyMagnitude_normalPathNotCaughtByGuard()
        {
            // QA① Edge:「m 极小非零但不 NaN(走正常路径,不被 ① 捕获)」
            var ev = AxisProcessor.Evaluate(new Vector2(1e-6f, 0f), CanonicalTuning());
            Assert.That(ev.M, Is.GreaterThan(0f).And.LessThan(1e-5f),
                "m 保留为极小正数 —— ① 的守卫不吞正常路径输入");
            Assert.That(ev.T, Is.EqualTo(0f), "仍处死区内(t 夹到 0)—— 死区语义,非守卫产物");
            Assert.That(ev.Out.x, Is.EqualTo(0f), "死区内输出恰 0");
            Assert.That(ev.Out.y, Is.EqualTo(0f), "死区内输出恰 0");
        }

        // ══════════ AC-3-A9② 满速可达 ══════════

        [Test]
        public void test_axisProcessor_magnitudeAtOuter_fullSpeedOne()
        {
            // QA② Edge:「m 恰等 DZ_OUTER(边界闭侧)」
            var tuning = CanonicalTuning();
            float outer = tuning.DzOuter.ToFloat();
            var ev = AxisProcessor.Evaluate(new Vector2(outer, 0f), tuning);
            AssertFullSpeed(ev, "m = DZ_OUTER 边界闭侧");
        }

        [Test]
        public void test_axisProcessor_unitMagnitude_fullSpeedOne()
        {
            var ev = AxisProcessor.Evaluate(new Vector2(1f, 0f), CanonicalTuning());
            AssertFullSpeed(ev, "m = 1");
        }

        [Test]
        public void test_axisProcessor_beyondUnitCircle_fullSpeedOne()
        {
            var ev = AxisProcessor.Evaluate(new Vector2(1.5f, 1.5f), CanonicalTuning());
            AssertFullSpeed(ev, "m > 1(越单位圆输入)");
        }

        [Test]
        public void test_axisProcessor_diagonal_fullSpeed_normOne()
        {
            // QA② Edge:「斜向输入(方向保持,只断模长)」
            var ev = AxisProcessor.Evaluate(new Vector2(1f, 1f), CanonicalTuning());
            Assert.That(ev.Out.magnitude, Is.EqualTo(1f).Within(UlpAtOne),
                "斜向满速 ‖out‖ = 1(方向保持不在 ② 断言面)");
        }

        [Test]
        public void test_axisProcessor_neg_tamperedOuterAboveOne_fullSpeedUnreachable()
        {
            // QA② Negative fixture 原文:「内存直改常量使 DZ_OUTER = 1.01 ⇒ ② 红(证明断言链闭合)」。
            // 本测断失效签名本身:t < 1 ⇒ 若对本输出套用 ② 的满速断言必红。
            var tampered = new AxisTuning(
                FixParse.FromRatio(1, 8), FixParse.FromRatio(101, 100), FixParse.FromRatio(3, 2));
            Assert.That(tampered.Validate("tamper"), Is.Not.Empty,
                "Given:篡改表须已被④ 拒绝 —— 绕过装载门的内存路径正是本负例的入口");
            var ev = AxisProcessor.Evaluate(new Vector2(1f, 0f), tampered);
            Assert.That(ev.Out.magnitude, Is.LessThan(1f - UlpAtOne),
                "失效签名:DZ_OUTER > 1 ⇒ 满速永不触发 ⇒ AC② 主断言(‖out‖ = 1)必红");
        }

        // ══════════ AC-3-A9③ 静止归零 ══════════

        [Test]
        public void test_axisProcessor_rest_outputsZeroWithTZero()
        {
            // QA③ Then:「输出 (0,0) —— 静止不自走」;与 ① 的 m = 0 同点但独立断言
            // (③ 断语义,① 断防护)⇒ 双通道都断。
            var ev = AxisProcessor.Evaluate(Vector2.zero, CanonicalTuning());
            Assert.That(ev.Out.x, Is.EqualTo(0f), "静止输出 x 恰 0");
            Assert.That(ev.Out.y, Is.EqualTo(0f), "静止输出 y 恰 0");
            Assert.That(ev.T, Is.EqualTo(0f), "静止点 t = 0 —— 增益通道同断(防 DZ_INNER < 0 静止自走)");
        }

        [Test]
        public void test_axisProcessor_neg_tamperedInnerBelowZero_restTNonZero()
        {
            // QA③ Negative fixture 原文:「直改 DZ_INNER < 0 ⇒ 红」(静止点 t > 0 ⇒ 此测红)。
            // T 无条件求值(见 AxisProcessor.Evaluate doc)正是为了让这条签名可观察。
            var tampered = new AxisTuning(
                FixParse.FromRatio(-1, 10), FixParse.FromRatio(7, 8), FixParse.FromRatio(3, 2));
            Assert.That(tampered.Validate("tamper"), Is.Not.Empty,
                "Given:篡改表须已被④ 拒绝 —— 负例走绕过装载门的内存构造");
            var ev = AxisProcessor.Evaluate(Vector2.zero, tampered);
            Assert.That(ev.T, Is.GreaterThan(0f),
                "失效签名:DZ_INNER < 0 ⇒ 静止点 t > 0 ⇒ AC③ 主断言(T = 0)必红");
        }

        // ══════════ AC-3-A9④ 装载期断言 ══════════

        [Test]
        public void test_axisTuningLoader_legalSeed_loadsAndValidates()
        {
            // QA④ Then:「合法表装载成功」—— 种子值归用户(项目铁律),只断装载不钉输出。
            string json = File.ReadAllText(SeedPath);
            AxisTuning tuning = InputAxisTuningBinder.BindFromSourceText(json, "seed");
            Assert.That(tuning.Validate("seed"), Is.Empty, "种子表装载成功且四条断言全过");
        }

        [Test]
        public void test_axisTuningLoader_boundaryOuterEqOne_loads()
        {
            // QA④ Edge:「DZ_OUTER = 1 恰等上界(合法)」
            Assert.DoesNotThrow(() =>
                InputAxisTuningBinder.BindFromSourceText(LegalJson(dzOuter: "\"1\""), "boundary-outer-eq-one"));
        }

        [Test]
        public void test_axisTuningLoader_boundaryPowHalfPlusEpsilon_loads()
        {
            // QA④ Edge:「CURVE_POW 恰等 1/2(拒)与 1/2 + ε(过)」—— 此处为 ε = 1 定点格的过侧;
            // 拒侧 = invalid_curve_pow_half.json 夹具。
            Assert.DoesNotThrow(() =>
                InputAxisTuningBinder.BindFromSourceText(
                    LegalJson(curvePow: "\"32769/65536\""), "boundary-pow-half-plus-eps"));
        }

        private static readonly object[][] CounterExampleCases =
        {
            // 字段名后缀 = 该反例的可定位锚(④ Then「每例装载硬失败」+ Errors 可诊断)
            new object[] { "invalid_dz_inner_negative.json", "DZ_INNER" },
            new object[] { "invalid_dz_inner_eq_outer.json", "DZ_INNER" },
            new object[] { "invalid_dz_outer_above_one.json", "DZ_OUTER" },
            new object[] { "invalid_curve_pow_half.json", "CURVE_POW" },
            new object[] { "invalid_curve_pow_infinite.json", "curve_pow" },
            new object[] { "invalid_curve_pow_nan.json", "curve_pow" },
            new object[] { "invalid_fix_float_token.json", "dz_outer" },
        };

        [Test, TestCaseSource(nameof(CounterExampleCases))]
        public void test_axisTuningLoader_counterExamples_failHard(string fixtureName, string expectedInMessage)
        {
            // QA④ Then:「每例装载硬失败(显式异常/构建失败,非警告、非 clamp 后继续)」
            string json = File.ReadAllText(Path.Combine(FixtureDir, fixtureName));
            var ex = Assert.Throws<BakeValidationException>(() =>
                InputAxisTuningBinder.BindFromSourceText(json, fixtureName));
            Assert.That(ex.Errors, Is.Not.Empty, "Errors 非空(聚合口径)");
            Assert.That(ex.Message, Does.Contain("烘焙校验失败"), "硬失败经 BakeValidationException,非警告");
            Assert.That(ex.Message, Does.Contain(expectedInMessage),
                $"违例字段可定位(期望含 \"{expectedInMessage}\")—— {fixtureName}");
        }
    }
}
