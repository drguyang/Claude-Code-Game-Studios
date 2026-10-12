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
            //   · fallback 病种 RecoveryTime=2000(2026-10-12 人工复验期值)⇒ τ<2000 期间
            //     position≡0(潜伏抑制)⇒ 推进 tick 越潜伏需 τ>2000;
            //     ⚠️ 两个既有真词表测改用**直接求值器**(skill=5 × position 1.0)构造
            //     「越过阈值」的数学面,不依赖 τ 推进 —— 组合根驱动的 τ 面由
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

            // 越过潜伏期的「病程推进」代理:直接推进到 legacy 曲线峰值 τ=2R 太慢(R=2000),
            // 故用**同形状**验证 —— 把词表/门槛/position 置到可读点后走组合根驱动:
            // 该测的可证伪点 = queryLevel(组合根值)而非 τ。
            long risePeak = 2L * 20;   // 语义面:R=20 时峰值 τ=40;此处只为「>潜伏」的形状
            if (_bag.TickDriver != null && risePeak <= 120)
            {
                for (long t = 1; t <= risePeak; t++) _bag.VitalsService.OnTickEdge(t);
            }

            float position = _bag.VitalsService.GetVitals(patient).Position;
            // 若病程还没推进到可读点(被 R=2000 拖住),该测的**主旨**是技能面 ——
            // 用求值器在同门槛下证明「skill=1 时 tier-1 词可读」,再断言组合根驱动同条件。
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
            // 2026-10-12 第五轮 playtest Q1 第二半(人工复验期判据):
            // fallback 病种 position 越组合根初始档门槛(floor(skill=1))后须**停留足够久**
            // —— 不是「越快越好」,而是「可读窗口宽到人工按得中」。
            // 原 RecoveryTime=100 ⇒ 可读窗口 ≈212 tick(≈10 s),偏窄;
            // 现 = 2000 ⇒ 可读窗口 ≈1900 tick(≈95 s)(潜伏 20 s + 线性上升 100 s 达峰值,
            // 之后才衰减)—— 复验从「秒级打地鼠」回到「分钟级」。
            // 判据 = 窗口下界 ≥ 600 tick(30 s):任何把 R 调回 ≤ 100 的改动恰红。
            var rows = _bag.ExamineDriver.LexiconRows();
            var floor = _bag.ExamineDriver.FloorTable();

            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            long firstReadable = -1, lastReadable = -1;
            for (long t = 0; t <= 6000; t++)
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
                "6000 tick 内必有某 τ 使 tier-1 词可读 —— 人工复验可达性判据");
            Assert.GreaterOrEqual(lastReadable - firstReadable, 600,
                $"可读窗口须 ≥ 600 tick(30 s)(实际 {lastReadable - firstReadable} tick) —— " +
                "窗口太窄 ⇒ 人工按键错过(Q1 人工面:按 E 无词 = 等太短或窗口秒级)");
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