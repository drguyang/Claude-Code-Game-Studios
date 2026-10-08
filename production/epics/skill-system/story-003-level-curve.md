# Story 003: 升级曲线

> **Epic**: 技能与熟练度
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skill-system.md`(§3.2 成长规则 · §4.2 升级曲线)
**Requirement**: TR-skill-001(`XP_to_next(n) = C × n^P`,指数 ∈ {整数, 整数+1/2};旧例值 1.4 作废)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长的定点化与持久化契约
**ADR Decision Summary**: XP_to_next(n) = C × n^P 的幂运算唯一实现 = FixPow(指数 ∈ {整数, 整数+1/2});半整数幂走 FixPow × FixSqrt;FixSqrt = 整数牛顿迭代(禁 Math.Sqrt/libm/float);允许构建期记忆化定表但不得成为第二真源;P 取值集 ∈ {整数, 1/2}(G-1 限死,P=1.4 作废)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯整数算术)
**Engine Notes**: FixPow/FixSqrt 的实现归 Sim.Contracts 程序集;本 story 只消费它们。构建期定表记忆化为可选优化(测试覆盖 FixPow 输出即可)。

**Control Manifest Rules (this layer)**:
- Required: XP_to_next 全路径 FixPow(指数构建期校验 ∈ {整数, 1/2});FixSqrt 为整数牛顿迭代
- Forbidden: Math.Sqrt / Math.Pow / Math.Exp / Math.Round 出现在 XP_to_next 计算链;任意浮点指数(如 1.4)
- Guardrail: n 的定义域 = 1..59(60 级锁定);构建期定表必须逐值等于 FixPow 输出

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] `XP_to_next(n) = C × n^P` 在 Q16.16 定点域求值,输入 n=int,输出 Fix
- [ ] P 的取值被构建期校验 ∈ {整数, 1/2}(P=1.4 须 throw)
- [ ] FixPow 整数幂路径: 重复 Fix.Mul,结果逐位确定
- [ ] FixPow 半整数幂路径: FixPow(base, k) × FixSqrt(base),FixSqrt = 整数牛顿迭代
- [ ] n=60 时返回 Fix(∞)(或特殊标记),表示已满级
- [ ] C=40, P ∈ {1, 1.5, 2} 时曲线形态正确(前段快/中段稳/后段慢)
- [ ] 可选:构建期定表(int64[60])可由 FixPow 生成,且逐值断言相等
- [ ] 升级触发:累计 XP ≥ XP_to_next(currentLevel) 时 level += 1,累计 XP -= XP_to_next(oldLevel)

---

## Implementation Notes

*Derived from ADR-026 Implementation Guidelines:*

1. **`XpToNext(int level)`** 返回 `Fix` —— 从 level 到 level+1 所需的经验值。level=60 → 返回 `Fix.PositiveInfinity`(或等效标记,禁止再升级)。
2. `C`(曲线系数)和 `P`(指数)读自调参表(Story 001 已注册)。`P` 在构建期由 `FixPow` 内部校验 —— 如果 `P` 不是整数或半整数,`FixPow` 抛 `ArgumentOutOfRangeException`。
3. **整数幂路径**:`FixPow(base, k)` = `base` 自乘 `k` 次(循环 Fix.Mul)。k=0 → `Fix.One`;k=1 → `base`;k<0 → throw(本系统不用)。
4. **半整数幂路径**:`FixPow(base, k + 0.5)` = `FixPow(base, k) × Fix.ISqrt(base)`。`Fix.ISqrt` 在 `ulong` 上做 Newton 迭代求 floor 平方根,再按 Q16.16 缩放。
5. **构建期定表(可选)**:在 `FixPow` 初始化阶段(或独立构建工具),对 n=1..59 预计算 `XpToNext(n)` 并存入 `int64[]`。运行期查表。硬约束:定表每个值必须等于 `FixPow(C, P, n)` 的求值结果 —— 构建期断言。
6. **升级判定**:`TryLevelUp(ref int currentLevel, ref Fix currentXp, Fix xpGained)` —— 尝试用 xpGained 升级,返回 bool(是否升级),可能连升多级。循环:while(currentXp >= XpToNext(currentLevel) && currentLevel < SKILL_CAP)。
7. **与 Story 002 的接缝**:`xpGained` 来自 Story 002 的 `ComputeXpGain`。累计 XP 由 caller 维护(本 story 只提供升级判定逻辑,不持有状态)。

---

## Out of Scope

- [Story 001]: 调参表加载(C、P、BASE 的来源)
- [Story 002]: XP_gain 计算(本 story 的输入)
- [Story 005]: 死亡掉级(调用 XpToNext 重建,但逻辑独立)
- [Story 006]: 新颖度追踪(影响 K_novelty,但不影响曲线本身)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: XP_to_next(n) 定点求值正确
  - Given: C=Fix(40), P=Fix(1), n=1
  - When: XpToNext(1)
  - Then: Fix(40) × 1^1 = Fix(40)
  - Edge cases: n=59, P=2 → Fix(40) × 59^2 = Fix(139640);n=60 → 特殊标记

- **AC-2**: P 取值集构建期校验
  - Given: P = Fix(1.4)(浮点,禁止值)
  - When: FixPow(base, P) 被调用
  - Then: throw ArgumentOutOfRangeException(构建期或运行期,看实现选择)
  - Edge cases: P=Fix(1) → OK;P=Fix(1.5) → OK(半整数);P=Fix(2) → OK

- **AC-3**: 整数幂路径正确
  - Given: base=Fix(3), k=4
  - When: FixPow(base, 4)
  - Then: Fix(81)(3^4 = 81)
  - Edge cases: k=0 → Fix(1);k=1 → base 本身

- **AC-4**: 半整数幂路径正确
  - Given: base=Fix(4), k=0(即 4^0.5 = sqrt(4) = 2)
  - When: FixPow(base, Fix(0.5))
  - Then: Fix(2.0)(raw = 131072)
  - Edge cases: base=Fix(2), k=0 → Fix.ISqrt(2) raw = 185794(floor(sqrt(2) × 2^16))

- **AC-5**: 满级锁定
  - Given: currentLevel = 60
  - When: XpToNext(60) / TryLevelUp(...)
  - Then: XpToNext(60) 返回特殊标记;TryLevelUp 返回 false
  - Edge cases: 59→60 升级后下一次 TryLevelUp 不再升级

- **AC-6**: 升级触发与连升
  - Given: level=1, xp=Fix(0), 获得 xpGained=Fix(120)(C=40,P=1 时 1→2 需 40,2→3 需 80,合计 120)
  - When: TryLevelUp(ref level, ref xp, xpGained)
  - Then: level=3, xp=Fix(0), 返回 true(连升两级)
  - Edge cases: xpGained 刚好等于阈值 → 升级,xp 归零

- **AC-7**: 构建期定表断言(若实现)
  - Given: 构建期生成的 int64[60] 定表
  - When: 构建期断言跑 FixPow 输出 vs 定表值
  - Then: 60 个值全部逐位相等
  - Edge cases: 修改 C 或 P 后定表自动重生成(构建工具职责)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skill-system/level_curve_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/SkillSystem/level_curve_test.cs`)

---

## Dependencies

- Depends on: Story 001(调参表加载 C / P / BASE)
- Unlocks: Story 004(CombatPower 不直接依赖曲线,但同属 30 核心)
