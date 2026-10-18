# Story 019: AC-44-10「无突变音效」映射 + OQ-44-7 呼吸尾音 spike(一卡两面)

> **Epic**: 音频系统
> **Status**: In Progress(10-a / 10-b 已落;10-c 听测 NOT-RUN ⇒ 不得 Complete)
> **Layer**: Foundation(不新增音频语义;兑现既有判据的载体)
> **Type**: Integration
> **Estimate**: 1 人日(三面载体,量小;10-c 除外 —— 挂 story 内待 spike)
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-18

## Context

**GDD**: `design/gdd/audio-system.md` —— AC-44-10 在三处被引用而**零 story 声明**
(`:408-411` Output Range 硬约束 / `:571-573` Edge「急促→停顿」/ `:878` 硬约束负面声明)
**Requirement**: 收口缺口 `TD-audio-100`(`docs/tech-debt-register.md` §「2026-10-17 · 44 音频 epic 收口缺口」)——
**16 条未全验证 AC 中唯一的「无 story 真空洞」**
**开放问题**: `OQ-44-7`(「停顿 vs 停止」可听辨具体怎么做 —— spike:呼吸尾音设计;失败退路 = 「玩家看动画」)
**ADR Governing Implementation**: ADR-018(§六 无提示音铁律 · 快照禁播报)· ADR-028(声源池唯一所有者 · cue 驱动生命周期)· ADR-014 §五(事件表烘焙管线)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 尾音 = 既有 `AudioSource` 的 `volume`/`AudioMixer` 参数增益衰减,**零新 clip、零新 API 面**;
其余走 story-018 已打通的装配缝(`PatientCueDriver` / `AudioSourcePool` / `AudioBusResolver`)。

### 判据事实(GDD 逐字,本 story 的宾语)

| 源 | 逐字要求 |
|---|---|
| `audio-system.md:408-411` | 呼吸层输出 = **连续变化,无突变音效**(AC-44-10);转危 = **玩家自己听出来**,不得用「突变音效」标记阈值跨越(承 `disease-simulation.md:1120-1121` 铁律一 R3.4:越阈不即时结算);「停顿 vs 停止」必须可听辨,但**不许用音效硬报**(`disease-simulation.md:1118`) |
| `:571-573` | 急促→停顿过渡:呼吸循环**连续**变慢/变浅,无突变音效;**停顿(昏迷)= 有极轻微的背景气流尾,停止(死亡)= 完全无声**;「但绝不用『叮』宣告是哪个」(承 9 的 AC-21 / ADR-018 §六) |
| `:878` | 硬约束负面声明:**禁「突变音效标记阈值跨越」(AC-44-10)** |
| `:569`(OQ-44-7 的设计难点) | 淡化 ≠ 提示音:淡化是**连续声床的增益斜坡**,不携带离散事件语义,故不触 AC-44-09;**若被做成可听辨的 onset 音形则违 AC-44-09** ⇒ 尾音形态必须是衰减而非新事件音 |

### 用户裁定(2026-10-18,立项时三项全裁)

1. **范围 = 一卡两面**(10-a/10-b 不拆卡,量小);
2. **尾音形态 = 基础气流层的增益指数衰减**(零新 clip),参数 `breath_tail_ramp_ms` +
   `breath_tail_gain_floor_db` 落 `AudioTuning`,**值域归数值轮(用户自己调)**;
3. **10-c 听测挂 story 内**(不另排期)—— 前置 = spike 出尾音参数后由用户实听,
   承 story-018 的「四值记录 + 签核件」形态;在那之前本卡**不得转 Complete**。

### 边界裁定(立卡轮定)

1. **只交「无突变音效」的载体**:构建期门 + 运行期断言 + 尾音 spike 的**参数化实现**;
   不改 `AudioCueDto` 形状、不改事件表 schema(白名单语义沿用)、不动 09 的既有三条断言。
2. **AC-44-10 与 AC-44-09 是两条判据,不得合并载体**:09 = 「触发源 ∈ 行为反馈白名单」
   (cue **存在性**纪律);10 = 「阈值跨越**不得映射成任何离散 cue**」(过渡**连续性**纪律)。
   一条新断言,不是把旧门换个名字。
3. **尾音不进三流、不进 sim**(ADR-009 派生态;ADR-018 44 只渲染)。
4. **9 侧「越阈不即时结算」不属本卡** —— 那是 `disease-simulation.md` 铁律一的 sim 判据
   (已有载体);本卡只管**音频侧不产生**任何把「越阈/停顿/停止」硬报出来的路径。
5. **失败退路(OQ-44-7 自带)**:若尾音听辨失败 ⇒ 退回「玩家看动画」,须另裁,
   **不得**静默降级为「加一个区分音效」(那正撞 AC-44-10)。

## Tasks

### 面 10-a:构建期门(事件表白名单的 10 半边)

- [ ] **AC-019-1**[A]: `AudioEventTableGates` 新增一条断言(或扩 `AC-018-7` 所在谓词,择一实现,
  判据独立编号):**全表不存在任何「阈值跨越 / 转危 / 停顿 / 停止宣告」语义的离散 cue** ——
  实现形态 = 事件表新增列或既有 `trigger_source` 值域**拒收**「状态阈值类」触发源
  (具体载体 = 实现期勘察定,登记在证据原件;禁测试内联谓词顶替 —— 承假绿复核病根)。
  **证伪判据**:注入一条假「转危叮」cue ⇒ 构建期红,且报文点名该 cue。
- [ ] **AC-019-2**[A]: 呼吸过渡类 cue(急促/浅慢/停顿各态)**零新增 `trigger_source`** ——
  过渡只允许既有 loop 的参数变化路径;断言宾语 = 真生产表 + 真门。

### 面 10-b:运行期断言(真装配缝,承 story-018)

- [ ] **AC-019-3**[A]: EditMode/PlayMode 真装配件 —— 沿 `PatientCueDriver` 缝推进一条
  平稳→急促→停顿的 Progress 轨迹,**新一次性 cue 发射计数 = 0**(只有既有呼吸 loop 的
  参数连续变化);断言的计数须**从被观测链路派生**(承「可观测标志从被观测对象派生」纪律)。
  **证伪判据**:把任一阈值跨越临时映射成一条一次性 cue ⇒ 本条红。
- [ ] **AC-019-4**[A]: 停止(死亡)= loop **干净终止**(`EndLoop`/prune 路径),
  终止时刻**零新增声源、零终止音效**(池活动计数在终止后不升);停顿(昏迷)= loop 存续 +
  增益进入尾音衰减分支。两态在断言层面**必须可分**(这是 10-c 听测「可听辨」的机器半边)。
- [ ] **AC-019-5**[A]: 尾音参数化落地 —— `AudioTuning.breath_tail_ramp_ms` /
  `breath_tail_gain_floor_db`(命名实现期可微调)驱动基础气流层增益的指数衰减;
  **零新 clip**(构建断言:衰减路径不引用任何新 `AudioClip`;衰减 onset 不可辨的机器判据 =
  单调无拐点增益序列)。**数值不冻结**(归数值轮,用户自调)。

### 面 10-c:听测半边([L],挂 story 内,前置 = 10-b spike 出参数)

- [ ] **AC-019-6**[L]: 人工协议(用户实听,承 `audio-wiring-listening-2026-10-18.md` 形态):
  ① 急促→停顿过渡**全程无「叮」类突变音**;② **停顿 vs 停止可听辨**(尾音在 / 完全无声);
  ③ 记录四值(哪条 cue · 哪条总线 · 可闻 · 有无播报音)。
  ⚠️ BLOCKING —— 未跑 ⇒ 本卡不得 Complete;「无给药类可听对象」不豁免本条
  (本条的宾语 = 呼吸过渡,**现表有物可听**,与 AC-11-21 的覆盖面问题不同型)。

### 收口联动(账面)

- [ ] **AC-019-7**: `docs/tech-debt-register.md` **`TD-audio-100` 结案注**(原判定 → 修复落点 → 验证命令);
  `OQ-44-7` 状态刷新(spike 结论:走「增益衰减」路径 or 退「看动画」);
  EPIC 头部「16 条未全验证」计数**本卡不重算**(承 2026-10-18 听测批同口径,归下次 epic 收口核查统一重算)。

## Dependencies

- **story-018**(运行期装配缝)—— ✅ Complete 2026-10-18
- `AudioTuning` / `AudioEventTableGates` / `PatientCueDriver` / `AudioSourcePool`(均已在盘)
- **10-c 前置** = 10-b 的尾音参数可实听(用户实机,Editor Play)

## Out of Scope

- 9 侧铁律一的 sim 结算(`disease-simulation.md`,既有载体);
- AC-44-09 既有三条断言的改动;`AudioCueDto` 形状;事件表 schema 扩列以外的改动;
- VR(AC-44-11)/ 双盲被试(TD-audio-103)/ 联机对拍(107)—— 各自外部触发;
- `TD-audio-027`(AC② 门 Master 盲区)—— 同触 mixer 拓扑但不同判据,归其触发条件。

## Test Evidence

- 门半边:`unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs` 扩测(真门 + 真种子正向半边);
- 运行期半边:`unity/Assets/Tests/{EditMode/Audio,PlayMode}/` 真装配件(打 story-018 的缝);
- 听测半边:`production/qa/evidence/` 新增协议件(执行后回填);
- 证伪轮:逐条「停用什么 ⇒ 哪条红」登记在 `impl-s19-*.md`(证据原件,S12-4 门)。

## Definition of Done(本卡)

- 10-a/10-b 全 [A] 绿 + 证伪转红记录齐;10-c [L] **执行并签核**;
- `TD-audio-100` 结案注落账;`OQ-44-7` 有结论(含失败退路的处置);
- ⚠️ 44 epic **仍不得收口**(101/102/103/104/105/107/108 及素材录制在途)—— 本卡只动 `TD-audio-100` 一条。

## Completion Notes(实现轮 2026-10-18 —— **未完成**)

**Status 口径**:`In Progress`。10-a / 10-b 已落盘且证伪转红齐;`AC-019-6`(听测)为 **NOT-RUN**
(BLOCKING)⇒ 本卡**不得**转 `Complete`(用户裁定 ③)。

| AC | 状态 | 载体 / 说明 |
|---|---|---|
| AC-019-1 | ✅ | `AudioEventTableGates.ValidateNoTransitionOneShotCues`(两臂)+ 6 测;证伪 F1 / F2 / F8 转红 |
| AC-019-2 | ✅ | `test_ac0192_realSeed_breathRows_keepExistingTriggerSource` |
| AC-019-3 | ✅ | `test_ac019_3_transitionToPause_addsNoNewOneShotCue` · `test_ac019_3_oneShotIntensity_followsVitals_neverFixedVolume`;证伪 F5 / F7 |
| AC-019-4 | ✅ | 停顿 = loop 存活 + 尾音;停止 = `EndLoop` 零尾音;证伪 F3 / F4 |
| AC-019-5 | ✅ | `BreathTailRamp`(指数、倍率恒等)+ `AudioTuning.BreathTailRampMs/GainFloorDb`;证伪 F6 / F9 |
| AC-019-6 | ⛔ **NOT-RUN** | `production/qa/evidence/s19-breath-transition-listening-2026-10-18.md`(协议 + 待填表) |
| AC-019-7 | 🟡 部分 | `TD-audio-100` 已刷「代码半边已落,听测未跑 ⇒ 不结案」;`OQ-44-7` 已刷 spike 代码结论 |

**证伪轮**:F1–F9 逐条转红(明细见 `production/qa/evidence/impl-s19-ac44-10-breath-tail-2026-10-18.md` §三);
**两处假绿在证伪轮被抓出并已修**(FG-1 强度判据混入尾音增益 + 前段恒值;FG-2 `coughCue: null` 使「零一次性 cue」恒真)。

**回归**:EditMode 全量 `3748 / 3703 / 0 / 45`(`unity/Logs/s19-full-editmode.xml`)。
⚠️ 与基线 `3732 / 3687 / 0 / 45` 的差 `+16` 中,**夹具逐名计数为 +15,差 1 挂【待核】**(基线全量 XML 未落件,逐名 diff 不可执行)。

**听测执行须用户做**:按 `s19-breath-transition-listening-2026-10-18.md` §三 六项勾选 + 四值记录;
**判据 3(尾音是否「一阵一阵」)须专门判** —— 本实现的停顿期尾音按 `BreathTailRampMs` 分段重复。

### ⚠️ 同批听测的串扰源:`docs/audio/statereport-walkthrough-2026-10-18.md` 是**预填稿回放**,不是听测结果

**2026-10-18 实测登记**(编排方查证,`statereport_walkthrough_probe.cs:749`):

跑全量 EditMode 时,`StateReportWalkthroughProbe.walkthrough_allCues_manifestPlaybackAndOrdering`
会**自动写盘** `docs/audio/statereport-walkthrough-{今日}.md` + `-manifest-{今日}.json`。
该记录**看起来像已完成的手跑走查**,实际不是:

| 事实 | 证据 |
|---|---|
| 记录正文的标题日期**取自谓词文件**,不是运行日 | `probe.cs:749` `md.Append("…手跑走查记录(").Append(date).Append(")")`;`date` 读自 `docs/audio/statereport-walkthrough-predicates.json` 的 `"date": "2026-10-17"` |
| 文件名日期才是运行日 | `probe.cs:605` `TodayStamp() => DateTime.Now.ToString("yyyy-MM-dd")` ⇒ 今日生成 `-2026-10-18.md`,**标题却写 2026-10-17** |
| 15 条 verdicts **全部是预填建议稿** | 谓词文件 `_instructions` ④ 逐字:「`not_reporting` 是我按『替代读法』预填的**建议稿**,`status_words` 一律预填 `null` —— 这是**默认判定**,不是你听过的结论……**留着不代表你听过**」 |
| 探针**不校验**「是否真听过」 | 它只校验格式合法 + 无状态词,预填稿必然通过 |

**⇒ 后果与纪律**:
1. **不得**把 `docs/audio/statereport-walkthrough-2026-10-18.md` 读成「E5 走查已过」——
   它是机器回放预填稿的产物,人耳判定为零。
   ⚠️ **但也不要反过来说 E5 没过**:E5 已在 **2026-10-17** 由 `dr_guyang` 逐条听过 15 条并签字
   (`docs/audio/statereport-walkthrough-2026-10-17.md`,story-016 `AC-44-E5` ✅)。
   **10-18 这份是重跑产生的冗余副本**,不是第二次走查,也不推翻 10-17 的结论。
2. **本次提交时该文件与 `test-results.xml` 一并留在工作区不提交**(用户 2026-10-18 裁定),
   等人工听测做完再随听测结果一起入库。
3. **做 `AC-019-6` 听测前须先改** `predicates.json` 的 `"date"` 为**实际听的那天**
   (文件 `_instructions` ① 逐字要求);否则生成记录的标题会一直顶着旧日期。
4. **两条听测是两件事,别混**:`AC-019-6` 判的是**呼吸尾音**(本卡,判据 3 = 尾音「一阵一阵」);
   `AC-44-E5` 判的是**无提示音铁律**(story-016,15 条 cue 是否在报状态)。同一次实机听测可一并做,
   但**判定表各填各的**。
