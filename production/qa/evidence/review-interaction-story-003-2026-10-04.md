# 评审原件 —— interaction-system Story 003(候选集四源构造与整数格输入)

> **对象**: `production/epics/interaction-system/story-003-candidate-set-four-sources.md`
> **日期**: 2026-10-04
> **轮次**: **单轮**(承用户「评审只做一轮」裁决 —— 修复轮后**不**复评)
> **代理**: 结构评审(lead-programmer 口径)+ QA 评审(qa-lead 口径),并行双代理
> **产出物**: `unity/Assets/Gameplay.Presentation/Interaction/{SourceDtos,CandidateSources,CandidateSetLoader}.cs`
> · `unity/Assets/Tests/EditMode/Interaction/{candidate_dto_reflection_test,candidate_sources_test}.cs`

---

## 一、评审对象与判据

| AC | 判据正文 | 落点 |
|---|---|---|
| AC-4-03 | 九个具名输入形状 + `Candidate` 的**全实例字段类型闭包**零 `UnityEngine.*` 位置类型 | 单元 14 条 |
| AC-4-20 | 模态不一致两客户端的**出境集合等值**(被拒意图 = 不存在的出境) | 集成 4 条 |
| AC-4-22 | 开档自带门柜(23 `BakedInitial`,**无流事件**)可被选出并路由 | 集成 2 条 + 装载 3 条 |

对照权威件:GDD `interaction-system.md` 规则二 / ADR-016 §三(判据 = **反射字段类型,不是 grep**)/
ADR-020 §四(玩家格 = 经流确立格)/ ADR-009 §五(身份进流 / 位置表现)/ ADR-015 §三(单一整数格)。

---

## 二、结构评审

**判定**: 未在轮次内交付独立报告原件(代理触顶后经重送请求判决,**未回**)。

**主会话结构复核结论(记录在案,非代理判定)**:

- [x] 生产零 `using UnityEngine`:`SourceDtos.cs` / `CandidateSources.cs` / `CandidateSetLoader.cs` 三件逐行确认
- [x] 装载器**无玩家格形参** —— 取路 (a) 的**结构保证**(不是运行期检查):`pending_cell` 物理上进不了选择路径
- [x] 装载器**不持 `IEventSink`**、不引结算侧类型(AC-4-01/05 承 story 001)
- [x] 四源接口计数 = **四**(`IWorldBakedSource` 三切片合一、`IBakedInitialSource` 四切片合一 —— 刻意不为切片拆接口,否则「四源」会数成「八源」)
- [x] 依赖显式注入(`CandidateSources` readonly struct),无服务定位器 / 单例

⚠️ **原件义务的不满足**:按 `.claude/docs/coding-standards.md`「评审报告原件」条,产出 BLOCKING 判定的评审须落原件。**结构侧本轮无 BLOCKING 判定、亦无代理判定原件** —— 本记录**不**以主会话结论冒充代理评审。该缺口**登记在案**:结构侧实质评审由 QA 代理与本记录的主会话复核共同承担。

---

## 三、QA 评审(ACCEPT —— 附 4 项登记)

**判定**: **ACCEPT**(代理 `a534e9550df6ceb8c` 返回)。

### 原判定(F-1 … F-4)

| # | 判定 | 类型 | 证据 |
|---|---|---|---|
| **F-1** | `test_ac422_negativeFixture_threeSourceLoaderReturnsNone` 的对照**有效** —— 生产 `Load()` 的 Kind 集 = `{BuildSlot, ClinicPanel, Door, Drop, ForageSpot, Patient, PoiCell, Switch, Utensil}`,影子 `ThreeSourceLoader` = `{BuildSlot, Drop, ForageSpot, Patient, PoiCell}`,**差集恰 = 第四源全体** `{ClinicPanel, Door, Switch, Utensil}` | 正面确认 | 两侧 Kind 集对账 |
| **F-2** | `test_ac403` 反射扫描器**非空转** —— 白名单反空转探针到得了底层 int 字段 | 正面确认 | `test_ac403_whitelistMembersAreActuallyVisited` |
| **F-3** | 装箱 `object` 字段的判据**初稿是循环论证**(只扫影子类型,未接生产面)⇒ 须扫**生产** DTO | 🔴 须修 | 见 §四 F-3 |
| **F-4** | 基类继承来的引擎类型字段 —— 展开实现**有**、覆盖**零** | 🔴 须修 | 见 §四 F-4 |

### 登记项(非阻塞,要求落 `Completion Notes`)

- **F-5**:刻意留白的边缘须**登记 + 附理由**(「登记不隐藏」):AC-4-20「A 先闭后开(同 tick 翻转)」/「双方都闭」/ AC-4-22「同格共存 KindPriority 决胜」/「建成 vs 开档等价」/ 6 侧复验器缺席。

---

## 四、修复轮(2026-10-04)

| # | 原判定 | 修复落点 | 验证命令 |
|---|---|---|---|
| **F-3** | 装箱判据循环论证 | **拆两条**:① `test_ac403_noProductionDtoCarriesBoxedObjectField` 扫**生产** `Ac403Subjects`(禁入 `System.Object`,违规集须空);② `test_ac403_negativeFixture_boxedObjectInShadowDtoIsPointedlyRed` 同机器对影子类型**必红**(反空转) | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Interaction"` ⇒ 绿 |
| **F-4** | 基类展开零覆盖 | 加 `ShadowDtoBaseWithEngineType`(基类藏 `Vector3`)+ `ShadowDerivedDtoFromEngineBase`(顶层全合法,唯一违规来自基类)+ `test_ac403_negativeFixture_engineTypeViaBaseClassIsCaught` | 见下**变异证明** |
| **F-5** | 边缘未登记 | 落 `Completion Notes` 的 `Deviations` 六条(**登记不隐藏**);补两条 AC-4-20 边缘夹具(双方都开 / 确立格 vs pending 岔口) | 同上 |

### 变异证明(可红性 —— 判据非纸面)

| 变异 | 改动 | 结果 |
|---|---|---|
| **MUT-base** | 删 `Expand` 的基类展开分支 | **恰** `test_ac403_negativeFixture_engineTypeViaBaseClassIsCaught` **单条红**(55/56),其余全绿 ⇒ 基类展开**确为承重** |
| MUT1–MUT3 | (story-002 轮存量:删 int64 拓宽 / 删键③ / 键③ 改哈希序) | 逐项令对应测试红 |

生产与测试文件在变异后**逐字节还原**(md5 校验)。

---

## 五、最终证据

- **实跑**:`unity/Logs/interaction-s003-r5.xml` = **56/56 green**(单元 14 + 集成 11 + story-001 边界 24 + story-002 全序 7,同 run 零回归)
- **不得借绿**:AC-4-20 / AC-4-22 依赖的对侧(1 的 `ActorCellEntered` 写方链、23 的 `BakedInitial` 表形状、6 的主机复验器)以**注入替身**签本故事的**读方形状**;对侧真身联调归各自 Epic,**本绿不豁免之**
- **未裁项**:chunk 驻留口径(未驻留 POI 进不进候选)= `BLOCKED-BY: OQ-6-8`,夹具两侧都不断言

---

## 六、判定汇总

| 侧 | 判定 | 阻塞项残余 |
|---|---|---|
| 结构 | 无代理原件;主会话复核通过 | 0 |
| QA | **ACCEPT** | 0(F-3 / F-4 / F-5 已修并验证) |

**故事事件**:`Complete`(2026-10-04)。单轮评审,修复轮后**不**复评(承用户裁决)。
