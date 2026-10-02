# Story 004: OccupancyOverlay 合成与占用单一表(F-23-1 缝一)

> **Epic**: 模块化建造
> **Status**: In Review — 双代理评审 5 BLOCKING 已修复(`da04f41`),测试绿 `OccupancyOverlayTest` 6/6;EPIC 级不转 Complete(story 级 AC 勾选未逐条复跑复核)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/modular-building.md`(规则三 占用所有权与 Overlay · F-23-1 合成 · EC-23-04/13 · B3 裁定 · AC-23-02/03/04)
**Requirement**: TR-building-002(Overlay = BakedInitial ⊕ 事件流派生,无第三来源)· TR-building-003(`EffectiveWalkable` 唯一合成点 = 23,13/27/6 只读)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主,§一/§五): 三源不变量 —— Overlay 是派生态(不进流不存档,重建期重建);`EffectiveWalkable` 口径已修订补注(27 侧 GDD 开写时登记读方)· ADR-015(次,§二/§四): 派生态两类源之「版本化烘焙数据」= BakedInitial;chunk 仅脏块优化非坐标原点,Overlay 重算范围 = 变更格及邻接 · ADR-022(次): `BakedInitial` 随骨架版本化导出(评审 B3 落点)· ADR-009(次,§四/§六): 快照若存 Overlay 只是加载加速不是真相
**ADR Decision Summary**: `Overlay(0) := BakedInitial`(开档既有家具的占用真源,评审 B3 —— 否则与「世界流唯一真源」矛盾);`Overlay(t) := BakedInitial ⊕ Structure* 事件序列`(纯函数重放);合成规则 **`EffectiveWalkable(cell) := Nav[cell].walkable ∧ ¬Overlay.blocked(cell)`**,合成唯一在 23,**27/13 只读合成结果、不读 Nav 原件**,**玩家不读**(评审 B2 —— 玩家走感 = CharacterController collide-and-slide)。占用单一表纪律:`slot_occupied` 与 `Overlay.blocked` 是同一张表的两个访问器,不允许两套副本漂移(AC-23-04);`Overlay` 类型无自有存储字段(反射断言)。EC-23-13:`BakedInitial` 不进流、不存档;玩家对初始家具的第一次改动 = 第一条 `StructureRemoved`。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数位掩码逻辑(门 A 内)。实现建议(unity-specialist #6):`Nav` / `Overlay` / `EffectiveWalkable` 按 chunk 存 `ulong` 位掩码 —— 属实现优化,不改变判据形状;`Nav` 原件是 6 的静态烘焙(会话内不变),23 只产 Overlay 局部。

**Control Manifest Rules (this layer)**:
- Required: 重建 = `BakedInitial ⊕ fold(Structure*)` 纯函数(无第三输入;`Overlay` 无自有存储字段);读接口只暴露 `EffectiveWalkable` 合成结果;期望终态表**独立夹具钉死**(qa-lead #8 —— 不得由同一重建代码算出,防循环自证)
- Forbidden: Overlay 进三流/进快照真源(派生态纪律;快照加速缓存 ≠ 真相);27/13/42/1 读 `Nav` 原件或读 `Overlay` 反推占用判定;`UnityEngine.Random`/墙钟入重放;玩家走感接 `EffectiveWalkable`(B2 假边已裁死)
- Guardrail: 重放对拍必须同时跑「纯重放」与「增量维护(逐事件 dirty-block)」两路 —— 两路终态逐格相等才算合成正确(增量实现漂了不会被单路测试抓到)

---

## Acceptance Criteria

*From GDD `design/gdd/modular-building.md`, scoped to this story:*

- [ ] **AC-23-02**: `GIVEN` `StructurePlaced` 序列,`WHEN` 重放,`THEN` 重建的 `slot_occupied` / `structure_at` / `Overlay` 与事件序列**逐位一致**(占用真相 = `BakedInitial ⊕ 事件` 纯函数);**期望终态表由独立夹具钉死**(`effective_walkable.json` 同族独立载体,禁自算自证)
- [ ] **AC-23-03**: `GIVEN` 静态 `Nav` + 23 的 `Overlay`,`WHEN` 合成 `EffectiveWalkable`,`THEN` = `Nav.walkable ∧ ¬Overlay.blocked`,且 27/13 读接口**只暴露合成结果**(无 `Nav` 原件泄漏 —— 接口面断言);**玩家不读**(B2)
- [ ] **AC-23-04**: `GIVEN` 同一占用表,`WHEN` 以 `slot_occupied` 与 `Overlay.blocked` 两访问器读写,`THEN` 两者恒一致。载体 = ① 反射断言 `Overlay` 类型**无任何自有存储字段**(无 `bool[]`/`Dictionary` 成员,`blocked` 必是 `slot_occupied` 纯函数)② Place+Remove 混合序列逐步不变量检查
- [ ] **EC-23-13 基线面**: `GIVEN` 开档(零事件),`THEN` `Overlay(0) = BakedInitial` 生效(医馆既有家具格 blocked = true);`BakedInitial` 不进流不存档;对初始家具第一次改动 ⇒ 第一条 `StructureRemoved`(测试:拆除一把椅子前流空,之后恰一条)
- [ ] **EC-23-04 失效谓词面(23 侧半边)**: `GIVEN` 放置/拆除使某格 blocked 翻转,`WHEN` 下 tick 读 `EffectiveWalkable`,`THEN` 变更格及邻接即时反映(23 交付「变更即重算,不等下一次放置」的可见性;27/13 的重规划触发 = 对方读方义务,本件只证合成新鲜度)

---

## Implementation Notes

*Derived from ADR-016 §一(主)/ 规则三:*

1. 数据结构:`Overlay` = 对 `slot_occupied`(Story 003 实例表占用的格索引,同一张表)的**视图访问器**,零自有字段 —— AC-23-04 反射断言钉死「blocked 是纯函数」而非第二副本。
2. 重建路径(fold):`BakedInitial(骨架版本) ⊕ Structure* 按全序键扫过` → 每 Placed 置位 `OccupiedCells(m, anchor, orientation)`(Story 005 旋转件,P0 恒 0°),每 Removed **释放格集从实例表回查**(载荷无 orientation/variant,规则四注 —— 释放必须以实例表记录为准,禁按目录默认格重算,否则改过朝向的多格模块漏格)。
3. 增量路径(运行期):逐事件 dirty-block 更新(ADR-015 §四③ 脏块口径),与纯重放路径双路对拍。
4. 合成:`EffectiveWalkable(cell) = Nav[cell].walkable ∧ ¬blocked(cell)`,按 chunk `ulong` 位掩码实现(建议,非判据);对外接口 = `IWalkableQuery{ EffectiveWalkable(WorldPos) }` 一类只读形,`Nav` 原件不进 23 暴露面(接口断言 + 引用面审查)。
5. 快照缓存:若 7a 快照带 Overlay 加速位,加载后必须以 `BakedInitial ⊕ 尾段事件` 校验/重建 —— 快照非真相(ADR-009 §六),测试注入「缓存与重放不一致」夹具 ⇒ 重放赢。
6. 读方登记义务不在本件:13 GDD 回填 `EffectiveWalkable` 读方 = `O-23-8`(13 下一轮修订),27 于其 GDD 轮登记 —— 本故事交付 23 侧读接口与合成正确性。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:Placeable 条件② 读占用(同一张表,读方测试在对方)
- Story 003:`Structure*` 事件写入与实例表 fold(本件的输入序列来源)
- Story 005:旋转格集 `OccupiedCells(m, anchor, orientation)`(本件 fold 按默认 orientation=0 桩接)
- Story 27/13(epics: enemy-ai / patient-ai):重规划触发(路径格 ∩ 新 blocked ≠ ∅ ⇒ dirty)与避让 —— 读方义务,`O-23-8` 回写
- ADR-020/系统 1:玩家走感(B2 已裁不经 23;模板 collider 足迹一致性挂 ADR-022 C2,`O-23-9`)
- 7a 快照件:Overlay 缓存字段的落盘形态(本件只交付「缓存非真相」的校验断言)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-23-02**: 重放逐位一致。
  - Given: 混合序列(Placed/Removed/Modified 交错 + 多格模块 + 负偏移锚点);独立钉死的期望终态表夹具。
  - When: 纯重放 + 增量双路。
  - Then: 两路 `slot_occupied`/`structure_at`/`Overlay` 三者与期望表逐格相等;改 Removed 一条 ⇒ 终态可预期变化(敏感性)。
  - Edge cases: 同 tick 多事件按 `Seq` 全序;Removed 释放格集经实例表回查(改朝向后的多格模块拆除不漏格)。
- **AC-23-03**: 合成与接口面。
  - Given: `effective_walkable.json` 夹具(Nav 可走×blocked 四象限)+ 27/13 读接口。
  - When: 逐格求值;反射读接口类型面。
  - Then: 四象限真值表全对;接口只暴露合成结果(无返回 `Nav` 原件/`Overlay` 成员的路径);「玩家读」路径不存在(编译面无入口)。
  - Edge cases: Nav 不可走 ∧ blocked(双真)⇒ false(无重复计数);BakedInitial 格在零事件下即 false。
- **AC-23-04**: 单一表反射门。
  - Given: `Overlay` 类型。
  - When: 反射扫描实例字段。
  - Then: 零自有存储字段(非 static 非 const 的 `bool[]`/`Dictionary`/集合成员 = 红);混合序列 1000 步逐步两访问器一致。
  - Edge cases: 负样例注入 `Dictionary` 缓存字段必红;并发读(51 只读消费者)不要求锁(纯函数无写路径)。
- **EC-23-13**: 基线。
  - Given: 新档 + BakedInitial 含 10 件家具;执行「拆椅子 → 放桌」。
  - When: spy-sink + Overlay 比对。
  - Then: 开局 Overlay = BakedInitial 逐格;流中第一条是 `StructureRemoved`(不是 Placed);BakedInitial 行不出现在流/存档真源。
  - Edge cases: 骨架改版 v2 的旧档重放用旧基线(交叉 Story 001 AC-23-17)。
- **EC-23-04 半边**: 新鲜度。
  - Given: AI 路径格夹具;放置使路径中格 blocked。
  - When: 下 tick 查询。
  - Then: 该格 `EffectiveWalkable = false` 即刻可见(不等下一次事件)。
  - Edge cases: 拆除使玩家所在格变可走 —— sim 不补偿(表现层自然下落归 1,登记不实现)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: 真身 `unity/Assets/Tests/EditMode/ModularBuilding/occupancy_overlay_test.cs`(6/6 全绿)· 账本路径 `EditMode/Building/` 未建(占位),实际落 `EditMode/ModularBuilding/`

**Status**: ✅ 2026-10-01 实现落盘 + EditMode 验证(【超算】batchmode)—— Overlay 合成(`OccupancyOverlayTest` 6/6)

---

## Dependencies

- Depends on: Story 001(BakedInitial 输入),Story 003(Structure* 事件与实例表 fold),Story 005(旋转格集,先以 orientation=0 桩并行可)
- Unlocks: Story 005(Modifiable 的新占用集判空走本表),Story 006(拆除后 blocked 清位即刻重算),enemy-ai / patient-ai epics 的 `EffectiveWalkable` 读接线

---

## Completion Notes

*(placeholder — to be filled at story completion)*

**Code Review**: 双代理评审 2026-10-01 裁 REQUEST_CHANGES(5 BLOCKING:entity check 缺失/错误 · payload bit-packing · 无 `OccupancyOverlay` 独立类型),修复落 `da04f41`(2026-10-01);复跑 `OccupancyOverlayTest` 6/6 全绿。评审报告原文未落 `production/qa/evidence/`(桌面批遗留缺口,依「不得借绿」EPIC 级不转 Complete);全量复跑逐例证据见 `production/qa/evidence/editmode-full-rerun-2026-10-02.md`(实跑 6 例全绿)。
