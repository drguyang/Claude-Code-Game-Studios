# Story 021: `knowsClinic` 注入缝(装配层不再发明行为输入)

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(装配面;把 13 的行为输入从硬编码改成注入)
> **Type**: Integration
> **Estimate**: 0.5 人日
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-18
> **Completed**: 2026-10-18

## Context

**GDD**: `design/gdd/patient-ai.md` —— F-13.2(行为决策的输入项)· AC-13-D2/D3(行为态与终局)
**Requirement**: 13 的 `Seeking` 行为可达性(经 44 侧的驱动链观测)
**缺口来源**: `TD-audio-033`(`docs/tech-debt-register.md` §「2026-10-18 · story-018 补评批」)
**ADR Governing Implementation**: ADR-016 §一(重建三源不变量 —— 行为输入必须可从三源重构)·
ADR-018 §一(44 只触发)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 注入缝;零引擎 API。

### 判据事实(实测,本件的宾语)

| 实测 | 出处 |
|---|---|
| `PatientCueDriver.cs:125` 逐字 `_behavior.Step(id, knowsClinic: false)` —— **装配层写死** | `TD-audio-033` ① |
| F-13.2 的真值源(病人是否知道医馆 = story-002 的 `HomeRegion` 语义)**未作注入缝** ⇒ `BehaviorMap.cs:80/:83` 两个分支皆需真值 ⇒ **全部病人永不进 `Seeking`** | `TD-audio-033` ① |
| 装配层与视觉侧**各持一份行为输入**(`CompositionRoot` 私有 `PatientBehaviorDirector` vs `PatientCueDriver._behavior`)⇒ 同一病人**两份行为态可分叉** | `TD-audio-033` ② |

### 边界裁定

1. **只加注入缝**,不在装配层发明「知不知道医馆」的规则 —— 真值源归 13 的 `HomeRegion` 语义;
   缝的形式 = 构造参数(`Func<PatientId, bool> knowsClinic`),**null ⇒ 显式 fail-loud 或明确默认二选一,
   须在实现轮登记所选**(禁静默 `false`)。
2. **两份行为态可分叉**的处置:本件**只登记**这份分叉(债项 ②),合并归视觉侧/装配层收敛轮,
   **不在本件硬改**(否则碰到 13 的视觉链路,超出 44 epic 面)。
3. 不动 `BehaviorMap` 的判定规则、不动 `TerminalFlag` 口径。

## Tasks

- [x] **AC-021-1**[A]: `PatientCueDriver` 的 `knowsClinic` 改为**注入**(构造参数),
  生产装配由 `CompositionRoot` 从真值源喂入;**生产树(`Tests/` 之外)不得再出现 `knowsClinic: false` 字面量**
  ⚠️ **2026-10-18 刷账(S15-7)口径收窄**:原文写「**全仓**」不成立 —— 测试树仍有一处
  `Tests/EditMode/PatientAI/behavior_map_test.cs:61`(`Map(0.60f, BehaviorState.Idle, knowsClinic: false)`),
  它是「不知道医馆」分支的**夹具必需**,不是漏网;源码门(`knowsclinic_seam_test.cs:245`,扫描 `Tests/` 之外的
  生产树、scan >300 件、先剥注释)的口径本就是**生产树**。判定不变,只是把范围说准。
  **证伪判据**:把注入改回 `false` ⇒ 本条红(判据 = 反射/源码谓词须是**生产件可测**的,
  不得只 grep 测试文件 —— 承假绿复核病根)。
- [x] **AC-021-2**[A]: `knowsClinic = true` 时病人**可进 `Seeking`** —— 断言宾语 = 真
  `PatientBehaviorDirector` + 真 `BehaviorMap`,经 `PatientCueDriver` 的 seam 观测。
  **证伪判据**:把 `BehaviorMap` 的 `Seeking` 分支短路 ⇒ 本条红。
- [x] **AC-021-3**[A]: `knowsClinic = false` 时**不进 `Seeking`**(反向对照 —— 防「一律 true」的假修)。
  **证伪判据**:把注入恒为 `true` ⇒ 本条红。
- [x] **AC-021-4**(账面): `TD-audio-033` 的 ① 脚结案、② 明确留 open(两份行为态分叉);
  债行刷新不得只改声明。

## Dependencies

- `PatientCueDriver` / `PatientBehaviorDirector` / `BehaviorMap`(均已在盘)
- **与 story-020 同住 `PatientCueDriver` 邻域,但无代码冲突面**(020 改 `AudioClipLibrary`,
  本卡改构造参数);两者可并行,⚠️ 若同轮触碰 `CompositionRoot` 须串行

## Out of Scope

- 两份行为态**合并**(`TD-audio-033` ②)—— 归视觉侧/装配层收敛轮;
- 变体选择(story-020);
- AC-13 的 `[L]` 走查(归 13 epic 的走查面)。

## Test Evidence

- 主件:`unity/Assets/Tests/EditMode/Audio/` 或 `unity/Assets/Tests/PlayMode/` 真装配件
  (打 story-018 已通的缝);
- 证伪轮:逐条「停用什么 ⇒ 哪条红」登记在 `impl-s21-*.md`。

## Definition of Done(本卡)

- AC-021-1…3 全 [A] 绿 + 证伪转红记录齐;
- `TD-audio-033` ① 结案 / ② 留 open 的注落账;
- ⚠️ **44 epic 仍不得收口**。

## Completion Notes

**Completed**: 2026-10-18
**Criteria**: 4/4(AC-021-1…3 全 [A] 绿 + 证伪齐;AC-021-4 账面已落)
**所选口径(边界裁定 1 的二选一)**: **显式 fail-loud** —— `knowsClinic` 必填,传 null ⇒ 具名
`ArgumentNullException`。**不提供**「静默默认 false」:那正是本条债项的形态本身。
**Test Evidence**: `unity/Assets/Tests/EditMode/Audio/knowsclinic_seam_test.cs`(8 测)
**Falsifiability**: F1–F6 全转红;两处失败尝试(F1/F3 首轮不红 = 主测未驱动装配件;F5 首版与 F1 无观测差)已如实登记
**Regression**: 终轮 `3765 / 3720 / 0 / 45`(首轮 `3765/3719/1/45`,唯一红为 `MovementFeedTest` 已知间歇红,终轮未复现)vs 基线 `3757 / 3712 / 0 / 45` ⇒ total `+8` = 新增 8 条;
`passed +7 + 1 红 = +8` 严丝合缝。唯一红 = `MovementFeedTest` 已知间歇红(`TD-player-01`,单跑 5/5 绿)
**原件**: `production/qa/evidence/impl-s21-knowsclinic-seam-2026-10-18.md`
**Code Review**: 已跑 —— BLOCKING 0 · WARNING 8 · INFO 4;修 2(W-1/W-2,各有反证 F7/F8),余 6 登记。原件 `production/qa/evidence/review-s21-knowsclinic-seam-2026-10-18.md`

### 实现轮登记的三处实况(不得读成「已修」)

1. **`TD-audio-033` ② 仍 open** —— 两份行为态分叉未合并(story Out of Scope);
   服务袋暴露 `BehaviorDirector` 只是**可观测性**,不是合并。
2. **生产 `KnowsClinic` 今日恒假** —— 生态区 / POI 定义 / `StructurePlaced` 三个装载面均未接
   ⇒ 组合根自建空表。行为与结案前**等价**,差别只在**形状**(真值源到位后只换注入缝)。
   `test_ac021_3_emptyRegistry_makesTruthSourceHonestlyFalse` 钉住这一实况,**接上后须改期望**。
   ⚠️ **不得**把本卡读成「`Seeking` 现在可达了」—— 可观测行为与旧硬编码 `false` **逐位相同**,变的只是传值路径。
3. **锚点取的是在场登记簿的逻辑格**,不是 `spawn_anchor(p)`(病人侧该字段仍未登记,`O-13-5`)。

⚠️ **44 epic 仍不得收口**。

### 评审轮修正(2026-10-18)

- **W-1**:`test_ac021_3_emptyRegistry_…` 的宾语从**夹具自造**的 `ClinicKnowledge` 改成组合根产物,反证 = F8。
- **W-2**:源码门从 2 棵扩到 `Tests/` 之外**全部**生产树(`scanned > 300`),反证 = F7(Sim 树塞违例 ⇒ 红)。
- **W-3** `StripComments` 已落**未证**(无「字符串含 `//`」/「块注释含违例」的夹具)—— 如实登记。
- **债行复核**:`TD-audio-033 ②` 是前瞻性债项(视觉侧尚未接线),已写回债行。

### 收口核验(/story-done · 2026-10-18)

| AC | 载体 | 状态 |
|---|---|---|
| AC-021-1 | `test_ac021_1_driver_ctor_requiresKnowsClinic_neverSilentlyDefaults` · `test_ac021_1_compositionRoot_feedsSeamFromTruthSource_notHardcodedFalse` · `test_ac021_1_productionTree_hasNoHardcodedKnowsClinicFalse` | COVERED(3/3 绿;反证 F1/F3/F4/F5/F7) |
| AC-021-2 | `test_ac021_2_knowsClinicTrue_patientEntersSeeking` | COVERED(反证 F2) |
| AC-021-3 | `test_ac021_3_knowsClinicFalse_patientDoesNotEnterSeeking` · `_emptyRegistry_…` · `_noAnchorCell_…` · `_directory_caches…` | COVERED(反证 F3/F6/F8) |
| AC-021-4 | `docs/tech-debt-register.md` `TD-audio-033` ① 结案 / ② 留 open(并标注为**前瞻性**债项) | COVERED(账面) |

**Falsifiability Gate**:命中形态 = 定义域收窄(源码门 + 契约测)+ 一次性/幂等(缓存)。反证 F1–F8 全部转红;
两处失败尝试(首轮 F1/F3 未驱动装配件、F5 首版与 F1 无观测差、F7 前两版锚点选错)**已如实登记**。→ **PASS**
**Manifest 陈旧检查**:story 2026-10-15 = 现行 manifest 2026-10-15,无陈旧。
**范围检查**:story 列出的 Out of Scope 三项均未触碰(`BehaviorMap` 零改动;行为态未合并;AC-13 `[L]` 走查未做)。
**未证项**:`StripComments` 无夹具(W-3,已落未证);生产 `KnowsClinic` 今日恒假;锚点用在场逻辑格而非 `spawn_anchor(p)`;IL2CPP 三格 NOT-RUN。
**⚠️ 44 epic 仍不得收口**(本卡 DoD 第 3 条)。
