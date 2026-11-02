# Story 014 证据 — 阶滤波器载体暴露参数

> **Story**: `production/epics/audio-system/story-014-tier-filter-carrier-exposed-params.md`
> **AC**: AC-14-01 ~ AC-14-11（阶滤波器参数暴露 / 载体一致性 / 运行时可调） <!-- ac-id-gate: legacy 旧形对照 -->
> **类型**: Integration
> **门级**: BLOCKING
> **当前状态**: ✅ 已验证
>
> 🔁 **2026-11-02(S23-36)复核注**:上行 `AC-14-01 ~ AC-14-11` 是**2 位**写法,与系统 14(辨证)的
> AC 形状相同;而 `story-014-tier-filter-carrier-exposed-params.md` 实际用的是 `AC-44-T1/T2/T3`。
> ⇒ 这行标签**既是旧形、也对不上现件**,按「原文不删」保留并加 `legacy` 标记,**不代为编造新 ID**。
> 同类 2 位 story 写法今日全库仅此 1 处。

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
