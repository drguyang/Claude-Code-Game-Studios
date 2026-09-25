# Story 012: 开发者调试视图与构建剥离

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(§UI Requirements 二 · `AC-3-E2`)
**Requirement**: ⚠️ **无专属 TR** —— 直引 GDD `AC-3-E2` 与 §UI Requirements 二(开发者调试视图三条件)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time;本故事判据权威出处 = GDD AC 原文)*

**ADR Governing Implementation**: `ADR: N/A — GDD §UI 二「开发者调试视图(非玩家可见)」是纯呈现侧开发工具规格,无架构裁决需求;取向已由 GDD 明文锚定(同 13 / 51 / ADR-019 §五),不新立边界。` **载体注记(非治理件)**:AC-E2② 的 CI 命名 job(`player-symbol-check`)与 `.github/workflows/` 同属「判据已定、载体未建」八条(GDD §Dependencies 五),**与 ADR-012 CI 门同批落地** —— ADR-012 不在本 Epic 治理清单,本故事只交付 ② 的**工具/脚本本体与其可跑断言**,CI job 挂账另轮。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 是长期稳定的编译期符号;MEDIUM 来自 ② 的「检查已生成 player 符号」—— 符号面在 IL2CPP/Mono 与裁剪下行为差异须实测(ADR-012 F7 spike 同族),且**载体(命名工具)尚不存在**,断言设计须先于载体落地。

**Control Manifest Rules (this layer)**:
- Required: 调试代码**仅 Development Build 编译**(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`)—— GDD §UI 二条件一
- Forbidden: 玩家构建中存在该代码路径(构建期剥离,AC-E2)· 调试视图**显示 raw 轴值数值**(GDD §UI 二条件三 —— 诱导「调数字」而非「调手感」,同 13 禁显病种的理路)
- Guardrail: 3 的调试视图**不读 42 焦点栈**(三轮已删该项 —— 与 AC-C3 BLOCKING 正面冲突;焦点信息在 42 自有调试视图里看,承 ADR-013 §9 C3)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] **AC-3-E2①(BLOCKING)**: **源侧断言** —— 调试代码块**被 `#if` 包裹**(`UNITY_EDITOR || DEVELOPMENT_BUILD`);静态扫描对全部调试视图代码路径断言包裹存在
- [ ] **AC-3-E2②(BLOCKING · 载体待建)**: **检查已生成 player 的符号** —— 断言目标符号在出货 player 中**不存在**;须在 CI 中出现一个**命名 job**(建议 `player-symbol-check`)与一个**命名工具/脚本**。⚠️「判据已定、载体未建」(与 `AC-3-A4②` / `AC-3-B2` 分析器同族)—— **与 ADR-012 CI 门同批落地**,本故事交付工具本体 + 可跑断言,CI job 挂账
- [ ] **AC-3-UI-2(BLOCKING · §UI 二三则)**: 开发者调试视图满足三条件:① 仅 Development Build 编译 ② 玩家构建中不存在该代码路径(= E2 的运行面)③ **不得显示 raw 轴值的数值**(显示数值会诱导实现去「调数字」而不是「调手感」—— 同 13 调试视图禁显病种的理路)。视图内容 = 当前动作值(非 raw 数值形态)· 设备态 · 直读通道态 · 最近一次意图 · overrides 装载结果(hit / mismatch-cleared)—— **「当前焦点栈」项已删**(三轮修正,与 AC-C3 冲突)

---

## Implementation Notes

*Derived from GDD §UI Requirements 二 + `AC-3-E2` 原文;ADR: N/A(见 Context):*

- **这是 3 唯一的「UI」**,且不出现在玩家构建里(§UI 二)—— 取向同 13 / 51 / ADR-019 §五「开发者切面」
- **三条件的分工**:①=`#if` 包裹(E2① 源侧断言)②=玩家构建无此路径(E2② 符号断言,运行面)③=禁显 raw 轴值(类型/内容断言)
- **禁显 raw 轴值的口径**:视图可显示**归一化后的语义值**(如「方向:上」/归一输出的定性呈现),**不得**显示原始轴浮点数字 —— 防诱偏,不是防泄密
- **E2② 载体边界**:命名工具/脚本(如 `tools/ci/player-symbol-check`)本故事可交付并**本地可跑**(对已生成 player 断言);`.github/workflows/` 的命名 job 归 ADR-012 CI 轮(仓库现状:workflows 与 tests 目录**均不存在**,GDD §Dependencies 五原文)
- **焦点栈项已删**(三轮修正 D-…):调试视图**不读 42 焦点栈** —— 复活该项 = 与 AC-C3(BLOCKING)正面冲突 + 复活规则十三已作废的 `42 → 3` 边;焦点信息在 42 自有调试视图(承 ADR-013 §9 C3)。本故事实现须把该条目从显示清单**排除**

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010:热路径零分配(E1/E4/E5)—— 调试视图是**旁路观察者**,不进热路径;其调用频度约束另测
- Story 009:焦点导航视图(009 的 `FocusNavigationIntent` 只读视图 ≠ 本调试视图的焦点栈显示 —— 后者已删)
- CI 命名 job(ADR-012 轮)· `tests/` 目录树建制(同批)
- 玩家可见 UI(§UI 一:P0 无 —— 改键页 `OQ-3-1`,归 49 / 42)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). UI 类型 —— Manual check + 源侧可自动化项混合:*

- **AC-3-E2①**: 调试块被 `#if` 包裹(源侧断言)。
  - Setup: 全部调试视图代码文件。
  - Verify: 逐块检查 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹;未包裹块计数 = 0。
  - Pass condition: 静态扫描输出 0 个未包裹调试块(自动化断言,可入单测)。
  - Edge cases: 部分包裹(类包裹但条件缺 `DEVELOPMENT_BUILD`)⇒ 红;`#if` 内嵌 `#if` 深层漏出。
- **AC-3-E2②**: 已生成 player 符号检查(工具本体可跑)。
  - Setup: 一个 Development player 与一个 Release player(或符号表导出)。
  - Verify: 运行命名工具对 Release 产物断言目标符号(调试视图入口)不存在;对 Development 产物断言存在(正反两向)。
  - Pass condition: 工具**本地可跑**并出红/绿结论;CI 命名 job 本体**不在本故事验收**(挂 ADR-012 轮,报告中记挂账)。
  - Edge cases: IL2CPP 符号名经 mangling(工具须处理);Mono 与 IL2CPP 差异(双后端各出一行)。
- **AC-3-UI-2**: 三条件 + 内容清单。
  - Setup: Development Build 内打开调试视图;Release Build 检查。
  - Verify: ① Dev 可见、Release 不可见 ② 视图显示五项内容(动作值〔非 raw 数字〕· 设备态 · 通道态 · 最近意图 · overrides 装载结果)③ **无 raw 轴浮点数值**④ **无「当前焦点栈」条目**。
  - Pass condition: 四项全过;raw 数值以字符串/数字形态出现(如 `0.8732`)⇒ ③ 红。
  - Edge cases: `hit` 与 `mismatch-cleared` 两种 overrides 结果切换时视图更新;Release Build 中连符号都没有(E2② 保障)。

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- UI: `production/qa/evidence/developer-debug-view-evidence.md`(手动走查:Dev 可见 / Release 不可见 / 无 raw 数值 / 无焦点栈条目 + 签核);E2① 源侧断言可另附自动化测试

**Status**: [ ] Not yet created
- 真身落点注记:自动化断言(如落地)随 EditMode 树 `unity/Assets/Tests/EditMode/InputSystem/`;证据文档路径按登记口径 `production/qa/evidence/`

---

## Dependencies

- Depends on: Story 001(视图观察的是唯一动作资产上的 action 值)
- Unlocks: None(开发期工具,不被下游故事消费;调试视图显示「最近意图」读 Story 006 交出物)
