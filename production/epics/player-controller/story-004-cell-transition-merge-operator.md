# Story 004: 跨格检测与归并算子 —— 整数化 · tick 边沿提交 · 事件语义全表

> **Epic**: 玩家控制器与移动
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/player-controller-and-movement.md`
**Requirement**: TR-player-002(玩家位移 = 纯表现态;唯一投影 = 跨格世界流事件;判据分两层 —— 载荷类型纯净 + 事件率与帧率无关)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事是 **ADR-009 §六 世界流有界性论证的唯一依赖点**:一旦归并算子被移除,事件率上界从 tick 频率**退化回帧率**,该论证**静默**失效(EC-5)。同时它承载系统 1 首轮 `/design-review` **第二根因**(「归并算子三版伪码互不等价」→ `AC-1-32` 双向用例)。

**ADR Governing Implementation**: ADR-020(§四 唯一投影 = `ActorCellEntered`)· ADR-009(世界流 + 有界性 + `tick` 字段语义 = "观察到跨格的那一 tick")· ADR-005(`IEventSink.Append` / `ITickProvider`)· ADR-015(§三 单一整数格,`WorldPos` 为唯一跨系统坐标类型)· ADR-006(Amendment B `PatientId.None` 哨兵不污染高水位)
**ADR Decision Summary**: ADR-020 §四:`ActorCellEntered` 载荷 `{ actor_id, cell(WorldPos), tick }` —— **只有整数格,没有连续坐标**;`Append` 每 tick 至多一条(F-1-1b 归并)。ADR-006 Amendment B:世界级/玩家级事件用 `PatientId.None = -1` 哨兵,`max(patient_id)` 重构扫描**显式排除**该 Kind。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: `Mathf.FloorToInt(p / LATTICE_SIZE)` 负数语义已核(C# 参考实现即 `(int)Math.Floor(f)`;`⌊-0.5⌋ == -1` ≠ 截断 `0`;`-0.0f` → `0` 数学正确,**不是陷阱**)。⚠️ **残留风险在 `Position` 本身**:float32 在 `|p| ≈ 10⁵ m` 处有效间距接近亚格量级 ⇒ 贴界行走时跨格与否由累积误差决定。**GDD 已裁:接受**(表现态;事件一经发出即成真源,重放读事件不重算)但须明写 —— 本故事的实现注释必须含此条。

**Control Manifest Rules (this layer)**:
- Required: **空间位置一律整数格**;需亚格精度用格的**整数细分**,**不引入 `Fix` 坐标** — ADR-015 §三:179
- Required: 一切推进走定 tick;**`ActorCellEntered.tick` 只能来自 `ITickProvider.CurrentTick`**(禁帧数 / 真实墙钟)— ADR-005(manifest Core · tick 驱动)
- Forbidden: **chunk 局部坐标系** — ADR-015 §四:197(跨块判定必错;本故事的格身份必须全图一致)
- Guardrail: 载荷类型纯净的判据 = **递归反射断言**,不是 grep `Vector3`(F-1-6 合法路径必然读 `Vector3` ⇒ grep 式判据是**假阳性机器**,会误杀正确实现)

---

## Acceptance Criteria

*From GDD `design/gdd/player-controller-and-movement.md`, scoped to this story:*

- [ ] **AC-1-02(BLOCKING)** —— **载荷类型纯净**:**反射断言** `ActorCellEntered` 的字段类型集合 ⊆ `{int32, int64, 整数枚举}`。(**判据是载荷类型,不是 grep** —— F-1-6 要求 `FloorToInt(p / LATTICE_SIZE)`,合法路径**必然**读 `Vector3` ⇒ 「grep `Vector3`」式判据会**误杀正确实现**,是假阳性机器。)
- [ ] **AC-1-34(BLOCKING · 复核新增 —— 载荷纯度须递归)** —— **`AC-1-02` 的递归形态**:反射断言须**递归下钻** `ActorCellEntered` 载荷的**嵌套类型**(如 `WorldPos` 内的三个字段、任何 `struct` 成员),断言**可达字段类型闭包** ⊆ `{int32, int64, 整数枚举}`。**非递归写法**会被"外层是个 struct、内层藏着 `Vector3`"的载荷绕过 —— `AC-1-02` 单独存在时是**浅断言**。
- [ ] **AC-1-03(BLOCKING)** —— **归并算子与帧率无关(分两层断)**:
  ① **单元(不接积分器)**:喂跨格检测 + 归并算子**同一 tick 的 N 个位置样本**,`N ∈ {1, 8, 64}`,断言 `Append` 次数 == **1**,且载荷格 == **第 N 个样本**的格;
  ② **集成(逐 tick 核对,非按"相异格"计数)**:断言 `Append 总数 ≤ tick 数` ∧ **逐 tick** 核对「`Append` 的格 == 该 tick 边沿的 `pending_cell`」(即提交序列的**相邻不同格**转移数)。
  ⚠️ **原 ② 的合取项 `事件数 ≤ 相异格数` 已删除 —— 它与 EC-3 直接矛盾,恒不可满足**(回访会再发)。正确的不变量是**序列**性质的。
  ⚠️ **不得**断言"两种帧率下事件序列相同" —— 变 `dt` 积分会改轨迹,该等式**设计上不成立**。
  ⇒ **② 必须配 `AC-1-32` 的反向用例**(回访必须发 2 条)才构成互补判据,**不得单独立项**。
- [ ] **AC-1-07(BLOCKING)** —— **跨格判定走引擎算符**:`Mathf.FloorToInt`(或 `Vector3Int.FloorToInt`)**零**手写整数除法 / 手写 `floor`。**AST / Roslyn 分析器判据**(`.cs` 文本匹配会被 `Mathf.Floor` 之类绕过)。**并须覆盖 F-1-6 的负数语义**(`⌊-0.5⌋ == -1` ≠ 截断 `0`)。
- [ ] **AC-1-05(BLOCKING · 复核收窄)** —— **载荷 `tick` 的来源唯一**:写入 `ActorCellEntered.tick` 的值**必须**来自 `ITickProvider.CurrentTick`。⚠️ **不得**写成"1 不得使用 `Time.*`" —— **EC-13 明写 `dt = Time.deltaTime`(受 `timeScale` 影响)是要求**(暂停时 `dt = 0` ⇒ 不发事件)。**被约束的是载荷的 `tick` 字段,不是帧时间源。**
- [ ] **AC-1-08(BLOCKING · 复核补限定词)** —— **静止不产生格变化**:`GIVEN` 玩家**静止且未被 collide-and-slide 推挤**,`WHEN` 连续 N 帧(N ≥ 256)firing,`THEN` `last_committed_cell` **不变** ∧ `Append` 次数 == 0。⚠️ **"且未被推挤"是限定词,不可省** —— 原稿省略它会把**合法的贴墙推挤**判为失败。反向用例:`Move(Vector3.zero)`(持续调 Move)⇒ 若实现如此,**本条失败**(EC-1 的 ① 反面)。
- [ ] **AC-1-13(BLOCKING)** —— **EC-2 对角单事件**:喂一帧内 x 与 z **同时**跨格的位置样本 ⇒ `Append` == **1**,载荷为三维**同时**取 floor 的新格;**不**拆两条、**不**补角格。
- [ ] **AC-1-14(BLOCKING)** —— **EC-3 回访再发**:`GIVEN` 玩家离开 A 格进 B 格,`WHEN` 退回 A 格,`THEN` **再发一条** `ActorCellEntered{A}`。⚠️ **复核点名的漏网实现**:"已访问格集合"式(visited-set)写法可通过**其余全部 AC** ⇒ 本条的**反向用例必须存在**(断言 `Append` 次数 == 2,不是 1)。
- [ ] **AC-1-15(BLOCKING)** —— **EC-4 传送只发落点**:`GIVEN` 一帧内注入 ≥ 2 格位移(重生 / 位置修正,即「被放置」路径),`WHEN` 检测运行,`THEN` `Append` == **1**,载荷 == **落点格**,**零中间格补发**。
- [ ] **AC-1-16(BLOCKING)** —— **EC-6 `dt` 钳位不累积**:`GIVEN` 一帧 `dt = 10 × MAX_DT`,`THEN` 该帧位移按 `MAX_DT` 计,超出部分**丢弃**;下一帧 `dt` **不**含积压(断言下一帧位移 == 常规 `dt` 下的位移)。
- [ ] **AC-1-32(BLOCKING · 复核新增 —— 根因 2「归并算符三版不等价」)** —— **tick 内折返不发事件**(§States 的 `else → pending_cell := null` 支):`GIVEN` 同一 tick 内位置样本序列 `A → B → A`(`A = last_committed_cell`),`WHEN` 该 tick 边沿到达,`THEN` `Append == 0` ∧ `last_committed_cell == A`(否定初稿三版伪码共有的「残留 `pending = B` ⇒ 发一条 B」缺陷)。**反向用例(必须同时存在,否则与 EC-3 混淆)**:`GIVEN` 跨**两个** tick 边沿的 `A → B → A`(`A→B` 已在第一个边沿提交),`THEN` `Append == 2`(两条事件 —— 两次跨格都真实发生过)。⇒ **两个用例互为判据**:只测其一会被"一律不发"或"一律发"两种错实现通过。
- [ ] **AC-1-17(BLOCKING)** —— **EC-9 接地不靠帧计数**:源码路径中**零**"连续 N 帧接地"式计数器;`Grounded` 进入条件含 `垂直速度 ≤ 0`。**AST / 代码审查判据**(计数器的形态不限,故 grep 不够)。⚠️ **`Grounded` 进入条件的具体形式被 `OQ-1-12` 挂起**(R12)—— 若 spike 改判,本条随轴 2 表同步重写。
- [ ] **AC-1-24(BLOCKING)** —— **`PatientId.None` 不污染高水位**:`ActorCellEntered` 的 `Patient` 字段 == `PatientId.None`,且 `max(patient_id)` 的重构扫描**显式排除**该事件(ADR-006 Amendment B / ADR-009 Amendment E)。**判据**:喂一条含 `ActorCellEntered` 的流,断言重构出的 `next` **不因它变化**。
- [ ] **AC-1-04(ADVISORY · 复核降级)** —— **VR 零事件**:VR 模式下头显 / 手柄位移**不产生** `ActorCellEntered`。⚠️ **P0 主语不存在**(VR 推 P1a,AC-20-06/07)⇒ ADVISORY;**发版前(VR 落地时)回升为 BLOCKING。**

---

## Implementation Notes

*Derived from GDD R4/R5/R6 · F-1-1b/F-1-1c · F-1-6 · EC-1…EC-8 · §States「跨格边沿检测」(唯一权威处):*

- **算符唯一权威处**:归并算子的伪码在 **§Detailed Design → States and Transitions → 跨格边沿检测**,GDD 明写「初稿在 §States / F-1-1b / F-1-6 三处各写一版**不等价**的伪码,是缺陷本身,已收敛」⇒ 实现**照该节逐字落地**,不得参考任何旧 ADR/文档里的版本。两级 guard + `pending_cell` 覆盖语义 + tick 边沿提交。
- **`Cell := WorldPos`**(`(i32,i32,i32)`,ADR-015 §三);`Vector3Int` 只作投影**中间量**,不进接口(`AC-1-10③` 已在 story 001 守)。
- **`tick` 字段语义变更须写进 doc comment**:不是"跨格发生的那一 tick",而是"**观察到跨格的那一 tick**" —— 二者最多差一个 tick(ADR-009 Amendment G 措辞已同步)。实现注释与 codec 注释都要点明,否则重放期会被当成事件真实时刻。
- **EC-2 对角单事件**:`FloorToInt` 对三维**同时**取值,天然给出对角新格 —— 判据是「不补角格」(禁止把对角拆成 x-then-z 两条)。
- **EC-4「被放置」路径**是本故事唯一允许直接写 `transform.position` 的入口(story 001 的 `AC-1-01` 分析器须给它显式豁免注解 —— 建议 `[TeleportEntryPoint]` 特性 + 分析器白名单),且落点仍是"发一条落点格事件",**零中间格补发**(与 F-1-1c 竖直隧穿同构:中间格从未被占据,伪装连续会让 AI 看到未发生的移动)。
- **EC-8 垂直跨格**:y 轴显式豁免(F-1-1c)⇒ 跳跃/坠落可一 tick 跨多层,但事件数仍 ≤ 1/tick(由归并算子保证)。旧论证「垂直跨格数 ≤ ⌈JUMP_HEIGHT_MAX / LATTICE_SIZE⌉」**已作废**,不得复活。
- **EC-13 暂停**:`dt = Time.deltaTime` 受 `timeScale` ⇒ 暂停时 `dt = 0` ⇒ 无位移 ⇒ 不发事件(这是**要求**,不是缺陷;`AC-1-05` 的判据只约束载荷 `tick` 来源)。
- **visited-set 反模式**:实现若用「已访问格集合」去重 ⇒ `AC-1-14` 红。本故事的 QA 台账把该反模式列为**首要负例**。
- `AC-1-17` 的进入条件形式随 `OQ-1-12` spike 回填;先落「零帧计数器」的 AST 判据(可先行)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005:联机下 `Append` 权归谁(客户端零 `Append` + 第二 QoS 上行 + 提交态归主机,`AC-1-30`)—— 本故事只实现**单机/主机模式**的算子本身
- Story 003:位移的产生(v 链积分)与 `MAX_DT` 在积分中的钳位实现 —— 本故事只断言钳位的**不累积**性质(`AC-1-16`)
- Story 001:`LATTICE_SIZE` 单一装载常量与原点/轴三项契约(`AC-1-10`)
- Story 006:`AC-1-12`(R3/R8 禁止项零引用 —— `Terrain.SampleHeight` / `NavMesh.*` 的 grep + 引用集双判据)与 `AC-1-27`(不持有游戏状态)
- `OQ-4-12` 类跨 chunk 线性化(4 的 `StableId` 议题,不在 1);chunk 流式激活(系统 6,ADR-023 ⑥)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1-02 / AC-1-34**: 载荷类型纯净(递归闭包)。
  - Given: `ActorCellEntered` 及其载荷全部嵌套类型(`WorldPos` 三字段、任何 struct 成员、数组/List 元素类型)。
  - When: 反射递归下钻求**可达字段类型闭包**。
  - Then: 闭包 ⊆ `{int32, int64, 整数枚举}`;`float`/`double`/`Vector3`/`string` 零命中。
  - Edge cases: 外层 struct 内藏 `Vector3`(`AC-1-34` 明文点名的绕过形态)⇒ 必须红;`const int` 字段不误报(承 input-system story-006 S7 先例:编译期字面量不该让整族载荷恒红);`Nullable<int>` / 枚举数组元素须覆盖。
  - Negative fixture: 把 `WorldPos` 某字段改 `float` / 在载荷里加一个 `Vector3 offset` 字段 ⇒ 红。

- **AC-1-03① / AC-1-32**: 归并算子单元判据(四组用例,互为补)。
  - Given: 直接喂归并算子(不接积分器),同 tick 内 N 个位置样本。
  - When: tick 边沿到达。
  - Then:
    - `N ∈ {1, 8, 64}` 单调跨格 ⇒ `Append == 1` 且载荷格 == **第 N 个样本**的格;
    - **折返** `A → B → A`(同 tick,`A = last_committed_cell`)⇒ `Append == 0` ∧ `last_committed_cell == A`(`AC-1-32` 正例);
    - **跨两 tick** 的 `A → B → A` ⇒ `Append == 2`(`AC-1-32` 反例);
    - 反向用例(AC-1-03① 与 AC-1-32 同夹具族)⇒ 「一律不发」与「一律发」两种错实现各被一条捕获。
  - Edge cases: 同 tick 内 tick 边沿恰在样本间到达(fake `ITickProvider` 可控相位)⇒ 归属正确;N=0(无样本)⇒ 不发。
  - Negative fixture: 算子改「追加」语义 ⇒ `Append == N` 红;算子改 visited-set ⇒ 由 `AC-1-14` 用例捕获(见下)。

- **AC-1-03②**: 集成上界(逐 tick 核对序列性质)。
  - Given: 跑 ≥ 10³ tick 的移动序列(含折返、对角、传送、垂直)。
  - When: 收集 `Append` 序列与每 tick 边沿的 `pending_cell`。
  - Then: `Append 总数 ≤ tick 数` ∧ 逐 tick 核对「`Append` 的格 == 该 tick 边沿的 `pending_cell`」(= 提交序列的**相邻不同格**转移数)。
  - Edge cases: **不得**用「事件数 ≤ 相异格数」作判据(与 EC-3 矛盾,恒不可满足 —— GDD 明文);**不得**断言两种帧率下事件序列相同(设计上不成立)。
  - Negative fixture: 移除归并 ⇒ 帧率 > tick 频率时 `Append > tick` 红(证明有界性载体真实存在,不是空转)。

- **AC-1-07**: 引擎算符判据 + 负数语义。
  - Given: 源串两种 —— `Mathf.FloorToInt(p / L)`(合法)与手写 `(int)(p / L)` / `Mathf.Floor(...)` / 自写 floor。
  - When: Roslyn 分析器编译期扫描。
  - Then: 手写除法/自写 floor ⇒ **构建失败**;引擎算符通过。数值面:`⌊-0.5⌋ == -1` 断言成立(负格不误截断为 0)。
  - Edge cases: `-0.0f` ⇒ 归 0 号格(GDD 已核:数学正确,不是陷阱,但须有断言钉住);`|p|` 接近 `10⁵ m` 的贴界抖动 ⇒ 允许(已裁接受),但不得出现「同 tick 内因误差抖动产生 >1 条」。
  - Negative fixture: `floorf` 式自写算符;别名 `Func<float,int> f = (v) => (int)v;`。

- **AC-1-05**: `tick` 来源唯一。
  - Given: fake `ITickProvider`(可控 `CurrentTick`)。
  - When: 跨格发生。
  - Then: 载荷 `tick == provider.CurrentTick`(逐位);AST/调用点断言 1 的写流路径**无** `Time.frameCount` / `DateTime` / `Environment.TickCount` / 自增计数器进入 `tick` 字段。
  - Edge cases: **允许** `dt = Time.deltaTime`(EC-13 是要求)⇒ 判据只扫载荷 `tick`,不得把 `Time.*` 整体判违规(GDD 明文的"原判据过宽"教训)。
  - Negative fixture: 把 `Time.frameCount` 写进 `tick` ⇒ 红。

- **AC-1-08**: 静止零事件(含限定词与反向用例)。
  - Given: 玩家静止**且未被 collide-and-slide 推挤**,连续 N ≥ 256 帧。
  - When: 帧循环 firing。
  - Then: `last_committed_cell` 不变 ∧ `Append == 0`。
  - Edge cases: 贴墙推挤(合法)⇒ **不得**判失败(限定词不可省);实现若每帧调 `Move(Vector3.zero)` ⇒ EC-1 的 ① 反面,本条**失败**(GDD 明文要求)。
  - Negative fixture: 每帧 `Move(Vector3.zero)` 的实现形态。

- **AC-1-13 / AC-1-14 / AC-1-15**: EC-2/3/4 事件语义。
  - Given: 三组夹具 —— ① 一帧内 x 与 z 同时跨格;② A→B→A(跨两 tick);③ 一帧注入 ≥ 2 格位移(被放置路径)。
  - When: 检测运行。
  - Then: ① `Append == 1` 且载荷为三维**同时** floor 的对角新格(不拆两条、不补角格);② `Append == 2`(回访再发,visited-set 必红);③ `Append == 1` 且载荷 == **落点格**,零中间格补发。
  - Edge cases: ① 含 y 同时变化(三维对角);③ 注入 ≥ 2 格 vs 恰 1 格(后者是普通跨格,须仍发 1 条落点)。
  - Negative fixture: visited-set 去重实现(② ⇒ `Append == 1` 红);中间格补发实现(③ ⇒ `Append == n` 红)。

- **AC-1-16**: `dt` 钳位不累积。
  - Given: 一帧 `dt = 10 × MAX_DT`。
  - When: 位移结算 + 下一帧常规 `dt`。
  - Then: 该帧位移 == 按 `MAX_DT` 计的位移,**超出部分丢弃**;下一帧位移 == 常规 `dt` 下的位移(无积压补偿)。
  - Edge cases: 连续多帧大 `dt`(断点续跑 / 首次加载)⇒ 每帧独立钳位;`dt = 0` ⇒ 零位移不发事件(EC-13 呼应)。
  - Negative fixture: 实现做"时间债补偿"(把丢掉的 `dt` 累加到下帧)⇒ 红。

- **AC-1-24**: `PatientId.None` 不污染高水位。
  - Given: 一条含 `ActorCellEntered` 的事件流(混在真实 `patient_id` 事件之间)。
  - When: 执行 `max(patient_id) + 1` 重构。
  - Then: 重构出的 `next` **不因它变化**;`ActorCellEntered.Patient == PatientId.None`(-1)。
  - Edge cases: 流中**只有** `ActorCellEntered` ⇒ `next` == 初始值(不是 0 被 -1 拉低,也不是误取 -1);`IIdAuthority` 高水位与 `max(id)` 扫描的三流并集口径(ADR-008)由该测试的断言面覆盖。
  - Negative fixture: 扫流实现不过滤 `Kind == ActorCellEntered` ⇒ `next` 变化 ⇒ 红。

- **AC-1-17**: 零帧计数 + 接地进入条件。
  - Given: 1 的接地相关源码经 AST。
  - When: 查找"连续 N 帧接地"式计数器(任意形态:字段、局部、LINQ `Count` 窗口)。
  - Then: 零命中;`Grounded` 进入条件含 `v_y ≤ 0`。
  - Edge cases: `Grounded` 具体形式挂 `OQ-1-12` ⇒ spike 结果回填后本条随之重写(**记 BLOCKED-BY-OQ-1-12 不借绿**,但"零帧计数"半边可先行签)。

- **AC-1-04(ADVISORY)**: VR 零事件 —— P0 主语不存在,记 `NOT-RUN`(发版前 VR 落地时回升 BLOCKING,须重立)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/player_controller/cell_transition_test.cs` — must exist and pass(归并算子四组用例 + EC-2/3/4 语义 + 递归载荷反射 + 高水位对账)
- Logic: `tests/unit/player_controller/stream_bound_test.cs` — 集成上界(≥ 10³ tick,序列性质判据)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/EditMode/PlayerController/`)
⚠️ `AC-1-17` 的接地进入条件半边记 **BLOCKED-BY-OQ-1-12**;`AC-1-04` 记 `NOT-RUN`(P0 无 VR)—— **均不得借绿**。

---

## Dependencies

- Depends on: Story 001(`WorldPos` / `LATTICE_SIZE` 单一源 + `CharacterController.Move` 唯一写入点的豁免注)/ Story 003(位置更新是本链上游)
- Unlocks: Story 005(`Append` 权与提交态归属的判据建立在"单机算子正确"之上)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
