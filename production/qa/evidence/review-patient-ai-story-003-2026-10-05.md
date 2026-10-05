# 评审原件 —— patient-ai(13)story-003:呈现投影与视图 API

> **对象**:`production/epics/patient-ai/story-003-presentation-projection-and-view-apis.md`
> **交付面**:`unity/Assets/Gameplay.Presentation/PatientAI/PresentationProjection.cs` ·
> `.../PatientCueSchedule.cs` · `unity/Assets/Tests/EditMode/PatientAI/presentation_projection_test.cs`
> **日期**:2026-10-05 · **轮次**:单轮双代理(承用户「评审只做一轮」)· **判定**:CHANGES REQUIRED → 修复 → 复跑绿

---

## 〇、评审方式与原件义务

本件是 **BLOCKING 判定的报告原件**(`.claude/docs/coding-standards.md` §Review Evidence Standards)。
承「评审只做一轮」:原判定 → 修复落点 → 验证命令,**三栏可证伪**;修复后**不重评审**。

双代理并行:
- **结构侧**(lead-programmer 视角):C# 架构 / 契约面 / 反空转三件;
- **QA 侧**(qa-lead 视角):测试判别力 / 借绿扫描 / 负夹具共用机器。

两侧结论**收敛**为下表(分歧项已就地裁定,列于 §二)。

---

## 一、原判定(逐条 · 修复前)

| # | 判定 | 严重度 | 证据(修复前) |
|---|---|---|---|
| **G** | `PatientCueSchedule.Decide` 首拍公式 `firstDue = phase` **破坏去同步** —— 相位被当作**绝对 tick**;真实入场 tick 远大于 interval ⇒ `tick >= phase` **恒真** ⇒ 全体病人**同一 tick 齐发** | **BLOCKING** | 实证:id=0/1/2/3/7/11 在 entryTick=5000 时 `due` **全为 True**。TC-5 原夹具把 `tick = phase0`(纪元锚)⇒ 两种语义**恰好不可分辨**,把 bug 藏住 |
| **A/C2** | AC-13-C2 断言**恒真**(借绿)—— 判据走 `GetReferencedAssemblies()`,而 **37 病例系统程序集根本不存在** ⇒ 引用集里永远没有它 | **BLOCKING** | `test_ac13c2_patientAiAssemblyDoesNotReferenceCaseSystem` 零判别力;GDD `:1134` 原文是「**grep 断言**」 |
| **B/C4** | AC-13-C4 只扫**成员名**(`DeclaredOnly`)⇒ 看不见 `new GameObject()` / `Object.Destroy()` / `Object.Instantiate()` —— 而「不生成不删除」的真实违规形态**名字里没有 Spawn** | **BLOCKING** | `ScanTypeMembersForNames` 仅名字面 |
| **E/D3** | AC-13-D3 **姿态半边零承载**:`CueDispatch` 无姿态字段 ⇒ 断言「姿态落最静止档」是**断言不存在之物**(恒真) | MAJOR | `CueDispatch` 修复前仅 4 字段:{EmitOneShot, Kind, Intensity, Breath} |
| **H/D6** | `test_ac13d6_cueInterval_isPiecewiseConstant_perTier` **全恒真** —— 循环 50 次读同一个 `readonly` `For(tier)`(`x == x`),再断言三个字面量 | MAJOR | 其自陈注释已承认「`For` 只吃 tier」⇒ 该命题经 `For` **不可测** |
| **I/C3** | AC-13-C3 负夹具**恒真** —— 影子 `PresentCount => 0` 硬编码 ⇒ `Greater(calls, 0)` 必真 | MAJOR | `ShadowProjectAllPatients.PresentCount => 0` |
| **K/D4** | `test_ac13d4_intensity_scanAlwaysInByteRange` **恒真** —— 返回类型就是 `byte`,`>= 0` / `<= 255` 被类型系统先证 | MINOR | for 循环内两个断言与实现无关 |
| **C/A4** | AC-13-A4 调试视图判据**名字面 + `!h.Contains("Shadow")` 过滤**,既非构建面、也非 IL 面 | MAJOR | `ScanAssemblyTypeNamesForTokens` |
| **F/F3** | AC-13-F3 判据 `Contains("13")` 在 26KB 文档里**近乎恒真** | MAJOR | 原断言 `doc.Contains("patient-ai") \|\| doc.Contains("13")` |
| **F/F1-F2** | F1/F2 夹具**过claim** —— 名为 `..._nonColor` / `..._WithinPerceptR`,实际只断言 13 侧结构前提,未触「可见对应物 / 可闻」 | MAJOR | 达成面在 42/44,13 判据面错位 |
| **L** | `BreathShouldLive(BehaviorState) => true` **死重载 / 误导**(恒真,且暗示行为态影响呼吸层,与 §States 相悖) | NIT | 零生产调用点 |
| **M** | `ShadowCaseSystem` **死代码**(无任何断言引用) | MINOR | 修复前 test 文件末 |
| **MINOR-1** | `PresentationProjection.cs` 注释谎称「本文件内 `VitalsDto` 唯一消费点」 | MINOR | 真实消费点有两处(转发链) |

---

## 二、分歧裁定

| 项 | 结构侧 | QA 侧 | 裁定 |
|---|---|---|---|
| C2 判据形态 | 建议 IL/类型图闭包 | 建议源码面 grep(合 GDD 原文) | **取 QA 侧**:GDD `:1134` 字面「grep 断言」⇒ 源码面是权威判据;且 37 程序集不存在使引用面**结构上恒真** |
| D1 处置 | 补产物 | 移出 story 范围 | **取折中**:产物(`ai_patient.json` + 44 签署)归外部(NR-S3-3),夹具改名不再 claim `ac13d1` 达成,登记 NOT-RUN |

---

## 三、修复落点(逐条)

| # | 修复 | 文件:落点 |
|---|---|---|
| **G** | 增 `entryTick` 形参,`firstDue = entryTick + phase`(相位是**相对入场**偏移);TC-5 重写为 entryTick=5000 真实入场扫描 | `PatientCueSchedule.cs` `Decide`;`presentation_projection_test.cs` `test_tc5_*` |
| **A/C2** | 改**源码面 grep**(新增 `ScanSourceFilesForTokens`);负夹具造**真源文件**走**同一台机器** | test `test_ac13c2_patientAiSourceDoesNotReferenceCaseSystem` + `..._negativeFixture` |
| **B/C4** | 新增 **IL 引用面**扫描器 `ScanTypeForForbiddenCalls`(解析 `call`/`callvirt` token);负夹具影子方法体**真发起调用** | test `test_ac13c4_viewNeverSpawnsOrDespawnsPatients` + `..._negativeFixture`;`ShadowWithSpawn.MakeLabel` |
| **E/D3** | `CueDispatch` 增 `PostureTier` 字段;`Decide` 终局恒 0、未终局 `Bedridden`=0;D2 字段清单增列 | `PatientCueSchedule.cs`;test `test_ac13d3_*` + `test_ac13d2_*` |
| **H/D6** | 重写为**行为面**:经 `Decide` 扫 position 观察首拍**与稳态间隔**恒等 | test `test_ac13d6_piecewiseConstant_observableThroughDecide` |
| **I/C3** | 影子 `PresentCount` 改**真计数**(`OnPresentEntered` 同源);正/影走**同一不变量**「投影次数 ≤ 在册数」 | test `test_ac13c3_absentNotProjected_..._negativeFixture`;`ShadowProjectAllPatients` |
| **K/D4** | 删恒真 byte-range;改测**单调不减**(回绕会打破单调 ⇒ 有判别力) | test `test_ac13d4_intensity_isMonotonicNonDecreasing` |
| **C/A4** | 改 IL 引用面判 13 数据层零调试面;「条件编译剥离」**显式 NOT-RUN**(NR-S3-4) | test `test_ac13a4_dataLayer_hasNoDebugViewMembers` |
| **F/F3** | 判据收窄为「**显式点名 `AC-13-F3`** + 与「非目标/不可及」**同窗口**」 | test `test_ac13f3_nonTargetClause_referencedByAccessibilityDoc` |
| **F/F1-F2** | 夹具**改名**为 `..._structuralHalf_*`,注释明示达成面归 42/44 | test `test_ac13f1_structuralHalf_*` / `test_ac13f2_structuralHalf_*` |
| **L** | 删 `BreathShouldLive(BehaviorState)` 死重载 | `PatientCueSchedule.cs` |
| **M** | 删 `ShadowCaseSystem`(改用 `ShadowWithSpawn` 走 IL 面) | test 文件末 |
| **MINOR-1** | 注释订正(两处消费点:转发 + `MaterialTable.Material`) | `PresentationProjection.cs` |

---

## 四、验证命令与结果(可证伪)

```bash
# ① 修复后单跑(patient-ai)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PatientAI" \
  --output unity/Logs/s003-fixgreen-pa.xml
#   ⇒ total=131 passed=131 failed=0

# ② 全库回归(确认零回归)
unity test unity --mode EditMode --output unity/Logs/s003-fixgreen.xml
#   ⇒ total=2499 passed=2455 failed=0 inconclusive=1 skipped=43
#   (与修复前基线 s003-full2.xml 逐项一致 ⇒ 零回归)

# ③ 变异证明(MUT 逐个注入 ⇒ 必红且点名)
```

| MUT | 注入 | 期望红 | 实证 |
|---|---|---|---|
| **G** | `firstDue` 退回 `phase`(绝对 tick) | TC-5 | ✅ `failed=1`,`test_tc5_*` 点名 |
| **B3** | `PresentPatientsView.Rebuild` 注入 `Object.Instantiate` | C4 | ✅ `test_ac13c4_*` 点名(+6 附带) |
| **H** | `interval + (int)(position*3)`(间隔随 position 变) | D6 | ✅ `test_ac13d6_*` **+** `test_tc5_*` 双红(首拍半边漏、稳态半边抓 —— 见 §五) |
| **C2** | 13 目录源文件注入 `"DaYiJingCheng.Case.CaseSystem"` 字面量 | C2 | ✅ `failed=1`,`test_ac13c2_*` 点名 |

日志:`unity/Logs/s003-mutG.xml` · `s003-mutB3.xml` · `s003-mutH2.xml` · `s003-mutC2.xml`。

---

## 五、修复轮内自暴露的第二层缺陷(记录)

**MUT-H 首轮只被 TC-5 抓到,D6 测试漏网**(2026-10-05 补救):
根因 = 我的 D6 重写**只测首拍**,而 `Phase` 内部用的是 `intervals.For(tier)`
(与调用点无关的纯函数)⇒ **即便调用点把 interval 写成 position 的函数,首拍也不变**。
⇒ 补测**稳态间隔**(第二拍),MUT-H 遂被 D6 抓到。
**这是「变异证明反过来证伪了我的修复」的实例** —— 记此以明「绿 ≠ 有判别力」。

---

## 六、未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)

- **NR-S3-1 / 2 / 3 / 4**(无障碍与外部件):见
  `production/qa/evidence/patient-ai/story-003-accessibility-signoff.md`。
- **AC-13-D1 达成**:`assets/data/ai_patient.json`(ADR-014 烘焙)+ 44 签署记录 —— 外部未就位。
- **AC-13-D3 姿态的呈现**:13 已发 `PostureTier` 档位,但**呈现映射归 42** —— 与 NR-S3-1 同族。
- **(承 story-002)NR-S2-1**:真 `ClinicCells` 数据 —— story-003 交付面未触及,继续滚入。

---

## 七、声明

- 原判定 **CHANGES REQUIRED**(G/A/B 三条 BLOCKING);修复落点见 §三;复跑绿见 §四。
- **修复后不重评审**(承「评审只做一轮」)—— 本件即该对象的终局评审原件。
- §六 各项**不记绿**;story-003 的 `[L]` 项与 D1 达成为**部分闭 + BLOCKED-BY 外部**。
