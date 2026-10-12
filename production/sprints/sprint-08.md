# Sprint 08 — 方笺交互面轮(把接线宿主变成玩家可操作)

> **Status**:🚧 **In Progress** — 2026-10-12 开(四专家评估修订版落盘)
> **2026-10-12 开 · 上游**:sprint-07 收口(`70c2e5f`;S1–S3 + C1–C3 全绿 + 第五轮 playtest Q1 双半达成)
> **本 sprint 判据**:核心循环七环节**全链玩家可达** —— 落笔(病名册指认 → 三态循环)与
> 开方(戥子选档 → 处方)两环从「接线宿主就位、玩家摸不着」变为**玩家可操作 + 机械判据可证伪**
> **规模**:6–8 人日(含 S0 前置窄批 + DTO 契约钉死 + 端到端 PlayMode 门)
> **规划依据**:sprint-07 C3 + 第五轮 playtest 登记残留 ①③;四专家评估
> (producer / lead-programmer / qa-lead / technical-director)2026-10-12 全量过审

---

## §1 背景:七环节差的最后两环

sprint-07 后七环节状态:走近 ✅ · 查体 ✅(E 键,人工+自动化双半)· 对照 ◐ ·
**落笔 ◐**(接线宿主 `PrescriptionPageHost` 就位,但入口在 42 UI 侧,玩家无任何按键能触发
`WriteJudgment`)· **开方 ◐**(同上,`TryPrescribe` 无玩家入口)· 施治 ◐(F 键事件入流、
零可见反馈 = P2 story-008)· 纸面反馈 ◐。

**根因**:42 的方笺页只有 `PrescriptionPageHost`(语义动作 → 写者的接线接收点),
**交互面缺失** —— 病名栏 / 病名册 / 方笺翻页 / 戥子选档全部未实现;
`CasebookScreen` 单页 224 行无翻页;病名册全仓零实现(grep 0 命中)。
与 sprint-07「灰盒三层根因」同型:不是判据写错,是**面没立起来,唯一可测形态就是空态**。

**四专家评估结论(2026-10-12)**:方向照准,判据与范围须修订 ——
四项裁定全为窄修订/erratum(TD),判据面按修订后形状重写(qa-lead),
病名册/方笺页为净新范围(producer P0),Submit 路径取甲案 + 枚举登记(lead-programmer BLOCKING)。

---

## §2 任务清单

### S0 前置窄批(两处文档裁定 · 先行解锁)✅ 完成 2026-10-12

| 项 | 落点 | 判据(可证伪) |
|---|---|---|
| S0.1 ADR-011 §二 窄 Amendment | ✅ **Amendment C 已落盘**(`adr-011-input-architecture.md` §二节首 + Status 头指针):50ms 直读纪律**仅约束急救动作**;拟物 UI 焦点导航与 Submit 路径归 ADR-013,不受方差约束(TD 甲案;乙案 Boot 直读否决留档) | Amendment 落盘;42 挂 `NavigationSubmitEvent` 不再与 ADR-011 §二 字面冲突 |
| S0.2 UX spec K 计数回写 | ✅ **已回写**(`casebook-39.md` §5.2 / §13×2 / §14 第 5 条四处):K 7→8(五通道 + 问诊栏 + 病名栏 + **折边**);OQ-CB-5 甲「零新焦点位」语义澄清 = 仅置信度不增焦点位,不含折边 | K 计数与焦点序一致;历史行原文保留 + ⭑ 订正注(不删原文惯例);8/42 侧 F3 算例归其回写轮 |

> **BLOCKING-2 降级注记(lead-programmer 实测 → TD 复核推翻)**:
> `InputBoundaryGates.DeliveredIntentTypes` 是 `Editor.Tools.Gates` 构建期清单,**非 ADR-025
> 管辖的运行时装配边界** —— 新增意图 = 同文件追加一行,向既有枚举加成员是类型内变更,
> 不触装配引用集。**无需 ADR-025 装配轮**(初判 blocking 为误判,留痕)。

### S1 病名栏 + 病名册整面(落笔环节玩家可达 · 42/37)

> **S1.0 前置(病名册词表烘焙管线)✅ 完成 2026-10-12** —— 兑现 story-005 的 NOT-RUN 缺口
> 「AC-37-35 的构建期双射校验半边:ADR-014 阶段 2 校验器未建」。词表源
> `assets/data/case_judgment_lexicon.json`(3 条:伤寒→病种1,湿温/温毒孤儿)→ 阶段2 绑定
> (`JudgmentLexiconBinder`,聚合 throw + 未知键拒)→ 校验器(`JudgmentLexiconValidator`:
> AC-37-35 双射拒 / ordinal append-only / 非空 / u8 上界)→ cooked 产物入库
> (`case_judgment_lexicon.cooked.bytes`,73 B)+ 运行期读方(`JudgmentLexiconCookedCodec`,
> E-13 硬失败)。**11 测全绿**;全量 EditMode **3268 = 3221/0/1inc/46skip**(基线 3257 ⇒ +11
> 零回归)。修的真 bug ×3:① `ComputeRepoRoot` 少退一层(3 测红)② 绑定器空串检查抢先
> ⇒ 校验器 `lexicon-text` 判据成不可达死代码 ③ `disease_count` u8 静默截断 + 假注释
> (校验器补上界 + 负夹具测)。证据:`unity/UnityLogsTmp/s8_s10_lexicon_filter4.xml` ·
> `s8_s10_editmode_full.xml`。**S1.1 UI 面的前置已就绪**(词表可读、可指认)。

| 项 | 落点 | 依赖 | 判据(可证伪) |
|---|---|---|---|
| ~~S1.1 病名册 UI 整面~~ **✅ 2026-10-12** | 42 新面:病名栏 Submit → 翻开病名册(OQ-CB-1 甲案);条目 = **已见过的病名**(派生态,由事件流重建;反幻想二:未见过的病名不可指认);**DTO 条目显式携带 `lexicon_id`**,Submit 用该字段而非显示索引(TD 第五风险 —— 显示序 ≠ lexicon_id 序时隐式映射 = 第二真源失效) | S0 | `test_lexiconBook_unseenDisease_notPointable`:Given 病名未见过;When 翻册;Then 条目集不含该病 |
| ~~S1.2 指认 → 落笔~~ **✅ 2026-10-12** | 玩家指认条目(虚拟按键注入,非自动)→ `lexicon_id` 落 `JudgmentRecorded`(病例流)+ `PrescriptionPageHost.WriteJudgment` → 墨龄 > 0 | S1.1 | `test_lexiconBook_pointIdentify_writesJudgmentRecordedWithLexiconId`:Then 病例流新增 JudgmentRecorded,lexiconId = X ordinal + confidence = 疑似 + 墨龄 > 0;**变异删接线 ⇒ 零事件恰红** |
| ~~S1.3 置信度三态循环~~ **✅ 2026-10-12** | 同栏重复 Submit = OQ-CB-5 甲案:空 → 疑似 → 确定 → 擦除回空(零新焦点位) | S1.2 | `test_lexiconBook_submitCycle_threeStates`:When 同栏再 Submit ×2;Then confidence 循环 疑似→确定→擦除 |

### S2 方笺页渲染 + 翻页 + 戥子选档开方(开方环节玩家可达 · 42)

| 项 | 落点 | 依赖 | 判据(可证伪) |
|---|---|---|---|
| ~~S2.1 方笺页渲染 + 翻页~~ **✅ 2026-10-12** | `CasebookScreen` 增翻页机制(原单页零翻页);方笺书写面可见 + **戥子实例化**(档位 = hi−lo+1;黄铜侧读数元件,ADR-013 Amendment B ③,禁降级数字角标);**折边首次实现**(S0.2 已计进 K 但代码零实现)+ **页缘 ×2(前案/后案)入 K ⇒ K=10** | S1 | EditMode `casebook_page_turn_test`(翻页状态机 + 不环绕 + 折边 + **零流事件 AC-39-10** + 档数/userData/零假数据)+ PlayMode `casebook_page_turn_wiring_test`(真 panel 派发:后案 ⇒ 翻页 / 前案 ⇒ 翻回 / 折边 ⇒ 折叠 / 档位行 ⇒ ordinal);**变异删 `RegisterCallback` ⇒ wiring 恰红** |
| ~~S2.2 戥子选档确认 → 开方~~ **✅ 2026-10-12** | 选档确认 → `PrescribeRequestDriver.TryPrescribe` → `DrugTreatmentApplied` 入病史流;接线 = `ICasebookCommandSink.SubmitDose` → `CasebookCommandRouter` → `PrescriptionPageHost.Prescribe` | S2.1 | EditMode `casebook_prescribe_chain_test`:戥子落点 ⇒ DrugTreatmentApplied 入病史流;档序数经唯一量化点(档 1 vs 3 ⇒ 不同 dose_potency);**变异删接线 ⇒ 宿主未接时 fail-loud 恰红** |
| ~~S2.3 拒开 + 防重入~~ **✅ 2026-10-12** | 无货拒开零事件;**删原「接线期限 1」闸门**(用户裁定 甲 —— 拒绝权归**库存**,不归接线层;常驻选档 UI 已落地) | S2.2 | EditMode `casebook_prescribe_chain_test.test_noStock_refuses_zeroEvents`(零事件)+ `test_withStock_repeatable_noWiringDeadline`(库存足 ⇒ 可重复,原闸门已删的可证伪面)+ `prescription_page_host_test.test_s24_prescribe_outOfStock_rejects`(现为「重复开方被拒」的唯一判据) |

### S3 词条可读性 polish(sprint-07 残留 ① · 42)

| 项 | 落点 | 判据(承 sprint-07 登记) |
|---|---|---|
| ~~S3.1 字号 + 底衬~~ **✅ 2026-10-13** | **题签 + 落字**(用户裁定 甲「都升」)新增专用档 `--skeuo-font-size-lexeme: 24px`(**走全局变量,不硬编码**)+ 值区底衬 `--skeuo-reading-backing` + **阴性墨 `--skeuo-ink-negative`**(修既有 3.87:1 违约)。⚠️ **判据行原写「md 15→16–17px」经实测证伪**:题签实为**零 class ⇒ 14.0px**(非 md)、落字 15px、且 5 档上限 22px 够不到 §11 的 24px 下限 ⇒ 改走专用档兑现承诺 | EditMode `skeuo_readability_test`(**6 测**:对比 ≥7:1 ×2 态 + 公式锚定 + 字号 ≥24px + 三类绑档 + 底衬走 var + 明度轴 Δ);**四变异各恰红 1**(阴性墨未达标 / 字号<24 / 落字解绑 / 删底衬);`sign_channel_binder_test` 阴性墨断言**升格**(ink-faded→ink-negative)。⚠️ **截图腿 = 人工走查**(batch 出不了图,2026-10-13 实测);**几何腿**(24px 撑行高)亦人工 —— 见 `casebook-39.md` §13 `[V]` 行 |

### 端到端 PlayMode 门(补「七环节仅人工 ADVISORY」缺口 · qa-lead P1-3)

| 项 | 判据 |
|---|---|
| E2E-1 | `test_e2e_examine_to_judgment_viaVirtualKeys`:E 查体 → 词条 → Submit 指认 → JudgmentRecorded |
| E2E-2 | `test_e2e_judgment_to_prescribe_viaVirtualKeys`:落笔 → 翻页 → 选档确认 → DrugTreatmentApplied → 体征变 |
| ~~E2E-3~~ **✅ 2026-10-13** | `e2e_full_loop_test.test_e2e_fullLoop_sevenStages`:**七环节全链,收口 BLOCKING 门**。一测到底(用户裁定)+ 每环保留断言:① 走近 `OnPositionSample` 跨格 + `OnTickEdge` ⇒ `ActorCellEntered` 世界流(⚠️ `Teleport` 不打 sample ⇒ 须显式喂样本);② 查体 `ExamineDriver.OnExamine`(ledger 快照)+ `SeenDisease.OnExamined`(SkillGrown);③ 对照 `RevealedRows`=19 条(探针实测)+ 值区 `.channel-reading` 格渲染词条文字(内嵌 Label);④ 落笔 病名栏 Submit → 摊开 → 条目 Submit ⇒ `JudgmentRecorded`(显式 lexiconId + 疑似);⑤ 开方 后案 Submit → 档位行 Submit ⇒ `DrugTreatmentApplied`(恰一条);⑥ 施治 **虚拟 F 注入**(用户裁定;三件套照 `interaction_input_chain_test`)⇒ `EmergencyAttempt` + `EmergencyTreatmentApplied` 双事件;⑦ 纸面反馈 墨龄 > 0 + 翻回脉案值区仍在。末附三流 tick 单调不减全序断言。**变异**:删 `BindCommandSink` ⇒ ④环恰红(0 判断事件),还原后复跑绿。⚠️ **病程前置** `AdvancePastIncubation()` 2500 tick —— 探针实测病种 1 `Incubation` ≈ 2000 tick,@20Hz ≈100 s,`position=0` ⇒ 双条件门 `aboveFloor` 恒 false ⇒ 查体 0 词(既有行为非缺陷,测试须推进越过) |

### 结构纪律(承 sprint-07)

| 项 | 落点 | 判据 |
|---|---|---|
| C1 头部状态必刷 | active.md 头部 + STATUS 块 + sprint 行状态 | 收口 commit 含 active.md 头部变更(git 可验) |
| ~~C3 评审门~~ **✅ 2026-10-13** | 本 sprint ≥1 轮双代理评审 | 评审原件落 `production/qa/evidence/`。**合判 = C3 通过,零 BLOCKING**:**qa-lead APPROVED**(5 动作全清零:A-1 ⑦环头注过claim 订正 / A-2 三发变异实测 ④⑤⑦ 各恰红 1 / A-3 M2 ③+AC-39-10 复评勾选 / A-4 三处分工登记)+ **lead-programmer CHANGES REQUIRED(3 条 P2/P3,自陈不阻断结案,ADR 符合性 8/8)**:A2 已做(收集方 anchor 亲属限定;生产侧经评估**不加 name** —— 会破守护测 `test_bookContainer_isAnonymous`,假阳风险由收集方收窄,同批两处 + 全量复绿);A1(三处 Submit/三件套重复抽 helper)与 A3(void TearDown 对齐)**登记入 tech-debt / polish 轮**(不阻断收口,判据已写死) |

---

## §3 分阶段验收

- **S0**:两处文档裁定落盘(ADR-011 Amendment + K 计数回写)
- **S1.0**(前置):病名册词表烘焙管线贯通(源 → 校验 → cooked → 读方),11 测绿
- **S1**:玩家按 E 对病名栏 Submit → 病名册翻开 → 指认 → 落笔三态可见(墨龄 > 0 + JudgmentRecorded 入流)
- **S2** ✅ 2026-10-12:翻页至方笺(页缘 ×2)→ 戥子选档(档位行 = `DoseDetentCount`)→ 确认
  → `DrugTreatmentApplied` 入病史流;翻页 / 折页**零流事件**(`AC-39-10` 可证伪)
- **S3** ✅ 2026-10-13:题签 / 落字升 `--skeuo-font-size-lexeme`(24px,兑现 `:85` 下限)+
  值区底衬 + 阴性墨(修既有 3.87:1 违约);`skeuo_readability_test` 6 测绿 + 四变异恰红。
  ⚠️ **截图 / 几何腿 = 人工走查**(batch 出不了图,实测);自动化腿已跑满,人工腿禁借绿
- **收口判据**:E2E-3 七环节全链 BLOCKING 绿 ✅ 2026-10-13(PlayMode 161/161 + EditMode 3308 = 3261/0/46/1 零回归)· C3 合判通过 ✅ 2026-10-13(零 BLOCKING)· **第六轮人工 playtest = CHANGES REQUIRED**(🔴 × 5:输入路径全断 C2/C3/C4 + 可读性 C1/C5;✅ × 1:几何腿等重成立)⇒ 收口判据**未达**,立在 sprint-09/hotfix(见 `production/playtests/playtest-2026-10-13-m2-round6.md` §6 B-6-1…B-6-4)· C1 全绿
- **M2 Exit #8 复评可勾清单(qa-lead)**:
  - [x] ③ EmergencyCandidateList 生产实例化 ✅ **2026-10-13 C3 复评勾选**(qa-lead A-3):`new EmergencyCandidateRuntime()` = `CompositionRoot.cs:246` + `BootRoot.cs:229` `MountTo(UiRoot)` —— sprint-05/07 时「零生产实例化(仅测试)」病因已消除(复评核验路径见 evidence 报告 §四)
  - [x] ② 墨龄上游落笔 tick ✅ **2026-10-13 E2E-3 ⑦ 环**:`InkAgeTicks > 0` 落笔后从 0 起算(ac-42-G6;链内真组合根判定,非合成 fixture;**变异实测**:删 tick 推进 ⇒ ⑦环恰红)
  - [x] ④ RevealedRows 非空 + 病名册条目集 ⊆ 已见过病集 ✅ **2026-10-13 E2E-3 ③④ 环**:查体后 `RevealedRows` = 19 条(探针实测)+ 条目 id = `SeenLexiconIds[0]` 词表反查显式 id(已见集真源 = SkillGrown,ADR-026 ⑥;反幻想二由 `test_lexiconBook_unseenDisease_notPointable` 另立)
  - [x] 翻页/折页零事件(AC-39-10:39 的 `IEventSink.Append` 调用数 = 0)✅ **2026-10-13 C3 正式复评通过**(qa-lead A-3):判据载体 `casebook_page_turn_test.test_pageTurnAndFold_zeroStreamEvents`(真组合根命令路由,Stream.Count 前后相等,S2.1 已绿);**生产侧 no-op 空体**(`CasebookCommandRouter.SubmitPageTurn`/`SubmitFold`,`:196`/`:201`)⇒ 结构性不可能写流(非「有写路径被测试绕开」);E2E-3 链内 ⑤→⑦ 间事件增量恰 2(开方 + 施治),翻页本身不增

## §4 风险与依赖

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| 病名册 DTO `lexicon_id` 回溯既有 lexicon 消费方 | 中 | 中(规模上限触 8–9 人日) | S1.1 契约先行钉死;回溯面先勘察再动 |
| Submit 虚拟按键注入不现实(与 E/F 不同路径) | 低 | 中(E2E-3 判据) | 判据与输入路径解耦:语义层直调 Host,输入路径另立 wiring 测(qa-lead) |
| 三态状态归宿主 + `ApplyData` 无条件重建回执 | 低 | 低 | **TD 已裁:天然闭合** —— `SkeuoRuntimeDriver.OnTickEdge` 每 tick 重读 DTO,≤50ms 重建读数格,零新通道 |
| 折边 Submit 焦点位与 OQ-CB-5 矛盾 | — | — | **TD 已裁:erratum 非分岔**(S0.2 K 计数回写即闭) |

## §5 不做

- **数值轮**(字号取值最终档 / 词表数值 —— 承「数值用户自己调」;S3 只做结构 + 可读性)
- **S4 = 9 侧 F2 病种级投影**(承 sprint-07 §5,继续挂起)
- **联机 / VR / 教学流程**(P1a+)
- **F 键可见反馈**(= P2 story-008,不进本 sprint)

---

## §6 收口

(待收口时填:S1–S3 + C1/C3 状态 · E2E-3 判据 · 第六轮 playtest · 全量门数字)
