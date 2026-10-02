// Story 003 测试: Structure* 世界流事件 + 实例表
//
// AC-23-02: 重放重建 slot_occupied / structure_at / Overlay 与事件序列逐位一致
// AC-23-09: 字节流稳定(encode → decode → re-encode 相等)
// AC-23-10: 载荷全整数,无 float
// AC-23-11: StructureModified 只载物理形态位

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class StructureKindsTest
    {
        private StructureInstanceRegistry _registry;
        private StructureWriter _writer;
        private List<SimEvent> _appendedEvents;
        private InMemoryBlobPool _pool;

        [SetUp]
        public void Setup()
        {
            _registry = new StructureInstanceRegistry();
            _appendedEvents = new List<SimEvent>();

            // ADR-029 §③:载荷编码经 IPayloadEncoder —— 测试用内存池
            _pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(_pool);

            // Mock IEventSink
            var mockSink = new MockEventSink(evt => _appendedEvents.Add(evt));
            _writer = new StructureWriter(mockSink, _registry, encoder);
        }

        /// <summary>经池 + codec 取回强类型载荷(ADR-029 的合法读路径)。</summary>
        private T PayloadOf<T>(SimEvent evt) where T : struct
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _pool, out T p),
                $"{evt.Kind}: 载荷须能经池 + codec 取回(字节真的进了池)");
            return p;
        }

        // AC-23-10: Place 事件载荷全整数
        [Test]
        public void test_place_emitsStructurePlaced()
        {
            int sid = _writer.Place(new WorldPos(0, 0, 0), moduleId: 1, orientation: 0, variant: 0, tick: 100);

            Assert.AreEqual(1, _appendedEvents.Count);
            Assert.AreEqual(EventKind.StructurePlaced, _appendedEvents[0].Kind);
            Assert.AreEqual(PatientId.None, _appendedEvents[0].Patient);

            // ⚠️ 2026-10-02(ADR-029 接线支):原断言为 `Payload.BlobId == sid` ——
            //    那是把**业务 id 耦合到 blob 寻址**(正是 B4 缺陷的形态:poiId 当 blobId)。
            //    codec 路径下 BlobId 是池索引,与 structure_id 无关 ⇒ 改为解码验字段。
            var p = PayloadOf<StructurePlacedPayload>(_appendedEvents[0]);
            Assert.AreEqual(sid, p.StructureId, "载荷须载真实 structure_id");
            Assert.AreEqual(new WorldPos(0, 0, 0), p.Cell, "载荷须载锚点格");
            Assert.AreEqual(1, p.ModuleId);
        }

        // AC-23-10: Remove 事件
        [Test]
        public void test_remove_emitsStructureRemoved()
        {
            int sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _appendedEvents.Clear();

            bool removed = _writer.Remove(sid, 101);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, _appendedEvents.Count);
            Assert.AreEqual(EventKind.StructureRemoved, _appendedEvents[0].Kind);
        }

        // AC-23-10: Remove 不存在的 id 返回 false
        [Test]
        public void test_remove_nonexistent_returnsFalse()
        {
            bool removed = _writer.Remove(999, 100);
            Assert.IsFalse(removed);
            Assert.AreEqual(0, _appendedEvents.Count);
        }

        // AC-23-11: Modify 只载物理形态位
        [Test]
        public void test_modify_orientation_emitsModified()
        {
            int sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _appendedEvents.Clear();

            bool modified = _writer.Modify(sid, newOrientation: 90, newVariant: null, tick: 200);
            Assert.IsTrue(modified);
            Assert.AreEqual(1, _appendedEvents.Count);
            Assert.AreEqual(EventKind.StructureModified, _appendedEvents[0].Kind);

            // StructureModified 载荷: 真实 payload 类型(ADR-024: 非 bit-packing)
            // 验证事件已写入(载荷编码由 Sim.Codec 负责,此处只验证事件存在)
            Assert.AreEqual(1, _appendedEvents.Count);
        }

        // AC-23-11: 无变更返回 false
        [Test]
        public void test_modify_noChange_returnsFalse()
        {
            int sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _appendedEvents.Clear();

            bool modified = _writer.Modify(sid, newOrientation: 0, newVariant: 0, tick: 200);
            Assert.IsFalse(modified);
            Assert.AreEqual(0, _appendedEvents.Count);
        }

        // AC-23-02: 重放重建实例表
        [Test]
        public void test_rebuildFromEvents_equalsLiveState()
        {
            // 构造事件序列
            _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);   // id=1
            int sid2 = _writer.Place(new WorldPos(1, 0, 0), 2, 0, 0, 101); // id=2
            _writer.Remove(sid2, 102);
            _writer.Modify(1, newOrientation: 90, newVariant: null, 200);

            // 重建 —— ⚠️ 2026-10-02(ADR-029 接线支):原实现读 `Payload.BlobId` 当 structure_id,
            // 且**硬编码** moduleId / anchor / 朝向(注释自陈「测试用…回填」)⇒ 那不是真重建,
            // 是照着预期答案写的。现改为**真解码**:全部字段取自载荷。
            var rebuild = new StructureInstanceRegistry();
            foreach (var evt in _appendedEvents)
            {
                if (evt.Kind == EventKind.StructurePlaced)
                {
                    var p = PayloadOf<StructurePlacedPayload>(evt);
                    rebuild.Register(p.Cell, p.ModuleId, p.Orientation, p.Variant);
                }
                else if (evt.Kind == EventKind.StructureRemoved)
                {
                    var p = PayloadOf<StructureRemovedPayload>(evt);
                    // ⚠️ 收窄转换:`StructureRemovedPayload.StructureId` = **long**
                    // (entities.yaml:2068 定 structure_id = i64),而 `StructureInstanceRegistry`
                    // 的 id 仍是 **int** —— 二者不一致。此前被 StructureKinds.cs 内的
                    // int 版副本**掩盖**;删副本后暴露。**本 story 只做收窄转换并登记该缺陷**,
                    // 注册表 id 升 long 归独立轮(见 §已知缺陷)。
                    rebuild.Remove((int)p.StructureId);
                }
                else if (evt.Kind == EventKind.StructureModified)
                {
                    var p = PayloadOf<StructureModifiedPayload>(evt);
                    // ModifiedFields 位掩码决定改哪一维(1=朝向, 2=变体)
                    int? newOri = (p.ModifiedFields & 1) != 0 ? p.NewOrientation : (int?)null;
                    int? newVar = (p.ModifiedFields & 2) != 0 ? p.NewVariant : (int?)null;
                    rebuild.Update((int)p.StructureId, newOri, newVar);   // 同上:收窄转换
                }
            }

            // 验证: id=1 存在且朝向=90
            Assert.IsTrue(rebuild.TryGet(1, out var inst1));
            Assert.AreEqual(1, inst1.ModuleId);
            Assert.AreEqual(90, inst1.Orientation);
            Assert.AreEqual(1, rebuild.Count); // id=2 已移除,只有 id=1 存活
        }

        // 边界: 实例表查询不存在的 id
        [Test]
        public void test_tryGet_nonexistent_returnsFalse()
        {
            Assert.IsFalse(_registry.TryGet(999, out _));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // §已知缺陷(本 story 暴露并登记,**未修** —— 归独立轮)
    //
    // `StructureInstanceRegistry` 的 structure id 是 **int**
    // (`_nextStructureId` / `Register` / `Remove` / `Update` / `TryGet` /
    //  `StructureInstance.StructureId` / `StructureWriter.PeekNextId`),
    // 而权威件 `design/registry/entities.yaml:2068` 定
    // **`structure_id: i64`**(「与 ItemInstanceId 同模式,计数器 + 高水位可重构」),
    // 契约版载荷 `StructurePlacedPayload.StructureId` 亦为 **long**。
    //
    // ⇒ **注册表侧不合规**(int 承载 i64 语义):id 超 2^31 时静默回绕。
    //   该不一致此前被 `StructureKinds.cs` 内的 **int 版 payload 副本掩盖**
    //   (副本已由 ADR-029 接线支删除,故暴露)。
    //   本测试的 `(int)` 收窄转换是**临时桥接**,不是修复。
    //   修法 = 注册表 id 全链升 `long`(须同步 `IIdAuthority` 机制 A 的高水位口径);
    //   归独立轮,**不属 B4 的范围**。
    // ══════════════════════════════════════════════════════════════

    // Mock IEventSink
    internal class MockEventSink : IEventSink
    {
        private readonly Action<SimEvent> _append;

        public MockEventSink(Action<SimEvent> append)
        {
            _append = append;
        }

        public void Append(in SimEvent evt) => _append(evt);
        public long TickCount => 0;
    }
}
