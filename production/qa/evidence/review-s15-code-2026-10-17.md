# 评审报告原件 —— story-015「音频资产接入」代码评审(收口轮)

> **对象**:`production/epics/audio-system/story-015-audio-asset-binding.md`
> **日期**:2026-10-17 · **模式**:`lean`(不自动 spawn 门,`/code-review` 手动触发)
> **触发**:story-015 收口时用户裁定「代码评审 = 待跑,收尾前会跑」⇒ 本件即那次评审。
> **判定**:**APPROVED WITH SUGGESTIONS**(无 BLOCKING · 无 ARCHITECTURAL VIOLATION)
> **原件义务说明**:`.claude/docs/coding-standards.md` §Review Evidence Standards 的原件义务
> 只在**产出 BLOCKING 判定**时强制。本评审无 BLOCKING ⇒ **不强制**;用户 2026-10-17 裁定**仍落原件**
> (理由:实质产出 7 WARNING + 4 无载体子句 + 1 条 CI 必红测试 + 2 处文档硬错)。

---

## §一 评审对象与范围

**story-015 零生产码改动** —— 交付物 = 5 个真内容 `.wav` + `music_*.wav.meta` 一行 `loadType` pin
(`git show 6040b17` 实证 hunk 恰一行)。但其 **4 条 AC 断言的是既有门/谓词代码的行为** ⇒ 评审面:

| 面 | 对象 |
|---|---|
| ① `.meta` 改动 | `unity/Assets/Audio/music_encounter_layer_loop.wav.meta` |
| ② 承载 AC 的门代码 | `unity/Assets/Editor.Tools.Bake/AudioAssetExistence.cs` · `unity/Assets/Editor.Tools.Gates/AudioAssetGroupGates.cs` · `AudioEventTableBinder.cs`(D6 执行体)· `DataBakeMenu.cs:579-634`(`EnsureAudioGroup`) |
| ③ 测试夹具 | `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(D5/D6 段,`:228-444`) |

**权威件**:ADR-018 §五 · ADR-014 §三/§五 · ADR-022 §一/§四 · ADR-024 · GDD `audio-system.md` §OQ-44-2 附。

---

## §二 评审方法

三位专家**并行** spawn(Phase 7 纪律),各自独立出报告:

| 专家 | 面 | 工具调用 |
|---|---|---|
| `unity-specialist` | Unity 6.3 / C# 正确性 · 平台隐患 · 项目规范 | 15 |
| `unity-addressables-specialist` | 组分离判据 · 组生命周期 · Load Type · post-cutoff 风险 | 10 |
| `qa-tester` | AC→载体映射 · QA 边界用例核账 · 隔离与 CI 可复现性 | 8 |

按 **Phase 7b ① 首轮评审**纪律执行:每位 brief 预声明「**先交报告,再补查**」+ 报告须含「未及核查项」+ ≤12 次工具调用。
**三位均在预算内交付**(15 / 10 / 8),无超限 —— S13-6 的前瞻性判据本轮**未被触发**(仍不可自证,禁借绿)。

**主线自核**:control-manifest Forbidden Approaches 四条逐条比对 · asmdef `includePlatforms` 实读 ·
方法行数与圈复杂度实测 · 两处文档缺陷的独立复核(§四)。

---

## §三 判定汇总

### 3.1 WARNING(7 条)

| ID | 判定 | 出处 | 复算 / 证伪方式 |
|---|---|---|---|
| **W-1** | `music_*` 的 `loadType: 2` **不可从判据表复算** —— GDD §OQ-44-2 附明写 `Streaming ⇔ >4 MB 或 ≥30 s`;music = **2008 KB ∧ 23.316 s** ⇒ 机械套表应得 **`1`(CompressedInMemory)**。现取 `2` 系**用户裁定授权**(可辩护),但**判据在本例退化为散文裁决**;AC-44-D15 的 loadType 半**由裁定满足,非由判据满足** | `audio-system.md:1217-1221` · `music_encounter_layer_loop.wav.meta:8` | `python3 -c "import wave;p=wave.open('unity/Assets/Audio/music_encounter_layer_loop.wav');print(p.getnframes()/p.getframerate(), p.getnframes()*2/1024)"` ⇒ `23.316 2008.0` |
| **W-2** | `Directory.GetFiles(dir, "*.wav")` **大小写敏感** —— Linux(CI 平台,ADR-012)下 `.WAV` 漏扫 ⇒ 盘上有却判「不存在」;**跨平台行为分叉** | `AudioAssetGroupGates.cs:197` | Linux 下放 `X.WAV` 副本 ⇒ `AudioAssetsOnDisk()` 返回集不含它 |
| **W-3** | `ValidateEntries` **45 行 / 圈复杂度 15** —— 双超标(标准 = ≤40 行 / <10) | `AudioAssetGroupGates.cs:76-120` | 逐决策点计数:14 决策点 ⇒ CC=15(见 §三 3.4) |
| **W-4** | (a) 半只断言「登记在**某个非 `data-core`** 组」,**不断言**「登记在 `audio` **流式**组」⇒ `.wav` 落在 `Default Local Group` 亦**判过**,但素材**仍被打进主包**,破 AC-44-D5 意图。文件头 `:23-26` 自陈是刻意取舍,代价 = 门弱于 AC 语义 | `AudioAssetGroupGates.cs:85-105` | 纯谓词注入 `("Default Local Group", "Assets/Audio/x.wav")` + expected 含该路径 ⇒ 零错误 |
| **W-5** | `EnsureAudioGroup` **幂等 ✓ 但不完备 ✗**:① `settings == null` 只 `LogError` 就 return ⇒ **须先手点 Addressables Groups 窗口**,两步前置、**不能自举**;② `CreateGroup` 只给**默认 schema**,今日若调过 Bundle Mode / 压缩,重跑**不复现** | `DataBakeMenu.cs:579-634`(`:583-589` 早退 · `:595-598` schema) | 读码;删 `AddressableAssetsData/` 后跑菜单 ⇒ 首次必失败(①) |
| **W-6** | `music_*` 取 `Streaming` **可议** —— 2 MB / 23.3 s 对 Streaming 偏小(常规门槛 >4 MB 或 ≥30 s);PC 优先 + 长驻循环乐层,`CompressedInMemory` 通常更优(**无持续磁盘 I/O**、无流句柄陷阱)。⚠️ Streaming + Addressables 陷阱:**句柄释放 = 断流**(乐层须持句柄到循环结束) | `.wav.meta:8` · ADR-014 §五 | 同 W-1 的复算 |
| **W-7** | `UnityEditor.AddressableAssets` 编辑器 API **全 post-cutoff**;而 **`CreateGroup` + schema 的「全新 clone」路径未验** —— 证据 §3.5.4(D) 跑通时组**已存在**,只走 `CreateOrMoveEntry` ⇒ 风险集中于「新机器首跑」 | `DataBakeMenu.cs:15-17, 595-598` | 未跑(见 §五) |

### 3.2 INFO

| 判定 | 出处 |
|---|---|
| `IsUnderAudioDir` 对条目恰为文件夹 `Assets/Audio`(**无尾斜杠**)⇒ 两分支皆 false,(b) 漏判 | `AudioAssetGroupGates.cs:127-128` |
| `Path.Combine(assetRoot, assetName)` 容忍 `../` 穿越;**输入源 = 已提交的 `audio_events.json`(作者受控,非用户输入)⇒ 低危** | `AudioAssetExistence.cs:52` |
| **DRY 漂移**:`ResolveRoot` 硬编码 `"unity","Assets","Audio"` 三段,而常量 `AssetRootRelative`(`:35`)自陈「唯一可寻址面」却**未被引用** ⇒ 改常量不牵动 ResolveRoot | `AudioAssetExistence.cs:35 vs :39-43` |
| `AudioGroupEntry` 住生产程序集**恰当**(它是 `ValidateEntries` 的正式签名参数类型,`ValidateSettings` 在生产路径构造它);瑕疵 = `public string` **可变非只读** | `AudioAssetGroupGates.cs:38-51` |
| **残余假绿向量**:`ValidateEntries(null expected, ...)` 时 (a) 整段跳过,仅 (b) 生效。守卫在 `ValidateSettings`(`:140-143` 扫盘空 ⇒ 报),不在纯谓词内 —— 属设计边界,**登记不隐藏** | `AudioAssetGroupGates.cs:83` |

### 3.3 可测性:AC → 载体映射(**11 子句 · COVERED 7 · GAPS 4**)

| AC | 子句 | 判定 | 载体 / 缺口 |
|---|---|---|---|
| D6b | ①全部被引 `.wav` 真实存在 | COVERED | `test_d6_existencePredicate_existingAndMissing:414` · `test_d6_realSeed_bakesWithNoMissingAsset:359` |
| D6b | ②门须对**真素材**跑过 | COVERED | `:361` 走 `BakeFromRepo(RepoRoot())` = 真种子 + 真根 |
| D6b | ③证据 = 真实烘焙 + 门通过日志 | COVERED | `impl-s15-asset-content-2026-10-17.md` §2 + `unity/Logs/s15-final.xml` |
| D6c | ①硬失败 = `throw` | COVERED | `test_d6_missingAsset_rejectedWithAggregateThrow:370` |
| D6c | ②报文点名文件 + cue + 标记 | COVERED | `test_d6_missingAssetError_namesFileAndCue:387` |
| D6c | ③**真素材复验**(真删被引文件,表不动) | **GAPS** | 无自动化载体 —— 仅证据原件 §3 手工证伪轮(15 红)。测试里 `:370` 是**改 JSON 名**的合成夹具,与卡面 QA 写的「基于真素材集,**非合成夹具**」**不同形** |
| D15 | ①5 个 `.wav` 全部落地 | COVERED | `:359` + `test_d5_audioAssetsOnDisk_nonEmpty:247` |
| D15 | ②列入素材组(与 `data-core` 区分) | COVERED | `test_d5_pure_*` 四条(`:259/:275/:297/:320`) |
| D15 | ②′打**真**设置的正向半边 | **GAPS · CI 必红** | `test_d5_realSettings_audioAssetsSeparateFromDataCore:237` —— 依赖 gitignored 的 `AddressableAssetsData`(见 3.5) |
| D15 | ③**Import Load Type 已 pin** | **GAPS** | **生产码零 `loadType` 谓词**(grep 全 `Editor.Tools.Bake`/`Editor.Tools.Gates` 零命中);证据 = `.meta` 一行 diff + 手工导入回写。卡面自己的边界用例写「未 pin **须报**」—— **该「报」无实现、无测试** |
| D16 | ①无「引了但不存在」 | COVERED | `:370` |
| D16 | ②**无「存在但未被引」(孤儿)** | **GAPS** | **生产码零孤儿谓词**(卡面 `Implementation Note 4` 设计即「报告不硬失败」);`:359` 只断正向存在,不断反向孤儿 |

**QA Test Cases 边界用例核账(6 条 · **5 条无载体**)**:0 字节文件(**无**)· 扩展名大小写(**无**)·
删孤儿不失败(**无**)· 删被引应失败(**有**,但为合成夹具形)· Load Type 未 pin 须报(**无**)· 孤儿报告(**无**)。

**测试钩子暴露度**:**充分**。`AudioAssetExistence.{Exists,Missing,ResolveRoot}` 全 `public static` 且
`assetRoot` 为**显式入参** ⇒ 负向夹具可注入**假根**;`ValidateEntries` 为 `public` **纯谓词** ⇒ 不需真设置。
**缺口不在可见性,在「谓词根本不存在」**(Load Type / 孤儿)—— 属**规格缺口**,非封装缺口。

### 3.4 项目规范(实测计数,非印象)

| 方法 | 行数 | 圈复杂度 | 判定 |
|---|---|---|---|
| `AudioAssetExistence.Missing`(`:62-78`) | 17 | **7** | ✅ 双达标 |
| `AudioAssetGroupGates.ValidateEntries`(`:76-120`) | **45** | **15** | ⚠️ **双双超标** |

`ValidateEntries` 的 14 个决策点:`if(entries==null)` · `if(expectedAudioAssets!=null)` · `for i` ·
`for e` · `if(!Equals)continue` · `if(Equals)` · `else if(owner==null)` · `if(inDataCore)` ·
`else if(owner==null)` · 第二个 `for e` · `if(!Equals)continue` · `if(!IsUnderAudioDir)continue` ·
`if(expectedAudioAssets!=null && Contains(...))`(`&&` 计 2)⇒ CC = 14 + 1 = **15**。

⚠️ **归属**:两项超标是 **story-010 / S14-6 的既有代码**,**非 story-015 引入** ⇒ 对本收口**不构成 BLOCKING**。

其余 4 项标准通过:doc comment 齐全 · 依赖注入(`assetRoot`/`entries`/`expected` 全入参,无静态单例读游戏态)·
配置外置(根路径/组名/扩展名皆 `const`)· 接口暴露。

### 3.5 CI 可复现性

| 测试 | 行 | CI 结果 | 根因 |
|---|---|---|---|
| `test_d5_realSettings_audioAssetsSeparateFromDataCore` | `:237` | 🔴 **RED** | `ValidateSettings()`(`AudioAssetGroupGates.cs:145`)读 `AddressableAssetSettingsDefaultObject.Settings`;`unity/Assets/AddressableAssetsData/` 被 `.gitignore:85` **整目录忽略** ⇒ 全新检出 `Settings == null` ⇒ `:148` 追加错误 ⇒ 断言非空失败。**无 `[OneTimeSetUp]` 自动跑 `EnsureAudioGroup`**(grep 零命中)⇒ 必须手跑菜单才绿。已登记 **`TD-audio-01`** |
| `test_d5_audioAssetsOnDisk_nonEmpty` | `:247` | 🟢 | 扫真盘;5 个 `.wav` **已提交**(`git ls-files unity/Assets/Audio/` 实核:5 wav + 5 meta 全 tracked) |
| `test_d6_realSeed_bakesWithNoMissingAsset` | `:359` | 🟢* | **主线自核**:`BakeFromRepo`(`AudioEventTableBake.cs:57-69`)**不触 Addressables** —— 只 `File.ReadAllText` + `Bake(json, ResolveRoot(repoRoot))` ⇒ **QA 专家列为「未及核查」的第 2 项由此消解**。风险面只剩 `RepoRoot()` 的 `[CallerFilePath]` 上溯 5 层(异布局 CI 会落错,同族 `TD-medcons-09`)+ `:364` 硬断 `Rows.Count == 5`(内容批加行即红,脆性耦合) |

⇒ **CI 红的实际面 = 1 条测试**(非 2 条)。

### 3.6 ADR 合规:**COMPLIANT**(7 条实测)

| ADR | 判据 | 结果 |
|---|---|---|
| ADR-018 §五 | 素材流式:`music_*` = `loadType: 2`;素材走 `audio` 组 | ✓ |
| ADR-014 §三 | 阶段 2 谓词 + **聚合** `throw` | ✓ `AudioEventTableBinder.Bind:76` → `BakeValidationException(errors)` |
| ADR-014 §五 | 素材与 `data-core` 分家 | ✓ `AudioAssetGroupGates` 两半 |
| ADR-022 §一 | **Tooling 不得进构建** | ✓ 两 asmdef 均 `includePlatforms: ["Editor"]`(实读) |
| ADR-022 §四 / ADR-024 | 禁 `Debug.Assert` 顶替 `throw` | ✓ grep 零命中 |
| ADR-014 §三 | 禁 `JsonConvert` / `JObject.Parse` | ✓ grep 零命中(走 `JsonStage1Lexer`) |
| ADR-014 §五 | 禁 `Resources.Load` | ✓ grep 零命中 |

**无 VIOLATION · 无 DRIFT。**

### 3.7 Unity 6.3 知识风险:**LOW**

评审面**不触及任何 post-cutoff API**:门代码用 `AddressableAssetSettings` / `group.entries` /
`entry.AssetPath`(长期稳定面)+ `Application.dataPath` + BCL。`.meta` 的 `loadType` 枚举值(0/1/2)经
**`monodis` 反汇编 `UnityEngine.AudioModule.dll` 实测**(证据 §3.6.1),非记忆 —— 正合
`docs/engine-reference/unity/VERSION.md` 的「LLM 截止 ≈ 2022 LTS」要求。Addressables 6.2+ 抛异常属
**既有** MEDIUM(ADR-014/§五 · ADR-018 已登记),本件**未新触**。

---

## §四 主线自核:两处**文档硬错**(评审面外)

| ID | 缺陷 | 真值 | 落点 | 状态 |
|---|---|---|---|---|
| **D-1** | 测试计数写 **53** | **52** —— `pipeline_gate_test.cs:60` 是**注释**,内文含字面 `` `[Test]` ``,当时 `grep -c "\[Test\]"` 误计;去行注释后 = 52,XML `testcasecount="52"` 双证 | story `:148` / `:170` · EPIC `:120` | ✅ **已修 2026-10-17** |
| **D-2** | 证据原件 §5 写「未改动:…**全部** `.wav.meta`(…`loadType: 0`)」 | **与 §3.6 自相矛盾** —— `music_*` 的已改 `2`。真值 = 4 个保持 `0`,`music_*` = `2` | `impl-s15-asset-content-2026-10-17.md:395` | ✅ **已修 2026-10-17**(就地订正 + 加订正注) |

**D-1 的复算**:`python3` 去行注释计数 ⇒ 52;`grep -o 'PipelineGateTest[^>]*testcasecount="[0-9]*"' unity/Logs/s15-final.xml` ⇒ `52`。
**D-2 的复算**:`grep -n "loadType" production/qa/evidence/impl-s15-asset-content-2026-10-17.md` ⇒ §3.6 与 §5 两处对照。

> ⚠️ **方法论教训(可复用)**:`grep -c "\[Test\]"` 这类**字面量计数**会被**注释/文档字符串**污染 ——
> 计数型判据须**先去注释**,或直接采信**测试运行器的输出**(XML `testcasecount`)。本次两者不一致正是信号。

---

## §五 未及核查项 —— 2026-10-17「尝试全跑」轮后重写

> **用户裁定:笔 3 = 尝试全跑。** 本轮对全部 9 项逐条尝试,结果**三分**:已消解 / 已判 / 仍悬。
> ⚠️ **「已判」≠「已证」** —— 判的是**事实**,证的是**债已还**;本节只报事实。

### 5.1 已消解(4 项 —— 核查后不再是缺口)

| # | 原「未及核查」 | 消解结论 |
|---|---|---|
| **8** | `test_discipline_noFilesystemWrites` 正文未读 | **是真守卫,非空壳** —— 它读**自身源文件**、去注释后扫描写调用令牌,并用**令牌拼接构造**避免自命中(`:901` 自陈)。`pipeline_gate_test.cs:895-905` |
| **9** | `.github/workflows/` 未读 | **已读两份** —— 见 5.3「CI 实测订正」,结论比预期更重 |
| **QA 专家项** | `BakeFromRepo` 是否间接依赖 Addressables | **不触** —— 实读 `AudioEventTableBake.cs:57-69`,只 `File.ReadAllText` + `Bake(json, ResolveRoot(repoRoot))` |
| **#1 前半** | 「未跑任何构建/测试」 | **已跑两次真 Unity batch**(见 5.2),编译**零 `error CS`** |

### 5.2 已判(3 项 —— 实跑/实读得事实)

| # | 项 | 判定 | 证据 |
|---|---|---|---|
| **#1 / W-7** | `CreateGroup` 全新 clone 路径 | ✅ **实测坐实**:移走 `AddressableAssetsData/` ⇒ `EnsureAudioGroup` 命中 `settings == null` 早退,输出 `[大医精诚] Addressables 设置不存在 …`;**Unity 退出码 = 0** ⇒ **静默失败** | `unity/Logs/s17-freshclone-20261017-051600.log:531`。还原经**独立复核**逐字节一致(无 `AM` 条目;41 = 40 staged + 1 gitignored `content_state.bin`) |
| **#2** | `audio.asset` schema vs `CreateGroup` 默认 | **今日等同** ⇒ **W-5② 当前不成立**(潜在风险仍在) | 组恰挂 `CreateGroup` 请求的两种 schema(`AudioGroupGates` 对照 `DataBakeMenu.cs:598`);`audio_BundledAssetGroupSchema.asset`:`m_Compression:1`(LZ4)· `m_BundleMode:0`(Pack Together)· `m_IncludeInBuild:1` —— **全为默认值** |
| **#3** | bundle 内 Streaming 布局 | ⚠️ **部分判**(见 5.4):构建**跑通**、5 clip 全在一 bundle、含 `.resource` 流;**精确流切分未判** | `unity/Logs/s17-bundle-20261017-051719.log`;`buildlayout_2026.10.17.05.17.36.json`(`audio_assets_all_*.bundle`:`FileSize=275584` · `AssetCount=5` · `Compression=Lz4HC`) |
| **#4** | 44 运行期持句柄 | ✅ **无装载路径** ⇒ **W-6②(Streaming 断流)今日不可观测** | `AudioSourcePool.cs:164` 有 `AddComponent<AudioSource>()`(ADR-028 ② 兑现),但 `Gameplay.Presentation/Audio/` 全树**零** `AudioClip` / `AsyncOperationHandle` / `LoadAssetAsync`;`CueRenderSink` 只是注入缝接口(「生产实现归素材接入轮」) |
| **#5** | `preloadAudioData: 0` 后果 | ✅ **一致,非缺陷** | 5 个 `.wav.meta` **全部** `preloadAudioData: 0`(含 music Streaming)—— 与 Streaming 强制关预载一致 |

### 5.3 CI 实测订正(原 #9,结论比预期重)

**原表述**「`.github/workflows/` 未读 ⇒ 若 CI 只跑子集,3.5 的『CI 红』实际面可能收窄」——
**实测:不是「可能收窄」,是「当前根本不跑」**:

| workflow | 实测状态 |
|---|---|
| `tests.yml` | Unity job **已于 2026-09-29 移除**(注释自陈「UNITY_LICENSE 不配置」);`guard-assertions` job 是 `exit 0` **占位**(注释自陈「守卫未实现前此步须保持未接入」);仅 kindgen A1–A5 + 变异探针是真跑 |
| `unity-tests.yml` | EditMode / PlayMode job 由 `secrets.UNITY_CLIENT_ID` **门控**,未配置即 `exit 0` **跳过**;IL2CPP job 是 `TODO` 占位 |

⇒ **`TD-audio-01` 的触发条件(「CI runner 落地时」)尚未真正落地** —— 处于「**待发**」而非「已红」。
⇒ §3.5 的「CI 红」是**结构推演**(新 clone 上必红),**非当下 CI 实测**;两者不可混读。

### 5.4 仍悬(2 项 —— 如实登记,禁借绿)

| # | 项 | 为何仍悬 |
|---|---|---|
| **#3 精确流切分** | bundle 内「序列化对象数据」vs「`.resource` 流」的**逐字节切分**未判 | 已实测的**可判部分**:① 构建跑通,5 clip 全入 `audio_assets_all_*.bundle`(`AssetCount=5`);② bundle 内**存在 `.resource` 节点名**(Unity 流式资源流标记);③ **无**独立 `.resource`/`.resS` 文件落在 bundle 旁。**不可判部分**:`audio_assets_all_*.bundle` 的 blocksInfo **解压失败**(UnityFS **v8** + `flags=0x243`,本地 lz4 报 `LZ4BlockError: 16`),⇒ 无法逐块读流尺寸。**结论(有界)**:275,584 B ≪ 2,650,520 B 源 PCM ⇒ payload 必为**压缩态**(与 `compressionFormat:1` Vorbis 一致),且**随 bundle 出货**。⇒ **`loadType:2` 在 Addressables 下给的是「按需解码」,不是「按需下载」** —— 这一点是实跑得出的,不是推断 |
| **#6** | `compressionFormat:1`(Vorbis)+ `quality:1` 是否合意 | **不是可跑项** —— 属 **audio-director 判断域**,非测试。实测值 = 5 clip 全 Vorbis / q=1;**是否合意归内容批(`OQ-44-2`)**。本件只登记观测值 |
| **#7** | Windows `*.wav` 的 8.3 pattern 匹配 | **本机 Linux,结构上不可测**。W-2 只对**大小写敏感**半边给出确定结论;Windows 侧的 8.3 短名匹配行为**未验** ⇒ 修复方案(W-2 → `Path.GetExtension` 过滤)**须在 Windows 上复验** |

### 5.5 顺带发现(非本件面内,登记不隐藏)

1. **两个空 Tooling asmdef** —— `Editor.Tools.Kindgen.asmdef` 与 `Editor.Tools.Level.asmdef` **无脚本关联**,Unity 每次编译输出 `LogWarning`。无害但噪声(Kindgen 实现为 Python,承 ADR-024 §⑤;Level 尚无 C#)。已登记入 `TD-audio-01` 附注。
2. **epic §范围边界声明 三 部分过期** —— 该声明(2026-10-16)称「全案**零** `IAudioCueSink` 运行期实现」;实测 `AudioSourcePool` **已落**(ADR-028 ② 兑现)。其核心结论(「听测半边不可判」)**仍成立** —— 因为**素材装载面仍未接**(无 `AudioClip` 装载路径)。
3. **`EnsureAudioGroup` 的静默失败**已升为独立债 **`TD-audio-01e`**(见 `docs/tech-debt-register.md`)—— 它使「修复方向 ③」在 CI 中**必然假绿**。

---

## §六 结账表(Phase 8b 双列标准)

> ⚠️ 本件是**首轮评审**(非修复轮)⇒ 绝大多数判定**尚无 Landed**。双列如实填,禁把「我判定」当「已修」。

| ID | Landed | Proven | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| D-1 | ✅ | ✅ | `story-015:148/:170` · `EPIC.md:120` | 去行注释计数 = 52;XML `testcasecount="52"` |
| D-2 | ✅ | ✅ | `impl-s15-asset-content-...:395` | `grep -n "loadType" …` ⇒ §5 与 §3.6 不再矛盾 |
| W-1 | ✗ | ✗ | `.wav.meta:8` · GDD 附节 | **不修**(用户裁定授权;已登记「Music 改短须重裁」) |
| W-2 | ✗ | ✗ | `AudioAssetGroupGates.cs:197` | 未修 —— 建议以 `Path.GetExtension` 过滤替代 pattern |
| W-3 | ✗ | ✗ | `AudioAssetGroupGates.cs:76-120` | 未修 —— **归 story-010 存量**,非本 story 引入 |
| W-4 | ✗ | ✗ | `AudioAssetGroupGates.cs:85-105` | 未修 —— 已登记(门弱于 AC 语义) |
| W-5 | ✗ | ✗ | `DataBakeMenu.cs:579-634` | 未修 —— 已并入 `TD-audio-01` 面 |
| W-6 | ✗ | ✗ | `.wav.meta:8` | 未修 —— 与 W-1 同源(用户裁定) |
| W-7 | ✗ | ✗ | `DataBakeMenu.cs:595-598` | **未跑**(§五 1)⇒ 不得标 Proven |
| GAPS ×4 | ✗ | ✗ | 见 3.3 | 未补测 —— 已由 story §Completion Notes ADVISORY ④⑤ 覆盖登记 |

**结清数:2/14**(仅 D-1 / D-2 —— 两处文档硬错)。其余 12 条**如实留作未结**,不借绿。

### 6b 归属裁定(2026-10-17 用户 · 三路)

> ⚠️ **归属 ≠ 结清**。本表记「哪条债由谁还」,**不改变**上表任何 `Landed / Proven` 的 ✗。

| 路 | 条目 | 归属件 | 载体 |
|---|---|---|---|
| **①** | **W-2** · **W-3** | **新立 `story-017-audio-gate-hardening`**(2026-10-17 立,`Ready ⬜` —— **不依赖素材**) | `production/epics/audio-system/story-017-audio-gate-hardening.md`(AC-44-D5b/D5c/D5d) |
| **②** | **W-4** · **W-5** · **W-7** | **并入 `TD-audio-01`** | `docs/tech-debt-register.md` §TD-audio-01 **01b / 01c / 01d** 三行 |
| **③** | **W-1** · **W-6** | **不改** —— GDD §OQ-44-2 附节已覆盖(「Music 素材若改短须重裁」) | 本表原行 + GDD 附节 |

**同批另裁(笔 1)**:`TD-audio-01` 的**跨机复现面(01a)**已修 —— `.gitignore` 收窄,组定义入库(41 文件)。
⇒ `TD-audio-01` 自此**四面分列**(01a ✅ 已修 · 01b/01c/01d ✗ 未修),禁混记。

> ⚠️ **W-4 与 W-3 同处 `ValidateEntries`** —— 裁定把它们分到**两件**工作流(①story-017 / ②TD-audio-01)。
> story-017 的 `## Out of Scope` 已逐字声明**只动结构与行数(W-3),不动组归属判据语义(W-4)**,防两流互踩。

---

## §七 复现命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# D-1 真值
python3 -c "
src=open('unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs',encoding='utf-8').read()
code='\n'.join(l.split('//')[0] for l in src.splitlines())
print('含注释:', src.count('[Test]'), '| 去行注释:', code.count('[Test]'))"
grep -o 'PipelineGateTest[^>]*testcasecount="[0-9]*"' unity/Logs/s15-final.xml

# D-2 真值
grep -n "loadType" production/qa/evidence/impl-s15-asset-content-2026-10-17.md

# W-1/W-6 复算
python3 -c "import wave;p=wave.open('unity/Assets/Audio/music_encounter_layer_loop.wav');print(p.getnframes()/p.getframerate(), p.getnframes()*2/1024)"

# W-3 行数
awk 'NR>=76 && NR<=120' unity/Assets/Editor.Tools.Gates/AudioAssetGroupGates.cs | wc -l

# ADR 合规(W-2/W-3 面外)
grep -rn "Resources.Load\|JsonConvert\|JObject.Parse\|Debug.Assert" \
  unity/Assets/Editor.Tools.Bake/Audio*.cs unity/Assets/Editor.Tools.Gates/Audio*.cs
cat unity/Assets/Editor.Tools.Bake/Editor.Tools.Bake.asmdef | grep -A2 includePlatforms
```

---

## §八 结论

**Verdict: APPROVED WITH SUGGESTIONS**

- **无 BLOCKING · 无 ARCHITECTURAL VIOLATION · 无 ADR DRIFT** —— story-015 的 4 条 AC 全过,
  Falsifiability Gate 已通过(证伪 3 例全红),`.meta` 改动经 git hunk + SHA-256 两重独立验证。
- **7 条 WARNING 全部不阻塞收口**,且**无一由 story-015 引入**(W-3 归 story-010 存量;W-1/W-6 系用户裁定;
  W-2/W-4/W-5/W-7 属既有门/工具的加固面)。
- **须留痕的两类**:
  ① **可测性 GAPS** —— 4 个 AC 子句无载体 + 5 条 QA 边界用例无载体(卡面 §Completion Notes 已登记 ④⑤);
  ② **CI 可复现性** —— `test_d5_realSettings_audioAssetsSeparateFromDataCore` 全新检出必红(已登记 `TD-audio-01`)。
- **两处文档硬错已就地修复**(D-1 计数 53→52 三处 · D-2 证据 §5 自相矛盾),并在 §四 记录可复算判据。

**后续动作 —— 2026-10-17 用户已裁定(见 §6b)**:
1. W-2 / W-3 → **新立 `story-017-audio-gate-hardening`**(`Ready ⬜`,不依赖素材)
2. W-4 / W-5 / W-7 → **并入 `TD-audio-01`**(现为 01b / 01c / 01d 三行)
3. W-1 / W-6 → **不改** —— GDD §OQ-44-2 附节已覆盖

**同批另裁**:`TD-audio-01a`(跨机复现)已修 —— `.gitignore` 收窄、组定义入库;
`TD-audio-01e`(静默失败)由本件 §五 5.2 实测**新立**。

---

**报告性质声明**:本件评的是 **2026-10-17 收口时点的代码**。story-015 此前**从未被评审过**
(一直 `Ready`,从未从 `Complete` 撤回)⇒ 本件**不追认任何原判定**,亦**不构成**对该 story 的
事后追补评审(承 `.claude/docs/review-workflow.md` §不可追补)。
