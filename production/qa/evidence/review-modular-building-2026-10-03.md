# 代码评审 —— modular-building(系统 23 建造)

> **评审对象**:`unity/Assets/Sim/World/`(StructureKinds / PlaceableChecker / DemolishChecker /
> World / BuildSlotCatalog)+ `unity/Assets/Sim.Contracts/` + `unity/Assets/Sim.Codec/PayloadEncoder.cs`
> + `unity/Assets/Tests/EditMode/ModularBuilding/`
> **评审日期**:2026-10-03
> **评审方式**:逐条独立复核 —— 读源码 / grep 落点,**不采信 commit message 与 story 自述**
> **引擎**:Unity 6.3 LTS(6000.3.24f1)
>
> ## ⚠️ 本报告的性质声明(务必先读)
>
> **本报告评的是「补做时点(2026-10-03)」的代码,不追认原判定。**
>
> 本 epic 曾于 2026-10-01 有一次双代理评审(结论 REQUEST_CHANGES,5 条 BLOCKING),
> **但该评审报告原件从未落盘、已不可得**。因此本报告**不是补录、不是转录**,而是**新评一次**:
> 5 条「原 BLOCKING」的措辞取自 `EPIC.md` 与 `reconciliation-modular-building-2026-10-02.md`
> 的**转述**,本报告对每一条**只对当下代码作出判决**。
>
> **凡本报告未亲自读源码确认的条目,一律显式标注 `未验证` 或 `仅文档,未验证源码`。**
> **本评审未运行 Unity / 未执行测试套件** —— 所有「测试绿」类断言均标 `未验证`。

---

## 🔍 独立复核批注(2026-10-03 · 由主会话复核,非 agent 自述)

复核人**逐条实测**了本报告的关键结论:

| 报告条目 | 复核结果 |
|---|---|
| **N1** 占用格集恒 = `{anchor}` | ✅ **成立** —— `StructureKinds.cs:60-61` 自陈「简化:仅锚点格」;`DemolishChecker.cs:66` 迭代的正是该集合 ⇒ **B2 的「已修」确为纸面** |
| **N2** `OccupyCells`/`FreeCells` 零生产调用方 | ✅ **成立** —— 全仓 grep 仅 `World.cs:76/83` 定义体,**无任何调用方**(连测试都没有);`Place` 不调它 |
| B4 已修(`new PayloadRef(` 零命中) | ✅ 成立(仅注释命中) |

⚠️ **本报告的 N1/N2 是既有对账件与前两轮评审均未登记的缺陷** —— 它们说明
「迭代形态改对了」不等于「被迭代的集合是对的」。

## 1. 结论摘要表

| # | 原判定(转述) | 本评判决 | 依据 |
|---|---|---|---|
| B1 | PlaceableChecker 条件⑥ entity check 缺失 | ✅ **已修** | 源码坐实 |
| B2 | DemolishChecker entity check 改为检查占用格 | ⚠️ **部分修 —— 结构在,实质退化** | 源码坐实(见 N1) |
| B3 | DemolishChecker shell detection 查模块目录 | ✅ **已修** | 源码坐实 |
| B4 | `Structure*` payload bit-packing | ✅ **已修**(story-007) | 源码 + grep 坐实 |
| B5 | 无 `OccupancyOverlay` 独立类型 | ✅ **已修** | 源码坐实 |
| ID | `StructureInstanceRegistry` id 类型 `int` vs `i64` | 🔴 **未修 —— 独立验证:缺陷仍在** | 源码坐实 |
| N1 | (新)`Register` 占用格集恒 = {anchor} | 🟠 **新发现 · 高** | 源码坐实 |
| N2 | (新)`World` 占用表与写路径未接线 | 🟠 **新发现 · 高** | grep 坐实 |

**5 条原 BLOCKING 中:B1/B3/B4/B5 = 已修;B2 = 部分修(结构在但退化)。**
**另:** 登记的 id 类型缺陷**仍在**;**新查 2 条高严重度缺口**。
⇒ 本 epic **不应转 Complete**。

---

## 2. 逐条详节

### B1 —— PlaceableChecker 条件⑥ entity check

- **原判定**:条件⑥ entity check 缺失/错误。
- **修复落点**:
  - `unity/Assets/Sim/World/PlaceableChecker.cs:95` —— `if (_presenceQuery.IsPresentAt(cell)) return PlaceableResult.EntityOnCell;`
  - `unity/Assets/Sim.Contracts/IPresenceQuery.cs`(≈:33,对账件标注)—— `bool IsPresentAt(WorldPos cell);`(本评已读接口文件确认该方法存在)
- **实测证据**:条件⑥ 在占用格循环内(`:84-97`)逐格判实体;接口扩展落 `Sim.Contracts`(边界程序集)。
- **判决**:✅ **已修**。
- **验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -n "IsPresentAt" unity/Assets/Sim/World/PlaceableChecker.cs
  grep -n "IsPresentAt" unity/Assets/Sim.Contracts/IPresenceQuery.cs
  ```

### B2 —— DemolishChecker entity check 改为检查占用格

- **原判定**:entity check 判「全局是否有实体」,应改为「**占用格**是否有实体」。
- **修复落点**:`unity/Assets/Sim/World/DemolishChecker.cs:66-70`
  ```csharp
  foreach (var cell in inst.OccupiedCells)
      if (_presenceQuery.IsPresentAt(cell))
          return DemolishResult.EntityOnCell;
  ```
- **实测证据**:循环**结构**确已从全局收窄到 `inst.OccupiedCells` 逐格判 —— 形态正确。
- **⚠️ 但实质退化**:`inst.OccupiedCells` 的**内容恒为 `{anchor}` 单格** ——
  见 `unity/Assets/Sim/World/StructureKinds.cs:60-61`:
  ```csharp
  // 计算占用格(简化:仅锚点格;完整实现需查模块目录)
  var occupiedCells = new List<WorldPos> { anchor };
  ```
  即:对**多格模块**(`ModuleDefinition.LocalOccupancy` 支持多格,见 `PlaceableChecker.ComputeOccupiedCells`),
  拆除实体检查**只查锚点格** —— 玩家站在非锚点足迹格上时**不阻挡拆除**。
- **判决**:⚠️ **部分修** —— 迭代形态已修,**被迭代的集合本身退化**,判据实质不成立(详 N1)。
- **验证命令**:
  ```bash
  grep -n "OccupiedCells\|IsPresentAt" unity/Assets/Sim/World/DemolishChecker.cs
  grep -n "仅锚点格\|occupiedCells = new List" unity/Assets/Sim/World/StructureKinds.cs
  ```

### B3 —— DemolishChecker shell detection 查模块目录

- **原判定**:用 `ModuleId` 判 Shell,应查**模块目录**取 `SlotType`。
- **修复落点**:`unity/Assets/Sim/World/DemolishChecker.cs:59-63`
  ```csharp
  var moduleDefOpt = _moduleCatalog.GetModuleDefinition(inst.ModuleId);
  if (moduleDefOpt == null) return DemolishResult.StructureNotFound;
  if (moduleDefOpt.Value.SlotType == SlotType.Shell) return DemolishResult.ShellNotRemovable;
  ```
- **实测证据**:经 `IModuleCatalog.GetModuleDefinition` 取定义后判 `SlotType.Shell`,且目录缺失时 fail-closed。
- **判决**:✅ **已修**。
- **验证命令**:
  ```bash
  grep -n "GetModuleDefinition\|SlotType.Shell" unity/Assets/Sim/World/DemolishChecker.cs
  ```

### B4 —— `Structure*` payload bit-packing(story-007 接线)

- **原判定**:`StructurePlaced` / `StructureModified` 载荷走 bit-packing(`moduleId | (orientation<<16) | (variant<<24)`),
  且 `StructurePlaced` 侧伴死代码;`StructureModified` 侧用 `offset: 0` 占位丢字段。
- **修复落点**:
  - `unity/Assets/Sim/World/StructureKinds.cs:152-153` —— `Place` 改 `_encoder.Encode(EventKind.StructurePlaced, new StructurePlacedPayload(structureId, anchor, moduleId, orientation, variant))`
  - `unity/Assets/Sim/World/StructureKinds.cs:170-171` —— `Remove` 改 `_encoder.Encode(EventKind.StructureRemoved, new StructureRemovedPayload(structureId, inst.Anchor, inst.ModuleId))`
  - `unity/Assets/Sim/World/StructureKinds.cs:203-205` —— `Modify` 改 `_encoder.Encode(EventKind.StructureModified, new StructureModifiedPayload(structureId, inst.Anchor, inst.ModuleId, orientation, variant, modifiedFields))`(**六字段全载**,原缺陷的 orientation/variant 丢弃已消除)
  - `unity/Assets/Sim/World/StructureKinds.cs:13-19`(文件头注)—— 三支重复 payload struct 副本已删
  - `unity/Assets/Sim.Codec/PayloadEncoder.cs:44-49` —— `Encode<T>` 实现:编码 + 入池,返回真 `PayloadRef`
- **实测证据**:
  - `grep -rn "new PayloadRef(" unity/Assets/Sim/` **仅命中注释行**(`:12-13`、`:44`、`:131`、`:120` 全为文档说明),**代码零命中**。
  - 三处写入点(Place/Remove/Modify)全部经 `IPayloadEncoder`,零手搓。
  - `StructureWriter` 构造注入 `IPayloadEncoder`(`:133-139`),null 抛 `ArgumentNullException`。
- **判决**:✅ **已修**(对账件所报的「残留两处」在当下代码中**均已消除**)。
- **验证命令**:
  ```bash
  grep -rn "new PayloadRef(" unity/Assets/Sim/          # 期望:仅注释命中
  grep -n "_encoder.Encode" unity/Assets/Sim/World/StructureKinds.cs   # 期望:3 处
  ```
- ⚠️ **本评未运行 `structure_payload_encoder_test.cs`** —— story 自述「12/12」标 `未验证`。

### B5 —— `OccupancyOverlay` 独立类型

- **原判定**:无 `OccupancyOverlay` 独立类型,`slot_occupied` 无可投影对象。
- **修复落点**:`unity/Assets/Sim/World/World.cs:143-160`
  ```csharp
  public readonly struct OccupancyOverlay
  {
      private readonly IReadOnlyList<bool> _slotOccupied;
      public OccupancyOverlay(IReadOnlyList<bool> slotOccupied) { _slotOccupied = slotOccupied; }
      public bool IsBlocked(WorldPos cell, WorldGeometry geometry) { ... }  // 越界 fail-closed
  }
  ```
- **实测证据**:独立 `readonly struct`,`IsBlocked` 越界返回 `true`(fail-closed)。
- **判决**:✅ **已修**。
- **验证命令**:
  ```bash
  grep -n "struct OccupancyOverlay" unity/Assets/Sim/World/World.cs
  ```

### ID —— `StructureInstanceRegistry` id 类型 `int` vs `i64`(**独立验证**)

- **登记缺陷**:注册表 id 全链 `int`,而权威件定 `i64` ⇒ 超 2^31 静默回绕。
- **独立实测证据(缺陷仍在)**:
  - `unity/Assets/Sim/World/StructureKinds.cs:32` —— `public readonly int StructureId;`
  - `unity/Assets/Sim/World/StructureKinds.cs:53` —— `Dictionary<int, StructureInstance> _instances`
  - `unity/Assets/Sim/World/StructureKinds.cs:54` —— `private int _nextStructureId = 1;`
  - `unity/Assets/Sim/World/StructureKinds.cs:57` —— `public int Register(...)`
  - `unity/Assets/Sim/World/StructureKinds.cs:67 / :73 / :87 / :93 / :106 / :142` —— `Remove` / `Update` / `TryGet` / `GetInstanceAt` / `Count` / `PeekNextId` 全 `int`
  - **权威件**:`design/registry/entities.yaml:2068` —— `structure_id: i64(与 ItemInstanceId 同模式,计数器 + 高水位可重构)`(`:2083`、`:2098` 同)
  - **契约版载荷**:`unity/Assets/Sim.Contracts/Payloads/WorldPayloads.cs:112 / :129 / :144` —— `public readonly long StructureId;`
  - **测试桥接(证明不一致真实存在)**:`unity/Assets/Tests/EditMode/ModularBuilding/structure_kinds_test.cs:146` 与 `:154` 用 `(int)p.StructureId` 收窄转换;`:174-189` 文件末**显式登记该缺陷为「未修」**。
- **判决**:🔴 **未修 —— 缺陷确认仍在**。
- **严重度评估**:**中(潜在,契约违规)**。理由:
  - **可达性极低** —— 回绕需累计 ~2.1×10⁹ 次放置;单局游戏不可能达到。
  - **但失败模式是「静默」** —— 恰是本项目 ADR-006 判为最高危的一类(静默失败)。
    一旦事件流折叠/高水位重构(`max(id)+1`)遇上 > `int.MaxValue` 的 id,注册表**承载不了**:
    要么 `(int)` 收窄静默截断,要么重建分叉 —— 与 ADR-010 §五 / ADR-006 Amendment B 的
    「计数器 + 高水位可重构」语义**直接冲突**。
  - **契约面已违规** —— `entities.yaml` 与契约版载荷是 `i64`,注册表是 `int`;同一 id 在流里是 long、
    在内存表里是 int,存在**两处不一致的宽度**。
  - **本评未评估**:是否存在 `IIdAuthority` 侧的高水位口径与注册表联动的实现(未读 `IIdAuthority` 全貌)。标 `未验证`。
- **验证命令**:
  ```bash
  grep -n "int StructureId\|Dictionary<int\|_nextStructureId\|public int Register\|public int PeekNextId" unity/Assets/Sim/World/StructureKinds.cs
  sed -n '2068p' design/registry/entities.yaml
  grep -n "long StructureId" unity/Assets/Sim.Contracts/Payloads/WorldPayloads.cs
  ```

---

## 3. 新发现(本次评审新查出)

### 🟠 N1 —— `StructureInstanceRegistry.Register` 的占用格集恒 = {anchor}(高)

- **落点**:`unity/Assets/Sim/World/StructureKinds.cs:60-61`
  ```csharp
  // 计算占用格(简化:仅锚点格;完整实现需查模块目录)
  var occupiedCells = new List<WorldPos> { anchor };
  ```
- **问题**:注册表**不查模块目录**计算真实足迹 —— 无论模块占几格,`StructureInstance.OccupiedCells`
  恒为单格 `{anchor}`。而 `PlaceableChecker.ComputeOccupiedCells`(`:109-134`)对**同一模块**却算出
  完整多格足迹。⇒ **同一模块的占用格集在「放置判定」与「实例表」两处不一致**。
- **后果(可追溯至已登记的 BLOCKING)**:
  1. **B2 的修复被此抵消** —— `DemolishChecker` 逐格判实体,但被迭代的集合恒为单格 ⇒ 多格模块
     的非锚点足迹格**不参与**拆除实体检查(玩家站在其上不阻挡拆除)。
  2. `World.GetModuleAt`(`World.cs:114-124`)经 `GetInstanceAt` **只匹配锚点格** ⇒ 非锚点足迹格
     查不到模块。
- **严重度**:**高** —— 静默语义缺口,且**直接削弱本 epic 已判「已修」的 B2**。
- **验证命令**:
  ```bash
  sed -n '56,64p' unity/Assets/Sim/World/StructureKinds.cs
  sed -n '109,134p' unity/Assets/Sim/World/PlaceableChecker.cs
  ```

### 🟠 N2 —— `World` 占用表与生产写路径未接线(高)

- **落点**:`unity/Assets/Sim/World/World.cs:60-87` 定义 `OccupyCell` / `FreeCell` / `OccupyCells` / `FreeCells`;
  `unity/Assets/Sim/World/StructureKinds.cs:147` 的 `Place` **只调 `_registry.Register`**,
  **从不调 `World.OccupyCells`**。
- **实测证据(grep)**:
  ```bash
  grep -rn "OccupyCells\|FreeCells\|OccupyCell\|FreeCell" unity/Assets/Sim/ unity/Assets/Tests/EditMode/ModularBuilding/
  ```
  命中:**仅 `World.cs` 的定义体** + **`world_test.cs` 的直接调用**。
  **零生产调用方** —— `StructureWriter` 未接线。
- **问题**:放置结构**不会**标记世界占用表 ⇒ `World.IsSlotOccupied` / `IsEffectivelyWalkable`
  (F-23-1 的 `EffectiveWalkable = Nav ∧ ¬Overlay.blocked`,**本 epic 的头号交付物**)在放置后
  **不反映**结构占用。`world_test.cs` 直接手工调 `OccupyCell` 掩盖了该缺口(它测 `World` 隔离体,
  不测 `Place → Occupy` 集成)。
- **严重度**:**高** —— Overlay 合成点(23 的三大职责之一)与写路径**未连通**;在集成层,
  `EffectiveWalkable` 目前对「已放置结构」不生效。
- **⚠️ 免责边界**:可能存在**尚未实现的上层编排者**(调用 `Place` 后再调 `OccupyCells`);
  本评只证明**当下仓库内无此调用方**。若属「后续 story 范围」,须在 EPIC 显式登记,
  不得留作隐含假设。
- **验证命令**:
  ```bash
  grep -rn "OccupyCells\|FreeCells" unity/Assets/Sim/
  grep -rn "OccupyCell\|FreeCell" unity/Assets/Tests/EditMode/ModularBuilding/
  ```

### 🟡 N3 —— `BuildSlotCatalog.IsInBuildSlotRegion` = `_slots.ContainsKey`(低,登记项)

- **落点**:`unity/Assets/Sim/World/BuildSlotCatalog.cs:82-89`(自陈「P0 简化:有槽位即区域内」)。
- **问题**:条件③ `OutOfRegion` 对**多格模块**要求每个足迹格**自身**是注册槽位,否则拒绝 ——
  与「区域 = 多边形包含」(ADR-022 烘焙数据)语义不同。P0 已显式自陈简化,非新缺陷,
  **但须确认这是否与 `world_buildslots.json` 的实际导出粒度一致**。本评 `未验证` 烘焙数据。
- **严重度**:低。

---

## 4. 转 Complete 的前置

1. **闭合 B2 的实质退化** —— 修 `StructureInstanceRegistry.Register`(N1)使其经模块目录
   (`IModuleCatalog.GetModuleDefinition`)计算**真实多格足迹**,与 `PlaceableChecker.ComputeOccupiedCells` 同源。
   (否则 B2 的「已修」是纸面的。)
2. **接线 Overlay 写路径(N2)** —— 明确 `Place`/`Remove` 后由谁调 `World.OccupyCells`/`FreeCells`;
   若归后续 story,须在 EPIC 登记并注明「F-23-1 合成目前对已放置结构不生效」。
3. **处置 id 类型缺陷** —— 注册表 id 全链升 `long`(同步 `IIdAuthority` 机制 A 高水位口径);
   或**显式降级**为已知缺口并在 `entities.yaml` / EPIC 登记(不得只留测试注释)。
4. **补跑测试并落证据** —— 本评**未运行**任何测试;story-007 自述的 63/63 与全量 2060
   均标 `未验证`。转 Complete 前须以可复现命令重跑并落 `production/qa/evidence/`。
5. **确认 ADR-029 §③ 的 b6 门已绿** —— `AssemblyGates.cs:256-257` 的 `PayloadRefWaivers`
   已空(自述 2026-10-03 撤销全部豁免);本评**仅读源码**,标 `未验证执行`。

---

## 5. 本评审未覆盖 / 未验证清单(诚实边界)

| 项 | 状态 |
|---|---|
| 运行 EditMode 测试(63/63 / 2060 全量) | `未验证`(本评未启动 Unity) |
| b2/b4/b5/b6 门的实际执行结果 | `仅文档/源码,未验证执行` |
| ADR-015 全文 | `未读`(本评只读 ADR-029 全文) |
| `world_buildslots.json` / `build_modules.json` 烘焙产物与 N3 的一致性 | `未验证` |
| `IIdAuthority` 的高水位实现与注册表是否已有联动 | `未验证`(只确认接口文件存在) |
| `HostEmergencyProcessor` / `PoiStateMachine` 的接线质量(同族 b6 面) | `未验证`(非本 epic 范围) |
| 是否存在调用 `Place` 后再 `OccupyCells` 的上层编排者 | `未验证`(本评只证当下仓库无调用方) |
