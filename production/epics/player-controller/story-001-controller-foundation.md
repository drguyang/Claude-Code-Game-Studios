# Story 001: 控制器地基 —— `CharacterController` 唯一位移写入点 + 程序集边界白名单

> **Epic**: 玩家控制器与移动
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/player-controller-and-movement.md`
**Requirement**: TR-player-001(移动模型 = `CharacterController`,不参与 PhysX 求解;判据三条并列 `AC-1-01`①②③)· TR-player-004(手感旋钮的形状与归属 —— 本故事只钉「哪些常量存在、住哪个程序集」,取值一律留白)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
⚠️ 本故事同时立 **AC-1-28**(程序集引用集白名单)与 **AC-1-10②**(几何约束 `LATTICE_SIZE ≥ radius×2`)两条 BLOCKING,它们是后续五个 story 的地基判据。

**ADR Governing Implementation**: ADR-020(主:§一 移动模型 + §四 位移确定性边界)· ADR-025(契约程序集清单 = `AC-1-28` 的可执行对像)· ADR-015(§三 单一整数格 + `LATTICE_SIZE` 单一装载常量)· ADR-017(§二 门 A 白名单先例,本故事同法硬化 1 的引用集)
**ADR Decision Summary**: ADR-020 §一:**移动 = `CharacterController`(kinematic,不参与 PhysX 求解)**;AC-20-01 判据三条并列 —— ① 无 `Rigidbody`/`AddForce`;② `CharacterController.Move` 是**唯一位移写入点**;③ `Physics.Raycast`/`CheckCapsule`/`Overlap*` 引用数为 0(可走性只由 collide-and-slide 给出,R3)。ADR-025 §①:`Sim.Contracts` 引用集**恰 = BCL**;`Sim` 引用集**恰 = {BCL, `Sim.Contracts`}** —— 1 的移动程序集(本仓 asmdef 名待 001 定,登记见 Completion Notes)按同法白名单硬化。ADR-015 §三:`LATTICE_SIZE` 是**单一装载常量,两侧同源,禁二次定义**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(ADR-020 本体)
**Engine Notes**: `CharacterController` 是长期稳定 API(不依赖 post-cutoff)。`AC-1-01`③ 的例外注(R12/F-1-9):`isGrounded`/`slopeLimit` 是引擎**内部**碰撞结果,**不是** `Physics.*` API 调用,不受③约束 —— 若 spike(`OQ-1-12`)裁定需要显式地面探测,③须**按裁决重写**,不得静默放行。`AC-1-10③`(`Vector3Int` 不出现在跨系统接口签名)判据 = AST/反射,`Vector3Int` 只允许作 F-1-6 的**投影中间量**(局部)。

**Control Manifest Rules (this layer)**:
- Required: 移动 = `CharacterController`(kinematic,不参与 PhysX 求解)—— ADR-020 §一(manifest Presentation · 控制器与相机)
- Required: 空间位置一律整数格;**格尺寸须是单一装载常量(两侧同源,禁二次定义)** — ADR-015 §三:184(manifest Core · 世界几何与格)
- Forbidden: **把视觉层地形采样喂进 sim**(如 `Terrain.SampleHeight` 定可走性)— ADR-015 §一:123-124(manifest Core · Forbidden 表;1 的可走性一律由烘焙逻辑层整数查表给出)
- Forbidden: **chunk 局部坐标系** — ADR-015 §四:197(同一点在不同 chunk 有不同坐标 ⇒ 跨块判定必错)
- Guardrail: 引用集白名单以**构建失败**守住(与 ADR-017 §二 门 A 同法 —— 约定升为构建失败),不靠约定;`AC-1-01` 断言面**含玩家 prefab / 场景资产的组件清单**,不止 `.cs` 引用集(grep 抓不到 Inspector 挂的组件)

---

## Acceptance Criteria

*From GDD `design/gdd/player-controller-and-movement.md`, scoped to this story:*

- [ ] **AC-1-01(BLOCKING)** —— **移动不由物理驱动**:
  ① **`Rigidbody` 组件零挂载** —— 断言面 = **场景 / prefab 资产 + 代码**,不只是 `.cs` grep(球员是场景资产,prefab 里挂组件 grep 抓不到);
  ② **`AddForce` / `AddTorque` / `velocity` 写入零引用**;
  ③ **`Physics.Raycast` / `CheckCapsule` / `Overlap*` 零引用** —— R3「禁事先探测」的唯一机械守门,"用 Raycast 做地面探测"恰是最像合法的越界写法(EC-11 判 `groundNormal` 为"技术上允许但 P0 不做")⇒ 须显式拒绝。
  ⚠️ **例外(R12 / F-1-9)**:`CharacterController` 的 `isGrounded` / `slopeLimit` 是引擎**内部**碰撞结果,**不是** `Physics.*` API 调用 —— 不受本条③约束。若 spike(`OQ-1-12`)裁定需要显式地面探测,本条③须按裁决重写,**不得静默放行**。
- [ ] **AC-1-28(BLOCKING)** —— **1 的 asmdef 引用集白名单**:1 的移动程序集**只**引用**边界程序集**(ADR-005 Amendment F)+ BCL。**不**引用 sim **实现**程序集、不引用 `Unity.Entities` / `Unity.Burst` / `Unity.Jobs` / `Unity.Mathematics`。(ADR-020 的 AC-20-05 只管相机;1 自己此前无人管。与 ADR-017 §二 同法 —— 约定升为构建失败。)
- [ ] **AC-1-10(BLOCKING)** —— **坐标契约三项 + 几何约束**:
  ① 装载期断言 `烘焙层原点 == 运行期原点 ∧ 轴对应一一 ∧ LATTICE_SIZE 逐位一致`(**故意注入错位一格的原点 ⇒ 装载必须失败**);
  ② `LATTICE_SIZE ≥ CharacterController.radius × 2`(**EC-12**;越界 ⇒ 装载失败);
  ③ 断言 **`Vector3Int` 不出现在任何跨系统接口签名**里(ADR-015 §三 单坐标类型判据 —— 它只是**投影的中间量**)。

---

## Implementation Notes

*Derived from ADR-020 §一/§四 · ADR-025 §① · ADR-015 §三 · GDD F-1-9/R3/R4/R6:*

- **程序集落点先定**:1 的移动 asmdef(建议名 `Gameplay.Player`;若 ADR-025 六装配清单另有登记名,以清单为准,在 Completion Notes 记偏差)。引用集 = `Sim.Contracts` + BCL + `UnityEngine`(表现层组件),**不含** sim 实现程序集。AC-1-28 的判据 = asmdef `references` 白名单断言 + 传递闭包(承 story-006 input-system 的 `ReferenceClosure` 先例 —— 间接引用也算违规)。
- **`CharacterController` 组件 + 玩家 prefab 落地**:prefab 只挂 `CharacterController`,**零** `Rigidbody`;F-1-9 的参数契约表(本故事只钉**存在性与归属**,`AC-1-33` 的装载期比较值归 story 003):`minMoveDistance`(P0 = 0)/ `slopeLimit`(归 ADR-015 §一 + 1)/ `stepOffset`(归关卡几何/ADR-015,**不归 1**)/ `skinWidth`/`radius`/`height`/`center`/`enableOverlapRecovery` 一并登记归属。**值留白**(AC-20-11)。
- **`LATTICE_SIZE` 单一源**:定义落**边界程序集**(`Sim.Contracts` 或 1 读取的烘焙常量),1 **不自定义**第二份(ADR-015 §三)。AC-1-10① 的装载期断言 = 读烘焙逻辑层的格常量与 1 运行期读的格常量逐位比较。
- **AC-1-01③ 的 `Physics.*` 零引用判据 = Roslyn 分析器**(编译期,非事后 grep —— 承 input-system story-001 的 Legacy 分析器先例:符号被裁剪/内联后构建产物 grep 可能假阴性)。分析器拒 `Physics.Raycast`/`CheckCapsule`/`Overlap*`/`AddForce`/`AddTorque` 族;`CharacterController.Move` 为**唯一允许的位移写入点**(其余 `transform.position` 直接写也须拒,除传送/重生注入的显式「被放置」路径 —— 该路径见 story 004 的 EC-4)。
- **载体注记**:`tests/` 与 `.github/workflows/` 由 ADR-012 轮建;本故事交付判据本体(分析器 + EditMode 断言 + 装载期校验),不自建 CI。AC「已定义」≠「已验证」,见 EPIC.md Key Cross-References。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:F-1-9 的 `AC-1-33` 装载期值比较(`slopeLimit` 等于 ADR-015 几何取值)—— 本故事只钉参数**存在与归属**,值比较归手感链
- Story 002:`‖MoveInput‖ ≤ 1` 的 3→1 边界断言(`AC-1-09`)—— 输入契约,非地基
- Story 004:`Physics.*` 例外若被 `OQ-1-12` spike 改判(显式地面探测)⇒ 本故事 AC-1-01③ 随其回写
- 跨格事件载荷的字段类型纯净(`AC-1-02`/`AC-1-34`)= Story 004(载荷类型递归反射,依赖 `ActorCellEntered` 结构,本故事尚无)
- `Append` 权归主机(`AC-1-30`)= Story 005

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1-01①**: `Rigidbody` 组件零挂载(断言面含 prefab)。
  - Given: 玩家 prefab 与 World 场景已装配。
  - When: EditMode 遍历 prefab / 场景 GameObject 的组件集合(反射 / `GetComponents`)。
  - Then: 玩家根节点**无** `Rigidbody`,**有** `CharacterController`;组件集合断言对「prefab 里挂了 Rigidbody」失败(反例夹具:临时挂 `Rigidbody` ⇒ 红)。
  - Edge cases: 子节点(相机锚点、胶囊视觉)也不得挂驱动位移的 `Rigidbody`;`isKinematic = true` 的 `Rigidbody` **仍算违规**(AC-20-01 是「无 Rigidbody 驱动」,不区分 kinematic)。
  - Negative fixture: prefab 挂 `Rigidbody`(kinematic 与非 kinematic 各一)。

- **AC-1-01②③**: `AddForce`/`AddTorque`/`velocity` 写 + `Physics.Raycast`/`CheckCapsule`/`Overlap*` 零引用(Roslyn 分析器)。
  - Given: 测试用 C# 源串分别含 `rb.AddForce(...)` / `Physics.Raycast(...)` / `Physics.CheckCapsule(...)` / `Physics.OverlapSphere(...)` / `rb.velocity = ...`。
  - When: 以分析器跑该源编译。
  - Then: 每种均**编译失败**,诊断指向该引用;合法写法 `controller.Move(delta)` 编译通过。
  - Edge cases: 全限定 `UnityEngine.Physics.Raycast` 与 `using UnityEngine; Physics.Raycast` 两种写法均拒;方法组转换(`Func<...> f = Physics.Raycast;`)与 `typeof(UnityEngine.Physics)` 两种死角引用形态须覆盖(承 input-system story-001 F11 先例)。
  - Negative fixture: 一条只 `using UnityEngine` 但不触 `Physics` 成员 / `AddForce` 的源 ⇒ 不得误报。

- **AC-1-28**: asmdef 引用集白名单(含传递闭包)。
  - Given: 1 的移动程序集 asmdef 已建立。
  - When: 读 `references`(或 GUID 引用)集合,并沿工程引用图求传递闭包。
  - Then: 闭包 ⊆ {BCL, `Sim.Contracts`(边界程序集), `UnityEngine`/`UnityEditor` 表现层允许项};出现 `Unity.Entities`/`Unity.Burst`/`Unity.Jobs`/`Unity.Mathematics` 或 sim 实现程序集 ⇒ 构建/断言失败。
  - Edge cases: 间接引用(A 引用合法程序集、后者再引用 `Unity.Entities`)⇒ 闭包断言须捕获(承 input-system story-006 G3 先例);asmdef 缺 references 时的默认「全部」须被显式白名单关闭。
  - Negative fixture: 向引用集注入一条 `Unity.Entities` 引用 ⇒ 红。

- **AC-1-10①**: 坐标契约三项装载期断言。
  - Given: 烘焙逻辑层的原点/轴/`LATTICE_SIZE` 与 1 运行期读的同一常量。
  - When: 装载期校验。
  - Then: 三项逐位一致;正常装载绿。
  - Edge cases: **故意注入错位一格的原点 ⇒ 装载必须失败**(AC 明文);轴对应非一一(如 x/z 交换)⇒ 失败;`LATTICE_SIZE` 两处不同值 ⇒ 失败。
  - Negative fixture: 三项各一个违例夹具(原点偏一格 / 轴交换 / 格尺寸 1↔2)。

- **AC-1-10②**: `LATTICE_SIZE ≥ radius×2`(EC-12)。
  - Given: 玩家 prefab 的 `CharacterController.radius` 与 `LATTICE_SIZE`。
  - When: 装载期断言。
  - Then: `LATTICE_SIZE ≥ radius * 2` 成立。
  - Edge cases: `radius = LATTICE_SIZE/2` 恰边界 ⇒ 绿;`radius` 略大 ⇒ 装载失败(错误串点名两值)。
  - Negative fixture: 注入 `radius = LATTICE_SIZE` 的 prefab ⇒ 红。

- **AC-1-10③**: `Vector3Int` 不出现在跨系统接口签名。
  - Given: 1 与外部系统(2/6/45)之间的公开接口类型(如 `ICameraRig` 消费面、`IEventSink` 载荷)。
  - When: 反射扫描公开方法签名 / 字段类型。
  - Then: 无 `Vector3Int`;`Vector3Int` 仅允许作为 F-1-6 内部**局部**中间量。
  - Edge cases: 私有方法内的 `Vector3Int` 局部变量不违规(判据 = 接口签名,非「代码里出现该类型」)。
  - Negative fixture: 把一个接口返回类型改成 `Vector3Int` ⇒ 红。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/player_controller/controller_foundation_test.cs` — must exist and pass(含 Editor-only 的 prefab 组件断言、Roslyn 分析器测试、装载期契约断言)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/PlayerController/`,登记口径 = `tests/integration/player_controller/`;Unity 只编译 `unity/Assets/` 树,承 input-system 先例)

---

## Dependencies

- Depends on: None(1 的程序集与 prefab 是移动层第一块砖;引用 `Sim.Contracts` 已由 ADR-025 六装配清单落定)
- Unlocks: Story 002(读 `MoveInput` + `YawBasis`)/ 003(F-1-9 值比较)/ 004(`ActorCellEntered` 载荷)/ 005(Append 权)/ 006(状态纯净反射断言依赖本故事立住的程序集边界)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
