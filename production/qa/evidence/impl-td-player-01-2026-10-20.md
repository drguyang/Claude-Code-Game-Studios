# 实现轮 —— `TD-player-01` 完整修复（`AC-1-33` 参数落地 + 测试改判真值）

**日期**: 2026-10-20
**用户裁定**: 「B」= 做**完整修复**（非只补一行治症状）
**前置**: `production/qa/evidence/rootcause-movementfeed-fullred-2026-10-20.md`（根因坐实）
**系统归属**: 系统 1 玩家控制器（**不在 audio epic 边界内** —— 本件的越域理由见 §0）

---

## §0 为什么这一件落在 audio sprint 里

用户在两轮查因后明确裁定做完整修复。该债**归属系统 1**，但触发点是 sprint-15 音频批的全量对账
（`TD-player-01` 三次全量红污染了 `passed` 基线，导致 audio 侧无法据「零红」收口）。
⇒ 本件作为**跨 epic 的基线清账**执行，不并入 audio epic 的完成度。

---

## §1 修复面（原状态 → 改动落点）

| # | 原状态（实测证据） | 改动落点 |
|---|---|---|
| **F-1** | `LocomotionConfig` 声明了 `MinMoveDistance` / `SkinWidth` / `Radius` / `Height`，但**全库零处**写进 `CharacterController`；`center` / `enableOverlapRecovery` 连声明都没有 | `LocomotionEvaluator.cs`:`LocomotionConfig` 增 `Center` / `EnableOverlapRecovery` 两字段（默认 `Vector3.zero` / `true`），六项加分组注释与逐项 doc（语义 + 出处 + 陷阱） |
| **F-2** | 控制器一直用引擎默认：`minMoveDistance=0.001` / `skinWidth=0.08` / `height=2` | `PlayerController.cs`:新增 **`ApplyControllerParameters(CharacterController)`**，在**懒绑定点**（`Controller` 属性取件时）写六项；`Awake` 经该属性访问 ⇒ 运行期同样生效 |
| **F-3** | `test_ac133_minMoveDistanceIsZero` / `..._skinWidthRadiusHeightPositive` 断言的是 **`LocomotionConfig.LoadDefault()`（夹具自己的字段）** ⇒ 恒真、对「是否落地」零判别力 | `locomotion_chain_test.cs`:断言对象改为**控制器真值**；新增 3 条；补一条**判别力前置**测 |
| **F-4** | 无任何测试能区分「引擎默认」与「配置已应用」 | 新增 `test_ac133_engineDefaultsDifferFromConfig_provesApplicationIsObservable` —— 钉住「至少 3 项引擎默认 ≠ 配置值」 |

### 1.1 刻意**不做**的两项（归属不在系统 1）

`slopeLimit` / `stepOffset` **未**在本件落地：

- GDD F-1-9 与 **ADR-015 §一之补**把二者的**几何取值**归**关卡工具 / 世界几何**（`O-9`）；
  系统 1 只是**执行者**，无权在代码里发明取值。
- ADR-015 §一之补明写：「本补只定语义与归属，**不定数值**」「几何取值由用户在关卡工具中调」。
- ⇒ 它们的落地形态应是**关卡烘焙数据注入**，不是 `LocomotionConfig` 硬编码。
  **本件不解除 `AC-1-33②`**，该子条仍待 `O-9` 的几何取值。**如实保留为开放项。**

---

## §2 证伪轮（改坏生产码 ⇒ 断言转红；同命令内还原 + SHA-256）

脚本 `/tmp/fz-td01.py`；原始 `PlayerController.cs` SHA-256 = **`ae867848f6cb3528…`**。

| # | 停用 / 改写的守卫 | 转红的测试 | 是否符合预期 |
|---|---|---|---|
| **M-A** | 注释掉 `ApplyControllerParameters(_controller);`（整个参数应用停用） | `test_ac133_configValues_areAppliedToActualController` · `test_ac133_minMoveDistanceIsZero_onActualController` | ✅ **2 红** |
| **M-B** | 只注释 `controller.minMoveDistance = ...` 一行 | `test_ac133_minMoveDistanceIsZero_onActualController` | ✅ **恰 1 红**（区分度成立：其余参数的测试不误红） |

还原校验：`RESTORE SHA256 OK ae867848f6cb3528 ae867848f6cb3528`（逐字节一致）。

### 2.1 ⚠️ 如实登记：一条夹具的判别力局限

`test_ac133_skinWidthRadiusHeightPositive` 在 **M-A 下未红**。

- **原因**：该子条判据是「`skinWidth > 0 ∧ radius > 0 ∧ height > 0`」（GDD 原文），而引擎默认
  `0.08 / 0.5 / 2` **也都 > 0** ⇒ 该断言**无法区分「应用了」与「没应用」**。
- **性质**：这是 **AC 原文判据形态**所限（它要的是「不存在 Inspector 留空」，不是「等于配置值」），
  **不是**夹具做弱了。真正判「是否应用」的是 M-A 下转红的那两条。
- **未改 AC 措辞** —— 改 AC 要动 GDD，归设计轮；此处**只登记**。

---

## §3 回归对账（全量 EditMode）

| 项 | 值 |
|---|---|
| 基线（修复前，同一工作区）`m8m11-full-2026-10-20.xml` | total 3938 · passed 3901 · failed **1** · skipped 36 |
| 修复后 `td01-full-2026-10-20.xml` | total **3940** · passed **3904** · failed **0** · skipped 36 |

**等式（禁借绿口径）**：

```
Δtotal   = 3940 − 3938 = +2
集合差实测:新增 3 条 / 消失 1 条 ⇒ 净 +2  ✓
  + test_ac133_configValues_areAppliedToActualController
  + test_ac133_engineDefaultsDifferFromConfig_provesApplicationIsObservable
  + test_ac133_minMoveDistanceIsZero_onActualController
  − test_ac133_minMoveDistanceIsZero            (改名为 …_onActualController)

Δpassed  = 3904 − 3901 = +3 = Δtotal(+2) + 修复的那条红(+1)  ✓
Δfailed  = 0 − 1 = −1                                          ✓
Δskipped = 36 − 36 = 0                                         ✓
```

> ✅ **`MovementFeedTest` 那条红已消失**,且**无新增红** ⇒ 全量首次真正**全绿**（0 failed）。
> 这是 `TD-player-01` 自登记以来**第一次**全量为零红。

---

## §4 结账表（Landed / Proven 分列，承 `code-review` Phase 8b）

| ID | Landed | Proven | File(s) | Proven-by |
|---|---|---|---|---|
| F-1 声明补全 | ✅ | ✅ | `LocomotionEvaluator.cs:40-80` | M-A 转红 `configValues_areAppliedToActualController`（`center`/`enableOverlapRecovery` 含在该测内） |
| F-2 参数落地 | ✅ | ✅ | `PlayerController.cs`:`Controller` 属性 + `ApplyControllerParameters` | M-A 转红 2 条 · M-B 恰转红 1 条 |
| F-3 断言改真值 | ✅ | ✅ | `locomotion_chain_test.cs` | M-B：只停 minMoveDistance ⇒ 恰 `…_onActualController` 红 |
| F-4 判别力前置 | ✅ | ✅ | 同上 `…_engineDefaultsDifferFromConfig_…` | 该测本身判「≥3 项引擎默认 ≠ 配置」；若将来引擎默认改到与配置一致，它会红 |
| `AC-1-33②`（`slopeLimit`/`stepOffset`） | ❌ **未做（归属 ADR-015 / O-9）** | ❌ | — | **如实保留开放项**，不得标已结 |
| `AC-1-34`（`DOWNWARD_PRESSURE`） | ❌ 未做 | ❌ | — | 常量全库零命中；`skinWidth=0.01` 现已真落地，但不等式仍无守卫 —— **登记为残留** |

---

## §5 未解 / 残留（如实）

1. **`AC-1-33②` 仍无载体** —— `slopeLimit` / `stepOffset` 的几何取值卡在 `O-9`（ADR-015 只定归属未定值）。
   系统 1 无权自行填值，**本件不解除该子条**。
2. **`AC-1-34` 的 `DOWNWARD_PRESSURE` 全库零命中** —— 该 AC 无载体。`skinWidth` 现已落地为 `0.01`，
   但「`0.015 > skinWidth ∧ > minMoveDistance`」这条不等式**仍无装载期断言**。
3. **`LocomotionConfig` 其余 5 个字段仍零生产消费方** —— `TurnRate` / `AirControl` /
   `JumpHeightMin` / `JumpHeightMax` / `GravityFallMult`（本件范围外，属手感链接线）。
4. **capsule 几何变更的下游影响未做 PlayMode 验证** —— `height` 从引擎默认 `2` 变为 `1.8`、
   `skinWidth` 从 `0.08` 变为 `0.01`。EditMode 全绿，但**贴墙 / 台阶 / 接地手感须实机确认**
   （`AC-1-20b` 本就是 playtest 签核项）。**NOT-RUN**。

---

## §6 复算命令

```bash
# 定向
cd unity && unity test unity --mode EditMode \
  --filter "DaYiJingCheng.Tests.PlayerController.LocomotionChainTest" \
  --output ../unity/Logs/ac133-dir.xml        # 期望 25 total / 23 passed / 0 failed / 2 ignored

# 证伪（改 → 跑 → 还原同一命令内）
python3 /tmp/fz-td01.py                        # 期望 RESTORE SHA256 OK；M-A 2 红 / M-B 1 红

# 全量
cd unity && unity test unity --mode EditMode --output ../unity/Logs/td01-full.xml
#   期望 total=3940 passed=3904 failed=0 skipped=36
```
