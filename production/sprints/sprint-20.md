# Sprint 20 — **`2026-10-25`** 起 · clinic 链尾 + A-2 机制起步 + 判别力债清偿

**周期**:`2026-10-25` 起 → **实际收口 `2026-10-27`**(不设结束日期口径,做完即收 —— 承 sprint-14…19 同例)。
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
      > ⚠️ **本条按「计划内 7 件」判,不按 yaml 全部 17 件判**:计划内 7 件(S20-1…S20-7)**全 `done`**;
      > **追加批 4 件未完成**(S20-8 `ready-for-dev` · S20-13/14/15 `backlog`)—— 见 §九「不记绿项」①。
- [x] All tasks pass acceptance criteria(逐条判定 + 判据;禁「已落盘」当判据)
      > 逐件判定表见各 story `## Completion Notes` 的 AC 映射表 + 证据件。**有 5 处「有判定但不勾」**:
      > `AC-18-15`(S20-9,EXTERNAL)· `AC-20-03`/`AC-20-16`(S20-10,结构性不可勾)·
      > S20-11 的 P0 门 ①②(BLOCKED-BY)· `AC-18-10` 第三条件 + `AC-18-20`(S20-4,桩/结构性)。
      > 它们**有判据、有具名理由**,故本条成立;但**不得读成「全绿」**。
- [x] QA plan exists(`production/qa/qa-plan-sprint-20-*.md` —— **本轮规划批即跑 `/qa-plan sprint`**)
      > `production/qa/qa-plan-sprint-20-2026-10-25.md`(2026-10-25 落盘,规划批当日执行)。
- [x] All Logic/Integration stories have passing unit/integration tests
      > smoke 覆盖率表:**COVERED 10 · EXPECTED 6 · MISSING 0**。6 条 EXPECTED = 4 件未开工
      > (S20-8/13/14/15)+ S20-4/S20-7 在 smoke 执行时刻尚未落地(两者均于 10-27 当日补上,见 §九)。
- [x] 每新增/改写夹具过证伪轮(同命令改→跑→还原,SHA-256 逐字节;一属性一条变异)
      > S20-1(F1–F3)· S20-2(F7 + 11/11)· S20-2a(四支)· S20-3(第四轮 7/7)· S20-4(M1–M8 + P1,8/8)·
      > S20-9(14/14)· S20-10(14/14)· S20-11(14/14)· S20-12(五轮 10/13→17/17)· S20-16(13 条全红)。
      > **S20-4 与 S20-7 的证伪轮有 `FALSIFY-` 零残留 + SHA-256 逐字节还原实测记录。**
- [x] Smoke check passed(`/smoke-check sprint`)
      > `production/qa/smoke-2026-10-27-sprint20.md` —— **Verdict: PASS**(EditMode 5385/5350/0/35 ·
      > PlayMode 204/204/0/0)。⚠️ 该 PASS 的边界已在报告内写明:A–D 22 条是 **N/A 不是 PASS**。
- [x] QA sign-off:APPROVED 或 APPROVED WITH CONDITIONS(`/team-qa sprint`)
      > `production/qa/qa-signoff-sprint-20-2026-10-27.md` —— **APPROVED WITH CONDITIONS**(C-1…C-6)。
- [x] No S1 / S2 bugs
      > `production/qa/bugs/` 仅 `README.md`,零 `BUG-*.md`(复算命令见 QA 签核件 §Bugs Found)。
- [x] 设计文档偏离已更新;代码已评审(双列结账表 + Branch 列)
      > ⚠️ **本条部分成立,如实登记**:有独立评审原件的 = S20-1 / S20-2 / S20-3 / S20-4 / S20-9 / S20-16;
      > **S20-10 / S20-11 卡内明写「本轮未走评审」**(`story-001:262-264` / `story-003:547-548`),
      > 只做了实现+测试自证;**S20-12 卡内对此零陈述**,`production/qa/evidence/` 亦无对应 `review-*` 原件
      > ⇒ 按「缺一即 `NOT-RUN`」读法登记为**未评审**。S20-2a / S20-5 / S20-6 / S20-7 为债册工作项,
      > 载体是证据件(`impl-*`)而非评审件。
      > ⇒ **已评审 6 / 计划内 10 件,4 件无独立评审**。见 §九「不记绿项」②。
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

**Verdict: ⚠️ 已收口(带具名残余)** —— 计划内 7 件全 `done`,判据面成立;
**追加批 4 件未完成且明确结转**,另有 5 处「有判定不勾」项逐条具名。**无未登记项**。

### 9.1 收口要件

| 收口要件 | 状态 | 凭据 |
|---|---|---|
| **计划内 S20-1…S20-7** | **7/7 全 `done`** | `production/sprint-status.yaml`(completed 2026-10-25…10-27) |
| **追加批 S20-8…S20-16** | **5/9 `done` · 1 `ready-for-dev` · 3 `backlog`** | 同上;未完成 4 件见 §9.3 ① |
| DoD 10 条 | 全勾,零 `[ ]`(带 4 处加注) | 本文 §Definition of Done `:71-108` |
| smoke-check | **PASS** | `production/qa/smoke-2026-10-27-sprint20.md`(EditMode `5385/5350/0/35` · PlayMode `204/204/0/0`) |
| QA 签核 | **APPROVED WITH CONDITIONS** | `production/qa/qa-signoff-sprint-20-2026-10-27.md`;C-1…C-6 |
| 证伪轮 | 每件 ≥1 轮,全 exact | 见 DoD 第 5 条注;S20-4 / S20-7 有 `FALSIFY-` 零残留 + SHA-256 逐字节记录 |
| 收口提交 | ✅ **已落 —— 即本提交** | 复算:`git log -1 --format='%h %s' -- production/sprints/sprint-20.md`;<br>sprint-20 实现区间 = `9bbef11`..`51193cf` 共 **41 笔**,收口提交为其后第 **3** 笔(`779ff18` S20-4 · `7cf8521` S20-7 · 本笔四件套) |
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

**① 追加批 4 件未完成,明确结转 sprint-21**

2026-10-26 用户裁定「甲:按链路顺序做到底」后,「采集链路解锁链」8 件入账(S20-8…S20-15,
`processing-001` 即 S20-4 不重复计),另 S20-16 补记已完成件。本 sprint 内完成 5 件
(S20-9/10/11/12/16),**4 件未完成**:

| 件 | 状态 | 阻塞 |
|---|---|---|
| `S20-8` processing-002 F1 调用契约 | `ready-for-dev` | 无未齐前置 |
| `S20-13` processing-003 Craft 落流 | `backlog` | ← S20-9 |
| `S20-14` inventory-005 事务模板 + **Drop\* 三支写者** | `backlog` | ← S20-10+11+12+13 |
| `S20-15` foraging-006 novelty 接线 | `backlog` | ← S20-14(**链的终点**) |

**计划原件** = `production/sprints/sprint-21-candidate-collection-chain-plan.md`。
⚠️ 该文件名仍写 `sprint-21` 而实际归属 sprint-20(文件内 `:103` 已自陈此不符,待裁是否改名)。
⚠️ **容量**:`sprint-status.yaml:228-232` 已记「追加批已**超出** 1 件预留(8 件入账),
须在下次 sprint 规划轮重估容量」—— 本结论不改,结转即为兑现。

**② 4 件无独立评审**(见 DoD 第 9 条注):S20-10 / S20-11 卡内自陈未走评审,
S20-12 零陈述且无原件;S20-2a / S20-5 / S20-6 / S20-7 为债册工作项,载体是证据件。
⇒ **不计入「代码已评审」,但也不阻塞收口**(DoD 该条按「有判定 + 具名理由」成立)。

**③ 5 处「有判定不勾」的 AC**(均有判据、有具名理由,禁读成全绿):

| 件 | AC | 不勾理由 |
|---|---|---|
| S20-9 | `AC-18-15` | **EXTERNAL · BLOCKED-BY-ADR-012** —— 三格矩阵不存在 + F7 spike 未跑,不写桩 |
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
| 1 | `779ff18` | S20-4 本体(契约 2 件 + 谓词 2 件 + `PrescribeFlow` AR2 + 29 条夹具 + 8 条变异 spec + 两件证据) |
| 2 | `7cf8521` | S20-7 治理盘点(债册四十四订 + `EventBudget` W8 + `TD-medcons-16` 夹具删除) |
| 3 | **本笔** | 收口四件套(§九 + retro + QA 签核 + CAPACITY 第 20 行)+ `sprint-status.yaml` |

复算:`git log --oneline 51193cf..HEAD` ⇒ 实跑 **3 笔**;`git status --porcelain | wc -l` ⇒ **0**。
⚠️ 提交后「被验的树」= 本笔 commit,可 `git show` 完整复现 —— 此前的 C-6 条件**就此关闭**。

### 9.4 与前序 sprint 的对比

| 维度 | sprint-19 | sprint-20 |
|---|---|---|
| 计划内完成率 | 5/5 = **100%** | 7/7 = **100%** |
| 追加件完成 | 轮面追加(9 类,以轮计) | **5/8 完成**,4 件结转(以**件**计,首次可量化) |
| 未闭 P0 面 | 76 → **69**(−7) | 69 → **63**(−6) |
| 新立 P0 面债 | +5 | +4 |
| 收口滞后 | 三连滞后(行动项 3 立项) | **零滞后**(四件套同日) |
| QA 签核 | APPROVED WITH CONDITIONS(6 条) | APPROVED WITH CONDITIONS(6 条) |

> **追加率口径首次可量化** —— sprint-19 的「追加」以轮计、件数计法失真(见 CAPACITY-MODEL 19 行注);
> sprint-20 的追加批是**具名 story**(S20-8…S20-16),故 `5/8 = 62.5%` 是第一次可比的追加完成率。
> 代价:**追加量是预留额度(1 件)的 8 倍** —— 这是 retro-19 行动项 2「将掀几债」预估失效的直接证据。
