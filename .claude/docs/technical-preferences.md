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
> 地形 Gaia Pro / MapMagic 2 · 联机 Netcode for GameObjects **或** Photon Fusion(**二选一**)
> · 叙事 Ink / Yarn Spinner · 建造 Voxel Play 或自研模块化网格 ·
> AI Behavior Tree + DOTS。**在 ADR 通过之前,这些都不算已批准依赖。**

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [ADR-001 待建]**联机方案二选一**:Netcode for GameObjects **vs** Photon Fusion。
  初稿 GDD 同时列了两套互斥方案。P0 的「架构预留」必须先知道答案。
  ⚠️ **ADR-005 追加的约束**:选型须支持**自定义可靠有序消息流**
  (两者均满足,故不改变裁决空间)。
- [ADR-002 待建]**开放世界地形方案**:第三方(Gaia Pro / MapMagic 2)vs 自研。
- [ADR-003 待建]**建造系统网格方案**:Voxel vs 模块化网格。
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
