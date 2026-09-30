# Story 003: 命中判定 F-25-1(整数格距离 · 白名单 · Down 查询)

> **Epic**: 格斗与武器线
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30
## Context

**GDD**: `design/gdd/combat-and-weapon-lines.md`(F-25-1 五合取项 · 规则〇 先判后打 · 落空仍耗冷却的判据半边)
**Requirement**: TR-combat-002(命中判定全在 sim 整数域:tick 求值 + 整数格距离 + 无表现态输入,可逐位重放)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主): 单一整数格;ADR-016: 感知输入 = 粗粒度整数格
**ADR Decision Summary**: 距离判据 `d2 ≤ RANGE_CELLS[line]²`(int64,格单位,`d∞` 形状由 cell_size 定,OQ-25-5 只裁比例);禁读表现态连续位置(第二 QoS 到达时序不确定 ⇒ 决策非确定性函数,ADR-016 §三);解锁判定走 30 的 `QueryLevel` 整数等级比较 vs `unlock_level`(G-3 纪律);`Down(·)` 查询归 9(若 9 侧缺件 → 2026-09-21 已升格为 Sim 装配构建失败断言,不允许静默降级)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数比较 + 只读查询,零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: F-25-1 合取项全备 —— `ready(占用门)` ∧ `unlocked(30.QueryLevel ≥ unlock_level)` ∧ `TargetWhitelist(禁友伤,含自身非战斗态)` ∧ `¬Down(a)` ∧ `¬Down(g)` ∧ `d2 ≤ range²`
- Forbidden: Vector3/float 参与距离判定;PhysX overlap;读表现态位置构造目标集;把「白名单」写成攻击者侧硬编码敌人名单(目标种类来自世界流/烘焙定义)
- Guardrail: 落空(miss)照常进入占用/冷却消费 —— 判据在 Story 005 落地,本 story 只保证「判定失败= 无 onset、不豁免冷却」的接口形状

---

## Acceptance Criteria

*From GDD `design/gdd/combat-and-weapon-lines.md`, scoped to this story:*

- [ ] AC-25-1-01:d2 恰 = RANGE²(界内)命中;d2 = RANGE²+1 落空(整数边界夹具)
- [ ] AC-25-1-02:友伤白名单 —— 目标是友方单位/病人时不产生 onset;自身亦受白名单约束(非战斗态自击不结算)
- [ ] AC-25-1-03:解锁未达(unlock_level > QueryLevel(格斗)) ⇒ 该动作不可执行(判据 = 可执行性拒绝,非冷却推迟,与 AC-25-3-06/07 同口径)
- [ ] AC-25-1-04:`Down(a)` 或 `Down(g)` 为真 ⇒ 不出手/不收手(9 的 Down 查询接口消费面)
- [ ] AC-25-1-08(次序半边):判定输入 = 本 tick 已吸收 `ActorCellEntered` 后的格快照(与 Story 002 合流复验)
- [ ] 类型面断言:F-25-1 判定函数的全部输入类型 ∈ 整数域(WorldPos/long/int),签名零浮点

---

## Implementation Notes

*Derived from ADR-015/016 Implementation Guidelines:*

1. `d2(ax,ay,az,bx,by,bz)` 用整数格坐标(承 ADR-015 `WorldPos` i32³);RANGE_CELLS 逐武器线读 cooked 表(兽类走 `range_override`,A23 必填)。
2. 目标候选集构造与 4 的纯选择器边界一致:目标种类/格来自事件流与烘焙定义(承 TR-interaction 口径),25 不发明第二候选源。
3. 9 的 `Down` 查询未落地前,25 侧以接口存在性构建断言守(缺件 = 构建失败,禁「当没倒下」的静默降级)—— 联测夹具以桩注入 Down=true/false 双分支。
4. 白名单枚举 = 烘焙的目标种类闭集(敌/兽可打;病人/村民不可),新增种类必须过表门。
5. RANGE_CELLS / range_override 具体值归用户数值轮;夹具用符号化小格数。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 占用门与 tick 内次序(ready 项的判定在 002,本 story 消费其结果)
- [Story 004]: magnitude(命中成立后才计算)
- [Story 005]: 冷却消费与切换
- [Story 006]: onset 写入
- 系统 9:Down/HurtLevel 真相;系统 30:QueryLevel 本体

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(整数边界)**: RANGE² 界内界外
  - Given: 格差 (R,0,0) 与 (R,1,0)
  - When: F-25-1 距离项
  - Then: 前者 d2=R² 过;后者 d2=R²+1 拒
  - Edge cases: 对角 (1,1) d2=2 > 1²(1 格近战打不了对角,与 story-002 占用无关)
- **AC-2(友伤)**: 白名单拦截
  - Given: 目标为病人(病史流在册)
  - When: 玩家对其实攻击意图
  - Then: 无 onset;占用/冷却照常消费(断言「打空也耗」)
  - Edge cases: 目标为倒下(Down)的敌人 ⇒ ¬Down(g) 项拦,不结算补刀
- **AC-3(解锁)**: 可执行性拒绝
  - Given: 动作 unlock_level=10,QueryLevel(短兵)=9
  - When: 发意图
  - Then: 拒绝执行且**不推冷却**(区别于 cooldown 未毕)
  - Edge cases: 等级=10(恰解锁)可执行

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Combat/combat_hit_resolution_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(表)、Story 002(求值点);9 的 Down 查询契约(⚠️ BLOCKED-BY-9 接口面 —— 未落地则以桩 + 构建断言双轨,不记绿)
- Unlocks: Story 004(命中成立 → magnitude)、Story 006(onset)

---

## Completion Notes

*(留空 — story 关闭时回填)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `HitDeterminationInput` — 命中判定输入
- `HitDeterminationResult` — 命中判定结果
- `CombatHitResolution` — 命中判定 F-25-1（ComputeD2 / Determine / ValidateIntegerDomain）
- 五合取项：解锁 ∧ 距离 ∧ 友伤白名单 ∧ ¬Down(a) ∧ ¬Down(g)
- 整数边界判定
- 测试: 8 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/Combat/combat_hit_resolution_test.cs` — 8 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
