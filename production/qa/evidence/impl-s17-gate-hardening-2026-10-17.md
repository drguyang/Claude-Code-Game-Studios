# story-017 音频门加固 —— 实现证据原件

> **对象**:`production/epics/audio-system/story-017-audio-gate-hardening.md`
> **AC**:AC-44-D5b(跨平台大小写)· AC-44-D5c(≤40 行 / CC<10)· AC-44-D5d(错误集逐字不变)
> **日期**:2026-10-17
> **来源判定**:`production/qa/evidence/review-s15-code-2026-10-17.md` §三 3.1 的 **W-2 / W-3**
> (story-015 收口轮代码评审产出;两条落在 **story-010 的既有门代码**上,非 story-015 引入)
> **本件性质**:BLOCKING 判定的**修复原件**(原判定 → 修复落点 → 验证命令,逐条可证伪)。

---

## §一 原判定 → 修复落点 → 验证命令

| # | 原判定(story-015 评审) | 修复落点(文件:行) | 验证命令 / 判据 |
|---|---|---|---|
| **W-2** | `Directory.GetFiles(dir, "*.wav")` **大小写敏感** —— Linux(CI 平台,ADR-012)下 `.WAV` 漏扫 ⇒ 盘上有却判「不存在」,**跨平台行为分叉** | ① `Editor.Tools.Gates/AudioAssetGroupGates.cs:85` 新增纯谓词 `IsAudioAssetName`(**判据单一出处**);② 同文件 `:103` 新增纯函数 `FilterAudioNames`(映射半边);③ 同文件 `:269` `AudioAssetsOnDisk` 改为 `FilterAudioNames(Directory.GetFiles(dir, "*"))`;④ `Editor.Tools.Bake/AudioImportSettingsGate.cs:259` `IsWavName` 改为**委派**到 ① | `unity test unity --mode EditMode --filter PipelineGateTest` ⇒ `test_d5b_audioAssetName_extensionCaseInsensitive` / `test_d5b_filterAudioNames_threeCaseFormsAllKept` / `test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob` 三条绿;证伪轮 **F5 / F6 / F10** 三条反证 |
| **W-3** | `ValidateEntries` **45 行 / 圈复杂度 15** —— 双超标(标准 = ≤40 行 / <10) | `Editor.Tools.Gates/AudioAssetGroupGates.cs:132` 主方法退化为**编排**;两个判据半拆为私有方法 `CheckExpectedAssetsAreGrouped`(`:144`)/ `CheckDataCoreHasNoAudioEntries`(`:177`) | 同上 ⇒ `test_d5c_validateEntries_withinLineAndComplexityBudget` 绿(**含两半各自的 ≤40 行断言**);证伪轮 **F7 / F8** 两条反证 |

### 实测值(同一口径,可复算)

**口径 = 注释剥离后,「签名所在行首 → 闭括号所在行尾」的物理行数;CC = 1 + 决策点
(`if`/`for`/`foreach`/`while`/`case`/`catch` 按词界 + `&&` + `||`)。** 该口径即夹具
`test_d5c_*` 内 `ExtractMethodText` + `CountDecisionPoints` 的口径。

| 方法 | 加固前(HEAD) | 加固后(工作树) | 界 |
|---|---|---|---|
| `ValidateEntries` | **48 行 / CC 15** | **10 行 / CC 2** | ≤40 / <10 ✅ |
| `CheckExpectedAssetsAreGrouped` | 不存在 | 29 行 / CC 9 | ≤40 ✅ |
| `CheckDataCoreHasNoAudioEntries` | 不存在 | 13 行 / CC 6 | ≤40 ✅ |

> ⚠️ **口径差须写明,不得含糊**:story-017 卡面与评审原件记的是「**45 行**」,本件实测 **48 行**。
> 差 3 行 = **口径不同**(卡面数的是方法体,本件含签名行与续行参数)。**两个数都 > 40**,
> 故「夹具能否抓住加固前状态」这一判断不受影响 —— 但**不得**把两个数混着引用。
> CC 一侧两处**一致(15)**。

### 为什么真源搬到 `Gates`(而非两处各留一份)

装配引用是 `Editor.Tools.Bake` → `Editor.Tools.Gates` **单向**(`Editor.Tools.Bake.asmdef`
的 `references` 含 `Editor.Tools.Gates`;反向不含)。`Gates` 物理上够不着 `Bake`,
故判据真源必须住**下层** `Gates`,由 `Bake` 委派。两处各留一份 = 「另抄一份判据」,
与本仓「单一出处」纪律冲突。

**可观测形态**(F5 反证):停用 `IsAudioAssetName` 的 `OrdinalIgnoreCase` ⇒
**三条夹具同时红**(谓词侧 / 映射结果侧 / 委派侧)—— 一处改,三处账本同时翻。

### 为什么映射半边要抽纯函数(`FilterAudioNames`)

story-017 的 QA 用例逐字要求:

> **AC-44-D5b** · Given:`unity/Assets/Audio/` 内存在 `.wav` 与 `.WAV` 两种大小写副本
> → When:`AudioAssetsOnDisk()` → Then:两者**均被返回**

首版实现只钉住了**谓词**与**调用点源码**,对「映射结果」只有**复合论证**
(谓词对 ∧ 调用点用了谓词),**没有直接证据**。补法不是往盘上造 `.WAV` 副本 —— 副本会
同时进 D5 的期望集与 D6 的存在性判据(**假红**),且违夹具「零文件写入」纪律
(`test_discipline_noFilesystemWrites` 自证)。故把映射半边抽为**纯函数**,夹具注入路径
直断三形态。

---

## §二 AC-44-D5d —— 加固前后错误集对照(机械比对,非散文)

**判据**:对同一输入,加固前后 `ValidateEntries` 的返回错误集**逐字相同**。

**做法**:从 `HEAD` 版本与工作树版本各自抽取**全部 `errors.Add(...)` 报文**
(取其中所有字符串字面量并拼接,消除换行/缩进/`+` 拼接形式的影响),比对**多重集**。

**命令(可复算)**:

```bash
python3 - <<'PY'
import re, subprocess, io
p = "unity/Assets/Editor.Tools.Gates/AudioAssetGroupGates.cs"
before = subprocess.run(["git","show","HEAD:"+p], capture_output=True, text=True).stdout
after  = io.open(p, encoding="utf-8").read()
def msgs(src):
    return ["".join(re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(1)))
            for m in re.finditer(r"errors\.Add\((.*?)\);", src, re.S)]
b, a = msgs(before), msgs(after)
print(len(b), len(a), sorted(b) == sorted(a))
PY
```

**输出**:

```
加固前报文数 = 7
加固后报文数 = 7
逐字相同(集合相等): True
```

**加固后 7 条报文逐字**(与加固前一致):

1. `[D5] 条目集为 null —— 空集冒充绿`
2. `[D5] 音频素材「{asset}」登记在 `data-core` 组 —— AC-44-D5:素材与 `data-core` 数据烘焙组**必须区分**(混组 = 流式素材被打进数据包)`
3. `[D5] 音频素材「{asset}」未登记进任何 Addressables 组 —— AC-44-D5:素材须走 Addressables 流式(未登记 = 打进主包 / 不可寻址)。跑菜单「确保 audio Addressables 组」`
4. ``[D5] `data-core` 组含音频目录条目「{entries[e].AssetPath}」—— AC-44-D5:组区分(该文件不在期望集内,仍是混组)``
5. `[D5] 工程内音频目录无任何 .wav 素材 —— 扫描键落空 = 假绿(AC-44-D6 的正向半边也依赖这些素材存在)`
6. `[D5] AddressableAssets 设置不存在 —— 本机未初始化。…`
7. `[D5] Addressables 设置里零条目 —— 扫描键落空 = 假绿;…`

**另**:夹具侧 `test_d5d_errorSetIsByteIdentical` 以**黄金串表**钉住**条数 + 逐字文本 + 顺序**
(6 例,含「两半同时触发 ⇒ (a) 报文集在前、(b) 在后」的次序断言)。反证 = **F9**(只改一个字符
⇒ 其余「非空 / 含关键字」断言照旧全绿,唯黄金串表转红)。

---

## §三 证伪轮(BLOCKING · 驱动 `tools/falsify/unity_falsify.py`)

**命令(可复算)**:

```bash
python3 tools/falsify/unity_falsify.py \
  --spec tools/falsify/specs/audio-import-settings-and-playmode.json --keep-logs
```

**结果:`FALSIFY_EXIT = 0` —— 10 个变异全部 `exactRed: true` 成立。**
`exactRed` 的语义是**红集恰好等于**预期集(**不是**「至少包含」)—— 它同时抓两种失败:
少红(夹具判别力不足)与**多红**(变异打到了无关断言)。逐个施加、逐个还原,**不叠变异**。

| # | 变异(停用什么) | 落点 | 期望红 | **实红** | pass |
|---|---|---|---|---|---|
| **F1** | `RequiredLoadType` 的 `music_` 语义档覆盖 | `AudioImportSettingsGate.cs:75` | 3 | **3** | 71 |
| **F2** | 小档闭区间 `<=` → `<`(512 KB / 5 s) | `AudioImportSettingsGate.cs:83` | 1 | **1** | 73 |
| **F3** | `FindAssetDatabaseBuilderIndex` 的 `-1` → `0` | `DataBakeMenu.cs:839` | 1 | **1** | 73 |
| **F4** | 不可读 `.meta` ⇒ 静默归零 | `AudioImportSettingsGate.cs:221` | 1 | **1** | 73 |
| **F5** | `IsAudioAssetName` 的 `OrdinalIgnoreCase`(**W-2 修复点**) | `AudioAssetGroupGates.cs:89` | 3 | **3** | 71 |
| **F6** | `AudioAssetsOnDisk` 枚举回退为 glob(**W-2 调用点**) | `AudioAssetGroupGates.cs:275` | 1 | **1** | 73 |
| **F7** | `ValidateEntries` 行数预算(**W-3**) | `AudioAssetGroupGates.cs:138` | 1 | **1** | 73 |
| **F8** | `ValidateEntries` 圈复杂度预算(**W-3**) | `AudioAssetGroupGates.cs:139` | 1 | **1** | 73 |
| **F9** | 错误报文文案漂移(**AC-44-D5d**) | `AudioAssetGroupGates.cs:136` | 1 | **1** | 73 |
| **F10** | 映射半边跳过谓词(**单一出处**) | `AudioAssetGroupGates.cs:110` | 4 | **4** | 70 |

**逐例红集(实测,`unity/Logs/falsify-<变异名>.xml` 解析所得)**:

| 变异 | 恰转红的测试 |
|---|---|
| **F1** | `…requiredLoadType_matchesGddTable` · `…musicPrefix_overridesNumericTier` · `…realMetas_matchRequiredTier` |
| **F2** | `…boundaries_areInclusiveExclusiveAsSpecified` |
| **F3** | `test_playModeScript_predicate_nullOrMiss_returnsMinusOne` |
| **F4** | `…unreadableInput_isViolationNotSilentSkip` |
| **F5** | `test_d5b_audioAssetName_extensionCaseInsensitive` · `test_d5b_filterAudioNames_threeCaseFormsAllKept` · `test_importSettings_wavExtension_isCaseInsensitiveAcrossPlatforms` |
| **F6** | `test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob` |
| **F7** | `test_d5c_validateEntries_withinLineAndComplexityBudget` |
| **F8** | `test_d5c_validateEntries_withinLineAndComplexityBudget` |
| **F9** | `test_d5d_errorSetIsByteIdentical` |
| **F10** | `test_d5_audioAssetsOnDisk_nonEmpty` · `test_d5_realSettings_audioAssetsSeparateFromDataCore` · `test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob` · `test_d5b_filterAudioNames_threeCaseFormsAllKept` |

### 三条须写明的话(禁含糊)

**① F5 红 3 条 = 「单一出处」的可观测形态,不是夹具冗余。**
W-2 修复后判据**只有一条实现**(`AudioAssetGroupGates.IsAudioAssetName`);
`AudioImportSettingsGate.IsWavName` 改为**委派**。停用 `OrdinalIgnoreCase` ⇒
**谓词侧 / 映射结果侧 / 委派侧三条账本同时翻** —— 这正是把真源收在一处要换来的东西:
改一处,所有消费点同步可见。**若两处各留一份判据,F5 只会红 1~2 条,漏掉的那条就是漂移面。**

> ⚠️ **F5 的锚点本轮迁移过**:原锚 `AudioImportSettingsGate.cs:258`(那份自带的 `IsWavName`)
> **已随委派化消失** —— 锚点若不跟着搬,证伪轮会「施加成功但改的是死代码」⇒ 静默 0 红。
> 本件已迁至新真源 `AudioAssetGroupGates.cs:89`。

**② F6 是 F5 的补集,缺一不可。**
纯函数留着、谓词也还在,只把**枚举**换回 glob ⇒ F5 那三条**仍全绿**
(素材目录恰好只有小写 `.wav`,glob 也能扫到)⇒ 只有**源码扫描**那条能看见它。
**这就是 `test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob` 存在的唯一理由。**

**③ F10 的红集比首轮预测多 1 条 —— 如实登记,不是把判别面往夹具上凑。**
首轮预测只写了 3 条,实测 **4** 条。复核后确认第 4 条
(`test_d5_realSettings_audioAssetsSeparateFromDataCore`)是**同一根因的直接后果**:
过滤一撤,`ValidateSettings` 的期望集本身被污染(`Assets/Audio/` 下的 `.mixer` / `.meta`
被当成素材),正向半边**必然**报它们「未登记进任何 Addressables 组」。
**这是正确的红,不是打到无关断言** ⇒ `expectRed` 已按实测订正为 4。
**预测不全这件事本身已登记在此**,不掩盖。

### 还原纪律(两条判据,均由驱动自动执行)

| 判据 | 结果 |
|---|---|
| 全部变异还原后 **SHA-256 逐字节校验** | ✅ **通过**(任一文件与施加前不一致 ⇒ 驱动自身崩,不会静默放过) |
| **受保护路径指纹未变**(证伪轮不留痕) | ✅ 未变 —— `unity/Assets/AddressableAssetsData` · `unity/Assets/Audio` |

> ⚠️ **驱动边界(承前,不隐藏)**:它守的是**路径**不是**语义** —— 污染写到守卫路径
> **之外**即看不见;且只保证**被检出**,不保证**不被写入**。
> 本件 10 个变异的落点全部是**源码文件**(`Editor.Tools.*` 三支),**零文件系统副作用**,
> 故该边界在本件不构成缺口。

---

## §四 回归

**实测(非预测)**:

| 轮次 | 结果 XML | 范围 | `total/passed/failed/skipped` | 对账 |
|---|---|---|---|---|
| 本轮改动后 · 过滤 | `unity/Logs/s22-filtered.xml` | `--filter PipelineGateTest` | **`74 / 74 / 0 / 0`** | 上轮 **69** ⇒ **+5**(本轮 5 条 `d5*` 夹具) |
| | | | | ✅ 对账成立(XML 差集 5 条 = 本轮新增)—— 见下方「计数订正」 |
| 本轮改动后 · 全量 | `unity/Logs/s22-full.xml` | 全量 EditMode | **`3635 / 3590 / 0 / 45`** | 基线 `s19d-full-final.xml` = `3630 / 3585 / 0 / 45` ⇒ **Δtotal = +5**,恰等于新增测数 |

**命令(可复算)**:

```bash
unity test unity --mode EditMode --filter PipelineGateTest --output unity/Logs/s22-filtered.xml
unity test unity --mode EditMode --output unity/Logs/s22-full.xml
```

> **判据(禁借绿)**:`failed = 0` **且** `total − 前次 total = 新增测数`。两个条件同时成立才算零回归;
> 只报 `failed = 0` 不构成对账。
> ✅ **计数订正(2026-10-17 · 三次核对,结论:本节成立,补充口径说明)** ——
> 头两版订正(两版说法相反)都不完整;现按**两份 XML 的逐 fullname 差集**定案,这是唯一自洽的读法:
>
> | 量 | 基线 `s19d-full-final.xml` | 本轮 `s22-full.xml` | 差额 |
> |---|---|---|---|
> | 全量 EditMode `test-run total` | **3630** | **3635** | **+5** |
> | 其中 `PipelineGateTest` 用例 | **69** | **74** | **+5** |
> | 其中 D5/D6 面 | 18 | 23 | +5 |
>
> **逐 fullname 差集(可复算)**:`s22` 相对 `s19d` **新增 5 条、删除 0 条**,新增的恰是
> 本轮的 5 条 `d5*`(`d5b` ×3 / `d5c` ×1 / `d5d` ×1)。命令:
>
> ```bash
> python3 -c "import re,sys;f=lambda p:set(m.group(1) for m in re.finditer(r'<test-case\\b[^>]*?fullname=\"(DaYiJingCheng\\.Tests\\.Unit\\.Audio\\.PipelineGateTest\\.[^\"]+)\"',open(p,encoding='utf-8').read()));print(sorted(f('unity/Logs/s22-full.xml')-f('unity/Logs/s19d-full-final.xml')))"
> ```
>
> ⚠️ **口径陷阱(记下来)**:同一时刻 `pipeline_gate_test.cs` 的**源码**里已有 74 个
> `public void test_`,但基线 XML 记的是 69 —— 差的 5 条,**其中 4 条**(`d5b` ×3 + `d5c`)
> **早就在源码里**,只是 `s19d` 那次全量跑时**未被采集**;只有 `d5d` 是采集之后新增的。
> ⇒ **「HEAD 源码测数」≠「基线 XML 用例数」,两者不能直接相减**。对账只认**同一采集方式**的两份 XML。
> 本节原写的「+5 恰等于新增测数」**成立**,依据是 XML 差集(5 条),不是源码行数差。
>
> **d5_menu_groupMissing_throwsHard 不在本轮 5 条内** —— 它是 TD-audio-01 补口轮落的
> (见 `impl-td-audio-01-fix-2026-10-17.md`),已被 `s19d` 采集到。这也是 D5/D6 面
> 「18 既有」的由来 —— **18 是 `s19d` 采集时点**的量,`pipeline_gate_test.cs` 在 HEAD 的
> **源码**里是 17,差的 1 条正是它。
>
> **`skipped = 45` 与基线逐位相同** —— 本轮**未新增也未消化**任何 skip;
> 45 条 skip 的既有归属不变(不属本件面内,不得借读为「本轮清掉了一些」)。

> 🔴 **2026-10-17 复跑(内容批接入后)曾红 3 条 —— 已修,现已全绿。原判定不追认,逐条记如下**:
>
> | 测试 | 曾红原因(实测) | 修法 | 状态 |
> |---|---|---|---|
> | `test_d6_realSeed_bakesWithNoMissingAsset` | 断言「真种子**恰好 5 行**」,而 34 个素材接入后表已 15 行 | 改为 **≥15 下界** + 「烘焙行数 == 源 `rows[]` 行数」自洽断言 | ✅ |
> | `test_d6_missingAssetError_namesFileAndCue` | 夹具拿**已退役并删除**的 `vo_patient_cough_damp_m1.wav` 去 `Replace` ⇒ **空转** ⇒ 烘焙成功 ⇒ 期望 throw 实得 null | 改为**从真种子取第一个素材名**再替换,并断言前提非空 | ✅ |
> | `test_preload_fromTable_derivesStethoscopeFamily` | 硬编码「听诊族恰好 2 条」;内容批给 `Pulse_Contact` / `Steth_Contact` 也选了 `Stethoscope`(承 GDD `:810`) | 改为**从真种子独立推出期望集**(与被测门不同源),按**集合**比 | ✅ |
>
> **撰写者须知**:`ExpectedStethoscopeCues` 的期望集推导**前两版用正则切行,两版都漏行** ——
> ① `[^{}]*"cue"[^{}]*` 在 `assets[]`(嵌套数组)前断;② `[^{}]*` 命中
> `adventitious_policy.{trigger_phase,jitter}` 嵌套块,把行的 cue 与子块的 bus 配了对。
> 正解 = **顶层花括号深度扫描**(跳过字符串字面量)。**这条已固化为代码注释。**
>
> **复跑实测**:
> - 过滤 `unity/Logs/s23f-filtered.xml` = **`74 / 74 / 0 / 0`**,exit 0
> - 全量 `unity/Logs/s23g-full.xml` = **`3635 / 3590 / 0 / 45`**,exit 0
> - 与上一版 `s22-full.xml` 逐 fullname 差集 = **新增 0 / 删除 0**(本轮只改测试实现,不改用例集合)

### 编译面附带校验(非本件 AC,但同批改动)

本轮同时改动了三处 **Editor 工具程序集**源码(`Editor.Tools.Spike.asmdef` +
`U1SpikeSetup.cs` + `DataBakeMenu.cs` 文档注),归 **`TD-audio-01f` 根因①**
(「Play Mode Script 漂移」)的 Teardown 复原项,与 story-017 的 AC **不合并记账**。
上述两次测试跑即为其编译面校验 —— **两次均通过**(若 asmdef 引用边写错,整棵
`Editor.Tools.Spike` 装配编译失败,`--filter PipelineGateTest` 会直接崩而非出 74 绿)。

---

## §五 未做 / 登记项(禁借绿)

| # | 事项 | 处置 |
|---|---|---|
| 1 | **W-4 / W-5 / W-7**(组判据弱于 AC 语义 · `EnsureAudioGroup` 自举 · `CreateGroup` 全新 clone 路径) | **本件不碰** —— 用户 2026-10-17 裁定归 **`TD-audio-01`**(与 W-3 同处 `ValidateEntries`,避免同一方法被两条工作流同时改) |
| 2 | **W-1 / W-6**(`music_*` 的 `loadType` 判据张力) | **不改** —— GDD §OQ-44-2 附已覆盖 |
| 3 | **点文件策略不对称**(本件**发现但刻意未改**) | `FilterAudioNames` 剔除 `.` 开头的编辑器临时文件;`AudioImportSettingsGate.Validate` **不**剔除 ⇒ 同一目录下 `.hidden.wav` 会被导入门报违例、被组门忽略。**这是两门语义选择,不是矛盾**,且不在 AC-44-D5b 面内(该 AC 只管扩展名大小写)⇒ **登记不动**,归两门所有者 |
| 4 | **story-017 卡面的陈旧计数** | ✅ **已订正(三次核对定案)**。卡面原写「既有 **14** 条 D5/D6 测」(`:67`)与「既有 **52** 测」(`:142`)。**两个口径须分清**:① **HEAD `d21e43a` 源码** = 总 **52** 测 / D5/D6 **17**(D5 10 · D6 7);② **基线 XML `s19d-full-final.xml` 采集时点** = `PipelineGateTest` **69** 用例 / D5/D6 **18**。差的 1 条(`test_d5_menu_groupMissing_throwsHard`,TD-audio-01 补口轮落的)与另外 4 条 `d5b`/`d5c` 都在源码里、但 `s19d` 未采集到。**写实现时数源码(17),对账回归时数 XML(18)**。卡面「52 → 61 → 68 → 69」那条演进序列是**把两种口径缝成了一条曲线**,已订正为「源码口径只有 11 → 52 → 52 三点 / XML 采集口径 61 → 68 → 69 → 74」。 |
| 7 | **`U1SpikeSetup` 的 Teardown 复原(用户裁定 #11)** | ✅ **本轮已落**。`Editor.Tools.Spike.asmdef` +`Editor.Tools.Bake`(单向)⇒ `Teardown` 的 `finally` 委派 `DataBakeMenu.EnsureAssetDatabasePlayMode` 复原。**⚠️ 与 story-017 的 AC 不合并记账**(它归 `TD-audio-01f` 根因①);**无独立证伪变异**(该件无夹具面 —— 见下条) |
| 8 | **`U1SpikeSetup` Teardown 复原的判据缺口** | ⚠️ **如实登记** —— 该改动**没有夹具钉住**:`Teardown` 是 Unity 编辑器生命周期回调,EditMode 下不触发 ⇒ **判别力载体只能是「源码扫描」或「PlayMode 手跑」**,本件两者**均未做**。⇒ 按 `review-workflow.md` Phase 8b 双列纪律,该项标 **`Landed = ✅ / Proven = ✗`**,**不得标已证**。补法归 `TD-audio-01f` 的后续轮 |
| 5 | **PlayMode 半边** | 本件 **Story Type = Integration**,但三处改动全在 **Editor 工具程序集**(`includePlatforms: ["Editor"]`)⇒ 无 PlayMode 半边可跑。**不是「未跑」,是「不适用」** |
| 6 | **`Editor.Tools.Gates.asmdef` 复核** | ✅ `includePlatforms: ["Editor"]` **未变**(story-017 §Implementation Notes 5);本件**未改任何 asmdef** |

---

## §六 文件清单

| 文件 | 动作 |
|---|---|
| `unity/Assets/Editor.Tools.Gates/AudioAssetGroupGates.cs` | 改:`IsAudioAssetName`(新)· `FilterAudioNames`(新)· `ValidateEntries` 拆三 · `AudioAssetsOnDisk` 简化 |
| `unity/Assets/Editor.Tools.Bake/AudioImportSettingsGate.cs` | 改:`IsWavName` 改为委派(单一出处) |
| `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs` | 追加 5 测(**既有测零删改**) |
| `tools/falsify/specs/audio-import-settings-and-playmode.json` | 改:5 → **10** 变异(F6–F10 新增;F5 重锚到新真源) |
| `tests/integration/audio_system/README.md` | 改:账本(Story 017 段) |
| `production/epics/audio-system/story-017-audio-gate-hardening.md` | 改:状态 + 陈旧计数订正 + Completion Notes |
| `production/session-state/active.md` | 改:Session Extract |

**同批但不同账**(用户裁定 #11 · 归 `TD-audio-01f` 根因①,**不属 story-017 的 AC**):

| 文件 | 动作 |
|---|---|
| `unity/Assets/Editor.Tools.Spike/Editor.Tools.Spike.asmdef` | 改:`references` 首位 +`Editor.Tools.Bake`(单向) |
| `unity/Assets/Editor.Tools.Spike/U1SpikeSetup.cs` | 改:`Teardown` 的 `finally` 加 Play Mode 复原委派 + `using` + 文档注 |
| `docs/architecture/adr-025-contract-assembly-manifest.md` | 改:§① 表内窄修订注记(Spike 引用边,承 S14-6 先例) |

**不经用户指令不 commit。**
