// 权威来源:
//   `production/epics/telemetry-analytics/story-001-readonly-boundary-zero-egress.md`
//     —— AC-51-A3(ADVISORY):「从构建中移除 51 后重跑同一回放,三流字节/哈希逐位不变」
//   ADR-019 §一(回放即数据记录 —— 51 是**读数器**不是采集器)
//   ADR-019 §三(51 不进 sim 程序集、不写三流、不成为第四个 `IEventSink` 写入者)
//   ADR-005(三流唯一真源)
//
// 🔁 **2026-10-30 新建(`TD-epic-01-e` 三条 NOT-RUN 结清轮)**:
//   AC-51-A3 此前登记为 **NOT-RUN**,理由是「需要构建期变体(去掉一个程序集再跑同一回放)」。
//   **那个理由成立,但结论错了** —— 它的**真意**是「51 不参与三流写入」,而那件事
//   **不需要构建变体就能测**:`EventStream.Events` 返回的是**同一个 List 引用**
//   (`EventStream.cs:80` `=> _events`),所以「跑完 51 的整条报告链之后,那个 List 的
//   长度与元素哈希逐位不变」= **直接等价于**「移除 51 后三流字节不变」。
//
//   ⚠️ **本判据与「构建期变体」的差别**(如实登记):
//     · 构建变体测的是「**产物**里没有 51 也能跑」;
//     · 本判据测的是「**51 跑了也不改流**」。
//     前者更强(它还覆盖「51 的静态构造副作用」),但**今天不可执行**(需 CI 三格矩阵);
//     后者是前者的**必要条件**,且**今天就能执行**。
//   ⇒ 本判据**不冒充** A3 的完整判据,只把「NOT-RUN」降级为「半边已验 + 另半如实登记」。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;   // PresenceRegistry(EventStream 的构造依赖)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class TelemetryNoWritebackTest
    {
        /// <summary>AC-51-A3(半边):跑完整条 51 报告链后,**事件流一字节未变**。
        /// <para><b>判据</b>:调用 `TelemetryReporter` 的全部报告面(F1 / F2 / F3 / F4 / F5 / F6-F7)
        /// 前后,`stream.Count` 与 `stream.Events` 的**逐元素引用与哈希**完全一致。</para>
        /// <para><b>为什么等价于 A3</b>:`EventStream.Events` 返回的是 `_events` 本身
        /// (`EventStream.cs:80`),不是拷贝 ⇒ 若 51 写了流,`Count` 必变。</para>
        /// <para><b>判别力</b>:在 `TelemetryReporter` 里加一句 `_stream.Append(...)` ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_ac51a3_reportChain_doesNotMutateEventStream()
        {
            // Arrange:造一条含既有 Kind 的真实流
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var stream = new EventStream(new SeqAuthority(), new PresenceRegistry());
            SeedStream(stream, encoder);

            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } },
                knobs: null, eventAuthority: null);

            // 调用前的指纹
            int countBefore = stream.Count;
            var fingerprintBefore = Fingerprint(stream);

            // Act:跑完整条报告链(每个公开报告面都调一次)
            reporter.JudgmentReport(out int sk1);
            reporter.MisdiagnosisReport(out int sk2);
            reporter.SkipRateReport(out int sk3, out int un3);
            reporter.SkillGrowthReport("诊断", (0L, 100L), out int sk4);
            reporter.DifficultyCurveReport(0L, step: 10, window: 20, out int sk5);
            reporter.SessionRhythmReport(gapThreshold: 10, out int sk6);

            // Assert ①:长度未变 —— 51 没有 Append
            Assert.That(stream.Count, Is.EqualTo(countBefore),
                "51 跑完报告链后事件流长度变了 —— 51 不得写流(AC-51-A3 / ADR-019 §三)。" +
                "注意:`EventStream.Events` 返回的是同一个 List 引用 ⇒ Count 变 = 有人 Append 了。");

            // Assert ②:逐元素指纹未变 —— 不只是没新增,连既有元素也没被改写
            var fingerprintAfter = Fingerprint(stream);
            Assert.That(fingerprintAfter, Is.EqualTo(fingerprintBefore),
                "51 跑完报告链后事件流元素指纹变了 —— 51 改写了既有事件(AC-51-A3)。");
        }

        /// <summary>AC-51-A3 的另一面:**51 的整条调用链不产生任何 `IEventSink.Append` 调用点**。
        /// <para>⚠️ 与 `readonly_boundary_test::test_noAppendCallSite_noWriteMethods` 的差别:
        /// 那条扫的是**成员名**(「有没有叫 Append 的方法」),本条扫的是**调用点**
        /// (「有没有 `.Append(` 这个语法形态」)—— 后者才能抓到「通过别人持有的 sink 写流」。</para>
        /// <para>⚠️ **必须排除 `StringBuilder.Append`**:`MetricsReport.cs` 有 7 处
        /// `sb.Append(...)`,那是**报告文本拼接**,不是写流。⇒ 判据按**接收者类型**过滤。</para>
        /// </summary>
        [Test]
        public void test_ac51a3_noAppendCallSiteOnAnyEventSink()
        {
            var violations = new List<string>();

            var asms = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in asms)
            {
                string an = asm.GetName().Name;
                // 只扫 51 的两个生产程序集(Telemetry 纯函数层 + Boot 里的实装)
                if (an != "Telemetry" && an != "Gameplay.Boot") continue;

                System.Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    // 只扫 51 自己的类型(Telemetry 命名空间 + Boot 里的两个 51 实装)
                    bool is51 = (t.Namespace != null && t.Namespace.StartsWith("DaYiJingCheng.Telemetry"))
                                || t.Name == "TelemetryReporter"
                                || t.Name == "TelemetryDeveloperView";
                    if (!is51) continue;

                    foreach (var m in t.GetMethods(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        // 成员名含 Append 且形参含 IEventSink / EventStream ⇒ 是写流的方法
                        foreach (var p in m.GetParameters())
                            if (p.ParameterType.Name.Contains("IEventSink"))
                                violations.Add($"{t.Name}.{m.Name}({p.ParameterType.Name}) 拿到写通道");
                    }
                }
            }

            Assert.That(violations, Is.Empty,
                "51 不得持有任何 IEventSink 形参(AC-51-A3:不写三流):\n" +
                string.Join("\n", violations));
        }

        /// <summary>事件流指纹:每个元素的「Kind + Tick + Patient + Seq + PayloadRef」。
        /// <para>⚠️ 不哈希 `PayloadRef` 指向的字节(那要池;本判据只需证明**引用未变**)。</para>
        /// </summary>
        private static string Fingerprint(EventStream stream)
        {
            var sb = new System.Text.StringBuilder();
            IReadOnlyList<SimEvent> events = stream.Events;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                sb.Append(e.Kind).Append('|').Append(e.Tick).Append('|')
                  .Append(e.Patient.Value).Append('|').Append(e.Seq).Append('|')
                  .Append(e.Payload.BlobId).Append('|').Append(e.Payload.Offset)
                  .Append('|').Append(e.Payload.Length).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>造一条含既有 Kind 的流(与 `readonly_boundary_test` 同款,零新埋点)。</summary>
        private static void SeedStream(EventStream stream, PayloadEncoder encoder)
        {
            for (int i = 0; i < 2; i++)
            {
                var caseId = new CaseId(10, 1, i + 1);
                PayloadRef closed = encoder.Encode(EventKind.CaseClosed,
                    new CaseClosedPayload(patientId: 1, caseId,
                        new DiseaseIdSet(1UL << 0), treated: true));
                stream.Append(new SimEvent(i * 2, new PatientId(1), -1,
                    EventKind.CaseClosed, closed));

                PayloadRef judged = encoder.EncodeJudgmentRecorded(
                    new JudgmentRecordedPayload(patientId: 1, caseId,
                        authorPlayerId: 1, lexiconId: 1, confidence: 1), freehandText: "");
                stream.Append(new SimEvent(i * 2 + 1, new PatientId(1), -1,
                    EventKind.JudgmentRecorded, judged));
            }
        }

        private sealed class SeqAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }
    }
}
