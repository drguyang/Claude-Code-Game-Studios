# 实现证据原件 —— sprint-17 批 1（S17-1 / S17-3 / S17-4）

**日期**:2026-10-21
**批次**:sprint-17 批 1(3 件,CAPACITY-MODEL §3.2 并发上限 3)
**执行**:`/dev-story` 三件并发派发 → 编排方串行验收(batch 独占工程)

---

## 一、基线溯源三元组(强制)

| 项 | 值 |
|---|---|
| **基线截至时刻** | 2026-10-21 05:08 CST |
| **基线批次** | sprint-16 收口记账轮(引自 `sprint-17.md` §溯源) |
| **基线 commit** | `6855ba7`(本批起于 `1dde232`) |
| **基线 EditMode** | `4086 / 4050 / 0 / 36` |

**本轮实测**(引用本文件即视为引用下列三元组):
截至 `2026-10-21` · sprint-17 批 1 验收轮 · 工作树基于 `1dde232` + 本批未提交改动

---

## 二、交付清单

### S17-1 — clinic-002 房间析出与连通规则

| 文件 | 行数 | 职责 |
|---|---|---|
| `unity/Assets/Sim/Clinic/IPlacedStructureReadView.cs` | 55 | 只读输入视图(零写成员,类型层面断系统 23 写路径) |
| `unity/Assets/Sim/Clinic/ClinicRoomId.cs` | 75 | 房间标识 + `ROOM_NONE (0,0)` 哨兵 |
| `unity/Assets/Sim/Clinic/ClinicRoomExtraction.cs` | 296 | 4 邻接并查集析出 + 确定性 room id + 缺口哨兵 |
| `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` | — | 21 测 <br>🔁 **2026-10-21 订正:实测 22 测**(`grep -c "\[Test\]"` 与 `grep -c "public void test_"` 同为 22)。`review-sprint17-batch1-2026-10-21.md:211` 记的 22 才与盘上一致。原文保留作沿革。 |

- **门 A 实测**:`grep -n 'UnityEngine\|UnityEditor' Sim/Clinic/*.cs` 命中项**全部是注释**,零类型引用;零 `float`/`double`/`Math.Round`。
- **甲案代理判据**:按 story Implementation Notes 第 7 条落,代码注释逐字登记「代理而非等价物、代理更宽」。
- **两条陷阱均规避**:未调用 `GetInstanceAt`(改用视图的 `GetOccupiedCells`);输入经自定义只读接口注入。

### S17-3 — tutorial-001 内容 schema + 六项构建期校验

| 文件 | 行数 | 职责 |
|---|---|---|
| `unity/Assets/Editor.Tools.Bake/TutorialContentSchema.cs` | 265 | 键白名单、六步闭集、170/2 行上限、`Checkpoint` 例外、外置词族表、`ITutorialDiseaseNameSource` + `TutorialDiseaseScanOutcome`(**唯一定义处**) |
| `unity/Assets/Editor.Tools.Bake/TutorialContentBinder.cs` | 960 | 阶段 2 绑定 + 六项校验(聚合 throw,错误串 `[①]`…`[⑥]` + 条目定位)+ `ITutorialMediaKeySource` |
| `unity/Assets/Editor.Tools.Bake/TutorialContentCookedWriter.cs` | 243 | 确定性编码,行序 = 闭集序/order/id 全序复合键 |
| `unity/Assets/Editor.Tools.Bake/TutorialContentBake.cs` | 95 | 仓根装载器,零 UnityEngine/Addressables,EditMode 可直驱 |
| `unity/Assets/Gameplay.Presentation/Tutorial/TutorialContentTable.cs` | — | 运行期只读表 + `TryDecode`(AC-5 接线) |
| `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`(**改**) | — | `TutorialContentCookedAssetName` + `[MenuItem] BakeTutorialContent()` |
| `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs`(**改**) | — | address 常量 + `Load<T>` 分支 + `Preload()` + E-13 硬失败分支 + 「未知类型」错误串订正 |
| `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs`(**改**) | — | `DataCoreAddresses` 补第 16 项 |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_content_validation_test.cs` | — | 37 测 |
| `unity/Assets/Tests/EditMode/Tutorial/tutorial_content_runtime_table_test.cs` | — | 10 测 |

- **之八(BLOCKING)落地**:比较器唯一出口 `ScanForTokens` 显式 `StringComparison.OrdinalIgnoreCase`;扫描结果为 `TutorialTextScanResult`(结构化 `HitCount` + 命中列表),**失败与否由计数派生,不由字符串派生**。
- **产物已真落盘**:`unity/Assets/DataCooked/48_tutorial_content.cooked.bytes` = **1 843 B**,`ConfigVersion = 0x71D8182F`(6 步 / 4 口述 / 6 示范 / 1 纸),已入 data-core 组(16 条目)。

### S17-4 — casebook-002 病例列表全序纯函数

| 文件 | 行数 | 职责 |
|---|---|---|
| `unity/Assets/Gameplay.UI/Casebook/CaseListOrdering.cs` | 290 | `CaseSortKey`(三元逐分量 `CompareTo`)+ `OrderedList` / `BuildGroups` / `OrderSheet` |
| `unity/Assets/Tests/EditMode/Casebook/Casebook.Tests.asmdef` | — | **新建装配**(共享 `Sim.Contracts.Tests` 不引 `Gameplay.UI`) |
| `unity/Assets/Tests/EditMode/Casebook/casebook_sort_test.cs` | — | 16 测 |

- **禁 `a-b`** 已落实:比较器逐分量 `CompareTo`,零减法比较。
- **反射断言**:无 display-order/index 字段、无静态可变状态,两条**均带「空集绿守卫」**。

### 跨件改动(门要求,非新功能)

| 文件 | 改动 | 缘由 |
|---|---|---|
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | 清单增 `Casebook.Tests` | b3 清单封闭性(ADR-025 §④) |
| `docs/architecture/adr-025-contract-assembly-manifest.md` §① | 增 `Casebook.Tests` 行 | 同批回写(该 ADR 自陈的强制配对) |
| `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs` | `pinnedDeclared` 15→16 + `BakedArtifacts` 增第 16 份 | 新产物须登记 |

---

## 三、测试对账

| 项 | 值 |
|---|---|
| 基线 | `4086 / 4050 / 0 / 36` |
| 现值 | **`4171 / 4135 / 0 / 36`** |
| **Δtotal = Δpassed** | **+85 / +85** ✅ |
| 新增(按全名枚举) | Clinic 21 + Casebook 16 + Tutorial 校验 37 + Tutorial 运行期 10 + `drift[48_tutorial_content…]` 参数化 1 = **85** |
| **消失** | **0**(Δ 恰等于新增 ⇒ 无「新增 N 同时坏 N」) |

**验证命令**:
```
Unity -batchmode -nographics -projectPath unity -runTests \
  -testPlatform EditMode -testResults unity/Logs/s17-b1-editmode.xml \
  -logFile unity/Logs/s17-b1-editmode.log
```

---

## 四、证伪轮(3/3 全红,`exactRed` 逐例精确)

> **纪律**:预期在跑之前写下(`/tmp/fz-out/EXPECT.md`,时间戳先于运行);还原**禁用** `git checkout`/`git stash`,
> 改用「备份 → 单点变异 → 跑 → `cp` 还原 → SHA-256 逐字节比对」;变异窗口内不读被变异文件。

| 例 | 变异 | 预期转红 | 实际转红 | 判定 |
|---|---|---|---|---|
| **F1** | `ClinicRoomExtraction.CountComponents` 增对角 `(+1,+1)` 并查 ⇒ 8 邻接 | `test_diagonal_pair_returns_room_none` | 同名 1 条 | ✅ 恰中 |
| **F2** | `TutorialContentBinder.ScanForTokens` 的 `OrdinalIgnoreCase` → `CurrentCultureIgnoreCase` | `test_dinf_ordinalScan_isCultureIndependent` · `test_dinf_scanResult_isIdenticalAcrossCultures` | 同名 2 条 | ✅ 恰中 |
| **F3** | `CaseSortKey.CompareTo` 的 `OpenedTick.CompareTo` → `(int)(a-b)` | `test_orderedList_tickAtInt64Extremes_ordersWithoutOverflow` · `test_sortKey_compareAtInt64Extremes_isAntisymmetric` | 同名 2 条 | ✅ 恰中 |

- 三例均 `EXPECTED_NOT_RED: []` / `UNEXPECTED_RED: []`。
- 三例 `total` 与 `skipped` 均 = `4171 / 36`(与绿基线同)⇒ **排除「变异破坏编译致少跑」的假存活形态**。
- 三份文件 `RESTORE_BYTE_IDENTICAL=YES`:
  - `ClinicRoomExtraction.cs` `82522a09…ede5b`
  - `TutorialContentBinder.cs` `2a404d81…349337`
  - `CaseListOrdering.cs` `1ca44282…aaf4bad`

---

## 五、如实登记的缺口(禁借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| **S17-3 AC ④「病名闭集词表扫描」** | ✅ **已解除（2026-10-21 修整轮）** | 原记 NOT-RUN 的原因是**生产路径零实现者**，不是缺判据。现补：`assets/data/disease_names.json`（8 病 / 12 显示名，逐条转录 GDD §R3，所有者 = 系统 9）+ `TutorialDiseaseNameSource`（缺席 ⇒ false = NOT-RUN；损坏 / 空表 ⇒ 硬失败）+ `DataBakeMenu` 恒定注入并把 ④ 三态打进日志。种子内容现 **④ 真跑且 Clean**（`AllSixChecksExecuted == true`）；判别力经 M5–M8 四例反证，见 `review-sprint17-batch1-2026-10-21.md` §六之二 |
| **S17-3 AC ② 外部半边(cueId / clipKey)** | **NOT-RUN** | `audio_events.json` 无 `teach_*` 键;全库无 13 示范片段表。`CueIdTableReady` / `ClipKeyTableReady` = false |
| **S17-3 AC-3「运行期零解析器」** | 以 EditMode 等价落地 | 断言运行期程序集引用面 + `Count > 0` 守卫 + 判别力对照;**非 IL2CPP 符号面** |
| **S17-4 AC-3「前缀性」** | **有条件成立** | `OrderedList(A)` 是 `OrderedList(B)` 的连续前缀,**仅当**追加项的三元组 ≥ 既有全部键(由 `IIdAuthority` 单调发号 + 新案开案 tick 更大保证)。反例已单独成测 `test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix`,**未记为无条件全绿** |
| **S17-4「存档字节流无顺序残留」** | 未覆盖 | 本件无序列化路径,`casebook-001` 未实现 |
| **S17-1 生产侧 adapter** | 缺席 | `StructureInstanceRegistry` 无公开枚举器且本轮禁改系统 23 ⇒ 只能注入替身 |
| **ADR-012 IL2CPP 三格** | **NOT-RUN** | 本机仅 Mono <br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。 |

---

## 六、本轮自己抓到并修正的错误(如实记)

1. **首轮编译失败**:`TutorialContentBinder.cs` 与 `TutorialContentSchema.cs` **各声明了一份**
   `ITutorialDiseaseNameSource` / `TutorialDiseaseScanOutcome`(CS0101 / CS0111)。三份 agent 报告均自陈
   「未编译」⇒ 该错误只有真跑才会暴露。修正 = 删除 Binder 中的副本,保留 Schema 为唯一定义处。
2. **首轮 `exit code 0` 是假象**:编译失败时 Unity 仍退出 0 且**不产出 XML** ⇒ 判据必须读 XML,
   不能读退出码。这是本仓已登记的「harness 的 exit 0 可能是外层 echo 的码」同型形态。
3. **`DataCoreAddresses` 漏第 16 项**:由 `provider_wiring_gate_test` 门④ 与
   `test_preloader_list_sources_matchPreloadedProducts` **双门捕获**,非自查发现 ⇒ 印证该门有效。

---

## 七、未做的事

- **未 commit**(待用户指令)。
- **未勾任何 AC 复选框** —— 归 `/story-done` 的验收动作,本原件只提供判据。
- **未跑 `/code-review`**。
- `TutorialContentBinder.cs` 960 行、单方法可能超 40 行标准 —— 属 `/code-review` 的评审面,本件未处理。
