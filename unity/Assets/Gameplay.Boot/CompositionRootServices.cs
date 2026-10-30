// M2 接线轮阶段 1 装配轮 · 组合根案 A(2026-10-09)
//
// 权威来源:ADR-025 §①(Gameplay.Boot 装配行,2026-10-09 增补)· ADR-005(抽象点)
//          · ADR-006 Amendment B(机制 A 计数器)· ADR-029(第七抽象点 IPayloadEncoder)
//          · ADR-023(Boot 场景承载启动序)
//
// 服务袋 = Assemble(...) 的只读产物。**只读属性,不提供 setter** —— 装配完成后不允许
// 事后替换依赖(fail-loud 的一半:要么全装配,要么抛异常,不存在「半装配袋」)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.Foraging;                      // HarvestOrchestrator(S20-15)
using DaYiJingCheng.Sim.Prescription;
using DaYiJingCheng.Sim.Contracts.SkillSystem;         // NoveltyTracker(S20-15)
using DaYiJingCheng.Sim.World;                         // World / StructureWriter(S20-17 装配轮)
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation;             // ClinicRoomMatchingService(TD-building-01 读侧)
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Building;   // BuildController(S20-19)
using DaYiJingCheng.Gameplay.Presentation.Telemetry;   // TelemetryAnalyticsTuning(第十二片)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 组合根的服务袋 —— <see cref="CompositionRoot.Assemble()"/> 的只读产物。
    /// </summary>
    public sealed class CompositionRootServices : IDisposable
    {
        /// <summary>事件流实装(实现 <see cref="IEventSink"/>;三流的病史流,P0 唯一真源)。</summary>
        public EventStream Stream { get; }

        /// <summary>事件写入通道(= <see cref="Stream"/>,按抽象点类型暴露给写者)。</summary>
        public IEventSink EventSink => Stream;

        /// <summary>53 的禁忌结算器(2026-10-30 · ADR-035 §①B/⑤)。
        /// <para>由 `CompositionRoot.Assemble` 在构造完本对象后赋值(**不走 ctor** —— 参数表已过长,
        /// 再加一项会与既有 20+ 形参的装配顺序耦合);`null` = 未装配(测试上下文 / 装配失败路径)。</para>
        /// <para>⚠️ 语义:`null` ⇒ 禁忌代价**不结算**,与「表为空」行为一致(都是零事件)。</para></summary>
        public DaYiJingCheng.Sim.Consequences.ContraindicationSettler ContraindicationSettler { get; internal set; }

        /// <summary>53 的**过量**结算器(2026-10-30 · ADR-035 §⑥ 解除后)。
        /// <para>与 <see cref="ContraindicationSettler"/> **刻意分成两个对象**:前者由「一条用药事件」
        /// 驱动(离散),后者由「一个 tick 的体内药量」驱动(连续累积)。合并会把两种游标语义
        /// 塞进同一个 `Step`,出错时无法归因 —— 见 `OverdoseSettler` 文件头。</para>
        /// <para>⚠️ 语义:`null` 或阈值空 ⇒ 过量代价**不结算**,与「表为空」一致(零事件)。</para></summary>
        public DaYiJingCheng.Sim.Consequences.OverdoseSettler OverdoseSettler { get; internal set; }

        /// <summary>53 的**判定链驱动**(2026-10-30 · ADR-036 D2/D3/S4)。
        /// <para>每个 tick 边沿跑 `Aggregate → Judge → Emit`,把「已结案病例」变成后果事件。</para>
        /// <para>⚠️ **语义**:<c>null</c> = <b>未装配</b>(无 <see cref="IDataProvider"/> ⇒ 两张表都
        /// 拿不到)。ADR-036 D3 的惰性口径:**不发,不是不判** —— 与「表为空」行为一致(零事件),
        /// 但**须有显式日志**(<c>BootRoot</c> 启动期读本属性打一条),否则与「机制未启用」同形。</para>
        /// <para>⚠️ 由 <c>Assemble</c> 在构造完本对象后赋值(与 <see cref="ContraindicationSettler"/>
        /// 同款 —— 参数表已过长,再加一项会与 20+ 形参的装配顺序耦合)。</para></summary>
        public DaYiJingCheng.Gameplay.Boot.ConsequenceDriver ConsequenceDriver { get; internal set; }

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

        /// <summary>
        /// 死亡候选产出侧(9 侧 · <see cref="IDeathCandidateSink"/> 的生产实现,story-008)。
        /// <para>29 的装配轮调 <see cref="IDeathCandidateSink.Register"/> 挂上
        /// `DeathCandidateConsumer`;在此之前 `Publish` 静默丢弃(接口 <c>:68</c> 允许)。</para>
        /// <para>⚠️ 本对象**只有 9 写、29 注册**;29 不读它的任何状态。</para>
        /// </summary>
        public IDeathCandidateSink DeathCandidates { get; }

        /// <summary>
        /// 死亡候选的**消费侧**(29 的注入点,2026-10-24 装配)。
        /// <para>⚠️ 已由装配层 <c>Register</c> 挂进 <see cref="DeathCandidates"/>
        /// (单消费者契约,构造期一次)—— 装配完成后 <c>DeathCandidates</c> 的
        /// <c>Consumer</c> 非 null 是**恒成立**的。</para>
        /// </summary>
        public DeathCandidateConsumer DeathConsumer { get; }

        /// <summary>
        /// 死亡冷却门(F-29-2,story-004)。<c>DeathAllowed(actor, t)</c> 从世界流重算
        /// <c>last_death_tick</c> —— **零计数器**。
        /// <para>暴露供观测 / 夹具直接求值;生产判据路径是 <see cref="DeathConsumer"/>。</para>
        /// </summary>
        public DeathCooldownGate DeathCooldown { get; }

        /// <summary>
        /// 玩家 actor 花名册 —— 「某 id 是不是玩家」。
        /// <para>⚠️ <b>由 <c>BootRoot</c> 在生成玩家后登记</b>(id 在装配之后才铸造)。
        /// 装配刚完成时 <c>Count == 0</c> 是**合法状态**,不是装配失败。</para>
        /// <para>登记前 <c>ClassOfDeathCandidate</c> 恒 <c>Patient</c> ⇒ 与接线前逐位一致。</para>
        /// </summary>
        public PlayerActorRegistry PlayerActors { get; }

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

        /// <summary>
        /// 17 采集编排(S20-15)—— 一次成功采集的五步裁决(铸 id → 三条产出事件 → 成长)。
        /// <para><b>本属性是本件解除 AC-17-01 挂起的**唯一**入口</b>:接线前
        /// `new HarvestOrchestrator(` 全仓零生产构造点,采集链在运行期不存在。</para>
        /// <para>⚠️ <b>生产触发点仍归 4 的交互装配轮</b>(「对着一株药材按 E」→ 判距 → 本编排)。
        /// 「已装配」≠「玩家按得到」—— 与 <see cref="DropEventWriter"/> 同款的可观测边界。</para>
        /// </summary>
        public HarvestOrchestrator Harvest { get; }

        /// <summary>
        /// 新颖度追踪器(30 的运行期加速缓存,ADR-026)。
        /// <para>⚠️ <b>与 <see cref="SeenDisease"/> 共用同一实例</b>(键含 <c>skillId</c>,
        /// 诊断与采集两域永不共键 ⇒ 共用安全且必需 —— 另建实例 = 两个装配点,违 AC-17-14g)。
        /// 暴露它不是为了让人另开一个,而是为了让「诊断侧与采集侧问的是不是同一个追踪器」
        /// 成为**可判定**的(夹具直接比引用)。</para>
        /// <para>⚠️ <b>不独立快照</b>(ADR-026 ⑥):真源在病史流的 <c>SkillGrown</c> 行;
        /// 从流回填的实现**全仓仍为零**(见 story-006 §已知缺口 ①)。</para>
        /// </summary>
        public NoveltyTracker Novelty { get; }

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

        /// <summary>44 的 cue 界面(story-018 · A-1 的生产构造点)。
        /// <para>⚠️ 本属性非 null **恒成立** —— 装配缝未接时它是 `CueRenderSink(NullRenderTarget)`,
        /// 契约语义(句柄 / 档位)仍完整,只是没有声音。<b>是否有声</b>看
        /// <see cref="AudioRenderAssembled"/>,不看本属性是否为 null。</para></summary>
        public IAudioCueSink AudioSink { get; }

        /// <summary>44 的世界语境声源池(story-018 · A-4 的生产构造点)。
        /// <para>⚠️ **单池不变量**(AC-018-4):全案只有这一个池;
        /// `SpatializationDirector` 若被构造**必须**走 `(latticeSize, pool)` 重载共享它。</para></summary>
        public AudioSourcePool AudioPool { get; }

        /// <summary>渲染面是否真装配(`false` ⇒ <see cref="AudioSink"/> 走 `NullRenderTarget`)。
        /// <para>启动期日志 / 测试的可观测面 —— 「装了没装」不得靠猜(承 story-018 头注)。</para></summary>
        public bool AudioRenderAssembled { get; }

        /// <summary>51 遥测的装配层入口(TD-epic-01 接线片 · 2026-10-19)。
        /// <para>**纯读**:不持 `IEventSink`、不写三流、不写文件(ADR-019 §三 / GDD R1)。</para>
        /// <para>用处:任何人问「F1 是多少」只调 <see cref="TelemetryReporter.JudgmentReport"/> ——</para>
        /// <para>接线前 51 的重算器只有 EditMode 夹具驱动、**生产零调用**(普查判的正是这条)。</para>
        /// <para>⚠️ 当前只接线 F1;F2–F7 的还原器接线归各自 story 轮(见
        /// <see cref="TelemetryReporter.AllMetricsReport"/> 的自陈)。</para></summary>
        public TelemetryReporter Telemetry { get; }

        /// <summary>世界语境空间化的装配入口(S15-4 · TD-audio-028)。
        /// <para>⚠️ 与 <see cref="AudioPool"/> **共享同一池**(AC-018-4 单池不变量)。
        /// null ⇔ 渲染面未装配(<see cref="AudioRenderAssembled"/> = false)。</para>
        /// <para>可观测面:位置是否真被写看 <c>(renderTarget as AudioSourceRenderTarget).SpatializedCount</c>;        /// 排名 / 寿命看本属性的 <c>ActiveCount</c> / <c>Order</c>。</para></summary>
        public SpatializationDirector Spatialization { get; }

        /// <summary>44 的渲染目标(听者锚点注入缝在它身上 —— <see cref="Gameplay.Presentation.Audio.AudioSourceRenderTarget.SetListenerPosition"/>)。
        /// <para>⚠️ 与 <see cref="AudioRenderAssembled"/> **同一真源**:后者就是由它
        /// `is AudioSourceRenderTarget` 派生的 ⇒ 两处不可能分叉。</para>
        /// <para>null ⇔ 渲染面未装配(null sink)。</para></summary>
        public AudioSourceRenderTarget RenderTarget { get; }

        /// <summary>病人 cue 驱动(story-018 · AC-018-6 的**调用方**)—— tick 边沿 → 13 调度 → 44 发射。
        /// <para>⚠️ 由 <see cref="BootRoot"/> 的 tick 边沿序列驱动(与 <see cref="PatientAppearedDriver"/>
        /// 等同一泵);本类只提供引用,**不自行推进 tick**。</para></summary>
        public PatientCueDriver PatientCueDriver { get; }

        /// <summary>病人 cue 驱动内部消费的**同一个** 13 行为导演实例(story-021 起的可观测面)。
        /// <para>⚠️ 暴露它**不是**把行为态合并成单一所有者 —— `TD-audio-033` ②(视觉侧另持一份)
        /// 仍 open。此处只为让「装配层究竟喂了 `KnowsClinic` 什么」成为**可判定的**:
        /// 驱动 `PatientCueDriver` 后读 <c>BehaviorDirector.For(id).Current</c> 即可。</para></summary>
        public PatientBehaviorDirector BehaviorDirector { get; }

        /// <summary>听诊精度档位驱动(B 轮 2026-10-20)—— 44 侧 `tier_map` 的运行期承载。
        /// <para>null ⇔ 事件表缝未接(`audio.Table` 为空)· 由 `BootRoot` 帧泵每帧调 `Tick(Time.deltaTime)`。</para></summary>
        public AudioTierDriver AudioTierDriver { get; }

        /// <summary>24「布局 → 房间类型」匹配链的生产装配(`TD-building-01` 读侧 · 2026-10-21)。
        /// <para>链 = 23 注册表 → 只读视图 → 家具属性索引 → 房间析出(真判据)→ ROOM_TABLE 子句求值。</para>
        /// <para>⚠️ <b>null ⇔ 装配时未传入 23 的 <c>StructureInstanceRegistry</c></b>
        /// (见 `CompositionRoot.Assemble` 的 <c>structures</c> 形参备注)——
        /// 本装配**不自建**注册表(自建 = 与将来的 `World` 实例分叉,两份真源)。
        /// ⇒ 「24 能不能在运行期求值」是**可观测**的,不是靠猜。</para>
        /// <para>⚠️ 无缓存:每次 <c>Match()</c> 都按**当下**的注册表重算(建造 / 拆除后自动生效;
        /// 失效机制 / Memoize 归 story-005)。</para></summary>
        public ClinicRoomMatchingService ClinicRoomMatching { get; }

        /// <summary>
        /// 世界实例(S20-17 装配轮 · 2026-10-28)。
        /// <para><b>null ⇔ 装配时未传入 <c>world</c></b>(既有 117 处 <c>Assemble</c> 调用方
        /// 都不传 ⇒ 仍为 null,与 <see cref="ClinicRoomMatching"/> 同款「可观测而非静默」)。</para>
        /// <para>⚠️ <b>生产路径今日给的是 <c>MinimalWorldFactory.Create()</c> 的合成世界</b> ——
        /// 16×1×16 全 walkable + 空槽位目录。世界烘焙数据零产出(唯一作者 ADR-022 关卡工具
        /// 已裁定不排期),故本属性非 null <b>不等于</b>「世界内容正确」。那条差别由
        /// <c>BootRoot.IsSyntheticWorld</c> 与 <c>MinimalWorldFactory</c> 的启动日志承载。</para>
        /// </summary>
        public World World { get; }

        /// <summary>
        /// 23 的结构写者(<c>Place</c> / <c>Remove</c>)(S20-17 装配轮 · 2026-10-28)。
        /// <para>构造条件 = 装配时传了 <see cref="World"/>(或仅传了注册表,见
        /// <c>CompositionRoot.Assemble</c>)。<b>null ⇔ 无世界</b>。</para>
        /// <para>⚠️ <b>「已装配」≠「可被玩家触发」</b>:<c>Place</c> / <c>Remove</c> 的
        /// <b>生产发起方</b>归 42 的建造交互(<c>O-23-6</c>),今日零调用点。本属性只证明
        /// 写者<b>存在且可被调用</b>(通电待信,承 <c>LoadSequencer.RequestLoad</c> 同形态)。</para>
        /// </summary>
        public StructureWriter StructureWriter { get; }

        /// <summary>
        /// 世界读档重建服务(S20-18 · 2026-10-28)。
        /// <para><b>null ⇔ 装配时未传 <c>world</c></b>(无世界即无从重建;既有 100+ 处
        /// <c>Assemble</c> 调用方都不传 ⇒ 仍为 null)。</para>
        /// <para><b>唯一生产调用点 = <c>LoadSequencer</c> 的 <c>afterRestore</c> 钩子</b>
        /// (与库存投影同在):读档完成、流已 `Restore` 之后,先重建库存投影、再重建世界。</para>
        /// <para>⚠️ <b>「已装配」≠「读档已可用」</b>:<c>BootRoot.RequestLoad</c> 今日零调用方
        /// (UI 存档位未接线),且 <c>StructureWriter.Place</c> 生产零调用点 ⇒ 生产流里
        /// 永不含 <c>StructurePlaced</c> ⇒ 本服务在生产上<b>恒空跑</b>(逐条登记在
        /// <c>sprint-20</c> §十 S20-18 的「挂标不记绿」第 1 条)。</para>
        /// </summary>
        public WorldLoadService WorldLoad { get; }

        /// <summary>
        /// 42 的建造发起方(S20-19 · 2026-10-28)—— 判定 + 选料 + 落流的编排器。
        /// <para><b>null ⇔ 装配时未传 <c>world</c></b>(无世界即无槽位 / 占用 / 写者)。</para>
        /// <para><b>它兑现了什么</b>:`StructureWriter.Place` / `Remove` 自 S20-17 起有生产实例,
        /// 但**没有发起方**(`O-23-6` 登记两个多月)。本属性是那个发起方的编排层 ——
        /// 输入侧(<c>BuildInputDriver</c>)住 `Gameplay.Boot`,在 `BootRoot` 里接线。</para>
        /// <para>⚠️ <b>「已装配」仍 ≠ 「玩家能建造」</b>:① 扣料归属须 <see cref="Building.BuildController.BindOwner"/>
        /// 绑定(玩家 actorId 在装配后才铸造);② `BuildSlotCatalog` 生产侧**仍空**
        /// (`RegisterSlot` 零调用,`MinimalWorldFactory` 造空目录)⇒ `PlaceableChecker.Check`
        /// 在生产上恒 `SlotNotFound`。两条都登记在 `sprint-20` §十 S20-19 的「挂标不记绿」。</para>
        /// </summary>
        public BuildController BuildController { get; }

        /// <summary>
        /// 7a 存档服务(S19-1)。
        /// <para><b>`null` ⇔ 装配时未指定存档目录</b>(`Assemble(..., saveBasePath: null)`),
        /// 是**合法状态**,不是半装配 —— 与 <see cref="AudioRenderAssembled"/> 同款
        /// 「可观测而非静默」处置。生产路径由 `BootRoot` 显式传目录。</para>
        /// <para>🔁 <b>2026-10-25 生产接线轮订正(原文不删)</b>:下句「即便非 null,今日
        /// `Checkpoint` / `Load` 也必然抛」**已过期** —— 瞬时源
        /// <see cref="NarrativeContext"/> 已接通(读急救通道 Armed 态),`Checkpoint` /
        /// `SaveOnExit` / `Load` 的门**已可用**;事件触发半边(锚点接线)仍归
        /// `TD-persist-04` 动作 ①,**不得**读成「AC-7a-20 已全满足」。</para>
        /// <para>⚠️ 判据 ④ 的**字节半**(「产出的字节里折叠行出现」)今日 = `NOT-RUN`,
        /// 归 `TD-persist-03`。本属性的存在只证明**位置半 + 委托半**成立。</para>
        /// </summary>
        public SaveService SaveService { get; }

        /// <summary>
        /// 「无未决医疗动作」瞬时源(规则十四「一个量」· 2026-10-25 生产接线轮)。
        /// <para><see cref="NoPendingMedicalActionQuery"/> —— `SaveService` 与
        /// <see cref="CheckpointFallback"/> 共用的**同一实例**(装配期单变量,拆实例 =
        /// 读档门与兜底门分叉)。`IsBound` 在 `Assemble` 返回时恒 true(急救装配段 Bind)。</para>
        /// <para>⚠️ 7b 呈现侧(册级「写不可 / 读不可」灰置)**可经本属性取态** ——
        /// 这是登记的复用面,不是本轮新义务。</para>
        /// </summary>
        public NoPendingMedicalActionQuery NarrativeContext { get; }

        /// <summary>
        /// `CHECKPOINT_INTERVAL` 定时兜底调度器(AC-7a-20 定时半边 · 2026-10-25 生产接线轮)。
        /// <para>`null` ⇔ `SaveService` 为 null(未指定存档目录);驱动 = `BootRoot` 的
        /// tick 边沿序列(`OnTicks(本帧推进 tick 数)`)。</para>
        /// </summary>
        public CheckpointFallback CheckpointFallback { get; }

        /// <summary>
        /// 事件触发 checkpoint 的锚点观察器(AC-7a-20 事件触发半边 · 2026-10-25 动作① 实现轮)。
        /// <para>`null` ⇔ `SaveService` 为 null(未指定存档目录);驱动 = `BootRoot` 的
        /// tick 边沿序列(`Drain(Stream.Events)` —— 集中观察,裁定①)。</para>
        /// </summary>
        public AnchorCheckpointTrigger AnchorCheckpointTrigger { get; }

        /// <summary>
        /// 52 随机事件导演主循环壳(tick 边沿)—— 本轮只接出诊启动一节
        /// (TD-randomevents-02 二十九订 ⚠ 接线义务①;其余 52 节留具名槽,见驱动类头注)。
        /// <para>玩家 actorId 由 <c>BootRoot</c> 生成玩家后 <c>BindPlayer</c> 晚绑定
        /// (id 在装配之后才铸造);未绑定期 <c>OnTickEdge</c> 为 no-op(fail-safe)。</para>
        /// </summary>
        public RandomEventsDirectorDriver RandomEventsDirector { get; }

        /// <summary>
        /// 读档缝合序列器(TD-persist-04 义务② · Load 接线轮 2026-10-25):
        /// <c>SaveService.Load</c> 成功后依序复位锚点游标与兜底计数(见 <see cref="LoadSequencer"/>
        /// 类头注的调用序与派生态未重建清单)。
        /// <para><b>通电待信(裁定②)</b>:生产触发点 = <c>BootRoot.RequestLoad</c>,
        /// 今日零调用方(UI 存档位接线轮直接调)。saveBasePath 为 null 时本件与三件
        /// 存档侧组件同 null(同源装配)。</para>
        /// </summary>
        public LoadSequencer LoadSequencer { get; }

        /// <summary>
        /// 20 库存实体装载服务(S20-10/11/12 修复轮 · 2026-10-27 用户裁定「「装载库存实体」的链,现在建」)。
        /// <para><b>本属性是本轮三条 BLOCKING 的接线落点</b> ——
        /// <c>InventoryFold.Fold</c>(S20-10 B-1)· <c>BuildPartQualityGate</c>(S20-11 B-1)·
        /// <c>CarryLoadSolver.Sum</c>(S20-11 W-2) 三者此前**零生产调用点**,本服务的
        /// `Rebuild()` 是它们的唯一调用者。</para>
        /// <para>⚠️ <b>非 null 恒成立</b>(与 <see cref="SaveService"/> 不同:它不依赖存档目录)。
        /// 但 <see cref="InventoryLoadService.Current"/> 在首次 <c>Rebuild()</c> 前为 null ——
        /// 「装没装」看本属性,「重建没重建」看 <c>Current</c>。</para>
        /// <para>⚠️ <b>生产触发点两处</b>:① 读档后(<see cref="LoadSequencer"/> 的 <c>afterRestore</c>
        /// 钩子 —— 用户裁定「在装配根加「读档后重建库存投影」」);
        /// ② 任何下游按需调 <c>Rebuild()</c>(20/20b 的呈现侧接线轮)。</para>
        /// </summary>
        public InventoryLoadService InventoryLoad { get; }

        /// <summary>
        /// 「医馆格」判据源的生产占位(空集 fail-safe,真源归 23/24 接线轮)。
        /// <para><b>同源义务的单点发放</b>:写者 / 将来的 F1PipelineInput 构造 /
        /// 将来的规则十「无人」检测器三处消费**必须取本实例**,不得另立
        /// (IClinicCellSource 接口头注禁令;两个将来的消费点出现时,组合根测试
        /// 的同源断言面随之扩列)。</para>
        /// </summary>
        public IClinicCellSource ClinicCells { get; }

        internal CompositionRootServices(EventStream stream, IIdAuthority idAuthority,
                                         InMemoryBlobPool blobPool, IPayloadEncoder encoder,
                                         IPayloadDecoder decoder,
                                         IReadOnlyList<DiseaseRegistryEntry> registry,
                                         PresenceRegistry presence, PatientSpawner patientSpawner,
                                         PatientAppearedDriver patientAppearedDriver,
                                         CaseOpenedDriver caseOpenedDriver,
                                         EmergencyAttemptDriver emergencyAttemptDriver,
                                         EmergencyManualDriver emergencyManualDriver,
                                         EmergencyChannelDriver emergencyChannel,
                                         PatientVisualSpawner patientVisualSpawner,
                                         ulong worldSeed, DiseaseVitalsService vitalsService,
                                         IDeathCandidateSink deathCandidates,
                                         DeathCandidateConsumer deathConsumer,
                                         DeathCooldownGate deathCooldown,
                                         PlayerActorRegistry playerActors,
                                         CasebookVitalsSource casebookSource,
                                         ExaminationLedger examinationLedger,
                                         ExamineDriver examineDriver,
                                         SeenDiseaseProjector seenDisease,
                                         HarvestOrchestrator harvest,
                                         NoveltyTracker novelty,
                                         LexiconBookSource lexiconBook,
                                         SimTickDriver tickDriver,
                                         PrescribeRequestDriver prescribeDriver,
                                         PrescribePorts prescribePorts,
                                         MinimalPortionsStore portionsStore,
                                         EmergencyCandidateRuntime emergencyCandidates,
                                         PrescriptionPageHost prescriptionPage,
                                         CasebookCommandRouter casebookCommands,
                                         IAudioCueSink audioSink,
                                         AudioSourcePool audioPool,
                                         SpatializationDirector spatialization,
                                         AudioSourceRenderTarget renderTarget,
                                         bool audioRenderAssembled,
                                         PatientCueDriver patientCueDriver,
                                         PatientBehaviorDirector behaviorDirector,
                                         AudioTierDriver audioTierDriver = null,
                                         TelemetryAnalyticsTuning? telemetryKnobs = null,
                                         ClinicRoomMatchingService clinicRoomMatching = null,
                                         SaveService saveService = null,
                                         CheckpointFallback checkpointFallback = null,
                                         NoPendingMedicalActionQuery narrativeContext = null,
                                         AnchorCheckpointTrigger anchorCheckpointTrigger = null,
                                         Func<uint?> currentConfigVersion = null,
                                         RandomEventsDirectorDriver randomEventsDirector = null,
                                         IClinicCellSource clinicCells = null,
                                         LoadSequencer loadSequencer = null,
                                         InventoryLoadService inventoryLoad = null,
                                         World world = null,
                                         StructureWriter structureWriter = null,
                                         WorldLoadService worldLoad = null,
                                         BuildController buildController = null)
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
            DeathCandidates = deathCandidates;
            DeathConsumer = deathConsumer;
            DeathCooldown = deathCooldown;
            PlayerActors = playerActors;
            CasebookSource = casebookSource;
            ExaminationLedger = examinationLedger;
            ExamineDriver = examineDriver;
            SeenDisease = seenDisease;
            Harvest = harvest;
            Novelty = novelty;
            LexiconBook = lexiconBook;
            TickDriver = tickDriver;
            PrescribeDriver = prescribeDriver;
            PrescribePorts = prescribePorts;
            PortionsStore = portionsStore;
            EmergencyCandidates = emergencyCandidates;
            PrescriptionPage = prescriptionPage;
            CasebookCommands = casebookCommands;
            WorldSeed = worldSeed;
            AudioSink = audioSink;
            AudioPool = audioPool;
            Spatialization = spatialization;
            RenderTarget = renderTarget;
            AudioRenderAssembled = audioRenderAssembled;
            PatientCueDriver = patientCueDriver;
            BehaviorDirector = behaviorDirector;
            AudioTierDriver = audioTierDriver;
            ClinicRoomMatching = clinicRoomMatching;
            SaveService = saveService;
            CheckpointFallback = checkpointFallback;
            NarrativeContext = narrativeContext;
            AnchorCheckpointTrigger = anchorCheckpointTrigger;

            // 51 遥测接线(TD-epic-01):decoder 在 Sim.Codec、51 的装配引不到 ⇒ 在本装配层包成委托。
            // ⚠️ stream / decoder / registry 均已是本 ctor 的形参 ⇒ 不加新装配边、不改 Assemble 签名。
            // 第十二片:旋钮表(TD-epic-01)由调用方传入,缺省 null ⇒ F4–F7 保持「未给」,不代填默认值。
            // 🔁 2026-10-24 `TD-epic-01-f`:`currentConfigVersion` 把全局派生值喂进 51,
            //    使 `AC-51-D7` 的版本门在生产路径上**第一次**有值可比。
            //    ⚠️ 传 null(装配层未提供)⇒ 开发者预览不做版本门 —— 不是「恒匹配」。
            Telemetry = new TelemetryReporter(stream, decoder, registry ?? CreateFallbackRegistry(),
                                              telemetryKnobs, currentConfigVersion: currentConfigVersion);
            // 52 出诊启动接线(2026-10-25 接线轮 · 三裁全取推荐):主循环壳 + 医馆格占位源。
            // 两者可为 null(测试装配不传)⇒ BootRoot 边沿块 null 条件跳过 —— 与
            // AnchorCheckpointTrigger / narrativeContext 的「saveBasePath 为 null 同 null」同格。
            RandomEventsDirector = randomEventsDirector;
            ClinicCells = clinicCells;
            LoadSequencer = loadSequencer;
            InventoryLoad = inventoryLoad;
            // S20-17(2026-10-28):世界与 23 的结构写者。两者**成对**由装配根传入 ——
            // ctor 不做「有 world 无 writer」的补构造:补构造需要 modules 目录 / DropEventWriter,
            // 那是 Assemble 的知识,不该藏在 DTO 里。传了 world 却没传 writer ⇒ 玩家可走但不可建造,
            // 由 Assemble 的装配断言拦(它才是构造点)。
            World = world;
            StructureWriter = structureWriter;
            // S20-18(2026-10-28):世界读档重建服务。同款「不补构造」—— 补构造需要
            // decoder / modules 目录,那是 Assemble 的知识。传了 world 却没传本件 ⇒
            // 读档后世界不重建(旧世界残留)—— 故 Assemble 在 `world != null` 的同一支路里
            // 必然构造它,本 ctor 只负责转交。
            WorldLoad = worldLoad;
            // S20-19(2026-10-28):42 的建造发起方。同款「不补构造」—— 补构造需要槽位目录 /
            // 三个判定器 / 投影读数,那是 Assemble 的知识。无世界 ⇒ 恒 null。
            BuildController = buildController;
        }

        /// <summary>最小内联病种注册表(DiseaseId=1)—— 装配层未传入真表时的接线载体。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> CreateFallbackRegistry()
            => new List<DiseaseRegistryEntry>
            {
                // Kind 显式赋值 —— 零值陷阱守卫(见 DiseaseKind 注释);本路径不调 Validate。
                new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "fallback", Kind = DiseaseKind.Disease },
            };

        /// <summary>
        /// 释放装配产物持有的引擎资产(清债轮 · TD-audio-026 丙案「完全构造」· 2026-10-25)。
        /// <para>构造保持**无条件建池**(AC-018-4 单池不变量:池是 ADR-028 ① 唯一所有者的
        /// 承载物,不因未配音频而偷懒)—— 本方法补的是**释放半边**:装配袋是池根的
        /// 托管入口,Dispose ⇒ 池销毁自身 root 与全部声源(幂等;编辑器 / 播放双态分流
        /// 见 <see cref="AudioSourcePool.Dispose"/> 的实现注)。</para>
        /// <para>⚠️ 只处置引擎侧资产(池根 / 声源);三流 / blob 池 / 服务对象不带
        /// 非托管资源,不在此处置。</para>
        /// <para>⚠️ 生产侧不受影响:<c>BootRoot.OnDestroy</c> 已直接调
        /// <c>AudioPool.Dispose()</c>(与本方法同终点,双路径幂等)。</para>
        /// </summary>
        public void Dispose()
        {
            AudioPool?.Dispose();
        }
    }
}
