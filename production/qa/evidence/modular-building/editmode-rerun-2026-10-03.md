# modular-building — EditMode 复跑证据(2026-10-03 · 判据修复轮)

> **本件性质**:C8/ID 修复轮后的**实测复跑证据**。本件**只**主张「下面这条命令，在该时刻，产出该计数」。
> 本件**不**主张评审各项已完备（那属评审原件），**不**主张任何 NOT-RUN / EXTERNAL 项已通过。

## 一、运行命令与环境

```bash
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath unity \
  -runTests -testPlatform EditMode \
  -logFile unity/Logs/mod-full.log
```

| 项 | 值 |
|---|---|
| 引擎 | Unity 6.3 LTS(6000.3.24f1) |
| 平台 | Linux x64 · Mono(Editor) |
| 结果文件 | `unity/TestResults-639266666379500340.xml` |
| 退出码 | **2**（= 存在 skipped/inconclusive，**非失败**；见 §四 判读口径） |

## 二、全量结果

**`2204 = 2163 passed + 0 failed + 1 inconclusive + 40 skipped`**

- **0 failed** —— 零红。
- `1 inconclusive` / `40 skipped` = **既有项**，与本次修复无关（详见 §四）。
- 与 player-controller 轮基线（`2202 / 2161 / 0 / 1 / 40`）逐位比对：**恰 +2（本轮的 2 条新回归用例），其余全同**
  ⇒ **零回归**。

## 三、ModularBuilding 逐 fixture(72 例)

同一 XML 内按测试类聚合：

| Test Class | 例数 |
|---|---:|
| StructurePayloadEncoderTest | 12 |
| PlaceableCheckerTest | 10 |
| **StructureKindsTest** | **9** |
| WorldTest | 8 |
| BuildSlotCatalogTest | 7 |
| **StructureOccupancyWiringTest** | **7** |
| OccupancyOverlayTest | 6 |
| ModifiableCheckerTest | 5 |
| RefundCalculatorTest | 5 |
| DemolishCheckerTest | 3 |
| **合计** | **72** |

> 回归前 ModularBuilding = **70**（`bc7657e` 转录口径 51 例为更早快照，非本轮基线）。
> 本轮 **+2** = 新增的两条 C8/ID 回归用例（见 §五）。

## 四、判读口径（防误读）

- **退出码 2 ≠ 失败**：Unity 在存在 skip / inconclusive 时返回 2。本轮判决依据是 XML 内
  `failed="0"`，**不是**退出码。单以退出码读会误报红。
- **`1 inconclusive` / `40 skipped` 为既有项**：与本轮改动无关 —— 全量基线在 player-controller
  轮（`a78c27a`）即为同一计数，本轮逐位一致。
- `-resultFile` 参数在本环境**不被遵守**（Unity 固定写 `unity/TestResults-<id>.xml`）；
  本件记录的即该实际落盘文件。该产物已由 `.gitignore` 排除（本地项，不入库）。

## 五、本轮修复的针对性验证（C8/ID）

### 5.1 复跑前既有用例 70/70 全绿（未捕获原缺陷）

原缺陷（structure id 全链 `int`，权威件为 `i64`）**逃过了全部既有用例** ——
既有夹具的 id 均为「小值」（1 / 2 / 999），即便实现退回 `int` 也照样全绿。
⇒ **判据必须真的把 id 推过 `int` 上界**，否则捕获不到原缺陷。

### 5.2 新增两条回归用例（`StructureKindsTest` 7 → 9）

| 用例 | 判据形态 |
|---|---|
| `test_ac23_id_exceedsIntRange_survivesRoundTrip` | **行为**：把 id 推过 `int.MaxValue`（`4_000_000_000L`）走全链；夹具自带前提断言 `Assert.Greater(bigId, int.MaxValue)` 防自身失效 |
| `test_ac23_id_typeIsInt64_inBothRegistryAndPayload` | **类型身份**：反射断言 `StructureInstance.StructureId` / `Registry.Register` 返回 / `StructureWriter.Place` 返回 / 契约版 `StructurePlacedPayload.StructureId` **四处同为 `long`** |

### 5.3 突变测试（证「判据非空转」）

对 `StructureKinds.cs` 做两次反向突变，均**不产生绿色用例**：

| 突变 | 结果 |
|---|---|
| 直接退回 `StructureId` 为 `int` | **编译失败**（类型系统已无法表达原缺陷形状） |
| `int` 计数器 + `(int)` 收窄转换（更隐蔽的复现形态） | **编译失败** |

⇒ 判据**在编译期即可执行**，强于「红测试」。突变后原件已从备份复原并逐行核验
（`long` 声明位于 `:42` / `:63` / `:64` / `:77` / `:117` / `:151` / `:179` / `:182`）。

## 六、本件**不**主张（禁借绿）

- ❌ **不**主张评审各项判据已完备 —— 本件是**复跑证据**，非评审原件。
- ❌ **不**主张 **N-r2**（无生产装配根）已闭。
- ❌ **不**主张 **story-005** 桌面走查 / 工具 CI（EXTERNAL）已执行。
- ❌ **不**主张 story-004 `World.unity` 构建期扫描已验（该场景文件在 ADR-023 三场景制下尚不存在）。
- ❌ **不**以本件替代行为性 SIGN-OFF。
