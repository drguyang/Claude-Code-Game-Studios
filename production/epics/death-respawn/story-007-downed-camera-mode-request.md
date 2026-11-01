# Story 007: 29 的档位请求生产载体 —— `Downed` / `Explore`(requesterId 29)与 `TR-camera-007` 补登记

> **Epic**: 死亡与复活
> **Status**: **Ready ⬜**(2026-11-02 立件 —— 规范已写定,**实现未开工**;§Acceptance Criteria 全部未勾,禁借绿)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 0.5–1 人日(约 4–8h)
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-02

> **本 story 的由来(一句话)**:`DeathTransitionPresenter` → `BootRoot` 的**倒相机闭包**
> 于 `a5f766f6`(2026-11-01)写进了 `BootRoot.cs:556-575`,而 **`death-respawn` 内没有任何 story
> 提到它** ⇒ 改这条接线时**没有一条 AC 会红**,只有 `CameraSetModeRequesterGate` 会。
> 这是 2026-11-02 §14 按 epic 扫描查出的**缺口 A**。
>
> 立件时顺带查出**第二件**:`camera-and-viewpoint.md` 的 `R-2-5` 档位表已于 2026-11-01 补入
> **29 的两行**,但 `tr-registry.yaml` 里 **没有任何 TR 覆盖它**(`TR-camera-001…006` 全部是别的)
> ⇒ **GDD 要求存在、ID 不存在**。本卡一并补登 `TR-camera-007`。

---

## Context

**GDD / spec**:
- `design/ux/death-transition-29.md` §9「相机(Φ1→Φ2)」(`:188`)
  「**两段式**:先失去支撑(下沉 + 微倾)→ 再躺定(**禁甩镜**)」
- 同件 `:127`「Φ1 倒下·失支撑 | `PlayerDied` 落流 | 水墨自四边渐入;**相机开始两段式第一段**」
- 同件 `:129`「Φ3 醒来 | 复活命令落地 | **相机第二段**」
- 同件 `:133`「**VR 变体**(P1a)| **全禁镜头效果**;平面两段式相机运动**不得平移到 VR**」
- `design/gdd/camera-and-viewpoint.md` `R-2-5` 档位表(2026-11-01 补入 29 两行:29 → `Downed` / 29 → `Explore`)
- **`AC-2-18①`**:档位表的 4×4 穷举 + 同请求方双向判据
- **`AC-20-15`**(ADR-020 Amendment C 新增):`Downed` 档的**四条语义**

**Requirement**: **`TR-camera-007`(本卡新登,当前尚不存在)**
> 「29 是 `Downed` 档的请求方:倒下进 `Downed`、醒来 / 退出回 `Explore`;`requesterId` 恒 = 29;
> `FirstPerson`(VR)在位时不产生该意图」

⚠️ **登记的现状**:`tr-registry.yaml` 现有 `TR-camera-001…006` **无一覆盖本条**;
`traceability-index.md` 同理。⇒ 本卡须**同时**完成「要求已存在但 ID 不存在」的补登(In Scope 第 1 项)。

**ADR Governing Implementation**:
- **ADR-020 §五**(主):相机**只读不持状态** —— 29 只能发**意图**,不得读回 `Mode` 做判定
- **ADR-020 Amendment C ①②**(主):`Downed` 为第五档,优先级 `Casebook(3) > Downed(2) = Treatment(2) > Explore(1)`;
  `RequesterId` **参与裁决**(甲案「同一请求方可降档」);生产调用方**必须**双参且传真实系统号
- **ADR-013 §9 C3**(次):呈现层只渲染
- **ADR-001 第二 QoS**(次):`IsDowned` 远端同步(本卡不碰)

**ADR Decision Summary**:档位是**呈现参数**不是游戏事实;29 是**请求方**不是控制方。
`SetMode` 只入队,由 `CameraModeMachine.Settle` 每帧结算。
**⚠️ 2026-11-02 之前该结算从不发生**(`CameraRig.Tick` 零生产调用)—— 已由 `emergency-procedures/story-010`
接上,本卡的 L2 判据因此**才有意义**(此前即使接线也对相机毫无效果)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 回调与接口调用,零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- **Required**: 生产调用方**必须**用双参 `SetMode(mode, requesterId)` 且传真实系统号(29);
  意图只需**边沿**(状态变化时登记),不得每帧入队
- **Forbidden**: 29 读回 `rig.Mode` 做判定(除了 `FirstPerson` 的**早退** —— 那是「不得产生意图」,
  不是「按状态决策」);转发 `rig.Tick` / `Settle`(结算权归 2);把相机意图写进任何流
- **Guardrail**: 呈现层零游戏状态(ADR-013 §9 C3);VR 全禁(spec §9)

---

## 现状实测(2026-11-02 立件前勘察)

| # | 事实 | 命令 / 依据 |
|---|---|---|
| ① | 相机闭包**内联在 `BootRoot`** 的 `deathPresenter.Bind(...)` 里,是 `Action<bool>` lambda | `BootRoot.cs:556-575` |
| ② | `death-respawn` 的 6 份 story **零处**提 `SetMode` / `Downed` 档 | `grep -rn "SetMode\|Downed" production/epics/death-respawn/*.md`(除 EPIC 的 2026-11-02 登记节) |
| ③ | 该闭包的**唯一**覆盖是 PlayMode 走查,且断的是**位姿**(下沉 / 微倾),不是 `SetMode` 调用本身 | `Tests/PlayMode/camera_downed_visual_walkthrough.cs` |
| ④ | `CameraModeMachine` 侧的 `Downed` 语义有 EditMode 夹具(并非零覆盖) | `Tests/EditMode/Camera/camera_downed_phases_test.cs` · `CameraViewpoint/camera_mode_machine_test.cs` |
| ⑤ | **边沿触发**在呈现器内(`_downedRequested`),**零直接夹具** | `DeathTransitionPresenter.cs:95-103` |
| ⑥ | `tr-registry.yaml` 无覆盖本条 → 见 §Context | `grep -n "id: TR-camera-" docs/architecture/tr-registry.yaml` ⇒ 001…006 |

⇒ **缺的不是功能,是「载体」**:接线在盘、行为大致正确,**但没有任何断言指向它**。
这正是本项目的「引用却无登记」失效模式 —— 只是这次被引用的是一条**闭包**,比一个类更难被发现。

---

## Detailed Rules(实现口径)

**R-7.1 把闭包从 `BootRoot` 提取为具名生产类型(不改行为)。**

```csharp
// Gameplay.Presentation/DeathTransition/DownedCameraRequester.cs
public sealed class DownedCameraRequester
{
    public const int RequesterId = 29;                       // systems-index.md 编号 29
    private readonly Func<ICameraRig> _rigOf;
    public DownedCameraRequester(Func<ICameraRig> rigOf) { ... }

    /// <param name="downed">true = 进入倒下档;false = 退出回探索档。</param>
    public void Request(bool downed)
    {
        var rig = _rigOf?.Invoke();
        if (rig == null) return;                              // 拆卸段 / 未建
        if (rig.Mode == CameraMode.FirstPerson) return;       // VR 全禁(spec §9)
        rig.SetMode(downed ? CameraMode.Downed : CameraMode.Explore, RequesterId);
    }
}
```

`BootRoot` 只保留 `new DownedCameraRequester(() => _cameraRig).Request` 的绑定。

⚠️ **为什么住 `Gameplay.Presentation`**:它与 `DeathTransitionPresenter` 同属**呈现层**,
且 `AC-1-23` 那类「按程序集判」的机械门在本仓已有先例(`story-011` 被 `Gameplay.Boot` 判红过一次)——
**不要**把新的相机关联逻辑再塞回装配层。
⚠️ `Func<ICameraRig>` 的理由与 `story-010` 的 `BindCameraRig` **完全一致**:机位会被重建 / 拆卸置 null,
必须懒求值 + 判空。

**R-7.2 行为必须与提取前**逐条等价**(这是「提取」不是「顺手改」)。**

| 条件 | 提取前 | 提取后 |
|---|---|---|
| `_cameraRig == null` | return,零请求 | 同 |
| `rig.Mode == FirstPerson` | return,零请求 | 同 |
| `downed == true` | `SetMode(Downed, 29)` | 同 |
| `downed == false` | `SetMode(Explore, 29)` | 同 |

⇒ **本卡的证伪轮必须证明「改坏任一条都会红」**(见 §Implementation Notes 的 M1–M5)。

**R-7.3 边沿由呈现器负责,本卡不搬。**
`DeathTransitionPresenter.Update` 已用 `_downedRequested` 做边沿(避免每帧入队撑爆 `_pending`)。
本卡**只补夹具**,不改逻辑。⚠️ 若实现轮发现边沿逻辑有缺陷,**另立卡**,不在本卡顺手改
(理由:它是呈现器的既有行为,改它 = 改 29 的相位语义)。

**R-7.4 补登 `TR-camera-007`(登记面,不是代码)。**
- `docs/architecture/tr-registry.yaml` 追加一条(`status: covered` 只在本卡实现并验证后才是真的,
  立件时先写 `gap`,收口时改 `covered`);
- `docs/architecture/traceability-index.md` 增对应行;
- 两份文件都**只追加**,不改既有条目(ADR-024 的 append-only 精神)。

---

## In Scope

- **新** `Gameplay.Presentation/DeathTransition/DownedCameraRequester.cs`(R-7.1)
- `Gameplay.Boot/BootRoot.cs`:闭包改为构造该类型 + 绑定 `Request`(行为等价)
- **新** `Tests/EditMode/DeathRespawn/downed_camera_requester_test.cs`(S29-01…05)
- **新** `Tests/PlayMode/DeathRespawn/downed_camera_wiring_test.cs`(S29-06 / S29-07)
- `tr-registry.yaml` + `traceability-index.md` 补登 `TR-camera-007`(R-7.4)
- `Tests/PlayMode/camera_downed_visual_walkthrough.cs`:若提取导致其**构造方式**变化,同步(只改构造,不改断言)

## Out of Scope

- **`DeathTransitionPresenter` 的相位机与边沿逻辑** —— 既有行为,本卡只补夹具(R-7.3)
- **`CameraRig` / `CameraModeMachine` 的任何改动** —— 归 2 相机视角
- **`CameraRig.Tick` 的接线** —— 已由 `emergency-procedures/story-010` 完成(2026-11-02);
  ⚠️ **不要在本卡再动它**,否则两次改动会互相掩盖
- **Φ3 床格视角(第二段)** —— spec §9 的「醒来」行;归**机位轮**(`camera_downed_visual_walkthrough`
  的头注已登记该缺口)
- **VR 两段式的实际表现** —— P1a;本卡只保证「VR 在位时 29 不产生意图」(意图层)
- **远端玩家的 `Downed` 同步** —— ADR-001 第二 QoS,归 45
- **死亡线三半边落点的重新裁决** —— `TR-death-007` 已定,本卡不动

---

## Dependencies

- **`emergency-procedures/story-010`**:`Complete ✅ 2026-11-02` —— **形状依赖 + 时序前提**:
  它接上了 `CameraRig.Tick`,使**本卡的 L2 判据有意义**(此前档位机从不结算,请求发了也没效果)。
  ⚠️ 这不是「依赖它的实现才能编译」,而是「依赖它的接线才能**观察到**结果」——
  实现轮**须先确认** `BootRoot.Update` 里那行还在,否则 L2 会红成一个假失败。
- **camera-viewpoint story-005 / 006**:`Complete ✅` —— `ICameraRig` / `CameraModeMachine` 形状
- **ADR-020 Amendment C**:`Accepted 2026-11-01`

---

## Acceptance Criteria

- [ ] **S29-01**(`[A]` · BLOCKING)`Request(downed: true)` ⇒ 相机**恰收到一条** `(Downed, requesterId: 29)`
- [ ] **S29-02**(`[A]` · BLOCKING)`Request(downed: false)` ⇒ **恰一条** `(Explore, 29)`
- [ ] **S29-03**(`[A]` · BLOCKING)**VR 全禁** —— `rig.Mode == FirstPerson` 时两种入参**各 ⇒ 零请求**
      (spec §9「平面两段式不得平移到 VR」)
- [ ] **S29-04**(`[A]` · BLOCKING)**机位缺席** —— 访问器返回 null ⇒ 零请求、不抛
      (拆卸段 / 启动序早期)
- [ ] **S29-05**(`[A]` · BLOCKING)**系统号单一出处** —— `DownedCameraRequester.RequesterId == 29` 且
      `CameraSetModeRequesterGate` 全树**零违例**(新调用点接收者不是豁免名 `_modeMachine`)
- [ ] **S29-06**(`[A]` · BLOCKING)**生产启动序真的接上了** —— 走真 `BootRoot` 后,
      该请求器**存在且已绑定机位访问器**(可观测);删 `BootRoot` 的绑定 ⇒ 本条红
- [ ] **S29-07**(`[A]` · BLOCKING)**边沿不重复** —— 连续多帧 `downed == true` ⇒ 只登记**一条**请求
      (呈现器的 `_downedRequested`;夹具走真启动序 + 真流,不重实现边沿)
- [ ] **S29-08**(`[I]` · BLOCKING)**登记面已补** —— `tr-registry.yaml` 有 `TR-camera-007` 且
      `traceability-index.md` 有对应行(逐字判据:`grep -c "TR-camera-007"` 在两文件中各 ≥ 1)
- [ ] **S29-09**(`[L]` · ADVISORY)真机走查:倒下时相机的**两段式节奏**与 `AC-29-11` 合判 ——
      `-batchmode -nographics` 结构性不可判 ⇒ **`NOT-RUN`**,不得借绿

---

## Edge Cases

| 场景 | 处置 |
|---|---|
| 拆卸段:`_cameraRig` 已置 null | 零请求、不抛(R-7.2 逐条等价) |
| 启动序早期(机位未建) | 同上 |
| `FirstPerson`(VR)在位 | 零请求;**不是**「改成别的档」 |
| 同一帧内先倒下又醒来 | 呈现器按相位出边沿;两条请求都可能登记,由 `CameraModeMachine` 裁决(不属本卡) |
| `Casebook` 模态在位时倒下 | 29 **仍发** `Downed`(意图必发);是否回退由甲案裁决,`Downed(2) < Casebook(3)` ⇒ 相机留在脉案是**合法**行为 |
| `PlayerDied` 之后玩家被 25 再次击倒 | 呈现器相位机不重入(既有);本卡不新增行为 |

---

## Tuning Knobs

**本 story 不引入任何新数值。** 两段式的时长 / 幅度在 `CameraDownedPhases`(已由用户数值轮裁定:
`SagSeconds 1.2` / `SagDepth 0.45` / `SagTiltDegrees 10`)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- L1 EditMode:`unity/Assets/Tests/EditMode/DeathRespawn/downed_camera_requester_test.cs`(S29-01…05、08)
- L2 PlayMode:`unity/Assets/Tests/PlayMode/DeathRespawn/downed_camera_wiring_test.cs`(S29-06、07)

**回归基线(立件时读数,非收口读数)**:
EditMode `6500 / 6463 / 0 / 0 / 37` · PlayMode `279 / 275 / 0 / 2 / 2`
`@ 2026-11-02 03:1x CST` · `S23-8` · `5ed75004`。收口须重跑。

---

## Implementation Notes

1. **改动面预估(2 生产文件 + 1 新 + 2 夹具 + 2 登记文件)**。

2. **`Request(bool)` 而不是 `OnDowned()` / `OnRevived()` 两个方法** —— 参数名就是自检测:
   调用方必须显式说明「现在是倒下还是不是」,与呈现器的 `Action<bool>` 缝一一对应,零适配层。

3. **不要把它做成静态类** —— 它持一个 `Func<ICameraRig>`,`BootRoot` 每实例一份;
   静态类会让两个 `BootRoot` 实例(测试间残留)共享同一个访问器,**上一次测试的机位会被下一次用到**。

4. **证伪轮(必跑,一属性一条)**:

   | 变异 | 打什么 | 期望红 |
   |---|---|---|
   | M1 | `downed ? Downed : Explore` 的两个档对调 | S29-01 + S29-02 |
   | M2 | 删 `FirstPerson` 早退 | S29-03 |
   | M3 | 删 null 守卫(直接 `_rigOf().SetMode(...)`) | S29-04(改为抛,判据断言「不抛」) |
   | M4 | `RequesterId` 常量改 `-1` | S29-01/02 + S29-05(门) |
   | M5 | 删 `BootRoot` 的绑定 | S29-06(+ S29-07) |
   | M6 | 呈现器改为每帧请求(删 `_downedRequested` 判据)—— ⚠️ **这是改既有行为**,仅用于证明 S29-07 有判别力,**跑完必须还原** | S29-07 |

   还原纪律:`cp` 回备份 + **SHA-256 逐字节**;禁 `git checkout` / `stash`。

5. **登记给后续轮**:
   - Φ3 床格视角(醒来第二段)仍空 —— 机位轮
   - VR 两段式的表现 —— P1a
   - 「该发没发」的覆盖矩阵门 —— 今日仍无(**`CameraSetModeRequesterGate` 只判 arity / 字面 `-1`**)

---

## Engine Notes

**Engine**: Unity 6.3 LTS(6000.3.24f1) | **Risk**: LOW
- 零 post-cutoff API。
- L2 若走真启动序,`TearDown` 必须 `Addressables.UnloadSceneAsync` 卸载加性世界场景
  (承 `story-010` / `story-011` 的同款纪律),否则跨测试累积。

---

## Control Manifest Rules(本卡逐条对照)

- **双参 + 真系统号** ⇒ R-7.1 的 `RequesterId` 常量 + S29-05
- **29 不读回 `Mode` 做判定** ⇒ 唯一例外是 `FirstPerson` 早退(「不得产生意图」,不是「按状态决策」),
  已在 R-7.1 注里写明边界
- **呈现层零游戏状态** ⇒ 本卡零 `IEventSink` 写入,零流事件
- **边沿入队** ⇒ R-7.3(既有行为,本卡只补夹具)

---

## 用户批准记录

**立件(2026-11-02)** —— 由用户指令「立 29 的档位请求 story」建立。
本卡**无待决分叉**:R-7.1 的提取是**等价重构**(R-7.2 逐条对照),R-7.4 的登记是补缺。
若实现轮发现提取会**改变行为**,须停下另立卡,不得在本卡内改 29 的相位语义。
