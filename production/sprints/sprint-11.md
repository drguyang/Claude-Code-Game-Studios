# Sprint 11 — 判断回路前台化 + 治理收口(2026-10-14 规划)

> **规划依据**:三专家研判(技术总监 / 创意总监 / QA 负责人,2026-10-14 Opus 级并行研判)
> **头条订正**:四系统(13/8/37/11)epic 均已于 2026-10-06→10-08 story-Complete ——
> M2 README「16/31」为 2026-10-03 旧快照,实测 ≈**22/31 epic Complete**。
> **sprint-11 实为「补体验 + 治理收口」,非「照 GDD 实现新系统」。**
> **用户裁定(2026-10-14)**:范围 = 草案全收(Must×3 + Should×1 + Nice×2);
> S11-3 处置方向 = **修 8 测**(CI 门保持「退出码 = 0」严格口径)。

## Sprint Goal

把判断回路从「流里通」变成「玩家眼前通」——9 查体 revealed 接线让脉案值区运行期非空
(支柱一「判断为骨」首次可玩),同时清掉三笔承重治理债(W-1 会签 / 退出码 2 / 处方 DoD 残留)。

**补评修复批(2026-10-15 用户裁定并入)**:清 `case-system` story-001~004 补评认定的
**两处真实现缺陷 + 一处免责失效项 + 一处设计冲突**(S11-7…S11-10),外加一项零成本订正(S11-11);
**断言形态整批顺延 sprint-12**(见 DoD「顺延面显式登记」)。

## Capacity

- Total days: 8(solo)
- Buffer (20%): 1.6 天
- Available: ≈6.4 天(草案 8-11pd,超容部分自然顺延 sprint-12)
- 已用:S11-1 + S11-2 + S11-3 = **3.75 天**(三件均 done)⇒ **余量 ≈2.65 天**
- ⚠️ **既有 backlog 已超容**:S11-4(3.5)+ S11-5(1.5)+ S11-6(0.75)= 5.75 天 ⇒ 未加新活已超 **3.1 天**
- **补评修复批追加 5 项 ≈2.85 天**(S11-7…S11-11)⇒ 本轮合计超容 ≈5.95 天
- **处置(用户裁定「必须项优先」)**:S11-6(nice,0.75 天)**顺延吸收**;S11-4(should)/ S11-5(nice)保留不动;
  修复批按「真缺陷优先」切,断言形态整批顺延 sprint-12 —— **不做超额承诺**

## Tasks

### Must Have (Critical Path)

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-1 | **9 查体 revealed 接线 → 8 前台回路**(脉案值区运行期非空;顺手结 O-13-7 `OnExamSessionChanged` 发出侧登记) | unity-specialist + Claude | 2-3 | M2 链已闭(e2e_full_loop 绿) | 脉案值区运行期显示查体产出(`RevealedRows` 非空);`e2e_full_loop` + 五 wiring 测零回退;O-13-7 结案登记 脉案值区运行期显示查体产出(`RevealedRows` 非空);`e2e_full_loop` + 五 wiring 测零回退;O-13-7 结案登记 | **QA**:Integration 测 `examine_revealed_wiring_test`(~4-6 测)+ 人工连走(查体→显示→落笔→开方) |
| S11-2 | **W-1 会签裁决**(ScriptedChain 三案链 9+37 二选一;图样可辨识度 × 8 可读性互斥性验证) | game-designer + user | 0.5 | 无(纯决策轮) | 裁决落盘(case-system.md W-1 行 + 决策记录);「不裁不得实现联调」解除;若裁互斥 ⇒ 支柱四 P0 落点重裁 裁决落盘(case-system.md W-1 行 + 决策记录);「不裁不得实现联调」解除;若裁互斥 ⇒ 支柱四 P0 落点重裁 | **QA**:会签记录(互斥性可证伪结论;user+designer+qa-lead 三签) |
| S11-3 | **EditMode 退出码 2 处置 = 修 8 测**(8 个旧测试 `new MonoBehaviour` ⇒ 真夹具) | Claude | 0.5-1 | 无 | 全量 EditMode **退出码 = 0** 且 XML failed=0 双达标;判决口径在 sprint 文件登记(两信号不再混读) 全量 EditMode **退出码 = 0** 且 XML failed=0 双达标;判决口径在 sprint 文件登记(两信号不再混读) | **QA**:XML 实证订正 = 7 测/2 文件/8 处 new(host_authority + o6_actor_cell_payload);纯测试面修复,禁动产品代码 |

### Must Have — 补评修复批(case-system 001–004 · 2026-10-15 用户裁定并入)

> **来源**:`production/qa/evidence/review-case-system-story-00{1..4}-retro-2026-10-15.md` +
> `review-case-system-001-004-gap-audit-2026-10-15.md`(含 §七 勘误轮)。
> **净保留判定 22 条 → 去重 ≈13 类**;本批只取「**真缺陷 / 免责失效 / 设计冲突 / 零成本**」四类,
> 断言形态整批顺延 sprint-12。**修复一律归新一轮**,不得声称「原判定如此」(义务第 3 条)。

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-7 | **story-002 BLK-8:墨龄取错病人(真实现缺陷)** —— `CaseStreamQuery.LastJudgmentOf` 以 `caseId` 过滤(现 `caseId` 形参**零引用**,自承「P0 简化:调用方负责传入该案事件子集」)+ `CompositionRoot.cs:245-246` 改传**该案子集**(现传 `stream.Events` = 全局混合流)+ `CasebookVitalsSource.cs:214` 注释「按 case 过滤」与实现对齐 | Claude | 0.75 | 无 | 新增**跨病人交错夹具**(2 病人各一案交错)可证伪:改回全局流即转红;`EditMode/Boot/ink_age_test.cs` 现 4 测**全为单案夹具**,须补跨案一测;`InkAgeTicks` 不再取到他病人判断事件 |
| S11-8 | ✅ **DONE 2026-10-15** —— **story-003 B-1:复诊案永远无法结案(真实现缺陷)** —— `CaseCloseQuery.IsClosed` 改按 `CaseId` **三元组**精确匹配(原 `CaseCloseDecider.cs:135` = `e.Patient.Value == caseId.Patient`,粒度从 `case_id` 退化为 `Patient`)+ **统一 `IsOpen`/`IsClosed` 输入契约**。⚠️ **本条两处计划口径已就地改判**(见下方订正框):判等路径不用 `PayloadRef` 解码,契约取「整条全序流」而非「该案子集」 | Claude | 0.75 | 无 | 流 `[Opened(100,1,0), Closed(200,1,1), Opened(300,1,2)]` 上 `IsClosed(newCaseId) == false`(**已交付**:35/35 绿;改回旧实现 ⇒ 退出码 2 · 28/35 · **7 红**,含复诊案由 `CanClose` 退化为 `RejectAlreadyClosed` 的现象本体);**同批补流层判据**(已交付:端到端组装用例 —— 此前全文件无一条把两条流接进 `Decide`);`case_close_precondition_test.cs:133` 硬编码 `isClosed:false` 的假绿解除(已交付:含 `:146`/`:162`/`:510`,共 4 处) |
| S11-9 | **story-004 CB-2/TB-5:AC-37-29 零覆盖,且免责前提当日失效** —— 落地 `ChainEligible` / `ScriptedChain`(全库 `.cs` **零命中**,连 story `:40` 自认「先落」的那半边也没落);**W-1 已于 2026-10-15 结案**(三案各挂 `DIS_DYSENTERY`,会签记录 `case-system.md:610-627`)⇒ 夹具今日即可写 | Claude | 0.75 | 无(W-1 已解除) | 二符号从零命中转为**有实现 + 有测试**;夹具覆盖「脚本链名额**不被随机病例消耗**」+「**一次性**」两条语义 |
| S11-10 | ✅ **DONE 2026-10-15** —— **story-003 B5:处置集收录 `InjuryOnset`(设计冲突)** —— **撤 `InjuryOnset`**(「病人挨了打」被计入「已被处置」⇒ 玩家把病人打到即可满足结案前置,正面击穿「空手连关三例不可能」)· **补 `CareApplied`**(8 的护理动作,病史流,原**未收**,取舍方向与语义相反)· 处置集枚举**回填权威**:`adr-008` §四(**已于 2026-10-15 先行补立**)+ `case-system.md` F-37.2 `:671`/`:674`/`:686` + 规则六 `:255` + 规则八 `:283`(**闭环四处已核一致**) · **顺批 ADVISORY A-1**:`Set` 收为 `private` 真源 + `Contains` + `MembersSnapshot()`(返副本) | Claude | 0.5 | 无 | 已交付:39/39 绿;**回退成员表即转红**(退出码 2 · 39 测 **4 红**,含 `equalsAuthorityExactly` 打印出两个集合);**双向穷尽**判据 = 期望值**逐字引自 `adr-008` §四**(不引生产默认值);`EmergencyAttempt` 的**正确排除**保持(ADR-009 Amendment I) |
| S11-11 | ✅ **DONE 2026-10-15** —— **story-001 B-3:测试头 AC 号一号两义(零成本)** —— `case_identity_routing_test.cs` 自填 `AC-37-03/04/05/06`,与 GDD 权威编号**指向完全不同的需求**(GDD:`03` = 已结案再结幂等拒收 · `04` = 未勾「已处置」拒绝 · `05` = 判断记录落病例流 · `06` = 迁移重放一致),且已被姊妹测试占用并声称覆盖;story 那 4 条 bullet **本就无编号**,测试头是自行编号。⚠️ **范围就地订正**(见下方订正框):retro 只点名**测试头 `:5-8`**,实测误编号有**三层 25 处** | Claude | 0.1 | 无 | 已交付:全文件去号(头 4 + 体注释 14 + **方法名 10**),保留 `AC-37-01/02`(与 GDD 一致)与 `AC-37-14`(合法);`13/13 绿 · 退出码 0 · error CS = 0`,**测数 13 → 13**;判据 = 同号前缀下方法数 `test_ac3703` **7→2** / `ac3704` **4→2** / `ac3705` **5→3** / `ac3706` **1→0**,修复前 7 个方法同挂 `AC-37-03` 而 5 个与该 AC 无关 ⇒ 重复计数逐字成立 |

> ⚠️ **S11-8 计划口径就地订正(2026-10-15 执行期 · 两处,均已报用户并获批)**
> **① 「经 `PayloadRef` 解码 `CaseClosedPayload.CaseId` 判等」= 结构上不可行。** 该建议同时出现在
> 补评件 §二 与本 story 实现注释 `CaseCloseDecider.cs:129`(作者自承「实际生产代码应通过 PayloadRef 解码」),
> 但 `PayloadCodec` 住 **`Sim.Codec`**,而 `CaseCloseDecider` 住 **`Sim`** —— `Sim` 引用集**恰 =
> {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门),`PayloadRef` 只有 `BlobId/Offset/Length`,**物理上够不着**。
> ⇒ 改走 **流序扫描关联**(与既有正确范式 `CaseStreamQuery.OpenCaseOf` 同口径)。
> **代价已登记在实现注释**:该口径下 `CaseClosed` 的归属是**推断**而非**读取**;与载荷解码**在合法流上等价**,
> 仅在违反「同一病人同时至多一个开案」(GDD 规则二)的**非法流**上分歧 —— 该流下写者行为本身未定义。
> **② 契约取「整条全序流」,不取「该案子集」。** 原计划写「统一为『该案子集』」;但两函数**签名都收整条
> `caseEvents`**,而 `Sim` 侧**没有**「按 case_id 切片」的合法手段(同 ① 的引用集约束)——写者/调用方
> 也无从构造该子集。⇒ 统一口径改取**「入参 = 病例流全序前缀」**,由同一扫描派生两值,结构上不可再分叉。
> **两处订正均属「计划写了一个做不到的形状」,非范围缩减**;story-003 的 🟢 登记块有完整记录。
> 📄 **修复证据原件** = `production/qa/evidence/fix-s11-8-case-system-story-003-b1-2026-10-15.md`
> (自洽可复核;因 `unity/Logs/` 被 gitignore,日志不可充当交付证据)。

> ⚠️ **S11-11 计划口径就地订正(2026-10-15 执行期 · 已报用户并获批「全文件去号」)**
> **retro B-3 只点名「测试头 `:5-8`」,实测该误编号在文件内有三层、共 25 处**:
> ① 测试头 4 处 · ② 文件体注释 14 处 · ③ **方法名 10 处**(`test_ac3703_a1_…` 等)。
> **只改测试头等于没改** —— 按 AC 号做的覆盖矩阵会扫**文件任意位置**(含方法名),重复计数照旧产生。
> ⇒ 按用户裁定取 **①+②+③ 全去**,保留 `AC-37-01/02`(与 GDD 一致)与 `AC-37-14`(GDD `:1110`,本文件是其载体半边)。
> **改名安全性 = 实测**:全仓 grep 确认被改名方法**零外部引用**;UTF 按 `[Test]` 特性发现,`-testFilter` 走类全名。
> ⚠️ 另有一处**计划文档自身的引用偏差**:S11-11 行原写 GDD 权威编号在 `case-system.md:1036-1051`
> —— 实测该区间是**「信息边界」节**,AC 表在 **`:1075-1092`**。已就地订正,登记于此备查。
> 📄 **修复证据原件** = `production/qa/evidence/fix-s11-11-case-system-story-001-b3-2026-10-15.md`。

**本批不取(整批顺延 sprint-12,须在四件 story 内显式登记)**:
story-001 B-1(AC-37-06 判据**算术上不可证伪**,须退设计重述)· B-2(kindgen A2 空跑)· B-4(kindgen 借绿);
story-002 的 5 条断言形态(BLK-2/3/5/9 + BLK-1 恒真半边);
story-003 B2(快照半场 —— **写者未落,无载体可测**,见 DoD 写者延后条)· B3(AC-37-18 恒真夹具,须澄清「病人死亡可否表态」);
story-004 的其余 7 条(TB-1 定序判别力 / TB-2 成员冻结 / TB-3 真空夹具 · CB-1/CB-3 实现侧 · 及 `TA-6` 元规则「定序类 AC 夹具须至少一处 输入序 ≠ 期望序」)。

### Should Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-4 | **prescription DoD 残留**(AC-11-22 7a 白名单本体 + DC-2 `action_id` master 登记(OQ-11-2) + DC-6 `NOISE_BAND_9` 复评——9 侧常量 10-07 已立,OQ-11-13 10-06 已裁,前置已解锁) | Claude | 3-4 | S11-3 顺(同仓测试基线) | prescription EPIC §Epic Status 三项逐项闭合或显式结转;若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解) prescription EPIC §Epic Status 三项逐项闭合或显式结转;若动 7a 须带 case↔7a 折叠语义集成夹具(TD 风险 1 缓解) | **QA**:EditMode 断言 ×3(AC-11-22 IL 扫描/DC-2 闭集 fail-loud/DC-6 引用链闭环) |

### Nice to Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|--------------|---------------------|
| S11-5 | **53 medical-consequences 起首 story**(支柱四载体,机械自足 P0 水龙头,纯 C# + SplitMix64,Engine Risk LOW) | Claude | 1-2 | 9+37 已 Complete ✓ | story 立件 + 判据面成型;不强求当轮完成 story 立件 + 判据面成型;不强求当轮完成 | **QA**:随立件定测试路径(Logic 级) |
| S11-6 | **11 人工走查 AC-11-12/13/21**(前置:先修 Story009WalkthroughDriver `passed` 汇总 bug) | user + Claude | 0.5-1 | Driver bug 修复 | 三 AC 走查证据落 playtests/;Driver 汇总判定修复登记 三 AC 走查证据落 playtests/;Driver 汇总判定修复登记 | **QA**:人工走查三签(主创/医学/音频);Driver passed 汇总修后自证 |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| 用户数值轮(8 门槛族 / 37 半径/就诊触发 / 11 EFF 逐档 / 13 分档) | 承「数值用户自己调」——四系统旋钮批成一轮,不逐一阻塞 | 未估(用户轮) |
| story-008 资产半(止血/CPR 素材 + 手势 VFX) | BLOCKED-BY batch-03 外部链,维持 P2 | — |
| 六屏 UX 走查余五屏 | 桌面集中轮缓办(AC-42-F1 NOT-RUN 已登记);脉案屏随 S11-1 顺带 | — |
| B-6-2 焦点可见性(独立 story) | sprint-08 唯一 open,未入本轮排期 | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| case↔7a 折叠语义集成未验证(TD 风险 1,LIVE) | 中 | 存档正确性/高水位重构 | S11-4 动 7a 必带集成夹具(未结案存档折叠 + 三流并集 max 验证) |
| UI Toolkit 焦点桥假设 6 半可信 | 中 | S11-6 走查面失真 | 走查前小 spike;失败则登记另裁 |
| case-system 001-004 无评审原件 | 低 | 治理缺口(不可追补条款) | QA 计划前置批核:核当年有无 BLOCKING 判定,有则补做评审,无则登记结论 |
| **case-system 001–004 顺延面未清(≈15 条断言形态)** | **高** | 四件**不得转 `Complete`**,EPIC 维持 4/6 In Review;若 sprint-12 再度被挤占,治理债跨 sprint 累积 | 本 sprint 只切「真缺陷优先」四项;顺延面**逐条带状态+归属**登记进四件 story 与 EPIC;若 sprint-12 无法承接,回到用户裁定(不自行降级为「接受」) |
| **S11-7 / S11-8 触及生产代码**(前几批治理轮均为纯测试面) | 中 | 可能破 M2 已闭链(e2e_full_loop / vertical_slice / 五 wiring) | 两处修改**须先跑** M2 链基线;`Sim` 侧改动过门 A + ADR-029 + kindgen 三关;禁动 `Sim` 引用集 |
| W-1 裁决出「互斥」 | 低 | 支柱四 P0 落点重裁(设计层) | S11-2 预案:互斥则升级用户裁定,不擅自改设计框架 |

## Dependencies on External Factors

- 无外部阻塞(ADR-023 S1-S7 与 45 联机夹具经 grep 证实对四系统 P0 机械面**零阻塞**——TD 研判 C 节)
- batch-03 外部素材链(story-008 资产半)持续 BLOCKED,不在本轮解除面

## Definition of Done for this Sprint

- [ ] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria
- [ ] QA plan exists (`production/qa/qa-plan-sprint-11.md`)——**开工前必落**
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations
- [ ] Code reviewed and merged
- [ ] **M2 链零回退**(sprint-11 特有):e2e_full_loop + vertical_slice + 五 wiring 测全绿——四系统正压在已闭 M2 Exit 链上,破链 = 重开 M2
- [ ] **NOT-RUN 显式结转**(sprint-11 特有):未闭 AC(AC-11-12/13/21 · case W-1 后续 · story-005 等)逐项带状态+归属,禁借绿
- [ ] **退出码口径结清**(sprint-11 特有):EditMode 判决以「退出码 + XML failed」双信号一致为准,S11-3 后两信号不得再混读
- [ ] **补评修复批五项闭合并带判据**(sprint-11 特有):S11-7…S11-11 各自的 AC 须有**可证伪断言**——
  「改完即算」不成立;S11-7/S11-8 的修复须给出**改回旧实现即转红**的反证
- [ ] **写者延后正式登记**(sprint-11 特有 · 用户裁定 2026-10-15):story-003 / story-004 的 §Out of Scope
  增列「37 结案写者 / `PatternRecognized` 写者延后」并**同步订正 AC-37-08/09 的证据面** ——
  现 story 自认含 `Append` 步却无写者(`WriterExistenceGate.cs:87-88` 具名豁免),
  是 `Complete` 里的**悬空**,不是可静默的欠账
- [ ] **顺延面显式登记**(sprint-11 特有):001 的 B-1/B-2/B-4 · 002 的 5 条断言形态 · 003 的 B2/B3 ·
  004 的其余 7 条 —— 整批**带状态 + 归属**结转 sprint-12;四件 **维持 `In Review`**,**禁借绿**

## 三专家研判归档

- 技术总监:四系统全 Complete 的头条订正 + 轨 A 治理/轨 B 53 双轨建议 + 风险 top3(7a 折叠/W-1/处方 DoD 歧义)
- 创意总监:8 查体前台回路为体验主轴(`RevealedRows` 恒空 = 支柱一不可玩)+ W-1 互斥性承重债 + 体验债分级进
- QA 负责人:测试债清单(退出码 2/Driver bug/case 评审原件核查)+ story-009 当日模式 + 整 sprint 一份 QA plan + DoD 三条特有判据
