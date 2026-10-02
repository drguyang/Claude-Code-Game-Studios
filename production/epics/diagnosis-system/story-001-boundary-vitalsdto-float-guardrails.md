# Story 001: 程序集边界与 VitalsDto 只读门面

> **Epic**: 诊断与体征揭示
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/diagnosis-system.md`(边界五条铁律 · F-8.6 8 不在定点域内 + 护栏 G-1…G-4 · 规则一 只读不写不持状态)
**Requirement**: TR-diag-001(8 位于唯一浮点出口之后 IVitalsQuery→VitalsDto)· TR-diag-002(铁律① 8↔11 无数据流)· TR-diag-003(铁律② 不回写 9)· TR-diag-004(铁律③ 不拥有持久化)· TR-diag-007(F-8.6 8 在定点域之外)· TR-diag-010(G-4 位宽固定 System.Single,禁 FMA 收缩依赖)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(`IVitalsQuery` 抽象点 + `VitalsDto` 全案唯一浮点出口)· ADR-006(边界契约;8 出浮点**设计上不违** ADR-005,但受护栏)· ADR-013 §9 C3(42/8 只渲染/只读,永不持游戏状态;`PresentationDtoGuard` 递归扫描)· ADR-025(装配落点:`Gameplay.UI`/`Gameplay.Presentation` 侧,零 sim 内部类型引用)
**ADR Decision Summary**: 8 的输入**只经** `IVitalsQuery`/`VitalsDto`(不引用任何 sim 内部类型;8 侧不存在 `ToFloat` 之外的浮点入口);8 **零** `Publish`/写入点;重启不丢东西 = 8 不持跨调用累积量;`Math.Sqrt` 出现在 sim 程序集即违门 B(AC-8-6 2026-09-23 订正),8 侧走**预计算定表**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(边界断言 = EditMode 反射/IL;承 audio story-001 已趟平的 `AssemblyGates` + `PresentationDtoGuard` 双层机制 —— 复用,不新建扫描器)
**Engine Notes**: G-4(禁 FMA 收缩)须 IL2CPP 实测定论:IL 层断「或走定表、或显式 Mul 后 Add」;跨 CPU/IL2CPP 收缩差异面列 AC-8-F5 矩阵(归 story 004)。

**Control Manifest Rules (this layer)**:
- Required: 8 的全部输入 = `VitalsDto`(float 只进呈现/求值门面);8 的全部输出 = 呈现意图 + 成长意图(经门控出口)
- Forbidden: `IEventSink` 写入、`Publish` 调用点、存档 API、sim 内部类型引用、libm 超越函数(G-1)、`UnityEngine.Random`/`System.Random`(F-8.4 前提)
- Guardrail: 「合法的字段」口径 —— 8 可持**当帧**呈现缓存,禁**跨调用累积量**(AC-8-3 括注口径)

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [ ] **AC-8-1**[I] BLOCKING:8 全流程期间 `IEventSink` 写入数 = 0;9 的 `SimEvent` 流哈希与「不经 8 的对照跑」逐位相同;静态守门 = 8 程序集零 `Publish` 调用点
- [ ] **AC-8-2**[L] BLOCKING:8 只引用 `VitalsDto`/`SimEvent` 等公开类型,零 sim 内部类型;不存在 `ToFloat` 之外的浮点入口
- [ ] **AC-8-3**[L/I] BLOCKING:同一病人 · 同一 Skill · 同一 `(Skill, sign_id)` 在**两个独立进程**求 `display_词`/把握度/四态 ⇒ 输出逐字相同;字段反射遍历 ⇒ 无跨调用累积量(读数/已查记录/病名/置信度/改写次数)
- [ ] **AC-8-4**[I] BLOCKING:8↔11 零数据流 —— 11 的输入契约不含病名字段;8→11 无任何数据边(支柱一守门;成对断言落 prescription epic story 003 的双判据,本 story 断 8 侧)
- [ ] **铁律③/④**[A]:8 零持久化 API 调用点(反射断言);`EmitGrowth` 唯一门控出口 = 主机 + `IIdAuthority`(TR-diag-005;调用语义归 story 005,本 story 断**存在唯一出口形状**)
- [ ] **AC-8-6(G-1 半边)**[A]:grep+IL 双层零 `Math.Pow/Exp/Log/Cbrt`;`READ_FLOOR`/`C_neg` 的插值与线性项「或走预计算定表,或显式 Mul 后 Add」(FMA 收缩不依赖;G-4)
- [ ] **TR-diag-019/020 前置**[A]:`PresentationDtoGuard` 挂入 8 的全部呈现 DTO 递归扫描(disease_id 不进呈现层;判据 = 反射,承 AC-37-15)

---

## Implementation Notes

*Derived from F-8.6 + ADR-025:*

1. 8 实现住 `Gameplay.UI`/`Gameplay.Presentation`(按 ADR-025 清单;与 42 的装配关系评审时重读清单)。
2. 定表化:`(1−s)^READ_GAMMA` 与 `s^NEG_GAMMA` 两族曲线烘焙期按 `Skill∈[0,60]` 整数档预计算为定表(F-8.1/8.3 的消费面,表生成逻辑归 story 003/004,本 story 锁「8 运行期查表不现算浮点幂」的形状)。
3. 双进程一致性夹具:测试内起两 AppDomain/进程等价物(EditMode 用纯函数入口 + 独立实例即可,不引多进程框架)。
4. AC-8-1 的对照跑夹具 = disease epic story 002 的空流基线;跨 epic 引用在 CI 层做(证据文件注明)。

## Out of Scope

- [Story 002]: 词条表 schema 与数据行
- [Story 003/004]: F-8.1/8.2/8.3 公式实现(本 story 只锁其浮点出口形状)
- [Story 005]: 状态机与成长门控调用
- [Story 006]: 呈现与走查类 AC
- 音频四条(TR-diag-021…025 归 audio-system epic)

## QA Test Cases

*Written at story creation(lean mode).*

- **零写入**: 全流程脚本化(查体+落笔+2 改写)⇒ Append/Publish 计数 0;流哈希 ≡ 对照(AC-8-1)。
- **引用面**: Cecil 扫 8 程序集 TypeRef ⇒ ∈ 白名单;影子负夹具引 `PatientState` 内部类型 ⇒ 红。
- **双进程逐字**: 两独立求值实例四输出逐字等(AC-8-3)。
- **11 契约**: 反射 11 输入类型集 ⇒ 无病名字段(AC-8-4)。
- **超越函数**: IL 扫 `call Math::Pow` 等 ⇒ 0;定表命中断言(同输入两次查表)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DiagnosisSystem/boundary_guard_test.cs` — must exist and pass
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: disease-simulation epic story 001/002(`Fix`/`VitalsDto`/`SimEvent` 形状)、audio-system epic story-001(`AssemblyGates`/`PresentationDtoGuard` 机制复用)、skeuomorphic-ui epic(装配清单)
- Unlocks: Story 002…006(8 的全部实现面)、prescription-medication epic story 003(AC-11-01 双判据的另一半)

## Completion Notes
