# QA 策略 — sprint-18(team-qa Phase 2 · qa-lead)

**日期**: 2026-10-22 · **Stage**: Pre-Production · **Review mode**: `lean`
**代码树现值**: `ad7316c` · `git status` 干净(0 项)
**本件性质**: 只出策略,不写测试用例(Phase 4 另派)。**不修改任何既有文件。**

---

## §0 勘察期的四处事实更正(先于判定)

| # | 派单 / 既有件的说法 | 实测 | 出处 |
|---|---|---|---|
| 0-1 | 「S18-1 的实测文件是 `entity_death_gates_test.cs`,QA plan 点名的 `self_limited_predicate_test.cs` 不存在」 | ✅ **成立**。`Tests/EditMode/DiseaseSimulation/` 下只有 `entity_death_gates_test.cs` | `ls` 实测 |
| 0-2 | smoke 报告 verdict = `PASS WITH WARNINGS`,`S18-2` 记 `MISSING` / 「件未开工」 | ⚠️ **该判定只对 commit `220acc3` 成立**。smoke 报告自身头注 `HEAD = 2ba2e9b`、`220acc3` 后「零 `.cs` 差异」—— 而 **`ad7316c` 在它之后**落了 6 个 `.cs`(1004 行)+ 29 测 | `smoke-2026-10-22.md:10-17`、`:58`、`:95-98`;`git log` |
| 0-3 | 「S18-1 = 20 测」 | 实测 **17 个 `public void test_` 方法**(`test_orthogonality_…` 带 `TestCase` 参数化,展开后或为 20)。**计数口径未核**,不以 20 作判据 | `entity_death_gates_test.cs` grep 计数 = 17 |
| 0-4 | smoke 报告预期 S18-2 测试落点 `Tests/EditMode/Persistence/folded_predicate_test.cs` | ❌ 实际落 `Tests/EditMode/**PersistenceService**/folded_predicate_test.cs`;目录名不同 ⇒ **按 smoke 的预期路径去「找证据」会扑空**(第五形态:扫不到被测装配) | `ad7316c --stat`、`ls` 实测 |

> ⚠️ **0-2 是本轮最重要的一条**:smoke 报告唯一的那条 WARNING(S18-2 无测试证据)**已被 `ad7316c` 推翻**。
> 但「WARNING 的前提消失」≠「新树 PASS」—— 新树有 6 个从未 smoke 过的生产件。

---

## §1 分类表(逐项 · 类型 / 自动化证据 / 人工验收 / 阻塞)

| 项 | 类型(判定) | 自动化证据 | 人工验收 | 是否阻塞 | 判定 |
|---|---|---|---|---|---|
| **S18-1** `SelfLimited` 谓词及依赖面 | **Logic**(承 QA plan 推断) | **部分**:谓词面有 17 方法/约 20 例(全 `Passed`);**AC ⑦⑧ 零证据** | 无(纯函数) | **BLOCKING** | **不达 Done**(见 §3/§4) |
| **S18-2** `Folded(p)` + 折叠行 | **Logic** | **齐**:29 测(全 `Passed`)+ 证伪轮 4/4 全红 | 无(纯函数) | **ADVISORY**(判定面齐,接线未落) | **达 Done(谓词级)**,但生产零接线(见 §5) |
| **S18-3** 台账回写 | **Config/Data**(纸面) | **齐**:5 条可复算判据全过(判据 4 数字订正 2→4 已就地记) | 无 | **ADVISORY** | **达 Done** |

**分类依据**:两件实现均为纯判定 / 纯整数域,无 UI / VFX / 「手感」,与 QA plan 一致。
**逐项阻塞性**:
- S18-1 → **阻塞**。理由:① `status = backlog`(件未完成);② 8 条「What to test」中 ⑦⑧ **零命中**;
  ③ **谓词本体无证伪轮**(`R1_CHECK_31` 的那轮证伪守的是 *kind 守卫*,不是 `SelfLimited`/`LethalFor`)。
- S18-2 → **不阻塞本站交付**,但 3 条条件未结(零生产调用点 · `OQ-7a-10` · `PRE-S18-D` 待确认)。
- S18-3 → **不阻塞**,已结。

---

## §2 Smoke check 判定

**判定:对当前树 `ad7316c` = `UNKNOWN`(不可用);对其自述树 `220acc3` = `PASS WITH WARNINGS`(有效但已过期)。**

| 判据 | 实测 | 结论 |
|---|---|---|
| 报告时间 / 批次 / commit | `2026-10-22 07:34` · `s18-editmode-final-20261022` · **`220acc3`** | 三元组齐备 ✅ |
| 覆盖的是不是 sprint-18 **最终**树 | ❌ **不是**。`ad7316c`(S18-2)晚于 `220acc3`;`fc1705d`(S18-1)亦在其后…… | **覆盖面缺口** |
| verdict 依据是否仍成立 | smoke 的唯一 WARNING = 「S18-2 无测试证据 ⇒ MISSING」;该前提**已被 `ad7316c` 推翻** | 判定**失真** |

**为什么不给 `PASS WITH WARNINGS`**:该 verdict 的 WARNING 集合已被证据作废,而新树新增 6 个 `.cs` + 29 测**从未过 smoke**。
按 skill 规则「无法确认即 `UNKNOWN`」—— 现有报告**不是**当前树的 smoke 结论。
**为什么不给 `FAIL`**:无任何失败证据;`4418 / 4382 / 0 / 36` 是新树上的真跑数(引自编排方实测 · 批次 `s18-2-folded-full-20261022`)。
**为什么不给 `PASS`**:smoke 报告的 coverage 表里 S18-2 = `MISSING`,与现值直接矛盾 ⇒ 引用它即「借绿」。

> **必做的动作**:S18-2 落地后须**重跑 `/smoke-check sprint`**,新报告的三元组 commit 字段须为 `ad7316c` 或其后代。
> 在此之前,S18-2 不得引 smoke 报告作证据。

---

## §3 S18-1 的账实不符 —— 判定 = **两者兼有,但性质不同**

| 面 | 判定 | 证据 | 出处 |
|---|---|---|---|
| **状态字段 `status: "backlog"`** | ✅ **正确,不是记账缺陷** | AC ⑦(生产调用点)`EntityDeathGates` **生产命中 = 0**(排除 `/Tests/` 后仅剩 `FoldedPredicate.cs:23` 的一句**注释**);AC ⑧(`InjuryStateChanged` 写者)仍挂 `WriterExistenceGate` 豁免表 | grep 实测 · `WriterExistenceGate.cs:98-102` |
| **`evidence:` 字段** | ❌ **记账缺陷** | 指向 `Tests/EditMode/DiseaseSimulation/self_limited_predicate_test.cs` —— **该文件不存在**;实际载体是 `entity_death_gates_test.cs` | `sprint-status.yaml:52` vs `ls` |
| **`sprint-18.md` / QA plan 的路径面** | ❌ **同型缺陷(第二处)** | QA plan `:35` / `:55` 点名同一不存在的路径 | `qa-plan-sprint-18:35/55` |
| **实现本体** | ⚠️ **部分落盘(17 方法已落,谓词面有测)** | `fc1705d` 交 `Sim/EntityDeathGates.cs` + `Sim.Contracts/ActorClass.cs` + 夹具 | `git log` · smoke `:109-110` |

**结论**:**实现未完成** 是主因(status 如实反映);**记账缺陷**是并发的次因 —— 但它有两处,
且两处都属「**判据载体指错**」:若下游按 `evidence:` 路径去核,会得出「S18-1 零测试」的**反向误判**。
这比状态字段错更危险(借绿的第六形态:载体错位)。

> 与 smoke `:102-104` 的口径一致(「这不是漏测,是件未完成」)—— 我确认该结论,并补两点它没写的:
> ① `evidence:` 路径本身是错的(它不是「未完成」,是「指向不存在的文件」);
> ② **S18-1 的谓词本体缺证伪轮** —— 已跑的证伪轮(`R1_CHECK_31` 那条,1 红)守的是 **kind 零值守卫**,
> 与 `SelfLimited`/`LethalFor`/`IsCombatant` **无一条断言关系**。DoD 明写「每件过证伪轮」。

---

## §4 S18-1 的 AC 面缺口(QA plan 8 条逐条)

| # | QA plan 要测的 | 判定 | 证据 |
|---|---|---|---|
| 1 | 同病种同 `self_limit`,`Player` vs `NPC` 判死不同 | **已覆盖** | `test_selfLimited_sameEntryDiffersByActorClass`(:101) |
| 2 | `IsCombatant` 真值表 + 一条反例 | **已覆盖** | `test_isCombatant_playerIsNotCombatant` / `…enemyAndBeastAreCombatants` / `…unspecifiedFailsLoud` + `test_lethalFor_combatantAlwaysFalse_evenWhenLethal` + `…nonLethalEntryAlwaysFalse` |
| 3 | 正交性四组合(真真/真假/假真/假假) | **已覆盖(方法级)**,参数化展开数未核 | `test_orthogonality_lethalAndSelfLimitedIndependent`(:123,带 `TestCase`) |
| 4 | 三条件合取 + 三条否例 | **已覆盖且超额**(4 条否例) | `neg1_notPlayer` / `neg2_notInjuryKind` / `neg3_notInWhitelist` / `neg4_notLethal` + `selfLimitFalseIsNoOp` + `whitelist_isExactlyTheFiveKeys` |
| 5 | **零涟漪**:用**改前基线**(`state_machine_death_test.cs` 既有断言)对照,不是自造期望 | ⚠️ **部分覆盖** | 有 `test_zeroRipple_predicateDoesNotMutateEntry` / `…legacyNullCurveTreatedAsNotSelfLimiting` —— 但那是**新文件内的自造断言**,不是「跑 `state_machine_death_test.cs` 既有 11 条对照」。QA plan 连带后果 ② 要求的「11 条既有测试改写」**未做**,而它们钉的是**改前无条件语义** ⇒ 本条的判据形态与 plan 要求**不同** |
| 6 | 规则九:`d.kind` 走数据驱动,**不得有 `if kind == injury` 分支**;判据 = **反射/IL 扫描 + 源码 grep 双证** | ⚠️ **部分覆盖** | 生产侧形态**合规**:`IsBattleInjury` = `entry.Kind == DiseaseKind.Injury && … && whitelist.Contains(key)`(`EntityDeathGates.cs:71-77`),是**集合成员判定**不是控制流分叉;有 `test_ruleNine_kindIsDataDrivenNotControlFlow`。**但 IL/反射那一半我未核** —— 只看到方法名,未读其实现体 ⇒ **不作「双证成立」主张** |
| 7 | `DeathCandidate` 产出路径有生产调用点 | ❌ **未覆盖(零命中)** | `IDeathCandidateSource` / `PlayerDeathWarranted` **生产树 0 命中**(排除 `/Tests/`);`EntityDeathGates` 生产命中亦为 0 ⇒ **连接口都还没实现**,不只是没接线 |
| 8 | `InjuryStateChanged` 写者落地 | ❌ **未覆盖** | `WriterExistenceGate.cs:102` 仍挂「9 伤情状态真值写者未落 —— 归 9 实现轮(2026-10-10)」 |

**边界三条**:`self_limit=false` 对 `Player`(**已覆盖** `…selfLimitFalseIsNoOp`)· 白名单空集(⚠️ **未见对应测**;
`whitelist_isExactlyTheFiveKeys` 断言的是「恰五个」,**不是**「空集降级不崩」—— 两者不等价)·
`kind=disease` 对 `Player` 破例不成立(**已覆盖** `neg2_notInjuryKind`)。

**S18-1 小结**:**5/8 覆盖 · 2/8 部分 · 2/8 未覆盖**(⑦⑧)+ 1 条边界未见。
⇒ 判据面**不足以**让 S18-1 转 `done`;但**谓词本体面**(①②③④)是扎实的。

> ⚠️ **一条只属于 S18-1 的假绿形态**:它 17 个方法全绿,而 `EntityDeathGates` **零生产调用点** ——
> 这绿的语义是「谓词自洽」,**不是**「9 用它判死」。任何「F4 死亡支已实现」的表述都会是越权
> (承本仓「自造断言顶替 AC 语义层」三次教训)。QC 判据必须带「生产调用点」这一层。

---

## §5 S18-2 的 AC 面缺口(QA plan 8 条 + 3 条边界)

| # | QA plan 要测的 | 判定 | 证据 |
|---|---|---|---|
| 1 | 四条件逐条,各一否例 | **已覆盖** | `noTerminalEvent` / `plateauNonLethal_isExempt` / `openCase` / `retainsSkillGrownRows` + `allFourConditionsHold` |
| 2 | 终态:痊愈 / 死亡 各一测 | **已覆盖** | `recoveredCountsAsTerminal` / `diedCountsAsTerminal` + `terminalEventOfOtherPatient_doesNotCount` |
| 3 | `lethal=false ∧ plateau` 不折;**`plateau ∧ lethal` 可折**(口径对齐点) | **已覆盖** | `plateauNonLethal_isExempt_returnsFalse` / `**plateauAndLethal_isNotExempt_returnsTrue**`(恰是证伪轮②的目标) |
| 4 | `relapse.*` 块不折 | **已覆盖** | `relapseBlock_isExempt_returnsFalse` |
| 5 | ③ 有未结案 ⇒ 不折;**复用 `HasOpenCase`,不得另写** | **已覆盖** | `openCase` / `closedCase_countsAsNoOpenCase` / `emptyCaseStream_treatedAsNoOpenCase`;`SaveService` 侧**零判定逻辑**(只有构造注入 + 属性)⇒ 禁项自动满足 |
| 6 | ④ `SkillGrown` 行不被抹除;**判据 = 折叠后 `Level(s)@t` 逐位一致**,不是「返回某值」 | **已覆盖** | `retainsSkillGrownRows` + `skillGrownReplayIsBitIdentical` |
| 7 | 折叠行六字段 + **`patient_id` 单独断言** | **已覆盖** | `rowShapeIsExactlySixFields_inAdrOrder` / `allSixFieldsMatchTheirSources` / **`rowKeepsPatientId_standaloneAssertion`** |
| 8 | **折叠后重放等价**:折叠 + 重放 ≡ 未折叠全量重放 | ⚠️ **部分覆盖(语义被收窄)** | 只有 `skillGrownReplayIsBitIdentical` 一条「逐位一致」,它覆盖的**仅是 skill-growth 维**。而 `removesPatientRowsButKeepsOtherPatients` 表明实现**抹除该病人全部病史行**(减 `SkillGrown`)⇒ 被抹掉的 `CareApplied` / `DrugTreatmentApplied` / `EmergencyAttempt` 在折叠后**不可重放** ⇒ **general 意义的 AC ⑧ 无法成立**。该收窄正是 `OQ-7a-10`(见 §6) |

**边界三条**:**全中** —— 空病史流(`emptyHistory_returnsFalseWithoutThrow`)· 多病程按**病人整体**判定
(`exemptOnAnyOfMultipleDiseases` + `multiDisease_producesOneRowPerDisease`)· 病例流空(`emptyCaseStream_treatedAsNoOpenCase`)。
**额外加分两条**(不在 plan 但在盘上):`saveService_takesFoldedPredicateViaConstructor_only` +
`iSaveService_publicMethodSetUnchanged` ⇒ 直接守住 ADR-010 `:331` 的 `{SaveSlot}` 反射断言。

**S18-2 小结**:**7/8 覆盖 + 1/8 语义收窄 + 3/3 边界**。**谓词级判据面是齐的**。

### §5.1 证伪轮的判别力 —— 「无附带红」能否排除「夹具对变异无判别力」?

**我的判定:接受作为「四条条件各自承重」的证据,但拒绝把它读成「夹具整体有判别力」。理由如下(三层)。**

1. **「无附带红」本身的证据力是中性的,不是正面的**。它同时兼容两种世界:
   (a) 夹具隔离良好,变异只打中目标;(b) 该变异对夹具**本就无影响**,恰红来自别处。
   排除 (b) 靠的是「**恰红的是 pre-registered 的那一条**」,而 `sha256` 还原只证**还原**,不证**变异生效**。
   ⇒ 这 4 轮真正证明的是「这 4 条断言**依赖**这 4 处代码」,**不是**「夹具能抓住一般性错误」。
   承本仓 `project-falsification-probe-pitfalls.md` 的三种假存活:**本轮不属假存活**(每轮都有独立目标红、
   还原逐字节),但它属**作用域受限**的真存活。
2. **四条变异的覆盖面**:定义域守卫 / `Lethality<=0` 半条件 / 重复终态抛错 / `SkillGrown` 豁免 ——
   **正好是「四条件 + 一条守卫」**,与 QA plan 的「四条件必须独立可辨」对齐度**高**。
   这是本轮最强的部分:**条件 ② 的半条件**(删掉 `Lethality<=0` 让 plateau 单独成豁免)**是最容易写成
   「全 plateau 免豁」的错** —— 它恰被 `plateauAndLethal_isNotExempt_returnsTrue` 抓住 ⇒ 该假绿的**主要形态已被反证**。
3. **未覆盖的变异面(必须列为条件,不得记绿)**:
   - **抹除作用域**:没有任何变异打 `HistoryFolder` 的「抹哪些行」。若把「抹除全部减 `SkillGrown`」改成
     「只抹 onset/outcome 行、保留 `CareApplied`」,现有测里**只有** `removesPatientRowsButKeepsOtherPatients`
     有可能红(取决于它断的是**计数**还是**具体行身份**)。**这一面从未被反证**,且它**恰好就是实现偏离所在**(§6)。
   - **`SaveService` 接线**:形参默认 `null` ⇒ 可空路径未被任何变异打过;`saveService_takesFoldedPredicate…`
     断的是**签名形状**,不是**行为**。
   - **`HasOpenCase` 的取反/短路**:无变异。
   ⇒ **结论:「4/4 全红 + 无附带红」足以支撑「四条件各自承重」,不足以支撑「折叠产出面有判别力」。**

> **给签核的措辞建议**:写「条件 ①–④ 各自由变异反证(N=4,各自恰红、零附带、还原逐字节)」,
> **不要**写「夹具具备判别力」或「折叠产出面已验证」。

---

## §6 `OQ-7a-10` 是否构成签核条件 —— **是条件项(非阻塞),且它让 AC ⑧ 的语义处于未裁状态**

> 🔁 **2026-10-24 补注(原文不删)—— 本节的前提「语义未裁」已解除**
>
> `OQ-7a-10` 已由用户裁定 **A**:条件 ① 由 ∃ 收紧为 **∀**(`终态(p) = ∀ d ∈ D_p : ∃ PatientOutcomeChanged(p, d)`),
> 空集 `D_p = ∅` ⇒ **false**。落点 = `Sim/FoldedPredicate.cs`,夹具 5 条,证伪轮 2/2 全红。
>
> **对本节三处判定的影响(逐条)**:
> - 「AC ⑧ 的语义未裁」⇒ ❌ **不成立** —— 现语义已定:抹除只在**全部病种皆终态**时发生。
>   但 **AC ⑧ 的 general 重放等价仍不成立**(终态病人的 `CareApplied` 等行照旧被抹),
>   故「不得把『折叠不影响重放』写成已达成」这条**仍然成立**,理由从「未裁」换成「折叠语义本身」。
> - 「数据丢失不可达」⇒ ✅ **仍成立**,理由不变(`FoldedPredicate` 零生产调用点)。
> - 「将来会静默出错且无机械门」⇒ ⚠️ **部分缓解**:「A 终态、B 演进时被误抹」这一条**已不可达**
>   (被 ∀ 挡在条件 ① 外);但「全部终态后 `CareApplied` 被抹」是**设计内行为**,不是缺陷。
>   **仍缺的**是 `Erase` 取舍面的变异反证 —— 归 `TD-persist-01`,不在本件条件面。
>
> ⇒ 条件项 C-6 的**形态**由「裁 OQ **或**补判据」变为**只剩「补判据」**一条路。

| 问题 | 判定 | 依据 |
|---|---|---|
| 它是否让**某条 AC 的语义**未裁? | ✅ **是 —— AC ⑧(折叠后重放等价)** | GDD `:639` 自述:条件 ① 是**病人级存在性查询**,而折叠行按 `(病人 × 病种)` 逐行产;当前实现取「权威字面」⇒ 抹除该病人**全部**病史行(减 `SkillGrown`),**不按病种保留**。GDD 原文**未裁**「被抹掉的那些病史行还能不能重放」 |
| 它是否让 AC ①–⑦ 失控? | ❌ 否 | ①②③④ 是布尔谓词面;⑤ 是复用 `HasOpenCase`;⑥ 是 `SkillGrown` 维逐位;⑦ 是行形状。四条件与行形状**与保留面正交** |
| 是否阻塞本次签核? | ❌ **不阻塞**(ADVISORY 级条件) | 今天的**数据丢失不可达**:`FoldedPredicate` **零生产调用点**(`SaveService` 形参默认 `null`,无装配根传入)⇒ 折叠动作**不在任何存档路径上**。且已登记归属(7a × 9 × 37)+ 期限(P1a 前,与 7a 往返轮同批) |
| 但它会不会将来静默出错? | ⚠️ **会,且无机械门** | 一旦 7a 把 `FoldedPredicate` 接进 `Checkpoint`,「抹除全部非 `SkillGrown` 行」就会**真实丢数据**,而唯一反查判据是「折叠后重放与未折叠不等」—— 目前**没有这条测**(见 §5.1 层 3) |

**结论**:`OQ-7a-10` 构成**条件项 C-6**,判据形态 = 「AC ⑧ 的判据面就地写明覆盖范围(仅 skill-growth 维),
**或**补齐其余病史行的重放等价判据」。**不得**在 GDD 未裁之前把「折叠不影响重放」写成已达成。

---

## §7 人工 QA 量估算 + 签核建议

### 7.1 人工 QA 量

| 面 | 数量 | 说明 |
|---|---|---|
| 人工测试用例 | **0 项 / 0 人时** | 三件全在纯函数 + 纸面判据面,无 UI / VFX / 手感 / 真机可观察项。QA plan §Manual QA Checklist 的「零人工项」判定**在现状下依然成立** |
| 启动可玩 smoke | **0**(沿用计划免测) | 零玩家可见变化。⚠️ 但**理由**须订正:smoke `:88` 写「因为还没接线」—— 该理由**只对 S18-1 成立**;S18-2 的理由是「谓词未进存档路径」,不是「未接线」 |
| **将来**会需要的人工/集成面 | 1 项(不计入本 sprint) | 9 的终态写者落地后,须一条 **PlayMode 集成测**:F4 判终态的那一 tick 写出 `PatientOutcomeChanged`,且漏写能被反查判据抓到(否则「该病人永不折叠」无门可扫) |

⇒ **本 sprint 不需要排人工 QA 班次。** 缺口全在**自动化侧**(S18-1 的证伪轮 + AC ⑦⑧),不是人工侧。

### 7.2 签核建议

**整体 verdict:`NOT APPROVED`(sprint 级不可收口)**

分件判定:

| 件 | 判定 | 一句话理由 |
|---|---|---|
| **S18-3** | **APPROVED** | 5 条判据可复算全过;判据 4 的 2→4 订正已就地记账,属**计划数字错**而非执行错 |
| **S18-2** | **APPROVED WITH CONDITIONS**(C-4…C-7) | 谓词级判据面 7/8 + 3/3 边界 + 四条变异反证;**但零生产调用点 + AC ⑧ 语义收窄 + smoke 不覆盖本树** |
| **S18-1** | **NOT APPROVED** | 8 条 AC 中 ⑦⑧ **零命中**(连接口 `IDeathCandidateSource` 都不存在于生产树)、谓词**无证伪轮**、`evidence:` **指向不存在的文件** |

**为什么不给整体 `APPROVED WITH CONDITIONS`**:DoD 明写「每件过证伪轮」「AC 逐条勾选并附可复算判据」,
而 S18-1 是 sprint 内件且**两项硬判据未达** ⇒ 它是**未完成**,不是**带条件通过**。
若把它当条件项放行,等于用「条件」这个词包装一个**没做完的 L 件**(承「借绿的两种数字形态」)。
**唯一放行路径**:用户**显式裁定 S18-1 结转 sprint-19**(在 `sprint-status.yaml` 落 `deferred_to`),
则 sprint 级 verdict 可升为 **APPROVED WITH CONDITIONS**(条件仅 C-4…C-7)。

### 7.3 条件项(每条可判 PASS / FAIL)

| ID | 条件 | 判据(PASS / FAIL) | 隶属 |
|---|---|---|---|
| **C-1** | S18-1 的**去向**必须显式化 | PASS = `status: done`(AC ⑦⑧ 齐)**或** `sprint-status.yaml` 的 S18-1 新增 `deferred_to: "sprint-19"` + 用户裁定记录;FAIL = 维持 `backlog` 且无去向声明(现值) | S18-1 |
| **C-2** | S18-1 的**判据载体**订正 | PASS = `evidence:` 改为 `Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs` **且** QA plan `:35`/`:55` 就地加注路径偏离(原文不删);FAIL = 仍指向 `self_limited_predicate_test.cs`(该文件 `ls` 零命中) | S18-1 |
| **C-3** | S18-1 **谓词本体补证伪轮** | PASS = ≥3 轮变异(至少:`IsCombatant` 真值表 / 白名单五项之一 / 三条件合取之一),每轮**恰红** pre-registered 测、零附带、还原 `sha256sum -c` 成功;FAIL = 无新证伪轮 | **BLOCKING** |
| **C-4** | **重跑 `/smoke-check sprint`** 覆盖当前树 | PASS = 新报告三元组 commit = `ad7316c` 或其后代,且 coverage 表 S18-2 = `COVERED`;FAIL = 仍引 `smoke-2026-10-22.md`(其 S18-2 = `MISSING`,且预期路径 `Persistence/` 与实际 `PersistenceService/` 不符) | **BLOCKING** |
| **C-5** | 回归三元组的 **commit 字段** | PASS = 新跑(commit 字段为 SHA)**或** 落盘等价性论证:`git diff --stat 220acc3 ad7316c -- 'unity/*.cs'` = **6 files / +1004 / −2**(我已实测),即「`220acc3` + 未提交 S18-2 件」在 `.cs` 面与 `ad7316c` 等价 —— 须**明写这是论证非观测**(未提交件的内容哈希未留证);FAIL = 既不重跑也不落论证 | S18-2 |
| **C-6** | `OQ-7a-10` 就地约束 **AC ⑧** 的判据面 | PASS = GDD / QA plan 加注「AC ⑧ 的『重放等价』现只覆盖 skill-growth 维」**或**补一条其余病史行等价判据;FAIL = 仍笼统写「折叠后重放等价已通过」 | S18-2 |
| **C-7** | `PRE-S18-D`(lethal 口径)用户确认 | PASS = `status` 从「⚠️ 待用户确认」改为已裁(现实现取 `Lethality > 0`,承 `EntityDeathGates.LethalityOf` / `R1_CHECK_22` 先例);FAIL = 仍「待用户确认」而实现已自裁落码 | **S18-1/S18-2 共用** |
| **C-8** | ADR-025 §①:112 加注**状态复核** | PASS = `grep -c 'ActorClass'` 与 `grep -c 'IFoldedPredicate'` 于 `adr-025…:112` 各 ≥1,且「已落」标记与装配实况一致;FAIL = 标记「已落」而清单未含 | S18-1/S18-2 |

> **C-7 的隐患值得单说**:实现**已经**按 `Lethality > 0` 落码(S18-2 在 `FoldedPredicate.cs` 用了同一口径),
> 而台账仍写「待用户确认」⇒ 这是**「实现先于裁决」**,若用户改判 bool,改动点虽只有一处
> (`FoldedPredicate.cs` 自述)但**语义会静默翻转**(`Lethality=0` 的伤情从「非致死」变「致死」)。

---

## §8 未及核查项(禁借绿 —— 如实列出)

| # | 项 | 为什么没核 | 影响 |
|---|---|---|---|
| 1 | S18-1 「20 测」的**计数口径**(17 方法 vs 20 例) | 未读 `TestCase` 参数化展开数 | 只影响计数表述;不影响覆盖面判定(我按**方法名**映射 AC) |
| 2 | `test_ruleNine_kindIsDataDrivenNotControlFlow` 的 **IL / 反射半边** | 只读到方法名,未读实现体 | AC ⑥ 我判「部分覆盖」,不作「双证成立」主张 |
| 3 | **既有 11 条测试**(`state_machine_death_test.cs`)是否已被改写 | 未读该文件现状;QA plan 连带后果 ② 要求改写 | 若未改写,它们仍钉**改前无条件语义** ⇒ S18-1 接线时**会红**,且那是**应当红**。未核不影响 C-1…C-3 |
| 4 | 白名单**空集**边界 | `entity_death_gates_test.cs` 无对应方法名 | 我判「未见」,未判「不存在」(可能以 `TestCase` 形式并入 `whitelist_isExactlyTheFiveKeys`) |
| 5 | `HistoryFolder` 抹除面的**变异反证** | 未跑任何命令(本件只出策略) | §5.1 层 3 已列为条件;属**已知未反证面**,不作「已排除」主张 |
| 6 | ADR-025 §①:112 的**实际加注文本** | 未 grep 该行 | 已转为条件 C-8,**不代判** |
| 7 | IL2CPP 矩阵 / CI 常驻 | `TD-ci-01` 仍 EXTERNAL,与本 sprint 零交集 | 矩阵级缺口,**不参与**本判定 |
| 8 | PlayMode 全量在 `ad7316c` 上的数 | 现有 `202/202/0/0` 是 `220acc3` 上的;S18-2 零 PlayMode 新增 ⇒ 逻辑上不应变,但**未跑** | ⚠️ 不得把 `202/202/0/0` 写成「本树已核」。本件**不引**该数作判据 |

---

## §9 一句话交付

sprint-18 的两件 Logic 里,**S18-2 的谓词级判据面站得住(7/8 + 3/3 + 四条变异反证)**,
**S18-1 只完成了谓词本体、8 条 AC 中 ⑦⑧ 零命中且无证伪轮**;
**smoke 报告成于 `220acc3`,其唯一 WARNING 已被 `ad7316c` 推翻而报告本身未更新** ⇒ 当前树无有效 smoke 结论。
故 **整体 `NOT APPROVED`**;若用户显式裁定 S18-1 结转 sprint-19,则升为 **APPROVED WITH CONDITIONS(C-4…C-8)**。
