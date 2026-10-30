// R1-37 —— F2 投影阈值的写入期判据(`signs[].appear_at`)。
//
// 权威来源:
//   `disease-simulation.md` F2(`Signⱼ = Project(Progress, 病种)ⱼ`)+ `D-9-R` 订正注
//   `RegistrySchema.cs` 里 `RegistrySchemaValidator.R1_CHECK_37` —— **两段判据**:
//     ① 绝对域  `appear_at ∈ (0, 1]`
//     ② 可达性  `appear_at < A_peak/SCALE`(2026-10-30 数值轮加门)
//
// ── 立件缘由 ──────────────────────────────────────────────────────────────────
// ① 的原注释说的是「>1 ⇒ 永不出现」,但那只覆盖了一半:`position` 的**实际上界**
// 不是 1,而是该行的 **峰值 `A_peak/SCALE`**(如 `DIS_NEURASTHENIA` 的 0.15)。
// ⇒ 阈值落在 `(峰值, 1]` 区间内的词条**结构上不可达**,而 ① 全部放行。
// 2026-10-30 数值轮实测:**9 条体征**踩这个洞(其中 `DIS_NEURASTHENIA` 全 4 条
// ⇒ 该行掩码恒 0,8 的脉案对它完全失明)。本文件把「死词条」钉成构建期拒收。
//
// ⚠️ 与 `disease_registry_data_test` 的分工:
//   后者验**真数据**过校验;本文件验**校验器本身会拒**(用合成夹具构造违例)。
// ⚠️ 与 `registry_curve_floor_test` 的分工:
//   那个文件管 R1-34/35(峰值能不能打到死线);本文件管 R1-37(体征阈值能不能被波形够着)。
//   两者都在「峰值」上做文章,但一个比的是**状态机阈值**,一个比的是**体征阈值**。

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>R1-37 —— `signs[].appear_at` 的域判据与可达性判据。</summary>
    [TestFixture]
    internal sealed class RegistrySignThresholdGateTest
    {
        // ══════════════════ 夹具 ══════════════════

        /// <summary>
        /// 造一条**曲线与其余字段全部合法**的条目,峰值 = `aPeakPercent / 100`。
        /// 用自限型(`self_limit = true`)绕开 R1-22 / R1-34 / R1-35 三个致死门 ——
        /// 本文件要单独分离 R1-37 的判别力,不能被别的门抢先抛。
        /// </summary>
        private static DiseaseRegistryEntry Entry(int aPeakPercent, SignEntry[] signs)
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 63,                             // R1-33:须 ∈ [1, 64]
                DiseaseKey = "SYN_SIGN_GATE",
                Kind = DiseaseKind.Disease,
                Polarity = 2,
                Severity = 2,
                Contagion = 0,
                Lethality = 0,
                TreatmentDifficulty = 1,
                RecoveryTime = 100,
                Scale = new Fix(100 * Fix.OneRaw),          // SCALE = 100 ⇒ 峰值 raw 可精确表达
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(aPeakPercent * Fix.OneRaw),
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(100 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = true,
                    BoundaryMode = "monotone",
                },
                Signs = signs,
            };
        }

        /// <summary>把 appear_at 表达成「分母 100」的定点值(便于夹具里直读百分比)。</summary>
        private static Fix Percent(int hundredths) => new Fix(hundredths * Fix.OneRaw / 100L);

        private static SignEntry Sign(string key, int channelMask, int appearAtHundredths)
            => new SignEntry(key, channelMask, Percent(appearAtHundredths));

        private static void AssertR137(DiseaseRegistryEntry entry, string why)
        {
            var ex = Assert.Throws<RegistryValidationException>(
                () => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { entry }), why);
            Assert.That(ex.RuleNumber, Is.EqualTo(RegistrySchemaValidator.R1_CHECK_37),
                $"{why} —— 抛的应是 R1-37;实际 {ex.RuleNumber}" +
                "(被别的门抢先 ⇒ 本夹具前提不成立,不能算 R1-37 的判别力)");
        }

        // ══════════════════ ① 绝对域 ══════════════════
        //
        // ⚠️ 本组**刻意移除曲线块**(Curve = null)：R1-37 的两条判据共用同一个错误码,
        //   若保留曲线,`appear_at = 1.50` 既违反域上界、又高于峰值 0.50 ⇒ 停用域判据后
        //   仍会被可达性判据以同一个 R1-37 拒掉 ⇒ 夹具对域判据**零判别力**。
        //   (本条由证伪轮 M1 实测暴露:停用可达性时这两条仍绿,看似"域判据也在守",
        //    实际是把两条判据的判别力混在了一个错误码上。)
        //   Curve 缺席时可达性判据不参与 ⇒ 这两条只钉绝对域。

        private static DiseaseRegistryEntry DomainOnly(SignEntry[] signs)
        {
            DiseaseRegistryEntry e = Entry(50, signs);
            e.Curve = null;
            return e;
        }

        [Test]
        public void test_appearAtZero_rejected()
        {
            // 恰好 0 = 「出生即在场」⇒ 通道掩码恒满,与「配了没判别力」同型。
            AssertR137(DomainOnly(new[] { Sign("sign_probe", SignChannels.Face, 0) }),
                "appear_at = 0 应被 R1-37 拒(恒在场)");
        }

        [Test]
        public void test_appearAtAboveOne_rejected()
        {
            AssertR137(DomainOnly(new[] { Sign("sign_probe", SignChannels.Face, 150) }),
                "appear_at = 1.50 应被 R1-37 拒(域上界;无曲线 ⇒ 只有域判据能拒它)");
        }

        // ══════════════════ ② 可达性(本件的核心) ══════════════════

        [Test]
        public void test_appearAtExactlyAtPeak_rejected()
        {
            // 峰值 = A_peak/SCALE = 0.50;appear_at 恰等于 0.50。
            // 判据是**严格小于**(`>=` 即拒)—— 因为波形在峰值处只是**瞬时**触及,
            // 而 `position` 是 clamp 后的定点量,恰等于时是否可观测取决于采样相位
            // ⇒ 不允许把「恰好够不着」这种边界留给实现期。
            AssertR137(Entry(50, new[] { Sign("sign_probe", SignChannels.Face, 50) }),
                "appear_at 恰等于峰值应被 R1-37 拒(边界为严格 <)");
        }

        [Test]
        public void test_appearAtAbovePeak_rejected()
        {
            // 峰值 0.50,阈值 0.75 —— 这正是 2026-10-30 实测出的死词条形态
            // (`DIS_MALARIA` 的 jaundice 0.60 vs 峰值 0.45 同型)。
            AssertR137(Entry(50, new[] { Sign("sign_probe", SignChannels.Face, 75) }),
                "appear_at > 峰值应被 R1-37 拒(死词条)");
        }

        [Test]
        public void test_appearAtBelowPeak_accepted()
        {
            // 峰值 0.50,阈值 0.30 ⇒ 可达,不得抛。
            Assert.DoesNotThrow(
                () => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry>
                {
                    Entry(50, new[] { Sign("sign_probe", SignChannels.Face, 30) }),
                }),
                "appear_at < 峰值是合法配置,门不得误拒");
        }

        [Test]
        public void test_curveAbsent_skipsReachabilityButKeepsDomain()
        {
            // 可达性依赖 A_peak 与 SCALE,两者都由曲线块供给 ⇒ Curve 缺席时无从计算。
            // 既有条目的 Curve 可缺席(本循环独立于 curve 块),此时**只**守绝对域。
            DiseaseRegistryEntry noCurve = Entry(50, new[] { Sign("sign_probe", SignChannels.Face, 75) });
            noCurve.Curve = null;
            Assert.DoesNotThrow(
                () => RegistrySchemaValidator.Validate(new List<DiseaseRegistryEntry> { noCurve }),
                "Curve 缺席时不得因可达性拒(峰值无从计算)");

            DiseaseRegistryEntry noCurveZero = Entry(50, new[] { Sign("sign_probe", SignChannels.Face, 0) });
            noCurveZero.Curve = null;
            AssertR137(noCurveZero, "Curve 缺席时绝对域仍须生效");
        }
    }
}
