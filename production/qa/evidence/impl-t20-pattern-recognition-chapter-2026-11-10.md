# T20 第一批 —— 同源检测接活 + 章推进(证据原件)

**日期**:2026-11-10
**批次**:T20(用户裁定:丁 = 甲「先接同源检测,再挂章」)
**commit**:`ada07b5a` + 工作树(未提交)
**引擎**:Unity 6.3 LTS(6000.3.24f1)batch EditMode / PlayMode

---

## 1. 本批补的两条「零生产调用点」

| # | 事实(改前) | 后果 |
|---|---|---|
| ① | `PatternRecognitionWriter` **生产构造点 = 0**(全仓只有夹具) | 37 规则八(三例同源 ⇒ 世界级 `PatternRecognized`)在运行期**从未运行**;53 的后果链与 39 的「图样」判定都以它为前提 |
| ② | `ChapterAdvanceWriter.TryAdvance` **零生产者** | 章号恒 0 ⇒ `HookCasePolicy.MaxPerChapter = 1` 等于「整局 1 例」 |

## 2. 改法

### 2.1 数据件(阈值必须可追溯,TR-case-031)

`schema` 只有一枚键:**`pattern_threshold` = 3**(GDD `case-system.md:480` 的 P0 值,安全范围 ≥ 2)。
按 ADR-014 两阶段管线全套落地:

| 环节 | 文件 |
|---|---|
| 源 | `assets/data/case_pattern.json` |
| 阶段 2 绑定器(白名单 / 必填 / `≥ 2`)| `Editor.Tools.Bake/CasePatternBinder.cs` |
| cooked 写方 | `Editor.Tools.Bake/CasePatternCookedWriter.cs` |
| 烘焙入口 | `Editor.Tools.Bake/CasePatternBake.cs` |
| 菜单 | `DataBakeMenu.BakeCasePattern`(45 条产物之一)|
| 运行期解码 + 加载期第二道 `≥ 2` 门 | `Gameplay.Presentation/CasePatternCookedCodec.cs` |
| 装载面 | `AddressablesDataProvider.CasePattern` / `EnsureCasePattern` / `CasePatternAddress` |
| 预载面 | `DataCorePreloader.DataCoreAddresses` |

**为什么不写代码常量**:`TR-case-031` 要的是「改值可追溯(`ConfigVersion` 变),不拒载」。值住在常量里就改不动版本号,`PatternRecognizedPayload.Version`(冻结三元组 3/3)也会失去参照。

### 2.2 冻结三元组 3/3 的版本来源

`version` 取 **`disease_registry.cooked.bytes` 的 `ConfigVersion`**(病种映射表版本),
**不由** 37 的旋钮表派生 —— 两者量纲不同(一个是映射表版本,一个是阈值哈希)。

### 2.3 驱动(`Gameplay.Boot/PatternRecognitionDriver.cs`,新)

```
每个 tick 边沿:
  流里 CaseClosed 条数变了(或首次)⇒ PatternRecognitionWriter.TryRecognize(tick)
  章 = 已识别的 PatternRecognized 条数 ⇒ ChapterAdvanceWriter.TryAdvance(章, sourceId=病种, tick)
```

**两处就地裁定的口径**(不是 GDD 原文,写在这里以免被读成原文):

1. **何时检测** = 出现新的 `CaseClosed`。同源检测的候选集就是已结案病例(F-37.1),
   没有新结案 ⇒ 结果不可能变(与 `TechniqueUnlockDriver` 的「零规则零扫描」同款省法)。
2. **章 = 已识别同源模式条数**(1 = 第一次识破图样之后)。绝对量、从流可重建、单调不减
   ⇒ 与写者「禁倒退」的约束天然相容。叙事宿主到位后若改由剧情推进,只换 `TryAdvance` 调用点。

### 2.4 装配

`CompositionRoot`:仅当 `dataProvider is AddressablesDataProvider`(与 `CarryCap` 同款取法)
⇒ 构造写者 + 驱动;读不到产物 ⇒ **不装配**(不回落常量),`BootRoot` 启动期显式告警。

## 3. 判据

| # | 测试 | 钉住的属性 |
|---|---|---|
| 1 | `test_casePatternBake_repoValue_isThree_andRoundTrips` | 源 3 → cooked → 读回 3,版本号逐位相同 |
| 2 | `test_casePatternCodec_readsProductionArtifact` | 盘上真产物读回 = 3(改 JSON 不重烘 ⇒ 红) |
| 3 | `test_casePatternBinder_rejectsThresholdBelowTwo` | 1 / 0 构建期硬失败(判别力 = 同一份 JSON 只改一个数) |
| 4 | `test_casePatternBinder_rejectsUnknownKey` | 白名单外键具名报出 |
| 5 | `test_threeSameDiseaseCases_recognizePattern_andAdvanceChapter` | **端到端**:两例不识别 → 第三例识别 → 章推进到 1 + 载荷四字段 |
| 6 | `test_repeatedTicks_doNotDoubleWrite` | fires-once:再扫不重发、章不重推 |
| 7 | `test_firstTick_afterLoad_recognizesExistingCases` | 读档后首轮自愈(流里已有三例) |
| 8 | PlayMode `test_patternRecognition_isAssembled_withThresholdFromArtifact` | 真启动序后驱动非 null **且**写者阈值字段 = 3(反射) |

`case_close_driver_test`(T19)与 `pattern_recognition_writer_test`(既有)继续全绿 —— 本批未改它们的被判对象。

## 4. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7385 / 7348 / 0 / 0 / 37` | `2026-11-10 08:47:02Z`–`08:49:07Z` | T20 第一批(定稿树,含两处就地加注) | `ada07b5a` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10 08:50:01Z`–`08:50:22Z` | T20 第一批(定稿树,含两处就地加注) | `ada07b5a` + 工作树 |

基线(T19):EditMode `7377 / 7340 / 0 / 0 / 37` · PlayMode `375 / 370 / 0 / 2 / 3`。
**等式**:`7385 − 7377 = 8` = 新夹具 7 条 + `drift[case_pattern.cooked.bytes]` 参数化 1 条;
`376 − 375 = 1` = PlayMode 新接线测。

## 5. 门抓到的两处漏登记(如实记录 —— 是我漏的,不是门误报)

首跑全量 EditMode 即 **3 红**,全部来自「四条集合必相等」的同型门:

| 红 | 缺什么 | 修法 |
|---|---|---|
| `ProviderWiringGateTest.test_wiring_preload_invokesEveryEnsureMethod` | `DataCorePreloader.DataCoreAddresses` 漏了新地址 | 补一行(该处注释已自陈这是**第六次**同型漏登记) |
| `PipelineGateTest.test_preloader_list_sources_matchPreloadedProducts` | 同上 | 同上 |
| `CatalogFreshnessGateTest.test_catalog_containsEveryDataCoreAddress` | Addressables catalog 未重建 | `DataBakeMenu.BuildAddressablesContent`(45 条) |

补后全量转绿。**这三条门是有效的** —— 它们在本批的第一次运行就抓住了真实遗漏。

## 6. 证伪轮

变异窗口:`2026-11-10 08:40Z`–`08:52Z`;窗口内不编辑被变异文件;还原判据 = SHA-256 逐字节(两件「成功」)。

| 变异 | 改法 | 结果 |
|---|---|---|
| **M1** | 删掉 `_recognizer.TryRecognize(tick)` | `4/7` 通过,**3 红**(三例端到端 / 幂等 / 首轮自愈) |
| **M2** | `_lastCaseClosedCount` 初值 `-1` → `0` | **`7/7` 全绿 —— 未抓住** |
| **M3** | `CasePatternSchema.MinThreshold` 2 → 1 | `6/7` 通过,**1 红**(阈值下界) |
| **M4** | 章推进 `current < patterns` → `current < patterns - 1` | `4/7` 通过,**3 红** |

**M2 未转红的分析(承「证伪轮九种假存活」)**:
`-1` 只是「从未扫过」的意图表达;任何 **≠ 当前条数** 的初值都会让首轮触发扫描 ⇒
该哨兵**不是承重件**,删/改它不改变可观测行为(写者 fires-once 才是语义保证,而它由 M1 覆盖)。
⇒ 已把该注释就地订正为「初值不是承重件」,并在本文件登记;**不得**据此认为「首轮自愈已由初值保证」。

## 7. 未覆盖(禁借绿)

1. **§5.2 规则 2 / 3 / 4**(钩子病例必须可诊断 / 必须指出缺哪门技法 / 不点破地点)未在本批触达 ——
   它们归 `HookCasePolicy` 与呈现层。
2. **章推进的叙事语义**:「一章 = 一个被识破的图样」是**机制口径**;叙事宿主(奇遇)到位后可能改,
   届时只换 `TryAdvance` 的调用点,写者与读侧不动。
3. **`scriptedChains` = null**(GDD F-37.1b 的数据面未建)⇒ P0 全走自然检测;
   脚本三案链一旦有数据,须补 binder + 断言「逐 D 二选一」。
4. **本批不涉及 42 / 39**:玩家**看不到**「识破了图样」的任何反馈 —— 归下一批(丙)。
