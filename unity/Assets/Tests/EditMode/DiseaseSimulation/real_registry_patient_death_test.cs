// 真表病人「会不会死」的行为判据(2026-10-23 round28)。
//
// ── 为什么需要本文件 ──────────────────────────────────────────────────────────
// round25 实测结论:PlayMode 全量**测不到**「病人会不会死」。原因是三条叠加:
//   ① PlayMode 里 13 处 `CompositionRoot.Assemble(...)`,12 处不传 `dataProvider`
//      ⇒ 走 caller 表或 fallback 表,**碰不到真表**;
//   ② 唯一走真表的四条(`composition_root_smoke_test` 等)只断言「有 DiseaseOnset /
//      SpawnedCount ≥ 1」,**零条断言病种 id 或 lethality**;
//   ③ F4 死亡支要 `position ≥ DEATH ∧ (t − last_intervention) > TREATMENT_WINDOW(3600)`,
//      而 PlayMode 的 tick 窗口远不够推进到那个点。
// ⇒ 「玩法侧病人会不会死」**从未被任何测试观测过**。本文件补这一跳。
//
// ── 判据形态(三向,但判别力的归属**分开写清**) ──────────────────────────────
// 同一次装配里同时跑三个病人,**共用同一条 tick 时间线**:
//
//   | 病人 | 病种 | lethal | self_limit | peak position | 期望 | 「不死」的归因 |
//   |---|---|---|---|---|---|---|
//   | 甲 | DIS_TETANUS(13)       | true  | false | 1.00 ≥ 0.58 | **必死** | — |
//   | 乙 | DIS_RHEUMATIC_FEVER(8)| false | true  | 0.40 < 0.58 | 不死 | ⚠️ **阈值**,不是门 |
//   | 丙 | DIS_NEURASTHENIA(14)  | false | false | 0.15 < 0.58 | 不死 | ⚠️ **阈值**,不是门 |
//
// ⚠️ **乙丙的「不死」归因于阈值够不着,不是 `LethalFor` / `SelfLimited` 门** ——
//    这一点必须写明,否则本文件会被读成「两个门被验证了」。
//
//    实测(2026-10-23,`disease_registry.json` 全表算 peak = a_peak/scale):
//      · 真表里 **lethal ∧ ¬self_limit** 的行全部 peak ≥ DEATH({2,3,4,5,6,9,10,12,13});
//      · 真表里唯一的 **lethal ∧ self_limit** 行 = `DIS_MALARIA`(id 7),peak = **0.45 < 0.58**
//        ⇒ **真表无法为 `SelfLimited` 门提供「够得着却不死」的对照**。
//    ⇒ **两个门的判别力归 `entity_death_gates_test` 的合成夹具**(那里可以只差一个字段),
//      本文件不重复承担,也不得被读成已经承担。
//
//    本文件真正证的是:**真表 + 真装配 + 真 Step 下,病人确实会死** —— 这正是
//    round25 实测「从未被任何测试观测过」的那一格。
//
// ── 用的是**盘上真产物**,不是合成夹具 ────────────────────────────────────────
// `DataCookedDisk.LoadDiseaseRegistry()` 读 `unity/Assets/DataCooked/disease_registry.cooked.bytes`
// (受 `cooked_drift_all_artifacts_test` 的逐字节漂移门保护)。⇒ 本文件证的是
// 「**真数据**经**真装配**在**真 Step** 下会死」,不是「我构造的夹具会死」。
//
// ── 覆盖不到(如实登记,禁借绿) ──────────────────────────────────────────────
// ① 本文件是 **EditMode**,不是 PlayMode / 真启动序。它证「9 的求值链在真表下判得出
//    死亡」,**不证**「BootRoot 启动后玩家能看见病人死」—— 后者还差两跳:
//    生产触发点把终态写成 `PatientOutcomeChanged`(见 `PatientOutcomeChangedWriter`
//    类头的「装配冲突」),以及表现层消费该终态。两跳均未接线。
// ② `W` / `TREND_EPS` 在真表里仍为 **0**(**数值轮未裁**,非 `OQ-9-1`)⇒ 痊愈支 / 迁延支
//    在本文件里**不求值**。这不影响死亡支(死亡支不读 window)。
//    ⚠️ 2026-10-23 订正:原文把原因写成「数值轮未裁,`OQ-9-1`」,读起来像两者是一件事。
//    `OQ-9-1`(潜伏期起算点)已于 2026-10-23 裁为乙案;`W` 仍 0 是数值轮的事。
// ③ 本文件**不**证 `catalog.bin` 新鲜度 —— EditMode 直读盘上产物,不经 Addressables。
//    player 腿的真表可达性仍是另立的缺口(见 round25 §①)。
//
// 权威来源:
//   GDD disease-simulation.md:1086-1090(死亡支四条件)· :1153-1210(两个实体门)
//   `EntityDeathGates`(`LethalFor` / `SelfLimited` / `IsCombatant` 三门的唯一实现)
//   `StateMachine.DEATH_THRESHOLD` / `TREATMENT_WINDOW`
//   ADR-014 §五(运行期读烘焙产物)· ADR-005(主机唯一 Step)

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>
    /// 真表病人的终态判据 —— 三个病种共用一条时间线,证明「死的会死、不该死的不死」。
    /// </summary>
    public class RealRegistryPatientDeathTest
    {
        // ══════════════════════════════════════════════════════════════════════
        // 病种 id(来自 `assets/data/disease_registry.json`,非本文件发明)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>破伤风 —— `lethal ∧ ¬self_limit`(急性保持型,发病后无对因手柄);peak 1.00 ≥ DEATH。
        /// <para>⚠️ 它的 `incubation = 6000` **本身大于** `TREATMENT_WINDOW = 3600`
        /// ⇒ 它**不能**充当窗口条件的载体(见 <see cref="StabId"/>)。</para></summary>
        private const int TetanusId = 13;

        /// <summary>
        /// 刺伤 —— `lethal ∧ ¬self_limit ∧ incubation = 0`;peak 1.00 ≥ DEATH,**τ ≈ 35 即越阈**。
        /// <para><b>它是「死亡窗口」条件的唯一载体</b>:越阈发生在 tick ≈ 35,
        /// 远早于 `TREATMENT_WINDOW = 3600` ⇒ 若窗口条件失效,它会在 ≈35 就死;
        /// 窗口正常则死在 3601。这个 ≈3566 tick 的差距是窗口判据的**全部判别力来源**。</para>
        /// <para>⚠️ 破伤风不能替代它:`incubation = 6000 > 3600` ⇒ 即使窗口条件整个删掉,
        /// 破伤风的首判死仍 &gt; 3600(是潜伏期挡的,不是窗口挡的)。2026-10-23 探针实测:
        /// 删掉窗口条件后破伤风那条断言**照样绿** —— 正是本节登记的失效形态。</para>
        /// </summary>
        private const int StabId = 2;

        /// <summary>风湿热 —— `¬lethal`;peak 0.40 &lt; DEATH(⚠️ 它的「不死」归因于阈值,不是 `LethalFor` 门)。</summary>
        private const int RheumaticFeverId = 8;

        /// <summary>神经衰弱 —— `¬lethal ∧ ¬self_limit`;peak 0.15 &lt; DEATH。</summary>
        private const int NeurastheniaId = 14;

        /// <summary>
        /// 驱动上界。实测破伤风首次判死 ≈ τ 8030(`incubation 6000` + 上升段越过 0.58),
        /// 取 10000 留 ≈ 2000 tick 余量。
        /// <para>⚠️ 若本常量被调小到 &lt; 8100,甲病人会「未判死」而红 ——
        /// 那是**驱动不够**,不是数据错。红时先看报错里的实际首判 tick。</para>
        /// </summary>
        private const long DriveUntil = 10000;

        // ══════════════════════════════════════════════════════════════════════
        // 盘上真产物(本文件自己不写盘)
        // ══════════════════════════════════════════════════════════════════════

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}(须先跑烘焙菜单)");
            return File.ReadAllBytes(path);
        }

        /// <summary>⚠️ 静态注入缝跨测试残留 ⇒ 每测前后复原(承 `reader_wiring_gate_test` 同款)。</summary>
        [SetUp]
        [TearDown]
        public void ResetInjectionSeam() => KindPriorityTable.Provider = null;

        // ══════════════════════════════════════════════════════════════════════
        // 装配 —— 真表 + 其余真 data-core 产物
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>真装配:`dataProvider` 给**盘上真产物** ⇒ 走 ② 级(真注册表 + 真 W/eps)。</summary>
        /// <remarks>
        /// 其余六份 data-core 产物都是盘上真件(零合成):<c>AssembleCore</c> 在
        /// <c>dataProvider != null</c> 时对它们做**即时** Load,缺任一份装配当场抛。
        /// 不注册的只有 <c>BuildModules</c> / <c>ClinicFurniture</c> / <c>ClinicRoom</c> ——
        /// 它们仅在 <c>structures != null</c> 时 Load,本装配不传 <c>structures</c>。
        /// </remarks>
        private static CompositionRootServices AssembleReal()
            => CompositionRoot.Assemble(
                registry: null,
                dataProvider: new MultiTableProvider()
                    .Add(DataCookedDisk.LoadDiseaseRegistry())
                    .Add(DiseaseActionAxisCookedCodec.Read(
                        ReadCommitted("disease_action_axis.cooked.bytes")))
                    .Add(PrescriptionActionsCookedCodec.Read(
                        ReadCommitted("prescription_actions.cooked.bytes")))
                    .Add(InteractionKindCookedCodec.Read(
                        ReadCommitted("interaction_kinds.cooked.bytes")))
                    .Add(DiagnosisCookedDisk.LoadSigns())
                    .Add(DiagnosisCookedDisk.LoadReadFloor())
                    .Add(DiagnosisCookedDisk.LoadJudgmentLexicon())
                    // 生态区表(story-003 AC-5 接线后组合根**即时** Load)—— 缺它装配当场抛。
                    // 本测不读区划语义,只为装配面完备(同上方六份产物:盘上真件,零合成)。
                    .Add(DataCookedDisk.LoadWorldEcozones()));

        /// <summary>把真表行集按 id 取出来(前提断言用 —— 缺行要**当场说清**而不是下游空转)。</summary>
        private static DiseaseRegistryEntry Row(DiseaseRegistryDataSet table, int diseaseId)
        {
            for (int i = 0; i < table.Rows.Count; i++)
                if (table.Rows[i].DiseaseId == diseaseId) return table.Rows[i];
            Assert.Fail($"真表里没有 disease_id={diseaseId} —— 本判据的前提不成立" +
                        $"(表内 id:{IdsOf(table)})");
            return null;
        }

        private static string IdsOf(DiseaseRegistryDataSet table)
        {
            var ids = new List<string>();
            for (int i = 0; i < table.Rows.Count; i++) ids.Add(table.Rows[i].DiseaseId.ToString());
            return string.Join(",", ids);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 驱动 —— 一条时间线,三个病人
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>一次驱动里每个病人的观测结果。</summary>
        private readonly struct Observation
        {
            /// <summary>首次判出 <see cref="InjuryState.Deceased"/> 的 tick;<c>-1</c> = 全程未判死。</summary>
            public readonly long FirstDeathTick;

            /// <summary>驱动结束时的九态(用来看「不死」究竟落在哪个态)。</summary>
            public readonly InjuryState FinalState;

            public Observation(long firstDeathTick, InjuryState finalState)
            {
                FirstDeathTick = firstDeathTick;
                FinalState = finalState;
            }
        }

        /// <summary>
        /// 一条时间线上同时驱动三个病人 —— 共用 tick ⇒ 三者「活过的时间一样长」。
        /// </summary>
        private static Dictionary<int, Observation> DriveThreePatients(
            CompositionRootServices bag, params int[] diseaseIds)
        {
            DiseaseVitalsService svc = bag.VitalsService;

            var patients = new PatientId[diseaseIds.Length];
            for (int i = 0; i < diseaseIds.Length; i++)
                patients[i] = bag.PatientSpawner.SpawnNext(diseaseIds[i], tick: 0);

            var firstDeath = new long[diseaseIds.Length];
            for (int i = 0; i < firstDeath.Length; i++) firstDeath[i] = -1;

            for (long tick = 0; tick <= DriveUntil; tick++)
            {
                svc.OnTickEdge(tick);
                for (int i = 0; i < patients.Length; i++)
                {
                    if (firstDeath[i] >= 0) continue;      // 首次命中后不再覆写
                    if (svc.GetOutcome(patients[i]) == InjuryState.Deceased)
                        firstDeath[i] = tick;
                }
            }

            var result = new Dictionary<int, Observation>();
            for (int i = 0; i < patients.Length; i++)
                result[diseaseIds[i]] = new Observation(firstDeath[i], svc.GetOutcome(patients[i]));
            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 前提:真表里三行的字段确实是本判据假设的那样
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 三行数据前提 —— 缺了这条,下面「甲死乙丙不死」的红会归因不明
        /// (可能是数据改了,也可能是求值错了)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **本测同时把「真表为什么不能验两个门」这件事钉成判据**:
        /// 逐行断言 `peak ≥ DEATH` ⇔ `lethal ∧ ¬self_limit`。将来若有人给
        /// `DIS_MALARIA` 加高 `a_peak`(使它够得着死线),本测会**转红并点名** ——
        /// 那时就可以把「lethal ∧ self_limit 够得着却不死」的对照加回来了。
        /// 在那之前,加回来只会得到一条**归因错误**的绿。
        /// </remarks>
        [Test]
        public void test_realRegistry_threeRowsCarryExpectedGates()
        {
            DiseaseRegistryDataSet table = DataCookedDisk.LoadDiseaseRegistry();

            DiseaseRegistryEntry tetanus = Row(table, TetanusId);
            Assert.That(tetanus.DiseaseKey, Is.EqualTo("DIS_TETANUS"), "id 13 应是破伤风");
            Assert.That(tetanus.Lethality, Is.GreaterThan(0), "破伤风须 lethal(否则死亡支整支关闭)");
            Assert.That(tetanus.Curve, Is.Not.Null, "破伤风须带曲线(否则走 legacy 简化形)");
            Assert.That(tetanus.Curve.SelfLimit, Is.False,
                "破伤风须 ¬self_limit(否则 `SelfLimited` 门关掉死亡支)");
            Assert.That(EntityDeathGates.LethalFor(ActorClass.Patient, tetanus), Is.True,
                "病人 + 破伤风 ⇒ `LethalFor` 须为真");
            Assert.That(EntityDeathGates.SelfLimited(ActorClass.Patient, tetanus), Is.False,
                "病人 + 破伤风 ⇒ `SelfLimited` 须为假(死亡支才开)");

            DiseaseRegistryEntry rheumatic = Row(table, RheumaticFeverId);
            Assert.That(rheumatic.DiseaseKey, Is.EqualTo("DIS_RHEUMATIC_FEVER"), "id 8 应是风湿热");
            Assert.That(rheumatic.Lethality, Is.EqualTo(0), "风湿热须 ¬lethal");
            Assert.That(EntityDeathGates.LethalFor(ActorClass.Patient, rheumatic), Is.False,
                "病人 + 风湿热 ⇒ `LethalFor` 须为假");

            DiseaseRegistryEntry neurasthenia = Row(table, NeurastheniaId);
            Assert.That(neurasthenia.DiseaseKey, Is.EqualTo("DIS_NEURASTHENIA"), "id 14 应是神经衰弱");
            Assert.That(neurasthenia.Lethality, Is.EqualTo(0), "神经衰弱须 ¬lethal");

            // ── 刺伤:窗口判据的载体,前提必须逐条成立 ──
            DiseaseRegistryEntry stab = Row(table, StabId);
            Assert.That(stab.DiseaseKey, Is.EqualTo("INJ_STAB"), "id 2 应是刺伤");
            Assert.That(stab.Curve, Is.Not.Null);
            Assert.That(stab.Curve.Incubation, Is.EqualTo(0),
                "刺伤须 incubation = 0 —— 否则它的首判死也会被潜伏期推到窗口之后," +
                "失去「窗口判别载体」的资格(这正是破伤风不能当载体的原因)");
            Assert.That(EntityDeathGates.LethalFor(ActorClass.Patient, stab), Is.True,
                "病人 + 刺伤 ⇒ `LethalFor` 须为真");
            Assert.That(EntityDeathGates.SelfLimited(ActorClass.Patient, stab), Is.False,
                "病人 + 刺伤 ⇒ `SelfLimited` 须为假");
            Assert.That(PeakPosition(stab).Raw, Is.GreaterThanOrEqualTo(StateMachine.DEATH_THRESHOLD),
                "刺伤峰值 position 须 ≥ DEATH");
            Assert.That(stab.Curve.TauPeak, Is.LessThan(StateMachine.TREATMENT_WINDOW),
                $"刺伤 tau_peak({stab.Curve.TauPeak})须 < TREATMENT_WINDOW" +
                $"({StateMachine.TREATMENT_WINDOW})—— 保证「越阈早于窗口」这条前提成立");

            // ── 峰值 position 与死线的关系(本文件判别力的**边界**,必须钉死)──
            Assert.That(PeakPosition(tetanus).Raw, Is.GreaterThanOrEqualTo(StateMachine.DEATH_THRESHOLD),
                "破伤风的峰值 position 须 ≥ DEATH —— 否则它「不死」是因为够不着,本文件的阳性判据失去对象");
            Assert.That(PeakPosition(rheumatic).Raw, Is.LessThan(StateMachine.DEATH_THRESHOLD),
                "风湿热的峰值 position 当前 < DEATH ⇒ 它「不死」归因于**阈值**,不是 `LethalFor` 门。\n" +
                "若本行转红(它够得着死线了),说明数值轮改了曲线 —— 那时**应**把本测升级成\n" +
                "「够得着却不死 ⇒ 门有效」的对照,而不是删掉这条断言");
            Assert.That(PeakPosition(neurasthenia).Raw, Is.LessThan(StateMachine.DEATH_THRESHOLD),
                "神经衰弱的峰值 position 当前 < DEATH ⇒ 同样归因于阈值");
        }

        /// <summary>峰值 position = A_peak / SCALE(曲线达峰后的上界;三分支共同上界)。</summary>
        private static Fix PeakPosition(DiseaseRegistryEntry entry)
            => entry.Curve.APeak / entry.Scale;

        // ══════════════════════════════════════════════════════════════════════
        // ② 核心:真表病人**会死**,且只有该死的那个死
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>「玩法侧病人会不会死」的直接观测</b> —— 三个病人共用一条 10000 tick 的时间线。
        /// </summary>
        /// <remarks>
        /// <b>判别力(如实分级,不夸大)</b>:
        /// <list type="bullet">
        /// <item><b>强</b> —— 甲(破伤风)必死:删掉 <c>F4OutcomeEvaluator</c> 的死亡支、
        /// 或把 <c>DEATH_THRESHOLD</c> 调到够不着、或断掉窗口条件,本测都红。
        /// 这条钉的是「**病人确实会死**」这个此前从未被观测的能力。</item>
        /// <item><b>弱(仅防回归,不验门)</b> —— 乙丙不死:它们的 peak &lt; DEATH
        /// (见 <c>test_realRegistry_threeRowsCarryExpectedGates</c>),所以「不死」是**阈值**
        /// 造成的,不是 <c>LethalFor</c> / <c>SelfLimited</c> 门的功劳。
        /// 把两个门改成恒 true/false,本测**不会**红 ⇒ 不得据本测声称门被验证。</item>
        /// </list>
        /// <para>⇒ 两个门的判别力归 <c>entity_death_gates_test</c> 的合成夹具
        /// (那里可以构造「只差 self_limit 一个字段」的两行)。</para>
        /// </remarks>
        [Test]
        public void test_realRegistry_patientDies_onlyForLethalNonSelfLimited()
        {
            CompositionRootServices bag = AssembleReal();
            Dictionary<int, Observation> obs =
                DriveThreePatients(bag, TetanusId, RheumaticFeverId, NeurastheniaId);

            // ── 甲:破伤风必须死(本文件的**强**判据)──
            Observation tetanus = obs[TetanusId];
            Assert.That(tetanus.FirstDeathTick, Is.GreaterThanOrEqualTo(0),
                $"破伤风病人({DriveUntil} tick 内)未判死 —— 实际末态 {tetanus.FinalState}。\n" +
                "0 命中 = 死亡支没接上 / DEATH_THRESHOLD 够不着 / 窗口条件恒不成立。\n" +
                "这是「玩法侧病人会不会死」的唯一直接观测,它红即代表该能力**不存在**");

            // 且必须**晚于**潜伏期 —— 潜伏期内 position = 0,不可能越阈。
            int tetanusIncubation = Row(DataCookedDisk.LoadDiseaseRegistry(), TetanusId).Curve.Incubation;
            Assert.That(tetanus.FirstDeathTick, Is.GreaterThan(tetanusIncubation),
                $"首判死(tick {tetanus.FirstDeathTick})不得早于 incubation({tetanusIncubation})" +
                "—— 潜伏期内 position 恒 0,越阈不可能");

            // ── 乙 / 丙:不死(⚠️ **弱判据** —— 归因于阈值,不是门;见 remarks)──
            Observation rheumatic = obs[RheumaticFeverId];
            Assert.That(rheumatic.FirstDeathTick, Is.EqualTo(-1),
                $"风湿热病人于 tick {rheumatic.FirstDeathTick} 被判死 —— 它的 peak position(0.40)" +
                "够不到 DEATH(0.58),判死说明**别的东西**越过了死线(求值量纲错 / 聚合串味)");

            Observation neurasthenia = obs[NeurastheniaId];
            Assert.That(neurasthenia.FirstDeathTick, Is.EqualTo(-1),
                $"神经衰弱病人于 tick {neurasthenia.FirstDeathTick} 被判死 —— 它的 peak position(0.15)" +
                "够不到 DEATH(0.58),同上");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 死亡窗口:越阈 ≠ 即死
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>窗口条件的唯一判别载体</b> —— 刺伤(`incubation = 0`,τ≈35 越阈)。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么必须用刺伤而不是破伤风</b>:破伤风的 `incubation = 6000`
        /// 本身就大于 `TREATMENT_WINDOW = 3600` ⇒ 「首判死 &gt; 3600」对它是**恒真**的,
        /// 与窗口条件无关。2026-10-23 探针 F 实测确认:删掉窗口条件后,
        /// 以破伤风为载体的那条断言**照样绿**(假判别力)。</para>
        ///
        /// <para><b>本测的三段判据</b>:</para>
        /// <list type="number">
        /// <item>越阈时刻 ≈ 35(&lt; 3600)⇒ 窗口若失效,死亡会在此刻发生;</item>
        /// <item>首判死 &gt; 3600 ⇒ 窗口确实拦住了「越阈即死」;</item>
        /// <item>首判死 ≤ 3600 + 余量 ⇒ 窗口一到就死,不是「永远不死」。</item>
        /// </list>
        /// <para>三条合取 ⇒ 删窗口条件必红(第 2 条)、窗口条件恒不成立必红(第 3 条)。</para>
        /// </remarks>
        [Test]
        public void test_realRegistry_deathWaitsForTreatmentWindow()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseVitalsService svc = bag.VitalsService;
            PatientId patient = bag.PatientSpawner.SpawnNext(StabId, tick: 0);

            long firstDeath = -1;
            long firstOverDeathLine = -1;

            for (long tick = 0; tick <= DriveUntil; tick++)
            {
                svc.OnTickEdge(tick);

                // 「越阈」= 九态已达昏迷及以上(死亡支的两门对刺伤恒开 ⇒ 越阈后先落昏迷)
                if (firstOverDeathLine < 0)
                {
                    InjuryState s = svc.GetOutcome(patient);
                    if (s == InjuryState.Coma || s == InjuryState.Deceased)
                        firstOverDeathLine = tick;
                }

                if (firstDeath < 0 && svc.GetOutcome(patient) == InjuryState.Deceased)
                    firstDeath = tick;
            }

            Assert.That(firstOverDeathLine, Is.GreaterThanOrEqualTo(0),
                "刺伤病人在驱动窗口内从未越过死线 —— 前提不成立(曲线或阈值改了)");

            // ① 越阈必须**早于**窗口 —— 否则本测无法区分「窗口拦住的」与「本来就晚」
            Assert.That(firstOverDeathLine, Is.LessThan(StateMachine.TREATMENT_WINDOW),
                $"刺伤越阈于 tick {firstOverDeathLine},不早于 TREATMENT_WINDOW" +
                $"({StateMachine.TREATMENT_WINDOW})—— 本测失去判别力(无法证明窗口拦住了什么)");

            // ② 首判死必须**晚于**窗口 —— 这条是窗口条件的直接判据
            Assert.That(firstDeath, Is.GreaterThan(StateMachine.TREATMENT_WINDOW),
                $"刺伤首判死于 tick {firstDeath},不晚于 TREATMENT_WINDOW" +
                $"({StateMachine.TREATMENT_WINDOW})—— 但它越阈在 tick {firstOverDeathLine}。" +
                "⇒ 窗口条件失效:病人越阈即死(违铁律一「不能一越线就死」)");

            // ③ 且不能「永远不死」—— 窗口一到就该死
            Assert.That(firstDeath, Is.LessThanOrEqualTo(StateMachine.TREATMENT_WINDOW + 60),
                $"刺伤首判死于 tick {firstDeath},晚于窗口 + 60({StateMachine.TREATMENT_WINDOW + 60})" +
                "—— 窗口过了却迟迟不死,说明窗口条件恒不成立(死亡支被永久挡住)");
        }

        /// <summary>
        /// 死亡是**吸收态**:一旦判死,后续 tick 不得回到活着的态。
        /// <para>F4 是**纯函数**(逐 tick 由 position 重算),它本身不保证吸收性 ——
        /// 保证来自「曲线单调」。本测钉住该性质在真表下确实成立:
        /// 若某病种的 position 在越阈后回落,病人会「死了又活」,而流里没有任何东西会红。</para>
        /// </summary>
        [Test]
        public void test_realRegistry_deathIsAbsorbing_neverReverts()
        {
            CompositionRootServices bag = AssembleReal();
            DiseaseVitalsService svc = bag.VitalsService;
            PatientId patient = bag.PatientSpawner.SpawnNext(TetanusId, tick: 0);

            long firstDeath = -1;
            for (long tick = 0; tick <= DriveUntil; tick++)
            {
                svc.OnTickEdge(tick);
                InjuryState state = svc.GetOutcome(patient);

                if (firstDeath < 0)
                {
                    if (state == InjuryState.Deceased) firstDeath = tick;
                    continue;
                }

                Assert.That(state, Is.EqualTo(InjuryState.Deceased),
                    $"病人于 tick {firstDeath} 判死后,在 tick {tick} 回到 {state} —— " +
                    "F4 是纯函数,吸收性由曲线单调保证;回退 = 曲线在真表下非单调,或求值读了可变状态");
            }

            Assert.That(firstDeath, Is.GreaterThanOrEqualTo(0),
                $"破伤风病人在 {DriveUntil} tick 内从未判死 —— 吸收性判据无对象(前提不成立)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 按类型分派的 <see cref="IDataProvider"/> 夹具。
        /// <para>⚠️ 缺表 ⇒ **抛**,不返回 default —— 「装配问到了没准备的表」本身就是接线错的
        /// 信号,静默返回 <c>default</c> 会把它变成下游的空表崩溃(与本文件判据无关的噪声)。</para>
        /// </summary>
        private sealed class MultiTableProvider : IDataProvider
        {
            private readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();

            public MultiTableProvider Add<TDataSet>(in TDataSet dataSet) where TDataSet : struct
            {
                _byType[typeof(TDataSet)] = dataSet;
                return this;
            }

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                if (_byType.TryGetValue(typeof(TDataSet), out object boxed))
                    return (TDataSet)boxed;
                throw new InvalidOperationException(
                    $"MultiTableProvider 无 {typeof(TDataSet).Name} —— 本测不该问到它(真问 = 接线错)");
            }
        }
    }
}
