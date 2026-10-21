# C-1 门链接线判据 —— 实现与证伪证据原件

**日期**:2026-10-21
**任务**:C-1(sprint-16 QA 签核件 §7 条件)+ `TD-gate-01` + `TD-gate-02`
**基线溯源三元组**:截至 `2026-10-21 01:01 CST` · 批次 `C-1 实现轮(全量 EditMode)` · commit `f6cdaaf`(工作树含本件未提交改动)

> ## ⚠️ 行号订正(2026-10-21 后补 · 原文不删)
>
> **本件全文出现的 `AssemblyGates.cs:130` 现为 `:139`。**
>
> 病因:C-2 接线轮(`fdfa705`)在 `RunAll` 里 `:130` 附近插入两行
> (`EcozoneIntegerGates.RunAll()` / `EmergencyIntegerGates.RunAll()`)加注释,
> `FsWriteBoundaryGate.RunAll(".")` 那条接线整体下移 **9 行**。
> **接线本身没变、没被删** —— 只是位置变了。
>
> ⚠️ **这是静默过期**:本件正文里的 `:130` 读起来仍然通顺,而按它去源码里找会**找到一行注释**
> 而不是接线。复算的人如果不先核对原文,会把「行号漂移」误读成「接线被删」。
>
> **复算入口以 spec 为准**(已同步修正并留 `_lineHistory`):
> `tools/falsify/specs/c1-gate-wiring.json` 的 `F1-removeFsWriteWiring`。
> `python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/c1-gate-wiring.json --dry-run`
> ⇒ 3 个变异前提全部成立(2026-10-21 实测)。
>
> **影响面**:仅行号。§4.1 的 `total=14 passed=11 failed=3` 与三条 RED 名单**不受影响**
> —— 变异内容与断言逻辑一字未变,改的只是「打在哪一行」。
> 原文四处 `130` **均保留**,以此注为准:§一「原判定」的实测代码块 · §四 变异总表 F1 行 ·
> §4.1 F1 代码块首行 · §五 复算命令的 `sed` 行。
> ⚠️ **此处刻意不写行号** —— 写行号的注会随后续编辑一起漂移,正是本注要防的那件事。

---

## 一、原判定(为什么需要这一件)

`production/qa/qa-signoff-sprint-16-2026-10-21.md` §7 条件 **C-1** 逐字:

> 给 `AssemblyGates.RunAll` 门链补**反向判据**(`TD-gate-01`)……**补上之前,`FsWriteBoundaryGate`
> 只能记「本体已证、接线未证」,不得在 `/gate-check` / M3 判定 / 构建前门说明里写成「写盘边界门已生效」**

**病根不是「少写了一个门」,是判据有方向性。** 实测(S16-4 QA 轮):

```
# 备份 → 删 AssemblyGates.cs:130 → 跑 --filter FsWriteBoundaryGateTest
EXIT=0
{'total': '17', 'passed': '17', 'failed': '0'}
```

> ⚠️ `:130` 是**当时**的行号,今为 `:139`(见文首「行号订正」注)。

门从门链掉线,**17 条门测试 + 2 条端到端 = 19 条判据一条都不红**。

原因在 `assembly_gate_runall_test.cs:31` 断的是 `errs.Count == 0` —— **单调判据**:
删一个门只会让 `errs` 变少,永远不会让它变多。「零红」只覆盖「多了不该有的红」,
**对「少了一个门」完全失明**。

---

## 二、设计:为什么判据必须住测试,不能住生产门

若做成 `GateWiringGate` 并接进 `RunAll`,它就在检查「自己是否在链上」——
**它自己被删的那一次,也正好是它不再运行的那一次**。「谁监督监督者」的递归必须有底,
而底只能落在**独立入口**。⇒ 这是**测试缺口**,不是门缺口。

**判据 = 双向差集,清单手写**:

```
每个强制入口:  源文本实际调用的门  ↔  EntryWiringManifest(手写)
               ① 源有、清单无 ⇒ 新接线未声明
               ② 清单有、源无 ⇒ 门从链上掉线   ← RunAll 端到端测试抓不到的方向
每个门类:      目录里声明的 public static class ↔ ClassConsumerManifest(手写)
               新加门类而不登记 ⇒ 红(TD-gate-02 的「逐门核」)
```

---

## 三、落地

| 文件 | 行数 | 作用 |
|---|---|---|
| `unity/Assets/Tests/EditMode/Sim/gate_chain_source.cs` | 350 | 源文本解析器(剥注释/字符串/字符字面量 + 花括号配平 + 三入口抽取) |
| `unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs` | 597 | 手写清单 + 14 条断言 |
| `tools/falsify/specs/c1-gate-wiring.json` | — | F1/F2/F3 变异规格 |
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | +10 行注释 | 订正 `:1197` 的过期 reload 说明(**零代码改动**) |

**14 条断言**:

| # | 测试 | 面 |
|---|---|---|
| 1 | `test_entryWiring_matchesManifest_bidirectionally` | **核心**:三入口 × 双向差集 |
| 2 | `test_extractor_actuallyExtractsKnownMembers` | 反空转守卫(防 `0 == 0` 假绿) |
| 3 | `test_extractor_ignoresCommentedOutCalls` | 词法:注释里的调用不算 |
| 4 | `test_extractor_ignoresCallsInsideStringLiterals` | 词法:报文里的门名不算 |
| 5 | `test_methodBody_unknownSignature_returnsNull` | 「解析失败」与「真的没调用」可分 |
| 6 | `test_classManifest_coversEveryDeclaredClass` | 全量判决(正向) |
| 7 | `test_classManifest_hasNoPhantomEntries` | 全量判决(反向) |
| 8 | `test_classManifest_hasNoDuplicateClassNames` | 重名会让双向差集静默吞一条 |
| 9 | `test_classManifest_everyEntryNamesAConsumer` | Consumer 空 = 未判决 |
| 10 | `test_classManifest_chainKindEntriesAreActuallyWired` | Kind=Chain 须真是事实 |
| 11 | `test_classManifest_libraryAndTestConsumersAreRealReferences` | Consumer 路径须真引用该类 |
| 12 | `test_classManifest_menuConsumersMatchMenuItemAttributes` | 菜单路径须与 `[MenuItem]` 逐字一致 |
| 13 | `test_knownTestDrivenBlockingGaps_areStillAccuratelyRecorded` | 缺口补齐后登记须同步删 |
| 14 | `test_knownTestDrivenBlockingGaps_agreeWithManifestKind` | 登记与 manifest 须一致 |

### 3.1 23 个类的判决分布(逐条实测,非推断)

| Kind | 数量 | 类 |
|---|---|---|
| **Host** | 1 | `AssemblyGates` |
| **Chain** | 6 | `DiagnosisBoundaryGates` · `FsWriteBoundaryGate` · `InputBoundaryGates` · `MedConsFloatGate` · `PrescriptionWriterGates` · `WriterExistenceGate` |
| **Library** | 9 | `AudioAssetGroupGates` · `AudioEventTableGates` · `ConservationGates` · `DrugProfileGates` · `ItemDbValidation` · `ItemValidationGates` · `MixerTopologyGates` · `RecipeValidationGates` · `TextureBindingGates` |
| **TestDriven** | 4 | `EcozoneIntegerGates` · `EmergencyIntegerGates` · `PodTypeScanner` · `PresentationDtoGuard` |
| **Menu** | 2 | `SkeuomorphicUiGates` · `RoslynAnalyzerLabel` |
| **Tool** | 1 | `MixerAssetGenerator` |

> ⚠️ **「不是所有门都该在链上」是本件的判决结论,不是借口。** IL 级门(Cecil + 真 DLL)
> 放不进 reload 路径(会读陈旧产物)—— 那不是缺陷是设计。本枚举的作用是让每一种形态
> **都必须有具名的强制点**,而不是消失在无人区。

### 3.2 `TD-gate-02` 的逐门核结论 —— 两个真缺口

| 类 | AC | 现状 |
|---|---|---|
| **`EcozoneIntegerGates`** | **AC-6-22 [BLOCKING]** | 只有 EditMode 测试驱动,**无构建期强制点** |
| **`EmergencyIntegerGates`** | **AC-10-02 / AC-10-03 [BLOCKING]** | 同上 |

已登记入 `KnownTestDrivenBlockingGaps`。该表**不是白名单**(不豁免任何检查),
而是把缺口**具名**:`test_knownTestDrivenBlockingGaps_areStillAccuratelyRecorded`
断言的正是「它们**仍然**是缺口」—— 一旦有人把其中一条接进 BuildGate,该断言转红并要求同步删行。
⇒ 缺口不会被静默补齐,也不会被静默遗忘。

> ✅ **2026-10-21 更新(本件写完之后):两门已接进 BuildGate,本节的状态列已过时。**
> 用户裁定「接 —— 复刻 b5 先例」,两门现同时在 `RunMenu` 与 `OnPreprocessBuild` 上,
> `Kind` 由 `TestDriven` 改判 `Chain`,本表所记的上方两条「现状」不再成立。
> 详见 `docs/tech-debt-register.md` §TD-gate 结案记录的「2026-10-21 已裁并已接线」小节
> (含**我自己把不接理由写错一次的完整留痕**)。
> ⚠️ **原文不删** —— 它记录的是 C-1 当时的事实判断,当时确实成立。

⚠️ **签核件的措辞已由此订正**:`TD-gate-02` 原文记「`EcozoneIntegerGates` 等**未逐门核**」。
现实测:它**确实不在链上**,且**不可能是**「掉线」—— 它**从未上线**。两者处置不同
(掉线 = 恢复调用;从未上线 = 决定是否要接),故 `Kind` 单列 `TestDriven` 而非 `Chain`。

---

## 四、证伪轮(三向各一条)

判据 = 每个变异**恰如预期**转红(`exactRed`),且跑完后受保护路径零 diff。

| # | 变异 | 打在哪 | 转红(实测) |
|---|---|---|---|
| **F1** | 删 `AssemblyGates.cs:130` ⚠️**今为 `:139`** | **生产侧**(真实接线) | 3 条 |
| **F2** | 从 `RunAll` 清单删一条声明 | **清单侧** | 1 条 |
| **F3** | 停用解析器(恒返回空集) | **判别力源头** | 5 条 |

### 4.1 F1 —— 方向「清单有、源无」

```
施加 F1-removeFsWriteWiring:unity/Assets/Editor.Tools.Gates/AssemblyGates.cs:130
  unity 退出码 = 8(红即非零,属预期)
  total=14 passed=11 failed=3
    RED: test_classManifest_chainKindEntriesAreActuallyWired
    RED: test_entryWiring_matchesManifest_bidirectionally
    RED: test_extractor_actuallyExtractsKnownMembers
  ✅ 恰如预期转红
```

> ⚠️ **上面这份是当时的原始输出,一字未改** —— 其中 `:130` 是**跑的那一刻**的行号。
> 今日复算请用 `:139`(见文首「行号订正」注)。**改的是位置,不是结果**:三条 RED 名单与
> `14/11/3` 的计数不受影响。

三条同时红的**逐条论证**(不是「多红了就放宽预期」):

| 红的测试 | 为什么**应该**红 |
|---|---|
| `bidirectionally` | 清单声明 `FsWriteBoundaryGate.RunAll`,源没了 ⇒ 核心方向红 |
| `chainKindEntriesAreActuallyWired` | 该类 `Kind=Chain`,链上已无它 ⇒ 声明与事实不符 |
| `actuallyExtractsKnownMembers` | 该测 **`:324` 逐字断言**「`FsWriteBoundaryGate.RunAll` —— 这正是 C-1 立案的那条线」;删掉必红 |

> ⚠️ **这正是 S16-4 QA 轮那个变异。** 当时的结果是 **17/17 全绿**;现在是 **3 条红**。
> 同一操作,判据从「完全失明」变为「三重报警」。

### 4.2 F2 —— 方向「源有、清单无」

```
施加 F2-dropManifestEntry:...gate_wiring_manifest_test.cs:85
  total=14 passed=13 failed=1
    RED: test_entryWiring_matchesManifest_bidirectionally
  ✅ 恰如预期转红
```

**这条证明「双向」二字的实质**:若判据只查「清单 ⊆ 源」(单向),删清单条目**不会**红 ——
因为源侧仍然包含它,没有「源多出来的东西」。只有双向差集才能抓住「声明少了一条」。

### 4.3 F3 —— 判别力源头

```
施加 F3-disableExtractor:...gate_chain_source.cs:237
  total=14 passed=9 failed=5
    RED: test_classManifest_chainKindEntriesAreActuallyWired
    RED: test_entryWiring_matchesManifest_bidirectionally
    RED: test_extractor_actuallyExtractsKnownMembers
    RED: test_extractor_ignoresCallsInsideStringLiterals
    RED: test_extractor_ignoresCommentedOutCalls
  ✅ 恰如预期转红
```

五条全红是**灵敏度正确**:解析器恒返回空集 ⇒ 凡依赖解析面的断言全红。
⚠️ 其中两条词法夹具(注释 / 字符串)红的原因是**解析器被整体停用**,不是词法判错 —— 如实登记。

### 4.4 还原与残留

```
──── 还原:SHA-256 逐字节校验通过 ────
──── 判据二:受保护路径**指纹未变**(证伪轮不留痕)────
✅ 指纹未变 —— 受保护路径:unity/Assets/AddressableAssetsData, unity/Assets/Audio

══ 证伪轮通过:3 个变异全部恰如预期 ══
```

手工复核:`grep -c "FALSIFY-F"` 对三个曾被变异的文件 = **0 / 0 / 0**。

> ⚠️ **2026-10-21 订正(`TD-gate-03`):这一句是手工补充复核,不是驱动判据。**
> 驱动的还原判据是 `restore_all()` 的**逐文件 SHA-256 比对**(见上「还原」段与 §4.5 的哈希表),
> 它**不依赖任何标记名** ⇒ 换 `FALSIFY-QA` 之类的标记同样抓得住(因为压根不看标记)。
> 原文不删,保留作当时的操作记录;判据权以 SHA-256 为准。

### 4.5 保留的 XML 原件(可复核)

| 变异 | XML | 结果 |
|---|---|---|
| F1 | `unity/Logs/falsify-F1-removeFsWriteWiring.xml` | `14 / 11 / 3` |
| F2 | `unity/Logs/falsify-F2-dropManifestEntry.xml` | `14 / 13 / 1` |
| F3 | `unity/Logs/falsify-F3-disableExtractor.xml` | `14 / 9 / 5` |

**还原后逐文件 SHA-256(与变异前逐位相同)**:

```
22049a17…  unity/Assets/Editor.Tools.Gates/AssemblyGates.cs
57d74d9e…  unity/Assets/Tests/EditMode/Sim/gate_chain_source.cs
64c3ddc0…  unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs
```

> ⚠️ **纪律注记**:首轮证伪用的是**不加 `--keep-logs`** 的命令,驱动按默认行为删掉了 XML。
> ⇒ 那一轮「通过」在磁盘上**无原件**。本件随后以 `--keep-logs` **重跑一遍**才落下上表。
> 两轮结论逐条相同(3 条 / 1 条 / 5 条),但**只有第二轮有原件** —— 承
> `coding-standards.md`「commit message 摘要不构成原件」的同款口径:**没有原件的判据不算已证**。

---

## 五、全量回归

**基线溯源三元组(本批)**:截至 `2026-10-21 01:03 CST` · 批次 `C-1 实现轮(全量 EditMode)` · commit `f6cdaaf`(工作树含本件未提交改动)

> ⚠️ 时刻取自 XML 的 `mtime`(`stat -c '%y' unity/Logs/c1-full-editmode.xml` = `01:03:12`),
> 不是凭记忆填的 —— 首版曾误写 `01:32`,已订正。

| 套件 | XML | 实测 | 对照基线(同带三元组) |
|---|---|---|---|
| EditMode | `unity/Logs/c1-full-editmode.xml` | `4085 / 4049 / 0 / 36` | `4071 / 4035 / 0 / 36` @ `2026-10-21 00:23` · sprint-16 收口轮 · `f6cdaaf` |

### 5.1 对账

```
Δtotal  = 4085 − 4071 = +14
Δpassed = 4049 − 4035 = +14
failed     0 → 0
skipped   36 → 36
```

**等式 `Δtotal = Δpassed = 新增条数 = 14` 成立。**

### 5.2 差集实测(不等式的独立判据)

```
本次唯一测试名 4046   基线唯一测试名 4032
新增 14               消失 0

新增 14 条 = GateWiringManifestTest.* 全体:
  + test_classManifest_chainKindEntriesAreActuallyWired
  + test_classManifest_coversEveryDeclaredClass
  + test_classManifest_everyEntryNamesAConsumer
  + test_classManifest_hasNoDuplicateClassNames
  + test_classManifest_hasNoPhantomEntries
  + test_classManifest_libraryAndTestConsumersAreRealReferences
  + test_classManifest_menuConsumersMatchMenuItemAttributes
  + test_entryWiring_matchesManifest_bidirectionally
  + test_extractor_actuallyExtractsKnownMembers
  + test_extractor_ignoresCallsInsideStringLiterals
  + test_extractor_ignoresCommentedOutCalls
  + test_knownTestDrivenBlockingGaps_agreeWithManifestKind
  + test_knownTestDrivenBlockingGaps_areStillAccuratelyRecorded
  + test_methodBody_unknownSignature_returnsNull
消失 0 条
```

> **「消失 0」是等式之外的独立判据** —— 排除「新增 14 同时坏 14、净增恰好对得上」的掩盖形态。

> ⚠️ 「唯一测试名」4046 与「总数」4085 差 **39**,是**同名测试在不同 Fixture 下重名**所致
> (基线 4032 对 4071 同样差 39 ⇒ 结构一致,不是本次引入的形变)。
> 差集比对用唯一名是**正确做法**:同名测试在基线与本次之间无法用名称区分,
> 但本次新增的 14 条名称均为新出现的,故差集结论不受重名影响。

### 5.3 ⚠️ 本批未跑 PlayMode

`PlayMode` **NOT RUN**。理由:本件**零运行时改动**(全部改动是测试件 + 一处注释),
PlayMode 覆盖的是播放面装配,不受影响。**但这是判断,不是实测** —— 如实登记,
若下游需要 PlayMode 证据,须另跑(`sprint-16` 收口轮的 `197/197/0/0` 仍成立:
本件未触及任何 PlayMode 依赖)。

---

## 六、实现期抓到并修掉的 bug(如实登记)

### 6.0 首轮证伪轮的 `expectRed` 漏列 —— 「多红了就放宽预期」的诱惑

首轮证伪轮**未通过**(退出码 1),原因**不是变异没打中**,而是我的 `expectRed` 清单漏列了
会一起变红的测试:F1 我写 2 条、实得 3 条;F3 我写 2 条、实得 5 条。

**处理方式 = 逐条论证「它们为什么**应该**红」,而不是直接把实际结果抄进 `expectRed`。**
论证见 §4.1 / §4.3(每条红的测试都指向了具体断言与其 `file:line`)。
论证成立后才更新 spec,并在 spec 里留下 `_expectRed_justification` 字段记录理由。

> ⚠️ **这一步的方法论意义**:`exactRed` 的本意是「转红集合**恰等于**预期集合」。
> 若把「实得」直接当「预期」,**exactRed 就退化成 tautology** —— 任何红都算对。
> 本件坚持先论证再更新,正是为了避免把它变成「照着结果写期望」的假判据。

### 6.1 `IsGateClassName` 的 `Gates` 漏判 —— 由反空转守卫当场抓住

首版 C# 实现只判 `"Gate"` 后缀:

```csharp
name.EndsWith("Gate")   // "DiagnosisBoundaryGates".EndsWith("Gate") == false !
```

后果:**全部门类(`*Gates`)被漏光**,只剩 `FsWriteBoundaryGate` / `MedConsFloatGate` 这类单数名可见。

**这个 bug 是我自己在 Python 原型里修过、却没同步到 C# 的** —— 原型阶段已发现
`"Gates".EndsWith("Gate")` 恒假并改成 `Gate || Gates`,写 C# 时凭记忆写回了错的版本。

**抓到它的是 `test_extractor_actuallyExtractsKnownMembers`** —— 首跑 3 红:
```
Failed test_extractor_actuallyExtractsKnownMembers
  提取器抽不到 BuildGate(构建前门)的接线 —— 该入口在嵌套类里,解析须穿透
  Expected: collection containing "DiagnosisBoundaryGates.RunAll"
  But was:  < "CheckToFloatCallsites", "RunAll", "RunAudioBoundary" >
```

⇒ **反空转守卫的价值在此实测兑现**:若无它,双向差集会在「两边都只看到 3 个门类」上
**保持绿色**,而判据已静默失去三分之二的覆盖面。这正是 `0 == 0` 假绿的教科书形态。

### 6.2 修复

```csharp
public static readonly string[] GateClassNameSuffixes = { "Gate", "Gates" };
```

修复后 14/14 全绿。

---

## 七、已知残余风险(**本件不隐瞒**)

### 7.1 清单「是否手写」无机械判据

`TD-gate-01` 的修法条文逐字要求「**清单禁从 RunAll 体反射生成**(否则恒等 ⇒ 零判别力)」。
本件照做了(两份 manifest 都是手写常量),但**这一条没有机械判据** ——
「这份常量是不是手写的」无法从产物反推,生成出来的常量与手写的长得一样。

**把清单改成 `GateChainSource` 的产物不会让任何断言转红,只会让本判据静默归零。**

可获得的最近似证据:① `test_extractor_actuallyExtractsKnownMembers`(提取器确实在抽);
② `test_classManifest_hasNoPhantomEntries`(清单不随源自动膨胀)。
⇒ 已写入代码头注供后来者看见。**这是本件的已知残余,不是已解决项。**

### 7.2 F3 未在叠变异态实测

本驱动不允许叠变异(每条从干净基线出发)⇒ F3 停在「解析器失效」这一层。
它与「接线判据失效」是同一件事的合成形态,但**未在真删线的状态下叠加实测解析失效** —— 不借绿。
(与 S16-4 的 F3 同款局限,同款登记。)

### 7.3 解析器是源文本级,不是语义级

刻意不用 Roslyn(承 b4/b6 同款口径)。已知漏报面:宏(`#if` 内外的调用都会被数进来)。
属**误报方向(偏安全)**:只会让判据更严,不会让门静默掉线。

---

## 八、可复算命令(第三方可直接重跑)

```console
# ① 测试全绿
$ cd unity && unity test "$PWD" --mode EditMode --output ../unity/Logs/c1-run2.xml --filter GateWiringManifestTest
  ⇒ total=14 passed=14 failed=0

# ② 证伪轮(F1/F2/F3 三条 exactRed + 还原校验)
$ python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/c1-gate-wiring.json --keep-logs
  ⇒ ══ 证伪轮通过:3 个变异全部恰如预期 ══

# ③ 本件的核心判据:删一行 ⇒ 必红(S16-4 当初的变异)
$ sed -i '130s/.*//' unity/Assets/Editor.Tools.Gates/AssemblyGates.cs   # 备份先行 ⚠️ 今须写 139s
$ cd unity && unity test "$PWD" --mode EditMode --output /tmp/x.xml --filter GateWiringManifestTest
  ⇒ failed=3(S16-4 时:failed=0)

# ④ 清单封闭性:23 类全覆盖
$ grep -c 'ClassName = ' unity/Assets/Tests/EditMode/Sim/gate_wiring_manifest_test.cs
  ⇒ 23
$ grep -h 'public static class' unity/Assets/Editor.Tools.Gates/*.cs | wc -l
  ⇒ 23(需先剔除头注里的示例行;实际由 test_classManifest_coversEveryDeclaredClass 保证)
```

---

## 九、残余(不在本件裁决面)

| # | 项 | 归属 |
|---|---|---|
| 1 | `EcozoneIntegerGates` / `EmergencyIntegerGates` **接不接进 BuildGate** | ✅ **2026-10-21 已裁「接 —— 复刻 b5 先例」并已接线**(本件写完之后的事)。~~须另裁~~ ⇒ 见债册 §TD-gate 结案记录的「已裁并已接线」小节。⚠️ 我在该轮**把不接的理由写错了一次**(说「BuildGate 无等价守卫」,实测 `RunAudioBoundary` 早在里面且守卫就在其内部)—— 完整留痕见该节 |
| 2 | `RunMenu` 重复调用 `CheckPayloadRefCallsites`(RunAll 内已调)⇒ 同一批红行打印两次 | 无害冗余;已在 manifest 注释里点名,修复归 Editor.Tools.Gates 维护轮 |
| 3 | IL2CPP 三格(ADR-012 矩阵) | **NOT RUN**(本机仅 Mono) <br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。 |

---

> **不经用户指令不 commit。**
