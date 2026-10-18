# story-019 · AC-44-10 无突变音效 / OQ-44-7 呼吸尾音 —— 实现证据原件

> **对象**:`production/epics/audio-system/story-019-ac44-10-no-spike-sfx.md`
> **AC**:`AC-019-1` … `AC-019-7`(7 条)
> **日期**:2026-10-18
> **来源判定**:`docs/tech-debt-register.md` **`TD-audio-100`**(AC-44-10 完全未映射 —— 16 条未全验证
> AC 中唯一「无 story 真空洞」)· `OQ-44-7`(「停顿 vs 停止」可听辨怎么做)。
> **本件性质**:实现轮证据(**交付面 → AC 载体 → 证伪轮 → 回归对账 → 如实登记缺口**)。
> **⚠️ 状态口径**:story-019 **本轮不转 `Complete`** —— `AC-019-6`(人工听测,**BLOCKING**,
> 用户裁定「10-c 挂 story 内」)不可自动判定,本件如实登记 **NOT-RUN**,**不借绿**。

---

## §一 交付面

| # | 交付 | 文件 | 关法 |
|---|---|---|---|
| **10-a** | 构建期门谓词 `ValidateNoTransitionOneShotCues` + 过渡语义族名 `TransitionSemanticTokens` | `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs` | 接入总门 `Validate`(两条臂:① 非循环行名字带过渡语义;② 非循环行 `trigger_source` ∈ 禁止集 `{VitalsCrossed, ThresholdCrossed, OutcomeResolved}`);`rows == null` ⇒ NOT-RUN 守卫 |
| **10-b** | 停顿/停止的音频分界 + 尾音指数衰减 | `unity/Assets/Gameplay.Boot/PatientCueDriver.cs` | `BreathAction.End` 不再 `EndLoop`:loop 存续 + 开尾音段(`_tailStartTick`);`AdvanceBreathTail` 每拍写一次增益、到期停表(整数 tick 比较,r=30);`PruneAbsent` / `StopAll` 才 `EndLoop`(完全无声) |
| **10-b** | 指数衰减纯函数 | `unity/Assets/Gameplay.Presentation/Audio/BreathTailRamp.cs`(新) | `ValueAt = v0·(floor/v0)^(t/T)`(等 dt 间隔倍率恒等 = 无拐点判据)· `DbToLinear` |
| **10-b** | 参数落 `AudioTuning` | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | `BreathTailRampMs = 1500.0` · `BreathTailGainFloorDb = -36.0` —— **值域归数值轮(用户自调)** |
| **10-b** | 入口白名单增列 | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` | `EntryAllowlist += BreathTailRamp.State`(与 `VolumeRamp.State` 同族:调用方持有的 ramp 状态,非状态入口) |
| 测试 | 门半边 | `unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs` | +6 测(34 → 69 时全套件) |
| 测试 | 运行期半边 | `unity/Assets/Tests/EditMode/Audio/runtime_assembly_test.cs` | 27 → 36 测(+9) |

**为什么 `BreathTailRamp.State` 必须入白名单(而不是改门)**:
`ScanEntryPoints` 的语断言 =「44 公开方法签名的项目类型 ⊆ 白名单」。`State` 只承载 5 个线性 volume
标量,**无游戏状态字段**、不进三流、不进 sim —— 与 `VolumeRamp.State` / `FilterRamp.State` 完全同族,
属**同一白名单条目的既有语义**,不是新入口。增列带日期注,承既有增列先例(Story 006 / 008)。

---

## §二 AC ↔ 测试载体映射

| AC | 载体(测试名) | 判据 |
|---|---|---|
| **AC-019-1** | `EventTableGateTest.test_ac0191_fakeDeathDingOneShot_reportsRed_andNamesCue` | 注入 `Voice_转危叮_Death`(非循环)⇒ 恰 1 条错,报文**点名该 cue** + `AC-44-10` |
| | `test_ac0191_oneShotWithThresholdTrigger_reportsRed` | 逐值注入 3 个禁止触发源 ⇒ 每值恰 1 条,**点名行** |
| | `test_ac0191_transitionNameOnLoopRow_accepted` | 过渡名落在**循环**行 ⇒ 放行(门拒「做成一次性」,不拒名字) |
| | `test_ac0191_transitionNameWithLoopFieldAbsent_reportsRed` | 行**缺 `loop` 字段**(缺省 = 一次性),只改 cue 名 ⇒ **总门**恰 1 条红且点名(story 逐字场景) |
| | `test_ac0191_rowsMissing_reportsNotRunGuard` | rows 缺失 ⇒ NOT-RUN,不静默放行 |
| | `test_ac0191_realSeed_zeroTransitionErrors` | 真种子 `assets/data/audio_events.json` ⇒ 零错 |
| **AC-019-2** | `test_ac0192_realSeed_breathRows_keepExistingTriggerSource` | 三行呼吸 cue(`BreathLayer_Base_Calm` / `_Adventitious_Fine` / `WorldBreath_Baseline`)全部 `loop = true` 且 `trigger_source` **全部** = 既有值 `ContinuousPhysiology`;并断言该值 ∈ 既有合法集(不是本条新增) |
| **AC-019-3** | `RuntimeAssemblyTest.test_ac019_3_transitionToPause_addsNoNewOneShotCue` | 真驱动 + 真 13 调度:平稳 200 拍 → 急促 40 拍 → 终局 40 拍;**前置** = 平稳期确有既有一次性 cue(`EmitCount > 0`);终局后 `EmitCount` **零新增**;`BeginLoopCount == 1`(过渡不重起 loop) |
| | `test_ac019_3_oneShotIntensity_followsVitals_neverFixedVolume` | 只取**一次性** cue(`Looped == false`)的强度,逐条 ∈ 任意拍体征派生集合(`PatientCue.Intensity(p)`) |
| **AC-019-4** | `test_ac019_4_pauseKeepsBreathAndStopEndsIt` | 停顿:loop 存活、`EndLoop == 0`、尾音簿记在、增益在涨、过渡后零新增一次性 cue、停顿段末强度已离开满幅且在尾音底之上;**停止**(`PruneAbsent`):`EndLoop == 1`、簿记清空、**不再有任何增益写入** |
| | `test_ac019_4_pauseLifespan_isBoundedByTailRamp` | 逐拍采样:到期前一拍推进 ≥ 0.9×时长;到期拍簿记清;loop 仍存续;停表后**下一拍重开新段**且 `elapsed = 0`(段间不累加);每拍增益写入 ≤ 1 |
| | `test_ac019_4_stop_leavesNoTail_andEndsLoop` · `test_ac019_4_stopAll_clearsTailBookkeeping` | 停止路径零尾音 / 全清 |
| **AC-019-5** | `test_ac019_5_tailRamp_isMonotone_andMultiplicativelyUniform` | 单调不增 + 相邻步**倍率恒等**(无拐点)+ 终点 = `10^(floorDb/20)` |
| | `test_ac019_5_degenerateInputs_neverProduceBurst` | 0 / NaN / 零时长 ⇒ 不产生衰减(无爆音) |
| | `test_ac019_5_breathRows_introduceNoNewClipName` | **零新 clip**:三行呼吸 cue 的 `assets[]` 各恰 1 条;呼吸素材名不被任何非呼吸行复用 |
| **AC-019-6** | `production/qa/evidence/s19-breath-transition-listening-2026-10-18.md` | **NOT-RUN —— BLOCKING**,协议已备,待用户实机执行并签核 |
| **AC-019-7** | `docs/tech-debt-register.md` `TD-audio-100` 结案注 · `design/gdd/audio-system.md` `OQ-44-7` 状态刷 | **部分落**:代码半边/证伪记录已备;**结案注待 10-c 签核**(哨兵:10-c 未签 ⇒ TD-audio-100 不结案) |

---

## §三 证伪轮(逐条「停用什么 ⇒ 哪条红」)

> 纪律:每例**改 → 跑 → 还原在同一命令内**(`/tmp/fz.py` 形态);还原后 **SHA-256 逐字节校验**
> (`restored sha ok`)。种子注入例用独立原子脚本(同款 finally + SHA 校验)。

| # | 停用的守卫 | 期望变红的测试 | 实测(本轮回读) |
|---|---|---|---|
| **F1** | 门谓词 ①「过渡语义族名」:从 `TransitionSemanticTokens` 删 `"Death"` | `test_ac0191_fakeDeathDingOneShot_reportsRed_andNamesCue` · `test_ac0191_transitionNameWithLoopFieldAbsent_reportsRed` | **红 ×2**(`Expected: 1, But was: 0`)✅ |
| **F2** | 门谓词 ②「非循环行 × 禁止触发源」整臂 `if (false)` | `test_ac0191_oneShotWithThresholdTrigger_reportsRed` · `test_legalCategoryForbiddenTrigger_reportsRed` | **红 ×2** ✅ |
| **F3** | 驱动力 `BreathAction.End` 恢复成**旧行为**(`EndLoop` + 移除句柄) | `test_ac019_4_pauseKeepsBreathAndStopEndsIt` · `test_ac019_4_pauseLifespan_isBoundedByTailRamp` | **红 ×2** ✅ |
| **F4** | 尾音停表 `if (finished) _tailStartTick.Remove(...)` 停用 | `test_ac019_4_pauseLifespan_isBoundedByTailRamp`(到期拍簿记未清) | **红 ×1** ✅ |
| **F5** | `PatientCueEmit.Emit` 的强度改常量 200(集合外) | `test_ac019_3_oneShotIntensity_followsVitals_neverFixedVolume` | **红 ×1** ✅ |
| **F6** | `BreathTailRamp.ValueAt` 改**线性**衰减 | `test_ac019_5_tailRamp_isMonotone_andMultiplicativelyUniform`(倍率不一致 = 拐点) | **红 ×1** ✅ |
| **F7** | `BreathAction.End` 时**额外**发一条一次性 cue(阈值跨越 → 离散音效) | `test_ac019_3_transitionToPause_addsNoNewOneShotCue` · `test_ac019_4_pauseKeepsBreathAndStopEndsIt` | **红 ×2** ✅ |
| **F8** | 真种子 `assets/data/audio_events.json` 末尾注入 `Voice_Transition_Death_Ding`(非循环) | `test_ac0191_realSeed_zeroTransitionErrors` · `test_realSeedAudioEvents_zeroErrors` | **红 ×2**,报文点名该 cue ✅ |
| **F9** | 真种子呼吸行 `assets[]` 追加**已存在**的第二素材(`sfx_world_bell_01.wav`) | `test_ac019_5_breathRows_introduceNoNewClipName` | **红 ×1** ✅ |

**证伪轮抓出的两处假绿(已修,非「验证通过」而是「先红后修」)**

| 假绿 # | 现象 | 根因 | 修法 |
|---|---|---|---|
| **FG-1** | F5 首轮用常量 **128** 时**不红** | ① `TailSink.Intensities` 混入尾音增益(`Emit` 同时写增益),集合里常有 128 ⇒ 常量 128 被误认成合法值;② 前 200 拍体征恒 0.2,该段内「固定强度」与真值不可分 | ① 判据只取 `Looped == false` 的 DTO;② 体征全程缓升(`0.2 + tick×0.001`)。修后常量 200 ⇒ 红 |
| **FG-2** | `test_ac019_4_pauseKeepsBreathAndStopEndsIt` 的「停顿期零一次性 cue」恒真 | 该测构造时 `coughCue: null` ⇒ 13 的**全部**一次性发射被装配门掐掉,断言测不到任何东西 | `coughCue: 0` + 平稳期跑足 200 拍 + 前置断言「平稳期确有既有一次性 cue」;该测同时承载 AC-019-3 主判据 |

> ⚠️ 另有一处**残留假绿(未修,如实登记)**:F5 的第二形态「常量 128」在**修后**仍可能与体征派生值集合撞值
> (0.2 + tick×0.001 覆盖 51…65,128 不在其中,故修后常量 128 也会红 —— 本行随 FG-1 修复一并消除)。
> **F9 首版**用了一个**不存在**的新素材名,红的是 **D6 素材门**(`BakeValidationException`),
> 不是零新 clip 判据本身 ⇒ 改用已存在的素材名重做,才是对 `test_ac019_5_breathRows_introduceNoNewClipName`
> 的判别力证明(两次都如实记于本节:`F9-a = D6 门红 ×8(不是目标夹具)`;`F9-b = 目标夹具红 ×1 ✅`)。

---

## §四 回归

| 项 | 值 | 文件 |
|---|---|---|
| 基线(上一全量,修复轮) | `3732 / 3687 / 0 / 45`(EditMode) | `active.md` §story-025 修复轮 |
| 本件全量 ① | `3748 / 3702 / 1 / 45` —— 唯一红 = `AssemblyBoundaryTest.test_entryPoints_production44_noUnknownEntry`(`BreathTailRamp.State` 不在入口白名单) | **无原件**(被终跑覆盖;数字取当次 stdout) |
| 白名单增列后套件 | `AssemblyBoundaryTest` **24 / 24 / 0 / 0** | 本轮 batch |
| 本件全量 ②(最终) | `3748 / 3703 / 0 / 45`,exit 0 | `unity/Logs/s19-full-editmode.xml` |
| 等式判据 | `total(最终) − 3732 = 新增 [Test] 数` | 新增 `[Test]`:门 +6 · 运行期 +9 = **+15** |

> ⚠️ 首跑的红**不是既有回归**,而是本件新类触发 ADR-018 §一 的入口白名单门 —— 这正是该门在起作用。
> 处置 = **增列白名单**(附理由,见 §一末),不是改门、不是跳过测试。

**最终全量**:`3748 / 3703 / 0 / 45`,exit 0(`unity/Logs/s19-full-editmode.xml`,2026-10-18 08:03Z)。

**等式对账**:`total 3748 − 基线 3732 = +16`;`passed 3703 − 3687 = +16`。
新增 `[Test]` 逐名数 = 门 `EventTableGateTest` **+6** + 运行期 `RuntimeAssemblyTest` **+9** = **+15**。
⚠️ **差 1 未消 —— 如实挂【待核】**:差值来自「同一 total 下 1 条由红转绿」(首跑 3702/1 → 终跑 3703/0,
`AssemblyBoundaryTest` 入口测),按 `active.md` 的判据口径 ⇒ 1 条翻转不影响总数;
但**本件不得凭夹具计数宣称 +15 等于差额**,须以 `fullname` 逐名 diff 基线确定(**本轮未做**,见下)。

**⚠️ 缺基线原件**:`active.md` 记的基线 `3732 / 3687 / 0 / 45`(story-025 修复轮)**未留全量 XML 件**
(只有 PlayMode `s25-ussfix-full-playmode.xml` 197)→ **逐名 diff 不可执行**。
这本身是本件登记的一处**账面缺口**:全量回归不带原件 ⇒ 等式判据只能靠总数。
⇒ 后续 sprint 全量强制落件(建议:每轮全量 `cp test-results.xml unity/Logs/<id>-full-editmode.xml` —— 本件已落 `s19-full-editmode.xml`)。
⚠️ **另**:首跑的 XML 已被终跑覆盖(`s19-full-editmode.xml` 写的是终跑)⇒ §四表内「首跑」一行的原件**不可复算**,
其数字取自当次 stdout。如实登记。

---

## §六 单测差 1 之【待核】的处置(2026-10-18)

本件**不消**这 1 条差额,理由:可复算的判据(逐名 diff)缺基线原件;凭夹具计数把它算平,
正是「借绿的两种数字形态」禁止的形态。**处置**:挂到下次有原件对照的全量轮(建议 sprint-15 收口轮):
以「同基线 + 逐名 diff」一次结清本件 `+16` 与夹具计数 `+15` 的差 1。
