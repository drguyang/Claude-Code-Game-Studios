# Story 001: 槽位骨架与模块目录装载 + 构建期校验

> **Epic**: 模块化建造
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/modular-building.md`(规则一 数据两面 · 规则二 目录校验 · EC-23-05/06/07 · AC-23-08)
**Requirement**: TR-building-002(Overlay = BakedInitial ⊕ 事件流派生,无第三来源 —— 本故事交付其两个烘焙输入面)· TR-building-008(collider 足迹与逻辑格一致 = 构建期硬失败,ADR-022 C2)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-022(主): 关卡工具 = 骨架唯一作者,`world_buildslots.json` 导出契约,BakedInitial 随骨架版本化 · ADR-015(次): 建造槽位与地形共用单一整数格 `WorldPos` · ADR-014(次): 两阶段烘焙,`ConfigVersion` 内容哈希 · ADR-021(次): 定义(派生态,加载期重建)与状态(模拟态,进流)一分为二
**ADR Decision Summary**: 两套数据:骨架 `world_buildslots.json`(槽位格/`SlotType`/`BuildSlotRegion`/BakedInitial 预摆物,派生态不进流)+ 目录 `build_modules.json`(21a `category = build_part` 扩展:`module_id / module_type / OccupiedCells_local / cost / refundable`);目录构建期校验 = `(0,0) ∈ OccupiedCells_local` ∧ 占用格 ⊆ `BuildSlotRegion`(AC-23-08),不合格**拒绝**;骨架改版(EC-23-05):已放置结构保留、不迁移、`BakedInitial` 随版本走(EC-23-07:旧存档 `module_id` 不在目录 ⇒ 重放保留为**不透明实例**,锚点单格计 blocked,不静默丢弃);槽位骨架全图驻留 `data-core` 预载(EC-23-06:放置判定不因 chunk 驻留与否改变)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 装载与校验为纯数据逻辑(门 A + 边界装载件);MEDIUM 半边(Addressables `data-core` 预载、E-13 启动期硬失败)已由 persistence/item-database 既有件覆盖,本故事只增两组数据键;54 工具侧 Terrain/导出实现不在本件(Tooling 层不进构建)。

**Control Manifest Rules (this layer)**:
- Required: 占用格集 ⊆ 整数格(与地形同系,禁 chunk 局部坐标系,ADR-015 §四);`SlotType` 枚举整数化(外壳识别经标记,非名字清单 —— AC-23-14 的输入面);目录/骨架经 ADR-014 烘 `*.cooked`;`ConfigVersion` 联动(ADR-010 §七 比对)
- Forbidden: 运行期读 JSON;表现态几何(Unity Terrain / collider 原件)反推逻辑格(方向锁死:54 单向导出,呈现模板经 ADR-022 C2 钩与格同源);把骨架行写进世界流(派生态纪律,EC-23-13:`BakedInitial` 不进流不存档)
- Guardrail: 「逻辑格 vs 视觉 footprint」一致性 = 54 侧 C2 **硬失败**(`throw` 级),本故事消费其导出并做装载期防御断言(同 processing Story 002 的双侧分工形)

---

## Acceptance Criteria

*From GDD `design/gdd/modular-building.md`, scoped to this story:*

- [ ] **AC-23-08**: `GIVEN` 模块目录,`WHEN` 校验,`THEN` 可返还模块满足 `cost ≥ ⌈1/DEMOLISH_REFUND_RATIO⌉`(EC-23-08,P0 `R=1` 时自动通过);`(0,0) ∈ OccupiedCells_local` ∧ 占用格 ⊆ `BuildSlotRegion`,否则构建期拒绝
- [ ] **AC-23-17** [L-ADVISORY]: `GIVEN` 槽位骨架改版(EC-23-05),`THEN` 已放置结构跨版本保留,不迁移位置(迁移 = 改写玩家资产,不做)—— 迁移链单测
- [ ] **EC-23-06 装载面**: `GIVEN` 骨架数据,`THEN` 全图驻留(`data-core` 预载),占用查询不受 chunk 驻留影响;放置判定输入与驻留与否零耦合
- [ ] **EC-23-07 防御面**: `GIVEN` `module_id` 不在目录(旧存档/漂移),`THEN` 新放置拒绝(判定⑤);重放时该结构行保留为不透明实例(占位呈现归 42),按锚点单格计 blocked,不静默丢弃 —— 装载/重放侧在本故事,渲染占位半边归呈现故事线

---

## Implementation Notes

*Derived from ADR-022 §三(主)/ ADR-015 §三:*

1. 两烘焙源:`world_buildslots.json`(54 导出,含 `slot_grid` / `SlotType` / `BuildSlotRegion` 多边形整数化 / `BakedInitial` 预摆行)+ `build_modules.json`(作者态,21a 扩 category);经 ADR-014 阶段 2 校验(白名单/schema_version/区间)后入 `data-core`。
2. 目录校验三条(构建期):`(0,0) ∈ OccupiedCells_local`(锚点定义性,否则旋转语义漂);占用格 ⊆ region(几何包容);`refundable ⇒ cost ≥ ⌈1/R⌉`(承 Story 006 舍入例外的配对校验,`⌈1/R⌉ = (65536 + rawR − 1)/rawR` 整数式)。
3. `SlotType` 整数枚举(地基/墙/房顶/家具槽/帐篷例外槽…),外壳 = 经标记识别(AC-23-14 输入);帐篷例外用「骨架外预置 tent 槽」承载(规则六:载荷形状不变,不在代码开洞)。
4. 版本联动:`BakedInitial` 随骨架版本化(EC-23-05)—— 旧存档重放用旧基线,`ConfigVersion` 比对落 7a 头(消费既有件,本故事交付数据形状)。
5. 不透明实例(目录漂移):重放表保留行 + 占用退化为锚点单格(评审 ai-programmer #4 显式裁定);判定⑤与重放分两条码路(新放置拒、旧行留)。
6. 一致性方向锁:54 → 逻辑层导出为唯一流;运行期零反推;`InstantiateAsync` 模板的 collider 足迹与格的一致性检查在 **Tooling C2**(构建失败类),本故事不重 implemented,只钉契约。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:五条件判定(消费本故事数据)
- Story 004:Overlay 合成(消费 `BakedInitial` + Structure* 事件)
- Story 006:Refund 舍入与拆除判定(消费目录 `cost`)
- 54 关卡工具(Tooling 层):导出器本体、C1/C2/C4–C6 检查实现(ADR-022,不进构建)
- item-database epic:`category=build_part` schema 落位(AC-21a 族)
- skeuomorphic-ui(42):占位模板视觉与预览(AC-23-13 对方)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-23-08**: 目录三条构建期拒绝。
  - Given: 负夹具 {缺 (0,0)} / {占用格出 region} / {refundable ∧ cost < ⌈1/R⌉(R=1/2 的合成表)} 各一 + 正例。
  - When: 构建期校验(阶段 2)。
  - Then: 三负例各自具名拒绝;P0 R=1 表自动过第三条;正例过。
  - Edge cases: `R = 0`(拒,禁除零);占用格集为空(非法,(0,0) 已钉);region 为单格 + 模块 2 格 ⇒ 拒。
- **AC-23-17**: 骨架改版不迁移。
  - Given: v1 骨架 + 已放置结构存档;换 v2(槽位移动)。
  - When: 加载重放。
  - Then: 结构行原样、位置不迁;新放置按 v2;旧 `BakedInitial` 基线用于旧存档(版本比对驱动)。
  - Edge cases: v2 删除了旧锚点所在槽 ⇒ 旧行保留(占用仍真)但不透明化路径走 EC-23-07 同码;ADVISORY 级记号。
- **EC-23-06**: 驻留无关性。
  - Given: chunk 驻留状态置 random(合成)。
  - When: 全图占用查询/放置判定输入准备。
  - Then: 结果与驻留标志零相关(骨不在表现层全图驻留)。
  - Edge cases: 医馆 chunk 卸载时执行拆除(返还入箱,跨 chunk 合法,Story 006 联动)。
- **EC-23-07**: 不透明实例。
  - Given: 存档含 `module_id = X`,目录已删 X。
  - When: 新放置 X / 重放含 X 旧行。
  - Then: 新放拒(⑤);旧行留、锚点单格 blocked;实例表可序列化(不透明≠丢弃)。
  - Edge cases: X 恢复入目录 ⇒ 旧行重新透明化(同 structure_id 连续身份);多格模块退化单格的保守性断言(blocked ⊆ 原占用)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Building/skeleton_catalog_load_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: item-database story-001/002(Complete,`FixParse`/schema 基建),ADR-022 工具导出件(54,编辑期;未就绪时以合成导出夹具先行,形状义务不豁免)
- Unlocks: Story 002(判定①③⑤数据源),Story 004(`Overlay(0) := BakedInitial`),Story 006(cost/region 校验联动)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
