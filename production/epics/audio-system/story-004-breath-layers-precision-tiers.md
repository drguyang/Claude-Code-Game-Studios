# Story 004: 听诊呼吸两层与精度档

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 6h(含 [L] 听测排期)
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(规则四 · F-44.1 · F-44.5 · §Edge Cases 听诊段)
**Requirement**: TR-audio-005(听诊呼吸两层 = 通带+噪声底,禁静音路径;细湿啰音非连续水声;相位锁定)

**ADR Governing Implementation**: ADR-018: 音频架构(主:§四 需求① V-8.7 债务清偿 + AC-44-08 医学准确性)
**ADR Decision Summary**: 低技能 ≠ 静音,低技能 = 更吵的接触噪声 + 更窄的通带 + 更少频段细节;三档附加音层始终非零。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(AudioMixer 6.3 post-cutoff ⇒ `OQ-44-5` spike;逐源 `AudioLowPassFilter` 须补录 engine-reference)
**Engine Notes**: 滤波/噪声底变化 ramp ≥ 50 ms(GDD F-44.1 注硬下界);`tier_map` 数据经 Story 002 schema 门。

**Control Manifest Rules (this layer)**:
- Required: 三档附加音层始终非零(数据级断言)· ramp ≥ 50 ms · `adventitious_policy` 相位窗 ⊆ 吸气段
- Forbidden: 任何「档位 ⇒ 静音」路径(AC-44-01 BLOCKING)· 连续水声冒充细湿啰音(AC-44-08)· 独立自由循环(相位必须经 `clock_ref` 锁定)
- Guardrail: 「可辨」= 双盲 forced-choice 协议(qa 具体化),不是口头判断

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-01**(BLOCKING):数据级 —— `tier_map` 三档的附加音层增益字段均 > 0;`.mixer` 无 tier 条件静音标志位(Force Text YAML 扫描);(可选 PlayMode)三档 RMS 探针均 > 0。
- [ ] **AC-44-02** [L]:三档呼吸音差异**可辨**且非静音型(通带更窄/噪声底更高,而非更小音量)—— 双盲 forced-choice:(0,1)(1,2)(0,2) 三对,n ≥ 6 人 × 每对 ≥ 8 次,判对率 ≥ 75%;`production/qa/evidence/` 签署。
- [ ] **AC-44-08** [L]+[A]:听测 + 波形 —— 细湿啰音 = 吸气末、断续、细密爆裂音,**咳嗽后不消失**;非连续水声;[A] 半 = `adventitious_policy` 字段存在 + **窗口 ⊆ 吸气段**(`trigger_phase ∈ [0.8,1.0]×吸气段时长`,相位原点 = 吸气起点;校验规则 8)。
- [ ] **AC-44-14**:附加层行 `clock_ref` 指向存在的基础层行(悬空 = 失败);基础层与附加层共用同一节拍时钟,非独立循环。

---

## Implementation Notes

*Derived from ADR-018 §四 + GDD F-44.1/F-44.5/Edge Cases(2026-09-25 二轮 F5 相位重钉)*:

- `TierMap` = 事件表顶层 `tier_map`(六列,Story 002 载体);本 story 消费列做滤波/噪声底驱动。
- 附加音层**相位锁定**到基础循环,窗口 = 吸气段末 20%;`INSPIRE_FRACTION`(I:E 派生)入 Tuning;`clock_ref` 由 schema 门(002)强制,运行期执行。
- 咳嗽交互:咳嗽 cue **不得** `EndLoop` 呼吸循环(Edge Cases 医学条)—— EditMode 断言咳嗽实现不调用该循环的 EndLoop。
- 两层独立循环的相位漂移 bug 由 `clock_ref` 结构消除;[L] 听测与 [A] 结构断言**分别留证**。
- ramp ≥ 50 ms 属切换平滑 —— 与 Story 006 的交叉淡化共用实现底座(50ms 常量单处定义)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: SNR 分析域与噪声下界的数值断言(05/06)
- Story 006: 交叉淡化的曲线/变体选择(03/04)
- Story 013: 主通道(隔门)听测 —— 本 story 是听诊层贴耳路径

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-01**: 数据级非静音。Given: 合法 `tier_map` 三行。When: 读附加音层增益字段 + 扫 `.mixer`。Then: 三档 > 0、无静音标志位;负向:把低档增益写 0 ⇒ 红;插入 mute 标志位 ⇒ 红。
- **AC-44-02** [L]: 听测。Setup: 三档各出样段(同素材源)。Verify: 双盲 forced-choice 三对判别。Pass: 判对率 ≥75% 且差异属通带/噪声型而非音量型;证据签署入 `production/qa/evidence/`。
- **AC-44-08**: 波形 + 相位。Setup: 播附加层(细湿啰音素材)。Verify: [L] 波形瞬态簇 + 听测非水声;[A] `trigger_phase ∈ 吸气段窗`、咳嗽路径不调 EndLoop。Pass: 双侧留证。
- **AC-44-14**: clock_ref 闭合。Given: 附加层行。When: 门 + 运行期时钟核对。Then: 悬空 `clock_ref` 红;合法行两层同节拍。

## Test Evidence

**Story Type**: Logic(含 [L] 听测)
**Required evidence**:
- `tests/unit/audio_system/breath_layers_test.cs` — [A] 半必须通过
- `production/qa/evidence/breath-layers-listen-evidence.md` — [L] 听测签署(AC-02/08)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 · 002(tier_map/clock_ref 载体)· 003(StethoscopeFocus 快照)
- Unlocks: Story 005(SNR 消费同两列)· 013(听测协议先在贴耳路径演练)
