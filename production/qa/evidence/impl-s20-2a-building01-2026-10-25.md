# 实现证据件 —— S20-2a(`TD-building-01` 两半全补)

> **日期**:2026-10-25 · **批次**:sprint-20 · **工作项**:`S20-2a`
> **工作树**:commit `84a85e0` + 未提交的 S20-2a 改动(生产码 4 件 + 测试 2 新 + 4 迁移)
> **证据原件 = 本文件**:这是**实现证据**,不是评审件。
> 🔁 **2026-10-27 订正(承 QA 签核 C-5)**:原句尾注「承 `coding-standards.md` §Review Evidence Standards」
> **措辞失当** —— 该条款管的是**评审报告原件**,本件是**实现证据**(修复证据),两者义务不同
> (「对账件/修复证据不可替代原件」是同一条款自己写的)。
> **本轮未走 `/code-review`,故不主张任何评审背书** —— 如实:本件 = 实现证据,**未评审**。
> **债面**:`docs/tech-debt-register.md` §`TD-building-01`(状态维持 `open`,结案判据见 §七)

---

## 一、改动清单

### 1.1 生产码(前一位实现者已改完,**本件未回退、未重写**)

| 文件 | 改动 | 性质 |
|---|---|---|
| `unity/Assets/Sim/World/PlaceableChecker.cs:145` | `ModuleDefinition` 五字段 → **六字段**,新增 `public readonly int Cost`;构造形参 `int cost`(`:158`)位于 `validOrientations` 之后、`refundable` 之前,**无默认值**(静默默认 0 = 静默失败) | (b) 契约面 |
| `unity/Assets/Sim/World/DemolishChecker.cs:86` | 新增实例方法 `CalculateRefund(long structureId, int demolishRefundRatio)` —— cost 从 `_moduleCatalog.GetModuleDefinition(...)` 读;结构不存在 ⇒ 0;模块缺行 ⇒ 0(`:92` fail-closed) | (b) 读路径 |
| `unity/Assets/Sim/World/DemolishChecker.cs:102` | 新增 `ValidateRefundable(int moduleId, int demolishRefundRatio)` —— 同上 fail-closed(`:105`) | (b) 读路径 |
| `unity/Assets/Sim/World/DemolishChecker.cs:118` | 原静态 `RefundCalculator` **保留为纯公式核**(其 doc 已就地声明「生产读入口 = 实例方法,本静态式的 cost 形参只服务公式边界测试」) | 边界澄清 |
| `unity/Assets/Gameplay.Presentation/BuildModulesCatalog.cs:124` | `cost: row.Cost` —— cooked 行 → `ModuleDefinition.Cost` 的生产映射 | (b) 装配面 |
| `unity/Assets/Gameplay.Presentation/BuildModulesCookedCodec.cs:149-152` | 解码 `cost` 字段并透传 | (b) 装配面 |

**9 处 `new ModuleDefinition(` 构造点全部补 `cost:` 形参**(编译期强制,无默认值兜底)。

### 1.2 测试(本件新增 / 迁移)

| 文件 | 改动 | 性质 |
|---|---|---|
| `unity/Assets/Tests/EditMode/ModularBuilding/world_end_to_end_test.cs` | **新增**,8 测 —— (a) 判据载体 | 本件交付主体 |
| `unity/Assets/Tests/EditMode/ModularBuilding/demolish_refund_from_catalog_test.cs` | **新增**,12 测 —— (b) 判据载体 | 本件交付主体 |
| 上述两件的 `.cs.meta` | Unity batch 首跑自动生成 | 派生物(入库) |
| `demolish_checker_test.cs` / `modifiable_checker_test.cs` / `placeable_checker_test.cs` / `structure_kinds_test.cs` / `structure_occupancy_wiring_test.cs` / `structure_payload_encoder_test.cs` | 9 处 `new ModuleDefinition(` 补 `cost:` 形参(纯机械迁移,**断言语义零改动**) | 编译适配 |

---

## 二、(a) 真 `World` 端到端 —— 判据逐条落点

**为什么 S17-6 的 e2e 不算**:`Tests/EditMode/ClinicMachine/clinic_room_matching_e2e_test.cs:19-24`
自陈「本文件**未**经 `World` …… `IsEffectivelyWalkable` 的端到端判据**不在本文件面内**,**未验**」——
它持的是裸 `StructureInstanceRegistry`。本件的 `WorldEndToEndTest` 把 **`World` 实例本身**
传进 `StructureWriter` 的 `IWorldOccupancy` 位(`world_end_to_end_test.cs:86`),
使「`Place` → 真足迹 → `World.OccupyCells`(`StructureKinds.cs:238`)→ `IsEffectivelyWalkable`
(`World.cs:95`)」在**同一个真 `World` 实例**上闭环。

| 债面判据(逐字) | 载体 | 结果 |
|---|---|---|
| **`Place → IsEffectivelyWalkable == false`** | `world_end_to_end_test.cs:112` `test_world_e2e_place_blocksAnchorAndNonAnchorFootprint`(锚点 + **非锚点足迹格**双腿;并断言 `OccupiedCount == 2`) | ✅ 已证 |
| **`Remove → true`** | `world_end_to_end_test.cs:148` `test_world_e2e_remove_restoresAnchorAndNonAnchorFootprint`(双腿恢复 + `OccupiedCount == 0`) | ✅ 已证 |

**判别力配套(防「恒 false / 恒 true」冒充判据)**:

| 测试 | 钉的属性 |
|---|---|
| `:97` `test_world_e2e_beforePlace_anchorAndFootprintWalkable` | 前置:未放置时两格**可走** ⇒ 排除「本就可走」被误读成 false 的来源 |
| `:132` `test_world_e2e_place_leavesUnrelatedCellsWalkable` | 一次 `Place` 不得 block 全图 ⇒ 排除「整体 block」骗过 `OccupiedCount` |
| `:169` `test_world_e2e_placeRemovePlace_roundTripIsStable` | 放→拆→再放;`structure_id` 不复用、占用格数不累加 ⇒ 排除 `FreeCells` 只清一次 |
| `:215` `test_world_e2e_withoutOccupancyWiring_walkableUnchanged` | **对照组**:同一 `World`、同一目录,写者 occupancy 位换成不接线替身 ⇒ 仍 `true` ⇒ **证明 §1 的 false 确由 `World.OccupyCells` 产生** |
| `:241` `test_world_e2e_realBakedCatalog_surgeryTableBlocksBothCells` | 真 `build_modules.cooked` 的 module 2(两格足迹)全链 ⇒ 生产数据真被吸收 |
| `:189` `test_world_e2e_getModuleAt_reflectsPlaceAndRemove` | 同一链的旁证:`World.GetModuleAt` 经真注册表报出 / 清空 |

---

## 三、(b) 目录读路径 —— 判据逐条落点

**字面量神谕**:期望值全部写成手算整数(`cost=4` / `R=1.0` ⇒ `4`),**不**写
`RefundCalculator.CalculateRefund(def.Cost, r)` —— 后者是自指断言(本项目已抓过多次的假绿形态)。

| 判据 | 载体(`demolish_refund_from_catalog_test.cs`) | 结果 |
|---|---|---|
| ① 正常路径 = 目录 cost × ratio | `:75` `test_calculateRefund_ratioOne_returnsCatalogCost`(cost=4, R=1.0 ⇒ **4**)<br>`:90` `test_calculateRefund_ratioHalf_floorsCatalogCost`(cost=9, R=0.5 ⇒ **4**,向下取整)<br>`:104` `test_calculateRefund_twoModules_differByCatalogRow`(4 vs 9,差 = 5) | ✅ 已证 |
| ② 结构不存在 ⇒ 0 | `:124` `test_calculateRefund_unknownStructure_returnsZero` | ✅ 已证 |
| ③ 模块不在目录 ⇒ 0(fail-closed) | `:136` `test_calculateRefund_moduleNotInCatalog_returnsZero`<br>`:153` `test_calculateRefund_emptyCatalog_returnsZeroNotThrow`(整个目录为空 ⇒ 不抛、返 0) | ✅ 已证 |
| ④ `ValidateRefundable(moduleId, ratio)`:模块不在目录 ⇒ false | `:198` `test_validateRefundable_moduleNotInCatalog_returnsFalse`<br>`:205` `test_validateRefundable_emptyCatalog_returnsFalse` | ✅ 已证 |
| ⑤ `ValidateRefundable` 阈值真用目录 cost | `:170` `test_validateRefundable_ratioOne_passesForCatalogRows`<br>`:178` `test_validateRefundable_ratioHalf_thresholdFromCatalogCost`(cost=1 在 R=0.5 下 false、R=1 下 true ⇒ 排除恒定 false) | ✅ 已证 |
| ⑥ 生产烘焙目录真的被读到 | `:223` `test_calculateRefund_realBakedCatalog_usesAuthoredCost`(module 1 ⇒ **4**;module 2 ⇒ **8**,字面量来自 `assets/data/build_modules.json`)<br>`:242` `test_calculateRefund_realBakedCatalog_shellIsNotRefundable`(module 8 ⇒ **1**) | ✅ 已证 |

**生产树调用点**:`DemolishChecker.CalculateRefund` / `ValidateRefundable` 的**实例方法**即生产读入口;
`RefundCalculator` 的静态式自此**只剩公式边界测试**调用(`refund_calculator_test.cs` 5 测),
不再是生产路径的一部分 —— 该事实由 `DemolishChecker.cs:112-117` 的 doc 明写。
⚠️ **诚实登记**:`DemolishChecker.CalculateRefund` 的**生产发起方**(谁在拆除流程里调它)
= 42 的建造交互(`O-23-6`,写侧),**不在本件面内**(见 §六)。

---

## 四、S12-4 证伪轮(四支,一属性一条变异)

**纪律**:每支 = 声明变异窗口 + 点名被变异文件 + 锚串恰命中 1(`count==1` 断言在 `falsify.py` 内)
+ 窗口内不读不编辑被变异文件 + `cp` 备份还原(禁 `git checkout` / `git stash`)
+ **还原判据 = SHA-256 逐字节** + 还原后 `grep FALSIFY` 残留 = 0。
驱动 = `python3 /home/gu/.claude/jobs/181888b1/tmp/falsify.py`,每次 `FILTER=DaYiJingCheng.Tests.ModularBuilding`,
输出覆盖 `tmp/falsify.xml`,RED 按 XML `result != 'Passed'` 过滤(Skipped 不计)。

| # | 属性 | 被变异文件 | 锚串 | 变异 | 结果 |
|---|---|---|---|---|---|
| **F1** | (b) 目录缺行 **fail-closed** | `unity/Assets/Sim/World/DemolishChecker.cs` | `if (!defOpt.HasValue)\n                return 0;` | 删掉该守卫(落到公式值路径) | ✅ **2 红**:`…moduleNotInCatalog_returnsZero` · `…emptyCatalog_returnsZeroNotThrow`;其余 90 绿 |
| **F2** | (a) `World` 的 Overlay 合成项 | `unity/Assets/Sim/World/World.cs` | `return terrain.Walkable && !_slotOccupied[idx];` | 改为 `return terrain.Walkable;`(去掉占用项) | ✅ **7 红**,含 `WorldEndToEndTest` 的 place / remove / roundTrip / realBaked 四测 + `WorldTest` 三测(既有);其余 85 绿 |
| **F3** | (a) `Place` 写**全足迹** | `unity/Assets/Sim/World/StructureKinds.cs` | `            _occupancy.OccupyCells(occupied);` | 改为只写 `{ anchor }`(C2 退化) | ✅ **5 红**,含 `test_world_e2e_place_blocksAnchorAndNonAnchorFootprint`(非锚点腿)+ `StructureOccupancyWiringTest.test_c2_place_writesOccupancy`;其余 87 绿 |
| **F4** | (b) cost **从目录读**(非硬编码) | `unity/Assets/Sim/World/DemolishChecker.cs` | `return RefundCalculator.CalculateRefund(defOpt.Value.Cost, demolishRefundRatio);` | 改为 `CalculateRefund(1, demolishRefundRatio)`(cost 硬编码 1) | ✅ **4 红**:`ratioOne` · `ratioHalf` · `twoModulesDiffer` · `realBakedCatalog_usesAuthoredCost`;其余 88 绿 |

**SHA-256 还原判据(四支共用三文件)**:

```
de950d170a12d25c9f5abf092c1ae9de9215641387e961289c43559f0092704e  Sim/World/DemolishChecker.cs
96ec0ad261c2cc1f7fe7cf04459c050c0bffbd90b63523022424629e94ca1d6b  Sim/World/World.cs
e49caf689bdb3e09f57de6f3cb7d6ce1205202569738ae4f45f5d890b007bf42  Sim/World/StructureKinds.cs
```

四支跑完后的 `sha256sum` 与 `tmp/falsify-bak/` 备份**逐字节相同**;
`grep -c "FALSIFY"` 于三文件 = `0 / 0 / 0`(零残留)。

**一属性一条变异**:F1 钉 fail-closed、F2 钉 Overlay 合成、F3 钉足迹写全、F4 钉 cost 来源 —— 四条互不叠加,
删任一条即该面零覆盖。

---

## 五、回归三元组 + 增量等式

**基线**(**引用自** `unity/Logs/s20-1-full-regression.xml` @ 2026-10-25 · S20-1 收口轮 · `a3242b6` 工作树):

| 截至时刻 | 批次 | commit / 工作树 | total | passed | failed | skipped |
|---|---|---|---|---|---|---|
| 2026-10-25(S20-1 收口) | sprint-20 / S20-1 | `a3242b6` + S20-1 未提交件 | **5045** | 5010 | 0 | 35 |

**本轮**(`unity/Logs/s20-2a-full.xml` @ 2026-10-25 · S20-2a · `84a85e0` + 本件未提交件):

| 截至时刻 | 批次 | commit / 工作树 | total | passed | failed | skipped |
|---|---|---|---|---|---|---|
| 2026-10-25 | sprint-20 / S20-2a | `84a85e0` + S20-2a 未提交件 | **5065** | 5030 | 0 | 35 |

**增量等式**:

```
total:   5065 − 5045 = 20   ==  新增测试数 20   ✅
passed:  5030 − 5010 = 20   ==  新增测试数 20   ✅
failed:     0 −    0 =  0                        ✅
skipped:   35 −   35 =  0                        ✅
```

**新增 20 = 8(`WorldEndToEndTest`)+ 12(`DemolishRefundFromCatalogTest`)**,
逐条 `fullname` 差集比对:**新增 20 · 消失 0**(零测试被改名 / 删除 / 静默跳过)。

**中间轮**(`unity/Logs/s20-2a-modular.xml`,`-testFilter DaYiJingCheng.Tests.ModularBuilding`):
**92 / 92 / 0 / 0**(S20-2a 前该过滤器 = 72 / 72 / 0 / 0,增量 20 同源)。

**复算命令(自包含)**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for p in ('unity/Logs/s20-1-full-regression.xml','unity/Logs/s20-2a-full.xml'):
    r=ET.parse(p).getroot()
    print(p, {k:r.get(k) for k in ('total','passed','failed','skipped')})
a={tc.get('fullname') for tc in ET.parse('unity/Logs/s20-1-full-regression.xml').getroot().iter('test-case')}
b={tc.get('fullname') for tc in ET.parse('unity/Logs/s20-2a-full.xml').getroot().iter('test-case')}
print('新增',len(b-a),'消失',len(a-b))
PY
```

**预期输出**:两行 `total/passed/failed/skipped` 分别 `5045/5010/0/35` 与 `5065/5030/0/35`,末行 `新增 20 消失 0`。

---

## 六、NOT-RUN 清单(禁借绿)

| # | 项 | 为什么 NOT-RUN | 归属 |
|---|---|---|---|
| **N1** | **写侧**(`Place` / `Remove` 的**生产发起方**) | 归 42 的建造交互(`O-23-6`,`modular-building.md:494`,状态「⏳ 待 42 下一轮」)。`TD-building-01` 与 `sprint-20.md` 均逐字排除本件面内;**本件不为它发明 UI 发起方** | 42 |
| **N2** | `DemolishChecker.CalculateRefund` 的**生产调用方** | 实例方法已就位、判据已证「cost 从目录读」,但**拆除流程本身**(谁在运行时调它、失败走 `DropSpawned`)是写侧的一环 ⇒ 同 N1。本件只证**读路径**接通,不证**被谁读** | 42(`O-23-6`) |
| **N3** | `AC-24-11` 布局源走真 `World` | 归 **`clinic-005` / `S20-2` 本体**(`sprint-20.md:28`),不在本件。本件为它提供前提(真 `World` 端到端可用),但**不代它记绿** | S20-2 |
| **N4** | `AC-23-08` 的「`refundable ⇒ cost ≥ ⌈1/R⌉`」全表校验 | 本件证的是**校验函数能从目录读 cost 并正确判**(§三 ⑤);**对真目录 9 行逐行跑校验**未做 —— 且 `R = 1` 下该式恒真(债册已登记),**无可证伪性** | 数值轮 / 后续 |
| **N5** | 生产装配根(`CompositionRoot` 里 `new World(`) | `TD-building-01` 的**另一层**(四件构造点生产树为零)。本件只交「经真 `World` 的端到端判据」,**未**在 `CompositionRoot` 里构造 `World` —— 那需要 ADR-023 三场景制的场景装配轮 | 后续装配轮 |
| **N6** | `BuildModulesCatalog.ValidOrientations = {0}` 的限制 | 作者态 `build_modules.json` 无该字段(文件头 `:24-28` 已如实登记)。本件不改 `assets/data/` 作者态 | `TD-itemdb-01` 同族 |

---

## 七、`TD-itemdb-01` 消费半边 —— **判定**(只判不回写)

| 半边 | 结案判据(`TD-itemdb-01` 解除动作 ②) | 本件状态 | 判定 |
|---|---|---|---|
| `ModuleDefinition` 补 `Cost` | 六字段含 `Cost` | `PlaceableChecker.cs:145` ✅ | **可转结** |
| `DemolishChecker` 从目录读 | 实例方法从 `_moduleCatalog` 读,不由调用方喂 | `DemolishChecker.cs:86` / `:102` ✅ | **可转结** |
| 生产树调用点 | 「`CalculateRefund` 的生产树调用点 = 零」这条实测项 | 实例方法 = 生产读入口,**但它的调用方仍归 42 写侧**(N2) | ⚠️ **部分** |

⇒ **判定**:`TD-itemdb-01` 的**消费半边**(「那笔数据没有读方」)**在契约与读路径层面已解除** ——
`cost` 现在有真读方(`DemolishChecker` 的实例方法 + `BuildModulesCatalog` 的映射),
且该读方经真烘焙产物验证(§三 ⑥)。
**但**「读方在**运行时被谁调用**」仍指向 `O-23-6` 写侧 ⇒ 若债册口径要求「生产调用链完整」才转结,
则该条应转 **`partial → covered` 但注明写侧依赖**;若口径只要求「读路径存在且被验证」,则**可直接转结**。
**本件不替债册做这个口径选择** —— 两条口径的差别记在 N2。**未回写债册**(按任务边界)。

---

## 八、surface 事项

1. **`TD-building-01` 的状态**:结案判据 ((a) + (b)) 两半**均已落齐并证伪**,
   按债册 `:3250` 的逐字判据,该条**已满足结案条件**。但**债册未回写**(任务边界)。
   回写时须一并处置 N5(生产装配根另属一层)与 §七 的口径选择。
2. **`clinic_room_matching_e2e_test.cs:19-24` 的「未验」注记**:该注记现在**过时** ——
   `World` 侧端到端已由本件 `world_end_to_end_test.cs` 覆盖。是否就地订正该注记,
   归 `clinic-005` / S20-2 轮决定(本件不动 clinic 侧文件)。
3. **`WorldTest` 与本件的重叠**:`world_test.cs` 的 `test_effectiveWalkable_occupied` 直调
   `World.OccupyCell`(单元级);本件经 `StructureWriter.Place` 的真足迹(**集成级**)。两者**不同面**,不重复。

---

## 九、自包含验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity

# 0) 前置:确认无活 batch(有则先报告,禁 kill、禁并行起第二个)
pgrep -af "6000.3.24f1" | grep -v VBCSCompiler; ls Temp/UnityLockfile 2>/dev/null

# 1) 本件夹具(预期 92 / 92 / 0 / 0)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults Logs/s20-2a-modular.xml \
  -testFilter DaYiJingCheng.Tests.ModularBuilding

# 2) 全量回归(预期 5065 / 5030 / 0 / 35)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults Logs/s20-2a-full.xml

# 3) 证伪轮(例:F4 cost 来源;FILTER 必设)
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
FILTER="DaYiJingCheng.Tests.ModularBuilding" python3 \
  /home/gu/.claude/jobs/181888b1/tmp/falsify.py \
  unity/Assets/Sim/World/DemolishChecker.cs \
  'return RefundCalculator.CalculateRefund(defOpt.Value.Cost, demolishRefundRatio);' \
  'return RefundCalculator.CalculateRefund(1, demolishRefundRatio);' F4_cost_from_catalog
# 预期:4 红(ratioOne / ratioHalf / twoModulesDiffer / realBakedCatalog_usesAuthoredCost)+ 还原 SHA-256 一致

# 4) 还原核验(预期三行与 §四 的哈希逐字节相同)
sha256sum unity/Assets/Sim/World/DemolishChecker.cs \
          unity/Assets/Sim/World/World.cs \
          unity/Assets/Sim/World/StructureKinds.cs
grep -c "FALSIFY" unity/Assets/Sim/World/DemolishChecker.cs \
                  unity/Assets/Sim/World/World.cs \
                  unity/Assets/Sim/World/StructureKinds.cs   # 预期 0 0 0
```

> ⚠️ batch 跑完可重开 Unity 编辑器(`unity/Temp/UnityLockfile` 已释放)。
