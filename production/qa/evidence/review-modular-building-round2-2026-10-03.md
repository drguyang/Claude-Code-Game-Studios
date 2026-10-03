# 代码评审(第二轮)—— modular-building(系统 6 建造)

> **评审对象**:`unity/Assets/Sim/World/`(StructureKinds / PlaceableChecker / World / DemolishChecker)
> + `unity/Assets/Tests/EditMode/ModularBuilding/`
> **评审日期**:2026-10-03
> **⚠️ 本轮为第二轮;评的是修复后的代码**(第一轮 = `review-modular-building-2026-10-03.md`)。
> **评审方式**:逐条独立复核 —— 读源码 / grep 落点,**不采信 commit message 与 story 自述**
> **引擎**:Unity 6.3 LTS(6000.3.24f1)

> ## 性质声明(务必先读)
> - 本报告**未运行 Unity / 未执行测试套件** —— 所有「测试绿」类断言一律标 `未验证`。
> - 凡本报告未亲自读源码确认的条目,一律显式标 `未验证`。
> - 「缺陷若还原,测试是否会红」为**静态推理**(读断言 + 读被断言代码),非实跑突变。
> - 本报告对第一轮判定**重新独立验证**,不追认其结论。

---

## 1. 结论摘要表

| # | 项 | 第一轮判决 | 本轮判决 | 依据 |
|---|---|---|---|---|
| C1 | `Register` 占用格集恒 = `{anchor}` | 🟠 高 | ✅ **已修(但为 opt-in,见 N-r1)** | `StructureKinds.cs:67-76` / `:172-181` |
| C2 | `World.OccupyCells/FreeCells` 零调用方 | 🟠 高 | ✅ **已修(类级;端到端见 N-r2)** | `PlaceableChecker.cs:191` / `World.cs:18` / `StructureKinds.cs:186-187,206-207` |
| T | 新测试 `structure_occupancy_wiring_test.cs`(5 例) | — | ✅ **判据有效(静态推理)**,`未跑` | 断言读法(见 §2-T) |
| B1 | PlaceableChecker 条件⑥ entity check | ✅ 已修 | ✅ **仍成立** | `PlaceableChecker.cs:95` / `IPresenceQuery.cs:33` |
| B2 | DemolishChecker entity check 改占用格 | ⚠️ 部分修 | ✅ **本轮升级为真修(条件于 C1 opt-in 路径)** | `DemolishChecker.cs:66-69` + C1 修复 |
| B3 | DemolishChecker shell detection 查模块目录 | ✅ 已修 | ✅ **仍成立** | `DemolishChecker.cs:59-62` |
| B4 | `Structure*` payload bit-packing | ✅ 已修 | ✅ **仍成立** | `StructureKinds.cs:190,212,245` / grep 零 `new PayloadRef(` in Sim |
| B5 | `OccupancyOverlay` 独立类型 | ✅ 已修 | ✅ **仍成立** | `World.cs:143` |
| C8/ID | 注册表 id `int` vs `entities.yaml:2068` `i64` | 🔴 未修 | 🔴 **未修 —— 独立复核:缺陷仍在** | `StructureKinds.cs:32,53,54,67,79,105` |
| **N-r1** | (新)C1/C2 修复为 **opt-in**,`moduleCatalog == null` 静默退回旧缺陷 | — | 🟠 **新发现 · 中** | `StructureKinds.cs:173-187` |
| **N-r2** | (新)无生产装配根;端到端经**真 `World`** 未被测 | — | 🟡 **新发现 · 低-中** | grep 无 `new World(` / `new StructureWriter(` 于非测试 |

**一句话**:C1/C2 两条高严重度缺陷**在源码层确已修复**,新测试**判据有效**;
B1/B3/B4/B5 维持已修;B2 **本轮升为真修**(其纸面化根因 = C1 已闭)。
**但**:修复是**opt-in 的**(N-r1)—— 漏传 `moduleCatalog` 会**静默**退回 C1+C2 原缺陷;
且**无生产装配根**(N-r2),端到端经真 `World` 未被测。**C8/ID 仍未修**。
⇒ 本 epic **仍不应转 Complete**(前置见 §4)。

---

## 2. 逐条详节

### C1 —— 占用格集恒 = {anchor}

- **原判定(第一轮 N1)**:`StructureInstanceRegistry.Register` 自造 `new List<WorldPos> { anchor }`
  ⇒ 多格模块的非锚点足迹格不进实例表 ⇒ 抵消 B2 的修复。**🟠 高**。
- **修复落点**:
  - `unity/Assets/Sim/World/StructureKinds.cs:67-76` —— `Register(WorldPos anchor, int moduleId, int orientation, int variant, IReadOnlyList<WorldPos> occupiedCells = null)`;
    `:73` `var cells = occupiedCells ?? new List<WorldPos> { anchor };`;`:74` 存入 `StructureInstance`。
  - `unity/Assets/Sim/World/StructureKinds.cs:172-181` —— `StructureWriter.Place` 现**先经模块目录算真足迹**:
    ```csharp
    IReadOnlyList<WorldPos> occupied = null;
    if (_moduleCatalog != null) {
        var defOpt = _moduleCatalog.GetModuleDefinition(moduleId);
        if (defOpt.HasValue)
            occupied = PlaceableChecker.ComputeOccupiedCells(defOpt.Value, anchor, orientation);
    }
    int structureId = _registry.Register(anchor, moduleId, orientation, variant, occupied);
    ```
  - 构造注入 `IModuleCatalog moduleCatalog`(`:154-164`),`Place` 使用之。
- **实测证据**:
  - 足迹**与放置判定同源** —— `PlaceableChecker.cs:81` 放置判定亦调 `ComputeOccupiedCells(moduleDef, anchor, orientation)`;
    `Place` 调**同一静态函数**(`:178`)。⇒ 两处足迹自此**必然一致**(第一轮的「两处不一致」根因消除)。
  - `StructureInstance` 的 `OccupiedCells` 字段(`:37`)仍存在,`Update` 保留原足迹(`:94` 传 `inst.OccupiedCells`)。
- **判决**:**✅ 已修(主路径)** —— 真足迹路径成立。
  **⚠️ 但兜底路径仍 = `{anchor}`**(`:73`),且该兜底在 `moduleCatalog == null` 时**会被 `Place` 实际走到**(`:173` `occupied` 保持 null)。
  ⇒ 缺陷可**静默复现**。详见 §3 **N-r1**。
- **验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  sed -n '67,76p'  unity/Assets/Sim/World/StructureKinds.cs
  sed -n '172,187p' unity/Assets/Sim/World/StructureKinds.cs
  sed -n '109,134p' unity/Assets/Sim/World/PlaceableChecker.cs
  ```

### C2 —— Overlay 写路径未接线

- **原判定(第一轮 N2)**:`World.OccupyCells/FreeCells` **零生产调用方**;`Place` 只调 `registry.Register`
  ⇒ 放置结构不标记占用表 ⇒ F-23-1 `EffectiveWalkable` 对已放置结构不生效。**🟠 高**。
- **修复落点**:
  - **新增接口** `unity/Assets/Sim/World/PlaceableChecker.cs:191-198`:
    ```csharp
    public interface IWorldOccupancy {
        void OccupyCells(IEnumerable<WorldPos> cells);
        void FreeCells(IEnumerable<WorldPos> cells);
    }
    ```
  - **实现者** `unity/Assets/Sim/World/World.cs:18` —— `public sealed class World : IWorldOccupancy`;
    `OccupyCells`(`:76-80`)/ `FreeCells`(`:83-87`)已在库,签名与接口**逐字匹配**。
  - **消费者接线** `unity/Assets/Sim/World/StructureKinds.cs`:
    - `:138` 字段 `private readonly IWorldOccupancy _occupancy;`,构造注入(`:157`);
    - `:185-187` `Place`:`if (_occupancy != null && occupied != null) _occupancy.OccupyCells(occupied);`
    - `:205-207` `Remove`:`if (_occupancy != null) _occupancy.FreeCells(inst.OccupiedCells);`
- **实测证据**:
  - 接口定义(`PlaceableChecker.cs:194-197`)与 `World` 实现(`World.cs:76,83`)**签名一致**(`IEnumerable<WorldPos>`)⇒ **匹配**。
  - `grep -rn "IWorldOccupancy"` 命中:定义 1 处 + `World` 实现 1 处 + `StructureWriter` 消费 3 处 + 测试 spy 1 处 ⇒ **接线成立**。
  - `Remove` 用 `inst.OccupiedCells`(真足迹)释放 ⇒ 与 `Place` 的占用对称。
- **判决**:**✅ 已修(类级)** —— 生产写路径(`StructureWriter.Place/Remove`)确已调 `OccupyCells`/`FreeCells`。
  **⚠️ 两点保留**:① `Place` 的 `occupied != null` 前置 ⇒ `moduleCatalog == null` 时 **Overlay 也不写**(同 N-r1);
  ② 全仓**无生产装配根**构造 `World` / `StructureWriter`(见 N-r2),故「真 `World` 被接线」仅在测试 spy 上验证。
- **验证命令**:
  ```bash
  grep -rn "IWorldOccupancy" unity/Assets/
  sed -n '18p;76,87p'  unity/Assets/Sim/World/World.cs
  sed -n '185,187p;205,207p' unity/Assets/Sim/World/StructureKinds.cs
  ```

### T —— 新测试 `structure_occupancy_wiring_test.cs` 是否真能检出这两条缺陷

- **文件**:`unity/Assets/Tests/EditMode/ModularBuilding/structure_occupancy_wiring_test.cs`,`[Test]` 计数 = **5**(与任务书一致)。
- **判据逐例(静态推理)**:

  | 用例 | 断言 | 若还原缺陷是否红 |
  |---|---|---|
  | `test_c1_register_usesFullFootprint_notAnchorOnly`(`:54`) | `inst.OccupiedCells.Count == 2` 且含 `(10,0,10)`+`(11,0,10)` | **红** —— 旧实现恒 1 格 ⇒ `Count==2` 失败。**检出 C1** |
  | `test_c1_footprintMatchesPlaceableChecker`(`:70`) | 实例足迹 `AreEquivalent` 于 `ComputeOccupiedCells(...)` | **红** —— 旧实现 `{anchor}` ≠ 真足迹(90° 旋转后多格)。**检出 C1 同源** |
  | `test_c2_place_writesOccupancy`(`:91`) | `occupancy.OccupyCalls == 1` 且 `LastOccupied.Count == 2` | **红** —— 旧实现零调用 ⇒ `OccupyCalls==0`。**检出 C2** |
  | `test_c2_remove_freesOccupancy`(`:107`) | `occupancy.FreeCalls == 1` 且 `LastFreed.Count == 2` | **红** —— 旧实现零调用。**检出 C2** |
  | `test_c2_noCatalog_noOccupancy_noThrow`(`:123`) | `DoesNotThrow` | 恒绿(仅测不抛)。**不检出** |

- **实测证据**:用例用**两格 L 形**模块(`LShapeLocal = {(0,0,0),(1,0,0)}`,`:28`)使「非锚点足迹」**可观测**;
  `SpyOccupancy` 记录调用次数与末次载荷(`:139-154`)。**设计正确** —— 单格模块无法暴露 C1,两格模块可以。
- **判决**:**✅ 判据有效(静态推理确凿)** —— 前 4 例**在缺陷还原时必红**,第 5 例为向后兼容冒烟(不承担检出)。
  **⚠️ 本评审未运行测试**(未启动 Unity)⇒ 「实测红/绿」标 `未验证`。
- **⚠️ 覆盖缺口**:全部经 **`SpyOccupancy`**,**无一例**经**真 `World`** 断言 `IsEffectivelyWalkable`(见 N-r2)。
- **验证命令**:
  ```bash
  grep -c "\[Test\]" unity/Assets/Tests/EditMode/ModularBuilding/structure_occupancy_wiring_test.cs   # 期望 5
  sed -n '53,67p;90,104p' unity/Assets/Tests/EditMode/ModularBuilding/structure_occupancy_wiring_test.cs
  ```

### B1–B5 —— 原 BLOCKING 当下状态

- **B1**(条件⑥ entity check)✅ —— `PlaceableChecker.cs:95` `if (_presenceQuery.IsPresentAt(cell)) return PlaceableResult.EntityOnCell;`,
  循环在占用格内(`:84-97`);接口 `Sim.Contracts/IPresenceQuery.cs:33` 存在。**仍成立**。
- **B2**(DemolishChecker 逐占用格判实体)✅ **本轮升级为真修** —— `DemolishChecker.cs:66-69` 逐 `inst.OccupiedCells` 判 `IsPresentAt`;
  第一轮判「部分修」的**根因是 C1 使集合恒 = `{anchor}`**。**C1 已修** ⇒ 被迭代集合现为**真多格足迹** ⇒ 判据实质成立。
  **⚠️ 条件**:仅当实例经 **C1 真足迹路径**注册(即 `StructureWriter` 注入了 `moduleCatalog`);兜底路径下 B2 仍纸面(见 N-r1)。
- **B3**(shell detection 查模块目录)✅ —— `DemolishChecker.cs:59-62` 经 `IModuleCatalog.GetModuleDefinition` 取 `SlotType`,
  目录缺失 fail-closed 返回 `StructureNotFound`。**仍成立**。
- **B4**(payload 无 bit-packing)✅ —— `StructureKinds.cs` 三处写入(`:190` Place / `:212` Remove / `:245` Modify)全经 `_encoder.Encode`;
  `Modify` 六字段全载(`:245-247`,含 orientation/variant/modifiedFields,原「算出即弃」已消除);
  `grep -rn "new PayloadRef("` 于 `Sim/`+`Sim.Codec/` **仅命中 `CodecReader.cs:119` 与 `PayloadEncoder.cs:48` 两处 codec 内部合法构造** + 注释,`StructureKinds`/`PoiStateMachine` 零代码命中。**仍成立**。
- **B5**(`OccupancyOverlay` 独立类型)✅ —— `World.cs:143` `public readonly struct OccupancyOverlay`,`IsBlocked` 越界返回 `true`(fail-closed,`:153-159`)。**仍成立**。
- **判决**:B1/B2/B3/B4/B5 **全部已修**(B2 本轮由「部分」升「真修」)。

### C8/ID —— `StructureInstanceRegistry` id 类型 `int` vs `entities.yaml:2068` `i64`(**独立复核**)

- **登记缺陷**:注册表 id 全链 `int`,权威件定 `i64` ⇒ 超 2^31 静默回绕。
- **独立实测证据(缺陷仍在,逐行坐实)**:
  - `unity/Assets/Sim/World/StructureKinds.cs:32` —— `public readonly int StructureId;`
  - `:53` —— `Dictionary<int, StructureInstance> _instances`
  - `:54` —— `private int _nextStructureId = 1;`
  - `:67` —— `public int Register(...)`;`:70` `int id = _nextStructureId++;`
  - `:79` —— `public bool Remove(int structureId)`;`:105` —— `public int GetInstanceAt(WorldPos anchor)`
  - `:139` / `:167` / `:183` —— `StructureWriter._nextStructureId`(int)/ `PeekNextId`(int)/ `_nextStructureId + 1`
  - **权威件**:`design/registry/entities.yaml:2068` —— `payload_schema: "structure_id: i64(与 ItemInstanceId 同模式,计数器 + 高水位可重构)…"`
  - **契约版载荷**:`unity/Assets/Sim.Contracts/Payloads/WorldPayloads.cs:112 / :129 / :144` —— `public readonly long StructureId;`
  - **测试桥接(证明不一致真实存在)**:`structure_kinds_test.cs:146 / :153` 用 `(int)p.StructureId` 收窄;
    `:174-189` 文件末**显式登记该缺陷为「未修 · 归独立轮」**(逐字确认)。
- **判决**:🔴 **未修 —— 缺陷确认仍在**。
- **严重度**:**中(潜在,契约违规)**。可达性极低(需 ~2.1×10⁹ 次放置),但失败模式**静默**,
  与 ADR-006 / ADR-010 §五「计数器 + 高水位可重构」语义直接冲突。**修法须同步 `IIdAuthority` 机制 A 高水位口径**(跨契约面)。
- **验证命令**:
  ```bash
  grep -n "int StructureId\|Dictionary<int\|_nextStructureId\|public int Register\|public int PeekNextId" unity/Assets/Sim/World/StructureKinds.cs
  sed -n '2068p' design/registry/entities.yaml
  grep -n "long StructureId" unity/Assets/Sim.Contracts/Payloads/WorldPayloads.cs
  sed -n '146p;174,189p' unity/Assets/Tests/EditMode/ModularBuilding/structure_kinds_test.cs
  ```

---

## 3. 新发现

### 🟠 N-r1 —— C1/C2 修复为 **opt-in**;`moduleCatalog == null` 静默退回旧缺陷(中)

- **落点**:`unity/Assets/Sim/World/StructureKinds.cs:172-187`
  ```csharp
  IReadOnlyList<WorldPos> occupied = null;
  if (_moduleCatalog != null) { ... occupied = PlaceableChecker.ComputeOccupiedCells(...); }
  int structureId = _registry.Register(anchor, moduleId, orientation, variant, occupied); // null ⇒ 兜底 {anchor}
  ...
  if (_occupancy != null && occupied != null) _occupancy.OccupyCells(occupied);          // null ⇒ 不写 Overlay
  ```
  以及 `:73` 兜底 `occupiedCells ?? new List<WorldPos> { anchor }`;构造参数 `moduleCatalog = null`(`:156`)与 `occupancy = null`(`:157`)均为**可选默认 null**。
- **问题**:修复**依赖调用方显式传入 `moduleCatalog`**。若生产装配根**漏传**(或未来新调用点不传):
  ① `occupied` 保持 null ⇒ `Register` 兜底 = `{anchor}` ⇒ **C1 缺陷静默复现**;
  ② `occupied != null` 为假 ⇒ **`OccupyCells` 不被调用** ⇒ **C2 缺陷静默复现**。
  **无 fail-closed**:不抛、不告警。且 `test_c2_noCatalog_noOccupancy_noThrow`(`:123`)把该路径断言为「**不得抛**」——
  **等于把静默退化路径合法化**。
- **严重度**:**中** —— 它使「已修」的效力**取决于调用点纪律**,而非类型/构建约束;
  与本项目最忌的**静默失败**同类(ADR-006)。修复本身正确,但**未把「必须传真足迹」变成不可绕过**。
- **建议**(供决策,非本轮裁决):`Place` 在 `moduleCatalog == null` 时 **fail-closed 抛异常**(或至少 `Debug.Assert`),
  使「未接线」成为**显式错误**;并删/改 `test_c2_noCatalog_noOccupancy_noThrow` 以反映该口径。
- **验证命令**:
  ```bash
  sed -n '154,164p;170,197p' unity/Assets/Sim/World/StructureKinds.cs
  sed -n '122,128p' unity/Assets/Tests/EditMode/ModularBuilding/structure_occupancy_wiring_test.cs
  ```

### 🟡 N-r2 —— 无生产装配根;端到端经**真 `World`** 未被测(低-中)

- **实测证据(grep,非测试)**:
  ```bash
  grep -rn "new StructureWriter" unity/Assets/ --include=*.cs | grep -v "/Tests/"   # 空
  grep -rln "new World(" unity/Assets/ --include=*.cs | grep -v "/Tests/"          # 空
  grep -rn "new DemolishChecker" unity/Assets/ --include=*.cs | grep -v "/Tests/"  # 空
  ```
  ⇒ `World` / `StructureWriter` / `DemolishChecker` **均无生产构造点**,仅测试构造。
- **问题**:
  1. C2 的接线**仅在 `SpyOccupancy` 上验证**;「真 `World` 实例被 `StructureWriter` 写入后
     `IsEffectivelyWalkable` 变 false」这一 **F-23-1 端到端语义未被测**(`world_test.cs` 仍是**手工** `OccupyCell` 直调,
     与 `Place` 无关 —— 第一轮的掩盖机制**未消除**)。
  2. `World.Structures`(`World.cs:26`)是 `World` **自建**的注册表;若未来装配根给 `StructureWriter` 传**另一个**
     `StructureInstanceRegistry`,则**实例表与 `World.GetModuleAt` 分叉**(第二真源风险)。
- **严重度**:**低-中** —— P0 尚无装配根可能是**既有结构性事实**(非本轮引入);但**至少应登记**,
  并补一例「`new World(...)` → `new StructureWriter(..., world, ...)` → `Place` → 断言 `IsEffectivelyWalkable==false`」的真端到端测试。
- **验证命令**:
  ```bash
  grep -rn "new StructureWriter\|new World(\|new DemolishChecker" unity/Assets/ --include=*.cs | grep -v "/Tests/"
  grep -n "OccupyCell\|IsEffectivelyWalkable" unity/Assets/Tests/EditMode/ModularBuilding/world_test.cs
  ```

### 🟡 N-r3 —— EPIC 状态行未反映本轮修复的「opt-in 保留」(低)

- `production/epics/modular-building/EPIC.md:6` 的 Status 行已记「✅ C1/C2 已闭(2026-10-03)… 5 例集成判据(突变测试坐实)」,
  但**未记** N-r1 的 opt-in 边界与 N-r2 的「无装配根 / 端到端未测」。
  自称「突变测试坐实」在本报告中**未获独立确认**(本评未跑测试)。
- **严重度**:**低**(文档/状态一致性)。

---

## 4. 转 Complete 的前置

1. **处置 N-r1(opt-in 静默退化)** —— 使「必须传真足迹」不可绕过:
   `Place` 在 `moduleCatalog == null` 时 **fail-closed**(抛或显式断言),
   并修正 `test_c2_noCatalog_noOccupancy_noThrow` 的口径(不得把静默退化合法化)。
2. **补真端到端测试(N-r2)** —— 经**真 `World`** 断言 `Place → IsEffectivelyWalkable == false` 与 `Remove → true`;
   并明确「`StructureWriter` 与 `World.Structures` 是否须同一注册表实例」(防第二真源)。
3. **处置 C8/ID(仍未修)** —— 注册表 id 全链升 `long` 并**同步 `IIdAuthority` 机制 A 高水位口径**;
   或**显式降级**为已知缺口并在 `entities.yaml` / EPIC 登记(不得只留测试注释)。
4. **补跑测试并落证据** —— 本评**未运行**任何测试;`structure_occupancy_wiring_test`(5 例)与全量的
   「红/绿」均标 `未验证`。转 Complete 前须以当前 HEAD 重跑 EditMode 并落 `production/qa/evidence/`。
5. **确认 ADR-029 §③ 的 b6 门已绿** —— `PayloadRefWaivers` 现状**本评未读**(标 `未验证`)。
6. **同步 EPIC 状态行**(N-r3)—— 记 N-r1 边界与 N-r2 缺口。

---

## 5. 本评审未覆盖 / 未验证清单(诚实边界)

| 项 | 状态 |
|---|---|
| 运行 EditMode 测试(`structure_occupancy_wiring_test` 5 例 / 全量) | `未验证`(本评未启动 Unity) |
| 「缺陷还原 ⇒ 测试红」的**实跑**突变 | `未验证`(仅静态推理,见 §2-T) |
| `PayloadRefWaivers` / b6 门实际执行结果 | `未验证`(本评未读该文件) |
| `IIdAuthority` 高水位实现与注册表是否已有联动 | `未验证`(仅确认接口/测试注释) |
| `world_buildslots.json` / `build_modules.json` 烘焙产物粒度 | `未验证`(本评未读烘焙数据) |
| ADR-029 全文 | `未读`(本评只读源码 + EPIC 状态行) |
| 生产装配根是否存在(未来 story / Boot 场景) | `未验证`(本评只证**当下仓库**无构造点) |
| `StructureWriter` 与 `World.Structures` 是否须同一实例 | `未验证`(设计意图未查 ADR) |
