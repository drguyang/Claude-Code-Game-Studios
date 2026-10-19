# Story 018: 音频运行期装配(6 处缺口 A-1…A-6 · 从「库齐全」到「Play 里有声音」)

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(装配面;不新增音频语义)
> **Type**: Integration
> **Estimate**: 1.5 人日
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-18

## Context

**GDD**: `design/gdd/audio-system.md`(§一 44 只触发 / 只渲染 · §二 `AudioCueDto` 唯一入口 ·
§五 七总线拓扑 · AC-44-09 无提示音铁律)
**Requirement**: `TR-audio-011`(世界语境声源池)· `TR-audio-012`(EndLoop 自评兜底)·
`TR-audio-010`(素材绑定)的**运行期装配半边**
**ADR Governing Implementation**: ADR-018(音频架构)· ADR-028(声源池唯一所有者 = 44 · 零场景预摆)·
ADR-013 §9 C3(44 只渲染)· ADR-014 §五(Addressables 预载)· ADR-025 §①(装配清单 · b5 门)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 关键 API = `AudioSource.outputAudioMixerGroup`(`docs/engine-reference/unity/modules/audio.md:96`,
**运行期属性**,长期稳定)。⚠️ **不走 `.meta` 的 `m_audioMixerGroup`** —— 该字段不在
`docs/engine-reference/unity/` 覆盖面内,且 37 个 wav 的 `.meta` 实测零命中;引擎参考件
明写的是运行期赋值路径。Addressables 2.10.3 的 `LoadAssetAsync<AudioMixer>` 见 ADR-014 §五。

### 病灶(2026-10-17 勘察,`production/qa/evidence/audio-asset-gaps-2026-10-17.md` §2.2)

**素材不缺** —— 37 个 wav 全部在位、全部有真实波形、全部 44100 Hz 单声道 PCM。缺的是**装配**:

| # | 缺口 | 实测证据 | 后果 |
|---|---|---|---|
| **A-1** | `CueRenderSink` **零生产构造点** | 全仓 `new CueRenderSink(` 仅命中文档注释 `CueRenderSink.cs:51` | 没有任何东西把 cue 变成声音 |
| **A-2** | `ICueRenderTarget` **只有 `NullRenderTarget`** | `CueRenderSink.cs:130`;无接 `AudioSourcePool` 的实现 | 即便构造 sink 也是静默空转 |
| **A-3** | cue 源侧**零调用** | `PatientCueEmit.Emit` / `CoughChannel.Dispatch` / `CueEmitDeriver` 在 `Gameplay.Boot` 与病人生成路径零命中 | 病人不会咳、呼吸层不出声 |
| **A-4** | `AudioSourcePool` **零生产构造** | 全仓 `new AudioSourcePool(` 零命中(仅 `SpatializationDirector` 内部自建,而后者也零生产构造) | 世界声源池根本不生成 |
| **A-5** | `PlayerBusVolumeInitializer` **零构造** · `MixerResolver` **零赋值** | `PlayerBusVolumeInitializer.cs` 的 `MixerResolver` 仅声明 + 测试引用 | 音量设置不生效 |
| **A-6** | 37 个 wav `.meta` **零 `m_audioMixerGroup`** | `grep m_audioMixerGroup unity/Assets/Audio/*.meta` = 0 命中 | 全部走 Master 直通,七总线拓扑形同虚设 |
| **A-7(新)** | `DaYiJingCheng.mixer` **不在任何 Addressables 组** | `AddressableAssetsData/AssetGroups/{audio,data-core}.asset` 均无该 GUID | `MixerResolver` **根本解析不到**混音器 —— A-5 的前置阻塞 |

> **A-7 是本轮勘察新发现**,不在 `audio-asset-gaps-2026-10-17.md` 原表内(该件 §四 自陈未核
> Addressables 组内容)。不补 A-7,A-5 的修法无处落地。

### 边界裁定(2026-10-17 立卡轮定,不再逐条问用户)

1. **只交装配** —— 不改任何 cue 语义、不改事件表、不改白名单、不改 `AudioCueDto` 形状。
2. **A-6 走运行期**,不改 38 个 `.meta` —— 理由见 Engine Notes。总线归属真源 = 事件表
   `Bus` 列(`assets/data/audio_events.json`),**不得**在代码里另抄一份 bus 映射(ADR-024 §⑥)。
3. **`AudioSourcePool.CreateSource()` 是唯一挂点** —— 池是「唯一所有者 = 44」的承载物
   (ADR-028 ①),总线路由在**建源时**决定;换 bus 走**重挂**而非重建(池内复用语义不变)。
4. **`AddressablesDataProvider` 不得出现四个 token** —— `AudioCueDto` / `IAudioCueSink` /
   `AudioCueHandle` / `TierSource`;该文件不在 `.Audio` 命名空间下,b5 逃逸扫描会判红。
5. **`TD-audio-024` 并入本件** —— 处方音效须**验证不在**无提示音白名单(`AC-44-09` 半边)。
6. **`TR-audio-012`(EndLoop 兜底)不在本件裁决面** —— 承 ADR-028 Ordering Note,归 45 的
   GDD 轮 + ADR-001 窄修订。本件只保证 `EndLoop` **能被调用**(A-1/A-3 接线),不新增兜底机制。

## Tasks

### 步① 真渲染目标(关 A-2 / A-6)

- [x] **AC-018-1**: 新增 `AudioSourceRenderTarget : ICueRenderTarget`(`Gameplay.Presentation/Audio/`)——
      `PlayOneShot` → `pool.Spawn(...)` + 载入 clip + `Play`;**`StartLoop` / `StopLoop`** 走池的
      句柄语义(同源替换、陈旧句柄静默忽略,承 `CueRenderSink` 既有契约);
      **零新状态**(所有句柄簿记仍归 `CueRenderSink`)。
      *判别力*:让 `PlayOneShot` 变成空实现 ⇒ 「cue 触发后池内 ActiveCount 增」断言转红。

- [x] **AC-018-2**: **总线路由**(A-6)—— `AudioSourcePool` 在**建源**时按事件表 `Bus` 列设置
      `source.outputAudioMixerGroup`。① bus 名 → `AudioMixerGroup` 的查找**只经**
      `MixerRegistry.BusNames`(不另抄字面量);② 未知 bus ⇒ fail-loud(不静默落 Master);
      ③ 池 API 增 `Spawn(..., string busName)` 或等价,旧签名保留并默认 `"SFX"`(向后兼容既有测试)。
      *判别力*:把 `outputAudioMixerGroup` 赋值删掉 ⇒ 「bus 归属」断言转红。
      > **🔁 2026-10-18 评审批订正注(补评 S3-c)**:③ 的「默认 `"SFX"`」**未照字面实现** ——
      > 实作旧签名重载转调 `Spawn(..., bus: null)`(`AudioSourcePool.cs:99-100`),
      > null 语义 = **不挂组(落 Master 直通)**,在 `<remarks>` 有逐字声明;非功能缺陷,属卡面口径差。
      > 向后兼容目的达成(既有测试零改动编译通过);该 null 默认行为**零测覆盖**,随评审原件 S4 登记。

### 步② 组合根装配(关 A-1 / A-4)

- [x] **AC-018-3**: `CompositionRoot.Assemble` 构造并装配音频链:
      `AudioSourcePool` → `AudioSourceRenderTarget` → `CueRenderSink`,
      产物经 `CompositionRootServices` 只读属性暴露(如 `IAudioCueSink AudioSink`)。
      ① 装配失败(**任一步为 null / 抛**)⇒ 整袋失败(fail-loud,承「半装配袋不存在」口径);
      ② **既有测试不受影响** —— `Assemble(...)` 的无参重载在**无音频依赖**时仍可成功
      (音频链按 `IDataProvider` 是否提供事件表决定装配与否;未提供 ⇒ 暴露 `NullRenderTarget` 链
      并在诊断面可见,**不静默**)。③ `SpatializationDirector` 的 `AudioSourcePool` 自建路径
      **不得**与装配路径产生第二个池(单池不变量)。
      *判别力*:把音频装配段删掉 ⇒ 「`AudioSink` 非 null 且非空转」断言转红。

- [x] **AC-018-4**: **单池不变量** —— 运行期任意时刻 `AudioSourcePool` 的**根节点只有一个**。
      断言面:装配产物的池与 `SpatializationDirector`(若被构造)共用同一实例,或后者不被构造;
      **不得**出现两个 `"AudioSourcePool"` 根(承 ADR-028 ②「零场景预摆」的对称面)。
      *判别力*:让 `SpatializationDirector` 自建第二个池 ⇒ 断言转红。

### 步③ 启动序 + 总线音量(关 A-5 / A-7)

- [x] **AC-018-5**: `DaYiJingCheng.mixer` 登记进 Addressables(择一:并入 `audio` 组或新立
      `mixer` 组;建议**并入 `audio` 组**,减少组数),并让 `PlayerBusVolumeInitializer.MixerResolver`
      在生产路径**被赋值**(从 `IDataProvider` 或 Addressables 直取)。
      **`BootRoot` 启动序新增一步**(建议 ②.6,紧随 ②.4 的 settings provider 之后):
      解析 mixer → 赋值 resolver → 应用 `FactoryDefaults`。
      ① resolver 未解析 ⇒ **fail-loud 且诊断可读**(不静默跳过 —— 现行为是静默 `return`);
      ② `ValidateDefaults` 的既有断言不变。
      *判别力*:删掉 `MixerResolver` 赋值 ⇒ 「启动后 Master 音量 = 出厂默认」断言转红。

### 步④ cue 源接线(关 A-3)

- [x] **AC-018-6**: cue 源侧接上装配出的 `IAudioCueSink`:
      ① **咳嗽 / 语声** —— 病人 tick 路径调 `PatientCueEmit.Emit`(或 `CoughChannel.Dispatch`);
      ② **呼吸层** —— `BreathLayerDriver` / `CueEmitDeriver` 的 loop 起停接到 `BeginLoop` / `EndLoop`;
      ③ **接线点必须可被 EditMode 断言**(纯逻辑接缝,不依赖真实音频播放)。
      **Out of Scope 提示**:若 13 病人 AI 的 cue 调度(`PatientCueSchedule.Decide`)在生产路径
      **尚未有调用方**,则本步只接「调用方已存在」的链路,并在 `## Test Evidence` 如实登记
      哪条链**仍无上游驱动**(禁借绿 —— 不得把「接了一个没上游的调用」写成「病人会咳了」)。
      *判别力*:删掉 `Emit` 调用 ⇒ 「cue 触发计数」断言转红。

- [x] **AC-018-7**: **处方音效白名单验证**(`TD-audio-024`)—— 处方相关 cue
      (方笺翻页 `Page_Turn` / 出诊箱 `Cabinet_Prop` / 戥子相关若有)在音频事件表的
      `WhitelistCategory` 中**不属于**「行为反馈白名单」之外的状态播报类;
      断言 = 这些 cue 的类别 ∈ 允许集,且**零** sting / jingle 类。
      *判别力*:把某处方 cue 改成状态播报类 ⇒ 断言转红。

## Dependencies

- **Blocked-by**: story-010(烘焙管线 + cooked 表 + Addressables 音频组)✅ ·
  story-015 / 016(素材绑定 + 残余声源覆盖)✅ · story-017(门加固)✅
- **Enables**: `AC-11-21` 由「不可判」转可判(须重走)· `AC-44-09` 听测半边可执行 ·
  `TD-audio-023` 结清
- **不阻塞**: 真实素材内容替换(content 批 · `OQ-44-2`)· `TR-audio-012` 兜底(→ 45)

## Acceptance Criteria

- [x] `AC-018-1` … `AC-018-7` 全勾,过滤测试绿 + 变异恰红
- [x] 全量 EditMode + **PlayMode** 留痕,与基线对账**零回归**
      —— ⚠️ 口径:**「两棵树在本轮全部新增测上零回归」**,非无限定的「零回归」。
      EditMode 首跑另有 1 条既有间歇红(隔离复跑 5/5 绿、全量复跑零红),根因见 `TD-player-01`。
- [x] **实机听测**:Play 模式下至少一条 cue 可闻(须记录是哪条、走的哪条总线)
      —— ✅ **2026-10-18 用户实机听测并签核**:`Voice_Cough_Damp` **可闻**,走 **`Voice`** 总线,
      且拉/推 `DaYiJingCheng.mixer` 的 `Voice` 滑块**音量随之变化** —— 这是 A-6 总线路由的
      **人耳判据**(此前自动化只证 `outputAudioMixerGroup` 被赋值,证不了它在混音图上真生效)。
      ⚠️ 原「NOT-RUN」登记**保留在记录件 §一–§五**(执行前原文,不就地改写;现状读其 §〇/§六/§七)。
      **同日听测的连带判定(各自独立签,不互借)**:
      ① **零状态播报音**(`Page_Turn` / `Cabinet_Prop` 等处方/交互音效)⇒
      **`AC-11-21` 与 `AC-44-09` 听测半边同批转 ✅**(`design/gdd/prescription-and-medication.md:986`
      逐字「与 `AC-44-09` 联合」=
      同一条判据、同一次听测,**非**溢出;归 `story-005` / 签核件 / 债册处置)。
      ⚠️ `AC-44-09` 的 ✅ **不独力记在本批** —— 10-17 的 E5 走查已覆盖**全 15 cue**(probe 真播形态),
      本批只补「游戏内场景」形态 ⇒ 该 ✅ = **E5 + 本批合取**(见「听测签核轮记录」与记录件 §七);
      ② **呼吸层实机可闻** ⇒ 记录件 §四 的「可能仍不可闻」预判**被推翻**(口径错在拿 PlayMode
      **合成场景**的 `存活呼吸层=0` 外推真实会话;`BehaviorMap.TrendWired = false` 复核仍在,
      **`TD-audio-025` 处置不变** —— 该债记的是 `ComputeTrend` 占位,与呼吸层听感正交);
      ③ 协议第 4 项「无爆音/意外噪声」用户**未专门判定** ⇒ 如实登记「未判」,不勾不借
      (该项不在本 AC 判据文本内,不影响结案)。
      记录件:`production/qa/evidence/audio-wiring-listening-2026-10-18.md`(**已签核**)
- [x] 文档同批回刷:`docs/tech-debt-register.md` 的 `TD-audio-023` / `TD-audio-024` ·
      EPIC 行 · `sprint-status.yaml` · ADR-025 §① 清单(若 `Gameplay.Boot` 引用面变化)
- [x] **零 cue 语义改动 · 零事件表改动 · 零白名单改动 · 零 `.meta` 批量改动**
      (唯一数据改动 = `audio.asset` 登记 mixer,属 A-7 补口)

## Test Evidence

- **载体**:`unity/Assets/Tests/EditMode/Audio/runtime_assembly_test.cs`(**新建,27 测**)+
  `unity/Assets/Tests/PlayMode/audio_wiring_test.cs`(**新建,5 测**,实机播放半边)
- **证据原件**:`production/qa/evidence/impl-s18-runtime-audio-assembly-2026-10-18.md`
  (交付面 → AC 载体 → 证伪轮 → 回归对账 → NOT-RUN 登记,逐条可复算)
- **实机听测记录**:`production/qa/evidence/audio-wiring-listening-2026-10-18.md`(**✅ 2026-10-18 已执行并签核** —— 执行前的 NOT-RUN 原文保留在其 §一–§五,现状读 §〇/§六/§七)
- **首轮评审原件**:`production/qa/evidence/review-story-018-2026-10-18.md`(2026-10-18 **补评** —— 本 story 转 Complete **之后**追补,评的是 commit `60580ad` 时点代码,**不追认**原结案判定;详见 Completion Notes「Code Review」行)

### ⚠️ 哪条链仍无上游驱动(AC-018-6 的 Out-of-Scope 提示逐字要求的登记,2026-10-18 评审批补落 —— 原实现轮漏登 = 评审 BLOCKING B-1)

卡面 AC-018-6 ② 点名的 `BreathLayerDriver` / `CueEmitDeriver` **运行期仍零生产构造/调用方**:

- `new BreathLayerDriver(` 全仓唯一命中 = `Tests/EditMode/Audio/breath_layers_test.cs:897`(测试侧);
- `CueEmitDeriver` 生产侧**仅注释引用**,零调用点;
- 呼吸 loop 实际由**本 story 自建的 `PatientCueDriver`** 达成(`PatientCueDriver.cs:212` `_sink.BeginLoop`,
  `:161` 真调 `PatientCueEmit.Emit`)⇒ **「有声」成立,但走的是新写的驱动,不是卡面点名的那两个类**。
- **语声半边无载体**:`CompositionRoot.cs:342-344` 只解析 `CoughCueName` + `BreathBaseCueName`
  两条 ordinal,`vo_` 语声变体族**零解析、零发射方**;
  `AudioClipLibrary.cs:84` 对多素材行**恒取 `Assets[0]`**(确定性第 0 条,非变体选择),
  `VoiceVariantLib` 生产零调用 ⇒ `AC-44-02`「变体库混合」在装配后**仍空转**。

⇒ 以上均为**本 story 的 AC 未主张**的面(故不阻断结案),现逐条立债:**`TD-audio-028`**(空间化链未接)/
**`TD-audio-029`**(one-shot 不归还)/ **`TD-audio-030`**(变体混合未兑现)/
**`TD-audio-031`**(静态缝未回收 + 异常路径泄漏)/ **`TD-audio-032`**(常量/截断/行长/簿记清扫)/
**`TD-audio-033`**(`knowsClinic` 硬编码)。⚠️ **禁把本节的「有声」读成「链路闭环」**,亦**禁**据
`tr-registry.yaml` 的 `TR-diag-022 status: covered` 推定变体选择器已接(该 covered 是对**素材/库面**的主张)。

### 证伪轮(S12-4 · BLOCKING 门)—— `FALSIFY_EXIT=0`,14/14 转红

| 命中形态 | 停用的守卫 | 反证:哪条转红 |
|---|---|---|
| 定义域收窄 | `AudioClipLibrary.BusOf` 恒 null | `test_ac018_2_busName_sourceIsEventTableColumn_notCodeCopy` |
| 阈值边界 | `ApplyBus` 未装配 fail-loud 分支 | `test_ac018_2_spawnWithBus_withoutResolver_throws` |
| 一次性幂等 | `PlayOneShot` 变空实现 | `test_ac018_1_playOneShot_spawnsActiveSourceInPool`(+ `clipMiss` 同红) |
| 定义域收窄 | `AudioSourcePool.ApplyBus` 删 `outputAudioMixerGroup` 赋值 | `test_ac018_2_spawnWithBus_assignsMixerGroupFromRegistry`(impl 例号 **F2**,本表原漏此行;与首行是 bus 链上**两个不同变异** —— 首行断「bus 名来源不是代码抄写」,本行断「赋值这件事本身发生」) |
| 共存互斥 | 组合根渲染目标恒走 `NullRenderTarget` | `test_ac018_3_assemble_withSeams_rendersAndExposesDriver` |
| 共存互斥 | `BuildAudioSeams` 混音器缺失 fail-loud | `test_ac018_5_mixerUnresolved_failsLoudWithReadableDiagnostics` |
| 定义域收窄 | 删 `ApplyDefaults` 调用 **且** 移除 `BusVolumeErrors` | `test_ac018_5_realMixer_assignsResolverAndAppliesFactoryDefaults` |
| 一次性幂等 | `PatientCueDriver` 删 `Emit` 调用 | `test_ac018_6_emitBridge_hasProductionCaller_andFiresOnSchedule` |
| 定义域收窄 | 驱动「已建档」门 | `test_ac018_6_courseGate_excludesNotYetRegisteredPatient` |
| 阈值边界 | 处方 cue 改状态播报类 + 门停用 | `test_ac018_7_prescriptionCues_categoryInWhitelist_andNoStateAnnouncement`(连带 6) |
| 共存互斥 | 启动序 `audio: null` | `test_ac018_3_bootAssemblesRenderChain_andSinglePoolRoot`(连带 2) |
| 一次性幂等 | `Update()` 不驱动 `OnTickEdge` | `test_ac018_6_patientCueDriver_runsOnRealTickPump` |
| 定义域收窄 | `AudioClipLibrary.Preload` 变空实现 | `test_ac018_2_clipsPreloaded_noMissingCues`(连带 1) |
| 阈值边界 | `TrendWired` 门撤除(上游修复的反证) | `test_ac018_6_patientCueDriver_runsOnRealTickPump` |

> **首轮唯一假绿 F4 已修** —— `AudioRenderAssembled` 标志原在别处**重算**了一遍构造条件
> (同一判断的第二份拷贝 ⇒ 两个可分叉真源),变异只改三元时不转红。
> 改由**已构造出的对象**派生(`renderTarget is AudioSourceRenderTarget`)后复跑转红。
> **可观测标志必须从被观测对象派生,不得复述构造条件。**

### 回归对账(等式判据)

| 树 | 基线 | 本轮 | 等式 |
|---|---|---|---|
| EditMode | `s53` 3682/3637/0/45 | `s18-full-editmode-2` **3709/3664/0/45** | `+27 = +27` ✅ |
| PlayMode | `s51` 192/192/0/0 | `s18-full-playmode` **197/197/0/0** | `+5 = +5` ✅ |

> ⚠️ **口径**:准确表述是「**两棵树在本轮全部新增测上零回归**」。
> EditMode **首跑**(`s18-full-editmode`)另有 1 条既有间歇红
> (`MovementFeedTest.test_move_afterFreeFall_...`),隔离复跑 5/5 绿、全量复跑零红
> ⇒ 环境相关;**其根因已定位为 `AC-1-33` ③ 未落地的下游症状**(见 `TD-player-01`),
> **非本件引入**。

## Out of Scope

- 真实素材内容替换 / Import Settings 定稿(`OQ-44-2` → content 批)
- `TR-audio-012` 的 EndLoop 网络兜底(→ 45 GDD 轮 + ADR-001 窄修订)
- 听诊预载门的 PlayMode 真 `AudioClip.loadState` 半边(承 story-010 裁定 ③ 的 NOT-RUN 哨兵)
- 空间化遮挡的**数值**调参(`ADR-028 ⑥` 归用户)
- VR 音频(ADR-018 §七,推 P1a)

## 评审轮记录

**2026-10-18 · 首轮 `/code-review`(转 Complete 后追补)** —— 三代理并行(A unity-specialist /
B lead-programmer / C qa-tester),原件 `production/qa/evidence/review-story-018-2026-10-18.md`。
- **Verdict**:CHANGES REQUIRED → 修复后 **APPROVED WITH DEBTS(TD-audio-028…033)**。
- **BLOCKING 1 项(B-1,账面)**:本区上方 Test Evidence 的「哪条链仍无上游驱动」登记**从未落**
  (卡面 AC-018-6 逐字要求)⇒ **本批已补落**;证伪表补 F2 行(13→14 与表头对齐);
  AC-018-2 ③「默认 `"SFX"`」订正注(实作 `bus: null`)。
- **代码面**:七缺口 A-1…A-7 确认全闭、未发现假绿(可观测标志全部由被观测对象派生)、
  HC-1…HC-8 全 PASS;新缺陷 N-1…N-10 逐条对码复核属实 ⇒ **六条新债立案**,本批**零代码改动**
  (已 Complete 的 story 不因补评重开;N-6 两行加固是否顺手做待用户裁)。
- ⚠️ **补评性质**:本评审评的是 `60580ad` 时点代码,**不追认**原结案判定,也**不撤销** `Complete`。

## 实现轮记录(2026-10-18)

**状态:代码与自动化判据全部落地并自证;唯一未闭项 = 实机听测 AC(不可自动判定)⇒ 本 story 留 `In Progress`,等用户听测签核。**

- **交付**:7 处缺口 A-1…A-7 逐条有生产落点(6 新生产件 + 6 改生产件 + 2 新测试件)。
- **证伪轮**:14/14 转红(`FALSIFY_EXIT=0`);首轮 F4 假绿已修(标志改由被观测对象派生)。
- **回归**:EditMode `3709/3664/0/45`(基线 3682 ⇒ `+27` 恰等于新增测数)·
  PlayMode `197/197/0/0`(基线 192 ⇒ `+5`)⇒ 新增测上**零回归**。
- **上游修复(用户「本轮就修」授权)**:13 的痊愈锁存停用(`BehaviorMap.TrendWired = false`)——
  9 侧 `ComputeTrend` 恒 0 的占位使「新病人首拍即判痊愈」,呼吸层与一次性 cue 永不发射。
  两侧已下 NOT-RUN 哨兵(接线后转红);反证 F11 坐实哨兵有判别力。新债 **`TD-audio-025`**。
- **本轮新发现(非本件引入,已立债)**:
  **`TD-player-01`** = `AC-1-33` ③(`minMoveDistance` P0 = 0)语义层未被验证 ——
  测试断言的是夹具字段而非控制器真值,全库零处把该值写进 `CharacterController`;
  §回归 那条间歇红即其下游症状。
  **`TD-audio-026`** = `CompositionRoot.Assemble()` 无条件构造 `AudioSourcePool`(含 GameObject 根),
  而装配袋无 `IDisposable`、EditMode 测试无 TearDown ⇒ 泄漏(生产侧 `BootRoot.OnDestroy` 有 `Dispose`,不受影响)。
- **证据原件**:`production/qa/evidence/impl-s18-runtime-audio-assembly-2026-10-18.md`
- **听测记录**:`production/qa/evidence/audio-wiring-listening-2026-10-18.md`(**NOT-RUN**)
- ⚠️ **未 commit**(无用户指令)。

## 听测签核轮记录(2026-10-18 · 本 story 结案批)

**状态:实机听测已由用户执行并签核 ⇒ 唯一余项达成 ⇒ 本 story 转 `Complete`。**

- **判定(cue / 总线 / 可闻 / 滑块,四值齐)**:`Voice_Cough_Damp`(事件表第 0 行)·
  **`Voice`** 总线 · **可闻** · **`Voice` 滑块拉动即音量变化** ⇒ **A-6 总线路由在人耳层面首次坐实**
  (自动化半边此前已绿:证的是 `outputAudioMixerGroup` **被赋值**;本次补的是它**在混音图上生效**)。
- **签核**:听测执行 + 音频 lead 两栏由 **`dr_guyang`** 同签(本项目惯例允许兼任);
  ⚠️ **不声称**已获独立第二人耳复核(承 10-17 三签同款声明)。
- **连带结案两条外部 AC**(各自独立签,与本案不互借):
  ① **`AC-11-21`**(BLOCKING [L])由 **⚫ 不可判 → ✅ 通过** —— 听测半边 = 「处方/交互音效零状态播报」,
  实机**未听到任何**给药 jingle / 生效提示 / sting / ducking;自动化半边 = 本 story `AC-018-7`
  三条断言(实现轮已绿)。⇒ `TD-prescription-04` 结案。
  ② **`AC-44-09` 听测半边**(BLOCKING)同步转 ✅ —— 与 ① 是**同一条判据、同一次听测**
  (`design/gdd/prescription-and-medication.md:986` 逐字「与 `AC-44-09` 联合」),
  **不是**一条通过导致另一条。⚠️ **归属须收窄**:该 AC 的听测半边**并非本批首次兑现** ——
  10-17 的 E5 走查(`docs/audio/statereport-walkthrough-2026-10-17.md`,story-016 `AC-44-E5`)已按
  **全 15 cue** 逐条人耳判定(经 probe 真播,当时运行期装配尚不存在)⇒
  **`AC-44-09` 的 ✅ = E5(全表 probe 形态)+ 本批(游戏内场景形态)两段证据的合取**,
  本 story **不主张独力结清**;E5 的签核载体与 story-016 卡面自述存在账面矛盾,归 story-016
  的 reconcile 轮处置(本批只登记,不顺手改)。详 `audio-wiring-listening-2026-10-18.md`
  §七「与 E5 的关系」· `impl-s18-*` §九附。
  ⚠️ **实听覆盖面(禁读成「给药音效已听测」)**:当前事件表 **无任何给药/疗效 cue 条目**,
  实听对象 = `Page_Turn` / `Cabinet_Prop` 两条交互声 ⇒ 「给药 jingle」子面**当时无物可听**。
  判据仍成立的两条支撑:`AC-018-7` 白名单门覆盖面 = **全表**且构建期硬拒(新增播报类 cue 当场拦)·
  11 侧**零独立 `AudioCue`**(无绕过事件表的发声路径)。详 `audio-wiring-listening-2026-10-18.md`
  §〇「实听覆盖面」。
- **实机推翻记录件 §四 的一处预判**(如实登记,不藏):听测件预判「呼吸层本次可能仍不可闻」,
  **实机为可闻**。根因是**口径错误** —— 该预判拿 PlayMode **合成场景**的 `存活呼吸层=0`
  外推真实会话,而合成病人只注册一次性 cue 路径。与 `BehaviorMap.TrendWired = false`
  (复核仍在)**不矛盾**:锁存停用后 `BreathShouldLive` 不再被误判掐死 ⇒ 真实会话可起 loop。
  ⚠️ **`TD-audio-025` 不因此结案、处置不变**(该债记 `ComputeTrend` 恒 0 占位,须 9 侧 F2 中心差分;
  `behavior_map_test` 的 `IsFalse(TrendWired)` 哨兵仍红仍有效,接线后须改回真断言,禁删)。
- **未判项**:听测协议第 4 项「无意外噪声 / 爆音 / 静音」用户未给单项结论 ⇒ 登记**未判**,不勾不借
  (不在本 AC 判据文本内,不影响结案;若后续素材批撞出爆音,归 `OQ-44-2` Import Settings 面)。
- **本 story 不主张的范围**(禁溢出):
  **44 epic 仍不得收口** —— 债册 `TD-audio-100…109`(AC-44-10 完全未映射 + 9 组 `[A]` 顶 `[L]` 的其余
  听测/人工/联机/VR 半边)**逐条未动**,本批只动 `AC-44-09` 的听测半边与运行期链路这一事实。
- **听测牵出的资产副作用与复跑核验**(`impl-s18-*` §十):听测在编辑器 Mixer 窗口做 ⇒
  `DaYiJingCheng.mixer` 的 **`Default` 快照新增捕获 `Master` 原始电平 = 0 dB**(键 `4b26ff…` = `:551`)。
  值 = 出厂默认 ⇒ **当前运行期等价**,且**提交前复跑核验**:`MixerTopology` EditMode **21/21** ·
  `AudioWiringTest` PlayMode **5/5**。⚠️ 但它**不是任何 AC 的证据**;顺带暴露
  **AC② 门(快照 ∩ 玩家 exposed = ∅)对「快照捕获 `Master` 原始电平」判绿通过**
  (Master 原始电平不在 exposed 参数名集内)⇒ **新立 `TD-audio-027`**,本批不改门(归 003 复评口径)。

## Completion Notes

**Completed**: 2026-10-18
**Criteria**: 7/7 `AC-018-1…7`(自动化)+ 实机听测半边 ✅(真人签核)⇒ **全数达成**;
协议第 4 项「无爆音」登记**未判**(非判据文本项)。
**Test Evidence**: Integration —— 真装配件 `unity/Assets/Tests/EditMode/Audio/runtime_assembly_test.cs`
(27 测)+ `unity/Assets/Tests/PlayMode/audio_wiring_test.cs`(5 测);证据原件
`impl-s18-runtime-audio-assembly-2026-10-18.md`(证伪 14/14 转红)+ 人耳件
`audio-wiring-listening-2026-10-18.md`(已签核)。
**Code Review**: ~~**未跑 `/code-review`**~~ ⇒ **🔁 2026-10-18 同日补评落原件** ——
用户裁定「补跑 code review」⇒ `review-story-018-2026-10-18.md`(三代理并行,Verdict =
APPROVED WITH DEBTS;修复轮结账表 Phase 8b 双列见其 §五)。⚠️ **如实留痕(两条都不删)**:
① 结案**当时**的依据 = **AC 全部达成 + 证伪轮 14/14**,不 = 「已代码评审」——
结案先于评审是本仓的真实时序,补评**不追认**原结案判定(承 `review-workflow.md`「评审不可事后追补」,
本件评的是补评时点即 `60580ad` 的代码);② 结案时「未含评审」的原裁定原文保留于
听测签核轮记录与 impl §九,不改写。
**Deviations**: ① 立卡时列 6 处缺口,实做 **7 处**(A-7 为勘察新发现,已在卡面登记);
② 上游修复 `BehaviorMap.TrendWired` 门(用户「本轮就修」授权)超出原卡面边界,已立 `TD-audio-025`;
③ 听测件 §四 的一处预判被实机推翻,已就地加订正注(见上)。
**Untested criteria**: 无(AC 面全覆盖);`AC-11-21` / `AC-44-09` 的**其余** `[L]` 子项不在本 story 界内。
