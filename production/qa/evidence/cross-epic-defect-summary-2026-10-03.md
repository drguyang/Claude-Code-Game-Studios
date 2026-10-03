# 跨 Epic 缺陷汇总 —— 四份补做评审的合并视图

> **编制**: 2026-10-03 · **来源**: `production/qa/evidence/review-{modular-building,world-ecozones,player-controller,emergency-procedures}-2026-10-03.md`
> **基线**: HEAD `7d7dd85` · **复核**: 主会话逐条实测,**已纠正一处 agent 误报**
> **用途**: 供排优先级。⚠️ **本件是索引,不是证据** —— 每条的可复现命令在其源报告内。

## 一句话结论

**四个 epic 全部「不转 Complete」,但不是因为同一件事。** 缺陷分三类:

| 类 | 含义 | 危害 |
|---|---|---|
| **A 类 · 判据空转** | AC 已勾,但判据恒真 / 与别的测重复 / 只查常量非零 | **最危险** —— 绿是假的 |
| **B 类 · 已勾无实现** | AC 已勾并自述「已实现」,但实现/测试**不存在** | 等同虚报 |
| **C 类 · 实现缺口** | 代码真缺,判据也没写 | 常规欠账 |

**A/B 两类是本批的核心发现** —— 它们让「看起来全绿」的 epic 实际未被验收。

---

## A 类 · 判据空转(最危险:绿是假的)

| # | Epic | AC | 恒真/空转的机制 | 落点 | 复核 |
|---|---|---|---|---|---|
| ~~**A1**~~ ✅**已闭** | emergency-procedures | ~~**AC-10-02** `[B]`~~ | 断言取 `Sim.Contracts` 程序集,却查其名 `Contains("Input")` —— **该名永不含 "Input"** ⇒ 零扫描、恒过 | `reading_contract_test.cs:174` | ✅ 主会话实测成立 |
| ~~**A2**~~ ✅**已闭** | emergency-procedures | ~~**AC-10-03** `[B]`~~ | 与 `:20-38` 的字段类型测**逐字重复**;无任何操作码/IL 检查 | `reading_contract_test.cs:154-166` | ✅ 成立 |
| ~~**A3**~~ ✅**已闭** | player-controller | ~~**AC-1-06a/b/c** `[B]`×3~~ | 三个测试**都只查 `config.SpeedWalk > 0`** ⇒ 手填常数与派生量**无法区分** | `locomotion_chain_test.cs` | ✅ 成立 |
| ~~**A4**~~ ✅**已闭** | player-controller | AC-1-10② | 只查 `LatticeSizeMm` **字段存在**,不验 `LATTICE_SIZE >= radius*2` | `controller_foundation_test.cs` | ✅ 成立 |
| ~~**A5**~~ ✅**已闭** | player-controller | AC-1-17 | 用 `field.Name.Contains("frame")` **字段名匹配**,非 AST ⇒ 改名即绕过 | 同上 | ✅ 成立 |
| ~~**A6**~~ ✅**已闭** | emergency-procedures | AC-10-24 | `holdMode`/`accessibilityOn` **参数被忽略** ⇒ 两模式必然同值,测试恒绿 | `ModalPhaseEvaluator.cs:82-99` | ✅ 成立 |
| ~~**A7**~~ ✅**已闭** | player-controller | AC-1-28 | 检测到 `Sim` 引用时 `Assert.Pass` ⇒ **pass-through**,不真失败 | `controller_foundation_test.cs` | ✅ 成立(已知技术债) |

> **同型根因**:判据**停在「对象存在」层**,未下沉到**字段/操作码层**。

> ### ✅ A 类**全部 7 条已闭**(2026-10-03)
>
> | 批 | 内容 |
> |---|---|
> | A1/A2 | emergency `AC-10-02/03` → 新增 `EmergencyIntegerGates`(**真 IL 扫描**:引用面 + call 图闭包 + 负向夹具 + 非空转守卫) |
> | A4 | player-controller `AC-1-10②` → 真约束断言 + 负例 |
> | A5 | `AC-1-17` → 类型面 + 源码面双判据 |
> | A6 | emergency `AC-10-24` → `holdMode` 真参与 + **非空转守卫**(闭集外须抛) |
> | A7 | `AC-1-28` → 真断言 + **具名豁免**(仅 `Sim` = RecipeDataSet 债;AC 记**部分成立**) |
> | A3 | player-controller `AC-1-06a/b/c` → **重定判据面**(见下) |
>
> **A3 的重定(用户裁定取「甲」)** —— 查证发现原测「只查 `SpeedWalk > 0`」**是因无物可查**:
> 1 侧**零 `K_TERRAIN_MAX`/`K_CONTEXT_MAX` 概念**(它们是 `SteadyStateSpeed`/`ComputeVTarget` 的**入参**),
> `DeriveMaxSpeed` 全库不存在;story-003 `:19` 自陈**所有者反转**(约束对象 = `LATTICE_SIZE`,归 6)。
> ⇒ 现:① **06a** 验「1 不自持派生量」+ 6 侧差分神谕(独立第二路径扫表);
> ② **06b** 验变异性 + 双向(注入抬上界 ⇒ 红;删行 ⇒ 绿);
> ③ **06c 降级 NOT-RUN** —— 其载体 = Roslyn,而 **ADR-024 §⑤ 明令不引**;
> 判据面在 6 侧 `AC-6-07` 的值级守卫。**AC-1-06c 的 `[x]` 已撤**。
> 判据落**测试装配**(已引 `Sim`),**不动生产 asmdef**。

> ### ✅ A1/A2 已闭(2026-10-03)
> 新增 `unity/Assets/Editor.Tools.Gates/EmergencyIntegerGates.cs`(与既有 `EcozoneIntegerGates` 同族,
> 复用它已验的解析器形态),提供**真 IL 判据**:
> - **AC-10-02** → `CheckInputBoundaryIl`:扫**真 3 侧**(`Gameplay.Input`)的**引用面 + 全类型 IL**;
>   配负向夹具 + 「扫描方法数 > 0」非空转守卫;
> - **AC-10-03** → `CheckJudgeIntegerIl`:`JudgeEvaluator` 四根的**真实 call 图闭包**,
>   扫浮点指令 + 局部/签名类型;配**闭包形状守卫**。
>
> **突变测试坐实非空转**:向 `ScaleFixed` 注入浮点 ⇒ `test_ac1003_judgePath_zeroFloatIl` **红**。
>
> ⚠️ **实现期三处自身错误(已修,留痕)**:① 闭包初版用 `callee.Resolve()` ⇒ 跨程序集(BCL)
> `AssemblyResolutionException`(改为**名字级闭包**,零 Resolve);② 突变第一版 `double probe=1.5; _=probe;`
> **被编译器死代码消除** ⇒ 未触发;③ 突变第二版破坏编译(`CS0103`)。⇒ 改**结构不变**的突变后坐实。
> emergency 报告的 §5 对此有专门分析(见下 §方法论)。

## B 类 · AC 已勾但实现/取证不存在

| # | Epic | AC | 自述 vs 实测 | 复核 |
|---|---|---|---|---|
| ~~**B1**~~ ✅**已处置** | world-ecozones | story-004「白名单静态断言」 | AC **已勾**并自述「已实现」;但 `Editor.Tools.Gates/` **零** POI 白名单断言,测试侧仅**注释**提及 `{4,25,37}` | ✅ 成立 |
| ~~**B2**~~ ✅**已处置** | world-ecozones | story-004 发现门集成 `[B]` | AC **已勾**;但 `chunk_activation_test.cs` **7 例全为 chunk 拓扑纯函数**,**零** `TryDiscover`/`ActorCellEntered` 用例 | ✅ 成立 |
| ~~**B3**~~ ✅**已处置** | world-ecozones | story-004「`World.unity` 零 gameplay 对象扫描」 | AC **已勾**;**未检索到扫描实现** | ⚠️ 报告标「可疑」,未定论 |
| ~~**B4**~~ ✅**已处置** | emergency-procedures | **DC-5** | `GetRequiredKindWhitelist()` **仅返回字符串数组**,**无校验体** | ✅ 成立 |

> **与既有教训同型**:`B1/B2` 正是 **AC-6-26a 同族失败模式**(B2 原判定)在 story-004 内**重演** ——
> 「AC 已勾 ≠ 判据已执行」。

> ### ✅ B 类全部已处置(2026-10-03)
>
> **处置分两类**(判据对象是否存在决定):
>
> | # | 判据对象 | 处置 |
> |---|---|---|
> | **B2** | `TryDiscover` **已在库** ⇒ 可测 | ✅ **补集成测试 3 例**:
> `firesOnCellEntry_sameTick`(跨格 → 激活 → 发现门,且**同 tick**)· `idempotentOnSecondEntry` ·
> `blockedOnUnloadedChunk`(AC-6-23 保守侧) |
> | **B4** | `StreamRouting` **已在库**(kindgen 产物)⇒ 可校验 | ✅ **补真校验**
> `ValidateRequiredKindsRoutable()`:逐 Kind 断言可路由且落病史流 + 2 例测试 |
> | **B1** | **27 侧零 `PoiState` 引用** ⇒ 无对象可扫 | ⚠️ **撤勾 AC**
> (自陈的实现不存在 ⇒ 留勾 = 虚报);重开条件 = 27 落地时同批补断言 + 负向夹具 |
> | **B3** | **`World.unity` 尚不存在** ⇒ 无对象可扫 | ⚠️ **撤勾 AC**;
> 重开条件 = ADR-023 三场景制落地时同批补构建期扫描 |
>
> ⇒ **「补实现」与「撤勾」都是诚实选项**;不诚实的是**留勾而实现不存在**。

## C 类 · 实现缺口(真缺,且判据也没写)

| # | Epic | 缺陷 | 后果 | 严重度 | 复核 |
|---|---|---|---|---|---|
| **C1** | modular-building | 占用格集恒 = `{anchor}`(`StructureKinds.cs:60-61` 自陈「简化」),**不查模块目录** | **直接抵消 B2**:多格模块的非锚点足迹格**不参与拆除实体检查** ⇒ **B2 的「已修」是纸面** | 🔴 高 | ✅ 实测成立 |
| **C2** | modular-building | `World.OccupyCells/FreeCells` **零调用方**;`Place` 只调 `registry.Register` | Overlay 写路径**未接线** ⇒ F-23-1 `EffectiveWalkable` 对**已放置结构不生效** | 🔴 高 | ✅ 实测成立 |
| **C3** | world-ecozones | host gate 返回 `PoiNotFound`,与「POI 不存在」**同码**(`:100` vs `:103`) | 调用方**无法区分**「我不是主机」与「该 POI 不存在」⇒ 可能触发错误降级路径 | 🟠 中 | ✅ 实测成立 |
| **C4** | emergency-procedures | `SkillMul` **死代码**(算出即弃);稳度门未按 F-10.2 用 `SkillMul(L)` | 公式与文档不符(P0 数值无害) | 🟠 中 | ✅ 成立 |
| **C5** | emergency-procedures | JITTER 用 **C# 裸 `/` 截断** | **Control Manifest 明列 Forbidden** | 🟠 中 | ✅ 成立 |
| **C6** | world-ecozones | `RebuildFromDecoded` **无生产调用方**(仅测试) | 重建链未闭环(归 7a/45) | 🟡 低 | ✅ 成立 |
| **C7** | emergency-procedures | `Cause` 真源不在本处理器(由调用方传,自陈「真实调用方 = 45/P1b」) | AC-10-39 的 `Cause` 在 P0 **无真实生产者** | 🟡 低 | ✅ 成立 |
| **C8** | modular-building | 注册表 id `int` vs `entities.yaml:2068` 的 `i64` | 超 2^31 静默回绕 | 🟠 中 | ✅ 既有登记,未修 |
| **C9** | world-ecozones | Story 005 真身 = **Pending**(桌面走查) | 非代码缺陷,状态确认 | — | ✅ 成立 |

## D 类 · 文档/状态不一致

| # | Epic | 问题 |
|---|---|---|
| **D1** | emergency-procedures | **A8 勘误未同步 AC 表** —— F-10.4 正文 `:617-630` 已订正为 `32768×16385`,但 **AC-10-04a 单元格仍写 `32769×16384`** |
| **D2** | emergency-procedures | story-006 的 Test Evidence 声明证据在 `Tests/PlayMode/`,实际在 **EditMode**;3 项 NOT-RUN 亦全在 EditMode |
| **D3** | emergency-procedures | story-007 头「Complete」而 Test Evidence 仍 `[ ] Pending`;EPIC 表 002–006 标 `Ready` 而各 story 标 `Complete`;story-001 头「5/5」vs 证据「11/11」 |

---

## ❌ 已撤回(agent 误报,复核判定不成立)

| 原报 | 内容 | 复核结论 |
|---|---|---|
| emergency **N-2** | 「`SimEvent` header `Seq` 硬编码 0(未登记),破坏 ADR-008 全序键」 | ❌ **不成立** —— 传 `0` 是**全库既定占位约定**:`EventStream.cs:89-92` 明写「如果事件没有 Seq,则发号」并给 `Seq == 0` 赋值;`PoiStateMachine.cs:129` 同样传 0。**header `Seq` 由流发号,非缺陷。** 已在源报告正文撤回(非仅批注) |

> ⚠️ **本条留档的意义**:它是「不采信 agent 结论、逐条复核」这一纪律的**实际收益** ——
> 若直接采信,会去「修」一个不存在的缺陷,并可能改动正确的全库约定。

---

## 方法论教训(emergency 报告 §5,值得跨 epic 适用)

**为什么前两轮评审漏掉了 story-007 的手搓点?** 该缺陷**不抛异常、不越界、编译通过**,
既有测试只断言 `Kind` 与**事件条数**、**从不读载荷字段** ⇒ 静默通过。根因三条:

1. **「事件存在」≠「载荷正确」** —— 判据停在**事件层**,未下沉到**字段层**;
2. **无「构造路径唯一性」门** —— 手搓 `PayloadRef` 与走 `IPayloadEncoder` 在**类型系统里等价**,
   唯有**源码/IL 级扫描**(b6 门)能区分;前两轮无此门;
3. **重复 struct 副本掩盖类型错配** —— 处理器用的根本不是权威 struct,
   却因副本存在而静默编译通过。

⇒ **方法论结论**:BLOCKING AC 的判据**必须读到被断言对象的字段/操作码层**。
**A 类缺陷(判据空转)正是同型失效模式的现役实例** —— 说明该模式**未被根除**。

---

## 建议的修复优先级

| 序 | 批次 | 内容 | 理由 |
|---|---|---|---|
| **1** | **A 类清零** | A1–A7 全部改为真判据,或**降级为 NOT-RUN** | **禁以空转判据记绿** —— 这是「不得借绿」的直接适用 |
| **2** | **B 类补齐** | B1/B2/B3/B4 补实现或**撤勾 AC** | 撤勾是诚实选项;**留勾 = 虚报** |
| **3** | **C1/C2** | modular 的占用格集 + Overlay 接线 | 它们**抵消了已判「已修」的 B2**,且是 epic 头号交付物 F-23-1 的承重面 |
| **4** | **C3** | we 加 `NotHost` 结果码 | 小而明确,可诊断性 |
| **5** | **C4/C5** | emergency 的 `SkillMul` + JITTER 舍入 | C5 违 Control Manifest Forbidden |
| **6** | **D1–D3** | 文档状态对齐 | 低风险,但影响可信度 |
| **7** | C6/C7/C8/C9 | 各有归属(7a/45/独立轮) | 非本批阻塞 |

⚠️ **四份报告均未实跑 Unity 套件**(静态源码复核)⇒ **任何转 Complete 前须以当前 HEAD 重跑 EditMode/PlayMode**。
