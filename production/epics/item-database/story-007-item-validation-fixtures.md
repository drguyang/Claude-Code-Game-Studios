# Story 007: 物品表写入期校验套件

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-008(药材 schema)◆ · TR-itemdb-009(炮制方法 schema)◆ · TR-itemdb-016(品质分级与品质轴定义)◆
◆ = `status: no-adr-by-design`,归属件 = GDD 自身;永不因补 ADR 转 ✅。
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014: 数据管线与 JSON 解析器
**ADR Decision Summary**: 全量校验失败 = 构建失败,不是警告;逐 schema 白名单;禁 `JsonConvert.DeserializeObject<T>`;ordinal ↔ 名称映射表须进 `ConfigVersion` 覆盖集,既有条目的号永不重用、永不改义(改义/改号/复用 = 构建期硬失败)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-014 Engine Knowledge Risk MEDIUM;本故事校验逻辑本身不依赖 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 全量校验失败 = 构建失败;负向夹具 + 合法对照;显式 `throw`
- Forbidden: 校验失败降级为警告;只验合法值不算通过;`weight` 走 FixParse
- Guardrail: `weight` / `stack_max` 是 `int` 计数(D-21-17)—— 夹具 `invalid_stack_weight` 须覆盖 Fix 形式("1/2")拒

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-13**: MAX_QUALITY < 2 或非整数 ⇒ 拒绝(品级维度必须存在)
- [ ] **AC-21a-14**: quality_distribution 支撑 ⊄ [1, MAX_QUALITY] ⇒ 拒绝(D-21-7 双向耦合)
- [ ] **AC-21a-15**: stack_max < 1 或 weight ≤ 0 ⇒ 拒绝(规则六)
- [ ] **AC-21a-23**: P0 期出现 P1a 值(honey_fried / dry_fried / 非空 tcm_profile)⇒ 拒绝(对齐 8 的 AC-8-33 同型纪律)
- [ ] **AC-21a-24**: 配方 state 对不在物品声明的 legal_transitions 内(如 raw → extracted 当该条目未声明)⇒ 拒绝(「枚举只给词汇不给通路」)
- [ ] **AC-21a-25**: category=weapon 无 inflicts_injury,或非 weapon 带 inflicts_injury,均拒;⚠️ 只校验存在性与类别门,不校验语义(「哪次命中造成哪个伤」归 25 的 maps_to_injury);集合漂移门:21a 的 inflicts_injury 集合 ⊇ 该线全部动作的 maps_to_injury,构建期断言,违例=硬失败(承 25 的 A20;外键悬空 injury_id 亦拒)

**共同断言口径**: 同 Story 006 —— 负向夹具 → 构建期硬失败,合法对照通过。

---

## Implementation Notes

*Derived from ADR-014 §Decision:*

- **全量校验失败 = 构建失败,不是警告** —— ADR-014 §三:192
- ordinal ↔ 名称映射表须进 `ConfigVersion` 覆盖集;**既有条目的号永不重用、永不改义** ⇒ 改义 / 改号 / 复用 = **构建期硬失败** —— ADR-014 §五:238,258(同 ADR-006 A-B / D-21-13:156-164)—— AC-13 的 MAX_QUALITY 变更、AC-14 的 D-21-7 双向耦合均走此口径:常量变了,校验必须重跑、旧数据静默失效 = 红
- `weight` / `stack_max` 是 `int` 计数,不是 `Fix`(D-21-17)—— ADR-006 §Decision 一(AC-15 夹具须覆盖 weight 写 Fix 语法的拒收)
- 执法体统一形态 = **构建期断言** —— manifest 元规则
- AC-25 语义侧(逐次命中造成哪个伤)明确**不在**本断言 —— 归 25 的 maps_to_injury;21a 只断存在性 + 类别门 + 集合 ⊇(防测试过度断言,QA spec 已注明)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: schema 类型与枚举闭合(AC-21/22)—— 本故事 AC-23 的 P0 收窄是校验层,枚举本身归 002
- Story 004: axis 数组长度与地板(AC-50/50b/61/62)
- Story 006: 配方表侧夹具(AC-7~20/66)
- Story 008: 管线执行本套校验的落点

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-13**: MAX_QUALITY < 2 或非整数 ⇒ 拒绝(品级维度必须存在)。
  - Given: MAX_QUALITY = 1 / 0 / "3.5" 三种配置。
  - When: 常量校验。
  - Then: 三者均硬失败;=2 及以上整数(过,具体档数 = 数值待用户定)。
  - Edge cases: 字符串 "4"(须按整数解析成功,非拒);浮点串拒;MAX_QUALITY 变更触发 D-21-7 全分布重验(交叉 AC-14)。
  - Negative fixture: `invalid_max_quality.json`

- **AC-21a-14**: quality_distribution 支撑 ⊄ [1, MAX_QUALITY] ⇒ 拒绝(D-21-7 双向耦合)。
  - Given: 分布含支撑点 0 或 MAX_QUALITY+1。
  - When: 构建期支撑校验。
  - Then: 硬失败;支撑恰在 [1, MAX] 边界内(过)。
  - Edge cases: 仅一档越界;支撑全在界内但 MAX_QUALITY 后下调(重跑校验拒 —— 静默失效回归点);权重为 0 的档(仍计支撑,拒越界档)。
  - Negative fixture: `invalid_quality_dist.json`

- **AC-21a-15**: stack_max < 1 或 weight ≤ 0 ⇒ 拒绝(规则六)。
  - Given: stack_max = 0 / weight = 0 / weight = −n 轮换。
  - When: 构建期校验。
  - Then: 硬失败;stack_max=1(过,不可堆叠合法)、weight=1(过,最小正单位)。
  - Edge cases: stack_max=1 + 堆叠逻辑(AC-32/33 全不合并);weight 为 Fix 形式("1/2")拒 —— weight 是 int 非 Fix(D-21-17,交叉 AC-41);weight 浮点拒。
  - Negative fixture: `invalid_stack_weight.json`

- **AC-21a-23**: P0 期出现 P1a 值(honey_fried / dry_fried / 非空 tcm_profile)⇒ 拒绝(对齐 8 的 AC-8-33 同型纪律)。
  - Given: processing_state=honey_fried 或 dry_fried;或任一记录 tcm_profile 非空(P0 恒空)。
  - When: 构建期 P0 收窄校验。
  - Then: 三形态各硬失败;P0 枚举内 state + tcm_profile 空/字段在(过,D-21-6)。
  - Edge cases: tcm_profile 占位块全字段 null(过)vs 含任一非空值(拒);honey_fried 经枚举外字面量路径(先被 AC-22 拒,两条门不互相替代);P1a 解锁须改本校验。
  - Negative fixture: `invalid_p1a_leak.json`

- **AC-21a-24**: 配方 state 对不在物品声明的 legal_transitions 内(如 raw → extracted 当该条目未声明)⇒ 拒绝(「枚举只给词汇不给通路」)。
  - Given: 源条目 legal_transitions 不含某转换,而配方 boundary_state 声明了它。
  - When: 构建期通路校验。
  - Then: 硬失败;声明内的对(过);legal_transitions 空数组 + 任何转换(拒)。
  - Edge cases: 声明了但配方未用(过);跨 base 配方 —— 通路查源条目(输入侧 state)的声明,输出条目另按其自身规则;图上非相邻对(raw→pill 未经中间态)须显式声明才过。
  - Negative fixture: `invalid_transition.json`

- **AC-21a-25**: category=weapon 无 inflicts_injury,或非 weapon 带 inflicts_injury,均拒;⚠️ 只校验存在性与类别门,不校验语义(「哪次命中造成哪个伤」归 25 的 maps_to_injury);集合漂移门:21a 的 inflicts_injury 集合 ⊇ 该线全部动作的 maps_to_injury,构建期断言,违例=硬失败(承 25 的 A20;外键悬空 injury_id 亦拒 —— Edge Cases 同族)。
  - Given: weapon 记录缺字段;material 记录带字段;weapon 记录 injury_id 在 9 枚举不存在;25 某动作 maps_to_injury 值不在 21a 集合内(漂移)。
  - When: 构建期类别门 + 外键门 + 集合漂移断言。
  - Then: 四形态各硬失败;合法 weapon 带有效集(单值或列表,P0 语义按集合 ⊇)且非 weapon 无字段(过)。
  - Edge cases: inflicts_injury 单值 vs 数组(P0 形态归数值/数据轮 OQ,两种承载都过存在性门);集合恰相等(⊇ 取等,过);25 侧删动作后集合悬空(反向漂移不在本 AC,注记归 25);语义(逐次命中)明确不在本断言 —— 防测试过度断言。
  - Negative fixture: `invalid_injury_fk.json`(外键+类别门)与 `injury_set_drift.json`(集合漂移)—— 两件,GDD 同行指名。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/item_validation_fixtures_test.cs` — must exist and pass;负向夹具落 `tests/unit/item_database/fixtures/`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(FixParse),Story 002(schema 类型)
- Unlocks: Story 008(管线执行本套校验)
