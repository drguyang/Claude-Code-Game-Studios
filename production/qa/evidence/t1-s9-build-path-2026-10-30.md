# T1 · S9 —— 构建路径(`CostTableCookedWriter` / `CostTable53Bake` / `CostTableGate` / 探针轮)

- **对象**:`ADR-035 §Migration Plan` 的 **S9**(2026-10-30 用户裁定「加 S9」)
- **日期**:2026-10-30
- **动机**:V-5 / V-6 / V-7 的字面判据都是「**构建** Failure」,而 `53_cost_table.json` 此前
  **零读方** —— 无 CookedWriter、无 `DataBakeMenu` 入口、无构建前门 ⇒ **不存在能被"构建"执行到的路径**,
  三条判据只能记「绑定器层拒收」。

---

## 一、交付面

| 件 | 落在 | 作用 |
|---|---|---|
| `CostTableCookedWriter.cs` | `Editor.Tools.Bake` | deterministic 编码(20 字节头 + `row_count` + 逐行 4×i32) |
| `CostTable53Bake.cs` | `Editor.Tools.Bake` | 仓根装载:两源读取 → `CostTableBinder.Bind` → **外键解析** → 编码 → `ConfigVersion` |
| `CostTableGate.cs` | `Editor.Tools.Bake` | `IPreprocessBuildWithReport`;构建前任一条失败 ⇒ `BuildFailedException` |
| `Cli/CostTableProbeBuildCli.cs` | `Editor.Tools.Bake/Cli` | 探针轮的**最小 player 构建入口**(全仓此前零 `BuildPipeline.BuildPlayer`) |
| `DataBakeMenu.BakeCostTable()` | `Editor.Tools.Bake` | 菜单写盘链路(产物 `53_cost_table.cooked.bytes`) |
| `cost_table_bake_test.cs` | `Tests/EditMode/MedConsequences` | **10 条** |

### 四处实现期判断(ADR 未写,本轮就地定)

**判断 ①:`CostTableGate` 住 `Editor.Tools.Bake`,不住 `Editor.Tools.Gates`。**
ADR 的 S9 行原文写「`AssemblyGates` 新增 `CostTableGate`」—— **不可照字面实现**:
程序集边 `Editor.Tools.Bake → Editor.Tools.Gates` **已存在**(Bake 的 asmdef 引用了它),
反向再引 = **循环引用**,Unity 直接拒绝编译;而门必须调用 `CostTableBinder`(住 Bake)。
⇒ 门自带 `IPreprocessBuildWithReport`(Unity 按反射发现,与 `AssemblyGates.BuildGate` 并列),
**不经** `AssemblyGates.RunAll()`。代价:门的清单位于两处 —— 已在 `CostTableGate.cs` 文件头注明。
ADR 该行已加「🔁 实现期订正」注(**原文不删**)。

**判断 ②:cooked 产物里是**整数** `injury_id`,不是作者态字符串。**
`CostTable53.Row.InjuryId` 是 `int`(S4 已落),而 ADR-014 要求「玩家构建零 JSON 解析器」
⇒ 字符串 → 整数的解析必须发生在**烘焙期**。解析复用 `DiseaseRegistryBinder`(与 Binder 内部同一入口,
**不另写第二个解析器**),并**不改 `CostTableBinder` 的判据面**(S3 已结案)。

**判断 ③:`ConfigVersion` 由**两个源**一起派生。**
本表同时依赖 `53_cost_table.json` 与 `disease_registry.json`。若只哈希前者,
「只改注册表」不会让版本号变 ⇒ 与存档头的比对(ADR-010 §七)对注册表改动是瞎的。
夹具 `test_configVersion_coversRegistryToo` 钉这一条。

**判断 ④:空表合法,cooked 里 `row_count = 0`。**
与 `DelayTableCookedWriter`「空即抛」**刻意不同**:那张表空 = 53 永不发后果(静默失败);
本表空 = 代价机制未启用(ADR-035 §⑥ / S2 的已裁惰性形态)。

---

## 二、夹具(10 条,`cost_table_bake_test.cs`)

| 判据 | 夹具 |
|---|---|
| S2 空表可烘焙、`row_count = 0` | `test_realSkeleton_bakesEmptyTable` |
| 同源两次烘焙逐位一致(ADR-014 §二) | `test_determinism_sameInputsSameBytes` |
| 行序按 `(harm_kind, band)` 规范化 | `test_rowOrder_isCanonicalNotInputOrder` |
| 外键在烘焙期解析成注册表整数 id | `test_injuryId_isResolvedToRegistryInteger` |
| ConfigVersion 覆盖注册表 | `test_configVersion_coversRegistryToo` |
| 真实仓过门 | `test_gate_realRepo_isGreen` |
| 仓根缺失 ⇒ 门**返回**错误不抛 | `test_gate_missingSources_returnsErrorNotThrow` |
| **V-5** 门层:致死行 ⇒ 红且点名 N-1 | `test_gate_lethalRow_isRed_andNamesN1` |
| **V-6** 门层:`rows` 非空 + `enabled` 空 ⇒ 红 | `test_gate_rowsWithoutEnabledKind_isRed` |
| **V-7** 门层:启用 `overdose` ⇒ 红(点名 `TD-sim-02`) | `test_gate_overdoseEnabled_isRed` |

> ⚠️ **首跑 1 红,是夹具的错不是实现的错**:`test_rowOrder_isCanonicalNotInputOrder` 首版比了**整串 cooked 字节**
> —— 而两种行序是两份不同的源文本,`configVersion` 本来就该不同。改为比**载荷**。
> 这条留在夹具注释里,免得下一个人照「整串相同」再写一遍。

---

## 三、探针轮(`CLAUDE.md` §构建前门 · **S9 的核心判据**)

> ⚠️ 基线绿(`Result: Success`)**不等于门跑了** —— 门链无错时零输出,无法区分
> 「门跑了且通过」与「门没被调用」。下面五次构建把这一层补掉。

| # | 输入 | 构建结果 | 判据 |
|---|---|---|---|
| 0 | 未改动 | `Build Finished, Result: Success.`(30.7 s) | 基线 |
| 1 | **门里插无条件失败项** | `Result: Failure` | 探针串在日志、栈帧 = `CostTableGate/PreprocessBuild:OnPreprocessBuild`、`utp` 停在 **`Preprocess Player`** |
| 2 | **V-5**:注入指向 `lethal=true` 伤情的行 | `Result: Failure` | 报文 `rows[0] injury_id「INJ_HEMORRHAGE」的 lethal = true —— **N-1**` |
| 3 | **V-6**:`rows` 非空 + `harm_kind_enabled` 空 | `Result: Failure` | 报文点名 R4 /「静默跳过」 |
| 4 | **V-7**:`harm_kind_enabled` 含 `overdose` | `Result: Failure` | 报文点名 `TD-sim-02` |
| 5 | **还原后同一路径** | `Result: Success` | **还原即绿**(禁只断「会红」) |

**还原判据**:`CostTableGate.cs` `561dacf3…` · `53_cost_table.json` `eafa6494…` ·
`disease_registry.json` `023b0b36…` —— 全部 **SHA-256 逐字节一致**,无 `FALSIFY-` 残留,
`git status assets/data/` 干净。

**探针轮的归因三件套(构建 #1)逐项**:

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串出现在**构建日志**里 | `[PROBE] ADR-035 S9 —— CostTableGate 探针轮(无条件失败)`(日志 `:1017`) |
| 2 | 栈帧给出该门的 `OnPreprocessBuild` | `CostTableGate/PreprocessBuild:OnPreprocessBuild … CostTableGate.cs:114` |
| 3 | 构建**因它失败** | `BuildFailedException` + `utp steps = [{"description":"Preprocess Player"}]` 即止 |

---

## 四、实跑中撞到的四处(如实登记)

| # | 现象 | 处置 |
|---|---|---|
| 1 | 产物放 `unity/Library/…` ⇒ `ArgumentException: Invalid build path … The 'Library' directory is an internal work directory of Unity` | 改落系统临时目录;不污染仓库、不需要 `.gitignore` 条目。**实测,非预判** |
| 2 | 全仓**零** `BuildPipeline.BuildPlayer`(CI 也只跑测试)⇒ `IPreprocessBuildWithReport` 此前**没有任何执行者** | 新增 `CostTableProbeBuildCli` —— 这是 S9「给门一条可执行的构建路径」的字面宾语 |
| 3 | 全量回归 1 红:`cooked_drift_all_artifacts_test` 的产物名哨兵`pinnedDeclared = 36`,而新增产物名后实测 **37** | **这是本轮改动的正确后果,不是误报**。处置:**登记进 `BakedArtifacts`(带现烘委托)+ 哨兵 36 → 37 + 真实落盘** `unity/Assets/DataCooked/53_cost_table.cooked.bytes`(24 B = 20 头 + 4 字节 `row_count = 0`;`ConfigVersion = 0x8C681871`) |
| 4 | 修完 ③ 再跑,**第二道门又红**:`nav_chunk_slicing_test.test_ac5_allChunksBaked_andRegisteredInDataCoreGroup` —— 「组条目 36 ≠ 盘上产物 37」 | 同一条纪律的**另一面**:盘上多一份就必须进 `data-core` Addressables 组。跑「确保 data-core Addressables 组」菜单 ⇒ 日志「37 个条目已入组」;`grep -c -- "- m_GUID:"` = **37** = `ls DataCooked/*.cooked.bytes \| wc -l` = **37** |
| 5 | 修完 ④ 再跑,**第三道门又红**:`CatalogFreshnessGateTest.test_catalog_containsEveryDataCoreAddress` —— 「组声明 37 条,catalog 只有 36 条,缺 `53_cost_table.cooked.bytes`」 | 跑「构建 Addressables 内容(BuildPlayerContent)」菜单 ⇒ 日志「37 条,与盘上 37 个产物一致」。⚠️ catalog 落 `unity/Library/`(**gitignored、机器本地**)⇒ 新检出后须重跑该菜单;测试在 catalog 缺失时走 `Inconclusive`(不是绿) |

> ⚠️ **③④⑤ 是同一条纪律的三道门**,不是三个独立缺陷:31 条 → 32 条产物,三处登记面各有一道机械门
> (源码声明表 / Addressables 组 / 构建出的 catalog)。**它们依次咬出来**,说明这套门确实在工作 ——
> 也说明「加一份产物」的真实成本是**三处登记 + 一次重烘 + 一次 catalog 重建**。

> ⚠️ 第 3 条**为什么不走 `KnownUnbaked`**:该表是「欠账」表,而本产物**能且已**落盘。
> 登记成已烘还多做了一件事 —— 漂移门从此钉住「`CostTableGate` 验的表 **==** 出货的表」。

> ⚠️ 第 3 条也解释了本件与 S9 字面的关系:S9 只说要「有构建路径」,没说要产出 cooked;
> 但**产物清单哨兵**把「声明了产物名就必须登记」变成了硬约束 ⇒ 要么登记、要么把菜单里的字面量去掉。
> 前者才是「真的有构建路径」。

---

## 五、回归

**基线溯源三元组**:截至时刻 `2026-10-30 07:27 CST` · 批次 `S9 收口全量` · commit `37f9f154`+工作树

| 套件 | total | passed | failed | skipped |
|---|---|---|---|---|
| EditMode | **6029** | **5992** | **0** | 37(inconclusive 0) |
| PlayMode | **238** | **234** | **0** | 2(inconclusive 2) |

- 相对 S6 批(`6018 / 5981 / 0 / 37`):**+11 跑 / +11 过 / 零回归**。
  等式 `6029 − 6018 = 11` 的**逐项来源**(不是估的,从 `s9c-full.xml` 数出来):
  `CostTableBakeTest` **10 条** + `drift[53_cost_table.cooked.bytes]` **1 条**
  (后者是 `BakedArtifacts` 的 `[TestCaseSource]` 参数化用例 —— 新增一条登记就多一条用例,
  **不用手写**)。复算:`python3 -c` 数 XML 里 `CostTableBakeTest` 与含 `53_cost_table` 的 fullname。
- 本批三连红(③④⑤)的修复过程**完整保留在 §四**,不是「一次跑绿」。
- ⚠️ **过程数**:`s9-full` 首跑 `1 红`(产物哨兵)→ `s9b-full` 次跑 `1 红`(data-core 组)→
  `s9c-full` 三跑 `0 红`。三次的 XML 都留在 `unity/Logs/`。

## 六、本件**不**主张

| 项 | 状态 |
|---|---|
| `CostTable53` 已接进运行路径 | ❌ **仍零读方**:没有装配点调用 `CreateTable`、cooked 不入 Addressables(归 **T2**) |
| 「永不致死」已获证明 | ❌ `N-1` 的可行集今日仍为空(`TD-sim-03`);本件只证「想塞致死行,构建就红」 |
| 构建门覆盖面已完整 | ⚠️ 只覆盖**代价表**这一面;其它数据表的构建期校验仍只在烘焙菜单里 |
| CI 会跑这个门 | ❌ CI **不产出 player**(`tests.yml` 只有测试)⇒ 本门今日只在**本机构建**时生效;要进 CI 须另开构建作业 |
| `AssemblyGates` 清单仍单一 | ⚠️ 门现在分居两处(`AssemblyGates.BuildGate` + `CostTableGate`),见判断 ① |
