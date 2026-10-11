# M2 Exit Criteria 复评(sprint-06 G5 · 2026-10-11)

- **日期**:2026-10-11(sprint-06 G1–G4 全绿后 · 待提交)
- **上游**:`production/qa/evidence/m2-exit-recheck-2026-10-11.md`(T3.3 复评)
- **对象**:`production/milestones/README.md` §三 M2 Vertical Slice Exit Criteria(G1–G4 后逐条复判)
- **判读纪律**:禁借绿 —— 每条只认**实测证据**;机制侧闭环 ≠ 玩家交互可玩;复评不重开已闭裁决。

---

## 复评总表(G5 视角)

| # | Exit 条 | T3.3(10-11) | **G5(10-11)** | 依据(实测) |
|---|---|---|---|---|
| 2 | 四链端到端可跑 | ◐ 机制侧闭环 · 处方腿机制半 | **✅ 转闭** | G3 `PrescribeRequestDriver` 生产装配 + 真 `PrescribePorts`(最小端口实装)⇒ `DrugTreatmentApplied` **运行期可达**;判据 8 测全绿(prescribe_driver_test) |
| 3 | 链的写者存在性 | ✅ 维持闭 | ✅ **维持闭(增强)** | b7 门 2026-10-11 加固:`DrugTreatmentApplied` 写者面断言 + SkillGrown 撤豁免(AssemblySkillGrowthPort 生产写者) |
| 4 | 管道终点体征可测 | ✅ 维持闭 | ✅ **维持闭** | F-2 7/7 含 returnsAfterTreatment(G3 处方 → 体征链仍可求值) |
| 5 | 施治腿纳入链(10) | ✅ 转闭(自动触发) | ✅ **转闭(手动化)** | G4.1 移除自动触发(退役壳零事件)+ G4.2 `EmergencyManualDriver` 手动入口双事件入流;wiring 测**反向改造**(零输入零事件 = 红线守死) |
| 7 | ≥1 次文档化 playtest | ✅ 维持闭 | ✅ **维持闭** | 第三轮 5A;第四轮待用户人工 |
| 8 | 4 项形态件交付 | ◐ 施加点接一半 | **◐ 施加点接大半** | ①②④ 运行期已挂真源(T2 系);③ 候选列表**仍零生产实例化**(仅测试);②④ 真值源(37/9)**仍未接**(G2 已给 InkAgeTicks >0,但落笔驱动/查体输入入口未接玩家) |
| 12 | CatchUp NOT-RUN 显式登记 | ✅ 维持 | ✅ 维持 | — |
| 13 | 全量门基线 | EditMode 3147/0/1/46 · PlayMode 132/132 | **✅ 提升** | **EditMode 3185/0/1/46** · **PlayMode 133/133**(G3 8 测 + G4 5 测 + wiring 1→2 条) |

---

## 判决 A —— #2 转闭(处方腿全判)

T3.3 的唯一悬空 = `PrescribeFlow.Prescribe` 零生产调用方。G3(2026-10-11 用户裁定「最小生产端口实装」)闭环:

- **生产调用方**:`CompositionRoot.cs:170` 装配 `PrescribeRequestDriver`(服务袋暴露 `PrescribeDriver`)
- **真端口**:`MinimalDrugCatalog`(21a 最小表)+ `MinimalPortionsStore`(20 库存面,`HasPortions`/`ConsumePortions` 真行为)+ `AssemblySkillGrowthPort`(30,SkillGrown 生产写者)
- **判据全判**(`prescribe_driver_test` 8 测):选档落笔入病史流(载荷五字段 + F-11.1 量纲)· ≥2 档 dose 区分(1.0 vs 2.0)· 不选档零事件 · 无货拒开 · 未知药零事件 · 越界/负 tick fail-loud · GateHit SkillGrown 入流
- **运行期可达性**:四链现为 `DiseaseOnset → CaseOpened → [查体→落笔→开方→施治] → DrugTreatmentApplied → 体征变化`(玩家驱动半由 G1/G2/G3/G4 驱动实装承接,输入入口归后续交互轮)

## 判决 B —— #5 转闭(急救手动化)

T3.3 时急救链 = 自动触发(`EmergencyAttemptDriver` 每 400 tick)。G4.1(用户裁定「急救手动化」)移除自动触发:
- `EmergencyAttemptDriver` 退役壳(零 Append/零状态,保留 BootRoot 调用面)
- **红线守死**:`emergency_attempt_wiring_test` **反向改造** —— 旧断言「启动即自动两事件」= 假绿;现断言**零输入观察窗 ⇒ 病史流零 EmergencyAttempt**(重新引入自动触发即红)+ 手动入口 ⇒ 双事件(新增第二测)
- **手动入口**:`EmergencyManualDriver`(G4.2)→ `HostEmergencyProcessor.Process` → `EmergencyAttempt` + `EmergencyTreatmentApplied` 双事件入病史流(病人归因 + 流路由 History + Skip 载荷 method=1 可判别 R-6)
- **判据 5 测全绿**(`emergency_manual_driver_test`)

## 判决 C —— #8 施加点接大半(◐ 维持,不勾)

G1–G4 后形态件状态:
- ①②④ 运行期真源**已挂**(T2.1/T2.5 + G1.4 查体分册快照 + G2.2 InkAgeTicks 派生态)
- **新增**:G2 落笔写者(`JudgmentWriter`)→ 墨龄上游(37 写者)已有生产装配,但**玩家落笔输入入口未接**(TryRecord 无生产调用点)
- **仍未接**:③ 急救候选列表 `EmergencyCandidateList` **零生产实例化**(仅测试);查体/落笔/开方/施治的**玩家输入入口**(Interact 键位已存在于 InputSystem_Actions.inputactions,零运行期消费)⇒ 运行期仍是自动驱动(病人出现/开案)或驱动已就绪零入口
- ⇒ #8 维持 ◐ 不勾;剩余内容 = ③ 生产实例化 + 玩家输入入口(归下一交互轮),非本案故障

## 对里程碑状态的输入

1. `README.md §三`:#2 转闭加注 · #5 转闭加注(手动化口径)· #8 维持 ◐(施加点接大半)。
2. **禁借绿声明**:#8 未勾 ≠ sprint-06 未达目的 —— 冲刺目的(G1–G4 玩家驱动主链 + 全量门绿)
   已达成;余项 = 输入入口(键位已有零消费)与 ③ 载体实例化,归下一交互轮。

## 复评方法(可证伪)

```
# 处方腿生产调用方(G3)
grep -rn "new PrescribeRequestDriver" unity/Assets/Gameplay.Boot/CompositionRoot.cs   # :170
grep -rn "Encode(EventKind.DrugTreatmentApplied" unity/Assets/Sim/Prescription/PrescribeFlow.cs   # :340(写者面)
# 急救手动化(G4)
grep -rn "_triggeredPatients" unity/Assets/Gameplay.Boot/EmergencyAttemptDriver.cs   # = 0(已删除)
grep -rn "new EmergencyManualDriver" unity/Assets/Gameplay.Boot/CompositionRoot.cs   # 装配在
# 形态件 ③ 候选列表生产实例化(仍缺)
grep -rn "new EmergencyCandidateList" unity/Assets --include="*.cs" | grep -v Tests   # = 0(仅测试)
# 玩家输入入口(仍未接 —— Interact/Emergency 键位已有零消费)
grep -rn "TryRecord(\|TryPrescribe(\|TryManual(\|OnExamine(" unity/Assets/Gameplay.Boot/BootRoot.cs   # = 0
# 全量门
unity test unity --mode EditMode   # 3185/0/1/46
unity test unity --mode PlayMode   # 133/133
```
