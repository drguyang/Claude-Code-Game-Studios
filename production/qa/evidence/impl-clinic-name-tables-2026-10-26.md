# 实现证据件 —— S20-2 补轮 · 24 两张词表(ROOM_NAMES / CONTEXT_NAMES)

> **日期**:2026-10-26 · **Story**:`production/epics/clinic-machine/story-005-purity-replay-memoize.md`(关联)
> **类型**:Integration(数据链 + 交付面)· **批次**:sprint-20 · **工作树**:commit `11cbc64` + 未提交的词表轮改动
> **用户裁定**:①-C(房间名显式词表文件)/ ②-C(情境显式词表文件)/ ③-B(`ROOM_NONE = 0`)/ A(完整烘焙链)
> **证据原件 = 本文件**(承 coding-standards「评审报告原件落盘」)。

---

## 一、为什么有这一轮

`ClinicEnvDto` 的两个字段(裁定前)**没有真源**:

| 字段 | 裁定前状态 | 后果 |
|---|---|---|
| `RoomNameId` | 「房间名字典 ordinal」,字典全库不存在 | 只能落 `ROOM_NONE` 或发明哈希 ⇒ 静默错读 |
| `ContextIds` | 「情境词表索引」,词表 size 全库未定 | 同上;S20-2 评审 **W-2** 因此把 DTO 缓存判为 NOT-RUN |

⇒ 用户裁定 ①-C / ②-C / ③-B,并要求 **A 完整烘焙链**(不是只写两个 JSON)。
本轮的判据不是「文件存在」,而是**七段全接上**:作者态 → Binder → CookedWriter → Codec → Addressables →
DataCookedDisk → 投影 → Memo 交付面。缺任一段 = 「补烘 ≠ 接线」的半成品(产物在盘上、无人读它)。

---

## 二、改动清单

### 2.1 新增(作者态 + 构建期 + 运行期)

| 文件 | 作用 |
|---|---|
| `assets/data/clinicmachine_room_names.json` | 作者态房间名词表(`room_type` → 稳定 id;3 条) |
| `assets/data/clinicmachine_context_names.json` | 作者态情境词表(`context_id` → 稳定 id;3 条) |
| `unity/Assets/Editor.Tools.Bake/ClinicMachineRoomNamesBinder.cs` | 阶段 2 绑定器(白名单 + 正整数 id + 双向唯一 + 空表拒) |
| `unity/Assets/Editor.Tools.Bake/ClinicMachineContextNamesBinder.cs` | 同上(context 侧) |
| `unity/Assets/Sim/Clinic/ClinicNameTables.cs` | 运行期双向查找表(住 `Sim`,门 A 零引擎) |
| `unity/Assets/Sim/Clinic/ClinicEnvProjection.cs` | 求值 → DTO 的**投影单点**(纯函数) |
| `unity/Assets/Tests/EditMode/ClinicMachine/clinic_name_tables_test.cs` | 全链测试 |
| `tests/unit/clinic_machine/fixtures/invalid_*_names_*.json` ×6 | 违例夹具 |

### 2.2 修改(接线)

| 文件 | 改动 |
|---|---|
| `Editor.Tools.Bake/ClinicMachineCookedWriter.cs` | `WriteRoomNames` / `WriteContextNames`(各含空表守卫 + ordinal 排序) |
| `Editor.Tools.Bake/ClinicMachineTablesBake.cs` | 六文件烘焙 + **两条跨表覆盖门** + ConfigVersion 输入 4 → 6 对;`BakeFromJson` 4 → 6 参(**刻意不留跳过词表的重载**) |
| `Gameplay.Presentation/ClinicMachineCookedCodec.cs` | `ReadRoomNames` / `ReadContextNames` |
| `Gameplay.Presentation/AddressablesDataProvider.cs` | 两个 address + 两个 `Load` 分支 + `Preload` + 两个 `Ensure*` |
| `Gameplay.Presentation/DataCorePreloader.cs` | `DataCoreAddresses` 补两条 |
| `Gameplay.Presentation/DataCookedDisk.cs` | `LoadClinicRoomNames` / `LoadClinicContextNames` |
| `Editor.Tools.Bake/DataBakeMenu.cs` | 两个产物名 + 两行落盘 + 日志 |
| `Gameplay.Presentation/ClinicEnvironmentMemo.cs` | ctor 6 → 8 参;新增 `_cachedDto` + `GetDto()`;`Get()` 末尾同步投影 |
| `AddressableAssetsData/AssetGroups/data-core.asset` | 两条组条目 |
| `Tests/EditMode/DataPipeline/{cooked_drift_all_artifacts,provider_wiring_gate}_test.cs` | 两条产物 / 两条 address 断言 |
| `design/gdd/clinic-machine.md` `:340` | 词表口径六条补注 |
| `Sim.Contracts/ClinicEnvDto.cs` | 「支 4-a 结案」段 |

### 2.3 烘焙产物

`clinicmachine_room_names.cooked.bytes`(68 B)· `clinicmachine_context_names.cooked.bytes`(83 B),
各 3 条目;`ConfigVersion = 0xC7A0FC68`(六文件内容哈希)。

---

## 三、判据(逐条可复算)

*(证伪轮结果见 §四;此处列判据与载体)*

| # | 判据 | 载体 |
|---|---|---|
| ① | **编号来自作者态,不是行序** | `test_binder_{room,context}Names_idsComeFromAuthoring*`(真源 + **逆序字面量**双腿) |
| ② | **id = 0 被拒**(`ROOM_NONE` 保留) | `test_binder_roomNames_zeroId_rejected` |
| ③ | 键 / 编号双向唯一 | `…duplicateId_rejected` / `…duplicateKey_rejected` |
| ④ | 空词表拒 | `…empty_rejected` |
| ⑤ | 白名单外键拒 | `…unknownKey_rejected` |
| ⑥ | **跨表覆盖门 ⊇**(缺项 ⇒ 烘焙期硬失败) | `test_coverage_{roomNames,contextNames}Missing*_rejected` |
| ⑦ | **覆盖门单向**(多余条目允许 = P1a 预留) | `test_coverage_extraNameEntry_accepted_oneWayOnly` |
| ⑧ | 编码 ↔ 解码往返(双向查找) | `test_codec_{room,context}Names_roundTrip_*` |
| ⑨ | 盘上产物 == 现烘产物 | `test_codec_diskArtifacts_matchFreshBake` |
| ⑩ | 未匹配 ⇒ 哨兵 0 + 空数组非 null | `test_projection_unmatched_roomNameIdIsNoneSentinel` |
| ⑪ | 匹配 ⇒ 编号来自词表 | `test_projection_matched_usesVocabularyIds` |
| ⑫ | 未知 room_type / context_id ⇒ **throw 不降级** | `test_projection_unknown{RoomType,ContextId}_*` |
| ⑬ | 运行期表核四条守卫(产物损坏的最后防线) | `test_runtimeTableCore_rejectsCorruptVocabularies` |
| ⑭ | Memo 缓存 DTO 与求值**同步**(W-2 真修) | `test_dto_memoCachesProjectedDto_inLockstepWithEvaluation` |
| ⑮ | 拆除后 `GetDto()` 反映 `ROOM_NONE` | `test_dto_afterRemove_reflectsRoomNoneSentinel` |

---

## 四、S12-4 证伪轮(15 条变异)

**纪律**:每条变异 = 一条独立 Unity 进程;改 → 跑 → **同命令内 SHA-256 逐字节还原**;
锚串预检 15/15 恰命中 1 次;变异窗口内不读不编辑被变异文件;变异保持可编译。
驱动脚本 `/home/gu/.claude/jobs/181888b1/tmp/falsify_names.py`;原始日志
`falsify-names-run.log` + 逐条 `falsify-M*.xml`。

| # | 属性(五形态) | 被变异文件 | 变异 | 结果 | 转红测试 |
|---|---|---|---|---|---|
| **M1a** | 编号≠行序(定义域收窄) | `ClinicMachineRoomNamesBinder.cs:156` | `id` → `i + 1` | ✅ 1 红 | `test_binder_roomNames_idsComeFromAuthoringNotRowOrder` |
| **M1b** | 编号≠行序(同上 · context 侧) | `ClinicMachineContextNamesBinder.cs:157` | `id` → `i + 1` | ❌ **0 红(逃逸)** | —— 见 §4.1 |
| **M2** | 编号唯一(共存互斥) | `…RoomNamesBinder.cs:150` | 停用 `seenIds.Add` | ✅ 1 红 | `…duplicateId_rejected` |
| **M3** | 键唯一(共存互斥) | `…RoomNamesBinder.cs:144` | 停用 `seenTypes.Add` | ✅ 1 红 | `…duplicateKey_rejected` |
| **M4** | 空表拒(阈值边界) | `…RoomNamesBinder.cs:100` | 停用空表守卫 | ✅ 1 红 | `…empty_rejected` |
| **M5** | id ≥ 1(阈值边界) | `ClinicMachineSchema.cs:439` | `n.Int <= 0` → `< 0` | ✅ 1 红 | `test_binder_roomNames_zeroId_rejected` |
| **M6** | ROOM_NAMES 覆盖门 | `ClinicMachineTablesBake.cs:181` | 停用 | ✅ 1 红 | `test_coverage_roomNamesMissingTableRoomType_rejected` |
| **M7** | CONTEXT_NAMES 覆盖门 | `…TablesBake.cs:182` | 停用 | ✅ 1 红 | `test_coverage_contextNamesMissingTableContextId_rejected` |
| **M8** | 覆盖门**单向**(过度收紧) | `…TablesBake.cs:215` | 改双向差集归零 | ✅ 1 红 | `test_coverage_extraNameEntry_accepted_oneWayOnly` |
| **M9** | 编码面(写方镜像) | `ClinicMachineCookedWriter.cs:212` | 编号恒写 0 | ✅ 2 红 | `…roundTrip_preservesIdsBothWays` + `…diskArtifacts_matchFreshBake` |
| **M10** | 不降级为 None | `ClinicEnvProjection.cs:66` | throw → `return NoneId` | ✅ 1 红 | `test_projection_unknownRoomType_throwsInsteadOfDegradingToNone` |
| **M11** | 不静默丢项 | `ClinicEnvProjection.cs:86` | throw → `continue` | ✅ 1 红 | `test_projection_unknownContextId_throws` |
| **M12** | ROOM_NONE 哨兵 | `ClinicEnvProjection.cs:60` | `NoneId` → `1` | ✅ 2 红 | `…unmatched_roomNameIdIsNoneSentinel` + `…afterRemove_reflectsRoomNoneSentinel` |
| **M13** | DTO 接线(**补烘≠接线**) | `ClinicEnvironmentMemo.cs:162` | `_cachedDto = default` | ✅ 2 红 | `…memoCachesProjectedDto_inLockstepWithEvaluation` + `…afterRemove_…` |
| **M14** | 失效键闭集(定序/一次性) | `ClinicEnvironmentMemo.cs:142` | 停用 `_hasCache = false` | ✅ **7 红** | 见 §4.2 |
| **M15** | 运行期表核 id 守卫 | `ClinicNameTables.cs:78` | `e.Id < 1` → `false` | ✅ 1 红 | `test_runtimeTableCore_rejectsCorruptVocabularies` |

**14/15 转红,红名单全部落在预期测试上(零误伤)**。

### 4.1 M1b 逃逸 —— 已就地补判别力(本轮真实收获)

`test_binder_contextNames_idsComeFromAuthoring` 原版只断言 `ward_plain=1 / surgery_sterile=2 /
herb_heavy=3`。而**真源三行的编号恰好等于 `i + 1`** ⇒ 把 Binder 改成按行序发号,值完全不变 ⇒ 全绿。

**这正是证伪轮存在的理由**:该夹具若不经反证,会以「绿」留下却什么都没测。
**修法**(测试侧,已落):补一条**编号与行序刻意逆序**的字面量 —— `herb_heavy` 在第 0 行却是编号 3,
按行序发号会得 0 或 1。room 侧同类补强亦已落(`M1a` 本已转红,补强后两种退化形态 `i` 与 `i+1` 都被钉住)。

⚠️ **M1b 的补强未在本轮 15 条内重跑反证** —— 补强落盘时证伪主进程正在跑,为免「变异窗口内改被测对象」
污染归因,本轮**未重跑**。补强后的反证见 §4.3。

### 4.2 M14 的 7 条红名单(全部合理)

`test_dto_afterRemove_reflectsRoomNoneSentinel` · `test_ec2405_afterModify_invalidatesCache` ·
`test_ec2405_afterPlace_memoInvalidatesAndEqualsDirectEvaluation` ·
`test_ec2405_afterRemove_staleEntryNotReturned` · `test_ec2405_sameTickTwoChanges_terminalStateConsistent` ·
`test_replayEquivalence_incrementalPath_equalsDirectTerminalLayout` ·
`test_replayEquivalence_withRemove_equalsDirectTerminalLayout`

停用「失效」这一处逻辑,凡依赖「缓存会失效」的测试全红 —— **一处守卫被多条测试覆盖**,不是误伤。

### 4.3 M1b 补强后的反证(已跑)

补强落盘后**重跑同一变异**(`falsify_names.py M1b M1a`),结果:

| 变异 | 补强前 | 补强后 | 红名单 |
|---|---|---|---|
| **M1a**(room 侧按行序发号) | 1 红 | **1 红** | `test_binder_roomNames_idsComeFromAuthoringNotRowOrder` |
| **M1b**(context 侧按行序发号) | **0 红(逃逸)** | **1 红** | `test_binder_contextNames_idsComeFromAuthoring` |

⇒ 补强把一条**假绿夹具**改成了有判别力的夹具。这一条是本轮证伪轮**唯一抓到的缺陷**,
且抓法与既往一致:**不是读代码看出来的,是把守卫改坏跑出来的**。

---

## 五、测试数(带三元组)

| 项 | 值 |
|---|---|
| **截至时刻** | `2026-10-25 23:40 UTC`(XML `end-time`;本轮实跑) |
| **批次** | sprint-20 · S20-2 词表补轮 · 全量第二轮 |
| **commit** | `11cbc64` + 本轮未提交件 |

### 5.1 夹具级(ClinicMachine 命名空间)

| 轮次 | total / passed / failed / skipped | XML |
|---|---|---|
| ClinicMachine 夹具(补强后基线) | **172 / 172 / 0 / 0** | `unity/Logs/names-test4.xml` |
| 补强前基线 | 172 / 172 / 0 / 0 | `unity/Logs/names-test3.xml` |
| 15 条证伪 | 见 §四(14 条转红 + M1b 逃逸) | `falsify-M*.xml` |
| M1a/M1b 补强后反证 | 各 171/172(各 1 红) | 同路径 |

**增量等式**:`names-test3` 172 → `names-test4` 172(**零新增测** —— 本轮补的是**既有测试的判别力**,
不是新测试;新增的两条 `test_runtimeTableCore_…` 与逆序断言均已计入 172)。
⚠️ **`172` 与 `names-test2` 的 `171` 差 1** = 本轮新增 `test_runtimeTableCore_rejectsCorruptVocabularies`
(逆序断言是**加进既有测试体**的,不增测数)。

### 5.2 全量 EditMode 回归

| 轮次 | total / passed / failed / skipped | XML | 说明 |
|---|---|---|---|
| 基线(引用,未重盖时间戳) | **5081 / 5046 / 0 / 35** | `Logs/s20-2-final3.xml` @ 2026-10-26 · S20-2 收口轮 · `734565c` | — |
| 全量首跑 | **5104 / 5067 / 2 / 35** | `Logs/names-full.xml` | 2 红,均已归因(见 5.3) |
| **全量二跑(收口值)** | **5104 / 5069 / 0 / 35** | `Logs/names-full2.xml` | **零红** |

**增量等式(可复算)**:

```
5081 → 5104  ⇒ +23
新增来源(实测差集 added=23 removed=0):
  ClinicNameTablesTest            19   ← 本轮新类
  ClinicPurityReplayTest           2   ← 本轮新增 2 测
  cooked_drift_all_artifacts_test  2   ← 两条 [TestCase],两个新产物各生 1 case
  ────────────────────────────────────
  合计                            23   == 增量 23 ✓
```

复算命令:

```bash
cd unity && python3 - <<'EOF'
import xml.etree.ElementTree as ET, collections
def load(p):
    r = ET.parse(p).getroot()
    return {(c.get('fullname') or c.get('name')) for c in r.findall('.//test-case')}
base, new = load('Logs/s20-2-final3.xml'), load('Logs/names-full2.xml')
added, removed = sorted(new - base), sorted(base - new)
print(f"base={len(base)} new={len(new)} added={len(added)} removed={len(removed)}")
for k, v in collections.Counter(n.rsplit('.', 1)[0] for n in added).most_common():
    print(f"  {v:3d}  {k}")
EOF
```

**预期输出**:`base=5081 new=5104 added=23 removed=0`,分组为
`19 ClinicNameTablesTest` / `2 ClinicPurityReplayTest` / `2 cooked_drift_all_artifacts_test.drift[...]`。

### 5.3 首跑两红的归因与修复(本轮新增产物直接导致,非回归)

| # | 红 | 根因 | 修复落点 |
|---|---|---|---|
| ① | `CatalogFreshnessGateTest.test_catalog_containsEveryDataCoreAddress` | `data-core` 组声明 24 条,`catalog.bin` 只有 22 条。`m_BuildAddressablesWithPlayerBuild: 0` ⇒ **打包不会顺带重建 catalog**,必须显式跑菜单 | `DataBakeMenu.BuildAddressablesContent`(batch `-executeMethod`);`catalog.bin` 07:33 更新(17230 B) |
| ② | `cooked_drift_all_artifacts_test.test_sweep_declaredArtifactNames_allTriaged` | 本轮往 `BakedArtifacts` 加了两条产物,却漏改扫描器完备性哨兵 `pinnedDeclared`(22)。该测试**自己就写着**「新增产物名须同时改本数字」 | `cooked_drift_all_artifacts_test.cs:360` `pinnedDeclared` `22 → 24` + 文件头第五次增补注 |

⚠️ **这两条红是门在正常工作,不是门坏了** —— ① 抓到 catalog 未重建,② 抓到哨兵漏改。
**修复方向都是补该补的东西,没有一处是放宽门**。②尤其值得记:本轮把产物加进 `BakedArtifacts`
时,同一份文件里的哨兵没跟着改 —— 这是本门**第四次**抓到同类漏登记。


---

## 六、明确未做(如实登记,非遗漏)

| # | 项 | 为什么不做 |
|---|---|---|
| ① | `IClinicEnvQuery` 的**生产实现** | `GetEnv(int roomId)` 需要「int 房号 ↔ `ClinicRoomId(High,Low)`」注册表 —— 全库不存在(`ClinicRoomId` 的 High/Low 是布局哈希的两个 32 位半,不是坐标也不是序号)。词表只解决 `ContextIds` 那一半。**归装配轮** |
| ② | `StructureModified` 支的独立测试 | S20-2 已补(`test_ec2405_afterModify_invalidatesCache`),非本轮面 |
| ③ | ADR-012 跨平台逐位矩阵 | 归 ADR-012 批(NOT-RUN 直至该批复跑) |
| ④ | 词表的 `display_name` 等展示字段 | 裁定只要求 `(键, 编号)`;展示归 42 |
