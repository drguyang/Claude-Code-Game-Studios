// M2 接线轮阶段 1 装配轮 · 组合根案 A(2026-10-09)
//
// 权威来源:
//   案 A(用户裁定)—— 组合根 = 新装配 Gameplay.Boot:b5 门把 Sim.Codec 装配级标红,
//     而 PayloadEncoder 实现体住 Sim.Codec、Gameplay.Presentation 不可引用它
//     ⇒ 「编码器的构造点」必须落在一个**可同时看见 Sim.Codec 与 Gameplay.Presentation**
//     的独立装配,这就是本装配(Gameplay.Boot,ADR-025 §① 2026-10-09 增补行)。
//   ADR-005(六 + 一抽象点)· ADR-006 Amendment B(机制 A 计数器 = IdAuthority)
//   ADR-029(第七抽象点 IPayloadEncoder,乙案:编码 + 入池一体)
//   ADR-030(9 的 DiseaseOnset 写者 = PatientSpawner)
//   ADR-025 §①(IVitalsQuery 生产实装落本装配 · M2 阶段 2 批次 C)· ADR-025 §② 甲案(ToFloat 出口白名单)
//   sprint-05 T1.1(2026-10-10):「病人出现」驱动接线 —— 见 <see cref="PatientAppearedDriver"/> 与
//   <see cref="CreateFallbackRegistry"/>。disease_registry.json 未建(数值轮未开 ⇒ 建全表 = 假数据
//   过不了 17 条校验)⇒ 组合根自带最小内联注册表(1 病种,满足 R1-17「至少 1 个病种」)。
//
// fail-loud 口径:任一依赖缺失 ⇒ 具名 ArgumentNullException / InvalidOperationException,
// **不返回半装配袋**;所有实装类自身构造器同样 fail-loud,任何一步抛出都中止整次装配。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Gameplay.Presentation.Telemetry;   // TelemetryAnalyticsTuning(第十二片注入缝)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.EmergencyProcedures;
using DaYiJingCheng.Sim.Prescription;
using DaYiJingCheng.Sim.World;                 // EcozoneRegistry(story-021 真值源链)
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Input;                 // EmergencyDirectReadChannel(story-009 通道装配)
using DaYiJingCheng.Gameplay.Interaction;           // KindPriorityTable(2026-10-20 交互契约表读方接线)
using UnityEngine.InputSystem;                 // InputActionAsset(story-009 通道装载,可空)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 组合根 —— 生产装配的唯一入口(把抽象点接成可用的实装图)。
    /// <para>可 EditMode 直测(纯 C# 装配逻辑,不碰场景 / 地址 / 异步)。</para>
    /// </summary>
    public static class CompositionRoot
    {
        // ── F-10.1 三乘子占位(story-009;值归数值轮 OQ-10-7)──
        // 形状值与既有测试 fixture 同款:AXIAL_SCALE ≥ MAG_MAX 结构门(F-10.1)、
        // DZ_MAG = 0 = 死区关闭、满幅 = MUL_ONE(Q16.16)。
        private const int EmergencyAxialScale = 65536;
        private const int EmergencyDzMag = 0;
        private const int EmergencyMagMax = 65536;

        // ── 设备切换迟滞六旋钮 = **烘焙表的兜底值**(TD-epic-01 第十一片;值归数值轮)──
        // 与 `assets/data/input_device_switch.json` 的种子**同值**(Fix 字符串经 FixParse 得
        // 同一 raw;区间断言由 InputDeviceSwitchBinder 在装载期执法)。
        // ✅ **2026-10-19 还债**:原注释写「临时种子,未进 data-core —— 只有来源 JSON + EditMode
        //    装载判据,零运行期制品」。现 data-core 已有 `input_device_switch.cooked.bytes`
        //    (`InputDeviceSwitchBake` 烘焙 → `AddressablesDataProvider` 装载),生产真源 = 烘焙表;
        //    本组常量**降级为兜底**(预载失败时由 `BootRoot.ResolveDeviceSwitchKnobs` 取)。
        // ⚠️ 兜底与烘焙表**必须逐值相等** —— 由 `device_switch_wiring_test` 的对账夹具钉住
        //    (承 44 侧 `AudioSettingsRegistryBinder.ValidateValueReconciliation` 同构形态);
        //    数值轮定稿时改 JSON → 重烘焙 ⇒ 对账夹具会先红,提醒同改本组常量(或按当时裁定删除兜底)。
        public const int EmergencyDeviceSwitchSeedAxisThreshold = 32768;      // "1/2"
        public const int EmergencyDeviceSwitchSeedAxisTolerance = 8192;       // "1/8"
        public const int EmergencyDeviceSwitchSeedAxisDwell = 4;
        public const int EmergencyDeviceSwitchSeedPointerThreshold = 262144;  // "4"
        public const int EmergencyDeviceSwitchSeedPointerTolerance = 65536;   // "1"
        public const int EmergencyDeviceSwitchSeedPointerDwell = 4;

        // ── story-018 · A-3 接线:GDD 点名的两个 cue 名(单一出处 = AudioEventDataSet)──
        // ⚠️ 这里**只引用常量,不另抄字面量**(ADR-024 §⑥):名字的存在性由构建期门
        //    `AudioEventTableGates.RequiredCues` 断言;运行期解析不到 ⇒ ordinal = null ⇒ 不发 cue。
        private const string CoughCueName = AudioEventDataSet.CoughCueName;
        private const string BreathCueName = AudioEventDataSet.BreathBaseCueName;

        /// <summary>
        /// 全自动装配(全新世界:计数器从 0 起、空在场登记簿、内存 blob 池、world_seed = 0)。
        /// </summary>
        /// <param name="registry">病种注册表(体征链 Step 的查表输入;null = 空表 ——
        /// 空表下体征链仍可装配,但任何 <c>DiseaseOnset</c> 都会 fail-loud「病种不在注册表内」,
        /// 属预期而非半装配)。</param>
        /// <param name="dataProvider">烘焙数据装载面(ADR-014 §五;null = EditMode 磁盘兜底)。</param>
        /// <param name="inputActions">项目级动作资产(story-009 R-9.5:生产 = BootRoot 传
        /// <c>InputSystem.actions</c>;null = 逻辑层模式 —— 通道不解析动作,Attach 非法,
        /// FeedForTest 可用。⚠️ 不得经 Addressables 装载第二实例 = 违 AC-3-A1 单实例纪律)。</param>
        /// <returns>装配完成的服务袋(绝不为 null)。</returns>
        /// <exception cref="InvalidOperationException">防御性,实际不可达(装配环节产出 null 的
        /// fail-loud 兜底断言 —— 上游构造器已抛 / 参数检查先 ANE)。</exception>
        public static CompositionRootServices Assemble(
            IReadOnlyList<DiseaseRegistryEntry> registry = null,
            IDataProvider dataProvider = null,
            InputActionAsset inputActions = null,
            AudioAssemblySeams audio = null,
            ClinicKnowledge clinicKnowledge = null,
            TelemetryAnalyticsTuning? telemetryKnobs = null,
            StructureInstanceRegistry structures = null)
            => AssembleCore(new IdAuthority(), new PresenceRegistry(), new InMemoryBlobPool(),
                            0UL, registry, dataProvider, inputActions, audio, clinicKnowledge, telemetryKnobs,
                            structures);

        /// <summary>
        /// 显式依赖注入装配(读档 / 测试用:由调用方给出发号权威、在场登记簿、blob 池与世界种子)。
        /// </summary>
        /// <param name="idAuthority">ID 发号权威(机制 A 计数器)。</param>
        /// <param name="presence">在场登记簿(EventStream 的 AC-15 有界性依赖)。</param>
        /// <param name="blobPool">不可变 blob 池(同时作编码器的写面 <see cref="IBlobSink"/>)。</param>
        /// <param name="worldSeed">世界种子(存档头;派生 patient_seed)。</param>
        /// <param name="registry">病种注册表(见 <see cref="Assemble(IReadOnlyList{DiseaseRegistryEntry})"/>)。</param>
        /// <returns>装配完成的服务袋(绝不为 null)。</returns>
        /// <exception cref="ArgumentNullException">任一引用依赖为 null(参数名具名)。</exception>
        /// <exception cref="InvalidOperationException">防御性,实际不可达(装配环节产出 null 的
        /// fail-loud 兜底断言 —— 上游构造器已抛 / 参数检查先 ANE)。</exception>
        public static CompositionRootServices Assemble(IIdAuthority idAuthority,
                                                       PresenceRegistry presence,
                                                       InMemoryBlobPool blobPool,
                                                       ulong worldSeed,
                                                       IReadOnlyList<DiseaseRegistryEntry> registry = null,
                                                       IDataProvider dataProvider = null,
                                                       InputActionAsset inputActions = null,
                                                       AudioAssemblySeams audio = null,
                                                       ClinicKnowledge clinicKnowledge = null,
                                                       TelemetryAnalyticsTuning? telemetryKnobs = null,
                                                       StructureInstanceRegistry structures = null)
        {
            if (idAuthority == null) throw new ArgumentNullException(nameof(idAuthority));
            if (presence == null) throw new ArgumentNullException(nameof(presence));
            if (blobPool == null) throw new ArgumentNullException(nameof(blobPool));
            return AssembleCore(idAuthority, presence, blobPool, worldSeed, registry, dataProvider,
                                inputActions, audio, clinicKnowledge, telemetryKnobs, structures);
        }

        private static CompositionRootServices AssembleCore(IIdAuthority idAuthority,
                                                             PresenceRegistry presence,
                                                             InMemoryBlobPool blobPool,
                                                             ulong worldSeed,
                                                             IReadOnlyList<DiseaseRegistryEntry> registry,
                                                             IDataProvider dataProvider,
                                                             InputActionAsset inputActions,
                                                             AudioAssemblySeams audio,
                                                             ClinicKnowledge clinicKnowledge,
                                                             TelemetryAnalyticsTuning? telemetryKnobs,
                                                             StructureInstanceRegistry structures)
        {
            // ── 依赖前置校验(全自动路径同样 fail-loud)──
            if (idAuthority == null)
                throw new InvalidOperationException(
                    "Assemble: idAuthority 缺失 —— 组合根拒绝返回半装配服务袋");
            if (presence == null)
                throw new InvalidOperationException(
                    "Assemble: presence 缺失 —— 组合根拒绝返回半装配服务袋");
            if (blobPool == null)
                throw new InvalidOperationException(
                    "Assemble: blobPool 缺失 —— 组合根拒绝返回半装配服务袋");

            // ── 载荷面:池(写面 IBlobSink)→ 编码器(第七抽象点)/ 解码器(第八抽象点)──
            // 池与编解码器一体接线(ADR-029 乙案 / ADR-031 §③):Sim 侧只见 IPayloadEncoder /
            // IPayloadDecoder,构造点只在本装配可见 Sim.Codec —— 案 A 的全部意义。
            // 编解码共用同一池(同池读写,无第二真源;ADR-031 §②「池依赖在实现的构造函数」)。
            IPayloadEncoder encoder = new PayloadEncoder(blobPool);
            IPayloadDecoder decoder = new PayloadDecoder(blobPool);

            // ── 事件流:Kind → StreamId 纯函数路由 + Seq 发号 + AC-15 有界性 ──
            EventStream stream = new EventStream(idAuthority, presence);

            // ── 病人创建器:9 的 DiseaseOnset 写者(ADR-030 §③)──
            PatientSpawner spawner = new PatientSpawner(idAuthority, stream, encoder, worldSeed);

            // ── 病种注册表 ────────────────────────────────────────────────
            // 优先级:① 调用方显式传入(EditMode/PlayMode 测试注入合成 fixture)·
            //        ② 无 ⇒ 组合根自带最小内联注册表(接线载体,非数值真源)。
            // ⚠️ **不可合并为「fallback ∪ caller」**:真表到位后,fallback 的 DiseaseId=1
            //    可能与真表撞号(R1-01 唯一性违例)⇒  caller 给表时 fallback 必须**整体让位**。
            //    2026-10-10 实测事故:VerticalSliceTest 显式传 9001 fixture,而体征链仍持
            //    fallback(只有 DiseaseId=1)⇒ `DiseaseOnset disease_id=9001 不在注册表内`
            //    fail-loud,两条核心循环测试红。真表未建前,该分支只由测试走。
            var fallbackRegistry = ResolveRegistry(registry);

            // ── 体征链核心(批次 C):apply 驱动 + 投影桥 + IVitalsQuery 生产实装 ──
            // 它**同时**需要 EventStream/Sim(状态与求值)与 Sim.Codec(载荷解码)
            // ⇒ 只能在本装配(案 A)构造;客户端侧不装配(ADR-005 主机唯一 Step)。
            DiseaseVitalsService vitals = new DiseaseVitalsService(
                stream, blobPool, fallbackRegistry, worldSeed);

            // ── 病人出现驱动(sprint-05 T1.1):把 spawner 接进 tick 边沿序列 ──
            // 原状:spawner 在服务袋里但**零生产调用方**(F-6 复评实测)⇒ 运行期无病人。
            // 驱动 = 每 tick 边沿按节奏调 SpawnNext;节奏 = 每 N tick 一个病人(见驱动类头注)。
            var patientDriver = new PatientAppearedDriver(spawner, presence, fallbackRegistry);

            // ── 病例开账写者 + 驱动(sprint-05 T1.2):把 CaseOpenWriter 接进 tick 边沿序列 ──
            // 原状:CaseOpenWriter 零生产调用方 ⇒ 运行期链可达性 0%。
            // 驱动 = 每 tick 边沿对在场且未开案的病人自动开案。
            var caseOpenWriter = new CaseOpenWriter(stream, presence, encoder, stream.Events);
            var caseDriver = new CaseOpenedDriver(caseOpenWriter, presence);

            // ── DC-4 处置轴闭集(2026-10-20 真源接线)──
            // 病因:`disease_action_axis` 的 `actions` 段编辑期已被消费
            //   (`PrescriptionActionIdRegistry.RealActionIds()` 读 `owner=="11"` 段作 DC-2 闭集),
            //   但**运行期零读点** ⇒ `EmergencyActionSchema.ValidateActionId` 长期是
            //   `Enum.IsDefined` 的影子(枚举注释自己点名「9 的轴新增 id ⇒ 影子会错误拒绝」)。
            // 现:装配层读产物 → 按 `owner=="10"` 过滤 → 注入 `HostEmergencyProcessor`。
            // ⚠️ **为什么在装配层过滤而不是在 Processor 里**:`Sim` 住门 A(`noEngineReferences`),
            //    够不着 `Gameplay.Presentation.DiseaseActionAxisCookedCodec`;
            //    Processor 只收一个 `ISet<int>`,与 `IPayloadEncoder` 同构(零新增程序集边)。
            // ⚠️ dataProvider == null(EditMode / 15+ 既有调用方)⇒ 传 null ⇒ 回退枚举影子,
            //    与接线前**逐位相同**。
            System.Collections.Generic.ISet<int> emergencyActionAxis = null;
            if (dataProvider != null)
            {
                DiseaseActionAxisDataSet axis = dataProvider.Load<DiseaseActionAxisDataSet>();
                var owner10 = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < axis.Actions.Count; i++)
                    if (axis.Actions[i].Owner == "10")
                        owner10.Add(axis.Actions[i].ActionId);
                emergencyActionAxis = owner10;
            }

            // ── 急救链处理器 + 驱动(sprint-05 T1.3 → sprint-06 G4.1 退役)──
            // G4.1(2026-10-11 用户裁定「急救手动化」):自动触发分支已从
            // EmergencyAttemptDriver 删除(退役壳零事件);触发权归玩家,
            // 手动入口 = EmergencyManualDriver(BootRoot 生成玩家后调用)。
            var emergencyProcessor = new HostEmergencyProcessor(stream, idAuthority, encoder,
                actionAxis: emergencyActionAxis);
            var emergencyDriver = new EmergencyAttemptDriver();
            // cause 判据的输入面(TD-epic-01 第八片):空 = 夹具默认「玩家选择」;
            // 生产 BootRoot 以 3 的 DeviceStateManager 造 EmergencyDeviceReadiness 适配注入
            // (启动序 4.2 —— 设备态须先挂接才能读到在场设备)。
            var emergencyManual = new EmergencyManualDriver(emergencyProcessor, presence);

            // ── 病人最小可见实体(sprint-05 T2.0):给运行期病人生成占位视觉 ──
            // 病因(playtest 2026-10-10 §6):病人在场但**零视觉实体** ⇒ 人工面看不到病人,
            //   核心循环入口不可感知,第二轮 playtest 无法覆盖「病人出现」。
            // 只读在场集 spawn 占位胶囊体;不写流、不持状态(ADR-013 §9 C3)。
            var patientVisuals = new PatientVisualSpawner(presence);

            // ── tick 驱动(ADR-005 抽象点之一 · G2.2)──
            // tick 源全案唯一 ⇒ 由组合根创建并暴露,BootRoot 从服务袋取(不再自 new);
            // 墨龄/G2 等都从袋内同一 source 读,保证「当前 tick」唯一口径。
            var tickDriver = new SimTickDriver();

            // ── 42 侧只读数源(T2.1 + G1.4 + G2.2):把拟物 UI 接到唯一浮点出口 ──
            // 病因(sprint-05 §1):`SkeuoRuntimeDriver` 两处 TODO ⇒ 42 是空壳,
            //   playtest 人工面看不到脉案 / 体征。本桥 = 42 与体征链的**唯一**接点,
            //   实现住本装配(ADR-025 §① 案 A 同理由:42 不可反向引用装配层)。
            // ⚠️ G1.4:revealed 真源 = 查体分册快照(乙案派生态);
            //   G2.2:InkAgeTicks = 当前 tick − 最近 Judgment Tick(病例流事件视图)。
            // ⚠️ S1.2(sprint-07):词表 / 门槛定表经 **IDataProvider 从 data-core 装载**
            //   (ADR-014 §五;乙案走真 Addressables —— 用户 2026-10-11 裁定)。装配入口
            //   provider 注入 ⇒ 测试可传合成 fixture(禁 Fake 惯例);provider 缺 ⇒ 判据红。
            var examinationLedger = new ExaminationLedger();
            // ⚠️ casebookSource 的构造**后移**到 seenDisease / lexiconBook 之后
            //   (sprint-08 S1.1 件 E:病名册条目集经 SeenLexiconIds 接入 42 只读源)。

            // 查体求值的**求值输入**(真烘焙产物):词条 34 行 + 门槛 61 档。
            // ⚠️ 装载策略(2026-10-11 用户裁定「乙案 + EditMode 直读兜底」):
            //   · dataProvider != null(生产:BootRoot 传 DataCorePreloader.Provider)
            //     ⇒ 真 Addressables(乙案;组 gitignored 本地登记);
            //   · dataProvider == null(EditMode / 测试:15+ 既有 Assemble 调用方不传)
            //     ⇒ DiagnosisCookedDisk 从 Assets/DataCooked 直读**同一份产物** +
            //     同一个 codec 解码 —— 真产物 / 零 Fake / 零 Addressables。
            //   ⚠️ 两条路径读同一份 baked 字节 + 同一解码器 ⇒ 无第二真源。
            DiagnosisSignDataSet signTable;
            DiagnosisReadFloorDataSet readFloorTable;
            if (dataProvider != null)
            {
                signTable = dataProvider.Load<DiagnosisSignDataSet>();
                readFloorTable = dataProvider.Load<DiagnosisReadFloorDataSet>();
            }
            else
            {
                signTable = DiagnosisCookedDisk.LoadSigns();
                readFloorTable = DiagnosisCookedDisk.LoadReadFloor();
            }
            var examineRows = signTable.Rows;
            var readFloor = readFloorTable.Table;

            // 查体入口驱动:求值输入 = 真烘焙产物(S1.2 装载面)
            //
            // ⚠️ queryLevel = 1(2026-10-12 第五轮 playtest 修复):原占位 0 使真词表
            //   双条件与门 `named = skill ≥ tier_named`(tier_named ∈ {1,10,20})恒 false
            //   ⇒ 玩家按 E 查体后值区**恒空**(Q1 实测:按 E 零变化)。
            //   初始档 1 = 20 条 tier_named=1 词可读;后续熟练度成长(30)覆盖本值
            //   —— 初始档数值归数值轮,机制面从「恒 0」解冻。
            // ⚠️ 本值同时是听诊档位的**唯一出处**(B 轮 2026-10-20):音频档位 = `TierIndex(本值)`,
            //    下面 audio 段的 `AudioTierDriver` **引用本变量**,不得另写字面量 1(两处 = 两处分叉)。
            int queryLevel = 1;
            var examineDriver = new ExamineDriver(vitals, examinationLedger, queryLevel: queryLevel);
            examineDriver.SetLexicon(examineRows, in readFloor);

            // ── 「查体 ⇒ 见过病 D」投影器(sprint-08 S1.1 件 B · 2026-10-12)──
            // 病名册条目集的数据源:查体完成 ⇒ 写一条 SkillGrown(诊断, 病种 D)落病史流。
            // ⚠️ NoveltyTracker = 运行期加速缓存(真源在病史流,ADR-026 ⑥);冷却期取默认
            //   24000 tick(= 20 min @ 20 Hz;数值轮可调,见 NoveltyTracker.CooldownTicks)。
            // ⚠️ 写路径 = 装配层写者形态(与 AssemblySkillGrowthPort 同构;不走 8 的
            //   DiagnosisGrowthGate —— 该门需 IEventAuthority 生产实装,归 P1b/52,见类头注)。
            var noveltyTracker = new NoveltyTracker();
            var seenDisease = new SeenDiseaseProjector(vitals, noveltyTracker, stream, encoder);

            // ── 病名册条目源(sprint-08 S1.1 件 C/D · 2026-10-12)──
            // 词表 = S1.0 已烘产物(`case_judgment_lexicon.cooked.bytes`),本批接入装载。
            // 装载策略同 S1.2(provider != null ⇒ Addressables 乙案;否则 DiagnosisCookedDisk
            // 磁盘兜底 —— 同一份字节 + 同一 codec,零第二真源)。
            JudgmentLexiconDataSet lexiconTable;
            if (dataProvider != null)
                lexiconTable = dataProvider.Load<JudgmentLexiconDataSet>();
            else
                lexiconTable = DiagnosisCookedDisk.LoadJudgmentLexicon();
            var lexiconBook = new LexiconBookSource(lexiconTable.Rows);

            // ── 24「布局 → 房间类型」匹配链(TD-building-01 读侧 · 2026-10-21)──────────
            // 病因:链上**四个零件**今日都只被测试直接构造,生产装配零落点 ——
            //   ① `IPlacedStructureReadView` 生产实现(现 = `StructureInstanceReadView`);
            //   ② `IModuleCatalog` 生产实现(现 = `BuildModulesCatalog`,读 `build_modules.cooked`);
            //   ③ `ClinicFurnitureTraitsSource`(24 的 `type(c)` 来源)零生产构造点;
            //   ④ `ClinicRoomExtraction` / `ClinicRoomMatcher` 两条纯函数零生产调用点。
            // ⇒ 本段把它们接成 `ClinicRoomMatchingService`(装配层胶水,零新增判据)。
            //
            // ⚠️ **`structures` 从哪来**:23 的 `StructureInstanceRegistry` 今日在生产树**零构造**
            //    (`new World(` 同样零构造,见 `TD-building-01` 实测 ①)。本装配**不自建**一个 ——
            //    那会造出「装配层私有的世界」,与将来的 `World` 实例分叉(两份注册表 = 两份真源)。
            //    故:
            //      · 调用方给了(`BootRoot` 的 World 场景装配轮 / 测试注入真 `World.Structures`)
            //        ⇒ 接真注册表;
            //      · 未给 ⇒ 本段**不构造**匹配服务(`ClinicRoomMatching = null`),如实可观测。
            //    ⇒ 缺口从「静默 Fake」变成「null 可判」,且不新增第二真源。
            // ⚠️ 三份烘焙数据的装载策略同 S1.2:provider != null ⇒ Addressables;
            //    provider == null ⇒ `DataCookedDisk` 直读**同一份产物** + 同一 codec。
            // ⚠️ 装载失败(产物缺失 / 解码抛)⇒ **不吞**:本段在装配期抛(E-13 同口径)。
            //    ⚠️ 但只在 `structures != null` 时才装载 —— 未接世界时不该因为「产物缺失」
            //    让 77 处既有 `Assemble()` 调用方集体转红(那会把本接线变成全局回归)。
            ClinicRoomMatchingService clinicRoomMatching = null;
            if (structures != null)
            {
                BuildModulesDataSet buildModules = dataProvider != null
                    ? dataProvider.Load<BuildModulesDataSet>()
                    : DataCookedDisk.LoadBuildModules();
                ClinicFurnitureDataSet clinicFurniture = dataProvider != null
                    ? dataProvider.Load<ClinicFurnitureDataSet>()
                    : DataCookedDisk.LoadClinicFurniture();
                ClinicRoomDataSet clinicRooms = dataProvider != null
                    ? dataProvider.Load<ClinicRoomDataSet>()
                    : DataCookedDisk.LoadClinicRoom();

                clinicRoomMatching = new ClinicRoomMatchingService(
                    structures,
                    new BuildModulesCatalog(buildModules),
                    clinicFurniture.Catalogue,
                    clinicRooms.Table);
            }

            // ── 42 侧只读源(sprint-08 S1.1 件 E 起:病名册条目集接入)──
            // 依赖已备齐(ledger / 病例流 / tick / seenDisease / lexiconBook)⇒ 此处构造。
            // 玩家 actorId 由 BootRoot 生成玩家后 SetViewer(装配期未知 —— 与
            // PrescriptionPageHost.SetAuthor 同款后置形态)。
            // ⚠️ 命令路由(casebookCommands)在下方处方链之后才构造(需复用方笺页的
            //   JudgmentWriter)—— 故此处**暂传 null**,待路由就绪后经 BindCommands 接回。
            var casebookSource = new CasebookVitalsSource(examinationLedger,
                stream.Events, decoder, tickDriver, seenDisease, lexiconBook);

            // ── 处方链(G3 · 2026-10-11 用户裁定「最小生产端口实装」)──
            // 21a(换算/药物档案)+ 20(库存面)+ 30(成长)三端口最小实装 + 驱动,
            // 把 PrescribeFlow(11)接进运行期。⚠️ 完整 21a/20 落地后只换端口实现,签名零改动。
            //
            // ⚠️ **2026-10-20 读方接线**:`prescription_actions` 产物此前**零生产读者**
            //    (`_entries` = 硬编码单条 · `doseBase` = 写字面量 65536)。现:
            //    dataProvider != null ⇒ **一次** `Load<PrescriptionActionsDataSet>()`,
            //    行集喂驱动、`DoseBase` 喂端口;dataProvider == null ⇒ 原状(零行为变化)。
            //    ⚠️ 本轮**行为逐值不变** —— 产物唯一条目 (salicylic_acid, 10, symptomatic)
            //       与回退条目同形;`DOSE_BASE` 恰 = 65536。判据是「源真的换了」。
            PrescriptionActionsDataSet? prescriptionActions = dataProvider != null
                ? dataProvider.Load<PrescriptionActionsDataSet>()
                : (PrescriptionActionsDataSet?)null;

            var drugCatalog = new MinimalDrugCatalog();                 // 21a 最小换算表
            var portionsStore = new MinimalPortionsStore();            // 20 最小库存面
            var skillGrowthPort = new AssemblySkillGrowthPort(stream, encoder, prescriptionLevel: 0);
            var prescribePorts = new PrescribePorts(drugCatalog, portionsStore,
                presence, skillGrowthPort,
                doseBase: prescriptionActions?.DoseBase ?? 65536);      // 产物优先;回退字面量(数值轮注记)
            var prescribeDriver = new PrescribeRequestDriver(stream, encoder,
                prescribePorts, isHost: true,
                prescriptionRows: prescriptionActions?.Rows,            // null ⇒ 回退硬编码单条
                // 甲档剩余项(2026-10-20):档数上限此前读常量 9(窄化前的旧建议),
                // 产物 `MAX_DOSE_DETENTS`(= 7,GDD 已裁)零读者 ⇒ 上限读数与真源脱钩。
                // 现产物优先;null ⇒ 回退常量(两者现已同为 7)。
                // ⚠️ 不改行为:当前药 dose_range = (1,5) ⇒ 5 落点,上限 7/9 都远大于它。
                maxDoseDetents: prescriptionActions?.MaxDoseDetents);

            // ── 交互契约表读方接线(2026-10-20)──
            // ⚠️ **`KindPriorityTable` 的最终运行期消费者 = 4 的选择器,而 `InteractionSelector`
            //    当前在生产侧**零构造点**(实测:非 Tests 面只有 `ModalGate` 的形参)。
            //    ⇒ 本注入接的是**契约层读点**(选择器的第二键查表 + 4-DC-3 校验器),不是
            //    「运行期已有实例在查」。选择器的生产装配归 4 的装配轮 —— 本行只保证
            //    「到那时读的是产物,不是字面量」。
            // ⚠️ 静态注入 ⇒ 须在**任何** `PriorityOf` 调用前设好;跨 Play 轮残留由
            //    `DataCorePreloader.ResetForPlayModeTests()` 清(与 AudioSettingsRegistry 同款)。
            if (dataProvider != null)
                KindPriorityTable.Provider = () => dataProvider.Load<InteractionKindDataSet>().Rows;

            // ── 急救候选列表生产实例宿主(S3.1 · 2026-10-11)──
            // M2 形态件③(候选动作列表)的「运行期有载体」半边:实例化 + 喂 10 最小动作
            // 标签(止血/通气)。动作判定 / Idle 时序 / 点击行为归 10 + 数据绑定轮 ——
            // 本宿主只回答「列表实体存在 + 跳过入口可用」(story-023 边界①)。
            var emergencyCandidates = new EmergencyCandidateRuntime();

            // ── 急救直读通道运行期装配(story-009 · 2026-10-14)──
            // 通道(3 的状态机)经可选 inputActions 构造:生产 = BootRoot 传 InputSystem.actions
            // (R-9.5 原稿的 Addressables 路径被实测推翻 —— InputSystem.actions 是项目级动作
            // 资产、git-tracked,且 AC-3-A1 单实例纪律禁止经 Addressables 再装载第二实例);
            // 测试 / 纯逻辑 = null(逻辑层模式,Attach 非法、FeedForTest 可用)。
            // Idle 门回调指向候选列表可见性(R-9.3:Idle 复现 / 非 Idle 隐藏)。
            var emergencyChannel = new EmergencyDirectReadChannel(
                inputActions, EmergencyAxialScale, EmergencyDzMag, EmergencyMagMax);
            var emergencyChannelDriver = new EmergencyChannelDriver(
                emergencyChannel, emergencyManual, () => tickDriver.CurrentTick,
                onIdleGate: visible => emergencyCandidates.SetVisible(visible));

            // 方笺页种子库存(20 最小面 · C3-M3):**装配期一次** Grant 玩家 0 ——
            // 测试 authorId 可任意(测试自 Grant,见 prescription_page_host_test 注);
            // 生产 BootRoot 第 4 步 SetAuthor(恒 0)消费同一种子。
            // ⚠️ 移除义务:真 20 库存落地后删(见 sprint-07 §4 C3 修复注)。

            // ── 方笺页接线宿主(S2.3/S2.4 · 2026-10-11)──
            // 落笔 → JudgmentWriter.TryRecord(墨龄)· 开方 → PrescribeRequestDriver.TryPrescribe。
            // 玩家 actorId 由 BootRoot 生成玩家后 SetAuthor(BootRoot 启动序第 4 步之后)。
            // ✅ 2026-10-12 C3-M3:种子**装配期一次**(Grant 1)—— 原宿主内逐次 Grant
            //   自证库存(HasPortions 恒真)且失败可无限重复致库存增长;现库存前置真生效。
            portionsStore.Grant(0, MinimalDrugCatalog.DrugKey, 1);   // 种子玩家 0(BootRoot SetAuthor 同步)
            var prescriptionPage = new PrescriptionPageHost(
                stream, encoder, stream.Events,
                prescribeDriver, portionsStore, tickDriver,
                initialAuthorId: -1);
            // ⚠️ 种子玩家补发(见上方 `portionsStore.Grant(0,…)`):id 0 的种子已在
            //   装配期发;若未来种子改随 SetAuthor 走,此注为登记点。C3-M3 修复完成。

            // ── 脉案命令路由(sprint-08 S1.1 件 F2/G · 2026-10-12)──
            // 42 病名栏 Submit(ICasebookCommandSink)⇒ JudgmentWriter(Recorded / Revised,
            // OQ-CB-5 甲循环落笔)。⚠️ **复用方笺页的同一 JudgmentWriter 实例**
            // (prescriptionPage.Writer)—— 作者同步(SetAuthor)只在一处,不新开第二写者。
            // ⚠️ sprint-08 S2(2026-10-12):第三参 = 方笺页宿主 —— 戥子落点(SubmitDose)
            //   的开方写路径。与 Writer 同源(宿主即 Writer 的持有者)⇒ 单写者、单作者真源。
            var casebookCommands = new CasebookCommandRouter(
                prescriptionPage.Writer, () => tickDriver.CurrentTick, prescriptionPage);

            // ── 音频链装配(story-018 · A-1/A-4)─────────────────────────────
            // 池 → 渲染目标 → sink(ADR-028 ①「池唯一所有者 = 44」;单池,不建第二个)。
            // ⚠️ 缝为 null(62 处既有 EditMode 调用)⇒ sink 走 NullRenderTarget:
            //    **契约语义(句柄 / 档位)仍成立**,只是没有声音 —— 与装配前的行为一致。
            //    生产路径(BootRoot)传真缝 ⇒ 真播放 + 真总线(A-6)。
            var audioPool = new AudioSourcePool(busResolver: audio?.Buses);

            // ── 空间化链接线(S15-4 · TD-audio-028 · 用户裁定四项)──────────────
            // 导演**共享**上面那个池(AC-018-4 单池不变量 / ADR-028 ① —— **禁**走自建池重载)。
            // ⚠️ 格边长 = `AudioAssemblySeams.LatticeSize`(**此前零读取方的死字段**,本轮起成为唯一入参);
            //    它今天的 1f 是 **P0 占位**,正式值归系统 6(`LatticeToWorld` 头注明令 44 不自造)。
            // ⚠️ 遮挡输入**本轮恒 false**(用户裁定):真 raycast 未实现,登记 `TD-audio-028` 残留。
            float latticeSize = audio?.LatticeSize > 0f
                ? audio.LatticeSize : AudioTuning.LatticeSizePlaceholder;
            SpatializationDirector spatialDirector = null;
            ICueRenderTarget renderTarget;
            if (audio != null && audio.HasAny)
            {
                spatialDirector = new SpatializationDirector(latticeSize, audioPool);
                var sources = new AudioSourceRenderTarget(audioPool, spatialDirector, latticeSize,
                                                          // story-020:按整条 cue 解析 —— 变体选择键含
                                                          // 患者身份(cue.Source),只给 ordinal 分不开病人。
                                                          cue => audio.Clips != null
                                                                  ? audio.Clips.ResolveCue(cue) : null,
                                                          cue => audio.Clips != null
                                                                  ? audio.Clips.BusOf(cue) : null,
                                                          // 遮挡:用户 2026-10-18 裁定「本轮恒 false,登记缺口」
                                                          occludedFor: () => false);
                renderTarget = sources;
            }
            else
            {
                renderTarget = new NullRenderTarget();
            }
            var audioSink = new CueRenderSink(renderTarget);
            audioSink.SetTier(TierSource.Local);      // 裁定 D-A:恒本机档

            // ── 听诊精度档位驱动(B 轮 2026-10-20 · 补 T-1 接线空白)────────────
            // `TierFilterDriver` / `AudioMixerParameterSink` 此前**生产零构造** ⇒ `tier_map`
            // 五列的运行期承载为零。本段:档位来源 = 诊断等级(与 `ExamineDriver` **同一** queryLevel)
            // ⇒ `TierIndex` ⇒ `min(·,2)`(4→3 **已裁** = 甲案上钳,见 AudioTierMapping 注 /
            //   GDD F-44.1 段末补注;`TD-audio-035` 该子项已结案)。
            // ⚠️ 档变才 `SetTargets`(每帧调会重置 ramp);mixer 句柄 null ⇒ fail-loud 一次并禁用。
            // ⚠️ tick 边沿链**不调**本驱动:它按帧推进 ramp,由 BootRoot 帧泵调 `Tick(Time.deltaTime)`。
            AudioTierDriver audioTierDriver = null;
            AudioEventDataSet? tierTable = audio?.Table;
            if (tierTable.HasValue && tierTable.Value.TierMap != null)
            {
                var tierFilter = new TierFilterDriver(
                    new AudioMixerParameterSink(PlayerBusVolumeInitializer.MixerResolver()));
                audioTierDriver = new AudioTierDriver(
                    () => DiagnosisReadFloorEvaluator.TierIndex(queryLevel),
                    tierTable.Value.TierMap, new TierFilterSinkAdapter(tierFilter));
                audioTierDriver.BindMixer(PlayerBusVolumeInitializer.MixerResolver != null);
            }

            // ── 病人 cue 驱动(story-018 · AC-018-6 / A-3 的调用方)──────────────
            // 13 的 `PatientCueSchedule.Decide` 与 `PatientCueEmit.Emit` 此前**双双零调用方**
            // (A-3)—— 本驱动即那个调用方:每 tick 边沿对每个在场病人跑一次调度,按结果
            // 起停呼吸层循环 / 发射一次性 cue。
            // ⚠️ cue ordinal **从事件表按名解析**,不写 `(int)CueKind`(见 PatientCueDriver 头注:
            //    后者今日恰好是 0,是**巧合不是契约**)。
            // ⚠️ 缝未接(62 处既有 EditMode 调用)⇒ 两个 ordinal 均为 null ⇒ 驱动仍装配
            //    (调度逻辑可测),但不发任何声音 —— 与 AudioRenderAssembled 同一诚实口径。
            // ── `knowsClinic` 注入缝的真值接线(story-021 · AC-021-1 · TD-audio-033 ①)──
            // F-13.2:`KnowsClinic(p) := ClinicKnownByRegion(HomeRegion(spawn_anchor(p)))`。
            // 三件事实全部来自三源(ADR-016 §一):生态区 = 6 的烘焙定义;医馆 POI = 6 的定义侧;
            // 玩家建造 = 23 的世界流 `StructurePlaced`。**装配层不发明其中任何一项**。
            // ⚠️ **今日口径(如实登记)**:生态区注册表在本装配内**尚未有装载面**
            //    (ADR-022 §三 的 `world_ecozones.cooked` 未接),POI / 建造查询缝亦未接
            //    ⇒ `EcozoneOf` 对任何格都回 `EcozoneId.None`、两缝恒假 ⇒ `KnowsClinic` **恒假**,
            //    与 story-021 之前的硬编码 `false` **行为等价**。差别是**形状**:真值源到位后
            //    只需给本处三个注入缝,`PatientCueDriver` 与本装配的调用点均不再改
            //    (债项 `TD-audio-033` ② 仍 open:两份行为态分叉)。
            // 注入优先(测试 / 读档轮可从真装载面给);null ⇒ 组合根自建**空**生态区表
            // (装载面未接时的今日形态 —— `EcozoneOf` 恒 `None`)。
            ClinicKnowledge knowledge = clinicKnowledge ?? new ClinicKnowledge(
                new RegistryClinicKnowledgeSource(new EcozoneRegistry()));
            var clinicDirectory = new ClinicKnowledgeDirectory(
                knowledge,
                // 锚点格 = 在场登记簿的逻辑格(不读表现态位置,ADR-016 §三)。
                p => presence.TryGetCell(p, out WorldPos anchor)
                        ? (true, anchor) : (false, default));

            var behaviorDirector = new PatientBehaviorDirector(vitals, BehaviorBands.Default);
            var patientCueDriver = new PatientCueDriver(
                audioSink, vitals, presence, behaviorDirector,
                coughCue: audio != null && audio.TryResolveCue(CoughCueName, out int coughOrdinal)
                    ? coughOrdinal : (int?)null,
                breathCue: audio != null && audio.TryResolveCue(BreathCueName, out int breathOrdinal)
                    ? breathOrdinal : (int?)null,
                knowsClinic: clinicDirectory.KnowsClinic,
                hasCourse: p => vitals.Courses.TryGet(p, out _));
            // 后置接回:命令路由就绪后把档位面接进 42 只读源(构造序所限,见本源 BindCommands 注)
            casebookSource.BindCommands(casebookCommands);
            // 后置接回(2):戥子档数真源 —— 处方驱动(构造序所限,见 BindPrescribeDriver 注)
            casebookSource.BindPrescribeDriver(prescribeDriver);

            // ── fail-loud 兜底:S1.2 诊断数据集非空(词表/门槛装载态)──
            // ⚠️ 「空行」在**运行期**是诚实形态(未查 = 空行,禁假数据);但**装配期**空数据集
            //    = 装载失败被吞 ⇒ fail-loud(与 DiseaseOnset 不在注册表同口径)。
            if (signTable.Rows == null || signTable.Rows.Count == 0)
                throw new InvalidOperationException(
                    "Assemble: DiagnosisSignDataSet 解码出 0 行词条 —— 烘焙产物 / 装载路径异常," +
                    "拒绝返回半装配服务袋(诊断装载面,ADR-014 §五 / E-13 同口径)");

            // ── fail-loud 兜底 ──
            if (stream == null)
                throw new InvalidOperationException("Assemble: EventStream 装配产出 null");
            if (encoder == null)
                throw new InvalidOperationException("Assemble: PayloadEncoder 装配产出 null");
            if (decoder == null)
                throw new InvalidOperationException("Assemble: PayloadDecoder 装配产出 null");
            if (spawner == null)
                throw new InvalidOperationException("Assemble: PatientSpawner 装配产出 null");
            if (vitals == null)
                throw new InvalidOperationException("Assemble: DiseaseVitalsService 装配产出 null");
            if (patientDriver == null)
                throw new InvalidOperationException("Assemble: PatientAppearedDriver 装配产出 null");
            if (caseOpenWriter == null)
                throw new InvalidOperationException("Assemble: CaseOpenWriter 装配产出 null");
            if (caseDriver == null)
                throw new InvalidOperationException("Assemble: CaseOpenedDriver 装配产出 null");
            if (emergencyProcessor == null)
                throw new InvalidOperationException("Assemble: HostEmergencyProcessor 装配产出 null");
            if (emergencyDriver == null)
                throw new InvalidOperationException("Assemble: EmergencyAttemptDriver 装配产出 null");
            if (emergencyManual == null)
                throw new InvalidOperationException("Assemble: EmergencyManualDriver 装配产出 null");
            if (emergencyChannel == null)
                throw new InvalidOperationException("Assemble: EmergencyDirectReadChannel 装配产出 null");
            if (emergencyChannelDriver == null)
                throw new InvalidOperationException("Assemble: EmergencyChannelDriver 装配产出 null");
            if (patientVisuals == null)
                throw new InvalidOperationException("Assemble: PatientVisualSpawner 装配产出 null");
            if (casebookSource == null)
                throw new InvalidOperationException("Assemble: CasebookVitalsSource 装配产出 null");
            if (examinationLedger == null)
                throw new InvalidOperationException("Assemble: ExaminationLedger 装配产出 null");
            if (examineDriver == null)
                throw new InvalidOperationException("Assemble: ExamineDriver 装配产出 null");
            if (seenDisease == null)
                throw new InvalidOperationException("Assemble: SeenDiseaseProjector 装配产出 null");
            if (lexiconBook == null)
                throw new InvalidOperationException("Assemble: LexiconBookSource 装配产出 null");
            if (tickDriver == null)
                throw new InvalidOperationException("Assemble: SimTickDriver 装配产出 null");
            if (drugCatalog == null)
                throw new InvalidOperationException("Assemble: MinimalDrugCatalog 装配产出 null");
            if (portionsStore == null)
                throw new InvalidOperationException("Assemble: MinimalPortionsStore 装配产出 null");
            if (skillGrowthPort == null)
                throw new InvalidOperationException("Assemble: AssemblySkillGrowthPort 装配产出 null");
            if (prescribePorts == null)
                throw new InvalidOperationException("Assemble: PrescribePorts 装配产出 null");
            if (prescribeDriver == null)
                throw new InvalidOperationException("Assemble: PrescribeRequestDriver 装配产出 null");
            if (emergencyCandidates == null)
                throw new InvalidOperationException("Assemble: EmergencyCandidateRuntime 装配产出 null");
            if (prescriptionPage == null)
                throw new InvalidOperationException("Assemble: PrescriptionPageHost 装配产出 null");
            if (casebookCommands == null)
                throw new InvalidOperationException("Assemble: CasebookCommandRouter 装配产出 null");
            if (audioSink == null)
                throw new InvalidOperationException("Assemble: CueRenderSink 装配产出 null");
            if (audioPool == null)
                throw new InvalidOperationException("Assemble: AudioSourcePool 装配产出 null");
            if (patientCueDriver == null)
                throw new InvalidOperationException("Assemble: PatientCueDriver 装配产出 null");

            return new CompositionRootServices(stream, idAuthority, blobPool, encoder, decoder,
                                               fallbackRegistry,   // registry → 51 TelemetryReporter
                                               presence, spawner, patientDriver, caseDriver,
                                               emergencyDriver, emergencyManual,
                                               emergencyChannelDriver, patientVisuals,
                                               worldSeed, vitals,
                                               casebookSource, examinationLedger, examineDriver,
                                               seenDisease, lexiconBook,
                                               tickDriver, prescribeDriver, prescribePorts,
                                               portionsStore, emergencyCandidates,
                                               prescriptionPage, casebookCommands,
                                               audioSink, audioPool, spatialDirector,
                                               renderTarget as AudioSourceRenderTarget,
                                               // ⚠️ **单一出处**:标志由**已构造出的那个对象**派生,
                                               // 不重算一遍 `audio != null && audio.HasAny` ——
                                               // 重算 = 同一判断的第二份拷贝,两者可分叉(证伪轮 F4 实测:
                                               // 把上面的三元逼成 `NullRenderTarget` 而此处仍重算 ⇒ 标志照旧 true
                                               // ⇒ `test_ac018_3_assemble_withSeams_...` **不转红** = 假绿)。
                                               renderTarget is AudioSourceRenderTarget,
                                               patientCueDriver, behaviorDirector,
                                               audioTierDriver, telemetryKnobs,
                                               clinicRoomMatching);
        }

        /// <summary>
        /// 病种注册表解析 **当前实际的决策**(经 T1.1–T1.5 五轮驱动 + 5 PlayMode 全量跑验证):
        /// caller 显式传表 ⇒ 用 caller 的(合成 fixture 进 EditMode/PlayMode 测试);
        /// 不传(null)⇒ 组合根自带最小内联注册表(DiseaseId=1,数值轮未开前的接线载体)。
        /// <para><b>为何不是 union</b>:fallback 的 DiseaseId=1 是占位,与真表可能撞号
        /// (R1-01 唯一性触构建失败)⇒ caller 有表时 fallback **整体让位**,不并表。</para>
        /// <para><b>为何不是「caller 表 + fallback 兜底追加」</b>:会让「注册表须覆盖全部在流病种」
        /// 的 fail-loud 判据静默退化(测试永远测不到真表缺病种的情形)。</para>
        /// </summary>
        private static IReadOnlyList<DiseaseRegistryEntry> ResolveRegistry(
            IReadOnlyList<DiseaseRegistryEntry> callerRegistry)
            => callerRegistry ?? CreateFallbackRegistry();

        /// <summary>
        /// 最小内联病种注册表 —— sprint-05 T1.1 的接线载体。
        /// <para><b>为何存在</b>:体征链 Step 的求值要查注册表(<see cref="DiseaseRegistryEntry"/>),
        /// 但 <c>disease_registry.json</c> 未建(数值轮未开 —— 建全表 = 被迫填占位值,
        /// 而 9 的 17 条区间校验会面对假数据 = GDD 点名的「判据对合法输入类误判」)。
        /// 没有注册表时体征链 fail-loud「病种不在注册表内」,运行期病人无法建档。</para>
        /// <para><b>它是什么</b>:1 个病种条目的最小接线载体,满足 R1-17「至少 1 个病种」与
        /// <see cref="DiseaseVitalsService"/> 的 Scale &gt; 0 要求;数值全部取**语义中性**
        /// (严重度/传染性等 = 1 或 0,不影响接线判定)。</para>
        /// <para><b>它不是什么</b>:数值真源 —— 数值归用户;真表到位后只需替换本方法体,
        /// 装配签名与下游零改动。</para>
        /// </summary>
        private static IReadOnlyList<DiseaseRegistryEntry> CreateFallbackRegistry()
        {
            var entry = new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "fallback",
                Polarity = 2,            // 平
                Severity = 1,
                Contagion = 0,
                Lethality = 0,
                TreatmentDifficulty = 1,
                // ⚠️ 2026-10-14 订正(承 CLI 探针 `e_probe_chain_test` 实证):
                //    本字段**已不再决定 position 的可读时机** —— Curve != null ⇒ 求值走
                //    F1 三分支,incubation 取 Curve.Incubation(见 ProgressionEvaluator:143);
                //    本字段只剩 R1-08(> 0)校验面与 Curve == null 的 legacy 占位条目使用。
                //    【为什么弃用「调 RecoveryTime」这条老路】legacy 曲线的 rise/decay 共用
                //    同一个 R(ProgressionEvaluator:240/250)⇒ 调小 R 只是把峰值前移,同时把
                //    衰减一起变快:R=20 时峰值 τ=40,此后每 20 tick 腰斩(τ=60 → 0.5 …),
                //    越过 floor(skill=1)≈0.9752 的窗口只剩 ≈1 s;R=2000 则要先等 ≈100 s。
                //    **两个极端都不可用** —— 形态问题是调 R 调不出来的。
                //    保留 2000 只为 R1-08 与 legacy 面;真病种表到位后整表替换,本行随之消失。
                RecoveryTime = 2000,
                RelapseChance = 0,
                ComorbidityFactor = 0,
                SeasonalMod = 0,
                AgeMod = 0,
                GenderMod = 0,
                OccupationMod = 0,
                RegionMod = 0,
                ClimateMod = 0,
                TreatableBy = Array.Empty<TreatableByEntry>(),
                Handle = "symptomatic_only",
                // ⚠️ 2026-10-14 A 案(用户裁定「按 A 做」):占位 null → **真配一张 plateau 型
                //    F1 曲线**(此前「曲线面校验跳过」的已知弱点就此闭合)。
                //    【为什么是 plateau 而不是继续调 RecoveryTime】legacy 曲线的峰值必然衰减,
                //    ⇒ 「立刻可读」与「可读窗口够宽」天然互斥(见上方 RecoveryTime 注释)。
                //    plateau(self_limit=false ∧ plateau=true)在 ComputeBaseF1 的 τ ≥ τ_peak 分支
                //    恒返回 A_peak —— **达峰后不衰减**,position 永远停在 1.0 ≥ floor(skill=1)
                //    ≈0.9752 ⇒「Play 一打开、过了潜伏就能按 E 且永远按得出词」成为曲线固有属性,
                //    不再靠人工卡时机。
                //    【校验面逐条过门】R1-20 互斥(plateau 单真)· R1-21(incubation=20 ≥ 0 ∧
                //    τ_peak=40 > 20 ∧ τ_rise=5 > 0)· R1-30(riseRatio = 20/5 = 4 ⇒ R_rise ≈ 0.982
                //    ≫ 1 LSB)· R1-22(仅 !self_limit ∧ !plateau 触发;plateau 型不踩,故
                //    Lethality=0 合法)· R1-24(A_peak=1 > 0)· R1-23(Scale=1 ≥ A_peak=1)·
                //    R1-25(self_limit=false ⇒ τ_fall 忽略)· R1-27(BoundaryMode="monotone")·
                //    R1-28(Sigma=null 不查)。
                //    【数值性质】**接线期可复验性数值,不是玩法数值** —— 潜伏 20 tick(1 s @20 Hz)
                //    + 上升 20 tick,τ=40(2 s)达峰;数值归用户轮,真病种表到位后整表替换。
                Curve = new NaturalProgressCurve
                {
                    Incubation = 20,                    // 1 s 潜伏(保 τ=0 仍处潜伏的既有判据)
                    APeak = Fix.One,                    // 峰值 1.0 ⇒ 越 floor(skill=1)≈0.9752
                    // ⚠️ 禁 FixParse:它是**编辑期 / 烘焙期**类型(ADR-014 §二:玩家构建
                    // 零 JSON 解析器、零 FixParse)⇒ 运行期源码出现它 = AC-21a-48 守卫恰红。
                    // 故时间常数走 raw 域字面量:Q16.16 ⇒ N tick = N × Fix.OneRaw。
                    TauRise = new Fix(5 * Fix.OneRaw),      // 上升常数 5 tick(陡)
                    TauPeak = 40,                           // τ=40(2 s)达峰
                    TauFall = new Fix(600 * Fix.OneRaw),    // plateau 型下忽略(R1-25 不查)
                    Plateau = true,                     // ★ 达峰后保持峰值,不衰减
                    SelfLimit = false,                  // 与 Plateau 互斥(R1-20)
                    Sigma = null,                       // 不覆盖,走全局默认
                    BoundaryMode = "monotone",
                },
                Relapse = null,          // 无复发(合法形态)
                Scale = Fix.One,         // F2 position = Progress / SCALE 的分母;1 = 恒等 ⇒ position = 1.0
                Signs = Array.Empty<SignEntry>(),
            };
            return new[] { entry };
        }
    }
}
