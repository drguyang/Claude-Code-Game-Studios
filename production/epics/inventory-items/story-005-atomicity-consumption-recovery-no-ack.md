# Story 005: 装卸原子性、选栈消耗、死亡回捡与无负确认

> **Epic**: 库存与物品
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则六 原子性 · BL-23④ 选栈 · R7 死亡回捡 · BL-30 无负确认 · 23 交互 · AC-20-06/07/14/17/19/25)
**Requirement**: TR-inventory-008(存取原子性:容量不足整体拒绝,无半途状态)· TR-inventory-009(20 只交读数,不接触战斗与病程 —— 死亡回捡的消费侧接线)· TR-inventory-014(消耗/转移全部复用既有 Kind,零追加)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 状态可序列化前提 ⇒ 部分成功的中间态无处归因 · ADR-011(次,Amendment B): 意图上行 + 主机终裁(单机 P0 = 本地主机)· ADR-024(次): 零新增 Kind 即 registry 无变更 · ADR-009(次): 死亡散落 = `DropSpawned` 身份进流 / 位置表现
**ADR Decision Summary**: 拾取**整件拒绝**(超容量 ⇒ 地面实例留存、`InventoryOf` 零变化);炮制/建造**先验容量后扣**(AC-18-12 同构),失败**整体回滚**(料不扣、货不出);跨栈消耗须多条 `DropDespawned` —— **✅ OQ-20-10 已于 2026-10-06 裁取 ① 相邻多条 N 条**(R2 定稿按单事件即截断的风险已由形状裁定封死;余下仅 R2 的 `reason` 值域未定稿);选栈:消耗意图点名 `[(instance_id, qty)]`(BL-23④ 已裁,落地 = R14 归 11/45/21 共编,未落地前 id 升序按「兜底序」读);死亡回捡(R7 已兑现 AC-29-17):每条 `DropClaimed` 前置移动意图含死亡格;无负确认(AC-20-25):零 `PickupRejected`/`IntentAck` Kind,被拒 = 流零事件、客户端 fold 自然收敛;禁向 45 索 ack(ADR-001 纯管道);23 交互:build_part 在 `StructurePlaced` **接受后**被扣,P0 拆除全额返还经 `StructureRemoved` 入箱。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 原子性与意图路径为门 A 纯逻辑 + spy-sink 集成;跨系统走查(AC-20-14)经 23 集成测试载体;下行 instance_id 依赖客户端世界流副本(单机 P0 同源,联机半边 R1/45 未立 ⇒ 不外纳)。

**Control Manifest Rules (this layer)**:
- Required: 全部写入路径 = 「验容量(Story 004 `CanCarry`)→ 预演扣减 → 原子 Apply → Append 事件」;被拒意图 ⇒ 世界流零新增;消耗意图载荷 ⊆ `[(instance_id, qty)]` 整数对;返还失败走 Story 006 引用的落地路径(同 23 规则九)
- Forbidden: 半途状态(部分入包/部分扣料);否定信令事件(`PickupRejected`/`IntentAck`/`ConsumeFailed`);静默吞失败(被拒无感知 = 机制侧也要零事件 + fold 收敛,呈现半边归 42);先扣后补
- Guardrail: ~~OQ-20-10 未裁 ⇒ 反例夹具~~ **✅ 2026-10-06 已裁(相邻多条)** ⇒ 本故事按裁定形状交付(跨栈 = N 条同 tick 相邻 `Seq`),不再需要「单事件即截断」反例夹具;**不得**自行改回请求级单事件形状(违 `entities.yaml` DropDespawned 裁定)

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [ ] **AC-20-06**: `GIVEN` 超容量拾取,`WHEN` 请求,`THEN` 整体拒绝,`InventoryOf` 零变化,地面实例留存(规则六)
- [ ] **AC-20-07**: `GIVEN` 炮制中途失败(容量不足),`WHEN` 结算,`THEN` 整体回滚(料不扣、货不出)(规则六)
- [ ] **AC-20-17**: `GIVEN` 一次炮制/一次建造,`WHEN` 检索世界流,`THEN` 零 `ItemConsumed`/`ItemTransferred` 追加 Kind;扣减由 `Craft.ActualConsumed` / `StructurePlaced` 的 `cost(m)` 唯一推出(OQ-20-1 · 规则一①)
- [ ] **AC-20-19** [已解除 BLOCKED-BY-R7]: `GIVEN` 玩家死亡后掉落物散落,`WHEN` 逐条检视 `DropClaimed`,`THEN` 每条 `DropClaimed` 的前置移动意图含该 drop 所在死亡格(可判伪正向谓词;OQ-25-1 已裁路甲 ⇒ 留 P0)
- [ ] **AC-20-25**(BL-30①): `GIVEN` 20 全部代码路径与 `entities.yaml` Kind 闭集,`WHEN` 检索,`THEN` 零 `PickupRejected`/`IntentAck`/`ConsumeFailed` 类否定事件;被拒意图后世界流零新增(`InventoryOf` 前后逐位相同);禁向 45 索 ack 通道
- [ ] **AC-20-14** [I]: `GIVEN` 23 的一次放置,`WHEN` 集成走查,`THEN` `build_part` 在 `StructurePlaced` **接受后**被扣除;P0 拆除**全额返还**(23 规则七)

---

## Implementation Notes

*Derived from ADR-005 §Decision(主)/ ADR-011 Amendment B:*

1. 统一写入事务模板(拾取/放下/消耗/建造扣/返还入箱共用):快照前态 → 判 `CanCarry`/预演扣减 → 通过 ⇒ 改投影 + `Append` 既有三支(`DropClaimed`/`DropSpawned`/`DropDespawned`)或复用上游扣减(Craft/StructurePlaced);失败 ⇒ 回滚 + **零事件**(AC-20-25 信令侧:被拒的「反馈」在 P0 单机 = 呈现层读 fold 差值,机制零信令)。
2. 选栈消耗:上游(11 用药 / 18 炮制 / 23 建造)点名 `[(instance_id, qty)]`;20 按点逐栈扣,单栈 qty 不足 ⇒ 该栈整扣 + 后续栈续扣(跨栈 = **N 条同 tick 相邻 `DropDespawned`**,✅ OQ-20-10 已裁形状,不再需要反例夹具);id 升序「兜底序」仅作非点名查询的稳定遍历(承 Story 003 摆放序同一函数)。
3. 死亡回捡接线:消费 29 的死亡散落产物(`DropSpawned` 群)+ 回捡移动意图(经 interaction/4 的目标选择)⇒ 回捡 = 标准拾取事务(容量不足的回捡也走整件拒绝,无特例);谓词形态 = AC-20-19 的正向可判伪(逐条 `DropClaimed` 查其前置意图含死亡格)。
4. 23 交互半边:放置 = 23 判定通过 → 20 扣料(先验容量,BL-6 与 Story 004 共件)→ 23 `Append(StructurePlaced)` —— 时序「接受后被扣」的可观测形式 = spy-sink 断 `StructurePlaced` 在前、扣减效果(由 cost 推出的 fold 差)在后且原子;返还失败(满)→ `DropSpawned`(EC-23-11,发出者 = 20,23 触发 —— 与 processing Story 005 溢出同路径复用)。
5. 用药消耗(AC-20-16 相关):实例级 `DropDespawned`(**✅ OQ-20-10 已裁:跨栈 = N 条同 tick 相邻 `Seq`**,单事件候选已否决 ⇒ 不再需双形状解析;余 `reason` 值域归 R2,定稿前该半仍挂 Ignore)。
6. 无负确认的呈现侧(被拒反馈的拟物形态)归 42/10 UX Flag(承 processing「禁负信令但反馈可读」的分裂处理先例:机制零事件 + 呈现读差值)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004:`CanCarry` 谓词与单源 cap(本件只调用)
- Story 001:folds/重建等值(本件用前后投影差做断言)
- Story 006:满溢四档/音效走查/药箱界面
- modular-building epic:放置判定五条件、拆除谓词本体(23 规则九);本件只钉 20↔23 交接时序
- death-and-respawn(29)epic:死亡散落的生产侧(R7 已兑现,本件是消费接线)
- emergency/prescription epics:用药时机与药效(11/10);本件只接消耗意图形状
- R1 / ADR-001 窄修订 / 45:联机意图上行与 ack 缺位(明确不索)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-06**: 超容量拾取整件拒。
  - Given: cap 临界(carry_load = cap − w + 1,候选 w 装不下);地面实例 g。
  - When: 拾取意图。
  - Then: `InventoryOf` 前后逐位等;g 仍在 `DropSpawned` 归属(地面);流零新增;反馈由呈现读差值(不断呈现)。
  - Edge cases: 恰等界(cap − w ⇒ 成功);多玩家抢同 instance(同 tick 串行,后者见已被 Claimed ⇒ 目标消失路径非容量路径,另具名)。
- **AC-20-07**: 中途失败整体回滚。
  - Given: 炮制点火时 20.Apply 拒(Story 004 谓词 false)。
  - When: 结算事务。
  - Then: 料实例 qty 不变、无产出实例入库、零 `Craft`(与 processing AC-18-12 同夹具双跑);事务后投影 = 事务前(全表 diff 空)。
  - Edge cases: 跨栈点名中含一个不足栈 ⇒ 全事务重放回滚(禁「先扣够的栈」)。
- **AC-20-25**: 无负确认信令。
  - Given: 全部失败路径(满载/目标消失/不可点)各触发一次。
  - When: spy-sink + registry 扫描。
  - Then: 流增量恒 = 0;`entities.yaml` 无三类否定 Kind;45 接口面零 ack 符号(反射)。
  - Edge cases: 连续被拒 N 次 ⇒ 流仍零事件(无「累计告警」Kind 私设)。
- **AC-20-19**: 死亡回捡谓词。
  - Given: 29 夹具:死亡 → 散落 `DropSpawned` 群 → 回捡序列。
  - When: 逐条检视 `DropClaimed`。
  - Then: 每条前置存在「移动意图含该死亡格」记录;反例(无前意图的 Claimed)注入必红。
  - Edge cases: 容量不足的回捡 = 标准整件拒(无死亡特例通道);部分回捡后再死亡(两轮序列)。
- **AC-20-14** [I]: 23 交接时序。
  - Given: 23 放置夹具(判定通过)+ 拆除夹具(P0 R=1)。
  - When: 集成走查 spy-sink + fold。
  - Then: `StructurePlaced` 接受后才见扣减效果;拆除 ⇒ 全额返还入箱(投影含返还栈);返还满 ⇒ 落地 `DropSpawned`(23 触发/20 发出)。
  - Edge cases: cost 恰满剩余容量;返还栈与既存栈同 key 同 q ⇒ 并栈(stack_max 界内)。
- **AC-20-16/03 债锚点**: Ignore 用例群。
  - Given: R2 的 `reason` 值域未定稿(形状已裁,不再计入阻塞)。
  - When: 涉及载荷扩字段(qty/reason)的子用例。
  - Then: `Ignore("BLOCKED-BY R2")` 显式挂起,理由点名(禁静默跳过)。
  - Edge cases: 定稿后摘除 Ignore ⇒ 同一测试体翻真绿(回归锚)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/inventory_atomicity_test.cs` — must exist and pass
- Logic(信令/registry 扫描): `unity/Assets/Tests/EditMode/Inventory/no_negative_ack_test.cs`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(投影差值断言基座),Story 003(堆叠扣减语义),Story 004(CanCarry),processing Story 003/004(点火事务对方),modular-building Story 002/005(放置/拆除判定对方 —— 可并行开发,以接口桩先行)
- Unlocks: Story 006(呈现层读被拒后的收敛投影);数值轮存在性门翻真(AC-20-21 经本件可达状态搜索实测)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
