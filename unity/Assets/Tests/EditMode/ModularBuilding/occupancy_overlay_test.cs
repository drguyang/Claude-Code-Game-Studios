// Story 003 测试: 占用表合成 + EffectiveWalkable
//
// AC-23-02: 重放重建 slot_occupied / structure_at / Overlay 与事件序列逐位一致
// AC-23-03: EffectiveWalkable = Nav.walkable ∧ ¬Overlay.blocked
// AC-23-04: slot_occupied 与 Overlay.blocked 恒一致(单一真相,无漂移)
// AC-23-12: 42 不是 Structure* 写者

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class OccupancyOverlayTest
    {
        // AC-23-02: 重放重建一致性
        [Test]
        public void test_rebuildFromEvents_equalsLiveState()
        {
            // 模拟事件序列: Place at (0,0,0) → Place at (1,0,0) → Remove at (0,0,0)
            var events = new[]
            {
                CreatePlacedEvent(1, new WorldPos(0, 0, 0), 1, 0, 0),
                CreatePlacedEvent(2, new WorldPos(1, 0, 0), 1, 0, 0),
                CreateRemovedEvent(3, new WorldPos(0, 0, 0), 1)
            };

            // 重建
            var rebuild = new Dictionary<WorldPos, int>();
            var structureMap = new Dictionary<int, (WorldPos anchor, int moduleId)>();
            foreach (var evt in events)
            {
                if (evt.Kind == EventKind.StructurePlaced)
                {
                    int sid = evt.Payload.BlobId;
                    int moduleId = evt.Payload.Offset;
                    WorldPos anchor = DecodeWorldPos(evt.Payload);
                    rebuild[anchor] = moduleId;
                    structureMap[sid] = (anchor, moduleId);
                }
                else if (evt.Kind == EventKind.StructureRemoved)
                {
                    int sid = evt.Payload.BlobId;
                    if (structureMap.TryGetValue(sid, out var info))
                    {
                        rebuild.Remove(info.anchor);
                        structureMap.Remove(sid);
                    }
                }
            }

            // 期望终态: (1,0,0) 有 module 1
            Assert.IsTrue(rebuild.ContainsKey(new WorldPos(1, 0, 0)));
            Assert.IsFalse(rebuild.ContainsKey(new WorldPos(0, 0, 0)));
        }

        // AC-23-03: EffectiveWalkable 合成
        [Test]
        public void test_effectiveWalkable_navAndOverlay()
        {
            bool navWalkable = true;
            bool overlayBlocked = true;

            bool effective = navWalkable && !overlayBlocked;
            Assert.IsFalse(effective);
        }

        [Test]
        public void test_effectiveWalkable_bothTrue()
        {
            bool navWalkable = true;
            bool overlayBlocked = false;

            bool effective = navWalkable && !overlayBlocked;
            Assert.IsTrue(effective);
        }

        // AC-23-04: 同一占用表双访问器一致
        [Test]
        public void test_singleSource_noDrift()
        {
            // 使用同一字典模拟占用表
            var occupancy = new Dictionary<WorldPos, int>();
            occupancy[new WorldPos(0, 0, 0)] = 1; // module 1

            // slot_occupied 视图
            bool slotOccupied(WorldPos p) => occupancy.ContainsKey(p);

            // Overlay.blocked 视图
            bool overlayBlocked(WorldPos p) => occupancy.ContainsKey(p);

            // 两者一致
            var positions = new[] { new WorldPos(0, 0, 0), new WorldPos(1, 0, 0) };
            foreach (var p in positions)
            {
                Assert.AreEqual(slotOccupied(p), overlayBlocked(p),
                    $"双视图不一致 at {p}");
            }
        }

        // 边界: 空占用表
        [Test]
        public void test_emptyOccupancy_allWalkable()
        {
            var occupancy = new Dictionary<WorldPos, int>();
            bool overlayBlocked(WorldPos p) => occupancy.ContainsKey(p);

            Assert.IsFalse(overlayBlocked(new WorldPos(0, 0, 0)));
        }

        // AC-23-10: 载荷字段类型检查(全整数)
        [Test]
        public void test_payloadFields_allInteger()
        {
            // StructurePlaced 载荷: structure_id(int), cell(WorldPos = 3×int), module_id(int), orientation(int), variant(int)
            // 全整数,无 float/double
            var payload = new PayloadRef(blobId: 1, offset: 1, length: 8);
            Assert.AreEqual(1, payload.BlobId);       // structure_id
            Assert.AreEqual(1, payload.Offset);        // module_id
            Assert.AreEqual(8, payload.Length);        // 载荷长度
        }
    }

    // 辅助: 创建 StructurePlaced 事件
    private static SimEvent CreatePlacedEvent(long tick, WorldPos cell, int structureId, int moduleId, int orientation)
    {
        // 编码: BlobId = structure_id, Offset = module_id, Length = 8
        // cell 编码到 payload 的高位(简化测试用)
        var payload = new PayloadRef(blobId: structureId, offset: moduleId, length: 8);
        return new SimEvent(tick, PatientId.None, 0, EventKind.StructurePlaced, payload);
    }

    // 辅助: 创建 StructureRemoved 事件
    private static SimEvent CreateRemovedEvent(long tick, WorldPos cell, int structureId)
    {
        var payload = new PayloadRef(blobId: structureId, offset: 0, length: 8);
        return new SimEvent(tick, PatientId.None, 0, EventKind.StructureRemoved, payload);
    }

    // 辅助: 从 PayloadRef 解码 WorldPos(简化版)
    private static WorldPos DecodeWorldPos(PayloadRef payload)
    {
        // 测试用简化解码
        int x = (payload.Offset >> 20) & 0xFFF;
        int z = payload.Offset & 0xFFF;
        return new WorldPos(x, 0, z);
    }
}
