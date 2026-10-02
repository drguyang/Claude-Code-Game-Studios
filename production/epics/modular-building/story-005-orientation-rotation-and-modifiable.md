# Story 005: 朝向整数旋转(F-23-2b)+ Modifiable 改造判定

> **Epic**: 模块化建造
> **Status**: In Review — 双代理评审 REQUEST_CHANGES 5 BLOCKING(`da04f41` 修复,逐条对账件未落 evidence),测试绿 `ModifiableCheckerTest` 5/5;EPIC 级不转 Complete(对账件未落 evidence · story 级 AC 勾选未逐条复跑复核)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/modular-building.md`(F-23-2b 整数旋转 · 规则十 改造 · EC-23-10 · AC-23-05/06/11/15)
**Requirement**: TR-building-010(P1a 建造内容扩张判据 = 零代码改动,纯数据表新增 ⚠️ partial —— 「零代码」本身是未验断言,本故事的注入夹具即其首验载体;实现轮回写义务在夹具转绿后结)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主,§三/§五): `WorldPos(i32×3)` 单一整数格;朝向 = 整数枚举,禁浮点旋转矩阵 · ADR-006(次): 整数域纪律(旋转零中间量,无定点参与)· ADR-009(次,Amendment J): `StructureModified` 三支之一,载荷 ⊆ 整数域(kindgen A2)· ADR-005(次): 确定性 —— 旋转是纯整数函数,重放逐位可复现
**ADR Decision Summary**: `OccupiedCells(m, anchor, orientation) := { anchor + R(orientation)·local_offset }`;**仅四向**,整数置换 `R(90°)(x, y) = (−y, x)`(180° = 两次复合,270° = 三次;禁止任意角度)。旋转作用于**水平平面**(竖坐标不参与,`R` 的 x/y 是水平两轴;顶层构建恒 `y = 0`,楼层 = P2 不在本 GDD)。**P0 目录朝向集合 ⊆ {0°}**(简化夹具,不引入旋转内容)—— 但**判定代码必须支持四向**,否则 P1a 改代码 = 回归(AC-23-06 判据与内容的分离:测试注入合成四向目录)。改朝向判定:`Modifiable(m, anchor, new_orientation) := 新占用格集 ∖ 自身旧格 全空 ∧ TypeOK(新朝向)` —— **现在写**,不在 P0 目录生效时潜伏(规则十)。`StructureModified` 的 `modified_fields` 只载**物理形态位**(朝向/变体),**不载任何数值结果**(数值 = 24 的派生,24 **不发** `StructureModified`);EC-23-10:改朝向**不发 `Removed+Placed` 对**(同一 `structure_id` 连续身份)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 全整数置换 + 谓词,门 A 内纯逻辑。类型级反射断言(AC-23-05 载体升级:字段/参数类型 ∈ 整数集,与 `PresentationDtoGuard` AC-37-15 同构;grep 仅辅助)。

**Control Manifest Rules (this layer)**:
- Required: `R` 由**单一置换函数**实现(90° 基元复合出 180/270,禁四份手抄坐标表漂移);`Modifiable` 的「∖ 自身旧格」差集先行(旋转扫自身现格,否则原地转 90° 必假阳);目录 `orientation_set` 为 int 枚举子集,构建期校验 ⊆ 四向
- Forbidden: `Quaternion` / `Mathf` / float 旋转矩阵入判定路径;任意角度;`modified_fields` 携带数值(24 派生侧);换模块位(白名单外,P1a);改朝向走 Removed+Placed 偷懒路径
- Guardrail: P0 真实目录 ⊆ {0°} ⇒ 四向用例**全部来自合成注入夹具**(AC-23-15 载体点名)—— 夹具的形状义务不豁免(AC-18-25 同纪律);TR-building-010 的 partial 处置 = 本夹具转绿后由回写轮改 covered,故事不自宣

---

## Acceptance Criteria

*From GDD `design/gdd/modular-building.md`, scoped to this story:*

- [ ] **AC-23-05**: `GIVEN` 四向朝向的模块(合成夹具),`WHEN` 应用 F-23-2b 整数旋转,`THEN` 占用格集正确且**零浮点中间量**;载体升级 = 类型级反射断言(旋转模块的字段/参数类型 ∈ 整数集,与 AC-37-15 同构),grep 仅辅助
- [ ] **AC-23-06**: `GIVEN` 判定代码,`THEN` 支持四向朝向(**即使 P0 目录 ⊆ {0°}**)—— `rotation_cases.json` 含**合成目录四向条目**,驱动 Placeable / 重放走 `orientation ∈ {90, 180, 270}` 的**可执行用例**;改朝向走 `Modifiable`(规则十)
- [ ] **AC-23-11**: `GIVEN` `StructureModified`,`THEN` `modified_fields` 只载物理形态位(朝向/变体),**不载任何数值结果**(规则十:数值 = 24 派生);改朝向须过 `Modifiable` 占用检查
- [ ] **AC-23-15**: `GIVEN` P1a 自由建造设想,`THEN` 只需烘焙更大骨架 + 更宽目录,**sim 代码零改动**(`Structure*` 载荷不变)—— 测试期注入「P1a 形状」夹具(更大骨架 + 含旋转的更宽目录),放置此前未见过的格**必须成功**,全程不改代码。**⚠️ TR-building-010 partial 注**:夹具转绿 = 「零代码」断言的首验;registry 改 covered 归回写轮,本故事不自宣结案
- [ ] **EC-23-10 连续身份**: 改朝向序列后流中恰一条 `StructureModified`(同 `structure_id`),**无 `Removed+Placed` 对**(反证测试在 Story 003 编排纪律基础上闭环);变体位(`variant`)与朝向位可同条共载,`modified_fields` 按位标记

---

## Implementation Notes

*Derived from ADR-015 §五(主)/ F-23-2b:*

1. `Rotate90(offset) := (-offset.u, offset.v)`(水平面两轴记 u/v 避免与 `WorldPos` 竖直分量混淆);`R(k·90°) = Rotate90^k`,k ∈ {0..3} 由整数枚举 `orientation` 索引 —— 单基元复合,竖分量恒等透传。
2. `OccupiedCells(m, anchor, orientation)` 替换 Story 002 的桩:判定②(占用全空)/③(region 包容)/Story 004 fold 的置位格集统一改走本件函数(单点,禁副本)。
3. `Modifiable` 谓词:新格集 = `OccupiedCells(m, anchor, new_orientation)`;差集 `新 ∖ 旧` 逐格查 `slot_occupied`(Story 004 同一张表)为空 ∧ `TypeOK(new_orientation)`(目录 `orientation_set` 含之)∧ region 包容(旋转后出 region ⇒ 拒,承 AC-23-01③ 同源判据);拒绝 ⇒ 零 Append(承 Story 002 结构纪律)。
4. 编排(following Story 003 写权):通过 ⇒ `Append(StructureModified{structure_id, cell, module_id, new_orientation, new_variant, modified_fields})` → 实例表更新朝向/变体 → Overlay 增量(差集格清位/置位)。**实体占用不重查**(评审:改朝向的实体条件归 P0 目录无旋转 ⇒ 恒不触发;留注释指向 P1a 扩展点,禁顺手加 Physics)。
5. `modified_fields` 位枚举 = {orientation, variant} 闭集;数值结果(如 24 的邻接修正)走 24 自家派生,零入流(Story 003 的 Forbidden 在此有测试对偶:注入带数值的载荷构造期拒)。
6. `rotation_cases.json` 载体:非对称多格 L 形模块 × 四向 × 锚点含负偏移 → 期望格集独立钉死(手工可算);P1a 夹具 = 注入版更大骨架 + 更宽目录(含 90° 条目),放置未见格成功 + 代码 diff 为零(测试只换数据不换码,即 AC-23-15 本体)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:五条件合取与失败原因枚举(本件替换其 orientation 桩)
- Story 003:`StructureModified` 载荷形状与写权(本件交付其判定与语义)
- Story 004:Overlay 增量(本件产出差集格清单)
- Story 006:拆除(与旋转无交集;Removed 载荷本就无 orientation)
- 24:布局邻接数值派生(明确不入流)
- P1a:任意角度 / 换模块位 / 楼层(y 参与)—— 均裁死在本 GDD 之外,本件只留注释位
- TR-building-010 registry 改 covered:回写轮义务(夹具绿 ≠ 自动改账)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-23-05**: 旋转正确 + 类型门。
  - Given: L 形三格夹具四向;反射扫旋转模块。
  - When: 求占用格集;类型断言。
  - Then: 每向格集 = 手工期望表(独立钉死);180° = 90° 复合自检;字段/参数类型 ∈ 整数集零 float。
  - Edge cases: 负偏移(u=-2,v=3)旋转;单格模块四向恒等;竖分量透传不随转。
- **AC-23-06**: 四向可执行用例(目录 ⊆ {0°} 下仍跑)。
  - Given: 合成四向目录 + `rotation_cases.json`。
  - When: Placeable(orientation=90/180/270)/ 重放走全向。
  - Then: 放置判定与重放逐格正确;P0 真目录路径行为不变(0° 用例全绿为回归基线)。
  - Edge cases: 旋转后出 region ⇒ 拒(③);旋转后撞邻模块 ⇒ 拒(Modifiable 差集非空)。
- **AC-23-11**: 载荷位语义。
  - Given: 改朝向 + 改变体同条 / 各单条夹具。
  - When: 断流。
  - Then: `modified_fields` 恰标记所改位;无任何数值字段;24 的派生量不出现在任何载荷(反例注入必拒)。
  - Edge cases: 空 `modified_fields`(非法);朝向未变仅变体(Modifiable 判定的 ∖ 自身旧格 = 全空 ⇒ 过,不假拒)。
- **AC-23-15**: P1a 注入夹具(零代码首验)。
  - Given: 注入「更大骨架 + 含旋转目录」数据版本,代码不动。
  - When: 放置此前未见过的新格/新模块。
  - Then: 成功且走完整五条件;git diff sim 代码 = 空(测试断言只增数据)。
  - Edge cases: P1a 夹具仍不改载荷形状(Structure* 三支字段集恒定);夹具骨架若缺 SlotType 标记 ⇒ 构建期校验拒(承 Story 001)。
- **EC-23-10**: 连续身份。
  - Given: 已放置多格模块;执行改朝向。
  - When: spy-sink。
  - Then: 恰一条 `StructureModified`;流中无该 id 的 Removed/Placed;实例表同 id 更新(身份连续)。
  - Edge cases: 改向被拒 ⇒ 零事件;改向后拆除 ⇒ Removed 正常、释放格集按**新朝向**回查(交叉 Story 004 的实例表回查纪律)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: 真身 `unity/Assets/Tests/EditMode/ModularBuilding/modifiable_checker_test.cs`(5/5 全绿)· 账本路径 `EditMode/Building/` 未建(占位),实际落 `EditMode/ModularBuilding/`

**Status**: ✅ 2026-10-01 实现落盘 + EditMode 验证(【超算】batchmode)—— 朝向旋转 + Modified(`ModifiableCheckerTest` 5/5)

---

## Dependencies

- Depends on: Story 002(谓词框架),Story 003(Modified 载荷与写权),Story 004(同一张占用表供 `Modifiable` 判空)
- Unlocks: Story 006(拆除释放格集按当前朝向),TR-building-010 回写轮(交夹具转绿证据),P1a 旋转内容(零代码判据的本证)

---

## Completion Notes

*(placeholder — to be filled at story completion)*

**Code Review**: 双代理评审 2026-10-01 裁 REQUEST_CHANGES(5 BLOCKING:entity check 缺失/错误 · payload bit-packing · 无 `OccupancyOverlay` 独立类型),修复落 `da04f41`(2026-10-01);复跑 `ModifiableCheckerTest` 5/5 全绿。逐 BLOCKING 对账件未落 `production/qa/evidence/`(桌面批遗留缺口 —— `da04f41` 只给 commit message 自述,依「不得借绿」EPIC 级不转 Complete);全量复跑逐例证据见 `production/qa/evidence/editmode-full-rerun-2026-10-02.md`(实跑 5 例全绿)。
