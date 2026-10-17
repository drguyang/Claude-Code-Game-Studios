# Impl Evidence — story-016 · R-2 归属闭合门(E1 / E3 / E4 · 三处结合)

> **日期**:2026-10-17
> **Story**:`production/epics/audio-system/story-016-audio-residual-source-coverage.md`
> **Story Type**:Integration
> **判据形态**:原判定 → 落点 → 验证命令(逐条可复算)

---

## 一、立项前提已过期(实测订正)

卡面 `Status: Blocked` 与「⚠️ 本 story 的存在理由」写着「事件表内尚无对应条目」「7 项无承接件」。
**2026-10-17 实测,该前提不再成立**:

| AC | 卡面断言 | 实测 |
|---|---|---|
| E1 9 项各有条目 | 事件表内尚无对应条目 | **9 项全部有 cue**(见 §二 常量表) |
| E2 素材存在 | 前置未解除 | ✅ 37 个 `.wav` 已在 `unity/Assets/Audio/` |
| E4 三方差集为零 | 已订正为「按 9 个文件比」 | 事件表 15 行 `assets[]` 覆盖全部 36 个需求文件 |

⇒ 本 story 的**实际剩余工作 = 写门**,不是补条目。

> ⚠️ **本件不改写卡面历史**:原「存在理由」一节保留,仅追加订正注。

---

## 二、三处结合(用户 2026-10-17 裁定「ABC 结合起来读」+「红,三处都要改」)

| 处 | 落点 | 职责 |
|---|---|---|
| **A(锁)** | `AudioEventTableGates.RequiredRequirements` + `RequiredRequirementIds` + `WorldSoundCues` | 9 项的身份在代码里具名;**改数据件即便同步改了文档,仍红** |
| **C(值)** | `assets/data/audio_requirements.json`(新) | 唯一机器可读输入;`schema_version` / 未知键白名单 / `world_cue_split` |
| **B(出处)** | `AudioRequirementsBake.ValidateManifestAgrees` → `design/assets/specs/audio-material-manifest.md` | 权威件 ↔ 常量 逐项对账;**文档滞后也红** |

**链**:B 的表格 → C 的数据件 → A 的常量断言。三者任一不合 ⇒ 构建期 `throw`。

### 9 项恒定(冻结)

| id | 项 | cue | 文件数 |
|---|---|---|---|
| SFX-1 | 诊脉接触音 | `Pulse_Contact` | 1 |
| SFX-2 | 听诊器接触音 | `Steth_Contact` | 2 |
| SFX-3 | 病人呼吸两层 | `BreathLayer_Base_Calm` | 2 |
| SFX-6 | 病人语声 | `Voice_Cough_Damp` | 12 |
| SFX-7 | 纸面物理声 | `Page_Turn` | 2 |
| SFX-8 | 脚步声 | `Footstep_Base` | 4 |
| SFX-9 | 药柜/器物声 | `Cabinet_Prop` | 3 |
| SFX-10 | 世界声(9 项拆 4 cue) | `World_Threat` / `_Opportunity` / `_Reaction` / `_Disaster` | 9 |
| AMB-1 | 医馆室内环境音 | `Clinic_Room_Tone` | 1 |

合计 **36 个不可重复文件**。素材库 37 个,多出的 `music_encounter_layer_loop.wav` 逐项登记在
`AudioEventTableGates.LibraryUnregisteredExempt`(它属 `Music_Encounter_Layer`,不属 R-2 九项)。

---

## 三、实现过程中实测发现并修正的三件事

### 3.1 我最初把 SFX-6 的 12 个文件名编错了

首版按 `_m1_1` … `_m1_6` 补造,磁盘上实际是 `{m,f}{1,2,3}_{1,2}`。**已按
`audio-material-manifest.md` §二 的冻结命名更正**,并逐文件核过盘。

### 3.2 权威件缺两族的逐字冻结文件名(B 支首跑即报)

B 支首跑报了 **8 条真实差异**:呼吸两层(SFX-3)与语声(SFX-6)在 manifest 里**没有逐字冻结文件名**
(二者不在「余 7 项」表内,由 story-015 交付)。已在 manifest 补两张「机读索引行」表
(§二 语声、§三 呼吸)。

⇒ **这不是为了让测试变绿而改文档** —— 是权威件本来就该有、且 B 支的职责就是把它逼出来。

### 3.3 manifest 散文里的「退役旧名」被误判为需求文件

首版把「现 `vo_..._m1.wav` / `_f1.wav` 退役」里的反引号值当成文件名,劈出 `_f1.wav` / `.wav` 碎片。
两处都修:**解析器**只收形如 `^[A-Za-z]…\.wav$` 的值;同时把该句改写为点名完整文件名
(正文本来也更清楚)。

---

## 四、证伪轮(BLOCKING · S12-4)

驱动 `/tmp/fz-s16.py`:**改 → 跑 → 还原在同一命令内**,`finally` 做 SHA-256 逐字节校验。

| ID | 变异 | 结果 | 判定 |
|---|---|---|---|
| **F1** | 停用总门里的 A 锁 | **首轮:87 条零红** | 🔴 **假绿** |
| **F1′** | (补 2 条入口测试后)同上 | **恰红 2 条**:`gateEntry_pointNamesALockWhenOnlyLockCanCatch` / `gateEntry_pointRejectsExtraRequirement` | ✅ |
| F2 | 停用 E1 cue 覆盖 | 恰红 1 条:`eventTableMissingCue_reportsThatRequirement` | ✅ |
| F3 | 停用 E4 需求文件在库 | 恰红 1 条:`requiredFileAbsent_rejected` | ✅ |
| F4 | 停用 E4 无归属检查 | 恰红 1 条:`libraryExtraUnregisteredFile_rejected` | ✅ |
| F5 | 停用 B 对账 | 恰红 1 条:`manifestMissingFile_rejected` | ✅ |

### ⚠️ F1 的假绿为什么值得记

A 锁原有两个夹具,但**都绕开了总门入口**:
- `dataTableCueRenamed_lockedByConstant` 直调内层 `ValidateRequirementTableMatches`;
- 覆盖类夹具直调 `ValidateEventTableCoverage`。

⇒ 「门入口是否真的接上 A 锁」**没有任何夹具**。修法 = 新造两条**只有 A 锁能报**的用例
(数据件与事件表两侧同时改名 / 数据件多出未登记项,经 `ValidateAtAssetRoot` 驱动)。
**这条假绿若不跑证伪轮,会以绿留下且什么都没测。**

---

## 五、同批修正:文件名与重载歧义

| 问题 | 表现 | 修法 |
|---|---|---|
| `using System.Linq` 缺失 | `AudioEventTableGates` 里 `Except`/`First`/`SelectMany` 编译失败 | 补 `using` |
| 重载歧义 | `AudioEventTableGates.Validate(...)` 与 `AudioRequirementsBake.Validate(...)` 在测试装配内同名冲突(CS1501) | 后者改名 `ValidateAtAssetRoot` |
| 纪律门命中 | E3 夹具用了 `Directory.CreateDirectory`,被 `test_discipline_noFilesystemWrites` 当场拦下 | 改为指向**不存在的**素材根(等价空库,零副作用) |

---

## 六、验证命令与结果

```bash
# 过滤
unity test unity --mode EditMode --filter PipelineGateTest --output unity/Logs/s27-pipe.xml
# → 89 / 89 / 0 / 0

# 全量回归
unity test unity --mode EditMode --output unity/Logs/s28-full.xml
# → total 3650 · passed 3605 · failed 0 · inconclusive 0 · skipped 45
```

**对账(逐 fullname)**:`s28-full.xml` vs 前次绿 `s23g-full.xml`
- 新增 **15** / 删除 **0** ⇒ `3650 − 3635 = 15` **成立**
- 新增明细:U1 `test_u1teardown_*` **3** · story-016 `test_e1_*` **7** + `test_e3_*` 1 + `test_e4_*` 2 =
  **10** · 入口补口 **2**

---

## 七、AC 结账(双列)

| AC | Landed | Proven | Proven-by |
|---|---|---|---|
| **E1** 9 项逐项有条目 | ✅ | ✅ | `test_e1_realRequirements_gatePassesWithNoError` + 反证 F1′/F2 |
| **E2** 9 项 `.wav` 真实存在 | ✅ | ✅ | `test_e1_realRequirements_gatePassesWithNoError`(跑**真素材**,非夹具) |
| **E3** 删被引 wav ⇒ 构建期 `throw` | ✅ | ✅ | `test_e3_missingRequiredFile_throwsAtBakeTime`(断言 `BakeValidationException` 且点名文件) |
| **E4** 双向集合差归零 | ✅ | ✅ | `test_e1_realRequirements_*` + 反证 F3 / F4 |
| **E5** 无提示音铁律 | ⚠️ **判定器已落;走查未执行** | ✗ | **见下 + §十** |

### ⚠️ E5 —— 如实登记,不标已证

E5 要求「新增 9 项条目不得用于状态播报」。**本轮未为它新增任何断言** —— 理由是它**已被既有门覆盖**:

- `AudioEventTableGates.ValidateTriggerSources`(`ForbiddenTriggerSources` = `VitalsCrossed` /
  `ThresholdCrossed` / `OutcomeResolved`)对**全部行**生效,不分新增或既有;
- 新增行须落在 `WhitelistCategories` 闭集内(同样全表生效)。

⇒ **结构面已闭合**,但「语义面(某条 cue 是否实质在报状态)」**写不出自动夹据**。
本件**不声称 E5 已被独立证明**,只声称其**结构面**由既有门覆盖。

> **2026-10-17 第二次处理(用户裁定 = PlayMode 手跑走查)**:见 §十。结论不变 —— **E5 仍 ✗**。

---

## 八、NOT-RUN / 未做(不掩盖)

| 项 | 说明 |
|---|---|
| **cooked 产物** | `audio_requirements.json` **刻意不产出 `*.cooked`** —— 它是**构建期门**的输入,运行期无消费者。**凭空造一个没有消费者的编码格式 = 第二真源**(承 `AudioSettingsRegistryBake` 头注的同一条纪律) |
| **PlayMode 全量** | 本件未跑;最近一次全量 `189/189/0/0`(2026-10-17 重建 catalog 后) |
| **E5 语义面 · 走查执行** | 见 §七 + §十 —— 判定器已落,走查未跑 |

---

## 九、改动清单

| 文件 | 性质 |
|---|---|
| `assets/data/audio_requirements.json` | **新建**(C 值) |
| `unity/Assets/Editor.Tools.Bake/AudioRequirementsBake.cs` | **新建**(装载 + 绑定 + B 对账),含 `.meta` |
| `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs` | 改:`RequiredRequirements` 常量族 + `ValidateRequirements` + 三个类型 |
| `unity/Assets/Editor.Tools.Spike/U1SpikeSetup.cs` | 改:`RestorePlayModeCore` 抽出(属 `TD-audio-01f` 残留②,不属本 story 的 AC) |
| `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs` | 改:74 → **89** 测 |
| `design/assets/specs/audio-material-manifest.md` | 改:补两张机读索引行表 + 一处退役句改写 |
| `docs/tech-debt-register.md` | 改:`TD-audio-01f` 残留② 结清登记 |

---

## 十、E5 第二次处理:PlayMode 手跑走查(2026-10-17,用户裁定)

### 10.1 用户裁定

三选一(声明式自洽门 / 只加布尔字段 / PlayMode 手跑走查)⇒ **PlayMode 手跑走查**。

### 10.2 已落(全部在盘上)

| 件 | 性质 | 状态 |
|---|---|---|
| `unity/Assets/Gameplay.Presentation/Audio/CueTextOrderingRules.cs` | **判定器**(文本先后 + 归属三选一 `Reason`),纯 C#,零 Unity 依赖 | 落 |
| `unity/Assets/Tests/EditMode/Audio/statereport_text_ordering_test.cs` | 判定器 6 条夹具 | **6/6 绿** |
| `unity/Assets/Tests/EditMode/Audio/statereport_walkthrough_probe.cs` | 走查骨架(清单 / 真播 / 文本先后 / 谓词重跑 / 记录 + 设备执行门) | **走查触发已停用**(`Assert.Ignore`) |
| `design/assets/specs/audio-material-manifest.md` §四 | 「替代读法」权威件 + 15 cue 逐项归属表 + 文本先后判据 | 落 |

### 10.3 证伪轮(S12-4)

见 §10.5。

### 10.4 装配阻塞(如实登记 —— 这是 E5 仍 ✗ 的原因)

| # | 阻塞 | 实测证据 |
|---|---|---|
| ① | 走查的文本半须与门同源读 `subtitle_text`;词法器 `JsonStage1Lexer` 是 `Editor.Tools.Bake` 的 `internal` + 仅编辑器装配 | PlayMode 装配(`Gameplay.Tests`)与 EditMode 装配(`Sim.Contracts.Tests`)均 **CS0122**;暂加 `internalsVisibleTo` 后**仍 CS0122**(两次实测)⇒ 已撤回该改动 |
| ② | 真播放的批次语义未定 | EditMode 批次内 `AudioSource.Play` 是否出声**未实测**;`StethoscopePreloadLoadStateTest` 文件头自陈 PlayMode 批次内「真不出声」但未给环境 |
| ③ | 走查须人在场 | 本机无交互式编辑器;batch 只能跑机器半 |

**不得以「骨架已落」充当「走查已做」**(同 commit message ≠ 结局)。

### 10.5 走查骨架的诚实边界

- 本件**不证明**「播放出来的声音不是在报状态」—— 那是耳朵的判定;机器半是**必要条件**,不是充分条件。
- 走查记录 `docs/audio/statereport-walkthrough-<date>.md` **尚未生成**(零走查)。

### 10.6 装配阻塞 ① 的根因与结清(2026-10-17 追加)

**根因(用户要求「先查清为什么没生效」)**:本工程的 IVT 成例**不是** asmdef 的
`internalsVisibleTo` 键,而是**源码 `AssemblyInfo.cs` 上的 `[assembly: InternalsVisibleTo]`**
(先例:`Sim.Codec/AssemblyInfo.cs`、`Editor.Tools.Spike/AssemblyInfo.cs`;承 ADR-025 §①)。
`Editor.Tools.Bake` **从未有过 `AssemblyInfo.cs`** ⇒ 无论测试侧怎么写都够不着其 `internal`。
另:本轮先前对 asmdef 的两次文本替换还**制造了重复键**(`versionDefines` / `noEngineReferences` 各出现两次),
Unity 静默忽略非法 asmdef ⇒ 双重失败,与 Unity 版本行为无关。

**落法**:新增 `unity/Assets/Editor.Tools.Bake/AssemblyInfo.cs`(1 行 IVT + 缘由注);
`Editor.Tools.Bake.asmdef` 已**逐字节还原为 HEAD**。装配阻塞 ① **结清**。

**随即接回的文本半**(用户指示「接着把走查件的文本半接回来」)—— 走查件新增:

| 半边 | 判据 | 走查触发停用时是否被执行 |
|---|---|---|
| **真源字幕读入** | `textHalf_realSource_readsSubtitlesAndPassesOrdering`(读真 `audio_events.json`,逐 cue 取 `subtitle_text` 过判定器) | ✅ 执行 |
| **谓词重跑**(3 个坏输入用例 + 2 个正反用例) | `replayPredicates_*` | ✅ 执行 |
| **静音 cue 的判定无效** | `replayPredicates_silentAsset_rejectsVerdictAsInvalid` | ✅ 执行 |
| **清单 / 真播 / 记录产出** | `walkthrough_allCues_manifestPlaybackAndOrdering` | ❌ 仍 `Assert.Ignore`(阻塞 ②③) |

### 10.6b 待用户裁决(仍在)

- 阻塞 **②**:EditMode 批次的出声语义**未实测**;
- 阻塞 **③**:走查须人在交互式编辑器里跑,本机 batch 只能跑机器半;
- `Music_Encounter_Layer` 是否在 E5 射程内 —— manifest §四 该格现为「**待裁**」
  (ADR-018 §六 G1–G3 护栏是否**取代** E5 判定,无权威件可依)。

### 10.7 证伪轮实测(S12-4 —— 含首轮假绿)

驱动 `/tmp/fz-e5.py`:改 → 跑 → 还原在同一命令内,`finally` 做 SHA-256 逐字节校验。

| ID | 变异 | 首轮结果 | 处理 |
|---|---|---|---|
| **E5-1** | 停用「首词须陈述式」(`!IsStatementHead` → `false && …`) | **8 / 7 / 0 —— 零红 ⇒ 🔴 假绿** | 夹具输入改为「肺热已退…咳」(结论句 + **无**结论词形)⇒ 复跑 **8 / 6 / 1**,恰红 `textOrdering_conclusionHead_isRejected` ✅ |
| E5-2 | 停用结论词形扫描 | 恰红 1:`textOrdering_conclusionAnywhere_includingMidword_isRejected` | ✅ |
| E5-3 | 停用循环行豁免 | 恰红 1:`textOrdering_loopRowWithSubtitle_isExempt` | ✅ |
| E5-4 | 取消走查停用(`if (false) Assert.Ignore`) | 红 1:`walkthrough_allCues_manifestPlaybackAndOrdering`(骨架执行到 NOT-RUN 断言 ⇒ 红,符合预期) | ✅ |

**E5-1 首轮假绿值得记**:首版夹具用「确诊:肺热…咳」——「确诊」同时命中结论词形,
**结论词形扫描兜住了首词守卫**,把首词守卫删掉也照样有 problem。修法不是放宽,是让输入**只有首词守卫能抓**。

| **W1** | 文本半的源字幕读入被架空(读入后不参与判定) | **14 / 12 / 1** —— 恰红 `textHalf_realSource_readsSubtitlesAndPassesOrdering` | ✅ |
| **W2** | 谓词层词法失败不报 | **14 / 12 / 1** —— 恰红 `replayPredicates_rejectsMalformedInput_case("{ not json","词法失败")` | ✅ |
| **W4** | 停用「静音 cue 的判定无效」 | 首轮 **14 / 13 / 0** —— **零红 ⇒ 🔴 无判别力**(静音分支当时无夹具) | 补夹具 `replayPredicates_silentAsset_rejectsVerdictAsInvalid` ⇒ 复跑 **15 / 13 / 1**,恰红该条 ✅ |
| **W5** | 复跑 E5-1 的首词守卫变异 | **14 / 12 / 1** —— 仍恰红 `textOrdering_conclusionHead_isRejected`(夹具修复未回退) | ✅ |

**W4 与 E5-1 同型**:守卫在**生产码里**、断言在**别处** —— 只证「跑过」不证「被守卫钉住」。
两轮都是先红出假绿、再补输入才真红。

### 10.8 回归

```bash
unity test unity --mode EditMode --output unity/Logs/s33-full.xml
# → total 3667 · passed 3621 · failed 0 · inconclusive 0 · skipped 46(exit 0)
```

**对账(逐 fullname)**:`s33-full.xml` vs `s32-full.xml`(3660)→ **新增 7 / 删除 0**,
`3667 − 3660 = 7` 成立。新增全部 = `StateReportWalkthroughProbe` 的接线半边
(源字幕 1 · 词法坏输入 3 · 谓词正/反 2 · 静音 1)。

> ⚠️ **XML 计数口径**:`s33` 的 `<test-case>` 元素为 **3896**、`s32` 为 **3889**,
> 比 `testcasecount`(3667 / 3660)各多 **229** —— 差值是 NUnit 对 `[UnityTest]` / `IEnumerator`
> 用例在 XML 里的**额外节点表示**,两边**差值恒定**(229)⇒ 逐 fullname 差集口径不受影响。
> 差额本身未追根(不在本件判据面内),如实登记。

