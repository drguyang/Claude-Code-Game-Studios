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
> 自研模块化网格(Voxel Play 不采纳)** · AI Behavior Tree + DOTS。**在 ADR 通过之前,这些都不算已批准依赖。**

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
- [ADR-004 待建]**是否需要 DOTS**:大型 NPC / 瘟疫模拟的规模未定 —— 先不用,
  等规模数据出来再决策。**注**:9 已裁决「不上 DOTS,只做零成本预防」
  (ADR-005),但那不等于全案不用 DOTS,本项仍待决。
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

> **本日志残留缺口**:ADR-008 / 009 / 010 / 011 的日志条目此前未补录(仅 001 / 002 / 003 / 005 /
> 006 / 007 / 012 / 013 / 014 / 015 有),待下一轮统一补。

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
