# processing-005 —— 完成调度 / cue 发射 / 溢出 18 侧（实现轮原件）

**日期**：2026-11-03
**Story**：`production/epics/processing/story-005-completion-schedule-spill-and-audio.md`
**Sprint**：24（S24-1）
**判据面**：`AC-18-21` [I]（BLOCKING）；其余四条 `[L]` **本轮未跑**

---

## 一、交付物（逐件 `file:line`）

| 件 | 作用 | 落点 |
|---|---|---|
| `FurnaceCompletionSchedule.CollectAt` | 「本 tick 哪些炉到点」的**纯函数**（AC-18-21 的调度落点） | `unity/Assets/Sim/Processing/FurnaceCompletionSchedule.cs` |
| `FurnaceCompletion` | 到点事实的形状（`ActorId` / `CompleteTick` / `ToolCell` / `OutputInstanceIds`） | 同上 |
| `ProcessingCompletionDriver.OnTickEdge` | 发射侧：到点 → `sfx_process_complete`（18 → 44 边界） | `unity/Assets/Gameplay.Boot/ProcessingCompletionDriver.cs` |
| 装配 | `services.ProcessingCompletion` 由 `Assemble` 构造；`BootRoot` 在 tick 边沿驱动 | `CompositionRoot.cs` · `CompositionRootServices.cs` · `BootRoot.cs` |
| 夹具 | 13 测（调度 6 · 发射 5 · 结构/扫描 2） | `unity/Assets/Tests/PlayMode/process_completion_and_spill_test.cs` |
| 18 既有扫描面加宽 | 新类型进 AC-18-13 的源码面 + IL 面 | `furnace_timing_no_process_state_test.cs` 的 `FurnaceSourceFiles` / `ProcessingTypeNames` |

## 二、判据口径（三条，均由夹具钉住）

1. **等号，不是区间**：`CompleteTick == tick`。M1 实测：改成 `>` ⇒ 3 条红。
2. **前缀与定序只用 envelope tick**（承 `FurnaceState` 的 2026-10-26 W1 订正）。M3 实测：改用载荷 `StartTick` ⇒ 1 条红。
3. **每 actor 至多一条**（全序键最小者胜）。M2 实测：改成后者覆盖 ⇒ 1 条红。

## 三、证伪轮（6 变异，全部命中，SHA-256 逐字节还原）

| 变异 | 改什么 | 期望红 | 实测 |
|---|---|---|---|
| M1 | 完成判据 `!=` → `>` | `test_ac1821_completionHitsExactlyOnEqualityTick` | ✅ 命中（该轮 3 红） |
| M2 | 每 actor 去重改为后者覆盖 | `test_ac1821_sameActorTwoFurnaces_keepsEarliestByOrdering` | ✅ 命中 |
| M3 | 前缀改用载荷 `StartTick` | `test_ac1821_prefixUsesEnvelopeTick_notPayloadStartTick` | ✅ 命中 |
| M4 | tick 单调守卫 `<=` → `<` | `test_ac1821_repeatedTickEdge_throwsInsteadOfDoubleEmitting` | ✅ 命中 |
| M5 | 未解析 cue 仍扫描并发射 | `test_ac1821_unresolvedCue_noEmission` | ✅ 命中 |
| M6 | 发射路径注入 `DateTime.UtcNow` | `test_ac1821_emissionPath_hasNoWallClockSymbols` | ✅ 命中 |

还原判据 = **SHA-256 逐字节**（不是 grep 探针串）：

```
（改前/还原后一致）
85112b98ef57ad0f1dc66e9d2c1232a045e150098dbf0e03c02cd6f279592b07  unity/Assets/Sim/Processing/FurnaceCompletionSchedule.cs
07cc22998baf7813a5ab398d318f05615bee2c7498a59d1566a54fa74c8fd927  unity/Assets/Gameplay.Boot/ProcessingCompletionDriver.cs
```

## 四、**未做**（禁借绿 —— 逐条写明为什么）

| 项 | 状态 | 理由（可复算） |
|---|---|---|
| `AC-18-09` 等待期声床听测 | **NOT-RUN** | 人听；且事件表**没有** `sfx_process_*` 三行 ⇒ 无素材可听 |
| `AC-18-22` 去标注盲测 | **NOT-RUN** | 同上 + 需 playtest 受试者 |
| `AC-18-24` 仅手柄全流程 | **NOT-RUN** | 依赖 42 面板与焦点导航落地 |
| `AC-18-08` 逐屏零数字走查 | **NOT-RUN** | 依赖 42 面板落地 |
| **溢出落地的「真 20 结算 + 真 fold」** | **NOT-RUN（新登记）** | 见 §五 |
| **「起货音已实现」** | **不成立** | 见 §五 |

## 五、两处必须如实登记的残余

### 5.1 生产上**不会出声**

`assets/data/audio_events.json` 的 19 行 cue 里**没有** `sfx_process_complete`（复算：
`grep -c "sfx_process" assets/data/audio_events.json` ⇒ `0`）。⇒ 装配期 `TryResolveCue`
解析失败 ⇒ 驱动拿到 `AudioEventDataSet.NoOrdinal` ⇒ `OnTickEdge` 首行返回，**不扫描、不发**。

⇒ 本轮交付的是**发射时机这一侧**（判据 + 条数 + 无墙钟），**不是**音效。加表行的那一轮
（44 的路线图）本驱动无需改动即生效。

### 5.2 溢出落地的下游**没有入口**

`AC-18-21` 的溢出半边要求「完成 tick 当场容量不足 ⇒ 本格 `DropSpawned`（id = 点火已铸）」。

实测：**20 侧不存在「完成时点的结算入口」**。`InventoryFold.ApplyCraftOutputs`
（`InventoryFold.cs:851-889`）把 `Craft` 的产出**直接建进炮制者名下**，与完成 tick 无关
⇒ 产出在点火那刻就已「入包」，`DropSpawned` 溢出发出者（= 20）**无调用点**。

⇒ 本轮的溢出交付只到 **18 侧**：`FurnaceCompletion` 原样携带点火已铸的
`OutputInstanceIds` 与 `ToolCell`（`test_ac1821_completionCarriesIgnitionCastIds_unchanged`），
并断「调度不可能自铸 id」（`test_ac1821_scheduler_cannotMintIds`，结构面）。
**落地半边登记为 `TD-processing-05`**（见该轮债册更新），不得记 ✅。

> 用 spy 顶替 fold 写一条绿夹具 = F1 型假绿（断言的是测试自己写的谓词）⇒ 本轮**不写**。

## 六、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① cue 名不在事件表(→ 生产不出声)
grep -c "sfx_process" assets/data/audio_events.json          # 期望 0
# ② 溢出落地无 20 侧入口:`DropSpawned` 的完成时点发出者
grep -rn "EventKind.DropSpawned" unity/Assets/Sim unity/Assets/Gameplay.Boot --include=*.cs \
  | grep -v "/Tests/" | grep -v "InventoryFold"               # 期望只剩非完成路径
# ③ 新类型已进 AC-18-13 扫描面
grep -n "FurnaceCompletionSchedule" unity/Assets/Tests/EditMode/Processing/furnace_timing_no_process_state_test.cs
# ④ 夹具存在
ls -l unity/Assets/Tests/PlayMode/process_completion_and_spill_test.cs
```
