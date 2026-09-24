// 权威来源:Story 011 AC-21a-29(跨平台对拍 —— Editor Mono 与 IL2CPP player 哈希逐位相同)
//          · ADR-012 §一 层级 1 / F4 执行载体(纯逻辑哈希入口须可进 player 的 bootstrap;
//            EditMode 恒为 Mono 不经 IL2CPP ⇒ 对拍腿必须走 PlayMode → Standalone player)
//          · ADR-012 F7(2026-09-21 承 RC-4 降级):SplitMix64 / Fix 中间乘 = ulong,
//            无符号回绕两侧定义性 —— 本文件的 F7 向量测即「IL2CPP 侧已知值」探针
//
// ⚠️ 场景表与 unity/Assets/Tests/EditMode/ItemDatabase/determinism_golden_fixtures_test.cs
//    受控重复(两 asmdef 互不引用 —— EditMode = Sim.Contracts.Tests,本程序集 = Gameplay.Tests,
//    测试装配族跨引不属 ADR-025 §④ 六装配清单范围但会把 Editor-only 依赖带进 player,故不取)。
//    防漂移绑定 = **两侧都对同一金标准 golden-v1.txt 断言**:任一侧改表 ⇒ 该侧红。
//    金标准仍是唯一仲裁者(防自指:本测试只读金标准,结果文件写 AC29_OUT,外部 diff 判决)。
//
// ⚠️ AC-29 判决口径(禁借绿):
//    · 编辑器 PlayMode 跑 = Mono 侧(与 EditMode AC-28 同值,已由 463 绿背书);
//    · `-testPlatform StandaloneLinux64` + Standalone 后端 IL2CPP = IL2CPP 侧;
//    · **外部 diff(player 输出 vs golden-v1.txt)逐位相同 ⇒ AC-29 才可转 VERIFIED**;
//    · 测试内断言是第一道,外部 diff 是入账证据(测试绿但 diff 不同等价于红)。
//
// 环境变量(由调用方注入,player 子进程继承):
//    AC29_OUT    —— 计算结果写入路径(缺省 = Application.persistentDataPath/ac29-computed.txt)
//    AC29_GOLDEN —— 金标准路径(缺省 = 仓库相对路径;player 内 dataPath 指向 player_Data ⇒ 必须注入)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;

namespace DaYiJingCheng.Tests.PlayMode
{
    [TestFixture]
    internal sealed class DeterminismGoldenCrossplatformTest
    {
        private const string GoldenFileName = "golden-v1.txt";

        // ══════════════ 常量表夹具(与 EditMode 侧 / golden_v1_reference.py 逐字对应)══════════════

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
            skillModCap: FixParse.Parse("0"),
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

        private static string HashValues(IReadOnlyList<long> values)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("哈希向量不得为空");
            ulong state = unchecked((ulong)values[0]);
            for (int i = 1; i < values.Count; i++)
                state = SplitMix64.Fold(state, unchecked((ulong)values[i]));
            return SplitMix64.Avalanche(state).ToString("x16");
        }

        private static string HashBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("字节流不得为空");
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

        // ══════════════ 环境注入(AC-29 player 腿)══════════════

        private static string ResolveOutputPath()
        {
            string env = Environment.GetEnvironmentVariable("AC29_OUT");
            return string.IsNullOrEmpty(env)
                ? Path.Combine(Application.persistentDataPath, "ac29-computed.txt")
                : env;
        }

        private static string ResolveGoldenPath()
        {
            string env = Environment.GetEnvironmentVariable("AC29_GOLDEN");
            if (!string.IsNullOrEmpty(env)) return env;
            // 编辑器侧回退:仓库相对路径(EditMode AC-28 同一解析法)
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repoRoot, "tests", "unit", "item_database", "golden", GoldenFileName);
        }

        private static Dictionary<string, string> LoadGolden(string path)
        {
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

        /// <summary>后端标记 —— 证据文件头必载(证明本结果出自哪个编译后端,防「拿 Mono 结果冒充 IL2CPP」)。
        /// 判据 = FrameworkDescription(IL2CPP player 返回「Mono Unity IL2CPP」,Editor 返回「Mono x.y.z」);
        /// ⚠️ 首版用 `Mono.Runtime` 存在性判后端**为假** —— IL2CPP 同样生成该类型(首跑误标 backend=Mono,
        /// 同日修正),现以 FrameworkDescription 含「IL2CPP」为唯一判据。</summary>
        private static string BackendMarker()
        {
            string framework = RuntimeInformation.FrameworkDescription;
            string backend = framework.Contains("IL2CPP", StringComparison.Ordinal) ? "IL2CPP" : "Mono";
            return backend + " | " + framework;
        }

        // ══════════════ AC-29 主测:全 19 条 → 写结果文件 → 金标准可达时就地断言 ═══════════════

        [Test]
        public void test_ac21a29_allScenarioHashes_writtenAndCompared_whenGoldenReachable()
        {
            Dictionary<string, string> computed = ComputeAll();

            // ① 无条件落盘(断言失败也有文件可下钻 —— ADR-012 双级下钻协议)
            string outPath = ResolveOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            var sb = new StringBuilder();
            sb.AppendLine($"# ac29 backend={BackendMarker()}");
            sb.AppendLine($"# unity={Application.unityVersion} platform={Application.platform}");
            foreach (KeyValuePair<string, string> kv in new SortedDictionary<string, string>(computed, StringComparer.Ordinal))
                sb.AppendLine($"{kv.Key} {kv.Value}");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[AC-29] 计算结果已写入: {outPath}({computed.Count} 条, backend={BackendMarker()})");

            Assert.That(computed.Count, Is.EqualTo(19), "条目数必须 = 19(12 S + 4 F5 + 3 B)—— 场景表脱钩即红");

            // ② 金标准可达时就地逐位断言(编辑器腿恒可达;player 腿靠 AC29_GOLDEN 注入)
            string goldenPath = ResolveGoldenPath();
            if (!File.Exists(goldenPath))
            {
                Assert.Ignore($"金标准不可达({goldenPath})—— player 未注入 AC29_GOLDEN;本测退化为「只落盘」," +
                              "判决由外部 diff 承担(AC-29 口径:测试绿且外部 diff 逐位相同才 VERIFIED)");
            }

            Dictionary<string, string> golden = LoadGolden(goldenPath);
            CollectionAssert.AreEquivalent(golden.Keys, computed.Keys,
                "金标准条目集 ≠ 计算条目集(改表须升 golden-v2 全平台重签 —— ADR-012 裁决③)");
            foreach (KeyValuePair<string, string> kv in computed)
            {
                Assert.That(golden[kv.Key], Is.EqualTo(kv.Value),
                    $"[{kv.Key}] 哈希与金标准逐位不符 —— 跨平台确定性回归");
            }
        }

        // ══════════════ F7 向量(ADR-012 F7 降级:ulong 回绕已知值 —— IL2CPP 侧探针)══════════════
        // 期望值全部照录 Tests/EditMode/Sim/golden_hash_v1_test.cs(Python 独立参考产出,同源 provenance)。

        [Test]
        public void test_ac21a29_f7_splitmix64Wraparound_knownValuesUnderCurrentBackend()
        {
            ulong s = ulong.MaxValue;
            Assert.AreEqual(0x9E3779B97F4A7C14UL, unchecked(s + SplitMix64.Gamma),
                "ulong 回绕步进漂移(F7:无符号回绕须两侧定义性一致)");
            Assert.AreEqual(0xE4D971771B652C20UL, SplitMix64.NextValue(ref s),
                "回绕后 avalanche 输出漂移");
            Assert.AreEqual(0x7AD6664F09FFE52CUL, SplitMix64.Avalanche(0xDEADBEEFCAFEBABEUL),
                "Avalanche 定值漂移");
            Assert.AreEqual(0xB4D055FCF2CBBD7BUL, SplitMix64.Avalanche(ulong.MaxValue),
                "Avalanche(全1) 漂移");
        }

        [Test]
        public void test_ac21a29_f7_fixMulPeakProducts_knownValuesUnderCurrentBackend()
        {
            // 中间乘峰值参数(AC-29 Edge cases 明文要求):hi 位携带 / 补码取负 / 域外抛
            Assert.AreEqual(281474976710656L, Fix.MulRaw(4294967296L, 4294967296L),
                "2^32 × 2^32 → hi 位携带漂移");
            Assert.AreEqual(70368744112128L, Fix.MulRaw(2147483647L, 2147483647L),
                "int.Max² 峰值漂移");
            Assert.AreEqual(-281474976710656L, Fix.MulRaw(long.MinValue, 2L),
                "long.MinValue 量积(补码取负路径)漂移");
            Assert.AreEqual(3L, Fix.MulRaw(5L, 32768L),
                "5/2 = 2.5 → half-away 远离零 = 3(ties-to-even 会得 2)");
            Assert.Throws<OverflowException>(() => Fix.MulRaw(140737488355328L, 4294967296L),
                "2^47 × 2^32 → 域外须抛 OverflowException,不得回绕");
        }
    }
}
