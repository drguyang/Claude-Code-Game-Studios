# Story 002: 天气纯函数 —— 块哈希掷骰与生态区查表

> **Epic**: 时间与天气
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/time-and-weather.md`(§Formulas F-5.3 `Weather(t,cell)=Roll(WorldSeed, FDiv(t,WEATHER_BLOCK_TICKS), EcozoneOf(cell))` · R-5-A 块哈希 · B-8 无时间积分 · §Rules 派生态判据 · AC-5-06 / AC-5-11…13)
**Requirement**: TR-timeweather-003(天气 = (WorldSeed, 块(tick), EcozoneOf(cell)) 纯函数,派生态不进流/不存档)· TR-timeweather-004(掷骰统一走 IEventAuthority,零 UnityEngine.Random)· TR-timeweather-005(WorldSeed 归 7a 存档头)· TR-timeweather-002(生态区气候属性由 6 定义、5 消费)· TR-timeweather-011(未驻留 chunk ⇒ 全局默认,不取『最近区』)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-007(主): 事件权威与掷骰状态;ADR-009 / ADR-015(生态区查询与派生态)
**ADR Decision Summary**: 掷骰每个输入必须可从事件流 / 烘焙数据重构(三源不变量);WorldSeed 住存档头非 SimEvent;天气为派生态 —— 不进流、不存档,重建期由纯函数重算;块哈希(R-5-A)保证同块内天气恒定且**无时间积分**(B-8:逐 tick 累积会破坏纯函数性)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 整数哈希(SplitMix64 类,BCL 纯 C#)须过 ADR-012 黄金夹具的单元级对拍;不触及 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 掷骰走 `IEventAuthority` 统一通道;`EcozoneOf(cell)` 复用 6 的整数几何查询;天气结果 = `(kind: int 枚举, intensity: int ∈ [0, INTENSITY_MAX])`,INTENSITY_MAX 归 5 所有
- Forbidden: `UnityEngine.Random` / `System.Random` / 墙钟参与抽池;把天气写进任何事件流或存档字段;跨块插值 / 渐变(= 时间积分,B-8)
- Guardrail: 同 (WorldSeed, block, ecozone) 三元组 ⇒ 逐位同结果;无 ECOZONE 命中 ⇒ 全局默认档,禁「最近区」兜底

---

## Acceptance Criteria

*From GDD `design/gdd/time-and-weather.md`, scoped to this story:*

- [ ] `Weather(t, cell)` 对同一 `(WorldSeed, FDiv(t, WEATHER_BLOCK_TICKS), EcozoneOf(cell))` 三元组返回逐位相同的 kind/intensity(AC-5-06)
- [ ] 块边界处天气可跳变、块内恒定 —— 断言「块内任意两 tick 结果相等」与「跨块不要求连续」两侧(AC-5-11/12)
- [ ] 掷骰输入完全可重构:给定存档头 WorldSeed + tick + 烘焙生态区数据,离线重放能复算出与运行期完全一致的天气序列(三源不变量,AC-5-13)
- [ ] `EcozoneOf(cell)` 无命中(玩家未驻留 chunk)⇒ 取全局默认气候档;代码中不存在「最近生态区」路径(TR-timeweather-011)
- [ ] 5 不定义任何生态区/气候字面量,全部经 6 的烘焙数据消费(TR-timeweather-002)
- [ ] 零 `UnityEngine.Random` / 零墙钟:反射/引用断言抽池路径输入类型恰 ⊆ {int, long, Fix, 枚举, 烘焙 struct}

---

## Implementation Notes

1. 抽池 = `Roll(WorldSeed, blockIndex, ecozoneId) → (kind, intensity)`:块索引 `FDiv(t, WEATHER_BLOCK_TICKS)`(整除,承 Story 001 的 FMod/整除纪律)。
2. 哈希体走 `IEventAuthority` 的统一骰子实现(SplitMix64 系,BCL),5 只提供输入三元组,不自建第二骰子。
3. 生态区气候表(每区各 kind 的权重/强度分布)为 ADR-014 烘焙数据 `Fix`/`int` 字段;权重和的合法性由阶段 2 校验(白名单/区间)。
4. 结果 DTO 只读暴露;42/44 侧的天空读数与语境声 cue 由后续 story 接线,本 story 不产呈现。
5. WEATHER_BLOCK_TICKS / 权重 / INTENSITY_MAX 具体值归用户数值轮 —— 机制与合法性断言先行。

---

## Out of Scope

- [Story 001]: tick 相位与 FMod(本 story 消费其块索引)
- [Story 003]: EnvMod_raw 的组装(消费本 story 的 kind/intensity)
- [Story 004]: skylight 呈现通道与跨平台重放夹具

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 固定 WorldSeed、同一块内两个 tick | 求 Weather(t1,cell) / Weather(t2,cell) | kind/intensity 逐位相同 |
| TC-2 | 跨块边界(t 与 t+1 分属相邻两块) | 连续求值 | 允许跳变,无插值中间态(B-8) |
| TC-3 | 同三元组在「运行期」与「离线重放」各算一遍 | 比较 | 完全一致(可重构性) |
| TC-4 | cell 不在任何生态区多边形内(未驻留 chunk) | 求 Weather | 返回全局默认档,无异常、无「最近区」 |
| TC-5 | 把骰子实现换成 UnityEngine.Random 的编译变体(探针) | 静态检查 | 引用集断言失败(负面夹具) |
| TC-6 | intensity 分布上界 = INTENSITY_MAX | 全 ecozone × 全块扫描 | 永不越界 |

**Edge cases**: WorldSeed 未装载(存档头缺失)⇒ 装载期硬失败;块索引溢出(long 域安全范围断言);空权重表 ⇒ 烘焙期校验失败。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/TimeWeather/weather_roll_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001(块索引/整除)· 系统 6 的 `EcozoneOf` 整数查询(Story world-ecozones 002 提供;接口先立桩可并行)· ADR-007 `IEventAuthority`
**Unlocks**: Story 003(EnvMod 组装)· Story 004(重放夹具的天气项)· 52 的遭遇天气过滤消费

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `WeatherParams` — 天气参数（WEATHER_BLOCK_TICKS / INTENSITY_MAX）
- `WeatherState` — 天气状态（Kind / Intensity）
- `WeatherRoll` — 天气纯函数（ComputeBlockIndex / ComputeWeather / IsConstantWithinBlock）
- 块哈希掷骰（SplitMix64）
- 块内恒定 + 跨块允许跳变
- 测试: 7 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/TimeWeather/weather_roll_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
