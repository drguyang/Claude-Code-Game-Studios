# level-tool story-003 实现轮 · 证据原件 —— `world_ecozones` / C4 / C5

**对象**:`production/epics/level-tool/story-003-world-ecozones-and-c4-c5.md`(AC-1 … AC-6)
**日期**:2026-10-29
**承接**:S20-20 挂标 **#3「C1–C6 七项未实现」**中的 **C4 / C5** 两条
**结论**:AC-1 … AC-6 全部有载体;**15 条变异全部转红(零假绿)**,但**预判 17 红 / 实得 31 红**
—— 两条超判逐条独立论证(§三.2),不是「多红了就算了」;
本轮另抓到并修掉**我自己的五处错误**,其中 ④ 让首跑全量回归红了 **20 条**(§八④)

---

## §一 落点

| # | 件 | 落点 | 性质 |
|---|---|---|---|
| 1 | **生态区声明式源 + C4 五项** | `unity/Assets/Editor.Tools.Level/EcozoneSource.cs` | 新建 |
| 2 | **生态区遮罩(视觉层单一源)+ C5 生成/比对** | `unity/Assets/Editor.Tools.Level/EcozoneMask.cs` | 新建 |
| 3 | **P0 医馆:两个区(共享边 `x = 8`)** | `unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs` | 修改 |
| 4 | **脚手架关卡:一个区** | `unity/Assets/Editor.Tools.Level/ScaffoldSecondLevel.cs` | 修改 |
| 5 | **导出**:`ExportEcozones` + `EcozonesSchemaVersion` + 「导出全类别」纳入 | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 修改 |
| 6 | **一致性检查**:C4 / C5 两条移出 `UnimplementedChecks`(**7 → 5**) | `unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` | 修改 |
| 7 | **分派表登记**:Ecozones 从「未实现」移到「已实现」 | `unity/Assets/Editor.Tools.Level/WorldDataSetKind.cs` | 修改(未实现 4 → 3) |
| 8 | **烘焙三件套** | `Editor.Tools.Bake/WorldEcozones{Bake,Binder,CookedWriter}.cs` | 新建 ×3 |
| 9 | **烘焙程序集引用** | `unity/Assets/Editor.Tools.Bake/Editor.Tools.Bake.asmdef` | 修改(+`Editor.Tools.Level`) |
| 10 | **运行期 codec** | `Gameplay.Presentation/WorldEcozonesCookedCodec.cs` | 新建 |
| 11 | **装载面接线**(1 address / `Load` 分派 / `Preload` / 磁盘兜底) | `AddressablesDataProvider.cs` · `DataCorePreloader.cs` · `DataCookedDisk.cs` | 修改 |
| 12 | **装配根**:`LoadEcozoneRegistry(IDataProvider)`(**public**,EditMode 直调) | `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 修改 |
| 13 | **产物**(作者态 + 烘焙) | `assets/data/world_ecozones.json`(263 B)· `unity/Assets/DataCooked/world_ecozones.cooked.bytes`(104 B) | 新建 ×2 |
| 14 | **data-core 组条目** | `unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset` | 修改(**28 → 29**) |
| 15 | **漂移门登记** | `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` | 修改(`pinnedDeclared` **28 → 29**) |
| 16 | **夹具** | `unity/Assets/Tests/EditMode/LevelTool/ecozone_polygon_check_test.cs` | 新建,16 条 `[Test]`,零写盘 |
| 17 | **五处装配夹具补正**(见 §八④) | `real_registry_patient_death_test.cs` · `real_registry_patient_outcome_fold_test.cs` · `w_trend_eps_vitals_end_to_end_test.cs` · `action_axis_reader_gate_test.cs` · `reader_wiring_gate_test.cs` | 修改 |
| 18 | **既有夹具一处靶前移**(见 §八③) | `level_source_generalization_test.cs` | 修改 |

**未碰**(故事卡 Out of Scope):C1 / C2 / C2' / C3 / C6(归 S5 / S6)· POI / 资源点(S4)·
`EcozoneOf` 的算法本体(`EcozoneRegistry.cs` 已有,本 story 只接装载面)·
生态区的名称 / 叙事内容 · 视觉层遮罩的**美术质量**

---

## §二 AC 逐条 → 载体 → 反证

夹具文件 = `unity/Assets/Tests/EditMode/LevelTool/ecozone_polygon_check_test.cs`(16 条)。

| AC | 判据(story 原文要点) | 载体 | 反证(改坏什么 ⇒ 哪条红) | 判定 |
|---|---|---|---|---|
| **AC-1** | `world_ecozones.json` 由工具导出,**零浮点**;`ecozone_id` 唯一;顶点是 `[x, z]` 二元组 | `LevelExporter.ExportEcozones`;`test_ac1_export_hasZeroFloatLiterals` · `…_verticesAreXZPairs_idsUnique` · `…_undeclaredEcozones_throws` | **M1**(x 写成 `1.0`)⇒ 2 红;**M2**(写成 `[x, 0, z]` 三元组)⇒ 1 红;**M3**(停用「未声明」守卫)⇒ 1 红 | ✅ |
| **AC-2** | **C4 五项退化全部硬失败,各一条负向夹具** | `EcozonePolygonCheck`;`test_ac2_overlap_…` · `…_fewerThanThreeVertices_…` · `…_selfIntersection_…` · `…_zeroArea_…` · `…_coincidentAdjacentVertices_…`(+ 边界半条 `…_sharedEdge_neighbouringZones_doNotFail`) | **M4**~**M8** 逐项停用对应判据 ⇒ **各恰 1 红**(五项判别面**可分**);**M9**(把边界点算作内部)⇒ 贴边那条红 | ✅ |
| **AC-3** | **C5 边界单一源**:遮罩由整数多边形生成;检「手画第二份」 | `EcozoneMaskGenerator.Generate` · `EcozoneMaskCheck.Verify`;`test_ac3_mask_generatedTwice_bitIdentical` · `…_inconsistentInjection_throws` · `…_sizeMismatch_throws` | **M10**(遮罩头掺 `Guid`)⇒ 确定性红;**M11**(`Verify` 直接 `return`)⇒ **2 红** | ✅ |
| **AC-4** | 烘成 `.cooked` 进 `data-core`;**组条目数 +1** | `WorldEcozones{Bake,Binder,CookedWriter}`;`test_ac4_addressInPreloadList_andProductExists`;**真 batch 实跑见 §五** | **M12**(从 `DataCoreAddresses` 删该条)⇒ 1 红。「组条目数 28→29」的判据在 Addressables 资产上(EditMode 无 locator)⇒ **实跑证据见 §五.3** | ✅ |
| **AC-5** | 空表被**装载路径**替换;**两个方向都测** | `CompositionRoot.LoadEcozoneRegistry`;`test_ac5_loadEcozoneRegistry_insideReturnsId_outsideReturnsNone` · `…_compositionRoot_wiresLoadEcozoneRegistry_notEmptyTable` | **M13**(装载面退回空表)⇒ 1 红;**M14**(装配根退回 `new EcozoneRegistry()`)⇒ 1 红 | ✅ |
| **AC-6** | `UnimplementedChecks` 长度 **7 → 5**,剩余仍是 C1 / C2 / C2' / C3 / C6 | `LevelConsistencyCheck.UnimplementedChecks`;`test_ac4_unimplementedChecks_stillSeven_andNoLongerSaysThisBatch` · `level_tool_consistency_test::test_levelTool_realSeed_…` | **M15**(把 C4 / C5 加回)⇒ **2 红** | ✅ |

### AC-4 的判别力边界(**如实划界,不借绿**)

「组条目数 +1」这条判据**没有专打变异**:它落在 Addressables 的 `data-core.asset` 上,
EditMode 无 locator ⇒ 判据只能用「源码里登记了 address + 盘上产物存在」两件来近似。
真正的「29 == 29」由**真 batch 实跑**给出(§五.3),两者不在同一个证据面上,不得互相顶替。

### C4 为何是「五项 × 五条变异」而不是「一条变异验五项」

story AC-2 逐字要求「五项**不得合并**成一条夹具」。若五条夹具共用一份构造、只改一个顶点,
停用任一项时其余四项的**同一批**夹具会一起红 ⇒ 看不出是哪一项失守。
本轮 M4~M8 **各停用一项、各恰红一条**,判别面可分的判据是实跑出来的,不是设计意图。

---

## §三 证伪轮 —— 15 条变异,**全部转红**,但预判低估了两条

**纪律**:一次变异只改**一个属性**;改 → 跑 → 还原**在同一脚本内**完成;
还原判据 = **SHA-256 逐字节**;全程**禁用 `git checkout` / `git stash`**;窗口内不读不编辑被变异件。
驱动器:`/home/gu/.claude/jobs/181888b1/tmp/falsify_story003.py`;原始日志 `falsify-story003.log`。

**靶场**:`-testFilter DaYiJingCheng.Tests.LevelTool`(52 条,基线 52/52 全绿)。
用整个命名空间而非单个类 —— AC-6 的判据落在 `level_source_generalization_test` 与
`level_tool_consistency_test`,不在新类里。

### 3.1 逐条结果

| ID | 属性 | 变异 | 预判红 | 实得红 | 判定 |
|---|---|---|---|---|---|
| M1 | 导出零浮点 | 顶点 x 写成 `1.0` | 1 | **2** | 超判(见 §三.2①) |
| M2 | 顶点是 `[x, z]` 二元组 | 写成 `[x, 0, z]` | 1 | 1 | ✅ |
| M3 | 未声明生态区硬失败 | 停用 `Ecozones == null` 守卫 | 1 | 1 | ✅ |
| M4 | C4 ① 内部重叠 | 停用 `InteriorsIntersect` | 1 | 1 | ✅ |
| M5 | C4 ② 顶点 < 3 | 停用顶点数判据 | 1 | 1 | ✅ |
| M6 | C4 ③ 自交 | 停用真交叉判据 | 1 | 1 | ✅ |
| M7 | C4 ④ 零面积 | 停用 shoelace 判据 | 1 | 1 | ✅ |
| M8 | C4 ⑤ 重合顶点 | 停用重合判据 | 1 | 1 | ✅ |
| M9 | C4 开集口径 | 边界点也算内部 | 1 | **14** | 超判(见 §三.2②) |
| M10 | C5 生成器确定性 | 遮罩头掺 `Guid.NewGuid()` | 1 | 1 | ✅ |
| M11 | C5 比对器判不同源 | `Verify` 直接 `return` | 2 | 2 | ✅ |
| M12 | 地址表登记 | 从 `DataCoreAddresses` 删该条 | 1 | 1 | ✅ |
| M13 | 装载面两方向 | `LoadEcozoneRegistry` 早退空表 | 1 | 1 | ✅ |
| M14 | 装配根接线 | 退回 `new EcozoneRegistry()` | 1 | 1 | ✅ |
| M15 | `UnimplementedChecks` 7 → 5 | 把 C4 / C5 加回 | 2 | 2 | ✅ |
| | **合计** | | **17** | **31** | |

**六份被变异文件的还原全部 SHA-256 逐字节相同**:

```
b3e9972757f0211c235ecbbc1910d4989a7b3783d703f4b06a7360719b9bd87b  EcozoneMask.cs
2f3e8b44e1bfed2f411e96237185442eaee43b1a11f0aba9f2f905c01bed25ba  EcozoneSource.cs
1446f746db0155af0798ea7b14c7881d22c10eb7f11db5bd6a574f77fe5a60a3  LevelConsistencyCheck.cs
fa6dc1bca3daf322fbfe0bde6bfc0eac36a39f440a94cae208acd4020388b442  LevelExporter.cs
0b7d9b38296f93474bfab9abdf674a8e2edf57574f452ac8613f981d7d219ac5  CompositionRoot.cs
82e7b62066cc0d939d1f305d8f266f67de97216a83697927583ea4fccfe2e14f  DataCorePreloader.cs
```

### 3.2 两条**超判** —— 先独立论证「它为什么该红」,再订正预判

> 承纪律:实得 > 预判时**不得**先改预判迁就结果,须先给出独立解释。

**① M1(1 → 2)。** 实得两条红:
`test_ac1_export_hasZeroFloatLiterals` + `test_ac1_export_verticesAreXZPairs_idsUnique`。
**为什么该红**:后者第 64 行是**逐字符**断言
`Assert.That(json, Does.Contain("\"vertices\": [[1, 1], [8, 1], [8, 14], [1, 14]]"))` ——
把 `1` 写成 `1.0` 直接改变了这个子串,**它本来就该红**。
我的预判只想到「浮点正则那条会抓」,漏了「同一条 AC 还有一条字面量子串断言」。
⇒ **预判错,夹具对**。两条都是 AC-1 的合法判据,超判方向安全(不是假绿)。

**② M9(1 → 14)。** 实得十四条红,横跨 `LevelTool` 全部三个夹具类。
**为什么该红**:`LevelExporter.cs:85 / 147 / 185 / 246` 四条导出入口**每条**都先调
`LevelConsistencyCheck.CheckOrThrow(src)`,而它在 `:159` 调
`EcozonePolygonCheck.TryCheck(src.Ecozones, …)` ⇒ **C4 是每一次导出的前置门**。
P0 真种子的两个区**共享 `x = 8` 这条边**(刻意如此,为的是让开集语义有对象)——
M9 把边界点算作内部 ⇒ 真种子当场成为「内部重叠」⇒ 所有走导出 / 一致性检查的夹具**全部**红。
⇒ 这条变异的**爆炸半径是设计使然**,不是夹具互相污染;
靶向夹具 `test_ac2_sharedEdge_neighbouringZones_doNotFail` 在其中且**只有它**是「该属性」的判据。
预判写 1 是**只算了靶向面,没算 C4 是全局前置门**。

⚠️ **副产物(值得单记)**:M9 顺带实证了一件事 —— **C4 真的接在导出链路上了**,
而且是**每一次**导出。若当初 C4 只接在某一条导出入口,这条变异不会红 14 条。

### 3.3 命中形态(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| **定义域收窄** ×5 | C4 五项退化各被排除 | 停用 ①重叠 / ②顶点<3 / ③自交 / ④零面积 / ⑤重合顶点 ⇒ 各自对应夹具红(M4–M8,各恰 1) |
| **共存/互斥** | 「贴边合法」与「内部重叠非法」互斥 | 把边界并入内部 ⇒ `test_ac2_sharedEdge_neighbouringZones_doNotFail` 红(M9) |
| **一次性/幂等** | 同输入两次生成逐字节相同 | 掺入非确定性 ⇒ `test_ac3_mask_generatedTwice_bitIdentical` 红(M10) |
| **阈值边界** | `UnimplementedChecks` 恰 5 条 | 加回两条 ⇒ 2 红(M15);**变少**由 `Is.EqualTo(5)` 反向覆盖 |

**Gate 结果**:PASS —— 15 条变异**全部转红**,零假绿;两条超判已逐条独立论证。

---

## §四 回归

**三元组(强制)**:截至时刻 `2026-10-29 02:22:27 CST`(EditMode)/ `02:23:02 CST`(PlayMode)
· 批次 **S3 收口终跑** · commit `3f446e70` + 工作树(38 项改动)

### 4.1 全量 EditMode

| 项 | 值 |
|---|---|
| total | **5770** |
| passed | **5735** |
| failed | **0** |
| inconclusive | 0 |
| skipped | 35 |

基线(S2 收口,`unity/Logs/final2-editmode.xml` @ `2026-10-29 01:36 CST` · commit `3f446e70` 同):
`5754 / 5719 / 0 / 0 / 35`。

**等式(可复算)**:`5770 − 5754 = 16` = **15** 条新 `[Test]`(`ecozone_polygon_check_test.cs`)
+ **1** 条漂移门的参数化用例 `drift[world_ecozones.cooked.bytes]`。**消失 0 条**。

```bash
# 复算等式两侧:对两份 XML 的用例名做集合差
python3 - <<'PY'
import xml.etree.ElementTree as ET
def names(p):
    return {tc.get("fullname") for tc in ET.parse(p).getroot().iter("test-case")}
a = names("unity/Logs/final2-editmode.xml"); b = names("unity/Logs/s3-final-edit.xml")
print("S2 =", len(a), " S3 =", len(b), " 新增 =", len(b - a), " 消失 =", len(a - b))
PY
```

**实跑输出**:

```
S2 = 5754  S3 = 5770  新增 = 16  消失 = 0
```

新增的 16 条(逐条):

```
15 × DaYiJingCheng.Tests.LevelTool.ecozone_polygon_check_test.*
 1 × DaYiJingCheng.Tests.Unit.DataPipeline.cooked_drift_all_artifacts_test.drift[world_ecozones.cooked.bytes]
```

### 4.2 ⚠️ **首跑红 20 条**(原文不删 —— 这是本轮最值得留的一段)

**首跑**(`unity/Logs/s3-editmode.xml` @ `02:08 CST`):`5770 / 5715 / **20** / 0 / 35`。
20 条**同一个报错** `MultiTableProvider 无 WorldEcozonesDataSet`,根因与修法见 **§八④**。

修复后 `DaYiJingCheng.Tests.DiseaseSimulation` 单跑 `240 / 240 / 0 / 0` 先行确认
(`unity/Logs/s3-fix2.xml` @ `02:11 CST`),再做全量终跑。

### 4.3 全量 PlayMode

| 项 | 值 |
|---|---|
| total | **238** |
| passed | **234** |
| failed | **0** |
| inconclusive | **2** |
| skipped | 2 |

基线(S2 收口 `238 / 234 / 0 / 2 / 2`)**逐项相同 ⇒ 零回归**。

⚠️ **退出码 = 2,不是 0** —— Unity 的 UTP 把 `Inconclusive` 计为失败码。
两条 inconclusive 是既有的显式占位:`U1Assump6GamepadFocusSpikeTest`
的 `test_u1assump6_judgement1_doubleTrigger_notRun` / `…judgement2_directionalSemantics_notRun`
(名字里就写着 `notRun`)。**基线同形**,非本轮引入。

> 📌 **纪律**:**判「跑过了」看 XML 的 `total > 0`,不看退出码** ——
> 本轮 §八⑤ 实测了一次「退出码 0 但 total 也是 0」(静默跳过),印证这条。

### 4.4 既有产物未被改动

见 §六.2:`world_buildslots` / `world_geometry` / `world_terrain` 三份作者态 JSON
与两份 cooked 产物**逐字节不变**(哈希 + mtime 双向印证)。

---

## §五 真 batch 实跑证据(三份,均 `-executeMethod` + `-quit`)

### 5.1 导出(`ExportP0ClinicToRepo`)—— `unity/Logs/s3-export.log`

四类数据集逐条落盘(原文摘录):

```
[关卡工具] 已导出 world_buildslots.json → …/assets/data/world_buildslots.json
[关卡工具] 已导出 world_geometry.json  → …/assets/data/world_geometry.json
[关卡工具] 已导出 world_ecozones.json  → …/assets/data/world_ecozones.json
[关卡工具] 已导出 world_terrain.json   → …/assets/data/world_terrain.json
```

⚠️ **同一轮里 story-002 的三份产物必须逐字节不变** —— 复算见 §六.2。

### 5.2 烘焙(`BakeWorldEcozones`)—— `unity/Logs/s3-bake.log`

```
[大医精诚] 烘焙完成:world_ecozones.cooked.bytes = 104 B,生态区 2 个,
          ConfigVersion = 0x80A6253B(data-core 组条目须经「确保 data-core Addressables 组」菜单)
```

### 5.3 `data-core` 组与 catalog(`BuildAddressablesContent`)—— `unity/Logs/s3-addr.log`

```
[大医精诚] data-core 组就绪:29 个条目已入组并打标签。
```

**复算命令(当场可跑)**:

```bash
python3 - <<'PY'
import re
t = open("unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset", encoding="utf-8").read()
print("组条目:", len(re.findall(r"- m_GUID:", t)))
print("地址数:", len(re.findall(r"m_Address: (.+)", t)))
print("含 world_ecozones:", "world_ecozones.cooked.bytes" in t)
PY
ls unity/Assets/DataCooked/*.cooked.bytes | wc -l
```

**实跑输出(2026-10-29 02:22 CST)**:

```
组条目: 29
地址数: 29
含 world_ecozones: True
29
```

⇒ 组内条目 **29** == 盘上产物 **29**,且新增项确在组内(AC-4 的「+1」)。

---

## §六 产物指纹与既有产物的零漂移

### 6.1 新增两份

```
c228df16b58a8cdc291decd412cac5bbe654de2595389c2817660e5ac85f6246  assets/data/world_ecozones.json      (263 B)
f20f0dcaabca218a77e58e82f4ec3871e7788e95a553b93882f7784ff5e990f6  unity/Assets/DataCooked/world_ecozones.cooked.bytes  (104 B)
```

作者态产物**零浮点字面量**(`grep -c '[0-9]\+\.[0-9]\+'` = **0**),顶点是 `[x, z]` 二元组:

```json
{"ecozone_id": 0, "name": "clinic_ward",     "vertices": [[1, 1], [8, 1], [8, 14], [1, 14]]},
{"ecozone_id": 1, "name": "herb_courtyard",  "vertices": [[8, 1], [14, 1], [14, 14], [8, 14]]}
```

两区**共享 `x = 8`** —— 这不是巧合,是**为了让「贴边不误报」在真种子上有对象**
(否则 M9 那条边界判据只能靠手造多边形证明,真产物从未被检验)。

### 6.2 story-002 的三份产物**逐字节未变**

| 产物 | SHA-256 | 字节 |
|---|---|---|
| `assets/data/world_buildslots.json` | `1c946bff60177ff294f4e3bcf4a235e67b4e5067ba07db105047d57762074c6d` | 1249 |
| `assets/data/world_geometry.json` | `6bb382718a0c59c053ab791957d6a16aa5ae5dccb0211b7644b09ad99b8d2de0` | 976 |
| `assets/data/world_terrain.json` | `2802ae72503e01ee44f5ae8657b0317e7bdcb2698a325d02e333d9562ccfa8fd` | 1129 |
| `DataCooked/world_geometry.cooked.bytes` | `e5ec78413126eccdf95cbd4b555c0f8e47c2bde20108cb154d85cc2f73a2c2f8` | 1064 |
| `DataCooked/world_terrain.cooked.bytes` | `673c27012be8c8dd6e2e03a3f9f591565f53619200f825f941afbe7346e00206` | 1096 |

`world_buildslots.json` 的哈希同时是 `level_source_generalization_test.PreRefactorBuildSlotsSha256`
的字面量 ⇒ 它若变了,AC-3 的金标准夹具会当场红。

⚠️ 三份**作者态** JSON 的 mtime 被 `ExportP0ClinicToRepo` 刷新到 `02:01`(它是「导出全类别」,
四类一起写),**故 mtime 不构成「未改动」的证据** —— 哈希才是。
两份 **cooked** 的 mtime 仍是 S2 的 `01:13` / `01:14`(S3 没有重烘它们)⇒ 交叉印证。

### 6.3 复算命令

```bash
sha256sum assets/data/world_{buildslots,geometry,terrain,ecozones}.json \
          unity/Assets/DataCooked/world_{geometry,terrain,ecozones}.cooked.bytes
```

---

## §七 本件证明不了的事(禁借绿)

1. **生态区数量 / 形状是否合理** —— 本 story 只证明「有多边形、合法、能装载」;
   `clinic_ward` / `herb_courtyard` 的**划分**是关卡设计的事。
2. **C5 的「乙路」(与已存在的视觉层遮罩资产逐像素比对)未跑** —— 视觉层生态区遮罩资产
   **尚不存在**(美术轮),甲路(生成器是唯一写入面)已落地,乙路无从验证。
   故事卡 §诚实边界 2 已预置此条,此处如实登记。
3. **`EcozoneOf` 的跨机器逐位一致性** —— 归 ADR-012 的黄金夹具矩阵,不在本 story。
4. **生态区与天气 / 病人 AI 的行为联动** —— 本 story 只让 `EcozoneOf` **有真值**;
   消费侧(5 时间天气、13 病人 AI 的 `KnowsClinic`)行为是否正确归各自实现轮。
5. **视觉层遮罩的美术质量** —— 故事卡 Out of Scope 明写。

---

## §八 本轮自己的错误(五处,逐条如实登记)

### ① `Editor.Tools.Bake.asmdef` 里写了 `//` 注释 ⇒ JSON 解析失败

asmdef 是**严格 JSON**,不认注释。实得 `JSON parse error: Invalid value`。
⇒ 重写为纯 JSON,把理由搬进 `WorldEcozonesBinder.cs` 的文件头。
**这条此后每次加程序集引用都会撞,值得记。**

### ② `LoadEcozoneRegistry` 的文档注释里出现重复的 `**`

一次编辑残留。修掉。**危害等级低,但它会在 XML 文档生成里变成字面量。**

### ③ `test_ac3_unimplementedKind_throwsNamingOwningStory` 的靶**必须随实现进度前移**

story-002 时它打在 `Ecozones`(那时未实现);story-003 把 Ecozones 实现了 ⇒ 该测从「绿」变「红」。
⇒ 靶前移到 `Poi`(归 S4),并在注释里写清「**每条实现轮都得把它前移一格,判据形态一字未变**」。
这不是错误本身,是**错误的形状** —— 一个「随进度腐烂」的夹具若不改靶,会以「红」的形式
逼人把它删掉。

### ④ ⚠️ **首跑全量回归红 20 条 —— 我把生态区表变成了装配期的隐式必需依赖**

**现象**:`test_total=5770 passed=5715 failed=20`,20 条全部同一个报错:

```
System.InvalidOperationException : MultiTableProvider 无 WorldEcozonesDataSet —— 本测不该问到它(真问 = 接线错)
```

**根因**:`LoadEcozoneRegistry(dataProvider)` 在 `dataProvider != null` 时**无条件**即时 Load
生态区表(刻意不设 `catch` —— 静默降级到 `None` 正是本仓反复记的假绿形态:GDD EC-6-10 明写
`None` 是**合法结果**,降级后「看起来完全正常」)。
而 5 个测试夹具的 `MultiTableProvider` 只登记了各自关心的表 ⇒ 装配当场抛。

**为什么不是「夹具不完整」**:那个 provider 的注释写得很明确 ——
「缺表 ⇒ 抛,不返回 default —— 装配问到了没准备的表**本身就是接线错的信号**」。
⇒ 是**装配真的多读了一份之前不读的数据**,夹具的报警是对的。

**修复**:按仓内既有先例(处方表 / 处置轴 / 病种表接真时都是同一形态),
在 5 个夹具的 provider 里补 `.Add(DataCookedDisk.LoadWorldEcozones())` 并写明理由:

- `real_registry_patient_death_test.cs`
- `real_registry_patient_outcome_fold_test.cs`
- `w_trend_eps_vitals_end_to_end_test.cs`
- `action_axis_reader_gate_test.cs`
- `reader_wiring_gate_test.cs`

**验证**:`DaYiJingCheng.Tests.DiseaseSimulation` = **240/240/0/0** 全绿(见 §四)。

⚠️ **这条的连带后果要记住**:生态区表从此是**装配期的必需输入**。
将来任何「用 provider 装配 `CompositionRoot`」的新夹具,都必须登记它 ——
否则会以「同一个报错」红,而报错文本指向的是 provider 而非真实原因。

### ⑤ `-testFilter` 不吃**逗号分隔**的两个命名空间

`-testFilter "A,B"` 实得 `filters[0] = `(空)+ `No tests were executed`,**静默 0 条**,
退出码仍是 `0`。
⚠️ **这正是本仓记过的「扫描不到被测装配 ⇒ 症状是跳过不是绿」**:
如果只判退出码,这一轮会被当成「跑过了」。
⇒ 追加一条纪律:**判「跑过了」须看 XML 的 `total > 0`,不是退出码。**

---

## §九 未做 / 归后续轮

- **`/code-review` 未跑** —— 归用户触发(与 story-001 / story-002 同)。
- **C5 乙路**(视觉层遮罩逐像素比对)—— 视觉层资产不存在,归美术轮 + S6。
- **C1 的生态区半边**(占用格 ⊆ 生态区)—— 归 S6。
- **`world_ecozones` 的生态区名称 / 叙事** —— 归 `world-ecozones` GDD / 叙事轮。
