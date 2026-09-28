# Story 003: 候选集四源构造与整数格输入 —— 禁读表现态位置 / 经流确立格 vs `pending_cell` / `BakedInitial` 第四源

> **Epic**: 交互系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-002(候选集 = **四源**:世界流 `DropSpawned.spawn_anchor` · 6 烘焙逻辑层(POI/资源点/建造槽位)· 13 `IPresentPatients` 只读视图 · **23 `BakedInitial` 第四源**,全部整数格)· TR-interaction-003(禁读表现态位置;判据 = 反射字段类型非 grep)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事回答「argmin 的输入从哪来」。两处承重:**① 第四源** —— 诊所开档自带的门/柜**没有任何流事件**(23 的 `BakedInitial` 是版本化烘焙数据),候选集若只取三源则**开档门柜不可交互**且不会有任何报错 —— 静默失效,`AC-4-22` 是它唯一的证伪夹具;**② 玩家格的取路** —— 规则二裁 (a):**选择计算用「经流确立的格」**(本地 `pending_cell` 只用于预表现),否则同一 tick 两客户端各用各的格 ⇒ 选择分叉,而选择分叉又不在流里,无从重放。13 的读入承 F-4.1b:病人格序列**显式入参**,4 不得自行现生成。

**ADR Governing Implementation**: ADR-016 §三(禁读表现态位置构造/过滤:第二 QoS 到达时序不定 ⇒ 决策非确定性函数;感知输入 = 粗粒度整数格)· ADR-020 §四(玩家位移纯表现态,唯一 sim 投影 = `ActorCellEntered`;本故事 = 该裁决的**读方**半边,玩家 Epic story 004/005 是写方)· ADR-009(三问判据/三态分类:候选集 = 派生态,**由外生源确定性重建,不进流**;`DropSpawned{instance_id, spawn_anchor(WorldPos), …}` 身份进流/位置表现)· ADR-015(四源的格坐标全部住**同一逻辑层整数格**;6 的烘焙逻辑层 = `world_*.cooked`,ADR-022 导出)· ADR-016 §六(13 ↔ 37 单向;4 读 13 的 `IPresentPatients` 只读在场视图,不引用 37)
**ADR Decision Summary**: ADR-016 裁「读方走整数格」、ADR-020 裁「写方跨格发事件」,但**4 的候选集具体从哪几个源、以什么形状装载**从未有 ADR 给出 —— 这是 GDD 规则二的交付面;本故事把「四源 + 具名 DTO + 经流确立格」做成可反射断言的形状。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(构造与过滤为纯 C#;Addressables/烘焙数据读取归 story 006 的装载面,本故事喂注入数据)
**Engine Notes**: `Candidate` 与四源 DTO 是**具名产物**(GDD 首轮点名原稿「4 的候选集构造路径」是无名范畴 ⇒ 无从枚举被测对象;须先命名再断言)。反射对象 = 这些类型定义的**全部实例字段类型**闭包(`AC-4-03`),真身类型住 `Gameplay.Presentation`(非门 A —— 13 的 `IPresentPatients`/`VitalsDto` 消费面物理上引用不到 `Sim`)。运行期读取源(世界流重放视图 / `world_*.cooked` / 23 烘焙表)经 ADR-014 `IDataProvider` 边界,测试侧全部以注入替身驱动。

**Control Manifest Rules (this layer)**:
- Required: 感知/候选输入 = **粗粒度整数格**;掉落 = **身份进流 / 位置表现**,`spawn_anchor` 取 `WorldPos` 整数格 — ADR-016 §三 / ADR-009 §五(manifest Foundation 事件流条目)
- Required: 派生态由外生源(事件流 / 版本化烘焙数据)确定性重建,**不进流、不存档** — ADR-009 §一/§四 · ADR-015 §一(4 的候选集重建 = 该判据在选择层的落实)
- Forbidden: **读表现态连续位置**(`Vector3`/`Transform` 系)构造或过滤候选集 — manifest Feature 表首行(ADR-016 §三:213,216;第二 QoS 到达时序不确定 ⇒ 非确定性函数)
- Forbidden: 把 13 的病人格序列当 4 的内部生成物(须显式夹具入参 — F-4.1b / `AC-4-16` 出处纪律归 story 002)
- Guardrail: chunk 驻留与否**不得改变判定结果**(ADR-015 §四)—— 未驻留 chunk 内的 POI/资源点是否进候选集,GDD 未裁 ⇒ 登记为实现期二选一注释(承 `OQ-6-8`「分块流式,未驻留视为不可达」的 6 侧口径,4 侧对齐须回写注记)

---

## Acceptance Criteria

*From GDD `design/gdd/interaction-system.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [ ] **AC-4-03([A])** —— `GIVEN` **具名产物** `Candidate` 与四源 DTO(`Drop` / `ForageSpot` / `Patient` / `PoiCell` / `BuildSlot` / `Utensil` / `ClinicPanel` / `Door` / `Switch` 的输入形状),`WHEN` **反射**其**全部实例字段类型**,`THEN` **零** `Vector3` / `Vector2` / `Quaternion` / `Transform` / 任何 `UnityEngine.*`。**⚠️ 首轮改**:原稿「4 的候选集构造路径」是**无名范畴** ⇒ 无从枚举被测对象;须先命名再断言(规则二 · ADR-016 §三)
- [ ] **AC-4-20([A])** —— `GIVEN` 两客户端的 `ModalOpen` **不一致**(一开一闭),`WHEN` 双方各按交互键,`THEN` **出境集合的差异不产生任何流内容的分歧**(被拒的意图 = **不存在的出境**);并 `THEN` **6 的主机复验不因「客户端少发一笔」而误判违规**。⇒ 这是 `Accept` 的**替代重放判据**,取代 AC-4-14 原本承担不了的那一半(F-4.2)
- [ ] **AC-4-22([A])** —— `GIVEN` 诊所**开档自带**的门与柜(23 的 `BakedInitial`,**无任何流事件**),`WHEN` 玩家站到其格旁按交互,`THEN` **可被选出并路由**。⇒ **证伪「候选集只有三源」**:漏第四源时本条**必红**(规则二)

---

## Implementation Notes

*Derived from 规则二(四源 + 取路 (a))· 规则四 ①②③ · F-4.1b · Overview 边界层改判:*

- **四源装载器各是一个可注入的只读接口**:① 世界流侧重放视图(`DropSpawned` 锚格 + 未被 `DropClaimed` 移除者)—— 身份取 `instance_id`(经 `IIdAuthority`,D-21-27 不本地铸造);② 6 烘焙逻辑层(POI 定义格 / `ForageSpot` / `BuildSlot`);③ 13 `IPresentPatients` 只读在场视图(病人格,`VitalsDto` **不得**入 `Candidate` —— story 001 `AC-4-05` 已守);④ 23 `BakedInitial`(诊所初始构建物:门/柜/工作台)。**每源的 DTO 是具名类型**,`AC-4-03` 的反射清单 = GDD 点名的九个输入形状逐一 + `Candidate` 本体(全字段类型闭包,`WorldPos` ∈ `Sim.Contracts` 为**允许**类型 —— 非 `UnityEngine.*`,注释写明白名单成员,防止反射扫描器把 `WorldPos` 误报或把 `UnityEngine.Vector3` 放进来)。
- **取路 (a) 的实现形状**:4 的玩家格输入 = **`ActorCellEntered` 重放后的确立格**(每 tick 的「最后确立」值),由 1 侧写方(story 004/005 链)供给;`pending_cell`(表现态本地值)**只**允许出现在预表现路径(UI 高亮候选指示),**不得**进入 `F-4.1` 的输入 —— 判据 = 选择函数的参数表只有确立格(`AC-4-20` 的两客户端夹具即抓此项)。
- **`AC-4-20` 的夹具结构**:客户端 A `ModalOpen=true`(吞意图)/ B `ModalOpen=false`(出境)⇒ 断言流内容 = 仅 B 的一笔,且 6 主机复验(判距 + 幂等 + latch)对「A 少发一笔」无违规判定 —— **被拒意图 = 不存在的出境,无须重放**(F-4.2 核心口径;`Accept` 因此**不需要**进 `AC-4-14` 的重放域)。负边界:若实现把被拒意图也上行 → 流多出无主笔 ⇒ 复验违规红。
- **`AC-4-22` 夹具**:空事件流(`StreamPrefix = ∅`)+ 仅 23 `BakedInitial` 表 → 站门格旁喂 `InteractIntent` → 选出 `Door` 并路由(路由表读 `RoutesTo`,story 006 校验;本夹具注入合法表)。三源实现的失败形态 = 候选集空 → 返回 None → 本条红(GDD 原文点名「必红」)。
- **13 的病人格 = 显式入参**:测试侧以 13 格序列生成器输出(出处标注纪律,`AC-4-16`)或手工 fixture 灌入;4 内**不得**出现病人位置生成/插值代码(表现态平滑归 13/42,选择只见格)。
- **掉落位置的口径**:候选过滤只用 `spawn_anchor`(整数格),`DropClaimed` 后该 `instance_id` 移出(重放视图性质);**禁**读掉落物的表现态位置(ADR-009 §五 同构面)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:程序集可达性扫描器本体(`AC-4-03` 复用其反射机器,本故事只提供 DTO 命名与白名单)
- Story 002:argmin 数学(本故事交付输入,不交付「选谁」)
- Story 004:`R_INTERACT` 邻域裁剪(半径过滤在装载**之后**、argmin 之前,其单源校验 `AC-4-17` 归 004;本故事的夹具集已按注入半径预裁或注明)
- Story 005:`Accept` 门的 `Armed`/`ModalOpen` 读取面(`AC-4-09/10`)—— 本故事只交付「被拒意图不产生出境」的**流侧后果**断言
- Story 006:`RoutesTo`/`SlotLinearKey` 等 4-DC 校验、真实烘焙装载(Addressables 启动预载面)
- 系统 1:`ActorCellEntered` 的发出与归并(story 004/005 链);系统 13:病人格的生成器本体;系统 23:`BakedInitial` 表的制作与烘焙 —— 本故事只**读**其形状
- `R_INTERACT` 真值 / chunk 驻留口径的最终裁决 —— 归用户 / `OQ-6-8` 注记回写轮

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-4-03**: 具名 DTO 反射闭包。
  - Given: `Candidate` + 九个输入形状类型(Drop/ForageSpot/Patient/PoiCell/BuildSlot/Utensil/ClinicPanel/Door/Switch);反射全部实例字段类型(含私有、泛型实参递归)。
  - When: 扫描。
  - Then: 违规集(`Vector3`/`Vector2`/`Quaternion`/`Transform`/`UnityEngine.*`)== ∅;`WorldPos`/`int64`/枚举 ∈ 允许白名单(白名单成员逐个断言,防扫描器空转)。
  - Edge cases: DTO 经基类继承来的引擎类型字段(须被闭包抓到 —— 承 `PresentationDtoGuard` 「仅扫顶层会漏」同款);装箱 `object` 字段藏 `Vector3`(静态反射看不见值 ⇒ 测试夹具以「类型即违规」断言:`object` 字段要求显式白名单标注,否则红 —— 登记该口径为测试侧规则)。
  - Negative fixture: 给 `Drop` 加 `Vector3 visualPos` 影子类型 ⇒ 点名红。

- **AC-4-20**: 模态不一致的出境等值。
  - Given: 双客户端替身(A `ModalOpen=true` / B `=false`,一开一闭双向各跑一遍);6 侧 spy sink + 主机复验器(判距/latch/幂等)。
  - When: 双方同 tick 对同一 POI 各按一次交互键。
  - Then: 流内容只含 B 出境的一笔(等价于单客户端场景);**零**「客户端少发」违规判定;A 的被拒意图无上行、无缓存、无重试。
  - Edge cases: A 先闭后开(同 tick 内 `ModalOpen` 翻转,意图按在闭态)/ B 的格与 A 不同(确立格 vs `pending_cell` 岔口:各用自己的确立格出境,主机复验以**主机侧格**为准 —— GDD 口径「出境产生时刻差异由主机判距/幂等吸收」);双方都开(零出境,合法)。
  - Negative fixture: 「被拒也 Publish」实现 ⇒ 流多一笔 + 复验违规红;「用 `pending_cell` 出境且恰在格边」实现 ⇒ 与确立格场景分歧,红。

- **AC-4-22**: 开档门柜可交互(第四源存在性)。
  - Given: 空三流前缀 + 仅注入 23 `BakedInitial`(诊所门/柜格);注入合法 kinds 表。
  - When: 玩家确立格 = 门旁格,喂 `InteractIntent`。
  - Then: 选出 `Door`(`Kind`+`StableId`)+ 路由到登记拥有方(spy 路由出口,非结算);柜同理。
  - Edge cases: 同格 `BakedInitial` + 后掉落物共存(第四源与第一源同格,KindPriority 决胜 —— 回指 story 002 注入表);建造物**建成**后(走世界流事件的第二形态)与开档自带(第四源)两形态在同一路由面等价。
  - Negative fixture: 三源实现(候选装载器不接第四源)⇒ 返回 None,红(GDD:漏第四源本条必红)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/interaction/candidate_sources_test.cs` — must exist and pass(四源装载 + (a) 取路 + 双客户端出境 + 第四源夹具;EditMode fake tick + spy sink 即可跑,依 `AC-4-12` 的 `[I]`→`[A]` 同款先例,真集成环境**不作本条前提**)
- Logic: `tests/unit/interaction/candidate_dto_reflection_test.cs` — `AC-4-03` 闭包扫描 + 白名单断言

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/`;登记口径 = `tests/integration|unit/interaction/`)
⚠️ 不得借绿:`AC-4-20`/`AC-4-22` 依赖的对侧(1 的 `ActorCellEntered` 写方链、23 的 `BakedInitial` 表形状)以**注入替身**签本故事的读方形状;对侧真身联调归各自 Epic,本绿不豁免之,该注记随证据归档。chunk 驻留口径(未驻留 POI 进不进候选)未裁 ⇒ 夹具两侧都不断言,登记为 BLOCKED-BY:`OQ-6-8` 4 侧对齐回写。

---

## Dependencies

- Depends on: Story 001(边界扫描器复用)/ Story 002(argmin 消费本故事的候选集;注入表机器同源)/ ADR-015 + ADR-022(6/23 烘焙数据形状 = 读取契约,测试用替身,真装载归 story 006)
- Unlocks: Story 004(POI 自报的候选与邻域 = 本故事装载器输出)/ Story 005(`Accept` 门的上游意图流形态在此定型)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_(交付时须附:白名单成员清单 + `object` 装箱口径的测试侧规则注 + 双客户端出境差异全枚举表)
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
