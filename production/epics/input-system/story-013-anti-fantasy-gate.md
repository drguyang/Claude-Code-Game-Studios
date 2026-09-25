# Story 013: 反幻想守门登记面(P0 / 后阶段)

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 2h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(§Dependencies 四 · `AC-3-F1a` / `F1b`)
**Requirement**: TR-input-020(⚠️ partial:反幻想守门 —— 门本体归 42 + 48 联合,3 侧只交付键名与登记)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构 · ADR-013(次): 拟物 UI 框架
**ADR Decision Summary**: ADR-011 §一 —— 3 的「零存在感」玩家幻想(`Player Fantasy`)是反幻想门的**动机来源**;3 侧义务 = 交付 `iconKey` 键名(§Visual/Audio 三)而不交付任何按键提示渲染。ADR-013 §9 C3 / Amendment —— 走查的呈现面归 42,教学叙事归 48;判据标准 = `technical-preferences.md:32-33`(拟物 UI 同时支持键鼠与手柄导航)+ `AC-8-43/44`,**不引不存在的 ux 路径**(GDD 判据标准注记 2026-09-20 处置)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 本故事 = **登记面 + 交接**,3 侧**不实现**走查本体、不渲染任何提示层 —— 纯文档/契约,零引擎 API。LOW;走查执行依赖 42 / 48 的实现轮(见各 AC 的 BLOCKED-BY 注)。

**Control Manifest Rules (this layer)**:
- Required: 3 只交付键名(`iconKey`),不承担反幻想门本体(GDD §Dependencies 四「反幻想守门」行 · 归属 42 + 48)
- Forbidden: 3 侧出现任何按键提示浮层 / QTE 提示条 / 「按 X 键」HUD 代码路径(与 AC-F1a① 同源 —— 3 本来就不渲染 UI,登记为不变量)
- Guardrail: 判据标准只引 `technical-preferences.md:32-33` + `AC-8-43/44`(GDD 明注:不用 `design/ux/accessibility-requirements.md` 之外的新依赖;43 出诊箱纸归 F1b/P1a)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story(3 侧 = 登记与交付;走查执行本体归 42 + 48):*

- [ ] **AC-3-F1a①(BLOCKING · P0 · 联合 · 3 侧登记)**: **反幻想门 · 目视半条** —— 任意 P0 游戏状态、键鼠/手柄(**不含 VR**)屏幕零按键提示浮层 / 零 QTE 提示条 / 零「按 X 键」持久 HUD(证据 = 截图集 + 主创签核)。**归属 42(呈现)+ 48(教学);BLOCKED-BY: 42/48 走查执行** —— 本故事交付:① 门条目登记(EPIC / 故事链)② 3 侧不变量确认(3 零渲染代码路径)③ `iconKey` 键名清单交接给 42/48
- [ ] **AC-3-F1a②(BLOCKING · P0 · 联合 · 3 侧登记)**: **手柄单机走查半条** —— 不接键鼠、仅手柄完成**两份 P0 纸**:脉案(39)+ **20 自己的 P0 容器界面**(界面名由 20 定;四轮覆写:出诊箱纸归 43/P1a ⇒ 归 F1b,P0 不出「出诊箱纸」),须满足 `AC-8-44`(无按键提示即完成诊断);**缺一即败**。**归属 42 + 48;BLOCKED-BY: 42/48 走查 + 39/20 界面就位** —— 本故事交付:登记 + 键名交接(两半**同判**的联合语义记入 EPIC)
- [ ] **AC-3-F1b(BLOCKING 限 P1a / P1b 阶段 · 3 侧登记)**: **后阶段扩展** —— VR + 纸质地图(43)手柄走查(43 落地 P1a ⇒ 该阶段签核;P0 **不适用**,同 `AC-3-B1b` 分级先例)。**登记不执行**:P0 轮只把本条挂入故事链与阶段门清单,不产出走查证据

---

## Implementation Notes

*Derived from GDD `AC-3-F1a` / `F1b` 全文 + §Dependencies 四 + ADR-013:*

- **3 的角色 = 键名供给方,不是门的执行方**(GDD §Dependencies 四原文:「3 只给键名,**不承担本门**」):`iconKey` 清单(`QueryBinding` 的输出面,Story 008)交接给 42 / 48
- **两半同判**:F1a①(目视零提示)与 F1a②(手柄两纸走查)**缺一即失败** —— 联合 AC 语义,登记时不得只挂一半
- **主语已就地覆写**(四轮):F1a② 走查对象 = **20 自己的 P0 容器界面**,**不是**「出诊箱(43)」—— 43 出诊箱纸归 F1b(P1a);若按旧写法 = 「以 BLOCKING 之名、行不可签核之实」(与三轮拆 F1 同型病灶)
- **分级不降级**:F1a 整体 **BLOCKING**(用户裁定④「强制执行」的落点),其①证据类型虽属 Visual/Feel ⇒ ADVISORY,**整体仍 BLOCKING**;F1b 限 P1a/P1b 阶段签核(P0 不适用,不是取消)
- **判据标准(共用)** = `technical-preferences.md:32-33` + `AC-8-43/44`;**不引**其他路径(2026-09-20 处置:`design/CLAUDE.md` 已订正,`adr-013:276` 回写归 `/architecture-review` —— 本故事不改 ADR 正文)
- **本故事的可交付物**:① EPIC 故事链登记门条目与阶段 ② 3 侧「零渲染」不变量的静态确认(3 本无 UI 代码,断言零成本)③ `iconKey` 清单文件(交接件)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008:`QueryBinding` / `iconKey` 的**机制**(本故事只消费其产出做交接)
- 走查执行、截图集、主创签核 —— 42(呈现)+ 48(教学)的实现轮;3 不出走查证据
- 39 脉案界面 / 20 容器界面本身的实现 —— 各自系统的 GDD 轮
- VR 走查与 43 出诊箱纸 —— F1b(P1a/P1b),本故事只登记
- 手柄焦点导航 spike(ADR-013 假设 6 / S5)—— 桌面集中调试轮挂账,非本故事

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). UI 类型 —— Manual check 格式:*

- **AC-3-F1a①**(3 侧交付物 + 联合门登记):
  - Setup: 3 的代码树 + EPIC 门登记。
  - Verify(3 侧,本故事可自验):3 源码零按键提示/零 QTE/零 HUD 渲染代码路径(3 无 UI 栈引用 —— 与 AC-A6/B2① 同构断言);门条目已登记(EPIC / 故事链含联合归属 42+48 与 BLOCKED-BY)。
  - Verify(联合门,42/48 执行):截图集 + 主创签核 —— **BLOCKED-BY 42/48**,不计入本故事签核。
  - Pass condition: 3 侧两项过 + 登记齐全;联合走查状态标 BLOCKED(禁借绿)。
- **AC-3-F1a②**(登记 + 交接):
  - Setup: `iconKey` 清单 + 走查登记。
  - Verify: ① 键名清单覆盖两纸(脉案 39 + 20 P0 容器界面)所需全部动作键;② 联合语义「两半同判缺一即败」已写入登记;③ 主语 = 20 容器界面(非 43 出诊箱)—— 覆写正确落账;④ `AC-8-44` 引用正确。
  - Pass condition: 四项过;走查执行本身 BLOCKED-BY(42/48 + 39/20 就位)。
- **AC-3-F1b**(登记不执行):
  - Setup: 阶段门清单。
  - Verify: 条目标注「BLOCKING 限 P1a/P1b · P0 不适用」;P0 轮**无**走查证据产出(不误跑);VR + 43 出诊箱纸范围正确挂接。
  - Pass condition: 登记正确 + P0 零误执行。

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- UI: `production/qa/evidence/anti-fantasy-gate-evidence.md`(3 侧交付物:零渲染确认 + 键名清单 + 门登记截图/摘录 + 签核;联合走查部分标 BLOCKED-BY,禁借绿)

**Status**: [ ] Not yet created
- 分级注:F1a 联合走查证据(截图集)由 42/48 轮产出 —— 本文件在 3 侧只收登记面;F1b 证据 P1a/P1b 阶段补

---

## Dependencies

- Depends on: Story 008(`iconKey` 键名是本故事的交接内容)
- Unlocks: 42 / 48 联合走查轮(消费键名清单与门登记)· F1b 的 P1a/P1b 阶段门
