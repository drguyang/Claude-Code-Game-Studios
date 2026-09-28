# Story 006: 手感、预表现与键鼠回退

> **Epic**: 急救动作模块
> **Status**: Ready
> **Layer**: Feature
> **Type**: Visual-Feel
> **Estimate**: 8h + 原型门(OQ-10-12)
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/emergency-procedures.md`(F-10.6 两个延迟预算切分 · 规则十二 OQ-51-9 兑现 · §Game Feel · §Visual/Audio Requirements · UI-10.2 硬约束 · Edge Cases 键鼠/非主机)
**Requirement**: TR-emergency-019(L_input<50 ms 切分口径)· TR-emergency-020(键鼠回退定义性条件)· TR-emergency-021(toggle 等价的表现侧)· AC-10-17 的呈现侧(零档位指示)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011 §二(直读通道 <50 ms)+ Amendment B(本地判定 = 预表现,权威在主机)· F-10.6 切分:`AC-10-08` **只测 `L_input`**(预表现路径,10 全责);`L_eval`(主机 Append→体征可见 = 50 ms + 求值次序)归 9 的求值节奏 —— **禁把两者重新合并成「端到端 < 50 ms」** · ADR-018 §六(无提示音铁律:不因 `JudgeResult` 改变音色/素材/强度)· ADR-013(呈现 DTO 无评价字段;`mag_last` 仅表现)
**ADR Decision Summary**: 键鼠无模拟量通道 ⇒ `magnitude ≡ MAG_MAX`(回退形状),**幅度门恒过、节奏+稳度双门照评、`cause = 降级`**(AC-10-21 定义性条件,原稿零条)。非主机玩家 `L_input` 在本机独立成立,预表现与主机结果不出现「玩家可感知结果反转」之外的差异。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(全部 [L]/[I] 手感项须实机;直读时序 = Input System 6.3 post-cutoff 行为;OQ-10-12 原型门 = **写第一行生产代码前**的 2 动作实测)
**Engine Notes**: 测量点已定死 = **直读回调 → 该帧呈现结束**,不含 `L_eval`;`tick ≥ 20 Hz` 时「端到端」数学上必破(GDD 原文),测错终点即错判。

**Control Manifest Rules (this layer)**:
- Required: 回退路径是**定义性条件**非兜底特例;`mag_last` 只供预表现定格/48 回放,不进任何门;反馈只报「手多抖」不报「够不够」
- Forbidden: 零 QTE 条 / 零评价文字 / 零分数(AC-10-09);档位指示(音色/素材/强度因 `JudgeResult` 而异,AC-10-17);为降 `L_eval` 走即时生效(承 story 004 禁令)
- Guardrail: 本 story 的 [L] ADVISORY 项以主创签核结;自动化半边(静态扫描)可 BLOCKING

---

## Acceptance Criteria

*From GDD `design/gdd/emergency-procedures.md`, scoped to this story:*

- [ ] **AC-10-08**[L] ADVISORY:目标硬件 2 个动作实测 `L_input`(输入→表现呈现)< 50 ms 且残余抖动视觉上可接受(主创签核);**测量点 = 直读回调→该帧呈现结束,不含 `L_eval`**(F-10.6)—— 实测前 NOT-RUN(依赖 OQ-10-12 原型门 + 桌面实机)
- [ ] **AC-10-21**[A] BLOCKING:纯键鼠(无模拟量通道)完整走一次动作 ⇒ **幅度门恒过、节奏+稳度双门照评、`cause = 降级`**;`magnitude ≡ MAG_MAX` 的回退形状是定义性条件(规则十二 · Edge Cases)
- [ ] **AC-10-09**[A+B 双判据] BLOCKING:静态扫描(10 的呈现 DTO 无任何评价/分数/进度/完成度字段 —— `PresentationDtoGuard` 递归反射,承 ADR-013 AC-37-15 先例)+ 走查 ⇒ 零 QTE 条/零评价文字/零分数
- [ ] **AC-10-17**[A] BLOCKING:一次 `Missed` ⇒ 音频/视觉**零档位指示**(音色/素材/强度不因 `JudgeResult` 而异);由 AC-44-09 白名单断言 + `trigger_source` 单值断言共同守(与 audio-system epic 联测)
- [ ] **AC-10-19**[L] ADVISORY:联机非主机玩家本机 `L_input < 50 ms` 成立(预表现本机独立),且主机 `JudgeResult` 与该机预表现在 `agg` 相同前提下不出现「玩家可感知结果反转」**之外**的差异 —— 非主机实测面 NOT-RUN(依赖 45 联机夹具,承记忆库「R8/45 夹具为 AC-42-F1 同类硬前置」同型处置)
- [ ] **AC-10-23**[L] ADVISORY:动作进行中录屏,主创走查确认玩家**能从世界内几何/声音读出「手现在多抖」**(不读出「够不够」;§未交付项 3 · OQ-10-13)—— 手感不可自动化测
- [ ] **预表现实现**[L]:客户端本地跑 `Judge`(复用 story 003)仅驱动预表现(定格值 = `mag_last`);主机结果到达后覆盖;**不写流、不发成长**(与 story 004 的本地=预表现条款对偶,判据归本 story 的呈现断言)
- [ ] **F-10.6 口径护栏**[A]:代码与测试注释中不存在「端到端 < 50 ms」表述/断言(测量终点 = `L_input`);`L_eval` 面归 9 的 tick 节奏,不并入本 story

---

## Implementation Notes

*Derived from F-10.6 + §Game Feel + 规则十二:*

1. **原型门优先**: OQ-10-12 = 写第一行生产代码前,以 2 个动作的垂直原型实测 `L_input < 50 ms` + 抖动门 11×(`TICK_PERIOD 50 ms` vs CPR ≈550 ms,已核验比值)。原型住 `prototypes/`(与 src 隔离),脚本化优先(记忆库裁定:代码化 + 菜单一键生成 + PlayMode 出数字,只留手感项给用户手测)。
2. 键鼠回退在**读数侧**物化(story 001 的 3 侧交付含数字键路径),10 只见 `magnitude = MAG_MAX` 常量档 ⇒ 门逻辑零分支(回退=输入形状,不是判定分支 —— 避免第二判定路径)。
3. 预表现层只读 agg + 本地 Judge;订阅主机 `EmergencyTreatmentApplied` 到达做结果对齐;反转抑制:本地 AppliedWeak 而主机 Applied ⇒ 静默上调(不可感知);本地 Applied 而主机 Missed ⇒ 以一个 tick 内收敛为「轻/重」呈现差,零评价文字(与 AC-10-09 一致性)。
4. `cause = 降级` 进载荷(story 004 七项之一),遥测口径归 51(telemetry epic 消费,本 story 只保证字段有值)。
5. 音频触发源单值:`trigger_source ∈ 行为反馈白名单`(ADR-018);失败/成功共用同一声音素材,差异只在**动作本身的物理声**(手抖程度读出),不在评价层。

## Out of Scope

- [Story 004]: 上行/落流权威(本 story 只呈现)
- VR 模式急救实现(ADR-013 §七 推 P1a;OpenXR 绑重与站定式交互另 epic)
- 教学回放(48 tutorial epic 消费 `mag_last`)
- 手柄焦点缓办轮的排程(记忆库裁定,由生产侧统一插档)

## QA Test Cases

*Written at story creation(lean mode).*

- **DTO 洁净**: 递归扫呈现 DTO 传递闭包 ⇒ 零评价/分数/进度字段;负夹具注入 `score` 字段 ⇒ 红(AC-10-09 静态半边)。
- **键鼠回退**: 无模拟量设备夹具 ⇒ 幅度门恒过、双门照评、`cause=降级`(AC-10-21 三断言);同读数走手柄 ⇒ 幅度门可不过(对偶)。
- **零档位指示**: Missed vs Applied 两跑 ⇒ 音频事件表触发集与素材 id **完全相同**,仅物理参数随抖动(AC-10-17;与 audio 白名单门联测)。
- **L_input 脚本化实测**: PlayMode 测试在直读回调记 t0、该帧呈现结束记 t1 ⇒ p95 < 50 ms;输出落 `unity/Logs/`,**测错终点(含 L_eval)= 无效测**(GDD 口径护栏)。
- **预表现不写流**: 本地跑 1000 次动作 ⇒ 流事件数 = 主机权威数(2×完成数),本地零直写。

## Test Evidence

**Story Type**: Visual-Feel(自动化半边 AC-10-09/21/17 为 BLOCKING 逻辑测;[L] 手感项走查签核)
**Required evidence**: `unity/Assets/Tests/PlayMode/EmergencyProcedures/feel_latency_test.cs` + 签核记录 `production/qa/evidence/emergency-procedures/story-006-*.md`(注意:`production/qa/` 全仓从未建立 —— 建目录时一并落 README,承记忆库 NOT-RUN-override 处置)
**Status**: [ ] Created — NOT STARTED;AC-10-08/19/23 NOT-RUN(实机/走查/45 夹具前置),禁借绿

---

## Dependencies

- Depends on: Story 001(直读读数)、Story 003(本地 Judge)、Story 004(权威落流与 cause 字段)、Story 005(跳过/中止呈现覆盖)、audio-system epic(AC-44-09 白名单侧)、skeuomorphic-ui epic(零 QTE 条的元件层保证)
- Unlocks: OQ-10-12 原型门结论 → 回填 ADR-011/本 epic DoD;48 tutorial(回放数据);本 epic DoD

## Completion Notes
