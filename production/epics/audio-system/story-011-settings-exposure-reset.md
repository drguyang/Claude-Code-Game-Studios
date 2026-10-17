# Story 011: 设置暴露面与归零机制

> **Epic**: 音频系统
> **Status**: Complete ✅ 2026-10-16(S14-7 复评结案 —— 假绿撤回 → S14-5 生产实现 + 证伪轮全红 → 本节复评转正)
> **Layer**: Foundation(系统分类;实现落表现层 + 边界层 store)
> **Type**: Config/Data
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

> **⚠️ 2026-10-16 假绿复核 —— Status 由 `Complete` 撤回为 `In Review`**
> sprint-13 S13-9 补评判定:本 story 的 AC 语义层**未被验证** —— `settings_visible` 注册表
> (`assets/` 下**零命中**)/ 设置 store / 归零机制**不存在**,测试以自造 Fake + 本地数组顶替
> ⇒ 断言在「被测对象不存在」时仍为绿(**BLOCKING 级**)。
> 实测:11 测中 🟢 真 1 · 🟡 半真 3 · 🔴 假 7。
> ⚠️ **AC-44-13 的「出厂默认 ∈ 烘焙分区」半边已被删空**(修复轮 B2 删断言留注释,注释还自称「非假绿」),
> 而 story 的 `Criteria` 仍记「4/4 passing」。
> **评审原件**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`(§2.5 逐条表)。
> 实现另立 sprint;本 story 维持 `In Review` 直至生产类落地并经证伪轮复评。
>
> **✅ 2026-10-16 S14-5 实现轮落盘 —— 生产件已落地 + 证伪轮 12/12 全红 + 计划五条判据逐条兑现**
> (本 story 仍 `In Review`:转 `Complete` 归 **S14-7** 的 6 条复评批,承 `sprint-14.md` DoD)。
> 落地内容见下方 `## Completion Notes §S14-5` 与
> `production/qa/evidence/impl-s14-5-settings-exposure-2026-10-16.md`。

## Context

**GDD**: `design/gdd/audio-system.md`(§UI Requirements 设置暴露面注册表 · 规则九 · AC-44-12/13/15/19)
**Requirement**: ⚠ 无专属 TR(无障碍/设置面 GDD AC 直承;七路注册表挂 **TR-audio-003**;字幕挂 **TR-audio-005 族**;registry 见报)
**ADR Governing Implementation**: ADR-018: 音频架构(主:mono/总线音量义务 + 44 只渲染)+ ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)
**ADR Decision Summary**: 44 拥有闭枚举 `settings_visible` 注册表与出厂默认;壳条目表 = 镜像;归零三步(写默认 → 推 mixer → 落 sidecar)承 OQ-SS-5=甲。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯数据 + store 机制;sidecar 承 3 规则四先例)
**Engine Notes**: 字幕呈现归 42(F9=乙:上游发射点扇出,44 不新增出向口);归零控件形态 = 42 元件轮(OQ-SS-7=甲 分工)。

**Control Manifest Rules (this layer)**:
- Required: 注册表 = 单一出处(壳表镜像)· 归零三步缺一无效 · 玩家 exposed ∩ 脚本面 = ∅
- Forbidden: 壳直写非注册表参数 · store 绕过 sidecar 落盘(归源无效)· 44 新增到 42 的出向接口(违只入不出)
- Guardrail: 分叉的只有音 —— 设置值是设备偏好(与存档位解耦,OQ-SS-5=甲),不得进 7a 存档头

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **注册表 AC①**(§UI Requirements):壳消费集 ⊆ `settings_visible` 闭枚举(8 条 = 7 总线音量含 Master + mono;越集 = 断言失败)。— **S14-5 落**:`assets/data/audio_settings_registry.json`(8 行闭枚举)+ `MixerRegistry.SettingsVisibleParameters`(名字半边**单一出处**)+ `AudioSettingsRegistryBinder`(阶段 2 未知键白名单 / 值对账 / 域检查)。证伪 ⑥(未知键白名单停用 ⇒ 1 红)· ⑦(binder 名字对账「缺员」半边 ⇒ 1 红)· ⑧(值对账 ⇒ 1 红)。
- [x] **AC-44-13**:提供 **mono 选项**(`mono_enabled` ∈ 注册表,出厂默认入烘焙分区)。— **S14-5 落**:注册表 `mono_enabled` 行(类型 `Bool`);`PlayerBusVolumeInitializer.FactoryDefaults` **改读烘焙注册表**(经 `VolumeUnitMapping`),原 C# 常量降为回退。证伪 ⑨(binder 出厂默认区间检查 ⇒ 1 红)· ⑪(回退 mono 行类型 `Bool` → `Float` ⇒ **5 红**)· ⑫(binder bool 行禁区间检查 ⇒ 1 红)。⚠️ **mono 的运行期载体 = 缺口**:`.mixer` 的 `m_ExposedParameters` 恰 12 员不含 `mono_enabled`,mixer 侧映射是**尚未裁定的资产拓扑问题**。
- [x] **AC-44-15**(双半):[A] 上游语声/口述 cue 发射双投递(cue→44,文本→42);文本本体 = 事件表 `subtitle_text`,**表键集 ⊇ 语声 cue 集**(缺失 = 构建失败;含 48 口述);[L] 字幕存在性听测签署(WCAG 1.2.2)。— **S14-5 落**:[A] 改打 `AudioEventTableGates.ValidateSubtitles` per-cue(`HasSubtitleForCue`,该 cue 行内 500 字符内查 `subtitle_text`)+ 仓库夹具负例(原本地字面量恒真已消);[L] **NOT-RUN**。⚠️ **「含 48 口述」半边 = NOT-RUN(缺口)**:48 的 4 支教学口述不在事件表、前缀不匹配 ⇒ 生产门看不见;缺口哨兵钉住,48 契约落地后转红。
- [x] **AC-44-19**(OQ-SS-7=甲):归源(写默认 → 推 mixer → **落 sidecar**)后 store == 出厂集;**重启后仍 == 出厂集**;负向:跳过 sidecar 落盘 ⇒ 红。— **S14-5 落**:`AudioSettingsStore`(真 store,持值 + 三步归零 + `ResetOutcome`,**原子载入**)+ `AudioSettingsSidecar`(两文件 sidecar + 自写头部 + 失配改名备份 `.bak-NNN` + FNV-1a 32)。证伪 ①(`ComputeRegistrySignature` 恒返固定串 ⇒ 1 红)· ②(归零第三步失败判定 ⇒ 1 红)· ③(归零第二步缝缺失判定 ⇒ 1 红)· ④(`LinearToDb` 换算直通 ⇒ 1 红)· ⑤(`EqualsFactoryDefaults` 值比较 ⇒ 2 红)· ⑩(store 半截载荷缺键判定 ⇒ 1 红)。⚠️ **归零钮控件形态 / 焦点导航** 归 42 元件轮(OQ-SS-7=甲)。
- [x] **AC-44-12** [L](文档判据):三条具名项在位 —— 视觉替代引 `AC-13-F1` · P0 非目标「隔墙听觉无视觉等价」(a11y L-3 已登)· mono/总线音量/语声字幕三义务(本 story 与 012/013 承接)。— **S14-5 落**:`test_a11yDocumentation_threeItemsPresent`(三条具名项逐条在位断言)。⚠️ **该 [L] 半的人工走查 = NOT-RUN**(项目外部)。

---

## Implementation Notes

*Derived from GDD §UI Requirements 注册表(2026-09-25 新增)+ OQ-SS-3/SS-5/SS-7 三裁(2026-09-25)*:

- **注册表结构**:`settings_visible` 行含参数名/类型/区间(`0–1`,dB −80–0)/出厂默认/`player_group` 字段/题签语义源;物理落点 = 44 侧数据(随事件表或独立注册文件,ADR-014 烘焙)。
- **OQ-SS-3=甲**:数据层恒 7 路;`player_group` 只影响壳呈现分组(归 42 元件轮)。
- **归零三步**与边界层 store:store = 默认值真源消费者 + mixer 推送方 + sidecar 落盘方(44 不持值 —— 只渲染宪法);sidecar 承 3 规则四模式(schema 头)。
- **字幕 F9=乙**:上游(13/48)发射点解析 `subtitle_text` 后扇出两消费方;44 无新出向口(只入不出不破);48 的口述经同通道(`AC-48-15`)。
- 与 42 的分权:控件形态/焦点/手柄输入 = 42 元件轮;本 story 交付数据面 + 机制面。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: `subtitle_text` 字段的 schema 校验([A] 半的表侧)—— 本 story 承消费与键集覆盖断言
- Story 012: E2 性能、18 乐层
- 42 元件轮:归零钮/滑杆的呈现与焦点(OQ-SS-7=甲 分工)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **注册表 AC①**: Given: 注册表 + 壳行目录。When: 求差集。Then: 消费 ⊆ 注册表;负向:壳引用未登记参数 ⇒ 红。
- **AC-44-13**: Given: 注册表。When: 查 `mono_enabled`。Then: 存在 + 出厂默认 ∈ 烘焙分区 + mixer 暴露映射正确。
- **AC-44-15 [A]**: Given: 语声 cue 全集。When: 校验 `subtitle_text` 键集。Then: ⊇ 语声 + 48 口述 cue 集;缺键 ⇒ 构建失败;[L]:字幕听测签署。
- **AC-44-19**: 归零机制。Given: 被改坏的 store 值。When: 归源。Then: 三步后 store==出厂;**重启进程复验仍==出厂**;负向:跳过第三步 ⇒ 重启后红。
- **AC-44-12** [L]: 文档三件在位核对(a11y L-3 行 + F1 引用 + 三义务 AC 存在)。

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- 烘焙/注册表断言测试 + `production/qa/evidence/settings-exposure-evidence.md`([L] 半 + 归零重启复验记录)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs`(**31 测**,S14-5 由 11 扩);账本互链 `tests/unit/audio_system/README.md`
**实现证据**: `production/qa/evidence/impl-s14-5-settings-exposure-2026-10-16.md`

---

## Dependencies

- Depends on: Story 001 · 002 · 003(E3 七路)· 006(变体表承载字幕文本结构)
- Unlocks: 42 元件轮的壳行镜像可开工;OQ-SS-7 控件形态裁定后归零钮可进架

## Completion Notes

> ⚠️ **本节的「4/4 passing」判定已于 2026-10-16 被 S13-9 补评推翻**
> (`review-audio-epic-fake-green-2026-10-16.md` §2.5:11 测中 🟢 真 1 · 🟡 3 · 🔴 7)。
> 原文**保留不改写**(历史陈述),现行状态见下方 **§S14-5**。

**Completed**: 2026-09-28
**Criteria**: 4/4 passing(注册表 AC① · AC-44-13 mono · AC-44-15 字幕键集 · AC-44-19 归零机制;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(设置 store/归零机制/注册表)尚未实现,本 story 测试用自持谓词面(`FakeSettingsStore`/`FakeResetMechanism`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **mono 出厂默认值用 `Assert.Inconclusive`**:AC-44-13 要求 mono 选项存在且出厂默认有效,但出厂默认值归 ADR-014 烘焙分区(用户调),当前阶段生产代码未实现。测试用 `Assert.Inconclusive` 明确标记待生产实现后验证,不用硬编码常量制造假绿。
3. **归零机制测试验证契约面形状**:3 个 reset 测试验证归零三步语义(写默认 → 推 mixer → 落 sidecar)+ sidecar 持久化契约面形状,生产实现后应替换为调用真实 `IResetMechanism`/`ISettingsStore` 接口。

**评审与修复**:双评审并行(代码质量面 3 BLOCKING + QA 覆盖面 待返回)→ 全修:
- **B1**(代码面):`test_subtitleKeySet_coveredByEventTable` 恒真断言(全局字符串搜索替代 per-cue 验证)→ 改为 `HasSubtitleForCue` per-cue 验证(该 cue 行内 500 字符内是否有 subtitle_text)。
- **B2**(代码面):`test_monoOption_existsWithValidDefault` 恒真断言(断言硬编码常量等于自身)→ 改为 `Assert.Inconclusive`(mono 出厂默认值待生产实现后从烘焙分区读取验证)。
- **B3**(代码面):3 个 reset 测试 Fake 零耦合假绿 → 加注释标注"生产归零机制尚未实现,本测试验证归零契约面形状",生产实现后应替换为调用真实接口。
- **R1**(代码面):`Prefix` 常量声明但从未使用 → 删除。

**残余 NICE(登记不修)**:
- `SettingsVisibleRegistry` 是测试本地副本,可与生产漂移(待生产实现后对齐)。
- `repoRoot` 重复 10+ 次(可抽取为共享 helper)。
- `test_settingsRegistry_unregisteredParam_rejected` 负例无效(检查硬编码数组,应验证生产代码拒收)。
- `test_settingsRegistry_exactly8Entries` 恒真断言(检查本地数组字面量长度)。
- `test_settingsRegistry_monoEnabled_inRegistry` 恒真断言(检查本地硬编码数组)。
- `test_subtitleKeySet_missingSubtitle_rejected` 负例无效(字符串字面量永远不包含子串)。
- `test_a11yDocumentation_threeItemsPresent` 断言过于宽松(应验证具体 AC 条目)。
- AC-44-19 覆盖不完整(只检查 2 条注册表,应遍历全部 8 条;只测跳过 sidecar,应补测跳过写默认/推 mixer)。
- `ExtractCuesWithPrefix` 用 Regex 解析 JSON(应复用 `AudioEventTableGates.ParseTable()`)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs`(**31 测全过**;S14-5 由 11 扩)→ ⚠️ 下行「11 测:10 passed + 1 inconclusive / 1154」是 **2026-09-28 首轮**(假绿轮)的陈述,现行回归见 `## Completion Notes §S14-5`。
**Code Review**: Complete —— 双评审并行 + B1/B2/B3 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-018(音频架构:mono/总线音量义务)· ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)· OQ-SS-5=甲(端本地 sidecar)· OQ-SS-7=甲(归零控件形态 = 42 元件轮)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 9 项已分处登记(本 notes · story Known Risks · 账本)。

---

## Completion Notes §S14-5(2026-10-16 · 生产实现轮)

> **本节的判据 = 变异测试**(停用生产码守卫 ⇒ 断言转红),**不是**「测试名 + 行号」——
> 承 `sprint-14.md` Risks 第 3 条与 retro-13 行动项 2。
> **证据原件**:`production/qa/evidence/impl-s14-5-settings-exposure-2026-10-16.md`。

### 用户裁定四项(2026-10-16,AskUserQuestion)

① **注册表落点 = 独立注册文件** `assets/data/audio_settings_registry.json` + `MixerRegistry` 扩名
   (不动事件表的三层白名单,不耦合尚未落地的 story-010 binder);
② **AC-44-15 [A] 的 48 口述半边 = 如实登记 NOT-RUN + 缺口**(不代 48 立契约 / 不扩前缀 / 不登种子);
③ **`ISettingsStore` / 归零机制全落生产**(接口 + 真 sidecar + 归零三步,落
   `Gameplay.Presentation/Settings/` 中立目录,**禁 `Sim`**);
④ **AC-44-13 出厂默认真源 = 烘焙数据**,`PlayerBusVolumeInitializer.FactoryDefaults` 改读它、
   原 C# 常量降为回退。

### 交付面(7 新 + 3 改)

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(数据) | `assets/data/audio_settings_registry.json` | 8 行闭枚举(`schema_version` / `_note` / `rows[]`) |
| 新(生产) | `Gameplay.Presentation/Settings/SettingsValue.cs` | `SettingsValueKind` 闭集 + readonly struct(**无装箱**) |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsRegistry.cs` | 注册表行 + `Provider` 缝 + `ParamNames` 转发 + `FallbackRows`(非真源) |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsSidecar.cs` | 两文件 sidecar(自写行格式载荷 + 自写头部)· 读序 · 失配改名备份 `.bak-NNN` · FNV-1a 32 |
| 新(生产) | `Gameplay.Presentation/Settings/VolumeUnitMapping.cs` | `LinearToDb` / `DbToLinear`(钳位;禁调用点自行 `Math.Log10`) |
| 新(生产) | `Gameplay.Presentation/Settings/AudioSettingsStore.cs` | 真 store(持值 + 三步归零 + `ResetOutcome`)· `MixerPush` 注入缝 · **原子载入** |
| 新(生产) | `Gameplay.Presentation/Settings/ISettingsStore.cs` | 设置 store 契约(**含归属诚实声明**) |
| 新(构建期) | `Editor.Tools.Bake/AudioSettingsRegistryBinder.cs` | 阶段 2 绑定器:名字对账 / 值对账(双向)/ 域检查(聚合 throw) |
| 新(构建期) | `Editor.Tools.Bake/AudioSettingsRegistryBake.cs` | 仓根装载器(binder 唯一调用点链路;不碰 `UnityEngine`) |
| 改(生产) | `Gameplay.Presentation/Audio/MixerRegistry.cs` | `MonoParameter` + `SettingsVisibleParameters`(8 员;名字半边**单一出处**) |
| 改(生产) | `Gameplay.Presentation/Audio/PlayerBusVolumeInitializer.cs` | `FactoryDefaults` 改读烘焙注册表(经 `VolumeUnitMapping`);原常量降回退 |
| 改(测试) | `Tests/EditMode/Audio/settings_exposure_test.cs` | 11 → **31 测**;四处自造替身**全数删除** |

### 修复的病灶(评审原件 §2.5 逐条)

| 原假绿 | 判定 | S14-5 处置 |
|---|---|---|
| `settings_visible` 注册表生产侧不存在 | 🔴 | 落 JSON + `MixerRegistry.SettingsVisibleParameters` + 构建期 binder |
| 注册表断言宾语 = 测试本地 8 元数组 | 🔴 | 改打 `MixerRegistry` + 烘焙 JSON |
| **AC-44-13「出厂默认 ∈ 烘焙分区」被删空** | 🔴 | 恢复为真断言(两条) |
| 归零三步测打自造 `FakeSettingsStore` / `FakeResetMechanism` | 🔴 | 改打 `AudioSettingsStore` + 真 sidecar |
| `ISettingsStore` 全库无权威定义 | 🔴 | 落生产(文件头含归属诚实声明) |
| 字幕键集测复刻生产判据 | 🟡 | 改打 `AudioEventTableGates.ValidateSubtitles` |
| 字幕键集负例 = 本地字面量(恒真) | 🔴 | 改打仓库夹具 + 生产门 |
| AC-44-15 [A]「含 48 口述」空真 | 🟢→空真 | 缺口哨兵 + NOT-RUN 登记(用户裁定 ②) |
| 单位域无收口(float 0–1 vs dB −80–0) | 🟡 | `VolumeUnitMapping` 单点换算 |

### 证伪轮 —— 12 例全红(停用生产码守卫 ⇒ 断言转红)

| 例 | 停用的守卫 | 红数 |
|---|---|---|
| ① sig | `ComputeRegistrySignature` 恒返固定串 | 1 |
| ② writestep | 归零第三步失败判定 | 1 |
| ③ pushstep | 归零第二步缝缺失判定 | 1 |
| ④ dbmap | `LinearToDb` 换算 → 直通 | 1 |
| ⑤ eqfac | `EqualsFactoryDefaults` 值比较 | 2 |
| ⑥ unknownkey | binder 未知键白名单 | 1 |
| ⑦ name | binder 名字对账「缺员」半边 | 1 |
| ⑧ value | binder 值对账 | 1 |
| ⑨ range | binder 出厂默认区间检查 | 1 |
| ⑩ trunc | store 半截载荷缺键判定 | 1 |
| ⑪ mono | 回退 mono 行类型 `Bool` → `Float` | **5** |
| ⑫ boolrange | binder bool 行禁区间检查 | 1 |

**合计 17 条断言转红**;逐例输出见 `unity/Logs/s14-5-fz.xml` 与证据原件 §3。

### 回归终值

**`3572 / 3526 / 0 / 46`**(`unity/Logs/s14-5-final.xml`)—— 基线 S14-4 `3552 / 3506 / 0 / 46`
⇒ **+20 / +20,零回归**(11 → 31 测)。`SettingsExposure` 过滤套件 = **31 / 31 / 0 / 0**。

### ⚠️ 证伪轮抓到的三处缺陷(如实登记)

1. **生产实现缺陷(第一轮之前由新测抓到)** —— `test_sidecar_truncatedPayload_rejected` 首跑转红:
   `LoadFromSidecar` 原式先 `Clear` 再逐键写入 ⇒ 半截载荷**不回滚**。修:先校验后落值(原子)。
2. **夹具判别力缺陷 ①(第一轮 0 红)** —— 签名测用**字面量**错签名 ⇒ 派生改成常量仍绿。
   修:补 `test_sidecar_signature_isSensitiveToParamSet`。
3. **夹具判别力缺陷 ⑦(第一轮 0 红)** —— 名字对账测被**值对账**报文满足 ⇒ 停用后仍绿。
   修:`AnyError` 两段式重载,断言钉住 `名字对账` + `mono_enabled` 同条报文。

> **登记理由**:本 sprint 内 **第四次**由证伪轮抓到撰写者自己的缺陷 —— 且**第一次连夹具本身**
> 都被证伪轮抓出弱判别面。`story-done` 立此门的原因在此得到第二次实证。

### NOT-RUN 如实登记(禁借绿)

- **AC-44-15 [A]「含 48 口述」半边** —— NOT-RUN(缺口):48 的 4 支教学口述不在事件表、
  前缀不匹配 ⇒ 生产门看不见。缺口哨兵钉住,48 契约落地后它会转红。
- **mono 的运行期载体** —— 缺口:`.mixer` 的 `m_ExposedParameters` 恰 12 员,不含 `mono_enabled`;
  其 mixer 侧映射是**尚未裁定的资产拓扑问题**(已就地登记)。
- **AC-44-15 [L] 字幕听测签署**(WCAG 1.2.2)· **AC-44-12 [L] 人工走查** —— NOT-RUN(项目外部)。
- **归零钮控件形态 / 焦点导航** —— 归 42 元件轮(OQ-SS-7=甲)。
- **运行期装载面**(`Provider` 缝的实际注入)—— 归 story-010(S14-6)。
- **IL2CPP 三格**(ADR-012 矩阵)—— **矩阵整体 NOT-RUN**(CI 常驻 job 未建 + ARM64 腿未跑)。
  ⚠️ **2026-10-17 逐格订正**:原文「本机仅 Mono」**为假** —— 本机装有 IL2CPP 模块,且
  **Linux-x64 的 Mono 与 IL2CPP 两格已于 2026-09-24 实测逐位相同(19/19)**,2026-09-25 补
  Mono player 第三上下文(15/15);真正未跑的只有 **ARM64 腿**(本机无该模块;ADR-012 §二 要求
  原生 ARM64 runner,qemu 否决)与 **Windows-x64 / Apple Silicon**(发版前必跑)。
  证据:`production/qa/evidence/ac-29-il2cpp-crosscheck-2026-09-24.md`。

### 登记项

- **story-011 转 `Complete`** —— 归 **S14-7**(6 条复评批,`sprint-14.md` DoD)。
- **`AudioSettingsRegistry.Provider` 的 Addressables 注入** —— 归 S14-6。
- **`FallbackRows` 的最终退役** —— Provider 常驻后退居二线;退役时机归装配轮。
