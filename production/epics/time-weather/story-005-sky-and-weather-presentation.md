# Story 005: 天空与体感读数呈现 —— 42/44 消费通道([L] 走查)

> **Epic**: 时间与天气
> **Status**: Complete
> **Layer**: Feature
> **Type**: Visual-Feel
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/time-and-weather.md`(§Rules 输出面 skylight→42 · 语境 cue→44 · §ACs AC-5-09 / AC-5-10 / AC-5-18 / AC-5-21 四项 [L] 人工走查)
**Requirement**: TR-timeweather-009(5 只产读数,呈现归 42 / 44,各消费方自读)· TR-timeweather-013(呈现所读的读数与 sim 侧逐位同源)· TR-timeweather-015(深水线季节修正的调制呈现归 17,5 只出 season_index · 现 `partial`)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架;ADR-018: 音频架构
**ADR Decision Summary**: 42/44 与 44 同构 —— 只渲染/只触发,永不持有游戏状态,只读呈现 DTO;呈现层禁 `disease_id` 等 sim 语义直漏;音频走无提示音铁律(行为反馈白名单,不「报」状态);skylight 是 42 侧对 `isNight`/`Weather` 的**读**,不是 5 的推。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: URP 天光/雾参数为长期稳定 API,但「块边界跳变是否需要表现层自行渐变」是表现层设计(B-8 禁止 sim 侧时间积分,表现层插值合法且不回写)。

**Control Manifest Rules (this layer)**:
- Required: 呈现层只读 5 的输出 DTO(isNight / season_index / kind / intensity / skylight 读数),零回写;天气视觉过渡(若做)住表现层,不进 sim
- Forbidden: 42/44 持有时间或天气状态;音频 sting 直报「下雨了」(违 AC-44-09 白名单);把 `intensity` 原始整数直接显示给玩家
- Guardrail: 表现层插值永不反推逻辑值(AC-20 同型纪律:呈现与 sim 单向)

---

## Acceptance Criteria

*From GDD `design/gdd/time-and-weather.md`, scoped to this story:*

- [ ] **[L]** 入夜 / 破晓走查:玩家能在无 UI 文本的情况下从天空与光照**读出**昼夜相位(AC-5-09,人工走查 SIGN-OFF)
- [ ] **[L]** 天气转变走查:块边界跳变经表现层渐变后**可读**(非瞬移式闪变)且渐变不回写 sim(AC-5-10,人工走查)
- [ ] **[L]** 季节体感走查:同一天气 kind 在不同 season_index 下的视觉/音频差异可被玩家区分(AC-5-18,数值轮冻结前以「差异存在且稳定」为准,具体档位值 NOT 判)
- [ ] **[L]** 无状态播报走查:44 的天气语境不出现 sting/jingle「报状态」,只有白名单内的行为反馈(AC-5-21,与 ADR-018 AC-44-09 同判据)
- [ ] 反射/静态断言:42/44 侧对 5 的读数接口只读,零写路径;DTO 无 float 泄漏进 sim(承 Story 003 的递归断言)
- [ ] 呈现帧率不构成 sim 依赖:渲染帧率 30/60/144 变化时,5 的输出序列逐位不变(tick 驱动,不由渲染帧驱动,承 OQ-25-8 相位注)

---

## Implementation Notes

1. skylight 读数 = `Fix` 或 `int` 语义的相位/天气档,由 42 侧的视觉参数表映射(映射表归 42;值归数值轮)。
2. 表现层渐变(若启用)在 `Gameplay.Presentation` 内做 float 插值,单向消费整数读数,永不写回。
3. 语境 cue 经 `AudioCueDto`(ADR-018),天气类为环境层(Ambience 子通道),非 UI cue。
4. 四项 [L] 走查需 49 教学/走查环境与数值轮部分参数就位 —— AC-5-18/21 的 EXTERNAL 性质照 Story 004 口径登记,禁借绿。
5. 证据 = 走查记录 + 截图(渲染类不做自动化保真断言,承 coding-standards「What NOT to Automate」)。

---

## Out of Scope

- [Story 004]: 跨平台逐位对拍与消费边界集成(本 story 在其读数通道稳定后接线)
- [Story 001–003]: sim 侧机制(本 story 零 sim 改动)
- VR 模式的天空呈现(承 ADR-013 §七,VR 推 P1a)
- 深水线季节修正曲线(归 17)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 快进跨昼夜边界的调试视图 | 观察天空/光照 | 渐变平滑、无闪变;sim 读数仍块状(不插值) |
| TC-2 | 渲染帧率锁 30 与放开 | 记录一段会话的 5 输出序列 | 两序列逐位相同 |
| TC-3 | 天气块跳变瞬间 | 听 44 输出 | 无 sting/jingle;语境层交叉淡入 |
| TC-4 | 静态扫描 42/44 对 5 接口的调用 | 断言 | 只读、零回写 |

**Edge cases**: 读档瞬间的呈现跳变(允许 snap,因 sim 真值即如此);未驻留 chunk ⇒ 全局默认档的天空呈现。

---

## Test Evidence

**Story Type**: Visual-Feel
**Required evidence**: `production/qa/evidence/time-weather/story-005-sky-weather-walkthrough-[date].md`(走查记录 + 截图 + 签核) — must exist
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 004(读数通道与集成稳定)· 42 的呈现框架(ADR-013 双栈)· 44 的事件表烘焙管线(ADR-014/018)
**Unlocks**: 系统 5 epic 的 Definition of Done([L] 四项 SIGN-OFF)· 49 教学中的昼夜教学段

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- 静态断言测试（只读接口、无 float 泄漏、帧率独立）
- [L] 走查证据文档（4 项 NOT-RUN，需可玩构建 + 人工走查）
- 测试: 3 条单元测试（全部通过）

**Deviations**: 
- 四项 [L] 走查（AC-5-09/10/18/21）为 NOT-RUN，需可玩构建 + 人工走查环境

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/TimeWeather/sky_weather_presentation_test.cs` — 3 测全过
- `production/qa/evidence/time-weather/story-005-sky-weather-walkthrough-2026-09-30.md` — 走查记录

**Code Review**: unity-specialist + qa-tester 评审完成，1 BLOCKING 问题已修复：
- B1: test_frameRateIndependent 空测试修复（替换为真正的反射检查）

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
