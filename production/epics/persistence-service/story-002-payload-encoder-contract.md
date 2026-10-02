# Story 002: 载荷编码契约 —— `IPayloadEncoder` + `IBlobSink` + `EncodeBoxed` 分派

> **Epic**: 7a 持久化服务
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
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
- [ ] **AC-29-04(BLOCKING)** —— **`EncodeBoxed` 分派覆盖全部 34 个 `EventKind`**;
  分支集与 `entities.yaml` 的 34 支 `payload_schema` **双向差集归零**(承 ADR-024 A5 口径)。
- [ ] **AC-29-05(BLOCKING)** —— **`Sim.Contracts` 零 `Sim.Codec` 类型引用**;
  `IPayloadEncoder` 的签名面只出现 `PayloadRef` / `EventKind`(均住 `Sim.Contracts`)。
- [ ] **AC-29-06(BLOCKING)** —— **`Sim` 引用集实测仍 = `["Sim.Contracts"]`** —— 本 story **不改 `Sim.asmdef`**;
  b2 门保持绿。
- [ ] **AC-29-07(BLOCKING)** —— **`Encode<T>` 与 `Decode<T>` 形态对称**:约束同、分派层同款
  (`EncodeBoxed` ↔ `DecodeBoxed`)。反射断言两条泛型方法的约束与形参个数。
- [ ] **AC-29-08** —— **34 支载荷 `Encode → Decode` 逐字段往返一致**(经 `IPayloadEncoder` 编码、
  `PayloadCodec.Decode` 解码);覆盖病史 13 / 病例 5 / 世界 16。

---

## Implementation Notes

*Derived from ADR-029 §①/§②/§Implementation Guidelines:*

- **`IPayloadEncoder` 落 `Sim.Contracts`** —— 与六抽象点同装配。加 doc comment 注明
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
  (签名/对称性 + 分派完备性 + 装配边界 + 34 支往返 + 字节真入池)

**Status**: [ ] Pending — story not yet implemented

---

## Dependencies

- Depends on: **ADR-029 Accepted** ✅(2026-10-02)· `PayloadCodec` 的 34 个具名 `Encode` 重载(已在库)·
  `IBlobPool` 读面(已在库)
- Unlocks: `world-ecozones/story-006`(we B1 接线)· `modular-building/story-007`(modular B4 接线)·
  §③ 手搓门(须待两处接线完成后启用)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
