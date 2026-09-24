# Story 006: 配方表写入期校验套件

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-007(配方 schema)◆ · TR-itemdb-024(配方解锁与技能门,与 30 的接口)◆
◆ = `status: no-adr-by-design`,归属件 = GDD 自身;永不因补 ADR 转 ✅。
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014: 数据管线与 JSON 解析器
**ADR Decision Summary**: 全量校验失败 = 构建失败,不是警告;作者态解析失败在烘焙期即失败、不进运行期;逐 schema 白名单 + `schema_version` + 长度校验全部烘焙期硬失败;禁 `JsonConvert.DeserializeObject<T>`(数字经 double 中转 = 浮点泄漏)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-014 Engine Knowledge Risk MEDIUM(Addressables 6.2+ 异常为 post-cutoff,须实测;本故事校验逻辑本身不依赖)。

**Control Manifest Rules (this layer)**:
- Required: 全量校验失败 = 构建失败;作者态解析失败在烘焙期即失败;逐 schema 白名单;显式 `throw` 非 `Debug.Assert`
- Forbidden: 校验失败降级为警告;`JsonConvert.DeserializeObject<T>`;负向夹具缺省(只验合法值不算通过)
- Guardrail: 每条「校验拒绝」AC 必须配 `tests/unit/item_database/fixtures/invalid_*.json` 负向夹具,断言构建期硬失败(GDD 图例负向夹具铁律)

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [x] **AC-21a-7**: 任一 outputs[].qty ≤ 0 ⇒ 拒绝(逐条 outputs_i.qty,非配方级标量 BaseQty)
- [x] **AC-21a-9**: Σ(正的 cap) + max(0, ENV_MOD_MAX) > QTY_MULT_MAX − 1 ⇒ 拒绝(ENV_MOD_MAX 必须计入)
- [x] **AC-21a-10**: QTY_MULT_MIN ≥ QTY_MULT_MAX ⇒ 拒绝(区间为空)
- [x] **AC-21a-11**: RETAIN_MIN > RETAIN_MAX / RETAIN_MAX > 1 / RETAIN_MIN ≤ 0 三条件之一 ⇒ 拒绝
- [x] **AC-21a-12**: ENV_MOD_MIN > ENV_MOD_MAX ⇒ 拒绝
- [x] **AC-21a-16**: 配方项(inputs 或 outputs)qty ≤ 0 ⇒ 拒绝(零量=凭空造物/静默销毁)
- [x] **AC-21a-17**: 配方项 item_key 外键悬空(inputs/outputs 引用不存在的物品条目)⇒ 拒绝
- [x] **AC-21a-18**: duration_ticks ≤ 0 ⇒ 拒绝(规则五;单位是 tick 不是秒)
- [x] **AC-21a-19**: skill_gate ∉ [0, SKILL_CAP] ⇒ 拒绝(> SKILL_CAP 的配方永不可制,是数据错误)
- [x] **AC-21a-20**: min_quality < 1 或 > MAX_QUALITY ⇒ 拒绝(准入闸,非品级出口)
- [x] **AC-21a-66**: 全量配方表装载期按 owner ∈ {process, craft, build} 分三子集 ⇒ 并 = 全表、两两交 = ∅、任一配方缺 owner 即硬失败(D-21-30;18 的 AC-18-18 断调用方半边)

**共同断言口径**: 每条 = 负向夹具注入 → 构建期(导入/烘焙/EditMode)硬失败(显式 throw,非静默忽略);合法夹具对照通过。「只验合法值不算通过」(GDD 图例负向夹具铁律)。

---

## Implementation Notes

*Derived from ADR-014 §Decision:*

- **全量校验失败 = 构建失败,不是警告**;作者态解析失败在**烘焙期**即失败,**不进运行期** —— ADR-014 §三:192 · §二:160
- 逐 schema 维护**已知键白名单:未知键 = 烘焙期硬失败**;每文件带 `schema_version`,版本不匹配 = 硬失败;数组长度校验 —— ADR-014 §三:179,184,191
- 禁 `JsonConvert.DeserializeObject<T>` / `JObject.Parse`(数字经 `double`/`decimal` 中转 = 浮点泄漏,绕开 `FixParse`)—— ADR-014 §三:173
- **执法体统一形态 = 构建期断言,不落成散文** —— manifest 元规则(AC-3 正反判据与 Story 003 共用 `invalid_cap_sum.json`)
- GDD §Acceptance Criteria 组三(AC-7…27 各带夹具表)是本故事直接规格 —— 夹具文件名照 GDD,不擅造;SKILL_CAP 引 skill-system,不写死

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: schema 类型本体与复合主键 / 枚举闭合(AC-21/22/27/59)
- Story 004: axis 数组长度与地板(AC-50/50b/61/62)—— 配方侧长度校验不在本故事
- Story 005: 守恒律与 EFF 区间(AC-8/39/40/56/65)—— 本故事 AC-9/10/11/12 是常量表区间校验,与 005 的 EFF 校验同管道不同条目
- Story 007: 物品表侧负向夹具(AC-13/14/15/23/24/25)
- Story 008: 烘焙管线执行这些校验的落点(本故事先写校验器 + 夹具,管线接线归 008)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-7**: 任一 outputs[].qty ≤ 0 ⇒ 拒绝(逐条 outputs_i.qty,非配方级标量 BaseQty)。
  - Given: 配方某条 outputs 记录 qty=0 或 qty=−n。
  - When: 构建期校验。
  - Then: 硬失败;全部 qty ≥ 1 通过。
  - Edge cases: 仅一条非法、多条非法;inputs 侧 qty≤0 由 AC-16 夹具承载(同判据不同表);qty 为 0.5 浮点(先被类型/整数校验拒)。
  - Negative fixture: `invalid_base_qty.json`

- **AC-21a-9**: Σ(正的 cap) + max(0, ENV_MOD_MAX) > QTY_MULT_MAX − 1 ⇒ 拒绝(ENV_MOD_MAX 必须计入)。
  - Given: 常量表 cap 和超限(含 ENV_MOD_MAX 为正且大)。
  - When: 构建期常量校验。
  - Then: 硬失败;≤ 边界(过)。
  - Edge cases: 仅 ENV_MOD_MAX 单独把式子推过界(cap 全小)—— 原稿漏 EnvMod 的回归点;ENV_MOD_MAX≤0 时该项为 0;与 AC-3 正反同判据。
  - Negative fixture: `invalid_cap_sum.json`

- **AC-21a-10**: QTY_MULT_MIN ≥ QTY_MULT_MAX ⇒ 拒绝(区间为空)。
  - Given: QTY_MULT_MIN = QTY_MULT_MAX 及 MIN > MAX 两表。
  - When: 常量校验。
  - Then: 二者均硬失败;MIN < MAX 通过。
  - Edge cases: 相等(拒,含等号!);差一个最小单位(过)。
  - Negative fixture: `invalid_qty_range.json`

- **AC-21a-11**: RETAIN_MIN > RETAIN_MAX / RETAIN_MAX > 1 / RETAIN_MIN ≤ 0 三条件之一 ⇒ 拒绝。
  - Given: 三张非法常量表(或同表三子条件轮换)。
  - When: 常量校验。
  - Then: 三条件各触发一次硬失败;0 < RETAIN_MIN ≤ RETAIN_MAX ≤ 1 通过(RETAIN_MAX>1 会被 clamp 掩盖,必须显式拒)。
  - Edge cases: RETAIN_MAX 恰=1(过,满技能全保);RETAIN_MIN 恰=0(拒,≤0);RETAIN_MIN=RETAIN_MAX(过,区间退化点)。
  - Negative fixture: `invalid_retain.json`(GDD 指名单一文件;实现可含三条子记录覆盖三条件)

- **AC-21a-12**: ENV_MOD_MIN > ENV_MOD_MAX ⇒ 拒绝。
  - Given: 环境修正区间倒置常量表。
  - When: 常量校验。
  - Then: 硬失败;MIN ≤ MAX 通过(可为负域,MIN<0 合法)。
  - Edge cases: 相等(过 —— GDD 只拒 `>`);全负区间(过);MIN/MAX 跨零(过)。
  - Negative fixture: `invalid_env_range.json`

- **AC-21a-16**: 配方项(inputs 或 outputs)qty ≤ 0 ⇒ 拒绝(零量=凭空造物/静默销毁)。
  - Given: inputs[0].qty=0 / outputs[0].qty=−1 轮换。
  - When: 构建期校验。
  - Then: 硬失败;双侧全 ≥1 通过。
  - Edge cases: inputs 空数组 / outputs 空数组(Edge Cases:独立拒绝,归本族同管道);qty 为字符串"0"(类型错误先拒)。
  - Negative fixture: `invalid_recipe_qty.json`

- **AC-21a-17**: 配方项 item_key 外键悬空(inputs/outputs 引用不存在的物品条目)⇒ 拒绝。
  - Given: inputs[].item_key 指向 (base_id, state) 不存在的组合;outputs 侧轮换。
  - When: 构建期外键校验。
  - Then: 硬失败;双向(入/出)都查;base 存在但 state 不成条目(如 raw 存在而 extracted 未登记)也算悬空。
  - Edge cases: 仅 state 错、仅 base 错;跨 base 配方(柳树皮→水杨酸)本身合法 —— 外键各自独立存在即过;大小写敏感。
  - Negative fixture: `invalid_recipe_fk.json`

- **AC-21a-18**: duration_ticks ≤ 0 ⇒ 拒绝(规则五;单位是 tick 不是秒)。
  - Given: duration_ticks = 0 / 负值。
  - When: 构建期校验。
  - Then: 硬失败;=1(过,单 tick 配方)。
  - Edge cases: 极大 tick(int 上界,溢出交 AC-64 族);写成秒值(无法区分,归语义审查 —— 本 AC 只判 >0)。
  - Negative fixture: `invalid_duration.json`

- **AC-21a-19**: skill_gate ∉ [0, SKILL_CAP] ⇒ 拒绝(> SKILL_CAP 的配方永不可制,是数据错误)。
  - Given: skill_gate = −1 / SKILL_CAP+1 轮换(SKILL_CAP 引 skill-system)。
  - When: 构建期校验。
  - Then: 硬失败;0 与 SKILL_CAP 恰在界上(过)。
  - Edge cases: skill_gate=0(无门槛,过);=SKILL_CAP(过);SKILL_CAP 本身变更时测试随常量表走(不写死 60 —— GDD 载 60 属引用)。
  - Negative fixture: `invalid_skill_gate.json`

- **AC-21a-20**: min_quality < 1 或 > MAX_QUALITY ⇒ 拒绝(准入闸,非品级出口)。
  - Given: min_quality = 0 / MAX_QUALITY+1 轮换(MAX_QUALITY 数值待用户定)。
  - When: 构建期校验。
  - Then: 硬失败;=1 与 =MAX_QUALITY(过)。
  - Edge cases: MAX_QUALITY 变更后旧配方失效(同 D-21-7 双向耦合型 —— 校验须随常量表重跑);min_quality 与输入实例 quality 的运行期比较归 18/19 调用方,不在本 AC。
  - Negative fixture: `invalid_min_quality.json`

- **AC-21a-66**: 全量配方表装载期按 owner ∈ {process, craft, build} 分三子集 ⇒ 并 = 全表、两两交 = ∅、任一配方缺 owner 即硬失败(D-21-30;18 的 AC-18-18 断调用方半边)。
  - Given: 全量配方表,每条含 owner 字段。
  - When: 装载期三子集划分。
  - Then: S_process ∪ S_craft ∪ S_build = 全表;两两交集为空;任一记录缺 owner ⇒ 硬失败;owner 取枚举外值 ⇒ 硬失败(同 AC-22 闭合口径)。
  - Edge cases: 单子集空表(合法 —— 并仍等全表);owner 字段存在但为 null(缺字段同罚);重复 recipe_id 跨子集(唯一性由 recipe_id 校验,owner 划分仍须互斥)。
  - Negative fixture: `invalid_recipe_owner.json`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/recipe_validation_fixtures_test.cs` — must exist and pass;负向夹具落 `tests/unit/item_database/fixtures/`

**Status**: [x] Created —— 真身 = `unity/Assets/Tests/EditMode/ItemDatabase/recipe_validation_fixtures_test.cs`(47 [Test]:AC-7×4/9×4/10×4/11×5/12×4/16×4/17×5/18×3/19×4/20×4/66×6)+ 11 负向夹具 `tests/unit/item_database/fixtures/invalid_*.json`(账本路径 = `tests/unit/item_database/recipe_validation_fixtures_test.cs`,Unity 不编译 Assets 外 —— Story 001–005 同一先例);**执行 ✅ VERIFIED 2026-09-24 桌面 EditMode 317 全绿**(前批 270 + 本批 47)

---

## Completion Notes
**Completed**: 2026-09-24
**Criteria**: 11/11 已实现(AC-7/9/10/11/12/16/17/18/19/20/66),逐条 = 负向夹具注入 → 构建期拒 + 合法对照过
**Deviations(ADVISORY)**:
- ① AC-7 的「qty 为 0.5 浮点」边界**未单测** —— `RecipeEntry.Qty` 是 `int`,结构上不可表达(类型/整数校验先拒);浮点泄漏的执法归绑定层(Story 008 管线),本故事结构排除。
- ② AC-16 空 inputs / 空 outputs 按**拒绝**实现并有测(`test_recipeQty_emptyInputsOrEmptyOutputs_rejected`)—— 承 GDD :759/:760「校验拒绝」原文;QA Edge「独立拒绝,归本族同管道」同义。
- ③ AC-9 错误文案**列不等式两侧 raw 分量、不重算 Σ** —— 防在门里二次抄算术(单一实现纪律:算术只活在 `RecipeSettlementConstantTableValidator.IsSelfConsistent`,门只消费其真假)。
**范围边界**:AC-24 归 Story 007;11 条执法体的**调用方**(烘焙管线接线)归 Story 008 —— 本故事只交付纯函数 + 夹具,文件内零非注释 `throw`(聚合后 throw 是 008 的义务)。
**装配**:零 asmdef 改动、零新 asmdef —— 门用 `Editor.Tools.Gates` 既有 GUID 引用集(Story 002/004 加),测试用 `Sim.Contracts.Tests` 既有引用集(ADR-025 §④ 清单封闭不受影响)。
**Test Evidence**: Logic —— 真身 `unity/Assets/Tests/EditMode/ItemDatabase/recipe_validation_fixtures_test.cs`(47 [Test])+ 11 负向夹具。
**Code Review**: Skipped(lean 模式,承 Story 004/005 先例)
**执行状态**: ✅ **VERIFIED 2026-09-24 桌面** —— EditMode **317 全绿**(前批 270 + 本批 47);同批 PlayMode **12 全绿**(含上条 AC-21a-6 白名单修复回归确认)。
**同批修复(非本故事范围,Story 003 扫描器)**: PlayMode `test_formulaBodyIdentifiers_duplicatedOutsideUniqueSolver_none` 因 Story 005 新增 `ConservationSolver.cs` 触发 3 条命中(`QtyMultiplier`/`ActualConsumed`/`Efficiency`)—— 诊断为**委托调用 + 大小写不敏感参数名误命中**,非重复公式体;已在 `recipe_settlement_solver_test.cs::Whitelist()` 增条目④放行(带理由),复扫 violations 空、自证测试独立仍能命中合成违例;**桌面 PlayMode 实跑已确认转绿**。
**已知黄警(非本故事引入,不阻塞)**: `Assembly Definition File 'Assets/Gameplay.Presentation/Gameplay.Presentation.asmdef' will not be compiled, because it has no scripts associated with it` —— ADR-025 §① 六装配清单**预先登记**的 L5 边界程序集,当前目录**零 .cs**(表现层故事尚未落地);Unity 对空程序集跳过编译属预期行为,EditMode/PlayMode 全绿即证无影响。`Gameplay.UI.asmdef` 同型(同样零脚本)。首个表现层脚本落盘后自动消失;**不加占位脚本**(无意义内容,ADR-025 清单封闭性下徒增噪音)。

---

## Dependencies

- Depends on: Story 001(FixParse),Story 002(schema 类型),Story 005(EFF/守恒门同管道)
- Unlocks: Story 007(物品表校验套件),Story 008(管线执行本套校验)
