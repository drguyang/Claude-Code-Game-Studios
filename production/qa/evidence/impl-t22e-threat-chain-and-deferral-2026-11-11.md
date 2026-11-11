# T22 批五 —— 威胁档链 + 延迟三支(AC-52-29 ②)证据原件

**日期**:2026-11-11
**批次**:T22 批五(用户「开工 T22 批五」→ 复勘发现**威胁档链在生产里根本不存在** → 用户裁定 **甲 · 全做**)
**债**:`TD-randomevents-08`(`open` → 关闭)

---

## 0. 开工前复勘:债册写的是「零接线」,实测比那更重

`TD-randomevents-08` 的实测栏说「写者与状态载体都在,只是没有生产调用方」。复勘时查出**另一层**:

**判据全部指向批四提交 `f9037b8c`(改动前的状态),可在任何时刻复算**:

```bash
git show f9037b8c:unity/Assets/Gameplay.Boot/CompositionRoot.cs | grep -n "new RandomEventSpawnChain\|tier:"
# ⇒ 只有 :807 一处 new,且 :845 是 `tier: 1`(机会档)—— 威胁档零链
git show f9037b8c:unity/Assets/Gameplay.Boot/RandomEventSpawnChain.cs | grep -c ShouldDefer   # ⇒ 0
git show f9037b8c:unity/Assets/Gameplay.Boot/CompositionRoot.cs | grep -c DeferredThreatSlotWriter  # ⇒ 0
```

| 事实 | 判据(见上方命令) |
|---|---|
| 生产只装配了**机会档**(`tier: 1`)一条链 | `new` 恰一处,`tier: 1` |
| 威胁档(Tier=0)**没有任何链** | 同上 —— `EventTier.Threat` 在改动前的 `CompositionRoot` 零出现 |
| `EventBudget.ShouldDefer` 零生产调用方 | 改动前的链里 `grep -c` = **0** |
| `DeferredThreatSlotWriter` 零 `new` | 改动前的装配根里 `grep -c` = **0** |

⇒ 单接「入槽」没有意义:链本身不跑,`ShouldDefer` 永远不被问到。**这就是为什么本轮取「全做」**
(接威胁档链 + 延迟三支一起),而不是只补一个 `TryDefer` 调用。

---

## 1. 做了什么

| # | 件 | 内容 |
|---|---|---|
| ① | **`Sim/PlayerCellCrossing.cs`(新)** | 把「本 tick 跨格」的判定从 `HouseCallStartedWriter` 的 ②/③ 两步**抽成单一出处**。`TD-randomevents-08` 解除动作 ② 逐字要求「与 `HouseCallStartedWriter` **共用同一次跨格判定**」——照抄会造第二份判据(假绿形态 F8)。 |
| ② | **`HouseCallStartedWriter.cs`(改)** | 改为调 `PlayerCellCrossing.At(...)`,行为零变化。 |
| ③ | **`RandomEventSpawnChain.cs`(改)** | 新增结果 `ThreatDeferred = 9` / `DeferSlotsFull = 10`;新增 `clinicSuppressedThreat` 分支(**排在 `NoQuota` 之前**);空槽预检必须在**掷骰之前**(否则留下「掷过骰、没入槽」的孤儿行);新增 ⑦之三 延后支(落 `EventRolled` + `TryDefer`)。 |
| ④ | **`Gameplay.Boot/DeferredThreatReleaseDriver.cs`(新)** | 三条收尾:**跨日作废** → **离开医馆补发** → **当日预算耗尽 ⇒ 丢弃**。槽集与挂起日**全部从事件流折叠**,零内存日界标志。 |
| ⑤ | **`RandomEventsDirectorDriver.cs`(改)** | 可选 `threatSpawnChain` / `deferredRelease`;`BindPlayer` 把玩家转发给释放驱动;`OnTickEdge` 依次驱动机会链 → 威胁链 → 释放驱动,各自 try/catch。 |
| ⑥ | **`CompositionRoot.cs`(改)** | 构造 `DeferredThreatSlotWriter` + 威胁链(`tier: Threat`)+ 释放驱动,均传入导演。 |

### 1.1 三条设计判断(都是「不这么做会静默坏」)

| 判断 | 不这样的失效面 |
|---|---|
| **延后支排在 `NoQuota` 之前** | 医馆内威胁档配额为 0 ⇒ 先返回 `NoQuota` ⇒ 永远走不到入槽 ⇒ 「延后」被偷换成「丢弃」(正是 AC-52-29 二轮修正点名的形态)。 |
| **空槽在掷骰之前确认** | 先掷后判 ⇒ 流里留下「掷过骰、没入槽、也没降临」的孤儿行,读档重放凭空多一次掷骰(与 ⑥ CAP 预检、⑦之三 病人预检同一条纪律)。 |
| **跨日作废不靠内存日界标志** | 槽的挂起日 = 该 `ThreatDeferred` 事件头的 `Tick / TICKS_PER_DAY` ⇒ 读档后自动正确(ADR-039 D2 同一纪律)。用「记住上一边沿是第几天」的写法会在读档后静默错(同 `TD-sim-05` 那类)。 |

### 1.2 `DEFER_MAX` 的取值

GDD Tuning Knobs `:1176` 明写 `DEFER_MAX` = `*待定*`,值归**用户数值轮**。本轮取
`CompositionRoot.cs` 的 `DeferMaxPlaceholder = 3` —— 它是**接线旋钮,不是裁定值**,
注释里点名了这一点。⚠️ 改它 = 改一处常量,不影响任何结构。

---

## 2. 与 AC 的对应

| AC / 规则 | 载体 | 判据 |
|---|---|---|
| **AC-52-29 ②**(压制 ⇒ 记入槽) | 链 ⑤之二 + ⑦之三 | `test_inClinicThreat_deferred_notDropped_noArrived` |
| 规则六之三「当日离开 ⇒ 立即补发,消费当日预算」 | 释放驱动 ② | `test_leavingClinicSameDay_reissuesWithBudget` |
| 规则六之三「跨日 ⇒ 作废,不结转」· **AC-52-48** | 释放驱动 ① | `test_crossDay_voided_noReissue` |
| 规则六之三「当日预算耗尽 ⇒ 丢弃」 | 释放驱动 ② 的 else 支 | `test_budgetExhausted_dropsInsteadOfReissuing` |
| 规则六之三「至多 `DEFER_MAX` 条」 | 链 ⑤之二的空槽预检 | `test_deferSlotsFull_zeroWrites_noOrphanRoll` |
| 未接线 ⇒ 维持旧语义(零写入) | 链 ⑤之二 的 `_deferWriter == null` 分支 | `test_deferralNotWired_fallsBackToNoQuota_zeroWrites` |
| 医馆**外**威胁档照常生成(不被延后支误伤) | 链 ⑤之二 | `test_outsideClinic_threatSpawnsNormally` |
| 导演真的驱动两条链 | `RandomEventsDirectorDriver.OnTickEdge` | `test_director_drivesThreatChainAndRelease` |

**「作废」的 `reason` 值域缺口(如实登记)**:`DeferredThreatSlotWriter` 的 `reason` 闭集只有
`{0=补发, 1=作废}` ⇒ 「跨日作废」与「预算耗尽丢弃」在流里**同值**。今天两者的可观测后果相同
(槽清掉、不补发),但**不可区分**。已写进 `DeferredThreatReleaseDriver.cs` 的文件头注;
若将来要区分,须扩 `reason` 值域(写者 + 折叠器 + registry 三处同批)。

---

## 3. 证伪轮(六枪,还原 = SHA-256 逐字节)

跑法:每枪改一处 → `-testFilter DaYiJingCheng.Tests.RandomEvents`(173 条)→ 还原 → 比哈希。

| 枪 | 改法 | 结果 |
|---|---|---|
| **M1** | 链里 `clinicSuppressedThreat` **恒假**(医馆内威胁不再入槽) | **4 红**:`test_inClinicThreat_deferred_notDropped_noArrived` · `test_leavingClinicSameDay_reissuesWithBudget` · `test_deferSlotsFull_zeroWrites_noOrphanRoll` · `test_director_drivesThreatChainAndRelease` |
| **M2** | `TryAllocateDeferSlot` **恒成功、恒给 0 号槽**(不找空槽) | **1 红**:`test_deferSlotsFull_zeroWrites_noOrphanRoll`(槽满时写者返回不可应用 ⇒ 链按不变量破坏抛) |
| **M3** | 跨日判据 `>= today` → `> today`(同日挂起的也当场作废) | **3 红**:`test_leavingClinicSameDay_reissuesWithBudget` · `test_budgetExhausted_dropsInsteadOfReissuing` · `test_director_drivesThreatChainAndRelease` |
| **M4** | 补发预算门恒通过(`spawnsToday < budgetPerDay + 100`) | **1 红**:`test_budgetExhausted_dropsInsteadOfReissuing` |
| **M5(首枪·未红)** | `PlayerCellCrossing` 里 `if (e.Tick == tick)` → `>=` | **0 红** —— **空操作变异**:上一行已有 `if (e.Tick > tick) continue;`,故该位置 `>=` 与 `==` 等价。**不算证明** |
| **M5b(二枪)** | `if (e.Tick > tick) continue;` → `>=`(本 tick 的事件被当成「未来」丢掉) | **10 红**:3 条延后链(`test_leavingClinicSameDay_reissuesWithBudget` / `test_budgetExhausted…` / `test_director_…`)+ **7 条出诊链**(`test_ac5256_*` 四条 · `test_ac5258_*` 三条) |

**M5b 是本轮最有信息量的一枪**:同一个函数上的**一处**变异,同时打红**两个消费者**
(延迟补发 + 出诊启动)⇒ 证明 `PlayerCellCrossing` 确实是**两边共用的单一出处**,
不是「照抄了一份、各自夹具都绿」的假绿形态 F8。结构侧的旁证:
`grep -rn "PlayerCellCrossing.At" unity/Assets --include=*.cs | grep -v /Tests/` ⇒
**定义 1 处、调用 2 处**(`HouseCallStartedWriter.cs:117` / `DeferredThreatReleaseDriver.cs:191`)。

**M5 首枪的教训(与批四 M3 同型,第二次)**:变异写完没先问「它真的改变了行为吗」——
`>=` 落在一个已有 `> tick` 守卫之后,构造上不可能不同。**「0 红」不能读成「夹具没抓住」,
先读一遍变异点周围的控制流**。

三文件 SHA-256 逐字节还原(`RandomEventSpawnChain.cs` / `DeferredThreatReleaseDriver.cs` /
`PlayerCellCrossing.cs`),脚本终态逐件打印「一致」;变异窗口内未编辑任何编译单元。

---

## 4. 回归(三元组)

| 套件 | 结果 | 截至时刻(UTC) | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7427 / 7390 / 0 / 0 / 37` | `2026-11-11T00:38:52Z`–`00:41:29Z` | T22 批五 | `f9037b8c` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-11T00:41:29Z`–`00:42:25Z` | T22 批五 | `f9037b8c` + 工作树 |

基线(T22 批四):EditMode `7419 / 7382 / 0 / 0 / 37` · PlayMode `376 / 371 / 0 / 2 / 3`。
**等式**:`7427 − 7419 = 8` = 本批新增夹具 `deferred_threat_wiring_test` 的 `[Test]` 计数;
PlayMode 零变化。37 条 skipped 与批四同集合(逐条均为 `NOT-RUN` / 外部前置类,非本批引入)。
命令:`~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath unity -runTests -testPlatform {EditMode|PlayMode} -testResults …`。

---

## 5. 本批没有做的(不得借绿)

| 项 | 状态 |
|---|---|
| `DEFER_MAX` 定值 | 仍是 `*待定*`(用户数值轮);本轮用 `DeferMaxPlaceholder = 3` 接线 |
| 「补发 ⇒ 玩家真的看见威胁」端到端 | **NOT-RUN** —— 生产候选池为空(`TD-randomevents-03` 未解)⇒ 端到端恒 `NoPool`,与 ADR-039 §R3 同一条登记 |
| `Mod_Eco` / `Mod_Rep` | 仍推 P1a(`TD-randomevents-04`) |
| 「跨日作废」与「预算丢弃」在流里可区分 | **未做** —— `reason` 值域只有两员(见 §2 尾注) |
