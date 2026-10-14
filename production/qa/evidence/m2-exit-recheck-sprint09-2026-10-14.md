# M2 Exit Criteria 复评(sprint-09 S3 · 2026-10-14)

- **日期**:2026-10-14(sprint-09 S2 落定后 · S3)
- **上游**:`production/qa/evidence/m2-exit-recheck-sprint06-g5-2026-10-11.md`(G5 复评)
- **对象**:`production/milestones/README.md` §三 M2 Vertical Slice Exit Criteria 的三条滞后勾
  (`:111` 端到端链 · `:169` 4 项形态件 · `:224` 灰盒登记)
- **判读纪律**:禁借绿 —— 每条只认**实测证据**;「勾选滞后」≠「可无据翻勾」;
  **「epic Complete」≠「运行期接线」**(承 2026-10-03 已知缺口口径,`README:251-255`)。
- **本轮方法**:两条独立子代理全仓核(处方腿链路 / 四项形态件施加点)+ 本席**独立抽验承重行**
  (非仅重读旧文档 —— 旧复评的读数本身可能已陈旧,见判决 A/B 各自的「陈旧订正」)。

---

## 复评总表

| # | Exit 条 | 行 | 前状态 | **本轮判定** | 依据(实测) |
|---|---|---|---|---|---|
| 2 | 四链端到端可跑 | `:111` | `[ ]`(G5 已判转闭,**勾未翻**) | **✅ 翻勾** | 处方腿生产调用方实测存在(`PrescriptionPageHost.cs:137`)⇒ `DrugTreatmentApplied` **运行期可达** |
| 8 | 4 项形态件交付 | `:169` | `[ ]` ◐(G5 判「施加点接大半」) | **◐ 维持不勾** | ① ② ④ 施加点已接;**③ 跳过入口零交互绑定**(`SetVisible` 零生产调用)⇒「可跳过」**功能半未接** |
| 11 | 其余资产显式登记灰盒 | `:224` | `[ ]`(已做未勾) | **✅ 翻勾** | 登记表 `:236-249` 逐类齐,与 `entity-inventory.md` 42 项 VS Critical **逐类对齐** |

> **对 sprint-09 §1.2 预判的订正**:TD 预判三条**均为**「勾选滞后」(§1.2 表)。
> 实测**部分成立** —— `:111` / `:224` 确为陈旧可翻;**`:169` 非陈旧,是*真缺口***
> (③ 归 10 实现轮,**有主**,非欠债)。此订正即本轮 S3 的主要产出。

---

## 判决 A —— `:111` 翻勾(处方腿运行期可达)

**前状态**:G5 复评(`m2-exit-recheck-sprint06-g5-2026-10-11.md` 判决 A)已判「#2 转闭」,
但 `README:111` 行首仍为 `- [ ]` ⇒ **勾选滞后**,非未闭。

**本轮实测**(全仓 grep + 逐跳读,非重读旧件):

| 环节 | 文件:行号 | 证据 |
|---|---|---|
| 驱动类定义 | `Gameplay.Boot/PrescribeRequestDriver.cs:74` | `public sealed class PrescribeRequestDriver` |
| 驱动 → 模拟 | `PrescribeRequestDriver.cs:158` | `PrescribeFlow.Prescribe(in request, tick, _ports, _eventSink, _encoder);` |
| **唯一真正生产调用点** | `Gameplay.Boot/PrescriptionPageHost.cs:137` | `return _prescribe.TryPrescribe(in intent, CurrentTick).Applied;` |
| 装配 | `CompositionRoot.cs:237`(new PrescribePorts)· `:239`(new PrescribeRequestDriver) | 服务袋 `CompositionRootServices.cs:89`(`PrescribeDriver` 属性) |
| 事件写出 | `Sim/Prescription/PrescribeFlow.cs:339-341` | `new SimEvent(..., EventKind.DrugTreatmentApplied, ...); eventSink.Append(e);` |

**UI 触发链(逐跳闭合,生产装配)**:

```
CasebookScreen.SubmitDose(rowOrdinal)              CasebookScreen.cs:482
  → _commandSink?.SubmitDose(...)                  CasebookScreen.cs:482
  → (注入) BootRoot.cs:229-230  _uiDriver.CommandSink = _services.CasebookCommands
  → CasebookCommandRouter.SubmitDose               CasebookCommandRouter.cs:174
  → _prescriptionPage.Prescribe(DrugKey, dose)     CasebookCommandRouter.cs:184
  → PrescriptionPageHost.Prescribe                 PrescriptionPageHost.cs:127
  → PrescribeRequestDriver.TryPrescribe            PrescriptionPageHost.cs:137
  → PrescribeFlow.Prescribe → Append(DrugTreatmentApplied)   PrescribeFlow.cs:339-341
```

**结论**:「`PrescribeFlow.Prescribe` 全库零生产调用方」**为假** —— 该读数属 **2026-10-11 旧快照**,
早于 sprint-08 S2 接线(2026-10-12)。⇒ 处方腿**机制上运行期可达**,`:111` 翻勾。

**⚠️ 诚实登记 —— 两处运行期前置约束(不改变「可达」判定,但限制「可达几次」)**:

1. **生产库存种子只有 1 份** —— 全仓唯一生产 `.Grant(` = `CompositionRoot.cs:258`
   `portionsStore.Grant(0, MinimalDrugCatalog.DrugKey, 1);`;而 `portions = 档值 × 1`
   (`PrescribeFlow.cs:288-294`),库存门 `PrescribeFlow.cs:294`。
   ⇒ **仅「档 1」的第一次开方能过**;档 ≥ 2 或第二次开方被拒(零事件)。
   **这是最小端口实装的已知边界**(20 完整库存面未落地),非接线缺口;
   移除义务已登记于 `CompositionRoot.cs:252-253`(「真 20 库存落地后删」)。
2. **须先 E 查体绑定病人** —— 否则 `PrescriptionPageHost.cs:130-132` fail-loud(设计如此)。

**未确认项(显式登记,不掩盖)**:玩家 `actorId == 种子 id(0)` 的次序不变量
**无显式生产断言** —— 仅靠启动序事实成立(`BootRoot.cs:162` 发号 vs `:130` World 场景加载;
`CompositionRoot.cs:53` 计数器默认 0)。**归第七轮 playtest 人工确证**(脚本 §4 步 ④–⑤)。

---

## 判决 B —— `:169` 维持不勾(③ 是真缺口)

**前状态**:G5 复评判「◐ 施加点接大半,维持不勾」。**本轮逐条重核**(四条形态件的施加点):

| 形态件 | 施加点状态 | 证据 |
|---|---|---|
| ① 脉案线格/空行/明度轴 | **✅ 已接** | `CasebookScreen.ApplyData` 每 tick 调 `SignChannelBinder`;`.ruled` 挂脉案行 |
| ② 墨乾湿两态 | **✅ 已接** | `InkAgeTicks` 挂 `.ink-wet`/`.ink-dry`;`JudgmentWriter`(`JudgmentWriter.cs:48`)经 `PrescriptionPageHost.cs:68/100` 生产装配 |
| ③ 急救零数字 + **可跳过** | **◐ 载体在 · 功能半未接** | 见下 |
| ④ 状态反馈通道 | **✅ 已接** | `RevealedRows`(`CasebookVitalsSource.cs:187`)数据源 + E 键消费链完整闭合 |

**④ 消费链(生产,闭合)**:
```
E 键 → PlayerInputReader.InteractPressed        PlayerInputReader.cs:35
  → InteractionInputDriver.TryExamine           InteractionInputDriver.cs:82-88
  → ExamineDriver.OnExamine → ExaminationLedger 快照   ExamineDriver.cs:93
  → (装配)CasebookVitalsSource                   CompositionRoot.cs:228-229
  → (注入)BootRoot.cs:224-225  _uiDriver.Source = _services.CasebookSource
  → SkeuoRuntimeDriver tick 边沿 → CasebookScreen.ApplyData   SkeuoRuntimeDriver.cs:246/332
  → ApplyData 读 RevealedRows → SignChannelBinder.Bind        CasebookScreen.cs:655-660
```
未查体 ⇒ 按设计返回空集(「未查 = 空行」,禁假数据)—— 这是判据,非缺陷。

**③ 为何维持不勾(本轮新发现,TD 未预见)**:

- 载体**确已生产挂载**:`CompositionRoot.cs:246` `new EmergencyCandidateRuntime()`;
  `BootRoot.cs:236-237` `_services.EmergencyCandidates.MountTo(_uiDriver.GetUiRoot())`。
- **但跳过入口零交互绑定**(本席独立复核 `EmergencyCandidateList.cs`,可证伪):
  - `grep -n "focusable\|RegisterCallback\|clicked" EmergencyCandidateList.cs` ⇒ **零命中**
    (该元件只有 `SkipEntryName` 常量 `:41` · `HasSkipEntry` `:56` · 建元素 `:96-98` · `SetVisible` `:108`)
  - `SetVisible(bool)` 全仓调用点**仅测试**(`emergency_candidate_list_test.cs:158/162`),
    **零生产调用** ⇒ 列表在生产运行期的可见性门无人开。
- **归属明确、有主**:story-023 边界裁定①(`story-023-...md:29/34`)明写
  「**施加点 / 候选列表载体 / `Idle` 态可达时序归 10 实现轮 + 数据绑定轮**」;
  `README:189` 同款口径(「story-023 → 10 急救轮 · sprint-04 行 6『阶段 3 形态件施加轮』」)。
- ⇒ **「可跳过」的*形态*已交付(元件 + 零数字门 + 焦点落点,story-023 10/10 AC 勾),
  *功能*归 10 实现轮**。按 `README:182-183` 口径「交付 ≠ 可 playtest 的真通道」⇒ **整条不勾**。

**本轮订正两处 README 陈旧读数(实测推翻)**:

1. `README:202`「**玩家落笔输入入口未接**(TryRecord 无生产调用点)」= **已过时**。
   实测:sprint-08 S1.1(2026-10-12)已接病名栏 Submit → CommandSink 链 ——
   `CasebookScreen.PointLexicon`(`CasebookScreen.cs:383`)→ `CasebookCommandRouter.SubmitDiseaseName`
   (`:102`)→ `JudgmentWriter.TryRecord/TryRevise`(`:117-121`)。
2. `README:196-200`「③ 急救候选列表 `EmergencyCandidateList` **零生产实例化**(仅测试)」=
   **已过时**。实测:载体生产挂载于 `BootRoot.cs:237`(2026-10-11 S3.1 起)。

⇒ ③ 的**残余**不是「零实例化」,而是**「实例化了但不可交互」** —— 更窄、且有主。

---

## 判决 C —— `:224` 翻勾(灰盒登记逐类齐)

**前状态**:`[ ]`(已做未勾)。**本轮实测**:登记表 `README:236-249` 逐类齐,
口径 = `entity-inventory.md` 的 **42 项 VS Critical**(`:232`)。

**逐类对齐(登记表 vs `entity-inventory.md:249-262` 汇总表)**:

| 类 | inventory VS 数 | 登记表行 | 覆盖 |
|---|---|---|---|
| Characters | 2 | `:241` 角色(主角 · 普通病人 T0–T3) | ✅ 逐名 |
| Buildings/Environment | 1 | `:242` 环境(医馆单房间) | ✅ |
| Props | 6 | `:243` 道具(脉案纸页 · 出诊箱 · 诊脉台 · 听诊器 · 戥子 · 手术灯) | ✅ 逐名 6 |
| Items | 6 | `:244` 物品(柳树皮 · 毛地黄 · 金鸡纳 · 止血草 · 针具 · 绷带) | ✅ 逐名 6 |
| UI Screens | 1 | `:245` 界面(脉案页) | ✅ |
| HUD Elements | 8 | `:246` HUD 8 项(含焦点高亮) | ✅ 数量对 |
| SFX | 10 | `:247` SFX 10 项 | ✅ 数量对 |
| Ambient | 1 | `:248` 环境音(医馆室内) | ✅ |
| VFX Events | 7 | `:249` VFX 7 项(水墨晕染除外) | ✅ 数量对 |
| Enemies | 0 | — | ✅ 正确无行 |
| Music Cues | 0 | — | ✅ 正确无行 |
| **合计** | **42** | 真做 3 行(`:238-240`)+ 灰盒 9 类 | **✅ 无空白** |

⇒ 判据原文「**不留「看起来像忘了做」的空白**」**满足** ⇒ `:224` 翻勾。

> ⚠️ 附注:登记表本身于 2026-10-03 才补(`README:230-231` 记录原为**悬空引用**)。
> 本轮只判「登记表现在是否逐类齐」,不追认历史时点。

---

## 对里程碑状态的输入

1. `README.md §三`:`:111` **翻 `[x]`** · `:224` **翻 `[x]`** · `:169` **维持 `[ ]`**(③ 归 10 轮)。
2. `README:196-205` 的 T3.3 / G5 两轮复评注**不删**(留闭环记录),本轮注**追加**于其后。
3. `README:202` / `:196-200` 两处陈旧读数**就地订正**(见判决 B),原文保留并加「已过时」标注。
4. **M2 Exit 剩余未闭 = 1 条**(`:169`,因 ③ 功能半归 10 实现轮)。
   **这不是 sprint-09 未达目的** —— sprint-09 目的是「按证据翻滞后勾」,三条已按证据各归其位。
   M2 是否带此残余收口 = **用户裁定项**(登记于本件,不在 S3 判据面内)。

## 复评方法(可证伪)

```bash
# 判决 A —— 处方腿生产调用方(推翻「零生产调用方」)
grep -rn "TryPrescribe" unity/Assets/Gameplay.Boot/PrescriptionPageHost.cs        # :137
grep -rn "new PrescribeRequestDriver" unity/Assets/Gameplay.Boot/CompositionRoot.cs  # :239
grep -rn "DrugTreatmentApplied" unity/Assets/Sim/Prescription/PrescribeFlow.cs    # :339-341
grep -rn "\.Grant(" unity/Assets/Gameplay.Boot/CompositionRoot.cs | grep -v "//"  # :258(唯一生产 Grant)

# 判决 B —— ③ 跳过入口零交互(维持不勾的可证伪判据)
grep -n "focusable\|RegisterCallback\|clicked" \
  unity/Assets/Gameplay.UI/Skeuomorphic/EmergencyCandidateList.cs                 # = 0 命中
grep -rn "\.SetVisible(" unity/Assets --include="*.cs" | grep -v Tests            # = 0(仅测试)

# 判决 C —— 灰盒登记逐类齐
sed -n '236,249p' production/milestones/README.md
sed -n '249,262p' design/assets/entity-inventory.md                              # 汇总表 42 项

# 全量门(基线不回退)
unity/Logs/s09_playmode_full.xml    # total=177 passed=177 failed=0
```

---

## 签署

- **复评人**:Claude(sprint-09 S3 · 子代理全仓核 + 本席抽验)
- **判读**:禁借绿纪律下,`:111` / `:224` 有实测证据支撑 ⇒ 翻勾;`:169` 证据不足 ⇒ **不翻勾**。
- **未结**:M2 带 `:169` 残余收口的裁定(用户)· 第七轮 playtest 人工确证(用户)。
