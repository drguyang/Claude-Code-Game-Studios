# Story 006: 语声变体库与 Intensity 分桶

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(规则五 · F-44.3 · F-44.6)
**Requirement**: TR-audio-006(语声变体库混合,禁参数调制装多样 + 50ms ramp 防爆音)

**ADR Governing Implementation**: ADR-018: 音频架构(主:§四 需求② —— 变体库 + 交叉淡化)
**ADR Decision Summary**: 男/女 × 强/中/弱 × 句尾按表选变体交叉淡化;**禁**音高/气声/语速/断句/共鸣五项独立调制(变声器不是病人说话)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(素材量 `OQ-44-2` 未定,content 批前置)
**Engine Notes**: F-44.6 的 `bucket(u)` 是 AC-44-03 的选择输入(2026-09-25 二轮轴对齐:传桶不传 raw);分桶阈值属用户调参。

**Control Manifest Rules (this layer)**:
- Required: `SelectVariant(CueId, Tier, bucket(u))` 查表 · 交叉淡化 ramp ≥ 50ms · VariantLib per-CueId 行结构(性别 = 素材表声明)
- Forbidden: 五项参数调制(AC-44-03 禁)· 运行期 `Enum.Parse`/反射查表(玩家构建零解析器)· 分桶边界绑 sim 阈值(换素材报状态的后门)
- Guardrail: 分桶 = 连续参数的渲染量化,边界与 sim 阈值**解耦**(GDD 二轮注)—— 断言边界常量不等于任何已知 sim 阈值

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-03** [A]+[L]:素材形态 = 变体库(per-CueId 行,行内 gender 声明 + 桶→句尾变体集),运行时按 (`CueId`, 精度档, `bucket(u)`) 选变体并交叉淡化;**禁**五项独立调制([A] = 调用面扫描零五项 API;[L] = 听测不像变声器)。
- [ ] **AC-44-04**:交叉淡化无爆音(spike)、无相位抵消;**滤波/噪声底变化 ramp ≥ 50 ms**。
- [ ] **F-44.6 映射面**(AC-44-03 的载体):`u = Intensity/255` → `gain_scale`/`density_mult` 线性映射 + `{弱/中/强}` 分桶(桶序与公式一致);`bucket(u)` 输出入 `SelectVariant`。
- [ ] **回退规则**(GDD Edge Cases):变体库缺该组合 ⇒ 回退最近可用档(档距最近,平局取高);烘焙门 = 逐 (CueId × Tier × 桶) 全组合非空,任一轴向空缺 = 构建失败(原「整 cue 可回退」不足已废)。

---

## Implementation Notes

*Derived from ADR-018 §四 + GDD F-44.3/F-44.6(2026-09-18 幽灵输入修复 + 2026-09-25 轴对齐)*:

- **幽灵输入已废**:选择输入不含 `PatientSemantics`(44 读不到);病种语义由上游编进 `CueId`。
- VariantLib = **per-CueId 行表**(烘焙整数索引查表,无运行期反射);gender 是**内容侧声明**(素材行携带),非选择输入 —— 悬空的「男/女选择轴」已修。
- `bucket(u)` = F-44.6 分桶(弱/中/强,`BUCKET_LOW < BUCKET_HIGH`);`Intensity=0` ⇒ u=0 ⇒ 弱桶 + `GAIN_MIN` —— **是否发声**归 Edge Cases 边界(`DENS_MIN ≥ 0` 值域用户调;若 `DENS_MIN=0` 出现静音路径,挂 AC-44-01 同格断言,二轮已记)。
- 50ms ramp 常量与 Story 004 共用单处定义。
- 分桶边界解耦断言:`BUCKET_LOW/HIGH ∉ 已知 sim 阈值集`(静态对照注记;sim 阈值集来自9/52 常量表引用)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: `subtitle_text`(语声文本本体)—— 本 story 只管变体音频选择
- Story 005: `SIGNAL_dB` 分析域(渲染增益与 SNR 断言分属两域)
- 内容批:变体素材实际录制(`OQ-44-2` 素材量上限)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-03**: 选变体 + 禁调制。Given: VariantLib 夹具(per-CueId 三桶齐全)。
  - When: `SelectVariant(cue, tier, bucket)` + 调用面扫描。
  - Then: 同输入恒选同 clip(确定性);五项调制 API 零调用点;负向:注入缺 (cue, tier, 桶) 组合的表 ⇒ 烘焙门红。
  - Edge cases: Tier=0 表空且高档有 ⇒ 回退「档距最近平局取高」不落空;全表空 ⇒ 构建失败。
- **AC-44-04**: ramp 断言。Given: 滤波/噪声底切换路径。When: 读过渡时长参数。Then: ≥50ms;负向:写 10ms ⇒ 红;[L] 听测无 spike/相位抵消(证据可与 004 听测同批)。
- **F-44.6 载体**: 分桶映射。Given: Intensity ∈ {0,1,127,128,254,255}。When: 映射。Then: 桶序 弱/中/强 单调;u=0 ⇒ 弱+GAIN_MIN;边界 `BUCKET_LOW` 恰含(< 为弱,≥ 为中)。
- **回退门**: Given: 轴向局部空缺夹具。When: 烘焙。Then: 红(逐组合非空)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/audio_system/voice_variants_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 · 002(事件表字段载体)
- Unlocks: Story 011(字幕文本与变体同表)· 内容批素材录制可开工(结构定)
