# 实现原件 —— `TD-interaction-04`:模态打开时玩家仍能移动

> **对象**:`TD-interaction-04`(债册 `open` · `P0 面 = N`)
> **批次**:2026-11-02 · 用户指令「继续」(承 `TD-foraging-04` 收口)
> **裁定**:两轮 `AskUserQuestion` —— **① 甲(4 出主意、Presentation 出手)**; **② 合并(模态 ‖ 采集窗口,同一个位)**
> **截至时刻 / commit**:见 §五

---

## 一、开工前的实测(四条硬事实)

| # | 事实 | 复算 |
|---|---|---|
| 1 | **`Gameplay.Boot` 不在租约白名单里** | `motor_lease_test.cs` 的 `AllowedCallerAssemblies` = `{Gameplay.Presentation, Gameplay.Input, Sim, Sim.Contracts, Sim.Codec}`;10 的实现轮实测过「在 Boot 里直接调 `Acquire` 判红」(`EmergencyChannelDriver.cs:161-164`) |
| 2 | **4 的写面早就存在、零调用方** | `ModalGate.SetMotorSuppression(MotorLease, bool)`,住 `Gameplay.Presentation`;全仓只有测试在调 |
| 3 | 移动**真读租约** | `PlayerController.cs:286` `if (_motorLease.IsSuppressed) moveInput = Vector3.zero;` |
| 4 | **GDD 早就在声称这件事** | `interaction-system.md` 规则八:`Utensil`/`ClinicPanel` 走模态 ⇒ `SuppressesMotor = false`,「**由 42 的模态门承担抑制**」;`Container` 2026-11-02 改 `false` 后也搭在这句话上 |

⇒ 事实 4 + 事实 1/2 合起来 = **一句没有落点的断言**:文档说有人管,代码里没人管。

---

## 二、裁定与实现

### 甲:4 出主意、Presentation 出手

| 件 | 落点 |
|---|---|
| 判据 | `InteractionPressRouter.ShouldSuppressMotor(modalOpen, harvestHolding) => modalOpen \|\| harvestHolding` |
| 驱动 | `InteractionPressRouter.OnTick()` 每 tick 重算 |
| 写面 | `ModalGate.SetMotorSuppression`(住 `Gameplay.Presentation`,拿到**第一个生产调用方**) |
| 绑定 | `BootRoot` 在 `_pressRouter = pressRouter` 之后后置绑定(`_player.MotorLease` + `() => _uiDriver?.BlocksPlay`) |

### 四处理由

**① 为什么两个合成一个位。** `LeaseSource.Self` 是**4 的位**,语义 = 「4 判定该定身」。模态开与采集
窗口在途**都是 4 的判定** ⇒ 一个写者、一个位、一条判据。`OQ-4-13` 修的是**跨源**丢失更新
(10 清位时顺手清掉 4 的),同源两件事合成一个布尔不属其面。

**② 为什么写面不能放 Boot。** `AC-1-23` 的机械门按**调用点所在程序集**判白名单,`Gameplay.Boot`
**不在**白名单内。⇒ Boot 只算**布尔**,写面留在 `Gameplay.Presentation`。**白名单一个字符未改**
—— 这与 `EmergencyMotorSuppression` 是同一处置,同一理由(「门红是因为位置错了,不是因为门太严」)。

**③ 为什么每 tick 幂等重算,而不是「开时置、关时清」。** 后者要为 8 个模态的**每一条**退出路径
各写一次清位,漏一条 = 玩家**永久定身**(4 的 GDD 正是这样警告的)。重算把「漏释放」变成
**结构上不可能** —— 位只跟着当 tick 的真值走。

**④ 为什么顺序是「先推进窗口、再算位」。** 结算会让窗口在**本 tick** 消失,应当同 tick 就清位;
反过来的话会多压一 tick。

### ⚠️ 未选乙(扩 `LeaseSource` 给 42),理由是可复算的

`MotorLease._bitmap` 是 **`int`**,`Acquire` 写的是 `_bitmap |= 1 << (int)source`。
**C# 规格:`int` 的 `<<` 把位移数按 `& 0x1F` 截断** ⇒ `1 << 42` 与 `1 << 10`(`Emergency`)
**落到同一格**。⇒ 走乙必须先把位图改 `ulong`(并处理既有三位),再动
`AC-1-23` 白名单(`Gameplay.UI` 不在其中)与 `leaseSourceOrdinalIsSystemNumber` 夹具。

⚠️ **本条我未能在本机跑出实测**:`dotnet` 可用但被权限策略挡住了 `dotnet run`。
⇒ 上面的截断结论**按 C# 语言规格陈述**,不是实测。**若将来真要选乙,第一步就是写一条夹具确认它。**
选甲则整条不涉及 —— 这正是选甲的一个附带收益。

---

## 三、判据(新增 4 条,并入 `interaction_press_router_test`)

| # | 判据 | 测试 |
|---|---|---|
| ① | 合成判据的**真值表**(四种组合逐一断言) | `test_tdi04_shouldSuppressMotor_truthTable` |
| ② | `OnTick` 把模态开合**如实**反映到 4 的位上(置 / 清双向) | `test_tdi04_onTick_bindsModalStateToSelfLease` |
| ③ | 未绑定 ⇒ **不写租约**(诚实降级:不压制,而不是压住不放) | `test_tdi04_onTick_unboundWritesNothing` |
| ④ | 只碰 4 那一位 —— **不得顺手清掉 10 的压制** | `test_tdi04_onTick_doesNotTouchOtherSources` |

---

## 四、证伪轮(4 条变异,**全红**;SHA-256 逐字节还原 4/4)

基线 = `2df360300d7e` —— 变异对象文件 `unity/Assets/Gameplay.Boot/InteractionPressRouter.cs` 的
SHA-256 前 12 位。
靶场 = `-testFilter DaYiJingCheng.Tests.Interaction.interaction_press_router_test`
(每条变异轮的 `total = 11`,即该类 11 条夹具)。

| 变异 | 打的是什么 | 红在哪 |
|---|---|---|
| `M1` | `ShouldSuppressMotor` 改 `=> modalOpen;`(**丢掉**采集窗口那一路) | `test_tdi04_shouldSuppressMotor_truthTable` |
| `M2` | `ShouldSuppressMotor` 改 `=> harvestHolding;`(**丢掉**模态那一路) | `test_tdi04_shouldSuppressMotor_truthTable` · `test_tdi04_onTick_bindsModalStateToSelfLease` |
| `M3` | `OnTick` **只置位、不清位**(去掉「模态关 ⇒ 清位」) | `test_tdi04_onTick_bindsModalStateToSelfLease` |
| `M4` | `OnTick` 传 `modalOpen: true`(**恒压制**) | `test_tdi04_onTick_bindsModalStateToSelfLease` · `test_tdi04_onTick_doesNotTouchOtherSources` |

```
BASELINE sha: 2df360300d7e
[M1] total=11 failed=1 红:['test_tdi04_shouldSuppressMotor_truthTable']
[M1] RESTORE OK
[M2] total=11 failed=2 红:['test_tdi04_onTick_bindsModalStateToSelfLease', 'test_tdi04_shouldSuppressMotor_truthTable']
[M2] RESTORE OK
[M3] total=11 failed=1 红:['test_tdi04_onTick_bindsModalStateToSelfLease']
[M3] RESTORE OK
[M4] total=11 failed=2 红:['test_tdi04_onTick_bindsModalStateToSelfLease', 'test_tdi04_onTick_doesNotTouchOtherSources']
[M4] RESTORE OK
FINAL-OK
SUMMARY failures= none
```

⚠️ **`M4` 的两条红各自落在不同的断言上**(据夹具原文逐条核对):

| 红的夹具 | 红的是哪一句 | 为什么 |
|---|---|---|
| `onTick_bindsModalStateToSelfLease` | `[前置] 模态关 ⇒ 4 的位置空` | `modalOpen: true` 恒真 ⇒ 第一次 `OnTick` 就把位举起来了 |
| `onTick_doesNotTouchOtherSources` | `4 自己那一位置空` | 该夹具的模态恒关,恒压制 ⇒ 4 的位残留 |

⇒ 判据④**不只在证伪轮里被动红**,它在「恒压制」这个最像「安全默认」的变异下**主动**指出
「举着不放」也是一种坏 —— 这是本轮四变异里唯一一条**从另一面**验证的判别力。

⚠️ **`M1`/`M2` 同时打红判据①**:真值表逐格断言,任一路丢失都必然红 —— 这说明①不是
「只看合成结果」的空洞正控(F4),它真的能分辨两条输入各自的贡献。

**窗口纪律**:四条变异各自「改 → 跑 → 还原」在同一条命令内完成,`RESTORE OK` 逐条比对
SHA-256(不是 `git checkout`),收尾 `FINAL-OK`。

---

## 五、回归

**截至时刻 · 批次 · commit**:2026-11-02 · `S23-28` · 工作树(基线 = `93f4f393`,即本批开工前的 HEAD)。

| 平台 | 读数(total / passed / failed / inconclusive / skipped) | 时刻 | 基线(本批开工前) | 等式 |
|---|---|---|---|---|
| EditMode | `6624 / 6587 / 0 / 0 / 37` | 20:53:51 | `6620 / 6583 / 0 / 0 / 37` | **`+4`** = 本轮新增的 4 条夹具 |
| PlayMode | `311 / 307 / 0 / 2 / 2` | 20:54:36 | `311 / 307 / 0 / 2 / 2` | `+0` |

本仓 PlayMode 常态退出码 = 2;判据取 XML 的 `failed` 计数 ⇒ `failed = 0`。

定向靶场(证伪轮用的那条 filter):`DaYiJingCheng.Tests.Interaction.interaction_press_router_test`
在**全量** EditMode XML 里数得 **11** 条用例、0 红 —— 与证伪轮每条变异轮的 `total = 11` 一致。

等式复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/tdi04-full-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/tdi04-full-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
grep -c "public void test_tdi04_" unity/Assets/Tests/EditMode/Interaction/interaction_press_router_test.cs   # 期望 4
```

---

## 六、证明不了的事(禁借绿)

1. **「玩家真的停住了」没有端到端判据。** 本件钉的是**判据 + 写入**;`PlayerController` 读租约
   那一段(`:286`)是既有代码,未在本轮改动、也未新增夹具。⇒ 「按开药箱的那一帧人真的停了」
   需要 PlayMode / 手动走查,本件**没有**。
2. **`OnTick` 的调用点无自动化判据。** 删掉 `BootRoot` 的 `_pressRouter.OnTick()` 一行,
   EditMode 全绿,而租约永不置位。与 `TD-foraging-04` 的 `Advance` 调用点同一状态。
3. **乙方案的移位风险未实测**(见 §二末)—— 本件按语言规格陈述,未跑。
4. **每 tick 重算依赖 tick 在跑。** 若 tick 停在某个模态打开的时刻,位会**留在置位状态**
   ⇒ 玩家停住。今日 `Pause` 只挡交互意图、不停 tick(ADR-013 §十-C),故不发生;
   但这是**前提**,不是本件证明过的性质。

---

## 七、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | 把「`1 << 42` 撞 `1 << 10`」当既成事实写进方案 | 标注为**按语言规格陈述、未实测**(`dotnet run` 被权限挡住),并写明「选乙的第一步就是写夹具确认」 |
| 2 | 首版 GDD 补丁的锚串写错(漏了行首缩进)⇒ 断言失败 | 重新定位原文后重打;文件未被写坏 |
