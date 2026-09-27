# Story 004: 战斗效能与医术修正

> **Epic**: 技能与熟练度
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skill-system.md`(§3.4 格斗线与医术耦合 · §4.4 战斗效能)
**Requirement**: TR-skill-003(CombatPower 定点求值) · TR-skill-004(医术修正 = 0.20 × 等级/60) · TR-skill-007(30 与 25 格斗线的数据边界)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长的定点化与持久化契约 · ADR-025: 契约程序集清单(30→25 的 Fix 入参越过门面)
**ADR Decision Summary**: CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)全路径 Q16.16 定点求值;医术修正 = FixDiv(MED_COMBAT_MOD × 关联医术等级, SKILL_CAP);CombatPower 以 Fix 传入 25(承 25 的 AC-25-6-02 / A16);医术上限 +20% 硬约束。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯定点算术;与 25 的 Fix 入参交接点 = `Gameplay.Presentation` 程序集的接口层(ADR-025 传送契约拆分)。

**Control Manifest Rules (this layer)**:
- Required: CombatPower 全路径 Fix.Mul + Fix.Div;医术修正 ROUND_HALF_AWAY_FROM_ZERO;CombatPower 以 Fix 传入 25(禁 int 直乘)
- Forbidden: 医术等级替代格斗等级的逻辑;CombatPower 以 int/float 传入 25
- Guardrail: 医术修正硬上限 +20%(MED_COMBAT_MOD = 0.20,可验证);P0 仅徒手线吃到医术修正(急救 P0 / 手术 P1a)

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] `CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)` Q16.16 定点求值
- [ ] 医术修正 = FixDiv(MED_COMBAT_MOD × 关联医术等级, SKILL_CAP),结果 ∈ [0, 0.20]
- [ ] 医术等级 60 + 徒手 60 → CombatPower = 60 × 0.9 × 1.20 = Fix(64.8)(上限 +20% 验证)
- [ ] 短兵 5 + 手术 60 → CombatPower = 5 × 1.0 × 1.0 = Fix(5)(P0 手术未上线,医术修正不生效)
- [ ] P0 仅徒手线(急救)吃到医术修正;短兵/钝器/长兵/暗器在 P0 修正 = 0
- [ ] CombatPower 以 Fix 传入 25,禁 int 直乘(承 25 的 AC-25-6-02)
- [ ] 敌人生命归零 = 昏迷(UNCONSCIOUS_AT = 0),不进入死亡状态
- [ ] 五条武器线的 WeaponMultiplier 可配置(0.8~1.1),来自调参表

---

## Implementation Notes

*Derived from ADR-026 Implementation Guidelines:*

1. **`ComputeCombatPower(int combatSkillLevel, int medicalSkillLevel, WeaponLine line)`** —— 返回 `Fix`。
2. `WeaponMultiplier[line]` 读自调参表(`Fix`);`MED_COMBAT_MOD = Fix(0.20)` 读自调参表。
3. 医术修正计算: `Fix.Div(MED_COMBAT_MOD × medicalSkillLevel, SKILL_CAP)` —— 使用 `ROUND_HALF_AWAY_FROM_ZERO`(ADR-006 唯一舍入)。
4. 医术修正的上限由 `MED_COMBAT_MOD = 0.20` 硬约束 —— 即使 medicalSkillLevel = 60,结果也 ≤ 0.20。
5. **P0 耦合检查**:只有 `WeaponLine.徒手` 的 `关联医术 = 急救`(P0);`WeaponLine.短兵` 的 `关联医术 = 手术`(P1a)。P0 实现时,短兵的医术修正 = 0(手术未上线)。
6. **与 25 的接口**:CombatPower 以 `Fix` 返回 —— 25 的消费方负责接收 `Fix` 类型(承 25 的 A16)。接口归 ADR-025 的「传送契约」拆分(整数命令半进 `Sim.Contracts`,连续半留 `Gameplay.Presentation`)。
7. **昏迷阈值**:`UNCONSCIOUS_AT = 0`(Fix 常量),敌人生命 ≤ 0 → 昏迷状态(非死亡)。本 story **不实现昏迷状态机**,只提供阈值常量。

---

## Out of Scope

- [Story 001]: 调参表加载(WeaponMultiplier / MED_COMBAT_MOD 的来源)
- [Story 003]: XP_to_next 曲线(不直接影响 CombatPower)
- [Story 005]: 死亡掉级(影响格斗技能等级,但不影响 CombatPower 公式本身)
- [Story 025]: 格斗线的 CombatPower 消费(25 负责接收 Fix 并用于伤害计算)

---

## QA Test Cases

**[Integration story — automated test specs]:**

- **AC-1**: CombatPower 定点求值正确(徒手)
  - Given: 徒手=60, 急救=60, WeaponMultiplier=0.9
  - When: ComputeCombatPower(60, 60, 徒手)
  - Then: Fix(60) × Fix(0.9) × (Fix(1) + Fix(0.20)) = Fix(64.8)
  - Edge cases: 徒手=0 → CombatPower=0

- **AC-2**: 医术修正硬上限 +20%
  - Given: 徒手=1, 急救=60, WeaponMultiplier=1.0
  - When: ComputeCombatPower(1, 60, 徒手)
  - Then: 医术修正 = FixDiv(Fix(0.20) × 60, 60) = Fix(0.20);CombatPower = 1 × 1.0 × 1.20 = Fix(1.2)
  - Edge cases: 急救=0 → 医术修正 = 0

- **AC-3**: P0 短兵线医术修正为 0
  - Given: 短兵=40, 手术=60, WeaponMultiplier=1.0
  - When: ComputeCombatPower(40, 60, 短兵)
  - Then: 医术修正 = 0(手术 P1a,P0 不生效);CombatPower = 40 × 1.0 × 1.0 = Fix(40)
  - Edge cases: 手术=0 → 同样为 0

- **AC-4**: CombatPower 以 Fix 传入 25(接口契约)
  - Given: ComputeCombatPower 返回 Fix(64.8)
  - When: 25 接收 CombatPower
  - Then: 25 收到 Fix 类型,非 int/float
  - Edge cases: Fix raw 值正确传递(AD-025 传送契约)

- **AC-5**: 昏迷阈值常量
  - Given: UNCONSCIOUS_AT = Fix(0)
  - When: 敌人生命 = Fix(-5)
  - Then: life ≤ 0 → 昏迷(非死亡)
  - Edge cases: 生命恰好 = 0 → 昏迷

- **AC-6**: 五条武器线 WeaponMultiplier 可配置
  - Given: 调参表已加载
  - When: 读取各线 WeaponMultiplier
  - Then: 徒手=0.9, 短兵=1.0, 钝器=1.0, 长兵=1.1, 暗器=0.8(默认值)
  - Edge cases: 用户修改调参表后重新加载 → 新值生效

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/skill-system/combat_power_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(调参表加载 WeaponMultiplier / MED_COMBAT_MOD) · Story 002(Fix.Mul / Fix.Div 已就位)
- Unlocks: Story 005(死亡掉级后 CombatPower 会变化,但公式独立)

---

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 8/8 passing
**Deviations**: None
**Test Evidence**: Integration: `unity/Assets/Tests/EditMode/SkillSystem/combat_power_test.cs` — 1036 passed, 0 failed
**Code Review**: Skipped (lean mode)
