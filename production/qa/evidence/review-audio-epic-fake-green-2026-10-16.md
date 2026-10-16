# 评审原件:音频 epic 6 条 story 假绿批(`/code-review` 补评 · 评当下)

> **评审类型**:`/code-review` **补评**(read-only;本文件为该轮记录原件)
> **评审日期**:2026-10-16(sprint-13 S13-9)
> **评审对象**(6 条 story + 6 个测试文件):
> - `production/epics/audio-system/story-007-net-divergence-remote-derivation.md` ← `unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs`(19 测)
> - `story-008-spatialization-world-breath.md` ← `spatialization_test.cs`(20 测)
> - `story-009-source-lifecycle-handover.md` ← `loop_lifecycle_test.cs`(12 测)
> - `story-010-asset-pipeline-bake-gates.md` ← `pipeline_gate_test.cs`(11 测)
> - `story-011-settings-exposure-reset.md` ← `settings_exposure_test.cs`(11 测)
> - `story-012-music-guardrails-perf-vr.md` ← `music_guardrails_test.cs`(12 测)
> **触发**:sprint-13 S13-7 11 处方用药人工走查发现「全案运行期音频链未接通」⇒ 上溯深查
> **本文件裁定**:**6 条全部 REQUEST_CHANGES** —— 假绿(BLOCKING 级),`Complete` 不成立
> **⚠️ 不可追补声明**:本件**评的是 2026-10-16 当下的代码与测试**,得到的是**新判定**;
> 它**不追认**这 6 条 story 原先的 `Complete` 判定,也**不追认**它们各自评审轮的判定
> (`.claude/docs/review-workflow.md` §不可追补)。
> **⚠️ 复核范围声明**:本件**只覆盖 007–012 六条**。epic 内 **001–006 / 013 / 014 共 8 条**
> 抽样判为低危(测试真引生产类,Fake 仅为注入缝替身),**本件不声称已复核它们** ——
> 抽样 ≠ 普查(承 ADR-031 V-10 教训)。

---

## §0 判定摘要

| Story | 声称状态 | 本轮判定 | 🔴 假 | 🟡 半真 | 🟢 真 | 测试数 |
|---|---|---|---|---|---|---|
| 007 联机分叉与远端派生 | Complete ✅ 5/5 | **REQUEST_CHANGES** | 10 | 5 | 4 | 19 |
| 008 空间化与世界语境呼吸 | Complete ✅ 5/5 | **REQUEST_CHANGES** | 16 | 4 | 0 | 20 |
| 009 声源生命周期与贴耳交接 | Complete ✅ 4/4 | **REQUEST_CHANGES** | 9 | 2 | 1 | 12 |
| 010 素材管线与事件表烘焙门 | Complete ✅ 4/4 | **REQUEST_CHANGES** | 7 | 4 | 0 | 11 |
| 011 设置暴露面与归零机制 | Complete ✅ 4/4 | **REQUEST_CHANGES** | 7 | 3 | 1 | 11 |
| 012 乐层护栏与性能/VR 切面 | Complete ✅ 4/4 | **REQUEST_CHANGES** | 5 | 6 | 1 | 12 |
| **合计** | 14 条中 6 条 | **6/6 REQUEST_CHANGES** | **54** | **24** | **7** | **85** |

**一句话判定**:85 个测试中只有 **7 个**断言真生产物(且全在 `Sim.Contracts` 契约层或真数据文件),
**零个**断言 44 生产树(`Gameplay.Presentation/Audio/`)里的实现类。
**AC 的语义层不被实现保证,而由测试自造的断言顶替** ⇒ 测试全绿 ≠ AC 被验证。

**级别 = BLOCKING**(不是 ADVISORY),依据三条:
1. 项目自己的**证伪轮门**(`.claude/skills/story-done/SKILL.md` §Falsifiability Gate)逐字写:
   「假绿夹具会以绿的形式留下却什么都没测……断言仍绿 ⇒ **BLOCKING**」;
2. 6 条 story 的测试在账本 `tests/unit/audio_system/README.md` 中**全部标 `BLOCKING`**(逐行实测);
3. 与 `case-system` 001–004 病灶**同型**(记忆 `project_case_system_001_004_in_review`)——
   这是本项目**第二次**撞上该病灶,规模更大(6 条 / 85 测)⇒ **系统性,非偶发**。

---

## §1 单一根因

**6 条 story 的 54 条 🔴 可归为同一句话**:

> story 声称的**生产实现类从未存在**,测试改用**测试文件内自造的 Fake / 自写谓词 / 本地字面量**顶替,
> 于是断言在「被测对象不存在」时**依然为绿**。

### 1.1 决定性事实(可复算)

**事实 A —— 三条测试文件的 `using` 挂空**:

```
$ grep -n "using DaYiJingCheng.Gameplay.Presentation.Audio;" \
    unity/Assets/Tests/EditMode/Audio/{net_derivation,spatialization,loop_lifecycle}_test.cs
net_derivation_test.cs:35:using DaYiJingCheng.Gameplay.Presentation.Audio;
spatialization_test.cs:15:using DaYiJingCheng.Gameplay.Presentation.Audio;
loop_lifecycle_test.cs:26:using DaYiJingCheng.Gameplay.Presentation.Audio;
```
import 了 44 生产命名空间,**零处使用该命名空间的具体类型**(下条实证)。

**事实 B —— story 声称的生产类型,生产树与测试树命名全部零命中**:

```
$ for t in SourcePool LatticeToWorld CellJitter RankKeyComputer OcclusionDriver \
           LoopWatchdog HandleManager HandoverFsm HandoverMux MusicXfadeGuard DspBudgetGuard; do
    p=$(grep -rl "class $t\|struct $t" unity/Assets --include=*.cs 2>/dev/null | grep -v /Tests/ | wc -l)
    q=$(grep -rl "class $t\|struct $t" unity/Assets --include=*.cs 2>/dev/null | grep /Tests/ | wc -l)
    echo "$t 生产树=$p 测试树=$q"
  done
```
实测结果:**11 个类型生产树全部 = 0**;其中 8 个测试树也 = 0,
`RankKeyComputer` / `CellJitter` / `MusicXfadeGuard` / `DspBudgetGuard` 测试树 = 1
—— **那 1 个正是测试文件自己定义的那个类**。

**事实 C —— 全库零生产 `IAudioCueSink` 实现**:

```
$ grep -rn ": IAudioCueSink" unity/Assets
```
唯 3 处,全在测试树(`PatientAI/behavior_map_test.cs:887` · `Audio/net_derivation_test.cs:532` ·
`Audio/breath_layers_test.cs:1124`),无一处接真 `AudioSource`。

**事实 D —— 44 生产树实际只有 15 个类**(全部命名空间 `DaYiJingCheng.Gameplay.Presentation.Audio`):
`AudioMixerParameterSink · AudioSourceLayerTransport · AudioTuning · BreathLayerDriver · BreathPhaseGate ·
CoughChannel · FilterRamp · IBreathLayerTransport · IMixerParameterSink · InspirePhaseMapping ·
MixerRegistry · PlayerBusVolumeInitializer · ReverbPreset · ReverbPresetSwitcher · SnapshotDirector ·
SnrAnalysis · TierFilterDriver · VoiceBucket · VoiceVariantLib · VoiceVariantRow · VoiceVariantTable · VolumeRamp`
—— 无一属于 007/008/009/010/011/012 声称交付的范畴(声源池 / 派生器 / 交接 FSM / 设置 store / 乐层护栏)。

### 1.2 「已披露却仍判 Complete」—— 病灶最要紧的一条

**不是隐瞒**。6 条 story 的 `Completion Notes` **都逐字自陈了同一句**(实测 6/6 命中):

```
1. **测试自持谓词面**:生产代码(…)尚未实现,本 story 测试用自持谓词面(…)。
   这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
```
(story-008 `:89` · 009 `:88` · 010 `:85` · 011 `:87` · 012 `:86`;story-007 同型见其 Completion Notes)

**病灶在于**:这句自陈被登记为 **ADVISORY 偏差**,然后 story 判 `Complete`、测试**仍计入 BLOCKING 证据**。
按项目自己的规则,**这两件事不能并存** —— 自陈「生产代码尚未实现」= 该 AC 的语义层零验证 = BLOCKING。

### 1.3 「空转真扫」—— 本次发现的新型假绿(007/008/009 共 5 处)

**机理**:真门 + 非空扫描面 + 被断言的类不存在 ⇒ 零违例是**对空集的平凡成立**,且**绕过 b5 的零命中护栏**。

```
$ sed -n '1097,1115p' unity/Assets/Editor.Tools.Gates/AssemblyGates.cs
public static List<string> RunAudioBoundary(out List<string> warnings)
{
    ...
    errs.AddRange(CheckAudioAssemblyIl(dll, AudioModuleNamespacePrefix, out var matched));
    if (matched == 0)
        warnings.Add($"[b5-W] 44 命名空间前缀「{AudioModuleNamespacePrefix}」在 " +
                     $"{PresentationAssemblyName} 内 0 命中 —— 44 实现类型尚不存在,① 对空集" +
                     "平凡成立(扫描键 = AudioModuleNamespacePrefix;实现期 0 命中转 FAIL)。");
```

三点实测:
1. `CheckAudioAssemblyIl` **扫的是整个命名空间前缀**(`AssemblyGates.cs:444-546`,按 `IsInNamespacePrefix` 逐类型),
   不是特定类 —— 而 44 命名空间下**有 15 个真类型** ⇒ `matched = 15 ≠ 0` ⇒ **护栏不触发**;
2. 即便触发,动作是 `warnings.Add`(**WARN**),**不是** `errs.Add`(FAIL)—— 该 WARN 在
   `RunAudioBoundary` 的调用点(`:85` / `:1167`)未升格为红;
3. 于是「派生面零 `IPositionalChannel` 引用」「派生面方法体禁 `Random`/`Time`/`DateTime`」两条断言
   —— 门真、面非空、**但「派生面」这个类不存在** ⇒ 绿得毫无意义。

⚠️ 值得注意:`AssemblyGates.cs:1113` 自己写着「**实现期 0 命中转 FAIL**」—— 即该护栏**设计上知道**
「0 命中 = 假绿」这件事,却在**当前形态**下无法捕捉「面非空但被断言类不存在」。

---

## §2 逐 story 判定

### §2.1 Story 007 —— 联机分叉与远端派生(静态半)

**AC 数实测**:3 个 checkbox(`AC-44-07` / `AC-44-D1` / `AC-44-D7`);Completion Notes 另称交付 5/5,
含两个**非 checkbox** 的 AC(`AC-44-D10 锚点纪律` / `EndLoop 自评兜底`)。

| AC | 对应测试(行号) | 断言对象 | 判定 |
|---|---|---|---|
| AC-44-07 | `test_setTier_defaultLocalRecorded`(L80) | 自造 `TierRecordingSink : IAudioCueSink`(L532);`TierSource` 是真契约 | 🟡 半真 |
| AC-44-07(文档) | `test_gdd_netSection_noHostTierWording`(L101) | 真 GDD 文件 | 🟢 真 |
| AC-44-D1 | `test_positionalChannel_singleConsumerImplementation`(L128) | 真装配反射;`implementors.Count ≤ 1`,**实测 P0 内 = 0** ⇒ L146 直接 `return` 跳过方法集断言 | 🟡 半真(空转) |
| AC-44-D1 | `test_worldPosLatest_carriesServerTick`(L163) | 真契约 `WorldPosLatest.ServerTick:uint` | 🟢 真 |
| AC-44-D1(IL) | `test_derivationSurfaceTypes_zeroPositionalChannelRefs`(L177) | 真门 `CheckAudioPositionalChannelRefs` | 🟡 半真(空转真扫) |
| AC-44-D1 负向 | `test_derivationSurfaceScan_fixtureWithChannelRef_flagged`(L198) | 自造 `ChannelRefFixture`(L625) | 🔴 假 |
| AC-44-D7 ② | `test_derivationSurfaceTypes_ilBody_noClockOrRandomRefs`(L217) | 真门 `CheckAudioClockTokens` | 🟡 半真(空转真扫) |
| AC-44-D7 ② 负向 | `test_derivationScan_ilBodyFixtureRandom_flagged`(L239) | 自造 `RandomBodyFixture`(L630) | 🔴 假 |
| AC-44-D7 ③ | `test_audioCueDto_allFieldsIntegerDomain`(L265) | 真契约 `AudioCueDto` | 🟢 真 |
| AC-44-D7 ③ | `test_audioCueDto_intensityIsByte`(L283) | 真契约 | 🟢 真 |
| AC-44-D7 ③ | `test_audioCueDto_cellIsInt3`(L293) | 真契约 | 🟢 真 |
| AC-44-D7 重排差分(**核心**) | `test_reorderInvariance_arrivalOrderPreservesCueHash`(L308) | **自造** `DeriveCueHash`(L512)+ 自造 `NetSimEvent`(L637) | 🔴 假 |
| 重排差分 | `test_reorderInvariance_intensityFeedsHash`(L339) | 同上 | 🔴 假 |
| 重排差分 | `test_reorderInvariance_eventCountFeedsHash`(L366) | 同上 | 🔴 假 |
| AC-44-D10 | `test_anchorAbsent_oneShotDropped_loopCuePending`(L387) | 自造 `FakeAnchorGate`(L549) | 🔴 假 |
| AC-44-D10 | `test_anchorArrived_pendingLoopReleased`(L407) | 自造 `FakeAnchorGate` | 🔴 假 |
| AC-44-D10 | `test_anchorStale_refused`(L427) | 自造 `FakeAnchorGate.IsStale`(L592) | 🔴 假 |
| EndLoop 兜底 | `test_endLoop_selfAssessPredicate_pureAndBounded`(L447) | 自造 `FakeEndLoopWatchdog`(L607)+ `FakeLoopProgress`(L598) | 🔴 假 |
| EndLoop 有界 | `test_endLoop_selfAssessThreshold_isPositiveBoundedConstant`(L467) | 自造常量 `StagnantTicksThreshold = 10`(L610) | 🔴 假 |

**计:🟢 4 / 🟡 5 / 🔴 10。**
**⚠️ 关键**:AC-44-D7 的**核心判据**(重排差分哈希一致性)—— 那是整条 AC 的落点 ——
被测物是**测试自己写的折叠函数**,不是发射模块。这是本批**最重的一处**:
它测的是「我写的哈希函数对乱序输入稳定」,与「44 的 cue 派生器对乱序流稳定」无关。

### §2.2 Story 008 —— 空间化与世界语境呼吸

**AC 数实测**:4 个 checkbox(`AC-44-D2` / `D3` / `D8` / `16`)+ 1 个同格式 checkbox(`优先级公式`)= 5。

| AC | 对应测试(行号) | 断言对象 | 判定 |
|---|---|---|---|
| AC-44-D2 | `test_latticeToWorld_noFloatToWorldPosMethod`(L47) | 真装配反射 | 🟡 半真(空转真扫) |
| AC-44-D2 负向 | `test_latticeToWorld_reverseDerivationFixture_flagged`(L67) | 自造 `ReverseDerivationFixture`(L416) | 🔴 假 |
| AC-44-D3 | `test_spatialization_noEventSinkRefs`(L81) | 真门 `CheckAudioAssemblyIl`(**有 `matched > 0` 守卫**,L86) | 🟡 半真 |
| AC-44-D8 ① | `test_occlusionDriver_occluded_lowpassPositive`(L95) | 自造 `FakeOcclusionDriver`(L342) | 🔴 假 |
| AC-44-D8 ① | `test_occlusionDriver_notOccluded_noLowpass`(L104) | 自造 | 🔴 假 |
| AC-44-D8 ① 负向 | `test_occlusionDriver_zeroCutoff_rejected`(L113) | 自造 | 🔴 假 |
| AC-44-D8 ② | `test_sourcePool_sameCell_nSourcesNotMerged`(L123) | 自造 `FakeSourcePool`(L330)—— 内部仅 `Dictionary<int, SourceEntry>`,**零 `AudioSource`** | 🔴 假 |
| AC-44-D8 ② | `test_sourcePool_singlePatient_oneSource`(L136) | 自造 | 🔴 假 |
| AC-44-D8 ② | `test_sourcePool_release_reusable`(L144) | 自造 | 🔴 假 |
| 优先级公式 | `test_rankKey_deterministicAcrossCalls`(L156) | 自造 `RankKeyComputer`(L363) | 🔴 假 |
| 优先级公式 | `test_rankKey_sameClassCloserFirst`(L176) | 自造 | 🔴 假 |
| 优先级公式 | `test_rankKey_equalDistance_cellJitterBreaksTie`(L191) | 自造 + 自造 `CellJitter`(L398) | 🔴 假 |
| 优先级公式 | `test_rankKey_recalcOnlyOnBirthDeath`(L207) | 自造(静态字段计数) | 🔴 假 |
| 优先级公式 | `test_priority_duckDoesNotDrop`(L225) | 自造 `FakeDuckComputer`(L407,硬编码 `-12f`) | 🔴 假 |
| 优先级公式 | `test_priority_busNotPriorityClass`(L238) | 自造 | 🔴 假 |
| AC-44-16 | `test_eventTable_worldBreathRow_presentAndWired`(L262) | 真数据 `assets/data/audio_events.json`(`:42-51` 实测 bus=Ambience / min=1 / max=12 / occlusion=1200) | 🟡 半真 |
| AC-44-16 负向 | `test_eventTable_worldBreathRow_missing_red`(L277) | 自造正则 + `string.Replace` 合成 JSON | 🔴 假 |
| AC-44-16 负向 | `test_eventTable_worldBreathRow_wrongBus_red`(L288) | 同上 | 🔴 假 |
| F-44.4 | `test_latticeToWorld_integerCellToFloat`(L303) | 自造 `FakeLattice`(L392) | 🔴 假 |
| F-44.4 | `test_cellJitter_deterministicAndDistinguishesSameCell`(L313) | 自造 `CellJitter`(内部用真 `SplitMix64.Hash`) | 🔴 假 |

**计:🟢 0 / 🟡 4 / 🔴 16。**
**⚠️ 关键**:`FakeSourcePool` **内部没有任何 `AudioSource`** —— 而 AC-44-D8 ② 的判据逐字是
「同 cell N 病人 = **N 条独立 `AudioSource`**(实例数断言 = N,不合并)」。
被测物连 `AudioSource` 都没有,「N 条独立」这句话无从谈起。
**⚠️ AC-44-16 的真门半边尚不存在**:真门 `AudioEventTableGates` 的 `RequiredCues` 只查 cue 存在性,
`grep Ambience AudioEventTableGates.cs` **零命中** ⇒「bus ∈ Ambience 子通道 = 构建失败」这一半无实现。

### §2.3 Story 009 —— 声源生命周期与贴耳交接

**⚠️ AC 编号格式瑕疵**:该 story 的 4 条 AC **无 `AC-44-xx` 编号**,用中文描述名 + `(AC 源:…)`:
```
:33 - [ ] **EndLoop 自评兜底**(AC 源:Edge Cases「EndLoop 永不到达」条 + ADR-001 裁决二 Validation):…
:34 - [ ] **贴耳交接契约**(AC 源:Edge Cases 交接条):…
:35 - [ ] **重复/过期句柄**(Edge Cases):…
:36 - [ ] **咳嗽不停呼吸**(Edge Cases 医学条):…
```
全文 `AC-44-` 编号仅 1 处命中(`:36`),且是**引用 Story 004 的 `AC-44-08`**,不是本 story 自己的。
⇒ **任何按 `AC-44-xx` 编号做的 traceability / 账本 / 门检对 009 全数落空。**
(成因:该 story AC 段自陈「§Edge Cases 为 AC 源」,不来自 GDD 的编号 AC。)

| AC | 对应测试(行号) | 断言对象 | 判定 |
|---|---|---|---|
| EndLoop 兜底 | `test_endLoopWatchdog_staleStream_stopsWithinBound`(L42) | 自造 `FakeLoopWatchdog`(L275) | 🔴 假 |
| EndLoop 兜底 | `test_endLoopWatchdog_resumeNoDoubleStop`(L62) | 自造 | 🔴 假 |
| EndLoop 兜底 | `test_endLoopWatchdog_zeroBoundStopsImmediately`(L85) | 自造 | 🔴 假 |
| EndLoop 兜底 | `test_endLoopWatchdog_noWallClockRefs`(L104) | 真门 `CheckAudioClockTokens` | 🟡 半真(空转真扫) |
| EndLoop 兜底 | `test_endLoop_contractShape_localHandleOnly`(L124) | 真契约 `IAudioCueSink` + `AudioCueHandle` | 🟢 真 |
| 贴耳交接 | `test_handover_enterStethoscope_worldFadesOut`(L146) | 自造 `FakeHandoverFsm`(L318) | 🔴 假 |
| 贴耳交接 | `test_handover_exitStethoscope_worldFadesIn`(L165) | 自造 | 🔴 假 |
| 贴耳交接 | `test_handover_idempotentReentry`(L185) | 自造 | 🔴 假 |
| 贴耳交接 | `test_handover_worldCueArrives_noRestart`(L204) | 自造 | 🔴 假 |
| 贴耳交接 | `test_handover_hardCut_rejected`(L223) | 自造(异常由测试自己的 ctor L329 抛) | 🔴 假 |
| 重复/过期句柄 | `test_duplicateBeginLoop_lastWins`(L234) | 自造 `FakeHandleManager`(L386) | 🔴 假 |
| 重复/过期句柄 | `test_expiredEndLoop_ignored`(L253) | 自造 | 🔴 假 |
| 咳嗽不停呼吸 | **本文件无测试**(L265-269 注释委托) | 委托 Story 004 `breath_layers_test.cs:530`(调**真生产类** `CoughChannel.Dispatch`) | 🟡 半真(跨 story) |

**计:🟢 1 / 🟡 2 / 🔴 9。**
**⚠️** 唯一 🟢 的 `test_endLoop_contractShape_localHandleOnly` 也只验**契约形状**
(`Sim.Contracts` 的接口),不验 44 的实现。

### §2.4 Story 010 —— 素材管线与事件表烘焙门

| AC | 对应测试(行号) | 断言对象 | 判定 |
|---|---|---|---|
| AC-44-D4 | `test_noJsonParser_noNewtonsoftRefs`(L59) | 真生产类型公开方法签名(**有 `types.Count > 0` 守卫**,L78) | 🟡 半真 |
| AC-44-D4 | `test_noJsonParser_noJsonReaderRefs`(L86) | 真生产类型 | 🟡 半真(**无 Count 守卫** ⇒ 空集恒绿) |
| AC-44-D4 | `test_noFixParse_refs`(L112) | 真生产类型 | 🟡 半真(同上) |
| AC-44-D5 | `test_addressables_audioGroupSeparateFromDataCore`(L145) | 真文件 `AssetGroups/*.asset`;音频组半边 `Assert.Ignore`(L175) | 🟡 半真 |
| AC-44-D5 负向 | `test_addressables_mixedGroup_rejected`(L187) | 测试本地字符串 + 自造 `GroupSeparabilityPredicate`(L352) | 🔴 假 |
| AC-44-D6 | `test_assetExistence_missingWav_rejected`(L209) | **测试自己创建的文件** + 内联 `File.Exists` | 🔴 假 |
| AC-44-D6 | `test_assetExistence_missingWavFixture_rejected`(L232) | 测试本地数组 + 内联 `File.Exists` | 🔴 假 |
| AC-44-D6 | `test_assetExistence_bakeThrowsNotSilent`(L249) | 真生产类型 `BakeValidationException`(`Editor.Tools.Bake/ItemDatabaseBaker.cs:21`) | 🟡 半真 |
| 听诊窗预载 | `test_preloadGate_stethoscopeFocus_preloaded`(L273) | 自造 `FakePreloadGate`(L336) | 🔴 假 |
| 听诊窗预载 | `test_preloadGate_notLoaded_blocks`(L289) | 自造 | 🔴 假 |
| 听诊窗预载 | `test_preloadGate_oneShotCueDelay_bounded`(L304) | **两个硬编码 int**(`maxDelayMs=100` / `actualDelayMs=50`) | 🔴 假 |

**计:🟢 0 / 🟡 4 / 🔴 7。**
**⚠️ 自造谓词原文**(AC-44-D5 的负向断言对象):
```csharp
private static class GroupSeparabilityPredicate
{
    public static bool IsMixedGroup(string yaml)
        => yaml.Contains("assets/audio/") && yaml.Contains("data-core");
}
```
测试在 L190-192 **自己拼**一个 `mixedYaml` 字面量,再喂给这个自造谓词。
整个断言 = 「字面量含两个子串」。
**⚠️ 副作用登记**:`test_assetExistence_missingWav_rejected` 会真实创建并写入仓库根
`assets/audio/`(L213-227),末尾 `File.Delete` 清理 —— 违反 `test-standards.md`
「Unit tests must not depend on external state (filesystem)」。
**⚠️ 听诊窗预载 AC 要求 PlayMode 断言,实际三测全在 EditMode 且全为 Fake。**

### §2.5 Story 011 —— 设置暴露面与归零机制

| AC | 对应测试(行号) | 断言对象 | 判定 |
|---|---|---|---|
| 注册表 AC① | `test_settingsRegistry_busVolumes_subsetOfRegistry`(L51) | 真 `MixerRegistry.BusVolumeParameters`(7 路)⊆ **测试本地 8 元数组** | 🟡 半真 |
| 注册表 AC① | `test_settingsRegistry_monoEnabled_inRegistry`(L71) | 测试本地数组 | 🔴 假 |
| 注册表 AC① | `test_settingsRegistry_unregisteredParam_rejected`(L83) | 测试本地数组 + 字面量 | 🔴 假 |
| 注册表 AC① | `test_settingsRegistry_exactly8Entries`(L98) | 测试本地数组 `.Length` | 🔴 假 |
| AC-44-13 | `test_monoOption_existsWithValidDefault`(L110) | 测试本地数组;**出厂默认半边零断言**(见下) | 🔴 假 |
| AC-44-15 [A] | `test_subtitleKeySet_coveredByEventTable`(L135) | 真文件 `assets/data/audio_events.json`(前置 `File.Exists`) | 🟢 真(「含 48 口述」半边空真) |
| AC-44-15 [A] | `test_subtitleKeySet_missingSubtitle_rejected`(L170) | 测试本地字符串字面量 | 🔴 假 |
| AC-44-19 | `test_resetMechanism_threeStepsReachFactory`(L190) | 自造 `FakeSettingsStore` + `FakeResetMechanism` | 🔴 假 |
| AC-44-19 | `test_resetMechanism_persistsAfterRestart`(L213) | 自造 Fake(**硬编码了被断言的期望值**) | 🔴 假 |
| AC-44-19 | `test_resetMechanism_skipSidecar_rejected`(L235) | 自造 Fake | 🔴 假 |
| AC-44-12 [L] | `test_a11yDocumentation_threeItemsPresent`(L261) | 真文件 `design/gdd/audio-system.md` | 🟡 半真 |

**计:🟢 1 / 🟡 3 / 🔴 7。**

**⚠️ 三处最重**:

**(a) AC-44-13 的出厂默认半边被删空** —— 测试尾部原文:
```csharp
// 出厂默认语义:mono 默认 = false(关)。
// ⚠️ S11-3(2026-10-15):原尾部 `Assert.Inconclusive("待生产实现后从烘焙分区
//    读取验证")` 删除 —— UTF 把 Inconclusive 计入非零退出码…
//    「从烘焙分区读实际默认值断言」的实现义务不丢:44 生产实现轮接手,
//    届时把本注释块替换为真断言。
```
⇒ 「出厂默认 ∈ 烘焙分区」**零断言**,而 story 的 `Criteria` 仍记「4/4 passing」、
账本 `README.md:389` 仍记 BLOCKING。**注意注释自称「注册表存在性断言保留(非假绿)」**
—— 而该存在性断言的宾语是**测试本地的数组**,恰是假绿的定义。

**(b) 归零 Fake 循环论证**(L305-318 + L226-229):
```csharp
public void LoadFromSidecar(bool sidecarWritten)
{
    if (!sidecarWritten) { _values.Clear(); }
    else {
        _values["bus_volume_master"] = 1.0f;   // ← 硬编码
        _values["mono_enabled"] = false;
    }
}
...
Assert.That(newStore.GetValue("bus_volume_master"), Is.EqualTo(1.0f), "重启后 … == 出厂值");
```
Fake 的恢复路径硬编码 `1.0f`,断言就是查 `1.0f`。

**(c) `settings_visible` 注册表在生产侧不存在** —— 实测:
```
$ grep -rn "settings_visible" assets/          → 零命中
$ grep -rln "settings_visible\|bus_volume_master" assets/   → 零命中
```
`audio_events.json` 顶层键 = `schema_version` / `_note` / `tier_map` / `rows`,**无 `settings_visible`**。
权威件:GDD `design/gdd/audio-system.md:903-908`(8 行闭枚举)· `design/ux/settings-shell-42.md:268`。
自然落点 = 名字半边扩 `MixerRegistry`(加 `mono_enabled`)+ 值半边入 ADR-014 烘焙数据。
**⚠️ `ISettingsStore` / `IResetMechanism` 全库无权威定义** —— 仅出现 2 处:
story-011 `:89`(Completion Notes,非 AC 非 Implementation Notes)+ 测试文件 `:188`(注释)。
⇒ 接口名本身是**测试作者的发明**,story 事后追认。

### §2.6 Story 012 —— 乐层护栏与性能/VR 切面

| AC | 对应测试(行号) | 断言对象 | 判定 |
|---|---|---|---|
| AC-44-18① | `test_musicXfade_belowMin_rejected`(L47) | 本地 `const 2000` + 自造 `MusicXfadeGuard` | 🔴 假 |
| AC-44-18① | `test_musicXfade_atMin_accepted`(L63) | 同上 | 🔴 假 |
| AC-44-18① | `test_musicXfade_aboveMin_accepted`(L79) | 同上 | 🔴 假 |
| AC-44-18② | `test_musicLayer_triggerSource_isEncounterMusicLayer`(L96) | 真文件 `audio_events.json` | 🟢 真 |
| AC-44-18② | `test_musicLayer_g1g3Guardrails_registered`(L121) | 真文件 ADR-018(`Contains("G1")` **无锚点**) | 🟡 半真 |
| AC-44-18③ | `test_musicXfadeMinMs_registeredInTuning`(L138) | 真文件 GDD | 🟡 半真 |
| AC-44-E2 | `test_dspBudget_p95WithinBudget`(L159) | 本地 `const 2.5f` / `1.8f` + 自造 `DspBudgetGuard` | 🔴 假 |
| AC-44-E2 | `test_dspBudget_p95Exceeds_rejected`(L175) | 同上 | 🔴 假 |
| AC-44-E2 | `test_dspBudget_measurementRegistry_noEmit`(L194) | 真文件 GDD(**AC 自身散文**) | 🟡 半真 |
| AC-44-E1 | `test_vrAudio_p0Build_noVrAudioRefs`(L216) | 真生产 44 类型签名(**有 `types.Count > 0` 守卫**,L238) | 🟡 半真 |
| AC-44-E1 | `test_vrAudio_p1bFacet_registered`(L245) | 真文件 GDD | 🟡 半真 |
| AC-44-11 | `test_vrListen_ac4411_blockedByP1b`(L263) | 真文件 GDD | 🟡 半真 |

**计:🟢 1 / 🟡 6 / 🔴 5。**

**⚠️ 两行自写谓词**(L315-329):
```csharp
private static class MusicXfadeGuard {
    public static bool IsXfadeValid(int xfadeMs, int musicXfadeMinMs) => xfadeMs >= musicXfadeMinMs;
}
private static class DspBudgetGuard {
    public static bool IsP95WithinBudget(float p95Ms, float audioBudgetMs) => p95Ms <= audioBudgetMs;
}
```
AC-44-18① 的判据是「`xfade_ms ≥ MUSIC_XFADE_MIN_MS`(**构建期数值比较**)」。
真种子里 `xfade_ms = 4000`(`audio_events.json:58`),而测试硬编码 `2000` / `1999`
—— **测试从不读真种子**。

**⚠️ 唯一具名空位(本批最易兑现)**:`AudioEventTableGates.ValidateXfade`
(`unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs:955-1000`)的 XML doc 逐字写:
```
/// **不做** <c>≥ MUSIC_XFADE_MIN_MS</c> 的数值断言(AC-44-18,Story 012)。
```
实测该函数当前只做三件:① 乐层行必有 `xfade_ms`;② 必须是整数字面量;③ 乐层行 `> 0`。
**下界比较一行都没有。** 而 `ValidateXfade` 已在 `AudioEventTableGates.Validate` 的聚合链里
(`:233-255`),是构建期真门 ⇒ **正确落点已具名,兑现成本最低**。
`MUSIC_XFADE_MIN_MS` 常量应落 `Gameplay.Presentation/Audio/AudioTuning.cs`(44 运行期常量先例)。

**⚠️ 「测量点注册表」无任何落点** —— 实测:
```
$ grep -rn "AUDIO_BUDGET_MS\|AudioBudgetMs\|DspBudgetGuard" unity/Assets/  → 仅测试文件本身
$ grep -rn "测量点注册表" 全库                                  → 仅 GDD :1127 + 测试文件 5 处注释
```
而 `test_dspBudget_measurementRegistry_noEmit` 断言的宾语是
**「AC 的判据原文存在于 GDD」**,不是「某个注册表不含 Emit」。

---

## §3 三处「修完仍假绿」返工记录(病灶会穿过修复轮存活)

**这三处是本批最有价值的发现** —— 它们证明该病灶**不是一次性疏忽**,而是会**穿过修复轮**:

| story | 修复轮 | 原稿 | 「修复」后 | 性质 |
|---|---|---|---|---|
| 010 | B1/B2/B4 | 恒真断言 | **自包含夹具恒真**(`FakePreloadGate` / 自建 wav / 自造谓词) | 「恒真」换成「自包含夹具恒真」—— 同为假绿 |
| 011 | B2 | 硬编码常量等于自身 | **删掉断言留注释**(注释自称「非假绿」) | 从「假绿」变成「零断言」,账本仍标 BLOCKING |
| 012 | R2 | (测试断言 GDD 缺某词) | **先改 GDD 再让测试断言 GDD 含该词** | 断言对象先造出来再断言 |

⇒ **推论**:仅「重跑测试确认绿」**不足以**发现此类假绿。修复轮的验收判据必须是
**「断言的真值随生产码变异而变」**(证伪轮),不是「断言仍绿」。

---

## §4 须实现生产类清单(按依赖排序 —— 交付物,非本件裁决面)

> ⚠️ 本清单是**实现地图的输入**,不是本件的裁决。**实现另立 sprint**(用户 2026-10-16 裁定)。
> ⚠️ **本清单不声称已穷尽** —— 它来自 6 条 story 的 AC 与 Implementation Notes 反推。

### 4.1 依赖根(008 —— 其余依赖它)

| 生产类 | 落点 | 由哪条 AC 要求 | 同型先例 |
|---|---|---|---|
| `LatticeToWorld`(整数格→float) | `Gameplay.Presentation/Audio/`,命名空间 `DaYiJingCheng.Gameplay.Presentation.Audio` | AC-44-D2 / F-44.4(GDD :376 具名) | 无 |
| `cell_jitter`(`SplitMix64(ActorId,Cell)` 派生 u16) | 同上 | AC-44-D8 ② / 优先级公式 | `Sim.Contracts/SplitMix64.cs:60` `Hash(long,long,long,long)` |
| `rank_key` 计算器(priority_class → distance → cell_jitter → CueId) | 同上 | 优先级公式(GDD :838-848 有完整定义) | 无 |
| **44 声源池**(`AudioSource` 创建/复用/释放,同 cell N 不合并) | 同上(**ADR-028 ① 唯一所有者 = 44**) | AC-44-D8 ② | 无生产先例;`AudioSourceLayerTransport` 是薄壳非池 |
| 遮挡驱动器(逐源 `AudioLowPassFilter` + cutoff > 0 拒绝) | 同上 | AC-44-D8 ① | `TierFilterDriver.cs` |
| 空间化装配/派发入口 | 同上 | AC-44-D2/D8 全 | `SnapshotDirector` / `BreathLayerDriver` |

### 4.2 依赖 008

| 生产类 | 落点 | 由哪条 AC 要求 | 同型先例 |
|---|---|---|---|
| 44 侧 cue 发射派生器(007 只说「发射模块」,未具名) | 同上 | AC-44-D7 ①②③(三腿全打它) | `PatientCueEmit.cs`(13→44 唯一发射桥) |
| `IAudioCueSink` 生产实现(渲染适配层) | 同上(**含 `AudioCueDto` token ⇒ b5 门硬约束**) | AC-44-07 | 全库零先例 |
| EndLoop 自评看门狗(从快照 `Progress` 自求值,时钟 = `ITickProvider`) | 同上 | EndLoop 自评兜底 | `ITickProvider`(`Sim.Contracts/Abstractions.cs`) |
| `HandoverMux` / 交接 FSM | 同上 | 贴耳交接契约;008 `:45` 已点名 | 无 |
| 句柄生命周期管理器 | 同上 | 重复/过期句柄 | 无 |
| `LOOP_EVAL_MAX_TICKS` / `HANDOVER_MS` 常量 | `AudioTuning.cs`(扩) | 上述两条 | `AudioTuning.RampSeconds` |

### 4.3 独立支线(010 / 011 / 012)

| 生产类/数据 | 落点 | 由哪条 AC 要求 | 同型先例 |
|---|---|---|---|
| 音频事件表 Baker/Binder/CookedWriter | `unity/Assets/Editor.Tools.Bake/`(**实测零 audio 文件**) | AC-44-D4 / D6 | `DelayTable53Bake.cs` / `DelayTableBinder.cs` / `DelayTableCookedWriter.cs` |
| 素材存在性谓词(`assets[]` ↔ `File.Exists`) | 同上,阶段 2 | AC-44-D6 | `DelayTableBinder.cs:75-107`(三条拒收 + 聚合 `BakeValidationException`) |
| 音频素材 Addressables 组 | `AddressableAssetsData/AssetGroups/audio.asset`(新) | AC-44-D5 | `data-core.asset` |
| 组区分门 | `Editor.Tools.Gates/AudioAssetGroupGates.cs`(新) | AC-44-D5(AC 明写读 `AddressableAssetSettings`) | **实测全库零门读 `AddressableAssetSettings`** |
| `settings_visible` 注册表(数据) | `audio_events.json` 顶层新键 **或** 新注册文件 → ADR-014 烘焙 | 注册表 AC① / AC-44-13 | `audio_events.json` 的 `tier_map` 顶层键 |
| 注册表名闭集(含 `mono_enabled`) | `MixerRegistry.cs`(扩) | 注册表 AC① / AC-44-13 | `MixerRegistry.BusVolumeParameters` |
| `ISettingsStore` / `IResetMechanism` + sidecar | `Gameplay.Presentation/`(**禁 `Sim`**,GDD :1204) | AC-44-19 | `Gameplay.Input/BindingsStore.cs` |
| `MUSIC_XFADE_MIN_MS` 常量 | `AudioTuning.cs`(扩) | AC-44-18①③ | `AudioTuning.InspireFraction` |
| `xfade_ms ≥ MUSIC_XFADE_MIN_MS` 数值断言 | `AudioEventTableGates.ValidateXfade`(**现有具名空位**) | AC-44-18① | 同函数现存的存在性/类型两半 |
| 测量点注册表(AC-10-08) | **未找到任何落点** —— 须新建 | AC-44-E2 | 无 |
| DSP 预算探针(60s 窗 P95) | 性能测试基建 / `production/qa/evidence/` | AC-44-E2 | `DataBakeMenu.cs:642` 性能冒烟 |
| 世界呼吸行 bus 值规则 | `AudioEventTableGates.cs`(扩) | AC-44-16 的「构建失败」半边 | `ValidateRequiredCues` / `ValidateTierMap` |
| 听诊预载门 | `Gameplay.Presentation/Audio/` | 听诊窗预载 | `PlayerBusVolumeInitializer.cs` |

**⚠️ b5 门硬约束**(`AssemblyGates.cs:995-1009`):任何**含** `AudioCueDto` / `IAudioCueSink` /
`AudioCueHandle` / `TierSource` token 的新文件,**必须**声明
`namespace DaYiJingCheng.Gameplay.Presentation.Audio`。

---

## §5 可证伪验证命令(供复评轮复跑)

```bash
# ① 三条测试文件 using 挂空(import 44 命名空间却零引用其具体类型)
grep -c "Gameplay.Presentation.Audio" \
  unity/Assets/Tests/EditMode/Audio/{net_derivation,spatialization,loop_lifecycle}_test.cs
# 预期:每文件 1(仅 using 行),无类型使用

# ② story 声称的生产类型在生产树零命中
for t in SourcePool LatticeToWorld CellJitter RankKeyComputer OcclusionDriver \
         LoopWatchdog HandleManager HandoverFsm HandoverMux MusicXfadeGuard DspBudgetGuard; do
  printf "%-18s 生产树=%s\n" "$t" \
    "$(grep -rl "class $t\|struct $t" unity/Assets --include=*.cs 2>/dev/null | grep -vc /Tests/)"
done
# 预期:11 行全 = 0

# ③ 全库零生产 IAudioCueSink 实现
grep -rn ": IAudioCueSink" unity/Assets | grep -v /Tests/
# 预期:空输出

# ④ b5 零命中护栏 = WARN 非 FAIL
grep -n "b5-W" unity/Assets/Editor.Tools.Gates/AssemblyGates.cs
# 预期:1109-1114 行,warnings.Add(非 errs.Add)

# ⑤ 账本把 007-012 全标 BLOCKING
grep -c "BLOCKING" tests/unit/audio_system/README.md
# 预期:大量(含 007-012 各节)

# ⑥ story-009 无 AC-44 编号
grep -c -- "- \[ \] \*\*AC-44-" production/epics/audio-system/story-009-source-lifecycle-handover.md
# 预期:0(但 grep -c -- "- \[ \]" = 4)

# ⑦ 6 条自陈「生产代码尚未实现」
grep -rn "尚未实现,本 story 测试用自持谓词面" production/epics/audio-system/story-0{07,08,09,10,11,12}-*.md | wc -l
# 预期:6

# ⑧ xfade 具名空位
grep -n "不做.*MUSIC_XFADE_MIN_MS" unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs
# 预期:命中(XML doc)

# ⑨ 生产树无 audio 烘焙文件
grep -rln "audio" unity/Assets/Editor.Tools.Bake/ ; echo "exit=$?"
# 预期:空 + exit=1
```

---

## §6 处置(本件裁决)

1. **6 条 story 全部 `Complete` → `In Review`** —— 承 `case-system` 001–004 先例。
   `In Review` 是诚实状态:story 文件与测试在,但 AC 未被验证。
2. **实现另立 sprint** —— 用户 2026-10-16 裁定「先评审定案,实现另立 sprint」。
   本件**只落判定**,不排实现期。
3. **本件不修改** `production/epics/audio-system/EPIC.md` 的 6 行状态 —— EPIC 口径
   reconcile 留到实现轮收口时一并做(现在改会让 epic 表与 story 头短暂不一致)。
4. **本件不声称已复核 001–006 / 013–014** —— 抽样 ≠ 普查。

**评审人**:`claude`(agent 侧勘察 + 独立复核)
**复核方式**:两条逐条 AC 清单(007/008/009 一组 · 010/011/012 一组)由两个独立勘察员产出,
**关键事实由本会话独立复跑 §5 命令核实**(using 挂空 · 生产类零命中 · 账本 BLOCKING ·
6 条自陈 · xfade 空位 · 生产树实况),未仅凭勘察员自述。
