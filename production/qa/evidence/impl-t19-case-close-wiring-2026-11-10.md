# T19 甲 —— 结案路径接线(证据原件)

**日期**:2026-11-10
**批次**:T19(用户勾「甲 · 接上结案路径」)
**commit**:`a91eb0d1` + 工作树(未提交)
**引擎**:Unity 6.3 LTS(6000.3.24f1)batch EditMode / PlayMode

---

## 1. 本批要补的断点

`CaseCloseWriter.TryClose` 的**生产调用点 = 0**(写者自己的头注 `:35` 就写着「生产触发点尚未接线」)。
后果不是「少一个按钮」,而是三条连锁事实:

| # | 后果 | 判据 |
|---|---|---|
| ① | 三条 **Case 源**解锁规则全部不可达 | `TechniqueUnlockRules.Default` 的 `ForCase(2→2)` / `ForCase(6→3)` / `ForCase(17→10)` 只能由 `CaseClosed` 触发,而流里永远没有它 |
| ② | 病例生命周期停在**立案**,永不收口 | `CaseClosed` 事件数恒 0 |
| ③ | `Folded(p)` 条件 ① 的「无未结案病例」永假 | `FoldedPredicate.HasOpenCase`,7a 折叠永不发生 |

## 2. 改法(只补链接,不复制判据)

| 位置 | 作用 |
|---|---|
| `Gameplay.Boot/CaseCloseDriver.cs`(新) | 把「病人」翻成「他开着的那一例 + 该病人的病种位集」,交 37 写者;判据一行不复制 |
| `Sim/CaseOpenDecider.cs`(`CaseStreamQuery` 内新增 `DiseaseSetOf`) | 病种位集 = 该病人全部 `DiseaseOnset`(位序 `DiseaseId − 1`);**并入既有类,不新立类型** |
| `Gameplay.Boot/CompositionRoot.cs` | 装配 `CaseCloseWriter` + `CaseCloseDriver` → `services.CaseClosers` |
| `Gameplay.Boot/CompositionRootServices.cs` | 新增服务位 `CaseClosers` |
| `Gameplay.Boot/PlayerInputReader.cs` | `CloseCasePressed()`(键盘 `C` / 手柄 `leftShoulder`) |
| `Gameplay.Boot/InteractionInputDriver.cs` | 新增 `routeClosePress` 缝 + `_closeHeld` 边沿(与转诊键同构) |
| `Gameplay.Boot/BootRoot.cs` | `routeClosePress: patient => _services.CaseClosers?.TryClose(patient)` |
| `Gameplay.Boot/HookCaseReferDriver.cs` | 反查**收敛到** `CaseStreamQuery.OpenCaseOf`(删掉类内私有扫描与 `decoder` 依赖) |
| `Sim/CaseCloseWriter.cs` | 头注 `:35` 就地加注「已接」(原文不删) |

### 两个**刻意的取舍**(不是遗漏)

1. **`isChecked = true` 不是「跳过校验」**。GDD 规则六-4 要求「处置证据(流的事实)**且** 玩家自述已处置」两件同时成立;
   按键 = 玩家的自述。证据那半边仍由写者查流(`HasTreatmentInWindow`)—— 负例见 §4 的 `treatmentMissing`。
2. **「找开着的病例」不新写第二份**。原先 `HookCaseReferDriver` 类里有一份私有扫描(T15 · C3-a);
   T19 曾把它搬成 `CaseStreamQuery.TryFindOpenCase`(按 `CaseClosedPayload.CaseId` 精确比对),
   随后**删除**,改为复用既有 `OpenCaseOf` —— 后者已被 `CaseOpenWriter` / `JudgmentWriter` /
   `CasebookVitalsSource` / `FoldedPredicate` 条件③ 共用。两条规则只在**非法流**上不同
   (同一病人多案并存时,`OpenCaseOf` 遇任何 `CaseClosed` 即清空),而本处取到的只是**候选**
   caseId —— 写者 `TryClose` 会按**精确三元组**再复核(`IsOpen` / `IsClosed`),非法流上写成不成立。

## 3. 同批抓到的一条生产缺陷(off-by-one)

`TechniqueUnlockDriver.ContainsDisease` 原读 `bit = diseaseId`(`set.Bits >> diseaseId`),
而**全库写入侧**是 `bit = DiseaseId − 1`(`DiseaseIdSet` 位序契约;`PatternDetector.DiseaseBitIndex` /
`TelemetryReporter.bit + 1` 同口径)。⇒ 「病种 4 结案 ⇒ 解锁某技法」会被**病种 5** 的结案触发(静默开错技法)。

- 未暴露的原因:`CaseCloseWriter` 生产调用点当时为 0(T19 才接上),而夹具按 `1UL << d` 造位集,
  与错误读法**自洽** —— 典型的自指判据(F3)。
- 已改:`bit = diseaseId - 1`;夹具改 `1UL << (d − 1)`,并新增 `test_caseAdjacentDiseaseId_doesNotLeak`
  (集合里只有病种 5、规则指向病种 4 ⇒ 必须不解锁;差一位的读法下**红**)。

## 4. 判据覆盖

### 4.1 新增 EditMode `CaseSystem/case_close_driver_test.cs`(11 条)

| # | 测试 | 钉住的属性 |
|---|---|---|
| 1 | `test_tryClose_treatedCase_closesWithDiseaseSetFromOnsets` | 真结案 + `case_id` = 开着那一例 + 快照位集 = 全部 onset |
| 2 | `test_tryClose_treatmentMissing_rejectsNoTreatment_evenWhenChecked` | `isChecked = true` 不跳过证据半边 |
| 3 | `test_tryClose_noOpenCase_rejectsNotOpen_noWrite` | 没立案 ⇒ 零写入 |
| 4 | `test_tryClose_noneSentinel_rejectsNotOpen_noWrite` | 哨兵病人 ⇒ 零写入 |
| 5 | `test_tryClose_secondPressAfterClose_writesNothing` | 结案后再按 ⇒ 不写第二条 |
| 6 | `test_diseaseSetOf_multipleOnsets_unionOfIds` | 并集 + id 64 落 bit 63 |
| 7 | `test_diseaseSetOf_ignoresOtherPatients` | 别人的病种不得混入快照 |
| 8 | `test_diseaseSetOf_outOfRangeIds_areSkipped` | id 0 / 65 跳过 |
| 9 | `test_diseaseSetOf_noOnset_isEmpty` | 无 onset ⇒ 空集 |
| 10 | `test_caseClosedUnlocksTechnique_endToEndWithProductionRuleTable` | **端到端**:真结案 → `TechniqueUnlockRules.Default` → 技法 2 解锁 |
| 11 | `test_nullDependency_throws` | 四个依赖各自 fail-loud |

### 4.2 新增 PlayMode `CaseCloseWiringTest.cs`(1 条)

`test_caseCloseDriver_isAssembledAndWiredToTheKey`:真启动序后
`services.CaseClosers` 非 null **且** `BootRoot._interactionDriver._routeClosePress` 非 null
—— 只证接线,不证按键可读(见 §6)。

### 4.3 改造的既有夹具

- `SkillSystem/technique_unlock_trigger_test.cs`:位序改 `1UL << (d − 1)`;新增相邻 id 不串门 1 条;
  「id ≥ 64 不命中」重写为 id 65 不命中 + **id 64 必须命中**的正控(反空洞)。
- `SkillSystem/chapter_advance_test.cs`:`HookCaseReferDriver` 少一个构造参数(decoder 已摘)。

## 5. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7377 / 7340 / 0 / 0 / 37` | `2026-11-10 04:44:48Z`–`04:46:53Z` | T19 定稿(含 `CaseCloseWriter.cs` 注释加注后) | `a91eb0d1` + 工作树 |
| PlayMode | `375 / 370 / 0 / 2 / 3` | `2026-11-10 04:47:46Z`–`04:48:07Z` | T19 定稿 | `a91eb0d1` + 工作树 |

基线(T18 尾):EditMode `7365 / 7328 / 0 / 0 / 37` · PlayMode `374 / 369 / 0 / 2 / 3`。

**等式**:`7377 − 7365 = 12` = 新文件 11 条 + `technique_unlock_trigger_test` 新增 1 条;
`375 − 374 = 1` = PlayMode 新接线测 1 条。

## 6. 如实登记的未覆盖面(不得借绿)

1. **按键读面未在 batch 下验证**:`C` 的读面是 `PlayerInputReader.CloseCasePressed`,
   batch 的虚拟键盘注入需另改工程设置(与 `M` / `R` 键同一处境,见记忆 `project_unity_keyboard_injection_batch`)。
   ⇒ **边沿逻辑**(`_closeHeld` 去重)今日**零夹具**,判据只能是真包走查。转诊键 `R` 同缺,T19 未扩大该缺口。
2. **「结案后能否重新立案」未验**:`CaseOpenWriter` 的 `HasOpenCase` 前置在结案后转 false 是既有行为,
   T19 未新增夹具(既有 `case_open_and_judgment_test` 的「复诊」条覆盖)。
3. **送医 / 转诊的理由半支**仍未接(`HookCaseReferDriver` 只覆盖 `ReasonTransferred`),承 ADR-044 §D7。
4. **42 的拟物界面**仍是按键触发,**没有屏上入口** —— 承 §6-1 同因(42 侧需要一条行内态 UI)。

## 7. 证伪轮

变异窗口:`2026-11-10 04:38Z`–`04:52Z`;窗口内**不编辑**被变异文件(`CaseCloseDriver.cs` / `CaseOpenDecider.cs`),
还原判据 = **SHA-256 逐字节**(`sha256sum -c` 两件均「成功」)。靶场 = `-testFilter CaseCloseDriverTest`(11 条)。

| 变异 | 改法 | 结果 | 变红的测试 |
|---|---|---|---|
| **M1** | `isChecked: true` → `false`(自述位) | `7/11` 通过,**4 红** | `treatedCase` · `treatmentMissing` · `secondPress` · `endToEnd` |
| **M2** | `bit = DiseaseId − 1` → `bit = DiseaseId`(位序差一位) | `6/11` 通过,**5 红** | `treatedCase` · `multipleOnsets` · `ignoresOtherPatients` · `outOfRangeIds` · `endToEnd` |
| **M3** | 越界守卫 `bit >= 64` → `bit > 64` | `10/11` 通过,**1 红** | `outOfRangeIds`(id 65 ⇒ `1UL << 64` 被 CLR 掩成 bit 0) |
| **M4** | 删 `if (p.PatientId != patient.Value) continue;` | `9/11` 通过,**2 红** | `ignoresOtherPatients` · `noOnset_isEmpty` |

⚠️ **M3 只红一条**:该守卫的判别力单薄(仅越界形态),如实记录 —— 不是「已充分覆盖」。
⚠️ **未做变异**:`CaseCloseDriver` 的 `patient.IsNone` 短路 —— 去掉它,§4.1 第 4 条**仍会绿**
(流里没有 `Patient -1` 的 `CaseOpened` ⇒ `OpenCaseOf` 本来就返回 null)。⇒ 该守卫是**防御性**的,
写者侧另有一道同款守卫(`caseId.Patient < 0 ⇒ RejectNotOpen`,由 `case_close_writer_test` 覆盖)。
第 4 条钉的是**可观测行为**,不是那道守卫。

## 8. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
grep -o 'result="[^"]*" total="[^"]*" passed="[^"]*" failed="[^"]*" inconclusive="[^"]*" skipped="[^"]*"' Logs/t19-editmode-final.xml | head -1
grep -o 'result="[^"]*" total="[^"]*" passed="[^"]*" failed="[^"]*" inconclusive="[^"]*" skipped="[^"]*"' Logs/t19-playmode-final.xml | head -1
```
