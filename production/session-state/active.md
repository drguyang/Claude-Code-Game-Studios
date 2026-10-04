# Session State — 2026-10-03(**当前阶段 = Pre-Production · Sprint 04 Phase 1 ✅ 已收口 · Phase 2 进行中 2/7**)

## 📊 全项目进度总览（2026-10-03 实测重算）

> ⚠️ **本表于 2026-10-03 按各 story 真件逐件重算**(口径:`> **Status**:` 首行 + 体 `**Status**: [x]`)。
> 下表为**实测值**;旧值(124 Complete / 6 Ready / 77 In Progress / 59.9%)系**陈旧转录**,已废。

### 阶段状态

| 项 | 值 |
|---|---|
| **Stage** | Pre-Production |
| **Sprint** | sprint-03 ✅ 已闭(17/17) · **sprint-04 Phase 1 ✅ 已收口** · **Phase 2 进行中 = 2/7 系统(~12/37 SP)** —— 关键路径断于 `interaction-system` |
| **Gate Check** | CONCERNS（2026-09-29 二轮，无 NOT READY 阻塞） |
| **ADRs** | 28/28 Accepted |
| **P0 GDDs** | 31/31 Approved |
| **Commits since 09-22** | 375 |

### Epic 故事进度

> **列语义(防混计 · 2026-10-03 明写)**:四列为**互斥且穷尽**的分类,口径 = story 件 `> **Status**:` 首行。
> - **Complete** = 首行含 `Complete`(或体 `**Status**: [x]`)
> - **Ready** = 首行为 `Ready` **或 `In Review`** —— 二者皆「story 已实现但 epic 未收口」,
>   本表**不分开列**(此为既定口径,非疏漏;`In Review` 的明细读「备注」列)
> - **In Progress** = 首行含 `In Progress`(**实测恒为 0**)
>
> ⚠️ **旧表误读的成因**:把「`In Review`」当成 `In Progress` 计 ⇒ 虚报 77。
> **`In Review` ≠ `In Progress`** —— 前者是「已实现待收口」,后者是「实现进行中」。
> 同理 **`In Review` ≠ `Complete`**(「不得借绿」)。

| Epic | Stories | Complete | Ready | In Progress | 备注 |
|------|---------|----------|-------|-------------|------|
| **audio-system (44)** | 14 | **14** | 0 | 0 | ✅ 全收口 |
| **skeuomorphic-ui (42)** | 18 | **18** | 0 | 0 | ✅ 全收口 |
| **skill-system (30)** | 8 | **8** | 0 | 0 | ✅ 全收口 |
| **telemetry-analytics (51)** | 10 | **10** | 0 | 0 | ✅ 全收口 |
| **input-system (3)** | 13 | **13** | 0 | 0 | ✅ 全收口 |
| **item-database (21a)** | 12 | **12** | 0 | 0 | ✅ 全收口 |
| camera-viewpoint (2) | 6 | **6** | 0 | 0 | ✅ 全收口(2026-10-03) |
| casebook (39) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| case-system (37) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| clinic-machine (24) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| combat-weapons (25) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| death-respawn (29) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| diagnosis-system (8) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| disease-simulation (9) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| emergency-procedures (10) | 7 | **7** | 0 | 0 | 🔶 **story 7/7 Complete;EPIC 未收口**(A1/A2/A6/B4/C4/C5 已闭;✅ 评审原件两份均在库,**旧记「评审原件缺」为方向性错记**;**真实残留 = D1/D2/D3 文档对齐 + 实跑测试套件**(round2 自陈未实跑,「0 红」系读源码而非执行)) |
| enemy-ai (27) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| foraging (17) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| interaction-system (4) | 7 | **7** | 0 | 0 | ✅ **全收口 2026-10-04**(001…006 + **007 装载接线 · NR-1 已闭**;**未闭登记 = NR-2 真表数值轮 · NR-3/4/5 三条 `[L]` 走查 · NR-6 `OQ-17-3` · 007 新增 4-DC-4①/跨会话/§三其余规则 等七条 NOT-RUN**) |
| inventory-items (20) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| medical-consequences (53) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| modular-building (23) | 7 | **7** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（C1/C2/N-r1/C8-ID 全闭 · 本轮 72/72 绿 · 全量 2204/2163/0红，`9bb912b`+`bfa6234`;**未闭登记 = N-r2 生产装配根 + AC-23-09 跨平台签名**） |
| patient-ai (13) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| player-controller (1) | 6 | **6** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（两轮评审判据缺陷已修;88 过 + 3 NOT-RUN，`a78c27a`） |
| prescription-medication (11) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| processing (18) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| random-events (52) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| time-weather (5) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| tutorial-onboarding (48) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| world-ecozones (6) | 6 | **5** | 1 | 0 | ✅ **Complete ✅ 2026-10-03**（6/6 story;N1/N5 已修 · 本轮 109/109 绿;**未闭登记 = N3 白名单判据(待 27 侧落地)+ Story 005 [L] 走查 EXTERNAL**) |
| persistence-service (7a) | 2 | **2** | 0 | 0 | ✅ 全收口（002 = ADR-029 契约支） |
| save-slot-ui (7b) | 1 | 0 | 1 | 0 | ⬜ 未启动 |
| **合计** | **207** | **137** | **70** | **0** | **66.2% 完成(137/207)** |

> ⚠️ **2026-10-03 实测重算(当前口径)**:按各 story 真件逐件解析 ⇒ **207 / 136 Complete / 0 In Progress / 71 Ready**。
> 与上一版(124 / 6 / 77 / 59.9%)的差额来源:
> ① `modular-building` 6 → **7** 且 0 → **7 Complete**(story-007 = ADR-029 接线支;本轮并闭 C1/C2/N-r1/C8-ID → 转 Complete);
> ② `world-ecozones` 5 → **6**,4 → **5 Complete**(story-006 = ADR-029 接线支;转 Complete);
> ③ `player-controller` / **`emergency-procedures`** 由「Complete」/「In Review」重分类 —— 前者转 Complete,后者按 story 真件归 In Review(7 件,非旧记 6);
> ④ `persistence-service` 002 契约支 ⇒ 1 → **2 Complete**;`save-slot-ui` 归 Ready(非 In Progress);
> ⑤ **`camera-viewpoint` 6 Complete** —— 旧表已记 ✅,本轮无变;
> ⑥ 旧「In Progress 77」系把 `In Review` 与 `Ready` 混同计;实测 **In Progress = 0**(无 story 件标 `In Progress`)。
> 重算口径:逐 story 件解析 `> **Status**:` 首行 + 体 `**Status**: [x]`;`Complete*` / `In Review*` / `In Progress*` / 其余=Ready。

> 📌 **历史转录订正记录(保留闭环)**:原记「191 / 93 / 95 / 6 / 49%」及 2026-10-02 口径「203 → 206」
> 均已作废,勿再引用。成因同族:总数漏计 `persistence-service` / `save-slot-ui`,
> 且把 `In Review` 与 `In Progress` 混计。

### 测试状态

| 指标 | 值 |
|------|---|
| EditMode | **2204 total = 2163 Passed · 0 Failed · 40 Skipped · 1 Inconclusive**（2026-10-03 batchmode 实测，Unity 6000.3.24f1） |
| PlayMode | 25/25 Passed · 0 Failed（2026-10-01 实测） |
| 确定性验证 | F7 反汇编 CLEAN · AC-29 三平台逐位一致 |

> ⚠️ **上表 EditMode 一行 2026-10-03 更新为当前 HEAD 实测**(2204/2163/0/40/1)。
> 历史链条(保留闭环):原记「1831/1858」出自 `185063f`,该提交**编译未通过**
> (`Scripts have compiler errors`)⇒ 测试从未跑起来,数字**无源**;`5572d66` 修复编译后 = 1989/2022;
> 2026-10-02 = 1995/2028;2026-10-03(本轮,三 epic 收口后)= **2204/2163**。
> ⚠️ 旧行把 `total` 写成分子分母两个数(1995/2028),易误读为「1995 通过 / 2028 应为」——
> 现行口径按 `total = passed + failed + inconclusive + skipped` 记账。
> exit code 2 源自唯一 Inconclusive(`Audio/SettingsExposureTest.test_monoOption_existsWithValidDefault`,既有项),**非失败**。

### 关键里程碑

| 日期 | 事件 |
|------|------|
| 2026-09-20 | `/create-architecture` 完成，architecture.md v1.0 |
| 2026-09-20 | ADR-023/024/025 起草并 Accepted（Required #1/#2/#3） |
| 2026-09-21 | Gate Check 一轮 FAIL → 用户承接 → CONCERNS |
| 2026-09-22 | U0a 工具链闭合 · UX Review Phase 3A · 批裁轮 OQ 全结 |
| 2026-09-23 | ADR-026/027/028 Accepted（Required #4/#5 + 音频归属） |
| 2026-09-24 | AC-29 三平台确定性验证通过 · F7 反汇编 CLEAN |
| 2026-09-25 | 数值批三批全拍 · R13 回写闭环 · 44 二轮修订 |
| 2026-09-26 | 音频 Story 001-004 完成 · 技能/遥测/输入/UI 全推进 |
| 2026-09-27 | 音频 Story 005/006/014 完成 · 986/986 测试全绿 |
| 2026-09-28 | 音频 Story 007-013 完成 · 1175/1175 测试全绿 |
| 2026-09-29 | Gate Check 二轮 CONCERNS（无阻塞） |
| 2026-09-30 | active.md 刷新 |
| 2026-10-01 | Sprint 02 全部完成（16/16）· Sprint 03 部分完成（11/17） |
| 2026-10-01 | modular-building / world-ecozones 越序实现（架构依赖先行） |
| 2026-10-01 | 冲突解决提交 c291873 |

### Sprint 状态

| Sprint | 计划时间 | 实际状态 | 备注 |
|--------|---------|---------|------|
| Sprint 01 | 10-05 ~ 10-18 | ✅ 8/8 Complete | 提前完成 |
| Sprint 02 | 10-19 ~ 11-01 | ✅ 16/16 Complete | 提前完成 |
| Sprint 03 | 11-02 ~ 11-15 | ✅ 17/17 story Complete | 完成（2026-10-02）· ⚠️ **AC-S03-5 黄金夹具未兑现**（emergency NOT-RUN · combat 零存在;根因 = ADR-012 矩阵未激活） |

### 交付物
- **生产**:`InteractionSelector.cs` —— 第一键改测 `d∞(playerCell, cell)`(评审外发现:原测「到原点」,
  与 F-4.1 判定式不符,是 F-4.1b 三例失败真因)· `IsBetter` static + 三键字典序 +
  `KindPriorityOf` 查表 + `Chebyshev(a,b)` int64 两参
- **测试**:`target_selection_test.cs`(**13 条**)· `replay_selection_test.cs`(**4 条**)—— 全部断言**直接驱动生产**
- **评审原件**:`production/qa/evidence/review-interaction-story-002-2026-10-04.md`

### 实跑(2026-10-04 修复轮)
- `unity/Logs/interaction-s002-final.xml` = **41/41 green**(24 边界 + 13 + 4)

### 双代理评审 → 修复轮(已完成)
- **结构评审 REJECT**(3 BLOCKING)· **QA 评审 REJECT**(F-1…F-13)⇒ 逐条修复
- 修复:全部断言改绑生产 · int64 夹具命中真陷阱 · 哈希序负夹具驱动生产 · F-4.1b 三例逐字照抄 ·
  补两条 QA 案例(到达序打乱 + 缓存上一目标负夹具)· 闭集计数改 Enum.Length
- **变异测试证可红**:MUT1(删 int64 拓宽)/MUT2(d∞ 从原点)/MUT3(删键③)/MUT4(键③ 哈希序)/
  MUT5(删键②)—— 逐项令对应测试红,生产已还原

### 待办
- ✅ story-002 收口提交推送(12133f7 前的 13891b3/893f3a3)

---

## story-003(候选集四源构造)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`SourceDtos.cs`(九个具名输入形状)· `CandidateSources.cs`(四源只读接口 +
  装配包)· `CandidateSetLoader.cs`(四源合并 · **无玩家格形参** = 取路 (a) 结构保证 ·
  不裁剪 · 不持 sink)
- **测试**:`candidate_dto_reflection_test.cs`(**14 条** AC-4-03)·
  `candidate_sources_test.cs`(**11 条** AC-4-20/22 + 装载形状)
- **评审原件**:`production/qa/evidence/review-interaction-story-003-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s003-final.xml` = **56/56 green**(14 + 11 + story-001 24 + story-002 7)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **QA ACCEPT**;F-3(装箱判据循环论证)/ F-4(基类展开零覆盖)/ F-5(边缘登记)逐条修复
- 结构侧无独立代理原件(代理触顶未回)⇒ 主会话复核通过,缺口登记在案
- **变异证明可红**:MUT-base(删基类展开 ⇒ 恰单条红 55/56)

### 待办
- ✅ 收口提交推送(12133f7,已 push)
- ⬜ story 004(R_INTERACT 邻域裁剪)—— interaction-system 下一件

### 交付物
- **生产**:`DiscoveryRequest.cs`(F-4.3 三字段载荷)· `InteractionRadius.cs`(单源持有者,4-DC-1 下界)·
  `KindRouteTable.cs`(路由表双向闭合,RegisteredSystems 10 值)· `NeighbourhoodReporter.cs`(广播自报)·
  `IDiscoveryReporter.cs`(Request 签名改为载 `DiscoveryRequest`)· `InteractionSelector.cs`(半径单源化)
- **测试**:`discovery_report_test.cs`(F-4.3/4.3b/4.4 + AC-4-13)· `radius_single_source_test.cs`(AC-4-17 + 4-DC-1 下界)·
  `kind_route_closure_test.cs`(AC-4-18 + 4-DC-5)
- **评审原件**:`production/qa/evidence/review-interaction-story-004-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s004-final2.xml` = **86 / 83 passed / 0 failed / 3 skipped(NOT-RUN 机检)**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 APPROVED WITH SUGGESTIONS** · **QA 侧 ACCEPT-WITH-FIXES**;两件原件皆落盘
- 修复:扫描器分叉(删属性分支)· 6 半改机检 NOT-RUN · 帧率测试重写为「计数=调用次数」·
  新增「无主动交互 ⇒ 零出境」负夹具 · 半径扫描器已知限制登记 · RegisteredSystems 7→10(PF-1)
- **变异证明可红**:mut-cheb(丢 int64 拓宽 ⇒ 恰 1 红)· mut-gate(删主动交互门 ⇒ 恰 1 红)

### 待办
- ✅ 收口提交推送
- ⬜ 系统 6 Epic(接收侧:latch/幂等/判距复验/唯一 Append)—— 转绿 AC-4-13 6 半 + AC-4-17 6 消费半

<!-- STATUS -->
Epic: 交互系统
Feature: 数据契约烘焙接线
Task: story-007 收口(7/7 · 135/132/0/3)
<!-- /STATUS -->

---

## story-005(模态门与路由边沿)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`ModalGate.cs`(`IModalGateState` 单布尔消费契约 + `IArmedState` + `ModalGate.Accept/Select/SetMotorSuppression`)
- **测试**:`modal_gate_test.cs`(AC-4-09/10/19 + 规则九,含选择器自报计数接缝、单 bool 可执行影子、闭集基数守卫、零出现 `ModalId` 断言)
- **评审原件**:`production/qa/evidence/review-interaction-story-005-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s005-final.xml` = **101 / 98 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING;F-2…F-11)· **QA 侧 ACCEPT-WITH-FIXES**(F1…F6);两件判定皆回填原件
- 修复:F-1 丢弃接缝改选择器自报计数 · F-2 删 ordinal 死代码 · F-3 闭集基数守卫 · F-4 单 bool 可执行影子 ·
  F-6 cref 订正 · F-8 零出现 `ModalId` · F-9 正向对照反空转门
- **变异证明 6 项全可红**:MUT-A(删模态门 → 2 红)· MUT-B(排队替代丢弃 → 2 红)· MUT-C(清全位图 → 3 红)·
  MUT-D(闭集 +1 员 → 1 红)· MUT-E(生产引用 `ModalId` → **编译错**,方向性实证)· MUT-E2(代码级串 → 3 红)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 方向① 生产实现体(归 10)· NR-2 方向② 适配器(归 42)· NR-3 AC-4-10 3 侧扫描(归 story-006)·
  NR-4 动态第 8 屏夹具 · NR-5 运行期消费者接线

### 待办
- ✅ 收口提交推送
- ⬜ story-006(数据契约构建期校验与呈现/手柄验收面)—— interaction-system 下一件


---

## story-006(数据契约构建期校验与呈现/手柄验收面)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindTable.cs`(`KindContractRow` + `DurationOwnerKind` + `InteractionKindTableValidator` 六条校验
  + **`KindPriorityTable`** 第二键唯一真源)· `RoutedSystems.cs`(被路由系统集单一来源)
- **测试**:`data_contract_validation_test.cs`(**13 条**:6 条 DC 夹具 + 4-DC-4 W/H/D + 4-DC-6 归属方 + 4-DC-3 行⟷表
  + AC-4-21 机器代理 + 反空转门 + 独立性)
- **评审原件**:`production/qa/evidence/review-interaction-story-006-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s006-final.xml` = **116 / 113 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN 机检)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING 死代码;F-2…F-9)· **QA 侧 REJECT(AC-4-15)**(F-0 skip 归因更正;F-1…F-7)
- 修复:**真表单一来源接线**(F-2,消除「校验的表 ≠ 选择的表」)· 补 4-DC-4 W/H/D 半边(F-1/F-2)·
  `DurationOwner` 拆形态 + id 并断登记(F-3)· `RegisteredSystems` 单一来源(F-4/F-5)·
  **AC-4-21 机器代理**落地(F-3)· story 登记 NOT-RUN + 实际夹具数(F-6/F-7)
- **连带定向修复 story-002**:真表接线后 `target_selection_test` 4 条断言方向相反 ⇒ 按真表更新(MUT-7 先复现)
- **变异证明 10 项全落盘**:MUT-1…6(六条 DC 逐条可红)· **MUT-7**(接线真表 ⇒ story-002 恰 4 红,证接缝真实)·
  MUT-A(4-DC-4 W/H/D)· MUT-B(4-DC-6 归属方)· MUT-C(4-DC-3 行⟷表)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 装载器/烘焙接线(归 **story 007**)· NR-2 `KindPriorityTable` 真表(数值轮)·
  NR-3/4/5 三条 `[L]` 走查(可玩构建)· NR-6 4-DC-6 对 17/20 的真实时长登记(`OQ-17-3`)

### 待办
- ✅ 收口提交推送
- ✅ **interaction-system Epic 收口**(6/6 Complete · 2026-10-04;未闭项已显式登记)
- ✅ **story-007 装载接线收口**(NR-1 已闭 · 2026-10-04;见下)

---

## story-007(interaction_kinds 烘焙接线 · NR-1)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindBinder.cs`(阶段 2 绑定 + **唯一 `Validate(...)` 调用点** —— 消解 story-006 F-1 死代码)·
  `InteractionKindBaker.cs`(仓根种子 → 产物)· `InteractionKindCookedWriter.cs` + `InteractionKindCookedCodec.cs`(镜像编解码)·
  `InteractionKindBinderProbe.cs`(测试可见薄转发)· `DataBakeMenu.BakeInteractionKinds`(菜单调用点)
- **测试**:`interaction_kinds_bake_test.cs`(**19 条**)· `tests/unit/interaction/fixtures/`(15 夹具 / 11 负)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING = 首轮自造 4-DC-4 ③)· **QA 侧 REJECT**(无 BLOCKING 安全洞;F-0/F-3/F-6/F-7 MAJOR)
- 修复:**删自造判据 ③**(收回 4-DC-4 ①②,与 GDD 一字对齐)· **4-DC-4 ② 接生产接缝**(W/H/D 只注入 `SlotLinearKey` 行 + 行内自报)·
  **维度随产物落盘**(writer/reader 头部扩展,(D) 改读 `ds.*`)· **ConfigVersion 测试前置守卫** · **跨会话范围订正**
- **变异证明**:MUT-A(删 `Validate` 调用 ⇒ 7/7 负夹具红)· MUT-B′(4-DC-4 ② 承重)· MUT-F7(维度落盘可证伪,`unity/Logs/s007-mut-f7.xml`)
- **评审原件**:`production/qa/evidence/review-interaction-story-007-2026-10-04.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- 4-DC-4 ①(类型面恒真不可达)· 跨会话逐位一致(只证同进程)· 4-DC-6 归属方未登记半边(编译期常量无注入点)·
  `schema_version` 交叉一致性(守卫短路)· 聚合多错纪律 · 4-DC-6 对 17/20(`OQ-17-3`)· §三其余绑定层规则

### 待办
- ✅ 收口提交推送
- ⬜ interaction-system 7/7 全闭;下一系统见 Phase 2 关键路径
