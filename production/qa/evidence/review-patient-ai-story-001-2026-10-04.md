# 评审原件 —— patient-ai (13) story-001(双代理 · 单轮)

> **BLOCKING 级原件**(承 `.claude/docs/coding-standards.md` §评审报告原件落盘 + `.claude/docs/review-workflow.md`)。
> **无原件 ⇒ 该对象不得转 Complete。** 本件即原件。
> 对象 = `unity/Assets/Gameplay.Presentation/PatientAI/` 7 生产件 + `unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs`。
> 口径 = **评审只做一轮**(2026-10-04 用户令)。

**日期**:2026-10-04
**评审基线**:patient-ai 测试 **38 条**(含后补 `test_ac13a5_trendIsInertInMap_branching` + NaN/±Infinity 边界)
**双代理**:
- 结构侧 = `unity-specialist`(`aa64bdaa32ad46640`)— 判定 **CHANGES REQUIRED**
- QA 侧 = `qa-lead`(`a21992cb2ace8b063`)— 判定 **REJECT**

> ⚠️ **两位评审均非主会话意见** —— 曾停在轮次上限,按「子代理满轮可以接着再送」显式重送后收回报告。

---

## 一、原判定(两件独立)

### 结构侧 —— **CHANGES REQUIRED**(无 BLOCKING)

| # | 严重度 | 位置 | 问题 |
|---|---|---|---|
| F-1 | MAJOR(架构债) | `PatientCue.cs` / `PatientCueEmit.cs` | `CueKind`/`CueIntervals` 归 44 前缀 / `Emit` 以 int 承载类型 —— 见 §二(已被本轮前置修复部分消解) |
| F-2 | MAJOR(正确性·静默) | `PatientBehavior.cs:99` | `BehaviorBands.Validate` **只有测试调用者**,生产构造路径无调用 ⇒ 破约束表静默生效 |
| F-3 | MAJOR(AC 未字面满足) | `behavior_map_test.cs:151` | `test_ac13b5_sessionWriterIsUnique_reflection` 名为「写入者唯一」,实只断言 setter 私有 |
| F-4 | MINOR | `PatientBehavior.cs:140` | `Material` 未实现为「signs 只从此口出」的正向结构 |
| F-5 | MINOR | `PatientBehavior.cs:57` | 终态置位后仍更新 `Tier`,与 §States 一-ter 字面「不再重新求值」有张力 |
| F-6 | MINOR | `behavior_map_test.cs:580` | 复制机不扫 ctor 参数(生产机扫),对 A1/A3 不构成漏报 |
| F-7 | MINOR(文档) | `PresentPatientView.cs:25` | `Present` 注释与实现「否则 Present」不一致 |

### QA 侧 —— **REJECT**(2 BLOCKING)

| # | 严重度 | 位置 | 问题 |
|---|---|---|---|
| B1 | **BLOCKING** | `behavior_map_test.cs:344` | `test_ac13a1_zeroEventSinkInTypeGraph` 扫描根 = ns `DaYiJingCheng.Gameplay.PatientAI`,但唯一 44 桥 `PatientCueEmit.cs` 住 `...Presentation.Audio` ⇒ **不在扫描面内**,阴性**恒真** |
| B2 | **BLOCKING** | `behavior_map_test.cs:361` | `test_ac13a2_vitalsOnlyViaDirector` 只证「构造收了 `IVitalsQuery`」,不证「取数来源唯一」 |
| M1 | MAJOR | `behavior_map_test.cs:155` | 同结构 F-3:`var setters` 算出后**从未断言**(死代码) |
| M2 | MAJOR | `behavior_map_test.cs:384` | `test_ac13a3` 扫描根同 B1;且自称「与 PresentationDtoGuard 同款」为不实(归一化口径不同) |
| m1 | MINOR | `PatientCue`/`Map` | trend 惰性断言以 trend=0 为基,不证带符号方向性 |
| m2 | MINOR | `PatientBehavior.cs:135` | `ResetForLoad` 无负夹具 |
| m3 | MINOR | `BehaviorBands.cs:59` | `Validate` 缺 `SeekMin < DeathBandMin` 硬约束(GDD §Tuning 一 明写) |

QA 另列 **NOT-RUN 10 条**(AC-13-A1 运行期半边 · AC-13-A2 唯一取数 · AC-13-A3 全类型半边 · TC-5 · 边缘「band 空表」· TC-1 单调 · TC-2 振荡 · TC-4 Director 面 · B5① 负夹具 · 数值轮护栏)。

---

## 二、主会话独立复核(两处编号问题**已复现**)

| 主张 | 复现方式 | 实测 |
|---|---|---|
| B1/M2 扫描根不含 44 桥 | 读 `ScanClosureForNames:572-575`,seed = `Namespace.StartsWith("...PatientAI")` | ✅ **成立** —— `PatientCueEmit.cs:18` 声明 `...Presentation.Audio`,不在面内 |
| B2 只证「持了接口」 | 读 `:361-372`,仅比对 ctor 参数名集 | ✅ **成立** —— 无来源闭包断言 |
| F-2 `Validate` 无生产调用者 | `grep Validate` 生产侧 = 定义处 + 文档引用,**零调用**;`PatientBehaviorDirector` ctor `:99-103` 只存 `_bands` | ✅ **成立** |
| F-3/M1 死代码 + 判据降级 | 读 `:151-164`,`setters` 变量未进任何 `Assert` | ✅ **成立** |
| m3 `Validate` 缺 `SeekMin < DeathBandMin` | 读 `:59-93` —— 链校验只有 `MildMin<SeekMin<CollapseMin<DeathBandMin<1`,**无** `SeekMin<DeathBandMin` 独立项 | ⚠️ **部分成立** —— 该条被 `SeekMin<CollapseMin<DeathBandMin` 传递蕴含,但 GDD §Tuning 一 明写为独立约束 ⇒ 直报表缺项 |

> ⚠️ **F-1 的现状**:结构侧评的是**评审前**的形态(`CueKind`/`CueIntervals` 曾在 44 前缀)。
> 主会话在评审期间**已前置修复**:`CueKind`/`CueIntervals` 归 13 命名空间 + `Emit` 缩纯基元 +
> 桥单独成文件。**故 F-1 的根因(13 语义错放 44)已不成立**,残留仅「`Emit` 签名用 int 丢掉类型信息」
> 一条(结构侧自评「时间紧可只订正注释并登记该债」)。**登记为 story 003 义务,本轮不改。**

---

## 三、修复落点(本轮执行)

| 编号 | 处置 | 落点 |
|---|---|---|
| **B1 / M2** | **必修** —— 扫描根扩到「13 的**全部**程序集内类型」（含 44 桥命名空间），并配「正测与夹具共用同一根枚举」 | `ScanClosureForNames` seed |
| **B2** | **必修** —— 加「`VitalsDto` 全部来源闭包 ⊆ {`IVitalsQuery.GetVitals`}」反射断言 | 新增断言 |
| **F-3 / M1** | **必修** —— 重写为「扫描 `PatientBehavior` 闭包内所有写 `Session` 的点，断言 ⊆ {`ApplyExamSession`,`ResetForLoad`}」+「新增第三写点 ⇒ 红」负夹具 | 重写 `test_ac13b5_...` + 负夹具 |
| **F-2** | **必修** —— `PatientBehaviorDirector` ctor 调用 `Validate` 并在非空时 `throw` | 生产 + 断言 |
| **m3** | **必修** —— `Validate` 补 `SeekMin < DeathBandMin` 独立项 | `BehaviorBands.cs` |
| **m2** | **修** —— `ResetForLoad` 补负夹具 | 测试 |
| **m1** | **修** —— trend 惰性断言补带符号邻域球扫 | 测试 |
| **B1 运行期半边 / NOT-RUN 各项** | **显式登记**(禁借绿)，归 story 003 / 后续 | 见 §四 |
| F-4 / F-5 / F-6 / F-7 | 留档,本轮不改(或收进登记) | — |

**验证命令**:`unity test unity --mode EditMode --filter DaYiJingCheng.Tests.PatientAI --output "$PWD/unity/Logs/patientai-s001.xml"`
**变异证明**:逐条修复须能令对应断言**红**(见 §五)。

---

## 四、NOT-RUN 登记(禁借绿 —— 供收口闸门引用)

| # | 对象 | 归属 |
|---|---|---|
| NR-1 | AC-13-A1 运行期半边(Append 计数 = 0) | story 003 / 集成 |
| NR-2 | AC-13-A2 唯一取数来源闭包(本轮补,见 §三) | ✅ 本轮闭 |
| NR-3 | AC-13-A3 全类型半边(扫描根修复后闭) | ✅ 本轮闭(根修复) |
| NR-4 | QA TC-5 全类型反射扫描 | 同 NR-3 |
| NR-5 | 边缘「band 表为空 = 加载硬失败」 | `BehaviorBands` 为 readonly struct,无「空表」形态 ⇒ 规格与实现不匹配,**归 story 003 重述** |
| NR-6 | 终态后 `Tier` 规格(F-5) | 二选一明确规格,归 story 003 |
| NR-7 | `Material` 正向唯一消费点(F-4) | story 003 |
| NR-8 | `Emit` 签名类型信息(§二 F-1 残留) | story 003 |
| NR-9 | QA TC-1 单调 / TC-2 振荡 | 补测或登记 |
| NR-10 | 数值轮护栏 | 数值轮 |

---

## 五、变异证明(修复轮坐实 · 2026-10-04 实测)

**方法**:逐条**删/改生产侧承重件** ⇒ 复跑 patient-ai 套件 ⇒ 期望**恰一条**红;跑毕还原。

| 变异 | 注入 | 实测 | 落盘 |
|---|---|---|---|
| **MUT-B1** | 扫描根回退为单前缀 `...PatientAI`(删 44 桥前缀) | ✅ **恰 1 红** = `test_ac13a1_scanRootCoversAudioBridgeNamespace_b1` | `unity/Logs/mut-b1.xml` |
| **MUT-B2** | 给 Director 加 `GetVitalsRaw()`(自力产出 `VitalsDto`) | ✅ **恰 1 红** = `test_ac13a2_vitalsProducedOnlyByIVitalsQuery_sourceClosure` | `unity/Logs/mut-b2.xml` |
| **MUT-F3** | 加 `InferSessionFromProximity()`(第三 `Session` 写点) | ✅ **恰 1 红** = `test_ac13b5_sessionWriterIsUnique_reflection`,**点名该写点** | `unity/Logs/mut-f3.xml` |
| **MUT-F2** | 删构造期 `Validate` 调用(回「只有测试调用者」旧态) | ✅ **恰 1 红** = `test_ac13a3_directorConstructorRejectsBrokenBands_f2` | `unity/Logs/mut-f2.xml` |
| **MUT-m3** | 删 `SeekMin < DeathBandMin` 独立约束项 | ✅ **恰 1 红** = `test_ac13a3_validateChecksSeekBelowDeathBand_m3` | `unity/Logs/mut-m3.xml` |

**五条全落盘,各恰一条红。** 还原后复跑:**patient-ai 45/45 绿**;全量 EditMode **2384 / 2340 / 0 / 43 / 1**。

> ⚠️ **F-3/M1 修复的过程教训(诚实登记)**:首版 IL 扫描只认 `stfld`(直接写后备字段),
> 实测生产侧只扫出 `set_Session` —— 因 C# 把属性赋值编成对 **setter 的 `call`**,
> **不**直接 `stfld` ⇒ 两个语义写者(`ApplyExamSession`/`ResetForLoad`)被漏。
> 修复 = 两径都计(`stfld` 后备字段 **∪** `call set_Session`)。**首轮 2 红**正是此缺口的证据。

---

## 六、修复轮实测(2026-10-04)

| 项 | 数值 |
|---|---|
| patient-ai 测试 | **45 / 45**(38 → 45,+7 新断言) |
| 全量 EditMode | **2384 total / 2340 passed / 0 failed / 43 skipped / 1 inconclusive**(2375→2384) |
| inconclusive | 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`(与本 story 无关) |
| 变异 | **5/5 各恰一条红**(见 §五) |
| 日志 | `unity/Logs/patientai-s001-final.xml`(收口)· `full-editmode-fix.xml`(全量) |

**判定**:结构侧 CHANGES REQUIRED 的必修项(F-2/F-3)+ QA REJECT 的两条 BLOCKING(B1/B2)
+ 两条 MAJOR(M1/M2 注释订正)+ m2/m3 **全部落实并变异坐实**。
**残留 NOT-RUN**(§四)归 story 003 / 数值轮,**显式登记、禁借绿**。
