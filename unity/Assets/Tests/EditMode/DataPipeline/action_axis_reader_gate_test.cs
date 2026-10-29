// 权威来源:ADR-014 §二/§五(运行期读烘焙产物)· E-13(失败 = 启动期硬失败)
//          · `production/qa/evidence/impl-data-core-readers-2026-10-20.md` §2.4(乙档登记)
//
// ⚠️ **本门补的是乙档第一份(`disease_action_axis`)的「装载 → 消费」那一跳**。
//    上一轮把 6 份产物接进装载面后,把 `disease_action_axis` 归为**乙档**:
//    「缺的不是接线而是调用方」。本轮勘察证实该判断**只对了一半** ——
//
//      | 段             | 编辑期                                                     | 运行期(接线前) |
//      |----------------|------------------------------------------------------------|----------------|
//      | `actions`      | **已消费**(`PrescriptionActionIdRegistry` 读 `owner=="11"` 作 DC-2 闭集) | **零读点** |
//      | `treatable_by` | 烘焙期校验                                                  | **零读点**     |
//
//    `actions` 的 `owner=="10"` 段消费者本该是 DC-4(`EmergencyActionSchema`),
//    但该方法注释自陈「真源接线待后续」,实测**两个方法在生产侧都是零调用**
//    ⇒ DC-4 长期是 `Enum.IsDefined` 的影子。本轮把它接上。
//
//    `treatable_by` 段**仍未接** —— 其消费者是 F-11.5 的 `GateHit`,
//    属 11 二轮评审 BL-3 的阻断项,不在本门面内(见文件末 §未接边界)。
//
// ⚠️ **判据口径(必须看清,否则会误读成行为变化)**:
//    产物 `owner=="10"` 段 = `{0, 1}`,恰等于 `EmergencyAction` 枚举成员
//    ⇒ 接线前后**行为逐值不变**。本门证的是「**源真的换成了产物**」,
//    **不是**「行为变了」。禁把前者写成后者(借绿家族)。
//    故「换源」半边**只能靠合成夹具证** —— 真产物测恒绿。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// <b>处置轴读者门</b> —— `disease_action_axis` 的 `actions` 段必须真被 DC-4 消费。
    /// <para>⚠️ 「换源」与「对照」分开记分:前者靠**合成夹具**,后者靠**真产物**。
    /// 真产物的值恰等于枚举 ⇒ 用真产物测「源换了」是恒绿假门。</para>
    /// </summary>
    [TestFixture]
    public class ActionAxisReaderGateTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        /// <summary>⚠️ 静态注入缝**跨测试残留** ⇒ 每测先复原、每测后复原。
        /// <para><b>为什么必须有(2026-10-20 实测)</b>:D 组两条测经
        /// <c>CompositionRoot.Assemble(dataProvider: …)</c> 装配,而那会把
        /// <c>KindPriorityTable.Provider</c> 设成本夹具的 provider 上的委托
        /// (<c>CompositionRoot:348</c>)。本夹具的 provider **刻意不含** `InteractionKindDataSet`
        /// ⇒ 残留到下一测时,凡走到 <c>InteractionKindTableValidator.ValidatePriorityTotalOrder</c>
        /// 的路径(经 <c>KindPriorityTable.PriorityOf</c> → Provider)会抛
        /// 「MultiTableProvider 无 InteractionKindDataSet」——
        /// **实测**:未加本复位时 `cooked_drift_all_artifacts_test.drift[interaction_kinds]` 因此转红。
        /// 与 <c>ReaderWiringGateTest.ResetInjectionSeam</c> 同款(那里是本缝的既有先例)。</para>
        /// <para>置 <c>null</c> 即让按 Provider 身份缓存的表失效 ⇒ 无需额外的 Reset API。</para></summary>
        [SetUp]
        [TearDown]
        public void ResetInjectionSeam() => KindPriorityTable.Provider = null;

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static DiseaseActionAxisDataSet CommittedAxis()
        {
            string path = Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                "disease_action_axis.cooked.bytes");
            Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}");
            return DiseaseActionAxisCookedCodec.Read(File.ReadAllBytes(path));
        }

        /// <summary>从产物取 `owner=="10"` 段的 id 集。
        /// <para>⚠️ 与组合根同一算法但**刻意各写一遍** —— 两份独立实现 = 交叉校验;
        /// 抽成公共方法会让「两处都漂移」不可见。</para></summary>
        private static HashSet<int> Owner10Of(DiseaseActionAxisDataSet ds)
        {
            var set = new HashSet<int>();
            foreach (ActionAxisCookedRow row in ds.Actions)
                if (row.Owner == "10")
                    set.Add(row.ActionId);
            return set;
        }

        // ══════════════════════════════════════════════════════════════════════
        // A. 真产物:对照 + 完整性
        // ══════════════════════════════════════════════════════════════════════

        /// <summary><b>对照</b>:产物的 `owner=="10"` 段 ⟷ `EmergencyAction` 枚举**逐值相等**。
        /// <para>本测锚住「影子当前碰巧是对的」这一事实 —— 它正是「换源不改行为」的依据。
        /// 一旦产物新增一个 `owner=="10"` 的 id,本测**转红**:那正是影子会错误拒绝的时刻,
        /// 而那时轴门(已接线)**不会**跟着错。</para>
        /// <para>⚠️ 本测绿**不**证明接线有效;接线由 <see cref="test_axis_syntheticAxis_rejectsActionOutsideAxis"/> 独立记分。</para></summary>
        [Test]
        public void test_axis_artifactOwner10Set_matchesEmergencyActionEnum()
        {
            HashSet<int> axis = Owner10Of(CommittedAxis());

            var enumIds = new HashSet<int>();
            foreach (EmergencyAction a in (EmergencyAction[])Enum.GetValues(typeof(EmergencyAction)))
                enumIds.Add((int)a);

            CollectionAssert.AreEquivalent(enumIds, axis,
                "产物 owner==\"10\" 段 ⟷ EmergencyAction 枚举 须逐值相等 —— " +
                "不等 ⇒ 要么产物被换过,要么枚举漏了轴上的动作(后者正是影子会错误拒绝的形态)");
        }

        /// <summary>产物完整性:`actions` 段 id **两两互异**(处置轴是 id 的唯一 master),
        /// 且 `owner` 只取 `{"10","11"}`(闭集 —— 新 owner 出现须先改本测,防静默扩域)。</summary>
        [Test]
        public void test_axis_artifactIdsUniqueAndOwnerClosed()
        {
            DiseaseActionAxisDataSet ds = CommittedAxis();

            var ids = new HashSet<int>();
            foreach (ActionAxisCookedRow row in ds.Actions)
            {
                Assert.IsTrue(ids.Add(row.ActionId),
                    $"处置轴 id {row.ActionId} 重复 —— 同一 id 两条记录会让闭集归属取决于行序(不可见)");
                Assert.That(row.Owner, Is.EqualTo("10").Or.EqualTo("11"),
                    $"action_id={row.ActionId} 的 owner=\"{row.Owner}\" ∉ {{\"10\",\"11\"}} —— " +
                    "新 owner 出现时须同步本测,不得静默扩域");
            }
            Assert.Greater(ids.Count, 0, "处置轴为空 ⇒ 拒以空集冒充绿");
        }

        // ══════════════════════════════════════════════════════════════════════
        // B. 换源(承重):合成轴
        // ══════════════════════════════════════════════════════════════════════

        /// <summary><b>换源(承重·拒)</b>:注入**不含 0 也不含 1** 的合成轴 ⇒ 入口必须拒。
        /// <para>⚠️ 这是**唯一**能区分「读轴」与「读枚举影子」的判据:若仍走影子,
        /// `action=0` 合法 ⇒ 双事件照常入流 ⇒ 本测红。</para>
        /// <para>强度:不仅断言抛,还断言**流里零事件** —— 门须在 Append **之前**
        /// (先拒再算,不留半途的流)。</para></summary>
        [Test]
        public void test_axis_syntheticAxis_rejectsActionOutsideAxis()
        {
            var pool = new InMemoryBlobPool();
            var sink = new CapturingSink();
            var processor = new HostEmergencyProcessor(sink, new FakeIdAuthority(),
                new PayloadEncoder(pool), actionAxis: new HashSet<int> { 7, 8 });

            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => processor.Process(Attempt(action: 0), Action(actionId: 0), Ctx(),
                                        patientId: new PatientId(1), tick: 1, cause: 0,
                                        analogChannelAbsent: false));
            StringAssert.Contains("DC-4", ex.Message, "失败报文须点名 DC-4(可诊断)");
            Assert.AreEqual(0, sink.Count,
                "门须在 Append **之前** —— 流里有事件 ⇒ 拒得太晚(半程执行)");
        }

        /// <summary><b>换源(承重·放)</b>:注入只含 `{0}` 的轴 ⇒ `action=0` 放行且双事件入流。
        /// <para>⚠️ 与上一条合起来才构成「读轴」的充分证据:只证「拒」不证「放」,
        /// 一个恒抛的实现也能过。</para></summary>
        [Test]
        public void test_axis_syntheticAxis_acceptsActionInsideAxis()
        {
            var pool = new InMemoryBlobPool();
            var sink = new CapturingSink();
            var processor = new HostEmergencyProcessor(sink, new FakeIdAuthority(),
                new PayloadEncoder(pool), actionAxis: new HashSet<int> { 0 });

            processor.Process(Attempt(action: 0), Action(actionId: 0), Ctx(),
                              patientId: new PatientId(1), tick: 1, cause: 0,
                              analogChannelAbsent: false);
            Assert.AreEqual(2, sink.Count,
                "轴内 action 须放行:EmergencyAttempt + EmergencyTreatmentApplied 双事件入流");
        }

        /// <summary><b>门覆盖载荷侧而非只覆盖表行</b>:`attempt.Action` 与 `action.ActionId`
        /// 落**不同**去处(前者进 `EmergencyAttempt` 载荷,后者进 `treatmentId`)——
        /// 只查后者会漏掉真正进流的那个。
        /// <para>构造:轴 `{0}`、`action.ActionId = 0`(合法)但 `attempt.Action = 99`(轴外)⇒ 必须拒。</para></summary>
        [Test]
        public void test_axis_gatesPayloadActionNotJustRowActionId()
        {
            var pool = new InMemoryBlobPool();
            var sink = new CapturingSink();
            var processor = new HostEmergencyProcessor(sink, new FakeIdAuthority(),
                new PayloadEncoder(pool), actionAxis: new HashSet<int> { 0 });

            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => processor.Process(Attempt(action: 99), Action(actionId: 0), Ctx(),
                                        patientId: new PatientId(1), tick: 1, cause: 0,
                                        analogChannelAbsent: false));
            StringAssert.Contains("载荷", ex.Message,
                "报文须指明是**载荷**侧越界(与表行侧可区分 —— 否则无法定位是哪个字段错了)");
            Assert.AreEqual(0, sink.Count, "拒得太晚:流里已有事件");
        }

        // ══════════════════════════════════════════════════════════════════════
        // C. 负向:空集 / 未注入(两者刻意不同)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary><b>负向</b>:注入**空集** ⇒ 入口即拒(`InvalidOperationException`)。
        /// <para>⚠️ 「空集 ≠ 未注入」是刻意的:<c>null</c> = 装配层没装(EditMode 15+ 既有调用方)
        /// ⇒ 走枚举影子;**空集** = 装了但产物里没有 `owner=="10"` 条目 ⇒ **装配错误**。
        /// 混为一谈的话,产物损坏会静默退化成「全部动作都非法」或「全部合法」。</para></summary>
        [Test]
        public void test_axis_emptyAxis_throwsDistinctFromNotInjected()
        {
            var pool = new InMemoryBlobPool();
            var sink = new CapturingSink();
            var processor = new HostEmergencyProcessor(sink, new FakeIdAuthority(),
                new PayloadEncoder(pool), actionAxis: new HashSet<int>());

            var ex = Assert.Throws<InvalidOperationException>(
                () => processor.Process(Attempt(action: 0), Action(actionId: 0), Ctx(),
                                        patientId: new PatientId(1), tick: 1, cause: 0,
                                        analogChannelAbsent: false));
            StringAssert.Contains("空集", ex.Message, "报文须点名「空集」(与「未注入」可区分)");
        }

        /// <summary><b>回退</b>:未注入(`null`)⇒ 走枚举影子,**行为与接线前逐位相同**。
        /// <para>⚠️ 本测是「零行为变化」的直接判据:15+ 个既有装配调用方都不传该参,
        /// 回退路径若变了,那些调用方全线受影响。</para></summary>
        [Test]
        public void test_axis_notInjected_fallsBackToEnumShadow()
        {
            var pool = new InMemoryBlobPool();
            var sink = new CapturingSink();
            var processor = new HostEmergencyProcessor(sink, new FakeIdAuthority(),
                new PayloadEncoder(pool));   // 不传 actionAxis

            processor.Process(Attempt(action: 0), Action(actionId: 0), Ctx(),
                              patientId: new PatientId(1), tick: 1, cause: 0,
                              analogChannelAbsent: false);
            Assert.AreEqual(2, sink.Count, "未注入 ⇒ 回退路径须照常放行(零行为变化)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // D. 接线(承重):组合根真注入
        // ══════════════════════════════════════════════════════════════════════

        /// <summary><b>接线(对照半边)</b>:真产物经组合根装配后,轴内 `action=0` 放行。
        /// <para>⚠️ 本条**单独看没有判别力** —— 组合根不注入(回退影子)时它也绿
        /// (证伪轮 F4 实测)。它的价值是「真产物这条路走得通」,
        /// 判别力在 <see cref="test_axis_assemblyInjectsSyntheticAxis_overridesShadow"/>。</para></summary>
        [Test]
        public void test_axis_assemblyWithArtifact_acceptsActionInAxis()
        {
            CompositionRootServices services = CompositionRoot.Assemble(
                dataProvider: BuildProvider());

            PatientId patient = services.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            services.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            EmergencyManualResult ok = services.EmergencyManualDriver.TryManual(
                patient, method: 0, actorId: 7, tick: 0);
            Assert.IsTrue(ok.Applied, "真产物轴内 action=0 须放行(装配这条路径走得通)");
        }

        /// <summary><b>接线(承重半边)</b>:用**合成轴 `{99}`**(不含 0)经组合根注入 ⇒
        /// 端到端跑 `action=0` 必须**被拒**。
        /// <para>⚠️ <b>为什么必须用合成轴</b>:真产物的轴 = `{0,1}` 恰等于枚举 ——
        /// 「注入了轴」与「没注入(走影子)」在 `action=0` 上**行为相同**,
        /// 用真产物测这条是**恒绿假门**(证伪轮 F4 实测:删掉组合根那一行,8 条测全绿)。
        /// 合成轴 `{99}` 把两者分开:注入 ⇒ 0 ∉ {99} ⇒ 拒;没注入 ⇒ 影子认 0 ⇒ 放行。</para>
        /// <para>这是「组合根那一行真的在」的**唯一**判据。</para></summary>
        [Test]
        public void test_axis_assemblyInjectsSyntheticAxis_overridesShadow()
        {
            var synthetic = new DiseaseActionAxisDataSet(
                actions: new List<ActionAxisCookedRow>
                {
                    new ActionAxisCookedRow(99, "SYNTHETIC_ONLY", "10"),
                },
                treatableBy: new List<TreatableByCookedRow>(),
                configVersion: 0u);

            CompositionRootServices services = CompositionRoot.Assemble(
                dataProvider: BuildProvider(true, synthetic));

            PatientId patient = services.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            services.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            // ⚠️ 只数**急救**事件 —— `SpawnNext` 本身会写 `DiseaseOnset`(病人出现即带病种),
            //    数全流会把它算进来 ⇒ 断言恒假(首版实测:Expected 0 But was 1)。
            int emergencyBefore = CountEmergencyEvents(services);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => services.EmergencyManualDriver.TryManual(patient, method: 0, actorId: 7, tick: 0),
                "组合根须把合成轴 {99} 注入 Processor ⇒ action=0 被拒;" +
                "放行 ⇒ 组合根**没**注入轴(走的还是 Enum.IsDefined 影子)");
            StringAssert.Contains("DC-4", ex.Message, "失败报文须点名 DC-4(可诊断)");
            Assert.AreEqual(emergencyBefore, CountEmergencyEvents(services),
                "门须在 Append 之前 ⇒ 急救事件数不变(零新增)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════════

        private static EmergencyActionRow Action(int actionId = 0)
            => new EmergencyActionRow
            {
                ActionId = actionId,
                Polarity = 1,
                BasePotency = 100000L,
                MagThreshold = 400,
                MinEdges = 2,
                MinHoldTicks = 50,
                HalfLifeTicks = 120,
                DurationTicks = 200,
                ResultMul = EmergencyActionSchema.GetResultMulTiers(),
            };

        private static EmergencyAttemptPayload Attempt(int action = 0, int actorId = 7)
            => new EmergencyAttemptPayload(
                action: action, holdTicks: 60, edges: 3, magPeak: 800, magLast: 750,
                method: 0, actorId: actorId, edgeTicks: new[] { 10, 30, 60 });

        private static JudgeContext Ctx() => new JudgeContext
        {
            Level = 1,
            MagThresholdEffective = 50,
        };

        /// <summary>流里的急救事件数(`EmergencyAttempt` + `EmergencyTreatmentApplied`)。
        /// <para>⚠️ 不数全流 —— 病人出现 / 开案等无关事件会把计数垫高。</para></summary>
        private static int CountEmergencyEvents(CompositionRootServices services)
        {
            int n = 0;
            foreach (SimEvent e in services.Stream.Events)
                if (e.Kind == EventKind.EmergencyAttempt ||
                    e.Kind == EventKind.EmergencyTreatmentApplied)
                    n++;
            return n;
        }

        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _next = 1;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class CapturingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public int Count => Events.Count;
            public void Append(in SimEvent e) => Events.Add(e);
        }

        /// <summary>装配面最小 provider:诊断三表 + 待测的处置轴。
        /// <para>⚠️ 组合根在 `dataProvider != null` 时会**即时** Load 若干表 ⇒ 缺哪个当场抛
        /// (真问 = 接线错,不是「provider 不完整」)。</para></summary>
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

        /// <param name="syntheticAxis">非 null ⇒ 用它替换盘上真产物(换源判据用)。</param>
        private static MultiTableProvider BuildProvider(
            bool useSynthetic = false, DiseaseActionAxisDataSet syntheticAxis = default)
            => new MultiTableProvider()
                .Add(DiagnosisCookedDisk.LoadSigns())
                .Add(DiagnosisCookedDisk.LoadReadFloor())
                .Add(DiagnosisCookedDisk.LoadJudgmentLexicon())
                .Add(useSynthetic ? syntheticAxis : CommittedAxis())
                // ⚠️ 处方表**必须**在场:组合根对它做的是**即时** Load(不像交互表是惰性 Provider)
                //    ⇒ 缺它装配当场抛。本测只用它的存在性,不读它的内容。
                .Add(PrescriptionActionsCookedCodec.Read(File.ReadAllBytes(
                    Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                        "prescription_actions.cooked.bytes"))))
                // ⚠️ 9 病种注册表**必须**在场(2026-10-22 B1 接真起):`AssembleCore` 在
                //    `dataProvider != null` 时对它做**即时** Load ⇒ 缺它装配当场抛。
                //    走 `DataCookedDisk` = 盘上真产物 + 同一 codec(零 Fake)。
                //    ⚠️ 本测不跑 tick、`SpawnNext` 不查注册表 ⇒ 真表 id 从 2 起不影响本测。
                .Add(DataCookedDisk.LoadDiseaseRegistry())
                // ⚠️ 生态区表(story-003 AC-5 接线后组合根**即时** Load)⇒ 缺它装配当场抛。
                //    本测不读区划语义,只为装配面完备。
                .Add(DataCookedDisk.LoadWorldEcozones())
                // 13 调参常量表(TD-audio-032 ① 接线后组合根**即时** Load)—— 缺它装配当场抛。
                .Add(DataCookedDisk.LoadAiPatient());
    }
}
