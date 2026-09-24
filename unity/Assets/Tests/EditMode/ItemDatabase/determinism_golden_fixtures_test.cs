// 权威来源:Story 011(production/epics/item-database/story-011-determinism-golden-fixtures.md)
//     · AC-21a-28(边界组合逐条 F1/F2/F5 求解,SplitMix64 哈希 vs 已提交金标准逐位相同;
//       金标准非首跑生成 —— 独立 Python 参考实现产出)
//     · AC-21a-30(F1–F5 全部中间变量静态扫描:零 float/double、零 Math.Round/Exp/Pow/Sqrt、
//       零浮点字面量;`ToFloat()` 白名单 = facade 定义自身住 Sim.Contracts,不在扫描面;
//       Sim 内出现 `.ToFloat(` = 违 ADR-025 QQ-03 甲案)
//   GDD:design/gdd/item-database.md §Formulas F1/F2/F5 · ADR-012(双级黄金夹具 · 版本化刷新)
//        · ADR-005(整数定点域,存储零 float)· ADR-006 §三(half-away)· ADR-010(codec 布局)
//
// ⚠️ 落点:账本路径 = tests/unit/item_database/determinism_golden_fixtures_test.cs(GDD 指名);
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(Story 001–010 同一先例)。
// ⚠️ 金标准 = tests/unit/item_database/golden/golden-v1.txt,由**独立 Python 参考实现**
//    (同目录 golden_v1_reference.py,与 C# 零共享代码)产出并入库 —— 本测试**只读不写**
//    (AC-28 防自指:金标准经由 C# 首跑生成 = 恒过)。刷新纪律 = 升 golden-v2 + 全平台重签。
// ⚠️ AC-29(跨平台对拍)不在本文件 —— BLOCKED-BY-实测(IL2CPP player + F7 spike),
//    禁以 Mono 单侧结果借绿(故事头 Guardrail)。
//
// 测试纪律:test_[scenario]_[expected];确定性(无随机、无墙钟);文件 I/O = 只读金标准。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class DeterminismGoldenFixturesTest
    {
        private const string GoldenFileName = "golden-v1.txt";

        // ══════════════ 常量表夹具(与参考实现 golden_v1_reference.py 逐字对应)══════════════

        private static RecipeSettlementConstants FullCaps() => new RecipeSettlementConstants(
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
            maxQuality: 5,
            skillCap: 60);

        private static RecipeSettlementConstants ZeroCaps() => new RecipeSettlementConstants(
            qtyMultMin: FixParse.Parse("1/4"),
            qtyMultMax: FixParse.Parse("2"),
            skillModCap: FixParse.Parse("0"),      // 所有修正 cap 取 0(AC-28 边界)
            qualModCap: FixParse.Parse("0"),
            equipModCap: FixParse.Parse("0"),
            envModMin: FixParse.Parse("-1"),
            envModMax: FixParse.Parse("1/2"),
            retainMin: FixParse.Parse("1/2"),
            retainMax: FixParse.Parse("1"),
            effMin: FixParse.Parse("1/2"),
            effMax: FixParse.Parse("1"),
            maxQuality: 5,
            skillCap: 60);

        // ══════════════ canonical 哈希(与金标准文件头口径一致)══════════════

        /// <summary>值级:state = u64(v0);逐后续 Fold;终态 Avalanche 一次。</summary>
        private static string HashValues(IReadOnlyList<long> values)
        {
            Assert.That(values, Is.Not.Empty, "哈希向量不得为空");
            ulong state = unchecked((ulong)values[0]);
            for (int i = 1; i < values.Count; i++)
                state = SplitMix64.Fold(state, unchecked((ulong)values[i]));
            return SplitMix64.Avalanche(state).ToString("x16");
        }

        /// <summary>字节级:state = 0;逐 byte Fold;终态 Avalanche 一次。</summary>
        private static string HashBytes(byte[] bytes)
        {
            Assert.That(bytes, Is.Not.Empty, "字节流不得为空");
            ulong state = 0UL;
            foreach (byte b in bytes)
                state = SplitMix64.Fold(state, b);
            return SplitMix64.Avalanche(state).ToString("x16");
        }

        // ══════════════ 场景表(与 golden_v1_reference.py SCENARIOS 逐名对应)══════════════

        private sealed class Scenario
        {
            public readonly string Name;
            public readonly RecipeSettlementConstants Constants;
            public readonly int Skill;
            public readonly int Quality;
            public readonly int[] Outputs;
            public readonly int[] Inputs;
            public readonly Fix Equip;
            public readonly Fix Climate;
            public readonly Fix Clinic;

            public Scenario(string name, RecipeSettlementConstants constants, int skill, int quality,
                int[] outputs, int[] inputs, Fix equip, Fix climate, Fix clinic)
            {
                Name = name; Constants = constants; Skill = skill; Quality = quality;
                Outputs = outputs; Inputs = inputs; Equip = equip; Climate = climate; Clinic = clinic;
            }
        }

        private static readonly Fix Neg1 = FixParse.Parse("-1");
        private static readonly Fix Half = FixParse.Parse("1/2");
        private static readonly Fix Pos1 = FixParse.Parse("1");
        private static readonly Fix Zero = FixParse.Parse("0");

        private static Scenario[] BuildScenarios() => new[]
        {
            new Scenario("S01_full_skill0_q3_m1",      FullCaps(), 0,  3, new[]{2},    new[]{3,7,1}, Zero,   Zero,   Zero),
            new Scenario("S02_full_skill60_q5_m1",     FullCaps(), 60, 5, new[]{2},    new[]{3,7,1}, Zero,   Zero,   Zero),
            new Scenario("S03_full_skill0_q1_n1m1",    FullCaps(), 0,  1, new[]{1},    new[]{5},     Zero,   Zero,   Zero),
            new Scenario("S04_full_skill30_q5_m2",     FullCaps(), 30, 5, new[]{2,3},  new[]{4,6},   Zero,   Zero,   Zero),
            new Scenario("S05_full_skill0_q3_envMin",  FullCaps(), 0,  3, new[]{1,2},  new[]{3,7,1}, Zero,   Neg1,   Zero),
            new Scenario("S06_full_skill60_q5_envMax", FullCaps(), 60, 5, new[]{2},    new[]{3,7,1}, Zero,   Half,   Zero),
            new Scenario("S07_full_envMinBoth_restFull", FullCaps(), 0, 1, new[]{1},    new[]{5},     Zero,   Neg1,   Neg1),
            new Scenario("S08_full_envMaxBoth_restFull", FullCaps(), 60, 5, new[]{2,3}, new[]{4,6},   Half,   Half,   Half),
            new Scenario("S09_zero_caps_floor",        ZeroCaps(), 0,  1, new[]{1},    new[]{5},     Zero,   Neg1,   Zero),
            new Scenario("S10_zero_caps_envOverMax",   ZeroCaps(), 60, 5, new[]{2,2},  new[]{3,3},   Zero,   Pos1,   Zero),
            new Scenario("S11_zero_caps_mid",          ZeroCaps(), 30, 3, new[]{3},    new[]{3,7,1}, Zero,   Zero,   Zero),
            new Scenario("S12_full_skill60_q1_m1",     FullCaps(), 60, 1, new[]{2},    new[]{3,7,1}, Zero,   Zero,   Zero),
        };

        private static long[] SolveVector(in Scenario s)
        {
            var request = new RecipeSettlementRequest(
                s.Outputs.Select(q => new RecipeEntry(new ItemKey("out_golden", ProcessingState.Raw), q)).ToArray(),
                s.Inputs.Select(q => new RecipeEntry(new ItemKey("in_golden", ProcessingState.Raw), q)).ToArray(),
                s.Skill, s.Quality, s.Equip, s.Climate, s.Clinic);
            RecipeSettlementResult r = RecipeSettlementSolver.Solve(request, s.Constants);

            var vector = new List<long> { r.OutputQuality };
            vector.AddRange(r.OutputQty.Select(v => (long)v));
            vector.AddRange(r.ActualConsumed.Select(v => (long)v));
            vector.Add(r.Efficiency.Raw);
            vector.Add(r.QtyMultiplier.Raw);
            vector.Add(r.EnvModTotal.Raw);
            vector.Add(r.SumOfModifiers.Raw);
            return vector.ToArray();
        }

        // ══════════════ F5 场景(profile 字段序 = onset, peak, half_life, elimination)══════════════

        private static readonly DrugProfile F5Profile = new DrugProfile
        {
            Onset = FixParse.Parse("1"),
            Peak = FixParse.Parse("2"),
            HalfLife = FixParse.Parse("5"),
            Elimination = FixParse.Parse("10"),
            QualityAxis = QualityAxis.HalfLife,
            AxisOffsetByQuality = new[]
            {
                FixParse.Parse("0"), FixParse.Parse("3/8"), FixParse.Parse("-1/4"),
                FixParse.Parse("1/4"), FixParse.Parse("1/2"),
            },
        };

        private static long[] F5Vector(DrugProfile profile, int quality) =>
            F5FromTimeline(QualityTimelineSolver.ApplyQualityTimeline(profile, quality));

        private static long[] F5Passthrough(DrugProfile profile) =>
            F5FromTimeline(QualityTimelineSolver.ApplyQualityTimeline(profileWithoutF5(profile), 3));

        private static long[] F5FromTimeline(EffectiveTimeline t) => new[]
        {
            t.Onset?.Raw ?? -1L, t.Peak?.Raw ?? -1L,
            t.HalfLife?.Raw ?? -1L, t.Elimination?.Raw ?? -1L,
        };

        private static DrugProfile profileWithoutF5(DrugProfile p) => new DrugProfile
        {
            Onset = p.Onset, Peak = p.Peak, HalfLife = p.HalfLife, Elimination = p.Elimination,
            // QualityAxis / AxisOffsetByQuality 不设 ⇒ 四轴原样透传
        };

        // ══════════════ 集成级字节样本(与 golden_v1_reference.py BYTE_SCENARIOS 同字面)══════════════

        private static byte[] CraftSampleBytes() => PayloadCodec.Encode(new CraftPayload(
            1, 6, 320L, 40L,
            new long[] { 3001L, 3002L }, new[] { 1, 2 }, new[] { 3 }, new[] { 1 },
            new long[] { 4001L }, new WorldPos(2, 2, 2)));

        private static byte[] InstanceSampleBytes() => ItemInstanceCodec.Encode(new ItemInstance(
            7L, new ItemKey("willow_bark", ProcessingState.Dried), 3, 5, new long[] { 8L, 9L }));

        private static byte[] HeaderSampleBytes() => SimEventCodec.Encode(new SimEvent(
            100L, new PatientId(7), 5L, EventKind.Craft, new PayloadRef(0, 0, 30)));

        // ══════════════ 全量计算(名字 → 十六进制)══════════════

        private static Dictionary<string, string> ComputeAll()
        {
            var computed = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Scenario s in BuildScenarios())
                computed[s.Name] = HashValues(SolveVector(s));

            computed["S13_f5_halfLife_q1_offset0"]   = HashValues(F5Vector(F5Profile, 1));
            computed["S14_f5_halfLife_q5_offsetMax"] = HashValues(F5Vector(F5Profile, 5));
            computed["S15_f5_halfLife_q3_offsetNeg"] = HashValues(F5Vector(F5Profile, 3));
            computed["S16_f5_noAxis_passthrough"]    = HashValues(F5Passthrough(F5Profile));

            computed["B01_craft_payload_bytes"]    = HashBytes(CraftSampleBytes());
            computed["B02_item_instance_bytes"]    = HashBytes(InstanceSampleBytes());
            computed["B03_sim_event_header_bytes"] = HashBytes(HeaderSampleBytes());

            return computed;
        }

        private static Dictionary<string, string> LoadGolden()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "tests", "unit", "item_database", "golden", GoldenFileName);

            Assert.That(File.Exists(path), Is.True,
                $"金标准缺失:{path}(由独立 Python 参考实现产出;测试只读不写 —— AC-28 防自指)");

            var golden = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                Assert.That(parts.Length, Is.EqualTo(2), $"金标准行格式非法: {raw}");
                golden[parts[0]] = parts[1];
            }
            return golden;
        }

        // ══════════════ AC-21a-28 ══════════════

        [Test]
        public void test_ac21a28_allScenarioHashes_matchGoldenV1()
        {
            Dictionary<string, string> computed = ComputeAll();
            Dictionary<string, string> golden = LoadGolden();

            CollectionAssert.AreEquivalent(
                golden.Keys, computed.Keys,
                "金标准条目集 ≠ 计算条目集 —— 场景表与 golden-v1.txt 脱钩(改表须升 golden-v2 全平台重签)");

            foreach (KeyValuePair<string, string> kv in computed)
            {
                Assert.That(golden[kv.Key], Is.EqualTo(kv.Value),
                    $"[{kv.Key}] 哈希与金标准逐位不符 —— 确定性回归(篡改/漂移即红;" +
                    "红了先查实现,金标准刷新须全体平台同时重签,禁单平台独签 —— ADR-012 裁决③)");
            }
        }

        [Test]
        public void test_ac21a28_tamperedGoldenOneBit_detected()
        {
            // QA 冒烟反证:篡改任一位应红(负向 fixture = 无;夹具即金标准)。
            Dictionary<string, string> computed = ComputeAll();
            string name = "S01_full_skill0_q3_m1";
            string honest = computed[name];
            char flipped = honest[honest.Length - 1] == '0' ? '1' : '0';
            string tampered = honest.Substring(0, honest.Length - 1) + flipped;

            Assert.That(tampered, Is.Not.EqualTo(honest), "篡改必产生不同期望值(冒烟前提)");
            Assert.That(computed[name], Is.Not.EqualTo(tampered),
                $"[{name}] 篡改金标准一位必须被抓(逐位比对)—— 比对面失效即 AC-28 恒过风险");
        }

        [Test]
        public void test_ac21a28_goldenHeader_carriesProvenanceMetadata()
        {
            // 评审检查项:金标准带「独立参考实现产出」元数据(非首跑自动冻结 —— AC-28)。
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "tests", "unit", "item_database", "golden", GoldenFileName);
            string head = File.ReadAllText(path);

            Assert.That(head.Contains("golden_v1_reference.py"), Is.True,
                "金标准须声明独立 Python 参考实现出处(防自指 ⇒ 恒过)");
            Assert.That(head.Contains("golden-v2") || head.Contains("golden-vN"), Is.True,
                "金标准须载版本化刷新纪律(ADR-012 裁决③:升版本 + 全平台重签 + 旧版保留)");
        }

        // ══════════════ AC-21a-30:静态扫描(语法级;排除注释与字符串)══════════════

        [Test]
        public void test_ac21a30_f1f5Sources_scanForFloatAndLibm_noneFound()
        {
            string simDir = Path.Combine(Application.dataPath, "Sim");
            Assert.That(Directory.Exists(simDir), Is.True, $"扫描面缺失: {simDir}");

            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories))
                violations.AddRange(ScanSource(Path.GetFileName(file), File.ReadAllText(file)));

            Assert.That(violations, Is.Empty,
                "AC-30 静态扫描红(ADR-005 存储零 float / ADR-026 禁 libm / ADR-025 QQ-03 甲案):\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void test_ac21a30_scanner_selfProvesEachTokenClassDetected()
        {
            // 与真扫描同一谓词(QA 边例:注入含违例的临时内容应红 —— 内存形,免临时文件)。
            const string synthetic = @"
class Probe {
    float f = 1.5;
    double d;
    decimal m;
    void N() {
        var a = (float)1;
        double b = (double)2;
        double r = System.Math.Round(1.0);
        double e = System.Math.Exp(1.0);
        double p = System.Math.Pow(2, 3);
        double s = System.Math.Sqrt(4);
        object o = null; o.ToFloat();
    }
}";
            List<string> hits = ScanSource("synthetic_probe", synthetic);

            foreach (string token in new[]
                     {
                         "float 类型", "double 类型", "decimal 类型", "浮点字面量",
                         "Math.Round", "Math.Exp", "Math.Pow", "Math.Sqrt", "ToFloat() 调用点",
                     })
            {
                Assert.That(hits.Any(h => h.Contains(token)), Is.True,
                    $"扫描器漏检 [{token}] —— 自证失败(与真扫描同一谓词,漏检 = AC-30 空断言)");
            }

            List<string> clean = ScanSource("clean_probe", "class Clean { long A(long x) => x + 1; }");
            Assert.That(clean, Is.Empty, "干净源不得误报(自证另一侧)");
        }

        /// <summary>违例谓词(真扫描与自证共用 —— 漏检自证即红,防伪证)。
        /// 白名单:facade 的 <c>ToFloat()</c> **定义自身**住 Sim.Contracts/Fix.cs(不在扫描面);
        /// Sim 内出现调用点 = 违 ADR-025 QQ-03 甲案(甲案构建期断言的 EditMode 同族镜像)。</summary>
        private static List<string> ScanSource(string label, string source)
        {
            string code = StripCommentsAndStrings(source);
            var hits = new List<string>();

            void Check(Regex rx, string what)
            {
                foreach (Match m in rx.Matches(code))
                    hits.Add($"{label}: [{what}] 「{m.Value}」");
            }

            Check(new Regex(@"\bfloat\b"), "float 类型");
            Check(new Regex(@"\bdouble\b"), "double 类型");
            Check(new Regex(@"\bdecimal\b"), "decimal 类型");
            Check(new Regex(@"\d+\.\d+"), "浮点字面量");
            Check(new Regex(@"Math\.(Round|Exp|Pow|Sqrt)"), "Math.libm 家族");
            Check(new Regex(@"Math\.Round"), "Math.Round");        // 分列点名(AC-30 原文)
            Check(new Regex(@"Math\.Exp"), "Math.Exp");
            Check(new Regex(@"Math\.Pow"), "Math.Pow");
            Check(new Regex(@"Math\.Sqrt"), "Math.Sqrt");
            Check(new Regex(@"\.ToFloat\("), "ToFloat() 调用点");
            return hits;
        }

        /// <summary>剥离 // 、/* */ 注释与字符串/字符字面量(AC-30「排除注释/字符串」;
        /// 插值串按整串剥离 —— 其内嵌表达式的残余盲区登记于故事 Deviations(ADVISOORY 级)。</summary>
        private static string StripCommentsAndStrings(string source)
        {
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            string noLine = Regex.Replace(noBlock, @"//[^\r\n]*", " ");
            string noVerbatim = Regex.Replace(noLine, "@\"(?:[^\"]|\"\")*\"", " \"\" ");
            string noString = Regex.Replace(noVerbatim, "\"(?:\\\\.|[^\"\\\\])*\"", " \"\" ");
            string noChar = Regex.Replace(noString, "'(?:\\\\.|[^'\\\\])'", "' '");
            return noChar;
        }
    }
}
