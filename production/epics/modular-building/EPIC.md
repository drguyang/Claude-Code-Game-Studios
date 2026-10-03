# Epic: 模块化建造

> **Layer**: Core
> **GDD**: design/gdd/modular-building.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 世界流 Structure* 三支唯一写者
> **Status**: **Complete ✅ 2026-10-03**(7/7 story 已实现;两轮评审判据缺陷 C1/C2/N-r1/C8-ID 全闭;本轮 `2204/2163/0红` 全量复跑 + ModularBuilding **72/72**;未闭项 = **N-r2** 生产装配根,已登记不阻塞 —— 详见 §Epic Status)
> **Stories**: 7 stories — see table below

## Overview

模块化建造(23)在 P0 是「固定医馆 + 小改造」:23 拥有三样东西 —— **槽位占用真相**、**`OccupancyOverlay` 合成**(F-23-1 `EffectiveWalkable = Nav[cell].walkable ∧ ¬Overlay.blocked(cell)`,合成唯一在 23,27/13 只读合成结果,玩家不读)、**`StructurePlaced` / `StructureRemoved` / `StructureModified` 三支世界流 Kind 的唯一写者**。两套烘焙数据:骨架 `world_buildslots.json`(派生态,54 关卡工具唯一作者,ADR-022 §三,含 `BakedInitial`)+ 模块目录 `build_modules.json`(category=build_part 扩展归 21a;构建期校验 `(0,0) ∈ OccupiedCells_local` ∧ 占用格 ⊆ `BuildSlotRegion`)。放置判定五条件全整数(锚点∈骨架∧类型匹配 / 占用全空 / 占用格在区域内 / 骨架未改[P0 恒真] / 目录∧库存可及∧格上无实体),拒绝 ⇒ 零 Append;`structure_id` 与 `ItemInstanceId` 共用 `IIdAuthority` 空间(ADR-010 义务 12,折叠谓词不适用结构行);朝向 = 四向整数旋转 `R(90)(x,y)=(−y,x)`,P0 目录 ⊆ {0°} 但判定代码须支持四向(P1a 零改码判据 AC-23-15);拆除返还 `Refund = ⌊cost × R⌋` **向 −∞ 截断**(显式舍入例外注,非 ROUND_HALF_AWAY),返还失败走 `DropSpawned` 不静默吞。P0 范围门:外壳(墙/地基/房顶)不可动(SlotType 标记识别)、唯一新建例外 = 序章帐篷、无原子 swap、**禁嵌套容器**(承任务规则,与 20 BL-7③ 同条纪律)。承重 ADR:ADR-015(单一整数格 · 模块化网格)/ ADR-009+Amendment J / ADR-022(54 作者契约 + C2 足迹同源)/ ADR-016(读方义务)/ ADR-024 / ADR-025(程序集边界)。⚠️ 记号警示:23 的 AC 表图例 `[L] = 逻辑(EditMode 纯逻辑)`,**与 17/18/20 的 `[L] = 人工走查` 不同** —— 故 23 的 AC 全部映射为 Logic/Integration 型故事判据,无走查挂起项。数值(`R` 返还比例 / `BUILD_TIME` / 目录成本)归用户数值轮;`AC-23-09` 序列化字节稳定的刷新政策绑 ADR-012 §三(矩阵未建 ⇒ 本仓先钉 Mono 往返等值,跨平台签名外抛)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-015: 世界几何 | 单一整数格 `WorldPos(i32×3)` 地形/建造槽位共用;模块化网格否决 Voxel;NavMesh 仅表现态 | LOW |
| ADR-009: 世界状态事件化边界 | Structure* 三支进世界流;Overlay/实例表 = 派生态不进流不存档;Amendment J 全序键 | MEDIUM |
| ADR-022: 关卡工具 | 骨架唯一作者 = 54;`world_buildslots.json` 导出契约;C2 collider 足迹同源(硬失败);BakedInitial | MEDIUM |
| ADR-016: AI 架构 | 13/27 只读合成 `EffectiveWalkable`(缝一消费方);感知禁读表现态 | MEDIUM |
| ADR-010: 7a 持久化 | §三 义务 12 structure_id 共用 id 空间;§一 世界流序列化字节面 | MEDIUM |
| ADR-006: 定点域边界 | Refund Q16.16 截断例外的显式注;载荷 ⊆ 整数域 | MEDIUM |
| ADR-024: Kind 单一真源 | 三支 Structure* 已在 registry;kindgen A1–A5 | LOW |
| ADR-025: 契约程序集清单 | 23 住 `Sim`/`Gameplay.Presentation` 边界;零呈现引用可断言 | LOW |
| ADR-013: 拟物 UI | 42 只渲染;放置预览非写者(AC-23-13 的对方) | HIGH |

**Engine Risk**: **MEDIUM**(整体)。判定/合成/载荷本体为门 A 纯整数(LOW);MEDIUM 面 = ① Addressables `data-core` 预载占位模板与 `InstantiateAsync/Release` 泄漏纪律(ADR-023 拆序六步,E-13);② 54 工具侧 Terrain/导航导出(不进构建,ADR-022 已注);③ 黄金夹具刷新绑 ADR-012(未建,EXTERNAL 半边)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-building-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-28 实测):**10 条 = 7 covered + 3 partial**。
> partial:`TR-building-004`(禁 Physics 由禁令推定,执法断言归实现轮 —— 即本 epic Story 002 的反射/扫描门)、
> `TR-building-009`(Refund 截断例外未回写 ADR-006 守恒律 —— 回写轮义务)、
> `TR-building-010`(「P1a 零代码」本身是未验断言 —— Story 005 注入夹具即其首验载体)。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-building-001 | 三个 Structure* Kind(放置/拆除/状态变更)落世界流 | ADR-009 ✅ |
| TR-building-002 | Overlay = BakedInitial ⊕ 事件流派生,无第三来源 | ADR-022+009 ✅ |
| TR-building-003 | EffectiveWalkable 唯一合成点 = 23(13/27/6 只读) | ADR-016 ✅ |
| TR-building-004 | 全部放置/可居判定走整数格,禁 Physics/NavMesh 进判定面 | ADR-015 ⚠️ partial |
| TR-building-005 | structure_id 与病人共用 IIdAuthority 空间;折叠不适用结构行 | ADR-006+016 ✅ |
| TR-building-006 | 建造重放字节稳定:绑 ADR-012 黄金夹具(单元+集成双层) | ADR-012 ✅ |
| TR-building-007 | 23 程序集零呈现引用;42 不是建造状态写者 | ADR-025+013 ✅ |
| TR-building-008 | collider 足迹与逻辑格一致 = 构建期硬失败(不一致 throw) | ADR-022 ✅ |
| TR-building-009 | Refund 截断例外未回写 ADR-006 守恒律 | ADR-006 ⚠️ partial |
| TR-building-010 | P1a 建造内容扩张判据 = 零代码改动(纯数据表新增) | ADR-015 ⚠️ partial |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/modular-building.md`(AC-23-01…18,[L]=EditMode 逻辑)are verified
- AC-23-09 的跨平台签名半边按 ADR-012 现状处置(Mono 往返等值先绿,矩阵落地后补签,禁单平台独签)
- Logic/Integration 测试全绿;O-23-* 义务清单回写闭合
- 数值轮交付 `R` / `BUILD_TIME` / 目录成本后,返还与代价测试翻真值

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 骨架与模块目录装载 + 构建期校验 | Logic | In Review — `BuildSlotCatalogTest` **7/7**(CLI 坐实,原记 9/9) | ADR-022/014/015 |
| 002 | 放置五条件判定 Placeable(全整数) | Logic | In Review — `PlaceableCheckerTest` 10/10 | ADR-015/009 |
| 003 | Structure* 三 Kind 载荷、写权与实例表 | Integration | In Review — `StructureKindsTest` 7/7 + `WorldTest` 8/8 | ADR-009/010/024 |
| 004 | OccupancyOverlay 合成与占用单一表 | Logic | In Review — `OccupancyOverlayTest` 6/6 | ADR-016/022 |
| 005 | 朝向整数旋转 + StructureModified | Logic | In Review — `ModifiableCheckerTest` 5/5 | ADR-006/009 |
| 006 | 拆除、返还舍入例外与 P0 范围门 | Logic | In Review — `DemolishCheckerTest` 3/3 + `RefundCalculatorTest` 5/5 | ADR-006/015/022 |
| 007 | 结构载荷接线 —— `Structure*` 改走 `IPayloadEncoder`(闭合 B4) | Logic | **Complete ✅ 2026-10-02**(12/12) | ADR-029 + ADR-015 |

## Epic Status

**Complete ✅ 2026-10-03**(7/7 story 已实现;两轮评审判据缺陷全闭)。

**Test Evidence(2026-10-03 batchmode 实测)**:ModularBuilding **72/72**
**= 72 Passed + 0 Failed + 0 Skipped + 0 Inconclusive**,
逐 fixture:`StructurePayloadEncoderTest` 12 · `PlaceableCheckerTest` 10 · `StructureKindsTest` 9 ·
`WorldTest` 8 · `BuildSlotCatalogTest` 7 · `StructureOccupancyWiringTest` 7 · `OccupancyOverlayTest` 6 ·
`ModifiableCheckerTest` 5 · `RefundCalculatorTest` 5 · `DemolishCheckerTest` 3。
全量 EditMode 同批复跑 **`2204 = 2163 过 + 0 红 + 1 inconclusive(既有 Audio)+ 40 跳过`**
—— 与 player-controller 轮基线(`2202/2161/0/1/40`)逐位比对 **恰 +2**(本轮两条新回归用例),**零回归**。
证据原件:`production/qa/evidence/modular-building/editmode-rerun-2026-10-03.md`。
⚠️ 旧计数(51/51 · 63/63 · 70/70)系不同快照,**不再作为依据**(承「不得借绿」)。

### 逐条判据缺陷结算(两轮评审 → 当前 HEAD 实测)

| # | 原判定 | 当前 HEAD 实测 | 结算 |
|---|---|---|---|
| **C1** 占用格集恒 = `{anchor}`(多格模块足迹不入实例表) | 🔴 真缺陷 | 改由调用方传入**真足迹**(`PlaceableChecker.ComputeOccupiedCells`,与放置判定**同源**);`test_c1_register_usesFullFootprint_notAnchorOnly` 钉死 | ✅ **已修** |
| **C2** `World.OccupyCells/FreeCells` 零调用方 ⇒ Overlay 写路径未接线 | 🔴 真缺陷 | `Place`/`Remove` 接 `IWorldOccupancy`;`test_c2_place_writesOccupancy` / `test_c2_remove_freesOccupancy` 钉死 | ✅ **已修** |
| **N-r1** `moduleCatalog == null` 时静默退化(复现 C1/C2) | 🔴 真缺陷 | 改 **fail-closed**:未注入目录 / 未知 moduleId / 未注入 occupancy 三处**抛**;三条 `test_nr1_*_throwsFailClosed` 钉死 | ✅ **已修(真到位)** |
| **C8/ID** 注册表 id `int` vs 权威件 `i64` | 🔴 真缺陷 | **本轮修复**(2026-10-03)—— 全链升 `long`,与契约 / codec / `entities.yaml:2068` 口径一致;两条回归用例 + 两次突变测试 | ✅ **已修** |
| **N-r2** 无生产装配根(非测试代码 `new World(` / `new StructureWriter(` 零引用) | ⚠️ 缺口(低-中) | 实测仍为 **0 引用**;ADR-023 三场景制下 `World.unity` 尚不存在,现在补装配点 = **孤岛接线**(零消费者、其自身正确性无处验证) | ⚠️ **登记为待办(用户裁定 2026-10-03),不阻塞本 epic** |

### 未闭项登记(不阻塞本 epic 转 Complete)

- ⚠️ **N-r2 —— 生产装配根**:实缺陷仍开。**重开条件** = ADR-023 三场景制落地 / Boot 装配轮开工时,
  同批补 `World` + `StructureWriter` 的生产装配入口(`IModuleCatalog` / `IWorldOccupancy` /
  `IPayloadEncoder` 注入),并补该接线自身的集成判据。
- ⏸️ **AC-23-09 跨平台签名半边**:按 ADR-012 现状处置 —— 本仓先钉 **Mono 往返等值**(已绿),
  **跨平台黄金夹具矩阵未建**,落地后补签,**禁单平台独签**。
- ⏸️ **`TR-building-004` / `-009` / `-010` 三条 `⚠️ partial`**:归数值轮 / P1a,非本 epic 阻塞项。
- ⏸️ **story-004 `World.unity` 构建期扫描**(ADR-023 三场景制):场景文件尚不存在,重开条件 = 该场景创建时同批补。

### 文档卫生订正(2026-10-03,零行为影响)

- `structure_kinds_test.cs` §已知缺陷 段(原写「id 用 `(int)` 收窄作临时桥接,归独立轮」)
  → 改为 §历史缺陷(**已闭**),保留闭环记录(【原缺陷】/【原处置】/【已修】)。
- `StructureKinds.cs` 头注的「本文件三支 payload struct 副本为零引用死代码」记录保留 ——
  该删除(2026-10-02)与 id 宽度修复(2026-10-03)是两个独立事件,不合并不删注。
- EPIC 原 §Status 行(约 900 字单体字符串,内含「不转 Complete」的旧判定与已闭项混杂)
  → 收敛为一行 `Complete` + 本节逐条结算,**避免读者从单体字符串里读到已失效的结论**。

## Next Step

**本 Epic 已 Complete(2026-10-03)** —— 无剩余**本 epic 范围内**的实现工作。
等待事项均属外部主语 / 外部轮次:

- **N-r2 生产装配根** ⇒ ADR-023 三场景制 / Boot 装配轮开工时同批补(带该接线自身的集成判据)。
- **AC-23-09 跨平台签名** ⇒ ADR-012 黄金夹具矩阵落地后补签(禁单平台独签)。
- **`TR-building-004/-009/-010`** ⇒ 数值轮交付 `R` / `BUILD_TIME` / 目录成本后翻真值;`-010` 归 P1a。
- **story-004 `World.unity` 构建期扫描** ⇒ 该场景创建时同批补。

下一件:见 `production/epics/index.md` —— Route A 三 epic(player-controller → world-ecozones → modular-building)
**全部收口完毕**。
