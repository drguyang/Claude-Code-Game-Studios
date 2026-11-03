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
| V-7 | `dataProvider == null` ⇒ 生成链为 null,不抛 | **NOT-RUN** —— 见 §六 | **NOT-RUN** |

## 五、证伪轮(4 条有效 + 1 条打错靶)

| # | 变异 | 靶夹具 | 结果 | 还原 |
|---|---|---|---|---|
| M11 | 边界门 `if (false)` | `test_notAtBoundary_isNotEvaluated` | **红**(10/11) | SHA 相同 |
| M12 | 抽数门 `if (false)` | `test_drawCapReached_stopsDrawing` | **红**(10/11) | SHA 相同 |
| M13 | 预算门 `if (false)` | 预算四项 | **红 ×4**(7/11) | SHA 相同 |
| ~~M14~~ | ~~只把 `RollRequest` 的 window 改成 `tick`~~ | 消费夹具 | **未转红(打错靶)** —— 当时没有夹具钉 `RollRequest.Win` | SHA 相同 |
| M14b | `RollRequest(tick, _tier, 0)`(window 与 ordinal 同时错) | `test_rollRequest_usesDayWindow_andStreamOrdinal` | **红**(11/12) | SHA 相同 |

**M14 作废的理由**:`LastF1Input.Window` 与 `RollRequest.Win` 是两处独立取值;
首版夹具只钉了前者,变异打在后者上 ⇒ 靶场不可达。M14b 前已补
`test_rollRequest_usesDayWindow_andStreamOrdinal`,重打即红。承 `tools/falsify/README.md`
「变异打错靶」。

**SHA-256(还原后)**:`unity/Assets/Gameplay.Boot/RandomEventSpawnChain.cs`
`c116e0ae36b3d2dfe4d60ef0bc3626ee4131328593e78b343268aa337348bf7b`。

## 六、未做 / 盲区(逐项,不借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| 生产端到端可达 | **不可达** | 候选池为空(37 / 27 内容未注入)⇒ 链恒 `NoPool`,预算与抽数门只有夹具可达(ADR-039 §R3) |
| V-7 的 `dataProvider == null` 夹具 | **NOT-RUN** | 该行在 `CompositionRoot.Assemble` 内,需整机装配;现有 `composition_root_test` 未断言生成链指针 |
| `mods`(F2 的 `Mod_Eco` / `Mod_Rep`) | 空集 | P0 无输入源;`ComputeDailyBudget(BASE, null, MAX)`(ADR-039 D4) |
| 预算 / 抽数门的**次序**偏离 F1 字面 | 已登记 | ADR-039 §D3 / §R1:掷骰前判定,避免孤儿 `EventRolled` |

## 七、回归三元组

| 套件 | 结果(total/passed/failed/inconclusive/skipped) | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **6754 / 6717 / 0 / 0 / 37** | 2026-11-03 12:41:57 CST | sprint-24 · ADR-039 实现轮 | `8d0c859b` + 工作树 |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 12:45:11 CST | 同上 | 同上 |
| 编译警告(全量重编译) | **CS 警告 0** | 2026-11-03 12:45:11 CST | 同上(`touch` 全部 1346 个 `.cs` 后跑 PlayMode) | 同上 |

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
