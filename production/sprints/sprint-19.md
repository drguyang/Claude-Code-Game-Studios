# Sprint 19 — **`2026-10-24`** → **`2026-10-25`** · **AC 门清偿**(G-4 · G-5 · G-6 · G-7)

**周期**:`2026-10-24` 起(不设结束日期口径,做完即收,**收口时补记实际收口日** —— 承 sprint-14 `2026-10-18` / sprint-15 `2026-10-20` / sprint-16 `2026-10-21` / sprint-17 `2026-10-21` / sprint-18 `2026-10-24` 同例)。
🔁 **实际收口日 = `2026-10-25`**(收口轮补记;收口判定见 §九)。

**上游件**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` **§6.5:910** sprint-19 行(本 sprint 即其兑现)。
**前置**:sprint-16 ③(债册「P0 面 / 状态」两列)✅ 已收口 2026-10-20。
**前序**:sprint-18 已收口(`production/sprints/sprint-18.md` §九.7,判据面可收口 · 两条不记绿)。

---

## Phase 1 硬前置:重扫核出的三处前提错位(2026-10-24 实测)

> 承 `sprint-plan` skill Phase 1 的硬前置:**不得从陈旧快照写任务描述**。
> 本轮逐符号重扫现树,核出**三处**路线图 / 债册的表述与现状不符。

### ① 路线图的 Exit 漏了一半 7a —— **只做 G-4 不可能归零**

路线图 `:910` 原文:`7a/7b · 1,2 · 27 · 52 名下 TD 的 P0 面归零 ⇒ M3 第二条合取可判`。

**实测**(债册 §五 官方脚本,见下方「溯源」):`persistence-service` 名下未闭 P0 面 = **2 条** ——

| TD | 状态 | 对应门 | 说明 |
|---|---|---|---|
| `TD-persist-02` | `open` | **G-4** | 槽位契约高层面 |
| `TD-persist-01` | `open` | **G-2** | sprint-18 结转,**剩两项** |

⇒ **按路线图字面只做 G-4,7a 名下的 P0 面仍是 1,Exit 不可达。**

**处置**:不新增件数,而是把 `TD-persist-01` 的剩余两项**并入 S19-1 同批做**。
**理由(不是凑数)**:剩余判据 ① 是「折叠接入存档路径」,而**唯一合法的接入点就是 `Checkpoint` 内部**
(ADR-010 §六:唯一写入口)—— 它与 G-4 是**同一段代码**,拆成两件会造出「先写 Checkpoint、
再回头改 Checkpoint」的返工。

> ⚠️ `TD-persist-01` 的剩余两项(**2026-10-24 `OQ-7a-10` 裁定 A 后**):
> ① 折叠接入存档路径(`FoldedPredicate` 现零生产调用点)· ③ 折叠产出面无变异反证(签核件 D-5)。
> 原三项中的 ②(裁定)已结。详见 `production/sprints/sprint-18.md` §九.2 的 🔁🔁 补记。

### ② G-7 不是「从零发明类型」,是「把内存态换成流态」—— 且含一处**假断言**

债册 `TD-randomevents-01` 写「`DeferredThreatSlot` 全树零命中」,**该表述正确但不完整**:
52 侧**已有**一个内存版延迟槽,只是不叫这个名字:

```csharp
// unity/Assets/Sim/EventBudget.cs:51
public readonly List<int> DeferSlots = new List<int>();
```

而同一文件里:

```csharp
// unity/Assets/Sim/EventBudget.cs:137-142
/// <summary>验证预算状态可从流重构。</summary>
public static bool ValidateReconstructable(BudgetState state)
{
    // 简化版：验证状态非空
    return state != null;
}
```

⇒ **方法名声称「验证可从流重构」,实现只判非空。** 这正是 ADR-007 §三 逐字禁止的形态
(「只活在可变运行时对象里 ⇒ 主机迁移静默丢失 / 重复补发」),也是本仓反复抓过的
**自造断言顶替判据**。

**更硬的一层**:该方法**零调用点** —— 连测试都没调它。

```bash
grep -rn 'ValidateReconstructable' unity/Assets --include='*.cs'
# unity/Assets/Sim/EventBudget.cs:137:        public static bool ValidateReconstructable(BudgetState state)
# 实跑 = 1 行,即声明本身。零调用点(含 Tests/)。
```

⇒ **「有校验」的自述背后是一个既没被调用、判据也不成立的静态方法。**
按 `coding-standards.md` §Review Evidence Standards,这属于**第五形态**
(「用一句自述顶替了一个从未存在的门」)的变体 —— 门有形、名对、体空、且无人调用。

**处置**:S19-4 的验收判据**必须含「删掉这条假断言并换成真判据」**,不得只加新类型。

### ③ `TD-sim-01` 的债册描述已过期 8 小时

债册 `TD-sim-01` 的 🔁 订正段(落盘 commit `fed0639`,2026-10-24 **00:23**)写:
> `DeathCandidateConsumer` / `PlayerDiedWriter` **仅被测试构造**,**生产侧零构造点**

**实测**(现树):

```bash
grep -rn "new DeathCandidateConsumer\|new PlayerDiedWriter" unity/Assets --include='*.cs' | grep -v '/Tests/'
# unity/Assets/Gameplay.Boot/CompositionRoot.cs:307:  var playerDiedWriter = new PlayerDiedWriter(stream, encoder);
# unity/Assets/Gameplay.Boot/CompositionRoot.cs:332:  var deathConsumer = new DeathCandidateConsumer(
```

`519700c`(2026-10-24 **08:29**)落了生产构造点 ⇒ **该句在落盘 8 小时后即过期**。

**处置**:S19-5 记账订正。⚠️ **本 sprint 不自行宣布 `TD-sim-01` 结案** ——
债册该行自陈「结案与否归 `death-respawn` / QA 侧核,本行只记事实」,记账轮不得越权。

### 附带:`retro-18` 的五条行动项**无结转**

| # | 行动项 | 现状(2026-10-24 实测) |
|---|---|---|
| 1 | 数字须带可跑复算命令 | ✅ 已入 `.claude/docs/coding-standards.md` §Review Evidence Standards |
| 2 | 分支覆盖列 | ✅ 已入 `.claude/skills/code-review/SKILL.md:252`(`Branch(分支覆盖)` 列) |
| 3 | CAPACITY-MODEL 补 16/17/18 三行 | ✅ 在位(`grep -c "^\| 1[678] \|" production/CAPACITY-MODEL.md` ⇒ `3`) |
| 4 | S18-1 结转入口判据写进 sprint-19 正文 | ⛔ **失效** —— S18-1 已于 2026-10-22 拉回 sprint-18 并 `done` |
| 5 | 「某件有门」须给 `file:line` + 可跑命令 | ✅ 已入同一节 |

⇒ **本 sprint 零结转。**

---

## Sprint Goal

把四道 **AC 门**(G-4 / G-5 / G-6 / G-7)从「ADR 已裁 · GDD 已写 · epic 已标 `Complete` · **代码零落点**」
推到「契约在盘 + 有反证夹具」,使 `7a/7b · 1,2 · 27 · 52` 六户名下**未闭 P0 面归零**,
从而让 **M3 Exit 第二条合取**从 `NOT-YET-EVALUABLE` 转为**可机械求值**。

> **AC 门的性质**(承路线图 §3.1:513):这些 epic **零剩余 story** ——
> 卡面全是 `Complete`,而它们点名的契约在代码里**不存在**。
> ⇒ 本 sprint 的产物**不是功能**,是**让已宣布完成的 epic 名实相符**。

---

## Capacity(承 `production/CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束,**不估天**)

| 元 | 值 | 出处 |
|---|---|---|
| **件数** | 计划 **5 件**(4 实现 + 1 记账) | 本节 Tasks 表 |
| **复杂度分布** | **L × 1**(S19-1)+ **M × 3**(S19-2 / S19-3 / S19-4)+ **S × 1**(S19-5) | §3.2「C」= 依赖数 + 判别力要求(人判) |
| **追加率预留** | 按实测区间 **[0%, 57%]** 预留 1 件额度 | §3.1 第三元 |
| **约束** | 每批 **2–4** 件 · 并发 **≤ 3** · **L 件独占 1 批,不与 M/S 混批** ⇒ **三批** | §3.2 三条 |

**复杂度逐件理由**:

| 件 | C | 理由 |
|---|---|---|
| `S19-1` | **L** | 依赖 ≥3(槽位契约 + 两投影 + 载入门 + 折叠接入)**且其中两项在代码里零落点**(叙事上下文查询 / 未决医疗动作查询)—— 命中 L 的两条判据(§3.2) |
| `S19-2` | **M** | 依赖 1(ADR-020 §Key Interfaces 签名齐备);判据可机械构造(`ConsumeCellChanged` 恰好一次) |
| `S19-3` | **M** | 依赖 2(ADR-016 §二/§九 形状 + `EnemyPathing`/`EnemyStateMachine` 在盘);但「决策不进流」需构造可证伪形态 |
| `S19-4` | **M** | 依赖 3(ADR-007 两 `Kind` + 两个 payload 在盘 + `EventBudget.BudgetState` 现内存态);含**删除假断言**的迁移风险 |
| `S19-5` | **S** | 零依赖,判据单一(三处文本同值可 grep) |

---

## Tasks

### Must Have(关键路径)

**批 1 —— `S19-1` 独占(L 不混批)**

| ID | 任务 | 层/型 | C | 依赖 | 验收判据(可复算) | 测试载体 |
|---|---|---|---|---|---|---|
| **S19-1** | **G-4**(`TD-persist-02`)+ **`TD-persist-01` 剩余两项**:`Checkpoint` / `SaveOnExit` / `Load` 三方法 + `Writable` / `Loadable` 两投影 + `Gating` 载入门;折叠接入存档路径 | Core / Logic | **L** | ① `ISaveCodec`(**在盘**)② `IFoldedPredicate` + `HistoryFolder`(**在盘**,`Sim/FoldedPredicate.cs` / `Sim/HistoryFolder.cs`)③ **叙事上下文查询** —— **零落点**④ **未决医疗动作查询** —— **零落点** | ① `ISaveService` 公开方法集 **= `{Checkpoint, Load, LoadBytes, SaveBytes, SaveOnExit}`**,且既有反射断言(`Tests/EditMode/PersistenceService/folded_predicate_test.cs:606` 现断言恰 = `{LoadBytes, SaveBytes}` —— ⚠️ 行号实测订正,曾记 `:600`)**同批更新**,不得删测;② **`AC-7a-19` 四子判据 (a)(b)(c)(d)** 各一例,含**闭合回路**「先手动存 → 读档 → 再覆盖回去」任一环被拒(BLOCKING);③ `Writable` / `Loadable` 为**纯函数投影**(输入 = 槽位 + 上下文,无内部可变状态),各配独立灰置通道 —— 🔁 **2026-10-24 判据对象收窄**:反射断言**只扫 `SlotProjections`(静态类)+ `SlotContext`(readonly struct)**;状态宿主 `SaveSlotLedger` **不在本判据面内**(另以四例状态迁移夹具覆盖);④ 折叠在 `Checkpoint` 内生效 ⇒ 折叠动作**有生产调用链**(`TD-persist-01` ① 的判据) —— 🔁 **2026-10-24 原措辞作废**:「该行在 `Sim.Codec/SaveService.cs` 内」**字面不可兑现**(`Sim` / `Sim.Codec` 兄弟装配互不可见),改判 ①`grep -rn "HistoryFolder\.Fold" … \| grep -v Tests/` ⇒ ≥1 行**且不在 `Sim.Codec/` 内** + ②`SaveService.cs` 内 `_compose(` 落在 `Checkpoint` 体内且 fail-closed;④**字节半**「`Checkpoint` 产出的字节里折叠行出现」= **`NOT-RUN`**(存档体字节布局不存在,用户 2026-10-24 裁定**并入 `TD-persist-03`**);⑤ **折叠产出面变异反证 ≥3 轮**(`TD-persist-01` ③);⑥ **门 A 不变**(`Sim` 引用集恰 = `{BCL, Sim.Contracts}`),且构造形参 **3 → 6**(`folded` / `narrativeContext` / `replaySink` / `compose` 四个可选,🔁 原「3 → 5」漏算 `Checkpoint` 数据源);⑦ **`Load` 交 3/4 段**(`PRE-S19-D` 甲案):头部解析 + 迁移链三分支 + `Replaying` 前半段(字节 → 列表,经构造注入委托交出),**真重放归 `TD-persist-03`** —— 详见 §S19-1 的裁定前提 | `unity/Assets/Tests/EditMode/PersistenceService/save_slot_contract_test.cs`(新) |

> ⚠️ **③④ 两个查询的归属边界(本件最容易被做成越权)**:7a **不拥有**「现在有没有未决医疗动作」
> 这个事实 —— 那是 10 急救动作 / 8 诊断 / 37 病例的**状态**。
> ⇒ 本件只**注入抽象**(接口 + 假件),真实实现归各自系统的实现轮。
> 这与 ADR-010 §三「7a 是义务汇总表」同构:**7a 定义门,不定义被门观察的事实。**

**批 2 —— `S19-2` / `S19-3` / `S19-4` 三件并行(文件不相交)**

#### S19-1 的裁定前提(2026-10-24 · `PRE-S19-D` 已结)

> **用户裁定:甲案 —— `Load` 交 3/4 段。** 读档锁在 **`Verifying` 通过后**置位(裁定 A)。
> **真重放不在本件**,另立 `TD-persist-03`。

**五条实测(逐条可跑,裁定的事实基础)**:

| # | 事实 | 命令 / 出处 | 后果 |
|---|---|---|---|
| **F1** | `Load` 零生产消费者 | `grep -rn "new SaveService" unity/Assets --include=*.cs \| grep -v Tests/` ⇒ **0** | `Load` 落了也没人调 ⇒ 本条当下不阻塞任何在盘行为 |
| **F2** | **装配边双向不通(根因)** | `Sim` / `Sim.Codec` 均只引 `Sim.Contracts`;`Sim.Contracts` 引用集为空 | `Load` 要调 `Sim.EventStream.Append`,而 `SaveService` 住 `Sim.Codec` ⇒ **物理上够不着**。ADR-029 / ADR-031 同一个洞,**同族第三次** |
| **F3** | 迁移链无对象可迁 | `grep -rn "SaveVersion" unity/Assets --include=*.cs` ⇒ **5 行**:`SaveHeader.cs:9`(注释)/ `:23`(常量 `CurrentSaveVersion = 1`)/ `:27`(字段声明)/ `:37`(构造赋值)+ `Tests/.../skill_grown_xp_gain_test.cs:19`(注释)。**其中零处是「读该字段做判断」** ⇒ 迁移链零触发面 | `CurrentSaveVersion = 1` + 零 v0 写入者 ⇒ 空链是**事实**;升链路径须以**注入 v0 夹具字节**驱动 |
| **F4** | 「三流」在存储上是一个 `List<SimEvent>` | `Sim/EventStream.cs:38`;`StreamRouting.Of` 生产侧只用于 `Sim/EventOrder.cs:21` 排序 + `Gameplay.Boot/TelemetryReporter.cs:547` 遥测,**不用于分桶** | ADR-010 §一「头部 + 三流」与现实现有**形状差** ⇒ ADR 级二选一,归 `TD-persist-03` |
| **F5** | 重放不能走 `Append` | `Sim/EventStream.cs:77-83` 的 CAP 检查依赖 `IPresenceQuery` | 读档时在场视图尚未重建 ⇒ 重放会误抛。需独立旁路,三关(CAP / 去重 / 单调)须逐条判 |

**本件交什么 / 不交什么**:

```
Load(SaveSlot):
    Gating        → 安全上下文门(注入抽象驱动,7a 只定义门)
    Verifying     → 复用既有 LoadBytes(校验和 + bak 回退,已在盘)
    ──────────── 以下为 PRE-S19-D 新增 ────────────
    头部解析      → SaveHeader 四字段读回(SaveVersion / WorldSeed / ConfigVersion / Tick)
    迁移链判定    → == 直通 / < 逐版本升(WorldSeed 逐位保留)/ > 拒绝(前向不兼容)
    Replaying(前半) → 字节 → IReadOnlyList<SimEvent>,经构造注入的
                      Action<IReadOnlyList<SimEvent>> 交出
    ⛔ 真重放(列表 → sim 状态)= TD-persist-03,不在本件
```

**四条实现约束**:

| # | 约束 | 判据 |
|---|---|---|
| ① | 重放委托走**构造注入**,**零新增 `Sim.Contracts` 成员、零新增装配边** | 与 `IFoldedPredicate`(S18-2)同一先例:`Sim` / `Sim.Codec` 的 `references` 数组**一字不改** |
| ② | 构造形参 3 → 5(见 §⑧ 的合并口径) | `folded_predicate_test.cs:590` 的 `Assert.AreEqual(3, ps.Length, …)` **必红,须同批改而非删**(`:588` 是 `ctors.Length` 那条,勿混)。🔁 **2026-10-24 订正为 3 → 6** —— 原算术只覆盖「两门 + 重放」,**漏算 `Checkpoint` 的数据源**(加 `compose` 后实为 6);权威口径见 §S19-1 的裁定前提 · 折叠接线形态 |
| ③ | 未注入委托时进 `Replaying` 前**抛具名异常** | **禁静默跳过** —— 静默跳过 = 假绿入口 |
| ④ | 读档锁在 `Verifying` 通过后置位 | 否则闭合回路环 2 到不了锁点 |

**两条残余(不阻塞本件,须如实登记)**:

| ID | 内容 | 处置 |
|---|---|---|
| **R-1** | 读档中途失败会**永久锁槽**(锁在 `Verifying` 后置位,而 `Replaying` 真重放未落地) | `Replaying` 真落地后复核是否移到 `Ready` 后;`TD-persist-03` 解除动作 ④ 已登记 |
| **R-2** | **`AC-7a-01` / `AC-7a-02` 语义层今天零测试,而 `story-001` 标 `Complete`、7 个 AC 勾选框 0/7 已勾** | **本轮不代修** —— 修它要动已 `Complete` 的 `story-001`,属载体裁定面。已登记于 `TD-persist-03`「相邻发现」栏,须另立评审项 |

> ⚠️ **R-2 是本仓抓过的第五个假绿形态的同型复现**(用一句自述顶替判据)。
> `save_service_test.cs:96-98` 唯一挂 `AC-7a-01` 注释的 `test_checksumFallback_readsBakWhenFinalCorrupted`
> **只测 `bak` 回退**;`AC-7a-02` **零注释命中**。**S19-1 不得声称这两条 AC 已满足。**

#### S19-1 的裁定前提 · ⑧ 段(2026-10-24 · `PRE-S19-A` 已结)

> **用户裁定:甲案 —— 注入委托,不开 ADR。** `Func<bool> narrativeContext` 走 `SaveService`
> 构造注入,与 `IFoldedPredicate`(S18-2)/ 重放委托(`PRE-S19-D`)**同一先例**:
> 零新增 `Sim.Contracts` 成员、零新增装配边、不开 ADR。**未注入时 fail-closed**(拒绝 + 抛具名异常)。

**⚠️ 登记项本身的两处实测错误(原文不删,就地加注)**:

| # | 登记项原文 | 实测 | 后果 |
|---|---|---|---|
| **E-1** | 「G-4 的**两个**上下文查询(叙事上下文 / 未决医疗动作)无归属系统」 | **实为一个量**。GDD `:212` / `:219` 逐字「与手动存**共用同一观察钩子**」;7b `F-7b.2` 两式(`save-slot-ui.md:206-207`)只出现**一个** `InNarrativeContext(t)`;`:213` 明注「**= 载入安全上下文门同钩**」 | 注入**一个** `Func<bool>` 即够,**不得实现成两个正交查询**(见 ⑧c) |
| **E-2** | (无此条) | **真正缺的东西连登记都没有** —— `AC-7a-20`(事件触发 checkpoint)全仓零载体 | 已立 `TD-persist-04`;本件**不得声称 `AC-7a-20` 已满足**(见 ⑧e) |

**三个候选驱动源,逐个否决(实测落点 + 否决理由)**:

| 候选 | 落点 | 否决理由 |
|---|---|---|
| `IModalState` | `unity/Assets/Gameplay.UI/Skeuomorphic/IModalState.cs:12` | 住 `Gameplay.UI`;7a 是 `Sim.Codec`,**够不着**。且方向反了 —— UI 是呈现层,7a 不该反向依赖它 |
| `MotorLease` | `unity/Assets/Gameplay.Presentation/Player/MotorLease.cs:32` | 同上,住**表现层** |
| `ModalPhaseResult.MotorSuppressed` | `unity/Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs:24` | 是**方法返回值**,不是**可持续查询的状态** ⇒ 供不出 `InNarrativeContext(t)` |

**两条本件约束(与 ⑦ 段并列)**:

| # | 约束 | 判据 |
|---|---|---|
| ⑤ | **fail-closed** | 未注入驱动源时 `Checkpoint` / `Load` **拒绝并抛具名异常**。**禁 fail-open** —— 恒真 = save-scum 通道在生产路径默开,而测试全绿(假绿入口) |
| ⑥ | `InNarrativeContext` **是一个量** | 构造形参 **3 → 5**(`folded` / `narrativeContext` / `replaySink` 三个可选形参,均可 null)⇒ `folded_predicate_test.cs:590` 的 `Assert.AreEqual(3, ps.Length, …)` **必红,须同批改而非删**。🔁 **2026-10-24 订正为 3 → 6**(加 `compose`;`folded` 仍保持第 3 位,`ps[2]` 类型断言不受影响) |

**两条残余(不阻塞本件,须如实登记)**:

| ID | 内容 | 处置 |
|---|---|---|
| **R-A1** | **真实驱动源零落点** —— 谁在锚点事件(脉案落笔 / 病例结案 / 出诊启动)时调它,归 8 / 37 / 52 的实现轮 | 已立 `TD-persist-04`(含 `AC-7a-20` 零载体的事实) |
| **R-A2** | **fail-closed 意味着本 sprint 之后存档 / 读档在生产路径上不可用**,直到驱动源接通 | 这是**刻意的**:不可利用 > 不可用。须在 sprint 收口时如实登记,**不得读成「功能已完成」** |

> ⚠️ **⑧ 段的 AC 口径**:写成「**门能被驱动**」而非「**事实为真**」。
> 7a 只**定义门**,不**定义被门观察的事实** —— 后者属于 8 / 10 / 37(同 ADR-010 §三「义务汇总表」定位)。
> ⇒ 本件可证的是「注入委托后门按 `InNarrativeContext` 的布尔值放行 / 拒绝」,
> **不可证**的是「锚点事件发生时该布尔值真会变 true」。

#### S19-1 的裁定前提 · 折叠接线形态(2026-10-24 · 三家专家合成)

> **出处原件**:`production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md`
> (三家 = `technical-director` / `unity-specialist` / `lead-programmer`;草稿已合入,原件含 12 处自核清单)
> **用户裁定两项**:① 判据 ④ 的**字节半推迟并入 `TD-persist-03`**;② 写裁定原件 + 改判据。

**接线形态 = 甲-a(`Func<SaveSlot, byte[]> compose` 注入)**,不取另两个「第五案」:

| 案 | 提出方 | 否决 / 采纳理由 |
|---|---|---|
| **戊案** | technical-director | **否** —— `HistoryFolder`+`FoldRow`+`FoldResult` 全搬 `Sim.Codec`,须 ADR-025 §① 窄修订;而 `adr-025:112` **已明文记过相反结论**(「折叠动作与折叠行,**不进本表** —— 它们是 `Sim` 内部类型」)⇒ 是**已有相反记录**,不是未授权 |
| **第五案** | unity-specialist | **否** —— 只搬 `FoldRow`+`FoldResult` 到 `Sim.Contracts`,把**数据 DTO** 塞进契约集,比甲-a 多两处搬动、多一次契约面扩张 |
| **甲-a** | lead-programmer | ✅ **取** —— 委托返回 **`byte[]`(BCL)**,`SaveService` 永不命名 `FoldRow`;折叠+组版+编码全在 `Gameplay.Boot` 闭包内;零 ADR 修订 / 零装配边 / 零 `Sim.Contracts` 新增成员 |

**为何 TD 的「甲案不可实现」不推翻甲-a**:该结论只覆盖「委托返回 `FoldResult`」这一种形态
(`FoldResult` 住 `Sim` ⇒ `Sim.Codec` 不可命名),**不覆盖返回 `byte[]` 的委托**。

**判据 ④ 的三段拆分(订正后)**:

| 段 | 判据 | 状态 |
|---|---|---|
| **位置半** | `grep -rn "HistoryFolder\.Fold" unity/Assets --include=*.cs \| grep -v Tests/` ⇒ ≥1 行,**且不在 `Sim.Codec/` 内** | 可兑现 |
| **委托半** | `SaveService.cs` 内 `_compose(` 落在 `Checkpoint` 方法体内;未注入 ⇒ fail-closed 抛具名异常 | 可兑现 |
| **字节半** | 「`Checkpoint` 产出的字节里折叠行出现」 | **`NOT-RUN`** ⇒ 并入 `TD-persist-03` |

**字节半为何是 `NOT-RUN` 而非「没写」**(六条实测,原件 §三):
`SaveHeader` 七字段**无折叠段偏移** · `Sim.Codec` **零 `FoldRow` 编码器** ·
**全库无任何存档体生产器** · `SimEventCodec.Decode` **只解单事件头部**(无列表容器) ·
`CodecWriter` 数组写只有**定长** `WriteFieldArrayInt32/Int64` · `SaveHeader` **无读回方法**。
⇒ **存档体的字节布局从未被造出来**,与 `TD-persist-03` 实测 ③ 登记的单列表 vs「头部 + 三流」形状差
**是同一个洞**。

**同批订正的三处判据**(原文不删,已就地加注):

| # | 原措辞 | 订正 | 依据 |
|---|---|---|---|
| **③** | 「反射查**该类型**无可变实例字段」 | 判据对象 = `SlotProjections`(静态)+ `SlotContext`(readonly);`SaveSlotLedger` **排除**,另配四例状态迁移夹具 | `next_writable_seq` / `Locked` 按 `save-slot-ui.md:212-214` 归 7a **且只进不退**(`persistence-service.md:203-206`)⇒ 跨调用状态,不可能是纯函数;但判据对象是**两个函数**,不是它们读的量 |
| **⑥** | 「构造形参 3 → 5」 | **3 → 6**,顺序 `(basePath, codec, folded, narrativeContext, replaySink, compose)`;`folded` 保持第 3 位 ⇒ `ps[2]` 类型断言仍成立,只改计数 | 原算术只覆盖「两门 + 重放」,**漏算 `Checkpoint` 的数据源** |
| **⑦** | 「头部解析」/「字节 → 列表」 | 补:两者**均须新写**(`SaveHeader` 无读回;无列表容器编解码器) | 见原件 §三 B-4 / B-6 |

> ⚠️ **判例登记(乙案 = 无声漂移)**:给 `Sim.Codec` 加 `Sim` 引用**不撞任何机械门** ——
> b2 的白名单块被 `AssemblyGates.cs:214` 的 `if (asmName == "Sim")` 守卫、只对 `Sim` 生效;
> b3 只比 asmdef 的 `name` 字段集合、**从不读 `references`**;`AC-10-02` 门扫的是装配 "3";
> 全仓**零测试**断言 `Sim.Codec` 的引用集。
> ⇒ 乙案的唯一拦截是 **ADR-025 §① 的文本**,不是执行体。**门绿 ≠ ADR 合规。**

| ID | 任务 | 层/型 | C | 依赖 | 验收判据(可复算) | 测试载体 |
|---|---|---|---|---|---|---|
| **S19-2** | **G-5**(`TD-player-02`):`IPlayerMotor` 契约 + `MoveInput` 类型 + `PlayerController` 实现 | Presentation / Logic | **M** | ADR-020 §Key Interfaces(**签名齐备**) | ① 接口四成员齐:`Tick(float, in MoveInput)` / `WorldPos Cell` / `Vector3 Position` / **`bool ConsumeCellChanged()`(无出参 —— `PRE-S19-C` 已裁)**;② `PlayerController` 实现之,**不得新增第二个位移真源**(既有 `GetCell` / `Teleport` 语义不变);③ 夹具:**跨格时 `ConsumeCellChanged` 恰好触发一次** —— 不进格不触发 / 原地抖动不重复 / 连跨两格的两条各一次;④ `IPlayerMotor` 与 `ICameraRig` 同在 `Gameplay.Presentation`(ADR-025 §② 落位) | `unity/Assets/Tests/EditMode/PlayerController/player_motor_test.cs`(新) |
| **S19-3** | **G-6**(`TD-enemy-01`):`LogiPose` + `ITameable` / `TameState` 形状 + 接 27 逐 tick 整数推进 | Core / Logic | **M** | ① ADR-016 §二/§九 ② `EnemyPathing.StepAccumulator`(**在盘**,`:181`)③ `EnemyStateMachine`(**在盘**) | ① `LogiPose = (WorldPos Cell, byte Facing)` 落 `Sim.Contracts`(或 `Sim` —— 须在实施前裁定并写明);② **禁从表现层 `Transform` 反推逻辑格** —— 源码 + 反射双判(ADR-016 §二「反向不可信」);③ `ITameable` / `TameState` **只落形状**(P0 不要驯化行为,ADR-016 §七);④ 夹具证「**决策不进流、效果进流**」(ADR-016 §一 三源不变量的可证伪形态) | `unity/Assets/Tests/EditMode/EnemyAI/logi_pose_test.cs`(新) |
| **S19-4** | **G-7**(`TD-randomevents-01`):`DeferredThreatSlot` 流态载体 + 两 `Kind` 读写 + **删 `ValidateReconstructable` 假断言** | Core / Logic | **M** | ① ADR-007 §三/§四 ② `ThreatDeferredPayload` / `ThreatDeferralClearedPayload`(**在盘**,`Sim.Contracts/Payloads/HistoryPayloads.cs:323/:335`)③ `EventBudget.BudgetState`(**在盘**,内存态) | ① `DeferSlots` 由 `List<int>` 改为**从流重构**(不保留第二真源);② `EventBudget.ValidateReconstructable` 的「验证非空」**删除并换成真判据**(`file:line` = `Sim/EventBudget.cs:137-142`);③ `Editor.Tools.Gates/WriterExistenceGate.cs:130-131` 两条豁免(`ThreatDeferred` / `ThreatDeferralCleared`)**同批撤除**;④ 夹具:**主机迁移后延迟威胁不丢不重**(重放同一段流两次,`DeferSlots` 逐元素相同) | `unity/Assets/Tests/EditMode/RandomEvents/deferred_threat_slot_test.cs`(新) |

#### S19-2 的裁定前提(2026-10-24 · `PRE-S19-C` 已结)

> **用户裁定:取「只读边沿」。** `ConsumeCellChanged` **不夺** `OnTickEdge` 的 Append 主路径,
> 只观察**已提交**的边沿。签名冲突一并解除 ⇒ **`bool ConsumeCellChanged();`(无出参)**,
> 与 4 份架构件一致(`adr-020:401` / `architecture.md:529` / `architecture.md:890` / `architecture.yaml:135`);
> 债册 `:1625` 的 `(out WorldPos)` 记作**笔误**,已就地加注(原文不删)。

**三条实现形态硬约束**(与上表判据 ①②③ 并列,均可在实施期机械核):

| # | 约束 | 判据 |
|---|---|---|
| ① | 边沿走**独立锁存器**,只在 `PlayerController.cs:319`(`_lastCommittedCell = cell`)**之后**置位 | `_hasPending` 写点集合保持 `{:145, :152, :170, :177, :309}` **五处不变**;锁存器与 `_hasPending` / `_pendingCell` **完全不相交** |
| ② | `Cell` 走 `CellFromPosition`(**FloorToInt**),**不得**用 `GetCell()`(**RoundToInt**,`:354-362`) | 两者在负坐标 / 半格处取值不同 ⇒ 与 `ActorCellEnteredPayload.Cell` 同源者只能是前者 |
| ③ | **只落形状,不发明消费者** —— 今天零消费者(2 的 `story-003:64` 只读 `Position`) | GDD `player-controller-and-movement.md:128` 那句须读作**因果绑定**(Append 是因,边沿是果)**而非调用次序**;该读法须在实施期加注 |

**正确形态(伪代码,`S19-2` 按此实现)**:

```
OnTickEdge(tick):
    ...                                  // :302-316 原样不动
    AppendCellEnteredEvent(cell, tick)   // :317 —— 因
    _lastCommittedCell = cell            // :319
    _cellEdgeLatched = true              // ← 新增,只在此处置位

bool ConsumeCellChanged():
    if (!_cellEdgeLatched) return false;
    _cellEdgeLatched = false;
    return true;
```

**三条残余(不阻塞 S19-2,须在实现期如实登记)**:

| ID | 内容 | 处置 |
|---|---|---|
| **C-1** | 客户端模式永不产生边沿(`:305` Client 直接 return ⇒ `_lastCommittedCell` 永不推进;`OnUplinkSample` 对它零赋值,且生产侧零调用点 —— `BootRoot.cs:744` 自陈是设计) | **正确且必须有**(客户端不 Append ⇒ 无提交 ⇒ 无边沿)。上行链归 **P1b / 45**;S19-2 **不得**声称覆盖联机 |
| **C-2** | `ConsumeCellChanged` 今天零消费者(`grep -rn 'ConsumeCellChanged' design/ production/epics/` ⇒ 1 命中 = GDD `:128` 自己) | 只落形状,不发明消费者 |
| **C-3** | `PlayerController.cs:113 _cellDetector` 死字段(零赋值零使用) | ✅ **已裁:删死字段**(改动面 1 行,不动既有测试);生产内联算子与 `CellTransitionDetector` 的**重复**另立 TD 归后续轮 |

**批 3 —— `S19-5` 记账**

| ID | 任务 | 层/型 | C | 依赖 | 验收判据(可复算) | 测试载体 |
|---|---|---|---|---|---|---|
| **S19-5** | 记账订正:`TD-sim-01` 过期描述 + 路线图 §6.5 sprint-19 行 Exit 订正 + 四门结案回写 | 记账 | **S** | 无 | ① 债册 `TD-sim-01` 的「生产侧零构造点」句**就地加注**(原串仍可 grep 到 **且** 加注串同时命中);② 路线图 `:910` 的 Exit 由「G-4 一条」改为「G-4 + `TD-persist-01` 剩余两项」(**原文不删**);③ 四门(G-4…G-7)各自的 §三行按 §6.4 回写义务划掉或加注;④ 每处附**可跑命令 + 实跑输出** | — |

### Should Have

**无** —— L 独占 1 批 + 一批 3 件已满「每批 2–4 件 · 并发 ≤ 3」的额度。
追加件按实测追加率 `[0%, 57%]` 从预留额度承接,**规划期不预设**。

### Nice to Have

**无** —— 同 Should Have。

---

## Carryover

**零结转。**

- sprint-18 的 `S18-1`(原定结转)已于 **2026-10-22 用户改判拉回并在 sprint-18 内 `done`** ⇒ 结转作废。
- `retro-18` 的五条行动项:四条已落地,一条(第 4 条)因 S18-1 拉回而**失去对象**。逐条见 §Phase 1 附带表。
- **`TD-ci-01`**(CI 常驻矩阵)= **EXTERNAL**,`P0 面 = N`,`open` 但**不减少 M3 分母**;
  用户 2026-10-21 已裁定「不做,登记」。⇒ **不计入本 sprint 件数,不阻断 Exit。**

---

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **G-4 的两个上下文查询无归属系统** —— 「叙事上下文」「未决医疗动作」在代码里**零落点**,而它们的事实属于 8 / 10 / 37 | **确定** | **高** | 本件**只注入抽象 + 假件**,真实实现归各系统实现轮。AC 判据写成「门能被驱动」而非「事实为真」;**不得**在 7a 内自建状态源 |
| **G-7 改动会碰到 52 已 `Complete` 的 `story-004`**(整条 story 名即 `deferred-threat-slot`) | **确定** | 中 | 承 **TD 工作项载体裁定**(2026-10-20):**不立 story、不重开 epic** —— 补立会把 epic 计数从「全绿」打回「未全绿」并扰动 M3 分母 |
| ~~**`ValidateReconstructable` 的删除会打断既有调用方**~~ | ~~中~~ | ~~中~~ | ✅ **已核(2026-10-24 规划轮)**:全树**零调用点**(`grep -rn 'ValidateReconstructable' unity/Assets --include='*.cs'` ⇒ **1 行**,即声明本身)。⇒ 风险解除,但**换实现而非删方法的判据仍保留**(将来若有调用方接入,不得因本件而失效) |
| **三件 M 同时写 `Sim.Contracts`** | 中 | 中 | 逐件**分文件**(`IPlayerMotor.cs` / `LogiPose.cs` / `DeferredThreatSlot.cs`),并发 ≤3 且**文件不相交**;`S19-2` 落 `Gameplay.Presentation` 不触 `Sim.Contracts` |
| **借绿** —— 四件全 Logic,且 G-5/G-6 的判据天然易滑向「造一个类型就算落地」 | 中 | **高** | 每件过**证伪轮 ≥3**(改坏生产码 ⇒ 指定断言转红);`exactRed` **先论证后定预期**,禁把实得抄进 `expectRed`;`Proven` 列填可复算判据,**禁写动作描述** |
| **「门已清」的自述无 `file:line`** | 中 | 中 | 承 `coding-standards.md` §Review Evidence Standards:缺 `file:line` + 可跑命令的「有门」自述**一律按 `NOT-RUN` 登记** |
| ADR-012 IL2CPP 矩阵 | **确定** | 低 | 本批零跨平台新增面;矩阵级,非本件个体判据。⚠️ **CI 常驻 job 仍开**(`TD-ci-01`,EXTERNAL) |

---

## Dependencies on External Factors

- **无外部阻塞** —— 四门的上游契约全部在盘(ADR 已 Accepted、payload 类型已在、`EnemyPathing` / `HistoryFolder` / `IFoldedPredicate` 已在)。
- ⚠️ **`TD-ci-01` 仍 EXTERNAL**(CI 常驻 job 缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`)——
  与本 sprint **零交集**,不阻塞;`P0 面 = N` ⇒ 不计入 M3 分母。

---

## Definition of Done for this Sprint

- [x] **`S19-1` … `S19-5` 的 AC 逐条勾选并附可复算判据**(禁止「已落盘」当判据)
      > 🔁 **2026-10-25(S19-5 记账)勾选**:判定载体 = 四份证据件(`impl-td-persist-03-…` 含 S19-1 面 / `impl-s19-2` / `impl-s19-3` / `impl-s19-4`,各含 AC 判据表 + 复算命令)+ S19-5 本件六处加注(命令与实跑输出就地贴)。`[x]` 指「每条 AC 有明确判定 + 判据」,非「全部满足」。
      > **口径(承 sprint-18 同款)**:本条要的是「每条 AC 都有明确判定 + 判据」,
      > **不要求每条都 ✅**。表内出现「⚠️ 不勾 + 具名理由」的行,**不违反**本条;
      > 但 `[x]` **不得**被读成「全部满足」。
- [x] 四门各自的**证伪轮 ≥3 轮**,且每轮记录「变异 → 红集 → 还原 `sha256sum -c` 逐字节相同」
      > 🔁 **2026-10-25(S19-5 记账)勾选**:TD-persist-03 反证(S19-1 面)· S19-2 F1–F3(3 轮)· S19-3 M1'–M8(8 轮)· S19-4 M1–M9(9 轮)—— 均同命令内改→跑→还原,SHA-256 逐字节比对于各证据件 §证伪。
      (禁用 `git checkout` / `git stash` 还原;变异窗口内**不读**被变异文件)
- [x] 每件「有门 / 有断言 / 有校验」的自述**带 `file:line` + 一条能证明它真的会红的命令**(缺一按 `NOT-RUN` 登记)
      > 🔁 **2026-10-25(S19-5 记账)勾选**:四件的自述门均以证伪轮红集为「会红」证据(变异表逐轮列测试名),`file:line` 见各证据件 §改动清单;未跑出的门一律 NOT-RUN 登记(如 S19-1 字节半的 e2e 曾 NOT-RUN,后由 TD-persist-03 兑现)。
- [x] 回归三元组齐备(**截至时刻 · 批次 · commit**),并附**全名级差集**(不只比总数)
      > 🔁 **2026-10-25(S19-5 记账)勾选**:四件齐;末基线 = `4867/4832/0/35` @ 2026-10-25 07:19 CST · `s194` 全量 · `6198671`,差集 +21/−0(S19-4 件 §三)。
- [x] 债册 `TD-INDEX` 复算:`未闭 P0 面 74 → 70`(**预期值,实跑为准**;禁写未跑数)〔🔁 **2026-10-24 实测作废**:基线已由 `74` 变 **`76`**(两条新 P0 面债入表),且原 `→ 70` 的算术**本身错**(按「四门各 1 条」算,漏了 `S19-1` 的两债合一)⇒ 现推算 = **`76 → 71`**。**收口时须实跑**,两数皆不得照抄〕
      > 🔁 **2026-10-25(S19-5 记账)勾选——实跑 = 71,与推算恰合**:§五 权威脚本本轮实跑输出 = `条目 104 | closed 27 | open 70`,未闭 P0 面 **71**(已闭但曾标 P0 面 22 条不计),分布 **11/31** epic(`random-events` 行清空)。⚠️ 顺手订正:十九订「条目 105」系笔误,权威实测 104(二十订就地订正)。
      > 🔴 **2026-10-25(QA 收口轮)就地订正 —— 上面那个 71 已失效,原文不删**:用户裁定 C-6
      > 「都入债册」后新立 **`TD-enemy-02`**(`enemy-ai` · `open` · P0 面 `Y`)⇒ §五 权威脚本**当场复跑**:
      > ```
      > 条目 105 | 未闭 P0 面 = 72
      > M3 Exit 第二条合取 ⇒ 假 —— 72 条,分布在 12/31 个 epic
      > ```
      > ⇒ **本 sprint 的净变化 = `76 → 72`(降 4)**,不是 `76 → 71`。
      > **差的 1 不是漏算,是本轮新立的债** —— 四门清偿 −4,同时 C-6 新立 +1。
      > ⚠️ 这正是「数字须带可跑复算命令」要防的形态:71 在 S19-5 记账当刻是**真值**,
      > 但同一 sprint 后续的裁定改变了分母,**旧数不能当现值**。三处引用(计划 / baseline / 签核)
      > 凡写 71 的,均须读作「@ 2026-10-25 S19-5 记账时刻」。
      > **复算命令**(自包含,当场可跑):见下方「溯源」节。
      > ⚠️ **本行是预期不是结论** —— 收口时若实跑 ≠ 70,**以实跑为准并写明差在哪**。
      > 🔁 **2026-10-24 就地订正(原文不删)**:本行写于 sprint 规划时,基线 `74` 现**已失效** ——
      > 入场阻塞项裁定轮新立两条 P0 面债(`PRE-S19-D` ⇒ `TD-persist-03`;`PRE-S19-A` ⇒ `TD-persist-04`),
      > 实跑十二订 = **`未闭 P0 面 = 76`**(`条目 102`)。
      > ⇒ **起点改为 76**;`70` 这个终值**须在收口时按实际清偿条数重算**,**不得**沿用。
      > 复算命令同上节 §五(自包含);本条**不预设**收口终值(禁写未跑数)。
- [x] 路线图 **M3 Exit 第二条合取**由 `NOT-YET-EVALUABLE` 转**可机械求值**
      > 🔁 **2026-10-25(S19-5 记账)勾选**:求值器 = 债册 §五 脚本本体(自包含),本轮实跑 = **假 —— 71 条,分布在 11/31 个 epic**(四门清偿不改变全局合取,门面与全局已分离,见路线图 §6.5 sprint-19 行同日加注)。
      > 🔴 **2026-10-25(收口轮)就地订正 —— 上面那个 71 已失效,原文不删**:C-6 裁定「都入债册」
      > 新立 `TD-enemy-02` ⇒ 当场复跑 §五 脚本:
      > ```
      > 条目 105 | 未闭 P0 面 = 72
      > M3 Exit 第二条合取 ⇒ 假 —— 72 条,分布在 12/31 个 epic
      > ```
      > ⇒ **现值 = `72 条 / 12 epic`**,`71 / 11 epic` 读作「@ 2026-10-25 二十订时刻」。
      > ⚠️ **本条 `[x]` 不撤销** —— 它判的是「由 `NOT-YET-EVALUABLE` 转**可机械求值**」,
      > 而求值结果(假)本来就不是绿;**结论方向未变,只是量从 71 升到 72**。
      > 复算命令 = 上方 §五 脚本本体(自包含)。
- [x] QA plan 存在(`production/qa/qa-plan-sprint-19-*.md`)
      > 🔁 **2026-10-25(QA 轮)勾选**:`production/qa/qa-plan-sprint-19-2026-10-24.md`(规划期 2026-10-24 落盘;本轮追加 §实收对账 + 判据⑦形状订正)。
      > 复算:`wc -l production/qa/qa-plan-sprint-19-2026-10-24.md` ⇒ 实跑 **673** @ 2026-10-25 08:21。
      > S19-5 记账时误留不勾 —— 该件当时已存在,本轮复核确认已满足。
- [x] QA 签核报告 **APPROVED** 或 **APPROVED WITH CONDITIONS**(`/team-qa sprint`)
      > 🔁 **2026-10-25(QA 轮)勾选**:`production/qa/qa-signoff-sprint-19-2026-10-25.md`
      > Verdict = **APPROVED WITH CONDITIONS**(满足本条「或」分支)。C-1…C-6 处置见该件 §八:
      > **4 已处置**(C-1 / C-3 / C-4 / C-6)· **2 如实登记未满足**(C-2 归 10 的裁定结果、C-5 不可复验)。
      > ⚠️ **不转 APPROVED** —— C-2(`TD-persist-04`)须等 10 的实现轮,不在本 sprint 能力面内。
- [x] 无 S1 / S2 bug
      > 🔁 **2026-10-25(QA 轮)勾选**:`ls production/qa/bugs/BUG-*.md` ⇒ 无命中(零已立案 bug)。
      > 复算:`ls production/qa/bugs/` ⇒ 仅 `README.md`。
      > ⚠️ **但本轮的缺陷没有走 bug 通道** —— S19-2 / S19-3 两条重复实现已按用户裁定入债册
      > (`TD-enemy-02` 等),属技术债而非 bug,不阻塞本条。
- [x] 记账件全部落盘,且**原文不删、就地加注**
      > 🔁 **2026-10-25(S19-5)勾选**:六处加注全走就地注 —— 债册 `TD-sim-01` 实测栏 + G-1 门行(`S19-5 记账订正`串 ×2,原串仍 grep 到 ×2)· 路线图 §6.5 sprint-19 行 + §3 四门行 + §3.4 表节注。
- [x] `/code-review` 结账表用**双列**(`Landed` / `Proven`)+ `Branch(分支覆盖)` 列
      > 🔁 **2026-10-25(评审修复轮)勾选**:原件 `production/qa/evidence/review-sprint19-2026-10-25.md`
      > §一 双列结账表(**Branch** 列亦在)+ §七 修复轮双列结账表。
      > **三条必修(W1 / W4 / W5)双列皆 ✅**;W6–W10 未修,`Landed ✗ / Proven ✗` **如实登记**。
      > 证伪轮:`tools/falsify/specs/s19-review-fix-w1w4w5.json` **四条变异实跑(2026-10-25 重跑)**,
      > **4/4 全部恰如预期**(`══ 证伪轮通过:4 个变异全部恰如预期 ══`);
      > 还原 SHA-256 逐字节校验通过 + 受保护路径指纹未变。
      > ⚠️ **W5 拆两条**(用户 2026-10-25 追问后补):`W5a` 守接线 fail-closed(今天的安全属性)·
      > `W5b` 守纯函数 Kind 判定(留待 10 复用的预备件)。**两者不可互替** ——
      > 「留待 10」≠「不需证伪」,要复用的算子更须被钉住。首版只留 a 是漏做,已补。
      > 连带红归因(W5a 3 / W5b 3 / W4 3)见原件 §七.1b。

---

## QA Plan

**✅ 已落盘(2026-10-24)**:`production/qa/qa-plan-sprint-19-2026-10-24.md`(497 行)。
> 🔁 **2026-10-25 行数订正(原文不删)**:上记 `497 行` 是**落盘当刻**的值;此后 2026-10-24 裁定轮
> (追加 `PRE-S19-A/C/D` 三处入场阻塞裁定 + 七处地雷表)与本 QA 轮(实收对账 + 判据⑦订正)持续增员。
> 复算:`wc -l production/qa/qa-plan-sprint-19-2026-10-24.md` ⇒ 实跑 **673** @ 2026-10-25 08:21。
> ⚠️ 引用行数时须带 `@ 时刻`,否则是下一份「陈旧数字被当现值」。
**用户裁定(2026-10-24)**:**先跑 `/qa-plan sprint`**,先于任何实施 —— 已执行。
**落盘范围裁定(2026-10-24)**:取「**只写计划文件**」—— 五件是 TD 工作项、不是 story,
**无 `## QA Test Cases` 载体**;强行回填 = 新造一节,与「TD 载体裁定:不立 story」冲突。
⇒ 每件的测试规格**只在该 QA plan**,`/dev-story` 实施时须回来读。

**该件对规划件的订正(实施前须读)**:

| # | 订正 | 影响 |
|---|---|---|
| **分类** | 四件实现的类型**不是全 Logic** —— `S19-1` 与 `S19-4` 按 skill 的「含糊取 Integration」口径归 **Integration**(存档往返 / 主机迁移后从流重构);`S19-2` / `S19-3` = Logic;`S19-5` = Config/Data | 证据要求不变(仍落 `EditMode/` 真装配件),但 `/story-done` 的证据门口径按此 |
| **新增两处入场阻塞** | `PRE-S19-C`(`ConsumeCellChanged` **4 : 1** 签名冲突 + 与 `OnTickEdge` 的消费语义未裁,阻塞 S19-2)· `PRE-S19-D`(`Load` 的后两段零实现,阻塞 S19-1) | 已同步进 `sprint-status.yaml` 的 `open_items`;**两处均已于 2026-10-24 裁定结案** —— `PRE-S19-C` 取「只读边沿」(见 §S19-2 的裁定前提);`PRE-S19-D` 取「甲案 3/4 段」+ 新立 `TD-persist-03`(见 §S19-1 的裁定前提) |
| **新增三处假绿** | `event_budget_defer_test.cs` 的 `test_deferSlotBounded`(空断言)· `test_dwellNotInBudget`(恒真)· AC-52-30 编号张冠李戴 —— 均在 **S19-4** 的必做面 | S19-4 的件量按此上调(改既有 3 条 + 新 16 条) |
| **🔁 2026-10-24 追加:第三处入场阻塞** | `PRE-S19-A`(G-4 的上下文查询无归属系统,阻塞 S19-1)—— **原文不删**:本表初稿只记了 C / D 两处,A 此前已 `status: open` 但未进本表 | 已于 2026-10-24 裁定结案:**甲案 注入委托 + fail-closed**(见 §S19-1 的裁定前提 · ⑧ 段);新立 `TD-persist-04`。<br>⚠️ **该登记项本身两处实测错误**:(E-1)「两个查询」**实为一个量**;(E-2)**真正缺的 `AC-7a-20` 连登记都没有** |

> ⚠️ **本件 §「四件均为 Logic 型」的原表述已订正**(原文不删,留作闭环记录)——
> 规划期未按 `qa-plan` skill 的 Integration 判据核过,QA 轮实测两件落 Integration 面。

---

## 溯源

| 项 | 出处 |
|---|---|
| 件集登记 | 路线图 §6.5:**910**(`G-4 · G-5 · G-6 · G-7`,经 §Phase 1 ① 订正为 **+ `TD-persist-01` 剩余两项**) |
| G-4 载体 | 债册 §六 `TD-persist-02` · `persistence-service` · `open` · P0 面 `Y` |
| G-2 残留(并入 S19-1) | 债册 §六 `TD-persist-01` · `persistence-service` · `open` · P0 面 `Y` · 剩余两项见 `sprint-18.md` §九.2 |
| G-5 载体 | 债册 §六 `TD-player-02` · `player-controller` · `open` · P0 面 `Y` |
| G-6 载体 | 债册 §六 `TD-enemy-01` · `enemy-ai` · `open` · P0 面 `Y` |
| G-7 载体 | 债册 §六 `TD-randomevents-01` · `random-events` · `open` · P0 面 `Y` |
| AC 门语义 | 路线图 §3.1:513(「已标 `Complete` 的 epic 的 AC —— 这些 epic 零剩余 story」) |
| 回写义务 | 路线图 §6.4(「某道 G-x 门清掉 ⇒ §3 划掉 + 从 §4 移除对应阻塞」) |
| TD 载体裁定 | 债册 §六(2026-10-20 用户裁定:门的清偿载体 = **TD 工作项**,不立 story、不重开 epic) |

### 分母现值(可跑复算,禁抄)

```bash
python3 - <<'PYEVAL'
import re, collections
txt = open('docs/tech-debt-register.md', encoding='utf-8').read()
blk = re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->', txt, re.S).group(1)
rows = [[c.strip() for c in l.strip().strip('|').split('|')]
        for l in blk.split('\n') if l.startswith('| `TD-')]
st = collections.Counter(r[2] for r in rows)
p  = collections.Counter(r[3].strip('*') for r in rows)
live = [r for r in rows if r[2] != 'closed' and r[3].strip('*') == 'Y']
ep = collections.Counter(r[1].strip('`') for r in live)
print('条目', len(rows), '| 状态', dict(st), '| P0 面', dict(p))
print('未闭 P0 面 =', len(live))
print('涉及 epic 数 =', len(ep))
PYEVAL
```

**实跑输出(2026-10-24 规划轮)**:

```
条目 100 | 状态 {'open': 73, 'partial': 6, 'closed': 20, '**done**(2026-10-21)': 1} | P0 面 {'Y': 89, 'N': 10, '~~**Y**~~': 1}
未闭 P0 面 = 74
涉及 epic 数 = 13
```

> 🔁 **2026-10-24 追加实跑(原文不删)** —— 入场阻塞项裁定轮新立两条 P0 面债
> (`PRE-S19-D` ⇒ `TD-persist-03`;`PRE-S19-A` ⇒ `TD-persist-04`),同一脚本复跑结果:
>
> ```
> 条目 102 | 状态 {'open': 75, 'partial': 6, 'closed': 20, '**done**(2026-10-21)': 1} | P0 面 {'Y': 91, 'N': 10, '~~**Y**~~': 1}
> 未闭 P0 面 = 76
> 涉及 epic 数 = 13
> ```
>
> ⇒ **`persistence-service` 一户 2 → 4**;其余十二户一位不动。
> ⚠️ 上方规划轮块是**当时的真值**(不是笔误),**不得**用它当本 sprint 起点;
> 起点取本节追加块的 **76**。

**四门归属 epic 的未闭 P0 面现值(逐户)**:

| epic | 未闭 P0 面 | 明细 |
|---|---|---|
| `persistence-service` | **4**(🔁 2026-10-24 由 **2** 上调) | `TD-persist-01` · `TD-persist-02` · **`TD-persist-03`** · **`TD-persist-04`** |
| `player-controller` | **1** | `TD-player-02` |
| `enemy-ai` | **1** | `TD-enemy-01` |
| `random-events` | **1** | `TD-randomevents-01` |
| `save-slot-ui` | **0** | ✅ 已达标(其 BLOCKED 由 G-4 解除) |
| `camera-viewpoint` | **0** | ✅ 已达标(其只读上游由 G-5 补齐) |

⇒ **本 sprint 若清偿四门,六户未全归零** —— `persistence-service` 会剩
**`TD-persist-03`(真重放)+ `TD-persist-04`(AC 载体记账)** 两条,
故全库 `未闭 P0 面 76 → 71`。
⚠️ **终值仍须收口时实跑**,本行是**按当前债册的推算**,禁当结论抄。
> 🔁 **2026-10-24 两次订正(原文不删,逐次留痕)**:
> ① **原句**「六户归零,`74 → 70`」写于**两条新债入表前**,且**算术本身也错** ——
> 它按「四门 × 每门 1 条 = 减 4」算,而 `S19-1` 是**两债合一**(`TD-persist-01` + `TD-persist-02`),
> 实际拟清偿 **5 条**。
> ② **本轮初改**写「`76 → 74`(减 2)」—— **同样错**:把「四门各一条」的旧口径当成对的,
> 只补了新债却没补那条漏算的。**实测复算**(脚本见 §分母现值):
> `persistence-service` 未闭 P0 面 4 条 = `{TD-persist-01, -02, -03, -04}`;
> 全库 76;S19-1…S19-4 拟清 5 条(`-01` `-02` `TD-player-02` `TD-enemy-01` `TD-randomevents-01`)
> ⇒ **`76 − 5 = 71`**。
> ⇒ **`save-slot-ui` / `camera-viewpoint` 两户归零判断不变;`persistence-service` 剩 2 条,不归零。**

### 四门符号重扫(规划轮实测,承 Phase 1 硬前置)

```bash
for s in Checkpoint SaveOnExit Loadable Gating IPlayerMotor LogiPose ITameable TameState DeferredThreatSlot; do
  echo "$s => $(grep -rn "\b$s\b" unity/Assets --include='*.cs' | grep -vE ':[0-9]+: *(//|///|\*)' | wc -l)"
done
```

**实跑输出(2026-10-24)**:

```
Checkpoint => 5          ← ⚠️ 5 处全为无关命中(见下)
SaveOnExit => 0
Loadable => 0
Gating => 0
IPlayerMotor => 0
LogiPose => 0
ITameable => 0
TameState => 0
DeferredThreatSlot => 0
```

> ⚠️ **`Checkpoint` 的 5 处命中全部与 7a 无关**(逐处核过,禁混读 —— 承 `TD-sim-01` 的
> `Curve.SelfLimit` / `TD-randomevents-01` 的「Kind 在册」两次同型陷阱):
> · `Tests/EditMode/InputSystem/overrides_sidecar_test.cs:716` —— 禁止持久化键词表
> · `Tests/EditMode/Tutorial/tutorial_content_validation_test.cs:527` / `:533` —— 教程谓词字面量
> · `Editor.Tools.Bake/TutorialContentSchema.cs:103` —— 同上(常量定义)
> · `Editor.Tools.Bake/TutorialContentBinder.cs:426` —— 注释
> ⇒ **`ISaveService` 的高层面仍是零落点。**

---

## 附:本 sprint 不做什么(边界,防scope 漂移)

- **不做 `TD-ci-01`**(EXTERNAL,P0 面 `N`)。
- **不重开任何已 `Complete` 的 epic** —— 承 TD 工作项载体裁定。
- **不自建 8 / 10 / 37 的状态源** —— G-4 只注入抽象,真实实现归各自系统。
- **不实现 P1a 内容** —— `ITameable` 只落形状(ADR-016 §七 逐字「P0 交付:接口 + 数据字段占位」)。
- **不改任何 ADR 的裁决** —— 四门都是「裁决齐、实现零落点」型,本 sprint 的活是**兑现**,不是**重裁**。
- **不结案 `TD-sim-01`** —— 其结案归属 29 / QA 侧核(见 §Phase 1 ③)。

---

## §九 收口判定(2026-10-25 · 收口轮)

**Verdict: ✅ 已收口**(判据面全绿,残余全部具名登记,无未登记项)。

| 收口要件 | 状态 | 凭据 |
|---|---|---|
| S19-1…S19-5 | 全 `done` | `production/sprint-status.yaml`(5/5;completed 2026-10-25) |
| DoD 12 条 | 全勾,零 `[ ]` | 本文 §Definition of Done `:391-451` |
| smoke-check | PASS 件在 | `production/qa/smoke-2026-10-25-sprint19.md` |
| QA 签核 | **APPROVED WITH CONDITIONS** | `qa-signoff-sprint-19-2026-10-25.md`;C-1…C-6 处置(4 处置 / 2 如实登记) |
| `/code-review` 双列结账 | 已落 | W1/W4/W5 修 ✅;W6–W10 未修**如实登记**(承 retro-19 行动项 4:sprint-20 规划时盘点) |
| 收口提交 | 已落 | `f398cb2`…`7223126`(主链)+ `abc9293`(收敛轮) |
| 债册复算 | 已实跑 | 三十订:`未闭 P0 面 69 / 9/31 epic`(§五 脚本;净 −7) |
| retro | 已落 | `production/retrospectives/retro-sprint-19-2026-10-25.md` |
| CAPACITY-MODEL 三元行 | 已补 | `production/CAPACITY-MODEL.md` 表 19 行(承 retro-18 行动项 3 同款) |

**不记绿项(如实,非缺口)**:① QA 两条治理级条件按「如实登记」处置(非「已满足」);
② W6–W10 五条 code-review 项未修,各自具名;③ sprint-19 行动项 3(收口滞后三连)已由本轮
消解,但机制化(sprint 模板 DoD 尾部收口四件套)归 retro-19 行动项 3,前瞻性,未验证。
