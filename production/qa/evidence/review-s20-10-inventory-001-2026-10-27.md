# S20-10 评审原件 —— inventory-001 InventoryOf 折叠重建

**评审日期**: 2026-10-27
**评审性质**: **补做评审**（QA 签核 C-4）。⚠️ 按 `.claude/docs/review-workflow.md` §不可追补：
本报告评的是**当下代码**，**不追认** story 收口时的任何判定。story §评审 自陈「未做 `/code-review`」，
故本件是**首次**独立评审，不是对历史判定的复核。

**对象**：
- `unity/Assets/Sim/ItemDatabase/InventoryFold.cs` — **633 行**（门 A 纯 C#）
- 类型数 **6**（`InventoryRow` / `InventoryFoldGapKind` / `InventoryFoldGap` /
  `InventoryProjection` / `InventoryFoldResult` / `InventoryFold`）

**测试件**：
- `unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs` — **710 行** · **28 个 `[Test]`** · **`[Ignore]` = 0**
- 落地树为工程主体树（`unity/Assets/Tests/EditMode/`）；账本树 `tests/` 内 `grep -rn "InventoryFold"`
  **零命中**（两树均已扫）

> 复算命令（可直接跑，已在本轮实际执行）：
> ```bash
> cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
> wc -l unity/Assets/Sim/ItemDatabase/InventoryFold.cs \
>       unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs      # 633 / 710
> grep -c '\[Test\]' unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs   # 28
> grep -c '\[Ignore' unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs   # 0
> grep -o '<test-run[^>]*' unity/Logs/inv-fold.xml | head -1
> #   testcasecount="28" result="Passed" total="28" passed="28" failed="0" skipped="0"
> sha256sum unity/Assets/Sim/ItemDatabase/InventoryFold.cs tools/falsify/baseline-invfold/InventoryFold.cs
> #   两侧同为 1c64d1639ff15bb9187559a33d640211c4bcf103ae060393b020794dbfea0359  ← 还原判据成立
> ```

**权威件**：
- `design/gdd/inventory-and-items.md` — 规则一（`:151` fold 谓词式）· 规则五（`:346` 零求值）·
  规则九（`:620` 跨栈相邻多条）· AC 表（`:862` AC-20-03 · `:864` AC-20-05 · `:875` AC-20-16 · `:876` AC-20-17）·
  §涟漪登记（`:920` R2 · `:928` R10 · `:946` R16 · R15/R17/R18 同段）
- `docs/architecture/adr-009-world-state-event-boundary.md` §一 / §二（三态分类 · 库存 = 派生态）
- `docs/architecture/adr-005-deterministic-sim.md`（事件流唯一真源 · 主机唯一 `Append`）
- `docs/architecture/adr-006-fixed-point-boundary-contract.md`（定点边界 · 禁 `float` 进 sim）
- `docs/architecture/adr-031-payload-decoder-abstraction.md` §①（`Sim` 读载荷唯一合法路径 = `IPayloadDecoder`）
- `.claude/docs/technical-preferences.md`（命名 · Forbidden Patterns 仍为「None configured」）·
  `.claude/docs/coding-standards.md`（doc 注释 · 无硬编码 · 可测性）· `.claude/rules/test-standards.md`

**记分基线**：story 自报 **2/4 记绿**（AC-20-05 · AC-20-17），AC-20-03 / AC-20-16 **不勾**。
本评审**认可该记分方向**，但对 AC-20-05 的 `✅` 提出**范围异议**（见 §2.2 W-1）。

---

## 一、AC 结账

| AC | 载体 | Landed | Proven | Proven-by（可复算判据） |
|---|---|---|---|---|
| **AC-20-03** 清空重建逐位相同 | `InventoryFold.Fold`（`:305`）· 8 条夹具（`inventory_fold_test.cs:93/115/133/151/168/186/212/230`） | ✅ 机制面 | **✗（AC 行不勾，与 story 一致）** | 反向记录：M1 改按传入序 ⇒ `test_ac2003_shuffledInput_yieldsIdenticalProjection` 红（spec `M1`）；M3 停剔除 ⇒ `despawnToZero` 红；M6 注入跨调用状态 ⇒ `sameStreamTwice` 红。⚠️ 但**增益/归还/建造扣/非 17 来源品级四面结构性不可重建**（R15–R18）⇒「逐位相同」在全集上**不可成立**，不勾正确 |
| **AC-20-16** 用药经 `DropDespawned` 消失 | 双向夹具（`:244` 反向 · `:266` 正向） | ✅ fold 消费端 | **✗（AC 行不勾，与 story 一致）** | 正向 `:279` 补 `DropDespawned` ⇒ 实例消失；反向 `:260` 只有病史流事件 ⇒ 实例仍在。⚠️ **缺 `DropDespawned` 生产端**（`grep` 见 §3 G-1）⇒「用药会发它」零证据；7a 往返半条未验 |
| **AC-20-05** 20 零结算求值 | 源面 `:521` + IL 面 `:542` | ✅ | **⚠️ 部分 —— 见 W-1** | 源面判据（剥注释符号扫描）自足；IL 面**实际扫描面 = 单个类型**（`:567` `type.FullName != "DaYiJingCheng.Sim.InventoryFold"` ⇒ `continue`），**不是 AC 原文的「20 的代码路径」** |
| **AC-20-17** 零 `ItemConsumed`/`ItemTransferred` | 枚举面 `:594` + 七谓词面 `:605` | ✅ | **✅** | `Enum.IsDefined(typeof(EventKind), "ItemConsumed"/"ItemTransferred")` = false（`:598`/`:600`）—— 真能力断言，不可自造；七谓词面由 M5（加 `case ActorCellEntered` ⇒ 红）反证 |

---

## 二、发现

### 2.1 BLOCKING

**B-1 · `InventoryFold.Fold` 在生产代码中零调用点，且当前任何消费者都必然抛**

实测（已跑，非推断）：

```bash
grep -rn "InventoryFold\.Fold\|InventoryFold\." unity/Assets --include=*.cs \
  | grep -v "/Tests/" | grep -viE "^\S+:[0-9]+:\s*(//|///|\*)"
# (none outside comments)
```

命中的 12 处**全部是注释**（`StackingRules.cs:16/236`、`InventoryTransaction.cs:50`、
`DropEventWriter.cs:192`、`CraftEventWriter.cs:49`、`FurnaceState.cs:95`、`HarvestOrchestrator.cs:174` 等）。
`Gameplay.Boot/CompositionRoot*.cs` 内 `grep -rn "Inventory"` **零命中**；账本树 `tests/` 亦零命中。

派生的**更重的一层**（不是「没接线」这句套话）：本实现的 `IsComplete` 在**任何真实流**上都恒为
`false` —— `R18`（`:449`）要求每个活行都有品级来源，而 `R16`/`R17` 一旦出现 `StructurePlaced`/
`StructureRemoved` 就必报缺口（`:414`/`:425`，**无条件**，不看上下文）。真实存档里建造事件不可能缺席
⇒ 按本实现自己的契约，`RequireComplete()`（`:255`）**在任何真实存档上都会抛**。
`Projection` 因此只是「部分应用态」，而 story §对下游的契约承诺 **C-1 / C-6**（`003`/`004`/`42` 读它）
today **零兑现面**。

⚠️ **这是 story 未登记的缺口**：story「未落地项」表列了 4 项（性能门 / 按格视图 / 高水位 / `DropDespawned` 写者），
但**没有**「fold 本体零调用点、且对真实流恒不完整」这一条。按任务纪律「不得因测试全绿就说它可用」，
本项必须进登记表（见 §3 G-1），并在**任何**下游 story 以 `Projection` 为输入之前裁出接线方案。

⚠️ 不判「死代码」——`InventoryFold` 是被 story 明确交付的**本体**，且已有 3 条下游测试（`carry_load_adjudication_test` /
`inventory_atomicity_test` / `craft_event_payload_test`）引用其类型。这里断的是**生产接线缺失**与
**「恒不完整」这一未登记后果**，两者都不是「代码写错了」。

**B-2 · `ConflictingQualitySource` 分支零夹具 + 零变异，且该分支在真实流上可达**

`InventoryFold.cs:519-525`：同一实例两次 `ResourceHarvested` 给出不同 `out_quality` ⇒ 报缺口。
实测覆盖为零：

```bash
grep -c "ConflictingQualitySource" unity/Assets/Sim/ItemDatabase/InventoryFold.cs  # 3（定义 + 2 处使用）
grep -c "ConflictingQualitySource" unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs  # 0
grep -c "ConflictingQualitySource" tools/falsify/specs/s20-10-inventory-fold.json  # 0
```

**它可达，不是纵深防御**：`ResourceHarvested` 无「每实例恰好一条」的守卫（`CollectHarvested`
`:508-531` 只查矛盾、不查重复），17 侧重复采集 / 重放叠加 / 分段流拼接都会产生两条。
**失效场景**：此时 `gaps` 会加一条 ⇒ `IsComplete == false`，故行为**不是静默错算**；
但「品级最早一条胜出（`:526-529` `continue` 保留首次）」这一**取值语义**既无夹具也无变异
⇒ 若有人把 `:528` 改成后写覆盖，28 条夹具**全绿**，而「逐位重建」的结果已经变了。
按 Phase 8b 新增的 `Branch(分支覆盖)` 列标准：**多分支判定缺分支 ⇒ 该行 `Proven` 不得标 ✅**。
本件应在补一条夹具（两采不同档 ⇒ 报缺口且首条胜出）之前，把该分支登记为 `Proven = ✗`。

### 2.2 WARNING

**W-1 · AC-20-05 的 IL 面判据范围窄于 AC 原文，`✅` 有借绿成分**

AC 原文（`design/gdd/inventory-and-items.md:864`）：「`GIVEN` **20 的代码路径**，`WHEN` 检索
`EFF`/`QualityMod`/`quality_distribution` 的求值，`THEN` 零」。
而 IL 夹具（`inventory_fold_test.cs:567`）只扫**一个类型**：

```csharp
if (type.FullName != "DaYiJingCheng.Sim.InventoryFold") continue;
```

`Sim/ItemDatabase/` 下共 **17** 个 `.cs`（`ls unity/Assets/Sim/ItemDatabase/*.cs | wc -l`）。
另两处 AC-20-05 相邻面亦未覆盖：
1. **嵌套编译器类型**：`Fold` 内 `:495-499` 有 `Array.Sort(idx, (a,b) => …)` lambda，
   编译为 `InventoryFold/<>c__DisplayClass…` 一类嵌套类型，`FullName` **不等于** `…InventoryFold`
   ⇒ 该闭包体（以及将来任何加进 fold 的 lambda 体）**落在扫描面之外**。这与 `scanned > 0`（`:584`）
   的反空集断言**不冲突** —— 反空集断言只挡「一条都没扫到」，挡不住「扫了但漏了一半」。
2. 源面判据（`:521`）含 `Does.Contain("DropSpawned")` 下界，但仍只读**一个文件**。

⇒ **判定**：AC-20-05 对 `InventoryFold.cs` 成立；对「20 的代码路径」**未证**。
两处应二选一：**(a)** 把 IL 扫描面放宽到 `Sim.ItemDatabase` 命名空间前缀（`AssemblyGates` 已有
`IsInNamespacePrefix` 设施，`AssemblyGates.cs:535/654`）；**(b)** 把 AC-20-05 的 `✅` 降级为
「本件覆盖 `InventoryFold` 一个类型，余 16 文件未扫」，并登记为缺口。
⚠️ 纯 `grep` 层我未发现越界：`ConservationSolver.cs` 的 `EFF` 全部出现在**注释**里
（剥注释后零命中，本节已实跑）。但这不构成「其余 16 文件零求值」的证明 —— 那是 `(a)` 才有的资格。

**W-2 · AC-20-17 的判据与 GDD 原文不同形（结论一致，措辞漂移）**

GDD（`:876`）：「扣减由 `Craft.ActualConsumed` / **`StructurePlaced` 的 `cost(m)`** 唯一推出」。
story 与测试判的是「枚举里无 `ItemConsumed`/`ItemTransferred`」+「七谓词外 Kind 不改变投影」。
后者**不覆盖**「`StructurePlaced.cost(m)` 唯一推出」这半边 —— 而本实现对 `StructurePlaced`
的处理是**无条件报 R16 缺口、不扣任何量**（`:410-419`）。故 AC-20-17 的「唯一推出」在
建造侧 today **不成立**（被 R16 挡在门外）。结论（零追加 Kind）仍成立，但 AC 的**两个**从句
只兑现了**一个**。建议把 AC-20-17 的 `✅` 限定为「零追加 Kind 面」，把「`cost(m)` 唯一推出」
显式挂 R16。

**W-3 · 缺 `ConflictingQualitySource` 之外的第二个未覆盖面：`InvalidDespawnQuantity` 的 `p.Qty <= 0` 半边**

`:384`（`p.Qty <= 0`）与 `:390`（`p.Qty > row.Qty`）是**两个不同分支**、**两个不同失效模式**
（前者是畸形输入，后者是越界）。夹具 `:413` 只打 `qty=5 > 2`（后者），变异 `M9`（spec `:131`）
也只打后者 ⇒ **`:384` 分支零夹具零变异**。`:387` 的注释断言「负值 / 零属畸形输入，不是状态」
这句**没有任何可复算判据支撑**。

### 2.3 MINOR

- **M-1 · `NoOwner` 哨兵的断言是自指夹具**：`inventory_fold_test.cs:144` 断
  `row.Owner == InventoryProjection.NoOwner`，两边一起指 `:186` 的 `const int NoOwner = -1`。
  变异 `const` 为 `0` ⇒ 夹具同步变化 ⇒ **零红**（spec 内 `NoOwner` 唯一出现是 M8 的守卫文本，
  非对哨兵值的变异）。消息（`:145`）写着「不是 0（0 是合法 player_id）」但**没断** `NoOwner != 0`。
  ⇒ 会把「未认领」与「`player_id = 0` 的玩家」混为一谈的回归**无夹具可抓**。
  建议补一条 `Assert.That(InventoryProjection.NoOwner, Is.Not.EqualTo(0))`（字面量，非自指）。
- **M-2 · R18 缺口的 `EventKind` 是合成归属**：`:452-453` 对缺失品级的行一律报
  `EventKind.DropSpawned` + `NoEvent` 全序键（`At.Tick = 0`），而该行**未必**由 `DropSpawned` 建立
  （例如它可能先出现于别处）。缺口定位对「按 `At` 排序 / 归并」的消费者会聚到 tick 0，
  看起来像「流首」。不阻塞，但 `ToString()`（`:171`）会打印出误导性的 `@ (tick 0, seq -1)`。
- **M-3 · `AccumulateCraftOutputs` 先豁免后校验**：`:585-586` 先把 `OutputInstanceIds` 全部塞进
  `craftOutputIds`，`:588` 才判三组不等长。畸形载荷因此**既**报 `MalformedPayloadPairing`
  **又**对残缺 id 集授予 R18 豁免。两者都进 `Gaps`（`IsComplete` 仍为 false），故只是
  缺口清单的冗余语义，不是错算。
- **M-4 · `Fold` 方法 171 行 / 24 个分支点，超出 coding-standards 的 40 行与复杂度 10**
  （`InventoryFold.cs:305-475`；实测 `awk` 计入 `case`/`if`/`for` 共 24）。
  该标准在本仓被普遍突破（`RecipeSettlementSolver.cs` 32 KB 级），且本方法呈**扁平 switch 分派**形态
  （每支独立、无深嵌套），可读性风险低于行数暗示。**登记不阻断**；若后续要给 `StructurePlaced`/
  `StructureRemoved` 补真实扣减，届时拆 `ApplyXxx` 每个 Kind 一支。
- **M-5 · 测试 XML 的 `asserts="0"`**：`unity/Logs/inv-fold.xml` 根元素 `asserts="0"`，而 28 条用例
  普遍含 2–7 条断言。不影响 `result="Passed"`，但该字段不可用于交叉核对「断言条数」。

---

## 三、缺口登记（不掩盖）

| # | 缺口 | 可跑命令 / 证据 | 建议归属 |
|---|---|---|---|
| **G-1** | `InventoryFold.Fold` **零生产调用点**；`IsComplete` 对任何真实流恒 `false` ⇒ `RequireComplete()` 必抛，契约 C-1/C-6 零兑现面 | 见 §2.1 B-1 的 `grep`（输出「none outside comments」）+ `grep -rn "Inventory" unity/Assets/Gameplay.Boot/*.cs`（零命中） | **新立登记项**（story「未落地项」表补第 5 行） |
| **G-2** | `ConflictingQualitySource` 分支零夹具零变异（真实流可达） | `grep -c "ConflictingQualitySource" <测试件>` = **0**；spec 内 = **0** | 本 story 补夹具 |
| **G-3** | `InvalidDespawnQuantity` 的 `p.Qty <= 0` 半边零夹具零变异 | `sed -n '384p;390p' InventoryFold.cs` 两个独立分支；spec `M9` 只打 `:390` | 本 story 补夹具或降 `Proven` |
| **G-4** | `MalformedPayloadPairing` 分支零夹具 —— **story 已自陈登记**（`Test Evidence` 已知边界 1），本轮**未推翻**：`:468` 那条件反证的是 codec 拒收，不是 fold 分支 | `inventory_fold_test.cs:481/489` 断的是 `_encoder.Encode` 抛 | 维持 `Proven = ✗`（story 已如实登记，无新增） |
| **G-5** | AC-20-05 的 IL 扫描面 = 1 个类型（`Sim/ItemDatabase/` 共 17 文件）；嵌套 lambda 类型落在扫描外 | `sed -n '567p' inventory_fold_test.cs`；`ls unity/Assets/Sim/ItemDatabase/*.cs \| wc -l` = 17 | 见 §2.2 W-1 |
| **G-6** | `NoOwner` 哨兵自指夹具（M-1） | 见 §2.3 M-1 | 本 story 补一条字面量断言 |
| **G-7** | 跨平台逐位（ADR-012）不在本条判据内 —— **story 已自陈**，本轮确认无遗漏主张 | story `Test Evidence` 已知边界 2 | 无 |
| **G-8** | `tools/falsify/baseline-invfold/` 仍**未跟踪**（story「计划外/待裁」已登记） | `git status --porcelain` 该路径为 `??`；`.gitignore` 窄 glob 只覆盖 `/fz-s19*/` | **待裁**（story 已登记，本轮仅确认仍开） |

**证伪轮本身的核查（本轮实际做了的）**：sha256 逐字节比对**成立**（见头部复算命令，两侧同为
`1c64d163…`）；spec 文件存在、`mutations` 14 条；M1/M3/M5/M8/M9 的 `expect` 文本与
`InventoryFold.cs` 现行行号（`:373` 附近 / `:363` / `:390`）**对得上**。
⚠️ **本轮未重跑** `python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s20-10-inventory-fold.json`
（见 §五），故「14/14 恰如预期」这一**执行面**结论本报告**不认领**，只认领「基线与实现逐字节相同」。

---

## 四、结论

- **判定：CHANGES REQUIRED**
- **一句话理由**：`InventoryFold` 本体在**可重建子域**内做得干净且诚实（真 codec、fail-closed null 解码器、
  显式 stable tiebreak、缺口带全序键定位、缺口不静默吞），2/4 记分方向与 AC-20-03/16 的「不勾」判断均**站得住**；
  但 **AC-20-05 的 `✅` 建立在一个只扫 1 个类型的判据上，而 AC 原文说的是「20 的代码路径」**
  （G-5），加上 **B-1「零生产调用点 + 对真实流恒不完整」完全未登记**、**B-2 一个真实可达的完整性分支零覆盖** ——
  这三条都属于本项目已烧过六次的「以更小的判据面顶替 AC 语义层」形态，不宜直接放行。

**必须修（转 APPROVED WITH SUGGESTIONS 的最小集）**：
1. G-1 进登记表（story「未落地项」补第 5 行 + 下游契约 C-1/C-6 标注「无接线，today 必抛」）。
2. W-1 二选一：放宽 IL 扫描面到命名空间前缀，**或**把 AC-20-05 的 `✅` 降级为「仅 `InventoryFold` 一类」。
3. B-2 补一条 `ConflictingQualitySource` 夹具（两采不同档 ⇒ 报缺口 + 首条胜出），并在补上之前标 `Proven = ✗`。

**建议修（不阻断）**：W-2（AC-20-17 措辞收窄到「零追加 Kind」面）· W-3/M-1（`p.Qty<=0` 分支与
`NoOwner != 0` 各补一条字面量夹具）· M-2～M-5 登记存档。

---

## 五、未及核查项（如实登记，禁读成已验）

1. **未重跑测试** —— 本轮为只读评审，未启动 Unity batch。28/28 的**唯一**依据是
   `unity/Logs/inv-fold.xml`（`start-time` = `end-time` = 2026-10-26 09:39:54Z，`duration` 0.296 s）。
   该 XML 属 story 自陈产物，本轮**未独立复现** ⇒ 「今天仍全绿」**未证**。
2. **未重跑证伪轮** —— 未执行 `tools/falsify/unity_falsify.py`。只核了基线与实现 sha256 相同（真做了）、
   spec 的 14 条与部分 `expect` 文本对得上（真做了）。「14/14 恰如预期」未复现。
3. **未做全量回归面勘察** —— story 自陈「纯新增文件、理论上零回归」，本轮未验证该前提仍然成立
   （`git status` 显示工作树有大量并行改动，故本轮**不**对回归面下任何结论）。
4. **GDD 全文未通读** —— 只读了与四条 AC 及 R2/R10/R16 直接相关的行（`:151 / :188 / :346 / :620 /
   :862 / :864 / :875 / :876 / :920 / :928 / :946`）。故「R15/R17/R18 的载荷缺位描述是否准确」
   **不在本报告判定面内**。
5. **`entities.yaml` 的 Kind 闭集未核** —— AC-20-17 的枚举面夹具判的是 `Enum.IsDefined`，
   与 `entities.yaml` 是否同步（ADR-024 A5 双向差集）本轮**未查**。
6. **跨平台（ADR-012）未涉及** —— 与本条判据面一致，非遗漏。

---

## 六、修复轮结账(2026-10-27 · Phase 8b 双列)

> **本节为**修复轮**追加,不改写 §一–§五 的任何原判定(「原文不删,就地加注」)。
> 用户裁定原话:「**不进21也不记债,现在完成**」+「**「装载库存实体」的链,现在建**」。

| ID | Landed(已落) | Proven(已证) | Branch(分支覆盖) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| **B-1** 零生产调用点 | ✅ | ✅ | — | `unity/Assets/Gameplay.Boot/InventoryLoadService.cs`(新建)`CompositionRoot.cs`(:381–407 构造 / :1149 入袋)`CompositionRootServices.cs`(新属性)`LoadSequencer.cs`(第 4 步钩子) | 证伪 M1(把 `InventoryFold.Fold` 的入参换成空数组)⇒ `test_rebuild_callsFold_producesInstancesFromTheStream` 等 **9 条红**;M4(停 `_afterRestore?.Invoke()`)⇒ `test_loadSequencer_afterRestoreHookRunsAndRebuildsProjection` 红 |
| **B-2** `ConflictingQualitySource` 零夹具 | ✅ | ✅ | ✅ 1/1 | `unity/Assets/Tests/EditMode/Inventory/inventory_fold_test.cs` | 新增 `test_conflictingQualitySource_raisesGapAndFirstSeenWins`(两采不同档 ⇒ 报缺口 + 首条胜出 = 2);反证:把 `:528` 的 `continue` 改成后写覆盖 ⇒ 该条红 |
| **W-1** IL 扫描面 = 1 类型 | ✅ | ✅ | — | `inventory_fold_test.cs` | 扫描面改为 `InventoryFold.cs` 的 **6 个声明类型** + 一条机械对账 `CollectionAssert.AreEquivalent(declared, FoldCodePathTypes)`(集合与源文件的 `class/struct/enum` 声明**逐一对应**,集合不能偷偷漂移) |
| **W-3/M-1** `p.Qty <= 0` 分支零夹具 | ✅ | ✅ | ✅ 2/2 | `inventory_fold_test.cs` | `test_despawnNonPositiveQty_raisesInvalidQuantity_andKeepsRow`(循环 `{0, -3}`) |
| **G-1**(缺口登记) | ✅ | — | — | 本节 + `story-001` 的 §未落地项 | 本表即登记;**下游契约 C-1/C-6 的「today 零兑现面」已被本轮的装载链部分兑现** —— 见下方诚实边界 |

### 诚实边界(禁借绿)

1. **`IsComplete` 在真实存档上仍恒 false** —— 本轮只接上了**调用链**,没有补 R16/R17/R18 的载荷载体
   (`StructurePlaced` 无 actor 位 / `StructureRemoved` 无返还物身份 / `Craft` 产出无 `item_key`)。
   `InventoryLoadResult.IsComplete` 的文档已明写此边界,**下游必须读它**再决定能否当完整库存用。
2. **「玩家看得到库存」未接线** —— 20b 呈现侧零改动。本轮兑现的是「投影在装配层被重建出来」,
   不是「UI 上显示得出来」。
3. **`BuildPartQualityGate` 的判据面 = 装载期**(不是 story 原文的「构建期」)—— 已在
   `InventoryLoadService.EnforceBuildPartQuality` 的 doc 与 story-003 就地登记。
4. **`ContainerWeightFold.FoldWeight`(S20-11 W-2)本轮仍未接线** —— 本服务用的是
   `CarryLoadSolver.Sum`(它内部走 `SumDetailed`,自带「空箱自重 + 子件逐件」口径),
   故 W-2 的**第二真源**性质未变,只是 `CarryLoadSolver` 这一侧有了生产调用点。

### 复算命令

```bash
# ① 三条孤儿函数现在各自有几个生产调用点(排除注释)
grep -rn "InventoryFold\.Fold(" unity/Assets --include=*.cs | grep -v /Tests/ | grep -v "^\S*: *//"
grep -rn "BuildPartQualityGate\.RequireValid(" unity/Assets --include=*.cs | grep -v /Tests/
grep -rn "CarryLoadSolver\.Sum(" unity/Assets --include=*.cs | grep -v /Tests/

# ② 本轮新增/修改件的清单
git status --porcelain unity/Assets/Gameplay.Boot unity/Assets/Gameplay.Presentation/DataCookedDisk.cs \
  unity/Assets/Tests/EditMode/Inventory

# ③ 夹具实测(含本轮 18 条新增)
#    total=178 passed=178 failed=0 inconclusive=0 skipped=0
```
