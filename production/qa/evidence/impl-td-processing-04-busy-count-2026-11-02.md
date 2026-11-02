# 实现原件 —— `TD-processing-04` 收口:`BusyActorCount` 单遍扫描(判据仍唯一)

> **对象**:`docs/tech-debt-register.md` 的 `TD-processing-04`
> **批次**:`S23-21`(用户指令「按你的建议继续修」)· **截至时刻**:见 §四
> **commit**:本批未提交工作树(基于 `7b5bde91`)

---

## 一、病因(改前实测)

`BusyActorCount` 旧实现 = **扫一遍收 actor 集合 + 逐 actor 重扫全流**:

```csharp
var actors = new HashSet<int>();
for (...) { if (e.Kind != Craft) continue; actors.Add(Decode<CraftPayload>(...).ActorId); }
foreach (int actor in actors) if (IsBusy(events, decoder, actor, tick)) busy++;
```

⇒ 每条 `Craft` 被 `Decode` **A 次**(A = actor 数),总解码量 `O(A×E)`。

**债册的硬要求逐字**:「**必须保持 `IsBusy` 为唯一判据** —— 可让单遍扫描**产出**
`IsBusy` 所需的中间量,而不是另写一份判据」。

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Sim/Processing/FurnaceState.cs` | `BusyActorCount` 改为**单遍**:一次遍历算出每 actor 的 `FurnaceOccupancy`,再逐 actor 走 **`IsBusyCore`** |
| 2 | 同上 | 新增 **`private static bool IsBusyCore(in FurnaceOccupancy, long)`** —— 「忙」的**唯一谓词核**;`IsBusy` 与 `BusyActorCount` 都调它 |
| 3 | `Tests/EditMode/Processing/furnace_busy_count_equivalence_test.cs` | **新增** 4 条判据 |

**为何抽 `IsBusyCore`**:`IsBusy` 原本写 `tick < occ.CompleteTick`。单遍扫描若也自己写一遍,
**两处判据迟早在某次修订里分叉**(债册点名的「第二真源」形态)。抽成一处后,
改「完成当拍忙不忙」的口径只需改一个 `<`。

**两处行为细节刻意与 `TryGetLatest` 逐条对齐**(否则两条路径会在边界分叉):
① 时长域先验(`DurationTicks < 1` ⇒ 抛)**不论是否落在前缀内**;
② 前缀与定序都只用 **envelope `e.Tick`**,全序键取最大(W1 订正的口径)。

---

## 三、判据(4 条,逐条可证伪)

| # | 判据 | 位置 |
|---|---|---|
| ① | **等价性**(主判据):4 条 actor × 10 个 tick(含 `tick == CompleteTick` 边界)⇒ `BusyActorCount` 逐点等于「逐 actor 问 `IsBusy`」 | `test_tdp04_busyCount_equalsPerActorIsBusy_atEveryTick` |
| ② | 前缀外的坏时长(`duration_ticks = 0`)仍须抛(两路径同语义) | `test_tdp04_zeroDuration_beyondPrefix_stillFailsLoud` |
| ③ | 同 tick 两条 `Craft`(不同 `Seq`)⇒ 取**全序键更大**者,与 `IsBusy` 一致 | `test_tdp04_sameTickTwoCrafts_bothPathsPickLargestOrderKey` |
| ④ | 全流混入非 `Craft` ⇒ 计数只认 `Craft` | `test_tdp04_nonCraftEvents_areIgnoredByBusyCount` |

---

## 四、证伪轮

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 单遍里写 `Exists && tick <= CompleteTick`(口径分叉 `<=` vs `<`) | **1 红**:等价性在 `tick 140`(= actor 1 的 `CompleteTick`)处红 | **exact** |
| **M2** | 时长校验挪到前缀判断**之后** | **1 红**:`zeroDuration_beyondPrefix_stillFailsLoud` | **exact** |
| **M3** | 取「最后遇到者」(不比较全序键) | **1 红**:`sameTickTwoCrafts_bothPathsPickLargestOrderKey` | **exact** |
| **M4** | 不筛 `Kind`(任意事件都当 `Craft` 解码) | **1 红**:`nonCraftEvents_areIgnoredByBusyCount`(解码抛) | **exact** |

**结果:4/4 转红,4/4 SHA-256 逐字节还原**(每条 `RESTORE-OK (sha=188f983b…)` +
`FINAL-OK sha 一致`,收尾 `grep MUTATION` = 0)。

⚠️ **M1 首轮是假存活(4 条全绿)**:原变异「把 `IsBusyCore(...)` 换成
`tick < CompleteTick`(去掉 `Exists` 守卫)」在**该调用点完全等价** ——
`latest` 里存的都已是 `Exists=true` 的实例(只在前缀内且过时长校验才入库)
⇒ **F8「行为等价掩盖结构分叉」**。改靶为「让**口径**真的分叉」(`<=` vs `<`)后转红。
**首轮读数作废,不引用**。

---

## 五、回归

| 套 | 读数 | 文件 |
|---|---|---|
| **EditMode 全量** | **`6598 / 6561 / 0 / 0 / 37`** @ `2026-11-02 13:45:37 CST` | `unity/Logs/tdp04-em-full.xml` |
| **PlayMode 全量** | **`299 / 295 / 0 / 2 / 2`** @ `2026-11-02 13:55:43 CST` | `unity/Logs/tdp04-pm-full.xml` |
| **本件过滤** | `4 / 4 / 0 / 0 / 0` @ `2026-11-02 13:38:14 CST` | `unity/Logs/tdp04b.xml` |

**等式**:`6598 − 6594 = 4` = 本件新增 4 条 `[Test]`(基线 `6594/6557/0/0/37` = 上一批 `S23-20`)。
`passed` 同步 `6557 → 6561`。**PlayMode `+0`**,`failed 0`。

⚠️ **`PM exit=2` 是本仓 PlayMode 常态退出码**(承 `S23-18` 的记录)⇒ 判据取 XML 的 `failed` 计数。

---

## 六、证明不了的事(禁借绿)

1. **加速幅度未测** —— 债册原文自陈「分配量为推算,**未用 Profiler 实测**」。本件钉的是
   **等价性**(修法的正确性前提),不是「快了多少」。
2. **零生产调用方未变** —— `BusyActorCount` 在 `Tests/` 外仍零命中。⇒ 本轮的收益**今日为零**,
   是「接线前的预防」;真正兑现要等 18 的 Craft 落流接线轮。
3. **`FurnaceOccupancy` 的字典分配** —— 单遍扫描引入一个 `Dictionary<int, FurnaceOccupancy>`
   + 一个 `List<int>`,而旧实现有一个 `HashSet<int>`。**分配量未必减少**(可能略增),
   减少的是**解码次数**(`O(A×E) → O(E)`)与**扫描遍数**(`A+1 → 1`)。这一点如实登记:
   债名写的是「O(A×E) 分配」,而实际优化的是**解码 + 遍历**,不是分配块数。

---

## 七、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | 夹具首轮 4 处编译错:参数名写成 `inputIds` / `consumed` / `outQty` / `outIds`,真名是 `inputInstanceIds` / `actualConsumed` / `outputQty` / `outputInstanceIds` | 按 `WorldPayloads.cs:98-100` 逐字订正 |
| 2 | **M1 首轮假存活**(4 条全绿):原变异「把 `IsBusyCore(...)` 换成 `tick < CompleteTick`(去掉 `Exists` 守卫)」在**该调用点完全等价** —— `latest` 里存的都已是 `Exists=true` 的实例 ⇒ F8「行为等价掩盖结构分叉」 | 改靶:让**口径**真的分叉(`<=` vs `<`),等价性夹具在 `tick == CompleteTick` 处才红 |
