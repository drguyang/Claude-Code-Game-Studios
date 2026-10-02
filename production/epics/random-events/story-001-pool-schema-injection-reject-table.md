# Story 001: 池条目 schema、注入接口与构建期拒收表

> **Epic**: 随机事件导演
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/random-events.md`(规则二 池条目 schema · 规则十一 注入接口与脚本条目 · DC-2 定点纪律 · 构建期拒收表 18 谓词 · AC-52-01/02/03/09/41/42/43)
**Requirement**: TR-randomevents-001(池 schema 全整数) · TR-randomevents-002(档枚举闭集) · TR-randomevents-011(池/档表外部化烘焙 —— gap,执行体 ADR-014 阶段 2,本 story 挂载) · TR-randomevents-030(注入接口 —— gap,登记不立件)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线 · ADR-024: Kind 单一登记真源 · ADR-006: 定点域边界(D-21-17:`W_base` 是 int 计数,禁 Fix)· ADR-017: 门 A
**ADR Decision Summary**: 池条目五字段全整数:`key:int`、`W_base:int`(**非 Fix**,D-21-17 裁定)、档枚举 ∈ {威胁, 机会, 反应, 灾难}(恰一)、`spawn_anchor` ∈ {CLINIC_FRONT, TRAVEL_PATH, GATHER_POINT}(P0 三员;SETTLEMENT/BIOME_REGION 标 P1a)、触发方式 ∈ {随机, 脚本}(默认随机)+ 可选 `cause_flag`。作者态 JSON → 两阶段烘焙 → `.cooked`(`Fix` 旋钮写字符串经 `FixParse`;ScriptableObject 承载 = 构建错误 AC-52-09)。**构建期拒收表 = 18 谓词**(GDD 自陈 17、实测 18,ADR-024 §⑥;执行体归 ADR-014 阶段 2,本 story 交输入)。52 **零内容字段**:类型引用扫描(AC-52-03)证明 52 不 import / 不引用任何医学内容类型 —— 内容归 37。脚本条目(三案链 ×4)**不入池不入预算**。
**⚠️ 前置义务**:52 的 sim 程序集 `"noEngineReferences": true` **尚未声明**(AC-52-07/08 假通过风险)—— 本 story 把它升为 asmdef 断言;Roslyn analyzer 安装法未定(OQ spike)⇒ 构建期 hook 先用 Editor 脚本 + EditMode 断言双跑,不违禁等 spike。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: schema + 校验器 = 纯 C#;门 A 断言 = asmdef 引用集白名单(恰 = BCL)。

**Control Manifest Rules (this layer)**:
- Required: 全整数 schema;Fix 字段 JSON 写字符串;拒收表 = 显式 `throw` 硬失败;白名单方向(列表外构建期拒绝)
- Forbidden: `W_base` 用 Fix / float;52 程序集出现医学内容类型引用;ScriptableObject / `.asset` 承载池数据;运行期读 JSON
- Guardrail: 池空(0 条)合法(全脚本世界);单条池合法;`cause_flag` 闭集外值 = 拒收

---

## Acceptance Criteria

*From GDD `design/gdd/random-events.md`, scoped to this story:*

- [ ] 池条目 schema 绑定完整(五字段 + `cause_flag?`),全部整数域;`W_base:int` 非 Fix(静态类型断言)
- [ ] 档枚举恰一/条、闭集四员;`spawn_anchor` P0 三员(AC-52-42 构建期枚举校验;P1a 两员解析器存在但校验拒绝)
- [ ] **AC-52-03 零内容**:52 程序集类型引用扫描零命中医学内容类型(37/8/9 的内容件);泄漏 = 构建失败
- [ ] **拒收表 18 谓词**全部实装为构建期硬失败(MIN>MAX 档 / MAX−MIN<1 / 预告窗口≤0 / 各 cap≤0 / Σ档占比≤0 / BUDGET_MAX<BASE / WINDOW_SIZE≤0 / `TICKS_PER_DAY % ROLL_INTERVAL ≠ 0` / DEFER_MAX≤0 / SPAWN_AHEAD_DIST≤0 / TODMult>TOD_MULT_MAX / spawn_anchor∉enum / PAYLOAD_MAX≤0 等 —— 谓词表以 ADR-024 §⑥ 实测 18 条为准,逐条挂夹具)
- [ ] `AC-52-43`:P0「医馆不可损毁」= 构建期事实(F3 损伤字段在 P0 schema 中不存在,非运行期 if)
- [ ] 脚本条目通道(规则十一):`触发方式=脚本` 的条目不入池抽取 / 不入预算,注入接口 = 外部具名触发(三案链 trigger 由 37 定义,52 只接「注入一条」的整数命令)
- [ ] **AC-52-31 [B][I]**:脚本条目与随机条目同时到期时 **脚本优先**（同 tick 同 slot 冲突裁决）
- [ ] **AC-52-32 [B][I]**:P0 全程主线不因未遇随机事件卡住 + 三案链在满足 37 触发条件时必然进窗口（正向保证）
- [ ] 门 A:52 程序集 `"noEngineReferences": true` + 引用集白名单断言(恰 = BCL)入构建(承 ADR-017 §二 形制)
- [ ] 数据文件驱动(AC-52-41):池内容零硬编码于 C#;调参旋钮走 `FixParse`

---

## Implementation Notes

*Derived from ADR-014 / ADR-024 Implementation Guidelines:*

1. 阶段 2 绑定器落 `Editor.Tools` 族(`tools/` 下 per-schema binder,同 Story 48-001 形制,复用其校验框架但词表独立)。
2. 拒收表逐条 = `(谓词名, 违规夹具, 期望错误码)` 三元组数据表(禁散落在 if 里),表源链接 GDD 规则十一 / ADR-024 §⑥ 的 18 条实测清单;「GDD 写 17 实测 18」的差集注记写进代码注释(不改 GDD,GDD 回写归文档轮)。
3. 注入接口形状:`IEventDirector.Inject(scriptKey:int, ctx)` —— 只接整数 key;三案链 ×4 条目以 `触发方式=脚本` 入表(表内有、池外)。
4. Roslyn 未定 ⇒ 拒收表以「烘焙期 throw + EditMode 断言表」双跑交付;Roslyn spike 结论出来后迁移(迁移不新增判据,只换执行体,登记为后续义务)。
5. ⚠️ **数值冻结**:全部谓词的**阈值比较逻辑**是机制(冻结可写);表内具体数值(档占比、cap 值)是用户数值轮产物 —— 夹具用边界值(0 / ±1)不用「平衡值」。

---

## Out of Scope

- [Story 002]: 掷骰与流登记(池的消费方)
- [Story 003]: F1 管线(池条目的 W_i 计算)
- 37 的三案链内容与其 trigger 定义(52 只接注入形状)
- F3 损伤/重建 schema(P1a,本 story 只保证 P0 面「字段不存在」)
- Roslyn analyzer 安装 spike(独立工程项,OQ 结清后另开)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 拒收表逐条必红
  - Given: 18 条谓词各一份违规夹具(MIN>MAX / WINDOW_SIZE=0 / 非整除 ROLL_INTERVAL / anchor=SETTLEMENT(P1a)…)
  - When: 跑烘焙校验
  - Then: 18 次全 `throw`,错误定位到条目 key
  - Edge cases: 合规边界夹具(MIN==MAX 且 MAX−MIN=0 的合法读法按表定义)不误伤

- **AC-2**: schema 类型面(AC-52-01/02/03)
  - Given: 绑定后的池类型 + 52 程序集
  - When: 反射扫描
  - Then: 无 float/double/Fix 于 `W_base`;无医学内容类型引用;档枚举每条目恰 1
  - Edge cases: `cause_flag` 空串 = 合法(随机条目);∈ 闭集外 = 拒收

- **AC-3**: 脚本条目不入池
  - Given: 池含 2 随机 + 4 脚本(三案链)条目
  - When: 抽池集合导出
  - Then: 随机池 = 2 条;脚本条目经 `Inject` 可达且预算计数不受其影响
  - Edge cases: 全表皆脚本 ⇒ 随机池空合法(0 事件不报错)

- **AC-4**: 门 A 断言
  - Given: asmdef 配置
  - When: 构建期引用集检查
  - Then: `"noEngineReferences": true`;引用集恰 = BCL(命中 `UnityEngine.*` = 构建失败)
  - Edge cases: 注入接口参数若需 `WorldPos` → 用 `Sim.Contracts` 的整数格(非 `Vector3`)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/RandomEvents/event_pool_schema_test.cs` + `unity/Assets/Tests/EditMode/RandomEvents/event_reject_table_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: ADR-014 管线基建(既有)· ADR-024 kindgen(既有)
- Unlocks: Story 002(池就位才可抽)· Story 003(F1 消费 schema)· Story 006(锚点枚举消费)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `EventTier` — 事件档枚举（闭集四员）
- `SpawnAnchor` — 生成锚点枚举（P0 三员）
- `TriggerMode` — 触发方式枚举
- `EventPoolEntry` — 池条目 schema（五字段全整数）
- `EventPoolValidator` — 池条目校验器（拒收表）
- `IEventDirector` — 事件导演接口
- 测试: 10 条单元测试（全部通过）

**Deviations**: 
- 拒收表为简化版（5 条谓词），完整版 18 条归 ADR-014 阶段 2

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/RandomEvents/event_pool_schema_test.cs` — 10 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
