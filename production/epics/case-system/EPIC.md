# Epic: 病例系统

> **Layer**: Feature
> **GDD**: design/gdd/case-system.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 病例流(第二逻辑流)+ Presentation(只读 DTO 侧)
> **Status**: Ready
> **Stories**: 6 stories — see table below

## Overview

病例系统(37)是「病例」这种东西的宿主 —— 玩家实际**从事**的游戏单元:接病人 → 看体征 → 在脉案上落(或空)一个病名 → 施治 → 显式结案。它把 9 的病史流包装成可开/可结的容器(病例只走两态 开/结,与病人九态**正交** —— 死亡不是病例的转移条件),并持有**支柱四「拼出来的图样」在 P0 的唯一载体**:F-37.1 同源检测 —— 判「结案」不判「诊断正确」,`FirstPerPatient(D)` 筛选使成员互异结构性成立,第 `PATTERN_THRESHOLD`(P0=3)例结案时发一条世界级 `PatternRecognized`(哨兵 `PatientId.None`,载荷 `salted_key = SplitMix64(WorldSeed,"case-salt",ordinal(D))` + `anchor_case` + `member_set`,冻结不重发)。承重 ADR-008:**病例流 = 第二条逻辑流**,五 Kind(`CaseOpened`/`CaseClosed`/`PatternRecognized`/`JudgmentRecorded`/`JudgmentRevised`)经 `entities.yaml` + kindgen 路由;`case_id = (Tick,Patient,Seq)` 三元组;跨流全序 `(Tick,StreamPriority,Patient,Seq)`;处置证据窗口 `[CaseOpened.Tick, CloseTick]`(**2026-09-17 方向订正**,复诊后门在公式层关闭)+ `treated`/`disease_set` 快照进 `CaseClosed`;病例流不物理折叠。规则九守密:`disease_id` 类型层面不得进 39/42/48 DTO(`PresentationDtoGuard` 递归扫描,AC-37-15 唯一落点;保密 = player 不可见非 client 不可知);第六泄漏面(玩家自书病名分组/词表一一对应)由 AC-37-24/35 + 烘焙期非同一性校验堵。37 全 int 零 Fix 零浮点数值、**P0 不发任何数值奖励**(AC-37-32 反射断言)。⚠️ 未裁项:**W-1**(三案链纯伤情 vs D 域口径 → 须 9+37 会签二选一,不裁不得实现 ScriptedChain 联调)、D-37-B(病史流有界性新增行为学前提须 9/7a 登记);36 条 AC 多为负存在断言,载体缺失时记 NOT-RUN 禁空集绿。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-008: 病例事件流(第二条逻辑流) | 按 Kind 路由;跨流全序键;「已处置」= 处置事件为证 + 窗口化快照;盐 = WorldSeed 派生(防御纵深);病例流不折叠 | HIGH |
| ADR-005: 确定性模拟 | 病例状态 = 流前缀纯函数;主机唯一 Append;SimEvent/Seq 复用 | HIGH |
| ADR-006: 定点域边界 | ordinal/枚举为整数计数,**移出 Fix 解析集**(FixSet 之错的裁定源);存档禁 float | MEDIUM |
| ADR-007: 事件权威与掷骰 | `PatientId.None = -1` 世界级哨兵,不污染高水位 | LOW |
| ADR-009: 世界状态事件化边界 | 病例 = 模拟态进流判据(三问);三流并集 max(id) 重构 | MEDIUM |
| ADR-010: 持久化与存档格式 | 病例流序列化;`Folded(p)` 含「无未结案病例」(7a 侧);非拒载口径 | MEDIUM |
| ADR-013: 拟物 UI 框架 | PresentationDtoGuard 递归扫描(AC-37-15 机制);42 承载走查;quill_tick 只读 | HIGH |
| ADR-014: 数据管线 | `case_judgment_lexicon` 烘焙 + 非一一对应校验 + ordinal append-only 基线门;ConfigVersion 覆盖集 | MEDIUM |
| ADR-024: Kind 单一登记真源 | 五 Kind 入 entities.yaml(stream/author/payload_schema);kindgen A1–A5 | LOW |

**Engine Risk**: **HIGH**(ADR-008/005 的刻意不用 post-cutoff API 但验证面挂 IL2CPP 逐位与折叠语义实测;本 epic 机制层为纯 C# 契约,风险不阻塞实现,复跑归 ADR-012 矩阵批)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-case-*`,共 36 条,与 GDD 36 条 AC 一一对应)。
> 计数(2026-09-28 实测):**36 条 = 26 covered + 6 partial + 4 gap**(gap = TR-case-012 立案两路径 / 013 串接语义 / 016 结案出口 / 018 同源算法 / 020 不拥有清单 / 027 图样可辨识度 —— 其中 012/013/016/018 的**机制层**由本 epic stories 兑现,ADR 侧无需新裁决;027 归 playtest)。partial 主因 = 借绿降级(TD C4)与 53 实现轮。

| TR-ID(代表) | Requirement(摘要) | Status |
|-------|--------------------|--------|
| TR-case-001…011 | case_id 三元组 / 病例流 / 不折叠 / 全序键 / 五 Kind 载荷 / 哨兵 / 判断事件化 | covered(ADR-008 主体) |
| TR-case-012 | 规则二:立案两条路径 | gap —— story-002 兑现机制 |
| TR-case-013 | 规则三:串接语义 | gap —— story-002/006(呈现归 39) |
| TR-case-016 | 规则六:结案唯一出口、显式、单例、不可撤销 | gap —— story-003 |
| TR-case-018 | 规则八:同源检测算法 | gap —— story-004 |
| TR-case-020 | 规则十:不拥有清单 | gap(边界负断言)—— story-005 |
| TR-case-027 | 图样可辨识度 playtest | gap —— 出本 epic(内容/playtest 轮) |
| TR-case-028…033 | ordinal/append-only/词表/ConfigVersion/ScriptedChain/开案唯一 | covered/partial 见注册表 |
| TR-case-034 | quill_tick 进判定记录 DTO | covered(ADR-013) |
| TR-case-035/036 | 「37 沉默≠世界沉默」/ 就诊动作词表归属 | partial(53/8 实现轮) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- AC-37-01…36 中可自动化条目全部有通过测试;负存在断言按「闭集点名 + 载体是否已建」纪律执行,载体缺失记 **NOT-RUN** 禁空集绿
- `ScriptedChain(D)` 联调(**W-1**)在 9+37 会签结案前**不出测不关闭**(GDD 明写「不裁不得实现」)
- [V] 走查条目(AC-37-16/17/19)由 39/42 epic 的走查批承接并留档,本 epic 登记移交
- 数值(PATTERN_THRESHOLD 维持 3 / 立案半径 / 就诊触发条件)归用户数值轮;D-37-B 行为学前提已转 9/7a 登记

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 病例流 Kind 登记与病例身份 | Logic | Ready | ADR-008/024/006 |
| 002 | 立案两路径 · 开案唯一 · 判断记录事件化 | Integration | Ready | ADR-008/009/005 |
| 003 | 结案前置:处置证据窗口与快照 | Logic | Ready | ADR-008/007 |
| 004 | F-37.1 同源检测与 PatternRecognized | Logic | Ready | ADR-008/007/012 |
| 005 | 守密纪律:DTO 守卫 · 词表烘焙 · 零奖励断言 | Integration | Ready | ADR-013/014/006 |
| 006 | 重放持久化与跨系统边界义务 | Integration | Ready | ADR-010/005/008 |
