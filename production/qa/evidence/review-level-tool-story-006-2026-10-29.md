# 代码评审原件 —— 关卡工具 story-006(C1 / C2 / C2' / CI)

> **对象**:`production/epics/level-tool/story-006-c1-c2-c2prime-and-ci.md` 及其实现
> (`unity/Assets/Editor.Tools.Level/` 7 件 + `tools/level/run_ci.sh`)
> **日期**:2026-10-29 · **模式**:lean(`/code-review`,双专家并行)
> **评审人**:`unity-specialist`(引擎 / C# / 架构)· `qa-tester`(可测试性)
> **裁定**:⚠️ **CHANGES REQUIRED —— 1 条 BLOCKING 族(3 处同源)+ 7 WARNING + 7 MINOR**
> **本件的证据强度**:两条 BLOCKING 断言由**编排方独立复核**(见 §一 逐条 `file:line` + 复算命令),
> 其余条目按专家报告原文采信并标注「未复核」。

---

## §一 BLOCKING —— 「没跑」在导出路径上被静默成绿(三位一体)

> **共同根因**:`CheckReport` 的三态设计是对的,`LevelCiRunner` 也把 `NotRun` 打印出来了;
> **但导出器走的是另一条路(`CheckOrThrow`),那条路把 `NotRun` / `Warnings` 全丢了。**

| ID | 判定 | 落点 | 证据(可复算) |
|---|---|---|---|
| **B-1** | **导出唯一门 `CheckOrThrow` 只判 `IsGreen`,`Warnings` / `NotRun` / `Unimplemented` 整条丢弃** ⇒ 一份违反 C2 的关卡可以**正常导出成功**,且**一行提示都不打印** | `LevelConsistencyCheck.cs:276-283`;调用方 `LevelExporter.cs:96,160,209,270,319,372,521` | `grep -c "CheckOrThrow(src)" unity/Assets/Editor.Tools.Level/LevelExporter.cs` ⇒ **7**;`Check(src)` 调用**零实参** ⇒ `engineParams=null` / `catalog=null` / `navSampler=null` |
| **B-2** | **C2 的烘焙侧缺失被静默成绿**:`terrain == null ⇒ return true`,与同一段「引擎侧缺失 ⇒ 登记 `NotRun`」的**口径不对称**。形态可达(`LevelSource` 允许 `terrain=null`) | `WalkabilityParityCheck.cs:46` · `LevelConsistencyCheck.cs:227-239` | `grep -n "terrain == null" unity/Assets/Editor.Tools.Level/WalkabilityParityCheck.cs` ⇒ `:46`;**全树零夹具**覆盖该组合 |
| **B-3** | **C3 无 `NotRun` 登记**:C1 / C2 / C2' 三处都有,C3 没有 —— 「未跑」与「跑了且无告警」在报告上不可分 | `LevelConsistencyCheck.cs:220-222` | 同文件内三处 `notRun.Add(...)` 均无 C3 分支 |

> **为什么合并成一条族而不是三条独立 BLOCKING**:三处的失效**是同一个**
> ——「报告里没有这一项」被读者当作「这一项通过了」。
> 修的时候必须**同批**,只修一处会留下另一种成因的同型洞。
>
> **它为什么是 BLOCKING 而不是 WARNING**:本仓已记录五次同型事故
> (`case-system 001–004` · `audio 007/008/009` · S18-1 规则九门 · `TD-s13-01`),
> 其中最贵的一种正是「**用一句自述顶替一个从未存在的门**」。
> 本 story 的 AC-5 声明「七项全部实现」,而导出路径实际只跑槽位子集 —— 形态相同。

---

## §二 WARNING(7 条)

| ID | 内容 | 落点 | 备注 |
|---|---|---|---|
| **W-1** | `StepOffsetMm` 缺失时 `stepMm = 0` 是**自造阈值**:「有足迹声明却无地形声明」时,判决数由残缺输入造出 | `LevelConsistencyCheck.cs:244` | 建议改 `NotRun` 或显式硬失败 |
| **W-2** | C2' 的**足迹比对零生产消费者**:`catalog: null` 写死在 CI 入口 | `LevelCiRunner.cs:155` | `grep -rn "moduleCatalog:" unity/Assets --include=*.cs \| grep -v Tests` ⇒ 0 |
| **W-3** | **注释顶替门**:`LocalOccupancy` 的 doc 写「须含 `(0,0,0)`」,但 `CompareOccupancy` 只比集合相等,**无执行体**核锚点 | `ColliderFootprintCheck.cs:34` vs `:142-169` | 与「注释不是执行体」纪律同型 |
| **W-4** | **自述强于执行体**:`LevelCiRunner.cs:19` 写「`NotRun` 一定会被打印」,只在 CI 入口成立;导出器 7 处不打印 | `LevelCiRunner.cs:19` | 即 B-1 的措辞面 |
| **W-5** | **C2 的引擎侧输入是 Unity 默认值**(45° / 0.3 m),不是玩家配置 ⇒ 绿只证明「与默认值相等」;烘焙值一改就会**假红** | `LevelCiRunner.cs:144-155` | 方法注已如实登记,故 WARNING |
| **W-6** | `run_ci.sh` **无 CI 调用方**:全仓工作流 / BuildGate 零引用 ⇒ AC-4 字面「**可在** CI 里跑」成立,「**接进** CI」记 **NOT-RUN** | `tools/level/run_ci.sh` | 承本仓「门须有调用方」纪律 |
| **W-7** | **负半边断言弱**:「有声明 / 等高 / 无漂移 / 干净源」四条在被测检查**整体删除**时仍绿(靠正半边配对兜底) | `consistency_c1_c2_c2prime_test.cs:184,202,279,306` | 单条对「检查是否存在」零判别力 |

---

## §三 MINOR(7 条)

| ID | 内容 | 落点 |
|---|---|---|
| **M-1** | `Quantize` 对 **NaN / ±∞ / 溢出**无守卫;`(int)NaN` 为未指定值,若落 0 恰是 `stepOffset` 的**合法值** ⇒ 静默绿 | `LatticeEngineParams.cs:93-104` |
| **M-2** | 足迹「重复项也算不一致」的 doc 只在**声明侧**兑现;目录侧被 `HashSet` 静默去重 | `ColliderFootprintCheck.cs:141-153` |
| **M-3** | `run_ci.sh` 取 Unity 路径用 glob **字典序最后一个**,不校验 `VERSION.md` 钉的 `6000.3.24f1` ⇒ 多版本机器会静默用错引擎 | `run_ci.sh:30-39` |
| **M-4** | 头注称退出码只有 0/1/2,实际 `exit $CODE` 把 137/139 等原样透传(与 `LevelCiRunner` 的三态约定不一致) | `run_ci.sh:58-75` |
| **M-5** | 方法长度超 `coding-standards` 的 ≤40 行:`Check` ≈142 · `LevelSource` 构造 ≈88 · 两个 `TryCheck` 各 56;`Check` 分支 ≈20(>10) | 多件 |
| **M-6** | `test_ac4_injectedC2Defect_exitOne_andNamesCheck` **名字说 C2,实注入 C2'** | 测试文件 |
| **M-7** | `NotRun` 只断**标签**不断**理由**;`Fatal` 只断非空;`Render()` 的告警块与「另有 N 处」截断行从未被渲染过 | 测试文件 `:151,245,296,343` |

---

## §四 两位专家独立命中的同一件事(交叉验证)

| 结论 | `unity-specialist` | `qa-tester` | 编排方复核 |
|---|---|---|---|
| `Terrain == null` ⇒ C2 静默绿 | U-02 | G-01(BLOCKING) | ✅ 属实(`WalkabilityParityCheck.cs:46`) |
| 「没跑」→「绿」的主通道在**调用点**而非三态设计 | U-01(BLOCKING) | — | ✅ 属实(`CheckOrThrow` 7 处) |
| `NotRun` 只断标签不断理由 | — | Q5 | 采信(未复核) |
| 无一条靠 `total=0` 变成 pass(事故在驱动器) | — | Q6 | ✅ 与证伪轮事故记录一致 |

> **交叉验证的价值**:两个 agent 拿到的是**同一份代码**、**不同的评审角度**,
> 却各自独立定位到同一处失效 —— 这比任一单方判定强一档。

---

## §五 OK(已核,明确没问题)

- **级别未搞反**:C1 只进 `Warnings`,C2 / C2' 只进 `Violations`,三态三个独立 readonly list;`IsGreen` 只看 `Violations`。
- **C2 两条判据真独立**:两个独立 `if`、两套报文前缀、两种量子;偏差公式**只有** `IsWithinOneQuantum` 一处。
- **舍入实现正确**:away-from-zero 在 ±0.5 与负半值上都对(缺的是 NaN/∞ 守卫,M-1)。
- **程序集边界干净**:`Editor.Tools.Level.asmdef` = `includePlatforms:["Editor"]` + `references:["Sim","Sim.Contracts"]`,不进构建,无 `Sim → 工具` 反向边,未破门 A / ADR-025 §①。
- **AC-6 回指注记确已落盘**(`adr-015:328` · `adr-016:531`)。
- **0/1/2 在两条入口一致**;`finally` 的 `DestroyImmediate` 在 `Exit` 之前。
- **注入面无需反射**:引擎参数 / 视觉层 / 模块目录 / 采样器四个接缝全可注入。

---

## §六 未及核查项(禁借绿)

1. 本轮评审**未执行**任何测试、**未跑** `run_ci.sh`;证据件里的回归数(`5842/5805/0/37`)与证伪轮 15/15 由实现轮自证,**评审未复算**。
2. `NotRun` 理由文本、`Render()` 告警块、C1 截断行的断言缺失(Q5/M-7)按报告采信,**未逐条复核**。
3. `IModuleCatalog` / `ModuleDefinition.LocalOccupancy` 的源码语义未读(W-3/M-2 按「doc 未被任何执行体核」定级)。
4. C3 路径(`NavMeshReachabilitySampler.For` 在无 NavMesh 时的实际返回)未验。
5. `roadmap` / `session-state` 等文档面不在本次评审对象内。

---

## §七 结论

**CHANGES REQUIRED。** 实现的主体(三态模型、判据独立性、舍入、程序集边界)是干净的;
问题**集中在「报告产出之后,谁来读它」这一层** ——
`LevelCiRunner` 读到了,`CheckOrThrow` 没读。B-1/B-2/B-3 必须同批修。

> ⚠️ **时点关系(如实登记)**:本评审在 story 已被标记 `Complete` **之后**执行。
> 因此「Complete」这一判定是在**未经 code review** 的情况下作出的;
> 本件**不追认**它,也不自动推翻它 —— 处置权归用户(修完重评 / 记债 / 维持)。

---

## §八 修复轮 + 复评结账(2026-10-29 · 同日)

**复评方式**:Phase 7b ② 拆两个 agent —— **A 只核原判据**、**B 只找新缺陷 + 硬约束**，并行。

### 8.1 结账表(Landed / Proven / Branch 三列)

| ID | Landed | Proven | Branch | File(s) | Proven-by(可复算) |
|---|---|---|---|---|---|
| **B-1** 导出门丢弃 NotRun/Warnings | ✅ | ✅ | ✅ 2/2 | `LevelConsistencyCheck.cs:311-336` · `LevelExporter.cs` 7 处 | ① `grep -c "ProbeCurrent())" LevelExporter.cs` ⇒ **7**;`grep -c "CheckOrThrow(src)"` ⇒ **0** ② **F20**(删 NotRun 聚合日志 ⇒ `test_b1_…surfacesNotRun` 红)· **F21**(删告警日志 ⇒ `…surfacesWarnings` 红)· **F19**(日志加 `IsGreen` 守卫 ⇒ `…logsBeforeThrowing` 红) |
| **B-2** `terrain==null` 静默绿 | ✅ | ✅ | ✅ 2/2 | `LevelConsistencyCheck.cs:239-252` · `WalkabilityParityCheck.cs:53-61` | **F16**(关掉该分支 ⇒ `test_b2_…notSilentGreen` 红);另把 `TryCheck` 本体改为**抛**(fail-closed),**F22** 钉住 |
| **B-3** C3 无 NotRun | ✅ | ✅ | ✅ 2/2 | `LevelConsistencyCheck.cs:224-231` | **F17**(删该登记 ⇒ `test_b3_…forC3` 红) |
| **A-残留 ①** 告警日志零断言 | ✅ | ✅ | ✅ | 同上 | **F21** 红 |
| **A-残留 ②** 「日志先于 throw」零断言 | ✅ | ✅ | ✅ | 同上 | **F19** 红(日志被 `IsGreen` 挡住 ⇒ `…logsBeforeThrowing` 的 `LogAssert.Expect` 无人满足) |
| **A-残留 ③** `TryCheck` 本体仍 `return true` | ✅ | ✅ | ✅ | `WalkabilityParityCheck.cs:53-61` | **F22** 红(`Assert.Throws` 落空) |
| **B-3'** 探针每次新建 + 场景假脏 | ✅ | ✅ | — | `LatticeEngineParams.cs:92-118` | domain 级缓存 + `HideFlags.HideAndDontSave`;**F23**(对调两字段 ⇒ `…ExactEngineDefaults` 红)证明精确值断言有效 |
| **B-4** 导出日志无上限 | ✅ | ✅ | — | `LevelConsistencyCheck.cs:317-330` | 三类改为**每类一条聚合行**;`Unimplemented` 死循环已删 |
| **B-6** 探针断言过弱 | ✅ | ✅ | — | `test.cs` `…ExactEngineDefaults` | 由 `>0` 改为 `Is.EqualTo(4500/300)`;**F23** 红 |
| **B-7** `<remarks>` 判别力自述为假 | ✅ | ✅ | — | `test.cs` `…surfacesNotRun` 的 remarks | 已改写,并把判别力移交给 `…logsBeforeThrowing`(F19 钉住) |
| **B-8** `Unimplemented` 死代码 | ✅ | — | — | `LevelConsistencyCheck.cs:321` | 已删;无判据(它本就恒空) |
| **B-9** 探针两份实现 | ✅ | ✅ | — | `LevelCiRunner.cs:142-158` | 改为调 `ProbeCurrent()`;`grep -c "new GameObject(\"LevelCiRunner" ⇒ **0** |
| **B-5** `Check` 方法 163 行 > 40 | ✅<br>**(2026-10-29 单开轮)** | **✗**(等价性按构造) | — | `LevelConsistencyCheck.cs` 拆为 11 个方法 | 原记「**未拆分**。如实登记为未结(见 §九)」—— **原文不删**。2026-10-29 单开轮已拆:163 → **最长 35 行**;补 1 条夹具钉住拆分引入的结构风险,反证 = 删 ③ 早退 ⇒ `…_doesNotIndexWalkable` 红(**`10 / 9 / 1`**)。<br>⚠️ `Proven` **刻意不标 ✅** —— 纯重构旧新值相等,**其余 10 个方法的等价性没有机器判据**(本仓 `feedback_refactor_falsifiability_ceiling`)。原件 = `impl-level-tool-check-split-2026-10-29.md` |

### 8.2 对复评 B 的 BLOCKING 的驳回(**有实跑反证**)

复评 B 判 `test_b1_checkOrThrow_surfacesNotRun_toCallerAndLog` 会红(3 条 `NOT-RUN` 日志 vs 1 条 `Expect`)。

**驳回依据 = 实跑**:该用例在修复轮全量里执行过 —— LevelTool 子集 `122 / 120 / 0 failed`、
全量 EditMode `5846 / 5809 / 0 failed`,均**包含**该用例且**未红**。
⇒ UTF 只在**未预期的 Error/Exception/Assert** 上判红;多余的同类型 `Log` 不判。
复评 B 自陈「静态分析,未实跑」—— 本条正是「静态推演过头」的实例。
**降级为不成立**,但把它的**顾虑**吸收为 B-4(日志噪声)的处理。

### 8.3 证伪总账

| 轮 | 变异 | 结果 |
|---|---|---|
| 首轮 | F1–F15 | **15/15 红**(F13 首轮假存活 → 修夹具 → 红) |
| 修复轮 | F16–F18(B-1/B-2/B-3) | **3/3 红** |
| 加固轮 | F19–F23(顺序 / 两类日志 / fail-closed / 精确值) | **5/5 红** |
| **合计** | **23 条** | **23/23 红,零假存活(终态)** |

### 8.4 回归(三元组:2026-10-29 07:30 CST · 修复+复评轮 · `4ecf2e54`+工作树)

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode 全量 | **`5849 / 5812 / 0 / 37`** | 首轮基线 `5842 / 5805 / 0 / 37` ⇒ **+7 = 新增 7 条夹具**,failed 0 → 0 |
| LevelTool 子集 | **`125 / 123 / 0 / 2`** | — |
| 端到端 CI `run_ci.sh` | **退出码 0** | 导出路径接了探针后仍绿;注入探针见 §四 |

### 8.5 复评后仍**未结**的项(如实登记,不藏)

1. **`Check` 方法仍 163 行 > 40**(`coding-standards` 的硬界)。**未拆** ——
   拆它是纯重构,与本轮「堵假绿」的目标无关,且会动到 7 处导出门的公共路径;
   建议**单开一件**做,不与修复混批(混批会让证伪窗口覆盖两件事,归因变难)。
   > 🔁 **2026-10-29 单开轮已结(原文不删)**:`Check` **163 → 拆为 11 个方法,最长 35 行**
   > (`CheckSlotCell`)。回归 EditMode `5850 / 5813 / 0 / 37`(较本表基线 **+1** = 新增夹具)、
   > PlayMode `238 / 234 / 0 / 2 / 2`(逐项相同)。**等价性 ≠ 已证**(纯重构无新属性可证伪),
   > 只钉住了拆分**引入**的那一处结构风险。原件 =
   > `production/qa/evidence/impl-level-tool-check-split-2026-10-29.md`。
2. **导出路径仍不跑 C3、也不跑 C2' 的目录半边**(`CheckOrThrow` 内 `navSampler`/`moduleCatalog` 恒 null)。
   现在**会打日志说明「没跑」**(不再是静默),但「导出前置门实际不验 C3」这条事实成立。
   归 **W-2 / W-6** 的同族,需 `run_ci.sh` 真接进工作流后一并解决。
3. **`run_ci.sh` 仍无 CI 调用方**(复评 W-6 / U-21)—— AC-4 的「接进 CI」记 `NOT-RUN`。
