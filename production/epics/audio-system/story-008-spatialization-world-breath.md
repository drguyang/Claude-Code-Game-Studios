# Story 008: 空间化与世界语境呼吸

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(F-44.4 · F-44.7 · 优先级公式 · AC-44-D2/D3/D8/16)
**Requirement**: TR-audio-011(世界语境呼吸 —— 幻想主通道「门后的人还在喘气」的载体)

**ADR Governing Implementation**: ADR-028: 世界语境声源归属(主:44 持有声源池 / 零场景预摆 / 位置只读 / 生命周期 cue 驱动)+ ADR-015 §三(整数格)· ADR-018 §二(禁反推)
**ADR Decision Summary**: `AudioSource` 归 44 运行期池,非 Boot 场景内容(预摆 = 构建失败);位置真源 = `AudioCueDto.Cell` / 远端表,禁 float 反推;总线 = Ambience 世界语境子通道。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(3D 空间化长期稳定;逐源 `AudioLowPassFilter` 须补录 engine-reference 挂 `OQ-44-5`)
**Engine Notes**: 组件三件套 pin(`spatialBlend=1` · Logarithmic rolloff + min/max · `dopplerLevel=0`);`occlusion` = 44 本地表现层 raycast(派生态不进流,帧预算挂 E2)。

**Control Manifest Rules (this layer)**:
- Required: 零场景预摆(`AudioSource` 非 Boot 内容 = 构建失败,ADR-028 ②)· 位置只读 · cell_jitter 确定性格内偏移(渲染用不回写)
- Forbidden: 从 float 反推逻辑格 · 44 自建位置通道(Story 007 D1)· 同 cell N 病人合并成一条声
- Guardrail: 隔门 ≠ 静音,是变闷(occlusion_lowpass);max_distance 内须仍可辨(旋钮)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-D2**:`AudioCueDto.Cell`(整数格)→ 表现层换算 float;**禁从 float 反推逻辑格**(反射断言:无返回 `WorldPos` 且参数含 float/Vector3 的方法)。
- [ ] **AC-44-D3**:音频空间化不写 sim、不回写三流(与 B1 IL 扫描合并断言,防双账)。
- [ ] **AC-44-D8**:① 遮挡输入(= 44 表现层 raycast 结果)为真 ⇒ 逐源 `AudioLowPassFilter` 施加低通且 cutoff > 0(**穿门 ≠ 静音**);② 同 cell N 病人 = **N 条独立 `AudioSource`**(实例数断言 = N,不合并);增益序由 `rank_key`(class → distance → cell_jitter → CueId)确定性给出。
- [ ] **AC-44-16**:事件表存在世界语境呼吸行(`bus` ∈ Ambience 世界语境子通道,含 min/max_distance 与 occlusion 参数);行缺失 / bus 不符 = 构建失败(**堵 AC-D8 预设式条件空真通过** —— 主通道接线断言)。
- [ ] **优先级公式**(§Visual/Audio 多声源混合规则):`rank_key` 实现 + 「赢」= 抢占序与 `DUCK_DEPTH_DB` 深度(只压不丢)+ 重算只在声源生灭时(防抖动)+ `bus ≠ 优先级类别`(世界呼吸走 Ambience 但属生理声类)。

---

## Implementation Notes

*Derived from ADR-028 + GDD F-44.4/F-44.7(2026-09-25 二轮 F3/Y3 补式与登记)*:

- **声源池**:44 拥有池的创建/复用/释放(ADR-028 ⑤);生命周期 cue 驱动,存在性不进三流;挂 `HandoverMux` 与 Story 009 的交接窗共用。
- **cell_jitter** = `SplitMix64(ActorId, Cell)` 派生 u16(ADR-023 ⑦ / S7 同族)—— 同格整数锚点距离恒等的破平键;只叠渲染坐标。
- **d=0 / 等距**:公式零除式结构性不存在(权重不除 d);NaN 路径以边例测试锁定。
- **occlusion 判定源** = 44 本地 raycast(视觉几何;不进流,ADR-015 两层纪律);输入存在性 = D8 的 GIVEN。
- **AC-44-D3 与 B1 合并断言**(qa 指出的双账):一条 IL 扫描覆盖。
- `rank_key` 的重算时机 = 声源生灭事件(挂起不逐帧)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: 交接窗(HANDOVER_MS)与循环句柄生命周期(本 story 交付「能播、序确定」)
- Story 007: 位置来源纪律(D1)—— 本 story 消费其只读面
- Story 013: 隔门听测验收

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-D2**: 禁反推。Given: 空间化实现。When: 反射扫方法签名。Then: 无「float 入 → WorldPos 出」方法;负向:注入反推方法 ⇒ 红。
- **AC-44-D8**: 遮挡 + 多病人。Given: 同 cell 2 病人夹具 + 模拟遮挡输入。When: 跑空间化。Then: AudioSource 实例数 = 2;遮挡真 ⇒ 两源 cutoff < 无遮挡基线且 >0;rank_key 序确定(同输入三次一致)。
  - Edge cases: d=0(听者同格)⇒ 不静音不 NaN;两病人等距 ⇒ cell_jitter 分出主次;N=1 时实例数=1。
- **AC-44-16**: 接线断言。Given: 事件表。When: 校验。Then: worldBreath 行存在且 bus/参数齐;删行 ⇒ 构建失败。
- **优先级公式**: Given: 混合声源集(自身动作/呼吸/狗吠/氛围)。When: 求 rank_key。Then: 类序正确;等距平局确定;「赢」= duck 至 `DUCK_DEPTH_DB` 不丢声;生灭前序稳定(挂起期重算 = 红)。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/unit/audio_system/spatialization_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 · 002(worldBreath 行载体)· 007(D1 位置来源)
- Unlocks: Story 009(交接建立在两层皆可播之上)· 013(主通道听测)
