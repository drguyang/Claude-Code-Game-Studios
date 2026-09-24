# Story 010: 实例权威与持久化往返

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24(实现 + 执行 VERIFIED)

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-003(Fix 不可经 Unity 内置序列化器承载 · D-21-18)· TR-itemdb-019(instance_id 权威 · IIdAuthority ItemInstanceId Next())· TR-itemdb-020(D-21-27:仅主机铸币)· TR-itemdb-023(物品数据在存档中的编码)· TR-itemdb-031(掉落实体的世界状态事件化边界)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-010(主): 7a 持久化与存档格式 · ADR-005(次): 确定性模拟与状态同步模型 · ADR-006(次): 定点域边界数据契约(Amendment B 机制 A)· ADR-009/015(次): 掉落身份进世界流
**ADR Decision Summary**: ADR-010 规定全二进制 codec(禁 JSON / PlayerPrefs)、校验和 + 自动回退、版本号 + 迁移协议;`ItemInstanceId.Next()` 机制落点归 §五。ADR-006 Amendment B(机制 A)规定 id 计数器永不复位 0,迁移后 `next = max(id) + 1` 由事件流/快照高水位重构。ADR-009 §二规定掉落实体**身份**进世界流(模拟态)、**位置**走表现态(第二 QoS)。ADR-005 规定存档与事件流禁 float,`Fix` 走 ADR-006 自定义编码器(D-21-18:不可经 Unity 内置序列化器)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-010 MEDIUM —— 原子写 / 后台线程 / 哈希须实测;**刻意只用 BCL 稳定 API**,不用 `UnityEngine.Hash128`。ADR-005 HIGH(IL2CPP 逐位性需实测,但本故事编码逻辑本身不依赖 post-cutoff API)。

**Control Manifest Rules (this layer)**:
- Required: 存档 = 全二进制 codec;`Fix` 走自定义编码器(`_raw` long 显式写出/读入),**不可经 Unity 内置序列化器**(D-21-18);`instance_id` 铸造唯一来源 = `IIdAuthority.ItemInstanceId.Next()`(主机侧);迁移后 `next = max(item_id) + 1` 高水位重构,计数器永不复位 0
- Forbidden: JSON / PlayerPrefs 承载存档;客户端本地铸造 instance_id(D-21-26/27);用 Unity 内置序列化器序列 `Fix` readonly struct;读档触发 F1 重算改 qty(Schema E:物化存储不重算)
- Guardrail: 执法体 = 构建期断言 + 单元测试;`PatientId.None = -1` 属事件侧哨兵,**不进物品 id 空间**、不污染高水位(ADR-007 哨兵口径)

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [x] **AC-21a-31**: 含 quality 的 ItemInstance 经 7a 持久化往返 ⇒ item_key 与 quality 完全不变(bit/值级相等);qty 物化存储不重算(Schema E 裁定——往返不得触发 F1 重算改量);children 闭包一致
- [x] **AC-21a-34**: 容器内部为子实例 id 列表,任意增删后 ⇒ 无实例被静默丢弃或复制(容器结构守恒);实例 id 集合与出现次数守恒,Σ 各处 qty 守恒(交叉 AC-33)
- [x] **AC-21a-35**: 物品表标记 deprecated 的条目,读取存档中的对应实例 ⇒ 仍能正确解析(只增不删的验证);「物理删除条目导致存档损坏」的反例在对照组触发失败(证明门有效)
- [x] **AC-21a-53**: Fix 自定义编码器往返(_raw 的 long 显式写出/读入)一组 drug_potency / half_life / axis_offset_by_quality[] ⇒ 值逐位复原(含负值 offset)。⚠️ 断言的是「自定义编码器正确」,不是「Unity 内置序列化器失败」——内置序列化器行为(Unity 跳过 readonly struct 私有字段)由一条 EditMode 探针记录为**观察事实**,不作为断言(防「引擎变好反而测试失败」)
- [x] **AC-21a-58**: 容器实例闭包校验:无 instance_id 同属两容器 / 无自引用 / 容器图无环 / children 内每个子实例均已被登记 —— 容器守恒的结构前提;四形态均硬失败,合法单层容器通过
- [x] **AC-21a-63** [I]: instance_id 铸造路径检索全部铸造点 ⇒ 唯一来源 = `IIdAuthority`(或主机);17 采集不得在客户端本地铸造(D-21-26/D-21-27;迁移后重号 = 物品悄悄合并/丢失的静默失败)

> ⚠️ **AC 勾选口径(禁借绿注)**:六条 AC 的用例全部执行全绿(2026-09-24,EditMode 458/458);
> 但 AC-31/35 的 When 路径「**经 7a 持久化往返**」半边未跑(7a 持久化服务未实现)——
> 承位 = `ItemInstanceCodec` 字节级 encode→decode(ADR-010 存档快照段同一编码路径),
> **该半边 BLOCKED-BY-7a,本故事不宣称已绿**;7a 落地后须补真存档往返并复核
> (口径见 `tests/{integration,unit}/item_database/README.md` §Story 010)。

---

## Implementation Notes

*Derived from ADR-010 (primary) / ADR-005 + ADR-006 (secondary):*

- **全二进制 codec**,校验和 + 自动回退,版本号 + 迁移协议 —— ADR-010 §一/§四/§七
- `ItemInstanceId.Next()` 机制落点 = ADR-010 §五(**ADR-006 Amendment B 机制 A 的物品侧实例**:计数器永不复位 0,迁移后 `next = max(item_id) + 1` 由事件流重构 —— ADR-006 Amendment B §不变量)
- **`Fix` 不可经 Unity 内置序列化器承载**(D-21-18)—— 须自定义编码器,由 EditMode 探针守住(ADR-006 §Decision 二;AC-53 的「自定义编码器正确」与「内置序列化器行为」分列,后者仅记录观察)
- 存档与事件流**禁 float**(ADR-005;AC-53 的 drug_potency / half_life 往返全部走 `_raw` long)
- **掉落实体身份进世界流 / 位置表现**(ADR-009 §二 三态表 + ADR-015 `spawn_anchor` 改整数格)—— 本故事的 instance_id 铸造与存档往返只管**身份**,不管连续位置
- **Schema E:qty 物化存储,不重算**(AC-31)—— 读档不得触发 F1;若实现里读档路径调用了求解器改 qty = 违规
- 执法体 = 构建期断言(AC-58 闭包四形态显式 `throw`)+ 单元测试(AC-63 静态扫描 + 单测调用权威)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: AC-33 堆叠溢出新实例的 qty 守恒(本故事 AC-34 只管容器结构守恒,两者在测试中交叉引用但文件分开)
- Story 009: Craft 事件载荷的 ActualConsumed 只在事件流(本故事 AC-31 的「快照无 ActualConsumed」是负向断言)
- Story 008: ConfigVersion 与存档头字段比对接线(本故事管 codec 本体与 id 铸造)
- Story 011: 跨平台哈希与 IL2CPP 对拍

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-31**: 含 quality 的 ItemInstance 经 7a 持久化往返 ⇒ item_key 与 quality 完全不变。
  - Given: 实例 instance_id、item_key=(base_id, state)、quality ∈ [1, MAX_QUALITY]、qty、children。
  - When: ADR-010 全二进制 codec 保存 → 加载。
  - Then: item_key 两分量与 quality 完全不变(bit/值级相等);qty 物化存储不重算(Schema E 裁定——往返不得触发 F1 重算改量);children 闭包一致。
  - Edge cases: quality=1 与 =MAX 两极;deprecated 条目(交叉 AC-35);空 children(= [],非 null);多实例批量往返。
  - Negative fixture: 无。
  - Suggested test path: `tests/integration/item_database/instance_authority_persistence_test.cs`

- **AC-21a-34**: 容器内部为子实例 id 列表,任意增删后 ⇒ 无实例被静默丢弃或复制(容器结构守恒)。
  - Given: 容器 children:long[] 与全实例登记表;执行若干增/删/移操作序列(确定性固定序列,无随机)。
  - When: 每步操作后核对。
  - Then: 实例 id 集合与出现次数守恒——无 id 消失、无 id 双份、无未登记 id 被引用;Σ 各处 qty 守恒(交叉 AC-33)。
  - Edge cases: 删除容器本身(子件去向按 20 语义,21a 断言不静默丢);移出再移入;满溢出产生的新实例(id 新铸,唯一);容器 qty/quality 恒 1 的不变量。
  - Negative fixture: 无(守恒正向;结构非法归 AC-58 夹具)。
  - Suggested test path: `tests/unit/item_database/instance_authority_persistence_test.cs`(容器守恒子用例,逻辑级)

- **AC-21a-35**: 物品表标记 deprecated 的条目,读取存档中的对应实例 ⇒ 仍能正确解析(只增不删的验证)。
  - Given: 存档含指向 deprecated 条目的实例;物品表该条目 deprecated=true 但未物理删除。
  - When: 读档解析。
  - Then: 实例完整解析(item_key/quality/qty 可读);无静默丢弃、无解析异常;「物理删除条目导致存档损坏」的反例在对照组触发失败(证明门有效)。
  - Edge cases: 条目真被删除(对照组,应失败——政策 = 只增不删);deprecated 条目仍参与堆叠判定(表现归 20);deprecated 条目作配方外键(是否允许归数据轮,本 AC 不裁)。
  - Negative fixture: 无(GDD 未命名;对照组用代码构造删除态)。
  - Suggested test path: `tests/integration/item_database/instance_authority_persistence_test.cs`

- **AC-21a-53**: Fix 自定义编码器往返(_raw 的 long 显式写出/读入)一组 drug_potency / half_life / axis_offset_by_quality[] ⇒ 值逐位复原(含负值 offset)。⚠️ 断言的是「自定义编码器正确」,不是「Unity 内置序列化器失败」——内置序列化器行为(Unity 跳过 readonly struct 私有字段)由一条 EditMode 探针记录为**观察事实**,不作为断言(防「引擎变好反而测试失败」)。
  - Given: Fix 值集合:drug_potency、half_life、axis_offset_by_quality[](含 0、负值、Q16.16 半单位 raw=32768、极大/极小 raw long)。
  - When: 经自定义编码器 write → read。
  - Then: Fix raw 逐位相等(含负值——TR-itemdb-028 舍入方向不涉本条,但符号位不得丢);断言全部落在编码器往返,不断言 Unity 内置序列化的字段丢失。
  - Edge cases: 0;−1 raw;long.MinValue/MaxValue raw(若值域允许,以常量表为界);半整数;全零数组;数组逐元素往返;探针用例单独标记为「记录观察,不计入通过条件」。
  - Negative fixture: 无(纯往返;D-21-18 静态拒收归 AC-27 族)。
  - Suggested test path: `tests/unit/item_database/fix_codec_roundtrip.cs`(GDD 指名文件路径,照录)

- **AC-21a-58**: 容器实例闭包校验:无 instance_id 同属两容器 / 无自引用 / 容器图无环 / children 内每个子实例均已被登记 —— 容器守恒的结构前提。
  - Given: 四种非法结构各一:同 id 在两容器 children 中;容器 children 含自身 id;A→B→A 环;children 指向未登记实例。
  - When: 装配期闭包断言。
  - Then: 四形态均硬失败;合法单层容器(children 全登记、无环、无共享)通过;非容器 children=[] 合法。
  - Edge cases: 多层嵌套(Schema E 禁嵌套实例——容器的 children 只能是叶子 id,嵌套容器即拒);兄弟容器共享同 id(拒);深链无环(过);空容器(过)。
  - Negative fixture: `invalid_container_closure.json`
  - Suggested test path: `tests/unit/item_database/instance_authority_persistence_test.cs`

- **AC-21a-63** [I]: instance_id 铸造路径检索全部铸造点 ⇒ 唯一来源 = IIdAuthority(或主机);17 采集不得在客户端本地铸造(D-21-26/D-21-27;迁移后重号 = 物品悄悄合并/丢失的静默失败)。
  - Given: src 全部铸造点(全局扫描 ItemInstanceId.Next / new instance_id 赋值 / Guid/Random 类 id 生成)。
  - When: 静态检索 + 单元测试调用权威。
  - Then: 铸造点 ⊆ `IIdAuthority.ItemInstanceId.Next()`(主机侧);17 程序集无本地铸造调用;计数器永不复位 0,迁移后 `next = max(item_id)+1` 高水位重构(ADR-006 机制 A);与病人 id 共用机制、不新开计数器。
  - Edge cases: 换名铸造(扫描词表须登记防绕过);客户端侧任何 Guid.NewGuid 型 fallback(拒);迁移重构在存档含 id=0/负哨兵时的行为(PatientId.None=-1 属事件侧,不进物品 id 空间——交叉 ADR-007 哨兵口径,不污染高水位);主机迁移后连续铸造无重号。
  - Negative fixture: 无(GDD 指名即测试文件本身)。
  - Suggested test path: `tests/unit/item_database/id_authority.cs`(GDD 指名文件路径,照录)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/item_database/instance_authority_persistence_test.cs` — must exist and pass
- Logic (AC-34/58/53 子用例): `tests/unit/item_database/instance_authority_persistence_test.cs` + `tests/unit/item_database/fix_codec_roundtrip.cs`

**Status**: [x] Created —— 三条账本四个真身(集成账本与单元账本的
`instance_authority_persistence_test` 共用同一真身):
`unity/Assets/Tests/EditMode/ItemDatabase/instance_authority_persistence_test.cs`(AC-31/34/35/58)+
`fix_codec_roundtrip.cs`(AC-53,GDD 照录名)+ `id_authority.cs`(AC-63,GDD 照录名);
新增生产件 = `Sim/ItemDatabase/{IdAuthority,ContainerClosure,InstanceResolver}.cs` +
`Sim.Codec/ItemInstanceCodec.cs`;负向夹具 `invalid_container_closure.json`(QA 指名,六案例)。
**执行 ✅ VERIFIED 2026-09-24 桌面 batch** —— EditMode **458 全绿**(前批 416 + 本批 42);
过程:首跑 3 编译错(using 缺 / ItemInstance 成员名 Id·ItemKey 实为 InstanceId·Key)+
1 断言语法错 + 1 用例前提错(null-children 须走 default 形),四跑收敛至全绿。
⚠️ 7a 文件级存档往返半边 BLOCKED-BY-7a(见 AC 注,不记绿)。

---

## Dependencies

- Depends on: Story 001(FixParse),Story 002(schema 类型),Story 008(ConfigVersion / 烘焙产物)
- Unlocks: Story 009(快照无 ActualConsumed 的验证依赖本故事编码),Story 011(黄金夹具覆盖存档字节流)
