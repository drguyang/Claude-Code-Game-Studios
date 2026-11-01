# Story 011: 移动压制租约的生产接线 —— 10 置位 / 清位 `LeaseSource.Emergency`,并让 1 真的读它

> **Epic**: 10 急救动作
> **Status**: **Complete ✅ 2026-11-02**(**COMPLETE WITH NOTES** —— 见 §Completion Notes;9 条 AC 全证,
> 但 §七 的三项证明不了的事仍开着)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 1.5–2 人日(约 12–16h)
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-02

> ✅ **2026-11-02 用户裁定 R-11.4 取甲案**(原话「甲,开工」)⇒ 1 的读面**一并接**:
> `PlayerController.Move` 读 `MotorLease.IsSuppressed`。至此本卡无待决分叉。
> ⚠️ 另一项(R-11.2 直绑 `MotorLease` 而非 `Func<>`)仍属立件轮拟定、实现轮沿用,
> 见 §用户批准记录。

---

## Context

**GDD**: `design/gdd/emergency-procedures.md`
- **规则九**(`:374`)「移动抑制 —— 动作期间玩家不动」:动作**开始** → 10 置位 1 的 `MotorSuppressed`;
  动作**结束**(含跳过)→ 清位;**1 不过问原因**(它只认位)
- **规则六之甲**(`:724`)中止行:清 `MotorSuppressed`
- **`AC-10-13`**(`:1056`)`BLOCKING` · `[A]`:动作期间 `MotorSuppressed` **被置位**且结束时清除(含跳过 / 中止)

**Requirement**: **`TR-emergency-012`**(`status: covered` —— ⚠️ 该状态**与本卡实测不符**,见 §现状实测)
> 「动作期间置位 1 的 MotorSuppressed,结束(含跳过)清位;1 不过问原因」

**ADR Governing Implementation**:
- **ADR-020 §一**(主):移动抽象与压制位形状(`IPlayerMotor` / `MotorLease` 落位 `Gameplay.Presentation`)
- **GDD 1 轴 4**(`:271-279`)+ **`OQ-4-13`**:单 bool → **per-source 位图 + OR 聚合**,`LeaseSource` 闭集
  **恰 = `AC-1-23` 白名单三项**(`Self`=4 / `Emergency`=10 / `Combat`=25);**29 不占本位图**
- **`AC-1-23`**:三个写者**互不知晓**,抑制请求独立发出、由 1 求和
- **ADR-013 Amendment A**:`IArmedState`(10 的 Armed 只读契约;story-009 已实现)

**ADR Decision Summary**:压制是**位图 + OR 聚合**,不是单个 bool —— 单 bool 下三个互不知晓的写者会
**丢失更新**(4 置位 → 10 置位 → 4 清位 ⇒ 清掉了 10 的压制)。每个写者**只碰自己那一位**,
幂等(同源重复 Acquire 不计数),**无到期**(压制期由持有者自己的规则说了算)。
作用域 = **水平位移 + Jump,不含攻击**(1 不得扩语义)。1 **不过问原因** —— 它只读聚合位。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 位运算与相位回调,零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- **Required**: 每源只碰自己那一位(`LeaseSource.Emergency`);Acquire/Release **成对**且覆盖三条结束出口;
  结束清位**不得**以「有事件」为条件(零样本签约同样是动作结束)
- **Forbidden**: `SetSuppressed(bool)` 形态(丢失更新入口 —— `modal_gate_test` 已有反射负断言);
  10 扩语义到攻击;10 读 4 / 25 的位做判定(三者互不知晓);用 `ModalPhaseResult.MotorSuppressed`
  这个**返回值**充当 AC 载体(F1)
- **Guardrail**: `LeaseSource` 闭集 **append-only**(ADR-014 ordinal 纪律,已用值永不复用);
  `Gameplay.Boot` 引用集不变

---

## 现状实测(2026-11-02 立件前勘察 · 五条命令逐条可跑)

| # | 事实 | 命令 | 实测 |
|---|---|---|---|
| ① | `MotorLease` **零生产实例化** | `grep -rn "new MotorLease()" unity/Assets --include=*.cs \| grep -vc "/Tests/"` | **`0`** |
| ② | `MotorLease` 在自身文件之外的生产命中 **恰 2 处**,且**都在 `ModalGate` 的注释/形参**里,无持有者 | `grep -rn "MotorLease" unity/Assets --include=*.cs \| grep -v "/Tests/" \| grep -vc "Player/MotorLease.cs"` | **`2`**(`ModalGate.cs:131` 注释 / `:136` 形参类型) |
| ③ | `ModalGate.SetMotorSuppression`(4 侧入口)**零生产调用** | `grep -rn "SetMotorSuppression" unity/Assets --include=*.cs \| grep -v "/Tests/"` | 只有定义那一行 |
| ④ | `LeaseSource.` 在 `MotorLease.cs` / `ModalGate.cs` 之外 **零生产引用** | `grep -rn "LeaseSource\." unity/Assets --include=*.cs \| grep -v "/Tests/" \| grep -v "Player/MotorLease.cs" \| grep -v "Interaction/ModalGate.cs"` | **零输出** |
| ⑤ | `PlayerController`(1)内 **`Suppress` 零命中** | `grep -c "Suppress" unity/Assets/Gameplay.Presentation/Player/PlayerController.cs` | **`0`** |

⇒ **规则九的运行期可达性 = 0%**:没有任何东西创建租约,没有任何东西置位/清位,
`PlayerController.Move` 也从不读它。`AC-10-13` 今日由 `modal_phase_test.cs` 的 **3 条**
(④ 处)对 `ModalPhaseResult.MotorSuppressed` 的断言顶替 —— 那断的是**求值器自己返回的 bool**,
工厂里写死、改坏生产也不会红。**与 story-010 的 `ExploreCount` 是同一个 F1 形态**。

> ⚠️ **本卡与 story-010 的关系**:story-010 把「10 的相机意图」接上了,本卡接的是**同一个相位钩子上的
> 第二个效应**(移动压制)。story-010 在 `Arm` / `EndAction` / `AbortAction` / `SkipAction` 上
> 建立的四条出口**可直接复用** —— 本卡不重建钩子,只在同四处加 Acquire/Release。

---

## Detailed Rules(实现口径)

**R-11.1 租约实例归 1(`PlayerController`),10 只拿引用。**
GDD 轴 4 明写「租约(位图 + OR 聚合)归 1(玩家控制器)」⇒ `PlayerController` 新增:

```csharp
private readonly MotorLease _motorLease = new MotorLease();   // 1 的私有真源
public MotorLease MotorLease => _motorLease;                   // 只读暴露给装配层
```

⚠️ **不得**让 10 自己 `new MotorLease()` —— 两份位图 = 两个真源,4 / 25 与 10 会各压各的。
⚠️ 暴露**只读属性**而非字段:外部拿到句柄后只能调 `Acquire/Release`(只碰自己的位),
拿不到「整体重置」的入口。

**R-11.2 绑定形态:`BindMotorLease(MotorLease)` 直绑可空引用**(与 `BindCameraRig` 的差异是有意的)。

| 维度 | `BindCameraRig(Func<ICameraRig>)` | `BindMotorLease(MotorLease)` |
|---|---|---|
| 为什么是这个形状 | 机位会被**重建 / 拆卸置 null** ⇒ 必须懒求值 | 租约是 `PlayerController` 的**字段**,生命周期与玩家一致,**不会被换掉** |
| 漏绑后果 | 不发档位意图 | 不置位 ⇒ 玩家不被压制 |
| 观测面 | `HasCameraRig` | `HasMotorLease`(同名同义) |

⚠️ 两者都**必须**有「漏绑」观测面 + 生产启动序断言 —— 「没绑」不崩不报错,是本类失败的**唯一**形态。

**R-11.3 四出口成对置位/清位(与 story-010 的档位意图**同四出口**)。**

| 出口 | 动作 | 依据 |
|---|---|---|
| `Arm` **成功后** | `Acquire(LeaseSource.Emergency)` | 规则九「动作开始置位」 |
| `EndAction`(原 Armed) | `Release(LeaseSource.Emergency)` | 规则九「结束清位」 |
| `AbortAction`(原 Armed) | `Release(LeaseSource.Emergency)` | 规则六之甲 `:724`「清 MotorSuppressed」 |
| `SkipAction` | `Release(LeaseSource.Emergency)` | 规则九「结束(**含跳过**)清位」 |

⚠️ **`Release` 幂等、无害**:`MotorLease.Release` 对未持有的源是 no-op(无下溢)。
⚠️ **`EndAction` 的清位不得以 `agg != null` 为条件** —— 零样本签约同样是动作结束
(与 story-010 的 `Explore` 同一条纪律,M-系列变异会专打这一点)。

**R-11.4 ⚠️ 1 的**读**面:本卡**是否**一并接上?—— 立件轮建议「一并接」,理由同 Tick。**

事实:`PlayerController.Move` 里 `Suppress` **零命中**(实测 ⑤),即

```csharp
float vTarget = config.SpeedWalk * moveInput.magnitude;   // 无 K_suppressed
```

⇒ 即使本卡把租约置位了,**玩家仍然照走** —— `AC-10-13` 的字面(「被置位且清除」)能满足,
但**规则九的目的从句「动作期间玩家不动」完全不成立**。这与 story-010 查出的
「`CameraRig.Tick` 零调用 ⇒ 请求发了相机不动」是**同一个形态**。

| 路 | 做法 | 代价 / 判据 |
|---|---|---|
| **甲** ✅ **2026-11-02 已裁(用户「甲,开工」)** | `PlayerController.Move` 开头加一行 `if (_motorLease.IsSuppressed) moveInput = Vector3.zero;`(或等价地在 `vTarget` 上乘 `K_suppressed = 0`)。**规则九从此可失败** | 3 行 + 1 条真机夹具;⚠️ 属**跨域改动**(1 的文件),须按「本卡顺带接」先例如实登记,不得静默 |
| **乙** | 本卡只接 10 的写面,`AC-10-13` 只判**租约位**;1 的读面另立 `player-controller` epic 的 story,并把 `AC-10-13` 的**目的从句**标 `BLOCKED-BY` | 诚实;但玩家在 P0 就是个「压不住」的状态,而所有判据都是绿的 —— 又一个「绿着的空功能」 |
| **丙** | 只写不读且**不登记** | **否决**:等于把「假绿」写进卡面 |

> **本裁定由立件轮就地拟定(同 story-010 的 R-10.5 / ADR-034 口径:未经用户逐条确认,实现轮前可改)。**
> ⚠️ **若裁乙**,`AC-10-13` 必须**拆两半**记分(照 `AC-10-11` 的先例):
> 「10 置位/清位」可判可绿;「玩家不动」永久 `NOT-RUN`,**不得**记 ✅。

**R-11.5 计数/返回值载体退役(照 `R-10.5` 先例)。**
`modal_phase_test.cs` 的 3 条 `test_ac1013_*` **删除**(它们断的是 `ModalPhaseResult.MotorSuppressed`);
`ModalPhaseResult` 的 `MotorSuppressed` / `MotorSuppressedSet` 两个字段是否一并删除,由实现轮定 ——
**若保留**,必须加注「本字段不构成 `AC-10-13` 的证据」;若删除,须先确认无其它读者
(立件时实测:生产读者 **0**,仅 `modal_phase_test` 读)。
**共同底线与 story-010 相同:返回值不得再充当 AC 载体。**

---

## In Scope

- `PlayerController`:新增 `_motorLease` + 只读 `MotorLease` 属性(1 侧真源,**唯一实例**)
- `EmergencyChannelDriver`:新增 `BindMotorLease` / `HasMotorLease`;四出口成对 Acquire/Release
- `BootRoot`:玩家建立后后置绑定 `BindMotorLease(_player.MotorLease)`
- `modal_phase_test.cs`:删 3 条 `test_ac1013_*` 返回值断言
- 新夹具:`Tests/EditMode/EmergencyProcedures/emergency_motor_lease_wiring_test.cs`
- 新夹具(视 R-11.4 裁定):`Tests/PlayMode/EmergencyProcedures/emergency_motor_suppression_wiring_test.cs`

## Out of Scope

- **4 交互(`LeaseSource.Self`)的接线** —— `ModalGate` 实例自身的生产接线归 **4 交互轮**
  (story-009 已登记该空挂);`SetMotorSuppression` 保持现状不动
- **25 格斗(`LeaseSource.Combat`)的接线** —— 归 25 的 GDD 轮
- **29 死亡禁动** —— GDD 1 `:279` 明写「29 不占本位图」,沿用既有待细化项;**不得**借本卡塞进去
- **`LeaseSource` 枚举扩员** —— append-only,本卡**不增任何成员**
- **`LocomotionEvaluator` 的乘数链本体**(`K_terrain` / `K_context`)—— 那是 1 的移动手感轮;
  本卡只碰 `K_suppressed` 这一项(且仅在 R-11.4 裁甲时)
- **攻击不被抑制的语义** —— 已由 `AC-1-23` 负边界钉死,本卡不重定义

---

## Dependencies

- **story-010**:`Complete ✅ 2026-11-02` —— **实现依赖**:四条出口(`Arm` / `EndAction` / `AbortAction` /
  `SkipAction`)与后置绑定形态由它建立,本卡在同一处加第二个效应
- **story-006**(player-controller epic,`MotorLease` 本体 + `AC-1-23`):`Complete ✅` ——
  **形状依赖**:`MotorLease` / `LeaseSource` 已在盘,本卡不改它们
- **story-009**:`Complete ✅ 2026-10-14` —— `EmergencyChannelDriver` 的相位入口由它建立
- **ADR-020** / **ADR-013 Amendment A**:Accepted

---

## Acceptance Criteria

- [x] **AC-10-13**(GDD · `BLOCKING` · `[A]`)动作期间 1 的 `MotorSuppressed` **被置位**,结束时清除
      (含跳过 / 中止)
      ⟶ **载体 = 真 `MotorLease` 实例的状态断言**(返回值断言退役,见 R-11.5)
- [x] **S11-01**(`[A]` · BLOCKING)`Arm` 成功后 ⇒ `lease.HasLease(LeaseSource.Emergency) == true`;
      `Arm` 抛出的那一轮 ⇒ **仍为 false**(与 R-10.1 同一条「成功之后」纪律)
- [x] **S11-02**(`[A]` · BLOCKING)三条结束出口 —— **正常完成 / 零样本签约 / 跳过** 各 ⇒ 清位;
      ⚠️ 零样本例**必须**独立断言(清位不得以「有事件」为条件)
- [x] **S11-03**(`[A]` · BLOCKING)中止两路(停手 / tick 看门狗)⇒ 清位
- [x] **S11-04**(`[A]` · BLOCKING)**只碰自己那一位** —— 前置把 `Self`(4)与 `Combat`(25)的位置起来,
      10 的 Acquire/Release 全程**不得**改变这两位
- [x] **S11-05**(`[A]` · BLOCKING)**唯一实例** —— `MotorLease` 的生产构造点**恰一处**
      (`PlayerController`),`grep` 判据见 §Test Evidence;两份位图 = 两个真源
- [x] **S11-06**(`[A]` · BLOCKING)**后置绑定真的发生了** —— 走生产启动序后
      `driver.HasMotorLease == true`(删 `BootRoot` 的绑定调用 ⇒ 本条红)
- [x] **S11-07**(`[A]` · BLOCKING · **依 R-11.4 甲案**)真机:`Arm` 后给 `Move` 喂满幅输入 ⇒
      位移 ≈ 0;`EndAction` 后同输入 ⇒ 位移恢复。⚠️ 若裁乙,本条转 `BLOCKED-BY` 并登记
- [x] **S11-08**(`[A]` · ADVISORY)三条结束出口的清位来自**同一处**代码路径(不三写)

---

## Edge Cases

| 场景 | 处置 |
|---|---|
| **零样本签约**(`agg == null`) | **仍清位** —— 动作结束了(同 story-010 的 Explore 纪律) |
| `Arm` 时通道非 `Idle` ⇒ 抛 | 先 `_channel.Arm` 成功,再 `Acquire`;抛出那一轮**零置位** |
| Idle 态 `EndAction` / `AbortAction` | **不 Release**(本来就没持有);`Release` 幂等,但语义上不该发 |
| tick 看门狗中止 | 走 `AbortAction` ⇒ 同一条清位路径,不重复实现 |
| 10 与 4 同时压制 | 位图 OR ⇒ `IsSuppressed == true`;4 清位后 **10 的位仍在** ⇒ 仍压制(这正是位图要解决的丢失更新) |
| 玩家死亡(29) | **不碰本位图**(GDD 1 `:279`);死亡禁动走 29 自己的路径 |
| `BindMotorLease` 从未调用 | 静默不崩 —— 由 `HasMotorLease` + S11-06 守卫;**不要靠代码审查守它** |
| 同一动作内重复 `Arm` | 通道抛 ⇒ 无第二次 Acquire;`Acquire` 本身也幂等(位语义,非引用计数) |

---

## Tuning Knobs

**本 story 不引入任何新数值。** 压制是布尔位,无参数。

---

## Test Evidence

**Story Type**: Integration

**Required evidence**:
- L1 租约状态:`unity/Assets/Tests/EditMode/EmergencyProcedures/emergency_motor_lease_wiring_test.cs`
- L2 真机位移(依 R-11.4 甲案):`unity/Assets/Tests/PlayMode/EmergencyProcedures/emergency_motor_suppression_wiring_test.cs`

**S11-05 的机械判据(命令 + 期望**):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -rn "new MotorLease()" unity/Assets --include=*.cs | grep -v "/Tests/" | wc -l   # 期望 1
```

**回归基线(立件时读数,非收口读数)**:
EditMode `6492 / 6455 / 0 / 0 / 37` · PlayMode `277 / 273 / 0 / 2 / 2`
`@ 2026-11-02 01:0x CST` · `S23-7` · `6909ade4`。收口须重跑,等式 `新 total − 本读数 = 本批新增数 − 删除数`。

---

## Implementation Notes

1. **改动面预估(3 生产文件 + 1 改夹具 + 2 新夹具)**:

| 文件 | 变更 |
|---|---|
| `Gameplay.Presentation/Player/PlayerController.cs` | `_motorLease` 字段 + 只读属性;(R-11.4 甲案) `Move` 首行读 `IsSuppressed` |
| `Gameplay.Boot/EmergencyChannelDriver.cs` | `BindMotorLease` / `HasMotorLease`;四出口置位/清位 |
| `Gameplay.Boot/BootRoot.cs` | 玩家建立后 `BindMotorLease(_player.MotorLease)` |
| `Tests/EditMode/EmergencyProcedures/modal_phase_test.cs` | 删 3 条返回值断言 |
| `Tests/EditMode/EmergencyProcedures/emergency_motor_lease_wiring_test.cs` | **新**(S11-01…06、08) |
| `Tests/PlayMode/EmergencyProcedures/emergency_motor_suppression_wiring_test.cs` | **新**(S11-07,视裁定) |

2. **顺序**:`Acquire` 必须在 `_channel.Arm(...)` **返回后**(同档位意图),否则抛出的那一轮会留下
   一个没有动作的压制位 —— 玩家被锁住而没有任何东西会去清它。**这是本卡最危险的一处**:
   档位意图漏了只是相机不对,压制位漏了是**玩家永远动不了**。
   ⇒ S11-01 的负例(Arm 抛出 ⇒ 仍 false)是**必测项**,不是可选项。

3. **清位必须在 `wasArmed` 守卫内**,且与 story-010 的 `Explore` **共用同一次** `wasArmed` 读取 ——
   两处各读一次 `_channel.State` 会在两次读取之间出现状态交错(虽然今日不会,但那是靠时序而不是靠结构)。

4. **R-11.4 甲案 · `Move` 的插入点**(🔁 2026-11-02 实现轮订正 —— 立件稿在此自相矛盾,见下):
   `ValidateMoveInput` **之后**、`vTarget` 计算**之前**。
   ⚠️ **立件稿原文写「之前」,但同一段给的理由是「它校验的是**外部输入**的合法性,不该因为是压制路径
   就跳过校验」—— 理由要求校验**原输入**,所以插入点必须是**之后**。原文自相矛盾,已就地订正。**
   若真按「之前」写:`moveInput` 先被清零 ⇒ `ValidateMoveInput` 校验的是零向量
   ⇒ `AC-1-09` 的上界/NaN 硬断言在压制期**恒真通过**,等于被压制路径静默关掉。
   ⇒ 形态:`if (_motorLease.IsSuppressed) moveInput = Vector3.zero;` 然后照常走完
   (不 early-return —— 否则 `AC-1-01②` 的「`CharacterController.Move` 是唯一位移写入点」
   在压制期会变成「不写入」,落地判定与重力会停,玩家会悬在空中)。

5. **证伪轮(必跑,一属性一条)**:

   | 变异 | 打什么 | 期望红 |
   |---|---|---|
   | M1 | 删 `EndAction` 的 `Release` | S11-02 |
   | M2 | 删 `AbortAction` 的 `Release` | S11-03 |
   | M3 | 删 `SkipAction` 的 `Release` | S11-02 的跳过例 |
   | M4 | `Acquire` 提到 `_channel.Arm` **之前** | S11-01 负例 |
   | M5 | `Release` 改成 `lease.Release(LeaseSource.Self)`(打错位) | S11-04 |
   | M6 | 删 `BootRoot` 的 `BindMotorLease` | S11-06(⚠️ 先核实是否**只有它**红 —— story-010 的 M5 就栽在
   「只有它」这个预期上) |

   还原纪律:`cp` 回备份 + **SHA-256 逐字节**;禁 `git checkout` / `stash`。

6. **登记给后续轮**:
   - **4 的 `LeaseSource.Self` 接线** —— `ModalGate` 实例零生产接线(story-009 已登记),归 4 交互轮
   - **25 的 `LeaseSource.Combat`** —— 归 25 的 GDD 轮
   - **`LocomotionEvaluator` 的乘数链**(`K_terrain` / `K_context`)**至今只是注释** ——
     `PlayerController.Move` 直接算 `SpeedWalk × |input|`,没有走求值器。本卡只碰 `K_suppressed`;
     其余两项归 1 的移动手感轮。⚠️ **这条须在卡面登记,不要因为本卡动了同一行就以为乘数链接上了**

---

## Engine Notes

**Engine**: Unity 6.3 LTS(6000.3.24f1) | **Risk**: LOW
- 零 post-cutoff API。`MotorLease` / `LeaseSource` / `PlayerController` 全为项目自建件。
- S11-07(若裁甲)是 PlayMode:需真 `BootRoot` 启动序 + `Time.captureDeltaTime` 固定帧节奏,
  `TearDown` 必须 `Addressables.UnloadSceneAsync` 卸载加性世界场景(承 story-010 的同款纪律)。

---

## Control Manifest Rules(本卡逐条对照)

- **只碰自己那一位** ⇒ R-11.3 + S11-04 + 变异 M5
- **禁 `SetSuppressed(bool)`** ⇒ 本卡零新增写 API;`modal_gate_test` 的反射负断言继续有效
- **10 不扩语义到攻击** ⇒ 本卡只碰水平位移路径,零攻击面
- **`LeaseSource` append-only** ⇒ 本卡不增成员

---

## 用户批准记录

**待批(2026-11-02 立件)** —— 由用户指令「立 MotorLease 的 story」建立。
以下两项由立件轮**就地拟定**,未经用户逐条确认(同 story-010 / ADR-034 / ADR-036 口径):

| # | 事项 | 状态 |
|---|---|---|
| 1 | **绑直引用 `BindMotorLease(MotorLease)`** 而非 `Func<>`(R-11.2) | ⏳ 立件轮拟定,实现轮**已采用**;未经用户逐条确认 |
| 2 | ⚠️ **R-11.4:1 的读面是否一并接** | ✅ **已批 2026-11-02(用户「甲,开工」)**:取甲案 —— `Move` 读 `IsSuppressed`,`AC-10-13` 无需拆半 |

> **甲案已落(2026-11-02)**:`PlayerController.Move` 在 `ValidateMoveInput` **之后**、
> `vTarget` 计算**之前**读 `IsSuppressed` 并清零 `moveInput`(**不 early-return** —— 见 §Implementation Notes 第 4 项)。
> ⚠️ 这使 `AC-10-13` 的两半(「位被置/清」与「玩家不动」)都**可失败**,
> 因此本卡**不**采用 `AC-10-11` 那种拆半记分 —— 拆半只是因为那一半结构性不可失败(R-10.7)。
> 1 的 epic(`player-controller`)侧已在 `EPIC.md` 登记「`Move` 自此才真正读压制位」。

---

## Completion Notes

**Completed**: 2026-11-02
**Verdict**: **COMPLETE WITH NOTES** —— 9 条 AC **全部已证**;但 §七 列的三项「证明不了的事」仍开着(4 / 25 侧零接线、乘数链仍只是注释、真机观感未走查)
**Deviations**: 见 §三;零阻塞项
**Test Evidence**: L1 `Tests/EditMode/EmergencyProcedures/emergency_motor_lease_wiring_test.cs`(12 测)· L2 `Tests/PlayMode/EmergencyProcedures/emergency_motor_suppression_wiring_test.cs`(2 测)
**Code Review**: 未跑(lean 模式;证伪轮 7 条覆盖)

### 一、逐条 AC 记分

全部 ✅,判据见证据件 §二。**AC-10-13 不拆半** —— 甲案使它的两半(「位被置/清」与「玩家不动」)都**可失败**,
所以不需要照 `AC-10-11` 那样拆(那次拆半是因为跳过路径**结构性不可能失败**,R-10.7)。

### 二、证伪轮(7 条 · 全数各自转红 · SHA-256 逐字节还原)

M1 **4 红** · M2 **4 红** · M3 **2 红** · M4 **1 红(exact)** · M5 **7 红** · M6 **2 红** · M7 **1 红(exact)**。

⚠️ **M1/M2/M3/M5/M6 是超集,不是 exact**。两处耦合已查清并登记:
- `S11-08` 断「三出口调用数 ≥ 3」⇒ 任何删一条出口调用都会带上它;
- `S11-01` 的**前置**依赖 `EndAction` 清位 ⇒ M1 顺带打红它。
它们仍有判别力,但**不得**记成「单属性判别」。

### 三、回归(三元组)

`EditMode 6500 / 6463 / 0 / 0 / 37` · `PlayMode 279 / 275 / 0 / 2 / 2`
`@ 2026-11-02 03:1x–03:4x CST` · commit `11e29dbe` + 工作树。
等式:`6500 − 6492 = 8 = 12 新 − 4 删`;`279 − 277 = 2`。

### 四、如实登记的四件事

1. **`AC-1-23` 的机械门把我判红了,而它是对的。** 初版把 `MotorLease.Acquire/Release` 写在
   `Gameplay.Boot`(装配层),门的白名单不含该程序集。**处置 = 把写面下放到 `Gameplay.Presentation`**
   (`EmergencyMotorSuppression.cs`),**不是**把 `Gameplay.Boot` 加进白名单 —— 装配层什么都装得下,
   开这个口子等于废掉「只有 4 / 10 / 25 能压」那句判据。门红给出了正确的架构落位。
2. **立件稿在 R-11.4 的插入点上自相矛盾** —— 结论写「之前」,理由却要求校验**原输入**(⇒ 必须是「之后」)。
   实现按理由走,卡面已就地订正,原文保留。
3. **删了 4 条既有测试**(`modal_phase_test` 的 3 条 `ac1013` + 1 条 `ac1018`),并把
   `ModalPhaseResult` 的两个压制布尔字段一并退役。回归等式里的 `− 4` 是明写的。
   ⚠️ 这不是「测试不够」,是那四条断的是**求值器自己写的字面量**(F1)。
4. ~~**本卡未排 sprint**~~ ✅ **2026-11-02 收口后即结**:用户指令「排进 sprint-23」⇒
   登记为 **`S23-8`**(与 `S23-7` 同为 sprint-23 的**收口后追加件**,不进 §七 判据面;
   `sprint-23.md` §八已有专属读数段)。原文保留,以本注为准。
