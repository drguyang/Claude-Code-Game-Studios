// S12-9 · story-002 —— F-53.1 确定性结算(Outcome = Judge(P,C,D,U))测试。
//
// 权威来源:
//   GDD design/gdd/medical-consequences.md —— F-53.1(双轴枚举 · 枚举非分数)/
//     规则三(U 派生 + 试药史判据 OQ-53-9)/ 规则五(入向闭集)/ 规则七(后果非数)/
//     F-53.2(定义域 = 回响轴三值;结局轴不进延迟表)/ Edge Cases(误诊自愈 ⇒ 不回响)
//   卡面 production/epics/medical-consequences/story-002-deterministic-outcome-judge.md
//     —— AC-53-01(枚举闭集)/ AC-53-08(无 37 verdict;D 仅经 treatable_by 命中位)/
//     AC-53-03(零浮点)/ U 逐位钉 + 退化路径钉死(非静默 0)/ AC-53-02/15(黄金夹具)/
//     双轴验证(53 不写生死)/ 试药史形状(阈值留槽)/ AC-53-12(31 零引用)/ 误诊自愈
//   键域裁定(2026-10-15 用户裁):P ∈ {空,疑似,确定}(S-8.3 三态)×
//     C ∈ {无处置,有处置未命中,有处置命中}(命中位经 IDiseaseProjection,53 不判)×
//     D ∈ {痊愈,未痊愈}(误诊自愈判据位);表 = 3×3×2 = 18 行 + default;
//     U 档列默认 1 档(档数常量留槽);无痕 = 显式枚举成员 NoEcho。
//   ADR-012(黄金夹具 · 负夹具)/ ADR-031(解码行 Inputs53)/ ADR-014(烘焙)/ ADR-006(定点)
//
// ⚠️ S12-9-AC-b 纪律:本夹具**先于实现**落笔(Write tests first)——
//   初次运行时 Judge / Outcome53 / TrialHistoryPredicate 等符号不存在 ⇒ 编译红;
//   实现落地后转绿。不得先实现再造判据(假绿夹具同型,S12-4 新规)。
//
// ⚠️ IL2CPP 格(AC-53-02/15 三格)本机不可跑 —— NOT-RUN 如实登记(S12-9-AC-c 禁借绿),
//   归 CI(ADR-012 矩阵)。本文件只跑 Mono 侧 + 负夹具。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 Story 002 —— Judge 纯函数 + U 派生 + 试药史谓词 + 枚举闭集。</summary>
    [TestFixture]
    public sealed class MedconsOutcomeJudgeTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 夹具基建(与 medcons_aggregation_test 同款:真装配袋 + 真载荷编码)
        // ════════════════════════════════════════════════════════════════════

        private static readonly InMemoryBlobPool SharedPool = new InMemoryBlobPool();
        private static readonly PayloadEncoder Encoder = new PayloadEncoder(SharedPool);
        private static readonly PayloadDecoder Decoder = new PayloadDecoder(SharedPool);

        /// <summary>判定表(烘焙机产物;测试直接驱动 Binder —— 反空转,菜单与测试同一台机器)。
        /// <para>⚠️ 测试不读 .cooked.bytes 磁盘件(Binder 直接绑定源 JSON)——
        /// 源与产物同步由烘焙测试守(见 BakeTable 四条校验段)。</para></summary>
        private static Outcome53.Table LoadTable()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return OutcomeTable53.BakeTableFromRepo(repoRoot);
        }

        // ── 事件构造 helper(与聚合测试同签名;本文件只需要子集)────────────

        private static SimEvent EvJ(long tick, int patient, long seq,
                                    int lexiconId = 1, byte confidence = 2)
        {
            var payload = new JudgmentRecordedPayload(
                patient, new CaseId(0, patient, 0), authorPlayerId: 1, lexiconId, confidence);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.JudgmentRecorded,
                                Encoder.EncodeJudgmentRecorded(payload, string.Empty));
        }

        private static SimEvent EvD(long tick, int patient, long seq, int treatmentId = 1)
        {
            var payload = new DrugTreatmentAppliedPayload(
                tick, treatmentId, actorId: 1, polarity: -1, new Fix(0), halfLife: 100, seq: 0);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.DrugTreatmentApplied,
                                Encoder.Encode(EventKind.DrugTreatmentApplied, payload));
        }

        /// <summary>GDD 规则五 `:165` 逐字的处置集(与聚合测试同款默认)。</summary>
        private static readonly EventKind[] CareKindsPerGddRule5 = { EventKind.CareApplied };

        /// <summary>被测聚合(真解码行)。</summary>
        private static IReadOnlyList<Inputs53> Aggregate(IReadOnlyList<SimEvent> stream,
                                                         IDiseaseProjection projection = null)
            => ConsequenceInputAggregator.Aggregate(stream, Decoder, projection, CareKindsPerGddRule5);

        // ── 键域投影 helper(裁定键域的 C# 侧物化)──────────────────────────

        /// <summary>病程投影桩:D 轴 = 痊愈位 + C 命中位(生产真源 = 9 注册表,归 9 轮)。</summary>
        private sealed class StubProjection : IDiseaseProjection
        {
            private readonly bool _recovered;
            /// <summary>C 命中位(treatable_by;构造给值 —— 53 不判,桩即调用方立场)。</summary>
            public bool TreatableByMatched { get; }

            public StubProjection(bool recovered, bool treatableByMatched = false)
            {
                _recovered = recovered;
                TreatableByMatched = treatableByMatched;
            }

            public bool TryProject(PatientId patient, long tick, out DiseaseProjectionSnapshot snapshot)
            {
                snapshot = new DiseaseProjectionSnapshot(tick);
                return true;
            }
            /// <summary>D 轴判据(痊愈位;本桩恒返回构造值)。</summary>
            public bool Recovered => _recovered;
        }

        /// <summary>把 Inputs53 投影到键域(P/C/D 三键;Judge 的输入侧)。</summary>
        private static (Outcome53.JudgmentLane p, Outcome53.CareLane c, Outcome53.RecoveryLane d)
            ProjectLanes(Inputs53 inputs, StubProjection projection)
        {
            var p = Outcome53.JudgmentLaneOf(inputs.JudgmentRecords);
            // C 命中位:真源 = 9 注册表 treatable_by(53 不判);本桩按构造值给位
            // (生产 = IDiseaseProjection 实现侧经注册表反查 —— 归 9 轮,登记项)。
            bool matched = projection != null && projection.TreatableByMatched;
            var c = Outcome53.CareLaneOf(inputs.CareRecords, matched);
            var d = projection != null && projection.Recovered
                ? Outcome53.RecoveryLane.Recovered
                : Outcome53.RecoveryLane.NotRecovered;
            return (p, c, d);
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-53-01 —— Outcome 枚举闭集 + 零连续分数
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac5301_outcome_isClosedEnum_noScoreOutlet()
        {
            // ── THEN ①:枚举成员 = 双轴 5 员 + 无痕 = 恰 6 员(闭集反射)──
            var members = Enum.GetValues(typeof(Outcome53.Outcome)).Cast<Outcome53.Outcome>().ToList();
            Assert.AreEqual(6, members.Count,
                "Outcome 枚举须恰 6 员(结局轴 2 + 回响轴 3 + 无痕;增员 = GDD 修订轮,不得实现期扩)");
            foreach (var name in new[] { "PatientLives", "PatientLoses",
                                         "Residual", "Recurrence", "TrialHistory", "NoEcho" })
                Assert.IsTrue(members.Any(m => m.ToString() == name), $"缺枚举成员 {name}");

            // ── THEN ②:Judge 出口类型面零连续量(出口 = 枚举,非 int 分值 / Fix / float)──
            var method = typeof(Outcome53.Judge).GetMethod("Evaluate");
            Assert.IsNotNull(method, "Judge.Evaluate 符号缺失");
            Assert.AreEqual(typeof(Outcome53.Outcome), method.ReturnType,
                "Judge 出口类型须恰为 Outcome 枚举(AC-53-01:枚举非分数;连续通道 = 断言红)");
        }

        [Test]
        public void test_ac5301_judgeCoversFullClosure_allRowsAreClosureMembers()
        {
            // 表驱动:18 组合 × (有/无试药史) 全部产出 ∈ 枚举闭集(恒真式防御:返回类型即枚举;
            // 本条的价值在「全组合可求值不抛」—— 空洞命中在构建期已 throw,运行期不可达)。
            var table = LoadTable();
            foreach (var p in Enum.GetValues(typeof(Outcome53.JudgmentLane)).Cast<Outcome53.JudgmentLane>())
                foreach (var c in Enum.GetValues(typeof(Outcome53.CareLane)).Cast<Outcome53.CareLane>())
                    foreach (var d in Enum.GetValues(typeof(Outcome53.RecoveryLane)).Cast<Outcome53.RecoveryLane>())
                    {
                        var inputs = new Outcome53.JudgeInputs(
                            p, c, d, hasTrialHistory: false, uBand: 0, uRaw: 0);
                        Assert.DoesNotThrow(() => Outcome53.Judge.Evaluate(inputs, table),
                            $"组合 ({p},{c},{d}) 求值不得抛(构建期已保证全覆盖)");
                        var outcome = Outcome53.Judge.Evaluate(inputs, table);
                        Assert.IsTrue(Enum.IsDefined(typeof(Outcome53.Outcome), outcome),
                            "出口恒 ∈ 闭集(枚举类型保证;双保险断言)");
                    }
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-53-08 —— 53 自算正确性;零 37 verdict 读取;D 仅经命中位
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac5308_noVerdictPath_judgeInputsCarryNoCaseSystemSymbols()
        {
            // JudgeInputs 类型面反射:字段集 = {P 键, C 键, D 键, 试药史位, U 档, U raw};
            // 零 37 verdict / 零 disease_id / 零 correctness 类字段(反渗前置)。
            var fields = typeof(Outcome53.JudgeInputs).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.FieldType.Name + ":" + f.Name).ToList();
            Assert.AreEqual(6, fields.Count, "JudgeInputs 字段恰 6(闭集;新增字段 = 本断言红)");
            Assert.IsTrue(fields.All(f => !f.Contains("Verdict") && !f.Contains("Correctness")),
                "零 verdict / correctness 字段(AC-53-08:37 无此字段,53 自算)");
            Assert.IsTrue(fields.All(f => !f.ToLowerInvariant().Contains("disease")),
                "零 disease_id 类字段(AC-53-07 反渗前置:salted_key 语义替代)");
        }

        // ════════════════════════════════════════════════════════════════════
        // 判定表:键域全覆盖 + 镜像符号规则(与 python 参考实现 TABLE_ROWS 同表)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>夹具符号映射(golden_v1_reference.py fixture_outcome 的 C# 镜像;
        /// 行值归数值轮 —— 本表只证形状)。</summary>
        private static Outcome53.Outcome FixtureOutcome(Outcome53.JudgmentLane p,
            Outcome53.CareLane c, Outcome53.RecoveryLane d)
        {
            if (d == Outcome53.RecoveryLane.Recovered && c != Outcome53.CareLane.AppliedMatched)
                return Outcome53.Outcome.NoEcho;
            if (c == Outcome53.CareLane.AppliedMatched)
                return p == Outcome53.JudgmentLane.Confirmed
                    ? Outcome53.Outcome.NoEcho : Outcome53.Outcome.Residual;
            return p == Outcome53.JudgmentLane.Confirmed
                ? Outcome53.Outcome.Recurrence : Outcome53.Outcome.Residual;
        }

        [Test]
        public void test_table_eighteenRows_allMatchSymbolicFixture()
        {
            var table = LoadTable();
            Assert.AreEqual(18, table.RowCount, "判定表恰 18 行(3×3×2 用户裁定键域)");
            Assert.AreEqual(1, table.UBandCount, "U 档数默认 1(P0;档数常量留槽,数值轮扩)");

            foreach (var p in Enum.GetValues(typeof(Outcome53.JudgmentLane)).Cast<Outcome53.JudgmentLane>())
                foreach (var c in Enum.GetValues(typeof(Outcome53.CareLane)).Cast<Outcome53.CareLane>())
                    foreach (var d in Enum.GetValues(typeof(Outcome53.RecoveryLane)).Cast<Outcome53.RecoveryLane>())
                    {
                        var inputs = new Outcome53.JudgeInputs(
                            p, c, d, hasTrialHistory: false, uBand: 0, uRaw: 0);
                        var expected = FixtureOutcome(p, c, d);
                        Assert.AreEqual(expected, Outcome53.Judge.Evaluate(inputs, table),
                            $"表行 ({p},{c},{d}) 须与夹具符号规则一致(与 python 参考实现同表)");
                    }
        }

        [Test]
        public void test_table_defaultRow_presentAndIsClosureMember()
        {
            var table = LoadTable();
            Assert.IsTrue(table.HasDefaultRow, "判定表须带显式 default 行(构建期校验 ① 的空洞兜底)");
            Assert.IsTrue(Enum.IsDefined(typeof(Outcome53.Outcome), table.DefaultOutcome),
                "default 行出口 ∈ 闭集");
        }

        // ════════════════════════════════════════════════════════════════════
        // U 派生(AC-53-15)—— SplitMix64 case-salt 黄金向量 + 退化路径钉死
        // ════════════════════════════════════════════════════════════════════

        private static Dictionary<string, string> LoadGolden()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "tests", "golden", "medcons", "golden-v1.txt");
            Assert.That(File.Exists(path), Is.True,
                $"金标准缺失:{path}(由独立 Python 参考实现产出;测试只读不写 —— 防自指)");
            var golden = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int sp = line.IndexOf(' ');
                Assert.That(sp, Is.GreaterThan(0), $"金标准行格式非法:{line}");
                golden[line.Substring(0, sp)] = line.Substring(sp + 1).Trim();
            }
            return golden;
        }

        [Test]
        public void test_ac5315_caseSaltGoldenVectors_matchIndependentReference()
        {
            var golden = LoadGolden();
            // 五组黄金向量(A01–A05):键序 / 混盐写法 / 大端补零块逐位钉。
            Assert.AreEqual(golden["A01_casesalt_seed0_key0"],
                string.Format("{0:X16}", SplitMix64.HashTagged(0L, "case-salt", 0L)), "A01");
            Assert.AreEqual(golden["A02_casesalt_seed1234_key7"],
                string.Format("{0:X16}", SplitMix64.HashTagged(1234L, "case-salt", 7L)), "A02");
            Assert.AreEqual(golden["A03_casesalt_seed-5_key-1"],
                string.Format("{0:X16}", SplitMix64.HashTagged(-5L, "case-salt", -1L)), "A03(负哨兵位型)");
            Assert.AreEqual(golden["A04_casesalt_seed9223372036854775807_key9223372036854775807"],
                string.Format("{0:X16}", SplitMix64.HashTagged(long.MaxValue, "case-salt", long.MaxValue)), "A04(溢出边界)");
            Assert.AreEqual(golden["A05_casesalt_seed43981_key3"],
                string.Format("{0:X16}", SplitMix64.HashTagged(0xABCDL, "case-salt", 3L)), "A05(与既有 case-salt golden 同值交叉验证)");
        }

        [Test]
        public void test_ac5315_negativeFixture_signedShiftDriftRejected()
        {
            // 负夹具(N01):有符号算术右移变体在高位为 1 时符号扩展 ⇒ 哈希漂移。
            // 本夹具钉死「正确值 ≠ 漂移值」,防位移语义被改回 signed 而夹具仍绿。
            ulong correct = SplitMix64.HashTagged(1234L, "case-salt", 7L);
            var golden = LoadGolden();
            Assert.AreEqual(golden["A02_casesalt_seed1234_key7"], string.Format("{0:X16}", correct),
                "正确值须等于 golden A02");
            Assert.AreNotEqual(golden["N01_signedshift_variant_sameInputsAsA02"],
                string.Format("{0:X16}", correct),
                "正确值不得等于 signed-shift 漂移变体(ADR-012 F7 逐位纪律的防回归形)");
        }

        [Test]
        public void test_ac5315_urawMatchesGolden()
        {
            var golden = LoadGolden();
            Assert.AreEqual(long.Parse(golden["A01_uraw"]), (long)OutcomeJudgeDerive.URaw(0L, 0UL), "A01 uraw");
            Assert.AreEqual(long.Parse(golden["A02_uraw"]), (long)OutcomeJudgeDerive.URaw(1234L, 7UL), "A02 uraw");
            Assert.AreEqual(long.Parse(golden["A03_uraw"]), (long)OutcomeJudgeDerive.URaw(-5L, unchecked((ulong)(-1L))), "A03 uraw");
            Assert.AreEqual(long.Parse(golden["A04_uraw"]),
                (long)OutcomeJudgeDerive.URaw(long.MaxValue, unchecked((ulong)long.MaxValue)), "A04 uraw(溢出边界)");
        }

        [Test]
        public void test_uDerive_degradedPath_isExplicitMidpoint_notSilentZero()
        {
            // 退化路径(未认出模式,salted_key 缺失):显式中点 1/2(Q16.16 raw = 32768)。
            // 非 0、非静默 —— 卡面 U 退化口径的夹具钉死;数值轮只改常量不改形状。
            Assert.AreEqual(32768L, (long)OutcomeJudgeDerive.URawDegraded(),
                "退化 U = 显式中点 1/2(raw 32768)—— 非 0、非静默");
            Assert.AreEqual(32768L, (long)OutcomeJudgeDerive.URawDegraded(),
                "退化路径恒定(零隐藏可变态)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-53-02 —— Judge 逐位一致(Mono 侧;IL2CPP 三格 = CI,NOT-RUN 登记)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac5302_judgeDeterministic_twentyTuplesMatchGolden()
        {
            var golden = LoadGolden();
            var table = LoadTable();

            // 20 例 tuple 夹具(B01–B20,与 python 参考实现 TUPLES 同表):
            // (seed, saltedKey|null, P, C, D, hasTrial)。saltedKey=null ⇒ 退化 U。
            var tuples = new (string name, long seed, ulong? key, Outcome53.JudgmentLane p,
                              Outcome53.CareLane c, Outcome53.RecoveryLane d, bool trial)[]
            {
                ("B01", 1234L, 7UL, Outcome53.JudgmentLane.Confirmed,
                 Outcome53.CareLane.AppliedMatched, Outcome53.RecoveryLane.Recovered, false),
                ("B02", 1234L, 7UL, Outcome53.JudgmentLane.Empty, Outcome53.CareLane.AppliedMatched,
                 Outcome53.RecoveryLane.Recovered, false),
                ("B03", 1234L, 7UL, Outcome53.JudgmentLane.Suspected, Outcome53.CareLane.AppliedMatched,
                 Outcome53.RecoveryLane.NotRecovered, false),
                ("B04", 1234L, 7UL, Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.Recovered, false),
                ("B05", 1234L, 7UL, Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.NotRecovered, false),
                ("B06", 1234L, 7UL, Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.NotRecovered, false),
                ("B07", 1234L, 7UL, Outcome53.JudgmentLane.Suspected, Outcome53.CareLane.AppliedUnmatched,
                 Outcome53.RecoveryLane.Recovered, false),
                ("B08", 1234L, 7UL, Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedUnmatched,
                 Outcome53.RecoveryLane.NotRecovered, false),
                ("B09", 999L, 0UL, Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.Recovered, false),
                ("B10", -5L, unchecked(0UL - 1), Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                 Outcome53.RecoveryLane.Recovered, false),
                ("B11", long.MaxValue, unchecked((ulong)long.MaxValue), Outcome53.JudgmentLane.Suspected,
                 Outcome53.CareLane.None, Outcome53.RecoveryLane.NotRecovered, false),
                ("B12", 1234L, 7UL, Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                 Outcome53.RecoveryLane.Recovered, true),
                ("B13", 1234L, 7UL, Outcome53.JudgmentLane.Empty, Outcome53.CareLane.AppliedUnmatched,
                 Outcome53.RecoveryLane.Recovered, true),
                ("B14", -5L, unchecked(0UL - 1), Outcome53.JudgmentLane.Suspected, Outcome53.CareLane.AppliedUnmatched,
                 Outcome53.RecoveryLane.NotRecovered, true),
                ("B15", 777L, 0xFFFFFFFFUL, Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.NotRecovered, false),
                ("B16", 777L, 0x7FFFFFFFFFFFFFFFUL, Outcome53.JudgmentLane.Empty,
                 Outcome53.CareLane.AppliedUnmatched, Outcome53.RecoveryLane.Recovered, false),
                ("B17", long.MinValue, 0xFFFFFFFFFFFFFFFFUL, Outcome53.JudgmentLane.Suspected,
                 Outcome53.CareLane.AppliedMatched, Outcome53.RecoveryLane.NotRecovered, false),
                ("B18", 42L, 1234567890123UL, Outcome53.JudgmentLane.Confirmed,
                 Outcome53.CareLane.AppliedUnmatched, Outcome53.RecoveryLane.Recovered, false),
                ("B19", 1234L, 7UL, Outcome53.JudgmentLane.Suspected, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.Recovered, false),
                ("B20", 0L, null, Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                 Outcome53.RecoveryLane.Recovered, false),
            };

            foreach (var t in tuples)
            {
                // U 派生:逐例重算(与 python 参考实现同形)—— 退化例走退化常量。
                ulong uRaw = t.key.HasValue
                    ? OutcomeJudgeDerive.URaw(t.seed, t.key.Value)
                    : OutcomeJudgeDerive.URawDegraded();
                int band = Outcome53.BandOf(uRaw, table.UBandCount);

                var inputs = new Outcome53.JudgeInputs(t.p, t.c, t.d, t.trial, band, uRaw);
                var outcome = Outcome53.Judge.Evaluate(inputs, table);

                Assert.AreEqual(golden[t.name + "_outcome"], GoldenNameOf(outcome),
                    $"{t.name}(seed={t.seed}, key={(t.key.HasValue ? t.key.Value.ToString() : "degraded")}) 判定逐位一致");
            }
        }

        /// <summary>Outcome 枚举 → golden 行名(snake_case;与 python 参考实现/JSON 表同词法)。</summary>
        private static string GoldenNameOf(Outcome53.Outcome o) => o switch
        {
            Outcome53.Outcome.NoEcho => "no_echo",
            Outcome53.Outcome.Residual => "residual",
            Outcome53.Outcome.Recurrence => "recurrence",
            Outcome53.Outcome.TrialHistory => "trial_history",
            Outcome53.Outcome.PatientLives => "patient_lives",
            Outcome53.Outcome.PatientLoses => "patient_loses",
            _ => o.ToString(),
        };

        [Test]
        public void test_ac5302_sameInputsTwice_identicalOutcome()
        {
            // 纯函数性:同输入两次调用同输出(零隐藏可变态;Judge 零字段)。
            var table = LoadTable();
            var inputs = new Outcome53.JudgeInputs(
                Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                Outcome53.RecoveryLane.Recovered, false, 0, 12345UL);
            Assert.AreEqual(Outcome53.Judge.Evaluate(inputs, table),
                            Outcome53.Judge.Evaluate(inputs, table), "同输入同输出");
        }

        // ════════════════════════════════════════════════════════════════════
        // 双轴验证 —— 53 不写生死;「玩家致死」不存在
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_dualAxis_judgeNeverMutatesLifeFields_noPlayerKillOutlet()
        {
            // ① JudgeInputs 类型面:零「死亡 / 生死可控位」字段(结局轴由 9 结算,53 只读)。
            var fields = typeof(Outcome53.JudgeInputs).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name).ToList();
            Assert.IsTrue(fields.All(f =>
                    !f.ToLowerInvariant().Contains("death") && !f.ToLowerInvariant().Contains("life")),
                "JudgeInputs 零生死可控位(双轴:结局轴 = 9,53 不上裁决面)");

            // ② Outcome 枚举闭集:零「玩家致死」语义成员(52 / GDD 支柱:53 永不产出玩家致死)。
            var members = Enum.GetNames(typeof(Outcome53.Outcome));
            Assert.IsTrue(members.All(m => m != "PlayerKill" && m != "PlayerCausedDeath"),
                "Outcome 零「玩家致死」枚举成员");

            // ③ Judge 类面:零写回通道(纯函数;Evaluate 出口 = 枚举,无 ref/out 生死形参)。
            var evaluate = typeof(Outcome53.Judge).GetMethod("Evaluate");
            Assert.IsTrue(evaluate.GetParameters().All(p => !p.ParameterType.IsByRef),
                "Judge.Evaluate 零 ref/out 形参(纯函数;零写回通道)");
        }

        // ════════════════════════════════════════════════════════════════════
        // 试药史形状(OQ-53-9)—— run 计数 + 支持性判断打断;阈值留槽
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_oq539_trialRun_countingAndJudgmentBreak()
        {
            // GDD 规则三:「同一 patient 连续 DrugTreatmentApplied 序列、无 JudgmentRecorded 支撑」。
            // (D, D, J, D) ⇒ 判断打断 run:前段 run=2 < 阈值(3),后段 run=1 < 阈值 ⇒ 无试药史。
            var stream = new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1), EvJ(30, 5, 2), EvD(40, 5, 3),
            };
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(stream, Decoder),
                "(D,D,J,D):支持性判断打断 run ⇒ 两段均 < 阈值 ⇒ 无试药史");

            // (D, D, D) 连续 3 条 ≥ 阈值 3 ⇒ 有试药史。
            var stream2 = new List<SimEvent> { EvD(10, 5, 0), EvD(20, 5, 1), EvD(30, 5, 2) };
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(stream2, Decoder),
                "(D,D,D):连续 run = 阈值 3 ⇒ 有试药史");
        }

        [Test]
        public void test_oq539_thresholdIsSlottedConstant_notHardcodedMagic()
        {
            // 阈值常量留槽(卡面 Implementation Note 6:值归数值轮)—— 公开常量 + 注记,
            // 不得内联魔数(测试标准:no hardcoded data;此处边界值本身即断言点,允许)。
            Assert.AreEqual(3, TrialHistoryPredicate.TrialRunThreshold,
                "阈值 = 留槽常量 3(数值轮可调;断言值本身即边界值)");
            Assert.AreEqual(0, TrialHistoryPredicate.MinConsecutiveDrugs,
                "run 下界 0(空流语义)");
        }

        [Test]
        public void test_oq539_runBoundary_exactlyThreshold_counts()
        {
            // 边界:恰阈值 3 条算,2 条不算;跨病人不计(谓词是**同一病人**序列)。
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1), EvD(30, 5, 2),
            }, Decoder), "恰 3 条(= 阈值)⇒ 算");

            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1),
            }, Decoder), "2 条(< 阈值)⇒ 不算");

            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 6, 1), EvD(30, 7, 2),   // 不同病人
            }, Decoder), "跨病人序列不算 run(OQ-53-9:同一 patient)");
        }

        // ════════════════════════════════════════════════════════════════════
        // 端到端(聚合 → 键域投影 → Judge)—— 与 story-001 输出的衔接面
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_endToEnd_aggregateToJudge_producesExpectedOutcome()
        {
            // 误诊自愈:无判断 + 无处置 + 痊愈 ⇒ (空, 无处置, 痊愈) ⇒ 无痕 ⇒ NoEcho(世界不撒谎)。
            var projection = new StubProjection(recovered: true);
            var stream = new List<SimEvent>
            {
                EvJ(10, 5, 0, lexiconId: 0),                       // 空判断(病名空置合法终态)
                new SimEvent(20, new PatientId(5), 1, EventKind.CaseClosed, default(PayloadRef)),
            };
            var inputs = Aggregate(stream, projection);
            Assert.AreEqual(1, inputs.Count, "恰一组(一个 CaseClosed)");

            var lanes = ProjectLanes(inputs[0], projection);
            Assert.AreEqual(Outcome53.JudgmentLane.Empty, lanes.p, "P = 空(lexiconId=0)");
            Assert.AreEqual(Outcome53.CareLane.None, lanes.c, "C = 无处置");

            var table = LoadTable();
            var judgeInputs = new Outcome53.JudgeInputs(
                lanes.p, lanes.c, lanes.d, hasTrialHistory: false,
                uBand: 0, uRaw: OutcomeJudgeDerive.URaw(42L, 7UL));
            Assert.AreEqual(Outcome53.Outcome.NoEcho, Outcome53.Judge.Evaluate(judgeInputs, table),
                "误诊自愈(无痕)⇒ 不回响 —— 世界不撒谎(GDD Edge Cases)");
        }

        [Test]
        public void test_endToEnd_judgmentLane_threeStates()
        {
            // P 轴三态物化:lexiconId=0 ⇒ 空;lexiconId>0 + 低置信 ⇒ 疑似;高置信 ⇒ 确定。
            Assert.AreEqual(Outcome53.JudgmentLane.Empty,
                Outcome53.JudgmentLaneOf(Array.Empty<JudgmentRow53>()), "无判断行 ⇒ 空");
            Assert.AreEqual(Outcome53.JudgmentLane.Empty,
                Outcome53.JudgmentLaneOf(new[] { new JudgmentRow53(10, 5, 0, lexiconId: 0, confidence: 2) }),
                "lexiconId=0(病名空置)⇒ 空");
            Assert.AreEqual(Outcome53.JudgmentLane.Suspected,
                Outcome53.JudgmentLaneOf(new[] { new JudgmentRow53(10, 5, 0, lexiconId: 1, confidence: 1) }),
                "lexiconId>0 + 置信 < 分界 ⇒ 疑似");
            Assert.AreEqual(Outcome53.JudgmentLane.Confirmed,
                Outcome53.JudgmentLaneOf(new[] { new JudgmentRow53(10, 5, 0, lexiconId: 1, confidence: 2) }),
                "lexiconId>0 + 置信 ≥ 分界 ⇒ 确定");
        }

        // ════════════════════════════════════════════════════════════════════
        // 烘焙四条构建期校验(ADR-014 阶段 2;菜单与测试同一台机器)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_bake_sourceJson_bindsAndPassesAllFourValidations()
        {
            // ① 全覆盖(18 组合无空洞)② 值域 ∈ 闭集 ③ 零浮点 ④ 极性列不存在
            // —— 四条都在 Binder 内以聚合 throw 兑现;本条 = 正路径冒烟(合法源绑定成功)。
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            Assert.DoesNotThrow(() => OutcomeTable53.BakeFromRepo(repoRoot),
                "合法源 JSON 须通过全部四条构建期校验");
        }

        [Test]
        public void test_bake_missingCombination_hardFails()
        {
            // ① 覆盖校验负例:删掉一行 ⇒ 空洞 ⇒ 硬失败(聚合 throw,消息点名缺失组合)。
            string json = MinimalTableJson();
            json = json.Replace("\"confirmed|applied_matched|recovered\"", "\"confirmed|applied_matched|GONE\"");
            var ex = Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json),
                "组合缺失(键名对不上)⇒ 构建期硬失败,绝不静默降级");
            // ⚠️ 消息面:改 key 名在**行解析层**先报「key 非法」(覆盖校验是 errors 清空后
            // 才跑的第二道)—— 两条都是「组合缺失」的正当错误面,断言两者任一。
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("覆盖") || e.Contains("缺失")
                                          || e.Contains("非法")),
                "错误消息须点名 key 非法/覆盖缺失:" + string.Join("; ", ex.Errors));
        }

        [Test]
        public void test_bake_unknownOutcomeValue_hardFails()
        {
            // ② 值域校验负例:Outcome 写成闭集外名字 ⇒ 硬失败。
            string json = MinimalTableJson().Replace("\"no_echo\"", "\"supreme_praise\"");
            var ex = Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json), "闭集外值 ⇒ 硬失败");
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("闭集") || e.Contains("Outcome")),
                "错误消息须点名闭集:" + string.Join("; ", ex.Errors));
        }

        [Test]
        public void test_bake_floatLiteral_hardFails()
        {
            // ③ 零浮点校验负例:U 档列写成小数字面量 ⇒ 硬失败(词法层拒绝)。
            string json = MinimalTableJson().Replace("\"u_band\": 0", "\"u_band\": 0.5");
            Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json), "浮点字面量 ⇒ 硬失败(ADR-006 边界)");
        }

        [Test]
        public void test_bake_polarityColumn_hardFails()
        {
            // ④ 极性列不存在:53 不重判极性(真源 = 9 注册表)—— 作者态出现极性键 ⇒ 硬失败。
            string json = MinimalTableJson().Replace("\"u_band\": 0", "\"polarity\": 1, \"u_band\": 0");
            var ex = Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json), "极性列 ⇒ 硬失败(53 不重判)");
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("极性") || e.Contains("polarity")),
                "错误消息须点名极性:" + string.Join("; ", ex.Errors));
        }

        /// <summary>最小合法判定表 JSON(19 行:18 组合 + default;U 档列恒 0)。</summary>
        private static string MinimalTableJson()
        {
            var rows = new List<string>();
            foreach (var p in new[] { "empty", "suspected", "confirmed" })
                foreach (var c in new[] { "none", "applied_unmatched", "applied_matched" })
                    foreach (var d in new[] { "recovered", "not_recovered" })
                    {
                        // 与 FixtureOutcome 同规则(镜像 python)。
                        string outcome;
                        if (d == "recovered" && c != "applied_matched") outcome = "no_echo";
                        else if (c == "applied_matched")
                            outcome = p == "confirmed" ? "no_echo" : "residual";
                        else outcome = p == "confirmed" ? "recurrence" : "residual";
                        rows.Add($"{{\"key\": \"{p}|{c}|{d}\", \"u_band\": 0, \"outcome\": \"{outcome}\"}}");
                    }
            rows.Add("{\"key\": \"default\", \"u_band\": 0, \"outcome\": \"residual\"}");
            return "{\"schema_version\": 1, \"u_band_count\": 1, \"rows\": [" +
                   string.Join(",", rows) + "]}";
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-53-12 —— 31 零引用(声誉/名声系统零符号命中)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac5312_noReputationSymbols_inConsequencesNamespace()
        {
            // 53 住 Sim 程序集 DaYiJingCheng.Sim.Consequences;31(声誉与名声)未实现 ⇒
            // 零符号可扫 —— 本条以**类型面闭集**守:Consequences 命名空间的全部公开类型
            // 名/成员名零「声誉/信任/累积分」类 token(53 是水龙头,单向流只到世界流)。
            var asm = typeof(Outcome53).Assembly;
            var types = asm.GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Sim.Consequences").ToList();
            Assert.Greater(types.Count, 0, "Consequences 命名空间非空(被测面存在)");

            var forbidden = new[] { "Reputation", "Trust", "Favor", "Score", "Points", "Credit" };
            foreach (var t in types)
            {
                Assert.IsTrue(!forbidden.Any(tok => t.Name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0),
                    $"类型 {t.Name} 含声誉类 token(AC-53-12:53 不知 31 存在)");
                foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Assert.IsTrue(!forbidden.Any(tok => m.Name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0),
                        $"成员 {t.Name}.{m.Name} 含声誉类 token(AC-53-12)");
            }

            // Sim 引用集恰 = {BCL, Sim.Contracts}(ADR-025 §①;b2 门同口径,双保险):
            var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToList();
            Assert.IsTrue(refs.All(r => r.StartsWith("System") || r.StartsWith("netstandard") ||
                                        r == "mscorlib" || r == "Mono" || r == "Sim.Contracts"),
                "Sim 引用集零 31 侧程序集(声誉系统未实现;出现任何非 BCL/Contracts 引用即红)");
        }
    }
}
