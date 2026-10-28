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
