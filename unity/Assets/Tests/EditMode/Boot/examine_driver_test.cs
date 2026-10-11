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

using System;
using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Bake;
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

        /// <summary>仓根:由本文件路径回溯(承 sign_table_test 先例;batch 模式
        /// AppContext.BaseDirectory 指向 Unity 安装目录,不可用)。
        /// ⚠️ 2026-10-12 实测:CallerFilePath = 源码绝对路径 ⇒ 本文件位于
        /// `unity/Assets/Tests/EditMode/Boot/`,到仓根恰需 <c>..×5</c>。上一轮用
        /// Assembly.Location(ScriptAssemblies)+ ×4 算出 <c>仓根-1</c>(少一跳)已红。</summary>
        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

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

            // S1.2:真烘焙产物经组合根装载 ⇒ _bag.ExamineDriver 已注入真词表/门槛。
            // 本测试沿用「合成夹具」覆盖(既有判据面不变):恒定门槛 0.5 + 单条词。
            var floor = new float[SkillCap + 1];
            for (int i = 0; i <= SkillCap; i++) floor[i] = 0.5f;
            _floor = new DiagnosisReadFloorTable(SkillCap, 0x8000L, 0x8000L, 0x10000L, floor);

            _rows = new List<SignLexemeRow>
            {
                new SignLexemeRow("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" },
                    SignChannel.FaceColor, new[] { RevealMethod.Inspection },
                    1, SignPolarity.Positive, null),
            };

            _driver.SetLexicon(_rows, in _floor);
        }

        // ══════════ S1.2 新增判据:真烘焙产物装载面 ══════════

        [Test]
        public void test_s12_compositionRoot_loadsRealLexicon_fromBaked()
        {
            // S1.2 判据:组合根从**真烘焙产物**装载词表 + 门槛(非合成夹具、非空表)
            Assert.IsTrue(_bag.ExamineDriver.HasLexicon,
                "组合根装配后 ExamineDriver 须已注入词表(S1.2 装载面闭合)");
            Assert.AreEqual(34, _bag.ExamineDriver.LexiconRowCount,
                "真词表 34 行(R-8.2 P0 冻结表;0 = 装载失败被吞 = 假绿)");
        }

        [Test]
        public void test_s12_cookedDisk_roundtrip_matchesBakerRows()
        {
            // 兜底装载读的 = bake 写的同一份字节;此处与 **baker 现烘逐字节比对**
            // (C3-B3 产物漂移门):源 JSON 改了不重烘 ⇒ 库内字节 ≠ baker 现烘输出 ⇒ 红。
            var baked = DiagnosisSignBaker.BakeFromRepo(RepoRoot);
            byte[] onDisk = System.IO.File.ReadAllBytes(
                System.IO.Path.Combine(
                    DaYiJingCheng.Gameplay.Boot.DiagnosisCookedDisk.CookedDirectory(),
                    "diagnosis_signs.cooked.bytes"));
            Assert.AreEqual(baked.Cooked, onDisk,
                "库内 diagnosis_signs.cooked.bytes 须 = baker 现烘输出(改源不重烘 = 漂移,恰红)");
            Assert.AreNotEqual(0u, baked.ConfigVersion, "ConfigVersion 非零(源内容哈希派生)");
        }

        [Test]
        public void test_s12_readFloor_cooked_matchesBaker()
        {
            // read_floor 对称漂移门(C3-B3 补的另一半)
            var baked = DiagnosisReadFloorBaker.BakeFromRepo(RepoRoot);
            byte[] onDisk = System.IO.File.ReadAllBytes(
                System.IO.Path.Combine(
                    DaYiJingCheng.Gameplay.Boot.DiagnosisCookedDisk.CookedDirectory(),
                    "diagnosis_read_floor.cooked.bytes"));
            Assert.AreEqual(baked.Cooked, onDisk,
                "库内 diagnosis_read_floor.cooked.bytes 须 = baker 现烘输出(漂移恰红)");
            Assert.AreNotEqual(0u, baked.ConfigVersion, "ConfigVersion 非零");
        }

        [Test]
        public void test_s12_examineRealLexicon_revealedRows_nonEmpty()
        {
            // C3-B3:头号判据「查体后值区非空」的**真词表端到端正向测** ——
            // 走组合根的真 34 行词表 + 真门槛 + 真体征(position)。三个真面:
            //   · 真词表 tier_named ∈ {1,10,20} ⇒ 组合根 queryLevel=0 **读不出任何词**
            //     (双条件「与」门 named = skill ≥ tier_named;0 < 1)⇒ 断言用
            //     skill=5(≥1,<10 ⇒ 20 条 tier_named=1 词可读)—— 这就是真实玩家
            //     熟练度 > 0 时的查体面;
            //   · fallback 病种 RecoveryTime=100 ⇒ τ<100 期间 position≡0(潜伏抑制)
            //     ⇒ 推进 tick 越潜伏(τ=120 → position 1.0);
            //   · 真门槛 floor(skill=5) ≈ 0.88 < 1.0 ⇒ readable ⇒ 收录。
            // 断言走**求值器**(OnExamine 的同调用面):真词表 + 真门槛 + 真体征
            // position 1.0 + skill 5 ⇒ SignCount > 0 = 「值区有词」的真链。
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);   // τ=0:潜伏期(Accumulate)
            for (long t = 1; t <= 120; t++)
                _bag.VitalsService.OnTickEdge(t);   // 推进过潜伏期 → position 1.0(真曲线)

            var realRows = _bag.ExamineDriver.LexiconRows();      // 真 34 行(组合根装载)
            var realFloor = _bag.ExamineDriver.FloorTable();      // 真门槛定表(同上)
            var vitals = _bag.VitalsService.GetVitals(patient);   // 真体征(position)

            Assert.Greater(realRows.Count, 0, "真词表非空(删装载 = 恰红)");
            var set = DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisExamineEvaluator.Evaluate(
                in realFloor, realRows, skill: 5, vitals.Position);
            Assert.Greater(set.SignCount, 0,
                "查体后值区有词(C3-B3 头号判据的真链:真词表 × 真门槛 × 真体征)");
        }

        [Test]
        public void test_s12_examineDriver_withoutLexicon_failsLoud()
        {
            // 禁假数据:未注入词表的驱动不得假装完成查体
            var bare = new ExamineDriver(_bag.VitalsService, _ledger, queryLevel: 5);
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(5);
            Assert.Throws<InvalidOperationException>(
                () => bare.OnExamine(patient, tick: 6),
                "词表未注入 ⇒ fail-loud(装配遗漏不得静默降级为空集)");
        }

        [Test]
        public void test_examine_recordedSnapshot_inBagStream_noNewEvent()
        {
            // 真病人建档(DiseaseOnset 入流;diseaseId=1 = fallback 注册表)
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(5);   // 推进求值(建档可见)

            int streamCountBefore = _bag.Stream.Count;

            // Act:查体(仅当该病人当前位置未越过 READ_FLOOR —— 合成病程 position 取 dto.Position)
            _driver.OnExamine(patient, tick: 6);

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

            _driver.OnExamine(patient, tick: 1);

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
                () => _driver.OnExamine(new PatientId(9999), tick: 1),
                "不在场/未建档查体须 fail-loud —— 全零 DTO 与「真无症状」不可区分");
        }

        [Test]
        public void test_examine_reExamine_overwritesSnapshot()
        {
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(0);   // position=0

            _driver.OnExamine(patient, tick: 1);   // 空集
            _driver.OnExamine(patient, tick: 2);   // 复查(同读法)

            Assert.IsTrue(_ledger.TryGetSnapshot(patient.Value, out var snap));
            Assert.AreEqual(2, snap.ExaminedTick, "复查覆盖 → tick 更新(AC-8-46 后续精度)");
        }
    }
}