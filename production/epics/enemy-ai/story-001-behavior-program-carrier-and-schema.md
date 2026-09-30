# Story 001: 行为程序载体 —— ai_enemy.json 烘焙 schema 与整数决策器

> **Epic**: 敌人 AI
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/enemy-ai.md`(§Detailed Rules 规则七 行为程序 = 版本化烘焙数据(手写 `ai_enemy.json`,零行为树工具)· 规则六 单套程序 × 两类参数行(`entity_kind`/`down_class` 是身份不是分支)· 规则八 禁 float · §Formulas 变量表 · E 组 AC-27-10 / AC-27-30 / AC-27-31 / AC-27-32)
**Requirement**: TR-enemy-005(P0 行为面 = 单套行为程序 × 两类参数行,差异全落烘焙数据,零 `isBeast` 代码分支 · 现 `partial`)· TR-enemy-006(载体 = `ai_enemy.json → ai_enemy.cooked` 两阶段烘焙;运行期零第三方行为树/寻路库;源改未重烘 ⇒ 构建失败)· TR-enemy-007(禁 float:空间量 WorldPos 整数格/朝向枚举,非空间量 Fix 或 int)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): AI 架构 §四(行为载体)+ 2026-09-17 修订(P0 不引入编辑器期可视化行为树工具,直接走自研数据表载体);ADR-014: 数据管线
**ADR Decision Summary**: 作者态 = 手写 `assets/data/ai_enemy.json`(PR-27-6 ④ 砍工具 ⇒ ADR-016 唯一 post-cutoff 悬置消解);构建期 ADR-014 两阶段烘焙(Newtonsoft 仅词法 + 自研 per-schema 绑定 + FixParse);运行期自研整数决策器解释 `.cooked`,零第三方;陈旧门 = 构建失败非运行期幻觉。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# schema 绑定 + 烘焙校验(编辑期工具链);不触及 post-cutoff API;IL2CPP 侧只有读表的整数求值。

**Control Manifest Rules (this layer)**:
- Required: 每参数行**恰有** `entity_kind` / `down_class` / `morale_enabled` / `flank_enabled` / `target_policy` / `default_attack` 各一(缺项 `throw`);Fix 字段 JSON 写字符串 → FixParse;决策器读表为纯整数/定点求值
- Forbidden: `isBeast` / `if (entity_kind == …)` 出现在任何**转移或公式**判定(`entity_kind` 仅输出侧,AC-27-32 谓词白名单);JSON 数字字面量直接进 Fix 字段(浮点泄漏);第三方行为树/寻路/效用 AI 库进引用集(AC-27-30 正面白名单)
- Guardrail: `target_policy` 非 `NearestVisible` 值 = 构建期 `throw`(数据声明了本构建不支持的能力即硬失败,禁静默退回默认);陈旧门(源改未重烘)构建失败

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-ai.md`, scoped to this story:*

- [ ] `ai_enemy.json` schema 校验:字段类型**恰 ⊆ 整数域白名单**(无 float 字段);每行六个必备字段齐全,否则 **`throw`**(AC-27-10)
- [ ] 单套程序验证:决策器代码分支谓词白名单恰 = {`Visible`, `PathExists`, `d2` 比较, `morale_enabled`, `flank_enabled`, 超时, `entity_kind`(仅输出侧)};`entity_kind` 不出现在转移/公式判定(AC-27-32 正面白名单,非「零 isBeast」负断言)
- [ ] 玩家构建引用集恰 ⊆ {BCL, Unity 引擎核心, 本工程 sim/边界/表现程序集} —— 零第三方 AI 库(AC-27-30,承 ADR-017 §二 同法)
- [ ] 陈旧门:改 `ai_enemy.json` 不重烘直接构建 ⇒ **构建失败**(`throw` 级,非告警)(AC-27-31)
- [ ] `target_policy ∈ {LowestVitality, LastAttacker}` 的夹具行 ⇒ 构建期 `throw`;`NearestVisible` 唯一合法实现值(F-27-6 扩展点纪律)
- [ ] Fix 字段以字符串写(`"3/4"`)经 FixParse 解析;浮点字面量夹具被拒(承 ADR-006/014 词法门)
- [ ] 兵痞/野兽两行数据在**同一决策器**下跑出不同参数语义(士气/包抄门生效,`entity_kind` 只改输出)—— 集成冒烟用夹具表(值归数值轮,机制先验)

---

## Implementation Notes

1. schema 落地 = ADR-014 阶段 2 的 per-schema 绑定器新增 `ai_enemy` 分支;校验组含必填集、值域枚举、单调性(与 Story 002/003 的构建断言同批)。
2. 参数行结构:`{ id, entity_kind, down_class, morale_enabled, flank_enabled, target_policy, default_attack, bands:{R_VIS,R_ALERT,R_CHASE,R_CONTACT}, speed(Fix), morale:{...}, decide:{...} }` —— 分组名按 GDD 变量表,**值全部待裁**(OQ-27-1…7),表先给合法占位与断言。
3. 决策器 = 自研整数解释器(住门 A `Sim` 程序集,零引擎引用);六态机/公式的实现归 Story 003/004,本 story 交付「读得到、校验得住」。
4. 与 25 的 A24 联动(`RANGE(default_attack) ≤ R_CONTACT` 烘焙期两表同批可见,`O-27-10`)在烘焙器实现,测试用两表夹具。
5. `ai_enemy.json` 归 52 与 27 共读的遭遇原型域 —— 52 的池引用原型 id,本 story 不与 52 的抽池互相阻塞。

---

## Out of Scope

- [Story 002]: 感知输入与 F-27-1 分档判定(消费参数行的 bands)
- [Story 003]: 六态状态机 + 士气/脱离公式
- [Story 004]: 步进与确定性 A*
- [Story 005]: 遭遇生命周期与呈现 DTO
- 52 的遭遇池抽池本体(归 randomevents epic)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 合法兵痞/野兽两行 | 烘焙 | 通过;`.cooked` 中 Fix = raw long |
| TC-2 | 缺 `down_class` 的行 | 烘焙 | `throw` 点名行与字段 |
| TC-3 | `"R_VIS": 12.5`(JSON 数字) | 烘焙 | FixParse 拒收(浮点字面量) |
| TC-4 | `entity_kind=Beast` 行 | 决策器转移检查 | 转移序列与 Human 行同参数时**全同**(身份不改行为) |
| TC-5 | 改 json 不重烘 | 构建 | 失败(陈旧门) |
| TC-6 | 引用第三方 BT 库的负面构建 | 引用扫描 | AC-27-30 断言失败 |

**Edge cases**: 重复原型 id;`ThreatType` 项数越上界(构建断言,F-27-2 溢出纪律);空数据集 ⇒ 装载硬失败。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/EnemyAI/behavior_program_schema_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: ADR-014 两阶段烘焙管线(audio/itemdb 等已有先例)· ADR-006 FixParse · ADR-025 `Sim` 程序集清单
**Unlocks**: Story 002/003/004 的数据面 · 52 的遭遇原型引用 · `O-27-10` 的 A24 烘焙期校验

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `EnemyBehaviorRow` — 行为程序参数行
- `EnemyBehaviorSchema` — schema 校验器（Validate / ValidateAll / ParseFixField）
- `EnemyDecisionEvaluator` — 整数决策器（EvaluateTargetSelection / IsInRange / IsInChaseRange / EvaluateMorale）
- 每参数行恰有 entity_kind / down_class / morale_enabled / flank_enabled / target_policy / default_attack
- Fix 字段 JSON 写字符串 → FixParse
- 测试: 11 条单元测试（全部通过）

**Deviations**: 
- 决策器为简化版（无完整状态机），完整版归 Story 003

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/EnemyAI/behavior_program_schema_test.cs` — 11 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
