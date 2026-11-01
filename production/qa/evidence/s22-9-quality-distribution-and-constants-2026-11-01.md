# S22-9(第二半)· `quality_distribution` 数据通道 + `ForagingConstants` 运行期读方

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-9`(甲案「补接线」的数据前置) |
| **执行轮** | 2026-11-01 · commit `8a309e73` + 本批未提交改动 |
| **状态** | ③ + ① + ②(`CARRY_CAP` 通道)**已完成**;驱动（④）与 Allow 门（⑤）**未开工** |

---

## 1. ③ `quality_distribution`:从「全库不存在」到「数据可解」

### 病因(可复算)

```bash
grep -c "quality_distribution" assets/data/item_database_items.json   # 改前 = 0
```

`QualityDistributionShape.cs` 文件头早已登记:

> `item_database_items.json` 的四行 `gather_profile` **都不含** `quality_distribution`，
> 而 21a 的 binder 对**该对象的任何字段**都报「未登记字段」⇒ **数据通道全库不存在**。

⇒ 17 的 `GatherQualityRoll.OutQuality` 没有输入 ⇒ **一次采集永远算不出 `out_quality`**
⇒ 采集动作即使过闸也无法完成。

### 改动(四层同批)

| 层 | 文件 | 改动 |
|---|---|---|
| 契约 | `Sim.Contracts/ItemDatabase/GatherProfile.cs` | +`int[] QualityDistribution`(**权重**，`weights[q-1]` = 品级 q 的权重，D-21-17 计数非 `Fix`) |
| 烘焙 | `Editor.Tools.Bake/ItemDatabaseBinder.cs` | `quality_distribution` 由「任何字段都拒」改为**读 int 数组**，并**直接调 17 的 `QualityDistributionShape.Validate`**（单一真源，不写第二份判据） |
| 烘焙 | `Editor.Tools.Bake/CookedWriter.cs` | +`WriteIntArray`；gather 块尾部写分布（**schema 3**） |
| 运行期 | `Gameplay.Presentation/CookedCodec.cs` | +`ReadIntArray`；`ExpectedItemSchemaVersion` 2 → **3** |
| 数据 | `assets/data/item_database_items.json` | `schema_version` 2 → **3**；四行 `gather_profile` 各加 `quality_distribution` |

**权重取 `[1, 1, 1, 1, 1]`（均布）= 临时值，归数值轮** —— 本轮只证「通道存在且可解」，
不主张品级分布是定稿（已在 `_note` 与本节具名）。

### 一处必须记的坑（本轮自己踩的）

首版把 `maxQuality` 直接读 `pkg.Constants?.Constants.MaxQuality ?? 0`，常量表读不出来时
退化成 `0` ⇒ `QualityDistributionShape.Validate(weights, 0)` 抛出「长度 5 ≠ MAX_QUALITY 0」，
**把真正的根因（常量表坏了）埋在下面**：
`foraging_bake_wiring_test.test_w3_maxQuality_isReadFrom21a_notHardcoded` 因此转红。

⇒ 修法：`maxQuality < 1` 时**跳过**分布校验（真实错误已在 `BindConstants` 报过）。
**这不是放过一个错，而是不让第二个错掩盖第一个。**

---

## 2. ① `ForagingConstants` 运行期读方（产物在、读方 = 0）

```bash
grep -rn "new ForagingConstants(" unity/Assets --include=*.cs | grep -v /Tests/ | wc -l   # 改前 0
```

产物 `foraging_constants.cooked.bytes` 一直在，Binder 有 8 条校验 —— **但没有一个运行期读方**
⇒ `NodeRemaining.CanHarvest` / `GatherQualityRoll.HarvestQty` / `OutQuality` 在生产里
**没有任何调用者能拿到表**。这是采前闸「接通」所缺的第一块。

新增：
- `Gameplay.Presentation/ForagingConstantsCookedCodec.cs` —— 与写方逐字镜像的解码器；
  形状/值域判据**原样交给 `ForagingConstants` 构造期**（四条硬拒收），本类不重写。
- `DataCookedDisk.LoadForagingConstants(int maxQuality)` —— 新增装载入口。

**`maxQuality` 刻意不进产物**：它是 21a 的 `MAX_QUALITY`，真源归 21a；
运行期由调用方从 `RecipeDataSet.Settlement.MaxQuality` 取（**那条通道已存在**）。

---

## 3. ② `CARRY_CAP` 运行期通道（**新增 baked 产物**）

改前实测：`inventory_constants.json` **连 cooked 产物都不存在**，生产 `new CarryCapacity(` = 0
（唯一一处是编辑期门 `CarryCapExistenceGate`）⇒ 采前闸的「满载」一侧无从求值。

新增（一件完整的产物链）：

| 层 | 文件 |
|---|---|
| 写方 | `Editor.Tools.Bake/InventoryConstantsCookedWriter.cs`（载荷 = `i32 carry_cap`） |
| 烘焙入口 | `Editor.Tools.Bake/InventoryConstantsBake.cs`（`ConfigVersion` 走全仓唯一的 `ConfigVersionUtility`，不自算哈希） |
| 菜单 | `DataBakeMenu.BakeInventoryConstants()`（binder 的**唯一生产调用点链路**） |
| 读方 | `Gameplay.Presentation/InventoryConstantsCookedCodec.cs` |
| 装载 | `DataCookedDisk.LoadCarryCap()` |
| 地址/预载 | `AddressablesDataProvider.InventoryConstantsAddress` + `DataCorePreloader` 清单 |
| 漂移门 | `cooked_drift_all_artifacts_test` 的 `BakedArtifacts` + `pinnedDeclared` 37 → **38** |

**实烘**：`inventory_constants.cooked.bytes = 24 B`（20 头 + 4），**CARRY_CAP = 60**，
`ConfigVersion = 0xDD7F4143`。

> ⚠️ **产物已落盘 ≠ 已接通**：本轮只把「表 → 运行期值」这条路铺好；
> **谁在采集时构造 `CarryCapacity` 并调用 `CanCarry`** 归下一批的驱动（④）。

### 3.1 首轮全量：3 红，同一个根因（**如实登记**）

产物链铺完后第一次全量 EditMode（`s229-em6.xml`，`2026-11-01 10:59:17 CST`）：

```
total=6348 passed=6308 failed=3 skipped=37
```

| 红灯 | 报文 |
|---|---|
| `ProviderWiringGateTest.test_wiring_dataCoreGroup_containsEveryProviderAddress` | `address「inventory_constants.cooked.bytes」在 data-core 组中应恰有 1 条条目 … Expected: 1 But was: 0` |
| `ProviderWiringGateTest.test_wiring_preload_invokesEveryEnsureMethod` | `Ensure* 方法数 32 ≠ DataCoreAddresses 长度 33` |
| `nav_chunk_slicing_test.test_ac5_allChunksBaked_andRegisteredInDataCoreGroup` | `组条目 37 ≠ 盘上产物 38` |

**根因只有一个**：我把 `InventoryConstantsAddress` 加进了 `DataCoreAddresses`，
却**漏了另外两处同源登记** —— `Preload()` 里没有 `EnsureInventoryConstants()`，
`AddressableAssetsData/AssetGroups/data-core.asset` 里没有组条目。

**这三条红正是那组门存在的理由**。`DataCorePreloader` 的注释里已经记着**四次同型**
（`BuildModules` / `DiseaseRegistry` / `WorldBuildSlots` / `WorldGeometry`），并写死了
「`Ensure*` / `Preload()` / 地址表 / 常量**四处集合相等**，漏一处即红」。**我是第五次**
—— 注释读到了，登记还是漏了。⇒ 说明「靠注释提醒」不是机制，**门才是**。

**补法（三处同批）**：

| # | 文件 | 改动 |
|---|---|---|
| 1 | `AddressablesDataProvider.cs` | `private int? _carryCap` + `EnsureInventoryConstants()` + `public int CarryCap`（走 `FetchBytes`，E-13 硬失败） |
| 2 | `AddressablesDataProvider.cs` | `Preload()` 末尾补 `EnsureInventoryConstants();` |
| 3 | `AddressableAssetsData/AssetGroups/data-core.asset` | 补组条目（GUID `0ff01323ef74912cca72c775dd2f54f6` = 该 `.meta` 的 guid） |

**补完这三处之后还有第 4 条红** —— 它不是漏登记，而是**组条目变了 ⇒ 预建 catalog 陈旧**：

```
Failed test_catalog_containsEveryDataCoreAddress
  data-core 组声明了 38 条,构建出的 catalog 只有 37 条 —— 缺:inventory_constants.cooked.bytes
```

⇒ 跑「大医精诚/数据管线/构建 Addressables 内容(BuildPlayerContent)」（`m_BuildAddressablesWithPlayerBuild: 0`，
打包**不会**顺带重建，必须显式跑）。实跑输出：

```
[大医精诚] data-core 组就绪:38 个条目已入组并打标签。
[大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。(38 条,与盘上 38 个产物一致)
```

⚠️ **产物落 `unity/Library/`（gitignored，机器本地）** ⇒ 新检出 / CI 上这条门会走
`Assert.Inconclusive`（**不等于通过**），本地须先跑该菜单。

> **两条读路不构成第二真源**：`DataCookedDisk.LoadCarryCap()` 是**盘上直读**（编辑期工具 /
> 夹具），`EnsureInventoryConstants()` 是**Addressables 装载**（玩家构建里没有盘可读）。
> 两者读**同一份产物**、过**同一套判据**（`InventoryConstantsCookedCodec.ReadCarryCap`）。

**顺带抓到一处文档漂移（与本批无因果，如实登记）**：`DataCorePreloader` 那段
「逐次累加…共 **26** 条」的注释**在 26 之后就脱节了** —— S22-9 之前实际已是 **32** 条。
已就地标注「口径以代码为准、不再逐次累加」，并把长度钉点指回那两条门。

---

## 4. 仍未做（下一批）

| # | 缺什么 | 现状 |
|---|---|---|
| ④ | **驱动**：3 的输入沿 → 4 的 `InteractionSelector` → 真闸门 → `Resolve` | 未写 |
| ⑤ | **「生产传 `HarvestGate.Allow` 即红」的门** | 未写 |
| ⑥ | 驱动侧测试 / 证伪 / 全量回归 / 收口账 | 未做 |

> ⚠️ **本件不主张 Exit 已达成**：采前闸要真跑，还差 ④（驱动）与 ⑤（Allow 门）；
> `sprint-22` 的收口判定维持 **NOT PASS**(Exit 未达)不变。

---

## 5. 新增夹具与证伪轮（S12-4 门）

### 5.1 为什么必须补夹具

两个新 codec（`InventoryConstantsCookedCodec` / `ForagingConstantsCookedCodec`）**改前零直接覆盖** ——
`grep` 全仓无任何测试引用它们。它们是「产物 → 领域类型」的唯一读路，**采前闸拿不到容量就是死路**，
所以这不是可选项。

新增 `unity/Assets/Tests/EditMode/DataPipeline/inventory_and_foraging_constants_codec_test.cs`（6 条）：

| # | 测 | 命中形态 |
|---|---|---|
| 1 | `test_carryCap_realArtifact_isSixty` | 哨兵（数值变了要有人看见） |
| 2 | `test_carryCap_zero_isRejectedAtLoad` | 定义域收窄 |
| 3 | `test_carryCap_trailingBytes_isRejected` | 定义域收窄（布局镜像） |
| 4 | `test_foragingConstants_realArtifact_constructsWithInjectedMaxQuality` | 正例 + 注入缝 |
| 5 | `test_foragingConstants_capTableLengthDrift_isRejected` | 定义域收窄 |
| 6 | `test_foragingConstants_maxQualityBelowOne_isRejectedByConstructor` | 定义域收窄 |

**两条设计口径**（不是风格问题，是会决定夹具真假的问题）：

- **正例一律读盘上真产物**。codec 的全部价值是「与写方逐字节镜像」；
  把载荷内联成字面量 ⇒ 写方改了布局夹具仍绿，恰好放过它唯一要防的缺陷。
- **尾随字节测试必须同步改头部 `payloadLength`**。只追加字节而不改头部，
  会先在「载荷长度不符」那一条就抛，`AssertFullyConsumed` **永远走不到** ——
  那样该测绿在**另一条**判据上，对目标守卫零判别力（假绿形态「靶测绕过被变异函数」）。

### 5.2 证伪轮（5 条变异，逐条实测）

还原纪律：备份 + `cp` 回 + **SHA-256 逐字节比对**；窗口内不读被变异文件。

| 变异 | 打法 | 期望 | 实测 |
|---|---|---|---|
| **M1** | `InventoryConstantsCookedCodec` 的 `carryCap <= 0` 分支 → `if (false && …)` | 测 2 红 | ✅ `test_carryCap_zero_isRejectedAtLoad` **红**（5/6） |
| **M2** | 删 `CookedReadUtil.AssertFullyConsumed(...)` 调用 | 测 3 红 | ✅ `test_carryCap_trailingBytes_isRejected` **红**（5/6） |
| **M3** | `ForagingConstantsCookedCodec` 的 `capCount != CapTableLength` → `if (false && …)` | 测 5 红 | ✅ `test_foragingConstants_capTableLengthDrift_isRejected` **红**（5/6） |
| **M4** | 构造期 `maxQuality` 实参 → 常量 `3` | 测 4 红 | ✅ 测 4 + 测 6 **双红**（4/6） |
| **M5** | `ReadCarryCap` 在 `OpenChecked` 后直接 `return 60`（跳过读取） | 测 2/3 红 | ✅ 测 2 + 测 3 **双红**（4/6）；**测 1 仍绿** |

**还原核对**（每条变异后即做）：

```
InventoryConstantsCookedCodec.cs: 成功   ← sha256sum -c
ForagingConstantsCookedCodec.cs : 成功
FALSIFY 残留(全仓): 0
```

### 5.3 M5 的结果要如实说：哨兵**不**证明字节被读

M5 下 `test_carryCap_realArtifact_isSixty` **仍然是绿的** —— 因为把 `60` 写死同样满足
「读出 60」。⇒ **测 1 只证「值变了会被发现」，不证「值是从产物读的」**。
它俩不是同一件事，本轮**不**用测 1 冒充后者；读路径由测 2 / 测 3 承担（M5 令它们双红）。
登记在此以免日后有人拿测 1 当「已证读通」。

---

## 6. 回归（三元组）

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **11:12:28 CST** |
| **批次** | | S22-9 第二半（③①② + 三处同源登记 + catalog 重建 + 6 新测） |
| **commit** | | `8a309e73` + 工作树（提交前） |

| 套件 | 结果 | 前次 | 等式 |
|---|---|---|---|
| EditMode | `total=6354 passed=6317 failed=0 skipped=37` | `6348 / 6311 / 0 / 37`（11:08:12） | `6354 − 6348 = 6` = 新增测数 ✅ |
| PlayMode | `total=273 passed=269 failed=0 skipped=2`（+2 Inconclusive） | `273 / 269 / 0 / 2`（S22 收口） | `±0`（本批未加 PlayMode 测） |

> ⚠️ PlayMode 的 2 条 Inconclusive 是 `U1Assump6GamepadFocusSpikeTest` 的**存量**状态，
> 与本批无关；禁把它读成「本批新增跳过」。

---

## 7. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import json;d=json.load(open('assets/data/item_database_items.json'));print(d['schema_version']);print([ (i['ordinal'], i['gather_profile']['quality_distribution']) for i in d['items'] if i['gather_profile']])"
# 3
# [(0, [1,1,1,1,1]), (2, ...), (3, ...), (4, ...)]
grep -c "ExpectedItemSchemaVersion = 3u" unity/Assets/Gameplay.Presentation/CookedCodec.cs   # 1
grep -rn "new ForagingConstants(" unity/Assets --include=*.cs | grep -v /Tests/ | wc -l      # 仍 0（读方在 DataCookedDisk）
```
