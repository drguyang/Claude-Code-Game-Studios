# 相机两段式(`CameraMode.Downed`)—— 实现 + 测试 · 证据

**日期**: 2026-11-01
**批次**: 用户选 **B 路**(扩 `ICameraRig` + 加 `Downed` 档 + 占位数值)
**前置**: 用户指令「② 相机两段式(2 的数值轮)。这个需要怎么做」→ 用户「选 b」

---

## 1. 为什么这是 B 路而不是「只做接线」

勘察(2026-11-01)实测三处缺口:

| # | 缺什么 | 证据 |
|---|---|---|
| ① | 相机侧**零死亡处理** | `grep 死亡/Downed/两段/Fall` 在 `Camera/*.cs` **零命中** |
| ② | `CameraMode` 四档里**没有「倒下」档** | `Explore / Treatment / Casebook / FirstPerson` |
| ③ | 唯一档位写入点是 `SetModeForTest` | `BootRoot.cs:715`,测试专用;生产零请求方 |

⇒ 只做接线没有可调的量(`ICameraRig` 只有 `SetMode/Tick/YawBasis`,无「下沉」参数)。
B 路 = 补档位 + 补相位机 + 补位姿效果 + 接线,数值全用**具名占位**。

---

## 2. ⚠️ 发现一处**引据错误**(如实登记)

`design/gdd/death-and-respawn.md:491` 写:

> 「倒下 = **两段式**(先失去支撑 → 再躺下,承 **25 `:1639`**)」

但 `design/gdd/disease-simulation.md` 的**第 1639 行**是伤害曲线表的**历史注记**
(「…而该基准已被 38011 raw(0.58)取代」),**不含两段式**。

**真实出处** = `design/gdd/combat-and-weapon-lines.md:1665`
(「两段式倒下:**先失去支撑 → 再躺下**(承 13)」—— 那是**敌人**的呈现契约)。

⇒ 本件按 `design/ux/death-transition-29.md` §9 的**玩家相机**规格实现;
`combat:1665` 与 `29:491` 的「先失支撑 → 再躺下」**语义同构**,故引据错误不改变形状,
但 `death-and-respawn.md:491` 的引用须订正(归 29 的回写轮,本批**不动** GDD 正文)。

---

## 3. 实现(四件)

| 文件 | 改动 |
|---|---|
| `Camera/ICameraRig.cs` | `CameraMode` 加 **`Downed`** 成员(含 spec 依据与 VR 全禁注) |
| `Camera/CameraModeMachine.cs` | `CameraModePriority` 加 `Downed => 2` |
| `Camera/CameraDownedPhases.cs` | **新增**:两段式相位机(`CameraDownedDriver` 纯结构) |
| `Camera/CameraRig.cs` | `Tick` 推进相位机;`GetCameraPosition` 叠加下沉;有效 pitch 叠加微倾 |
| `DeathTransition/DeathTransitionPresenter.cs` | `Bind` 加 `requestDownedCamera` 委托缝;`Update` 边沿触发请求 |
| `Gameplay.Boot/BootRoot.cs` | 4.3b 接线(闭包直引 `_cameraRig`,VR 显式跳过) |

### 3.1 `Downed` 的优先级 = 2(与 `Treatment` 同格)—— 取舍说明

三选一:

| 取值 | 后果 | 判 |
|---|---|---|
| 3(与 `Casebook` 同) | 死亡会**压过**脉案模态 | ❌ 违规则七(模态优先不可翻) |
| 1(与 `Explore` 同) | 玩家死亡瞬间若有残留 Explore 请求可顶掉它 | ❌ 倒下的玩家不该被普通探索态抢机位 |
| **2(与 `Treatment` 同)** | 与急救档同格 | ✅ **采纳** |

**为什么 2 可以**:玩家一旦 `PlayerDied` 落流,1 侧已抑制移动/交互(29 story-001 接线)
⇒ 10 的急救请求在那一刻**不再产生** ⇒ 二者实际不会同时入队;
真同帧入队时「后者胜」也给出确定结果,无活锁。

### 3.2 微倾只改**有效 pitch**,不改 `_pitch`

⚠️ 若把 `SagTiltDegrees` 直接累加进 `_pitch`,玩家醒来后会发现俯角莫名变了且调不回去。
⇒ `GetCameraPosition` 内用 `effectivePitch = clamp(_pitch + tilt, PITCH_MIN, PITCH_MAX)`,
`_pitch` 本身不动。夹具 `test_tilt_doesNotMutatePlayerPitch` 钉这一条。

### 3.3 下沉只沿世界 **-Y**

⚠️ 不参与绕点 —— 否则就是「绕着人转」= **视场旋转**,spec §9 晕动评估明禁
(「小幅度机位运动 + 全屏 2D 叠层,**非视场旋转**」)。

### 3.4 为什么呈现器用**委托缝**而不是直接持 `ICameraRig`

① `CameraRig` 是 `BootRoot` 的**私有字段**、不在服务袋里(`CompositionRootServices` 只有
`DeathPresenter` / `TeleportSink`),呈现器拿不到;
② 呈现三件套纪律要求呈现层**不持有**别系统对象,只发意图(ADR-020 §五「相机只读不持状态」)。
⇒ 委托缝与 `Bind` 的其余三支同款;`null = 未接线` ⇒ 呈现器跳过请求,不抛。

### 3.5 边沿触发

⚠️ 意图制(ADR-020 §Key Interfaces)只需在**状态变化**时登记;每帧重复入队会让
`CameraModeMachine._pending` 无限增长。⇒ `_downedRequested` 记上一帧状态,只在变化时发。

---

## 4. ⚠️⚠️ 发现一处**规格自相矛盾**(待用户裁定,不是实现能自裁的)

### 4.1 矛盾

| 出处 | 原文 | 要求 |
|---|---|---|
| `camera-and-viewpoint.md:252-256` | `Casebook` 的**退出条件** = 「请求方请求 `Explore`」;`Treatment` 退出条件 = 「10 请求 `Explore`」 | **低优先级请求应能降档** |
| `CameraModeMachine.Settle` | 判据 = `bestPrio >= curPrio` | **低优先级请求被拒** |
| `camera_mode_machine_test.cs:96` `test_ac218a_lowerPriorityCannotInterruptHigher` | 穷举 3×3 档对,**明确断言** `Casebook→Explore` 必须保持 `Casebook` | **低优先级不能打断** |

⇒ **GDD 状态表与 AC-2-18① 直接矛盾**。两者都是权威件。

### 4.2 对 `Downed` 的直接影响

`Downed`(2)无法被 `Explore`(1)顶掉 ⇒ **玩家复活后相机永远出不了倒下档**。
这与 `Casebook` 无法被 `Explore` 退出是**同一个机制**,不是我新引入的缺陷 ——
但它使 `Downed` 在生产上**不可退出**。

### 4.3 本批的处置(不越权)

- **锁住今天的行为**:`test_downed_cannotExitViaExplore_currentBehaviorLocked` 断言
  「当前出不去」,并在 XML 注释里逐条登记矛盾双方。
- **不擅自改 `Settle` 判据** —— 那会同时改掉 `Casebook` / `Treatment` 的退出语义,
  影响 39 / 10 两个系统,属跨域裁定。
- **修复方向归用户**:见下节。

### 4.4 三个可选修法 —— **用户裁定甲案**(2026-11-01)

| # | 修法 | 代价 | 影响面 |
|---|---|---|---|
| **甲 ✅ 已采纳** | `Settle` 加「**同请求方**可降档」判据(`requesterId` 相同时允许降) | 小,改一处判据 | 只影响「同一请求方进出」,`Casebook` 仍不能被别的系统顶掉 |
| 乙 | 加**显式退出信号**(`ClearMode(requesterId)` 而非靠低优先级请求) | 中,要扩接口 | 最干净,但要动 ADR-020 的接口面 |
| 丙 | `Downed` 特判(优先级取 1) | 最小 | ❌ **不推荐**:会让死亡被 Explore 顶掉(§3.1 已否) |

### 4.5 甲案的落地(用户裁定后,2026-11-01)

**判据**(`CameraModeMachine.Settle`):

```
sameRequesterDowngrade = best.Mode == Explore
                      && best.RequesterId == CurrentRequesterId
                      && Mode != Explore
canSwitch = (bestPrio >= curPrio || sameRequesterDowngrade) && best.Mode != Mode
```

**为什么必须是「同一请求方」而不是「任意低优先级」**:若放开成「任意 Explore 可降档」,
39 的脉案会被 10 的收尾请求顶掉(反之亦然)—— 那正是 `O-12` 要防的互抢机位。

**四处连带改动**:

| # | 改动 | 为什么 |
|---|---|---|
| 1 | `CameraModeMachine` 新增 `CurrentRequesterId`(初值 `-1` = 无请求方) | 判据要认「谁开的这个档」;初值 `-1` 使启动默认档不被任何降档判据命中(行为与改动前一致) |
| 2 | `ApplyMode(mode, requesterId)` 重载(旧单参版转调、保留) | `Mode` 写入点仍**恰一处**(AC-2-17 不破) |
| 3 | `ICameraRig` 加 `SetMode(CameraMode, int)`;**保留**单参重载(记 `-1`) | 既有调用方不破;生产调用方须用带号重载 |
| 4 | `BootRoot` 接线传 `requesterId: 29` | 29 = 死亡过渡(本呈现器归属系统号)。**不传 ⇒ 退出语义静默失效** |

**`RequesterId` 语义升格**(如实登记):它原本注释为「仅审计用」,甲案后**参与裁决**。
⇒ 所有生产调用点必须传**真实**系统号,否则「同一请求方」判据恒假
(退化为旧行为:不崩,但「只能由请求方退出」静默失效)。

**既有夹具的影响**:`test_ac218a_lowerPriorityCannotInterruptHigher` 用**同一个**
`requesterId: 1` 穷举 3×3 ⇒ 其 `Casebook→Explore` 分支现在**会**降档(甲案使然)。
该夹具须同批订正(见 §10)。

### 4.6 甲案**不削弱** AC-2-18①

`Casebook` 仍**不能**被**别的**请求方打断 —— 见新夹具
`test_casebook_differentRequester_stillCannotInterrupt` 与
`test_downed_differentRequester_cannotDowngrade`(两条都是**负控**)。

---

## 4.7 甲案触发的**三处既有夹具订正**(全量回归抓到,定向跑看不见)

| # | 夹具 | 红因 | 处置 |
|---|---|---|---|
| 1 | `camera_mode_machine_test.test_ac218a_lowerPriorityCannotInterruptHigher` | 首版用**同一个** `requesterId: 1` 穷举 3×3 ⇒ 甲案下 `Treatment→Explore` 变成「同请求方可降档」⇒ 与 AC-2-18① 的断言冲突 | **改用两个不同请求方**(`Owner=39` / `Other=10`)。AC-2-18① 的原意就是「**别的系统**的低优先级请求不能打断」(`O-12` 防互抢机位)—— 同号是夹具简化,恰好撞上甲案。**AC-2-18① 未被放宽** |
| 2 | `camera_mode_machine_test.test_ac220_noModeDurationTimer` | 白名单式断言「任何标量字段不在 `allowed` 即红」⇒ `CurrentRequesterId`(int)命中 | 加入 `allowed` 并注明**它不是计时器**:它是请求方系统号(10/29/39…),参与「谁开的这个档」的裁决,与「相机是否自行计时」无关。**守卫的靶(`_idleTicks` 形态)仍会被抓** |
| 3 | `camera_obligations_reconciliation_test.test_vrInterface_firstPersonEnumExists_planeChainIndependent` | `ICameraRig` 现有两个 `SetMode` 重载 ⇒ `GetMethod("SetMode")` 抛 `AmbiguousMatchException` | 改为**按签名取**:同时断言两个重载都在 —— 那才是「ADR-020 的 `SetMode` 完好 **且** 甲案的新入口已落」的完整判据 |

⚠️ **三条都只有全量回归才暴露**(定向只跑 `DaYiJingCheng.Tests.Camera` 时,`CameraViewpoint` 与
`PlayerController` 的夹具不在筛选面内)。又一次「定向跑 ≠ 全量跑」。

⚠️ **另有一处编译期红**(在跑测试之前就挡住):`input_contract_test.cs` 的两个假 `ICameraRig`
(`CountingCameraRig` / `TestCameraRig`)须补 `SetMode(CameraMode, int)` 空实现
—— 接口加成员必然波及所有实现方,这是 `CS0535` 而非运行期红。

## 5. 测试(13 条,`camera_downed_phases_test.cs`)

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_downed_twoPhases_sagThenSettle` | ① 两段式相位序列 + 第一段时长 |
| 2 | `test_notDowned_resetsImmediately` | 未倒下 ⇒ 立即归零(不插值) |
| 3 | `test_downed_monotonic_noJerk` | ② **禁甩镜**:逐帧单调 + 第二段恒 1 |
| 4 | `test_downed_easingHitsEndpointsExactly` | 缓动端点精确(0 / 1) |
| 5 | `test_downed_negativeDtTreatedAsZero` | 负 dt 按 0(不抛) |
| 6 | `test_tilt_doesNotMutatePlayerPitch` | ③ 微倾**不污染玩家输入** |
| 7 | `test_tilt_increasesDownwardPitch` | 微倾进度到位 |
| 8 | `test_downed_priority_belowCasebook_aboveExplore` | 优先级三方关系 |
| 9 | `test_downed_vrInPlace_requestDiscarded` | ④ **VR 全禁**(条件性丢弃) |
| 10 | `test_downed_noVr_requestAccepted` | 上条的**负控**(无 VR ⇒ 生效) |
| 11 | `test_downed_cannotExitViaExplore_currentBehaviorLocked` | **锁住矛盾现状**(见 §4) |
| 12 | `test_downed_sameModeIdempotent` | 同档幂等(AC-2-18②) |
| 13 | `test_placeholderKnobs_areNonZero_butNotRuledValues` | 占位数非零但不断言具体值 |

> ⚠️ 第 11 条**刻意断言「出不去」** —— 它不测「正确的行为」,而是把矛盾**锁成可判**:
> 将来任何人改 `Settle` 判据(往任一方向修)都会让它红,从而强制重新审视。

---

## 6. 数值占位(归用户数值轮)

spec §9 只给**形状**(下沉 + 微倾、两段、禁甩镜),**一个数都没有**。三常量全为占位:

| 常量 | 占位值 | 含义 | 数值轮注意 |
|---|---|---|---|
| `SagSeconds` | 1.2 s | 第一段时长 | 建议与 29 spec Φ1 的水墨渐入(`FallSeconds = 1.2 s`)同量级 —— 二者**同步发生**(spec §6 Φ1),差异过大会「墨满了但相机还在掉」 |
| `SagDepth` | 0.45 m | 下沉幅度 | 须与 `ARM` 距离联动,幅度过大时相机穿地 |
| `SagTiltDegrees` | 6° | 微倾幅度(pitch 增量) | 须远小于 `PITCH_MAX`(60°),否则顶到硬界 |

⚠️ **不得把占位值当成「已裁定数值」在下游引用**(本仓已五次抓过「自述顶替裁定」)。
数值轮改这三处 + 同步 `death-transition-29.md` §14 的调参旋钮表。

---

## 7. 证伪轮(5/5 各转红)—— 含**两轮**,首轮两条逃逸

同命令内「改 → 跑 → 还原」,三文件 SHA-256 逐字节还原。

### 7.1 首轮:3 红 / 2 逃逸(M3、M4 未红)

| 变异 | 内容 | 首轮结果 |
|---|---|---|
| **M1** | 去掉第二段「保持」⇒ 躺定后继续推进 | ✅ 3 红(`monotonic_noJerk` / `easingEndpoints` / `tilt_increases`) |
| **M2** | 未倒下不归零 | ✅ `notDowned_resetsImmediately` |
| **M3** | 微倾直接写 `_pitch`(污染输入) | ❌ **0 红 —— 假绿** |
| **M4** | 去掉下沉位移 | ❌ **0 红 —— 覆盖缺口** |
| **M5** | `Downed` 优先级 2 → 3 | ✅ `priority_belowCasebook_aboveExplore` |

### 7.2 两条逃逸的根因(都是**我的夹具缺陷**,不是生产 bug)

**M3 —— 断言与变异点不在同一条路径(F8 形态)**
`test_tilt_doesNotMutatePlayerPitch` 只调 `rig.Tick()`,而微倾的叠加点在
`GetCameraPosition()` 体内 ⇒ **被变异的那条路径从未执行** ⇒ 断言恒绿。
修:夹具循环里补 `rig.GetCameraPosition(target)`。

**M4 —— 断言落在纯结构上,效果落在位姿上**
相位机夹具测的是 `SagFraction`(分数);分数正确 ≠ 位移真的发生。
**首版补的判据也是错的**:拿「pitch=0 的 Explore 基线」与「躺定的 Downed」比,
而躺定时微倾把 pitch 抬到 6° ⇒ `offset.y = sin(6°)×distance ≈ +0.49 m`,
**几乎完全抹平下沉 -0.45 m**(实测首跑:downedY=0.073 vs baselineY=0.000)。
修:**隔离下沉这一支** —— 用两台 rig,基线那台把 `_pitch` 手动设成躺定时的
**有效 pitch**(`SagTiltDegrees`),使两次微倾贡献相同 ⇒ 差值恰为 `SagDepth`。

### 7.3 第二轮(补强后)

| 变异 | 结果 |
|---|---|
| **M3** | ✅ `test_tilt_doesNotMutatePlayerPitch` 转红 |
| **M4** | ✅ `test_sag_actuallyMovesCameraDown` 转红 |

还原哈希(与变异前逐字节相同):

```
0745768149a713760d336e4282cf456217ffeadb9d2db0649abbf554808542bc  CameraDownedPhases.cs
657b286138a5b731428d168e8d3970f4ca26f123c6bff5761bdbe22c760fa8f0  CameraRig.cs
eaf9bd4b59e8bfeccccfc37da1f51ab6f493bb4eb6823a50b2d5ba7ca1bca59e  CameraModeMachine.cs
```

> ⚠️ **这一轮的价值恰在于首轮逃逸**:若只跑首轮就宣布「证伪通过」,
> 两条夹具会以**绿**的形式留下却什么都没测 —— 那正是 `coding-standards` F8 与
> `tools/falsify/README.md`「假存活」防的东西。

---

## 7.5 甲案证伪轮(3/3 各转红,哈希逐字节还原)

同命令内「改 → 跑 → 还原」,靶 = `CameraModeMachine.Settle` 的同请求方判据。

| 变异 | 内容 | 转红 |
|---|---|---|
| **M1** | 去掉「同请求方可降档」(退回旧行为 ⇒ 出不去) | `test_downed_sameRequester_canExitViaExplore` + `test_casebook_sameRequester_canExitViaExplore` |
| **M2** | 放开成「**任意** Explore 可降档」(不限定请求方 ⇒ 会互抢) | `test_downed_differentRequester_cannotDowngrade` + `test_ac218a_lowerPriorityCannotInterruptHigher` |
| **M3** | `CurrentRequesterId` 不记录(恒 `-1` ⇒ 判据恒假) | 同 M1 两条 |

⚠️ **M1 与 M2 红在**不同**的测试上** —— 那正是甲案两个方向都被钉住的证据:
「同请求方**能**退出」与「异请求方**不能**抢」各有一组判据,任一侧改坏都会红。

还原哈希: `1ef3bb7ec8f1a923e95c5375dda14ec1f4bb3f1338d14ac809c74b82bf6bb1c6`(逐字节相同)

⚠️ **首轮三个变异全 NO-XML** —— 原因是我把证伪轮与 PlayMode 全量回归**并行**跑了,
Unity 工程被占用。**这是调度失误,不是变异失败**。等 PlayMode 结束后重跑才得到上述结果。
(如实登记:NO-XML 不是绿,是**没跑成** —— 承既有口径。)

## 8. 回归(三元组)

| 项 | 值 |
|---|---|
| **截至时刻** | 2026-11-01 **22:47 CST**(EditMode,甲案后)/ **22:5x CST**(PlayMode) |
| **批次** | S24 相机两段式(`CameraMode.Downed`) |
| **commit** | 待提交(本批) |

| 套件 | 结果 | 前次 | 等式 |
|---|---|---|---|
| EditMode(甲案前) | `total=6469 passed=6432 failed=0 skipped=37` | `6455 / 6418 / 0 / 37` | `6469 − 6455 = 14` ✅(新增 14 测) |
| **EditMode(甲案后)** | **`total=6472 passed=6435 failed=0 skipped=37`** | `6469 / 6432 / 0 / 37` | `6472 − 6469 = 3` ✅(甲案新增 3 测:1 正控 + 2 负控) |
| PlayMode | `total=274 passed=270 failed=0 skipped=2` | `274 / 270 / 0 / 2` | 持平 ✅(本批未加 PlayMode 测试) |

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/em-cam.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/pm-cam.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
```

---

## 9. 仍未做(不借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| **`Downed` 出不去**(§4 的矛盾) | **如实登记,待用户裁定** | 玩家复活后相机停在倒下档 —— 与 `Casebook` 同一机制,修法三选一见 §4.4 |
| **`death-and-respawn.md:491` 引据错误** | 未修 | 「承 25 `:1639`」应为 `combat-and-weapon-lines.md:1665`;归 29 的回写轮 |
| **数值**(`SagSeconds` / `SagDepth` / `SagTiltDegrees`) | **占位** | 归用户数值轮(§6) |
| **醒来段的机位**(Φ3 床格视角) | 未做 | spec §6 Φ3 是**另一条相机线**,归机位轮;本批只做「倒下」 |
| **图形走查**(下沉观感是否合 spec) | **NOT-RUN** | 机械判据只覆盖「位移发生」,不覆盖「看起来像失去支撑」 |
| **AC-29-11 / AC-29-12**(`[L]` 节奏 / 听测) | **NOT-RUN** | 承既有口径不变 |
