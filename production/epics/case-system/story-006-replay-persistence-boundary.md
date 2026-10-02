# Story 006: 重放持久化与跨系统边界义务

> **Epic**: 病例系统
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/case-system.md`(规则十一 重放/持久化边界 · EC-37-6 主机迁移 · 53 消费两条 · D-37-B · [V] 走查三条移交)
**Requirement**: TR-case-003(病例流不折叠) · TR-case-006/011 的持久化半边 · TR-case-025(53 延迟 + 不可归因,partial —— 载体在 53) · TR-case-028 重放半边 · TR-case-036(37 沉默 ≠ 世界沉默 —— 53 侧)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-010(主): 序列化/折叠;ADR-005: 逐位重放;ADR-008: 不折叠裁决
**ADR Decision Summary**: 病例流走 ADR-010 二进制 codec(禁 float 承 ADR-006;`Fix` 不进病例载荷,ordinal/int 直接编码);**病例流不物理折叠**(ADR-008 §六 —— 折叠豁免外的实现明确拒绝,历史行永久保留);7a 折叠谓词 `Folded(p)` 含「无未结案病例」(病史流折叠侧,义务归 7a,37 提供判据接口);**主机迁移逐位重放** = ADR-012 矩阵义务的消费面(AC-37-06):同一事件流在新主机重放 ⇒ 病例状态/FiredSet/MemberSet 逐位同;53 消费两条纪律:① `PatternRecognized` 按 **Tick** 聚合非到达序(哨兵 -1 前向悬垂,AC-37-28 载体=53);② 图样触达玩家的时机/延迟/不可归因全归 53(AC-37-25,① 已核 AC-53-06,② 待 AC-53-07);**D-37-B**(同源检测给病史流有界性论证新增行为学前提)登记转 9/7a,不在 37 内解决;[V] 走查三条(AC-37-16/17/19:脉案读感/串接可视/结案仪式)由 39/42 走查批承接留档。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 存档往返 = EditMode 文件 I/O(临时目录);IL2CPP 逐位对拍归 ADR-012 三格矩阵(常驻 Linux 三格先跑,禁借绿 —— 本 story 只交付 Mono 侧自测)。

**Control Manifest Rules (this layer)**:
- Required: 病例流序列化 round-trip 字节级相等;重放输入 = 流前缀纯函数(无 WallClock/无 UnityEngine.Random);`Folded(p)` 判据接口对 7a 可见
- Forbidden: 在 37 内实现折叠(谓词归 7a);为「迁移后病例丢了」写特例补偿(丢失 = codec bug,修它不绕它);把 53 的延迟策略回填 37
- Guardrail: AC-37-06 跨平台半边在 IL2CPP 矩阵跑齐前记 **BLOCKED-BY-ADR-012**,本 story 只交 Mono round-trip;[V] 三条移交后在本 epic DoD 留档指针

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-06(Mono 半边):含开案/结案/改写/已触发图样的复合存档 ⇒ 编码→解码→重放 ⇒ 病例状态集逐位同;迁移(新主机续跑)不产生分叉
- [ ] 不折叠验证:同一 `patient_id` 既往全部病例行在重放后完整可数(无历史行被终态吸收);`Folded(p)` 谓词对「有未结案病例」的 p 返回 false(接口断言,折叠执行归 7a)
- [ ] 高水位联查:病人 id 高水位扫描 = 三流并集(病史/病例/世界),病例流的哨兵行不污染(承 story-001,此处用真实存档路径复验)
- [ ] 53 边界两条登记断言:37 出站仅事件流本身(无直接接口调用 53);夹具证明「PatternRecognized 乱序到达 ⇒ 按 Tick 重排后 FiredSet 解读不变」(37 侧只做发出方纪律,消费方测试载体在 53 ⇒ 该半边记 NOT-RUN 挂 AC-53-04)
- [ ] D-37-B 转登:「同源检测引入结案行为 ⇒ 事件率新前提」写入 7a/9 的有界性登记(文档义务,PR 附链接)
- [ ] [V] 移交登记:AC-37-16/17/19 在 39/42 走查批的留档路径回填本 epic DoD(不在本 story 执行走查)

---

## Implementation Notes

*Derived from ADR-010/012 Implementation Guidelines:*

1. 复用 ADR-010 三流序列化的既有头部/流段布局,**病例流段**为本 story 的覆盖增量(如 7a 侧已预留则只做 round-trip 测试补齐,不重开格式)。
2. 重放测试驱动 = `ITickProvider` 桩按 tick 序列灌入,`CatchUp` 与逐 tick `Step` 双路径同果(ADR-005 性质)。
3. IL2CPP 半边:挂 ADR-012 矩阵 job 的黄金夹具清单(病例流 round-trip 字节档 `golden-vN` 刷新流程),本 story 提交期望值文件与刷新说明。
4. 53 侧两条(AC-37-25/28)的断言写法 = 「37 发出物无延迟语义字段」(载荷面) + 「Tick 单调可读」(全序面),消费面 NOT-RUN 点名 AC-53-*。
5. D-37-B 是**登记**不是实现:提交 = entities.yaml/tr-registry 注记 + 9/7a 文档的对应义务行(跨 epic 协作,producer 传播)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 载荷形状(本 story 序列化它)
- [Story 004]: 检测算法(本 story 验证其重放不变量)
- [Story 005]: 守密断言批(词表/DTO 与存档无关)
- 系统 7a:折叠执行/存档头版本迁移;53:延迟与不可归因消费;39/42:[V] 走查执行;ADR-012:IL2CPP 矩阵跑批

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(round-trip)**: 复合存档字节相等
  - Given: fixture 流 = 2 开案 + 1 结案(带快照)+ 2 改写 + 1 PatternRecognized
  - When: encode→decode→encode
  - Then: 两版字节相等;decode 后重放状态 = 原状态
  - Edge cases: 空病例流合法(零字节段 + 计数 0)
- **AC-2(不折叠)**: 历史行完整
  - Given: 病人 p 三案全结 + 病史流长前缀
  - When: 触发折叠语义的快照生成
  - Then: 病例流三行俱在;`Folded(p)` = 该病人病史流可折、病例流不折
  - Edge cases: 「有未结案病例」的 p ⇒ 病史流亦不折(判据接口返回 false)
- **AC-3(迁移分叉)**: 换主机续跑
  - Given: 主机 A 写到半程存档
  - When: 主机 B 加载并续 Step 100 tick
  - Then: 与 A 直跑同 tick 的流逐位同(含新 CaseClosed 的 Seq 续号)
  - Edge cases: B 上重放含同 tick 多 D 定序(承 story-004 AC-37-27)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_replay_persistence_test.cs` — must exist and pass(Mono 半边);IL2CPP 半边 `BLOCKED-BY-ADR-012` 矩阵批,53 消费半边 NOT-RUN 挂 AC-53-04
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Stories 001–004(全部被测对象);7a 序列化布局(ADR-010 既有);ADR-012 夹具刷新流程
- Unlocks: 本 epic DoD;39/42 走查批的留档指针;9/7a 的 D-37-B 登记

---

## Completion Notes

*(留空 — story 关闭时回填)*
