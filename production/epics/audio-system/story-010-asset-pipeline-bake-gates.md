# Story 010: 素材管线与事件表烘焙门

> **Epic**: 音频系统
> **Status**: Complete ✅ 2026-10-16(S14-7 复评结案 —— 假绿撤回 → S14-6 生产实现 + 证伪轮全红 → 本节复评转正)
> **Layer**: Foundation(系统分类;实现落编辑期工具链)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

> **⚠️ 2026-10-16 假绿复核 —— Status 由 `Complete` 撤回为 `In Review`**
> sprint-13 S13-9 补评判定:本 story 的 AC 语义层**未被验证** —— 音频事件表烘焙管线
> (`Editor.Tools.Bake/` **实测零 audio 文件**)/ Addressables 音频组 / 听诊预载门**不存在**,
> 测试以自造夹具顶替 ⇒ 断言在「被测对象不存在」时仍为绿(**BLOCKING 级**)。
> 实测:11 测中 🟢 真 0 · 🟡 半真 4 · 🔴 假 7。
> ⚠️ **本 story 的修复轮(B1/B2/B4)把「恒真断言」换成了「自包含夹具恒真」** —— 同为假绿。
> **评审原件**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`(§2.4 逐条表)。
> 实现另立 sprint;本 story 维持 `In Review` 直至生产类落地并经证伪轮复评。

> **✅ 2026-10-16 S14-6 实现轮落盘 —— 生产件已落地 + 证伪轮 11/11 全红 + 用户裁定四项逐条兑现**
> 落地面:烘焙管线(`AudioEventTableBinder` / `CookedWriter` / `AudioEventTableBake` 三件套 +
> `AudioSettingsRegistryCookedWriter` 补口)· D6 存在性谓词 `AudioAssetExistence`(单一出处)·
> D5 组区分门 `AudioAssetGroupGates`(纯谓词 + 真设置两半)· 听诊预载门 `StethoscopePreloadGate` ·
> 5 个占位 wav · 运行期 `AudioEventDataSet` + `AudioEventCookedCodec` · `AddressablesDataProvider`
> 两条 audio 支路 · `BootRoot` 的 `AudioSettingsRegistry.Provider` 注入。
> **四个自造替身 + 文件系统写入副作用整段删除**;**11 测 → 52 测**。
> 落地内容见下方 `## Completion Notes §S14-6` 与
> `production/qa/evidence/impl-s14-6-asset-pipeline-2026-10-16.md`。
> ⚠️ **Status 仍为 `In Review`** —— 转 `Complete` 归 **S14-7**(6 条复评批)。

## Context

**GDD**: `design/gdd/audio-system.md`(硬约束 7 · Edge Cases 素材与管线段 · §Dependencies ADR-014 行)
**Requirement**: TR-audio-010(音频事件表走 ADR-014 烘焙:玩家构建零 JSON 解析器)

**ADR Governing Implementation**: ADR-014: 数据管线与 JSON 解析器(主:两阶段 · 硬失败 · Addressables §五)+ ADR-018 §五(素材 = `assets/audio/` Addressables 流式,与 `data-core` 组区分)
**ADR Decision Summary**: 编辑期烘焙门拒绝缺失/越界;玩家构建零 JSON、零 `FixParse`;素材流式预载策略有归属。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Addressables 6.2+ 抛异常 post-cutoff;E-13 先例 = 启动期硬失败不降级)
**Engine Notes**: 「听诊窗预载」原指针悬空(ADR-014 §五只管 `data-core`)—— 二轮 REC 落点:预载机制 = `DownloadDependenciesAsync`/`LoadAudioData`(`addressables.md`/`audio.md` 已核),门与 AC 在本 story 落;运行期失败语义承 ADR-014 既有挂账。

**Control Manifest Rules (this layer)**:
- Required: 素材缺失 = 编辑期门拒绝(非运行期降级)· 事件表走两阶段烘焙 · `assets/audio/` 与 `data-core` 组区分登记
- Forbidden: 玩家构建带 JSON 解析器 · 运行期静默播「空 cue」· 开发构建直读原文件进玩家路径
- Guardrail: 高频 cue(呼吸/脚步)进对应生态区/场景时预载;听诊窗预载(运行期 AC)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **AC-44-D4**:音频事件表走 ADR-014 烘焙管线;玩家构建**零 JSON 解析器**(构建产物扫描:无 Newtonsoft/解析器符号进 player 程序集)。— **S14-6 落**:`Gameplay.Presentation` 装配引用集断言(含扫描键非空守卫)+ 44 类型签名扫描 + 运行期 codec 只解字节。
- [x] **AC-44-D5**:素材本体 = `assets/audio/`(Addressables 流式),与 `data-core` 组区分(组条目扫描断言;`Editor.Tools.Gates` 读 `AddressableAssetSettings`)。— **S14-6 落**:`AudioAssetGroupGates.ValidateSettings`(真设置,本机 `audio` 组已建)+ `ValidateEntries`(纯谓词负向)。
- [x] **AC-44-D6**:素材缺失 = **编辑期烘焙门拒绝**(事件表 `assets[]` ↔ 文件存在谓词,入 ADR-014 阶段 2 谓词集;负向:删一个 wav ⇒ 构建失败,不是运行期降级)。— **S14-6 落**:`AudioAssetExistence`(门强制,`assetRoot` 必填)+ `AudioEventTableBinder.Bind` 路径必经。
- [x] **听诊窗预载**(Edge Cases 素材段):进入 `StethoscopeFocus` 前听诊族 cue `loadState == Loaded`(PlayMode 断言,未就绪不进听诊);一次性 cue 迟发 ≤ 登记常量(「极小迟发」量化)。— **S14-6 落**:`StethoscopePreloadGate`(纯谓词,注入式 `LoadStateQuery`);⚠️ **真 `AudioClip.loadState` 的 PlayMode 半边 = NOT-RUN**(缺口哨兵 `test_preload_realAudioClipLoadState_pendingPlayModeHalf` 钉住)。

---

## Implementation Notes

*Derived from ADR-014 §三/§五 + GDD(2026-09-25 二轮指针修)*:

- **谓词集追加**:ADR-014 阶段 2 现有谓词(白名单/守恒/区间/长度/schema_version)增「素材文件存在」—— `Editor.Tools.Bake` 显式加列。
- 预载策略**不再指 ADR-014 §五**(该节只管 `data-core`,二轮已确认悬空)—— 归本 story + `audio.md` 机制(`LoadAudioData` + `loadState`);E-13 失败抛异常形态须 try/catch 显式处理(启动不崩,听诊不可入)。
- 开发构建允许直读原文件(Edge 玩家构建零 JSON 红线不破)。
- 素材 Import Load Type(呼吸/语声族 = DecompressOnLoad 族)按 `audio.md` 建议 pin,防流式放大首次迟发 —— 具体值随 content 批。
- 高频 cue 预载触发点 = 进入生态区/场景(与 ADR-014 的 `data-core` 首次 `Step` 前预载并列,组不同)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 事件表**内容**校验(白名单/schema)—— 本 story 是**管线与素材存在性**面
- 内容批:wav 素材本体产出与 Import Settings 定稿(`OQ-44-2`)

> **⚠️ 2026-10-16 S14-6 就地改注(裁定 ① 扩了本件范围)**:原第二行「wav 素材本体产出」**整体**
> 归内容批 —— S14-6 起**收窄**为:**「文件存在」这一条判据的载体(5 个占位静音 wav)属本件**,
> 落 `unity/Assets/Audio/`;**素材真实内容 + Import Settings 定稿仍归内容批**(`OQ-44-2`)。
> 依据:AC-44-D6 的**正向半边**必须由真种子证明(两轮评审的病根都是「自包含夹具恒真」),
> 而真种子 `audio_events.json` 的 5 行引用 5 个 bare wav 名 ⇒ 素材缺席时正向半边只能靠替身,
> 即重蹈假绿。占位 wav = 2044 B = 44 B RIFF/WAVE 头 + 2000 B 全零 PCM(单声道 44100 Hz 16 bit ⇒ 1000 样本 ≈ 22.7 ms 静音)(门与 Unity 导入都认),
> `.meta` 由 Unity 首次导入生成(不由脚本手写)。

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-D4**: 零解析器。Given: 玩家构建产物。When: 扫 player 程序集符号。Then: 无 Newtonsoft JSON 解析路径;事件表经 cooked 读取。
- **AC-44-D5**: 组区分。Given: Addressables 设置。When: 枚举组条目。Then: 素材 ∈ `assets/audio` 组,`data-core` 组仅烘焙数据;混组 ⇒ 红。
- **AC-44-D6**: 存在性门。Given: 表中引用缺失 wav 的夹具。When: 烘焙。Then: 构建失败(聚合 throw);合法表绿。
- **听诊窗预载**: PlayMode。Given: 冷启动(素材未载)。When: 触发进入听诊。Then: 进入前听诊族 `loadState == Loaded` 才放行,否则不进(或预载完成后进);负向:跳过预载直入 ⇒ 断言红。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/audio_system/pipeline_gate_test.cs` — must exist and pass

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(**52 测**,S14-6 由 11 扩);账本互链 `tests/integration/audio_system/README.md`
**实现证据**: `production/qa/evidence/impl-s14-6-asset-pipeline-2026-10-16.md`
**⚠️ NOT-RUN(如实登记,禁借绿)**: 真 `AudioClip.loadState` 的 PlayMode 半边(素材本体未定稿)·
素材 Import Settings 定稿(归内容批 `OQ-44-2`)· IL2CPP 三格(本机仅 Mono,ADR-012 矩阵)

---

## Dependencies

- Depends on: Story 002(事件表内容先合法)· 011 无
- Unlocks: Story 004/006 的 [L] 听测(素材就位前提)· content 批交付接口

## Completion Notes

> ⚠️ **本节的「4/4 passing」判定已于 2026-10-16 被 S13-9 补评推翻**
> (`review-audio-epic-fake-green-2026-10-16.md` §2.4:11 测中 🟢 真 0 · 🟡 半真 4 · 🔴 假 7)。
> 原文**保留不改写**(历史陈述),现行状态见下方 **§S14-6**。

**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-44-D4 零 JSON 解析器 · AC-44-D5 Addressables 组区分 · AC-44-D6 素材存在性门 · 听诊窗预载;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(烘焙管线/Addressables 组配置)尚未实现,本 story 测试用自持谓词面(`FakePreloadGate`/`GroupSeparabilityPredicate`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **音频素材组测试用 `Assert.Ignore` 跳过**:AC-44-D5 要求音频素材组存在且与 data-core 区分,但音频素材组配置归 content 批(Story 010 Out of Scope),当前阶段缺失是正常的。测试用 `Assert.Ignore` 明确标记跳过(不判失败),待 content 批补齐后取消忽略。
3. **素材存在性门用自包含夹具**:真种子 `audio_events.json` 引用了不存在的 wav 文件(`vo_patient_cough_damp_m1.wav` / `vo_patient_cough_damp_f1.wav`),但 wav 素材本体归 content 批(Story 010 Out of Scope)。测试用自包含夹具验证谓词逻辑本身工作正常,不要求真种子完整。

**评审与修复**:双评审并行(代码质量面 5 BLOCKING + QA 覆盖面 3 BLOCKING)→ 全修:
- **B1**(代码面):`test_addressables_audioGroupSeparateFromDataCore` 恒真断言(`Assert.Pass` 假绿)→ 改为 `Assert.Ignore`(音频素材组未配置时跳过)。
- **B2**(代码面):`test_addressables_mixedGroup_rejected` 恒真负例(对测试自己构造的字符串做 Contains)→ 改为调用谓词函数 `GroupSeparabilityPredicate.IsMixedGroup`。
- **B3**(QA 面):`Prefix` 字面量重复定义(违反文件自身声明的纪律)→ 改为 `AssemblyGates.AudioModuleNamespacePrefix`。
- **B4**(代码面):`test_assetExistence_missingWav_rejected` 恒真断言(对测试自己创建/未创建的文件做 File.Exists)→ 改为自包含夹具验证谓词逻辑。
- **B5**(代码面):`test_preloadGate_oneShotCueDelay_bounded` 恒真断言(双端硬编码)→ 保留但注释标注"实际迟发待生产实现后接入"。
- **R1**(代码面):未使用 `using DaYiJingCheng.Sim.Contracts` → 删除。
- **R3/R4**(代码面):死代码 `ExtractGroupNames` / `ExtractJsonStringArray` → 删除。

**残余 NICE(登记不修)**:
- `ExtractFieldName` 用朴素字符串匹配解析 YAML,脆弱(当前阶段可接受)。
- `ProductionAudioTypes()` 每次调用都反射加载全装配类型(当前 15 个类型,性能无问题)。
- 三个 `test_noJsonParser_*` 测试只扫描公开方法签名,不覆盖字段/基类/IL(深度 IL 扫描归 `assembly_boundary_test.cs`)。
- `test_preloadGate_stethoscopeFocus_preloaded` 只测了一个 cue(建议参数化)。
- `test_preloadGate_notLoaded_blocks` 注释提到"E-13 失败抛异常形态须 try/catch"但测试本身没有 try/catch。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(**52 测全过**;S14-6 由 11 扩)→ ⚠️ 下行「11 测:10 passed + 1 ignored / 1144」是 **2026-09-28 首轮**(假绿轮)的陈述,现行回归见 `## Completion Notes §S14-6`。
**Code Review**: Complete —— 双评审并行 + B1-B5 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-014(数据管线与 JSON 解析器:两阶段烘焙,玩家构建零 JSON 解析器)· ADR-018 §五(素材 = assets/audio/ Addressables 流式)· ADR-025 §①(契约程序集清单)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。

---

## Completion Notes §S14-6(2026-10-16 · 生产实现轮)

> **本节的判据 = 变异测试**(停用生产码守卫 ⇒ 断言转红),**不是**「测试名 + 行号」——
> 承 `sprint-14.md` Risks 第 3 条与 retro-13 行动项 2。
> **证据原件**:`production/qa/evidence/impl-s14-6-asset-pipeline-2026-10-16.md`。

### 用户裁定四项(2026-10-16,AskUserQuestion)

① **D6 门强制**(非可选参数)+ 落 **5 个占位静音 wav** —— 正向半边由**真种子**证明,
   判别力由负向夹具证明(两轮评审的病根都是「自包含夹具恒真」,不能让正向半边重蹈);
② **素材根 = `unity/Assets/Audio/`** —— GDD `:258` / `:695` / `:1092` 与 ADR-018 `:153` 的
   `assets/audio/` **就地加补注**订正为逻辑路径(承 ADR-015 修订 ADR-009 先例:原文保留 + 加注);
③ **听诊预载门 = 真生产谓词 + `loadState` 查询缝**;真 `AudioClip.loadState` 的 PlayMode 半边
   **如实登记 NOT-RUN** + 缺口哨兵;
④ **含最小接线** —— 落 cooked 字节 → 表的运行期读取面,并接上 story-011 §登记项
   明确留给本件的 `AudioSettingsRegistry.Provider` 注入。

### 交付面(新 11 + 改 8 + 素材 5)

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(烘焙) | `Editor.Tools.Bake/AudioEventTableBinder.cs` | 阶段 2 绑定;三层白名单**引用** `AudioEventTableGates`(禁另抄,ADR-024 §⑥)+ **D6 存在性谓词**;聚合 `BakeValidationException`;`assetRoot` **必填** |
| 新(烘焙) | `Editor.Tools.Bake/AudioEventTableCookedWriter.cs` | deterministic 编码;头槽写 `bound.SchemaVersion`(**不写 0** —— R-1 教训) |
| 新(烘焙) | `Editor.Tools.Bake/AudioEventTableBake.cs` | 仓根装载器(不碰 `UnityEngine`/Addressables);`BakeFromRepo` / `BakeFromJson` / `Materialize`(真往返)/ `AssertHeaderContract` |
| 新(烘焙) | `Editor.Tools.Bake/AudioAssetExistence.cs` | **D6 谓词单一出处**;`AssetRootRelative = "unity/Assets/Audio"`;`Missing` 保序去重,null 根 ⇒ **抛** |
| 新(烘焙) | `Editor.Tools.Bake/AudioSettingsRegistryCookedWriter.cs` | 设置注册表 cooked **写面**(S14-5 刻意留白的补口);行序 = `param` Ordinal 升序 |
| 改(烘焙) | `Editor.Tools.Bake/AudioSettingsRegistryBake.cs` | 补 cooked 输出 + `Materialize` + `AssertHeaderContract` |
| 改(烘焙) | `Editor.Tools.Bake/DataBakeMenu.cs` | 三菜单项:`BakeAudioEvents` · `BakeAudioSettingsRegistry` · `EnsureAudioGroup` |
| 新(门) | `Editor.Tools.Gates/AudioAssetGroupGates.cs` | AC-44-D5 执行体,**两半分离**:纯谓词 `ValidateEntries` + 真设置 `ValidateSettings`;判据 = **分离**,不是组名 |
| 改(门) | `Editor.Tools.Gates/Editor.Tools.Gates.asmdef` | +`Unity.Addressables` / `Unity.Addressables.Editor`(ADR-025 §① 清单已注记) |
| 新(生产) | `Gameplay.Presentation/Audio/StethoscopePreloadGate.cs` | 听诊窗预载门(纯谓词;`FromTable` 从事件表派生听诊族 —— cue 集单一出处 = 表);空集 / null 查询 = **构造期硬失败** |
| 新(生产) | `Gameplay.Presentation/Audio/AudioEventDataSet.cs` | 事件表运行期载体(行集 + tier_map + 双版本号;零 `UnityEngine`) |
| 新(生产) | `Gameplay.Presentation/Audio/AudioEventCookedCodec.cs` | cooked 读方(与写方**严格镜像**);载荷 schema_version ↔ 头槽比对;E-13 硬失败 |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsDataSet.cs` | 设置注册表运行期载体 |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsRegistryCookedCodec.cs` | 设置注册表读方(同款守卫) |
| 改(生产) | `Gameplay.Presentation/AddressablesDataProvider.cs` | 两条 audio 支路(**单一出处** = 两 `*DataSet.CookedFileName`)+ `Preload()` 追加 + 未知类型错误串订正 |
| 改(生产) | `Gameplay.Boot/BootRoot.cs` | `ApplyAudioSettingsProvider(bool, IDataProvider)`(**静态可直驱** —— 内联在 `Start()` 里则 EditMode 无从断言) |
| 新(素材) | `unity/Assets/Audio/*.wav` ×5 | 占位静音 wav(2044 B = 44 B RIFF/WAVE 头 + 2000 B 全零 PCM(单声道 44100 Hz / 16 bit / 1000 样本 ≈ 22.7 ms 静音));`.meta` 由 Unity 首次导入生成(**默认 Import Settings,未 pin** —— `music_*` 档按 `audio.md` 尺寸规则应为 Streaming) |
| 改(测试) | `Tests/EditMode/Audio/pipeline_gate_test.cs` | **11 → 52 测**;四个自造替身 + 文件系统写入副作用**全数删除** |
| 改(测试) | `Tests/EditMode/Audio/assembly_boundary_test.cs` | `EntryAllowlist` 增列 5 项(B3 门;附先例式理由) |

### 修复的病灶(评审原件 §2.4 逐条)

| 原假绿 | 判定 | S14-6 处置 |
|---|---|---|
| `Editor.Tools.Bake/` **零个 audio 文件** | 🔴 | 落三件套 + `AudioSettingsRegistryCookedWriter` |
| D4 两条扫描测**无 `Count > 0` 守卫** | 🟡 | 补扫描键非空守卫 + 新增装配引用集断言 |
| Addressables 音频组不存在;全库零门读 `AddressableAssetSettings` | 🟡 | `AudioAssetGroupGates`(两半)+ asmdef 补引用 + 建组菜单 |
| D5 负向打**测试本地字符串 + 自造谓词** | 🔴 | 删自造谓词;改打 `ValidateEntries` |
| D6 两条测**自建文件 + 内联 `File.Exists`** | 🔴 | 谓词单一出处 = `AudioAssetExistence`;正向打**真种子** |
| D6 正向半边从未跑过真种子 | 🔴 | 5 个占位 wav ⇒ `test_d6_realSeed_bakesWithNoMissingAsset` 真绿 |
| 听诊预载 3 测打自造 `FakePreloadGate` | 🔴 | `StethoscopePreloadGate`(真谓词 + 注入式 `LoadStateQuery`) |
| 「一次性 cue 迟发」= **两个硬编码 int 互比** | 🔴 | **该测删除**(恒真);判据面改由「全族就绪才放行」承担 |
| 测试**写仓库 `assets/audio/`**(违 `test-standards.md`) | 🔴 | 副作用整段删除 + `test_discipline_noFilesystemWrites` 结构性守卫 + 空目录 `rmdir` |
| 真 `AudioClip.loadState` 的 PlayMode 半边 | 🟡 悬空 | 承裁定 ③:**NOT-RUN** + 缺口哨兵 |

### 证伪轮 —— 11 例全红(停用生产码守卫 ⇒ 断言转红)

| 例 | 停用的守卫 | 红数 |
|---|---|---|
| **C1** | `AudioAssetExistence.Exists` ⇒ 恒真 | 4 |
| **C2** | 行级白名单摘除真键 `subtitle_text` | **56**(跨 story-002 连带 40+) |
| **C3** | D5(a) 不区分 `data-core` | 1 |
| **C4** | D5(b) `Assets/Audio/` 目录判定停用 | 首轮 **0** ⇒ 修夹具后 **1** |
| **C5** | 预载门「全 Loaded 才放行」判据取反 | 5 |
| **C6** | cooked 头槽 `schemaVersion` 写 `0`(R-1 缺陷类复现) | 12 |
| **C7** | 写方行序反转(跨侧等值断言须红) | 10 |
| **C8** | `AudioEventsAddress` 由单一出处改为另抄陈旧字面量 | 1 |
| **C9** | `BootRoot` 的 `Provider` 注入停用 | 1 |
| **C10** | 读方「载荷 schema_version == 头槽」守卫取反(audio_events) | 12 |
| **C11** | 同款守卫取反(settings 注册表) | 6 |

**还原校验**:11 例跑完后 9 个被改文件的 SHA-256 **逐项等于**基线(脚本在任一例还原不符时即中止)。
逐例输出见 `unity/Logs/s14-6-fz.xml` 与证据原件 §3。

### 回归终值

**`3613 / 3568 / 0 / 45`**(`unity/Logs/s14-6-final.xml`)—— 基线 S14-5 `3572 / 3526 / 0 / 46`
⇒ **+41 / +42,零结果变化**。逐 `fullname` diff:仅 S14-5 有 11 条(旧音频测名)·
仅 S14-6 有 52 条(新测名)· **结果变化 0 条**。跳过 46 → 45 的唯一原因 =
旧 `test_addressables_audioGroupSeparateFromDataCore`(Skipped)被
`test_d5_realSettings_audioAssetsSeparateFromDataCore`(Passed)取代 —— 即 **P4 转真绿**。
`DaYiJingCheng.Tests.Unit.Audio` 过滤套件 = **395 / 395 / 0 / 0**。

### ⚠️ 证伪轮抓到的缺陷(如实登记)

**C4 弱判别面(BLOCKING 级,已修)** —— 首轮 **0 红**:`test_d5_pure_strayAudioEntryInDataCore_rejected`
的**期望集成员未合法登记** ⇒ (a) 半边先报「未登记进任何组」,断言只写 `errors Is.Not.Empty`
⇒ **被 (a) 半边的误报满足**,与 (b) 是否生效无关。

修 = ① **夹具形态**:期望集成员合法登记在 `audio` 组,漏网条目设为**另一个**文件(`orphan.wav`)
⇒ (a) 半边通过,判据面只剩 (b);② **报文钉定**:`Does.Contain("orphan.wav")` +
`Does.Not.Contain("sfx_breath_base_loop_small")`。复跑同一条 C4 变异 ⇒ **1 红**。

> **登记理由**:本 sprint **第五次**由证伪轮抓到撰写者缺陷,且**第二次**抓到「夹具判别力」本身
> (S14-5 第 ⑦ 例是第一次)。两例同型:**断言只写「非空 / 抛了」而不钉「是谁报的」**,
> 就会被同一函数内**别的**分支的误报满足。⇒ 后续负向夹具一律**钉报文内容**。

**另一处(构造期、非证伪轮)**:`Materialize` 走真往返时抓到写方/读方布局分叉
(读方漏读载荷首字段 `schema_version`),修复后 10 条红转绿 —— 这是「真往返」相对
「直构」的可证伪性优势的实证。

### NOT-RUN 如实登记(禁借绿)

- **真 `AudioClip.loadState` 的 PlayMode 半边** —— NOT-RUN(素材本体未定稿);缺口哨兵
  `test_preload_realAudioClipLoadState_pendingPlayModeHalf` 钉住,**素材定稿后它转红 ⇒ 须随之改判,
  不得当回归修掉**。
- **素材 Import Settings 定稿**(呼吸/语声族 `DecompressOnLoad` 等)—— 归内容批(`OQ-44-2`)。
- **IL2CPP 三格**(ADR-012 矩阵)—— NOT-RUN:本机仅 Mono。
- **`AddressableAssetsData/` 是 gitignored 机器生成物** —— 每台机器首次须跑
  「确保 data-core / audio Addressables 组」菜单(与既有 `data-core` 同款)。
- **两份阶段 2 抽取器的已登记重复**(`AudioEventTableBinder` vs `event_table_gate_test.cs` 的
  手写 JSON 扫描器)—— **刻意不动**:后者承载 story-002 的截断夹具语义(`__truncated__` 哨兵);
  归 story-002 轮。

### 登记项

- **story-010 转 `Complete`** —— 归 **S14-7**(6 条复评批,`sprint-14.md` DoD)。
- **`Out of Scope` 第二行已就地改注** —— 占位 wav 归本件,真实内容 + Import Settings 仍归内容批。
- **素材批交付接口** —— 5 个占位 wav 替换为真实素材时,只需保持 `rows[].assets[]` 的 bare 名不变
  (D6 门与 Import Settings 的接口即此一条)。
