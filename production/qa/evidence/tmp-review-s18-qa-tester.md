# 临时评审草稿 —— S18-1 / S18-2 可测性(只读)

**Reviewer**: qa-tester · **Date**: 2026-10-22 · **Mode**: 只读可测性评审(不跑 Unity、不改码)
**Scope**: `unity/Assets/Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs`(17 方法)
· `unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs`(29 测)
**判据面**: `production/qa/qa-plan-sprint-18-2026-10-21.md` 的 S18-1 / S18-2 两节
**初判**: **GAPS**(无 S1/S2;测试钩子总体可见,两处 AC 被「自造断言」顶替,一处 AC 的结构性断言根本不存在)
**流程说明**:本文件为草稿(先 Write 表头,逐条 Append)。**所有未读取/未核实项在末节显式登记,禁借绿**。

## 表头约定

- 判定取值:`PASS`(AC 有真载体且判别力成立)· `GAP`(有载体但判别力不足 / 语义被替换)· `NOT-RUN`(无载体)· `RISK`(实现与测试口径不一致,可能掩盖缺陷)
- 「证据」一律给文件 + 行号;**只写本次实际读到的内容**,未读到的写「在 X 范围内未发现」。

## A. S18-1 — AC → 载体映射

| ID | 判定 | 落点 | 证据 |
|---|---|---|---|
| S1-AC1 同条目不同实体 | PASS | `test_selfLimited_sameEntryDiffersByActorClass` | `entity_death_gates_test.cs:100-111`;其余入参完全相同,只差 `ActorClass`(符合 QA plan 的判别力要求) |
| S1-AC2 IsCombatant 真值表 + 反例 | PASS | `test_isCombatant_*` ×3 + `test_lethalFor_combatantAlwaysFalse_evenWhenLethal` | `:49-96`;同一 lethal 条目对 Player=true / 敌人兽=false,反例半边**在同一条方法内**成立 |
| S1-AC2b `Unspecified` fail-loud | **GAP** | 只测了 `IsCombatant` 一条入口 | `:63-71` 只断言 `IsCombatant`;`LethalFor` / `SelfLimited` 经短路/顺序进入 `IsCombatant` 的路径**零测**(见 D-1) |
| S1-AC3 正交性四组合 | PASS | `[TestCase]` ×4 | `:119-134`,四格各一测,Player 侧避免 combatant 恒定值掩盖 |
| S1-AC4 破例三条件各否例 | **GAP** | neg1 不具判别力 | `:148-156` 测的是 `HumanEnemy`,而 `EntityDeathGates.cs:148` 的 combatant 分支**先于** `:152` 的 `actorClass == Player` 返回 ⇒ 即便删掉 `actorClass == Player` 这个合取项,该测试**仍绿**(测试注释 `:152` 自陈「不是回到注册表值而是恒 true」) |
| S1-AC4b 破例其余否例 | PASS | neg2 / neg3 / neg4 + 白名单闭集 | `:158-201`;Kind / 白名单 / lethal 三半各有否例,闭集恰五项有等价断言 |
| S1-AC5 零涟漪(对照改前基线) | **GAP(自造断言)** | `test_zeroRipple_predicateDoesNotMutateEntry` | `:205-220` 断言「纯静态谓词不改入参」—— 对两个无状态的 static 方法,**恒真**,与 AC 要求的「13/52/10 既有行为不变、用 `state_machine_death_test.cs` 改前基线对照」无交集;且生产调用点 = 0 ⇒ 该 AC 在实现期**不可能**被此测试证伪 |
| S1-AC6 规则九(反射/IL + grep 双证) | **NOT-RUN** | `test_ruleNine_kindIsDataDrivenNotControlFlow` 自称有「文件末尾的 AST 式断言」 | `:241-256` 的注释 `:248` 明写「见本文件末尾的 AST 式断言(禁子串)」,而**文件在 `:258` 结束,不存在该断言**。行为测试 `:249-255` 的真假翻转变换只能抓「按 Kind 推断白名单」一类实现,抓不到 `if kind == injury {…} else {…}` 的硬分支(测试注释自己承认) |

## B. S18-2 — AC → 载体映射

| ID | 判定 | 落点 | 证据 |
|---|---|---|---|
| S2-C1 四条件逐条(单变量翻面) | PASS | ①`:131-139` ②`:171-219` ③`:223-233` ④`:384-394` | 各夹具只翻一个条件;② 的两个否例(plateau / relapse)与 ① 的否例互斥成立 |
| S2-C2 终态两态 | PASS | `test_folded_recoveredCountsAsTerminal` / `_diedCountsAsTerminal` | `:141-156`,两态并列;另 `:158-167` 有「他人终态不算」的过滤判据 |
| S2-C3 豁免口径对齐点 | PASS | `_plateauNonLethal_isExempt` / `_plateauAndLethal_isNotExempt` | `:171-194`;后者正是 QA plan 点名「最易实现成全部 plateau 免豁」的正例,存在且断言 `Folded` 为真 |
| S2-C4 relapse 正交 | PASS | `test_folded_relapseBlock_isExempt_returnsFalse` | `:196-206`;另 `:208-219` 有「任一病种豁免 ⇒ 整体不折」的多病种判据 |
| S2-AC5 ③ 复用 HasOpenCase | PASS | `_openCase` / `_closedCase` / `_emptyCaseStream` | `:223-254`;实现侧确为复用(`FoldedPredicate.cs:145`),非另抄 |
| S2-AC6 ④ SkillGrown 不被抹除 | PASS(维度受限) | `_retainsSkillGrownRows` + `_skillGrownReplayIsBitIdentical` | `:384-426`;判据是折叠**后** `Level(s)@t` 逐位一致(ADR-026 §⑦ 原文),非返回值断言 —— **不是**自造断言 |
| S2-AC7 折叠行六字段 + patient_id 单列 | PASS | `_allSixFieldsMatchTheirSources` / `_rowShapeIsExactlySixFields_inAdrOrder` / `_rowKeepsPatientId_standaloneAssertion` | `:301-344`;字段序用反射按公开实例字段比字面序列,`patient_id` 有独立断言 |
| S2-AC8 折叠后重放等价 | **GAP(限定已声明)** | 仅 `test_fold_skillGrownReplayIsBitIdentical` | `:412-426` + 头注 `:13-20`;general 重放等价不成立的原因(抹除全部病史行)在头注已声明,属 `OQ-7a-10` 未裁,记为**已知限定**而非新缺陷 |
| S2-Seam 抹除作用域(全体减 SkillGrown) | 🔁 **2026-10-24 已闭** | `HistoryFolder.cs:139-148` 的循环本身**被测到**(`:396-409`) | 「抹除面 = 全部 vs 仅终态病种」这一语义选择已由裁定 A 定为「**只有全部病种皆终态才抹**」,并由 `multiDisease_partialTerminal` 两条夹具(谓词层 + 端到端 `erasesNothing`)锚定 ⇒ 换回 ∃ 实现会**转红**(M1 证伪轮实测) |
| S2-SaveService 构造注入形态 | PASS(表面) | `test_saveService_takesFoldedPredicateViaConstructor_only` | `:485-497`;仅反射构造函数,**未实例化** ⇒ 见 D-4 |
| S2-ISaveService 方法集不变 | **GAP(proxy)** | `test_iSaveService_publicMethodSetUnchanged` | `:499-510`;断言的是 `{LoadBytes, SaveBytes}` —— 而 ADR-010 `:331` 要护的是 `Checkpoint(SaveSlot)` 的参数集,该接口**根本没有 Checkpoint** ⇒ 该断言无法因「给 Checkpoint 加形参」而转红,护错了契约 |

## C. 测试钩子可见性(问题 1)

| ID | 判定 | 落点 | 证据 |
|---|---|---|---|
| C-1 判死谓词四判据可独立驱动 | PASS | `EntityDeathGates.IsCombatant/LethalFor/SelfLimited/IsBattleInjury` 全 public static,`BattleInjuryKeys` public 只读视图 | `Sim/EntityDeathGates.cs:62,71,91,117,143`;无需反射,无需替身 |
| C-2 折叠判据可独立驱动 | PASS | `FoldedPredicate.Terminal/Exempt/HasOpenCase/DiseasesOf` 全 public | `Sim/FoldedPredicate.cs:87,110,141,154`;四个入口各有直接断言(`folded_predicate_test.cs:145,179,231`,但 `DiseasesOf` **仅有间接覆盖**,无直接调用) |
| C-3 折叠动作可驱动 | PASS | `HistoryFolder.Fold` + `FoldResult.Rows/Retained/IsIdentity` 全 public | `Sim/HistoryFolder.cs:35,38,47,69`;无反射 |
| C-4 反射使用点 | 可接受但一处可避免 | 折叠行字段序(`:320-333`)合理;`SaveService` 构造反射(`:486-497`)**无必要** | 可传 `Path.GetTempPath()` 实例化后断言 `FoldedPredicate` 属性,不必靠反射绕开文件 I/O(见 D-4) |
| C-5 自造替身 | 无问题 | 夹具用注入的 `Func<>` 闭包(`:107-110`)是设计内的 DI 缝,不是绕过 | `FoldedPredicate.cs:55-65` 构造函数即为此设计 |

## D. 触达不到 / 零覆盖路径(问题 3)

| ID | 判定 | 落点 | 证据 |
|---|---|---|---|
| **D-1** `LethalFor(Unspecified, Lethality==0)` **静默返回 false** | **RISK(S3)** | `EntityDeathGates.cs:120` `LethalityOf(entry) && !IsCombatant(actorClass)` —— `&&` 左操作数为假时 `IsCombatant` **不被调用**,fail-loud 守卫被短路绕开 | 全测试文件对 `Unspecified` 只有一条调用(`:69`,`IsCombatant` 直调)。同一 `Unspecified` 入参:lethal 条目抛,非 lethal 条目静默返回 false ⇒ 零值守卫**半失效**;`ActorClass.cs:33-38` 与 `EntityDeathGates.cs:85-90` 的「fail-loud,不猜」承诺在不对称路径上不成立 |
| D-2 `SelfLimited` 破例里的 `actorClass == Player` 合取项不可证伪 | RISK | `EntityDeathGates.cs:148` 先返回、`:152` 后判 ⇒ 该合取项对任何可达输入**恒真**;删除它不会使任一测试转红 | 与 S1-AC4 同一事实的两种表述;若保留该写法,应在注释里明写「该合取项是防御性的、当前不可达」,否则会被误读为「三条件各否例已覆盖」 |
| D-3 `HistoryFolder.Fold` 的入参守卫 | GAP | `HistoryFolder.cs:75-80` 三处 `ArgumentNullException` + 负 id `ArgumentOutOfRangeException` 全树零测 | 测试只测了 `FoldedPredicate.Folded(-1)`(`:264-270`),`Fold(-1)` 的独立守卫未触达 |
| D-4 `SaveService.FoldedPredicate` 属性 | **GAP** | `Sim.Codec/SaveService.cs:59` `public IFoldedPredicate FoldedPredicate => _folded;` 全测试树零引用 | 变异成 `=> null` **无测试转红**;同时「构造注入的实例是否真被存下」也从未被验证(测试只反射了形参类型与默认值) |
| D-5 `FoldedPredicate` / `HistoryFolder` 的 null 流分支 | GAP | `FoldedPredicate.cs:89-90`(history==null)、`:143-144`(case==null)、`HistoryFolder` 认为 history 恒非 null | 测试只经「空 List」而非 null;`events == null` 两处分支不可达 |
| D-6 `FoldedPredicate` 构造函数四守卫 | GAP(低) | `FoldedPredicate.cs:61-64` | 四参数 null 抛出零测 |
| D-7 `EntityDeathGates` 的 entry==null 守卫 | GAP(低) | `EntityDeathGates.cs:73,119,145` | 三处 `ArgumentNullException` 零测 |
| D-8 `HistoryFolder` 抹除循环 | **已被覆盖**(订正任务描述中的候选) | `folded_predicate_test.cs:396-409` 断言 P 的非 SkillGrown 行归零、OTHER 两行不动 | 结论:循环可测且已测;**不可见的是其作用域语义**(见 S2-Seam) |
| D-9 `ActorClass.Unspecified` 抛错路径 | 部分覆盖 | `IsCombatant` 已测(`:63-71`);`LethalFor` 的短路洞见 D-1;`SelfLimited` 因 `:148` 先调 `IsCombatant` 而**总是抛**,该分支实际可达但无独立断言 | — |

## E. 新边界未覆盖(问题 4)

| ID | 判定 | 落点 | 证据 |
|---|---|---|---|
| E-1 `Rows.Count == 0` 与「折叠成功但零行」的区分 | GAP(潜伏) | `FoldResult.IsIdentity => Rows.Count == 0`(`HistoryFolder.cs:47`) | 只测了「谓词假 ⇒ Rows 空 + 流恒等」(`:444-455`);`IsIdentity` 把两种状态压成一个布尔,调用方**无法区分**「没折」与「折了但零行」。当前结构上不可达(Terminal 要求存在 outcome ⇒ rows ≥ 1),但**没有一条测试把这个不变量钉住**,将来放宽 ① 就会静默退化 |
| E-2 同 tick 多病种 onset(排序 tie-break) | GAP | `HistoryFolder.cs:136-137` 三元排序的 `DiseaseId` 分支 | 两处排序测试的 onset 均不相同(`:347-380` 用 10/12 与 20/30)⇒ **`a.Onset != b.Onset` 的假分支从未执行**;同 tick 两病种的行序无断言 |
| E-3 某病种有 onset 无 outcome 时被连带抹除 | 🔁 **2026-10-24 已闭** —— 裁定 A(∀ 收紧)后此形态**不再被抹**;由 `test_folded_multiDisease_partialTerminal_isNotTerminal` + `test_fold_multiDisease_partialTerminal_erasesNothing` 双夹具钉住 | `HistoryFolder.cs:139-148` 按**病人**整体抹除(**仅在全部病种皆终态时**) | 已补两测。构造:病种 A 有终态、病种 B 只有 onset ⇒ `Folded=true`、rows=1、**B 的 onset 行随病人一并消失且无折叠行**。这正是 `OQ-7a-10` 的缝;测试面完全空白 ⇒ 该语义既未被确认也未被否定 |
| E-4 `PatientOutcomeChanged` 重复抛错 | PASS | `:459-469` | 断言 `InvalidDataException` + 报文「至多一条」 |
| E-5 有 outcome 无 onset 抛错 | PASS | `:471-481` | 报文点名 `DiseaseOnset` |
| E-6 未知病种 fail-loud | PASS | `:272-281` | 且**只在 Terminal 通过后**触发,符合短路次序 |
| E-7 同一病种两条 onset(去重取首条) | GAP(低) | `HistoryFolder.cs:99-100` | 无测试;`DiseasesOf` 的 `seen` 去重同样仅间接覆盖 |
| E-8 `PatientId.None(-1)` 事件混在流里 | GAP(低) | 世界级事件哨兵不入病史流的前提未被测试锚定 | 无测试 |

## F. 证伪轮形态(问题 5)

| ID | 轮次/变异 | 打中的判据 | 证据/说明 |
|---|---|---|---|
| F-1 | 定义域守卫(停用) | **归因不明** | 若指 `Folded(-1)` / `DiseaseKind.Unspecified`:打的是「入参定义域」,不是四条件之一;若指 `R1_CHECK_31`(QA plan §实施轮 B 的写法):打的是**注册表字段**,与 S18-1/S18-2 的谓词码**无关**。两种读法下该轮都**不构成**任一 AC 的条件级反证 ⇒ 收口时须写明变异落点与靶判据的对应 |
| F-2 | `Lethality<=0` 半条件 | ② 的 `lethal=false` 半边(`FoldedPredicate.cs:123`)/ 或 `LethalityOf`(`EntityDeathGates.cs:169`) | 三者共用 `Lethality > 0` 的取法,一次变异可同时打两处;靶点唯一性未声明 |
| F-3 | 重复终态抛错 | 流损坏路径(`HistoryFolder.cs:108-111`) | 不是四条件之一;属 fail-loud 面 |
| F-4 | `SkillGrown` 豁免 | 条件 ④(抹除例外,`HistoryFolder.cs:145`) | 是唯一打在折叠**动作**语义上的轮次 |
| **F-5** | **条件 ① 终态(Terminal 存在性查询)** | **无变异** | `FoldedPredicate.cs:87-98` 的 `Kind == PatientOutcomeChanged` 与 `Patient.Value == patientId` 两个合取项,**没有任何轮次反证**;把 `PatientOutcomeChanged` 改成任意 Kind 或删掉病人过滤,现有测试会红吗?`:158-167` 会抓病人过滤,但 Kind 误改仍可被 `:124-139` 抓(改动会让 Folded 恒 false) —— **但只是推断,未实测**。登记为「无已跑变异」 |
| **F-6** | **条件 ③ `HasOpenCase` 复用** | **无变异** | 若在 `FoldedPredicate` 里把 `HasOpenCase` 改成恒 `false`,`:223-233` 会红(该测试先断言 `HasOpenCase(P)` 为真再断言 `Folded` 为假)。判别力**存在但未做变异反证** |
| **F-7** | **抹除作用域(全体 vs 逐病种)** | **无变异** | 见 S2-Seam / E-3。改变 `belongsToFolded` 的谓词(**按病种保留**)后,现有断言仍绿 ⇒ **该偏离没有任何测试形态能抓住**,这是本次评审最关键的一处「假绿面」 |
| **F-8** | **折叠行字段来源(onset / seed / outcome / t_end)** | **无变异** | `:301-317` 的六字段断言依赖夹具里 payload 与头部的一致性;`t_end` 取头部 `Tick`、`seed` 取载荷 —— 两者若互相替换,**现有夹具无法区分**(`Outcome()` 未在载荷里放 tick,故 t_end 唯一来源是头部,这一点是对的;`seed` 若能重算则无法判别,属设计使然)。变异未跑 |
| **F-9** | **排序确定性 & tie-break** | **无变异** | 见 E-2 |
| **F-10** | **S18-1 combatant 分支**(`LethalFor` / `SelfLimited` 对敌人兽的恒值) | **无变异** | 四轮全部落在 S18-2 侧(或注册表侧)⇒ **S18-1 的谓词本体零证伪轮**;仅有行为断言,没有「改坏生产码 ⇒ 指定断言转红」的反证 |
| **F-11** | S18-1 破例 `actorClass == Player` | **不可变异**(变异=恒真) | 见 D-2;这一项不是「轮次缺失」,而是「该判据本身不可被任何测试区分」 |

## G. 缺口清单(按优先级)

| ID | 级别 | 缺口 | 建议最小动作 |
|---|---|---|---|
| G-1 | **RISK / 潜在 S3** | `LethalFor` 对 `Unspecified` 的 fail-loud 被 `&&` 短路打掉(非 lethal 条目静默 false) | 先由实现方裁定是否要「先解析类别再判 lethal」;裁定后补 `test_lethalFor_unspecifiedOnNonLethal_throws`(现写法下该测试会**失败**,故须先裁) |
| G-2 | **GAP(高)** | 抹除作用域(`OQ-7a-10` 偏离)零测试、证伪轮零变异 | 补一条「A 有终态 + B 只有 onset」的夹具,把**当前**行为钉住(rows=1、B 的 onset 行被抹),并在注释挂 `OQ-7a-10`;否则该语义变更**永不被发现** |
| G-3 | **GAP(高)** | AC ⑥ 的结构性断言缺失,而注释谎称存在 | 要么补真断言(反射/IL 或源码扫描门),要么把 `:248` 的注释改成「本文件不含结构性守卫,AC ⑥ 为 NOT-RUN」 |
| G-4 | **GAP(高)** | AC ⑤ 被等价改写为「不改入参」 | 恢复 QA plan 的原意(对 `state_machine_death_test.cs` 改前基线对照);若因「生产调用点=0」无法成立,应登记为「零涟漪不可测,理由 = 无调用点」而不是用恒真断言充数 |
| G-5 | GAP(中) | AC ④ 的「去掉 Player 条件」不可证伪 | 明写该合取项当前不可达(防御性),避免与「三否例已覆盖」混读 |
| G-6 | GAP(中) | `SaveService.FoldedPredicate` 属性零引用 | 用 `Path.GetTempPath()` 实例化并断言属性返回注入对象 / 未注入为 null |
| G-7 | GAP(中) | 同 tick 多病种排序 tie-break 未执行 | 补一条两病种同 `OnsetTick` 的夹具,断言按 `DiseaseId` 升序 |
| G-8 | GAP(中) | `FoldResult.IsIdentity` 把「未折」与「折了零行」压成一个布尔 | 补不变量断言 `Folded(p)==true ⇒ Rows.Count > 0`(或拆字段),把当前不可达性钉住 |
| G-9 | GAP(低) | `HistoryFolder.Fold` 入参守卫、`FoldedPredicate` 构造守卫、entry==null 守卫全零测 | 各补一条 `Assert.Throws` |
| G-10 | GAP(低) | `ISaveService` 方法集断言护错契约(断言 `{LoadBytes,SaveBytes}`,ADR-010 要护 `Checkpoint(SaveSlot)`) | 注释订正为「Checkpoint 尚未落,本断言仅冻结现状」,不得声称护住 ADR-010 :331 |
| G-11 | 已知未结 | AC ⑦(`IDeathCandidateSource`)/ AC ⑧(`PlayerDied` / `InjuryStateChanged` 写者)生产树零命中 | 与本次核验一致:`entity_death_gates_test.cs:11-15` 头注如实声明 NOT-RUN;不计为新发现 |

## H. 未及核查项(禁借绿)

| # | 项 | 原因 |
|---|---|---|
| 1 | 证伪轮 4 轮的**实际变异落点与靶判据**(命令行/日志/SHA-256 还原判据) | 只读评审,未读证伪轮证据原件;F-1 的归因不明即由此而来 |
| 2 | `save_service_test.cs` 是否已用 `Path.GetTempPath()` 实例化 `SaveService`(影响 D-4 的严重度) | 未读该文件 |
| 3 | `CaseStreamQuery.HasOpenCase` 本体是否覆盖「重复 CaseClosed」等畸形流 | 未读 `Sim/CaseOpenDecider.cs` |
| 4 | `DiseaseKind` 与 `DiseaseKey` 前缀(`INJ_`)的一致性是否有门约束(破例依赖键名字面) | 未读 `RegistrySchema.cs` 校验段 |
| 5 | 全量回归三元组与「消失 0」 | 本评审不跑测试,引用他人数字即属借绿 |
| 6 | `WriterExistenceGate.cs:98-102` 的豁免表当前形态 | 未读源码,仅据任务背景陈述 |

**工具预算说明**:本评审在 12 次调用上限内完成 A–H 全部小节;H 中 6 项为**未核**而非「已核为绿」。
