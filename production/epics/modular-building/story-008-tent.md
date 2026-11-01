# Story 008: 帐篷 —— P0 唯一新建例外(数据 + 类型位 + 可放置判据)

> **Epic**: 模块化建造
> **Status**: **Complete ✅ 2026-11-01**(S22-8 甲案落地;`TentPlacementTest` 3/3 + 两条既有哨兵按「契约演进」订正;证伪 **4 变异各转红 · SHA-256 逐字节还原**)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 3h(实数 ≈ 2.5h,含两次哨兵转红后的逐条复核)
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-11-01

## Context

**GDD**: `design/gdd/modular-building.md` 规则六(`:246-257`)· `design/gdd/game-concept.md:697-698`(M0 第 4 条,2026-09-20 改判)

**Requirement(原文)**:

> `game-concept.md:697-698` ——「一间固定医馆 + 小改造 …… **不允许新建**
> *(⚠️ 2026-09-20 用户裁定改判:**P0 允许「帐篷」这一次新建** = 剧情开场 …… 裁定原话要义:
> **P0 只是测试切片,也必须能新建** —— 帐篷即该能力在 P0 的最小证明……)*」

> `modular-building.md:246-257` ——「P0 唯一例外 = 帐篷:序章的一次新建,**落关卡工具在
> `world_buildslots.json` 预置的 `tent` 槽**(骨架外预置槽,**不破「不改骨架」**);
> `StructurePlaced` **照常发,载荷形状不变**;第二/三间棚子仍归 P1a。」

**为什么有这张卡(裁定与形态都在,承接者不在)**:
S22-8 勘察实测 `grep -rniE "\btent\b" unity/Assets --include=*.cs` = **0**,
`assets/data/*.json` 亦无帐篷条目 ⇒ 裁定与规则六的实现口径齐备,**数据与类型位零实现**。

⚠️ **一处必须如实订正的事实**(勘察件 `s22-8-smoke13-tent-two-options-2026-11-01.md` §2 原表
「23 的卡面 story-001…007 无一张认领帐篷」**说过头了**):`story-001` 实现注 #3 与
`story-006` 实现注 #4 都**提过**帐篷,`story-006` 还写明「(Story 001 数据承载)」。
但两边都只是**实现注里的一句话**,**没有任何 AC 认领它**,数据也从未落盘 ——
这正是本项目抓过的「**自述顶替门**」同族形态:一句「由 X 承载」被当作已交付。
本卡把这句话变成可执行判据。

**ADR Governing Implementation**:
- ADR-015 §三(主):建造槽位与地形共用单一整数格 `WorldPos`;模块对齐到格
- ADR-009 §二 / Amendment J(次):`StructurePlaced` 是世界流既有 Kind,**不为帐篷新增 Kind**
- ADR-022 §三(次):`world_buildslots.json` 的**唯一作者 = 关卡工具**;本卡按该契约追加一行
- ADR-024(次):`Kind` 单一真源 —— 本卡**零新 Kind**,`entities.yaml` 一字不改

**ADR Decision Summary**:帐篷 = `SlotType` 闭集扩员(`Tent = 7`)+ 模块目录一行 + 骨架外一行;
放置走**既有五条件**与**既有 `StructureWriter`**,`StructurePlaced` 载荷五字段形状不变
(`structure_id / cell / module_id / orientation / variant`)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: N/A —— 纯数据 + 枚举 + 门 A 纯整数判定,零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 占用格 ⊆ 整数格(ADR-015);`SlotType` 扩员须**同批**给出「新槽只接受新类型」的判据
- Forbidden: 为新建开第二套机制(新 Kind / 新写者 / 新判定函数);名字字符串匹配识别类型
- Guardrail: 出货数据由夹具**真读 cooked 产物**校验(内联假数据 = 改坏数据时仍绿)

---

## Acceptance Criteria

*来自 S22-8 甲案(`s22-8-smoke13-tent-two-options-2026-11-01.md` §3),逐条已实现:*

- [x] **① `SlotType` 扩员** —— `BuildSlotCatalog.SlotType` 加 `Tent = 7`(闭集 6 → 7 员)。
- [x] **② 模块目录一行** —— `assets/data/build_modules.json` 追加 `module_id: 10` /
      `module_type: "Tent"` / `furn_type: null` / `occupancy_local: [[0,0,0]]` /
      `cost: 5` / `refundable: true` / `build_item_key: 8`。
- [x] **③ 骨架外预置槽** —— `assets/data/world_buildslots.json` 追加
      `{"cell": [12,0,12], "slot_type": "Tent", "baked_initial": false, "module_id": null}`;
      两侧重烘(`build_modules.cooked.bytes` 611 B / 10 模块 · `world_buildslots.cooked.bytes` 372 B / 14 槽)。
- [x] **④ 正例判据** —— 载入目录 → `Placeable(tent 槽, tent 模块)` 为真 → 放置 ⇒
      **恰一条 `StructurePlaced`**,`Patient = None`,骨架三 Kind 之外**零新事件**。
- [x] **⑤ 反例判据** —— 非 `tent` 槽放 `tent` 模块 ⇒ 拒(五条件的 ① 支 = `TypeMismatch`),**零 Append**。

---

## Implementation Notes

1. **`SlotType.Tent = 7`** —— 扩员点被**两条既有哨兵**当场咬住(见 §连带),两条都已按
   「契约演进」而非「改期望值」处置:
   - `slot_type_vocabulary_test.test_slotTypeVocabulary_nameToOrdinal_isLiteralPinned`:
     七对字面量 + `ClosedSetText()`;扩员前只有六对 ⇒ **红**,补第七对才回绿。
   - `world_buildslots_codec_validation_test`:掩码上界 `MaxAllowedMask` 由枚举**派生**
     (`(1 << (max+1)) - 1`),127 → 255;越界负载 `128` → `256`(128 现在是**合法位**),
     另加一条 `tentBitMask_isAccepted`(`1 << 7 = 128` 放行 —— 越界判据的正向半边)。
2. **不做的事**:没有新 `Kind`、没有新写者、没有新判定函数。帐篷复用
   `PlaceableChecker.Check`(五条件)与 `StructureWriter.Place`(既有 `IPayloadEncoder` 路径)。
3. **`cost = 5` / `build_item_key = 8`(木柱)是临时值**:同目录 9 条的量级是 1–8,
   本轮取中位数附近的 5 —— **数值轮定稿前不加锁**(与 `R` / `BUILD_TIME` 同批归用户)。
4. **槽位位置 `(12,0,12)`**:医馆槽位簇在 `x∈[1,4]`(Shell)与 `x,z∈[6,8]`(内景),
   12 在外围 ⇒ 符合规则六的「**骨架外**预置」。

## 连带改动(非本卡主体,但同批必须落)

| 文件 | 改动 | 为什么必须同批 |
|---|---|---|
| `slot_type_vocabulary_test.cs` | 六对 → 七对字面量 + `ClosedSetText` 文案 | 它是**闭集哨兵**,扩员不补它 ⇒ 红(已实测) |
| `world_buildslots_codec_validation_test.cs` | 上界 127 → 255;越界负载 128 → 256;+1 条 Tent 位正向 | 上界**派生**自枚举,扩员即变;不改判定语义,只改负载 |

---

## Out of Scope

- 序章剧情节拍(「玩家在帐篷里住第一晚」)—— 归 37 病例 / 内容件
- 帐篷的外观、动画、UI 图标 —— 归美术轮
- 第二 / 第三间棚子、自由建造 —— **P1a**(规则六明写)
- 帐篷的拆除返还 —— 已由 `DemolishChecker` 按模块属性通用处理(`story-006` 边案例),
  本卡不出专用判据

---

## Test Evidence

| AC | 载体 |
|---|---|
| ① | `Assets/Tests/EditMode/ModularBuilding/slot_type_vocabulary_test.cs`(字面量钉板) |
| ②③ | `Assets/Tests/EditMode/ModularBuilding/tent_placement_test.cs::test_shippedData_hasTentSlotAndModule` |
| ④ | `...::test_tentPlacement_writesExactlyOneStructurePlaced` |
| ⑤ | `...::test_tentModule_onDecorSlot_isRejected` |
| ①②③(读侧契约) | `world_buildslots_codec_validation_test.cs::test_worldBuildSlots_codec_tentBitMask_isAccepted` 等 |

📖 **只读外部夹具声明**(`test-standards.md` 具名义务):`tent_placement_test.cs` 读
`unity/Assets/DataCooked/world_buildslots.cooked.bytes` 与 `build_modules.cooked.bytes`;
**必须读真产物** —— 本卡判据的对象就是「出货数据里能不能放下帐篷」,内联假表会让「数据被改坏」仍然绿。

### 证伪轮(4 变异,改 → 跑 → 还原 同一命令内完成;SHA-256 逐字节还原)

| 变异 | 打哪里 | 结果 |
|---|---|---|
| **M1** 停用五条件 ①(类型匹配) | `PlaceableChecker.Check` | **2 红**:`test_typeMismatch_returnsTypeMismatch`、`test_tentModule_onDecorSlot_isRejected` |
| **M2** `Place` 追加第二条事件 | `StructureKinds.cs` | **11 红**,含 `test_tentPlacement_writesExactlyOneStructurePlaced` |
| **M3** 掩码上界写死 `127`(退回 A-22 前形态) | `WorldBuildSlotsCookedCodec.ComputeMaxAllowedMask` | **18 红**(`total=233 / passed=215 / failed=18`):出货数据带位 7 ⇒ 整表读不进来,`test_worldBuildSlots_codec_tentBitMask_isAccepted` / `test_shippedData_hasTentSlotAndModule` / 世界装配系列连锁转红 |
| **M4** `SlotType.Tent` 7 → 8(位挪走) | `BuildSlotCatalog.cs` | **7 红**:`test_slotTypeVocabulary_nameToOrdinal_isLiteralPinned`、三条帐篷判据、`test_worldBuildSlots_diskMatchesFreshBake`、两条 codec 上界 |

**还原判据**:4 个被变异文件跑后 SHA-256 与跑前逐字节一致
(`sha256sum -c pre_tent.sha` → 4/4 成功)。编译错全程 `error CS` = 0。

---

## Completion Notes

**Completed**: 2026-11-01
**Criteria**: 5/5
**Deviations**: 无 ADR/GDD 偏离。两处**临时值**已登记(`cost = 5` 归数值轮)。
**Test Evidence**: Logic —— `EditMode/ModularBuilding/tent_placement_test.cs`(3 条)+ 两条既有哨兵订正
**Code Review**: 待 sprint 收口批
