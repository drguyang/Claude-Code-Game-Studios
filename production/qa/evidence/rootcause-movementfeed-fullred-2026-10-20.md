# 查因报告 —— `MovementFeedTest` 全量间歇红的根因

**日期**: 2026-10-20
**触发**: 用户指令「查因」（承 `impl-b-audio-tier-wiring-2026-10-20.md` §五 / §八 8.4 的三次实测）
**结论**: **根因已坐实 = `TD-player-01`** —— `CharacterController.minMoveDistance` 从未被生产写入，
引擎默认 **0.001** 吞掉了首帧亚阈值位移。**不是** B 轮改动的回归，也**不是**测试不稳定。

---

## 1. 症状（三次实测，同一条）

| 轮次 | 日志 | total | passed | failed | 红项 |
|---|---|---|---|---|---|
| B 轮 | `b-tiers-full-2026-10-20.xml` | 3938 | 3901 | 1 | `MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` |
| 复裁轮（代码零改动） | `m8m11-full-2026-10-20.xml` | 3938 | 3901 | 1 | **同一条** |
| 单跑隔离 | `mf-solo.xml` | 1 | 1 | 0 | —（**绿**） |

失败报文（两次几乎逐字相同，只有 dt 不同）：

```
落地后一帧水平位移须 ≈ 新速·dt = 0.0009(dt = 0.0095)… ⇒ 红
  Expected: 0.00090682023437693715d +/- 0.00019068202226274868d
  But was:  0.0d                       ← 注意：恰为 0，不是"偏小"
```

`But was: 0.0` 是**决定性的形态线索**：物理误差只会让值**偏小**，能产生**恰为 0** 的只有
「位移被整体丢弃」这一类机制。

---

## 2. 根因链（逐环实证）

### 2.1 `dt` 在全量跑测时约 9.5–10 ms，而单跑时是 333 ms

`MovementFeedTest` 文件头自陈：「`Time.deltaTime` 在 batch EditMode ≈ 0.333 s」。
但全量时**慢得多**——失败报文里的 `dt = 0.0095 / 0.0099`。这不是随机噪声，而是全量执行时
编辑器帧预算被压到约 10 ms（≈100 fps）。**单跑 0.333 / 全量 0.0095，相差 35 倍** —— 这正是「单跑绿、全量红」的分叉点。

### 2.2 该夹具的断言量是 `10·dt²`（二阶小量）

生产 `PlayerController.Move`（`:216-224`）：

```
currentSpeed = 0（静置）
maxDelta     = Accel · Time.deltaTime = 10 · dt
newSpeed     = MoveTowards(0, vTarget, 10·dt) = 10·dt      （dt 小时未饱和）
delta        = newSpeed · dt = 10·dt²
```

夹具按同一公式算 `expectedDx = Mathf.Min(vTarget, 10·dt) · dt = 10·dt²`。
⇒ `dt = 0.0095 ⇒ expectedDx ≈ 0.000907`。

### 2.3 `CharacterController.minMoveDistance` 仍是引擎默认 **0.001**（探针实测）

生产**从未**把这个值写进组件。`AC-1-33①` 要求 `minMoveDistance == 0`，
`LocomotionConfig.MinMoveDistance = 0f` 也确实声明了——**但声明与落地之间是断的**：

```
$ grep -rn "\.minMoveDistance" unity/Assets/ --include=*.cs | grep -v Tests/
（空）
```

探针实测（`unity/Logs/probe-cc.xml`，脚本跑完即删）：

```
[PROBE] 引擎默认 minMoveDistance = 0.001
[PROBE] 引擎默认 skinWidth       = 0.08
[PROBE] 引擎默认 radius          = 0.5
[PROBE] 引擎默认 height          = 2
```

### 2.4 A/B 实证：同一个 delta，0.001 吞、0 不吞

控制变量实验（`unity/Logs/probe-shapes.xml`），**只改 `minMoveDistance`**：

| 写入 delta | 模长 | `minMoveDistance = 0.001`（实际） | `minMoveDistance = 0`（AC-1-33① 要求） |
|---|---|---|---|
| `(0.0009, -0.00475, 0)` | 0.0048 | **dx = 0.0000** | **dx = 0.0009** ✅ |
| `(0.0009, 0, 0)` | 0.0009 | dx = 0.0000 | — |
| `(0.002, 0, 0)` | 0.0020 | dx = 0.0020 ✅ | — |

⚠️ 注意第二行与第一行的差别：`|d| = 0.0048 > 0.001` **仍然被吞**，说明
`CharacterController` 的阈值判定按**单轴**算，不是向量模长 —— 这也解释了为什么
加上「每帧 -0.00475 的下压」并没有救回水平位移。

**因果闭合**：`0.000907 < 0.001` ⇒ 水平位移被引擎整体丢弃 ⇒ `dx` **恰为 0.0**。

---

## 3. 修法反证（最硬的一环）

把 `AC-1-33①` 要求的 `minMoveDistance = 0f` **临时**补进 `PlayerController` 的懒绑定点，
跑**全量** EditMode：

| | total | passed | failed | skipped | MovementFeedTest |
|---|---|---|---|---|---|
| 修前（同一份代码） | 3938 | 3901 | **1** | 36 | 红 |
| 修后（仅补一行） | 3938 | 3902 | **0** | 36 | **绿** |

- 该红**消失**，且 **total / skipped 不变、无新增红** ⇒ 不是「红被别的红顶替」。
- 脚本 `/tmp/fz-mf-fix.py`：改 → 跑 → 还原**同一命令内**；
  `finally` 后 SHA-256 逐字节校验 → `RESTORE SHA256 OK 25f8dd2a96d07de4 25f8dd2a96d07de4`
- ⚠️ **生产码现已还原**：`PlayerController.cs` 无「反证用临时补丁」残留（grep 计数 = 0）。

---

## 4. 与既有债册的关系：`TD-player-01` **早已预言，本轮首次坐实**

`docs/tech-debt-register.md:207` 的 `TD-player-01` 逐字写着：

> `AC-1-33` ③ 语义层未被验证 —— 测试断言的是它自己的夹具……实测：该值只在
> `LocomotionEvaluator.cs:34` **声明**；**全库零处**把它写进 `CharacterController`
> ⇒ 控制器取 Unity 默认 **0.001**……**下游症状已实测**：`MovementFeedTest.…` 间歇红
> —— `dt = 0.0099` ⇒ `expectedDx = 0.00098 < 0.001` ⇒ 位移被引擎吞掉 ⇒ `dx` 恰为 `0.0`
> （GDD 预言的失败模式**如期出现**）。

**本轮补上的正是它缺的那一环**：此前只有「症状实测 + 机制推演」，
**没有「补上修法 ⇒ 红消失」的反证**。§3 把这条链闭合了。

⇒ 之前各批把它记作「已知间歇、不抵扣」是对的（**不是** B 轮的回归），
但「间歇」这个措辞偏软：**它不是随机间歇，而是 dt 降到 10 ms 以下就必然发作的确定性边界**。
全量跑测比单跑慢 35 倍，恰好把它推过了阈值。

---

## 5. 顺带查出的同源缺口（同一根因，未修，如实登记）

`LocomotionConfig` 的 12 个字段中，**只有 3 个有生产消费方**：

| 字段 | 生产消费点 |
|---|---|
| `SpeedWalk` / `Accel` / `Decel` | 1（`PlayerController.Move`） |
| `TurnRate` / `AirControl` / `JumpHeightMin` / `JumpHeightMax` / `GravityFallMult` | **0** |
| `MinMoveDistance` / `SkinWidth` / `Radius` / `Height` | **0** |

⇒ `AC-1-33①②③` 三项判据**一项都没有落到组件上**。`test_ac133_minMoveDistanceIsZero`
（`locomotion_chain_test.cs:200`）断言的是 `LocomotionConfig.LoadDefault().MinMoveDistance`
—— **夹具自己的字段**，不是控制器真值。这正是 `TD-player-01` 标题所说
「测试断言的是它自己的夹具」。

另：`AC-1-34` 要求的 `DOWNWARD_PRESSURE` 常量**全库零命中**，该 AC 亦无载体。
（本条未深查，归同族债，不并入本报告结论。）

---

## 6. 结论与建议

| 问题 | 答复 |
|---|---|
| 是 B 轮改动引起的回归吗？ | **不是**。代码零改动复跑仍红；补一行不相关的修复即绿 |
| 是测试不稳定（flaky）吗？ | **不是**。是 **dt < 0.01 s 必然发作**的确定性边界；「间歇」只是 dt 是否跨过阈值 |
| 根因是什么？ | `CharacterController.minMoveDistance` 未按 `AC-1-33①` 设为 0，引擎默认 0.001 吞掉 `10·dt²` |
| 修法？ | 一条：在 `PlayerController` 取件处/初始化时写 `minMoveDistance = 0f`；**完整修法** = `TD-player-01` 已列的全套（6 个参数应用 + 测试改判控制器真值） |

**建议（未执行，待用户裁）**：

1. **修 `TD-player-01`** —— 属**系统 1 的实现轮**，不在 audio epic 边界内。
   最小可先落 `minMoveDistance = 0f`（一行，专治本红）；完整修法须连 `skinWidth` / `radius` /
   `height` / `center` / `enableOverlapRecovery` 一并应用，并把 `test_ac133_*` 的断言对象
   从夹具改为**控制器真值**。
2. **在 `TD-player-01` 行补记本轮的修法反证**（§3 的 A/B 表），把「间歇」改述为
   「dt < 0.01 s 确定性发作」。
3. 在 `TD-player-01` 修好前，任何全量跑测的 `passed` 数**都不得称「全量全绿」**
   （该条禁令维持不变）。

---

## 7. 复算命令

```bash
# 根因取证（探针已删，此处记录当时命令形态）
#   探针文件: unity/Assets/Tests/EditMode/Boot/tmp_probe_move.cs（跑完已 rm）
#   unity test unity --mode EditMode --filter "…TmpProbeMove.probe_deltaShapes" \
#         --output ../unity/Logs/probe-shapes.xml

# 修法反证（改 → 跑 → 还原同一命令内，SHA-256 校验）
python3 /tmp/fz-mf-fix.py
#   期望：RESTORE SHA256 OK；修后 total=3938 passed=3902 failed=0 skipped=36

# 日志
unity/Logs/probe-cc.xml          # 引擎默认参数值
unity/Logs/probe-shapes.xml      # A/B 吞位移对照
unity/Logs/mf-solo.xml           # 单跑绿
unity/Logs/mf-fix-mm0.xml        # 补 minMoveDistance=0 后的全量绿
```
