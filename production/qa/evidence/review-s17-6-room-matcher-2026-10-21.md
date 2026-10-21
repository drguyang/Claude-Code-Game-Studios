# 评审原件 —— S17-6 / clinic-machine story-006 `ClinicRoomMatcher`

> **日期**:2026-10-21
> **对象**:`unity/Assets/Sim/Clinic/ClinicRoomMatcher.cs`(生产)+
> `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_matcher_test.cs`(测试)+
> `tools/falsify/specs/s17-6-clinic-room-matcher.json`(证伪规格)
> **缘起**:story-006 实现轮的**补齐轮**。原实现轮 `Code Review` 如实登记为「未跑」
> (`production/review-mode.txt` = `lean`),本轮**补做一次评审**(评当下代码,不追认原判定)。
> **执行方式**:三路并行(lead-programmer / qa-tester / unity-specialist),
> 各自独立出报告;草稿分别落 `tmp-review-s17-6-{lead,qa,unity}.md`。

---

## 一、三路裁定汇总

| 路 | 专家 | 裁定 | BLOCKING | WARNING | INFO |
|---|---|---|---|---|---|
| **A** | `lead-programmer`(代码本体 + ADR 合规) | **APPROVED WITH SUGGESTIONS** | 0 | 3(W1 / W2 / W3) | 4(I1–I4) |
| **B** | `qa-tester`(AC 覆盖 + 假绿) | **GAPS** | 0(无 BLOCKING 级假绿) | 4 条不判 ADEQUATE 的依据 | — |
| **C** | `unity-specialist`(引擎侧) | **CLEAN** | 0 | 0 | 3 |

**合并裁定:CHANGES REQUIRED(以 WARNING 级修复为主,无 BLOCKING)。**
下面逐条结清,承 `.claude/skills/code-review/SKILL.md` §Phase 8b 的**双列**要求
(Landed = 改动在盘上 / Proven = 被可复算判据钉住,**两列皆 ✅ 才算结清**)。

---

## 二、结账表(已落 / 已证 双列)

| ID | 来源 | 原判定 | Landed | Proven | 文件:行 | Proven-by |
|---|---|---|---|---|---|---|
| **W1** | A | 便捷重载未声明其会转抛的异常 | ✅ | ✅ | `ClinicRoomMatcher.cs:107-120` | 补 `<exception>` ×2;判据 = 与同文件夹 `ClinicModifierEvaluator.ClinicAdjSum` 的文档形态一致(逐条目视核对) |
| **W2** | A | `ROOM_NONE` 在同一个文件里有两个含义 | ✅ | ✅ | `ClinicRoomMatcher.cs:50-57` · `:150-153` | 收窄为「`HasRoom == false`(未析出房间)⇒ 诊断全零」+ 明列两义;判据 = `test_roomNone_emptyLayout_returnsFalse` / `test_unknownType_doesNotThrow_treatedAsNotSatisfied` 两条**同时**绿(前者诊断 0、后者诊断 1) |
| **W3** | A | 哨兵转红链只覆盖「实现不变」的裁定方向 | ✅ | ✅ | 测试 `:441-490` | **新增** `test_multiMatch_policyIsProvisional_isDetectable`,断言「做了裁决 ⇔ 哨兵为 false」的**双向等价**;判据 = 改任一侧 ⇒ 该测红(见 §三 M15) |
| **B-1** | B | `test_purity_sameInputsTwice` 近乎恒真,对确定性变异零判别力 | ✅ | ✅ | 测试 `:602-625` | **新增** `test_purity_rebuiltInputs_sameResult_noIdentityKeyedCache`(两次调用之间重建全部输入对象);原测保留但补 `<remarks>` 自陈判别力边界 |
| **B-2** | B | 控制清单 Required「Ordinal 比较」零载体 | ✅ | ✅ | 测试 `:508-530` | **新增** `test_ordinalComparison_caseVariant_isNotTreatedAsSameType`(「Bed」vs「bed」);判据 = M8 变异 ⇒ 该测红 |
| **B-3** | B | `+y` 方向守卫无专属变异 | ✅ | ✅ | spec **M15** · `ClinicRoomMatcher.cs:331` | **本轮补 M15**(`+y` 单向 → 双向)⇒ `test_clause_adjacency_neighbourOnMinusY_doesNotMatch` **恰 1 条红**(`falsify-M15-plusYTwoDirections.xml`:`total=55 passed=54 failed=1`)。**已证**。 |
| **B-4** | B | `int.MaxValue` 邻格守卫零载体 | ✅ | ✅ | 测试 `:534-560` | **新增** `test_adjacency_xAtIntMax_noPhantomNeighbourFromWraparound`;判据 = M10 变异 ⇒ 该测红 |
| **B-5** | B | 「家具但 type 为空 ⇒ throw」零载体 | ✅ | ✅ | 测试 `:564-580` | **新增** `test_furnitureCellWithEmptyType_throws`;判据 = M11 变异 ⇒ 该测红 |
| **B-6** | B | `traits` / `table` 为 null ⇒ ANE 零载体 | ✅ | ✅ | 测试 `:584-596` | **新增** `test_nullArguments_throwArgumentNullException`;判据 = M12 变异 ⇒ 该测红 |
| **B-7** | B | 邻接 `Min ≥ 2` 路径从未求值 | ✅ | ✅ | 测试 `:486-506` | **新增** `test_clause_adjacencyMinCount_twoNeighbours_requiresBoth`(min=2 正例 + min=3 负例);判据 = M9 变异 ⇒ 该测红 |
| **B-8** | B | `z ≠ 0` 的邻接零载体 | ✅ | ✅ | 测试 `:600-620` | **新增** `test_adjacency_zIsPassedThrough_notHardcodedZero`;判据 = M13 变异 ⇒ 该测红 |
| **B-9** | B | M7 是 `skip: true` ⇒ 重跑驱动会**静默跳过**唯一承载「禁缓存」的变异 | ✅ | ✅ | spec `_note` + M14 | 补 `_note` 显式声明「通过的是 6 个不是 7 个」;新增 M14(按引用分键缓存)手动复验 |
| **B-10** | B | 卡面 §Test Evidence 证伪表「删短路 ⇒ EC-24-01 测红」措辞在返回值语义上不成立 | ✅ | ✅ | 卡面 §Completion Notes | 就地加注为「⇒ **诊断断言**红」,与证据件 §3.4 对齐(原文不删) |
| **B-11** | B | AC-24-03 的「⇒ (0,0)」两半从未在同一测试里一起验证 | ✅ | ✅ | `clinic_room_matching_e2e_test.cs` · `test_emptyWorld_roomNone` | **接线轮已落地**:经**真装配**`CompositionRoot.Assemble(structures: registry)`(真 `StructureInstanceRegistry` + 真 cooked 表)断言 `Extraction.HasRoom == false` **且** `Matched == false` **且** `RoomType == null` **且** `MatchedRoomCount == 0` —— 两半在同一测试里一起验。⚠️ 布局来源是**裸 `StructureInstanceRegistry` + 真 `StructureWriter.Place`**,**不是 `World`**(见 §四 残余 B-11b)。 |
| **I1** | A | 邻接双实现无交叉约束 | ✅ | ✅ | `ClinicRoomMatcher.cs:313-317` | 补**反向指认**注释(`ClinicModifierEvaluator.SumAdjacencyRaws` 侧原已有正向指认) |
| **I3** | A | 未接线 | ✅ | ✅ | §五 | 本轮落地全链接线 |

---

## 三、证伪轮(补齐轮 · **15 变异**)

> 🔁 **2026-10-21 末轮订正**:本节原标题写「14 变异」、表内止于 M14,而 §二 的 B-3 已记
> 「本轮补 M15 ⇒ 恰 1 条红,已证」。**两处自相矛盾** —— 原文不删,标题就地改数并在此补 M15 行。
> 实跑原件 `unity/Logs/falsify-M15-plusYTwoDirections.xml`:`total=55 passed=54 failed=1`,
> 唯一红 = `test_clause_adjacency_neighbourOnMinusY_doesNotMatch`。

**先写预期,再跑**(承 `tools/falsify/README.md` 纪律)。规格 `tools/falsify/specs/s17-6-clinic-room-matcher.json`。

> ⚠️ **锚点漂移已修**:本轮对生产文件加注释使 7 条既有锚点行号过期(驱动 exit 2)。
> 逐条重新定位后 `check_all_anchors.py` 报 **14/14 全绿**。
> ⚠️ **这是「行号漂移」不是「锚点语义过期」** —— `expect` 原文逐条唯一命中,只有 `line` 变。
>
> 🔁 **2026-10-21 末轮:锚点**第二次**漂移,已修,现 `15/15 全绿`**。
> 原因同型 —— 接线轮继续在 `ClinicRoomMatcher.cs` 顶部加注释(`OQ-24-7` 语义订正段),
> 使全部 14 条**统一后移 8 行**;M15 为本轮新增(原 `:331` → 现 `:339`)。
> 逐条判据与上一轮相同:**`expect` 原文逐条唯一命中,只有 `line` 变**(M7 / M14 两条 `skip` 占位锚点除外,
> 它们在盘上有 2 处同形命中,按「离原行号最近」取 `:184`,并在 `_skipReason` 已声明「仅作占位锚点」)。
> **连带修了 4 份其它 spec 的 5 条漂移**(均由本轮改 `AddressablesDataProvider.cs` /
> `DataCorePreloader.cs` / `BuildMenu` 造成):
> `audio-import-settings-and-playmode` F3(1137→1233)· `s16-4-fs-write-boundary` F2/F3(219→230 / 216→227)·
> `s17-2-clinic-wiring` W2(337→350)· `s17-4-furniture-catalogue` F4(585→591)。
> ⇒ `python3 tools/falsify/check_all_anchors.py` 现报 **18/18 份 spec 锚点全绿 · 0 份过期**。

| 变异 | 形态 | 目标 | 预期红 |
|---|---|---|---|
| M1 | ① 定序 | 声明序首个 → 最后者胜 | 2 条多匹配测 |
| M2 | ③ 定义域收窄 | 合取 → 只看首子句 | 4 条 |
| M3 | ③ 定义域收窄 | `+x` 单向 → 双向 | `neighbourOnMinusX` |
| M4 | ③ 定义域收窄 | 删 `HasRoom` 短路 | 2 条 ROOM_NONE 测(**诊断断言**) |
| M5 | ③ 定义域收窄 | 邻接 `neighbor_type` 不计数 | `..._countsEachRef` |
| M6 | ③ 定义域收窄 | 格属性 `Unknown` 静默跳过 | `..._throws_incompleteSource` |
| **M8** | ③ 定义域收窄 | `Ordinal` → 文化敏感 | `..._caseVariant_...` |
| **M9** | ⑤ 阈值边界 | 邻接 `min` 阈值失效 | `..._requiresBoth` |
| **M10** | ⑤ 阈值边界 | 删 `int.MaxValue` 守卫 | `..._noPhantomNeighbour...` |
| **M11** | ③ 定义域收窄 | 停用「type 为空」throw | `..._emptyType_throws` |
| **M12** | ③ 定义域收窄 | 删 `traits` null 守卫 | `..._nullArguments_...` |
| **M13** | ③ 定义域收窄 | `cell.Z` 写死 0 | `..._zIsPassedThrough_...` |
| M7(手动) | ⑤ 一次性 / 无缓存 | 引入跨调用缓存 | `..._noCache_acrossDifferentLayouts` |
| **M14**(手动) | ⑤ 一次性 / 无缓存 | 引入**按引用分键**的缓存 | `..._rebuiltInputs_...` |
| **M15**(末轮补) | ③ 定义域收窄 | `+y` 单向 → **双向** | `..._neighbourOnMinusY_...` |

**实跑结果**(由驱动输出回填,不预先写):

| 变异 | 预期 | 实际 | 判决 |
|---|---|---|---|
| M1 | 2 条多匹配测 | 2 条 | **EXACT** |
| M2 | 4 条 | 4 条 | **EXACT** |
| M3 | `neighbourOnMinusX` | 同 | **EXACT** |
| M4 | 2 条 ROOM_NONE 测(**诊断断言**) | 2 条 | **EXACT** |
| M5 | `..._countsEachRef` | 同 | **EXACT** |
| M6 | `..._throws_incompleteSource` | 同(第 1 跑因变异形态不合法而零 XML,换形后恰 1 条 —— 见 `impl-s17-6-room-matcher-2026-10-21.md` §3.3) | **EXACT**(换形后) |
| M8 | `..._caseVariant_...` | 同 | **EXACT** |
| M9 | `..._requiresBoth` | 同 | **EXACT** |
| M10 | `..._noPhantomNeighbour...` | 同 | **EXACT** |
| M11 | `..._emptyType_throws` | 同 | **EXACT** |
| M12 | `..._nullArguments_...` | 同 | **EXACT** |
| M13 | `..._zIsPassedThrough_...` | 同 | **EXACT** |
| M7(手动) | `..._noCache_acrossDifferentLayouts` | 同 | **EXACT**(手动,`sha256sum -c` 还原) |
| M14(手动) | `..._rebuiltInputs_...` | 同 | **EXACT**(手动) |
| **M15** | `..._neighbourOnMinusY_...` | 同(55 / 54 / **1**) | **EXACT** |

**合计 15/15 EXACT**;全部还原经 `sha256` 逐字节校验 ✓;零 `FALSIFY` 残留。
M15 原件 = `unity/Logs/falsify-M15-plusYTwoDirections.xml`(2026-10-21 14:42)。

> ⚠️ **M6 的一处如实登记**:首跑变异形态(`if (false) throw …`)导致 **switch 段落穿透 =
> 编译失败**,驱动判「零 XML」计逃逸。**这不是判别力缺口,是变异形态不合法** ——
> 换形(「`Unknown` 单开 `break`」)后恰 1 条转红。教训已写进 spec 的 `_note_on_form`。

**接线面另有 6 条变异(V1–V6)**,结果见 `impl-s17-6-wiring-2026-10-21.md` §四:
**5/6 恰如预期 + 1 条过红(V6,判别力成立但精确性不符)**。

---

## 四、残余(禁借绿)

- ~~**B-3 的 `+y` 侧**~~ —— ✅ **2026-10-21 末轮已解**(spec `M15-plusYTwoDirections`)。
  原文保留:`+x` 有专属变异(M3),`+y` **没有**;M2 的红集里含 `neighbourOnMinusY`,
  但那是**合取收窄**的副产物,不是 `+y` 方向守卫的专属反证。
  **补证**:M15(`+y` 单向 → 双向)⇒ `total=55 passed=54 failed=1`,**唯一红恰为**
  `test_clause_adjacency_neighbourOnMinusY_doesNotMatch` ⇒ **判别力专属且精确**。
- **~~B-11 的端到端组合~~** —— ✅ **已解**(§二 B-11)。AC-24-03 的两半现在同一测试里一起验。
- **B-11b:布局来源是裸 `StructureInstanceRegistry`,不是 `World`** —— `TD-building-01` 的判据逐字写
  「判据 = **真 `World`** 端到端:`Place → IsEffectivelyWalkable == false` / `Remove → true`」,
  而 e2e 用的是 `new StructureInstanceRegistry()` + 真 `StructureWriter.Place`(不经 `World`)。
  ⇒ **注册表侧已真**,**`World` 侧仍未**。`IsEffectivelyWalkable` 的端到端判据本轮**未验**。
  **不标已证**;该缺口使 `TD-building-01` 的「读侧」只能算**部分解除**。
- **`test_purity_*` 的不可观测面** —— 一个**按值正确分键**的缓存(键 = 布局内容 + 表内容)
  结果逐位相同 ⇒ 不可观测。本轮的 M7 / M14 只能钉住「泄漏」与「按引用分键」两种失败形态。
- **三路专家各自的「未及核查项」** —— 见各草稿文件末节;本轮**未逐条补跑**
  (尤其:A 路未读 GDD 原文、B 路未读证伪日志原件、C 路未实跑测试)。

---

## 五、接线轮(与评审并行落地)

见 `impl-s17-6-wiring-2026-10-21.md`(独立原件)。

---

## 六、本轮的性质声明

⚠️ **本次评审评的是补齐轮时点的代码**。原实现轮的代码**未被评审**
(`review-mode = lean`,当时如实登记「Code Review 未跑」)。
按 `.claude/docs/review-workflow.md` §不可追补:**本报告不追认原判定** ——
它给出的是**新**判定。原实现轮的落地与证伪记录仍然有效(它们不是评审),
但「story-006 的代码从未被评审」这一事实在本轮之前成立,**不得**因本轮存在而回溯改写。
