# `TD-processing-08` —— 20 侧完成时点结算（起货溢出）实现轮原件

**日期**：2026-11-03
**债**：`docs/tech-debt-register.md` · `TD-processing-08`
**裁定**：用户 2026-11-03 选 **甲**（`Fold` 加 `asOfTick` + 完成时点结算器）
**解掉的两半**：① 产出在**点火**即入包（`CarryLoad` 提前计入、可提前使用）；
② 溢出路径**没有宾语**（20 侧不存在完成时点的结算入口）

---

## 一、交付物（逐件 `file:line`）

| 件 | 作用 | 落点 |
|---|---|---|
| `InventoryFoldContext.AsOfTick` | **投影切点**（必填）——`Craft` 产出只在 `AsOfTick ≥ CompleteTick` 时材化 | `unity/Assets/Sim/ItemDatabase/InventoryFold.cs` |
| `ApplyCraftOutputs` 的切点门 | 未到完成 tick ⇒ 直接 return（**先于查表**，不误报配方缺项） | 同上 |
| `droppedIds` 预处理 | 已被 `DropSpawned` 点名的 id **不**由 Craft 建持有行（否则撞 `DuplicateInstanceId`，且成品同时在背包与地面） | 同上 |
| 产出**品级**预处理 | 落地行的品级来源 = 点火那条 `Craft.OutputQuality[]`（`DropSpawnedPayload` 不携带品级） | 同上 |
| `CraftCompletionSettler` | 完成时点结算：容量裁决 → 装不下则**整批** `DropSpawned` | `unity/Assets/Sim/ItemDatabase/CraftCompletionSettler.cs` |
| 装配 | `CompositionRoot` 构造 → `CompositionRootServices.CraftCompletionSettler` → `BootRoot` tick 边沿 | 三处 |
| 夹具 | 8 测 | `unity/Assets/Tests/EditMode/Inventory/craft_completion_settler_test.cs` |

## 二、判据口径

1. **可见性**：`AsOfTick < CompleteTick` ⇒ 产出**不在**投影里；`== CompleteTick` ⇒ 在场（与 AC-18-21 的「当拍」同口径）。
2. **落地是整批**：20 规则六的原子单位 = 「炮制 = 一次结算」⇒ 一批装不下 ⇒ **整批在地上**，不做「一半入包一半落地」。
   ⚠️ 因此 GDD 里「溢出量的选栈归 20 的确定性选栈」在本裁下**退化**（不存在「部分溢出」这个量）。改回部分容纳只需改 `SettleOne` 一处。
3. **无第二铸造点**：落地 id **原样引用** `Craft.OutputInstanceIds[]`（走 `RecordSpawnedWithId`，**不走** `Spawn`）。
4. **判据不重写**：「哪些炉到点」仍只有 `FurnaceCompletionSchedule.CollectAt` 一处（18 交付），本类**调它**。

## 三、证伪轮（6 变异，全部命中，SHA-256 逐字节还原）

| 变异 | 改什么 | 期望红 | 实测 |
|---|---|---|---|
| N1 | 切点门失效（`< completeTick` → `< 0`） | `test_td08_foldHidesOutputsBeforeCompleteTick` | ✅ 命中 |
| N2 | 落地 id 仍建持有行 | `test_td08_afterDrop_foldPutsBatchOnGround_noGaps` | ✅ 命中 |
| N3 | 容量裁决恒判装得下 | `test_td08_capacityInsufficient_emitsWholeBatchWithIgnitionIds` | ✅ 命中（该轮 3 红） |
| N4 | 去掉「已落地」幂等 | `test_td08_alreadyDropped_noSecondDrop` | ✅ 命中 |
| N5 | 入库前载重未扣本批 | `test_td08_capacityCountsExistingLoad` | ✅ 命中 |
| N6 | 落地改铸新号 | `test_td08_capacityInsufficient_emitsWholeBatchWithIgnitionIds` | ✅ 命中（该轮 3 红） |

还原判据 = **SHA-256 逐字节**：

```
（改前 = 还原后）
e8e6998647d4724c1f134b46663efff51d309953fa87566fb6f51ecf9eeb0a01  unity/Assets/Sim/ItemDatabase/InventoryFold.cs
c53c0efe15375b75d79b61d00ecb570f0926dec21b1ed7490889b353388ab752  unity/Assets/Sim/ItemDatabase/CraftCompletionSettler.cs
```

## 四、实现期抓到的两处自己的缺陷（如实登记）

1. **`_capacity` 忘了赋值** —— 构造器收 `capacity` 却没写回字段 ⇒ `default(CarryCapacity).Cap = 0` ⇒
   **每次起货都判装不下、全部落地**。首轮夹具 4 红把它照出来了。
   ⇒ 这不只是笔误：`CarryCapacity` 的构造期守卫**挡不住 `default(struct)`**（F9 形态）。
   已在 ctor 内**再验一次** `Cap <= 0 ⇒ throw`，并在代码里写明「本行不是冗余」。
2. **落地行缺品级来源** —— `DropSpawnedPayload` 不携带品级 ⇒ fold 报
   `InstanceWithoutQualitySource`。修法 = 预扫描 `Craft.OutputQuality[]` 登记品级，
   **不看待切点**（`Craft` 在点火 tick、落地在完成 tick，若只在 `ApplyCraftOutputs` 里写会被切点门跳过）。

## 五、回归三元组

| 项 | 读数 |
|---|---|
| EditMode | **`6638 / 6601 / 0 / 37`**（total / passed / **failed** / skipped） |
| PlayMode | **`324 / 320 / 0 / 2 skipped + 2 inconclusive`**（两条 = 既有 `U1Assump6` 尖刺 `*_notRun`） |
| 截至时刻 · 批次 · commit | 2026-11-03 · `TD-processing-08` 实现轮 · `f9d5e247` + 工作树 |

**等式**：`6638 − 6630 = 8` = 本件新增 `[Test]` 数（`grep -c "\[Test\]" craft_completion_settler_test.cs` ⇒ 8）。
**零回归**：PlayMode 与上一轮逐位同值（`324/320/0/2+2`）。

## 六、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① 切点字段存在且必填
grep -n "public long AsOfTick" unity/Assets/Sim/ItemDatabase/InventoryFold.cs
grep -c "new InventoryFoldContext(" unity/Assets --include=*.cs   # 全部构造点均带 tick
# ② 结算器唯一写者路径(不铸新号)
grep -n "RecordSpawnedWithId" unity/Assets/Sim/ItemDatabase/CraftCompletionSettler.cs
grep -c "\.Spawn(" unity/Assets/Sim/ItemDatabase/CraftCompletionSettler.cs   # 期望 0
# ③ 夹具
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Inventory/craft_completion_settler_test.cs  # 期望 8
```

## 七、仍未覆盖（不借绿）

- **AC-18-21 的 `[L]` 半边**（听测 / 盲测 / 手柄走查 / 逐屏走查）与本件无关，仍 `NOT-RUN`。
- **起货音仍不响** —— `audio_events.json` 无 `sfx_process_complete` 行（见 story-005 的残余 ①）。
- **联机**：容量裁决在主机侧（本类住 `Sim`），客户端预表现不在本件范围。
