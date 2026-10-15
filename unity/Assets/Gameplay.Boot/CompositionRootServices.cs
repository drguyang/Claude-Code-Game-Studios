// M2 接线轮阶段 1 装配轮 · 组合根案 A(2026-10-09)
//
// 权威来源:ADR-025 §①(Gameplay.Boot 装配行,2026-10-09 增补)· ADR-005(抽象点)
//          · ADR-006 Amendment B(机制 A 计数器)· ADR-029(第七抽象点 IPayloadEncoder)
//          · ADR-023(Boot 场景承载启动序)
//
// 服务袋 = Assemble(...) 的只读产物。**只读属性,不提供 setter** —— 装配完成后不允许
// 事后替换依赖(fail-loud 的一半:要么全装配,要么抛异常,不存在「半装配袋」)。

using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.Prescription;
using DaYiJingCheng.Gameplay.PatientAI;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 组合根的服务袋 —— <see cref="CompositionRoot.Assemble()"/> 的只读产物。
    /// </summary>
    public sealed class CompositionRootServices
    {
        /// <summary>事件流实装(实现 <see cref="IEventSink"/>;三流的病史流,P0 唯一真源)。</summary>
        public EventStream Stream { get; }

        /// <summary>事件写入通道(= <see cref="Stream"/>,按抽象点类型暴露给写者)。</summary>
        public IEventSink EventSink => Stream;

        /// <summary>ID 发号权威(机制 A:计数器 + 高水位可重构,ADR-006 Amendment B)。</summary>
        public IIdAuthority IdAuthority { get; }

        /// <summary>不可变 blob 池(载荷字节的宿主;读面 = <see cref="IBlobPool"/>)。</summary>
        public InMemoryBlobPool BlobPool { get; }

        /// <summary>载荷编码器(第七抽象点 ADR-029;编码 + 入池一体)。</summary>
        public IPayloadEncoder Encoder { get; }

        /// <summary>载荷解码器(第八抽象点 ADR-031;读侧对称件 —— 池寻址 + 抛形解码)。
        /// <para>与 <see cref="Encoder"/> 共用同一 <see cref="BlobPool"/>(同池读写,无第二真源)。</para></summary>
        public IPayloadDecoder Decoder { get; }

        /// <summary>在场登记簿(= <see cref="IPresenceQuery"/> 的生产实装,EventStream 的 AC-15 依赖)。</summary>
        public PresenceRegistry Presence { get; }

        /// <summary>病人创建器(9 的 DiseaseOnset 写者,ADR-030)。</summary>
        public PatientSpawner PatientSpawner { get; }

        /// <summary>病人出现驱动(sprint-05 T1.1)—— 按 tick 节奏调 <see cref="PatientSpawner"/>
        /// 写 DiseaseOnset;由 <see cref="BootRoot"/> 的 tick 边沿序列驱动。</summary>
        public PatientAppearedDriver PatientAppearedDriver { get; }

        /// <summary>病例开账驱动(sprint-05 T1.2)—— 对在场且未开案的病人自动开案;
        /// 由 <see cref="BootRoot"/> 的 tick 边沿序列驱动。</summary>
        public CaseOpenedDriver CaseOpenedDriver { get; }

        /// <summary>急救驱动(G4.1 退役壳)—— 自动触发已移除,**零事件**;保留引用面防炸。</summary>
        public EmergencyAttemptDriver EmergencyAttemptDriver { get; }

        /// <summary>急救手动入口驱动(G4.2)—— 玩家意图 → HostEmergencyProcessor → 双事件入病史流。</summary>
        public EmergencyManualDriver EmergencyManualDriver { get; }

        /// <summary>急救直读通道装配驱动(story-009)—— 通道相位搬运 + 聚合上行映射 +
        /// Idle 门回调源;实现 <c>IArmedState</c>。BootRoot 生成玩家后 Attach。</summary>
        public EmergencyChannelDriver EmergencyChannel { get; }

        /// <summary>病人最小可见实体驱动(sprint-05 T2.0)—— 按在场集 spawn 占位视觉;
        /// 由 <see cref="BootRoot"/> 的 tick 边沿序列驱动。</summary>
        public PatientVisualSpawner PatientVisualSpawner { get; }

        /// <summary>体征链核心服务(批次 C):apply 驱动 + 投影桥 +
        /// <see cref="IVitalsQuery"/> 生产实装。由 <see cref="BootRoot"/> 的 tick 边沿序列驱动。</summary>
        public DiseaseVitalsService VitalsService { get; }

        /// <summary>42 侧脉案只读数源(sprint-05 T2.1)—— 把 <see cref="VitalsService"/>
        /// 接到 <c>ICasebookDtoSource</c>(42 装配的唯一数据入口)。</summary>
        public CasebookVitalsSource CasebookSource { get; }

        /// <summary>tick 驱动(ADR-005 抽象点之一 · 组合根唯一创建 —— 墨龄 / G2.2 同源)。</summary>
        public SimTickDriver TickDriver { get; }

        /// <summary>查体分册(39 侧最小持有者 —— 「查了没有」+ 快照;G1)。</summary>
        public ExaminationLedger ExaminationLedger { get; }

        /// <summary>查体入口驱动(G1)—— 玩家对病人查体 ⇒ 8 求值 ⇒ 分册快照。</summary>
        public ExamineDriver ExamineDriver { get; }

        /// <summary>「查体 ⇒ 见过病 D」投影器(sprint-08 S1.1 件 B)—— 查体后写一条
        /// <c>SkillGrown</c>(诊断, 病种 D)落病史流;病名册条目集的数据源。</summary>
        public SeenDiseaseProjector SeenDisease { get; }

        /// <summary>病名册条目源(sprint-08 S1.1 件 C)—— 已见集经词表反查 ⇒ 可指认
        /// <c>lexicon_id</c> 列表(守密:只出 lexicon_id,不出 disease_id)。</summary>
        public LexiconBookSource LexiconBook { get; }

        /// <summary>处方驱动(G3)—— 戥子落笔意图 → PrescribeFlow 五步 → DrugTreatmentApplied 入病史流。</summary>
        public PrescribeRequestDriver PrescribeDriver { get; }

        /// <summary>处方端口集合(G3.2)—— 21a/20/30 最小端口实装(装配层内联)。</summary>
        public PrescribePorts PrescribePorts { get; }

        /// <summary>最小库存面(G3.2 · 20)—— 持有量映射 + 扣减的真行为。</summary>
        public MinimalPortionsStore PortionsStore { get; }

        /// <summary>急救候选列表生产实例宿主(S3.1)—— 形态件③「运行期有载体」半边;
        /// 动作判定 / Idle 时序归 10,本宿主只持列表 + 跳过入口。</summary>
        public EmergencyCandidateRuntime EmergencyCandidates { get; }

        /// <summary>方笺页接线宿主(S2.3/S2.4)—— 落笔 → JudgmentWriter · 开方 →
        /// PrescribeRequestDriver;UI 事件 → 装配层的接线面(BootRoot SetAuthor 后可用)。</summary>
        public PrescriptionPageHost PrescriptionPage { get; }

        /// <summary>脉案命令路由(sprint-08 S1.1 件 F2/G)—— 实现 42 侧
        /// <c>ICasebookCommandSink</c>:病名栏 Submit ⇒ JudgmentWriter(Recorded / Revised,
        /// OQ-CB-5 甲循环落笔);档位经 <see cref="CasebookSource"/> 回读给 42。</summary>
        public CasebookCommandRouter CasebookCommands { get; }

        /// <summary>世界种子(存档头量;patient_seed = hash(world_seed, patient_id) 的入参)。</summary>
        public ulong WorldSeed { get; }

        internal CompositionRootServices(EventStream stream, IIdAuthority idAuthority,
                                         InMemoryBlobPool blobPool, IPayloadEncoder encoder,
                                         IPayloadDecoder decoder,
                                         PresenceRegistry presence, PatientSpawner patientSpawner,
                                         PatientAppearedDriver patientAppearedDriver,
                                         CaseOpenedDriver caseOpenedDriver,
                                         EmergencyAttemptDriver emergencyAttemptDriver,
                                         EmergencyManualDriver emergencyManualDriver,
                                         EmergencyChannelDriver emergencyChannel,
                                         PatientVisualSpawner patientVisualSpawner,
                                         ulong worldSeed, DiseaseVitalsService vitalsService,
                                         CasebookVitalsSource casebookSource,
                                         ExaminationLedger examinationLedger,
                                         ExamineDriver examineDriver,
                                         SeenDiseaseProjector seenDisease,
                                         LexiconBookSource lexiconBook,
                                         SimTickDriver tickDriver,
                                         PrescribeRequestDriver prescribeDriver,
                                         PrescribePorts prescribePorts,
                                         MinimalPortionsStore portionsStore,
                                         EmergencyCandidateRuntime emergencyCandidates,
                                         PrescriptionPageHost prescriptionPage,
                                         CasebookCommandRouter casebookCommands)
        {
            Stream = stream;
            IdAuthority = idAuthority;
            BlobPool = blobPool;
            Encoder = encoder;
            Decoder = decoder;
            Presence = presence;
            PatientSpawner = patientSpawner;
            PatientAppearedDriver = patientAppearedDriver;
            CaseOpenedDriver = caseOpenedDriver;
            EmergencyAttemptDriver = emergencyAttemptDriver;
            EmergencyManualDriver = emergencyManualDriver;
            EmergencyChannel = emergencyChannel;
            PatientVisualSpawner = patientVisualSpawner;
            VitalsService = vitalsService;
            CasebookSource = casebookSource;
            ExaminationLedger = examinationLedger;
            ExamineDriver = examineDriver;
            SeenDisease = seenDisease;
            LexiconBook = lexiconBook;
            TickDriver = tickDriver;
            PrescribeDriver = prescribeDriver;
            PrescribePorts = prescribePorts;
            PortionsStore = portionsStore;
            EmergencyCandidates = emergencyCandidates;
            PrescriptionPage = prescriptionPage;
            CasebookCommands = casebookCommands;
            WorldSeed = worldSeed;
        }
    }
}
