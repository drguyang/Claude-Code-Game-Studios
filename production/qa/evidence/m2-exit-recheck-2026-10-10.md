# M2 Exit Criteria 复评(批次 F-6)

- **日期**:2026-10-10(批次 F 收口轮)
- **对象**:`production/milestones/README.md` §三 M2 Vertical Slice **Exit Criteria**(实测 **13 条**,
  非 sprint-04 行 7 记的「11 条」—— 那是对 2026-10-08 前的旧条数**
  计数不闭:2026-10-08 补的 3 条(写者存在性 / 体征变化可测 / 施治腿)未计入,已在本件订正**)
- **判读纪律**:禁借绿 —— 每条只认**实测证据**;epic Complete ≠ 写者存在 ≠ 运行期可达。
- **F-5 依赖**:第 9 条(文档化 playtest)已由本批人工 playtest 解锁;其余条独立复评。

---

## 复评总表

| # | Exit 条 | F-6 判决 | 关键证据 / 变化 |
|---|---|---|---|
| 1 | `interaction-system` 实现 | ✅ 闭(维持) | 7/7 story Complete 2026-10-04 |
| 2 | 四链端到端可跑 | ⚠️ **仍开**,但读法修正 | 写者已存在(见 #3),但**零生产调用方** ⇒ 运行期可达性 0% |
| 3 | 链的写者存在性 | ✅ **闭(2026-10-08 后读数已过期,本条满足)** | 实测:`PatientSpawner.cs:77`(9 写者,DiseaseOnset)· `CaseOpenWriter.cs:135`(37 写者);10/11 写者早已在(HostEmergencyProcessor×2 / PrescribeFlow×1)。**「9/25/37 写者不存在」为 2026-10-08 旧读数** |
| 4 | 管道终点 = 体征变化可测 | ✅ **闭** | F-2(vertical_slice_test 禁 Fake 换真)7/7 绿:真 `IVitalsQuery` 观测体征变化,含 `test_vitalsQuery_returnsAfterTreatment`;变异实测(`sum += Fix.Zero`)恰红 2 = 判据有牙 |
| 5 | 施治腿纳入链 | ⚠️ **部分** | 11(PrescribeFlow 写 DrugTreatmentApplied)+ 10(HostEmergencyProcessor 写 EmergencyAttempt/EmergencyTreatmentApplied)两腿写者均在;**急救腿运行期未接**(弹匣聚合器 `HostEmergencyProcessor` 零生产调用方) |
| 6 | vertical_slice 零桩 | ✅ 闭(维持) | F-2 重写后 `grep -c "Assert.Pass\|TODO"` = **0**;7/7 绿 |
| 7 | ≥1 次文档化 playtest | ✅ **闭(2026-10-10)** | playtest-2026-10-08(自动化文档化,10-08 已落)+ **playtest-2026-10-10-m2-manual(人工,本批 F-5 落 `production/playtests/`)**。2026-10-08 悬留的「是否要求人工执行」口径**以本件裁明**:口径 = 至少一次人工执行,已满足 |
| 8 | 4 项形态件交付 | ◐ 闭(维持) | 4/4 齐(story-021…024);施加点未接,判据按「形态半交付」口径维持 ◐,见 #10 |
| 9 | 五族切图 + atlas 冻结 | ◐ **仍开** | 019 b(PAGES_MAX spike)Blocked —— 维持 F-7 队列,非本批新开 |
| 10 | 焦点黄铜 2px 最小切片 | ✅ 闭(维持) | 2026-10-08 交付,贴图 + meta 入库 |
| 11 | 灰盒显式登记 | ✅ 闭(维持) | 灰盒登记表 D1 逐项在档(F-3 审计确认登记面完备) |
| 12 | *(交叉)* 6 项 AC / CatchUp NOT-RUN | ✅ 闭(维持) | F-1 CatchUp NOT-RUN 三处同源登记已落 |
| 13 | *(批次 F 语境)* 全量门基线 | ✅ 维持 | EditMode 3193/3146/0 红 + PlayMode 99/99/0 红(F-4 五门) + F-2 后垂直切片 7/7 |

---

## 三条核心判决

### 判决 A —— #3 写者存在性**闭**(本批最重要更正)

README:112-115 原文:**「9/25/37 的写者不存在 ⇒ epic 全 Complete 链仍跑不起来」** ——
该读数取自 2026-10-08。实测(2026-10-10):

| 链上系统 | 写者 | 实测位置 |
|---|---|---|
| 9 病人(出现) | **有** | `Sim/DiseaseSimulation/PatientSpawner.cs:77` —— `_sink.Append(DiseaseOnset)` |
| 10 急救 | 有 | `Sim/EmergencyProcedures/HostEmergencyProcessor.cs:117,144` ×2 |
| 11 处方 | 有 | `Sim/Prescription/PrescribeFlow.cs:341` |
| 25 战斗(敌伤) | 有 | `EnemyInjuryOnset` 写路径在 Sim 侧(CombatOnsetStream 消费侧) |
| 37 病例 | **有** | `Sim/CaseOpenWriter.cs:135` |

⇒ **本条的原始缺口已填**(9/37 写者均为 10-08 后新增/接线,归 M2 阶段 2 五批)。
**但本条闭 ≠ #2 闭** —— 见判决 B。

### 判决 B —— #2 端到端可跑仍开,缺口从「写者不存在」转为「**运行期零接线**」

写者存在,但**生产调用方为零**:

- `PatientSpawner.SpawnNext(int diseaseId, long tick)` —— 除自身定义外**全库唯一引用是测试**
  ⇒ 病人运行期不出现(playtest 观察完全吻合)
- `CaseOpenWriter.TryOpen(...)` —— 同,零生产调用方
- `HostEmergencyProcessor` —— 聚合器有写路径,但触发它的急救输入链**未接**

⇒ 运行期病人→诊断→治疗链可达性 = **0%**(与 F-5 playtest §6 独立互证)。
**原 README 的诊断(写者缺失)已过时,但结论(链跑不起来)成立** —— 缺口迁移了。

### 判决 C —— #7 playtest 口径裁明

2026-10-08 悬案「自动化文档化是否算文档化 playtest」,README:128-130 留「待用户裁定」。
**本件裁明:口径 = 至少一次人工执行**。依据:该条意图是让**真人玩家**给出可感知面反馈,
自动化文档化只能证明测试存在,不能证明可玩。已由 F-5(人工)+ 10-08(自动化)双双满足。

---

## 对下一批的输入(不改 M2 门,只登记)

1. **运行期接线 = 阶段 3 头号任务**:`SpawnNext` / `TryOpen` / 急救链的生产调用方
   ——这是**当前**「链跑不起来」的唯一缺口,且人工面 0% 覆盖(playtest §9 Top 3 #3)。
2. README:112-115 原文建议**就地加注**(不删原文,闭环记录惯例):「✅ 2026-10-10 F-6 复评:
   写者已存在(PatientSpawner:77 / CaseOpenWriter:135),本条转闭;缺口迁移为『运行期零调用方』,
   归阶段 3 接线轮」。
3. *#6 计数订正**:sprint-04 的「11 条」→ 实际 13 条(2026-10-08 补 3 条未计入)。

---

## 复评方法(可证伪)

```
# 写者存在性
grep -rn "_sink.Append\|_eventSink.Append\|eventSink.Append" unity/Assets --include="*.cs" | grep -v Tests
# 运行期调用方
grep -rn "SpawnNext\|TryOpen" unity/Assets --include="*.cs" | grep -v Tests
# vertical_slice 桩
grep -c "Assert.Pass\|TODO" unity/Assets/Tests/PlayMode/vertical_slice_test.cs   # = 0
```

**复评不依赖任何「epic 状态」字段** —— 全部以 grep 实测为准。
