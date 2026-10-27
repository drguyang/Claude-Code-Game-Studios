# S20-11 评审原件 —— inventory-003 堆叠 / 容器 / 折重

**评审日期**: 2026-10-27
**评审性质**: **补做评审**（QA 签核 C-4）。⚠️ 按 `.claude/docs/review-workflow.md` §不可追补：
本报告评的是**当下代码**，**不追认** story 收口时的任何判定。
**对象**：

| 文件 | 行数 | 性质 |
|---|---|---|
| `unity/Assets/Sim/ItemDatabase/StackingRules.cs` | 255 | 堆叠视图 + 摆放序（门 A 纯 C#） |
| `unity/Assets/Sim/ItemDatabase/ContainerWeightFold.cs` | 132 | 折重算术核 |
| `unity/Assets/Sim/ItemDatabase/BuildPartQualityGate.cs` | 87 | P0 范围门 ③ |
| `unity/Assets/Editor.Tools.Gates/InventoryDtoFloatGate.cs` | 517 | AC-20-15 双层门（EditMode，不进构建） |

**测试件**：`unity/Assets/Tests/EditMode/Inventory/stacking_container_rules_test.cs` — 736 行 ·
**33 个 `[Test]`** · **0 个 `[Ignore]`**（实测 `grep -c` 见下）

> 复算命令（可直接跑）
>
> ```bash
> cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
> # ① 用例数 / Ignore 数
> grep -c "public void test_" unity/Assets/Tests/EditMode/Inventory/stacking_container_rules_test.cs   # 33
> grep -c "\[Ignore"       unity/Assets/Tests/EditMode/Inventory/stacking_container_rules_test.cs   # 0
> # ② StackKey 算式定义点（AC-20-04 的唯一出处判据）
> grep -rn "StackKey =>" unity/Assets/Sim unity/Assets/Sim.Contracts   # 恰 1 行(InventoryFold.cs:73)
> # ③ CanStack 是否真被调用（story 自述的证伪点）
> grep -n "StackingSolver.CanStack" unity/Assets/Sim/ItemDatabase/StackingRules.cs   # :155 为调用行
> # ④ 三件是否被生产侧调用（死代码判据）
> for s in StacksOf PlacementOrder BuildPartQualityGate FoldWeight; do
>   echo "--- $s"; grep -rn "$s" unity/Assets --include=*.cs | grep -v /Tests/
> done
> # ⑤ 门 A
> grep noEngineReferences unity/Assets/Sim/Sim.asmdef        # true
> # ⑥ 门的构建期接线点
> grep -rn "InventoryDtoFloatGate.RunAll" unity/Assets/Editor.Tools.Gates/AssemblyGates.cs   # :183
> ```

**权威件**：
- `design/gdd/inventory-and-items.md` 规则三（`:228-262` 堆叠键 / BL-7③ / 推荐 4 折重口径）·
  规则十（`:552-575` 摆放函数 / 容器契约 / 2026-10-26 加注的「不含容器型子件」口径）
- `docs/architecture/adr-006-fixed-point-boundary-contract.md` §Decision 一（D-21-17：`weight` / `stack_max`
  是 **int 计数**，移出 `Fix` 解析集；单一舍入 `ROUND_HALF_AWAY_FROM_ZERO`）
- `docs/architecture/adr-005-deterministic-sim.md` §Decision（纯函数 · 整数域 · 可重放）
- `docs/architecture/adr-025-contract-assembly-manifest.md` §①（`Sim` 引用集恰 = `{BCL, Sim.Contracts}`）
- `docs/architecture/adr-014`（烘焙管线）· `adr-013`（容器界面非网格）

---

## 一、AC 结账

| AC | 载体 | Landed | Proven | Proven-by（可复算判据） |
|---|---|---|---|---|
| **AC-20-04** 堆叠键唯一 = `(item_key, quality)` | `StackingRules.StacksOf`（`StackingRules.cs:125-200`） | ✅ | ✅（部分） | ① `test_ac2004_sameKeySameQuality_merges_othersStaySeparate`（`:102`）钉 key×quality×stack_max 三轴与 id 升序；② `test_ac2004_stackKeyExpression_definedExactlyOnce`（`:135`）以**字面量** `"StackKey =>"` 扫源计数 = 1（`grep -rn "StackKey =>" unity/Assets/Sim unity/Assets/Sim.Contracts` ⇒ 仅 `InventoryFold.cs:73`）；③ `test_ac2004_overflow_spillsToNewStack_quantityConserved`（`:165`）Σqty 守恒；④ `containerWithChildren_neverMerges`（`:197`）/ ⑤ `stackMaxOfOne_neverMerges`（`:215`）。**「20 不得改写公式体」由 `StackingRules.cs:155` 的真调用兑现**（`grep -n` 命中调用行，非注释行） |
| **AC-20-12** 非网格 / 零排序 / 零自动整理 | `StackingRules.PlacementOrder`（`:218-233`） | ✅ | ✅ | ① `test_ac2012_signature_cannotSeeAttributesOrTime`（`:267`）断言参数类型**恰** `{IReadOnlyList<long>, int}`（能力断言，字面量类型对象，非自指）；② `shuffledInput_yieldsIdenticalOrder`（`:230`）乱序喂入同序；③ `layout_isRowMajorByColumns`（`:249`）逐格 row/col 字面量；④ `nonPositiveColumns_throwsInsteadOfDegrading`（`:285`）；⑤ `emptyBox_yieldsEmptyOrderNotNull`（`:294`） |
| **AC-20-15** 输出 DTO 递归零 float | `InventoryDtoFloatGate`（517 行，双层） | ✅ | ✅（判别力已证；**构建期执行 NOT-RUN**） | ① 层 1：`layer1_recursiveReflection_noFloatFields`（`:525`）；负样例 `:554` / 泛参 `:566` / 枚举假阳性 `:581`；② 层 2：`layer2_ilScan_noFloatOpcodesOrTypes`（`:593`）、负样例 `:620`、空清单 `:644`、**清单非空但零命中** `:659`、扫描面守卫 `:676`。接线：`AssemblyGates.cs:183` + `gate_wiring_manifest_test.cs:287`（`WiringKind.Chain`）。⚠️ 构建期是否真跑 = **NOT-RUN**（story 已如实登记，本报告不借绿） |
| **折重不变量**（OQ-20-4） | `ContainerWeightFold.FoldWeight`（`:104-130`） | ✅ | ✅（口径）/ ⚠️ **未接线** | ① `foldWeight_isEmptyContainerPlusChildrenSummed`（`:307`）字面量 18；② `removeThenRefold_isBitIdentical`（`:339`）**已补独立字面量锚 40**（原自指夹具已修，见源码 `:331-338` 的订正注）；③ `addingChild_addsOnlyChildWeight`（`:364`）增量 12 = 新子件重；④ `emptyChildren_equalsSelfWeight`（`:322`）；⑤ `nullChildren_throws…`（`:385`）；`instanceWeight_*`（`:377` / `:394`）。⚠️ **`FoldWeight` 零生产调用点**（见 W-2） |
| **P0 范围门 ③** `build_part` quality ≡ 1 | `BuildPartQualityGate`（`:50-71`） | ✅ | ❌ **仅单元级** | 夹具：`nonBuildPart_anyQualityPasses`（`:405`）/ `buildPartQualityNotOne_reportsViolation`（`:417`）/ `buildPartQualityOne_passes`（`:431`）/ `requireValid_throwsOnViolation`（`:443`）。**但 `grep -rn "BuildPartQualityGate" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 仅 `BuildPartQualityGate.cs:30` 自身的类声明** ⇒ 无任何生产/构建期调用点。AC 要求的「不变量破坏 = 构建失败」**不成立**（见 B-1） |
| **P0 范围门 ①②** 容器 qty / quality ≡ 1 | 无 | ⛔ 不勾 | ⛔ | 卡面已挂 `BLOCKED-BY Story 010`；本报告核对属实（`ItemInstance` 无 `is_container`，`ItemCategory` 无 `container` 值）—— 按缺口登记，不记绿 |
| 21a 前哨（非本件 AC） | `ContainerClosure` 既有件 | — | ✅ | `nestedContainer_rejectedByExistingClosureValidator`（`:491`）+ 对照组 `:508` |

**AC 结账小结**：6 条中 4 条真正成立（AC-20-04 / 12 / 15 / 折重口径），1 条**只在单元级成立、执法面缺失**（P0 门 ③），1 条已正确挂 BLOCKED。

---

## 二、发现

### 2.1 BLOCKING

**B-1 · P0 范围门 ③ 的执法件是纯孤儿：AC 记绿所依据的「机器断言」不存在**

- **事实**：`BuildPartQualityGate.Validate` / `RequireValid` 在**生产代码里零调用点**
  （全仓 `grep` 只命中类自身声明 `BuildPartQualityGate.cs:30`）。它也不在 `AssemblyGates.RunAll`
  门链里，不在 `gate_wiring_manifest_test.cs` 的 `EntryWiringManifest` / `ClassVerdict` 表里
  （该 `grep` 对 `BuildPartQualityGate` 零命中，对 `InventoryDtoFloatGate` 有 2 处命中）。
- **与 AC 的冲突**：story `:75` 该 AC 写明「三条各立机器断言（**不变量破坏 = 构建失败**）」，
  且 `:203` 把 5 条夹具记作「P0 门 ③ ✅」。而现状是：**没有任何东西在构建期/装配期调用它** ⇒
  一个 `build_part` 且 `quality = 3` 的实例今天可以自由穿过整个工程。
- **性质**：这正是本项目第五形态「**用一句自述顶替一个从未被调用的门**」（同
  `coding-standards.md` §「某件有门」须给 `file:line` + 可跑命令的立规缘由；`S18-1 AC ⑥` 同款）。
  夹具全绿证明的是「函数正确」，**不是**「不变量被执行」。
- **最小修法**（二选一，均需用户裁）：
  (a) 在 20 的实例装载/投影入口接一次 `RequireValid`（并补一条夹具：注入 `quality=3` 的 `build_part`
      ⇒ 装载失败）；(b) 若 P0 确无装载入口可挂，则把该 AC 从「✅」降为
      「**Landed ✅ / Proven ✗ · 执法面 BLOCKED-BY Story 010**」，与 P0 门 ①② 同档登记。
  **无论哪条，现状的「✅ + 构建失败」表述都不能保留。**
- **复算**：`grep -rn "BuildPartQualityGate" unity/Assets --include=*.cs | grep -v /Tests/` ⇒ 1 行（类声明）。

**B-2 · `InventoryStack.InstanceIds` 的契约在「部分吸纳」时被破坏，且常规数据即可到达**

两处独立失真，均**已离线复算**（命令见下）：

1. **`InstanceIds` 漏记（可达 · 主判据）**：当某实例只有部分件数进得了未满栈时，那些件进了该栈，
   但该栈的 `InstanceIds` **不含这个实例**。`StackingRules.cs:186` 的
   `long[] ids = first ? new[]{inst.InstanceId} : Array.Empty<long>();` 只为**新开的栈**登记 id，
   而溢出分支（`:172-176`）把 `split.CurrentFinalQty` 写回旧栈时**不追加本实例 id**。
   这与 `InventoryStack.InstanceIds` 的文档契约（`:49-52`「本栈吸纳的实例 id」）直接矛盾。
2. **部分栈不回收（附带风险）**：`:188` 只在 `first` 时写 `openByKey[stackKey]`，指向**首个**栈；
   多栈切分后尾部的**部分栈**从未登记为「未满栈」⇒ 下一个同键实例另开新栈而不补满它。

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
SM = 9
def run(seq):
    stacks, open_by, key = [], {}, 1
    for name, q in seq:
        rem, absorbed = q, None
        if key in open_by:
            idx = open_by[key]; cur = stacks[idx][0]; tot = cur + rem
            if tot <= SM:
                stacks[idx][0] = tot; stacks[idx][1].append(name); continue
            stacks[idx][0] = SM; absorbed = SM - cur; rem = tot - SM; del open_by[key]
        first = True
        while rem > 0:
            take = min(rem, SM)
            stacks.append([take, [name] if first else []])
            if first: open_by[key] = len(stacks) - 1
            rem -= take; first = False
        if key in open_by and stacks[open_by[key]][0] >= SM: del open_by[key]
        print(f'  落 {name}(qty={q}, 前栈吸纳={absorbed}) -> {stacks}')
    return stacks
print("B) 普通溢出(open=2, incoming qty=10):"); run([('A',1),('B',1),('C',10)])
print("A) 单实例 qty>stackMax:");              run([('A',12),('B',5)])
PY
```

实测输出：

```
B) 普通溢出(open=2, incoming qty=10):
  落 A(qty=1,  前栈吸纳=None) -> [[1, ['A']]]
  落 C(qty=10, 前栈吸纳=7)    -> [[9, ['A','B']], [3, ['C']]]
A) 单实例 qty>stackMax:
  落 A(qty=12, 前栈吸纳=None) -> [[9, ['A']], [3, []]]
  落 B(qty=5,  前栈吸纳=None) -> [[9, ['A']], [3, []], [5, ['B']]]
```

- **可达性**：**两条路径的强度不同，必须分开说**。
  - **路径 1（`InstanceIds` 漏记）用普通溢出即可到达**，见上面的 **B 组**：未满栈已有 `A(1)+B(1)=2`，
    再落一个 `C(qty=10)` ⇒ `C` 有 **7 件进了首栈**，但首栈 `InstanceIds = ['A','B']` **不含 `C`**。
    不需要任何越界输入 ⇒ **这是常规数据就能走到的失真**。
  - **路径 2（部分栈不回收）在 A 组才显现**（`qty > stackMax`）；21a 若维护「实例 `qty ≤ stackMax`」，
    该组多半不可达 ⇒ 降为**附带风险**，不作主张。
  - **根因是结构性的**：`InventoryStack` 只有 `TotalQty` + `InstanceIds`，**没有逐 id 的分摊量**
    ⇒ 一旦某实例跨两栈，「本栈吸纳了谁多少件」**在该结构上不可表达**。修 B-2 不是补一行
    `ids` 追加就行，须先定分摊表示（`(id, qty)` 对，或禁止跨栈实例进入视图）。
  - **为何现有夹具抓不到**：`test_ac2004_overflow_spillsToNewStack_quantityConserved`（`:165`）的
    序列 `5 / 4 / 1` 恰好在 `4` 处**填满**（9 = stackMax）并清空 `open_by`，`1` 另开新栈 ⇒
    **从未发生部分吸纳**；且它断言的 `CollectionAssert.AllItemsAreUnique(allIds)` 与「漏记」
    恰好**同向**（漏记只会更唯一）⇒ 该夹具对该缺陷**零判别力**。
- **量级**：数量守恒不受影响；受影响的是「按栈取实例 id 集合」的下游（42 药箱逐格渲染、
  Story 006 的落位映射）—— 下游若按 `InstanceIds` 决定「这一格画哪几件」，**会漏画**。
  ⇒ 判 BLOCKING（契约破坏 + 常规数据可达），修法见 `§四` 第 2 条。

### 2.2 WARNING

**W-2 · `FoldWeight` 零生产调用点；OQ-20-4 的折重口径在工程里有两份实现**

- `grep -rn "FoldWeight" unity/Assets --include=*.cs` ⇒ 生产侧**零命中**（仅定义 + 测试）。
- 实际被生产消费的是 `ContainerWeightFold.InstanceWeight`（`CarryLoad.cs:294` / `:315`、
  `IgnitionAdmission.cs:141`）；而「空箱自重 + 子件逐件」这条口径被 `CarryLoad.SumDetailed`
  **另写了一遍**（`CarryLoad.cs:243-280`：`total += 本行重量` 再逐子件 `total += w`，并自带
  `DoubleCountedChildWeight` 分支）。
- 后果：D-1 承诺的「004 直接复用该加法件」在实作里没有兑现；同一口径存在两处公式体，
  正是 ADR 侧反复禁止的「第二真源」形态（口径漂移不可观测）。当前两侧口径一致，
  故不是 BLOCKING，但**story 的 D-1 承诺与代码不符**。

**W-3 · `InstanceWeight` 的溢出 catch 不可达（死代码），doc 声称的异常路径不存在**

`ContainerWeightFold.cs:77`：`checked((long)unitWeight * qty)` —— 显式转 `long` **先于**乘法求值，
故运算在 `long` 域完成（`int × int ≤ 2^62 < long.Max`），`:79-84` 的 `catch (OverflowException)`
**永不触发**。`:66` 的 doc 却把「或乘积超 `long`」列为一条 `InvalidOperationException` 路径。
- 判据：把 `:77` 改成 `checked(unitWeight * (long)qty)` 结果不变；把 `try/catch` 整段删掉，
  33 条夹具**全绿**（无任何夹具打这一面 ⇒ 与 `M14` 同型的「并联双保险只承担零属性」）。
- 处置：删 catch（承 story `:117-124` 对 `IsEnum` 早返回的同一处置逻辑：消除不可响应的冗余），
  或写成能真溢出的形态并补夹具。**不得**保留「doc 声称可抛、夹具永不覆盖」的形态。

**W-4 · `StacksOf` 违反编码标准的长度/复杂度上限**

- `StacksOf` 体 = `:128-199`（**72 行**，扣除 XML 文档后仍远超 40 行标准）。
- 判定点约 13 个（2 个 null 守卫 + `for` + `stack_max` 守卫 + 2 个复合条件 + 2 个 `while`/分支
  + 2 个三元 + `first` 分支 + 末尾复合条件）> 上限 10。
- 且 W-1 的两处失真都在这个长方法体内 ⇒ 拆分（「排序 → 单实例落位 → 未满栈回收」三段）
  同时是复杂度与缺陷的修法。

### 2.3 MINOR

- **M-1 `test_p0Gate_containerInvariants_areNotFakedViaChildrenLength`（`:464`）近乎恒真**：
  它对 `BuildPartQualityGate` 断言「公开面没有名为 `IsContainer` 的方法」—— 一个只有两个方法
  的类上，这条断言不承载「20 侧没有别处私造容器判定」这一属性（别处实现它抓不到）。
  story 已把它登记为反向夹具，但**其判别力应如实降级**，不宜计入 P0 门 ①② 的证据面。
- **M-2 `StackKeyOf`（`:237-238`）以构造 `InventoryRow(0L, ordinal, quality, 0, NoOwner)`
  的方式取键** —— 依赖 `InventoryRow` 构造参数顺序，且每实例分配一次结构体。
  功能正确（单一算式仍只有 `InventoryFold.cs:73`），但把一个「读属性」变成「造对象读属性」，
  属可读性/耦合层面的债，非缺陷。
- **M-3 `StackingRules` / `InventoryStack` 的 `TotalQty` 无上界断言**：文档写「∈ [1, stack_max]」，
  但构造器是 `public`（`StackingRules.cs:60`），任何调用方可造出 `TotalQty = 0` 或超 `stack_max`
  的栈。当前唯一构造者在本文件内（安全），登记为契约面缺口。
- **M-4 无边界夹具**：`qty = 0`（`StacksOf` 入参）、`StackMax` 满档叠加、`FoldWeight` 的
  `emptyContainerWeight = 0` / 负值 —— 前两者无夹具；折重侧仅 `InstanceWeight` 的
  `(0,1)/(1,0)/(-1,5)` 有夹具（`:394`），`FoldWeight(0, …)` 无。
- **M-5 `InventoryDtoFloatGate.RunAll(".")` 依赖 cwd**（`AssemblyGates.cs:183`）。story 已如实
  登记「构建期执行 = NOT-RUN」；本报告不重复扣分，仅确认该登记**必要且仍未结**。

### 2.4 ADR 合规核对（逐条）

| 约束 | 判定 | 证据 |
|---|---|---|
| ADR-006 D-21-17：`weight` / `stack_max` 是 **int 计数**，非 `Fix` | ✅ 合规 | `ItemStackAmount.UnitWeight/Qty` 均 `int`（`ContainerWeightFold.cs:32/:36`）；`def.StackMax` 为 `int`；三件**零 `Fix` 引用** |
| 舍入 `ROUND_HALF_AWAY_FROM_ZERO` | ✅ 不涉及 | 三件唯一的除法是 `i / columns` / `i % columns`（整数落位，`StackingRules.cs:231`），无定点除法、无 `Math.Round` |
| float 泄漏进 sim | ✅ 零泄漏 | `grep -n "float\|double\|decimal\|Math.Round\|BigInteger"` 于三件 ⇒ **仅注释行**（`ContainerWeightFold.cs:16/:93`） |
| 门 A（`Sim` 的 `noEngineReferences`） | ✅ 合规 | `unity/Assets/Sim/Sim.asmdef` ⇒ `"noEngineReferences": true`，`references: ["Sim.Contracts"]`；三件仅 `using System*` + `Sim.Contracts` |
| ADR-014 烘焙管线（配置值不自持） | ✅ 合规 | 三件零硬编码数值（`stack_max` / `columns` / `weight` 全部参数注入；`P0BuildPartQuality = 1` 是**范围门裁定值**非数值旋钮，源码 `:32-33` 已声明） |
| ADR-005 纯函数 / 可重放 | ✅ | 三个 static 类，无可变静态、无墙钟、无随机、无 I/O |
| ADR-025 §① 装配边界 | ✅ | 见门 A 行；`InventoryDtoFloatGate` 住 `Editor.Tools.Gates`（不进构建） |
| 整数溢出纪律（ADR-012 F7 同型） | ⚠️ 部分 | 折重侧 `checked` 齐全（`:77` / `:120`，但 `:77` 的 catch 不可达 = W-3）；`StacksOf` 内 `int` 加减**无** `checked` / 显式上界（`remaining -= take`、`split.CurrentFinalQty`），实际由「入参为 `int` 且 `take ≤ stackMax`」保证不溢出 —— 属可接受但**未声明**的隐含前提 |

---

## 三、缺口登记（不掩盖）

| # | 缺口 | 归属 | 状态 |
|---|---|---|---|
| G-1 | P0 范围门 ③ 无执法点（B-1） | 本件 | **open · BLOCKING** |
| G-2 | P0 范围门 ①② 无判据载体 | Story 010 | 已挂 `BLOCKED-BY`，核对属实 |
| G-3 | `StacksOf` / `PlacementOrder` **零生产调用点**（消费方 004 / 006 均未接） | Story 004 / 006 | open —— 按卡面 `Unlocks` 登记，属预期未接；但「AC-20-12 已生效」只能在单元级成立 |
| G-4 | `FoldWeight` 零生产调用点（W-2） | Story 004 | open |
| G-5 | AC-20-22（品级不可分视觉）无专用夹具 | AC-20-22 自身实现轮 | 已登记 `NOT-RUN` |
| G-6 | 构建期 `RunAll` 是否真执行 | 本件 / 门链 | `NOT-RUN`（需探针轮，判据见 `CLAUDE.md` §BuildGate） |
| G-7 | AC-20-15 层 2 只在 Mono 后端反证 | ADR-012 三格矩阵 | 已登记 |
| G-8 | `AllowedFloatMembers` 从「具名成员」放宽到「整个类型」无夹具 | 本件 | 已登记（story `:355-356`），核对属实 |
| G-9 | 跨栈 `InstanceIds` 记账失真（W-1）无夹具 | 本件 | open · WARNING |

### 未及核查项（如实登记）

- **未重跑夹具与全量回归**（本报告只做静态核查 + 离线复算）：33/33 绿、5300/5265/0/35 与
  `14/14` 证伪轮**均照 story 记录引用，未在本次复跑**（引用自
  `story-003-stacking-container-weight-fold.md` @ 2026-10-26 12:01Z）。按「数字须带可跑复算命令」，
  这三个数字在本报告中**不作为判据**。
- 未逐行走查 `InventoryDtoFloatGate` 的 517 行实现（只核了扫描面、白名单、`RunAll` 主链与
  五个兜底守卫的接线）；层 2 的 IL 过滤细节未逐条复审。
- 未核查 `tools/falsify/specs/s20-11-stacking-container.json` 的 14 条变异文本（只读了 story 的表格）。

---

## 四、结论

- **判定：CHANGES REQUIRED**
- **一句话理由**：核心算术与 AC-20-04 / 12 / 15 的口径扎实（`CanStack` 真调用、`StackKey` 单一定义、
  整数域零浮点、双层门有判别力），但 **P0 范围门 ③ 的执法件零调用点**——AC 记绿所依据的
  「不变量破坏 = 构建失败」在今天不成立（B-1，第五形态假绿），另有跨栈 `InstanceIds` 记账
  失真与折重口径双实现两处 WARNING 需补夹具或收敛。

### 建议的修复顺序

1. **B-1**：给 `BuildPartQualityGate` 找强制点（接 20 的装载/投影入口 + 反证夹具），
   或把该 AC 降为「执行面 BLOCKED-BY Story 010」。
2. **W-1**：拆 `StacksOf`（同时解 W-4），补「跨栈实例 id 全集覆盖」与「部分栈被补满」两条夹具。
3. **W-3**：删 `InstanceWeight` 的不可达 catch，同步改 doc。
4. **W-2**：明确 `FoldWeight` 与 `CarryLoad.SumDetailed` 谁是唯一口径；若保留两份，
   补一条「两侧对同一输入逐位相等」的对拍夹具。

### 正向观察（不省略）

- **story 自己抓出的 M2 假绿已真实修复且可复算**：`StackingRules.cs:155` 是真调用行，
  且 `:153-154` 留了「不得回退成手写」的明文警告 —— 这条自述与实作**一致**，值得作为后续
  story 的样板。
- `test_foldWeight_removeThenRefold_isBitIdentical`（`:339`）已从自指夹具改成**字面量锚 40**，
  并在注释里保留原失败形态的记录（`:331-338`）—— 「原文不删 + 加注订正」执行到位。
- AC-20-12 用**签名类型面**把「零排序」从纪律升为编译期事实（`:267`），比行为夹具更耐用。
- 三件全部**零硬编码数值**、零可变静态、门 A 干净；`InventoryDtoFloatGate` 的
  「清单非空但装配零命中 ⇒ 红」这类兜底**各有专属夹具**（`:659`），是少见的「兜底不留空」。
- story 把 AC-20-15 的错误载体（`PresentationDtoGuard` 不是 float 门）主动查明并改判、
  把 P0 门 ①② 如实挂 BLOCKED —— **主动不记绿**的记录习惯应保持。

---

## 六、修复轮结账(2026-10-27 · Phase 8b 双列)

> **本节为**修复轮**追加,不改写 §一–§五 的任何原判定(「原文不删,就地加注」)。
> 用户裁定原话:「**不进21也不记债,现在完成**」+「**「装载库存实体」的链,现在建**」。

| ID | Landed(已落) | Proven(已证) | Branch(分支覆盖) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| **B-1** P0 范围门 ③ 零执法点 | ✅ | ✅ | — | `unity/Assets/Gameplay.Boot/InventoryLoadService.cs`(`EnforceBuildPartQuality`) | 证伪 M2(把 `if (enforceGates) EnforceBuildPartQuality(instances);` 掏空)⇒ `test_rebuild_buildPartWithQualityNotOne_throwsAtLoad` + `test_loadSequencer_afterRestoreThrows_loadFailsLoud` **2 条红** |
| **B-2** 跨栈 `InstanceIds` 记账失真 | ✅ | ✅ | ✅ 2/2 | `unity/Assets/Sim/ItemDatabase/StackingRules.cs`(两条路径各自订正 + 契约文档改写) | ① 路径 1(溢出分支改走 `Append`)反证:改回 `open.InstanceIds` ⇒ `test_ac2004_partialAbsorption_recordsInstanceInBothStacks` 红;② 路径 2(登记最后一栈)反证:改回登记首栈 ⇒ 含上述共 4 条红;③ 另把零判别力的 `CollectionAssert.AllItemsAreUnique(allIds)` 换成**覆盖**断言(每个实例 id 至少出现在某个栈里) |
| **W-2** `FoldWeight` 零生产调用点 | ⚠️ 部分 | ⚠️ 部分 | — | — | **本轮未接线 `FoldWeight`**:装载链走 `CarryLoadSolver.Sum`(内部 `SumDetailed`,自带「空箱自重 + 子件逐件」)。W-2 的**第二真源**性质因此**未变**;只是 `CarryLoadSolver` 这一侧首次有了生产调用点(证伪 M3 ⇒ 载重值归零即两条红)。**如实登记为未结** |
| **W-3** `InstanceWeight` 的 catch 不可达 | ⬜ 未动 | ⬜ | — | `unity/Assets/Sim/ItemDatabase/ContainerWeightFold.cs` | 本轮未改(不在三条 BLOCKING 面内)。⚠️ **注**:S20-12 的 W-3 打的是 `CarryLoadSolver.CanCarry` 的**比较点**,与本条不是同一处 —— 勿混读 |
| **W-4** `StacksOf` 超 40 行 / 复杂度 | ⬜ 未动 | ⬜ | — | — | 本轮只改内部两条分支的**语义**,未拆分方法。standards 类,**Landed=⬜** |
| **G-1** 门 ③ 无执法点 | ✅ | ✅ | — | 同 B-1 行 | 同 B-1 |
| **G-4** `FoldWeight` 零调用点 | ⬜ 仍 open | ⬜ | — | — | 同 W-2 行 |
| **G-9** 跨栈记账无夹具 | ✅ | ✅ | ✅ 2/2 | `unity/Assets/Tests/EditMode/Inventory/stacking_container_rules_test.cs` | `test_ac2004_partialAbsorption_recordsInstanceInBothStacks` + `test_ac2004_tailPartialStack_absorbsNextInstance` |

### 诚实边界(禁借绿)

1. **判据面 = 装载期,不是构建期** —— `BuildPartQualityGate` 现在在读档 / 投影重建时抛,
   story 原文的「不变量破坏 = 构建失败」措辞**未改**,已在 story-003 就地加注。
2. **「构建期 `RunAll` 是否真执行」仍 `NOT-RUN`**(§三 G-6)—— 本轮**没有**做探针轮,
   因此「门链在真构建里生效」这件事的判据**仍然缺位**。本轮的证据只到「夹具能证明门会红」。
3. **`StacksOf` 仍零生产调用点**(§三 G-3)—— 本轮的装载链**没有**调 `StacksOf`
   (它交付的是实体集,堆叠视图归 20b 呈现侧)。G-3 维持 open。

### 复算命令

```bash
# ① 门现在有生产调用点
grep -rn "BuildPartQualityGate\." unity/Assets --include=*.cs | grep -v /Tests/ | grep -v "^\S*: *//"

# ② 跨栈记账的两条新夹具
grep -n "partialAbsorption_recordsInstanceInBothStacks\|tailPartialStack_absorbsNextInstance" \
  unity/Assets/Tests/EditMode/Inventory/stacking_container_rules_test.cs

# ③ 实测(Inventory 家族)
#    total=178 passed=178 failed=0 inconclusive=0 skipped=0
```
