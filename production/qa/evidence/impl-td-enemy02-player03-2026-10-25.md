# 实现证据件 — TD-enemy-02 + TD-player-03 双实现收敛轮

**日期**: 2026-10-25
**批次**: conv(收敛轮)
**commit**: `7223126` 工作树(含本轮未提交改动;债册翻转与证据件同批)
**用户裁定**(2026-10-25,双取推荐): TD-enemy-02 **甲案「删直线逼近」** · TD-player-03 **甲案「删实例状态机」**

---

## 一、裁定与执行

| 债 | 裁定 | 执行 |
|---|---|---|
| TD-enemy-02 | 删 `EnemyPathing.StepAccumulator` / `ValidateAccumulator`(直线逼近,GDD 无对应物;F-27-4 权威 = 沿路径 `EnemyLocomotion.Advance`) | 已删 + 留档注;`EnemyLocomotion` 头注钉「唯一实现」;AC-27-19 性质测试迁 Advance 载体;机械门随第二实现**结构性消解**(单算子无可断言之物) |
| TD-player-03 | 删 `CellTransitionDetector` 实例状态机(功能子集 + 帧末 tick 缺陷形态;内联走边沿自有 tick,BCD-码-2) | 类重写为**静态工具**(`CellFromPosition` / `ComputeDisplacement` 保留 —— 取格单一真源);12 条行为断言迁 `PlayerController` 内联;`CellFromPosition` 静态消费面不变 |

## 二、改动清单

**生产(3 文件)**:
- `unity/Assets/Sim/EnemyPathing.cs` — 删 `StepAccumulator` + `ValidateAccumulator`(留档注)
- `unity/Assets/Sim/EnemyLocomotion.cs` — 头注钉唯一实现
- `unity/Assets/Gameplay.Presentation/Player/CellTransitionDetector.cs` — 重写为 static class(两静态工具保留)
- `unity/Assets/Gameplay.Presentation/Player/PlayerController.cs` — C-3 注释处结清注(无逻辑改动)

**测试(7 文件)**:
- `Tests/EditMode/EnemyAI/deterministic_pathing_test.cs` — 删 `test_steppingProperty`(打错算子);AC-27-20 Ignore 注释订正
- `Tests/EditMode/EnemyAI/logi_pose_test.cs` — 新增 `test_ac2719_property_speedBelowOne_andMovingSequences`(4 speed × 3 序列:acc ∈ [0, 65536) 字面量 · Cell ∈ path · 曼哈顿位移 ≤ 1 · 冻结位姿不动)
- `Tests/EditMode/PlayerController/cell_transition_test.cs` — 全文件重写:S11-3 真夹具(AddComponent + `Initialize(Host, sink, provider, encoder, actorId)`),12 条行为断言打内联;`test_ac105` 升级为 `test_ac105_tickFromEdgeTick_notFrameEnd`(provider=999 / 边沿=42,比原断言更强)
- `Tests/EditMode/PlayerController/host_authority_test.cs` — `test_ac130_hostMode_emitsEvent` 迁内联
- `Tests/EditMode/PlayerController/stream_bound_test.cs` — 两测试迁 `controller.OnPositionSample` / `OnTickEdge`
- `Tests/EditMode/PlayerController/o6_actor_cell_payload_test.cs` — 三条迁 controller 形态(twoActors / payloadRoundTrip / negativeActorId)
- `Tests/EditMode/WorldEcozones/chunk_activation_test.cs` — 驱动源迁 `SpawnController` 真组件

**漏扫记录(如实登记)**: 首验编译红(CS0712/CS0723/CS1061)暴露消费面不止 `cell_transition_test` —— host_authority / stream_bound / o6 / chunk_activation 四夹具共 7 处 `new CellTransitionDetector` 为债文「等」字所含,首轮勘察漏扫,已全数迁移。全仓 grep 代码面残留 = 0(4 处命中均为注释里的删除注引述)。

## 三、回归三元组

**`5033 / 4998 / 0 / 35`** @ `2026-10-25 12:25 UTC` · 批次 `conv` · commit `7223126` 工作树。

**前基线**: `5033/4998/0/35` @ `2026-10-25 11:49 UTC` · `TD-persist-04 义务②` 轮 · `7223126` 工作树 ⇒ **净变化 0**:EnemyAI 侧 −1(`test_steppingProperty`)+ 1(`test_ac2719_property…`)相抵;cell_transition_test 迁移前后条数不变(19 条)。

**复算命令**(自包含):

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  unity/Logs/conv-full.xml | head -1
# 预期输出: total="5033" passed="4998" failed="0" inconclusive="0" skipped="35"
```

首验夹具批(六夹具): `Logs/conv-first3.xml` — `65/63/0/2`(2 skipped = 既有 Ignore:AC-27-20 / AC-104)。

**PlayMode 全量**(用户指令补跑): **`202 / 202 / 0 / 0`** @ `2026-10-25 12:35 UTC` · 批次 `conv-playmode` · `7223126` 工作树 —— 全绿,零回归。复算:

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  unity/Logs/conv-playmode.xml | head -1
# 预期输出: total="202" passed="202" failed="0" inconclusive="0" skipped="0"
```

⚠️ **PlayMode 基线溯源**:本轮数字为本件**新立基线**(此前各轮 PlayMode 记录散落且口径不一,
本件不引用旧数 —— 引用旧件须改写来源,承「数字须带可跑复算命令」纪律);后轮引用本数须带本三元组。

## 四、证伪轮(S12-4)

一属性一条变异;改 → 跑 → 还原同命令;SHA-256 逐字节还原;锚串恰命中 1 次;窗口内不读被变异文件。

| # | 被变异文件 | 变异(恢复已删缺陷/停用守卫) | FILTER | 结果 |
|---|---|---|---|---|
| M1 | `PlayerController.cs` | `AppendCellEnteredEvent(cell, tick);` → `AppendCellEnteredEvent(cell, _tickProvider.CurrentTick);`(边沿 tick → 帧末,恢复 BCD-码-2 前缺陷形态) | CellTransitionTest | **红 1**: `test_ac105_tickFromEdgeTick_notFrameEnd`(failed=1;ac104 为既有 Skipped 非失败);SHA-256 还原一致 |
| M2 | `EnemyLocomotion.cs` | `if (!moving)` → `if (acc.Raw < 0)`(冻结分支停用,冻结期仍累加) | LogiPoseTest | **红 2**: `test_ac2719_property_speedBelowOne_andMovingSequences` + 既有 `test_advance_frozenKeepsAcc_notZeroing` 双确认;SHA-256 还原一致 |

**变异零残留验证**: `grep -c "acc.Raw < 0" Assets/Sim/EnemyLocomotion.cs` = 0;`grep -c "CurrentTick);" Assets/Gameplay.Presentation/Player/PlayerController.cs` = 0;`git diff --stat` 两文件仅剩本轮有意改动(头注 / 结清注)。

## 五、NOT-RUN 与边界(禁借绿)

- ~~**PlayMode**: 本轮未跑~~ → **✅ 已补跑**(用户指令): `202/202/0/0` 全绿,见 §三 三元组。
- ~~**运行期行为验证**(编辑器/构建内实际跑): 未做~~ → **✅ 已补跑**(用户指令「完成他们」):
  StandaloneLinux64 全量构建 `Build Finished, Result: Success.` @ `unity/Logs/build-StandaloneLinux64-1792931952927.log:8621`,
  player 落 `/home/gu/.claude/jobs/181888b1/tmp/conv-verify-player`(复算:
  `grep -n "Build Finished" unity/Logs/build-StandaloneLinux64-1792931952927.log`)。
  ⚠️ 按 CLAUDE.md BuildGate 判据差一层:绿只证「门链无错」,不证门跑了 —— 门接线有效性由
  2026-10-21 C3 轮探针轮(三判决)承载,本轮不重跑。**NOT-RUN 清零**(余项:编辑器手动走查
  —— 本轮非「必须图形界面」情形,按 CLAUDE.md 不请用户操作)。
- **`GetCell()`(RoundToInt)分叉**: 不在本条裁决面(债册结案块 ⚠ 已登记)。
- **将来「直线逼近」需求**: 须另立条目重裁,不得复活 TD-enemy-02。

## 六、债册与索引

- `TD-enemy-02` / `TD-player-03` 索引行 `open → closed`(结案块见各自正文)
- 输出块刷**三十订**: `closed 34 / open 68 / 未闭 P0 面 69 / 9/31 epic`(§五 脚本实跑,复算命令在册)
- session state 同批更新
