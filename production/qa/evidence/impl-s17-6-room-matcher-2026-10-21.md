# S17-6 实现件 —— story-006 房间类型匹配(ROOM_TABLE 子句求值)+ `AC-24-03`

**日期**:2026-10-21
**缘起**:用户指令「story-006 实现」
**卡面**:`production/epics/clinic-machine/story-006-room-type-matching.md`(6 条 AC · Logic · 6h)

---

## 一、交付面

| # | 文件 | 状态 | 说明 |
|---|---|---|---|
| 1 | `unity/Assets/Sim/Clinic/ClinicRoomMatcher.cs` | **新建** | 生产实现。零 `UnityEngine`(门 A:引用集恰 = {BCL, `Sim.Contracts`}) |
| 2 | `unity/Assets/Sim/Clinic/ClinicRoomMatcher.cs.meta` | **新建** | guid `4d20a32e43d44c74ab15060e9f4ebd7a`(全库唯一,已 grep 核) |
| 3 | `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs` | **新建** | **24 测** |
| 4 | `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs.meta` | **新建** | guid `17e6b0246bdc49f1a60013f9cc7d8176` |
| 5 | `tools/falsify/specs/s17-6-clinic-room-matcher.json` | **新建** | 6 自动变异 + 1 手动变异(M7) |

**未改动任何既有文件** —— 本件是纯新增(`git status` 全为 `??`)。

### 1.1 公开 API

```csharp
public static class ClinicRoomMatcher
{
    public static bool MultiMatchPolicyIsProvisional => true;   // 缺口哨兵(OQ-24-7)

    public static bool TryMatchRoom(in ClinicRoomExtractionResult room,
                                    IClinicFurnitureTraitsSource traits,
                                    ClinicRoomTable table,
                                    out string roomType);        // 便捷重载

    public static bool TryMatchRoom(in ClinicRoomExtractionResult room,
                                    IClinicFurnitureTraitsSource traits,
                                    ClinicRoomTable table,
                                    out string roomType,
                                    out ClinicRoomMatchDiagnostics diagnostics);
}

public readonly struct ClinicRoomMatchDiagnostics
{
    public readonly int MatchedRoomCount;        // 全子句成立的房间类型个数(≥2 = 多匹配)
    public readonly int UnknownClauseTypeCount;  // 布局里无任何一格承载的子句类型引用条数
}
```

---

## 二、逐条 AC → 载体

| AC | 判据 | 载体(全部在 `clinic_room_matcher_test.cs`) |
|---|---|---|
| **AC-24-03 ①** | 空布局 ⇒ `false` + `null`(EC-24-01) | `test_roomNone_emptyLayout_returnsFalse` |
| **AC-24-03 ②** | 有家具但谓词全不成立 ⇒ `false`(EC-24-02) | `test_roomNone_furniturePresentButNoPredicateHolds_returnsFalse` |
| **AC-24-03 ③** | ≥ 2 簇 ⇒ 整盘 ROOM_NONE(EC-24-07) | `test_roomNone_twoClusters_returnsFalse` |
| **子句三型 · min_count** | 正 / 反 | `test_clause_minCount_twoBeds_matches` / `test_clause_minCount_oneBed_doesNotMatch` |
| **子句三型 · has_type** | 正 / 反 | `test_clause_hasType_singleSurgeryTable_matches` / `test_clause_hasType_absent_doesNotMatch` |
| **子句三型 · adjacency_min_count** | 正 / 反 **+ 方向反例** | `test_clause_adjacency_plusX_neighbourMatches` / `test_clause_adjacency_plusY_neighbourMatches` / **`test_clause_adjacency_neighbourOnMinusX_doesNotMatch`** / **`test_clause_adjacency_neighbourOnMinusY_doesNotMatch`** |
| **合取** | 任一不成立 ⇒ 不匹配 | `test_conjunction_onlyOneClauseHolds_doesNotMatch` / `test_conjunction_allClausesHold_matches` / `test_conjunction_partialMatch_isNotCountedAsMatched` |
| **多匹配 = 声明序首个** | 正 / 反序 / 哨兵 | `test_multiMatch_declarationOrderFirstWins` / `test_multiMatch_reversedDeclarationOrder_winsTheOtherOne` / `test_multiMatch_policyIsProvisional_sentinel` |
| **纯函数** | 两次求值逐项相同 / 无缓存泄漏 | `test_purity_sameInputsTwice_identicalResult` / `test_purity_noCache_acrossDifferentLayouts` |
| **未知类型不抛且可观测** | 单条 / 不阻断其它房间 / 与「格属性 Unknown」分家 | `test_unknownType_doesNotThrow_treatedAsNotSatisfied` / `test_unknownType_doesNotBlockOtherRooms_andCountsEachRef` / `test_unknownCellLookup_throws_incompleteSource` |
| **非家具格排除** | 非格不计分 | `test_nonFurnitureCell_excludedFromClauseCounts` |
| **输入面边界** | 签名不含 `ClinicAdjTable` / `ClinicContextTable` | `test_signature_takesNoAdjTableOrContextTable` |

**AC-24-03 的「双向」已兑现**:EC-24-01 与 EC-24-02 是**两条独立测试**,且都断言了
`HasRoom` 的前提值(`false` / `true`)—— 前者是后者的特例,反向不成立。

---

## 三、证伪轮(S12-4 · BLOCKING)

**规格**:`tools/falsify/specs/s17-6-clinic-room-matcher.json`
**驱动**:`python3 tools/falsify/unity_falsify.py --spec … --guard unity/Assets/DataCooked --guard unity/Assets/AddressableAssetsData`
**原始日志**:`unity/Logs/s17-6-falsify-2.log`(第 1 跑 `s17-6-falsify.log`,见 §3.3)

### 3.1 结果:6/6 自动变异 exactRed + 1/1 手动变异

| 变异 | 命中形态 | 停用了什么 | 转红(恰如预期) |
|---|---|---|---|
| **M1** | ① 定序 | 声明序首个 → 最后者胜 | `test_multiMatch_declarationOrderFirstWins` · `test_multiMatch_reversedDeclarationOrder_winsTheOtherOne` |
| **M2** | ③ 定义域收窄 | 合取 → 只看首子句 | `test_conjunction_onlyOneClauseHolds_doesNotMatch` · `test_conjunction_partialMatch_isNotCountedAsMatched` · `test_clause_adjacency_neighbourOnMinusX_doesNotMatch` · `test_clause_adjacency_neighbourOnMinusY_doesNotMatch` |
| **M3** | ③ 定义域收窄 | `+x` 单向 → 双向 | `test_clause_adjacency_neighbourOnMinusX_doesNotMatch` |
| **M4** | ③ 定义域收窄 | 删 `HasRoom == false` 短路 | `test_roomNone_emptyLayout_returnsFalse` · `test_roomNone_twoClusters_returnsFalse` |
| **M5** | ③ 定义域收窄 | 邻接子句 `neighbor_type` 引用不再计数 | `test_unknownType_doesNotBlockOtherRooms_andCountsEachRef` |
| **M6** | ③ 定义域收窄 | 格属性 `Unknown` 静默跳过 | `test_unknownCellLookup_throws_incompleteSource` |
| **M7**(手动) | ⑤ 一次性 / 无缓存 | 引入跨调用缓存(按 `table` 引用分键) | `test_purity_noCache_acrossDifferentLayouts` |

驱动尾行:`══ 证伪轮通过:7 个变异全部恰如预期 ══`
受保护路径指纹:`✅ 指纹未变 —— unity/Assets/DataCooked, unity/Assets/AddressableAssetsData`

### 3.2 M7 的手动复验(行替换表达不了)

**为什么不能自动**:缓存必须有**存储位**,而 `Sim` 侧当前无任何可变静态字段(全 `const` / `readonly`)
⇒ 需三处改动(字段声明 + 命中返回 + 写入),超出「单行替换」的方法论。
⇒ 驱动里标 `skip: true` + `_manualProof`,**不删掉**,把「为什么不适用」留在规格里。

**预期(先写,再跑)**:恰 1 条转红 = `test_purity_noCache_acrossDifferentLayouts`;
其余 23 条保持绿(其它测各自 `new ClinicRoomTable`,引用不同 ⇒ 不命中缓存)。

**实测**:`total=24 passed=23 failed=1` · `RED: test_purity_noCache_acrossDifferentLayouts` ⇒ **恰如预期**。
XML = `unity/Logs/falsify-M7-purityNoCache.xml`。

**还原**:`cp` 备份回 + `sha256sum -c` 通过:
```
变异前 fabaf592229e807f78dff8b46d2c182188074773f02b662eb5feaf3eabd3ca41
还原后 fabaf592229e807f78dff8b46d2c182188074773f02b662eb5feaf3eabd3ca41
✅ 逐字节相同(未用 git checkout / git stash —— 承「证伪还原禁令」)
```
残留 `FALSIFY` 标记 = **0**。

### 3.3 ⚠️ 第 1 跑的一处失败:变异形态本身不合法(如实登记)

第 1 跑(`s17-6-falsify.log`)M1–M5 全部 exactRed,**M6 编译失败**:

- 首版形态 = `if (false) throw new InvalidOperationException(…)`(把 `throw` 包进恒假分支)
- 后果:`default` 分支不再终止 ⇒ C# 报 **「switch 段落不得穿透」** ⇒ `unity` 退出码 **6**、**零 XML**
- 驱动判据:`!! 未产出结果 XML —— 编译失败?` ⇒ 计逃逸 ⇒ 退出码 **1**

**这不是判别力缺口,是变异形态不合法**。换形态为「给 `Unknown` 单开一支 `break`」
⇒ 该值不再落到 `default` 的 `throw` ⇒ 不抛(语义等价,且能编译)。
第 2 跑 M6 由「零 XML」变为「恰 1 条转红」。
该形态教训已写进 spec 的 `_note_on_form`,防后人重蹈。

### 3.4 证伪轮的**自陈边界**(禁借绿)

- **M4 的承重面是诊断契约,不是返回值**。删 `HasRoom == false` 短路**不改变返回值**
  (空 `RoomCells` 下子句本来就全不成立)—— 它改变的是 `UnknownClauseTypeCount`
  (ROOM_NONE ⇒ 不进入子句求值 ⇒ 恒 0)。**卡面证伪表原写「删短路 ⇒ EC-24-01 测红」,
  在本实现形态下由「诊断全零」这一条断言承载**,已在测试文件头与 spec 的 `_why` 里逐字登记。
- **M7 只能钉住「缓存泄漏」这一种失败**。一个按 `RoomId` **正确分键**的缓存是不可观测的
  (结果逐位相同),故 `test_purity_noCache_acrossDifferentLayouts` 钉不住它 ——
  已在测试的 `<remarks>` 里写明这一半不在可观测面内。
- **「未知类型」的语义在本件被拆成两件**,不得混读:
  ① **子句引用的 `type` 在布局里没有格** ⇒ 按「不成立」处理、不抛、计入诊断(**本件判据**);
  ② **该 `type` 不在 `FURN_TYPES` 闭集** ⇒ **烘焙期**判据(归 story-001 的成员资格门),
     匹配期**无从判定**,本件**不假装判定**(那需要闭集作输入,而本件的输入面刻意只有三件)。
  `ClinicRoomMatchDiagnostics.UnknownClauseTypeCount` 的 XML 注释逐字写明这一点。

---

## 四、回归

**基线**:`4291 / 4255 / 0 / 36` @ 2026-10-21 12:38(`unity/Logs/s17-5-full-editmode-2.xml`),
commit `09340e6`(本件开工前工作树干净)。

**本轮**:`4315 / 4279 / 0 / 36`(见 §六)—— **+24 / +24 / 0 / 0**,集合差「新增 24 · 消失 0」⇒ 零回归。

---

## 五、未做 / 未证

- **`OQ-24-7` 未裁** —— 只有暂定裁决(声明序首个)。哨兵 `MultiMatchPolicyIsProvisional => true`,
  裁定后须转 `false` + 改实现 + 哨兵测试**转红即提醒**,不得静默改绿。
- **`AC-24-03` 的「⇒ `(0,0)` 中性输出」这一半**由 `ClinicModifierEvaluator` 的
  `HasRoom == false` 短路承担(**story-003 已勾**),本件只产出 `room_type` / `ROOM_NONE`,
  **不重复实现中性输出**(卡面 §Implementation Notes 2:不改 `ClinicModifierEvaluator` 签名)。
  两件合起来才是完整的 AC-24-03;本件不自称单独兑现了「输出 (0,0)」。
- **未接线到任何装配根** —— 生产装配层选哪条 `Extract` 入口(代理 / 真判据)尚未落地,
  归后续接线轮。本件只交付纯函数。
- **IL2CPP 腿未跑** —— 本件零跨平台新增面(纯 C# 整数与字符串序数比较,零引擎 API);
  矩阵级证据仍引用 ADR-012 §Validation 的 2026-09-24 实跑。
- **未 commit**(用户未指令)。

---

## 六、全量回归结果

**三元组**:截至时刻 `2026-10-21 13:50:53 → 13:52:46 CST` · 批次 `S17-6 全量` · commit `09340e6`(工作树含本件新增,未提交)

| 套件 | 退出码 | XML | 计数 |
|---|---|---|---|
| EditMode | 0 | `unity/Logs/s17-6-full-editmode.xml` | `total=4315 passed=4279 failed=0 skipped=36 inconclusive=0` |

**对照基线** `4291 / 4255 / 0 / 36` @ 2026-10-21 12:38(`s17-5-full-editmode-2.xml` · `09340e6`):

| 项 | 基线 | 本轮 | 差 |
|---|---|---|---|
| total | 4291 | 4315 | **+24** |
| passed | 4255 | 4279 | **+24** |
| failed | 0 | 0 | 0 |
| skipped | 36 | 36 | 0 |

**等式校验**:`4315 − 4291 = 24` = 本轮新增测数(本件 `clinic_room_matcher_test.cs` 的 24 条)。
**集合差实测**:新增 **24** · **消失 0** —— 逐条 `fullname` 比对,24 条全为
`DaYiJingCheng.Tests.ClinicMachine.ClinicRoomMatcherTest.*`。
⇒ **零回归**(不是「照抄陈旧数」,也不是「把未跑出的数先写成事实」)。
