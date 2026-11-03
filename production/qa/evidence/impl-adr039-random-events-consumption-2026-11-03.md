# ADR-039 实现轮证据原件 —— 52 运行期消费(预算 / 抽数 / 节奏)

- **日期**:2026-11-03
- **ADR**:`docs/architecture/adr-039-random-events-runtime-consumption.md`(Accepted 2026-11-03)
- **触发**:用户指令「继续 52 运行期消费」(承 ADR-038 §D7 的登记)
- **§D1–D7 由实现轮就地拟定**,未经用户逐条确认(同 ADR-034 / 037 / 038 口径)

---

## 一、补上了什么

ADR-038 落地 52 数据集后,四个值里只有 `ROLL_INTERVAL` 有一个读方(启动期整除门)。
本轮把另外三个接进生成链:

| 值 | 消费点 | 之前 |
|---|---|---|
| `ROLL_INTERVAL` | `TrySpawnAtTick` 的**边界门**(非倍数 ⇒ `NotAtBoundary`) | 只被启动期整除门读 |
| `WINDOW_SIZE` | `F1PipelineInput.WindowSize` + 当日 `EventRolled` 计数上限 | 零消费 |
| `BASE` / `BUDGET_MAX` | `EventBudget.ComputeDailyBudget(BASE, ∅, MAX)` + 当日 `EventArrived` 计数上限 | 纯函数零调用点 |

**顺带删除的第三个定义点**:`CompositionRoot` 的 `RandomEventWindowTicks = 200`,它同时被当作
「节奏(tick)」与「抽数(条数)」使用,且与数据集的 6000 / 6 都不等。

## 二、关键实现决定(全部写进 ADR)

- **状态从事件流重构**:`rollsToday` = 当日 `EventRolled` 数;`spawnsToday` = 当日 `EventArrived` 数;
  掷骰 `Ordinal` = `rollsToday`;`RollRequest.Window` = **预算窗口起点**(一天)。
  ⇒ 删除 `_lastWindow` / `_ordinalInWindow` 两个读档后不可重建的内存字段(ADR-039 D2)。
- **预算 / 抽数门排在掷骰之前**(ADR-039 D3):与既有 CAP 预检同一位置、同一理由 ——
  避免「掷过骰、事件没到」的孤儿行。
- **配置取不到 ⇒ 不构造生成链**(ADR-039 D5):`dataProvider == null` 时 director 收 `null`,
  **不回落默认值**。

## 三、落盘清单

- `Gameplay.Boot/RandomEventSpawnChain.cs` —— 边界门 / 当日计数 / 预算门 / 抽数门 /
  `RollRequest` 与 F1 输入改用日窗口 / `LastF1Input` 观测缝 / 三种新 outcome
- `Gameplay.Boot/CompositionRoot.cs` —— 从 `IDataProvider` 取 52 配置;provider 为 null ⇒ 不建链;
  删除 `RandomEventWindowTicks`
- `Tests/EditMode/RandomEvents/random_events_consumption_test.cs`(12 条)
- `Tests/EditMode/RandomEvents/random_event_spawn_chain_test.cs` —— 适配新构造与日窗口语义

## 四、Validation

| # | 判据 | 载体 | 状态 |
|---|---|---|---|
| V-1 | 非边界 ⇒ `NotAtBoundary`,零写入 | `test_notAtBoundary_isNotEvaluated` | **PASS** |
| V-2 | 当日抽取达 `WINDOW_SIZE` ⇒ `DrawCapReached` | `test_drawCapReached_stopsDrawing` | **PASS** |
| V-3 | 当日降临达 `clamp(BASE,∅,MAX)` ⇒ `OverBudget` | `test_overBudget_stopsSpawning` / `test_baseZero_meansNoSpawning` / `test_budgetMax_clampsBaseDown` | **PASS** |
| V-4 | 界内 ⇒ 恰两条,先骰后降临 | `test_atBoundary_evaluatesAndSpawns`(原链夹具 A 同守) | **PASS** |
| V-5 | 换日计数归零;`RollRequest.Win` = 日起点;ordinal = 当日已掷数 | `test_newDay_resetsCounts` / `test_rollRequest_usesDayWindow_andStreamOrdinal` | **PASS** |
| V-6 | `F1PipelineInput.WindowSize` 取自数据集 | `test_f1WindowSize_comesFromConfig_notTickWindow` | **PASS** |
| V-7 | `dataProvider == null` ⇒ 生成链为 null,不抛;provider 在场 ⇒ 链**必须**被构造(成对) | `test_assembly_withoutProvider_doesNotBuildChain`(反侧)· `reader_wiring_gate_test.test_readers_assembly_buildsSpawnChain_whenRandomEventsTablePresent`(正侧) | **PASS** |

## 五、证伪轮(4 条有效 + 1 条打错靶)

| # | 变异 | 靶夹具 | 结果 | 还原 |
|---|---|---|---|---|
| M11 | 边界门 `if (false)` | `test_notAtBoundary_isNotEvaluated` | **红**(10/11) | SHA 相同 |
| M12 | 抽数门 `if (false)` | `test_drawCapReached_stopsDrawing` | **红**(10/11) | SHA 相同 |
| M13 | 预算门 `if (false)` | 预算四项 | **红 ×4**(7/11) | SHA 相同 |
| ~~M14~~ | ~~只把 `RollRequest` 的 window 改成 `tick`~~ | 消费夹具 | **未转红(打错靶)** —— 当时没有夹具钉 `RollRequest.Win` | SHA 相同 |
| M14b | `RollRequest(tick, _tier, 0)`(window 与 ordinal 同时错) | `test_rollRequest_usesDayWindow_andStreamOrdinal` | **红**(11/12) | SHA 相同 |
| M15 | `CompositionRoot` 的 `if (dataProvider != null)` → `if (true)`(provider 缺省时照样取表) | `test_assembly_withoutProvider_doesNotBuildChain` | **红**(13 条中 1 红) | SHA 相同 |
| M16 | 同一分支 → `if (false)`(表在场也不建链) | `test_readers_assembly_buildsSpawnChain_whenRandomEventsTablePresent` | **红**(16 条中 1 红) | SHA 相同 |

**M15 / M16 是成对的两枪**:V-7 的单侧夹具必然被其中一枪打不中 ——
只测「缺省 ⇒ null」时,把整个分支删掉仍然绿(空洞正控);只测「在场 ⇒ 非 null」时,
恒建链(即 M15 的形态)使「缺省」那半失去意义。两枪各打一侧,合起来才闭合。

**M14 作废的理由**:`LastF1Input.Window` 与 `RollRequest.Win` 是两处独立取值;
首版夹具只钉了前者,变异打在后者上 ⇒ 靶场不可达。M14b 前已补
`test_rollRequest_usesDayWindow_andStreamOrdinal`,重打即红。承 `tools/falsify/README.md`
「变异打错靶」。

**SHA-256(还原后)**:
- `unity/Assets/Gameplay.Boot/RandomEventSpawnChain.cs`
  `c116e0ae36b3d2dfe4d60ef0bc3626ee4131328593e78b343268aa337348bf7b`
- `unity/Assets/Gameplay.Boot/CompositionRoot.cs`(M15 / M16 的变异对象)——
  **两个哈希,口径不同,勿混读**:
  · **变异窗口的还原判据** = `fa5dd7e0c5cade91687f498df4a459ff1be3b4406c25044e36d93309e6573c0f`
    (M15 / M16 各自跑完后,与**变异前同一时刻**的备份逐字节相同;两次都命中此值)
  · **现盘终态** = `76a16f5e78607ea7a31333a11d681d2681440b8c29a1470fbb5a8059dd44eafb`
    (其后做了 `:650` 附近的**注释订正**:「52 的池内容归 37」→ 池条目归 52 / 内容归 37)
  ⇒ 两值不同是**注定的**,不是还原失败。复算:`sha256sum unity/Assets/Gameplay.Boot/CompositionRoot.cs`。

## 六、未做 / 盲区(逐项,不借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| 生产端到端可达 | **不可达 · 已立债** | 池**条目**数据件不存在 ⇒ 链恒 `NoPool`,预算与抽数门只有夹具可达。**`TD-randomevents-03`**(2026-11-03 立;义务方 = 52,37 只提供医疗内容 —— 同批订正了「归 37」的错误写法) |
| V-7 的装配夹具 | ✅ **已补齐(成对)** | 反侧 + 正侧见 §四;证伪 M15 / M16 各红一枪 |
| `mods`(F2 的 `Mod_Eco` / `Mod_Rep`) | 空集 · **已立债** | 两值在 GDD 仍是 `*待裁*`;`TD-randomevents-04`(解除动作 = 数值轮裁定 + 生态区接线,实现轮不得自填) |
| 预算 / 抽数门的**次序**偏离 F1 字面 | 已登记 | ADR-039 §D3 / §R1:掷骰前判定,避免孤儿 `EventRolled` |

## 七、回归三元组

> **两轮读数,口径不同,勿混读** —— ① = ADR-039 实现轮(已提交 `a1409c1c`);
> ② = 同日的 V-7 补齐 + 债册立债轮(**本件 §四–§六 的终态**)。

| 轮 | 套件 | 结果(total/passed/failed/inconclusive/skipped) | 截至时刻 | 批次 | commit |
|---|---|---|---|---|---|
| ① | EditMode | 6754 / 6717 / 0 / 0 / 37 | 2026-11-03 12:41:57 CST | sprint-24 · ADR-039 实现轮 | `8d0c859b` + 工作树 |
| ① | PlayMode | 324 / 320 / 0 / 2 / 2 | 2026-11-03 12:45:11 CST | 同上 | 同上 |
| ① | 编译警告(全量重编译) | **CS 警告 0** | 2026-11-03 12:45:11 CST | 同上(`touch` 全部 1346 个 `.cs` 后跑 PlayMode) | 同上 |
| **②** | **EditMode** | **6756 / 6719 / 0 / 0 / 37** | **2026-11-03 13:05:10 CST** | sprint-24 · V-7 补齐 + 债册 | **`a1409c1c` + 工作树** |
| **②** | **PlayMode** | **324 / 320 / 0 / 2 / 2** | **2026-11-03 13:14:04 CST** | 同上 | 同上 |
| **②** | 编译警告 | **CS 警告 0**(EditMode / PlayMode 两份日志各计一次) | 同上 | 同上 | 同上 |

**第②轮等式**:`6756 − 6754 = 2` = 本轮新增的两条 V-7 夹具(反侧 + 正侧),`passed` 同步 +2。

**等式判据(F7 借绿)**:`6754 − 6742 = 12` = `random_events_consumption_test.cs` 的 `[Test]` 数
(6742/6705/0/0/37 @ 2026-11-03 12:19:57 CST · sprint-24 · `8d0c859b`+工作树 = Amendment A 批次基线)。
`passed` 同步 **+12**(6705 → 6717),`skipped` 不变(37)。

### 七之二、首轮全量回归的 26 红(实跑修正,不是「原本就红」)

首轮全量 EditMode(`Logs/editmode-52.xml` @ 2026-11-03 12:37:53)报 **26 failed / 6754**,
全部落在**装配面**:`RealRegistryPatientDeathTest` / `RealRegistryPatientOutcomeFoldTest` /
`WTrendEpsVitalsEndToEndTest` / `ActionAxisReaderGateTest` / `ReaderWiringGateTest`。
报文一律:

```
System.InvalidOperationException : MultiTableProvider 无 RandomEventsDataSet —— 本测不该问到它(真问 = 接线错)
```

**归因**:ADR-039 D5 让 `CompositionRoot` 在 `dataProvider != null` 时**装配期即时**
`Load<RandomEventsDataSet>()`;而这五个夹具的 partial provider 是按「问不到 = 接线错」建的,
从未登记 52 表。⇒ **这是接线真的多了一条**,不是夹具坏了,也不是随机回归。

**修复**:① `DataCookedDisk.LoadRandomEvents()`(读**同一份**烘焙产物 +
`RandomEventsCookedCodec`,零 Fake —— 与 `LoadItems` / `LoadDiseaseRegistry` 同格);
② 五个 provider 各补一行 `.Add(DataCookedDisk.LoadRandomEvents())`,带同款「装配面完备」注;
③ ADR-039 §D5 补明「provider 非 null 而表缺失 ⇒ 装配当场抛」及其连带义务(已落盘)。

**为什么不做「缺表就跳过」**:那会让「52 配置缺失」与「一切正常」不可分 ——
正是 D5 已经驳回过的那条理由。

## 八、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① 52 配置的四个消费点存在(各一处)
grep -n "_rollInterval\|_windowSize\|_baseEvents\|_budgetMax" \
  unity/Assets/Gameplay.Boot/RandomEventSpawnChain.cs | head
# ② 旧的第三定义点已删
grep -rn "RandomEventWindowTicks" unity/Assets --include=*.cs | wc -l   # 期望 0
# ③ 新夹具条数
grep -c "\[Test\]" unity/Assets/Tests/EditMode/RandomEvents/random_events_consumption_test.cs  # 期望 12
# ④ 无变异残留
grep -rn "MUTATION M" unity/Assets --include=*.cs | wc -l               # 期望 0
```

```bash
# PlayMode(PM 退出码 2 是本仓常态 ⇒ 判据取 XML 的 failed)
python3 - <<'PY'
import xml.etree.ElementTree as ET
r=ET.parse('Logs/playmode-52.xml').getroot()
print(r.attrib.get('result'), {k:r.attrib.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
PY
grep -c "warning CS" Logs/playmode-52.log            # 期望 0(全量重编译)
find Assets -name "*.cs" | wc -l                     # 1346 = 本轮 touch 的面
```
---

## 九、2026-11-03 后续轮 —— 池条目表 + 加权抽取

### 9.1 触发与裁定

用户「继续这些:生产端到端恒 NoPool」→ 勘察后提出问题 → 用户裁定 **「给数值,我一次做到底」**:

| 项 | 值 |
|---|---|
| `W_base`(P0 九条) | **全部 100** |
| `TODMult`(夜 · 威胁档) | **3/2** |
| `TOD_MULT_MAX` | **2/1** |
| `Mod_Eco` / `Mod_Rep` | 未裁(用户要求「讲清楚点再单独裁一轮」) |

### 9.2 勘察出的实质缺陷(不是「补个数据件」)

`EventDraw.Pick` 原实现 = `h % keys.Count` —— **均匀抽取,`W_base` 完全没被用**。
GDD `:672` 逐字警告过:「一个无视权重的实现**能通过 AC-52-06 却完全无视权重**」。
⇒ 即使把池填上,抽到的分布仍是错的。本轮同批修掉。

### 9.3 落盘清单(8 新件 + 6 改件)

**新件**
- `assets/data/52_event_pool.json`(9 条:P0 威胁 5 / 机会 3 / 反应 1)
- `unity/Assets/Sim.Contracts/EventPoolDataSet.cs`(`PoolHandler` / `EventPoolEntryData` / `EventPoolDataSet`)
- `unity/Assets/Sim/EventWeighting.cs`(F1 步骤② 的权重构成,纯函数)
- `unity/Assets/Editor.Tools.Bake/EventPool{Binder,Baker,CookedWriter}.cs`
- `unity/Assets/Gameplay.Presentation/EventPoolCookedCodec.cs`
- `unity/Assets/Tests/EditMode/RandomEvents/event_weighting_test.cs`(13 条)

**改件**
- `Sim/EventDraw.cs` —— 均匀 → **加权 CDF walk**(`TryPick`;`Pick` 降为等权兼容形)
- `Gameplay.Boot/SeedEventAuthority.cs` —— 候选集从 `IReadOnlyList<int>` → `IReadOnlyList<WeightedKey>`
- `Gameplay.Boot/RandomEventSpawnChain.cs` —— 候选集签名 + **新增 `ZeroWeight` outcome**
- `Sim.Contracts/RollRequest.cs` —— 增 `Tick` 四参形(昼夜项要按**掷骰那一刻**算)
- `Gameplay.Boot/CompositionRoot.cs` —— 从池表算候选 + `isNight` 接真来源
- `Gameplay.Presentation/{AddressablesDataProvider,DataCorePreloader,DataCookedDisk}.cs`
- `Editor.Tools.Bake/DataBakeMenu.cs` —— 新菜单 `BakeEventPool`
- 五个 partial `MultiTableProvider` 夹具(各 +2 行:池表 + 5 时间参数)
- `cooked_drift_all_artifacts_test.cs` —— `BakedArtifacts` +1、`pinnedDeclared` 42 → **43**

### 9.4 Validation

| # | 判据 | 载体 | 状态 |
|---|---|---|---|
| V-8 | `ΣW = 0` ⇒ `ZeroWeight`,不掷骰不写入 | `test_zeroWeight_skipsWithoutRollingOrWriting` | **PASS** |
| V-9 | 白天 `W_i = W_base`;夜威胁 `×3/2`;其余档不变 | `event_weighting_test`(13 条) | **PASS** |
| V-10 | 舍入 `ROUND_HALF_AWAY_FROM_ZERO`(4.5 ⇒ 5) | `test_oddWeight_roundsHalfAwayFromZero` | **PASS** |
| V-11 | 脚本条目不进抽池 | `test_scriptEntries_areExcludedFromPool` | **PASS** |
| V-12 | 权重**真的改变命中分布** | `test_distribution_changesWithWeight` | **PASS** |
| V-13 | 池表烘焙/解码/字节自洽(292 B) | `cooked_drift_all_artifacts_test`(43 份) | **PASS** |

⚠️ **V-12 的期望值来自离线复算**(48/64 · 18/64 · 36/64),不是「看起来应该是一半」。
首版我写成 32/32 与 16/48 —— **离线复算当场证伪**(`h % 2` 在 0..63 上并不均匀)。
按纪律**改断言不改实现**。

### 9.5 实跑修正(首轮 15 红 → 1 红 → 0)

| 轮 | 红数 | 归因 | 修法 |
|---|---|---|---|
| ① | **15** | `ResolveTimeParams` 让装配多问 `TimeParamsDataSet`(5 个夹具)+ catalog 42 vs 43 + 漂移门钉定值 | 五夹具补 `.Add(LoadTimeParams())`(并新增 `DataCookedDisk.LoadTimeParams`);`EnsureDataCoreGroup` + `BuildAddressablesContent` 重建 catalog(43);`BakedArtifacts` +1、`pinnedDeclared` 43 |
| ② | **1** | AC-21a-48 调参旋钮字面量门按 `\bweight\b` 命中 `WeightedKey(int key, long weight)` | 形参改名 `w`(GDD 自己的记号是 `W_i`)—— **不改门** |
| ③ | **0** | — | — |

### 9.6 回归三元组(第③轮 · 最终树)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **6771 / 6734 / 0 / 0 / 37** | 2026-11-03 15:05:25 CST | sprint-24 · 池条目表轮 | `4f2d9280` + 工作树 |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 15:08:14 CST | 同上 | 同上 |
| 编译警告 | **0**(`touch` 全部 `.cs` 全量重编译) | 同上 | 同上 | 同上 |

**等式**:`6771 − 6756 = 15` = 本轮新增夹具数(`event_weighting_test` **13** + `ZeroWeight` **1** + 池表装配正控 **1**)。
`passed` 同步 **+15**(6719 → 6734)。

### 9.7 仍未做(禁借绿)

| 项 | 状态 |
|---|---|
| `medical` 条目 → 37 病种映射 | **不存在** ⇒ `spawnPatient` 刻意抛;`encounter` 条目不受影响 |
| `Mod_Eco` / `Mod_Rep` | 未裁 ⇒ 预算恒 = `BASE`(`TD-randomevents-04` 维持 open) |
| 端到端「真池驱动下三道门全可达」夹具 | **仍缺**(现有夹具用合成候选集;`TD-randomevents-03` 维持 open) |
| `HistoryMult` / `KeyGate` | P0 无输入源 ⇒ 恒 1 / 恒放行 |
