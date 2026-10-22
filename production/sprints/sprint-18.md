# Sprint 18 — 2026-10-21 起 · **链门清偿(G-1 · G-2)**

**周期**:`2026-10-21` → **`""`**(不设结束日期口径,做完即收,**收口时补记实际收口日** —— 承 sprint-14 `2026-10-18` / sprint-15 `2026-10-20` / sprint-16 `2026-10-21` / sprint-17 `2026-10-21` 同例)
**规划依据**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` §6.5:909(段 = **链门**,件集经本节 §Phase 1 **订正为 2 件**)· §三 G-1 / G-2 · §3.1 链门 · §6.4 回写义务 · §6.2 取件三规则 · §6.3 四条硬复核
`docs/architecture/control-manifest.md`(Manifest Version = `2026-10-15`)
**Review Mode**:`lean`(Phase 4 的 `PR-SPRINT` 生产门按 skill **跳过** —— 非 PHASE-GATE)

---

## Phase 1 硬前置:重扫核出的五处前提错位(2026-10-21 实测)

> **本节的定位**:路线图 §6.5:909 登记的件集是 **`G-8` · `G-1` · `G-2` 三件**。逐件跑 §6.3 的
> R-1…R-4 后,**三件里有两件与登记不符**。承 sprint-17 §Phase 1 同形态:**规划期不订正,
> 估计与验收判据从起点就错**。

| # | 项 | 路线图登记 | 今日实测(可复算) | 对规划的影响 |
|---|---|---|---|---|
| **①** | **G-8** | sprint-18 的件之一 | **已于 2026-10-21 结案**(S17-4)。债册 `docs/tech-debt-register.md:525` 状态列 = `**✅ 已结**(2026-10-21)`、P0 面列 = `~~**Y**~~`;证据原件 = `production/qa/evidence/impl-s17-4-furniture-catalogue-2026-10-21.md`(§八「`TD-clinic-01` 可结:判据见 §三」/「路线图 §三 G-8 可结」) | **件集 3 → 2**;G-8 **从本 sprint 移除** |
| **②** | G-8 的 Exit 判据 | 「`clinic-001` 的两条挂标 AC 同步转可判」 | **已达** —— `clinic-machine/story-001-baked-tables-schema-gates.md:52`(`AC-24-08`)/ `:55`(`AC-24-12`)两条已解除 `BLOCKED-BY-O-24-1` 挂标并勾选 `[x]` | 该 Exit 判据**不再是待办**,是既成事实 |
| **③** | **G-1**(`TD-sim-01`) | 债册 `:1523`「解除动作」= 给判死路径加 `entity` 形参 + 补一条夹具 | **实现面是四件,不是一件**(下表展开) | **L 级**;AC 判据须按四件套写,不得按债册单句写 |
| **④** | G-2(`TD-persist-01`) | 「`Folded(p)` 谓词未实现」 | 与登记**一致**。缺 = 7a 侧谓词本体 + 折叠行产出;四条件里「无未结案病例」的**判据接口已在盘**(`CaseStreamQuery.HasOpenCase`,`unity/Assets/Sim/CaseOpenDecider.cs:66`) | 口径**无需订正** |
| **⑤** | **新发现的未登记阻塞** | — | `death-respawn/story-005-respawn-cell-teleport-command.md:25` / `:47` 挂 **`BLOCKED-BY-24`**(24 的 GDD 未定义「床类家具格的确定性选定规则」;`design/gdd/death-and-respawn.md:192` 明写「24 的下一轮须补」)。`IClinicReviveCell` **全树零命中**(仅卡面一处引用);**债册零条目** | 「引用却无登记」失效模式**第四次**;归 **S18-3** 登记 |

### ③ 展开 —— G-1 的口径订正

债册 `:1523` 写「解除动作 = 9 的下一轮:给判死路径加 `entity` 形参(或并列独立谓词 `SelfLimited(entity, d)`),
并补一条夹具证明「同病种对 Player 与 NPC 判死不同」」。实测该描述**只覆盖四件里的一件**:

| 缺件 | 实测(扫描面:`unity/Assets/` 全树 `.cs`,含大小写变体) |
|---|---|
| **谓词本体** | `SelfLimited` / `LethalFor` / `IsCombatant` **零命中** |
| **产出侧** | `DeathCandidate` / `PlayerDeathWarranted` / `DeathWarrant` **零命中** |
| **生产调用点** | `StateMachine.ComputeTransition` **零生产调用点** —— 9 处全在 `Tests/EditMode/DiseaseSimulation/state_machine_death_test.cs`;`Editor.Tools.Gates/WriterExistenceGate.cs:98` / `:101` 逐字 `PlayerDied` / `InjuryStateChanged` 写者「未落 —— 归 9 实现轮」 |
| **数据面** | `RegistrySchema.DiseaseRegistryEntry`(`unity/Assets/Sim/RegistrySchema.cs:137-197`)**无 `kind` 字段**,而 GDD `disease-simulation.md:285` 要求 `kind ∈ {injury, disease, poison}`;破例条件 `d.kind = injury` 无载体 |
| **白名单** | 「战伤白名单」**无任何数据定义** —— `death-and-respawn.md:96` 只点名、未给闭集;`assets/data/` 内 `INJ_*` 零命中 |

⇒ **G-1 = L 级**(≥3 依赖 **且** 需用户裁定),不是债册描述的「一个谓词」。

### `clinic-001` 的重扫(与 G-8 结案的关系)

`clinic-001` 仍 `In Review`(`story-001:4`)。⚠️ **G-8 结案 ≠ `clinic-001` 可转 `Complete`** ——
其三条现役理由中:① `FurnTypeClosureEnforced` 仍 `false`,且这是**合法状态**(闭集已落地,
但默认入口仍走代理判据,见 `Gameplay.Presentation/ClinicRoomMatchingService.cs:85` 自陈);
② 16 条评审残留 🟡 未修;③ IL2CPP 格。本 sprint **不为它转任何状态**(承 sprint-17 偏离 ① 同口径)。

**四件 AC 面逐条读过**:S18-1 与 S18-2 的判据均落在**纯函数 + 反射/字节断言**上,无一条需要真机。

---

## Sprint Goal

清掉 `clinic` / `death-respawn` 两链的剩余 `BLOCKED-BY` **根因**(G-1 `SelfLimited` · G-2 `Folded(p)`),
使 `death-respawn` 全 6 件、29 的 6 条 AC(`AC-29-01/02/05/08/10/17`)、25 的 `AC-25-6-08`
的阻塞前提从「实现待落」转为「已落」;同时把 G-8 结案后**路线图未回写的四处登记**补平。

> **本 sprint 不做**:
> - **不改变 M3 分母的方向** —— 实测复算:`TD-INDEX` **99 条** · `P0 = Y` **88** · **未闭 P0 面 = 73** ·
>   epic 分布 **13 / 31**。G-1 / G-2 结案会让它 **`73 → 71`**,但这是**清债**,不是新增进展
>   🔁 **2026-10-22 收口订正(原文不删)**:本句的 `73 → 71` **作废** —— 两门皆未清(G-1 结转 / G-2 不主张结案),
>   且 `TD-clinic-18` 立债令基数 +1 ⇒ 实测 **`73 → 74`**。连「双结案」假设下也只有 **72**,**`71` 从未成立**。见 §九.1。
> - **不转 `clinic-001` 状态**
> - **不开 `death-respawn` 任何 story** —— G-1 / G-2 结案是它的**前置**,不是它本身;且还有
>   `BLOCKED-BY-24` 未解(§Phase 1 ⑤)
> - 不动 G-4 / G-5 / G-6 / G-7(路线图 §6.5:910 = sprint-19)

---

## Capacity(承 `production/CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束,**不估天**)

| 元 | 值 | 出处 |
|---|---|---|
| **件数** | 计划 **3 件**(2 实现 + 1 记账)· 计划内完成 0 · 计划外追加 0(待收口记) | 本节 Tasks 表 |
| **复杂度分布** | **L × 1**(S18-1)+ **M × 1**(S18-2)+ **S × 1**(S18-3) | §3.2「C」= 依赖数 + 判别力要求(人判) |
| **追加率预留** | 按实测区间 **[0%, 57%]** 预留 1 件额度 | §3.1 第三元 |
| **约束** | 每批 **2–4** 件 · 并发 **≤ 3** · **L 件独占 1 批,不与 M/S 混批** ⇒ 两批 | §3.2 三条 |

**复杂度逐件理由**:

| 件 | C | 理由 |
|---|---|---|
| `S18-1` | **L** | 依赖 ≥3(谓词本体 + `kind` 字段 + 白名单闭集 + 产出路径 + 两条写者)**且需用户裁定**(白名单内容)—— 命中 L 的两条判据(§3.2) |
| `S18-2` | **M** | 依赖 2(ADR-010 §二 折叠行形状 · `CaseStreamQuery.HasOpenCase` 已在盘);判据可机械构造(纯函数 + 字节级折叠行) |
| `S18-3` | **S** | 零依赖,判据单一(四处文本同值可 grep) |

---

## Tasks

### Must Have(关键路径)

**批 1 —— `S18-1` 独占(L 不混批)**

| ID | 任务 | 层/型 | C | 依赖 | 验收判据(可复算) | 测试载体 |
|---|---|---|---|---|---|---|
| **S18-1** | `TD-sim-01` / **G-1**:`SelfLimited(entity, d)` 逐实体判死谓词及其依赖面 | Core / Logic | L | ① `DiseaseRegistryEntry.kind` 字段(数据面**缺**)② 战伤白名单闭集(**✅ 已裁 2026-10-21 `OQ-S18-1` = 双条件 `lethal(d) ∧ d ∈ {STAB, LACERATION, HEMORRHAGE, CLAW, BITE}`**,见 `death-and-respawn.md` 规则二订正注)③ `DeathCandidate` 产出 ④ `WriterExistenceGate` 两条写者 **⑤ F4 昏迷支的 combatant 矛盾**(`disease-simulation.md:1084-1090` 与 `:570` 两处渲染不一致) | ① 谓词存在且**同病种对 Player 与 NPC 判死不同** —— 夹具必须能区分,否则假绿;② `d.kind` 参与判据时**无 `if kind == injury` 分支**(规则九,`disease-simulation.md:358`);③ 破例**只**在 `kind = Player ∧ d ∈ 白名单` 成立;④ 不改任何既有 `self_limit` 数据值(对 13 / 52 / 10 **零涟漪**);⑤ `DeathCandidate` 产出路径有生产调用点,`WriterExistenceGate` 两条从「未落」转为「已落」 | `unity/Assets/Tests/EditMode/DiseaseSimulation/self_limited_predicate_test.cs`(新) |

**批 2 —— `S18-2` + `S18-3`**

| ID | 任务 | 层/型 | C | 依赖 | 验收判据(可复算) | 测试载体 |
|---|---|---|---|---|---|---|
| **S18-2** | `TD-persist-01` / **G-2**:`Folded(patientId)` 四条件折叠谓词 + 折叠行产出 | Core / Logic | M | ADR-010 §二 折叠行形状 · `CaseStreamQuery.HasOpenCase` | ① **四条件**全实现:终态(痊愈/死亡)∧ plateau / `relapse.*` 豁免 ∧ **无未结案病例**(ADR-008 §六)∧ **`SkillGrown` 行豁免**(ADR-026 §⑦);② 折叠行 = `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)` **逐字段**;③ 正反夹具:有未结案病例 ⇒ `Folded = false`;`SkillGrown` 行**不被抹除** | `unity/Assets/Tests/EditMode/Persistence/folded_predicate_test.cs`(新) |
| **S18-3** | 台账回写(G-8 结案后**路线图 §6.4 回写义务未执行**的四处 + `BLOCKED-BY-24` 登记 + **`death-respawn` 台账的 `OQ-29-4` / `OQ-29-5` 过期登记订正**) | 记账 | S | 无 | ① 路线图 §三 G-8 行按 §6.4 划掉;② §3.1 链门行移除 G-8;③ §6.5 sprint-18 行订正件集;④ `BLOCKED-BY-24` 有债册条目或用户裁定去向;⑤ `death-respawn` 台账五处过期登记就地加注(`story-004:17/:38/:82` + `EPIC.md:11/:51`);判据 = 各处**原串仍可 grep 到** **且** 加注串同时命中(原文不删) | — |

### Should Have

**无** —— 两件实现已占满「L 独占 1 批 + 另一批 2–4 件」的额度。追加件按实测追加率 `[0%, 57%]` 从预留额度承接,**规划期不预设**。

### Nice to Have

**无** —— 同 Should Have。

---

## Carryover

**无 sprint-17 结转** —— sprint-17 的 `conditions_open` 仅 **C-4** 一条,已按用户裁定登记为
**`TD-ci-01`**(性质 = **EXTERNAL**,凭证须用户侧申请),不结转 story 级任务
(查 `production/sprint-status-sprint17-archive.yaml` §`conditions_open`)。

---

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **G-1 的「一个谓词」口径失真** —— 债册原文只写加 `entity` 形参,实测缺四件 | **确定** | **高** | 已在 §Phase 1 ③ 就地订正;S18-1 的 AC 按四件套写,**不按债册单句写**;复杂度判 **L** 并独占 1 批 |
| ~~**战伤白名单内容无定义** ⇒ AC 判据不可写~~ | ~~**确定**~~ | ~~中~~ | ✅ **已结(2026-10-21 `OQ-S18-1`)** —— 闭集 = 双条件 `lethal(d) ∧ d ∈ {INJ_STAB, INJ_LACERATION, INJ_HEMORRHAGE, INJ_CLAW, INJ_BITE}`。**替代风险(同档,未结)**:① 破例**同时关闭玩家昏迷支**(F4 昏迷支读 `SelfLimited`);② 白名单伤情须靠自身曲线越 `DEATH_THRESHOLD`(`TRAUMA_CAP = 4/5` 已钉 ⇒ 注入量单独够不到),而 R1.3 十七条校验**无此项** ⇒ 须补一条新校验,否则「白名单里的伤情永远杀不死人」且**无任何东西报错** |
| `kind` 字段进 `DiseaseRegistryEntry` 触及 registry 17 条校验 + 烘焙管线 + 现有数据无该字段 | 中 | 中 | 实施前先测涟漪面(9 的 story-003 校验器 + `Sim.Codec` 的 cooked 面);若涟漪超「加一个字段」⇒ 拆批并**如实登记** |
| **G-1 / G-2 结案 ≠ `death-respawn` 可开工** —— 还有 `BLOCKED-BY-24` | **确定** | 中 | 本 sprint 明确**不开** `death-respawn` 任何 story;`BLOCKED-BY-24` 由 `S18-3` 登记去向 |
| **借绿**:两件全 Logic,且 G-1 的判据天然易滑向「自造断言顶替 AC 语义层」(本仓已三发) | 中 | **高** | 每件过**证伪轮**(改坏生产码 ⇒ 指定断言转红);`exactRed` 须**先论证后定预期**,禁把实得抄进 `expectRed`;`Proven` 列填可复算判据,禁写动作描述 |
| ADR-012 IL2CPP 矩阵 | **确定** | 低 | 本批零跨平台新增面;矩阵级,非本件个体判据。⚠️ **CI 常驻 job 仍开**(`TD-ci-01`,EXTERNAL) |

---

## Dependencies on External Factors

- **无外部阻塞** —— G-2 的上游全部在盘;G-1 的上游**除白名单闭集外**均在盘
  (白名单 = **用户裁定项**,非外部系统)。
- ⚠️ **`TD-ci-01` 仍 EXTERNAL**(CI 常驻 job 缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`)——
  与本 sprint **零交集**,不阻塞。

---

## Definition of Done for this Sprint

- [ ] `S18-1` / `S18-2` 的 AC 逐条勾选并附**可复算判据**(禁止「已落盘」当判据)
      🔁 **2026-10-22 收口实测:不勾(部分满足)** —— **S18-2 满足**(四条件各有断言 + 折叠行六字段逐字段);
      **S18-1 不满足** —— 它已**结转 sprint-19**(用户裁定),AC ⑦⑧ 的被测物在生产树 0 命中
      (`IDeathCandidateSource` / `PlayerDeathWarranted`),AC ⑥ = NOT-RUN(规则九无机械门,见 `R2-AC6`)。
      ⚠️ **不得**因「S18-2 满足」而把本条整体勾上 —— 本条的合取主语是两件。
- [ ] **每件过证伪轮**:改坏生产码 ⇒ 指定断言转红;`exactRed` 由**论证**给出后才定预期(禁把实得抄进 `expectRed`)
      🔁 **2026-10-22 收口实测:不勾** —— **S18-2 跑了 4/4 全红**(定义域守卫 / `Lethality <= 0` 半条件 /
      重复终态抛错 / `SkillGrown` 豁免),但**作用域受限**:四轮里**没有一条**打 `HistoryFolder` 的
      「抹哪些行」,而那正是实现偏离所在(签核件 D-5)⇒ 只可主张「条件①–④各由变异反证」,
      **不可**主张「折叠产出面已验证」。**S18-1 谓词本体零证伪轮**(D-2)。
- [x] 全量 EditMode / PlayMode 对账,**三元组齐备**(截至时刻 · 批次 · commit),
      等式 `Δtotal = Δpassed = 新增条数` 成立,且**「消失 0」独立判据**成立(排除「新增 N 同时坏 N」的掩盖形态)
      ✅ **2026-10-22 收口实测** —— EditMode `4419 / 4383 / 0 / 36` @ `2026-10-22 10:19 (+0800)` ·
      批次 `review-s18-r4-editmode-20261022` · commit `d91edeb` + 修复件;**PlayMode `202 / 202 / 0 / 0 / 0`**
      @ `2026-10-22 11:33 (+0800)` · 批次 `s18-final-playmode-20261022` · commit **`08049fb`(干净树,含 `d91edeb` 全部修复)**。
      ⚠️ **PlayMode 是本轮补跑的**:原记录判的是 `ad7316c` 干净树(08:55),**早于 `d91edeb` 对
      `HistoryFolder.cs` 的拆方法改动** ⇒ 不覆盖最终代码,故重跑;`error CS` = 0,零失败。
      等式 `Δtotal = Δpassed = 1` = R-4 专钉断言新增条数;全名集差集 = 新增 1 / 消失 0。
      ⚠️ **批次名与产物名不一致,如实记**:三元组原写 `review-s18-r4-fix-20261022`,**盘上无同名产物**;
      实际产物 = `review-s18-r4-editmode-20261022.{xml,log}`(数值对得上)。本行已改指向实际产物名。
- [x] `S18-3` 四处台账同值可 grep + `death-respawn` 台账五处「原串 + 加注」双命中
      (story-004 `:17` / `:38` / `:82` · EPIC.md `:11` / `:51`;判据见 QA plan S18-3 判据 5)
      ✅ **2026-10-22 收口实测** —— 五处逐处命中,且每处都是「原文 + 🔁 2026-10-21 订正」双段
      (原文不删、就地加注);`OQ-29-4` / `OQ-29-5` 两问均已结案并回填。
- [x] QA plan 存在(`production/qa/qa-plan-sprint-18-*.md`)
      ✅ `production/qa/qa-plan-sprint-18-2026-10-21.md`
- [x] Smoke check 通过(`/smoke-check sprint`)
      ✅ `production/qa/smoke-2026-10-22-sprint18-final.md`(判 `ad7316c`,Verdict = **PASS WITH WARNINGS**);
      旧件 `smoke-2026-10-22.md` 判的是 `220acc3`,已被取代但**不删**(原文留档)。
- [x] QA 签核 `APPROVED` 或 `APPROVED WITH CONDITIONS`(`/team-qa sprint`)
      ✅ `production/qa/qa-signoff-sprint-18-2026-10-22.md` —— **APPROVED WITH CONDITIONS**;
      S18-1 结转 sprint-19(用户 2026-10-22 裁定),C-1…C-8 中 C-3 **转移**而非结清。
- [x] 无 S1 / S2 缺陷
      ✅ 签核件 §三「**无 S1–S4 缺陷录入**」;未结缺口 D-1…D-5 属**未完成 / 未裁**,不是缺陷。
- [x] 代码评审完成(`/code-review`)
      ✅ 初判 `CHANGES REQUIRED` → 五条 Required 全修(`d91edeb`)→ **重评 `APPROVED WITH SUGGESTIONS`**。
      原件 `production/qa/evidence/review-sprint18-implementation-2026-10-22.md`(§一–§七 初评 + **§八 重评轮**)。
      ⚠️ 重评 5/5 核账成立、新引入 BLOCKING/WARNING = 0;两条 WARNING 为**结转项 S-6 / S-8**(文档批)。
- [ ] **不得**把本 sprint 的绿引作 M3 进展;分母变化须写清方向(~~**清债 `73 → 71`**,非新增~~)
      🔁 **2026-10-22 收口实测:原数字作废,本条**不勾** —— 见下方 §九 分母订正。**
      实测:`TD-INDEX` **100 条** · `P0 = Y` **89** · **未闭 P0 面 = 74** · epic 分布 **13 / 31**。
      **`71` 在任何情形下都不成立** —— 它假设 G-1 与 G-2 双结案,而 G-1 已结转 sprint-19;
      且本 sprint 期间 `TD-clinic-18` 立债令分母 `73 → 74`。真值 = **`74`**(G-2 亦不结)或 **`73`**(G-2 结)。
- [x] 收口时把 `production/sprint-status.yaml` 归档为 `production/sprint-status-sprint18-archive.yaml`
      (**SHA-256 逐字节比对**;承 sprint-14/15/16/17 同例)
      ✅ SHA-256 = `3212ceb6eb9b8f384c4c32556de09be5b0ed43a9d84191404678294cf1ecc6a7`;
      `cmp` 复制时刻 = BYTE-IDENTICAL;活件与归档件差异 = **仅 sprint-18 注那 10 行**。

---

## QA Plan

**规划时状态:不存在** —— `production/qa/` 下无 `qa-plan-sprint-18-*`(现存至 `qa-plan-sprint-17-2026-10-21.md`)。

**用户裁定(2026-10-21)**:**先跑 `/qa-plan sprint`**,先于任何实施。
两件均为 Logic 型,QA plan 定的正是「每件的测试要求 + AC → 载体映射」;
不写它,「什么算 done」只能由 `/dev-story` 逐件临场判 —— 承 sprint-17 同款裁定。

---

## 溯源

| 项 | 出处 |
|---|---|
| 件集登记 | 路线图 §6.5:909(`G-8 · G-1 · G-2`,经本节 §Phase 1 订正为 **G-1 · G-2**) |
| G-1 载体 | 债册 §六:517(`TD-sim-01` · `disease-simulation` 7/7 · `open` · P0 面 `Y`) |
| G-2 载体 | 债册 §六:518(`TD-persist-01` · `persistence-service` 2/2 · `open` · P0 面 `Y`) |
| G-8 结案 | 债册:523(状态 `✅ 已结 2026-10-21`)· `impl-s17-4-furniture-catalogue-2026-10-21.md` |
| 链门语义 | 路线图 §3.1(「清一道解锁整条链的剩余件」) |
| 回写义务 | 路线图 §6.4(「某道 G-x 门清掉 ⇒ §3 划掉 + 从 §4 移除对应阻塞」) |
| 分母现值 | `TD-INDEX` 复算:99 条 · `P0 = Y` 88 · 未闭 P0 面 **73** · epic **13/31**<br>🔁 **2026-10-22 收口复算(原文不删)**:`100` 条 · `P0 = Y` **89** · 未闭 P0 面 **74** · epic **13/31** —— 变化全来自 `TD-clinic-18` 立债。见 §九.1 |

---

## 九、收口记录(2026-10-22)

> 承 sprint-17 的「六 / 七 / 八」三节惯例。**原文不删**,本节为收口时新增。

### 九.1 M3 分母:原写 `73 → 71` **作废**,实测 = `74`(或 `73`)

**原数字错在哪**:`73 → 71` 的算术前提是 **G-1 与 G-2 双结案**。收口实测两条都不成立:

| 门 | 载体 TD | 收口时状态 | 是否减分母 |
|---|---|---|---|
| **G-1** | `TD-sim-01` | **结转 sprint-19**(用户 2026-10-22 裁定) | **不减** |
| **G-2** | `TD-persist-01` | 见 §九.2 —— 实现已落、**接线未落** ⇒ 不主张结案 | **不减** |

**另有反向一笔**:本 sprint 期间 `TD-clinic-18` 立债(S18-3 台账回写轮)
⇒ `条目 99 → 100` · `open 72 → 73` · `Y 88 → 89` · **`未闭 P0 面` 73 → 74**(债册「十订」)。

**实测复算**(债册自带脚本 `§五 M3 Exit 第二条合取的求值命令`,原样跑):

```
条目 100 | 状态 {open:73, partial:6, closed:20, done:1} | P0 面 {Y:89, N:10}
未闭 P0 面 = 74
```

| 情形 | 未闭 P0 面 |
|---|---|
| 两门都不结(**当前事实**) | **74** |
| 仅 G-2 结 | 73 |
| G-1 + G-2 双结(原计划假设,**未发生**) | 72 |

⚠️ 连「双结案」情形也是 **72**,**不是 71** —— 因为 `TD-clinic-18` 把基数抬了一位。
⇒ **`71` 从未在任何情形下成立**;它是规划期未计入新立债的算术。

⇒ **本 sprint 的绿不得引作 M3 进展**(方向是「立债 + 结转」,不是清债)。

### 九.2 G-2(`TD-persist-01`)为何**不主张结案**

S18-2 已 `done`,四条件谓词 + 折叠行产出都在盘上、有 29 条测。
但**折叠至今零生产调用点** —— `SaveService` 的 `IFoldedPredicate` 形参默认 `null`,
折叠未进任何存档路径;`HistoryFolder.Fold` 的调用点**全部在测试内**。

债册对该条的「解除动作」要求「实现谓词 + 折叠行产出 + 正反夹具」—— 字面三项均已满足。
⚠️ **但本收口不据此宣布结案**,理由:① `FoldedPredicate` 无生产消费者 ⇒ 「能折」与「真折了」是两件事;
② `OQ-7a-10`(条件① 病人级存在性 vs 折叠行逐病种)未裁,抹除作用域的实现取的是**权威字面**而非已裁口径;
③ 签核件 D-5 明确「折叠产出面无任何变异反证」。
⇒ **`TD-persist-01` 保持 `open`**,结案判据 = 折叠接入存档路径 + `OQ-7a-10` 裁定 + 产出面变异反证。
**这条是留给 sprint-19 之后某一轮的口子,已具名,不得含糊过去。**

### 九.3 收口时仍未做的一件(如实,不留白)

- **`/retrospective sprint-18` 未写** —— `production/retrospectives/` 最新为
  `retro-sprint-14-2026-10-18.md`,**retro-15 / 16 / 17 / 18 四份均不存在**。
  这是**累计缺口**(sprint-17 已登记为 retro-15/16/17),非本 sprint 独有。
  非 DoD 条目 ⇒ **不阻断收口**,列为下一步。⚠️ **不得**写成「已排期」—— 未排期。

### 九.4 本 sprint 收口**不**代表以下任一

- **不代表 `death-respawn` 全链解锁** —— 仍 `BLOCKED-BY-9`(G-1 结转)。
- **不代表 `TD-persist-01` 已结案** —— 见 §九.2。
- **不代表折叠产出面已验证** —— 四轮变异未打「抹哪些行」(D-5 / `OQ-7a-10`)。
- **不代表 S18-1 的 AC 已兑现** —— 它结转 sprint-19,四条入口判据见签核件 §六。
- **不代表 `S-1`…`S-8` 八条 Suggestions 已清** —— 全部仍 open,去向已逐条登记于评审原件 §五。
- **不代表 `R2-AC6`(规则九机械门)已建** —— 仍 NOT-RUN。
- **不代表 `AC-29-16` 转可判** —— 其 `BLOCKED-BY-7a 折叠实现` 需 §九.2 的三项同时满足。

> **不经用户指令不 commit。**
