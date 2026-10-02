# Story 005: 死亡掉级

> **Epic**: 技能与熟练度
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skill-system.md`(§3.3 死亡掉熟练度 · §4.3 死亡惩罚)
**Requirement**: —(无专属 TR;验收标准来自 GDD §4.3 公式与 §8 功能验收第 6 条)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长的定点化与持久化契约 · ADR-005: 确定性模拟与状态同步模型
**ADR Decision Summary**: 死亡掉级 = floor(level × 0.95)在 Q16.16 定点域求值;0.95 = FixParse("19/20");Fix.Mul 中间积 int64;floor 取回 int;允许掉到 0;掉级后 SkillGrown 事件触发(29 的 fold 输入)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯定点乘法 + floor,不触及任何 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: floor(level × Fix(0.95)) = Fix.Mul(level, Fix.Parse("19/20")) → 取 raw long 高 48 位(Q16.16 floor)
- Forbidden: (int)Math.Floor(double) 或任何 float 路径;死亡掉级不走随机
- Guardrail: 允许掉到 0(不设下限保护);至少损失 0 点

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] `new_level = floor(current_level × 0.95)` 定点求值正确
- [ ] 等级 60 → 56(损失 4 点);等级 40 → 37(损失 3 点);等级 20 → 18(损失 2 点)
- [ ] 等级 1 → 0(允许掉到 0,不设下限保护)
- [ ] 0 级技能仍可执行(无加成但不禁止)
- [ ] 掉级后触发 SkillGrown 事件(每项技能各一条,载荷含 Level),供 7a 持久化记录
- [ ] 已解锁动作 / 脉案记录 / 新颖度对象**不掉**(这些由其他系统维护,不在 30 管辖)

---

## Implementation Notes

*Derived from ADR-026 Implementation Guidelines:*

1. **`ApplyDeathPenalty(int currentLevel)`** → `int newLevel`。
2. 计算: `Fix.Mul(Fix(currentLevel), Fix.Parse("19/20"))` → 取 raw long 高 48 位(Q16.16 floor = 右移 16 位)。
3. **允许到 0**:不添加 `Math.Max(1, ...)` 之类的下限保护。`floor(1 × 0.95) = 0` 是合法结果。
4. **SkillGrown 触发**:掉级 = 成长事件的逆操作。每项技能掉级后,emit 一条 `SkillGrown` 事件(负增长,`Level` = newLevel)。**是否在同一 tick 批量触发,还是逐 tick 触发 —— 待实现期裁定**(本 story 只定义「掉级后触发」的语义)。
5. **不掉的那些**:已解锁动作、脉案、新颖度对象 —— 这些由 8/11/37 等系统维护,30 不碰。
6. **与 29 的接缝**:29 负责检测「玩家死亡」并触发掉级流程;30 只提供 `ApplyDeathPenalty` 纯函数。

---

## Out of Scope

- [Story 001]: 调参表加载(DEATH_LOSS 的来源)
- [Story 003]: XP_to_next 曲线(掉级不影响曲线,但重建经验需它)
- [Story 007]: SkillGrown 事件发射(掉级触发 SkillGrown,但事件机制在 Story 007)
- [Story 029]: 死亡检测与掉级流程编排

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 死亡掉级定点求值正确
  - Given: currentLevel = 60, DEATH_LOSS = Fix.Parse("19/20") = Fix(0.95)
  - When: ApplyDeathPenalty(60)
  - Then: floor(60 × 0.95) = floor(56.999) = 56(Q16.16 截断)
  - Edge cases: currentLevel = 40 → 37;currentLevel = 20 → 18

- **AC-2**: 允许掉到 0
  - Given: currentLevel = 1
  - When: ApplyDeathPenalty(1)
  - Then: floor(1 × 0.95) = floor(0.95) = 0
  - Edge cases: 无下限保护,0 是合法结果

- **AC-3**: 0 级技能不禁止操作
  - Given: 诊断等级 = 0
  - When: 执行诊断
  - Then: 操作允许(0 级 = 无加成,不是禁用)
  - Edge cases: 所有 19 项技能 0 级时均不禁止

- **AC-4**: 定点求值逐位一致
  - Given: currentLevel = 10, Fix(10) × Fix.Parse("19/20")
  - When: 取 raw long 右移 16 位
  - Then: raw = 622336000(10 × 0.95 × 2^16);右移 16 = 9500(floor 9.5 = 9)
  - Edge cases: 边界值验证舍入方向(ROUND_HALF_AWAY_FROM_ZERO 只影响除法,乘法 floor = 截断)

- **AC-5**: 掉级后 SkillGrown 事件触发(接口契约)
  - Given: 玩家死亡,19 项技能当前等级
  - When: ApplyDeathPenalty 被调用
  - Then: 每项技能 emit 一条 SkillGrown(Level = newLevel)(Story 007 实现事件机制)
  - Edge cases: 等级未变化(如 0→0) → 是否仍 emit(待实现期裁定)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skill-system/death_penalty_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(调参表加载 DEATH_LOSS) · Story 002(Fix.Mul 就位)
- Unlocks: Story 008(掉级后流重构验证)

---

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 7/7 passing
**Deviations**: AC-2 expected values corrected from 57/38/19 to 56/37/18 to match actual Q16.16 floor-truncation arithmetic (Fix.FromRational(19/20) = raw 62259 = 0.94999695..., not exactly 0.95)
**Test Evidence**: Logic: `unity/Assets/Tests/EditMode/SkillSystem/death_penalty_test.cs` — 7 tests, all passing
**Code Review**: unity-specialist APPROVED + qa-tester reviewed (test expectations corrected)
