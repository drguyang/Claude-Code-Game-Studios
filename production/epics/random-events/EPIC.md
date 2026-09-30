# Epic: 随机事件导演

> **Layer**: Core(sim 侧 · 门 A 程序集 `Sim`)
> **GDD**: design/gdd/random-events.md
> **Architecture Module**: L2 Sim(确定性模拟 · 病史流 + 世界流写入者)+ 表现层预告线索通道(42/44)
> **Status**: Ready
> **Stories**: 6 stories created(2026-09-28 `/create-stories`)

## Overview

随机事件导演(52)是世界的**编剧而不是骰子**:四轴管线(时机 → 抽池 → 强度定档 → 密度预算 → 生成)
全部在整数定点域内确定性求值 —— RNG 只有 **SplitMix64**(禁 `UnityEngine.Random` / `System.Random`),
每个掷骰的输入**可从事件流重构**(`EventRolled` 逐抽取记录),掷骰权经 **`IEventAuthority`**
(第六个 P0 抽象点,ADR-007;P0 本地占位 = 主机)。52 **零内容字段**(医学内容归 37,类型引用扫描
AC-52-03),池条目只有整数 key / `W_base:int`(D-21-17 口径,非 Fix)/ 档枚举 / 锚点枚举 /
触发方式枚举。预告制无直降路径;避险是一等公民(预告窗内离开 = 零损失);密度预算硬顶 +
唯一例外 `DeferredThreatSlot`(威胁档 + 在医馆 + 同日,有界、跨日作废、进流持久)。
F3 伤害/重建 = P1a 冻结;P0 医馆不可损毁是构建期事实。**构建期拒收表(GDD 自陈 17、实测 18 谓词,
ADR-024 §⑥)执行体归 ADR-014 阶段 2**,本 epic 只挂载 48/02 侧输入。
逐条数值(档占比 / `WINDOW_SIZE` / `BASE` / `SPAWN_AHEAD_DIST` / 各 cap)归用户数值轮 ——
机制与值域冻结,`TICK_SECONDS = 0.05`(20 Hz)与 `PATIENT_APPEARANCE_CAP = 24` 已裁(OQ-8 / OQ-25-8)。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟 | 全部模拟数学整数定点(Q16.16);病史流唯一真源;主机唯一执行 Step;`ITickProvider` 驱动(禁墙钟) | HIGH |
| ADR-006: 定点域边界 | `FixParse` 唯一解析入口;`ROUND_HALF_AWAY_FROM_ZERO` 单一舍入;存档/流禁 float;`weight` 类 int 计数移出 Fix 解析集(D-21-17) | MEDIUM |
| ADR-007: 事件权威与掷骰状态 | **`IEventAuthority` = 第六个 P0 抽象点**;`WorldSeed` 归 7a 存档头(非 SimEvent);掷骰每个输入可从流重构;五 Kind(`EventRolled`/`EventArrived`/`ThreatDeferred`/`ThreatDeferralCleared`/`HistoryFlagChanged`);世界级事件 `PatientId.None = -1` 不污染高水位 | LOW |
| ADR-008: 病例流 | 跨流全序键 `(Tick, StreamPriority, Patient, Seq)`;52 与病例流交互仅经 37 定义的三案链触发 | HIGH |
| ADR-009: 世界流(第三条流) | 事件化边界三问;世界流不折叠 + 有界性论证;52 的世界侧效果落流 | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | 黄金夹具矩阵(SplitMix64 / 定点乘单元哈希);IL2CPP 有符号溢出 UB 须 hi/lo 无符号拆分(承 ADR-005 Amendment G) | HIGH |
| ADR-014: 数据管线 | 池/档表作者态 JSON → `.cooked`;Fix 旋钮 JSON 写字符串;ScriptableObject 承载 = 构建错误(AC-52-09) | MEDIUM |
| ADR-015: 世界几何 | `spawn_anchor` 解析 = 烘焙逻辑层整数查询(非 NavMesh / 非视觉层);出诊路径 = 整数格判据 | LOW |
| ADR-024: Kind 单一登记真源 | 五 Kind 已在 `entities.yaml` 具名;kindgen 断言 A1–A5;构建期拒收表执行体归 ADR-014 阶段 2 | LOW |
| ADR-017: DOTS 裁定 | sim 侧结构性排除(门 A);52 程序集 `"noEngineReferences": true` **尚未声明** —— Story 001 前置义务 | LOW |

**Engine Risk**: **HIGH**(ADR-005 逐位性 + ADR-012 矩阵)。52 机制面为纯 C# 整数算术
(LLM 知识风险低,刻意不用 post-cutoff API),但 IL2CPP 溢出回绕(AC-52 组 + F7 spike)与
黄金夹具刷新归 ADR-012 三格矩阵 —— 判据在纸面,**实测未跑(禁借绿)**。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-randomevents-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-21 D-R3 回填实测):**32 条 = 15 covered + 2 partial + 15 gap**;
> **GDD Requirements Covered by ADRs = 17 / 32**;**Untraced(gap)= 15**。
> ⚠️ 多数 gap 属「登记不立件」类(判据在 52 GDD 自身 AC / ADR-014 阶段 2 执行体):
> `TR-randomevents-003`(Hamilton 配额)/ `006`(KeyGate)/ `008`(CDF walk)/ `011`(池/档表)/
> `014`(冷却去重)/ `015`(性能)/ `016`(流体积)/ `017`(五边界)/ `020`(注入形状)/
> `022`(出诊/医馆判据)/ `023`(可感知门槛)/ `025`(本地化)/ `026`(联机)/ `027`(死亡复活)/
> `030`(注入接口)—— 均由本 epic stories 以 GDD-AC 为判据落地,不阻塞 story。
> `019`(预告线索通道)/ `021`(强度轴)= partial。**承重问题**(37 玩测门)另轨,不在 story 面。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-randomevents-001 | 池条目 schema 全整数(五字段) | ADR-006+014 ✅ |
| TR-randomevents-002 | 档枚举闭集 {威胁,机会,反应,灾难} | ADR-009 ✅ |
| TR-randomevents-003 | Hamilton 最大余额配额拆分 | — ❌ gap(登记不立件) |
| TR-randomevents-004 | SplitMix64 唯一 RNG | ADR-007 ✅ |
| TR-randomevents-005 | 掷骰输入可从流重构(EventRolled) | ADR-007 ✅ |
| TR-randomevents-006 | KeyGate 历史旗标门 | — ❌ gap(no_adr) |
| TR-randomevents-007 | WorldSeed 归存档头 | ADR-007 ✅ |
| TR-randomevents-008 | CDF walk 按 key 升序 | — ❌ gap(登记不立件) |
| TR-randomevents-009 | IEventAuthority 第六抽象点 | ADR-007 ✅ |
| TR-randomevents-010 | PatientId.None 哨兵 | ADR-007 ✅ |
| TR-randomevents-011 | 池/档表外部化烘焙 | — ❌ gap(ADR-014 执行体) |
| TR-randomevents-012 | 禁墙钟,tick 经 ITickProvider | ADR-005 ✅ |
| TR-randomevents-013 | 定点域全管线(零 float) | ADR-005+006 ✅ |
| TR-randomevents-014 | 冷却去重窗口 | — ❌ gap |
| TR-randomevents-015 | 性能预算(GC.Alloc=0 / P99) | — ❌ gap |
| TR-randomevents-016 | 事件流体积有界性 | — ❌ gap(承 ADR-009 §六) |
| TR-randomevents-017 | 五边界条件(Σ=0 等) | — ❌ gap |
| TR-randomevents-018 | 预告制无直降 | ADR-009 ✅ |
| TR-randomevents-019 | 预告线索零数值通道 | — ⚠️ partial |
| TR-randomevents-020 | 注入形状 | — ❌ gap(no_adr) |
| TR-randomevents-021 | 强度轴输入闭集 | — ⚠️ partial |
| TR-randomevents-022 | 出诊/医馆判据(布尔非几何) | — ❌ gap(no_adr) |
| TR-randomevents-023 | 可感知门槛 | — ❌ gap |
| TR-randomevents-024 | 52↔51 边界 = 三流本身 | ADR-019 ✅ |
| TR-randomevents-025 | 本地化(线索文本) | — ❌ gap |
| TR-randomevents-026 | 联机读队态 / 不抬预算 | — ❌ gap(45 轮) |
| TR-randomevents-027 | 死亡复活与事件窗 | — ❌ gap |
| TR-randomevents-028 | dwell time 永不入输入 | ADR-005 ✅ |
| TR-randomevents-029 | 避险一等公民 | ADR-009 ✅ |
| TR-randomevents-030 | 注入接口(脚本条目通道) | — ❌ gap |
| TR-randomevents-031 | spawn_anchor 确定性解析 | ADR-015 ✅ |
| TR-randomevents-032 | 表现层通道(42/44) | ADR-013+018 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All P0 acceptance criteria from `design/gdd/random-events.md`(AC-52-01 … AC-52-48,除标 P1a 的 33/34/35 与 M1 玩测门)are verified
- All Logic and Integration stories have passing test files in `tests/` / `unity/Assets/Tests/`
- ADR-012 黄金夹具矩阵对 SplitMix64 / 定点乘已刷新并三格常驻绿(实测未跑 ⇒ epic 不得收口,禁借绿)
- 52 程序集 `"noEngineReferences": true` + 引用集 = BCL 白名单断言入构建(Story 001 前置义务)
- ⚠️ F3 伤害/重建(P1a)、联机队态(P1b)、37 玩测承重门 —— 全部**不在**本 epic DoD

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 池条目 schema、注入接口与构建期拒收表 | Logic | **Complete ✅ 2026-09-30** | ADR-006/014/024/017 |
| 002 | 确定性掷骰与流登记(DC-1/DC-3) | Logic | Ready | ADR-007/005/012 |
| 003 | F1 抽取管线:配额、上下文门与强度轴 | Logic | Ready | ADR-005/006/009 |
| 004 | F2 密度预算与 DeferredThreatSlot | Logic | Ready | ADR-005/007/009 |
| 005 | 预告制、避险与因果可见 | Integration | Ready | ADR-009/013/018 |
| 006 | spawn_anchor 解析与事件状态机 | Integration | Ready | ADR-015/007/016 |
