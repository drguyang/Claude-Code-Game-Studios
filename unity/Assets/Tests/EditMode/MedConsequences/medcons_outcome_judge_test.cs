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
using DaYiJingCheng.EditorTools.Gates;
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
                tick, treatmentId, actorId: 1, polarity: -1, new Fix(0), halfLife: 100, seq: 0,
                contraindicationBand: DrugTreatmentAppliedPayload.NoContraindication);
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

            // 🔁 2026-10-30(`TD-medcons-01` 同族全扫):A05 的 uraw 此前也是孤儿 ——
            //   golden 写了 `A05_uraw`,但没有任何测试读它(只断言了 A01–A04,以及 A05 的
            //   **case-salt** 行)。参考实现的 `A_VECTORS` 里 A05 = (seed 0xABCD, key 3)。
            Assert.AreEqual(long.Parse(golden["A05_uraw"]),
                (long)OutcomeJudgeDerive.URaw(0xABCDL, 3UL), "A05 uraw");
        }

        [Test]
        public void test_uDerive_degradedPath_isExplicitMidpoint_notSilentZero()
        {
            // 退化路径(未认出模式,salted_key 缺失):显式中点 1/2(Q16.16 raw = 32768)。
            // 非 0、非静默 —— 卡面 U 退化口径的夹具钉死;数值轮只改常量不改形状。
            //
            // 🔁 **2026-10-30(`TD-medcons-01` 结案)**:期望值**改从 golden 读**,不再写字面量。
            //   原写法在**两处各写一次** 32768(golden 文件 + 本测试),两者互不引用 ⇒
            //   golden 的 `C01_degraded_uraw` 行**从未被任何测试消费**(孤儿行),而测试照绿 ——
            //   这是「自证」形态:被测对象与期望值同源,分叉了也发现不了。
            //   改后 golden 行进入判据链:参考实现与 C# 实现的退化常量一旦分叉,本条转红。
            var golden = LoadGolden();
            Assert.IsTrue(golden.ContainsKey("C01_degraded_uraw"),
                "golden 缺 C01_degraded_uraw 行 ⇒ 本条的期望值无出处(不得回退到本地字面量)");
            long expected = long.Parse(golden["C01_degraded_uraw"]);

            Assert.AreEqual(expected, (long)OutcomeJudgeDerive.URawDegraded(),
                $"退化 U 须等于 golden 的 C01_degraded_uraw({expected})—— 非 0、非静默");

            // 纯度:连续两次调用相同。⚠️ 此条**不**引用 golden —— 它测的是「无隐藏可变状态」,
            // 拿 golden 当期望值会把两种判据混成一条(纯度坏了而常量没变时,回显不出来)。
            Assert.AreEqual((long)OutcomeJudgeDerive.URawDegraded(), (long)OutcomeJudgeDerive.URawDegraded(),
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

        /// <summary>
        /// AC-5(故事本地编号,= GDD `AC-53-xx` 的 OQ-53-8 形状门)**可执行的那一半**:
        /// 代价路径的 Outcome 限定 ∈ {伤, 后遗} 子集 —— 本工程以**类型级封闭**兑现:
        /// <see cref="Outcome53.Outcome"/> 是闭集枚举,其成员**全部**是「结局轴(只读)/ 回响轴 / 无痕」三类,
        /// 没有任何「致死」语义成员;新的代价型 Outcome 只能作为新成员加进来 ⇒ 本测即红。
        /// <para>⚠️ **不可执行的那一半(如实登记,不借绿)**:「致死行在 P0 表 = 构建期拒收」需要一张
        /// **代价表**载体;全仓今日无该表(`DelayTable` 定义域 = 回响轴三分立值,结局轴不入表)
        /// ⇒ 该判据**无宾语**。归 `TD-medcons-15` / 数值轮;本测**不**声称已覆盖它。</para>
        /// </summary>
        [Test]
        public void test_ac5_outcomeEnumClosure_hasNoLethalMember_costPathCantExpressDeath()
        {
            var names = Enum.GetNames(typeof(Outcome53.Outcome)).ToHashSet();
            Assert.AreEqual(
                new HashSet<string>
                {
                    "PatientLives", "PatientLoses",           // 结局轴:9 结算,53 只读
                    "Residual", "Recurrence", "TrialHistory", // 回响轴:伤 / 后遗
                    "NoEcho",                                  // 无痕
                },
                names,
                "AC-5 类型级半边:Outcome 闭集须恰为这 6 个成员 —— 新增任何成员(尤其是致死语义)" +
                "都必须先回到本测显式放行,否则代价路径就可能表达「致死」");
            Assert.IsTrue(names.All(n =>
                    !n.ToLowerInvariant().Contains("death") && !n.ToLowerInvariant().Contains("kill") &&
                    !n.ToLowerInvariant().Contains("lethal") && !n.ToLowerInvariant().Contains("fatal")),
                "AC-5:闭集内零致死语义成员(勿只靠上一条的字面集合 —— 两层判据独立成立)");
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
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(stream),
                "(D,D,J,D):支持性判断打断 run ⇒ 两段均 < 阈值 ⇒ 无试药史");

            // (D, D, D) 连续 3 条 ≥ 阈值 3 ⇒ 有试药史。
            var stream2 = new List<SimEvent> { EvD(10, 5, 0), EvD(20, 5, 1), EvD(30, 5, 2) };
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(stream2),
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
            }), "恰 3 条(= 阈值)⇒ 算");

            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1),
            }), "2 条(< 阈值)⇒ 不算");

            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 6, 1), EvD(30, 7, 2),   // 不同病人
            }), "跨病人序列不算 run(OQ-53-9:同一 patient)");
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
            // ③ 零浮点校验负例:U 档列写成小数字面量 ⇒ 硬失败。
            // ⚠️ N-9(2026-10-16):原注释写「词法层拒绝」为误 —— JsonStage1Lexer **接受**
            // 浮点 token(JsonNodeKind.Float),拒收发生在阶段 2 ReadUBand(类型非 Integer)。
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

            // H(2026-10-15 返工):token 集**读门侧单一真源** —— 原实现自带字面量
            // { Reputation, Trust, Favor, Score, Points, Credit },与 MedConsFloatGate 的
            // { Reputation, Trust, Favor, Renown, Standing, Prestige } + { ScoreAccum,
            // ScorePool, CreditScore } **互不为超集** ⇒ 同一 AC 两套判据,可各绿而语义分叉。
            var forbidden = MedConsFloatGate.ReputationTokens
                .Concat(MedConsFloatGate.ScoreTokens).ToArray();
            Assert.Greater(forbidden.Length, 0, "门侧 token 集非空(单一真源可读)");
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

        // ════════════════════════════════════════════════════════════════════
        // S12-4 假绿夹具强制证伪轮 —— 2026-10-15 返工新增 10 条可证伪探针
        // 每条对应 review-medcons-story-002-2026-10-15.md §四 反证表的一行;
        // 「停用 X ⇒ 本夹具须转红」的实证见该文件(改与跑同一次 Bash 调用,trap 纪律)。
        // ⚠️ 无该实证 ⇒ 该条**未证伪**,不得据此声称 Gate 通过。
        // ════════════════════════════════════════════════════════════════════

        /// <summary>仓根(与 <see cref="LoadTable"/> 同源)。</summary>
        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>FNV-1a u32(测试内自足 —— 不用 UnityEngine.Hash128,承 ADR-010 纪律)。</summary>
        private static uint Fnv1a(byte[] bytes)
        {
            uint h = 2166136261;
            foreach (byte b in bytes) { h ^= b; h *= 16777619; }
            return h;
        }

        /// <summary>显式 little-endian u32 读(N-8:与写方 WriteU32Le / CookedCodec.ReadU32Le
        /// 同形 —— ADR-014「字节序固定、不随平台」,不用平台序相关的 BitConverter)。</summary>
        private static uint ReadU32Le(byte[] buffer, int offset)
            => (uint)(buffer[offset]
                      | (buffer[offset + 1] << 8)
                      | (buffer[offset + 2] << 16)
                      | (buffer[offset + 3] << 24));

        /// <summary>三轴全枚举(18 组合遍历用)。</summary>
        private static readonly Outcome53.JudgmentLane[] AllP =
            { Outcome53.JudgmentLane.Empty, Outcome53.JudgmentLane.Suspected,
              Outcome53.JudgmentLane.Confirmed };
        private static readonly Outcome53.CareLane[] AllC =
            { Outcome53.CareLane.None, Outcome53.CareLane.AppliedUnmatched,
              Outcome53.CareLane.AppliedMatched };
        private static readonly Outcome53.RecoveryLane[] AllD =
            { Outcome53.RecoveryLane.Recovered, Outcome53.RecoveryLane.NotRecovered };

        // ── F1 / R-1:cooked 头 offset 8 回声 schemaVersion(非 0),载荷首字段不再是它 ──

        [Test]
        public void test_bake_cookedHeader_schemaVersionEchoesBound()
        {
            var output = OutcomeTable53.BakeFromRepo(RepoRoot());

            // 魔数读常量(原硬写 'D','Y','J','C' —— CookedFormat.Magic 已存在,双真源)。
            string magic = CookedFormat.Magic;
            Assert.AreEqual(4, magic.Length, "前置:CookedFormat.Magic 恰 4 字符");
            for (int i = 0; i < 4; i++)
                Assert.AreEqual((byte)magic[i], output.Cooked[i], $"魔数字节 {i}");

            // R-1 ①:头 offset 8 = CookedFormat.SchemaVersion(旧实现硬写 0 ⇒ 本断言红)。
            uint headerSchema = ReadU32Le(output.Cooked, 8);
            Assert.AreEqual(CookedFormat.SchemaVersion, headerSchema,
                "头 offset 8 须为 schemaVersion 槽(旧实现硬写 0;读方 CookedCodec.OpenChecked " +
                "正与此常量比对,不符 ⇒ 启动期 InvalidDataException)");

            // R-1 ②:载荷首字段 = u_band_count(schema_version 已删)。
            uint payloadFirst = ReadU32Le(output.Cooked, CookedFormat.HeaderSize);
            Assert.AreEqual((uint)output.Bound.UBandCount, payloadFirst,
                "载荷首字段 = u_band_count");

            // R-1 ③(2026-10-16 重评返工):**载荷长度**按公式推导后比对 ——
            // 这才是「载荷首字段已删」的**可判别**判据。原断言只比值:现网
            // schema_version == u_band_count == 1 ⇒ 旧布局(载荷首字段仍是 schema_version)
            // 下同样绿,「第二真源已消」这半句无任何夹具钉住。
            // 布局 = u_band_count(i32) + default_outcome(i32) + row_count(i32)
            //        + row_count × { key(i32), outcome(i32) }。
            int expectedPayload = 4 + 4 + 4 + output.Bound.Rows.Count * 8;
            Assert.AreEqual(expectedPayload, output.Cooked.Length - CookedFormat.HeaderSize,
                "载荷长度 = 12 + 行数×8(schema_version 的 4 字节已删;旧布局会多 4 ⇒ 本断言红)");

            // R-1 ④:头 offset 4 = formatVersion、12 = configVersion、16 = payloadLength。
            Assert.AreEqual(CookedFormat.FormatVersion, ReadU32Le(output.Cooked, 4));
            Assert.AreEqual(output.ConfigVersion, ReadU32Le(output.Cooked, 12));
            Assert.AreEqual((uint)expectedPayload, ReadU32Le(output.Cooked, 16),
                "payloadLength 槽 = 推导载荷长(头 20 + 载荷 = 总长)");
        }

        // ── F12 / 校验① 覆盖分支首次真正被执行 ────────────────────────────

        [Test]
        public void test_bake_missingWholeRow_coverageValidationFires()
        {
            // 2026-10-16 重评返工:原 `test_bake_missingCombination_hardFails` 把 key 改名
            // ⇒ 在**行解析层**就报「key 非法」⇒ errors.Count > 0 ⇒
            // `ValidateCoverage` 被 `if (uBandCount > 0 && errors.Count == 0)` 整体短路
            // (OutcomeTableBinder.cs)⇒ **校验① 的本体从未被执行过**。
            // 正确负例 = **整行删除**(行级零错误)⇒ 首次进入覆盖分支。
            // ⚠️ MinimalTableJson 的行串**不带尾逗号**(逗号由 string.Join 插入)——
            // 返工期首轮带 ", " 匹配不上;次轮只删行串留下 `,,` 致词法失败。
            // 正确做法 = 连同**前导逗号**一起删(该行非首行,前导逗号必存在)。
            string json = MinimalTableJson();
            json = json.Replace(",{\"key\": \"confirmed|applied_matched|recovered\", " +
                                "\"u_band\": 0, \"outcome\": \"no_echo\"}", "");
            Assert.IsFalse(json.Contains("confirmed|applied_matched|recovered"),
                "前置:该组合行已整行删除");
            Assert.IsFalse(json.Contains(",,"), "前置:无双逗号(JSON 仍合法)");

            var ex = Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json),
                "整行删除 ⇒ 组合空洞 ⇒ 覆盖校验硬失败(校验① 本体)");
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("覆盖") && e.Contains("缺组合行")),
                "错误消息须点名覆盖校验 + 缺失组合:" + string.Join("; ", ex.Errors));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("confirmed|applied_matched|recovered")),
                "错误消息须点名缺失的具体组合");
        }

        // ── F13 / C 轴命中位正例(AppliedMatched 链零载体)──────────────────

        [Test]
        public void test_careLaneOf_matchedTrue_yieldsAppliedMatched()
        {
            // 2026-10-16 重评返工:`CareLaneOf(cares, matched: true) → AppliedMatched`
            // 在全文件从未被断言过 —— 唯一 StubProjection 构造点用默认
            // `treatableByMatched: false` ⇒ 「处置命中 ⇒ 回响轴」这条链在 002 内
            // 无端到端载体(与已登记的 D 轴缺口同族但更窄,漏登在 C 轴)。
            var cares = new List<CareRow53>
            {
                new CareRow53(10L, 5, 0L, EventKind.CareApplied, actionId: 1, polarity: 0),
            };
            var judgments = new List<JudgmentRow53>
            {
                new JudgmentRow53(10L, 5, 0L, lexiconId: 7, confidence: 2),
            };

            Assert.AreEqual(Outcome53.CareLane.AppliedMatched,
                Outcome53.CareLaneOf(cares, matched: true),
                "有处置 + 命中位 true ⇒ AppliedMatched(命中位真源 = 9 注册表,53 不判)");
            Assert.AreEqual(Outcome53.CareLane.AppliedUnmatched,
                Outcome53.CareLaneOf(cares, matched: false),
                "有处置 + 命中位 false ⇒ AppliedUnmatched(反向对照)");
            Assert.AreEqual(Outcome53.CareLane.None,
                Outcome53.CareLaneOf(new List<CareRow53>(), matched: true),
                "零处置 ⇒ None(命中位不影响 —— 无处置即无处置)");
            Assert.AreEqual(Outcome53.CareLane.None,
                Outcome53.CareLaneOf(null, matched: true),
                "null 处置集 ⇒ None(空态防御)");

            // 端到端:真 CareApplied 载荷 + 桩给命中位 ⇒ C 轴 = AppliedMatched。
            // (经 ConsequenceInputAggregator.Aggregate 真解码,不手搓 Inputs53。)
            var table = LoadTable();
            var matched = new StubProjection(recovered: true, treatableByMatched: true);
            var stream = new List<SimEvent>
            {
                EvC(10, 5, 0),                                              // 真 CareApplied
                EvJ(20, 5, 1, lexiconId: 7, confidence: 2),                 // 真 JudgmentRecorded
                new SimEvent(30, new PatientId(5), 2, EventKind.CaseClosed, default(PayloadRef)),
            };
            var aggregated = Aggregate(stream, matched);
            Assert.AreEqual(1, aggregated.Count, "恰一组(一个 CaseClosed)");
            Assert.Greater(aggregated[0].CareRecords.Count, 0,
                "C 路确有条目(否则 C 轴恒 None,本条无判别力)");

            var lanes = ProjectLanes(aggregated[0], matched);
            Assert.AreEqual(Outcome53.CareLane.AppliedMatched, lanes.c,
                "ProjectLanes 经桩命中位 ⇒ C 轴 = AppliedMatched(端到端)");
            Assert.IsTrue(Enum.IsDefined(typeof(Outcome53.Outcome),
                Outcome53.Judge.Evaluate(
                    new Outcome53.JudgeInputs(lanes.p, lanes.c, lanes.d, false, 0, 0UL), table)),
                "该链可求值且 ∈ 闭集");
        }

        /// <summary>真载荷 `CareApplied`(C 路消费;与 medcons_aggregation_test 同款)。</summary>
        private static SimEvent EvC(long tick, int patient, long seq, int actionId = 1, int phase = 0)
        {
            var payload = new CareAppliedPayload(tick, actionId, caregiver: 1, phase);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.CareApplied,
                                Encoder.Encode(EventKind.CareApplied, payload));
        }

        // ── F16 / N-3:TryLookup 的 uBand 域守护(越界档位静默别名)────────────

        [Test]
        public void test_table_uBandOutOfRange_fallsBackToDefault_notAliasToNextCombo()
        {
            // N-3(2026-10-16 重评返工):越界 uBand 会使 key 越出本 (p,c,d) 段、落入
            // **下一个组合的段内**(bandCount=1, uBand=1 ⇒ key = pcd+1,恰为下一组合的合法键)
            // ⇒ 静默返回**另一组合**的 Outcome。全案反复登记的「静默失败」失效模式。
            const int bandCount = 1;
            var rows = new Dictionary<int, Outcome53.Outcome>();
            foreach (var p in AllP)
                foreach (var c in AllC)
                    foreach (var d in AllD)
                        rows[Outcome53.RowKeyOf(p, c, d, 0, bandCount)] =
                            (p, c, d) == (Outcome53.JudgmentLane.Empty,
                                          Outcome53.CareLane.None,
                                          Outcome53.RecoveryLane.Recovered)
                                ? Outcome53.Outcome.Residual
                                : Outcome53.Outcome.Recurrence;
            var table = Outcome53.CreateTable(rows, bandCount, true, Outcome53.Outcome.NoEcho);

            // 越界档位 ⇒ 未命中 + 回落 default 行,**不是**相邻组合的 Recurrence。
            bool hit = table.TryLookup(
                Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                Outcome53.RecoveryLane.Recovered, uBand: 1, out var aliased);
            Assert.IsFalse(hit, "uBand=1 ≥ bandCount=1 ⇒ 未命中");
            Assert.AreEqual(Outcome53.Outcome.NoEcho, aliased,
                "越界档位 ⇒ 回落 default 行;若别名到下一组合会得 Recurrence(N-3 的失效形态)");

            // 负档位同样拒收。
            Assert.IsFalse(table.TryLookup(
                Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                Outcome53.RecoveryLane.Recovered, uBand: -1, out var negative),
                "uBand = -1 ⇒ 未命中");
            Assert.AreEqual(Outcome53.Outcome.NoEcho, negative, "负档位 ⇒ 回落 default 行");

            // 经 Judge 同口径(inputs.UBand 直穿)。
            var judge = new Outcome53.JudgeInputs(
                Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                Outcome53.RecoveryLane.Recovered, false, uBand: 1, uRaw: 0UL);
            Assert.AreEqual(Outcome53.Outcome.NoEcho, Outcome53.Judge.Evaluate(judge, table),
                "Judge 对越界 UBand 回落 default 行,不吐相邻组合的 Outcome");

            // 域内不受影响(证明守护没有把合法档位也拒掉)。
            Assert.IsTrue(table.TryLookup(
                Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                Outcome53.RecoveryLane.Recovered, uBand: 0, out var inRange),
                "uBand = 0(< bandCount=1)⇒ 命中");
            Assert.AreEqual(Outcome53.Outcome.Residual, inRange, "域内档位正常查表");
        }

        // ── F17 / N-4:Table 构造器拒收 uBandCount < 1 ──────────────────────

        [Test]
        public void test_table_uBandCountZero_rejected_notSilentRowCollapse()
        {
            // N-4(2026-10-16 重评返工):uBandCount = 0 ⇒ EncodeRowKey 乘子 0
            // ⇒ 全部 18 行 key 坍缩为 uBand(通常全 0)⇒ Dictionary 构造即静默丢 17 行。
            var rows = new Dictionary<int, Outcome53.Outcome>
            {
                { Outcome53.RowKeyOf(Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                                     Outcome53.RecoveryLane.Recovered, 0, 1),
                  Outcome53.Outcome.Residual },
                { Outcome53.RowKeyOf(Outcome53.JudgmentLane.Suspected, Outcome53.CareLane.None,
                                     Outcome53.RecoveryLane.Recovered, 0, 1),
                  Outcome53.Outcome.Recurrence },
            };
            Assert.Throws<System.ArgumentException>(
                () => Outcome53.CreateTable(rows, uBandCount: 0, true, Outcome53.Outcome.NoEcho),
                "uBandCount = 0 ⇒ 构造拒收(否则行键全坍缩,字典静默丢行)");
            Assert.Throws<System.ArgumentException>(
                () => Outcome53.CreateTable(rows, uBandCount: -3, true, Outcome53.Outcome.NoEcho),
                "uBandCount < 0 ⇒ 构造拒收");

            // 合法值不受影响。
            Assert.DoesNotThrow(
                () => Outcome53.CreateTable(rows, uBandCount: 1, true, Outcome53.Outcome.NoEcho),
                "uBandCount = 1 ⇒ 放行");
        }

        [Test]
        public void test_bandOf_uRawBeyondDomain_doesNotReturnNegativeBand()
        {
            // 2026-10-16 重评返工:`(int)(uRaw / span)` 是 unchecked 窄化 —— uRaw 超域时
            // 低 32 位可为负,而 `-1 >= bandCount` 为假 ⇒ 返回**负档序数**。
            // 与 F6 同属「构面不可达仍须自守」(URaw 生产路径已 >>48 掩到 16 位)。
            foreach (int bandCount in new[] { 2, 100, 40000, 65536 })
            {
                int band = Outcome53.BandOf(ulong.MaxValue, bandCount);
                Assert.GreaterOrEqual(band, 0,
                    $"BandOf(ulong.MaxValue, {bandCount}) = {band} —— 负档序数(窄化回绕)");
                Assert.Less(band, bandCount,
                    $"BandOf(ulong.MaxValue, {bandCount}) = {band} —— 档序数须 < bandCount");
                Assert.AreEqual(0, Outcome53.BandOf(65536UL, bandCount),
                    "uRaw = 65536(恰超域 1)⇒ 退化单档 0");
            }
            // 域内不受影响(证明守护没有把合法输入也吞成 0)。
            Assert.AreEqual(1, Outcome53.BandOf(65535UL, 2),
                "uRaw = 65535(域内上界), bandCount=2 ⇒ 末档 1(span=32768, 65535/32768=1)");
            Assert.AreEqual(0, Outcome53.BandOf(32767UL, 2),
                "uRaw = 32767, bandCount=2 ⇒ 档 0(均分切点下侧)");
            Assert.AreEqual(1, Outcome53.BandOf(32768UL, 2),
                "uRaw = 32768, bandCount=2 ⇒ 档 1(均分切点上侧)");
        }

        // ── F15 / HasTrialHistory 空态 + 置换不变 ─────────────────────────

        [Test]
        public void test_oq539_nullAndEmptyStream_returnsFalse()
        {
            // 2026-10-16 重评返工:空态分支无载体(原测试只断言常量值并注释「空流语义」,
            // 却从未以空流调用)。
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(null), "null 流 ⇒ false");
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(new List<SimEvent>()),
                "空流 ⇒ false");
        }

        [Test]
        public void test_oq539_arrivalOrderPermutation_invariantResult()
        {
            // 2026-10-16 重评返工:实现自陈「任意到达序,先按全序键排序」,但所有夹具的
            // tick/seq 均已升序 ⇒ 排序分支从未被打乱过。本条故意打乱到达序。
            //
            // ⚠️ 判别性要求:夹具必须让「排序后」与「到达序」给出**不同**结论,
            //   否则删掉排序也照样绿(返工期首版即如此 —— 两组夹具两种序都 false/true)。
            //   下面这组:J 的 tick 大于前两条 D 而**小于**第四条 D。
            //   · 排序后:D,D,J,D ⇒ run=2 被打断,尾段 1 ⇒ **false**
            //   · 到达序:J,D,D,D ⇒ J 先到(无 run 可断),随后三条连跑 ⇒ **true**
            var ascending = new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1), EvJ(30, 5, 2), EvD(40, 5, 3),
            };
            var shuffled = new List<SimEvent>
            {
                EvJ(30, 5, 2), EvD(10, 5, 0), EvD(20, 5, 1), EvD(40, 5, 3),
            };

            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(ascending),
                "排序序 ⇒ J 打断 run ⇒ false(参照值)");
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(shuffled),
                "打乱到达序 ⇒ 仍按全序键排序 ⇒ 同为 false(置换不变;若排序被删,此处会得 true)");

            // 反向:达到阈值的序列打乱后仍为真。
            var runAsc = new List<SimEvent> { EvD(10, 5, 0), EvD(20, 5, 1), EvD(30, 5, 2) };
            var runShuf = new List<SimEvent> { EvD(30, 5, 2), EvD(10, 5, 0), EvD(20, 5, 1) };
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(runAsc), "升序 ⇒ 真");
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(runShuf), "打乱 ⇒ 仍真");
        }

        // ── F2 / A:同源两次烘焙逐字节一致(ADR-014 §二)─────────────────────

        [Test]
        public void test_bake_cookedBytes_deterministic_sameSourceTwice()
        {
            var a = OutcomeTable53.BakeFromRepo(RepoRoot());
            var b = OutcomeTable53.BakeFromRepo(RepoRoot());

            Assert.AreEqual(a.Cooked.Length, b.Cooked.Length, "两次烘焙长度一致");
            Assert.AreEqual(Fnv1a(a.Cooked), Fnv1a(b.Cooked),
                "同源两次烘焙逐字节一致(ADR-014 §二;FNV-1a 汇总,失败时下钻逐字节)");
            Assert.AreEqual(a.ConfigVersion, b.ConfigVersion, "ConfigVersion 亦确定");
        }

        // ── F3 / A:行键在 bandCount ∈ {1..8} 下全单射(乘子 = 逐表量)──────────

        [Test]
        public void test_rowKey_noCollision_acrossBands()
        {
            foreach (int bandCount in new[] { 1, 2, 3, 4, 5, 7, 8 })
            {
                var keys = new HashSet<int>();
                foreach (var p in AllP)
                    foreach (var c in AllC)
                        foreach (var d in AllD)
                            for (int band = 0; band < bandCount; band++)
                            {
                                int key = Outcome53.RowKeyOf(p, c, d, band, bandCount);
                                Assert.IsTrue(keys.Add(key),
                                    $"键碰撞:({p},{c},{d},band={band}) @ bandCount={bandCount} → {key}" +
                                    " —— 乘子须为逐表量 bandCount(旧式逐行量 uBand+1 会碰撞)");
                            }
                Assert.AreEqual(18 * bandCount, keys.Count,
                    $"bandCount={bandCount} ⇒ 恰 18×{bandCount} 个互异键(无丢行)");
            }
        }

        // ── F4 / B:bandCount > 1 时 U 档参与判别(inputs.UBand 不再是死字段)────

        [Test]
        public void test_table_uBandDiscriminates_whenBandCountGtOne()
        {
            const int bandCount = 2;
            var rows = new Dictionary<int, Outcome53.Outcome>();
            foreach (var p in AllP)
                foreach (var c in AllC)
                    foreach (var d in AllD)
                    {
                        // 同 (P,C,D) 两档给**不同**回响轴值 ⇒ 档位必须可判别。
                        rows[Outcome53.RowKeyOf(p, c, d, 0, bandCount)] = Outcome53.Outcome.Residual;
                        rows[Outcome53.RowKeyOf(p, c, d, 1, bandCount)] = Outcome53.Outcome.Recurrence;
                    }
            var table = Outcome53.CreateTable(rows, bandCount, true, Outcome53.Outcome.NoEcho);

            var band0 = new Outcome53.JudgeInputs(
                Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                Outcome53.RecoveryLane.Recovered, false, 0, 0UL);
            var band1 = new Outcome53.JudgeInputs(
                Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                Outcome53.RecoveryLane.Recovered, false, 1, 0UL);

            Assert.AreEqual(Outcome53.Outcome.Residual, Outcome53.Judge.Evaluate(band0, table));
            Assert.AreEqual(Outcome53.Outcome.Recurrence, Outcome53.Judge.Evaluate(band1, table),
                "U 档进入查表(B 返工:旧实现 inputs.UBand 从未被消费,band_count>1 时 U 无判别力)");

            // 表内 36 行(18 组合 × 2 档)—— 键单射的另一面。
            Assert.AreEqual(36, table.RowCount, "bandCount=2 ⇒ 36 行(碰撞会丢行 ⇒ 行数不等)");
        }

        // ── F5 / C:未命中回落 default 行,**不**是 default(枚举) = PatientLives ──

        [Test]
        public void test_table_miss_fallsBackToDefault_notLifeAxis()
        {
            var table = Outcome53.CreateTable(
                new Dictionary<int, Outcome53.Outcome>(), 1, true, Outcome53.Outcome.NoEcho);

            bool hit = table.TryLookup(
                Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                Outcome53.RecoveryLane.Recovered, 0, out var outcome);

            Assert.IsFalse(hit, "空表 ⇒ 未命中");
            Assert.AreEqual(Outcome53.Outcome.NoEcho, outcome,
                "未命中 ⇒ 回落 **default 行**(C 返工:Dictionary.TryGetValue 未命中会把 out 覆写为 " +
                "default(Outcome) = PatientLives(0) —— 结局轴,枚举注释自陈「表内永不出现」)");
            Assert.AreNotEqual(Outcome53.Outcome.PatientLives, outcome);

            // 经 Judge 同口径(默认兜底路径)。
            var inputs = new Outcome53.JudgeInputs(
                Outcome53.JudgmentLane.Confirmed, Outcome53.CareLane.AppliedMatched,
                Outcome53.RecoveryLane.Recovered, false, 0, 0UL);
            Assert.AreEqual(Outcome53.Outcome.NoEcho, Outcome53.Judge.Evaluate(inputs, table),
                "Judge 对空表亦回落 default 行,不吐结局轴");
        }

        // ── F6 / G:BandOf 在超 raw 域档数下不除零 ─────────────────────────

        [Test]
        public void test_bandOf_bandCountBeyondRawDomain_doesNotDivideByZero()
        {
            // 构面上不可达(Binder 已拒 > 65536),但 BandOf 是 Sim 侧纯函数、须自守:
            // 65536L / bandCount == 0 ⇒ uRaw / span 除零。
            Assert.DoesNotThrow(() => Outcome53.BandOf(12345UL, 65537),
                "bandCount = 65537(raw 域外)⇒ 不抛 DivideByZeroException");
            Assert.DoesNotThrow(() => Outcome53.BandOf(12345UL, int.MaxValue),
                "bandCount = int.MaxValue ⇒ 不抛");
            Assert.AreEqual(0, Outcome53.BandOf(12345UL, 65537),
                "超域档数 ⇒ 退化单档(与 bandCount ≤ 1 同口径;G 返工)");

            // 正常域不退化(证明守护没有把合法输入也吞成 0)。
            Assert.AreEqual(0, Outcome53.BandOf(0UL, 2), "bandCount=2,raw=0 ⇒ 档 0");
            Assert.AreEqual(1, Outcome53.BandOf(40000UL, 2), "bandCount=2,raw=40000 ⇒ 档 1(均分 32768)");
            Assert.AreEqual(0, Outcome53.BandOf(32767UL, 2), "bandCount=2,raw=32767 ⇒ 档 0(边界)");
            Assert.AreEqual(1, Outcome53.BandOf(32768UL, 2), "bandCount=2,raw=32768 ⇒ 档 1(边界)");
        }

        // ── F7 / D:他人事件不污染本病人的 run ────────────────────────────

        [Test]
        public void test_oq539_interleavedPatients_doNotContaminateRun()
        {
            // 证伪器:旧实现(单游标 run/runPatient)在 B 病人的 Drug 处 run=1,
            // A 病人在跑的 run 被**静默丢弃** ⇒ 整条流恒 false。
            // 新实现(逐病人 Dictionary)A 的 run 达 3 ⇒ true。
            var interleaved = new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1), EvD(30, 6, 0), EvD(40, 5, 2),
            };
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(interleaved),
                "A 的连续 run 不被 B 的病人事件打断(D 返工:GDD 规则三「同一 patient 的连续序列」)");

            // 反向:他人的 Judgment 也不打断本病人的 run。
            var foreignBreak = new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1), EvJ(30, 6, 0), EvD(40, 5, 2),
            };
            Assert.IsTrue(TrialHistoryPredicate.HasTrialHistory(foreignBreak),
                "他人(病人 6)的 Judgment 不打断病人 5 的 run");

            // 负例:两病人各 2 条 —— 谁都没到阈值。
            var twoShort = new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 6, 0), EvD(30, 5, 1), EvD(40, 6, 1),
            };
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(twoShort),
                "两病人各 2 条(< 阈值 3)⇒ 无试药史");

            // 负例:本病人自己的 Judgment 仍打断自己的 run(打断语义未被放宽)。
            var selfBreak = new List<SimEvent>
            {
                EvD(10, 5, 0), EvD(20, 5, 1), EvJ(30, 5, 2), EvD(40, 5, 3),
            };
            Assert.IsFalse(TrialHistoryPredicate.HasTrialHistory(selfBreak),
                "本病人的 Judgment 仍打断本病人的 run(OQ-53-9 逐字)");
        }

        // ── F8 / T-2 + I:表构造器拒收结局轴行值(F-53.2 定义域收窄的 Sim 侧纵深)──

        [Test]
        public void test_ac5301_outcomeAxisNarrowing_tableRejectsLifeAxisRows()
        {
            // 行值 ∈ 结局轴 ⇒ ArgumentException(Binder 校验 ② 已拦,此处防「绕过 Bake 直构表」)。
            var lifeRow = new Dictionary<int, Outcome53.Outcome>
            {
                { Outcome53.RowKeyOf(Outcome53.JudgmentLane.Empty, Outcome53.CareLane.None,
                                     Outcome53.RecoveryLane.Recovered, 0, 1),
                  Outcome53.Outcome.PatientLives },
            };
            Assert.Throws<System.ArgumentException>(
                () => Outcome53.CreateTable(lifeRow, 1, true, Outcome53.Outcome.NoEcho),
                "行值 = PatientLives ⇒ 表构造拒收(结局轴由 9 结算,53 只读)");

            // default 行 ∈ 结局轴 ⇒ 同样拒收。
            Assert.Throws<System.ArgumentException>(
                () => Outcome53.CreateTable(new Dictionary<int, Outcome53.Outcome>(), 1, true,
                                            Outcome53.Outcome.PatientLoses),
                "default 行 = PatientLoses ⇒ 表构造拒收");

            // 合法行值不受影响(回响轴三值 + NoEcho 全放行)。
            Assert.DoesNotThrow(() => Outcome53.CreateTable(new Dictionary<int, Outcome53.Outcome>
            {
                { 0, Outcome53.Outcome.Residual }, { 1, Outcome53.Outcome.Recurrence },
                { 2, Outcome53.Outcome.TrialHistory }, { 3, Outcome53.Outcome.NoEcho },
            }, 1, true, Outcome53.Outcome.NoEcho), "回响轴三值 + NoEcho 全合法");
        }

        // ── F9 / E:u_band 缺键 ⇒ 聚合 BakeValidationException(**不是** NRE)────

        [Test]
        public void test_bake_uBandMissingKey_hardFails()
        {
            // E 返工:原实现 `bandNode = default`(JsonNode 是 class ⇒ null)⇒ ReadUBand 内
            // node.Kind **NRE**,而非聚合 BakeValidationException。
            string json = MinimalTableJson().Replace("\"u_band\": 0, ", "");
            // ⚠️ 前置断言须匹配 `"u_band":` 而非裸 `u_band` —— 根键 `"u_band_count"`
            // 本身含子串 u_band,裸匹配恒真(返工期首轮实测踩过,已修)。
            Assert.IsFalse(json.Contains("u_band\":"), "前置:JSON 已无 u_band 键");

            var ex = Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json),
                "u_band 缺键 ⇒ 聚合烘焙异常(本绑定器一贯口径:拼写错误会静默丢字段 ⇒ 硬失败)");
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("u_band") && e.Contains("必填")),
                "错误消息点名 u_band 必填:" + string.Join("; ", ex.Errors));
        }

        // ── F10 / R-4:schema_version 与期望值不符 ⇒ 硬失败 ─────────────────

        [Test]
        public void test_bake_schemaVersionMismatch_hardFails()
        {
            // R-4 返工:原实现只做域检查、**从不与期望值比对** —— 读方 CookedCodec.OpenChecked
            // 正是 `schemaVersion != CookedFormat.SchemaVersion ⇒ InvalidDataException`。
            // 构建期不拦 ⇒ 错误表一路烘到运行期才炸。
            string json = MinimalTableJson().Replace("\"schema_version\": 1",
                                                     "\"schema_version\": 99");
            var ex = Assert.Throws<BakeValidationException>(
                () => OutcomeTable53.BindForTest(json),
                "schema_version ≠ CookedFormat.SchemaVersion ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("schema_version") && e.Contains("期望")),
                "错误消息点名期望值:" + string.Join("; ", ex.Errors));

            // 相符 ⇒ 放行(现网配置即此路径)。
            Assert.DoesNotThrow(() => OutcomeTable53.BindForTest(MinimalTableJson()),
                "schema_version = CookedFormat.SchemaVersion ⇒ 放行");
        }

        // ── F11 / R-3:门的浮点**字面量**判据(正例命中 + 负例不误报)──────────

        [Test]
        public void test_gate_floatLiteralPatterns_catchLiterals_notDocReferences()
        {
            // R-3:AC-53-03 原文判「零浮点**字面量**」;原门只扫标识符 token
            // (float/double/...)⇒ `1.5` / `0.5f` / `3e10` 一个都抓不到。
            var mustHit = new[]
            {
                "double x = 1.5;",
                "var y = .5f;",
                "var z = 3e10;",
                "var w = 2m;",
                "var v = 1.5E-4;",
                "var u = 12.0d;",
                "float f = 0.5f;",     // 由标识符 token 与字面量双面各捉一次
                "var a = 5F;",
                // N-6(2026-10-16 重评返工):C# 7.0+ 数字分隔符形 —— 原 `\d+` 漏这两种。
                "var sep1 = 1_000f;",
                "var sep2 = 1_000.5;",
                "var sep3 = 1_0.5e2;",
            };
            foreach (string code in mustHit)
                Assert.Greater(MedConsFloatGate.FindFloatLiterals(code).Count, 0,
                    $"须命中浮点字面量:{code}");

            // 负例:文档引用 / 字符串内容 / 标识符 / 十六进制 / 版本号 / 无后缀整数。
            var mustNotHit = new[]
            {
                "string s = \"F-53.2\";",              // 本门自己的文档引用(返工期实测误报点)
                "var v = \"1.5\";",
                "int band1f = 0;",                      // 标识符内含 f
                "uint h = 0xAd;",
                "ulong m = 0xFFFFFFFFFFFFFFFFUL;",
                "var q = 1min;",                        // m 后接单词字符 ⇒ 非字面量
                "int k = 5;",
                "var t = 65536L / bandCount;",          // 53 现行 BandOf 形态
            };
            foreach (string code in mustNotHit)
                Assert.AreEqual(0, MedConsFloatGate.FindFloatLiterals(code).Count,
                    $"不得误报:{code}");

            // 现行 53 三个源文件:零命中(反证本门的加强没有把现行代码判红)。
            // ⚠️ ConsequencesDirRelative 相对**工程根** `unity/`(门侧 RunAll(".") 同口径),
            //   而 Application.dataPath 已是 `unity/Assets` ⇒ 须上跳一级。
            string dir = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", MedConsFloatGate.ConsequencesDirRelative));
            var scanned = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(scanned.Length, 0, "扫描面非空(Assets/Sim/Consequences)");
            foreach (string f in scanned)
                Assert.AreEqual(0, MedConsFloatGate.FindFloatLiterals(File.ReadAllText(f)).Count,
                    $"现行源零浮点字面量:{Path.GetFileName(f)}");
        }
    }
}
