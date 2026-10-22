// 权威来源:ADR-014 §二/§五(运行期读烘焙产物)· E-13(失败 = 启动期硬失败)
//          · `production/qa/evidence/impl-data-core-wiring-b-2026-10-20.md` §7.1(本门要闭合的边界)
//
// ⚠️ **本文件补的是「装载 → 消费」之间那道缝的门**。
//    2026-10-20 的两轮把 6 份产物接进了**装载面**,并如实登记了一条边界:
//    「6 个数据集在运行期**仍然零读者** —— 做的是『可装载』,不是『已被使用』」。
//    本门是那条边界的**收口**:对其中**真有生产读点**的两份
//    (`interaction_kinds` → `KindPriorityTable`;`prescription_actions` → `PrescribeRequestDriver`)
//    把「读者」接上,并把「源真的换了」变成可红的机器。
//
// ⚠️ **判据口径(必须看清,否则会误读成行为变化)**:
//    两份产物的**数据恰好与既有硬编码字面量逐值相同**(实测:`experimental` 见下两条对照断言)
//    ⇒ 接线前后**行为逐值不变**。本门证的是「**源真的换成了产物**」,
//    **不是**「行为变了」。禁把前者写成后者(借绿家族)。
//
// ⚠️ **本门不做的事**:不证「4 / 11 的 AC 已满足」。`InteractionSelector` 在生产侧
//    **仍零构造点**(实测:非 Tests 面只有 `ModalGate` 的形参)⇒ 本轮接的是**契约层读点**
//    (选择器的第二键查表 + 4-DC-3 校验器),不是「运行期已有实例在查」。
//    选择器的生产装配归 4 的装配轮。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// <b>读方接线门</b> —— 两份「有真生产读点」的产物,其消费者必须真从产物取数。
    /// <para>⚠️ 每份产物的**换源**与**对照**分开记分(禁合并成一条绿):
    /// 「源真的换了」与「产物与字面量一致」是两件事,前者靠**合成夹具**证,后者靠**真产物**证。</para>
    /// </summary>
    [TestFixture]
    public class ReaderWiringGateTest
    {
        private const int PlayerId = 9301;

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}");
            return File.ReadAllBytes(path);
        }

        private static InteractionKindDataSet CommittedInteractionKinds()
            => InteractionKindCookedCodec.Read(ReadCommitted("interaction_kinds.cooked.bytes"));

        private static PrescriptionActionsDataSet CommittedPrescriptionActions()
            => PrescriptionActionsCookedCodec.Read(ReadCommitted("prescription_actions.cooked.bytes"));

        /// <summary>9 的处置轴产物(`disease_action_axis`)。
        /// <para>⚠️ 本门**不测**它(其读者门 = `ActionAxisReaderGateTest`)。此处只为让
        /// <c>CompositionRoot.Assemble(dataProvider: …)</c> **装配得起来** ——
        /// 组合根对它是**即时** Load(DC-4 闭集注入),缺它装配当场抛;
        /// 且喂的是**盘上真产物**而非合成值:本门用的是真装配路径,不该塞假输入。</para></summary>
        private static DiseaseActionAxisDataSet CommittedDiseaseActionAxis()
            => DiseaseActionAxisCookedCodec.Read(ReadCommitted("disease_action_axis.cooked.bytes"));

        /// <summary>⚠️ 静态注入缝跨测试残留 ⇒ 每测先复原(生产由 BootRoot/PlayMode 复位)，
        /// 每测后同样复原。置 null 即让按 Provider 身份缓存的表失效。</summary>
        [SetUp]
        [TearDown]
        public void ResetInjectionSeam() => KindPriorityTable.Provider = null;

        // ══════════════════════════════════════════════════════════════════════
        // A. interaction_kinds → KindPriorityTable
        // ══════════════════════════════════════════════════════════════════════

        /// <summary><b>对照</b>:真产物行 ⟷ 回退字面量(`FallbackRows`)**逐项相等**。
        /// <para>这正是「保留硬编码字面量」的用处 —— 它是一份**独立于产物的 oracle**:
        /// 产物被烘成另一套优先级 ⇒ 本测红。删掉字面量 = 少一条判别力来源。</para>
        /// <para>⚠️ 判据**当前为真**(两者同一套值)⇒ 单看本测绿不构成「接线有用」;
        /// 接线本身由 <see cref="test_readers_interactionKinds_providerInjection_swapsTable"/> 独立记分。</para></summary>
        [Test]
        public void test_readers_interactionKinds_artifactRows_matchFallbackLiteral()
        {
            InteractionKindDataSet ds = CommittedInteractionKinds();

            Assert.AreEqual(KindPriorityTable.FallbackRows.Count, ds.Rows.Count,
                "产物行数须与回退表项数相等(否则下面逐项对拍会漏项)");

            foreach (KindContractRow row in ds.Rows)
            {
                Assert.IsTrue(KindPriorityTable.FallbackRows.TryGetValue(row.Kind, out int expected),
                    $"回退表缺 kind {row.Kind} —— 两者覆盖面不同");
                Assert.AreEqual(expected, row.KindPriority,
                    $"kind {row.Kind}:产物行 kind_priority={row.KindPriority} ≠ 回退字面量={expected}" +
                    " ⇒ 产物被烘成了另一套顺序(oracle = GDD §F-4.1b 演示序)");
            }
        }

        /// <summary>产物行**两两互异** + **覆盖枚举全集**(4-DC-2 / 4-DC-3 的数据半边)。</summary>
        [Test]
        public void test_readers_interactionKinds_artifactRows_totalOrderAndCoverEnum()
        {
            InteractionKindDataSet ds = CommittedInteractionKinds();

            var kinds = new HashSet<InteractableKind>();
            var priorities = new HashSet<int>();
            foreach (KindContractRow row in ds.Rows)
            {
                Assert.IsTrue(kinds.Add(row.Kind), $"产物行 kind {row.Kind} 重复");
                Assert.IsTrue(priorities.Add(row.KindPriority),
                    $"产物行 kind_priority={row.KindPriority} 平局 ⇒ 全序退化到 StableId 决胜(4-DC-3)");
            }

            foreach (InteractableKind k in (InteractableKind[])Enum.GetValues(typeof(InteractableKind)))
                Assert.IsTrue(kinds.Contains(k), $"产物缺 kind {k}(4-DC-2 闭集)");
        }

        /// <summary><b>换源</b>:注入一个**优先级刻意不同**的合成行集 ⇒ 查表结果必须随之改变;
        /// 置回 null ⇒ 必须回到回退表。这条是「源真的换了」的**唯一承重判据**。</summary>
        /// <remarks>⚠️ 不用真产物做本判据 —— 真产物与回退字面量逐值相同(见上一条),
        /// 用它测「换源」是恒绿假门。</remarks>
        [Test]
        public void test_readers_interactionKinds_providerInjection_swapsTable()
        {
            // 前置:未注入 ⇒ 回退表(枚举序数首值 Drop=0,而 GDD 演示序里 Drop 优先级 = 9)
            Assert.AreEqual(9, KindPriorityTable.PriorityOf(InteractableKind.Drop),
                "前置:未注入时读回退表(GDD §F-4.1b 演示序:Drop=9)");

            // 注入合成行集:优先级 = 9 − 回退值(即完全反转)
            KindPriorityTable.Provider = () => ReversedRows();
            Assert.AreEqual(0, KindPriorityTable.PriorityOf(InteractableKind.Drop),
                "注入后须读**产物**表(Drop=9−9=0)—— 仍得 9 ⇒ Provider 缝没接上,读的还是字面量");
            Assert.AreEqual(9, KindPriorityTable.PriorityOf(InteractableKind.Patient),
                "Patient:回退 0 ⇒ 反转 9");

            // 换**另一个** Provider(值 = 回退表原样)⇒ 必须跟着换。
            // ⚠️ 这条才是 `ReferenceEquals(provider, _cachedFor)` 的**唯一**判据:
            //    没有它,「缓存按 Provider 身份失效」只是一句注释(F8 变异实测)。
            KindPriorityTable.Provider = () => FallbackAsRows();
            Assert.AreEqual(9, KindPriorityTable.PriorityOf(InteractableKind.Drop),
                "换第二个 Provider 后须重建表(Drop 回到 9)—— 仍得 0 ⇒ 缓存不随 Provider 身份失效," +
                "第二个 Provider 被静默忽略");

            // 置回 null ⇒ 回到回退表(跨轮残留形态)
            KindPriorityTable.Provider = null;
            Assert.AreEqual(9, KindPriorityTable.PriorityOf(InteractableKind.Drop),
                "Provider 置 null 后须回到回退表 —— 仍得 0 ⇒ 缓存没随 Provider 失效(跨轮残留形态)");
        }

        /// <summary><b>装配真接线</b>:经 `CompositionRoot.Assemble(dataProvider: …)` 装配后,
        /// 查表须读到 **provider 的行集**(而非回退字面量)。
        /// <para>⚠️ 用**反转优先级的合成行集**(与回退处处不同)—— 真产物与回退逐值相同,
        /// 用它测这条是恒绿假门。本测是「`CompositionRoot` 那行 `Provider = …` 真在」的**唯一**判据。</para></summary>
        [Test]
        public void test_readers_interactionKinds_assemblyInjectsProvider()
        {
            var synthetic = new InteractionKindDataSet { Rows = ReversedRows(), ConfigVersion = 0u };

            AssembleWith(BuildProvider().Add(synthetic));

            Assert.AreEqual(0, KindPriorityTable.PriorityOf(InteractableKind.Drop),
                "装配后须读**产物**表(Drop:回退 9 ⇒ 反转 0)—— 仍得 9 ⇒ CompositionRoot 没设 Provider");
            Assert.AreEqual(9, KindPriorityTable.PriorityOf(InteractableKind.Patient),
                "Patient:回退 0 ⇒ 反转 9(整表都换了,不是只换一项)");
        }

        /// <summary><b>负向</b>:产物行内同一 kind 出现两次 ⇒ 建表即抛(不静默后者胜出)。</summary>
        [Test]
        public void test_readers_interactionKinds_duplicateKindInRows_throws()
        {
            var rows = new List<KindContractRow>(ReversedRows())
            {
                // 第二行同 kind、不同优先级 —— 静默覆盖会让生效值取决于行序(不可见)
                new KindContractRow(InteractableKind.Drop, 42, StableIdSource.PoiId, 6, false,
                                    DurationOwnerKind.None, 0, IntentUplink.None),
            };
            KindPriorityTable.Provider = () => rows;

            var ex = Assert.Throws<ArgumentException>(
                () => KindPriorityTable.PriorityOf(InteractableKind.Drop));
            StringAssert.Contains("出现两次", ex.Message, "失败报文须点名「重复」这一损坏面(可诊断)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // B. prescription_actions → PrescribeRequestDriver
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>真产物 ⇒ 端到端开方成功,且载荷 `treatment_id` = 产物声明的 10。</summary>
        [Test]
        public void test_readers_prescriptionActions_realArtifact_rowIsPrescribable()
        {
            PrescriptionActionsDataSet ds = CommittedPrescriptionActions();
            Assert.AreEqual(1, ds.Rows.Count, "前置:真产物当前唯一条目(内容批未开)");
            Assert.AreEqual(10, ds.Rows[0].ActionId, "前置:产物声明的处置 id");

            CompositionRootServices bag = AssembleWith(BuildProvider());

            Assert.AreEqual(10, PrescribeTreatmentIds(bag)[0],
                "开方成功 ⇒ DrugTreatmentApplied 载荷 treatment_id 取自产物行");
        }

        /// <summary><b>对照</b>:真产物唯一行 ⟷ `PrescribeRequestDriver.FallbackEntries` 逐字段相等。
        /// <para>这是处方侧「保留硬编码单条」的用处 —— 一份**独立于产物的 oracle**:
        /// 产物被烘成另一套(item_key / action_id / polarity 任一不同)⇒ 本测红。</para>
        /// <para>⚠️ 判据**当前为真**(两者同值)⇒ 单看本测绿不构成「接线有用」;
        /// 接线由 <see cref="test_readers_prescriptionActions_syntheticRow_drivesEntriesAndDoseBase"/> 独立记分。</para></summary>
        [Test]
        public void test_readers_prescriptionActions_artifactRows_matchFallbackLiteral()
        {
            PrescriptionActionsDataSet ds = CommittedPrescriptionActions();

            Assert.AreEqual(PrescribeRequestDriver.FallbackEntries.Count, ds.Rows.Count,
                "产物行数须与回退表项数相等(否则下面逐项对拍会漏项)");

            foreach (PrescriptionActionCookedRow row in ds.Rows)
            {
                // 产物只带 base_id ⇒ ProcessingState 取 Extracted(派生映射,见驱动 BuildFromRows 注)
                var key = new ItemKey(row.ItemKey, ProcessingState.Extracted);
                Assert.IsTrue(PrescribeRequestDriver.FallbackEntries.TryGetValue(key, out PrescriptionEntry expected),
                    $"回退表缺 item_key \"{row.ItemKey}\" —— 两者覆盖面不同");
                Assert.AreEqual(expected.ActionId, row.ActionId,
                    $"item_key \"{row.ItemKey}\":产物 action_id={row.ActionId} ≠ 回退字面量={expected.ActionId}");
                Assert.AreEqual(expected.Polarity.ToString().ToLowerInvariant(), row.Polarity,
                    $"item_key \"{row.ItemKey}\":产物 polarity=\"{row.Polarity}\" ≠ 回退字面量");
                Assert.AreEqual(0, expected.UnlockLevel,
                    "回退表 unlockLevel 须为 0 —— 与 BuildFromRows 的派生映射同口径" +
                    "(产物不携带 unlock_level;两者不一致 ⇒ 回退行为与产物行为分叉)");
            }
        }

        /// <summary><b>换源</b>:合成产物(action_id = 11 · DOSE_BASE = 1&lt;&lt;24,两者都与字面量不同)
        /// ⇒ 载荷 `treatment_id` 必为 11、`PrescribePorts.DoseBase` 必为 1&lt;&lt;24。
        /// <para>⚠️ 这是本段的**唯一承重判据** —— 真产物这两处恰好等于字面量
        /// (`action_id=10` / `DOSE_BASE=65536`)⇒ 用真产物测恒绿。</para></summary>
        [Test]
        public void test_readers_prescriptionActions_syntheticRow_drivesEntriesAndDoseBase()
        {
            const int syntheticDoseBase = 1 << 24;   // ≠ 65536

            var synthetic = new PrescriptionActionsDataSet(
                rows: new List<PrescriptionActionCookedRow>
                {
                    new PrescriptionActionCookedRow("salicylic_acid", 11, "causal"),
                },
                doseBase: syntheticDoseBase,
                maxDoseDetents: 5,
                singleDoseMaxRaw: 0,
                configVersion: 0u);

            CompositionRootServices bag = AssembleWith(BuildProvider().Add(synthetic));

            Assert.AreEqual(syntheticDoseBase, bag.PrescribePorts.DoseBase,
                "端口 DoseBase 须取自产物 `dose_const.DOSE_BASE`(仍得 65536 ⇒ 没读产物)");
            Assert.AreEqual(11, PrescribeTreatmentIds(bag)[0],
                "载荷 treatment_id 须取自产物行 action_id(仍得 10 ⇒ 走的还是回退硬编码单条)");
        }

        /// <summary><b>负向</b>:处方表产物内 item_key 重复 ⇒ 建表即抛(不静默后者胜出)。
        /// <para>⚠️ 本条补 F6 暴露的缺口:原改动集里 `BuildFromRows` 的重复检查**无专属判据** ——
        /// 把它停用后 8 条测全绿(证伪轮 F6 实测)。</para></summary>
        [Test]
        public void test_readers_prescriptionActions_duplicateItemKey_throws()
        {
            var dup = new PrescriptionActionsDataSet(
                rows: new List<PrescriptionActionCookedRow>
                {
                    new PrescriptionActionCookedRow("salicylic_acid", 10, "symptomatic"),
                    new PrescriptionActionCookedRow("salicylic_acid", 11, "causal"),
                },
                doseBase: 65536, maxDoseDetents: 5, singleDoseMaxRaw: 0, configVersion: 0u);

            var ex = Assert.Throws<ArgumentException>(() => AssembleWith(BuildProvider().Add(dup)));
            StringAssert.Contains("出现两次", ex.Message, "失败报文须点名「重复」这一损坏面(可诊断)");
        }

        /// <summary><b>负向</b>:极性串 ∉ {causal, symptomatic} ⇒ 建表即抛(不静默默认)。</summary>
        [Test]
        public void test_readers_prescriptionActions_unknownPolarity_throws()
        {
            var bad = new PrescriptionActionsDataSet(
                rows: new List<PrescriptionActionCookedRow>
                {
                    new PrescriptionActionCookedRow("salicylic_acid", 10, "homeopathic"),
                },
                doseBase: 65536, maxDoseDetents: 5, singleDoseMaxRaw: 0, configVersion: 0u);

            var ex = Assert.Throws<ArgumentException>(() => AssembleWith(BuildProvider().Add(bad)));
            StringAssert.Contains("极性", ex.Message, "失败报文须点名「极性」这一损坏面(可诊断)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // C. prescription_actions → PrescribeRequestDriver.MaxDoseDetents(甲档剩余项)
        //
        // 病因:`MAX_DOSE_DETENTS` 是甲档 2 份产物里**最后一个零读者字段** ——
        //   驱动此前读常量 `DentchDoseSelector.RegisteredMaxDoseDetents`(= 9),
        //   而产物带 7(GDD 已裁)⇒ 上限读数与真源脱钩。
        //
        // ⚠️ **为什么必须用合成夹具证「换源」**(与 A/B 段同因,理由实测):
        //   当前药 `dose_range = (1,5)` ⇒ **5 落点**;上限 7 与 9 都远大于它 ⇒
        //   `TickCount`(落点数)在两种取值下**相同**。若拿 `TickCount` 当观察面,
        //   接线前后恒绿 = 假门。观察面必须是**上限本身**(`MaxDoseDetents` 属性)。
        // ══════════════════════════════════════════════════════════════════════

        /// <summary><b>对照</b>:盘上真产物的 <c>MAX_DOSE_DETENTS</c> ⟷ 回退常量
        /// (<see cref="PrescribeRequestDriver.FallbackMaxDoseDetents"/>)。
        /// <para>两者现已同为 7(2026-10-20 把回退常量自 9 订正为 GDD 已裁的 7)。
        /// 本测钉住「它们不许再分叉」—— 分叉 = 有无产物两条路径拿到不同的上限。</para></summary>
        [Test]
        public void test_readers_maxDoseDetents_artifactMatchesFallbackConstant()
        {
            PrescriptionActionsDataSet ds = CommittedPrescriptionActions();
            Assert.AreEqual(PrescribeRequestDriver.FallbackMaxDoseDetents, ds.MaxDoseDetents,
                $"产物 MAX_DOSE_DETENTS={ds.MaxDoseDetents} ≠ 回退常量=" +
                $"{PrescribeRequestDriver.FallbackMaxDoseDetents} ⇒ 有/无产物两条路径的上限分叉");
        }

        /// <summary>真产物经组合根装配 ⇒ 驱动生效上限 = 产物值。</summary>
        [Test]
        public void test_readers_maxDoseDetents_assemblyReadsArtifact()
        {
            CompositionRootServices bag = AssembleWith(BuildProvider());

            Assert.AreEqual(CommittedPrescriptionActions().MaxDoseDetents, bag.PrescribeDriver.MaxDoseDetents,
                "装配后驱动的档数上限须取自产物 `dose_const.MAX_DOSE_DETENTS`");
        }

        /// <summary><b>换源(承重)</b>:合成产物把上限设成 <b>6</b>(与真产物 7、回退 7 都不同)
        /// ⇒ 驱动生效上限必为 6。
        /// <para>⚠️ 这是本段的**唯一承重判据** —— 真产物与回退常量同为 7 ⇒ 用真产物测恒绿。</para></summary>
        [Test]
        public void test_readers_maxDoseDetents_syntheticArtifact_drivesCeiling()
        {
            const int syntheticCeiling = 6;   // ≠ 7(真产物)· ≠ 回退常量

            var synthetic = new PrescriptionActionsDataSet(
                rows: new List<PrescriptionActionCookedRow>
                {
                    new PrescriptionActionCookedRow("salicylic_acid", 10, "symptomatic"),
                },
                doseBase: 65536,
                maxDoseDetents: syntheticCeiling,
                singleDoseMaxRaw: 0,
                configVersion: 0u);

            CompositionRootServices bag = AssembleWith(BuildProvider().Add(synthetic));

            Assert.AreEqual(syntheticCeiling, bag.PrescribeDriver.MaxDoseDetents,
                $"驱动上限须取自产物(期望 {syntheticCeiling})—— 仍得 " +
                $"{PrescribeRequestDriver.FallbackMaxDoseDetents} ⇒ 读的还是回退常量,产物没接上");
        }

        /// <summary><b>回退(零行为变化)</b>:不带产物的装配 ⇒ 上限 = 回退常量。
        /// <para>与上一条配对:一条证「产物优先」,一条证「无产物时仍可用」。
        /// 单有前者会把「回退路径已死」放过去。</para></summary>
        [Test]
        public void test_readers_maxDoseDetents_withoutArtifact_fallsBackToConstant()
        {
            var synthetic = new PrescriptionActionsDataSet(
                rows: new List<PrescriptionActionCookedRow>
                {
                    new PrescriptionActionCookedRow("salicylic_acid", 10, "symptomatic"),
                },
                doseBase: 65536,
                maxDoseDetents: PrescribeRequestDriver.FallbackMaxDoseDetents,
                singleDoseMaxRaw: 0,
                configVersion: 0u);

            CompositionRootServices bag = AssembleWith(BuildProvider().Add(synthetic));

            Assert.AreEqual(PrescribeRequestDriver.FallbackMaxDoseDetents, bag.PrescribeDriver.MaxDoseDetents,
                "上限须等于回退常量");
        }

        /// <summary>⚠️ <b>本段的判别力分布(禁借绿)</b>:五条里只有
        /// <see cref="test_readers_maxDoseDetents_syntheticArtifact_drivesCeiling"/> 一条
        /// 能区分「读了产物」与「读了常量」;其余四条是对照 / 装配面 / 回退 / 判别力论证,
        /// 单看恒绿。<b>不得</b>以「五条全绿」替代「换源已证」。
        /// <para>⚠️ 另一处易犯的假门:<c>DoseDetentCount</c>(落点数)**不是**本段的观察面 ——
        /// 当前药 `dose_range = (1,5)` 只有 5 落点,上限 6/7/9 都不改变它。
        /// 下一条把这一点变成可红的机器。</para></summary>
        [Test]
        public void test_readers_maxDoseDetents_detentCountIsBlindToCeiling()
        {
            // 判据:同一药档 (1,5) 在两个不同上限(6 / 9)下,驱动的 `DoseDetentCount` **同值**。
            // ⇒ 若谁拿 DoseDetentCount 当「上限已接线」的判据,那是恒绿假门。
            CompositionRootServices atSix = AssembleWith(BuildProvider().Add(
                new PrescriptionActionsDataSet(
                    rows: new List<PrescriptionActionCookedRow>
                    {
                        new PrescriptionActionCookedRow("salicylic_acid", 10, "symptomatic"),
                    },
                    doseBase: 65536, maxDoseDetents: 6, singleDoseMaxRaw: 0, configVersion: 0u)));
            CompositionRootServices atNine = AssembleWith(BuildProvider().Add(
                new PrescriptionActionsDataSet(
                    rows: new List<PrescriptionActionCookedRow>
                    {
                        new PrescriptionActionCookedRow("salicylic_acid", 10, "symptomatic"),
                    },
                    doseBase: 65536, maxDoseDetents: 9, singleDoseMaxRaw: 0, configVersion: 0u)));

            // 前置:上限**确实**不同(否则本测在证一个不存在的对照)
            Assert.AreEqual(6, atSix.PrescribeDriver.MaxDoseDetents, "前置:上限 6 已生效");
            Assert.AreEqual(9, atNine.PrescribeDriver.MaxDoseDetents, "前置:上限 9 已生效");

            // 承重:上限不同 ⇒ 落点数**仍相同** ⇒ `DoseDetentCount` 看不见上限。
            Assert.AreEqual(atSix.PrescribeDriver.DoseDetentCount, atNine.PrescribeDriver.DoseDetentCount,
                "落点数随上限变了 ⇒ 本段的判别力论证失效,须重评观察面选择");
            Assert.AreNotEqual(6, atNine.PrescribeDriver.DoseDetentCount,
                "落点数恰等于上限 ⇒ 上限与落点数的区分度消失,本测不再有证明力");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>回退表的值整体反转(9 − v)—— 刻意与 GDD 演示序处处不同,作「换源」的观察面。</summary>
        private static List<KindContractRow> ReversedRows()
        {
            var rows = new List<KindContractRow>(KindPriorityTable.FallbackRows.Count);
            foreach (KeyValuePair<InteractableKind, int> kv in KindPriorityTable.FallbackRows)
                rows.Add(new KindContractRow(kv.Key, 9 - kv.Value, StableIdSource.PoiId, 6, false,
                                             DurationOwnerKind.None, 0, IntentUplink.None));
            Assert.AreEqual(10, rows.Count, "前置:回退表须 10 项(枚举闭集)");
            return rows;
        }

        /// <summary>回退表原样转成行集(作「第二个 Provider」的内容 —— 与第一个刻意不同)。</summary>
        private static List<KindContractRow> FallbackAsRows()
        {
            var rows = new List<KindContractRow>(KindPriorityTable.FallbackRows.Count);
            foreach (KeyValuePair<InteractableKind, int> kv in KindPriorityTable.FallbackRows)
                rows.Add(new KindContractRow(kv.Key, kv.Value, StableIdSource.PoiId, 6, false,
                                             DurationOwnerKind.None, 0, IntentUplink.None));
            return rows;
        }

        /// <summary>按**真装配**跑一次开方(在场 + 有货),返回载荷 `treatment_id` 列表。</summary>
        private static List<int> PrescribeTreatmentIds(CompositionRootServices bag)
        {
            bag.PortionsStore.Grant(PlayerId, MinimalDrugCatalog.DrugKey, 4);
            PatientId patient = bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            bag.Presence.AddPresent(patient, new WorldPos(0, 0, 0));

            PrescribeDriverResult result = bag.PrescribeDriver.TryPrescribe(
                new PrescribeIntent(PlayerId, patient, MinimalDrugCatalog.DrugKey, doseOrdinal: 1), tick: 0);
            Assert.IsTrue(result.Applied, "前置:本次开方须走完五步(在场 + 有货 + 档位合法)");

            var ids = new List<int>();
            foreach (SimEvent e in bag.Stream.Events)
            {
                if (e.Kind != EventKind.DrugTreatmentApplied) continue;
                Assert.IsTrue(PayloadCodec.TryGetPayload(in e, bag.BlobPool,
                        out DrugTreatmentAppliedPayload p), "载荷可解码");
                ids.Add(p.TreatmentId);
            }
            Assert.AreEqual(1, ids.Count, "恰一条药疗事件");
            return ids;
        }

        private static CompositionRootServices AssembleWith(MultiTableProvider provider)
            => CompositionRoot.Assemble(dataProvider: provider);

        /// <summary>按类型分派的最小 `IDataProvider`:诊断三表走**真磁盘兜底装载器**
        /// (与生产 `dataProvider == null` 分支同一份产物 + 同一 codec —— 零 Fake),
        /// 两份待测表默认喂**盘上真产物**(调用方可用 <c>Add</c> 覆写成合成行集)。
        /// 问不到的类型直接抛(真问 = 接线错)。
        /// <para>⚠️ 处方表须**默认在场** —— <c>AssembleCore</c> 对它做的是**即时** Load
        /// (交互表是惰性 Provider,处方表不是)⇒ 缺它装配当场抛。</para></summary>
        private static MultiTableProvider BuildProvider()
            => new MultiTableProvider()
                .Add(DiagnosisCookedDisk.LoadSigns())
                .Add(DiagnosisCookedDisk.LoadReadFloor())
                .Add(DiagnosisCookedDisk.LoadJudgmentLexicon())
                .Add(CommittedInteractionKinds())
                .Add(CommittedPrescriptionActions())
                // ⚠️ DC-4 处置轴(2026-10-20 接线后组合根**即时** Load)——
                //    缺它装配当场抛;本门不测其语义,只为装配面完备。
                .Add(CommittedDiseaseActionAxis())
                // ⚠️ 9 病种注册表(2026-10-22 B1 接真后组合根**即时** Load)——
                //    同上,只为装配面完备。本门不跑 tick ⇒ 真表 id 从 2 起无影响。
                .Add(DataCookedDisk.LoadDiseaseRegistry());

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
