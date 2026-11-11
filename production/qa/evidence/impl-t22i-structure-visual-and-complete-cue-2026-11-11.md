# T22 批九 —— 结构视觉剩余半边 + 18 的起货锚点(`TD-building-05`)证据原件

**日期**:2026-11-11
**批次**:T22 批九(用户「开工 T22 批九」→ 选 **丙 · 结构视觉剩余半边**;中途裁定「顺手接起货音」)
**债**:`TD-building-05`(`partial` → 状态见 §6)

---

## 0. 开工复勘:卡面写的「剩余半边」比实际大

| 卡面原文 | 实况 |
|---|---|
| 「② `AC-18-21` 端到端人工腿跑通」 | **AC 本身早已机器覆盖**:`process_completion_and_spill_test`(S24-1)有 **13 条 PlayMode 夹具** —— 调度落点、1 tick 相等、cue 恰一条、重复边沿抛、无墙钟符号等 |
| 「`OQ-18-9` 的具名形态仍未裁」(T22 批一证据 §8) | **已裁 2026-10-06**:语义 = 世界器物状态 / 42 不画进度;具名渲染形态 = 42 的 `OQ-IC-4` 器具材质档 ⇒ 该句是**陈旧前提** |
| 真正的缺口 | 生产上**起货无任何可感知信号**:`audio_events.json` 19 行里零 `sfx_process_*`(`TD-processing-08` 关闭注已记);体块不随完成变化;42 不画进度 |

⇒ 本批做两件:① **生产装配级判据**(上一轮只有真包日志,没有机器判据);
② 按用户裁定**接起货音**(素材 + 表行 + 构建期门 + 重烘),让 ② 的人工腿**变得可能**。

---

## 1. 生产装配级判据(新)

`Tests/EditMode/Boot/structure_visual_wiring_test.cs` —— 2 条,从**生产装配入口**出发:

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_assemble_withRealProvider_spawnsOnePerBakedSlotPlusContainers` | `Assemble(world:, dataProvider: 真 provider)` ⇒ 生成器**非 null**,且 `SpawnedCount == baked 槽位数 + 容器行数`(数取自真产物,不是内联常数) |
| 2 | `test_assemble_withoutProvider_leavesSpawnerNull` | **负控**:`Assemble()` ⇒ 生成器为 null(「本次装配没装世界」可观测,不是「世界没有槽位」) |

> 上一轮的 7 条夹具**直接 `new` 生成器**,证明的是「生成器对」,不证明「装配真的接上并铺出来」。
> 这一层此前只有真包日志(`[结构视觉] 槽位体块 9 个 + 容器柜 1 个`)一条证据。

---

## 2. 起货音(`sfx_process_complete`)

| 步 | 落点 |
|---|---|
| 素材 | `unity/Assets/Audio/sfx_process_complete.wav`(44.1 kHz / 单声道 / 16 bit / 1.375 s;`.meta` `loadType: 0`) |
| 作者态表 | `assets/data/audio_events.json` **末位追加**一行(`WorldPerceptible` × `WorldPerceptible`,bus `SFX`)—— 行序 = cue ordinal,只能追加 |
| 构建期门 | `AudioEventTableGates.RequiredCues` 增该名(驱动常量注释原话:「加表行的那一轮再同步加门」) |
| 名称真源 | `design/assets/specs/audio-material-manifest.md` §一之三(冻结行) |
| 烘焙 | `audio_events.cooked.bytes` 20 行,`ConfigVersion = 0x51895E27` |
| Addressables | `audio` 组 42 个素材(含新增),`EnsureAudioGroup` 已跑 |
| 判据 | `Tests/EditMode/Audio/process_complete_cue_test.cs` 4 条(行在末位 / 运行期解析到同 ordinal / `RequiredCues` 认 / `.meta` 档位与头算一致) |

⚠️ **人耳试听已由用户裁定通过**(2026-11-11,`assets/audio-staging/sfx_process_complete/sfx_process_complete.post.wav`)。
⚠️ 本件**不代签**任何 `[L]` 声学判据(`AC-44-03` 等),只记结构面。

---

## 3. 判据映射

| 判据 | 载体 |
|---|---|
| `TD-building-05` ①(真包看见可辨识体块) | T22 批一已 PASS(灶台 `(14,0,14)` + 截图) |
| 生产装配真的铺视觉(本批补) | `structure_visual_wiring_test` 2 条 |
| `TD-building-05` ②(AC-18-21 人工腿) | AC 机器面 = 既有 13 条;**完成信号** = 本批新增的 cue 行(此前为零) |
| AC-18-21 的 cue 发射面 | `process_completion_and_spill_test`(既有)+ 本批「表里真的有这一行且解析得到」 |
| E5 走查(AC-44-E5) | 谓词文件 `docs/audio/statereport-walkthrough-predicates.json` 补第 20 条(人耳判定)+ `date` → 2026-11-11;probe 重跑 ⇒ 新记录 `docs/audio/statereport-walkthrough-a591833793aebe2a.md`(20 cue,内容寻址,旧的 `cbb794f1…` 原样保留) |

---

## 4. 证伪轮

脚本 `$CLAUDE_JOB_DIR/tmp/falsify_t22i.py` + `run_falsify_t22i.sh`(逐枪「命中恰 1 处才跑」),
每枪单跑对应套件。**每条变异至少打红 1 条测试**,五个文件还原后 SHA-256 逐字节一致。

| 枪 | 变异 | 靶文件 | 结果 |
|---|---|---|---|
| M1 | `RequiredCues` 删 `sfx_process_complete` | `AudioEventTableGates.cs` | 1 红:`test_requiredCues_gateIncludesProcessComplete` |
| M2 | 驱动常量改名(表行不动)⇒ 两处失联 | `ProcessingCompletionDriver.cs` | 1 红:`test_runtimeTable_resolvesCue_toLastOrdinal` |
| M3 | 新行与上一行交换(不再末位) | `audio_events.json` | 2 红:`test_realTable_hasProcessCompleteRow_atLastOrdinal` · `test_runtimeTable_resolvesCue_toLastOrdinal` |
| M4 | `.meta` `loadType: 0 → 1` | `sfx_process_complete.wav.meta` | 1 红:`test_wav_loadType_matchesHeaderDerivedRequirement` |
| M5 | 装配不铺槽位体块(`int spawned = 0;`) | `CompositionRoot.cs` | 1 红:`test_assemble_withRealProvider_spawnsOnePerBakedSlotPlusContainers` |

终态 SHA-256 对照(全部一致):
`fef04f1c7ba04902` `AudioEventTableGates.cs` · `614c52afc39b6e7c` `ProcessingCompletionDriver.cs` ·
`6fff1e9d16139a30` `audio_events.json` · `703d42912fbcdbe8` `sfx_process_complete.wav.meta` ·
`f4086bc8c096e2db` `CompositionRoot.cs`。

> M3 是两条独立判据各红一次(行末位判据 + 运行期解析判据),证明「末位」不是只被一条测试钉住。
> ⚠️ 上表哈希是**各枪还原当时**的值。之后对 `AudioEventTableGates.cs` 又做过一处**注释订正**
> (`LibraryUnregisteredExempt` 的 remarks:『目前两类』→『三类』,补上第 ③ 项起货音)——
> 现值 `1d7b9fe049ea60d9…`。该订正**只动注释**,重跑 `EventTableGateTest` + `PipelineGateTest`
> **179/179 全绿**(`unity/Logs/editmode-t22i-commentcheck.xml`)。

---

## 5. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| 定向(受影响四套件) | `199 / 199 / 0 / 0 / 0` | `2026-11-11 02:57:08Z`–`02:57:42Z` | T22 批九 | `622554f5` + 工作树 |
| EditMode 全量 | `7443 / 7406 / 0 / 0 / 37` | `2026-11-11 03:02:07Z`–`03:04:14Z` | T22 批九 | `622554f5` + 工作树 |
| PlayMode 全量 | `380 / 375 / 0 / 2 / 3` | `2026-11-11 03:05:07Z`–`03:05:29Z` | T22 批九 | `622554f5` + 工作树 |

**对照基线**(T22 批八):EditMode `7437 / 7400 / 0 / 0 / 37` · PlayMode `380 / 375 / 0 / 2 / 3`。
⇒ EditMode **+6**(= 本批新增两个夹具文件的 `[Test]`:4 + 2),PlayMode **逐项相同(零回归)**。
等式自证:`7443 − 7437 = 6`。

---

## 6. 债册

| 债 | 前 | 后 | 依据 |
|---|---|---|---|
| `TD-building-05` | `partial` / P0 面 **Y** | **不变**(`partial` / P0 面 **Y**) | ① 已 PASS;② 人工腿仍 `NOT-RUN`(端到端没人跑过 —— 但路径齐备,见 §7);本批新增 2026-11-11 行说明两件落地 |
| `TD-processing-08` | `closed` | **不变**(`closed`) | 关闭记录里「起货音仍不响」一句**加日期订正注**(原文保留):现已有 cue 行,该句不再成立 |

`debtcount --check` ⇒ **exit 0**。现值(实跑 `python3 tools/debtcount/debtcount.py --print`):

```text
条目 207 | 状态 {'closed': 175, 'partial': 9, 'open': 22, '**done**(2026-10-21)': 1}
P0 面 {'Y': 135, 'N': 66, '~~**Y**~~': 4, '~~**N**~~': 2}
未闭 P0 面 = 11      M3 Exit 第二条 ⇒ 假 —— 11 条,分布在 6/31 个 epic(modular-building: 1)
```

⚠️ **数字与 T22 批八相同,这不是没刷** —— 本批**没有**关闭或新开任何债(两个方向都没动),
`TD-building-05` 按卡面判据「解除判据 ② 未跑 ⇒ 不得 closed」维持 `partial`。

---

## 7. 本批没有做的(不得借绿)

| 项 | 状态 |
|---|---|
| **② 的人工腿** | 仍 **NOT-RUN** —— **没人跑过端到端**。本批把它从「即使跑也听不到完成信号」变成「**路径齐备、可跑**」。⚠️ **订正初稿**:初稿写「P0 无任何发料入口 ⇒ 不可达」,**实测不成立** —— 料可从 4 个资源点采集获得(`world_resources.json` 4 行;`CompositionRoot.cs:1518` 的 `if (dataProvider is AddressablesDataProvider)` 段内 `:1627` 装配 `HarvestInteractionDriver`),经 20 屏选中态(`ContainerSelectionStore`)进点火。⇒ 「不可达」不再成立,只是**未跑** |
| **`sfx_process_light` / `sfx_process_work`** | 未落:GDD `:833` 点名三支,本批只落起货(唯一有生产消费点的一支) |
| **`AC-44-03 [L]` 等声学判据** | 仍 `NOT-RUN` —— 人耳试听只证明「这一版能接受」,不关闭任何 AC |
| **体块无碰撞** | 不变(与病人胶囊同纪律,归 23 的物理轮) |