# Epic: 遥测与分析

> **Layer**: Foundation
> **GDD**: design/gdd/telemetry-analytics.md
> **Architecture Module**: L4 边界层(呈现侧)· EDGE(`JudgmentMetrics` 本地只读重算)
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories telemetry-analytics`

## Overview

遥测与分析(51)是全案唯一回答「判断层到底好不好玩」的仪器(绑定支柱一)。
ADR-019 的核心裁决 = **回放即完整数据记录**:确定性模拟本身已是一台完整记录仪,
想测的每个量真源都已存在于三流 ⇒ 51 不埋点、它是**读数器不是采集器** ——
只读订阅病史 / 病例 / 世界三条逻辑流,把判断准确率 / 误诊分布 / 跳过率 / 熟练度成长 /
难度曲线聚合成**本地**指标,输出到本地文件与 DevBuild-only 开发者调试视图。
51 住**边界层**(不进 sim 程序集、不写三流、不成为第四个 `IEventSink` 写入者);
**零第三方 SDK · 零出厂数据 · 零写回 `assets/data`**(数值用户自己调);
指标全整数 `(num, den)` 对,无浮点统计路径。**P0 无玩家可见统计 UI**。
GDD Approved(2026-09-18,首轮 NEEDS REVISION 1 BLOCKING + 3 Recommended 全修,免二轮);
TR 8 = 7 covered + 1 partial(005 Fold 黄金夹具载体未建,禁借绿)+ 0 gap;
GDD Requirements Covered by ADRs = 8 / 8;Untraced = None。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-019: 遥测与隐私 | 权威件 —— 回放即完整数据记录(零新埋点);零第三方 SDK / 零出厂数据(AC-19-02 BLOCKING);51 住边界层只读不写;P0 最小切面(重算器 + 开发者视图 + 本地导出);AC-19-07 零写回 `assets/data` | LOW(不引入任何引擎 / 第三方 API) |
| ADR-010: 持久化与存档格式 | 离线重算的载体:读存档 → 还原三流;Fold 折叠单函数与 51 复用同一实现(义务入 §三 义务汇总) | MEDIUM(原子写 / 后台线程 / 哈希须实测;刻意只用 BCL) |
| ADR-025: 契约程序集清单 | 清单封闭性:51 住边界层程序集的清单落点有据可依(未登记 asmdef = 构建失败) | LOW(asmdef 机制 2019 起稳定) |
| ADR-006: 定点域边界契约 | 指标全部为整数 `(num, den)` 对的域边界依据(定点域在统计侧的延伸;分母显式,禁百分比裸数) | MEDIUM(IL2CPP 对整数的转译保证须实测) |

**Engine Risk**: **MEDIUM**(ADR-010 / ADR-006 并列最高,均为 IL2CPP / BCL 行为面,
裁决本身不依赖 post-cutoff API)。ADR-019 为 LOW(零引擎 API);接口层
(`ITelemetrySource` / 指标 schema)为纯 C# 契约,不受引擎版本影响。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-telemetry-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-23 实测):**8 条 = 7 covered + 1 partial + 0 gap + 0 no-adr-by-design**;
> **GDD Requirements Covered by ADRs = 8 / 8**(covered+partial);**Untraced = None**。
> 1 条 partial 为**禁借绿**登记:005 的 Fold 折叠单函数复用纪律**已裁**(ADR-010 + ADR-019),
> 但**黄金夹具载体未建**、断言载体归实现轮(AC-51-A3/C3 自标「回升风险」)—— 不阻塞
> epic 建置,story 按 BLOCKED-BY 处理。
> 剩余 OQ-51(1 调试视图归属 / 4 导出格式 / 5 联机在哪跑 / 7 Steam Cloud · P1a / 9 已裁
> 登记为 10 的 GDD 义务)**均非 ADR 面**,归实现前用户裁,不阻塞本 epic。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-telemetry-001 | 51 只读不写:`ITelemetrySource` 仅为消费者,不是第四个 `IEventSink` 写入者 | ADR-019 ✅ |
| TR-telemetry-002 | 零出厂数据双断言:构建面(无网络依赖)+ 接口面(无 Upload/Send/Post 符号) | ADR-019 ✅ |
| TR-telemetry-003 | 51 住边界层程序集(不进 sim,不污染门 A) | ADR-019 + ADR-025 ✅ |
| TR-telemetry-004 | 判断层指标全部可从既有事件流重算,零新埋点(回放即完整数据记录) | ADR-019 ✅ |
| TR-telemetry-005 | Fold 折叠单函数复用(51 与 7a 共用同一实现;黄金夹具载体未建) | ADR-010 + ADR-019 ⚠️ partial(夹具载体归实现轮,禁借绿) |
| TR-telemetry-006 | 指标全部为整数 `(num, den)` 对(无浮点统计路径) | ADR-006 + ADR-019 ✅ |
| TR-telemetry-007 | P0 交付 = 本地导出文件 + DevBuild-only 开发者视图(无玩家可见统计 UI) | ADR-019 ✅ |
| TR-telemetry-008 | 零写回 `assets/data` 的代码路径(数值用户自己调,51 不代调) | ADR-019 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/telemetry-analytics.md` are verified(52 条 AC · BLOCKING 42 · ADVISORY 10)
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories telemetry-analytics` to break this epic into implementable stories.
