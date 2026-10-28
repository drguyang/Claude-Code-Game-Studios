# level-tool story-004 实现轮 · 证据原件 —— `world_poi` / `world_resources`

**对象**:`production/epics/level-tool/story-004-world-poi-and-resources.md`(AC-1 … AC-6)
**日期**:2026-10-29
**承接**:S20-20 挂标 **#5「世界几何仍是合成」**的 **POI / 资源半边**
**结论**:AC-1 … AC-6 全部有载体;**15 条变异全部转红(零假绿)**;
本轮抓到并修掉**我自己的四处错误**(§八),其中 ④ 是**一条设计冲突**(一格多条资源 × 运行期单 int)。

---

## §一 落点

| # | 件 | 落点 | 性质 |
|---|---|---|---|
| 1 | **POI 定义声明式源**(id / 格 / type / guard_ref) | `unity/Assets/Editor.Tools.Level/PoiSource.cs` | 新建 |
| 2 | **资源点声明式源**(id / 格 / 生态区倾向) | `unity/Assets/Editor.Tools.Level/ResourceSource.cs` | 新建 |
| 3 | **关卡源扩展**(`Poi` / `Resources` 两个字段 + 两个可选形参) | `unity/Assets/Editor.Tools.Level/LevelSource.cs` | 修改 |
| 4 | **P0 医馆:3 条 POI + 4 个资源点** | `unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs` | 修改 |
| 5 | **脚手架关卡:1 条 POI + 2 个资源点**(其一带 `ecozone_id = -1`) | `unity/Assets/Editor.Tools.Level/ScaffoldSecondLevel.cs` | 修改 |
| 6 | **导出**:`ExportPoi` / `ExportResources` + 两个 per-product schema 号 | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 修改 |
| 7 | **分派表登记**:Poi / Resources 从「未实现」移到「已实现」(未实现 3 → 1) | `unity/Assets/Editor.Tools.Level/WorldDataSetKind.cs` | 修改 |
| 8 | **烘焙三件套 ×2** | `Editor.Tools.Bake/World{Poi,Resources}{Bake,Binder,CookedWriter}.cs` | 新建 ×6 |
| 9 | **菜单**(两条烘焙入口 + 两个产物名常量) | `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs` | 修改 |
| 10 | **运行期 codec ×2** | `Gameplay.Presentation/World{Poi,Resources}CookedCodec.cs` | 新建 ×2 |
| 11 | **装载面**(2 address / 2 `Ensure*` / `Load` 分派 / `Preload` / 磁盘兜底) | `AddressablesDataProvider.cs` · `DataCorePreloader.cs` · `DataCookedDisk.cs` | 修改 |
| 12 | **世界工厂填两格**(`PoiGrid` / `ResourceGrid`)+ `CellIndexChecked` | `unity/Assets/Gameplay.Boot/MinimalWorldFactory.cs` | 修改 |
| 13 | **定义集装载面**(`LoadPoiIds`,public) | `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 修改 |
| 14 | **产物**(作者态 + 烘焙) | `assets/data/world_{poi,resources}.json`(295 B / 346 B)· `DataCooked/world_{poi,resources}.cooked.bytes`(96 B / 104 B) | 新建 ×4 |
| 15 | **data-core 组条目** | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset` | 修改(**29 → 31**) |
| 16 | **漂移门登记** | `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` | 修改(`pinnedDeclared` **29 → 31** + 2 条 `ArtifactBake`) |
| 17 | **既有夹具三处**:① 分派表闭集/未实现数 ② 靶前移 `Poi` → `NavChunk` ③ 世界产能夹具扩展 | `level_source_generalization_test.cs` · `world_geometry_load_test.cs` | 修改 |
| 18 | **夹具** | `unity/Assets/Tests/EditMode/LevelTool/poi_and_resource_load_test.cs` | 新建,17 条 `[Test]`,零写盘 |

**未碰**(故事卡 Out of Scope):POI 状态机本体 / `PoiState` 转移(归 6)· 52 的 `spawn_anchor`
抽池 · 17 采集的资源消费 · C1–C6 · `guard_ref` 指向的具体敌人(27 的定义集今日不存在)。

---

## §二 AC 逐条 → 载体 → 反证

夹具文件 = `unity/Assets/Tests/EditMode/LevelTool/poi_and_resource_load_test.cs`(17 条)。

| AC | 判据 | 载体 | 反证(改坏什么 ⇒ 哪条红) | 判定 |
|---|---|---|---|---|
| **AC-1** | `poi_id` 唯一且 ≥ 0;格全整数;**同一格不声明两个 POI** | `LevelExporter.ExportPoi` + `PoiSource` 构造门 + `ValidateAgainstLevel`;`test_ac1_export_poiIdsUnique_…` · `…_negativePoiId_throws` · `…_duplicatePoiId_throws` · `…_twoPoisOnSameCell_throws` · `…_poiCellOutOfLevelRange_throws` | **M1**(停 `PoiId < 0`)⇒ 1 红 · **M2**(停 id 唯一)⇒ 1 红 · **M3**(停同格)⇒ 1 红 · **M4**(写 `4.0`)⇒ 2 红 · **M5**(停越界 throw)⇒ 1 红 | ✅ |
| **AC-2** | 格全整数且在范围内;`resource_id ≥ 0`;**格不与 POI 格冲突** | `LevelExporter.ExportResources` + `ResourceSource`;`test_ac2_export_resourceIdsNonNegative_…` · `…_negativeResourceId_throws` · `…_cellOutOfLevelRange_throws` · `…_resourceOnPoiCell_throws` | **M6** ⇒ 1 红 · **M7** ⇒ 1 红 · **M8**(停 POI 格交叉判据)⇒ 1 红 | ✅ |
| **AC-3** | 两份产物烘焙 + 进组;**组条目数 +2** | `World{Poi,Resources}{Bake,Binder,CookedWriter}`;`test_ac3_bakeTwice_bothProductsBitIdentical` · `…_bothAddressesInPreloadList_andProductsExist`;**真 batch 实跑见 §五** | **M9**(`guard_ref` 掺 `Guid`)⇒ 1 红 · **M10**(删地址表条目)⇒ 1 红 | ✅ |
| **AC-4** | `PoiGrid` 来自产物(不是全 `-1`);`PoiStateMachine` 能对已登记 id 发事件 | `MinimalWorldFactory` 填格 + `CompositionRoot.LoadPoiIds`;`test_ac4_poiGrid_…` · `…_poiStateMachine_idsFromArtifact_…` · `…_loadPoiIds_matchesProductDefinitions` | **M11**(填 `-1`)⇒ 1 红 · **M12**(`LoadPoiIds` 恒空)⇒ **2 红** | ✅ |
| **AC-5** | `ResourceGrid` 来自产物;**两个方向** | `MinimalWorldFactory` 填格 + 读侧同格门;`test_ac5_resourceGrid_…` · `…_codec_rejectsDuplicateCell` | **M13**(填 `-1`)⇒ 1 红 · **M14**(停读侧同格门)⇒ 1 红 | ✅ |
| **AC-6** | 工具程序集**零** `PoiState` 符号 | `test_ac6_toolAssembly_hasNoPoiStateSymbol`(扫全 `.cs`,**剥注释**;字符串也计入) | **M15**(在代码里加 `typeof(PoiState)`)⇒ 1 红 | ✅ |

### AC-6 的判据口径(**本轮就地收窄并登记**)

故事卡原文:`grep -c "PoiState" unity/Assets/Editor.Tools.Level/*.cs == 0`(纯文本)。
QA 附注:「须用 AST 或至少剥注释」。

**本轮取「剥注释、但**字符串字面量也计入**」** —— 即比 QA 附注**更严**,不比它松:

- 剥注释的理由:注释里写「为什么不碰 `PoiState`」是**正常**的,不剥会把说明文字判成违例。
- **字符串不剥**的理由:一旦允许「字符串里可以出现」,就等于给了一条后门 ——
  将来有人写 `Type.GetType("DaYiJingCheng.Sim.World.PoiState")`,判据仍绿。
- 代价(本轮实际付出):三条**异常报文**里的 `PoiStateMachine` / `PoiStateChanged` 字样被改写成
  「状态机的 id 集」「POI 状态变更事件」。**这是判据驱动代码的正当方向**(收紧判据 ⇒ 改代码),
  不是把判据改松去迁就代码。

### AC-4② 的边界(**刻意不经装配根,理由见 §七.1**)

判据是「`PoiStateMachine` 能对**已登记**的 poi_id 发 `PoiStateChanged`」。本轮的**已登记**来自真产物
(`CompositionRoot.LoadPoiIds(null)`),机器本身在夹具里用既有测试替身构造
(`FakeEventAuthority` + `WorldEcozones.SpyEventSink` + `PayloadEncoder`)。

⚠️ **没有**在 `CompositionRoot.Assemble` 里构造 `PoiStateMachine` —— 见 §七.1,那是**有意的**边界。

---

## §三 证伪轮 —— 15 条变异全部转红,一条超判

**纪律**:一次变异只改**一个属性**;改 → 跑 → 还原在同一脚本内;还原判据 = **SHA-256 逐字节**;
禁用 `git checkout` / `git stash`;窗口内不读不编辑被变异件。
驱动器 `/home/gu/.claude/jobs/181888b1/tmp/falsify_story004.py`;原始日志 `falsify-story004.log`。

**靶场**:`-testFilter DaYiJingCheng.Tests.LevelTool`(**69 条**,基线 69/69 全绿)。

| ID | 属性 | 预判 | 实得 | 判定 |
|---|---|---|---|---|
| M1 | AC-1 `poi_id ≥ 0` | 1 | 1 | ✅ |
| M2 | AC-1 `poi_id` 唯一 | 1 | 1 | ✅ |
| M3 | AC-1 一格一条 POI | 1 | 1 | ✅ |
| M4 | AC-1 导出**零浮点** | 1 | **2** | 超判(见下) |
| M5 | AC-1 POI 越界 throw | 1 | 1 | ✅ |
| M6 | AC-2 `resource_id ≥ 0` | 1 | 1 | ✅ |
| M7 | AC-2 资源越界 throw | 1 | 1 | ✅ |
| M8 | AC-2③ 资源格不撞 POI | 1 | 1 | ✅ |
| M9 | AC-3 烘焙**确定性** | 1 | 1 | ✅ |
| M10 | AC-3 **地址表登记** | 1 | 1 | ✅ |
| M11 | AC-4① 工厂填 POI 格 | 1 | 1 | ✅ |
| M12 | AC-4② 定义集装载 | 2 | 2 | ✅ |
| M13 | AC-5 工厂填资源格 | 1 | 1 | ✅ |
| M14 | AC-5 读侧同格门 | 1 | 1 | ✅ |
| M15 | AC-6 工具不碰状态 | 1 | 1 | ✅ |
| | **合计** | **16** | **17** | |

**九份被变异文件的还原全部 SHA-256 逐字节相同**(逐条 `RESTORE-OK`,见原始日志)。

### 3.1 唯一一条**超判**(M4,1 → 2)—— 先独立论证「它为什么该红」

实得两条:`test_ac1_export_poiIdsUnique_…`(预期)+ `test_ac3_bakeTwice_bothProductsBitIdentical`(意外)。

**为什么该红**:`test_ac3_bakeTwice` 把 `LevelExporter.ExportPoi(src)` 的文本喂给
`WorldPoiBake.BakeFromSourceText`。把 `4` 写成 `4.0` 之后,`WorldPoiBinder.TryReadI32` 要求
`JsonNodeKind.Integer`,`4.0` 是数字但非整数字面量 ⇒ **烘焙期类型门拒收** ⇒ 该测抛
`BakeValidationException` ⇒ 红。

⇒ **这是烘焙期门在起作用**,不是夹具互相污染。我的预判只想着「导出器自己的文本断言会抓」,
漏了「同一份文本还会过一遍烘焙门」。**预判错,夹具对**,超判方向安全(不是假绿)。

### 3.2 命中形态(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证 |
|---|---|---|
| **定义域收窄** ×8 | id 值域 / 唯一性 / 同格 / 越界 / 值域门 | M1 / M2 / M3 / M5 / M6 / M7 / M8 / M10 各恰 1 红 |
| **一次性/幂等** | 同源两次烘焙逐字节相同 | 掺 `Guid` ⇒ `test_ac3_bakeTwice_…` 红(M9) |
| **阈値边界** | `PoiGrid` / `ResourceGrid` 两个方向(`== id` 与 `== -1`) | 填 `-1` ⇒ 各 1 红(M11 / M13) |
| **共存/互斥** | 「有值」与「无值」互斥;「同格两行」非法 | M12 / M14 |

**Gate 结果**:PASS —— 15 条变异**全部转红**,零假绿;一条超判已独立论证。

---

## §四 回归

**三元组(强制)**:截至时刻 `2026-10-29 02:58:16 CST`(EditMode)/ `02:58:51 CST`(PlayMode)
· 批次 **S4 收口终跑** · commit `b05d57d2` + 工作树(43 项改动)

### 4.1 全量 EditMode

| 项 | 值 |
|---|---|
| total | **5789** |
| passed | **5754** |
| failed | **0** |
| inconclusive | 0 |
| skipped | 35 |

基线(S3 收口,`unity/Logs/s3-final-edit.xml` @ `2026-10-29 02:22:27 CST`):`5770 / 5735 / 0 / 0 / 35`。

**等式(可复算)**:`5789 − 5770 = 19` = **17** 条新 `[Test]`(`poi_and_resource_load_test.cs`)
+ **2** 条漂移门参数化用例(`drift[world_poi.cooked.bytes]` / `drift[world_resources.cooked.bytes]`)。
**消失 0 条。**

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
def names(p): return {tc.get("fullname") for tc in ET.parse(p).getroot().iter("test-case")}
a = names("unity/Logs/s3-final-edit.xml"); b = names("unity/Logs/s4-final-edit.xml")
print("S3 =", len(a), " S4 =", len(b), " 新增 =", len(b-a), " 消失 =", len(a-b))
PY
```

**实跑输出**:

```
S3 = 5770  S4 = 5789  新增 = 19  消失 = 0
```

### 4.2 全量 PlayMode

| 项 | 值 |
|---|---|
| total | **238** |
| passed | **234** |
| failed | **0** |
| inconclusive | **2** |
| skipped | 2 |

基线(S3 收口 `238 / 234 / 0 / 2 / 2`)**逐项相同 ⇒ 零回归**。
退出码非 0 系既有两条 `Inconclusive`(`U1Assump6GamepadFocusSpikeTest` 的 `…_notRun`),与基线同形。

> 📌 **首跑全量 EditMode 零红** —— 与 S2 / S3 不同,本轮没有抓到真回归。
> 原因是 §八④ 那条(`world_geometry_load_test` 的字节源)在**定向跑**阶段就暴露并修掉了,
> 没有留到全量。**这不改纪律**:全量仍是必跑项,只是这一次没再抓新的。

---

## §五 真 batch 实跑证据

### 5.1 导出(`ExportP0ClinicToRepo`)—— `unity/Logs/s4-export.log`

```
[关卡工具] 已导出 world_buildslots.json → …/assets/data/world_buildslots.json
[关卡工具] 已导出 world_geometry.json  → …/assets/data/world_geometry.json
[关卡工具] 已导出 world_ecozones.json  → …/assets/data/world_ecozones.json
[关卡工具] 已导出 world_poi.json       → …/assets/data/world_poi.json
[关卡工具] 已导出 world_resources.json → …/assets/data/world_resources.json
[关卡工具] 已导出 world_terrain.json   → …/assets/data/world_terrain.json
```

### 5.2 烘焙(`BakeWorldPoi` / `BakeWorldResources`)—— `s4-bake-poi.log` / `s4-bake-res.log`

```
[大医精诚] 烘焙完成:world_poi.cooked.bytes = 96 B,POI 3 个,ConfigVersion = 0x68735663
[大医精诚] 烘焙完成:world_resources.cooked.bytes = 104 B,资源点 4 个,ConfigVersion = 0x4CF2E965
```

### 5.3 `data-core` 组与 catalog(`BuildAddressablesContent`)—— `s4-addr.log`

```
[大医精诚] data-core 组就绪:31 个条目已入组并打标签。
```

**复算命令(当场可跑)**:

```bash
python3 - <<'PY'
import re
t = open("unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset", encoding="utf-8").read()
print("组条目:", len(re.findall(r"- m_GUID:", t)))
print("地址数:", len(re.findall(r"m_Address: (.+)", t)))
for k in ("world_poi", "world_resources"):
    print(f"含 {k}:", f"{k}.cooked.bytes" in t)
PY
ls unity/Assets/DataCooked/*.cooked.bytes | wc -l
```

**实跑输出(2026-10-29 02:47 CST)**:

```
组条目: 31
地址数: 31
含 world_poi: True
含 world_resources: True
31
```

⇒ 组内条目 **31** == 盘上产物 **31**,两个新项确在组内(AC-3 的「+2」)。

---

## §六 产物指纹与既有产物的零漂移

### 6.1 新增四份

| 产物 | SHA-256 | 字节 |
|---|---|---|
| `assets/data/world_poi.json` | `bed0cebaf8ba78232995f77decdd8959eb78c7a5834e9b43228e4df891129ea9` | 295 |
| `assets/data/world_resources.json` | `1c3d4e05270b00921c03ad29dec472f7642faa026499ed6be28bdbdbc560621a` | 346 |
| `DataCooked/world_poi.cooked.bytes` | `3bf4b3da7f3fe44c571d73c1d3f6c180551a835db413707f3f0ed79a4ef1c0f3` | 96 |
| `DataCooked/world_resources.cooked.bytes` | `eef0e6bb3672fcc06d0014bd54d4b44e2cdda4349d49a8738e16956990bd5525` | 104 |

作者态产物**零浮点字面量**,格式为三维格三元组(与生态区的 `[x, z]` 二元组**刻意不同**):

```json
{"poi_id": 0, "cell": [4, 0, 4], "type": 1, "guard_ref": 2}
{"resource_id": 1, "cell": [2, 0, 2], "ecozone_id": 0}
```

### 6.2 既有四份产物**逐字节未变**(「导出全类别」重写了它们,但字节相同)

```
1c946bff60177ff294f4e3bcf4a235e67b4e5067ba07db105047d57762074c6d  world_buildslots.json
6bb382718a0c59c053ab791957d6a16aa5ae5dccb0211b7644b09ad99b8d2de0  world_geometry.json
2802ae72503e01ee44f5ae8657b0317e7bdcb2698a325d02e333d9562ccfa8fd  world_terrain.json
c228df16b58a8cdc291decd412cac5bbe654de2595389c2817660e5ac85f6246  world_ecozones.json
```

⚠️ mtime 被 `02:38` 的导出刷新(四类一起写),**故 mtime 不构成「未改动」的证据 —— 哈希才是**。

---

## §七 本件证明不了的事(禁借绿)

1. **`PoiStateMachine` 未接进 `CompositionRoot.Assemble`** —— 它的构造需要 `IEventAuthority`,
   而全仓**零生产实装**(`SeenDiseaseProjector.cs:23-26` 把这条登记为 P1b 缺口,
   `out_of_bounds_rejection_test` 还有一条夹具断言「零生产实装」)。
   在装配根里塞一个生产占位 = **私自关掉那条已登记的缺口** ⇒ 本轮只接**定义侧**
   (`LoadPoiIds`),机器本身在夹具里由测试替身构造。**「已登记的定义集能驱动状态机」已证;
   「生产装配里存在一台状态机」未证**,归 P1b 的 `IEventAuthority` 轮。
2. **POI 状态转移是否真发生** —— 需要玩家走近,归 `world-ecozones/story-005` 的走查。
3. **资源是否真可采** —— 本 story 只让 `ResourceGrid` 有真值;采集链归 17 的实现轮。
4. **`guard_ref` 档位是否合理** —— 数值归用户数值轮。
5. **`ecozone_id` 的存在性未校验** —— 刻意如此(跨数据集门归 S6 的 C 系列,见 `ResourceSource.cs` 文件头)。
6. **AC-6 是文本级判据** —— 它证明「工具源码(剥注释、含字符串)里没有 `PoiState` 词元」,
   **不证明**「工具在语义上真的不需要状态」。若将来需要,该 AC 会红,那时须回 ADR-022 追加裁决。

---

## §八 本轮自己的错误(四处,逐条如实登记)

### ① ⚠️ **设计冲突:一格多种资源 × 运行期单 int**

初稿 `ResourceSource` 写的是「**一格可以长多种药材**,与 POI 的『一格一条』刻意不同」。
写完读运行期才发现:`WorldGeometry.ResourceGrid` 是 **每格一个 `int`**(`ResourceGrid[i] == 该格的 resource_id`)。
同格两条 ⇒ 填格只能**留下一条、丢掉其余的**,而且**没有任何报错**。

⇒ 改为**构造期拒绝**(`seenCells`),并在**读侧 codec** 再加一道纵深防御(产物可被手工改)。
⚠️ 「一格多种药材」若真要做,是 **ADR 级**改动:`ResourceGrid` 的 `int` 语义要换成集合 / 位域,
连同 `GetResourceId` 的返回类型与全部消费者一起改 —— 加一条 `HashSet` 挡不住它。

**这条值得单记**:它是「写了数据能表达、但运行期容器表达不了」的**静默**形态。

### ② 三处编译错误(逐条)

1. `PoiSource.cs` 的异常报文里用了 `PoiStateMachine` ⇒ **AC-6 的文本判据当场红**。
   起初我打算把判据改成「剥注释 + 剥字符串」—— **那正是把判据改松去迁就代码**。
   最终改的是**代码**(报文改写),判据一字未松。
2. `world.Geometry.GetPoiId` —— 我写成了 `world.GetPoiId`。`World` 不暴露 `GetPoiId`,
   `WorldGeometry` 才暴露(`World.Geometry` 本来就可达)。
3. `WorldPoiBake` 未 `using DaYiJingCheng.EditorTools.Bake`。

### ③ `test_ac5_codec_rejectsDuplicateCell` 首跑「因错的理由」失败

我手拼的坏产物只换了载荷、**没改头部 offset 16 的载荷长度** ⇒ `OpenChecked` 先抛「载荷长度不符」,
`StringAssert.Contains("两行")` 失败。
⚠️ 若不看报文只看到「测试确实失败了」,很容易误以为判据成立 —— **它当时证明的是另一件事**。
已补写头部长度。

### ④ 世界产能夹具(`world_geometry_load_test`)**必须多供两份产物**

`MinimalWorldFactory.Create` 现在即时 Load POI / 资源点 ⇒ 该夹具那条「只供世界三件产物」的
字节源当场抛(两条用例红)。已按**几何产物自己声明的尺寸**现场烘两份最小合法产物
(不用盘上真种子 —— 该类的几个夹具刻意用 4×1×4 这类**小于真种子**的合成世界,真种子的格会整体越界)。

⚠️ 与 S3 §八④ 同型:**每加一份装配期必需的产物,所有用 provider 装配的夹具都要跟着登记**。
这是第二次撞,已在本件留档。

---

## §九 未做 / 归后续轮

- **`/code-review` 未跑** —— 归用户触发(与 story-001/002/003 同)。
- **`PoiStateMachine` 的生产装配** —— 归 P1b(`IEventAuthority` 轮),见 §七.1。
- **S5(导航格切片 + C6/C3)· S6(C1/C2/C2')** —— 本 epic 剩余两件。
