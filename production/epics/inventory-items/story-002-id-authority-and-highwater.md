# Story 002: instance_id 铸造权威与高水位重构

> **Epic**: 库存与物品
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/inventory-and-items.md`(规则二 id 铸造 · BL-1/BakedInitial · AC-20-01/02)
**Requirement**: TR-inventory-002(instance_id 铸造权 = 主机唯一,意图上行、id 由主机回填)· TR-inventory-003(高水位重构 next = max(instance_id)+1,扫三流并集,与 patient_id 同机制)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-010(主,§五): 机制 A —— 计数器永不复位 0 + 迁移后 `next = max(id)+1` 由事件流重构 · ADR-006(次,Amendment B): id 空间语义(受伤实体 → +玩家 id,第二十六批扩大适用面)与「终态折叠行须保留 id」纪律 · ADR-005(次): 主机唯一执行/发号
**ADR Decision Summary**: 铸造点 = **实例出现事件当下**,三载体:`DropSpawned`(出生即铸)/ `Craft.output_instance_ids`(18 于点火 tick 铸,已认领 R10)/ `StructureRemoved` 返还新铸;客户端零自铸(上行只有意图,id 由主机回填);高水位扫描集 = **三流 ∪ `BakedInitial`**(R3/BL-1③ —— 不落地基线内的预摆物 id 也进 max,否则重建撞号);不新开第二计数器;`ItemInstanceId.Next()` 与 `patient_id`/`structure_id` 共用 `IIdAuthority` 空间(ADR-016 §二 / ADR-010 §三 义务 12)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数计数器与流扫描,门 A 程序集,零引擎 API;序列化落点归 7a codec(既有件)。

**Control Manifest Rules (this layer)**:
- Required: 铸造唯一入口 = `IIdAuthority.ItemInstanceId.Next()`(反射断言构造点,同 foraging AC-17-05 口径);高水位 = `max(三流 ∪ BakedInitial)`+1 的纯函数重构;迁移/读档路径不持久裸计数器当真值(可从流重构)
- Forbidden: 第二计数器;客户端路径铸 id;把 id 回收复用(永不复位);`BakedInitial` 从表现态场景读取(它是烘焙数据,ADR-022 导出)
- Guardrail: R3(高水位基线含 BakedInitial)**未落地** ⇒ 涉及预摆物撞号的用例挂 BLOCKED-BY-R3,本故事交付扫描器形状与「未落地 ⇒ 可复现撞号反例」的证据,不擅改 7a/54 的落地义务

---

## Acceptance Criteria

*From GDD `design/gdd/inventory-and-items.md`, scoped to this story:*

- [ ] **AC-20-01**: `GIVEN` 20 的代码路径,`WHEN` 检索 `instance_id` 赋值点,`THEN` 唯一来源 = `IIdAuthority.ItemInstanceId.Next()`,零本地计数器(规则二)
- [ ] **AC-20-02**: `GIVEN` 客户端拾取,`WHEN` 检索,`THEN` 零客户端铸 id 路径 —— 意图上行、id 由主机回填(规则二)。⚠️ 上行通道本体(R1 / ADR-001 窄修订)未立 ⇒ P0 单机 = 同一进程主机路径;本条断 **type-surface 零铸造旁路**,联机半边不外纳
- [ ] **AC-20-03 前置子件(R3)**: 高水位扫描集 = 三流 ∪ `BakedInitial` 的纯函数实现落地;`BakedInitial` 缺失时 ⇒ 重建可撞号的反例夹具交付(真修 = 54 导出 `BakedInitial` id 位 + 7a 接入,外抛登记)
- [ ] **AC-20-04 相邻(不重叠声明)**: 堆叠键 `(item_key, quality)` 唯一出处 21a,20 不改写(本体判据见 Story 003;本故事只断 id 空间与堆叠键不相混 —— `instance_id` 全序唯一 vs 堆叠键非唯一,两索引结构不互换)

---

## Implementation Notes

*Derived from ADR-010 §五(主)/ ADR-006 Amendment B:*

1. `IIdAuthority` 实现(若 item-database/sim 既有件已建 `Next()`,本故事只做 20 侧接线与扫描门,零第二实现 —— 先查既有件再动手,承 skill-system/item-database 已建件的复用纪律)。
2. 高水位重构函数 `NextFrom(streams ∪ bakedInitial) = max(id)+1`:扫病史/病例/世界三流 + `BakedInitial` 行;敌人/结构行同空间并扫(ADR-016 §二 / ADR-021 义务 12 的 instance↔structure 共空间语义在此消费)。
3. 折叠交集纪律:终态折叠行必须保留 `patient_id` 的同型义务 → instance 行折叠后 id 仍可被 max 扫到(若 7a 折叠器对 ItemInstance 行有豁免清单,以 ADR-010 义务 9/10 现文为准,不在本故事擅改折叠谓词)。
4. 读档/迁移路径:`next` 从重构值起步(不读快照裸值);快照里的裸计数器(若存在)仅作优化起点,与重构冲突时重构赢(承 AC-20-03「流赢快照」优先级同条)。
5. 客户端零铸造的静态门:type-surface 反射扫 `ItemInstanceId` ctor / `Next()` 调用点的程序集归属(铸造只许出现在主机侧权威实现件内),与 foraging Story 003 的 AC-17-05 共用扫描件。
6. 三载体的铸造时序由上游故事各自认领(DropSpawned=20/17、Craft=18 Story 003、StructureRemoved 返还=23/20);本故事提供单一 `Next()` 入口与撞号防护测试,不重复实现铸造点。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:fold 投影本体(消费 id,不铸造)
- Story 005:拾取意图路径(上行半边归 R1/45,P1b)
- foraging Story 003 / processing Story 003 / modular-building Story 003:各自铸造点的落流时序(本故事只交 `Next()` 件与共空间防护)
- 7a(persistence epic):折叠谓词与 codec(义务 9/10 本体)
- 54 关卡工具:`BakedInitial` 导出含 id 位(R3 的对方,ADR-022)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-20-01**: 唯一铸造源。
  - Given: 20 + 关联 sim 程序集全 type-surface。
  - When: 反射扫 `ItemInstanceId` 构造/赋值点。
  - Then: 命中集 ⊆ {`IIdAuthority` 实现};20 业务代码零 `new ItemInstanceId`;零私有计数器字段。
  - Edge cases: 测试装配豁免口径同 item-database story-011 先例;负样例(注入本地 `++next`)必红。
- **AC-20-02**: 客户端路径零铸。
  - Given: 客户端侧程序集(Gameplay.Presentation/UI)反射面。
  - When: 扫描。
  - Then: 零 `IIdAuthority` 调用(表现层只读 fold 投影中的 id)。
  - Edge cases: 单机 P0「客户端=同进程」⇒ 以程序集归属判,不以运行时判;联机半边显式不测(R1 未立)。
- **高水位重构**: max+1 正确性。
  - Given: 三流含 id {5, 9, 12}(乱序、跨流)+ BakedInitial 含 {20} 的夹具;无 BakedInitial 对照。
  - When: 重构。
  - Then: `next = 21`(含 BakedInitial 正例)/ `next = 13`(对照,证 BakedInitial 参与);迁移后不复用任何旧 id(单调断言)。
  - Edge cases: 空流+空基线 ⇒ next 从机制 A 初值(永不复位 0 语义按 ADR-010 现文);折叠后终态行 id 仍进 max。
- **R3 反例交付**: 撞号可复现。
  - Given: BakedInitial 未参与扫描的旧路径模拟(注入开关)。
  - When: 铸造与基线预摆 id 相交。
  - Then: 撞号发生且被 fold 一致性测试捕获(证明该债真实);修复路径 = R3 落地后摘除开关,同测试翻绿。
  - Edge cases: 本用例是债的证据非验收,不计绿(登记 BLOCKED-BY-R3 的机器可读锚点)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Inventory/id_authority_highwater_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(流扫描器 —— 高水位与 fold 共用全序遍历件),item-database story-008 类先例(实例权威,Complete),ADR-025(铸造件落 `Sim`/`Sim.Contracts` 清单位)
- Unlocks: Story 005(拾取路径引用回填 id),foraging Story 003 / processing Story 003(直调 `Next()` 的权威件),modular-building(返还新铸共用空间防护)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
