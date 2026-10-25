# Story 001: 炮制配方子集筛选与准入谓词 Admissible

> **Epic**: 炮制
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/processing.md`(规则二 · 规则三 · F-18.2 · 规则十 配方可知性 · AC-18-05/10/18/20)
**Requirement**: TR-processing-004(炮制子集判据 = Recipe.owner 显式字段,21a D-21-30)· TR-processing-005(准入两道门:skill_gate 门槛 + min_quality 准入闸,⚠️ partial —— 无 ADR 承接)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011(主,Amendment B): 判定归主机终裁,客户端预答降级为呈现门 · ADR-014(次): `Recipe.owner` 字段走 21a schema/烘焙管线 · ADR-005(次): 谓词输入全可重建
**ADR Decision Summary**: 准入谓词 `Admissible(recipe, actor, consumedInstances) ⟺ QueryLevel(actor, CRAFT_SKILL) ≥ skill_gate ∧ min(quality of consumedInstances) ≥ min_quality`,是 `(recipe, actor, consumedInstances)` 的**纯函数**;客户端预答与主机终裁走**同一函数零第二实现**(AC-18-20,ADR-011 Amendment B 同构:判定输入进流、终裁归主机);`skill_gate` 是门槛**非系数**(否则与 SkillMod 双重计数),`min_quality` 是准入闸**不改幅值**;子集筛选读 `owner == process`(「processing_state 是否变化」降为单向校验)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 谓词与筛选为门 A 程序集纯 C# 整数比较(等级 int × 品级 int),零引擎 API、零 post-cutoff 面。

**Control Manifest Rules (this layer)**:
- Required: `Admissible` 全仓唯一定义点(客户端预答与主机终裁两处调用同一函数);`min_quality` 使用点仅限该谓词;谓词输入 ∈ {事件流/烘焙数据/二者的纯函数}(三源不变量,ADR-016 §一)
- Forbidden: 在 18 侧把 `skill_gate` 当修正系数参与乘加;`min_quality` 进入幅值调制路径;自建「state 变化」判据当主筛(已降级为单向校验);第二份准入实现(哪怕为性能缓存,缓存键不纯即违三源)
- Guardrail: ~~`Recipe.owner` 字段实现归 21a(O-18-R1 回写义务,AC-18-18 在此之前 EXTERNAL 不得记绿)~~ **⚠️ 2026-10-20 订正(S-2):该阻塞的措辞已失真,见下方「S-2 订正」块**;配方可知性接缝 O-18-R6 未立 ⇒ 本故事只出只读谓词,不代 42 裁面板形态

> ### 📌 S-2 订正(2026-10-20)—— 空集借绿风险
>
> **原措辞**:「`Recipe.owner` 字段**有 schema、无实现**」⇒ AC-18-18 挂 EXTERNAL。
> **实测(可复算)该措辞已失真 —— 字段实现其实在盘上**:
>
> | 环节 | 实测 |
> |---|---|
> | 枚举 | `unity/Assets/Sim.Contracts/ItemDatabase/RecipeOwner.cs:9` `public enum RecipeOwner`(Process / Craft / Build) |
> | 属性 | `unity/Assets/Sim.Contracts/ItemDatabase/Recipe.cs:41` `public RecipeOwner Owner { get; set; }` |
> | 烘焙读取 | `unity/Assets/Gameplay.Presentation/CookedCodec.cs:181` `Owner = (RecipeOwner)r.ReadInt32()` |
> | 校验门 | `Editor.Tools.Gates/ItemDbValidation.TryParseRecipeOwner` · `RecipeValidationGates.cs`(按 `Owner` 分区) |
> | 引入提交 | `1c2bfba`(item-database Story 002,schema 类型与复合主键) |
>
> ⇒ **「字段无实现」不成立**。真正的缺口在**数据侧**,共两层:
> 1. **`Sim/` 侧零消费方** —— `grep -rn '\.Owner\b' unity/Assets/Sim/ unity/Assets/Sim.Contracts/`
>    命中 **0** 处。但这是**预期**的:本 story 尚未开工(`unity/Assets/Sim/Processing/` 目录
>    **不存在**),消费方正是本 story 要写的东西 ⇒ 这一层**不是阻塞**,是本 story 的交付内容。
> 2. **数据表内容不足以支撑判据** —— `assets/data/item_database_recipes.json` 实测:
>    **配方总数 = 2**(`dry_willow_bark` / `extract_salicylic`),**`owner` 分布 = `{process: 2}`**
>    ⇒ `craft` 与 `build` 两个子集**均为空集**。
>
> ### 🔴 由此产生的判据缺陷(本条订正的主因)
>
> AC-18-18 的判据是「三子集**并 = 全表**、**两两交 = ∅**」。在 `craft` / `build` 为空集时:
> - 并 = `{process} ∪ ∅ ∪ ∅` = 全表 ✅ **恒成立**
> - 两两交 = `∅` ✅ **恒成立**
>
> ⇒ **该判据在空子集下平凡为真,恒绿而什么都不测**。这正是本项目反复出现的
> 「**自造断言顶替 AC 语义层**」形态(见 `production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`
> 与跨 epic 假绿普查)。**若照原样实现并跑绿,得到的是零信息。**
>
> ### 订正后的判据(本 story 须按此实现)
>
> AC-18-18 增加 **非空前置断言**,三段缺一不可:
> 1. **非空**:`|process| ≥ 1 ∧ |craft| ≥ 1 ∧ |build| ≥ 1` —— 任一子集为空 ⇒ **测试红**,
>    报文点名「`<owner>` 子集为空,并/交判据无判别力:数据表须至少各含 1 条」;
> 2. **完备**:三子集之并 = 全表(按 recipe id 集合逐项比对,**禁只比基数**);
> 3. **互斥**:两两之交 = ∅(同上,逐项比对)。
>
> **测试数据的两种合法来源**(须在测试内显式声明选了哪一种):
> - **(a) 用真表 + 门** —— 若真表补齐了 `craft` / `build` 配方,直接读真表跑三段。
>    ⚠️ 当前真表 **2 条全 `process`** ⇒ 选 (a) **现在必然红**。
> - **(b) 用合成夹具 + 显式标注** —— 构造每个子集 ≥ 1 条的合成配方集跑三段,
>    并在测试名/注释里注明「**合成数据,不证明真表已覆盖**」。
>    这是唯一当下可行的路径,**但不得写成「AC-18-18 已通过」** ——
>    应记为「**判据机制已证 + 真表覆盖度未证**」两段。
>
> **⇒ 阻塞措辞订正**:从「`BLOCKED-BY-21a`(字段无实现)」改为
> **「数据表缺 `craft` / `build` 配方 = 真表覆盖度未证」**(归数值轮 / 21a 内容批),
> 而**判据机制本身可以现在就实现并验证**。

> ### 📌 S-2 补口执行记录(2026-10-20 · 用户指令「按你的建议进行」)
>
> 用户裁定先行**补真表**以使 ② 真表覆盖度**可判**(而非挂 NOT-RUN 等数值轮)。
> 补的是**占位数据**(数值为夹具值,非平衡值 —— 承该表 `_note` 既有口径),
> **不是内容轮**;真配方内容仍归数值轮 / 21a 内容批。
>
> | recipe_id | owner | 输入 | 输出 | 守恒(AC-21a-65 极值式) |
> |---|---|---|---|---|
> | `craft_wooden_splint` | `craft` | 干柳树皮×2 | 木夹板×1 | 1 ≤ 2 ✅ |
> | `assemble_wooden_post` | `build` | 干柳树皮×3 | 木柱×1 | 2 ≤ 3 ✅ |
>
> 配套新增两物品行:`wooden_splint`(`tool`)· `wooden_post`(`build_part`)。
> 真表现状:**6 物品 / 4 配方,`owner` 分布 = `{process:2, craft:1, build:1}`**,
> 零悬空外键,四条配方守恒全过。`*.cooked` 已随之重烘(ADR-014 两阶段,产物入 git)。
>
> ⚠️ **补口过程中的一次改道(实测踩到,已避开)**:首版 craft 产出写的是
> `herbal_salve`(`category: drug`),**会踩红 11 的 `AC-11-20`** ——
> `PrescriptionActionsBinder.cs:238` 对 `category == "drug"` 的条目**无条件入表**
> (哪怕 `dose_range` 为 null),`:264-267` 则要求**每一味入表的药**在本草词表
> `materia_lexicon.json` 里有功效词 ⇒ 新增一个 `drug` = **被迫替 11 写一条古籍功效词**。
> **S-2 的补口不该被逼着写别的 epic 的内容**,故改为 `category: tool`(木夹板,本作语义亦更贴)。
> 实测 `tool` 类零专属门;品类的附属约束是**单向**的(`material`↔`gather_profile` /
> `drug`↔`drug_profile` / `weapon`↔`inflicts_injury`),`tool` 三项全 null 合法。
> 详见 `tests/integration/processing/README.md`。


---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [ ] **AC-18-05**: `GIVEN` `min_quality` 的全部使用点,`WHEN` 符号引用扫描,`THEN` 仅出现在 `Admissible` 谓词(F-18.2),零幅值调制路径(规则三 · 21a `:167`)
- [ ] **AC-18-10**: `GIVEN` 任意配方与玩家状态,`WHEN` 求值谓词,`THEN` 配方进入候选集 ⟺ `Admissible ∧ owner=process ∧ 单炉空闲`(iff 双向,18 侧半边;呈现半边见 AC-18-10b)
- [ ] **AC-18-20**: `GIVEN` 客户端预答与主机终裁两条路径,`WHEN` 符号引用扫描 `Admissible` 实现,`THEN` 同一函数、零第二实现;谓词为纯函数,`actor_id` 由 20 `InventoryOf(player)` 归因
- [ ] **AC-18-18**(⚠️ **2026-10-20 订正 —— 原判据在空子集下恒真,已加非空前置**):
  - **(1) 非空前置**:`|owners==process| ≥ 1 ∧ |owners==craft| ≥ 1 ∧ |owners==build| ≥ 1`;
    任一为空 ⇒ **红**,报文点名该子集
  - **(2) 完备**:三子集之**并** = 全表(按 recipe id 集合**逐项**比对,禁只比基数)
  - **(3) 互斥**:三子集**两两之交** = ∅(同上逐项)
  - ⚠️ **原记「EXTERNAL · BLOCKED-BY-21a(字段有 schema、无实现)」经实测失真** ——
    `RecipeOwner` 枚举 / `Recipe.Owner` 属性 / `CookedCodec` 烘焙读取**均已在盘上**(见上方 S-2 订正块)。
    真缺口 = **数据表 2 条配方全为 `process`,`craft`/`build` 为空集**。
  - **分段记分(禁合并成一条绿)**:
    ① **判据机制** —— 合成夹具(每子集 ≥1 条,标注「合成」)+ **负向夹具**(只喂 `process` ⇒ 必红)。
       **实测 6/6 绿**(`RecipeOwnerPartitionTest`,真身 `unity/Assets/Tests/EditMode/Processing/`)。
    ② **真表覆盖度** —— **2026-10-20 补口后已可判**(真表已含 `craft`/`build` 各 1 条),
       由 `test_realSeed_allThreeOwnerSubsetsNonEmpty` **独立断言**;
       **经证伪轮证明有判别力**:删 craft 配方 / 删 build 配方 / 把 craft 改 `process`
       ⇒ 该测**三次全转红**(反证记录见 `tests/integration/processing/README.md`)。
       ⚠️ **两段各自记分,禁合并成一条绿**;
       ⚠️ **② 证的是「数据落在三个桶里」,不是「配方内容正确」** ——
       真配方内容仍归数值轮 / 21a 内容批,本 story 不追认内容侧。


---

## Implementation Notes

*Derived from ADR-011 Amendment B(主)/ ADR-014:*

1. 谓词签名 `bool Admissible(RecipeId, ActorId, IReadOnlyList<ItemInstanceRef> consumedInstances)`,住 18 的 sim 程序集(门 A);两调用点(预答/终裁)经由同一静态具名函数,禁委托复制。
2. 输入取数:等级 = 30 `QueryLevel`(int,承 AC-18-03 传等级纪律的镜像侧);`min(quality)` = 对被点名实例集求 min(整数比较;实例集来自 20 `InventoryOf(player)` 投影,18 不持库存副本);`skill_gate` / `min_quality` = 烘焙配方字段(int)。
3. 子集筛选 = 遍历烘焙配方表取 `owner == process`(21a 字段);同时把「processing_state 变化」做**单向校验断言**(若出现 owner=process 而 state 不变的行 ⇒ 装载告警级不一致检查,构建期归 21a 侧 AC-21a-66,本故事只做运行期防御断言半边)。
4. 候选集 iff 双向(AC-18-10):「单炉空闲」半边在 Story 004 落地,本故事先立谓词形状与桩接口(空闲位 = Story 004 的炉状态机,此处以 `ISingleFurnaceQuery` 接缝引用)。
5. 配方可知性(规则十):面板禁灰置 = 42 呈现半边(AC-18-10b EXTERNAL);18 只供只读谓词,供 42 判「候选集成员资格」;O-18-R6 接缝未立 ⇒ 回写义务继续挂账,不在本故事擅裁。
6. `min_quality` 不改药效:扫描器断其使用点 ∈ {Admissible 体内},出现于 F1 参数构造路径即红(与 Story 002 的 AC-18-02 扫描件共用基建,零第二扫描器)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:F1 调用契约、四修正项供料、AC-18-02 结算路径扫描(本故事只保证 min_quality 不进幅值)
- Story 003:准入通过后 `Craft` 落流(本故事只做判定)
- Story 004:单炉空闲位与并发拒绝(本故事以接缝引用)
- item-database epic:`Recipe.owner` schema 落地与烘焙主执行 ~~(AC-18-18 的 BLOCKED-BY 对方)~~
  —— **2026-10-20 订正:该前置已完成**(枚举 / 属性 / 烘焙读取均在盘,见 S-2 订正块);
  真表 `craft`/`build` 空集缺口**已经 S-2 补口填平(占位数据)**,故 AC-18-18 的 ② 真表覆盖度
  **当下可判、已判、并已证伪判别力**;**但真配方内容仍归数值轮 / 21a 内容批**,
  本 story 的绿**只覆盖「数据落在三个桶里」**,不覆盖内容侧正确性
- 42:炮制面板/候选呈现(AC-18-10b、O-18-R6 的呈现半边)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

> **QA plan 对齐(2026-10-25 /qa-plan sprint-20)** —— 证据件:`unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs`(AC-18-18 一角已落:`recipe_owner_partition_test.cs`,两段记分禁合并,见上方 S-2 订正块);估 ~10 条。AC-18-05 扫描器**负样例文件自证有效**;AC-18-10 = 8 组合 iff 矩阵(gate/quality 恰等均 ≥ 过;空实例集 ⇒ 短路 false + 具名理由);AC-18-20 = 同一 `MethodInfo` + spy 共享 + 纯函数零副作用;「单炉空闲」半边 = `ISingleFurnaceQuery` 接缝桩(Story 004 前)。**S12-4 证伪轮强制**:本件命中 阈值边界 / 定义域收窄 / 一次性幂等 三形态,每条主要断言配「停用守卫 ⇒ 红」反证。若撞 TD-medcons-11(CLI 烘焙入口缺口)⇒ 挂标不记绿。QA plan 全文:`production/qa/qa-plan-sprint-20-2026-10-25.md`。

- **AC-18-05**: min_quality 使用点扫描。
  - Given: 18 程序集源码树;合成注入一个「用 min_quality 调 output」的负样例文件(测试专用,不进出货)。
  - When: Roslyn 符号引用扫描。
  - Then: 真实代码路径使用点 ⊆ {Admissible};负样例触发即红(扫描器自证有效)。
  - Edge cases: 经别名/属性包装的读取同计(承 processing 扫描标识符口径白名单)。
- **AC-18-10**: 候选集 iff。
  - Given: 配方矩阵夹具(gate 上/下 × quality 上/下 × 空闲/占用 8 组合)。
  - When: 求候选集。
  - Then: 成员资格与三条件合取逐位等价(双向蕴含都测)。
  - Edge cases: gate 恰等(≥ 过)、min_quality 恰等(≥ 过)、空实例集(min over ∅ ⇒ 谓词短路 false 并具名理由)。
- **AC-18-20**: 同一函数零第二实现。
  - Given: 预答路径与终裁路径各一反射句柄。
  - When: 比对 MethodInfo 同一性 + spy 计数(两路径共享)。
  - Then: 同一 `MethodInfo`(非「行为相同的两份」);纯函数性:同输入两次求值逐位同、零副作用(spy-sink 零事件)。
  - Edge cases: 客户端副本流滞后(预答 yes → 终裁 no)是合法时序,谓词自身不感知网络(不在此断)。
- **AC-18-18**(**2026-10-20 订正:由「EXTERNAL 全挂」→「机制可测 / 覆盖度挂起」→
  补口后「**两段皆可判**」**)**:三子集并/交 + **非空前置**。
  - Given: ~~21a owner 字段实现落地后~~ **字段已在盘(2026-10-20 实测)**;
    两条数据源:**合成配方集**(每 `owner` 子集 ≥ 1 条,标注「合成」)+ **真表**
    (2026-10-20 补口后已含三子集各 ≥ 1 条)。
  - When: ① 对合成集求三子集 → 验非空 / 并 / 交**三段**;
    ② 对真表求三子集 → 跑**同一套三段判据**(单一定义处,合成与真表不分叉)。
  - Then: ① 三段全过 ⇒ 记「**判据机制已证**」;
    ② 真表三段全过 ⇒ 记「**真表覆盖度已证**」;**两段各自记分,禁合并成一条绿**。
  - Edge cases: 空子集必须被 **① 段**判出(负向夹具:只喂 `process` ⇒ ② ③ 两段**仍恒真**,
    唯有 ① 能红 —— 该反例已写进测试体自证)。
  - ⚠️ **禁**用 `Ignore("EXTERNAL BLOCKED-BY O-18-R1")` —— 该阻塞已不存在;
    ② 也不得再写「按设计挂起」—— 补口后**已无挂起项**。
  - ⚠️ **② 的绿边界**:只覆盖「数据落在三个桶里」+「划分器未被改成按 `processing_state` 猜归属」;
    **不覆盖配方内容正确性**(数值/平衡归数值轮)。


---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs` — must exist and pass
  （⚠️ 本 story 尚未开工；已落地的是 **AC-18-18 一项**，真身
  `unity/Assets/Tests/EditMode/Processing/recipe_owner_partition_test.cs`，账本见
  `tests/integration/processing/README.md`。其余 AC 的测试件仍归本 story 实现轮。）
- AC-18-18:**机制三段**(非空 / 并 / 交)打合成集 + **真表覆盖度**打真表 ——
  **两段皆已可判**（2026-10-20 补口后）；**各自记分,禁合并成一条绿**。
  判别力已由**证伪轮**证明：删 craft / 删 build / craft 改 process ⇒ 真表测**三次全转红**

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: item-database story-002/003(schema 类型 + F1 求解器,Complete —— 本故事消费其配方表形状);skill-system story-001(Complete,QueryLevel 件);inventory-items Story 001(`InventoryOf` 投影,取实例集)
- Unlocks: Story 002(准入通过后的供料与调用),Story 003(点火前置校验),Story 004(候选集第三条件接线)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
