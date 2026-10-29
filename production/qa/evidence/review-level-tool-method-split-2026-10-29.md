# 代码评审原件 —— `Editor.Tools.Level` 方法拆分轮(`5599f630` + `246e83fd`)

> **对象**:两次**纯重构**提交
> - `5599f630` —— `LevelConsistencyCheck.Check` 163 行 → 11 个方法
> - `246e83fd` —— 全树再拆 **19 个** >40 行方法(9 文件,含两个构造器)
>
> **日期**:2026-10-29 · **模式**:lean(`/code-review`,双专家并行)
> **评审人**:`unity-specialist`(等价性 / 引擎 / C#)· `qa-tester`(可测试性 / 判别力)
> **裁定**:⚠️ **CHANGES REQUIRED —— 2 条 BLOCKING(1 条真·可观测行为变更)+ 1 MINOR**
> **本件的证据强度**:两条 BLOCKING 均由**编排方独立复核**(读源码 + 实跑),不是照抄专家结论。

---

## §一 BLOCKING

### B-1 · 单个槽位内**判据次序**变了(可观测)

**落点**:`unity/Assets/Editor.Tools.Level/LevelConsistencyCheck.cs` 的 `CheckSlots`

| | 次序 |
|---|---|
| 拆分前(`77090ee3`) | ① 重复格 → **② 类型越界** → ③ 越界 → ③之补 不可走 → ④ → ⑤ |
| 拆分后 | ① → **③ → ③之补 → ②** → ④ → ⑤ |

**成因**:首版拆成 `CheckSlotCell(①③③之补) → CheckSlotType(②) → CheckSlotInitialModule(④⑤)`,
把 ② 挤到了 ③ 之后。

**为什么可观测**:`CheckReport.Violations` 是 `IReadOnlyList<string>`,`LevelCiRunner.Render`
的 `AppendList` 与 `CheckOrThrow` 的异常报文本都**按列表序**输出。同一槽位同时踩 ② 与 ③
时(`slot_type = None` + 格越界 —— 改尺寸后忘了挪槽位、类型也没填,作者期完全可能),
两条报文互换位置。

**为什么全量回归照不出来**:`grep -rln "Violations\[0\]\|Violations\[1\]" unity/Assets/Tests/`
**零命中**;**没有任何夹具构造「一行两个错」**。⇒ 上一轮 commit message 里
「零可观测行为变更」这句话,对 `LevelConsistencyCheck` 的拆分**不成立**。

**两位专家独立命中**:`unity-specialist` R-1(BLOCKING)· `qa-tester` Q3 第 4 条(WARNING)
—— 同一处,不同角度,分级不同(见 §三)。

### B-2 · `TerrainApiSpike.ReadBackAsset` 元组返回吞掉中间状态

**落点**:`TerrainApiSpike.cs` 的 `EmitC_AssetRoundtrip` / `ReadBackAsset`

| | 行为 |
|---|---|
| 原实现 | **渐进赋值**:`loadedOk = reloaded != null;` 落定后,才 `fromAsset = InvokeSample(...)` |
| 拆后 | 四值合成**一次**元组返回 `return (true, InvokeSample(...), ...)` |

**差异**:`AssetDatabase.LoadAssetAtPath` 成功但 `InvokeSample`(反射调 `GetHeight`)抛异常时,
原实现留下 `loadedOk = true` / `assetResolution = 真实分辨率`;新实现整条赋值都不发生
⇒ `loadedOk = false` / `assetResolution = -1` / `assetSize = default`。spike 日志的**数值面**随之改变。

**可达性**:仅异常路径。同一次 spike 里前序 ProbeA 已成功调过 `GetHeight`,生产上极少触发
—— 但它改的是**输出数值**,不是隐形差异,所以按 BLOCKING 处理而非 WARNING。

---

## §二 MINOR

| ID | 内容 | 落点 | 处置 |
|---|---|---|---|
| **M-1** | `DestroyImmediate` 销毁次序对调(ProbeA: `probe` 先于 `td`;ProbeB: `td` 先于 CC 段) | `TerrainApiSpike.cs` | **不可观测**(两个 `TerrainData` 互不引用),**不改代码**,只在本件登记 —— 本批以「逐字相同」为卖点,这不算逐字相同 |
| **M-2** | `TerrainSource` 用 6 处字面量 `"rows"` 代替 `nameof(rows)` | `TerrainSource.cs` | 已修:把 `nameof(rows)` 当**参数**传进 helper(见 §八 F-3),消除「重命名形参 ⇒ `ParamName` 静默漂移」 |

---

## §三 两位专家独立命中的同一件事(交叉验证)

| 结论 | `unity-specialist` | `qa-tester` | 编排方复核 |
|---|---|---|---|
| **单槽位内 ② 与 ③ 次序变了** | **R-1 · BLOCKING** | **Q3-4 · WARNING** | ✅ 属实(对读 `77090ee3` 与当前 `CheckSlots`),**已修 + 已补夹具** |
| 新夹具只覆盖「正溢出」,漏 clamp / 负坐标绕圈 | — | **Q2** | ✅ 属实,已补第 2 条夹具(§八 F-4) |
| `"rows"` 字面量是维护风险 | — | **Q3-5** | ✅ 属实,已改为参数传入(§八 F-3) |
| 元组返回吞中间状态 | **R-6 · BLOCKING** | — | ✅ 属实,已改回渐进赋值(§八 F-2) |

> **分级分歧的处置**:同一处 defect,`unity-specialist` 判 BLOCKING、`qa-tester` 判 WARNING。
> 我按**更严的一方**处理(修 + 补夹具),理由:分级分歧本身说明判据面不足,
> 而补一条夹具的成本远低于日后被它咬。

---

## §四 OK(已核,明确没问题)

`unity-specialist` R-2…R-14 与 `qa-tester` Q1 / Q4 / Q5 逐条核对,摘要:

- **`warnings` 常驻化不会 NRE** —— `NavMeshConsistencyCheck.cs:74` / `LayerDriftCheck.cs:51`
  都在任何 return 之前 `warnings = new List<string>()`。
- **`CopyWalkable` 双循环合单循环等价** —— 异常仍停在首个非法下标,类型与 `ParamName("walkable")` 不变。
- **`LevelSource` 六段校验次序未变**,`nameof` 参数名逐个同名 ⇒ `.NET` 的 `(Parameter 'x')` 尾部不变。
- **`LevelCiRunner.AppendList` 输出逐字节等价**;空表只有 Violations 打 `  ✓ 无硬失败`。
- **`NavChunkSource` / `LayerDriftCheck` / `ColliderFootprintCheck` / `EcozoneSource` 的拆分等价**,
  `MaxReported` 预算仍由同一个 `violations` 列表跨段共享,方向 ① 仍先于方向 ②。
- **程序集边界未动**:`git diff --stat 77090ee3..246e83fd -- '*.asmdef'` 零行;无 `Sim` → 工具反向边。
- **手动探针仍不进自动测试路径**:`NavMeshProbe` / `TerrainApiSpike` 仅 `[MenuItem]`,
  两文件零 `[Test]`,`grep -rn "TerrainApiSpike\|NavMeshProbe" unity/Assets/Tests/ tests/` 零命中。
- **既有 AC 的判别力未丢失**:`NotRun` 三态、异常类型、报文关键词全部保留。

---

## §五 未及核查项(禁借绿)

1. 两位专家**均未运行任何测试/构建**;其报告中的回归数**未复现**。本件的回归数以 §八 的**实跑**为准。
2. `TerrainApiSpike` 其余 `Emit` 行(Steepness / C 段 7 行)只按 diff 上下文判断次序未变,**未逐条比对原文**。
3. `LevelExporter` 的 203 行 diff 采用抽样阅读,`AppendTerrainTable` 与 `ExportTerrain` 的括号边界未通读。
4. 「全树 168 个方法、最长 40 行」由编排方复算(§八 F-5),专家未复算。
5. **`TerrainApiSpike` 是勘察工具,不是判据**;其日志行序无任何回归夹具钉住 ——
   本件只能证明「按 diff 逐条对照未发现次序变化」,不能证明「一定没有」。

---

## §七 结论

**CHANGES REQUIRED。** 拆分的**手法**是对的(17 个方法严格等价),问题集中在**两处自认为等价、
实际不等价**的地方 —— 一处在**报文次序**,一处在**异常路径的中间状态**。
两者都属「回归全绿也照不出来」的形态,正是纯重构最需要单独评审的理由。

> ⚠️ **时点关系(如实登记)**:本评审在「纯重构已完成并提交」**之后**执行。
> 因此 `5599f630` / `246e83fd` 两条 commit message 里的「零可观测行为变更」在评审时点**不成立**;
> 现已修,修后该措辞才成立(见 §八)。

---

## §八 修复轮结账表(Landed / Proven / Branch)

| ID | Landed | Proven | Branch | File(s) | Proven-by(可复算) |
|---|---|---|---|---|---|
| **B-1** 单槽位内 ② 与 ③ 次序 | ✅ | ✅ | ✅ 2/2 | `LevelConsistencyCheck.cs` 的 `CheckSlots` 拆成 `CheckSlotDuplicate` / `CheckSlotType` / `CheckSlotBounds`,调用序恢复 ①②③③之补④⑤ | **F-4 变异**:把 `CheckSlotType` 挪回 `CheckSlotBounds` 之后 ⇒ 该类 **`12 / 11 / 1`**,唯一红 = `test_levelTool_slotWithBothTypeAndBoundsErrors_reportsTypeBeforeBounds`。还原 SHA-256 `a4834c7…77cc` 逐字节一致 |
| **B-2** 元组吞中间状态 | ✅ | ✅ | ✅ 2/2 | `TerrainApiSpike.cs`:`ReadBackAsset` → `CreateAndReloadTerrainAsset`(只回 `TerrainData`),读值由调用方**逐句**赋 | **无专打变异** —— 只在异常路径可达,而该路径需要 `InvokeSample` 抛异常,盘上无此夹具。**按构造证明**(逐句次序 = 原文)。如实登记为「已落但仅读代码可证」 |
| **M-1** DestroyImmediate 次序 | ✗(不改) | — | — | `TerrainApiSpike.cs` | **刻意不改**:两个 `TerrainData` 互不引用,无副作用。登记于此,不假装「逐字相同」 |
| **M-2** `"rows"` 字面量 | ✅ | ✅ | ✅ 1/1 | `TerrainSource.cs`:`ValidateOneRow` 增 `string paramName` 形参,调用点传 `nameof(rows)` | **构造性**:代码内字面量已零出现 —— `grep -n '"rows"' TerrainSource.cs \| grep -v '<param' \| wc -l` ⇒ **0**(唯一剩余命中是 XML doc 的 `<param name="rows">`,不是代码);`ParamName` 值不变 |
| **F-4** 新夹具:裸索引会命中合法可走格 | ✅ | ✅ | — | `level_tool_consistency_test.cs`:`…_naiveIndexHitsWalkableCell_addsOnlyTheBoundsViolation` | **变异**:删 ③ 早退 ⇒ 该类 **`11 / 9 / 2`**,两条越界夹具同时红(一条抛异常、一条多出「不可通行」) |

### 8.1 证伪总账

| 变异 | 靶 | 预判 | 实得 |
|---|---|---|---|
| 删 ③ 早退 | `level_tool_consistency_test` 全类 | 两条越界夹具红 | **`11 / 9 / 2`** —— 两条都红 ✅ |
| ②/③ 次序对调 | 同上 | 只有次序夹具红 | **`12 / 11 / 1`** —— 唯一红即它 ✅ |

两次还原均以 **SHA-256 逐字节**判据(`a4834c71f3176fd20235ea24453e4a9181e36cf2b5effcb2d8656f93785477cc`)。

### 8.2 回归(三元组:截至时刻 · 批次 · commit)

| 套件 | 结果 | 对照 |
|---|---|---|
| 编译 | **0 error** | — |
| EditMode 全量 | **`5852 / 5815 / 0 / 0 / 37`** @ 2026-10-29 08:17 CST · 修复+评审轮 · `246e83fd`+工作树 | 评审前 `5850 / 5813 / 0 / 37` ⇒ **+2 = 新补的两条夹具**;等式 `5852 − 5850 = 2` |
| LevelTool 子集 | `127 / 125 / 0 / 2` | 评审前 `126 / 124 / 0 / 2` ⇒ **+1**;次序夹具加入后该类 `12 / 12 / 0` |
| PlayMode | 未跑 | 本轮只动 `Editor.Tools.Level`(editor-only 程序集)+ EditMode 夹具,PlayMode 面不受影响;如需可单独补跑 —— **如实登记为 `NOT-RUN`,不写"应无影响"冒充结果** |

### 8.3 修复后仍**未结**的项(如实登记)

1. **B-2 无专打变异** —— 它的正确性靠「逐句次序与原文相同」这一构造性论证,
   **没有夹具能复现异常路径**。记账为 `Landed ✅ / Proven 部分`。
2. **`MaxReported` 截断行为、`OrderSlots` 全序比较器**均**无夹具**
   (`grep "MaxReported" unity/Assets/Tests/` 零命中)—— 这不是本轮引入的,是既有缺口,
   但本轮把它们搬进了新方法,所以在此点名。
3. **`TerrainApiSpike` 日志行序无机器判据**(spike 工具的固有性质)。
