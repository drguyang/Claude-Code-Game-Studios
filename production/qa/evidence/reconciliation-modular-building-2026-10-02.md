# 逐 BLOCKING 对账件 —— modular-building(系统 23)

> **用途**:补齐 `production/epics/index.md` §越序实现登记 的**缺口 ①a** ——
> 「BLOCKING 已修」此前只有 commit message 自述,无可证伪对账件。
> **编制**:2026-10-02 · **编制方式 = 逐条独立复核**(非转录 commit message)
> **HEAD**:`1a884c9` · **引擎**:Unity 6000.3.24f1

## 结论摘要

| # | 原判定 | 实测判决 |
|---|---|---|
| B1 | PlaceableChecker 条件⑥ entity check 缺失 | ✅ **已修** |
| B2 | DemolishChecker entity check 全局错误 | ✅ **已修** |
| B3 | DemolishChecker shell detection 查 ModuleId 而非模块目录 | ✅ **已修** |
| B4 | Structure* payload bit-packing | 🔴 **部分修 —— 残留两处**(见下) |
| B5 | 无 OccupancyOverlay 独立类型 | ✅ **已修** |

**5 条中 4 条确认修复,B4 部分修复。** 故本 epic 维持 `In Review`,**不转 Complete**。

---

## B1 —— PlaceableChecker 条件⑥ entity check 缺失

- **原判定**(`bb477e6`):条件⑥ entity check 缺失/错误。
- **修复落点**(`da04f41` 自述):添加 `IPresenceQuery.IsPresentAt` 调用。
- **实测**:
  ```
  unity/Assets/Sim/World/PlaceableChecker.cs:95:  if (_presenceQuery.IsPresentAt(cell))
  unity/Assets/Sim.Contracts/IPresenceQuery.cs:33: bool IsPresentAt(WorldPos cell);
  ```
- **判决**:✅ **已修**。接口扩展落 `Sim.Contracts`(边界程序集,非 Sim 实现),调用点在条件⑥。

## B2 —— DemolishChecker entity check 全局错误

- **原判定**:entity check 判断「全局是否有实体」,而非「**占用格**是否有实体」。
- **修复落点**:改为遍历 `inst.OccupiedCells` 逐格判。
- **实测**:
  ```
  unity/Assets/Sim/World/DemolishChecker.cs:66:  foreach (var cell in inst.OccupiedCells)
  unity/Assets/Sim/World/DemolishChecker.cs:68:      if (_presenceQuery.IsPresentAt(cell))
  ```
- **判决**:✅ **已修**。判据从全局收窄到占用格集合。

## B3 —— DemolishChecker shell detection

- **原判定**:用 `ModuleId` 判 Shell,应为查**模块目录**取 `SlotType`。
- **修复落点**:经 `_moduleCatalog.GetModuleDefinition` 取定义再判 `SlotType.Shell`。
- **实测**:
  ```
  unity/Assets/Sim/World/DemolishChecker.cs:58-63:
      // ② Shell 不可拆(查模块目录获取 SlotType,非 ModuleId)
      var moduleDefOpt = _moduleCatalog.GetModuleDefinition(inst.ModuleId);
      if (moduleDefOpt.Value.SlotType == SlotType.Shell)
          return DemolishResult.ShellNotRemovable;
  ```
- **判决**:✅ **已修**。且注释显式记录了原缺陷形态。

## 🔴 B4 —— Structure* payload bit-packing(**部分修,残留两处**)

- **原判定**:`StructurePlaced` / `StructureModified` 载荷走 bit-packing。
- **修复落点**(`da04f41` 自述):「移除 bit-packing,使用真实 payload 类型」。
- **实测**:自述**不完全属实**。残留两处:

**残留 ① —— `StructurePlaced` 的 bit-packing 仍在,且伴随死代码**

```
unity/Assets/Sim/World/StructureKinds.cs:191-198:
    // 构造载荷
    var payload = new PayloadRef(blobId: structureId, offset: moduleId, length: 8);
    // 额外数据: orientation + variant 编码到 offset 高位
    payload = new PayloadRef(
        blobId: structureId,
        offset: moduleId | (orientation << 16) | (variant << 24),   // ← bit-packing
        length: 8);
```

两处问题:(a) `offset` 字段被当**业务位域**用,与 `PayloadRef` 契约(`Offset` = 字节偏移)语义冲突;
(b) 第一条干净赋值**立即被覆盖** = 死代码(且注释「构造载荷」与实际语义不符)。

**残留 ② —— `StructureModified` 去掉了 bit-packing,但用 `offset: 0` 占位**

```
unity/Assets/Sim/World/StructureKinds.cs:243-245:
    // 载荷: 真实 payload 类型(ADR-024: 非 bit-packing)
    var payload = new PayloadRef(blobId: structureId, offset: 0, length: 8);
    // TODO: 接入 Sim.Codec 编码 StructureModifiedPayload(需 IBlobPool 支持)
```

**「前置未就绪」的免责不成立** —— codec **已在库**:

```
unity/Assets/Sim.Codec/PayloadCodec.World.cs:266  Encode(in StructurePlacedPayload p)
unity/Assets/Sim.Codec/PayloadCodec.World.cs:304  Encode(in StructureModifiedPayload p)
```

⚠️ 但存在**结构面障碍**:`Sim/World/StructureKinds.cs` 住 `Sim` 程序集,而 `Sim.Codec`
**只引用 `Sim.Contracts`**;`Sim` 未引用 `Sim.Codec`(全仓 `Sim.asmdef` 引用集核验)。
故接线须先裁「`Sim` → `Sim.Codec` 的引用边是否允许」(门 A 约束的是 `UnityEngine`,
非 `Sim.Codec`,但 ADR-025 §一 的 `Sim` 引用集白名单**恰 = {BCL, Sim.Contracts}**)。
⇒ 这是**需 ADR-025 口径确认**的事项,不是单纯漏接。**此处不擅自接线,如实记账。**

- **判决**:🔴 **部分修**。bit-packing 在 `StructurePlaced` 侧**仍在**;`StructureModified`
  侧已去 bit-packing 但载荷字段未真正编码。

## B5 —— OccupancyOverlay 独立类型

- **原判定**:无 `OccupancyOverlay`,`slot_occupied` 无可投影的对像。
- **修复落点**:新增独立类型。
- **实测**:
  ```
  unity/Assets/Sim/World/World.cs:143:  public readonly struct OccupancyOverlay
  unity/Assets/Sim/World/World.cs:147:  public OccupancyOverlay(IReadOnlyList<bool> slotOccupied)
  ```
- **判决**:✅ **已修**。独立 `readonly struct`,承 F-23-1「纯函数投影」口径。

---

## 验证命令(可复现)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "IsPresentAt" unity/Assets/Sim/World/PlaceableChecker.cs
grep -n "OccupiedCells\|GetModuleDefinition\|SlotType.Shell" unity/Assets/Sim/World/DemolishChecker.cs
grep -n "orientation << 16\|offset: 0" unity/Assets/Sim/World/StructureKinds.cs
grep -n "StructurePlacedPayload\|StructureModifiedPayload" unity/Assets/Sim.Codec/PayloadCodec.World.cs
grep -n "OccupancyOverlay" unity/Assets/Sim/World/World.cs
```

**测试证据**(独立于本对账件):全量 EditMode batchmode 复跑 ModularBuilding **51/51 全绿**
—— `production/qa/evidence/editmode-full-rerun-2026-10-02.md`。
⚠️ **51/51 绿不覆盖 B4** —— 该 fixture 不含「载荷字段真正编码」的判据。

## 转 Complete 的前置

1. 闭合 B4 残留两处(含 `Sim` → `Sim.Codec` 引用边的 ADR-025 口径确认);
2. 或**显式降级** B4 为已知缺口并在 story/EPIC 登记(不得只留 TODO 注释 ——
   那正是 `world-ecozones` B1 的处置形态,已判为「免责不成立」)。
