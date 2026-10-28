# level-tool story-002 实现轮 · 证据原件 —— `world_geometry` / `world_terrain`

**对象**:`production/epics/level-tool/story-002-world-geometry-and-terrain.md`(AC-0 … AC-6)
**日期**:2026-10-29
**承接**:S20-20 挂标 **#5「世界几何仍是合成」** 的**地形半边**;AC-0 的 spike 另有原件
(`production/qa/evidence/spike-terrain-api-6.3-2026-10-29.md`)
**结论**:AC-0 … AC-6 全部有载体;**13 条变异逐条与预判一致(15 红 / 15 红)**;
本轮另抓到并修掉五处我自己的错误(§八),并对故事卡的「乙案」做了一处**收窄**(§六①,如实登记供裁定)

---

## §一 落点

| # | 件 | 落点 | 性质 |
|---|---|---|---|
| 1 | **量化约定**(`slopeLimit` / `stepOffset` 的整数单位与量程) | `unity/Assets/Sim.Contracts/LatticeParamQuantization.cs` | 新建 |
| 2 | **地形表声明式源** | `unity/Assets/Editor.Tools.Level/TerrainSource.cs` | 新建 |
| 3 | **关卡源扩展**(每格可走性 / 地形 / `LATTICE_SIZE`) | `unity/Assets/Editor.Tools.Level/LevelSource.cs` | 修改 |
| 4 | **P0 医馆:外墙环不可走 + 地形表 + 两个控制器参数量化值** | `unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs` | 修改 |
| 5 | **脚手架关卡同步**(第四格 `(7,1,7)` → `(6,1,6)`) | `unity/Assets/Editor.Tools.Level/ScaffoldSecondLevel.cs` | 修改 |
| 6 | **一致性检查新增一条**:槽位落在**不可走**的格上 | `unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` | 修改(已实现条数 5 → 6;`UnimplementedChecks` **仍 7 条**) |
| 7 | **导出**:`ExportGeometry` / `ExportTerrain` + 三个 per-product schema 号 + 「导出全类别」 | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 修改 |
| 8 | **分派表登记**:Geometry / Terrain 从「未实现」移到「已实现」 | `unity/Assets/Editor.Tools.Level/WorldDataSetKind.cs` | 修改(未实现 6 → 4) |
| 9 | **烘焙三件套 ×2** | `Editor.Tools.Bake/World{Geometry,Terrain}{Bake,Binder,CookedWriter}.cs` | 新建 ×6 |
| 10 | **菜单**(两条烘焙入口 + 两个产物名常量) | `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs` | 修改 |
| 11 | **运行期 codec ×2** | `Gameplay.Presentation/World{Geometry,Terrain}CookedCodec.cs` | 新建 |
| 12 | **装载面接线**(2 address / 2 `Ensure*` / Load 分派 / `Preload` / 磁盘兜底) | `AddressablesDataProvider.cs` · `DataCorePreloader.cs` · `DataCookedDisk.cs` | 修改 |
| 13 | **世界工厂改为纯装载器**(合成支删除;`KSpeedIdx` 首个消费者) | `unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs` | 重写 |
| 14 | `IsSyntheticWorld` 置 `false` | `unity/Assets/Gameplay.Boot/BootRoot.cs` | 修改 |
| 15 | **产物**(作者态 + 烘焙) | `assets/data/world_{geometry,terrain}.json` · `unity/Assets/DataCooked/world_{geometry,terrain}.cooked.bytes` | 新建 ×4 |
| 16 | **data-core 组条目** | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset` | 修改(**26 → 28**) |
| 17 | **夹具** | `unity/Assets/Tests/EditMode/LevelTool/world_geometry_load_test.cs` | 新建,14 条 `[Test]`,零写盘 |
| 18 | **既有夹具的两处补正**(见 §八②③) | `level_source_generalization_test.cs` · `world_slots_provider_wiring_test.cs` | 修改 |

**未碰**(故事卡 Out of Scope):生态区多边形 / C4 / C5(S3)· POI / 资源点(S4,本文件的
`PoiGrid` / `ResourceGrid` 仍全 `-1`)· 导航格 / C6 / C3(S5)· **C1 / C2 的检查实现**(S6)·
视觉层 Terrain 资产本体(美术 / 关卡设计)· `K_speed` / `LATTICE_SIZE` 的**具体数值**(用户数值轮)。

---

## §二 AC 逐条 → 载体 → 反证

夹具文件 = `unity/Assets/Tests/EditMode/LevelTool/world_geometry_load_test.cs`(14 条)。

| AC | 判据(story 原文要点) | 载体 | 反证(改坏什么 ⇒ 哪条红) | 判定 |
|---|---|---|---|---|
| **AC-0** | Terrain API 三项实测,**不是文档引用** | `TerrainApiSpike.cs` + `spike-terrain-api-6.3-2026-10-29.md`(3 次 batch 实跑) | 不适用(勘察类,不可自动化)。**本轮推翻了实现前的两条假设** —— 见该件 §五 | ✅ |
| **AC-1** | `world_geometry.json` 形状 = 尺寸 + 每格可走性 + `LATTICE_SIZE`;**零浮点** | `LevelExporter.ExportGeometry`;`test_ac1_geometryExport_hasZeroFloatLiterals` · `…_sizeWalkableLatticeComeFromSource` · `…_undeclaredWalkable_throws` | **M1**(`lattice_size_mm` 写成 `1000.0`)⇒ 零浮点红;**M2**(`world_size` 写死 `16,1,16`)⇒ 尺寸红;**M3**(未声明可走性时合成全 1)⇒ 抛红 | ✅ |
| **AC-2** | `K_speed` 等字段在 JSON 里是**字符串**(`"3/4"`) | `LevelExporter.ExportTerrain`;`test_ac2_terrainExport_fixFieldsAreJsonStrings` · `…_nonIntegerFix_roundTripsThroughFixParse` · `…_undeclaredTerrain_throws` | **M4**(`k_accel` 去引号)⇒ 2 红;**M5**(非整值只写整数商)⇒ 往返红 | ✅ |
| **AC-3** | 两份产物烘成 `.cooked` 进 `data-core`;**组条目数 +2** | `World{Geometry,Terrain}Bake/Binder/CookedWriter`;`test_ac3_bakeTwice_isBitIdentical` · `…_bothAddressesAreInDataCorePreloadList_andProductsExist`;**真 batch 实跑见 §五** | **M6**(ConfigVersion 掺 `DateTime.Ticks`)⇒ 确定性红;**M7**(从地址表删一条)⇒ 登记红 | ✅ |
| **AC-4** | 运行期由装载路径替换;`IsSyntheticWorld` 为 false;尺寸 / 可走性**来自产物** | `MinimalWorldFactory.Create`;`test_ac4_geometryComesFromArtifact_notFromConstants` · `…_realProduct_walkabilityIsNotAllTrue` | **M8**(工厂恒写 `walkable = true`)⇒ 1 红;**M9**(codec 强制 `walkable = 1`)⇒ 2 红 | ✅ |
| **AC-5** | `TERRAIN_TABLE` 空表 ⇒ 装载**抛**,**不是**静默降级 | `WorldTerrainBinder`(烘焙门)+ `WorldTerrainCookedCodec.Read`(读侧门)+ `MinimalWorldFactory` 装载序;`test_ac5_emptyTerrainTable_throwsBeforeAnySlotLoad` · `…_codecRejectsZeroRowTerrainPayload` | **M10**(工厂开头预取槽位)⇒ 「抛之前下游零调用」红;**M11**(读侧 `rowCount < 1` 放宽为 `< 0`)⇒ codec 门红 | ✅ |
| **AC-6** | `TerrainCell.KSpeedIdx` 的「全仓零消费者」解除,**至少一处读它做量程校验** | `MinimalWorldFactory.ValidateTerrainSpeedIndices`(public,逐格读 `KSpeedIdx`)+ `test_ac6_loadedTerrainCells_carryEncodedSpeedIndex` · `…_outOfRangeSpeedIndex_isRejected` | **M12**(工厂恒写 `KSpeedIdx = 0`)⇒ 编码透传红;**M13**(停用界判据)⇒ 越界红 | ✅ |

### AC-6 的**消费者**到底是谁(**如实划界,不借绿**)

`grep -c KSpeedIdx` 的非定义侧命中:`MinimalWorldFactory.ValidateTerrainSpeedIndices`(读)+
`MinimalWorldFactory.Create`(写)+ `WorldTerrainCookedCodec`(读 `cell_speed_index`)。
判据 `grep -rn 'KSpeedIdx' unity/Assets --include=*.cs | grep -c .` 的输出贴在 §七。

⚠️ **本 AC 只要求「有消费者」,不要求「消费得对」**(故事卡逐字)。今天的消费者是对**装载产物**
做量程校验;真正的速度求值(F-1-3 / F-6-1 展开)归**系统 1 的实现轮**。
**不得**把本 AC 读成「K_speed 已经在影响移动」—— 它还没有。

---

## §三 证伪轮 —— 13 条变异,逐条与预判一致

**纪律**:预判先落盘(`falsify-plan-story002.md`,写在任何变异之前);每条「改 → 跑 → 还原」
在同一脚本内完成;还原判据 = **SHA-256 逐字节**(未用 `git checkout` / `git stash`);
靶场 = `DaYiJingCheng.Tests.LevelTool.world_geometry_load_test`(基线 14/14 全绿)。

| ID | 属性(一属性一条) | 变异 | 预判红 | **实得红** | 命中的用例 | 一致 |
|---|---|---|---|---|---|---|
| M1 | 几何导出**零浮点** | `lattice_size_mm` 写成 `1000.0` | 1 | 1 | `…_hasZeroFloatLiterals` | ✅ |
| M2 | 尺寸 / `LATTICE_SIZE` 来自 `LevelSource` | `world_size` 写死 `16,1,16` | 1 | 1 | `…_sizeWalkableLatticeComeFromSource` | ✅ |
| M3 | 未声明可走性 ⇒ 硬失败 | 停用守卫 + 合成全 1 | 1 | 1 | `…_undeclaredWalkable_throws` | ✅ |
| M4 | `Fix` 写**字符串** | `k_accel` 去引号 | 2 | 2 | `…_fixFieldsAreJsonStrings` + `…_nonIntegerFix_roundTrips…` | ✅ |
| M5 | `Fix` 字面量**往返逐位无损** | 非整值只写整数商 | 1 | 1 | `…_nonIntegerFix_roundTripsThroughFixParse` | ✅ |
| M6 | 同源两次烘焙**逐位一致** | ConfigVersion 掺 `DateTime.UtcNow.Ticks` | 1 | 1 | `…_bakeTwice_isBitIdentical` | ✅ |
| M7 | 两条 address **都在**预载表 | 删 `WorldGeometryAddress` 一行 | 1 | 1 | `…_bothAddressesAreInDataCorePreloadList…` | ✅ |
| M8 | 装载侧可走性**来自产物** | 工厂恒 `walkable = true` | 1 | 1 | `…_geometryComesFromArtifact_notFromConstants` | ✅ |
| M9 | 盘上产物可走性**非全 true** | codec 读时强制 `walkable = 1` | 2 | 2 | 同 M8 那条 + `…_realProduct_walkabilityIsNotAllTrue` | ✅ |
| M10 | 空表失败**之前下游零调用** | 工厂开头预取槽位产物 | 1 | 1 | `…_emptyTerrainTable_throwsBeforeAnySlotLoad` | ✅ |
| M11 | 读侧 codec **独立**拒空表 | `rowCount < 1` 放宽为 `< 0` | 1 | 1 | `…_codecRejectsZeroRowTerrainPayload` | ✅ |
| M12 | 每格 `KSpeedIdx` 等于**产物编码值** | 工厂恒写 `KSpeedIdx = 0` | 1 | 1 | `…_loadedTerrainCells_carryEncodedSpeedIndex` | ✅ |
| M13 | 越界 `KSpeedIdx` **被量程校验拒掉** | 停用 `ValidateTerrainSpeedIndices` 的界判据 | 1 | 1 | `…_outOfRangeSpeedIndex_isRejected` | ✅ |

**合计:预判 15 红 / 实得 15 红。13 条全部命中预判用例,零多余红、零漏红。**
每次变异后 `RESTORE-OK`,六个被变异文件的 SHA-256 全部回到原值(脚本内逐条打印)。

### 命中形态(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证 |
|---|---|---|
| **定义域收窄** | 可走性只接受 0/1;`Fix` 只接受字符串 | **M3**(未声明 ⇒ 必须抛)· **M4**(写数字 ⇒ 必须拒) |
| **共存 / 互斥** | 两份产物的 `world_size` 必须相等 | 由 `MinimalWorldFactory` 的尺寸对账 + AC-4 的 M8/M9 覆盖 |
| **阈值边界** | `k_speed == 1` 恰好一行 / `KSpeedIdx ∈ [0, rowCount)` | **M13** ⇒ 越界红 |
| **一次性 / 幂等** | 同源两次烘焙逐位一致 | **M6** ⇒ 掺时间戳红 |
| 零行为变化(确定性) | `world_buildslots` 产物未被本 story 改动 | §五 的 `1c946bff…` 前后同哈希 |

**Gate 结果:PASS**。

---

## §四 回归

### 4.1 全量 EditMode(三元组:**截至时刻** · **批次** · **commit**)

```
total=5754 passed=5719 failed=0 inconclusive=0 skipped=35   exit=0
```

- **截至时刻**:2026-10-29 **01:36 CST**
- **批次**:level-tool story-002 **提交前终跑**
- **commit**:`b7a5956d` + 工作树(本 story 改动未提交)
- **树冻结凭据**:跑前 / 跑后 `git status --porcelain | wc -l` **均为 50** ⇒ 跑测本身没有改动任何文件

**基线**:`5738/5703/0/0/35` @ 2026-10-29 00:45 CST · level-tool story-001 提交前终跑 ·
`37265728` + 工作树 ⇒ `inconclusive` / `skipped` 逐位相同,**零回归**。

**等式**:`5754 − 5738 = 16` = **14**(新增夹具 `world_geometry_load_test` 的 `[Test]` 计数)
+ **2**(`cooked_drift_all_artifacts_test` 用 `TestCaseSource(nameof(BakedArtifactNames))`
**按产物逐条展开**,产物 26 → 28 ⇒ 用例 30 → 32)。
复算命令(两条都要跑,只跑第一条会看到 `+16 ≠ +14` 而误判「凭空多了两条」):

```bash
python3 -c "
import xml.etree.ElementTree as ET
t=ET.parse('unity/Logs/final2-editmode.xml').getroot()
n=[c.get('fullname') for c in t.iter('test-case')]
print(len(n), len([x for x in n if 'world_geometry_load_test' in x]),
         len([x for x in n if 'cooked_drift_all_artifacts_test' in x]))"
# 实测输出:5754 14 32
```

### 4.2 ⚠️ **首跑抓到 3 条真回归**(原文不删 —— 这是本轮最值得留的一段)

第一次全量回归的实测是 `5752/5714/**3**/0/35`。三条全是我自己引入的,**没有一条是夹具假绿**:

| # | 失败用例 | 真因 | 处置 |
|---|---|---|---|
| ① | `cooked_drift_all_artifacts_test.test_sweep_declaredArtifactNames_allTriaged` | 新增两份 `*.cooked.bytes` 却**没登记** `BakedArtifacts` ⇒ 源码声明 28 ≠ 钉定 26 | 补两条 `ArtifactBake`(裸 `BakeFromRepo` 委托)+ `pinnedDeclared` 26 → **28** |
| ② | `…test_sweep_noGhostArtifact_onDiskButUnregistered` | 同上:盘上两份产物 = 「幽灵产物」 | 同一处置。⚠️ **本门第六次抓到同类漏登记** |
| ③ | `DataPipelineBakeTest.test_ac48_runtimeSources_guardReportsNoViolation` | `MinimalWorldFactory` 的一条 `InvalidDataException` **报文**里写了作者态目录路径 ⇒ `RuntimeSourceGuard` 的运行期禁物字面量命中 | 报文案改成「作者态数据目录」;并在类头写明「注释里写路径安全、**报文里写会红**」 |

⚠️ ①② 同时把 **ADR-022 §②「无第二作者」**钉到了几何 / 地形上:手改
`world_geometry.json` / `world_terrain.json` 而不重烘 ⇒ 漂移门红。这是本 story 的**顺带收益**,
登记在此以免下一轮以为是既有保护。

### 4.3 全量 PlayMode

```
total=238 passed=234 failed=0 inconclusive=2 skipped=2   exit=0
```

- **截至时刻**:2026-10-29 **01:37 CST**
- **批次**:level-tool story-002 **提交前终跑**
- **commit**:`b7a5956d` + 工作树
- **基线**:`238/234/0/2/2`(story-001 收口轮同值)⇒ **逐位相同,零回归**
- 等式:`238 − 238 = 0`,即本 story **未新增 PlayMode 用例**(世界装载面全部在 EditMode 覆盖)。

> ⚠️ 唯一与「树冻结」口径有出入的一点**如实登记**:本轮 EditMode 与 PlayMode 之间我编辑了
> **本证据件**(`production/qa/evidence/…story-002…md`)。它**不在** `unity/Assets` 编译树内、
> 不被任何夹具读取 ⇒ 对两次跑测无影响。EditMode 那一次的跑前 / 跑后工作树计数均 50 已相符。

### 4.4 既有产物未被改动

| 项 | SHA-256 | 说明 |
|---|---|---|
| `assets/data/world_buildslots.json` | `1c946bff60177ff294f4e3bcf4a235e67b4e5067ba07db105047d57762074c6d` | **与 story-001 的金标准逐字节相同** ⇒ 本 story 未动槽位数据 |
| `unity/Assets/DataCooked/world_buildslots.cooked.bytes` | `60075ce41fef9640e0abb8d513398bc8f56af9bef86d954786865c5f2c87821b` | 同上 |

（复算:`python3 -c "import hashlib;print(hashlib.sha256(open('<path>','rb').read()).hexdigest())"`）

---

## §五 真 batch 实跑证据

### 5a 导出(`ExportP0ClinicToRepo`)

```
Unity -batchmode -nographics -projectPath unity -quit \
  -executeMethod DaYiJingCheng.EditorTools.Level.LevelExporter.ExportP0ClinicToRepo
```

| 项 | 值 |
|---|---|
| EXIT | **0** |
| 日志行 | `已导出 world_buildslots.json` / `world_geometry.json` / `world_terrain.json` |
| `world_buildslots.json` | **未变**(与 story-001 的金标准 `1c946bff…` 同哈希 ⇒ §五 的「零行为变化」仍成立) |

### 5b 烘焙(`BakeWorldGeometry` / `BakeWorldTerrain`,两条 `-executeMethod` 分两次跑)

| 产物 | 字节数 | 复算 | ConfigVersion |
|---|---|---|---|
| `world_geometry.cooked.bytes` | **1064 B** | 20 头 + 5×4 + 256×4 = 1064 ✅ | `0x952F7F0D` |
| `world_terrain.cooked.bytes` | **1096 B** | 20 头 + 4 + 4×24 + 12 + 4 + 256×4 + 8 = 1096 ✅ | `0x134FB669` |

### 5c `data-core` 组与 catalog(`BuildAddressablesContent`,自足 = 自己先 Ensure)

```
改动前条目数: 26      # git show HEAD:.../data-core.asset | grep -c '^  - m_GUID:'
改动后条目数: 28      # grep -c '^  - m_GUID:' unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset
盘上产物数:   28      # ls unity/Assets/DataCooked/*.cooked.bytes | wc -l
```

菜单自检输出逐字:`[大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。(28 条,与盘上 28 个产物一致)`
⇒ **AC-3 的「组条目数增加 2」在真实资产上兑现**(26 → 28,且增量恰为
`world_geometry.cooked.bytes` / `world_terrain.cooked.bytes` 两条,见 `git diff` 的 +12 行)。

---

## §六 本轮的三处裁定 —— **2026-10-29 用户逐条裁定:三条全部「维持现状」**

| # | 事项 | 本轮实现的形态 | **用户裁定(2026-10-29)** |
|---|---|---|---|
| ① | 合成支存废 | **整体删除**(甲案:纯装载器) | ✅ **维持甲案** —— 产物缺失 / 损坏一律启动期硬失败,与 AC-5 同一语义 |
| ② | 量化约定归属 | `Sim.Contracts`,**仅整数常量 + 整数形式 C2 判据** | ✅ **维持现状** —— sim 侧不开浮点出口;`ToDegrees(int)=>float` 不引入 |
| ③ | P0 几何 / 地形数值 | 全为占位(见下表) | ✅ **全部留占位** —— 归**用户数值轮**;外墙环的玩法面改动一并留待该轮 |

⇒ 本节以下三段保留**裁定前**的原始论证,**一字未改**(它们是「为什么这么实现」的记录);
三者的**处置**以上表为准。**本 story 无遗留待裁项。**

---

### 裁定前论证(原文保留)

### ① 故事卡的「乙案」被**收窄**为「甲案」—— 合成支整体删除

### ① 故事卡的「乙案」被**收窄**为「甲案」—— 合成支整体删除

故事卡 §Implementation Notes 取**乙**(「有产物则装载,无产物则合成 + LogWarning」),
并要求写明「甲是终态」。**本轮直接落了甲的形态**(合成支删除)。三条理由:

1. **与 AC-5 在同一入口上产生两种语义** —— AC-5 要求「空表 ⇒ 装载**抛**」,
   而「无产物则合成」要求「产物不在 ⇒ 静默造一个全可走世界」。两条判据在同一段 `try` 里只能二选一。
2. **「产物缺失」与「产物损坏」判别不可靠** —— `AddressablesDataProvider.FetchBytes` /
   `Ensure*` 把两类失败**都**包成 `InvalidOperationException`(本轮实测:注入字节源抛
   `InvalidDataException`,到调用方手上变成 `InvalidOperationException`,`InnerException` 才是真因)。
   按类型分不开 ⇒ 合成支会把「字节损坏」当成「没有数据」吞掉。
3. **合成支今日零可达** —— 两份产物随本 story 提交入库,`data-core` 组已登记。

⚠️ **若将来确需合成支**(例如「开发者未烘焙」的友好降级),须**另裁**,并同时解决理由 2。
~~⇒ 本条**请用户裁定**:接受「直接落甲」,还是要求补回乙的降级支。~~
**✅ 2026-10-29 用户裁定:维持甲案**(取消该待裁项;原文不删,留作闭环记录)。

### ② 量化约定住 `Sim.Contracts`,但**只定整数侧的量子**(不提供 float 反量化函数)

故事卡把「归 `Sim.Contracts` 还是工具侧」留给实现轮。本轮的取法与理由写在
`LatticeParamQuantization.cs` 文件头。要点:两侧(工具 + 读侧 codec)都要看见 ⇒ 只能住
`Sim.Contracts`;但 C2 的比对发生在 **S6 的编辑期检查**里,在那里 float 合法,
而 **sim 侧永远只持整数** ⇒ 在 `Sim.Contracts` 放 `ToDegrees(int) => float` 等于给整数域开浮点出口,
且它的唯一消费者(S6)**今天不存在**。
⇒ 单位关系用**整数常量**表达(`1 度 = 100 单位` / `1 米 = 1000 单位`),
外加一个**整数形式**的 C2 判据 `IsWithinOneQuantum(engineUnits, bakedUnits)`(S6 直接引用,
不另抄偏差公式)。该函数今天零生产消费者,由本 story 的夹具间接覆盖(§三 M13 所在文件不涉及它,
**如实登记:它没有专打变异**)。

**✅ 2026-10-29 用户裁定:维持现状** —— 量化约定留 `Sim.Contracts`、**仅整数侧**,
`ToDegrees(int)=>float` 这类浮点出口**不引入**(理由同上:唯一消费者 S6 今天不存在,
而 sim 侧永持整数)。

### ③ P0 医馆的几何 / 地形数值全是**占位**,归用户数值轮

| 项 | 占位值 | 依据 |
|---|---|---|
| 外墙环 | 1 格厚,不可走(256 格中 60 格 blocked / 196 walkable) | 「医馆是一间屋子」的最小表达;同时让 AC-4 的「可走性来自产物」**可判别**(全 true 与合成世界同值 ⇒ 同值变异不可判别) |
| `TERRAIN_TABLE` | 单行 `terrain_id=0` / `wood_floor` / `K_speed=1` / `K_accel=K_decel=1` | 单行表 ⇒ `K_TERRAIN_MAX` 恒 1。**这是「数值未裁」的如实表达**,不是缺陷 |
| `LATTICE_SIZE` | 1000 mm | 只满足 `> 0`(F-6-1 守卫);与 `SPEED_MODE_MAX` / `MAX_DT` / `SAFETY_MARGIN` 同属数值轮 |
| `slopeLimit` | 4500(= 45.00°) | **AC-0 spike 实测的 `CharacterController.slopeLimit` 引擎默认值** ⇒ S6 的 C2 在今天偏差恰为 0 |
| `stepOffset` | 300(= 0.300 m) | 同上,引擎默认值 |

**✅ 2026-10-29 用户裁定:上表五项全部留占位,归用户数值轮;** **外墙环的玩法面改动一并留待该轮。**

⚠️ **不可走的墙环是玩法面改动**(玩家走不到边界格)。它**不是**设计产物,
关卡设计轮替换 `P0ClinicLevel` 时整个重写。

---

## §七 本件证明不了的事(禁借绿)

1. **C1 / C2 一条都没实现** —— 本 story 只导出整数侧;两侧一致的**检查**归 S6。
   `LevelConsistencyCheck.UnimplementedChecks` 仍是 **7 条**(夹具
   `test_ac4_unimplementedChecks_stillSeven_andNoLongerSaysThisBatch` 钉着)。
2. **`K_TERRAIN_MAX` 的数值是否正确** —— 单行表让它恒 = 1。本 story 只让它**有消费者**。
3. **`K_speed` 是否真的影响移动** —— 没有。`KSpeedIdx` 的消费者今天只是**量程校验**;
   F-1-3 / F-6-1 的求值归系统 1。
4. **视觉层 Terrain 资产是否存在** —— 本 story 的产物是**纯整数逻辑层**;
   AC-0 §七 已如实记「全仓没有一份真美术地形」。
5. **`WorldLatticeParams` 能否拼出来** —— 不能。6 个数值里只有 `LatticeSizeMm` 与 K_speed 表
   来自产物,其余四个全归数值轮 ⇒ 装配期**不构造**它(不猜默认值,否则 F-6-1 的不等式以「猜的数」成立)。
6. **多 chunk 的世界** —— 本 story 的几何是**单块**;chunk 切片归 S5(且只切导航格)。
7. **`Addressables` 真实 catalog 的装载** —— EditMode 夹具走注入字节源,
   `→ codec` 链与生产同一条,差别只在字节从哪来。真 catalog 由「构建 Addressables 内容」菜单覆盖
   (§五 5c 的 28 == 28 自检)。

### AC-6 的复算命令及其实跑输出

```bash
grep -rn 'KSpeedIdx' unity/Assets --include=*.cs | wc -l                                # 实测 22
grep -rn 'KSpeedIdx' unity/Assets --include=*.cs | grep -v 'WorldLattice.cs' | wc -l   # 实测 19
```

**非定义侧 19 行的逐类归属**(非定义 = 排除 `WorldLattice.cs` 的 2 行:字段声明 + 构造赋值):

| 类 | 行数 | 位置 |
|---|---|---|
| **真正的读取**(唯一) | **1** | `MinimalWorldFactory.cs:206` —— `int kSpeedIdx = terrain[x, y, z].KSpeedIdx;` |
| 报文 / 注释 / `<see cref>` | 10 | `MinimalWorldFactory.cs` ×6(:118/180/181/182/193/209)· `WorldTerrainCookedCodec.cs:74` · `WorldTerrainBinder.cs:321` · `WorldTerrainCookedWriter.cs:20` · `TerrainSource.cs:34` |
| 工具侧注释 | 2 | `TerrainSource.cs:63-64` |
| 夹具断言 | 6 | `world_geometry_load_test.cs`(:314/316/333/335/338/355) |

1 + 10 + 2 + 6 = 19 ✅

⚠️ **`Create` 里那句写入**(`terrain[x, y, z] = new TerrainCell(walkable, ter.CellSpeedIndex[idx]);`)
**不在**这 19 行里 —— 它写的是 `ter.CellSpeedIndex[idx]`,字面不出现 `KSpeedIdx`。
⇒ 「写入侧」与本判据无关;本 AC 要的是**读者**,而读者只有 `ValidateTerrainSpeedIndices` 一处。

⇒ 故事卡判据「`grep -c KSpeedIdx` 的非定义侧 ≥ 1」**成立**(19 ≥ 1),且其中**确有一处读取**。

---

## §八 本轮自己的错误(五处,逐条如实登记)

### ① `TerrainSource.cs` 的注释引用了**当时还不存在**的类型

首版注释写「约定本身住在 `Sim.Contracts` 的 `LatticeParamQuantization`」,
而那个类型在那一刻**不存在**(全仓 `grep` 零命中,本轮实测确认)。
这不是笔误 —— 是「先写注释描述打算建什么,再建」的顺序被颠倒,而注释里的
**引用形态**(`<see cref="…">` 风格)在缺失时会静默失效。**处置**:先补类型,再让引用成立。

### ② 加外墙环时发现脚手架关卡的第四格落在环上(`(7,1,7)` → `(6,1,6)`)

`ScaffoldSecondLevel` 的第四格原在 `(7,1,7)`,而 `8×2×8` 的外环是 `x∈{0,7} ∨ z∈{0,7}`
⇒ 那格**不可通行**,槽位声明明永远无法抵达。**处置**:改 `(6,1,6)`(仍在 `y = 1`,AC-2 不受影响);
并新增 `LevelConsistencyCheck` 的判据「槽位落在不可走的格上」—— **没有这条判据,
那个错误只能靠人眼在 128 个数里发现**。
⚠️ 该判据**只在关卡声明了可走性时才产生**(`Walkable == null` = 只声明槽位的夹具),
所以 story-001 的 9 条一致性夹具一条都没红(它们不声明几何)。

### ③ `test_ac5` 首跑按 `InvalidDataException` 断言 ⇒ **实得 `InvalidOperationException`**

首跑 14 条里 **1 红**,红的正是它。按纪律「实得 > 预判须先查清」,查得:
`AddressablesDataProvider.FetchBytes` 会把**注入字节源**抛的异常包一层
`[E-13] … 注入字节源抛出`,`InnerException` 才是 `InvalidDataException`。
**处置**:判据改成「外层 `InvalidOperationException` + 报文含 `TERRAIN_TABLE` +
内层 `InvalidDataException`」—— **比只断类型更严**(它连「包装层把真因吃掉」也钉住)。

### ④ `DataBakeMenu` 里把 `[大医精诚]` 写成了 `[大精诚]`

复制粘贴时漏了一个字。日志前缀是全仓统一的诊断锚点,少一个字会让按前缀 grep 的运维动作漏掉这一条。
**处置**:当场订正。

### ⑤ `WorldTerrainCookedWriter` 的注释里留了一段垃圾(`(var747)`)

无意义的占位串混进了正式注释。**处置**:当场删除。

> ⚠️ ③ 是唯一一条**由夹具跑出来的**错误;①②④⑤ 都是写完当场自查发现。
> 不把「我自己发现得快」当成模型 —— ③ 说明外部判据(真的跑一次)仍然必要。

---

## §九 未做 / 归后续轮

| 项 | 归属 |
|---|---|
| C1 / C2 / C2' 的**检查实现** | S6 |
| POI / 资源点的真实数据 | S4 |
| 生态区多边形 / 导航格 | S3 / S5 |
| `K_speed` / `LATTICE_SIZE` / `slopeLimit` / `stepOffset` 的**取值** | 用户数值轮 |
| `WorldLatticeParams` 的装配 | 数值轮之后 |
| `/code-review` | 归用户触发(与 story-001 同) |
