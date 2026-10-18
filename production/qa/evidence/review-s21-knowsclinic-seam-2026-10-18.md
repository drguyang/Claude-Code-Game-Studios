# 评审原件 · story-021(`knowsClinic` 注入缝 · S15-3)

> **对象**:`production/epics/audio-system/story-021-knowsclinic-injection-seam.md`
> **代码面**:`Gameplay.Boot/{ClinicKnowledgeDirectory,PatientCueDriver,CompositionRoot,CompositionRootServices}.cs`
> · `Gameplay.Presentation/PatientAI/ClinicKnowledge.cs` · `Tests/EditMode/Audio/knowsclinic_seam_test.cs`
> **日期**:2026-10-18
> **方式**:两路并行评审(Unity/C# 专家 + QA 可测性),各自草稿增量落盘
> **原件性质**:本件评的是**评审当时**的代码;后续修复已并入并在 §五 逐条结账。
> **⚠️ QA 路在第 10 轮上限被停、未交正式报告** —— 其草稿
> `tmp-review-s21-qa.md`(12 条判定)已非空且覆盖清单过半,按 `/code-review` Phase 7b ③
> 的判据视为有效交付,由编排方并成本件并删除草稿。本件**不**宣称 QA 路完成了自检。

---

## §一 裁定汇总

| 级别 | 条数 | 处置 |
|---|---|---|
| **BLOCKING** | **0** | — |
| WARNING | 8 | 本轮修 2(W-1 / W-2),登记 6 |
| INFO | 4 | 登记 |

---

## §二 WARNING 明细与处置

| ID | 判定 | 落点 | 处置 |
|---|---|---|---|
| **W-1**(T3 / R1-AC3-empty) | `test_ac021_3_emptyRegistry_…` 的断言宾语是**夹具自造**的 `ClinicKnowledge`,组合根产出的 `services` 只断言了 `Not.Null` ⇒ 对「组合根空表形态」判别力弱 | `knowsclinic_seam_test.cs` | **✅ 已修**:宾语改为 `CompositionRoot.Assemble(...)` 产出的 `PatientCueDriver.KnowsClinicOf`,并补一拍 `OnTickEdge` 作行为面。验:**F8**(把 `KnowsClinicOf` 脱钩成常量 `false`)⇒ 该条红 |
| **W-2**(R2-scope / R2-literal) | 源码门只扫 `Gameplay.Boot` + `Gameplay.Presentation` 两棵,而 AC-021-1 原文是「**全仓**」;实测其余七棵生产树今日虽零违例,但门的面不足 | 同上 | **✅ 已修**:`Tests/` 以外**全部** `unity/Assets/**/*.cs`;下限断言 `scanned > 300`(实测 376 件)。验:**F7**(往 `Sim/World/ModifiableChecker.cs` 塞一条真代码违例)⇒ 该条红,报文点名 `ModifiableChecker.cs` |
| **W-3**(R2-commentstrip) | 原实现只剥 `//` 行注释:会误伤字符串里的 `//`(URL),且不剥 `/* */` 块注释 ⇒ 假红 / 假绿两头漏 | 同上 | **✅ 已修**:新增 `StripComments`(逐字符状态机,区分行注释 / 块注释 / 字符串 / 字符字面量)。⚠️ **无专门证伪**(见 §四 未证项) |
| **W-4**(U5) | 组合根默认 `new ClinicKnowledge(new RegistryClinicKnowledgeSource(new EcozoneRegistry()))`(空表)与 `RegistryClinicKnowledgeSource` 类注「不得静默默认 `None`」**口径拉扯**:同一个「世界未装载」状态,构造器一个抛、组合根一个演 | `CompositionRoot.cs:358-362` | **登记**(设计层)。归**装载面接线轮** —— 届时默认路径改显式 fail-loud 或把 `ClinicKnowledge` 改为必填。当下行为等价于结案前,**不影响本卡** |
| **W-5**(U10) | `IClinicKnowledgeSource` 未定义 `EcozoneId.None` 的语义:空表时 `EcozoneOf` 回 `None`,将来注入真实 POI 查询若把 `None` 当合法区号 ⇒ 静默误判 | `ClinicKnowledge.cs:41-47` | **登记**。当下两缝为 null ⇒ 短路恒假,不会触发;接线轮须在接口注释钉「`None` ⇒ 必假」 |
| **W-6**(U12) | `KnowsClinicAt` 与 `KnowsClinic` 共写同一张 `_homeRegion`,锚点先到先得、第二条无告警 | `ClinicKnowledgeDirectory.cs:70-87` | **登记**。今天只有测试用 `KnowsClinicAt`;`TD-audio-033` ② 收敛时一并处置 |
| **W-7**(U14) | `_knowsClinic(id)` 每 tick 调一次;锚点格越界时 `EcozoneOf` 抛 `ArgumentOutOfRangeException`,会冲出 `OnTickEdge` | `PatientCueDriver.cs:190` → `EcozoneRegistry.cs:293-296` | **登记(实为不可达)** —— 已实测 `WorldLatticeParams.MaxWorldHalfExtent = 1<<29`,而世界格以 `int` 承载,量程 2^31 中越界部分是**不可构造**的锚点;`PresenceRegistry.AddPresent` 不校验格值确属实,但越界格本身就是坏数据。归装载面接线轮一并加防御 |
| **W-8**(R1-AC3-cache / R1-directorynocache) | ① 缓存测走的是 `KnowsClinicAt`(不经 `_locate`),「经定位缝缓存」这条路径没被覆盖;② 「无锚点不缓存」的语义实际是「**首次在场**时求」而不是「首次初始化时求」,后者是 F-13.2 字面 —— 这个解释口径未登记 | `ClinicKnowledgeDirectory.cs:56-70` | **登记**。② 写进本件 §三 的「口径解释」 |

### 口径解释(承 W-8 ②)

F-13.2 说「`HomeRegion` 只在初始化时求一次」。本实现把它落成「**病人首次拿到锚点格时**求一次并缓存」:
在 `knowsClinic` 被问的那一刻,病人若尚未在册(无锚点),**不缓存、判假** —— 否则会先把
「无锚点 ⇒ 假」写死,病人之后再出现也永远求不到真值。代价是:`spawn_anchor` 在病人生命周期里
若发生改变(如跨格),缓存**不会**跟着变 —— 这与 F-13.2「`spawn_anchor` 是静态定义,不回改」一致。
⚠️ 该口径**不在 GDD 逐字内**,是接线期的实现解释,**登记待 13 的走查轮确认**。

---

## §三 INFO

| ID | 内容 |
|---|---|
| U3 | `knowsClinic` 插在 `hasCourse` 之前;现有调用全用具名实参,不破坏旧调用 |
| T2 | 源码门用 `CallerFilePath` 上溯五级找仓根,依赖当前测试目录布局(已有 `Directory.Exists` + `scanned > 300` 两道防线) |
| T7 | `KnownPatientCount` 仅为观测面,收敛轮可收 |
| U15 | 每病人每 tick 一次委托调用 + 一次字典查找,无分配;≤ 24 病人 × 20 Hz,量级可忽略 |

---

## §四 未及核查项(禁借绿)

1. **`StripComments` 无专门证伪**(W-3 修复):本轮**没有**构造「字符串里含 `//`」或「块注释里含违例」的夹具。
   ⇒ 「假红 / 假绿两头」的修复**已落未证**。
2. **视觉侧是否另持行为导演**:已 grep `new PatientBehaviorDirector(` —— 生产侧**唯一**一处 = `CompositionRoot.cs:366`;
   `PatientSpatialDirector` 生产侧**零构造点**(只在测试里用)。⇒ `TD-audio-033` ② 的「两份行为态分叉」
   **今天在生产里并不成立**(视觉侧尚未接),它是**前瞻性**债项,不是当下缺陷。该事实写回债行。
3. **回归**:本轮已跑,**`3765 / 3720 / 0 / 45`**(`unity/Logs/s21-full-editmode.xml`),零失败。

---

## §五 修复轮结账(已落 / 已证 分列)

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| W-1 | ✅ | ✅ | `knowsclinic_seam_test.cs` | §六 F8:把 `KnowsClinicOf` 脱钩成常量 ⇒ `test_ac021_1_compositionRoot_feedsSeamFromTruthSource_notHardcodedFalse` 红 |
| W-2 | ✅ | ✅ | `knowsclinic_seam_test.cs` | §六 F7:往 `Sim/World/ModifiableChecker.cs` 塞真代码违例 ⇒ `test_ac021_1_productionTree_hasNoHardcodedKnowsClinicFalse` 红,点名 `ModifiableChecker.cs` |
| W-3 | ✅ | **✗** | `knowsclinic_seam_test.cs` | `StripComments` 已落,但**无夹具** ⇒ 不得标已证(见 §四 行 1) |
| W-4 | ✗ | ✗ | — | 归装载面接线轮 |
| W-5 | ✗ | ✗ | — | 同上 |
| W-6 | ✗ | ✗ | — | 归 `TD-audio-033` ② 收敛轮 |
| W-7 | ✗(实测不可达) | ✅(可达性已证) | — | `WorldLatticeParams.MaxWorldHalfExtent = 1<<29` < `int` 上限 ⇒ 越界锚点不可构造 |
| W-8 | ✅(登记) | ✅ | 本件 §二「口径解释」 | 文本可复算 |

---

## §六 修复轮证伪(承 §五)

过滤器固定 `DaYiJingCheng.Tests.Unit.Audio.KnowsClinicSeamTest`,每例改 → 跑 → 还原同一命令内完成,还原后 SHA-256 逐字节校验(`restored sha ok`)。

| # | 变异 | 期望红 | 实测 |
|---|---|---|---|
| **F7** | 往 `Sim/World/ModifiableChecker.cs` 塞 `internal static class TextGateProbe { … \"knowsClinic: false\" }`(真代码,非注释) | 源码门 | **红 ×1** ✅(点名 `ModifiableChecker.cs`) |
| **F8** | `KnowsClinicOf` 脱钩(`=> false`) | AC-021-1 主测 | **红 ×1** ✅ |

**两处失败尝试(如实登记)**:
- F7 第 1 版把违例插在 `namespace` 块内的裸位置 ⇒ **编译错误**(exit 6),不是判别力问题;
- F7 第 2 版插在 `/// <summary>` 注释行下方被块注释**内外包住** ⇒ 被 `StripComments` 正确剥掉 ⇒ **不红**。
  第 3 版放进类体内的独立类型才红 —— 前两版是**锚点选错**,不是门坏。
