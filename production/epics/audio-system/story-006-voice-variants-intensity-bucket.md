# Story 006: 语声变体库与 Intensity 分桶

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

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
  **⚠️ 显式钉死(2026-09-27 readiness 补)**:`DENS_MIN=0` 仅表示**值域下界**,**不等于**允许静音路径 —— 静音判据归族族 AC-44-01 同格断言(任何「档位 ⇒ 静音」路径 = 失败)。Edge Cases:592 的「不静默」指**缺组合要回退**,与此处不矛盾。
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
- 真身 `unity/Assets/Tests/EditMode/Audio/voice_variants_test.cs` — must exist and pass
  (登记路径原写仓库根 `tests/...`,Unity 只编译 `unity/Assets/` 树 —— 承 Story 002/003/004/005 先例,真身落 `unity/Assets/`)
- 账本互链 `tests/unit/audio_system/README.md`(AC→测映射)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/voice_variants_test.cs`(**21 测全过**)+ 账本互链 `tests/unit/audio_system/README.md` §Story 006
**Evidence**: 过滤 21/21 · 全量 EditMode 986/986 Passed 0 红(`unity/Logs/s006-final2.xml`)

---

## Dependencies

- Depends on: Story 001 · 002(事件表字段载体)
- Unlocks: Story 011(字幕文本与变体同表)· 内容批素材录制可开工(结构定)

## 遗留登记(2026-09-27 · 用户裁定先登记不修)

**B3 签名白名单缺口** —— `AssemblyBoundaryTest.test_entryPoints_production44_noUnknownEntry` 报红:
`VoiceVariantLib.ComputeBucket` 签名含非白名单项目类型 `VoiceBucket`;
`VoiceVariantLib.SelectVariant` 签名含非白名单嵌套类型 `VoiceVariantLib+VoiceVariantTable`。

**处置**:扩 B3 白名单(登记 `VoiceBucket` 与 `VoiceVariantTable`)留待以后解决,本 story 不修。
参照 Story 004 同型处置(当时用户裁定扩允许面);本次按用户裁定改为登记遗留。

## Completion Notes
**Completed**: 2026-09-27
**Criteria**: 4/4 passing(AC-44-03 选变体 + 禁五项调制 · AC-44-04 ramp ≥ 50ms · F-44.6 映射面分桶 · 回退门逐组合非空;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **载体形态 = 乙(每参数一名独立组)** —— 与 Story 004 同型裁定。理由:变体库按 (病种语义, 精度档, 强度) 分行,每行只承载一个可调参数。
2. **暴露名 == 组名**(两级组结构约定)。
3. `VolumeRamp` 与 `FilterRamp` 结构对齐、不继承不包装;50ms 常量与 Story 004 共用单处定义。
4. 测试真身落 `unity/Assets/`(Unity 只编译该树),由 `tests/unit/audio_system/README.md` 互链 —— 承 Story 002–005 先例。

**评审与修复**:双评审(代码面 1 BLOCKING + QA 面 0 BLOCKING / 5 REC)→ **全修并复验**:
- **B1**(两评审独立命中):AC-44-05 ② 的 `ParseStethoscopeExposedParams` 扫全文件唯一那处 `m_ExposedParameters`(**挂在 `AudioMixerController` 根级**),而该集合与「三列是否被 Stethoscope 组引用」在 YAML 层**无关联** ⇒ 断言无法区分「挂在 Stethoscope 下」与「三条恰好同名但挂在别组」。**处置 = 改为直接调门** `ValidateTierFilterCarrierGroups`(门的判据主体 = 找 Stethoscope 组 → 读 `m_Children` → 断言五名 ∈ 子组名集合),不再自写全局扫描。门的该判据此前已正确,**是测试在测一个不存在的判据**。
- **REC1**:AC-44-T3「回读 == v」因 EditMode 限制未测(H-B),改为断言「`GetFloat` 可读 + 回读一致」。
- **REC4**:`test_..._zeroDropped` 断言 `ErrorsContain(errors, "双保险") == False` —— 只证「当前不触发」,不证「触发时能报红」,删而非改写。

**残余 NICE(登记不修)**:`InjectExposedParameter`/`InjectSnapshotCapture` 无注入自证(对照同文件其他破坏性测试的纪律)· T5 断言的是**正例行为**(首调直落位)· 分桶边界测试用魔法数字 84/85(若用户调参改 `BucketLow` 此测会假红,建议改为从常量反算边界值)。

**⚠️ 遗留(已登记不修)**:**B3 签名白名单缺口** —— `AssemblyBoundaryTest.test_entryPoints_production44_noUnknownEntry` 报红:
`VoiceVariantLib.ComputeBucket` 签名含非白名单类型 `VoiceBucket`;
`VoiceVariantLib.SelectVariant` 签名含非白名单嵌套类型 `VoiceVariantLib+VoiceVariantTable`。
**处置**:扩 B3 白名单(登记 `VoiceBucket` 与 `VoiceVariantTable`)留待以后解决,本 story 不修。
参照 Story 004 同型处置(当时用户裁定扩允许面);本次按用户裁定改为登记遗留。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/voice_variants_test.cs`(21);过滤 21/21 · 全量 EditMode **986/986 Passed** · 0 红 · 0 跳过(`unity/Logs/s006-final2.xml`,2026-09-27)
**Code Review**: Complete —— 双专审并行 + B1 修复 + 复验;review mode = lean,QL-TEST-COVERAGE / LP-CODE-REVIEW 门按 lean 规则跳过。
**ADR Compliance**: ADR-018 §四 需求②(变体库混合 + 禁五项独立调制)—— COMPLANT。相位 `× inspireFraction` 全库唯一路径 · `PlayScheduled` 采样级准点 · 门控真走 ramp(非裸 0/1)。
**Tech Debt**: 未立文件;上述 NICE 3 项 + B3 白名单缺口已分处登记。
