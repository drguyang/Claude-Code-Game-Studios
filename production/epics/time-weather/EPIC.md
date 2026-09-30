# Epic: 时间与天气

> **Layer**: Foundation → Core → Feature
> **GDD**: design/gdd/time-and-weather.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 呈现消费侧(42/44)
> **Status**: In Progress (1/5 stories complete)
> **Stories**: 5 stories — see table below

## Overview

时间与天气(5)是全案的**环境读数器**:它只从 `ITickProvider` 的 tick 与存档头 `WorldSeed` 纯函数地派生昼夜相位(`FMod` 环绕安全式,C# `%` 截断陷阱由自实现 `FMod` 消灭)、季节索引(只出 `season_index` 整数,SEASON_MULT 曲线归 17)、天气 `(kind, intensity)`(块哈希 R-5-A,块内恒定、跨块跳变、无时间积分 B-3/B-8)与环境乘子 `EnvMod_raw`(5 侧**零钳制**,负值原样传递,钳制归 21a F1)。三态分类下天气与昼夜全部是**派生态** —— 不进三流、不存档、重建期重算;5 不结算任何玩法,由 18 / 17 / 52 / 42 / 44 各自消费读数。全部数学在整数定点域(Q16.16,int64,除法全整除,单一舍入 ties-away-from-zero),跨平台逐位一致挂 ADR-012 黄金夹具(EXTERNAL)。`TICKS_PER_DAY` 单一定义点在 5、52 引用(副本 = 构建期硬失败)。天气对移动的影响 P0 明令不启用(AC-5-08)。逐参数数值(TICKS_PER_DAY / WEATHER_BLOCK_TICKS / 权重表 / ENV_MOD_MIN~MAX)归用户数值轮,机制与合法性断言先行。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟与状态同步模型 | tick 唯一来源 = ITickProvider,5 只读;全部模拟数学整数定点域;主机唯一执行 Step | HIGH |
| ADR-006: 定点域边界数据契约 | FixParse 唯一解析入口;单一舍入 ROUND_HALF_AWAY_FROM_ZERO;存档与事件流禁 float | MEDIUM |
| ADR-007: 事件权威与掷骰状态 | 掷骰走 IEventAuthority;WorldSeed 归 7a 存档头(非 SimEvent);骰子输入可从事件流重构 | LOW |
| ADR-009: 世界状态事件化边界 | 三问判据/三态分类 —— 天气/昼夜/季节 = 派生态,不进流不存档 | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | 天气三元组重放逐位一致挂双级黄金夹具三格矩阵(AC-5-07 EXTERNAL) | HIGH |
| ADR-014: 数据管线与 JSON 解析器 | 时间/天气参数走两阶段烘焙(Fix 字段 JSON 写字符串);装载期硬失败 | MEDIUM |
| ADR-015: 世界几何 | EcozoneOf 消费 6 的烘焙整数几何;5 不定义生态区字面量 | LOW |
| ADR-024: 三流 Kind 单一登记真源 | 5 零写入 Kind —— 白名单断言「5 不产任何 SimEvent」的落点 | LOW |

**Engine Risk**: **HIGH**(承 ADR-005/012)。5 本体为纯整数逻辑(门 A 程序集,LOW、零 post-cutoff API);风险集中在跨平台逐位对拍(IL2CPP vs Mono,int64 回绕谱系 ADR-012 F7)—— 判据可写、执行须 CI 矩阵,本机不可跑时记 NOT-RUN,禁借绿。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-timeweather-*`,Manifest Version 2026-09-21)。
> 计数(实测):**15 条 = 11 covered + 2 partial + 2 gap**;gap = TR-006(TICKS_PER_DAY 单一定义)/ TR-007(FMod 环绕判定);partial = TR-008(钳制归 21a 的接缝)/ TR-015(深水线季节曲线归 17)。
> Story 拆解不等待数值轮;TR-013 的执行挂 ADR-012 CI 矩阵(EXTERNAL)。

| TR-ID | Requirement(摘) | ADR Coverage |
|-------|-------------|--------------|
| TR-timeweather-001 | tick 唯一来源 = ITickProvider;5 只读零写 | ADR-005 ✅ |
| TR-timeweather-002 | 生态区气候属性由 6 定义、5 消费 | ADR-015 ✅ |
| TR-timeweather-003 | 天气 = (WorldSeed, 块(tick), EcozoneOf) 纯函数,派生态 | ADR-009+007 ✅ |
| TR-timeweather-004 | 掷骰统一 IEventAuthority;零 UnityEngine.Random | ADR-007 ✅ |
| TR-timeweather-005 | WorldSeed 归存档头,不占 SimEvent | ADR-007+010 ✅ |
| TR-timeweather-006 | TICKS_PER_DAY 单一定义点,52 引用,不一致=构建失败 | gap(story 001 落机制) |
| TR-timeweather-007 | FMod 环绕安全的昼夜判定 | gap(story 001 落机制) |
| TR-timeweather-008 | EnvMod 负值原样传递,钳制归 21a F1 | ADR-006 partial(story 003) |
| TR-timeweather-009 | 5 不结算玩法,只产乘子与读数 | ADR-009 ✅ |
| TR-timeweather-010 | 天气对移动影响 P0 不启用;P1a 走 K_TERRAIN 单通道 | ADR-020 ✅ |
| TR-timeweather-011 | 未驻留 chunk ⇒ 全局默认档,禁「最近区」 | ADR-015+014 ✅ |
| TR-timeweather-012 | F-5.1/5.2/5.4 零浮点,除法全整除,DTO 递归无 float | ADR-006 ✅ |
| TR-timeweather-013 | 同 (WorldSeed,tick,cell) 跨平台重放逐位相同 | ADR-012 ✅(EXTERNAL) |
| TR-timeweather-014 | 参数装载期硬失败,禁 null 兜底 | ADR-014 ✅ |
| TR-timeweather-015 | 深水线季节修正曲线归 17,5 只出 season_index | partial(story 003 留出口) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/time-and-weather.md`(AC-5-01…21)are verified —— 含 4 条 [L] 走查(AC-5-09/10/18/21)与 2 条 EXTERNAL(AC-5-07/21)
- All Logic and Integration stories have passing test files in `tests/`(或 `unity/Assets/Tests/`)
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`
- 数值轮解冻前,机制与合法性断言全部就位;逐参数值不作为本 epic 的阻塞项

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 时间基准 —— tick 只读与昼夜 / 季节相位 | Logic | Ready | ADR-005/006/014 |
| 002 | 天气纯函数 —— 块哈希掷骰与生态区查表 | Logic | Ready | ADR-007/009/015 |
| 003 | EnvMod_raw 环境修正输出 —— 原样传递、5 侧无钳制 | Logic | Ready | ADR-006/005-G |
| 004 | 消费边界与确定性对拍 —— 不进流、天气不改移动、跨平台逐位 | Integration | Ready | ADR-012/009/020 |
| 005 | 天空与体感读数呈现([L] 走查) | Visual-Feel | Ready | ADR-013/018 |
