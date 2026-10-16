# S14-6 实现证据 —— story-010(素材管线与事件表烘焙门)· 2026-10-16

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-010-asset-pipeline-bake-gates.md`
> **上游判定**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.4
> (010 = 11 测中 🟢 真 0 · 🟡 半真 4 · 🔴 假 7)
> **sprint**:`production/sprints/sprint-14.md` S14-6(Should Have · M 件)
> **用户裁定(2026-10-16,AskUserQuestion ×4)**:
> ① **D6 门强制**(非可选参数)+ 落 **5 个占位静音 wav** ⇒ 正向半边由**真种子**证明,
> 判别力由负向夹具证明(两轮评审的病根都是「自包含夹具恒真」,不能让正向半边重蹈)·
> ② **素材根 = `unity/Assets/Audio/`**(Addressables 可直寻址;`assets[]` 本是 bare 名 ⇒ 零映射代码),
> GDD `:258` / `:695` / `:1092` 与 ADR-018 `:153` 的 `assets/audio/` 加**补注订正**为逻辑路径
> (承 ADR-015 修订 ADR-009 先例)·
> ③ **听诊预载门 = 真生产谓词 + `loadState` 查询缝**;真 `AudioClip.loadState` 的 PlayMode 半边
> **如实登记 NOT-RUN** + 缺口哨兵 ·
> ④ **含最小接线** —— 除烘焙管线外,落 cooked 字节 → 表的运行期读取面,并接上 story-011 §登记项
> 明确留给本件的 `AudioSettingsRegistry.Provider` 注入。

---

## §1 原判定 → 修复落点

| # | 原判定(评审原件 §2.4) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | `Editor.Tools.Bake/` **零个 audio 文件** —— AC-44-D4 要求的烘焙管线**整条不存在** | 🔴 | `AudioEventTableBinder` + `AudioEventTableCookedWriter` + `AudioEventTableBake` 三件套(承 `DelayTable53Bake` 模板)+ `AudioSettingsRegistryCookedWriter`(S14-5 只落 binder,Bake/CookedWriter 是本件的补口) | 反证 C1 / C2 / C6 / C7 / C10 + `test_bake_*` 9 条 |
| 2 | AC-44-D4 三条扫描测中**两条无 `Count > 0` 守卫** ⇒ 空集恒绿 | 🟡 半真 | `test_d4_noJsonReaderRefs_inProductionAudioTypes` / `test_d4_noFixParseRefs_inProductionAudioTypes` 补扫描键非空守卫;新增 `test_d4_presentationAssembly_noNewtonsoftRef`(**装配引用集**断言,先例 `camera_presentation_discipline_test.cs:284`) | 该三条自身即判据 |
| 3 | Addressables 音频组**不存在**,且全库**零门读 `AddressableAssetSettings`** | 🟡 半真 | `AudioAssetGroupGates`(两半:纯谓词 `ValidateEntries` + 真设置 `ValidateSettings`)+ `Editor.Tools.Gates.asmdef` 补 `Unity.Addressables` / `Unity.Addressables.Editor` + 菜单「确保 audio Addressables 组」 | 反证 C3 / C4 + `test_d5_*` 8 条(本机 `audio` 组已建,正向半边转真绿) |
| 4 | AC-44-D5 负向打**测试本地字符串 + 自造 `GroupSeparabilityPredicate`** | 🔴 | 删自造谓词;负向打 `AudioAssetGroupGates.ValidateEntries`(纯谓词,注入条目集) | `test_d5_pure_*` 5 条 |
| 5 | AC-44-D6 两条测**测试自己创建的文件 + 内联 `File.Exists`** | 🔴 | 删内联 `File.Exists` 与整段文件系统写入副作用;谓词单一出处 = `AudioAssetExistence`;正向半边打**真种子** | 反证 C1 + `test_d6_*` 7 条 |
| 6 | AC-44-D6 正向半边从未跑过真种子(真种子引用的 5 个 wav **全部不存在**) | 🔴 | 落 5 个占位静音 wav(2044 B = 44 B RIFF/WAVE 头 + 2000 B 全零 PCM(单声道 44100 Hz 16 bit ⇒ 1000 样本 ≈ 22.7 ms 静音))⇒ `test_d6_realSeed_bakesWithNoMissingAsset` 真绿 | 该条自身 + 反证 C1 |
| 7 | 听诊预载 3 测打自造 `FakePreloadGate` | 🔴 | `StethoscopePreloadGate`(真生产谓词,注入式 `LoadStateQuery`,**空集 = 构造期硬失败**)+ `HandoverMux` 接线 | 反证 C5 + `test_preload_*` 11 条 |
| 8 | 「一次性 cue 迟发 ≤ 登记常量」= **两个硬编码 int 互比**(`maxDelayMs=100` / `actualDelayMs=50`) | 🔴 | **该测删除**(恒真)。其判据面现由 `StethoscopePreloadGate` 的「全族就绪才放行」承担 —— GDD `:640` 的「不得在听诊中因加载迟发」针对的是听诊**期间**任何一条迟到 | `test_preload_partialLoaded_blocks` |
| 9 | `pipeline_gate_test.cs:213-227` **写仓库 `assets/audio/`**(违 `test-standards.md`「Unit tests must not depend on external state (filesystem)」) | 🔴 | 副作用整段删除;新增 `test_discipline_noFilesystemWrites`(扫本文件源码,注释剥离)作为**结构性守卫**;遗留空目录 `assets/audio/` 已 `rmdir` | 反证:该测自身 + `test_discipline_*` |
| 10 | 真 `AudioClip.loadState` 的 PlayMode 半边 | 🟡 悬空 | 承裁定 ③:**如实登记 NOT-RUN** + 缺口哨兵 `test_preload_realAudioClipLoadState_pendingPlayModeHalf`(素材定稿后它转红 ⇒ 须随之改判) | 该条为哨兵,不签绿 |

---

## §2 交付面

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(烘焙) | `Editor.Tools.Bake/AudioEventTableBinder.cs` | 阶段 2 绑定:阶段 1 词法 → 行模型 → 门(`AudioEventTableGates.Validate`)→ **D6 存在性谓词**;聚合 `BakeValidationException`;`assetRoot` **必填** |
| 新(烘焙) | `Editor.Tools.Bake/AudioEventTableCookedWriter.cs` | deterministic 编码(固定字段序 / 零字典迭代 / 行序 = cue ordinal);头槽写 `bound.SchemaVersion`(**不写 0** —— R-1 教训) |
| 新(烘焙) | `Editor.Tools.Bake/AudioEventTableBake.cs` | 仓根装载器(不碰 `UnityEngine`/Addressables);`BakeFromRepo` / `BakeFromJson` / `Materialize`(**真往返** + 跨侧等值断言) / `AssertHeaderContract` |
| 新(烘焙) | `Editor.Tools.Bake/AudioAssetExistence.cs` | **D6 谓词单一出处**;`ResolveRoot` / `Exists` / `Missing`(保序去重);`AssetRootRelative = "unity/Assets/Audio"` |
| 新(烘焙) | `Editor.Tools.Bake/AudioSettingsRegistryCookedWriter.cs` | 设置注册表 cooked **写面**(S14-5 刻意留白的补口);行序 = `param` Ordinal 升序;空集 ⇒ 硬失败 |
| 改(烘焙) | `Editor.Tools.Bake/AudioSettingsRegistryBake.cs` | 补 cooked 输出 + `Materialize`(真往返 + 跨侧逐项等值)+ `AssertHeaderContract` |
| 改(烘焙) | `Editor.Tools.Bake/DataBakeMenu.cs` | 三菜单项:`BakeAudioEvents` · `BakeAudioSettingsRegistry` · `EnsureAudioGroup`(镜像 `EnsureDataCoreGroup`) |
| 新(门) | `Editor.Tools.Gates/AudioAssetGroupGates.cs` | AC-44-D5 执行体。**两半分离**:纯谓词 `ValidateEntries`(负向夹具可注入)+ `ValidateSettings`(读真 `AddressableAssetSettings`)。判据 = **分离**(素材 ∈ 非 `data-core` 组 ∧ `data-core` 零 `Assets/Audio/` 条目),**不是**「组叫什么名字」;**设置缺席 ⇒ 报错不跳过** |
| 改(门) | `Editor.Tools.Gates/Editor.Tools.Gates.asmdef` | +`Unity.Addressables` / `Unity.Addressables.Editor`(AC-44-D5 逐字要求该装配读 `AddressableAssetSettings`) |
| 新(生产) | `Gameplay.Presentation/Audio/StethoscopePreloadGate.cs` | 听诊窗预载门(纯谓词;`FromTable` 从事件表派生听诊族 —— cue 集单一出处 = 表);空集 / null 查询 = 构造期硬失败 |
| 新(生产) | `Gameplay.Presentation/Audio/AudioEventDataSet.cs` | 事件表运行期载体(行集 + tier_map + 双版本号;零 `UnityEngine`) |
| 新(生产) | `Gameplay.Presentation/Audio/AudioEventCookedCodec.cs` | cooked 读方(与写方**严格镜像**);载荷 schema_version ↔ 头槽比对;E-13 硬失败 |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsDataSet.cs` | 设置注册表运行期载体 |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsRegistryCookedCodec.cs` | 设置注册表读方(同款守卫) |
| 改(生产) | `Gameplay.Presentation/AddressablesDataProvider.cs` | 两条 audio 支路(`AudioEventsAddress` / `AudioSettingsAddress`,**单一出处** = 两 `*DataSet.CookedFileName`)+ `Preload()` 追加 + 未知类型错误串订正 |
| 改(生产) | `Gameplay.Boot/BootRoot.cs` | `ApplyAudioSettingsProvider(bool, IDataProvider)`(**静态可直驱** —— 内联在 `Start()` 里则 EditMode 无从断言,「接线存在」退化为不可证伪的口头承诺) |
| 新(素材) | `unity/Assets/Audio/*.wav` ×5 | 占位静音 wav(2044 B = 44 B RIFF/WAVE 头 + 2000 B 全零 PCM(单声道 44100 Hz / 16 bit / 1000 样本 ≈ 22.7 ms 静音));`.meta` 由 Unity 首次导入生成(**默认 Import Settings,未 pin**) |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs` | **11 → 52 测**;四个自造替身 + 文件系统写入副作用**全数删除** |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` | `EntryAllowlist` 增列 5 项(B3 门;附先例式理由) |

**关键设计**:
- **门强制,不是可选门** —— `AudioEventTableBinder.Bind(json, assetRoot)` 的 `assetRoot` 必填,
  `AudioAssetExistence.Missing` 对 null 根**抛**(刻意不提供「根可空 ⇒ 跳过」形态)——
  可选门正是 story-010 原稿 `Assert.Ignore` 假绿的翻版。
- **`Materialize` 走真往返,不是直构** —— 直构会让「写方与读方布局不一致」从判据面消失。
  本件构造期间**正是**该断言抓到写方/读方布局分叉(读方漏读载荷首字段 schema_version),
  修复后 10 条红转绿 —— 这是「真往返」相对「直构」的可证伪性优势的实证。
- **跨侧等值断言**(承 `DelayTable53Bake.AssertDomainDeclarationsMatch` 先例):
  「cue ordinal」这一个量同时被两处声明(绑定层 `Rows[i]` 行序 vs 解码后 `Row(i)`)⇒
  逐项比对 cue 名与 ordinal;无本断言时「同数但错位」接不住。
- **三层白名单单一出处** —— 绑定层**不另抄** `TopLevelKeys` / `RowKeys` / `TierMapColumns`,
  全部经 `AudioEventTableGates.Validate`(ADR-024 §⑥)。
- **b5 逃逸面守持** —— `AddressablesDataProvider.cs` **不含** `AudioCueDto` / `IAudioCueSink` /
  `AudioCueHandle` / `TierSource` 四个 token(它住非 `.Audio` 命名空间)。

---

## §3 证伪轮 —— 11 例(停用生产码守卫 ⇒ 断言转红)

**纪律**(承 S14-5 `tmp-fz-s145.py` 模式):每例**改 → 跑 → 还原**在**同一命令内**完成;
`finally` 还原后做 **SHA-256 逐字节校验**(不符即脚本崩)。脚本 = `/tmp/fz_s146.py`,
结果原件 = `unity/Logs/s14-6-fz.xml`(最后一例)。

### 逐例结果

| 例 | 停用的守卫(生产码) | 主要转红断言 | 红数 |
|---|---|---|---|
| **C1** | `AudioAssetExistence.Exists` ⇒ 恒真 | `test_d6_missingAsset_rejectedWithAggregateThrow` · `test_d6_missingAssetError_namesFileAndCue` · `test_d6_existencePredicate_existingAndMissing` · `test_d6_missing_isOrderPreservingAndDeduped` | **4** |
| **C2** | `AudioEventTableGates.RowKeys` 摘除真键 `subtitle_text`(单一出处须生效) | `test_unknownRowKey_reportsRed` · `test_realSeedAudioEvents_zeroErrors` · `test_d6_realSeed_bakesWithNoMissingAsset` · `test_bake_materialize_crossSideMatches` …(**跨 story-002 的 `event_table_gate_test.cs` 连带 40+ 条**) | **56** |
| **C3** | D5(a) 不区分 `data-core`(组区分判据停用) | `test_d5_pure_audioAssetInDataCoreGroup_rejected` | **1** |
| **C4** | D5(b) 停用 `Assets/Audio/` 目录判定 | 见下方 **⚠️ C4 弱判别面** | 首轮 **0** ⇒ 修夹具后 **1** |
| **C5** | 听诊预载门「全 Loaded 才放行」判据**取反** | `test_preload_notLoaded_blocks` · `test_preload_allLoaded_allows` · `test_preload_partialLoaded_blocks` · `test_preload_failedState_blocks` · `test_preload_handoverMux_{gateBlocks,gateAllows}` | **5** |
| **C6** | cooked 头槽 `schemaVersion` 写 `0`(**R-1 缺陷类复现**) | `test_bake_materialize_crossSideMatches` · `test_bake_rowOrderIsCueOrdinal` · `test_d4_runtimeCodec_decodesBakedBytesOnly` … | **12** |
| **C7** | 写方**行序反转**(跨侧等值断言须红) | `test_bake_materialize_crossSideMatches` · `test_bake_rowOrderIsCueOrdinal` · `test_preload_fromTable_derivesStethoscopeFamily` … | **10** |
| **C8** | `AddressablesDataProvider.AudioEventsAddress` 由**单一出处**改为另抄的陈旧字面量 | `test_wiring_addresses_singleSourceOfTruth` | **1** |
| **C9** | `BootRoot` 的 `AudioSettingsRegistry.Provider` 注入停用 | `test_bootroot_injection_whenPreloaded_rowsComeFromProvider` | **1** |
| **C10** | cooked 读方「载荷 `schema_version` == 头槽」守卫取反(`audio_events`) | `test_d4_runtimeCodec_payloadSchemaDivergedFromHeader_rejected` · `test_d4_runtimeCodec_decodesBakedBytesOnly` … | **12** |
| **C11** | 同款守卫取反(settings 注册表) | `test_d4_settingsCodec_payloadSchemaDivergedFromHeader_rejected` · `test_d4_settingsCodec_decodesBakedBytesOnly` · `test_wiring_bakedRows_matchFallbackValues` … | **6** |

**还原校验**:11 例跑完后 9 个被改文件的 SHA-256 **逐项等于**基线(`全部还原一致`),
脚本在任一例还原不符时即中止(exit 3)。

### ⚠️ C4 弱判别面 —— 证伪轮**第一次**抓到夹具本身(BLOCKING 级,已修)

**首轮结果:0 红。** 停用 (b) 判据(`Assets/Audio/` 目录判定)后,`test_d5_pure_strayAudioEntryInDataCore_rejected`
**仍绿** —— 即该夹具**对 (b) 判据毫无判别力**。

**根因(夹具形态,不是生产码)**:原夹具的**期望集成员没有合法登记** ——

```
entries  = [ ("data-core", "Assets/Audio/orphan.wav") ]
expected = [ "Assets/Audio/sfx_breath_base_loop_small.wav" ]
```

(a) 半边找不到 `expected[0]` 的任何登记 ⇒ 先报「未登记进任何 Addressables 组」。
断言只写了 `errors Is.Not.Empty` ⇒ **被 (a) 半边的误报满足**,与 (b) 是否生效无关。

**修**(与 S14-5 第 ⑦ 例同型:断言被**别的**错误满足):

1. **夹具形态**:期望集成员**合法登记在 `audio` 组**,漏网条目设为**另一个**文件(`orphan.wav`)
   ⇒ (a) 半边通过,判据面只剩 (b);
2. **报文钉定**:断言 `Does.Contain("orphan.wav")`(判据必须点名漏网条目)
   + `Does.Not.Contain("sfx_breath_base_loop_small")`(合法成员不得被报)。

**复跑**:同一条 C4 变异 ⇒ **1 红**(`test_d5_pure_strayAudioEntryInDataCore_rejected`)。

> **⚠️ 这是本 sprint 第五次由证伪轮抓到撰写者缺陷,且第二次抓到「夹具判别力」本身**
> (S14-5 第 ⑦ 例是第一次)。两例同型:**断言只写「非空 / 抛了」而不钉「是谁报的」**,
> 就会被同一函数内**别的**分支的误报满足。⇒ 后续负向夹具一律**钉报文内容**。

---

## §4 回归对账

| 轮次 | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| 基线 S14-5 | 3572 | 3526 | 0 | 46 | `unity/Logs/s14-5-final.xml` |
| 本件 Audio 过滤套件 | 395 | 395 | 0 | 0 | `unity/Logs/s14-6-audio.xml` |
| 本件全量 EditMode | 3613 | 3568 | 0 | 45 | `unity/Logs/s14-6-final.xml` |

**对账**:逐 `fullname` diff 基线与本轮 —— 差异**恰为** `PipelineGateTest` 的旧 11 测(已删)
→ 新 52 测;其余 3561 条**零结果变化**。跳过数 46 → 45 的唯一原因 =
旧 `test_addressables_audioGroupSeparateFromDataCore`(Skipped)被
`test_d5_realSettings_audioAssetsSeparateFromDataCore`(Passed)取代 —— 即 **P4 转真绿**。

---

## §5 如实登记(禁借绿)

| # | 项 | 状态 | 解除条件 |
|---|---|---|---|
| 1 | 真 `AudioClip.loadState` 的 PlayMode 半边 | **NOT-RUN** | 素材本体定稿(内容批 `OQ-44-2`)⇒ 缺口哨兵 `test_preload_realAudioClipLoadState_pendingPlayModeHalf` 转红 ⇒ **须随之改判,不得当回归修掉** |
| 2 | 素材 Import Settings 定稿(呼吸/语声族 `DecompressOnLoad` 等) | 归内容批 | `OQ-44-2` |
| 2b | **素材内容需求本身**(逐族声学判据 + 4 条系统级要求) | 已立册 | **`docs/tech-debt-register.md` §「44 音频素材内容需求」**(2026-10-16);本节此前**未登记内容需求**,只有「NOT-RUN + 解除条件」 |
| 3 | IL2CPP 三格(ADR-012 矩阵) | **NOT-RUN** | 本机仅 Mono;CI 矩阵 |
| 4 | `AddressableAssetsData/` 是 gitignored 机器生成物 | 机器前置 | 每台机器首次须跑「确保 data-core / audio Addressables 组」菜单(与既有 `data-core` 同款) |
| 5 | `Editor.Tools.Bake/AudioEventTableBinder` 与 `event_table_gate_test.cs` 的手写 JSON 扫描器是**两份阶段 2 抽取器** | 已登记的重复 | **刻意不动**:后者承载 story-002 的截断夹具语义(`__truncated__` 哨兵);归 story-002 轮 |

---

## §6 落地清单(供 commit 参考)

- **新(生产/门/烘焙)**:`AudioEventTableBinder` · `AudioEventTableCookedWriter` · `AudioEventTableBake` ·
  `AudioAssetExistence` · `AudioSettingsRegistryCookedWriter` · `AudioAssetGroupGates` ·
  `StethoscopePreloadGate` · `AudioEventDataSet` · `AudioEventCookedCodec` ·
  `AudioSettingsDataSet` · `AudioSettingsRegistryCookedCodec`
- **改(生产/门/烘焙)**:`AudioSettingsRegistryBake` · `DataBakeMenu` · `Editor.Tools.Gates.asmdef` ·
  `AddressablesDataProvider` · `BootRoot` · `AudioSettingsRegistry`(文档注) ·
  `AudioEventCookedCodec`(读方布局修复)
- **新(素材)**:`unity/Assets/Audio/` 5 个占位 wav(+ `.meta`)
- **改(测试)**:`pipeline_gate_test.cs`(11 → 52)· `assembly_boundary_test.cs`(B3 白名单 +5)
- **改(文档)**:`design/gdd/audio-system.md`(`:258` / `:695` / `:1092` 路径补注)·
  `docs/architecture/adr-018-audio-architecture.md`(`:153` 同款)·
  `docs/architecture/adr-025-contract-assembly-manifest.md`(`Editor.Tools` 族行注记)·
  `production/epics/audio-system/story-010-*.md` · `tests/integration/audio_system/README.md` ·
  `production/sprint-status.yaml` · `production/session-state/active.md`
- **删除**:仓库根空目录 `assets/audio/`(旧测试副作用的唯一产物)

**⚠️ 未 commit** —— 无用户指令。
