# sprint-20 收口遗留五项 —— 卡点分类与解法

**日期**:2026-10-27
**范围**:sprint-20 收口小结里列出的五条「诚实边界」
**目的**:把「未完成」拆成**可执行的卡点分类**,每项点明 **卡谁 / 谁解 / 前置是什么 / 现在能不能做**。

> ⚠️ 本件**不解除任何一条**。分类 ≠ 修复。凡标 ✅ 的是本轮真跑过的,其余按 `NOT-RUN` 读。

---

## 摘要:五项的卡点分四类

| # | 项 | 卡点类型 | 卡谁 | 现在能做吗 |
|---|---|---|---|---|
| 1 | `IsComplete` 恒 false(R15–R18) | **外部依赖 · 跨系统载荷位缺失** | 18 / 23 / 25 / 27 / 29 + ADR-009 | ❌ 不能 —— 四个 owner 各有自己的轮次 |
| 2 | 「玩家看得到库存」未接线 | **同 epic 内的下一件(未开工)** | story-006(20b) | ⚠️ 可开工,但被 R9 半阻塞 |
| 3 | 五个函数零生产调用点 | **分两半:一半同 #2,一半等 42** | 20b / 42 | ❌ 不能 —— 消费者还不存在 |
| 4 | 构建期 `RunAll` 是否真执行 | **判据欠跑 · 本轮可解** | 无(内部) | ✅ **本轮已做完** |
| 5 | `AC-18-15` 不勾 | **判据面差异 + 环境缺格** | processing / ADR-012 | ❌ 不能 —— processing 侧零黄金夹具 |

---

## #1 · `IsComplete` 恒 false —— **外部依赖,20 无权自解**

### 事实

`InventoryFold.IsComplete => Gaps.Count == 0`。而 `Gaps` 的四类里,有三类**必然**出现:

| 缺口 | 触发条件 | 载荷缺什么 | owner |
|---|---|---|---|
| **R16** `StructurePlacementWithoutActor` | 流里**每条** `StructurePlaced` | `StructurePlacedPayload` 五字段无 **actor 位** | 23 + 用户(口径裁) |
| **R17** `StructureRemovalWithoutRefundIdentity` | 流里**每条** `StructureRemoved` | 三字段,返还物**既无 `instance_id` 也无 `item_key`** | 23 + ADR-009 |
| **R18** `InstanceWithoutQualitySource` | 任何**非 17 来源**的实例 | `DropSpawnedPayload` 四字段**无 `quality` 位** | ADR-009 / 25 / 27 / 29 |

**复算**:
```bash
cd unity && sed -n '412,455p' Assets/Sim/ItemDatabase/InventoryFold.cs
# 实测:StructurePlaced / StructureRemoved 两个 case 无条件 gaps.Add(...);
#       尾段 ① 对每个「无品级来源」的活行 gaps.Add(InstanceWithoutQualitySource)
```

### 卡点性质

**这不是「20 没做完」,是「载荷位物理上不存在」**。GDD `inventory-and-items.md:930-950` 的
R15–R18 段自己就写明:

> R15–R18 是**载荷里根本没有这个字段**,性质不同 —— 它们**不是「构造合法占位就能跑通」**,
> 任何占位都等于**发明一个生产端永不发出的字段** ⇒ 跑通的只是夹具,不是链路(假绿形态第 4 变体)。

⇒ **给 20 补任何东西都改变不了 `IsComplete`**。要改它,必须动 18/23/25/27/29 的**载荷定义**
(即 ADR-009 + `entities.yaml`),那是四个不同系统的轮次。

### 可做 / 不可做

- ❌ **不可做**:让 `IsComplete` 在真实存档上变 true —— 缺的字段不存在。
- ✅ **已做**:让它在缺字段时 **fail-loud 而不是静默** —— 这正是当前行为(`Gaps` 逐条具名,
  `RequireComplete()` 会抛)。**这说明「缺口登记」机制是好的**。
- 💡 **唯一可提前做的**:R16 的 **(a) 案** —— 用户裁一句
  「P0 单机 ⇒ 建造扣料/返还恒归该唯一玩家」,把它从「推断」升级为「裁定」。
  这**不新建载荷位**,只给 `StructurePlacementWithoutActor` 一个 P0 豁免依据。
  ⚠️ 但即便如此,`IsComplete` 仍**不会**变 true(R17/R18 还在)。

**归属**:跨系统协调,归 `producer` 或用户裁定;20 侧**零可做项**。

---

## #2 · 「玩家看得到库存」未接线 —— **同 epic 的下一件,已存在**

### 事实

20b 呈现侧 = **story-006**(`production/epics/inventory-items/story-006-medicine-case-ui-walkthrough.md`)。

```
Status: Ready
Layer: Presentation
Type: UI
Estimate: 6h
Last Updated: 2026-09-28
```

**⚠️ 订正我此前的说法**:我两次写成「20b 呈现侧零改动」,容易被读成「20b 还不存在」。
**实际是 story-006 早就存在、状态 `Ready`、只是没开工。** 这两件事的处置完全不同 ——
前者要新建 story,后者只要排期。

**复算**:
```bash
sed -n '3,10p' production/epics/inventory-items/story-006-medicine-case-ui-walkthrough.md
# 实测:Status: Ready / Layer: Presentation / Type: UI / Estimate: 6h
grep -c "story-006" production/sprint-status.yaml
# 实得 0 —— 未入任何 sprint
```

### 卡点性质

**不是缺失,是未排期。** 但它自己被 R9 半阻塞:

> `BLOCKED-BY-R9`:`COLUMNS` / `ROWS_PER_PAGE`(布局参数)归 42,而 42 侧
> **从未接受**「20 = 布尔语义 + `{carry_load, cap}` 整数对」这个口径
> (`skeuomorphic-ui.md:1118` 仍写「载重归 20/21,登记待裁」)。

⇒ story-006 的**走查半边**(AC-20-20 / AC-20-23)等 42 的 R9 回写;
**但落位纯函数半边可以现在做**(story-003 已交付「给定 COLUMNS 的落位函数」)。

### 可做 / 不可做

- ✅ **可做**:story-006 的**非布局**部分 —— 药箱面板骨架、四档呈现函数
  (`slack = cap − carry_load` 的单调不增映射,阈值是 42 侧旋钮但**函数形状可先定**)。
- ❌ **不可做**:完整的翻页走查 —— 等 R9 的 `COLUMNS` / `ROWS_PER_PAGE`。
- ⚠️ **Engine 风险 HIGH**:ADR-013 §6.6 假设 6(UI Toolkit 运行时手柄焦点导航)**spike 未跑**,
  而 story-006 明文写「原型 spike 为前置」。

**归属**:排期决策归 `producer`;R9 回写归 42 轮。

---

## #3 · 五个函数零生产调用点 —— **两半,一半等 #2,一半等 42**

### 事实(逐个复算过,不是并成一串)

| 函数 | 生产调用点 | 它的消费者是谁 |
|---|---|---|
| `StackingRules.StacksOf` | **0** | story-006(摆放序 = 药箱落位输入) |
| `ContainerWeightFold.FoldWeight` | **0** | ⚠️ 见下方「第二真源」警告 |
| `CarryLoadSolver.Ratio` | **0** | story-006(出 `CarryRatioDto`) |
| `CarryLoadSolver.MinWeight` | **0** | 42(AC-20-18 的 `W_MIN`) |
| `CarryLoadSolver.IsFullForAnyAddition` | **0** | 42(AC-20-18 满档谓词) |

**复算**:
```bash
cd unity
for s in StacksOf FoldWeight Ratio MinWeight IsFullForAnyAddition; do
  echo "=== $s ==="; grep -rn "\b$s\s*(" Assets --include=*.cs | grep -v "^Assets/Tests/" | grep -v "///"
done
# 实测:每个都只命中定义行本身,零调用
```

### 卡点性质 —— **两种,不能并成一串**

**类型 A(3 个)**:`StacksOf` / `Ratio` / `MinWeight` / `IsFullForAnyAddition`
= **消费者还没写**。它们的调用点会随 story-006(20b)和 42 的 R9 回写自然出现。
**这不是缺陷,是正常的「先做被依赖件、后做消费件」顺序。**

**类型 B(1 个):`FoldWeight` —— 这条不是「等消费者」,是「两份实现 + 一个缺载体」**

评审 `review-s20-11` W-2 抓的是:折重口径在工程里**有两份公式体**:

1. `ContainerWeightFold.FoldWeight`(`:104-133`)= 空箱自重 + `Σ(子件 UnitWeight × Qty)`,**零调用**
2. `CarryLoadSolver.SumDetailed`(`CarryLoad.cs:243-280`)= 本行重量 + 逐子件并入,**有生产调用点**

**复算**:
```bash
cd unity
grep -rn "FoldWeight(" Assets --include=*.cs | grep -v "^Assets/Tests/" | grep -v "///"
# 实得:仅 ContainerWeightFold.cs:104 定义行 ⇒ 零调用
sed -n '243,280p' Assets/Sim/ItemDatabase/CarryLoad.cs   # SumDetailed 的并行实现
```

**但往下追一层,会发现它们今天不可能分叉 —— 原因不是「口径已统一」,是「子件面在 P0 是空的」**:

| 事实 | 复算 | 含义 |
|---|---|---|
| `Sum` 生产调用**恒传 `childrenByContainer = null`** | `CarryLoad.cs:212` 是唯一的生产调用点,第四参写死 `null` | 生产路径**从不并入子件** |
| P0 **无「这是容器吗」的判据载体** | `grep -rn "is_container\|IsContainer" unity/Assets --include=*.cs` → 命中**仅 `Tests/EditMode/Inventory/stacking_container_rules_test.cs`**(测试里讨论它「不存在」);生产侧零命中。GDD `:578-580` 自陈 | 即使想传子件表也**不知道哪些是容器** |
| `children` 在 P0 **并非恒空**(2026-10-26 订正) | GDD `:568-575`:「禁嵌套容器」= 不含**容器型**子件,不是不含任何子件 | 子件是设计上存在的,只是今天无载体 |

⇒ **真实卡点 = `BLOCKED-BY Story 010`**(容器的 `is_container` / `ItemCategory.container` 载体)。
在它落地前:

- `FoldWeight` 无法接线(**不知道怎么识别容器**);
- `SumDetailed` 的子件分支在生产路径上**永远不会被走到**(恒 null);
- 两份实现**今天结果必然相同**(都是「只管本行重量」)⇒ **不构成口径漂移**,W-2 的
  「当前两侧口径一致」判断成立,但它的一致**是退化的**,不是设计出来的。

### 可做 / 不可做

- ⚠️ **可做但不该做**:现在就把 `FoldWeight` 接成 `SumDetailed` 的容器分支实现 ——
  **没有容器判据载体,接了也是死路**(调用方拿不到 `childrenByContainer`)。
  这与 S20-10 刚修掉的 `BuildPartQualityGate` 零调用点是**同一形态**:
  **硬造调用点 = 假接线**。
- ✅ **可做的只有一件**:把「两份实现」这件事**登记为 `BLOCKED-BY Story 010`**,
  而不是登记成「20 待办」—— **归属写错会让人以为 20 自己能解**。
- ❌ **不可做**:给 A 类三个函数造调用点 —— 消费者不存在。

**归属**:类型 B 的真实归属 = **Story 010(容器判据载体)**,不是 20 下一轮。
类型 A 归 story-006 + 42。

---

## #4 · 构建期 `RunAll` 是否真执行 —— ✅ **本轮已解,探针轮做完**

### 原状态

story-003 已知边界 6 原文:

> `RunAll` 在构建期是否真的执行 = **NOT-RUN** —— 本件只证明了「它被接进 `AssemblyGates.RunAll`」
> 且**测试侧直调绿**(`assembly_gate_runall_test.cs:31`)。**构建期**这一处按
> `CLAUDE.md` §BuildGate「构建绿 ≠ 门跑了」口径**未做探针轮**。

### 本轮做法(承 `CLAUDE.md` §BuildGate 三件套)

在 `InventoryDtoFloatGate.RunAll()` 开头插一条**无条件失败项**并提前 `return`,
走 **player 构建路径**(`-runTests -testPlatform StandaloneLinux64` —— 该路径下
TestRunner 调 `BuildPipeline.BuildPlayer`,必然触发 `OnPreprocessBuild`)。

### 实测(三件套逐条)

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串出现在**构建日志**里 | ✅ `Logs/build-probe-invdtogate3.log:2314` — `[PROBE-InventoryDtoFloat] 探针轮 —— 本门在构建路径被执行` |
| 2 | 栈帧上是目标类的 `OnPreprocessBuild` | ✅ `DaYiJingCheng.EditorTools.Gates.AssemblyGates/BuildGate:OnPreprocessBuild (BuildReport) (at Assets/Editor.Tools.Gates/AssemblyGates.cs:1310)`,其下游链 = `BuildPipelineInterfaces:OnBuildPreProcess` → `BuildPipeline:BuildPlayer` |
| 3 | 构建**因它失败** | ✅ `BuildFailedException: [AssemblyGates] 构建前门失败 1 条(...)` · `Build Finished, Result: Failure.` |

### 还原判据 = SHA-256 逐字节

```
变异前: 2ce7e36668becd0917973ba5827aaeb4643fc4b4d5209ab37eea8d3f4569ce70  Assets/Editor.Tools.Gates/InventoryDtoFloatGate.cs
还原后: 2ce7e36668becd0917973ba5827aaeb4643fc4b4d5209ab37eea8d3f4569ce70  ← 逐字节相同
探针残留: grep -c "PROBE" → 0
git diff --stat  → 空(文件回到 HEAD 状态)
```

**未用** `git checkout` / `git stash`(承「证伪还原禁用 checkout」纪律),用 `cp` 回 + 哈希比对。

### ⚠️ 归因三件套的一处辨析(不隐瞒)

日志里探针串出现**两处**,栈帧不同,必须分清:

| 位置 | 栈帧 | 是什么 |
|---|---|---|
| `:484` | `AssemblyGates/<>c:<RegisterReloadHook>b__49_0` | **reload 钩子**路径,不是构建 |
| `:2314` | `AssemblyGates/BuildGate:OnPreprocessBuild` | ✅ **构建路径 —— 本项要证的就是这条** |

⇒ 「探针串出现」本身**不够**,必须看栈帧。第 484 行若单独看会得出错误结论。

### 干净轮(还原后确认门链正常,不是被探针弄坏)

`unity/Logs/build-clean-invdtogate.log`:

```
Build Finished, Result: Success.
grep -cE "PROBE-InventoryDtoFloat|BuildFailedException|error CS"  →  0
```

⇒ **「门跑了且零违例」**,不是「门没被调用」(探针轮已排除后者)。

### 结论

**「构建期 `RunAll` 真的执行」= 已证。** story-003 已知边界 6 已就地结案
(见 `story-003-stacking-container-weight-fold.md` 该条下的 2026-10-27 结案注)。

**⚠️ 本项只解 `InventoryDtoFloatGate` 这一道门**,不自动推广到 `RunAll` 里的其余 14 道门
—— 它们共享同一个 `OnPreprocessBuild` 调用点(栈帧已证),**但各自的判据体是否被走到未逐条探针**
(例如某门自己内部提前 `return`)。按「未见不声称」:本条只对被测的那一道门成立。

---

## #5 · `AC-18-15` 不勾 —— **判据面差异 + 环境缺格,两条独立理由**

### 事实

`AC-18-15` 要求:同一 `(recipe, L, 输入实例集, EquipMod, 两 EnvMod)` ⇒ **三格重放,三出参逐位相同**。

我的两格矩阵(`tools/determinism-matrix/`,实跑 `exit 0`)**测的不是它**。

### 两条理由(缺一都够)

**理由 A · 判据面差异**
矩阵测的是 **item-database 的 19 条黄金哈希**(`AC-21a-28/29` 面)。
processing 的三出参(`ActualConsumed` / `OutputQuality` / `ActualProduced`)**零黄金夹具**。

**复算**:
```bash
cd unity
grep -rln "golden\|Golden" Assets/Tests/EditMode/Processing/
# 实得:零命中
grep -rln "golden\|Golden" Assets/Tests/PlayMode/
# 实得:determinism_golden_crossplatform_test.cs —— 但看其头注:
head -3 Assets/Tests/PlayMode/determinism_golden_crossplatform_test.cs
# 「权威来源:Story 011 AC-21a-29」→ 同样不是 processing
```

**理由 B · 环境缺一格**
ADR-012 判据 = **三格齐 + 常驻 + 版本化刷新协议**。现状 = 两格(Mono / IL2CPP-x64)+ 缺 ARM64
+ 非常驻 + 无刷新协议。

**复算**:
```bash
grep -n "Linux-ARM64-IL2CPP\|三格全免费常驻" docs/architecture/adr-012-cross-platform-determinism-ci-gate.md | head -3
# 实得:三格定义 = Linux-x64-Mono + Linux-x64-IL2CPP + Linux-ARM64-IL2CPP(交叉构建)
```

### 卡点性质

**理由 A 归 processing 实现轮** —— 需要先给 processing 的三出参建黄金夹具(19 条那种形态),
矩阵工具才能复用它跑。**这是可做的,但不在 20 的域内。**

**理由 B 归 ADR-012 / CI 轮** —— ARM64 是环境问题(`BuildTargetProbeCli` 探针,
本机实测无 ARM64 目标),需要 CI runner 或交叉构建工具链。

### 可做 / 不可做

- ❌ **20 侧零可做项** —— processing 夹具归 processing,ARM64 归 CI。
- ✅ **已做**:把未勾语义从「矩阵不存在」精确到「两格可重跑、只差第三格与常驻」。

**归属**:processing 实现轮(理由 A)+ ADR-012/CI 轮(理由 B)。

---

## 汇总:可做项清单

| 项 | 可做 | 归谁 | 规模 |
|---|---|---|---|
| #4 探针轮 | ✅ **本轮已做完**(探针轮 + 干净轮双绿) | — | — |
| #1 R16 的 (a) 案口径裁定 | ✅ 可提前做 | 用户一句话 | 小 |
| #2 story-006 非布局部分 | ⚠️ 可开工 | producer 排期 | 6h 的一部分 |
| #3 类型 B `FoldWeight` 双实现 | ❌ 等容器判据载体 | **Story 010** | 中 |
| #2/#3 完整呈现侧 | ❌ 等 R9 回写 | 42 | — |
| #1 R17/R18 | ❌ 等载荷位 | 23 / 25 / 27 / 29 / ADR-009 | 大 |
| #5 | ❌ 等 processing 夹具 + CI | processing / ADR-012 | 大 |

**一句话**:五项里 **1 项本轮已解(#4,双轮验证)**、**1 项有一句话级别的可提前动作(#1 的 R16 口径)**、
**其余全是其他系统的轮次**。**没有任何一项能靠 20 单方推平。**

⚠️ **本轮的两次自我纠错值得记下**(都在本件写作过程中发生):

1. 我先前把 `CanCarry` 列进「零调用点」串 —— 实测 **3 处**生产调用点。已订正。
2. 我一度判断「评审 W-2 的措辞有偏差」—— **错**,重读评审原文(`review-s20-11:158-167`)
   发现它写的就是 `FoldWeight` vs `SumDetailed`,与实测一致。**已收回该判断**。

⇒ 这两次都印证同一件事:**把几个名字并成一串再下结论,是最容易出错的地方**。
