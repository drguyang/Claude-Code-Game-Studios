# S20-12 评审原件 —— inventory-004 CanCarry / 载重上限裁决

**评审日期**: 2026-10-27
**评审性质**: **补做评审**（QA 签核 C-4）。⚠️ 按 `.claude/docs/review-workflow.md` §不可追补：
本报告评的是**当下代码**，**不追认** story 收口时的任何判定。
**对象**：
- `unity/Assets/Sim/ItemDatabase/CarryLoad.cs`（**387 行**，新建，`ad1b033`）
- `unity/Assets/Editor.Tools.Bake/InventoryConstantsBinder.cs`（**220 行**，新建）
- `unity/Assets/Editor.Tools.Gates/CarryCapExistenceGate.cs`（**285 行**，新建）
- `assets/data/inventory_constants.json`（作者态数据件，单键 `carry_cap`）

**测试件**：`unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs`
（**1020 行**，**54 个 `[Test]`**，**零 `[Ignore]` / 零 `[TestCase]`**）

> 复算命令（可直接跑，已实跑）
> ```bash
> cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
> wc -l unity/Assets/Sim/ItemDatabase/CarryLoad.cs \
>       unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs
> #   387 unity/Assets/Sim/ItemDatabase/CarryLoad.cs
> #  1020 unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs
>
> grep -c '\[Test\]' unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs
> #   54
> grep -cE '\[Ignore|\[TestCase' unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs
> #   0
>
> head -2 unity/Logs/s20-12-c.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"'
> #   total="54" passed="54" failed="0" inconclusive="0" skipped="0"
> #   ⚠️ 该 xml 只含 CarryLoadAdjudicationTest 一个 class（总 54），不是全量
>
> head -2 unity/Logs/s20-12-full.xml | grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"'
> #   total="5354" passed="5319" failed="0" inconclusive="0" skipped="35"
>
> git status --porcelain unity/Assets/Sim/ItemDatabase/CarryLoad.cs \
>       unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs
> #   （空输出 ⇒ 两件与提交 ad1b033 逐字一致，本报告评的就是已入库的当下代码）
> ```

**权威件**：
- `design/gdd/inventory-and-items.md` — 规则四 F-20.1（载重整数求和）/ F-20.2（`:464` `CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP`）· 边例 `:623`（**恰等 `CARRY_CAP` ⇒ 允许**）· `:283`（`CARRY_CAP = 60`，值可调）· `:447-452`（`CarryLoad` 类型 **int64**）· `:302`（`CarryRatioDto` 行仍写 `int` = **陈旧**）· `:300-314`（BL-27：slack 由 42 自算，20 不交付）· `:705-712`（自指谓词 = 弱门自陈）
- `docs/architecture/adr-006-fixed-point-boundary-contract.md` — §Decision 一 D-21-17（`weight` / `stack_max` / cap 是 **int 计数**，移出 `Fix` 解析集）+ 整数域求值
- `docs/architecture/adr-014-data-pipeline-and-json-parser.md` §五 — 装载期硬失败纪律（E-13 同款）
- `docs/architecture/adr-013-skeuomorphic-ui-framework.md` — 42 只渲染不持状态 ⇒ `CarryRatioDto` 是唯一读数出口
- `docs/architecture/adr-025-contract-assembly-manifest.md` §① — `Sim` 引用集恰 = `{BCL, Sim.Contracts}`
- `.claude/docs/coding-standards.md` — 门 A / 40 行上限 / 复数复杂度 ≤10 / 「某件有门」须给 `file:line` + 可跑命令

---

## 一、AC 结账

| AC | 载体 | Landed | Proven | Branch | Proven-by（可复算判据） |
|---|---|---|---|---|---|
| AC-20-08 零浮点 | `CarryLoad.cs` + `ContainerWeightFold.cs` | ✅ | ✅ | ✅ 5/5 | 源码类型 `test_ac2008_sourceSurface_hasNoFloatingPointTypes`（test:129）· 字面量（:153）· **Cecil IL** `conv.r4/r8`（:174，含 `scannedMethods > 0` 防空集借绿）· 签名 `Func<int,int>`（:214）· `Fix` 口径恒拒（:235）。旁证：`InventoryDtoFloatGate.ScanDirsRelative` 覆盖 `Assets/Sim/ItemDatabase`（`InventoryDtoFloatGate.cs:70`） |
| AC-20-09 硬不变量 | `CarryLoadSolver.CanCarry` + 测试内累加器 | ✅ | **✗** | ✗ 2/3 | 接受路径后置断言 `test_ac2009_writePathPostcondition_holdsAcrossArbitrarySequence`（test:256，1000 步，`accepted>0 ∧ rejected>0` 双防空跑）**真判据**；绕过 `CanCarry` ⇒ 必越界（test:298）**真判据**；**拒绝路径断言 `test:287` 是同义反复**（见 W-1）⇒ 该分支实际零覆盖 |
| AC-20-13 未定值硬失败 | `CarryCapacity` 构造器 + `InventoryConstantsBinder` | ✅ | ✅（运行期半）／**NOT-RUN**（构建期半） | ✅ 10/10 | `CarryCapacity(null/0/负/Fix)` 各一具名异常（test:402/412/421/450）；`Binder` 缺键/零/负/越 int/字符串/浮点各一具名错误（test:471/504/543 + `Binder.cs:174,204,210`）；`test_ac2013_productionCode_hasNoDefaultCapacityConstant`（test:504）反射扫 `Sim`+`Sim.Contracts` 全部 `const int *CarryCap*/*Capacity*`。⚠️ **Binder 零生产调用点** ⇒ 构建期半 **NOT-RUN**（见 W-2） |
| AC-20-18 满档等价 | `IsFullForAnyAddition` | ✅ | ✅ | ✅ 4/4 | `test_ac2018_wMinGreaterThanOne_boundaryIsCapMinusWMin`（test:557）**字面量四点**：`cap=20, wMin=2` ⇒ `17→False / 18→False / 19→True / 20→True` —— 18（`== cap−wMin`）处断言**仍可放**，正是 `== cap` 简化会谎报的那一段；`fullPredicate_agreesWithCanCarryForEveryItem`（test:574）对 `load ∈ [0, cap+1]` × 表内每件做**等价性全扫描**；`wMin=1` 退化（test:595） |
| AC-20-21 存在性门 | `CarryCapExistenceGate`（纯函数 + fail-closed 适配器） | ✅ | **✗** | ✅ 判据 A/B/C/D 各 1 红夹具 | 判据本体已证红：`emptyItemTable_isRed`（test:673，点名 B）· `allIllegalWeights_isRed`（test:682，点名 C）· `nonPositiveCap_isRed`（test:691，点名 A）· 取数 fail-closed（test:716/736）。**但 AC 文本是「WHEN 构建门求值」** ⇒ 构建期执行 **未跑探针轮**（story 限制 ② 自陈，本评审确认其自陈属实）⇒ 按 `coding-standards.md` §「构建绿 ≠ 门跑了」**不得记绿** |
| AC-20-24 单源 / 零派生 | `CarryRatioDto` + 数据件 | ✅ | ✅ | ✅ 5/5 | 恰两字段反射（test:765）· 禁用词表 `slack/remain/percent/ratio/visual/capacity` + 浮点类型（test:782）· `RemainingSlack` 显式点名为 null（test:813）· `CarryLoad.cs` 零 `const int *Cap*`（test:822）· `assets/data` 顶层唯一含 `"carry_cap"` 的文件（test:835） |
| （相邻面）容器两路 | `SumDetailed` | ✅ | ✅ | ✅ 4/4 | 单层 / 显式子件表 / 双计本主 / 双计跨主 / 孤儿 / null≡单层（test:854–960） |

**夹具分布复核（与 story 自述逐条对齐，无出入）**：AC-20-08 **5** · 09 **9** · 13 **10** · 18 **10** · 21 **9** · 24 **5** · 容器 **6** = **54**，与 `grep -c '\[Test\]'` = 54 一致。

---

## 二、发现

### 2.1 BLOCKING

**无。** 核心算术、谓词与 ADR 边界均正确，关键边界（恰等 cap / `cap−W_MIN`）都由**字面量**断言钉住，非自指夹具。

### 2.2 WARNING

**W-1 · 两处同义反复断言被当作证明陈述（「自造断言顶替 AC 语义层」同族形态）**

1. `carry_load_adjudication_test.cs:287-290` —— AC-20-09 的拒绝分支：
   ```csharp
   long before = load;
   rejected++;
   Assert.That(load, Is.EqualTo(before), "拒绝路径不得改载重");
   ```
   `before` 与 `load` 之间**零语句**，`load` 不可能变 ⇒ **恒真，零判别力**。任何让拒绝路径真实改载重的变异都不会被它抓到。该属性在结构上确实成立（`CanCarry` 是纯谓词），但**这条断言并未证明它**。

2. `carry_load_adjudication_test.cs:329-331` —— AC-20-09 的恰等界：
   ```csharp
   long load = 10L;
   Assert.That(load, Is.LessThanOrEqualTo(capacity.Cap), "恰等界仍满足硬不变量");
   ```
   `10L` 是测试自己写的局部常量，与被测件无关 ⇒ 断言只证明「10 ≤ 10（且 10 ≤ 10 恒成立）」，**对硬不变量零举证**。真正的恰等界判据是紧邻的 `:326` 那条（`4 + 3×2 == cap 10 ⇒ Is.True`），它是有效的。

**定级理由与升级判据**：核心主张（接受路径后置断言）是真判据，故不判 BLOCKING。但本项目已三次抓到「测试自造断言顶替 AC 语义层」（见 agent memory `feedback_ac_semantic_layer_detection`），且 coding-standards 明令「注释里声称有断言不构成门存在」。**若 QA 按该先例判 BLOCKING，本项应随之升格** —— 这两条的形式与先例完全同型。处置成本极低：删除或改为「让被测件自己产出 `load`」。

**W-2 · `InventoryConstantsBinder` 零生产调用点 ⇒ AC-20-13 的「构建期执行体」是死代码**

```bash
grep -rn "InventoryConstantsBinder\." unity/Assets --include=*.cs | grep -v /Tests/ | grep -v "Editor.Tools.Bake/InventoryConstantsBinder.cs"
# （零输出）
```
该件在 story §交付面被描述为「ADR-014 阶段 2 绑定层（**AC-20-13 的构建期执行体**）」，但其 `BindFromRepo` / `Bind` **只有测试调用**。⇒ 真实构建里 `inventory_constants.json` 的六条负路径校验**不会执行**（`CarryCapExistenceGate.TryReadCarryCap` 是**另一份**正则取数器，只 fail-closed 到「取不出/非正」，不复现「字符串 vs 浮点 vs 越 int」的具名区分）。

按 coding-standards §「某件有门」：本项**缺可证明会红的构建命令** ⇒ 按 `NOT-RUN` 登记。

缓解事实（不掩盖，但也不越权）：AC-20-13 的**运行期**半边（`CarryCapacity` 构造器恒抛）是真实的，且被三个生产调用方消费（`InventoryTransaction.cs:220`、`IgnitionAdmission.cs:137`、`CraftIgnitionTransaction.cs:260`）⇒ AC 的实质需求「未定值 ⇒ 装载硬失败」在运行期**成立**。死的只是冗余的烘焙期一层。

**W-3 · `CanCarry` 的比较点在溢出面上未加 `checked`，且 story 自己的 QA 用例要求的溢出哨兵夹具缺席**

- 累加路径**已全部保护**：`CarryLoad.cs:248/271/276/277` 四站 `checked`；`ContainerWeightFold.InstanceWeight` 用 `checked((long)unitWeight * qty)` 并转具名异常（`ContainerWeightFold.cs:74-83`）。这部分**合规**，无问题。
- **未保护的是比较点** `CarryLoad.cs:316`：
  ```csharp
  return currentLoad + candidate <= capacity.Cap;
  ```
  `currentLoad` 是对外公开的裸 `long` 参数，只有 `< 0` 一道守卫；`candidate` 可达 ~2^62。二者相加 **可回绕成负数** ⇒ `负数 <= cap` 为真 ⇒ **静默接受超容量装载**。这正是本文件自己在 `:74-78` 论证 DTO 不许收窄到 int32 时所反对的「静默回绕」形态。
- 可达性：经 `CarryLoadSolver.Sum` 产出的 `load` 因四站 `checked` 不可能逼近 2^63 ⇒ **当下生产路径不可达**；但 `CanCarry` 是公开谓词，未来任何绕开 `Sum` 的调用方（或损坏的投影）都能触达。故判 **WARNING 而非 BLOCKING**。
- 佐证该缺陷是「本可提前抓到」的：story 的 §QA Test Cases（AC-20-08 条）**明文要求**「`long` 溢出哨兵夹具（临界 qty×weight 逼近 2^31/2^63）」。实跑：
  ```bash
  grep -n "MaxValue\|Overflow" unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs
  # （零输出）
  ```
  ⇒ **该夹具从未落地**。处置：比较点加 `checked`（或显式上界守卫）+ 补一条哨兵夹具，二者一起做才有判别力。

**W-4 · 三个方法超 coding-standards 的 40 行上限**

| 方法 | 行距 | 行数 |
|---|---|---|
| `CarryCapExistenceGate.Evaluate` | `CarryCapExistenceGate.cs:116-181` | **66** |
| `CarryLoadSolver.SumDetailed` | `CarryLoad.cs:226-282` | **57** |
| `InventoryConstantsBinder.ReadCarryCap` | `InventoryConstantsBinder.cs:164-218` | **55** |

属 standards 类，不阻断交付（且 `Evaluate` 的 A/B/C/D 四段已有清晰分段注释，拆分成本低）。按 Phase 8b 纪律：**Landed=✅ / Proven=✗**（≤40 界无任何夹具钉住 —— 与 `review-*` 里 J 项同型）。

### 2.3 MINOR

**M-1 · `CarryLoadSolver.Sum` / `Ratio` / `MinWeight` / `IsFullForAnyAddition` 生产调用点全为零**

```bash
grep -rn "CarryLoadSolver\.Sum\|CarryLoadSolver\.Ratio\|CarryLoadSolver\.MinWeight\|CarryLoadSolver\.IsFullForAnyAddition" unity/Assets --include=*.cs | grep -v "/Tests/" | grep -v "ItemDatabase/CarryLoad.cs"
# 仅 3 条命中，全部是**注释里的提及**（CraftIgnitionTransaction.cs:55 / IgnitionAdmission.cs:46 / CarryLoad.cs:320 自身）
```
- `CanCarry` 有真实生产调用点（3 处），✅ 已接线。
- `Sum`（F-20.1 的 `Σ weight×qty` 本体）**零消费方** ⇒ 生产代码里**没有任何地方真的算出 `CarryLoad`**；`ValidatePickup` / `CanAdmit` / `CraftIgnitionTransaction` 的 `currentCarryLoad` 都是**外部传参**。story §未落地项 #1 已登记「运行期 `IDataProvider` 装配归装配轮」，属**已声明**范围，但「单一真源已就位、零消费者」的状态应显式保持在台账上，勿被读成「载重已接入」。
- `Ratio` 零消费方 ⇒ AC-20-24 的「唯一容量出口」尚未被 42 / 52 消费（归 story 006 / 52 轮）。
- 这是**诚实的未落地**，不是缺陷。登记备查。

**M-2 · `CarryCapExistenceGate` 步数上界用 `wMin` 计算、循环用 `wMax` 累加（消息与实现口径不自洽）**

`CarryCapExistenceGate.cs:163` `stepBound = cap / wMin + 2`，而 `:171` 每步 `load += wMax`。上界**是充分的**（所需步数 ≤ ⌈cap/wMax⌉ ≤ ⌈cap/wMin⌉），故**不是 bug**。但失败消息 `:176` 写「领取 {steps} 件最重物品…步数上界 ⌈cap/W_MIN⌉+2」把两个量混在一句里，排障时易误读为「按 wMax 算的上界」。建议换成 `cap / wMax + 2` 并同步消息 —— 更紧、也更自洽。

**M-3 · story §限制 ③ 的归因有一处不准确（`heaviestSingleItem` 承担不了「真的领满」）**

story 写：「后者由 M17 + `heaviestSingleItem` 承担」。实测 `test_ac2021_heaviestSingleItemExceedingCap_isImmediatelyFull`（test:708，`cap=3 / w={5}`）在 **step 0** 就 `CanCarry(0,5,1,3)` 为假 ⇒ 与 `singleUnitWeightTable` 一样，**M16 取反后立即返回绿**，对「是否真装满」贡献**零**判别力。真正承担该属性的是 `realData_gateIsGreen`（story 第 4 轮记录里 M17 多红的那条）。建议订正该句，否则「两条夹具合起来覆盖了」是过强的自述。

**M-4 · `test_ac2024_capacityTruth_definedInDataFileNotInCode` 只扫 `assets/data` 顶层**

`test:826-838` 用 `SearchOption.TopDirectoryOnly` ⇒ 子目录里的第二份 `carry_cap` 不会被抓。当前 `assets/data` 无子目录，**今天无假绿**；但判据应写 `AllDirectories` 才具持续性。

**M-5 · GDD `:302` 的 `CarryRatioDto = {carry_load: int, cap: int}` 与实现（`long`）不一致 —— story 的 ADVISORY 登记属实且分类正确**

独立核对：GDD `:447-452` 明写 `CarryLoad | **int64**`，`:302` 的 DTO 行仍是 `int` ⇒ **GDD 内部先分叉**。实现取较新的 int64 口径**方向正确**（收窄到 int32 会新增一条无守卫的回绕路径，正是 `CarryLoad.cs:74-78` 反对的形态）。AC-20-18 文本只说「整数对」，**未**钉 int vs int64 ⇒ **不构成 AC 违例**。建议把 GDD `:302` 的 `int` 订正为 `int64`，消除文档内分叉。

**M-6 · `CarryLoadSolver` 是静态类而非接口（与项目「Systems expose interfaces」标准的表面偏离）**

`coding-standards.md` 要求「Systems expose interfaces (not concrete class dependencies)」。本件是纯函数工具（零状态、零 I/O），且与代码库既有先例同型（`ContainerWeightFold` / `IgnitionAdmission` / `StackingSolver`）⇒ 判 **INFO / 一致于既有模式**，不建议为此件单独立接口。

---

## 三、缺口登记（不掩盖）

| # | 缺口 | 性质 | 归属 |
|---|---|---|---|
| G-1 | AC-20-21 的**构建期执行**未跑探针轮（story 限制 ② 自陈） | **NOT-RUN**（禁借绿） | 下次真机构建轮 |
| G-2 | `InventoryConstantsBinder` 零生产调用点 ⇒ 烘焙期六条负路径不执行 | **NOT-RUN**（W-2） | 装配 / 烘焙接线轮 |
| G-3 | 溢出哨兵夹具缺席（story §QA Test Cases 明文要求，实为零） | 缺口（W-3） | 本件修复轮 |
| G-4 | 运行期 `IDataProvider` 装配 + `*.cooked` 产物不存在 | 已声明未落地（story §未落地项 #1） | 装配轮（届时须登记 `cooked_drift_all_artifacts_test` 的 `BakedArtifacts`） |
| G-5 | `CarryLoadSolver.Sum` / `Ratio` 零消费方 ⇒ 载重从未在生产中被真正计算 | 已声明未落地（M-1） | Story 005 / 006 / 52 |
| G-6 | AC-20-08 的 IL 面**仅 Mono 后端**；IL2CPP 生成物未对拍 | 已声明未落地（story 限制 ④） | ADR-012 三格矩阵 |
| G-7 | AC-20-21 是弱门：抓不到「cap 大到能带齐一切」 | GDD `:705-712` 自陈的设计弱点，非本件 bug | 待 OQ-20-9 具名 `ESSENTIAL_KIT` |
| G-8 | `singleUnitWeightTable` 夹具对「谓词取反」零判别力 | **独立复核：属实**（见下） | 需 M17 型属性夹具补位（已有 `realData_gateIsGreen` 承担） |

### 对「`singleUnitWeightTable` 夹具判别面受限」说的独立判断

**属实，且成因分析正确。** 依据（读码而非采信自述）：

`CarryCapExistenceGate.cs:165-172` 的循环体是
```csharp
if (!CarryLoadSolver.CanCarry(load, wMax, 1, capacity))
    return errs;            // ✅ 找到满状态 ⇒ 绿
load = checked(load + wMax);
```
`test_ac2021_singleUnitWeightTable_reachesFullState`（test:699，`cap=10 / w={1}`）在 **step 0** 时 `CanCarry(0,1,1,cap10)` = `1 ≤ 10` = **true**。若把判据取反（M16 形态：`if (CanCarry(...)) return errs;`），**同一条夹具仍然绿** ⇒ 该夹具只能证「门不误报红」，不能证「门真的领满了 cap」。

补充（这是我的独立补充，非 story 已登记）：**`heaviestSingleItem` 属同一逃逸面**（见 M-3），故 story 限制 ③ 的免责范围写得比实际宽。真正的补位是 `realData_gateIsGreen` + M17（「找到了也不返回」）。

---

## 四、结论

**判定：APPROVED WITH SUGGESTIONS**

一句话理由：`CarryLoad.cs` 的算术（整数域、四站 `checked`、int64 累加、零 float、零硬编码容量）与两个关键边界（恰等 `cap` 接受、`load > cap − W_MIN` 满档）**都与 GDD / ADR-006 逐条相符，且由字面量夹具钉住**，`CarryLoad` 不存在第二实现；失分集中在**外围**：两条同义反复断言冒充证明（W-1）、烘焙期执行体零接线（W-2）、比较点未 `checked` 且 story 自己要求的溢出夹具缺席（W-3）、三处超 40 行（W-4），以及四项诚实登记的 NOT-RUN / 未落地缺口。

**最关键三条**（按处置优先级）：
1. **W-1** —— `test:287` 与 `test:329-331` 是同义反复，分别被当作「拒绝路径不改载重」与「恰等界满足硬不变量」的证明。这与本项目已三次抓到的「自造断言顶替 AC 语义层」同型；若 QA 按先例判 BLOCKING，本项随之升格。
2. **W-3** —— `CanCarry` 比较点 `CarryLoad.cs:316` 未 `checked`（公开裸 `long` 入参 + `~2^62` 候选 ⇒ 可回绕为负 ⇒ 静默放行），而 story §QA Test Cases 明文要求的溢出哨兵夹具**从未落地**（`grep MaxValue|Overflow` 零命中）。
3. **W-2** —— `InventoryConstantsBinder` 零生产调用点 ⇒ AC-20-13 的「构建期执行体」在真实构建里永不运行，仅测试驱动；运行期半（`CarryCapacity` 构造器）成立，故 AC 实质需求未失守。

**升 BLOCKING 的判据（留给 QA 裁）**：W-1 若按「自造断言顶替 AC 语义层」先例判 BLOCKING，则本件应转 **CHANGES REQUIRED**，最小修复 = 删两条空心断言 + 改由被测件产出被断言值（半小时内可完成），不必重跑 54 条以外的东西。

### 未及核查项（禁借绿）

- **复数复杂度 ≤10**：未跑度量工具，仅静态目测 `Evaluate` ≈ 8 分支、`ReadCarryCap` ≈ 7 分支、`SumDetailed` ≈ 5 分支，**均判为「目测在界内，未实测」**。
- **`CraftIgnitionTransaction` 自身是否被生产代码调用**：只确认了它内部调 `IgnitionAdmission.CanAdmit`（`:260`），未上溯其调用方；`WriterExistenceGate.cs:158` 的注释暗示「只接了容量预检」，未核实。
- **`tools/falsify/specs/s20-12-carryload-adjudication.json` 的 17 条变异内容**：文件存在（`python3 json.load` 读得 `count 17`），但键名与预期不符，**未逐条核对 M1–M17 的靶点行号与 `expectRed` 列表**。故本报告对 §Falsifiability Gate 一节的采信限于「17 条存在」，**不追认其逐条命中形态**。
- **`unity/Logs/s20-12-c.xml` 的产生时间（10-26 20:56）与 `CarryLoad.cs` 的最后修改**：`git status` 证明当前两件与 `ad1b033` 一致，但未用 `git log -1 --format=%ci ad1b033` 与日志时间做先后比对 ⇒ 「这份 54/54 的日志测的正是当前代码」**未闭环证明**（属时间邻近推断）。

---

## 五、修复轮结账(2026-10-27 · Phase 8b 双列)

> **本节为**修复轮**追加,不改写 §一–§四 的任何原判定(「原文不删,就地加注」)。
> 用户裁定原话:「**不进21也不记债,现在完成**」。

| ID | Landed(已落) | Proven(已证) | Branch(分支覆盖) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| **W-1** 两处同义反复断言 | ✅ | ✅ | — | `unity/Assets/Tests/EditMode/Inventory/carry_load_adjudication_test.cs` | 两条均已改为**由被测件产出被断言值**:`long load = CarryLoadSolver.Sum(projection, …, owner)` 在**接受**的累加账本上重算,`before` 与终值之间隔着真实的接受调用。**反证**:把 `CanCarry` 的拒绝分支改成会改账本(注入一行)⇒ 该条红 |
| **W-3** `CanCarry` 比较点未 `checked` | ✅ | ✅ | ✅ 2/2 | `unity/Assets/Sim/ItemDatabase/CarryLoad.cs`(`:316` 附近) | `checked(currentLoad + candidate)` + `catch (OverflowException)` → 具名 `InvalidOperationException`。夹具 `test_ac2009_overflowingSum_throwsInsteadOfSilentlyAdmitting`:**反证**(去掉 `checked`)⇒ 该条红。⚠️ 另一半 `CanCarry(long.MaxValue - 4, 4, 1, cap)` ⇒ 合法地判 `Is.False`(不抛)—— 「算不出来」与「装不下」在类型上可分 |
| **W-2** `InventoryConstantsBinder` 零生产调用点 | ⬜ 未动 | ⬜ | — | `unity/Assets/Editor.Tools.Bake/InventoryConstantsBinder.cs` | 本轮未接。⚠️ **注**:本行 W-2 与 S20-11 的 W-2(`FoldWeight`)是**同名不同物**,勿混读 |
| **W-4** 三处超 40 行 | ⬜ 未动 | ⬜ | — | — | standards 类,`Landed=⬜` |
| **M-1** `Sum` / `Ratio` / `MinWeight` 生产调用点全零 | ⚠️ 部分 | ⚠️ 部分 | — | `unity/Assets/Gameplay.Boot/InventoryLoadService.cs` | `CarryLoadSolver.Sum` **首次有生产调用点**(证伪 M3:把 `long load = CarryLoadSolver.Sum(` 改成 `long load = 0L;` ⇒ `test_rebuild_computesUnclaimedLoadFromWeights` + `test_loadSequencer_afterRestoreHookRunsAndRebuildsProjection` 2 条红)。`Ratio` / `MinWeight` / `IsFullForAnyAddition` **仍零调用点**,如实登记 |

### 诚实边界(禁借绿)

1. **`CarryLoadSolver.CanCarry` 仍零生产调用点** —— 装载链只调 `Sum`(纯读数)。`CanCarry` 的
   「装不装得下」是**判定**,其生产调用方是 Story 005 的原子装卸事务 —— 那件仍未接线。
   ⇒ W-3 的「静默放行」在**今日生产路径上不可达**(只有 `Sum` 在用),本轮的修复价值是
   「未来接线的调用方不会踩坑」,不是「今天就有一条被堵住的路」。
2. **`Ratio` / `MinWeight` / `IsFullForAnyAddition` 零生产调用点未变**(§2.3 M-1 的三个)。
3. **ADVISORY 类的两条同义反复断言已修,但 W-1 的「是否应升 BLOCKING」这一裁**仍归 QA ——
   本轮的处置是**按最严判据修**(改成真判据),从而让「升不升」不再有实际影响。
