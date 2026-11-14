// 第二段节拍表夹具(2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` §第二段 · `PrologueBeats.BuildSecondAct` 的 remarks
//
// ═══ 读作者态文件的具名声明(test-standards「只读外部夹具的具名声明义务」)════════
//   **读** `assets/data/world_poi.json`(只读,不写)。
//   **为什么必须读真表而不是内联**:本件的判据是「节拍表里的药铺 / 救助站坐标
//   **与关卡逻辑层逐格相同**」。内联一份期望值只能证明「测试自己和自己一致」,
//   而真正要防的漂移是**关卡工具改了 POI、节拍表没跟** —— 那只有读真表才看得见。
//
// ═══ 它钉的三件事 ═════════════════════════════════════════════════════════
//   ① 事实计数器是**增量**的,且流变短(读档)会清零重扫。
//   ② 节拍表里的 POI 坐标与烘焙数据逐格相同(漂移即红)。
//   ③ **前置链有效**:玩家站在城市入口范围内时,除非前四拍都发生过,
//      否则一拍都不许播 —— 这一条防的是「把 `requires` 摘掉 ⇒ 开局即播」。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>第二段:P3-01 事实计数 / 锚点同源 / 前置链。</summary>
    [TestFixture]
    public class prologue_second_act_test
    {
        // ── ① 事实计数器 ────────────────────────────────────────────────────

        private static SimEvent Ev(EventKind kind, long tick = 0)
            => new SimEvent(tick, new PatientId(1), 0, kind, default);

        /// <summary>只计新增;重复 Sync 同一批不翻倍。</summary>
        [Test]
        public void test_factCounter_countsOnlyNewEvents()
        {
            var counter = new PrologueFactCounter();
            var events = new List<SimEvent>
            {
                Ev(EventKind.ResourceHarvested),
                Ev(EventKind.ResourceHarvested),
            };

            counter.Sync(events);
            Assert.That(counter.Of(EventKind.ResourceHarvested), Is.EqualTo(2));

            counter.Sync(events);   // 没有新事件
            Assert.That(counter.Of(EventKind.ResourceHarvested), Is.EqualTo(2), "重复扫描不得翻倍");

            events.Add(Ev(EventKind.ResourceHarvested));
            counter.Sync(events);
            Assert.That(counter.Of(EventKind.ResourceHarvested), Is.EqualTo(3), "新增须计入");
            Assert.That(counter.Of(EventKind.EmergencyTreatmentApplied), Is.EqualTo(0), "未发生的种类 = 0");
        }

        /// <summary>流变短(读档 / 清空)⇒ 清零重扫,不保留跨档计数。</summary>
        [Test]
        public void test_factCounter_resetsWhenStreamShrinks()
        {
            var counter = new PrologueFactCounter();
            var events = new List<SimEvent>
            {
                Ev(EventKind.ResourceHarvested),
                Ev(EventKind.ResourceHarvested),
                Ev(EventKind.ResourceHarvested),
            };
            counter.Sync(events);
            Assert.That(counter.Of(EventKind.ResourceHarvested), Is.EqualTo(3));

            // 读档:流只剩一条
            events.RemoveRange(1, 2);
            counter.Sync(events);
            Assert.That(counter.Of(EventKind.ResourceHarvested), Is.EqualTo(1),
                "流变短 ⇒ 旧计数作废,按新流重扫");
        }

        // ── ② 锚点与关卡数据同源 ───────────────────────────────────────────

        /// <summary>药铺废墟 / 坍塌救助站两处坐标须与 `world_poi.json` 逐格相同。</summary>
        [Test]
        public void test_anchors_matchBakedPoiCells()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "assets", "data", "world_poi.json"));
            Assert.IsTrue(File.Exists(path), "缺少烘焙产物:" + path);

            string text = File.ReadAllText(path);
            // 只做「这条 POI 的格串在文件里」的存在性判据 —— 解析器归 ADR-014(运行期零 JSON 解析器),
            // 夹具里手搓一个会让「解析口径」长出第二份。
            StringAssert.Contains("[" + SecondActAnchors.DrugshopX + ", 0, " + SecondActAnchors.DrugshopZ + "]",
                text, "药铺废墟格与 world_poi.json 漂移了(关卡工具改了 POI,节拍表没跟)");
            StringAssert.Contains("[" + SecondActAnchors.AidStationX + ", 0, " + SecondActAnchors.AidStationZ + "]",
                text, "坍塌救助站格与 world_poi.json 漂移了");
        }

        // ── ③ 前置链有效 ────────────────────────────────────────────────────

        /// <summary>玩家站在城市入口范围内、事实为零 ⇒ 一拍都不许播(前置未满足)。</summary>
        [Test]
        public void test_secondAct_nothingFires_beforePrerequisites()
        {
            var cell = new WorldPos(20, 0, 20);        // 城市入口(x ≤ 31)之内
            var regions = new RegionTriggerDriver(SecondActAnchors.BuildRegions(), () => cell);

            int children = 0, porters = 0;
            var beats = PrologueBeats.BuildSecondAct(regions, _ => 0, () => children++, () => porters++);
            var subs = new RecordingSubtitles();
            var driver = new PrologueBeatDriver(beats, () => cell, subs, narrationTicks: 1);

            for (long t = 1; t <= 20; t++)
            {
                regions.OnTickEdge(t);
                driver.OnTickEdge(t);
            }

            Assert.That(driver.FiredCount, Is.EqualTo(0),
                "前置(P1-07 / P2-01 …)未满足时第二段一拍都不得播 —— 摘掉 `requires` 会让城市入口开局即触发");
            Assert.That(children, Is.EqualTo(0));
            Assert.That(porters, Is.EqualTo(0));
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
