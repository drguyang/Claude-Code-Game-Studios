# Story 009: 声源生命周期与贴耳交接

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(§States 播放句柄层 · F-44.7 贴耳交接契约 · §Edge Cases)
**Requirement**: TR-audio-012(`EndLoop` 兜底 —— 事件终止信号丢失时的停止路径;2026-09-23 ADR-001 §一之三裁决二结清,gap → covered)

**ADR Governing Implementation**: ADR-001 §一之三 裁决二(主:呈现侧循环终止**必须自评兜底**,零网络依赖,升格架构义务)+ ADR-028 ⑤(44 持有声源池生命周期)
**ADR Decision Summary**: `EndLoop` 不作网络消息存在;P0 默认且唯一路径 = 44 从最新快照 `Progress` 自求值;重连拉流重建 = P1b 增强(须 45 登记三条前置,不免除自评义务)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(有界时间自评需 tick 时钟源;`HANDOVER_MS` 淡化 = mixer 双组已由 Story 003 结构就位)
**Engine Notes**: 交接窗的 crossfade 作用于两级组的 duck 组(不触玩家组);世界语境层句柄归 44 本地持有(ADR-028)—— 停止 ≠ 违「上游唯一收尾」。

**Control Manifest Rules (this layer)**:
- Required: 循环 cue 自评兜底(有界时间内自行结束)· 交接 = 玩家自因、禁双呼吸稳态并存超窗 · 重复 BeginLoop 以最后一次为准
- Forbidden: 循环依赖网络 EndLoop 才停(「呼吸层永不停止」= 已知失败形态)· 快照承担 bus 路由(归脚本动作,Story 003 F7)· 交接硬切(ramp 下界同 50ms 族)
- Guardrail: 分叉的只有音 —— 自评兜底的 `Progress` 求值读的是本地流副本事实,不读表现态

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story(§Edge Cases 为 AC 源)*:

- [ ] **EndLoop 自评兜底**(AC 源:Edge Cases「EndLoop 永不到达」条 + ADR-001 裁决二 Validation):流停更/快照停「循环中」时,44 在**有界时间**(≤ 登记常量 `LOOP_EVAL_MAX_TICKS`)内自行结束循环;`AudioCueDto.EndLoop` 不出现在任何网络序列化路径;负向:注入永不到达的收尾场景 ⇒ 循环必须在有界内停。
- [ ] **贴耳交接契约**(AC 源:Edge Cases 交接条):进入听诊 ⇒ 世界层在 `HANDOVER_MS` 窗内压出、听诊层同窗淡入,**两层并存 ≤ 交接窗,禁同刻双呼吸稳态**;退出反向;世界层停止由 44 对自有句柄执行(ADR-028)。
- [ ] **重复/过期句柄**(Edge Cases):同源重复 `BeginLoop` ⇒ 以最后一次为准释放旧句柄(防双呼吸层);过期 `EndLoop` 忽略不产生播放错误。
- [ ] **咳嗽不停呼吸**(Edge Cases 医学条):咳嗽 cue 不调用呼吸循环的 `EndLoop`(与 Story 004 AC-44-08 联合断言)。

---

## Implementation Notes

*Derived from ADR-001 §一之三裁决二 + ADR-028 + GDD(2026-09-25 二轮 EndLoop 收口)*:

- **P0 = 自评兜底唯一义务**(原「二者择一」读法已废):从最新快照 `Progress` 求值;时钟源 = sim tick(挂 `ITickProvider`,禁墙钟 —— 与 D7 时钟面同纪律)。
- P1b 拉流重建启用前置(45 登记三条):重放派生窗 / 活句柄 reconciliation / 触发方 —— 记 Dependencies,不实现。
- `LOOP_EVAL_MAX_TICKS` 入 §Tuning Knobs(用户调;有界性是「不永不停止」的量化)。
- 交接实现:世界层与听诊层各持句柄;交接状态机两态(世界/听诊)+ 窗内交叉淡化;44 本地 `Release` 世界层句柄不走上游。
- 快照在交接中的角色 = 只管电平(F7=甲);bus 路由动作与交接状态机同处(玩家自因入口)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: 快照两级组与调用点白名单(交接的 mixer 面前提)
- Story 004: 咳嗽×呼吸的相位/听测半边(本 story 只断「不调 EndLoop」)
- 45: P1b 拉流重建(登记前置)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **EndLoop 自评兜底**: Given: 循环 cue + 人为停止流更新(快照停「循环中」)。When: 推进 tick 至 `LOOP_EVAL_MAX_TICKS`。Then: 循环已停(句柄释放);负向:不实现自评 ⇒ 超时红;序列化扫描:EndLoop 类型零网络路径。
  - Edge cases: 流恢复更新后不得重复停;有界常量取 0 ⇒ 当轮必停(下界自证)。
- **贴耳交接**: Given: 世界层循环中。When: 触发进入听诊。Then: `HANDOVER_MS` 窗内完成交叉;窗结束断言 = 仅听诊层活跃(世界层句柄已 Release);退出镜像;负向:直接硬切(ramp=0)⇒ 红。
  - Edge cases: 窗内重复触发进入(幂等);听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)。
- **重复/过期句柄**: Given: 同 cue 两次 BeginLoop / 过期 EndLoop。When: 跑。Then: 只存活最后一次句柄;过期 EndLoop 无异常无状态破坏。
- **咳嗽不停呼吸**: Given: 呼吸循环 + 咳嗽 cue 到达。When: 派发咳嗽。Then: 呼吸句柄仍存活且未重启(相位不重锚)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/audio_system/loop_lifecycle_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(循环字段载体)· 003(两级组)· 008(两层皆可播)
- Unlocks: Story 011(注册表消费面)· 013(交接后主通道稳定态才可听测)
