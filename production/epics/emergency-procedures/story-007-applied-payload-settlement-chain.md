# Story 007: `EmergencyTreatmentApplied` 载荷的结算链补完 —— 九字段齐备

> **Epic**: 10 急救动作
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-03

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则五 `:195-224` 载荷定义 · F-10.4 `:604` 处置强度 · 规则十一 结算链)
**Requirement**: **AC-10-06(BLOCKING)** —— 检索 `EmergencyTreatmentApplied` 载荷,`THEN` **七项齐备**
(`polarity` / `drug_potency` / `half_life` / `处置_id` / `施予者` / `method` / `cause`)+ `Seq` 由主机填充。

🔴 **根因(2026-10-02 由 b6 载荷手搓门首次查出)**:
`Sim/EmergencyProcedures/HostEmergencyProcessor.cs:59` 以手搓法构造该载荷:

```csharp
new PayloadRef(attempt.Action, (int)result, 0)   // 三字段,其中一项还是错的
```

**九字段中只填了 2 项,且 1 项错**(详见 §现状实测)。**该缺陷未被前两轮评审、对账件或任何既有测试发现** ——
因手搓法**不抛异常、不越界**,静默通过全部既有判据(既有测试只断言 `Kind` 与事件条数,不读载荷字段)。

**为什么单开 story 而非就地补字段**:九字段的填充**依赖 10 的结算链**
(F-10.4 求值 · `method`/`cause` 的判定路径 · `Seq` 发号),而该链**尚未完整实现** ——
手搓法正是「链没建好、先糊一个」的产物。**只修字段不建链 = 再糊一次**。

**ADR Governing Implementation**: ADR-029(`IPayloadEncoder` —— 载荷编码唯一路径)· ADR-009 Amendment I(三病史流 Kind)· ADR-006 §三(单一舍入模式)· ADR-005(主机唯一 Append)
**ADR Decision Summary**: 载荷经 **`IPayloadEncoder`** 编码,零手搓。
**`Sim` 引用集一字不改**(ADR-025 §①:111)。`drug_potency` 须经 **F-10.4 的单一舍入**
(Q32.32 中间积,`ROUND_HALF_AWAY_FROM_ZERO` —— 承 R-2/A8)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数定点运算 + 装配内改动,零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 判定全在**整数 / Q16.16 域**,**单一舍入模式** —— ADR-006 §三
- Guardrail: `Sim` 内**不得手搓 `PayloadRef`** —— 唯一合法路径 = `IPayloadEncoder`(ADR-029 §③,由 **b6 门**强制)
- Required: `Seq` 由**主机**在 `Append` 时发号(施予者客户端**不填**)—— GDD 规则五

---

## 现状实测(2026-10-03 · HEAD `3e62020`)

九字段语义经 **GDD `:201-224` · `entities.yaml:2280` · codec `:1-9` 三方核对一致**。
手搓法 `new PayloadRef(attempt.Action, (int)result, 0)` 的逐字段后果:

| # | 字段 | 类型 | 手搓给了什么 | 后果 |
|---|---|---|---|---|
| 1 | `Tick` | `i64` | ❌ 未载 | 丢失 |
| 2 | `TreatmentId` | `i32` | `attempt.Action` | ⚠️ **碰巧对** —— 但取自 attempt 而非 `EmergencyAction` 查表 |
| 3 | `ActorId` | `i32` | `(int)result` | 🔴 **`result` 是 `JudgeResult`(判定结果),被当成施予者 id** |
| 4 | `Polarity` | 枚举 | `0` | 🔴 **恒 0** —— 从未查处置词表 |
| 5 | `DrugPotency` | `Fix` | ❌ 未载 | 丢失(**9 的 F4 对因判定拿不到**) |
| 6 | `HalfLife` | `i64` | ❌ 未载 | 🔴 **恒 0 ⇒ 触发 9 的 Decay 除零**(registry 明写 AC-28 写入期拒收) |
| 7 | `Method` | 枚举 | ❌ 未载 | 丢失(**「跳过」在流上不可判别**) |
| 8 | `Cause` | 枚举 | ❌ 未载 | 丢失 |
| 9 | `Seq` | `i64` | ❌ 未载 | 丢失(应由主机发号) |

同文件 `:50` 的 `EmergencyAttempt` 载荷**同病**(8 字段只写 3 个,
丢 `MagPeak` / `MagLast` / `Method` / `ActorId` / `EdgeTicks`)—— 见 AC-10-40。

---

## Acceptance Criteria

*From GDD 规则五 `:195-224` + AC-10-06 + F-10.4:*

- [ ] **AC-10-39(BLOCKING)** —— **`EmergencyTreatmentApplied` 九字段齐备**(AC-10-06 的「七项 + `Seq`」):
  `Tick` / `TreatmentId` / `ActorId` / `Polarity` / `DrugPotency` / `HalfLife` / `Method` / `Cause` / `Seq`
  **逐字段**取值正确,**零手搓 `PayloadRef`**。
  - **`Polarity`** 须由**处置词表查得**(9 的 `treatable_by[]` 每项带极性),**不得恒 0**;
  - **`HalfLife`** 须取自**动作数据表** `half_life_ticks[action]`,**不得为 0**
    (registry 明写「恒 0 会触发 9 的 Decay 除零,AC-28 写入期拒收」);
  - **`ActorId`** 须为**施予者 id**,**不得填 `JudgeResult`**;
  - **`Method`** ∈ {Manual, Skip};**`Cause`** ∈ {玩家选择, 降级};
  - **`Seq`** 由**主机**发号。
- [ ] **AC-10-40(BLOCKING)** —— **`EmergencyAttempt` 载荷八字段齐备**(同文件 `:50` 的同病):
  `Action` / `HoldTicks` / `Edges` / `MagPeak` / `MagLast` / `Method` / `ActorId` / `EdgeTicks`。
  ⚠️ `EdgeTicks.Length == Edges`(codec 侧跨字段约束,违反 ⇒ `ArgumentException`「坏数据不进字节面」)。
- [ ] **AC-10-41(BLOCKING)** —— **`drug_potency` 经 F-10.4 单一舍入**:
  `RoundFix(BASE_POTENCY[action] × ResultMul[JudgeResult]) / MUL_ONE`,
  中间积落 **Q32.32**,**全程只做一次舍入**,模式 = `ROUND_HALF_AWAY_FROM_ZERO`(ADR-006 §三)。
  ⚠️ **负例须覆盖 R-2/A8 的 ulp 反例**:`(32769 × 16384) ÷ 65536 = 8192.5`
  ⇒ C# 整数 `/` 向零截断得 **8192**(错),正确 = **8193**。
- [ ] **AC-10-42(BLOCKING)** —— **`HostEmergencyProcessor` 零手搓 `PayloadRef`**;
  载荷经 `IPayloadEncoder` 编码。
- [ ] **AC-10-43(BLOCKING)** —— **`Sim` 引用集仍 = `["Sim.Contracts"]`**;b6 门绿。
- [ ] **AC-10-44(BLOCKING)** —— **b6 门的两条具名豁免被清除**
  (`AssemblyGates.PayloadRefWaivers` 中本文件的两条),且**不得新增**豁免。
- [ ] **AC-10-45** —— **`Missed` 仍照常发处置事件**(GDD `:716`):
  `drug_potency = BASE_POTENCY × 0.25`,**不发任何提示**(反幻想;音频侧由 AC-44-09 白名单守)。

---

## Implementation Notes

*Derived from GDD 规则五 / F-10.4 / 规则十一:*

### 输入面须扩张(本 story 的核心工作量)

现签名:
```csharp
Process(EmergencyAttemptPayload attempt, EmergencyActionRow action, JudgeContext ctx, long tick)
```

**可从既有输入取得**:
- `TreatmentId` ← `action`(动作枚举)· `ActorId` ← `attempt.ActorId`
- `Polarity` ← `action`(处置词表带极性)· `HalfLife` ← `action`(动作数据表 `half_life_ticks`)
- `Tick` ← 参数 · `Method` ← 结算路径(见下)· `Seq` ← 主机发号
- `DrugPotency` ← **F-10.4**(需 `BASE_POTENCY[action]` × `ResultMul[result]`)

**须扩张或确认来源**:
- **`DrugPotency` 的求值点**:F-10.4 需 `BASE_POTENCY[action]`(动作数据表,ADR-014 烘焙)。
  ⇒ 确认它是否已在 `EmergencyActionRow` 内;若不在,须一并补入数据表/行结构。
- **`Method` / `Cause`**:GDD 规则十一说二者「由规则六 / 十一的结算路径决定」。
  ⇒ 须确认 `Process` 的输入面是否足以判定「Manual vs Skip」与「玩家选择 vs 降级」;
  不足则扩张签名(**不得**用默认值糊过去 —— 那会让「跳过」在流上不可判别,
  正是 R-1 要防的)。
- **`Seq` 发号**:GDD 明写「主机在 `Append` 时发号,施予者客户端**不填**」。
  ⇒ 须确认发号点(现有 `_idAuthority` 是否承载,或归 `IEventSink` 实现)。

⚠️ **本 story 的 Estimate 含上述三处的确认成本**;若其中任一处需新裁(如数据表结构变更),
**须先回报,不得擅自扩面**。

### 其余实现纪律

- **载荷 struct 已存在**(`Sim.Contracts/Payloads/HistoryPayloads.cs:95`,九字段),
  **无需改动** —— 本 story 只补**填充逻辑**与**编码路径**。
- **`EmergencyAttempt` 侧**(AC-10-40)同理:struct 八字段已存在,只补填充。
- **构造注入 `IPayloadEncoder`**:`HostEmergencyProcessor(IEventSink, IIdAuthority)` → 加形参。
  调用点 = `Tests/PlayMode/EmergencyProcedures/host_authority_test.cs`(2 处)。
- **测试缝**:注入 fake encoder + 内存池,保持单测无 I/O。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- **F-10.4 的 `ResultMul[]` 数值**(`Applied=1.0` / `AppliedWeak=0.5` / `Missed=0.25`)——
  **已由用户裁定 2026-09-18(`OQ-10-1` 结清)**,本 story **不改数值**,只实现求值与舍入
- **`BASE_POTENCY[action]` 的数值** —— 归数值轮(数值用户自己调)
- **`OQ-10-6`(`EmergencyAction` 枚举归属)** —— **已裁定归系统 10**(见 `oq-adjudication-2026-10-01.md`);
  本 story 只是它的消费者,不重开该裁定
- **`Polarity` / `HalfLife` 的处置词表与动作数据表内容** —— 归 ADR-014 烘焙管线与 10 的数据轮
- **`DrugTreatmentApplied`(11 写)** —— 载荷形状一致但**写者是 11**;
  11 的 F-11.2 与 `method` 恒 `Manual` 的空字段问题归 **11 的 GDD 轮**
- **`EmergencyTreatmentApplied` 的 `Missed` 提示音** —— 反幻想门归 44(`AC-44-09` 白名单断言)
- **`Sim` → `Sim.Codec` 引用边** —— **已裁禁**(ADR-025 §①:111),不得借本 story 打开

---

## QA Test Cases

*Written at story creation (lean mode).*

- **AC-10-39**:九字段齐备。
  - Given: 一次 `Applied` 判定,`action` 为某具名动作(带非零 `Polarity` / `HalfLife`)。
  - When: `Process` 落流,从池取字节 → `PayloadCodec.Decode<EmergencyTreatmentAppliedPayload>`。
  - Then: **九字段逐一对**;特别是 `Polarity ≠ 0`、`HalfLife ≠ 0`、
    `ActorId == 施予者 id`(**不得等于 `(int)JudgeResult`**)。
  - Edge cases: `Method = Skip` 与 `Manual` 两路**须在流上可判别**;
    `Cause` 两档同理。
  - Negative fixture: **还原手搓法** ⇒ `HalfLife == 0` 与 `Polarity == 0` ⇒ 红。

- **AC-10-40**:`EmergencyAttempt` 八字段。
  - Given: `edges = 2` 且 `edgeTicks` 长度 2。
  - When: 落流 → 解码。
  - Then: 八字段逐一相等,`EdgeTicks` 逐元素相等。
  - Edge cases: **`Edges ≠ EdgeTicks.Length`** ⇒ codec 抛 `ArgumentException`
    (「坏数据不进字节面」)—— 断言该抛,而非静默。
  - Negative fixture: 还原手搓法(3 字段)⇒ `MagPeak` / `Method` / `ActorId` 读不回 ⇒ 红。

- **AC-10-41**:F-10.4 单一舍入。
  - Given: **R-2/A8 的 ulp 反例** `BASE_POTENCY × ResultMul` 使中间积 = `32769 × 16384`。
  - When: 求 `drug_potency`。
  - Then: `8193`(**非 8192**)—— 证明用的是 `ROUND_HALF_AWAY_FROM_ZERO` 而非 C# 截断。
  - Edge cases: 三档 `ResultMul`(1.0 / 0.5 / 0.25)各一例;`Missed` 须**非零**。
  - Negative fixture: 用 `a * b / c` 的朴素写法 ⇒ `8192` ⇒ 红。

- **AC-10-42 / AC-10-44**:手搓面清除。
  - Given: `HostEmergencyProcessor.cs` 源文本(剥注释)+ b6 门。
  - When: 扫描。
  - Then: `new PayloadRef(` 零命中;`PayloadRefWaivers` 中本文件的两条**已删**。
  - Negative fixture: 保留任一豁免 ⇒ 红(豁免数变化须显式复核)。

- **AC-10-45**:`Missed` 照常发事件。
  - Given: `JudgeResult = Missed`。
  - When: `Process`。
  - Then: **仍发 `EmergencyTreatmentApplied`**,`drug_potency = BASE_POTENCY × 0.25`(非零)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/EmergencyProcedures/applied_payload_settlement_test.cs` — must exist and pass
  (九字段齐备 + 八字段齐备 + F-10.4 ulp 反例 + 手搓面零命中 + 豁免已清 + Missed 非零)
- ⚠️ **既有 `Tests/PlayMode/EmergencyProcedures/host_authority_test.cs` 须同步**(构造加 `IPayloadEncoder` 形参),
  其 4 例须保持全绿

**Status**: [ ] Pending — story not yet implemented

---

## Dependencies

- Depends on: **`persistence-service/story-002`(契约支)** ✅ Complete 2026-10-02 —— `IPayloadEncoder` 已可用
- Depends on: **`EmergencyActionRow` 是否已含 `BASE_POTENCY` / `half_life_ticks` / 极性**(须开工首步确认)
- Unlocks: b6 门豁免清零(全库零手搓)· 9 的 F4 对因判定拿到 `drug_potency` ·
  51 的 `OQ-51-9` 遥测口径拿到 `method`/`cause`

---

## Completion Notes

**Completed**: _待实现_
**Criteria**: _待填_
**Deviations**: _待填_
**Test Evidence**: _待填_
**Code Review**: _待填_
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
