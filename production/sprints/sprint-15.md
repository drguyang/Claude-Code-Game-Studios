# Sprint 15 — 2026-10-19 起 · 不设结束日期

**周期**:不设结束日期(承 sprint-12/13/14 口径)—— **做完即收,收口时补记结束日**
**规划依据**:sprint-14 收口记录 · `production/epics/audio-system/EPIC.md` §DoD 切片修订(用户裁定「甲」)·
`docs/tech-debt-register.md` §「44 音频 epic 收口缺口 · TD-audio-100…109」+ §「story-018 补评批 · TD-audio-028…033」
**前置勘察(Phase 1 硬前置 · 2026-10-18 实测)**:已重扫 `audio-system` 全部 19 条 story 的 `Status:`
与 EPIC §Epic Status,**并核出两处陈旧前提**(见下「🔁 前提订正」)—— 旧快照直接写入会让估计与验收判据从起点就错。

> **🔁 前提订正(2026-10-18 · 逐条实测,本 sprint 规划的直接依据)**
>
> | 项 | 文件旧说法 | 实测(今天) | 对规划的影响 |
> |---|---|---|---|
> | **TD-audio-030** | 「12 个 `vo_` 变体**未录制**」(`sprint-14.md` / 债册触发条件表) | ❌ **过时** —— `unity/Assets/Audio/` 下 12 个 `vo_patient_cough_damp_{m,f}{1,2,3}_{1,2}.wav` **全部在场**(m1_1 = 80 402 B),事件表 `Voice_Cough_Damp.assets[]` 已列 12 条 | 030 的阻塞**不是素材,是选择器** ⇒ 本 sprint 可做(S15-2) |
> | **TD-audio-101/102** | 「素材未接入 ⇒ `[L]` 不可判」 | ❌ **过时** —— 呼吸/啰音/音乐/环境共 18 个文件已于 10-17 落地并过 story-015 四门 | 听测**今天可排**(S15-6) |
> | **TD-audio-103** | 阻塞源写「变体素材未落地」 | ⚠️ **须改口** —— 素材已齐,真阻塞**只剩被试 ≥2 人**(项目外部) | 不解除阻塞,但债册欠一次刷账(S15-7) |
> | **TD-audio-100** | 「story-019 已立卡,`Ready`,未开工」 | ✅ 属实(`story-019:4`) | S15-1 直接开工 |
> | **story-020/021** | **不存在** | 本 sprint 按用户裁定「建议 = 先立卡」新建 | S15-2/3 的可开工前提 |

## Sprint Goal

把 epic 44 的 **P0 收口面**从「10 条余项」压到「只剩听测排期 + 用户裁决」:
story-019 落地 `AC-44-10`、让 `AC-44-02` 的变体混合**真正在运行期生效**、
并清掉 `028/029/032/033` 四条「装配诚实但链路未闭环」的结构性缺口。

> **本 sprint 不做**:
> ① `TD-audio-103/104/105/107/108`(P1b / 外部触发,已由 DoD 切片搬出 P0 分母);
> ② 45 联机层 / ADR-023 spike / 七屏走查(非 44 面);
> ③ 任何 `[L]` 的 `[A]` 代偿 —— 听测类判据**只能由用户实听兑现**。

## Capacity

- **不用 `estimate_days`**(依 `production/CAPACITY-MODEL.md`)· 复杂度 = S/M/L
- **件/日基线**:sprint-13 = 7 done · sprint-14 = 11 done ⇒ 本 sprint 按 **8–11 件**监督
- **并行纪律**:同时在跑 ≤ 3 件;⚠️ **Unity batch 独占工程** ⇒ 全量与证伪**一律串行**(承 2026-10-17 过程事故)
- **本 sprint 预算**:Must 3 件(**2×L + 1×M**)· Should 3 件(3×M)· Nice 2 件(2×S)= **8 件**
  —— ⚠️ **含 2 个 L 件,是本 sprint 最大风险**(见 Risks)

## Tasks

### Must Have (Critical Path)

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S15-1** | **story-019 实现**:`AC-44-10` 三面(10-a 构建期门 / 10-b 运行期断言 / 10-c 用户听测) | story-019 | Claude | **L** | 无 | ① 10-a:注入假「转危叮」cue ⇒ 构建期红且**点名该 cue** ② 10-b:平稳→急促→停顿轨迹 **一次性 cue 计数 = 0**,计数须**从被观测链路派生** ③ 停顿(尾音衰减分支)/ 停止(`EndLoop` 干净终止)在断言层**可分** ④ 尾音参数落 `AudioTuning`,衰减序列**单调无拐点**、零新 clip ⑤ **10-c 用户实听四值记录**(BLOCKING,未跑不得 Complete)⑥ 证伪轮逐条转红 |
| **S15-2** | **TD-audio-030 变体混合接线** | **story-020**(新立) | Claude | **L** | 无(素材已齐) | ① `AudioClipLibrary` 多素材行由「恒取 `Assets[0]`」改为经 `VoiceVariantLib.SelectVariant` ② 选择确定性(禁墙钟 / 禁 `UnityEngine.Random`)③ 12 个变体**逐个可被选中**(真种子 + 真库)④ **证伪:回退恒第 0 条 ⇒ 红** ⑤ `TR-diag-022` 补**运行期半边**判据(禁据 `covered` 推定已落) |
| **S15-3** | **TD-audio-033 `knowsClinic` 注入缝** | **story-021**(新立) | Claude | **M** | 无 | ① **生产树**不再有 `knowsClinic: false` 字面量,生产装配从真值源喂入(⚠️ **2026-10-18 刷账**:原写「全仓」不成立,测试树 `behavior_map_test.cs:61` 有一处夹具必需;源码门口径本就是生产树)② `knowsClinic = true` ⇒ 病人**可进 `Seeking`**(真 `BehaviorMap`)③ 反向对照:恒 `true` ⇒ 红 ④ **证伪:改回 `false` ⇒ 红** |

### Should Have

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S15-4** | **TD-audio-028 空间化链接入生产** | 债行(`TD-audio-028`) | Claude | **M** | 无 | ① `AudioSourceRenderTarget` 写 `transform.position`(经 `LatticeToWorld`)+ pin spatial 三件套 ② `SpatializationDirector` 有生产构造 或 其语义内联且可测 ③ `AudioAssemblySeams.LatticeSize` 不再零读取 ④ **补 S3-a 负例**(自建第二池 ⇒ 红)—— 该负例是 `TD-audio-028` 行内逐字要求的 |
| **S15-5** | **TD-audio-029 + 032 合并卫生批** | 债行(各子项单列) | Claude | **M** | 无 | ① `PlayOneShot` 有 `Release` 对偶 ⇒ `_free` 回流的夹具 ② `(int)tick` → long(附 `int.MaxValue+1` 夹具)③ `RowCount > 0` 守卫 ④ `PresentPatientIds` buffer 复用 ⑤ ⚠️ **拆方法须同补 ≤40 行界的夹具**(承 Phase 8b J 项教训)⑥ 逐项证伪 |<br>**✅ 2026-10-18 落(② ③ ④ ⑤ 结 · ① 仍 open)**:①(S15-5 的「Release 对偶」)= `EnqueueOneShot` / `MarkFinished` / `ReclaimExpired` + `OneShotReclaimPump`(每 tick 边沿观测 `isPlaying`)⇒ 夹具证「`_free` 回流」;S15-5 卡面的 ②③④⑤ 对应 TD-audio-032 的对应子项(② long tick · ③ ≤40 行夹具 · ④ snapshot 复用 + 清扫面 · ⑤ `RowCount>0`)。夹具 `S15_5HygieneTest` **13/13**;证伪 F1–F10(其中 F1–F10 全红);全量 `3794/3748/1/45` = 基线 +13。残留:`isPlaying` 时序 NOT-RUN(PlayMode)· `BuildAudioSeams` 仍 >40 行。原件 `production/qa/evidence/impl-s15-5-audio-hygiene-2026-10-18.md`。
| **S15-6** | ~~**TD-audio-101 + 102 同批听测**([L])~~ **⛔ 2026-10-20 移出本 sprint ⇒ 转 `TD-listen-01`(见债册)** | 证据协议件 | 用户 + Claude 备协议 | ~~**M**~~ | ~~S15-2~~ **前置未满足** | ⛔ **移出理由(2026-10-20 用户裁定「把这些前置不满足的移出 sprint15,登记待办」)**:三份听测协议的执行前置**代码级不满足** —— ① **无病人离场路径**(`IPresenceWriter.RemovePresent` 生产零调用方,全库仅 `PresenceRegistry.cs:78` 定义)⇒ 10-c 判据 4/5、AC-44-02 的「停顿 vs 停止」不可达;② **`queryLevel` 硬编码 `CompositionRoot.cs:258` 的局部 `int = 1`,全库零回写路径** ⇒ `TierIndex(1) = 0` 恒最低档 ⇒ AC-44-02「三档配对」无对象;③ **病程被占位曲线压缩到 1.55 s**(`fallback` 病种 plateau 型:潜伏 20 tick → 跨 SeekMin 1.15 s → 跨 DeathBandMin 0.90 于 1.55 s ⇒ 「急促档」仅 0.4 s),生理窗口存在但仓促;④ **同屏 3 病人需 ~40 s**(`PatientAppearedDriver.SpawnIntervalTicks = 400`)而每人 1.55 s 即死亡 ⇒ 语声变体「跨病人可辨」实际上凑不齐活人。详见 `docs/tech-debt-register.md` **`TD-listen-01`**。**原判据保留在下方 `S15-6(原卡面,已移出)` 供解除后复用。** |

> **~~S15-6~~ 原卡面(已移出,保留不改写 —— 解除后按此执行)**:① `breath-layers-listen-evidence.md` 38 `[ ]` 与主通道 5 `[ ]` 逐条执行 ② 四值记录(哪条 cue · 哪条总线 · 可闻 · 有无播报音)③ 人耳陈述按 story-018 形态登记,**不得**升格为可复算判据。
> ⚠️ **本节不主张「听测已做/已过」** —— 它登记的是「**任务被移出,原因是前置不满足**」。

### Nice to Have

| ID | Task | Owner | Est | Acceptance Criteria |
|----|------|-------|-----|-------------------|
| **S15-7** | 债册刷账批(103 阻塞源改口 + story-016 reconcile + 本 sprint 各件结案注) | Claude | **S** | 台账与代码**逐条对码**;「账面滞后」同型已出现三例 ⇒ 只改声明不改代码 = 不合格 |<br>**✅ 2026-10-18 落(S15-7 刷账批,零代码改动)**:只读对码 8 项 + 复核通过一批。**103 阻塞源已改口**(素材 12/12 在场,真阻塞 = 被试 ≥2 人)· **story-016 `AC-44-E5` reconcile**:签核格 `[ ] 通过` 与卡面三处「签字 [x]」相抵 ⇒ 按其自订判据「未勾选 = E5 未通过」,**E5 暂回退为「内容齐 / 签核缺」**(不代签、不追认、不改 `Status`)· 030 结案注的 API 名订正为 `SelectVariantCube`· 032 ③ 刷 `BuildAudioSeams` 行号(仍 45 行 >40,该半边仍 open)· 032 ④「零分配」订正为「零**分配**、O(N) 拷贝仍在」· 031 四处行号全刷 + 明写两半未修· 033「全仓」三处收窄为「生产树」· impl-s18「副本已删除」误记订正(未删,已随 S15-5 入库)。详见 `docs/tech-debt-register.md` §「S15-7 债册刷账批」。<br>**✅ 2026-10-18 用户裁定两项已落(E5 选 A · 副本选 A + 顺手做 C)**:① **E5 维持未通过** —— 签核格 `[ ] 通过` 与文件自订判据一致 ⇒ ✗;且实测出人半更弱一层(15 条 `not_reporting` 与 probe 预填建议稿**逐字相同**,`status_words: null` 证不了「人听过」)⇒ 出路 = 走查人真听一遍后**改写 15 条**;② **副本已删 + 根因已治** —— probe 文件名改「内容哈希」、`WriteOnce` 改「内容判等」+ 异内容拒写,夹具 3 条(16/16 绿)、证伪 **G1–G3 全红**;全量 `3797/3751/1/45` = 基线 `3794/3748/1/45` **+3**(= 新增夹具数)。⚠️ 治法判据**首轮即红**(首版只靠命名纪律,`WriteOnce` 仍会覆盖)—— 命名纪律 ≠ 断言兜底。 |
| **S15-8** | `/retrospective sprint-14`(sprint-14 收口时如实登记的未做件) | Claude | **S** | retro-13 行动项 1–4 逐条兑现核查;产出 `production/retrospectives/retro-sprint-14-*.md` |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| —(sprint-14 无未完成件:11/11 done) | — | — |
| epic 44 的 P0 余项(100/101/102/109第二半/025/027/028…033/AC-44-13 mono 载体/story-016 reconcile) | sprint-14 只清偿了 `AC-44-09` 听测半边 + 链路接通,**不是 sprint 任务而是 epic 收口面的顺延** | 本 sprint 覆盖其中 6 项,余项见 Risks |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **含 2 个 L 件(S15-1 / S15-2)**,且 S15-1 是三面一卡(门 / 断言 / 听测) | **高** | 中 | Should 三件可顺延;不硬凑 —— 承 CAPACITY-MODEL |
| **S15-1 的 10-c 与 S15-6 都依赖用户本人实听** ⇒ agent 侧做完 ≠ 收口 | **确定** | **高** | 两条在 DoD 中单列;不得以 `[A]` 代偿;听测窗口由用户自排 |
| **S15-2 改 `AudioClipLibrary`**(S14-6 烘焙产物的读取面)⇒ **回归面比其它件宽** | 中 | 中 | 全量 EditMode / PlayMode 对账,判据用等式 `total − 前次 = 新增数`;证伪轮与全量**串行** |
| **账面滞后**:债行写了但代码后改 / 代码改了债行没刷(本册已出现三例) | 中 | 中 | 每件结案时**同一轮**刷债行(S15-7 兜底);证伪轮记录落 `impl-s*.md` 原件 |
| **假绿夹具**:断言打自造替身而非生产类 | 中 | **高** | 本 sprint 起「已证」须附**变异测试记录**(停用生产码守卫 ⇒ 断言转红);禁「测试名 + 行号」 |

## Dependencies on External Factors

- **用户实听**(S15-1 的 10-c · S15-6 的 101/102)—— agent 不可代劳,四值记录由用户陈述。
- **TD-audio-103 被试 ≥2 人**(P1b / 外部)—— 本 sprint 界面外。
- **P1b / 外部复列项**(104 VR · 105 无障碍走查 · 107 45 层 · 108 目标硬件)—— 已搬出 P0 分母,触发时并回。
- CI 真 runner 环境(`UNITY_LICENSE` 未配)—— IL2CPP 三格**继续登记 NOT-RUN**(禁借绿)。

## Definition of Done for this Sprint

- [ ] All Must Have tasks completed(S15-1 + S15-2 + S15-3)
- [ ] 各任务按衍生验收标准通过,**每条附可证伪判据**(变异测试:停用生产码守卫 ⇒ 断言转红)
- [ ] **听测类 `[L]` 由用户执行并四值记录**;不得以 `[A]` 顶替,不得借绿
- [ ] QA plan 存在(`production/qa/qa-plan-sprint-15-*.md`,开工前跑 `/qa-plan sprint`)
- [ ] All Logic/Integration stories have passing unit/integration tests
- [ ] Smoke check passed(`/smoke-check sprint`)—— 若无自动冒烟件,按 sprint-13/14 同款口径以用户人耳陈述补记并**如实登记为人工证据**
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS(`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations(就地加订正注,不删原文)
- [ ] **债册逐行刷新**(防账面滞后);本 sprint 各件的 `TD-audio-*` 结案注落盘
- [ ] Code reviewed(`/code-review`)—— **merge 归用户自行执行**
- [ ] **`end` 日期在收口时补记**;`start` 若与实际执行日不符,**矛盾留档不改写**

> **Scope check**:S15-1 是 epic 44 已有卡(story-019);S15-2/3 是 epic 44 的 P0 余项新立卡
> (story-020/021,**先立卡再开工**,经用户裁定)—— 三者**均在 44 epic 界内**,
> 不新增系统范围,非 scope creep。

> ⚠️ **No QA Plan(规划时点)**:本 sprint 规划时 `production/qa/qa-plan-sprint-15-*.md` **不存在**。
> 规划后已按用户选择**立即跑 `/qa-plan sprint`**;若该步未完成,须在最后一件事之前补跑 ——
> Production → Polish 门要求 QA 签核报告,而签核报告以 QA plan 为前提。
