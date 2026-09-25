# Story 003: 配方结算唯一求解器(F1/F2)

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24

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

**Status**: [x] Created — 落点见下(Unity 只编译 `unity/Assets/` 树);**run: VERIFIED 2026-09-24 桌面 —— EditMode 175 全绿(Story 003 的 30 测 + Story 001/002 回归 145 全含) + PlayMode 7/7 绿**
- Logic(AC-1/2/3/4/47): `unity/Assets/Tests/EditMode/ItemDatabase/recipe_settlement_solver_test.cs`(装配 `Sim.Contracts.Tests`)
- Integration(AC-5/6): `unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs`(装配 `Gameplay.Tests`)
- **执行状态**: ✅ **VERIFIED 2026-09-24 桌面** —— EditMode 全量 **175 测全绿**(Story 003 的 30 测 + Story 001/002 回归 145 全含,含 `test_actualConsumed_efficiencyAtMostOne_neverBelowBase` 等;曾红 1 条 = 断言写了理想算术语义 9、定点世界实际产出 10,订正后绿,commit `2cf8092`)+ PlayMode **7/7 绿**(`RecipeSettlementSolverIntegrationTest`,桌面 XML 2026-09-24;同批 U1 3 条 spike 红与 003 无关,归 ADR-023 spike 批)
- **AC-21a-47(ADVISORY)**: 计时证据落 `production/qa/smoke-[date].md`,该目录**尚未建立** ⇒ 证据未生成(登记,不借绿)

## Completion Notes
**Completed**: 2026-09-24(用户显式 override —— verdict 曾为 BLOCKED 仅因测试 NOT-RUN;双评审修复后代码面无 BLOCKING。理由与 Story 002 同款:【超算】无 Unity Editor,NOT-RUN 是环境事实而非质量缺陷;桌面随后实跑翻 VERIFIED)
**Criteria**: 7/7 implemented & test-covered(AC-21a-1 · 2 · 3 · 4 · 47 → EditMode 30 测;AC-21a-5 · 6 → PlayMode 7 测;**run status: VERIFIED 2026-09-24 —— EditMode 175 全绿 + PlayMode 7/7**)
**Deviations**(均为 advisory,零 BLOCKING):
- **AC-21a-47 计时证据未生成**:判据要求落 `production/qa/smoke-[date].md`,而 `production/qa/` 目录**全仓从未建立**(Story 001/002 亦无先例)⇒ 证据文件不存在。**不借绿**:测试内 `[Category("Advisory")]` 冒烟只写 `TestContext.Out`,不计入任何绿判据。建 `production/qa/` 属项目级决定,不在本故事范围内擅建
- **AC-21a-6 扫描器的方法论边界**:文本扫描测**标识符子串**,剥离注释但不剥字符串字面量内的标识符(见 `tests/integration/item_database/README.md`);大小写不敏感已覆盖 camelCase 命名变体,但**真同义词**(如自建 `ComputeMultiplier`)不在表内 —— 该绕过面与 Story 002 AC-48 同型,属方法论固有局限,以 QA Test Cases 的标识符口径为准
- **GDD `:512` 示范数与 ADR-006 §三 冲突**:GDD 写 `19661 × 30 / 60 = 9830`(截断),ADR-006 §三 要求 `ROUND_HALF_AWAY_FROM_ZERO` ⇒ 实为 `9831`。**规则优先于示例**,实现取 9831,GDD 已就地加 2026-09-24 订正注(保留原文留闭环)
- **AC-21a-5/6 落 PlayMode 而非 EditMode**:两者判的是**跨系统结构事实**(MethodInfo 收敛 / 源码无第二份公式体),不依赖运行时 ⇒ unity-specialist 判「合理但次优」—— EditMode 更快且不触 Addressables 装配面。**非阻塞**,保留现状(移入 EditMode 需改装配引用集,收益边际)
- **入口接缝的成员面锁**仅锁**公开静态**成员(PlayMode `test_entryPointDelegates_haveNoFormulaBodyOfTheirOwn`)—— 私有/实例成员面不锁。18/19 落地时该测试自动纳入其源文件扫描
**Test Evidence**: Logic 真身 `unity/Assets/Tests/EditMode/ItemDatabase/recipe_settlement_solver_test.cs`(30 functions,装配 `Sim.Contracts.Tests`)+ Integration 真身 `unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs`(7 functions,装配 `Gameplay.Tests`);**run = VERIFIED 2026-09-24 桌面 —— EditMode 175 全绿(30 测含) + PlayMode 7/7**
**Code Review**: Complete —— `/code-review`(2026-09-23/24,lean 模式)= unity-specialist **APPROVED WITH SUGGESTIONS**(R1/R2 属「Done 前应处理、不阻塞合入」)+ qa-tester **GAPS**(§2/3.1/3.2/4a–4f/5.2/5.3/6);**全部发现已修复并经 grep 核验**:
- R1:`RecipeSettlementSolver.cs:188` XML 注释 9830 → 9831(舍入契约优先)
- R2:GDD `:512` 加 2026-09-24 订正注
- QA §4a:曲线分母 ≤ 0 → 具名 `InvalidOperationException`(`RequirePositiveCurveDivisor`),不再裸 `DivideByZeroException`
- QA §4b:`InputQuality < 1` → 具名拒(否则下钳越过上钳 = 刷品级)
- QA §4c/4d:`CurveScaled` / `Interpolate` 域外输入钳入 `[0, cap]`(QualityMod 不超 cap;EFF 不超 EFF_MAX ⇒ 不凭空造料)
- QA §4e:`ToInt32Checked` 溢出覆盖测试补齐
- QA §4f:`RequirePositiveEfficiency` 上提进 `Solve`,坏常量表**不随 Inputs 长度分叉**
- QA §2/3.2:AC-21a-4 两条测试改经求解器公开具名量 `SumOfModifiers`(去测试体内自加的同义反复)
- QA §3.1:补中点舍入可分辨测试(`5 × 1/2 = 2.5` → 3,区分 ties-to-even)
- QA §5.2:PlayMode 自证测试改调**与真扫描同一**的 `MatchIdentifiers` 谓词(修伪证)
- QA §5.3:扫描改 `OrdinalIgnoreCase`;删 `EnvMod_total` / `max(1,` 两个**死项**(非合法 C# 文本);补 `CeilDiv`
- QA §6:AC-47 注释「最大值」→「均值」(代码本就取均值)
**残留**:AC-21a-47 的 `production/qa/smoke-*.md` 证据(见 Deviations 第 1 条)—— 建 `production/qa/` 后补
**First follow-up**: ✅ 全部完成(2026-09-24)① 桌面 Unity 已生成 `.meta`(用户确认跑绿);② EditMode `RecipeSettlementSolverTest` 30 测 + 回归全量 = **175 全绿**;③ PlayMode `RecipeSettlementSolverIntegrationTest` **7/7 绿**;④ 状态翻 VERIFIED(本文件 + EPIC.md 同批);**无 open 跟进**。⚠️ 注意:同批 PlayMode 跑出 3 条 U1 spike 红(`U1SceneSpikesTest`,`No Location found for Key=U1_Spike*`)与 003 无关 —— 归 ADR-023 spike 批,桌面侧如需拿 U1 数字:先 `大医精诚/Spike/Setup U1 Spikes` 重建场景 + `BuildPlayerContent`,再在 Test Runner **PlayMode 标签**跑(勿 Run on Player)

---

## Dependencies

- Depends on: Story 001(FixParse / 舍入契约),Story 002(schema 类型)
- Unlocks: Story 005(守恒律门消费求解器输出形状),Story 009(ActualConsumed 写流)
