// 37 同源检测(T20)：旋钮产物 + 驱动 + 章推进 —— 判据夹具
//
// 权威：`case-system.md` 规则八(三例 ⇒ 同源)· F-37.1 / F-37.1b · GDD `:480`(`PATTERN_THRESHOLD`)
//       `PatternRecognitionWriter`(fires-once 真源 = `PatternDetector.NextEmissions`)
//       `ChapterAdvanceWriter`(章号绝对量、禁倒退)· `HookCasePolicy.MaxPerChapter`
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的五件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 旋钮真源 = `assets/data/case_pattern.json` 经 ADR-014 两阶段烘成 cooked,读回 = 3;
//   ② 绑定器拒收 `< 2`(GDD 安全范围)与未登记键 —— 判别力来自「同一份合法 JSON 只改一个数」;
//   ③ **三例同病种结案 ⇒ 一条 `PatternRecognized`**,且载荷四字段可回读;
//   ④ 同源模式识别 ⇒ **章推进到 1**(`ChapterAdvanced`),再扫不再写(幂等);
//   ⑤ 不足阈值 / 没有新结案 ⇒ **零写入**(零扫描省法不改变语义)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// · `assets/data/case_pattern.json`(经生产 `CasePatternBake.BakeFromRepo`)
// · `unity/Assets/DataCooked/case_pattern.cooked.bytes`(经生产 `CasePatternCookedCodec`)
//   为什么必须读真表:本条判的正是「源 → cooked → 运行期」这条链,内联一份数值只能证明夹具自身。
// ⚠️ **本文件零写盘**(烘焙全在内存里完成;读产物只读)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    [TestFixture]
    public sealed class pattern_recognition_driver_test
    {
        private const int DiseaseId = 2;          // INJ_STAB(刀伤)—— 三条结案同病种即同源
        private const int Threshold = 3;          // GDD P0

        private CompositionRootServices _bag;
        private CaseOpenWriter _openWriter;
        private CaseCloseWriter _closeWriter;
        private CaseCloseDriver _closeDriver;
        private PatternRecognitionDriver _driver;
        private long _tick;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _openWriter = new CaseOpenWriter(_bag.EventSink, _bag.Presence, _bag.Encoder, _bag.Stream.Events);
            _closeWriter = new CaseCloseWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            _tick = 0;
            _closeDriver = new CaseCloseDriver(_closeWriter, () => _bag.Stream.Events, _bag.Decoder, () => _tick);

            var recognizer = new PatternRecognitionWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events,
                worldSeed: 0xA11CE, patternThreshold: Threshold, scriptedChains: null, version: 1);
            var chapters = new ChapterAdvanceWriter(
                _bag.EventSink, _bag.Encoder, () => _bag.Stream.Events, _bag.Decoder);
            _driver = new PatternRecognitionDriver(_bag.Stream, recognizer, chapters, _bag.Decoder);
        }

        // ── 夹具:走完一个病人的 出现 → 立案 → 处置 → 结案 ──
        private void ClosedCase(int diseaseId, long openTick)
        {
            PatientId p = _bag.PatientSpawner.SpawnNext(diseaseId: diseaseId, tick: openTick);
            _bag.Presence.AddPresent(p, new WorldPos(0, 0, 0));
            Assert.AreEqual(CaseOpenWriteResult.Opened,
                _openWriter.TryOpen(openTick, p, new DiseaseIdSet(1)), "夹具:立案须成功");

            PayloadRef care = _bag.Encoder.Encode(
                EventKind.CareApplied, new CareAppliedPayload(openTick + 1, 0, 7, 0));
            _bag.EventSink.Append(new SimEvent(openTick + 1, p, -1, EventKind.CareApplied, care));

            _tick = openTick + 2;
            Assert.AreEqual(CaseCloseWriteResult.Closed, _closeDriver.TryClose(p), "夹具:结案须成功");
        }

        private int Count(EventKind kind) => _bag.Stream.Events.Count(e => e.Kind == kind);

        private ChapterAdvancedPayload LastChapter()
        {
            SimEvent e = _bag.Stream.Events.Last(x => x.Kind == EventKind.ChapterAdvanced);
            return _bag.Decoder.Decode<ChapterAdvancedPayload>(e.Kind, e.Payload);
        }

        // ══════════ ① 旋钮产物:源 → cooked → 读回 ══════════

        [Test]
        public void test_casePatternBake_repoValue_isThree_andRoundTrips()
        {
            CasePatternBake.BakeOutput baked = CasePatternBake.BakeFromRepo(RepoRoot());

            Assert.AreEqual(Threshold, baked.Bound.PatternThreshold, "源表当前值 = 3(GDD P0)");
            Assert.That(baked.ConfigVersion, Is.Not.EqualTo(0u), "ConfigVersion 须由内容哈希派生");

            CasePatternDataSet read = CasePatternCookedCodec.Read(baked.Cooked);
            Assert.AreEqual(Threshold, read.PatternThreshold, "cooked 读回须与源同值");
            Assert.AreEqual(baked.ConfigVersion, read.ConfigVersion, "版本号须逐位相同");
        }

        [Test]
        public void test_casePatternCodec_readsProductionArtifact()
        {
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "DataCooked",
                                       CasePatternDataSet.CookedFileName);
            Assert.IsTrue(File.Exists(path), "盘上须有已烘产物:" + path);

            CasePatternDataSet read = CasePatternCookedCodec.Read(File.ReadAllBytes(path));
            Assert.AreEqual(Threshold, read.PatternThreshold,
                "生产产物与源表不一致 ⇒ 改了 JSON 没重烘(漂移门之外的运行期面)");
        }

        // ══════════ ② 绑定器:两条负路径(判别力 = 同一份 JSON 只改一个数)══════════

        private static string Json(int threshold) =>
            "{\"schema_version\":1,\"pattern_threshold\":" + threshold + "}";

        [Test]
        public void test_casePatternBinder_rejectsThresholdBelowTwo()
        {
            Assert.AreEqual(Threshold, CasePatternBinder.Bind(Json(Threshold)).PatternThreshold,
                "正控:3 合法");

            Assert.Throws<BakeValidationException>(() => CasePatternBinder.Bind(Json(1)),
                "阈值 1 = 单例即同源(GDD :869 低侧理由)⇒ 构建期硬失败");
            Assert.Throws<BakeValidationException>(() => CasePatternBinder.Bind(Json(0)),
                "阈值 0 ⇒ 空候选集也『达标』,anchor_case 不存在");
        }

        [Test]
        public void test_casePatternBinder_rejectsUnknownKey()
        {
            var ex = Assert.Throws<BakeValidationException>(() => CasePatternBinder.Bind(
                "{\"schema_version\":1,\"pattern_threshold\":3,\"threshold\":3}"));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("未登记键")), "未登记键须具名报出");
        }

        // ══════════ ③ + ④ 三例同源 ⇒ 识别 + 章推进 ══════════

        [Test]
        public void test_threeSameDiseaseCases_recognizePattern_andAdvanceChapter()
        {
            ClosedCase(DiseaseId, 10);
            ClosedCase(DiseaseId, 20);

            // 两例:不足阈值 ⇒ 什么也不写
            Assert.AreEqual(0, _driver.OnTickEdge(tick: 30), "两例不得推进章");
            Assert.AreEqual(0, Count(EventKind.PatternRecognized), "两例不得识别同源");

            ClosedCase(DiseaseId, 40);

            int advanced = _driver.OnTickEdge(tick: 50);

            Assert.AreEqual(1, Count(EventKind.PatternRecognized), "三例同病种 ⇒ 恰一条 PatternRecognized");
            Assert.AreEqual(1, Count(EventKind.ChapterAdvanced), "同源识别 ⇒ 章推进到 1");
            Assert.AreEqual(1, advanced, "返回值 = 本 tick 新推进的章数");

            SimEvent evt = _bag.Stream.Events.Last(e => e.Kind == EventKind.PatternRecognized);
            Assert.AreEqual(PatientId.None, evt.Patient, "世界级事件(ADR-007 §四 哨兵)");

            var payload = _bag.Decoder.Decode<PatternRecognizedPayload>(evt.Kind, evt.Payload);
            Assert.AreEqual(1UL << (DiseaseId - 1), payload.MemberDiseaseSet.Bits,
                "member_set 的位序 = DiseaseId − 1(DiseaseIdSet 契约)");
            Assert.AreEqual(1, payload.Version, "冻结三元组 3/3 = 病种映射表版本(本夹具传 1)");
            Assert.AreNotEqual(0UL, payload.SaltedKey, "盐须由 WorldSeed 派生(非 0)");

            Assert.AreEqual(1, LastChapter().Chapter, "章号 = 已识别同源模式条数(绝对量)");
        }

        // ══════════ ④′ 幂等:同一份流再扫 / 无新结案 ⇒ 零写入 ══════════

        [Test]
        public void test_repeatedTicks_doNotDoubleWrite()
        {
            ClosedCase(DiseaseId, 10);
            ClosedCase(DiseaseId, 20);
            ClosedCase(DiseaseId, 30);
            Assert.AreEqual(1, _driver.OnTickEdge(tick: 40));

            int patterns = Count(EventKind.PatternRecognized);
            int chapters = Count(EventKind.ChapterAdvanced);

            Assert.AreEqual(0, _driver.OnTickEdge(tick: 41), "没有新结案 ⇒ 零推进");
            Assert.AreEqual(patterns, Count(EventKind.PatternRecognized), "fires-once:不得重发");
            Assert.AreEqual(chapters, Count(EventKind.ChapterAdvanced), "章不得重复推进");
        }

        // ══════════ ⑤ 首次调用(读档自愈)══════════

        /// <summary>
        /// 驱动**从未扫过**时,流里已有的三例(读档形态)⇒ 首次 tick 即识别并推进章。
        /// <para>判别力:若把首轮扫描省掉(只在「条数变化」时扫),读档后章永远停在 0 ——
        /// 而钩子配额正读它。</para>
        /// </summary>
        [Test]
        public void test_firstTick_afterLoad_recognizesExistingCases()
        {
            ClosedCase(DiseaseId, 10);
            ClosedCase(DiseaseId, 20);
            ClosedCase(DiseaseId, 30);

            Assert.AreEqual(1, _driver.OnTickEdge(tick: 100), "首轮必扫 ⇒ 读档后自愈");

            Assert.AreEqual(1, Count(EventKind.PatternRecognized));
            Assert.AreEqual(1, LastChapter().Chapter);
        }

        // ══════════ 辅助 ══════════

        private static string RepoRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "assets", "data", "case_pattern.json")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            Assert.Fail("找不到仓根(须含 assets/data/case_pattern.json)");
            return null;
        }
    }
}
