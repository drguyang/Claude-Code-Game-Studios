# T13 急救动作表按 `ActionId` 取行 —— 实施与证据(2026-11-09)

> **范围**:急救动作的结算**按动作取表行**,缺行 fail-loud。
> **用户指令**:「走甲」。
> **上游**:T7(`6e93e025` 动作 2/3 的 id 空间已开)· `OQ-10-4`(P0 急救动作 2 → 4)。
> **本件只改「怎么取行」,不动任何数值。**

---

## 1. 病灶(实测)

`EmergencyManualDriver.TryAttempt` 的结算尾,本件之前:

```csharp
_processor.Process(attempt, BandageRow, ctx, patientId, tick, ResolveCause(), IsAnalogChannelAbsent);
```

`BandageRow`(动作 0)是**私有静态字段、无条件使用**;`attempt.Action` 从头到尾**没被读过**。
而 `EmergencyAttemptPayload.Action` 是**真的写进事件流**的字段。

⇒ 只要有路径把 `Action` 设成 1/2/3,判定会用**止血包扎的参数**(potency 1.0 / 幅度门 50 /
半衰期 100 …),而流里记的是「清创缝合」或「蛇伤处置」。**事件与判定各说各话,完全静默。**

### 今天不可达(所以是「哑的隐患」,不是活的 bug)

| 入口 | 动作值 |
|---|---|
| `InteractionInputDriver.cs:189` | `Arm(actionOrdinal: 0, …)` 硬编码 |
| `EmergencyManualDriver.TryManual` | `action: 0` 硬编码 |

两个入口都写死 0 ⇒ 现状行为与修改前**逐位相同**。

### 但它正好卡在下一步

T7 已经开了动作 2/3 的 id 空间。**谁下一个去「给清创加一行数据」,会加完什么都不发生** ——
因为行是静态字段,不是按 id 取的。

---

## 2. 改动清单

| # | 文件 | 层 | 内容 |
|---|---|---|---|
| 1 | `Gameplay.Boot/EmergencyActionTable.cs` | **新** | 动作 id → 行;`Require` 缺行即抛;`ProductionDefault` 只有动作 0 |
| 2 | `Gameplay.Boot/EmergencyManualDriver.cs` | 改 | 私有静态 `BandageRow` **删除**;新增 `_actions` 字段(ctor 可选第 4 参);结算尾改为 `_actions.Require(attempt.Action)` |
| 3 | `Tests/EditMode/EmergencyProcedures/emergency_action_table_test.cs` | **新** | **9 条**判据 |

**数值零改动**:动作 0 的九个字段与搬迁前逐字段相同,`ResultMul` 仍引用
`EmergencyActionSchema.GetResultMulTiers()`(不写副本 —— AC-10-16 / `OQ-10-1`)。
`Sim` / `Sim.Contracts` 引用集未动。

---

## 3. 判据(9 条)

| # | 判据 | 判别力 |
|---|---|---|
| 1 | 同一张表两行不同参数 ⇒ 各取自己那行 | 恒返回第一行 ⇒ 红 |
| 2 | `TryGet` 缺失 ⇒ `false` 不抛 | 「先问有没有」的调用方被逼进异常路径 ⇒ 红 |
| 3 | `Require` 缺行 ⇒ 抛,**报文点名动作号 + 已注册集合 + 下一步做什么** | 只说「找不到」会让人查 id 而不是查表 |
| 4 | 重复 id ⇒ 构造期拒收 | 后者静默覆盖前者,「用哪一行」取决于迭代序 = 不可重放 |
| 5 | 负 id / null 行 / null 集合 ⇒ 构造期拒收 | — |
| 6 | `ProductionDefault` 只注册动作 0 | 有人悄悄加了行却没更新证据 ⇒ 红 |
| 7 | `ProductionDefault` 的 `ResultMul` == schema 默认表 | 手写副本复活 ⇒ 红(历史漂移过一次) |
| 8 | **端到端**:同输入、两行 potency 不同 ⇒ 落流 `drug_potency` 跟着动作变 | **驱动忽略表 / 用硬编码行 ⇒ 两者相同 ⇒ 红**。这条是「真的按 action 取行」的唯一证据 |
| 9 | **缺行时流零事件** | 只看异常类型不够:下层也可能抛同类型异常,而流已被污染 |

> 第 7 条为什么保留:`ResultMul` 副本在历史上**漂移过一次**(`{0, 32768, 65536}` ⇒
> Missed 档归零,违反 AC-10-16 / `OQ-10-1`)。搬迁是「副本复活」的高风险时刻,故在此重钉一次。

---

## 4. 回归

**三元组**:`2026-11-09T22:18:23Z`(EditMode 结束)/ `22:19:30Z`(PlayMode 结束) · **全量** · `e2bd90fc` + 工作树。

| 套件 | 本次 | 对照基线(T12 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7326 / 7289 / 0 / 0 / 37`** | `7317 / 7280 / 0 / 0 / 37` @ `2026-11-09T17:18:29Z` · T12 批 · `e2bd90fc` | **+9 / +9** |
| PlayMode(全量) | **`373 / 368 / 0 / 2 / 3`** | `373 / 368 / 0 / 2 / 3` @ `2026-11-09T17:19:40Z` · T12 批 · `e2bd90fc` | **零变化** |

**等式核对**:EditMode 增量 **9** = `emergency_action_table_test` 的 `[Test]` 条数。

### 4.1 中途一轮编译红(我的写法错)

`EmergencyActionTable.cs` 里 `ProductionDefault` 用了全限定名、其余用简单名,却漏了
`using DaYiJingCheng.Sim.EmergencyProcedures;` ⇒ `CS0246` ×4。补 using 并统一为简单名。

---

## 5. 未做(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **动作 1/2/3 的表行** | **未做** | 需要**数值裁定**(potency / 幅度门 / 持时 / 半衰期 / 持续 / 极性)。本件只保证「加一行就生效」,不替数值定案 |
| 玩家**选择**动作的交互 | **未做** | 今日 `Arm(actionOrdinal: 0)` 硬编码 ⇒ 玩家无从选择。归 3 / 4 的输入轮 |
| 动作与**技法解锁**的联动 | **未做** | 技法已解但没表行 ⇒ 现在会**响亮拒绝**(不是静默错判),这已经是本件能达到的最强状态 |
| `TryManual` 的动作形参 | **未做** | 它仍硬编码 `action: 0`(跳过路径 = 止血包扎)。要不要让「跳过」也带动作,是交互裁定 |

> **一句话**:本件把「静默用错参数」换成了「响亮拒绝」,并让「加一行数据就生效」成立。
> 但动作 1/2/3 **仍然没有数值**,玩家也**仍然无法选择动作**。
