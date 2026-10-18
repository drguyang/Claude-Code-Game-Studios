# Story 021: `knowsClinic` 注入缝(装配层不再发明行为输入)

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(装配面;把 13 的行为输入从硬编码改成注入)
> **Type**: Integration
> **Estimate**: 0.5 人日
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-18

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

- [ ] **AC-021-1**[A]: `PatientCueDriver` 的 `knowsClinic` 改为**注入**(构造参数),
  生产装配由 `CompositionRoot` 从真值源喂入;**全仓不得再出现 `knowsClinic: false` 字面量**。
  **证伪判据**:把注入改回 `false` ⇒ 本条红(判据 = 反射/源码谓词须是**生产件可测**的,
  不得只 grep 测试文件 —— 承假绿复核病根)。
- [ ] **AC-021-2**[A]: `knowsClinic = true` 时病人**可进 `Seeking`** —— 断言宾语 = 真
  `PatientBehaviorDirector` + 真 `BehaviorMap`,经 `PatientCueDriver` 的 seam 观测。
  **证伪判据**:把 `BehaviorMap` 的 `Seeking` 分支短路 ⇒ 本条红。
- [ ] **AC-021-3**[A]: `knowsClinic = false` 时**不进 `Seeking`**(反向对照 —— 防「一律 true」的假修)。
  **证伪判据**:把注入恒为 `true` ⇒ 本条红。
- [ ] **AC-021-4**(账面): `TD-audio-033` 的 ① 脚结案、② 明确留 open(两份行为态分叉);
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
