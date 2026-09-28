# Story 002: 只读投影 Proj 与装配护栏

> **Epic**: 教学与引导
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/tutorial-and-onboarding.md`(规则四 零持久化 · 规则四之二 Proj 提供者形态 · AC-48-03 / AC-48-04 / AC-48-11)
**Requirement**: TR-tutorial-001(教学进度零落盘,covered) · TR-tutorial-003(Proj 落哪个 asmdef —— **partial**:ADR-025 未点名,本 story 按清单就近落,不得记绿)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 世界状态事件化边界(派生态判据)· ADR-019 §三: 51 同构先例(边界层只读消费者,不写流)· ADR-025: 契约程序集清单 · ADR-010: 持久化(48 在存档布局中**无槽位**)
**ADR Decision Summary**: 教学进度 = 事件流的**读时投影**(派生态,Q1 判据),零持久化、零独立状态。Proj 提供者住边界层(与 51 的 AC-51-A2 同构):**只读消费者** —— 48 不是第四个 `IEventSink` 写入者,不持 `IEventAuthority`。装配护栏双守:① 48 交付物的公开面**不得引用** `SimEvent` / `PatientState` / `Fix`(递归反射);② asmdef 引用白名单禁 `IEventSink` / `IEventAuthority`(编译期,非 grep,AC-48-11)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 投影 + 反射断言;零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 投影 = 纯函数(同流前缀同输出);每次读取现算,类无可变字段(进度住在闩,见 Story 003)
- Forbidden: 任何写路径(`Append` / `Publish` / 存档段);48 侧缓存「已教到第几步」进可序列化类型
- Guardrail: 投影输入 = 三流只读快照(边界层给);48 不自行遍历流存储(消费面 = 只读接口,与 39 的投影纪律同形)

---

## Acceptance Criteria

*From GDD `design/gdd/tutorial-and-onboarding.md`, scoped to this story:*

- [ ] `Proj(streamSnapshot, player_id) → 教学谓词输入` 为纯函数;六步谓词各自的判据事件(CaseOpened / JudgmentRecorded(author=p) / EmergencyAttempt·DrugTreatmentApplied(initiator=p) / CaseClosed / Checkpoint written / StructurePlaced)可从投影读出
- [ ] **AC-48-03**:教学进度零落盘 —— 48 无任何存档写入路径;存档字节流不含 tutorial 进度字段(与无 48 的基线存档逐字节一致);区分 ADR-014 内容加载(读 `.cooked` 合法,非进度落盘)
- [ ] **AC-48-04**:48 程序集公开面的递归反射扫描零命中禁引类型(`SimEvent` / `PatientState` / `Fix` 及其嵌套泛型参数)
- [ ] **AC-48-11**:48 asmdef `references` 白名单不含 `IEventSink` / `IEventAuthority` 所在程序集(编译期失败,非 grep)
- [ ] 联机各玩家投影独立:同一 `Proj` 对两个 `player_id` 出两条独立结果(P0 单机夹具 = 两个发号 id 各投影;联机运行时 **BLOCKED-BY-45**,禁借绿)
- [ ] 投影不可变性:同一输入两次求值字节一致;追加一条流事件 ⇒ 输出仅按单调性增长(不回收)

---

## Implementation Notes

*Derived from ADR-009 / ADR-025 Implementation Guidelines:*

1. 落点:`Gameplay.UI` 内 48 模块 + 边界层只读快照接口(投影不 new 事件、不解析载荷二进制 —— 拿到的已是解码后的只读视图)。
2. 「archive 步谓词 = Checkpoint 已写」**不是流事件** —— 投影输入需带 7a 的「最近 checkpoint 序号/锚点回执」只读口(规则八白名单例外的运行期侧;若 7a 无此口,登记接口需求给 7a 实现轮,本 story 以夹具时钟替代并留 TODO,**不私开第二条通道**)。
3. 护栏扫描器与 Story 001 校验器分开:本 story 的反射断言 = EditMode 测试(`TutorialAssemblyGuardTest`),照 `PresentationDtoGuard` 递归形制。
4. ⚠️ **数值冻结**:投影无旋钮(全机制);媒体时序类旋钮在 Story 004。
5. TR-tutorial-003 partial 的处理:在 asmdef 与 story 注释双向登记「落点为就近解释,待架构回写」—— 回写前不得把本 story 记成 TR closure。

---

## Out of Scope

- [Story 003]: 两相触发(Anchor/Completion)与闩的求值消费本投影
- [Story 005]: 零设门反射断言(48 API 面查询禁项 —— 那是「暴露面」守卫,本 story 是「引用面」守卫)
- 7a checkpoint 回执口的正式实现(归 7a epic;本 story 夹具)
- 联机运行时独立进度(45 轮)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 投影纯函数性
  - Given: 固定事件流夹具 A(含 6 类谓词事件各 ≥1)与截断前缀 A'
  - When: `Proj` 各跑两次 + 前缀对比
  - Then: 同输入同输出;`Proj(A')` ⊑ `Proj(A)`(单调,无回收)
  - Edge cases: 空流 ⇒ 全谓词 false;世界级事件(Patient=-1)不误入作者过滤

- **AC-2**: 零落盘(AC-48-03)
  - Given: 同种子跑两条会话:一条走教学全流程、一条跳过教学,存同一时刻档
  - When: 存档字节对比
  - Then: 无 tutorial 专属字段差(允许其他系统正常差异);48 写路径调用计数 = 0
  - Edge cases: 读 `.cooked` 内容发生在装载期,不算落盘(断言只扫写向)

- **AC-3**: 引用面双守(AC-48-04 / AC-48-11)
  - Given: 故意在测试分支加一个 `SimEvent` 参数的 public 方法
  - When: 跑反射守卫 + 尝试编译
  - Then: 守卫失败可复现(负夹具);asmdef 白名单违规 = 编译错误
  - Edge cases: 嵌套 `List<Fix>` 也被递归命中(泛型参数扫描)

- **AC-4**: 双玩家投影独立
  - Given: 4 人夹具中的 p1/p2 各落笔判断
  - When: `Proj(E, p1)` 与 `Proj(E, p2)`
  - Then: judge 谓词各自 true 互不可见;treat 谓词按 initiator 过滤
  - Edge cases: 联机真环境运行面 BLOCKED-BY-45(单机夹具不替代其 closure)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_projection_test.cs` + `unity/Assets/Tests/EditMode/Tutorial/tutorial_assembly_guard_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(步定义 `.cooked` 就位,谓词→Kind 映射可查)· 边界层三流只读快照(既有基建)
- Unlocks: Story 003(触发引擎消费 Proj)· Story 004(媒体调度读投影状态)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
