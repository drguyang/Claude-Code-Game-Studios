# Epic: 病人 AI 与行为

> **Layer**: Foundation → Core → Feature
> **GDD**: design/gdd/patient-ai.md
> **Architecture Module**: 边界层(呈现侧)· **不在门 A sim 程序集内**(ADR-016 §一 补注)
> **Status**: ✅ **4/4 story 全闭**(001 `4076e1e` · 002 `6e0d178` · 003 `a1f7a36` · 004 `0b6f948` 2026-10-05 实现收口)· **story-004 评审原件已于 2026-10-07 补做落库**(`review-patient-ai-story-004-2026-10-07.md`:双代理单轮 → 修复 → 复跑绿 175/173 + 全量 2955/2908/0 红 ⇒ 004 转 Complete,治理缺口闭)
> **Stories**: 4 stories — see table below

## Overview

病人 AI(13)是瘟疫的**行为面**:它把 9 的体征读数(`IVitalsQuery.GetVitals() → VitalsDto{position, trend, signs[]}`,全案唯一浮点出口)映射为病人「做什么」—— 游荡、寻医、卧倒、接受治疗、终态 —— 并把行为与体征投影视给 37 立案、42 呈现、44 发声。承重裁定(ADR-016 §一 补注 + ADR-027):**13 的决策住边界层(呈现侧),物理上不可能住门 A 的 sim 程序集**(它消费 float DTO);决策 = **派生态**,不进三流、不存档,加载/重放期由三源(事件流/版本化烘焙数据/二者纯函数)确定性重建;行为分化**只**来自 `position`×`trend`(滞回带防抖),`signs[]` 只喂材质/表现,**永不进决策**(反幻想「读数病人」的机械护栏);两条物理干预写路径(查体诱发痉挛、搬运昏迷病人)**皆归 10**,13 零写。联机(V8):13 只在主机求值,客户端不重算,病人位姿走 ADR-001 第二 QoS。模拟范围 = 在场病人(9 的 OQ-8 已裁:CAP=24 硬上限归 9,13 只消费在场判定)。`BEHAVIOR_BAND_*` / `PERCEPT_R` / `PATIENT_SPEED` / `CUE_INTERVAL` 等数值归用户数值轮,机制与合法性断言先行。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-016: AI 架构 | **权威件**。§一 分层:13 决策住边界层(例外注);三源不变量;§三 感知粗粒度整数格禁读表现态;§五 整数导航格;§六 13↔37 单向(IPresentPatients);§九 LOD/冻结修订(积分量证伪「冻结不改判定」) | MEDIUM→LOW(表现层 NavMesh 成本一处) |
| ADR-027: 13 的写路径归属 | 痉挛/搬运两写路径皆归 10;三段式 + ADR-020 §四 对称;13 只出表现;新 Kind 归 10 的 GDD 轮 | LOW |
| ADR-005: 确定性模拟 | VitalsDto 经门面浮点出口;tick 驱动;门 A 程序集边界 | HIGH |
| ADR-006: 定点域边界 | 烘焙行为数据(空间整数格/非空间 Fix)禁 float;id 高水位 Amendment B | MEDIUM |
| ADR-009: 世界状态事件化边界 | 决策 = 派生态不进流;效果(经 9/10)进流 | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | float DTO 不逐位 ⇒ 13 联机只在主机跑;决策轨迹哈希进集成级夹具(EXTERNAL) | HIGH |
| ADR-014: 数据管线 | `ai_patient.json`(含 MaterialTable/CUE_INTERVAL)两阶段烘焙 | MEDIUM |
| ADR-015: 世界几何 | LogiPose = WorldPos 整数格;HomeRegion=EcozoneOf(spawn_anchor) | LOW |
| ADR-017: 不引入 DOTS | sim 侧结构性排除;13 住边界层同样不引 ECS(预算表 O-13-6 归 ADR-016 下轮) | LOW |
| ADR-018: 音频架构 | cue = AudioCueDto 整数语义;无提示音铁律(AC-44-09 白名单);死亡=呼吸层消失非播报 | MEDIUM |
| ADR-013: 拟物 UI 框架 | 13 零玩家可见 UI;`PresentationDtoGuard` 递归扫描,disease_id 不进呈现 | HIGH |
| ADR-021: POI 状态所有权 | 读 6 的静态定义(spawn_anchor),不读 POI 状态 | LOW |

**Engine Risk**: **MEDIUM**(本体 LOW,风险集中在三处外挂):① IL2CPP/float 逐位面(ADR-012,EXTERNAL,CI 产物为证)② 45 第二 QoS 与联机「客户端零重算」(P1b,P0 以桩+静态断言)③ 10 的新 Kind 义务(归 10 GDD 轮)。13 本体为纯 C# 边界层逻辑,零 post-cutoff API。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-patient-*`,Manifest Version 2026-09-21)。
> 计数(实测):**24 条 = 21 covered + 3 gap**(gap = TR-005 band/九态阈值对齐(随数值轮)· TR-023 调试视图与 UI(由 Story 003 落断言)· TR-024 无障碍(由 Story 003 落 AC-13-F1…F3)—— TR-005 无 ADR 属数值域对齐,不阻塞机制)。
> ADR-027 兑现 TR-021/022;ADR-016 兑现 TR-001…020 主干。

| TR-ID | Requirement(摘) | ADR Coverage |
|-------|-------------|--------------|
| TR-patient-001 | 13 只读不写,零三流事件 | ADR-016 ✅ |
| TR-patient-002 | 取数唯一入口 IVitalsQuery→VitalsDto | ADR-005+016 ✅ |
| TR-patient-003 | DTO 只含 position/trend 原始量 | ADR-016 ✅ |
| TR-patient-004 | 不重建九态,只用 BEHAVIOR_BAND_* | ADR-016 ✅ |
| TR-patient-005 | BAND 与九态阈值对齐 | gap(数值轮,story 001 登记) |
| TR-patient-006 | 模拟范围 = 在场病人,不自建在场 | ✅ |
| TR-patient-007 | 决策 = 派生态,住边界层重建 | ADR-009+016 ✅ |
| TR-patient-008 | 重建三源不变量,禁第四来源 | ADR-016 ✅ |
| TR-patient-009 | 感知 = 粗粒度整数格,禁 sqrt/Transform | ADR-016+020 ✅ |
| TR-patient-010 | 空间量一律 WorldPos,数据禁 float | ADR-015+014 ✅ |
| TR-patient-011 | 表现映射归 13;8/9/13 三权不互窜 | ADR-016 ✅ |
| TR-patient-012 | IPresentPatients 只读视图,不引用 37 | ADR-016 ✅ |
| TR-patient-013 | 出现率上限归 9,不生成/删除病人 | ADR-016 ✅ |
| TR-patient-014 | id 经 IIdAuthority 与敌人共空间 | ADR-006+016 ✅ |
| TR-patient-015 | 行为载体烘焙,运行期零第三方 BT | ADR-016+014 ✅ |
| TR-patient-016 | sim 整数导航格,NavMesh 仅表现 | ADR-016+015 ✅ |
| TR-patient-017 | LOD 节流/冻结三条现行口径 | ADR-016 ✅ |
| TR-patient-018 | cue 契约 AudioCueDto,无状态语义 | ADR-018 ✅ |
| TR-patient-019 | 昏迷/死亡靠呼吸层+姿态,禁硬报 | ADR-018 ✅ |
| TR-patient-020 | 呈现 DTO 受 PresentationDtoGuard 递归 | ADR-013 ✅ |
| TR-patient-021 | 查体诱发痉挛写路径 | ADR-027 ✅ |
| TR-patient-022 | 搬运昏迷病人写路径 | ADR-027 ✅(022 为 P0 不实现,只登记方向) |
| TR-patient-023 | 无玩家可见 UI;调试视图隔离 | gap(story 003 落断言) |
| TR-patient-024 | 听障/视障可及性 | gap(story 003 AC-13-F1…F3) |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/patient-ai.md`(AC-13-A/B/C/D/E/F 组,29 条,[B]24+[A]5)are verified —— [L] 走查与 EXTERNAL 项按各自通道 SIGN-OFF / CI 产物,禁借绿
- BLOCKED-BY 外部项(10 的 Kind、45 的第二 QoS、数值轮)以契约测试与登记面收口,不冒充绿
- 零写门(AC-13-A1)与三源白名单(AC-13-E 组 + Story 004 反射断言)常绿

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 体征只读消费与行为映射 —— VitalsDto 取数、双维状态与滞回 | Logic | **Complete ✅ 2026-10-04**(`4076e1e`) | ADR-016/027/005 |
| 002 | 空间行为 —— 感知格距、HomeRegion 寻医与定点累加器步进 | Logic | **Complete ✅ 2026-10-05**(`6e0d178`) | ADR-016/015/006 |
| 003 | 呈现投影 —— ViewState 优先级、IPresentPatients 与 cue / Material 通道 | Integration | **Complete ✅ 2026-10-05**(`a1f7a36`) | ADR-016/013/018 |
| 004 | 重建、联机单跑与写路径归 10 —— 端到端确定性与接缝验收 | Integration | **Complete ✅ 2026-10-07** —— 实现收口 2026-10-05(`0b6f948`)+ 补做评审原件 `review-patient-ai-story-004-2026-10-07.md`(15+12 条修复,复跑绿) | ADR-027/016/012 |
