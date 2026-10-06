# Story 001: InventoryOf 世界流投影与清空重建等值

> **Epic**: 库存与物品
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则一 fold · 边例 · AC A 组重建行)
**Requirement**: TR-inventory-001(库存 = 世界流纯投影,快照 = 优化非真相)· TR-inventory-009(20 不接触战斗与病程,只交读数)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类 —— 库存条目 = 模拟态进流,投影 = 派生态重建 · ADR-005(次): 事件流唯一真源 · ADR-010(次): 快照与三流冲突时以三流为准
**ADR Decision Summary**: `InventoryOf(player) = fold(世界流, {DropSpawned, DropClaimed, DropDespawned, StructureRemoved, Craft.ActualConsumed, StructurePlaced, ResourceHarvested})`;七类 Kind 全为既有,零追加;`ResourceHarvested` 并入为 17 侧 R-注⑧ 义务(否则品级重建不出);内存索引 = 优化非真相,清空重建须逐位相同;用药消耗走世界流 `DropDespawned`(实例级),**fold 域维持世界流**(OQ-20-1 已裁:处置事件住病史流不含 instance_id,不以其为载体)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: fold 为门 A 纯 C#(整数载荷 → 索引结构),零引擎 API;重建等值属确定性回归最小面,Mono 侧本故事可自证;跨平台逐位(承 ADR-012)不在本条判据内。

**Control Manifest Rules (this layer)**:
- Required: fold 纯函数(输入 = 流前缀 + 烘焙定义,零第三来源);投影结果只读(消费者不得回写);`InventoryOf` 是 20 对外唯一库存真值入口(零第二真源)
- Forbidden: 内存索引当真源(索引与 fold 冲突 ⇒ fold 赢 + 具名告警);读病史流/病例流条目入 fold 域(域 = 世界流);在 fold 内求值 EFF/QualityMod(那是 21a,AC-20-05 纪律)
- Guardrail: 前置债以 BLOCKED-BY 原样挂判据,禁借绿:AC-20-03 五前置(R2/R3/R10/R11/OQ-20-5;R10 已由 18 认领、R11 已闭合、R3 未落地);本故事交付「重建机制本体 + 可判伪的挂起项」

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [ ] **AC-20-03** [A]: `GIVEN` 一份存档,`WHEN` 清空 20 的内存索引并从三流重建,`THEN` `InventoryOf` 逐位相同(规则一)。**⚠️ BLOCKED-BY-R2 / R3 / R10 / R11 / OQ-20-5**(五件:Craft 载荷产出 id 位 R10 · D-21-28 全序键 · `DropDespawned.qty` R2 · 高水位扫 `BakedInitial` R3 · 17 铸造点口径 R11)—— 前置未全回写前**不得记绿**;已闭合项(R10 认领落 18、R11 同步、D-21-28 裁[甲])在证据链逐条点名
- [ ] **AC-20-16** [I]: 一次用药消耗,经 7a 持久化往返 + 清空内存索引重建 ⇒ 该实例经**世界流 `DropDespawned`** 消失(非处置事件),fold 域仍是世界流。**⚠️ BLOCKED-BY-OQ-20-5 / R2**(D-21-29 一株→几剂无认领方 + `DropDespawned.qty` 载荷未定稿 + BL-31 跨栈多条无载体)
- [ ] **AC-20-05** [A]: `GIVEN` 20 的代码路径,`WHEN` 检索 `EFF`/`QualityMod`/`quality_distribution` 求值,`THEN` 零(规则五:20 不求值 —— fold 只搬运上游算好的整数)
- [ ] **AC-20-17** [A]: 一次炮制/一次建造,检索世界流 ⇒ 零 `ItemConsumed`/`ItemTransferred` 追加 Kind;扣减由 `Craft.ActualConsumed` / `StructurePlaced.cost(m)` 唯一推出(OQ-20-1 · 规则一①)

---

## Implementation Notes

*Derived from ADR-009 §二/§一(主)/ ADR-005:*

1. fold 状态机:按全序键 `(Tick, StreamPriority, Patient, Seq)` 升序扫世界流,对七类 Kind 施加纯函数转移(出生=DropSpawned 建实例条目;归属=DropClaimed 迁 owner;消失=DropDespawned 扣 qty;增益=Craft 的 `output_instance_ids`+`OutputQty/Quality`;扣料=Craft.ActualConsumed;建造扣=StructurePlaced.cost;返还=StructureRemoved;品级源=ResourceHarvested 绑 instance→quality)。
2. 索引结构 = `(instance_id → {item_key, quality, qty, owner})` 纯内存字典,派生视图(按玩家 / 按格)都从该 fold 读;存档/快照只存事件流(ADR-010),读档 = 从流重建 + 快照作起点优化(冲突时流赢 —— 该优先级写进断言)。
3. 品级载体:实例的 `quality` 只在 `ResourceHarvested`(采出)与 `Craft.OutputQuality`(制出)两条上出现,fold 一次性绑定;此后只读(承 21a 品级不抬高纪律的投影侧)。
4. AC-20-03 的可执行子集(不违禁借绿):交付「重建器本体 + 用**完整夹具流**跑通」—— 夹具流内构造 R2/R3 尚未定稿字段的合法占位(如 `DropDespawned` 按单事件形状),但 AC 行保持未勾 + `Ignore` 标注,真实定稿后摘除;`Ignore` 理由逐条点名五前置(镜像 processing Story 001 的 O-18-R1 处理形)。
5. 用药/跨栈消耗形状(BL-23④ 选栈点名 `[(instance_id, qty)]`)由 Story 005 落地;本故事只保证 fold 消费端能表达「按实例扣 qty」(✅ OQ-20-10 已于 2026-10-06 裁「相邻多条 N 条」⇒ 兜底读法不再是猜测,fold 侧直接按「逐实例 qty」实现)。
6. 性能门:溢出界 `|Inventory| × stack_max × max(weight) < 2^31` 是烘焙期常量 + 构建期断言(TR-inventory-010 partial,值班方未裁)—— 本故事在 fold 读入口做防御断言,值班方裁定外抛登记。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:id 铸造与高水位(fold 消费 id,不生产 id)
- Story 004/005:CanCarry 裁决与原子装卸(本故事只提供投影读数)
- Story 006:摆放序与药箱呈现
- 7a(persistence epic):codec 与段清单(本故事只断「流赢快照」的优先级语义)
- ADR-012 落地轮:跨平台重建逐位(不在本条判据)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-03**: 清空重建逐位等。
  - Given: 合成事件流夹具(七类 Kind 全覆盖,≥500 事件,含多玩家/多实例/品级绑定);内存索引已建。
  - When: 清空索引 → 从流重建。
  - Then: 重建投影与原投影逐位相同(实例集、owner、qty、quality 四张表全等);二次重建幂等。
  - Edge cases: 乱序输入(先喂后 tick 事件)⇒ fold 按全序键排序后同结果(排序在 fold 内,非调用方义务);空流 ⇒ 空投影非 null。真实定稿字段前挂 `Ignore("BLOCKED-BY R2/R3")` 于涉及未裁字段的子用例。
- **AC-20-16**: 用药走 DropDespawned。
  - Given: 实例 i 被用药消耗;7a round-trip 夹具(或流重放替代 codec 半边)。
  - When: 重建。
  - Then: i 经 `DropDespawned` 消失;断言 fold 域读集 ⊆ 世界流(反射/路由表断言:病史流 Kind 零消费)。
  - Edge cases: 处置事件(病史流)在场但无 DropDespawned ⇒ 实例仍在(证明载体正确);OQ-20-5/R2 未裁 ⇒ 子用例 Ignore,AC 行未勾。
- **AC-20-05**: 零结算求值。
  - Given: 20 程序集。
  - When: 符号扫描 EFF/QualityMod/quality_distribution。
  - Then: 零求值命中(载荷搬运不算);负样例注入必红。
  - Edge cases: quality 值的**搬运**(从 Craft 载荷抄入索引)与**求值**(算出 quality)以调用图区分(承 processing AC-18-02 边界口径)。
- **AC-20-17**: 零追加 Kind。
  - Given: `entities.yaml` Kind 闭集 + 20 的 Append 调用点全集。
  - When: 炮制/建造/用药/转移四类消耗转移各一次。
  - Then: 流增量只含既有七类;20 自身 Append 位 = 拾取/放下/用药类(Drop* 三支),零第四支。
  - Edge cases: 与 Story 005 的意图路径共测;扣减推导正确性由 fold 测试反证(不重复造轮子)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs` — must exist and pass(本体用例;BLOCKED-BY 子用例 Ignore 挂起)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: item-database story-001/002(Complete,载荷类型面),foraging Story 003 / processing Story 003(两支上游事件的产出形状),`entities.yaml` 七类既有 Kind(零 kindgen 变更)
- Unlocks: Story 002(高水位扫流需要 fold 的读集),Story 004(CarryLoad 消费投影),Story 005(原子性断言基线 = 前后投影比对),Story 006(摆放序输入)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
