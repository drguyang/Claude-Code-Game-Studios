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
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using DaYiJingCheng.Sim.EmergencyProcedures;
using DaYiJingCheng.Sim.Prescription;
using DaYiJingCheng.Gameplay.PatientAI;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 组合根 —— 生产装配的唯一入口(把抽象点接成可用的实装图)。
    /// <para>可 EditMode 直测(纯 C# 装配逻辑,不碰场景 / 地址 / 异步)。</para>
    /// </summary>
    public static class CompositionRoot
    {
        /// <summary>
        /// 全自动装配(全新世界:计数器从 0 起、空在场登记簿、内存 blob 池、world_seed = 0)。
        /// </summary>
        /// <param name="registry">病种注册表(体征链 Step 的查表输入;null = 空表 ——
        /// 空表下体征链仍可装配,但任何 <c>DiseaseOnset</c> 都会 fail-loud「病种不在注册表内」,
        /// 属预期而非半装配)。</param>
        /// <returns>装配完成的服务袋(绝不为 null)。</returns>
        /// <exception cref="InvalidOperationException">防御性,实际不可达(装配环节产出 null 的
        /// fail-loud 兜底断言 —— 上游构造器已抛 / 参数检查先 ANE)。</exception>
        public static CompositionRootServices Assemble(
            IReadOnlyList<DiseaseRegistryEntry> registry = null,
            IDataProvider dataProvider = null)
            => AssembleCore(new IdAuthority(), new PresenceRegistry(), new InMemoryBlobPool(),
                            0UL, registry, dataProvider);

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
                                                       IDataProvider dataProvider = null)
        {
            if (idAuthority == null) throw new ArgumentNullException(nameof(idAuthority));
            if (presence == null) throw new ArgumentNullException(nameof(presence));
            if (blobPool == null) throw new ArgumentNullException(nameof(blobPool));
            return AssembleCore(idAuthority, presence, blobPool, worldSeed, registry, dataProvider);
        }

        private static CompositionRootServices AssembleCore(IIdAuthority idAuthority,
                                                             PresenceRegistry presence,
                                                             InMemoryBlobPool blobPool,
                                                             ulong worldSeed,
                                                             IReadOnlyList<DiseaseRegistryEntry> registry,
                                                             IDataProvider dataProvider)
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

            // ── 载荷面:池(写面 IBlobSink)→ 编码器(第七抽象点)──
            // 池与编码器一体接线(ADR-029 乙案):Sim 侧只见 IPayloadEncoder,
            // 构造点只在本装配可见 Sim.Codec —— 案 A 的全部意义。
            IPayloadEncoder encoder = new PayloadEncoder(blobPool);

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

            // ── 急救链处理器 + 驱动(sprint-05 T1.3 → sprint-06 G4.1 退役)──
            // G4.1(2026-10-11 用户裁定「急救手动化」):自动触发分支已从
            // EmergencyAttemptDriver 删除(退役壳零事件);触发权归玩家,
            // 手动入口 = EmergencyManualDriver(BootRoot 生成玩家后调用)。
            var emergencyProcessor = new HostEmergencyProcessor(stream, idAuthority, encoder);
            var emergencyDriver = new EmergencyAttemptDriver();
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
            var casebookSource = new CasebookVitalsSource(examinationLedger,
                stream.Events, tickDriver);

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
            var examineDriver = new ExamineDriver(vitals, examinationLedger, queryLevel: 0);
            examineDriver.SetLexicon(examineRows, in readFloor);

            // ── 处方链(G3 · 2026-10-11 用户裁定「最小生产端口实装」)──
            // 21a(换算/药物档案)+ 20(库存面)+ 30(成长)三端口最小实装 + 驱动,
            // 把 PrescribeFlow(11)接进运行期。⚠️ 完整 21a/20 落地后只换端口实现,签名零改动。
            var drugCatalog = new MinimalDrugCatalog();                 // 21a 最小换算表
            var portionsStore = new MinimalPortionsStore();            // 20 最小库存面
            var skillGrowthPort = new AssemblySkillGrowthPort(stream, encoder, prescriptionLevel: 0);
            var prescribePorts = new PrescribePorts(drugCatalog, portionsStore,
                presence, skillGrowthPort, doseBase: 65536);            // DOSE_BASE(数值轮注记)
            var prescribeDriver = new PrescribeRequestDriver(stream, encoder,
                prescribePorts, isHost: true);

            // ── 急救候选列表生产实例宿主(S3.1 · 2026-10-11)──
            // M2 形态件③(候选动作列表)的「运行期有载体」半边:实例化 + 喂 10 最小动作
            // 标签(止血/通气)。动作判定 / Idle 时序 / 点击行为归 10 + 数据绑定轮 ——
            // 本宿主只回答「列表实体存在 + 跳过入口可用」(story-023 边界①)。
            var emergencyCandidates = new EmergencyCandidateRuntime();

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
            if (patientVisuals == null)
                throw new InvalidOperationException("Assemble: PatientVisualSpawner 装配产出 null");
            if (casebookSource == null)
                throw new InvalidOperationException("Assemble: CasebookVitalsSource 装配产出 null");
            if (examinationLedger == null)
                throw new InvalidOperationException("Assemble: ExaminationLedger 装配产出 null");
            if (examineDriver == null)
                throw new InvalidOperationException("Assemble: ExamineDriver 装配产出 null");
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

            return new CompositionRootServices(stream, idAuthority, blobPool, encoder,
                                               presence, spawner, patientDriver, caseDriver,
                                               emergencyDriver, emergencyManual, patientVisuals,
                                               worldSeed, vitals,
                                               casebookSource, examinationLedger, examineDriver,
                                               tickDriver, prescribeDriver, prescribePorts,
                                               portionsStore, emergencyCandidates,
                                               prescriptionPage);
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
                RecoveryTime = 100,
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
                Curve = null,            // 既有占位形态:曲线面校验跳过(登记为已知弱点)
                Relapse = null,          // 无复发(合法形态)
                Scale = Fix.One,         // F2 position = Progress / SCALE 的分母;1 = 恒等
                Signs = Array.Empty<SignEntry>(),
            };
            return new[] { entry };
        }
    }
}
