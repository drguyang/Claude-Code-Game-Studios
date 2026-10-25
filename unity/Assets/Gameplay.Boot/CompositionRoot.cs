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
        /// 世界出生格 —— `death_cell` 查询在**玩家从未跨过格**时的兜底。
        ///
        /// <para>⚠️ <b>这是兜底,不是判据</b>。registry 对 `death_cell` 的原文是
        /// 「最后一条 <c>ActorCellEntered</c> 之 cell **原样携带**」——
        /// 一条都没有时,「原样」无从谈起。此时:</para>
        /// <list type="bullet">
        /// <item>**不得**抛(死亡是正常路径,玩家刚进游戏就病死是可能的);</item>
        /// <item>**不得**随手造 `WorldPos(0,0,0)` —— 那是世界原点,一个真实存在的格,
        /// 会伪造「死在那里」的结论。</item>
        /// </list>
        ///
        /// <para>本值 = <c>BootRoot</c> 生成玩家后 <c>Teleport(new Vector3(0f, 1f, 0f))</c>
        /// (<c>BootRoot.cs:261</c>)经 <c>CellTransitionDetector.CellFromPosition</c>
        /// (FloorToInt)得到的**同一个格** —— 即玩家出生时**实际所在**的格,
        /// 不是本处发明的一个数。⇒ 兜底值 = 玩家真实位置的已知事实。</para>
        ///
        /// <para>⚠️ <b>已知缺口(如实登记,不掩盖)</b>:正规修法是「1 在传送 / 初始化时
        /// 发一条 <c>ActorCellEntered</c>」—— 但 ADR-020 §四 与 AC-29-09 明令
        /// **29 不得 Append `ActorCellEntered`**(写者 = 1)。故本兜底留在装配层,
        /// 待 1 的初始化路径补发后**自动失效**(那时 `TryLastCellOf` 恒命中,
        /// 本常量成为死值)。归属 = 1 的 epic,不在 29 story-004 面内。</para>
        /// </summary>
        private static readonly WorldPos WorldSpawnCell = new WorldPos(0, 1, 0);

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
            StructureInstanceRegistry structures = null,
            string saveBasePath = null)
            => AssembleCore(new IdAuthority(), new PresenceRegistry(), new InMemoryBlobPool(),
                            0UL, registry, dataProvider, inputActions, audio, clinicKnowledge, telemetryKnobs,
                            structures, saveBasePath);

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
                                                       StructureInstanceRegistry structures = null,
                                                       string saveBasePath = null)
        {
            if (idAuthority == null) throw new ArgumentNullException(nameof(idAuthority));
            if (presence == null) throw new ArgumentNullException(nameof(presence));
            if (blobPool == null) throw new ArgumentNullException(nameof(blobPool));
            return AssembleCore(idAuthority, presence, blobPool, worldSeed, registry, dataProvider,
                                inputActions, audio, clinicKnowledge, telemetryKnobs, structures,
                                saveBasePath);
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
                                                             StructureInstanceRegistry structures,
                                                             string saveBasePath)
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

            // ── 病种注册表 + 全局趋势窗口(2026-10-22 B1 接真)─────────────
            // 三级优先:
            //   ① 调用方显式传入(EditMode/PlayMode 测试注入合成 fixture)—— 最高优先;
            //   ② 否则 dataProvider != null(生产:BootRoot 预载成功)⇒ 真注册表产物
            //      `disease_registry.cooked.bytes`,连同全局 W / TREND_EPS;
            //   ③ 否则(EditMode / 测试 / 预载失败)⇒ 组合根自带最小内联注册表。
            // ⚠️ **不可合并为「fallback ∪ caller」**:真表到位后,fallback 的 DiseaseId=1
            //    可能与真表撞号(R1-01 唯一性违例)⇒  caller 给表时 fallback 必须**整体让位**。
            //    2026-10-10 实测事故:VerticalSliceTest 显式传 9001 fixture,而体征链仍持
            //    fallback(只有 DiseaseId=1)⇒ `DiseaseOnset disease_id=9001 不在注册表内`
            //    fail-loud,两条核心循环测试红。
            // ⚠️ **B1 缺口(2026-10-22 前的状态)**:`disease_registry.cooked` 从未产出过,
            //    也没有读方 codec ⇒ 生产恒走 ③,全局 W / TREND_EPS 恒 0 ⇒ F4 的痊愈支 /
            //    迁延支在生产里**永不求值**。本批补的是 ② 这条路。
            // ⚠️ **为什么 ③ 不退化成「磁盘兜底读真表」**:真表 id 从 2 起,而 EditMode 侧
            //    45 处夹具按 `SpawnNext(diseaseId: 1, …)` 建病人(fallback 的占位 id)。
            //    让 ③ 读真表 ⇒ `DiseaseVitalsService.OnTickEdge` 的「病种不在注册表内」
            //    fail-loud 会在那 45 处集体触发 —— 那是把「接真」变成全局回归,不是它的目的。
            //    ③ 的语义**未变**:接线载体,非数值真源(见 <see cref="CreateFallbackRegistry"/>)。
            IReadOnlyList<DiseaseRegistryEntry> resolvedRegistry;
            long trendWindowTicks = 0L;
            long trendEpsRaw = 0L;
            if (registry != null)
            {
                resolvedRegistry = registry;
                // caller 传表 = 测试合成 fixture;此时**刻意不**混入真 W / TREND_EPS
                // (两个真源混用会让「这条测试跑的是哪张表」变得不可回答)。
            }
            else if (dataProvider != null)
            {
                DiseaseRegistryDataSet diseaseRegistry = dataProvider.Load<DiseaseRegistryDataSet>();
                resolvedRegistry = diseaseRegistry.Rows;
                trendWindowTicks = diseaseRegistry.TrendWindowTicks;
                trendEpsRaw = diseaseRegistry.TrendEpsRaw;
            }
            else
            {
                resolvedRegistry = CreateFallbackRegistry();
            }

            // ── 处置轴产物装载(一次装载,两个消费者)──────────────────────────
            // 消费者 ①(2026-10-20 起):DC-4 处置轴闭集 —— 按 `owner=="10"` 过滤注入
            //   `HostEmergencyProcessor`。病因:`disease_action_axis` 的 `actions` 段编辑期
            //   已被消费(`PrescriptionActionIdRegistry.RealActionIds()` 读 `owner=="11"`
            //   段作 DC-2 闭集),但**运行期零读点** ⇒ `EmergencyActionSchema.ValidateActionId`
            //   长期是 `Enum.IsDefined` 的影子(枚举注释自己点名「9 的轴新增 id ⇒ 影子会错误拒绝」)。
            // 消费者 ②(2026-10-23 起):`treatable_by` 查表面 —— 见下方
            //   `DiseaseActionAxisTreatableByLookup`(结清 `D-9-P`)。
            // ⚠️ **为什么在装配层过滤 / 造查表,而不是让 Sim 自己读产物**:`Sim` 住门 A
            //    (`noEngineReferences`),够不着 `Gameplay.Presentation.DiseaseActionAxisCookedCodec`;
            //    Processor 只收一个 `ISet<int>`,Sim 侧只收 `ITreatableByLookup` —— 与
            //    `IPayloadEncoder` / `IPayloadDecoder` 同构(零新增程序集边)。
            // ⚠️ dataProvider == null(EditMode / 15+ 既有调用方)⇒ 两者皆 null ⇒
            //    回退枚举影子 + 离牌门恒放行,与接线前**逐位相同**。
            System.Collections.Generic.ISet<int> emergencyActionAxis = null;
            ITreatableByLookup treatableBy = null;
            if (dataProvider != null)
            {
                DiseaseActionAxisDataSet axis = dataProvider.Load<DiseaseActionAxisDataSet>();
                var owner10 = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < axis.Actions.Count; i++)
                    if (axis.Actions[i].Owner == "10")
                        owner10.Add(axis.Actions[i].ActionId);
                emergencyActionAxis = owner10;

                // treatable_by 查表面(2026-10-23 用户裁定「运行期查轴」)。
                //   ⚠️ 轴是**单一真源**(`D-9-K`),故注册表产物**不携带** treatable_by
                //      —— 不再烘一份进 cooked,否则两者可分叉。
                //   ⚠️ `axis.TreatableBy` 为 null ⇒ 构造函数 fail-loud(空表 = 「所有处置
                //      都不可用」,与「未接线」不是同一状态;静默降级会把数据缺失伪装成设计)。
                treatableBy = new DiseaseActionAxisTreatableByLookup(axis);
            }

            // ── 体征链核心(批次 C):apply 驱动 + 投影桥 + IVitalsQuery 生产实装 ──
            // 它**同时**需要 EventStream/Sim(状态与求值)与 Sim.Codec(载荷解码)
            // ⇒ 只能在本装配(案 A)构造;客户端侧不装配(ADR-005 主机唯一 Step)。
            // ⚠️ W / TREND_EPS 原样搬运,**不在此校验**(值域守卫归 R1-36 写入期;
            //    0 = 未配置是合法入参 —— 见 DiseaseVitalsService 构造注释)。
            // ── 死亡候选产出侧(story-008 · 2026-10-24 · 承 S18-AC5 前半句)──
            // `IDeathCandidateSink` 此前只有声明、零实现,`Publish(` 全树零命中
            // ⇒ 死亡候选的产出路径在生产上从未存在过。本处构造实现并经服务袋暴露。
            var deathCandidates = new DeathCandidateSink();

            // ── 玩家 actor 花名册(2026-10-24 story-004 装配轮)──────────────
            // 此前**全库没有「某 id 属于哪一类」的查询面**,故 9 侧只能写死 `Patient`。
            // 花名册由 `BootRoot` 在**生成玩家之后**登记(id 由 `IIdAuthority` 在装配之后
            // 才铸造 ⇒ 登记必然是后置动作,与 `SetViewer` / `SetAuthor` 同格)。
            // ⚠️ 装配期 Count = 0 是**合法状态**(BootRoot 尚未跑),不是装配失败。
            var playerActors = new PlayerActorRegistry();

            // 实体类别查询面(story-008 AC-9-26 → story-004 装配轮接真)。
            // 🔁 **2026-10-24 订正(原文不删)**:原写「现状恒 `Patient` …… 真查询面须等
            //    玩家染病路径落地」。那句话对 **9 的 F4** 仍成立(只有病人有 course),
            //    但对 **29 的死亡消费链不成立** —— `DeathCandidateConsumer` 用同一个面
            //    判「该不该铸 PlayerDied」,恒 Patient ⇒ 类别闸永不放行 ⇒ 装了等于没装。
            //    现改为查花名册:`ClassOfDeathCandidate` = 「已登记 = Player,未登记 = Patient」。
            //    ⚠️ 该结论的**域**见 `PlayerActorRegistry` 头注(死亡候选链的输入域 =
            //    course 持有者 ⇒ 补集只可能是病人);**域外不得复用**。
            //    ⇒ 与接线前**逐位一致**(今天无人登记 ⇒ 恒 Patient),零行为变化。
            Func<int, ActorClass> actorClassOf = playerActors.ClassOfDeathCandidate;

            DiseaseVitalsService vitals = new DiseaseVitalsService(
                stream, blobPool, resolvedRegistry, worldSeed, trendWindowTicks, trendEpsRaw,
                treatableBy, actorClassOf, deathCandidates);

            // ── 死亡消费侧装配(2026-10-24 · 29 story-004 的装配轮)──────────
            // 管线序(story-004 实现注 2):`DeathCandidate → DeathAllowed 门 → PlayerDied`。
            // 三件在这里接成一条链,且**门在任何写流之前**(写者内再复核一次,双闸)。
            //
            // ① 冷却门:从世界流重算 `last_death_tick`(**零计数器**,AC-29-15)。
            var deathCooldown = new DeathCooldownGate(() => stream.Events, decoder);

            // ② 写者:`PlayerDied` 的唯一生产写入通道(ADR-029 §③ 编码路径)。
            var playerDiedWriter = new PlayerDiedWriter(stream, encoder);

            // ③ 病种条目查询面 —— 「该实体当前所受病种 / 伤情条目」。
            //    来源 = 体征链的**病程档案簿**(course 是唯一的病种归属载体),
            //    再经注册表 id 反查条目。⚠️ 玩家无 course ⇒ 返回 null ⇒
            //    `PlayerDeathWarranted` 否决(见下方「诚实边界」)。
            var registryById = new Dictionary<int, DiseaseRegistryEntry>();
            for (int i = 0; i < resolvedRegistry.Count; i++)
                registryById[resolvedRegistry[i].DiseaseId] = resolvedRegistry[i];

            Func<int, DiseaseRegistryEntry> entryOf = actorId =>
            {
                if (!vitals.Courses.TryGet(new PatientId(actorId), out DiseaseCourse course))
                    return null;
                return registryById.TryGetValue(course.DiseaseId, out DiseaseRegistryEntry e) ? e : null;
            };

            // ④ 死亡格查询 —— registry 逐字:「`death_cell` = 最后一条 `ActorCellEntered`
            //    之 cell 原样携带(派生态,禁读实时物理位置)」。ADR-020 §四。
            //    ⚠️ **未找到时的兜底**(玩家从未跨格)用 `WorldSpawnCell` —— 见其注释。
            Func<int, WorldPos> deathCellOf = actorId =>
                DeathCellQuery.TryLastCellOf(stream.Events, decoder, actorId, out WorldPos cell)
                    ? cell
                    : WorldSpawnCell;

            var deathConsumer = new DeathCandidateConsumer(
                playerDiedWriter,
                // 冷却门求值:tick = 候选自带的那一个(判据的另一半,不另取时钟)。
                deathAllowed: deathCooldown.DeathAllowed,
                actorClassOf: actorClassOf,
                entryOf: entryOf,
                deathCellOf: deathCellOf);

            // ⑤ 注册 —— 单消费者契约(`Register` 二次调用抛;此处只调一次)。
            deathCandidates.Register(deathConsumer);

            // ── 病人出现驱动(sprint-05 T1.1):把 spawner 接进 tick 边沿序列 ──
            // 原状:spawner 在服务袋里但**零生产调用方**(F-6 复评实测)⇒ 运行期无病人。
            // 驱动 = 每 tick 边沿按节奏调 SpawnNext;节奏 = 每 N tick 一个病人(见驱动类头注)。
            var patientDriver = new PatientAppearedDriver(spawner, presence, resolvedRegistry);

            // ── 病例开账写者 + 驱动(sprint-05 T1.2):把 CaseOpenWriter 接进 tick 边沿序列 ──
            // 原状:CaseOpenWriter 零生产调用方 ⇒ 运行期链可达性 0%。
            // 驱动 = 每 tick 边沿对在场且未开案的病人自动开案。
            var caseOpenWriter = new CaseOpenWriter(stream, presence, encoder, stream.Events);
            var caseDriver = new CaseOpenedDriver(caseOpenWriter, presence);

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

            // ── 7a 存档服务(S19-1 判据 ④ 位置半 + 委托半)────────────────────
            // 形态 = 甲-a(三家专家合成裁定 2026-10-24):`SaveService` 只认「拿到一坨字节」,
            //   折叠 / 行编码 / 布局**全在下面这个闭包里** ⇒ `Sim.Codec` 永不命名 `FoldRow`,
            //   零新增装配边(Sim / Sim.Codec 的 references 一字不改,ADR-025 §①)。
            //
            // ⚠️ **`saveBasePath == null` ⇒ 不构造 SaveService(`SaveService` 属性为 null)**。
            //   这是「未配置存档目录」的**合法状态**,不是半装配 —— 与 `AudioRenderAssembled`
            //   同款「可观测而非静默」处置。选 null 不选「自动落 persistentDataPath」的理由:
            //   全仓 109 处 `CompositionRoot.Assemble` 调用点(多为 EditMode 夹具)会在
            //   用户数据目录里各造一个目录 —— 那是 109 份污染,且 `FsWriteBoundaryGate`
            //   只扫测试**源码字面量**、抓不到它(派生路径无字面量)。
            //   生产路径 = `BootRoot` 显式传 `persistentDataPath/saves`(见其调用点)。
            //
            // ⚠️ ~~`narrativeContext` 今日传 null(fail-closed)~~(**2026-10-25 生产接线轮过期**,
            //   原文不删):真实驱动源已接通 = `NoPendingMedicalActionQuery`(急救通道 Armed 态
            //   取反,见下方声明);「谁在叙事锚点事件时翻 true」(事件触发半边)仍归
            //   8 / 37 / 52 的实现轮 ⇒ `TD-persist-04`。~~后果:存档 / 读档在生产路径上不可用~~
            //   ⇒ 定时兜底 + 退出保存 + 读档门自此在生产路径可用;恒真替代品仍被禁止
            //   (save-scum 通道不因接线而开 —— Armed 态是瞬时内存量,不是恒真)。
            SaveService saveService = null;
            CheckpointFallback checkpointFallback = null;
            // ── 瞬时源(规则十四「一个量」)—— 2026-10-25 用户裁定「生产接线今日不为零,现在修」──
            // 实装 = NoPendingMedicalActionQuery(读急救通道 Armed 态取反;W5 要求的
            // 「独立于流的内存态」就此兑现 —— 不是流内累积钩子,被证伪的形态不回归)。
            // ⚠️ **声明在 `if (saveBasePath)` 之外**:同一实例要跨三处共用 ——
            //   ① `SaveService`(写/读门)② `CheckpointFallback`(兜底,下文 if 内构造)
            //   ③ 急救装配段的 `Bind`(晚于本段,依赖序)—— 拆成两个实例 = 读档门与
            //   兜底门对同一时刻给出不同答案(规则十四被破坏)。
            // ⚠️ 未 Bind 期 `InNarrativeContext()` 恒 false(fail-closed,见该类头注)。
            var narrativeContext = new NoPendingMedicalActionQuery();
            if (!string.IsNullOrEmpty(saveBasePath))
            {
                // 折叠谓词 —— 四个输入全部可从三流 + 注册表取,无快照 / 无墙钟(ADR-016 §一)。
                var foldedPredicate = new FoldedPredicate(
                    historyStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.History),
                    caseStream: () => SaveComposer.FilterByStream(stream.Events, StreamId.Case),
                    diseaseLookup: entryOf,
                    decoder: decoder);

                // 组版闭包 —— `HistoryFolder.Fold` 的**生产调用点**(判据 ④ 位置半 (i))。
                // ~~折叠行的字节编码器尚不存在 ⇒ 有行要落盘时 `SaveComposer` 抛~~
                // 🔁 **2026-10-24 `TD-persist-03` 修复轮兑现字节半**:折叠行经
                //    `FoldRowCodec.EncodeSegment` 落折叠段(`SaveComposer.Compose` 内,
                //    ADR-010 §一 增补 v1 布局)—— 上句「尚不存在 / NOT-RUN」已过期。
                //
                // 🔁 **2026-10-24 `TD-epic-01-f` 修复**:configVersion 由**恒 0U** 改为**真派生**。
                //    口径 = ADR-014 §五「对 `assets/data/` 源文本集求哈希」(用户 2026-10-24 裁定
                //    「取全局组合值」)—— 由 `DataCoreConfigVersion.Combine` 把 22 份产物的
                //    逐份 ConfigVersion 组合成一个 u32。
                //    ⚠️ **派生失败 ⇒ 硬失败(E-13),不降级回 0** —— 0 会让 AC-51-D7 退回假绿。
                //    ⚠️ **惰性派生,不在装配期求值**:本闭包**首次组版时**才解析。理由是既有
                //       两条夹具(`save_composer_wiring_test.cs:341` / `:370`)传 `saveBasePath`
                //       但 `dataProvider: null` —— 它们验的是折叠谓词接线,与版本号无关。
                //       装配期就抛会让它们因**不相干的**理由转红(= 我引入的假红)。
                //       惰性不削弱判据:`compose` 是唯一读该值的路径,它一旦被调用就必抛。
                uint? resolvedConfigVersion = null;
                // ── GDD `:123` 缓冲池 —— **组版与 worker 共用同一份**(见 SaveService 构造点注)──
                // ⚠️ 声明在闭包**之外**:闭包每次组版都会跑,池必须在多次组版之间存活
                //    (每份存档新建一个池 = 零复用)。
                var saveBufferPool = new SaveBufferPool();
                Func<SaveSlot, byte[]> compose = _ =>
                {
                    if (!resolvedConfigVersion.HasValue)
                        resolvedConfigVersion = ResolveGlobalConfigVersion(dataProvider);
                    return SaveComposer.Compose(
                        stream.Events, foldedPredicate, decoder, new SaveCodec(),
                        worldSeed, resolvedConfigVersion.Value, tick: tickDriver.CurrentTick,
                        existingFoldedRows: stream.FoldedRows,
                        // 🔁 2026-10-25 AC-7a-07:`Compose` 现返回**不含校验和**的字节
                        //    (SHA256 移到 worker),`combined` 的合成缓冲从本池租。
                        bufferPool: saveBufferPool);
                };

                // 重放 + 折叠行交出 —— `TD-persist-03` 修复轮(2026-10-24,用户四裁定):
                // `Load` 契约次序 = 先事件列表、后折叠段字节 ⇒ 两个 BCL 委托协同,
                // 在折叠行到达时把两者合并喂给 `stream.Restore`(绕 CAP、留去重/单调、
                // Seq 不重发 —— 用户裁定「三分化」;读档时在场视图未重建,CAP 必误抛)。
                // 🔁 替代 S19-1 的空操作 sink(`_ => { }`):交出后丢弃 = 读档假绿。
                IReadOnlyList<SimEvent> pendingReplay = null;
                Action<IReadOnlyList<SimEvent>> replaySink = events => { pendingReplay = events; };
                Action<byte[]> foldedRowSink = foldedBytes =>
                    stream.Restore(
                        pendingReplay
                            ?? throw new InvalidOperationException(
                                "折叠段先于事件段到达 —— SaveService.Load 契约次序被破坏"),
                        FoldRowCodec.DecodeSegment(foldedBytes));

                // 🔁 **2026-10-25 W5 全程轨迹(原文不删,留作闭环记录)**:
                //   ① 原状:`narrativeContext: null` ⇒ `Checkpoint` / `SaveOnExit` / `Load`
                //      生产期**一律抛**(fail-closed)⇒ 存档/读档在生产路径上完全不可用。
                //   ② 评审轮按用户裁定「用已有流事件驱动」改为
                //      `() => HasNarrativeAnchorEvent(stream.Events)`。
                //   ③ 接线后暴露**语义违背**(见下方 🔴 段),用户 2026-10-25 裁定「丙」
                //      ⇒ **接线回退 `null`**。故本行现在是 ① 的状态,但**不是**「未处理」——
                //      它是「评估过、试过、被证伪、按裁定回退」的终态。
                //
                // **口径依据(GDD `persistence-service.md:128` 规则六逐字)**:
                //   「checkpoint 绑定**叙事事件** —— 脉案落笔 / 病例结案(37)· 出诊启动(52) 的
                //     锚点白名单。**7a 只出观察钩子形状**(「一个值得锚点的叙事事件」的接口),
                //     **不拥有具体事件列表**;各系统在**生成该类事件时**同步触发 checkpoint」
                // ⇒ 本闭包是**装配根的接线**,不是 7a 的事件表:7a(`SaveService`)只见 `INarrativeContextQuery`
//   (2026-10-25 裁定甲,原 `Func<bool>`;契约 = `Sim.Contracts/INarrativeContextQuery.cs`),
                //   看不见任何 `EventKind`。锚点 Kind 在此**具名列举**,是为了让缺口可被 grep 到。
                //
                // ⚠️ **锚点映射缺口 —— 必须如实登记,禁借绿**:
                //   GDD 列**三个**锚点,本闭包只接得上**两个**:
                //   | GDD 锚点 | 本闭包驱动源 | 状态 |
                //   |---|---|---|
                //   | 病例结案(37) | `EventKind.CaseClosed` | ✅ 有写者(`Sim/CaseCloseWriter.cs`) |
                //   | 脉案落笔 | **无** | ❌ `PatternRecognized` 是「同源检测」,**不是**脉案落笔;
                //   |          |      |    按「沾边就算」接上 = 把非锚点事件冒充锚点(借绿) |
                //   | 出诊启动(52) | **无** | ❌ 全库零载体(`EVENT_CHECKPOINT_ANCHORS` 零命中) |
                //   ⇒ 第三个驱动源 `PatternRecognized` 是**保守补集**:它比「脉案落笔」宽(会多放行
                //     一些非锚点时刻),但**比「流非空即真」窄得多** —— 后者会把每一次 `InjuryOnset`
                //     都当锚点,那正是规则六要守的 save-scum 通道。
                //     用户的裁定原话即「两个事件是否真的对应 GDD 的三个锚点」⇒ **答案是「不完全对应」**,
                //     本注释即该问题的登记落点,残余归 `TD-persist-04`。
                //
                // ⚠️ **仍不在本闭包内(→ `TD-persist-04`)**:
                //   ① 定时兜底 `CHECKPOINT_INTERVAL`(规则六「事件长期缺席」那半);
                //   ② 「最近一条叙事事件之后」的**时间语义** —— 本闭包只答「有没有过」,
                //      不答「上一次锚点距今多久」;
                //   ③ 「各系统在生成该类事件时**同步触发** checkpoint」的触发方(本闭包只提供**判据**,
                //      不调用 `Checkpoint`)。
                //   ⇒ `AC-7a-20` 仍**不得**声称已满足。
                //
                // 性能:全扫 O(n),无缓存 —— 调用点只有 `Checkpoint` / `Load`(事件粒度,非每 tick),
                //   且刻意不加游标缓存:读档走 `stream.Restore` 重建流,缓存态会失效而**静默**留旧值。
                //
                // 🔴 **2026-10-25 用户裁定「丙」—— 接线回退为 `null`(fail-closed),本闭包不启用**。
                //
                //   评审轮曾按用户前一裁定把本行接成 `() => HasNarrativeAnchorEvent(stream.Events)`,
                //   字面兑现后暴露**语义违背**,故整体撤回接线:
                //   | 项 | GDD 要求 | 已备实现 | 差在哪 |
                //   |---|---|---|---|
                //   | `InNarrativeContext` 语义 | **瞬时**量(「此刻**无**未决医疗动作」) | **累积**量(「流里**曾**出现过锚点」) | 首次锚点后**恒 true** |
                //   ⇒ 累积语义下,`AC-7a-19 (c)`「急救动作进行中拒绝读档」这道安全门**恒开**,
                //     逃生通道(带伤/带病/战斗中读档)重新打开。这是**从「安全但不可用」换成「可用但不安全」**,
                //     属 fail-open —— 比原状危险。
                //   ⚠️ 备选的 `LastEventIsAnchor`(瞬时判据)**也不接线**:它比 GDD 更严,且仍有一个洞 ——
                //     病人 A 未处置时若发生病人 B 的病例结案,最后一条是 `CaseClosed` ⇒ 门误开。
                //   ⇒ **真正的瞬时源** = 「有没有未决医疗动作」这个**独立于流**的内存态,归 **10 急救动作**的
                //     运行时状态(`EmergencyAttempt` 进流,但「进行中」不在流里)。在 10 落地前,
                //     `null`(fail-closed)是**唯一不破 AC 的状态**:不可用 > 不安全。
                //   ⇒ 两个纯函数 `HasNarrativeAnchorEvent` / `LastEventIsAnchor` **保留在盘**(已测、可供 10 复用),
                //     但**零接线点**;`AC-7a-20` 仍归 `TD-persist-04`,**不得**声称已满足。
                //
                // 🔁 **2026-10-25 用户裁定「生产接线今日不为零,现在修」—— 本缝当日接通,上述
                //    「回退 null / 生产不可用」的终态就此过期**(上段原文不删,留作闭环记录):
                //    瞬时源 = `NoPendingMedicalActionQuery`(声明在本 `if` 之前,与本段 / 兜底 /
                //    急救 Bind 三处共用同一实例)。**W5 证伪的形态不回归**:它读的是急救通道的
                //    **内存 Armed 态**(瞬时),不是流内「曾出现过锚点」(累积)。
                //    ⚠️ **接通的是「瞬时源可用」,不是锚点判定** —— 事件触发半边(锚点白名单
                //    接线)仍归 `TD-persist-04` 动作 ①,`AC-7a-20` 的事件触发面**仍不得**声称已满足。
                //
                //    兜底接线(`AC-7a-20` 定时半边 · `TD-persist-05` 动作④):闭包 =
                //    `Checkpoint(台账 NextWritableSeq)` —— 「下一个可写槽位」的单一出处
                //    (只进不退由 `EnsureWritable` 把守;高水位在 worker 写成功后推,
                //    异步窗口内重复触发只会重写同一槽位,15 min 间隔下不构成实际路径)。
                //    try/catch 与 `BootRoot` 退出钩子同款纪律:本闭包在 tick 边沿上跑,
                //    未兜的异常会杀死整个 `Update` 帧 ⇒ 降级为 LogError,下一 interval 重试
                //    (写失败本体已在 worker 内兜住,这里兜的是组版 / 门的编程错误)。
                checkpointFallback = new CheckpointFallback(
                    () =>
                    {
                        try
                        {
                            saveService.Checkpoint(new SaveSlot(saveService.Ledger.NextWritableSeq));
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogError(
                                "[7a][AC-7a-20] 定时兜底 checkpoint 抛出(下一 interval 重试,不杀帧):" + e);
                        }
                    },
                    narrativeContext);

                saveService = new SaveService(
                    saveBasePath,
                    new SaveCodec(),
                    folded: foldedPredicate,
                    narrativeContext: narrativeContext,
                    replaySink: replaySink,
                    foldedRowSink: foldedRowSink,
                    compose: compose,
                    // AC-7a-17 · TD-persist-06:退出保存失败的「记日志」落点。
                    // ⚠️ `SaveService` **不抛**写盘失败(退出路径上抛 = 卡退),故
                    //    GDD「不阻塞退出 **+ 记日志**」里的后半句只能经这条委托兑现。
                    //    `Debug.LogError` 而非 `LogWarning`:写盘失败 + 标志位可能未置 = 最坏情形,
                    //    须在日志里显眼。
                    exitSaveFailureSink: (writeFailure, flagFailure) =>
                    {
                        // ⚠️ **全限定** `UnityEngine.Debug` —— 本文件零 `using UnityEngine;`
                        //    (它只引 `UnityEngine.InputSystem`),加整条 using 会把
                        //    `Object` / `Random` 等裸名一并带进来。同 `BootRoot.cs:483` 先例。
                        UnityEngine.Debug.LogError(
                            "[7a][AC-7a-17] 退出保存失败(已置「会话未完整」标志,不阻塞退出)。" +
                            "写盘:" + (writeFailure?.ToString() ?? "(无)") +
                            " | 置位:" + (flagFailure?.ToString() ?? "(成功)"));
                    },
                    // ── AC-7a-07 后台写盘(规则五 · GDD `:117-123`)──
                    // 🔁 **2026-10-25 新增**。此前写盘与 SHA256 全在主线程
                    //    (`SaveService.WriteAtomic` 同步、`SaveComposer.Compose` 内 `AddChecksum`)。
                    //
                    // ⚠️ **`Task.Run` 是生产与夹具的唯一差别**:夹具传 `action => action()`
                    //    (同步内联,断言确定);生产传 `Task.Run`(真后台线程)。
                    //    该委托**不可省略** —— `SaveService` 对未注入者 fail-closed,
                    //    缺省会静默退化为同步而全部测试仍绿(见 `RequireWriteScheduler`)。
                    //
                    // ⚠️ 用 `Task.Run` 而非裸 `new Thread`:worker 是**长跑但空闲即退**
                    //    (队列空则 `Drain` 返回),不值得为每份存档付一次线程创建成本;
                    //    线程池的调度延迟在 ms 级,远小于存档写盘本身。
                    writeScheduler: action => System.Threading.Tasks.Task.Run(action),
                    // ── GDD `:123` GC 纪律:缓冲池复用 ──
                    // ⚠️ **同一份池必须同时给 `compose` 与 `SaveService`** —— 组版在主线程
                    //    租、校验和与写盘在 worker 归还,共用一份才有复用意义。
                    //    两处传不同的池 = 各自都只租不还,退化成两个只涨不缩的空池。
                    bufferPool: saveBufferPool);
            }


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

            // 瞬时源 Bind(2026-10-25 生产接线轮):EmergencyChannelDriver 实现 IArmedState
            // (ADR-013 Amendment A)⇒ 「无未决医疗动作」自此有真源。晚于 SaveService 构造段
            // 是依赖序,故 NoPendingMedicalActionQuery 用「先构造、后 Bind」(未 Bind 期恒 false)。
            narrativeContext.Bind(emergencyChannelDriver);

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
            if (deathCandidates == null)
                throw new InvalidOperationException("Assemble: DeathCandidateSink 装配产出 null");
            if (deathConsumer == null)
                throw new InvalidOperationException("Assemble: DeathCandidateConsumer 装配产出 null");
            if (deathCooldown == null)
                throw new InvalidOperationException("Assemble: DeathCooldownGate 装配产出 null");
            if (playerActors == null)
                throw new InvalidOperationException("Assemble: PlayerActorRegistry 装配产出 null");
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
                                               resolvedRegistry,   // registry → 51 TelemetryReporter
                                               presence, spawner, patientDriver, caseDriver,
                                               emergencyDriver, emergencyManual,
                                               emergencyChannelDriver, patientVisuals,
                                               worldSeed, vitals, deathCandidates,
                                               deathConsumer, deathCooldown, playerActors,
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
                                               clinicRoomMatching, saveService,
                                               // 2026-10-25 生产接线轮:兜底调度器与 SaveService 同源装配
                                               // (闭包与瞬时源见上;saveBasePath 为 null 时两者同 null)。
                                               checkpointFallback, narrativeContext,
                                               // 🔁 2026-10-24 `TD-epic-01-f` 断点①的解药:
                                               //   把「当前产物 ConfigVersion」的真值来源喂给 51,
                                               //   使 `ConfigVersionCheck` 在生产侧**有构造路径**。
                                               // ⚠️ 与存档侧同一个派生函数(`ResolveGlobalConfigVersion`),
                                               //   两处共用一个口径 —— 存档头写进去的值与 51 比对用的值
                                               //   必须同源,否则「永远不匹配」。
                                               // ⚠️ 委托形式 ⇒ 派生失败抛在**调用时**(开发者预览被触发时),
                                               //   不在装配期把整个启动序拖死(见 TelemetryReporter
                                               //   `CurrentConfigVersion` 的「为何是委托而非值」)。
                                               () => ResolveGlobalConfigVersion(dataProvider));
        }

        /// <summary>
        /// 最小内联病种注册表 —— sprint-05 T1.1 的接线载体。
        /// <para><b>注册表解析的现行决策(2026-10-22 B1 接真后)</b>:三级优先,写在
        /// <c>AssembleCore</c> 的「病种注册表 + 全局趋势窗口」段 ——
        /// ① caller 显式传表 ⇒ 用 caller 的;② 否则 dataProvider != null ⇒ 真产物
        /// (<see cref="DiseaseRegistryDataSet"/>);③ 否则(本方法)⇒ 最小内联表。
        /// 🔁 <b>原文</b>:「不传(null)⇒ 组合根自带最小内联注册表」—— 该二分已被 ② 插入,
        /// 原文不删;② 存在时本方法**不被调用**。</para>
        /// <para><b>为何不是 union</b>:fallback 的 DiseaseId=1 是占位,与真表可能撞号
        /// (R1-01 唯一性触构建失败)⇒ caller 有表时 fallback **整体让位**,不并表。</para>
        /// <para><b>为何不是「caller 表 + fallback 兜底追加」</b>:会让「注册表须覆盖全部在流病种」
        /// 的 fail-loud 判据静默退化(测试永远测不到真表缺病种的情形)。</para>
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
        /// <summary>
        /// 派生全局 `ConfigVersion`(ADR-014 §五「源文本集」口径 · 用户 2026-10-24 裁定 ·
        /// `TD-epic-01-f` 解除前置 ①)。
        /// <para><b>为什么 `dataProvider == null` 要抛而不是传 0</b>:传 0 会让
        /// `AC-51-D7` 的比对恒「匹配」—— 那正是 `TD-epic-01-f` 记录的假绿形态本身。
        /// 宁可让「没有 provider 却要存档」成为**显式失败**,也不让它静默绿。
        /// 本路径只在 `saveBasePath` 非空时走到(见 `AssembleCore` 的 `SaveService` 分支),
        /// 而生产路径 `BootRoot` 预载成功时 provider 恒非 null。</para>
        /// </summary>
        /// <param name="dataProvider">烘焙数据装载面;须为 <see cref="AddressablesDataProvider"/>
        /// (只有它能列出全部 22 条产物地址)。</param>
        /// <returns>全局组合 <c>ConfigVersion</c>。</returns>
        /// <exception cref="InvalidOperationException">
        /// provider 为 null,或不是 <see cref="AddressablesDataProvider"/>,或任一产物装载失败(E-13)。
        /// </exception>
        /// <summary>
        /// 叙事锚点观察钩子 —— **累积语义**,<b>当前零接线点</b>(用户 2026-10-25 裁定「丙」)。
        /// <para>抽成具名静态算子的理由:内联闭包**测不到** —— 装配级夹具拿不到那个委托,
        /// 只能靠「装配不抛」间接推断,那是自指判据。具名后可直接喂合成流做正反两向断言。</para>
        /// <para><b>锚点 Kind = `CaseClosed` / `PatternRecognized`</b>(具名列举,可 grep)。</para>
        ///
        /// <para>🔴 <b>【裁定结果】本方法<b>不接线</b> —— 它的语义与 GDD 规则十四不符</b>:</para>
        /// <para>GDD `persistence-service.md:211-215` 规则十四定义该钩子为
        /// 「**无未决医疗动作**的上下文」—— 一个**瞬时**量:急救动作进行中 / 病人未处置的
        /// **任意时刻**都须返回 false,据此拒绝读档。它要堵的是
        /// 「**带伤 / 带病 / 战斗中读档 = 逃生通道**」(`:213` ①)。</para>
        /// <para>本方法返回的是「流里**曾经**出现过锚点」= **累积**量:首次锚点之后**恒为 true**
        /// ⇒ 此后即使玩家正在做 CPR,钩子仍返回 true ⇒ **规则十四的安全门完全失效**。
        /// 这与改动前的 `null`(恒抛)相比,是把「安全但不可用」换成了「可用但不安全」。</para>
        /// <para>⚠️ 本形态曾是**用户 2026-10-25 前一裁定的字面兑现**(裁定选项原文即
        /// 「它们在流里出现即视为「锚点命中」,narrativeContext 返回 true」),但该选项描述
        /// **未覆盖「累积 vs 瞬时」这一维**。接线后暴露语义违背,故 **2026-10-25 用户裁定「丙」**:
        /// 接线回退 `null`(fail-closed),真正的瞬时源归 **10 急救动作**的运行时状态
        /// (「有没有未决医疗动作」是内存态,不在流里)。</para>
        /// <para>⇒ <b>本方法保留在盘的理由</b>:① 六条正反夹具已就位(`composition_root_test.cs`);
        /// ② 10 落地时可复用其 Kind 判定;③ 删掉会让「为什么不能这么接」的教训无处附着。
        /// <b>但它今天零生产调用点</b> —— 任何人接线前须先解决「累积 vs 瞬时」。</para>
        ///
        /// <para>⚠️ <b>锚点映射缺口(第二个,独立于上条)</b>:GDD 列三个锚点,本方法只接上两个:
        /// · 病例结案 → <c>CaseClosed</c> ✅(写者 `Sim/CaseCloseWriter.cs`);
        /// · 脉案落笔 → **无专属 Kind**;<c>PatternRecognized</c> 是「同源检测」,比脉案落笔**宽**,
        ///   属**保守补集**而非精确映射;
        /// · 出诊启动 → **零载体**(全库 `EVENT_CHECKPOINT_ANCHORS` 零命中)。
        /// </para>
        /// <para>⚠️ <b>不在本方法内</b>:定时兜底 `CHECKPOINT_INTERVAL` / 「各系统在生成该类事件时
        /// 同步触发 checkpoint」的**触发方**(本方法只提供判据,不调用 <c>Checkpoint</c>)
        /// ⇒ <c>AC-7a-20</c> 仍不得声称已满足。以上全部归 <c>TD-persist-04</c>。</para>
        /// </summary>
        /// <param name="events">全序事件流(只读;空集 ⇒ false)。</param>
        /// <returns>流内**是否出现过**叙事锚点事件(**累积**语义 —— 见上方 🔴 段)。</returns>
        /// <remarks>
        /// 全扫 O(n),无游标缓存 —— 若将来接线,调用点会是 `Checkpoint` / `Load`(事件粒度,非每 tick)。
        /// 刻意不缓存:读档走 <c>EventStream.Restore</c> **替换**流,缓存态会失效而**静默**留旧值。
        /// ⚠️ <b>今日零生产调用点</b>(裁定丙回退接线);六条夹具仍覆盖其行为。
        /// </remarks>
        public static bool HasNarrativeAnchorEvent(IReadOnlyList<SimEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            for (int i = 0; i < events.Count; i++)
            {
                EventKind kind = events[i].Kind;
                if (kind == EventKind.CaseClosed || kind == EventKind.PatternRecognized)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// **备选判据(未接线 · 已裁定不采用)** —— 瞬时语义:流的**最后一条**事件是叙事锚点。
        /// <para>🔁 <b>裁定结果(2026-10-25 用户裁定「丙」)</b>:本方法**不采用**,理由有二 ——
        /// ① 比 GDD 更严(锚点后走两步就不能存/读);
        /// ② **仍有一个洞**:病人 A 未处置时若发生病人 B 的病例结案,最后一条是 <c>CaseClosed</c>
        ///   ⇒ 门误开 ⇒ <c>AC-7a-19 (c)</c> 仍有缺口。真正的瞬时源归 10,见
        ///   <see cref="HasNarrativeAnchorEvent"/> 的 🔴 段。</para>
        /// <para>保留在盘的理由同累积版:供 10 落地时参考,且让「为什么两条路都不通」可考。</para>
        /// <para>与 <see cref="HasNarrativeAnchorEvent"/> 的区别就是「累积 vs 瞬时」:</para>
        /// <list type="bullet">
        /// <item>累积版:首次锚点之后**恒 true** ⇒ 规则十四的「急救中拒绝读档」安全门**失效**(fail-open)。</item>
        /// <item>瞬时版:锚点之后只要有**任何**后续事件(含 <c>EmergencyAttempt</c>)即为 false
        ///   ⇒ 安全门恢复,但代价是「锚点之后玩家走了两步」就又不让存/读了
        ///   —— 比 GDD 的「无未决医疗动作」**更严**(GDD 只要求没有未决动作,不要求流停在锚点上)。</item>
        /// </list>
        /// <para>⚠️ 两版**都不等于** GDD 规则十四的语义。真正的判据需要「有没有未决医疗动作」
        /// 这个**独立于流**的瞬时状态源(归 10 急救动作的运行时状态)。本方法**未接线、无调用点**
        /// —— 已裁定不采用,不得接线。</para>
        /// </summary>
        /// <param name="events">全序事件流(空集 ⇒ false)。</param>
        /// <returns>流非空 且 最后一条事件的 Kind ∈ {CaseClosed, PatternRecognized}。</returns>
        public static bool LastEventIsAnchor(IReadOnlyList<SimEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (events.Count == 0) return false;
            EventKind kind = events[events.Count - 1].Kind;
            return kind == EventKind.CaseClosed || kind == EventKind.PatternRecognized;
        }

        private static uint ResolveGlobalConfigVersion(IDataProvider dataProvider)
        {
            if (dataProvider == null)
                throw new InvalidOperationException(
                    "[E-13][data-core] 无法派生全局 ConfigVersion:dataProvider 为 null。" +
                    "启用存档路径(saveBasePath 非空)时必须有烘焙数据装载面 —— " +
                    "传 0 占位会让 AC-51-D7 恒『匹配』(TD-epic-01-f 记录的假绿形态),故拒绝。");

            if (!(dataProvider is AddressablesDataProvider addressables))
                throw new InvalidOperationException(
                    "[E-13][data-core] 无法派生全局 ConfigVersion:dataProvider 类型为 " +
                    $"{dataProvider.GetType().Name},不是 AddressablesDataProvider。" +
                    "只有后者持有全部 22 条产物地址(DataCorePreloader.DataCoreAddresses)。");

            return addressables.LoadGlobalConfigVersion();
        }

        private static IReadOnlyList<DiseaseRegistryEntry> CreateFallbackRegistry()
        {
            var entry = new DiseaseRegistryEntry
            {
                DiseaseId = 1,
                DiseaseKey = "fallback",
                // 2026-10-21 S18-1:占位条目是**病种**,不是伤情(零值陷阱见 DiseaseKind 注释)。
                // ⚠️ 本装配路径不调 RegistrySchemaValidator.Validate ⇒ R1_CHECK_31 不覆盖此处,
                //    显式赋值是**纪律**不是**门**;生产侧消除随真病种表落地(ADR-014 阶段 2)。
                Kind = DiseaseKind.Disease,
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
                // ⚠️ 2026-10-23:`Array.Empty<TreatableByEntry>()` → **null**。
                //    非 null(哪怕空数组)会踩 R1-19 的第二真源守卫 —— 该守卫现在抛
                //    `RegistryValidationException` 而不是 `NotImplementedException`(见
                //    `RegistrySchema.cs` R1-19 处)。本装配路径当前不调 `Validate`
                //    (同 R1-31/33 的覆盖面登记),但留一个**必然踩门**的值是陷阱:
                //    谁将来在这条路径上补一道 `Validate`,红的是他而不是写这行的人。
                TreatableBy = null,
                // 派生字段(GDD :329「写入期不填」)—— 这里经 `Derive` 走同一实现,
                // 不手写字符串字面量:`fallback` 既不在照护适用集、也不是破伤风、
                // 且无对因处置 ⇒ `symptomatic_only`。硬编码字面量 = 第二份派生逻辑。
                Handle = DiseaseHandle.Derive("fallback", hasCausalTreatment: false),
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
