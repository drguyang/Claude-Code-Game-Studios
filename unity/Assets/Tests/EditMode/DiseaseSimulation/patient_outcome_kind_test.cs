// S18-2 / ADR-032 —— 病程终态 Kind(PatientOutcomeChanged)的验收面。
//
// 权威来源:
//   ADR-032 ①②  —— Kind 名 / 流别(病史流)/ outcome 枚举 {Recovered=1, Died=2},0 留哨兵
//   ADR-032 ④   —— 载荷 (patient_id, disease_id, outcome, seq);**t_end 不进载荷**
//   ADR-032 ⑤   —— 有界性:每病人每病种至多一条(终态 = F4 九态的吸收态)
//   ADR-032 ⑥   —— 条件 ① 消费口径 = 存在性查询,不是重放
//   GDD persistence-service.md:316  —— Folded(p) = 终态(p) ∧ ¬豁免(p) ∧ ¬未结案(p)
//   GDD disease-simulation.md:1924   —— AC-36 终态折叠
//   GDD disease-simulation.md:1120   —— F4 的 d = argmax(终态是逐病种的结论)
//
// ⚠️ 本文件的验收范围(如实登记,禁借绿):
//   覆盖 ADR-032 §Validation 里**可在 EditMode 纯逻辑面验证**的条目:
//     ① registry 登记(三字段齐 + stream: history)  ② 载荷整数域
//     ③ kindgen 产物含本 Kind 且路由到病史流        ④ 编解码往返无损
//     ⑤ outcome = 0 被解码期拒收                    ⑥ 载荷含 patient_id
//   **不在本文件**(归各自实现轮,当前 NOT-RUN):
//     · 9 的终态写者(WriterExistenceGate 已具名挂债)
//     · 7a 的 Folded(p) 本体(S18-2 的谓词实现)
//     · 真实存档往返(7a 持久化轮)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>ADR-032 病程终态 Kind 的验收夹具。</summary>
    public class PatientOutcomeKindTest
    {
        // ══ ADR-032 ① / ②:枚举与流别 ══════════════════════════════════════

        [Test]
        public void test_outcomeEnum_valuesAreStable()
        {
            // 枚举值一旦落盘就不能变(存档字节流按 int 编码,改值 = 旧档读错)。
            Assert.AreEqual(0, (int)PatientOutcome.Unspecified, "0 必须是漏填哨兵");
            Assert.AreEqual(1, (int)PatientOutcome.Recovered, "Recovered 值固定为 1");
            Assert.AreEqual(2, (int)PatientOutcome.Died, "Died 值固定为 2");
        }

        [Test]
        public void test_routing_patientOutcomeChanged_goesToHistoryStream()
        {
            // ADR-032 ①:落病史流(病人侧,可随该病人折叠)。
            // ⚠️ 这是 kindgen 产物的**消费侧**断言 —— 产物错了这里就红。
            Assert.AreEqual(StreamId.History, StreamRouting.Of(EventKind.PatientOutcomeChanged),
                "PatientOutcomeChanged 必须路由到病史流(ADR-032 ①)");
        }

        // ══ ADR-032 ④:编解码往返 ═══════════════════════════════════════════

        [Test]
        public void test_codec_roundTrip_preservesAllFields()
        {
            var original = new PatientOutcomeChangedPayload(
                patientId: 42, diseaseId: 7, outcome: (int)PatientOutcome.Died, seq: 99L);

            byte[] bytes = PayloadCodec.Encode(original);
            var back = PayloadCodec.Decode<PatientOutcomeChangedPayload>(
                EventKind.PatientOutcomeChanged, bytes);

            Assert.AreEqual(original.PatientId, back.PatientId, "patient_id 往返");
            Assert.AreEqual(original.DiseaseId, back.DiseaseId, "disease_id 往返");
            Assert.AreEqual(original.Outcome, back.Outcome, "outcome 往返");
            Assert.AreEqual(original.Seq, back.Seq, "seq 往返");
        }

        [TestCase((int)PatientOutcome.Recovered, TestName = "test_codec_bothOutcomesRoundTrip_recovered")]
        [TestCase((int)PatientOutcome.Died, TestName = "test_codec_bothOutcomesRoundTrip_died")]
        public void test_codec_bothOutcomesRoundTrip(int outcome)
        {
            // 两个合法终态各走一遍 —— 只测一个无法排除「实现把两值绑死」。
            var p = new PatientOutcomeChangedPayload(1, 1, outcome, 0L);
            var back = PayloadCodec.Decode<PatientOutcomeChangedPayload>(
                EventKind.PatientOutcomeChanged, PayloadCodec.Encode(p));
            Assert.AreEqual(outcome, back.Outcome);
        }

        // ══ ADR-032 ②:零值陷阱守卫(本件最核心)════════════════════════════

        [Test]
        public void test_codec_unspecifiedOutcome_isRejectedAtDecode()
        {
            // 漏填哨兵必须 fail-loud —— 终态折叠不可逆,放过去就是折叠行撒谎且无法回退。
            // 构造:手工写 outcome = 0(绕过 struct ctor 无从阻止,只能靠解码守卫)。
            var bad = new PatientOutcomeChangedPayload(1, 1, (int)PatientOutcome.Unspecified, 0L);
            byte[] bytes = PayloadCodec.Encode(bad);

            var ex = Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<PatientOutcomeChangedPayload>(
                          EventKind.PatientOutcomeChanged, bytes),
                "outcome = 0(Unspecified)须在解码期拒收(ADR-032 ②)");
            StringAssert.Contains("Unspecified", ex.Message,
                "错误报文须点名 Unspecified,便于排障");
        }

        [Test]
        public void test_codec_outOfRangeOutcome_isRejected()
        {
            // 边界:3 及负数同样非法(闭集只有 1 / 2)。
            foreach (int bad in new[] { 3, -1, 99 })
            {
                var p = new PatientOutcomeChangedPayload(1, 1, bad, 0L);
                byte[] bytes = PayloadCodec.Encode(p);
                Assert.Throws<InvalidDataException>(
                    () => PayloadCodec.Decode<PatientOutcomeChangedPayload>(
                              EventKind.PatientOutcomeChanged, bytes),
                    $"outcome = {bad} 不在闭集 {{1, 2}} 内,须拒收");
            }
        }

        // ══ ADR-032 ④a:t_end 不进载荷(结构断言)═══════════════════════════

        [Test]
        public void test_payload_hasNoEndTickField()
        {
            // ADR-032 ④a:t_end 就是事件头部的 Tick。入载荷 = 同一量的第二处存储。
            // 判据 = 反射扫字段名 —— 出现 EndTick / TEnd 类字段即红。
            var fieldNames = typeof(PatientOutcomeChangedPayload)
                .GetFields(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(fieldNames, "EndTick",
                "t_end 不得进载荷(ADR-032 ④a —— 它是事件头部的 Tick)");
            CollectionAssert.DoesNotContain(fieldNames, "TEnd",
                "t_end 不得进载荷(同上)");
            CollectionAssert.AreEquivalent(
                new[] { "PatientId", "DiseaseId", "Outcome", "Seq" }, fieldNames,
                "载荷恰为四字段(ADR-032 ④)");
        }

        // ══ ADR-032 ④b:disease_id 必带(终态是逐病种的结论)════════════════

        [Test]
        public void test_payload_carriesDiseaseId_becauseOutcomeIsPerDisease()
        {
            // F4 的 d = argmax(disease-simulation.md:1120)⇒ 终态是逐病种结论。
            // 不带 disease_id,折叠行「死于哪个病」不可重建(patient_seed 不是 disease_id 的函数)。
            var fieldNames = typeof(PatientOutcomeChangedPayload)
                .GetFields(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name);

            CollectionAssert.Contains(fieldNames.ToArray(), "DiseaseId",
                "载荷须带 disease_id(ADR-032 ④b)");
        }

        // ══ ADR-032 ④:patient_id 必留(折叠行 / 高水位重构)════════════════

        [Test]
        public void test_payload_carriesPatientId_forFoldedRowAndHighWatermark()
        {
            // ADR-006 Amendment B:折叠行必须保留 patient_id,否则 max(patient_id) 不可重构。
            var fieldNames = typeof(PatientOutcomeChangedPayload)
                .GetFields(System.Reflection.BindingFlags.Public
                           | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name);

            CollectionAssert.Contains(fieldNames.ToArray(), "PatientId",
                "载荷须带 patient_id(ADR-006 Amendment B / 折叠行必留)");
        }

        // ══ ADR-024 A2:载荷全整数域 ═══════════════════════════════════════

        [Test]
        public void test_payload_allFieldsAreIntegral()
        {
            // ADR-006 边界:禁 float / 引用类型。kindgen A2 已机械扫过,
            // 本条是**运行时侧**的独立复核(两处判据不同源,一处被绕另一处仍拦得住)。
            var allowed = new HashSet<Type>
            {
                typeof(int), typeof(long), typeof(short), typeof(byte),
                typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte), typeof(bool),
            };
            foreach (var f in typeof(PatientOutcomeChangedPayload)
                         .GetFields(System.Reflection.BindingFlags.Public
                                    | System.Reflection.BindingFlags.Instance))
            {
                CollectionAssert.Contains(allowed, f.FieldType,
                    $"字段 {f.Name} 的类型 {f.FieldType.Name} 不在整数域白名单(ADR-024 A2)");
            }
        }

        // ══ 分派完备性:encoder / codec 两侧都接上了 ═══════════════════════

        [Test]
        public void test_encoder_dispatchesThisKind()
        {
            // PayloadEncoder.EncodeBoxed 的 default 抛「不在 36 支闭集内」;
            // 能编码 ⇔ 在分派表内(与 drug_event_test 的探针同款口径)。
            var sink = new MemoryBlobSink();
            var encoder = new PayloadEncoder(sink);
            var p = new PatientOutcomeChangedPayload(5, 3, (int)PatientOutcome.Recovered, 1L);

            Assert.DoesNotThrow(
                () => encoder.Encode(EventKind.PatientOutcomeChanged, p),
                "PayloadEncoder 分派表须含本 Kind(ADR-029 §① 完备性)");
            Assert.Greater(sink.Stored.Count, 0, "编码须真入池");
        }

        [Test]
        public void test_codec_rejectsUnknownTag()
        {
            // 编解码契约:未知 tag 必须拒收(否则字段错位会静默通过)。
            byte[] good = PayloadCodec.Encode(
                new PatientOutcomeChangedPayload(1, 1, (int)PatientOutcome.Died, 0L));
            var corrupted = new byte[good.Length + 2];
            Array.Copy(good, corrupted, good.Length);
            corrupted[good.Length] = 9;      // 未知 tag
            corrupted[good.Length + 1] = 0;

            Assert.Throws<InvalidDataException>(
                () => PayloadCodec.Decode<PatientOutcomeChangedPayload>(
                          EventKind.PatientOutcomeChanged, corrupted),
                "未知 tag 须拒收");
        }

        // ══ 内存 blob 池桩(与 drug_event_test 同款;测试专用)══════════════

        private sealed class MemoryBlobSink : IBlobSink
        {
            public readonly List<byte[]> Stored = new List<byte[]>();

            public int Store(ReadOnlySpan<byte> blob)
            {
                Stored.Add(blob.ToArray());
                return Stored.Count;
            }
        }
    }
}
