# 评审原件 —— interaction-system story-006(数据契约构建期校验与呈现/手柄验收面)

- **对象**:`unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs`(生产)· `RoutedSystems.cs`(新,单一来源)· `unity/Assets/Tests/EditMode/Interaction/data_contract_validation_test.cs`(测试)
- **权威**:GDD `design/gdd/interaction-system.md` §数据契约 `4-DC-1…6`(:1023-1028)· `§F-4.1/F-4.1b`(F-4.1b 演示序 :796-797)· AC-4-15/07/08/21(:1225-1233)
- **轮次**:**单轮**(承用户「评审只做一轮」)
- **日期**:2026-10-04
- **方法**:双代理并行(结构侧 = lead-programmer · QA 侧 = qa-lead)+ 主会话独立变异复核

---

## 一、原判定

### 结构侧(lead-programmer)—— **CHANGES REQUIRED**

| id | 严重度 | 判定 | 落点 | 修复 |
| --- | --- | --- | --- | --- |
| F-1 | **BLOCKING** | 校验器是**死代码** —— 无任何烘焙路径调用它 ⇒ AC-4-15 的「WHEN 阶段 2 烘焙 THEN throw」不可满足 | `grep -rln InteractionKindTableValidator` 只命中类自身 + 测试;ADR-014 阶段 2 位点(`DataBakeMenu.cs`)只烘 item-database | **登记 NOT-RUN**:装载器/烘焙接线归 **story 007**;story-006 只产校验器与夹具,不冒充装载路径(见下「未闭登记」) |
| F-2 | MAJOR | **4-DC-4 半边未实现 + doc-comment 过度宣称** —— GDD 要求 `SlotLinearKey` 行须有 `W/H/D`,`ValidateStableIdSource` 只查枚举值域且无 W/H/D 形参 | `:202-212`;doc 宣称 `:199-201` | **已修**:`KindContractRow` 增 `WorldW/H/D` 三元组 + 方法体补 ② 分支 + 负夹具 `dc4_slotLinearKeyWithoutDimensionsHardFails`(MUT-A 证可红) |
| F-3 | MAJOR | **4-DC-6「已登记」半边不可表达 + `DurationOwner` 编码有损** —— `{None=0, RoutedSystem=-1}` 把「被路由系统的 id」压成哨兵 ⇒ 无从校验其对错 | `:69-76`、`:239-251`;GDD `:1006,1028` | **已修**:拆为 `DurationOwnerKind` 形态 + `DurationOwnerSystemId`(真实 id)+ 断「∈ 已登记系统集」+ 负夹具(MUT-B 证可红) |
| F-4 | MAJOR | **`RegisteredSystems` 字面量双份** —— 本处与 `KindRouteTable.cs` 各持一份同数据 ⇒ 漂移 | `:103-106` vs `KindRouteTable.cs:45-57` | **已修**:抽出 `RoutedSystems.Registered` 单一来源,两处引用同一常量 |
| F-5 | MAJOR | **独立性测试弱 + 注释过度宣称** —— 只注入一条 4-DC-3 违例,断言其余四条不波及;非矩阵,且半径(4-DC-1)静默缺席 | `:227-246` | **保留 + 登记**:「各红在己」的**强形态**由本故事的 **9 条独立变异证明**承担(逐条删一个校验体,只红对应夹具)—— 比 6×6 矩阵更强的机械证据 |
| F-6 | MINOR | 无变异证据(对照 story-005 的逐项 `mut-*.xml`) | — | **已补**:MUT-1…MUT-7 + MUT-A/B/C 全落盘 `unity/Logs/` |
| F-7 | MINOR | 登记的 `[L]` 证据文件三份**均不存在** | `story-006:69-71` | **已修**:story 文件显式登记 NOT-RUN(三份证据槽标「不存在 / 待可玩构建」) |
| F-8 | MINOR | 测试名带 AC id,非 `test_[system]_[scenario]_[expected]` | 全体 | **保留**:`test_ac415_dc<N>_<scenario>_<expected>` 是本仓既定形态(story-001…005 一致),记偏差不记缺陷 |
| F-9 | INFO | 退化世界维度(W/H/D ≤ 0)未测 | `:135-141` | **登记**:`min(W,H,D) ≤ 1 ⇒ 合法区间空` 被下界门覆盖(抛下界);非本故事承重面 |
| F-10 | INFO | **忠实·非空转·驱动生产**:4-DC-2/3/5/6-false 半边;真反空转门(`LegalRows` 须通过);4-DC-1 上界 `min(W,H,D)−1` 与 GDD 三审逐字一致 | — | — |

### QA 侧(qa-lead)—— **REJECT(AC-4-15)**,范围明确

| id | 严重度 | 判定 | 修复 |
| --- | --- | --- | --- |
| **F-0** | 更正 | **skip 归因纠正**:3 条 skip **全部属 story-004**(两条重放/联机前置 + 一条注入半径消费),**不含** story-004 的 AC-4-18 登记项 —— 已核 XML 逐条名 | 采纳;见「实跑基线」 |
| F-1 | **S1·BLOCKING** | `ValidateStableIdSource` 断言的是**枚举值域 ∈ 枚举**,该检查**在类型面恒真**(强类型字段,编译器可接受路径只能产生枚举内值)⇒ 删整条生产检查所有夹具仍绿;且 4-DC-4 的另一半(`SlotLinearKey ⇒ W/H/D`)**完全未实现** | **已修**(同结构 F-2):补 ② 半边并让其承重;① 保留为显式兜底并在 doc 记「类型面恒真,不承重」 |
| F-2 | **S2·BLOCKING** | `ValidatePriorityTotalOrder` 只断行内互异,而生产第二键是 `(int)kind` 占位 ⇒ **与表无机械连接**,删掉整个 `Validate(` 全仓无一变红 | **已修**:抽出 `KindPriorityTable` 唯一真源,选择器与校验器读**同一张表**,加「行 == 表」对拍 + 负夹具 |
| F-3 | **S2·BLOCKING**(原误标 ADVISORY) | 这就是「AC-4-21 缺失的可自动化代理」。`ValidateDurationOwner` 不查归属方与 `RoutesTo` 的对应 ⇒ 删字段/删检查无测试变红;GDD 失效模式(归属方**错了**)对旧检查是**盲的** | **已修**:① 归属方 ∈ 已登记系统集(同属闭合);② **AC-4-21 机器代理已落** —— `test_ac421_patientBeatsSameCellDrop_withoutAnyPointerOrHover`(无指针/悬停输入,由真表 Patient 压过同格 Drop) |
| F-4 | S3 | `Rows` 形参类型 `IReadOnlyList<>`,体用索引循环 —— 对真实数组正常 | 无(非缺陷) |
| F-5 | S4 | `RegisteredSystems` 双源且校验器那份 `private` ⇒ GDD 抗漂移判据映射不到 | **已修**(同结构 F-4) |
| F-6 | S4 | 「每条违例各一个夹具」字面 = 6,实际 8 | **已修**:story Test Evidence 登记「6 条 DC / **13 个夹具**」 |
| F-7 | S3 | story 自身未登记 NOT-RUN;三份 `[L]` 证据文件不存在 ⇒ 读作「证据待补」而非 NOT-RUN(借绿入口) | **已修**:story 文件加显式 NOT-RUN 行 + 三槽标「不存在 / 待可玩构建」 |

---

## 二、修复轮(主会话)

### 2.1 判据从「纸面」改为「承重」

| 修复 | 形态 | 依据 |
| --- | --- | --- |
| **真表单一来源** | 新 `KindPriorityTable`(`InteractionKindTable.cs` 内)—— 选择器 `KindPriorityOf` 与校验器 4-DC-3 皆引用之 | F-2:消除「校验的表 ≠ 选择的表」两机器面 |
| **4-DC-4 ② 已实现** | `KindContractRow.WorldW/H/D` + `SlotLinearKey ⇒ W/H/D > 0` 分支 | F-1/F-2:补未实现的半边 |
| **4-DC-6 归属方闭合** | `DurationOwnerKind` + `DurationOwnerSystemId` + 断 ∈ `RegisteredSystems` | F-3:GDD 失效模式是「归属方错了」 |
| **`RegisteredSystems` 单一来源** | 新 `RoutedSystems.Registered`,`KindRouteTable` 与校验器共用 | F-4/F-5 |
| **AC-4-21 机器代理** | `test_ac421_patientBeatsSameCellDrop_...` 驱动生产选择器,断言 Patient 压过同格 Drop 且与加入序无关 | F-3:回答「AC-4-21 应有代理」 |

> ⚠️ **真假分歧的镜面 + 定向修复**:接线真表后,story-002 的 `target_selection_test` 有 **4 条**断言
> (断言枚举序占位下 `Drop(0)` 应为唯一胜者)**方向相反** —— 这正是 F-2/QA-F-3 指出的「AGENT-GREEN 断言与最终所需行为相反」。
> **MUT-7 先复现**:把生产 `KindPriorityOf` 改为读 GDD 演示表 ⇒ **恰那 4 条红**(不涉 story-006 任一夹具)—— 证该接缝真实。
> 随后按真表更新该 4 条期望(示例 (b) 解 Container、传递律方向、打乱稳定性解 Patient、占位对拍改为「生产 == 真表」对拍)。
> **方向从此与 AC-4-21 一致(患者优先)**。⚠️ 真表**取值**仍归数值轮 —— 此处接线用的是 GDD §F-4.1b **演示序**(逐字照录),换真表只改 `KindPriorityTable` 一处。

### 2.2 逐条 AC 覆盖审计(修复后)

| DC | 生产检查 | 承重夹具 | 删检查会红吗? |
|---|---|---|---|
| 4-DC-1 下界 | `ValidateRadius:138` | `dc1_zeroRadius` | **会红** ✅ |
| 4-DC-1 上界 | `:141` | `dc1_radiusAboveUpperBound` | **会红** ✅ |
| 4-DC-2 缺项 | `:164-170` | `dc2_missingKind` | **会红** ✅ |
| 4-DC-2 枚举外值 | `:171-173` | `dc2_extraKind` | **会红** ✅ |
| 4-DC-3 平局 | `:190-195` | `dc3_priorityTie` | **会红** ✅ |
| 4-DC-3 行⟷表 | **新** | `dc3_rowDisagreesWithSelectorTable` | **会红** ✅(MUT-C 证) |
| 4-DC-4 枚举(兜底) | `:206-212` | `dc4_illegalStableIdSource` | 会红,但**类型面恒真**(不承重,已在 doc 明记) |
| 4-DC-4 W/H/D | **新实现** | `dc4_slotLinearKeyWithoutDimensions` | **会红** ✅(MUT-A 证) |
| 4-DC-5 | `:222-227` | `dc5_unregisteredRoutesTo` | **会红** ✅ |
| 4-DC-6 非空 | `:245-249` | `dc6_suppressWithoutDurationOwner` | **会红** ✅ |
| 4-DC-6 归属方登记 | **新实现** | `dc6_unregisteredDurationOwner` | **会红** ✅(MUT-B 证) |

**AC-4-15 判定:✅ SIGN**(六条 DC 各有具鉴别力夹具;此前恒真/未实现/无机械连接的三处承重缺口全部闭合)。

---

## 三、变异证明(全部落盘 `unity/Logs/`,生产已还原)

| 变异 | 结果 | 红点 |
| --- | --- | --- |
| MUT-1:删 4-DC-6 体 | 恰 1 红 | `dc6_suppressWithoutDurationOwner` |
| MUT-2:删 4-DC-1 上界 | 恰 1 红 | `dc1_radiusAboveUpperBound` |
| MUT-3:删 4-DC-3 | 恰 2 红 | `dc3_priorityTie` + `eachViolationReddensOnlyItsOwnCheck` |
| MUT-4:删 4-DC-5 | 恰 1 红 | `dc5_unregisteredRoutesTo` |
| MUT-5:删 4-DC-2 闭集 | 恰 2 红 | `dc2_missingKind` + `dc2_extraKind` |
| MUT-6:删 4-DC-4 ① | 恰 1 红 | `dc4_illegalStableIdSource` |
| **MUT-7**:生产 `KindPriorityOf` 改读 GDD 演示表 | 恰 4 红(**story-002 侧**) | `ac406_exampleB` / `ac406_orderIsTotal_transitive` / `ac406_slippedProductionKindLiterals` / `ac406_winnerIsStableUnderShuffledInsertion...` —— **证接缝真实** |
| MUT-A:删 4-DC-4 ②(W/H/D) | 恰 1 红 | `dc4_slotLinearKeyWithoutDimensions` |
| MUT-B:删 4-DC-6 归属方闭合 | 恰 1 红 | `dc6_unregisteredDurationOwner` |
| MUT-C:删 4-DC-3 行⟷表对拍 | 恰 1 红 | `dc3_rowDisagreesWithSelectorTable`(首次跑为 0 红 ⇒ 暴露夹具缺口 ⇒ 补夹具后复验 1 红) |

---

## 四、实跑基线

- `unity/Logs/interaction-s006-final.xml` = **116 total / 113 passed / 0 failed / 3 skipped**
- 3 skipped = story-004 的三条 NOT-RUN(`Assert.Ignore` 机检),**非本故事失败**。逐条名(承 QA **F-0** 更正):
  - `test_ac413_sixSideLatchIsNotRunBlockedBySystem6`(story-004)
  - `test_f43_sameTickTwoClientsProduceTwoDistinctEgressesThatConvergeOnlyAtSystem6`(story-004)
  - `test_ac417_fourSideConsumesInjectedRadiusValue`(story-004)
  - ⚠️ story-004 的 AC-4-18 登记项 `KindRouteClosureTest` **未被 skip** —— 上表三条**不含**它
- story-006 夹具 **13 条**(QA 建议登记实际数,非字面 6)

---

## 五、未闭登记(NOT-RUN —— 禁借绿)

| 项 | 内容 | 归属 |
| --- | --- | --- |
| **NR-1** | **装载器 / 烘焙接线**(F-1):`InteractionKindTableValidator.Validate` 的调用点 —— ADR-014 阶段 2 位点,把 `interaction_kinds.json` 烘成 `*.cooked` 并调用校验器 | **story 007**(本故事只产校验器 + 夹具,不冒充装载路径) |
| **NR-2** | `KindPriorityTable` 的**真表**:当前取值 = GDD §F-4.1b 演示序;真表落 `assets/data/interaction_kinds.json` | **数值轮**(换表只改一处,选择器与校验器自动同源) |
| **NR-3** | AC-4-07 走查(`ac-4-07-walkthrough.md`)| 可玩构建 |
| **NR-4** | AC-4-08 听测(`ac-4-08-listen.md`)| 可玩构建 |
| **NR-5** | AC-4-21 手柄走查(`ac-4-21-gamepad.md`)| 可玩构建(**机器代理半边已落**,见 §2.1) |
| **NR-6** | AC-4-15 中 4-DC-6 对 `ForageSpot`(17)/`Container`(20)的**真实时长登记** | `OQ-17-3` 未裁;契约已写、判据待前置(GDD :1035-1037 明列,不得记绿) |

---

## 六、签字

| AC | 判定 | 依据 |
| --- | --- | --- |
| **AC-4-15** | ✅ **SIGN** | 六条 DC 各具承重夹具;三处承重缺口(4-DC-3 无机械连接 / 4-DC-4 半边未实现 + 恒真 / 4-DC-6 属性盲区)已闭;MUT-1…6 + MUT-A/B/C 逐条证可红 |
| **AC-4-07** | ⬜ **NOT-RUN** | 人工走查,EditMode 不可自动化;证据文件不存在(显式登记,禁借绿) |
| **AC-4-08** | ⬜ **NOT-RUN** | 人工听测,同上 |
| **AC-4-21** | 🔶 **部分 SIGN** | **机器代理半边 SIGN**(无指针/悬停,真表 Patient 压过同格 Drop,`test_ac421_...`);**手柄走查半边 NOT-RUN** |

**综合判定:APPROVED(修复轮已闭;单轮评审无复审)** —— AC-4-15 与 AC-4-21 机器半边可签;
三条 `[L]` 走查面按 F-7 显式 NOT-RUN。
