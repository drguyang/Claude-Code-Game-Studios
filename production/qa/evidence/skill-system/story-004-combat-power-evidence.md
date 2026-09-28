# Story 004 证据 — 战斗力与医术修正

> **Story**: `production/epics/skill-system/story-004-combat-power-and-medical-mod.md`
> **AC**: AC-1 ~ AC-7（CombatPower 公式 / 医术修正 / 武器倍率 / 昏迷非致命）
> **类型**: Integration
> **门级**: BLOCKING
> **当前状态**: ✅ 已验证

## 测试方法

EditMode 集成测试：`unity/Assets/Tests/EditMode/SkillSystem/combat_power_test.cs`

| 指标 | 值 |
|------|-----|
| Test count | 1036 |
| Passed | 1036 |
| Failed | 0 |
| Skipped | 0 |

## 覆盖的 AC

| AC | 判据 | 结果 |
|----|------|------|
| AC-1 | CombatPower = (SkillLevel × WeaponMultiplier) × (1 + 医术修正) Q16.16 | ✅ |
| AC-2 | 医术修正 = FixDiv(MED_COMBAT_MOD × 医术等级, SKILL_CAP)，结果 ∈ [0, 0.20] | ✅ |
| AC-3 | 医术 60 + 徒手 60 → 64.8（上限 +20% 验证） | ✅ |
| AC-4 | P0 仅徒手线吃到医术修正 | ✅ |
| AC-5 | 五条武器线 WeaponMultiplier 可配置 (0.8~1.1) | ✅ |
| AC-6 | CombatPower 以 Fix 传入 25，禁 int 直乘 | ✅ |
| AC-7 | 敌人生命归零 = 昏迷（UNCONSCIOUS_AT = 0），非致命 | ✅ |

## 通过判据

- 1036/1036 tests passed
- CombatPower 公式定点求值正确
- 医术修正边界（0 ~ +20%）正确
- 武器倍率五线配置可查询
- 满级徒手 + 满级医术 = 64.8 验证通过

## 当前状态

✅ **已验证** — EditMode 1036/1036 passed
