# Story 001: 炮制配方子集筛选与准入谓词 Admissible

> **Epic**: 炮制
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/processing.md`(规则二 · 规则三 · F-18.2 · 规则十 配方可知性 · AC-18-05/10/18/20)
**Requirement**: TR-processing-004(炮制子集判据 = Recipe.owner 显式字段,21a D-21-30)· TR-processing-005(准入两道门:skill_gate 门槛 + min_quality 准入闸,⚠️ partial —— 无 ADR 承接)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主,Amendment B): 判定归主机终裁,客户端预答降级为呈现门 · ADR-014(次): `Recipe.owner` 字段走 21a schema/烘焙管线 · ADR-005(次): 谓词输入全可重建
**ADR Decision Summary**: 准入谓词 `Admissible(recipe, actor, consumedInstances) ⟺ QueryLevel(actor, CRAFT_SKILL) ≥ skill_gate ∧ min(quality of consumedInstances) ≥ min_quality`,是 `(recipe, actor, consumedInstances)` 的**纯函数**;客户端预答与主机终裁走**同一函数零第二实现**(AC-18-20,ADR-011 Amendment B 同构:判定输入进流、终裁归主机);`skill_gate` 是门槛**非系数**(否则与 SkillMod 双重计数),`min_quality` 是准入闸**不改幅值**;子集筛选读 `owner == process`(「processing_state 是否变化」降为单向校验)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 谓词与筛选为门 A 程序集纯 C# 整数比较(等级 int × 品级 int),零引擎 API、零 post-cutoff 面。

**Control Manifest Rules (this layer)**:
- Required: `Admissible` 全仓唯一定义点(客户端预答与主机终裁两处调用同一函数);`min_quality` 使用点仅限该谓词;谓词输入 ∈ {事件流/烘焙数据/二者的纯函数}(三源不变量,ADR-016 §一)
- Forbidden: 在 18 侧把 `skill_gate` 当修正系数参与乘加;`min_quality` 进入幅值调制路径;自建「state 变化」判据当主筛(已降级为单向校验);第二份准入实现(哪怕为性能缓存,缓存键不纯即违三源)
- Guardrail: `Recipe.owner` 字段实现归 21a(O-18-R1 回写义务,AC-18-18 在此之前 EXTERNAL 不得记绿);配方可知性接缝 O-18-R6 未立 ⇒ 本故事只出只读谓词,不代 42 裁面板形态

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [ ] **AC-18-05**: `GIVEN` `min_quality` 的全部使用点,`WHEN` 符号引用扫描,`THEN` 仅出现在 `Admissible` 谓词(F-18.2),零幅值调制路径(规则三 · 21a `:167`)
- [ ] **AC-18-10**: `GIVEN` 任意配方与玩家状态,`WHEN` 求值谓词,`THEN` 配方进入候选集 ⟺ `Admissible ∧ owner=process ∧ 单炉空闲`(iff 双向,18 侧半边;呈现半边见 AC-18-10b)
- [ ] **AC-18-20**: `GIVEN` 客户端预答与主机终裁两条路径,`WHEN` 符号引用扫描 `Admissible` 实现,`THEN` 同一函数、零第二实现;谓词为纯函数,`actor_id` 由 20 `InventoryOf(player)` 归因
- [ ] **AC-18-18**: `GIVEN` `Recipe.owner` 字段落地(21a),`WHEN` 对 `{process, craft, build}` 三子集求并/交,`THEN` 并 = 全表、两两交 = ∅(R-18-B)。⚠️ **EXTERNAL · BLOCKED-BY-21a(O-18-R1)** —— 字段有 schema、无实现 ⇒ 判据不可执行前**不得记绿**(OQ-18-1 已裁[甲]=字段方案,裁决≠验收)

---

## Implementation Notes

*Derived from ADR-011 Amendment B(主)/ ADR-014:*

1. 谓词签名 `bool Admissible(RecipeId, ActorId, IReadOnlyList<ItemInstanceRef> consumedInstances)`,住 18 的 sim 程序集(门 A);两调用点(预答/终裁)经由同一静态具名函数,禁委托复制。
2. 输入取数:等级 = 30 `QueryLevel`(int,承 AC-18-03 传等级纪律的镜像侧);`min(quality)` = 对被点名实例集求 min(整数比较;实例集来自 20 `InventoryOf(player)` 投影,18 不持库存副本);`skill_gate` / `min_quality` = 烘焙配方字段(int)。
3. 子集筛选 = 遍历烘焙配方表取 `owner == process`(21a 字段);同时把「processing_state 变化」做**单向校验断言**(若出现 owner=process 而 state 不变的行 ⇒ 装载告警级不一致检查,构建期归 21a 侧 AC-21a-66,本故事只做运行期防御断言半边)。
4. 候选集 iff 双向(AC-18-10):「单炉空闲」半边在 Story 004 落地,本故事先立谓词形状与桩接口(空闲位 = Story 004 的炉状态机,此处以 `ISingleFurnaceQuery` 接缝引用)。
5. 配方可知性(规则十):面板禁灰置 = 42 呈现半边(AC-18-10b EXTERNAL);18 只供只读谓词,供 42 判「候选集成员资格」;O-18-R6 接缝未立 ⇒ 回写义务继续挂账,不在本故事擅裁。
6. `min_quality` 不改药效:扫描器断其使用点 ∈ {Admissible 体内},出现于 F1 参数构造路径即红(与 Story 002 的 AC-18-02 扫描件共用基建,零第二扫描器)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:F1 调用契约、四修正项供料、AC-18-02 结算路径扫描(本故事只保证 min_quality 不进幅值)
- Story 003:准入通过后 `Craft` 落流(本故事只做判定)
- Story 004:单炉空闲位与并发拒绝(本故事以接缝引用)
- item-database epic:`Recipe.owner` schema 落地与烘焙主执行(AC-18-18 的 BLOCKED-BY 对方)
- 42:炮制面板/候选呈现(AC-18-10b、O-18-R6 的呈现半边)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-18-05**: min_quality 使用点扫描。
  - Given: 18 程序集源码树;合成注入一个「用 min_quality 调 output」的负样例文件(测试专用,不进出货)。
  - When: Roslyn 符号引用扫描。
  - Then: 真实代码路径使用点 ⊆ {Admissible};负样例触发即红(扫描器自证有效)。
  - Edge cases: 经别名/属性包装的读取同计(承 processing 扫描标识符口径白名单)。
- **AC-18-10**: 候选集 iff。
  - Given: 配方矩阵夹具(gate 上/下 × quality 上/下 × 空闲/占用 8 组合)。
  - When: 求候选集。
  - Then: 成员资格与三条件合取逐位等价(双向蕴含都测)。
  - Edge cases: gate 恰等(≥ 过)、min_quality 恰等(≥ 过)、空实例集(min over ∅ ⇒ 谓词短路 false 并具名理由)。
- **AC-18-20**: 同一函数零第二实现。
  - Given: 预答路径与终裁路径各一反射句柄。
  - When: 比对 MethodInfo 同一性 + spy 计数(两路径共享)。
  - Then: 同一 `MethodInfo`(非「行为相同的两份」);纯函数性:同输入两次求值逐位同、零副作用(spy-sink 零事件)。
  - Edge cases: 客户端副本流滞后(预答 yes → 终裁 no)是合法时序,谓词自身不感知网络(不在此断)。
- **AC-18-18**(EXTERNAL 登记,不记绿): 三子集并/交。
  - Given: 21a owner 字段实现落地后(前置未满足 ⇒ 测试 `Ignore("EXTERNAL BLOCKED-BY O-18-R1")`)。
  - When: 全表分组。
  - Then: 并=全表、两两交=∅。
  - Edge cases: 落地前以合成 owner 夹具跑**同一测试体**(形状义务不豁免,镜像 AC-18-25 纪律),真实表判据保持外抛。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs` — must exist and pass
- AC-18-18:同文件内 `Ignore` 挂起项(字段落地后摘除)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: item-database story-002/003(schema 类型 + F1 求解器,Complete —— 本故事消费其配方表形状);skill-system story-001(Complete,QueryLevel 件);inventory-items Story 001(`InventoryOf` 投影,取实例集)
- Unlocks: Story 002(准入通过后的供料与调用),Story 003(点火前置校验),Story 004(候选集第三条件接线)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
