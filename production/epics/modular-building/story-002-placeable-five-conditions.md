# Story 002: 放置五条件判定 Placeable(全整数,零 Physics)

> **Epic**: 模块化建造
> **Status**: In Review — 双代理评审 REQUEST_CHANGES 5 BLOCKING(`da04f41` 修复,逐条对账件未落 evidence),测试绿 `PlaceableCheckerTest` 10/10;EPIC 级不转 Complete(对账件未落 evidence · story 级 AC 勾选未逐条复跑复核)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/modular-building.md`(F-23-2 放置五条件 · 规则四 载荷 · EC-23-01/03/12 · 缝二 实体判定)
**Requirement**: TR-building-004(全部放置/可居判定走整数格,禁 Physics/NavMesh 进入判定面,⚠️ partial —— 「禁 Physics」由禁令推定,执法断言归实现轮 = 本故事的反射/扫描门)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主,§三/§五): 单一整数格;医馆「红石逻辑」= 整数格邻接判定(非物理非 NavMesh)· ADR-009(次,§七 拾取同构): 意图 + 当下判定 + 拒绝零事件 · ADR-016(次,§三): 实体位置读粗粒度整数格(`ActorCellEntered`/sim 原生),禁读表现态 · ADR-020(次,§四): 玩家跨格事件 = 格读数唯一来源
**ADR Decision Summary**: `Placeable ⟺ ①锚点 ∈ 允许格集 ∧ TypeOK(SlotType(anchor), module_type) ②∀cell(占用为空) ③∀cell ∈ BuildSlotRegion ④骨架未改(P0 恒真) ⑤module ∈ 目录 ∧ Stock(20,m) ≥ 1 ∧ ∀cell ¬EntityOnCell`。全部整数,禁 float;条件⑤ 的实体判定 = **量化格 + 宽容半径**判定式(实体集 = 玩家 + 敌人,经 `ActorCellEntered`/sim 原生;**13 病人不查**;**零 Physics 查询** —— 评审 B4/unity-specialist #2);拒绝 ⇒ **零 Append**(预览 = 42 侧,取消零事件 EC-23-12)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 判定全在门 A 整数层(反射断言把「零 Physics」从禁令升级为执法面,恰结 TR-building-004 partial);库存半边 `Stock(20,m)` 走 inventory-items 投影读数,零跨程序集引擎调用。

**Control Manifest Rules (this layer)**:
- Required: 五条件具名函数逐条可单测(每条件独立谓词,合取在顶层);⑤ 的实体位 = 整数格量化 + 宽容半径;拒绝路径在 Append 之前(结构性零事件);判定输入 = 当下(意图事件 + 主机当下判,承拾取三段式)
- Forbidden: `Physics.*` / `NavMesh.*` / `Collider.*` 符号入判定路径(反射/IL 扫描零命中);float 中间量;读玩家/敌人连续位置(表现态);13 病人入实体集(裁量已明:不查)
- Guardrail: ④ 恒真不删判据(P0 以「骨架 API 不存在」结构性保证 + 保留位注释,禁为测试注入可变骨架);P0 无原子 swap(先拆后放,递归一步)⇒ 本判定不处理「替换」语义

---

## Acceptance Criteria

*From GDD `design/gdd/modular-building.md`, scoped to this story:*

- [ ] **AC-23-01**: `GIVEN` 槽位骨架 + 模块目录,`WHEN` 执行 F-23-2 五条件,`THEN` ①锚点 ∈ 允许格集 ∧ 类型匹配 ②占用全空 ③占用格全在 `BuildSlotRegion` 内 ④骨架未改 ⑤目录 + 库存可及 ∧ 占用格无实体 —— 任一不满足 `Placeable = false` 且不 Append 任何事件。⚠️ 条件④ P0 恒真,矩阵以 ①/②/③/⑤ 为主(载体 `placeable_cases.json` 含负偏移越界用例)
- [ ] **TR-building-004 执法面(本故事交付)**: `GIVEN` 23 判定程序集,`WHEN` 反射/IL 符号扫描,`THEN` `Physics` / `NavMesh` / `Collider` / `Vector3` 在判定路径零命中(把「由禁令推定」升为机器断言,结 partial 的实现轮义务半边)
- [ ] **EC-23-12 机制半边**: `GIVEN` 放置预览(42 渲染层),`WHEN` 玩家取消,`THEN` sim 零事件(预览不触判定写路径;本故事只断「判定纯函数无副作用」,spy-sink 零 Append)
- [ ] **条件⑤ 实体判定式**: 量化格 + 宽容半径的整数判据(实体在任一所占格 ⇒ 拒);玩家 + 敌人在册,13 病人不查(以「病人实例在场且格上」夹具反证判定不受影响)

---

## Implementation Notes

*Derived from ADR-015 §五(主)/ ADR-016 §三:*

1. 谓词分解:`SlotInSkeleton(anchor) ∧ TypeOK ∧ CellsFree(occupied(anchor, module, orientation)) ∧ WithinRegion ∧ SkeletonUnchanged ∧ CatalogAndStock ∧ NoEntityOnCells` —— 顶层合取,短路次序按成本(数据校验 → 占用 → 区域 → 库存 → 实体);结果带**具名失败原因枚举**(供 42 呈现「拒绝类别」,零负信令事件,承 20 AC-20-25 同纪律)。
2. 占用查询走 Story 004 的 `Overlay/slot_occupied` 同一张表(AC-23-04 同表纪律 —— 判定与合成共用一份占用,零副本);旋转格集 = Story 005 的整数旋转件(先桩 `orientation = 0`)。
3. `Stock(20, m)` = 20 投影读数(`countByItemKey(build_part)`,整数);18/17 式「不发明结算」同构:23 不判库存语义,只问数。
4. 实体格集来源:玩家/敌人 `ActorCellEntered` 的最新格(sim 世界流读数,ADR-020 §四 的写方产物)+ 敌人 sim 原生占用;宽容半径 = 烘焙 int(值归数值轮,形状 = `d∞(cell, entityCell) ≤ tol`)。
5. 拒绝 ⇒ 零事件由结构保证:判定纯函数 → 编排层(Story 003)只在 true 分支铸造/Append;测试用 spy-sink 对 false 全矩阵穷举(每条件至少一反例)。
6. `placeable_cases.json` 夹具矩阵:五条件 × 正/反 × 负偏移越界(GDD 载体点名)—— 合成夹具不豁免(AC-18-25 同纪律)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:通过后的 Append/铸造序(本件只产 true/false + 原因)
- Story 004:占用表与 Overlay 本体(本件是读方之一)
- Story 005:四向旋转与 `Modifiable`(本件先钉 orientation=0)
- Story 006:拆除判定与返还(同表读方)
- inventory-items epic:`Stock` 计数语义(20 读数)
- 42:预览渲染与取消交互(EC-23-12 呈现半边;AC-23-13 写者守卫在 Story 003)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-23-01**: 五条件矩阵。
  - Given: `placeable_cases.json`:每条件独立反例(锚点出集 / TypeOK 破 / 占用非空 / 越 region / 目录缺 / 库存 0 / 实体占格)+ 全正例 + 负偏移越界格集。
  - When: 判定。
  - Then: 每反例 false 且原因具名;全正例 true;**spy-sink 在所有 false 用例上零事件**。
  - Edge cases: 多条件同时破(原因取短路首条,允许);单格模块;`d∞` 恰在宽容半径边界(取等语义钉死)。
- **执法扫描**: 零 Physics/NavMesh/Vector3。
  - Given: 23 判定程序集 IL。
  - When: 符号扫描。
  - Then: 零命中(边界:呈现适配层不在该程序集);负样例注入 `Physics.OverlapBox` 必红。
  - Edge cases: `WorldPos` 整数类型(非 Vector3)类型级断言;宽容半径 int 非 float。
- **条件⑤ 实体**:
  - Given: 玩家/敌人夹具在占用格任一格(含旋转后格);13 病人格上在场对照。
  - When: 判定。
  - Then: 玩家/敌人 ⇒ 拒;病人 ⇒ 不影响(正查反查双向断言,禁「顺带查了病人」的实现漂)。
  - Edge cases: 实体恰跨两格边界(取 `ActorCellEntered` 最新格,单格归属);敌人昏迷行(仍算实体 —— 占用格语义)。
- **EC-23-12 半边**: 判定无副作用。
  - Given: 同输入重复判定 100 次(预览反复进出)。
  - When: spy-sink。
  - Then: 恒零事件、结果稳定(纯函数);42 取消路径不经 23 写口(接缝断言:预览 API 面只读)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: 真身 `unity/Assets/Tests/EditMode/ModularBuilding/placeable_checker_test.cs`(10/10 全绿)· 账本路径 `EditMode/Building/` 未建(占位),实际落 `EditMode/ModularBuilding/`

**Status**: ✅ 2026-10-01 实现落盘 + EditMode 验证(【超算】batchmode)—— 放置五条件(真身 `PlaceableCheckerTest` 10/10)

---

## Dependencies

- Depends on: Story 001(骨架/目录数据),inventory-items Story 001(Stock 读数),modular-building 与 20 的边界桩(可并行)
- Unlocks: Story 003(true 分支编排),Story 004(占用表读方接线),Story 005(旋转格集进条件②③)

---

## Completion Notes

*(placeholder — to be filled at story completion)*

**Code Review**: 双代理评审 2026-10-01 裁 REQUEST_CHANGES(5 BLOCKING:entity check 缺失/错误 · payload bit-packing · 无 `OccupancyOverlay` 独立类型),修复落 `da04f41`(2026-10-01);复跑 `PlaceableCheckerTest` 10/10 全绿。逐 BLOCKING 对账件未落 `production/qa/evidence/`(桌面批遗留缺口 —— `da04f41` 只给 commit message 自述,依「不得借绿」EPIC 级不转 Complete);全量复跑逐例证据见 `production/qa/evidence/editmode-full-rerun-2026-10-02.md`(实跑 10 例全绿)。
