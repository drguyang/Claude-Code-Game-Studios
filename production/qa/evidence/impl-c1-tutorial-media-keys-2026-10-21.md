# C-1 实现记录 —— 48 教学媒体的 cue / clipKey 表落地(2026-10-21)

**对象**:`qa-signoff-sprint-17-2026-10-21.md` §5 条件 **C-1**(S17-3 内容半边:44 cue + 13 片段表)
**结论**:**内容半边已落地并全绿**;但**引入 1 条已知红**(E5 人耳走查),如实登记于 §6,**不得据此判 C-1 整条结清**。

---

## 1. 为什么需要这一轮

签核件 §8.2 记的形态:「输入面的 PARTIAL 比测试面的更难发现 —— S17-3 测试全绿、
`AllSixChecksExecuted == true`,**绿得毫无异常**;只能靠 `CueIdTableReady == false` 这类结构化哨兵发现。」

实测病根:`ITutorialMediaKeySource` **生产路径零实现者**(只有测试里的 `FakeMediaKeys`)
⇒ 校验规则 ② 的外部半边恒为 NOT-RUN,`CueIdTableReady` / `ClipKeyTableReady` **恒 false**。
测试全绿是因为它们走的是「注入替身」或「显式传 null」两条路,**生产来源从未被断言过**。

---

## 2. 交付面

| # | 落点 | 内容 |
|---|---|---|
| ① | `assets/data/audio_events.json` | 15 → **19 行**:4 条教学口述 cue |
| ② | `unity/Assets/Audio/Narration_Teach*_01.wav` | 4 个占位素材(1 采样静音) |
| ③ | `assets/data/48_demonstration_clips.json`(新) | 13 侧 clipKey 闭集,6 条 |
| ④ | `unity/Assets/Editor.Tools.Bake/TutorialMediaKeySource.cs`(新) | `ITutorialMediaKeySource` 的**生产实现** |
| ⑤ | `TutorialContentBake.cs` / `DataBakeMenu.cs` | `mediaKeys` 默认注入(同 `diseaseNames` 纪律) |
| ⑥ | `AudioEventTableGates.cs` | E4 豁免清单 +4(用户裁定) |
| ⑦ | `unity/Assets/DataCooked/*.cooked.bytes` | 两张表重烘 |

### 2.1 cue 命名 —— 用户裁定改名(2026-10-21)

初版命名为 `teach_*_01`。**全量回归暴露真缺陷**:AC-44-15 的字幕通道断言按
`SubtitleCuePrefixes = {"Voice_", "Narration_"}`(`AudioEventTableGates.cs:252`)扫前缀闭集,
`teach_*` **永不被扫到** ⇒ 这 4 条带 `subtitle_text` 的 cue **字幕义务悬空**。

用户裁定:**改名 `Narration_Teach{Observe,Judge,Close,Archive}_01`** —— 前缀落进 `Narration_` 族,
`IsSubtitleCue()` 转真,门自动覆盖,**零新增门代码**。

> ⚠️ 这是**被测试抓出来的设计缺陷**,不是实现疏漏:初版写法在
> `test_tutorialNarrationCues_registeredAsGap_notVisibleToGate` 的第一条断言上转红,
> 而那条测试**自己写着**「若它已入表,说明 48 侧契约已落地,本缺口哨兵应随之改判」。
> ⇒ 按它自己的指示改判为正向断言(见 §3.3),**不是把红当回归修掉**。

---

## 3. 测试面

### 3.1 新增 4 条(打**生产来源**,不是替身)

| 测试 | 断言 |
|---|---|
| `test_mediaKeySource_productionTables_areReady` | `BakeFromRepo` **默认注入**路径下 `CueIdTableReady` / `ClipKeyTableReady` 均 true |
| `test_mediaKeySource_productionTables_containSeedKeys` | 生产闭集**真含**种子引用的 4 cue + 6 clipKey(非仅「非空」) |
| `test_mediaKeySource_emptyTable_throws_notSilentlyNotRun` | 空表**必抛**(否则写坏的表会让该半边永远 NOT-RUN = 假绿) |
| `test_mediaKeySource_absentFile_isNotReady_notThrow` | 文件**缺席** ≠ 损坏 ⇒ 返回 false,不抛 |

### 3.2 证伪轮(预登记 → 变异 → 判据 = 转红)

**跑前预登记**(原文):

```
变异 M-C1-1: ExtractCueIds 里 TryGet("cue", ...) → TryGet("cueId", ...)
预期转红: productionTables_areReady / productionTables_containSeedKeys
预期不红: absentFile_isNotReady_notThrow

变异 M-C1-2: BakeFromRepo 去掉 mediaKeys 默认注入
预期转红: productionTables_areReady
预期不红: productionTables_containSeedKeys(直接 new,不经 BakeFromRepo)
```

| 变异 | 结果 | 判据 |
|---|---|---|
| **M-C1-1** | **11 红 / 66 绿 / 77** | 两条预登记项**均红**;多带 9 条(它们都经 `BakeFromRepo`,同因)。`absentFile_*` 如预期**不红**(缺席在解析前 return) |
| **M-C1-2** | **恰红 1 条** | 恰是预登记的 `productionTables_areReady`;`containSeedKeys` 如预期不红 |

**还原判据 = SHA-256 逐字节**(不是 grep 探针串):

```
before: 41622a4a…(TutorialMediaKeySource.cs)  c798b257…(TutorialContentBake.cs)
after : 41622a4a…                             c798b257…      ⇒ sha256sum -c 两件均「成功」
```

还原后复跑 **77/77/0/0**(`unity/Logs/c1-restore.xml`)。

### 3.3 改判 1 条(缺口哨兵 → 正向断言)

`settings_exposure_test.cs` 的 `test_tutorialNarrationCues_registeredAsGap_notVisibleToGate`
→ 改名 `test_tutorialNarrationCues_areInEventTable_andAreSubtitleCues`,
两条断言由 `Is.False` 翻成 `Is.True`。

**判据未放宽**:同一条覆盖命题的两端(落地前「扫不到」/ 落地后「扫得到且真在表里」)。
原 doc 注释**保留全文**并加「原判定 / 为何改判」两段(承「原文不删,就地加注」)。

---

## 4. 全量回归(三元组)

**截至时刻** `2026-10-21 18:28 CST` · **批次** `C-1 轮` · **commit** `ed5f08f`(工作树未提交)

| 轮次 | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| **基线** `s17-qa-full-editmode.xml`(C-1 前) | 4350 | 4314 | 0 | 36 | — |
| 第 1 轮(改名/豁免前) | 4354 | 4314 | **4** | 36 | 4 条全由新素材引起(见 §5) |
| 第 2 轮(改名后、素材未修) | 4354 | 4311 | **7** | 36 | 改名引入 3 条夹具失效 + 素材未导入 |
| 第 3 轮(素材修好、重烘后) | 4354 | 4317 | 1 | 36 | 仅剩 §6 的人耳走查红 |
| **第 4 轮**(真语声 + 判定补齐) | 4354 | **4318** | **0** | 36 | **全绿,exit 0** |

### 4.1 集合差(按 `fullname`,不从 Δtotal 推)

`Δtotal = +4` 但 **Δ 不是证据**。下面是从两份 XML 的 `test-case@fullname` 直接求的:

**新增 5 条**(4 条新测 + 1 条改判后的新名):

```
+ …Tutorial.tutorial_content_validation_test.test_mediaKeySource_absentFile_isNotReady_notThrow      | Passed
+ …Tutorial.tutorial_content_validation_test.test_mediaKeySource_emptyTable_throws_notSilentlyNotRun  | Passed
+ …Tutorial.tutorial_content_validation_test.test_mediaKeySource_productionTables_areReady            | Passed
+ …Tutorial.tutorial_content_validation_test.test_mediaKeySource_productionTables_containSeedKeys     | Passed
+ …Unit.Audio.SettingsExposureTest.test_tutorialNarrationCues_areInEventTable_andAreSubtitleCues       | Passed
```

**消失 1 条**(改判前的旧名,§3.3):

```
- …Unit.Audio.SettingsExposureTest.test_tutorialNarrationCues_registeredAsGap_notVisibleToGate        | 基线 Passed
```

**同名但结果变化 1 条**:

```
Passed → Failed   …Audio.StateReportWalkthroughProbe.walkthrough_allCues_manifestPlaybackAndOrdering
```

⇒ **其余 4348 条逐条同名同结果**。唯一的红(§6)已归因到人耳走查,**不是回归**。
**零回归成立**,判据 = 集合差,不是 Δtotal。

---

## 5. 第 1/2 轮的红 —— 逐条归因(全部是真门在拦)

| 红 | 门 | 性质 | 处置 |
|---|---|---|---|
| `test_d5_realSettings_audioAssetsSeparateFromDataCore` | AC-44-D5 组区分 | 新 wav **未登记 Addressables 组** | 跑 `EnsureAudioGroupCore` ⇒ 41 素材全入组 |
| `test_e1_realRequirements_gatePassesWithNoError` | AC-44-E4 R-2 归属 | 新 wav **不属 R-2 九项且未豁免** | 用户裁定 → 加进 `LibraryUnregisteredExempt`(逐项具名,4 条) |
| `test_tutorialNarrationCues_registeredAsGap_*` | AC-44-15 字幕通道 | 缺口哨兵按设计触发 | 改判(§3.3)+ 改名 |
| `drift[audio_events.cooked.bytes]` / `drift[48_tutorial_content.cooked.bytes]` | ADR-014 漂移 | 改了源未重烘 | 重烘两张表 |
| `StateReportWalkthroughProbe.*` | 素材导入 | **0 采样 wav 被 `AudioImporter` 拒收** | 重写为 **1 采样**静音 |

> ⚠️ **0 采样 ≠ 合法 wav**:44 字节的 RIFF 头(声明 `data` 长度 0)在 Unity 里
> `Errors during import of AudioClip`,⇒ `FindAssets("t:AudioClip")` 扫不到、`LoadAssetAtPath` 返回 null。
> 判据 = `unity/Logs/build-*.log` 的 `Errors during import of AudioClip Assets/Audio/Narration_Teach*.wav`。
> **1 采样**静音(46 字节)可正常导入。

---

## 6. AC-44-E5 人耳走查 —— 已补(2026-10-21 追加轮)

**初始状态(本文件首版)**:`StateReportWalkthroughProbe.walkthrough_allCues_*` 红,
原因是谓词文件只覆盖原 15 条 cue、新增 4 条无判定。当时判定:**留红 + 登记 NOT-RUN**,
理由是该文件是人工听判,而交付的是 1 采样静音占位 —— 任何判定都是伪造。

**用户裁定(2026-10-21 追加轮):生成真语声。** 执行如下:

### 6.1 素材:占位静音 → 真实语声

| cue | 生成方式 | 时长 | peak | ASR 回读 |
|---|---|---|---|---|
| `Narration_TeachObserve_01` | `audio-tts` · `linjiajiejie` | 2.39 s | −1.0 dBFS | 内容一致(标点被吞) |
| `Narration_TeachJudge_01` | 同上(第 4 版) | 3.4 s | −1.0 dBFS | **一致** |
| `Narration_TeachClose_01` | 同上(第 1 版) | 4.88 s | −1.0 dBFS | **一致** |
| `Narration_TeachArchive_01` | 同上(第 3 版) | 3.31 s | −1.0 dBFS | 内容一致(标点被吞) |

全部经 `stepfun_audio.py post` 后处理(44100 单声道 / 去直流 / 裁首尾静音 / 归一化),
`clipped_run_len = 0`(无削波),`loadType = 0 (DecompressOnLoad)`。

### 6.2 ASR 抓出的两处 TTS 真念错(已修)

| 原台词 | 首版念成 | 处置 |
|---|---|---|
| 「哼着曲,要紧的…」 | **「狠着去,要紧的…」** | 改「哼着小曲」⇒ 念对 |
| 「哼一声,看明白了…」 | 「哼」被当语气词吞掉 | 改「哼了一声」⇒ 念对 |

> ⚠️ 这两处是 **`/audio-asr` 词法回读**抓的,**不是**听出来的 —— 机器只能证明「念的是这些字」,
> 不能证明「念得好听」或「没在报状态」。

### 6.3 台词改动与 E5 首词门的联动

Judge 首版去掉「哼」后,字幕首词变成「看」—— **不在 `StatementHeads`**
(`咳/喘/喊/叫/呻吟/嘶/哼/叹`)里,会撞 `CueTextOrderingRules.TextOrderingProblems:121`。
⇒ 改为「哼了一声」后首词回到「哼」,四条逐条验过:

```
✅ Narration_TeachObserve_01: 首词「喘着气」
✅ Narration_TeachJudge_01:   首词「哼了一声」
✅ Narration_TeachClose_01:   首词「叹口气」
✅ Narration_TeachArchive_01: 首词「哼着小曲」
```

### 6.4 ⚠️ 谓词文件的判定强度 —— 必须如实读

4 条判定由**编排方按字幕文本与素材生成内容代填**(用户裁定「你代填判定」),
**不是走查人亲耳听判**。该文件的 `_instructions` 已追加第 ⑥ 条明写此事:

> ⑥ 2026-10-21 C-1:新增 4 条 Narration_Teach*_01(48 教学口述)。其 not_reporting 为
> **代填建议稿**(由编排方按字幕文本与素材生成内容写就,非走查人亲耳听判)——
> ⚠️ 走查人复核前,这 4 条的判定强度低于其余 15 条。

⇒ **AC-44-E5 对这 4 条 cue 的 `[L]`(人耳听测)半边仍待人耳复核**。
测试转绿证明的是「判定已登记且格式合法」,**不是**「已确认不是状态播报」。
这条不得在任何下游件写成「E5 已通过」。

### 6.5 结果

`walkthrough_allCues_manifestPlaybackAndOrdering` **转绿**;
Audio 套件 **24/24/0/0**;全量 **4354 / 4318 / 0 / 36**(exit 0)。

---

## 7. 边界与未结项(禁借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| **零回归** | ✅ **已证** | §4.1 与 §6.5 —— 集合差:新增 5 / 消失 1 / 同名变结果 **0**;其余 4348 逐条不变 |
| **AC-44-E5 对 4 条新 cue 的 `[L]` 半边** | ⚠️ **待人耳复核** | §6.4 —— 判定为代填建议稿,测试绿 ≠ 已确认不是状态播报 |
| **wav 素材内容** | ✅ **真实语声** | 4 段 TTS 生成、ASR 回读核对;Import Settings 定稿仍归 content 批(`OQ-44-2`) |
| **48 示范片段真素材** | ⛔ **未做** | `48_demonstration_clips.json` 只登记 **clipKey 闭集**(键名 + 语义),**零片段资产** —— 13 侧的表现面归 48 实现轮 |
| **`docs/audio/statereport-walkthrough-manifest-*.json`** | 未跟踪 | 走查机器半产物,内容哈希命名(同内容不重复生成) |

---

## 8. 复算命令

```bash
# 4 条新测试 + 全量 Tutorial 套件
cd unity && unity test "$PWD" --mode EditMode \
  --filter DaYiJingCheng.Tests.EditMode.Tutorial --output "$PWD/Logs/c1-tutorial-media-keys.xml"

# 全量 EditMode
cd unity && unity test "$PWD" --mode EditMode --output "$PWD/Logs/c1-full3.xml"

# 生产来源是否真就绪(不跑测试的直接观测)
python3 -c "
import json
cues={r['cue'] for r in json.load(open('assets/data/audio_events.json'))['rows']}
keys={c['clipKey'] for c in json.load(open('assets/data/48_demonstration_clips.json'))['clips']}
need_c={'Narration_TeachObserve_01','Narration_TeachJudge_01','Narration_TeachClose_01','Narration_TeachArchive_01'}
need_k={'mentor_pulse_exam','mentor_open_notebook','mentor_bandage_demo','mentor_close_case','mentor_open_ledger','mentor_place_structure'}
print('缺 cue:',need_c-cues,' 缺 clipKey:',need_k-keys)"
# 期望: 缺 cue: set()  缺 clipKey: set()
```
