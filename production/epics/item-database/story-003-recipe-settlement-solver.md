# Story 003: 配方结算唯一求解器(F1/F2)

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-23

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-013(配方产出在定点域内确定性可重放)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟与状态同步模型 · ADR-006(次): 定点域边界数据契约
**ADR Decision Summary**: 全部模拟数学在整数定点域(int64 / Q16.16),中间乘法落 Q32.32 再移位回,哈希用 `SplitMix64`;病史事件流是唯一真源 ⇒ 求解器必须是无外部输入的纯函数、结果确定性可重放;ADR-006 补边界纪律:舍入 `ROUND_HALF_AWAY_FROM_ZERO` 整数域内完成,守恒律整数域求值。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: ADR-005 Engine Knowledge Risk HIGH —— IL2CPP 逐位性需实测,但裁决本身不依赖该实测(刻意不用 post-cutoff API);AC-21a-29 的跨平台对拍归 Story 011。

**Control Manifest Rules (this layer)**:
- Required: 全部模拟数学在整数定点域;`Fix` 纯值 struct 承载 Q16.16,中间乘法落 Q32.32 再移位回;128 位中间结果 = 手工 hi/lo 两 `ulong`(ADR-005 Amendment G)
- Forbidden: `System.Int128` / `BigInteger` / 条件分支式 128 位;`Math.Round` 默认 ties-to-even;float/double 中转
- Guardrail: 求解器 = 纯函数,入参即全部输入,不读运行时对象(可重建性三源不变量,manifest 交叉约束 4)

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-1**: 任意合法配方(逐条 outputs_i),施加任意合法 ΣM(含全部修正取最小、ENV_MOD = ENV_MOD_MIN 的极端组合),每条 OutputQty_i = max(1, Round(outputs_i.qty × QtyMultiplier)) ≥ 1 —— 零产出结构性不可能(下界由 max(1,·) 兜住,不依赖 QTY_MULT_MIN 取值)
- [ ] **AC-21a-2**: 任意 InputQuality ∈ [1, MAX_QUALITY]、任意 CraftSkill ∈ [0, SKILL_CAP],F2 求解 ⇒ OutputQuality ≤ InputQuality —— 品级永不被抬高(刷品级路径不存在)
- [ ] **AC-21a-3**: GIVEN 常量表(非某次求解)校验 ⇒ Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1 —— 常量表自洽性检查(非求解器行为)
- [ ] **AC-21a-4**: F1 的 QtyMultiplier 求解,打乱各修正项(SkillMod/QualityMod/EquipMod/EnvMod_total)施加顺序,结果逐位相同 —— 顺序无关性(仅在定点整数域成立)
- [ ] **AC-21a-5** [I]: 18 炮制与 19 制作各自发起结算,传入相同参数集 ⇒ 两者走同一个函数 —— 代码审查 + 单元测试双重验证
- [ ] **AC-21a-6** [I]: grep 21 求解器源码,17/18/19 不含任何重复结算实现
- [ ] **AC-21a-47**(单次配方求解侧): Performance:物品表全量加载 + 单次配方求解 < 1 ms(GDD 原文阈值)[D] 级冒烟

---

## Implementation Notes

*Derived from ADR-005 §Decision (primary) / ADR-006 (secondary):*

- 全部模拟数学在**整数定点域**:int64 承载 Q16.16 的 `Fix` 纯值 struct,中间乘法落 Q32.32 再移位回;哈希用 `SplitMix64` —— ADR-005 §Decision 一
- 128 位中间结果唯一类型 = 手工 hi/lo 两 `ulong` 带进位(32 位数字四路拆分,交叉项全程无符号 + 掩码提取,禁有符号右移);**禁 `System.Int128`**、**禁 `BigInteger`**、无条件钉 hi/lo 不留条件分支 —— ADR-005 Amendment G
- 舍入纪律:**保守带内边界搜索 = 向下保守**(宁多扫一步);表现层输出 = 就近舍入 —— ADR-005 §Decision 一;求解器内部统一 `ROUND_HALF_AWAY_FROM_ZERO` —— ADR-006 §Decision 三
- 守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`,`EFF_MAX ≤ 1`,**整数域内求值**;实耗按 `ActualConsumed_i = Ceil(inputs_i.qty / EFF)` —— ADR-006 §Decision 四(运行期公式;构建期门归 Story 005)
- 病史事件流是唯一真源 ⇒ 同参数集必须逐位产出同结果(AC-4 顺序无关性即此性质的实现面)—— ADR-005 §Decision
- GDD F1/F2 公式体(§Formulas,`:468-621`)是本故事的直接规格:OutputQty_i、QtyMultiplier clamp、EnvMod_total clamp、OutputQuality = clamp(Round(InputQuality × Retain(CraftSkill)), 1, InputQuality)—— 公式照 GDD,数值待用户

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: F4 堆叠重量与 F5 品级→时间轴(不同公式,不同落点)
- Story 005: 构建期守恒律门(AC-8/39/40/56/65)—— 本故事 AC-3 是常量表自洽检查,与 Story 005 的 AC-9 正反同判据、共用 `invalid_cap_sum.json`
- Story 011: 跨平台黄金哈希(AC-28/29)与 F1–F5 全量静态扫描(AC-30)
- Story 009: ActualConsumed 进 Craft 事件载荷(本故事只算出实耗,不写流)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-1**: 任意合法配方(逐条 outputs_i),施加任意合法 ΣM(含全部修正取最小、ENV_MOD = ENV_MOD_MIN 的极端组合),每条 OutputQty_i = max(1, Round(outputs_i.qty × QtyMultiplier)) ≥ 1 —— 零产出结构性不可能(下界由 max(1,·) 兜住,不依赖 QTY_MULT_MIN 取值)。
  - Given: 合法配方表(每条 outputs_i.qty > 0);ΣM 极端负组合使 QtyMultiplier 被 clamp 至 QTY_MULT_MIN。
  - When: 调用 F1 逐条求解。
  - Then: ∀i: OutputQty_i ≥ 1;构造 outputs_1.qty=1 且 Round(1×QTY_MULT_MIN)=0 的组合时,结果仍 =1(max 地板生效)。
  - Edge cases: outputs_i.qty=1 + 最负 ΣM;多条 outputs 混合(配方级 QtyMultiplier、逐条 max);QTY_MULT_MIN 取任意正值均不得击穿下界;Round 平局落在 0.5 时远离零(交叉 AC-42)。
  - Negative fixture: 无(GDD 未命名;正向性质测试)。

- **AC-21a-2**: 任意 InputQuality ∈ [1, MAX_QUALITY]、任意 CraftSkill ∈ [0, SKILL_CAP],F2 求解 ⇒ OutputQuality ≤ InputQuality —— 品级永不被抬高(刷品级路径不存在)。
  - Given: InputQuality 遍历 {1 … MAX_QUALITY}(MAX_QUALITY=数值待用户定,以常量表实际值为界);CraftSkill 遍历 {0, SKILL_CAP} 及中点。
  - When: 调用 F2:OutputQuality = clamp(Round(InputQuality × Retain(CraftSkill)), 1, InputQuality)。
  - Then: 全组合 OutputQuality ≤ InputQuality 且 ≥ 1;clamp 上界是 InputQuality 而非 MAX_QUALITY。
  - Edge cases: InputQuality=1 ⇒ 恒等 clamp(·,1,1)=1(全部技能档);CraftSkill=0 ⇒ Retain=RETAIN_MIN>0 保住部分不归零;Retain=RETAIN_MAX=1 + 满技能 ⇒ 取等 OutputQuality=InputQuality。
  - Negative fixture: 无。

- **AC-21a-3**: GIVEN 常量表(非某次求解)校验 ⇒ Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1 —— 常量表自洽性检查(非求解器行为)。
  - Given: 常量表:SKILL_MOD_CAP、QUAL_MOD_CAP、EQUIP_MOD_CAP(仅取正项)、ENV_MOD_MAX、QTY_MULT_MAX。
  - When: 构建期跑常量自洽断言。
  - Then: 不等式成立才通过;超限硬失败(即 AC-21a-9 的反面)。
  - Edge cases: ENV_MOD_MAX ≤ 0 ⇒ max(0,·)=0 项消失;全部 cap=0(死值旋钮,过不等式但另一侧死值风险归数值轮);ENV_MOD_MAX 恰使等式成立(过)。
  - Negative fixture: 与 AC-21a-9 共用 `invalid_cap_sum.json`。

- **AC-21a-4**: F1 的 QtyMultiplier 求解,打乱各修正项(SkillMod/QualityMod/EquipMod/EnvMod_total)施加顺序,结果逐位相同 —— 顺序无关性(仅在定点整数域成立)。
  - Given: 同一参数集,全部 4! 种施加排列(及含重复项的子集排列)。
  - When: 按每种顺序求 ΣM → clamp → QtyMultiplier → OutputQty。
  - Then: 各排列结果 Fix raw 逐位相等(long 相等,非近似);断言在整数域做,禁用 float 中转。
  - Edge cases: 某项为 0 的排列;含负 EnvMod;clamp 到 QTY_MULT_MIN/QTY_MULT_MAX 边界的排列;两项恰好抵消。
  - Negative fixture: 无。

- **AC-21a-5** [I]: 18 炮制与 19 制作各自发起结算,传入相同参数集 ⇒ 两者走同一个函数 —— 代码审查 + 单元测试双重验证。
  - Given: 同一配方、同一组输入参数;分别经 18 侧入口与 19 侧入口发起。
  - When: 双路径结算。
  - Then: 输出(OutputQty 数组 / OutputQuality / ActualConsumed / EFF)逐位相同;反射/结构断言两入口收敛到 21 的同一 Solver 单点(如委托指向同一 MethodInfo),非各自实现。
  - Edge cases: 同一配方被 18 与 19 同时读取(Edge Cases 明文「合法」);n=1,m=1 特例与 n>1 特例同参数对拍;入口只构造参数、不含公式体(交叉 AC-6)。
  - Negative fixture: 无。

- **AC-21a-6** [I]: grep 21 求解器源码,17/18/19 不含任何重复结算实现。
  - Given: src 中 17 采集 / 18 炮制 / 19 制作程序集与 21 求解器源码。
  - When: 静态扫描(grep/Roslyn):QtyMultiplier、Retain(、Ceil(.../EFF)、max(1, Round( 公式体标识符。
  - Then: 公式体仅存在于 21 求解器;17/18/19 仅构造参数并调用单点;出现重复体 ⇒ 失败。
  - Edge cases: 命名变体(同义词表须显式登记,防换名绕过);17 的 F3 采集品级分布是 17 拥有的动作(GDD 明文不归 F1 唯一求解器)—— 扫描白名单须含此例外,防误报。
  - Negative fixture: 无。

- **AC-21a-47**(单次配方求解侧): Performance:物品表全量加载 + 单次配方求解 < 1 ms(GDD 原文阈值,数据量小非瓶颈)。
  - Given: 全量配方表已加载;单次求解调用。
  - When: 计时(Stopwatch)单次 F1+F2 求解。
  - Then: < 1 ms(GDD 原文数值)。
  - Edge cases: 最大 outputs 条数配方;冷/热各一次。
  - Negative fixture: 无。非确定性计时 ⇒ 不入 BLOCKING 确定性套件;按 GDD [D] 级记冒烟/基准证据 `production/qa/smoke-[date].md`(ADVISORY 门)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/recipe_settlement_solver_test.cs` — must exist and pass
- Integration (AC-5/6): `tests/integration/item_database/recipe_settlement_solver_test.cs`

**Status**: [x] Created — 落点见下(Unity 只编译 `unity/Assets/` 树)
- Logic(AC-1/2/3/4/47): `unity/Assets/Tests/EditMode/ItemDatabase/recipe_settlement_solver_test.cs`(装配 `Sim.Contracts.Tests`)
- Integration(AC-5/6): `unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs`(装配 `Gameplay.Tests`)
- **执行状态**: 【超算】无 Unity Editor ⇒ EditMode / PlayMode 均 **NOT-RUN**;须【桌面】打开 Unity 生成 `.meta` 后跑(承 Story 001/002 先例)
- **AC-21a-47(ADVISORY)**: 计时证据落 `production/qa/smoke-[date].md`,该目录**尚未建立** ⇒ 证据未生成(登记,不借绿)

---

## Dependencies

- Depends on: Story 001(FixParse / 舍入契约),Story 002(schema 类型)
- Unlocks: Story 005(守恒律门消费求解器输出形状),Story 009(ActualConsumed 写流)
