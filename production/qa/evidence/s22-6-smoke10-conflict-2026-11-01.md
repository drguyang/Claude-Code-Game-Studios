# S22-6 · smoke `#10` 冲突裁定(两案,不实现)

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-6` |
| **对象** | `tests/smoke/critical_paths.md` #10「急救(平面):一次完整动作落**两条**病史流事件;主机 `Judge` 出 `potency`」 |
| **争点** | 2026-10-11 用户裁定 **G4.1「急救手动化」** 与本条判据(动作 ⇒ 两条事件)是否仍兼容 |
| **执行轮** | 2026-11-01 · 用户指令「做 S22-6」 |
| **交付形态** | **只出两案,不实现**(卡面硬约束:裁前不开工任何 `#10` 夹具 —— 对象不存在时写夹具 = F2 假绿) |

---

## 0. 一句话结论

**冲突的前提是陈旧的**:G4.1 退役的是**自动触发驱动** `EmergencyAttemptDriver`,
而 `#10` 判据描述的是**手动动作**的事件形状 —— 那条手动路径**今天在、被接线、且已有两条载体**。
⇒ **甲案(判无冲突、挂既有载体)零代码、零裁定、零 ADR 改动**;乙案是把「手动化」读成
「事件模型要改」,那才需要真裁,且须推翻 ADR-009 Amendment I。

---

## 1. 事实核账(逐条可复跑)

### 1.1 G4.1 到底退役了什么

```bash
sed -n '1,20p' unity/Assets/Gameplay.Boot/EmergencyAttemptDriver.cs
```

> 「本文件已无生产作用 —— 自动触发分支 `_triggeredPatients` 已删除 …
> 触发权归玩家:手动入口 = `EmergencyManualDriver`。」

退役对象 = 「每 tick 对在场未触发病人**自动**触发一次」。
G4.1 的判据原文 = **零输入观察窗,病史流零 `EmergencyAttempt`**(`sprint-06.md:68`)。
⇒ **它约束的是「谁触发」,不是「触发后落几条事件」。**

### 1.2 手动路径今天在,而且被接线

| 环节 | 落点 |
|---|---|
| 直读通道(聚合) | `EmergencyChannelDriver` → `EmergencyManualDriver.TryAggregated` |
| 病案「跳过」项 | `BootRoot.cs:611` `CasebookCommands.BindEmergencySkip(_services.EmergencyManualDriver, actorId)` → `TryManual(method: 1)` |
| 结算(主机 Judge + 双写) | `EmergencyManualDriver.TryAttempt` → `HostEmergencyProcessor.Process` |

```bash
grep -n "_eventSink.Append" unity/Assets/Sim/EmergencyProcedures/HostEmergencyProcessor.cs
# :206  Append(EmergencyAttempt)
# :233  Append(EmergencyTreatmentApplied)
grep -n "drugPotency" unity/Assets/Sim/EmergencyProcedures/HostEmergencyProcessor.cs
# :212-213  JudgeEvaluator.ScaleFixed(action.BasePotency, …) ⇒ F-10.4 单一舍入
```

⇒ 「一次动作落两条」与「主机 Judge 出 `potency`」**两半都在生产代码里**。

### 1.3 已有载体(两条,且都是真装配)

| 载体 | 断言 | 装配形态 |
|---|---|---|
| `EditMode/EmergencyProcedures/applied_payload_settlement_test.cs:240` | 「一条完成动作**恰两条**病史流事件」+ 两条都带真实 `patient_id` | 真 `HostEmergencyProcessor` + 捕获 sink |
| `EditMode/EmergencyProcedures/emergency_skip_entry_wiring_test.cs:61` | 「恰**两条**流事件(非空转守卫)」+ `Method = Skip` 在流上可判别 | **`CompositionRoot.Assemble()` 真装配** + 真 `EmergencyManualDriver.TryManual` |

⇒ `tests/smoke/critical_paths.json` #10 记的「**生产路径未接线 ⇒ 无判据可挂**」
**与实测不符**;它把「自动驱动退役」误读成「整条急救写路径退役」。

---

## 2. 两案

### 甲案(推荐)—— 判「无冲突」,`#10` 挂既有双载体

| 项 | 内容 |
|---|---|
| **动了什么** | 只动账本:`critical_paths.json` #10 `none → test`,挂上 §1.3 两条;`critical-paths.md` 不动(其注释明写「标 none 的条目若日后被证明有载体,**只改本表,不改 md**」)。**零产品代码、零 ADR 改动。** |
| **判据怎么判** | `run_smoke.py --results <em.xml> <pm.xml>` ⇒ #10 判 `PASS(2 载/全绿)`;若任一载体转红即 `FAIL`。退出码仍是 3(剩余 5 条无载体)。 |
| **与 ADR-009 Amendment I 的关系** | **完全一致,一字不改**。Amendment I 的形态(`EmergencyAttempt` 判定输入 + `EmergencyTreatmentApplied` 结算)正是两条载体所钉的形状;G4.1 改的是触发器归属,与 Amendment I 正交。 |
| **风险** | 低。唯一风险是「载体是旧件、判据表述可能漂移」—— 用 §1.3 的 `file:line` 逐条复核即可(本轮已跑)。 |
| **代价** | 半天内的账本订正。 |

### 乙案 —— 「手动化」改变事件模型(需真裁)

| 项 | 内容 |
|---|---|
| **动了什么** | 改**事件模型**本体:一次手动动作只落**一条**(合并 `EmergencyAttempt` 与 `EmergencyTreatmentApplied`),或把 `potency` 从「主机 Judge 出」改为「客户端预表现定、主机只盖章」。这要**推翻 ADR-009 Amendment I**,另开 ADR;连带:`PayloadCodec` 两支编解码、`StreamRouting` 路由、10 的判定链、37 结案门、9 体征链、ADR-001 的可靠上行。 |
| **判据怎么判** | 新版判据须重写成「恰一条」并**重签** `#10` 的表述;`applied_payload_settlement_test` / `emergency_skip_entry_wiring_test` **同批改**(否则它们与新模型互相矛盾)。 |
| **与 ADR-009 Amendment I 的关系** | **正面冲突**:Amendment I 的存在理由就是「判定输入进流 ⇒ 可重放」(ADR-011 Amendment B 的三条改判理由之一)。合二为一 = 判定输入不进流 = 跨平台逐位重放不可能,`AC-10-05` 随之失效。 |
| **风险** | 高。它相当于把 2026-09-18 两条用户裁定(Amendment I + ADR-011 Amendment B)一起推翻,而已有 6+ 条夹具钉住现形态。 |
| **代价** | 跨 5 个系统的一次改型 + 一次架构裁定。 |

---

## 3. 不做的部分(按卡面)

- **不实现**任何一案的落地:本轮**没有**改 `critical_paths.json`、没有开 `#10` 的新夹具。
- 甲案若被采纳,落地只需一次账本订正(见其「动了什么」);**本轮不动**,等裁。

---

## 4. 结论建议

**建议取甲案。** 依据不是「省事」,而是 §1 的三条实测:退役对象是触发器、手动路径在工作、
两条载体都是真装配。乙案要成立,必须先有一位当事人主张「手动化意味着一次动作只该留一条事件」——
本轮没有找到任何这样的文字依据(GDD `emergency-procedures.md` 与 ADR-009 Amendment I 都要求两条)。

---

## 5. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "_triggeredPatients" unity/Assets/Gameplay.Boot/EmergencyAttemptDriver.cs   # 只在注释里(已退役)
grep -n "_eventSink.Append" unity/Assets/Sim/EmergencyProcedures/HostEmergencyProcessor.cs  # :206 / :233
grep -n "BindEmergencySkip" unity/Assets/Gameplay.Boot/BootRoot.cs                  # :611
grep -n "恰两条" unity/Assets/Tests/EditMode/EmergencyProcedures/*.cs                # 两条载体
```
