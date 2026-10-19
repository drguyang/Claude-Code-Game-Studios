// 权威来源:ADR-019 §五(P0 切面 = 重算器 + 调试视图 + 导出)·
//   production/epics/telemetry-analytics/story-003-f2-misdiagnosis-matrix.md
//   (AC-51-B5…B7 — F2 误诊分布)。
//
// ⚠️ 本件是 TD-epic-01 F2 **接线判据**的夹具(2026-10-19):
//   TelemetryReporter 此前只被「编译通过 + 全量零回归」证明,没有一条测试
//   调用过它 ⇒ F2 在生产里算不出来。本夹具用 **真** EventStream + **真**
//   PayloadEncoder/Decoder + **真** InMemoryBlobPool 驱动它,断言「生产里真能
//   算出 F2 混淆矩阵 + 三桶」—— 与 EditMode 里喂假 CaseRecord 的替身是两回事。
//
// ⚠️ 关键空间(必须三方对齐,否则矩阵全错):
//   · truth = CaseClosed 位掩码展开:bit 位置 i → DiseaseId = i+1(1-based,
//     承 PatternDetector.DiseaseOrdinalsOf / ADR-014 append-only ordinal);
//   · judged = JudgmentRecorded/Revised 的 lexionId(P0 阶段按 lexicon_id == disease_id,
//     与 truth 同 DiseaseId 空间,承 ADR-030 Q3);
//   · diseaseEnum = 注册表的 DiseaseId 字符串集(TelemetryReporter 构造时注入)。
//
// 证伪判别力(每条若生产码守卫被移除/改错 ⇒ 本条转红):
//   · 位掩码未展开(把 Bits 当单值) ⇒ truth 集合错 ⇒ 矩阵行/列错 ⇒ 红;
//   · truth 不做 bit+1 ⇒ bit 0 → "0" ∉ diseaseEnum ⇒ nUnmappable 假升 ⇒ 红;
//   · judged 不走 lexionId ⇒ 列错 ⇒ 红;
//   · diseaseEnum 不注入(用 fallback 只有 "1") ⇒ DiseaseId 2/3 全判 unmappable ⇒ 红.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F2WiringTest
    {
        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private IIdAuthority _ids;
        private PresenceRegistry _presence;
        private EventStream _stream;
        private TelemetryReporter _reporter;
        private long _tailTick;

        /// <summary>最小注册表(DiseaseId → DiseaseKey)。DiseaseId 与位掩码 bit 位置
        /// 的映射:bit i ↔ DiseaseId i+1。F2 wiring 使用 1..3。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> Registry(params int[] ids)
            => ids.Select(id => new DiseaseRegistryEntry
            {
                DiseaseId = id,
                DiseaseKey = $"d{id}",
            }).ToList();

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _ids = NewIdAuthority();
            _presence = new PresenceRegistry();
            _stream = new EventStream(_ids, _presence);
            // F2 走 Compute,须把 truth/judged 还原成 diseaseEnum 空间 ⇒ registry 不能是
            // fallback(只有 DiseaseId=1),否则 DiseaseId 2/3 的案例全被判 unmappable。
            _reporter = new TelemetryReporter(_stream, _decoder, Registry(1, 2, 3));
            _tailTick = 10;
        }

        private static IIdAuthority NewIdAuthority() => new SeqIdAuthority();

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        /// <summary>写入一条 CaseClosed(真值 = 位掩码)—— 经真编码器入池 + 真写面入流。</summary>
        private void AppendCaseClosed(long caseSeq, ulong diseaseBits)
        {
            var payload = new CaseClosedPayload(
                patientId: 1,
                new CaseId(10, 1, caseSeq),
                new DiseaseIdSet(diseaseBits),
                treated: true);
            PayloadRef r = _encoder.Encode(EventKind.CaseClosed, payload);
            // ⚠️ tick 用递增尾序而非恒定 10:BCD-码-3 要求流按 Tick 非降,
            //    多案交错时恒定 tick 会与后续 JudgmentRecorded(11)冲突回退 ⇒ 误红。
            _stream.Append(new SimEvent(_tailTick++, new PatientId(1), -1, EventKind.CaseClosed, r));
        }

        /// <summary>写入一条 JudgmentRecorded(判 lexionId)—— 同上,真链路。</summary>
        private void AppendJudgmentRecorded(long caseSeq, int lexiconId)
        {
            var payload = new JudgmentRecordedPayload(
                patientId: 1,
                new CaseId(10, 1, caseSeq),
                authorPlayerId: 1,
                lexiconId,
                confidence: 2);
            PayloadRef r = _encoder.EncodeJudgmentRecorded(payload, "");
            _stream.Append(new SimEvent(_tailTick++, new PatientId(1), -1, EventKind.JudgmentRecorded, r));
        }

        /// <summary>写入一条 JudgmentRevised(改写,存新值)—— 载荷类型与 Recorded 不同,走具名重载。</summary>
        private void AppendJudgmentRevised(long caseSeq, int lexiconId)
        {
            var payload = new JudgmentRevisedPayload(
                patientId: 1,
                new CaseId(10, 1, caseSeq),
                authorPlayerId: 1,
                lexiconId,
                confidence: 3);
            PayloadRef r = _encoder.EncodeJudgmentRevised(payload, "");
            _stream.Append(new SimEvent(_tailTick++, new PatientId(1), -1, EventKind.JudgmentRevised, r));
        }

        // ══════════════ 接线正向 ══════════════

        /// <summary>核心接线:真链路端到端 ⇒ 混淆矩阵 + 三桶全部算出。</summary>
        [Test]
        public void test_wired_realStream_f2MatrixAndBuckets()
        {
            // Arrange:位掩码 bit i ↔ DiseaseId i+1; lexionId P0 阶段 == disease_id。
            //   病例流的三条也全部入流:错序版本 ⇒ `Seq` 键不符 ⇒ 错判为 N_open(可观测)且 tick 不冲突。
            //   c1: D={1}(bit0) ∧ J={1}          ⇒ M[1][1]      (判对)
            //   c2: D={2}(bit1) ∧ J={1}          ⇒ M[2][1]      (判错)
            //   c3: D=∅(bits=0)   ∧ J={1}        ⇒ M[无病][1]   (无病误开方)
            //   c4: 仅判断 J={2}, 无 CaseClosed  ⇒ N_open++
            //   c5: D={1,2}(bits 0,1,共病) ∧ J={1} ⇒ N_comorbid++, 不进矩阵
            //   c6: D={1} ∧ J={9}(9 ∉ registry)  ⇒ N_unmappable++
            foreach (long caseSeq in new long[] { 1L, 2L, 3L, 4L, 5L, 6L })
            {
                int lexiconId = caseSeq == 4L ? 2 : caseSeq == 6L ? 9 : 1;
                if (caseSeq != 4L)
                {
                    ulong bits = caseSeq == 1L || caseSeq == 6L ? 1UL << 0
                               : caseSeq == 2L ? 1UL << 1
                               : caseSeq == 3L ? 0UL
                               : (1UL << 0) | (1UL << 1);
                    AppendCaseClosed(caseSeq, bits);                 // 每条先 Close(ADR-008 流序)
                }
                AppendJudgmentRecorded(caseSeq, lexiconId);
            }

            // Act
            string report = _reporter.MisdiagnosisReport(out int skipped);

            // Assert:无解码失败
            Assert.That(skipped, Is.EqualTo(0), "真载荷不该有解码失败");

            // Assert:混淆矩阵格(字典序输出)
            StringAssert.Contains("F2 M[1][1] = 1", report, "c1 判对 ⇒ M[1][1] = 1");
            StringAssert.Contains("F2 M[2][1] = 1", report, "c2 判错 ⇒ M[2][1] = 1");
            StringAssert.Contains("F2 M[无病][1] = 1", report, "c3 D=∅ ∧ J={1} ⇒ M[无病][1] = 1");
            // 矩阵恰三格:不应有 M[1][未落笔] 等(所有案例皆有判断且非共病/非不可映射)
            int matrixLineCount = report.Split('\n')
                .Count(line => line.StartsWith("F2 M["));
            Assert.That(matrixLineCount, Is.EqualTo(3),
                "矩阵恰三格(M[1][1], M[2][1], M[无病][1])");

            // Assert:三桶
            StringAssert.Contains("F2 N_comorbid = 1", report, "c5 共病 ⇒ N_comorbid = 1");
            StringAssert.Contains("F2 N_open = 1", report, "c4 无 CaseClosed ⇒ N_open = 1");
            StringAssert.Contains("F2 N_unmappable = 1", report, "c6 lexionId 9 ∉ diseaseEnum ⇒ N_unmappable = 1");
            StringAssert.Contains("F2 N_matched_comorbid = 0", report, "c5 D={1,2}≠J={1} ⇒ 不进 matched");
        }

        /// <summary>F1a 恒等式在接线面也成立:Σ M[t][t] + M[无病][未落笔] + N_matched_comorbid。</summary>
        [Test]
        public void test_wired_f2_f1aIdentity()
        {
            // 一例判对 D={1}∧J={1} ⇒ M[1][1]=1;其余均不贡献分子。
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 0);
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 1);

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.AllMetricsReport(out int _);
            StringAssert.Contains("F2 M[1][1] = 1", report);
            // M[无病][未落笔]=0(无 D=∅∧J=∅ 案例),N_matched_comorbid=0 ⇒ 分子=1
            StringAssert.Contains("F2 N_matched_comorbid = 0", report);
        }

        /// <summary>⚠️ 2026-10-19 第十六片回归:改写的**载荷类型**是 `JudgmentRevisedPayload`
        /// (与 `JudgmentRecordedPayload` 字段同名同序但类型不同),而 `Decode<T>` 带类型校验。
        /// <para><b>修前缺陷</b>:`DecodeMisdiagnosisRecord` 对两个 Kind 统一按
        /// `Decode&lt;JudgmentRecordedPayload&gt;` 请求 ⇒ **每一笔改写都抛 ⇒ 计进 skipped**,
        /// 且该案在矩阵里退化成「未落笔」。</para>
        /// <para><b>判别力</b>:把 F2 解码分支改回统一解 `JudgmentRecordedPayload` ⇒ 本条红
        /// (skipped 由 0 变 1)。</para></summary>
        [Test]
        public void test_wired_judgmentRevised_decodedAsItsOwnPayload_noSkip()
        {
            AppendCaseClosed(caseSeq: 3L, diseaseBits: 1UL << 0);            // D={1}
            AppendJudgmentRevised(caseSeq: 3L, lexiconId: 1);                // 改写后 J={1}

            string report = _reporter.MisdiagnosisReport(out int skipped);

            Assert.That(skipped, Is.EqualTo(0), "改写事件须按其自有载荷类型解出,不得计进跳过");
            StringAssert.Contains("F2 M[1][1] = 1", report);
            StringAssert.Contains("F2 N_open = 0", report);
        }

        /// <summary>空流 ⇒ 空矩阵 + 零桶 + skipped=0(不除零)。</summary>
        [Test]
        public void test_wired_emptyStream_zeroEverything()
        {
            string report = _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));
            Assert.That(report, Does.Not.Contain("F2 M["), "空流不产出矩阵格");
            StringAssert.Contains("F2 N_comorbid = 0", report);
            StringAssert.Contains("F2 N_open = 0", report);
            StringAssert.Contains("F2 N_unmappable = 0", report);
            StringAssert.Contains("F2 N_matched_comorbid = 0", report);
        }

        /// <summary>**diseaseEnum 含性检查真工作**:判断带不在注册表的 lexionId ⇒
        /// 进 N_unmappable,不入矩阵(AC-51-B7,不静默丢且不加解析器)。</summary>
        [Test]
        public void test_wired_judgedNotInRegistry_unmappableNotMatrix()
        {
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 0); // D={1}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 42);    // 42 ∉ {1,2,3}

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.MisdiagnosisReport(out int _);
            StringAssert.Contains("F2 N_unmappable = 1", report);
            Assert.That(report.Split('\n').Count(line => line.StartsWith("F2 M[")),
                Is.EqualTo(0), "不可映射不入矩阵");
        }

        // ══════════════ 证伪面(生产码守卫被移除 ⇒ 本条红) ══════════════

        /// <summary>**bit+1 映射守候**:truth bit 0 必须还原成 DiseaseId "1"。
        /// 若生产码改回裸 bit(bit 0 → "0"),"0" ∉ diseaseEnum ⇒ c1 判
        /// unmappable,矩阵 M[1][1] 消失 ⇒ 本条红。</summary>
        [Test]
        public void test_wired_truthBitPlusOne_isDiseaseId()
        {
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 0); // bit 0 → DiseaseId 1
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 1);

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.MisdiagnosisReport(out int _);
            StringAssert.Contains("F2 M[1][1] = 1", report,
                "bit 0 必须映射成 DiseaseId 1 并进 M[1][1](若裸 bit ⇒ \"0\" ∉ diseaseEnum ⇒ 红)");
            StringAssert.Contains("F2 N_unmappable = 0", report);
        }

        /// <summary>**judged 用 lexionId 守候**:c1 判 lexionId=1 ⇒ 列 "1"。
        /// 若生产码改成取错字段(如 authorPlayerId) ⇒ 列错 ⇒ 红。</summary>
        [Test]
        public void test_wired_judgedIsLexiconId_notOtherField()
        {
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 0); // D={1}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 1);    // J={1} ⇒ M[1][1]

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.MisdiagnosisReport(out int _);
            StringAssert.Contains("F2 M[1][1] = 1", report,
                "judged 必须是 lexionId(1-based DiseaseId 空间),列 " + "\"1\"");
        }
    }
}
