# S15 实现证据 —— story-015(音频资产接入 · 5 个被引 `.wav` 落地)· 2026-10-17

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-015-audio-asset-binding.md`
> **sprint**:`production/sprints/sprint-14.md`(**计划外增项** —— 见 §0)
> **用户裁定(2026-10-17,三选一取 B)**:`先落证据原件 … 再跑证伪轮`
> **素材来源**:① 男声压抑咳嗽(24 kHz MP3 → 44.1 kHz WAV)·
> ② 女声压抑咳嗽(同)· ③ 正常肺泡呼吸音 0–2 s(50 ms 交叠交叉淡化)·
> ④ 中湿罗音 9.25–11.25 s(同)**经用户医学裁定认定为细湿啰音** ·
> ⑥ 战斗遭遇乐层(切 0.1 s 头部 + 2000 ms 交叉淡化)
> **原始 MP3 已移出工程** → `/home/gu/文档/nm/nm2/素材原始/`(SHA-256 已核)

---

## §0 本件的性质 —— 为什么它不在 sprint-14 的原计划内

story-015 的原始前提是 **「素材本体产出归内容批(`OQ-44-2`);本 story 只做接入与检验」**。
但本轮用户改为**只做素材、暂不做接入检验**,于是本件实际交付的是**原本划给内容批的素材本体**
(S14-6 只落了 2044 B 的**占位静音** wav)。

⇒ **本件不是新 story,是 story-015 的前置半边提前落地。**
**story-015 本身仍 `Ready ⬜`** —— 它的四条 AC 里有两条(D6c 判别力 / D15 loadType)
今天**不能**判绿,理由逐条列在 §4。

⚠️ **本件不声称 story-015 已完成。**

---

## §1 原判定 → 修复落点

| # | 原判定(`review-audio-epic-fake-green-2026-10-16.md` §2.4 + EPIC「范围边界声明 一」) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | `assets/data/audio_events.json` 引用了 **5 个具体 `.wav` 文件名,逐个均不存在** —— 「引用存在、资产不存在,而它无任何告警」 | 🔴 | 5 个真实音频内容落 `unity/Assets/Audio/`(替换 S14-6 的 2044 B 占位静音) | §2 双向差集 = 零 |
| 2 | 素材为 **单声道 44100 Hz / 16-bit** 未知(占位为 1000 样本 ≈ 22.7 ms 静音) | 🟡 | 逐文件 `wave` 头实测(§2) | 播放头解析 |
| 3 | 5 个 wav 是否在 **`audio`** Addressables 组、是否与 `data-core` 不相交 —— **无实测** | 🟡 | 读真 `audio.asset` / `data-core.asset` | §2 组分离实测 |
| 4 | AC-44-D6c「删任一被引 `.wav` ⇒ 构建期硬失败」—— **从未在真素材上跑过**(story-010 只验了夹具) | 🔴 | **证伪轮**:真删 → 跑 → 还原(§3) | §3 反证 15 红 |
| 5 | Import Load Type **未 pin**(`music_*` 按 `audio.md` 尺寸规则**应为 Streaming**,现为默认 `DecompressOnLoad`) | 🟡 | **本件未修** —— 见 §4 欠账 ① | — |

---

## §2 已实测判据(实测命令 + 实测值)

### §2.1 双向差集归零 —— **AC-44-D16 ✅**

命令(仓根):
```python
tbl = json.load(open("assets/data/audio_events.json"))
referenced = {a for r in tbl["rows"] for a in r.get("assets", [])}
on_disk = {os.path.basename(p) for p in glob.glob("unity/Assets/Audio/*.wav")}
referenced - on_disk   # 缺项
on_disk - referenced   # 孤儿
```
实测:
```
表引用 (5): music_encounter_layer_loop.wav · sfx_breath_adventitious_fine.wav ·
            sfx_breath_base_loop_small.wav · vo_patient_cough_damp_f1.wav · vo_patient_cough_damp_m1.wav
盘上 wav (5): 同一集合
被引但不存 (缺项): 无
存在但未被引 (孤儿): 无
```

### §2.2 5 个素材的实测规格与哈希

| 文件 | 字节 | 通道 | 采样率 | 位深 | 帧数 | 时长 | SHA-256 |
|---|---|---|---|---|---|---|---|
| `music_encounter_layer_loop.wav` | 2056516 | 1 | 44100 | 16 | 1028236 | **23.3160 s** | `97121b3c742e7c815df9c4d3bcea6b62cf8e15197b5a2b0830091c6a04401502` |
| `sfx_breath_adventitious_fine.wav` | 172030 | 1 | 44100 | 16 | 85993 | **1.9500 s** | `a553f10dfff00370ac8913c48ec10c9c4366113fc057f71f02f74e351b196826` |
| `sfx_breath_base_loop_small.wav` | 172034 | 1 | 44100 | 16 | 85995 | **1.9500 s** | `68261a7e29953b9d908a849070fda56abae6d79090f8743d9420ccc8c9c3f0e5` |
| `vo_patient_cough_damp_f1.wav` | 139788 | 1 | 44100 | 16 | 69855 | **1.5840 s** | `0586f19f0cb385cc34508e02d49ab27e75a1392492913c031c927878ed4a4d7c` |
| `vo_patient_cough_damp_m1.wav` | 110152 | 1 | 44100 | 16 | 55037 | **1.2480 s** | `4b8e91447b7c4f1034077b350d07b87732beeb229f18e915410b47c48e6dbef4` |

### §2.3 Addressables 组分离 —— **AC-44-D15 的「组」半边 ✅**

```
unity/Assets/AddressableAssetsData/AssetGroups/audio.asset
  组名: audio · 条目数: 5
    sfx_breath_base_loop_small.wav · vo_patient_cough_damp_f1.wav ·
    vo_patient_cough_damp_m1.wav · sfx_breath_adventitious_fine.wav ·
    music_encounter_layer_loop.wav
unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset
  "Assets/Audio" 出现次数 = 0
```
✅ 5 个素材全在 `audio` 组;`data-core` 含**零**条 `Assets/Audio/` 条目 ⇒ 分离成立。

### §2.4 全量回归(EditMode · 命名空间 `DaYiJingCheng.Tests.Unit.Audio`)

```
total="395" passed="395" failed="0" inconclusive="0" skipped="0"   (result="Passed")
```
逐 fixture:
| fixture | total/passed/failed |
|---|---|
| AssemblyBoundaryTest | 24 / 24 / 0 |
| BreathLayersTest | 37 / 37 / 0 |
| EventTableGateTest | 62 / 62 / 0 |
| LoopLifecycleTest | 16 / 16 / 0 |
| MainChannelListenTest | 7 / 7 / 0 |
| MixerAssetGeneratorTest | 8 / 8 / 0 |
| MixerTopologyTest | 21 / 21 / 0 |
| MusicGuardrailsTest | 22 / 22 / 0 |
| NetDerivationTest | 34 / 34 / 0 |
| **PipelineGateTest** | **52 / 52 / 0** |
| SettingsExposureTest | 31 / 31 / 0 |
| SnrAnalysisTest | 21 / 21 / 0 |
| SpatializationTest | 28 / 28 / 0 |
| TierFilterCarrierTest | 11 / 11 / 0 |
| VoiceVariantsTest | 21 / 21 / 0 |

**结果 XML**:`unity/Logs/s15-audio.xml`(283514 B)· **日志**:`unity/Logs/s15-audio.log`

⚠️ **口径声明(禁借绿)**:这 395 是**整个 audio 命名空间**(story-001…014 的历史测试),
**不是** story-015 四条 AC 的专属证据。其中 **52 条 `PipelineGateTest`** 才是 story-010/015 的判据面。

---

## §3 证伪轮 —— AC-44-D6c(真素材复验)· **判别力已证实 ✅**

### §3.1 被判别的守卫与变异设计

| 项 | 值 |
|---|---|
| **守卫** | `AudioAssetExistence.Missing`(`unity/Assets/Editor.Tools.Bake/AudioAssetExistence.cs:62-78`) |
| **调用点** | `AudioEventTableBinder.AppendMissingAssetErrors`(`:111`),`:76` 聚合 `throw BakeValidationException` |
| **变异** | **真删** `unity/Assets/Audio/vo_patient_cough_damp_m1.wav`(**事件表不动**) |
| **被测 fixture** | `DaYiJingCheng.Tests.Unit.Audio.PipelineGateTest`(52 条) |

**与既有夹具的差别(这是本 AC 要求「真素材复验」的理由)**:

| | 既有负向夹具 | 本次真删 |
|---|---|---|
| 事件表 | **改了**(`sfx_breath_adventitious_fine.wav` → `__missing_cue__.wav`) | **不动** |
| 文件 | 不动 | **真删** |
| 门看到 | 新造的名字不在根下 | 原本在根下的名字**消失了** |

两者对 `Missing` 谓词的判别面同型,但**触发路径不同** —— 前者走「表里有个根下没有的名字」,
后者走「表里有个**本该在**根下的名字没了」。

### §3.2 反证结果 —— **15 红 / 37 绿**(`failed="15"`)

```
total="52" passed="37" failed="15" inconclusive="0" skipped="0"   (result="Failed(Child)")
```
**15 条红测,全部同一根因**:
```
DaYiJingCheng.EditorTools.Bake.BakeValidationException : 烘焙校验失败(1 条):
  1. [D6·素材存在性] 行「Voice_Cough_Damp」引用了缺失素材:vo_patient_cough_damp_m1.wav
     —— 期望根 assets/audio(物理 …/unity/Assets/Audio)。AC-44-D6:素材缺失 = 编辑期烘焙门拒绝(构建…
```
红测清单(逐条):
`test_bake_headerContract_audioAndSettings` · `test_bake_materialize_crossSideMatches` ·
`test_bake_rowOrderIsCueOrdinal` · `test_bake_rowOutOfRange_throws` ·
`test_bake_sameSeedTwice_bitIdentical` · `test_d4_runtimeCodec_decodesBakedBytesOnly` ·
`test_d4_runtimeCodec_payloadSchemaDivergedFromHeader_rejected` ·
**`test_d6_realSeed_bakesWithNoMissingAsset`** · `test_preload_allLoaded_allows` ·
`test_preload_failedState_blocks` · `test_preload_fromTable_derivesStethoscopeFamily` ·
`test_preload_handoverMux_gateAllowsEntry` · `test_preload_handoverMux_gateBlocksEntry` ·
`test_preload_notLoaded_blocks` · `test_preload_partialLoaded_blocks`

### §3.3 判读

**① AC-44-D6c 的硬失败语义 = 成立。** 门是 `throw`,**不是** `Debug.Assert`,**不是**运行期降级。
错误报文同时点名**文件**(`vo_patient_cough_damp_m1.wav`)· **cue**(`Voice_Cough_Damp`)·
**标记**(`[D6·素材存在性]`)· **期望根**(逻辑 `assets/audio` + 物理绝对路径)⇒ 可追溯、可证伪。

**② 判别力是「广谱」而非「点对点」(已知特征,登记不隐藏)。**
15 红中**仅 1 条**(`test_d6_realSeed_bakesWithNoMissingAsset`)是 D6 正向判据本身;
其余 **14 条**是**下游连带** —— 它们经 `BakeFromRepo(RepoRoot())` 取真种子,
门一抛错即全线拿不到夹具。
⚠️ **这不是假绿**(它们确依赖真实烘焙),但意味着 **D6 门是全 `PipelineGateTest` 夹具的单点依赖**。
**判为设计使然**(门强制的必然结果),登记为已知特征,**不计缺陷**。

**③ 反证 = 变异测试,不是「测试名 + 行号」。** 判据为「停用守卫 ⇒ 断言转红」,已达成。

### §3.4 还原纪律 —— **同命令原子 + SHA-256 逐字节校验 ✅**

删 / 跑 / 还原**在同一个 Bash 调用内**完成(无跨命令窗口),`finally` 语义后做逐字节校验:
```
orig  sha (wav)  : 4b8e91447b7c4f1034077b350d07b87732beeb229f18e915410b47c48e6dbef4
orig  sha (meta) : c9a8b74e1ea7c27b1b49f0199c5861b15a4c5d75f892fe5f8ebc1599f4e55595
restored (wav)   : 4b8e91447b7c4f1034077b350d07b87732beeb229f18e915410b47c48e6dbef4   ✅
restored (meta)  : c9a8b74e1ea7c27b1b49f0199c5861b15a4c5d75f892fe5f8ebc1599f4e55595   ✅
wav count back to: 5
```
**工作树复核**:`git status --porcelain unity/Assets/Audio/` = 仅 5 个预期 `M`。

> ⚠️ **2026-10-17 更正:本句原写「零证伪残留」—— 该声称当时不成立。**
> 本次校验集只含 `.wav` + `.wav.meta`,**漏了 `unity/Assets/AddressableAssetsData/AssetGroups/audio.asset`** ——
> 真删资产触发 Unity 的 Addressables 自动维护,该组由 5 条变 **4 条**。
> 该目录在 `.gitignore:85` 被**整目录忽略** ⇒ `git status` **不提示**。
> **实际泄漏与修复全过程见 §3.5.4**。此处原文保留不改写,加本声明(承 ADR-015 修订 ADR-009 §四 先例)。

**结果 XML**:`unity/Logs/s15-fz.xml`(70193 B)· **日志**:`unity/Logs/s15-fz.log`
(Unity 退出码 `2`为 batch 模式退出码,**不作判据** —— 判据是 XML 的 `failed` 计数)

---

## §3.5 证伪轮 二 —— 两处未反证守卫的补充反证 · **两处均转红 ✅**

> **由来**:§3 的首轮证伪只覆盖了 `Missing` 的**存在性命中**半边,
> 留下两处未反证守卫(原 §4 ②③)。本节为首轮遗留的**补反证**。

### §3.5.1 变异设计(两处同批,同一命令内)

| # | 守卫 | 落点 | 变异手法 |
|---|---|---|---|
| **②** | `Missing` 的**保序去重** | `AudioAssetExistence.cs:70-75` | 删去 `if (!seen.Add(name)) continue;` 一行(去重失效,保序保留) |
| **③** | `ValidateEntries` 的 **`data-core` 不相交判据** | `AudioAssetGroupGates.cs` (a) 半边 | `if (inDataCore)` → `if (false && inDataCore)`(判据停用,消息保留) |

### §3.5.2 反证结果 —— **2 红 / 50 绿**(重做后 · 干净组状态)

> ⚠️ **本节为**首轮判读错误后的**重做件** —— 首轮曾误报 3 红(其中 1 条归因错误)。
> 错误根因与修正过程见 **§3.5.4**。

```
total="52" passed="50" failed="2" inconclusive="0" skipped="0"   (result="Failed(Child)")
```

| 变异 | 转红夹具 | 实测失败消息(关键片段) |
|---|---|---|
| **②** | `test_d6_missing_isOrderPreservingAndDeduped` | `Expected is String[2], actual is List<String> with 3 elements … Extra: "b.wav"` ⇒ **重复项未被去除,精准命中去重语义** |
| **③** | `test_d5_pure_audioAssetInDataCoreGroup_rejected` | `Expected: String containing "data-core" \| But was: "[D5] 音频素材「…」未登记进任何 Addressables 组…"` ⇒ 报的是**另一条**消息,(a) 半边确被停用 |

**应绿仍绿的两条(逐条说明,不是假绿)**:

| 夹具 | 变异 ③ 下 | 说明 |
|---|---|---|
| `test_d5_pure_strayAudioEntryInDataCore_rejected` | 🟢 绿 | `ValidateEntries` 的 (a)/(b) 是**互为冗余的两条独立判据**(源码注释自陈);停 (a) 后 **(b) 仍生效** ⇒ 该测由 (b) 满足。⇒ **反向印证**「互为冗余」属实 |
| `test_d5_realSettings_audioAssetsSeparateFromDataCore` | 🟢 绿 | 首轮它**红**,是**环境污染**非变异 —— 见 §3.5.4(A)(B) |

**结果 XML**:`unity/Logs/s15-fz3.xml` · **日志**:`unity/Logs/s15-fz3.log`

### §3.5.3 还原纪律 ✅(源码 **+ 组数据** 双校验)

改 / 跑 / 还原**在同一 Bash 调用内**完成。
```
orig     d5 src sha : b854b4cfa7a1f92daa087fa454ad91873c2dd30ed4bda632d2ad657826435e7c
orig  audio.asset   : 2f380c6b0704df4c21a6084cae8af497069c021657d5a56a58899de94a74d3dc (5 条)
restored d5 src     : b854b4cfa7a1f92daa087fa454ad91873c2dd30ed4bda632d2ad657826435e7c   ✅
restored audio.asset: 2f380c6b0704df4c21a6084cae8af497069c021657d5a56a58899de94a74d3dc (5 条)   ✅
```
⚠️ **本次校验集含 `audio.asset`** —— 首轮漏检该项,见 §3.5.4(C)。

### §3.5.4 ⚠️ 首轮判读错误 + 证伪手法缺陷(登记不隐藏)

**这是本件最重要的一处自我更正。**

**(A) 首轮误报:3 红 → 实为 2 红。**
首轮(`s15-fz2.xml`)报 3 红,含 `test_d5_realSettings_audioAssetsSeparateFromDataCore`。
我将其归因为「变异 ③ 的连带」—— **该归因错误**:
我停用的是 `inDataCore` 分支,而该测试当时报的是 **`owner == null`** 分支
(**那条分支我根本没改**)⇒ 它的红**不可能**由该变异造成。

**(B) 真正的红源:证伪轮一(§3.3)的副作用泄漏。**
证伪轮一真删 `vo_patient_cough_damp_m1.wav` 后启动 Unity,
**Addressables 检测到资产不存在,自动把该条目从组里移除** ——
`audio.asset` 由 5 条变 **4 条**(文件时间戳 `10月17 01:34` = 证伪轮一执行时刻)。
我随后 `cp` 还原了 `.wav` 与 `.wav.meta`,**但 `audio.asset` 已被 Unity 改写**。

**(C) 为何当时未发现:「零残留」的校验集不完整。**
§3.4 声称的「零证伪残留」只校验了 `.wav` + `.wav.meta` 的 SHA-256。
`unity/Assets/AddressableAssetsData/` 在 `.gitignore:85` 被**整目录忽略**
⇒ `git status` **不会**提示它变了。**两重盲区叠加 ⇒ 泄漏不可见。**

**(D) 泄漏的实际影响(已修复 + 已验证)。**

| 项 | 影响 |
|---|---|
| fz2 的判读 | ❌ **被污染** ⇒ 已重做(§3.5.2) |
| 证伪轮一(§3.3)的结论 | ✅ **未污染** —— 其 15 红中**无一条** `test_d5_*` ⇒「D6 存在性门硬失败」的判读**仍然成立** |
| 修复 | 跑**生产菜单项** `DataBakeMenu.EnsureAudioGroup`(幂等补齐)⇒ `[大医精诚] audio 组就绪:5 个素材已入组并打标签` |
| 修复后回归 | ✅ **`395/395/0`**(`unity/Logs/s15-final.xml`) |

**(E) 手法教训(须用于后续证伪轮)。**
⚠️ **「真删工程内资产」这一变异手法会触发 Unity 的资产维护,污染机器本地产物**
(`AddressableAssetsData` 等)。⇒ 后续此类证伪:
① **校验集须包含**被引擎间接改写的产物;
② 或改用**不触碰真实资产**的变异(如注入假根 —— 既有夹具已是此形,`test_d6_missingAsset_*` 即如此)。
⚠️ **本条不改变 AC-44-D6c 的结论**(硬失败语义已由 15 红证实),**只更正过程记录**。

---

## §3.6 AC-44-D15 Load Type 定档 —— `OQ-44-2` 结案的兑现(2026-10-17)

### §3.6.1 枚举真值(反汇编实测,非记忆)

```bash
monodis --output=/tmp/audio_il.txt \
  /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll
```
实测输出:
```
.class public auto ansi sealed AudioClipLoadType
  .field public static literal valuetype UnityEngine.AudioClipLoadType DecompressOnLoad   = int32(0x00000000)
  .field public static literal valuetype UnityEngine.AudioClipLoadType CompressedInMemory = int32(0x00000001)
  .field public static literal valuetype UnityEngine.AudioClipLoadType Streaming          = int32(0x00000002)
```

### §3.6.2 改动(单一 diff 行)

```
unity/Assets/Audio/music_encounter_layer_loop.wav.meta
8c8
<     loadType: 0
---
>     loadType: 2
```
⚠️ **过程中我一度越权改了 `compressionFormat`(1 → 0),已立即回退** ——
裁定只授权改 Load Type。最终 diff **仅一行**,已核。
其余 4 个 `.meta` **一字未动**(`vo_*` ×2 · `sfx_breath_*` ×2 保持 `loadType: 0`)。

### §3.6.3 判据(GDD §OQ-44-2 附的落地)

`audio.md:213-222` 只给语义三档、**无秒数阈值** ⇒ 自建可复算判据:
**驻留 = `帧数 × 声道 × 位深/8`**;`≤ 512 KB ∧ 时长 < 5 s` ⇒ `DecompressOnLoad`。

| 文件 | 时长 | 驻留 | 判定 | `.meta` |
|---|---|---|---|---|
| `music_encounter_layer_loop.wav` | 23.3160 s | **2008.3 KB** | Streaming(语义档点名 music + loop 长驻) | **2** ✅ |
| `sfx_breath_base_loop_small.wav` | 1.9500 s | 168.0 KB | DecompressOnLoad | 0 ✅ |
| `sfx_breath_adventitious_fine.wav` | 1.9500 s | 168.0 KB | DecompressOnLoad | 0 ✅ |
| `vo_patient_cough_damp_f1.wav` | 1.5840 s | 136.5 KB | DecompressOnLoad | 0 ✅ |
| `vo_patient_cough_damp_m1.wav` | 1.2480 s | 107.6 KB | DecompressOnLoad | 0 ✅ |

### §3.6.4 Unity 是否接受该 `.meta`(覆盖风险实测)

`File.Exists` 式的「改了就算」**不算验证** —— `.meta` 由导入器持有,手改可能被回写覆盖。
实跑一次 batch 导入验证:
```
导入前 .meta sha : 9f982f2764520da1796bbdc0766db8ef873dbb806fe24e5ba9395b8c92dc0755
导入后 .meta sha : 9f982f2764520da1796bbdc0766db8ef873dbb806fe24e5ba9395b8c92dc0755   ✅ 未变
grep loadType    : loadType: 2
```
⇒ **设置被引擎接受,未被覆盖**。
**日志**:`unity/Logs/s15-import.log`

---

## §4 欠账 / NOT-RUN(禁借绿)

| # | 项 | 状态 | 说明 |
|---|---|---|---|
| **①** | **Import Load Type pin** | ✅ **已定档(2026-10-17)** | 承 `OQ-44-2` 结案。`music_encounter_layer_loop.wav` `.meta` 由 `loadType: 0` 改 **`2`(Streaming)**(diff 仅此一行);其余 4 个保持 `0`(`DecompressOnLoad`,现值即正确档)。**判据可复算** = GDD `audio-system.md` §OQ-44-2 附(驻留 ≤ 512 KB ∧ 时长 < 5 s ⇒ 小档)。**Unity 导入回写实测**:batch 导入后 `.meta` SHA-256 不变 ⇒ **设置被引擎接受,未被覆盖**。⚠️ 残留口径张力:`music_*` 按时长(23.3 s)属中档判据、按驻留(2008 KB)超出小档 ⇒ 已在附节登记「Music 素材改短须重裁」 |
| **②** | **`AudioAssetExistence.Missing` 的保序去重语义未反证** | ✅ **已反证(2026-10-17)** | 见 §3.5 —— 停用 `seen.Add` 去重 ⇒ `test_d6_missing_isOrderPreservingAndDeduped` **转红** |
| **③** | **`AudioAssetGroupGates.ValidateEntries` 的 `data-core` 不相交判据未反证** | ✅ **已反证(2026-10-17)** | 见 §3.5 —— 停用 `inDataCore` 判据 ⇒ `test_d5_pure_audioAssetInDataCoreGroup_rejected` **转红**(首轮误报的连带红已更正,见 §3.5.4) |
| **④** | **四项听测** | ✅ **已跑(2026-10-17 用户签核)** | 见 §4.1 —— 四项均由**用户**试听判定;本题**不自行**判定音频内容 |
| **⑤** | **真 `AudioClip.loadState` 的 PlayMode 半边** | ❌ **NOT-RUN(承 S14-6 裁定 ③)** | 缺口哨兵 `test_preload_realAudioClipLoadState_pendingPlayModeHalf` 已钉;**素材落地后须重新裁定**,不得当回归修掉 |
| **⑥** | **IL2CPP 三格矩阵**(ADR-012) | ❌ **NOT-RUN** | 本机仅 Mono |
| **⑦** | **素材源规格上限(登记不隐藏)** | ⚠️ **已知内容损失** | ④ 源为**中湿罗音**(22050 Hz / 32 kbps)经**用户医学裁定认定为细湿啰音** —— 听感上更靠近 medium;升采样至 44.1 kHz **不恢复 >11 kHz 内容**。①②⑥ 同样自 24 kHz 升采样 |
| **⑧** | **`assets/audio/` → `unity/Assets/Audio/` 路径订正** | ✅ 已落(S14-6) | GDD `:258` / `:695` / `:1092` + ADR-018 §五 `:153` 已就地加注(承 ADR-015 修订 ADR-009 先例) |

### §4.1 听测登记(四项 · 用户签核 2026-10-17)

> **登记口径(禁借绿)**:以下为**用户主观试听**判定,原话「听了没问题」。
> AI **不具备**判听能力 ⇒ 本表**不是** AI 的验证结论,而是**用户签核的转述**;
> 每项所对应的**客观判据**(如「非连续水声」)仍**无仪器证据**。

| # | 素材 | 待判事项 | 关联 AC | 用户判定 |
|---|---|---|---|---|
| ① | `vo_patient_cough_damp_m1.wav` | 单次咳嗽 · 闷 · 无混响尾 · 无削波 | — | ✅ 无问题 |
| ② | `vo_patient_cough_damp_f1.wav` | **与 ① 是否两个人**(不得同一人变调) | **`AC-44-06` BLOCKING** | ✅ 无问题 ⇒ **视为不同人** |
| ③ | `sfx_breath_base_loop_small.wav` | 是「安静呼吸」而非「喘」 | `TR-audio-005` | ✅ 无问题 |
| ④ | `sfx_breath_adventitious_fine.wav` | 是**离散噼啪**而非**连续水声** | `AC-44-08` · `TR-audio-005` | ✅ 无问题 |

⚠️ **② 的 BLOCKING 解除依据 = 用户口头签核**(「听了没问题」)。
**无客观分离度证据**(未做两人声纹/基频比对)⇒ 该 BLOCKING 以**用户裁定**结清,
**不以技术证据**结清。若后续需要可证伪的凭据,须另立声学比对项。

### §4 附:story-015 四 AC 的当下可判性

| AC | 当下判决 | 依据 |
|---|---|---|
| **AC-44-D6b**(5 个被引 wav 真实存在;门对真素材跑过一次) | 🟢 **成立** | §2.1 差集零 + §2.4 `test_d6_realSeed_bakesWithNoMissingAsset` 绿 |
| **AC-44-D6c**(删任一 ⇒ 构建期硬失败) | 🟢 **成立(真素材已复验)** | §3.2 反证 15 红 + §3.4 还原校验 |
| **AC-44-D15**(落地 + 入 audio 组 + 与 data-core 区分 + **Load Type pin**) | 🟡 **部分** —— 前三者 🟢(§2.3),**Load Type pin 🔴 未做**(§4 ①) | 同上 |
| **AC-44-D16**(双向差集归零) | 🟢 **成立** | §2.1 |

⇒ **AC-44-D15 的「Import Load Type pin」子句未满足**,故 **story-015 不得转 `Complete`**。
**本件不签绿 story-015。**

---

## §5 交付面

| 类别 | 文件 | 内容 |
|---|---|---|
| **素材(替换)** | `unity/Assets/Audio/music_encounter_layer_loop.wav` | 战斗遭遇乐层 · 23.3160 s · 44.1 kHz/16-bit/mono · 切 0.1 s 头 + 2000 ms 交叉淡化 |
| **素材(替换)** | `unity/Assets/Audio/sfx_breath_adventitious_fine.wav` | 细湿啰音(源为中湿罗音,经裁定认定)· 1.9500 s · 源 9.25–11.25 s + 50 ms 交叉淡化 |
| **素材(替换)** | `unity/Assets/Audio/sfx_breath_base_loop_small.wav` | 呼吸基底层 · 1.9500 s · 源 0–2 s + 50 ms 交叉淡化 |
| **素材(替换)** | `unity/Assets/Audio/vo_patient_cough_damp_f1.wav` | 女声压抑咳嗽 · 1.5840 s |
| **素材(替换)** | `unity/Assets/Audio/vo_patient_cough_damp_m1.wav` | 男声压抑咳嗽 · 1.2480 s |
| **移出工程** | `/home/gu/文档/nm/nm2/素材原始/*.mp3` ×5 | 原始 MP3(SHA-256 已核) |

**未改动**:`assets/data/audio_events.json` · `audio.asset` · `data-core.asset` ·
全部 `.wav.meta`(由 Unity 导入器持有,`loadType: 0`)· 任何 `.cs`。

---

## §6 复现命令

```bash
# §2.1 双向差集
python3 -c "import json,glob,os; t=json.load(open('assets/data/audio_events.json')); \
r={a for x in t['rows'] for a in x.get('assets',[])}; d={os.path.basename(p) for p in glob.glob('unity/Assets/Audio/*.wav')}; \
print('missing:',r-d,'orphan:',d-r)"

# §2.2 规格 + 哈希
sha256sum unity/Assets/Audio/*.wav
python3 -c "import wave,glob,os; [print(os.path.basename(p), wave.open(p).getframerate(), wave.open(p).getnframes()) for p in sorted(glob.glob('unity/Assets/Audio/*.wav'))]"

# §2.3 组分离
grep -c "Assets/Audio" unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset   # 期望 0

# §2.4 全量回归
UNITY=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
"$UNITY" -batchmode -nographics -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Unit.Audio" -projectPath "$(pwd)/unity" \
  -testResults "$(pwd)/unity/Logs/s15-audio.xml" -logFile "$(pwd)/unity/Logs/s15-audio.log"

# §3 证伪轮(真删 → 跑 → 还原,同一命令内)
V=unity/Assets/Audio/vo_patient_cough_damp_m1.wav
cp -f "$V" /tmp/bak.wav && cp -f "$V.meta" /tmp/bak.wav.meta \
  && rm -f "$V" "$V.meta" \
  && "$UNITY" -batchmode -nographics -runTests -testPlatform EditMode \
       -testFilter "DaYiJingCheng.Tests.Unit.Audio.PipelineGateTest" \
       -projectPath "$(pwd)/unity" -testResults "$(pwd)/unity/Logs/s15-fz.xml" \
       -logFile "$(pwd)/unity/Logs/s15-fz.log" ; \
  cp -f /tmp/bak.wav "$V" && cp -f /tmp/bak.wav.meta "$V.meta" \
  && sha256sum "$V"   # 期望 4b8e91447b7c4f1034077b350d07b87732beeb229f18e915410b47c48e6dbef4
```

⚠️ **Unity batch 独占工程** ⇒ 跑前先 `pgrep -f "6000.3.24f1/Editor/Unity"` +
查 `unity/Temp/UnityLockfile`;跑完可重开编辑器。

---

## §7 结论

| 项 | 判定 |
|---|---|
| **素材本体落地** | ✅ **完成**(5 个真实内容替换占位静音,规格/哈希见 §2.2) |
| **AC-44-D16 双向差集** | ✅ **成立** |
| **AC-44-D15 组分离** | ✅ **成立** |
| **AC-44-D6b 真实存在** | ✅ **成立** |
| **AC-44-D6c 硬失败 + 判别力** | ✅ **成立**(真素材复验,反证 15 红,还原逐字节校验) |
| **未反证守卫 ②③ 补反证** | ✅ **两处均转红**(§3.5,反证 **2 红**;首轮误报 3 红已更正 §3.5.4) |
| **证伪轮副作用泄漏** | ⚠️ **已发生 + 已修复 + 已登记**(§3.5.4)—— `audio.asset` 曾 5→4 条,已由 `EnsureAudioGroup` 补齐并回归验证 |
| **AC-44-D15 Load Type pin** | ✅ **已完成**(§4 ①,2026-10-17 承 `OQ-44-2`) |
| **四项听测** | ✅ **用户签核无问题**(§4.1) |
| **全量回归** | ✅ **395/395/0**(`PipelineGateTest` 52/52) |
| **story-015 状态** | ⬜ **仍 `Ready`** —— 四条 AC 判据已**全部落地**;转 `Complete` 须经 `/story-done` 流程 |

**未反证守卫:0 处**(§4 ②③ 已于 2026-10-17 补反证转红)。
**未跑项 2 类**:§4 ⑤(PlayMode `loadState` 半边,解除条件 = 12 个 vo_ 变体落地)· ⑥(IL2CPP 三格)。
**本件不声称已复核全部 AC 的可判性。**
