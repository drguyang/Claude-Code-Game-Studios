# 结案原件 —— `TD-interaction-03`:其余 `InteractableKind` 无候选源 / 无消费面

> **对象**:`TD-interaction-03`(债册 `partial` · `P0 面 = N`)
> **批次**:2026-11-02 · 用户指令「继续」
> **本轮性质**:⚠️ **不是实现轮** —— 债的残余**今天 17:35 已被 `a39ea0b2` 消解**,
> 本轮做的是**取证 + 订正滞后记录 + 补两条未兑现的同步义务**。
> **截至时刻 / commit**:见 §六

---

## 一、本轮是怎么起来的(先说清楚,免得被读成「又做了一遍」)

`open` 队列里剩下的七条**全部**卡外部(VR / P1b / 45 / CI 矩阵 / 目标硬件 / P2),
逐条核过「触发条件」后没有一条今天能动。于是回头核 `partial` 队列,发现
`TD-interaction-03` 的记录**与代码不一致**:

| 记录怎么说(改前) | 代码实际 |
|---|---|
| `InteractionPressRouter.cs:21` 「`Utensil` → `_enterProcessing`(18;**P0 恒 null** —— BLOCKED-BY-42)」 | `BootRoot.cs:740-758` 的实参**非 null**:开 `ModalId.Processing` + 喂配方 / 准入 / 品级集 |
| `:46` 「`Utensil` → **消费面仍 null**…选中即落空」 | 同上 |
| `:52-54` 「`Utensil` 的**消费面**缺一个设计前置…路由面等 42」 | 同上 |
| `:241` `case Utensil` 注释「**P0 无生产消费面**(BLOCKED-BY-42)⇒ 落空」 | 同上 |
| 债册 `2026-11-02 · 「剩下三种」轮` 「`Utensil` 生产装配**恒 null**」 | 同上 |

**改写的时点**:`a39ea0b2`(2026-11-02 **17:35**,「炮制屏第九员 —— O-18-R6 落地 · `Utensil` 消费面接通」)
晚于该批记录的 12:03。⇒ **这不是代码没做,是记录没跟上。**

⚠️ 这正是本仓反复抓过的形态:**「注释里声称的状态」被当成了「代码的状态」的逆命题** ——
那次是「自述顶替门」(F6),这次是「自述顶替**现状**」。两者的共同点是**没人回去核一遍**。

---

## 二、消解条件逐条核(债的原文四条)

债的消解条件逐字:「`CandidateSources.BakedInitial` 与 `WorldBaked.PoiCells` / `BuildSlots`
有生产实装,**且** 每种至少有一个消费者接通(**且** 各自的选择/分派有夹具)」。

| # | 条件 | 判定 | 证据(可跑) |
|---|---|---|---|
| ① | 四个候选源有生产实装 | ✅ | `grep -rn "new GroundDropSource(\|new WorldBakedLogicalSource(\|new BakedInitialSource(" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ **3 命中** 全在 `CompositionRoot.cs`(`:1177` / `:1185` / `:1223`) |
| ② | 10 种 kind 各有生产消费面 | ✅ | 见下方 §三 表 |
| ③ | 每种的选择 / 分派有夹具 | ✅ | 见下方 §三 表「夹具」列 |
| ④ | `Utensil` 不再落空 | ✅ | `BootRoot.cs:746` `_uiDriver.OpenModal(ModalId.Processing)` |

---

## 三、10 种 `InteractableKind` 全覆盖(本轮实测)

| kind | 生产消费面 | 选择 / 分派夹具 |
|---|---|---|
| `Patient` | `BootRoot.cs:697` `patient => _interactionDriver?.Visit(patient)`(39) | `interaction_press_router_test.test_ir_03_patientCloser_kindIsPatient` |
| `ForageSpot` | `_services.HarvestDriver`(17 · `Gameplay.Boot/HarvestInteractionDriver.cs`) | `interaction_press_router_test.test_ir_02_forageCloser_kindIsForage_andVisitNotCalled` |
| `Drop` | `_services.DropClaimOrchestrator`(20) | `interaction_press_drop_test.test_dropInRange_routedToDrop_andClaimed` |
| `BuildSlot` | `BootRoot.cs:700` `_buildDriver.AimAt`(23) | `interaction_press_slot_poi_test.test_buildSlotOnCell_routesBuildSlot_andAimsThatCell` |
| `Door` | `BootRoot.cs:666` `StructureToggleDriver(SlotType.Door)`(23) | `interaction_press_door_container_test.test_doorOnCell_routesDoor_withStructureId` |
| `Switch` | `BootRoot.cs:674` `StructureToggleDriver(SlotType.Switch)` | `interaction_press_door_container_test.test_switchOnCell_routesSwitch_withStructureId` |
| `Container` | `BootRoot.cs:714` `OpenModal(ModalId.InventoryContainer)`(20 / 42) | `interaction_press_door_container_test.test_containerOnCell_routesContainer_withInstanceId` |
| `ClinicPanel` | `BootRoot.cs:731` `OpenModal(ModalId.ClinicPanel)`(24 / 42) | `interaction_press_door_container_test.test_clinicPanelOnCell_routesClinicPanel_withStructureId` |
| `Utensil` | `BootRoot.cs:740-758` `OpenModal(ModalId.Processing)` + 喂数据(18 / 42) | `interaction_press_door_container_test.test_utensil_withConsumer_routesUtensil` |
| `PoiCell` | 不分派:`Select` 内已发发现请求(6 `PoiStateMachine.TryDiscover`) | `interaction_press_slot_poi_test.test_poiOnCell_routesPoiCell_andEmitsOneDiscoveryRequest` |

**复算**(一条命令出全表所需的三个「源」):

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -rn "new GroundDropSource(\|new WorldBakedLogicalSource(\|new BakedInitialSource(" unity/Assets --include=*.cs | grep -v /Tests/
# 期望 3 命中,全在 CompositionRoot.cs(:1177 / :1185 / :1223)
grep -c "public void test_" unity/Assets/Tests/EditMode/Interaction/interaction_press_door_container_test.cs   # 期望 9
grep -n "OpenModal(UI.Skeuomorphic.ModalId.Processing)" unity/Assets/Gameplay.Boot/BootRoot.cs                 # 期望 1 命中(:746)
```

---

## 四、补的两条未兑现同步义务(`ADR-013 §十-E Amendment D` 的 ①–⑥)

ADR 自己列了六条同步义务。逐条核本轮之前的实况:

| # | 义务 | 改前 | 本轮 |
|---|---|---|---|
| ① | `ModalId` 增 `Processing = 9` | ✅ 已落 | — |
| ② | `ScreenFactories` / `ModalScreenNames` 增项 | ✅ 已落(`SkeuoRuntimeDriver.cs:143` / `:159`) | — |
| ③ | 闭集门改「10 值 / 9 员」 | ✅ 已落(`dto_boundary_and_modal_test.cs:104` 断言 `(int)ModalId.Processing == 9`) | — |
| ④ | **`skeuomorphic-ui.md` 的 `AC-42-F1` 闭集八 → 九** | ❌ **未落** —— 全文件 **零** `Processing` 命中 | ✅ 已补 |
| ⑤ | **`technical-preferences.md` 的 ADR-013 日志条目追加本 Amendment** | ❌ **未落** —— `grep "十-E\|Amendment D"` **零命中** | ✅ 已补 |
| ⑥ | `modal_gate_test.ClosedSetMembers` 补员 | ✅ 已落(`:411`) | — |

**复算**:

```bash
grep -c "Processing" design/gdd/skeuomorphic-ui.md        # 改前 0 → 改后 > 0
grep -c "十-E" .claude/docs/technical-preferences.md      # 改前 0 → 改后 > 0
```

⚠️ **④ 的补法不是「把八改成九」一句话**:`skeuomorphic-ui.md` 里 `AC-42-F1` 的闭集点名、
`ModalId` 那一节的「成员集」、以及 2026-09-21 遗留的「**本闭集当前恰七员**」三处**互相矛盾**
(`:399` 那句在两轮增员后从未订正)。三处一并处理:**原文不删 + 🔁 注**,不静默覆盖。

---

## 五、同批订正的三处 GDD 口径(免责:不改任何 AC 判定)

| 处 | 原文 | 现口径 |
|---|---|---|
| `processing.md` `AC-18-10b` | 级别 = **EXTERNAL · BLOCKED-BY-42** | **`NOT-RUN`** —— 前置已解除(42 认领 + 屏只建 `IsAdmissible == true` 的行 ⇒「不出现(非灰置)」**结构上**成立),但 ① 无 `design/ux/processing-18.md` ② 手柄走查从未执行 ⇒ **仍不得记绿** |
| `processing.md` `:403` / `:664` | 「接缝全案未登记」/「选料复用 20 容器屏」 | `O-18-R6` 已回写;「选料复用 20 容器屏」**未采纳**(`ADR-013 §十-E` 裁定增员,理由 = 两屏数据面与焦点序全不同) |
| `skeuomorphic-ui.md` `:1269` | 「42 屏清单**未认领**炮制交互」 | 已认领;`AC-42-F1` ⑨ 员记 `NOT-RUN`(无 spec ⇒ 阻塞签核) |

⚠️ **`AC-18-10b` 从 `EXTERNAL` 改 `NOT-RUN` 不是「转绿」** —— 两者的共同点是**都没通过**;
差别只在**谁在挡**:前者说「载体不存在」,后者说「载体在,但判据没跑」。把后者写成前者
是**低估自己的进度**,把前者写成 ✅ 是**借绿**。二者都不可接受。

---

## 六、回归

**截至时刻 · 批次 · commit**:2026-11-02 · `S23-29` · 工作树(基线 = `384f5d29`)。

| 平台 | 读数(total / passed / failed / inconclusive / skipped) | 时刻 | 基线(本批开工前) | 等式 |
|---|---|---|---|---|
| EditMode | 见 §六.1 | — | `6624 / 6587 / 0 / 0 / 37` | 期望 `+0` |
| PlayMode | 见 §六.1 | — | `311 / 307 / 0 / 2 / 2` | 期望 `+0` |

### §六.1 实跑读数

| 平台 | 读数(total / passed / failed / inconclusive / skipped) | 时刻 | 基线 | 等式 |
|---|---|---|---|---|
| EditMode | `6624 / 6587 / 0 / 0 / 37` | 21:19:43 | `6624 / 6587 / 0 / 0 / 37` | **`+0`** |
| PlayMode | `311 / 307 / 0 / 2 / 2` | 21:20:28 | `311 / 307 / 0 / 2 / 2` | **`+0`** |

本仓 PlayMode 常态退出码 = 2;判据取 XML 的 `failed` 计数 ⇒ `failed = 0`。

**与 §七 第 4 条吻合**:本轮除注释外零生产行为改动 ⇒ 读数与基线**逐位相同**。
⚠️ 这条**不是借绿**:它是本轮的**预期结论**,且若不同就说明工作树里混进了别的改动 ——
等式 `total − 基线 total = 0` 正是在验它。

等式复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s2329-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s2329-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
```

---

## 七、证明不了的事(禁借绿)

1. **「真开档、真按 E、真开到炮制屏」没有自动化判据** —— 上表每一格都是
   「**生产装配点存在**」+「**分派有夹具**」两件,合起来**不等于**端到端。
   `BootRoot` 的整段装配与其它 `OnTickEdge` 同状态:改坏它,EditMode 全绿。
   要它需要 PlayMode 或手动走查(`AC-17-12` 同族,均 `NOT-RUN`)。
2. **`Utensil` 的屏是「最小可操作壳」** —— `ADR-013 §十-E` 自陈:无 `design/ux/processing-18.md`,
   排版 / 文案 / 焦点序**均未经 `/ux-review`**。⇒ 「消费面接通」只等于「有一条路走到了屏」,
   **不等于**「炮制面板做完了」。
3. **`Switch` 今日无下游读者** —— 状态进流、可路由,但没有电路 / 灯具消费它。
   「接通」在此处 = 「不落空」,不是「有效果」。
4. **本轮未改任何生产行为** —— 除注释外零代码改动。⇒ 回归读数**必然**与基线相同;
   若不同,说明有别的改动混进了工作树,须先查清。
