// 2026-11-02 · `AC-4-22` 兑现轮 —— 开档容器实例(`world_containers`)的烘焙链。
//
// 判据(逐条可证伪):
//   ① Binder 五条域门各一例(空表 / 重复 id / 同格两条 / 越界 / 负 id);
//   ② 编码 → 解码往返逐字段一致,且行序 = `instance_id` 升序(规范形);
//   ③ 同源两次烘焙**逐字节**一致(ADR-014 §二);
//   ④ 导出器:声明乱序 ⇒ 产物按 id 排序;不可通行格 ⇒ 硬失败(容器是走过去交互的东西)。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>开档容器实例的 Binder / CookedWriter / Codec / 导出器。</summary>
    public sealed class world_containers_bake_test
    {
        private const string Head =
            "{\"schema_version\":1,\"level_id\":\"p0_clinic\",\"world_size\":[16,1,16],\"containers\":[";

        private static string Json(string rows) => Head + rows + "]}";

        private static string Row(long id, int x, int y, int z)
            => $"{{\"instance_id\":{id},\"cell\":[{x},{y},{z}]}}";

        private static void AssertRejected(string json, string keyword)
        {
            var ex = Assert.Throws<BakeValidationException>(() => WorldContainersBinder.Bind(json));
            bool hit = false;
            for (int i = 0; i < ex.Errors.Count; i++)
                if (ex.Errors[i].IndexOf(keyword, System.StringComparison.Ordinal) >= 0) hit = true;
            Assert.That(hit, Is.True,
                $"错误列表须点名判据「{keyword}」(否则任何 throw 都能让本夹具变绿):\n" + ex.Message);
        }

        // ══════════════ ① 合法行 ══════════════

        [Test]
        public void test_bind_validRow_carriesIdAndCell()
        {
            WorldContainersBinder.BindResult b = WorldContainersBinder.Bind(Json(Row(7, 5, 0, 5)));

            Assert.AreEqual(1, b.Rows.Count);
            Assert.AreEqual(7L, b.Rows[0].InstanceId);
            Assert.AreEqual(5, b.Rows[0].X);
            Assert.AreEqual(0, b.Rows[0].Y);
            Assert.AreEqual(5, b.Rows[0].Z);
            Assert.AreEqual(1u, b.SchemaVersion);
        }

        // ══════════════ ② 五条域门 ══════════════

        [Test]
        public void test_emptyContainers_isRejected()
            => AssertRejected(Json(""), "空表");

        [Test]
        public void test_duplicateInstanceId_isRejected()
            => AssertRejected(Json(Row(3, 1, 0, 1) + "," + Row(3, 2, 0, 2)), "重复");

        [Test]
        public void test_duplicateCell_isRejected()
            => AssertRejected(Json(Row(1, 5, 0, 5) + "," + Row(2, 5, 0, 5)), "出现两条");

        [Test]
        public void test_cellOutOfWorld_isRejected()
            => AssertRejected(Json(Row(1, 16, 0, 5)), "超出声明范围");

        [Test]
        public void test_negativeInstanceId_isRejected()
            => AssertRejected(Json(Row(-1, 5, 0, 5)), "≥ 0");

        // ══════════════ ③ 编码 / 解码往返 ══════════════

        [Test]
        public void test_cookedRoundTrip_preservesRowsSortedById()
        {
            WorldContainersBake.BakeOutput bake =
                WorldContainersBake.BakeFromSourceText(
                    Json(Row(9, 2, 0, 2) + "," + Row(4, 1, 0, 1)));

            WorldContainersDataSet data = WorldContainersCookedCodec.Read(bake.Cooked);

            Assert.AreEqual(2, data.Containers.Count);
            Assert.AreEqual(4L, data.Containers[0].InstanceId, "行序 = instance_id 升序(规范形)");
            Assert.AreEqual(9L, data.Containers[1].InstanceId);
            Assert.AreEqual(new WorldPos(1, 0, 1), data.Containers[0].Cell);
        }

        [Test]
        public void test_bake_isDeterministic_sameSourceSameBytes()
        {
            string json = Json(Row(2, 3, 0, 3) + "," + Row(1, 1, 0, 1));

            byte[] a = WorldContainersBake.BakeFromSourceText(json).Cooked;
            byte[] b = WorldContainersBake.BakeFromSourceText(json).Cooked;

            CollectionAssert.AreEqual(a, b, "同源两次烘焙须逐字节一致(ADR-014 §二)");
        }

        [Test]
        public void test_codec_rejectsTamperedDuplicateId()
        {
            WorldContainersBake.BakeOutput bake =
                WorldContainersBake.BakeFromSourceText(Json(Row(1, 1, 0, 1) + "," + Row(2, 2, 0, 2)));

            // 把第二行的 instance_id(i64)改成与第一行相同:行从 20 字节头 + 16 字节尾段起。
            byte[] bytes = (byte[])bake.Cooked.Clone();
            int secondRowIdOffset = 20 + 16 + 20;   // 头 + (W,H,D,count) + 第一行
            for (int i = 0; i < 8; i++) bytes[secondRowIdOffset + i] = (byte)(i == 0 ? 1 : 0);

            var ex = Assert.Throws<InvalidDataException>(() => WorldContainersCookedCodec.Read(bytes));
            StringAssert.Contains("重复", ex.Message);
        }

        // ══════════════ ④ 导出器 ══════════════

        [Test]
        public void test_exporter_sortsById()
        {
            LevelSource src = MinimalLevel(
                new ContainerSource(new[]
                {
                    new ContainerSpec(9, new WorldPos(2, 0, 2)),
                    new ContainerSpec(4, new WorldPos(1, 0, 1)),
                }));

            string json = LevelExporter.ExportContainers(src);

            int first = json.IndexOf("\"instance_id\": 4", System.StringComparison.Ordinal);
            int second = json.IndexOf("\"instance_id\": 9", System.StringComparison.Ordinal);
            Assert.That(first >= 0 && second > first, Is.True,
                "产物须按 instance_id 升序:\n" + json);
        }

        [Test]
        public void test_exporter_rejectsUnwalkableCell()
        {
            LevelSource src = MinimalLevel(
                new ContainerSource(new[] { new ContainerSpec(1, new WorldPos(0, 0, 0)) }),
                unwalkableCell: new WorldPos(0, 0, 0));

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => LevelExporter.ExportContainers(src));
            StringAssert.Contains("不可通行", ex.Message,
                "容器是走过去交互的东西;不可通行格上它永远选不中");
        }

        // ══════════════ 夹具 ══════════════

        /// <summary>16×1×16、全可走(可指定一格不可走)的最小关卡源。</summary>
        private static LevelSource MinimalLevel(ContainerSource containers,
                                                WorldPos? unwalkableCell = null)
        {
            const int w = 16, h = 1, d = 16;
            var walkable = new int[w * h * d];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = 1;
            if (unwalkableCell.HasValue)
            {
                WorldPos c = unwalkableCell.Value;
                walkable[(c.X * h + c.Y) * d + c.Z] = 0;
            }

            return new LevelSource(
                "test_containers", w, h, d,
                new List<SlotSpec>
                {
                    new SlotSpec(1, 0, 1, DaYiJingCheng.Sim.World.SlotType.Decor, bakedInitial: false),
                },
                walkable: walkable,
                terrain: null,
                latticeSizeMm: 1000,
                chunkSizeX: 8, chunkSizeY: 1, chunkSizeZ: 8,
                containers: containers);
        }
    }
}
