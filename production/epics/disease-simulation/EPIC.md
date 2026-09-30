# Epic: 疾病与伤情模拟

> **Layer**: Foundation(Core sim 模块)
> **GDD**: design/gdd/disease-simulation.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)
> **Status**: In Progress (1/6 stories complete)
> **Stories**: 6 stories — see table below

## Overview

疾病与伤情模拟(9)是全案的确定性真源:病人的全部病情由**整数定点域(Q16.16)**的病程模型驱动,
**病史事件流是唯一真源**,主机唯一执行 `Step` / `CatchUp`,终态折叠后存档仍可从事件流逐位重放。
伤情模型 11 态、病种注册表 R1 schema + 17 条构建期校验、F1 病程求值 / F2 体征投影 / F3 补算 /
F4 九态阈值机 / F5 共病合成五条公式全在 `Sim` 程序集(门 A:`"noEngineReferences": true`,
引用集恰 = BCL ∪ `Sim.Contracts`)。`TICK_SECONDS = 0.05`(20 Hz,OQ-25-8 已裁)、
`PATIENT_APPEARANCE_CAP = 24`、**在场才模拟**(OQ-8 已裁)。8 经 `IVitalsQuery` → `VitalsDto`
(全案唯一浮点出口)读 9,9 侧零 float。ADR-017:sim 侧**永不上 DOTS**(结构性排除)。
逐条数值(曲线参数、半衰期、权重)归用户数值轮冻结未动 —— 本 epic 只裁机制,全部数值以合成
fixture 参数化,test 绿不依赖任何调参。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: 确定性模拟与状态同步模型 | 全部模拟数学在整数定点域(int64 / Q16.16);病史事件流唯一真源;主机唯一 Step/CatchUp;P0 抽象点 ITickProvider / IEventSink / IIdAuthority / IVitalsQuery / SimEvent | HIGH |
| ADR-006: 定点域边界数据契约 | FixParse 唯一解析入口(拒浮点字面量);单一舍入 ROUND_HALF_AWAY_FROM_ZERO;Fix 禁 Unity 内置序列化器;全序键 (Tick, StreamPriority, Patient, Seq);id 高水位 max+1 排除 None=-1 | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | 双级黄金夹具 + 三格常驻矩阵(Linux-x64-Mono / x64-IL2CPP / ARM64-IL2CPP);F7:int64 溢出 IL2CPP 为 UB ⇒ 128 位中间积禁裸乘 | HIGH |
| ADR-014: 数据管线与 JSON 解析器 | 作者态 assets/data/*.json → 两阶段烘焙 *.cooked(Fix = raw long);玩家构建零 JSON 解析器;ConfigVersion u32 | MEDIUM |
| ADR-017: 是否引入 DOTS | sim 侧结构性排除(门 A 与 Unity.Entities 天然冲突);门 A 引用集白名单断言升为构建失败 | LOW |
| ADR-024: 三流 Kind 单一登记真源 | 真源 = entities.yaml;tools/kindgen 断言 A1–A5;9 的白名单「列表外构建期拒绝」 | LOW |
| ADR-025: 契约程序集清单 | Sim 引用集恰 = {BCL, Sim.Contracts};Sim.Codec 承载 Fix 编码器(internal + IVT) | LOW |
| ADR-026: 技能成长定点化 | FixPow/FixSqrt 唯一整数幂实现(9 侧 Exp/插值若涉幂运算同族约束);SkillGrown 折叠豁免 | LOW |

**Engine Risk**: **HIGH**(ADR-005/012 域)。核心判据:定点数学与流逻辑为纯 C#(LOW),但
**跨平台逐位一致**(IL2CPP C++ 有符号溢出 UB vs C# 定义性回绕)必须经三格矩阵实测;
在 ADR-012 S 系列 spike 未跑绿前,所有 AC-1 类判据记 NOT-RUN,**禁借绿**。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-disease-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-28 实测):**22 条**。需求原文以 registry 为准,评审时重读(read fresh at review time)。
> 逐病种数值冻结归用户数值轮;epic 不因数值未定阻塞(Foundation/Core 全部可用合成 fixture 验证)。

| TR-ID | Requirement(摘要,原文见 registry) | ADR Coverage |
|-------|--------------------------------------|--------------|
| TR-disease-001 | Fix struct(Q16.16,int64 内部) | ADR-005/006 ✅ |
| TR-disease-002 | Q32.32 中间乘(hi/lo) | ADR-005 Amendment G ✅ |
| TR-disease-003 | 手写定点 Exp(禁 libm) | ADR-005/012 ✅ |
| TR-disease-004 | SplitMix64 唯一哈希 | ADR-005/012 ✅ |
| TR-disease-005 | patient_seed = hash 派生,禁 UnityEngine.Random | ADR-007 ✅ |
| TR-disease-006 | ITickProvider 驱动,不由渲染帧驱动 | ADR-005 ✅ |
| TR-disease-007 | 病史事件流唯一真源 | ADR-005 ✅ |
| TR-disease-008 | 五抽象点 + SimEvent 形状 | ADR-005/006 Amendment A ✅ |
| TR-disease-009 | 主机唯一执行 Step/CatchUp | ADR-005 ✅ |
| TR-disease-010 | 终态折叠 | ADR-010 ✅ |
| TR-disease-011 | patient_id 跨权威稳定(机制 A) | ADR-006 Amendment B ✅ |
| TR-disease-012 | 折叠行保留 patient_id | ADR-006 Amendment B ✅ |
| TR-disease-013 | noEngineReferences 门 A 断言 | ADR-017/025 ✅ |
| TR-disease-014 | 零 Math.Exp/Pow 类型断言 | ADR-005/026 ✅ |
| TR-disease-015 | AC-1 跨平台逐位一致 | ADR-012 ✅ |
| TR-disease-016 | Exp 位确定 | ADR-012 ✅ |
| TR-disease-017 | F1 病程求值式 | ADR-005 ✅ |
| TR-disease-018 | F4 载荷三元组 | ADR-005/009 ✅ |
| TR-disease-019 | 保守带误差预算 | ADR-005 ✅ |
| TR-disease-020 | 边界扫描单向性 | ADR-005 ✅ |
| TR-disease-021 | 出现率上限配置(CAP=24) | ADR-008 ✅ |
| TR-disease-022 | 128 位中间结果唯一类型 = 手工 hi/lo 两 ulong | ADR-005 Amendment G ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/disease-simulation.md`(AC-1…43)are verified
- All Logic and Integration stories have passing test files(真身 `unity/Assets/Tests/EditMode/DiseaseSimulation/`,账本 `tests/unit/sim/`)
- 门 A 白名单断言与 kindgen 断言在构建期生效(构建失败级,非 Warning)
- 跨平台逐位判据(AC-1 系)在 ADR-012 三格矩阵上实跑;spike 未跑前维持 NOT-RUN,不得以 Mono 单格绿抵 IL2CPP

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 定点数学库与确定性哈希 | Logic | **Complete ✅ 2026-09-30** | ADR-005/006/012/025/026 |
| 002 | 抽象点、SimEvent 与病史流机制 | Logic | **Complete ✅ 2026-09-30** | ADR-005/006/007/010/024 |
| 003 | 注册表 schema、烘焙管线与门 A 护栏 | Integration | **Complete ✅ 2026-09-30** | ADR-014/017/024/025 |
| 004 | F1 病程求值与 F2 体征投影 | Logic | **Complete ✅ 2026-09-30** | ADR-005/006 |
| 005 | F4 九态阈值机与照护杠杆 | Logic | Ready | ADR-005/009 |
| 006 | F3 CatchUp、F5 共病合成与跨平台黄金夹具 | Integration | Ready | ADR-005/012 |
