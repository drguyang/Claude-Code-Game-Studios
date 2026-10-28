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
| DoD 10 条 | 全勾,零 `[ ]`(带 5 处加注) | 本文 §Definition of Done `:71-130` |
| smoke-check | **PASS**(两版数,禁混读) | `production/qa/smoke-2026-10-27-sprint20.md` —— 收口轮 `EditMode 5385/5350/0/35` · `PlayMode 204/204/0/0`;🔁 **链尾收口后**:`EditMode 5529/5494/0/35` · `PlayMode 229/228/0/1`(@ `2026-10-27T09:34Z` · `S20-15 批` · `e095b7b`) |
| QA 签核 | **APPROVED WITH CONDITIONS** | `production/qa/qa-signoff-sprint-20-2026-10-27.md`;C-1…C-8(首版 6 条,10-27 追加 C-7/C-8)。🔁 **收口后进展**:C-1(事实消解)· C-3 / C-4 / C-5 / C-6 / C-7 / C-8 **已闭**;余 **C-2(EXTERNAL,`TD-ci-01`)** —— 🔁 **2026-10-27 再收窄**:两格矩阵已实跑绿,`AC-18-15` 仍缺 processing 侧黄金夹具 + ARM64 格 + CI 常驻。<br>⚠️ **C-4 闭的是「有没有评审」,不是「代码通过评审」** —— 补评的三份里两份判 `CHANGES REQUIRED`,抓出 3 条新缺陷 |
| 证伪轮 | 每件 ≥1 轮,全 exact | 见 DoD 第 5 条注;S20-4 / S20-7 有 `FALSIFY-` 零残留 + SHA-256 逐字节记录 |
| 收口提交 | ✅ **已落 —— 即本提交** | 复算:`git log -1 --format='%h %s' -- production/sprints/sprint-20.md`;<br>sprint-20 实现区间 = `9bbef11`..`51193cf` 共 **41 笔**,收口提交为其后第 **3** 笔(`0a64faf` S20-4 · `34c75e9` S20-7 · 本笔四件套) |
| 债册复算 | **已实跑** | 见 §9.2(`--print` 实跑输出,`--check` exit 0) |
| retro | 已落 | `production/retrospectives/retro-sprint-20-2026-10-27.md` |
| CAPACITY-MODEL 三元行 | 已补 | `production/CAPACITY-MODEL.md` 表 20 行 |

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
**`Gameplay.Boot/MinimalWorldFactory.cs:109`**,由 `BootRoot.cs` 的
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
| AC-1 | `Gameplay.Boot/MinimalWorldFactory.cs:109` | `test_ac201_*`;反证 M1 |
| AC-1b | `Gameplay.Boot/BootRoot.cs:213` | `test_ac201b_*`(钉住「不是死工厂」) |
| AC-2 | `Gameplay.Boot/CompositionRoot.cs:858` | `test_ac202_*`;反证 M2 |
| AC-3 | `CompositionRoot.cs:819`(注册表取自 `world.Structures`) | `test_ac203_*`;反证 M3 |
| AC-4 | 写者 `occupancy` = 同一 `World` | `test_ac204_*`;反证 M4 |
| AC-5 | `Sim/World/StructureKinds.cs:214`(既有) | `test_ac205_*`;反证 M5 |

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

---

### S20-19 — 42 建造发起方接线(兑现 `O-23-6`)

| 项 | 内容 |
|---|---|
| **Owner** | unity-specialist(规格段 + `ux-designer` 复审) |
| **复杂度** | **M**(规格段 S + 接线段 M) |
| **前置(全部 ✅)** | S20-17 / S20-18(`World` 与 `StructureWriter` 已装配、世界可由流重建)· `InventoryTransaction`(S20-14,先全验后落流)· `ModuleDefinition.Cost` / `BuildItemKey`(2026-10-25/26 两轮补)· `DemolishChecker` P0 返还率 1.0(`:84/:125`) |

#### 用户裁定(2026-10-28,三项,与 S20-18 同批)

| # | 裁定 | 取项 |
|---|---|---|
| ① | **范围** | **规格 + 逻辑接线** —— 幽灵预览只落接口与量化结果,**不做可见渲染** |
| ② | **模态归属** | 建造 = **世界内操作态,不占 `ModalId`**(闭集维持 7 员) |
| ③ | **材料成本** | **本轮接扣减**(按 `inventory-and-items.md` 规则六的原子单位) |

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
