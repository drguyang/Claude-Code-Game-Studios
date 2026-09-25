# Story 011: L_input→pixel 延迟实测与 L_render/L_poll 分解

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Visual/Feel
> **Estimate**: 2h(+ 真实硬件前置)
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(§Formulas F-3.3 · §Tuning Knobs 四)
**Requirement**: TR-input-016(⚠️ gap = **实测前置** —— 非 ADR 缺口,系「最低目标硬件未定」的硬件依赖项,bound to `/test-setup` 轮)· TR-input-006(附注:`L_input < 50 ms` 的实测半边在本故事)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构
**ADR Decision Summary**: ADR-011 §二 / Amendment B —— 直读通道的**理由是方差不是算术**,F-3.3 单判据 `L_input → pixel ≤ 50 ms` 是唯一端到端验收线;分解项 `L_poll` / `L_render`(≈2 帧)是**诊断量**,不进验收线。两预算切分(manifest Guardrail):`L_input` 只测预表现路径(10 全责),`L_eval` 归 9 的求值节奏 —— **不得合并成「端到端 < 50 ms」**;3→10 两义务:①均值达标归 3(本故事 B1b)②抖动上界归 10(F-10.3b)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 本故事**必须在真实目标硬件 + Development Build 上实测** —— 编辑器/集群环境不可签核(帧计时与驱动层延迟不可代表出货形态)。最低目标硬件**未定** ⇒ 现阶段无法签核:设计阶段本条为 **ADVISORY**,**发版前为 BLOCKING**;VR 移出 P0(归 P1b,ADR-011 §二)。工具与帧捕获面 post-cutoff 零覆盖 ⇒ HIGH。

**Control Manifest Rules (this layer)**:
- Required: 方法学四要素在实测报告明写 —— 统计量/分位 · 采样数 · 剔除规则 · 工具(manifest Performance;与 E5 同纪律)
- Forbidden: 把 `L_eval` 并进 `L_input` 报成「端到端 < 50 ms」(manifest Guardrail 明禁;两预算切分承 `emergency-procedures.md` F-10.6 口径)
- Guardrail: `L_input < 50 ms` 只测**预表现路径**;抖动/时序误差上界归 10(F-10.3b),不归本故事

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md` `AC-3-B1b`, scoped to this story(拆三子条;分级:B1b 整体 = 发版前 BLOCKING · 设计阶段 ADVISORY):*

- [ ] **AC-3-B1b①(发版前 BLOCKING · 设计阶段 ADVISORY)**: **均值 ≤ 50 ms** —— `L_input → pixel` 实测**均值 ≤ 50 ms**(硬件实测;最低目标硬件未定 ⇒ 现无法签核,挂 `/test-setup` 轮)
- [ ] **AC-3-B1b②(BLOCKING)**: **方法学四要素明写** —— 报告须含:① **统计量/分位**(均值 + 建议 p95;阈值用户拍定)② **采样数**(建议 p95 ≥ 1000 次)③ **剔除规则**(显式、事前声明)④ **工具**(捕获手段);缺一 = 不可签核
- [ ] **AC-3-B1b③(BLOCKING)**: **`L_render` / `L_poll` 分解** —— 报告给出 `L_input` 的诊断分解:`L_poll`(轮询/更新相位间隔)+ `L_render`(≈2 帧)+ 残差;分解是**诊断量**,验收线仍只有 `L_input` 一条(单判据)
- 分级注:**抖动已移出** B1b(归 10 的 F-10.3b 时序误差 —— 3→10 两义务之②);VR 已移出 P0(归 P1b)

---

## Implementation Notes

*Derived from ADR-011 §二 / §Risks + GDD F-3.3 / §Tuning Knobs 四:*

- **一条验收线 + 若干诊断量**:验收 = `L_input → pixel` 均值 ≤ 50 ms;`L_poll` / `L_axis` / `L_judge` / `L_schedule` / `L_render`(≈2 帧)是**定位用分解**,单独达标无意义
- **方差不是算术**(ADR-011 §二):直读通道的存在理由 —— 报告须能说明链上每段的**分布**(至少均值 + p95),而非只给一个平均数
- **两预算切分不可合并**(manifest Guardrail / F-10.6):`L_input` = 输入 → 呈现(10 的预表现,3 全责);`L_eval` = tick 求值次序(9 的节奏,50 ms + 求值次序,玩家对体征本就延迟一个 tick)—— 报告**不得**把两者加总报成「端到端」
- **3→10 义务边界**:本故事只交①均值达标;抖动/时序误差上界归 10(`F-10.3b`)—— 报告若测到抖动,**记录但不归本故事签核**
- **硬件前置**:最低目标硬件未定 ⇒ 设计阶段**不跑实测**(跑了也不是出货形态)—— 本故事现阶段产出 = **测试方法学 + 分解采集脚本/流程**(可先落地),实测执行挂 `/test-setup` 轮;发版前重开为 BLOCKING
- **分级先例**:承 item-database story-012(阶段门 AC 照写不删,标注分级与前置)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 007:直读通道实现与相位正确性(B1a/B2)—— 通道就位是本故事的测量前提(Depends on)
- 抖动上界 / 时序误差(F-10.3b)—— 归 10(3→10 两义务之②)
- `L_eval` 与 tick 相位(`TICK_SECONDS` 步相位)—— 9 / `ITickProvider` 实现期义务
- VR 头显延迟(移出 P0,归 P1b)
- 最低目标硬件选型 —— `/test-setup` 轮(用户决策)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). Visual/Feel 类型 —— Manual check 格式:*

- **AC-3-B1b①**: `L_input → pixel` 实测均值 ≤ 50 ms。
  - Setup: Development Build + 目标硬件(未定,挂 `/test-setup`);合成注入单次急救输入,捕获输入帧与像素变化帧。
  - Verify: 报告给出均值(及 p95);均值 ≤ 50 ms。
  - Pass condition: 均值 ≤ 50 ms **且** 硬件 = 已定的最低目标机型;设计阶段此条标 ADVISORY(无硬件,不执行)。
  - 现阶段产出:方法学文档 + 采集流程就位(**不跑实测**)。
- **AC-3-B1b②**: 方法学四要素明写。
  - Setup: 实测报告(或现阶段的方法学文档)。
  - Verify: 四要素齐全 —— 统计量/分位、采样数(建议 p95 ≥1000)、剔除规则(事前声明)、工具(帧捕获手段);阈值标注「用户拍定」。
  - Pass condition: 四栏均有**具体值或具体规则**,无「视情况」占位。
- **AC-3-B1b③**: `L_render` / `L_poll` 分解。
  - Setup: 同一实测会话的采集数据。
  - Verify: 报告给出 `L_poll`(相位间隔)+ `L_render`(≈2 帧,实测帧数)+ 残差三段;三段之和 = `L_input`(允许测量误差,量级一致)。
  - Pass condition: 分解自洽且**验收线仍标为 `L_input` 单条** —— 报告无「端到端 = L_input + L_eval」型合并表述。
  - Edge cases: `L_render` 实测 ≠2 帧(高低刷屏)⇒ 如实记录,不改判据。

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- Visual/Feel: `production/qa/evidence/latency-measurement-evidence.md` + 主创签核(截图/数据表 + 方法学四要素)

**Status**: [ ] Not yet created
- 阶段注:设计阶段证据 = 方法学文档 + 流程就位(ADVISORY 可挂账);**发版前**须补实测数据翻 BLOCKING(先例:item-database story-012 阶段门)

---

## Dependencies

- Depends on: Story 007(直读通道 + 相位正确是测量对象)
- Unlocks: None(验收线收口;实测执行挂 `/test-setup` 轮)
