# Story 006: POI 载荷接线 —— `PoiStateChanged` 改走 `IPayloadEncoder`(闭合 B1)

> **Epic**: 6b 生态区与 POI
> **Status**: Complete ✅ 2026-10-02 (6/6 AC 落地;13 例新测试 + 突变测试坐实;WorldEcozones 106/106)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-02

## Context

**GDD**: `design/gdd/world-and-ecozones.md`
**Requirement**: 闭合双代理评审的 **B1** —— `PoiStateChanged` 未走 `Sim.Codec`。
🔴 **B1 的原处置是「加 TODO 注释」**(`43400dc` 自陈),而 `production/epics/index.md` 已明文判定
**「前置未就绪」的免责不成立**(codec 与 `IBlobPool` 均已在库)。

**ADR Governing Implementation**: ADR-029(✅ Accepted 2026-10-02)· ADR-021 §三(`PoiStateChanged` 世界流 Kind)· ADR-009 §七(三段式)· ADR-025 §①(`Sim` 引用集)
**ADR Decision Summary**: `Sim` 的写者经 **`IPayloadEncoder`**(第七抽象点,住 `Sim.Contracts`)编码载荷,
**零手搓 `PayloadRef`**。**`Sim` 引用集一字不改**(`Sim` → `Sim.Codec` 边由 ADR-025 §①:111 禁止、b2 门强制)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 装配内改动,零引擎 API。

**Control Manifest Rules (this layer)**:
- Forbidden: **`Sim` → `Sim.Codec` 引用边**(ADR-025 §①:111)
- Guardrail: `Sim` 的写者**不得手搓 `PayloadRef`**(ADR-029 §③)
- Required: 载荷字段 ∈ 整数域;`PatientId.None` 哨兵不污染高水位(ADR-021 裁定④)

---

## Acceptance Criteria

*From ADR-029 §③ + B1 的原文判定:*

- [ ] **AC-6-27(BLOCKING)** —— **`PoiStateMachine` 经 `IPayloadEncoder` 编码**:
  `TryAdvance` 的载荷构造改为 `_encoder.Encode(EventKind.PoiStateChanged, new PoiStateChangedPayload(poiId, (int)toState))`;
  **删** `PoiStateMachine.cs:104-106` 的手搓 `new PayloadRef(blobId: poiId, offset: (int)toState, length: 8)`
  及其 TODO 注释。
  ⚠️ 现状的两处语义冲突须一并消除:`poiId` 当 `blobId` 用、`newState` 当**字节偏移**用。
- [ ] **AC-6-28(BLOCKING)** —— **`RebuildFromDecoded` 经 codec 解码**:
  **删** `PoiStateMachine.cs:131-133` 的手搓反解(`BlobId` → poiId、`Offset` → state)及 TODO,
  改为从 blob 池取字节 → `PayloadCodec.Decode<PoiStateChangedPayload>`。
  ⚠️ 解码面**须经池**(`IBlobPool`),而 `Sim` 够不着 —— **见 §Implementation Notes 的接缝设计**。
- [ ] **AC-6-29(BLOCKING)** —— **`PoiStateMachine.cs` 内 `new PayloadRef(` 零命中**。
- [ ] **AC-6-30(BLOCKING)** —— **载荷经 codec 后 `Encode → Decode` 逐字段往返一致**:
  `(PoiId, NewState)` 两字段全等;覆盖三态与跳级。
- [ ] **AC-6-31(BLOCKING)** —— **`Sim` 引用集仍 = `["Sim.Contracts"]`**;b2 门绿。
- [ ] **AC-6-32** —— **重放重建仍成立**(AC-6-14/15 不回归):改走 codec 后,
  「从流重建 == 运行期内存态」判据仍绿。

---

## Implementation Notes

*Derived from ADR-029 §①/§③ + 现有 `PoiStateMachine` 形状:*

- **构造注入**:`PoiStateMachine(IEventSink, IEventAuthority, IEnumerable<int>)` →
  加 `IPayloadEncoder` 形参。**既有 6 个调用点须同步**(测试夹具 + 可能的装配代码)。
- 🔴 **接缝问题(本 story 的技术难点)**:`RebuildFromDecoded` 要**解码**载荷,
  而解码器 `PayloadCodec` 住 `Sim.Codec`(`Sim` 够不着)。三条路:
  - **甲**:`RebuildFromDecoded` **不在此处解码** —— 改由**调用方**(看得见 codec 的一侧)
    把已解码的 `(poiId, newState)` 序列喂进来。`PoiStateMachine` 只接受**已解码**的输入。
    ⇒ `Sim` 侧零 codec 依赖,**与写侧对称**(写侧也是把已编码的 `PayloadRef` 交出去)。
  - **乙**:为读侧也加一个 `IPayloadDecoder` 抽象点。
  - **丙**:`Sim` 侧只存 `PayloadRef`,重建延到边界层。
  ⇒ **本 story 取甲**(与写侧对称、零新抽象点);若实现期发现甲不可行,**须回 ADR-029 追加裁决**,
  **不得擅自加第二个抽象点**(ADR-029 §Alternatives 已驳回同类扩张)。
- **删 TODO 注释**:`PoiStateMachine.cs:105` / `:131` 两处 —— B1 的原处置正是「留 TODO」,
  本 story 落地后它们**必须消失**(否则 B1 的处置形态未被真正替换)。
- **`PoiStateChangedPayload` 已存在**(`Sim.Contracts/Payloads/WorldPayloads.cs:161`),
  字段 `PoiId` / `NewState`,**无需改动**。
- **测试缝**:注入 `IPayloadEncoder` 的 fake(返回可预测 `PayloadRef`)+ 内存 `IBlobPool`,
  保持单测无 I/O。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **契约支**(`IPayloadEncoder` / `IBlobSink` / `PayloadEncoder` / `EncodeBoxed`)—— 归
  `persistence-service/story-002`(**本 story 的硬前置**)
- **§③ 手搓门** —— 归实现轮收口批(须待本 story 与 modular 接线支**都**完成,否则门会误报另一处)
- **`PoiNotFound` 与主机混同缺陷** —— `PoiStateMachine.cs:84` 的 host gate 对客户端返回
  `PoiNotFound`,与「poi_id 不存在」混同(见 `reconciliation-world-ecozones-2026-10-02.md`)。
  **本 story 不修** —— 修法 = 新增 `NotHost` 结果码,须同步 `story-004` 的消费面,归独立轮。
- **`StructureWriter` 的接线**(modular B4)—— 归 `modular-building/story-007`
- **`Sim` → `Sim.Codec` 引用边** —— **已裁禁**(ADR-025 §①:111),不得在本 story 打开

---

## QA Test Cases

*Written at story creation (lean mode).*

- **AC-6-27 / AC-6-29**:手搓面消除。
  - Given: `PoiStateMachine.cs` 全量源文本。
  - When: 查找 `new PayloadRef(`。
  - Then: **零命中**。
  - Edge cases: 注释里提到 `PayloadRef` 不算命中(扫描须排除注释/字符串)。
  - Negative fixture: 保留任一 TODO 或手搓 ⇒ 红。

- **AC-6-30**:往返一致。
  - Given: 三态转移序列(`Undiscovered→Discovered→Resolved`)+ 跳级。
  - When: 写事件 → 从池取字节 → `PayloadCodec.Decode<PoiStateChangedPayload>`。
  - Then: `PoiId` / `NewState` 逐字段相等。
  - Edge cases: **`poiId` 与 `newState` 的语义不得再互换** —— 构造一个 `poiId=7, newState=2` 的样本,
    解码后须仍为 `(7, 2)`;现状的手搓法会把它解成 `(blobId=7, offset=2)` 的**错位**读法。
  - Negative fixture: 若实现把 `poiId` 塞进 `BlobId`(现状形态)⇒ 解码失败或字段错位 ⇒ 红。

- **AC-6-31**:装配边界。
  - Given: `Sim.asmdef` + 程序集引用集。
  - When: 扫描。
  - Then: 恰 = `["Sim.Contracts"]`;b2 门绿。

- **AC-6-32**:重建不回归。
  - Given: 一组已编码事件。
  - When: `RebuildFromDecoded`(经甲案 —— 已解码输入)。
  - Then: 与运行期内存态逐 POI 相等(承 AC-6-14/15 既有判据)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/WorldEcozones/poi_payload_encoder_test.cs` — must exist and pass
  (手搓零命中 + 往返一致含错位负例 + 装配边界 + 重建不回归)
- ⚠️ **既有 `poi_state_machine_test.cs` 须同步**(构造加 `IPayloadEncoder` 形参)——
  其 19 例须保持全绿,**不得**因本 story 而删改判据

**Status**: [x] Done — `poi_payload_encoder_test.cs` **13/13** + 既有 `poi_state_machine_test.cs` **19/19**(2026-10-03 batchmode 复跑,WorldEcozones 全组 **109/109**)
⚠️ 本行此前记 `[ ] Pending — story not yet implemented` —— 与文件头 `Status: Complete ✅ 2026-10-02`
及 §Completion Notes「6/6 AC 全部落地」自相矛盾,系状态漂移残留,2026-10-03 订正
(与 player-controller story 004/005/006 同型)。复跑证据 = `production/qa/evidence/world-ecozones/editmode-rerun-2026-10-03.md`。

---

## Dependencies

- Depends on: **`persistence-service/story-002`(契约支)—— 硬前置**;
  在其完成前本 story **无法开工**(`IPayloadEncoder` 不存在)
- Unlocks: §③ 手搓门(须与 `modular-building/story-007` 同批启用)·
  `world-ecozones` 的 EPIC 转 Complete(闭合 B1,唯一残留缺口)

---

## Completion Notes

**Completed**: 2026-10-02
**Criteria**: **6/6 AC 全部落地**,**B1 已闭**。写侧经 `IPayloadEncoder`;读侧按**甲案**改为
  `RebuildFromDecoded(IReadOnlyList<(int PoiId, PoiState State)>)` —— 解码归调用方。
  `PoiStateMachine.cs` 内 `new PayloadRef(` **代码零命中**(仅剩两处注释:规则声明 + 修复记录);
  **B1 的两处 TODO 已消失**(原处置正是留 TODO,`index.md` 已判「免责不成立」)。
  新测试 `poi_payload_encoder_test.cs` **13/13**;既有 `poi_state_machine_test.cs` **19/19**(构造点同步)。
**Deviations**: ① **读侧取甲案(用户 2026-10-02 预设,本 story 落实)** ——
     `RebuildFromEvents(IReadOnlyList<SimEvent>)` → `RebuildFromDecoded(IReadOnlyList<(int, PoiState)>)`。
     理由:解码器 `PayloadCodec` 住 `Sim.Codec`,而本类住 `Sim`(该引用边由 ADR-025 §①:111 禁止 + b2 门强制)。
     ⇒ **与写侧对称**:写侧交出已编码 `PayloadRef`,读侧收下已解码字段;**`Sim` 侧零 codec 依赖**。
     ⚠️ **若甲案不可行须回 ADR-029 追加裁决,不得擅自加第二个抽象点** —— 本 story 未遇该情形。
  ② **`RebuildFromDecoded` 的语义边界**(实现期明确,已写进 XML doc):
     它**不校验单调性**,按序列顺序覆盖,与「流是唯一真源」一致 ——
     写入侧 `TryAdvance` 已保证单调不减,重建只需忠实重放。
     另:未在 `_poiIdSet` 登记的 poi_id **跳过**(与旧行为一致);重放前先把全部已登记 POI 置回
     `Undiscovered`(原实现只 `Clear()` 后按事件回填,语义相同但更显式)。
  ③ **既有测试的构造点同步**:10 处 `new PoiStateMachine(...)` 加 `IPayloadEncoder` 形参
     (统一经 `Make(...)` helper);重建用例改走甲案。
     ⚠️ 一处**测试侧疏漏已就地修正**:定向负例刻意用 `poiId=7 ≠ newState=2` 暴露字段互换,
     但 `Setup` 的登记集原为 `{1,2,3}` ⇒ `poiId=7` 触发 `PoiNotFound`、**不发事件** ⇒ 3 例假红。
     已把 7 加入登记集,并在测试内注明该陷阱(「未登记 ⇒ 不发事件 ⇒ 测试假红」)。
**Test Evidence**: `poi_payload_encoder_test.cs` **13/13** · `poi_state_machine_test.cs` **19/19** ·
  WorldEcozones 合计 **106/106**(原 87 + 新 19)。
  全量 EditMode batchmode:`total 2073 · passed 2040 · failed 0 · skipped 32 · inconclusive 1`。
  ✅ **突变测试坐实非空转**:还原 B1 的手搓法
  (`new PayloadRef(blobId: poiId, offset: (int)toState, length: 8)`)后 **恰 9 例红**,
  含全部 3 条定向负例(`poiIdNotUsedAsBlobId` · `roundTrip_fieldOrderNotSwapped` ·
  `noManualPayloadRefInSource`)与两条既有重建用例。⇒ 判据真的能检出 B1。原文件已复原。
**Code Review**: 尚无独立评审件(归后续轮)。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
