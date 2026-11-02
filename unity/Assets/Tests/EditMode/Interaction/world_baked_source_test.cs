// 2026-11-02 · `TD-interaction-03` —— 第二源生产实装(`WorldBakedLogicalSource`)
//
// 判据(逐条可证伪):
//   ① 三张烘焙表 → 三个切片,逐行字段原样(POI id / 格、资源索引 = 行序、槽位格);
//   ② 槽位线性键 = `x + W·(y + H·z)`,**用夹具自己的字面量算式核对**
//      (不调用生产实现 —— 否则变异两边一起动);
//   ③ W/H 非正 ⇒ **抛**(线性键不可比,4-DC-4 的立论失效)。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    /// <summary>第二源的三切片投影。</summary>
    public sealed class world_baked_source_test
    {
        private const int W = 16;
        private const int H = 1;

        private static WorldBakedLogicalSource Build()
            => new WorldBakedLogicalSource(
                new[]
                {
                    new WorldPoiRow(poiId: 3, cell: new WorldPos(4, 0, 4), type: 1, guardRef: 2),
                    new WorldPoiRow(poiId: 5, cell: new WorldPos(11, 0, 7), type: 2, guardRef: 1),
                },
                new[]
                {
                    new WorldResourceRow(resourceId: 10, cell: new WorldPos(1, 0, 2), itemKey: 7, ecozoneId: 0),
                    new WorldResourceRow(resourceId: 12, cell: new WorldPos(3, 0, 9), itemKey: 8, ecozoneId: 1),
                },
                new[]
                {
                    new WorldBuildSlotRow(new WorldPos(1, 0, 1), allowedMask: 1, bakedInitial: true, moduleId: 8),
                    // ⚠️ 第二格刻意 `y = 2`(非 0):若夹具全在 y = 0 上,线性键的**轴次序**
                    //    写错也观测不到(`x + W·y + W·H·z` 与 `x + H·y + W·z` 在 y = 0 时同值)
                    //    —— 2026-11-02 证伪 M1 首轮假存活即此因。
                    new WorldBuildSlotRow(new WorldPos(2, 2, 3), allowedMask: 4, bakedInitial: false, moduleId: -1),
                },
                W, H);

        /// <summary>夹具自己的线性键算式(字面量 W/H;不复用生产实现)。</summary>
        private static long Key(WorldPos c, int w, int h) => (long)c.X + (long)w * ((long)c.Y + (long)h * c.Z);

        [Test]
        public void test_poiSlice_carriesIdAndCell()
        {
            IReadOnlyList<PoiCellDto> pois = Build().PoiCells;

            Assert.AreEqual(2, pois.Count);
            Assert.AreEqual(3L, pois[0].PoiId);
            Assert.AreEqual(new WorldPos(4, 0, 4), pois[0].Cell);
            Assert.AreEqual(5L, pois[1].PoiId, "POI id 原样(定义侧;状态归 6 的世界流)");
        }

        [Test]
        public void test_forageSlice_indexIsRowOrder()
        {
            IReadOnlyList<ForageSpotDto> spots = Build().ForageSpots;

            Assert.AreEqual(2, spots.Count);
            Assert.AreEqual(0L, spots[0].BakedResourceIndex, "行序即 BakedResourceIndex(与 17 采集同源)");
            Assert.AreEqual(1L, spots[1].BakedResourceIndex);
            Assert.AreEqual(new WorldPos(3, 0, 9), spots[1].Cell);
        }

        [Test]
        public void test_slotSlice_linearKeyMatchesLiteralFormula()
        {
            IReadOnlyList<BuildSlotDto> slots = Build().BuildSlots;

            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(Key(new WorldPos(1, 0, 1), W, H), slots[0].SlotLinearKey);
            Assert.AreEqual(Key(new WorldPos(2, 2, 3), W, H), slots[1].SlotLinearKey);
            Assert.AreNotEqual(slots[0].SlotLinearKey, slots[1].SlotLinearKey,
                "第三键必须两两不同(同图内线性化唯一)");
        }

        [Test]
        public void test_nonPositiveWorldExtent_throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new WorldBakedLogicalSource(
                    new List<WorldPoiRow>(), new List<WorldResourceRow>(),
                    new List<WorldBuildSlotRow>(), worldW: 0, worldH: 1),
                "W = 0 ⇒ 线性键不可比(所有 x 相同的格撞键)");
        }
    }
}
