# ADR-017: 是否引入 DOTS(sim 侧结构性排除 · 表现层留复评门)

## Status

Accepted

> **2026-09-15 用户裁定转 Accepted。** 一项核心裁定已锁:**分层裁决** ——
> ① **sim 侧永不上 DOTS**(**结构性排除**,非规模判断:门 A 的 `"noEngineReferences": true`
> 与 `Unity.Entities` 是引擎程序集这一事实**天然冲突** —— 不是「规模够不够」的问题,
> 而是「上 DOTS 就必须拆门 A」的问题);② **表现层留复评门**,触发条件挂在 **OQ-8**
> (病人数范围 / 在场 vs 全域)标定之后,阈值数字由用户填。
> **✅ 2026-09-20 两项前置均已裁定(OQ-8 / OQ-25-8 结案)**:模拟范围 = **在场才模拟** · `PATIENT_APPEARANCE_CAP = 24` · `TICK_SECONDS = 0.05`(20 Hz);
> **复评门阈值 = 同场表现实体数 ≥ 100 或 表现层帧时间 ≥ 8 ms(≈ 16.6 ms 帧预算的 48%)**。⇒ **sim 侧 24 < 100 由构造决定,门在 sim 永不触发**;触发面只剩表现层群集 / VFX 实体计数。
> **本 ADR 兑现 ADR-004**(`technical-preferences.md` 记为「待建」的那条),
> 并正式记载一处此前**全文无记录的接缝**(门 A ↔ `Unity.Entities`)。
> 引擎侧依据 `docs/engine-reference/unity/plugins/dots-entities.md`(仓库自有权威件)。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 一项核心裁定,分层照准**)· technical-director(起草与裁决)
· unity-specialist(引擎侧边界:门 A / 门 B 与 DOTS 程序集关系)· unity-dots-specialist(复评门的技术口径)
· 系统 9 疾病与伤情 / 13 病人 AI / 27 敌人 AI / 34 公卫与疫情模拟 / 36 NPC 与村落 GDD 作者

## Summary

`technical-preferences.md` 的 **ADR-004「是否需要 DOTS」** 自项目起就是**唯一长期挂账的待建项**:
大型 NPC / 瘟疫模拟的规模未定,故「先不用,等规模数据出来再决策」。ADR-005 已就系统 9 裁决
「不上 DOTS,只做零成本预防」,ADR-016 §九 又声明决策器保持「可平移」形状 ——
**但全案从未有一份权威件回答「要不要 DOTS」,且规模数据至今不存在**。
本 ADR 裁决:**分层**。
**sim 侧永不上 DOTS** —— 这不是规模判断而是**边界判断**:门 A(ADR-005)要求 sim 程序集
`"noEngineReferences": true`,而 `Unity.Entities` / `Unity.Burst` / `Unity.Jobs` / `Unity.Mathematics`
**全是引擎程序集**;上 DOTS 即须拆门 A,拆门 A 即放弃「sim 零引擎依赖」的结构性保证。
**表现层留一个带触发条件的复评门**,触发条件 = OQ-8(病人数范围)标定后同场实体数进入
`plugins/dots-entities.md` 所载的 DOTS 甜点区(~~阈值待用户裁定~~ → **✅ 2026-09-20 用户裁定:≥ 100 同场表现实体 或 ≥ 8 ms 表现层帧时间**)。
在触发前,表现层**同样不引入 DOTS**。并**撤回 ADR-016 §九「以便平移 DOTS」的理据**
(保留其「批处理友好」形状,理由改为缓存局部性与可测性)。ADR-004 就此结案。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(技术选型 / 程序集边界 / 性能策略) |
| **Knowledge Risk** | **LOW —— 本裁决不依赖任何 post-cutoff API。** 判据是**边界**(门 A)与**规模**(OQ-8,✅ 2026-09-20 已标定 = 在场才模拟 / CAP 24)两件结构性事实,与 Unity 6.3 的 DOTS 具体行为无关。**复评门一旦触发**,才需实测 post-cutoff DOTS(Entities 1.3+ / 6.3 的「production-ready DOTS」),届时按本 ADR 的复评协议另开 ADR |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`(6.0 / 6.3 的 DOTS 行)· **`docs/engine-reference/unity/plugins/dots-entities.md`**(421 行;Use/DON'T-use 指南)· `docs/engine-reference/unity/PLUGINS.md`(决策指南)· `docs/engine-reference/unity/breaking-changes.md`(Entities 1.0+ 完全重写)· `docs/engine-reference/unity/current-best-practices.md` · `docs/architecture/adr-005-deterministic-sim.md`(门 A / 门 B)· `docs/architecture/adr-016-ai-architecture.md` §九 · `design/gdd/disease-simulation.md`(OQ-8 / 冻结方向③)· `docs/architecture/architecture-review-2026-09-15.md`(R-11 · ADR-004) |
| **Post-Cutoff APIs Used** | **None** —— 本裁决是「不引入」;判据全为仓库自有的程序集边界与规模登记 |
| **Verification Required** | ① **门 A ↔ `Unity.Entities` 冲突的正式记载**(本 ADR §二 已记,实现期须有一条 EditMode 断言把「sim 程序集程序集引用集恰好 = {BCL}」固定下来,把冲突从「约定」升为「构建失败」);② ~~OQ-8 标定~~ ✅ **已完成(2026-09-20:在场才模拟 · CAP = 24 · 20 Hz)** —— 复评门与 ADR-005 性能表的共同前置就此结清;③ 复评门触发后的 post-cutoff DOTS 实测(仅在触发时) |

> **Note**: Knowledge Risk **LOW** —— 本 ADR 的成功标准是「不做错事」,而非「用对 API」。
> 升级引擎版本时**无需**重读本 ADR 的裁决部分;但若复评门触发,须按 §五 另开 ADR。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— **门 A / 门 B** 是本次结构性排除的全部依据:`"noEngineReferences": true` + EditMode 反射断言「sim 不引用 `UnityEngine.*`」)· **ADR-012**(Accepted —— IL2CPP 有符号溢出 UB spike F7;若 sim 上 Burst,定点中间乘的溢出语义将再叠一层,本 ADR §Alternatives 5 已记)· **ADR-016**(Accepted —— §九 的「可平移」理据由本 ADR §四 收窄)|
| **Enables** | **ADR-004 结案**(`technical-preferences.md` 的待建项就此兑现)· **9 / 13 / 27 / 34 / 36 的性能预算可以「纯 C# 值类型」为唯一假设**(不再需要对冲 DOTS 迁移)· **ADR-005 性能表的量纲确定**(实体数 × tick 频率 × 单次求值 —— ~~系数待 OQ-8~~ ✅ 前两因子 2026-09-20 已定:24 × 20 Hz;残留 = 单次求值成本,归实测非裁决)|
| **Blocks** | **不阻塞任何实现** —— 本裁决是「维持现状 + 关门」。34 公卫 / 36 NPC 的实现**从纯 C# 出发**,无需等待 DOTS 结论 |
| **Ordering Note** | 本 ADR 是 **R-11 的落点**,**兑现 ADR-004**。**OQ-8 是它的复评前置**,但**不是它的实现前置** —— 换言之:即便 OQ-8 今天标定出「病人数 = 5000」,sim 侧**仍然**不上 DOTS(门 A)。OQ-8 只可能推动**表现层**复评。**✅ 2026-09-20 实测口径:OQ-8 标定为 CAP 24 < 阈值 100 ⇒ 该门在 sim 侧由构造决定永不触发**,与「5000」这一假设情形相反 |

## Context

### Problem Statement

「要不要用 DOTS」是本项目**挂账最久的未决项**。它的代价不是「少用一个工具」,
而是**每个涉及的 GDD 各自对冲**:

- ADR-016 §九 要求决策器保持「**可平移**」形状(值 struct / 无引用类型状态 / 无虚调用热点),
  理由是「R-11 若判上 DOTS,决策器可平移而不重写」——**这条约束的存在理由完全依赖于一个未裁决的问题**;
- `disease-simulation.md:510-511` 明确要求 `PatientState` 跨系统**传值不传引用**,
  理由也是「否则 DOTS 迁移要重写每一个调用点」;
- `technical-preferences.md:15` 的物理配置写着「**若**大型 NPC / 瘟疫模拟走 DOTS,
  则该子系统用 Unity Physics」——一个**悬空的 if**;
- 而 ADR-005 已把「零成本预防」写成纪律,ADR-014 / 015 / 016 又连续三次建立
  「**运行期零第三方**」的取向。

**未裁决的代价**:凡是「为了将来可能的 DOTS」而做的形状约束,都无法判断**何时可以放手**;
而 DOTS 的真实采用门槛(`plugins/dots-entities.md`「1000s–10,000s entities」)与
本项目「P0 只留一个敌人原型 / 4 生态区 / P0 建造只留摆放」的**克制规模之间,
存在数量级落差**(见 §Current State)。

### Current State

**规模数据不存在 —— 这是全案最硬的事实。** 逐项:

| 量 | 现状 | 出处 |
|----|------|------|
| 病人数 | ~~**从未定义**~~ → **✅ 2026-09-20 定义(OQ-8 结案)**:「**在场才模拟**」· 同场上限 **24**;GDD 自陈的「生态区级普查 vs 散落病例差 2–3 个数量级」张力由「在场」这一侧的裁定终结;并**正式撤销**了 ADR-005 性能表里的「数十病人」自引(现 = 上界 24) | `disease-simulation.md:560-564` · `:1329-1335`(OQ-8 全文) |
| 敌人 | P0 只留**一个原型(兵痞)**;架构「**不为『多原型』预置复杂度**」 | `adr-016:271,276-277` · `systems-index.md:534` |
| 世界 | **4 生态区**(手工烘焙);P0 建造「**只留摆放**」 | `game-concept.md:573` · `systems-index.md:534` |
| 掉落 / 物品 | `stack_max` 有;实例总量**无** | `item-database.md:623` |

**引擎参考的判据(仓库自有权威件)**:

- `plugins/dots-entities.md:11-23` 开篇即写:designed for games with **massive scale(1000s–10,000s entities)**;
- 明确 **DON'T use DOTS for**:**Small games(overhead not worth it)** · **Gameplay requiring frequent
  structural changes** · **Heavy use of UnityEngine APIs**;
- `PLUGINS.md:43-50` 决策指南:I need **1000s of entities**(RTS, sim)→ DOTS/Entities;
- `breaking-changes.md:10-34`:**Entities 1.0+ 相对 0.x 是完全重写**,多数教程过时;
- `VERSION.md`:6.0 的 Key Theme 含「Entities 1.3, DOTS improvements」,6.3 含「production-ready DOTS」。

**既有三处「不上 DOTS」—— 全部以「规模未定」为据,无一处是方法论否决,也无一处有实测**:

1. `adr-005:75-76` —— 用户延后 ADR-004,取「不上 DOTS,只做零成本预防」;
2. `disease-simulation.md:15` —— 9 的冻结方向③;
3. `adr-016:279` —— sim 侧纯整数 C# 已足。

### Constraints

- 不得改动 ADR-005 的门 A / 门 B(它们是 sim 确定性的**结构性**保证,不是可选项)。
- 不得引入运行期第三方(ADR-014 零 JSON 解析器 · ADR-015 零第三方几何工具 · ADR-016 零第三方行为树 / 寻路库)
  —— 这已是**四连的取向**,本 ADR 是第五处一致性检查。
- 性能预算:60 fps 平面 / **90 fps VR(硬性)**;帧预算 16.6 ms / 11.1 ms(VR)。
- **P0 工期基线 6–9 个月 / 31 项系统**(已钉死,不再重算;风险对冲走**范围**不走工期)。
  引入 DOTS 的学习曲线与重构成本,在 P0 窗口内是**范围债**。
- 单机 + 1–4 人 P2P 合作;PC/Steam 优先。
- 本机**无法可靠联网**(集群 DNS + 企业代理串号)⇒ **不得以「上线核查 DOTS 现状」为裁决前提**;
  判据只能取自仓库自有的 `docs/engine-reference/unity/`。

### Requirements

- 必须**结清 ADR-004**,并给出**可在未来被推翻时清楚识别**的条件(不是「永远不谈」)。
- 必须**正式记载门 A ↔ `Unity.Entities` 的接缝** —— 它此前全文无记录。
- 必须**撤销或收窄**因「将来可能上 DOTS」而设的形状约束(ADR-016 §九 的「可平移」),
  使其理由**不再悬空**。
- 必须把 **OQ-8** 登记为复评门的前置输入,并与 ADR-005 性能表的量纲对齐(同一输入,同一出处)。
- 裁决须**不依赖任何 post-cutoff API**(同 ADR-005 的做法)。

## Decision

**裁决:分层。sim 侧永不上 DOTS(结构性排除,依据门 A);表现层留一个带触发条件的复评门
(触发 = OQ-8 标定后同场实体数进入 DOTS 甜点区,~~阈值待用户裁定~~ → **✅ 2026-09-20 用户裁定:同场表现实体 ≥ 100 或 表现层帧时间 ≥ 8 ms**);触发前表现层同样不引入 DOTS。
ADR-004 就此结案。并撤回 ADR-016 §九「以便平移 DOTS」的理据。**

### 一、sim 侧结构性排除 —— 不是规模判断

**门 A(ADR-005)**:sim 程序集必须是独立 asmdef 且 `"noEngineReferences": true`;
**门 B(ADR-005)**:EditMode 反射测试断言 sim 程序集不引用 `UnityEngine.*`。

`Unity.Entities` / `Unity.Burst` / `Unity.Jobs` / `Unity.Mathematics` **全部是引擎 / 引擎包程序集**。
因此在 sim 侧引入 DOTS 的唯一路径是:

```
引入 DOTS → Unity.Entities 进入 sim 引用集 → 门 A("noEngineReferences": true) 失败 → 拆门 A
          → sim 不再零引擎依赖 → 门 B 失效 → ADR-005 的「确定性结构面」瓦解
```

**这是二选一,不是权衡**。结论:**sim 侧永不上 DOTS**。此结论**不随规模变化** ——
即便 OQ-8 标定出天文数字的病人数,sim 的正确应对是**优化纯 C# 值类型**(分桶 / 增量求值 /
冻结尾区,ADR-016 §九 已立此法),而非拆门 A。

### 二、正式记载:门 A ↔ DOTS 接缝(此前全文无记录)

> **本节的价值在于「把一件潜规则写成明规则」。** 在 ADR-017 之前,门 A 与 DOTS 的冲突
> **全文无任何一处记载** —— 一个不知情的实现者完全可能「为了提高性能」在 sim 侧引入 Burst,
> 而 `"noEngineReferences": true` 会**编译失败**(这是好事),但**为什么**失败、该怎么办,
> 无从查考。

- **冲突本体**:门 A 的 `"noEngineReferences": true` 与 `UnityEngine.*` 的**全部**引擎程序集互斥;
  DOTS 栈(Burst / Jobs / Entities / Mathematics)**全部**是引擎程序集。
  ⚠️ **论据订正(2026-09-21 · 承 `architecture-review-2026-09-20.md` RC-3)**:`noEngineReferences`
  门控的是**引擎模块程序集**(`UnityEngine.*Module`),**不门控 UPM package 程序集** ——
  `Unity.Entities` / `Unity.Burst` / `Unity.Jobs` / `Unity.Mathematics` 是 **package** 程序集,
  在 asmdef 未显式列 `references` 时仍可能被自动解析进来。⇒ 上一行「会编译失败」的**机制归因
  不完全**;`noEngineReferences` 是**必要非充分**,**充分性由下行白名单断言承担**。**裁决结论
  (sim 侧永不上 DOTS)不受影响**;本订正确认「谁在挡」= 白名单断言,不是 asmdef flag。
  (标记号 A2 验证法:`using Unity.Entities` + 未列 references 的编辑期实测,归 U0a spike 批。)
- **实现期的硬化要求**:sim 程序集的引用集必须被一条 **EditMode 断言**固定为「恰好 = BCL
  (`System.*` / `System.Runtime.*`)」—— 把「约定」升为「**构建失败**」。
  这同时是门 B 的加强(门 B 只断言不引用 `UnityEngine.*`;此断言断言引用集白名单)。
- **不得绕过**:任何「用 `[BurstCompile]` 只加速一个热点函数」的局部引入**同样**撞门 A
  (Burst 是 `Unity.Burst` 程序集)。

### 三、表现层 —— 留复评门,触发前不引入

**表现层**指:渲染 / 动画 / NavMesh 群集转向 / 大规模 VFX / 视觉定位 —— 一切**不进 sim** 的层。
表现层**不受门 A 约束**(它本来就允许引用引擎)。

**复评门(条件式,不预设结论)**:

| 项 | 内容 |
|----|------|
| **触发输入** | ~~OQ-8 标定完成~~ → **✅ 已完成(2026-09-20)**:「在场才模拟」定案 · `CAP = 24` · `TICK_SECONDS = 0.05` |
| **触发判据** | **✅ 2026-09-20 用户裁定**:同场**表现层**实体数 ≥ **100** **或** 表现层帧时间 ≥ **8 ms**(≈ 16.6 ms 帧预算的 48%)—— ⚠️ 计数对象是**表现层实体**(群集 / VFX / 批处理),**不是** sim 病人数;后者上界 24 由构造 < 100 ⇒ **门在 sim 永不触发** |
| **判定对象** | **仅表现层**作用面:群集转向(NavMesh 群集 / 流场)· 大规模 VFX · 渲染批处理(GPU Resident Drawer / Hybrid Renderer)· 若采用则 Unity Physics 与 PhysX 的并存边界 |
| **不得触碰** | **sim 侧任何部分**(§一 已排除)。表现层上 DOTS 时,sim 与表现层的既有单向驱动契约(ADR-016 §一 · §六)**不变** |
| **执行方式** | 触发后**另开 ADR**(不得以本 ADR 的修订直接改判),须实测 post-cutoff DOTS(Entities 1.3+ / 6.3)并登记 spike 结果 |

> **阈值曾是有意留白的** —— 本项目的一贯纪律:**数值用户自己调**;本 ADR 只固定**机制**(谁触发、判定什么、走什么流程)。
> **✅ 2026-09-20 用户填值:同场表现实体 ≥ 100 · 表现层帧时间 ≥ 8 ms(≈48% of 16.6 ms)。**
> ⚠️ 这是**门的判据值,不是性能预算** —— 帧预算仍为 16.6 ms(平面)/ 90 fps(VR),本值只回答「何时另开 ADR 复评 DOTS」。
> `plugins/dots-entities.md` 的「1000s–10,000s」是引擎参考的量级提示,低于本项目的门阈 —— 取 100 是**保守触发**(早复评优于晚)。

**触发前**:表现层同样**不引入 DOTS**(含 Burst / Jobs)。理由 = 一致性:
全案四连的「运行期零第三方 / 零多余技术栈」取向(ADR-014/015/016 + 本 ADR §一)不允许
在无触发条件下预先引入一套会改变构建流程与调试方式的栈。

### 四、撤回 ADR-016 §九「以便平移 DOTS」的理据

ADR-016 §九 要求决策器保持「批处理友好」形状:**值 struct · 无引用类型状态 · 无虚调用热点**,
其所述理由是「使 R-11 若判『上 DOTS』时,决策器可平移而不重写」。

**本 ADR §一 判 sim 永不上 DOTS ⇒ 该理据随之失效。** 处理方式**不是**撤销该形状要求
(**形状仍然要保留**),而是**换掉它的理由**:

| | 旧理由(撤回) | 新理由(保留形状) |
|---|---|---|
| 值 struct(无引用类型状态) | 便于平移 DOTS | **缓存局部性** + **可序列化**(ADR-012 黄金夹具要对拍) + **确定性**(无 GC 抖动、无共享可变引用) |
| 无虚调用热点 | 便于 Burst 编译 | **可测性**(决策可单测)+ **批量增量求值**(ADR-016 §九 的节流 / 冻结尾区) |
| 决策器「批量友好」 | 为未来 DOTS 留门 | 为**当下**的纯 C# 分桶求值服务(与 DOTS 无关) |

**结论**:形状不变,理由从「未来主义」改为「当下主义」。这**加强**而非削弱 ADR-016 §九 ——
一条不悬空的约束比一条悬空的约束更可能被遵守。

### Architecture

```text
                        ┌─────────────────────────────────────────┐
                        │  sim 侧(门 A:"noEngineReferences": true)│
                        │  引用集 = BCL only({System.*})           │
                        │  ┌───────────────────────────────┐      │
                        │  │ Fix / 三流 / AI 决策器 / 9 / 34 │      │
                        │  │ 纯 C# 值类型 · 整数定点域        │      │
                        │  └───────────────────────────────┘      │
                        │   ✗ Unity.Entities  ✗ Unity.Burst        │
                        │   ✗ Unity.Jobs      ✗ Unity.Mathematics  │  ← §一 结构性排除
                        └──────────────┬──────────────────────────┘
                                       │ 单向驱动(事件流 / 只读 DTO)
                                       ▼
        ┌──────────────────────────────────────────────────────────────┐
        │  表现层(允许引用引擎;不受门 A 约束)                          │
        │  ┌────────────┐ ┌──────────┐ ┌────────────┐ ┌──────────────┐  │
        │  │ 渲染 / 动画 │ │ NavMesh  │ │ 大规模 VFX │ │ 44 音频 / 42 │  │
        │  │            │ │ 群集转向  │ │            │ │              │  │
        │  └────────────┘ └──────────┘ └────────────┘ └──────────────┘  │
        │        ▲                                                      │
        │        │ §三 复评门:仅此层可评估 DOTS                          │
        │        │ 触发 = OQ-8 ✅ + 阈值 ✅ 100 / 8ms(09-20 裁)         │
        │        │ 触发前不引入;触发后另开 ADR(不得改判)                 │
        └──────────────────────────────────────────────────────────────┘
```

### Key Interfaces

本 ADR **不新增任何接口** —— 它是「不引入」的裁决。其可验证产物是**约束**,不是契约:

```
# sim 程序集定义(ADR-005 门 A,本 ADR §二 硬化)
Sim.asmdef:
{
  "name": "Sim",
  "noEngineReferences": true,          # 门 A —— 与 DOTS 全栈结构性互斥(§一)
  "references": []                      # ⚠️ 2026-09-20 ADR-025 V-6 订正(原文不删,就地加注):
                                        #   该空集是**示例简写**,作为断言文本**不可能成立** ——
                                        #   `Sim` 必须能看见 `SimEvent` / 六个抽象点,故契约程序集
                                        #   必然在其引用集内(ADR-025 §Context「Current State」)。
                                        #   现行裁决(ADR-025 §①):`Sim` 引用集**恰 = {BCL, Sim.Contracts}**
                                        #   —— 白名单升格为「恰等于两件套」,仍不含任何引擎程序集,
                                        #   §一「sim 侧永不上 DOTS」的结构性结论**不受影响**。
                                        #   原注「引用集 = BCL only」自此作废。
}

# §二 硬化断言(EditMode,schema 同门 B)
[Test] void SimAssembly_ReferencesAreBclOnly()
{
    // 断言 Sim 程序集的程序集引用集 ⊆ {System.*, System.Runtime.*}
    // 任何 Unity.Entities / Unity.Burst / Unity.Jobs / Unity.Mathematics 出现 ⇒ 失败
    // 把「约定」升为「构建失败」;门 B 只断言"不含 UnityEngine.*",本断言断言白名单
}

# §三 复评门的登记形态(非代码,登记在 technical-preferences §Performance Budgets 侧)
# DOTS_REVIEW_GATE:
#   input:   ✅ OQ-8 已标定(2026-09-20:在场才模拟 · CAP=24 · TICK_SECONDS=0.05)
#   trigger: presentation_entity_count >= 100  OR  presentation_frame_time_ms >= 8.0   # ✅ 2026-09-20 用户裁定
#   scope:   PRESENTATION LAYER ONLY
#   action:  open a NEW ADR (do not amend ADR-017 in place)
```

### Implementation Guidelines

1. **不要在 sim 侧尝试任何 DOTS / Burst / Jobs 引用** —— 它会编译失败(门 A),
   而失败原因见 §二。若你认为需要 DOTS,正确的路是**走 §三 的复评门**,不是拆门 A。
2. **保持 ADR-016 §九 的「批处理友好」形状**,但**不要再用「为了 DOTS」当理由**(§四):
   理由是缓存局部性、可序列化、可测性、无 GC 抖动。
3. **sim 侧的性能应对手段**(规模变大时,按此顺序,全部纯 C#):
   增量求值 → 分桶 → 冻结尾区(远区不逐 tick 求值,ADR-016 §九 已立)→ 缓存友好布局。
   **不包含**:引入 Burst / Jobs。
4. **表现层的性能手段**(DOTS 之外):GPU Resident Drawer(Unity 6.3 已有,URP)·
   Addressables 流式分组(ADR-014)· LOD · 群集转向仍走 NavMesh(ADR-016 §五,
   **NavMesh 只在表现层**)。
5. 复评门触发时,**先核 OQ-8 的标定值是否仍为 24**(✅ 已于 2026-09-20 结清,改判须走 9 侧),再按阈值(✅ 100 / 8 ms)判,再另开 ADR。

## Alternatives Considered

### Alternative 1: 分层 —— sim 结构性排除 + 表现层留复评门(本裁决)

- **Description**:sim 侧因门 A 被结构性排除;表现层留一个带触发条件的复评门(触发 = OQ-8 + 阈值)。
- **Pros**:关掉 sim 的门(唯一正确的技术结论),同时**兑现 ADR-004「等规模数据再决策」的原意**
  —— 复评门就是那个「等」的机制化;不悬空;不在 P0 窗口引入范围债。
- **Cons**:表现层的复评门需要**日后有人记得触发**(靠 §三 的登记点缓解);
  ~~阈值留白意味着本 ADR 不给出「多少实体算多」的答案~~ → **✅ 2026-09-20 用户填值(100 / 8 ms),该 Cons 已消除**。
- **Estimated Effort**:0(P0 不引入任何新栈)。
- **Rejection Reason**:**采纳**。

### Alternative 2: 全案永不上,一期关门

- **Description**:sim 与表现层都不引入 DOTS,不给复评窗口。
- **Pros**:最彻底的关门;与「运行期零第三方」取向最一致。
- **Cons**:**违背 ADR-004 的原意** —— ADR-004 说「等规模数据出来再决策」,而规模数据
  **至今不存在**;直接关门等于在一个**判据缺失**的状态下永久封死一个选项。
  且若 OQ-8 标定出真的大规模,重新打开需要「推翻一份 Accepted ADR」,成本高于留一个门。
- **Estimated Effort**:0。
- **Rejection Reason**:**用户未选** —— 判据缺失时永久封死不如留一个带条件的门。
  (若用户日后改判此选项,只需删除 §三 的复评门,§一 不受影响。)

### Alternative 3: sim 侧上 DOTS

- **Description**:sim 用 `Unity.Entities` + Burst,以换来极致批处理性能。
- **Pros**:若实体数真达 10,000s,批处理性能显著。
- **Cons**:**结构性不可行** —— 撞门 A(§一 / §二)。且 Burst 编译的定点整数中间乘
  会与 ADR-012 的 **F7 spike(IL2CPP 有符号溢出为 UB)** 叠加第二层不确定的溢出语义;
  且 decision 的「可重建」不变量将依赖 Burst 的逐平台行为(ADR-016 §一)。
- **Estimated Effort**:巨大(sim 全栈重写 + 拆门 A + 重做确定性论证)。
- **Rejection Reason**:**结构性排除**。这不是成本权衡 —— 它直接瓦解 ADR-005 的确定性结构面。

### Alternative 4: 表现层立即上 DOTS

- **Description**:不等 OQ-8,表现层立即引入 DOTS(群集 / VFX / 渲染批处理)。
- **Pros**:可以提前熟悉栈。
- **Cons**:在**无触发条件**下引入一套改变构建流程 / 调试方式 / 学习曲线的栈,
  是 P0 窗口的**范围债**(6–9 个月基线已钉死);且规模数据不存在 ⇒ 无法论证收益;
  与全案四连的「零多余栈」取向相悖。
- **Estimated Effort**:中–大(学习 + 集成 + 调试方式改变)。
- **Rejection Reason**:**无判据的收益不足以支付 P0 窗口的成本**。

### Alternative 5: 只引 Jobs / Burst,不引 Entities(局部加速)

- **Description**:sim 侧不引入 ECS,但用 `Unity.Jobs` + `[BurstCompile]` 加速若干热点函数。
- **Pros**:看似比全 DOTS 轻;局部加速不改变数据布局。
- **Cons**:**同样撞门 A** —— `Unity.Jobs` / `Unity.Burst` **都是引擎程序集**;
  「只加速一个函数」不改变引用集白名单的性质。且 Burst 的整数溢出语义与
  ADR-012 F7 spike 叠加;且会让 sim 的「逐位可对拍」判据多一个编译器维度。
- **Estimated Effort**:小–中(误以为小)。
- **Rejection Reason**:**门 A 不看规模,只看引用集**。这是本项目最容易被误解的一点 —— §二 记此。

### Alternative 6: 维持「待决」,先标定 OQ-8

- **Description**:不改 ADR-004 状态,等 OQ-8 标定后再立 ADR。
- **Pros**:最保守;不在判据缺失时下结论。
- **Cons**:ADR-004 会**继续挂账**,而 ADR-016 §九 的「可平移」理据**继续悬空**;
  13 / 27 / 34 的规模假设继续靠「对冲」而非「假设」存在 ——
  而 §一 的结论**根本不依赖 OQ-8**(门 A 与规模无关)。既然如此,「先标定再裁决」不成立。
- **Estimated Effort**:0。
- **Rejection Reason**:**用户未选** —— sim 侧的答案在规模未知时就已确定(§一);
  继续挂账只是让一条已可裁决的问题继续悬空。

## Consequences

### Positive

- **ADR-004 结案** —— 项目挂账最久的待建 ADR 兑现。
- **sim 侧的性能假设收敛为纯 C# 值类型**(9 / 13 / 27 / 34 / 36),不再需要对冲 DOTS 迁移的
  形状约束「为了将来」而无期限保留。
- **门 A ↔ DOTS 接缝首次成文**(§二),把一个潜规则升为明规则 + 构建失败断言。
- **ADR-016 §九 的「可平移」理据收窄**(§四)—— 顺便把该形状要求的理由从未来主义改为当下主义,
  反而**加强**了它。
- **表现层的复评门是可执行的**:有触发输入、有判定对象、有执行方式(另开 ADR),
  而非一句「以后再说」。

### Negative

- **表现层的复评门依赖「日后有人记得触发」。** 缓解:§三 的登记点写在
  `technical-preferences.md` 的性能预算侧,且 OQ-8 标定本来就是一个 must-do 前置。
- ~~**阈值留白**(§三)~~ → **✅ 2026-09-20 已填(同场表现实体 ≥ 100 · 表现层帧时间 ≥ 8 ms)**。原问题「多少实体算多」就此有答案 ——
  但按项目纪律,那是用户的量级判断,不该由 ADR 拍定。
- **若 OQ-8 标定出真的大规模**,表现层将面对「不复评则性能不足 / 复评则引入新栈」的两难;
  缓解:§三 的执行方式是「另开 ADR」,即允许完整权衡,不预先封死。

### Neutral

- **`technical-preferences.md:15` 的悬空 if**(「若大型 NPC / 瘟疫模拟走 DOTS,则该子系统用
  Unity Physics」)获得确定语义:**sim 侧永不成立;表现层在其触发后才可能成立**。
- **`unity-dots-specialist` 的定位**随之明确:它在 P0 无 sim 侧工作;其职责是
  **§三 复评门触发后的表现层评估**。(保留该专家,不裁撤 —— 复评门是真实的门。)

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 实现者不知门 A 与 DOTS 互斥,在 sim 侧引入 Burst 后困惑于编译失败 | 中 | 低(编译失败,不会静默) | §二 成文 + 硬化断言报错信息指向本 ADR |
| 表现层复评门无人触发,性能问题被拖到发版前 | 中 | 中 | §三 登记于性能预算侧;OQ-8 标定是 must-do;§Risks 在此显式记账 |
| ~~阈值留白被误读为「不需要阈值」~~ | ~~低~~ | ~~中~~ | ✅ **已消除(2026-09-20 填值 100 / 8 ms)**;留白期由 §三「数值用户自己调」纪律兜住 |
| ~~OQ-8 长期不标定 ⇒ 复评门与 ADR-005 性能表**同时**悬空~~ | ✅ **已消除(2026-09-20 结案)** | — | 两处指向同一前置,已于同日一并标定(CAP 24 · 20 Hz);`adr-005` 加注随之闭合 |
| 「不上 DOTS」被误读为「性能不需要优化」 | 中 | 中 | §Implementation 3 / 4 明列纯 C# 与表现层的**实际**优化手段 |
| 日后有人以「本 ADR 可修订」为由直接改判(绕过另开 ADR) | 低 | 高 | §三 明写「不得以本 ADR 的修订直接改判」;§一 的排除须改门 A 才能推翻,门槛天然很高 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | **不变**(本裁决不引入新栈) | 16.6 ms 平面 / 11.1 ms VR |
| Memory | — | **不变** | 待定 |
| Load Time | — | **不变** | 待定 |
| 构建流程 | — | **不变**(无新包) | — |

> **本 ADR 的性能含义是「负的」:它不带来任何性能收益,只带来清晰度。**
> sim 侧的性能判据仍是 `实体数 × tick 频率 × 单次求值`(ADR-005 性能表),
> 系数曾待 **OQ-8** 标定 —— 与 §三 复评门**共用同一前置输入**。✅ **2026-09-20 两处同批结清:24 实体 × 20 Hz;残留 = 单次求值成本(实测,非裁决)**。

## Migration Plan

**本项目尚无 9 / 13 / 27 / 34 / 36 的实现,故无迁移 —— 本 ADR 是「不做错事」,不是「改现状」。**

1. **登记复评门** —— 在 `technical-preferences.md` 的性能预算侧加 §三 的 `DOTS_REVIEW_GATE` 条目
   (触发输入 / 判据 / 作用面 / 执行方式)。*验证:条目存在且指向本 ADR。*
2. **硬化门 A** —— 加 §二 的 EditMode 白名单断言(sim 引用集 ⊆ BCL)。*验证:故意加一条
   `using Unity.Burst;` 后测试失败且报错指向本 ADR。*
3. **同步 ADR-016 §九** —— 按 §四 换理由(保留形状,撤回「以便平移 DOTS」)。*验证:GDD/ADR 内
   无残留「为了 DOTS」表述。*
4. **同步 `technical-preferences.md`** —— ADR-004 条目标 ✅ Accepted 并指向本 ADR;
   「候选但未采纳」栏的 DOTS 行改写。*验证:grep「待建」不再命中 DOTS。*

**Rollback plan**:本裁决的撤回**不是**「删除本 ADR」,而是:
- 若用户改判 Alt 2(全案关门):删除 §三 复评门,§一 不变;
- 若用户改判「表现层上 DOTS」:走 §三 的另开 ADR 流程(不修订本 ADR);
- 若用户改判「sim 上 DOTS」:**必须同时推翻 ADR-005 的门 A**,即本 ADR §一 的排除
  是 ADR-005 的**推论**而非独立裁决 —— 这一点让 rollback 的代价**显式可见**。

## Validation Criteria

- [ ] **门 A 硬化断言存在且生效**:sim 程序集引用集白名单断言在 CI 中运行;
      故意引入 `Unity.Burst` 时测试**失败**,报错信息指向 ADR-017 §二(核心)
- [ ] **复评门登记存在**:`technical-preferences.md` 含 `DOTS_REVIEW_GATE` 条目,
      字段 = 输入(OQ-8 ✅ 2026-09-20 标定)/ 判据(✅ 100 / 8 ms)/ 作用面(表现层)/ 执行方式(另开 ADR)
- [x] ~~**阈值留白被识别为待用户裁定**~~ —— ✅ 2026-09-20 由用户填值(100 / 8 ms),留白期结束;登记处三处同步(§三 表 / §代码形态 / `technical-preferences.md`)
- [ ] **ADR-004 结案**:`technical-preferences.md` 中 ADR-004 条目为 ✅ Accepted 且指向本 ADR
- [ ] **sim 侧零 DOTS 引用**:grep `Unity.Entities|Unity.Burst|Unity.Jobs|Unity.Mathematics`
      在 sim 程序集目录下**零命中**
- [ ] **ADR-016 §九 理由已换**:该节无「为了 DOTS」表述;「批处理友好」形状**保留**,
      理由改为缓存局部性 / 可序列化 / 可测性 / 无 GC 抖动
- [ ] **无悬空 if**:`technical-preferences.md:15` 的物理配置行有确定语义
      (sim 侧永不成立;表现层触发后可能成立)
- [ ] **复评门的执行方式被写死**为「另开 ADR」,且本 ADR 明写「不得以本 ADR 的修订改判」
- [ ] **不引入任何新依赖**:`packages/manifest.json`(或等价)无新增 DOTS 相关包
- [ ] **性能表量纲对齐**:ADR-005 性能表的 `实体数 × tick 频率 × 单次求值` 与本 ADR §三
      复评门指向**同一** OQ-8 前置(一处标定,两处解锁 —— ✅ 2026-09-20 已解锁)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | 冻结方向③「不上 DOTS,只做零成本预防」(`:15`);`PatientState` 传值不传引用「否则 DOTS 迁移要重写每个调用点」(`:510-511`);**OQ-8**(`:1329-1335`) | **冻结方向③ 升为结构性结论**(不只是 9 的选择,而是 sim 全层的边界);传值要求**保留**但理由更换(§四);**OQ-8 被登记为复评门前置输入** |
| `design/gdd/disease-simulation.md` | 34(消费方) | 34 只订阅 9 的生态区级聚合;P0 病种硬上限 ~8 | 34 的实现**从纯 C# 出发**;规模增大时的应对是纯 C# 优化(§Implementation 3),不是 DOTS |
| `design/gdd/systems-index.md` | 13 / 27 / 34 / 36 | P0 只留一个敌人原型 / 4 生态区 / 建造只留摆放 —— 克制规模 | §Current State 的规模表;本裁决**与该克制一致**,不为未存在的规模预置栈 |
| `design/gdd/game-concept.md` | 全局 | 开放世界性能为关键技术挑战(`:568`);60/90 fps 预算;P0 基线 6–9 个月 | 性能应对手段明列(§Implementation 3/4);P0 窗口不引入新栈学习成本 |

> **Foundational decision** —— 本 ADR 无独立 GDD 需求(它是 ADR-004 的兑现)。
> **Enables**:9 / 13 / 27 / 34 / 36 的性能假设收敛;ADR-005 性能表量纲的输入确定;
> ADR-016 §九 的形状要求理由去悬空。

## Related

- **ADR-005**(`adr-005-deterministic-sim.md`)—— **门 A / 门 B 是本 ADR §一的全部依据**;
  本 ADR 未更改 ADR-005,而是**指出其推论**(sim 侧的 DOTS 排除)。
  ADR-005 性能表已加注:量纲 = 实体数 × tick 频率 × 单次求值,系数待 OQ-8。
- **ADR-012**(`adr-012-cross-platform-determinism-ci-gate.md`)—— **F7 spike(C# int64 回绕
  vs IL2CPP 有符号溢出 UB)**:若 sim 侧上 Burst,该问题会叠加第二层(§Alternatives 5)。
- **ADR-016**(`adr-016-ai-architecture.md` §九)—— 其「可平移 DOTS」理据由本 ADR §四 收窄;
  「批处理友好」形状保留,理由更换。
- **ADR-014 / ADR-015 / ADR-016** —— 三连的「运行期零第三方」取向;本 ADR 是**第五处一致性检查**。
- `docs/engine-reference/unity/plugins/dots-entities.md` —— 仓库自有的 DOTS 权威件
  (Use / DON'T-use 指南 · Entities 1.0+ 完全重写)。
- `technical-preferences.md` —— ADR-004 条目(本 ADR 兑现);复评门登记点;
  物理配置行的悬空 if。
