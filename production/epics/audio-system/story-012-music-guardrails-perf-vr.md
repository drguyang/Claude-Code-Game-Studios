# Story 012: 乐层护栏与性能/VR 切面

> **Epic**: 音频系统
> **Status**: Complete ✅ 2026-10-16(S14-7 复评结案 —— 假绿撤回 → S14-4 生产实现 + 证伪轮全红 → 本节复评转正)
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

> **⚠️ 2026-10-16 假绿复核 —— Status 由 `Complete` 撤回为 `In Review`**
> sprint-13 S13-9 补评判定:本 story 的 AC 语义层**未被验证** —— `xfade_ms` 下界断言
> (唯一**具名空位** `AudioEventTableGates.ValidateXfade`)/ DSP 预算探针 / 测量点注册表**不存在**,
> 测试以两行自写谓词 + 双端硬编码顶替 ⇒ 断言在「被测对象不存在」时仍为绿(**BLOCKING 级**)。
> 实测:12 测中 🟢 真 1 · 🟡 半真 6 · 🔴 假 5。
> ⚠️ **修复轮 R2 先改 GDD 再让测试断言 GDD 含该词** —— 断言对象先造出来再断言。
> **评审原件**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`(§2.6 逐条表)。
> 实现另立 sprint;本 story 维持 `In Review` 直至生产类落地并经证伪轮复评。
>
> **✅ 2026-10-16 S14-4 实现轮落盘 —— 生产件已落地 + 证伪轮 11/11 全红 + 真种子端到端红**
> (本 story 仍 `In Review`:转 `Complete` 归 **S14-7** 的 6 条复评批,承 `sprint-14.md` DoD)。
> 落地内容见下方 `## Completion Notes §S14-4` 与
> `production/qa/evidence/impl-s14-4-music-guardrails-2026-10-16.md`。

## Context

**GDD**: `design/gdd/audio-system.md`(规则二第 5 类 · §Event Table Schema · AC-44-18/E1/E2/11)
**Requirement**: TR-audio-014(战斗乐层 G1–G3:层间切换经混音参数,禁 sting/层素材报状态)

**ADR Governing Implementation**: ADR-018 §六 音乐三闸(主:G1 禁帧对齐/`MUSIC_XFADE_MIN_MS` · G2 去标注盲测 · G3 本地触发)+ ADR-018 §七(P1b)+ ADR-020 §六(镜头效果同铁律,引用)
**ADR Decision Summary**: 27 永不点歌;乐层缓变可,但切换须整体落在淡变区间;无护栏则第 5 类例外不成立。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(VR 音频 P1b;DSP 预算工具面须定探针)
**Engine Notes**: `MUSIC_XFADE_MIN_MS` owner = 44(25 侧同名引用,2026-09-25 二轮记账);VR 挂点 = ADR-020 §七,VR 音频实现 P1b 不在本 story。

**Control Manifest Rules (this layer)**:
- Required: `xfade_ms ≥ MUSIC_XFADE_MIN_MS` 构建断言(G1 载体)· 乐层触发源枚举 = `EncounterMusicLayer` 配对(规则二第 5 类)
- Forbidden: 乐层切换无淡变(硬切 = G1 违例)· 27 直接点歌 · 用乐层报状态(黑名单四禁对第 5 类原文适用)
- Guardrail: DSP 预算探针不得反向影响 sim 帧预算路径(音频不在急救 <50ms 路径内)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **AC-44-18**(2026-09-25 二轮新增):战斗乐层行 —— ① `xfade_ms ≥ MUSIC_XFADE_MIN_MS`(构建期数值比较,超界 = 失败);② `trigger_source = EncounterMusicLayer` 且 G1–G3 护栏登记在册;③ `MUSIC_XFADE_MIN_MS` owner = 44 本表(Tuning 已列),25 侧同名引用交叉注。— **S14-4 落**:① `AudioEventTableGates.ValidateXfade`(**构建期真门**,原唯一具名空位)+ `AudioTuning.MusicXfadeMinMs = 2000`(单一出处,门不另抄);② G1–G3 锚定**表格行首列**(`test_guardrailAnchor_rejectsBareMention` 自证);③ `test_musicXfadeMinMs_ownerIs44_withCrossFileReference`(44 GDD owner 声明 + 25 侧同名引用)。证伪 ①②(下界判据停用 / 退化 ⇒ 2 红)· ③(去 `music &&` 定义域收窄 ⇒ 1 红)· ④(诊断文本真常量 → 硬编码 `0` ⇒ 1 红)· ⑪(锚定退化裸 `Contains` ⇒ 1 红)。
- [x] **AC-44-E2**:60s 采样窗 `Audio.Process`/DSP 回调 **P95 ≤ `AUDIO_BUDGET_MS`**(登记旋钮;16.6/11.1ms 为参考上界);且 `AC-10-08` 测量点注册表**不含 `IAudioCueSink.Emit`**(静态核对)。— **S14-4 落**:`AudioBudgetProbe`(`Percentile` 最近秩法,与 `InputLatencyProbe` 同口径 · `IsP95WithinBudget`,**负样本哨兵不得冒充通过**)+ `AudioTuning.AudioBudgetMs = 2.5`;测量点 = `AudioMeasurementPoints`(44 侧生产闭集 `Ac1008Points` + `EmitSymbol` `nameof` 编译期绑定 + **反空转守卫**)。证伪 ⑤(`IsP95WithinBudget` → `true` ⇒ 2 红)· ⑥(`idx` 恒 0 ⇒ 1 红)· ⑦(空集守卫停用 ⇒ 1 红)· ⑧(null 守卫停用 ⇒ 1 红)· ⑨(`EmitSymbol` 比对停用 ⇒ 1 红)。
- [x] **AC-44-E1**:P0 构建无 VR 音频(装配/场景扫描:VR 相关音频路径零启用);P1b 交付切面(挂点/快照/延迟)登记在册(非「已实现」)。— **S14-4 落**:扫描面扩三面(方法 / **字段** / **属性**)+ 装配引用集 + **负向夹具自证**;`test_vrAudio_scanSurface_catchesVrMembers`。证伪 ⑩(扫描面去掉**字段**分支 ⇒ 1 红)· ⑩b。
- [ ] **AC-44-11**(P1b,BLOCKED):VR 听测无晕动副作用(受试者报告 + 帧率实测)+ 头显单 `AudioListener` —— **随 VR 模式 P1b 执行,本 story 只登记不跑**(禁借绿)。— **NOT-RUN**(随 VR P1b);本件只登记,**不勾选**。

---

## Implementation Notes

*Derived from ADR-018 §六/§七 + GDD(2026-09-25 二轮 AC-44-18 载体补)*:

- `xfade_ms` = Story 002 schema 字段(本 story 挂数值断言与 `MUSIC_XFADE_MIN_MS` 常量)。
- G1 的「禁帧对齐」运行面 = 淡变覆盖切换瞬间(断言:乐层参数变化斜率连续,无阶跃);G2 = 25 侧 `[V]`(`AC-25-V-03`)承载盲测,本 story 不重造,登记交叉引用。
- **分桶护栏同族纪律**(规则二注):乐层/分桶边界不得与 sim 阈值耦合 —— 与 Story 006 的解耦断言共用静态对照。
- E2 探针:Unity Profiler `Audio.Process` 采样器(60s 窗,P95);`AUDIO_BUDGET_MS` 入 Tuning(用户调,默认建议 = 帧预算 15% 类,值归用户)。
- E1 扫描面 = 场景/预制/装配中 VR 音频符号零启用(`VRComfort` 快照存在但 P1b 不绑 —— States 表已标 P1b)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: 分桶的 sim 阈值解耦断言(共用对照,实现归 006)
- 25 战斗:G2 盲测执行(`AC-25-V-03`)与遭遇态观察者实现
- VR 音频实现(P1b,ADR-018 §七)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-18**: 乐层护栏。Given: 战斗乐层行夹具。When: 门。Then: `xfade_ms=1999`(假设下限2000)⇒ 红;`= 2000` ⇒ 绿;`trigger_source` 错配 ⇒ 红;`MUSIC_XFADE_MIN_MS` 在 44 Tuning 表且被 25 引用注指向。
- **AC-44-E2**: 探针。Given: 60s 采样窗音频负载场景。When: 读 P95。Then: ≤ `AUDIO_BUDGET_MS`;测量点注册表静态核对无 `IAudioCueSink.Emit`;负向:注册表误加 ⇒ 红。
- **AC-44-E1**: VR 切面。Given: P0 构建。When: 扫 VR 音频启用符号。Then: 零启用;P1b 切面登记文本在册。
- **AC-44-11**(登记): 状态 = BLOCKED-BY-P1b;本 story 不产证据,只保 AC 存在与挂点正确(禁勾)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/audio_system/music_guardrails_test.cs` — must exist and pass(E2 的 P95 断言随性能测试基建,可落 evidence 文档)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/music_guardrails_test.cs`(**22 测**;S14-4 由 12 扩);账本互链 `tests/unit/audio_system/README.md`

---

## Dependencies

- Depends on: Story 002(xfade_ms 载体)· 003(乐层总线在 mixer 中)
- Unlocks: 25 侧 G2 盲测可排期(载体就绪)

## Completion Notes

> ⚠️ **本节的判定已于 2026-10-16 被 S13-9 补评推翻**
> (`review-audio-epic-fake-green-2026-10-16.md` §2.2/§2.3/§2.6:12 测中 🟢 真 1 · 🟡 半真 6 · 🔴 假 5(该数字逐字取自本 story 头部横幅))。
> 原文**保留不改写**(历史陈述),现行状态见下方 **§S14-4**。
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-44-18 乐层护栏 · AC-44-E2 DSP 预算 · AC-44-E1 VR 切面 · AC-44-11 VR 听测;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(乐层护栏/DSP 预算探针/VR 切面)尚未实现,本 story 测试用自持谓词面(`MusicXfadeGuard`/`DspBudgetGuard`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **GDD 补登记两处**:QA 评审发现 GDD 缺两处登记 — ① AC-44-11 行补 `状态 = BLOCKED-BY-P1b` 标记(与 story 文件口径一致);② §Tuning Knobs 补 `AUDIO_BUDGET_MS` 行(AC-44-E2 登记旋钮)。
3. **G1–G3 护栏测试补缺**:代码质量评审 R3 发现 AC-44-18 ② "G1–G3 护栏登记在册" 无测试覆盖。补 `test_musicLayer_g1g3Guardrails_registered` 验证 ADR-018 §六含 G1/G2/G3 三条护栏文本。

**评审与修复**:双评审并行(代码质量面 0 BLOCKING + 4 REC;QA 覆盖面 1 BLOCKING + 4 REC)→ 全修:
- **B1**(QA 面):`test_vrListen_ac4411_blockedByP1b` 形同虚设(断言 `Contains("P1b")` 无区分度)→ GDD AC-44-11 行补 `BLOCKED-BY-P1b` 标记,测试改查 `Contains("BLOCKED-BY-P1b")`。
- **R1**(代码面):`test_vrListen_ac4411_blockedByP1b` 断言与意图不符 → 改查 `Contains("BLOCKED-BY-P1b")`。
- **R1**(QA 面):`test_vrAudio_p1bFacet_registered` 判据过弱(`Contains("P1b")` 无区分度)→ 改查 `Contains("交付切面")`。
- **R2**(QA 面):AC-44-E2 `AUDIO_BUDGET_MS` 未入 GDD §Tuning Knobs → 先补 GDD 行,再补测试断言 `text.Contains("AUDIO_BUDGET_MS")`。
- **R2**(代码面):`GetTriggerSourceForCue` 正则解析脆弱(500 字符窗口)→ 改为搜索到文件尾。
- **R3**(代码面):AC-44-18 ② "G1–G3 护栏登记在册" 未测 → 补 `test_musicLayer_g1g3Guardrails_registered`。
- **R4**(代码面):AC-44-E2 "注册表误加 ⇒ 红" 负向未测 → 补 TODO 注释(待探针注册表实现后补负向)。

**残余 NICE(登记不修)**:
- `MusicXfadeGuard` / `DspBudgetGuard` 测试自持谓词面与生产代码的同步风险(生产实现后应替换为调用生产谓词)。
- 三个 xfade 测试用硬编码 `musicXfadeMinMs = 2000`(假设值,待 GDD 定稿后同步)。
- `ExtractCuesWithPrefix` 正则未处理转义引号(当前 cue 名是简单标识符,无此问题)。
- dspBudget 边界值 p95 == budget(≤ 端点)未测(xfade 侧有 atMin,dsp 侧没有)。
- AC-44-11「头显单 AudioListener」登记可加 doc 断言(因该 AC 整体 BLOCKED,优先级低)。
- AC-44-18③ 的 25 侧引用未跨文件断言。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/music_guardrails_test.cs`(**22 测全过**;S14-4 由 12 扩)—— ⚠️ 上行「12 测全过 / 1166 passed」是 **2026-09-28 首轮**(假绿轮)的陈述,现行回归见 `## Completion Notes §S14-4`。
**Code Review**: Complete —— 双评审并行 + B1/R1/R2/R3/R4 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-018 §六 音乐三闸(G1 禁帧对齐 / G2 去标注盲测 / G3 本地触发)· ADR-018 §七(P1b)· ADR-020 §六(镜头效果同铁律)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 7 项已分处登记(本 notes · story Known Risks · 账本)。

---

## Completion Notes §S14-4(2026-10-16 · 生产实现轮)

> **本节的判据 = 变异测试**(停用生产码守卫 ⇒ 断言转红),**不是**「测试名 + 行号」——
> 承 `sprint-14.md` Risks 第 3 条与 retro-13 行动项 2。

### 用户裁定三项(2026-10-16,AskUserQuestion)

① **`MUSIC_XFADE_MIN_MS` 种子值 = 2000 ms** —— GDD :778 只写「≥ 数秒级(待调)」,真值归数值轮;
   种子落 `AudioTuning`,留 2× 余量给种子乐层行(`xfade_ms = 4000`)⇒ 门不误红。
② **AC-44-E2 后半「测量点注册表」= 44 侧生产闭集 + 反空转守卫** —— 全库零落点、归属未定;
   裁定取「44 侧声明 + 反空转」,并在文件头登记**归属诚实声明**(10 侧真注册表落地时本类改读它)。
③ **三条 🟡 半真本轮一并强化** —— E1 扫描面 / G1–G3 锚点 / AC-44-18③ 跨文件引用。

### 交付面(2 新 + 3 改)

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioMeasurementPoints.cs` | `Ac1008Points` 闭集 · `EmitSymbol`(`nameof` 绑定)· `Validate`(null / 空集 / 含 Emit 三态) |
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioBudgetProbe.cs` | `Percentile`(最近秩法)· `IsP95WithinBudget`(负样本哨兵不得冒充通过) |
| 改(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | `MusicXfadeMinMs = 2000` · `AudioBudgetMs = 2.5` |
| 改(**构建期真门**) | `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs` | `ValidateXfade` 补下界比较(兑现原 XML doc 逐字写的「**不做**」空位) |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/music_guardrails_test.cs` | 12 → **22 测**;两个自写谓词类全删;三条半真强化;两条**判据自证测** |

### 修复的病灶(评审原件 §2.6 逐条)

| 原假绿 | 判定 | S14-4 处置 |
|---|---|---|
| xfade 三条测打自写 `MusicXfadeGuard` + 硬编码 2000/1999 | 🔴 ×3 | 改打**生产构建期门** `AudioEventTableGates.ValidateXfade`;`aboveMin` 读**真种子值** |
| dspBudget 两条测打自写 `DspBudgetGuard` + 双端硬编码 | 🔴 ×2 | 改打生产 `AudioBudgetProbe`;预算读 `AudioTuning.AudioBudgetMs` |
| 测量点注册表断言的宾语 = GDD 散文(注册表零落点) | 🟡 | 落 `AudioMeasurementPoints` + 反空转守卫 |
| E1 的 P0-VR 扫描面只有公开方法签名 | 🟡 | 扩三面 + 装配引用集 + **负向夹具自证** |
| G1–G3 用裸 `Contains("G1")`(无锚点) | 🟡 | 锚定**表格行首列** + **锚定判据自证测** |
| AC-44-18③ 的 25 侧引用未跨文件断言(原 NICE) | 🟡 | 补 `ownerIs44_withCrossFileReference` |

### 证伪轮 —— 11 例全红(停用生产码守卫 ⇒ 断言转红)

| 例 | 停用的守卫 | 转红断言 | 红数 |
|---|---|---|---|
| ① lowerbound | 门内下界比较 → `false` | `belowMin_rejected` · `belowMin_diagnosticCarriesTuningValue` | 2 |
| ② domain | 下界改 `xfade < 0` | 同 ① 两条 | 2 |
| ③ musiconly | 去掉 `music &&` 定义域收窄 | `nonMusicRow_belowMin_notRejected` | 1 |
| ④ diagvalue | 诊断真常量值 → 硬编码 `0` | `belowMin_diagnosticCarriesTuningValue` | 1 |
| ⑤ budget | `IsP95WithinBudget` → `true` | `p95Exceeds_rejected` · `noSamples_notWithinBudget` | 2 |
| ⑥ percentile | 最近秩公式 → 恒取首元素 | `percentile_nearestRank` | 1 |
| ⑦ antivacuous | 空集守卫 → `false` | `emptySet_isRed` | 1 |
| ⑧ nullguard | null 守卫 → `false` | `null_isRed` | 1 |
| ⑨ emitdetect | Emit 检出 → `false` | `emitInjected_isRed` | 1 |
| ⑩ vrfield | 扫描面去掉字段分支 | `vrAudio_scanSurface_catchesVrMembers` | 1 |
| ⑪ anchor | 锚定退化裸 `Contains` | `guardrailAnchor_rejectsBareMention` | 1 |

**真种子端到端**(计划判据 ③):改 `assets/data/audio_events.json` 的 `xfade_ms` → **1999**
⇒ `test_realSeedAudioEvents_zeroErrors` + `test_musicXfade_aboveMin_accepted` **两条转红**,
还原后逐字节校验通过。

### ⚠️ 证伪轮暴露的两处缺陷(如实登记,已修)

**(a) 例 ⑩ 首轮 0 红 = 弱判别面** —— E1 扩展的三个面在当前 44 代码下零命中 ⇒ 删掉不红。
**修**:抽 `ScanVrViolations` + 负向夹具 `VrLeakFixture` 自证测 ⇒ ⑩b 转红 1 条。

**(b) 🔴 判据自证测当场抓到我自己写的锚定缺陷** —— `test_guardrailAnchor_rejectsBareMention`
首版**转红**(`| 说明 | **G1…** |` 中间列同样以 `| **` 开头 ⇒ 误过)。
**修**:锚点改**真行首**(`StartsWith` 或 `"\n" + anchor`)⇒ 22 测全绿,⑪ 转红 1 条。
**这是本 sprint 内第二次由证伪轮抓到撰写者自己的缺陷**(S14-3 为假绿夹具)。

### 回归终值

**`3552 / 3506 / 0 / 46`**(`unity/Logs/s14-4-final.xml`,2026-10-16)—— 基线 S14-3 =
`3542 / 3496 / 0 / 46` ⇒ **+10 / +10 / 0 / 46 不变,零回归**(`music_guardrails_test` 12 → 22 测)。

### 门保持

b5(2 新文件全在 44 命名空间)· b3(新增公开面仅 BCL 类型)· b4(`AudioBudgetProbe` 零字段)·
ADR-029 §③(本件不涉)· **新增**:`ValidateXfade` 下界比较进 `Validate` 聚合链 ⇒ 构建期硬失败。

### NOT-RUN 如实登记(禁借绿)

- **AC-44-E2 实测半边**(60 s 窗 P95 实测值)—— **NOT-RUN**:须真 GPU + 真音频回调
  (EditMode 无 DSP 图)。本件只交判据 + 分位算法。
- **AC-44-E1 的 P1b 切面实测** —— NOT-RUN(P1b)· **AC-44-11 VR 听测** —— BLOCKED-BY-P1b(禁勾)。
- **IL2CPP 三格**(ADR-012)—— NOT-RUN:本机仅 Mono。

### 登记项

- **story-012 转 `Complete`** —— 归 **S14-7**(6 条复评批)。
- **`AudioMeasurementPoints` 归属收口** —— 文件头已登记:10 侧真注册表落地时本类**改读它**。
