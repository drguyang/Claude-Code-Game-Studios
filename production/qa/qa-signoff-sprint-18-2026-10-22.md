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
| D-4 | **`OQ-7a-10` 未裁** —— 「条件① 病人级存在性查询 vs 折叠行逐病种产」之缝;实现取权威字面(抹除全部减 `SkillGrown`) | `persistence-service.md:639` |
| D-5 | **S18-2 证伪轮作用域受限** —— 四轮变异里**没有一条**打 `HistoryFolder` 的「抹哪些行」,而那正是实现偏离所在 ⇒ 只可主张「条件①–④各由变异反证」,**不可**主张「折叠产出面已验证」 | qa-lead §5 独立核 |

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

---

## 六、判定

### 逐件

| 件 | 判定 |
|---|---|
| S18-3 | **APPROVED** |
| S18-2 | **APPROVED WITH CONDITIONS**(C-6 已结;C-3 转移) |
| S18-1 | **DEFERRED → sprint-19**(用户 2026-10-22 裁定) |

### 整体:**APPROVED WITH CONDITIONS**

放行依据 = 用户 **2026-10-22 显式裁定 S18-1 结转 sprint-19**(qa-lead Phase 2 给出的唯一放行路径)。
S18-1 的三个缺口(AC ⑦⑧ / 证伪轮 / 数据前置)**转为 sprint-19 的入口判据,不是消失**:

1. **C-3** —— 谓词补证伪轮 ≥3(`IsCombatant` 真值表 / 白名单一项 / 三条件合取之一,各恰红零附带)
2. **AC ⑦** —— `IDeathCandidateSource` + 29 侧注入点
3. **AC ⑧** —— `PlayerDied` / `InjuryStateChanged` 两条写者,落地同批撤 `WriterExistenceGate` 豁免表
4. **AC ③** —— 仍被 `PRE-S18-A`(白名单五项无数据行)· `PRE-S18-B`(F4 三常量未登记为 registry 名块)卡住,**须数值/登记裁定**

### 下一步

- `/code-review`(sprint DoD 仍欠)
- `sprint-status` 归档 → `sprint-status-sprint18-archive.yaml`(SHA-256 逐字节)
- sprint-19 规划时,S18-1 须携带上述四条入口判据,**不得**以「已结转」当已结
- **不得**把本 sprint 的绿引作 M3 进展(~~清债方向,分母 73 → 71~~)
  🔁 **2026-10-22 收口订正(原文不删)**:`73 → 71` **作废**。实测两门皆未清(G-1 结转 sprint-19 /
  G-2 不主张结案),且 `TD-clinic-18` 立债 ⇒ 未闭 P0 面 **`73 → 74`**。连双结案假设下也只有 **72**,
  **`71` 从未成立**。见 `production/sprints/sprint-18.md` §九.1。

---

## 七、未及核查项(禁借绿)

| # | 项 | 状态 |
|---|---|---|
| 1 | S18-1 测试方法计数口径(17 个方法 vs 展开测数,含 `TestCase`) | 未核 |
| 2 | S18-1 AC ⑥「规则九」的 **IL / 反射半边**(源码 grep 已合规) | 未核 |
| 3 | 既有 11 条 `state_machine_death_test.cs` 是否已改写(未改则接线时会红,且**应当红**) | 未核 |
| 4 | 白名单**空集**边界(判「未见对应测」,非「不存在」) | 判未见 |
| 5 | `HistoryFolder` 抹除面的变异反证 | 未做(见 §五 ②) |
| 6 | IL2CPP 矩阵 / CI 常驻 job(`TD-ci-01` EXTERNAL) | 与本 sprint 零交集 |
| 7 | 人工可玩性 smoke(A/B/C 三节) | 按 QA 计划 N/A,依据已核(生产构造点 = 0) |

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
