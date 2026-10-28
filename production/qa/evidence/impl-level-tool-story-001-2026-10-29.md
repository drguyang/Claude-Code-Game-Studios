# level-tool story-001 实现轮 · 证据原件 —— 关卡源泛化 + 数据集类别

**对象**:`production/epics/level-tool/story-001-level-source-generalization.md`(AC-1…AC-5)
**日期**:2026-10-29
**裁定出处**:`production/sprints/sprint-20.md:255` —— S20-20 挂标 **#4「关卡工具只服务一个关卡」**;
承接者 = 本 story(epic 于 2026-10-28 由用户裁定「推翻『不立 epic、不排期』」而立)
**结论**:**AC-1…AC-5 全部有载体;12 条变异逐条转红;本轮另抓到并修掉三处我自己的错误(见 §六)**

---

## §一 落点

| # | 件 | 落点 | 性质 |
|---|---|---|---|
| 1 | **数据集类别闭集** `WorldDataSetKind`(7 类)+ `WorldDataSets`(分派表 / 未实现表 / 命名) | `unity/Assets/Editor.Tools.Level/WorldDataSetKind.cs` | 新建 |
| 2 | **关卡目录** `LevelCatalog`(`p0_clinic` / `scaffold_second`) | `unity/Assets/Editor.Tools.Level/LevelCatalog.cs` | 新建 |
| 3 | **第二关卡(脚手架)** | `unity/Assets/Editor.Tools.Level/ScaffoldSecondLevel.cs` | 新建;文件头写明**不是设计产物** |
| 4 | **尺寸常量的删除** | `unity/Assets/Editor.Tools.Level/P0ClinicLevel.cs` | `Width` / `Height` / `Depth` 三个 `public const int` 删除,`Define()` 改传 `LevelSource` 实参 |
| 5 | **导出器泛化** | `unity/Assets/Editor.Tools.Level/LevelExporter.cs` | 删 `FileName` 常量;新增 `Export(kind, src)` / `ExportTo(kind, path, src)` / `ExportLevelToRepo` / `ExportLevelToStaging` / 两个无参 batch 入口 |
| 6 | **检查器措辞** | `unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` | `UnimplementedChecks` 7 条:「本批不产出」→「待 Sx 产出」;**条数与实现状态一字未变** |
| 7 | **夹具** | `unity/Assets/Tests/EditMode/LevelTool/level_source_generalization_test.cs` | 新建,14 条 `[Test]`,**零写盘**(不触 `FsWriteBoundaryGate`) |
| 8 | **ADR-022 命名注记** | `docs/architecture/adr-022-level-tool.md:187-199` | 加性扩展说明(基准关卡名**契约字面未改**) |

**未碰**(story 卡面 Out of Scope):`world_geometry` / `world_ecozones` / `world_poi` / `world_resources` /
`world_nav_{chunk}` / `world_terrain` 的导出与烘焙;C1–C6 任何一条的实现;运行期装载面。
**`world_buildslots` 的既有导出语义**(schema 2 / `baked_initial` / `module_id`)一字未改 ——
由 §三 的 M12 与 §四 的金标准哈希**双向**守住。

---

## §二 AC 逐条 → 载体 → 反证

夹具文件 = `unity/Assets/Tests/EditMode/LevelTool/level_source_generalization_test.cs`(14 条)。

| AC | 判据(story 原文要点) | 载体 | 反证(停用 / 改坏什么 ⇒ 哪条红) | 判定 |
|---|---|---|---|---|
| **AC-1** | 尺寸来自数据:`LevelSource` 是唯一载体 | `test_ac1_sizeComesFromData_eightByTwoByEightExport` · `test_ac1_p0ClinicLevel_noLongerDeclaresSizeConstants` · `test_ac1_canonicalLevel_keepsItsSixteenByOneBySixteenSize` | **M1**(导出器写死 `16,1,16`)⇒ `sizeComesFromData` 红;**M2**(把 `Width` 常量加回来)⇒ `noLongerDeclaresSizeConstants` 红 | ✅ |
| **AC-2** | 至少两个关卡并存,尺寸不同,都过检查,内容不同 | `test_ac2_twoLevels_differInSize_bothPassConsistencyCheck` · `test_ac2_secondLevel_isNotACopyOfFirst_hasSlotsOnSecondLayer` · `test_ac2_unknownLevelId_throwsListingKnownIds` | **M3**(第二关改成 16×1×16)⇒ 前两条红;**M10**(y=1 槽位挪到 y=0)⇒ 第二条红 | ✅ |
| **AC-3** | 类别成为一等概念;本 story 结束时 `world_buildslots` **逐字节等于**改动前产物 | `test_ac3_newDispatchPath_bytesEqualPreRefactorGolden` · `test_ac3_dispatchPath_equalsDirectSerializer` · `test_ac3_kindClosedSet_partitionsIntoImplementedAndUnimplemented` · `test_ac3_unimplementedKind_throwsNamingOwningStory` | **M12**(`module_id` 写 `""` 而非 `null`)⇒ 金标准哈希红;**M4**(已实现类再登记为未实现)⇒ `partitions` 红;**M5**(抛消息去掉 owner)⇒ `throwsNamingOwningStory` 红 | ✅ |
| **AC-4** | `UnimplementedChecks` 长度仍 7,措辞不再指向「本批」 | `test_ac4_unimplementedChecks_stillSeven_andNoLongerSaysThisBatch` | **M6**(第一条改回「本批不产出」)⇒ 该条红 | ✅ |
| **AC-5** | batch 入口泛化,至少两个关卡各有一条**可跑**命令 | 夹具只钉**形状**:`test_ac5_twoParameterlessBatchEntriesExist` · `test_ac5_fileNames_canonicalUnsuffixed_othersSuffixed` · `test_ac5_navChunkFileName_refusesLevelSuffix`;**实跑**见 §五 | **M9**(给 `ExportP0ClinicToRepo` 加可选形参)⇒ `twoParameterlessBatchEntriesExist` 红;**M7**(恒加 level 后缀)⇒ `fileNames_*` 红;**M8**(删 `NavChunk` 守卫)⇒ `navChunkFileName_*` 红 | ✅(形状 + 实跑分离登记) |

### AC-1 的判别力边界(**如实登记,不借绿**)

「构造 8×2×8 导出 ⇒ `world_size` 为 `[8,2,8]`」这条判据**在改动前就已经成立**
(`ExportBuildSlots` 一直读 `src.Width/Height/Depth`)。它对「尺寸来自数据」**没有判别力**。
真正的判别面是另两条:① 反射钉住 `P0ClinicLevel` **不再声明**那三个常量(M2);② 反证轮把导出器写死 `16,1,16` ⇒ 本判据转红(M1,证明它**能**抓住那类回归)。

同理,**M1 是「同值变异」**:写死的 `16,1,16` 恰等于 P0 真尺寸 ⇒ 基准关卡字节不变 ⇒
**金标准哈希那条保持绿**(实测确实是唯一没红的那一类)。这正是
`feedback_refactor_falsifiability_ceiling` 的实例:**金标准抓不住同值变异,只有用非 P0 尺寸的夹具抓得住**。

---

## §三 证伪轮 —— 12 条变异,逐条转红

**纪律**:预判先落盘(`lt001-falsify-plan.md`,写在任何变异之前);每次「改 → 跑 → 还原」在同一脚本内完成;
还原判据 = **SHA-256 逐字节**(未用 `git checkout` / `git stash`)。

| ID | 属性(一属性一条) | 变异 | 预判红 | **实得红** | 一致 |
|---|---|---|---|---|---|
| M1 | 尺寸**只**来自 `LevelSource` | 导出器 `world_size` 写死 `16,1,16` | 1 | 1 —— `test_ac1_sizeComesFromData_…` | ✅ |
| M2 | `P0ClinicLevel` **不再**有尺寸常量 | 加回 `public const int Width = 16;` | 1 | 1 —— `test_ac1_p0ClinicLevel_noLongerDeclares…` | ✅ |
| M3 | 两关尺寸**必须不同** | 脚手架关卡改 16×1×16 | 2 | 2 —— `twoLevels_differInSize` + `secondLevel_isNotACopy` | ✅ |
| M4 | 分派表 ↔ 未实现表**互补** | 给已实现的 `BuildSlots` 再登记一条「未实现」 | 1 | 1 —— `kindClosedSet_partitions…` | ✅ |
| M5 | 未实现类别**点名归属 story** | 抛消息去掉 owner | 1 | 1 —— `unimplementedKind_throwsNamingOwningStory` | ✅ |
| M6 | AC-4 措辞不得再写「本批」 | 第一条改回「本批不产出」 | 1 | 1 —— `ac4_…_stillSeven_andNoLongerSaysThisBatch` | ✅ |
| M7 | 基准关卡名**无后缀** | `FileNameFor` 恒加 `_{levelId}` | 1 | 1 —— `fileNames_canonicalUnsuffixed_othersSuffixed` | ✅ |
| M8 | `world_nav` **拒**按 level 命名 | 删 `NavChunk` 守卫 | 1 | 1 —— `navChunkFileName_refusesLevelSuffix` | ✅ |
| M9 | batch 入口**无参** | 给 `ExportP0ClinicToRepo` 加可选形参 | 1 | 1 —— `twoParameterlessBatchEntriesExist` | ✅ |
| M10 | 第二关**非复制品**(第二层有槽位) | y=1 的两格槽位挪到 y=0 | 1 | **首跑 0(无效)→ 修复后 1** | ⚠️ 见下 |
| M11 | `level_id` **来自 `LevelSource`** | 写死 `"p0_clinic"` | 1 | 1 —— `test_ac1_sizeComesFromData_…` | ✅ |
| M12 | AC-3 负向形态:`module_id` 不得写成省略/空串 | `baked_initial = false` 行写 `""` 而非 `null` | 1 | 1 —— `test_ac3_newDispatchPath_bytesEqual…` | ✅ |

### ⚠️ M10 首跑无效 —— **变异没打中,不是夹具假绿**

首跑实得 **0 红**(预判 1)。按纪律「实得 < 预判」须先查清再接受,查得根因:

`mutate_level001.py` 的 `apply()` 原实现是「**每条编辑**都从干净备份读文件、然后**整文件覆写**」。
M10 有两条编辑落在**同一个文件**(`ScaffoldSecondLevel.cs` 的两格 y=1 槽位)⇒
**第二条把第一条覆盖掉了**。离线复算确认:实际只挪了一格,`y=1` 仍剩 1 条槽位 ⇒ 判据理应仍绿。

**修复**:`apply()` 改为**按文件累积替换、最后一次写出**,并断言每条锚点恰好出现 1 次。
**修复后重跑**:应用后 `y=1` 槽位条数 = **0**(日志内留痕),实得 **1 红** = `test_ac2_secondLevel_isNotACopyOfFirst_hasSlotsOnSecondLayer`,与预判一致;还原 `RESTORE OK`。

**影响面已逐条核过**:12 条变异中只有 M10 有「同文件多编辑」,其余 11 条均为单编辑 ⇒ 首轮其余 11 条结果**有效**。

### 命中形态(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| **共存 / 互斥** | 已实现 ⊕ 未实现(两清单互补) | **M4** ⇒ `kindClosedSet_partitions…` 红 |
| **定义域收窄** | `world_nav` 的后缀参数**不得**是 level | **M8** ⇒ `navChunkFileName_refusesLevelSuffix` 红 |
| **阈值边界** | `UnimplementedChecks.Length == 7` | **M6** ⇒ `ac4_…_stillSeven_…` 红 |
| 零行为变化(金标准) | 与重构前产物逐字节相同 | **M12** ⇒ `newDispatchPath_bytesEqualPreRefactorGolden` 红 |

**Gate 结果:PASS** —— 四条命中形态各有独立变异,全部转红。

---

## §四 目标夹具 + 全量回归

### 4.1 目标夹具(story 卡面点名要求「既有 buildslots 夹具须保持全绿」)

| 夹具 | 结果 |
|---|---|
| `world_buildslots_bake_test` | **23/23** ✅ |
| `world_buildslots_codec_validation_test` | **12/12** ✅ |
| `level_tool_consistency_test` | **9/9** ✅ |
| `level_source_generalization_test`(本 story 新增) | **14/14** ✅ |
| `cooked_drift_all_artifacts_test`(全目录产物漂移门) | **30/30** ✅(首跑 `total=0` 无效,见 §六 ③) |

### 4.2 全量 EditMode 回归

```
total=5738 passed=5703 failed=0 inconclusive=0 skipped=35   exit=0
```

- **截至时刻**:2026-10-29 00:45 CST
- **批次**:level-tool story-001 **提交前终跑**(脚本 `final-regression`,跑的就是即将提交的那棵树)
- **commit**:`37265728` + 工作树(本 story 改动未提交)

**等式**:`5738 − 5724 = 14` = 新增夹具的 `[Test]` 计数(14)。

> ⚠️ **本数跑过三次,三次逐位相同** —— 因为 §六 ④⑤ 的两个死 API 是在第一次全量之后才删掉的:
> · **跑 A**(删除前):`5738/5703/0/0/35`;
> · **跑 B**(删 `LevelCatalog.TryDefine` + `LevelExporter.ExportTo(path, src)` 后):同一组数,目标夹具 `14/14`;
> · **跑 C**(提交前终跑,锁定上表数字):同一组数。
>
> **跑 C 的树冻结凭据**:跑前记录 7 个源文件的 SHA-256 与「工作树 17 个改动」,**跑后工作树仍是 17** ——
> 证明跑测本身没有改动任何文件。**不拿「只删了两个没人调用的方法」当免重跑的理由** —— 那正是本仓禁的借绿形态。
基线 `5724/5689/0/0/35` @ 2026-10-28 23:44 CST · ADR-033 收口轮 · `a03a62c4`+工作树
⇒ `inconclusive` / `skipped` 逐位相同,**零回归**。

### 4.3 既有产物未被改动

story 卡面要求「既有产物 `world_buildslots.cooked.bytes` 的 SHA-256 须在改动前后各记一次」:

| 项 | 改动前 / 后 |
|---|---|
| `assets/data/world_buildslots.json` | `1c946bff60177ff294f4e3bcf4a235e67b4e5067ba07db105047d57762074c6d`(**两次相同**) |
| `unity/Assets/DataCooked/world_buildslots.cooked.bytes`(348 B) | `60075ce41fef9640e0abb8d513398bc8f56af9bef86d954786865c5f2c87821b`(**两次相同**) |

`git status --porcelain assets/data/ unity/Assets/DataCooked/` ⇒ **空**。

---

## §五 两条 batch 入口**实跑**(AC-5 的「实跑」半边)

夹具只钉形状,**实跑**由本节承担(这一分工已在夹具文件头如实登记)。

### 5a `ExportP0ClinicToRepo`

```
Unity -batchmode -nographics -projectPath unity -quit \
  -executeMethod DaYiJingCheng.EditorTools.Level.LevelExporter.ExportP0ClinicToRepo
```

| 项 | 值 |
|---|---|
| EXIT | **0** |
| 跑前 SHA-256 | `1c946bff…` |
| 跑后 SHA-256 | `1c946bff…`(**逐字节相同**) |
| 日志行 | `[关卡工具] 已导出 world_buildslots.json → …/assets/data/world_buildslots.json` |

⇒ AC-3 的「零行为变化」在**真 batch 路径**上得到第二重确认,不只靠夹具内的字符串哈希。

### 5b `ExportScaffoldLevelToStaging`

| 项 | 值 |
|---|---|
| EXIT | **0** |
| 产物 | `unity/Logs/level-tool/world_buildslots_scaffold_second.json`(455 B) |
| 忽略判据 | `git check-ignore -v` ⇒ `.gitignore:257:unity/Logs/` |

产物节选:

```json
{
  "schema_version": 2,
  "level_id": "scaffold_second",
  "world_size": [8, 2, 8],
  "slots": [
    {"cell": [2, 0, 2], "slot_type": "Decor", "baked_initial": false, "module_id": null},
    {"cell": [5, 1, 5], "slot_type": "Decor", "baked_initial": true, "module_id": 7},
    {"cell": [6, 0, 1], "slot_type": "Shell", "baked_initial": true, "module_id": 8},
```

`y = 1` 的槽位是**第一关不可能有的**(`p0_clinic` 是单层 `Height = 1`)⇒ AC-2 的「非复制品」在
**真实产物**上可见,而不只靠夹具断言。

---

## §六 本轮自己的错误(五处,逐条如实登记)

### ① 测试条数数错:12 → **14**

证伪计划里写「靶测类 12 条」,实测该文件是 **14** 条 `[Test]`(`grep -c '^\s*\[Test\]'` = 14,
与 UTF 报的 `total=14` 一致)。计划文件已就地改注。
**教训**:条数在写计划时是**手数的**,没有跑一次计数 —— 而项目规矩是「数字须带可跑复算命令」。

### ② 变异器 bug:同文件多编辑时后一次覆写前一次

见 §三 的 M10 段。**这条最值得记**:它是「变异打错靶」
(`project-falsification-probe-pitfalls.md` 已列的形态之一),而它的表征是
**「预判 1 红、实得 0 红」** —— 如果当时把「0 红」直接读成「夹具假绿」,会去改一个**本来就对的夹具**;
如果反过来直接读成「变异太弱」,会漏掉「变异根本没落盘」这个真原因。
**判据**:修复后补了一条可复算凭据(应用后 `y=1` 槽位条数 = 0),不再只靠口头「打中了」。

### ③ drift 门的 filter 命名空间写错 ⇒ `total=0` 但 exit 0

首次按 `DaYiJingCheng.Tests.DataPipeline.cooked_drift_all_artifacts_test` 筛,**筛不到任何用例**,
UTF 仍报 `total=0 / passed=0 / failed=0 / exit=0`。真实命名空间是
`DaYiJingCheng.Tests.Unit.DataPipeline`(`cooked_drift_all_artifacts_test.cs:78`)。
按正确 FQN 补跑 ⇒ **30/30**。
**记这条的理由**:`total=0 exit=0` 与「全部通过」在退出码上**同形** —— 它是
`feedback_borrowed_green_regression_counts` 记的「命令跑出 0 可能是**没查**」的又一实例。
补跑脚本改为**逐条打印用例名**,让「跑过」与「没跑」在输出上不再同形。

---

### ④ 我自己新加了一个零消费方的死 API(`LevelCatalog.TryDefine`)

写 `LevelCatalog` 时顺手加了「非抛形的试探入口」`TryDefine(levelId, out LevelSource)`。
全仓 `grep -rn 'TryDefine' unity/Assets` ⇒ **零消费方**(只命中定义本身)。
而**同一天早些时候**,我在 `LevelExporter` 里删掉旧的 `FileName` 常量时,理由逐字写着
「再挂一个无消费方的属性只会重演 2026-10-28 评审 A-21 删掉的『零消费方死 API』」——
**同一个判据,我在另一个文件里没有用**。
**处置**:删除,并在原位留注记(含「『S2 将来可能要用』不是留下的理由 —— 那是**猜测的**消费方」)。

### ⑤ 保留了一个改动前就存在的死 API(`LevelExporter.ExportTo(path, src)`)

它是**改动前**的签名,我在泛化时把它改成委托到三参重载、**原样保留**了。
全仓零消费方。它比 ④ 更有害:它是「类别」这一概念的**第二个隐式默认值** ——
调用点看不出自己在导哪一类,而 AC-3 的全部意义就是让类别**在调用点显式**。
**处置**:删除,原位留注记。

> ⚠️ ④⑤ 都在**回归跑完之后**才被发现 ⇒ 删除后**重跑**了目标夹具 + 全量回归,
> 数字见 §4.2(与删除前逐位相同:`14/14` · `5738/5703/0/0/35`)。
> **不拿「反正只删了两个没人用的方法」当「不必重跑」的理由** —— 那正是本仓禁的借绿形态。

---

## §七 本件证明不了的事(禁借绿)

1. **多关卡 ≠ 多关卡可玩** —— 本 story 只证明「工具能导出第二个关卡」;
   运行期能否装载 / 切换是 S2 之后的事,`MinimalWorldFactory` 仍只认 `p0_clinic`。
2. **ADR-022 §三 的基准关卡命名是否将来也加后缀** —— 本 story 取**加性**扩展
   (基准关卡契约字面 `world_buildslots.json` **一字未改**),普通关卡用 `_{level_id}` 后缀。
   那一问归 **S2** 连同 ADR-022 的一次窄修订一起裁;本件**不预先裁决**。
3. **C1–C6 仍然一条未实现** —— `UnimplementedChecks` 实打实仍是 7 条,
   本 story 只改措辞。`AC-4` 的判据是「长度仍为 7」,**不是**「检查实现了」。
4. **`world_nav` 的切片命名** —— `FileNameFor(NavChunk, …)` 只**拒绝**按 level 命名,
   真正的 chunk 切片归 S5;本件不产出任何 `world_nav_*`。
5. **两条 batch 入口的产物「能否被读方 codec 读入」** —— 5a 的产物被既有烘焙链消费
   (见 §4.1 的 `world_buildslots_bake_test` 23/23);5b 的产物**没有任何消费者**
   (它是脚手架,刻意落暂存区)。这条差异是**设计**,不是遗漏。
6. **`LevelCatalog` 的未知 id 路径**只有夹具覆盖(`test_ac2_unknownLevelId_throwsListingKnownIds`),
   生产侧今日**零调用方会传未知 id**(两个 batch 入口传的都是常量)。
