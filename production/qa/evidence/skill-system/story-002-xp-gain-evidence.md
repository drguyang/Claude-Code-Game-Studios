# Story 002 证据 — XP 获取公式

> **Story**: `production/epics/skill-system/story-002-xp-gain-formula.md`
> **AC**: AC-1 ~ AC-7（XP_gain 定点求值 / 新颖度三档 / 难度系数 / 技能上限）
> **类型**: Logic
> **门级**: BLOCKING
> **当前状态**: ✅ 已验证

## 测试方法

EditMode 单元测试：`unity/Assets/Tests/EditMode/SkillSystem/xp_gain_test.cs`

| 指标 | 值 |
|------|-----|
| Test count | 18 |
| Passed | 18 |
| Failed | 0 |
| Skipped | 0 |

## 覆盖的 AC

| AC | 判据 | 结果 |
|----|------|------|
| AC-1 | XP_gain = BASE × K_difficulty × K_novelty Q16.16 定点求值 | ✅ |
| AC-2 | BASE 默认值 19 项全部可配置 | ✅ |
| AC-3 | K_novelty 三档系数：First=3.0 / Stale=0.2 / Normal=1.0 | ✅ |
| AC-4 | K_difficulty 作为外部入参，30 不自行计算 | ✅ |
| AC-5 | 技能到 SKILL_CAP 后经验丢弃 | ✅ |
| AC-6 | 冷却期内 novelty_class = Stale → 系数 0.2 | ✅ |
| AC-7 | 定点求值与手工高精度计算逐位一致 | ✅ |

## 通过判据

- 18/18 tests passed
- XP_gain 公式在 Q16.16 定点域正确求值
- 新颖度三档系数正确应用
- 边界值逐位一致（与手工计算比对）

## 当前状态

✅ **已验证 2026-09-28** — EditMode 18/18 passed
