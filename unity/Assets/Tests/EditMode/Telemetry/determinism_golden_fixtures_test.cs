// 权威来源:production/epics/telemetry-analytics/story-008-pure-function-recomputable.md
//   AC-51-C3(ADVISORY)—— 「可进 ADR-012 黄金夹具:三格矩阵跑 51 的折叠,哈希一致」
//   · ADR-012 §一(双级黄金夹具:单元级哈希 / 集成级字节)· §三(版本化刷新)
//   · ADR-006 §三(ROUND_HALF_AWAY_FROM_ZERO 唯一舍入)· GDD telemetry-analytics R7(整数纪律)
//
// ⚠️ 落点:账本路径 = `tests/golden/telemetry/`(与 item_database / medcons 同族);
//    Unity 只编译 `unity/Assets/` 树 ⇒ **真身 = 本文件**,账本侧由 README 互链。
//
// ⚠️ **防自指**(AC-51-C3 的核心纪律):金标准 `golden-v1.txt` 由**独立 Python 参考实现**
//    `golden_v1_reference.py`(同目录,与 C# 零共享代码)产出并入库。本测试**只读不写** ——
//    若由 C# 首跑生成金标准,那这条夹具恒过,等于没测。
// ⚠️ **刷新纪律**(ADR-012 §三):改公式 ⇒ 升 `golden-v2` + 变更日志 + 全体平台同时重签;
//    **禁**就地改老数。任一行变红 = 实现漂移或参考漂移,须双向核对。
//
// ⚠️ **本文件只覆盖 Mono(EditMode)格**。IL2CPP 三格(Linux-x64 / Linux-ARM64 / Windows-x64)
//    属 ADR-012 常驻矩阵 —— 本机仅有 Mono,那三格**如实 NOT-RUN**,见 story-008 的登记。
//
// 测试纪律:test_[scenario]_[expected];确定性(无随机、无墙钟);文件 I/O = 只读金标准。
// ⚠️ **对 `.claude/rules/test-standards.md`「单元测试不得依赖文件系统」的显式豁免**:
//    黄金夹具的**判据本体就是入库的字节**,不读盘就无从对拍。故本件只做**只读** I/O,
//    **零写入 / 零建目录 / 零删除**(pipeline_gate_test 的建目录副作用正是该条规的正面反例);
//    缺文件 = **硬红**,不跳过。同族先例:ItemDatabase / MedCons 的 determinism_golden_fixtures_test。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class TelemetryGoldenFixturesTest
    {
        private const string GoldenFileName = "golden-v1.txt";

        // ══════════════ 金标准装载(只读)══════════════

        /// <summary>读金标准并解析成 `键 → 值` 字典。缺文件 = **红**(不静默跳过)。</summary>
        private static Dictionary<string, string> LoadGolden()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "tests", "golden", "telemetry", GoldenFileName);

            Assert.That(File.Exists(path), Is.True,
                $"金标准缺失:{path} —— AC-51-C3 的载体不在盘上,判据无效(不得跳过冒充通过)");

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int sp = line.IndexOf(' ');
                Assert.That(sp, Is.GreaterThan(0), $"金标准行格式非法(缺空格分隔):{raw}");
                map[line.Substring(0, sp)] = line.Substring(sp + 1);
            }
            Assert.That(map.Count, Is.GreaterThan(0), "金标准为空 ⇒ 判据无效");
            return map;
        }

        // ══════════════ T1:ROUND_HALF_AWAY_FROM_ZERO ══════════════

        /// <summary>ADR-006 §三的统一舍入 —— 12 组边界向量与金标准逐位相同。
        /// <para><b>判别力</b>:换成 ties-to-even(`Math.Round` 默认)⇒ R02 / R04 立即红。</para></summary>
        [Test]
        public void test_golden_t1_roundHalfAwayFromZero_matchesReference()
        {
            Dictionary<string, string> golden = LoadGolden();

            var cases = new (string name, long num, long den)[]
            {
                ("R01_positive_exact", 7, 2),
                ("R02_positive_below", 5, 2),
                ("R03_negative_exact", -7, 2),
                ("R04_negative_below", -5, 2),
                ("R05_zero_numerator", 0, 7),
                ("R06_zero_denominator", 5, 0),
                ("R07_both_negative", -3, -4),
                ("R08_neg_over_pos", -2, 3),
                ("R09_third", 1, 3),
                ("R10_two_thirds", 2, 3),
                ("R11_large", 123456789, 1000),
                ("R12_tie_away_at_scale", 999999999999, 2),
            };

            var drift = new List<string>();
            foreach (var (name, num, den) in cases)
            {
                Assert.That(golden.ContainsKey(name), Is.True, $"金标准缺行 {name}");
                long want = long.Parse(golden[name]);
                long got = MetricsReport.RoundHalfAwayFromZero(num, den);
                if (got != want) drift.Add($"{name}: want {want} got {got}");
            }

            Assert.That(drift, Is.Empty, "舍入与金标准漂移:\n" + string.Join("\n", drift));
        }

        // ══════════════ T2:Q16.16 标量导出 ══════════════

        /// <summary>Q16.16 导出 —— 8 组向量 + 分母零标记与金标准逐位相同。
        /// <para><b>判别力</b>:去掉显式舍入(裸截断除法)⇒ Q01 由 43691 变 43690 ⇒ 红。</para></summary>
        [Test]
        public void test_golden_t2_toQ1616_matchesReference()
        {
            Dictionary<string, string> golden = LoadGolden();

            var cases = new (string name, long num, long den)[]
            {
                ("Q01_two_thirds", 2, 3),
                ("Q02_one_third", 1, 3),
                ("Q03_full", 1, 1),
                ("Q04_zero", 0, 5),
                ("Q05_zero_den", 3, 0),
                ("Q06_half", 1, 2),
                ("Q07_negative_half", -1, 2),
                ("Q08_three_halves", 3, 2),
            };

            var drift = new List<string>();
            foreach (var (name, num, den) in cases)
            {
                long got = MetricsReport.ToQ1616(num, den, out bool zero);
                long want = long.Parse(golden[name]);
                if (got != want) drift.Add($"{name}: want {want} got {got}");

                string zKey = name + "_z";
                Assert.That(golden.ContainsKey(zKey), Is.True, $"金标准缺行 {zKey}");
                bool wantZero = golden[zKey] == "1";
                if (zero != wantZero) drift.Add($"{zKey}: want {wantZero} got {zero}");
            }

            Assert.That(drift, Is.Empty, "Q16.16 导出与金标准漂移:\n" + string.Join("\n", drift));
        }

        // ══════════════ T3:F1 指标 ══════════════

        /// <summary>F1a / F1b 整数对 + 分母零标记 —— 三组输入与金标准逐位相同。
        /// <para><b>判别力</b>:把「落笔判空集」当「无判断」排除出分母 ⇒ F1_allWrong_a 由 0,2 变 0,1 ⇒ 红。</para></summary>
        [Test]
        public void test_golden_t3_f1Metrics_matchesReference()
        {
            Dictionary<string, string> golden = LoadGolden();

            CheckF1(golden, "F1_typical", BuildRecords(
                (new[] { "1" }, new[] { "1" }, false),
                (new[] { "2" }, new[] { "3" }, false),
                (new[] { "1" }, new[] { "1" }, true)));

            CheckF1(golden, "F1_empty", new List<JudgmentRecord>());

            CheckF1(golden, "F1_allWrong", BuildRecords(
                (new[] { "1" }, Array.Empty<string>(), true),   // 落笔判空集 ⇒ 进分母且判错
                (new[] { "2" }, new[] { "1" }, false)));
        }

        private static void CheckF1(Dictionary<string, string> golden, string label,
                                    IReadOnlyList<JudgmentRecord> records)
        {
            JudgmentMetrics.Result r = JudgmentMetrics.Compute(records);

            string aKey = label + "_a", bKey = label + "_b", zKey = label + "_z";
            foreach (string k in new[] { aKey, bKey, zKey })
                Assert.That(golden.ContainsKey(k), Is.True, $"金标准缺行 {k}");

            Assert.That($"{r.F1aNum},{r.F1aDen}", Is.EqualTo(golden[aKey]), $"{aKey} 漂移");
            Assert.That($"{r.F1bNum},{r.F1bDen}", Is.EqualTo(golden[bKey]), $"{bKey} 漂移");
            Assert.That(r.DenominatorZero ? "1" : "0", Is.EqualTo(golden[zKey]), $"{zKey} 漂移");
        }

        private static IReadOnlyList<JudgmentRecord> BuildRecords(
            params (string[] truth, string[] judged, bool revised)[] rows)
        {
            var list = new List<JudgmentRecord>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                var (truth, judged, revised) = rows[i];
                list.Add(new JudgmentRecord(
                    i + 1,
                    new HashSet<string>(truth, StringComparer.Ordinal),
                    new HashSet<string>(judged, StringComparer.Ordinal),
                    revised));
            }
            return list;
        }

        // ══════════════ T4:五元组计数折叠 ══════════════

        /// <summary>空白与退化场景的折叠 —— 6 组向量与金标准逐位相同。
        /// <para><b>判别力</b>:空流改报哨兵值(如 minTick = long.MaxValue)⇒ K02 红。</para></summary>
        [Test]
        public void test_golden_t4_tickFold_matchesReference()
        {
            Dictionary<string, string> golden = LoadGolden();

            var cases = new (string name, int[] ticks)[]
            {
                ("K01_typical", new[] { 10, 20, 30, 40 }),
                ("K02_empty", Array.Empty<int>()),
                ("K03_single", new[] { 7 }),
                ("K04_negative", new[] { -5, 0, 5 }),
                ("K05_duplicates", new[] { 3, 3, 3 }),
                ("K06_large", new[] { 1, 1000000000, 999999999 }),
            };

            var drift = new List<string>();
            foreach (var (name, ticks) in cases)
            {
                Assert.That(golden.ContainsKey(name), Is.True, $"金标准缺行 {name}");
                var (total, casesCount, sum, min, max) = TickFoldEngine.Fold(ticks);
                string got = $"{total},{casesCount},{sum},{min},{max}";
                if (got != golden[name]) drift.Add($"{name}: want {golden[name]} got {got}");
            }

            Assert.That(drift, Is.Empty, "五元组折叠与金标准漂移:\n" + string.Join("\n", drift));
        }

        // ══════════════ 防自指:金标准不是 C# 跑出来的 ══════════════

        /// <summary>AC-51-C3 的**防自指判据**:金标准必须由独立 Python 参考实现产出。
        /// <para><b>判据</b>:同目录存在 `golden_v1_reference.py`,且其输出与本文件所读金标准一致。
        /// 若金标准由 C# 首跑生成(或参考实现被删),本夹具就退化成「自己对自己」。</para>
        /// <para>⚠️ 本测试**不执行** Python(EditMode 单元测试不得起外部进程)——
        /// 它只断言参考实现**在盘上**,执行对拍归 `python3 golden_v1_reference.py --check`(CI/人工)。</para></summary>
        [Test]
        public void test_golden_referenceImplementationExists_notSelfReferential()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string dir = Path.Combine(repoRoot, "tests", "golden", "telemetry");

            Assert.That(File.Exists(Path.Combine(dir, GoldenFileName)), Is.True,
                $"金标准缺失:{GoldenFileName}");
            Assert.That(File.Exists(Path.Combine(dir, "golden_v1_reference.py")), Is.True,
                "独立参考实现 `golden_v1_reference.py` 不在盘上 —— 金标准失去非自指来源," +
                "AC-51-C3 退化为「C# 自己对自己」");

            string py = File.ReadAllText(Path.Combine(dir, "golden_v1_reference.py"));
            Assert.That(py, Does.Contain("与 C# 实现零共享代码"),
                "参考实现须自陈零共享代码(ADR-012 §一 防自指口径)");
        }
    }
}
