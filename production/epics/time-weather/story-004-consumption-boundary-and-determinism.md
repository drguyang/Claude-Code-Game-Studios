# Story 004: 消费边界与确定性对拍 —— 不进流、天气不改移动、跨平台逐位

> **Epic**: 时间与天气
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/time-and-weather.md`(§Rules 派生态与「5 不结算玩法」· §Edge Cases 无事件写入 · AC-5-07(EXTERNAL,挂 ADR-012)· AC-5-08 天气 P0 不影响移动 · AC-5-19 无流写入断言)
**Requirement**: TR-timeweather-003(派生态不进流/不存档)· TR-timeweather-009(5 只产乘子与读数)· TR-timeweather-010(天气对移动的影响 P0 不启用,5 零速度修正;P1a 若启用须走地貌同一条 K_TERRAIN 通道)· TR-timeweather-013(同一 (WorldSeed, tick, cell) 跨平台重放 ⇒ kind/intensity/EnvMod_env 逐位相同,`adr: ADR-012`)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-012(主): 跨平台确定性 CI 门;ADR-009(世界状态事件化边界)
**ADR Decision Summary**: 双级黄金夹具(单元级哈希 + 集成级字节)三格常驻矩阵;派生态不进流(ADR-009 §一 三问判据);5 只产读数与乘子,玩法结算归各消费方。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 跨平台逐位对拍依赖 IL2CPP player 实跑(ADR-012 F7 有符号溢出 UB 谱系)—— 本机【超算】无 player 矩阵 ⇒ 判据可写,执行须 CI;**禁借绿**。

**Control Manifest Rules (this layer)**:
- Required: 5 侧可写 `Kind` 集恰 = ∅(正面白名单断言);天气读数与移动通道物理隔离;黄金夹具纳入 `golden-vN` 版本化刷新
- Forbidden: `IEventSink.Append` 出现在 5 的任何路径;5 内出现速度 / 位移修正;单平台独签夹具(ADR-012 禁则)
- Guardrail: AC-5-07 属 EXTERNAL —— 本机不可跑时以 CI 矩阵产物为证据,不得以 EditMode 绿替代

---

## Acceptance Criteria

*From GDD `design/gdd/time-and-weather.md`, scoped to this story:*

- [ ] 静态断言:5 的程序集内 `IEventSink.Append` / `IEventAuthority` 写出调用点恰 = 0(读侧骰子除外);天气不产生任何 SimEvent(AC-5-19)
- [ ] 存档 round-trip:含一段跨夜跨季跨天气块的会话,存档体积与流条目数对 `isNight` / `season_index` / `Weather` 零敏感 —— 三者纯重建(AC-5-07 侧的「不存档」半边)
- [ ] `EnvMod_raw` 的消费方联调:21a 钳制 + 17 季节调制 + 52 天气过滤三条通道各取所需,5 无一方言;任一消费方越界取值 ⇒ 集成断言拒收(承 ADR-008 §六 有界性口径)
- [ ] AC-5-08:任意恶劣天气(最大 intensity)下玩家位移与 `Nav` 可走性不受 5 影响;代码层 5 与 1 / 23 之间无速度通道(P0 硬事实)
- [ ] 跨平台逐位对拍:同 (WorldSeed, tick, cell) 序列在 Mono 与 IL2CPP 上 kind/intensity/EnvMod_raw 哈希逐位相同(挂 ADR-012 集成级夹具,EXTERNAL)
- [ ] 重放一致性:读档后重算的历史天气序列与原始会话逐位相同(派生态重建判据)

---

## Implementation Notes

1. 对拍夹具输入 = 固定 WorldSeed + 覆盖多个天气块与昼夜环绕段的 tick 列表 + 若干 cell(含无生态区命中的 cell)。
2. 「不进流」用反射/引用断言 + 运行期计数探针双保险(静态白名单 + 一场真实会话的事件条目比对)。
3. 消费边界联调按消费方逐个接线:`EnvMod_raw → 21a F1`、`season_index → 17`、`(kind,intensity) → 52 / 42 / 44`;5 只提供读数接口。
4. 跨平台执行落 CI(三格常驻矩阵),本地只跑 Mono 侧;证据文件注明 NOT-RUN 项,禁借绿。
5. `ENV_MOD_MIN/MAX` / 曲线参数值属数值轮 —— 本 story 只验通道与逐位性,不判定数值合理性。

---

## Out of Scope

- [Story 005]: 呈现读数与 [L] 走查(天空光照 / 天气体感)
- [Story 001–003]: 相位 / 掷骰 / EnvMod 的机制实现(本 story 是其集成验证面)
- 21a F1 钳制与 17 季节曲线的实现本体(归各系统 epic,此处只联调)
- P1a 天气影响移动(明令不在 P0,AC-5-08)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 完整会话(跨 2 日 + 跨季 + 若干天气块) | 统计三流事件条目 | 无任何来自 5 的 Kind;条目数与天气序列无关 |
| TC-2 | 同一会话读档 | 重算历史天气 | 与存档前逐位相同(重建) |
| TC-3 | 最大 intensity 暴雨 + 夜 | 玩家移动 / AI 寻路 | 位移与可走性无变化(AC-5-08) |
| TC-4 | Mono 与 IL2CPP 各跑同一夹具 | 比对轨迹哈希 | 逐位一致(CI 产物为证) |
| TC-5 | 消费方(21a)传越界 ENV_MOD_MAX | 集成断言 | 拒收 + 构建/测试失败,非静默截断 |

**Edge cases**: 存档头 WorldSeed 缺失 ⇒ 硬失败;IL2CPP 侧 int64 回绕与 Mono 不一致(即 ADR-012 F7,须按 UB 谱系查);未驻留 chunk 的 cell 参与对拍。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/PlayMode/TimeWeather/time_weather_replay_test.cs` 或 `tests/integration/time-weather/` 集成测试 + CI 矩阵产物 — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001 / 002 / 003(被测机制)· ADR-012 黄金夹具矩阵(EXTERNAL 前置)· 21a F1 与 17 的消费接口存在
**Unlocks**: Story 005([L] 走查需读数通道稳定)· 52 的天气过滤验收 · 系统 5 的 epic DoD

---

## Completion Notes
