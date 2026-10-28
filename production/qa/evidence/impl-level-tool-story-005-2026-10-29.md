# level-tool story-005 实现轮 · 证据原件 —— 导航格切片 + C6 + C3

**对象**:`production/epics/level-tool/story-005-world-nav-chunk-slicing.md`(AC-1 … AC-6)
**日期**:2026-10-29
**承接**:S20-20 挂标 **#3「C1–C6 未实现」**的 **C6 / C3** 两条 + 挂标 **#4** 的 **chunk 半边**
**结论**:AC-1 … AC-6 全部有载体;**15 条变异全部转红(零假绿)**;
本轮抓到并修掉**我自己的三处错误**(§八),其中 ① 与 ③ 都是**首跑才暴露**的。

---

## §一 落点

| # | 件 | 落点 | 性质 |
|---|---|---|---|
| 1 | **`INavLattice` 契约**(只读查询面) | `unity/Assets/Sim.Contracts/INavLattice.cs` | 新建 |
| 2 | **`NavLattice` 实现**(未驻留 = block) | `unity/Assets/Sim/World/NavLattice.cs` | 新建 |
| 3 | **chunk 划分的唯一函数** `ChunkTopology.ForWorld` | `unity/Assets/Sim/World/ChunkActivator.cs` | 修改 |
| 4 | **切片器 + C6**(漏格 / 重复格两条独立) | `unity/Assets/Editor.Tools.Level/NavChunkSource.cs` | 新建 |
| 5 | **C3**(三态告警) | `unity/Assets/Editor.Tools.Level/NavMeshConsistencyCheck.cs` | 新建 |
| 6 | **C3 生产采样器**(NavMesh → 三态) | `unity/Assets/Editor.Tools.Level/NavMeshReachabilitySampler.cs` | 新建 |
| 7 | **`CheckReport.Warnings`** + `UnimplementedChecks` 5 → 3 | `unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` | 修改 |
| 8 | **`LevelSource.ChunkSize{X,Y,Z}`** + `GetChunkTopology()` | `unity/Assets/Editor.Tools.Level/LevelSource.cs` | 修改 |
| 9 | **导出**:`ExportNavChunks` / `RenderNavChunk` / 六个 `*Products` 适配器 | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 修改 |
| 10 | **分派表改多份**:`DataSetProduct` / `SerializeAll` / `NavChunkFileName` | `unity/Assets/Editor.Tools.Level/WorldDataSetKind.cs` | 修改 |
| 11 | **几何 schema 1 → 2**:`chunk_size` 根键 | `LevelExporter` · `WorldGeometryBinder` · `WorldGeometryCookedWriter` · `WorldGeometryCookedCodec` | 修改 ×4 |
| 12 | **导航格烘焙三件套** | `Editor.Tools.Bake/WorldNavChunk{Bake,Binder,CookedWriter}.cs` | 新建 ×3 |
| 13 | **运行期 codec** | `Gameplay.Presentation/WorldNavChunkCookedCodec.cs` | 新建 |
| 14 | **菜单**(烘焙全部切片 + P0 产物名登记表 4 条字面量) | `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs` | 修改 |
| 15 | **磁盘兜底装载** `LoadWorldNavChunk(int)` | `unity/Assets/Gameplay.Presentation/DataCookedDisk.cs` | 修改 |
| 16 | **chunk 尺寸声明**(P0 = 8×1×8;脚手架 = 4×1×4) | `P0ClinicLevel.cs` · `ScaffoldSecondLevel.cs` | 修改 ×2 |
| 17 | **产物** | `assets/data/world_nav_{0..3}.json`(各 402 B)· `DataCooked/world_nav_{0..3}.cooked.bytes`(各 308 B)· `world_geometry` 重烘 | 新建 ×8 + 重烘 ×2 |
| 18 | **data-core 组条目** | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset` | 修改(**31 → 35**) |
| 19 | **漂移门登记** | `cooked_drift_all_artifacts_test.cs` | 修改(`pinnedDeclared` **31 → 35** + 4 条 `ArtifactBake`) |
| 20 | **既有夹具订正** | `level_source_generalization_test.cs` · `level_tool_consistency_test.cs` · `world_geometry_load_test.cs` | 修改 ×3 |
| 21 | **夹具** | `unity/Assets/Tests/EditMode/LevelTool/nav_chunk_slicing_test.cs` | 新建,21 条 `[Test]`,零写盘 |

**未碰**(故事卡 Out of Scope):NavMesh 运行期烘焙(归 27)· 寻路算法本体(归 27)·
C1 / C2 / C2'(归 S6)· C4 / C5(归 S3)· **chunk 激活权(归 6 / ADR-023 ⑥)**。

---

## §二 AC 逐条 → 载体 → 反证

| AC | 判据 | 载体 | 反证(改坏什么 ⇒ 哪条红) | 判定 |
|---|---|---|---|---|
| **AC-1** | `INavLattice` 存在;**未驻留 chunk 查询回 `block`** | `Sim.Contracts/INavLattice.cs` + `Sim/World/NavLattice.cs`;`test_ac1_*`(4 条) | **M1**(未驻留 ⇒ true)⇒ 1 红 · **M2**(放掉 `< 0` 下界)⇒ 1 红 · **M13**(块内读值取反)⇒ 2 红 | ✅ |
| **AC-2** | 每 chunk 一文件;`{chunk}` 键与**几何侧**同一函数算 | `ExportNavChunks` + `NavChunkFileName` + `ChunkTopology.ForWorld`;`test_ac2_*`(3 条) | **M3**(文件名偏移 +1)⇒ 1 红 · **M4**(切片自报 `chunk_size` 与几何声明分家)⇒ 1 红 · **M14**(导出掺随机)⇒ 2 红 | ✅ |
| **AC-3** | **C6 每格恰覆盖一次**;**漏格 / 重复格两条独立** | `NavChunkSliceCheck.TryCheck`;`test_ac3_*`(3 条) | **M5**(停漏格分支)⇒ 1 红 · **M6**(停重复格分支)⇒ 1 红 · **M15**(`NavChunk` 退出分派表)⇒ 2 红 | ✅ |
| **AC-4** | **C3 = 告警**:① 注入矛盾 ⇒ **报出** ② **不**使构建失败 | `NavMeshConsistencyCheck.TryCheck` + `CheckReport.Warnings`;`test_ac4_*`(5 条) | **M7**(停方向一)⇒ 1 红 · **M8**(停方向二)⇒ 1 红 · **M9**(停 `Unknown` 短路)⇒ 1 红 · **M10**(告警并入 `Violations`)⇒ 2 红 | ✅ |
| **AC-5** | 各 chunk 产物烘焙 + 进组;组条目数 = 原数 + chunk 数 | `WorldNavChunk{Bake,Binder,CookedWriter}` + `DataBakeMenu` 菜单 + `WorldNavChunkCookedCodec`;`test_ac5_*`(5 条);**真 batch 实跑见 §五** | **M11**(停「装错块」门)⇒ 1 红 · **M16**(参数化名字与登记表分家)⇒ 3 红 | ✅ |
| **AC-6** | `UnimplementedChecks` 5 → 3(C3 / C6 移出) | `LevelConsistencyCheck.UnimplementedChecks`;`test_ac6_unimplementedChecks_dropsC3AndC6_leavingThree` | **M12**(把一条 C3 写回)⇒ **2 红** | ✅ |

### AC-1 的载体说明(形状取自 ADR-016 §三 的反推,未另立第二个坐标)

`INavLattice` = **只读**查询面 `bool IsWalkable(WorldPos cell)`,住 `Sim.Contracts`(契约),
实现 `NavLattice` 住 `Sim`(句柄)—— 与 ADR-029 / ADR-031 的「接口在契约程序集、实现在后」同款分层。
写面(装载 / 卸载 chunk)刻意**不进契约**:它归系统 6 的 chunk 激活器(ADR-023 ⑥),
与 `IBlobPool` 只有读面、写面另立 `IBlobSink` 是同一条纪律。

### AC-2 的「同一个函数」是怎么落到判据上的

`ChunkTopology.ForWorld(sizeX, sizeY, sizeZ, csX, csY, csZ)` 是划分的**唯一**出处,三处都调它:
① `WorldGeometryDataSet.Topology`(运行期,**读烘焙产物**的 `world_size + chunk_size`);
② `NavChunkSlicer.Slice`(工具,切导航格);③ `NavLattice`(运行期,查格)。

判据不是「代码里只有一处 `(x+n-1)/n`」(那是文本级,可被等价改写绕过),而是
**两个独立产物在数据面上对得上**:`test_ac2_chunkKeys_agreeWithGeometryProduct`
拿**烘焙出来的** `world_geometry` 解出 `chunk_size`,再要求导航格切片自报的
`chunk_size` 与 `chunk` 三元组都与之相符。M4(切片自报 `chunk_size + 1`)正是打这一面。

### AC-4 的三态设计(依据 = NavMesh spike,不是偏好)

`production/qa/evidence/spike-navmesh-6.3-2026-10-29.md` 实测:
`NavMesh.CalculateTriangulation().vertices.Length == 0` 是「本场景有没有 NavMesh 数据」的
**可靠**存在性探针(既不抛也不假成功)⇒ C3 分 `Reachable` / `Unreachable` / `Unknown`。

`Unknown` **不告警**。理由不是完备性讲究,是**必需**:P0 至今零 baked NavMesh 资产,
若把「无数据」当成「全不可达」,C3 会对**每一个**逻辑可走的格报一次 —— 196 条噪声
(P0 的可走格数)会把真告警淹掉,而那种「跑一次就满屏警告」的检查在实践中等于不存在。

**无需回 ADR-022 追加裁决**(故事卡 §诚实边界 5 的触发条件未成立)。

---

## §三 证伪轮 —— 16 条变异全部转红,一条超判

**纪律**:一次变异只改**一个属性**;改 → 跑 → 还原在同一脚本内;还原判据 = **SHA-256 逐字节**;
禁用 `git checkout` / `git stash`;窗口内不读不编辑被变异的编译单元。
驱动器 `/home/gu/.claude/jobs/181888b1/tmp/falsify_story005.py`(+ `falsify_s5_m4.py` / `falsify_s5_m16.py` 两条补跑);
原始日志 `falsify-story005.log`。

**靶场**:`-testFilter DaYiJingCheng.Tests.LevelTool`(基线 89/89 全绿)。

| ID | 属性 | 预判 | 实得 | 判定 |
|---|---|---|---|---|
| M1 | AC-1 未驻留 ⇒ block | 1 | 1 | ✅ |
| M2 | AC-1 越界 ⇒ block | 1 | 1 | ✅ |
| M3 | AC-2① 切片命名 | 1 | 1 | ✅ |
| M4 | AC-2② 切片自报划分 = 几何声明 | 1 | 1 | ✅(加长锚点后重跑) |
| M5 | AC-3 C6 漏格分支 | 1 | 1 | ✅ |
| M6 | AC-3 C6 重复格分支 | 1 | 1 | ✅ |
| M7 | AC-4 C3 方向一(可走→不可达) | 1 | 1 | ✅ |
| M8 | AC-4 C3 方向二(不可走→可达) | 1 | 1 | ✅ |
| M9 | AC-4 `Unknown` 不告警 | 1 | 1 | ✅ |
| M10 | AC-4 告警 ≠ 硬失败 | 2 | 2 | ✅ |
| M11 | AC-5 装错块要抛 | 1 | 1 | ✅ |
| M12 | AC-6 C3 / C6 已移出 | 1 | **2** | 超判(见 §3.1) |
| M13 | AC-1 块内读值方向 | 2 | 2 | ✅ |
| M14 | AC-2 导出确定性 | 2 | 2 | ✅ |
| M15 | AC-3 闭集全实现 | 2 | 2 | ✅ |
| M16 | AC-5 登记表 ↔ 参数化名字 | 3 | 3 | ✅ |
| | **合计** | **21** | **22** | |

**七份被变异文件的还原全部 SHA-256 逐字节相同**(逐条 `RESTORE-OK`,见原始日志)。

### 3.1 唯一一条**超判**(M12,1 → 2)—— 先独立论证「它为什么该红」

实得两条:`test_ac6_unimplementedChecks_dropsC3AndC6_leavingThree`(预期)+
`test_levelTool_realSeed_passesSlotSubsetChecks_andDeclaresSevenUnimplemented`(意外)。

**为什么该红**:后者**本来就在数未实现项条数**(`report.Unimplemented.Count == 3`)。
往 `UnimplementedChecks` 里塞一条 C3 ⇒ 计数变 4 ⇒ 该断言红。

⇒ **是这个变异同时改了两条夹具的输入,不是夹具互相污染**。我的预判只想着「专门为 AC-6 新写的那条」,
漏了「既有那条也在数同一个数」。**预判错,夹具对**,超判方向安全(不是假绿)。

### 3.2 命中形态(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证 |
|---|---|---|
| **定义域收窄** ×5 | 未驻留 ⇒ block / 越界 ⇒ block / 装错块 ⇒ 抛 / 名字分家 ⇒ 抛 | M1 / M2 / M11 / M16 |
| **共存/互斥** ×3 | 「驻留」与「未驻留」互斥;「覆盖 0 次」与「覆盖 >1 次」互斥 | M1 / M5 / M6 |
| **一次性/幂等** | 同源两次导出逐字节相同 | M14 |
| **阈值边界** | `Unknown` 短路 / 三态取值边界 | M9 |
| **结构性** ×4 | 划分同源 / 告警不升级 / 闭集覆盖 / 命名 | M3 / M4 / M10 / M12 / M15 |

**Gate 结果**:PASS —— 16 条变异**全部转红**,零假绿;一条超判已独立论证。

---

## §四 回归

**三元组(强制)**:截至时刻 `2026-10-29 05:08:55 CST`(EditMode)/ `2026-10-29 05:11:09 CST`(PlayMode)
· 批次 **S5 收口终跑(修复后终跑)** · commit `e9d951dd` + 工作树

### 4.1 全量 EditMode

| 项 | 值 |
|---|---|
| total | **5814** |
| passed | **5779** |
| failed | **0** |
| inconclusive | 0 |
| skipped | 35 |

基线(S4 收口,`unity/Logs/s4-final-edit.xml` @ `2026-10-29 02:58:16 CST`):`5789 / 5754 / 0 / 0 / 35`。

**等式(可复算)**:`5814 − 5789 = 25` = **21** 条新 `[Test]`(`nav_chunk_slicing_test.cs`)
+ **4** 条漂移门参数化用例(`drift[world_nav_{0..3}.cooked.bytes]`)+ **2** 条**改名**的既有测(净 0:
`test_ac3_unimplementedKind_…` → `test_ac3_everyKindInClosedSet_isImplemented`;
`test_ac4_unimplementedChecks_stillSeven_…` → `test_ac4_unimplementedChecks_noLongerSaysThisBatch_…`)。

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
def names(p): return {tc.get("fullname") for tc in ET.parse(p).getroot().iter("test-case")}
a = names("unity/Logs/s4-final-edit.xml"); b = names("unity/Logs/s5-final-edit2.xml")
print("S4 =", len(a), " S5 =", len(b), " 新增 =", len(b-a), " 消失 =", len(a-b))
PY
```

**实跑输出**:

```
S4 = 5789  S5 = 5814  新增 = 27  消失 = 2
```

⚠️ **两个「消失」是**改名**,不是删除**:`-2` 与新增集里的两条一一对应(净变化 0)。
**真正的消失 = 0 条。**

### 4.2 全量 PlayMode

| 项 | 值 |
|---|---|
| total | **238** |
| passed | **234** |
| failed | **0** |
| inconclusive | **2** |
| skipped | 2 |

基线(S4 收口 `238 / 234 / 0 / 2 / 2`)**逐项相同 ⇒ 零回归**。
退出码非 0 系既有两条 `Inconclusive`(`U1Assump6GamepadSpikeTest` 的 `…_notRun`),与基线同形。

> 📌 **首跑全量 EditMode 抓到了 1 条真回归**(`test_sweep_declaredArtifactNames_allTriaged`
> 报「声明 31 ≠ 钉定 35」)—— 见 §八①。修完后 `5814 / 5779 / 0 / 0 / 35` 全绿。

---

## §五 真 batch 实跑证据

### 5.1 导出(`ExportP0ClinicToRepo`)—— `unity/Logs/s5-export.log`

```
[关卡工具] 已导出 world_buildslots.json → …/assets/data/world_buildslots.json
[关卡工具] 已导出 world_geometry.json  → …/assets/data/world_geometry.json
[关卡工具] 已导出 world_ecozones.json  → …/assets/data/world_ecozones.json
[关卡工具] 已导出 world_poi.json       → …/assets/data/world_poi.json
[关卡工具] 已导出 world_resources.json → …/assets/data/world_resources.json
[关卡工具] 已导出 world_terrain.json   → …/assets/data/world_terrain.json
[关卡工具] 已导出 world_nav_0.json     → …/assets/data/world_nav_0.json
[关卡工具] 已导出 world_nav_1.json     → …/assets/data/world_nav_1.json
[关卡工具] 已导出 world_nav_2.json     → …/assets/data/world_nav_2.json
[关卡工具] 已导出 world_nav_3.json     → …/assets/data/world_nav_3.json
```

### 5.2 烘焙(`BakeWorldGeometry` / `BakeWorldNavChunks`)—— `s5-bake-geom.log` / `s5-bake-nav.log`

```
[大医精诚] 烘焙完成:world_geometry.cooked.bytes = 1076 B,声明范围 16×1×16,LATTICE_SIZE = 1000 mm,ConfigVersion = 0x67F60110
[大医精诚] 烘焙完成:world_nav 共 4 片
  world_nav_0.cooked.bytes = 308 B, chunk = 0, ConfigVersion = 0x7C6E72AF
  world_nav_1.cooked.bytes = 308 B, chunk = 1, ConfigVersion = 0x32334CEA
  world_nav_2.cooked.bytes = 308 B, chunk = 2, ConfigVersion = 0x3350638E
  world_nav_3.cooked.bytes = 308 B, chunk = 3, ConfigVersion = 0xA540776F
```

### 5.3 `data-core` 组与 catalog(`BuildAddressablesContent`)—— `s5-addr.log`

```
[大医精诚] data-core 组就绪:35 个条目已入组并打标签。
[大医精诚] Addressables 内容已构建 —— catalog 现含全部组条目。(35 条,与盘上 35 个产物一致)
```

**复算命令(当场可跑)**:

```bash
python3 - <<'PY'
import re
t = open("unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset", encoding="utf-8").read()
print("组条目:", len(re.findall(r"- m_GUID:", t)))
for i in range(4):
    print(f"含 world_nav_{i}:", f"world_nav_{i}.cooked.bytes" in t)
PY
ls unity/Assets/DataCooked/*.cooked.bytes | wc -l
```

⇒ 组内条目 **35** == 盘上产物 **35**,四份新切片确在组内(AC-5 的「+4」)。

### 5.4 切片内容自校验(可复算)

```bash
python3 -c "
import json
for i in range(4):
    d=json.load(open(f'assets/data/world_nav_{i}.json'))
    print(i, d['chunk'], d['chunk_index'], len(d['walkable']), sum(d['walkable']))
"
```

**实跑输出**:

```
0 [0, 0, 0] 0 64 49
1 [0, 0, 1] 1 64 49
2 [1, 0, 0] 2 64 49
3 [1, 0, 1] 3 64 49
```

⇒ 每片 8×1×8 = **64** 格;四片可走格合计 **196** == 16×16 去掉一圈 1 格宽墙环后的内部格数
(`14 × 14 = 196`)。**每格恰覆盖一次**(C6 的正例面)。

---

## §六 产物指纹与既有产物的零漂移

### 6.1 新增八份

| 产物 | SHA-256 | 字节 |
|---|---|---|
| `assets/data/world_nav_0.json` | `d630f31aba7774d87f5356611a60707ba42350ce380a5bd12d3761c21816a48a` | 402 |
| `assets/data/world_nav_1.json` | `b0daeb6b931345fafe61cde4081fffef1af699516ead32dda3942b8cb055a7ef` | 402 |
| `assets/data/world_nav_2.json` | `3c3c6fd434728ee1c88f69a595fd279a57a59fb01d2945c43dd235d67656a166` | 402 |
| `assets/data/world_nav_3.json` | `4117a98a4665921120153225213432eee14ec38d68caeaeab1d85ecaeccbe286` | 402 |
| `DataCooked/world_nav_0.cooked.bytes` | `49e553f6b0f05c5db0f1e57f713e06ff074ea2d77372435a93f69f9ae2efdaed` | 308 |
| `DataCooked/world_nav_1.cooked.bytes` | `3fae8cb9fbc64ad4281bf01946bccca3c10ed4d6cb8e24043345e40556b476d5` | 308 |
| `DataCooked/world_nav_2.cooked.bytes` | `2d0c34d9bec98df160567108563317527b0389dc43d4669d99adca57d67a8a8b` | 308 |
| `DataCooked/world_nav_3.cooked.bytes` | `d020ef3c11634c3286e22577b4aa0f63ade4b5ef14c7da285578031d2c403fdc` | 308 |

### 6.2 重烘两份(schema 1 → 2)

| 产物 | SHA-256 | 字节 |
|---|---|---|
| `assets/data/world_geometry.json` | `b1c914fe120d98896c42ea9a36f1b30de6144b4524c3212ff9baba0f724d2fbd` | — |
| `DataCooked/world_geometry.cooked.bytes` | `30102549401a0ac1828105e996e66432734c480d4689915303eb19764a8c129e` | 1076 |

### 6.3 既有五份产物**逐字节未变**(「导出全类别」重写了它们,但字节相同)

| 产物 | 与 HEAD 的差异 |
|---|---|
| `assets/data/world_buildslots.json` | 未变 |
| `assets/data/world_terrain.json` | 未变 |
| `assets/data/world_ecozones.json` | 未变 |
| `assets/data/world_poi.json` | 未变 |
| `assets/data/world_resources.json` | 未变 |
| `DataCooked/world_{buildslots,terrain,ecozones,poi,resources}.cooked.bytes` | 未变(5/5) |

⚠️ mtime 被 05:46 的导出刷新(全类别一起写),**故 mtime 不构成「未改动」的证据 —— 哈希/`git diff` 才是**。

---

## §七 本件证明不了的事(禁借绿)

1. **AI 寻路是否真的走通** —— 本 story 只出**数据**;寻路归 27。
2. **NavMesh 烘焙质量 / `maxDistance` 口径** —— spike 登记为 **NOT-RUN**(P0 零 baked NavMesh 资产)。
   C3 的三态设计**不依赖**该口径(采样半径 = 格边长,且判据面是「报不报」而非「报得准不准」)。
   ⚠️ **2026-10-29 补登记:这份资产今天「没有轮次认领」,不是「P0 的某个阶段会做」。**
   三处权威件一致 —— `ADR-016` §四/§五(NavMesh **仅**驱动表现态位移)· `ADR-022` Out of Scope
   (烘焙归 27 / 表现层)· `p0-remaining-roadmap-2026-10-20.md` **全文零处**出现 NavMesh / 表现层。
   而 27 的 sprint-03 交付的是 **sim 侧**算法,表现侧运动从未排期。
   ⇒ 它的真实落点是 **P1a 的表现层轮**,前置两项今天都不存在:
   ① **视觉层**(Unity Terrain + 建造模块网格,ADR-015 §一 的纯表现侧)未做,NavMesh 无东西可烘;
   ② 13 / 27 的**运动表现**实现轮不存在。
   ⇒ **`maxDistance` 的 NOT-RUN 是长期的,不得被读成「快了」。**
3. **chunk 激活的运行时行为** —— 归 6(ADR-023 ⑥)。本轮**只出切片数据**,
   `NavLattice` 的生产装配(谁在什么时候 `Mount` 哪一块)**未接线**。
4. **导航格切片不进「首次 Step 前常驻」** —— 本件按 ADR-022 §五 的口径**刻意不登记**到
   `DataCorePreloader.DataCoreAddresses`(见 `test_ac5_navChunksAreNotInFirstStepPreloadList`)。
   ⇒ 今日**没有任何生产代码**按需装载它们;`DataCookedDisk.LoadWorldNavChunk` 目前只有夹具消费方。
   这是**如实登记的缺口**,不是「已完成」。
5. **C6 的正例由切片器自己保证** —— 它真正防的是「整条产出链上任何一处让格→切片映射失真」
   (手改产物 / 换切片者 / 两块写成同一号),那些都不经过 `Slice()`。本件的两条负向夹具直接
   往 `NavChunkSliceCheck` 喂坏集合来验它,不证明「运行期真的会喂坏集合进去」。
6. **`ChunkTopology` 的既有零消费方问题只解开一半** —— `NavLattice` / 几何 codec 用了它,
   但 `ChunkActivator`(系统 6 的激活器)仍**零生产消费者**。

---

## §八 本轮自己的错误(三处,逐条如实登记)

### ① ⚠️ **漂移扫描器扫不到「拼接式」产物名** —— 首跑全量 EditMode 才暴露

`test_sweep_declaredArtifactNames_allTriaged` 按**字面量**正则 `"([A-Za-z0-9_]+\.cooked\.bytes)"`
枚举源码里声明的产物名。我把导航格产物名写成 `ProductName + "_" + chunkIndex + CookedSuffix`
(拼接)⇒ 那四条名字在源码里**没有字面量形态** ⇒ 扫描得 31 而钉定 35 ⇒ 红。

**修的是名字的形态,不是判据**:新增 `DataBakeMenu.WorldNavChunkCookedAssetNames`
四条**字面量**(给漂移门与组条目用),并加两条守护:
① 夹具 `test_ac5_registeredNames_matchParameterizedName` 钉「登记表 == 读侧参数化名字」;
② `BakeWorldNavChunksCore` 加硬失败门「几何推出的 chunk 数 == 登记表长度」。
`pinnedDeclared = 35` 一字未动。

### ② **`chunk` 三元组的下界写错** —— 首跑烘焙才暴露

`WorldNavChunkBinder.TryReadTriple` 原本所有三元组都要求每项 ≥ 1,而 **chunk 坐标的合法下界是 0**
(第一个 chunk 的原点就是 `(0,0,0)`)。首跑四条切片全部报「chunk[0] 须为 [1, …]」。
⇒ 给 `TryReadTriple` 加 `min` 形参:`world_size` / `chunk_size` 传 1,`chunk` 传 0。

**这条值得单记**:它是「把一个通用助手的下界套到语义不同的字段上」的形态 ——
助手本身没错,错的是调用点的假设。

### ③ 两处夹具自身的形状错误

1. `test_ac1_outOfRangeCell_returnsBlock` 里 `NavLattice(2,1,4, 2,1,2, …)` 的块内数组写成了
   `new int[8]`,而 chunk 是 2×1×2 = **4** 格 ⇒ 构造期长度门把**测试自己**拦下(报文清楚:
   「切片 chunk 0 的数组长度 = 8,须等于 chunk 格数 4」)。
2. `level_tool_consistency_test` 的「未实现项恰 5 条」在 C3/C6 移出后应变 3 —— 首跑全量才红。

### ④ M4 的变异锚点在两个方法里各出现一次 ⇒ 该条 ABORT

`ExportGeometry` 与 `RenderNavChunk` 都有
`.Append(Num(src.ChunkSizeX)).Append(", ")` 这段 ⇒ 锚点出现 2 次,驱动器按纪律 ABORT(不猜)。
加长锚点(带上后面的 `chunk_index` 行)使其唯一后重跑,结果 1 红。
**这是驱动器在保护我**:若它「取第一处」硬跑下去,变异的会是 `ExportGeometry`,
而报出来的红会指向另一个属性 —— 一场**打错靶的证伪轮**。

---

## §九 未做 / 归后续轮

- **`/code-review` 未跑** —— 归用户触发(与 story-001/002/003/004 同)。
- **`NavLattice` 的生产装配 / 按需装载** —— 归系统 6 的 chunk 激活轮(ADR-023 ⑥),见 §七.4。
- **S6(C1 / C2 / C2')** —— 本 epic 剩余最后一件。
