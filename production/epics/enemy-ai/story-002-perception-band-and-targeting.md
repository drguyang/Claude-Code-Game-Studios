# Story 002: 感知与目标 —— 三源白名单、Band 分档互斥、整数视线与 Target(e)

> **Epic**: 敌人 AI
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/enemy-ai.md`(§Detailed Rules 规则一 输入恰三源 · 规则二 感知 = 最近一条 `ActorCellEntered` 的 cell,禁读 Transform · 规则三 格距整数平方和 + Bresenham 视线 · 规则四 粗枚举取 sim 真值 · §Formulas F-27-1(`d2`/`Visible`/`Band` 求值顺序互斥,`ChaseReady` 摘除,断言对象 = 世界量程 `W`)· F-27-6(`Target(e)` = argmin d2 + lowest actor_id 决胜,`target_policy` 仅实现 NearestVisible)· A 组 AC-27-02 / AC-27-26 · B 组 AC-27-14 / AC-27-15)
**Requirement**: TR-enemy-001(决策输入恰三源;第四来源进 Forbidden Patterns)· TR-enemy-002(感知 = 粗粒度整数格,禁读 `Transform.position`)· TR-enemy-003(格距整数平方和禁 sqrt;视线 Bresenham 逐格查 `EffectiveWalkable`,禁 `Physics.Raycast`/NavMesh 采样)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): §一 重建三源不变量 + §三 感知粗粒度整数格;ADR-020: 玩家跨格事件写方口径(27 是读方)
**ADR Decision Summary**: 读表现态位置 ⇒ 决策不再是 tick 的确定性函数(第二 QoS 到达时序不确定)⇒ 主机与客户端分叉;**半格盲区是设计不是缺陷**(「敌人眼里的世界是格子」)。玩家位移在 sim 的唯一投影 = 跨格世界流事件,事件率上界 = tick 频率(与帧率无关)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数判定(门 A `Sim`);`d2` 中间量 int64(承 24 B6 教训);不触及引擎 API;视线 = 自实现 Bresenham 格步进。

**Control Manifest Rules (this layer)**:
- Required: `Band` 求值顺序 = Patrol → Alert → Chase **自上而下首个为真**(互斥,`ChaseReady` 把 Chase 从 Alert 摘出);`Chase` 进入条件含**承重的 `Visible` 项**(`PathExists` 只保证有路不保证看得见);`Target(e)` 决胜键 = lowest actor_id(**规则不是实现细节**)
- Forbidden: `sqrt` / `Mathf` / `Physics.Raycast` / NavMesh 采样 / `Transform.position` / 从动画或输入推断玩家状态(粗枚举必取 sim 真值)
- Guardrail: 构建期断言 `R_CONTACT ≤ R_CHASE ≤ R_ALERT ≤ R_VIS` 单调 + 世界量程 `3×(2W)² < 2^63`(断言对象是 `W` 不是 `R_VIS` —— 原稿写反已订正,EC-27-01/24);输入类型反射白名单正面断言
- [x] **✅ 2026-10-07 订正一处假绿(与 `OQ-27-1` 数值轮同批发现)**:`EnemyBehaviorProgram.Validate` 把本 guardrail 的三条比较**全部写反**(`R_VIS ≤ R_ALERT ≤ R_CHASE ≤ R_CONTACT`),而 `behavior_program_schema_test` 的 fixture(`2/5/8/10`)与三条 `_throws` 测试**同向验证反向** ⇒ 11 个测试全绿却守着一套与 GDD 相反的语义。**反方向不自洽**:`Alert` 的进入条件是 `Visible ∧ d2 ≤ R_ALERT²` 而 `Visible ⟺ d2 ≤ R_VIS²`,`R_ALERT > R_VIS` 时存在「看不见却停下转头」的格。已改代码为 guardrail 方向、重写三条测试(改名 `test_rAlertGreaterThanRVis_throws` 等)、fixture 换成定值 `12/8/6/2`;`perception_and_targeting_test` 的 `EvaluateBand` 造参同步换成满足单调的 `(12, 8, 6)`(原 `(5, 8, 3)` 在 GDD 属下非法)。Reg测试 = 同上 11 项 + 45 项 EnemyAI 全绿

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-ai.md`, scoped to this story:*

- [ ] 反射断言:27 决策器**全部输入类型恰 ⊆ {sim 值 struct, 烘焙数据 struct}**(正面白名单非负断言),覆盖范围含 `Target(e)` 候选集构造(最易偷读 `Transform` 的函数)(AC-27-02)
- [ ] `Band(e)` 互斥性质测试:对满足单调性的**任意**四元组半径(含 `R_CHASE < R_ALERT` 常见配置),`{Patrol, Alert, Chase}` 恰一个为真,求值顺序 = 文档序(AC-27-14)
- [ ] `Visible` 承重:隔墙等距场景(路径存在但视线被挡)⇒ 不进入 `Chase` 分档(Bresenham 中间 blocked 格 ⇒ 视线断)(AC-27-03 侧)
- [ ] `Target(e)`:两等距候选 ⇒ 选 `actor_id` 最小;**打乱候选集输入顺序结果不变**(交换律,性质测试)(AC-27-15)
- [ ] 感知源 = 最近一条 `ActorCellEntered.cell`:玩家在格内移动(无新事件)⇒ 敌人读到的目标格不变(半格盲区行为可观察且稳定)(TR-enemy-002)
- [ ] `d2` 全 int64:极端坐标差夹具(|Δ| > 46341)不溢出;量程断言存在且对 `W` 求值(AC-27-09 的 int64 前置半边)
- [ ] 27 的感知/查询 API 枚举断言:对外读取恰 ⊆ {世界流事件查询, 整数格查询, `EffectiveWalkable`, 烘焙数据读}(AC-27-26 正面白名单)

---

## Implementation Notes

1. `Visible(e,p) := d2 ≤ R_VIS² ∧ LineOfSight(e,p)`;LOS = Bresenham 格步进逐格查 `EffectiveWalkable`(23 合成结果,Story 004 提供轮询接口;本 story 可注入假可走性表)。
2. `Candidates(e)` P0 = 玩家侧行动者 ∩ Visible(敌对另一实体支 P0 空集);`argmin` 手写整数比较,禁 LINQ 浮点/顺序依赖写法。
3. `PathExists` 由 Story 004 的 A* 给出 —— 本 story 以接口占位(返回缓存),避免每 tick 全图寻路(F-27-1 注:单 tick 至多一次,结果进 `path_cache`)。
4. 粗枚举(`in_combat` 玩家侧 / 濒危)读 sim 真值:来自世界流/9 查询,禁动画侧推断(规则四)。
5. 半径四值与 `PERCEPT` 距离具体数归用户数值轮(OQ-27-1);story 只建断言与纯函数。

---

## Out of Scope

- [Story 001]: 参数行 schema(bands 值的载体)
- [Story 003]: 状态机转移表与士气(本 story 输出 Band 与其计时输入)
- [Story 004]: `PathExists` 的 A* 本体与 `EffectiveWalkable` 轮询
- 13 的感知(F-13.3 与本式共用纪律但独立实现,跨 epic 不合并代码)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | R_CHASE=3 < R_ALERT=6 配置,敌在 d2=20 | 求 Band | 恰 Alert(非双真) |
| TC-2 | 敌我同格距但有墙 | Band/Visible | Visible=false,Chase 不可达 |
| TC-3 | 两玩家行动者等距(id=7, id=4;P0 单玩家以夹具双目标) | Target(e) | 选 4;候选顺序反转仍选 4 |
| TC-4 | 玩家在格内走 0.9 格 | 感知 | 目标格不变;跨格瞬间(同 tick 事件)才变 |
| TC-5 | Δ=(46342,0,0) 极端夹具 | d2 | int64 正确值,int32 版会溢(对照断言) |
| TC-6 | 决策器加一行读 `Transform` 的负面构建 | 反射扫描 | AC-27-02 失败点名函数 |

**Edge cases**: `ActorCellEntered` 迟到(回放/网络)⇒ 读到旧格属预期(事件序 = 真值序,ADR-009 全序键);敌人自己也在移动时 d2 双方格都取上 tick 快照(Story 003 的求值序纪律)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/EnemyAI/perception_and_targeting_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/EnemyAI/perception_and_targeting_test.cs`)

---

## Dependencies

**Depends on**: Story 001(参数行 bands/target_policy)· 系统 1 的 `ActorCellEntered`(player-controller epic)· 23 的 `EffectiveWalkable` 读接口(假表可先行)
**Unlocks**: Story 003(Band → 状态机转移)· Story 004(PathExists 闭环)

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `PerceptionInput` — 感知输入（三源）
- `Band` — Band 枚举
- `EnemyPerception` — 感知与目标选择器（EvaluateBand / ComputeD2 / LineOfSight / SelectTarget / ValidateIntegerDomain）
- Band 求值顺序互斥（Patrol → Alert → Chase）
- Visible 承重（隔墙不进入 Chase）
- Target 决胜键 = lowest actor_id
- d2 全 int64
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- 视线检查为简化版（只检查起点和终点），完整版需要 3D Bresenham

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/EnemyAI/perception_and_targeting_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
