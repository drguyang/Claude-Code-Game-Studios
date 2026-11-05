// 钉住「`DIS_MALARIA` 恰好压线到昏迷」这条判据 —— 2026-11-05 余量勘察轮。
//
// 权威:GDD `disease-simulation.md` F4(昏迷分支)· R3.3(疟疾行)· ADR-005(Fix 域纪律)
//
// ⚠️ **为什么必须有这条**:`a_peak/scale = 0.45` 与 `COMA_THRESHOLD = 29491/65536 ≈ 0.44999695`
//    只差 **3.05×10⁻⁶ = 0.2 ULP**。运行时的 Q16.16 除法把 0.45 舍到 **29491**(恰等于阈值),
//    比较用 `>=` 才成立 ⇒ **昏迷支是靠「整数恰好落在线上」成立的,不是靠余量**。
//    而此前**没有任何夹具**覆盖它 ⇒ 谁把阈值 +1、或把 `a_peak` 改成 44/`scale` 改成 101,
//    这条支会**静默消失**,而热性惊厥那个 2×2 案子的对照半边随之塌掉。
//
// ⚠️ 本件**读真表**(`assets/data/disease_registry.json` 走真 binder),
//    因为要钉的正是「真数据算出来是多少」——内联夹具会让夹具与真表脱钩。

using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>疟疾峰值与 `COMA_THRESHOLD` 的余量(0.2 ULP)—— 钉住昏迷支可达性。</summary>
    public sealed class malaria_coma_margin_test
    {
        private const long OneRaw = 1L << 16;
        private const int ComaThresholdLiteral = 29491;   // 字面量,不用被测常量(防自指判据)

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static DiseaseRegistryEntry Malaria()
        {
            DiseaseRegistryBaker.BakeOutput outp = DiseaseRegistryBaker.BakeFromRepo(RepoRoot);
            for (int i = 0; i < outp.Rows.Count; i++)
                if (string.Equals(outp.Rows[i].DiseaseKey, "DIS_MALARIA", System.StringComparison.Ordinal))
                    return outp.Rows[i];
            Assert.Fail("真表里找不到 DIS_MALARIA —— 本测的靶子不存在(别改成别的病种蒙混过去)");
            return null;
        }

        /// <summary>F2 投影:`clamp(Progress / SCALE, 0, 1)`(与 `GetVitals` / `EvaluateOutcome` 同式)。</summary>
        private static long PositionRaw(DiseaseRegistryEntry reg, long atTick)
        {
            ProgressionResult r = ProgressionEvaluator.Evaluate(
                reg, onsetTick: 0L, currentTick: atTick,
                events: System.Array.Empty<SimEvent>(),
                worldSeed: 0x5EEDUL, patientId: new PatientId(1));
            Fix pos = r.Position / reg.Scale;          // ⚠️ `Evaluate` 返回的是 progress,不是 position
            if (pos.Raw < Fix.ZeroRaw) return Fix.ZeroRaw;
            if (pos.Raw > OneRaw) return OneRaw;
            return pos.Raw;
        }

        // ── ① 阈值本身:字面量哨兵 ────────────────────────────────────────

        [Test]
        public void test_comaThreshold_isExactly29491()
        {
            // 与下面「位置 == 29491」配对才构成判据:
            // 单看任一条件都可能因为「两边一起改」而恒真(自指判据)。
            Assert.AreEqual(ComaThresholdLiteral, StateMachine.COMA_THRESHOLD,
                "COMA_THRESHOLD 变了 ⇒ 疟疾的昏迷支会静默消失(它还差 0.2 ULP 才越过旧阈值)");
        }

        // ── ② 数据本身:0.45 与 100 ─────────────────────────────────────

        [Test]
        public void test_malariaPeakOverScale_isFortyFiveOverAHundred()
        {
            DiseaseRegistryEntry m = Malaria();

            Assert.AreEqual(45L * OneRaw, m.Curve.APeak.Raw, "a_peak 变了(44 ⇒ 位置 28835,昏迷支消失)");
            Assert.AreEqual(100L * OneRaw, m.Scale.Raw, "scale 变了(101 ⇒ 位置 29197,昏迷支消失)");

            // 精确有理数比较(不动点):a_peak/scale ? COMA
            //   45/100 = 0.45000000    vs    29491/65536 = 0.44999695
            //   ⇔ 45 × 65536 = 2949120  vs  29491 × 100 = 2949100  ⇒ **严格大于,差 20/6553600**
            Assert.AreEqual(2949120L, 45L * 65536L, "算术哨兵(常量折叠,防手误)");
            Assert.AreEqual(2949100L, ComaThresholdLiteral * 100L, "算术哨兵(常量折叠,防手误)");
            Assert.That(45L * 65536L, Is.GreaterThan(ComaThresholdLiteral * 100L),
                "精确值必须**严格高于**阈值 —— 若这条变假,昏迷支在有理数层面就不存在");
        }

        // ── ③ 运行时的舍入结果:恰好等于阈值 ───────────────────────────

        [Test]
        public void test_positionAtPeak_roundsToExactlyTheThreshold()
        {
            DiseaseRegistryEntry m = Malaria();
            long atPeak = PositionRaw(m, m.Curve.TauPeak);

            // 0.45 × 65536 = 29491.2 ⇒ ROUND_HALF_AWAY_FROM_ZERO ⇒ **29491**(余数 0.2 ULP)
            Assert.AreEqual(ComaThresholdLiteral, atPeak,
                "峰值位置必须**恰好等于**阈值 29491。若它变成 29490,昏迷支消失;若变成 29492," +
                "说明余量被人为放大(那会改变这个案子的脆弱性描述,须同批更新证据件)");
        }

        // ── ④ 可判窗口:峰值前后各多少 ─────────────────────────────────

        [Test]
        public void test_comaWindowIsNarrow_closesOneTickAfterPeak()
        {
            DiseaseRegistryEntry m = Malaria();
            long peak = m.Curve.TauPeak;

            // 峰值附近(上升段末)已在线上 —— 归一化构造上 (1-e)/R_rise → 1
            Assert.That(PositionRaw(m, peak - 1), Is.GreaterThanOrEqualTo(ComaThresholdLiteral),
                "峰值前一 tick 应仍在昏迷带上");
            Assert.AreEqual(ComaThresholdLiteral, PositionRaw(m, peak), "峰值 tick 在线上");

            // 自限型 ⇒ 峰值是**尖的**不是平台 ⇒ 之后立刻掉下来
            Assert.That(PositionRaw(m, peak + 1), Is.LessThan(ComaThresholdLiteral),
                "自限型在峰值后一个 tick 就跌破阈值 —— 昏迷窗口只有 1–2 tick(20 Hz 下 50–100 ms)");

            // 窗口之所以这么窄,是因为它**没有平台**:plateau 的两种病(心衰 / 伤寒)才持续在线上。
            // 这条把「尖峰」与「平台」的差钉成判据,防止有人把疟疾改成 plateau。
            Assert.That(m.Curve.Plateau, Is.False, "疟疾不是平台型 —— 改了就没有『尖峰窗口』这个性质");
            Assert.That(m.Curve.SelfLimit, Is.True, "疟疾是自限型 —— 这是峰值后立刻下跌的原因");
        }
    }
}
