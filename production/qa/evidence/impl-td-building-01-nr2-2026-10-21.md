# `TD-building-01` 立债件 —— 23 生产装配根零落点(N-r2)

**日期**:2026-10-21
**缘起**:用户问「`IModuleCatalog` 生产实现 / `ModuleDefinition.Cost` / `DemolishChecker` 读目录 /
`world_buildslots.json` / `AC-23-08` 目录行触发 —— 全归 23 装配轮与 54。**什么时候去做**」
**用户裁定(两轮 AskUserQuestion)**:
- ① 「登记 TD + 排进 sprint-20」→ 实测后**改判为「不写 sprint-20,只登记」**
- ② 54 关卡工具 → **「先不立 epic,但把缺口写进路线图 §七」**

> ⚠️ **裁定 ① 的一次改判,须如实登记**:用户首轮选的是「登记 TD **+ 排进 sprint-20**」。
> 我在落笔前实测发现 **sprint-20 的 `clinic-004/005` 只消费 23 的只读面**
> (`IPlacedStructureReadView`),**不创造写面消费者**(`Place`/`Remove` 的发起方归 42 的 `O-23-6`,
> 状态「⏳ 待 42 下一轮」)⇒ 把整条 N-r2 挂 sprint-20 会**把半边写成整边**。
> 我把这一点摆出后,用户改选 **「不写 sprint-20,只登记」**。
> **这不是事后改口,是裁定前先摆出了会推翻它的实测。**

---

## 一、病因复述(为什么这批东西「全归 23 装配轮」是一句空话)

用户原话把这五件归给「23 装配轮与 54」。实测下来,**这两个归属方都不是「会到来的轮次」**:

| 归属 | 实测状态 |
|---|---|
| **23 装配轮**(= N-r2) | **重开条件 2026-10-09/10-10 已触发**(Boot 装配轮开工、`CompositionRoot` 已建、门④冒烟绿),但**接线从未落**;而 §6.5 的 sprint-16…29 表里 `modular` 三处命中**全是「拿 23 当上游前提」,无一处是「做 23」** |
| **54 关卡工具** | **无 epic 目录、无 story、不在任何 sprint、债册无条目** —— 却被三处引用(ADR-022 §三 · `systems-index.md:90,170` · `world-ecozones/story-005` 的 EXTERNAL 前置) |

⇒ 所以「什么时候去做」的诚实答案是:**没有任何一个已排期的 sprint 会自己带上它们**。

---

## 二、实测(扫描面 = `unity/Assets/**/*.cs` 全树,`/Tests/` 单列)

### 2.1 四层构造点:生产树**全零**

| 构造点 | 总 | 测试 | **生产** | 测试树落点 |
|---|---|---|---|---|
| `new World(` | 1 | 1 | **0** | `Tests/EditMode/ModularBuilding/world_test.cs:39` |
| `new StructureWriter(` | 10 | 10 | **0** | `structure_occupancy_wiring_test.cs` ×7 / `structure_kinds_test.cs` / `structure_payload_encoder_test.cs` ×2 |
| `new BuildSlotCatalog` | 4 | 4 | **0** | `build_slot_catalog_test.cs` / `placeable_checker_test.cs` / `modifiable_checker_test.cs` / `world_test.cs` |
| `new WorldGeometry(` | 10 | 10 | **0** | `world_lattice_test.cs` ×8 / `world_test.cs` |

⇒ **23 的四层(几何 / 槽位骨架 / 世界 / 写者)在生产树里一层都没被构造过。**

复算命令:
```bash
for pat in "new World(" "new StructureWriter(" "new BuildSlotCatalog" "new WorldGeometry("; do
  total=$(grep -rn -- "$pat" unity/Assets --include=*.cs | wc -l)
  tests=$(grep -rn -- "$pat" unity/Assets --include=*.cs | grep -c "/Tests/")
  echo "$pat : 总 $total / 测试 $tests / 生产 $((total-tests))"
done
```

### 2.2 两个接口:生产实现**全零**

| 接口 | 定义 | 生产实现 | 测试替身 |
|---|---|---|---|
| `IModuleCatalog` | `Sim/World/PlaceableChecker.cs:174` | **0** | 4 个 stub |
| `IPlacedStructureReadView` | `Sim/Clinic/IPlacedStructureReadView.cs` | **0** | `FakeView` ×2 / `FakeStructureReadView` / `MutableView` |

⚠️ **`IPlacedStructureReadView` 这一条是本轮新揭的** —— 原 N-r2 只数 `World` / `StructureWriter`,
够不着它;而它正是 `clinic-004/005` 的布局输入面。

### 2.3 两处字段/数据缺口

| 项 | 实测 |
|---|---|
| `ModuleDefinition` 字段集 | **五字段**:`ModuleId` / `SlotType` / `LocalOccupancy` / `ValidOrientations` / `Refundable`(`PlaceableChecker.cs:138-144`)。**`Cost` 不在其中** |
| `cost` 在 `Sim/World` 的形态 | **唯一形态 = 形参**:`DemolishChecker.CalculateRefund(int cost, int demolishRefundRatio)`(`:86`)/ `ValidateRefundable(int cost, int demolishRefundRatio)`(`:100`)。**由调用方喂,不从目录读**;且 `CalculateRefund` 的生产树调用点 **= 0** |
| `BuildSlotRegion` | `BuildSlotCatalog.IsInBuildSlotRegion`(`:86`)**P0 简化为「有槽位即区域内」**;其注释(`:83-85`)自陈「多边形包含检查需 ADR-022 烘焙数据,**当前无多边形输入**」 |
| `world_buildslots.json` | **全仓零存在**(`assets/data/` 26 份 JSON 无它,`DataCooked/` 无任何 slot 产物) |

---

## 三、`AC-23-08` 的实况:它是**三条**判据,只落了一条

| 半边 | 状态 | 判据 |
|---|---|---|
| `(0,0) ∈ OccupiedCells_local` | ✅ **已落** | `BuildModulesBinder` 锚点格门;证伪轮 C 坐实(停用门 ⇒ `test_buildModules_missingAnchorCell_isRejected` 红) |
| `refundable ⇒ cost ≥ ⌈1/R⌉` | ⚠️ **恒真** | `R = 1` ⇒ `⌈1/R⌉ = 1`;目录 9 行 `cost ∈ [1,99]` ⇒ **无一行能触发** |
| 占用格 ⊆ `BuildSlotRegion` | 🔴 **零载体** | `BuildModulesBinder` **无此门**;Region 数据不存在(§2.3) |

> ⚠️ **本轮对上一轮 commit 的一处订正**:`7ff1d37` 的 message 写「`AC-23-08` 仍无目录行触发
> (`R = 1` 下自动满足)」—— **只说了第二半**,漏了第三半「Region 校验根本没写」。
> 后者比「无行触发」更硬:它不是**没有判别力**,是**代码不存在**。
> 原文不删,订正登记于此 + GDD 补注。

---

## 四、落账(四文件)

| # | 文件 | 改动 |
|---|---|---|
| 1 | `docs/tech-debt-register.md` | 新立 **`TD-building-01`**(判据面扩为四层构造点 + 两接口 + `Cost` + Region);§★ 索引 **+1 行**;六订计数注 **六处**;`TD-itemdb-01` 触发条件补注(指向的「那一轮」已具名) |
| 2 | `production/milestones/p0-remaining-roadmap-2026-10-20.md` | §七 新增 **SP-10**(54 的「三处引用零排期载体」);§7.1 标题 `SP-1…SP-9` → `SP-1…SP-10`;§5.2 第三源补注(第三源**不能自己发现缺口**,完备性取决于「有人先入册」);**SP-9 补注**(其「实测」列自身已半陈旧:`C_max` 已 1/64 → 1/8) |
| 3 | `production/epics/modular-building/EPIC.md` | N-r2 行 + 未闭项节 + Next Step 三处补注:已入债册、判据扩面理由、**读侧/写侧分两半不合并** |
| 4 | `design/gdd/modular-building.md` | §规则九后新增 `AC-23-08` 三半边进度表;`AC-23-08` 表行补注「本条含三条判据」 |

---

## 五、计数(必须明写,不能藏)

### 5.1 复算命令输出(六订后实测)

```text
条目 97 | 状态 {'open': 71, 'partial': 6, 'closed': 19, '**done**(2026-10-21)': 1} | P0 面 {'Y': 88, 'N': 8, '~~**Y**~~': 1}
未闭 P0 面 = 73 （已闭但曾标 P0 面的 15 条不计）
M3 Exit 第二条合取（31 个 epic,每个名下「未闭 TD 的 P0 面 = 0」）⇒ 假 —— 73 条,分布在 13/31 个 epic
```

### 5.2 与五订基线的差

| 列 | 五订 | 六订 | 差 |
|---|---|---|---|
| 条目 | 96 | **97** | **+1** |
| `open` | 70 | **71** | **+1** |
| `Y` | 87 | **88** | **+1** |
| **未闭 P0 面** | 72 | **73** | **+1** |
| **epic 分布** | 12/31 | **13/31** | **+1(`modular-building` 首次入列)** |

⚠️ **与五订性质相反**:五订是同一行换字(**三个数一位不动**);六订是**真新增一笔债** ⇒ **三个数全动**。
⚠️ **不是「回到四订前」**:四订的 +1 落在 `item-database`;六订的 +1 落在 **`modular-building` —— 一个此前名下零债的 epic**。
⚠️ **由此产生一句必须改写的话**:「23 已 `Complete` 且名下无债」**自此不成立**。M3 Exit ② 判定仍为假(本就为假),变的是**分母与其构成**。

---

## 六、未做 / 未证(禁借绿)

- **未写 sprint-20**(用户裁定)。排期归 `/sprint-plan` 取件时按 §6.3 **R-4** 现裁 ——
  **本轮不为它预支任何判据**。
- **未立 54 的 epic**(用户裁定)。`world_buildslots.json` 在 P0 内**仍不产出**;
  23 的 Region 校验**维持 P0 简化实现**;`AC-23-08` 第三半边**仍为已知缺口**。
- **未改任何代码**。本轮是**登记轮**,零实现。
- **写侧(`Place`/`Remove` 生产发起方)未登记触发条件之外的东西** ——
  它归 42 的 `O-23-6`(`modular-building.md:494`,状态「⏳ 待 42 下一轮」),该义务**自身也没有排期**。
- **IL2CPP 三格 NOT-RUN**(本机仅 Mono)—— 与本轮无关,照旧。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
- **未 commit**(用户未指令)。

---

## 七、本轮的一处自我订正

`SP-9` 行(路线图 §七)的「实测」列写「`OQ-24-5` 已裁 `n_max = 64`,**并连带拍定 `C_max = 1/64`**」——
该句**在写下时正确**,但 `C_max` 已于 **2026-10-21** 由 `1/64` 改裁为 **`1/8`**(与 `n_max` 解耦,耦合式 `n·C_max ≤ 1` 作废)。
**不影响 SP-9 的结论**(`n_max = 64` 未变、`partial` 仍该转 `covered`),但**引该行时不得照抄 `C_max = 1/64`**。
已就地补注,原文不删。
