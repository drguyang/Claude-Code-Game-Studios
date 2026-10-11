// sprint-06 G1.3 · 查体入口驱动 EditMode 判据(2026-10-11)
//
// 判据(可证伪):
//   ① 玩家对已建档病人查体 ⇒ 8 求值 → 分册快照落账(Revealed 非空当词条可读);
//   ② **病史流零新事件**(查体 = 派生态,乙案:不进三流)——
//      EventStream.Count 在 OnExamine 前后不变(可证伪判据「病史流哈希逐位相同」的轻量版);
//   ③ 不在场 / 未建档病人 ⇒ GetVitals fail-loud(查体无从谈起)。
//
// 纯逻辑 EditMode:手动装配 真 EventStream + 真 PatientSpawner(写 DiseaseOnset) +
// 真 DiseaseVitalsService + 合成门槛表/词表 —— 全生产路径(禁 Fake)。照 vertical_slice 先例。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;
// CS0104 消歧(承 sign_table_test):SignChannel = R-8.1 枚举。
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="ExamineDriver"/> 接通判据(G1.3)。</summary>
    internal sealed class ExamineDriverTest
    {
        private const int SkillCap = 60;

        private CompositionRootServices _bag;
        private ExaminationLedger _ledger;
        private ExamineDriver _driver;
        private DiagnosisReadFloorTable _floor;
        private List<SignLexemeRow> _rows;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _ledger = new ExaminationLedger();
            _driver = new ExamineDriver(_bag.VitalsService, _ledger, queryLevel: 5);

            // 合成定表:恒定门槛 0.5(测试夹具;真实定表由 read_floor 源烘焙)
            var floor = new float[SkillCap + 1];
            for (int i = 0; i <= SkillCap; i++) floor[i] = 0.5f;
            _floor = new DiagnosisReadFloorTable(SkillCap, 0x8000L, 0x8000L, 0x10000L, floor);

            _rows = new List<SignLexemeRow>
            {
                new SignLexemeRow("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" },
                    SignChannel.FaceColor, new[] { RevealMethod.Inspection },
                    1, SignPolarity.Positive, null),
            };
        }

        [Test]
        public void test_examine_recordedSnapshot_inBagStream_noNewEvent()
        {
            // 真病人建档(DiseaseOnset 入流;diseaseId=1 = fallback 注册表)
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(5);   // 推进求值(建档可见)

            int streamCountBefore = _bag.Stream.Count;

            // Act:查体(仅当该病人当前位置未越过 READ_FLOOR —— 合成病程 position 取 dto.Position)
            _driver.OnExamine(patient, tick: 6, in _floor, _rows);

            // Assert ①:分册有该病人快照(查体事实落账)
            Assert.IsTrue(_ledger.HasExamined(patient.Value), "查体后分册有记录");

            // Assert ②:病史流零新事件(查体 = 派生态,乙案)
            Assert.AreEqual(streamCountBefore, _bag.Stream.Count,
                "查体不得写任何 SimEvent —— 病史流哈希逐位相同的可证伪轻量版(乙案)");
        }

        [Test]
        public void test_examine_belowFloor_emptySet_noMask()
        {
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(0);   // 潜伏期(τ=0 < incubation)⇒ position = 0 < 0.5

            _driver.OnExamine(patient, tick: 1, in _floor, _rows);

            Assert.IsTrue(_ledger.TryGetSnapshot(patient.Value, out var snap),
                "查了就是查了(读数低也要记快照 —— AC-8-25 快照冻结)");
            Assert.AreEqual(0, snap.Set.SignCount,
                "position=0 < floor=0.5 ⇒ 全读不出 ⇒ 空集(值区空 = 阴性形态,禁假数据)");
            Assert.AreEqual(0, snap.Set.SignChannelMask, "空集 ⇒ 掩码 0");
        }

        [Test]
        public void test_examine_unknownPatient_failsLoud()
        {
            // 未建档病人 ⇒ GetVitals fail-loud(查体无从谈起,不吞)
            Assert.Throws<PatientCourseNotFoundException>(
                () => _driver.OnExamine(new PatientId(9999), tick: 1, in _floor, _rows),
                "不在场/未建档查体须 fail-loud —— 全零 DTO 与「真无症状」不可区分");
        }

        [Test]
        public void test_examine_reExamine_overwritesSnapshot()
        {
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(0);   // position=0

            _driver.OnExamine(patient, tick: 1, in _floor, _rows);   // 空集
            _driver.OnExamine(patient, tick: 2, in _floor, _rows);   // 复查(同读法)

            Assert.IsTrue(_ledger.TryGetSnapshot(patient.Value, out var snap));
            Assert.AreEqual(2, snap.ExaminedTick, "复查覆盖 → tick 更新(AC-8-46 后续精度)");
        }
    }
}