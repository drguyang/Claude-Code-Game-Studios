# Story 006: 采集侧新颖度接线 + 编排器生产装配 + AC-17-01 解除挂起

> **Epic**: 采集
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/gdd/skill-system.md`(规则二 新颖度加权 · §3.2 对象粒度表「采集」行 · `:500` 按玩家独立 · `:502` 必须亲手采过)· `design/gdd/foraging.md`(规则二 三事件 · 规则五 成长)
**Requirement**: GDD `skill-system.md:577` 新颖度系统能区分「首次 / 冷却内 / 正常」三种情况并施加正确倍率 · `:578` 追踪粒度 = `(技能, 对象)` 对且按玩家独立 · `TR-foraging-001`(三 Kind 恰发 —— 本卡解除挂起)
*(⚠️ 见 §TR 登记缺口 —— novelty 侧无对应 TR 条目,本卡以 GDD AC 行为判据)*

**ADR Governing Implementation**: ADR-026(主): 30 技能成长的定点化与持久化契约 —— ⑥「**不独立快照,从流重构**」 · ADR-005(次): 主机唯一 Append · ADR-024(次): Kind 单一登记真源 · ADR-007 §四: `PatientId.None` 不污染高水位
**ADR Decision Summary**: `NoveltyTracker` = **运行期加速缓存**,真源 = 病史流的 `SkillGrown` 行;`novelty_hint` 是**提示不是判定**,真源在 30 侧 `(skill_id, object_id)` 字典 + 冷却水位(tick);冷却一律按 **tick 计**,禁墙钟(墙钟 = 表现态量,进 sim 判定会引入不可重建的第四来源)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 装配与整数域求值;**不触任何 post-cutoff API**。`NoveltyTracker` 住 `Sim.Contracts`(门 A 内),`HarvestOrchestrator` 住 `Sim`;接线点在 `Gameplay.Boot` 的 `CompositionRoot`(边界程序集)。

**Control Manifest Rules (this layer)**:
- Required: novelty 求值在**编排器内部**(调用方不可传错);`Query` 是**有副作用**的写入口(首次/冷却期外推进 `lastSeenTick`),同一逻辑判定只调一次;对象粒度 = **品种**(`objectId`),非 `itemKey`;按 `actor` 独立追踪
- Forbidden: 把 `novelty` 作为 `HarvestRequest` 入参由外部喂入(本卡**删除**该字段);用墙钟/帧数计冷却;在 `Sim` 内 `new` 计数器;为凑绿伪造「冷却内」夹具(须真跑 `Query` 两次推进时钟)
- Guardrail: `Query` 副作用须在**原子性窗口内**发生 —— 失败路径(满载/余量不足/打断)不得推进 `lastSeenTick`,否则一次失败的采集会白白消耗掉「首次」奖励

---

## Acceptance Criteria

*From GDD `skill-system.md` §3.2 规则二 + `foraging.md` 规则二/五:*

- [x] **AC-17-14(新)**: `GIVEN` 玩家首次采某品种,`WHEN` 采集成功,`THEN` `SkillGrown.novelty_class == First`(×3.0)
- [x] **AC-17-14b(新)**: `GIVEN` 同一玩家在冷却期(`< NOVELTY_COOLDOWN` tick)内再采同品种,`WHEN` 采集成功,`THEN` `novelty_class == Stale`(×0.2),且 `lastSeenTick` **不被推进**(冷却锁定)
- [x] **AC-17-14c(新)**: `GIVEN` 同一玩家超出冷却期后再采同品种,`WHEN` 采集成功,`THEN` `novelty_class == Normal`(×1.0),且 `lastSeenTick` 被推进
- [x] **AC-17-14d(新)**: `GIVEN` 两名不同玩家各自首采同一品种,`WHEN` 各自采集,`THEN` 两人**都是** `First`(GDD `:500` 按玩家独立,不共享)
- [x] **AC-17-14e(新)**: `GIVEN` 一次**失败**的采集(满载/余量不足/打断),`WHEN` 动作终止,`THEN` `NoveltyTracker` 状态**零变化**(不消耗「首次」奖励)
  > 🔁 **2026-10-27 实现轮订正**:本条的夹具首稿**是假绿**。它用 `objectId: -1` 作非法入参,
  > 而 `NoveltyTracker.Query` **自己也守卫** `objectId < 0`(`NoveltyTracker.cs:73-75`),且守卫在
  > 写字典**之前** ⇒ `Query` 提前跑也只抛、不留痕 ⇒ 那条 `Count == 0` 断言对**定序零判别力**。
  > 证伪轮 M3 首轮红了 7 条,但**没有一条**来自这条断言。⇒ 改用 `qty: 0`(`Validate` **独有**的
  > 守卫,`Query` 不看 qty)。详见 `production/qa/evidence/review-s20-15-2026-10-27.md` §一.2。
- [x] **AC-17-14f(新)**: `GIVEN` 采集侧 `novelty` 求值路径,`WHEN` 静态检查,`THEN` `HarvestRequest` **不含** `Novelty` 字段 —— 求值只在编排器内部发生,调用方**在类型层面无法**传错
  > 🔁 **2026-10-27 订正本条的证伪口径**:原表写「加回该字段 ⇒ AC-17-14f 红」,**该表述不准确** ——
  > 加回字段是**编译期**变化,`falsify` 的行替换做不到(得同时改构造调用点)。
  > 实测分两面:**类型面**由「编译期不可表达」承载(`test_ac1714f_harvestRequestHasNoNoveltyMember`
  > 反射扫成员 + `…orchestratorTakesNoveltyTracker…` 断言构造收 `NoveltyTracker`、不收 `NoveltyClass`);
  > **行为面**由变异 M6(novelty 恒为 `First`)承载,红 3 条。
- [x] **AC-17-14g(新)**: `GIVEN` `HarvestOrchestrator`, `WHEN` 扫描 `CompositionRoot`, `THEN` 有且**仅有一个**装配点(零调用点 = 未接线;多个 = 第二真源)
- [x] **AC-17-01(解除挂起)**: `GIVEN` 一次成功采集,`WHEN` 检索产出路径,`THEN` 恰发 `ResourceHarvested` + `DropSpawned` + `DropClaimed` 三条既有 Kind,零新增 Kind(规则二)
  **⚠️ 前置 = `inventory-items/story-005`(20 写者)Complete** —— 届时换掉 `NullDropWriter` 装配,`test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot` 会转红,**那是期待不是故障**
  > ✅ **2026-10-27 已解除**:前置件 `inventory-items/story-005` 状态 = `Complete`(4/6 AC;其
  > BLOCKED 的两条与「写者是否可用」无关)。反向夹具 `test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot`
  > 已按预告**就地改判**为 `test_ac1701_nullDropWriterAssembly_writesOnlyResourceHarvested`
  > (保留为注入缝的**负向对照**),并新增正向判据
  > `test_ac1701_productionAdapter_writesAllThreeKinds_inOrder`(断言世界流恰三条 + 载荷共号 + 病史流恰一条 `SkillGrown`)。

---

## Implementation Notes

*Derived from ADR-026 §⑥ / GDD `skill-system.md` §3.2:*

1. **novelty 求值移入编排器内部**(用户 2026-10-26 裁定 甲):
   ```csharp
   // HarvestOrchestrator 构造增一依赖
   public HarvestOrchestrator(IIdAuthority ids, ResourceHarvestWriter harvestWriter,
                              IDropWriter dropWriter, IGrowthEmitter growth,
                              NoveltyTracker novelty)

   // Resolve 内,在 Validate 之后、任何写入之前:
   NoveltyClass novelty = _novelty.Query(
       request.ActorId, request.SkillId, request.ObjectId, checked((int)request.Tick));
   ```
   ⚠️ **`HarvestRequest.Novelty` 字段删除** —— 这是 AC-17-14f 的判据面。保留它 = 调用方仍可传错。

2. **对象粒度 = 品种**(`request.ObjectId`),**不是** `itemKey`。GDD `:170` 写「采集的对象是药用植物品种」。二者当前在 P0 同值(`foraging-001` 的 `item_key` 行),但语义不同 —— 用 `ObjectId` 是因为它是规则五明定的「对象 = 品种」。

3. **`Query` 有副作用,必须选对调用位置**:`Query` 在**首次**与**冷却期外**会推进 `lastSeenTick`。故它必须排在 `Validate` **之后**(非法请求不消耗奖励),但在**失败路径不可达处**。当前 `Resolve` 的所有失败都在 `Validate` 抛出 ⇒ 放在 `Validate` 后即满足 AC-17-14e。
   ⚠️ 但 `foraging/story-005`(失败收敛)落地后,**满载/余量不足的失败会在 `Validate` 之后**发生 ⇒ 届时须把 `Query` 移到失败判定之后。**本卡须留注释指明这个将来点**。

4. **`checked((int)request.Tick)`**:`HarvestRequest.Tick` 是 `long`,`NoveltyTracker` 用 `int`。承 `SeenDiseaseProjector.cs:101` 的既有口径 —— **超界 fail-loud,不静默截断**。

5. **装配点**(`CompositionRoot.cs`,现有 `noveltyTracker` 实例在 `:712`):
   ```csharp
   var noveltyTracker = new NoveltyTracker();          // 既有,复用同一实例
   var seenDisease = new SeenDiseaseProjector(vitals, noveltyTracker, stream, encoder);
   var harvest = new HarvestOrchestrator(ids, harvestWriter, dropWriter, growth, noveltyTracker);
   ```
   ⚠️ **诊断与采集共用同一 `NoveltyTracker` 实例是正确的** —— 键含 `skillId`,诊断(`DiagnosisSkillId`)与采集(`SkillId.采集 = 6`)永不相撞。**不要**为采集另建实例(那会让 AC-17-14g 变成两个装配点)。

6. **从流重建(ADR-026 ⑥)不在本卡范围** —— 见 §Out of Scope。

---

## TR 登记缺口(本卡不修,登记待裁)

`grep -n "新颖\|novelty" docs/architecture/tr-registry.yaml` ⇒ **零命中**。但 GDD `skill-system.md:577-579` 有明文三条验收标准。

⇒ 这是 **GDD 有 AC、TR registry 无条目**的缺口。本卡以 GDD AC 行为判据(不阻塞实现),
但**是否补登 TR 属架构轮**,须另裁 —— 不得在本卡擅自增 TR。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **`inventory-items/story-005`**:20 的三个写者(`DropSpawned`/`DropClaimed`/`DropDespawned`)—— **本卡的前置**
- **`foraging/story-004`**:节点余量前缀函数
- **`foraging/story-005`**:动作原子性 + 失败收敛 + 10/3/4 → 17 输入链
- **从病史流回填 `NoveltyTracker`(读档重建)**:ADR-026 ⑥ 要求「不独立快照,从流重构」,但全仓**尚无任何回填实现** —— `SeenDiseaseProjector.cs:30` 的注释是**愿望,不是代码**。本卡只做在线求值;重建路径须另立 story(见 §已知缺口)
- **10/3/4 输入侧**:本卡不碰 `Gameplay.Input`

---

## 已知缺口(须另立 story,本卡不兜)

| # | 缺口 | 判据 |
|---|---|---|
| ① | **`NoveltyTracker` 无从流回填实现** —— ADR-026 ⑥ 要求读档后从 `SkillGrown` 行重建,全仓零实现 | `grep -rn "EnumerateSeenObjects\|Reset(" --include=*.cs unity/Assets/` 除 `NoveltyTracker.cs` 自身与无关同名 `Reset` 外零命中;`SeenDiseaseProjector.cs:30` 自称有,实无 |
| ② | **novelty 侧零 TR 条目** | 见 §TR 登记缺口 |
| ③ | **`NoveltyTracker` 只增不减**(`Remove` 存在但无调用方) | 长期存档下字典无界增长;是否需修剪归数值/性能轮 |

---

## QA Test Cases

- **AC-17-14**: 首采 = First。
  - Given: 新 `NoveltyTracker`,玩家 A tick 100 首采品种 3。
  - When: `Resolve`。
  - Then: `SkillGrown` 载荷 `novelty_class == First`;`tracker.Count == 1`。
- **AC-17-14b**: 冷却内 = Stale 且不推进。
  - Given: 同上后再于 tick 100+1000(≪ 24000)`Resolve`。
  - When: 第二次采集。
  - Then: `Stale`;**`Peek` 仍返回 `Stale`(证明 `lastSeenTick` 未被推到 1000)** —— 若被推进,再等 24000 后行为会错。
- **AC-17-14c**: 冷却外 = Normal 且推进。
  - Given: 首采 tick 100,再于 tick 100+24000 采。
  - When: 第二次采集。
  - Then: `Normal`;再于 `+24000-1` 采 ⇒ `Stale`(证明 `lastSeenTick` 已推进到第二次的 tick)。
- **AC-17-14d**: 按玩家独立。
  - Given: A、B 两玩家同 tick 首采同品种。
  - When: 各自 `Resolve`。
  - Then: 两人载荷均 `First`;`tracker.Count == 2`。
- **AC-17-14e**: 失败不消耗。
  - Given: `qty = 0`(或 novelty 越界)的非法请求。
  - When: `Resolve` 抛。
  - Then: `tracker.Count == 0`(零副作用)。
  - Edge: **这条依赖 `Query` 排在 `Validate` 之后** —— 变异「把 `Query` 挪到 `Validate` 前」必须转红。
- **AC-17-14f**: 类型面无入参。
  - Given: `HarvestRequest` 的 type-surface。
  - When: 反射扫描字段。
  - Then: **零** `Novelty` / `novelty` 字段(反射非 grep)。
- **AC-17-14g**: 单装配点。
  - Given: `unity/Assets/Gameplay.Boot/CompositionRoot.cs`。
  - When: 扫描 `new HarvestOrchestrator(` 出现次数。
  - Then: **恰 1**;且 `new NoveltyTracker(` 亦恰 1(共用实例)。
- **AC-17-01**: 三事件恰发。
  - Given: 生产 `IDropWriter` 装配(20 已落地)。
  - When: 一次合法采集。
  - Then: 世界流增量 = {`ResourceHarvested`×1, `DropSpawned`×1, `DropClaimed`×1}。
  - Edge: 若仍用 `NullDropWriter` ⇒ **本 AC 不得勾**,须如实标 BLOCKED-BY。

---

## Test Evidence

**Story Type**: Integration

**Required evidence**:
- `unity/Assets/Tests/EditMode/Foraging/harvest_novelty_wiring_test.cs` —— **新建**
- `unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs` —— **改造**(删 `HarvestRequest.Novelty` 相关夹具,补 `NoveltyTracker` 注入)

**Status**: [x] Created and passing

| 文件 | 行数 | 用例 |
|---|---|---|
| `harvest_novelty_wiring_test.cs` | 404 | 11 `[Test]` |
| `harvest_orchestrator_test.cs` | 727 | 14 `[Test]` + 7 `[TestCase]` |

> 复算:`wc -l` 两件 · `grep -c '\[Test\]'` · `grep -c '\[TestCase'`

**AC → 载体映射**:

| AC | 载体 |
|---|---|
| 17-14 / 14b / 14c / 14d / 14e | `harvest_novelty_wiring_test.cs` 各一条同名 `test_ac1714*` |
| 17-14f | `test_ac1714f_harvestRequestHasNoNoveltyMember`(反射)+ `test_ac1714f_orchestratorTakesNoveltyTrackerAndQueriesItItself` |
| 17-14g | `test_ac1714g_compositionRoot_hasExactlyOne{Orchestrator,NoveltyTracker}ConstructionSite` + `…productionBag_exposesHarvest_wiredToTheSameNoveltyTracker` + `…productionBag_harvestWritesThroughToTheStream` |
| 17-01 | `harvest_orchestrator_test.test_ac1701_productionAdapter_writesAllThreeKinds_inOrder` + `harvest_novelty_wiring_test.test_ac1714g_productionBag_harvestWritesThroughToTheStream` + 负向对照 `test_ac1701_nullDropWriterAssembly_writesOnlyResourceHarvested` |

**Falsifiability Gate(S12-4 · 强制)** —— **PASS**

规格:`tools/falsify/specs/s20-15-novelty-wiring.json`(9 变异;复算 `len(mutations)` ⇒ 9)
驱动器:`python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s20-15-novelty-wiring.json --guard unity/Assets/AddressableAssetsData --guard unity/Assets/Audio --guard unity/Assets/DataCooked`

**首轮 7/9 → 修 1 条夹具假绿 + 修 2 条 spec 靶点 → 复跑 9/9 恰如预期,`exit 0`**

| 命中形态 | 变异 | 主要断言 | 反证:停用什么 ⇒ 哪条转红(实测) |
|---|---|---|---|
| ③ 定义域收窄 | M1 | 冷却判定 `elapsed >= cooldown`(`Query`) | 改 `>` ⇒ `test_ac1714c_…` 红(**1** 条) |
| ③ 定义域收窄 | M2 | 同上,`Peek` 侧 | 改 `>` ⇒ `14b` + `14c` 红(**2** 条) |
| ① 定序 | **M3** | `Query` 排在 `Validate` 之后 | `Validate` 抛出前泄漏一次 `Query` 副作用 ⇒ `test_ac1714e_…` 红(**1** 条) |
| ④ 一次性/幂等 | M4 | 冷却内不推进 `lastSeenTick` | Stale 分支也推进 ⇒ `test_ac1714b_…` 红(**1** 条) |
| 共存/互斥 | **M5** | 按玩家独立 | `NoveltyKey` 丢 `actorId` ⇒ `test_ac1714d_…` 红(**1** 条) |
| 类型面(行为侧) | M6 | 载荷 novelty 来自追踪器状态 | 恒为 `First` ⇒ `14b` + `14c` + `14f` 红(**3** 条) |
| 单装配点 | M7 | `new NoveltyTracker(` 恰 1 | 注入第二个 ⇒ `…OneNoveltyTrackerConstructionSite` 红(**1** 条) |
| 单装配点 | M8 | `new HarvestOrchestrator(` 恰 1 | 注入第二个 ⇒ `…OneOrchestratorConstructionSite` 红(**1** 条) |
| 写入口选错 | M9 | 适配器调 `RecordSpawnedWithId`(不自铸) | 两路都调 ⇒ `test_ac1701_…` + `…harvestWritesThroughToTheStream` 红(**2** 条) |

**还原判据**:`SHA-256 逐字节校验通过` · 受保护路径指纹未变 · 窗口后 `grep -c "FALSIFY-"` 四件全 0。

> ⚠️ **首轮两条不合格的形态**(如实留档,不删原表):
> - **M5 打错靶**:只改 `Query` 的键构造,`Peek` 仍用真 `actorId` ⇒ 同一字典两套键 ⇒
>   「哈希/相等不一致」脏状态 ⇒ 红 4 条。**那不是「按玩家独立被破坏」**。靶点改到
>   `NoveltyKey` 构造函数(一处改 ⇒ `Equals`/`GetHashCode`/全调用点同步降级)。
> - **M3 双重缺陷**:「把 `Query` 挪到 `Validate` 前」的字面实现 ⇒ 行替换做不到「移动」
>   (只能整行替换)⇒ 退化成**执行两次** ⇒ 同键二查判 Stale ⇒ 红 7 条。**连带暴露
>   AC-17-14e 夹具假绿**(见上方该 AC 的订正注)。
>
> ⚠️ **原表「加回 `Novelty` 字段 ⇒ AC-17-14f 红」已订正** —— 那是编译期变化,行替换做不到;
> 现由「编译期不可表达 + M6 行为面」两面承载(见 AC-17-14f 条目注)。

---

## Dependencies

- **Depends on(硬)**:
  - `inventory-items/story-005`(20 写者)—— **AC-17-01 解除挂起的唯一前置**;该件未 Complete 时本卡仍可完成 AC-17-14 系列,但 AC-17-01 不得勾
  - `NoveltyTracker`(`Sim.Contracts/SkillSystem/NoveltyTracker.cs`,已存在 + 4 条测试)
  - `foraging-003`(Complete,`8bfb861`)—— 提供 `HarvestOrchestrator` 与 `IDropWriter` 注入缝
- **Unlocks**: `foraging/story-005`(动作链需要 novelty 已在编排器内可求值)
- **被阻塞**: 从流重建 story(§已知缺口 ①)须在本卡之后 —— 它要回填的正是本卡开始写入的那些状态

---

## Completion Notes

**Completed**: 2026-10-27
**Criteria**: **8/8 passing**(AC-17-14 / 14b / 14c / 14d / 14e / 14f / 14g / AC-17-01)
**Deviations**: 无 BLOCKING。三处**已登记缺口**见 §已知缺口(均不属本件裁决面)。
**Test Evidence**: Integration —— `unity/Assets/Tests/EditMode/Foraging/harvest_novelty_wiring_test.cs`(11 用例,新建)+ `harvest_orchestrator_test.cs`(14 `[Test]` + 7 `[TestCase]`,改造)
**Code Review**: 评审原件 `production/qa/evidence/review-s20-15-2026-10-27.md`

### 交付性质(禁把「接线」读成「玩家采得到」)

本件交付 = **编排器内部求值正确** + **它在生产装配里存在**。
**生产触发点(「对着一株药材按 E」)仍归系统 4 的交互装配轮** —— 玩家今天按不到采集键。
这与 S20-14 / S20-16 的交付性质同款,均已如实登记。

### 关键裁定(用户 2026-10-27)

**R-3 双铸造点冲突 → 甲案**:在 20 侧加「收已铸 id」入口,`RecordSpawned` 退为薄包装。

| 段 | 行 | 职责 |
|---|---|---|
| `RecordSpawned(tick, anchor, key, qty)` | `DropEventWriter.cs:109` | **校验 → 铸号 → 委托**(既有公开面,行为不变) |
| `RecordSpawnedWithId(tick, id, anchor, key, qty)` | `:147` | **新增**:收外部已铸 id(17 侧用) |
| `ValidateSpawned(...)` | `:158` | 三条原守卫,两入口共用 |
| `AppendSpawned(...)` | `:171` | **唯一 `Append` 路径**,两入口共用 |

⇒ `RecordSpawned` 与 `RecordSpawnedWithId` 是**同一写者的两种形状,不是两条写路径**。
⚠️ 实现细节上的第一处自查:`RecordSpawned` 里 **`ValidateSpawned` 必须排在 `NextItemInstanceId()` 之前** ——
若照最简写法 `=> RecordSpawnedWithId(tick, _idAuthority.Next(), …)`,非法入参会**在校验之前烧掉一个号**
(机制 A 计数器前进后不可回退)。

适配器 `DropEventWriterAdapter` 显式调 `RecordSpawnedWithId`,**永不**调 `RecordSpawned`。

### 本轮实测到的三处纠正(均不掩盖)

| # | 事实 | 落点 |
|---|---|---|
| 1 | **AC-17-14e 夹具首稿是假绿** —— `objectId: -1` 这个非法入参被 `NoveltyTracker.Query` 自己也守卫,`Count == 0` 断言对定序零判别力 | 改 `qty: 0`(`Validate` 独有守卫);证伪 M3 现恰红 1 条 |
| 2 | **spec M5 首稿打错靶** —— 只改 `Query` 侧键构造,`Peek` 仍用真 actorId ⇒ 脏状态 | 靶点移到 `NoveltyKey` 构造函数,一处改全体同步 |
| 3 | **story 原表「加回 `Novelty` 字段 ⇒ 14f 红」表述不准确** —— 那是编译期变化 | 改由「编译期不可表达 + M6 行为面」两面承载 |

### 将来点(代码内已留注释,非本件兜)

`Query` 现排在 `Validate` 之后(`HarvestOrchestrator.cs:313`)。**`foraging/story-005`(动作原子性 +
失败收敛,状态 `Ready`)落地后,满载 / 余量不足的失败会发生在 `Validate` 之后** ⇒
届时须把 `Query` 移到那些失败判定**之后**,而不是移到写入之后。注释在 `:306-309`。
