# 评审原件 — sprint-08 E2E-3 收口判据 qa 视角半边(C3 双代理评审 · 2026-10-13)

> **义务出处**:`.claude/docs/review-workflow.md` 原件落盘义务(BLOCKING 级)·
> sprint-08 §3 结构纪律 **C3**(本 sprint ≥1 轮双代理评审)。
> **流程**:双代理并行(代码面 = lead-programmer · 测试面 = qa-lead)恰一轮
> → 主会话独立复核 → 本原件即 **qa 视角半边**。
> ⚠️ **评审评的是特定时点**:被评审 HEAD = `03089d6`(E2E-3 收口提交),
> 工作树干净;全量门数字由主会话跑出:PlayMode **161/161** ·
> EditMode **3308 = 3261 / 0 / 46skip / 1inc**(基线 3257,+11)。事后重做 = 新判定,
> 不追认本判定。未运行任何测试(门数字引自主会话;本评审为只读静态核验 + 生产码交叉 grep)。

## 一、受审对象

| 项 | 内容 |
|---|---|
| 主判据 | `e2e_full_loop_test.test_e2e_fullLoop_sevenStages`(`unity/Assets/Tests/PlayMode/SkeuomorphicUI/e2e_full_loop_test.cs:361-482`) |
| 判据出处 | sprint-08 §2 E2E-3 行(`sprint-08.md:85`):七环节全链,收口 BLOCKING 门 |
| 兼审 | S1.1(`casebook_point_identify_chain_test.cs` 三测)· S2.1(`casebook_page_turn_test.cs` 八 EditMode + 四 wiring)· S3(`skeuo_readability_test`,未读源,按 sprint 文档登记核) |
| 依赖生产件 | `CasebookScreen`(`BuildUI` / `ApplyData` / `RefreshLexiconBook` / `RefreshDentchDoses` / 三 Submit 语义方法)· `CasebookCommandRouter`(`SubmitDiseaseName` / `SubmitConfidence` / `SubmitDose` 三写动作 + 两 no-op)· `CasebookVitalsSource.InkAgeTicks` · `EventStream`(单流路由 + 单调断言) |

## 二、判据完备性(逐环)

**结论:七环节每环均绑住环节语义,非恒真、非空转。** 逐环核验如下(断言行号均指
`e2e_full_loop_test.cs`):

| 环 | 断言(实测存在) | 绑住的环节语义 | 判定 |
|---|---|---|---|
| ① 走近 | `:380` `StreamHas(ActorCellEntered)` + `:377` `OnPositionSample(2,1,0)` 跨格 + `:379` `OnTickEdge` | ADR-020 §四:玩家位移唯一 sim 投影 = 跨格世界流事件;头注 `:375` 显式记录「Teleport 不打 sample ⇒ 须显式喂样本」 | **真** |
| ② 查体 | `:391` `ExaminationLedger.HasExamined` + `:393` `StreamHas(SkillGrown)` | 查体真链(constructor projector 次序照抄 `TryExamine`)+ 已见集真源(ADR-026 ⑥) | **真** |
| ③ 对照 | `:399` `RevealedRows.Count > 0` + `:404` `reading.Count > 0` + `:406` `reading.Any(HasWordText)` | **sprint-07 B3/F1 头号缺口的闭合点**:值区非空 + 词条文字(非空行 / badge),AC-024-2 | **真(本链最重要的一条)** |
| ④ 落笔 | `:414` 前置零判断 + `:424` 恰一条 + `:425` kind=Recorded + `:426` lexiconId = `SeenLexiconIds[0]` 反查值 + `:428` confidence=疑似 | 载荷字段逐值断言(显式 lexiconId 防「显示索引」第二真源)+ 三档首态 | **真** |
| ⑤ 开方 | `:433` 前置零 DrugTreatmentApplied + `:436` `IsOnPrescriptionPage` + `:439` `doseRows.Count > 0` + `:440` `Submit(doseRows[0].userData - 1]` + `:442` 恰 +1 | 翻页→方笺页→选档确认→入流,含「恰一条」计数 | **真** |
| ⑥ 施治 | `:448-452` F/gamepadWest 注入 + `EmergencyPressed` 探针 + `:456` EmergencyAttempt + `:458` EmergencyTreatmentApplied | ADR-009 Amendment I 双事件;探针独立自证键位注入生效(非静默跳过) | **真** |
| ⑦ 纸面反馈 | `:463` `InkAgeTicks > 0` + `:468` 翻回后 `reading.Count > 0` + `:469` `reading.Any(HasWordText)` | AC-42-G6 墨龄 + 值区渲染存活到全链末 | **真** |
| 末 | `:473-479` 三流 tick 单调不减 | ADR-005 重放前提;`EventStream.Append` 自带 `_lastAppendedTick` 单调断言(生产侧同判) | **真** |

**问 1 之答(④ 环三态循环缺口)**:④ 环只判 Recorded + lexiconId + 疑似,**全链内未判**
「擦除回空 / 确定」两步。**结论 = 缺口真实存在但不构成 BLOCKING**,理由三条:
① **载体存在且更强** —— `casebook_point_identify_chain_test.test_lexiconBook_submitCycle_threeStates`
(`:190-220`)同一真组合根断言了三态全序列(Recorded→Revised 疑似/lexicon_id=0/Empty 恰三笔
+ DTO 回读),断言强度高于「在全链里多插两次 Submit」;
② **E2E-3 的职责是链可达性**,三态是单环节状态机语义,归 S1.3 单元判据(已绿);
③ 折叠谓词 / 擦除语义已在 `case-system.md:241` 有独立 EditMode 覆盖(本次未复核该文件,
标 **未核实**,建议并入后续取证 — 见动作清单 A-4)。
**S1.1 单测覆盖够用,E2E-3 内不补**,但应在 E2E 头注显式登记这一分工(现状已写在
sprint-08 §3 S1.3 行,测试头注 `:12-13` 未提 → 改进项)。

**问 3 之答(诚实边界·截阙腿 / 人工腿)**:测试头注声明的不覆盖项 —
① `:20-22` 判据与输入路径解耦:语义层直调 + 真派发,E/F **键位注入路径**本身由
`interaction_input_chain_test` ②③ 覆盖(⑥ 只标半个 F 标);
② 既有 `NOT-RUN` 面(SkeuomorphicUI 截图 / 走查 skip 类)与本判据无交集,未借绿。
**sprint 文档侧同步登记**:S3 截图腿 + 几何(24px 撑行高)腿 = **人工走查**(batch 出不了图,
`casebook-39.md §13 [V]` 行),已显式禁借绿。**未发现被隐含勾掉的禁借绿事项**;
⚠️ 唯一需点名者:E2E-3 ⑦ 环头注 `:18` 声称三项断言(墨龄 / 通道行 / **病名栏回显所指病名**),
实现只有两项 —— `grep channel-disease-name` 全生产码**零回显写入点**
(`CasebookScreen.ApplyData` 只写通道值区读数格;`RefreshLexiconBook` 只建条目容器)。
即:**⑦ 环第三条判据(病名栏回显)在生产面不存在,测试未实现,头注过claim**。
这不影响 E2E-3 现有断言的有效性(其余两条真),但它是一处**文档账目失真**
—— 与 sprint-07 M4 同型,须订正为「回显归 39 后续 story(登记 NOT-RUN / 新 story)」。

## 三、变异敏感度

**已实测**:删 `BindCommandSink` ⇒ ④ 环恰红(零判断事件),还原后绿(sprint-08.md:85 登记;
形态同 `casebook_point_identify_chain_test.test_noCommandSink_zeroJudgments_mutationSensitive`)。
**⑤⑥⑦ 环的证伪面**(下表每条均经生产码交叉 grep 定位删除点与预期红点):

| 环 | 若删 X | 哪条断言红 | 判据 |
|---|---|---|---|
| ⑤ 开方 | `:339` / `:351` `SubmitPageNext` 的 `_commandSink?.SubmitDose` 路由(`CasebookCommandRouter.SubmitDose` 整段) | `:442` 恰 +1 红(零事件 ⇒ fail-loud 抛 — 路由 `:174-179` 未接宿主 = `InvalidOperationException`,测试红得更早) | 待补实测 |
| ⑤ 开方 | `RefreshDentchDoses` 的 `row.userData = ordinal`(`:414`)改回显示索引 | `:440` `Submit(doseRows[0].userData - 1]` 取错行 ⇒ 档序数错 ⇒ `:442` 红 或载荷 dose_potency 错 | 待补实测 |
| ⑥ 施治 | `InteractionInputDriver.OnFrame` 的急救触发分支(不在本文件,但 `interaction_input_chain_test` ②③ 已覆盖变异面) | `:456`/`:458` 双事件红 | 他测已覆盖,sprint §4 已登记解耦 |
| ⑦ 纸面 | `CasebookVitalsSource.InkAgeTicks`(`:217-230`)的 `CaseStreamQuery.LastJudgmentOf` 过滤 | `:463` 红(恒 0) | 待补实测 |
| ⑦ 纸面 | `ApplyData` 的读数格清除 + 重建(`:538-563`) | `:469` `Any(HasWordText)` 红(清后不建 = 空) | 待补实测 |
| ① 走近 | `PlayerController.OnTickEdge` 的跨格写流 | `:380` 红 | `interaction_input_chain_test` 同型链覆盖 |

**纸面推演结论**:⑤⑥⑦ 环**具备结构性证伪面**(每环断言的生产依赖点已经定位,非恒真),
但**仅 ④ 环有已登记实测**,⑤⑦ 环的变异实测缺失 —— 按 M2 阶段 2 教训登记
(「纸面推演对多层门冗余结构性盲,变异实测不可省」,本仓第 3 次实证场地步),
判 **中危登记,不阻断本轮**(E2E-3 收口判决不依赖它),但**动作清单 A-2 要求补两发实测**
(⑤ 删路由、⑦ 删墨龄过滤),否则「变异敏感」仅有单点证据,链级结论偏弱。

## 四、M2 Exit #8 复评勾选核验

sprint-08.md:107-111 现态:② 已勾、④ 已勾、③ 未勾、AC-39-10 行未勾(登记「形式上已复核」)。

| 条目 | 文档勾选 | 证据链核验(本次 grep 生产码) | 判定 |
|---|---|---|---|
| ② 墨龄上游落笔 tick | ✅ 已勾 | `CasebookVitalsSource.InkAgeTicks` = `当前tick − LastJudgmentOf(case)`(装配层算,42 零计时器 — 符合 AC-42-G6 纪律);**sprint-05/07 时「恒 0」的两个病因(37 落笔 tick 未接)已闭合**:④ 环落笔写真 tick ⇒ E2E-3⑦ `InkAgeTicks > 0` 是链内真组合根判定(非合成 fixture) | **勾选成立**(链内判定形态合规) |
| ④ RevealedRows 非空 + 条目集 ⊆ 已见集 | ✅ 已勾 | RevealedRows 非空 = E2E-3 ③ `:399`(已从 HEADER 改查);条目集 ⊆ 已见集 = 反幻想二由 `test_lexiconBook_unseenDisease_notPointable`(`casebook_lexicon_book_test.cs:109-121`,**fixture 级**)另立 + E2E-3 `:410` 前置断 `SeenLexiconIds.Count > 0`(防条目空真空);真源 = SkillGrown ⇒ 已见集(ADR-026 ⑥),链内经 ② 环节真查体建立 | **勾选成立**;**⚠️ 边注**:「已见集非空 ⇒ 必有一条可见」在 fixture 侧成立,但 E2E-3 内 ④ 环条目非空断言(`:420`)实际依赖 ② 环 SkillGrown ⇒ 条目集非空,**真链成立,非空真空** |
| ③ EmergencyCandidateList 生产实例化 | 未勾 | grep `new EmergencyCandidateList` = `EmergencyCandidateRuntime.cs:50`(生产,组合根 `CompositionRoot.cs:246` 实例化 + `BootRoot.cs:225` 就绪后挂载);sprint-05/07 时「零生产实例化」的病因(仅测试)**已消除** | **不勾不成立 — 应勾(见动作清单 A-3)** |
| AC-39-10 翻页 / 折页零事件 | 未勾(登记「形式上已复核,正式归 qa 轮 = 本评审») | 测:`casebook_page_turn_test.test_pageTurnAndFold_zeroStreamEvents`(`:136-155`)对**真组合根命令路由**断 `Stream.Count` 前后相等;生产码核验:`CasebookCommandRouter.SubmitPageTurn`/`SubmitFold` = **no-op 空体**(`:196`/`:201`),结构性不可能写流;E2E-3 链内 ⑤→⑦ 间事件增量 = 开方 + 施治 = 恰 2(与 sprint-08:111 自查一致) | **正式复评通过 — 应勾**。零事件判据的载体强度附加确认:生产侧无 `IEventSink` 引用(纯呈现注释),非「有写路径但被测试绕开」 |

**结论:③ 与 AC-39-10 两条「不勾」/「形式上复核」的现行处置过严(证据链已闭合);
②④ 两条勾选成立。** 建议按动作清单 A-3 在收口轮一并勾掉,不因它们悬空影响 C3 转 Complete。

## 五、判决

### 判定:**APPROVED**(附 5 项登记 + 2 项收口动作;零 BLOCKING)

- 判据完备性:七环节逐环真绑定(§二),链路承载了 sprint-07 B3/F1 值区判据的正式闭合;
- 变异敏感:④ 环实测 + ⑤⑦ 环纸面定位(§三),**A-2 补测为 APPROVED 后的附条件动作,不阻断**;
- 诚实边界:输入解耦 / 人工腿双处登记合规,无禁借绿被隐含勾掉;**唯一账目失真 = ⑦ 环头注
  「病名栏回显」过claim(生产零载体)**→ 订正(A-1),非假绿(测试没为它编断言);
- M2 Exit #8:②④ 勾选成立;③ / AC-39-10 应勾未勾(§四),建议收口轮一并处理。

### 动作清单

| # | 级 | 动作 | 可证伪判据 |
|---|---|---|---|
| A-1 | 中(S4) | 订正 E2E-3 头注 `:18` ⑦ 环第三条(病名栏回显):生产 `CasebookScreen.ApplyData` 零回显写入点,grep `channel-disease-name` 全 Gameplay 只见 Submit 回调与册容器锚点 ⇒ **回显未实现**。改头注为「回显指认病名归 39 后续 story(登记 NOT-RUN)」,或在 sprint-08 立 story 行 | grep `channel-disease-name` 于 `Gameplay.UI/**`(排除 Tests)无 `Label` text 写入点;sprint-08 §2/§5 出现回显登记行 |
| A-2 | 中(建议) | 补两发变异实测:⑤ 删 `CasebookCommandRouter.SubmitDose` 转发 ⇒ `:442` 恰红;⑦ 断 `LastJudgmentOf` 过滤(造多 case 混流)⇒ `:463` 红 | 两份变异 XML 恰红 1 + 还原复跑全绿,证据路径落 `production/qa/evidence/` |
| A-3 | 低(文档) | M2 Exit #8:勾 ③(`grep -rn "new EmergencyCandidateList" 非 Tests` ≥ 1 = `EmergencyCandidateRuntime.cs:50`)+ AC-39-10(正式复评通过,§四);sprint-08.md:108/111 复选框更新 | grep 命中 + 复选框 `[x]` + 复评注记补日期 |
| A-4 | 低(登记) | 「擦除回空」在 case 流的 EditMode 独立覆盖(折叠谓词 / 擦除语义)本次**未核实**;E2E-3 与 S1.3 的分工头注亦未明写 | 补核 `case_open_and_judgment_test.cs` / `case_close_precondition_test.cs` 相关断言并登记;或立 NOT-RUN |
| A-5 | 低(登记) | S3 `skeuo_readability_test` 六测 + 四变异 + 人工腿,本次未读源(仅按 sprint 文档 + 主会话门数字登记) | 后续 qa 轮补读源核验(截图腿禁借绿既有登记已合规) |

### 未及查(如实登记)

未运行任何测试(门数字引主会话);未读 `casebook_page_turn_wiring_test` / `skeuo_readability_test` /
`interaction_input_chain_test` 源(按存在性与登记核);未核 `case_open_and_judgment_test` 的
擦除覆盖(A-4);EditMode 46 skip / 1 inc 未逐条 triage(主会话已核与本批零交集);
PlayMode 设备注入分支(键盘 E/F / gamepadWest)CI 可重复性未观察。

---

**原件元数据**:C3 双代理评审 qa 半边 · 被评审 HEAD = `03089d6` ·
门数字:PlayMode 161/161 · EditMode 3308 = 3261/0/46skip/1inc(基线 3257,+11 零回归)·
本原件即 sprint-08 §3 C3 判据「评审原件落 `production/qa/evidence/`」的**测试面**满足件;
**代码面判定以其报告为准,两半合并后 sprint-08 方可转 Complete**。
