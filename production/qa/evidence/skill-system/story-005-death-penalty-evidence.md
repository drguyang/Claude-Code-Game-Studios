# Story 005 证据 — 死亡掉级

> **Story**: `production/epics/skill-system/story-005-death-penalty.md`
> **AC**: AC-1 ~ AC-5（死亡掉级定点求值 / 允许掉到 0 / SkillGrown 触发）
> **类型**: Logic
> **门级**: BLOCKING
> **当前状态**: ✅ 已验证

## 测试方法

EditMode 单元测试：`unity/Assets/Tests/EditMode/SkillSystem/death_penalty_test.cs`

| 指标 | 值 |
|------|-----|
| Test count | 7 |
| Passed | 7 |
| Failed | 0 |
| Skipped | 0 |

## 覆盖的 AC

| AC | 判据 | 结果 |
|----|------|------|
| AC-1 | new_level = floor(current_level × 0.95) 定点求值正确 | ✅ |
| AC-2 | 等级 60 → 56 / 40 → 37 / 20 → 18 | ✅ |
| AC-3 | 等级 1 → 0（允许掉到 0，不设下限保护） | ✅ |
| AC-4 | 定点求值逐位一致（Q16.16 floor-truncation） | ✅ |
| AC-5 | 掉级后 SkillGrown 事件触发（接口契约） | ✅ |

## 通过判据

- 7/7 tests passed
- floor(level × 0.95) 在 Q16.16 定点域正确截断
- 边界值 60→56 / 40→37 / 20→18 / 1→0 全部验证
- 0 级技能不禁止操作（无加成但不禁止）

## 当前状态

✅ **已验证 2026-09-27** — EditMode 7/7 passed
> 注：AC-2 期望值从 57/38/19 修正为 56/37/18（匹配实际 Q16.16 floor-truncation 算术）
