// 53 medical-consequences Story 001 测试 —— 入向闭集与跨流聚合
//
// 被测对象 = **生产代码** `Sim/Consequences/ConsequenceInputAggregator.cs`
//            (`Inputs53` / `ConsequenceInputAggregator.Aggregate`),
//            **不是本文件的副本**(本文件只含夹具与断言)。
//
// 卡面:`production/epics/medical-consequences/story-001-input-closure-and-stream-aggregation.md`
//   (Test Evidence 点名本路径 —— `Tests/EditMode/MedConsequences/medcons_aggregation_test.cs`)
// 权威来源:
//   GDD `design/gdd/medical-consequences.md` 规则四(按全序键聚合,非到达序)· 规则五(入向闭集)
//   · AC-53-04(置换不变)· AC-53-05(零改写计数 = 承 AC-37-20)
//   · ADR-008 §跨流全序键 `(Tick, StreamPriority, Patient, Seq)`
//   · ADR-016 §一(三源不变量)· ADR-005(纯函数)
//
// ⚠️ **本测试为纯数据层**:不触引擎 API、不触 codec、不触持久化、不读文件系统
//    (唯一例外 = AC-5 读 `Sim.asmdef` 的引用集,与 b2 门同判据 —— 见该测注释)。
// ⚠️ **置换夹具的确定性**:shuffle 用**自实现 xorshift64 + Fisher-Yates**(纯整数),不用
//    `System.Random` —— 后者跨运行时实现有差(承 tests 纪律「每次运行结果相同」)。
//    且禁用真随机种子:迭代号即种子派生的唯一输入。
//
// ⚠️ **两处已登记张力(本测试不自行裁定;归一 —— 生产件文件头 + story 的 GDD 轮)**:
//    ① 处置 Kind 集(规则五 `:165` 只列 `CareApplied` vs 卡面 Note 4 提 10/11 两支)
//       ⇒ `careKinds` 是**显式参数**;本测试**只**钉「参数被尊重」,不钉集本身该是什么。
//    ② 处置窗口下界(取 `[CaseOpened, Close]` 需第六路,与「恰五路」冲突)
//       ⇒ 本测试钉**当下口径** `Tick ≤ 结案 tick`(无下界),并注明其为**待裁口径的实现**。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 Story 001 —— 入向闭集与跨流聚合(AC-53-04 / AC-53-05)。</summary>
    [TestFixture]
    public sealed class MedconsAggregationTest
    {
        // ════════════════════════════════════════════════════════════════════
        // 夹具:事件构造 / 指纹 / 确定性置换
        // ════════════════════════════════════════════════════════════════════

        // ══ 2026-10-15 接线轮(承 ADR-031 V-8 ②「退」)══
        // Aggregate 已改解码口径 ⇒ 被解码消费的 Kind(JudgmentRecorded / CareApplied /
        // EmergencyTreatmentApplied / DrugTreatmentApplied)须**真编码** ——
        // default(PayloadRef) 解码必抛。其余 Kind(哨兵 / 干扰 / 结案)仍零值。
        private static readonly InMemoryBlobPool SharedPool = new InMemoryBlobPool();
        private static readonly PayloadEncoder Encoder = new PayloadEncoder(SharedPool);
        private static readonly PayloadDecoder Decoder = new PayloadDecoder(SharedPool);

        /// <summary>header 层事件构造(**零载荷** —— 仅用于不被解码消费的 Kind)。</summary>
        private static SimEvent Ev(long tick, int patient, long seq, EventKind kind)
        {
            return new SimEvent(tick, new PatientId(patient), seq, kind, default(PayloadRef));
        }

        /// <summary>真载荷 `JudgmentRecorded`(P 路消费;lexicon/confidence 按夹具语义给值)。</summary>
        private static SimEvent EvJ(long tick, int patient, long seq, int lexiconId = 1, byte confidence = 2)
        {
            var payload = new JudgmentRecordedPayload(
                patient, new CaseId(0, patient, 0), authorPlayerId: 1, lexiconId, confidence);
            // ⚠️ Judgment 走具名重载(通用 Encode<T> 拒收 —— freehandText 变长段)
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.JudgmentRecorded,
                                Encoder.EncodeJudgmentRecorded(payload, string.Empty));
        }

        /// <summary>真载荷 `CareApplied`(C 路消费)。</summary>
        private static SimEvent EvC(long tick, int patient, long seq, int actionId = 1, int phase = 0)
        {
            var payload = new CareAppliedPayload(tick, actionId, caregiver: 1, phase);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.CareApplied,
                                Encoder.Encode(EventKind.CareApplied, payload));
        }

        /// <summary>真载荷 `EmergencyTreatmentApplied`(C 路消费,宽 careKinds 集)。</summary>
        private static SimEvent EvE(long tick, int patient, long seq, int treatmentId = 1)
        {
            var payload = new EmergencyTreatmentAppliedPayload(
                tick, treatmentId, actorId: 1, polarity: 1,
                new Fix(0), halfLife: 100, method: 0, cause: 0, seq: 0);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.EmergencyTreatmentApplied,
                                Encoder.Encode(EventKind.EmergencyTreatmentApplied, payload));
        }

        /// <summary>真载荷 `DrugTreatmentApplied`(C 路消费,宽 careKinds 集)。</summary>
        private static SimEvent EvD(long tick, int patient, long seq, int treatmentId = 1)
        {
            var payload = new DrugTreatmentAppliedPayload(
                tick, treatmentId, actorId: 1, polarity: -1,
                new Fix(0), halfLife: 100, seq: 0,
                contraindicationBand: DrugTreatmentAppliedPayload.NoContraindication);
            return new SimEvent(tick, new PatientId(patient), seq, EventKind.DrugTreatmentApplied,
                                Encoder.Encode(EventKind.DrugTreatmentApplied, payload));
        }

        /// <summary>世界级哨兵事件(Patient = -1,承 ADR-007 §四)。</summary>
        private static SimEvent Sentinel(long tick, long seq, EventKind kind)
        {
            return Ev(tick, PatientId.None.Value, seq, kind);
        }

        /// <summary>GDD 规则五 `:165` 逐字的处置集 —— 夹具默认值(张力 ① 的实现侧取值)。</summary>
        private static readonly EventKind[] CareKindsPerGddRule5 = { EventKind.CareApplied };

        /// <summary>被测调用(GDD 规则五口径的 careKinds)。</summary>
        private static IReadOnlyList<Inputs53> Aggregate(
            IReadOnlyList<SimEvent> stream, IDiseaseProjection projection = null)
        {
            return ConsequenceInputAggregator.Aggregate(stream, Decoder, projection, CareKindsPerGddRule5);
        }

        /// <summary>
        /// 聚合结果指纹 —— 逐位可比。**每一路都展开**:漏一路则置换不变测会假绿。
        /// ⚠️ 2026-10-15 接线轮:P/C 路已改解码行 ⇒ 指纹含**载荷内容**
        /// (LexiconId/Confidence、ActionId/Polarity)—— 比旧头级指纹更强(载荷置换也红)。
        /// </summary>
        private static string Fingerprint(IReadOnlyList<Inputs53> groups)
        {
            var sb = new StringBuilder();
            sb.Append("groups=").Append(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                Inputs53 g = groups[i];
                sb.Append("\n [").Append(i).Append("] close=").Append(E(g.CaseClosed));
                sb.Append(" sentinels="); AppendList(sb, g.PatternRecognized);
                sb.Append(" P="); AppendJudgments(sb, g.JudgmentRecords);
                sb.Append(" C="); AppendCares(sb, g.CareRecords);
                sb.Append(" D=").Append(g.DiseaseProjection.Tick);
            }
            return sb.ToString();
        }

        private static string E(in SimEvent e) =>
            $"({e.Tick},{e.Patient.Value},{e.Seq},{e.Kind})";

        private static void AppendList(StringBuilder sb, IReadOnlyList<SimEvent> list)
        {
            sb.Append('[');
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(E(list[i]));
                }
            sb.Append(']');
        }

        private static void AppendJudgments(StringBuilder sb, IReadOnlyList<JudgmentRow53> list)
        {
            sb.Append('[');
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var r = list[i];
                    sb.Append($"({r.Tick},{r.PatientId},{r.Seq},lex={r.LexiconId},conf={r.Confidence})");
                }
            sb.Append(']');
        }

        private static void AppendCares(StringBuilder sb, IReadOnlyList<CareRow53> list)
        {
            sb.Append('[');
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var r = list[i];
                    sb.Append($"({r.Tick},{r.PatientId},{r.Seq},{r.Kind},act={r.ActionId},pol={r.Polarity})");
                }
            sb.Append(']');
        }

        /// <summary>
        /// 确定性置换:xorshift64 播种 + Fisher-Yates。**纯整数、无 BCL 随机源**。
        /// </summary>
        private static List<SimEvent> Shuffle(IReadOnlyList<SimEvent> src, int iteration)
        {
            var list = new List<SimEvent>(src);
            ulong s = ((ulong)iteration + 1UL) * 0x9E3779B97F4A7C15UL;
            for (int i = list.Count - 1; i > 0; i--)
            {
                s ^= s << 13; s ^= s >> 7; s ^= s << 17;      // ulong 移位 ⇒ 无符号,确定
                int j = (int)(s % (ulong)(i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        /// <summary>夹具投影实现(卡面 Note 3:9 侧形状未就位 ⇒ 由夹具承位)。</summary>
        private sealed class StubProjection : IDiseaseProjection
        {
            private readonly HashSet<long> _missingTicks;
            public int Calls { get; private set; }

            public StubProjection(params long[] missingTicks)
            {
                _missingTicks = new HashSet<long>(missingTicks ?? Array.Empty<long>());
            }

            public bool TryProject(PatientId patient, long tick, out DiseaseProjectionSnapshot snapshot)
            {
                Calls++;
                if (_missingTicks.Contains(tick))
                {
                    snapshot = default;
                    return false;                      // 边界退化路径
                }
                snapshot = new DiseaseProjectionSnapshot(tick);
                return true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 混合夹具 —— 50 事件,三流混合 + 哨兵(卡面 QA AC-1 的 Given)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 三流混合夹具 —— **恰 50 条**(卡面 QA AC-1 的 Given):3 个病人 + 世界级哨兵
        /// + **闭集之外的干扰事件**(世界流 / 病史流其他支),用于证明聚合器按 Kind 过滤、
        /// 不把干扰项算进任何一路。产出 **3 组**(3 个 `CaseClosed`)。
        /// </summary>
        private static List<SimEvent> MixedFixture()
        {
            var s = new List<SimEvent>();

            // ── 病人 1(结案 tick=100)──
            s.Add(Ev(10, 1, 0, EventKind.CaseOpened));
            s.Add(EvC(20, 1, 0));
            s.Add(EvJ(30, 1, 0, lexiconId: 1));
            s.Add(EvC(40, 1, 0, actionId: 2));
            s.Add(Ev(50, 1, 0, EventKind.JudgmentRevised));       // 改写史 —— **不在闭集**,须被忽略
            s.Add(EvJ(60, 1, 0, lexiconId: 3));                   // 最后一笔 ⇒ P 应取这条
            s.Add(EvC(70, 1, 0));
            s.Add(Ev(100, 1, 0, EventKind.CaseClosed));

            // ── 病人 2(结案 tick=120;**结案后**仍有事件 ⇒ 不得进窗口)──
            s.Add(Ev(10, 2, 1, EventKind.CaseOpened));
            s.Add(EvC(80, 2, 0));
            s.Add(EvJ(90, 2, 0, lexiconId: 2));
            s.Add(Ev(120, 2, 0, EventKind.CaseClosed));
            s.Add(EvC(130, 2, 0));                                // 结案之后 ⇒ 出窗
            s.Add(EvJ(140, 2, 0, lexiconId: 9));                  // 结案之后 ⇒ 出窗(不得覆盖 P)

            // ── 病人 3(结案 tick=120,**与病人 2 同 tick** —— 全序键靠 Patient 分离)──
            s.Add(Ev(15, 3, 0, EventKind.CaseOpened));
            s.Add(EvJ(25, 3, 0, lexiconId: 4));
            s.Add(Ev(120, 3, 0, EventKind.CaseClosed));

            // ── 世界级哨兵(Patient = -1)──
            s.Add(Sentinel(25, 0, EventKind.PatternRecognized));
            s.Add(Sentinel(120, 0, EventKind.PatternRecognized));  // 与结案同 tick

            // ── 闭集之外的干扰(世界流 / 病史流其他支)──
            s.Add(Ev(20, 1, 1, EventKind.InjuryOnset));
            s.Add(Ev(35, 2, 2, EventKind.SkillGrown));
            s.Add(Ev(45, 3, 3, EventKind.ActorCellEntered));
            s.Add(Ev(55, 1, 4, EventKind.ResourceHarvested));
            s.Add(Ev(65, 2, 5, EventKind.DropSpawned));
            s.Add(Ev(75, 3, 6, EventKind.StructurePlaced));
            s.Add(Ev(85, 1, 7, EventKind.EncounterStarted));
            s.Add(Ev(95, 2, 8, EventKind.EventRolled));
            s.Add(Ev(105, 3, 9, EventKind.PoiStateChanged));
            s.Add(Ev(110, 1, 10, EventKind.PlayerDied));
            s.Add(Ev(115, 2, 11, EventKind.ConsequenceResolved));
            s.Add(Ev(118, 3, 12, EventKind.EnemyInjuryOnset));

            // ── 追加干扰(凑足卡面 QA AC-1 的「50 事件」;扩大闭集外 Kind 的覆盖面)──
            s.Add(Ev(101, 1, 11, EventKind.StructureModified));
            s.Add(Ev(102, 2, 12, EventKind.StructureRemoved));
            s.Add(Ev(103, 3, 13, EventKind.DropClaimed));
            s.Add(Ev(104, 1, 14, EventKind.DropDespawned));
            s.Add(Ev(106, 2, 15, EventKind.InjuryStateChanged));
            s.Add(Ev(107, 3, 16, EventKind.EncounterEnded));
            s.Add(Ev(108, 1, 17, EventKind.Craft));
            s.Add(Ev(109, 2, 18, EventKind.CompoundTriggered));
            s.Add(Ev(111, 3, 19, EventKind.CompoundExpired));
            s.Add(Ev(112, 1, 20, EventKind.EmergencyAttempt));
            s.Add(Ev(113, 2, 21, EventKind.EventArrived));
            s.Add(Ev(114, 3, 22, EventKind.ThreatDeferred));
            s.Add(Ev(116, 1, 23, EventKind.ThreatDeferralCleared));
            s.Add(Ev(117, 2, 24, EventKind.HistoryFlagChanged));
            s.Add(Ev(119, 3, 25, EventKind.ResourceHarvested));
            s.Add(Ev(121, 1, 26, EventKind.CaseOpened));    // 病人 1 **二次开案**且永不结案
            s.Add(Ev(122, 2, 27, EventKind.ActorCellEntered));   // ⇒ 不得产出第二组
            s.Add(Ev(123, 3, 28, EventKind.PoiStateChanged));
            s.Add(Ev(124, 1, 29, EventKind.PlayerDied));

            return s;
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-1 组 —— 置换不变(AC-53-04)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-53-04:同一批事件任意打乱到达序 ⇒ 聚合结果**逐位一致**。
        /// 卡面 QA AC-1:固定伪随机置换 × 20。
        /// </summary>
        [Test]
        public void test_ac5304_permutationInvariance_twentyShufflesYieldIdenticalOutput()
        {
            List<SimEvent> canonical = MixedFixture();
            string baseline = Fingerprint(Aggregate(canonical, new StubProjection()));

            Assert.That(canonical.Count, Is.EqualTo(50),
                "前置:夹具须恰 50 事件(卡面 QA AC-1 的 Given;改动夹具须同步订正此数)");
            Assert.That(baseline, Does.Contain("groups=3"),
                "前置:夹具须产出 3 组(3 个 CaseClosed)—— 否则本测的判别力不成立");

            for (int iter = 0; iter < 20; iter++)
            {
                List<SimEvent> shuffled = Shuffle(canonical, iter);
                Assert.That(shuffled.Select(x => E(in x)).SequenceEqual(canonical.Select(y => E(in y))), Is.False,
                    $"前置:第 {iter} 次置换须真的改变到达序(否则该轮恒等置换,测不出任何东西)");

                string actual = Fingerprint(Aggregate(shuffled, new StubProjection()));
                Assert.That(actual, Is.EqualTo(baseline),
                    $"AC-53-04:第 {iter} 次置换后结果须逐位一致(到达序不得影响聚合)");
            }
        }

        /// <summary>
        /// 卡面 QA AC-1 Edge:同 `(Tick, StreamPriority, Patient)`、不同 `Seq`
        /// —— 排序须确定(靠 Seq 分量)、**不崩**。
        /// </summary>
        [Test]
        public void test_ac5304_sameTickStreamPatientDifferentSeq_isOrderedBySeq()
        {
            var multi = new List<SimEvent>
            {
                EvC(50, 7, 2),
                EvC(50, 7, 0),
                EvC(50, 7, 1),
                Ev(50, 7, 3, EventKind.CaseClosed),
            };

            IReadOnlyList<Inputs53> groups = Aggregate(multi);
            Assert.That(groups.Count, Is.EqualTo(1));

            long[] seqs = groups[0].CareRecords.Select(x => x.Seq).ToArray();
            Assert.That(seqs, Is.EqualTo(new long[] { 0, 1, 2 }),
                "同 (Tick, Stream, Patient) 内须按 Seq 升序(全序键的第四分量)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-2 组 —— 哨兵先行(卡面 QA AC-2)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-2:同 tick 的 `PatternRecognized(Patient=-1)` 排序先于病人事件。</summary>
        [Test]
        public void test_ac5304_sentinelSortsBeforePatientEventAtSameTick()
        {
            var stream = new List<SimEvent>
            {
                EvJ(30, 3, 0),
                Sentinel(30, 0, EventKind.PatternRecognized),
                Ev(30, 3, 1, EventKind.CaseClosed),
            };

            IReadOnlyList<Inputs53> groups = Aggregate(stream);
            Assert.That(groups.Count, Is.EqualTo(1),
                "同 tick 的哨兵不得被误判为「未结案」而漏掉该组(AC-2 逐字要求)");

            // ⚠️ **取证口径说明**:哨兵与病人事件在聚合器输出里**天然分路**
            //    (第二路 vs 第三路),故「谁先」无法从输出直接读出 —— 本测改为
            //    **直接对全序键取证**:它是 AC-53-04 的**定序前提**(键错 ⇒ 聚合必错)。
            SimEvent sentinel = Sentinel(30, 0, EventKind.PatternRecognized);
            SimEvent patientEv = EvJ(30, 3, 0);
            Assert.That(EventOrder.Compare(in sentinel, in patientEv), Is.LessThan(0),
                "哨兵 (Patient=-1) 须排在同 tick 病人事件之前(AC-53-04 前向悬垂的定序前提)");
        }

        /// <summary>
        /// AC-2:**聚合不把哨兵并入该病人的 P 折叠** —— 哨兵是世界级事件(Patient=-1),
        /// 病人 3 的 P 路只应含病人 3 自己的 `JudgmentRecorded`。
        /// </summary>
        [Test]
        public void test_ac5304_sentinelNotFoldedIntoPatientJudgmentLane()
        {
            var stream = new List<SimEvent>
            {
                EvJ(30, 3, 0),
                Sentinel(30, 0, EventKind.PatternRecognized),
                Ev(30, 3, 1, EventKind.CaseClosed),
            };

            IReadOnlyList<Inputs53> groups = Aggregate(stream);
            Assert.That(groups[0].JudgmentRecords.Count, Is.EqualTo(1),
                "病人 3 的 P 路恰 1 条(哨兵 Patient=-1 ≠ 3,不得并入)");
            Assert.That(groups[0].JudgmentRecords[0].PatientId, Is.EqualTo(3));
            Assert.That(groups[0].PatternRecognized.Count, Is.EqualTo(1),
                "哨兵须落在**第二路**(世界级加速器),不在 P 路");
            Assert.That(groups[0].PatternRecognized[0].Patient.Value,
                Is.EqualTo(PatientId.None.Value));
        }

        /// <summary>AC-2 Edge:纯哨兵流 ⇒ 空病例结果集(无 `CaseClosed` ⇒ 零组)。</summary>
        [Test]
        public void test_ac5304_pureSentinelStream_yieldsNoCaseGroups()
        {
            var stream = new List<SimEvent>
            {
                Sentinel(10, 0, EventKind.PatternRecognized),
                Sentinel(20, 0, EventKind.PatternRecognized),
            };

            IReadOnlyList<Inputs53> groups = Aggregate(stream);
            Assert.That(groups, Is.Empty,
                "纯哨兵流无 CaseClosed ⇒ 聚合退化为空结果(非「未结案组」)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-3 组 —— 闭集反射守卫(AC-53-05 / 承 AC-37-20)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-1 逐字:`Inputs53` 结构**恰含五路**(类型级闭集)。
        /// 第六路存在 ⇒ 本测红(以 AC-53-05 的反射断言为之)。
        /// </summary>
        [Test]
        public void test_ac5305_inputs53_hasExactlyFiveLanes()
        {
            FieldInfo[] fields = typeof(Inputs53)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(fields.Length, Is.EqualTo(5),
                "Inputs53 须恰含五路(CaseClosed / PatternRecognized / JudgmentRecorded / " +
                "CareApplied / 病程投影);实际:" +
                string.Join(",", fields.Select(f => f.Name)));

            CollectionAssert.AreEquivalent(
                new[] { "CaseClosed", "PatternRecognized", "JudgmentRecords",
                        "CareRecords", "DiseaseProjection" },
                fields.Select(f => f.Name).ToArray(),
                "五路的**具名**须与 GDD 规则五 `:165` 的一一对应(改名 = 改契约,须同步改卡面)");
        }

        /// <summary>递归收集类型上可触及的名字:字段名 + 泛型实参名(卡面 QA AC-3 Edge)。</summary>
        private static List<string> CollectReachableNames(Type t, int depth = 0)
        {
            var names = new List<string>();
            if (t == null || depth > 4) return names;

            names.Add(t.Name);
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.Instance | BindingFlags.Static))
            {
                names.Add(f.Name);
                names.AddRange(CollectReachableNames(f.FieldType, depth + 1));
            }
            return names;
        }

        /// <summary>黑名单命中(大小写不敏感,去下划线 —— 覆盖 `_rewriteCount` 形态)。</summary>
        private static List<string> ForbiddenHits(IEnumerable<Type> types)
        {
            string[] black = { "rewritecount", "revisioncount", "edithistory",
                               "rewritehistory", "editcount", "revisionhistory" };
            var hits = new List<string>();
            foreach (Type t in types)
                foreach (string n in CollectReachableNames(t))
                {
                    string flat = n.Replace("_", string.Empty).ToLowerInvariant();
                    foreach (string b in black)
                        if (flat.Contains(b)) hits.Add($"{t.Name}.{n}");
                }
            return hits;
        }

        /// <summary>
        /// AC-53-05:53 的全部类型(载面 = `DaYiJingCheng.Sim.Consequences` 命名空间)
        /// 经**递归**反射扫描 ⇒ 零改写/修订计数类字段。
        /// <para>⚠️ **扫描面的载体识别**(与 AC 原文「53 程序集」的关系):53 **无独立程序集**
        /// (ADR-025 具名六装配清单里没有 53 专属件)⇒ 其载体 = `Sim` 程序集内的本命名空间。
        /// **刻意不扫全 `Sim` 程序集** —— 那会把 9 / 25 / 37 的字段纳入 53 的判据
        /// (越权;承 `review-workflow` 义务 4「评据不得越权」)。</para>
        /// </summary>
        [Test]
        public void test_ac5305_noRewriteCountFieldsInConsequencesNamespace()
        {
            Type[] owned = typeof(ConsequenceInputAggregator).Assembly.GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Sim.Consequences")
                .ToArray();

            Assert.That(owned, Is.Not.Empty,
                "前置:53 载面须非空(命名空间改名 ⇒ 本测失去判别力,须同步订正)");

            List<string> hits = ForbiddenHits(owned);
            Assert.That(hits, Is.Empty,
                "53 绝不消费改写次数(GDD 规则五 · AC-53-05 / AC-37-20)—— 命中:" +
                string.Join(",", hits));
        }

        /// <summary>
        /// **判别力自检**(卡面 AC-3 逐字:「注入 `int revision_count` 负夹具必红」):
        /// 同一台扫描器对一个**注入了它**的对照类型必须点名 —— 否则本守卫恒真(假绿)。
        /// </summary>
        [Test]
        public void test_ac5305_guardHasDiscriminatingPowerOnInjectedFixture()
        {
            List<string> hits = ForbiddenHits(new[] { typeof(ShadowWithRevisionCount) });

            Assert.That(hits, Is.Not.Empty,
                "判别力自检:扫描器对注入的 revision_count 必须命中 —— " +
                "空结果证明守卫恒真,上方 AC-53-05 测即为假绿");
            StringAssert.Contains("revision_count", string.Join(",", hits),
                "命中须**点名**注入字段");
        }

        /// <summary>负夹具:注入 `revision_count` 的对照类型(仅用于判别力自检)。</summary>
        private sealed class ShadowWithRevisionCount
        {
#pragma warning disable CS0169, CS0649
            private readonly int revision_count;
            private readonly List<int> edit_history_size;
#pragma warning restore CS0169, CS0649
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-5 组 —— 病程投影经只读 DTO 边界
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-5:`Sim` 的 asmdef 引用集 = 契约件(**门 A** 的编译期表达)。
        /// ⚠️ 与既有 b2 门**同判据**,此处为 story-001 的 AC 落点(非新判据)。
        /// </summary>
        [Test]
        public void test_ac53_diseaseProjectionBoundary_simAsmdefReferencesOnlyContracts()
        {
            string[] refs = typeof(ConsequenceInputAggregator).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name).ToArray();

            Assert.That(refs, Does.Contain("Sim.Contracts"),
                "53 须经 `Sim.Contracts` 取契约件(第四路 D 的 DTO 边界在其内)");
            Assert.That(refs.Where(r => r.StartsWith("Unity")).ToArray(), Is.Empty,
                "门 A:53 住 `Sim` ⇒ 零引擎程序集引用(D = 只读 DTO,不得读 9 的私有态)");
            Assert.That(refs.Where(r => r.StartsWith("Sim.") && r != "Sim.Contracts").ToArray(),
                Is.Empty,
                "53 不得引用 `Sim.Codec` 等其它 Sim 族程序集 —— 门 A 边界:载荷解码" +
                "**经 `Sim.Contracts` 的 `IPayloadDecoder` 抽象点**(ADR-031 §①),不经 `Sim.Codec`" +
                "(2026-10-15 接线轮订正:原「不读载荷内容」措辞已随 V-8 ②「退」失效," +
                "但**引用集判据不变**);实际:" + string.Join(",", refs));
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-4 组 —— 边界退化(卡面 QA AC-4)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-4:空流 ⇒ 空结果,不抛。</summary>
        [Test]
        public void test_boundary_emptyStream_yieldsEmpty()
        {
            Assert.That(Aggregate(new List<SimEvent>()), Is.Empty);
            Assert.That(Aggregate(null), Is.Empty, "null 流亦不得抛(边界退化)");
        }

        /// <summary>
        /// AC-4:单 `CaseClosed` ⇒ 一组,缺 P / C 按 GDD 缺省语义(空投影,非错误)。
        /// </summary>
        [Test]
        public void test_boundary_singleCaseClosed_yieldsOneGroupWithEmptyPC()
        {
            var stream = new List<SimEvent> { Ev(100, 5, 0, EventKind.CaseClosed) };

            IReadOnlyList<Inputs53> groups = Aggregate(stream);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].CareRecords, Is.Empty, "C 空投影");
            Assert.That(groups[0].JudgmentRecords, Is.Empty, "P 空投影");
            Assert.That(groups[0].PatternRecognized, Is.Empty, "无哨兵 ⇒ 第二路空");
            Assert.That(groups[0].DiseaseProjection.Tick, Is.EqualTo(0L),
                "无投影实现(null)⇒ D 为 default");
        }

        /// <summary>AC-4:「结案无判断」= **合法输入**(P 空,交 story-002 判 missed),不抛。</summary>
        [Test]
        public void test_boundary_closeWithoutJudgment_isLegalInputNotThrow()
        {
            var stream = new List<SimEvent>
            {
                Ev(10, 5, 0, EventKind.CaseOpened),
                EvC(20, 5, 0),
                Ev(100, 5, 0, EventKind.CaseClosed),
            };

            IReadOnlyList<Inputs53> groups = null;
            Assert.DoesNotThrow(() => groups = Aggregate(stream));
            Assert.That(groups[0].JudgmentRecords, Is.Empty,
                "结案无判断 ⇒ P = 空投影(**合法**,非错误 —— missed 由 story-002 判)");
            Assert.That(groups[0].CareRecords.Count, Is.EqualTo(1));
        }

        /// <summary>AC-4:流中有处置/判断但**无任何 `CaseClosed`** ⇒ 空结果(零组)。</summary>
        [Test]
        public void test_boundary_projectionOnly_noCaseClosed_yieldsEmpty()
        {
            var stream = new List<SimEvent>
            {
                EvC(10, 5, 0),
                EvJ(20, 5, 0),
            };

            var proj = new StubProjection();
            Assert.That(Aggregate(stream, proj), Is.Empty);
        }

        /// <summary>AC-4 边界:投影返回 false(无投影)⇒ D 为 default,不抛。</summary>
        [Test]
        public void test_boundary_projectionReportsMissing_isNotAnError()
        {
            var stream = new List<SimEvent> { Ev(100, 5, 0, EventKind.CaseClosed) };
            var proj = new StubProjection(100L);

            IReadOnlyList<Inputs53> groups = Aggregate(stream, proj);
            Assert.That(groups[0].DiseaseProjection.Tick, Is.EqualTo(0L),
                "TryProject=false ⇒ D 保持 default(边界退化,非异常)");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-6 组 —— 投影语义(卡面 Note 1 / 张力 ① ②)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 卡面 Note 1:P 的「@结案时刻」投影 = 该病例**最后一笔**判断记录。
        /// (夹具里病人 1 有两笔 `JudgmentRecorded`(tick 30 / 60)+ 一笔 `JudgmentRevised`(50)。)
        /// </summary>
        [Test]
        public void test_judgmentProjection_keepsOnlyLastJudgmentBeforeClose()
        {
            IReadOnlyList<Inputs53> groups = Aggregate(MixedFixture());
            Inputs53 p1 = groups.Single(g => g.CaseClosed.Patient.Value == 1);

            Assert.That(p1.JudgmentRecords.Count, Is.EqualTo(1),
                "P 路恰 1 条(**最后一笔**,非全部)");
            Assert.That(p1.JudgmentRecords[0].Tick, Is.EqualTo(60L),
                "P 须取结案前**最后一笔** JudgmentRecorded(tick=60),不是 30");
            // ⚠️ 2026-10-15 接线轮:P 路已改解码行(无 Kind 字段)⇒ 闭集判别改**内容级**:
            // 夹具的改写史 @50(若被误计入 P)会顶掉 tick=60 那笔 ⇒ 上面 Tick 断言红;
            // 且本行钉载荷内容真进了行(lexiconId=3 来自 @60 那笔真载荷)。
            Assert.That(p1.JudgmentRecords[0].LexiconId, Is.EqualTo(3),
                "P 行携带**载荷内容**(lexicon_id=3,@60 那笔)—— JudgmentRevised 若混入,内容必漂移");
        }

        /// <summary>
        /// 张力 ② 的**当下口径**:处置窗口 = `Tick ≤ 结案 tick`(无下界)。
        /// 夹具里病人 2 在结案后(tick 130)另有处置 ⇒ 须**出窗**。
        /// </summary>
        [Test]
        public void test_careProjection_excludesEventsAfterCloseTick()
        {
            IReadOnlyList<Inputs53> groups = Aggregate(MixedFixture());
            Inputs53 p2 = groups.Single(g => g.CaseClosed.Patient.Value == 2);

            Assert.That(p2.CareRecords.Select(x => x.Tick).ToArray(),
                Is.EqualTo(new long[] { 80L }),
                "C 窗口 = Tick ≤ 120(结案 tick);tick=130 的处置须出窗");
            Assert.That(p2.JudgmentRecords[0].Tick, Is.EqualTo(90L),
                "P 同样受窗口约束 —— tick=140 的判断不得覆盖 tick=90 的最后一笔");
        }

        /// <summary>同 tick 的处置算**在窗内**(口径:`>` 停,不是 `>=`)。</summary>
        [Test]
        public void test_careProjection_includesEventsAtExactlyCloseTick()
        {
            var stream = new List<SimEvent>
            {
                EvC(50, 9, 0),    // 同 tick,seq 0 ⇒ 在结案之前
                Ev(50, 9, 1, EventKind.CaseClosed),     // seq 1 ⇒ 结案
            };

            IReadOnlyList<Inputs53> groups = Aggregate(stream);
            Assert.That(groups[0].CareRecords.Count, Is.EqualTo(1),
                "同 tick 且 Seq 更小的处置须在窗内(结案前发生)");
        }

        /// <summary>
        /// 张力 ①:`careKinds` 是**显式参数** —— 聚合器尊重传入集,不自带隐含默认。
        /// 本测**只**钉「参数被尊重」,**不**钉该集本身该是什么(那归 53 的 GDD 轮)。
        /// </summary>
        [Test]
        public void test_careProjection_obeysCareKindsParameter()
        {
            var stream = new List<SimEvent>
            {
                EvC(10, 4, 0),
                EvE(20, 4, 0, treatmentId: 5),                       // 窄集下不解码;宽集下须真载荷
                EvD(30, 4, 0, treatmentId: 6),                       // 宽集下须真载荷(CareRowOf 分派)
                Ev(100, 4, 0, EventKind.CaseClosed),
            };

            // ① GDD 规则五 `:165` 逐字集 —— 只 `CareApplied`
            IReadOnlyList<Inputs53> narrow =
                ConsequenceInputAggregator.Aggregate(stream, Decoder, null, CareKindsPerGddRule5);
            Assert.That(narrow[0].CareRecords.Select(x => x.Kind).ToArray(),
                Is.EqualTo(new[] { EventKind.CareApplied }),
                "传入 GDD 规则五的窄集 ⇒ C 路只收 CareApplied");

            // ② 卡面 Note 4 提及的更宽集 —— 聚合器须如实响应(不硬编码任何一种)
            IReadOnlyList<Inputs53> wide = ConsequenceInputAggregator.Aggregate(stream, Decoder, null,
                new[] { EventKind.CareApplied, EventKind.EmergencyTreatmentApplied,
                        EventKind.DrugTreatmentApplied });
            Assert.That(wide[0].CareRecords.Count, Is.EqualTo(3),
                "传入更宽集 ⇒ C 路全收 —— 证明 Kind 集**不是**聚合器内嵌常量(张力 ① 待裁)");

            // ③ null 集 ⇒ 零处置(退化,不抛)
            IReadOnlyList<Inputs53> none =
                ConsequenceInputAggregator.Aggregate(stream, Decoder, null, null);
            Assert.That(none[0].CareRecords, Is.Empty, "null careKinds ⇒ 零处置,不抛");
        }

        /// <summary>结果按**结案事件的全序**排序(同 tick 靠 Patient 分离)。</summary>
        [Test]
        public void test_result_groupsOrderedByCloseEventOrder()
        {
            IReadOnlyList<Inputs53> groups = Aggregate(MixedFixture());

            Assert.That(groups.Select(g => g.CaseClosed.Patient.Value).ToArray(),
                Is.EqualTo(new[] { 1, 2, 3 }),
                "结案 tick:1→100,2→120,3→120;同 tick 120 时 Patient 2 < 3 ⇒ 序 1,2,3");
        }

        /// <summary>闭集之外的干扰事件(世界流等)**不混入任何一路**。</summary>
        [Test]
        public void test_outOfClosureEvents_neverAppearInAnyLane()
        {
            IReadOnlyList<Inputs53> groups = Aggregate(MixedFixture());
            EventKind[] closure =
            {
                EventKind.CaseClosed, EventKind.PatternRecognized,
                EventKind.JudgmentRecorded, EventKind.CareApplied,
            };

            foreach (Inputs53 g in groups)
            {
                var seen = new List<EventKind>();
                seen.AddRange(g.PatternRecognized.Select(x => x.Kind));
                seen.Add(EventKind.JudgmentRecorded);   // P 路解码行:聚合器只收该 Kind(闭集由构造保证)
                seen.AddRange(g.CareRecords.Select(x => x.Kind));

                foreach (EventKind k in seen)
                    Assert.That(closure, Does.Contain(k),
                        $"闭集外的 Kind 渗入了某一路:{k}(GDD 规则五:入向契约是闭集)");
            }
        }
    }
}
