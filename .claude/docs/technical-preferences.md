# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

> **引擎已切换:Godot 4.6 → Unity 6.3 LTS**(2026-09-13,随 `/brainstorm` 概念
> 《大医精诚:破晓之剂》而定)。`docs/engine-reference/godot/` 保留在库中,
> 但**不是本项目依据**。现行依据:`docs/engine-reference/unity/`。

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: URP (Universal Render Pipeline)
- **Physics**: PhysX(默认 3D);若大型 NPC / 瘟疫模拟走 DOTS,则该子系统用 Unity Physics
- **XR**: OpenXR —— **仅用于「急救动作」小游戏**,平面为主形态
- **Build System**: Unity Build Pipeline(CI 用 Unity Build Automation)
- **Asset Pipeline**: Unity Asset Import Pipeline + Addressables

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Steam) 优先;VR(OpenXR)为急救动作的可选模式;**主机延后至 P2**
- **Input Methods**: Keyboard/Mouse、Gamepad、VR 手柄/手势(仅 VR 模式)
- **Primary Input**: Keyboard/Mouse(生存建造类在 PC 上的主导输入)
- **Gamepad Support**: Partial —— 手柄需完整可用,但不作为一等公民
- **Touch Support**: None
- **Platform Notes**:
  - 联机 1-4 人合作,**架构从 P0 起预留**(见下方 Architecture Decisions Log)
  - **拟物 UI(无血条 / 无小地图)必须同时支持键鼠与手柄导航** —— 手柄没有指针,
    脉案 / 出诊箱 / 纸质地图的全部交互都需要焦点导航路径
  - VR 模式不承担开放世界移动(晕动症),只做站定式急救操作

## Naming Conventions

- **Classes**: PascalCase(如 `PlayerController`)
- **Public fields/properties**: PascalCase(如 `MoveSpeed`)
- **Private fields**: _camelCase(如 `_moveSpeed`)
- **Variables**: 局部变量与参数用 camelCase
- **Methods**: PascalCase(如 `TakeDamage()`)
- **Signals/Events**: C# event 用 PascalCase(如 `public event Action<DamageInfo> DamageTaken;`)
- **Files**: PascalCase 与类名一致(如 `PlayerController.cs`)
- **Scenes/Prefabs**: PascalCase(如 `MedBay.unity`、`Patient.prefab`)
- **Constants**: PascalCase 或 UPPER_SNAKE_CASE

## Performance Budgets

> **状态:临时值** —— 待确定最低目标硬件后定稿(见 `game-concept.md` §技术考量)。

- **Target Framerate**: 60 fps(平面)/ **90 fps(VR 模式,硬性)**
- **Frame Budget**: 16.6 ms(平面)/ 11.1 ms(VR)
- **Draw Calls**: 待定 —— 开放世界 + 建造系统,需先确定美术密度
- **Memory Ceiling**: 待定

> **本项目特有的附加预算**:
> - **急救动作输入延迟 < 50 ms** —— 手感小游戏的核心是「手稳」,延迟直接杀死它
> - **联机时的急救操作同步精度** —— 必须与「可选跳过」的降级路径兼容

> **✅ sim 实体数(已裁定 —— OQ-8 结案 · 2026-09-20 用户裁定)**:
> `disease-simulation.md` 登记的 **OQ-8** 已裁定:**模拟范围 = 在场才模拟**(离屏病人不进 `Step`,
> 由事件流 + 闭式 `CatchUp` 补算)· **`PATIENT_APPEARANCE_CAP = 24`**(同场被模拟病人数硬上限)。
> **该输入是两处的前置,现已解锁**:① ADR-005 性能表 CPU 行的量纲(`24 × 20 Hz × 单次求值`);
> ② **ADR-017 §三 的 DOTS 复评门**(触发判据阈值见下)。
> ⚠️ **CAP 的语义不变**:仍是 ADR-008 §六 有界性论证的**硬界** —— 52 疫情事件与季节调制**在界内**取值,
> 超界拒收(集成断言);「在场才模拟」**不得**被读成「全域模拟再 LOD」。
> ⚠️ **仍归数值轮**:曲线参数 / 半衰期 / 权重等**逐病种数值**冻结未动(承「数值用户自己调」)。

> **✅ tick 频率(已裁定 —— OQ-25-8 结案 · 2026-09-20 用户裁定)**:**20 Hz** ⇒ **`TICK_SECONDS = 0.05`**
> (即 `TICK_PERIOD = 50 ms`;两名为同一量,以 `entities.yaml` 的 `TICK_SECONDS` 为准)。
> 全案以 tick 计的量(`*_ticks` / `cooldown_ticks` / `duration_ticks` / `hold_ticks` / `edge_ticks` /
> `half_life_ticks` / `WEATHER_BLOCK_TICKS` / `ROLL_INTERVAL` / `COOLDOWN_TICKS` / `NOVELTY_COOLDOWN` /
> `DWELL_DURATION` / `STALE_WINDOW` / `RECHECK_WINDOW` / `JITTER_MAX`)的**量纲自此有值**。
> **三条已核验的约束**:
> ① **`O-10-3` 量化下限满足**:`TICK_PERIOD(50 ms) ≪ CPR 动作周期(≈ 550 ms)` —— 比值 **11×**,
> ≥ 该条建议的「至少 10 倍」⇒ `JITTER` 门**不退化为噪声**(10 侧约束就此满足,不得再各系统自填)。
> ② ⚠️ **`L_eval ≤ TICK_PERIOD = 50 ms` 与 `L_input < 50 ms` 同量级** —— 承 `emergency-procedures.md`
> F-10.6 的切分口径:`AC-10-08` **只测 `L_input`**(预表现路径,10 全责),`L_eval` 归 9 的求值节奏;
> **「主机 `Append` 到体征可见」的实际延迟 = 50 ms + 求值次序**,**不**受 50 ms 手感预算约束(玩家对
> 体征何时变本就延迟一个 tick)。**不得**把两者重新合并成一个「端到端 < 50 ms」。
> ③ **与 60 fps 帧时间(16.67 ms)不整除** ⇒ 一个 tick 跨约 3 帧:实现期须定**步相位**(每 tick 恰好
> 一次 `Step`,由 `ITickProvider` 驱动,**不由渲染帧驱动**)—— 登记为**实现期义务**,不改本裁定值。
> ⚠️ 若实现期实测该相位造成问题,**须另裁 `TICK_SECONDS`**(改它 = 9 / 5 / 25 / 7a 四份文档同时失效,
> 且**须重算所有历史事件的时间戳与存档**),**不得由单个系统自行调频**。

> **✅ DOTS 复评门(ADR-017 §三,登记点 —— 阈值已由用户裁定 2026-09-20)**:
> - **input**: **OQ-8 已标定**(2026-09-20:在场模拟 + `CAP = 24`)
> - **trigger**: **同场实体数 ≥ 100** **或** **表现层帧时间占比 ≥ 8 ms / 16.6 ms(≈ 48%)**
> - **当前评估**:CAP 24 ⇒ 第一判据在 sim 侧**不可能触发**(24 < 100);触发面**仅剩**表现层群集 /
>   VFX / 渲染批处理自身的实体数,与病人模拟规模**解耦** ⇒ **P0 预期不触发**;一旦实测逼近阈值,
>   按 action 另开 ADR
> - **scope**: **仅表现层**(群集转向 / VFX / 渲染批处理 / Unity Physics 边界)
> - **action**: **另开 ADR**(不得以 ADR-017 的修订直接改判);触发前表现层不引入 DOTS
> - **不得触碰**: sim 侧任何部分(ADR-017 §一 已结构性排除)

## Testing

- **Framework**: Unity Test Framework(UTF / NUnit)+ Unity Test Runner
  - **EditMode** → 纯逻辑:判断链、熟练度成长、伤害公式、库存
  - **PlayMode** → 集成:急救动作流程、建造、联机基础
- **Minimum Coverage**: 待定 —— 建议先覆盖游戏逻辑(BLOCKING 级)
- **Required Tests**: 判断链逻辑、熟练度成长公式、战斗伤害公式、联机状态同步

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- [None configured yet — add as dependencies are approved]

> **候选但未采纳**(来自 2026-09-13 初稿 GDD §12;**均需 ADR 裁决后方可登记**):
> 地形 Gaia Pro / MapMagic 2 → **✅ 2026-09-15 ADR-015 裁定:仅编辑期工具(素材 / 预览),
> 非运行期依赖,不登记为 Allowed Library、不进构建**。· 联机 Netcode for GameObjects **或**
> Photon Fusion(**二选一**;ADR-001 已定 P0 先立 pipe 抽象,库选型 P1b 前 swap 评审)·
> 叙事 Ink / Yarn Spinner · 建造 Voxel Play 或自研模块化网格 → **✅ 2026-09-15 ADR-015 裁定为
> 自研模块化网格(Voxel Play 不采纳)** · AI Behavior Tree + DOTS → **✅ 2026-09-15 ADR-016 裁定:
> 行为树仅编辑器期作者态(第三方工具须过导出 / 烘焙可行性评估,不合格则退回自研数据表),
> 运行期零第三方行为树 / 寻路库**;**2026-09-17 更新**:**编辑器期可视化行为树工具**亦由用户裁定
> **不引入**(P0 直接走「自研数据表载体」—— 手写 `assets/data/ai_enemy.json`;`PR-27-6` ④),
> 故「第三方工具评估」一项**已消解**,AI 侧**零第三方、零可视化工具**。
> **DOTS 已于 2026-09-15 由 ADR-017 裁定:
> sim 侧结构性排除(门 A),表现层留复评门(不采纳为当前依赖)**。
> 音频中间件(FMOD / Wwise 等,初稿未列但同属「第三方运行期」)→ **✅ 2026-09-15 ADR-018 裁定:
> 不引入** —— 44 走 Unity 内置 AudioMixer + 音频事件表烘焙管线(ADR-014),运行期零第三方音频中间件。
> 分析 / 遥测 SDK(Unity Analytics / UGS / 崩溃上报)→ **✅ 2026-09-15 ADR-019 裁定:不引入** ——
> 51 本地优先,零出厂数据。
> 相机工具(Cinemachine,初稿未列但同属「第三方运行期」)→ **✅ 2026-09-15 ADR-020 裁定:不引入** ——
> 相机走自建机位(`ICameraRig`),Cinemachine 3.0 的知识风险由「不使用它」消解。
> **注意:「官方包」不是零第三方取向的豁免** —— DOTS 亦为官方包,已由 ADR-017 结构性排除。
> **在 ADR 通过之前,这些都不算已批准依赖。**

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [ADR-001 ✅ Accepted 2026-09-15]**联机选型:45 网络层 pipe 抽象,库延后** ——
  `docs/architecture/adr-001-networking-pipe-abstraction.md`。
  **P0 先定 pipe 抽象(`IReplayPipe` Publish/Subscribe + 有界 `ReorderBuffer`),底层库
  (NGO / Fusion)推迟到 P1b 前一次 swap 评审再定**。pipe 契约 = **可靠即可,保序非必需**
  (重排由三流全序键 `(Tick, StreamPriority, Patient, Seq)` 吸收);表现态位置走
  **第二 QoS 通道**(unreliable latest-value,防 HOL 阻塞)。系统 45 = **P0 预埋 · P1b 实现**。
  **约束**:选型须承载**三条逻辑流**(病史 / 病例 / 世界)+ 表现态位置同步;`TR-concept-002` 解除 blocking。
- [ADR-002 ✅ Accepted 2026-09-15]**开放世界地形方案:手工烘焙固定世界** ——
  由 **ADR-015** 兑现。第三方(Gaia Pro / MapMagic 2)**仅编辑期辅助,运行期零第三方**;
  静态世界几何走**确定性整数逻辑层**(经 ADR-014 烘成 `*.cooked`)+ **纯视觉层**(Unity Terrain)。
  详见下方 ADR-015 条目。
- [ADR-003 ✅ Accepted 2026-09-15]**建造系统网格方案:模块化网格** ——
  由 **ADR-015** 兑现。**否决 Voxel**;建造槽位**与地形共用同一世界格**。
  详见下方 ADR-015 条目。
- [ADR-004 ✅ Accepted 2026-09-15]**是否需要 DOTS** —— 由 **ADR-017** 兑现。
  **分层裁决:sim 侧永不上 DOTS(结构性排除 —— 门 A `"noEngineReferences": true` 与
  `Unity.Entities` 等引擎程序集天然冲突);表现层留复评门(触发 = OQ-8 标定 + 阈值待用户裁定 —— **✅ 2026-09-20 两项均已裁定:OQ-8 = 在场才模拟 / CAP 24;阈值 = 同场表现实体 ≥ 100 或 表现层帧时间 ≥ 8 ms**)。
  ADR-004 就此结案。**详见下方 ADR-017 条目。
- [ADR-005 ✅ Accepted 2026-09-13]**确定性模拟与状态同步模型** ——
  `docs/architecture/adr-005-deterministic-sim.md`。
  **全部模拟数学在整数定点域**(int64 / Q16.16)· **病史事件流是唯一真源** ·
  **主机唯一执行 Step / CatchUp** · P0 必须预留五个抽象点
  (`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery` / `SimEvent`)。
  **必须在写 9 / 7a / 25 的代码之前 Accepted** —— 它定义数据形状,事后改 = 重写每个病种。
  Engine Knowledge Risk **HIGH**(IL2CPP 逐位性需实测),但本裁决**不依赖该实测**
  (刻意不用任何 post-cutoff API)。
- [ADR-006 ✅ Accepted 2026-09-14]**定点域边界数据契约** ——
  `docs/architecture/adr-006-fixed-point-boundary-contract.md`。
  ADR-005 只定义了定点域**内部**的纪律,**未定义域边界**;本 ADR 补齐:
  **唯一解析入口 `FixParse`(拒浮点字面量)** · **存档与事件流禁 `float`** ·
  **单一舍入模式 `ROUND_HALF_AWAY_FROM_ZERO`(禁 `Math.Round` 默认 ties-to-even)** ·
  **守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`(`EFF_MAX ≤ 1`)在整数域内求值**。
  **2026-09-14 二轮复核就地修正三处口径错误**(均属**静默**失败模式):
  ① `weight` / `stack_max` 是 `int` 计数,**移出 `Fix` 解析集**(D-21-17);
  ② **`Fix` 不可经 Unity 内置序列化器承载**(原断言「`Fix` 序列化为内部 `long`」**为假**),
  须自定义编码器,由 EditMode 探针守住(D-21-18,新增 §Decision 五);
  ③ 守恒律原式**量纲不齐**,须乘 `weight` 归一(D-21-19)。
  并以两条修正案回填 ADR-005 的自相矛盾:
  **Amendment A(D-9-D)** `SimEvent` 补 `Seq` + `Payload`(✅ 已定)·
  **Amendment B(D-9-E)** `patient_id` 跨权威稳定性 —— ✅ **2026-09-14 用户裁定取机制 A
  「计数器 + 高水位可重构」**(计数器永不复位 0 · 迁移后 `next = max(patient_id) + 1` 由事件流重构 ·
  **终态折叠行必须保留 `patient_id`**)。备选 B(纯哈希派生)**已否决**。
  **ADR-005 的 `:126-127` 与折叠规则已就地修正**;残留实现项归 **7a 持久化 / 45 网络层**。
  Engine Knowledge Risk **MEDIUM**(不依赖 post-cutoff API)。

- [ADR-007 🟢 Accepted 2026-09-15]**事件权威与掷骰状态** ——
  `docs/architecture/adr-007-event-authority-and-roll-state.md`。
  由 **52 随机事件导演二轮复核**(2026-09-15)提出:52 曾**越权**断言两个跨域决定,
  而 ADR-005 / ADR-006 均未覆盖。四项裁决(2026-09-15 用户裁定,均照准):
  ① **`IEventAuthority` = 第六个 P0 抽象点**(不与 `IEventSink` 合并 —— 写入通道语义
  vs 掷骰权语义不同);② **`WorldSeed` 归 7a 持久化的存档头**(非「一条 `SimEvent`」,
  避免鸡生蛋);③ **掷骰的每个输入必须可从事件流重构**(核心不变量;新增
  `EventRolled` / `EventArrived` / `ThreatDeferred` / `ThreatDeferralCleared` /
  `HistoryFlagChanged` 五个 `Kind`);④ 世界级事件用 `PatientId.None = -1` 哨兵,
  不污染 `max(patient_id)` 高水位。
  **原 Blocks(52 的代码实现)已解除**;**不阻塞** 37 病例的内容撰写。
  Engine Knowledge Risk **LOW**。

- [ADR-008 ✅ Accepted 2026-09-15]**病例事件流(第二条逻辑流)** ——
  `docs/architecture/adr-008-case-event-stream.md`。
  由 **37 病例系统复核 12 项 blocking** 提出:病例生命周期事件无确定性存储与全序。
  三项用户裁定(均照准):**跨流全序 = 流优先级**(病史 < 病例);**「已处置」= 处置事件为证**
  (非玩家自述复选框 —— 否则可空手连关三例触发 Boss,与支柱四冲突);
  **盐 = `WorldSeed` 派生**(保密语义收窄为**防御纵深**,非技术保密层)。
  裁决:**新增独立病例流**(病史流之外第二条逻辑流)· **按 `Kind` 路由**(`IEventSink.Append` 纯函数);
  **跨流全序键 `(Tick, StreamPriority, Patient, Seq)`**;三 `Kind` + 判断记录 `Kind`
  (`CaseOpened` / `CaseClosed` / `PatternRecognized` + 读数 / 落笔 / 改写史);
  **处置证据窗口化 + 快照**;**病例流不物理折叠**(拒绝折叠豁免外的实现)。
  经 Opus TD 复核「需调整非否决」,五点修订已并入(Problem 补三驱动约束 · 全序键补 `Patient` ·
  处置证据窗口化 · 保密措辞收窄 · Dependencies 补 7a / ADR-001 / 13)。
  **残留实现义务**:7a 折叠谓词 `Folded(p)`(含「无未结案病例」)· `max(patient_id)` 扫三流并集
  · 9 侧补登记「病人出现率上限」有界性配置项。
  Engine Knowledge Risk **HIGH**(Unity 6.3 post-cutoff;但裁决**刻意不用 post-cutoff API**,同 ADR-005)。

- [ADR-009 ✅ Accepted 2026-09-15]**世界状态的事件化边界(三问判据 · 三态分类 · 第三条逻辑流)** ——
  `docs/architecture/adr-009-world-state-event-boundary.md`。
  由 **架构复核 B-7**(报告定为最高优先级)提出:开放世界日常状态(采过的药草 / 掉落物 /
  建造物 / 医馆状态)**哪些进流、哪些只做表现,从未被裁决** —— 不裁决则「确定性」范围在实现期
  被各系统各自扩大(21a 已把 `IIdAuthority` 扩到 `ItemInstanceId`,D-21-26)。四条用户裁定(均照准):
  **三问判据 / 三态分类**;当用户其余三项裁定为:**静态地形 = 确定性重建** ·
  **掉落实体身份进流 / 位置表现** · **新增世界流**(当轮原口径为「`WorldSeed` 逐位重建」)。
  裁决:**三问判据**(真源 / 权威 / 可感知)判定进流与否;**三态分类**(**模拟态**进流 ·
  **派生态**由外生源确定性重建 · **表现态**走网络层);**新增第三条逻辑流「世界流」**;
  **世界流不折叠 + 有界性论证**;**拾取判定 = 意图事件 + 当下判距 + 宽容半径**;
  以 **Amendment E** 升格 ADR-006 Amendment C / D 至三流口径。
  **2026-09-15 ADR-015 修订**:§四 静态地形 / 生态区从「`WorldSeed` 逐位重建」改为
  「**烘焙逻辑层加载重建**」,派生态判据推广为两类源(种子纯函数 / 版本化烘焙数据);
  §七 `spawn_anchor` 随之由定点坐标改整数格。
  Engine Knowledge Risk **MEDIUM**(**由 HIGH 降级** —— ADR-015 后地形不再经 Unity Terrain 运行期生成;
  残留表现态项 = PhysX 落点异机差)。
  **⚠️ 2026-09-18 Amendment I(承 10 急救动作 GDD 首轮评审 · 根因 R-2)**:追加**三个病史流 Kind**
  —— `EmergencyAttempt`(10 产出 / **主机物化**)· `EmergencyTreatmentApplied`(**10 写**)·
  `DrugTreatmentApplied`(**11 写**)。**这是首次以「系统 GDD 追加 Kind」通道扩病史流**
  (前六项走该通道的追加全在世界流;`InjuryOnset` 属病史流但由 25 的 GDD 直登、未走本通道)。
  失效模式同 Amendment G/H:10 原稿称载荷为 `TreatmentEvent`,**是内联元组不是具名 Kind**
  ⇒ 9 的白名单(`disease-simulation.md:183-184`「列表外构建期拒绝」)会**拒收 10 的每一笔写入**。
  **§二 域归属表就地同步**(病史流第 26~28 支)。⚠️ **新形态**:`EmergencyAttempt` 是**判定输入**
  而非结算 ⇒ 一条动作落**两条**病史流事件 —— `ADR-009 §七` 的三段式**首次在流层物化两步**。
  有界性 = 每完成动作 ≤ 2 条,**与帧率无关**(§六 扩展不重写)。

- [ADR-010 ✅ Accepted 2026-09-15]**7a 持久化与存档格式(全二进制 codec · 单一义务清单)** ——
  `docs/architecture/adr-010-persistence-save-format.md`。
  系统 7a「持久化服务」是**全案唯一无 GDD 的 Core 系统**,却被 ADR-005 / 006 / 007 / 008 / 009
  **五份 ADR 集体委派义务**(存档头契约 · 三流序列化 · 折叠 · 快照 · `ItemInstanceId.Next()` ·
  迁移协议),**它自己没有权威件** —— 义务散落、部分冲突。四条用户裁定(均照准):
  **全二进制 codec**(不用 JSON / PlayerPrefs);**校验和 + 自动回退**;
  **定期 checkpoint(默认 5 分钟)+ 退出保存 + 7b 手动槽**;**版本号 + 迁移脚本**。
  裁决:存档布局 = **头部 + 三逻辑流 + 快照**;§三 **义务汇总表**收敛全部既有委派
  (**单一出处,一处不留**);§四 损坏恢复;§五 `ItemInstanceId.Next()` 机制落点;
  §六 存档时机;§七 迁移协议。**2026-09-15 ADR-014 补注**:TR-itemdb-032「配置版本号联动」
  现读 `adr: ADR-010 + ADR-014` —— **头部字段归本 ADR,`ConfigVersion` 派生(内容哈希 u32)归 ADR-014 §五**。
  **残留**:`random-events.md` 仍写「存档头(或创建时的一条 `SimEvent`)」= 与 ADR-007 已 Accepted 裁决冲突,待修。
  Engine Knowledge Risk **MEDIUM**(原子写 / 后台线程 / 哈希须实测;**刻意只用 BCL 稳定 API**,
  不用 `UnityEngine.Hash128`)。

- [ADR-011 ✅ Accepted 2026-09-15 · **2026-09-18 Amendment B(F1 改判)**]**输入架构(Input System 动作映射与急救延迟路径)** ——
  `docs/architecture/adr-011-input-architecture.md`。
  系统 3 输入与设备 = **架构复核 §2 记为零覆盖的系统**(GDD ❌ / 架构覆盖 ❌),
  `TR-concept-007`(急救 < 50 ms)与 `TR-concept-008`(手柄焦点导航)均为 gap。三条用户裁定(均照准):
  **action-based 官方路线**(Input System 动作资产,K&M + Gamepad + OpenXR 三套绑重 +
  `bindings overrides` 持久化;Legacy Input Manager 已弃用);
  **急救动作独立直读通道**(< 50 ms 直读动作值,**不穿过 42 UI 事件栈**);
  **焦点导航接口归本 ADR · 呈现归 R-6**(接口 / 呈现分离)。
  经 unity-specialist lean 复核(2026-09-15):**无引擎侧 blocker**,F1–F6 结论并入 §Risks
  (联机判定归表现层本地即时 · 双导航风险 + 意图事件流 · 绑重 GUID 稳定性 · float 边界 ·
  OpenXR 通用绑定 + 渲染延迟 · 预缓存 action 引用)。
  **Enables** 3 / 4 / 10 / 42 的输入层契约 + `TR-concept-007`(gap → covered)。
  **⚠️ 2026-09-18 Amendment B —— F1 改判**(由 **10 急救动作 GDD 首轮 `/design-review`** 的
  根因 R-1 提出,用户裁定取 **C 路**):原 F1「联机急救判定**归表现层本地即时**,仅结果事件进流」
  **作废**。三条独立改判理由:① 判定输入不在流里 ⇒ **违反 ADR-016 §一 三源不变量 + ADR-005
  唯一真源**,`AC-10-05` 的跨平台逐位重放**不可能实现**;② 客户端上报 `JudgeResult` = 把**流的
  内容**交给客户端诚实性做保(伪造成本 = 改一个枚举值,收益 = 满 potency);③ 原理由
  「判定走主机权威必然破 < 50 ms」是**范畴错误** —— `L_input` 是**「输入 → 呈现」预算,
  不是「权威归属」预算**(玩家对「事件何时进流」不可感知,对「体征何时变」本就延迟一个 tick)。
  **现口径**:客户端**聚合为一条 `EmergencyAttempt` 全整数意图事件**上行 → **主机执行 `Judge` +
  `Append` + 发号 `Seq`**;本地判定**降级为预表现**。**「不逐帧同步输入」保留**(原裁决对的那一半)。
  ⇒ 急救侧从此**回到与 ADR-009 §七 拾取完全同构**(拾取的三段式此前在急救被自己开了例外)。
  **未结 `OQ-10-9`**:`EmergencyAttempt` 走 ADR-001 第二 QoS 通道会**丢**,而它是判定输入
  ⇒ 须 **ADR-001 的一次窄修订**(归 45 的 GDD 轮,P1b 前);**不在 ADR-011 打补丁**。
  Engine Knowledge Risk **HIGH**(Input System 6.3 具体行为须实测;**接口层为纯 C# 契约,不受影响**)。

- [ADR-012 ✅ Accepted 2026-09-15]**跨平台确定性 CI 门(黄金夹具矩阵与 IL2CPP 编译旗标登记)** ——
  `docs/architecture/adr-012-cross-platform-determinism-ci-gate.md`。
  由 **架构复核 R-5 / B-9** 提出:ADR-005 / 006 / 009 / 010 均声明「IL2CPP 与 Mono 逐位一致实测」
  类义务,但 `tests/` 与 `.github/workflows/` 均不存在 —— **判据是纸面的**。三项用户裁定(均照准):
  ① **双级黄金夹具**(单元级哈希:sim 纯逻辑 `Fix` 四则 / 负值右移 / 定点 `Exp` / `SplitMix64` /
  `CDF walk` / 编码器往返;集成级字节:ADR-010 存档字节流;字节错位可下钻到单元级算子);
  ② **三格全免费常驻矩阵**(Linux-x64-Mono + Linux-x64-IL2CPP + Linux-ARM64-IL2CPP 交叉构建;
  Windows-x64-IL2CPP 与 Apple Silicon 列**发版前必跑**,不进常驻;Linux runner 零计费;
  **qemu 否决** —— softfloat 不可信为逐位判据);
  ③ **版本化刷新**(`golden-vN` + 变更日志 + 全体平台同时重签 + 旧版保留回归对比;
  禁单平台独签)。
  **工具链修正**:`unity-test-runner@v4` 只在 Editor(Mono)跑 UTF —— IL2CPP 对拍须
  `unity-builder@v4` 出 player + 独立 job(引擎复核 F1)。**BLOCKING spike(F7)**:C# `int64`
  溢出为定义性回绕,IL2CPP 生成的 C++ 有符号溢出为 UB —— `SplitMix64` 与 Q16.16 中间乘正踩此线。
  Engine Knowledge Risk **HIGH**(矩阵实现须 spike,判据本身为纯规格)。

- [ADR-013 ✅ Accepted 2026-09-15]**拟物 UI 框架(UI Toolkit 主 + UGUI 补 world/XR)** ——
  `docs/architecture/adr-013-skeuomorphic-ui-framework.md`。
  由 **架构复核 R-6 / E-16** 提出:E-16 明写「**UI Toolkit 无原生 world-space / XR 支持** ⇒
  同一 UI 两套栈**要早决**」。三条用户裁定(均照准):
  ① **UI Toolkit 为主 + UGUI 补 world-space / XR**(平面拟物 UI:脉案 / 出诊箱 / 纸质地图 /
  存档位 / 教学 → UI Toolkit UXML/USS;世界空间体征与 **VR 急救 → UGUI world canvas,
  VR 必须 World Space**);
  ② **P0 定两栈接口与元件库契约,VR / world-space 实现推 P1a**(VR 急救不在 P0 关键路径;
  E-16 要求早决的是**方向**,不是双栈都在 P0 完工);
  ③ **拟物视觉 = 自建 USS 元件库**(纸纹 / 墨迹 / 卷轴九宫格 `-unity-slice-*` + 主题变量)
  **+ UXML 组合**;UGUI 侧语义对齐。
  **承 ADR-011**:焦点呈现侧 = 官方桥 `NavigationMoveEvent` + `FocusController` 引擎自动焦点移动
  (**不自实现焦点算法**,禁同键双触发);**焦点单栈门**(同一时刻仅一栈接收导航意图流);
  **两栈共用同一 EventSystem**(禁双 EventSystem / 双输入模块)。
  **承 §9 C3**:42 **只渲染,永不持有游戏状态**;UI 只读 `VitalsDto`;`disease_id` 不进呈现层
  (落地 = `PresentationDtoGuard` **递归**反射扫描,AC-37-15)。
  Engine Knowledge Risk **HIGH**(焦点桥 / world-space / UI Toolkit 自定义材质 / 图集 / 无障碍均须 spike;
  报告 §6.6 假设 6「UI Toolkit 运行时手柄焦点导航可用」= ⚠️ 半可信,原型 spike 为前置)。

- [ADR-014 ✅ Accepted 2026-09-15]**数据管线与 JSON 解析器(作者态外部化 · 构建期烘焙 · 两阶段工具链)** ——
  `docs/architecture/adr-014-data-pipeline-and-json-parser.md`。
  **合并架构复核 R-7(Addressables 数据管线)+ R-8(JSON 解析器选型)** —— 出货形态这一承重决策
  把两者锁在一起。三条用户裁定(均照准):
  ① **作者态 = `assets/data/[system]_[name].json`**(承仓库既有先例 `item-database.md:290` /
  `random-events.md:617-628`,**非本文新立**;把「必须外部化」收窄到**承载 `Fix` 的字段** ——
  `Fix` 物理上不可能住 `.asset`,纯 `int` 常量若 GDD 声明为固定常量则合法);
  ② **出货形态 = 构建期烘焙** —— JSON 走两阶段工具链(阶段1 `JsonTextReader` 仅词法 ·
  阶段2 自研 per-schema 绑定 + `FixParse` + 白名单/守恒/区间/长度/schema_version 校验)烘成
  deterministic `*.cooked`(`Fix` = raw `long`);**玩家构建零 JSON 解析器、零 `FixParse`**,
  R-8 的 LOW 评级由**结构性规避**坐实(解析器逐平台逐位性从运行期确定性面消失);
  ③ **解析器 = Newtonsoft 仅作词法器 + 自研绑定层** —— **禁 `JsonConvert.DeserializeObject<T>`**
  (数字经 `double`/`decimal` 中转 = 绕 `FixParse` 的浮点泄漏);**`Fix` 字段在 JSON 里写字符串**
  (`"offset": "3/4"`,非 JSON 数字)。
  **§五 Addressables**:单一 `data-core` 组 + 首次 `Step` 前启动预载(门 A:sim 零 `UnityEngine`,
  装载落边界程序集 `IDataProvider`);E-13(6.2+ 抛异常)⇒ 启动期硬失败,不 null 解引用;
  **`ConfigVersion` = 源数据集内容哈希派生**(u32,改数值自动改版本号),与 ADR-010 §七 存档头比对。
  **§六 与 `random-events.md:617-628` 调和**:保留「Addressables 承载」意图,修正「运行期读 JSON 文本」
  字面为「运行期读烘焙产物」(52 处已加 note)。
  Engine Knowledge Risk **MEDIUM**(Addressables 6.2+ 抛异常为 post-cutoff,须实测;结构面不依赖)。

- [ADR-015 ✅ Accepted 2026-09-15]**世界几何(手工烘焙固定世界 · 单一整数格 · 模块化建造)** ——
  `docs/architecture/adr-015-world-geometry-fixed-world-lattice.md`。
  **合并架构复核 R-9(ADR-002 开放世界地形)+ R-10(ADR-003 建造网格)** —— 两者真正的接口是
  `WorldPos` 坐标系与 chunk 划分。四条用户裁定(均照准):
  ① **地形 / 生态区真相来源 = 手工烘焙固定世界** —— 4 生态区由关卡 / 美术手制,烘成静态资产出货;
  **`WorldSeed` 只驱动动态量**(病人种子 / 掷骰 / 采集散布 / 掉落表现),**不再是地形生成参数**;
  ② **范围 = R-9 + R-10 合并**;③ **建造网格 = 模块化网格**(承英灵神殿锚点,**否决 Voxel**),
  **与地形共用同一格**;④ **第三方地形工具(GAIA / MapMagic 2)= 仅编辑期辅助,运行期零第三方**。
  **§一 两层世界**:确定性整数**逻辑层**(生态区多边形 / POI / 资源点 / 导航格 / 建造槽位,经 ADR-014
  烘 `*.cooked`)+ 纯**视觉层**(Unity Terrain,float 任意);运行期只加载逻辑层;视觉层采样**禁入 sim**
  (破 ADR-006 边界)。**§三 `WorldPos` = `(i32 x, i32 y, i32 z)` 单一整数格** —— 地形 / 建造槽位 /
  掉落锚点 / 资源点 / 导航格 / 医馆房间格**共用同一套格**;`Fix` 只用于非空间模拟量。
  **§四 chunk = 流式 / 脏块优化,不是坐标原点**(禁 chunk 局部坐标系)。**§五 建造模块对齐到格**;
  医馆「红石逻辑」= 整数格邻接判定(非物理 / 非 NavMesh);NavMesh 仅服务 AI 表现态移动。
  **§二 就地修订 ADR-009 §四** —— 「派生态」分类保留,重建源由「`WorldSeed` 纯函数」改为
  「烘焙逻辑数据」;并推广「派生态」判据为两类源(种子纯函数 / 版本化烘焙数据)。
  **§七 修订清单**:ADR-009 §一(Q1 判据 / 判定流程注释)· §二(三态表)· §四(标题 + 正文)· §五
  (`spawn_anchor` 定点坐标 → 整数格)。
  **后果**:R-9 的 Knowledge Risk 从 ADR-009 的 **HIGH 降到 LOW**(不再需自研定点地形生成器 / 不再需
  地形逐位重建跨平台实测);ADR-009 §四 预置的「工具逐位实测」判据**随之作废**(工具已不在运行期)。
  Engine Knowledge Risk **LOW**(刻意不依赖任何 post-cutoff API)。

- [ADR-016 ✅ Accepted 2026-09-15 · **2026-09-17 就地修订**]**AI 架构(分层确定性 · 作者期行为程序烘焙 · 整数导航格寻路)** ——
  `docs/architecture/adr-016-ai-architecture.md`。
  **合并架构复核 R-14(13 病人 AI + 27 敌人 AI)** —— 两者共享同一套基础设施,拆开会互相预判。
  四条用户裁定(均照准):
  ① **范围 = 13 + 27 合并**;② **确定性边界 = 分层** —— **行为决策进 sim**(整数域,可重放),
  **运动表现态**(连续位移 / 动画 / 绕障)在表现层驱动,与 ADR-009 §五 的「掉落身份进流 / 位置表现」同构。
  **2026-09-15 补注(13 GDD 落盘时裁定)**:**27 的决策进 sim;13 病人 AI 的决策住边界层(呈现侧)** ——
  13 消费 `VitalsDto`(float,唯一浮点出口),**物理上不可能住门 A 的程序集**;13 仍是派生态,
  只是不在 sim 程序集内(ADR-016 §一 表已补 13 例外注;详 `design/gdd/patient-ai.md` 规则五);
  ③ **行为载体 = 作者期数据表 → 构建期烘为整数数据**(走 ADR-014 管线;运行期零第三方行为树库;
  **2026-09-17 修订** —— 作者态**不引入**编辑器期可视化行为树工具,P0 手写 `assets/data/ai_enemy.json`);
  ④ **寻路 = sim 走整数导航格,NavMesh 仅驱动表现态位移**(兑现 ADR-015 §五 已写但未细化的口径)。
  **§一 重建三源不变量(核心)**:AI 决策可重建的**充要条件**是其全部输入 ∈
  {事件流, 版本化烘焙数据, 二者的纯函数};任何第四来源(表现态位置 / 墙钟 / `UnityEngine.Random` /
  未版本化场景几何)都是**静默不可重建通道**。AI 决策是**派生态**(不进流、不存档、重建期重建),
  与 ADR-009 §一 Q1 判据同源;**效果进流,决策不进流**。
  **§二 敌人实体复用 9 的伤情模型**:25 已把「生命归零」登记为 `INJ_COMA`,非致命模型(归零 = 昏迷)
  自动适用敌人;**id 经 `IIdAuthority` 与病人共用同一空间**(⇒ ADR-006 Amendment B 的 `max(id)+1`
  高水位重构仍成立);**敌人伤情落世界流**(不污染病史流)。**ADR-006 Amendment B 的 id 空间语义由本 ADR 扩大**
  (病人 → 受伤实体),折叠谓词不适用于敌人行。
  **§三 感知输入 = 粗粒度整数格**:玩家跨格写世界流事件(**事件率上界 = tick 频率**,
  与帧率 / 位移距离无关;⚠️ 2026-09-16 口径订正 —— 原写「频率 = 格穿越率」是**等值**断言,
  不可证伪且与归并算子相抵,见系统 1 的 F-1-1b / `AC-1-03`),
  **禁读表现态位置**(第二 QoS 到达时序不确定 ⇒ 决策非确定性函数)。**§六 13 ↔ 37 结清**:
  `IPresentPatients` 只读在场视图,**37 读 13,13 不引用 37**(单向无环)—— 结清 `case-system.md:473`
  的「契约暂定」(与 ADR-008 B-2 拒绝预留不矛盾:B-2 时 13 无消费者,此处 37 已立案)。
  **§七 动物 AI 的 P1a 驯化接口本次定型**(`ITameable` + 复用行为程序的命令通道),
  兑现 `game-concept.md:647`「否则 P1a 要重写 27」。
  **§九 冻结 / LOD(2026-09-17 就地修订)**:冻结判据**只用 sim 量**(`d2` / `in_combat`,
  **删「离屏」**);原「冻结不改变判定」**已就地证伪**(`LogiPose` / `acc` / `path_cursor`
  是**积分量**,跳过 step 就是少积分)⇒ 改为「冻结态可由三源重放复现 ≠ 从不冻结态」+
  「冻结须同冻 `acc`」;「**单原型**」口径澄清 = **行为程序表达能力的上限**,非实体种类数;
  撤回「以便平移 DOTS」的理据(形状约束保留,理由改当下主义 —— 承 **ADR-017 §四**)。
  Engine Knowledge Risk **MEDIUM → 单一来源** —— sim 侧判据纯 C#(**LOW**);抬到 MEDIUM 的
  **仅剩**表现层一处(动态建造下的 NavMesh tile 更新成本);原第二处「编辑器期行为树工具的
  导出 / 烘焙可行性」**随 §四 修订消失**(工具砍掉)。

- [ADR-017 ✅ Accepted 2026-09-15]**是否引入 DOTS(sim 侧结构性排除 · 表现层留复评门)** ——
  `docs/architecture/adr-017-dots-decision.md`。**兑现 ADR-004**(本条此前为「待建」)。
  一项核心用户裁定(**分层,照准**):① **sim 侧永不上 DOTS** —— **结构性排除,非规模判断**:
  门 A(`"noEngineReferences": true`)与 `Unity.Entities` / `Unity.Burst` / `Unity.Jobs` /
  `Unity.Mathematics`(**全是引擎程序集**)天然冲突,上 DOTS 即须拆门 A;
  **此结论不随规模变化**;② **表现层留复评门** —— 触发输入 = **OQ-8** 标定完成
  (`disease-simulation.md`,「在场才模拟 vs 全域模拟」),触发判据 = 同场实体数 / 表现层
  帧占比 ≥ **[阈值待用户裁定]** → **✅ 2026-09-20 结案:范围 = 在场才模拟 · `CAP = 24`;阈值 = 同场表现实体 ≥ 100 或 表现层帧时间 ≥ 8 ms(≈48%)**
  —— ⚠️ 阈值计的是**表现层实体**,不是 sim 病人数;**门在 sim 侧永不触发**(24 < 100);作用面**仅表现层**(群集转向 / VFX / 渲染批处理 / Unity Physics 边界);
  **触发后须另开 ADR,不得以本 ADR 修订改判**;触发前表现层**同样不引入**。
  **§二 首次成文一处此前全文无记载的接缝**:门 A ↔ `Unity.Entities`;
  硬化要求 = sim 程序集引用集白名单断言(恰 = BCL),把约定升为构建失败。
  **§四 撤回 ADR-016 §九「以便平移 DOTS」的理据**(形状保留,理由改为缓存局部性 / 可序列化 /
  可测性 / 无 GC 抖动 —— 从未来主义改为当下主义,**加强**该约束)。
  **驳回 Alt 3「sim 上 DOTS」= 结构性排除**(且 Burst 定点中间乘会与 ADR-012 F7 spike 叠加);
  **驳回 Alt 5「只引 Jobs / Burst」** —— 门 A 不看规模只看引用集。
  Engine Knowledge Risk **LOW**(不依赖任何 post-cutoff API;复评门触发后才需实测 post-cutoff DOTS)。

- [ADR-018 ✅ Accepted 2026-09-15]**音频架构(44 的呈现层边界 · 混音拓扑 · 无提示音机械化)** ——
  `docs/architecture/adr-018-audio-architecture.md`。**44 是 P0 最名不副实的一项**
  (`systems-index.md:513` 自认):Foundation 零依赖、全案两行描述,P0 却要它承担状态反馈;
  `diagnosis-system.md` **V-8.7** 称「44 是本节最重的技术债」,并登记 **8 对 44 的四条硬需求**
  (「必须在 44 写 GDD 之前定」)。一项核心用户裁定(照准):
  **44 与 42 同构 —— 只触发 / 只渲染,永不持有游戏状态**(承 ADR-013 §9 C3)。
  裁决:**`AudioCueDto`**(呈现层 DTO,整数语义,**无 `disease_id`**,受 `PresentationDtoGuard`
  **递归**扫描覆盖 —— AC-37-15 一致性)· **音频事件表走 ADR-014 烘焙管线**(玩家构建零 JSON 解析器)·
  **混音拓扑单一定义**(Master / Music / Ambience / Voice / SFX / Stethoscope / UICue)+ 快照清单,
  **明写快照不得用于状态播报**;**无提示音铁律机械化** = **白名单断言**
  (音频触发源 ∈ 行为反馈白名单;禁 sting / jingle / ducking / 素材切换 **报**状态 —— **AC-44-09 BLOCKING**)·
  **8 的四条硬需求逐条落 AC**(AC-44-01…07:呼吸两层「通带 + 噪声底」差异**禁静音路径** ·
  语声**变体库混合**(非参数调制)· 接触噪声 / 信噪比参数 · 联机取主机技能)·
  **AC-44-08 医学准确性**(细湿啰音**非连续水声** —— 医学受众第一个出戏点)·
  **§五 联机**:单 `AudioListener` 每设备一条混音总线 ⇒ 精度统一取主机技能
  (脉案仍各自分叉,**分叉的只有音**);远端空间化复用 ADR-001 第二 QoS 位置,**不新增通道**,
  **不做语音通道**· **§七 VR 音频推 P1a**(与 ADR-013 同批)。
  Engine Knowledge Risk **MEDIUM**(`modules/audio.md:4` 自陈「6.3 mixer improvements」Knowledge Gap,
  是本 ADR 唯一 post-cutoff 悬置;基础 AudioMixer API 长期稳定)。

- [ADR-019 ✅ Accepted 2026-09-15]**遥测与隐私(51 本地优先 · 回放即数据记录 · 零第三方上报)** ——
  `docs/architecture/adr-019-telemetry-and-privacy.md`。**51 是职责与规格严重失衡的一项**:
  `systems-index.md:255` 称它是「回答**判断层到底好不好玩**」的仪器(绑定**支柱一**),
  却零 GDD / 零 ADR / 零 TR、估 S。一项核心用户裁定(照准):**51 本地优先**。
  裁决:**§一 回放即完整数据记录(核心)** —— ADR-005 的确定性模拟**本身就是完整记录仪**
  (逐位可复现;`case-system.md:162` 保证可回放);**「埋点」在本项目是冗余** ——
  想测的每个量真源**已存在**于事件流;51 是**读数器**不是**采集器**;**AC-19-01(BLOCKING)**
  = 判断层指标均可从既有事件流重算、**零新埋点**;**§二 零第三方 SDK · 零出厂数据**
  (`docs/engine-reference/unity/` 对 Unity Analytics / UGS **零覆盖** ⇒ 无权威件可依,不作承诺;
  **AC-19-02 BLOCKING** = 构建 + 运行期无任何网络上报;接口层无 `Upload` / `Send` / `Post`);**§三 51 住边界层** ——
  **不进 sim 程序集**(不污染门 A)、**不写三流**(只读消费者,不成为第四个 `IEventSink` 写入者);
  **§四 隐私面因「无出厂」而结构性满足**(无账号 / 无同意机制 / 无导出删除 / **无退出开关**
  —— 因为没有任何东西可退出;**并写明硬边界**:一旦引入出厂数据,本节即失效,须**另开 ADR**,
  **不得打补丁**);**§五 P0 交付最小切面**(指标重算器 + 开发者调试视图 + 本地导出;
  **无玩家可见统计 UI**);**§六 51 不得成为「替用户调数值」的替身**(数值用户自己调;
  **AC-19-07** = 无写回 `assets/data/` 的代码路径);**§七 结清 `TR-randomevents-024`** ——
  52 ↔ 51 边界 = **三流本身**,零直接接口(`gap` → `covered`)。
  驳回 Alt 2(第三方 SDK:违背零第三方取向 + 无权威件 + 本机无法联网核查条款 + 规模上无收益)·
  Alt 3(自建端点:为不存在的需求建基础设施)· Alt 4(埋点进 sim:门 A 污染 + 遥测污染真源)。
  Engine Knowledge Risk **LOW**(实现面 = 读已有之物 + 写本地文件;**不引入任何引擎 / 第三方 API**)。

- [ADR-020 ✅ Accepted 2026-09-15]**玩家控制器与相机(CharacterController · 自建机位)** ——
  `docs/architecture/adr-020-player-controller-and-camera.md`。**架构复核 R-13 落点 = 末项**;
  落盘后 **R-1…R-15 全部结清**。三条用户裁定(均照准):
  ① **移动 = `CharacterController`**(kinematic,不参与 PhysX 求解;AC-20-01);
  ② **相机 = 自建机位**(不引入 Cinemachine,消解 post-cutoff 知识风险;AC-20-02);
  ③ **视角双路径** —— 平面 = 第三人称越肩(承 `game-concept.md:32`「全程第三人称」),VR = 独立第一人称
  (头显物理事实;VR 不承担开放世界移动,承 `technical-preferences.md:34`),实现推 P1a。
  **§四 玩家位移确定性边界(核心)**:玩家位移 = **纯表现态**;在 sim 中的唯一投影 =
  **跨格世界流事件**(连续位置 / 速度 / 朝向永不写流;**AC-20-03 BLOCKING**,判据 = **反射断言**
  载荷字段类型,**不是 grep** —— F-1-6 跨格检测必然读 `Vector3`)。
  **这不是新裁决,是 ADR-016 §三 在玩家侧的对称落实** —— ADR-016 §三 定**读方**(13 / 27 读粗粒度整数格,
  `adr-016:181-190`),本 ADR §四 定**写方**(玩家跨格发事件);**事件率上界 = tick 频率**,**与帧率无关**
  ⇒ 不破坏 ADR-009 世界流有界性论证。**Append 权 = 主机唯一**(2026-09-16 **Amendment B**:
  客户端经 ADR-001 第二 QoS 上行其格,`ActorCellEntered` **不在可靠通道**)。
  **§五 相机只读不持状态**(承 ADR-013 §9 C3 的 42 / ADR-018 §一 的 44,呈现层三件套同构)。
  **§六 镜头效果归属 = 8 语义 + 2 实现**(承 `diagnosis-system.md:1641`),**VR 全禁镜头效果**
  (承 `:1519` 舒适度);**不得用于报状态** —— 与 ADR-018 §六 无提示音铁律**同源**(同一铁律的视觉侧)。
  **§七 结清 44 的 `AudioListener` 单挂点**(`adr-018:295` 留白):平面 = 主相机,VR = 头显(AC-20-10)。
  **零第三方第七处一致性**:Cinemachine = **不引入**(「官方包」不是豁免 —— DOTS 同为官方包,已于
  ADR-017 结构性排除)。
  Engine Knowledge Risk **LOW**(§Engine Compatibility 自陈 **不引入任何 post-cutoff API** ——
  Cinemachine 的知识盲区经「不使用它」消解,而 `CharacterController` 是长期稳定 API)。

- [ADR-021 ✅ Accepted 2026-09-15]**POI 状态所有权与世界流承载** ——
  `docs/architecture/adr-021-poi-state-ownership.md`。**结清三方复核(2026-09-15,
  `design/research/expansion-review-2026-09-15.md`)登记的洞 H2** ——
  「6 世界与生态区只是 POI **容器**,没人拥有『这个 POI 已清』;`已清` 是派生态(ADR-009 §四)
  ⇒ 不可推导,必须进世界流并指定写者」。一项核心用户裁定(照准):
  **POI 状态的写者 = 6 世界与生态区**(容器顺理成章成为所有者,**不新立系统** —— 与「不加 #54」取向一致)。
  裁决:**① POI 一分为二** —— **定义 = 派生态**(位置/守卫/类型,烘焙逻辑层 ADR-015 §一,
  加载期重建,不进流)/ **状态 = 模拟态**(进世界流);**② 所有者 + 唯一写者 = 6**(写经
  `IEventSink.Append`,主机唯一执行,承 ADR-005);**③ 新增世界流 Kind `PoiStateChanged`**,
  载荷 `{ poi_id, new_state }` 均整数枚举(**`PoiState` 具体值刻意归 6 的 GDD** ——
  同 ADR-009 §三「骨架先行,载荷归系统 ADR」纪律),`Patient = PatientId.None`(不污染高水位);
  **④ 有界性** ≤ `|POI| × |STATE|`(补 ADR-009 §六,扩展而非重写);**⑤ 52 的 `spawn_anchor`
  读 POI 定义(静态),不读状态**(否则抽池变成状态的函数,破坏确定性抽池前提)。
  **本 ADR 是首条以「系统 ADR 追加世界流 Kind」方式扩骨架的实例**(ADR-009 §三 预置的口子)。
  **涟漪**:ADR-009 §二/§三/§六 三处就地修订(**Amendment F**)· ADR-010 §三 **义务 11** ·
  `architecture.yaml`(世界流 interface / `IEventSink` 路由 / 新增 `poi_state` 所有权条目)·
  `entities.yaml`(`SimEvent.Kind.PoiStateChanged`)· `systems-index.md` §11(H2 闭合)·
  `tr-registry.yaml` + `traceability-index.md`(新 slug `world-eco`,TR +9 = 187 → 196)。
  **同批未结**:H1(模态解锁 —— 分治登记:19/21 物品门 + 8 动作词表 + 30 非物品能力)·
  H3(敌人可救治 —— 归 10)—— 用户裁二者 ADR **推迟 P1a**,本轮仅在 §11 登记所有权。
  Engine Knowledge Risk **LOW**(纯数据边界与所有权裁决,不触及任何引擎 API)。

- [ADR-022 ✅ Accepted 2026-09-16]**关卡工具(内容产出的正式系统 · 两层世界单一版权威)** ——
  `docs/architecture/adr-022-level-tool.md`。**由系统 6 世界与生态区的首轮 `/design-review`
  第 4 条根因(🔴「关卡工具无系统归属」)提出**;用户裁定(2026-09-16,照准):
  **另开 ADR 立新系统** —— **不**并入既有系统行。
  **病因**:6 的**全部输入**(生态区多边形 / 导航格 / POI 格 / 资源点 / 建造槽位 /
  `TERRAIN_TABLE` / `slopeLimit` / `stepOffset`)由一件「关卡工具」手工产出,而该工具
  **无系统行、无 ADR、无 GDD** —— `ADR-015:222` / `ADR-009:537` / `ADR-016:231` 三处**引用**它,
  **无一处定义**它(与 `CompoundTriggered` / `ActorCellEntered` 同型的「引用却无登记」失败模式)。
  后果**不是少写一个文档**:`ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的
  「两层 / 导航格一致性检查**在关卡工具中存在**」这句判据**在架构上悬空** ——
  **没有任何东西被指定为「关卡工具」**。
  裁决:**① 关卡工具 = 正式系统,归新立 Tooling 层**(编辑期 / 构建期,**运行期零存在**;
  程序集 `tools/level/`,`UnityEngine` / UnityEditor 引用自由 —— **门 A 不约束**,因其**不进构建**;
  **不计入 P0 的 31 项**);**② 唯一作者** —— 逻辑层全部整数数据只能由本工具导出,
  第三方(GAIA / MapMagic 2)**只进视觉层**、**绝不自动导出逻辑层**(承 ADR-015 §六);
  **③ 导出契约** —— `assets/data/world_{geometry,ecozones,poi,resources,buildslots,nav_{chunk},terrain}.json`
  → ADR-014 两阶段烘焙 → `world_*.cooked`(`data-core` 预载;`ConfigVersion` 由内容哈希派生);
  **④ 六项一致性检查 C1–C6**(CI 可跑)—— **C2 可走性同源 / C4 多边形合法性 / C5 边界单一源 /
  C6 切片完整性 = 硬失败**(显式 `throw`,非 `Debug.Assert`);
  **C1 两层漂移 / C3 导航格↔NavMesh = 告警**(漂移只影响表现,不退化为构建失败 —— 与
  `ADR-015 §Risks` 同口径);**⑤ 切片者** —— 导航格按 chunk 切片(`world_nav_{chunk}.json`),
  结清 `O-6-12` 的**工具侧**;**⑥ 与 6 的边界锁死** —— 工具产出**派生态输入**(ADR-021 的「定义」侧),
  6 拥有**运行期状态**(「状态」侧),二者不重叠。
  **结清**:`O-6-9`(立系统)· `O-6-11`(C5)· `O-6-12` 工具侧(C6)· `O-6-14` **产出方**(§三)·
  `OQ-6-8`(§五 —— 判「分块流式」,未驻留 chunk 视为全 `block`,`EcozoneOf` 不得假设全图可达)。
  **涟漪**:`systems-index.md`(新类别 **Tooling** + **行 54**)· `tr-registry.yaml`
  (`TR-leveltool-001…008`,全 `covered`;221 → **229** 条)· `traceability-index.md`(§12 + 汇总)·
  `architecture.yaml`(`level_tool_authoring` 契约)· `adr-015 §四` **补注**(导航格切片,
  承 `O-6-12` —— ADR-014 §五 的「首次 `Step` 前常驻」只约束小体量几何数据,不约束导航格)·
  6 的 GDD(`O-6-9` / `O-6-11` / `O-6-12` / `O-6-14` / `OQ-6-8` / `R-6-16` / §Cross-References 回填)。
  Engine Knowledge Risk **MEDIUM**(**工具实现面** Terrain / `com.unity.ai.navigation` 6.3 API 须实测;
  **裁决本身**为纯数据边界规格,**LOW** —— 且工具**不进构建**,其 API 面**不污染出货**)。

- [ADR-023 ✅ Accepted(附条件)2026-09-20]**渲染与场景加载策略(三场景拓扑 · 拆序六步 · 零
  gameplay 对象铁律)** —— `docs/architecture/adr-023-scene-lifecycle-rendering.md`。
  **Required ADR #1 的兑现件**(TD 条件 C2 的 #1 半边)。病因:19→22 份 ADR 对场景生命周期
  **零字**,`architecture.md` §3.4 的 [0]/[7] 两步自 v1.0 标 🔴。裁定:① **三场景制**
  (`Boot.unity` 常驻含相机 + `AudioListener` + tick driver / `MainMenu` / `World` additive,
  **永不用 `LoadSceneMode.Single`**);② `World.unity` **零 gameplay GameObject**(逻辑层是烘焙
  数据,ADR-015 —— 预摆 = 第二真源;构建期 `throw` 级扫描,Tooling 层);⑤ **拆序六步**(停
  `Step` → 落盘 flush → `ReleaseInstance` 全部 → `Release` handle → `UnloadSceneAsync` →
  **断言登记簿空**)= 根因是 `Addressables.UnloadSceneAsync` **不销毁** `InstantiateAsync`
  产物,不强制则**每次读档漏一个世界**;⑥ **运行期 chunk 激活权 = 系统 6**(只读 `ActorCellEntered`
  的格,不读表现态位置 ⇒ 激活是派生态不进流);⑦ 读档后连续位置 = 格锚点 + **确定性格内偏移**
  (BCL 整数哈希,不引入第二随机源);⑧ P0 **零 custom Renderer Feature** + 触发条款(ADR-013
  §6.6 假设 6 spike 失败时按新签名 `RecordRenderGraph` 另开 ADR 才可引入)。
  **附条件的定义(用户裁定口径)**:S1–S4 spike 回填是**实现故事的前置**,**不是裁决的效力条件**
  —— S3 被推翻则第 6 步断言零成本保留、不改判;S1/S4 不可接受则按 Alternatives 另开修订。
  **S1–S7 逐条未跑,§Validation 表内全部未勾(禁借绿)。** Engine Knowledge Risk **HIGH**
  (RenderGraph / Addressables 6.2+ 行为均 post-cutoff;本件刻意只引用已登记形状)。

- [ADR-024 ✅ Accepted 2026-09-20]**三流 `Kind` 单一登记真源 + 构建期校验体系** ——
  `docs/architecture/adr-024-kind-single-source.md`。**Required ADR #3 的兑现件**(TD 条件 C3)。
  病因(两轮集合作用**可复算**):`IEventSink.Append` 的路由是「Kind→StreamId 纯函数白名单,
  列表外构建期拒绝」,但**白名单从哪读**从未裁决,而三个候选登记处**都不完整** ——
  `entities.yaml` 24 支 / ADR-009 §三骨架 15 支(世界流专属,结构上装不下全集)/ ADR-007 §三
  5 支(零 registry 条目),并集 33;**按 registry 生成拒收 9,按骨架生成拒收 18,两个方向都会死**。
  六项裁定:① **真源 = `entities.yaml`**,必填字段扩三件 `stream:`(枚举,**禁从散文解析**)/
  `author:` / `payload_schema:`(类型只允许整数域);② **ADR-009 §三/§二 就地降级为路由注记**
  (骨架文本不删,加节首声明「不一致时以 registry 为准并触发 V-1 断言失败」);③ **Amendment
  追加通道退役**(F–L 用过的那条 —— 它正是幽灵引据的生产线);④ 补齐 **9 支**(世界流 `Craft`
  / `Drop*` 三支 + 病史流 ADR-007 五支,载荷逐字搬出处件)+ §二 **补 4 支具名** + 修 4 处陈旧
  「9-Kind」计数 + `ConsequenceResolved` **幽灵引据就地订正**(其自述经 Amendment 通道入 §三,
  而该件最后一条是 **L**、无 M,且 §三 不含它);⑤ **构建期生成器 `tools/kindgen/`**(编辑期
  .NET 工具,与 ADR-022 Tooling 层同构、不进构建)→ `src/Sim/StreamRouting.g.cs` + 断言
  **A1 唯一流别 / A2 载荷 ∈ 整数域 / A3 无重名 / A4 author 必填 / A5 双向差集归零**;
  ⑥ 拒绝表(自陈 17、实测 18 谓词)执行体归 **ADR-014 阶段 2**,本件只登记落点与差 1 的事实。
  **无引擎实测前置**(纯数据边界)。Engine Knowledge Risk **LOW**。

- [ADR-025 ✅ Accepted 2026-09-20]**契约程序集清单与命名(§2.0 七行骨架收口)** ——
  `docs/architecture/adr-025-contract-assembly-manifest.md`。**Required ADR #2 的兑现件**
  (TD 条件 C2 的 #2 半边)。病因:§2.0 七行里 6 行标「⚠️ 名未定」,**全案唯一有名字的 asmdef 是
  `Sim`**(ADR-017 §二 只硬化门 A 内侧),「边界程序集 / 门面程序集 / 独立契约程序集」是三个
  从未对齐的名字 —— 后果不是文档不齐,是**约束落不了地**(`Fix` 编码器守卫 D-21-18 需要可执行
  的程序集对像;「仅门面可调用 `ToFloat()`」在「门面 = Sim 自身」读法下**恒假**;
  `tests/unit/sim/sim_fixedpoint_test.cs` 已因此不被编译)。裁定:① **具名六装配清单**
  (`Sim` 引用集**恰 = {BCL, `Sim.Contracts`}** / `Sim.Contracts` **恰 = BCL** 含 `WorldPos` +
  六抽象点 + `Fix` + `ITeleportCommandSink` / `Sim.Codec`(BCL,`Fix` 编码器 `internal` +
  `InternalsVisibleTo("Sim.Contracts.Tests")`)/ `Gameplay.Presentation` / `Gameplay.UI`
  (= 焦点单栈门的**编译期**表达)/ `Editor.Tools` 族含 `tools/level/` + `tools/kindgen/`,
  `includePlatforms:["Editor"]` **不进构建**);② **QQ-03 = 甲案** —— `Fix` 保持 public,
  「仅门面可调用」改**构建期 `ToFloat()` 调用点白名单断言**(`Sim` 内调用 = 构建失败),
  乙案(`internal`+IVT)否决留档;③ **QQ-01 = ①′** —— 传送契约**拆两半**:整数命令半
  `ITeleportCommandSink.RequestTeleport(int actorId, WorldPos cell)` 进 `Sim.Contracts`,
  `Vector3` 连续半留 `Gameplay.Presentation`(全案唯一跨门调用点 = 29 结算 → 触发 1 传送);
  ④ **清单封闭性 = 本法**(未登记 asmdef = 构建失败,与 ADR-024 A1 同构);⑤ 测试装配落点
  (`Sim.Contracts.Tests` 解种子测试不编译);⑥ **QQ-02 不入本件**(逐字回填 ADR-005 的独立义务,
  防「清单 ADR 顺手裁流内契约」的 ADR-007 型越权)。**「门面程序集」「独立契约程序集」两称谓
  自此作废**(四处原文加注归回写轮,V-5)。**残留**:V-6 向 ADR-017 挂 `"references": []`
  歧义订正(该空集作断言文本不可能成立 —— `Sim` 必须见 `SimEvent`)。Engine Knowledge Risk
  **LOW**(asmdef 机制自 2019 稳定,不触任何 post-cutoff API)。

> **本日志状态**:全部 ADR(001–**025**)均有日志条目。**ADR-004 已于 2026-09-15 由 ADR-017 兑现结案**;
> ADR-008 / 009 / 010 / 011 的条目已于同日补录。**架构复核 R-1…R-15 全部结清(ADR-020 为末项)**。
> **ADR-021 由三方复核(奇遇扩张裁定)的洞 H2 提出,非架构复核 R 系列** —— R 系列无残留缺口;
> 洞 H1 / H3 的 ADR 由用户裁定**推迟 P1a**(本轮仅登记所有权,见 `systems-index.md` §11)。
> **ADR-022 由 #6 首轮 `/design-review` 的根因 4 提出**(关卡工具无系统归属),
> **非 R 系列、亦非洞系列** —— 它**不新增运行期系统**(立的是**编辑期 Tooling 层**),
> 故 P0 的 31 项**不变**。
> **ADR-023 / 024 / 025 是 `architecture.md` §Required ADRs 的 #1 / #3 / #2 兑现件**(2026-09-20
> 逐份裁定轮全转 Accepted;文件号按落盘时序,**#序与文件号非同号是刻意设计**)。
> **Required ADRs 剩余未兑现项 = 仅 #4(系统 30 定点算术,非开工阻塞)/ #5(13 的写路径归属,非开工阻塞)**
> —— TD 条件 **C1–C4 四条全部结案**,Pre-Production 开工门的技术侧无阻塞项。

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist(C# 评审 —— 由 primary 覆盖)
- **Shader Specialist**: unity-shader-specialist(Shader Graph、HLSL、URP 材质与后处理)
- **UI Specialist**: unity-ui-specialist(UI Toolkit UXML/USS、UGUI Canvas、运行时 UI)
- **Additional Specialists**: unity-dots-specialist(ECS、Jobs、Burst)、
  unity-addressables-specialist(资源加载、内存管理、内容目录)
- **Routing Notes**: 架构决策与通用 C# 代码评审走 primary。任何 ECS/Jobs/Burst 代码
  走 DOTS 专家。渲染与视觉特效走 shader 专家。所有界面实现走 UI 专家。
  资源管理走 Addressables 专家。
- **当前 agent 集未覆盖的领域(本项目需要,但无引擎专属专家)**:
  - **联机网络编程** → 用通用的 `network-programmer`
  - **VR/XR 交互与晕动症** → `ux-designer` + `technical-artist` + `unity-specialist` 协同

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| DOTS / ECS / Jobs / Burst code | unity-dots-specialist |
| Addressables / content catalog config | unity-addressables-specialist |
| VR / XR interaction code | unity-specialist(+ `ux-designer` 做舒适度评审) |
| Networking / replication code | network-programmer(引擎无关) |
| General architecture review | unity-specialist |
