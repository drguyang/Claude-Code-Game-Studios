# Epic: 急救动作模块

> **Layer**: Core(输入聚合 / 判定)× Feature(模态期交互)
> **GDD**: design/gdd/emergency-procedures.md
> **Architecture Module**: L3 Gameplay + L2 Sim 接缝(判定输入进流,主机权威)
> **Status**: In Review(7/7 story 已实现 —— 007 Complete ✅ 2026-10-03;b6 手搓点已修(九字段/八字段齐备 + 删两个 payload struct 副本);EditMode 0 红 · PlayMode 36/36;✅ **评审原件已落盘**(`qa/evidence/review-emergency-procedures-2026-10-03.md`,补做,不追认原判定);🔴 **但评审查出多条判据空转 ⇒ 不转 Complete**:**AC-10-02/03** 两条 BLOCKING **恒真**(`reading_contract_test.cs:174` 取 `Sim.Contracts` 程序集,断言其名不含 "Input" ⇒ 零扫描)· `SkillMul` 死代码 · JITTER 用 C# 裸 `/`(违 Forbidden)· `DC-5` 仅返回字符串数组无校验体。⚠️ 复核已排除 agent 误报(header `Seq` 传 0 系全库既定占位约定,非缺陷);残留待裁:`载荷 Seq` 占位(归上行链 45);✅ **C4/C5 已闭(2026-10-03)** —— 稳度门真用 `SkillMul`(GDD F-10.2 原文式)· JITTER 改 `ROUND_HALF_AWAY_FROM_ZERO`(此前 C# 裸 `/` 违 Forbidden))
> **Stories**: 6 stories — see table below

## Overview

急救动作(10)是支柱一「判断为先」的手感落点:CPR / 止血 / 包扎等动作以**全整数读数**
`EmergencyReading{action, hold_ticks, edges, edge_ticks[], magnitude}` 进入,`Judge` 为定点纯函数
(三扇门:幅度/节奏/稳度 → `Applied / AppliedWeak / Missed` 枚举,零连续分值)。**ADR-011 Amendment B
改判后的权威路径**:客户端聚合为一条 `EmergencyAttempt` 全整数意图事件 → **可靠通道**上行
(ADR-001 §一之三 裁决一)→ **主机执行 Judge + Append + 发号 Seq**;本地判定降级为**预表现**
(本地不写流,结果以主机为准)。一条动作落**两条**病史流事件(`EmergencyAttempt` 判定输入 +
`EmergencyTreatmentApplied` 效果,ADR-009 Amendment I —— 三段式首次在流层物化两步)。
**`L_input < 50 ms` 是硬性手感预算**,但只测「输入→预表现呈现」路径,**不与 `L_eval`
(主机 Append→体征可见 = 50 ms + 求值次序)合并成端到端**。跳过路径 = `AppliedWeak`
(无障碍开关 ON ⇒ `Applied`),`method=Skip` 不发成长;中止 ≠ 跳过(零事件)。
`SkillMul` 的定义者 = 10(稳度容差乘子,11 复用零第二实现);`result_mul[3]` 已裁
= 烘焙 raw long `{65536, 32768, 16384}`。数值(阈值/系数/`MAG_MAX`)归用户数值轮;
**OQ-10-12 = 写第一行代码前的 2 动作原型门**。VR 模式实现推 P1a(ADR-013 §7)。
**✅ 2026-10-02 用户裁决: 推迟到 P1a** —— P0 目标是验证核心循环，不是手感调优；当前无 VR 设备，无法实测。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-011: 输入架构(+ Amendment B) | action-based 官方路线;急救独立直读通道(<50 ms,不穿 42 UI 事件栈);Amendment B:判定归主机,客户端聚合 `EmergencyAttempt` 上行,本地判定=预表现;「不逐帧同步输入」保留 | HIGH(Input System 6.3 行为须实测;接口层纯 C# 不受影响) |
| ADR-001 §一之三 裁决一 | 上行按语义二分:判定输入类 → **可靠通道**(`EmergencyAttempt` 结清 OQ-10-9);表现态位置 → 第二 QoS | LOW |
| ADR-009 Amendment I | 病史流三新 Kind:`EmergencyAttempt`(10 产出/主机物化)· `EmergencyTreatmentApplied`(10 写)· `DrugTreatmentApplied`(11 写);一条动作落两条事件;有界性 ≤ 每完成动作 2 条 | MEDIUM |
| ADR-005 / ADR-006 | 全整数判定路径;`ROUND_HALF_AWAY_FROM_ZERO`;主机唯一 Append + Seq | HIGH/MEDIUM |
| ADR-012 | F-10.4/F-10.3b 单元级黄金夹具三格逐位(AC-10-04b);F7 溢出前置 | HIGH |
| ADR-013 | 拟物 UI 双栈;`ModalId` 闭集 7 员;`IModalState` 只读「模态摊开」;呈现 DTO 无评价字段(PresentationDtoGuard) | HIGH(焦点桥 spike;假设 6 半可信) |
| ADR-018 | 无提示音铁律:音色/素材/强度不因 `JudgeResult` 而异(AC-44-09 白名单机械化) | MEDIUM |
| ADR-020 §四 | 位移纯表现态;本 epic 消费其「格意图」同构纪律(判距在主机、当下格) | LOW |
| ADR-024 / ADR-025 | 三 Kind 经 entities.yaml + kindgen 入白名单(AC-10-07b);装配落点 `Gameplay.*` 与 `Sim.Contracts` 边界 | LOW |

**Engine Risk**: **HIGH** —— Input System 直读通道时序、UI Toolkit 手柄焦点(承假设 6,⚠️半可信)、
IL2CPP 逐位对拍三处叠加;**OQ-10-12 原型门(2 动作实测 `L_input`<50 ms + 抖动门 11×)= 排程级前置**,
未过门前本 epic 不得进入实现故事执行。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-emergency-*`)。计数(2026-09-28 实测):**21 条**。
> 需求原文以 registry 为准,评审时重读(read fresh at review time)。

| TR-ID | Requirement(摘要) | ADR Coverage |
|-------|--------------------|--------------|
| TR-emergency-001…003 | EmergencyReading 全整数读数 · 直读通道不穿 UI 栈 · 3 侧零判定/零 SimEvent | ADR-011 ✅ |
| TR-emergency-004…008 | Judge 三扇门纯函数 · SkillMul 定义者=10 · 定点缩放/稳度时序误差 · result_mul 三档 · 跨平台重放整数一致 | ADR-005/006/012 ✅ |
| TR-emergency-009…013 | EmergencyAttempt 聚合上行(可靠通道)· 主机 Judge+Append+Seq · 两支处置事件 · 七项载荷对齐 11 · Kind 白名单在表 | ADR-001/009/024 ✅ |
| TR-emergency-014…018 | 跳过路径偏序 · 中止≠跳过 · 档位意图必发 · MotorSuppressed · Armed 压制 InteractIntent(3 侧零状态) | ADR-011/013 ✅ |
| TR-emergency-019…021 | L_input<50 ms 切分口径 · 键鼠回退定义性条件 · toggle 等价 | ADR-011/005 ✅ |

## Definition of Done

This epic is complete when:
- **OQ-10-12 原型门先行**:2 个动作的垂直原型实测 `L_input` < 50 ms(测量点=直读回调→该帧呈现结束)且抖动门 11× 成立 —— 未过门不写生产代码
- All stories implemented, reviewed, closed via `/story-done`
- All AC from `design/gdd/emergency-procedures.md`(AC-10-01…24)verified;[L] ADVISORY 项以主创走查签核
- AC-10-04b 三格逐位在 ADR-012 矩阵实跑(此前该条 NOT-RUN,禁借绿)
- 与 prescription-medication epic 的载荷交叉校验(AC-10-06b)双向绿;三 Kind 在 kindgen 白名单(AC-10-07b)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | EmergencyReading 读数与直读通道契约 | Integration | **Complete ✅ 2026-09-30** | ADR-011/006 |
| 002 | 动作表/熟练度表与 result_mul 烘焙 | Config-Data | **Complete ✅ 2026-10-02** | ADR-014/006/024 |
| 003 | Judge 三扇门定点纯函数 | Logic | **Complete ✅ 2026-10-02** | ADR-005/006/012 |
| 004 | Aggregate、可靠上行与主机落流 | Integration | **Complete ✅ 2026-10-02** | ADR-001/009/011/024 |
| 005 | 模态期:跳过/中止/档位意图/输入压制 | Integration | **Complete ✅ 2026-10-02** | ADR-011/013/020 |
| 006 | 手感、预表现与键鼠回退 | Visual-Feel | **Complete ✅ 2026-10-02** | ADR-011/013/018 |
| 007 | `EmergencyTreatmentApplied` 载荷的结算链补完 —— 九字段齐备(b6 门查出的手搓点) | Logic | **Complete ✅ 2026-10-03**(17/17) | ADR-029 + ADR-009 Amendment I |

---

> ⚠️ **2026-10-03 状态订正(D3-b)**:本表 002–006 原标 `Ready`,
> 而各 story 件**自身**标 `Complete ✅ 2026-10-02`(双代理评审修复后)。
> 本表已对齐 story 件 —— **以 story 件为准**(本表为索引,不产生状态)。
