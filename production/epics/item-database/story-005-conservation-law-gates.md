# Story 005: 守恒律构建期与运行期门

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-004(守恒律在整数域内求值,weight 归一 · D-21-19)· TR-itemdb-029(Σ(weight × InstanceWeight) 的 int64 溢出上限)◆
◆ = `status: no-adr-by-design`,归属件 = GDD 自身;永不因补 ADR 转 ✅。
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006: 定点域边界数据契约
**ADR Decision Summary**: 守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`,`EFF_MAX ≤ 1`,**整数域内求值**;实耗按 `ActualConsumed_i = Ceil(inputs_i.qty / EFF)`(D-21-15);原稿量纲不齐须乘 `weight` 归一(D-21-19)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-006 Engine Knowledge Risk MEDIUM(不依赖 post-cutoff API)。

**Control Manifest Rules (this layer)**:
- Required: 守恒律整数域内求值;`EFF_MAX ≤ 1`;实耗 `ActualConsumed_i = Ceil(inputs_i.qty / EFF)`;weight 归一到同一最小单位(两侧同量纲)
- Forbidden: 浮点中转;逐项舍入(须先乘后比);`EFF_MAX > 1`
- Guardrail: 执法体 = 构建期断言,显式 `throw` 非 `Debug.Assert`;数值旋钮归用户(EFF_MIN/MAX、QTY_MULT_MAX 测试参数化)

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [x] **AC-21a-8**: 构建期守恒上界:QTY_MULT_MAX × Σ(weight × outputs_i.qty) > EFF_MAX × Σ(weight × inputs_j.qty) ⇒ 拒绝 —— 产出侧必须含 QtyMultiplier 上界;EFF_MAX > 1 亦拒(D-21-21)
- [x] **AC-21a-39**: 任意合法配方、任意 EFF ≤ EFF_MAX、任意 QtyMultiplier ⇒ Σ(weight × OutputQty_i) ≤ EFF_MAX × Σ(weight × ActualConsumed_j)(规则八/D-21-21)—— 两侧同量纲(重量),产出侧含 QtyMultiplier、投入侧取实耗非基数;整数域先乘后比、禁逐项舍入
- [x] **AC-21a-40**: EFF_MAX > 1 的任何常量表,加载 ⇒ 构建期硬失败
- [x] **AC-21a-56**: 常量表 EFF_MIN ≤ 0 或 EFF_MIN > EFF_MAX ⇒ 构建期硬失败(EFF 是 F1 除数,下端此前无人守)
- [x] **AC-21a-65**: 构建期以逐条同形极值式校验:Σ(weight_out × max(1, Round(outputs_i.qty × QTY_MULT_MAX))) ≤ EFF_MAX × Σ(weight_in × Ceil(inputs_j.qty / EFF_MAX)),违反即硬失败 —— D-21-32 落盘;聚合式保留为必要非充分,极值式为唯一硬门;整数域(Round=ROUND_HALF_AWAY_FROM_ZERO,先乘后比,禁浮点中转)

---

## Implementation Notes

*Derived from ADR-006 §Decision 四:*

- 守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`,`EFF_MAX ≤ 1`,**在整数域内求值**;实耗按 `ActualConsumed_i = Ceil(inputs_i.qty / EFF)`(D-21-15)—— ADR-006 §Decision 四
- **2026-09-14 二轮复核就地修正**:守恒律原式量纲不齐,须乘 `weight` 归一(D-21-19)—— 两侧必须同为「重量」量纲
- `weight` / `stack_max` 是 `int` 计数,不是 `Fix`(D-21-17)—— ADR-006 §Decision 一
- 全部舍入 `ROUND_HALF_AWAY_FROM_ZERO` 整数域内完成 —— ADR-006 §Decision 三(AC-65 的 Round 调用同一入口)
- 执法体统一形态 = **构建期断言**(manifest 元规则)
- GDD §Formulas F1 守恒律段 + AC-8/65 文档反例(w_in=10 / w_out=9 / EFF_MAX=1 / QTY_MULT_MAX=2 与 w_out=6 / QTY_MULT_MAX=1.5 两例)是本故事直接规格 —— 反例数字是 GDD 原文自带,照录;EFF_*/QTY_MULT_* 具体定值归用户数值轮

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: F1/F2 运行期求解本体(本故事只做构建期门 + 正向不变量断言);AC-3(常量表 cap 和)与 Story 006 的 AC-9 共用 `invalid_cap_sum.json`,归 006 的校验套件执行
- Story 004: AC-64 int64 溢出上界(先证不溢出)—— 本故事 AC-39/65 的先乘后比依赖该上界,但测试文件归 004
- Story 006 / 007: 其余负向夹具

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-8**: 构建期守恒上界:QTY_MULT_MAX × Σ(weight × outputs_i.qty) > EFF_MAX × Σ(weight × inputs_j.qty) ⇒ 拒绝 —— 产出侧必须含 QtyMultiplier 上界;EFF_MAX > 1 亦拒(D-21-21)。
  - Given: 配方 + 常量表;GDD 文档反例:w_in=10, w_out=9, EFF_MAX=1, QTY_MULT_MAX=2(基数式 9≤10 过、含乘子运行期 18>10 击穿)。
  - When: 构建期聚合守恒校验(两侧同量纲,weight 归一)。
  - Then: 不等式违反 ⇒ 硬失败;EFF_MAX > 1 ⇒ 硬失败(交叉 AC-40);恰好相等 ⇒ 过。
  - Edge cases: 单输入单输出;多条 outputs;EFF_MAX<1 使右侧收缩;weight 混合值(归一到同一最小单位);空 inputs/空 outputs 由配方校验前置拒(Edge Cases:凭空造物/销毁)。
  - Negative fixture: `invalid_conservation.json`

- **AC-21a-39**: 任意合法配方、任意 EFF ≤ EFF_MAX、任意 QtyMultiplier ⇒ Σ(weight × OutputQty_i) ≤ EFF_MAX × Σ(weight × ActualConsumed_j)(规则八/D-21-21)—— 两侧同量纲(重量),产出侧含 QtyMultiplier、投入侧取实耗非基数;整数域先乘后比、禁逐项舍入。
  - Given: 合法配方;EFF 遍历 (EFF_MIN … EFF_MAX];QtyMultiplier 遍历 [QTY_MULT_MIN, QTY_MULT_MAX];ActualConsumed_j = Ceil(inputs_j.qty / EFF)。
  - When: 整数域求和比较。
  - Then: 不等式恒成立;断言不经浮点、不经逐项舍入(先乘后比)。
  - Edge cases: EFF=EFF_MAX=1 零损耗取等;EFF=EFF_MIN 实耗最大(最宽裕);逐条 max(1,·) 地板触发的产出条目;全部修正取最负使产出走下界。
  - Negative fixture: 无(正向不变量;击穿类由 AC-8/AC-65 夹具承载)。

- **AC-21a-40**: EFF_MAX > 1 的任何常量表,加载 ⇒ 构建期硬失败。
  - Given: 常量表 EFF_MAX 以合法 Fix 形式取 >1 的值。
  - When: 常量表加载/校验。
  - Then: 硬失败;EFF_MAX 恰 =1(过,零损耗档);EFF_MAX < 1(过)。
  - Edge cases: EFF_MAX 写为浮点字面量(由 AC-41/57 先拒,防绕道);EFF_MAX 与 EFF_MIN 同时非法(AC-56 判下端)。
  - Negative fixture: AC 表未命名;与 `invalid_conservation.json` 同族记录(GDD 在 AC-8 夹具中已含「EFF_MAX>1 亦拒」的反例域,复用不新增名)。

- **AC-21a-56**: 常量表 EFF_MIN ≤ 0 或 EFF_MIN > EFF_MAX ⇒ 构建期硬失败(EFF 是 F1 除数,下端此前无人守)。
  - Given: EFF_MIN = 0 / 负值 / > EFF_MAX 三种常量表。
  - When: 常量表校验。
  - Then: 三者均硬失败;0 < EFF_MIN ≤ EFF_MAX 通过(GDD 现文只拒 `>`,EFF_MIN=EFF_MAX 退化为点集按现文过 —— 注记不擅自收紧)。
  - Edge cases: EFF_MIN 恰=0(拒,除零);负(拒,实耗为负=凭空造料);EFF_MIN=EFF_MAX>0(现文过);EFF_MIN 以浮点写(先被 FixParse 拒)。
  - Negative fixture: `invalid_eff_range.json`

- **AC-21a-65**: 构建期以逐条同形极值式校验:Σ(weight_out × max(1, Round(outputs_i.qty × QTY_MULT_MAX))) ≤ EFF_MAX × Σ(weight_in × Ceil(inputs_j.qty / EFF_MAX)),违反即硬失败 —— D-21-32 落盘;聚合式保留为必要非充分,极值式为唯一硬门;整数域(Round=ROUND_HALF_AWAY_FROM_ZERO,先乘后比,禁浮点中转)。
  - Given: 配方;GDD 文档反例:w_in=10, w_out=6, EFF_MAX=1, QTY_MULT_MAX=1.5, 单条 qty=1(聚合式 9≤10 过;运行期 HALF_AWAY 得 2 ⇒ 12>10 击穿)。
  - When: 构建期跑逐条极值式。
  - Then: 击穿组合硬失败;恰好相等过;聚合式(AC-8)同时满足但极值式失败时以极值式为准(硬门)。
  - Edge cases: max(1,·) 地板起作用的子域(qty 小 + QTY_MULT_MIN 低);平局取整(qty×QTY_MULT_MAX 恰 .5)远离零;多条 inputs 的 Ceil 累加;EFF_MAX<1 使投入侧 Ceil 放大;两式结论不一致的中间带(必须红)。
  - Negative fixture: `invalid_conservation_perline.json`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/conservation_law_gates_test.cs` — must exist and pass

**Status**: [x] Created —— 真身落 `unity/Assets/Tests/EditMode/ItemDatabase/conservation_law_gates_test.cs`
(装配 `Sim.Contracts.Tests`;**31** 个 `[Test]`,AC-8×7 + AC-39×5 + AC-40×4 + AC-56×5 + AC-65×8 +
TryMultiply×2)。账本路径同 Story 001/002/003/004 口径:Unity 不编译 `unity/Assets/` 之外的代码 ⇒
测试真身落 EditMode 树,`tests/unit/item_database/` 只承载账本与三个负向夹具。
**执行状态:NOT-RUN**(【超算】无 Unity Editor)⇒ 全绿判据待【桌面】跑出。

---

## Completion Notes
**Completed**: 2026-09-24
**Criteria**: 5/5 实现落盘(AC-8/39/40/56/65)
**执法体形态**: 分两处 —— 运行期算术原语 + 三谓词住 `unity/Assets/Sim/ItemDatabase/ConservationSolver.cs`
(装配 `Sim`,`noEngineReferences: true`);构建期四条门住
`unity/Assets/Editor.Tools.Gates/ConservationGates.cs`(错误列表形态,空 = 通过;显式 `throw`
由调用方 Story 008 烘焙管线聚合非空列表后执行)。**单一实现承重纪律**:四条门不复制任何算术,
判据一律由 `ConservationSolver` 谓词裁定;AC-65 逐条极值式**直接消费**运行期
`RecipeSettlementSolver.OutputQty` / `ActualConsumed` —— 构建期与运行期**同一实现**,
D-21-32 的缝(聚合式与逐条式不同形)在结构上被消除,靠 `test_perLineExtreme_gateVerdict_equalsSolverPredicate`
自证门/谓词同号,不靠「两处小心抄一致」。
**两式分工(D-21-32)**: 聚合式(AC-8)= 必要非充分,只作诊断/交叉检查(测试
`fixtureAggregatePassesButPerLineRejected` 断言**聚合过 + 极值红**的中间带必须红);
极值式(AC-65)= **唯一硬门**。
**夹具**: `invalid_conservation.json`(GDD 反例一 10/9/1/2 ⇒ 18>10)·
`invalid_conservation_perline.json`(反例二 10/6/1/"3/2" ⇒ 聚合 9≤10 过、Round 得 2 ⇒ 12>10 红)·
`invalid_eff_range.json`(eff_min=0;兄弟案 eff_min>eff_max 由测试代码构造)。数字照录 GDD 原文
(规格自带,非新造),其余均为**夹具值,非游戏平衡值**。
**范围边界**: 常量表 cap 和与 QTY_MULT 下界(AC-3/9 及其余负向夹具)= Story 006/007;
烘焙管线接线 = Story 008;容器 children 闭包 = Story 010。Story 003/004 全部文件本故事零改动。
**装配**: 零 asmdef 改动、零新 asmdef —— Gates 已引 Sim GUID(Story 004 加),EditMode 测试装配
`Sim.Contracts.Tests` 引用集已含全部所需 GUID(ADR-025 §④ 清单封闭)。
**Deviations**: None(旋钮扫描对两个非测试新文件模拟 stripComments+string 后 IndexOf 全 29 token,
零命中;EFF_MIN=EFF_MAX>0 按 GDD 现文**过**,未擅自收紧)。
**Test Evidence**: Logic — `unity/Assets/Tests/EditMode/ItemDatabase/conservation_law_gates_test.cs`(31 个 `[Test]`),账本 `tests/unit/item_database/conservation_law_gates_test.cs`
**Code Review**: Skipped(lean 模式)
**执行状态**: NOT-RUN(【超算】无 Unity Editor)⇒ 待【桌面】跑绿翻 VERIFIED;
桌面将生成 3 个新 `.meta`(ConservationSolver.cs / ConservationGates.cs / 测试文件),
需同 `a3ce361` 先例补 `chore(meta)` 提交。

---

## Dependencies

- Depends on: Story 001(FixParse / 舍入),Story 003(F1/F2 求解器输出形状),Story 004(AC-64 溢出上界)
- Unlocks: Story 006(校验套件其余夹具),Story 008(管线执行本套门)
