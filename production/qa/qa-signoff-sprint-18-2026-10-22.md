# QA Sign-Off Report — Sprint 18(链门清偿 G-1 · G-2)

**Date**: 2026-10-22
**Sprint**: 18
**Stage**: Pre-Production
**Review Mode**: `lean`
**QA Plan**: `production/qa/qa-plan-sprint-18-2026-10-21.md`(本 sprint 权威计划,不重写;本轮仅就地加注载体订正 + AC⑧ 判据面收窄)
**Smoke 报告**: `production/qa/smoke-2026-10-22-sprint18-final.md`(判 `ad7316c`)
**生成**: `/team-qa sprint` · Phase 2 策略由 `qa-lead` 出(草稿 `production/qa/evidence/tmp-qa-sprint-18-qa-lead.md`),Phase 3–5 由编排方执行,Phase 6 由编排方据其判定合并

**基线溯源三元组**:截至 `2026-10-22 08:59 (+0800)` · 批次 `s18-2-*`(全量)· commit `ad7316c`

---

> ## 🔁🔁 2026-10-24 就地订正(原文全部保留,本节为**总纲**,逐条订正见各节末尾)
>
> **本件写于 2026-10-22 08:59,判的是 `ad7316c`。此后同日发生两件改变判定的事:**
>
> | # | 事件 | 时点 | 对本件的影响 |
> |---|---|---|---|
> | ① | **用户改判:S18-1 拉回 sprint-18**(原「结转 sprint-19」作废) | 2026-10-22 同日 | §二 / §四 C-1 / §六 的 `DEFERRED → sprint-19` **全部失效** |
> | ② | S18-1 的 AC ⑥⑦⑧ 落地(`5f40b59` / `7014630` / `3bb2df8` / `4e005bd`) | 10-22 13:55–16:04 | §三 D-1 / D-2 的「被测物不存在」「零证伪轮」**已过期** |
>
> **⚠️ 本件正文的判定保持原样(不删不改),但下列各处已不成立 —— 阅读时须先看本节。**
>
> ### 已失效的判定(逐条索引)
>
> | 位置 | 原文判定 | 现状 | 订正依据 |
> |---|---|---|---|
> | §二 表 S18-1 行 | `DEFERRED → sprint-19` | ❌ **失效** | 用户改判拉回;AC ⑥⑦⑧ 全部落地 |
> | §四 C-1 | `deferred_to: "sprint-19"` ✅ 结清 | ❌ **失效** | 同上;`sprint-status.yaml` 已改回 sprint-18 |
> | §四 C-3 | ⏭ 转 sprint-19 | ✅ **已执行** | 四轮变异 M1–M4,预测红集 = 实得(见 `sprint-18.md` §九.6) |
> | §三 D-1 | AC ⑦⑧ 被测物**不存在** | ❌ **已过期** | 生产树实测:`IDeathCandidateSource` 3 处 / `PlayerDeathWarranted` 2 处 / `WriterExistenceGate` 两条豁免已撤 |
> | §三 D-2 | 谓词本体**零证伪轮** | ✅ **已闭合** | M1–M4 四轮,`sprint-18.md:225-240` |
> | §三 D-5 | 折叠抹除面**零变异反证** | ✅ **已闭合** | 两轮变异,`sprint-18.md:211-219` |
> | §六 逐件 S18-1 | `DEFERRED → sprint-19` | ❌ **失效** | 同 §二 |
> | §六 整体 | 放行依据 = 结转裁定 | ❌ **失效** | 该裁定已作废 |
> | §六 下一步 | 「sprint-19 须携带四条入口判据」 | ❌ **失效** | 四条已在 sprint-18 内执行完 |
> | §八 用户裁定 | S18-1 结转 sprint-19 | ❌ **失效** | 已改判 |
>
> ### ⚠️ 仍然成立、且**不得**被上述订正冲掉的缺口
>
> | ID | 缺口 | 现状 |
> |---|---|---|
> | **D-3** | **AC ⑤ 是自造断言顶替** | ⚠️ **仍未解** —— 见 §三 D-3 订正 |
> | **D-4** | `OQ-7a-10` 未裁 | 🔁 **2026-10-24 已关闭**(用户裁定 A)⇒ **不再属于「仍成立」的缺口**。原文「仍未裁」作废,保留。判据见 §三 D-4 专段的 🔁 块(`~~删除线~~` 格式约定,非散文关键词) |
> | **§七 #1** | S18-1 测试方法计数口径 | ⚠️ 仍未核 |
> | **§七 #3** | 既有 11 条 `state_machine_death_test.cs` 是否改写 | ⚠️ 仍未核(实测该文件 12 个 `[Test]`,零 `SelfLimited` / 零 `EntityDeathGates` 引用 ⇒ **未被改写**) |
> | **§七 #5** | `HistoryFolder` 抹除面变异反证 | ✅ **已做**(两轮,见 D-5 行) |
>
> ### 回归证书的处置
>
> §二「回归证书」表的两个三元组(`4418/4382` @ `ad7316c` · `202/202` @ `ad7316c`)**对 `ad7316c` 仍为真**,
> 且产物仍在盘上(`unity/Logs/s18-2-*.xml`,实测存在)。
> ⚠️ 但 `ad7316c` **不覆盖** S18-1 收口后的代码 ⇒ **不得**当作 sprint-18 收口三元组。
> **真正的收口三元组** = `4475 / 4439 / 0 / 36` + `202 / 202 / 0 / 0` @ **`4e005bd` 干净树**
> (2026-10-24 00:56–01:00 实测),见 `production/sprint-status.yaml` `baseline.closeout_regression`。

---

## 一、范围

sprint-18 = **3 个工作项,零 story**(承债册 §六用户裁定:八道硬门的清偿载体 = TD 工作项,不立 story)。

| 项 | 类型 | 实现载体 |
|---|---|---|
| **S18-1** 逐实体判死谓词及依赖面 | Logic | `Sim.Contracts/ActorClass.cs` · `Sim/EntityDeathGates.cs` · `Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs` |
| **S18-2** 四条件折叠谓词 + 折叠行产出 | Logic | `Sim.Contracts/IFoldedPredicate.cs` · `Sim/FoldedPredicate.cs` · `Sim/FoldRow.cs` · `Sim/HistoryFolder.cs` · `Sim.Codec/SaveService.cs` · `Tests/EditMode/PersistenceService/folded_predicate_test.cs` |
| **S18-3** 台账回写 | Config/Data | 路线图 / 债册 / epic 卡面(纸面) |

---

## 二、测试覆盖

| 工作项 | 类型 | 自动测试 | 人工 QA | 结果 |
|---|---|---|---|---|
| S18-1 | Logic | 谓词面有测(17 个 test 方法);**AC ⑦⑧ 零证据** | — | **DEFERRED → sprint-19** |
| S18-2 | Logic | 29 测 + 证伪轮 4/4 | — | **PASS(带条件)** |
| S18-3 | Config/Data | 五条判据可复算 | — | **PASS** |

> 🔁 **2026-10-24 订正(原文不删)**:S18-1 行**已失效**。
> ① 用户 2026-10-22 同日改判 **S18-1 拉回 sprint-18**,「DEFERRED → sprint-19」作废;
> ② AC ⑦⑧ 的「零证据」已过期 —— `5f40b59`(13:55)落了 `IDeathCandidateSource` +
> `DeathCandidateConsumer` + `PlayerDiedWriter` + `InjuryStateChangedWriter` + 测试
> `death_candidate_writers_test.cs`(13 条);AC ⑥ 由 `7014630`(13:55)的 `RuleNineGate` 兑现。
> ③ **但 AC ⑤ 仍未解**(见 §三 D-3 订正),S18-1 不得整体判 PASS。
> **现判定 = `APPROVED WITH CONDITIONS`(条件 = AC ⑤ 的 D-3 缺口)。**

**人工 QA 量 = 0 项 / 0 人时**。QA 计划判「本 sprint 零人工验收项」,三件全在自动化侧或纸面判据面。
**Phase 4(用例编写)/ Phase 5(人工执行)无对象,不排班次。**

### 回归证书

| 套件 | 结果 | 截至(+0800) | 批次 | commit |
|---|---|---|---|---|
| EditMode | `4418 / 4382 / 0 / 36` | 2026-10-22 08:59 | `s18-2-editmode-commit-20261022` | `ad7316c` 干净树 |
| PlayMode | `202 / 202 / 0 / 0` | 2026-10-22 08:55 | `s18-2-playmode-final-20261022` | `ad7316c` 干净树 |

**对账**:基线(本 sprint 自身)= EditMode `4389 / 4353 / 0 / 36` @ `s18-editmode-final-20261022` · `220acc3`;
`Δtotal = Δpassed = 新增条数 = 29`;全名集差集 EditMode = **新增 29 / 消失 0** · PlayMode = **新增 0 / 消失 0**;
`error CS` = 0 · failed = 0 · skipped 36(与基线同名同数)。
**S18-2 收口后对测试文件做了一次纯注释编辑(C-6),已重跑**该 fixture:`29 / 29 / 0 / 0`(start `2026-10-22T01:12:10Z`,批次 `s18-2-c6-recheck-20261022`)。

---

## 三、缺陷

**无 S1–S4 缺陷录入**(未发现功能缺陷)。

**未结缺口(非缺陷,属未完成 / 未裁)**:

| # | 缺口 | 证据 |
|---|---|---|
| D-1 | **S18-1 AC ⑦⑧ 的被测物不存在** —— `IDeathCandidateSource` / `PlayerDeathWarranted` 生产树 **0 命中**;`WriterExistenceGate.cs:98-102` 的 `PlayerDied` / `InjuryStateChanged` 仍挂「写者未落」 | grep 生产树 |
| D-2 | **S18-1 谓词本体无证伪轮** —— 已跑的 `R1_CHECK_31` 那轮守的是 `kind` 零值,与本件三条断言零关系 | QA plan DoD「每件过证伪轮」 |
| D-3 | **S18-1 AC ⑤ 是自造断言顶替** —— 用新文件自造期望,不是计划要求的「跑 `state_machine_death_test.cs` 既有 11 条对照」 | 本仓已三发此型 |
| D-4 | ~~**`OQ-7a-10` 未裁**~~ 🔁 **2026-10-24 已裁(裁定 A:条件① 由 ∃ 收紧为 ∀)** —— 「条件① 病人级存在性查询 vs 折叠行逐病种产」之缝;实现取权威字面(抹除全部减 `SkillGrown`) | `persistence-service.md:639` |
| D-5 | **S18-2 证伪轮作用域受限** —— 四轮变异里**没有一条**打 `HistoryFolder` 的「抹哪些行」,而那正是实现偏离所在 ⇒ 只可主张「条件①–④各由变异反证」,**不可**主张「折叠产出面已验证」 | qa-lead §5 独立核 |

---

> ## 🔁 2026-10-24 D-1…D-5 逐条订正(原文不删)
>
> | # | 原判 | 现状 | 复算判据(实跑) |
> |---|---|---|---|
> | **D-1** | AC ⑦⑧ 被测物**不存在** | ✅ **已过期** | 生产树实测:`IDeathCandidateSource` **12 处**(含注释)/ **4 处代码行**(`:38` 接口声明 · `:64` 参数类型 · `DeathCandidateConsumer.cs:42` 实现 · `PlayerDiedWriter.cs` 注释);`PlayerDeathWarranted` **8 处**(2 处代码 + 6 处注释);`WriterExistenceGate.cs` 内 `PlayerDied` / `InjuryStateChanged` 两条豁免**已撤**(该文件现含「✅ 2026-10-22 撤 …豁免」注) |
> | **D-2** | 谓词本体**零证伪轮** | ✅ **已闭合** | 四轮变异 M1–M4,预测红集 = 实得(10 / 1 / 1 / 2),还原 SHA-256 逐字节。见 `sprint-18.md:225-240` |
> | **D-3** | AC ⑤ 是自造断言顶替 | ⚠️ **仍未解,且比原判更严重** —— 见下方专段 | 见下 |
> | **D-4** | `OQ-7a-10` 未裁 | 🔁 **2026-10-24 已关闭** —— 用户裁定 **A**:条件 ① 由 ∃ 收紧为 ∀,空集 `D_p = ∅` ⇒ false;落 `Sim/FoldedPredicate.cs`。原文「仍未裁」作废,保留 | 见下方专段 🔁 块(判据改用 `~~删除线~~` 格式约定 —— 散文关键词实测假阳性 6 行) |
> | **D-5** | 折叠抹除面**零变异反证** | ✅ **已闭合** | 两轮变异(`sprint-18.md:211-219`);⚠️ ~~但 `OQ-7a-10` 未裁~~ 🔁 **该前提 2026-10-24 已消** ⇒ 现可主张「抹除作用域有两轮独立反证 **且** 口径已由裁定 A 钉住」;仍**不可**主张「折叠产出面整体已验证」(另有两条未满足:折叠未接存档路径 · 产出面变异反证) |
>
> ### ⚠️ D-3 专段 —— 本条**未被任何后续提交解决**,且比原判更严重
>
> **原判**(本件 §三 D-3):「S18-1 AC ⑤ 是自造断言顶替 —— 用新文件自造期望,
> 不是计划要求的『跑 `state_machine_death_test.cs` 既有 11 条对照』」。
>
> **2026-10-24 实测,原判不但成立,还低估了问题**:
>
> ① **`state_machine_death_test.cs` 从未被改写**:
> ```
> $ grep -c "\[Test\]" unity/Assets/Tests/EditMode/DiseaseSimulation/state_machine_death_test.cs
> 12
> $ grep -c "SelfLimited\|EntityDeathGates" unity/Assets/Tests/EditMode/DiseaseSimulation/state_machine_death_test.cs
> 0
> ```
> ⇒ 既有 12 条测试**零** `SelfLimited` / `EntityDeathGates` 引用,计划要求的「对照」从未发生。
>
> ② **AC ⑤ 的字面判据实测未满足**。Tasks 表(`sprint-18.md:95`)AC ⑤ 原文:
> > ⑤ `DeathCandidate` 产出路径**有生产调用点**,`WriterExistenceGate` 两条从「未落」转为「已落」
>
> 实测:
> ```
> $ grep -rn "new DeathCandidateConsumer\|new PlayerDiedWriter\|new InjuryStateChangedWriter" \
>     unity/Assets --include='*.cs' | grep -v '/Tests/'
> unity/Assets/Sim/EntityDeathGates.cs:50://  · 但 `InjuryStateChangedWriter` **自身零生产构造点** …
> ```
> ⇒ **`DeathCandidate` 产出路径的生产调用点 = 0**。AC ⑤ 的前半句**字面未满足**;
> 后半句(`WriterExistenceGate` 两条撤表)**已满足**。
>
> ③ **`sprint-18.md` 的 DoD 表整行漏掉了 AC ⑤**。该表 7 行覆盖
> 「Tasks 表 ①–④ + 签核件 ⑥⑦⑧」,**零行对应 Tasks 表 ⑤**:
> ```
> $ sed -n '144,200p' production/sprints/sprint-18.md | grep -c "Tasks 表 ⑤"
> 0
> ```
> ⇒ AC ⑤ **既未勾选、也未登记为不勾**,是**静默漏项**。这与 §九.5 缺口 ③ 记的
> 「DoD 11 条全未勾」是不同形态:那次是「没勾」,这次是**表里没有这一行**。
>
> **⇒ 订正判定**:S18-1 **不得**判 PASS / APPROVED。AC ⑤ 字面未满足,应登记为
> **`APPROVED WITH CONDITIONS`**,条件 = 「`DeathCandidate` 产出路径的生产调用点」,
> 归 9 / 29 的装配轮(`DeathCandidateConsumer.cs:27` 自陈)。
>
> ### D-4 专段 —— `OQ-7a-10` 的「未裁」怎么算(判据须避开自指)
>
> > 🔁🔁 **2026-10-24 本条已关闭 —— 本专段降级为「判据方法学的历史记录」,原文不删**
> >
> > **现判定**:`OQ-7a-10` = **✅ 已裁(2026-10-24 用户裁定 A)** —— 条件 ① 由 ∃ 收紧为
> > **∀**(`终态(p) = ∀ d ∈ D_p : ∃ PatientOutcomeChanged(p, d)`);空集 `D_p = ∅` ⇒ false。
> > **判据**(可当场跑):
> > ```
> > $ grep -n 'OQ-7a-10' design/gdd/persistence-service.md | grep -oE '\| ~~\*\*`OQ-7a-10`\*\*~~|\| \*\*`OQ-7a-10`\*\*'
> > | ~~**`OQ-7a-10`**~~
> > ```
> > 有 `~~删除线~~` = 已裁。**三向对照**:`OQ-7a-9` 有删除线(已裁)· `OQ-7a-7` 无(未裁)·
> > `OQ-7a-10` 有(已裁)。
> >
> > **⚠️ 本专段下方的判据方法本身被证伪了一半** —— 它主张「正确判据 = 找『裁定值』而非
> > 『裁定』二字」,但仍用**散文关键词** `grep -iE "已裁|裁定|结案|closed|resolved"` 落地,
> > 2026-10-24 实测**假阳性 6 行**:3 行撞「结案**判据**」(那恰是未裁的表述),3 行是
> > 「**裁定** + 反证」待办式。⇒ **散文关键词判「某 OQ 是否已裁」不可靠**(本仓第三次踩同型)。
> > **现判据改用格式约定**(`~~删除线~~`),见上。下方原文保留,作为「为什么不能用散文」的实例。
> >
> > **关闭不解除 `TD-persist-01`** —— 该债另有两条判据未满足(折叠接入存档路径 ·
> > 产出面变异反证)。
>
> **本条的判据不能写成 `grep OQ-7a-10 | grep 裁定`** —— 实测该式命中 7 处,
> 但逐处看**没有一处是裁定记录**:
>
> | 命中位置 | 实际内容 | 是否裁定 |
> |---|---|---|
> | `persistence-service.md:639` | 该 OQ 的**登记行本身**(问题描述) | ❌ 是问题,不是答案 |
> | `sprint-18.md:388` | 「结案判据 = 折叠接入存档路径 + **`OQ-7a-10` 裁定** + 产出面变异反证」 | ❌ 是**待办条件**,不是裁定 |
> | `sprint-status*.yaml`(2 处) | 四条件说明,提及该 OQ | ❌ 提及,非裁定 |
> | `qa-signoff` 本文件 3 处 | **本件自己写的**「仍未裁」 | ❌ **自指污染** |
>
> **正确判据 = 找「裁定值」而非「裁定」二字**:
> ```bash
> # 该 OQ 若已裁,应有「裁定 = / 取值 = / resolved:」形态的记录,且不在本件内
> grep -rn "OQ-7a-10" --include='*.md' --include='*.yaml' . | grep -v session-logs | grep -v "qa-signoff"
> ```
> **实测:30 处命中,按文件分组(可复算)** ——
>
> ```
> $ grep -rn "OQ-7a-10" --include='*.md' --include='*.yaml' . | grep -v session-logs | grep -v "qa-signoff" | cut -d: -f1 | sort | uniq -c | sort -rn
>   5  production/qa/evidence/tmp-qa-sprint-18-qa-lead.md
>   4  production/sprints/sprint-18.md
>   4  production/qa/evidence/tmp-review-s18-qa-tester.md
>   3  production/session-state/active.md
>   2  production/sprint-status.yaml
>   2  production/retrospectives/retro-sprint-18-2026-10-22.md
>   2  production/qa/qa-plan-sprint-18-2026-10-21.md
>   2  production/qa/evidence/review-sprint18-implementation-2026-10-22.md
>   2  production/milestones/p0-remaining-roadmap-2026-10-20.md
>   1  production/sprint-status-sprint18-archive.yaml
>   1  production/qa/evidence/tmp-review-s18-unity-specialist.md
>   1  docs/tech-debt-register.md
>   1  design/gdd/persistence-service.md
> ```
>
> **13 个文件、30 处,逐文件性质**:
>
> | 性质 | 文件 | 是否裁定 |
> |---|---|---|
> | **问题定义**(OQ 登记行) | `persistence-service.md:639` | ❌ 是问题 |
> | **现状陈述**(「未裁 / 仍未解」) | `sprint-18.md`(4)· `retro-sprint-18`(2)· `active.md`(3)· `roadmap`(2) | ❌ 陈述缺口 |
> | **待办条件**(「结案判据 = … 该 OQ 裁定」) | `tech-debt-register.md:520` · `sprint-18.md` 内一处 | ❌ 是前置,非答案 |
> | **评审 / QA 草稿**(GAP / 条件项标注) | `tmp-qa-*`(6)· `tmp-review-*`(5)· `review-sprint18-implementation`(2) | ❌ 分析,非裁定 |
> | **门面提及**(四条件说明 / 归档) | `sprint-status.yaml`(2)· `archive.yaml`(1)· `qa-plan`(2) | ❌ 提及 |
> | **裁定取值记录** | **无** | — |
>
> ⇒ **未裁**,结论成立。**本件自己是第 14 个文件,已被 `grep -v "qa-signoff"` 排除** ——
> 若不排除,本件写的「仍未裁」会自我印证。
> ⚠️ 这条教训本身值得记:凡「某 OQ 是否已裁」的 grep 判据,**必须排除本件自指**,
> 否则自己写的「未裁」会把判据变成自我印证。

---

## 四、条件项结账(「已落 / 已证」双列)

| ID | 条件 | 已落(Landed) | 已证(Proven-by) | 状态 |
|---|---|---|---|---|
| **C-1** | S18-1 去向显式化 | `sprint-status.yaml` S18-1 加 `deferred_to: "sprint-19"` + `deferred_ruling` | `yaml.safe_load` 解析通过,S18-1 `status=backlog` 且 `deferred_to=sprint-19` | ✅ 结清 |
| **C-2** | S18-1 载体订正 | `evidence:` → `entity_death_gates_test.cs`;QA plan `:35` / `:55` / `:353` 三处加注 | `ls` 只有实际载体存在;`grep -c self_limited_predicate_test` = 4(全在订正注内) | ✅ 结清 |
| **C-3** | S18-1 谓词证伪轮 ≥3 | ⏭ **随 C-1 转为 sprint-19 入口判据** | — | ⏭ 转 sprint-19 |
| **C-4** | 在 `ad7316c`+ 重跑 smoke | `production/qa/smoke-2026-10-22-sprint18-final.md` | 该报告 coverage 表 S18-1/S18-2 = **COVERED**;Verdict = PASS WITH WARNINGS | ✅ 结清 |
| **C-5** | 回归三元组补 commit | EditMode / PlayMode 均在 `ad7316c` 真跑 | start-time `00:57:37Z` / `00:55:54Z` 均晚于 commit 时刻 `00:32:50Z` | ✅ 结清 |
| **C-6** | AC ⑧ 判据面写明只覆盖 skill-growth 维 | `folded_predicate_test.cs` 头注 + QA plan S18-2 「What to test」第 8 条下的收窄注 | 两处文本同款声明 + 该文件只有一条重放断言(`test_fold_skillGrownReplayIsBitIdentical`);改后重跑 `29/29` | ✅ 结清 |
| **C-7** | `PRE-S18-D` `lethal` 口径确认 | `sprint-status.yaml` PRE-S18-D → `✅ 已结 2026-10-22 · 口径 = Lethality > 0` | 本报告 §五 如实登记「实现先于裁决」的事实与失效条件 | ✅ 结清(见 §五 警示) |
| **C-8** | ADR-025 §①:112 加注复核 | `adr-025:112` 三项成员(`ActorClass` / `IFoldedPredicate` / `PatientOutcome`)全在行内;`PatientOutcome` 补「✅ 2026-10-22 已落」字样 | `grep` 三项各命中 ≥2 次;「已落」字样缺口的订正可复读 | ✅ 结清 |

**未结条件 = 0。** C-3 不是「消失」,是**转移**:它成为 sprint-19 的入口判据(见 §六)。

> 🔁 **2026-10-24 订正(原文不删)**:
> - **C-1 已失效** —— 其判据「`sprint-status.yaml` S18-1 `deferred_to: "sprint-19"`」在用户改判后
>   已不再成立。**该行的「✅ 结清」是当时为真的历史记录,现状为「已作废」。**
> - **C-3 已执行完毕** —— 不再是「转 sprint-19」。四轮变异 M1–M4 已跑,
>   预测红集由论证先行给出且与实得逐条相同(M1 = 10 / M2 = 1 / M3 = 1 / M4 = 2),
>   还原判据 = SHA-256 逐字节(`8aca6a6d…`)。见 `sprint-18.md:225-240`。
> - ⚠️ **「未结条件 = 0」这句现在不成立** —— AC ⑤ 的 D-3 缺口未解(见 §三 D-3 专段)。

---

## 五、两处必须写明的警示(禁借绿)

### ① C-7 的结案形式 = 实现先于裁决

`PRE-S18-D` 原文写「**待用户确认**」,而实施轮(`fc1705d` / `ad7316c`)**已经**按 `Lethality > 0` 落码。
本轮结案的口径 = **按现行实现确认**,不是「先裁后写」。
**若将来改判为 bool 字段**,本条立即失效,改动落点 = `EntityDeathGates.LethalityOf` +
`FoldedPredicate.cs:123` 的 `entry.Lethality <= 0` + 22 处夹具 + `R1-06` 校验。

### ② S18-2 的绿是「四条件各自成立」,不是「折叠全对」

证伪轮四轮分别打:定义域守卫 / `Lethality <= 0` 半条件 / 重复终态抛错 / `SkillGrown` 豁免。
**「抹除作用域」无任何变异反证** —— 而实现偏离恰在此处(D-5 + D-4)。
⇒ 签核接受的是 **谓词与产行**,**不接受**「折叠产出面已验证」。

> 🔁 **2026-10-24 订正(原文不删)**:「抹除作用域**无任何**变异反证」**已过期** ——
> `sprint-18.md:211-219` 记有**两轮**针对 `HistoryFolder.Erase` 抹除作用域的变异,故 D-5 已闭合。
> ⚠️ **但本节的结论不变,且理由换了**:
> - 原理由「没有反证」⇒ **失效**
> - 现理由「**`OQ-7a-10` 仍未裁**」⇒ 🔁 **2026-10-24 该理由亦失效**(裁定 A 已落)。
>   抹除作用域的**正确性口径已定**:病人级存在性 = `∀ d ∈ D_p`。两轮变异现可解读为
>   「在已裁口径下,抹除作用域被反证钉住」。
> ⇒ **仍不接受「折叠产出面已验证」**,但**理由再换一次**:现理由是
> **`TD-persist-01` 另两条判据未满足** —— ① 折叠未接入任何存档路径(`SaveService` 的
> `IFoldedPredicate` 形参默认 `null`);② 产出面「抹哪些行」的变异反证仍缺(D-5 的两轮
> 打的是谓词与条件,不是 `Erase` 的取舍面)。**不得**再用「口径未定」当理由。

---

## 六、判定

### 逐件

| 件 | 判定 |
|---|---|
| S18-3 | **APPROVED** |
| S18-2 | **APPROVED WITH CONDITIONS**(C-6 已结;C-3 转移) |
| S18-1 | **DEFERRED → sprint-19**(用户 2026-10-22 裁定) |

> 🔁🔁 **2026-10-24 订正(原文不删)—— 本节判定已整体失效,以下为现判定。**
>
> | 件 | 原判 | **现判定** | 依据 |
> |---|---|---|---|
> | S18-3 | APPROVED | **APPROVED**(不变) | 纸面判据五条仍可复算 |
> | S18-2 | APPROVED WITH CONDITIONS | **APPROVED WITH CONDITIONS**(条件不变) | ~~`OQ-7a-10` 仍未裁(D-4)~~ 🔁 **2026-10-24 D-4 已关闭** ⇒ 条件改由 `TD-persist-01` 的两条未满足判据承担(折叠未接存档路径 · 产出面变异反证),**判定本身不变** |
> | S18-1 | DEFERRED → sprint-19 | **APPROVED WITH CONDITIONS** | 原裁定被用户同日改判作废;AC ⑥⑦⑧ 已落地、D-2 已闭合;**但 AC ⑤ 字面未满足**(见 §三 D-3 专段)⇒ 不得判 APPROVED |
>
> **整体:仍为 `APPROVED WITH CONDITIONS`,但放行依据变了** ——
> 原依据「用户裁定 S18-1 结转 sprint-19」**已作废**;现依据 = 「S18-1 在 sprint-18 内落地,
> 残留一项 AC ⑤ 生产调用点缺口,归 9 / 29 装配轮」。
>
> **⚠️ 现存的未结条件(取代原文四条入口判据)**:
>
> | # | 条件 | 状态 |
> |---|---|---|
> | 1 | ~~C-3 证伪轮 ≥3~~ | ✅ **已结**(M1–M4 四轮) |
> | 2 | ~~AC ⑦ `IDeathCandidateSource` + 29 侧注入点~~ | ✅ **已落**(`5f40b59`);⚠️ 但**生产树零构造点** —— 见条件 5 |
> | 3 | ~~AC ⑧ 两条写者 + 撤 `WriterExistenceGate` 豁免~~ | ✅ **已结**(`5f40b59` 同批撤表) |
> | 4 | ~~AC ③ 数值/登记裁定~~ | ✅ **已解**(白名单五项 `4e005bd` 落数据行;三常量 `3bb2df8` 登记) |
> | **5** | **AC ⑤ `DeathCandidate` 产出路径有生产调用点** | ⚠️ **未满足**(实测 0 处)—— 归 9 / 29 装配轮 |
> | **6** | ~~`OQ-7a-10` 裁定(D-4)~~ | ✅ **已结**(2026-10-24 用户裁定 A:条件① ∃ → ∀,空集 ⇒ false) |
>
> ### 下一步(订正后)
>
> - ~~`/code-review`(sprint DoD 仍欠)~~ → ✅ **2026-10-24 已补**:
>   `production/qa/evidence/review-sprint18-postcloseout-8files-2026-10-24.md`
>   (补评原评审件漏掉的 8 件,1981 行;Verdict = `APPROVED WITH SUGGESTIONS`)
> - ~~`sprint-status` 归档~~ → ✅ 已于 2026-10-22 归档(`08049fb`)
> - ~~sprint-19 规划时,S18-1 须携带上述四条入口判据~~ → ❌ **已失效**(S18-1 已拉回并落地)
>   ⇒ 改为:**条件 5 / 6 须在 sprint-19 规划时承接**,不得以「S18-1 已 done」当已结
> - **收口回归三元组** = `4475 / 4439 / 0 / 36` + `202 / 202 / 0 / 0` @ `4e005bd` 干净树
>   (2026-10-24 00:56–01:00 实测),见 `production/sprint-status.yaml` `baseline.closeout_regression`
- **不得**把本 sprint 的绿引作 M3 进展(~~清债方向,分母 73 → 71~~)
  🔁 **2026-10-22 收口订正(原文不删)**:`73 → 71` **作废**。实测两门皆未清(G-1 结转 sprint-19 /
  G-2 不主张结案),且 `TD-clinic-18` 立债 ⇒ 未闭 P0 面 **`73 → 74`**。连双结案假设下也只有 **72**,
  **`71` 从未成立**。见 `production/sprints/sprint-18.md` §九.1。

---

## 七、未及核查项(禁借绿)

| # | 项 | 状态 |
|---|---|---|
| 1 | S18-1 测试方法计数口径(17 个方法 vs 展开测数,含 `TestCase`) | 未核 |

| 2 | S18-1 AC ⑥「规则九」的 **IL / 反射半边**(源码 grep 已合规) | 🔁 **2026-10-22 已核** —— 见下方订正 |
| 3 | 既有 11 条 `state_machine_death_test.cs` 是否已改写(未改则接线时会红,且**应当红**) | 🔁 **2026-10-24 已核** —— **未被改写**。实测该文件 `[Test]` = **12 个**(非 11),`SelfLimited` 命中 **0**、`EntityDeathGates` 命中 **0** ⇒ 计划要求的「跑既有测试对照」从未发生,这正是 D-3 的实证 |
| 4 | 白名单**空集**边界(判「未见对应测」,非「不存在」) | 判未见 |
| 5 | `HistoryFolder` 抹除面的变异反证 | 🔁 **2026-10-24 已做** —— 两轮变异(`sprint-18.md:211-219`)。🔁 **同日稍后 `OQ-7a-10` 已裁**(裁定 A)⇒ 原「仍未裁」保留条件解除;但仍**不可**主张「折叠产出面已验证」(`TD-persist-01` 另两条未满足:折叠未接存档路径 · `Erase` 取舍面的变异反证) |
| 6 | IL2CPP 矩阵 / CI 常驻 job(`TD-ci-01` EXTERNAL) | 与本 sprint 零交集 |
| 7 | 人工可玩性 smoke(A/B/C 三节) | 按 QA 计划 N/A,依据已核(生产构造点 = 0) |

> 🔁 **2026-10-22 第 2 项订正(原文不删)**:本表原记「IL / 反射半边未核」。
> 该缺口**已关闭** —— 新立 `unity/Assets/Editor.Tools.Gates/RuleNineGate.cs`,判据形态按
> `qa-plan-sprint-18-2026-10-21.md:80` 的**源码 + IL 双证**(原文:「单靠 grep 可被换写法绕过」)。
> 可复算判据:
> - **源码半** `RuleNineGate.CheckSource(".")` —— S1 `switch`(需 `case DiseaseKind.X` 作类型证据)/
>   S2 多语句 `if` / S3 三元 / S4 具体病种名硬编码
> - **IL 半** `RuleNineGate.CheckIl(<Sim.dll>)` —— I1 `switch` 按 `DiseaseKind` 字段分派 /
>   I2 同方法 ≥3 个 `DiseaseKind` 常量 / I3 `DiseaseKey` 与 `ldstr` 常量相等比较
> - **门真会红**(非空转):`RuleNineGateTest` **16 条** —— S1–S4 四种**源码**注入各自转红 ·
>   **I1/I2/I3 三条 IL 注入**各自转红(夹具写成测试源码里的真方法,让编译器生成 IL,
>   扫 `Sim.Contracts.Tests.dll` —— 夹具不进 `Assets/Sim` ⇒ 源码半对它失明而 IL 半抓住,
>   这正是 IL 半独立价值的直接证明)· `EventKind` / `ClinicRoomClauseKind` 同名尾巴**不得**误报 ·
>   注释不得触发 · 锚点活体守卫(`FieldType == DiseaseKind`)
> - **构建期强制点**:`AssemblyGates.RunMenu` + `BuildGate.OnPreprocessBuild` 两处已接,
>   `gate_wiring_manifest_test` 的入口表与类级表同批登记
> - **探针轮**(CLAUDE.md §构建前门纪律):无条件 `[PROBE-RuleNine]` ⇒ 真构建 `Result: Failure`,
>   栈帧 `AssemblyGates.cs:1275` `Debug.LogError`,`BuildFailedException` + utp 在 `Preprocess Player` 即止;
>   还原后 `sha256sum -c` 逐字节相同;干净轮 `Result: Success` 且零 `[RuleNine` / `[fs-write` / `error CS`。

---

## 八、溯源

| 项 | 出处 |
|---|---|
| 件集 | `production/sprints/sprint-18.md` · `production/sprint-status.yaml` `work_items` |
| 判据面 | `production/qa/qa-plan-sprint-18-2026-10-21.md` |
| Smoke | `production/qa/smoke-2026-10-22-sprint18-final.md` |
| 回归证书 | `unity/Logs/s18-2-editmode-commit-20261022.{xml,log}` · `unity/Logs/s18-2-playmode-final-20261022.{xml,log}` · `unity/Logs/s18-2-c6-recheck-20261022.{xml,log}` |
| 策略原件 | `production/qa/evidence/tmp-qa-sprint-18-qa-lead.md`(qa-lead Phase 2 草稿,**保留不删** —— 本报告 §三 D-5 / §四 的判定均可回溯到它) |
| 用户裁定 | S18-1 结转 sprint-19(2026-10-22)· PRE-S18-D 口径确认(2026-10-22) |

> 🔁 **2026-10-24 订正(原文不删)**:
> - 「S18-1 结转 sprint-19」**已作废** —— 用户同日改判 **拉回 sprint-18**。
>   裁定沿革见 `production/sprints/sprint-18.md` §九.6。
> - PRE-S18-D 口径确认(`Lethality > 0`)**仍有效**。
> - **新增溯源件**(2026-10-24):`production/qa/evidence/review-sprint18-postcloseout-8files-2026-10-24.md`
>   (补评 8 件盲区)· `production/sprint-status.yaml` `baseline.closeout_regression`(收口三元组)。
