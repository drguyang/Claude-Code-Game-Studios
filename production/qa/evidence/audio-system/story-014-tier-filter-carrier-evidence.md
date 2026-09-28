# Story 014 证据 — 阶滤波器载体暴露参数

> **Story**: `production/epics/audio-system/story-014-tier-filter-carrier-exposed-params.md`
> **AC**: AC-14-01 ~ AC-14-11（阶滤波器参数暴露 / 载体一致性 / 运行时可调）
> **类型**: Integration
> **门级**: BLOCKING
> **当前状态**: ✅ 已验证

## 测试方法

EditMode 集成测试：`unity/Assets/Tests/EditMode/Audio/tier_filter_carrier_test.cs`

| 指标 | 值 |
|------|-----|
| Test count | 11 |
| Passed | 11 |
| Failed | 0 |
| Skipped | 0 |

## 通过判据

- 全量 EditMode **872/872 Passed · 0 红 · 0 跳过**
- 日志：`unity/Logs/s014-r2-full.xml`

## 覆盖范围

| 检查项 | 结果 |
|--------|------|
| 阶滤波器参数暴露路径正确 | ✅ |
| 载体（carrier）与滤波器参数一致性 | ✅ |
| 运行时参数可调 | ✅ |
| 负向：非法参数值触发守卫 | ✅ |

## 当前状态

✅ **已验证** — EditMode 11/11 passed（`unity/Logs/s014-r2-full.xml`）
