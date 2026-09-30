# Story 001: 时间基准 —— tick 只读与昼夜 / 季节相位

> **Epic**: 时间与天气
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/time-and-weather.md`(§Detailed Rules F-5.1 昼夜相位 · F-5.2 季节索引 · §Formulas FMod 定义 · R-5 纪律 · AC-5-01…05 / AC-5-14 / AC-5-20)
**Requirement**: TR-timeweather-001(tick 唯一来源 = ITickProvider,5 只读零写)· TR-timeweather-006(TICKS_PER_DAY 单一定义点,52 引用,不一致 = 构建期硬失败 · 现 `gap`)· TR-timeweather-007(FMod 环绕安全的昼夜判定 · 现 `gap`)· TR-timeweather-014(参数装载期硬失败)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟与状态同步模型
**ADR Decision Summary**: 全部模拟数学在整数定点域(int64 / Q16.16);tick 唯一来源 = `ITickProvider`,5 只读 `CurrentTick`,零写 tick 路径(禁 `Time.deltaTime` / 墙钟 / 自增计数器);主机唯一执行 Step。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数逻辑(门 A 侧 `Sim` 程序集,`noEngineReferences`),不触及任何 post-cutoff API;EditMode 可测。

**Control Manifest Rules (this layer)**:
- Required: 一切除法为整除;取模一律走 `FMod(a,n)=a−n·⌊a/n⌋∈[0,n)` 自实现;TICKS_PER_DAY / TICKS_PER_SEASON / SEASONS_PER_YEAR / NIGHT_START / NIGHT_SPAN 从烘焙数据装载
- Forbidden: C# `%` 运算符直接用于负操作数(截断陷阱,AC-5-20);`float` / `double` 出现在时间推导;5 自行写 tick
- Guardrail: `SEASONS_PER_YEAR ≥ 2`;`NIGHT_SPAN < TICKS_PER_DAY`;装载期任一参数非法(=0 / 未装载)⇒ 硬失败,禁 null 兜底(AC-5-14)

---

## Acceptance Criteria

*From GDD `design/gdd/time-and-weather.md`, scoped to this story:*

- [ ] `phase = FMod(tick, TICKS_PER_DAY)`,`isNight = FMod(phase − NIGHT_START, TICKS_PER_DAY) < NIGHT_SPAN`,对任意 tick ∈ [0, 2^62] 成立(性质测试,不锁具体值)
- [ ] 环绕段判定正确:NIGHT_START + NIGHT_SPAN 越过日内边界时朴素区间比较会漏判,FMod 式实现无漏判(AC-5-02/03)
- [ ] `season_index = FMod(FDiv(tick, TICKS_PER_SEASON), SEASONS_PER_YEAR)` 单调推进且年尾回绕;5 只输出整数 `season_index`,季节效果表(17 的 SEASON_MULT)不出现在 5
- [ ] `TICKS_PER_DAY` 全仓单一定义点在 5,52 经引用消费;构建期一致性断言(副本 = 构建失败,AC-5-05 / DC-3)
- [ ] 非法负 tick / 零参数 / 未装载 WorldSeed 场景全部落入装载期硬失败通道(`throw`,非 `Debug.Assert`)
- [ ] C# `%` 截断陷阱探针:`FMod(-1, 5) = 4`(AC-5-20)

---

## Implementation Notes

1. `FMod` 为 `Sim` 内部门函数(或住 `Sim.Contracts`),整型签名 `(long a, long n) → long`,禁 `MathFloor` 浮点版。
2. 时间推导 = 纯函数 `(WorldSeed 无关) f(IReadonlyTickProvider, TimeParams) → {phase, isNight, season_index}`;`ITickProvider` 读侧接口注入,便于 EditMode 伪造 tick 序列。
3. 输出以只读快照 / DTO 暴露(整数语义),消费方(18 / 17 / 52 / 42 / 44)各自读取,5 不结算任何玩法(TR-timeweather-009)。
4. TICKS_PER_DAY 单一定义 = `public const`(或烘焙参数)单一出处 + CI 扫描禁止第二份字面量副本。
5. 数值(TICKS_PER_DAY 等具体值)归用户数值轮 —— 本 story 只建机制与合法性断言。

---

## Out of Scope

- [Story 002]: 天气块哈希掷骰(WorldSeed / IEventAuthority 路径)
- [Story 003]: EnvMod_raw 环境修正输出与无钳制边界
- [Story 004]: 呈现读数通道、跨平台重放、[L] 走查项

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 合法参数集 + tick = 0 | 求 phase / isNight / season_index | phase=0,isNight 按区间判定,season_index=0 |
| TC-2 | NIGHT_START + NIGHT_SPAN > TICKS_PER_DAY(跨日环绕配置) | 遍历一个完整日的每个 tick | 夜判定与直观点逐 tick 一致,环绕段无漏判 |
| TC-3 | tick = −1 / Long.MinValue 附近 | 求 FMod | 结果 ∈ [0, n),无负余数(AC-5-20) |
| TC-4 | TICKS_PER_DAY = 0 或 SEASONS_PER_YEAR = 1 | 装载参数 | 构建 / 装载期 `throw`,无 null 兜底(AC-5-14) |
| TC-5 | 同一参数集跑 10^6 伪随机 tick | 比较两次运行 | 逐位相同(纯函数确定性) |

**Edge cases**: 日末 / 年尾回绕 tick;参数缺项;52 侧引用副本漂移(构建期断言须拦下)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/TimeWeather/time_base_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: ADR-005 `ITickProvider` 抽象点(Boot 场景 tick driver,已就位);ADR-014 烘焙参数装载(`time_*.cooked`)
**Unlocks**: Story 002(天气掷骰以 block(tick) 为键)· Story 003(EnvMod 的时间分量)· 52 的 TICKS_PER_DAY 引用点

---

## Completion Notes

*(empty at creation)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `TimeParams` — 时间参数（TICKS_PER_DAY / TICKS_PER_SEASON / SEASONS_PER_YEAR / NIGHT_START / NIGHT_SPAN）
- `TimeBase` — 时间基准（FMod / FDiv / ComputePhase / IsNight / ComputeSeasonIndex）
- `TimeState` — 时间状态快照
- FMod 环绕安全（AC-5-20）
- 非法参数硬失败（AC-5-14）
- 测试: 10 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/TimeWeather/time_base_test.cs` — 10 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，3 BLOCKING gaps 全部修复：
- Gap 1: AC-5-06 triple fixture + boundary tests + reverse assertion
- Gap 2: AC-5-11 NIGHT_SPAN ≥ TPD 和 TICKS_PER_SEASON = 0 测试
- Gap 3: AC-5-20 int64 边界 + n=1 + C# % reverse sentinel

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
