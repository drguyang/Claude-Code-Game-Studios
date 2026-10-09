// M2 接线轮阶段 1 装配轮 · CompositionRoot 的 EditMode 直测。
//
// 覆盖两条:
//   ① 生产装配真接线 —— 袋中编码器 → IEventSink.Append → 真实 EventStream:Seq 已发号、
//      字节真进 InMemoryBlobPool(ADR-029 乙案:编码 + 入池一体),并可按 Kind 解码回读;
//   ② fail-loud —— 任一依赖为 null ⇒ 具名 ArgumentNullException(不返回半装配袋)。
//
// 判据纪律:确定性、隔离(每测自建装配),不依赖文件 / 网络(Coding Standards)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="CompositionRoot"/> / <see cref="CompositionRootServices"/> 单元测试。</summary>
    public class CompositionRootTest
    {
        [Test]
        public void test_assemble_wiresRealEventStream_roundTrip()
        {
            // Arrange + Act:全自动装配
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Assert:袋内各件齐备(缺一 = 半装配,fail-loud 已保证不返回这种袋)
            Assert.NotNull(bag);
            Assert.NotNull(bag.Stream, "EventStream 实装");
            Assert.NotNull(bag.EventSink, "IEventSink 抽象面");
            Assert.AreSame(bag.Stream, bag.EventSink, "EventSink 即 Stream(同一实装)");
            Assert.NotNull(bag.IdAuthority, "机制 A 发号权威");
            Assert.NotNull(bag.BlobPool, "不可变 blob 池");
            Assert.NotNull(bag.Encoder, "第七抽象点 IPayloadEncoder");
            Assert.NotNull(bag.Presence, "在场登记簿(IPresenceQuery 生产实装)");
            Assert.NotNull(bag.PatientSpawner, "9 的 DiseaseOnset 写者");

            // Act:编码 + 入池(ADR-029 乙案 —— 接口直接出 PayloadRef)
            // (测试面 T3:patientId 刻意取**非默认值** 5 —— 0 恰为 int 默认值,
            //  编码器漏写该字段时 round-trip 仍会「碰巧」相等,判据失真。)
            var payload = new DiseaseOnsetPayload(
                onsetTick: 7, diseaseId: 3, patientId: 5, patientSeed: 123456789L, seq: 0);
            PayloadRef reference = bag.Encoder.Encode(EventKind.DiseaseOnset, payload);

            // Assert:载荷非空(Length = 字节段长度)
            Assert.Greater(reference.Length, 0, "编码即入池,PayloadRef 须带非零长度");

            // Act:经抽象写面进流(Seq = -1 未发号哨兵,由主机 EventStream 发号)
            bag.EventSink.Append(new SimEvent(7, new PatientId(5), -1,
                                              EventKind.DiseaseOnset, reference));

            // Assert:事件真在流内,Seq 已发号 ≥ 0
            Assert.AreEqual(1, bag.Stream.Count, "事件进入真实 EventStream");
            SimEvent stored = bag.Stream.Events[0];
            Assert.AreEqual(EventKind.DiseaseOnset, stored.Kind);
            Assert.GreaterOrEqual(stored.Seq, 0L, "Seq 由 EventStream 发号(O-1 哨兵已消费)");
            Assert.AreEqual(reference.BlobId, stored.Payload.BlobId, "载荷引用随事件头进入流");

            // Assert:字节真进池(不是手搓假引用)
            Assert.IsTrue(bag.BlobPool.TryGetBlob(reference.BlobId, out var blob),
                "BlobId 须能在池中命中");
            Assert.Greater(blob.Length, 0, "池内字节非空");

            // Assert:按 Kind 解码回读 —— 编码 → 池 → 事件头 → 解码全链路闭合
            Assert.IsTrue(PayloadCodec.TryGetPayload(in stored, bag.BlobPool, out DiseaseOnsetPayload decoded),
                "TryGetPayload 解码成功");
            Assert.AreEqual(7, decoded.OnsetTick);
            Assert.AreEqual(3, decoded.DiseaseId);
            Assert.AreEqual(5, decoded.PatientId, "非默认 patientId 须逐位 round-trip(T3)");
            Assert.AreEqual(123456789L, decoded.PatientSeed);
            // 载荷 Seq 字段:生产写者 PatientSpawner.SpawnNext 填**占位 0**
            // (载荷内 Seq ≠ 流 header Seq —— 后者由主机 Append 时发号,O-1 哨兵 -1 消费);
            // 本断言 = 载荷 Seq 字段 round-trip 完好(与 Spawner 同款占位约定)。
            Assert.AreEqual(0, decoded.Seq, "载荷 Seq 字段须 round-trip(占位 0,流 Seq 另发号)");
        }

        [Test]
        public void test_assemble_patientSpawner_writesDiseaseOnsetToBagStream()
        {
            // 测试面 T4:代码面 #5 —— 袋内 PatientSpawner 真接线(9 的 DiseaseOnset 写者,ADR-030 §③),
            // 而非「构造出来但没人调」的摆设。
            // Arrange:全自动装配(计数器从 0 起)
            CompositionRootServices bag = CompositionRoot.Assemble();

            // Act:第一次创建病人(SpawnNext(diseaseId, tick))
            PatientId first = bag.PatientSpawner.SpawnNext(diseaseId: 3, tick: 7);

            // Assert ①:流尾事件 Kind == DiseaseOnset(写入真的落到袋内 EventStream)
            Assert.AreEqual(1, bag.Stream.Count, "首次 SpawnNext 须写入 1 条");
            SimEvent tail = bag.Stream.Events[bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.DiseaseOnset, tail.Kind, "尾事件须为 DiseaseOnset");
            Assert.AreEqual(first, tail.Patient, "事件头 patient_id = Spawner 返回值");

            // Assert ②:header Seq 已发号(≥ 0,非 -1 未发号哨兵)
            Assert.GreaterOrEqual(tail.Seq, 0L, "Seq 由 EventStream 发号(O-1 哨兵已消费)");

            // Assert ③:经生产 PayloadCodec 解码成功且 DiseaseId 匹配
            Assert.IsTrue(PayloadCodec.TryGetPayload(in tail, bag.BlobPool, out DiseaseOnsetPayload decoded),
                "载荷字节须真入池并可解码(ADR-029 乙案:编码 + 入池一体)");
            Assert.IsTrue(bag.BlobPool.TryGetBlob(tail.Payload.BlobId, out var bytes),
                "BlobId 须在池内命中(载荷非空的前提)");
            Assert.Greater(bytes.Length, 0, "解码所得字节非空");
            Assert.AreEqual(3, decoded.DiseaseId, "DiseaseId 须与 SpawnNext 入参一致");
            Assert.AreEqual(first.Value, decoded.PatientId, "载荷 patient_id 与事件头一致");

            // Assert ④:再创建一个 —— patient_id 递增恰 1(机制 A 计数器;不断绝对值,
            // 只断差值,免疫「计数器从几起」的口径漂移)
            PatientId second = bag.PatientSpawner.SpawnNext(diseaseId: 3, tick: 7);
            Assert.AreEqual(1, second.Value - first.Value, "第二次 SpawnNext 的 patient_id 须递增 1");
            Assert.AreEqual(2, bag.Stream.Count, "两次创建 = 流内两条(同 tick 不同 patient_id,不去重)");
            Assert.AreEqual(EventKind.DiseaseOnset, bag.Stream.Events[bag.Stream.Count - 1].Kind);
        }

        [Test]
        public void test_assemble_failLoud_missingDeps()
        {
            // Act + Assert:发号权威缺失 ⇒ 具名 ArgumentNullException,不吞不降级
            var exId = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(null, new PresenceRegistry(), new InMemoryBlobPool(), 0UL));
            Assert.AreEqual("idAuthority", exId.ParamName, "异常须带具名参数");

            // Act + Assert:在场登记簿缺失 ⇒ 具名
            var exPresence = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(new IdAuthority(), null, new InMemoryBlobPool(), 0UL));
            Assert.AreEqual("presence", exPresence.ParamName, "异常须带具名参数");

            // Act + Assert:blob 池缺失 ⇒ 具名
            var exPool = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(new IdAuthority(), new PresenceRegistry(), null, 0UL));
            Assert.AreEqual("blobPool", exPool.ParamName, "异常须带具名参数");

            // Act + Assert:三个引用依赖全缺 ⇒ 仍是最先校验的那一个具名(不返回半装配袋)
            var exAll = Assert.Throws<ArgumentNullException>(
                () => CompositionRoot.Assemble(null, null, null, 0UL));
            Assert.AreEqual("idAuthority", exAll.ParamName);
        }
    }
}
