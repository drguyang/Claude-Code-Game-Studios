# 评审原件 — Sprint 04 Phase 3 垂直切片 PlayMode 测试

## 原判定

**NOT VERIFIED** — 双代理一轮评审（lead-programmer 代码面 + qa-lead 测试面）

### 代码面（lead-programmer）

**APPROVED WITH SUGGESTIONS**

Required changes:
1. 误导性测试名（`test_caseOpened_withoutPatient_fails` 实际断言事件成功写入）
2. `new PayloadRef(0, 0, 0)` 应改用 `IPayloadEncoder`

### 测试面（qa-lead）

**NOT VERIFIED — 不建议作为 Milestone 2 出口证据**

核心发现:
1. **测试是重言式** — 自造事件 → Append → 断言原样回来
2. **EventStream 无流分离** — 扁平 List，路由从未被调
3. **三个核心 Kind 无生产写者** — 9/25/37 的 Append 不存在
4. **playtest 过度声称** — 声称 Seq/去重/CAP 正确但本文件零断言

## 修复落点

### 1. 测试文件重写

**文件**: `unity/Assets/Tests/PlayMode/vertical_slice_test.cs`

**修复内容**:
- 删除全部 7 个 `Assert.Pass("TODO: ...")` 假绿桩
- 驱动真生产路径:
  - `PrescribeFlow.Prescribe` (11) — 治疗腿
  - `CaseOpenDecider.Decide` (37) — 诊断腿
  - `IPayloadEncoder` — 真编码，非手搓 `PayloadRef`
- 病人出现腿（InjuryOnset）标记 `Assert.Ignore("NOT-RUN: 25/9 写者未实现")`
- 修正测试名: `test_caseOpened_withoutPatient_fails` → 断言 `CaseOpenDecision.NoCase`

### 2. playtest 文档修正

**文件**: `production/qa/playtests/playtest-2026-10-08-vertical-slice.md`

**修复内容**:
- 删除过度声称（Seq/去重/CAP 正确 → 本文件零断言）
- 如实记录 NOT-RUN 项（病人出现腿）
- 如实记录测试驱动的是真生产路径

## 验证命令

```bash
# 垂直切片测试
unity test unity --mode PlayMode --filter "DaYiJingCheng.Tests.PlayMode.VerticalSliceTest" \
  --output unity/Logs/vertical_slice_v4.xml

# 全量 PlayMode
unity test unity --mode PlayMode --output unity/Logs/playmode_full_phase3_v2.xml

# 全量 EditMode
unity test unity --mode EditMode --output unity/Logs/editmode_full_phase3_v2.xml
```

## 验证结果

| 测试集 | 结果 |
|--------|------|
| VerticalSlice | 7 total / 6 passed / 0 failed / 1 skipped (NOT-RUN) |
| PlayMode 全量 | 98 total / 97 passed / 0 failed / 1 skipped |
| EditMode 全量 | 3001 total / 2954 passed / 0 failed / 1 inconclusive / 46 skipped |

## 未闭登记

- **病人出现腿 NOT-RUN** — 25/9 的 `InjuryOnset` 写者未实现，归各系统 GDD 轮
- **人工 playtest 未做** — 本轮是自动化测试的文档化 playtest，**不满足** M2 退出条件「≥1 次**人工**文档化 playtest」
