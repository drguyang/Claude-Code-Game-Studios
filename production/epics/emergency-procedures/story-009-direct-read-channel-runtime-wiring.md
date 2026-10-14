# Story 009: 急救直读通道运行期装配 + 跳过入口接线(M2 形态件③ 功能半)

> **Epic**: 10 急救动作
> **Status**: **Ready ⬜**(2026-10-14 立 · 同日用户批准转 Ready —— 四项待批项全按建议:① story-009 成立 ② Estimate 3–5 人日 ③ F-10.1 三乘子注入占位(真值归数值轮)④ 新开 sprint-10 承载)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 3–5 人日(约 24–40h)
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-14

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

**R-9.5 输入资产装载**:**复用系统 3 既有的 `InputService`**(`Gameplay.Input/InputService.cs`)——
它是「全案唯一 `InputActionAsset` 实例的持有者」(AC-3-A1),其类头明写「装载方(后续故事)换成
Addressables」⇒ **本 story 即该「后续故事」**。装载路径 = Addressables(工程已有
`Assets/AddressableAssetsData/`);`InputService` 的同一性断言(`GetInstanceID()`)继续守
「唯一实例」纪律。**不新建 Provider 类**(避免第二装载点,违 AC-3-A1)。

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

- [ ] **AC-9-01**[A] BLOCKING:`InputActionAsset` 运行期装载成功 ⇒ `Emergency` 动作可解析(`FindAction("Emergency")` 非 null);资产缺失 ⇒ 启动期 fail-loud(不静默 null)
- [ ] **AC-9-02**[A] BLOCKING:通道相位 —— `Idle` 态 `State == Idle` 且 `EmergencyActionEnabled == false`;`Arm(n)` 后 `State == Armed` 且 enabled == true;`EndAction()` 后回 `Idle` 且 enabled == false(承 `AC-3-E4`)
- [ ] **AC-9-03**[A] BLOCKING:直读采样 —— `Armed` 期逐帧 `Feed` ⇒ `SampleCount` 递增;`Idle` 期 `Feed` 被拒(零采样)
- [ ] **AC-9-04**[A] BLOCKING:**端到端可达** —— `Idle → Arm → Feed×N → EndAction` 走完 ⇒ 病史流出现 `EmergencyAttempt` + `EmergencyTreatmentApplied`(本 story 的核心判据)
- [ ] **AC-9-05**[A] BLOCKING:**跳过路径** —— 跳过入口 Submit ⇒ 病史流 `EmergencyTreatmentApplied` 且 `method == 1`、`JudgeResult == AppliedWeak`、**零 `SkillGrown`**(承 `AC-10-10`)
- [ ] **AC-9-06**[A] BLOCKING:**Idle 门** —— 通道 `Armed` ⇒ 候选列表 `IsVisible == false`;回 `Idle` ⇒ `IsVisible == true`(规则六之甲)
- [ ] **AC-9-07**[A] BLOCKING:**跳过入口焦点可达** —— 跳过入口 `focusable == true` 且在 `Idle` 态可经焦点导航 Submit(承 `AC-10-12` 的键盘半)
- [ ] **AC-9-08**[I] BLOCKING:**零数字** —— 跳过入口与候选项文本零数字(`AC-023-2` 门回归)
- [ ] **AC-9-09**[A] BLOCKING:**中止 ≠ 跳过** —— `Armed` 内停手超 `ABORT_IDLE_TICKS` ⇒ `AbortAction` ⇒ **零处置事件、零 `EmergencyAttempt`**(承 `AC-10-18` 回归)
- [ ] **AC-9-10**[L] ADVISORY:纯键盘单机走查全部动作(含跳过)零按键提示即完成 —— **NOT-RUN**(走查证据归人工轮)
- [ ] **AC-9-11**[L] ADVISORY:`L_input < 50 ms` 实机实测 —— **NOT-RUN**(归 OQ-10-12 原型门后续;原型已判成立 2026-09-29)

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
