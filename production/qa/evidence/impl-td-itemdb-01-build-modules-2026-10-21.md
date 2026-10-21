# `TD-itemdb-01` 兑现件 —— `build_modules.json` 作者态 + 烘焙 + 跨表同源门

**日期**:2026-10-21
**用户裁定**:「乙 · 现在补 `build_modules.json` 作者态」(AskUserQuestion)
**裁定二**:`module_id` 形状 =「甲 · `module_id` = int + 新增 `furn_type` 字符串列」
**债条**:`docs/tech-debt-register.md` §`TD-itemdb-01`(`open` → **`partial`**)
**未 commit**(用户未指令)

> ⚠️ **编号订正(起草期自查)**:本件最初被我写成「S17-5」,**是错的** ——
> `production/sprints/sprint-17.md:143` 的 `S17-5` 是另一件事(retro-14 行动项 5 /
> `TD-player-01` 回写)。本件不在 sprint-17 的 story 表内:它是 S17-4 轮里
> **从 GDD 义务登记(`O-24-1`)长出来的 TD 工作项**,按 `TD` 命名先例
> (`impl-td-player-01-2026-10-20.md` / `impl-td-audio-01-fix-2026-10-17.md`)归为
> `impl-td-itemdb-01-*`。**不占 sprint story 编号、不改 `sprint-status.yaml` 的 story 表。**

---

## 一、病因复述(为什么这条债不能只靠「补个 JSON」了事)

`TD-itemdb-01` 的实测七条里,真正卡住的不是「文件不存在」,而是**三方表示不可互推**:

| 侧 | `module_id` 类型 | 词汇 |
|---|---|---|
| `StructurePlacedPayload.ModuleId`(`Sim.Contracts/Payloads/WorldPayloads.cs:110-122`) | **`int`** | — |
| `entities.yaml:2106` 的 `payload_schema` | **`i32`** | — |
| `IPlacedStructureReadView.GetModuleId(int)`(`Sim/Clinic/IPlacedStructureReadView.cs:69`) | **`string`** | 24 的 `FURN_TYPES`(`bed` / `herb_cabinet` / …) |
| `BuildSlotCatalog.SlotType`(`Sim/World/BuildSlotCatalog.cs:17-27`) | `enum` | `None/Bed/Table/Cabinet/Plot/Decor/Shell` |

⇒ 若只落一个 `module_id: int` 的表,24 侧**拿不到它要的字符串**;若把 `module_id` 写成字符串,
则与 payload / registry 的 `i32` 冲突。

**裁定结论(甲案)**:`module_id` 保持 `int`(**23 侧零改动**),**另加一列 `furn_type: string|null`**
作 24 的桥。`furn_type = null` 表示「这行不是家具件」(如 `Shell` 外壳 / 非家具装饰)。
**24 侧需要一个 `int → string` 适配,而该适配今日不存在** —— 故本裁定**不破任何现存代码**。

⚠️ **`module_type` 与 `furn_type` 同名不同物**:前者是 `SlotType` 枚举名(`Bed`/`Table`/…),
后者是 24 的家具词汇(`bed`/`surgery_table`/…)。表内 `_note` 已写明,防后来者把两者合并。

---

## 二、交付面(实测落盘)

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `assets/data/build_modules.json` | **新** | 9 行模块;`schema_version: 1`;`_note` 逐条写明 21a 归属 / `cost` 是夹具值 / 第六字段的裁定来源 / 两套词汇不同物 / `[0,0,0]` 必在 |
| 2 | `unity/Assets/Editor.Tools.Bake/BuildModulesBinder.cs` | **新** | 阶段 2 绑定 + 四道域门 + 三层白名单 |
| 3 | `unity/Assets/Editor.Tools.Bake/BuildModulesCookedWriter.cs` | **新** | deterministic 编码;行序 = `module_id` 升序 |
| 4 | `unity/Assets/Editor.Tools.Bake/BuildModulesBake.cs` | **新** | `BakeFromRepo` / `BakeFromSourceText` / `DeclaredFurnTypes` |
| 5 | `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs` | **改** | `BuildModulesCookedAssetName` 常量 + `BakeBuildModulesCore`(**跨表同源门**) + 菜单项 |
| 6 | `unity/Assets/DataCooked/build_modules.cooked.bytes` | **新** | 20 字节头 + 载荷 |
| 7 | `unity/Assets/Tests/EditMode/ClinicMachine/build_modules_bake_test.cs` | **新** | 18 测 |
| 8 | `unity/Assets/Editor.Tools.Gates/FsWriteBoundaryGate.cs` | **改** | `Declarations` 第 7 条 |
| 9 | `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` | **改** | `BakedArtifacts` 21 条 + `pinnedDeclared` 21 |
| 10 | `design/gdd/clinic-machine.md:310` | **改**(就地加注) | 「同源」从散文落成可执行门 + 声明两表不合表 |
| 11 | `design/gdd/item-database.md:859` | **改**(就地加注) | 同上,并声明 21a 只作作者态归属方、字段语义归 23 |

### 2.1 四道域门(Binder)

| 门 | 判据 | 为什么需要它 |
|---|---|---|
| 锚点格 | `(0,0,0) ∈ occupancy_local` | 否则「锚点可走、结构在其上」(评审 B5 / `modular-building.md:90`) |
| id 唯一 | `module_id` 不重复 | 重复 = 放置判定 ⑤ 二义 |
| id 为正 | `module_id > 0` | `≤ 0` 与「未分配」不可分 |
| 类型闭集 | `module_type ∈ {Bed,Table,Cabinet,Plot,Decor,Shell}` | 拼错被读成 `None` ⇒ 所有槽位恒不匹配 = **静默失效** |
| 成本域 | `cost ≥ 1` | `modular-building.md:507` |
| 字段缺席 | `refundable` / `furn_type` **必须出现**(`furn_type` 可为 `null`) | 「没写」与「明写 null」不同 —— 前者多半是漏字段 |

### 2.2 跨表同源门(生产链路,`DataBakeMenu.BakeBuildModulesCore`)

```csharp
furn = ItemDatabaseFurnitureBake.BakeFromRepo(repoRoot);          // 闭集来自**本次**烘焙
BuildModulesBake.BakeOutput result = BuildModulesBake.BakeFromRepo(repoRoot);
...
if (!known.Contains(row.FurnType))
    errors.Add($"… furn_type=\"{row.FurnType}\" 不在 FURN_TYPES 闭集内 —— 两表漂移…");
if (errors.Count > 0) throw new BakeValidationException(errors);
```

- **方向单向**:家具目录可以有不进模块目录的类型(前者是 24 的词汇表,后者是 23 的件表,后者可为真子集)。
- **闭集取本次烘焙,不取盘上旧产物** —— 与 24 侧 `FurnMembershipEnforced` 同款理由(旧产物会把漂移藏起来)。

---

## 三、判据(已落 / 已证 双列)

| # | 判据 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| 1 | 真种子过全部域门 | ✅ | ✅ | `test_buildModules_realSeed_passesAllGates`(绿) |
| 2 | 锚点格缺失 ⇒ 拒 | ✅ | ✅ | **证伪 C**:停用门 ⇒ `test_buildModules_missingAnchorCell_isRejected` 红 |
| 3 | `module_id` 重复 ⇒ 拒 | ✅ | ✅ | `test_buildModules_duplicateModuleId_isRejected` |
| 4 | `module_id ≤ 0` ⇒ 拒 | ✅ | ✅ | `test_buildModules_nonPositiveModuleId_isRejected` |
| 5 | `module_type` 越界 / `None` ⇒ 拒 | ✅ | ✅ | `..._unknownModuleType_isRejected` / `..._noneModuleType_isRejected` |
| 6 | `cost = 0` ⇒ 拒 | ✅ | ✅ | `..._zeroCost_isRejected` |
| 7 | `refundable` 缺席 ⇒ 拒 | ✅ | ✅ | `..._missingRefundable_isRejected` |
| 8 | `furn_type` 缺席 ⇒ 拒 | ✅ | ✅ | `..._missingFurnType_isRejected` |
| 9 | 未知键 ⇒ 拒 | ✅ | ✅ | `..._unknownKey_isRejected` |
| 10 | 空表 ⇒ 拒 | ✅ | ✅ | `..._emptyTable_isRejected` |
| 11 | 占用格 2 项 / 行内重复格 ⇒ 拒 | ✅ | ✅ | `..._badCellArity_isRejected` / `..._duplicateCellInRow_isRejected` |
| 12 | 同源两次烘焙逐位一致 | ✅ | ✅ | `test_buildModules_bakeIsDeterministic`(且证伪 D 中**仍绿**,证明它与行序测是两条独立判据) |
| 13 | 行序 = `module_id` 升序(声明序不进字节流) | ✅ | ✅ | **证伪 D**:注释掉 `rows.Sort` ⇒ `..._rowOrder_isModuleIdSorted` 红 |
| 14 | 跨表同源门真的会拒 | ✅ | ✅ | **证伪 E**:`if (!known.Contains(...))` → `false &&` ⇒ `..._productionCore_rejectsUnknownFurnType` 红 |
| 15 | 盘上产物 = 现烘字节 | ✅ | ✅ | `test_buildModules_diskMatchesFreshBake` |
| 16 | 新产物纳入漂移门 | ✅ | ✅ | `cooked_drift_all_artifacts_test` 两条由**红转绿**(见 §五) |
| 17 | 写盘边界门认可本测试 | ✅ | ✅ | `fs_write_boundary_gate_test.test_realTestTree_zeroRed`(绿) |

---

## 四、证伪轮(3 例,全部 `exactRed`)

预注册文本:`/tmp/fz-expect-s17-5-{C,D,E}.txt`(跑之前写好,原文未改)。
脚本:`/tmp/fz-s17-5-cde.py`(改 → 跑 → 还原,同进程;还原后 `sha256` 逐字节校验)。
⚠️ **文件名里的 `s17-5` 是编号订正前的旧标签**(见文首),内容与本件对应,**不改名** ——
预注册件的价值在「跑之前就存在」,改名会破坏它与运行时刻的对应关系。
跑法:`unity test <unity/> --mode EditMode --filter BuildModules`。

| 例 | 变异点 | 变异手法 | 实测 |
|---|---|---|---|
| **C** | `BuildModulesBinder.cs:367` `if (ok && !seen.Contains((0, 0, 0)))` | → `if (false && …)` | `total=18 passed=17 failed=1`,红 = `test_buildModules_missingAnchorCell_isRejected` ✅ |
| **D** | `BuildModulesCookedWriter.cs:44` `rows.Sort(CompareModuleId);` | 注释掉 | `failed=1`,红 = `..._rowOrder_isModuleIdSorted`;`bakeIsDeterministic` **如期仍绿** ✅ |
| **E** | `DataBakeMenu.cs:625` `if (!known.Contains(row.FurnType))` | → `if (false && …)` | `failed=1`,红 = `..._productionCore_rejectsUnknownFurnType` ✅ |

**还原校验**:三处 `sha256` 与变异前**逐字节一致**;`git status` 无新增改动。

### 4.1 ⚠️ E 的预期被证伪过 —— 如实登记

预注册 `/tmp/fz-expect-s17-5-E.txt` 原文写的是:

> 预期:`test_buildModules_productionCore_enforcesFurnTypeSamesource` **仍绿** …
> ⇒ 说明「跨表门」今日**无负向夹具** … 判据:若全绿 ⇒ 登记为已知缺口(不是假绿,是覆盖缺口),不阻塞

**该预期在跑之前就被推翻了**:预注册时我意识到跨表门只在 `DataBakeMenu` 里、唯一打它的是**正向**测,
⇒ 删掉门也会全绿。**于是先补了负向夹具**(`test_buildModules_productionCore_rejectsUnknownFurnType`,
造最小仓根 + 注入 `"ghost"`),再跑 E ⇒ 得到 **RED**。

⚠️ **这是「预期被证伪后改夹具」,不是「事后改预期」**:预注册文本原文未动,差异记在此处。
**且须点明**:若当时按预注册的判据「全绿 ⇒ 登记为覆盖缺口、不阻塞」放过去,
本轮的跨表门就会以**零判别力**的形态留库 —— 与 `TD-clinic-01` 的假绿同型。

---

## 五、全量回归

**首跑(落盘后,未登记漂移门)`2 条真红** —— 如实登记:

| 红测 | 报文 |
|---|---|
| `test_sweep_declaredArtifactNames_allTriaged` | 源码声明 = **21**,钉定值 = **20** |
| `test_sweep_noGhostArtifact_onDiskButUnregistered` | 幽灵产物:`[build_modules.cooked.bytes]` |

**病因**:新产物落盘了,但没进 `cooked_drift_all_artifacts_test.BakedArtifacts`。
**这不是本门的误报** —— 它是本门**第三次**抓到同类漏登记(24 三表 / 21a 家具目录 / 本份)。
⇒ 已补:`BakedArtifacts` +1 条(`BuildModulesBake.BakeFromRepo`,**裸烘焙**而非生产核心 ——
跨表门不改编码,只决定「要不要落盘」)+ `pinnedDeclared` 20 → 21 + 类头 changelog 第三段。

**复跑(登记漂移门之后)**:

`4291 / 4255 / 0 / 36 @ 2026-10-21 12:38 CST · 批次 = TD-itemdb-01 兑现轮全量 · HEAD 009f301 + 工作树`

对基线 `4272 / 4236 / 0 / 36`(`2026-10-21 12:06 CST · C_max 裁定轮 · 009f301 + 工作树`)的 **`fullname` 集合差**:

- **新增 19 · 消失 0** ⇒ **零回归**
  - 18 条 = `BuildModulesBakeTest.*`(本件新测试件)
  - 1 条 = `cooked_drift_all_artifacts_test.drift[build_modules.cooked.bytes]`(漂移门逐份展开的用例)

⚠️ **为什么用集合差而不是纯计数**:`total 4272 → 4291` 只差 19,但若同时有「新增 20 消失 1」,
纯计数会显示 +19 而掩盖一条消失的测试。集合差是判据,计数不是。
⚠️ **`skipped 36` 未变** —— 全部是既有 `[Ignore]` 标注项,非本轮新增。

---

## 六、未及核查(禁借绿)

> ⚠️ **本节只列「没跑 / 没做」,不列「跑了但不理想」** —— 后者在 §四 / §五 已如实登记。

- **`IModuleCatalog` 的生产树实现仍为零** —— 本件只落数据与烘焙,**接线归 23 装配轮**(承「补烘 ≠ 登记 ≠ 接线」三分)。
- **`ModuleDefinition.Cost` 仍缺**、`DemolishChecker` 仍由调用方喂 `cost` —— 同上,归 23。
- **`world_buildslots.json` 仍不存在** —— `story-001:45` 的「两烘焙源」只落了一源;另一源归 54 关卡工具。
- **`AC-23-08` 的 `refundable ⇒ cost ≥ ⌈1/R⌉`** —— 本表 `cost ∈ [1,99]` 全是 `R = 1` 下自动满足,**没有一行触发该判据** ⇒ 该 AC 仍只对夹具字面值成立。
- **数值定稿** —— `cost` 是夹具值不是平衡值,归用户数值轮(`modular-building.md:507`)。
- **IL2CPP 三格 / 跨平台逐位**(ADR-012 矩阵)—— **NOT-RUN**,本机仅 Mono。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
- **24 侧的 `int → string` 适配** —— 裁定甲案说「今日不存在,不破」,本件**不实现**它。
