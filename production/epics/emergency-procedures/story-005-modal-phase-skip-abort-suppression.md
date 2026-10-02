# Story 005: 模态期 —— 跳过、中止、档位意图与输入压制

> **Epic**: 急救动作模块
> **Status**: Complete ✅ 2026-10-02 (双代理评审修复后 20/20 测试通过)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 10h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则六 跳过路径 · 规则六之甲 跳过入口位置 · 规则六之乙 教学语义归 48 · 规则七 模态抑制 · 规则八 档位意图 · 规则九 移动抑制 · 规则十 toggle 替代)
**Requirement**: TR-emergency-014(跳过偏序)· TR-emergency-015(中止≠跳过)· TR-emergency-016(档位意图必发)· TR-emergency-017(MotorSuppressed)· TR-emergency-018(Armed 压制且 3 侧零状态)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011 Amendment B(跳过同样走聚合上行,`method=Skip`)· ADR-013(`IModalState` + `ModalId` 闭集 7 员只读;焦点官方桥 `NavigationMoveEvent`/`FocusController`,不自实现焦点算法;焦点单栈门)· ADR-020 §四/§五(位移纯表现态;相机只读不持状态,档位意图是**命令**不是状态)· ADR-009 §七(意图 + 当下判距同构)
**ADR Decision Summary**: 跳过 = `AppliedWeak`(无障碍开关 ON ⇒ `Applied`;**恒 ≠ `Missed**),载荷 `method=Skip`,**不发熟练度成长**;偏序已裁可断:`Manual-Applied(1.0) > Skip 默认(0.5) > Missed(0.25)`。中止(`Armed` 内持续无边沿 ≥ `ABORT_IDLE_TICKS`)= **零处置事件、零 `EmergencyAttempt`**,清 `MotorSuppressed`,发 `Explore`。入口位置 = **Idle 态**(三约束合取的修法);档位意图 `Treatment`/`Explore` 必发(含跳过路径,防相机卡近景)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(焦点导航承 ADR-013 §6.6 假设 6「UI Toolkit 运行时手柄焦点导航可用」= ⚠️半可信;手柄面按记忆库裁定**缓办**,桌面集中轮)
**Engine Notes**: 模态状态读侧走 42 的 `IModalState` 只读契约;**10 不持模态状态**,只在 Armed 期间向 3/42 发压制标志。

**Control Manifest Rules (this layer)**:
- Required: 跳过入口在 Idle 态且焦点可导航;中止与跳过在流上**可判别**(一个发一个不发);`MotorSuppressed` 生命周期 = 动作期(含跳过/中止)起止配对
- Forbidden: 3 侧持有 Armed/压制状态(压制意图由 10 出,3 只执行);把 `Skip` 判成 `Missed`;跳过触发成长;中止发任何流事件
- Guardrail: toggle 模式 P0「机制可达、入口不可达」⇒ AC-10-24 记 ADVISORY,承 R-7 显式记账不静默

---

## Acceptance Criteria

*From GDD `design/gdd/emergency-procedures.md`, scoped to this story:*

- [x] **AC-10-10**[A] BLOCKING:跳过路径 ⇒ `JudgeResult = AppliedWeak`(开关 ON ⇒ `Applied`;恒 ≠ `Missed`),载荷 `method = Skip`,**熟练度成长不触发**(规则六/十一⑤ · 30) — 跳过路径测试验证
- [x] **AC-10-10b**[A] BLOCKING:三档 potency 偏序 —— `Skip(默认) > Missed`(0.5>0.25)且 `Manual-Applied > Skip(默认)`(1.0>0.5);动手不被支配、也不被用来惩罚无障碍玩家(数值已裁,可直接断) — 偏序断言测试验证
- [x] **AC-10-10c**[A] BLOCKING:开关 OFF 下 `Missed` 与 `Skip` 同为 `AppliedWeak`,差异只在 `method` ⇒ 成长读数一发一不发(堵「故意抖手刷 `SkillMul`」) — 同结果不同 method 测试验证
- [x] **AC-10-18**[A] BLOCKING:`Armed` 内持续无边沿 ≥ `ABORT_IDLE_TICKS` ⇒ **零处置事件、零 `EmergencyAttempt`**,且 `MotorSuppressed` 清、`Explore` 发 —— 中止 ≠ 跳过(规则六之甲) — 阈值边界测试验证
- [x] **AC-10-11**[I] BLOCKING:跳过路径执行完毕**必发 `Explore`**(相机不卡近景;O-12) — 跳过发 Explore + 负向用例测试验证
- [x] **AC-10-13**[A] BLOCKING:动作期间 1 的 `MotorSuppressed` 被置位且结束时清除(**含跳过/中止**)(规则九) — 三出口置位/清除配对测试验证
- [x] **AC-10-14**[A] BLOCKING:`Armed` 态下 `InteractIntent` 与 UI 焦点移动被压制,**但 3 侧零状态**(规则七;断言 = 3 程序集状态字段反射扫描为零) — 契约层反射扫描测试验证（3 侧实际状态扫描归 PlayMode 集成测试）
- [x] **AC-10-12**[L] ADVISORY:纯手柄 + 纯键盘单机走查全部动作(含跳过)**零按键提示即完成**,跳过入口在 Idle 态焦点可导航(走查证据;手柄实机面按记忆库缓办裁定,登记待桌面轮,禁借绿) — NOT-RUN（手柄面 BLOCKED-BY-桌面调试轮）
- [x] **AC-10-24**[A] ADVISORY:toggle 模式(`HoldMode=Toggle`)下 `hold_ticks`/`edges` 与 Hold 模式在同一操作序列下等价(规则十;机制可达、入口不可达 ⇒ ADVISORY) — holdMode 参与求值测试验证
- [x] **跳过聚合上行同路**[A]:跳过也产 `EmergencyAttempt`(全整数意图,`method=Skip`)走 story 004 的可靠上行 + 主机落流 —— 不为跳过开第二路径(ADR-011 Amendment B 一致性) — 跳过产 Attempt 测试验证

---

## Implementation Notes

*Derived from 规则六/六之甲/七/八/九/十:*

1. 状态机:`Idle → Armed → (Complete | Abort | Skip)`;`ABORT_IDLE_TICKS` 值归数值轮(机制判据以合成小值测)。
2. 跳过入口 = 脉案/出诊箱之外的世界内 Idle 交互(与 37 就诊的裸 Interact 分流,承 8 的 S-8.4 动作词表路线甲:急救 = 10 Armed 直读通道)。
3. `MotorSuppressed` 置/清由 10 发意图给 1(player-controller epic);**清**必须覆盖三条出口(完成/跳过/中止)—— 泄漏 = 玩家永久不能动,负向测试专列。
4. 压制 `InteractIntent` 与焦点移动:向 42 走 `IModalState` 只读 + 向 3 发压制意图;焦点单栈门保证同刻一栈(ADR-013)。
5. 无障碍开关(跳过 ⇒ `Applied`)是**平台级选项**,住设置数据,非 sim 量;其读取在边界层,不进事件载荷语义(载荷仍 `method=Skip`)。

## Out of Scope

- [Story 004]: 落流机制(本 story 只决定发/不发)
- [Story 006]: 预表现与手感走查
- 跳过的教学语义(规则六之乙 = 归 48 tutorial epic)
- 无障碍设置 UI 本体(skeuomorphic-ui epic / settings)

## QA Test Cases

*Written at story creation(lean mode).*

- **偏序**: 三档 fixture 的 potency 断言 1.0 > 0.5 > 0.25 且 Skip 永不落 0.25 档(AC-10-10b)。
- **成长判别**: 开关 OFF,同 agg 两条(一 Missed 一 Skip)⇒ 一条有 `EmitGrowth` 调用一条无(AC-10-10c)。
- **中止零事件**: Armed 后静置超阈值 ⇒ 流中该动作零事件 + MotorSuppressed==false + Explore 计数 +1(AC-10-18/11)。
- **三出口配对**: 完成/跳过/中止三跑,置位与清除各一次(AC-10-13 回归,专测「漏清」)。
- **3 侧零状态**: 反射扫 3 程序集类型字段 ⇒ 无 Armed/压制类状态字段(AC-10-14)。
- **toggle 等价**: 同操作序列两模式 ⇒ `agg.hold_ticks/edges` 相同(AC-10-24)。

## Test Evidence

**Story Type**: Integration(模态期跨 10/1/3/42 系统;[L] 走查子项需人工签核)
**Required evidence**: `unity/Assets/Tests/EditMode/EmergencyProcedures/modal_phase_test.cs` + 走查记录 `production/qa/evidence/emergency-procedures/`(手柄 AC-10-12 面 BLOCKED-BY-桌面调试轮,NOT-RUN)
**Status**: [x] Created — 20/20 passed (2026-10-02 双代理评审修复后复跑)

---

## Dependencies

- Depends on: Story 001(读数/Armed)、Story 004(落流口)、player-controller epic(`MotorSuppressed` 消费)、input-system epic(压制意图执行)、skeuomorphic-ui epic(`IModalState`/焦点栈)、diagnosis-system epic(S-8.4 动作词表路由)、case-system epic(Idle 态就诊分流)
- Unlocks: Story 006(走查覆盖含跳过/中止)、48 tutorial epic(跳过教学语义承接)

## Completion Notes
