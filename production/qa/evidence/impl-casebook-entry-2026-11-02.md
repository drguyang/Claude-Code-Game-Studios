# 实现原件 —— 开册入口轮(结清 `TD-casebook-01`)

> **对象**:`docs/tech-debt-register.md` 的 `TD-casebook-01`(脉案屏生产不可达)
> **批次**:`S23-12`(sprint-23 收尾清账后追加;用户指令「修 TD-casebook-01(开册入口)」)
> **截至时刻**:`2026-11-02 07:22 CST` · **commit**:`f96ed10a` + 本批未提交工作树
> **结论**:债的三条消解判据**全部兑现**;同批新揭两条债务(`TD-interaction-01` / `TD-diagnosis-01`),
> 均已按 R4 立册,`未闭 P0 面` 回到 **0**。

---

## 一、修的是什么

债册原文三条判据:① 生产侧存在 `OpenModal(ModalId.Casebook)` 调用点;
② 触发源是**玩家 `Interact`**(不是 tick 边沿 / 不是自动);③ 夹具证明「玩家没交互 ⇒ 零 `CaseOpened`」。

原状(2026-11-02 实测):

| # | 现状 | 依据 |
|---|---|---|
| ① | 生产侧 `OpenModal` 只有 `Pause` / `SaveSlots` 两个调用点 | `grep -rn "\.OpenModal(" unity/Assets --include=*.cs \| grep -v /Tests/` |
| ② | 立案由 `CaseOpenedDriver.OnTickEdge(tick)` **每 tick 自动**开案 | 与 `case-system.md:176`「立案是一次玩家动作」/ `:927`「玩家没去 ⇒ 不立案」**方向相反** |
| ③ | 无任何夹具守「无交互 ⇒ 零立案」 | —— |

---

## 二、改了什么(七件)

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Gameplay.Boot/CaseOpenedDriver.cs` | **删** `OnTickEdge`(自动开案),**增** `TryOpen(PatientId, long tick)`。判定(在场 / 去重 / `PatientId.None`)全在 `CaseOpenWriter` 内,驱动不复制第二份 |
| 2 | `Gameplay.Boot/InteractionInputDriver.cs` | `TryExamine` → **`TryVisit`**:① 立案 ② 查体(P0 就近)③ 开册。新增两个注入缝 `caseOpen` / `openCasebook` |
| 3 | `Gameplay.Boot/BootRoot.cs` | 删 tick 边沿的自动开案调用;4.5 段接两个新缝(`openCasebook` **懒求值** —— `_uiDriver` 在本段之后才建);新增 `NearestPatientCellDistance()` |
| 4 | `Gameplay.Boot/ModalInputDriver.cs` + `ModalOpener` 接口 | 新增 `IsCasebookOpen`;`OnFrame` 的 toggle 分支**先判脉案** ⇒ Escape = 合册,**不**在脉案上叠暂停壳(存档位仍沿用「换屏回暂停壳」,未改) |
| 5 | `Gameplay.Boot/InteractClaimRoute.cs` | **跨种类比距**:采集只在**比最近病人更近**时才消费按键(原「半径内存在采集点就消费」) |
| 6 | `Gameplay.Boot/HarvestInteractionDriver.cs` + `HarvestInteractionBridge.cs` | 新增**零副作用窥视** `PeekNearestForageDistance` —— 与 `TryHarvest` 走同一 `ClipToRadius → Select` 前半段,不是第二份判据 |
| 7 | 夹具 6 件 | 新增 `Casebook/casebook_entry_wiring_test.cs`(4 条);`case_opened_wiring_test` 判据**翻转**;4 件既有夹具的 `OnTickEdge` 调用改 `TryOpen`;`FakeOpener` 补 `IsCasebookOpen` |

### 2.1 第 5 / 6 件为什么必须一起做(否则是「形式修好、实际没通」)

第一次跑通测试时 `CaseOpened` 一条都没有。探针给出的因是:

```
[VisitDiag] frame interact=true held=False allowed=True
[VisitDiag] claim pick=False harvest=True cell=0,0,0
```

`r_interact = 3`(`assets/data/interaction_kinds.json`),而 `world_resources.json` 只有 4 个资源点
⇒ **几乎所有可站格都在某个采集点半径内** ⇒ 每一次 E 都被采集链消费,
`TryVisit` 根本轮不到。把玩家挪到病人格 `(-1,1,2)` 后仍是 `harvest=True`。
⇒ 只补 ①②③ 三件(立案 + 开册)会**通过判据而在实机仍打不开册**,那是本仓点名过的「借绿」形态。

`InteractClaimRoute` 由此从「存在即抢」改为「**更近才抢**」。

---

## 三、判据与载体

| 判据(债册「何时消解」) | 载体 |
|---|---|
| 生产 `OpenModal(ModalId.Casebook)` 调用点 | `BootRoot.cs` 4.5 段 `openCasebook:` 缝 |
| 触发源 = 玩家 `Interact` | `InteractionInputDriver.OnFrame` E 按下沿 → `TryVisit` |
| 玩家没交互 ⇒ 零 `CaseOpened` | `case_opened_wiring_test.test_caseOpenedDriver_doesNotOpenCaseWithoutInteraction` · `casebook_entry_wiring_test.test_ce_01_noInteract_noCaseOpened` |

新增夹具的四条(全部走**真启动序**,不注入桩):

| 测试 | 断什么 | 判别步 |
|---|---|---|
| `test_ce_01_noInteract_noCaseOpened` | 病人已在场 + tick 前进(前置自证)⇒ 零 `CaseOpened` | 否定面 |
| `test_ce_02_interact_opensCaseAndCasebook` | 虚拟 E ⇒ 恰 1 条 `CaseOpened` **且** `CurrentModal == Casebook` | **两个判别步缺一不可** —— 只断事件 = 立案通了屏没开(本债原状);只断屏 = 屏开了没立案 |
| `test_ce_03_secondInteract_noNewCaseButCasesbookReopens` | 合册后再就诊 ⇒ 仍 1 条(AC-37-26),但仍开册 | 幂等 / 一次性 |
| `test_ce_04_escapeWhileCasebook_closesInsteadOfPause` | Escape = 合册,且合册零写流 | 定序 / 互斥 |

⚠️ **键位注入的三件套**(编辑器输入行为 + 后台行为 + 更新掩码去掉 `Editor`)承
`interaction_input_chain_test` 同款;`case_opened_wiring_test` 只守否定面(不需要注入)。

---

## 四、证伪轮(5 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 5/5)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 删 `_caseOpen?.Invoke(...)` | 2 红:ce_02 / ce_03 | exact(立案面) |
| **M2** | 删 `_openCasebook?.Invoke()` | 3 红:ce_02 / ce_03 / ce_04 | exact(开册面) |
| **M3** | 把自动开案**回填**进 tick 边沿 | 1 红:ce_01 | **exact**(否定面的判别力) |
| **M4** | 比距退回「存在即抢」(`< int.MaxValue`) | 3 红:ce_02 / ce_03 / ce_04 | exact(路由面) |
| **M5** | 停用 `IsCasebookOpen` 分支 | 2 红:ce_03 / ce_04 | exact(关册 toggle) |

**M3 首轮 NO-XML**:初版变异写法是「回填 `OnTickEdge` 调用」,而该方法**已删除** ⇒ 编译红、无 XML。
改成「回填一段 `foreach PresentPatientIds → TryOpen`」后转红 —— **记在这里是因为「编译红 ≠ 判据红」**
(证伪轮的失败模式之一:变异打错靶)。

**还原**:五个文件的 SHA-256 在变异前后**逐字节相同**(`RESTORE OK`),未用 `git checkout` / `stash`。

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-11` 收口) | 等式 |
|---|---|---|---|
| EditMode | `6523 / 6486 / 0 / 0 / 37` | `6523 / 6486 / 0 / 0 / 37` | `+0 = 0 新 − 0 删` ✅ |
| PlayMode | `294 / 290 / 0 / 2 / 2` | `290 / 286 / 0 / 2 / 2` | `294 − 290 = 4 = 4 新 − 0 删` ✅ |

`@ 2026-11-02 07:18–07:22 CST` · 批次 `S23-12` · commit `f96ed10a` + 工作树。
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形)。
⚠️ `case_opened_wiring_test` 是**判据翻转**(同一条数),不计入新增;新增 4 条全在 `casebook_entry_wiring_test`。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/b1-em-full.xml','unity/Logs/b1-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode 6523/6486/0/0/37 · PlayMode 294/290/0/2/2
```

---

## 六、新揭两条(都不是本债判据面内,但本债不能掩盖)

| 债 | 一句话 | `P0 面` |
|---|---|---|
| `TD-interaction-01` | 4 的确定性全序选择只落地第一键(`d∞`);病人未接成候选源 —— 就诊侧仍用**浮点视觉位置**自算最近者(违 ADR-016 §三) | `N`(R4 自陈) |
| `TD-diagnosis-01` | 查体由**世界 E** 代做,而 `S-8.4` 说它是**脉案模态内的行级动作**;`ICasebookCommandSink` 无行级查体成员 | `N`(R4 自陈) |

两处都写了「什么会把它翻成 `Y`」,不是「为了保住数字」。`未闭 P0 面`: `1 → 0`;M3 Exit 第二条合取
由**假**回**真**。

---

## 七、证明不了的事(禁借绿)

1. **「玩家真的按了键盘」未证** —— 本轮的 E 是 `InputSystem.QueueStateEvent` 注入的虚拟键;
   真实键位映射(`E` → `interact`)归输入动作资产,不在本件面内。夹具里有**注入探针**
   (`Assert.IsTrue(PlayerInputReader.InteractPressed())`),它把「注入失败」与「链断」分开。
2. **「屏看起来对」未证** —— 断的是 `CurrentModal == Casebook`(模态态),不是渲染。
   脉案的版面 / 可读性归 42 的走查,零证据。
3. **等距仲裁未证** —— `TD-interaction-01` 明写 `KindPriority` / `StableId` 两键缺失;
   本轮的比距在**完全等距**时判病人胜,而全序应按数据表。当前世界无此布局 ⇒ 无夹具。
4. **查体粒度退化为「整病人一次」** —— `TD-diagnosis-01`。世界 E 现在做三件事,玩家体验比设计**粗**
   (五手段不可分别使用),不是「少功能」但**不是设计形态**。
5. **暂停壳路径未重测** —— `ModalInputDriver` 的 toggle 分支加了脉案判断,存档位 → 暂停壳的
   「换屏」行为按原样保留,但没有新增夹具专门守它(既有 `pause_focus_navigation_test` 仍绿,
   但那是驱动级,不是「存档位按 Escape」这条)。
