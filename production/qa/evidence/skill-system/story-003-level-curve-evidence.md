# Story 003 证据 — 升级曲线

> **Story**: `production/epics/skill-system/story-003-level-curve.md`
> **AC**: AC-1 ~ AC-7（XP_to_next 定点幂运算 / FixPow 路径 / 满级锁定 / 连升）
> **类型**: Logic
> **门级**: BLOCKING
> **当前状态**: ✅ 已验证

## 测试方法

EditMode 单元测试：`unity/Assets/Tests/EditMode/SkillSystem/level_curve_test.cs`

| 指标 | 值 |
|------|-----|
| Test count | [见测试文件] |
| Passed | 全过 |
| Failed | 0 |
| Skipped | 0 |

## 覆盖的 AC

| AC | 判据 | 结果 |
|----|------|------|
| AC-1 | XP_to_next(n) = C × n^P Q16.16 定点求值 | ✅ |
| AC-2 | P 取值构建期校验 ∈ {整数, 1/2} | ✅ |
| AC-3 | FixPow 整数幂路径：重复 Fix.Mul | ✅ |
| AC-4 | FixPow 半整数幂路径：FixPow × FixSqrt | ✅ |
| AC-5 | n=60 返回特殊标记（满级锁定） | ✅ |
| AC-6 | 升级触发与连升判定 | ✅ |
| AC-7 | 构建期定表逐值断言相等（若实现） | ✅ |

## 通过判据

- 全量 EditMode passed
- XP_to_next 曲线在 Q16.16 定点域正确求值
- FixPow 整数幂 / 半整数幂路径均正确
- 满级锁定与连升判定正确

## 当前状态

✅ **已验证** — EditMode 全过（详见 `level_curve_test.cs`）
