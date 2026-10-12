// sprint-08 S1.1 件 B · 「查体 ⇒ 见过病 D」投影器判据(2026-10-12)
//
// 判据(可证伪):
//   ① 查体完成 ⇒ 病史流新增一条 SkillGrown(诊断, objectId = 病种 D, patient = 该病人);
//   ② novelty 真值:首见 ⇒ First;冷却内复查 ⇒ Stale(同栏不误判为 Normal);
//   ③ 已见集枚举 = 病名册条目集的数据源(SeenDiseaseIds 含 D,升序);
//   ④ 载荷可解码回读 ⇒ 已见集可从病史流重建(ADR-010/026「不独立快照」的可证伪面);
//   ⑤ fail-loud:未建档病人 ⇒ PatientCourseNotFoundException(装配/时序错误不静默);
//   ⑥ 负参 fail-loud(tick / actorId 为负)。
//
// 全生产路径:真组合根 + 真 PatientSpawner(写 DiseaseOnset)+ 真 DiseaseVitalsService
// + 真 EventStream/encoder/blobPool(照 prescription_page_host_test / examine_driver_test 先例)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="SeenDiseaseProjector"/> 判据(sprint-08 S1.1 件 B)。</summary>
    internal sealed class seen_disease_projector_test
    {
        private const int PlayerId = 9001;

        private CompositionRootServices _bag;
        private PatientId _patient;

        /// <summary>建档一个在场病人(病种 = 组合根 fallback 表唯一病种 DiseaseId=1)。</summary>
        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 查体 ⇒ SkillGrown 入病史流
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_examined_emitsSkillGrownWithDiseaseObjectId()
        {
            _bag.SeenDisease.OnExamined(_patient, tick: 5, actorId: PlayerId);

            int n = 0;
            SkillGrownPayload decoded = default;
            bool decodedOk = false;
            foreach (var e in _bag.Stream.Events)
            {
                if (e.Kind != EventKind.SkillGrown) continue;
                n++;
                decodedOk = PayloadCodec.TryGetPayload(in e, _bag.BlobPool, out decoded);
                Assert.AreEqual(_patient.Value, e.Patient.Value,
                    "SkillGrown 的 patient 项 = 被查病人");
            }

            Assert.AreEqual(1, n, "查体一次 ⇒ 恰一条 SkillGrown");
            Assert.IsTrue(decodedOk, "SkillGrown 载荷可解码(编码路径 ADR-029 真走通)");
            Assert.AreEqual((int)SkillId.诊断, decoded.SkillId, "成长技能 = 诊断(ordinal 0)");
            Assert.AreEqual(1, decoded.ObjectId, "objectId = 病种 D(= 建档病种 1)");
            Assert.AreEqual(PlayerId, decoded.ActorId, "施予者 = 玩家 actorId");
            Assert.AreEqual((int)NoveltyClass.First, decoded.NoveltyClass,
                "首见 ⇒ novelty = First");
            Assert.AreEqual(SkillGrownPayload.LevelNotGrown, decoded.Level,
                "未升级 ⇒ level = 哨兵 -1");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② novelty 冷却语义:复查 ⇒ Stale(不误判 Normal)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_repeatExamWithinCooldown_noveltyStale()
        {
            _bag.SeenDisease.OnExamined(_patient, tick: 5, actorId: PlayerId);
            _bag.SeenDisease.OnExamined(_patient, tick: 6, actorId: PlayerId);  // 冷却内

            var novelties = new System.Collections.Generic.List<int>();
            foreach (var e in _bag.Stream.Events)
            {
                if (e.Kind != EventKind.SkillGrown) continue;
                Assert.IsTrue(PayloadCodec.TryGetPayload(in e, _bag.BlobPool, out SkillGrownPayload p));
                novelties.Add(p.NoveltyClass);
            }

            Assert.AreEqual(2, novelties.Count, "两次查体 ⇒ 两条成长(每次查体一条)");
            Assert.AreEqual((int)NoveltyClass.First, novelties[0], "首见 ⇒ First");
            Assert.AreEqual((int)NoveltyClass.Stale, novelties[1],
                "冷却内复查 ⇒ Stale(NoveltyTracker 真值,非恒 Normal)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 已见集枚举 = 病名册条目集的数据源
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_seenDiseaseIds_reflectsExaminedDiseases()
        {
            Assert.IsEmpty(_bag.SeenDisease.SeenDiseaseIds(PlayerId),
                "未查体 ⇒ 已见集空(病名册无条目)");

            _bag.SeenDisease.OnExamined(_patient, tick: 5, actorId: PlayerId);

            Assert.AreEqual(new[] { 1 }, _bag.SeenDisease.SeenDiseaseIds(PlayerId),
                "查体后 ⇒ 已见集含病种 1(病名册条目集的反查输入)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 已见集可从病史流重建(ADR-010/026「不独立快照」的可证伪面)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_seenSet_rebuildableFromHistoryStream()
        {
            _bag.SeenDisease.OnExamined(_patient, tick: 5, actorId: PlayerId);

            // 模拟读档重建:新建空 tracker,扫病史流 SkillGrown 回填
            var rebuilt = new NoveltyTracker();
            foreach (var e in _bag.Stream.Events)
            {
                if (e.Kind != EventKind.SkillGrown) continue;
                if (!PayloadCodec.TryGetPayload(in e, _bag.BlobPool, out SkillGrownPayload p)) continue;
                rebuilt.Query(p.ActorId, p.SkillId, p.ObjectId, (int)e.Tick);
            }

            Assert.AreEqual(_bag.SeenDisease.SeenDiseaseIds(PlayerId),
                rebuilt.EnumerateSeenObjects(PlayerId, (int)SkillId.诊断),
                "已见集可从病史流逐位重建(运行期缓存非第二真源)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ fail-loud:未建档病人
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_unregisteredPatient_failLoud()
        {
            var ghost = new PatientId(4242);   // 未建档、不在场
            Assert.Throws<PatientCourseNotFoundException>(
                () => _bag.SeenDisease.OnExamined(ghost, tick: 5, actorId: PlayerId),
                "未建档病人查体 ⇒ fail-loud(不静默写空病种)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑥ 负参 fail-loud
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_negativeArgs_failLoud()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _bag.SeenDisease.OnExamined(_patient, tick: -1, actorId: PlayerId),
                "tick 为负 ⇒ fail-loud");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _bag.SeenDisease.OnExamined(_patient, tick: 5, actorId: -1),
                "actorId 为负 ⇒ fail-loud");
        }
    }
}
