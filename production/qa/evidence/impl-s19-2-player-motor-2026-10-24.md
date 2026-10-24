# S19-2 实现轮证据原件 —— `IPlayerMotor` 契约 + `MoveInput` + `PlayerController` 实现

**日期**:2026-10-24 · **执行**:AutoClaw 会话(`181888b1`)
**对象**:sprint-19 §S19-2(G-5 · `TD-player-02` 兑现)· 测试载体 `unity/Assets/Tests/EditMode/PlayerController/player_motor_test.cs`(新)
**裁定前提**:`PRE-S19-C` 已结(用户 2026-10-24「只读边沿」⇒ `bool ConsumeCellChanged();` 无出参,1:4 孤例记作债册笔误)

---

## 一、改动清单(Landed)

| 文件 | 动作 | 内容 |
|---|---|---|
| `unity/Assets/Gameplay.Presentation/Player/IPlayerMotor.cs` | 新 | `IPlayerMotor` 四成员(ADR-020 §Key Interfaces:396-402 逐字)+ `MoveInput` 薄包装 struct;与 `ICameraRig` 同装配(判据 ④,ADR-025 §②) |
| `unity/Assets/Gameplay.Presentation/Player/PlayerController.cs` | 改 | `: IPlayerMotor`;新增边沿锁存器 `_cellEdgeLatched`(只在 `_lastCommittedCell = cell` **之后**置位 —— 约束 ①);四成员实现(`Tick` 委托 `Move` / `Cell` 走 `CellFromPosition` / `Position` 只读投影 / `ConsumeCellChanged` 消费即清);**删死字段 `_cellDetector`**(C-3 已裁) |
| `design/gdd/player-controller-and-movement.md` | 改 | R5 加注:GDD `:128`「返回 true 的那一刻发事件」读作**因果绑定**而非调用次序(约束 ③) |

**不动面**:`Move` 仍是唯一位移写入点;`GetCell`(RoundToInt)/ `Teleport` 语义不变(判据 ②);`_hasPending` 五写点集合不变(约束 ① 的静态面);`CellTransitionDetector` 原样(与生产内联算子的重复另立 TD,C-3 登记口径)。

## 二、测试面(9 条新增,全绿)

| 测试 | 钉什么 |
|---|---|
| `test_motor_interface_members_areComplete` | 判据 ①④:四成员反射面(`in MoveInput` 按 IsByRef 判、`ConsumeCellChanged` 零形参、`Cell`=WorldPos、`Position`=Vector3)+ 与 `ICameraRig` 同 Assembly + `PlayerController` 实现 |
| `test_motor_noCellChange_doesNotLatch` | 判据 ③:不进格不触发(基线首提交消费后,格内 0.x 抖动零新事件零新边沿) |
| `test_motor_singleCross_latchesExactlyOnce_notTwice` | 判据 ③:跨格一次;消费即清(同边沿不二次);折返 A→B→A 无边沿 |
| `test_motor_twoConsecutiveCrosses_eachLatchesOnce` | 判据 ③:连跨两格的两条各一次 |
| `test_motor_unconsumedEdge_persistsAcrossTicks_untilConsumed` | 判据 ③ 补强:未消费边沿跨 tick 保持,恰好一次可取 |
| `test_motor_consume_doesNotSteal_pendingPath` | 约束 ① 可观测形态:**锁存器亮 + 新 pending 待提交**共存态下消费旧边沿 ⇒ pending 照常提交、第二条边沿仍在 |
| `test_motor_cell_usesFloorToFloat_notGetCell` | 约束 ②:负坐标判别(`FloorToInt(-0.5)=-1` 非 RoundToInt 的 `0`)+ **契约 Cell 与事件载荷 Cell 同源**(解出 `ActorCellEnteredPayload.Cell` 对照) |
| `test_tick_delegatesToMove_singleWritePath` | 判据 ② 机械面:ILBodyScanner 断言 `Tick` 体内调 `Move`(禁旁路重写)+ 行为面(Tick 段继续位移) |
| `test_motor_position_readonlyView_matchesTransform` | `Position` = `transform.position` 只读投影 |

## 三、回归数字(三元组)

- **PlayerController 面**:`110 / 107 / 0 / 3` 零失败(既有 101 + 新 9;3 跳过为既有)@ `2026-10-24 23:5x CST` · `s192-face-all` · `6198671`
- **全量 EditMode**:`4831 / 4796 / 0 / 35` @ `2026-10-24 23:54 CST` · 批次 `s192`(全量)· commit `6198671`
  (前基线 `4822/4787/0/35` @ `23:11 CST` · `tdef01f` · `6198671` ⇒ **total +9 = 本轮新测数,零失败零回归**;`4822+9=4831` 可复算)
- **PlayMode**:本轮实现未触 PlayMode 面(纯 EditMode 反射 / IL / 状态机测;`PlayerController` 的 PlayMode 载体 = 既有 `movement_feed` 系,其面由下轮 `/smoke-check` 全量覆盖)—— 如实登记为 NOT-RUN,不借上轮 `202/202` 充数
- 复算:`grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' <xml> | head -1`

## 四、证伪轮 F1–F3(S12-4 形态,三轮全红)

**纪律**:变异 → 跑 `PlayerMotorTest` → 还原同命令内完成;还原判据 = SHA-256 逐字节(基准 `7cc21b7922fdb103…`);锚串唯一性断言。

| # | 变异(停用哪个守卫) | 结果 | 转红的测试 |
|---|---|---|---|
| F1 | `ConsumeCellChanged` 误清 `_hasPending`(夺 Append 主路径) | 8/9,1 红 | `test_motor_consume_doesNotSteal_pendingPath` |
| F2 | `Cell` 改走 `GetCell()`(RoundToInt,违约束 ②) | 8/9,1 红 | `test_motor_cell_usesFloorToFloat_notGetCell` |
| F3 | 锁存器永不置位(删提交后置位行) | 4/9,5 红 | 全部四条跨格消费测 + noCellChange 的前提断言 |

**⚠️ 首轮夹具修正(如实登记)**:F1 首版**零红 = 假绿夹具** —— 原测的消费点在 `OnTickEdge` 已自清 pending **之后**,变异清的是已清的标志,变异前后不可区分。修正 = 重构测试前提为「锁存器亮 + 新 pending 待提交」**共存态**(约束 ① 的真威胁形态)。修正后 F1 转红。**这本身就是 S12-4 门要抓的东西:夹具对变异无判别力 = 假绿。**

## 五、三条残余(S19-2 卡面预登记,本轮如实处置)

| ID | 内容 | 处置 |
|---|---|---|
| C-1 | 客户端模式永不产生边沿(Client 直接 return ⇒ `_lastCommittedCell` 不推进) | **正确且必须有**;上行链归 P1b/45。S19-2 **不声称覆盖联机**(测面全部 Host 模式) |
| C-2 | `ConsumeCellChanged` 今天零消费者 | 只落形状,未发明消费者(测面是唯一调用方) |
| C-3 | 死字段 `_cellDetector` | ✅ **已删**(1 行);生产内联算子与 `CellTransitionDetector` 的重复**另立 TD 归后续轮**(未立,见 §六) |

## 六、遗留与边界(禁借绿)

- **C-3 的「另立 TD」尚未入册** —— 生产 `PlayerController` 内联的跨格算子与 `CellTransitionDetector` 类存在功能重复(两份同型状态机);本轮只删死字段,合并/豁免判定归后续轮。**待用户裁**:入债册或明确豁免。
- **`Tick` 的 `deltaTime` 形参未参与积分** —— `Move` 自取 `Time.deltaTime`;接口签名按 ADR-020 权威形状落全,实现不重复积分(否则 = 第二时间真源)。doc 已注。
- **联机上行链(OnUplinkSample 赋值 `_lastCommittedCell`?)不属本件** —— 归 P1b/45。
