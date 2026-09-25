# Story 010: 热路径零成本(预缓存 · Idle 零调用 · 零分配)

> **Epic**: 输入与设备
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-25

## Context

**GDD**: `design/gdd/input-system.md`(§Edge Cases 三 · §Tuning Knobs 五)
**Requirement**: TR-input-012(热路径零字符串查找、Idle 零调用、Armed 零分配)· ⚠️ E4 / E5 无专属 TR —— 直引 GDD `AC-3-E4` / `AC-3-E5`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time;E4/E5 权威出处 = GDD AC 原文)*

**ADR Governing Implementation**: ADR-011(主): 输入架构
**ADR Decision Summary**: ADR-011 §Performance / §Implementation Guidelines 2 —— 预缓存 `InputAction` 引用(禁热路径字符串查找);`Emergency` 动作平时 **`Disable()`**(GDD States 通道态 Idle = 唯一真零成本);Armed 时回调路径零分配 —— 这是「直读」相对轮询的性能下半边(上半边是方差,见 Story 007)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: E5 的工具面 = `ProfilerRecorder` GC.Alloc 计数,**须 Development Build**,Mono + IL2CPP **双后端**各跑 —— post-cutoff 工具行为零覆盖(ADR-011 HIGH 直接落点)。E1/E4 是 Roslyn + EditMode 断言(结构/行为),MEDIUM;HIGH 来自 E5 的采集工具与后端差异面。方法学四要素(统计量/窗口/剔除/工具)缺一不可 —— 与 B1b 同纪律。

**Control Manifest Rules (this layer)**:
- Required: 热路径预缓存 `InputAction` 引用(manifest Core · 输入(ADR-011);AC-E1)· Idle = `Emergency.Disable()`(States 通道态)
- Forbidden: `FindAction(string)` / `FindActionMap(string)` / `InputActionAsset` 字符串索引器出现在热路径(AC-E1,Roslyn 守门)
- Guardrail: 16.6 ms 帧预算(manifest Performance)—— 零分配判据是「一次即败」非「平均」(AC-E5 方法学 ①:统计量 = 分配字节数**上界** 0)

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] **AC-3-E1(BLOCKING)**: **预缓存** —— 热路径**零每帧字符串查找**;禁 `FindAction(string)` / `FindActionMap(string)` / `InputActionAsset` 字符串索引器(Roslyn 守门)
- [ ] **AC-3-E4(BLOCKING)**: **Idle 零成本** —— `Emergency` 动作 `enabled == false` ∧ 读取回调调用次数 `== 0`(回应「已 enable 的 action 每帧都被 Update 更新」的质疑 —— Idle 态必须是**真零调用**,不是「调了返回默认」)
- [ ] **AC-3-E5(BLOCKING)**: **Armed 零分配** —— 读取回调 `GC.Alloc == 0`;方法学四要素在测试报告明写(缺一不可):① **统计量** = 分配字节数**上界 0**(非平均 —— 一次即败)② **窗口** = 连续 ≥ N 帧 Armed 全程(N 用户定)③ **剔除** = 显式预热帧(用户定)④ **工具** = 分配探针(`ProfilerRecorder` GC.Alloc,**须 Development Build**,Mono + IL2CPP **双后端**);⑤ **扩展** = 非急救帧的常驻轴路径(`Move`/`Look` 逐帧**含 `Fix → float` 转换**)`GC.Alloc == 0`

---

## Implementation Notes

*Derived from ADR-011 §Performance / §Implementation Guidelines 2 + GDD §Edge Cases 三 / §Tuning Knobs 五:*

- **预缓存纪律**(E1):初始化时取 `InputAction` 引用入字段;热路径(每帧/每回调)只走字段。Roslyn 拒三类字符串入口出现在高频路径 —— 同义词表登记(防经 `asset["Map"]["Action"]` 索引器绕过)
- **Idle 是唯一真零成本点**(E4 / States 通道态):`Emergency.Disable()` 让 Input System **停止更新该 action**(回调注册不触发)。断言两半:① `enabled == false` ② 回调计数 `== 0` —— 只断①会被「disable 了但回调仍挂」骗过
- **状态机三态的成本含义**:Idle = 零调用(E4)· Armed = 调用但零分配(E5)· Reading = 每帧读(分配同 E5 约束);Suspended(失焦)= 合成 release(归 D3)。**「Armed zero cost」是错的** —— Armed 有调用,只是无分配(E4 断的是 Idle)
- **E5 方法学四要素**(测试报告必写,缺一 = 不可签核):上界而非均值(偶发一次分配也算败)· 连续窗口而非瞬时 · 预热帧显式剔除(冷启动分配不算)· 工具与后端明写(Development Build 才有 `ProfilerRecorder`;Mono 与 IL2CPP 各出一行结论)
- **子句 ⑤ = 常驻轴路径**:非急救帧里 `Move`/`Look` 每帧走 `Fix → float` 转换 —— 这条路径**同样** `GC.Alloc == 0`(boxing/闭包/临时数组是常见泄漏点);与 Story 002 的轴函数是同一条代码路径
- N(窗口帧数)与预热帧数是**用户旋钮**(Tuning Knobs),本故事只交机制与报告格式

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:F-3.1 轴算术本身(本故事只测其调用路径的分配)
- Story 007:直读通道相位(通道就位是 E4/E5 的测量前提 —— Depends on)
- N / 预热帧**数值** —— 用户数值轮
- `L_input` 延迟(归 Story 011)—— 零分配 ≠ 低延迟,两判据独立

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-3-E1**: 热路径零字符串查找。
  - Given: 3 的输入程序集源码。
  - When: Roslyn 扫描热路径(每帧 / 回调)调用点。
  - Then: `FindAction(string)` / `FindActionMap(string)` / 字符串索引器零出现;初始化路径(一次性)允许。
  - Edge cases: 索引器 `asset["Map"]["Action"]` 形态(同义词表);经私有方法间接调用(调用链可达性收紧);一次性缓存构建用字符串(豁免面须白名单明示)。
  - Negative fixture: 夹具回调里 `asset.FindAction("Emergency")` ⇒ Roslyn 红。
- **AC-3-E4**: Idle 态 `enabled == false` ∧ 回调计数 `== 0`。
  - Given: 通道态 = Idle(`Emergency.Disable()`)。
  - When: 推进 ≥2 帧(经 Story 007 的直读相位)。
  - Then: `enabled == false`;读取回调调用计数**恰 = 0**(两半同时断)。
  - Edge cases: Armed→Idle 迁移后**下一帧**起计数为 0(迁移帧允许 1 次);Suspended 后回 Idle;反复 Enable/Disable 抖动后计数归零。
  - Negative fixture: 只 `enabled=false` 但回调仍被调(计数 >0)⇒ 第②半红 —— 这正是 AC 要回应的失效形态。
- **AC-3-E5**: Armed 读路径 `GC.Alloc == 0`(含子句 ⑤ 常驻轴)。
  - Given: Development Build;显式预热帧(数);连续 ≥ N 帧窗口;Mono 与 IL2CPP 各一次。
  - When: `ProfilerRecorder` GC.Alloc 探针在窗口内计数。
  - Then: Armed 读路径分配字节数**上界 = 0**(一次即败);**同一探针**下非急救帧常驻轴路径(`Move`/`Look` 含 `Fix → float`)亦 = 0。
  - Edge cases: 首帧/冷启动分配(预热剔除,窗口外);`Fix → float` 的 boxing 泄漏(子句 ⑤ 专抓);IL2CPP 与 Mono 结论不一致 ⇒ 以**双后端全过**为准(任一红即败)。
  - Negative fixture: 回调里 `string.Format` / 闭包捕获 / `List` 新建 ⇒ 红;**报告缺四要素任一**(无统计量口径 / 无窗口帧数 / 无预热说明 / 无工具与后端)⇒ 签核无效(AC 原文「缺一不可」)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input_system/hot_path_zero_cost_test.cs` — must exist and pass(E5 需 Development Build 记录贴入测试报告段;真身可为 PlayMode + 文档路径登记口径)

**Status**: [ ] Not yet created
- 真身落点注记:Unity 只编译 `unity/Assets/` 树 ⇒ EditMode 断言(E1/E4)落 `unity/Assets/Tests/EditMode/InputSystem/hot_path_zero_cost_test.cs`;E5 Development Build 探针记录落 `production/qa/evidence/` 附录,文档路径 `tests/unit/input_system/` 为登记口径

---

## Dependencies

- Depends on: Story 001(预缓存的对象是唯一资产上的 action)· Story 007(直读通道就位,E4/E5 才有可测的回调)
- Unlocks: None(性能收口,下游故事不依赖其产出)
