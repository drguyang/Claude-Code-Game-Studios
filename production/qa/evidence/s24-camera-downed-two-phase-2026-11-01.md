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
| `Camera/CameraRig.cs` | `Tick` 推进相位机;`GetCameraPosition` **只**叠加下沉;`GetLookTarget` 承载微倾(**§6.5 订正后**:初版曾把微倾折进有效 pitch) |
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

### 3.2 微倾不污染玩家的 `_pitch`

⚠️ 若把 `SagTiltDegrees` 直接累加进 `_pitch`,玩家醒来后会发现俯角莫名变了且调不回去。
夹具 `test_tilt_doesNotMutatePlayerPitch` 钉这一条。

> 🔁 **2026-11-01 订正**:本节原写「用 `effectivePitch = clamp(_pitch + tilt, …)`」——
> 那个实现**方向是反的**(pitch 增大会把相机抬高),已废。现微倾走
> `CameraRig.GetLookTarget()`,**根本不碰 pitch**。详见 §6.5。

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

## 5. 测试(**17 条**,`camera_downed_phases_test.cs`)

> 🔁 **2026-11-01 订正**:本节原写「13 条」并列出 13 项 —— 那是**甲案之前**的账面。
> 甲案 +3 条、微倾方向修正把第 7 条**改写**为第 8 条(1 换 1)、并新增 `test_sag_actuallyMovesCameraDown`。
> 现为 **17 条**,逐条见下。

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_downed_twoPhases_sagThenSettle` | ① 两段式相位序列 + 第一段时长 |
| 2 | `test_notDowned_resetsImmediately` | 未倒下 ⇒ 立即归零(不插值) |
| 3 | `test_downed_monotonic_noJerk` | ② **禁甩镜**:逐帧单调 + 第二段恒 1 |
| 4 | `test_downed_easingHitsEndpointsExactly` | 缓动端点精确(0 / 1) |
| 5 | `test_downed_negativeDtTreatedAsZero` | 负 dt 按 0(不抛) |
| 6 | `test_tilt_doesNotMutatePlayerPitch` | ③ 微倾**不污染玩家输入**(M3 逃逸后补 `GetCameraPosition` 调用) |
| 7 | `test_sag_actuallyMovesCameraDown` | 下沉**真的**作用到位置:`−Δy` **恰等于** `SagDepth`(M4 逃逸后补) |
| 8 | `test_tilt_dropsLookTarget_doesNotRaiseCamera` | **微倾方向**:① 视线目标下移 ② 相机**不被抬高** ③ 常态零偏移<br>(**改写自** `test_tilt_increasesDownwardPitch` —— 那条是 F4 空洞正控,只断 `Is.Not.NaN`) |
| 9 | `test_downed_priority_belowCasebook_aboveExplore` | 优先级三方关系 |
| 10 | `test_downed_vrInPlace_requestDiscarded` | ④ **VR 全禁**(条件性丢弃) |
| 11 | `test_downed_noVr_requestAccepted` | 上条的**负控**(无 VR ⇒ 生效) |
| 12 | `test_downed_sameRequester_canExitViaExplore` | **甲案正控**:同请求方**能**降档 |
| 13 | `test_downed_differentRequester_cannotDowngrade` | 甲案负控:异请求方**不能**抢 |
| 14 | `test_casebook_sameRequester_canExitViaExplore` | 甲案在 `Casebook` 上的同一判据 |
| 15 | `test_casebook_differentRequester_stillCannotInterrupt` | 上条负控 |
| 16 | `test_downed_sameModeIdempotent` | 同档幂等(AC-2-18②) |
| 17 | `test_placeholderKnobs_areNonZero_butNotRuledValues` | 数非零但**不断言具体值** |

> ⚠️ **原第 11 条(`test_downed_cannotExitViaExplore_currentBehaviorLocked`)已随甲案删除** ——
> 它当时**刻意断言「出不去」**以把矛盾锁成可判;矛盾裁定后,锁的价值由第 12–15 条**双向**接替
> (「同请求方能退」与「异请求方不能抢」各有一组),判别力更强。

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Camera/camera_downed_phases_test.cs   # 期望 17
```

---

## 6. 数值 —— 已由用户数值轮裁定(2026-11-01)

spec §9 只给**形状**(下沉 + 微倾、两段、禁甩镜),**一个数都没有**。三项已于
2026-11-01 由用户数值轮裁定并回填:

| 常量 | **裁定值** | 原占位 | 含义 | 依据 |
|---|---|---|---|---|
| `SagSeconds` | **1.2 s** | 1.2 s(不变) | 第一段时长 | 与 29 spec Φ1 的水墨渐入(`FallSeconds = 1.2 s`)**同步发生**(spec §6 Φ1),两条线一起收尾 ⇒ 无「墨满了但相机还在掉」的割裂 |
| `SagDepth` | **0.45 m** | 0.45 m(不变) | 下沉幅度 | 相机 2.5 m → 2.05 m(降 18%)。**对屏幕位移几乎无影响**(见下),主要提供视点下坠的物理感;无穿地风险 |
| `SagTiltDegrees` | **10°** | 6° → **10°** | 微倾幅度 | 观感的**主导项**。6° 实测只换来 6.65 pp 屏高位移(**偏轻**,像「身子一软」不像「失去支撑」);10° **实测 12.6 pp**(模型预测 12.72,**误差 0.8%**),≈ 1.9 倍,仍落在 spec §9「小幅度机位运动」内 |

> ⚠️ **`SagDepth` 为什么不动**(2026-11-01 几何模型 + 图形走查实测,反直觉):
> 0.45 → 1.30 m 时,屏幕位移只从 **−7.4 pp** 变到 **−6.4 pp**(甚至略减)。
> 因为 `LookAt(target)` 会把「相机变低 ⇒ 目标在视野里相对升高」**自动补偿掉** ——
> 下沉的视觉贡献几乎被视线补偿吃干净。**要让读数变强,唯一有效的杠杆是 `SagTiltDegrees`。**
> 这使「下沉 + 微倾」两词在**屏幕读数**上的权重并不对称:前者是**体感**
>(相机高度变了),后者才是**画面读数**。
>
> 模型标定:两个独立测量点(基线 42.4% / 落定 35.5%)反解出的地板远缘距离
> 分别为 1.03 m / 0.94 m(自洽);现占位模型 −7.44 pp vs 实测 −6.65 pp,**误差 12%**
> ⇒ **只用于比大小,不用于定绝对值**。

复算(几何模型,自包含):

```bash
python3 - <<'PY'
import numpy as np
D=5.0; PITCH=np.radians(30.0); FOV=np.radians(60.0); t=np.tan(FOV/2)
def frac(cam, look, P):
    f=np.array(look)-np.array(cam); f/=np.linalg.norm(f)
    r=np.cross(f,[0.,1.,0.]); r/=np.linalg.norm(r); u=np.cross(r,f)
    v=np.array(P)-np.array(cam)
    return 0.5-(v.dot(u)/(v.dot(f)*t))/2
def cfg(sag=0.0,tilt=0.0):
    return (np.array([0.,D*np.sin(PITCH)-sag, D*np.cos(PITCH)]),
            np.array([0., -np.tan(np.radians(tilt))*D, 0.]))
d=1.0   # 由两个实测点反解(1.03 / 0.94)取中
c0,l0=cfg()
for s,th in ((0.45,0),(0,6),(0.45,6),(0.45,10),(0.45,12),(1.30,6)):
    c,l=cfg(s,th)
    print(f"sag={s:4.2f} tilt={th:4.1f} => {100*(frac(c,l,(0,0,-d))-frac(c0,l0,(0,0,-d))):+7.2f} pp")
PY
```

⚠️ **若 `SagDepth` / `SagSeconds` 将来再改**,数值的**唯一实体**是
`CameraDownedPhases.cs` 的三个常量 + 本表;`design/ux/death-transition-29.md` **没有**
调参旋钮表可同步(其 §14 是 *Open Questions*;**OQ-2「死亡过渡时长数值未定」指的是
水墨 `FallSeconds` 那一族**,与本机位三常量不是同一组数 —— 勿混读)。

---

## 6.5 图形走查:微倾方向反了(已修 · 2026-11-01)

### 6.5.1 症状

机械夹具全绿(位移「发生了」),但**观感读不出来**。图形模式产图实测:

| | 相机 Y 位移 |
|---|---|
| 下沉贡献 | −0.45 m |
| 微倾贡献(**旧做法,走 `_pitch`**) | **+0.49 m** |
| 净效果 | +0.04 m ⇒ 地平线只动 **2.6% 屏高** |

### 6.5.2 根因是**结构性**的,不是符号写错

- `BootRoot` 的 `cam.transform.LookAt(target)` **锁死了相机朝向** ⇒ `CameraRig`
  无法独立控制朝向;
- 第三人称绕点式里 `offset.y = sin(pitch) × distance` ⇒ **pitch 增大会把相机抬高**;
- ⇒ 「微倾」实际在把相机**往上顶**,与下沉对着干,两者互相抵消。

⚠️ 这解释了为什么 §7.2 的 M4 首版夹具**测不出**下沉 −0.45 m:它拿「pitch=0 基线」比
「躺定 Downed」,而旧实现的微倾把 pitch 抬到 6° ⇒ `sin(6°)×5 ≈ +0.49 m`,
几乎完全抹平。**当时修的是夹具(隔离两支),没意识到方向本身是错的** ——
夹具隔离让断言变精确,却也**把「两支互相抵消」这个真问题掩掉了**。
（教训同 `coding-standards` F8 的近亲:夹具改精确 ≠ 生产行为正确。）

### 6.5.3 修法

微倾改走 **`CameraRig.GetLookTarget(Vector3 target)`**(新增):

```
dropMeters = tan(SagTiltDegrees) × distance × TiltFraction
return new Vector3(target.x, target.y - dropMeters, target.z)
```

`BootRoot` 改 `cam.transform.LookAt(_cameraRig.GetLookTarget(target))`;
`GetCameraPosition` **只受下沉影响**(`effectivePitch` 折入已删)。

- ✅ **单一路径**:喂 `LookAt` 的目标点是 `CameraRig` 可控的,不开第二份朝向真源
  (ADR-020 §五:相机只持不写)。
- ✅ **不绕人转**:偏移只沿世界 −Y,无水平分量 ⇒ 非视场旋转(spec §9 晕动评估)。

### 6.5.4 修后实测(几何量取地板远缘)

两条曲线:**修方向前**(`SagTiltDegrees = 6°`)与**数值裁定后**(`= 10°`)。

| 时刻 | sag 进度 | 6° 地板远缘 y | 占屏高 | **10° 地板远缘 y** | **占屏高** |
|---|---|---|---|---|---|
| 常态 | 0 | 364 | 42.4% | 364 | 42.4% |
| 下沉中 | 0.3 | 345 | 40.2% | **329** | **38.3%** |
| 下沉中 | 0.6 | 329 | 38.3% | **299** | **34.8%** |
| 下沉中 | 0.9 | 311 | 36.2% | **267** | **31.1%** |
| 躺定 | 1.0 | 305 | 35.5% | **256** | **29.8%** |

| | 总位移 | |
|---|---|---|
| **修方向前**(旧做法走 `_pitch`) | **2.6 pp** | 微倾把相机抬高、与下沉抵消 |
| 修方向后 · 6°(原占位) | **6.65 pp** | 偏轻,像「身子一软」 |
| **修方向后 · 10°(已裁定)** | **12.6 pp** | 约 4.8 倍于修前;模型预测 12.7 pp,误差 0.8% |

> 📌 **模型在同一次改动内被验证了一次**:6° 时模型 −7.44 vs 实测 −6.65(误差 12%);
> 10° 时模型 −12.72 vs 实测 **−12.6**(误差 **0.8%**)。⇒ §6 那张「`SagDepth` 几乎无效、
> `SagTiltDegrees` 是主导项」的表**可按比例采信**。

⚠️ **10° 的副作用(如实登记)**:`dropMeters = tan(10°) × 5 ≈ 0.88 m` —— 视线目标落在
**玩家原点下方 0.88 m**。躺定时画面上部被后墙占去、玩家胶囊顶端出画。
因这段过渡随即被水墨覆盖(中央保底墨 `MinInk = 0.68`),**实测不受影响**;
但若将来把两段式复用到水墨不覆盖的场合,须重新评估取景。

图非重复帧(10° 版 sha256 各异;两两平均像素差 0.92–11.82):

```
05ff6e9d3815 cdw_00_explore   06c53fc29e99 cdw_01_sag_030   e3d93520cf4e cdw_02_sag_060
1374891b1d23 cdw_03_sag_090   b1963cc4a842 cdw_04_settled
```

> ✅ `cdw_00_explore.png` 的哈希在 **6° 与 10° 两轮里完全相同**(`05ff6e9d3815`)
> —— 常态基线不受微倾影响,证明改动只作用于倒下态、**没污染常态取景**。

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Logs/shots
python3 - <<'PY'
from PIL import Image; import numpy as np, hashlib, itertools
names=["cdw_00_explore","cdw_01_sag_030","cdw_02_sag_060","cdw_03_sag_090","cdw_04_settled"]
a={n:np.asarray(Image.open(n+".png").convert("RGB")).astype(int) for n in names}
for n in names:
    im=np.asarray(Image.open(n+".png").convert("RGB")).astype(int)
    R,G,B=im[:,:,0],im[:,:,1],im[:,:,2]
    green=(G>R+6)&(G>B+2)&(G>60)
    tops=[np.argmax(green[:,c]) for c in range(80,180) if green[:,c].any()]
    print(f"{n}: 地板远缘 y={int(np.median(tops))} sha={hashlib.sha256(open(n+'.png','rb').read()).hexdigest()[:12]}")
for x,y in itertools.combinations(names,2):
    print(f"{x} vs {y}: {np.abs(a[x]-a[y]).mean():.2f}")
PY
```

### 6.5.5 观感判读(**视觉模型直读**,非机械判据)

| 版本 | 直读结论 |
|---|---|
| 修方向后 · **6°** | 能看出相机矮下去了(**方向对了**),但读作「身子一软」,**不像失去支撑** |
| 修方向后 · **10°** | 地板占比明显增加(42.4% → 29.8%)、后墙带变厚、玩家胶囊整体上移并逼近画面上缘。<br>**「失去支撑」这一读法立住了**;仍在 spec §9「小幅度机位运动」内(总位移 12.6 pp ≈ 1/8 屏高) |

⚠️ **这只是「视觉模型看到了什么」,不是 `[L]` 人工签核。**
`AC-29-11` / `AC-29-12` 维持 `NOT-RUN` —— 观感是**人的**判断,须桌面批人工走查 + 听测。

---

## 6.6 本件自己的两处缺陷(如实登记)

### 6.6.1 `FsWriteBoundaryGate` 具名声明漏登 —— 同形态**第五例**

`camera_downed_visual_walkthrough.cs` 用 `Application.dataPath` 推项目根 + 写
`unity/Logs/shots/`,按 `test-standards.md` 须在 `FsWriteBoundaryGate.Declarations` 具名声明。
**起草时又没想起本门**,由真树断言 `test_realTestTree_zeroRed` 第五次点名
(前四例见该文件 `:100-108` 的自陈「**肌肉记忆仍未形成**」)。

⇒ 已补声明(理由段同时把「第五例」写进该文件,不再只写在证据里)。

### 6.6.2 世界场景泄漏 —— `main_menu_wiring_test` 定序判据转红

首轮全量 PlayMode:`275 / 270 / **1** / 2`,唯一红 =
`main_menu_wiring_test.test_bootWithMenu_showsSingleRow_andLandsFocus`
其定序判据「主菜单必须出现在**世界装载之前**」读到 **World 已装载**。

**根因**:本件走过真启动序 ⇒ 以 **additive** 方式装载了 Addressables 的 World 场景;
而 **additive 场景不会被 `Object.Destroy` 或测试框架自动卸载** ⇒ 留给了后续测试。
NUnit 按全名序跑,`CameraDownedVisualWalkthrough` 排在 `main_menu_wiring_test` **之前**。
**定向单跑绿、全量串跑红** —— 承 sprint-23 记的同一形态
(「全量回归九轮才全绿,前 8 轮全是测试隔离缺陷」)。

**修**:TearDown 里反射取 `BootRoot._worldLoadHandle` 并
`Addressables.UnloadSceneAsync(handle)`,承既有 10 件 boot 夹具同款范式;
同时补齐「Presenter 是独立 GameObject 须显式销毁」与「预摆最后拆」两条次序纪律。

⚠️ **这不是新知识** —— 该范式在本仓已存在 10 处。**是我写新件时没照抄既有 boot 夹具的 TearDown。**

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

---

## 7.6 接线判据证伪轮(2026-11-01 · 微倾方向修正后补)

微倾改走 `GetLookTarget` 后,**`CameraRig` 内部的 EditMode 夹具已证明不了接线**:
`BootRoot` 把位姿落到 `cam.transform` 那一刻改写回 `LookAt(target)`,`CameraRig` 的
全部夹具**仍绿**。⇒ 那是「行为等价掩盖结构分叉」(F8)的教科书形态,须单独证伪。

| 变异 | 内容 | 结果 |
|---|---|---|
| **M6** | `cam.transform.LookAt(_cameraRig.GetLookTarget(target))` → `LookAt(target)`(退回旧接线) | ✅ `test_downed_twoPhase_producesWalkthroughShots` **恰 1 条转红** |

失败报文(实测):

```
躺定时相机朝向 y 分量(-0.428)须明显低于「微倾被丢弃」时的重建值(-0.428)。
  Expected: less than -0.527896643f
  But was:  -0.427896649f
```

**两个数字同值(-0.428)** —— 正是「微倾被静默丢弃」的签名。

生产构建下的实测值(同一条日志行):

```
[CDW] 接线判据: ΔY=-0.539 m, forward.y 基线 -0.500 / 躺定 -0.561 / 去微倾重建 -0.428
```

- `基线 -0.500` = `−sin 30°`,即初始 pitch 30° 的朝向 ✅
- `躺定 -0.561` vs `去微倾重建 -0.428` ⇒ **差 0.133**,阈值 0.10,**余量 1.33×**
  (⚠️ 起草时写「约 1.7× 余量」是**估算**,实测只有 1.33× —— 已就地订正夹具报文)
- `ΔY = -0.539 m` 大于 `SagDepth = 0.45` —— 多出的 0.089 m 是玩家自身在两个采样点之间的
  沉降,**故 ΔY 判据的阈值取 0.5×`SagDepth`**(粗判「下沉落到变换上了」),
  精确的 `SagDepth` 等式由 EditMode 的 `test_sag_actuallyMovesCameraDown` 钉住。

还原:`33e0ff2f0620c224bf5fae89ae26248d3934e9c224d63a29ee65cba63282a82d`
逐字节一致;`MUTANT-M6` 残留计数 = 0。
(如实登记:NO-XML 不是绿,是**没跑成** —— 承既有口径。)

## 8. 回归(三元组)

| 项 | 值 |
|---|---|
| **截至时刻** | 2026-11-01 **23:24:25 CST**(EditMode)/ **23:25:09 CST**(PlayMode) |
| **批次** | S24 相机两段式(`CameraMode.Downed`)· 微倾方向修正 + 数值裁定 10° |
| **commit** | 待提交(本批) |

| 套件 | 结果 | 前次 | 等式 |
|---|---|---|---|
| EditMode(甲案前) | `6469 / 6432 / 0 / 37` | `6455 / 6418 / 0 / 37` | `6469 − 6455 = 14` ✅(新增 14 测) |
| EditMode(甲案后) | `6472 / 6435 / 0 / 37` | `6469 / 6432 / 0 / 37` | `6472 − 6469 = 3` ✅(甲案 1 正控 + 2 负控) |
| **EditMode(微倾修正后)** | **`6472 / 6435 / 0 / 37`** | `6472 / 6435 / 0 / 37` | **`0`** ✅(无新增 —— `test_tilt_increasesDownwardPitch` **改写**为 `test_tilt_dropsLookTarget_doesNotRaiseCamera`,**1 换 1**) |
| PlayMode(修正前) | `274 / 270 / 0 / 2` | — | — |
| **PlayMode(修正后)** | **`275 / 271 / 0 / 2`** | `274 / 270 / 0 / 2` | `275 − 274 = 1` ✅(新增 `CameraDownedVisualWalkthrough` 1 条) |

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/em-cam.xml').getroot();print('EM',r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/pm-cam.xml').getroot();print('PM',r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
```

⚠️ **中间有一轮红(已修,如实留档)**:首轮全量 PlayMode `275 / 270 / 1 / 2`,红项 =
`main_menu_wiring_test`(世界场景泄漏,见 §6.6.2)。修后重跑才是上表的值。

---

## 9. 仍未做(不借绿)

| 项 | 状态 | 说明 |
|---|---|---|
| ~~**`Downed` 出不去**(§4 的矛盾)~~ | **✅ 已结(甲案)** | 同请求方可降档,见 §4.4 / §7.5 |
| ~~**数值**(`SagSeconds` / `SagDepth` / `SagTiltDegrees`)~~ | **✅ 已裁定 2026-11-01** | 1.2 s / 0.45 m / **10°**,见 §6 |
| ~~**微倾方向反了**~~ | **✅ 已修 2026-11-01** | 根因 = 朝向被 `LookAt(target)` 锁死 + `offset.y = sin(pitch)×d`;见 §6.5 |
| **`death-and-respawn.md:491` 引据错误** | 未修 | 「承 25 `:1639`」应为 `combat-and-weapon-lines.md:1665`;归 29 的回写轮 |
| **`GetLookTarget` 未进 `ICameraRig`** | **如实登记** | 微倾的承载点 `CameraRig.GetLookTarget(target)` 只是**具体类**成员,接口上**没有**。今天唯一的消费者 `BootRoot` 持具体类型(`private CameraRig _cameraRig`),故能用;但任何 `ICameraRig` 消费者都会写 `cam.transform.LookAt(target)` ⇒ **微倾静默丢失**。<br>不加进接口的理由:ADR-020 §Key Interfaces 把接口钉成「四成员」,加第五个须动 ADR —— **不在本批越权**。归机位轮 / ADR-020 窄修订 |
| 🔴 **2 的 GDD `R-2-5` 档位表未登记 `Downed`** | **未修,须用户裁定** | `camera-and-viewpoint.md` 的 R-2-5 档位表(`:170-178`)只有 **10 / 39** 两个请求方,优先级式(`:202`)只有 `Casebook > Treatment > Explore`。而代码已有第五档 `CameraMode.Downed`(优先级 2,与 `Treatment` 同格)+ 请求方 **29**(`DeathTransitionPresenter`)。<br>⚠️ 这正是本仓反复抓过的「**引用却无登记**」形态;不过本批只被授权「修微倾方向 + 回填三个数」,**写 GDD 档位表须先经用户批准**(collaborative protocol)。<br>建议补法:① R-2-5 表加一行 `29 死亡过渡 → Downed → 倒下两段式第一段 → PlayerDied 落流`;② 优先级式改为 `Casebook > Downed(= Treatment) > Explore`;③ §9 的「三档」措辞(`:29` / `:129`)订正为五档。<br>⚠️ **不得**只改代码不改表 —— 那会让「档位的唯一真源」变成 `CameraModePriority.cs`。 |
| **醒来段的机位**(Φ3 床格视角) | 未做 | spec §6 Φ3 是**另一条相机线**,归机位轮;本批只做「倒下」 |
| **图形走查**(下沉观感是否合 spec) | **已跑(视觉模型直读)** | §6.5.4 / §6.5.5;⚠️ **不是** `[L]` 人工签核 |
| **`AC-29-11` / `AC-29-12`**(`[L]` 节奏 / 听测) | **NOT-RUN** | 承既有口径不变 —— 需用户桌面批人工走查 + 听测签核 |
