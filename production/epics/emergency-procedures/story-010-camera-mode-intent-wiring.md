# Story 010: 档位意图生产接线 —— `Treatment` / `Explore` 必发(含中止),并退役 `ExploreCount` 假绿载体

> **Epic**: 10 急救动作
> **Status**: **Ready ⬜**(2026-11-02 立件 —— 规范已写定,**实现未开工**;§Acceptance Criteria 全部未勾,禁借绿)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 1.5–2.5 人日(约 12–20h)
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-02

> ✅ **2026-11-02 R-10.7 已裁定(用户「甲+乙,按建议走」)** ⇒ spec 定稿,**无待决分叉**。
> 第 1–3 项(见 §用户批准记录)仍属立件轮就地拟定,实现轮可提替代裁定。

> **本 story 的由来(一句话)**:规则八「动作开始发 `Treatment`、结束(**含跳过路径**)必发 `Explore`」
> 被 `AC-10-11` 判 **BLOCKING**,而**生产码里 `Treatment` 的调用次数是 0** —— 计数器
> `ModalPhaseResult.ExploreCount` 顶替了「真的发了」这句话。这是 **假绿形态 F1**
> (自造断言顶替 AC 语义层),也是 2026-11-02 双参 `SetMode` 规则按 epic 扫描时查出的
> **两处既有缺口中的 B**(证据:`production/qa/evidence/s24-camera-downed-two-phase-2026-11-01.md` §14)。

> ⚠️ **立件时又查出第二层,比第一层更重(见 R-10.7)**:`AC-10-11` 的**跳过半根本不在相机链上** ——
> 跳过入口直连 `EmergencyManualDriver.TryManual(method: 1)`,不经 `EmergencyChannelDriver`,
> 且候选列表**只在 `Idle` 可见** ⇒ 生产可达的跳过永远发生在「相机本来就在 `Explore`」的时刻。
> **这不是「验证得不够」,是「这个 AC 从来没有可失败的机会」。**
> ✅ **2026-11-02 用户裁定:甲 + 乙** —— 跳过经 `EmergencyChannelDriver.SkipAction` 拉回相机链,
> **同时**把不可达性如实登记(该 `Explore` 是防御性幂等请求),`AC-10-11` 拆两半分开记分。

> ⚠️ **Requirements 引用勘误(本 story 立件时实测)**:`story-005` 卡面把本条要求记作
> `TR-emergency-016(档位意图必发)`,而注册表里 **`TR-emergency-016` = 「`ResultMul[Missed]` = 0 还是非 0」**
> (数值未裁项)。本条要求的正确 ID 是 **`TR-emergency-011`**(`tr-registry.yaml:3195-3202`)。
> 复算:`grep -A4 "id: TR-emergency-011$" docs/architecture/tr-registry.yaml`。
> **本 story 一律引用 011**;story-005 的错引**不在本 story 内改**(改已 Complete 的历史卡 = 改历史),
> 已在 §Out of Scope 登记为外抛项。

## Context

**GDD**: `design/gdd/emergency-procedures.md`
- **规则八**(`:352-360`)档位意图表:开始 → `Treatment`;结束(**含跳过路径**结束)→ `Explore`
- **规则六之甲**(`:724`)中止行:清 `MotorSuppressed`、**发 `Explore`**
- `:725` 跳过行:**必发 `Explore`**
- `:762` 接口表第 2 行:「**档位意图** `Treatment` / `Explore`(**含跳过路径**)—— 结清 **`O-12` 的 10 侧**」
- **`AC-10-11`**(`:1044`)`BLOCKING` · `[I]` ·「`GIVEN` **跳过路径执行完毕**,`WHEN` 集成走查,
  `THEN` **必发 `Explore`** —— 相机不卡在近景(规则八 · `O-12`)」

**Requirement**: **`TR-emergency-011`**(`status: covered` —— ⚠️ 该状态**与本卡实测不符**,见下)
> 「动作开始发 Treatment、结束(含跳过路径)必发 Explore 档位意图 —— 否则相机卡在近景」
> *(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**:
- **ADR-020 §五**(主):相机只读不持状态 —— 10 只能发**意图**,不得读回 `Mode` 做判定
- **ADR-020 Amendment C ②**(主):`SetMode(CameraMode, int requesterId)` + `RequesterId` **参与裁决**
  (甲案「同一请求方可降档」);生产调用方**必须**双参且传真实系统号(`10`)
- **ADR-013 §9 C3**(次):42 只渲染;10 不持有 UI 元素
- **ADR-011 §二**(次):显式相位,禁 `Script Execution Order`

**ADR Decision Summary**:档位是**呈现参数**不是游戏事实;10 是**请求方**不是控制方 ——
`SetMode` 只入队,由 `CameraModeMachine.Settle` 每帧结算一次(EC-2-9 四步)。
优先级 `Casebook(3) > Downed(2) = Treatment(2) > Explore(1)`;`Treatment` 与 `Downed` 同格的理由
(玩家 `PlayerDied` 落流后 1 侧已抑制交互 ⇒ 10 的请求那一刻不再产生)已写在 `CameraModePriority` 的注释里。
**10 的降档必须由 10 自己发** —— 甲案下只有「开档的请求方」能把档降回去;不传 `requesterId: 10`
⇒ 退出语义**静默失效**(不崩、不报错,相机卡在近景)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 相位回调与接口调用,零 post-cutoff API。真相机断言走既有 `CameraRig`
(已由 camera-viewpoint story-005 的 `camera_mode_machine_test` 与 story-006 的
`camera_obligations_reconciliation_test` 覆盖),
本 story 不新增引擎面义务。

**Control Manifest Rules (this layer)**:
- **Required**: 生产调用方**必须**用双参 `rig.SetMode(mode, requesterId: 10)`;意图事件经 `IEventSink.Append`(主机唯一);
  相位变化只在既有三入口(`Arm` / `EndAction` / `AbortAction`)上发,不新增计时器
- **Forbidden**: `rig.SetMode(mode)`(单参)/ `rig.SetMode(mode, -1)` —— 机械门 `CameraSetModeRequesterGate` 判红;
  10 读回 `rig.Mode` 做判定(ADR-020 §五);转发 `rig.Tick` / `Settle`(结算权归 2);
  用**计数器**顶替「真的发了」这个断言(F1)
- **Guardrail**: `Gameplay.Boot` 引用集不变(已含 `Gameplay.Presentation`,承 ADR-025 §①);
  不新增 `IEventSink` 写入者;不改 `Sim` / `Sim.Contracts` 的引用集

---

## 现状实测(2026-11-02 立件前勘察 · 四条命令逐条可跑)

| # | 事实 | 命令 | 实测输出 |
|---|---|---|---|
| ① | 生产侧 `.SetMode(` 共 **5 行**,其中**唯一真调用**是 29 的 `Downed`/`Explore`,其余 3 行是 `CameraRig` 内部转 `_modeMachine`,1 行是注释 | `grep -rn "\.SetMode(" unity/Assets --include=*.cs \| grep -v "/Tests/" \| grep -v "Editor.Tools.Gates/"` | `BootRoot.cs:564` · `CameraRig.cs:146/149/220` · `CameraModeMachine.cs:85`(注释) |
| ② | 上述 5 行里 **`CameraMode.Treatment` 出现 0 次** | 同上 + `\| grep -c "CameraMode.Treatment"` | **`0`** |
| ③ | 上述 5 行里 `CameraMode.Explore` 只出现 **1 次**(29 的降档分支) | 同上 + `\| grep -c "CameraMode.Explore"` | **`1`** |
| ④ | `ExploreCount` 的**生产读者 = 0**(2 处命中全是定义 / 构造赋值;其余全在 `Tests/`) | `grep -rn "ExploreCount" unity/Assets --include=*.cs \| grep -v "/Tests/" \| grep -vc "public readonly int ExploreCount\|ExploreCount = exploreCount"` | **`0`** |

⇒ **急救规则八的运行期可达性 = 0%**:`Treatment` 一次都没被请求过,`Explore` 的
`AC-10-11` 由 `modal_phase_test.cs:118/140` 的两条 `Assert.AreEqual(1, …ExploreCount)`
**顶替**。这两条断言断的是**求值器自己返回的整数**,不是相机真的收到了请求 ——
`ModalPhaseEvaluator.Skip()` 里写死 `exploreCount: 1`,断言必然为 1。**改坏生产行为它也不会红。**

> **⚠️ 为什么今天没有可见症状**:`Treatment` 本身也没接 ⇒ 相机从未进过近景,也就无从「卡在近景」。
> 但只要有人把 `Treatment` 接上而不接 `Explore`(最典型的漏法:只接 `Arm`,忘了
> `EndAction` / `AbortAction` 两条出口),症状**立刻成立**,而**新门抓不到** ——
> `CameraSetModeRequesterGate` 只判 arity 与字面 `-1`,**不判「该发没发」**。

---

## Detailed Rules(实现口径)

**R-10.1 档位意图挂在既有三入口上,不新增机制。**
`EmergencyChannelDriver` 的相位入口只有三个(`Arm` / `EndAction` / `AbortAction`,
外加 `OnTickEdge` → 转调 `AbortAction`)。档位意图**只能**挂在这三处:

| 入口 | 发什么 | 依据 |
|---|---|---|
| `Arm(actionOrdinal, target)` **成功之后** | `Treatment` | 规则八「动作开始」 |
| `EndAction(actorId)` —— **只要原本在 Armed** | `Explore` | 规则八「动作结束」 |
| `AbortAction()` —— **只要原本在 Armed** | `Explore` | 规则六之甲 `:724` |
| `SkipAction(target, actorId)`(**甲案** · 跳过入口) | `Explore` | 规则八「**含跳过路径**」· `:725` |

> ⚠️ 第四行是**甲案**(2026-11-02 裁定)新增的入口:跳过原本直连 `TryManual`、**不在本链上**,
> 其 `Explore` 在今日 P0 可达性下是**防御性幂等请求**(见 R-10.7 的乙案登记)。

⚠️ **「成功之后」不是措辞讲究**:`_channel.Arm()` 在非 `Idle` 态**抛** `InvalidOperationException`
(story-007 既有行为)⇒ 必须在 `_channel.Arm(...)` **返回后**再发 `Treatment`,否则抛出的那一轮
会留下一个没有动作的 `Treatment` 请求(相机进近景而动作不存在)。

⚠️ **`EndAction` 的 `Explore` 不得以 `agg != null` 为条件。** 零样本签约(`agg == null` ⇒ 零事件,
F-10.5)是**合法**的一次动作起止 —— 玩家按住又松开但没产生幅度沿。**动作结束了,相机就必须退出近景。**
这是本 story 最容易写错的一处(见 §Edge Cases 第 1 行)。

**R-10.2 驱动经「后置绑定」持 `Func<ICameraRig>`(懒求值 + 判空),不持实例、不进 `Assemble` 签名。**
理由与 `BootRoot` 给 `DeathTransitionPresenter` 的 `downed` 闭包**同构**:
① 相机机位在启动序第 5 步才建,而驱动在 `CompositionRoot.Assemble` 内**(更早)**构造 ⇒
装配期拿不到;② 拆卸段会把字段置 null,闭包可能在下一帧才求值 ⇒ 直取会 NRE
(2026-11-01 已有 94 条 PlayMode 红的先例)。

⇒ 形态:`EmergencyChannelDriver.BindCameraRig(Func<ICameraRig> accessor)`,**后置绑定**,
与既有 `TeleportSink.BindPlayer` / `CasebookCommandRouter.SetAuthor` / `BuildController.BindOwner`
**同一约定**(装配期拿不到的东西一律后置绑定,不塞 `Assemble` 参数)。
`accessor` 为 null 或返回 null ⇒ **不发请求**(逻辑层 / EditMode / 拆卸后),不抛。

⚠️ **「没绑」是一条静默失败通道** —— 没人调 `BindCameraRig` 就永远不发 `Treatment`,
且不崩不报错,**正是本 story 要消灭的那种失败**。⇒ 驱动须暴露可观测的
`HasCameraRig`(或 `IsCameraRigBound`),并由 **L2 真机夹具**断言生产启动序跑完后它为 `true`。
这条断言的价值在于:它把「接线被谁悄悄删掉」变成一条**会红的测试**。

**R-10.3 `requesterId` 恒为 `10`,字面写死,不经参数。**
不是「配置项」,是**身份**。写成可配参数就等于给「传错号」开了口子,而
「同一请求方可降档」判据对错号是**静默失效**(不崩、不报错)。
⇒ 三处调用点一律 `rig.SetMode(<mode>, requesterId: 10)`;`10` 以 `const int RequesterId = 10;`
落在 `EmergencyChannelDriver` 内,带注释指明系统号出处(`systems-index.md` 编号 10)。

**R-10.4 幂等与重复请求。**
`AbortAction` 在 Idle 态是幂等 no-op(返回 false)⇒ **不发** `Explore`。
`OnTickEdge` 的看门狗中止走 `AbortAction` ⇒ 自动获得同一条判据,**不重复实现**。

**R-10.5 `ExploreCount` 退役。**
`ModalPhaseResult.ExploreCount` 与 4 处构造参数一并删除;`ModalPhaseEvaluator`
的 4 个工厂(`Skip` / `Complete` / `Missed` / `Abort`)同步收缩;
`modal_phase_test.cs` 的 4 条 `ExploreCount` 断言**删除**(`AC-10-11` 的载体改指本 story 的相机断言)。
⚠️ **这是本 story 唯一触及 `Sim` 程序集的改动,且是「删字段」不是「改语义」** ——
`Sim` 与 `Sim.Contracts` 的引用集一字不改,门 A / b2 / ADR-025 §① 全部继续成立。
**理由**:该字段没有任何生产读者(实测 ④),它唯一的功能就是让「必发 Explore」这句话
**看起来被验证了**。留着它,下一次有人接 10 的时候会**再次**拿它当证据 ——
这正是 F1 的可复发形态,不是一次性误用。

> **本裁定由立件轮就地拟定(同 ADR-034 / ADR-036 口径:未经用户逐条确认,实现轮前可改)。**
> 若实现轮判定保留字段更合适,替代裁定 = **字段留下但必须标注「本字段不构成 `AC-10-11` 的证据」**,
> 并同时删掉那 4 条断言 —— **两种裁定的共同底线是:计数器不得再充当 AC 载体。**

**R-10.6 `AC-10-11` 的新载体(两层,缺一不算)。**

| 层 | 载体 | 判据 |
|---|---|---|
| **L1 意图层** | `EmergencyChannelDriver` + 间谍 `ICameraRig`(只记录 `(mode, requesterId)` 序列,**不重实现**任何相机行为) | 一个完整动作周期:请求序列**恰** `[Treatment, Explore]`,两个 `requesterId` **都 = 10**(跳过路径见 R-10.7 / S10-10 —— 它**不走**这条链) |
| **L2 真机层** | PlayMode + 真 `CameraRig` | `Arm` 后 `rig.Tick(…)` 充分转场 ⇒ `rig.Mode == Treatment`;`EndAction` 后充分 `Tick` ⇒ `rig.Mode == Explore` |

L2 是 `AC-10-11` 目的从句「**相机不卡在近景**」的**唯一**可判形式 —— L1 只证「请求发了」,
**不证「相机认了」**(优先级可能被 `Casebook` 压掉,那属规则七合法行为,须在 L2 里显式避开:
测试期间不请求 `Casebook`)。

**R-10.7 ⚠️ 跳过路径根本不在相机链上 —— 立件轮查出的第二处结构性事实。**

> **✅ 已裁定(2026-11-02 · 用户「甲+乙,按建议走」)** —— 甲:跳过拉回相机链;
> 乙:不可达性如实登记 + `AC-10-11` 拆两半分开记分。丙否决。
> 本节以下为立件时的勘察事实与三路分析,**原文保留**;采纳项以本裁定为准。

实测(`CasebookCommandRouter.cs:211-223`):

```
EmergencyCandidateList 跳过入口 Submit
  → CasebookCommandRouter.SubmitEmergencySkip()          // :211
  → EmergencyManualDriver.TryManual(target, method: 1)   // :221  ← 直连
```

**不经 `EmergencyChannelDriver`**:不 `Arm`(所以**没有** `Treatment`),不 `EndAction` / `AbortAction`
(所以**没有** `Explore`)。而 `R-9.3`(story-009 用户裁定)让候选列表**仅在 `Idle` 态可见**
⇒ 生产可达的跳过**永远发生在 `Idle`**,那一刻相机**本来就在 `Explore`**。

⇒ **规则八「跳过路径结束必发 `Explore`」的防护目标(相机不卡近景)在现行设计下不可达。**
也就是说 `AC-10-11` 不是「被计数器假装验证了」这么简单,它还**压根没有可失败的机会**:
即使按 R-10.5 删掉计数器、再在跳过路径上补一条 `Explore` 请求,
那条请求也只是「在 `Explore` 上再请求一次 `Explore`」—— **换了个形态的假绿**。

**三条路(✅ 2026-11-02 用户裁定:取甲 + 乙;丙否决):**

| 路 | 做法 | 代价 / 判据 | 裁定 |
|---|---|---|---|
| **甲** | 跳过入口改经 `EmergencyChannelDriver.SkipAction(target)`:`SkipAction` 先发 `Explore`,再委派 `TryManual(method: 1)`。**把跳过拉回相机链** | 改动小(改 `CasebookCommandRouter.SubmitEmergencySkip` 的接收者);⚠️ 必须**同时**做乙的登记,否则又成假绿 | **✅ 采纳** |
| **乙** | **如实登记**「现行 P0 跳过为 `Idle`-only ⇒ 该 `Explore` 是**防御性幂等请求**,不是 `AC-10-11` 的可失败判据」,并把 `AC-10-11` 拆两半:「动作结束必发 `Explore`」(真,S10-02/03 判)+「跳过路径必发 `Explore`」(标 `BLOCKED-BY 设计裁定` / `NOT-RUN`,**禁借绿**) | 诚实;代价 = AC 长期开着一半 | **✅ 采纳(与甲并行)** |
| **丙** | 让跳过入口在 `Armed` 期也可达(改 `R-9.3` 的 Idle 门) | 违 story-009 的用户裁定;改交互语义,不属本卡面 | **❌ 否决** |

> ⚠️ **乙不是甲的替代品,是甲的必要配套。** 只做甲 = 补一条跑不到的请求然后宣称 AC 绿了 ——
> 这是本项目抓过的**第六个**假绿形态的同构变体(把「不可达」写成「已覆盖」)。
> **只做乙不做甲** = AC 诚实地开着,可接受。**甲+乙** = 请求在位 + 不可达性透明登记,推荐。

> **✅ 裁定已落(2026-11-02)**:甲 + 乙。乙-only 的备选分支**不再适用** ——
> `In Scope` 的跳过项**保留**,S10-10 **是 BLOCKING 真判据**(不是登记项)。
> 落点见 §Implementation Notes 第 7 项与 §用户批准记录 第 4 项。

---

## In Scope

- `EmergencyChannelDriver`:三入口挂档位意图(`Treatment` / `Explore`,`requesterId: 10`)
- `EmergencyChannelDriver`:增 `BindCameraRig(Func<ICameraRig>)` 后置绑定 + `HasCameraRig` 观测面(R-10.2)
- `BootRoot`:`_cameraRig` 建立后调 `BindCameraRig(() => _cameraRig)`
- **`CasebookCommandRouter.SubmitEmergencySkip`**:改经 `EmergencyChannelDriver.SkipAction`(R-10.7 甲案)
- `ModalPhaseEvaluator`:删 `ExploreCount` 字段与 4 处构造实参
- `Tests/EditMode/EmergencyProcedures/modal_phase_test.cs`:删 4 条 `ExploreCount` 断言
- 新夹具:`Tests/EditMode/EmergencyProcedures/emergency_camera_mode_intent_test.cs`(L1)
- 新夹具:`Tests/PlayMode/EmergencyProcedures/emergency_camera_mode_intent_wiring_test.cs`(L2)
- **`design/gdd/emergency-procedures.md` 回写**(乙案载体):规则八 `:352-360` 与 `AC-10-11` `:1044`
  各加一条带日期的不可达性注(逐字文本见 §Implementation Notes 第 7 项)

## Out of Scope

*Handled by neighbouring stories / 其他 epic —— 不要在本 story 实现:*

- **`TR-emergency-012`(规则九 `MotorSuppressed`)的接线** —— **本 story 立件时顺带查出同型缺口**:
  `MotorLease` 全仓**零生产实例化**(`new MotorLease()` 仅出现在 `Tests/`),
  `PlayerController` 不消费它 ⇒ `AC-10-13`(BLOCKING `[A]`)今日同样只有
  `ModalPhaseResult.MotorSuppressed` 的值断言当载体,**F1 同型**。
  **本 story 不修**(用户立件口径 = 档位意图);须**另立 story**(建议 `story-011`),
  且它可复用本 story 在 `AbortAction` / `EndAction` 上建立的同一条出口钩子。
- **规则十 `HoldMode` 的玩家入口** —— 归 49(P2),`OQ-10-11` 已裁
- **`CameraRig` / `CameraModeMachine` 的任何改动** —— 属 2 相机视角;本 story 只发请求
- **改 `R-9.3` 的 Idle 门让跳过在 `Armed` 期可达**(R-10.7 丙案)**已否决** —— 违 story-009 的用户裁定
- **`ExploreCount` 之外 `ModalPhaseResult` 的其它字段** —— 不动
- **「该发没发」的覆盖矩阵门** —— `CameraSetModeRequesterGate` 只判 arity / 字面 `-1`;
  建一条「谁该发什么」的矩阵门是**另一个门的工作**,已登记为外抛项(§Implementation Notes 末)
- **story-005 卡面上 `TR-emergency-016` 的错引订正** —— 改已 Complete 的历史卡 = 改历史;
  按 `coding-standards.md`「原文不删」纪律,登记不改
- **VR(`FirstPerson`)** —— P1a;本 story 不新增 VR 分支(档位请求被 `CameraModeMachine`
  的 VR 早退路径自行丢弃,不需要 10 额外判断)

---

## Dependencies

- **story-005**(模态期求值器):**Complete ✅ 2026-10-02** —— 本 story 删它的 `ExploreCount` 字段
- **story-009**(直读通道运行期装配):**Complete ✅ 2026-10-14** —— 本 story 改的 `EmergencyChannelDriver`
  是它交付的;三入口(`Arm` / `EndAction` / `AbortAction`)全部由它建立
- **camera-viewpoint story-005**(档位状态机 · `gear-state-machine-perf`):**Complete ✅ 2026-10-03** ——
  `ICameraRig` / `CameraMode` / `CameraModeMachine` 的既有形状出自它(**不是** story-002 ——
  story-002 是 `yaw-basis-hard-delivery`,零 `CameraModeMachine` 命中)
- **ADR-020 Amendment C**:`Accepted 2026-11-01` —— 双参 `SetMode` 与 `RequesterId` 裁决语义

**形状依赖与实现依赖的区分**(承 `coding-standards.md` §依赖写法):
- 依赖 camera-viewpoint **story-005** 的 **`ICameraRig` / `CameraMode` / `CameraModeMachine` 形状**(已在盘 ⇒ 形状依赖,不要求其 story 重开)
- 依赖 story-009 的 **`EmergencyChannelDriver` 实现**(真运行期行为 ⇒ **实现依赖**,已 Complete)

---

## Acceptance Criteria

- [ ] **AC-10-11**(GDD · `BLOCKING` · `[I]`)`GIVEN` **跳过路径执行完毕**,`WHEN` 集成走查,
      `THEN` **必发 `Explore`** —— 相机不卡在近景
      ⟶ **按 R-10.7 拆两半,两半分开记分,禁合并借绿**:

      | 半 | 判据 | 本卡状态 |
      |---|---|---|
      | **「动作结束必发 `Explore`」** | S10-02(S10-03 为中止支) | **可判** —— 这是 AC 真正有内容的那一半 |
      | **「跳过路径必发 `Explore`」** | S10-10 + S10-11 | ⚠️ **今日不可失败**(R-10.7:跳过为 `Idle`-only)⇒ 甲+乙落地后为「请求在位(甲)+ 不可达性已登记(乙)」,**仍不得记作「已通过」** |
      ⟶ 计数器断言退役见 R-10.5;原「一条 AC 一个载体」的写法**就此作废**(它正是假绿的载体形式)
- [ ] **S10-01**(`[A]` · BLOCKING)动作开始 ⇒ 相机**恰收到一条** `(Treatment, requesterId: 10)`;
      `Arm` 因非 `Idle` 抛出的那一轮 ⇒ **零**档位请求(R-10.1 的「成功之后」)
- [ ] **S10-02**(`[A]` · BLOCKING)动作结束两出口 —— **正常完成** / **零样本签约(`agg == null`)**
      各 ⇒ **恰一条** `(Explore, 10)`;两条出口**分别独立**断言(缺一条即漏一种最典型的漏写法)。
      ⚠️ **跳过**不在本条 —— 它不走通道(R-10.7),另由 S10-10 判
- [ ] **S10-03**(`[A]` · BLOCKING)中止两路 —— **玩家停手(`AbortAction`)** 与 **tick 看门狗
      (`OnTickEdge` → `AbortAction`)** 各 ⇒ **恰一条** `(Explore, 10)`,且**零处置事件**
      (与 `AC-9-09` 不相冲:后者判事件,本条判相机)
- [ ] **S10-04**(`[A]` · BLOCKING)Idle 态误触 —— `EndAction` / `AbortAction` 在**未 `Arm`** 时
      ⇒ **零**档位请求(不得凭空发 `Explore`;这是 `EndAction` / `AbortAction` 现有
      `_onIdleGate?.Invoke(true)` 无条件调用所隐含的同一类问题)
- [ ] **S10-05**(`[A]` · BLOCKING)`ExploreCount` **已退役** —— `ModalPhaseResult` 无该字段;
      `grep -rn "ExploreCount" unity/Assets --include=*.cs` **零命中**
- [ ] **S10-06**(`[A]` · BLOCKING)真机 `CameraRig` —— `Arm` + 充分 `Tick` ⇒ `rig.Mode == Treatment`;
      `EndAction` + 充分 `Tick` ⇒ `rig.Mode == Explore`(L2;测试期间不请求 `Casebook`;
      ⚠️ **不走跳过路径** —— 跳过不经通道,R-10.7)
- [ ] **S10-06b**(`[A]` · BLOCKING)**后置绑定真的发生了** —— 走**生产启动序**后
      `EmergencyChannelDriver.HasCameraRig == true`。
      ⚠️ 这条是 R-10.2 的守卫:**「没绑」不崩不报错**,只有这条断言会把它变成红的
- [ ] **S10-07**(`[A]` · BLOCKING)机械门 —— `CameraSetModeRequesterGate` 全树 **零违例**,
      且新调用点落在 `Gameplay.Boot/EmergencyChannelDriver.cs`(接收者**不是**豁免名 `_modeMachine`)
- [ ] **S10-08**(`[A]` · ADVISORY)三处调用点的 `10` 来自**单一常量**(`EmergencyChannelDriver.RequesterId`),
      三处**不各写一个字面 `10`**(防「改一处漏两处」)
- [ ] **S10-09**(`[L]` · ADVISORY)真机走查 —— 长按 F 进近景、松开回探索视角,**无「卡在近景」**
      (需图形界面;`-batchmode -nographics` 结构性不可判 ⇒ 天然 `NOT-RUN`,**不得借绿**)
- [ ] **S10-10**(`[A]` · BLOCKING · 甲案 · **已裁定**)跳过入口 ⇒ 相机**恰收到一条**
      `(Explore, 10)`(甲案:经 `EmergencyChannelDriver.SkipAction`),且 `TryManual(method: 1)` 仍被调用
      **恰一次**(与 story-009 的 AC-9-05 不重复判结算,只判「没把跳过改坏」)
- [ ] **S10-11**(`[I]` · BLOCKING · 乙案 · **已裁定**)不可达性**已如实登记** ——
      story 内与 GDD 回写处均写明「现行 P0 跳过为 `Idle`-only ⇒ 该 `Explore` 是防御性幂等请求,
      不构成 `AC-10-11` 的可失败判据」。判据 = 该句在两个文件里**逐字存在**(`grep -F` 命中),
      ⚠️ 本条**不是**形式主义:没有这句,本卡就是把一个计数器换成了一个 no-op

---

## Edge Cases

| 场景 | 处置 |
|---|---|
| **按住又松开但零幅度沿**(`agg == null`,零事件) | **仍发 `Explore`** —— 动作结束了,相机必须退出近景(R-10.1 加粗项)。⚠️ 最容易漏的一条 |
| `Arm` 时通道非 `Idle`(重复预约)⇒ 通道抛 | **先**调 `_channel.Arm`,**成功返回后**才发 `Treatment`;抛出那一轮零请求 |
| Idle 态调 `EndAction` | 通道返回 null 且本就未 Armed ⇒ **零请求**(S10-04) |
| `AbortAction` 在 Idle 态(幂等 no-op,返回 false) | **零请求** |
| tick 看门狗中止(`OnTickEdge`) | 转调 `AbortAction` ⇒ 走同一条 `Explore` 出口,**不重复实现** |
| 相机机位尚未建 / 拆卸段已置 null | `Func<ICameraRig>` 返回 null ⇒ **不发**,不抛(R-10.2) |
| **`BindCameraRig` 从未被调用**(接线被删 / 新增装配路径漏绑) | 静默不崩 —— **这正是本卡要消灭的失败形态**。守卫 = `HasCameraRig` + **S10-06b** 的真机断言。⚠️ 不要靠代码审查守它 |
| `Casebook` 模态在位时动作结束 | 10 仍发 `Explore`(意图必发);档位是否回退由 `CameraModeMachine` 的甲案裁决 —— **10 不读回 `Mode` 做判断**(ADR-020 §五)。此时 `rig.Mode` 仍是 `Casebook` 是**合法**行为,不是 bug |
| 玩家 `PlayerDied` 落流后同一帧仍有残留 `Treatment` 请求 | `Downed` 与 `Treatment` 同优先级(2),按 EC-2-9「同优先级取最后到达者」给唯一解;不属本卡面 |

---

## Tuning Knobs

| 旋钮 | 值 | 归属 |
|---|---|---|
| 档位转场时长(`transitionDuration`) | 沿用 2 的既有值 | 2 相机视角 / 数值轮 |
| 请求方系统号 | **`10`,写死,非旋钮** | 本卡(R-10.3) |

**本 story 不引入任何新数值。**

---

## Test Evidence

**Story Type**: Integration

**Required evidence(两条路径,缺一即 BLOCKING)**:
- L1 意图层:`unity/Assets/Tests/EditMode/EmergencyProcedures/emergency_camera_mode_intent_test.cs`
- L2 真机层:`unity/Assets/Tests/PlayMode/EmergencyProcedures/emergency_camera_mode_intent_wiring_test.cs`

**回归基线(收口时须带三元组:截至时刻 · 批次 · commit)**:
EditMode `6481 / 6444 / 0 / 0 / 37` · PlayMode `275 / 271 / 0 / 2 / 2`(`@ 2026-11-02 00:12 CST` ·
批次 = ADR-020 Amendment C 收口轮 · `ea7ac81e` + 未提交)。

复算命令(不照抄本行):

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ["unity/Logs/em-cam.xml","unity/Logs/pm-cam.xml"]:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ("total","passed","failed","skipped","inconclusive")})
PY
# 期望: em-cam.xml total=6481 passed=6444 failed=0 skipped=37 inconclusive=0
#       pm-cam.xml total=275   passed=271 failed=0 skipped=2  inconclusive=2
```

⚠️ **这是立件时的读数,不是收口读数**;收口必须重跑并以新数为准,
等式判据 = `新 total − 本读数 = 本批新增测试数`(承 F7「借绿」形态)。

---

## QA Test Cases

*Written at story creation(lean mode —— QL-STORY-READY skipped;specs self-authored from AC text).*

- **S10-01 开始发 Treatment**
  - Given:间谍 `ICameraRig`(零行为,只记录 `(mode, requesterId)`);`EmergencyChannelDriver` 在 Idle。
  - When:调 `Arm(ordinal: 0, target: P)`。
  - Then:记录序列 = `[(Treatment, 10)]`。负例:`Arm` 在已 Armed 上再调 ⇒ 抛,记录序列**不变**。
- **S10-02 三条结束出口**
  - Given:三个独立夹具实例(正常完成 / 零样本 / 跳过入口)。
  - When:各走一条出口。
  - Then:每条序列 = `[(Treatment,10), (Explore,10)]`;零样本例**额外**断言 `EndAction` 返回 false 且流上零事件(证「零事件 ≠ 零请求」)。
- **S10-03 两条中止出口**
  - Given:两个实例(停手 / 看门狗)。
  - When:停手例调 `AbortAction`;看门狗例调 `OnTickEdge(tick + AbortIdleTicks)`。
  - Then:两条序列均以 `(Explore,10)` 收尾;**且**流上零 `EmergencyTreatmentApplied`(与 AC-9-09 同判但不重复断言其判据)。
- **S10-04 Idle 误触**
  - Given:从未 `Arm` 的驱动。
  - When:调 `EndAction` / `AbortAction`。
  - Then:记录序列 = `[]`(空)。
- **S10-05 字段退役**
  - Given:源码树。
  - When:`grep -rn "ExploreCount" unity/Assets --include=*.cs`。
  - Then:零命中。
- **S10-06 真机档位**(PlayMode)
  - Given:真 `CameraRig` + 真组合根装配。
  - When:`Arm` → `rig.Tick(dt)` 若干帧至 `TransitionT == 1`;`EndAction` → 同样 Tick。
  - Then:`Mode` 先 `Treatment` 后 `Explore`。
  - Edge:期间**不请求** `Casebook`(否则优先级 3 压过 2,属规则七合法行为,会使判据失效)。
- **S10-06b 后置绑定到位**(PlayMode,与 S10-06 同夹具)
  - Given:走**生产启动序**(BootRoot 全链,不手工拼装)。
  - When:读 `_services.EmergencyChannelDriver.HasCameraRig`。
  - Then:`true`。反证 M5(删 `BindCameraRig` 调用)⇒ 本断言红,其它断言**不变**。

---

## Implementation Notes

1. **改动面预估(3 生产文件 + 2 新夹具 + 2 既有夹具)**:

| 文件 | 变更 |
|---|---|
| `Gameplay.Boot/EmergencyChannelDriver.cs` | 增 `const int RequesterId = 10` + `BindCameraRig` / `HasCameraRig`;`Arm` / `EndAction` / `AbortAction` 各加一次请求(带「原本是否 Armed」守卫) |
| `Gameplay.Boot/BootRoot.cs` | `_cameraRig` 建立后 `BindCameraRig(() => _cameraRig)`(后置绑定;`Assemble` 签名**不改**) |
| `Sim/EmergencyProcedures/ModalPhaseEvaluator.cs` | 删 `ExploreCount` 字段 + 4 处构造实参 |
| `Tests/EditMode/EmergencyProcedures/modal_phase_test.cs` | 删 4 条 `ExploreCount` 断言 |
| `Tests/EditMode/EmergencyProcedures/emergency_camera_mode_intent_test.cs` | **新**(S10-01…S10-05 + S10-07) |
| `Tests/PlayMode/EmergencyProcedures/emergency_camera_mode_intent_wiring_test.cs` | **新**(S10-06) |

2. **`BindCameraRig(Func<ICameraRig>)` 后置绑定,而非 `Assemble` 增参** —— 见 R-10.2。
   `CompositionRoot.cs:1353` 的构造点**一字不改**(既有 `emergency_channel_wiring_test` 的
   两处构造点 `:223` / `:246` 同样零改动);新能力全部挂在后置绑定上。
   ⚠️ **不要**为了「装配期就给定」去改 `Assemble` 签名 —— 那会把相机拖进组合根的参数表,
   而相机在 `Assemble` 之后才被建(启动序第 5 步)。后置绑定是本仓**既有**约定,不是新机制。

3. **「原本是否 Armed」的取值时机**:
   - `EndAction`:现有实现先 `_channel.EndAction()` 再 `_onIdleGate(true)`。
     须在 **`_channel.EndAction()` 之前**读一次 `bool wasArmed = _channel.State == Armed`
     (或等价地复用 `State` 而不是事后推断)—— 因为 `_channel.EndAction()` 会把态置回 Idle。
   - `SkipAction`:本条**不判** `wasArmed` —— 跳过入口只在 `Idle` 可达(R-9.3),`Explore` 无条件发(幂等)。
   - `AbortAction`:现有实现已有 `bool wasArmed = _channel.State != Idle` ⇒ **直接复用**,
     但注意它当前**未用于门控** `_onIdleGate`,本卡顺带把同一守卫补上(见 S10-04)。

4. **门的方向**:`CameraSetModeRequesterGate` 扫 `Assets` 全树、豁免接收者名 `_modeMachine`。
   本卡三处调用接收者是 `rig`(或局部变量名),**不在豁免名单** ⇒ 会真正被门检查。
   写完后先跑 `Tests/EditMode/CameraViewpoint/camera_setmode_requester_gate_test.cs` 确认零违例。

5. **证伪轮要求(必跑,一属性一条)**:六条变异须各**恰**转红(超集须如实登记,不得写成「恰如预期」)——
   | 变异 | 打什么 | 期望红 |
   |---|---|---|
   | M1 | `EndAction` 的 `Explore` 请求删掉 | S10-02 + AC-10-11(L1/L2) |
   | M2 | `AbortAction` 的 `Explore` 请求删掉 | S10-03 |
   | M3 | 三处 `requesterId: 10` → `-1` | S10-01/02/03(L1 的 requesterId 断言)+ `CameraSetModeRequesterGate` |
   | M4 | 把 `Explore` 改成以 `agg != null` 为条件 | S10-02 的零样本例 |
   | M5 | 删掉 `BootRoot` 里的 `BindCameraRig(...)` 调用 | S10-06b(且**只有它** —— 这正是「静默失败」的判别力证明) |
   | M6 | `SkipAction` 里删掉 `Explore` 请求(甲案) | S10-10 |

   ⚠️ **M4 是本卡的核心判别力测试** —— 若它不红,说明夹具没有覆盖「零样本也必须回探索视角」,
   而这一条恰恰是最容易被实现成「有事件才回档」的地方。
   还原纪律:`cp` 回备份 + **SHA-256 逐字节**比对;禁 `git checkout` / `stash`。

6. **甲案落地形状(✅ 2026-11-02 已裁)** —— 三处**必须同批**,少一处即不成立:

   | # | 落点 | 内容 |
   |---|---|---|
   | 6a | `EmergencyChannelDriver.SkipAction(PatientId target, int actorId)` | **新**方法:先 `RequestCameraMode(CameraMode.Explore)`,再委派 `_manual.TryManual(target, method: 1, actorId, _currentTick())`,返回其 `Applied`。⚠️ **`Explore` 先于 `TryManual`** —— 反过来的话 `TryManual` 若抛/短路,相机就留在近景 |
   | 6b | `CasebookCommandRouter.SubmitEmergencySkip` | 接收者从 `EmergencyManualDriver` 改为 `EmergencyChannelDriver`;既有两条守卫**原样保留**(`_emergencySkip == null` ⇒ fail-loud;`_boundPatient < 0` ⇒ 静默零事件零提示) |
   | 6c | `BootRoot` 的 `BindEmergencySkip` 调用点 | 传入的宿主同步换成 driver(`BootRoot.cs:836` 的 `SetSkipCallback` 链**不动**) |

   ⚠️ **`SkipAction` 里的 `Explore` 请求就是 S10-10 的判据对象**;
   而它**不改变** `AC-10-11` 跳过半的记分状态(永远 `NOT-RUN`,见 §用户批准记录)。

7. **乙案落地形状(✅ 2026-11-02 已裁)** —— 两处**逐字同款**的注,缺一处即 S10-11 不通过:
   - 本 story 的 R-10.7 块内(已有,实现轮**不要删**)
   - `design/gdd/emergency-procedures.md` 的**规则八**(`:352-360`)与 **`AC-10-11`**(`:1044`)各加一条
     带日期的注:「**现行 P0 跳过为 `Idle`-only(`R-9.3` 的 Idle 门)⇒ 该路径上的 `Explore`
     是防御性幂等请求,不构成本 AC 的可失败判据;AC 的真实判别力在『动作结束必发 `Explore`』一半。**」

8. **登记给后续轮**:
   - **`MotorLease` 零生产接线**(`AC-10-13`)—— 本卡 Out of Scope 已登记,须另立 story;
     本卡在 `AbortAction` / `EndAction` 上建立的出口钩子可直接复用。
   - **「该发没发」覆盖矩阵门** —— 今日无门能判「某系统**该**发某档却没发」;
     `CameraSetModeRequesterGate` 只判 arity。建矩阵门须先有一张「谁在什么时刻发什么档」的**真源表**
     (候选:`camera-and-viewpoint.md` 的 `R-2-5` 档位表),归 2 相机视角的 GDD/门轮。

---

## Engine Notes

**Engine**: Unity 6.3 LTS(6000.3.24f1) | **Risk**: LOW

- 零 post-cutoff API。`ICameraRig` / `CameraMode` / `CameraModeMachine` / `CameraRig` 全部为项目自建件。
- L2 真机断言用 `CameraRig.Tick(dt)` 显式推进(单一显式相位,承 ADR-011/AC-2-27③),**不依赖渲染帧**。
- PlayMode 真机件若加载 `World` 场景,`TearDown` 必须 `Addressables.UnloadSceneAsync` 卸载
  加性场景 —— **`Object.Destroy` 与测试框架都不会卸载它**(2026-11-01 `main_menu_wiring_test` 因此串跑转红)。

---

## Control Manifest Rules(本卡逐条对照)

见 §Context 的 `Control Manifest Rules (this layer)`。逐条落点:
- **双参 `SetMode` + 真系统号** ⇒ R-10.3 + S10-07(`CameraSetModeRequesterGate`)
- **10 不读回 `Mode`** ⇒ §Edge Cases 的 `Casebook` 行(ADR-020 §五)
- **相位只在既有三入口发** ⇒ R-10.1(不新增计时器 / 不在渲染帧里轮询)
- **不新增 `IEventSink` 写入者** ⇒ 本卡零 `Append` 调用
- **`Sim` 引用集不变** ⇒ 改动只是删一个字段,无新依赖

---

## 用户批准记录

**立件(2026-11-02)** —— 本卡由用户指令「立 emergency-procedures 的档位意图 story」建立。

**✅ 已批(2026-11-02 · 用户「甲+乙,按建议走」)** —— 第 4 项。

| # | 事项 | 状态 |
|---|---|---|
| 1 | **`ExploreCount` 退役**(R-10.5) | ⏳ 立件轮就地拟定,**未逐条确认**(同 ADR-034 / ADR-036 口径,实现轮前可改;替代裁定见 R-10.5 引用块) |
| 2 | **`BindCameraRig(Func<ICameraRig>)` 后置绑定**而非 `Assemble` 增参(R-10.2) | ⏳ 同上 |
| 3 | **`MotorLease` / `AC-10-13` 不在本卡范围**,另立 story(§Out of Scope) | ⏳ 同上 |
| 4 | ⚠️ **R-10.7 跳过路径的设计分叉** | **✅ 已批 2026-11-02:甲 + 乙** |

### 第 4 项的裁定内容(逐条落点)

**用户原话**:「甲+乙,按建议走」。

- **甲(采纳)** —— 跳过入口改经 `EmergencyChannelDriver.SkipAction(target)`:
  `SkipAction` 先发 `(Explore, requesterId: 10)`,再委派 `TryManual(target, method: 1)`。
  改的是 `CasebookCommandRouter.SubmitEmergencySkip` 的**接收者**,不是它的判定语义。
- **乙(采纳,与甲并行)** —— **必须**同时登记两件事,缺一则本卡仍算假绿:
  1. **story 内**如实写明:现行 P0 跳过为 `Idle`-only ⇒ 该 `Explore` 是**防御性幂等请求**,
     不构成 `AC-10-11` 的可失败判据;
  2. **GDD 回写**:`emergency-procedures.md` 规则八 与 `AC-10-11` 处加**带日期**的注,
     同款措辞。
- **丙(否决)** —— 改 `R-9.3` 让跳过在 `Armed` 期可达:违 story-009 的用户裁定,不做。
- **`AC-10-11` 拆两半分开记分**(本裁定的直接后果):
  | 半 | 判据 | 可否记「已通过」 |
  |---|---|---|
  | 动作结束必发 `Explore` | S10-02 / S10-03 | **可以**(这是 AC 真正有内容的那一半) |
  | 跳过路径必发 `Explore` | S10-10(请求在位)/ S10-11(不可达性已登记) | **不可以** —— 永远标「请求在位 + 不可达,`NOT-RUN`」 |

> ⚠️ **这一条不是形式主义**:若实现轮把「跳过路径必发 `Explore`」记成 ✅,
> 就是把一个计数器换成了一个 no-op,假绿形态不变。S10-11 的 `grep -F` 判据守的就是这个。
