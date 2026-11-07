# Story 002: 只读投影 Proj 与装配护栏

> **Epic**: 教学与引导
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-07

## Context

**GDD**: `design/gdd/tutorial-and-onboarding.md`(规则四 零持久化 · 规则四之二 Proj 提供者形态 · AC-48-03 / AC-48-04 / AC-48-11)
**Requirement**: TR-tutorial-001(教学进度零落盘,covered) · TR-tutorial-003(Proj 落哪个 asmdef —— **partial**:ADR-025 未点名,本 story 按清单就近落,不得记绿)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 世界状态事件化边界(派生态判据)· ADR-019 §三: 51 同构先例(边界层只读消费者,不写流)· ADR-025: 契约程序集清单 · ADR-010: 持久化(48 在存档布局中**无槽位**)
**ADR Decision Summary**: 教学进度 = 事件流的**读时投影**(派生态,Q1 判据),零持久化、零独立状态。Proj 提供者住边界层(与 51 的 AC-51-A2 同构):**只读消费者** —— 48 不是第四个 `IEventSink` 写入者,不持 `IEventAuthority`。装配护栏双守:① 48 交付物的公开面**不得引用** `SimEvent` / `PatientState` / `Fix`(递归反射);② asmdef 引用白名单禁 `IEventSink` / `IEventAuthority`(编译期,非 grep,AC-48-11)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 投影 + 反射断言;零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 投影 = 纯函数(同流前缀同输出);每次读取现算,类无可变字段(进度住在闩,见 Story 003)
- Forbidden: 任何写路径(`Append` / `Publish` / 存档段);48 侧缓存「已教到第几步」进可序列化类型
- Guardrail: 投影输入 = 三流只读快照(边界层给);48 不自行遍历流存储(消费面 = 只读接口,与 39 的投影纪律同形)

---

## Acceptance Criteria

*From GDD `design/gdd/tutorial-and-onboarding.md`, scoped to this story:*

- [x] `Proj(streamSnapshot, player_id) → 教学谓词输入` 为纯函数;六步谓词各自的判据事件(CaseOpened / JudgmentRecorded(author=p) / EmergencyAttempt·DrugTreatmentApplied(initiator=p) / CaseClosed / Checkpoint written / StructurePlaced)可从投影读出
  - 载体:`tutorial_projection_test.test_ac5_eachPredicateEvent_completesOnlyItsOwnStep`(六条 iff)+ `test_ac1_sameInput_evaluatesIdenticallyTwice` / `…prefixIsMonotoneSubsetOfFull`
  - ⚠️ F-48.1 ③ 的「或」由代码侧**析取组**补(内容表 `predicateKind` 是单值)—— 载体 `test_ac5_emergencyAttempt_alsoCompletesTreatStep`
- [x] **AC-48-03**:教学进度零落盘 —— 48 无任何存档写入路径;存档字节流不含 tutorial 进度字段(与无 48 的基线存档逐字节一致);区分 ADR-014 内容加载(读 `.cooked` 合法,非进度落盘)
  - 载体:`test_ac4803_saveBytes_areUnchangedByProjection`(**字面面**:真 `EventStream` + 真 `SaveComposer.Compose`,投影前后两份存档体逐字节相同)+ `test_ac4803_projection_doesNotMutateTheStream` / `…streamCountUnchanged` + 48 装配引用集**不含** `Sim` / `Sim.Codec`(无写盘可达面)
- [x] **AC-48-04**:48 程序集公开面的递归反射扫描零命中禁引类型(`SimEvent` / `PatientState` / `Fix` 及其嵌套泛型参数)
  - 载体:`tutorial_assembly_guard_test.test_ac48_04_exportedSurface_hasNoSimTypes`;**负向夹具证明门会红**(嵌套泛型 `Fix` / 构造参数 `SimEvent`);正控证明门不恒红
  - ⚠️ `PatientState` **今日全库不存在**(只有表现层 `PresentPatientState`)⇒ 该支是**零输入可达的规则**,如实登记,不当作已兑现的守卫
- [x] **AC-48-11**:48 asmdef `references` 白名单不含 `IEventSink` / `IEventAuthority` 所在程序集(编译期失败,非 grep)
  - 载体:编译产物引用集断言 + asmdef 逐字相等。**编译期性**来自「无引用 ⇒ 写不出该类型名」;⚠️ 「真加引用就编不过」的**构建级负例未做**(见证据件 §7-2)
  - 🔁 **2026-11-07 收口轮**:上句的缺口已补,且**原假设被证伪** —— 「真加引用」**编得过**(0 错)。
    现载体三件:① EditMode 夹具(原两条)② **构建期门 `b2-exact`**(`AssemblyGates.ExactRefWhitelist`,
    `RunAll` 三处强制点)③ **构建级三条观测**(D1 探针 / P2 真负例 / N 编译负例)。
    详见证据件 §8;`ExactRefWhitelist` 表本身仍无单元夹具(`§8.6-3` 如实登记)
- [x] 联机各玩家投影独立:同一 `Proj` 对两个 `player_id` 出两条独立结果(P0 单机夹具 = 两个发号 id 各投影;联机运行时 **BLOCKED-BY-45**,禁借绿)
  - 载体:`test_ac4_twoPlayersAuthoredEvents_stayIndependent` · `test_ac5_judgmentByOtherPlayer_doesNotCompleteMyStep` —— **只覆盖单机夹具半边**;联机运行面仍 `BLOCKED-BY-45`
- [x] 投影不可变性:同一输入两次求值字节一致;追加一条流事件 ⇒ 输出仅按单调性增长(不回收)
  - 载体:`test_ac1_sameInput_evaluatesIdenticallyTwice`(`Describe()` 规范串逐字相等)· `test_ac1_prefixIsMonotoneSubsetOfFull` · `test_ac1_defaultView_isFailClosed`

---

## Implementation Notes

*Derived from ADR-009 / ADR-025 Implementation Guidelines:*

1. 落点:`Gameplay.UI` 内 48 模块 + 边界层只读快照接口(投影不 new 事件、不解析载荷二进制 —— 拿到的已是解码后的只读视图)。
2. 「archive 步谓词 = Checkpoint 已写」**不是流事件** —— 投影输入需带 7a 的「最近 checkpoint 序号/锚点回执」只读口(规则八白名单例外的运行期侧;若 7a 无此口,登记接口需求给 7a 实现轮,本 story 以夹具时钟替代并留 TODO,**不私开第二条通道**)。
3. 护栏扫描器与 Story 001 校验器分开:本 story 的反射断言 = EditMode 测试(`TutorialAssemblyGuardTest`),照 `PresentationDtoGuard` 递归形制。
4. ⚠️ **数值冻结**:投影无旋钮(全机制);媒体时序类旋钮在 Story 004。
5. TR-tutorial-003 partial 的处理:在 asmdef 与 story 注释双向登记「落点为就近解释,待架构回写」—— 回写前不得把本 story 记成 TR closure。

---

## Out of Scope

- [Story 003]: 两相触发(Anchor/Completion)与闩的求值消费本投影
- [Story 005]: 零设门反射断言(48 API 面查询禁项 —— 那是「暴露面」守卫,本 story 是「引用面」守卫)
- 7a checkpoint 回执口的正式实现(归 7a epic;本 story 夹具)
- 联机运行时独立进度(45 轮)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 投影纯函数性
  - Given: 固定事件流夹具 A(含 6 类谓词事件各 ≥1)与截断前缀 A'
  - When: `Proj` 各跑两次 + 前缀对比
  - Then: 同输入同输出;`Proj(A')` ⊑ `Proj(A)`(单调,无回收)
  - Edge cases: 空流 ⇒ 全谓词 false;世界级事件(Patient=-1)不误入作者过滤

- **AC-2**: 零落盘(AC-48-03)
  - Given: 同种子跑两条会话:一条走教学全流程、一条跳过教学,存同一时刻档
  - When: 存档字节对比
  - Then: 无 tutorial 专属字段差(允许其他系统正常差异);48 写路径调用计数 = 0
  - Edge cases: 读 `.cooked` 内容发生在装载期,不算落盘(断言只扫写向)

- **AC-3**: 引用面双守(AC-48-04 / AC-48-11)
  - Given: 故意在测试分支加一个 `SimEvent` 参数的 public 方法
  - When: 跑反射守卫 + 尝试编译
  - Then: 守卫失败可复现(负夹具);asmdef 白名单违规 = 编译错误
  - Edge cases: 嵌套 `List<Fix>` 也被递归命中(泛型参数扫描)

- **AC-4**: 双玩家投影独立
  - Given: 4 人夹具中的 p1/p2 各落笔判断
  - When: `Proj(E, p1)` 与 `Proj(E, p2)`
  - Then: judge 谓词各自 true 互不可见;treat 谓词按 initiator 过滤
  - Edge cases: 联机真环境运行面 BLOCKED-BY-45(单机夹具不替代其 closure)

- **AC-5**: 六谓词判据事件映射(🔁 **2026-11-06 sprint-25 `/qa-plan` 轮补** —— 原 AC-1 只测纯函数性质,未逐条覆盖六步映射)
  - Given: 六类事件各一条(`CaseOpened` / `JudgmentRecorded(author=p)` /
    `EmergencyAttempt ∨ DrugTreatmentApplied(initiator=p)` / `CaseClosed` / `Checkpoint` 写回执 / `StructurePlaced`)
  - When: 分别投影
  - Then: **只有**对应那一步的谓词为 true,其余五条 false(逐条 iff)
  - Edge cases: 同一 `Kind` 由**不同作者**产生 ⇒ 按 `author_player_id` 过滤,不得把别人的动作算到自己头上;
    世界级事件(`Patient = -1`)不误入作者过滤

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_projection_test.cs` + `unity/Assets/Tests/EditMode/Tutorial/tutorial_assembly_guard_test.cs` — must exist and pass
**Status**: [x] Created and passing(31 条:投影 20 + 守卫 11,2026-11-07)

---

## Dependencies

- Depends on: Story 001(步定义 `.cooked` 就位,谓词→Kind 映射可查)· 边界层三流只读快照(既有基建)
- Unlocks: Story 003(触发引擎消费 Proj)· Story 004(媒体调度读投影状态)

---

## Completion Notes

**2026-11-06 实施轮(sprint-25 S25-1)—— 代码面完成,`AC-48-03` 的字面面 `NOT-RUN`**

- **落点裁定(TR-tutorial-003 就近解释)**:`Proj` 住**新装配 `Gameplay.UI.Tutorial`**,引用集**恰 = `{ Gameplay.Presentation }`**。依据 = `AC-48-11` 要求白名单不含 `Sim.Contracts`(声明 `IEventSink` / `IEventAuthority` 处),而 `Gameplay.UI` 引用了它。解码面(`TutorialEventFact` / `ITutorialProjectionFeed` / `TutorialSnapshotBuilder`)住 `Gameplay.Presentation/Tutorial/`。
  - `TR-tutorial-003` **status 维持 `partial`** —— 本件是**实现侧登记**,不是架构回写(承 Implementation Notes 5)。
- **判据**:新增 **30 条**(投影 20 + 守卫 10),全绿;证伪 M1–M5 各自转红,SHA-256 逐字节还原。
- **回归**:EditMode `6998 / 6961 / 0 / 0 / 37`(基线 `6968 / 6931` ⇒ +30/+30,零回归)。
- **同批补登记**:`ADR-025 §①` 增 `Gameplay.UI.Tutorial` 行 · `AssemblyGates.Manifest` · `control-manifest.md` 入口注 · `gate_wiring_manifest_test` 的 ClassVerdict + EntryWiring。
- **⚠️ 未做(禁借绿)**:`AC-48-11` 的**构建级负例**(真加引用验证编不过;已跑的是编译产物引用集 + asmdef 逐字)· 联机运行面(`BLOCKED-BY-45`)· `BuildGate` 探针轮 · 7a checkpoint 回执生产实现(今日缺省恒 false ⇒ `archive` 步生产上恒不成立)。
  - 🔁 `AC-48-03` 的「存档字节逐字节」已由用户 2026-11-06 指令**补跑**(`test_ac4803_saveBytes_areUnchangedByProjection`),原文不删。
- **证据**:`production/qa/evidence/impl-s25-1-tutorial-projection-2026-11-06.md`。
- **本轮被门抓到三次**(CS0104 同名 / 私有包装不入接线集 / 类名不以 `Gate` 结尾不被认作门)—— 详见证据件 §6,第三条已无其他文档承载,值得进 `coding-standards` 的命名避让节。

**2026-11-07 收口轮(sprint-25 S25-1)—— `AC-48-11` 的构建期半边补齐,本卡转 Complete**

- **「构建期」此前不存在**(实测):往 asmdef 加 `Sim.Contracts` **且真用上** `IEventSink`
  ⇒ 编译通过、`b2-decl` 双向差集为空 ⇒ **旧门全绿、构建成功**。当时 `AC-48-11` 的唯一执行体是
  EditMode 夹具,而夹具**不在构建链上**。
  ⚠️ **原登记的「真加引用就编不过」是错的** —— 语言的限制是「**无引用写不出类型名**」,
  不是「有引用编不过」;原文不删,口径以此为准。
- **新增构建期强制点**:`AssemblyGates.ExactRefWhitelist` + `CheckExactReferenceWhitelist`,
  接进 `RunAll()`(菜单 / reload / `BuildGate` 三处共用)。判据 = 源 asmdef 的 `references`
  与白名单**集合相等**,**两向都报**。
- **三条观测**(构建级,均可复跑):
  - **D1 探针轮**:在 `TutorialSurfaceGate.RunAll()` 插无条件失败项 ⇒ `Build Finished, Result: Failure`;
    探针串 `Logs/s25-1-d1-probe.log:1068`;栈帧 `BuildGate.OnPreprocessBuild`(`AssemblyGates.cs:1570`);
    错误数 = 1(= 只有本探针);SHA-256 `cd684c4f…` 逐字节还原。
  - **P2 真负例**:asmdef 加 `Sim.Contracts` + 真用 `IEventSink` ⇒ 编译 **0** 错 · `b2-decl` **0** 输出 ·
    `b2-exact` **1** 条(`Logs/s25-1-p2-negative.log:1216`)· 构建 `Failure`(`:1271`)。
  - **N 编译负例**:只删引用保留使用 ⇒ `error CS0234`(`Logs/s25-1-n-compile.log:759`),exit=1
    ⇒「无引用写不出类型名」由**观测**坐实(此前是语言级推断)。
- **干净构建**:还原后 `Build Finished, Result: Success`(`Logs/s25-1-close-build-final.log:8779`,
  起始 `2026-11-07T06:01:17Z`)。
- **回归**:见证据件 `impl-s25-1-tutorial-projection-2026-11-06.md` §9(截至时刻 · 批次 · commit 三元组)。
- **仍未做(禁借绿)**:联机运行面 `BLOCKED-BY-45` · 7a 的 checkpoint 回执口(零生产实现 ⇒ `archive` 步
  生产上恒不成立)。
  ~~`ExactRefWhitelist` 表无独立单元夹具~~ 🔁 **同轮已补**:
  `test_ac48_11_buildGateWhitelist_matchesTheAsmdefFace`(字面量期望 + 门表 ↔ asmdef 双向一致)。

**🔁 2026-11-07 晚(S25-5 轮)—— 本卡的一处夹具锚点被改,性质是「上轮成果被 AC-48-17 推翻」**

S25-5 落 `TutorialZeroGate`(公开面零门禁词)时首跑即红:`TutorialProjection.Project` **公开返回**
`TutorialProgressView` ⇒ 规则五(零设门)当时**不成立**。处置 = 把 `TutorialStepCompletion` /
`TutorialProgressView` / `TutorialProjection` / `TutorialTrigger` **内化**(`internal` +
`InternalsVisibleTo("Gameplay.Boot")` / `("Sim.Contracts.Tests")`)。

**连带**:`test_ac48_04_exportedSurface_hasNoSimTypes` 的锚点类型原为 `TutorialProjection`
(用来断言「扫到的是 48 装配且公开面非空」),内化后它不在公开面 ⇒ 锚点改为 `TutorialDispatcher`。
⚠️ **只改了公开面的锚点**:`AC-48-04` 的另一半(编译产物引用集不含 `Sim*.Contracts` + 递增到
`AC-48-11` 的 `b2-exact` 白名单)是**装配级**判据,与公开面无关,**零改动、仍全绿**。
细节见 `production/qa/evidence/impl-s25-5-zero-gate-guards-2026-11-07.md` §1。
