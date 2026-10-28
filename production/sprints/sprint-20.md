# Sprint 20 — **`2026-10-25`** 起 · clinic 链尾 + A-2 机制起步 + 判别力债清偿

**周期**:`2026-10-25` 起 → **实际收口 `2026-10-27`**(不设结束日期口径,做完即收 —— 承 sprint-14…19 同例)。

> 🔁 **2026-10-28 重开(用户裁定「直接在 sprint-20 排一张装配轮的卡」)** —— 本 sprint 于 `2026-10-27`
> 收口,现**追加一个收口后工作项 `S20-17`**(World 场景装配轮),见 §十。
> ⚠️ **不推翻 §九 的收口判定**:收口时刻的读数(17/17 done · smoke PASS · 未闭 P0 面 63)
> 在**那一刻**为真;本批只新增一件,**既有 17 件状态一字未动**。
> ✅ **收口日 `2026-10-27` 已补记**。retro-19 行动项 3(收口四件套与最后一个实现轮**同日**)
> **本轮首次兑现** —— 最后一个实现轮 = S20-4 评审修复轮 + S20-7 治理盘点轮,均 10-27;
> 头部收口日 / §九 / retro / CAPACITY-MODEL 行四件**同日完成**。

**上游件**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` **§6.5:919** sprint-20 行(本 sprint 即其兑现)。
**前置**:sprint-18(G-8)✅ 已清偿(sprint-17 S17-4);sprint-19 四门 ✅ 全清。
**前序**:sprint-19 已收口(`production/sprints/sprint-19.md` §九,2026-10-25)。
**规划输入**:retro-19(`production/retrospectives/retro-sprint-19-2026-10-25.md`)行动项 4+5(盘点与 medcons 策略)+ 债册三十订四类分析(2026-10-25 会话)。

---

## Sprint Goal
交付路线图 §6.5 sprint-20 段的四件本体(clinic-004 / 005 · foraging-001 · processing-001,AC 半边按挂标),同轮清偿「判别力与测试卫生」债族 —— 本项目五次假绿事故的直接病灶面。

## Capacity(承 `production/CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束,**不估天**)
- 计划件:**7**(4M + 2S + 1S;复杂度并列,不比件数)
- 追加率预留:**1 件**额度(承 §3.1;⚠️ retro-19 实测追加以「轮」计结成新债,各件 note 附「将掀几债」预估 —— 行动项 2 首轮执行)

---

## Tasks

### Must Have(Critical Path)

| ID | Task | Agent/Owner | 复杂度 | Dependencies | Acceptance Criteria |
|----|------|-------------|--------|--------------|---------------------|
| `S20-1` | `clinic-004` 交付边界 ClinicEnvDto | unity-specialist | **M** | Story 003 ✅;21a clamp 契约、系统 1 的 K 消费接口(未就绪时**接口桩联测,`BLOCKED-BY` 不记绿**) | 3 条 AC 逐条判定 + 判据;桩联测半边如实挂标;证伪轮(新增/改写夹具) |
| `S20-2` | `clinic-005` 纯度重放 + Memoize | unity-specialist | **M** | Story 002–004 ✅;**S20-2a**(AC-24-11 布局源走真 `World` 后可记绿) | AC-24-10 / 24-11 逐条判定;~~**`TD-building-01` 残余两半就地裁定**~~ 🔁 **2026-10-25 已裁(用户裁定「两半全补」)**:补真 `World` 端到端 + `ModuleDefinition.Cost` 半边,**载体 = `S20-2a` 另计**(见下行);本卡本体照做,`AC-24-11` 布局源用真 `World` ⇒ 可记绿;裁定是 TD-building-01 债面内的执行选项,非重裁 |
| `S20-2a` | **TD-building-01 两半全补**(真 `World` 端到端 + `Cost` 读方) | unity-specialist | **M** | 23 epic 件(S17-6 已落读侧 4/6);数据半边 = `build_modules.json` ✅(TD-itemdb-01) | (a) 真 `World` 端到端:`Place → IsEffectivelyWalkable == false` / `Remove → true`(未经 `World` 的旧 e2e 不算);(b) `ModuleDefinition` 补 `Cost` + `DemolishChecker.CalculateRefund/ValidateRefundable` 从目录读(生产树调用点接通);写侧归 42(`O-23-6`)**不在面内**;结案时回写 `TD-building-01`(并核 `TD-itemdb-01` 的消费半边是否随之转结) |
| `S20-3` | `foraging-001` 品种闭包 + 数据装载校验 | unity-specialist | **M** | item-database 001/002 ✅ | AC-17-01b 等 4 条 AC 逐条判定;`item_key` 行未落地 ⇒ `Ignore("BLOCKED-BY: 21a item_key 行未落地")` 显式挂起,**禁记绿**(story :76 逐字) |
| `S20-4` | `processing-001` 配方子集 + 可采纳谓词 | unity-specialist | **M** | 21a(字段实测已非阻塞,措辞已订正 :75) | AC-18-18 三段非空前置逐条;若撞 `TD-medcons-11`(CLI 烘焙入口缺口)挂标不记绿 |

### Should Have

| ID | Task | Agent/Owner | 复杂度 | Dependencies | Acceptance Criteria |
|----|------|-------------|--------|--------------|---------------------|
| `S20-5` | **接受侧判别力收紧轮**(`TD-clinic-02 / 08 / 09`) | unity-specialist | **S~M** | 无 | ① **先变异坐实** `TD-clinic-02`(评审断言「删 `AssertOverflowBounds` 调用两条仍绿」系静态推断未实测;翻车即登记「推断失真」,只收紧坐实部分);② 收紧后每条收紧测配「停用守卫 ⇒ 红」反证(S12-4);③ `_n_max_boundary_floor_accepted` 等真判据化(只证解析回读 ⇒ 语义判据);④ 债册三条翻转(各结案块 + 三十一订) |
| `S20-6` | **`TD-prescription-07` 材质分支分类器** | unity-ui-specialist | **S** | 无 | 立 ink / brass / implement 三枚举分类器;`world_billboard_test.cs:87` 占位符改真断言;story-025 的 `test_ac025_6_formCarrier_staysInBrassBranch_notInk` **改打真分类器**(现类名断言,禁当已满足);债册翻转 |

### Nice to Have

| ID | Task | Agent/Owner | 复杂度 | Dependencies | Acceptance Criteria |
|----|------|-------------|--------|--------------|---------------------|
| `S20-7` | **治理盘点轮**(retro-19 行动项 4+5) | Claude | **S** | 无 | 四裁一次会,逐条留痕进债册:① W6–W10 五条逐条(承接 / 立债 / 显式豁免);② QA 2 条如实登记条件同批;③ medcons 20 条整块策略(等数值轮整块解锁 vs 挑前置无关子集先清);④ `TD-audio-026` 裁定(惰性构造 vs 只补测试侧 TearDown) |

---

## Carryover from Previous Sprint
| Task | Reason | New Estimate |
|------|--------|--------------|
| (无 story 结转 —— sprint-18 的 S18-1 已全额兑现进 S19-1) | — | — |
| W6–W10 + QA 2 条(登记面,非 story) | 非 BUG 级,转入 S20-7 盘点 | S20-7 面内 |

## Risks
| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| `TD-building-01` 处置裁定扩大 S20-2 面(补真 `World` 端到端 = 另一轮量级) | 中 | S20-2 超面 | 裁定显式二选一,不得默认扩面;补则立 `S20-2a` 另计 |
| `TD-clinic-02` 变异坐实翻车(断言其实有效) | 低 | S20-5 面缩小 | 坐实先行;翻车即登记「评审推断失真」 |
| `processing-001` 撞 `TD-medcons-11`(53 表 CLI 入口缺口) | 低 | 半边挂标 | 挂标不记绿;CLI 入口归运行期读方立案批 |
| 追加率再爆(入场即掀新债,承 sprint-19 九类轮面追加) | 中 | 容量 | 逐件「将掀几债」预评估(各 AC 列已注);超预留即裁减 Should Have |

## Dependencies on External Factors
- **数值轮(用户调值)不阻塞本轮四件本体** —— 机制不阻塞,表内容挂标(`foraging-001` 数值轮行同理)
- 用户实听 / 被试排期 / P1b(联机、VR)不进本轮

---

## Definition of Done for this Sprint

> 🔁 **2026-10-27 收口轮勾选**。口径与 sprint-19 同款:`[x]` 指「本条有明确判定 + 可复算判据」,
> **不表示「全部满足」**。表内出现「⚠️ 不勾 + 具名理由」的行**不违反**本条。

- [x] All Must Have tasks completed(本体交,半边挂标**不得记 `Complete`**)
      > 🔁 **2026-10-27 订正(本条已由「部分完成」变为「全部完成」)**:原判「计划内 7 件全 `done`,
      > 追加批 4 件未完成(S20-8 `ready-for-dev` · S20-13/14/15 `backlog`)」。
      > 同日用户下达全链实现指令后,**17/17 全部 `done`** ⇒ 本条**不再需要按「计划内 7 件」的窄口径判**,
      > 全量口径下亦成立。复算:
      > ```bash
      > python3 -c "import yaml,collections;d=yaml.safe_load(open('production/sprint-status.yaml'));print(len(d['stories']),collections.Counter(s['status'] for s in d['stories']))"
      > # 实跑:17 Counter({'done': 17})
      > ```
      > > 🔁 **原记录(原文不删)**:计划内 7 件(S20-1…S20-7)**全 `done`**;追加批 4 件未完成
      > > (S20-8 `ready-for-dev` · S20-13/14/15 `backlog`)—— 见 §九「不记绿项」①。
- [x] All tasks pass acceptance criteria(逐条判定 + 判据;禁「已落盘」当判据)
      > 逐件判定表见各 story `## Completion Notes` 的 AC 映射表 + 证据件。**有 5 处「有判定但不勾」**:
      > `AC-18-15`(S20-9,EXTERNAL)· `AC-20-03`/`AC-20-16`(S20-10,结构性不可勾)·
      > S20-11 的 P0 门 ①②(BLOCKED-BY)· `AC-18-10` 第三条件 + `AC-18-20`(S20-4,桩/结构性)。
      > 它们**有判据、有具名理由**,故本条成立;但**不得读成「全绿」**。
- [x] QA plan exists(`production/qa/qa-plan-sprint-20-*.md` —— **本轮规划批即跑 `/qa-plan sprint`**)
      > `production/qa/qa-plan-sprint-20-2026-10-25.md`(2026-10-25 落盘,规划批当日执行)。
- [x] All Logic/Integration stories have passing unit/integration tests
      > 🔁 **2026-10-27 订正(链尾四件完成后 · 本条为最新)**:原记「**COVERED 10 · EXPECTED 6 · MISSING 0**」,
      > 那是 smoke 报告执行时刻(`smoke-fix` @ 01:13–01:20,commit `5bdd226`)的快照。同日
      > **S20-4 / S20-8 / S20-13 / S20-14 / S20-15 五件全部落地**且各有夹具有实测用例数 ⇒
      > 现为 **COVERED 16 · EXPECTED 1 · MISSING 0**(仅剩 `S20-7` 治理轮,零新代码,载体是债册与证据件)。
      >
      > | 件 | 载体 | 用例 |
      > |---|---|---|
      > | S20-4 | `EditMode/Processing/admissible_predicate_test.cs` | 37 `[Test]` |
      > | S20-8 | `EditMode/Processing/f1_call_contract_test.cs` | 39 `[Test]` + 12 `[TestCase]` |
      > | S20-13 | `EditMode/Processing/craft_ignition_guards_test.cs`(+ `PlayMode/craft_event_payload_test.cs`) | 15 `[Test]` |
      > | S20-14 | `EditMode/Inventory/drop_event_writer_guards_test.cs` + `no_negative_ack_test.cs`(+ `PlayMode/inventory_atomicity_test.cs`) | 32 + 8(9) |
      > | S20-15 | `EditMode/Foraging/harvest_novelty_wiring_test.cs` | 11 `[Test]` |
      >
      > **复算(可直接跑)**:
      > ```bash
      > cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
      > grep -c '^| S20-' production/qa/smoke-2026-10-27-sprint20.md            # 17
      > grep -c '\[Test\]' unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs        # 37
      > grep -c '\[Test\]' unity/Assets/Tests/EditMode/Processing/f1_call_contract_test.cs            # 39
      > grep -c '\[TestCase' unity/Assets/Tests/EditMode/Processing/f1_call_contract_test.cs          # 12
      > grep -c '\[Test\]' unity/Assets/Tests/EditMode/Processing/craft_ignition_guards_test.cs       # 15
      > grep -c '\[Test\]' unity/Assets/Tests/EditMode/Inventory/drop_event_writer_guards_test.cs     # 32
      > grep -c '\[Test\]' unity/Assets/Tests/EditMode/Inventory/no_negative_ack_test.cs              # 8
      > grep -c '\[Test\]' unity/Assets/Tests/EditMode/Foraging/harvest_novelty_wiring_test.cs        # 11
      > ```
      >
      > ⚠️ **「有测试」≠「有独立评审」** —— 本条只判**证据存在且通过**;
      > 评审面归 DoD 第 9 条,两者**不得合并记账**。
      >
      > > 🔁 **原记录(原文不删)**:COVERED 10 · EXPECTED 6 · MISSING 0。6 条 EXPECTED = 4 件未开工
      > > (S20-8/13/14/15)+ S20-4/S20-7 在 smoke 执行时刻尚未落地。
- [x] 每新增/改写夹具过证伪轮(同命令改→跑→还原,SHA-256 逐字节;一属性一条变异)
      > S20-1(F1–F3)· S20-2(F7 + 11/11)· S20-2a(四支)· S20-3(第四轮 7/7)· S20-4(M1–M8 + P1,8/8)·
      > S20-9(14/14)· S20-10(14/14)· S20-11(14/14)· S20-12(五轮 10/13→17/17)· S20-16(13 条全红)。
      > **S20-4 与 S20-7 的证伪轮有 `FALSIFY-` 零残留 + SHA-256 逐字节还原实测记录。**
- [x] Smoke check passed(`/smoke-check sprint`)
      > `production/qa/smoke-2026-10-27-sprint20.md` —— **Verdict: PASS**(EditMode 5385/5350/0/35 ·
      > PlayMode 204/204/0/0)。⚠️ 该 PASS 的边界已在报告内写明:A–D 22 条是 **N/A 不是 PASS**。
      > 🔁 **2026-10-27 订正**:上数是 `smoke-fix` 轮(commit `5bdd226`)的快照。链尾四件落地后
      > 全量为 **EditMode `5529 / 5494 / 0 / 35`** · **PlayMode `229 / 228 / 0 / 1`**
      > (三元组 `2026-10-27T09:34Z` · `S20-15 批` · commit `e095b7b`)⇒ **PASS 判定不变**(零红依旧)。
      > ⚠️ 但 **A–D 22 条仍是 N/A** —— 「PASS」在本 sprint 只表示自动化套件零红 + 用户确认无异常,
      > **不表示游戏能玩**。详见 smoke 报告 §三度实测。
- [x] QA sign-off:APPROVED 或 APPROVED WITH CONDITIONS(`/team-qa sprint`)
      > `production/qa/qa-signoff-sprint-20-2026-10-27.md` —— **APPROVED WITH CONDITIONS**(C-1…C-8;
      > 首版 6 条,10-27 写 C-3 加注时核表核出 C-7 / C-8 两条新缺陷后追加)。
      > ✅ **已闭 4 条**(C-3 加注 · C-6 收口提交 · **C-7 补 `S20-2a` 行** · **C-8 头部改实测值**),
      > 🔚 **裁定接受 2 条**(**C-1 / C-4 不转**,2026-10-27 用户),**未动 2 条**(C-2 EXTERNAL · C-5)。
      > ⚠️ **真实状态 = 修好 4 · 接受 2 · 未动 2**,**不得读成「8 条全清」**。
- [x] No S1 / S2 bugs
      > `production/qa/bugs/` 仅 `README.md`,零 `BUG-*.md`(复算命令见 QA 签核件 §Bugs Found)。
- [x] 设计文档偏离已更新;代码已评审(双列结账表 + Branch 列)
      > 🔁 **2026-10-27 补评轮订正**:上段「4 件无独立评审」**已消解** —— S20-10 / S20-11 / S20-12
      > 三件的独立评审原件已补落盘(`review-s20-10-inventory-001` / `-11-inventory-003` /
      > `-12-inventory-004`,均 `2026-10-27`)。
      >
      > | 件 | 原件 | 判定 |
      > |---|---|---|
      > | S20-10 inventory-001 | ✅ `review-s20-10-inventory-001-2026-10-27.md` | **CHANGES REQUIRED**(B-1 / B-2) |
      > | S20-11 inventory-003 | ✅ `review-s20-11-inventory-003-2026-10-27.md` | **CHANGES REQUIRED**(B-1) |
      > | S20-12 inventory-004 | ✅ `review-s20-12-inventory-004-2026-10-27.md` | **APPROVED WITH SUGGESTIONS**(W-1) |
      >
      > ⇒ **「有没有评审」这一条已成立**(10/10 有原件或具名理由)。⚠️ 但**「评审说了什么」是另一回事**:
      > 三份里**两份是 `CHANGES REQUIRED`**,共抓出 3 条本 sprint 之前从未发现的缺陷
      > (fold 零接线 / 门零调用点 / 同义反复断言)。**这些缺陷不因本次补评而消失** ——
      > 它们是**新开的**工作项,须进 sprint-21 候选或债册。
      > ⇒ 本条的 ✅ 只表示「评审动作已完成」,**不表示「代码已通过评审」**。禁混读。
      >
      > > 🔁 **2026-10-27 就地加注(原文不删)**:上句「须进 sprint-21 候选或债册」**已被用户推翻** ——
      > > 「不进21也不记债,现在完成」⇒ 三条**当场修完**(详见本节末尾的处置表与三份原件 §六)。
      >
      > > 🔁 **原记录(原文不删)**:部分成立 —— 有独立评审原件的 = S20-1 / S20-2 / S20-3 / S20-4 / S20-9 / S20-16;
      > > S20-10 / S20-11 卡内明写「本轮未走评审」,S20-12 零陈述 ⇒ 「已评审 6 / 计划内 10 件,
      > > 4 件无独立评审」;用户当时裁定「不补评(C-4 不转)」—— **该裁定已被后续「1 完成」指令覆盖**。
- [x] **收口四件套同日化**(retro-19 行动项 3):头部收口日 + §九 + retro + CAPACITY-MODEL 行
      > **四件均 `2026-10-27` 完成** —— 本 sprint 是 retro-19 行动项 3 立规后的**首次执行**,零滞后。

---

## 边界(本 sprint 不做什么,防 scope 漂移)
- **不做数值轮**(用户调值;`TD-medcons-01/02` / `TD-audio-025` 等前置类债不碰)
- **不做 P1b 面**(`TD-net-01` / `TD-audio-104/107` / 联机、VR)
- **不做 sprint-23 death-respawn 前置**(`TD-sim-01` / `TD-clinic-18` —— 现在做属提前,触发未到)
- **不改任何 ADR 裁决**(S20-2 的裁定是 TD-building-01 债面内的执行选项,非重裁)
- ** clinic / audio 大块其余债**(未逐条展开者)不进本轮 —— 待 S20-7 ③ 的 medcons 策略裁定后再排

---

## 清债总体规划(已移出 —— 指针)

> 🔁 **2026-10-25 用户裁定**:本节属**总体 sprint 规划**面,不属于单轮 sprint 卡面 ⇒
> **整节移入** `production/milestones/p0-remaining-roadmap-2026-10-20.md` **§6.5.3**(逐字搬移,内容未改)。
> 原文本节不保留副本(挪移非删除);本指针防「引用却无登记」反向形态 —— 引到 sprint-20.md 的旧链接落空时可循此找到新址。

---

## §九 收口判定(2026-10-27 · 收口轮)

**Verdict: ✅ 已收口(带具名残余)** —— 计划内 7 件全 `done`,判据面成立;
**追加批 4 件未完成,用户裁定「不转」**;另有 5 处「有判定不勾」项逐条具名。**无未登记项**。
> 🔚 **2026-10-27 用户收口裁定:「C-1 和 C-4 不转,本sprint完成」** ——
> 4 件未完成 story **不做 sprint-21 承接承诺**,三件承重件**不补评审**。
> ⚠️ 这是**风险接受,不是缺陷修复** —— 真实状态是「修好 4 条 · 接受 2 条 · 未动 2 条」,
> **不得读成「8 条全清」**。详见 QA 签核件 §收口裁定。

### 9.1 收口要件

| 收口要件 | 状态 | 凭据 |
|---|---|---|
| **计划内 S20-1…S20-7** | **7/7 全 `done`** | `production/sprint-status.yaml`(completed 2026-10-25…10-27) |
| **追加批 S20-8…S20-16** | **9/9 `done`** 🔁 | 同上;收口轮记的是「5/9 done · 未完成 4 件」,**同日链尾四件补齐后转 9/9** —— 见 §9.3 ①(该条已被事实推翻) |
| **🔁 收口后追加批 S20-17…S20-20** | **4/4 `done`** | 同上。**收口后才发生**(2026-10-28 用户裁定「直接在 sprint-20 排一张装配轮的卡」+ 后续三件)⇒ 本行是 §九 收口判定的**事后追加**,不推翻原判定。逐件证据见 §十三。<br>🔁 **2026-10-28 就地订正(原文不删)**:原记「4 件的挂标**一条未勾**(`S20-17` 5 · `S20-18` 6 · `S20-19` **7** · `S20-20` 5)」**两处已失效** ——<br>① **`S20-19` 实为 9 条**,不是 7:`:815` 是实施前预估 7,实施轮 `:854` 补 #8/#9 至 9,收口轮 `:896` 再确认 9 ⇒ §9.1 那个 7 是**未随实施轮更新的预估数**。<br>② **「一条未勾」不成立** —— 后续卡片已兑现 6 条全闭 + 1 条部分闭 + 1 条原裁定被推翻。<br>**逐条现状见 §9.1.1**:总数 **25 条 · 全闭 6 · 部分闭 1 · 原裁定被推翻 1 · 仍开 17**。<br>⇒ **`done` 只表示「交付物已落 + 有证据 + 有评审原件」,不表示缺口已闭**(此句仍成立,只是「闭了几条」变了) |
| DoD 10 条 | 全勾,零 `[ ]`(带 5 处加注) | 本文 §Definition of Done `:71-130` |
| smoke-check | **PASS**(两版数,禁混读) | `production/qa/smoke-2026-10-27-sprint20.md` —— 收口轮 `EditMode 5385/5350/0/35` · `PlayMode 204/204/0/0`;🔁 **链尾收口后**:`EditMode 5529/5494/0/35` · `PlayMode 229/228/0/1`(@ `2026-10-27T09:34Z` · `S20-15 批` · `e095b7b`) |
| QA 签核 | **APPROVED WITH CONDITIONS** | `production/qa/qa-signoff-sprint-20-2026-10-27.md`;C-1…C-8(首版 6 条,10-27 追加 C-7/C-8)。🔁 **收口后进展**:C-1(事实消解)· C-3 / C-4 / C-5 / C-6 / C-7 / C-8 **已闭**;余 **C-2(EXTERNAL,`TD-ci-01`)** —— 🔁 **2026-10-27 再收窄**:两格矩阵已实跑绿,`AC-18-15` 仍缺 processing 侧黄金夹具 + ARM64 格 + CI 常驻。<br>⚠️ **C-4 闭的是「有没有评审」,不是「代码通过评审」** —— 补评的三份里两份判 `CHANGES REQUIRED`,抓出 3 条新缺陷 |
| 证伪轮 | 每件 ≥1 轮,全 exact | 见 DoD 第 5 条注;S20-4 / S20-7 有 `FALSIFY-` 零残留 + SHA-256 逐字节记录 |
| 收口提交 | ✅ **已落 —— 即本提交** | 复算:`git log -1 --format='%h %s' -- production/sprints/sprint-20.md`;<br>sprint-20 实现区间 = `9bbef11`..`51193cf` 共 **41 笔**,收口提交为其后第 **3** 笔(`0a64faf` S20-4 · `34c75e9` S20-7 · 本笔四件套) |
| 债册复算 | **已实跑** | 见 §9.2(`--print` 实跑输出,`--check` exit 0) |
| retro | 已落 | `production/retrospectives/retro-sprint-20-2026-10-27.md` |
| CAPACITY-MODEL 三元行 | 已补 | `production/CAPACITY-MODEL.md` 表 20 行 |

### 9.1.1 四件挂标逐条现状(2026-10-28 · 承上表 🔁 订正)

**这张表存在的理由**:§9.1 原记「一条未勾」,而实际已有 6 条全闭 + 1 条部分闭 + 1 条原裁定被推翻。**不逐条点名,
下一个人只会看到两个互相矛盾的数字**(一个说 23 条全开,一个说 6 条已闭),然后去猜哪个对。

**总数 25 · 全闭 6 · 部分闭 1 · 原裁定被推翻 1 · 仍开 17。** 复算:`S20-17` 5 + `S20-18` 6 + `S20-19` **9** + `S20-20` 5 = **25**;
逐卡分类:全闭 `2+1+1+2=6` · 部分 `0+1+0+0=1` · 推翻 `1+0+0+0=1` · 仍开 `2+4+8+3=17`。

**四类状态的含义(须分清,混读即失真)**:
- **✅ 全闭** —— 该条缺口被后续卡片**完整**补上。
- **🟡 部分闭** —— 补上了一部分,剩余部分仍在(如 S20-18 #4:13 格 ≠ 任意格)。
- **🔁 原裁定被推翻** —— 本卡当初**明确裁定**「不做」,后续卡片**改变了那个裁定**(如 S20-17 #5)。
  ⚠️ **这不是「当初记错」** —— 裁定在当时成立;变的是后来的决定。
- **仍开** —— 缺口仍在。

| 卡 | # | 现状 | 判据(可跑) |
|---|---|---|---|
| **S20-17** | 1 | **仍开** —— `world_geometry` 零产出 | `ls unity/Assets/DataCooked/world_geometry*` ⇒ 无此文件 |
| | 2 | **✅ 已闭** —— `StructureWriter.Place` 生产发起方 = 42 | `grep -rn "\.Place(" unity/Assets --include=*.cs \| grep -v "/Tests/" \| wc -l` ⇒ **1**(`BuildController.cs:146`) |
| | 3 | **仍开** —— `ClinicCellSource` 仍是空集占位 | `ClinicCellSource.cs:51` 自陈「当前恒 false —— 空集占位」;`ConfiguredCount` 全仓**零生产调用方**(`grep -rn "ConfiguredCount" unity/Assets --include=*.cs \| grep -v "/Tests/"` ⇒ 仅定义 `:55` 一处) |
| | 4 | **✅ 已闭** —— 裁定 A:`World` 归 `BootRoot`,卡内留痕 | 本节上方「实施裁定 A」 |
| | 5 | **🔁 原裁定已被推翻** —— 裁定 B 写「本轮**不接** `PlaceableChecker`」,而 **S20-19 当日晚就接了** | `CompositionRoot.cs:979-980` `var placeableChecker = new PlaceableChecker(...)`,并作实参传入 `:985` 的 `new BuildController`。⇒ **本条不是「仍开」,是「原裁定已被后续卡片改变」** —— 原记录在当时成立,不追认 |
| **S20-18** | 1 | **✅ 已闭** —— 同上(S20-17 #2 的同一判据) | 同上一行命令 |
| | 2 | **仍开** —— ADR-023 §⑤ 拆序不在面内 | 归 `World.unity` additive 加载轮 |
| | 3 | **仍开** —— `RequestLoad` 零调用方(通电待信) | `grep -rn "RequestLoad" unity/Assets --include=*.cs \| grep -v "/Tests/"` ⇒ **2 命中,均非调用**:`BootRoot.cs:874` 是定义,`:880` 是它自己的报文串 |
| | 4 | **🟡 部分闭** —— 目录**不再空**(13 格),但 `AC-23-08` 第三半边**只覆盖这 13 格** | `grep -c "RegisterSlot" unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs` ⇒ **4**;⚠️ `MinimalWorldFactory.cs:41-44` 自陈「**仍只是 13 格骨架** —— 覆盖面 = 那 13 格,**不是任意格**」⇒ **不得标全闭** |
| | 5 | **仍开** —— `Modify` 只覆盖足迹,朝向合法性归上游 | 设计如此,非缺口 |
| | 6 | **仍开** —— 在场视图 / 视觉侧不在读档重建面 | 设计如此,非缺口 |
| **S20-19** | 1–7 | **仍开** —— 见 `:815` 原列七条(幽灵预览 / 建造模式进入 / 拒绝原因 UI / 客机上行 / 质量门 / 拆除返还落点 / 音效) | 各自归属系统未实现 |
| | **8** | **✅ 已闭(全闭)** —— `BuildSlotCatalog` 生产侧由 **S20-20** 填充;**生产可达性亦已成立** | 三段闭合:① 槽位装载 `MinimalWorldFactory.cs` 的 `RegisterSlot` ×4;② 判定器装配 `CompositionRoot.cs:979-980`;③ 发起方装配 `:985` `new BuildController(...)`。⇒ `PlaceableChecker.Check` **不再恒 `SlotNotFound`**。<br>⚠️ 但**「能判定」≠「建得成」** —— 见下一行 |
| | 9 | **仍开** —— XR 绑重物理键重叠,归 P1a | P0 无 VR 建造 |
| **S20-20** | 1 | **✅ 已闭** —— §十二 修复轮(Addressables 装载面) | 原文已划掉,原件 `fix-s20-20-slot-provider-surface-2026-10-28.md` |
| | 2 | **✅ 已闭** —— 挂标 #2 修复轮(`BakedInitial` 运行期应用) | 原文已划掉,原件 `fix-s20-20-baked-initial-baseline-2026-10-28.md` |
| | 3 | **仍开** —— C1–C6 七项未实现 | `LevelConsistencyCheck.cs:57-66` 仍是 **7** 条;复算:`sed -n '/UnimplementedChecks =/,/};/p' unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs \| grep -c '^            "'` ⇒ **7**。承接者 `level-tool/story-006` |
| | 4 | **仍开** —— 关卡工具只服务一个关卡 | `P0ClinicLevel.cs:37` `Width = 16` · `:46` `LevelId = "p0_clinic"`(实测);承接者 `level-tool/story-001` |
| | 5 | **仍开** —— 世界几何仍是合成 | `MinimalWorldFactory.cs:43` 自陈「地形全 walkable + POI / 资源格全 `-1`」;复算:`ls assets/data/world_*.json \| wc -l` ⇒ **1**(ADR-022 §三 要 7 件) |

⚠️ **两处口径须同读**:
① **「已闭」= 该条的缺口被后续卡片补上**,不是「本卡当初记错了」。原记录在各自时点都成立。
② **`S20-19` 的 #1–#7 未逐条复验** —— 上表对它们只写「仍开」,判据是「各自归属系统未实现」,
**这是设计层面的断言,不是本轮实测**。若要把它们也转成实测判据,须逐条给出可跑命令;
本轮**未做**(如实登记,禁借绿)。

### 9.2 债册复算(承 `coding-standards.md` §数字须带可跑复算命令)

命令(自包含,当场可跑):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 tools/debtcount/debtcount.py --print     # 输出块
python3 tools/debtcount/debtcount.py --check     # exit 0 = 与正文一致
```

实跑输出(`2026-10-27` 收口轮):

```text
条目 127 | 状态 {'open': 77, 'closed': 44, 'partial': 5, '**done**(2026-10-21)': 1} | P0 面 {'Y': 102, 'N': 24, '~~**Y**~~': 1}
未闭 P0 面 = 63 （已闭但曾标 P0 面的 39 条不计）
M3 Exit 第二条合取 ⇒ 假 —— 63 条,分布在 10/31 个 epic
    medical-consequences: 19  audio-system: 17          clinic-machine: 15
    telemetry-analytics: 5    disease-simulation: 2     enemy-ai: 1
    item-database: 1          patient-ai: 1             processing: 1
    skeuomorphic-ui: 1
```

**净变化(起手 = sprint-20 首笔 `9bbef11` 的债册快照,`git show 9bbef11:docs/tech-debt-register.md`)**:

| 列 | 起手 | 收口 | 净 |
|---|---|---|---|
| 条目 | 109 | **127** | **+18** |
| `closed` | 34 | **44** | **+10** |
| `open` | 68 | **77** | **+9** |
| `partial` | 6 | **5** | **−1** |
| **未闭 P0 面** | **69** | **63** | **−6** |
| **epic 分布** | **9/31** | **10/31** | **+1** |

> ⚠️ **`未闭 P0 面 −6` 是 10 条结案 与 4 条新立 P0 面债 相抵后的净值**(结案 10 − 新立 P0 面 4 = 6)。
> 新立 18 条中 **14 条为 `P0 面 = N`**(治理/跨 epic/P1a 面),不进入分子。
> `modular-building` 清零(0 条)但 `enemy-ai` / `processing` 双双重回分布 ⇒ epic 数净 +1。
> 复算命令(全名差集,非总数巧合):
> ```bash
> git show 9bbef11:docs/tech-debt-register.md > /tmp/td-start.md
> python3 - <<'PY'
> import re
> def rows(p):
>     t=open(p,encoding='utf-8').read()
>     b=re.search(r'<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->',t,re.S).group(1)
>     return {l.split('|')[1].strip().strip('`'):[c.strip() for c in l.strip().strip('|').split('|')]
>             for l in b.split('\n') if l.startswith('| `TD-')}
> a,b=rows('/tmp/td-start.md'),rows('docs/tech-debt-register.md')
> print('新增',len([k for k in b if k not in a]),'消失',len([k for k in a if k not in b]))
> print('结案',[k for k in a if k in b and a[k][2]!=b[k][2]])
> PY
> # 实跑:新增 18 消失 0
> # 结案 10 条:TD-audio-026/028/030 · TD-building-01 · TD-clinic-02/08/09
> #            · TD-itemdb-01(partial→closed)· TD-medcons-16 · TD-prescription-07
> ```

### 9.3 不记绿项(如实,非缺口)

**① 追加批 4 件未完成 —— 🔚 用户裁定「不转」→ ✅ 2026-10-27 事实消解**

> 🔁 **本条已被后来的事实推翻(原文不删,留作闭环记录)**。
> 用户 2026-10-27 裁定「不转」后,**同日**又下达 `S20-8 → S20-13 → S20-14 → S20-15` 全链实现指令
> ⇒ 四件**全部落地并转 `done`**(含 S20-15 的强制证伪轮)。现在 `sprint-status.yaml` 为 **17/17 done**。
>
> 🔁 **2026-10-28 再注**:上句的 **17/17** 是**收口时刻**的读数;§十 追加 `S20-17` 后该分母已变 ——
> 现为 **17/18 done**(新增那件 `ready-for-dev`)。**17 件的 `done` 状态一字未动**,
> 变的只是分母。引本行时须连读本注,不得把「17/17」当成当下值。
> 复算:`python3 -c "import yaml,collections;d=yaml.safe_load(open('production/sprint-status.yaml'));print(len(d['stories']),collections.Counter(s['status'] for s in d['stories']))"`
> ⇒ `17 Counter({'done': 17})`。
>
> | 件 | 原状态 | 现状态 | 完成日 | 评审原件 |
> |---|---|---|---|---|
> | `S20-8` processing-002 F1 调用契约 | `ready-for-dev` | **`done`** | 2026-10-27 | `review-s20-8-2026-10-27.md` |
> | `S20-13` processing-003 Craft 落流 | `backlog` | **`done`** | 2026-10-27 | `review-s20-13-2026-10-27.md` |
> | `S20-14` inventory-005 事务模板 + **Drop\* 三支写者** | `backlog` | **`done`** | 2026-10-27 | `review-s20-14-2026-10-27.md` |
> | `S20-15` foraging-006 novelty 接线 | `backlog` | **`done`** | 2026-10-27 | `review-s20-15-2026-10-27.md` |
>
> **连带解除的两条后果**(上文 `🔚 不转` 段列的风险):
> - ✅ `S20-14` 落地后 `AC-17-01` **已解除挂起并勾选**(story-006 §AC-17-01 的 2026-10-27 注)。
> - ✅ 「下次 sprint 规划轮必须重新发现这 4 件」**不再需要** —— 它们已 `done`。
>
> ⚠️ **但「容量超支」这条不变**:追加批 8 件**全部真的做了**,超出 1 件预留的口径
> 比原判断**更严重**,不是更轻。该重估义务仍归下次 sprint 规划轮。

**② 4 件无独立评审 —— 🔚 用户裁定「不转」→ ⚠️ 部分消解(3 件已补,1 件未补)**

> 🔁 **2026-10-27 补评轮(本 sprint 收口后)**:用户下达「1 完成」(C-4)—— 补 S20-10 / S20-11 / S20-12
> 三件的独立评审原件。**三件全部交付**,但**判定不是「齐了就清」**:
>
> | 件 | 原件 | 判定 | 最重的一条 |
> |---|---|---|---|
> | `S20-10` inventory-001 | `review-s20-10-inventory-001-2026-10-27.md` | **CHANGES REQUIRED** | B-1 `InventoryFold.Fold` **零生产调用点**,且对真实流 `IsComplete` 恒 `false` ⇒ `RequireComplete()` 必抛 |
> | `S20-11` inventory-003 | `review-s20-11-inventory-003-2026-10-27.md` | **CHANGES REQUIRED** | B-1 `BuildPartQualityGate` **零调用点** = 第五形态假绿(AC 记绿的执法件不存在) |
> | `S20-12` inventory-004 | `review-s20-12-inventory-004-2026-10-27.md` | **APPROVED WITH SUGGESTIONS** | W-1 两条同义反复断言冒充证明(`test:287` / `test:329-331`) |
> | S20-8 / S20-13 / S20-14 / S20-15 | 各件 `review-s20-1x-2026-10-27.md` | — | 本就有 |
>
> ⚠️ **三条结构性发现,本 sprint 之前从未被发现**:
> - **S20-10 B-1**:inventory 的 fold 口径**从未接进生产** —— 这是本条②一直担心的
>   「fold 口径未获评审背书」的**实证**,而且比担心更糟(不是口径错,是**没接**)。
> - **S20-11 B-1**:`BuildPartQualityGate` 零调用点 = **第五形态假绿**
>   (「某件有门」的自述,实为一个从未被调用的类)。
> - **S20-12 W-1**:两条同义反复断言(`long before = load; … EqualTo(before)` 与
>   `long load = 10L; … LessThanOrEqualTo(cap)`)—— 与「自造断言顶替 AC 语义层」先例**完全同型**。
>
> ⇒ **评审判定本身是新的缺陷面**,须另立工作项。**不得**因「三份原件齐了」就把本条记成已清 ——
> 恰恰相反,补评把它从「未评审的风险」变成了「**已证实的缺陷清单**」。

> 🔁 **2026-10-27 就地加注(原文不删)**:**「须另立工作项」这一处置已被用户推翻**。
> 用户原话:「**不进21也不记债,现在完成**」⇒ 三条发现**当场修完**:
>
> | 原发现 | 处置 | 落点 |
> |---|---|---|
> | `InventoryFold.Fold` 零生产调用点 | ✅ 建「装载库存实体」链 | `Gameplay.Boot/InventoryLoadService.cs`(新建)· `CompositionRoot` 装配 · `LoadSequencer` 读档后钩子 |
> | `BuildPartQualityGate` 零调用点(**第五形态假绿**) | ✅ 接上唯一强制点 | 同上 `EnforceBuildPartQuality`(判据面 = **装载期**) |
> | 两条同义反复断言 | ✅ 改为由被测件产出被断言值 | `carry_load_adjudication_test.cs` |
>
> **证伪**:5 条变异 5/5 全红 · 5 个文件 SHA-256 逐字节还原。
> **回归**:`5552/5517/0/35`(`5529 + 23` 闭合)。
> **债册零新增**:`grep -c "TD-inventory" docs/tech-debt-register.md` ⇒ **0**。
> ⚠️ 未修完的:`IsComplete` 仍恒 false(R16–R18 载荷缺位)· 20b 呈现侧零改动 ·
> `FoldWeight` / `StacksOf` 仍零生产调用点 —— 逐条见三份原件 §六。
> > 🔁 **2026-10-27 就地订正(原文不删)**:本行原串里还有 **`CanCarry`**,**是错的** —— 它
> > 有 **3 处**真生产调用点(`CarryCapExistenceGate.cs:168` / `IgnitionAdmission.cs:137` /
> > `InventoryTransaction.cs:220`),`review-s20-12:139` 原文本就记「✅ 已接线」。
> > 复算:`cd unity && grep -rn "CarryLoadSolver\.CanCarry(" Assets --include=*.cs | grep -v "^Assets/Tests/" | wc -l` ⇒ **3**。
> > ⚠️ **不要**用不带类名前缀的 `grep "CanCarry"` —— 它把**定义行 + 两处文档注释**也数进去,得 **6**
> > (本轮初稿就写错过一次,当场被复算抓回)。
>
> 复算:`ls -1 production/qa/evidence/review-s20-1[012]-*.md | wc -l` ⇒ **3**

**②-原记录(原文不删)** S20-10 / S20-11 卡内自陈未走评审,
S20-12 零陈述且无原件;S20-2a / S20-5 / S20-6 / S20-7 为债册工作项,载体是证据件。
⇒ **不计入「代码已评审」**。

> 🔚 **原裁定(2026-10-27,现已被「1 完成」指令推翻)**:不补评。后果登记为
> 「inventory 的 fold 口径未获评审背书 —— 后续若因它返工,成本不追溯到本 sprint」。
> ✅ **该后果已消解**:补评轮直接抓出 B-1 / B-2,证明这条「未评审」的判断当时是对的
> —— **未评审的风险确实存在,且确实被后来的评审证实了**。

**③ 5 处「有判定不勾」的 AC**(均有判据、有具名理由,禁读成全绿):

| 件 | AC | 不勾理由 |
|---|---|---|
| S20-9 | `AC-18-15` | **EXTERNAL · BLOCKED-BY-ADR-012** —— 三格矩阵不存在 + F7 spike 未跑,不写桩<br>🔁 **2026-10-27 收窄(仍不勾)**:两格矩阵(Mono + IL2CPP-x64)已实跑绿(`tools/determinism-matrix/run_matrix.py`,五条判据全过);**缺** ① processing 侧黄金夹具(矩阵测的是 item-database 的 19 条,非 processing 三出参)② ARM64 格(目标不存在)③ CI 常驻(EXTERNAL)。见 `production/qa/evidence/ac18-15-two-cell-matrix-2026-10-27.md` |
| S20-10 | `AC-20-03` / `AC-20-16` | 前置核实揭出 R15/R16/R17/R18 四条载荷缺位 ⇒ **结构性不可勾** |
| S20-11 | P0 门 ①②(容器 qty/quality 恒 1) | **BLOCKED-BY Story 010** —— P0 无「这是容器吗」的判据载体 |
| S20-4 | `AC-18-10`(2/3 + 桩) | 炉半边是 Spy 桩,真装配归 Story 004 |
| S20-4 | `AC-18-20` | **结构性证明**(两条路径零生产调用点),真接线归 42 / Story 002 |

**④ smoke 的 A–D 22 条是 `N/A` 不是 `PASS`**
`tests/smoke/critical-paths.md` 自述「当前是清单,不是可执行门」,五组「当前可跑?」全 ❌
⇒ 22 条一律 N/A。已立债 **`TD-smoke-01`**(`P0 面 = N`)。**本 sprint 的可玩面未被任何自动化判据覆盖。**

**⑤ 本轮新立的 P0 面债 4 条**(净 −6 的分子项,须与净值同读):

| 债 | epic | 来源 |
|---|---|---|
| `TD-clinic-20` | clinic-machine | S20-2 收口(24→42 通道未收敛) |
| `TD-enemy-03` | enemy-ai | S20-7 盘点(W7a:`acc = Fix.Zero` 于路径耗尽,F-27-4 无此定义) |
| `TD-itemdb-02` | item-database | S20-4 轮(金标准无发号场景) |
| `TD-processing-07` | processing | S20-4 轮(`AC-18-15` 的 Seq 发号维) |

**⑥ ~~收口时工作树有 8 个已改 + 15 个未跟踪文件未提交~~ → ✅ 收口轮末已全部提交**

收口轮发现时工作树 = 23 条(8 改 + 15 未跟踪),含 S20-4 / S20-7 的生产码、全部证据件、
以及 §九 / retro / QA 签核三件本身。**用户当场裁定「提交收口」**,拆三笔提交:

| # | commit | 内容 |
|---|---|---|
| 1 | `0a64faf` | S20-4 本体(契约 2 件 + 谓词 2 件 + `PrescribeFlow` AR2 + 29 条夹具 + 8 条变异 spec + 两件证据) |
| 2 | `34c75e9` | S20-7 治理盘点(债册四十四订 + `EventBudget` W8 + `TD-medcons-16` 夹具删除) |
| 3 | `d69d4b0` | 收口四件套(§九 + retro + QA 签核 + CAPACITY 第 20 行)+ `sprint-status.yaml` |

复算:`git log --oneline 51193cf..HEAD` ⇒ 实跑 **3 笔**;`git status --porcelain | wc -l` ⇒ **0**。
⚠️ 提交后「被验的树」= `d69d4b0`,可 `git show` 完整复现 —— 此前的 C-6 条件**就此关闭**。

> 🔁 **2026-10-27 哈希订正(原文不删)**:上表三个哈希在首次提交时为 `779ff18` / `7cf8521` / `a0197f1`,
> 三笔的 message **均缺 `Co-Authored-By` 尾行**。按 `coding-standards.md` §commit message 里的定性错了怎么办,
> 用 `commit-tree` 重建(**tree 原样复用,逐位相同** —— 见下方核对),`update-ref` 换指向。
> 重建后旧哈希从历史消失,故本表就地改写为新值,并在此留痕。
>
> | 旧 | 新 | tree(两者必须相同) |
> |---|---|---|
> | `779ff18` | `0a64faf` | `5fc891c699bfeecff835d4f6da4f5f4c338b6ea1` ✅ |
> | `7cf8521` | `34c75e9` | `707befd7536da4febda8819d51d3ad0ebbe6fd5a` ✅ |
> | `a0197f1` | `d69d4b0` | `b1efa57ee8c3a5a03f1e7ab9adf9ddfc7627db4b` ✅ |
>
> 判据 = **改写前后 tree 哈希逐位相同** ⇒ 只动了 message,零文件内容变化。
> 改写前已核:未推送(`git branch -r --contains` 为空)· 工作树干净 · 全仓 grep 旧哈希仅命中本文件与 QA 签核件。

### 9.4 与前序 sprint 的对比

| 维度 | sprint-19 | sprint-20 |
|---|---|---|
| 计划内完成率 | 5/5 = **100%** | 7/7 = **100%** |
| 追加件完成 | 轮面追加(9 类,以轮计) | **8/8 完成** 🔁(收口轮记 **5/8**,4 件结转;同日链尾补齐后转 8/8) |
| 未闭 P0 面 | 76 → **69**(−7) | 69 → **63**(−6) |
| 新立 P0 面债 | +5 | +4 |
| 收口滞后 | 三连滞后(行动项 3 立项) | **零滞后**(四件套同日) |
| QA 签核 | APPROVED WITH CONDITIONS(6 条) | APPROVED WITH CONDITIONS(6 条) |

> **追加率口径首次可量化** —— sprint-19 的「追加」以轮计、件数计法失真(见 CAPACITY-MODEL 19 行注);
> sprint-20 的追加批是**具名 story**(S20-8…S20-16),故 `5/8 = 62.5%` 是第一次可比的追加完成率。
> 代价:**追加量是预留额度(1 件)的 8 倍** —— 这是 retro-19 行动项 2「将掀几债」预估失效的直接证据。
>
> 🔁 **2026-10-27 订正**:链尾四件同日补齐 ⇒ **完成率 `8/8 = 100%`**。
> ⚠️ **但上面那句「代价」不变,而且更重** —— 追加量是预留的 8 倍,这件事**不因为都做完了而变轻**:
> 计划被追加打穿是**过程缺陷**,结局圆满不撤销它。容量模型的口径修复仍是 sprint-21 的必办项。

---

## §十 收口后追加批(2026-10-28 · 用户裁定「直接在 sprint-20 排一张装配轮的卡」)

> ⚠️ **本批在 sprint-20 收口(`2026-10-27`)之后**。`sprint-status.yaml` 由 **17 件 → 18 件**,
> 其余 17 件的 `status:` / `completed:` 一字未动。
>
> ⚠️ **2026-10-28 追加注(上文原文不删)**:上文「17 → 18」是 **S20-17 落盘时**的口径。
> 本批其后又落 **S20-18**(World 生命周期轮)与 **S20-19**(42 建造发起方接线)⇒ 本节实为
> **三张卡**,`sprint-status.yaml` 现为 **20 件**(17 原有 + 3 追加);其余 17 件至今仍一字未动。
> 复算:`python3 -c "import yaml;d=yaml.safe_load(open('production/sprint-status.yaml'));print(len(d['stories']))"` ⇒ `20`。

### S20-17 — World 场景装配轮(`World` / `StructureWriter` 生产构造点 + 装配根接线)

| 项 | 内容 |
|---|---|
| **Owner** | unity-specialist |
| **复杂度** | **M** |
| **前置(全部 ✅)** | ADR-023 三场景制(`Boot.unity` / `World.unity` 已建,2026-10-09~11)· `CompositionRoot.Assemble` 的 `structures` 参数位(`CompositionRoot.cs:123`)· `BuildModulesCatalog` 生产实现(S17-6)· `DropEventWriter` 三段写者(S20-14) |

#### 病因:两处引用、零排期载体(承 `SP-10` 同型失效模式)

- `unity/Assets/Gameplay.Boot/CompositionRoot.cs:787-796` 逐字:「`structures` 从哪来:23 的
  `StructureInstanceRegistry` 今日在生产树**零构造**(`new World(` 同样零构造……」;
  注释指名「**`BootRoot` 的 World 场景装配轮**」。
- `production/qa/evidence/impl-s20-2a-building01-2026-10-25.md:176` 的 **N5** 条:
  「生产装配根 `CompositionRoot` 的 `new World(` —— 另属一层,本件未做」,处置写「后续装配轮」。
- `TD-building-01` 已于 2026-10-25 `closed`,**N5 未转成新债** ⇒ 该缺口今日**零载体**。
- `production/epics/` 无对应 story;`sprint-20` 原任务表无对应卡。

#### 边界:最小合成世界(**用户 2026-10-28 裁定**)

`World(WorldGeometry, BuildSlotCatalog)` 的两个实参**都用最小合成值**:

- `WorldGeometry(TerrainCell[16,1,16], new int[256], new int[256])` —— 全 walkable、POI / 资源格全 `NONE`
- `new BuildSlotCatalog()` —— **空槽位目录**(`RegisterSlot` 零调用)

**理由(实测)**:`assets/data/` **零份** `world_*` JSON;`unity/Assets/DataCooked/` 的 **25 份**
cooked 产物里**零份**世界数据;世界数据的唯一作者 = ADR-022 关卡工具,已裁定「不立 epic、不排期」
(`p0-remaining-roadmap-2026-10-20.md:1088`)。**装配不依赖这些数据** —— `World` 的两个实参都能合成。

#### AC(逐条可判伪 · 禁借绿)

| # | 判据 | 可复算命令 / 落点 |
|---|---|---|
| **AC-1** | 生产树 `new World(` 由 **0 → 1**,落点 = `Gameplay.Boot/BootRoot.cs` | `grep -rn "new World(" unity/Assets --include=*.cs \| grep -v "/Tests/"` ⇒ 恰 1 命中 |
| **AC-2** | 生产树 `new StructureWriter(` 由 **0 → 1**,落点同上 | 同上换符号 ⇒ 恰 1 命中 |
| **AC-3** | 经**生产装配路径**后 `CompositionRootServices.ClinicRoomMatching != null` | 今日 `structures == null` ⇒ 恒 `null`(`CompositionRoot.cs:806`);夹具断言非 null |
| **AC-4** | `World` 端到端**经装配根产出的服务**(非测试自建):`Place → IsEffectivelyWalkable == false`;`Remove → true` | 同一夹具内两步;判据同 S20-2a,差异只在**入口** |
| **AC-5** | `StructureWriter` 构造 fail-closed:`DropEventWriter` 未注入 ⇒ 构造期抛 | `Assert.Throws<ArgumentNullException>` + 「抛前下游零调用」副作用断言 |

**证伪轮(S12-4 · 一属性一变异 · SHA-256 逐字节还原)**:AC-1…AC-5 各一条。
⚠️ **AC-1 / AC-2 的变异形态 = 删除构造行**(不是改参数)—— 这两条的判据就是「该行存在」,
改参数不触及判据。

#### 挂标不记绿(如实,禁勾)

1. **世界烘焙数据** —— `world_geometry` / `world_buildslots` 仍零产出;`BuildSlotCatalog` 是空目录
   ⇒ `AC-23-08` 第三半边(占用格 ⊆ `BuildSlotRegion`)**仍无载体**,本卡**不解除**。
2. **`StructureWriter.Place` / `Remove` 的生产发起方** —— 归 42 的建造交互(`O-23-6`),**不在面内**。
   ⇒ **本卡交付 = 「写者已装配、可被调用」,不是「玩家能拆房」**。此句须与 AC-2 同读。
3. **`ClinicCellSource` 接真源** —— 装配后**具备条件**(registry 不再为 null),但接线归 24 侧,不在面内。
4. **`World` 的生命周期归属** —— 挂在 `BootRoot` 还是 `World` 场景的 additive 根,须在卡内**明确裁定并留痕**;
   未定 ⇒ **AC-3 不得标绿**(ADR-023 §① 三场景制的直接后果,不预设答案)。
5. **`PlaceableChecker` 是否接进装配** —— 若接,「空槽位目录 ⇒ 建造永不合法」即从潜在缺口转真;
   **接则不接,卡内裁定并留痕**,不得沉默二选一。

#### 实施裁定(2026-10-28 实施轮 · 卡内留痕,承上列第 4 / 5 项的强制要求)

**裁定 A —— `World` 的生命周期归 `BootRoot`。**
实例在 `BootRoot.Start()` 的启动序里由 `MinimalWorldFactory.Create()` 造出,
交给 `CompositionRoot.Assemble(world:)`,再经 `CompositionRootServices.World` 长期持有。
**不**挂 `World.unity` 的 additive 根。理由三条:
① ADR-023 §② 要求 `World.unity` **零 gameplay GameObject** —— 世界数据是烘焙产物,
场景里摆一个 `World` 持有者即第二真源;
② 拆序六步(ADR-023 ⑤)要 `ReleaseInstance` 全部 Addressables 产物并断言登记簿空 ——
世界若挂在场景根上,卸载顺序会与「服务袋仍持引用」冲突;
③ `BootRoot` 是启动序唯一承载(类注:只驱动、不持 sim 真值),世界是**装配产物**而非 sim 状态,
与 `SaveService` / `LoadSequencer` 同格。
⚠️ 后果如实登记:**世界不随场景卸载而销毁**(`BootRoot` 常驻)⇒ 换关 / 读档后
「旧世界还在」这件事**本卡不处理**,归 ADR-023 拆序轮的实现期义务。

**裁定 B —— `PlaceableChecker` 本轮**不接**进装配。**
`StructureWriter.Place` 今日**不调用**它(它只算足迹 + 写占用);把它接进装配需要
「建造合法性」的判定面(槽位区域 / 地形 / 模组规则),那是 23 的 GDD 规则面,
且空槽位目录会让每一次放置都被判非法 —— 一旦接上,`AC-4` 的 `Place` 将**永远失败**。
⇒ 本轮**只装写者、不装合法性门**;`AC-23-08` 第三半边仍无载体(挂标 1 不解除)。
此处明确:**这是「不接」,不是「忘接」**。

**偏差 C —— AC-1 的落点比卡原文深一层(就地加注,原文不删)。**
卡 AC-1 写「落点 = `Gameplay.Boot/BootRoot.cs`」;实施落点为
**`Gameplay.Boot/MinimalWorldFactory.cs:184`**,由 `BootRoot.cs:219` 的
`MinimalWorldFactory.Create()` 调用。
理由:`WorldLattice.cs:295-298` 明令 `WorldGeometry` 不得有「按尺寸造空白世界」的入口 ⇒
「合成」这件事必须**显式具名**且**带响亮日志**,一个 300 行的启动序里塞不下这段论证。
判据不变:AC-1 的**可复算命令**(生产树 `new World(` 恰 1 命中)**照样成立**;
另加一条接线判据 `test_ac201b_bootRootActuallyCallsTheWorldFactory` 钉住「它不是死工厂」。
⚠️ 若认为必须逐字落 `BootRoot.cs`,那要放弃工厂的封装 —— 属**可回退的设计取舍**,非判据妥协。

#### 实施结果(2026-10-28 · 收口)

**判定:AC-1…AC-5 全部「已落 + 已证」;证伪轮 5/5 恰如预期(第三轮);全量回归零漂移。**
原件 = `production/qa/evidence/impl-s20-17-world-assembly-2026-10-28.md`。

| # | 落点 | 判据载体 |
|---|---|---|
| AC-1 | `Gameplay.Boot/MinimalWorldFactory.cs:184` | `test_ac201_*`;反证 M1 |
| AC-1b | `Gameplay.Boot/BootRoot.cs:219` | `test_ac201b_*`(钉住「不是死工厂」) |
| AC-2 | `Gameplay.Boot/CompositionRoot.cs:941` | `test_ac202_*`;反证 M2 |
| AC-3 | `CompositionRoot.cs:882`(注册表取自 `world.Structures`)/ `:920`(服务构造) | `test_ac203_*`;反证 M3 |
| AC-4 | 写者 `occupancy` = 同一 `World` | `test_ac204_*`;反证 M4 |
| AC-5 | `Sim/World/StructureKinds.cs:281`(`_dropWriter` 断言;ctor 起于 `:270`) | `test_ac205_*`;反证 M5 |

> 🔁 **2026-10-28 就地订正(承 `review-s20-17-2026-10-28.md` §五 S-1)** —— 上表**原写的四条坐标全漂**:
> AC-1 `:109`→**`:184`** · AC-1b `:213`→**`:219`** · AC-2 `:858`→**`:941`** ·
> AC-3 `:819`→**`:882`/`:920`** · AC-5 `:214`→**`:281`**。
> **成因**:S20-19 / S20-20 在 `CompositionRoot` / `MinimalWorldFactory` 里插行,坐标未随动。
> ⚠️ **AC-5 不是「行号偏移」而是「指错断言」**:原 `:214` 指向 `structureId <= 0` 那条守卫,
> 与 AC-5 要的 `DropEventWriter` fail-closed **无关** —— 按旧坐标去读会读到无关代码。
> **判据本身未失效**(下表 `grep` 命令实跑仍恰 1 命中),错的只是坐标。
> 同一订正已同步到 `impl-s20-17-world-assembly-2026-10-28.md:32-37`。

**回归(三元组:截至 `2026-10-28 04:26 CST` · 批次 `S20-17 收口 全量`(最终树,注释清理后重跑)· commit `a42d2df` + 本批未提交)**:
| 套件 | 结果 | 基线(批 4) | 差 |
|---|---|---|---|
| EditMode | `5575 / 5540 / 0 / 35` | `5567 / 5532 / 0 / 35` | **+8 / +8 / 0 / 0**(用例级:仅在 new 8 条全为 `world_assembly_wiring_test`,仅在 old **空集**) |
| PlayMode | `238 / 234 / 0 / 2 inconclusive / 2 skipped`(退出码 `2`) | `238 / 234 / 0 / 2 / 2` | **0**(用例级逐条比对,零差异) |

⚠️ **证伪轮前两轮都失败过,两轮都不是「预期写对了」**:
① 第 1 轮 M2 **逃逸** —— 变异是「停用 `if (world != null)` 守卫」,而 AC-2 判的是**源码文本字面量恰 1 处**;
行为变异改不动文本 ⇒ 零判别力却照绿。② 第 2 轮 M2 转红但**多红一条 ac203** ——
根因是**一个夹具担了两个属性**(ac203 同时断言匹配服务与写者)。
两次处置都不改预期迁就实测:第 1 轮改变异形态(为此把写者构造合成**单行**,源码处已注明
「可读性换判据可证伪」),第 2 轮把夹具拆窄。完整实况留在 spec 的 `_history_…firstRun` / `secondRun`。
**元教训**:AC 的判据若是「源码文本某形态恰 N 处」,必须配**文本级**变异;配行为变异会得到
「绿而零反证」,且看起来完全正常。


---

### S20-18 — World 生命周期轮(`World` 重置 + 世界流重建 + `Modify` 足迹修复)

| 项 | 内容 |
|---|---|
| **Owner** | unity-specialist |
| **复杂度** | **M** |
| **前置(全部 ✅)** | S20-17(`World` / `StructureWriter` 生产构造点已落,`2026-10-28`)· `PoiStateMachine.RebuildFromDecoded`(`Sim/World/PoiStateMachine.cs:161`,同型先例)· `InventoryLoadService`(`Gameplay.Boot`,`_afterRestore` 钩子的既有消费者)· `IPayloadDecoder`(ADR-031,S12-12 实现轮) |

#### 病因:S20-17 显式留下的后果,且比「没销毁」更糟一层

S20-17 的卡内「挂标不记绿」第 4 条逐字:「**`World` 的生命周期后果** —— 世界挂在 `BootRoot`
(常驻)之上 ⇒ 换关 / 读档后「旧世界还在」这件事**本卡不处理**,归 ADR-023 拆序轮的实现期义务。」
本卡即该义务的兑现。勘察后,**缺口是三层,不是一层**(逐条实测):

1. **没有重置入口。** `StructureInstanceRegistry.Clear()`(`Sim/World/StructureKinds.cs:157-160`)
   只清 `_instances` —— **不清 `World._slotOccupied`、不复位 `_nextStructureId`**;
   `World` 本身零 `Reset`。⇒ 旧世界**既没销毁,也没法销毁**。
2. **没有重放器。** `StructurePlaced` / `Removed` / `Modified` 的读方只有表现层只读查询
   (`Gameplay.Presentation/PatientAI/ClinicKnowledge.cs`、`ClinicEnvironmentMemo.cs` 的失效键),
   `StructureInstanceRegistry` **从不被世界流重建**。⇒ 就算清零,读档后世界会**变空**
   —— 那比残留更糟(房子的记忆全没了)。
3. **写侧本身漏一处。** `StructureWriter.Modify`(`:360-391`)只算位掩码 + `_registry.Update`
   + 发事件,**既不 `FreeCells` 旧足迹、也不 `OccupyCells` 新足迹**,且 `Update` 收的仍是
   **旧足迹**(`:101-112` 传 `inst.OccupiedCells`)。对照 `Place`(`:264` 占用)与
   `Remove`(`:313-315` 释放),只有 `Modify` 漏。⇒ **旋转非方形模块后,旧格仍不可走、新格仍可走**
   (违 F-23-1)。这条是重放正确性的硬前置:流是唯一真源,**重放还原真足迹、现场不还原 ⇒ 两者分叉**。

#### 边界

- **只做 sim 侧 `World` 对象**(`DaYiJingCheng.Sim.World.World`),**不碰 ADR-023 §⑤ 的场景 / Addressables 拆序**。
  `unity/Assets/Scenes/World.unity` 存在(仅 `Ground` + `Directional Light`),但**不在 BuildSettings、零加载点** ⇒ 该线无触发面。
- **世界几何不动**:`WorldGeometry` / `BuildSlotCatalog` 来自烘焙静态层(ADR-015),读档不改变 ⇒ `Reset()` 只清派生态两样。
- 重建形态照 `PoiStateMachine.RebuildFromDecoded` 的既有纪律:**`Sim` 侧不自行解码**(够不着 `Sim.Codec`,ADR-029 / ADR-031 甲案),由 Boot 层解码后喂入。

#### AC(逐条可判伪 · 禁借绿)

| # | 判据 | 载体 |
|---|---|---|
| **AC-1** | `World.Reset()` 后 `OccupiedCount == 0` 且 `Structures.Count == 0` | `structure_restore_test` |
| **AC-2** | 注册表 `Reset()` **复位发号** ⇒ 其后第一次 `Register` 返回 `1` | 同上 |
| **AC-3** | `Placed` 序列重放后:实例明细 / 占用表 / `IsEffectivelyWalkable` 与放置时**逐格一致**(含多格足迹) | 同上 |
| **AC-4** | `Placed → Removed` 重放 ⇒ 净空(占用归零) | 同上 |
| **AC-5** | `Placed → Modified(旋转)` 重放 ⇒ 旧足迹释放、新足迹占用,且与 `ComputeOccupiedCells(新朝向)` **逐格相等** | 同上 |
| **AC-6** | 重放**按事件原号保号**且高水位 = `max(id)+1` ⇒ 重放后 `Place` 新结构不撞号 | 同上 |
| **AC-7** | fail-closed:**目录未注入 / `moduleId` 不在目录 / `Removed`·`Modified` 指向不存在 id** ⇒ 各抛,且**抛后世界仍为重置后的空态**(原子性) | 同上 |
| **AC-8** | 装配接线:给 `world` + 存档目录 ⇒ `CompositionRootServices.WorldLoad != null`;**不给 `world` ⇒ `null`**(既有 100+ 调用方零行为变化) | `world_load_service_test` |
| **AC-9** | 端到端:含 `StructurePlaced` 的流 ⇒ `Rebuild()` 后世界恢复到流所述形态;**空流 ⇒ 世界被清空**(不是残留) | 同上 |
| **AC-10** | `StructureWriter.Modify` 旋转后 ⇒ 旧足迹**可走**、新足迹**不可走**、注册表记录**新足迹** | `structure_restore_test` |

**证伪轮(S12-4 · 一属性一变异 · SHA-256 逐字节还原)**:**18 条**变异,逐条对上一个属性
(AC-1 / AC-2 / AC-3 / AC-4 / AC-5 / AC-6 / AC-7 ×3 / **AC-8** ×3 / **AC-9** ×2 / AC-10 ×2,
另 6 条参数守卫)。spec 落 `tools/falsify/specs/s20-18-world-lifecycle-restore.json`。

> ⚠️ **本卡原写「9 条(AC-8+9 合一条)」,实现轮改为 10 条** —— 理由:AC-8(装配接线:
> `WorldLoad` 非 null)与 AC-9(端到端重建:世界恢复到流所述形态)是**两个正交属性**,
> 而且能各自被**一条不同文件的单行守卫**击破(M8 在 `CompositionRoot.cs:889`、
> M9 在 `WorldLoadService.cs:114`)⇒ 一条变异**证不了两个**(承「一属性一条变异」)。
> 合并形态的实际后果是:不管选哪一行,总有一个 AC 的判据面**零反证**。
> **原文不删,就地改数并留此注。**
>
> ⚠️ **第二次订正:10 条 → 17 条**(实现轮收口前) —— 10 条只覆盖 **AC 主干断言**,
> 漏掉 S12-4 命中形态里的**两类**:④ 一次性 / 幂等类 与 ⑤ 阈值边界(参数守卫)类。
> 逐项补:
> - **M11**(`StructureRestore.cs:159` 重建前 `Reset()`)—— 首版 10 条里 `test_ac9f` **零反证**
>   (它的首版形态本身就是假绿,见证据原件 §二),补 M11 才把它钉住;
> - **G1–G6**(六个参数守卫:events / decoder / world / moduleCatalog / ops / catalog-in-Modify)——
>   六个 `ArgumentNullException` 守卫此前**一条反证都没有**,而「入口即拒」类判据
>   **不反证就等于没测**(只断异常类型会被下层同类型异常冒充)。
> **两条订正的方向相反**:第一次是「一个 AC 拆成两条变异」,第二次是「同一批 AC 补上此前零覆盖的
> 判据形态」。**原文不删,就地改数并留此注。**
>
> ⚠️ **第三次订正:17 条 → 18 条**(主轮**跑完之后**补 **M12**) —— 理由 = **分支覆盖**,不是新属性:
> AC-10 是**合取判据**(旧格可走 **∧** 新格不可走),而主轮只有 M10 打「释放旧足迹」半边,
> `StructureKinds.cs:478` `OccupyCells(newCells)` 那半边**零反证**。这与
> `review-workflow` Phase 8b 新增 `Branch(分支覆盖)` 列是同一件事在证伪轮上的落实
> (sprint-18 S18-2 的产物侧分支零反证就是这样漏掉的)。M12 单跑 = exact。

#### 挂标不记绿(如实,禁勾)

1. **重放器的生产实效 = 零。** `StructureWriter.Place` 的生产调用点 `grep` 实测 **0**
   (`grep -rn "\.Place(" unity/Assets --include=*.cs | grep -v "/Tests/"` 零命中)⇒
   生产流**永不含** `StructurePlaced` ⇒ 重放器在生产上**恒空跑**,只有夹具走到。
   与 `RequestLoad` 本身「通电待信」同格。**不得**把「重放器已接线」读成「读档已可用」。
2. **ADR-023 §⑤ 场景 / Addressables 拆序不在面内** —— 那要等 `World.unity` 真被 additive 加载。
3. **`RequestLoad` 仍零调用方**(UI 存档位接线轮才激活)。
4. **`BuildSlotCatalog` 仍空** ⇒ 占用格仍无法与槽位区域对账(`AC-23-08` 第三半边仍无载体)。
5. **`Modify` 修复只覆盖「足迹随朝向变化」** —— 朝向合法性仍由上游 `ModifiableChecker` 守,本批不重复判定。
6. **在场视图 / `PatientAppearedDriver` 内存态 / 视觉侧** 仍不在读档重建面(`LoadSequencer` 类头注的既有清单),
   本批**只加世界注册表一项**,不代它清账。

#### 实施结果(2026-10-28 收口)

**原件**:`production/qa/evidence/impl-s20-18-world-lifecycle-2026-10-28.md`

| 项 | 结果 |
|---|---|
| **AC** | AC-1…AC-10 **全部已落 + 已证**(逐条 Landed / Proven 分列见原件 §二) |
| **新增夹具** | `structure_restore_test`(13 测)· `world_load_service_test`(13 测)= **26 测** |
| **证伪轮** | **18 条变异,零逃逸**(主轮 17:15 exact + 2 超集;两条超集订正预期后单跑复验 exact;另补 M12 覆盖 AC-10 的另一分支)。还原 = SHA-256 逐字节通过 · 受保护路径指纹未变 · `FALSIFY-` 零残留 |
| **回归 EditMode** | `5601 / 5566 / 0 / 35` @ `2026-10-27T22:59Z` · 最终树批 · `c784d88`;基线 `5575 / 5540 / 0 / 35`(引用自 S20-17 收口记录)⇒ **+26 / +26,零失败** |
| **回归 PlayMode** | `238 / 234 / 0 / 2 / 2`(退出码 2,基线同)@ `2026-10-27T23:02Z` · `c784d88` ⇒ **逐项相同,零回归** |

**实现轮的三次自纠(均留痕,不是「一次写对」)**:

1. **`test_ac9f` 首版是假绿** —— 原版断言「连跑两次结果相同」,对**任何**确定性函数恒成立 ⇒ 零判别力。
   改写为「重建 → 弄脏世界 → 再重建 → 脏结构必须消失」,由 M11 反证。
2. **卡内证伪轮条数第二次订正(10 → 17,后补 M12 至 18)** —— 原因见上方两处 ⚠️;
   M12 是**主轮之后**按 `Branch(分支覆盖)` 纪律补的(AC-10 是合取判据,首次只覆盖了一半)。
3. **订正了一处既有夹具的非法输入** —— `clinic_purity_replay_test.cs` 用 `newOrientation: 1`
   (合法值只有 `0/90/180/270`,`PlaceableChecker.cs:127` 对其余抛)。以前 `Modify` 不重算足迹
   所以非法值被静默存下;本批修好占用同步后它会当场抛。**这是夹具用了非法输入,不是生产回归**。

**未及核查(NOT-RUN,禁借绿)**:`afterRestore` 钩子内 `worldLoad?.Rebuild()` 的**调用本身**
无夹具钉住(`CompositionRoot.cs:707-711`)—— 给了 `file:line` 但给不出能证明它真被调用的命令
⇒ 按 NOT-RUN 登记,不计入任何判据面。另:真存档往返、`World.unity` 真加载后的行为均未验(挂标 1/2/3)。

> 🔁 **2026-10-28 评审修复轮(收口后,见 §十三)**:补做独立评审时查出 **2 条缺陷**并已修:
> ① **B-1** `Placed` 重放**同号覆盖**时 `Restore` 不碰占用表 ⇒ 被覆盖实例的旧足迹
> **永久阻塞**(`StructureRestore.cs:183-215`,与 `Removed`/`Modified` 两支对齐);
> ② **B-1b** `StructureWriter.PeekNextId` 与注册表**双计数器**,基线先于写者 / `Reset` 复位
> 两方向都静默脱钩(`StructureKinds.cs:287-299`),已删镜像字段改委托。
> **证伪 M1 / M3 各恰 1 条红;还原 SHA-256 逐字节通过。**
> ⇒ 上文「18 条变异」是**实现轮**的数;**修复轮另有 2 条**,合计 **20 条**。
> 原件:`production/qa/evidence/review-s20-18-2026-10-28.md`。
> ⚠️ **仍未结**:schema 迁移期基线 id 段归属(谁该赢)需 ADR 级裁定,本修复只保证足迹一致。

---

### S20-19 — 42 建造发起方接线(兑现 `O-23-6`)

| 项 | 内容 |
|---|---|
| **Type** | **`Integration`**(主) · `Logic`(次) —— 见下方「类型与 TR 归属」 |
| **Owner** | unity-specialist(规格段 + `ux-designer` 复审) |
| **复杂度** | **M**(规格段 S + 接线段 M) |
| **前置(全部 ✅)** | S20-17 / S20-18(`World` 与 `StructureWriter` 已装配、世界可由流重建)· `InventoryTransaction`(S20-14,先全验后落流)· `ModuleDefinition.Cost` / `BuildItemKey`(2026-10-25/26 两轮补)· `DemolishChecker` P0 返还率 1.0(`:84/:125`) |

#### 用户裁定(2026-10-28,三项,与 S20-18 同批)

| # | 裁定 | 取项 |
|---|---|---|
| ① | **范围** | **规格 + 逻辑接线** —— 幽灵预览只落接口与量化结果,**不做可见渲染** |
| ② | **模态归属** | 建造 = **世界内操作态,不占 `ModalId`**(闭集维持 7 员) |
| ③ | **材料成本** | **本轮接扣减**(按 `inventory-and-items.md` 规则六的原子单位) |

#### 类型与 TR 归属(2026-10-28 收口轮补登)

> 为什么补:`/story-done` 的 Test Evidence 门依赖 `Type:` 字段,Phase 2 的 requirement 核对
> 依赖 `TR-ID`。本卡原两者皆空 ⇒ 两项 ADVISORY。**下表全部引用 `docs/architecture/tr-registry.yaml`
> 的既有条目,零新增 TR** —— 建造发起方接线是既有需求的**实现**,不是新需求。

**`Type: Integration`** 的依据:本卡的 AC 跨 **42(呈现 / 编排)· 23(建造核心判定)· 21a(库存投影)**
三个系统,且 B-9 直接判「装配是否成立」—— 按 `qa-plan` skill 的混合故事判据(取实现风险最高者),
Integration 为主;其判据载体本身是纯逻辑(选料 / 判定),故次类型记 `Logic`。

**`Type: Integration` 的证据载体(声明了 Integration 就抬高证据门,须写明载体)**:
`unity/Assets/Tests/EditMode/ModularBuilding/build_controller_test.cs` —— 真 `World`(真 `BuildSlotCatalog`
+ 真占用表)· 真 `StructureWriter` · 真 `PlaceableChecker` / `DemolishChecker`,按
`coding-standards.md` §Test Evidence 的「**同树 `EditMode/[System]/` 真装配件**」条款计入 Integration 载体,
不额外要求 PlayMode 件。
⚠️ **如实登记一处替身**:`IPresenceQuery` 走测试替身(`AlwaysEmptyPresence` / `PresenceOnCell`),
因为该路径**生产实现尚未存在** ⇒ 「真实占位病人 ⇒ 拒绝放置」这条依赖**未在真装配下验过**。
这不掩盖 B-2/B-6 的判据(它们判的是「判定器拒绝 ⇒ 写者零调用」,与 PRESENCE 的真假无关),
但若将来接上真 presence,须补一条真装配回归。

| AC | TR-ID | 该 TR 的现行 requirement(节录) |
|---|---|---|
| **A-1** | `TR-building-004` · `TR-skeuoui-007` · `TR-skeuoui-009` | 整数格判定面(A-1 的「量化器单一实现点」半边)· 42 只渲染永不持有游戏状态(幽灵预览)· 模态闭集契约(A-1 的「建造不是模态」论证半边) |
| **B-1** | `TR-building-004` | 全部放置 / 可居判定走整数格,禁 Physics / NavMesh 进入判定面 |
| **B-2** | `TR-building-004` | 同上(拒绝路径的判定同属整数格面) |
| **B-3** | `TR-building-001` · `TR-building-002` | 三个 `Structure*` Kind 落世界流 · 建造图层 Overlay = BakedInitial ⊕ 事件流派生 |
| **B-4** | `TR-inventory-001` | 库存 = 世界流的纯投影(`InventoryOf = fold`);7a 快照是优化非真相 |
| **B-5** | `TR-inventory-001` | 同上 —— 选料是同一投影的纯函数,不引入第二真源 |
| **B-6** | `TR-building-001`(`TR-building-009` 为**延伸**) | 三个 `Structure*` Kind 落世界流 · 〔延伸〕Refund 截断例外(守恒律回写义务,与拆除返还率的**方向**相关但**不覆盖**该 AC) |
| **B-7** | `TR-skeuoui-009` · `TR-skeuoui-007` | `IModalState` 契约(闭集 7 员)· 42 不持游戏状态 |
| **B-8** | `TR-input-002` | 三套绑重(K&M + Gamepad + OpenXR)同表共存 |
| **B-9** | `TR-building-007` | 23 程序集零呈现引用;42 不是建造状态写者 |

⚠️ **三处如实标注**:
① `TR-building-004` 现行 `status: partial`(其 note 写明「『禁 Physics』由禁令推定而非逐字段条款,
执法断言归实现轮)」—— B-1 的源码面门(`test_acB1_buildPath_doesNotReQuantizeCells`)是该
「实现轮」的一部分,**不据此把该 TR 转 `covered`**(转结需另做全量核对)。
② **未映射**:「建造扣料 = `StructurePlaced` 的 `cost(m)` 反推」这条没有专属 TR
(最接近的是 `TR-inventory-001`,但它讲的是投影与真源,不讲扣料时机)⇒ 该判据的真源是
`design/gdd/inventory-and-items.md` 规则六 + `modular-building.md:124`,**不在 registry 面内**,
如实登记为 registry 缺口,不在本卡内自行造 TR。
③ **B-6 的 `TR-building-009` 标了「延伸」** —— 该 TR 的 requirement 是「Refund 截断例外未回写
ADR-006 守恒律」(`status: partial`),讲的是**守恒律回写义务**;B-6 判的是**拆除返还比例 P0 = 1.0**
与「拒 ⇒ 零副作用」,两者**方向相关但不互相覆盖**。标「延伸」= 不主张 B-6 使该 TR 转绿。

**一条 TR 可映射多条 AC,同一 AC 也可映射多条 TR**(本表两个方向都有)—— 这是**覆盖关系**,不是
一对一编号。转 `covered` 的判定一律走 registry 自身的 `status` 字段,**不由本表推定**。

#### 病因:五处实测缺口(`O-23-6` 登记已两个多月,一直是「待 42 下一轮」)

1. **写侧发起方 = 0。** `grep -rn "\.Place(" unity/Assets --include=*.cs | grep -v "/Tests/"` → **0 行**。
   `StructureWriter` 自 S20-17 起已装配(`CompositionRoot.cs:879`),但**没有任何生产代码调用它**
   ⇒ 生产流**永不含** `StructurePlaced` ⇒ S20-18 的重放器在生产上恒空跑。
2. **判定器零生产构造点。** `PlaceableChecker` / `DemolishChecker` 在 `Sim/World/` 之外
   **零构造点**(实测:仅注释提及)⇒ 放置合法性在生产上**无处可判**。
3. **零建造输入动作。** `InputSystem_Actions.inputactions` 只有 `Player`(Move/Look/Sprint/Jump/
   Interact/Emergency/Attack/OpenInventory)与 `UI` 两图 —— **零建造动作**。
4. **42 的 GDD 零建造交互章节。** `skeuomorphic-ui.md` 全文只有 `:1267` 一句「非纸面进度
   (19 / 23 / 18)归世界器物状态,UI 不管」;`O-23-6` 的两缺口(手柄幽灵循环选择 / float→格
   量化器)在 42 侧**从未成文**。
5. **选料器全仓零实现。** 规则六 BL-23 定「玩家未点名 ⇒ id 升序兜底序」,但
   `grep` 实测**零个**消费计划器(`InventoryTransaction.ValidateConsumption` 只验**已给定**的
   `ConsumeLine[]`,不负责**选出**它们)。

**两处已就绪,不必新写**(勘察结论,写进卡以免实施期重复劳动):

- **量化器已有唯一实现**:`CellTransitionDetector.CellFromPosition`(`Mathf.FloorToInt` 三轴),
  玩家移动路径在用(`PlayerController.cs:156`),`IPlayerMotor.cs:58` 逐字「实现必须走 `CellFromPosition`」
  ⇒ 本卡只要求**同源**,不新建第二份。
- **返还比已定**:`DemolishChecker.cs:84/:125` 逐字「P0 = 1.0 = 65536」,不新增旋钮。

#### 边界

- **只做 42 侧的「发起」**(输入 → 格 → 判定 → 调用写者),**不碰 23 的判定语义**
  (`PlaceableChecker` / `DemolishChecker` 一行不改,只接线)。
- **不碰 21a / 19 的库存语义**:扣减走既有 `InventoryTransaction` 模板;选料器只**排序选取**,
  不新增消耗规则。
- **不做幽灵预览的可见部分**(用户裁定 ①):不新增材质、不新增 world-space 标记、不改 URP。
- **不新增 `ModalId`**(用户裁定 ②):`ModalId` 枚举零改动。
- **不碰 ADR-023 §⑤ 场景拆序**,不碰 45 的联机上行(P1b)。

#### AC(逐条可判伪 · 禁借绿)

| # | 判据 | 载体 |
|---|---|---|
| **A-1** | `skeuomorphic-ui.md` 新增「建造交互」节,逐条覆盖 `O-23-6` 的两缺口(手柄幽灵**循环**选择 / float→格量化器声明为 `CellFromPosition` 单一实现点)+ 「建造模式不是模态」的论证;**`modular-building.md:511` 的 `O-23-6` 转 ✅** | 文档走查(**非测试**,不得计入判据面) |
| **B-1** | 建造取格**只经** `CellTransitionDetector.CellFromPosition`;生产树无第二处 float→格 floor 转换 | 门 + 夹具 |
| **B-2** | 非法放置**零副作用**:`PlaceableChecker.Check ≠ 通过` ⇒ `StructureWriter.Place` 调用 **0 次**、世界流 **0 条** | 夹具(副作用判据) |
| **B-3** | 合法放置端到端:实例落库 + `IsEffectivelyWalkable == false` + 世界流 +1 | 夹具 |
| **B-4** | 材料**先验后写**:不足 ⇒ 拒放、`Place` **0 次**、**零事件**;足够 ⇒ 消耗事件与 `StructurePlaced` **同一事务**落流 | 夹具 |
| **B-5** | 选料器:玩家未点名 ⇒ **id 升序兜底**;跨栈 ⇒ N 条相邻 `Seq`;单请求内**不重复点名**同一实例 | 夹具 |
| **B-6** | 拆除:`DemolishChecker.Check` 拒 ⇒ `StructureWriter.Remove` **0 次**;可拆 ⇒ 流 +1、返还按 P0 1.0 | 夹具 |
| **B-7** | 开 / 关建造模式前后 `IModalState.Modal` **恒 `None`** | 夹具 |
| **B-8** | 建造动作存在于 `.inputactions`,且 K&M + Gamepad **双绑重齐**(ADR-011) | 门(读资产)+ 夹具 |
| **B-9** | `PlaceableChecker` / `DemolishChecker` 生产构造点 **0 → 1**,且经服务袋暴露 | grep + 夹具 |

**证伪轮(S12-4 · 一属性一变异)**:B-1…B-9 各一条变异,预计 **9 条**。
B-1 是门 ⇒ 变异形态 = 在建造链里插一处独立 `Mathf.FloorToInt`,要求门转红。
B-2 / B-4 / B-6 的判据是**副作用**(调用次数 / 事件条数)⇒ 变异形态 = 把判定与调用**顺序颠倒**
(先调用后判定),此时「结果状态」可能仍对,但副作用序错 —— 这类变异**只有副作用判据能抓**。

#### 挂标不记绿(预估 7 条,如实,禁勾)

1. **幽灵预览零渲染** —— 用户裁定 ①;42 只出「量化结果 + 合法性」的接口,不画。
2. **建造模式的进入方式** —— 若 4 的交互系统尚未实现,本批只能**键位直绑**(`Build` 动作切换),
   不是 ADR-009 §七 意义上的世界交互意图。**实施前须核 4 的实现状态**并如实登记。
3. **不可放置原因的 UI 呈现** —— 42 只留拒绝原因枚举,不画提示。
4. **客机建造上行**归 45(P1b;`EmergencyAttempt` 走可靠通道的同族问题,ADR-001 §一之三)。
5. **`BuildPartQualityGate` 不参与放置判定** —— P0 建材 `quality` 恒 1(该门自己是**范围债执法体**),本批不重复施加。
6. **拆除返还物的落点格** 由 20 定,本批只**触发**(`DropEventWriter` 已封装)。
7. **建造音效 / cue 未接** 归 44。

#### 待实施期核的事项(不是待裁,是待查)

- 4 的交互系统实现状态(决定挂标 #2 的措辞,不改变 AC)。
- `StructureWriter.Place` 的 `tick` 来源:须经 `ITickProvider.CurrentTick`(ADR-005 唯一时钟),不得读墙钟。
- B-4 的「同一事务」判据须落到 `InventoryTransaction.Commit` 的实际调用点,不能只断言「两条事件都在流里」
  —— 后者对「分两次写」也成立(**假绿风险已预判**)。

---

#### 实施轮就地加注(2026-10-28 · **原文不删**)

> 本节记录实施期实测与卡面原文的**四处不符**。卡面原文一律保留(可追溯);
> 凡与原文冲突处,**本节为准**,判据以本节给出的载体计。

| # | 卡面原文 | 实施期实测 | 处置(判据以右列为准) |
|---|---|---|---|
| ① | **B-4** 后半句:「足够 ⇒ **消耗事件**与 `StructurePlaced` **同一事务**落流」 | **前提错**。`InventoryFold.ApplyBuildCost`(`InventoryFold.cs:929`)已按 `cost(m)` 从 `StructurePlaced` **反推**扣减(2026-10-27 R16 甲案)⇒ 再写消耗事件 = **双重扣减**,玩家建一个花两份料 | 建造的**唯一**流事件 = 一条 `StructurePlaced`。判据改为:**恰一条** `StructurePlaced` + **零** `DropDespawned` + 同一批事件喂 fold 后材料**恰少 `cost(m)`(`build_controller_test.cs::test_acB4_foldDeductsCostFromSameStream`)** |
| ② | **B-5** 「跨栈 ⇒ N 条相邻 `Seq`」 | 同上 —— 无消耗事件,故**无 `Seq` 可数** | 载体改为纯函数产出物 `BuildMaterialPlan.Lines`:「跨栈」= N 条 `ConsumeLine`;「升序」= **数组序**;「不重复点名」= 同一 `InstanceId` 只出现一次(`build_material_planner_test.cs`) |
| ③ | **B-8** 「K&M + Gamepad **双绑重齐**」 | 既有门 `action_asset_identity_test::test_input_asset_structure_p0_actions_each_cover_three_binding_groups` 要求**三组齐**(K&M / Gamepad / **XR**) | 4 个新动作(`Build` / `BuildPlace` / `BuildRotate` / `BuildRemove`)各配**三组**;XR 物理键与 `Attack`(grip)/ `Emergency`(trigger)**重叠** ⇒ 登记为挂标 #9 |
| ④ | **B-7** 「开 / 关建造模式前后 `IModalState.Modal` 恒 `None`」 | **不可执行**:全仓**无** `IModalState` 聚合器(闭集 `ModalId` 只有枚举 + 各屏自述,**无单一读面**) | 判据改为**源码面**:建造链条五文件**剥注释后**零 `IModalState` / `ModalId` 记号(`build_controller_test.cs::test_acB7_buildPath_hasNoModalDependency`)。语义等价 —— 「建造不占模态」的强形式即「建造链条够不着模态集」 |

**「待实施期核的事项」三条的结案**:

- `tick` 来源 ✅ —— `CompositionRoot.cs:925` 传 `() => tickDriver.CurrentTick`,`tickDriver` = `ITickProvider`;**零墙钟读取**。
- B-4 的「同一事务」判据 ✅ —— 随 ① 作废(不存在第二笔事件);改由「fold 从同一条流反推扣减」承载,
  它同时钉住了「写前判定」与「重建判定」**同源**。
- 4 的交互系统实现状态 ✅ —— **未实现** ⇒ 挂标 #2 的措辞按「键位直绑」如实落(见下)。

#### 挂标不记绿(实施轮**补 2 条**,合计 **9** 条,一条未勾)

8. **`BuildSlotCatalog` 生产侧仍空** —— `RegisterSlot` **零生产调用点**,`MinimalWorldFactory.cs:99`
   造的是**空目录** ⇒ 生产 `PlaceableChecker.Check` 恒 `SlotNotFound` ⇒ **玩家今天仍然建不了东西**。
   本批兑现的是「**接线 0 → 1**」,不是「可玩」。解锁归关卡工具(ADR-022)导出 `buildslots` 烘焙产物。
   ⚠️ 夹具用**测试侧注册的槽位**跑通全链 —— 那条路径在生产上**尚不可达**,不得读成「建造已可玩」。
9. **XR 绑重物理键重叠** —— 见上表 ③;P0 无 VR 建造(用户裁定 ① 的平面范围),重绑归 P1a。

**同批另补两条实现期事实(不改 AC,只登记)**:

- **玩家选料界面未落地** —— `BuildInputDriver.SelectedModuleId` 的默认值 = 目录**最小 module id**;
  「玩家选哪个模块 / 已解锁哪些」归 42 / 21a 的选料界面(本批不画)。
- **建造输入不查模态开集** —— 全仓无 `IModalState` 聚合器可查(见 ④)⇒ `Enter` 可能与 UI 的
  `Submit` 双触发。归 42 的模态聚合器落地轮。

#### 收口轮(2026-10-28 · `/story-done`)

**`Status: Complete`** · 证据原件 `production/qa/evidence/impl-s20-19-build-initiator-2026-10-28.md`

| 项 | 结果 |
|---|---|
| AC 计分(分母 10) | **Proven ✅ 10** · Branch 全覆盖(B-6 2/2 · B-9 2/2)**[实施轮为 9 + 部分 1]** |
| Falsifiability Gate | **首判 BLOCKING** ⇒ 补 2 夹具 + 2 变异 ⇒ **PASS** |
| 证伪轮 | 18 变异全 exact,**分三批**:第三轮同轮 17 + M17 单跑 1(实施轮的 M5 单跑仍计入) |
| 回归(三元组) | EditMode `5641/5606/0/0/35`(+40/+40,`5641−5601=40` 恰等)· PlayMode `238/234/0/2/2`(零回归)@ `2026-10-28T01:25Z` · 收口轮 · `3ae5599` + 未提交工作树 |

**收口轮补的两处 —— 都不是「改代码」,是「补证明」**:

1. **B-6 的「拒 ⇒ 零副作用」半边** —— 实施轮判「无独立判别力」,该结论**只对 `StructureNotFound` 分支成立**。
   `DemolishChecker.Check` 另有两条**可达**拒绝分支(`ShellNotRemovable` / `EntityOnCell`),
   结构**真实存在**时判定器一放行写者就真会落流 ⇒ 可证伪。补
   `test_acB6_removeWithEntityOnCell_returnsEntityOnCellWithZeroSideEffects` + **M16**(停用
   `BuildController.cs:159` 的拆除早退)⇒ 转红。
2. **B-9 的反例半边** —— `buildController` 的赋值夹在**两层守卫的合取**内
   (`CompositionRoot.cs:847 structures != null` ∧ `:874 world != null`)。裸 `Assemble()` 让两层
   **同时为假** ⇒ 单独停用任一层都不改变可观察量(合取使然)。补
   `test_acB9_assembleWithStructuresButNoWorld_yieldsNullBuildController`(第一层真、第二层假)
   让 `:874` **单独成为决定项** ⇒ **M17** 转红。
   ⚠️ **M17 第三轮先逃逸后才补夹具**:该逃逸既不是靶点错、也不是夹具写错,而是
   **「靶点对,但靶场里没有靶」** —— 与 §五之二 三种根因同族不同型,已登记在 spec 的
   `_round3_note` 里。

**挂标不记绿 9 条一条未勾**(状态不变)。其中 #3 仍是最重的一条:
`BuildSlotCatalog` 生产侧仍空 ⇒ 生产 `PlaceableChecker.Check` 恒 `SlotNotFound` ⇒
**玩家今天仍建不了东西**。本卡兑现的是「接线 0 → 1」,**不是「可玩」**。

**代码评审门**:`production/review-mode.txt` = `lean` ⇒ LP-CODE-REVIEW 走 `AskUserQuestion` 分支
(本收口轮未跑 `/code-review`,如实登记,**不记作已评审**)。

---

### S20-20 — 最小关卡工具 + 建造槽位骨架全链(兑现 S20-19 挂标 #3)

| 项 | 内容 |
|---|---|
| **Type** | **`Integration`**(主) · `Config/Data`(次) —— 编辑期工具 → 烘焙管线 → 运行期装载 三段 |
| **Owner** | unity-specialist |
| **复杂度** | **M** |
| **前置(全部 ✅)** | ADR-022(关卡工具 = 逻辑层唯一作者)· ADR-014(两阶段烘焙)· ADR-025 §④(asmdef 清单已含 `Editor.Tools.Level`)· `BuildSlotCatalog`(`Sim/World` 既有,零改动) |
| **用户裁定(2026-10-28)** | ① 只做挂标 #3(`BuildSlotCatalog` 生产侧);② **先做最小关卡工具(合规)** —— 不手写种子数据 |

#### 病因(为什么不能只补一份 JSON)

`S20-19` 的挂标 #3 原文:`BuildSlotCatalog` 生产侧空 ⇒ 生产 `PlaceableChecker.Check` 恒
`SlotNotFound` ⇒ 玩家建不了任何东西。

最省事的修法是往 `assets/data/` 手写一份 `world_buildslots.json`。**这条路是架构违规**:
ADR-022 §二 裁决「逻辑层整数数据**只能由关卡工具**编写导出」,手写 = 绕过唯一作者。
⇒ 用户裁定走合规路径:**先实现最小关卡工具**,再由它导出数据。

⚠️ 同时如实登记一处**先前的裁定冲突**:`p0-remaining-roadmap-2026-10-20.md:1088` 记着
「关卡工具不立 epic、不排期」。本卡**不解除**该裁定 —— 它是「最小」工具(单关卡、单子集、
7 条 C 检查里 **5 条明确未实现**),不是 ADR-022 意义上的完整工具。二者关系:

| | ADR-022 完整关卡工具 | 本卡的最小工具 |
|---|---|---|
| 产出面 | `world_{geometry,ecozones,poi,resources,buildslots,nav,terrain}` **七类** | **仅** `world_buildslots` |
| C1–C6 | 六项全实现 | **槽位子集 3 条**(其余 7 条进 `CheckReport.Unimplemented`,是**返回值里的数据**,不是注释) |
| 排期 | 仍不排期 | 本卡一次性 |

#### 交付物(9 件,全部新建/最小改动)

| # | 文件 | 作用 |
|---|---|---|
| 1 | `unity/Assets/Editor.Tools.Level/LevelSource.cs` | 声明式关卡源:`SlotSpec{X,Y,Z,Type,BakedInitial}` + 尺寸;`SlotType` 是 Sim 枚举名的**镜像**(本程序集刻意零引用,漂移由烘焙期「名字不在闭集」硬失败暴露) <br>⚠️ **2026-10-28 代码评审 A-21 后本条已过期(原文不删)**:「枚举镜像 + 本程序集刻意零引用」**作废** —— `Editor.Tools.Level.asmdef` 现 `references: ["Sim"]`,`LevelSource` 直接用 `Sim.World.SlotType`;名字↔序数唯一出处 = `Editor.Tools.Bake/SlotTypeVocabulary.cs`(派生自同一枚举,带 `[1,30]` 位域守卫)。理由:镜像与运行期 `1 << (int)type` 之间**没有任何门**,唯一联系是「枚举值恰好 = 下标 + 1」这个**从未被断言的巧合** |
| 2 | `unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs` | 具体种子:9 格 Decor(3×3)+ 4 格 Shell 条 = 13 格,余 243 格非槽位 |
| 3 | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 确定性 JSON(UTF-8 **无 BOM**、字段序固定、行序 (x,y,z) 升序);导出前调 `LevelConsistencyCheck.CheckOrThrow` |
| 4 | `unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` | 槽位子集 3 条硬失败(重复格 / `None` 与未定义枚举 / 越界)+ **7 条未实现项清单** |
| 5 | `unity/Assets/Editor.Tools.Bake/WorldBuildSlotsBinder.cs` | 五道域门,聚合 `throw`(承 ADR-014 §四) |
| 6 | `unity/Assets/Editor.Tools.Bake/WorldBuildSlotsCookedWriter.cs` | deterministic 二进制,行序 (x,y,z) 升序,20 字节头沿用 `ClinicMachineCookedWriter` |
| 7 | `unity/Assets/Editor.Tools.Bake/WorldBuildSlotsBake.cs` + `DataBakeMenu.BakeWorldBuildSlots` | 烘焙入口 + 菜单项 |
| 8 | `unity/Assets/Gameplay.Presentation/WorldBuildSlotsCookedCodec.cs` + `DataCookedDisk.LoadBuildSlots()` | 读方,与写方镜像 |
| 9 | `unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs` | `Create()` 从产物装载并 `RegisterSlot`;**失败保留空目录 + `LogError` + 如实横幅** |

#### 本次真产物(非手写)

```
assets/data/world_buildslots.json            13 格(Decor 9 · Shell 4),由 `LevelExporter` 导出
unity/Assets/DataCooked/world_buildslots.cooked.bytes   296 B,ConfigVersion = 0x146822D2
```

导出命令(可复算):`Unity -batchmode -projectPath unity -executeMethod
DaYiJingCheng.EditorTools.Level.LevelExporter.ExportP0ClinicToRepo -quit`

#### AC(逐条可判伪)

| # | 判据 | 载体 |
|---|---|---|
| **C-1** | 真种子过五道域门,且 Decor / Shell **两个类型分支都可达** | 夹具 |
| **C-2** | 生产核(`DataBakeMenu.BakeWorldBuildSlotsCore`)**真会拒**越界格,报文含判据词 + 坏格坐标 + 表名前缀 | 夹具(报文关键词) |
| **C-3** | 同一最小仓根换成合法行 ⇒ 生产核**放行**(证明 C-2 不是「核恒抛」) | 夹具(与 C-2 成对) |
| **C-4** | 其余域门各有一条负向夹具:空表 / 重复格 / `None` / 缺 `baked_initial` / 未知键 | 夹具 ×5 |
| **C-5** | 同源两次烘焙**逐位一致**;cooked 往返**逐字段相等**;盘上产物 = 现烘字节 | 夹具 ×3 |
| **C-6** | **目的判据**:`MinimalWorldFactory.Create().SlotCatalog.Count > 0`,且真实槽位 `Check(...)` **不再** `SlotNotFound` | 夹具(端到端) |
| **C-7** | `BakedInitial` **两侧都有真实取值**(有 `true` 也有 `false`)—— 全 true 意味着「开档医馆已建满」= 玩家无可建格 | 夹具 |

#### 挂标不记绿(如实,禁勾)

1. ~~**生产 build 的 Addressables 装载面未接**~~(**2026-10-28 已兑现,原文不删** ——
   `MinimalWorldFactory` 走 `DataCookedDisk`(磁盘直读 `Assets/DataCooked/`),而 build 里
   该目录不存在 ⇒ 目录为空 + `LogError`。修法:`world_buildslots` 进 `data-core` 组 +
   `AddressablesDataProvider.Load<WorldBuildSlotsDataSet>()` + `MinimalWorldFactory` 收 provider。
   **本卡未做**(与 `CompositionRoot` 的 provider-first 形状不一致,是一处**已知偏离**)。
   ⇒ **已由 §十二 修复轮全部兑现**:四处集合闭合 + 组条目 24→26 + catalog 重建
   + 夹具 3 条 + 证伪 2/2 exact,原件 `fix-s20-20-slot-provider-surface-2026-10-28.md`。)
2. ~~**`BakedInitial` 运行期不应用**~~(**2026-10-28 已兑现,原文不删** ——
   原文:`BuildSlotCatalog` / `BuildSlot` 今日**没有承载该标记的字段** ⇒ 数据声明了、运行期不落地。
   `MinimalWorldFactory` 因此打 `LogWarning` 点名它,不静默。
   ⇒ **已由挂标 #2 修复轮全部兑现**:schema 1→2(每行另带 `module_id`)+ `BuildSlot` 承载
   `BakedInitial` / `ModuleId`(`BuildSlotCatalog.cs:38,40`)+ `World.ApplyBakedInitial`
   (`World.cs:90`)发号 1…N 进 `StructureInstanceRegistry` + `CompositionRoot.cs:918` 在
   `StructureWriter` **构造之前**调用。那条 `LogWarning` 已删(缺口已闭,警告失去对象)。
   证据件 `fix-s20-20-baked-initial-baseline-2026-10-28.md`;证伪 5 变异 + 补口 2 条守卫。
   > 🔁 **2026-10-28 评审修复轮(见 §十三)**:补做独立评审时查出 **3 条缺陷**并已修:
   > ① **B-1** `ApplyBakedInitial` 先置标志后跑本体 ⇒ 半应用 + 重试报文误导
   > (`World.cs:101-128` `try/catch` 回滚四样;`Reset()` 同型加固 `:196-215`);
   > ② **B-2** fail-closed 夹具骨架**单格** ⇒「零落库」断言**恒真**
   > (改**三格** + 前置探针 + 占用/计数归零 + 可重试);
   > ③ **S-1** 导出门判 `== NoModule(-1)`,而 ADR-022 `:176` 写 `≥ 1` ⇒ `module_id = 0` 漏过
   > (`LevelConsistencyCheck.cs:103-119` 改 `<= 0`)。
   > **证伪 M2 / M4 各恰 1 条红;还原 SHA-256 逐字节通过。**
   > ⇒ 上文「5 变异 + 补口 2 条守卫」是**挂标 #2 实现轮**的数;**评审修复轮另有 2 条**。
   > 原件:`production/qa/evidence/review-s20-20-2026-10-28.md`。
3. **C1–C6 七项未实现** —— 除槽位子集 3 条外的全部(需视觉层 / 地形 / 导航 / 生态区数据,
   本批不产出)。清单在 `LevelConsistencyCheck.UnimplementedChecks`,**返回值里的数据**。
   > 🔁 **2026-10-28 实测:仍 7 条,一条未减**(`LevelConsistencyCheck.cs:57-66`)。
   > **承接者已立**:`production/epics/level-tool/story-006-c1-c2-c2prime-and-ci.md`。
   > ⚠️ **立件 ≠ 缺口已闭** —— 本卡不改判,本条**维持未闭**。
4. **关卡工具只服务一个关卡** —— `P0ClinicLevel` 是硬编码的 16×1×16。多关卡 / chunk 切片归完整工具。
   > 🔁 **2026-10-28 实测:仍硬编码**(`P0ClinicLevel.cs:37` `Width = 16` · `:46` `LevelId = "p0_clinic"`;
   > `LevelExporter.cs:120` 仍写死 `P0ClinicLevel.Define()`)。
   > **承接者已立**:`level-tool/story-001-level-source-generalization.md`。
   > ⚠️ 同上,**维持未闭**。
5. **世界几何仍是合成** —— 地形全 walkable、POI / 资源格全 -1。本卡只让「槽位判定」这一面有真数据。
   > 🔁 **2026-10-28 实测:仍合成**(`MinimalWorldFactory.cs:43` 自陈「地形全 walkable + POI / 资源格全 `-1`」;
   > `BootRoot.cs:220` `IsSyntheticWorld = true`)。`assets/data/world_*.json` 仅 **1 件**
   > (ADR-022 §三 要 7 件)。
   > **承接者已立**:`level-tool/story-002-world-geometry-and-terrain.md`。
   > ⚠️ 同上,**维持未闭**。

> 📌 **本节的 5 条,2026-10-28 实测汇总:1 条已闭(#1)· 1 条已闭(#2)· 3 条未闭(#3/#4/#5)**。
> 未闭三条**全部有了承接者**(`production/epics/level-tool/`,2026-10-28 立),但
> **承接者存在 ≠ 缺口已闭** —— 6 个 story 一条未实现。逐条复算命令见各条 🔁 注记。

---

## §十一 S20-19 代码评审 A-5 修复轮(2026-10-28 · 用户指令「处理 A-5:修掉 SolePlayer 赋值」)

**`Status: Complete`** · 原件 `production/qa/evidence/fix-a5-sole-player-2026-10-28.md`

评审原件 `review-s20-19-2026-10-28.md` 在收口后**真跑**过一次(lean 模式下用户触发),
判定 `CHANGES REQUIRED`,余下**唯一 BLOCK = A-5**:`InventoryFoldContext.SolePlayer`
生产侧零赋值 ⇒ `ApplyBuildCost` 恒走「归因不可判」出口 ⇒ **建造扣料在玩家路径上从不发生**。
本轮结清它。

| 项 | 结果 |
|---|---|
| 修法 | **消掉分叉本身**,不是补赋值:`BuildOwnerSlot` 构造点上移到 `InventoryLoadService` 之前 ⇒ `BuildController` / `BuildStockQuery` / fold 读**同一个 int**;未绑定期 `Owner = -1` ⇒ fold 拿 `null` ⇒ fail-closed |
| 连带补齐 | `InventoryFoldContext` 的 `Items` / `Recipes` / `Modules` 此前**全为默认**(只补 `SolePlayer` 仍扣不动 —— `Modules == null` 走同一条缺口);新增 `DataCookedDisk.LoadRecipes()` |
| 夹具 | `Tests/EditMode/Boot/inventory_fold_sole_player_wiring_test.cs`(装配级 2 条:绑定前 fail-closed / 绑定后恰扣 `cost(m)`) |
| 证伪轮 | `s20-19-a5-sole-player.json` **2/2 exact**(M1 退回 `(int?)null` ⇒ 绑定后那条红;M2 去掉 `IsBound` 守卫 ⇒ 绑定前那条红);还原 SHA-256 逐字节通过,受保护路径指纹未变 |
| 回归(三元组) | EditMode `5678/5643/0/0/35`(基线 `5676/5641` ⇒ **+2/+2**,等式 = 新增夹具 2 条)· PlayMode `238/234/0/2/2`(**零回归**;`exit=2` 系既有 2 条 `inconclusive`)@ `2026-10-28T02:49Z` · A-5 修复轮全量 · `85f248e8`(工作树干净) |
| 评审判定 | `CHANGES REQUIRED` → **`APPROVED WITH SUGGESTIONS`**(BLOCK 3 → 0) |

**本件证明不了的 3 点**(禁借绿,已在原件 §七 逐条登记):
① 「槽里的 id 就是玩家」靠 `BootRoot` 铸造序,夹具里 `Owner = 42` 是夹具值;
② Addressables(`dataProvider != null`)那一半零夹具;
③ 将来若加「读档后立即 Rebuild」,须同时在绑定后补一次 —— 否则存档里的 `StructurePlaced`
会以 `SolePlayer = null` 折叠一次并被缓存。

**余下非 BLOCK 项不变**:12 条 GAP 夹具待裁 · `A-25`(player 可用性 = 0)属设计上不改码 ·
S20-20 的 5 条挂标(Addressables 装载面 / `BakedInitial` / C1–C6 / 单关卡 / 世界几何)。

---

## §十二 S20-20 挂标 #1 修复轮(2026-10-28 · 用户指令「处理 #2:补 Addressables 装载面」)

**`Status: Complete`** · 原件 `production/qa/evidence/fix-s20-20-slot-provider-surface-2026-10-28.md`

病因两条,同一条装配链的两个缺口:
① **生产 build 里槽位目录恒空** —— `MinimalWorldFactory` 只有磁盘那一半
(`DataCookedDisk` 读 `Assets/DataCooked/`,该目录**不随玩家构建出货**);
`AddressablesDataProvider` 侧**没有** `WorldBuildSlotsDataSet` 分支 ⇒ 连走对路的可能性都不存在。
② **组条目缺席** —— 产物 2026-10-28 09:45 落盘,但盘上 26 份 / 组内 24 条(缺 `world_buildslots`
与既有欠账 `foraging_constants`)。

| 项 | 结果 |
|---|---|
| 修法 | **四处集合闭合**:`WorldBuildSlotsAddress` 常量 → `EnsureWorldBuildSlots()` → 进 `Preload()` → 进 `DataCoreAddresses`(22 → **23**);`MinimalWorldFactory.Create(IDataProvider)` **provider 优先**、`null` 才走磁盘兜底;`BootRoot` 传 `preloaded ? DataCorePreloader.Provider : null` |
| 降级语义 | provider 失败 ⇒ **空目录 + `LogError`**,不静默回落磁盘;报文新增「装载源」(`SlotSourceLabel`),两条路径在日志上可分 |
| 组与 catalog | 菜单实跑:组 24 → **26** 条;`BuildAddressablesContent` 重建 catalog(可复算:`strings …/catalog.bin \| grep -o "[a-z0-9_]*\.cooked\.bytes" \| sort -u \| wc -l` ⇒ **26**) |
| 夹具 | `Tests/EditMode/Boot/world_slots_provider_wiring_test.cs` **3 条**:① 合成 1 行产物经 provider 送达 ⇒ 恰 1 格(盘上真产物 13 格);② provider 失败 ⇒ 空目录 + `LogError` 点名装载源;③ **A-5 §七-2 的 provider 半边** —— 三份表经 provider 送达时扣料仍恰少 `cost(m)` |
| 既有门 | `provider_wiring_gate_test` 门①追加 address ↔ 产物名三条;门②③④(组条目 ⊇ 常量 / 组→盘文件 / 四处集合)**12/12 绿** |
| 证伪轮 | `s20-20-slot-provider-surface.json` **2/2 exact**(M1 三元分支退回磁盘 ⇒ ① ② 红;M2 fold 物品表 provider 分支置空 ⇒ ③ 红);还原 SHA-256 逐字节通过,受保护路径指纹未变。⚠️ **首版 M1 是废轮**:锚点打在**条件行**并整行替换 ⇒ 三元分支悬空 ⇒ 编译失败(退出码 6、零 XML),已订正为动**分支**后重跑 |
| 回归(三元组) | EditMode `5681/5646/0/0/35`(基线 `5678/5643` ⇒ **+3/+3**,等式 = 新增夹具 3 条)· PlayMode `238/234/0/2/2`(**零回归**;`exit=2` 系既有 2 条 `inconclusive`)@ `2026-10-28T03:09Z` · 本轮全量 · `56633bce` + 未提交工作树 |
| 连带发现 | 源码级门 `world_assembly_wiring_test::test_ac201b` 断的是字面串 `MinimalWorldFactory.Create()` ⇒ 加实参后必红。**改判据不删断言**:现要求「调用了工厂」**且**「实参非无参」—— 无参正是本轮病灶形态,门的判别力反而更强 |
| doc 订正 | `DataBakeMenu.BakeWorldBuildSlotsCore` 的「生产 build 的 Addressables 装载面**尚未接**」就地加注(原文不删) |

**本件证明不了的 4 点**(禁借绿,原件 §七 逐条):
① `BootRoot:214` 的实参无夹具(源码承担,未加文本级门);
② Addressables 真实 catalog 的**装载**未测(字节走注入缝;条目存在性由门②按文件级判);
③ `foraging_constants` 进组后仍**无消费者**(既有欠账,不在本轮裁决面);
④ `DataCoreAddresses` 22 → 23 ⇒ 全局 `ConfigVersion` 组合值改变 ⇒ 旧存档头判「不匹配」
(ADR-014 §五 的设计内行为;P0 无对外存档,未做兼容处理)。

**S20-20 余下挂标不变**:#2 `BakedInitial` 运行期不应用 · #3 C1–C6 七项未实现 ·
#4 关卡工具只服务一个关卡 · #5 世界几何仍是合成。

---

## §十三 追加批评审补落 + 四条 BLOCKING 修复(2026-10-28 · 收口后第二轮)

> **缘由**:sprint-20 收口后追加批的 S20-17 / S20-18 / S20-20 三件**零独立评审**,
> 而 `story-done` 的「评审报告原件」条款是 **BLOCKING 级**(见
> `coding-standards.md` §Review Evidence Standards)。补做评审时**查出 4 条真缺陷**,
> 其中 2 条在本会话已提交的代码里(`d230eb36` 的 S20-20 挂标 #2)—— 故本轮 = **补原件 + 修缺陷**。

### 三份评审原件(补落)

| 对象 | 原件 | Verdict |
|---|---|---|
| S20-17 | `production/qa/evidence/review-s20-17-2026-10-28.md` | APPROVED WITH SUGGESTIONS(1 条须修 S-1 已订正) |
| S20-18 | `production/qa/evidence/review-s20-18-2026-10-28.md` | 原 CHANGES REQUIRED ⇒ 修复后 **APPROVED** |
| S20-20 | `production/qa/evidence/review-s20-20-2026-10-28.md` | 原 CHANGES REQUIRED ⇒ 修复后 **APPROVED** |

> ⚠️ **三份均为事后补做**,评的是 **2026-10-28 当天树上的代码**,
> **不追认**各卡收口时点的任何判定(承 `review-workflow` §不可追补)。

### 四条缺陷(全部已修 + 已证)

| ID | 缺陷 | 修法 | 落点 |
|---|---|---|---|
| **S20-20 B-1** | `ApplyBakedInitial` 先置标志后跑本体 ⇒ 半应用 + 重试报文误导 | `try/catch` 回滚(实例表 / 占用 / 标志 / 计数四样复位);`Reset()` 同型加固 | `Sim/World/World.cs:101-128` · `:196-215` |
| **S20-20 B-2** | fail-closed 夹具骨架**单格** ⇒「零落库」断言**恒真** | 骨架改**三格**`[合法, 非法, 合法]` + **前置探针**(单格不抛世界证「合法格真会落库/占格」)+ 占用/计数归零 + 可重试 | `world_buildslots_bake_test.cs:523-601` |
| **S20-20 S-1** | 导出门判 `== NoModule(-1)`,而 ADR-022 `:176` 写 `≥ 1` ⇒ `module_id = 0` 漏过 | 判据改 `<= 0`,报文按分支分措辞 | `LevelConsistencyCheck.cs:103-119` |
| **S20-18 B-1** | `Placed` 重放同号覆盖 ⇒ `Restore` 不碰占用表 ⇒ 旧足迹**永久阻塞** | `Placed` 支先 `FreeCells(existing.OccupiedCells)`(与 `Removed`/`Modified` 同构) | `StructureRestore.cs:183-215` |
| **S20-18 B-1b** | `StructureWriter.PeekNextId` 与注册表**双计数器**,两方向静默脱钩 | 删写者镜像字段,`PeekNextId` 委托注册表(唯一发号者) | `StructureKinds.cs:287-299` · `:329-332` |

> **B-1b 是主会话复核时新查出的**,不在任何专家的原判定里 —— 补原件的过程本身发现了新缺陷。

### 证伪轮(5 条,全部 exact)

| 变异 | 打什么 | 期望红 | 实测 |
|---|---|---|---|
| **M1** | 删 `StructureRestore` `Placed` 支的 `FreeCells` | `test_ac6b` | 恰 1 条 ✅ |
| **M2** | 删 `ApplyBakedInitial` 的 `try/catch` 回滚 | `…moduleNotInCatalogThrows` | 恰 1 条 ✅ |
| **M3** | `PeekNextId` 退回常量(模拟镜像字段) | `test_peekNextId_delegatesToRegistry` | 恰 1 条 ✅ |
| **M4** | 导出门判据改回 `== SlotSpec.NoModule` | `…WithModuleIdZero…` | 恰 1 条 ✅(`-1` 那条仍绿 ⇒ 两分支可分) |
| **M5** 🔁 | 删 `World.Reset()` 的 `catch` 三行回滚(留 `throw`) | `test_b1p_reset_baselineThrowsMidway_…` | 恰 1 条 ✅(16 条里 15 绿) |

> 🔁 **M5 是 2026-10-28 补做的第 5 条**(原文 4 条不删)。它补 `World.Reset()` 侧的回滚——
> 该半边此前只有阅读验证,却标着 `Proven ✅`,**自相矛盾**(Phase 8b:未跑判据不得标已证)。
> **此前认定它不可达是错的**:生产注入的是接口 `IModuleCatalog`,注入方可改其返回 ⇒
> 真造得出「基线中途抛」。夹具用**两件**开档家具(第一件落库、第二件模块被撤)才有残留可观测
> —— 单件时回滚前后都是空,**无判别力**。实测残留 `Expected: 0 / But was: 1`。

**还原**:五处 `cp 回 + SHA-256 逐字节`(**未用 `git checkout` / `git stash`**);
`grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l` = **0**。

### 回归(三元组)

截至时刻 `2026-10-28 14:47 CST` · 批次 `S20-20 评审修复轮 · 全量` · commit `78d12421` + 本批未提交改动

| 套件 | 结果(XML 原值) | 基线(S20-18 收口 · `c784d88`) | 差 |
|---|---|---|---|
| EditMode | `5698 / 5663 / 0 / 0 / 35` | `5694 / 5659 / 0 / 35` | **+4 / +4**,零失败 |
| PlayMode | `238 / 234 / 0 / 2 / 2` | `238 / 234 / 0 / 2 / 2` | 逐项相同,零回归 |

**+4 的用例级点名**(汇总差不算,须点名):
`test_ac6b_replayPlacedOverBaselineId_releasesOldFootprint` ·
`test_peekNextId_delegatesToRegistry_notLocalMirror` ·
`test_levelTool_bakedInitialWithModuleIdZero_reportsViolation` ·
`test_levelTool_bakedInitialWithoutModuleId_reportsViolation`。

> 🔁 **2026-10-28 M5 补做轮(原文上表不删)** —— 三元组:
> 截至时刻 `2026-10-28T09:17Z` · 批次 `M5 补做轮 · 全量` · commit `28c9584c`(工作树含本批未提交改动)。
>
> | 套件 | 结果(XML 原值) | 基线 | 差 |
> |---|---|---|---|
> | EditMode | `5699 / 5664 / 0 / 0 / 35` | `5698 / 5663 / 0 / 35`(本文件上一表) | **+1 / +1**,零失败 |
> | PlayMode | `238 / 234 / 0 / 2 / 2` | `238 / 234 / 0 / 2 / 2` | 逐项相同,零回归 |
>
> **+1 的用例级点名**:`test_b1p_reset_baselineThrowsMidway_leavesDefiniteEmptyState` = 恰 1 条新增,零删除。

> 🔁 **2026-10-28 §七-2 真存档往返轮(原文上表不删,下方为新实测)** —— 三元组:
> 截至时刻 `2026-10-28T10:22Z` · 批次 `§七-2 往返轮 · 全量` · commit `f58cb2c6`(工作树含本批未提交改动)。
>
> | 套件 | 结果(XML 原值) | 上一轮(M5 补做) | 差 |
> |---|---|---|---|
> | EditMode | `5701 / 5666 / 0 / 0 / 35` | `5699 / 5664 / 0 / 0 / 35` | **+2 / +2**,零失败 |
> | PlayMode | `238 / 234 / 0 / 2 / 2` | `238 / 234 / 0 / 2 / 2` | 逐项相同,零回归 |
>
> **`+2` 的用例级点名**(汇总差不算,须点名):① `test_ac9g_realSaveBytes_roundTripRebuildsWorld`
> —— **本轮**新增;② `test_worldBuildSlots_productMatchesSourceJson_fieldByField` —— 由
> `f58cb2c6` 引入,该批**只跑 targeted 未跑全量** ⇒ 在本轮才首次计入全量数。
> ⇒ 本轮真实新增 = **1** 条。**不得**把 `+2` 全记在本轮头上。

> 🔁 **2026-10-28 §七-1 嵌套异常轮(原文各轮不删,下方为最新实测)** —— 三元组:
> 截至时刻 `2026-10-28T11:22Z` · 批次 `§七-1 嵌套异常轮 · 全量` · commit `cc239594`(工作树含本批未提交改动)。
>
> | 套件 | 结果(XML 原值) | 上一轮(§七-2 往返轮) | 差 |
> |---|---|---|---|
> | EditMode | `5702 / 5667 / 0 / 0 / 35` | `5701 / 5666 / 0 / 0 / 35` | **+1 / +1**,零失败 |
> | PlayMode | `238 / 234 / 0 / 2 / 2` | `238 / 234 / 0 / 2 / 2` | 逐项相同,零回归 |
>
> **`+1` 的用例级点名**:`test_b1q_rebuild_rollbackAlsoThrows_originalExceptionNotMasked`
> = 恰 1 条新增,零删除。复算见 `production/qa/evidence/review-s20-20-2026-10-28.md` §六。

> 🔁 **2026-10-28 §七-6 Addressables 重建复跑轮(原文各轮不删,下方为新实测)** —— 三元组:
> 截至时刻 `2026-10-28T12:04Z` · 批次 `§七-6 重建复跑轮 · 全量` · commit `b53a53d6`(工作树含本批未提交改动)。
>
> | 套件 | 结果(XML 原值) | 上一轮(§七-1 嵌套异常轮) | 差 |
> |---|---|---|---|
> | EditMode | `5702 / 5667 / 0 / 0 / 35` | `5702 / 5667 / 0 / 0 / 35` | **全零** |
> | PlayMode | `238 / 234 / 0 / 2 / 2` | `238 / 234 / 0 / 2 / 2` | **全零** |
>
> **全零是预期形态** —— 本轮是**纯文档 + 只读实验**,零代码改动 ⇒ 用例数不应变。
> (⚠️ 反过来说:若本轮出现 `+N`,那才是异常信号。)
> 复算见 `production/qa/evidence/review-s20-20-2026-10-28.md` §六 的同轮块。

### 本轮明确未做(NOT-RUN,禁借绿)

1. ~~**`World.Reset()` 侧回滚无独立变异** —— 与 `ApplyBakedInitial` 共享同一语义,
   但**不是同一个执行体**;按「一属性一条变异」严格说应有独立变异,本轮**未做**。~~
   > 🔁 **2026-10-28 已闭**(原文不删):补夹具 + 变异 **M5**,实测恰 1 条红、SHA-256 还原通过。
   > ⚠️ **仍未覆盖的子项**:「嵌套异常不遮蔽原始异常」需 `StructureRestorer` 的第二次
   > `Reset()` 也抛才可观测 —— 本夹具不覆盖,**该子项仍开,不以 M5 借绿**。
   > 🔁 **2026-10-28 嵌套异常轮:该子项现已闭,且抓到真缺陷**(原文不删)。夹具
   > `test_b1q_rebuild_rollbackAlsoThrows_originalExceptionNotMasked`;修复
   > `Sim/World/StructureRestore.cs:171-203`(把回滚调用包住,`throw;` 必达)。
   > **修复前该夹具实跑红**,报文 `But was: "槽位 (3,0,1) 的 module_id = 8 不在模块目录内…"`
   > ⇒ 回滚失败那条把根因顶掉,而 `World.cs:197-216` 与本项上文都自述「避免嵌套异常顶掉原始异常」
   > —— **自述当时未兑现**。证伪:**M12 独占**(恰 1 条红)、**M13 不独占**(4 条红),
   > 两条属性各一条,不合并。证据原件 = `production/qa/evidence/review-s20-20-2026-10-28.md` §四/§五/§七-1。
2. **schema 迁移期基线 id 段归属未裁** —— B-1 的修复只保证**足迹一致**,
   **不裁定「谁该赢」**(基线家具被 schema-1 存档里的结构顶替,是语义问题,需 ADR 级裁定)。
   > 🔁 **2026-10-28 订正 + 已入册(原文不删)** —— 两处修正:
   > ① **「schema-1 存档」是用词错误**。仓库里「schema 1」专指 `world_buildslots.json` 的
   > **作者态 JSON 格式**,与**存档格式** `SaveVersion`(`CurrentSaveVersion = 1`)是两个量。
   > 真实类别是**时序**:基线由 `d230eb36`(2026-10-28)引入,该日之前的存档里没有基线。
   > ② **同会话内不可达(已证)**:基线 `CompositionRoot.cs:918` 与 writer `:941` 共用同一
   > 注册表,`Restore` 抬高高水位 ⇒ 玩家放置必得 `N+1` 起。撞号只在「基线诞生前的存档被重放」时出现。
   > ⇒ 已登记为债册 **`TD-building-02`**(`open` / **`P0 面 = N`** / 归 `modular-building`),
   > 含三个触发条件(版本号首次递增 / 首个迁移脚本注册 / P0 首次对外发放存档),
   > 以及「今日可达性 = 零」的实查(盘上无真实存档 · `CurrentSaveVersion = 1` 从未变 · 零个 `ISaveMigration`)。
   > ⚠️ 原文「**schema 迁移期**」这个提法也随之失效 —— 今天根本没有迁移期。
3. ~~**`BootRoot:214` 实参文本级门未加**(§十二 的 ① 项,本轮仍未做)。~~
   > 🔁 **2026-10-28 订正:该项已做**(原文不删)。落点不在 `provider_wiring_gate_test`,
   > 而在 `world_assembly_wiring_test.cs:105`(`test_ac201b` 二次收紧)—— 那里本来就是
   > `BootRoot.cs` 的源码级门。判据要求真分支**逐字**是 `DataCorePreloader.Provider`;
   > 上一版只要求「实参是三元」⇒ `Create(preloaded ? null : null)` 仍绿(病灶)。
   > 变异 spec = `tools/falsify/specs/s20-88-bootroot-provider-arg-gate.json`(已入库)。
   > ⚠️ **仍是源码级判据,不是运行期观测** —— 无夹具真跑 `BootRoot` 全链。
   > 另:证据件里记的 `BootRoot.cs:214` 是当时行号,实测已漂到 **`:219`**。
4. **真存档往返未跑**;`world_buildslots.cooked.bytes` 内容未逐字段解码。
   > 🔁 **2026-10-28 两半全闭**(原文不删)。① **真存档往返**:新增夹具
   > `test_ac9g_realSaveBytes_roundTripRebuildsWorld`(`world_load_service_test.cs`)——
   > `SaveService.Checkpoint` 落真文件 → 新流 `Load` → `WorldLoadService.Rebuild()` 到
   > **新建的 World 实例**上(不是落盘前那个),并先塞三条**不在存档里**的残留要求全清。
   > ② **产物逐字段解码**:`test_worldBuildSlots_productMatchesSourceJson_fieldByField`
   > (`world_buildslots_bake_test.cs`,`f58cb2c6` 引入)—— 正则解析源 JSON + 独立解码产物字节,
   > 两端都不经被测件。详见 `review-s20-20-2026-10-28.md` §七-2 / §七-3。
   >
   > ⚠️ **补做过程中新揭一条债**:载荷 blob 池是**进程内存**(`CompositionRoot.cs:132`),
   > 而存档字节只带 `PayloadRef`、不带载荷字节,ADR-010 §一 的 v1 布局也**无载荷段**
   > ⇒ **跨进程读档今日不可用**。该边界已从「注释里的声明」升级为**跑出来的断言**
   > (夹具末段:空池 + 同一份存档 ⇒ 断言抛,报文须含 `BlobId`)。
   > 登记为 **`TD-persist-09`**(归属 `persistence-service`,`P0 面 = Y`)。
   > ⚠️ **编号订正**:我先把它记成 `TD-building-04` —— **错了**,缺口在存档布局与池实现,
   > 不在建造投影;挂 23 会让 epic 分布记错一格。

5. **未跑 `Addressables` 内容重建**(§十三 之后新增,承 `review-s20-20` §七-6)。
   > 🔁 **2026-10-28 已闭(且实测证伪了上一轮的归因)**(原文不删)。三件事:
   >
   > ① **「理论上不影响烘焙产物」已实测证实**:复跑后产物**逐字节不变**
   > (`catalog.bin` SHA-256 `f52175cb…5cd3` · `BuildResultHash` `5d2deedb…8ce`,
   > **连续四轮同值**),而 `BuildStartTime` 每轮都在变(`13:52:34`→`19:23:46`→`19:25:52`→`19:44:15`)
   > ⇒ 构建真跑了,这轮改动(`Editor.Tools.Level`)不进产物。
   > `CatalogFreshnessGateTest` **3/3 绿**(含 `_premise` 探针),见 `unity/Logs/s20-73-catalog-gate.xml`。
   >
   > ② ⚠️ **上一轮的归因「必须关 Search 索引」被证伪**。索引开关**全程 `true`**
   > (`Search.settings` mtime 停在 `13:53:22`、SHA-256 `0b3e5423…`),而本轮**四格成功构建
   > 全在索引开启下跑成**。真实原因是 `addr6` 同时改了三样(`-quit` / 去重复 `-executeMethod` /
   > 关索引),**归因落在错的那一样上**。索引对构建成败无因果:有 `-quit` 时索引根本不启动,
   > 无 `-quit` 时索引在**构建之后**才启动(`noquit` `:715`→`:742`)。
   >
   > ③ **真实必要条件 = 单条 `-executeMethod`**(六格判别矩阵,正序两格 Ensure 跑/Build 不跑、
   > 反序一格 Build 跑/Ensure 不跑 ⇒ **Unity batchmode 只执行第一条,其余静默丢弃,零警告**)。
   > 由此新揭 **`TD-ci-02`**:`BuildAddressablesContent` 不调 `EnsureDataCoreGroup`,
   > 两入口无合并 ⇒ 写成两条 `-executeMethod` 时**组建好但 catalog 不重建、退出码 0** = 假绿。
   > 详见 `review-s20-20-2026-10-28.md` §七-6 与债册 `TD-ci-02`。
   > 🔁 **2026-10-28 同日已闭**(用户裁定「②自检 + ④Build 自足」)—— 落点 / 5 条变异实测 /
   > 还原哈希 / 账务见债册 `TD-ci-02 关闭记录`。⚠️ `TD-ci-01`(CI 常驻矩阵缺凭证)仍 open,
   > 本条只消除「一旦 CI 真跑会踩的假绿」这一半。
   > ⚠️ **诚实边界**:「只跑第一条」是**六格归纳**非源码级机制说明(Unity 未文档化);
   > `addr5` 那格来自上一轮日志(本轮未重跑),但本轮 `double-exec` / `double-noquit`
   > 两格同形态且为本轮实测 ⇒ 结论不依赖旧日志。
