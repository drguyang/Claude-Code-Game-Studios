# 实现原件 —— `TD-foraging-04`:17 采集的持续窗口

> **对象**:`TD-foraging-04`(债册 `open` · `P0 面 = N`)
> **批次**:2026-11-02 · 用户指令「修 TD-foraging-04」
> **上游裁定**:`OQ-17-3`(采集 = 持续交互,`SuppressesMotor = true`,时长归 17)
> **上游落值**:`duration_ticks = 20`(1.0 s @20 Hz,2026-11-02 · `OQ-20-11` 同批)
> **截至时刻 / commit**:2026-11-02 20:32:56 · `S23-27` · `fee91d04` + 工作树(读数见 §六)

---

## 一、开工前的实测(病因)

| # | 事实 | 复算 |
|---|---|---|
| 1 | 驱动**没有**持续窗口 —— 按下即结算 | `grep -n "duration\|hold\|Duration" unity/Assets/Gameplay.Boot/HarvestInteractionDriver.cs` ⇒ **零命中**(改前) |
| 2 | `AC-17-08`(采集中途被打断 ⇒ 零效果)**恒真不可判** | `OQ-17-3` 逐字:「若采集瞬时,该 AC 恒真不可判(判据空转)」 |
| 3 | `SuppressesMotor` 全仓**零运行期读者** | `grep -rn "SuppressesMotor" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 契约 / 门 / 烘焙三处 |
| 4 | `InjuryOnset` 全仓**零生产写者** ⇒ 「受伤打断」在 P0 结构上不可达 | `grep -rn "Kind.InjuryOnset" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 只有读方 |

⚠️ 事实 4 决定了本轮的判据怎么造:**不能**等真的挨打,只能用夹具落一条事件。

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Gameplay.Boot/HarvestInteractionDriver.cs` | 起表 / 每 tick 推进 / 打断 / 结算拆成四段;新增 `Advance` / `IsHolding` / `HoldElapsedTicks` / `CancelHold` |
| 2 | `Gameplay.Boot/InteractionPressRouter.cs` | 新增 `OnTick()`;采集分支改为「选中即消费」 |
| 3 | `Gameplay.Boot/BootRoot.cs` | tick 边沿调 `_pressRouter.OnTick()` |
| 4 | `Tests/EditMode/Foraging/harvest_interaction_driver_test.cs` | +7 条窗口夹具;既有 5 处「结算」断言改走「起表 + 推进」两段 |

### 五处设计判断(都写进了代码注释)

**① 事件时间戳 = 完成 tick,不是按下 tick。** 采集动作在窗口**满**时发生
⇒ `HarvestRequest.Tick = 起表 tick + duration_ticks`。既有夹具 `test_tryHarvestTarget_forwardsTickToStream`
的断言随之改写(它原先钉的是「意图的 tick 原样进事件头」)。

**② 「目标失效·走出交互半径」由「移动」覆盖,不另写一段。** 规则六列了三个打断源
(受伤 / 移动 / 目标失效)。草稿里我写了一条 `Chebyshev(playerCell, holdCell) > R` 的检查,
**它恒被「任何格变化都算移动」先行命中 ⇒ 不可达的守卫**(看着像判据,零判别力)。
⇒ 删掉,并在注释里写明它为什么不需要。目标失效的另一条真实形态(换目标 / 按到别的东西)
由 `TryHarvestTarget` 的取消分支承担。

**③ 同一目标重复按键不重置起点。** 持续按住时输入层**每 tick 都会发沿**;若每次重置,
窗口会被无限后推 ⇒ **永远采不完**(一个静默死循环形态)。

**④ 所有闸门在结算时重算,不用起表时的读数。** 1 秒内载重 / 余量都可能变
(别的玩家采了同一丛、玩家丢了东西);用旧读数 = 过期快照。

**⑤ 路由的「按键消费」语义必须改。** 采集改为「起表即消费」后,`HarvestAttempt.Attempted`
**恒为 false** ⇒ 若仍拿它当 `OnPress()` 的返回值,路由会判「没消费」而让调用方把同一个 E
再派给别的动作(查体 / 拾取)。这正是既有夹具 `test_tryHarvestTarget_..._attemptedEvenWhenGateRejects`
警告的失败模式。⇒ 采集分支改为**恒返回 true**(选中采集点即消费)。

---

## 三、判据(新增 7 条)

| # | 判据 | 测试 |
|---|---|---|
| ① | 未满时长 ⇒ 不结算、零事件、窗口仍在途 | `test_ac1708_hold_notSettledBeforeDuration` |
| ② | 满时长 ⇒ **恰好结算一次**;之后窗口清空 | `test_ac1708_fullDuration_settlesExactlyOnce` |
| ③ | **移动 ⇒ 零效果**(AC-17-08 打断源①) | `test_ac1708_movement_interrupts_zeroEffect` |
| ④ | **受伤 ⇒ 零效果**(AC-17-08 打断源②) | `test_ac1708_injury_interrupts_zeroEffect` |
| ⑤ | 别人的伤情不打断我(只断本人) | `test_ac1708_otherActorsInjury_doesNotInterrupt` |
| ⑥ | 重复按键不重置起点 | `test_ac1708_repeatPress_doesNotResetStart` |
| ⑦ | 换目标 / 目标不存在 ⇒ 旧窗口作废 | `test_ac1708_targetSwitch_cancelsPrevious` |

⚠️ ③④ 的「零效果」判的是**三件**:`Attempted == false`、事件数不变、`IsHolding == false` ——
只判其中一件会让「取消了但又写了事件」溜过去。

---

## 四、证伪轮(6 条变异,**全红**;SHA-256 逐字节还原 6/6)

| 变异 | 打的是什么 | 红在哪 |
|---|---|---|
| `M1` | 未满时长也结算(`if (false)`) | `hold_notSettledBeforeDuration` |
| `M2` | 去掉「移动」打断 | `movement_interrupts_zeroEffect` |
| `M3` | 去掉「受伤」打断 | `injury_interrupts_zeroEffect` |
| `M4` | 重复按键**重置**起点 | `repeatPress_doesNotResetStart` |
| `M5` | 结算前不清窗口(可重复结算) | `fullDuration_settlesExactlyOnce` |
| `M6` | 目标不存在时**不**取消旧窗口 | `targetSwitch_cancelsPrevious` |

每条**恰好命中一条**夹具(不是一片红)⇒ 七条判据各打各的靶。
`FINAL-OK` · `SUMMARY failures=none` · 退出码 0。

---

## 五、证明不了的事(禁借绿)

1. **`SuppressesMotor` 仍无运行期消费者。** 本轮实现的是**窗口**(时长 / 打断 / 结算),
   **不是移动压制**。窗口期间玩家仍可走动 —— 走动会**打断采集**(这正是规则六的语义),
   但「站定不能动」这一层仍未实现。归 **`TD-interaction-04`**(8 个模态共有;与之同批裁)。
   ⇒ **不得读成「采集已完全按持续交互实现」**:行为跑通了,压制面没有。
2. **`AC-17-08` 的「受伤」支在 P0 上仍不可达** —— `InjuryOnset` 零生产写者(§一 事实 4)。
   本轮把它实现并钉住了,但**没有真实伤害源能触发它**;等 25 的战斗线落地才可观测。
3. **端到端未走查** —— 本件全是 EditMode(驱动 + 路由 + 夹具)。
   「按 E 一秒后真的采到」需要 PlayMode / 手动走查(`AC-17-12` 是 `[L]`,仍 `NOT-RUN`)。
4. **`Advance` 的每 tick 调用点在 `BootRoot` 的 tick 边沿** —— 该段**无自动化判据**
   (与其它 `OnTickEdge` 同状态)。删掉它,EditMode 全绿而玩家永远采不到东西。

---

## 六、回归

**截至时刻 · 批次 · commit**:2026-11-02 · `S23-27` · `fee91d04` + 工作树。

| 平台 | 读数(total / passed / failed / inconclusive / skipped) | 时刻 | 基线(本批开工前) | 等式 |
|---|---|---|---|---|
| EditMode | `6620 / 6583 / 0 / 0 / 37` | 20:32:10 | `6613 / 6576 / 0 / 0 / 37` | **`+7`** = 本轮新增的 7 条窗口夹具 |
| PlayMode | `311 / 307 / 0 / 2 / 2` | 20:32:56 | `311 / 307 / 0 / 2 / 2` | `+0` |

本仓 PlayMode 常态退出码 = 2;判据取 XML 的 `failed` 计数 ⇒ `failed = 0`。

定向夹具(证伪轮基线):`DaYiJingCheng.Tests.EditMode.Foraging.HarvestInteractionDriverTest`
`total 18 / passed 18 / failed 0`(改前 11 条,本轮 +7)。

等式复算:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/tdf04-full-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/tdf04-full-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
grep -c "public void test_ac1708_" unity/Assets/Tests/EditMode/Foraging/harvest_interaction_driver_test.cs   # 期望 7
```

---

## 七、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | 草稿里的「超半径」打断是**不可达守卫** | 自查发现并删除,注释写明为什么不需要 |
| 2 | `_bag.EventSink.Append(new SimEvent(...))` ⇒ CS8156(`in` 形参要 lvalue) | 落局部变量再传 |
| 3 | `d.TryHarvest(in Intent(tick: 10), …)` ⇒ 同类 CS8156 | 同上 |
| 4 | 夹具 `targetSwitch` 首版假设「索引 99 越界」—— `StubWorld` 对**任意** `index ≥ 0` 都回同一个节点 | 改用负索引,并把「本夹具造不出另一个真实节点」写进注释 |
| 5 | 路由返回值未随语义更新(`Attempted` 恒 false ⇒ 按键被吞) | 自查发现,改「选中即消费」 |
