# QA 签核报告 —— Sprint 11「判断回路前台化 + 治理收口」

- **对象**:`production/sprints/sprint-11.md`(11 项任务:S11-1…S11-11)
- **签核日期**:2026-10-15
- **签核人**:qa-lead
- **判读纪律**:禁借绿(`In Review` ≠ `Complete`;`NOT-RUN` ≠ 通过)· 全域否定须附全树搜索命令与结果 ·
  需真人执行的项登记为**条件**,既不作罚没,也不写成已通过
- **落点说明**:本件落 `production/qa/evidence/`(与 `.claude/docs/review-workflow.md` 义务 1「产出判定的
  原件须落 evidence/」同址)。仓内**无既有 `qa-signoff-*` 先例**(`production/qa/` 下仅 qa-plan / smoke /
  evidence 三类),故本件按 evidence 目录的既有命名惯例 `qa-signoff-<sprint>-<date>.md` 落盘,不另立目录。

---

## 一、实测基线(本轮 2026-10-15 实跑,直接引用,未重跑)

| 套件 | 退出码 | XML | 对照基线 | 结论 |
|---|---|---|---|---|
| 全量 EditMode | **0** | `total=3366 passed=3320 failed=0 inconclusive=0 skipped=46` | S11-5 后 `3366/3320/0/46` | **逐位一致** |
| 全量 PlayMode | **0** | `total=186 passed=186 failed=0` | M2 基线 `186/186/0` | **逐位一致** |
| 静态面 | — | `error CS` = **0** / **0** | — | 无编译面污染 |

- 判决口径 = **退出码 + XML `failed=` 双信号一致**(sprint-11 特有 DoD 条,承 S11-3 固化的口径;两信号
  **不得再混读**)。
- **M2 链零回退(sprint-11 特有硬门)**:`e2e_full_loop` · `vertical_slice` · 五 wiring 测均在全量内且全绿
  ⇒ **不触发「重开 M2」**。

> ⚠️ **基线订正(S12-0 · 2026-10-15 规划期补 —— 上表原行不改,该值确为本签核时点的实跑值)**
> 上表 `3366/3320/0/46` 为本件签核时点实跑;**但本件不是 sprint-11 的终值记录** ——
> 当日**批 6**(ADVISORY 全清,commit `a5ef7c5` · 13:50)在**本件落盘之后**把 EditMode 推到
> **`3388/3342/0/46`**。下方「正向」段的 `3366/3320/0` 同理,同为签核时点值。
> **失守处 = 本件未记「截至时刻」**:裸数 + 「逐位一致」使本件在批 6 后**静默降级为伪基线**。
> **签核结论(APPROVED WITH CONDITIONS)不受影响** —— 该结论建立在**当刻双平台零红 + 反证齐备**上,
> 批 6 的 +22 测**不改变**「当刻零回归」的事实;此处订正只是**基线口径**,不是改判签核。
> **sprint-12 起**:signoff 件一律记「截至时刻 + 批次 + commit」三元组(S12-0 建闸)。
> 📌 **顺带**:本件是全仓**首例** `qa-signoff-*`,其命名先例(`qa-signoff-<sprint>-<date>.md`)
> 已被 sprint-11 记录在案,后续沿用。
- 补充:smoke 报告 `production/qa/smoke-2026-10-15.md`(由他人落盘)的自动面与本表**逐位一致**;
  其 Verdict = **PASS WITH WARNINGS(仅自动面)**,人工冒烟 Batch 1–3 **全部 `NOT RUN`**。

---

## 二、测试覆盖汇总(S11-1 … S11-11)

| ID | 任务 | Type | 交付载体(实测) | 覆盖判定 |
|---|---|---|---|---|
| **S11-1** | 9 查体 revealed 接线 → 8 前台回路 | Integration | EditMode `Boot/examine_driver_test.cs`(**13 测**;11 既有 + 2 新增 `47e0fc4` 多病人隔离)+ PlayMode `SkeuomorphicUI/e2e_full_loop_test.cs`(**真 `CompositionRoot.Assemble()`,`:428` 断言 `RevealedRows.Count > 0`**)+ GDD `O-13-7` 发出侧登记(`44bb2ee`) | **自动面 COVERED(换轨载体)**;人工连走半 **NOT RUN** |
| **S11-2** | W-1 会签裁决 | 决策轮(非测试项) | `case-system.md:615` W-1 注块(cut ① = 三案各挂 `DIS_DYSENTERY`)+ `:1210` 会签行 + `:657` **QA 会签记录(三签面:user ✅ / game-designer ✅ / qa-lead ✅)** | **N/A(非代码)**;三签齐备 |
| **S11-3** | EditMode 退出码 2 处置 | 逻辑(测试基建) | `2d20e71`(3 文件;8 处 `new MonoBehaviour` ⇒ 真夹具 + 1 处 `Assert.Inconclusive` 占位删除) | **COVERED**(退出码 0 + `failed=0` 双达标) |
| **S11-4** | prescription DoD 残留 | Logic | `fix-s11-4-prescription-dod-residuals-2026-10-15.md` + `td-ruling-ac11-22-narrowing-2026-10-15.md`;`drug_event_test.cs` 新增 2 测 · 反证 ×1 | **COVERED**(带真残余) |
| **S11-5** | 53 medical-consequences 起首 story | Logic | `s11-5-medcons-story-001-aggregation-2026-10-15.md`;`Sim/Consequences/ConsequenceInputAggregator.cs` + `medcons_aggregation_test.cs`(**20 测**)· 反证 ×2 | **COVERED** |
| **S11-6** | 11 人工走查 AC-11-12/13/21 | UI / 走查 | — | **NOT DONE — 用户裁定顺延**(见 §四 C4) |
| **S11-7** | story-002 BLK-8(墨龄取错病人) | Logic | `fix-s11-7-case-system-story-002-blk8-2026-10-15.md`;生产件 `CaseOpenDecider.cs` + 3 新判别夹具 · **回退旧实现 ⇒ 25/2 红 + 5/1 红** | **COVERED** |
| **S11-8** | story-003 B-1(复诊案无法结案) | Logic | `fix-s11-8-…`;`CaseCloseDecider.cs` + 35 测 · 反证 = 旧实现 **7 红** | **COVERED** |
| **S11-9** | story-004 CB-2/TB-5(AC-37-29 零覆盖) | Logic | `fix-s11-9-…`;`PatternDetector.cs` + 30 测 · 反证 ×2(3 红 / 1 红) | **COVERED** |
| **S11-10** | story-003 B5(处置集错收 `InjuryOnset`) | Logic | `fix-s11-10-…`;`TreatmentKinds` + 39 测 · 回退成员表 **4 红** | **COVERED** |
| **S11-11** | story-001 B-3(AC 号一号两义) | 零成本订正 | `fix-s11-11-…`;全文件去号 25 处 · 判据 = 同号前缀方法数 `test_ac3703 7→2` 等 | **COVERED** |

**汇总**:必做 8 项(S11-1/2/3/7/8/9/10/11)**全部完成且均有实测载体**;
should 1 项(S11-4)、nice 1 项(S11-5)完成;nice 1 项(S11-6)**顺延**。
**无 MISSING 测试件**(本 sprint 全部 Logic/Integration 任务均有带反证或判别力的实测载体)。

---

## 三、发现的问题(带严重度)

### F1 · S3(Minor)— S11-1 计划点名的证据载体**不存在**,实际走了另一条轨
QA 计划(`qa-plan-sprint-11-2026-10-14.md:17`)点名
`unity/Assets/Tests/PlayMode/Boot/examine_revealed_wiring_test.cs`。实测该文件与目录均不存在。

**全树搜索命令与结果(全域否定的取证义务)**:

```
$ grep -rln "examine_revealed_wiring\|ExamineRevealedWiring" unity/Assets/Tests/ tests/
(无输出,退出码 1)
$ ls unity/Assets/Tests/PlayMode/Boot/
(无此目录)
```

⇒ **在 `unity/Assets/Tests/` 与 `tests/` 两棵树内未发现**该符号/路径。
S11-1 的 AC 自动面**由换轨载体覆盖**:EditMode `Boot/examine_driver_test.cs`(真 `EventStream` +
真 `PatientSpawner` + 真 `DiseaseVitalsService`,13/13)+ PlayMode `e2e_full_loop_test.cs`
(真 `CompositionRoot.Assemble()`,断言查体后 `RevealedRows.Count > 0`)。
**判定**:AC 的自动面**有覆盖**(不判为缺失);但计划点名载体零命中须登记,以免下游按计划路径检索而误判「无证据」。

### F2 · S3(Minor)— S11-1 无独立证据原件,且台账无完成订正框
- `grep -rln "S11-1\b\|S11-2\b\|S11-3\b" production/qa/evidence/` 仅命中 3 份 **case-system** 文件(引用了同名编号),
  **未发现** S11-1/2/3 自身的过程原件。
- S11-1/S11-2/S11-3 三行在 `sprint-11.md` **均无 ✅ 完成订正框**(S11-4/S11-5 有),其完成痕迹仅在
  commit(`185849d` / `2aa28d1` / `80914f4`)+ `sprint-status.yaml`。
- S11-1 在 sprint 内的实际交付 = 2 条测试(`47e0fc4`)+ 1 条 GDD 登记(`44bb2ee`);**底层 revealed 接线
  早于本 sprint**(`e9af520` sprint-06 G1.4 · sprint-08)—— 与 S11-4/S11-5 的「任务书前提过时」**同型**,
  但**未在 sprint 文件登记**。
**判定**:不影响 AC 判定,但登记面不完整(治理债,非功能缺陷)。

### F3 · S2(条件项,非缺陷)— S11-1 的**人工连走半**未执行
QA 计划 §Manual QA Checklist(S11-1)要求 Playtest 连走(查体 → 脉案显示 → 落笔 → 开方)+ F12 截图 +
走查报告落 `production/playtests/`,并由 **user(主创)+ qa-lead** 签核。实测:`production/playtests/`
下**无** sprint-11 的查体连走走查件(最近一件为 `playtest-2026-10-14-story009-walkthrough.md`)。
**判定**:需真人,登记为**条件**(§四 C2),**不判为开发方失职**,但**不得**写成已通过。

### F4 · S2(条件项)— S11-6 顺延,其**前置 Driver bug 实测仍在**
`unity/Assets/Editor.Tools.Spike/Story009WalkthroughDriver.cs:406` 仍为
`r.passed = r.checks.All(c => c.StartsWith("✅"));` —— QA 计划 §S11-6 点名的「`passed` 汇总判定」修复
**未落地**。连带 AC-11-12 / AC-11-13 / AC-11-21 三签 **NOT-RUN**。
**判定**:条件(§四 C3),归属 S11-6(顺延 sprint-12)。

### F5 · S3(Minor)— S11-6 的顺延**仅登记于 sprint 文件**,`sprint-status.yaml` 无显式口径
`sprint-11.md:27`「必须项优先」有顺延裁定文字;而 `sprint-status.yaml` 中 S11-6 仅呈现为
`status: backlog` / `completed: ""`,**无** carryover/归因字段说明「顺延 sprint-12」。
**判定**:登记面不闭合(治理债)。归属:收口轮。

### F6 · S3(Minor)— S11-3 实际改动文件数**超出计划点名**
计划写「修 **2 文件** 8 处」(host_authority_test + o6_actor_cell_payload_test);
实际 `2d20e71` 动 **3 文件**,含 `Tests/EditMode/Audio/settings_exposure_test.cs`(删 `Assert.Inconclusive`
占位 —— 该占位是「全量 EditMode 恒退出码 2」的另一残源)。
**判定**:仍为**纯测试面、零产品代码**(合计划红线);偏差已在 commit message 说明。登记备查。

### F7 · S3(治理债,已登记)— case-system 001–004 维持 `In Review`,顺延面未清
四件经 2026-10-15 补评(评当下)+ 勘误轮后,净核心判定 ≈13 类;本 sprint 只清「真缺陷 / 免责失效 /
设计冲突 / 零成本」四类(S11-7…S11-11,**全部闭合且各带反证**)。
- 已闭:story-001 B-3(S11-11)· story-002 BLK-8(S11-7)· story-003 B-1 + B5(S11-8/S11-10)·
  story-004 CB-2/TB-5(S11-9)。
- **顺延 sprint-12(≈15 条断言形态)**:001 B-1/B-2/B-4 · 002 五条断言形态 + BLK-9/BLK-10 ·
  003 快照半场 + AC-37-18 恒真化 · 004 其余 6 条(TB-1/TB-2/TB-3/CB-1/CB-3 + TA-6 元规则)。
- **口径**:四件的 `In Review` 是**用户 2026-10-15 裁定**的结果,且顺延面**已逐条带状态 + 归属登记**
  进四件 story 与 `case-system/EPIC.md`(**非遗漏**)。
**判定**:`In Review` **不得**按 `Complete` 计。对 epic 收口的影响:`case-system` EPIC 维持 **4/6 In Review**
(005/006 合规 Complete)—— **epic 本 sprint 不转 Complete**,且该状态**不因本签核改变**。

### F8 · S3(已登记)— prescription epic DoD 第 2 / 第 4 条仍不绿
S11-4 使 three 项登记面闭合(AC-11-22 7a 白名单半边落判据 + DC-2/DC-6 登记回刷)+ TD 裁 AC-11-22
半边甲「收窄」(四件同批订正)。**但**:DoD 第 2 条受 AC-11-15(ADR-012 三格矩阵未激活)·
AC-11-12/13/21(人工走查)· 第 4 条受 emergency-procedures epic 仍 `In Review` 制约。真残余:
DC-6 对当前数据集零求值(`salicylic_acid.dose_range = null`)· BL-7(21a `drug_potency` 域未声明)。
**判定**:epic 维持 5/5 story Complete **但 DoD 未达**;文档已如实登记,不因本签核改变。

### F9 · S4(Trivial,过程面)— 两项任务书前提过时,已沉淀
S11-4 / S11-5 的任务书均写于 2026-10-14,而其假设(「DoD 残留需实现」/「story 立件」)在
2026-10-07 / 2026-09-28 已过时 ⇒ **sprint 计划时未回扫 epic 现状**。已沉淀为
`/sprint-plan` Phase 1 硬前置 + `review-workflow.md` 义务 5(两条均**刻意不做 hook**,理由已登记)。

### 关于 S1 / S2 的开列判定(依据范围声明)

**本 sprint 交付面内未发现开放的 S1 或 S2 缺陷。** 依据范围:
① 本 sprint 的五份修复原件(S11-7…S11-11)逐条复核 —— 所修的 3 条**真实现缺陷**
(`LastJudgmentOf` 死参实害链 / `IsClosed` 粒度退化 / 处置集错收 `InjuryOnset`)**性质均为契约层**
(当时**无生产调用点**:`CaseClosed` / `PatternRecognized` 写者未落,由 `WriterExistenceGate.cs:87-88`
具名豁免),**非当时运行期故障**,且**均已闭合**并各带「改回旧实现即转红」反证;
② 全量 EditMode(含 e2e_full_loop / vertical_slice / 五 wiring)+ PlayMode **双绿、零失败**;
③ `error CS` = 0。
⚠️ **范围限制(禁越权)**:上述「未发现 S1/S2」的取证面 = 本 sprint 的 21 份 10-14/15 原件 +
两份全量测试结果 + 相关 story/EPIC 现状;**不等于**对全仓所有历史模块的 S1/S2 全量普查。
人工冒烟 Batch 1–3 未跑(§四 C1),故「运行期无崩」这一面**在本件中无证据**。

---

## 四、条件与未闭项(逐条:状态 + 归属)

| # | 项 | 状态 | 归属 | 备注 |
|---|---|---|---|---|
| **C1** | 人工冒烟 Batch 1–3(启动不崩 / 新会话 / 输入响应 / 读存档往返 / 帧率预算) | **NOT RUN** | 后续人工轮 | 见 `smoke-2026-10-15.md` §Manual Smoke Checks;本件**不构成**「冒烟通过」 |
| **C2** | S11-1 人工连走(查体 → 脉案显示 → 落笔 → 开方)+ F12 截图 + 走查报告落 `production/playtests/` | **NOT RUN** | S11-6 轮 或 独立走查轮 | 主创 + qa-lead 签核;自动面已由 e2e_full_loop 覆盖 |
| **C3** | AC-11-12 / AC-11-13 / AC-11-21 人工三签(**主创 / 医学从业 / 音频 lead**) | **NOT RUN** | S11-6(**顺延 sprint-12**) | 前置 = `Story009WalkthroughDriver.cs:406` 的 `passed` 汇总修复(**实测未落**) |
| **C4** | S11-6 整项顺延的**显式登记** | ⚠️ **仅登记于 sprint 文件** | 收口轮 | `sprint-status.yaml` 无 carryover 口径(见 F5) |
| **C5** | case-system 001–004 断言形态批(≈15 条) | **In Review / 顺延 sprint-12** | sprint-12 | 四件 `In Review` **禁借绿**;`case-system` EPIC 维持 4/6 In Review |
| **C6** | prescription epic DoD 第 2 / 第 4 条 | **未绿** | 外部件(ADR-012 矩阵 / ADR-009 对面 epic / 人工走查 / 21a 内容轮) | DC-6 零求值 · BL-7 · AC-11-15 · AC-11-16(BLOCKED-BY-45) |
| **C7** | S11-1 计划点名载体零命中 + 无独立原件 + 台账无订正框 | **未登记** | 收口轮 | 建议补一行登记(见 F1/F2);**不改变** S11-1 的 AC 覆盖判定 |
| **C8** | S11-9 的两处边界(防误读) | **已登记** | sprint-12 | 写者仍未落(`NextEmissions` 不触 `IEventSink`,**不得**引为「写者已落」)· CB-3 未触及 |
| **C9** | `Story009WalkthroughDriver` 的 `passed` 汇总 bug | **未修(实测 `:406` 仍在)** | S11-6 | 与 C3 同一前置 |

---

## 五、DoD(15 条)逐条判定

| # | 条 | 判定 | 依据 |
|---|---|---|---|
| 1 | All Must Have tasks completed | ✅ | 8 项 must-have(S11-1/2/3/7…11)全 done;S11-6 为 nice-to-have |
| 2 | All tasks pass acceptance criteria | ⚠️ **条件** | S11-1 自动面覆盖(换轨载体),**人工半 NOT RUN**(C2);S11-6 顺延 |
| 3 | QA plan exists(开工前必落) | ✅ | `qa-plan-sprint-11-2026-10-14.md` 在盘(2026-10-14) |
| 4 | All Logic/Integration stories have passing tests | ✅ | 逐项见 §二;case-system 四件 `In Review` 的残余为**用户裁定顺延**,非缺测试 |
| 5 | Smoke check passed | ⚠️ **条件** | `smoke-2026-10-15.md` 存在,Verdict = PASS WITH WARNINGS(仅自动面);人工 Batch 1–3 **NOT RUN** |
| 6 | QA sign-off report | ✅ | 本件 |
| 7 | No S1 or S2 bugs in delivered features | ✅(范围内) | 见 §三末「依据范围声明」 |
| 8 | Design documents updated for deviations | ⚠️ 基本 | 已落:W-1(`case-system.md`)· `adr-008` §四 · AC-11-22 四件同批 · EPIC 状态口径;**未落**:S11-1 换轨/前提过时登记(F2) |
| 9 | Code reviewed and merged | ✅ | 全部提交在 `main`;21 份原件 + 五份修复件 |
| 10 | M2 链零回退(特有) | ✅ | PlayMode 186/186 逐位等于 M2 基线;e2e_full_loop + 五 wiring 全绿 |
| 11 | NOT-RUN 显式结转(特有) | ⚠️ 部分 | 多数 NOT-RUN 已逐项登记;**S11-1 人工半 / S11-6 顺延**的载体登记不闭合(F2/F5) |
| 12 | 退出码口径结清(特有) | ✅ | 「退出码 + XML `failed=`」双信号一致口径已固化并登记(`sprint-11.md` / smoke 报告) |
| 13 | 补评修复批五项闭合且带判据(特有) | ✅ | S11-7…S11-11 各带反证(见 §二) |
| 14 | 写者延后正式登记(特有) | ✅ | story-003 `:73-87` 与 story-004 `:67-85` §Out of Scope 均含「37 结案写者 / `PatternRecognized` 写者延后」正式登记 + 对应 AC 证据面订正 |
| 15 | 顺延面显式登记(特有) | ✅ | 四件 story 逐条带状态 + 归属;EPIC 维持 4/6 In Review(**禁借绿**) |

**计数**:✅ **10** · ⚠️ 条件 **4**(DoD 2 / 5 / 8 / 11)· ❌ **0**。

---

## 六、Verdict

# **APPROVED WITH CONDITIONS**

**理由**:
- **正向**:8 项 must-have 全部完成;**全部 Logic/Integration 任务均有带判别力的实测载体**(五份修复件
  各带「改回旧实现即转红」反证);**全量双平台双绿**(EditMode `3366/3320/0`、PlayMode `186/186/0`,
  `error CS` = 0);**M2 链零回退**(sprint-11 特有硬门满足);`error CS` = 0;W-1 会签三签齐备。
- **不给 APPROVED 的原因**:4 条 DoD 停于条件态,且**全部是「需真人 / 需外部件」而非开发方未尽**
  —— 人工冒烟 Batch 1–3 `NOT RUN`(C1)· S11-1 人工连走半 `NOT RUN`(C2)· AC-11-12/13/21 人工三签
  `NOT RUN`(C3)· S11-6 顺延的登记面不闭合(C4)。
- **不判 NOT APPROVED 的原因**:无开放 S1/S2(§三末);无 MISSING 测试件;M2 链未破;
  顺延与残余**均已如实登记**,不存在「静默欠账」或「借绿」。
- ⚠️ **本 Verdict 的收窄声明(禁借绿)**:本件**不**声明「游戏能跑起来」(人工面未证据)、
  **不**解除任何 `In Review` 对象、**不**使 `case-system` / `prescription` 的 epic DoD 转绿,
  亦**不**替代 S11-6 的三签。

---

## 七、签核结论落点

- 本件 = sprint-11 的 QA 签核原件(DoD 第 6 条)。
- **sprint-11 状态**:11 项任务 → **10 done + 1 顺延(S11-6)**;按本件口径 = **条件性通过**。
- **未决事项的移交**:C1–C9 逐条归属见 §四;其中 **C3 / C9 归 S11-6(sprint-12)**,
  **C5 归 sprint-12 断言形态批**,**C4 / C7 归收口轮**。
