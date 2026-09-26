# Story 007: 急救直读通道与输入更新相位

> **Epic**: 输入与设备
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/gdd/input-system.md`(规则七 · §Formulas F-3.3)
**Requirement**: TR-input-006(急救动作走独立直读通道,不穿 42 UI 栈;`L_input < 50 ms` 只测预表现路径)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主): 输入架构 —— **Engine Risk HIGH**
**ADR Decision Summary**: ADR-011 §二 —— 直读在 `InputSystem.onAfterUpdate` 回调(**非轮询**),理由 = **方差不是算术**(UI 路由 = 帧内调度,方差不可控的链不可练);Amendment B —— 直读产出聚合上行、主机权威,**本地判定仅预表现**;§Risks-A **spike S1**(updateMode 必须钉死)与 **S3**(onAfterUpdate 相位 —— 每帧恰一次,失败则相位确定性并入 10 误差带并登记修订)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: ADR-011 Knowledge Risk HIGH 的**核心面** —— `InputSystem.updateMode` 枚举与 `onAfterUpdate` 相位行为均 post-cutoff 零覆盖,spike S1/S3 是前置(性质判据已钉:相位 = 渲染帧相位、每帧恰一次)。降级路径(ADR-011 §Risks-A):spike 失败**不改判据,只改实现载体**(相位确定性并入 10 的误差带并登记修订)—— 本故事的性质断言不因 spike 结果改写。

**Control Manifest Rules (this layer)**:
- Required: 直读 = `InputSystem.onAfterUpdate` 回调(manifest Core · 输入(ADR-011))· 启动期相位性质断言(AC-B2③)
- Forbidden: 轮询式读取(每帧 `Update` 里主动 `InputSystem.Update`)· 直读程序集引用 UI Toolkit / `UnityEngine.UI` / `EventSystem`(AC-B2①,构建失败)
- Guardrail: `L_input < 50 ms` **只测预表现路径**,禁合并成「端到端」(manifest Guardrail;`L_eval` 归 9 的求值节奏,不得重新合并)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [x] **AC-3-B1a(BLOCKING · P0)**: **零硬件合成注入** —— 合成输入注入 → **输入 → 读数可见性 ≤ 1 帧且同帧可见**;四轮覆写:**断言对象 = 读数 `EmergencyReading`**(原「输入→判定」不可签核,判定归 10)
- [x] **AC-3-B2①(BLOCKING)**: 直读程序集 asmdef **不引用** UI Toolkit / `UnityEngine.UI` / `EventSystem`(构建失败)
- [x] **AC-3-B2②(BLOCKING)**: **Roslyn 拒 UI 事件符号**(编译期拒,非 grep)
- [x] **AC-3-B2③(BLOCKING)**: 启动期性质断言「**输入更新相位 = 渲染帧相位,每帧恰一次**」—— EditMode 对偶断言:`Time.frameCount` +1 ⇒ 输入采样计数**恰 +1**(排除 Fixed 0/2 次与 Manual 竞争)。**不得以枚举成员名为主语**(符号零覆盖,分层处置:性质不变则判据不改,实现载体可换 —— 降级路径承 ADR-011 §Risks-A)

---

## Implementation Notes

*Derived from ADR-011 §二 / §Implementation Guidelines 3 / §Risks-A S1·S3 + GDD 规则七:*

- **方差不是算术**(ADR-011 §二 核心理由):`< 50 ms` 预算里最大的敌手是**帧内调度的方差**,不是平均耗时;直读通道绕开 UI 事件栈 = 绕开方差不可控的链。F-3.3 单判据 `L_input → pixel ≤ 50 ms` 归 Story 011 实测
- **回调而非轮询**(Guideline 3):挂 `InputSystem.onAfterUpdate`,**不在 `Update` 里主动驱动**;S1 spike 确认 `updateMode` 钉死为渲染帧相位(Dynamic;旋钮表明列,失败路径见 §Risks-A)
- **B1a 断言对象** = `EmergencyReading` 可见性(**不是**「输入→判定」—— 判定归 10,签核不了):合成注入一帧内,同帧读到非默认 `EmergencyReading`;≤1 帧由帧计数断言
- **B2③ 是性质断言不是枚举断言**:只测「渲染帧 +1 ⇒ 采样 +1」的对偶关系 —— 不写 `updateMode == Dynamic` 字面比较(枚举名 post-cutoff 零覆盖);spike 若推翻现载体,性质与判据**原样保留**,只换实现并登记修订(ADR-011 §Risks-A 降级路径)
- **三道门同属「不穿 42」**:①asmdef 引用集(结构)②Roslyn 拒符号(编译)③相位对偶断言(行为)—— 三者缺一,B2 不闭合
- `L_render`(≈2 帧)与 `L_poll` 诊断分解归 Story 011;本故事只保证通道本身相位正确
- ⚠️ **`EdgeTicks` 无防御性拷贝(2026-09-26 评审 G9 移交)**:`EmergencyReading.EdgeTicks` 是
  `int[]` 直传(Story 006 的类型形状,承 `EmergencyAggregator.AppendEdges` 的**差值追加**语义 ——
  通道每帧可复用**同一数组实例**、只增长前缀)。**本故事装配直读通道时,喂给 `Sample` 的
  `EdgeTicks` 不得跨帧缓存**:要么每帧新数组,要么在 `Sample` 返回后立即丢弃该引用
  —— 通道不得把该数组存进任何字段(它会被下一帧覆写)。若本故事引入跨帧持有,
  聚合器的沿数组会读到被改写的内容 ⇒ `Edges` 与数组内容不一致(静默数据损坏)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006:`EmergencyReading` 的类型定义与交出物闭集(B3)—— 本故事只管**怎么读到它**
- Story 010:直读回调路径的零分配(E5)—— 相位对了之后再测分配
- Story 011:`L_input → pixel` 硬件实测与 `L_render`/`L_poll` 分解(B1b)—— 需真实硬件,设计阶段 ADVISORY
- 主机侧 `Judge` 与 `L_eval`(归 9;两预算不得合并 —— manifest Guardrail)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-B1a**: 合成注入 → `EmergencyReading` 可见性 ≤1 帧且同帧可见。
  - Given: 直读通道已挂载(updateMode 已按 S1 spike 钉死);`EmergencyReading` 处于默认值。
  - When: 在注入帧内以合成方式触发急救动作输入(零硬件)。
  - Then: **同帧**读到非默认 `EmergencyReading`(边缘标志/时长至少一项变化);帧计数断言可见性延迟 ≤1 帧。
  - Edge cases: 注入发生在渲染帧末尾 vs 开头(相位不敏感 —— B2③ 的对偶断言保证两处等价);连续两帧注入(两次都同帧可见)。
  - Negative fixture: 通道改成轮询且晚一帧更新 ⇒ 可见性 =2 帧 ⇒ 红。
- **AC-3-B2①**: 直读程序集 asmdef 不引用 UI 栈三件。
  - Given: 直读程序集 `.asmdef`。
  - When: 解析 `references`。
  - Then: 不含 UI Toolkit 包程序集、`UnityEngine.UI`、含 `EventSystem` 的程序集(否则构建失败)。
  - Edge cases: 经输入程序集**间接**可达(引用集传递闭包收紧);夹具加一条 `UnityEngine.UI` 引用 ⇒ 红。
  - Negative fixture: 夹具 asmdef。
- **AC-3-B2②**: Roslyn 拒 UI 事件符号。
  - Given: 测试源含 UI 事件 API 引用(如 `EventSystem` / `NavigationMoveEvent` 处理订阅)。
  - When: 以分析器编译。
  - Then: 编译失败且诊断指向该引用;合法 Input System 写法通过。
  - Edge cases: 全限定名与 `using` 短名两种写法均拒;别名/反射字符串不算(编译期符号面)。
  - Negative fixture: 只 `using UnityEngine.EventSystems` 不触符号 ⇒ 不误报。
- **AC-3-B2③**: 相位性质对偶断言 —— 渲染帧 +1 ⇒ 采样 +1。
  - Given: EditMode 驱动输入系统推进(测试内可控的帧步进)。
  - When: `Time.frameCount` 前后差 = 1。
  - Then: 直读通道采样计数差**恰 = 1**(排除 0 次与 2 次;覆盖 Fixed 更新 0/2 次与 Manual 驱动竞争的失效形态)。
  - Edge cases: 一次推进跨多帧 ⇒ 每帧恰一次累积;手动 `InputSystem.Update()` 干跑一次 ⇒ 采样计数**不得**双计(若双计 ⇒ Manual 与自动更新竞争被抓)。
  - Negative fixture: updateMode 被改成 Fixed ⇒ 帧 +1 时采样 0 或非 1 ⇒ 红;**断言内不得出现 `updateMode == Dynamic` 枚举字面比较**。
- **架构/测试边界说明**:S1/S3 spike(真实引擎 `updateMode` / `onAfterUpdate` 行为)**是本故事实现的前置**,结果记录在故事 Completion Notes(实现阶段);spike 失败不改本组判据,只改载体并登记修订(ADR-011 §Risks-A)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input_system/direct_read_channel_test.cs` — must exist and pass(含 B1a 同帧可见性与 B2③ 对偶断言)

**Status**: [x] Created — EditMode 真身 32 测 + PlayMode 8 测 + spike 探针 2 测全绿(见 Completion Notes)
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 真身落 `unity/Assets/Tests/EditMode/InputSystem/direct_read_channel_test.cs`,PlayMode 相位类断言落 `unity/Assets/Tests/PlayMode/`,文档路径 `tests/integration/input_system/` 为登记口径

---

## Dependencies

- Depends on: Story 001(直读挂在唯一动作资产的 `Emergency` action 上)
- Unlocks: Story 010(直读回调 = E5 零分配的测量窗口),Story 011(通道就位才能实测延迟)

---

## Completion Notes
**Completed**: 2026-09-26
**Criteria**: 4/4 passing(AC-3-B1a / AC-3-B2① / AC-3-B2② / AC-3-B2③,零 UNTESTED;G9 handoff 同批闭合)
**Deviations**: 两条登记项(均非缺陷,属载体/证据面诚实化注记,不改任何判据)—— ① **us-F1(B2③ 载体偏移)**:story 原文写「EditMode 对偶断言」,实现的**真实执行面落 PlayMode**(EditMode 同步调用间 `Time.frameCount` 不推进 ⇒ 差值恒 0,Edit 侧断言只能结构性成立);**性质不变、判据不改,只换执行载体** —— 承 ADR-011 §Risks-A「性质不变则判据不改,实现载体可换」;EditMode 侧保留同名对偶 + 源扫描 E4(守「断言内不得出现 `updateMode ==` / 枚举成员名」),真实帧步进走 PlayMode ② **qa-F15(残余风险)**:Unity 装载分析器(DLL + RoslynAnalyzer label)的行为只由间接证据推断(DLL 在位 / label / platform off / freshness sidecar 四测),无直接断言「Unity 编译确实执行了该分析器」的探针 —— 残余风险登记,不阻塞
**S1/S3 spike 结果**(2026-09-26,5 次 batch run 全一致,`Logs/story007_spike_results.txt`):
- **S1**:`updateMode = ProcessEventsInDynamicUpdate`(int = 1;is_manual=False / is_fixed_only=False)—— **钉死渲染帧相位 ✅**
- **S3**:8×`yield null` 每步 `frame_advance=1 ∧ on_after_update_delta=1` —— **每帧恰一次 ✅**
- **S3 环境依赖发现**:CI batch(未聚焦 / -nographics)下手动 `InputSystem.Update()` 干跑 `on_after_update_delta = 0`(期望 1)—— **手动干跑不触发 `onAfterUpdate` 是环境依赖行为**(聚焦环境 / 玩家构建下方差路径不同)⇒ **同帧去重闸落地**(`_lastCallbackFrame`),其机制验证走 `NotifyAfterUpdateForTest()` 测试缝同帧双调(qa-F3);段 II 手动干跑不消费事件队列 ⇒ 持续 hold 不发 `EndAttempt` 的语义面由同缝直接测;`post_manual_frame delta=1` 确认手动段不污染帧-采样对偶
- **spike 降级路径未触发**(ADR-011 §Risks-A:失败才换载体并登记修订;现载体原样成立)
**Test Evidence**: Integration —— EditMode 真身 `unity/Assets/Tests/EditMode/InputSystem/direct_read_channel_test.cs`(32 测)+ PlayMode `unity/Assets/Tests/PlayMode/direct_read_channel_phase_test.cs`(8 测)+ spike 探针 `story007_phase_spike_playmode_test.cs`(2 测);复跑全绿 **EditMode 784/784 · PlayMode 22 passed / 0 failed / 3 skipped(既有 Ignored 的 U1SceneSpikesTest)· 双 exit 0**(`unity/Logs/build-story007-reviewfix-editmode.log` / `-playmode.log`)
**Code Review**: Complete —— 会话内 `/code-review` 双代理并行:unity-specialist **us-F1~F8 全修** + qa-tester **F1~F15 全修**(全部 findings 含 minor);复跑双套件全绿后收口
**Traceability**: AC-3-B1a → EditMode `test_direct_read_channel_b1a_injected_reading_visible_same_frame` / `_consecutive_injections_both_visible_same_frame` / `_idle_rejects_feed_default_reading` + PlayMode `_b1a_injected_reading_visible_same_frame_wired`(同帧可见 · ≤1 帧 · 连续两帧注入)· AC-3-B2① → `test_direct_read_gate_b2_1_*` 族(真图零 UI 闭包 / 直引红 / 间接红 / 引擎叶非误报 / RunAll 接线 / precompiled 捕获 / precompiled 边达闭包;qa-F12 = asmdef JSON `precompiledReferences` 并入闭包图,引擎 AssemblyRef 刻意不读)· AC-3-B2② → `test_direct_read_gate_b2_2_*` 族(DLL+label / platform off / freshness sidecar / 全限定 typeof 红 / using 短名红 / 别名成员红 / **声明面红(us-F5)** / 负例 using-only 不误报 / 反射字符串不算 / 作用域外不门 / InputAction 合法)· AC-3-B2③ → PlayMode `test_direct_read_phase_frame_plus_one_yields_sample_plus_one` / `_manual_update_dry_run_does_not_double_count` / `_fixed_mode_negative_fixture_caught` / `_sampling_completes_in_input_update_not_behaviour_update`(qa-F4 相位探针)/ `_same_frame_second_callback_deduplicated` / `_reattach_same_frame_no_double_count` + EditMode 源扫描 `_phase_test_source_has_no_update_mode_enum_comparison`(E4)+ 夹具恢复 `_fixture_restores_global_input_state` · **G9 handoff** → `test_direct_read_channel_g9_no_defensive_copy_field_held`(反射断通道无 `int[]` 字段)+ `_idle_callback_does_not_advance_hold_ticks`(Idle 采样零推进)—— **0/4 UNTESTED**
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
**提交**: `9f35fff`(实现批)· `eaf5da3`(评审修复批)—— 均已 push
