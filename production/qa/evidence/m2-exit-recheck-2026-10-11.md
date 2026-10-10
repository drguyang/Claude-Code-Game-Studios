# M2 Exit Criteria 复评(sprint-05 T3.3 · 接线后)

- **日期**:2026-10-11(sprint-05 Phase A/B 收口后 · `656b823`)
- **上游**:`production/qa/evidence/m2-exit-recheck-2026-10-10.md`(F-6 复评,写者存在但零生产调用方)
- **对象**:`production/milestones/README.md` §三 M2 Vertical Slice Exit Criteria(接线后逐条复判)
- **判读纪律**:禁借绿 —— 每条只认**实测证据**;机制侧闭环 ≠ 玩家交互可玩;复评不重开已闭裁决。

---

## 复评总表

| # | Exit 条 | F-6(10-10) | **T3.3(10-11)** | 依据(实测) |
|---|---|---|---|---|
| 2 | 四链端到端可跑 | ⚠️ 开(零生产调用方) | **◐ 机制侧闭环 · 处方腿机制半** | 五写者全有运行期生产调用方;唯一悬空 = `PrescribeFlow` 零生产调用方(玩家交互轮未开) |
| 3 | 链的写者存在性 | ✅ 闭 | ✅ **维持闭** | 写者 + 接线全在 |
| 4 | 管道终点体征可测 | ✅ 闭 | ✅ **维持闭** | F-2 7/7 含 returnsAfterTreatment |
| 5 | 施治腿纳入链(10) | ⚠️ 部分 | ✅ **转闭** | 急救链运行期已接(wiring 测 + playtest) |
| 7 | ≥1 次文档化 playtest | ✅ 闭(F-5 人工) | ✅ **维持闭** | + 第三轮 5A |
| 8 | 4 项形态件交付 | ◐(形态半,零施加点) | **◐ 施加点接一半** | ①②④ 运行期已挂真源;③ 载体就绪零生产实例化;②④ 真值源(37/9)未接 |
| 12 | CatchUp NOT-RUN 显式登记 | ✅ 闭(维持) | ✅ 维持 | — |
| 13 | 全量门基线 | ✅ 维持 | ✅ **提升** | EditMode 3147/0/1/46 · PlayMode **132/132** |

---

## 判决 A —— #2 机制侧闭环(◐ 维持,不勾)

sprint-05 Phase A(T1.1–T1.5)五写者全部接入运行期 tick 驱动:

| 写者 | 运行期生产调用方 | 证据 |
|---|---|---|
| `PatientSpawner`(9 · DiseaseOnset) | `PatientAppearedDriver.cs:100`(tick 边沿,BootRoot Update) | `patient_appeared_wiring_test`(DiseaseOnset 在流) |
| `CaseOpenWriter.TryOpen`(37 · CaseOpened) | `CaseOpenedDriver.cs:59` | `case_opened_wiring_test` |
| `HostEmergencyProcessor`(10 · EmergencyAttempt/Applied) | `EmergencyAttemptDriver`(CompositionRoot:133) | `emergency_attempt_wiring_test` |
| `DiseaseVitalsService`(体征链) | `BootRoot.Update → StepVitals` | F-2 vertical_slice 7/7 |

运行期病史流实际形态:`DiseaseOnset → CaseOpened → EmergencyAttempt → EmergencyTreatmentApplied`
(playtest 第三轮世界侧:病人出现 + 胶囊 + 体征链驱动确认)。

**唯一未跑通 = `DrugTreatmentApplied`(11 处方腿)**:`PrescribeFlow.Prescribe` 全库**零生产调用方**
(grep 实测;PlayMode 中的该类事件 = 测试直喂)。根因 = 开方行为没有玩家/系统驱动(交互轮未开),
**机制半边,不是接线缺口**,登记不隐藏、不禁借绿。

## 判决 B —— #5 施治腿转闭

F-6 时「急救腿运行期未接(弹匣聚合器零生产调用方)」。现已接:
`EmergencyAttemptDriver` 对在场且未触发过急救的病人每 400 tick 自动触发一次 ⇒
病史流含 `EmergencyAttempt` + `EmergencyTreatmentApplied`(wiring 测断言 + 第三轮 playtest)。
**10 已纳入 M2 链**;11 处方腿的机制半由判决 A 承接。

## 判决 C —— #8 形态件施加点接一半(◐ 维持,不勾)

sprint-05 Phase B(T2.1–T2.5)后,形态件从「零施加点」变为「三个运行期已挂 + 两个真值源未接」:

**已接(运行期可见,第三轮 playtest 5A 人工确认)**:
- ① 线格 `.ruled` 挂脉案五行 + 问诊栏(`CasebookScreen.AddChannel`,T2.0/T2.5)
- ② 墨龄两态 `.ink-wet`/`.ink-dry` 随 `InkAgeTicks` 挂值区行(规则十三,零计时器)
- ④ 通道分发 `SignChannelBinder` 经 `CasebookScreen.ApplyData` 每 tick **生产调用**
  (此前全库唯一引用 = 测试),值区挂到 `ICasebookDtoSource` 真源读取面

**未接(诚实登记)**:
- ③ 急救候选列表 `EmergencyCandidateList` **零生产实例化**(仅测试;跳过入口形态就绪,
  施加点归 10 交互轮)
- ② 墨龄上游(落笔 tick)归 37 未实现 ⇒ `InkAgeTicks` 恒 0(刚落笔形态)
- ④ 词条**真值源**(9 查体 revealed 集)归 9 未接线 ⇒ `CasebookVitalsSource.RevealedRows`
  恒空集(F2 signs tripwire)⇒ 脉案值区运行期空行 —— **禁假数据的判据面**,
  空行正是「未查 = 空行」(`:329`)的诚实呈现,不冒充已读状态

⇒ #8 维持 ◐ 不勾;剩余内容 = ③ 施加点 + ②④ 真值源(37/9 接线),非本案故障。

---

## 对里程碑状态的输入

1. `README.md §三`：#2/#5/#8 三条已就地加注 T3.3 复判(原文不删,闭环记录)。
2. **尚未勾选**:#2 ◐、#8 ◐ —— 处方腿(M11)与 9/37 真值源仍属「无玩家驱动的机制空位」。
3. **禁借绿声明**:M2 Exit 未全勾 ≠ sprint-05 未达目的 —— 冲刺目的(运行期链 + 四形态件真通道)
   已达成;余项是下一交互轮的机制面。

## 复评方法(可证伪)

```
# 写者生产调用方(接线后)
grep -rn "SpawnNext\|TryOpen" unity/Assets/Gameplay.Boot/*.cs | grep -v "//"   # 驱动接线在
grep -rn "new EmergencyAttemptDriver" unity/Assets/Gameplay.Boot/CompositionRoot.cs   # :133
# 处方腿悬空
grep -rn "\.Prescribe(\|new PrescribeFlow" unity/Assets --include="*.cs" | grep -v Tests | grep -v Editor.Tools   # = 0
# 形态件真值源
grep -rn "RevealedRows" unity/Assets/Gameplay.Boot/CasebookVitalsSource.cs   # Array.Empty(tripwire)
grep -rn "new EmergencyCandidateList" unity/Assets --include="*.cs" | grep -v Tests   # = 0(仅测试)
# 全量门
unity test unity --mode EditMode  # 3147/0/1/46
unity test unity --mode PlayMode  # 132/132
```