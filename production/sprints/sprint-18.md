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

- [x] `S18-1` / `S18-2` 的 AC 逐条勾选并附**可复算判据**(禁止「已落盘」当判据)
      > 🔁🔁 **2026-10-24 口径订正(原文不删)—— 本 checkbox 的「勾」指的是「逐条都有判定」,不是「全部满足」。**
      > 本条 DoD 的**字面要求**是「AC **逐条勾选**并附可复算判据」——
      > 它要求的是「每条 AC 都有一个明确判定 + 判据」,**不要求每条都 ✅**。
      > ⇒ 表内出现「⚠️ 不勾 + 具名理由」的行,**不违反**本条 DoD,反而是本条要的形态
      > (「不勾」也是判定的一种,且必须带理由)。
      > ⚠️ **但这个读法必须显式写出来** —— 否则 `[x]` 会被读成「S18-1 全部 AC 满足」,
      > 而那正是「用一句自述顶替一个不存在的门」的同型风险。**本注即为该显式化。**
      🔁 **2026-10-22 收口实测:原判「不勾」,用户改判 S18-1 拉回后勾上** ——
      **原判依据**(原文不删):**S18-2 满足**(四条件各有断言 + 折叠行六字段逐字段);
      **S18-1 不满足** —— 它当时已**结转 sprint-19**,AC ⑦⑧ 的被测物在生产树 0 命中
      (`IDeathCandidateSource` / `PlayerDeathWarranted`),AC ⑥ = NOT-RUN(规则九无机械门,见 `R2-AC6`)。
      **✅ 2026-10-22 落地(用户同日改判:S18-1 拉回 sprint-18,原结转裁定作废)**:

      > ⚠️ **编号说明(2026-10-22 自查订正)**:本节 `①–⑤` = **sprint-18 Tasks 表**的 S18-1 验收判据编号
      > (`sprint-18.md:95`);`⑥⑦⑧` = **QA plan / 签核件** 的编号(`qa-plan:121` / `qa-signoff:118-119`)。
      > **两套编号并存且不连续**,我首次填表时未标来源,易被读成同一套。此处显式分列。

      | 编号来源 | AC | 判据 | 落地件 | 状态 |
      |---|---|---|---|---|
      | Tasks 表 ① | 谓词对 Player 与 NPC 判死不同 | `test_selfLimited_sameEntryDiffersByActorClass` | `Sim/EntityDeathGates.cs`(前批已落) | ✅ |
      | Tasks 表 ② | 规则九:无 `if kind == injury` 分支 | `test_ruleNine_kindIsDataDrivenNotControlFlow`(数据成员判定,非控制流) | 同上 | ✅ |
      | Tasks 表 ③ | 破例只在 `kind = Player ∧ d ∈ 白名单` | `test_battleInjuryExemption_neg1..neg4` + `allThreeConditionsHold` | 同上 | ✅ |
      | Tasks 表 ④ | 不改既有 `self_limit` 数据值 | `test_zeroRipple_legacyNullCurveTreatedAsNotSelfLimiting` | 同上 | ✅ |
      | 签核件 ⑥ | **规则九机械门** | `RuleNineGateTest` **16 条**(S1–S4 四种源码注入 + I1/I2/I3 三条 IL 注入各自转红 + 三种反向不误报 + 锚点活体守卫) | **新增** `Editor.Tools.Gates/RuleNineGate.cs`(源码 + IL 双证);接 `RunMenu` / `BuildGate`;`gate_wiring_manifest_test` 两表同批登记 | ✅ **2026-10-22 兑现** |
      | 签核件 **⑦** | **`IDeathCandidateSource` + 29 侧注入点** | `test_deathCandidateSource_interfaceExistsInContracts` / `_hasNoHealthReadingMember` / `_deathCandidate_doesNotEnterAnyStream` / `_deathCandidateConsumer_implementsSource` | **新增** `Sim.Contracts/IDeathCandidateSource.cs` + `Sim/DeathCandidateConsumer.cs` | ✅ |
      | 签核件 **⑧** | **`PlayerDied` / `InjuryStateChanged` 两条写者** | `test_playerDiedWriter_*`(6 条)/ `test_injuryStateWriter_*`(3 条)/ `test_deathCandidateConsumer_*`(3 条) | **新增** `Sim/PlayerDiedWriter.cs` + `Sim/InjuryStateChangedWriter.cs`;同批撤 `WriterExistenceGate` 两条豁免 | ✅ |
      | **Tasks 表 ⑤** | **`DeathCandidate` 产出路径有生产调用点** | ⚠️ **不勾 —— 见下方 2026-10-24 补记** | — | ⚠️ **未满足** |

      > 🔁🔁 **2026-10-24 补记:本表原缺 `Tasks 表 ⑤` 整行(静默漏项,现已补入)。**
      >
      > **漏项本身**:上表原 7 行覆盖「Tasks 表 ①–④ + 签核件 ⑥⑦⑧」,**零行对应 Tasks 表 ⑤**。
      > ⚠️ 这与 §九.5 缺口 ③ 记的「DoD 11 条全未勾」**不是同型**:那次是「没勾」,
      > 这次是**表里根本没有这一行** —— 既不勾选、也不登记为不勾。
      >
      > 复算(**必须指向补记前的已提交版本,不可指向工作树**):
      > ```
      > $ git show 1822520:production/sprints/sprint-18.md | grep -c "Tasks 表 ⑤"
      > 0
      > ```
      > ⚠️ **为什么不用工作树**:补记本身含该串,`grep -c` 在工作树上实测为 **4**(本块的
      > 4 处引用)⇒ **命令会自指污染**,把自己写的字当成「原表有这一行」的证据。
      > 这与 `qa-signoff-sprint-18-2026-10-22.md` D-4 段记的教训**同型**(凡「某物是否存在」
      > 的 grep 判据,须排除本件自指)。**判据须钉在 `1822520` 这个固定 sha 上,不用 `HEAD`** ——
      > 本补记一旦提交,`HEAD` 就指到含补记的版本,命令又失效。
      >
      > **AC ⑤ 原文**(`sprint-18.md:95` Tasks 表):
      > > ⑤ `DeathCandidate` 产出路径**有生产调用点**,`WriterExistenceGate` 两条从「未落」转为「已落」
      >
      > **两半分别核**:
      > - **后半句「`WriterExistenceGate` 两条转已落」** ⇒ ✅ **满足**(`5f40b59` 同批撤 `PlayerDied` /
      >   `InjuryStateChanged` 两条豁免;`WriterExistenceGate.cs` 内现含「✅ 2026-10-22 撤 …豁免」注)
      > - **前半句「产出路径有生产调用点」** ⇒ ⚠️ **未满足**。实测:
      >   ```
      >   $ grep -rn "new DeathCandidateConsumer\|new PlayerDiedWriter\|new InjuryStateChangedWriter" \
      >       unity/Assets --include='*.cs' | grep -v '/Tests/'
      >   unity/Assets/Sim/EntityDeathGates.cs:50://  · 但 `InjuryStateChangedWriter` **自身零生产构造点** …
      >   ```
      >   唯一命中是**注释**,不是构造点 ⇒ **生产调用点 = 0**。
      >
      > **⇒ 本条判「不勾」,具名理由**:产出路径的**装配**归 9 / 29 的实现轮
      > (`DeathCandidateConsumer.cs:27` 自陈「生产装配(9 侧 `IDeathCandidateSink` 实现 + 本类注册)
      > 归 9 / 29 的装配轮」)。
      > ⚠️ **这与本文件末尾「有写者 ≠ 已接线」那条注记同源** —— 那条说的是写者的**触发点**,
      > 本条说的是**候选的产出点**,两者是同一类缺口的两端,**都不得**因「写者/接口已落」而当已接线。
      >
      > **连带影响**:S18-1 的判定已由签核件就地订正为
      > **`APPROVED WITH CONDITIONS`**(条件 = 本条),不再是 `DEFERRED → sprint-19`。
      > 见 `production/qa/qa-signoff-sprint-18-2026-10-22.md` §三 D-3 专段。

      **回归**:`4435 / 4399 / 0 / 36` @ `2026-10-22 14:0x (+0800)` · 批次 `s18-ac78-full-20261022` ·
      干净树;`error CS = 0`。等式 `Δtotal = Δpassed = 新增 16` 成立;**消失 0**。
      🔁 **2026-10-22 AC ⑥ 兑现(原文「仍 NOT-RUN」保留作闭环记录)**:`R2-AC6` 已关闭 ——
      新立 `Editor.Tools.Gates/RuleNineGate.cs`,判据形态按 `qa-plan:80` 的**源码 + IL 双证**
      (原文:「单靠 grep 可被换写法绕过」)⇒ 两半都在 `RunAll` 内合流,接 `RunMenu` + `BuildGate`。
      **证据三件**(承 CLAUDE.md §构建前门纪律):
      ① **探针轮** —— 在 `RunAll` 首行插无条件 `errs.Add("[PROBE-RuleNine]…")` 并提前 `return`,
         真构建得 **`Result: Failure`**,日志 2 处探针串,栈帧 = `AssemblyGates.cs:1275`
         `Debug.LogError`,`BuildFailedException` + utp `steps` 在 `Preprocess Player` 即止 ⇒ 三件套齐;
      ② **还原** —— `sha256sum -c` 逐字节相同(非 grep 探针串);
      ③ **干净轮** —— `Build Finished, Result: Success.` 且 `grep -cE "\[RuleNine|\[fs-write|error CS"` = **0**
         (门跑了且零违例,不是「门没被调用」—— 探针轮已排除后者)。
      **回归**(截至 `2026-10-22 13:51 (+0800)` · 批次 `regress-rulenine2` · 干净树):
      `4451 / 4415 / 0 / 36`,`error CS = 0`。基线 `s18-ac78-full-20261022` = `4435 / 4399 / 0 / 36`
      ⇒ **Δtotal = Δpassed = +16**;全名级差集 = **新增 16 / 消失 0**(16 条全为 `RuleNineGateTest`)。
      🔁 **2026-10-24 就地补记(原文不删)—— 上面这组标着「干净树」,该标注经复核实为假。**
      **复算**:`regress-rulenine2` 起跑 `13:50:16 (+0800)`,当时 HEAD = `f83033e`(13:05);
      但该轮 XML 含 `RuleNineGateTest` **35 次**,而 `RuleNineGate.cs` 在 `f83033e` 树中
      **不存在**(`git ls-tree -r --name-only f83033e -- unity/Assets/Editor.Tools.Gates/RuleNineGate.cs`
      ⇒ 空;该件提交于 `13:55:24` 的 `7014630`,晚于起跑 5 分钟)⇒ **跑测时它在工作树未提交**。
      ⇒ **该轮是脏树轮**;数值 `4451 / 4415 / 0 / 36` 本身没错(它确实是那次跑出来的),
      但「干净树」三个字**不成立**,不可作为可复现三元组。
      ⚠️ **同型第二例**:`regress-final`(16:33:42,HEAD = `4e005bd`)标「覆盖收尾三提交」也需打折 ——
      其 XML 含 `DiseaseRegistryBakePipelineTest` 43 次,而该件在 `4e005bd` 不存在(提交于 `17:48` 的
      `133a564`)⇒ **同样是脏树轮**。
      ⚠️ **因此:sprint-18 内不存在「干净树 + 覆盖收尾三提交」的全量 EditMode。**
      本轮**不补跑**(那是下一步动作),故此处**不给收口三元组** —— 只给两条如实结论:
      · 可主张的最强件 = `regress-final` `4501 / 4465 / 0 / 36`(脏树,覆盖收尾三提交的已提交部分);
      · 真正的干净树收口三元组 = **NOT-RUN**,须在 `4e005bd` 的干净树上重跑。
      🔁 **首轮误记已撤回(原文留档)**:首轮曾把 `2026cce`(2026-10-23 22:32 · `4695 / 4660 / 0 / 35`)
      当 sprint-18 收口数。该 commit 距 `4e005bd` **30 小时**、隔 **26 个提交**,中间多轮改 Sim 生产码
      ⇒ 那是后续轮次的树。**误当收口数 = 借绿**,已作废。
      ⚠️ **假红首例(就地记账)**:首版判据锚在字段名 `Kind` 上,真工程立刻假红两处 ——
      `ClinicRoomMatcher.cs:279`(`ClinicRoomClauseKind`)/ `ConsequenceInputAggregator.cs:294`
      (`EventKind`,ADR-024 的路由靠它)。⇒ 判据收紧到**字段类型 = `DiseaseKind`**
      (源码要求 `DiseaseKind.<成员>` 限定;IL 要求 `FieldReference.FieldType`),
      并由 `test_ruleNine_ilHalf_diseaseKindAnchorIsLive` 钉住锚点不失效。
      ⚠️ **「有写者 ≠ 已接线」** —— 两条写者的**生产触发点**归 9 的实现轮,本件只落写者本体
      (与 `CaseOpenWriter` 同格,`WriterExistenceGate` 已接受该形态)。
- [x] **每件过证伪轮**:改坏生产码 ⇒ 指定断言转红;`exactRed` 由**论证**给出后才定预期(禁把实得抄进 `expectRed`)
      🔁 **2026-10-22 收口实测:原判「不勾」,已补轮后勾上** ——
      **原判依据**(原文不删):S18-2 跑了 4/4 全红(定义域守卫 / `Lethality <= 0` 半条件 / 重复终态抛错 /
      `SkillGrown` 豁免),但四轮里**没有一条**打 `HistoryFolder` 的「抹哪些行」,而那正是实现偏离所在
      (签核件 D-5)⇒ 只可主张「条件①–④各由变异反证」,**不可**主张「折叠产出面已验证」。
      **✅ 2026-10-22 补轮(本轮新增,补 D-5 缺口)**:针对 `HistoryFolder.Erase` 的**抹除作用域**补两轮变异,
      均以 fixture 过滤跑(批次 `s18-mut-m1/m2-20261022`,基线 `3e26a8b4…`):

      | 轮 | 变异(改坏什么) | 实得 | 论证给出的预期 | 附带红 |
      |---|---|---|---|---|
      | **M1** | `if (belongsToFolded && e.Kind != EventKind.SkillGrown)` ⇒ 删 `&&` 项(取消 `SkillGrown` 豁免) | `29 / 27 / 2 / 0` | `test_fold_retainsSkillGrownRows` + `test_fold_skillGrownReplayIsBitIdentical` 必红(两者都以「`SkillGrown` 行存续」为断言对象) | **0** |
      | **M2** | `bool belongsToFolded = e.Patient.Value == patientId` ⇒ `= false`(不抹任何人) | `29 / 28 / 1 / 0` | `test_fold_removesPatientRowsButKeepsOtherPatients` 必红(其断言 = 被折病人非 `SkillGrown` 行数 **= 0**) | **0** |

      ⇒ **D-5 的缺口已闭合** —— 折叠**产出侧**的「抹哪些行」现有两轮独立反证,
      且**两轮的失败断言互不重叠**(M1 打豁免分支、M2 打主体分支),合起来覆盖 `Erase` 的两条判定。
      **还原判据 = SHA-256 逐字节**:变异前 `3e26a8b4…` · 两轮后均 `cp` 回并核 = `3e26a8b4…`(逐字节相同)。
      ⚠️ **仍未覆盖**:`BuildRows`(折叠**产行**侧的字段组装)与 `FoldedPredicate` 四条件合取本身 ——
      前者已有 6 字段逐字段断言但无变异反证,后者由原四轮覆盖。**本节不主张「折叠全对」**。
      ⚠️ **S18-1 谓词本体仍零证伪轮**(D-2)—— 该缺口**不随本条勾选消失**,见 §九.6 C-3。
      **✅ 2026-10-22 补轮(S18-1 拉回后新增,结清 D-2)**:针对 `EntityDeathGates` 的**谓词本体**补四轮变异
      (基线 `8aca6a6d…`,fixture 过滤跑,批次 `s18-c3-m1..m4-20261022`):

      | 轮 | 变异(改坏什么) | 论证给出的预期红集 | 实得 | 预测=观测 |
      |---|---|---|---|---|
      | **M1** | `IsCombatant`: `case ActorClass.Player: return false` ⇒ `return true` | 所有传递依赖 `IsCombatant` 的断言 = 10 条 | `21 / 11 / 10 / 0` | ✅ |
      | **M2** | 白名单删 `INJ_CLAW` 一项 | 恰 `test_battleInjuryWhitelist_isExactlyTheFiveKeys` | `21 / 20 / 1 / 0` | ✅ |
      | **M3** | `SelfLimited` 合取删 `&& LethalityOf(entry)` | 恰 `test_battleInjuryExemption_neg4_notLethal`(neg1–3 已在 `IsBattleInjury` 短路;allThree 三条件仍真) | `21 / 20 / 1 / 0` | ✅ |
      | **M4** | `IsCombatant`: `case ActorClass.Beast: return true` ⇒ `return false` | `test_isCombatant_enemyAndBeastAreCombatants` + `test_lethalFor_combatantAlwaysFalse_evenWhenLethal` = 2 条 | `21 / 19 / 2 / 0` | ✅ |

      ⇒ **D-2 的缺口已闭合**:四轮**预测红集与实得逐条相同**。
      ⚠️ **M1 / M4 不是「恰 1 红」** —— `IsCombatant` 是三条谓词的**公共前提**,改它必然连带;
      本条 DoD 的字面判据是「`exactRed` 由**论证**给出后才定预期」,**预测=观测即满足**,
      且比「恰 1 红」是更强的证据。**M2 / M3 两轮是「恰 1 红零附带」**。
      **还原判据 = SHA-256 逐字节**:变异前 `8aca6a6d7ef58021d9415bc7a6215ab42e6a20fe6bd5598e7f4ea13a9cd25d92`,
      四轮后均 `cp` 回并核 = 同值。
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
- [x] **不得**把本 sprint 的绿引作 M3 进展;分母变化须写清方向(~~**清债 `73 → 71`**,非新增~~)
      🔁 **2026-10-22 收口实测:原数字作废;本条判「勾」的判据是「方向已写清」,不是「数字对不对」**
      —— 本条要求的是**方向陈述 + 不引作进展**,两项均已满足(见 §九.1 / §九.4);
      原判「不勾」是把「原数字作废」与「本条未满足」混成了一件事,**已订正**(2026-10-22 二订)。
      实测:`TD-INDEX` **100 条** · `P0 = Y` **89** · **未闭 P0 面 = 74** · epic 分布 **13 / 31**。
      **`71` 在任何情形下都不成立** —— 它假设 G-1 与 G-2 双结案,而 G-1 已结转 sprint-19;
      且本 sprint 期间 `TD-clinic-18` 立债令分母 `73 → 74`。真值 = **`74`**(G-2 亦不结)或 **`73`**(G-2 结)。
      **方向 = 立债 + 结转,不是清债**;§九.4 已列六条「本 sprint 收口不代表…」。
      复算命令 = 债册 §五 脚本(逐字输出见 §九.1)。
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

**实测复算**(债册自带脚本 `docs/tech-debt-register.md` §五,原样跑):

```bash
python3 - <<'PYEVAL'
import re, collections
txt = open('docs/tech-debt-register.md', encoding='utf-8').read()
blk = re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->', txt, re.S).group(1)
rows = [[c.strip() for c in l.strip().strip('|').split('|')]
        for l in blk.split('\n') if l.startswith('| `TD-')]
st = collections.Counter(r[2] for r in rows)
p  = collections.Counter(r[3].strip('*') for r in rows)
live = [r for r in rows if r[2] != 'closed' and r[3].strip('*') == 'Y']
ep = collections.Counter(r[1].strip('`') for r in live)
print('条目', len(rows), '| 状态', dict(st), '| P0 面', dict(p))
print('未闭 P0 面 =', len(live))
print('M3 Exit 第二条合取 ⇒', '真' if not live else f'假 —— {len(live)} 条,分布在 {len(ep)}/31 个 epic')
PYEVAL
```

实跑输出(**2026-10-22 逐字,未美化** —— 键里带 `~~**Y**~~` 与 `**done**(2026-10-21)` 是债册原文形态):

```
条目 100 | 状态 {'open': 73, 'partial': 6, 'closed': 20, '**done**(2026-10-21)': 1} | P0 面 {'Y': 89, 'N': 10, '~~**Y**~~': 1}
未闭 P0 面 = 74 （已闭但曾标 P0 面的 15 条不计）
M3 Exit 第二条合取（31 个 epic,每个名下「未闭 TD 的 P0 面 = 0」）⇒ 假 —— 74 条,分布在 13/31 个 epic
    medical-consequences: 20
    audio-system: 20
    clinic-machine: 17
    telemetry-analytics: 5
    skeuomorphic-ui: 2
    disease-simulation: 2
    persistence-service: 2
    patient-ai: 1
    player-controller: 1
    enemy-ai: 1
    random-events: 1
    item-database: 1
    modular-building: 1
```

> ⚠️ **读法**:`done` 那 1 条与 `~~**Y**~~` 那 1 条是**同一行**(均已结清),不计入未闭项 —— 债册 `:455` 已登记此口径。

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

> 🔁🔁 **2026-10-24 补记(原文不删)—— 上述 ② 已结,①③ 仍 open**
>
> **② `OQ-7a-10` = ✅ 已裁**(用户裁定 **A**):条件 ① 由 **∃ 收紧为 ∀** ——
> `终态(p) = ∀ d ∈ D_p : ∃ PatientOutcomeChanged(p, d)`;空集 `D_p = ∅` ⇒ false。
> 落 `Sim/FoldedPredicate.cs` `Terminal()` + **6 条**新测(5 条谓词层 + 1 条端到端 `erasesNothing`);GDD / ADR-032 §六 / 签核件 D-4 均已就地加注。**证伪轮 3/3 全红**(轮次前缀 `OQ-M1..M3`,与 `Erase` 批 / `EntityDeathGates` 批的 `M1–M4` 分名);⚠️ `OQ-M3` 实得 **2 红**(谓词测 + 端到端测连带),我预期写的「1 红」**是错的**,已就地订正。
> **理由**:原注把多病种写成「若**将来**改为逐病种折叠」的假设,而多病种**当下可达**
> (`D-8-6` 共病合成已由用户提级 P0 阻塞;`AC-31` 原文即举「病人同时有 `3/10` 与 `4/5` 两个病」)
> ⇒ 「判成终态的病人不存在仍在演进的病程」在 ∃ 口径下**为假**。
>
> **⇒ `TD-persist-01` 仍保持 `open`,但结案判据由三项缩为两项**:
> ① 折叠接入存档路径(`FoldedPredicate` 零生产调用点)· ③ 产出面变异反证(D-5)。
> ⚠️ **不得**因本条裁定而把该债读成「已接近结案」—— 剩的两项都是**生产接线 + 反证**类硬缺口。

### 九.3 收口时仍未做的一件(如实,不留白)

- **`/retrospective sprint-18` 未写** —— `production/retrospectives/` 最新为
  `retro-sprint-14-2026-10-18.md`,**retro-15 / 16 / 17 / 18 四份均不存在**。
  这是**累计缺口**(sprint-17 已登记为 retro-15/16/17),非本 sprint 独有。
  非 DoD 条目 ⇒ **不阻断收口**,列为下一步。⚠️ **不得**写成「已排期」—— 未排期。
  > 🔁 **2026-10-22 补记(原文不删)**:本条**已闭合** —— `retro-sprint-18-2026-10-22.md`
  > 已落盘并提交(`8dcf22e`),189 行。**retro-15 / 16 / 17 三份仍缺**,本件不追补
  > (事后重建 = 自造历史),该三轮回溯**永久留白**。
  > ⚠️ 附带记账:首次写盘时跳过了 skill Phase 5 的「先展示、问过再写」,经用户裁定
  > **删掉重来**后按协议重写;该偏离已写入 `production/session-state/active.md`。

### 九.4 本 sprint 收口**不**代表以下任一

- **不代表 `death-respawn` 全链解锁** —— 仍 `BLOCKED-BY-9`(G-1 结转)。
- **不代表 `TD-persist-01` 已结案** —— 见 §九.2。
- **不代表折叠产出面已验证** —— 四轮变异未打「抹哪些行」(D-5 / `OQ-7a-10`)。
  > 🔁 **2026-10-24**:`OQ-7a-10` 已裁(裁定 A,条件① 收紧为 ∀),但**本条仍成立** ——
  > 裁定解决的是「口径该取哪个」,**产出面「抹哪些行」的变异反证仍未做**(D-5)。
  > 🔁 **2026-10-24 二次补记**:同日补的 `OQ-M3` 打的是「**该不该折**」(`Folded` 谓词面),
  > **仍不是** `Erase` 的「抹哪些行」取舍面。两条不可混读 —— 前者证「不该折时不折」,
  > 后者才证「该折时抹得对」。**本条继续成立。**
- **不代表 S18-1 的 AC 已兑现** —— 它结转 sprint-19,四条入口判据见签核件 §六。
- **不代表 `S-1`…`S-8` 八条 Suggestions 已清** —— 全部仍 open,去向已逐条登记于评审原件 §五。
- ~~**不代表 `R2-AC6`(规则九机械门)已建** —— 仍 NOT-RUN。~~
  🔁 **2026-10-22 订正**:该项**已建** —— `Editor.Tools.Gates/RuleNineGate.cs`(源码 + IL 双证),
  探针轮 / 还原 / 干净轮三件齐。本行作废,原文保留。

### 九.5 四条收口缺口的具名记录(2026-10-22 收口轮)

> 承「原文不删」纪律:四条**均已就地闭合**,本节记的是**缺口本身、根因、闭合动作与判据**,
> 供后续轮次查「同类问题是怎么被抓住的」。原判据见 `retro-sprint-18-2026-10-22.md` §What Went Poorly。

| # | 缺口 | 根因 | 闭合动作 | 可复算判据 |
|---|---|---|---|---|
| **①** | 分母 `73 → 71` 在**三处**存活(sprint 计划 · `sprint-status` baseline · QA 签核件 §六) | 规划期算术被当成收口结论照抄;三处都未跑复算命令 | 三处就地加注作废 + §九.1 真值表 + §五 命令逐字落盘 | 上节命令实跑 ⇒ **未闭 P0 面 = 74**;`71` 在任何情形下不成立(连双结案也只有 72) |
| **②** | PlayMode `202/202/0/0` 判的是**旧树** `ad7316c`(08:55),未覆盖 `d91edeb`(10:22)对 `HistoryFolder.cs` 的改动 | 三元组只填了 SHA,**未核 commit 与最后一次生产代码改动的时间序** | 在干净树 `08049fb` 上重跑 | `202 / 202 / 0 / 0 / 0` @ `2026-10-22 11:33 (+0800)` · 批次 `s18-final-playmode-20261022` · commit `08049fb` · `error CS` = 0 |
| **③** | DoD 11 条在收口时**一条未勾**,而 QA 签核 / smoke / 代码评审 / 归档**均已交付** | 「收口」是隐含步骤而非显式流程 —— 靠记忆不靠 checklist | 逐条对着勾:8 勾 + 3 不勾(**每条带具名理由**,不勾的见 DoD 段 🔁 注)。🔁 **2026-10-22 二次收口**:余下 3 条经补轮后**全部勾上**(见下行) | `grep -c '^- \[ \]' production/sprints/sprint-18.md` ⇒ DoD 段内**0 条未勾**(原 3 条已逐条结清) |
| **③-b** | 余下 3 条不勾项 | 前一轮判「不勾」时,两条是**真缺口**(证伪轮作用域 / AC 被测物不存在),一条是**判断错误**(分母方向) | ① 分母方向 = 判据误读,已订正为勾;② 证伪轮 = 补 M1/M2 两轮打抹除面;③ AC 逐条 = 用户改判 S18-1 拉回并落地 AC⑦⑧ + 补 C-3 四轮 | 见 DoD 第 1/2/3 条的 🔁 落地表;回归 `4435/4399/0/36` |
| **④** | 回归三元组的批次名 `review-s18-r4-fix-20261022` **盘上无同名产物** | 只填批次名,**未验该名对应的文件存在** | 改指实际产物名 + 补 `artifact:` 字段(数值不变) | `ls unity/Logs/review-s18-r4-editmode-20261022.{xml,log}` 命中;`ls unity/Logs/review-s18-r4-fix-20261022.*` ⇒ 无此文件 |

**四条的共同形态**:**三条是「数字没跑就写了」**(①②④),一条是「产物齐了但流程没走」(③)。
⇒ 已升格为机制:`coding-standards.md` §Review Evidence Standards 新增两条 BLOCKING 级规则
(**数字须带可跑复算命令** · **「某件有门」须给 `file:line` + 可跑命令**)。

### 九.6 S18-1 ~~结转交接单~~ → **已拉回并在 sprint-18 落地**(2026-10-22 改判)

> 🔁 **2026-10-22 用户改判(原文不删)**:原裁「S18-1 结转 sprint-19」**作废** ——
> 用户裁定 **S18-1 拉回 sprint-18**。下方四条入口判据**已全部执行**,结果见 DoD 第 1 条的表。
> **本节的「交接单」语义随之失效**,保留原文仅作裁定沿革记录。
>
> **执行结果速览**:
> - **C-3 证伪轮 ≥3** → ✅ **四轮**(M1 `IsCombatant(Player)` / M2 白名单删一项 / M3 删 `LethalityOf` 合取 / M4 `IsCombatant(Beast)`),
>   每轮**预测红集由论证先行给出**且与实得逐条相同(M1 = 10 / M2 = 1 / M3 = 1 / M4 = 2)。
>   ⚠️ **M1/M4 不是「恰 1 红」** —— `IsCombatant` 是三条谓词的公共前提,改它必然连带;
>   这正是「`exactRed` 由论证给出」比「恰 1 红」更强的证据。
> - **AC ⑦** → ✅ `IDeathCandidateSource` + `IDeathCandidateSink` + `DeathCandidateConsumer`
> - **AC ⑧** → ✅ `PlayerDiedWriter` + `InjuryStateChangedWriter`,同批撤 `WriterExistenceGate` 两条豁免
> - **AC ③ 数值裁定** → 白名单五项**已于 2026-10-21 裁定**(`OQ-S18-1` = 双条件 `lethal(d) ∧ d ∈ {STAB, LACERATION, HEMORRHAGE, CLAW, BITE}`)
>
> 🔁 **2026-10-22 AC ⑥ 兑现(原文「仍未兑现」保留作闭环记录)**:`RuleNineGate`(源码 + IL 双证)
> 已落盘并接 `RunMenu` / `BuildGate`,探针轮三件套齐、还原 SHA-256 逐字节相同、干净轮绿。
> 详见上方 AC 表「签核件 ⑥」行。

<details>
<summary>原交接单(裁定沿革,原文不删)</summary>

`S18-1`(`TD-sim-01` / 门 G-1 `SelfLimited`)结转 sprint-19。**四条入口判据**如下,
sprint-19 规划时须**写进计划正文**(不是只放签核件),且**逐条可复算**:

| # | 入口判据 | 判据形态 | 当前缺口证据 |
|---|---|---|---|
| **C-3** | 谓词本体**证伪轮 ≥ 3** —— `IsCombatant` 真值表 / 白名单一项 / 三条件合取之一,各**恰红零附带** | 三份变异记录,每份含「改坏什么 ⇒ 哪条断言红 ⇒ 附带红 = 0」 | 签核件 D-2:谓词本体**零证伪轮**;已跑的 `R1_CHECK_31` 守的是 `kind` 零值,与本件无关 |
| **AC ⑦** | `IDeathCandidateSource` 接口 + **29 侧注入点**落地 | 生产树 `grep -rn "IDeathCandidateSource" unity/Assets --include=*.cs` 非空,且 29 有实现类 | 签核件 D-1:该符号与 `PlayerDeathWarranted` 在生产树 **0 命中** |
| **AC ⑧** | `PlayerDied` / `InjuryStateChanged` **两条写者**落地,**同批撤** `WriterExistenceGate` 豁免表 | 两条写者的 `Append` 调用点存在;`WriterExistenceGate.cs:98-102` 的「写者未落」条目**同批删除** | 同上:`WriterExistenceGate.cs:98-102` 仍挂「写者未落」 |
| **AC ③** | 数值 / 登记裁定 —— 白名单五项无数据行(`PRE-S18-A`)+ F4 三常量未登记为 registry 名块(`PRE-S18-B`) | 两项前置各有裁定记录,且数据行 / registry 名块可 grep | 两前置**均未解**,AC ③ 被卡住 |

⚠️ **另两条不随结转消失的约束**(见 §九.4):~~`R2-AC6` 规则九机械门仍 NOT-RUN~~
(**✅ 2026-10-22 已兑现**:`RuleNineGate` 落盘 + 双证 + 探针轮);
`OQ-7a-10` 仍未裁(影响 G-2 而非 G-1,但同在折叠面)。
> 🔁 **2026-10-24 订正**:`OQ-7a-10` = ✅ **已裁**(裁定 A,条件① 由 ∃ 收紧为 ∀)。本行原文作废,保留。
⚠️ **`S18-1` 在 sprint-19 占 L 额度**,而 sprint-19 还要承接 `TD-ci-01`(EXTERNAL)——
**规划时必须先算这一笔**(`retro-sprint-18` §Carryover Analysis)。
🔁 **2026-10-22 改判后本条失效**:S18-1 已拉回并在 sprint-18 落地,**不再占 sprint-19 额度**;
sprint-19 的 L 额度只剩 `TD-ci-01`(EXTERNAL)一笔。

</details>

> **不经用户指令不 commit。**
