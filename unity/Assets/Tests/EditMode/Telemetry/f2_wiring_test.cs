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
//   · truth = CaseClosed 位掩码展开:**bit index i → DiseaseId = i + 1**
//     (2026-10-22 裁定的位序契约,见 `Sim.Contracts/Payloads/PayloadCommon.cs`
//     的 `DiseaseIdSet` 注释块);
//   · judged = JudgmentRecorded/Revised 的 lexionId(P0 阶段按 lexicon_id == disease_id,
//     与 truth 同 DiseaseId 空间,承 ADR-030 Q3);
//   · diseaseEnum = 注册表的 DiseaseId 字符串集(TelemetryReporter 构造时注入)。
//
// 🔁 **2026-10-22 夹具 id 起点订正(1,2,3) → (2,3,4)** —— 这一改让本文件从**假绿**变成**真绿**:
//   原夹具 id 从 **1** 起,而真表 `assets/data/disease_registry.json` 从 **2** 起
//   (`id = 1` 归 `CompositionRoot.CreateFallbackRegistry`)。
//   ⇒ 在「id = 1,2,3」的夹具里,`bit = id − 1` 与 `bit = 行序` **数值相同**
//     ⇒ 本文件**分辨不出**两种读法,它证明的不是契约,是巧合。
//   改成 (2,3,4) 后,若有人把 `bit` 理解成行序,`bit 0 → DiseaseId 1` 落不进
//   `diseaseEnum = {"2","3","4"}` ⇒ **立刻转红**。
//   位序契约本身由 `disease_id_bit_pin_test.cs`(P1/P2/P3)用真表钉死。
//
// 证伪判别力(每条若生产码守卫被移除/改错 ⇒ 本条转红):
//   · 位掩码未展开(把 Bits 当单值) ⇒ truth 集合错 ⇒ 矩阵行/列错 ⇒ 红;
//   · truth 不做 bit+1 ⇒ bit 1 → "1" ∉ diseaseEnum ⇒ nUnmappable 假升 ⇒ 红;
//   · truth 多做一次 +1(或把 bit 当行序) ⇒ bit 1 → "2" 恰好仍对,**但 bit 0 → "1" 落空**
//     ⇒ 这正是本夹具 id 起点必须 ≠ 1 的理由(见上);
//   · judged 不走 lexionId ⇒ 列错 ⇒ 红;
//   · diseaseEnum 不注入(用 fallback 只有 "1") ⇒ DiseaseId 2/3/4 全判 unmappable ⇒ 红.

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
        // ── 夹具的 DiseaseId 起点 = 2(对齐真表;见文件头「夹具 id 起点订正」)──
        private const int IdA = 2;   // bit index 1
        private const int IdB = 3;   // bit index 2
        private const int IdC = 4;   // bit index 3

        /// <summary>
        /// 把 <c>DiseaseId</c> 换成位掩码 —— <b>按位序契约</b> <c>bit index = DiseaseId − 1</c>。
        /// <para>⚠️ 本 helper 内嵌了被测契约,故**不能**用它单独证明契约(那是
        /// <c>disease_id_bit_pin_test.cs</c> 的活)。它在这里的作用是让每个夹具<b>显式说出</b>
        /// 它想表示哪个病种,而不是靠一个裸的 <c>1UL &lt;&lt; 0</c> 让读的人猜。</para>
        /// </summary>
        private static ulong BitsOf(int diseaseId) => 1UL << (diseaseId - 1);

        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private IIdAuthority _ids;
        private PresenceRegistry _presence;
        private EventStream _stream;
        private TelemetryReporter _reporter;
        private long _tailTick;

        /// <summary>最小注册表(DiseaseId → DiseaseKey)。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> Registry(params int[] ids)
            => ids.Select(id => new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = id,
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
            // fallback(只有 DiseaseId=1),否则 IdA/IdB 的案例全被判 unmappable。
            // ⚠️ id 从 2 起 —— 与真表一致,使「bit = id − 1」与「bit = 行序」在此**可区分**。
            _reporter = new TelemetryReporter(_stream, _decoder, Registry(IdA, IdB, IdC));
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
            // Arrange:位掩码 bit index = DiseaseId − 1; lexionId P0 阶段 == disease_id。
            //   病例流的三条也全部入流:错序版本 ⇒ `Seq` 键不符 ⇒ 错判为 N_open(可观测)且 tick 不冲突。
            //   c1: D={IdA}(bit1) ∧ J={IdA}          ⇒ M[2][2]      (判对)
            //   c2: D={IdB}(bit2) ∧ J={IdA}          ⇒ M[3][2]      (判错)
            //   c3: D=∅(bits=0)   ∧ J={IdA}          ⇒ M[无病][2]   (无病误开方)
            //   c4: 仅判断 J={IdB}, 无 CaseClosed     ⇒ N_open++
            //   c5: D={IdA,IdB}(bits 1,2,共病) ∧ J={IdA} ⇒ N_comorbid++, 不进矩阵
            //   c6: D={IdA} ∧ J={9}(9 ∉ registry)     ⇒ N_unmappable++
            foreach (long caseSeq in new long[] { 1L, 2L, 3L, 4L, 5L, 6L })
            {
                int lexiconId = caseSeq == 4L ? IdB : caseSeq == 6L ? 9 : IdA;
                if (caseSeq != 4L)
                {
                    ulong bits = caseSeq == 1L || caseSeq == 6L ? BitsOf(IdA)
                               : caseSeq == 2L ? BitsOf(IdB)
                               : caseSeq == 3L ? 0UL
                               : BitsOf(IdA) | BitsOf(IdB);
                    AppendCaseClosed(caseSeq, bits);                 // 每条先 Close(ADR-008 流序)
                }
                AppendJudgmentRecorded(caseSeq, lexiconId);
            }

            // Act
            string report = _reporter.MisdiagnosisReport(out int skipped);

            // Assert:无解码失败
            Assert.That(skipped, Is.EqualTo(0), "真载荷不该有解码失败");

            // Assert:混淆矩阵格(字典序输出)
            StringAssert.Contains($"F2 M[{IdA}][{IdA}] = 1", report, "c1 判对 ⇒ M[2][2] = 1");
            StringAssert.Contains($"F2 M[{IdB}][{IdA}] = 1", report, "c2 判错 ⇒ M[3][2] = 1");
            StringAssert.Contains($"F2 M[无病][{IdA}] = 1", report, "c3 D=∅ ∧ J={2} ⇒ M[无病][2] = 1");
            // 矩阵恰三格:不应有 M[2][未落笔] 等(所有案例皆有判断且非共病/非不可映射)
            int matrixLineCount = report.Split('\n')
                .Count(line => line.StartsWith("F2 M["));
            Assert.That(matrixLineCount, Is.EqualTo(3),
                "矩阵恰三格(M[2][2], M[3][2], M[无病][2])");

            // Assert:三桶
            StringAssert.Contains("F2 N_comorbid = 1", report, "c5 共病 ⇒ N_comorbid = 1");
            StringAssert.Contains("F2 N_open = 1", report, "c4 无 CaseClosed ⇒ N_open = 1");
            StringAssert.Contains("F2 N_unmappable = 1", report, "c6 lexionId 9 ∉ diseaseEnum ⇒ N_unmappable = 1");
            StringAssert.Contains("F2 N_matched_comorbid = 0", report, "c5 D={2,3}≠J={2} ⇒ 不进 matched");
        }

        /// <summary>F1a 恒等式在接线面也成立:Σ M[t][t] + M[无病][未落笔] + N_matched_comorbid。</summary>
        [Test]
        public void test_wired_f2_f1aIdentity()
        {
            // 一例判对 D={IdA}∧J={IdA} ⇒ M[2][2]=1;其余均不贡献分子。
            AppendCaseClosed(caseSeq: 1L, diseaseBits: BitsOf(IdA));
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: IdA);

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.AllMetricsReport(out int _);
            StringAssert.Contains($"F2 M[{IdA}][{IdA}] = 1", report);
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
            AppendCaseClosed(caseSeq: 3L, diseaseBits: BitsOf(IdA));         // D={IdA}
            AppendJudgmentRevised(caseSeq: 3L, lexiconId: IdA);              // 改写后 J={IdA}

            string report = _reporter.MisdiagnosisReport(out int skipped);

            Assert.That(skipped, Is.EqualTo(0), "改写事件须按其自有载荷类型解出,不得计进跳过");
            StringAssert.Contains($"F2 M[{IdA}][{IdA}] = 1", report);
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
            AppendCaseClosed(caseSeq: 1L, diseaseBits: BitsOf(IdA)); // D={IdA}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 42);      // 42 ∉ {2,3,4}

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.MisdiagnosisReport(out int _);
            StringAssert.Contains("F2 N_unmappable = 1", report);
            Assert.That(report.Split('\n').Count(line => line.StartsWith("F2 M[")),
                Is.EqualTo(0), "不可映射不入矩阵");
        }

        // ══════════════ 证伪面(生产码守卫被移除 ⇒ 本条红) ══════════════

        /// <summary>
        /// **bit+1 映射守候**:truth 的 bit index <b>1</b> 必须还原成 <c>DiseaseId</c> <b>"2"</b>。
        ///
        /// <para><b>⚠️ 这里刻意用字面量 <c>1UL &lt;&lt; 1</c>,不用 <c>BitsOf</c></b> ——
        /// 本条的职责就是钉住「哪个 bit 对应哪个 id」,用 helper 会让断言变成同义反复。</para>
        ///
        /// <para><b>判别力</b>:若生产码改回裸 bit(bit 1 → "1"),"1" ∉ <c>diseaseEnum = {2,3,4}</c>
        /// ⇒ 该案判 unmappable,矩阵格消失 ⇒ 本条红。
        /// 若有人把 bit 理解成行序(bit 0 → id 2),本条的 bit 1 会落到 id 3 ⇒ 同样红。
        /// <b>⇒ 这正是夹具 id 起点必须 ≠ 1 的理由</b>:id 从 1 起时,裸 bit 与 bit+1
        /// 只在 bit 0 处分岔,而那一点在旧夹具里恰好被「id=1 存在」掩盖。</para>
        /// </summary>
        [Test]
        public void test_wired_truthBitPlusOne_isDiseaseId()
        {
            const ulong bits = 1UL << 1;                            // bit index 1 ⇒ DiseaseId 2
            AppendCaseClosed(caseSeq: 1L, diseaseBits: bits);
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 2);

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.MisdiagnosisReport(out int _);
            StringAssert.Contains("F2 M[2][2] = 1", report,
                "bit index 1 必须映射成 DiseaseId 2 并进 M[2][2]" +
                "(若裸 bit ⇒ \"1\" ∉ diseaseEnum ⇒ 红;若把 bit 当行序 ⇒ 落到 3 ⇒ 红)");
            StringAssert.Contains("F2 N_unmappable = 0", report);
        }

        /// <summary>**judged 用 lexionId 守候**:c1 判 lexionId=IdA ⇒ 列 "2"。
        /// 若生产码改成取错字段(如 authorPlayerId) ⇒ 列错 ⇒ 红。</summary>
        [Test]
        public void test_wired_judgedIsLexiconId_notOtherField()
        {
            AppendCaseClosed(caseSeq: 1L, diseaseBits: BitsOf(IdA)); // D={IdA}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: IdA);     // J={IdA} ⇒ M[2][2]

            _reporter.MisdiagnosisReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));

            string report = _reporter.MisdiagnosisReport(out int _);
            StringAssert.Contains($"F2 M[{IdA}][{IdA}] = 1", report,
                "judged 必须是 lexionId(1-based DiseaseId 空间),列 \"2\"");
        }
    }
}
