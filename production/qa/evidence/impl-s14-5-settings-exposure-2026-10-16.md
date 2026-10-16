# S14-5 实现证据 —— story-011(设置暴露面与归零机制)· 2026-10-16

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/audio-system/story-011-settings-exposure-reset.md`
> **上游判定**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §2.5
> (011 = 11 测中 🟢 真 1 · 🟡 半真 3 · 🔴 假 7)
> **sprint**:`production/sprints/sprint-14.md` S14-5(Should Have · M 件)
> **用户裁定(2026-10-16,AskUserQuestion ×4)**:
> ① 注册表落点 = **独立注册文件 `assets/data/audio_settings_registry.json` + `MixerRegistry` 扩名**
> (不动事件表三层白名单,不耦合尚未落地的 story-010 binder)·
> ② AC-44-15 [A] 的 48 口述半边 = **如实登记 NOT-RUN + 缺口**(不代 48 立契约 / 不扩前缀 / 不登种子)·
> ③ `ISettingsStore` / 归零机制 = **全落生产**(接口 + 真 sidecar + 归零三步,落
> `Gameplay.Presentation/Settings/` 中立目录)·
> ④ AC-44-13 出厂默认真源 = **落烘焙数据**,`PlayerBusVolumeInitializer.FactoryDefaults`
> 改读它、原 C# 常量降为回退。

---

## §1 原判定 → 修复落点

| # | 原判定(评审原件 §2.5) | 严重度 | 修复落点 | 验证命令 |
|---|---|---|---|---|
| 1 | `settings_visible` 注册表**生产侧不存在**(`grep assets/` 零命中) | 🔴 | `assets/data/audio_settings_registry.json`(8 行)+ `MixerRegistry.SettingsVisibleParameters`(名字半边)+ `AudioSettingsRegistryBinder`(构建期名字/值双向对账) | 反证 ⑥⑦⑧⑨⑫ + `test_bakedRegistry_*` 8 条 |
| 2 | 注册表断言宾语 = **测试本地 8 元数组** | 🔴 | 改打 `MixerRegistry.SettingsVisibleParameters` + 烘焙 JSON | `test_settingsRegistry_*` 4 条 |
| 3 | **AC-44-13 的「出厂默认 ∈ 烘焙分区」半边被删空**(只剩注释块) | 🔴 | 恢复为真断言:`test_monoOption_factoryDefault_fromBakedPartition` + `test_busVolumeFactoryDefaults_matchBakedPartition` | 该两条自身即判据 + 反证 ⑨⑪ |
| 4 | 归零三步测打**自造 `FakeSettingsStore` / `FakeResetMechanism`**(恢复路径硬编码被断言的期望值) | 🔴 | `AudioSettingsStore`(真 store + 三步 + `ResetOutcome`)+ `AudioSettingsSidecar`(真两文件 sidecar) | 反证 ②③⑤⑩ + `test_resetMechanism_*` 6 条 |
| 5 | `ISettingsStore` / `IResetMechanism` **全库无权威定义**(测试作者的发明) | 🔴 | `ISettingsStore` 落生产(文件头含**归属诚实声明**:来源是测试,不是设计文档) | 编译期存在性 + `test_settingsRegistry_unregisteredParam_rejected` |
| 6 | 字幕键集测用**测试自写 `HasSubtitleForCue`**(复刻生产判据) | 🟡 半真 | 改打 `AudioEventTableGates.ValidateSubtitles` + `IsSubtitleCue`(**判据单一出处**) | `test_subtitleKeySet_*` 2 条(打生产门) |
| 7 | 字幕键集负例 = **本地字符串字面量**(永远不包含子串 ⇒ 恒真) | 🔴 | 改打仓库夹具 `tests/unit/audio_system/fixtures/invalid_subtitle_missing.json` + 生产门 | `test_subtitleKeySet_missingSubtitle_rejected` |
| 8 | AC-44-15 [A] 的「**含 48 口述**」半边**空真**(48 口述不在事件表、前缀不匹配) | 🟢→空真 | **如实登记 NOT-RUN + 缺口哨兵**(用户裁定 ②) | `test_tutorialNarrationCues_registeredAsGap_notVisibleToGate` |
| 9 | 单位域**无收口**:GDD 注册表行同时写「float 0–1」与「dB −80–0」 | 🟡 | `VolumeUnitMapping`(线性 ↔ dB 单点换算 + 钳位) | 反证 ④ + `test_volumeUnitMapping_*` 2 条 |

## §2 交付面

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(数据) | `assets/data/audio_settings_registry.json` | 8 行闭枚举(`schema_version` / `_note` / `rows[]`);行 = `param`/`type`/`min`/`max`/`factory_default`/`player_group`/`label_source` |
| 新(生产) | `Gameplay.Presentation/Settings/SettingsValue.cs` | `SettingsValueKind` 闭集 + `SettingsValue` readonly struct(**无装箱**,承 ADR-029 §① 同一取向) |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsRegistry.cs` | `SettingsRegistryRow` + 注册表(`Provider` 缝 / `ParamNames` 转发 `MixerRegistry` / `FallbackRows` 非真源) |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsSidecar.cs` | 两文件 sidecar(自写行格式载荷 + 自写头部)· 读序(头 → 签名 → 载荷)· 失配改名备份 `.bak-NNN` · FNV-1a 32 签名 |
| 新(生产) | `Gameplay.Presentation/Settings/VolumeUnitMapping.cs` | `LinearToDb` / `DbToLinear`(钳位 `[−80, 0]`;编码防线:禁调用点自行 `Math.Log10`) |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsStore.cs` | 真 store(持值 + 三步归零 + `ResetOutcome` 可诊断)· `MixerPush` 注入缝 · **原子载入**(先校验后落值) |
| 新(生产) | `Gameplay.Presentation/Settings/ISettingsStore.cs` | 设置 store 契约(**含归属诚实声明**) |
| 新(构建期) | `Editor.Tools.Bake/AudioSettingsRegistryBinder.cs` | 阶段 2 绑定器:名字对账 / 值对账(双向)/ 域检查(聚合 throw) |
| 新(构建期) | `Editor.Tools.Bake/AudioSettingsRegistryBake.cs` | 仓根装载器(binder 的唯一调用点链路;不碰 `UnityEngine`) |
| 改(生产) | `Gameplay.Presentation/Audio/MixerRegistry.cs` | `MonoParameter` + `SettingsVisibleParameters`(8 员;名字半边**单一出处**) |
| 改(生产) | `Gameplay.Presentation/Audio/PlayerBusVolumeInitializer.cs` | `FactoryDefaults` 改读烘焙注册表(经 `VolumeUnitMapping` 换算);原 C# 常量降为回退 |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs` | 11 → **31 测**;四处自造替身**全数删除** |

**关键设计**:
- **名字半边 vs 值半边** —— 名字(`SetFloat` / `FindSnapshot` 需要编译期可见)=
  `MixerRegistry.SettingsVisibleParameters`;**值**(出厂默认 = 烘焙分区)= 作者态 JSON。
  binder **引用**名字常量、**对账**值,禁另抄(ADR-024 §⑥)。
- **回退不可能静默漂移** —— `AudioSettingsRegistry.FallbackRows`(代码内)与作者态 JSON
  由 binder **双向逐键对账**:任一侧多 / 少 / 值不同 ⇒ 构建失败。
- **载荷刻意不用 JSON** —— 玩家构建零 JSON 解析器(ADR-014 §二 / AC-44-D4);
  自持一个无解析器的行格式(唯一解析 = 按 `=` 切分 + 类型转换)。

## §3 证伪轮 —— 12 例(停用生产码守卫 ⇒ 断言转红)

**纪律**:每例**改 → 跑 → 还原**在**同一命令内**完成(`tmp-fz-s145.py`):
① 先把干净文件读到内存;② 写坏;③ 跑 Unity EditMode(`--filter SettingsExposure`);
④ `finally` 块还原;⑤ 还原后 **SHA-256 逐字节校验**(断言失败即脚本崩)。

| 例 | 停用的守卫(生产码) | 转红断言 | 红数 |
|---|---|---|---|
| ① sig | `ComputeRegistrySignature` 恒返固定串 | `test_sidecar_signature_isSensitiveToParamSet` | 1 |
| ② writestep | 归零第三步失败判定 `if (!written)` → `if (false)` | `test_resetMechanism_sidecarWriteFailure_fails` | 1 |
| ③ pushstep | 归零第二步缝缺失判定 `if (push == null)` → `if (false)` | `test_resetMechanism_missingPushSink_fails` | 1 |
| ④ dbmap | `LinearToDb` 换算 → 直通(`db = linear`) | `test_volumeUnitMapping_linearValue_notPassedThroughAsDb` | 1 |
| ⑤ eqfac | `EqualsFactoryDefaults` 值比较 → 恒真 | `test_resetMechanism_skipSidecar_rejected` · `test_resetMechanism_threeStepsReachFactory` | 2 |
| ⑥ unknownkey | binder 未知键白名单 → 停用 | `test_bakedRegistry_unknownRowKey_rejected` | 1 |
| ⑦ name | binder 名字对账「缺员」半边 → 停用 | `test_bakedRegistry_missingParam_rejected` | 1 |
| ⑧ value | binder 值对账 → 停用 | `test_bakedRegistry_defaultMismatchWithFallback_rejected` | 1 |
| ⑨ range | binder 出厂默认区间检查 → 停用 | `test_bakedRegistry_defaultOutOfRange_rejected` | 1 |
| ⑩ trunc | store 半截载荷缺键判定 → 停用 | `test_sidecar_truncatedPayload_rejected` | 1 |
| ⑪ mono | 回退 mono 行类型 `Bool` → `Float` | `test_bakedRegistry_realSeed_bindsWithZeroErrors` · `test_busVolumeFactoryDefaults_matchBakedPartition` · `test_monoOption_factoryDefault_fromBakedPartition` · `test_resetMechanism_pushSinkReceivesDbValues` · `test_resetMechanism_threeStepsReachFactory` | 5 |
| ⑫ boolrange | binder bool 行禁区间检查 → 停用 | `test_bakedRegistry_boolRowWithRange_rejected` | 1 |

**原始输出**:`unity/Logs/s14-5-fz.xml` 逐例覆盖。

### §3.1 证伪轮结果

**12 例全部转红**(合计 17 条断言转红;逐例见上表与 `tmp-fz-s145.py` 汇总输出)。

> ⚠️ **第一轮 ①⑦ 各 0 红、⑩ 脚本崩溃** —— 前两例是**夹具判别力缺陷(BLOCKING)**,
> 已修后重跑;⑩ 是**变异脚本锚点陈旧**(§3.2 改写生产码后未同步),非夹具问题。
> 详见 §3.3。**修复前的第一轮结果不可用作通过判据。**

### §3.2 ⚠️ 证伪轮**之前**就抓到的一处生产实现缺陷(如实登记)

新增的 `test_sidecar_truncatedPayload_rejected` 在**首跑即转红**(尚未进入证伪轮):

```
FAIL: test_sidecar_truncatedPayload_rejected |   失配后维持出厂默认
  Expected: True
  But was:  False
```

**根因**:`AudioSettingsStore.LoadFromSidecar` 原式**先 `_values.Clear()` 再逐键写入**,
键集完整性检查(`foreach ParamNames … if (!_values.ContainsKey(param)) return Mismatch`)
放在**写完之后** ⇒ 半截载荷已写进去的部分**不回滚**,store 停在
「一半旧值一半默认」的中间态 —— 而失配出口的契约是**维持出厂默认**。

**修法**:改**先校验后落值**(候选表 `candidate` 上做两项完整性校验,全过才整体换入 `_values`)。
文件内已就地登记该缺陷与由来。

> **登记理由**:这与 S14-3(假绿夹具)· S14-4(锚定缺陷)同型 ——
> **本 sprint 内第三次由证伪轮/新测抓到撰写者自己的缺陷**。区别:这次抓的是**生产实现**,
> 不是判据。它证明「写测试」本身就是一次对抗性检查。

### §3.3 ⚠️ 证伪轮**第一轮**抓到两处夹具判别力缺陷(如实登记,已修后重跑)

第一轮 12 例中 **① sig** 与 **⑦ name** 各 **0 红** ⇒ 按 `story-done` 的
Falsifiability Gate,弱判别面 = **BLOCKING**,须先修夹具(第三例 ⑩ 是变异脚本锚点陈旧,
非夹具问题 —— 生产码已由 §3.2 的「先校验后落值」改写,脚本仍锚旧行)。

| 例 | 现象 | 根因 | 修法 |
|---|---|---|---|
| **① sig** | 停用签名派生 ⇒ **0 红** | `test_sidecar_signatureMismatch_*` 用**字面量** `"deadbeef"` 作错签名 ⇒ 把 `ComputeRegistrySignature` 改成「恒返常量」**结论不变**。全库**从无**「签名对参数集敏感」的断言 —— 而这正是签名存在的唯一理由 | 补 `test_sidecar_signature_isSensitiveToParamSet`(缺员 / 多员**双向**) |
| **⑦ name** | 停用名字对账的缺员半边 ⇒ **0 红** | `test_bakedRegistry_missingParam_rejected` 的断言 `AnyError(errors, "mono_enabled")` 被**值对账**的报文(`运行期回退行「mono_enabled」在烘焙数据中不存在`)满足 ⇒ 打到了隔壁分支 | `AnyError` 增**两段式重载**,断言钉「同一条报文须**同时**含 `名字对账` + `mono_enabled`」 |
| **⑩ trunc** | 变异脚本**崩溃**(锚点未命中) | 脚本锚的是 `_values.ContainsKey`(§3.2 修复**前**的行),修复后已改为 `candidate.ContainsKey` | 脚本锚点同步为现行码 |

> **登记理由(第二次同型)**:与 §3.2 同型 —— **夹具的「绿」不等于夹具在测东西**。
> ① 尤其典型:签名测试**看起来**在测签名(构造了错签名、断言失配),实则对派生本体
> **零判别力**。这正是 `story-done` 立证伪轮的原因 —— 若无此轮,这条会以绿留下。

## §4 计划判据逐条兑现(sprint-14.md S14-5 行)

| 计划判据 | 兑现 | 证据 |
|---|---|---|
| ① 11 测换真 | ✅ **11 → 31 测**;四处自造替身全删 | `settings_exposure_test.cs`(无 `FakeSettingsStore` / `FakeResetMechanism` / `SettingsVisibleRegistry` / `HasSubtitleForCue`) |
| ② `assets/` 下须有 `settings_visible` | ✅ `assets/data/audio_settings_registry.json`(全库唯一命中) | `grep -rln "settings_visible" assets/` ⇒ 1 文件 |
| ③ `ISettingsStore` 落 `Gameplay.Presentation`(**禁 `Sim`**) | ✅ `Gameplay.Presentation/Settings/ISettingsStore.cs`;`Sim` 侧零命中 | `grep -rln "ISettingsStore" unity/` ⇒ 2 文件,均在 `Gameplay.Presentation` |
| ④ **恢复 AC-44-13 被删空的断言** | ✅ 两条真断言(`fromBakedPartition` + `matchBakedPartition`) | §1 行 3 |
| ⑤ sidecar 承 3 规则四模式 | ✅ 两文件(载荷 + 自写头部)· 读序(头 → 签名 → 载荷)· 失配改名备份 `.bak-NNN` + 三要素日志 | `AudioSettingsSidecar` 文件头 + `test_sidecar_*` 3 条 |

## §5 回归终值

**`3572 / 3526 / 0 / 46`**(`unity/Logs/s14-5-final.xml`,2026-10-16)
—— 基线 S14-4 = `3552 / 3506 / 0 / 46` ⇒ **+20 / +20 / 0 / 46 不变,零回归**。

差额精确吻合:`settings_exposure_test` 由 **11 → 31 测**(+20)。
`SettingsExposure` 过滤套件单跑 = **31 / 31 / 0 / 0**(`unity/Logs/s14-5-settings.xml`)。

## §6 门保持

| 门 | 判据 | 结果 |
|---|---|---|
| b5 | 含 `AudioCueDto`/`IAudioCueSink`/`AudioCueHandle`/`TierSource` token 的文件须声明 44 命名空间 | ✓ `Settings/` 6 文件零 token(`grep` 零命中)⇒ 走 `CheckAudioEscapeText` 放行;`AudioSettingsRegistry.cs` 只 `using` 44 命名空间(不引 token) |
| b2/b5 引用集 | `Gameplay.Presentation` 引用集漂移 | ✓ 未新增引用(只加 `using DaYiJingCheng.Gameplay.Presentation.Audio;` = 同装配内) |
| b5① | 44 源禁引 `Sim`/`Sim.Codec` 命名空间 + 十项禁入契约类型 | ✓ `Settings/` 6 文件 `grep "DaYiJingCheng\.Sim"` **零命中** |
| ADR-025 §① | `Sim` 引用集 = {BCL, `Sim.Contracts`} | ✓ 本件**未碰 `Sim` 装配** |
| ADR-029 §③ | `Sim/` 内 `new PayloadRef(` | ✓ 本件不涉 |
| ADR-014 §三 | 未知键 = 烘焙期硬失败 | ✓ `AudioSettingsRegistryBinder.RejectUnknownKeys`(根 + 行两层) |

## §7 NOT-RUN 如实登记(禁借绿)

- **AC-44-15 [A] 的「含 48 口述」半边** —— **NOT-RUN(缺口)**。实测:
  48 的 4 支教学口述(`assets/data/48_tutorial_content.json` 的 `narration[].cueId` =
  `teach_observe_01` / `teach_judge_01` / `teach_close_01` / `teach_archive_01`)
  ① 不在 `assets/data/audio_events.json` 的 `rows[]`;② 前缀不匹配已登记的
  `SubtitleCuePrefixes`(`Voice_` / `Narration_`)⇒ 生产门**看不见它们**,
  「表键集 ⊇ 语声 cue 集(含 48 口述)」的字面在本门下**恒真**。
  **处置(用户裁定 ②)**:`test_tutorialNarrationCues_registeredAsGap_notVisibleToGate`
  作**缺口哨兵**钉住该事实;48 侧契约落地后它会**转红** ⇒ 须随之改判,**不得当回归修掉**。
  不代 48 立契约、不扩前缀、不登种子。
- **mono 的运行期载体** —— **缺口(未裁定)**。实测 `.mixer` 的 `m_ExposedParameters`
  恰 12 员(7 `bus_volume_*` + 5 `tier_*`,由 `MixerTopologyGates` 断言),**不含
  `mono_enabled`**。注册表第 8 行只承载「成员身份 ∈ 闭集」;其 mixer 侧映射
  (AudioSettings 单例 / mixer effect / 声像折叠)是**尚未裁定的资产拓扑问题**。
  已在 `MixerRegistry.MonoParameter` 文件注与 JSON `_note` 就地登记。
- **AC-44-15 [L] 字幕听测签署**(WCAG 1.2.2)—— **NOT-RUN**(人工签署,项目外部)。
- **AC-44-12 [L] 文档判据** —— 测试覆盖**文档存在性**(三条具名项在位),
  **不覆盖** a11y 人工走查本身。
- **归零钮的控件形态 / 焦点导航** —— 归 **42 元件轮**(OQ-SS-7=甲 分工),本件不涉。
- **运行期装载面**(`AudioSettingsRegistry.Provider` 缝的实际注入 = 读烘焙分区)——
  归 **story-010** 的 Addressables 接线轮;本件交付缝 + 回退 + 构建期对账。
- **IL2CPP 三格**(ADR-012 矩阵)—— **NOT-RUN**:本机仅 Mono;CI 真 runner 未配。

## §8 登记项(本批不做)

- **story-011 转 `Complete`** —— 归 **S14-7**(6 条复评批,`sprint-14.md` DoD 逐字要求)。
- **`AudioSettingsRegistry.Provider` 的 Addressables 注入** —— 归 S14-6(story-010)。
- **`FallbackRows` 的最终退役** —— Provider 常驻后退居二线;退役时机归装配轮。
