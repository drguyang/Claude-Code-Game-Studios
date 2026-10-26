# Story 003: 三条世界流事件 + 主机铸造身份 + 恰好一条 SkillGrown

> **Epic**: 采集
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-26

## Context

**GDD**: `design/gdd/foraging.md`(规则二/三/五 · AC A 组事件与身份 · Edge Cases 多玩家同 tick 行)
**Requirement**: TR-foraging-001(采集只发三个世界流 Kind,不多发不漏发)· TR-foraging-005(采集掉落 instance_id 由主机经 IIdAuthority 铸造,客户端不铸)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主,Amendment K): 世界状态事件化边界 · ADR-010(次): 持久化与 `ItemInstanceId.Next()` 机制 A · ADR-024(次): Kind 单一登记真源 · ADR-005(次): 主机唯一 Append
**ADR Decision Summary**: 掉落身份进流 / 位置表现(ADR-009 §一 用户裁定);`ResourceHarvested` 载荷 `{instance_id, node_id, gather_seq, qty, out_quality}` 归 17 GDD 具名(Amendment K 首次以「系统 GDD 追加 Kind」通道);`DropSpawned{instance_id, spawn_anchor=node.cell, item_key, qty}` 是 id **出生即铸**的铸造点;主机唯一执行 Append(ADR-005);Kind 路由真源 = `entities.yaml` + kindgen 断言(ADR-024);`SkillGrown` 落病史流,采集每次成功恰好一条(对象 = 品种)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 事件载荷与路由为纯 C# 数据契约 + `entities.yaml` 既有 Kind(零新增 ⇒ 零 kindgen 变更);三流全序与 fold 集成测试走 EditMode 纯逻辑(门 A 程序集),不触 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 一次成功采集**恰好**发 `ResourceHarvested` + `DropSpawned` + `DropClaimed` 三条(同 tick 连发,全序键 `(Tick, StreamPriority, Patient, Seq)` 内 `Seq` 由主机发号);id 铸造点 = `DropSpawned` 出生当下经 `IIdAuthority.ItemInstanceId.Next()`;成长经 30 的 `EmitGrowth`(唯一交接点)
- Forbidden: 新增任何 Kind(零 `ItemGathered` / `Gather` 类);客户端本地铸造;`raw_quality` 入载荷;`world_pos`(float)入载荷 —— `spawn_anchor` = 整数格(ADR-015 §三)
- Guardrail: 载荷字段 ⊆ 整数域(ADR-024 A2 断言天然覆盖);三事件必须**原子地**同进同出(spy-sink 断「有二无一」序列 = 非法)

---

## Acceptance Criteria

*From GDD `design/gdd/foraging.md`, scoped to this story:*

- [ ] **AC-17-01**: `GIVEN` 一次成功采集,`WHEN` 检索产出路径,`THEN` 恰发 `ResourceHarvested` + `DropSpawned` + `DropClaimed` 三条既有 Kind,零新增 Kind(规则二)
  **⚠️ 不勾 —— BLOCKED-BY 20 的两个写者**(`DropSpawned` / `DropClaimed` 生产写者不存在,`WriterExistenceGate.cs:152-154` 已登记「20 落地写者未落」)。17 侧已做到「正确地**要求**这三条」(`NullDropWriter` 显式空实现 + 反向夹具 `test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot` 钉住「今天只发 1 条」),但「三条真的进流」未证。真实前置 = `inventory-items/story-005`
- [x] **AC-17-05**: `GIVEN` 采集在客户端执行,`WHEN` 反射扫描 sim 程序集的 `ItemInstanceId` 构造点(type-surface 反射,非 grep),`THEN` 唯一来源 = `IIdAuthority`(D-21-27)
  ✅ **4 条夹具**,含 Mono.Cecil IL 构造点扫描(`newobj` + `call .ctor` 双扫,因 `ItemInstanceId` 是 struct)+ 上下界双向断言
- [x] **AC-17-05b**: `GIVEN` 一次成功采集,`WHEN` 检索成长事件,`THEN` 恰好一条 `SkillGrown`(对象 = 品种),不因三条产出事件多发(规则五)
  ✅ **4 条夹具**,含生产适配器真链路(解码载荷逐字段等值,不只数条数)
- [ ] **AC-20-03 邻接义务(17 侧 R11)**: `DropSpawned` 铸造点口径与 20 的高水位扫描集同步(R11 已闭合,本故事交「铸造点 = 出生事件当下」的可断言形态;fold 重建整体验收归 inventory-items epic Story 001/002)
  **⚠️ 半边** —— 17 侧自足那半已证(`test_r11_*` 两条:三条载荷共用同一 id、一次采集恰申请 1 个 id);「id 真的从出生事件落进流」这半条随 AC-17-01 一并 BLOCKED

---

## Implementation Notes

*Derived from ADR-009 Amendment K(主)/ ADR-010 §五 / ADR-024:*

1. 事件序(主机裁决,同 tick):`DropSpawned`(铸 id,`spawn_anchor = node.cell`,`item_key` = 品种,`qty` 由 F-17-2)→ `ResourceHarvested`(载荷五字段,`instance_id` 引用刚铸 id,`out_quality` 由 Story 002)→ `DropClaimed`(`instance_id, claimer = actor_id`)。三条 `Seq` 由主机依发号顺序连编,全序键满足 ADR-008 跨流全序。
2. id 铸造 = `IIdAuthority.ItemInstanceId.Next()`(机制 A:计数器永不复位 + 迁移后 `max+1` 由三流并集 ∪ `BakedInitial` 重构,ADR-006 Amendment B / ADR-010 §五);17 **不自建计数器**,只申请。
3. `SkillGrown`:经 30 `EmitGrowth(采集, 对象=品种)`,每次成功采集**一条**(挂在裁决尾部,与三事件同 tick);新颖度/成长值计算全归 30(skill-system epic 已 Complete),17 零公式。
4. 载荷五字段与 `entities.yaml` 的 `ResourceHarvested` 条目逐字对齐(ADR-024 真源;若 registry 字段序/名不一致 ⇒ 以 registry 为准并回写 GDD,禁两读)。
5. 多玩家同 tick 打同一节点:主机**串行**裁决(EC:先采者消耗余量,后采者可能落「余量不足 ⇒ 零事件」路径 —— 与 Story 004 的采前闸联动,本故事只钉「每条成功 = 三条 + 一条成长」的计数不变量)。
6. 集成测试载体:spy-sink `IEventSink` 收流 + 从空流重放等值断言(确定性回归的最小面,替代尚不存在的 ADR-012 矩阵)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:`out_quality` 怎么算(本故事只搬运进载荷)
- Story 004:失败路径的零事件语义(余量不足 / 满载不产本组事件)
- Story 005:动作整合(3 输入 → 4 目标选择 → 17 裁决的端到端链)
- inventory-items epic:`InventoryOf` fold 对三支事件的消费与高水位并集(R3/BL-1③ 落点在 20 侧)
- skill-system epic:`SkillGrown` 载荷与新颖度(Complete,本故事仅调用 `EmitGrowth`)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-17-01**: 三条恰发、零新增。
  - Given: spy-sink 包裹的事件汇;一次合法成功采集(余量足、库存可容)。
  - When: 主机裁决。
  - Then: 流增量恰 = {`ResourceHarvested`×1, `DropSpawned`×1, `DropClaimed`×1};Kind 全属既有白名单;`StreamId` 路由 = 世界流三支(与 `StreamRouting.g.cs` 一致)。
  - Edge cases: 「有二无一」注入(手工造缺 `DropClaimed` 序列)⇒ 下游 fold 不完整可判(为 20 侧测试提供夹具,不断本件绿);同 tick 两玩家各采 ⇒ 六条,按玩家分组计数仍各三。
- **AC-17-05**: 反射扫描唯一铸造源。
  - Given: sim 程序集(门 A)全 type-surface。
  - When: 反射扫 `ItemInstanceId` 构造/`Next()` 调用点。
  - Then: 唯一来源 = `IIdAuthority` 实现;17 侧零 `new ItemInstanceId(...)` 旁路;客户端代码路径零调用(单机 P0 下「客户端」= 同一进程的表现层侧,判定按程序集归属)。
  - Edge cases: 测试装配(`Sim.Contracts.Tests`)豁免口径与 item-database story-011 先例一致。
- **AC-17-05b**: 成长恰一条。
  - Given: 成功采集 spy `EmitGrowth` 调用。
  - When: 单次裁决。
  - Then: `SkillGrown` 恰 1 条、对象 = 品种 item_key;三事件不各自触发成长(计数不随事件数放大)。
  - Edge cases: 失败采集 ⇒ 成长 0 条(与 Story 004 共断);连续采同品种 ⇒ 每条成功各一(新颖度衰减归 30,不断值)。
- **R11 邻接**: 铸造点当下性。
  - Given: 断言 `DropSpawned` 载荷 `instance_id` 与同 tick `ResourceHarvested`/`DropClaimed` 所引用 id 同一。
  - When: 重放前缀。
  - Then: 自空流 fold 可重建同一 id 绑定(为 20 Story 002 的高水位扫流供可重放样本)。
  - Edge cases: 跨 tick 不回收、不重用 id。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**(建卡时声明 —— ⚠️ **两条路径均未按此落地**,见下):
- ~~Integration: `unity/Assets/Tests/PlayMode/foraging_stream_events_test.cs`~~ — **不存在**
- ~~Logic(AC-17-05 反射面): `unity/Assets/Tests/EditMode/Foraging/gather_identity_reflection_test.cs`~~ — **不存在**

**实际载体**(2026-10-26 回填,评审 W-4):

| 项 | 值 |
|---|---|
| 文件 | `unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs`(646 行) |
| 夹具数 | **21**(15 个测试方法,其中 2 个参数化:`test_invalidRequest_throwsBeforeAnyWrite` ×5 + `test_b1_growthFieldsValidatedBeforeAnyWrite` ×3;其余 13 个各 1 条) |
| 级别 | EditMode(门 A 程序集,纯逻辑)—— **非** PlayMode 集成 |
| 装配 | 真 `IEventSink` / `IPayloadEncoder` / `IPayloadDecoder` + `ResourceHarvestWriter` + `HarvestOrchestrator`;20 侧与 30 侧用 Spy |

**AC → 载体映射**:

| AC | 载体 |
|---|---|
| AC-17-05 | `test_ac1705_instanceIdComesFromIdAuthority_notLocallyMinted` · `..._harvestOrchestratorType_hasNoLocalCounter` · `..._simAssembly_hasExactlyOneIdAuthorityImplementation` · `..._simAssembly_hasExactlyOneItemInstanceIdCtorSite` |
| AC-17-05b | `test_ac1705b_oneSuccessfulHarvest_emitsExactlyOneSkillGrown` · `..._growthCountScalesWithHarvests_notWithEventCount` · `..._growthCarriesHarvestTickAndNonePatient` · `..._productionAdapter_actuallyAppendsSkillGrownToHistoryStream` |
| AC-17-01(不勾) | `test_ac1701_orchestratorRequiresBothDropEvents_withCorrectPayload`(证「要求」)· `test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot`(**反向夹具**,20 落地后转红 = 期待) |
| R11 邻接(半边) | `test_r11_threeIdCarryingPayloadsShareTheSameInstanceId` · `test_r11_instanceIdIsMintedBeforeAnyWrite` |
| (前置校验) | `test_invalidRequest_throwsBeforeAnyWrite`(5 个 `TestCase`)· `test_b1_growthFieldsValidatedBeforeAnyWrite`(3 个 `TestCase`) |
| (事件序) | `test_eventOrder_dropSpawnedPrecedesResourceHarvested_precedesDropClaimed` |

**Test Evidence 三元组**:

| 项 | 值 |
|---|---|
| 截至时刻 | 2026-10-26 15:03(本机) |
| 批次 | `f3-fix2`(夹具级)· `f3-full-final`(全量 EditMode) |
| commit | `22ffe86`(本件改动**未提交**) |

| 级别 | total | passed | failed | skipped |
|---|---|---|---|---|
| 夹具级 | **21** | **21** | 0 | 0 |
| 全量 EditMode | **5217** | **5182** | 0 | 35 |
| 基线(`f3-full`,评审修复前) | 5213 | 5178 | 0 | 35 |
| **增量** | **+4** | **+4** | 0 | 0 |

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity/Logs/f3-fix2.xml | head -1      # 21/21/0
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity/Logs/f3-full-final.xml | head -1 # 5217/5182/0
grep -c "error CS" unity/Logs/f3-full-final.log                                                  # 0
```

**Falsifiability Gate(S12-4 · 强制)**:

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定义域收窄 | `qty ≥ 1` | 放宽为 `> 0` ⇒ `test_invalidRequest_throwsBeforeAnyWrite` 红(**首轮曾红=0,该夹具已修**) |
| 定义域收窄 | `novelty ∈ [0, Normal]` 上界 | 停用 `> Normal` ⇒ `test_b1_growthFieldsValidatedBeforeAnyWrite` 红(M11) |
| 定义域收窄 | `novelty >= 0` 下界 | 停用 `< 0` ⇒ 同上红(M12) |
| 定义域收窄 | `xpGain.Raw >= 0` | 停用 ⇒ 同上红(M13) |
| 定序 | `DropSpawned` 先于 `ResourceHarvested` | 把 `Spawn` 挪回 `Record` 之后 ⇒ `test_eventOrder_...` 红(M10) |
| 一次性/幂等 | 成长恰一条 | 发两条 ⇒ 4 条红(M2) |
| 定义域收窄 | 本地铸造 id | 绕过 `IIdAuthority` ⇒ 2 条红(M1) |
| 定序 | claimer 归属 | 偏移 ⇒ 1 条红(M3) |
| 类型面 | 无可变字段 | 加可变字段 ⇒ 1 条红(M7) |
| 前置校验 | 停用 `Validate` | ⇒ 1 条红(M8) |
| 新增判据判别力 | IL 构造点扫描 | Sim 内加越界构造点 ⇒ **恰只** IL 测红(M9) |

⇒ **13 条变异全转红**。⚠️ 首轮 7/8(M5 假绿)**已如实记录并修复**,不掩盖。

---

## Dependencies

- Depends on: Story 002(`out_quality`/`gather_seq` 产出),skill-system story-006/007(`EmitGrowth`/`SkillGrown`,Complete),item-database story-008/010 类先例(id 铸造件已建),`entities.yaml` 三支既有 Kind(无需 kindgen 变更)
- Unlocks: Story 005(端到端动作链以本组事件为落点),inventory-items Story 001(fold 并入 `ResourceHarvested` 的消费样本流)

---

## Completion Notes

**Completed**: 2026-10-26
**Criteria**: **2/4 记绿**(AC-17-05 · AC-17-05b)· AC-17-01 **不勾**(BLOCKED-BY 20 写者)· R11 **半边**

### 交付面

| 文件 | 性质 |
|---|---|
| `unity/Assets/Sim/Foraging/HarvestOrchestrator.cs` | 新建 · 315 行 · 五步编排 |
| `unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs` | 新建 · 646 行 · 21 条夹具 |

### 评审(必读 · BLOCKING 级原件已落盘)

`production/qa/evidence/review-foraging-003-2026-10-26.md` —— 原判定 **CHANGES REQUIRED**
(BLOCKING 2 / WARNING 4 / MINOR 3)。两条 BLOCKING 均已修复并经变异证伪:

- **B-1** 类头「任何前置失败都在任何写入之前抛出」**为假** —— `Validate` 不核 `Novelty` / `XpGain`,
  而这两项的校验在最后一次写入之后。**已修**:补两条前置守卫,三条分支各经 M11/M12/M13 证伪。
- **B-2** 事件序与 story 相反 —— `ResourceHarvested` 先于其出生事件 `DropSpawned` 落流。
  **已修**:②③ 对调,经 M10 证伪;错误理据(「`gather_seq` 须先算」)已就地订正。

### 未修项(评审 WARNING / MINOR,6 条)

| ID | 内容 | 处置 |
|---|---|---|
| **W-1** | 三方写者口径冲突的**裁决锚**未登记(story `:17`) | 待补:照 ADR-024 §① 取 `entities.yaml` 是执行既有 ADR,非越权 |
| **W-2** | 成长入口**三套抽象并存**,两处适配器方法体近逐字重复 | 待裁:复用 / doc 明写不可复用理由 |
| **W-3** | `HarvestRequest.Novelty` **无生产者** | 见下「交付性质」② |
| **W-4** | 建卡声明的两条测试路径不存在 | **已在本卡回填实际载体**(见 §Test Evidence) |
| **M-1** | `IDropWriter.Spawn` 的 doc 说「同时是铸造点」,`NullDropWriter` 不铸 | 待改 doc 措辞 |
| **M-3** | AC 勾选未回填 | **已回填**(见 §Acceptance Criteria) |

### 交付性质(**不得读成「采集功能已可用」**)

本件交付的是**形状完备的实现 + 自足的夹具**,不是跑得起来的采集链路。三条实测事实:

| # | 事实 | 判据 |
|---|---|---|
| ① | `HarvestOrchestrator` **全仓零生产调用点** | `grep -rn "HarvestOrchestrator\|IDropWriter\|IGrowthEmitter" --include=*.cs unity/Assets/` 只命中自身 + 测试 |
| ② | `Novelty` **无生产者** | `grep NoveltyClass.` 只余常量定义与守卫说明;`PrescriptionPorts.cs:152` 自陈「novelty 仍取 Normal」⇒ 照抄则**「首次 ×3.0」永不触发**,且无夹具会红 |
| ③ | `DropSpawned` / `DropClaimed` 写者**不存在** | `WriterExistenceGate.cs:152-154`(2026-10-10 登记) |

⇒ ①③ 是 AC-17-01 / R11 不勾的延伸;② 是**新发现的静默失效面** —— B-1 补了域界守卫后更隐蔽
(守卫通过,但传进来的值恒为 `Normal`)。**建议另立 story 处理 ②。**

### 计划外

本件**不在 sprint-20 计划内**:`production/sprint-status.yaml` 只有 S20-1..S20-7,无本件条目;
`sprint-20.md` 亦无。属计划外插入工作 —— 故无 sprint-status 行可更新(不是漏改)。

### 证据

- 实现证据:`production/qa/evidence/impl-foraging-003-2026-10-26.md`
- 评审原件:`production/qa/evidence/review-foraging-003-2026-10-26.md`
- 证伪轮日志:`~/.claude/jobs/181888b1/tmp/falsify_f3_{run,m5_retry,m9,review_fixes,m12,m13}.log`
