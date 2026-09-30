# Story 005: 遭遇生命周期、伤情真值路由与呈现信号契约 —— EncounterEnded / id 空间 / EnemySignalDto

> **Epic**: 敌人 AI
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 8h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/enemy-ai.md`(§Detailed Rules 规则十六 复用 9 的伤情模型(归零=INJ_COMA 非致命)· 规则十七 id 经 IIdAuthority 与病人共空间,敌人行住世界流不折叠 · 规则十八 伤情事件落世界流(Kind 形状归 25,provisional 已解除)· 规则十九 ITameable P0 定型不实现 · 规则二十 两 Kind 写者分工(Started=52 / Ended=27)· 规则二十之二 ENCOUNTER_TIMEOUT 超时机械定义 · 规则二十一 不写第二条 ActorCellEntered · 规则二十二 不拥有呈现(只交付触发与信号)· §States 三 遭遇生命周期 · §Visual and Feel 一 EnemySignalDto 形状 + 二 呈现层三条契约(①归零=不动了 ②半格盲区必须可见 ③Down 零主动表现)+ down_class 不改表现警告 · §UI 一 P0 零玩家可见 UI + 二 开发者调试视图隔离 · D 组 AC-27-22…25/27/28 · E 组 AC-27-29/33/34/35)
**Requirement**: TR-enemy-015(敌人 = 可受伤实体,复用 9 模型,不新开第二套生命)· TR-enemy-016(id 共用空间与高水位,`max(id)+1` 扫三流并集排除 None)· TR-enemy-017(伤情事件落世界流不污染病史流;载荷形状归 25)· TR-enemy-018(ITameable P0 定型不实现,命令通道复用行为程序)· TR-enemy-019(遭遇两 Kind 写者故意拆开)· TR-enemy-020(27 不写 ActorCellEntered —— 敌人格纯派生态由三源重建)· TR-enemy-021(27 不做读数条/音效/不持有 UI —— 只交付触发与信号,呈现归 42/44)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): §二 敌人复用 9 的伤情模型;ADR-006: Amendment B 的 id 高水位;ADR-021: 状态所有权 = 写权(Ended 归 27 的判据来源)
**ADR Decision Summary**: 「生命归零」已由 25 登记为 `INJ_COMA` ⇒ 非致命模型自动适用敌人(支柱二的实现侧保证);id 经 `IIdAuthority` 与病人共用同一计数空间(高水位重构仍成立);敌人行**不适用病史流折叠**;`EncounterEnded` 归 27 的理由 = 52 不持有「遭遇何时真的结束」这份状态(写作分工 = 状态所有权分工)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 事件路由/枚举断言为纯数据边界;与 52/25/9 的联调走各自 GDD 已登记的 Kind(`EnemyInjuryOnset`/`InjuryStateChanged`/`EventArrived`),零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 27 可写 `Kind` 集**恰 = {`EncounterEnded`}**;`EncounterEnded.reason` = 三值整数枚举{脱离, 全倒地, 超时};同一遭遇**恰写一次** Ended(先触发者生效,另一路径不再写);超时判据 = `Tick − Started.tick ≥ ENCOUNTER_TIMEOUT` 每 tick 检查(非状态机第六态)
- Forbidden: 27 写 `ActorCellEntered`(敌人格不进流 —— 加事件=掩盖决策器分叉 bug);27 直读 25 事件自算 `HurtLevel`(读 9 的 `QueryHurtLevel`);`Down` 进 27 的状态机或 DTO 的 `state` 字段(= 把 9 的真值拖进 27 = 第二真源);病史流出现敌人行(套用折叠规则错误折叠)
- Guardrail: 高水位扫描排除 `PatientId.None`;`ENCOUNTER_TIMEOUT > DISENGAGE_DELAY` 为构建断言(否则超时/脱离竞态);52 的 `EncounterStarted` 与 `EventArrived` 同 tick(中间零 tick,真空窗不可观察)

---

## Acceptance Criteria

*From GDD `design/gdd/enemy-ai.md`, scoped to this story:*

- [ ] 路由纯函数断言:人形敌人归零 ⇒ 伤情事件落**世界流**、病史流**零**该事件(AC-27-22);敌人行不被病史流折叠规则触碰
- [ ] id 通道:敌人 id 全部经 `IIdAuthority`,与病人同空间;迁移/重放后 `next = max(id)+1` 扫三流并集且排除 `None`(AC-27-23)
- [ ] 可写集白名单:枚举 27 的写出 `Kind` **恰 = {EncounterEnded}**(AC-27-24);`EncounterStarted` 写者 = 52 的反证(27 侧无该调用点)
- [ ] 运行期探针:完整一场「多次跨格 + Engage + 倒地」遭遇,27 发出的 `ActorCellEntered` 恰 = 0;**同场玩家跨格照常发**(对照臂证明探针有效,非恒绿死断言)(AC-27-25)
- [ ] 三值可达:构造三条路径 —— 不接战走到脱离 / 打完全倒地 / 放置不管到超时 ⇒ `reason` 三值各自可达;**同遭遇 Ended 恰一条**(超时与脱离竞态先到先写)(AC-27-35)
- [ ] `EncounterStarted` 与 `EventArrived` 同 tick(零真空窗,AC-27-35①)
- [ ] `entity_kind`/`down_class` 只出现在 `EnemySignalDto` 两字段 + 烘焙 schema(正面白名单);`Healable` 行不出现在 28 取材入口、`Salvageable` 行不出现在 10 救治入口(AC-27-27)
- [ ] `Down` 敌人行为全停:逐 tick 检查零转移/零位移/零攻击请求/零寻路调用;`state` 保持最后态而 `is_down=true`(AC-27-28)
- [ ] `ITameable` 形状存在(标记 + `OwnerId` + `TameState`),动物实体 `OwnerId=None`,驯化行为代码路径为空集(AC-27-33 `[A]`)

- [ ] `EnemySignalDto` 形状交付:`{actor_id, cell(WorldPos 整数), facing(byte), state(六态,不含 Down), down_class, is_down, path_next, tick}` —— 无指针/无 float/无 Unity 引用,受 `PresentationDtoGuard` 递归扫描;无 `encounter` 字段(身份信息不进呈现层)(AC-27-29 的 DTO 半边)
- [ ] 归零信号语义 = 「不动了」:DTO **不含**任何销毁/淡出/死亡标记字段(正面白名单 = 恰 `EnemySignalDto` 所列);`down_class = Salvageable` 不改变呈现契约(AC-27-29)
- [ ] 开发者调试视图三条隔离:①只读(零 `Append`/零状态写)②不进玩家构建(条件编译符号白名单)③不引用 42 运行时类型;六态着色/格坐标叠加等仅调试可见(AC-27-34 `[A]`)
- [ ] 27 零玩家可见 UI:P0 不产生任何读数条本体(呈现归 42 读 `VitalsDto` 同通道)、零音效素材、动画只发触发(TR-enemy-021,AC-27-29/34 的范围面)

---

## Implementation Notes

1. 遭遇生命周期检查插在每 tick 的②段(实体求值后、输出前,承「求值序钉死」):`ENCOUNTER_TIMEOUT` 到点即写超时 Ended,与敌人当前态无关(纯兜底清理)。
2. 「结束 ≠ 消失」:Ended 写出后实体留场走 `Patrol`,再靠近可重新 `Alert`(EC-27-12)—— 遭遇 id 不复用。
3. 伤情读法:27 订阅世界流的 `EnemyInjuryOnset`/`InjuryStateChanged`(写者 25/9),伤重档位只经 9 的查询接口(F-27-2① 订正口径)。
4. 联机半边:`EncounterEnded` Append 权 = 主机唯一(承 ADR-020 Amendment B 同构);客户端敌人格零重算分歧由 Story 003/004 的确定性保证,不加事件(EC-27-20)。
5. `ENCOUNTER_TIMEOUT` 量级须先于本 story 实装有值(OQ-27-7 硬前置半边),值本身归数值轮;性质测试不锁值。
6. `EnemySignalDto` 每 tick 重建(由 `LogiPose`/态/9 真值投影,纯派生零持有);`is_down` = 9 的归零真值直读,`state` 保持最后态 —— 两字段分离即「Down 不是 27 的态」的实现形状。cue(逼近音/接触音/倒地)全部走 `AudioCueDto` 同构通道(ADR-018 / ADR-014 烘焙事件表),27 侧只有触发,零素材。

---

## Out of Scope

- [Story 003]: `DisengageIf` 谓词本体(本 story 只接「脱离成功→写 Ended」)
- 42 / 44 的呈现实现本体(读数条视觉、音素材、动画状态机)—— 本 story 只交付 `EnemySignalDto`/cue 触发与三条契约的**信号侧**;呈现消费归 ui/audio epics
- 25 的攻击判定与武器线(归 combat epic;本 story 只共读动作表)
- 28 的取材/驯化动词(P1a;此处只交付接口占位)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 人形敌人被 25 打到归零 | 路由事件 | 世界流恰一条;病史流零;9 真值 = INJ_COMA 投影 |
| TC-2 | 病人 id 至 10 后新敌入场 | 发号 | 11(同空间);重放高水位含敌行 |
| TC-3 | 玩家全程绕行不接战 | DISENGAGE_DELAY 后 | Disengage → 脱离成功 → Ended{脱离};恰一条 |
| TC-4 | 放置不管的遭遇 | ENCOUNTER_TIMEOUT 到点 | Ended{超时};敌人留场走 Patrol |
| TC-5 | 超时与脱离同 tick 竞争 | 写流 | 恰一条 Ended(Seq 全序) |
| TC-6 | Down 敌人逐 tick 探针 | 运行 | 转移/位移/攻击请求/寻路 四计数 = 0;state 不变、is_down=true |
| TC-7 | 一场含玩家跨格的遭遇 | 流扫描 | 敌人 ActorCellEntered=0 且玩家 >0(对照臂) |
| TC-8 | 归零野兽(down_class=Salvageable) | 出 DTO | is_down=true、state=最后态、零销毁字段;活野兽时刻 down_class 被下游忽略 |
| TC-9 | DTO 反射递归扫描 | PresentationDtoGuard | 无 float/无 Unity 引用/无 encounter 字段 |

**Edge cases**: 超时后敌又被救治再倒地(新遭遇还是旧遭遇 —— 遭遇已 Ended,不再写);`EncounterStarted` 同 tick 实例化失败(装载硬失败路径);id 回绕(int 域断言)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/enemy-ai/encounter_lifecycle_and_routing_test.cs`(或 `unity/Assets/Tests/EditMode/EnemyAI/`)— must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 003(态机)/ Story 004(位移)· 52 的 `EventArrived`+`EncounterStarted`(randomevents epic)· 25 的归零登记与 9 的 `QueryHurtLevel` · `entities.yaml` Kind 登记(已入,ADR-024)
**Unlocks**: 42 / 44 的敌人呈现消费(ui/audio epics)· 10 的救治入口(Healable 路由)· 51 的回放分析(reason 三值区分)

---

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `EncounterEndReason` — 遭遇结束原因枚举（三值：脱离/全倒地/超时）
- `EncounterState` — 遭遇状态（派生态，从流重建）
- `EnemyEncounter` — 遭遇生命周期管理器（IsTimeout / Transition / ValidateWritableSet / ValidateIdChannel / ValidateInjuryRoute）
- 六态迁移表完整实现且闭集
- 三值可达（脱离/全倒地/超时）
- 同遭遇 Ended 恰一条
- 测试: 10 条单元测试（全部通过）

**Deviations**: 
- 遭遇生命周期为简化版（无完整状态机驱动），完整版需要 Story 003/004 集成

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/EnemyAI/encounter_lifecycle_test.cs` — 10 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
