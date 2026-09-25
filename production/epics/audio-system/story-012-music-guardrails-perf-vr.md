# Story 012: 乐层护栏与性能/VR 切面

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

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

- [ ] **AC-44-18**(2026-09-25 二轮新增):战斗乐层行 —— ① `xfade_ms ≥ MUSIC_XFADE_MIN_MS`(构建期数值比较,超界 = 失败);② `trigger_source = EncounterMusicLayer` 且 G1–G3 护栏登记在册;③ `MUSIC_XFADE_MIN_MS` owner = 44 本表(Tuning 已列),25 侧同名引用交叉注。
- [ ] **AC-44-E2**:60s 采样窗 `Audio.Process`/DSP 回调 **P95 ≤ `AUDIO_BUDGET_MS`**(登记旋钮;16.6/11.1ms 为参考上界);且 `AC-10-08` 测量点注册表**不含 `IAudioCueSink.Emit`**(静态核对)。
- [ ] **AC-44-E1**:P0 构建无 VR 音频(装配/场景扫描:VR 相关音频路径零启用);P1b 交付切面(挂点/快照/延迟)登记在册(非「已实现」)。
- [ ] **AC-44-11**(P1b,BLOCKED):VR 听测无晕动副作用(受试者报告 + 帧率实测)+ 头显单 `AudioListener` —— **随 VR 模式 P1b 执行,本 story 只登记不跑**(禁借绿)。

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

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(xfade_ms 载体)· 003(乐层总线在 mixer 中)
- Unlocks: 25 侧 G2 盲测可排期(载体就绪)
