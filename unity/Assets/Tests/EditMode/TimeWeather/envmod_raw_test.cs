// time-weather · 5 的 EnvMod 环境分量(**加法增量**)测试
//
// 权威来源:
//   GDD `time-and-weather.md`
//     · F-5.4(:388-410)入参形状;规则六(:216-246)`EnvMod_total = 24 分量 + 5 分量`(**加法域**)
//     · B-3 订正(:399-403)5 侧**不钳制**
//     · AC-5-12 玩家不在任何生态区 ⇒ **全局默认**(非最近区)
//     · AC-5-19 5 侧导出**允许越界**,钳制唯一在 21a
//   `item-database.md` F1:`EnvMod_total = clamp(climate + clinic, MIN, MAX)`
//   `clinic-machine.md` EC-24-01/02/07:24 的中性出口 = `(0, 0)`
//
// 🔁 2026-11-11(`TD-processing-09` · 用户裁定「甲 · 加法增量」)
//   **本文件此前钉的是乘性契约**(`中性 = Fix.One`、天气 / 昼夜 / 季节各乘一个系数)。
//   那条契约与 F1 的加法域相反,且三个系数在 GDD 里没有出处(`g(·)` 形状 = `OQ-5-4` 未裁)。
//   现契约 = **加法增量,中性 0,零钳制**,`g(·)` 今天只透传生态区基线。
//   ⚠️ 旧文件名保留(`envmod_raw_test.cs`):Unity 改文件名要连带 .meta,而本文件的**类名**
//   `EnvModRawTest` 已不描述被测物 —— 见下方「命名」注。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.TimeWeather
{
    /// <summary>
    /// ⚠️ **命名**:类名沿用旧名 `EnvModRawTest`(文件与 .meta 不动)。
    /// 被测物自 2026-11-11 起是 `EnvMod.ComputeEnvModDelta` ——
    /// 「raw」在 GDD 里仍指 F-5.4 的 `EnvMod_raw`,故不是错名,但**不是**乘性乘子。
    /// </summary>
    public class EnvModRawTest
    {
        private static readonly long OneRaw = Fix.OneRaw;

        // ── 中性 ────────────────────────────────────────────────────────────────

        [Test]
        public void test_envmodDelta_neutralIsZero()
        {
            var input = new EnvModInput(Fix.Zero, 0, 0, false, 0);

            Fix result = EnvMod.ComputeEnvModDelta(input);

            Assert.AreEqual(0L, result.Raw,
                "加法域的中性 = 0(与 24 的中性出口 (0,0) 同数)—— 不是 Fix.One");
            Assert.AreEqual(EnvMod.Neutral.Raw, result.Raw,
                "Neutral 常量须与被测函数的中性出口一致");
        }

        [Test]
        public void test_envmodDelta_globalDefault_isZeroWhenNoEcozone()
        {
            // AC-5-12:不在任何生态区 ⇒ 取**全局默认**。全局默认的数值归数值轮,
            // 今日的中性占位 = 0(**不是**「取最近区」—— 该判据由调用侧的 EcozoneOf 决定,
            // 本函数只接收已解析好的基线)。
            var input = new EnvModInput(Fix.Zero, 0, 0, false, 0);

            Assert.AreEqual(0L, EnvMod.ComputeEnvModDelta(input).Raw);
        }

        // ── 负值与零钳制(AC-5-16 / AC-5-19)──────────────────────────────────────

        [Test]
        public void test_envmodDelta_negativeBaseline_passesThroughUnclamped()
        {
            // −0.9(规则六 B-3 的算例值):若 5 侧把它截到 −0.5,叠 24 的 +0.4 后
            // 21a 只见 −0.1 —— 环境惩罚被好机器无声吃掉。故必须**原样** −0.9。
            var negativeNineTenths = new Fix(-9 * OneRaw / 10);
            var input = new EnvModInput(negativeNineTenths, 0, 0, false, 0);

            Fix result = EnvMod.ComputeEnvModDelta(input);

            Assert.AreEqual(negativeNineTenths.Raw, result.Raw,
                "5 侧零钳制:−0.9 必须原样出,不得被截到任何 ENV_MOD_MIN");
            Assert.Less(result.Raw, 0L, "负值合法(规则六「可为负 —— 是结构不是错误」)");
        }

        [Test]
        public void test_envmodDelta_composesAdditivelyWithClinicComponent()
        {
            // 域判据(债 `TD-processing-09` 的病根):两个分量必须**相加**,不是相乘。
            // climate = −0.2,clinic = +0.4 ⇒ 合计 +0.2。
            // 若 climate 是乘性乘子(旧实现的形态),合计会是 0.4 × (−0.2) 或 1.4 × 0.8 之类,
            // 与 +0.2 都不等 —— 这条把「同一个量纲」钉死。
            var climate = new Fix(-2 * OneRaw / 10);
            var clinic = new Fix(4 * OneRaw / 10);

            Fix sum = EnvMod.ComputeEnvModDelta(new EnvModInput(climate, 0, 0, false, 0)) + clinic;

            Assert.AreEqual(2 * OneRaw / 10, sum.Raw,
                "EnvMod_total 的两个分量是加法关系(规则六 · 21a F1)");
        }

        // ── 未裁项不得自造系数(`OQ-5-4`)────────────────────────────────────────────

        [Test]
        public void test_envmodDelta_undecidedInputs_doNotInventCoefficients()
        {
            // `g(·)` 的五项里,昼夜 / 季节 / 天气的**映射形状**未裁(OQ-5-4「未定」,值归用户)。
            // 基线恒 0 时,给这三项任意取值,输出都必须仍是 0 ——
            // 否则实现侧就凭空造了一条系数(旧实现正是这样:夜晚 ×0.9、季节 ×1.05、天气 ×0.015)。
            var day = new EnvModInput(Fix.Zero, 0, 0, false, 0);
            var extreme = new EnvModInput(Fix.Zero, 3, 1000, true, 4);

            Assert.AreEqual(EnvMod.ComputeEnvModDelta(day).Raw,
                EnvMod.ComputeEnvModDelta(extreme).Raw,
                "未裁项(天气 / 昼夜 / 季节)今日不得改变输出 —— 否则即自造 g(·) 系数");
            Assert.AreEqual(0L, EnvMod.ComputeEnvModDelta(extreme).Raw);
        }

        // ── 纯函数面(AC-5-04)─────────────────────────────────────────────────────

        [Test]
        public void test_envmodDelta_sameInputSameOutput()
        {
            var input = new EnvModInput(new Fix(5000), 2, 500, true, 3);

            Assert.IsTrue(EnvMod.IsDeterministic(input), "同输入应逐位同输出");
        }

        [Test]
        public void test_envmodDelta_baselineOnly_isDeterministicAcrossRepeats()
        {
            var input = new EnvModInput(new Fix(-12345), 0, 0, false, 0);

            Assert.AreEqual(EnvMod.ComputeEnvModDelta(input).Raw,
                EnvMod.ComputeEnvModDelta(input).Raw);
        }

        // ── 类型面(TR-timeweather-012 / AC-5-13)─────────────────────────────────

        [Test]
        public void test_envmodInput_noFloatOrDoubleFields()
        {
            Type envModType = typeof(EnvModInput);
            foreach (var field in envModType.GetFields())
            {
                Assert.IsFalse(field.FieldType == typeof(float), $"字段 {field.Name} 不应为 float");
                Assert.IsFalse(field.FieldType == typeof(double), $"字段 {field.Name} 不应为 double");
            }
        }

        [Test]
        public void test_envmodDelta_returnsFix_notFloat()
        {
            Assert.AreEqual(typeof(Fix), typeof(EnvMod).GetMethod(
                nameof(EnvMod.ComputeEnvModDelta)).ReturnType,
                "导出类型须是定点 Fix —— 零 float 出口(门 A)");
        }
    }
}
