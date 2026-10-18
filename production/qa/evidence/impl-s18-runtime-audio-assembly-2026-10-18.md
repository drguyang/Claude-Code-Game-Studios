# story-018 音频运行期装配 —— 实现证据原件

> **对象**:`production/epics/audio-system/story-018-runtime-audio-assembly.md`
> **AC**:`AC-018-1` … `AC-018-7`(7 条)
> **日期**:2026-10-18
> **来源判定**:`docs/tech-debt-register.md` 2026-10-17 段 —— **`TD-audio-023`**(音频链零运行期装配
> 6 处 A-1…A-6)· **`TD-audio-024`**(处方音效白名单验证);勘察件
> `production/qa/evidence/audio-asset-gaps-2026-10-17.md` §2.2;立卡轮新发现 **A-7**。
> **本件性质**:实现轮证据(**交付面 → AC 载体 → 证伪轮 → 回归对账 → 如实登记缺口**),
> 逐条可复算。
> **⚠️ 状态口径**:story-018 **本轮不转 `Complete`** —— 其 AC 含「实机听测」一条,
> **可闻与否不可自动判定**(用户 2026-10-18 裁定「你实机听测后再签」)。
> 本件如实登记该条为 **NOT-RUN**,**不借绿**。

> **🔁 2026-10-18 听测批订正(上方口径已被同日听测取代,现状读 §九)** ——
> 「不转 Complete」是**实现轮当时**的正确结论(听测未跑,禁借绿),原文保留。
> 同日用户实机听测并签核 ⇒ **唯一余项达成** ⇒ story-018 转 **`Complete`**。

---

## §一 交付面 —— 缺口 A-1…A-7 逐条落点

| # | 缺口(原判定) | 落点(文件) | 关法 |
|---|---|---|---|
| **A-1** | `CueRenderSink` 零生产构造点 | `Gameplay.Boot/CompositionRoot.cs:329`(`new CueRenderSink(renderTarget)`)→ 经 `CompositionRootServices.AudioSink` 暴露 | 生产构造点从 0 → 1 |
| **A-2** | `ICueRenderTarget` 只有 `NullRenderTarget` | **新建** `Gameplay.Presentation/Audio/AudioSourceRenderTarget.cs`(`PlayOneShot` / `StartLoop` / `StopLoop` 全走池句柄语义) | 真渲染目标落地 |
| **A-3** | cue 源侧零调用 | **新建** `Gameplay.Boot/PatientCueDriver.cs`(`tick 边沿 → 13 调度 → PatientCueEmit.Emit` / `BeginLoop` / `EndLoop`);`BootRoot.Update()` 驱动 | 调用方从 0 → 1 |
| **A-4** | `AudioSourcePool` 零生产构造 | `CompositionRoot.cs:321`(`new AudioSourcePool(busResolver: audio?.Buses)`)→ 经 `CompositionRootServices.AudioPool` 暴露 | 生产构造点从 0 → 1 |
| **A-5** | `PlayerBusVolumeInitializer` 零构造 · `MixerResolver` 零赋值 | `Gameplay.Boot/BootRoot.cs::BuildAudioSeams`(解析 mixer → `ApplyDefaults`)→ 结果经 **`AudioAssemblySeams.BusVolumeErrors`** 带出 | 赋值 + 应用均落地 |
| **A-6** | 37 个 wav `.meta` 零 `m_audioMixerGroup` | **改判走运行期**(承立卡轮边界裁定 ②):`AudioSourcePool.ApplyBus` 在建源时设 `source.outputAudioMixerGroup`;bus 名**唯一真源** = 事件表 `Bus` 列(经 `AudioClipLibrary.BusOf`),**代码内零第二份 bus 映射** | 总线路由生效 |
| **A-7(新)** | `DaYiJingCheng.mixer` 不在任何 Addressables 组 | `unity/Assets/AddressableAssetsData/AssetGroups/audio.asset` 登记 mixer | `MixerResolver` 可解析 |

**新建文件(6 生产 + 2 测试)**

| 文件 | 作用 |
|---|---|
| `Gameplay.Presentation/Audio/AudioSourceRenderTarget.cs` | `ICueRenderTarget` 真实现(A-2) |
| `Gameplay.Presentation/Audio/AudioBusResolver.cs` | bus 名 → `AudioMixerGroup`(**只经 `MixerRegistry.BusNames`**,不另抄字面量) |
| `Gameplay.Presentation/Audio/AudioClipLibrary.cs` | cue ordinal → `AudioClip` + `BusOf`(总线真源 = 事件表列) |
| `Gameplay.Boot/AudioAssemblySeams.cs` | 装配缝载体(mixer / clips / table / `BusVolumeErrors`) |
| `Gameplay.Boot/PatientCueDriver.cs` | A-3 的调用方(tick 边沿驱动) |
| `Tests/EditMode/Audio/runtime_assembly_test.cs` | 27 测(EditMode 半边) |
| `Tests/PlayMode/audio_wiring_test.cs` | 5 测(实机半边) |

**修改文件(生产 6 + 测试 2 + 数据 1)**

`CompositionRoot.cs` · `CompositionRootServices.cs` · `BootRoot.cs` ·
`AudioSourcePool.cs`(建源挂 bus + 未装配 fail-loud)· `SpatializationDirector.cs`(共享池重载)·
`AudioEventDataSet.cs`(cue 名常量)· `PatientAI/PresenceRegistry.cs`(`TryGetCell` 只读格查询)·
`Tests/EditMode/PatientAI/behavior_map_test.cs`(NOT-RUN 哨兵,见 §五)·
`AddressableAssetsData/AssetGroups/audio.asset`(A-7)。

> **`PresenceRegistry.TryGetCell` 为何必须新增**:`AudioCueDto.Cell` 要的是病人的**逻辑格**,
> 而登记簿此前只暴露「是否在场」。返回的是**整数格**,不是表现态位置(ADR-016 §三 读方口径)。
> **只读,不改任何状态。**

---

## §二 AC ↔ 测试载体映射(27 EditMode + 5 PlayMode = 32)

| AC | EditMode 载体(`runtime_assembly_test.cs`) | PlayMode 载体(`audio_wiring_test.cs`) |
|---|---|---|
| **AC-018-1** 真渲染目标 | `playOneShot_spawnsActiveSourceInPool` · `playOneShot_clipMiss_doesNotSpawn_andIsObservable` · `startLoop_stopLoop_poolLifecycleFollowsHandles` · `stopLoop_staleHandle_isNoOp`(4) | — |
| **AC-018-2** 总线路由 | `spawnWithBus_assignsMixerGroupFromRegistry` · `spawnWithBus_withoutResolver_throws` · `unknownBus_isRejectedByResolver` · `busName_sourceIsEventTableColumn_notCodeCopy`(4) | `clipsPreloaded_noMissingCues` |
| **AC-018-3** 组合根装配 | `assemble_withoutSeams_nullRenderTargetChain_butObservable` · `assemble_withSeams_rendersAndExposesDriver` · `assemble_secondCall_producesIndependentPool`(3) | `bootAssemblesRenderChain_andSinglePoolRoot` |
| **AC-018-4** 单池不变量 | `assemble_createsExactlyOnePoolRoot` · `spatializationDirector_sharedPool_keepsSingleRoot`(2) | — |
| **AC-018-5** 启动序 + 总线音量 | `mixerUnresolved_failsLoudWithReadableDiagnostics` · `noProvider_returnsNullSeam_notThrow` · `realMixer_assignsResolverAndAppliesFactoryDefaults`(3) | `mixerResolved_andMasterVolumeIsFactoryDefault` |
| **AC-018-6** cue 源接线 | `emitBridge_hasProductionCaller_andFiresOnSchedule` · `emitInterval_isRespected_betweenFires` · `courseGate_excludesNotYetRegisteredPatient` · `nullCoughOrdinal_breathStillStarts_emitStaysZero` · `pruneAbsent_endsBreathLoopAndClearsBookkeeping` · `stopAll_endsEveryLoop` · `driver_ctor_rejectsBrokenIntervals` · `driver_rejectsNegativeTick`(8) | `patientCueDriver_runsOnRealTickPump` · `cueReachesRenderTarget_poolBecomesActive` |
| **AC-018-7** 处方音效白名单 | `prescriptionCues_categoryInWhitelist_andNoStateAnnouncement` · `wholeTable_noForbiddenTriggerSource` · `prescriptionCues_areNotLoops`(3) | — |

**实机半边实测取证(PlayMode 日志)**

```
[AudioWiring] cue 驱动:末驱动 tick=40 · 一次性发射=1 · 存活呼吸层=0 · 在场=1
[AudioWiring] cue#0 → 池活动源 0→1,bus=Voice
```

—— 病人**真的咳了一声**(端到端走到引擎对象),总线归属取自事件表 `Bus` 列。

---

## §三 证伪轮(S12-4 Falsifiability Gate · BLOCKING)

**纪律**:每例**改 → 跑 → 还原在同一命令内**完成(`/tmp/fz-s18.py`);`finally` 还原后逐文件
**SHA-256 逐字节校验**(不符即打印告警)。全部 14 例的还原均通过校验;
工作树复验:`assets/data/audio_events.json` / `AudioEventTableGates.cs` 已回到**零 diff**。

**判据**:每例的期望测试**必须出现在失败集里**;仍绿 ⇒ 该夹具弱判别面 = **BLOCKING**。

| 例 | 停用的守卫 / 变异 | 期望红 | 实测 |
|---|---|---|---|
| **F1** | `AudioSourceRenderTarget.PlayOneShot` 变空实现 | `ac018_1_playOneShot_spawnsActiveSourceInPool` | ✅ 红(连带 `clipMiss` 同红) |
| **F2** | `AudioSourcePool.ApplyBus` 删 `outputAudioMixerGroup` 赋值 | `ac018_2_spawnWithBus_assignsMixerGroupFromRegistry` | ✅ 红 |
| **F3** | `ApplyBus` 未装配 fail-loud 分支停用 | `ac018_2_spawnWithBus_withoutResolver_throws` | ✅ 红 |
| **F4** | 组合根渲染目标选择恒走 `NullRenderTarget` 链 | `ac018_3_assemble_withSeams_rendersAndExposesDriver` | ✅ 红(**首轮假绿,已修 — 见下**) |
| **F5** | `BuildAudioSeams` 混音器缺失 fail-loud 停用 | `ac018_5_mixerUnresolved_failsLoudWithReadableDiagnostics` | ✅ 红 |
| **F6** | 删 `ApplyDefaults` 调用 **且** 移除其可观测面 | `ac018_5_realMixer_assignsResolverAndAppliesFactoryDefaults` | ✅ 红 |
| **F7** | `PatientCueDriver` 删 `PatientCueEmit.Emit` 调用 | `ac018_6_emitBridge_hasProductionCaller_andFiresOnSchedule` | ✅ 红 |
| **F8** | 驱动「已建档」门停用 | `ac018_6_courseGate_excludesNotYetRegisteredPatient` | ✅ 红 |
| **F9** | 处方 cue 改状态播报类 + 构建期门停用 | `ac018_7_prescriptionCues_categoryInWhitelist_andNoStateAnnouncement` | ✅ 红(连带 6 条) |
| **F10** | `AudioClipLibrary.BusOf` 恒返回 null | `ac018_2_busName_sourceIsEventTableColumn_notCodeCopy` | ✅ 红 |
| **P1** | 启动序不把装配缝交给组合根(`audio: null`) | `ac018_3_bootAssemblesRenderChain_andSinglePoolRoot` | ✅ 红(连带 2) |
| **P3** | `Update()` 不驱动 `PatientCueDriver.OnTickEdge` | `ac018_6_patientCueDriver_runsOnRealTickPump` | ✅ 红 |
| **P4** | `AudioClipLibrary.Preload` 变空实现 | `ac018_2_clipsPreloaded_noMissingCues` | ✅ 红(连带 1) |
| **F11** | `TrendWired` 门撤除(痊愈锁存回到恒真形态) | `ac018_6_patientCueDriver_runsOnRealTickPump` | ✅ 红 |

**终值:`FALSIFY_EXIT=0` —— 14/14 全红。**

### F4 —— 首轮唯一假绿及其修法(本件的核心教训)

**首轮实测**:把 `renderTarget` 三元逼成恒 `NullRenderTarget` ⇒
`ac018_3_assemble_withSeams_rendersAndExposesDriver` **仍全绿**(`total=27 passed=27`)。

**根因**:`CompositionRootServices.AudioRenderAssembled` 标志**在别处被独立重算了一遍**
(`audio != null && audio.HasAny`)。变异只改了三元,**没改那处重算** ⇒ 标志照旧 `true` ⇒
断言看不见变异。**同一判断的第二份拷贝 = 两个可分叉的真源**(ADR-024 §⑥ 的同型病灶)。

**修法(单一出处)**:标志改由**已构造出的那个对象**派生 ——

```csharp
renderTarget is AudioSourceRenderTarget,   // 不再重算 audio != null && audio.HasAny
```

复跑 F4 ⇒ ✅ 转红。**教训**:可观测标志必须**从被观测的对象派生**,不得复述构造条件。

---

## §四 回归对账(等式判据,禁借绿)

> 判据写成等式:`本轮 total − 基线 total = 本轮新增测数`;两棵树分别对账。

| 轮次 | 结果件 | total | passed | failed | skipped |
|---|---|---|---|---|---|
| EditMode 基线 | `unity/Logs/s53-full-editmode.xml` | 3682 | 3637 | 0 | 45 |
| EditMode 首跑 | `unity/Logs/s18-full-editmode.xml` | 3709 | 3663 | **1** | 45 |
| EditMode **复跑** | `unity/Logs/s18-full-editmode-2.xml` | **3709** | **3664** | **0** | 45 |
| PlayMode 基线 | `unity/Logs/s51-playmode.xml` | 192 | 192 | 0 | 0 |
| PlayMode | `unity/Logs/s18-full-playmode.xml` | **197** | **197** | **0** | 0 |

- **EditMode**:`3709 − 3682 = +27` **恰等于** `runtime_assembly_test.cs` 的测数(27);
  复跑 `passed 3664 − 3637 = +27` ⇒ **零回归**。
- **PlayMode**:`197 − 192 = +5` **恰等于** `audio_wiring_test.cs` 的测数(5);
  `passed +5` ⇒ **零回归**。

### 首跑那 1 红的定性(附:它牵出一条未落地的 AC)

**红者**:`DaYiJingCheng.Tests.Boot.MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig`
(`movement_feed_test.cs:324`)。报文:`Expected: 0.00097358244238421321d But was: 0.0d`。

**三步定性**:

1. **隔离复跑**该测类 ⇒ **5/5 全绿**;
2. **全量复跑**同一棵树 ⇒ **3709/3664/0**,该测绿。⇒ **间歇性坐实**,非本件引入。
3. **根因可复算**:该断言的前提是 `expectedDx > CharacterController.minMoveDistance`。
   首跑实测 `dt = 0.0099` ⇒ `vTarget = 2.5`、`expectedSpeed = min(2.5, 10×0.0099) = 0.099`、
   **`expectedDx = 0.00098`**。而 `minMoveDistance` 的真实值是 Unity 默认 **0.001**
   (见下)**⇒ `0.00098 < 0.001` ⇒ 位移被引擎丢弃 ⇒ `dx` 恰为 `0.0`**。

**为什么真实值是 0.001 而不是 0** —— 这是本轮**新发现的既有缺口**,详见 §六 ①。

**口径**:本件**不主张**「零回归」这句无限定语。准确表述是:
**两棵树在本轮全部新增测上零回归;首跑另有 1 条既有间歇红,已定性为环境相关 + 未落地 AC 的下游症状。**

---

## §五 上游修复 —— 13 的痊愈锁存停用(`TrendWired` 门)

**授权**:用户 2026-10-18 裁定 **「本轮就修」**(最小改动:让 `EvaluateTerminal` 在 trend 未接时
不据 trend 判痊愈)。**改的是 13/9 的跨系统语义,已获明确授权。**

**病灶**:`Sim/ProgressionEvaluator.cs:439` 的 `ComputeTrend` **恒返回 `Fix.Zero`**
(该文件头明写「保留占位(tripwire,归 M2 可玩级)· **不得当成本批缺陷删改**」)。
而 `BehaviorMap.EvaluateTerminal` 的痊愈判据是 `position ≤ RecoverMax ∧ trend ≤ 0` ——
**trend 恒 0 ⇒ `trend ≤ 0` 恒真** ⇒ **每一个新出现的病人(position ≈ 0)在首拍即被锁存「痊愈」**
⇒ `PatientCueSchedule.BreathShouldLive` 恒 false ⇒ **呼吸层永不启动、一次性 cue 永不发射**。
实机取证(修复前):`tick=2` 起 `term=True emit=False breath=None`。

**修法**:新增 `BehaviorMap.TrendWired = false` 常量,痊愈半边门控于它;
**死亡锁存不受影响**(只依赖 position)。取「**停用**」而非「误判」——
「已痊愈」在 GDD §States 一-ter 里是**终局**,一旦锁存不可逆;误判会把「刚发病」写成「已痊愈」。

**两侧 NOT-RUN 哨兵**(接线后转红,届时须改回真断言,**禁删哨兵**):

- `Tests/EditMode/PatientAI/behavior_map_test.cs::test_ac13b1_terminalLatch_recoversDoNotResurrect`
  ⇒ 断言 `IsFalse(BehaviorMap.TrendWired)` + 「trend 未接线 ⇒ 不得据 position 低判痊愈」。
- `Tests/PlayMode/audio_wiring_test.cs::test_ac018_6_patientCueDriver_runsOnRealTickPump`
  ⇒ 断言 `driver.LastDrivenTick > 0` 与「在场 > 0 ⇒ 呼吸层或一次性 cue 必须非 0」。

**反证**:F11(把 `TrendWired` 改 `true`)⇒ `test_ac018_6_patientCueDriver_runsOnRealTickPump` **红** ✅
—— 证明该哨兵**真的有判别力**,不是摆设。

**修复后实测**:`末驱动 tick=40 · 一次性发射=1 · 存活呼吸层=0 · 在场=1`。

> ⚠️ **本修复是「停用」不是「接线」** —— `ComputeTrend` 仍是占位。
> 真正的还债 = 9 侧实现 F2 中心差分(归 M2 可玩级 / 数值轮),届时把 `TrendWired` 改 `true`。
> 已登记为 **`TD-audio-025`**。

---

## §六 本轮新发现(均**非**本件引入 · 已立债)

### ① `AC-1-33` ③ 语义层未被验证(高价值 —— 它解释了 §四 那条间歇红)

**GDD 要求**(`docs/architecture/tr-registry.yaml:1618` 逐字):**`minMoveDistance` P0 = 0**,
理由 =「非零会吞掉亚阈值位移 ⇒ 手感粘滞 + 与 F-1-1a 的逐帧预算不一致」。

**实测**:

- 该值只在 `Gameplay.Presentation/Player/LocomotionEvaluator.cs:34` **声明**(`MinMoveDistance = 0f`);
- **全库零处**把它写进 `CharacterController`(`grep -rni minmovedistance` 在生产侧只命中声明与字段)
  ⇒ 控制器取 Unity 默认 **0.001**;
- `Tests/EditMode/PlayerController/locomotion_chain_test.cs:200::test_ac133_minMoveDistanceIsZero`
  读的是 **`LocomotionConfig.LoadDefault().MinMoveDistance`(夹具字段)**,
  **不是**控制器的真实值;**全库零测**断言控制器的真实 `minMoveDistance`。

⇒ **AC-1-33 ③ 断言的是它自己的夹具** —— 正是本仓反复出现的「自造断言顶替 AC 语义层」病灶。
**GDD 早已逐字预言了这条后果**,而 §四 那条间歇红正是它的**下游症状**。

**归属**:系统 1(输入与设备)· **不在 story-018 边界内**。已登记为 **`TD-player-01`**。

### ② `CompositionRoot.Assemble()` 无条件构造 `AudioSourcePool`

**实测**:`CompositionRoot.cs:321` 的 `new AudioSourcePool(busResolver: audio?.Buses)` **无条件执行**
(即便 `audio == null`),而 `AudioSourcePool` 的构造函数**会建一个 GameObject 根**
(`AudioSourcePool.cs:65`,`hideFlags = DontSave`)。

**后果**:

- **生产侧无影响** —— `BootRoot.OnDestroy` 已调 `AudioPool.Dispose()`。
- **EditMode 测试侧泄漏** —— `CompositionRootServices` **无 `IDisposable`**、既有 Boot 测试
  **无 TearDown**(`grep` 30 处 `Assemble()` 调用),每次装配遗留一个池根节点。

**是否该改**:「无音频也建池」在 **AC-018-4 单池不变量**下是**可辩护的**(池是 `AudioSourcePool`
唯一所有者的承载物,ADR-028 ①,且 `AudioRenderAssembled` 才是「装了没装」的判据);
但**泄漏**是实打实的。**是否改为惰性构造须用户裁**。已登记为 **`TD-audio-026`**。

---

## §七 如实登记的 NOT-RUN(禁借绿)

> **✅ 2026-10-18 听测批**:本表 9 行里**只有第 1 行解除**(实机听测已签核);
> **第 2–9 行全部仍 NOT-RUN** —— 本批未动其中任何一条。
> ⚠️ **story-018 转 `Complete` 的依据 = 该 story 的 7 条 AC + 听测 AC 全达成**,
> **不等于**本表其余缺口已闭(它们各自归 `TD-audio-025` / content 批 / 13 数据轮 / CI 矩阵)。

| # | 项 | 为什么 NOT-RUN | 哨兵 / 载体 |
|---|---|---|---|
| 1 | **实机听测「至少一条 cue 可闻」** | **可闻与否不可自动判定**(听感) | 待用户实机听测后签;PlayMode 已证「cue 走到引擎对象 + 总线正确」<br>✅ **2026-10-18 已解除** —— 用户实机签核,详见 §九 |
| 2 | 真 `AudioClip.loadState` 听诊预载 PlayMode 半边 | 承 story-010 裁定 ③ | 缺口哨兵已钉 |
| 3 | `CueKindOf(p, signs)` 未实现 | 固定 `CueKind.Cough` | 归 13/8 后续轮 |
| 4 | 事件表 `row.Assets[0]` 确定性取用 | 非变体选择 | 变体库归 content 批 |
| 5 | `BehaviorBands.Default` 在用 | `assets/data/ai_patient.json` **不存在** | 归 13 的数据轮 |
| 6 | `knowsClinic: false` 硬编码 | 无 `KnowsClinic(p)` 生产实现 | 归 13 |
| 7 | EditMode 下 clip 不装载 | 实测 `LoadedCount == 0`(Addressables 不在 EditMode 装载) | `test_ac018_3_...` 内 NOT-RUN 哨兵,值非 0 ⇒ 转真断言 |
| 8 | IL2CPP 三格矩阵(ADR-012) | 本机**仅 Mono** | 归 CI runner |
| 9 | **痊愈锁存停用**(`TrendWired == false` 期间) | 见 §五;9 侧 `ComputeTrend` 仍占位 | `TD-audio-025` —— ⚠️ **仍 NOT-RUN**(本批未动);同日听测**可闻呼吸层**与其**不矛盾**(锁存停用正是 `BreathShouldLive` 不再被误判掐死的原因),见 §九 |

---

## §八 判据与结论

| 判据 | 结果 |
|---|---|
| 缺口 A-1…A-7 逐条有生产落点 | ✅ §一 |
| AC-018-1…7 各有自动化载体 | ✅ §二(32 测) |
| **证伪轮 14/14 转红**(S12-4 BLOCKING 门) | ✅ `FALSIFY_EXIT=0` |
| EditMode 回归等式成立 | ✅ `+27 = +27`,复跑零红 |
| PlayMode 回归等式成立 | ✅ `+5 = +5`,零红 |
| 零 cue 语义改动 · 零事件表改动 · 零白名单改动 · 零 `.meta` 批量改动 | ✅(唯一数据改动 = `audio.asset` 登记 mixer,属 A-7) |
| 实机听测 | ~~⛔ **NOT-RUN** —— 待用户签~~ ⇒ ✅ **2026-10-18 已执行并签核**,见 §九 |

**结论**:story-018 的**代码与自动化判据全部落地并自证**;
~~**唯一未闭项 = 实机听测 AC**(不可自动判定)。
⇒ **本轮不转 `Complete`**,story 留 **In Progress**,等用户听测签核。~~
⇒ **🔁 2026-10-18 同日听测批:唯一余项达成 ⇒ story-018 转 `Complete`**(§九)。
**不借绿**:实现轮落盘时本件不主张「story-018 已完成」——该主张**只在听测签核后成立**,
故原文保留,改判以时间戳标注。

**复算命令**

```bash
# 证伪轮(改 → 跑 → 还原 → SHA-256 校验,同一命令内)
python3 /tmp/fz-s18.py                # 期望末行 FALSIFY_EXIT=0 (14/14 例转红)
# 全量回归
unity test unity --mode EditMode  --output unity/Logs/s18-full-editmode-2.xml
unity test unity --mode PlayMode  --output unity/Logs/s18-full-playmode.xml
```

---

## §九 2026-10-18 实机听测批 —— 唯一余项达成(story-018 转 `Complete`)

**件**:`production/qa/evidence/audio-wiring-listening-2026-10-18.md`(其 §〇 结果摘要 / §三 勾满 /
§六 双栏签核 / §七 结案联动 / §四 订正注)。**本批零代码改动、零新测**(纯人耳签核 + 账面回填)。

| 判据 | 结果 | 对账 |
|---|---|---|
| **至少一条 cue 实机可闻**(story-018 听测 AC) | **✅ `Voice_Cough_Damp` 可闻,走 `Voice` 总线**(记录四值齐) | 与 §二 自动化半边同向(自动化证「走到引擎对象」,本批证「耳朵听见」) |
| **A-6 总线路由在人耳层面生效** | **✅ 拉/推 `Voice` 滑块,音量随之变化** | 这是 §二 明写「证不了」的那一层 —— 本批补上;若滑块无变化即落 §五 失败分支(未触发) |
| **零状态播报音**(`AC-11-21` + `AC-44-09` 听测半边,联合) | **✅** | ⚠️ 覆盖面须钉:**当前事件表无给药 cue 条目**,实听对象 = `Page_Turn` / `Cabinet_Prop`;判据成立靠**全表构建期白名单门**(`AC-018-7`,覆盖面不依赖逐条实听)+ **11 侧零独立 `AudioCue`**。详听测件 §〇「实听覆盖面」。⚠️ **`AC-44-09` 的 ✅ 归属 = 「10-17 E5 走查 + 本批」合取,本行不主张独力结清** —— 详见本节下 **§九附**(该附节为本行的归属收窄说明,须与本行同读) |
| **呼吸层** | **⚠️ 可闻 ⇒ 听测件 §四 的预判被推翻** | 口径错因:拿 PlayMode **合成场景** `存活呼吸层=0`(合成病人只注册一次性 cue)外推真实会话。与 `TrendWired=false` 不矛盾,`TD-audio-025` 处置不变 |
| 无爆音 / 意外噪声 | **未判**(用户未给单项结论) | 不在 story-018 听测 AC 判据文本内 ⇒ 登记未判,**不勾不借** |

**结案与不结案的边界(禁溢出)**:

| 结案 | 不结案(本批未动) |
|---|---|
| `TD-audio-023`(音频链零运行期装配)⇒ **✅ 结案**(代码侧 10-18 + 听测侧 10-18 双半齐) | `TD-audio-025`(`ComputeTrend` 占位)⇒ **仍 open** |
| `TD-audio-024`(处方音效白名单验证)⇒ **✅ 结案**(`AC-018-7` 三断言 + 实听零播报) | `TD-audio-026`(EditMode 池泄漏)⇒ **仍待用户裁** · **`TD-audio-027`(AC② 门盲区,本件 §十 新立)⇒ 本批不结案** |
| `TD-prescription-04`(音频链未通 ⇒ `AC-11-21` 不可判)⇒ **✅ 结案** | `TD-audio-100…109`(44 epic 其余 9 组 `[A]` 顶 `[L]` + `AC-44-10` 未映射)⇒ **全部仍 open** |
| `AC-11-21` ⇒ **⚫ → ✅** | **44 epic 不得收口**(DoD「36 条 AC 全部验证」残余面未清) |
| story-018 ⇒ **`Complete`** · S14-10 ⇒ **`done`** | `TD-player-01`(间歇红根因)⇒ **仍归系统 1 实现轮** |

### §九附 · 顺带发现的一处 E5 账面矛盾(10-17 批遗留,本批**不代裁**)

回填覆盖面时扫到:story-016 `:83` 声明「**AC-44-E5 走查完成并签字**」(commit `443a413`,15/15
判「不是状态播报」,签字 [x]),但其入库原件 `docs/audio/statereport-walkthrough-2026-10-17.md`
的签字表**复选框是空的**(`[ ] 通过`),且该文件自带判据注逐字写着「**⚠️ 未勾选 = E5 未通过**」。
机器半(谓词 `status_words` 全 null + 程序拒收逻辑)支持「已判」,**人半的签署载体与声明不同步**。
⇒ 与本件 §九 腿② **同题但相互独立**(都落在「零状态播报」判据面上):E5 签字行若需补勾,
**归 story-016 账面清理**,本批不动它、也不借它作本件证据。
⚠️ **由此须收窄本批 §九 对 `AC-44-09` 的归属表述**:`AC-44-09` 的听测半边**并非本批首次兑现** ——
10-17 的 E5 走查已按**全 15 cue** 逐条人耳判定(覆盖面完整,含处方两条),本批的真实增量是
**「游戏内真实场景」这一形态的听测**(证的是**装配后**链路上的播报行为 ——
E5 走查经 probe 逐条真播,当时**运行期装配还不存在**)。⇒ `AC-44-09` 的 ✅ =
**E5(全表逐条 probe 真播,签核载体待对账)+ 本批(游戏内场景)** 两段证据的合取,
本批**不主张独力结清**;`TD-audio-024` 的结案同理(其白名单验证 = `AC-018-7` 门 + 本批游戏内实听,
E5 走查在前)。
另:工作树存在两份**逐字节相同**的 10-18 副本(`statereport-walkthrough-2026-10-18.md` + manifest,
时间戳晚于 10-17 原件),内容与已声明签字的 10-17 版无差 ⇒ 本批未采用、未改动,处置归用户。
> 🔴 **2026-10-18 刷账订正(S15-7):上一段的「已删除」为误记,实测未删除** ——
> `docs/audio/statereport-walkthrough-2026-10-18.md` + 同名 manifest **仍在盘上**,且与 10-17 入库原件
> **逐字节相同**(md5 `1ad6ba93…` / `152ea3b0…`),并已随 S15-5(`910aa79`)**入库**。
> 成因 = probe 的 `RecordFileName()` / `ManifestFileName()` 按**当天日期**命名(`statereport_walkthrough_probe.cs:601-603`)
> 且 `WriteOnce` 对**当天**生成的文件不覆盖 ⇒ 跨天重跑必然产出同内容副本。
> **副本本体无新信息**(判定内容与 10-17 完全相同),但「用户裁定删除」这一处置声明与事实不符,故就地订正;副本去留**交回用户裁**。
> 该矛盾本体(E5 签字行与 story-016 卡面自述相抵)**不因副本在否而消解**,已由 S15-7 的 story-016 reconcile 专节处置。

**未跑 `/code-review`(如实登记)**:本 story 转 Complete 的依据 = **AC 全达成 + 证伪轮 14/14**;
用户本批裁定不含评审。若后续补评审,须落 `review-*.md` 原件,且**不追认**本次结案
(承 `.claude/docs/review-workflow.md`「评审不可事后追补」)。

## §十 听测批后的资产副作用与复跑核验(2026-10-18)

听测在**编辑器 Play** 里做(拉/推 `Voice` 滑块),因而 `unity/Assets/Audio/DaYiJingCheng.mixer`
出现一处**非本批意图**的改动,提交前如实登记:

```diff
   m_Name: Default
-  m_FloatValues: {}
+  m_FloatValues:
+    4b26ff2484ad0dd3dbba5a2756453a2e: 0
```

- **键 `4b26ff…` = `Master` 组的 `m_Volume` 哈希**(实测 `:551`,该 doc 的 `m_Name: Master`)
  ⇒ 语义 = **「Default」快照现在开始显式捕获 Master 总线电平,值 0 dB**。
- **成因**(编辑器行为,非代码路径):在 Mixer 窗口选中 `Default` 快照时动过 Master 电位器,
  Unity 把当时值写进该快照。值 = 出厂 0 dB ⇒ **当前运行期等价**(无脚本写 Master 原始电平;
  玩家音量走 `bus_volume_master` 子组,非 Master 本身)。
- **⚠️ 由此暴露 AC② 门的一处真实盲区(新债 `TD-audio-027`)**:
  `MixerTopologyGates.ValidateSnapshotExposedDisjoint` 把捕获键经 `m_Volume` 哈希反查成**组名**,
  再与**暴露参数名集**(`bus_volume_*` / `tier_*`)求交。`Master` 组的原始电平**不在暴露名集里**
  ⇒ **快照捕获 Master 电平天然判「绿」**,门挡不住这类改动。失效形态是**将来式**:
  若日后有人把 Master 原始电平调成全局 headroom(如 −3 dB),`Default` 快照会在每次切入时
  **静默打回 0** —— 属本项目明令警惕的**静默**类失败。
- **提交前的复跑核验**(batch,Unity 编辑器已关、无 `UnityLockfile`;改后资产为被测对象):

| 命令 | 输出件 | 结果 |
|---|---|---|
| `unity test unity --mode EditMode --filter "MixerTopology"` | `unity/Logs/s18-listening-mixer-editmode.xml` | **21 / 21 通过**(含 `test_realMixerAsset_*` 总门 + AC② 非空转守卫 `CountResolvedCaptureKeys > 0`) |
| `unity test unity --mode PlayMode --filter "DaYiJingCheng.Tests.PlayMode.AudioWiringTest"` | `unity/Logs/s18-listening-playmode2.xml` | **5 / 5 通过**(含 `test_ac018_5_mixerResolved_andMasterVolumeIsFactoryDefault` —— 该测断言的是 **7 个玩家参数** = 出厂默认,不覆盖 Master 原始电平,故改动不使其红) |

> ⚠️ 首跑 PlayMode 用了**类名短式**过滤器(`--filter "audio_wiring_test"`)⇒ `total=0` 空跑,
> 非红非绿。过滤器须写**命名空间限定名**(先例 `review-phase3-vertical-slice-2026-10-08.md:54`)。
> 空跑不作证据,已用限定名重跑取上表值。

**处置**:该改动**随本批提交**(值 = 出厂默认、两组判据复跑全绿、且它是听测这一轮的**真实产物**),
但它**不是任何 AC 的证据** ⇒ 只登记为副作用 + 盲区债 `TD-audio-027`,**不得**被读成
「快照捕获已验证」。若要把资产恢复原状,出路是 `git revert`/手删那两行并**重跑上表两条命令**,
**不是**把它当噪声静默丢弃。


---

## §十一 2026-10-18 补评轮(首轮 `/code-review` 落账 · **零代码改动**)

> **性质**:本 story 已于同日听测批转 `Complete`;本节记的是**转 Complete 之后追补**的首轮评审。
> 原件 `production/qa/evidence/review-story-018-2026-10-18.md`(三代理并行:A unity-specialist /
> B lead-programmer / C qa-tester;Phase 7b ③ 增量落盘,三草稿均非空)。

### ① 评审确认的本件自述(不复算,采原件)

- **A-1…A-7 七缺口按卡面确实全闭**;**未发现「自造断言顶替 AC 语义层」型假绿** ——
  C 侧逐条实证可观测标志全部由**被观测对象**派生:`AudioRenderAssembled` ←
  `renderTarget is AudioSourceRenderTarget`(`CompositionRoot.cs:440`,F4 修法在码)·
  `LastDrivenTick` ← `PatientCueDriver.cs:101/:113` 自写 · `ActiveCount` ← 池 `_entries[].Active` ·
  `BusVolumeErrors` ← `BootRoot.cs:534` `ApplyDefaults` 真实返回值。
- 硬约束 HC-1…HC-8 全 PASS(b5 逃逸段 / 门 A / ADR-028 ①②③⑤ / 无提示音铁律 / 命名避让 /
  Unity 6.3 API 面 / TD-audio-026 与 -027 现状与债册逐字一致)。HC-3 的 **④ 为部分** → 派生 N-1。
- ⚠️ **未跑任何测试**(3709/197 与 §十 的 21/21、5/5 均采本件自述,评审侧未复算)—— 禁读成评审重跑。

### ② 一条 BLOCKING(B-1,账面)及其修复

**AC-018-6 的 Out-of-Scope 提示逐字要求「在 `## Test Evidence` 如实登记哪条链仍无上游驱动」,
而实现轮从未落该登记。** 实测(三位 + 编排方独立对码,复核属实):

| 点名的类 | 生产侧现状 |
|---|---|
| `BreathLayerDriver` | `new BreathLayerDriver(` 全仓**唯一命中** = `Tests/EditMode/Audio/breath_layers_test.cs:897` |
| `CueEmitDeriver` | 生产侧**仅注释引用**,零调用点 |
| 呼吸 loop 实际承载 | 本 story 自建的 `PatientCueDriver`(`:212` `BeginLoop` · `:161` 真调 `PatientCueEmit.Emit`) |
| `vo_` 语声变体族 | **零解析、零发射方**(`CompositionRoot.cs:342-344` 只解析咳嗽 + 呼吸基础两条 ordinal) |

⇒ **修复 = 该登记已补落**到 story 卡面 `## Test Evidence`(新增小节「⚠️ 哪条链仍无上游驱动」)。
⚠️ 结论文本须写成「**有声成立,但走的是新写的驱动,不是卡面点名的那两个类**」——
不得读成「链路闭环」。

### ③ 同批落地的另两处账面修复

- **证伪表补 F2 行**:卡面表头称 `14/14`,**表体仅 13 行**(缺
  `AudioSourcePool.ApplyBus` 删 `outputAudioMixerGroup` 赋值 ⇒ `ac018_2_spawnWithBus_…`)。
  该例**本件 §三 `:93` 一直有**(当时实跑转红)⇒ 属**卡面誊写漏行**,非证据缺失;
  补行后表体 14 行与表头对齐。
- **AC-018-2 ③ 订正注**:卡面 `:69` 写「旧签名保留并默认 `"SFX"`」,实作转调
  `bus: null`(`AudioSourcePool.cs:99-100`;null = 不挂组落 Master 直通,`<remarks>` 有逐字声明)。
  向后兼容目的达成,属**卡面口径差**;该 null 默认行为**零测覆盖**,记 S4。

### ④ 新缺陷十条 → 六条新债(本批**不改代码**,story 已 Complete)

| 债 | 内容 | 关键实测 |
|---|---|---|
| **TD-audio-028** | **空间化链整体未接入生产** | `AudioSourceRenderTarget.cs:77-82` 不写 position、不 pin 三件套(`CreateSource` 注释「pin 归装配层」`:210` 无人 pin);`SpatializationDirector` **生产零构造**(全仓唯一命中 = 其自例注释 `:23`);`LatticeSize` 死字段。与听测「可闻」**不矛盾**(2D 也可闻),但 ADR-028 的位置/遮挡语义**今日无承载** |
| **TD-audio-029** | **one-shot 池条目永不归还** | `PlayOneShot` 无 Stop/Release 对偶 ⇒ `_free` 恒空、`ActiveCount` 含静音源失真;规模有界(≤CAP 24)**非泄漏**,但 ADR-028 ⑤「cue 驱动生命周期」对 one-shot **只起不收** |
| **TD-audio-030** | **语声变体混合运行期未兑现** | `AudioClipLibrary.cs:84` 恒 `Assets[0]`;`VoiceVariantLib` 生产零调用。⚠️ **账面矛盾**:`tr-registry.yaml` **`TR-diag-022 status: covered`** 而运行期选择器不存在 ⇒ 债行明写「covered 只主张素材/库面」 |
| **TD-audio-031** | 静态缝未回收 + 异常路径泄漏 | `BootRoot.cs:541` `MixerResolver` / `:479` `AudioSettingsRegistry.Provider` 赋值后 `OnDestroy`(`:709-738` 处置五对象)**不置 null**(测试侧 `runtime_assembly_test.cs:440/:469` 自复位成对照);`BuildAudioSeams` `:520-546` `Preload()` 成功后抛 ⇒ 已载 clip 无 Dispose。⚠️ **细化**:`EditorSettings.asset:27-28` 域重载**开着** ⇒ 静态每 Play 复位,**失效形态是条件性**(仅该设置被关时咬人)⇒ 加固项而非缺陷 |
| **TD-audio-032** | 常量/截断/行长/簿记清扫(合并四条低-微) | `CueIntervals.Default` / `BehaviorBands.Default` 代码常量入生产(未走 ADR-014)· `(int)tick` 截断 `:132/:143/:165` · `OnTickEdge` ≈57 行(`:109-168`)+ `BuildAudioSeams` ≈47 行超 40 限且**无夹具钉界** · `PruneAbsent` `:174-176` 只跟 `_breathHandles` 键清三本簿记 + 每边沿 `new List<int>` |
| **TD-audio-033** | `knowsClinic: false` 硬编码入生产驱动 | `PatientCueDriver.cs:125`;F-13.2 真值源(13 的 HomeRegion)未作注入缝 ⇒ 病人永不进 `Seeking`,且与视觉侧私有 `PatientBehaviorDirector` 各持输入**可分叉**(归 13 语义/数据轮) |

另:评审 S3-b 的症状(`test_ac018_4_assemble_createsExactlyOnePoolRoot` 的绿**依赖他夹具清池根**)
已就地加注到 **TD-audio-026** 行(根因即该债的 30 处 `Assemble()` 无 TearDown)。

### ⑤ 未及核查项(禁借绿)

评审侧未跑测试 · `runtime_assembly_test.cs` 27 测**未逐条读完**(C 抽查非普查)·
`CueRenderSink` 既有代码未逐行读 · `audio.asset` GUID 逐项比对未做 ·
`.mixer` diff 采 TD-audio-027 原文未独立开文件复核。

**复算命令(评审修复轮的 Landed 判据)**

```bash
grep -n "无上游驱动" production/epics/audio-system/story-018-runtime-audio-assembly.md   # 非空 = R-B1 已落
grep -c "^| " production/epics/audio-system/story-018-runtime-audio-assembly.md          # 证伪表体行数
grep -n "TD-audio-03[0-3]" docs/tech-debt-register.md                                     # 六债在案(028/029 同查)
```
