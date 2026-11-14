// 第三段节拍表夹具(2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` §第三段 · `PrologueBeats.BuildThirdAct` 的 remarks
//
// ═══ 它钉的三件事 ═════════════════════════════════════════════════════════
//   ① **按模块分桶的放置计数**(「帐篷建成没」的唯一判据面):同一批事件里 10 号与 8 号分开计,
//      不给解码器时**恒 0**(如实返回「不知道」,不猜)。
//   ② **表里不该有的两拍**:P3-02a 是按 Enter 的即时反馈(N4),P3-03 等陈婆婆的实体裁定 ——
//      谁把它们加回表里,本件当场红。
//   ③ 前置链有效:玩家站在营地里、事实为零时,一拍都不许播。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>第三段:模块分桶计数 / 表的成分 / 前置链。</summary>
    [TestFixture]
    public class prologue_third_act_test
    {
        private static SimEvent Placed(PayloadEncoder encoder, int moduleId)
            => new SimEvent(0, new PatientId(1), 0, EventKind.StructurePlaced,
                encoder.Encode(EventKind.StructurePlaced,
                    new StructurePlacedPayload(1L, new WorldPos(108, 0, 16), moduleId, 0, 0)));

        // ── ① 模块分桶计数 ──────────────────────────────────────────────────

        /// <summary>同批事件按模块分开计;未出现的模块 = 0。</summary>
        [Test]
        public void test_placedCount_bucketsByModule()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            var events = new List<SimEvent> { Placed(encoder, 10), Placed(encoder, 8), Placed(encoder, 10) };
            var counter = new PrologueFactCounter();
            counter.Sync(events, decoder);

            Assert.AreEqual(2, counter.PlacedCount(ThirdActAnchors.TentModuleId), "帐篷(10)须计 2");
            Assert.AreEqual(1, counter.PlacedCount(8), "木柱模块(8)须计 1");
            Assert.AreEqual(0, counter.PlacedCount(11), "未出现的模块 = 0");
            Assert.AreEqual(3, counter.Of(EventKind.StructurePlaced), "总计数不受分桶影响");
        }

        /// <summary>不给解码器 ⇒ 分桶恒 0(「不知道」,不是「没有」)。</summary>
        [Test]
        public void test_placedCount_withoutDecoder_isZero()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var events = new List<SimEvent> { Placed(encoder, 10) };

            var counter = new PrologueFactCounter();
            counter.Sync(events);   // 无解码器重载

            Assert.AreEqual(1, counter.Of(EventKind.StructurePlaced), "总数仍可计");
            Assert.AreEqual(0, counter.PlacedCount(10), "无解码器时分桶为 0 —— 不得猜");
        }

        /// <summary>流变短(读档)⇒ 分桶一并清零。</summary>
        [Test]
        public void test_placedCount_resetsOnShrink()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);
            var events = new List<SimEvent> { Placed(encoder, 10), Placed(encoder, 10) };

            var counter = new PrologueFactCounter();
            counter.Sync(events, decoder);
            Assert.AreEqual(2, counter.PlacedCount(10));

            events.RemoveAt(1);
            counter.Sync(events, decoder);
            Assert.AreEqual(1, counter.PlacedCount(10), "读档后须按新流重扫,不保留旧桶");
        }

        // ── ② 表的成分 ─────────────────────────────────────────────────────

        /// <summary>表里不得有 P3-02a(N4 的即时反馈)与 P3-03(等实体裁定)。</summary>
        [Test]
        public void test_table_excludesDecisionPendingBeats()
        {
            var cell = new WorldPos(18, 0, 33);
            var regions = new RegionTriggerDriver(ThirdActAnchors.BuildRegions(), () => cell);
            var beats = PrologueBeats.BuildThirdAct(regions, _ => 0, _ => 0);

            var ids = new List<string>();
            foreach (PrologueBeat b in beats) ids.Add(b.Id);

            CollectionAssert.DoesNotContain(ids, "P3-02a",
                "P3-02a 是按 Enter 的即时反馈(N4),不是一次性节拍 —— 进表会让第二次按键静默");
            CollectionAssert.DoesNotContain(ids, "P3-03",
                "P3-03 等「陈婆婆是不是病人」的裁定 —— 未裁前不得进表");
            CollectionAssert.Contains(ids, "P3-02b");
            CollectionAssert.Contains(ids, "P3-05");
        }

        // ── ③ 前置链 ────────────────────────────────────────────────────────

        /// <summary>玩家站在营地格、事实为零 ⇒ 一拍都不许播。</summary>
        [Test]
        public void test_thirdAct_nothingFires_beforePrerequisites()
        {
            var cell = new WorldPos(100, 0, 20);   // 营地(x ≥ 96)之内
            var regions = new RegionTriggerDriver(ThirdActAnchors.BuildRegions(), () => cell);
            var beats = PrologueBeats.BuildThirdAct(regions, _ => 0, _ => 0);
            var subs = new RecordingSubtitles();
            var driver = new PrologueBeatDriver(beats, () => cell, subs, narrationTicks: 1);

            for (long t = 1; t <= 20; t++)
            {
                regions.OnTickEdge(t);
                driver.OnTickEdge(t);
            }

            Assert.AreEqual(0, driver.FiredCount,
                "P2-09 / 帐篷 / 病人 / 立案 都还没发生时,P3-01 起一拍都不得播");
        }

        private sealed class RecordingSubtitles : DaYiJingCheng.Gameplay.UI.Skeuomorphic.ISubtitleSink
        {
            public readonly List<string> Shown = new List<string>();
            public void Show(in DaYiJingCheng.Gameplay.UI.Skeuomorphic.SubtitleLineData line)
                => Shown.Add(line.Text);
            public void Hide() { }
        }
    }
}
