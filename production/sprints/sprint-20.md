# Sprint 20 — **`2026-10-25`** 起 · clinic 链尾 + A-2 机制起步 + 判别力债清偿

**周期**:`2026-10-25` 起(不设结束日期口径,做完即收,**收口时补记实际收口日** —— 承 sprint-14…19 同例;⚠️ retro-19 行动项 3:收口四件套与最后一个实现轮**同日**,不再拖)。

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
- [ ] All Must Have tasks completed(本体交,半边挂标**不得记 `Complete`**)
- [ ] All tasks pass acceptance criteria(逐条判定 + 判据;禁「已落盘」当判据)
- [ ] QA plan exists(`production/qa/qa-plan-sprint-20-*.md` —— **本轮规划批即跑 `/qa-plan sprint`**)
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] 每新增/改写夹具过证伪轮(同命令改→跑→还原,SHA-256 逐字节;一属性一条变异)
- [ ] Smoke check passed(`/smoke-check sprint`)
- [ ] QA sign-off:APPROVED 或 APPROVED WITH CONDITIONS(`/team-qa sprint`)
- [ ] No S1 / S2 bugs
- [ ] 设计文档偏离已更新;代码已评审(双列结账表 + Branch 列)
- [ ] **收口四件套同日化**(retro-19 行动项 3):头部收口日 + §九 + retro + CAPACITY-MODEL 行

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
