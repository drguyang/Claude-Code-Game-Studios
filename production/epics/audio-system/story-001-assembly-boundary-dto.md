# Story 001: 装配边界与 DTO 护栏

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-26(实现 + 审查 + 关单)

## Context

**GDD**: `design/gdd/audio-system.md`
**Requirement**: TR-audio-001(44 与 42 同构:只触发/只渲染;输入 = `AudioCueDto` 整数语义)· TR-audio-009(cue 载荷不复制三源事实)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-018: 音频架构(主)· ADR-025: 契约程序集清单(引用,装配登记集)· ADR-017 §三(表现层 DOTS 禁引,黑名单同格)
**ADR Decision Summary**: 44 只触发/只渲染,永不持有游戏状态;唯一游戏状态入口 = `AudioCueDto`(整数语义,无 `disease_id`);契约住 `Sim.Contracts`(仅 BCL)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(ADR-018 AudioMixer 6.3 post-cutoff)
**Engine Notes**: 接口层为纯 C# 契约不受引擎版本影响;`PresentationDtoGuard` 反射扫描为 EditMode 门(Mono),若日后进 player 须 link.xml 保 DTO 树(OQ-44-5 同批 spike)。

**Control Manifest Rules (this layer)**:
- Required: 44 只触发/只渲染 · `AudioCueDto` 整数语义无 `disease_id` · 契约程序集字段用原语/契约类型
- Forbidden: 44 引用 `Sim`/`Sim.Codec` 类型(含 `IEventSink`)· 引擎类型进契约程序集(违 AC-44-B1)· 表现层引 DOTS 家族
- Guardrail: 分叉的只有音(联机各自渲染)—— 装配断言不得把合法的引擎引用误杀

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-B1**(BLOCKING):44 实现类型的 IL/metadata 扫描 —— ① 零 `TypeRef`/`MethodRef` 解析到 `Sim`/`Sim.Codec` 类型(含 `IEventSink`/`IEventAuthority`/`SimEvent`);② 所属装配引用集 ⊆ ADR-025 §① 登记集 ∪ 引擎模块白名单;③ 禁 DOTS 家族。执行面 = `AssemblyGates` 新 b5(用户裁定 2026-09-25 = 黑名单+IL 扫描路线)。
- [ ] **AC-44-B2**:`PresentationDtoGuard` **递归**扫描 `AudioCueDto` ⇒ 无 `disease_id` 字段(AC-37-15 一致性)。
- [ ] **AC-44-B3**:公开接口只有 `IAudioCueSink`(只入不出);唯一**游戏状态**入口 = `AudioCueDto`(渲染参数入口 = `SetTier`/mixer 暴露/`IPositionalChannel` 只读,不携带游戏状态)—— 入口类型白名单反射断言。
- [ ] **AC-44-B4**:44 不持有任何游戏状态(病人病情/病例/世界状态),只持「声音正在发生什么」—— 44 装配全类型字段 ∈ {音频类型, BCL, `Sim.Contracts` DTO 族} 反射断言。

---

## Implementation Notes

*Derived from ADR-018 Implementation Guidelines + ADR-025 §① + 二轮修订(2026-09-25)*:

- 44 实现住 `Gameplay.Presentation`(ADR-025 登记);**该装配现有 `Sim` 引用属 L4 合法用途** —— B1 的黑名单落在**类型级 IL 扫描**(44 命名空间下的类型),装配级断言 = 引用集 vs 登记集**差集**(新登记的漂移单列,不动既有工程债 `Presentation→Sim` 摘除,另账)。
- IL 扫描用 **Mono.Cecil 1.11.6**(`com.unity.nuget.mono-cecil` 已钉 manifest 直接依赖)读编译产物 TypeRef/MemberRef(承 `AssemblyGates` b4「不引 Roslyn」口径)。**⚠️ 原写 `System.Reflection.Metadata` 已废**(2026-09-26 实现期核销:该程序集在本工程 unity-4.8-api 下**编译不过**;Cecil 判据等价 —— 引用类型解析回定义装配+命名空间+类型名);**双层** = Cecil 元数据层 + b4 同格源文本层(`using`/`nameof`/反射字符串只有源文本层能抓)。
- `AudioCueDto` / `IAudioCueSink` 已在 `Sim.Contracts`(ADR-025 §① 成员列);字段 = `WorldPos`/`PatientId`/原语。
- B3 的三分口径:状态入口 vs 渲染参数入口分开断言(2026-09-25 二轮 Q2 收窄,原「唯一入口」恒假已废)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 事件表 schema 与白名单门(内容/时机断言的载体)
- Story 007: `IPositionalChannel` 的读取纪律与联机对拍(D1 的运行面)
- `Presentation→Sim` 装配引用摘除(独立工程债,装配轮)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-B1**: 装配边界扫描。
  - Given: 44 实现代码已入 `Gameplay.Presentation`,工程可编译。
  - When: 跑 `AssemblyGates` b5(引用集差集 + 44 类型级 IL 扫描 + DOTS 黑名单)。
  - Then: 三段全绿;负向夹具(2026-09-26 实现期形态修正):**合成文本**喂源文本谓词(`using DaYiJingCheng.Sim;` + `IEventSink` 参数 ⇒ 必红)+ **测试装配内44 前缀 fixture 类型**对真实编译产物跑同一 IL 谓词(真 IL 必红)—— **不在生产树注入**(会把工程编译搞红);前缀逃逸负例:含 `AudioCueDto` token 却未声明前缀的根命名空间文本 ⇒ 红。
  - Edge cases: 引擎模块(`UnityEngine.*`/Addressables/URP)不被误杀(IL 层装配级正例);`nameof`/反射字符串形态由**源文本层**抓住(2026-09-26 回刷:原写「被 IL 扫描抓住」不实 —— metadata 层看不见 ldstr,双层各管一边)。
- **AC-44-B2**: DTO 递归扫描。Given: `AudioCueDto` 实例或类型。When: `PresentationDtoGuard` 递归扫描。Then: 无 `disease_id`(含嵌套/私有字段);负向:注入含 `disease_id` 的影子 DTO ⇒ 红。
- **AC-44-B3**: 入口白名单。Given: 44 公开接口清单。When: 反射枚举公开方法/属性。Then: 仅 `IAudioCueSink` 成员;状态入口类型白名单 = {`AudioCueDto`}(`SetTier` 参数 `TierSource` 属渲染参数,显式放行清单内)。
- **AC-44-B4**: 字段类型白名单。Given: 44 全类型。When: 反射扫字段类型。Then: ∈ {音频类型, BCL, `Sim.Contracts` 族(含 `IAudioCueSink`/`IPositionalChannel`/`WorldPosLatest`,防字面「DTO 族」假红), 44 自身};负向(2026-09-26 澄清):加一个 `Sim.RecipeDataSet` 或 `Vector3` 类型字段 ⇒ 红(**原举例 `DiseaseState` 作废** —— 若该类型住 `Sim.Contracts`,B4 白名单放行、由 B1 契约禁名词面与 `PresentationDtoGuard` 诊断词面{diseas/diagnos/symptom}拦,层间分工不同)。**执行面 = 测试**(b5 编译钩子只跑 b2/b3;B3/B4 断言谓词住测试文件,2026-09-26 登记)。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 账本路径:`tests/integration/audio_system/assembly_boundary_test.cs`
- **真身(Unity 只编译 `unity/Assets/` 树)**:`unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs`
  —— 2026-09-26 已建,24 测;账本 README `tests/integration/audio_system/README.md` 已互链

**Status**: [x] Created 2026-09-26 —— 过滤 24/24 + 全量 EditMode 绿(详 Completion Notes)

---

## Dependencies

- Depends on: None(装配边界是 44 实现的第一道门)
- Unlocks: Story 002…013(全部实现面)

## Completion Notes
**Completed**: 2026-09-26
**Criteria**: 4/4 passing(AC-44-B1/B2/B3/B4 全覆盖,测试-判据映射见 /story-done 报告)
**Deviations**: IL 路线 `System.Reflection.Metadata` → **Mono.Cecil 1.11.6 双层**(元数据 + 源文本;SRM 在本工程 unity-4.8-api 下编译不过,实现期核销,故事文本已回刷;GDD AC-B1「IL/metadata 扫描」字面仍满足)· B3/B4 执行面 = 测试(编译钩子只跑 b2/b3,已登记)· 范围外登记:`adr-025` §① 族行补 Gates/Spike/Bake(§④ 冲突平账)+ `adr-017` DOTS 应用面窄注(审查 Required-3/5 强制)· `Presentation→Sim` 工程债未动(Out of Scope 守住)
**审查 REC 榜单(排期,未做)**:长方法拆分(`CheckAudioAssemblyIl` 88 行等 5 处)· 公有成员 doc 注释 · 深度护栏统一改红(IL 层 32 层静默 vs 守卫 throw)· ReaderParameters 共享工厂 · 基线 WARN 按装配×文件 · `nameof` 专测 · IL 引擎类型不误杀正例
**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs`(24 测)+ 账本 `tests/integration/audio_system/README.md`;**过滤 24/24 · 全量 EditMode 568/568 · 0 failed**(含云端并行测试)
**Code Review**: Complete —— `/code-review` 判 CHANGES REQUIRED(1 BLOCKING 前缀逃逸 + 6 REC)→ **7 条 Required 全修 → 24/24 + 568/568 复验通过**;过程含两个实修 bug:门/测试 `ScriptAssemblies` 路径少 `Library` 段、「假绿防护三处」全落 · `\b` 在非逐字串 = 退格符
**执行状态**: ✅ **VERIFIED 2026-09-26** —— b5 三段门 + 守卫首建 + 24 测入库;**域重载降噪**(只跑 b3/b2,`b5-W` 刷屏消失);manifest 钉 `mono-cecil 1.11.6`(lock 刷为直接依赖);依赖项 None,unlocks = 故事 002–013 全部实现面
