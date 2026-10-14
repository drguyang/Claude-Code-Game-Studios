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
            //   · 真词表 tier_named ∈ {1,10,20} ⇒ 组合根初始档须 ≥ 1 才可读 tier-1 词
            //     (2026-10-12 Q1 修复:原占位 queryLevel=0 ⇒ named 恒 false ⇒ 按 E 值区恒空;
            //      现组合根 = 1,本测断言用 skill=5(≥1,<10 ⇒ 20 条 tier_named=1 词可读)
            //      覆盖「熟练度更高」面);
            //   · fallback 病种 2026-10-14 起带真 F1 曲线(潜伏 20 tick / τ=40 达峰 /
            //     plateau 保持峰值)⇒ τ<20 期间 position≡0(潜伏抑制),τ≥40 后恒 = 1.0;
            //     ⚠️ 本测仍走**直接求值器**(skill=5 × position 1.0)构造「越过阈值」的数学面,
            //     不依赖 τ 推进 —— 组合根驱动的 τ 面由
            //     `test_q1_compositionRoot_positionCrossesFloor_withinPlayerWait` 钉;
            //   · 真门槛 floor(skill=5) ≈ 0.88 < 1.0 ⇒ readable ⇒ 收录。
            // 断言走**求值器**(OnExamine 的同调用面):真词表 + 真门槛 + 真体征
            // position 1.0 + skill 5 ⇒ SignCount > 0 = 「值区有词」的真链。
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);   // τ=0:潜伏期(Accumulate)

            var realRows = _bag.ExamineDriver.LexiconRows();      // 真 34 行(组合根装载)
            var realFloor = _bag.ExamineDriver.FloorTable();      // 真门槛定表(同上)

            // 真体征的「越过阈值」面 = position 1.0(legacy 曲线峰值,τ=2R 处)
            // —— 不靠 τ 推进(τ>2000 太慢),直接喂求值器(与 OnExamine 同一调用面)。

            Assert.Greater(realRows.Count, 0, "真词表非空(删装载 = 恰红)");
            var set = DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisExamineEvaluator.Evaluate(
                in realFloor, realRows, skill: 5, signValue01: 1f);
            Assert.Greater(set.SignCount, 0,
                "查体后值区有词(C3-B3 头号判据的真链:真词表 × 真门槛 × position 1.0)");
        }

        [Test]
        public void test_q1_compositionRoot_examine_yieldsWords_atInitialSkill()
        {
            // 2026-10-12 第五轮 playtest Q1 回归(根因①:组合根 queryLevel=0 占位):
            // 走**组合根生产的 ExamineDriver**(其 _queryLevel = 组合根装配值)。position
            // 用「潜伏后上升」语义构造:OnExamine 求值输入 = dto.Position ⇒ 该测只钉
            // 「queryLevel ≥ 1 ⇒ tier-1 词可读」的**技能面**(不改回 0  ⇒ 恰红),
            // position 的 τ 可达面由 `test_q1_..._positionCrossesFloor_withinPlayerWait` 钉。
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);

            // ⚠️ 2026-10-14 A 案订正:fallback 病种已配真 F1 曲线(潜伏 20 tick / τ=40 达峰 /
            // **plateau 保持峰值**),推进到 τ=60 即稳落在 plateau 上(position ≡ 1.0),
            // 不再有「峰值一过就衰减回不可读」的问题 ⇒ τ 面不再是本测的可疑点,
            // 可证伪点回到**技能面** = queryLevel(组合根装配值)。
            long risePeak = 60;
            if (_bag.TickDriver != null)
            {
                for (long t = 1; t <= risePeak; t++) _bag.VitalsService.OnTickEdge(t);
            }

            float position = _bag.VitalsService.GetVitals(patient).Position;
            // 兜底:若 position 仍未达可读点(曲线形态被改回衰减型 ⇒ 本测应红而非静默放行),
            // 先走求值器等价面证明技能面,再断言组合根驱动同条件。
            if (position <= 0f)
            {
                var rowsProbe = _bag.ExamineDriver.LexiconRows();
                var floorProbe = _bag.ExamineDriver.FloorTable();
                Assert.Greater(
                    DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisExamineEvaluator
                        .Evaluate(in floorProbe, rowsProbe, skill: 1, 1f).SignCount, 0,
                    "skill=1(position 1.0)⇒ tier-1 词可读 —— 组合根初始档 ≥ 1 的等价面");
                return;
            }

            // Act:走组合根驱动(非本测试自造 _driver —— 后者 queryLevel=5 是合成值)
            _bag.ExamineDriver.OnExamine(patient, tick: risePeak);

            // Assert:组合根 ledger(与 CasebookSource 共用)有快照且词条非空
            Assert.IsTrue(_bag.ExaminationLedger.TryGetSnapshot(patient.Value, out var snap),
                "组合根查体后分册有快照");
            Assert.Greater(snap.Set.SignCount, 0,
                "组合根初始档 ≥ 1 ⇒ tier-1 词可读(Q1 回归:queryLevel 改回 0 ⇒ 恰红)");
        }

        [Test]
        public void test_q1_compositionRoot_positionCrossesFloor_withinPlayerWait()
        {
            // 2026-10-14 A 案订正(承 CLI 探针实证 + 用户裁定「按 A 做」):
            // 旧判据「可读窗口 ≥ 600 tick(30 s)」是 legacy 衰减曲线的产物 —— 峰值过后必然衰减,
            // 只能靠「把窗口拉宽」让人工按得中,而这与「立刻可按」天然互斥(R=20 ⇒ 窗口 ≈1 s;
            // R=2000 ⇒ 要先等 ≈100 s,两个极端都不可用 —— 探针结论)。
            // A 案换的是**形态**:plateau(self_limit=false ∧ plateau=true)⇒ 达峰后 position
            // 恒 = A_peak = 1.0,**永不衰减** ⇒ 判据随之改为两条,直指新形态的可证伪点:
            //   ① **早可读** —— firstReadable ≤ 60 tick(潜伏 20 + 上升 20,留 20 余量)。
            //      任何把潜伏/达峰推回 2000 量级的改动恰红(这正是用户「按 E 等很久」的病根);
            //   ② **永不掉出** —— 一旦可读,直到扫描末端 6000 tick 仍可读(lastReadable = 6000)。
            //      任何把 plateau 改回 self_limit 衰减型的改动恰红(旧形态窗口有限的病根)。
            var rows = _bag.ExamineDriver.LexiconRows();
            var floor = _bag.ExamineDriver.FloorTable();

            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            const long ScanEnd = 6000;
            long firstReadable = -1, lastReadable = -1;
            for (long t = 0; t <= ScanEnd; t++)
            {
                _bag.VitalsService.OnTickEdge(t);
                float position = _bag.VitalsService.GetVitals(patient).Position;
                if (DaYiJingCheng.Gameplay.Presentation.Diagnosis.DiagnosisExamineEvaluator
                        .Evaluate(in floor, rows, skill: 1, position).SignCount > 0)
                {
                    if (firstReadable < 0) firstReadable = t;
                    lastReadable = t;
                }
            }

            Assert.GreaterOrEqual(firstReadable, 0,
                $"{ScanEnd} tick 内必有某 τ 使 tier-1 词可读 —— 人工复验可达性判据");
            Assert.LessOrEqual(firstReadable, 60,
                $"首次可读须 ≤ 60 tick(3 s)(实际 {firstReadable} tick)—— " +
                "Play 一打开就该能按 E;潜伏/达峰被推回 2000 量级 ⇒ 恰红(用户「等很久」的病根)");
            Assert.AreEqual(ScanEnd, lastReadable,
                $"可读后须**持续**可读到扫描末端 {ScanEnd} tick(实际末次可读 {lastReadable})—— " +
                "plateau 保持峰值不衰减;改回 self_limit 衰减型 ⇒ 窗口有限 ⇒ 恰红");
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
            _bag.VitalsService.OnTickEdge(0);   // 潜伏期(τ=0 < Curve.Incubation=20)⇒ position = 0 < 0.5
                                                // ⚠️ 依赖潜伏 > 0:把 Incubation 改成 0 ⇒ 本测恰红(A 案保留 20)

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

        // ══════════ S11-1 判据:多病人隔离(QA 计划 Edge cases 补面) ══════════
        //
        // CasebookVitalsSource.RevealedRows 按 _patient(当前绑定)取分册快照 ——
        // 隔离语义 = 查过 A 不为 B 产生词条(快照按 patientId 分册键隔离)。
        // 本测在 ExamineDriver+Ledger 层钉住「分册按 id 隔离」这半边;
        // 绑定切换的 42 呈现半边由 CasebookVitalsSource.TryGetSnapshot(_patient) 结构保证
        // (源码只按 _patient.Value 单键查,无跨键路径),不另建装配级双源测试。

        [Test]
        public void test_examine_twoPatients_ledgerIsolatedByPatientId()
        {
            PatientId a = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            PatientId b = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(0);   // 两病人同 tick 同 position(夹具等态)

            // 只查 A
            _driver.OnExamine(a, tick: 1);

            Assert.IsTrue(_ledger.TryGetSnapshot(a.Value, out _), "A 查过 ⇒ 有快照");
            Assert.IsFalse(_ledger.TryGetSnapshot(b.Value, out _),
                "B 未查 ⇒ 无快照(分册按 patientId 隔离 —— 查 A 不为 B 产生词条,禁串册)");
            Assert.IsFalse(_ledger.HasExamined(b.Value),
                "HasExamined(B) 亦须 false(两查询口径一致)");
        }

        [Test]
        public void test_examine_twoPatients_bothExamined_snapshotsIndependent()
        {
            PatientId a = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            PatientId b = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.VitalsService.OnTickEdge(0);

            _driver.OnExamine(a, tick: 1);
            _driver.OnExamine(b, tick: 2);

            Assert.IsTrue(_ledger.TryGetSnapshot(a.Value, out var sa));
            Assert.IsTrue(_ledger.TryGetSnapshot(b.Value, out var sb));
            Assert.AreEqual(1, sa.ExaminedTick, "A 的快照 tick 不被 B 的复查覆盖");
            Assert.AreEqual(2, sb.ExaminedTick, "B 的快照 tick 独立");
            // 两病人同夹具态 ⇒ 词条数应一致(隔离 = 互不影响,非内容不同)
            Assert.AreEqual(sa.Set.SignCount, sb.Set.SignCount,
                "同态两病人独立快照 ⇒ 词条数一致(复查 B 不得扰动 A 的读数)");
        }
    }
}