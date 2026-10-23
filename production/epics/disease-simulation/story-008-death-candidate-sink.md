# Story 008: 死亡候选产出侧（`IDeathCandidateSink`）与 F4 接线

> **Epic**: disease-simulation(系统 9)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Status**: Ready
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-24
> **ADR**: ADR-009 §二(三问判据:候选不进流)· ADR-016 §二(id 空间共用)· ADR-005(主机唯一 Step)
> **GDD**: design/gdd/disease-simulation.md(F4)· design/gdd/death-and-respawn.md(规则二 · `:90` / `:103`)

## Context

`S18-AC5` 的前半句 —— 「`DeathCandidate` 产出路径**有生产调用点**」—— 至今未满足。
实测(2026-10-24):

```bash
$ grep -rn "IDeathCandidateSink" unity/Assets --include='*.cs'
unity/Assets/Sim.Contracts/IDeathCandidateSource.cs:57:    public interface IDeathCandidateSink
unity/Assets/Sim/DeathCandidateConsumer.cs:27://   - 生产装配(9 侧 `IDeathCandidateSink` 实现 + 本类注册)归 9 / 29 的装配轮。

$ grep -rn "\.Publish(" unity/Assets --include='*.cs'
(零命中 —— 含 Tests/)
```

- 产出侧接口 `IDeathCandidateSink` **只有声明**,**零实现**;
- `Publish(` 全树(含 `Tests/`)**零命中** —— 它从未被调用过一次;
- 消费侧 `DeathCandidateConsumer`(`Sim/DeathCandidateConsumer.cs:42`)只有测试构造点。

### ⚠️ 本件不是「实现一个接口」—— 接线前有两个硬前提

**前提一:类别信息无载体。** `DiseaseVitalsService.cs:483` 把 F4 的实体类别**硬编码**
为 `ActorClass.Patient`。若照字面「判出 `Deceased` 就 `Publish`」,被 publish 的**必然是病人**;
而 `DeathCandidateConsumer.OnDeathCandidate(playerId, tick)` 的语义是「**玩家**应死」
⇒ 会铸出一条 `actor_id = 病人 id` 的 `PlayerDied`。**这是真 bug,不是接线。**
⇒ 本件必须先把类别从「硬编码」改为「可查」,并把 `Publish` 闸在 `ActorClass.Player` 上。

**前提二:玩家根本不进 F4。** `DiseaseOnsetPayload`(`Sim.Contracts/Payloads/HistoryPayloads.cs:32-46`)
五字段**无实体类别**;`PatientSpawner` 是唯一 `DiseaseOnset` 写者,`SpawnNext` 恒发新 `patient_id`;
`StepAll`(`DiseaseVitalsService.cs:319`)只遍历 `Courses.InOrder`。
玩家 actorId 虽与病人共用 `IIdAuthority` 空间(`BootRoot.cs:252`),但**玩家没有 `DiseaseOnset`**
⇒ **玩家永不进 F4** ⇒ 生产上 `Publish` **恒不因玩家触发**。

⇒ **本件交付的是「产出路径存在且正确」,不交付「玩家真的会死」。**
后者需要玩家染病路径(即 `AC-35` 结构半边 + 玩家侧 `DiseaseOnset` 入口),**不在本件面内**。
本件**不得**被读成「死亡链已可用」。

### 有界性(为什么必须只在转移那一 tick 发)

`Deceased` 在 F4 九态里是**吸收态**(`Sim/StateMachine.cs:61-73`)⇒ 一旦判出,此后每 tick 都是 `Deceased`。
若每 tick 都 `Publish`,`EventStream` 的去重键含 `PayloadRef`(`Sim/EventStream.cs:99-101`),
而每次编码产生新 blob ⇒ **不去重** ⇒ 事件率退化为「tick 数 × 玩家数」,
与 `design/registry/entities.yaml:2170` 的 `上界 ≤ 玩家数 / DEATH_COOLDOWN` 直接冲突。
⇒ `Publish` **只在状态转移进 `Deceased` 的那一 tick** 调用一次 ——
与 `InjuryStateChangedWriter` 的「未变化不写」同一条理由(`Sim/InjuryStateChangedWriter.cs:56-58`)。

## Acceptance Criteria

> **编号约定**:`AC-9-NN` 前缀在本 epic 内已用到 `AC-9-20`(全在 `story-007`)。
> 本件取 **`AC-9-21…28`** —— 实测 `AC-9-2[1-8]` 全库零命中(`grep -rn "AC-9-2[1-8]" --include='*.md' --include='*.yaml' .`
> 排除本件后为空)@ 2026-10-24。⚠️ 与 GDD `disease-simulation.md` 的 `AC-1…AC-44`
> 是**两套编号**,不可互读(`AC-9-01` 不是 `AC-9` 系统的 `AC-01`)。

| # | 判据 | 验证方式 |
|---|------|----------|
| AC-9-21 | `DeathCandidateSink` 实现 `IDeathCandidateSink`,住 `Sim`(门 A 内侧,零引擎引用) | 类型断言 + 门 A 反射断言 |
| AC-9-22 | **单消费者**:第二次 `Register` **抛错**(非静默覆盖);未注册时 `Publish` **静默丢弃且不抛** | 三条夹具:首注册通过 / 二注册抛 / 未注册 Publish 不抛且零通知 |
| AC-9-23 | `DiseaseVitalsService.StepAll` 在 F4 **转移进** `Deceased` 的那一 tick 调 `Publish` —— **生产调用点存在** | `grep -n "Publish(" unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs` 非空;夹具断言通知恰 1 次 |
| AC-9-24 | **类别闸**:`Publish` 仅当该实体类别 = `ActorClass.Player`;病人死亡**不产生**候选 | 同一夹具两例:类别 `Patient` ⇒ 零通知;类别 `Player` ⇒ 1 次 |
| AC-9-25 | **吸收态不重复发**:连续 N tick 停在 `Deceased`,`Publish` 通知数恒 = **1** | 驱动 N=50 tick,断言通知数 = 1 |
| AC-9-26 | 实体类别来源 = **注入的查询面**,`DiseaseVitalsService` 内**不得**硬编码 `ActorClass.Patient` | `grep -n "ActorClass.Patient" unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs` ⇒ 零命中(注释除外) |
| AC-9-27 | `CompositionRoot` 构造 `DeathCandidateSink` 并经 `CompositionRootServices` 暴露 | `grep -rn "new DeathCandidateSink" unity/Assets --include='*.cs' \| grep -v '/Tests/'` 非空 |
| AC-9-28 | **诚实边界登记**:玩家无 `DiseaseOnset` ⇒ 生产上 `Publish` 恒不因玩家触发。类头注与 GDD 注**双处**写明,不得宣称「玩家会死」 | 文本断言(两处可 grep)+ 本 story Out of Scope 复述 |

## Implementation Notes

1. **`DeathCandidateSink` 住 `Sim`**,与 `DeathCandidateConsumer` 平级。纯 C#,无引擎引用。
   单消费者语义:内部 `IDeathCandidateSource _consumer`;`Register` 时若非 null ⇒
   `throw new InvalidOperationException("死亡候选消费者已注册 —— 单消费者契约")`。
   `Publish` 时 `_consumer?.OnDeathCandidate(playerId, tick)`(**null = 静默丢弃**,P0 允许
   「29 未装配的构建里 9 照常跑」—— 逐字承 `IDeathCandidateSource.cs:68`)。

2. **类别查询面**:`DiseaseVitalsService` 构造增可选参
   `Func<int, ActorClass> actorClassOf = null`。null ⇒ 回退 `_ => ActorClass.Patient`
   (即当前行为,**注释写明这是未接线的合法入参**,与 `treatableBy` / `trendWindowTicks` 同格 ——
   见 `DiseaseVitalsService.cs:117-127` 的既有口径)。
   `EvaluateOutcome` 的 `:483` 改为查该面。

3. **转移检测**:`StepAll` 内比较「本次 `EvaluateOutcome` 结果」与 `_outcomes` 里的旧值;
   仅当 `旧 != Deceased && 新 == Deceased` 且类别 = `Player` 时 `Publish`。
   ⚠️ **顺序**:先读旧值,再写 `_outcomes`(现 `DiseaseVitalsService.cs:340` 是直接写)。

4. **不写流**:本件**不**引入任何 `IEventSink.Append`。`Publish` 是**进程内通知**,
   不是流事件(ADR-009 §二 三问判据:候选不满足「可感知」;`entities.yaml` 的 `SimEvent.Kind`
   条目里零命中 —— `grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml` = **36**
   @ 2026-10-24)。⚠️ `IDeathCandidateSource.cs:16` 写的是「35 个 `Kind`」,那是 2026-10-21 的时点值
   (ADR-032 追加 `PatientOutcomeChanged` 后为 36)⇒ 引用时须带时点,不得把 35 当现值。
   ⚠️ `PatientOutcomeChangedWriter` 的装配冲突(「`DiseaseVitalsService` 不写流」vs「终态落流」,
   见该写者类头注) **仍待裁**,不在本件 —— 本件不碰它。

5. **`Register` 的真实消费者不在本件**:`DeathCandidateConsumer` 依赖 29 story-004 的
   `DeathAllowed` 冷却门,而该件仍 `Ready`(未实现)。⇒ 本件只把 sink 建好并暴露,
   `Register` 由 29 的装配轮注入。**不得**为凑「有消费者」造一个假门。

6. **门 A 遵守**:`DeathCandidateSink` 只依赖 `Sim.Contracts`(接口)与 BCL。
   构造依赖 = 零(无 `IEventSink` / 无 encoder —— 本类不编码、不写流)。

## Out of Scope

- **玩家染病路径**(`AC-35` 结构半边)—— 玩家无 `DiseaseOnset` ⇒ 本件**不**宣称「玩家会死」。
- **`DeathCandidateConsumer` 的生产构造** —— 归 29 story-001 + story-004(冷却门未落)。
- **`PatientOutcomeChangedWriter` 接线** —— 待裁「不写流 vs 终态落流」的装配冲突。
- **`S18-AC5` 原 `verify_cmd` 的订正** —— 归记账轮;本件只提供新判据,不改旧命令原文。
  ⚠️ 原命令探的是**消费侧**构造(`new DeathCandidateConsumer|new PlayerDiedWriter|new InjuryStateChangedWriter`),
  而 AC 原文说的是「**产出**路径有生产调用点」—— 两半不是同一件事,不可互相顶替。

## QA Test Cases

| # | 用例 | 预期 |
|---|------|------|
| T1 | 首注册消费者 | 通过,后续 `Publish` 通知到它 |
| T2 | 二次注册 | 抛 `InvalidOperationException` |
| T3 | 未注册时 `Publish` | 不抛、零通知 |
| T4 | 合成 course + 类别 `Player`,驱动到 `Deceased` | `Publish` 恰 1 次 |
| T5 | 同上但类别 `Patient` | 零通知 |
| T6 | 停在 `Deceased` 再驱动 50 tick | 通知数仍 = 1 |
| T7 | 反射:门 A 引用集 | `Sim` 程序集零 `UnityEngine` 引用 |
| T8 | `DeathCandidateSink` 构造依赖 | 零依赖(无 `IEventSink` / 无 encoder) |

## Test Evidence

`unity/Assets/Tests/EditMode/DiseaseSimulation/death_candidate_sink_test.cs`(新,真装配
`CompositionRoot.Assemble`,零 Fake 被测对象)。
账本路径:`tests/unit/sim/`。

## Dependencies

- **不阻塞本件**:29 story-004(`DeathAllowed` 门)—— 阻塞的是 `Register` 的真实消费者,不是 sink 本体。
- **不阻塞本件**:`AC-35` 玩家染病路径 —— 决定「生产上 `Publish` 是否可达」,不决定「产出路径是否正确」。
- 承:`IDeathCandidateSource` / `IDeathCandidateSink`(`Sim.Contracts/IDeathCandidateSource.cs`,2026-10-22 落地)。
- 承:`F4OutcomeEvaluator.Evaluate`(`Sim/F4OutcomeEvaluator.cs:146`,已接生产)。
- 承:`S18-AC5`(`production/sprint-status.yaml` `conditions_open`)。

## Completion Notes

(待实现轮填写)
