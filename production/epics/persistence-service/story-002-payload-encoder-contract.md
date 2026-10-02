# Story 002: 载荷编码契约 —— `IPayloadEncoder` + `IBlobSink` + `EncodeBoxed` 分派

> **Epic**: 7a 持久化服务
> **Status**: Complete ✅ 2026-10-02 (8/8 AC 落地;14/14 测试通过 + 突变测试坐实)
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-02

## Context

**GDD**: `design/gdd/persistence-service.md`
**Requirement**: 本 story 兑现 **ADR-029**(`IPayloadEncoder` 第七抽象点)的**契约半边**。
🔴 **根因**:ADR-006 Amendment G-2 只定了载荷的**读形**(`PayloadRef` 寻址 + 不可变 blob 池),
**未定写形**(谁编码、谁入池)。实测后果:**全库每一个 `Sim` 写者都在手搓 `PayloadRef`,
且零字节真的进池** —— 连 `craft_event_payload_test.cs:280` 这个「最正确」的写路径
也是 `new PayloadRef(0, 0, blob.Length)` 假引用。

**ADR Governing Implementation**: ADR-029(✅ Accepted 2026-10-02)· ADR-006 Amendment G-2(载荷 header/blob 形态)· ADR-025 §①(`Sim` 引用集白名单)· ADR-005(抽象点集合)
**ADR Decision Summary**: 新增**第七个 P0 抽象点 `IPayloadEncoder`**,住 `Sim.Contracts`。
**乙案**(用户 2026-10-02 裁定):编码 + 入池**一体**,接口直接出 `PayloadRef`(非 `byte[]`)。
签名 = `PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct;`。
**分派形态 = 镜像既有 `PayloadCodec.Decode<T>`**(`where T : struct` + `EncodeBoxed(EventKind)` 的 34 分支 switch),
**不引入 `IPayload` 标记接口、不走 kindgen、接受瞬时装箱**(评审修正三处,见 ADR-029 §Status 的修正记录)。
**`Sim` 引用集一字不改**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 接口契约,零引擎 API。`Sim.Contracts` / `Sim.Codec` 均保持 `noEngineReferences: true`。

**Control Manifest Rules (this layer)**:
- Required: `Sim.Contracts` 引用集 **恰 = BCL**(ADR-025 §①:112)—— 新接口只能依赖 BCL + 本装配类型
- Required: `Sim.Codec` 引用集 = BCL + `Sim.Contracts`(ADR-025 §①:113)
- Forbidden: **`Sim` → `Sim.Codec` 引用边**(ADR-025 §①:111 + b2 门 `AssemblyGates.cs:148-150`)
- Guardrail: `Sim` 的写者**不得手搓 `PayloadRef`** —— 唯一合法路径 = `IPayloadEncoder`(ADR-029 §③)

---

## Acceptance Criteria

*From ADR-029 §① / §② / §Implementation Guidelines, scoped to this story:*

- [ ] **AC-29-01(BLOCKING)** —— **`IPayloadEncoder` 住 `Sim.Contracts`**,签名逐字:
  `PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct;`
  ⚠️ `where T : struct` **无标记接口**(与 `Decode<T>` 对称);**必须收 `EventKind` 形参**(`T` 无法反推 `Kind`)。
- [ ] **AC-29-02(BLOCKING)** —— **`IBlobSink` 住 `Sim.Codec`**(池的**写面**),签名:
  `int Store(ReadOnlySpan<byte> blob);`。⚠️ **刻意不住 `Sim.Contracts`** —— 否则 `Sim` 可直接摸池,开第二条旁路。
- [ ] **AC-29-03(BLOCKING)** —— **`PayloadEncoder` 实现 `IPayloadEncoder`**,住 `Sim.Codec`,经构造注入 `IBlobSink`;
  实现体 = `PayloadCodec.Encode` 具名重载 + `_blobSink.Store` → `PayloadRef`。
- [ ] **AC-29-04(BLOCKING)** —— **`EncodeBoxed` 分派覆盖 32 支 + 2 支显式抛**
  (⚠️ **2026-10-02 实现期订正**,用户裁定;原写「覆盖全部 34 个 `EventKind`」):
  - **32 支**:正常编码,分支集与 `entities.yaml` 的 32 支对应 `payload_schema` **双向差集归零**;
  - **2 支显式不支持**:`EventKind.JudgmentRecorded` / `JudgmentRevised` ⇒ 抛
    `NotSupportedException`(错误串指向具名重载 `Encode(in JudgmentXxxPayload, string freehandText)`)。

  **根因**:这 2 支的具名 `Encode` 重载**签名不同** —— 多收一个 `string freehandText`。
  成因 = **ADR-006 G-3**:`freehand_text` 迁入 blob 的**变长 UTF-8 段**(tag 6),
  而 `JudgmentRecordedPayload` / `JudgmentRevisedPayload` struct **故意不含该字段**
  (G-3 明写「sim 消费面不持有该值」)⇒ `freehandText` **不在 `T` 里**,
  `Encode<T>(EventKind, in T)` 无从表达。
  **不给 `Encode<T>` 加 `string` 形参的理由**:G-3 既已禁止 sim 程序集**读** `freehand_text`,
  sim 侧的通用编码面更不应**承载**它 —— 否则等于把自由文本通道开进 `Sim`。
  **当前无人需要**:全仓 `Sim/` 内零 Judgment 写入者(37 case-system 未实现);
  真正的写者归 37 的 GDD 轮另裁。
- [ ] **AC-29-05(BLOCKING)** —— **`Sim.Contracts` 零 `Sim.Codec` 类型引用**;
  `IPayloadEncoder` 的签名面只出现 `PayloadRef` / `EventKind`(均住 `Sim.Contracts`)。
- [ ] **AC-29-06(BLOCKING)** —— **`Sim` 引用集实测仍 = `["Sim.Contracts"]`** —— 本 story **不改 `Sim.asmdef`**;
  b2 门保持绿。
- [ ] **AC-29-07(BLOCKING)** —— **`Encode<T>` 与 `Decode<T>` 形态对称**:约束同、分派层同款
  (`EncodeBoxed` ↔ `DecodeBoxed`)。反射断言两条泛型方法的约束与形参个数。
- [ ] **AC-29-08** —— **32 支载荷 `Encode → Decode` 逐字段往返一致**(经 `IPayloadEncoder` 编码、
  `PayloadCodec.Decode` 解码);覆盖病史 13 / 病例 **3**(5 支病例中 2 支为 Judgment 例外)/ 世界 16。

---

## Implementation Notes

*Derived from ADR-029 §①/§②/§Implementation Guidelines:*

- **`IPayloadEncoder` 落 `Sim.Contracts`** —— 与七抽象点同装配。加 doc comment 注明
  「第七抽象点 · ADR-029 · 消费者 = `Sim` 的全部事件写者」。
- **`IBlobSink` 落 `Sim.Codec`** —— 与 `IBlobPool`(读面)配对。**两条接口的装配归属不同是刻意的**:
  读面 `IBlobPool` 归 `Sim.Codec`(消费者 = `PayloadCodec.TryGet*`),写面 `IBlobSink` 也归 `Sim.Codec`
  (消费者 = `PayloadEncoder`),**两者都不暴露给 `Sim`**。
- **`EncodeBoxed` 照抄 `DecodeBoxed` 的形制**(`PayloadCodec.cs:49-`):
  ```csharp
  private static object EncodeBoxed(EventKind kind, object payload)
  {
      switch (kind)
      {
          case EventKind.InjuryOnset: return Encode((InjuryOnsetPayload)payload);
          // …34 分支…
          default: throw new InvalidDataException($"Encode: 未知 Kind {kind}");
      }
  }
  ```
  ⚠️ **接受瞬时装箱** —— 与既有解码路径同款;事件率上界 = tick 频率(20 Hz),无实质影响。
- **池的分配 / 释放 / 去重 / 并发不在本 story** —— `IBlobSink` 只定签名;
  实现归 7a 后续 / 45(承 `IBlobPool.cs` 头注「生命周期归流实现」)。本 story 提供一个
  **测试用内存池**(`InMemoryBlobSink`)供单测与接线支使用。
- **单测无 I/O**(coding-standards):往返测试用内存池,不落盘。
- **不动 `PayloadCodec` 的既有 34 个具名 `Encode` 重载** —— 本 story 只**新增**分派层与实现类。
- **Judgment 两支的 `NotSupportedException` 消息须含具名重载签名** —— 让未来 37 轮的实现者
  一眼看到该走哪条路(可诊断性,非装饰)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **接线支**:`PoiStateMachine` / `StructureWriter` 改走 `IPayloadEncoder` —— 归
  `world-ecozones/story-006` 与 `modular-building/story-007`
- **§③ 手搓门**(`Sim/` 内 `new PayloadRef(` = 违例)—— 归接线支(门须在两处接线完成后才不误报)
- **ADR-005 计数订正五处** —— 归实现轮收口批(本 story 只登记)
- **ADR-010 §三 义务 15 落表** —— 归 7a 收口批
- **池的生产实现**(7a 存档侧 / 45 网络侧)—— 本 story 只交付契约 + 测试用内存池
- **`PoiStateMachine` 的 `PoiNotFound` / 主机混同缺陷** —— 归 we 接线支(与本 story 无关)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-29-01 / AC-29-07**:签名与对称性。
  - Given: 反射读取 `IPayloadEncoder.Encode` 与 `PayloadCodec.Decode` 的泛型方法定义。
  - When: 比对泛型约束与形参。
  - Then: 两条均为 `where T : struct`(无接口约束);`Encode` 形参 = `(EventKind, in T)`。
  - Edge cases: 若实现给 `Encode<T>` 加了接口约束(初稿的 `IPayload`)⇒ 红。
  - Negative fixture: 删掉 `EventKind` 形参(初稿形态)⇒ 编译失败或断言红 —— 证明该形参是必需的。

- **AC-29-04**:分派完备性(双向差集)。
  - Given: `entities.yaml` 的 34 支 `payload_schema` Kind 名集合 + `EncodeBoxed` 的 case 集。
  - When: 求双向差集。
  - Then: 两侧差集均为空。
  - Edge cases: 若 `EncodeBoxed` 漏一支 ⇒ 差集非空 ⇒ 红(不得静默 fallback 到 default)。
  - Negative fixture: 注释掉任一个 case ⇒ 红。
  - **Judgment 两支**:断言抛 `NotSupportedException`(而非静默 default / 返回空 ref);
    且断言异常消息**指向具名重载**(可诊断性)。

- **AC-29-05 / AC-29-06**:装配边界。
  - Given: `Sim.Contracts.asmdef` / `Sim.asmdef` 文本 + 程序集引用集。
  - When: 扫描引用。
  - Then: `Sim.Contracts` 零 `Sim.Codec` 引用;`Sim` 引用集恰 = `["Sim.Contracts"]`。
  - Edge cases: 有人为图省事给 `Sim` 加 `Sim.Codec` ⇒ b2 门红(既有的 `AssemblyGates` 覆盖)。

- **AC-29-03 / AC-29-08**:往返一致(34 支)。
  - Given: 34 支载荷的边界值样本(每支 ≥ 1 例;含数组字段支如 `EmergencyAttempt` / `Craft`)。
  - When: `_encoder.Encode(kind, payload)` → `PayloadCodec.Decode<T>(kind, 取回字节)`。
  - Then: 逐字段相等。
  - Edge cases: `Store` 返回的 `BlobId` 须能被 `IBlobPool.TryGetBlob` 取回**同一段字节**
    (证明字节**真的**进池 —— 这是本 story 的核心判据,直接否证现状的假引用)。
  - Negative fixture: 若实现只返回 `PayloadRef` 而不真入池(现状的 `PayloadRef(0,0,len)` 形态)
    ⇒ `TryGetBlob` 取不回 ⇒ 红。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/PersistenceService/payload_encoder_test.cs` — must exist and pass
  (签名/对称性 + 分派完备性 32+2 + 装配边界 + 32 支往返 + 字节真入池)

**Status**: [ ] Pending — story not yet implemented

---

## Dependencies

- Depends on: **ADR-029 Accepted** ✅(2026-10-02)· `PayloadCodec` 的 34 个具名 `Encode` 重载(已在库)·
  `IBlobPool` 读面(已在库)
- Unlocks: `world-ecozones/story-006`(we B1 接线)· `modular-building/story-007`(modular B4 接线)·
  §③ 手搓门(须待两处接线完成后启用)

---

## Completion Notes

**Completed**: 2026-10-02
**Criteria**: **8/8 AC 全部落地**。交付:
  - `Sim.Contracts/IPayloadEncoder.cs` —— 第七抽象点。签名 `PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct`
    (AC-29-01/07:约束与 `Decode<T>` 对称、**无接口约束**、收 `EventKind` 形参)。
  - `Sim.Codec/IBlobSink.cs` —— 池的**写面**(AC-29-02);刻意住 `Sim.Codec` 而非 `Sim.Contracts`(防旁路)。
  - `Sim.Codec/PayloadEncoder.cs` —— 实现,经构造注入 `IBlobSink`;`EncodeBoxed` 分派
    **镜像既有 `DecodeBoxed`**(AC-29-03/04)。
  - `Sim.Codec/InMemoryBlobPool.cs` —— 测试/接线期内存池(读面 + 写面同实现,append-only ⇒ 不可变性天然成立)。
  - `Tests/EditMode/PersistenceService/payload_encoder_test.cs` —— **14 例全绿**。
**Deviations**: ① **AC-29-04 由「覆盖 34 支」改为「32 支 + 2 支显式抛」**(用户 2026-10-02 裁定,已回写 ADR-029 §Status 修正 ④)。
    根因:34 个具名 `Encode` 重载中 **2 支签名不同** —— `Encode(in JudgmentRecordedPayload, string freehandText)` /
    `Encode(in JudgmentRevisedPayload, string freehandText)`(ADR-006 G-3 的 blob 变长段,该值**不在 `T` 里**)。
    ⇒ `EncodeBoxed` 对这两支抛 `NotSupportedException`(消息指向具名重载);
    **不给 `Encode<T>` 加 `string` 形参** —— G-3 既已禁 sim **读** `freehand_text`,通用编码面更不应**承载**它。
    全仓 `Sim/` 内零 Judgment 写入者,故当前无影响;真正写者归 37 case-system 的 GDD 轮。
  ② **3 支含数组字段的载荷**(`EmergencyAttempt` / `Craft` / `EncounterStarted`)不能用 `default` 构造 ——
    其数组为 null,而 `Encode` 侧对 null 数组**先拒**(`ArgumentException`,「坏数据不进字节面」);
    且 `Craft` 有五数组等长约束、`EmergencyAttempt` 有 `Edges == EdgeTicks.Length` 约束。
    测试为这 3 支造了满足约束的样本(其余 29 支 `default` 即合法)。
  ③ **反射限制(测试侧,非实现)**:`where T : struct` 在反射下产出 **1 条**约束(`System.ValueType`)而非 0 条
    —— 判据意图须写成「**无接口约束**」;且 `byte[]` → `ReadOnlySpan<byte>` 的隐式转换**不参与** `Invoke` 实参绑定,
    故经编译期泛型 helper 过渡。两处均已就地注明。
**Test Evidence**: `Tests/EditMode/PersistenceService/payload_encoder_test.cs` **14/14 Passed**。
  全量 EditMode batchmode:`total 2048 · passed 2015 · failed 0 · skipped 32 · inconclusive 1`。
  ✅ **突变测试坐实非空转**:把 `PayloadEncoder.Encode` 改成现状的手搓形态(字节丢弃、`blobId` 恒 0)后,
  **恰 2 例红** —— `test_ac2903_bytesActuallyEnterPool`(核心判据:字节须真的进池)
  与 `test_ac2908_poiStateChanged_fieldOrderNotSwapped`(须经池往返才能验字段序)。
  ⇒ 本 story 的判据**真的能检出** ADR-029 要消灭的假引用形态。原文件已复原,工作树无残留。
**Code Review**: 尚无独立评审件(本 story 为 ADR-029 实现轮首件;评审归后续轮)。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
