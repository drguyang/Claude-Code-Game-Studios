# Story 003: F1 抽取管线:配额、上下文门与强度轴

> **Epic**: 随机事件导演
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/random-events.md`(F1 四步:档配额 → W_i 调制 → 强度定档 → 预算后生成 · Hamilton 最大余额法 · ContextGate 布尔判据 · AC-52-17/20/21/22/23/24/36/37/38/45/48)
**Requirement**: TR-randomevents-003(Hamilton 配额拆分 —— gap,登记不立件) · TR-randomevents-021(强度轴输入闭集,partial) · TR-randomevents-022(出诊/医馆判据 = 布尔非几何 —— gap,no_adr) · TR-randomevents-006(KeyGate,partial 于本 story 的 W_i×0 路) · TR-randomevents-017(五边界 Σ=0 系 —— gap)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟 · ADR-006: 定点域边界 · ADR-009: 三问判据(ContextGate 读模拟态,不读表现态)
**ADR Decision Summary**: F1 四步顺序**不可换**(抽池在先、定档在后):
① **档配额**:`档配额权重[档] = 档占比[档] × ReputationMult[档](P0≡1) × ContextMult` —— ContextMult **在配额处进入**,不在条目 W_i;对 `WINDOW_SIZE` 做 **Hamilton 最大余额法**整数拆分,余额并列按档序(威胁<机会<反应<灾难)打破 —— 实现者不得自选拆分法;Σ配额权重=0 ⇒ 0 条不报错。
② **条目权重**:`W_i = W_base × TODMult(夜:仅威胁档逐条,其他档≡1) × HistoryMult_i(clamp(1+HIST_W[cause_flag], 1, HIST_MULT_MAX);KeyGate 不过 ⇒ ×0)`;ΣW_j=0 ⇒ 跳过该档配额、同窗口内再分(全四档皆零 ⇒ 0 条)。
③ **强度**:`StrengthRaw = Σ(clamp 归一 PlayerState 变量 × W_strength)`,`StrengthTier = clamp(Round(StrengthRaw), MIN_tier, MAX_tier)`(**先舍入后钳制**),`StrengthCap = min(tier, f(最高格斗线, 急救))`;P0 中 EcoTier/Reputation 权重 ≡ 0(坐标不是输入,AC-52-22)。
④ 预算检查(Story 004)后才生成。
ContextGate:「在出诊路径上」≡ `有活跃出诊目标 ∧ 不在医馆地块内`(**布尔判据,非几何/NavMesh**,TR-022);医馆地块:威胁档 ×0、机会/反应保留;战斗冷却窗:全档 ×0 且窗口重置(AC-52-48)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 全整数 + Q16.16 定点(Fix 旋钮经 FixParse);零引擎 API,门 A 内。

**Control Manifest Rules (this layer)**:
- Required: 四步顺序与公式形态冻结;舍入 = `ROUND_HALF_AWAY_FROM_ZERO` 单一模式;钳制用整数 Min/Max;归一变量 ∈ [0,1] 定点
- Forbidden: 用浮点做配额比例;把 ContextMult 挪进 W_i;「先钳后舍」;读表现态(距离 / 屏内外)当输入
- Guardrail: 五边界(Σ配额=0 / ΣW=0 单档 / 全档零 / MIN=MAX / 夜窗)各有显式行为定义与夹具

---

## Acceptance Criteria

*From GDD `design/gdd/random-events.md`, scoped to this story:*

- [ ] Hamilton 拆分实现 + 夹具:含并列余额按档序(威胁<机会<反应<灾难)打破的**规定性用例**;两实现者跑同输入出同配额(拆分法无自由度)
- [ ] ContextMult 只在配额层进入(结构断言:W_i 公式里无 ContextMult 项);ReputationMult P0 ≡ 1 且**无来源接线**(P1a 才接)
- [ ] TODMult 夜窗只乘威胁档条目(其他档恒 ≡1 路径);HIST clamp 下界 1 上界 `HIST_MULT_MAX`;KeyGate 失败 ⇒ W_i = 0(不删条目,权重置零)
- [ ] 强度轴输入闭集(AC-52-21 partial 的落点):PlayerState 归一变量表逐变量 clamp [0,1];**EcoTier / Reputation 权重 ≡ 0**(AC-52-22,配置面钉死而非运行期忽略)
- [ ] 先舍入后钳制(AC-52-38 类用例:raw 出界时 tier = 钳值;raw 半分位时舍入方向 = away from zero)
- [ ] `StrengthCap = min(tier, f(最高格斗线, 急救))` 的 f 形状(档位表映射)实现,表值归数值轮
- [ ] 五边界夹具(AC-52-17/36/37):Σ配额权重=0 ⇒ 0 条;单档 ΣW=0 ⇒ 跳过并同窗再分;四档全零 ⇒ 0 条;不产生「补抽至满」副作用
- [ ] ContextGate 布尔判据接线(AC-52-45 侧):出诊目标 / 医馆地块读**世界流模拟态**,零 `Vector3` / 距离运算;战斗冷却 ×0 + 窗口重置(AC-52-48)

---

## Implementation Notes

*Derived from F1 / DC-2:*

1. 四步各一个纯函数,管道串联,每步可单测:`Quota(win) → WVector → Tier → BudgetQuery`;中间量全落栈上只读结构(禁缓存进对象字段 —— 派生态重建纪律)。
2. Hamilton 定点实现:配额权重(Σ=1 的 Fix 组)× WINDOW_SIZE → 整数floor + 余额排序;并列按档序索引 —— 排序在整数余数域比较(hi/lo 原语,Story 002 已就位)。
3. 「同窗再分」= 把被跳过档的配额按剩档重跑一次 Hamilton(记录迭代界 = 档数,防死循环;实现细节进代码注释钉死)。
4. 夜窗 = tick 换算(系统 5 的 TOD 投影,只读);出诊目标 = 1 出诊系统的世界流态;医馆地块 = 6 烘焙逻辑层的格归属(整数查表)。
5. 冷却「窗口重置」语义:进入冷却 ⇒ 当前未抽完窗作废、从下一窗起算(夹具逐 tick 钉)。
6. ⚠️ **数值冻结**:档占比 / TODMult 值 / HIST_W / W_strength / MIN-MAX_tier / f 表 —— 全部值归用户数值轮;本 story 交公式形状 + 边界夹具(0/±1 边界值,非平衡值)。

---

## Out of Scope

- [Story 002]: 掷骰原口与 EventRolled(本 story 调其口)
- [Story 004]: 预算判定与超限弃置(F1 step④ 的「检查」调用方)
- [Story 005]: 预告窗内事件(本 story 到「选中条目 + 定档」为止)
- ReputationMult 的真实来源(P1a)· F3 损伤(P1a)
- 三案链脚本条目(不入 F1,Story 001 注入通道)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: Hamilton 规定性
  - Given: 档占比夹具 {威胁:3/8, 机会:3/8, 反应:1/8, 灾难:1/8},WINDOW_SIZE=10;并列余额夹具
  - When: 拆分
  - Then: 配额唯一确定(并列按档序给);两次运行逐位同
  - Edge cases: WINDOW_SIZE=0(构建期已拒,运行层防御性断言);占比和≠1 ⇒ 归一在烘焙期,运行期不纠

- **AC-2**: W_i 调制与 KeyGate
  - Given: 夜窗 + cause_flag 命中/不命中/旗标缺失三态
  - Then: 威胁档吃 TODMult;HistoryMult clamp ∈ [1, MAX];KeyGate 败 ⇒ W_i=0,ΣW 含其零贡献
  - Edge cases: 非威胁档夜窗 ≡1(不乘);flag 值超 HIST_W 表界 ⇒ clamp 非 throw

- **AC-3**: 先舍后钳(AC-52-38)
  - Given: StrengthRaw 夹具 = {MIN−ε, 半分位, MAX+ε}
  - Then: tier 分别 = MIN / away-from-zero 舍入值 / MAX;顺序反例(先钳后舍)在夹具上产生不同值 ⇒ 被断言拒
  - Edge cases: 全变量 0 ⇒ raw=0 ⇒ tier=MIN;格斗/急救最高档进 StrengthCap

- **AC-4**: 五边界(AC-52-17/36/37)
  - Given: Σ配额=0 / 单档ΣW=0 / 四档ΣW=0 / MIN=MAX / 夜+医馆叠加 五夹具
  - Then: 分别 = 0条 / 再分成功 / 0条 / 恒该档 / 威胁×0 且机会保留
  - Edge cases: 再分迭代后仍全零 ⇒ 0 条退出,不死循环(迭代界断言)

- **AC-5**: ContextGate 纯布尔(AC-52-45/48)
  - Given: 出诊中/在医馆/战斗冷却中三态组合表
  - When: 配额层
  - Then: 与 GDD 判据表逐格一致;程序集反射面无 `Vector3`/距离符号
  - Edge cases: 冷却结束恰跨窗边界(窗口重置语义夹具)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/RandomEvents/event_f1_pipeline_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(掷骰口 / hi-lo 原语)· Story 001(池与权重 schema)
- Unlocks: Story 004(生成前预算检查)· Story 005(选中条目进预告)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `F1PipelineInput` — F1 管线输入
- `F1PipelineOutput` — F1 管线输出
- `EventF1Pipeline` — F1 抽取管线（HamiltonSplit / EvaluateContextGate / ComputeContextMult / ComputeStrengthTier / Execute）
- Hamilton 最大余额法（带 ContextMult 和 ReputationMult）
- ContextGate 布尔判据（医馆只压制威胁档）
- 先舍入后钳制
- 五边界夹具
- 测试: 12 条单元测试（全部通过）

**Deviations**: 
- F1 step ② 和 step ③ 为简化版（无 TODMult / HistoryMult / KeyGate / StrengthCap）

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/RandomEvents/event_f1_pipeline_test.cs` — 12 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，4 BLOCKING 问题全部修复：
- B1: ContextGate 语义修复（医馆只压制威胁档）
- B2: HamiltonSplit 接受权重参数（ContextMult / ReputationMult）
- B3: 五边界夹具补全
- B4: F1 step ② 和 step ③ 简化版实现

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
