// 拾取面板的**装配层出口**(`DriverLootActionSink` · 2026-11-14) —— 判据夹具
//
// 权威:`docs/architecture/adr-013-*.md` §十-H Amendment G(闭集第十一员 `Loot`);
//       ADR-009 §七(拾取 = 意图事件 + 主机当下判距 + 宽容半径);
//       ADR-013 §9 C3(42 只渲染,判定不在这里)。
//
// 本文件证明三件事:
//   ① 逐件 ⇒ 恰一条 `DropClaimed`,列表随之收缩(未拿完不合屏);
//   ② 批量 ⇒ 每件各一条 `DropClaimed`(逐个走**同一个** `TryClaimInstance`),
//      拿完即合屏(合屏闭包恰一次);
//   ③ 批量再调一次 ⇒ **零新增**(第二发是空列表;幂等仍由 20 的归属面守)。
//
// ⚠️ 零写盘:所有事件进内存 `EventStream`(`CompositionRoot.Assemble()`)。
// ⚠️ 本文件**不测**屏的行绘制 / 行文字 —— 那在 `SkeuomorphicUI/loot_screen_test.cs`
//   (该目录的 asmdef 看不见 `Gameplay.Boot`,两条边界刻意分开)。

using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    public sealed class driver_loot_action_sink_test
    {
        private const int PlayerId = 7;
        private const long Tick = 100;
        private static readonly WorldPos PlayerCell = new WorldPos(2, 0, 2);

        private CompositionRootServices _bag;
        private DropEventWriter _drops;
        private LootScreen _screen;
        private int _closeCount;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _drops = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
            _screen = new LootScreen(new VisualElement(), new SkeuoElementLibrary());
            _closeCount = 0;
        }

        private long SeedDrop(WorldPos cell, int itemKey, int qty)
            => _drops.RecordSpawned(Tick - 10, cell, itemKey, qty).Value;

        private InventoryProjection Projection()
            => InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder, InventoryFoldContext.None).Projection;

        /// <summary>此刻地面行(夹具侧的最小构造 —— 与生产 `BootRoot.BuildLootRows` 同源判据)。</summary>
        private List<LootRow> GroundRows()
        {
            var rows = new List<LootRow>();
            InventoryProjection proj = Projection();
            IReadOnlyList<long> ground = proj.InstancesOf(InventoryProjection.NoOwner);
            for (int i = 0; i < ground.Count; i++)
            {
                proj.TryGet(ground[i], out InventoryRow row);
                rows.Add(new LootRow(ground[i], "物#" + row.ItemOrdinal + "×" + row.Qty));
            }
            return rows;
        }

        private DriverLootActionSink MakeSink()
            => new DriverLootActionSink(
                _bag.DropClaimOrchestrator, PlayerId,
                () => PlayerCell, () => Tick, GroundRows,
                () => _screen, () => _closeCount++);

        private int ClaimedCount() => _bag.Stream.Events.Count(e => e.Kind == EventKind.DropClaimed);

        [Test]
        public void test_claim_writesOneDropClaimed_andShrinksList()
        {
            long first = SeedDrop(PlayerCell, itemKey: 4, qty: 1);
            SeedDrop(PlayerCell, itemKey: 4, qty: 2);

            List<LootRow> rows = GroundRows();
            Assert.AreEqual(2, rows.Count, "[前置] 地上两件");
            _screen.Present(rows);

            MakeSink().Claim(first);

            Assert.AreEqual(1, ClaimedCount(), "拿一件 ⇒ 恰一条 DropClaimed");
            Assert.AreEqual(1, _screen.RowCount, "列表随之收缩");
            Assert.AreEqual(0, _closeCount, "还没拿完 ⇒ 不合屏");
        }

        [Test]
        public void test_claimAll_claimsEveryGroundRow_thenCloses()
        {
            SeedDrop(PlayerCell, itemKey: 4, qty: 1);
            SeedDrop(PlayerCell, itemKey: 1, qty: 3);

            _screen.Present(GroundRows());
            MakeSink().ClaimAll();

            Assert.AreEqual(2, ClaimedCount(), "两件都进流(逐件走同一个 TryClaimInstance)");
            Assert.AreEqual(0, GroundRows().Count, "地上已无可拾");
            Assert.AreEqual(1, _closeCount, "拿完即合屏(闭包恰一次)");
        }

        [Test]
        public void test_claimAll_twice_doesNotDoubleClaim()
        {
            SeedDrop(PlayerCell, itemKey: 4, qty: 1);

            DriverLootActionSink sink = MakeSink();
            sink.ClaimAll();
            sink.ClaimAll();

            Assert.AreEqual(1, ClaimedCount(),
                "第二发是空列表 ⇒ 零新增(幂等由 20 的归属面守)");
        }
    }
}
