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

        /// <summary>急救链接线驱动(sprint-05 T1.3)—— 对在场且未触发过急救的病人自动触发一次;
        /// 由 <see cref="BootRoot"/> 的 tick 边沿序列驱动。</summary>
        public EmergencyAttemptDriver EmergencyAttemptDriver { get; }

        /// <summary>体征链核心服务(批次 C):apply 驱动 + 投影桥 +
        /// <see cref="IVitalsQuery"/> 生产实装。由 <see cref="BootRoot"/> 的 tick 边沿序列驱动。</summary>
        public DiseaseVitalsService VitalsService { get; }

        /// <summary>世界种子(存档头量;patient_seed = hash(world_seed, patient_id) 的入参)。</summary>
        public ulong WorldSeed { get; }

        internal CompositionRootServices(EventStream stream, IIdAuthority idAuthority,
                                         InMemoryBlobPool blobPool, IPayloadEncoder encoder,
                                         PresenceRegistry presence, PatientSpawner patientSpawner,
                                         PatientAppearedDriver patientAppearedDriver,
                                         CaseOpenedDriver caseOpenedDriver,
                                         EmergencyAttemptDriver emergencyAttemptDriver,
                                         ulong worldSeed, DiseaseVitalsService vitalsService)
        {
            Stream = stream;
            IdAuthority = idAuthority;
            BlobPool = blobPool;
            Encoder = encoder;
            Presence = presence;
            PatientSpawner = patientSpawner;
            PatientAppearedDriver = patientAppearedDriver;
            CaseOpenedDriver = caseOpenedDriver;
            EmergencyAttemptDriver = emergencyAttemptDriver;
            VitalsService = vitalsService;
            WorldSeed = worldSeed;
        }
    }
}
