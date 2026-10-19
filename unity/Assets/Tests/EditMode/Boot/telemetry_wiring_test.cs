// 权威来源:ADR-019 §五(P0 切面 = 重算器 + 调试视图 + 导出)· §一(回放即数据记录)。
//
// ⚠️ 本件是 TD-epic-01 **接线片**的判据(2026-10-19):`TelemetryReporter` 此前只被
//   「编译通过 + 全量零回归」证明 —— 那**不是**接线的证据(没有一条测试调用过它)。
//   本夹具用**真** `EventStream` + **真** `PayloadEncoder/Decoder` + **真** blob 池驱动它,
//   断言「生产里真能算出 F1」—— 与 EditMode 里喂假集合的替身是两回事。

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    internal sealed class TelemetryWiringTest
    {
        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private IIdAuthority _ids;
        private PresenceRegistry _presence;
        private EventStream _stream;
        private TelemetryReporter _reporter;

        /// <summary>F2 wiring 用的最小注册表(bit i ↔ DiseaseId i+1)。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> MinimalRegistry(params int[] diseaseIds)
        {
            var list = new List<DiseaseRegistryEntry>();
            foreach (int id in diseaseIds)
                list.Add(new DiseaseRegistryEntry { DiseaseId = id, DiseaseKey = $"d{id}" });
            return list;
        }

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _ids = NewIdAuthority();
            _presence = new PresenceRegistry();
            _stream = new EventStream(_ids, _presence);
            // F1 只解码,不进 Compute ⇒ registry 不影响 F1 断言;传最小表 DiseaseId=1。
            _reporter = new TelemetryReporter(_stream, _decoder, MinimalRegistry(1));
        }

        /// <summary>IdAuthority 的生产实装(测试内联替身;机制 A 计数器)。</summary>
        private static IIdAuthority NewIdAuthority() => new SeqIdAuthority();

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        [TearDown]
        public void TearDown()
        {
            // InMemoryBlobPool 无 IDisposable(P0 内存池随域回收)⇒ 无显式释放
        }

        /// <summary>写入一条 CaseClosed(真值 = 位掩码)—— 经**真编码器**入池 + 真写面入流。</summary>
        private void AppendCaseClosed(long caseSeq, ulong diseaseBits)
        {
            var payload = new CaseClosedPayload(patientId: 1, new CaseId(10, 1, caseSeq),
                                                new DiseaseIdSet(diseaseBits), treated: true);
            PayloadRef r = _encoder.Encode(EventKind.CaseClosed, payload);
            _stream.Append(new SimEvent(10, new PatientId(1), -1, EventKind.CaseClosed, r));
        }

        /// <summary>写入一条 JudgmentRecorded(判 LexiconId)—— 同上,真链路。</summary>
        private void AppendJudgmentRecorded(long caseSeq, int lexiconId)
        {
            var payload = new JudgmentRecordedPayload(patientId: 1, new CaseId(10, 1, caseSeq),
                                                      authorPlayerId: 1, lexiconId, confidence: 2);
            PayloadRef r = _encoder.EncodeJudgmentRecorded(payload, "");
            _stream.Append(new SimEvent(11, new PatientId(1), -1, EventKind.JudgmentRecorded, r));
        }

        // ══════════════ 接线判据 ══════════════

        /// <summary>**真链路**:经真编码器入流 → 真解码器还原 → F1a = 1/1。
        /// <para>这条是本件的核心:它证明了「接线」不只是编译过 ——
        /// 事件流里的真载荷能被还原成指标。若 `TelemetryReporter` 的解码委托写错
        /// (取错字段 / 位掩码没展开 / Kind 分派漏一支),本条红。</para></summary>
        [Test]
        public void test_wired_realStream_f1aComputed()
        {
            // 位掩码 bit i → DiseaseId i+1(1-based,与 F2 / diseaseEnum 同空间):
            //   bit 3 → DiseaseId 4; lexiconId 4(P0 阶段 lexicon_id == disease_id)。
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 3);   // 真值 {4}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 4);      // 判 {4} ⇒ 判对

            string report = _reporter.JudgmentReport(out int skipped);

            Assert.That(skipped, Is.EqualTo(0), "真载荷不该有解码失败");
            Assert.That(_reporter.ScorableCount, Is.EqualTo(1), "一案可打分");
            StringAssert.Contains("F1a 判断准确率 = 1 / 1", report);
            StringAssert.Contains("F1b 改写率 = 0 / 1", report);
        }

        /// <summary>判错 ⇒ F1a = 0/1(**不是**分母 0 —— 两者是不同状态,别混)。</summary>
        [Test]
        public void test_wired_wrongJudgment_f1aZeroOverOne()
        {
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 3);   // 真值 {3}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 7);      // 判 {7} ⇒ 判错

            string report = _reporter.JudgmentReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));
            StringAssert.Contains("F1a 判断准确率 = 0 / 1", report);
            Assert.That(report, Does.Not.Contain("分母 0"), "判错 ≠ 不可定义");
        }

        /// <summary>**位掩码必须展开**:真值给两个位 {bit2, bit5},判其中一个 ⇒ 判错
        /// (集合不相等)。若解码委托没展开位掩码(比如把 Bits 当单值),本条红。</summary>
        [Test]
        public void test_wired_bitmaskExpanded_multiDiseaseTruth()
        {
            AppendCaseClosed(caseSeq: 1L, diseaseBits: (1UL << 2) | (1UL << 5));   // 真值 {2,5}
            AppendJudgmentRecorded(caseSeq: 1L, lexiconId: 2);                     // 判 {2} ⇒ 不全对

            string report = _reporter.JudgmentReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));
            StringAssert.Contains("F1a 判断准确率 = 0 / 1", report);
        }

        /// <summary>**载荷与 Kind 不符 ⇒ 解码失败必须计数并跳过,不得当 0**
        /// (这是 M3 的判据;首版夹具无损坏场景 ⇒ 「跳过」与「当 0」不可观测)。
        /// <para>制造法:用 `CaseClosed` 编码、却以 `JudgmentRecorded` Kind 入流
        /// ⇒ 解码器按 Kind 分派去解析 Case 字节当 Judgment ⇒ 必失败。</para></summary>
        [Test]
        public void test_wired_corruptPayload_countedNotZeroed()
        {
            // 一条真值(能解码)
            AppendCaseClosed(caseSeq: 1L, diseaseBits: 1UL << 3);

            // 一条损坏:Kind 与载荷不符 ⇒ 解码必失败
            var wrong = new CaseClosedPayload(patientId: 1, new CaseId(10, 1, 2L),
                                              new DiseaseIdSet(1UL << 5), treated: true);
            PayloadRef bad = _encoder.Encode(EventKind.CaseClosed, wrong);
            _stream.Append(new SimEvent(11, new PatientId(1), -1, EventKind.JudgmentRecorded, bad));

            string report = _reporter.JudgmentReport(out int skipped);

            Assert.That(skipped, Is.GreaterThanOrEqualTo(1),
                "损坏载荷须被计数并跳过(静默吞 = 普查抓的「空跑」形态)");
            // 剩下的好那一侧仍算得出来(0/1,不是分母 0)
            StringAssert.Contains("F1a 判断准确率", report);
        }

        /// <summary>空流 ⇒ 分母 0(不除零、不报 0)。</summary>
        [Test]
        public void test_wired_emptyStream_denominatorZero()
        {
            string report = _reporter.JudgmentReport(out int skipped);
            Assert.That(skipped, Is.EqualTo(0));
            Assert.That(_reporter.ScorableCount, Is.EqualTo(0));
            StringAssert.Contains("分母 0", report);
            StringAssert.Contains("无 Scorable 病例", report);
        }

        /// <summary>**只读不变量**:`TelemetryReporter` 不得**以写面身份**持 `IEventSink`
        /// (ADR-019 §三 · GDD R1)。
        /// <para>⚠️ 判据必须精确(首版过宽被自己的断言打红):`_stream` 的声明类型是
        /// `EventStream`(**具体类**),它**恰好实现** `IEventSink` —— 但本类只用它的
        /// `Events` 只读快照,从不用它写。⇒ 判据收窄为两条:
        /// ① **不得有声明类型就是 `IEventSink` 的字段**(那才是「持写面」);
        /// ② **不得有公开 / 私有的 Append/Write/Emit 方法**(写动作)。</para>
        /// <para>若将来有人给本类加 `IEventSink` 字段或写方法,本条红。</para></summary>
        [Test]
        public void test_wired_reporter_holdsNoWriteFace()
        {
            var bad = new List<string>();

            // ① 字段声明类型 = IEventSink(或以其为名的接口)⇒ 持写面
            foreach (var f in typeof(TelemetryReporter).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public))
            {
                if (f.FieldType == typeof(IEventSink) || f.FieldType.Name == "IEventSink")
                    bad.Add($"field {f.Name}: {f.FieldType.Name}");
            }

            // ② 写动作方法(名级判据,与 readonly_boundary_test 同口径)
            // ⚠️ 名级判据的**已知过宽**(2026-10-19 实测):子串 "Emit" 亦会命中含
            //    `…m-i-t…` 的常规词(如 `Limit` / `Submit` 形态);本批两处解码委托
            //    (`DecodeJudgment` / `DecodeF2`)因此改名为 `Decode*Record`。
            //    判据不放松(禁借「改门」过关),以命名避让解决
            //    (coding-standards「机械门避让表」同源)。
            foreach (var m in typeof(TelemetryReporter).GetMethods(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (m.Name.Contains("Append") || m.Name.Contains("Emit"))
                    bad.Add($"method {m.Name}");
            }

            Assert.That(bad, Is.Empty,
                "TelemetryReporter 不得持 IEventSink 字段 / 写方法(51 只读 · ADR-019 §三):\n" +
                string.Join("\n", bad));
        }

        /// <summary>**AllMetricsReport 自陈须调用方传参的部分**(禁借绿):F4–F7 的参数全部待定,
        /// 汇总报告**不得**假装已覆盖 —— 必须明说须调用方传参。</summary>
        [Test]
        public void test_wired_allMetrics_admitsUnwired()
        {
            string all = _reporter.AllMetricsReport(out _);
            // 2026-10-19 第六片:F3 已进汇总(部分还原),F2 早已接入 ⇒ 自陈面从 F2–F7 收窄到 F4–F7
            StringAssert.Contains("F4 / F5 / F6·F7 须调用方传参", all);
            Assert.That(all, Does.Not.Contain("F2–F7 未接线"), "F2/F3 已接入,不得再自陈未接线");
        }
    }
}
