# Epic: 急救动作模块

> **Layer**: Core(输入聚合 / 判定)× Feature(模态期交互)
> **GDD**: design/gdd/emergency-procedures.md
> **Architecture Module**: L3 Gameplay + L2 Sim 接缝(判定输入进流,主机权威)
> **Status**: In Review(7/7 story 已实现;两轮评审 A1/A2/A6/B4/C4/C5 **已全闭**;✅ **评审原件两份均在库**(首轮 + round2)—— 旧记「评审原件缺」为**方向性错记**,已订正。**不转 Complete 的真实原因**:① **实跑 Unity 测试套件**(round2 自陈**未实跑**,「测试通过」类结论均来自读测试源码)⇒ 现记的「0 红」**未经验证**;② D1/D2/D3 文档状态对齐(round2 未验证)。其余未闭项:载荷 `Seq` 占位(归上行链 45)· AC-10-04b 三格逐位(ADR-012 矩阵未激活,禁借绿)。⚠️ 详见下方 §Epic Status)
> **Stories**: **7 stories** — see table below

## Epic Status(2026-10-03)

**不转 Complete** —— 但**原因已变**(旧记「评审原件缺」是**方向性错记**,见下)。

### ① 评审原件**不但在库,还是两份**(订正)

本 EPIC 此前被登记为「**评审原件缺**」,`epics/index.md` 与 `session-state/active.md` 同此口径。
**实测该登记为误** —— 两份原件**均已在库**:

| 件 | 路径 |
|---|---|
| 首轮 | `production/qa/evidence/review-emergency-procedures-2026-10-03.md` |
| 二轮 | `production/qa/evidence/review-emergency-procedures-round2-2026-10-03.md` |

⇒ 「评审原件缺」这条残留**不成立**。原措辞已就地在 `index.md` / `active.md` 订正。

### ② 判据缺陷结算(两轮评审 → 实测)

| # | 原判定 | 实测 | 结算 |
|---|---|---|---|
| **A1** AC-10-02 恒真 | 🔴 断言取 `Sim.Contracts` 查其名 `Contains("Input")` ⇒ 零扫描 | round2 逐行复核:`EmergencyIntegerGates.cs:89-174` 已扫**引用面 + IL 面**,配非空转守卫(`scannedMethods == 0` ⇒ 记红) | ✅ **已闭** |
| **A2** AC-10-03 恒真 | 🔴 与字段类型测逐字重复,无操作码检查 | round2 复核:`:186-322` 真实 call 图闭包 + 浮点指令扫描 + 签名面 | ✅ **已闭** |
| **A6** AC-10-24 holdMode 被忽略 | 🔴 `Complete` 不读 `holdMode` | `ModalPhaseEvaluator.cs:94-130` | ✅ **已闭** |
| **B4** DC-5 无校验体 | 🔴 仅返回字符串数组 | `EmergencyAction.cs:114-155` + `action_tables_bake_test.cs:154-162` | ✅ **已闭** |
| **C4** SkillMul 死代码 | 🔴 算出即弃 | `JudgeEvaluator.cs:126-134` 真用(GDD F-10.2 原文式) | ✅ **已闭** |
| **C5** JITTER 违 Forbidden | 🔴 C# 裸 `/` 向零截断 | `JudgeEvaluator.cs:81-89` 改 `ROUND_HALF_AWAY_FROM_ZERO` | ✅ **已闭** |
| **D1/D2/D3** 文档状态 | ⏳ round2 **未验证** | round2 自陈「本轮未读 `design/gdd/emergency-procedures.md` 的 AC-10-04a 单元格与 story-006/007 的 Test Evidence 行」 | ⏳ **仍未验证** |

⚠️ **D3-b(2026-10-03 已局部处置)**:本 EPIC §Stories 表 002–006 曾标 `Ready`,而各 story 件**自身**标 `Complete` —— 已对齐 story 件。story-007 头/体矛盾(头 `Complete ✅` vs 体 `[ ] Pending`)在同批**同型**缺陷中,但 **story-007 的体已由 modular-building 轮订正** —— 本 epic 侧尚需逐件复核。

### ③ 转 Complete 的真实前置(round2 §5 逐条)

1. ✅ **报告已落盘**(2026-10-03)—— **两份**。
2. ⏳ **D1/D2/D3 文档状态对齐** —— AC-10-04a 单元格数字同步 A8 勘误 · story-006 测试位置声明 · story-007 Test Evidence 行。
3. 🔴 **实跑 Unity 测试套件** —— **本轮最大盲区**:round2 **未实跑**,
   自陈「凡『测试通过』类结论均来自**读测试源码**而非执行结果」。
   ⇒ 本 EPIC 现记的「EditMode 0 红 · PlayMode 36/36」**须按此口径重读** ——
   **在 round2 基线(`265ad85`)上未经验证**;任何转 Complete 前须以当前 HEAD 重跑。

### ④ round2 自陈的其余评审盲区

- `action_tables_bake_test.cs` / `modal_phase_test.cs` / `aggregate_stream_test.cs` /
  `host_authority_test.cs` / `feel_latency_test.cs` **全文未读**;
- 第一轮证据(如 `applied_payload_settlement_test.cs:131-140` 的 Seq 占位)二轮**未重新验证**。

### 未闭项登记(不阻塞本 epic,但阻塞转 Complete)

- ⏸️ **D1/D2/D3** 文档状态对齐(round2 明确未验证)。
- 🔴 **实跑 EditMode/PlayMode** —— round2 的判据均为源码阅读;「0 红」为**未执行**状态。
- ⏸️ **载荷 `Seq` 占位** —— 归上行链 45(现有测试已钉死占位事实,合规)。
- ⏸️ **AC-10-04b 三格逐位** —— ADR-012 矩阵未激活,NOT-RUN,禁借绿。

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
