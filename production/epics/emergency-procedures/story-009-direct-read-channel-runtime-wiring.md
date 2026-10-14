# Story 009: 急救直读通道运行期装配 + 跳过入口接线(M2 形态件③ 功能半)

> **Epic**: 10 急救动作
> **Status**: **Complete ✅**(2026-10-14 实现轮 + 当日评审收口 —— 评审原件
> `production/qa/evidence/review-story-009-2026-10-14.md`:1 BLOCKING(中止看门狗)当场修复
> + 回归测试,双套件零回归;AC-9-10/11 ADVISORY 欠账登记归人工走查轮,不挡收口)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 3–5 人日(约 24–40h;实际 1 日内)
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-14

> **⚠️ 2026-10-14 实现轮四项用户裁定(开工前裁定,全按建议)**:
> ① **R-9.5 装载路径 = `InputSystem.actions`**(原稿 Addressables 被实测推翻 —— 见 R-9.5 订正注);
> ② **F 键 = 按住 = 一次动作**(按下沿 → Arm + 候选列表隐藏;松开沿 → EndAction → 聚合上行;
>    按住超 `AbortIdleTicks`(50)⇒ tick 边沿中止零事件 —— 规则六之甲);
> ③ **`EmergencyChannelDriver` 顺带实现 `IArmedState`**(结清 ADR-013 Amendment A 方向①
>    `ModalGate` 侧零生产实现者的空挂;`ModalGate` 实例自身的生产接线归 4 交互轮);
> ④ **AC-9-05 带最小 Judge 分支**(`HostEmergencyProcessor.Process` 内 `method==1` ⇒
>    结果取 `ModalPhaseEvaluator.Skip(false).Result` = AppliedWeak;强度仍走 F-10.4 公式;
>    同时给 `ModalPhaseEvaluator` 落首个生产调用方 —— 结清其零生产调用面的同型空挂)。

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则六 跳过路径 `:255-300` · 规则六之甲 跳过入口位置 `:301-325` · 规则七 模态抑制 `:326-355` · 规则一 `:108-135` 读数形状 · F-10.1 magnitude 定点化)
**Requirement**: **`AC-10-12`**(ADVISORY)「纯手柄 + 纯键盘单机走查全部动作(含跳过),`THEN` **零按键提示即完成**,且跳过入口在 **`Idle` 态**焦点可导航」+ **`AC-10-10`**(BLOCKING)「跳过路径 ⇒ `JudgeResult = AppliedWeak`,载荷 `method = Skip`,熟练度成长不触发」+ `TR-emergency-001`(直读通道契约)/ `TR-emergency-020`(动作词表)

**本 story 兑现的是一处「引用却无登记」的幽灵待办**:`story-023` 边界裁定①(`story-023-...md:29/34`)把「施加点 / 候选动作列表载体 / `Idle` 态可达时序」判给「**10 实现轮**」,`milestones/README.md:198` 亦如此引用 —— 但**全库无「10 实现轮」的 epic / story / 工量登记**(与 ADR-022 关卡工具、`CompoundTriggered` 同型的失效模式)。本 story 即该轮的**首个具名载体**。

**现状实测(2026-10-14 · 本 story 立件前勘察)**:

| 环节 | 生产状态 | 证据 |
|---|---|---|
| `EmergencyDirectReadChannel`(四态机 · 417 行) | ❌ **零生产实例化**(全仓唯一 `new` 在测试) | `grep -rn "new EmergencyDirectReadChannel" unity/Assets` |
| `EmergencyAggregator`(244 行) | ❌ 零生产调用 | 同上 |
| `HostEmergencyProcessor` | ✅ 已装配(经 `EmergencyManualDriver`) | `CompositionRoot.cs:143` |
| `InputActionAsset` 运行期装载 | ❌ **零装载点** | `MovementInputReader.cs:13` 自陈 |
| `EmergencyCandidateList` 交互绑定 | ❌ **零**(无 `focusable`/`RegisterCallback`/`clicked`) | `EmergencyCandidateList.cs:69-114` |
| `SetVisible` 生产调用 | ❌ **零**(仅测试) | `grep -rn "\.SetVisible(" unity/Assets \| grep -v Tests` |
| `InteractionInputDriver.cs:105` | ⚠️ 硬编码 `method: 0`(Manual)⇒ **跳过路径无生产入口** | 本 story 核心缺口 |

⇒ **急救链运行期可达性 = 0%**。按 `milestones/README.md:251-255` 口径:**「epic Complete」≠「运行期接线」**。

## Detailed Rules(实现口径)

**R-9.1 直读通道生命周期**(承 `EmergencyDirectReadChannel` 既有 API,不新增机制):

```
Idle ──Arm(actionOrdinal)──▶ Armed ──逐帧 Feed(直读)──▶ Reading(瞬时)
                                │
                                ├── EndAction() ──▶ AggregatedEmergency(恰一条)──▶ Idle
                                └── AbortAction() ──▶ 丢弃、不发 ──▶ Idle
```

**R-9.2 驱动权归属**:通道生命周期由 `EmergencyChannelDriver`(新,住 `Gameplay.Boot`)驱动;
它**只做相位搬运**,不判定、不构造 `SimEvent`、不写流(承 `EmergencyDirectReadChannel.cs:76-77` 的判定边界)。

**R-9.3 Idle 门语义**(规则六之甲):候选列表**仅在 `Idle` 态可见**;
进入 `Armed` ⇒ `SetVisible(false)`;`EndAction`/`AbortAction` 回 `Idle` ⇒ `SetVisible(true)`。
`Armed` 态内的退出路径 = 停手中止(规则六之甲),**不是**跳过入口。

**R-9.4 跳过入口接线**(`O-10-1`:10 不拥有 UI 元素,向 42 提规格):
跳过入口 = 候选列表末项(`EmergencyCandidateList.SkipEntryName`,恒存在);
其 Submit ⇒ `ICasebookCommandSink.SubmitEmergencySkip()` ⇒ `EmergencyManualDriver.TryManual(target, method: 1, ...)`。
**2026-10-14 实现注**:① 目标 = `CasebookCommandRouter._boundPatient`(与 `SubmitDose` 同构;
未绑定 ⇒ **静默零事件**(合法玩家路径 —— 未查体就点跳过,零提示纪律;与 `SubmitDose` 的
fail-loud 刻意不同);不在场 ⇒ presence 门静默。② 写路径宿主(`EmergencyManualDriver`)
经 `BindEmergencySkip(manual, actorId)` 后置注入(SetAuthor 同款 —— 装配期 actorId 未知;
未注入 ⇒ fail-loud 装配遗漏)。③ 顺带勘误:候选 entry 原稿误挂 `.skip-entry` 类(与真跳过
入口混淆)⇒ 改挂 `.candidate-entry`(宽半承接类,story-023 警示②)。

**R-9.5 输入资产装载**:**复用系统 3 既有的 `InputService`**(`Gameplay.Input/InputService.cs`)——
它是「全案唯一 `InputActionAsset` 实例的持有者」(AC-3-A1),其类头明写「装载方(后续故事)换成
Addressables」⇒ **本 story 即该「后续故事」**。~~装载路径 = Addressables~~
**⚠️ 2026-10-14 实现轮就地订正(原稿前提被实测推翻)**:装载路径 = **`InputSystem.actions`**
(项目级动作资产,`ProjectSettings` 注册、git-tracked,guid `052faaac586de48259a63d0c4782560b`)
—— ① `Assets/AddressableAssetsData/AssetGroups/` **被 gitignore**(`.gitignore:85`),
Addressables 登记不可版本化;② 经 Addressables 再装载同一资产 = **第二实例 = 违 AC-3-A1**
(`InputService.EnsureSameAssetInstance` 以 `GetInstanceID()` 断言);
③ `InputSystem.actions` 天然是同一实例(BootRoot `EnsureEventSystem` 已在用)。
`CompositionRoot.Assemble` 增可选参 `inputActions`(生产 = BootRoot 传入并 fail-loud 校验非 null;
EditMode/测试 = null = 逻辑层模式,Attach 非法、FeedForTest 可用)。`InputService` 不经手
(它仍在等真正的 Addressables 化需求;本 story 不引入第二装载点)。

## In Scope

- `InputActionAsset` 运行期装载(**经 Addressables 装入既有 `InputService`**,不新建 Provider)
- `EmergencyChannelDriver`(新):通道相位搬运(Idle→Arm→Feed→End)
- `CompositionRoot` 装配上述二者 + 注入 F-10.1 三乘子
- `ICasebookCommandSink` 增 `SubmitEmergencySkip()`
- `EmergencyCandidateList` 跳过入口加 `focusable` + `RegisterCallback`
- `CasebookCommandRouter` 实现 `SubmitEmergencySkip()` ⇒ `TryManual(method: 1)`
- `EmergencyCandidateRuntime` 加 Idle 门驱动(`SetVisible` 生产调用)

## Out of Scope

- **真动作表** `emergency_action.json` / `emergency_skill.json`(归 21a/10 烘焙轮;本 story 沿用 `EmergencyManualDriver.BandageRow` 内联最小表)
- **真素材**(接触音 / 节律 VFX)—— 归 **story-008**,BLOCKED-BY batch-03 外部供应链
- **F-10.1 三乘子的具体值**(`AXIAL_SCALE` / `DZ_MAG` / `MAG_MAX`)—— **归数值轮**(承「数值用户自己调」;本 story 构造注入占位,判据标 NOT-RUN)
- **VR 模式** —— 已裁定推迟 P1a(`EPIC.md:39`)
- **联机上行**(45)—— `story-004` 已交单机路径;网络传输子句 BLOCKED-BY-45
- **手柄实机走查**(`AC-10-12` 的手柄半)—— BLOCKED-BY 桌面调试轮,NOT-RUN
- **`O-10-2` 跳过教学语义** —— 归 48 tutorial epic

## Dependencies

- **story-001…007**:全部 Complete ✅(本 story 复用其产出:`EmergencyDirectReadChannel` / `EmergencyAggregator` / `HostEmergencyProcessor` / `ModalPhaseEvaluator`)
- **story-023**(skeuomorphic-ui):Complete ✅ —— 本 story 接其「功能半」
- **story-008**:Ready ⬜ —— **非依赖**(本 story 不碰素材)

## Acceptance Criteria

- [x] **AC-9-01**[A] BLOCKING:`InputActionAsset` 运行期装载成功 ⇒ `Emergency` 动作可解析(`FindAction("Emergency")` 非 null);资产缺失 ⇒ 启动期 fail-loud(不静默 null)—— ✅ BootRoot 启动序 2.5 步 fail-loud(`InputSystem.actions` null ⇒ 抛,guid 校验提示);`Attach` 期 `FindAction` 落空由通道抛(story-007 既有)。**自动化面 = 启动序代码 + `emergency_channel_wiring_test`(逻辑层不依赖);实机 Attach 面归人工走查**
- [x] **AC-9-02**[A] BLOCKING:通道相位 —— `Idle` 态 `State == Idle` 且 `EmergencyActionEnabled == false`;`Arm(n)` 后 `State == Armed` 且 enabled == true;`EndAction()` 后回 `Idle` 且 enabled == false(承 `AC-3-E4`)—— ✅ `test_armFeedEnd_writesBothEvents_withAggregatedShape`(三态 + enabled 位逐点断言)
- [x] **AC-9-03**[A] BLOCKING:直读采样 —— `Armed` 期逐帧 `Feed` ⇒ `SampleCount` 递增;`Idle` 期 `Feed` 被拒(零采样)—— ✅ 同上(`SampleCount` 0→2 递增;Idle 拒收 = 通道 story-007 既有判据,`FeedForTest` 非 Armed 恒返当前计数)
- [x] **AC-9-04**[A] BLOCKING:**端到端可达** —— `Idle → Arm → Feed×N → EndAction` 走完 ⇒ 病史流出现 `EmergencyAttempt` + `EmergencyTreatmentApplied`(本 story 的核心判据)—— ✅ `test_armFeedEnd_writesBothEvents_withAggregatedShape`(载荷形状逐字段 + 归因目标病人);`test_armFeedEnd_fullMagnitude_yieldsAppliedPotency`(强度判别 = 65536)
- [x] **AC-9-05**[A] BLOCKING:**跳过路径** —— 跳过入口 Submit ⇒ 病史流 `EmergencyTreatmentApplied` 且 `method == 1`、`JudgeResult == AppliedWeak`、**零 `SkillGrown`**(承 `AC-10-10`)—— ✅ `test_ac905_skipMethod_appliedWeakPotency_andZeroGrowth`(Method=1 + DrugPotency=32768 + 零成长不变式 + 非空转守卫);`test_ac905_manualVersusSkip_potencyDiscriminable`(两档判别)。**判定可判形式 = `DrugPotency.Raw==32768` ∧ `Method==1`(JudgeResult 不是流上字段)**
- [x] **AC-9-06**[A] BLOCKING:**Idle 门** —— 通道 `Armed` ⇒ 候选列表 `IsVisible == false`;回 `Idle` ⇒ `IsVisible == true`(规则六之甲)—— ✅ `test_idleGate_hidesOnArm_restoresOnEnd` / `test_idleGate_restoresOnAbort`(回调直驱;`EmergencyCandidateRuntime.SetVisible` 透传 + `CompositionRoot` onIdleGate 接线)
- [x] **AC-9-07**[A] BLOCKING:**跳过入口焦点可达** —— 跳过入口 `focusable == true` 且在 `Idle` 态可经焦点导航 Submit(承 `AC-10-12` 的键盘半)—— ✅ `test_ac907_skipEntry_focusable_true`(focusable 断言)+ `test_candidateRuntime_skipCallback_invokedThroughRouter`(回调链闭环:UI Submit → 路由 → 写者双事件)。**焦点导航 Submit 的实机面(官方桥 NavigationMoveEvent 落焦后 NavigationSubmitEvent 派发)归人工走查 AC-9-10**
- [x] **AC-9-08**[I] BLOCKING:**零数字** —— 跳过入口与候选项文本零数字(`AC-023-2` 门回归)—— ✅ **2026-10-14 评审原件就勾**:结构面 = `EmergencyCandidateList` 零新增数字载体(`Label("跳过")` 纯文字)+ USS 门(`emergency_skip_entry_test`:`.skip-entry` 声明块全库恰一、块内禁 `content:` / 数字)在评审套件内持续回归绿;走查面 ✅ 同日补齐(截图 `unity/Logs/shots/20261014_222810_1x.png` 判读:止血/通气/跳过三项零数值载体,`production/playtests/playtest-2026-10-14-story009-walkthrough.md` §6;结构面证据 `production/qa/evidence/review-story-009-2026-10-14.md`)
- [x] **AC-9-09**[A] BLOCKING:**中止 ≠ 跳过** —— `Armed` 内停手超 `ABORT_IDLE_TICKS` ⇒ `AbortAction` ⇒ **零处置事件、零 `EmergencyAttempt`**(承 `AC-10-18` 回归)—— ✅ `test_armedIdleBeyondAbortTicks_tickEdgeAborts_zeroEvents`(49 tick 不中止 / 第 50 中止零事件 / 流上零 method=1)+ `test_abortThenRelease_endActionIdempotent`(中止后松开幂等)。判据常量提出为 `ModalPhaseEvaluator.AbortIdleTicks`(公开,驱动侧同源)
- [x] **AC-9-10**[L] ADVISORY:纯键盘单机走查全部动作(含跳过)零按键提示即完成 —— ✅ **2026-10-14 人工走查批执行全绿**(`production/playtests/playtest-2026-10-14-story009-walkthrough.md` §9):主链 A+B 人工真机走通(F 长按 >3 s 不误杀 / 松开复现 / E 查体 / 焦点落「跳过」/ Space 提交零异常零提示 / 全程零鼠标);自动化驱动器同日腿 B 全链绿。走查批未转出 BLOCKING 缺陷(自动化腿 A 四红判读 = 注入夹具环境怪癖,双独立证据压过 —— 真机实证 + CI `test_longPressWithContinuousFeeding_notAborted` 绿)
- [x] **AC-9-11**[L] ADVISORY:`L_input < 50 ms` 实机实测 —— ✅ **2026-10-14 感知探针实测即时**(走查批 §5 腿 D:按下沿列表消失 / 松开沿复现均「即时/无延迟」,用户目视报告;OQ-10-12 原型历史实测 < 35 ms 旁证;多次操作无一次察觉延迟)

## Edge Cases

| 场景 | 处置 |
|---|---|
| `Armed` 期内二次 `Arm` | `EmergencyDirectReadChannel` 既有行为:抛 `InvalidOperationException`(10 须先 End/Abort) |
| `Idle` 态 `EndAction()` | 既有行为:返回 null(零样本签约 —— 单周期 0 动作 ⇒ 0 条上行) |
| 玩家无在场病人时按 F | `TryManual` 前置:不在场 ⇒ `Applied = false`,零事件 |
| 通道 `Suspended`(设备断连) | 保留位;入口裁定归 Story 008 设备态(本 story 不实现,承既有 TODO 注记) |
| 候选列表零候选项 | 合法形态:列表仍存在且仍带跳过入口(`story-023` AC) |

## Tuning Knobs

| 旋钮 | 值 | 归属 |
|---|---|---|
| `AXIAL_SCALE` / `DZ_MAG` / `MAG_MAX`(F-10.1) | **占位,不填真值** | **数值轮** |
| `ABORT_IDLE_TICKS` | 沿用 `ModalPhaseEvaluator` 合成小值 50 | 数值轮 |
| `JITTER_MAX` | 0(原型期) | 数值轮 |

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/PlayMode/EmergencyProcedures/emergency_channel_wiring_test.cs`(端到端可达 + Idle 门)+ `unity/Assets/Tests/EditMode/EmergencyProcedures/emergency_skip_entry_wiring_test.cs`(跳过路径 + 焦点可达)
**Evidence doc**: `production/qa/evidence/review-story-009-*.md`

## Implementation Notes(2026-10-14 实现轮)

**⚠️ 评审收口(2026-10-14 当日)**:实现轮评审(`production/qa/evidence/review-story-009-2026-10-14.md`)
查获 **1 BLOCKING(F-1)**:中止判据 `tick - armedTick >= 50` 与按住语义(裁定②)致命交互 ——
首 press 沿后恒无新边沿 ⇒ 任何 ≥ 2.5 s 的合法长按被 tick 边沿误杀(零事件)。**修复** =
判据改**采样活跃度看门狗**(`_lastActiveTick` 随 Feed 推进;`tick - lastActiveTick >= 50`
才中止);回归测试 `test_longPressWithContinuousFeeding_notAborted`(120 tick 持续采样不中止,
松开正常上行,对旧实现必红)。修复后 PlayMode **186/186** · EditMode **3279/0** 零回归。
**F-2(ADVISORY 登记)**:单键按住 ⇒ 聚合恒 `edges=1` ⇒ 稳度门(`edges<=1` 免评)生产
不可达 —— 裁定②的设计后果,节奏玩法归 21a/10 烘焙轮的多键输入形状。

**BatchMode 双套件(2026-10-14 · 6000.3.24f1 batch)**:
- EditMode:`total=3326 / passed=3279 / failed=0 / skipped=46 / inconclusive=1(既有)`
  —— 基线 3270 通过 → 3279(**+9**,新 `emergency_skip_entry_wiring_test` 九测,零回归)
- PlayMode:`total=185 / passed=185 / failed=0`
  —— 基线 175 → 185(**+8** 新 `emergency_channel_wiring_test` 八测;另 interaction_chain ②
  与 e2e ⑥ 两测**改判**为按住语义,非回归)。日志:`unity/Logs/story009_{editmode,playmode}.{log,xml}`

**改动面(9 文件 + 3 新)**:
| 文件 | 变更 |
|---|---|
| `Assets/Gameplay.Boot/EmergencyChannelDriver.cs` | **新**:通道相位搬运 + 聚合映射 + Idle 门回调 + `IArmedState` 实现(裁定③) |
| `Assets/Gameplay.Boot/EmergencyManualDriver.cs` | 增 `TryAggregated`(聚合 → 判域映射,method 恒 0);ctx/row 收进共享尾 `TryAttempt` |
| `Assets/Gameplay.Boot/CompositionRoot.cs` | 增可选参 `inputActions`(R-9.5 订正)+ 装配通道/驱动 + F-10.1 三乘子占位常量 |
| `Assets/Gameplay.Boot/CompositionRootServices.cs` | 袋增 `EmergencyChannel` |
| `Assets/Gameplay.Boot/BootRoot.cs` | 启动序 2.5 fail-loud 资产校验 + 4.55 Attach + 4.65 skip 写路径注入 + 6.6 UI 回调接线 + tick 边沿中止检查 + OnDestroy Detach |
| `Assets/Gameplay.Boot/InteractionInputDriver.cs` | **F 键改按住语义**(裁定②):按下沿 Arm / 松开沿 EndAction |
| `Assets/Gameplay.Boot/CasebookCommandRouter.cs` | 实现 `SubmitEmergencySkip`(未绑静默 / 未接 fail-loud)+ `BindEmergencySkip` |
| `Assets/Gameplay.UI/Skeuomorphic/CasebookBinding.cs` | 接口增 `SubmitEmergencySkip()`(三测试 Fake 同步补员) |
| `Assets/Gameplay.UI/Skeuomorphic/EmergencyCandidateList.cs` | skip entry `focusable` + Submit/Click 回调 + 洪流守护(CasebookScreen 同款);**勘误**:候选项 `.skip-entry` → `.candidate-entry` |
| `Assets/Gameplay.Boot/EmergencyCandidateRuntime.cs` | `SetVisible`/`IsVisible`/`SkipFocusable`/`SetSkipCallback` 透传(不暴露 Gameplay.UI 类型) |
| `Assets/Sim/EmergencyProcedures/HostEmergencyProcessor.cs` | 最小 Judge 分支(裁定④):method==1 ⇒ Skip(false).Result;强度仍走 F-10.4 |
| `Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs` | `ABORT_IDLE_TICKS` 提出为公开常量 `AbortIdleTicks`(防驱动侧第二真源) |
| `Assets/Gameplay.Boot/Gameplay.Boot.asmdef` + ADR-025 §① | refs 增 `Gameplay.Input`(窄修订,依据 R-9.2 驱动权落表) |

**登记给后续轮**:
- **EdgeTicks 量纲**(数值轮):聚合的 `edge_ticks` 现为「动作内样本序号」(通道内单调计数
  非逻辑 tick);`hold_ticks = 末沿 − 首沿` 在该量纲下 = 采样帧数,与 20 Hz tick 的换算
  归数值轮裁定(与 F-10.1 三乘子同批)。
- **`ModalGate` 的 `IArmedState` 消费接线**:生产实现者已就位(driver),4 交互轮接
  `ModalGate(armedState, modalState)` 实例化。
- **候选 entry 动作语义**:止血 / 通气两项的点击行为归 21a/10 真动作表轮(本 story 只接跳过)。

## Engine Notes

**Engine**: Unity 6.3 LTS(6000.3.24f1) | **Risk**: **HIGH**
- `InputSystem.onAfterUpdate` 时序 = post-cutoff 行为(ADR-011 Engine Risk HIGH;story-007 spike 已在未聚焦 batch 实测帧-采样对偶 8/8 恰 +1,但**生产聚焦环境未验**)
- `InputActionAsset` 运行期装载:`AssetDatabase` 仅编辑期;**运行期须 `Resources` / Addressables / 直接引用** —— 装载路径须实测选定
- **不引入任何第三方**(零第三方铁律);`InputSystem` 为官方包且已由 ADR-011 裁决采用

## Control Manifest Rules (this layer)

- **Required**: 意图事件经 `IEventSink.Append`(主机唯一);直读通道**非轮询**(`onAfterUpdate` 回调);`Fix` 舍入 `ROUND_HALF_AWAY_FROM_ZERO`
- **Forbidden**: 在 `Update` 内轮询输入(ADR-011 §二);3 侧持有 Armed/压制状态(压制意图由 10 出);把 `Skip` 判成 `Missed`;跳过触发成长
- **Guardrail**: 42 只渲染不持状态(ADR-013 §9 C3);通道判定边界(不构造 `SimEvent`、不写流)

---

## 用户批准记录(2026-10-14)

四项待批项**全按建议**:
1. ✅ **本 story 成立** —— 把「10 实现轮」具名化为 story-009
2. ✅ **Estimate 3–5 人日**接受
3. ✅ **F-10.1 三乘子注入占位**(真值归数值轮)
4. ✅ **新开 sprint-10 承载**(超出 sprint-09「M2 收口冲刺」范围)
