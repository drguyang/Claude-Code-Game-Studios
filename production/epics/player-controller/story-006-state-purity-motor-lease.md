# Story 006: 状态纯净与 `MotorSuppressed` per-source lease —— 零游戏状态反射断言 + 调用面白名单

> **Epic**: 玩家控制器与移动
> **Status**: Complete ✅ 2026-10-02 (实现 `5572d66`;判据修复后 **14/14** 测试通过)
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-02

## Context

**GDD**: `design/gdd/player-controller-and-movement.md`
**Requirement**: TR-player-001(移动模型的**安全边界半边** —— `AC-1-12` 禁止项零引用与 `AC-1-27` 状态纯净是 R3/R8/R11 三条禁令的机械守门)· TR-player-004(`MotorSuppressed` 通道在乘数链中的形状 —— `K_suppressed`,消费侧性质已由 story 003 签,本故事签**机制与调用面**)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事兑现系统 1 首轮 `/design-review` **根因 6 的前向登记**(`AC-1-29` 卡死清单)与 **2026-09-20 的 per-source lease 裁定**(`OQ-4-13`):原「单 bool」被证明与「4 / 10 / 25 互不知晓」(`combat-and-weapon-lines.md:507`)结构性互斥 —— 三个互不知晓的写者共享一个布尔 ⇒ **丢失更新**(4 的 Release 顺手清掉 10 仍持有的压制)。这是**既有 GDD 的内部矛盾**,裁定后形状 = **每源一位 + OR 聚合**。

**ADR Governing Implementation**: ADR-020(§一 移动模型 + §五 呈现层只读不持状态的对称纪律)· ADR-013(§9 C3「UI 只渲染,永不持有游戏状态」—— `AC-1-23` 的 UI 不得直接调用由此而来;`IModalState`/`ModalId` 闭集 = 4 侧抑制的合法路径,与 1 的位图无交集)· ADR-014(ordinal 纪律 —— `LeaseSource` append-only **禁重排**)· ADR-018(无提示音铁律同源的"不得用呈现面推状态"—— 44 不得读 `MotorSuppressed` 推"是否在动作中",`O-5` 反向登记仍悬空)
**ADR Decision Summary**: ADR-020 未直接管 1 的状态纯净(该 ADR 只管相机 AC-20-05 的引用面),`AC-1-27`/`AC-1-28` 是 GDD 侧的补位。lease 裁定的执行面全在 1:`Acquire(LeaseSource)` / `Release(LeaseSource)`,`LeaseSource` 闭集恰 = `{Self, Emergency, Combat}`(4 / 10 / 25),**29 死亡不占本位图**(沿用其「`MotorSuppressed` 或专用 `SetEnabled`」的既有待细化项,不随本次裁定扩张)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 全部判据 = 反射 / AST / 程序集引用集 + 位图单元语义,不触任何 post-cutoff API。`AC-1-29` 的软锁/卡死是 playtest 签核面(Visual/Feel ⇒ ADVISORY,不红构建);其中「地形移除后玩家会下落」依赖 `OQ-1-12` spike 对 `isGrounded` 陈旧性的裁定 —— 签核**可以**先做,但判据文案若被 spike 改判须随轴 2 表同步。

**Control Manifest Rules (this layer)**:
- Required: 可走性与地貌一律由**烘焙逻辑层整数查表**给出 — ADR-015 §一(manifest Core · 世界几何)
- Forbidden: **把视觉层地形采样喂进判定**(`Terrain.SampleHeight` / splat / 高度图)—— ADR-015 §一:123-124;Forbidden 表
- Forbidden: `NavMesh.SamplePosition` / `NavMesh.Raycast` 进 1 的移动路径 — ADR-016 §四(NavMesh 仅驱动 AI 表现态位移)+ ADR-015 §五
- Forbidden: **UI(42 / 48)直接置位 `MotorSuppressed`** — ADR-013 §9 C3(UI 若需冻结移动须经合法游戏系统,§Interactions 注)
- Guardrail: 反射扫描按**字段类型白名单**,不是字段名黑名单 —— "名字不含 hp/skill/inventory" 式判据是**假阴性机器**(GDD 明文)

---

## Acceptance Criteria

*From GDD `design/gdd/player-controller-and-movement.md`, scoped to this story:*

- [ ] **AC-1-27(BLOCKING)** —— **1 不持有游戏状态**(R11):1 的类型图中**不存在**血量 / 技能 /
  库存 / 任务字段或引用。**反射断言**(与 ADR-018 / ADR-013 的同构体例)。
  ⚠️ 与 `AC-1-28` 的**引用集白名单**互补:**本条管字段,那条管程序集**(那条归 story 001)。
  ⚠️ **反射扫描须按字段类型白名单**(而非"字段名不含 hp/skill/inventory")
  —— 后者是**假阴性机器**(任何命名的自造类型都能绕过)。
- [ ] **AC-1-23(BLOCKING)** —— **`MotorSuppressed` 单写者**:压制回调的**唯三**调用者是 4 / 10 / 25;
  **UI(42 / 48)不得直接调用**(ADR-013 §9 C3)。**程序集白名单 + 调用点断言**。
  ⚠️ **判据须区分"接口归属"与"调用点"**:`MotorSuppressed` 的**置位接口**可以住 1
  (1 是执行者),但**调用者白名单** = 4 / 10 / 25 —— 断言的是**调用点集合**,不是**接口定义位置**。
  否则"接口在 1 里"会被误读为"1 自己可以置位"。
  **负边界(2026-09-17 承 25 · R14)**:`MotorSuppressed` 的语义**止于水平位移 + Jump**,**不含攻击**
  —— 25 的判定式**不查** `MotorSuppressed`(被压制者仍可出手)。
  **1 不得扩语义**:被 `MotorSuppressed=true` 的玩家其攻击意图仍照常进 sim 求值。
  **✅ 2026-09-20 已裁(per-source lease)**:单 bool → **每源一位 + OR 聚合**;调用面
  `SetSuppressed(bool)` → **`Acquire(LeaseSource)` / `Release(LeaseSource)`**;`LeaseSource` 枚举归 **1**
  (闭集 = `Self` 4 · `Emergency` 10 · `Combat` 25;新调用者 = **append-only 加位,禁重排**;29 不占位图)。
  **纪律 ①–④**:① 每个来源**只碰自己那一位**;② **幂等** = 同 `LeaseSource` 重复 Acquire 不计数(位语义,非引用计数);
  ③ **无到期** = 压制期由持有者自己的规则说了算,1 不过问原因;④ **作用域不变** = 水平位移 + `Jump`,不含攻击。
  **⚠️ 作用域限定(承 4 规则八 冲突 B)**:「互不知晓」**只约束发压制,不约束读 `Armed`** ——
  4 读 10 的 `Armed` 是 `F-4.2` 的接受判据,不构成"知晓彼此存在"。
  **下游判据**:4 的 `AC-4-19`(4 与 10 同持、4 先 Release、压制仍生效)**随本故事落位图转可运行**。
- [ ] **AC-1-12(BLOCKING)** —— **R3 / R8 禁止项零引用**:1 的移动路径**零** `Terrain.SampleHeight` /
  splat / 高度图采样 / `NavMesh.SamplePosition` / `NavMesh.Raycast`
  (ADR-015 §一 判据:可走性与地貌一律由**烘焙逻辑层整数查表**给出)。
  **grep + 程序集白名单双判据**(`AC-1-28`)。
- [ ] **AC-1-29(ADVISORY · 复核新增 —— 根因 6「接地模型的遗留」/ 前向登记)** ——
  **可达性与"卡死"清单**(playtest 签核):
  ① **玩家可达性** —— 逻辑层 `slopeLimit` / `stepOffset` 的取值不得产生**软锁**(能走进去、走不出来);
  ② **地形移除后下落** —— 地面被移除 / 建造物被拆时玩家**会下落**(R12 的 `isGrounded` 陈旧问题的**症状判据**);
  ③ **跳跃代价** —— 跳跃不得成为规避地貌乘数 / 情境克己的手段;
  ④ **无水域系统** —— P0 无游泳,**浅水 = 地貌乘数的一种**,深水若存在则属 6 的几何边界
  (P0 须明确"进不去"或"减速",**不得**出现"走在水底")。
  ⇒ **属 Visual/Feel / 关卡体验 ⇒ ADVISORY 签核**;若发现软锁,**升级为 6 的阻塞项**。

---

## Implementation Notes

*Derived from 轴 4 裁定块 · R3/R8/R11 · §Interactions 压制表:*

- **位图形状**:`MotorSuppressed` = 内部 `int` 位图(或 `flags` 枚举),对外只暴露
  `Acquire(LeaseSource)` / `Release(LeaseSource)` / 只读 `IsSuppressed`(`OR(位图)`)。
  **不提供** `SetSuppressed(bool)` 公开面 —— 旧调用面须在重构中删除(保留 = `AC-1-23` 白名单旁路)。
- **`LeaseSource` ordinal 钉死**:`Self = 4` · `Emergency = 10` · `Combat = 25`(**enum 值 = 系统号**,
  既是位序也是审计标签);承 ADR-014 ordinal 纪律:**append-only、禁重排、已用值永不复用**。
  新增调用者走「改 GDD → 加位 → `AC-1-23` 白名单同步」三处一致,不得实现期私加。
- **纪律 ① 的机械半边**:`Release(src)` 只清 `src` 位 —— 单测逐位覆盖(4 家 Release 不影响 10 家位 = `AC-4-19` 的 1 侧镜像);
  **纪律 ②** 幂等:同 src 重复 Acquire ⇒ 位图不变(非计数,重复 Release 后仍为 0 无下溢);
  **纪律 ③** 无到期:1 内零计时器/零超时字段(反射断言并入 `AC-1-27` 的类型白名单面 —— 任何 `leaseExpiry` 式字段即违规)。
- **调用点判据形态**:接口定义住 1(合法),故判据 = **调用点集合扫描**(AST 全工程:`Acquire/Release` 的接收方所属程序集 ∈ {4, 10, 25 的移动可见程序集}),配程序集引用白名单(`AC-1-28` 对像扩展)双判据。UI 侧的合法替代路径写明:UI 需冻结移动 → 经游戏系统(如 4 的 `Interact` 触发压制),不直触 1(§Interactions 注原文)。
- **`AC-1-12` 的双判据分工**:grep/分析器抓**符号引用**(`Terrain.SampleHeight` 等 API 名);程序集白名单抓**间接通道**(若 1 引用了某个内部封装高度采样的第三方程序集,grep 抓不到)。⚠️ 与 story 001 的 `AC-1-01③` 区分:那条管 `Physics.*`(碰撞探测),本条管 `Terrain.*` / `NavMesh.*`(地貌/AI 导航)—— 两条不互相覆盖。
- **`AC-1-27` 的白名单写法**:字段类型 ∈ {表现层允许集}(float/Vector3/枚举/`CharacterController` 引用/1 自有类型/`ICameraRig` 只读引用…)+ 边界程序集的整数类型;**其余类型一律红**。血量/技能/库存/任务的"自造类型"(`struct Health`)在类型域上即被拒,与命名无关。
- **`AC-1-29` 的签核载体**:playtest 清单四项逐条 + 「发现软锁 ⇒ 升级为 6 的阻塞项」的升级路径写明(软锁是**关卡几何问题**,不是 1 的 bug —— 归属分界要留在签核记录里);`slopeLimit`/`stepOffset` 取值未定(`O-9`)⇒ ① 的真判据迟于本故事,先交付清单与流程。
- **与 story 003 的接缝**:本故事交付**机制**(位图 + 调用面),003 交付**消费性质**(`K_suppressed = 0` 时走裸 `DECEL`、不吃地貌乘数,AC-1-18 附注)。两半合起来才构成轴 4 的完整验收。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:`AC-1-28` asmdef 引用集白名单本体(本故事只**扩展其调用点判据面**至 `Acquire/Release`)与 `AC-1-01③` 的 `Physics.*` 零引用
- Story 003:`K_suppressed` 在乘数链中的减速性质(裸 `DECEL`、`AC-1-18` 附注)
- Story 005:压制与联机模式的交互(无交互 —— Client 模式压制置位走同一本地机制,上行照发)
- 系统 4 的 `AC-4-19`(4 与 10 同持时 4 先 Release)—— 本故事只保证 1 侧位图语义可运行,4 侧判据在 4 的 Epic 签
- 10 的 lease **存续期规则**(动作实例存续期 = 压制期,Release 时机归 10)、25 的压制时机(攻击动画窗口)—— 调用者各自的家规
- 29 死亡禁用移动(**不占本位图**;`SetEnabled` 或复用形态待 29 细化,随其 GDD 轮)
- `O-5`(44 读垂直速度的反向登记)—— 悬空义务归 44 下轮回填,本故事只守「44 不得读 `MotorSuppressed` 推动作态」的接口可见性边界

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1-27**: 类型图无游戏状态字段(反射 + 类型白名单)。
  - Given: 1 的移动程序集全量公开与私有类型的字段闭包(递归,承 story 004 的 AC-1-34 同法)。
  - When: 反射扫描按**字段类型白名单**判定。
  - Then: 全部字段类型 ∈ 白名单;血量/技能/库存/任务的任何表示形态(自造 struct / int / 引用他系统类型)零命中。
  - Edge cases: **假阴性机器负例(AC 明文)**:`struct PlayerHp` 字段名为 `_foo` ⇒ 类型不在白名单 ⇒ 必须红(名字黑名单写法在此通过 = 该判据本身不合格,测试须证明用的是类型判据);`object`/泛型 `T` 装箱逃逸须覆盖(反射到运行时实际类型)。
  - Negative fixture: 给控制器加一个 `int _currentSkillLevel` ⇒ 红;加 `List<Item> _backpack` ⇒ 红。

- **AC-1-23(机制)**: 位图 + 幂等 + 只碰自己位。
  - Given: `Acquire(Self)` / `Acquire(Emergency)` / `Acquire(Combat)` 的受控序列。
  - When: 逐步调用并读 `IsSuppressed`。
  - Then: 任一位置位 ⇒ `IsSuppressed == true`;`Release(Self)` 后 `Emergency`/`Combat` 位**保持**,`IsSuppressed` 仍 true(丢失更新缺陷的正面否定);全 0 ⇒ false。幂等:同 src 二次 `Acquire` 位图不变;同 src 二次 `Release` 无下溢不变。
  - Edge cases: 从未 Acquire 过的 src 直接 Release ⇒ no-op(不抛、不误清他位);三源同时各持各放交错 10² 随机序(固定夹具序,**非测试内随机** —— coding-standards 确定性)终态与逐位预期一致。
  - Negative fixture: 单 bool 实现(旧形态)⇒ "4 Release 顺手清掉 10" 序列红 —— 本夹具就是当年裁定触发形态。

- **AC-1-23(调用面)**: 调用点集合白名单。
  - Given: 全工程 `Acquire(`/`Release(` 调用点经 AST,按调用者所属程序集归类。
  - When: 白名单校验。
  - Then: 集合 = {4, 10, 25} ∪ 1 内部(测试夹具除外);UI 程序集(`Gameplay.UI`)零命中;44 零命中(`O-5` 边界的可判半边)。
  - Edge cases: **接口住 1 不误伤**(AC 明文:判据是调用点非定义位置)—— 1 定义 `IPlayerMotor` 不得使本条红;10 经中间封装层调用 ⇒ 闭包追溯(承 story 001 传递闭包先例),封装层本身在 {4,10,25} 的引用集内才合法。
  - Negative fixture: UI 按钮回调里 `Acquire(Self)` ⇒ 红;新增第四个调用者(如 23 采集)未走裁定 ⇒ 红(白名单硬失败,提醒"加位先改 GDD")。

- **AC-1-23(负边界)**: 压制不含攻击。
  - Given: 1 的攻击/意图出口(若有)与压制读取面。
  - When: `IsSuppressed == true` 时断言 1 **不**拦任何攻击意图路径(1 本无攻击面 —— 判据 = 1 的代码中零「以压制为由拒发意图」的路径)。
  - Then: 被压制玩家的攻击意图照常上行/进 sim(25 判定式不查 `MotorSuppressed` 是 25 侧义务,1 侧只守"不扩语义")。
  - Edge cases: `Jump` **在**作用域内(压制期不得起跳,轴 4 表原文)⇒ 与攻击的负边界在测试中并列存在,防"攻击也不跳"式扩权。
  - Negative fixture: 1 内加 `if (IsSuppressed) return;` 于攻击输入转发路径 ⇒ 红。

- **AC-1-12**: 禁止项零引用(grep + 引用集双判据)。
  - Given: ① Roslyn 分析器扫 `Terrain.SampleHeight` / `Terrain.*` splat / 高度图 API / `NavMesh.SamplePosition` / `NavMesh.Raycast`;② `AC-1-28` 程序集引用闭包。
  - When: 编译期 + 装载期各跑一次。
  - Then: 双判据均零命中;地貌查询实际路径 = 整数查表(story 003 的 fake 表注入即其测试缝)。
  - Edge cases: 别名/委托捕获(`Func<Vector3,float> s = Terrain.SampleHeight;`);经 `UnityEngine.Terrain` 命名空间全限定写法;`NavMesh.CalculatePath`(不在禁列 —— 判据按 AC 列举闭集,**不得自行扩大**禁止面误杀合法实现)。
  - Negative fixture: 注入一行 `Terrain.SampleHeight` 的"顺手探坡"实现 ⇒ 红。

- **AC-1-29(ADVISORY)**: 卡死清单 playtest 签核。
  - Given: 可玩构建 + 签核模板四项(软锁 / 移除地面会下落 / 跳跃规避 / 深水形态)。
  - When: playtest 执行。
  - Then: 逐项签核记录存 `production/qa/evidence/player-controller/`;发现软锁 ⇒ 按 AC 原文**升级为 6 的阻塞项**(建 issue 指向系统 6,不记在 1 的缺陷账)。
  - Edge cases: ② 的 `isGrounded` 陈旧判据形态挂 `OQ-1-12`(症状签核可先做,机制文案随后同步);④ 的深水形态依赖 6 的几何边界定义,表未就位 ⇒ 记 `NOT-RUN`。
  - 签核前状态:`NOT-RUN`(ADVISORY 不红构建;**不得借绿**)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/player_controller/motor_lease_test.cs` — must exist and pass(位图语义 + 幂等 + 交错终态 + 丢失更新负例)
- Logic: `tests/unit/player_controller/state_purity_test.cs` — 字段类型白名单反射(含 `struct PlayerHp` 改名绕过负例)
- Integration: `tests/integration/player_controller/caller_whitelist_test.cs` — 调用点 AST 扫描 + `AC-1-12` 双判据
- Visual/Feel: `production/qa/evidence/player-controller/ac-1-29-stuck-checklist.md` — playtest 签核(ADVISORY)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/`;登记口径 = `tests/unit|integration/player_controller/`)
⚠️ `AC-1-29` 签核面 `NOT-RUN`(需可玩构建);② 机制文案随 `OQ-1-12` spike 同步 —— **均不得借绿**。

---

## Dependencies

- Depends on: Story 001(`AC-1-28` 白名单对像 —— 本故事扩调用点面)/ Story 003(`K_suppressed` 消费性质已立,本故事接上机制)
- Unlocks: 系统 4 的 `AC-4-19` 转可运行(位图落位后 4 侧判据才有物可测);10 / 25 的压制调用接入;42 UI 冻结移动需求走 4 的合法路径(`§Interactions` 医馆面板行的实现前提)

---

## Completion Notes

**Completed**: 2026-10-02(实现 `5572d66`;两条 BLOCKING 判据于同日修复,`MotorLeaseTest` **14/14** 通过)
**Criteria**: 4 条 AC 全部落地。载体 = `unity/Assets/Tests/EditMode/PlayerController/motor_lease_test.cs`(14 例):
  - ✅ **AC-1-23(机制)** —— 位图 5 例:单源一位 / 幂等(位语义非引用计数)/ 只碰自己位(纪律①)/ 未持有 Release 无下溢 / 交错源。
  - ✅ **AC-1-23(调用点白名单)** —— 4 例:**UI 源码零 `LeaseSource`/`MotorLease` 符号引用**(`Gameplay.UI` + `Gameplay.Presentation/Skeuomorphic`,目录缺失即红不静默)· **调用点集合 ⊆ 白名单**(IL 扫描器逐 `call`/`callvirt`/`newobj` 解析 `MotorLease.Acquire|Release` 的调用者)· **扫描器非空转自检**(对含 lease 调用的程序集必须报出 ≥1 处,否则判据失效即红)· **ordinal = 系统号**(`Self 4 / Emergency 10 / Combat 25`,ADR-014 append-only 禁重排)。
  - ✅ **AC-1-27(字段类型白名单)** —— 3 例:1 的字段类型 ∈ 白名单(**DeclaredOnly**,不牵连 `MonoBehaviour` 基类;含**非空转守卫** —— 字段集为空即红)· **负向夹具**(自造 `FakeHealthState` / `FakeVitalityState` 须被拒,后者专证「改名后仍被拒」即原黑名单漏洞形态;`List<自造状态>` 递归须拒;未登记泛型 `Stack<int>` 须拒)· **正向半边**(白名单不得过窄)。
  - ✅ **AC-1-12** —— 2 例:`SampleHeight`/`TerrainSample`/`NavMeshSample` 零引用。
  - ✅ `LeaseSource` ordinal 表 = `Self 4 / Emergency 10 / Combat 25`(原挂账条件已满足)。
  - ✅ 旧 `SetSuppressed(bool)` 公开面**不存在**(全仓零命中)。
**Deviations**: **无残留缺陷**。本 story 曾于同日两次记账,留痕如下:
  ① **首轮(状态回填轮)**:发现两条 BLOCKING 判据有缺陷 —— AC-1-27 实现为**字段名黑名单**(`{"hp","skill","inventory","quest","health","mana"}`),而 AC 原文与其 Guardrail 明写「须按**字段类型白名单** —— 后者是假阴性机器」,黑名单形态**恒真**;AC-1-23 只验位图机制,**零调用点断言**。据此记 `In Review`,依「不得借绿」不转 Complete。
  ② **本轮(判据修复轮)**:两条均已按 AC 原文重写 —— AC-1-27 改为**类型域**判据(与命名无关,自造类型在类型域上即被拒);AC-1-23 补 IL 调用点扫描 + UI 零符号引用双判据。两条各配**非空转守卫**(否则「全绿」仍可能是空转)。
  ③ **AC-1-23 调用点集合当前为空**(P0 尚未接线 —— 全仓生产代码零 `Acquire`/`Release` 调用点)。故该断言此刻是「白名单子集」守卫 + 扫描器有效性自检,而非调用点**覆盖**。真正的调用点覆盖须待 4 / 10 / 25 接线 —— 与「Unlocks」栏所列一致。**此为本 story 唯一的残留登记,不影响其 4 条 AC 的落地判定。**
  ④ `AC-1-29`(ADVISORY)依赖 `OQ-1-12` 接地 spike 的 `slopeLimit`/`stepOffset` 取值(`O-9`),判据迟于本 story,已按原文只交付清单与流程。
**Test Evidence**: `production/qa/evidence/` 无本 story 专项件;复跑证据 = 2026-10-02 batchmode 全量 EditMode(`total 2028 · passed 1995 · failed 0 · skipped 32 · inconclusive 1`),`MotorLeaseTest` **14/14 Passed**。
**Code Review**: 尚无独立评审件。`5572d66` 为编译修复轮、本轮为判据修复轮,均由用户裁定后执行,非双代理评审。**评审报告原文未落 evidence**(与全 6 story 同缺口)。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
