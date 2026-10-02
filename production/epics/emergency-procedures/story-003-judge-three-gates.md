# Story 003: Judge 三扇门定点纯函数

> **Epic**: 急救动作模块
> **Status**: Complete ✅ 2026-10-02 (双代理评审修复后 16/16 测试通过)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 10h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则四 判定 = 定点纯函数 · F-10.2 SkillMul · F-10.3 三扇门 · F-10.3b 稳度时序误差 · F-10.4 处置强度)
**Requirement**: TR-emergency-004(Judge 纯函数)· TR-emergency-005(SkillMul 定义者=10)· TR-emergency-006(稳度以时序误差表述)· TR-emergency-007(三档映射)· TR-emergency-008(result_mul 消费)· TR-emergency-020(edges≤1 定义域)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(整数域求值)· ADR-006 §三(`ROUND_HALF_AWAY_FROM_ZERO`,中间积 Q32.32 **仅一次舍入**)· ADR-012(单元级黄金夹具:F-10.4 缩放 / F-10.3b 整数比值)
**ADR Decision Summary**: `Judge(agg, action, ctx) → {Applied, AppliedWeak, Missed}` 枚举非分值;三门 = 幅度(`mag_peak ≥ MAG_THRESHOLD_EFFECTIVE`)/ 节奏(`edges ≥ MIN_EDGES` **或** `hold_ticks ≥ MIN_HOLD`)/ 稳度(`JITTER(agg.edge_ticks) × MUL_ONE ≤ JITTER_MAX × SkillMul(L)`);映射 = 三门全过 Applied / 过幅度+节奏稳度不过 AppliedWeak / 节奏不过 Missed。`drug_potency = RoundFix(BASE_POTENCY × ResultMul) / MUL_ONE`。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(求值本体 LOW;AC-10-04b 三格对拍受 ADR-012 F7 溢出 UB 前置,矩阵未跑前该条 NOT-RUN)
**Engine Notes**: 复用 disease-simulation story 001 的 `Fix`/hi-lo 原语(同程序集域,10 判定住 `Sim`?—— 按 ADR-025:Judge 属 sim 侧整数判定,住 `Sim`;3 的读数采集在边界侧,跨边界只见 `EmergencyReading` 契约类型)。

**Control Manifest Rules (this layer)**:
- Required: 纯函数(同输入同输出,零副作用);`JITTER` 全式整数(单次舍入);`MAG_THRESHOLD_EFFECTIVE` 投影下界 1 结构性
- Forbidden: 熟练度放大 `magnitude`(幻想=作用于稳度);连续分值输出;`Math.Round`;C# `/` 裸用(向零截断)
- Guardrail: 阈值/系数数值全归用户数值轮 —— 合成 fixture 参数化,判「形状+舍入+单调」不判「手感难度」

---

## Acceptance Criteria

*From GDD `design/gdd/emergency-procedures.md`, scoped to this story:*

- [x] **AC-10-04a**[A] BLOCKING:10 自身定点缩放的 EditMode 对拍 —— 故意落 `.5` 的中间积 `(32769×32768) ÷ 65536` = **16385**(half-away),不是 C# 默认 16384(F-10.4 / ADR-006 §三) — `ScaleFixed` 测试验证
- [x] **AC-10-04b**[I]:单元级黄金夹具(F-10.4 缩放 / F-10.3b 整数比值)在 ADR-012 三格逐位一致 —— **NOT-RUN 直至矩阵实跑,禁借绿**(Mono 单跑不构成判据) — 占位标记测试
- [x] **AC-10-15**[A] BLOCKING:`JudgeResult` 是枚举(非连续分值),输出零分数/评价字段(反射断言) — 反射断言测试验证
- [x] **AC-10-20**[A] BLOCKING:`edges ≤ 1` ⇒ 稳度门**不评且不抛异常**(定义域,非兜底;直接落节奏门 `edges ≥ MIN_EDGES`) — `edges=0/1` 测试验证
- [x] **AC-10-10c 前半**[A]:一次 `Missed` ⇒ **发**成长读数路径可达(承 30「做过即成长」;与 story 004/005 的 Skip 不发对偶判别) — Missed 不阻断成长路径测试验证
- [x] **F-10.3 三门矩阵**[L]:档位映射穷举 2³ 输入组合 ⇒ 结果恰按「全过=Applied / 幅+节过=AppliedWeak / 节不过=Missed」;注:节奏门两支为「或」 — 8 组合穷举测试验证
- [x] **F-10.3b JITTER**[L]:相对偏差式(÷`MEAN_d` + `MUL_ONE` 整数比值 + 单次舍入):**整体等比慢/快不触发稳度门**(手速非抖动)—— 夹具:`edge_ticks=[0,10,20]` vs `[0,20,40]`(同比例)⇒ 同判;`[0,10,25]` ⇒ 抖动项 > 前者 — 比例不变性测试验证
- [x] **F-10.2 方向裁定**[L]:`magnitude` 不被熟练度放大(同读数不同 L ⇒ `mag_peak` 门结果相同);`SkillMul(L)` 只出现在稳度门容差侧;`MAG_CAP` 只升上限不升实测(形状断言,档位表可全同) — 方向回归测试验证
- [x] **F-10.4 唯一数值接口**[A]:Judge 输出 → `drug_potency` 三档 `{×1, ×0.5, ×0.25}` 全经 hi/lo + 一次舍入;`BASE_POTENCY` 从 story 002 烘焙表读,零硬编码 — 三档缩放测试验证

---

## Implementation Notes

*Derived from F-10.2/10.3/10.3b/10.4:*

1. `Judge` 签名按规则四:`Judge(agg, action, ctx)`,其中 `ctx` 含 `patient_ctx`(投影 `MAG_THRESHOLD_EFFECTIVE` 的唯一消费点)与 `L = QueryLevel(玩家, EMERGENCY)`(30 只交档,int)。
2. `JITTER` 分母 `MEAN_d` 防零:`edges≥2 ⇒ d_i 存在`;`MEAN_d = 0`(同 tick 双沿)在 20 Hz 下非法输入 ⇒ 3 侧单调断言(story 001)为前提,10 侧仍写防御断言(不抛,判「抖动=最大值档」的口径**若被触碰须重开 OQ-10-2**,登记不代裁)。
3. 求值顺序:节奏门先判(不过 ⇒ Missed 短路),再幅度,再稳度 —— 顺序与 F-10.3 映射表逐字对齐,注释引门名。
4. 合成 fixture 生成器:三门全组合 + 边界(阈值恰等 / `edges=MIN_EDGES−1` / `.5` 中间积)参数化表驱动测试。
5. 黄金夹具入库格式承 ADR-012 §三(golden-vN + 变更日志;本 story 产「单元级」条目 2 项)。

## Out of Scope

- [Story 004]: Aggregate / 上行 / 落流(Judge 的调用者)
- [Story 005]: 跳过/中止/开关路径(在 Judge 之外构结果)
- [Story 006]: 预表现复用 Judge 的本地跑(结果不回写权威)
- 手感难度调参(数值轮)

## QA Test Cases

*Written at story creation(lean mode).*

- **8193 断言**: F-10.4 缩放夹具 `(32769, ResultMul=0.5)` ⇒ 8193;若实现走 C# `/` ⇒ 8192 红(AC-10-04a)。
- **枚举洁净**: 反射 `Judge` 返回类型 + 输出结构 ⇒ 零 `float`/零分数域字段。
- **edges≤1**: `edges=0/1` ⇒ 无异常,结果只由节奏门支决定(AC-10-20)。
- **比例不变**: 等比慢两组同判;不齐组差于两者(F-10.3b 三断言组)。
- **2³ 矩阵**: 门组合表驱动 ⇒ 映射全合(每格一测)。
- **熟练度只稳**: 同 agg、L=0 vs L=max ⇒ 幅度门结果一致、稳度门结论可不同(F-10.2 方向回归)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/EmergencyProcedures/judge_test.cs` — must exist and pass(AC-10-04b 列 NOT-RUN-BLOCKED-BY-ADR-012)
**Status**: [x] Created — 16/16 passed (2026-10-02 双代理评审修复后复跑)

---

## Dependencies

- Depends on: Story 001(agg 形状)、Story 002(表)、disease-simulation epic story 001(Fix/hi-lo 原语)、skill-system epic(`QueryLevel`)
- Unlocks: Story 004(主机调 Judge)、Story 005(偏序 AC-10-10b 引用档位值)、Story 006(预表现调本地 Judge)

## Completion Notes
