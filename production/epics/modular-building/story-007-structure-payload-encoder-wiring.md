# Story 007: 结构载荷接线 —— `Structure*` 改走 `IPayloadEncoder`(闭合 B4)

> **Epic**: 6 建造(模块化)
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-10-02

## Context

**GDD**: `design/gdd/modular-building.md`
**Requirement**: 闭合双代理评审的 **B4** —— `Structure*` 载荷的 bit-packing。
🔴 **B4 是对账轮判定的「部分修」**(`da04f41` 自述「移除 bit-packing」,实测未全):
- `StructureKinds.cs:191-198` 的 `StructurePlaced` **仍走 bit-packing** `moduleId | (orientation << 16) | (variant << 24)`,
  **且第一条干净赋值 `offset: moduleId` 立即被覆盖 = 死代码**;
- `StructureKinds.cs:243-245` 的 `StructureModified` 去掉 bit-packing 但用 `offset: 0` 占位 + TODO。

**ADR Governing Implementation**: ADR-029(✅ Accepted 2026-10-02)· ADR-015 §五(建造模块对齐到格)· ADR-024 §①(Kind 真源)· ADR-025 §①(`Sim` 引用集)
**ADR Decision Summary**: `Sim` 的写者经 **`IPayloadEncoder`** 编码,**零手搓 `PayloadRef`**。
**`Sim` 引用集一字不改**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 装配内改动,零引擎 API。

**Control Manifest Rules (this layer)**:
- Forbidden: **`Sim` → `Sim.Codec` 引用边**(ADR-025 §①:111)
- Guardrail: `Sim` 的写者**不得手搓 `PayloadRef`**(ADR-029 §③)
- Forbidden: 载荷字段出现 bit-packing 式的「业务字段挤进 `Offset`」(ADR-006 G-2:`Offset` = 字节偏移)

---

## Acceptance Criteria

*From ADR-029 §③ + B4 的对账结论:*

- [ ] **AC-23-31(BLOCKING)** —— **`StructureWriter.Place` 改走 `IPayloadEncoder`**:
  载荷改为 `_encoder.Encode(EventKind.StructurePlaced, new StructurePlacedPayload(structureId, anchor, moduleId, orientation, variant))`;
  **删** `StructureKinds.cs:193-199` 的 bit-packing 与**死代码**(两条赋值,后者覆盖前者)。
- [ ] **AC-23-32(BLOCKING)** —— **`StructureWriter.Remove` 改走 `IPayloadEncoder`**:
  载荷 = `StructureRemovedPayload`(**注意**:现状用 `offset: inst.ModuleId` 手搓 —— 须消除)。
- [ ] **AC-23-33(BLOCKING)** —— **`StructureWriter.Modify` 改走 `IPayloadEncoder`**:
  载荷 = `StructureModifiedPayload`(含 `NewOrientation` / `NewVariant` / `ModifiedFields` 位掩码);
  **删** `:245` 的 `offset: 0` 占位与 TODO。
  ⚠️ 现状把 `orientation` / `variant` 算出来**却未放进载荷**(只写 `offset: 0`)—— 本 story 须真正编码。
- [ ] **AC-23-34(BLOCKING)** —— **`StructureKinds.cs` 内 `new PayloadRef(` 零命中**。
- [ ] **AC-23-35(BLOCKING)** —— **三条载荷 `Encode → Decode` 逐字段往返一致**;
  `StructurePlaced` 的 `(StructureId, Cell, ModuleId, Orientation, Variant)` **五字段全等**;
  `StructureModified` 的 `ModifiedFields` 位掩码语义正确(仅朝向位 / 仅变体位 / 两者)。
- [ ] **AC-23-36(BLOCKING)** —— **`Sim` 引用集仍 = `["Sim.Contracts"]`**;b2 门绿。
- [ ] **AC-23-37** —— **`StructureInstance` 注册表语义不回归**:改走 codec 后,
  既有 51 例 ModularBuilding 测试全绿(`StructureKindsTest` / `WorldTest` / `ModifiableCheckerTest` 等)。

---

## Implementation Notes

*Derived from ADR-029 §③ + 现有 `StructureWriter` 形状:*

- **`StructureWriter` 住 `StructureKinds.cs`**(⚠️ **ADR-029 写的是「`StructureKinds` 写者」——
  实际类名 = `StructureWriter`,文件 = `StructureKinds.cs`**;本 story 以真名为准,并在收口批回写 ADR-029 的措辞)。
- **构造注入**:`StructureWriter(IEventSink, StructureInstanceRegistry)` → 加 `IPayloadEncoder` 形参。
  **既有调用点须同步**(测试夹具 + 装配代码)。
- **三支载荷 struct 均已存在**(`Sim.Contracts/Payloads/WorldPayloads.cs`):
  `StructurePlacedPayload`(:110,五字段)· `StructureRemovedPayload`(:39)·
  `StructureModifiedPayload`(:142,六字段含 `ModifiedFields`)。**无需改动 struct**。
- **`StructureRemovedPayload` 的字段须核**:现状手搓 `offset: inst.ModuleId` ——
  实现时须核对 struct 的真实字段,确保 `Remove` 的语义与 `StructurePlaced` 对称。
- **死代码必须删净**:`:193` 的第一条赋值被 `:195` 覆盖 —— 保留任何一条都是缺陷形态。
- **测试缝**:注入 `IPayloadEncoder` 的 fake + 内存 `IBlobPool`,保持单测无 I/O。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **契约支** —— 归 `persistence-service/story-002`(**本 story 的硬前置**)
- **§③ 手搓门** —— 归实现轮收口批(须待本 story 与 we 接线支**都**完成)
- **`StructurePlaced` 的 bit-pack「评审未点」面** —— `index.md` 原 §①b 末句指出该 bit-pack
  「评审未点、现状仍在」;本 story **即为其闭合件**,不再单列
- **`PoiStateMachine` 的接线**(we B1)—— 归 `world-ecozones/story-006`
- **`Sim` → `Sim.Codec` 引用边** —— **已裁禁**,不得在本 story 打开

---

## QA Test Cases

*Written at story creation (lean mode).*

- **AC-23-34**:手搓面消除。
  - Given: `StructureKinds.cs` 全量源文本(排除注释/字符串)。
  - When: 查找 `new PayloadRef(`。
  - Then: 零命中。
  - Edge cases: 三处写入点(`Place` / `Remove` / `Modify`)全须覆盖。
  - Negative fixture: 保留 `:195` 的 bit-pack 或 `:245` 的 `offset: 0` ⇒ 红。

- **AC-23-35**:往返一致 + 位掩码语义。
  - Given: `StructurePlaced` 样本含非零 `Orientation` / `Variant`(如 1 / 2)+ 非零 `Cell`;
    `StructureModified` 三种 `ModifiedFields`(1 / 2 / 3)。
  - When: 编码 → 解码。
  - Then: 五字段全等;`ModifiedFields` 位掩码逐位正确。
  - Edge cases: **`Orientation = 0` 与 `Variant = 0` 的样本** —— 现状的 bit-pack 在
    `orientation = 0` 时与「未设」不可区分(bit-pack 的固有缺陷);codec 路径须能区分。
  - Negative fixture: 若实现退回 bit-pack ⇒ 高位字段(orientation / variant)在
    `moduleId` 较大时被**静默截断**(`moduleId | (orientation << 16)`)⇒ 解码不一致 ⇒ 红。

- **AC-23-36**:装配边界。
  - Given: `Sim.asmdef` + 程序集引用集。
  - When: 扫描。
  - Then: 恰 = `["Sim.Contracts"]`;b2 门绿。

- **AC-23-37**:注册表不回归。
  - Given: 既有 51 例 ModularBuilding 测试。
  - When: 全量复跑。
  - Then: 51/51 绿。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/ModularBuilding/structure_payload_encoder_test.cs` — must exist and pass
  (手搓零命中 + 三支往返含位掩码 + 装配边界)
- ⚠️ **既有 `structure_kinds_test.cs` 等须同步**(构造加 `IPayloadEncoder` 形参)——
  51 例须保持全绿,**不得**因本 story 而删改判据

**Status**: [ ] Pending — story not yet implemented

---

## Dependencies

- Depends on: **`persistence-service/story-002`(契约支)—— 硬前置**
- Unlocks: §③ 手搓门(须与 `world-ecozones/story-006` 同批启用)·
  `modular-building` 的 EPIC 转 Complete(闭合 B4,唯一残留缺口)

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
