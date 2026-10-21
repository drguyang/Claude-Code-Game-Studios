# 丙档 —— 阻塞登记 + 三处新发现(零代码改动)

**日期**: 2026-10-20
**用户裁定**(AskUserQuestion 两轮,三项):
1. **丙档 5 项处置** = 「**先裁「结案触发」再谈接线**」⇒ 本轮**只登记**,不动代码;
2. **F-1 处置** = 「**登记 + 请 20 侧补登记**」⇒ 落 `inventory-and-items.md` 一条 OQ 行(唯一一处 design 文档改动);
3. **结案触发裁定** = 「**归 39 UX 轮先设计形态**」⇒ 53 链继续阻塞,不造 P0 载体。

**前置**: `370345c`(甲档 2 份换源)· `001de75`(乙档 `disease_action_axis`)· `7d563a2`(甲档剩余 `MAX_DOSE_DETENTS`)· `24ac2da`(乙档剩余 4 项全登记)

---

## §0 本轮范围:丙档是哪几项

账本 `更新④` 的三档口径 —— **甲** = 有真读点(硬编码换源)· **乙** = 载体在、缺调用方 ·
**丙** = **整条消费链不存在**。丙档共 **5 项**,两种口径:

| # | 项 | 所属产物 | 口径 |
|---|---|---|---|
| C-1 | `suppresses_motor` | `interaction_kinds` | 字段级 |
| C-2 | `duration_owner` + `duration_owner_system_id` | `interaction_kinds` | 字段级 |
| C-3 | `intent_uplink` | `interaction_kinds` | 字段级 |
| C-4 | `53_outcome_table` | 53 产物 | 数据集级 |
| C-5 | `53_delay_table` | 53 产物 | 数据集级 |

**结论:5 项全部不可接。** 但**两类根因不同**,且**比乙档更硬** —— 乙档缺的是「调用方」,
丙档缺的是「**整条链 + 前置裁定**」。

---

## §1 字段级三项:读者面有两种形态(不可混谈)

### 1.1 C-1 / C-2 —— 有**一个烘焙期读者**,运行期**零**读者

命令(全文引用,`grep -v "/Tests/"`):

```
$ grep -rn "SuppressesMotor\|suppresses_motor" unity/Assets --include=*.cs | grep -v "/Tests/"
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:28    ← 行模型 doc
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:46    ← 行模型字段
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:91    ← 行模型 ctor
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:115   ← DurationOwnerKind doc
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:120   ← DurationOwnerKind.None doc
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:313   ← 4-DC-6 doc
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:317   ← 4-DC-6 doc
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:328   ← 4-DC-6 判据体(唯一真读点)
unity/Assets/Gameplay.Presentation/Interaction/InteractionKindTable.cs:331   ← 4-DC-6 抛
unity/Assets/Editor.Tools.Bake/InteractionKindCookedWriter.cs:58             ← 烘焙期写
unity/Assets/Editor.Tools.Bake/InteractionKindBinder.cs:31                   ← 烘焙期白名单
unity/Assets/Editor.Tools.Bake/InteractionKindBinder.cs:184                  ← 烘焙期读 JSON
```

⇒ 命中**全部**落在 ① 行模型 ② 三个烘焙期件 ③ **4-DC-6 校验器**。`DurationOwner` / `DurationOwnerSystemId`
同形(与 `SuppressesMotor` 同段、同一判据体内)。
**运行期消费者 = 0** —— 而 4-DC-6 只在**烘焙期**跑(`InteractionKindBinder.cs:128`,与 `更新⑦ §三` 同一条证据,本轮复测未变)。

### 1.2 运动压制的**消费面整体不存在**(可复算)

```
$ for s in IMotorGate IMotorSuppression SuppressMotor ArmedState IArmedState; do
    printf "%-18s %s\n" "$s" "$(grep -rn "\b$s\b" unity/Assets --include=*.cs | grep -v '/Tests/' | wc -l)"; done
IMotorGate         0
IMotorSuppression  0
SuppressMotor      0
ArmedState         0
IArmedState        10
```

⚠️ `IArmedState` 的 10 处**不等于**消费面存在:它的生产实现 = `EmergencyChannelDriver`
(`Gameplay.Boot/EmergencyChannelDriver.cs:48`),那是 **10 的模态门**(急救动作进行中),
与 **4 的持续交互**(搜箱 / 采集抑制移动)**不是同一回事**。⇒ 不得据这 10 处判「运动压制已接」。

### 1.3 C-3 `intent_uplink` —— **连烘焙期读者都没有**

```
$ grep -rn "\bIntentUplink\b\|intent_uplink" unity/Assets --include=*.cs | grep -v "/Tests/"
（10 处）行模型 :76/:85/:97/:106 · 枚举定义 :128 · 解码 :64 · 编码 writer :64 · binder 白名单 :31 · binder 读 :188-189
$ grep -c "intent_uplink\|IntentUplink" design/gdd/interaction-system.md
1        ← 唯一命中 = 字段表那一行(:1006);4-DC 判据表(:1023-1028)六条**无一条**涉及它
```

⇒ `intent_uplink` 被**声明**了,但**没有任何判据要求读它**。它的消费者是 `ADR-001 §一之三` 的
通道分流(判定输入类走可靠通道),归 **45 的 P1b 轮**。
**本轮不自行发明判据**(发明一条 4-DC-7 = 越权改 GDD)。

---

## §2 数据集级两项:链头断在**一个尚不存在的玩家 UI 动作**上

### 2.1 53 链关键符号的生产构造点(**全部为 0**)

```
$ grep -rn "new <符号>\b" unity/Assets --include=*.cs | grep -v "/Tests/"
CaseCloseWriter            new: 0   (7 处引用:6 在自身/注释,1 在 WriterExistenceGate 注释)
ConsequenceEmitter         new: 0   (4 处引用:全在自身 + 1 注释)
ConsequenceInputAggregator new: 0   (2 处引用:自身 + IPayloadDecoder.cs 注释)
TrialHistoryPredicate      new: 0   (1 处引用:自身)
$ grep -rn "new JudgeInputs(" unity/Assets --include=*.cs        ⇒ 0(全库,**连 Tests 都没有**)
$ grep -rn "IDiseaseProjection" unity/Assets --include=*.cs      ⇒ 生产实现 0(3 处全是 Tests 的 StubProjection)
```

⇒ `Judge.Evaluate`(C-4 的载体)与 `ConsequenceEmitter`(C-5 的载体)**都在,但整条上游不在**。

### 2.2 链头的触发点,**其文件头自己就登记了未接线**

> `unity/Assets/Sim/CaseCloseWriter.cs:38-39` 逐字:
> 「生产触发点(结案 UI「已处置」勾选 + 确认 → 本写者)**尚未接线**:归 37/42 的交互装配轮。」

### 2.3 而那个 UI 动作在**呈现层也不存在**

```
$ grep -c "^\s*void Submit" unity/Assets/Gameplay.UI/Skeuomorphic/CasebookBinding.cs
6        ← SubmitDiseaseName / SubmitConfidence / SubmitDose / SubmitPageTurn / SubmitFold / SubmitEmergencySkip
```
`ICasebookCommandSink`(`CasebookBinding.cs:112-168`)的 **6 个命令里没有结案命令**。

### 2.4 上游另外两块也不在

| 缺件 | 证据 | 归属 |
|---|---|---|
| `IDiseaseProjection` 生产实现 | 全库 0(3 处全在 Tests) | 9 的轮(测试件自陈「归 9 轮,登记项」) |
| `eligible` 现算器(= 把 `Inputs53` → `JudgeInputs` → `Judge.Evaluate` → `(CaseClosed, Outcome)` 逐 tick 喂给 `Emit`) | 不存在 | 53 的装配层(`ConsequenceEmitter.cs:130` 明写「由装配层每 tick 现算现喂」) |

---

## §3 为什么**不能**照抄 `CaseOpenedDriver` 那个 P0 接线载体先例

`unity/Assets/Gameplay.Boot/CaseOpenedDriver.cs` 是本仓**已成立的**「补调用方」先例
(`sprint-05 T1.2`):它在 tick 边沿遍历在场病人自动开案,自陈「真实开案触发点 = 就诊交互」。

**它能成立的前提**:开案有一个**不依赖 UI 的近似触发**(病人在场)。
**结案没有这种近似** —— 前置(37 `case-system.md` 规则八)=
`可结案(c) := c.state = 开 ∧ 已处置(c) ∧ **玩家已勾选「已处置」**`,
其中「已处置」是**玩家在结案页的显式动作**(UI 仪式,不产生独立事件)。
⇒ 造一个自动结案载体 = **每 tick 自动结案** ⇒ 后果事件自动涌出 + **覆盖规则八/`AC-37-04`(BLOCKING)**
= **改玩法语义**,不是接线。**不由本轮代裁**(见 §4.3 与 §5)。

---

## §4 附带发现(四条,全部**新**;不在原普查面内)

### 4.1 F-1:`Container` 行的 4-DC-6 **绿着**,而 GDD 期望它**红**

| 面 | 内容 |
|---|---|
| **GDD 期望** | `design/gdd/interaction-system.md:1247` 逐字:「`4-DC-6` 会拒收「suppress 但 `DurationOwner = None`」的项 ⇒ `Container` 未登记即**构建期红**(合法待办,非缺陷)」;`:1036` 同款(「契约已写、判据待前置」) |
| **实际数据** | `assets/data/interaction_kinds.json` kind 4 行:`"suppresses_motor": true` · `"duration_owner": "RoutedSystem"` · `"duration_owner_system_id": 20` |
| **判据结果** | 4-DC-6 的判据体(`InteractionKindTable.cs:335`)只查 `RegisteredSystems.Contains(20)`;`RoutedSystems.Registered`(`RoutedSystems.cs:32`)含 `20` ⇒ **通过** ⇒ **烘焙绿** |
| **20 侧登记** | `grep -n "搜箱\|持续交互\|SuppressesMotor\|suppresses_motor\|duration_owner\|4-DC\|OQ-4-4" design/gdd/inventory-and-items.md` ⇒ **零命中** |
| **值的存在时长** | `git log --oneline -- assets/data/interaction_kinds.json` ⇒ **唯一一次提交 `7eacc12`(2026-10-04)** ⇒ 该值自种子落地起就在 |

⇒ 丙档字段 `duration_owner` 的**唯一读者**,正靠一个 **20 侧无出处**的值通过。
**两种读法**:(a) 种子填了一个未经 20 登记的填值 ⇒ GDD 的「合法待办」被判据静默放过;
(b) 判定已在别处作出而 GDD 未回刷。**本轮不裁**(用户裁定 = 登记 + 请 20 侧补登记,落 §6 R-2)。

### 4.2 F-2:GDD 的 `StableIdSource` 枚举名清单与现行实现不一致

| 处 | 值 |
|---|---|
| GDD `interaction-system.md:1006` | `InstanceSeq` / `PatientSeq` / `PoiId` / `ResourcePointIndex` / `StructureSeq` / `SlotLinearKey` |
| C# 枚举 `Gameplay.Presentation/Interaction/Candidate.cs:22-41` | `InstanceId` / `BakedResourceIndex` / `PatientId` / `PoiId` / `SlotLinearKey` / `StructureId` |
| 种子 JSON `stable_id_source` 实值 | `PatientId` / `PoiId` / `StructureId` / `InstanceId` / `BakedResourceIndex` / `SlotLinearKey` |

⇒ **6 项里 4 项名不同**(`InstanceSeq`↔`InstanceId` · `PatientSeq`↔`PatientId` ·
`ResourcePointIndex`↔`BakedResourceIndex` · `StructureSeq`↔`StructureId`)。
属 `stable_id_source`(乙档,**已登记**)的**文档漂移**,与丙档无关。
**哪边是权威名**(改 GDD 文字 vs 改枚举与产物)**本轮不裁、也不改**。

### 4.3 F-3(本轮最硬的一处):**39 的结案形态规格不存在,且 GDD 点名的形状被 39 自己的禁令挡住**

| 件 | 逐字 |
|---|---|
| 39 GDD `casebook.md:141` | 「结案页「已处置」**勾选**」= **UI 仪式,不产生独立事件**;「**37 定语义;39 定形态**」 |
| 39 GDD `casebook.md:26` | 「玩家在这里做的唯一事情是落笔与**结案**」 |
| 39 UX spec `casebook-39.md` §7(9 个动作) | 开册 · 关册 · 移焦 · 查体 · 病名落笔 · 改判 · 置信度档位 · 施治 · 翻页折页 ⇒ **零结案行** |
| 39 UX spec `casebook-39.md` §5.3 | 「**零现代 UI 元件**:全屏无列表控件 / 下拉 / **复选框** / 徽章 / 角标 / 进度条」 |
| 39 GDD `casebook.md:292` | 「结案**只有一条路径**:在某一例上执行「结案」」 |
| 39 GDD `casebook.md:401` | Impact Moment ② = 「**结案合上纸**的那一下」 |

⇒ **39 GDD 判给 39 定形态的那个「勾选」,被 39 自己的规格禁令(禁复选框)挡住**,而 §7 动作表里
连一条结案行都没有。**结案触发不是「照规格实现」,是「规格本身空缺」** —— 这正是链头 37/42/39
三侧都没有它的原因。形态候选其实写在别处(`:401` 的「结案合上纸」),只是没进 §7 动作表。

### 4.4 F-4:42 的结案命令面为零(与 F-3 同源,单列以便对账)

见 §2.3:`ICasebookCommandSink` 6 个命令,**零结案命令**。

---

## §5 用户裁定(三项,AskUserQuestion)

| # | 问 | 裁定 | 效力 |
|---|---|---|---|
| 1 | 丙档 5 项如何处置? | 「**先裁「结案触发」再谈接线**」 | 本轮**只登记**,零代码;结案触发先裁 |
| 2 | F-1 如何处置? | 「**登记 + 请 20 侧补登记**」 | 落 `inventory-and-items.md` 一条 OQ 行(唯一 design 文档改动) |
| 3 | 结案触发怎么裁?(承 1) | 「**归 39 UX 轮先设计形态**」 | 53 链继续阻塞;**不造 P0 自动结案载体**;F-3 的规格缺口登记在案 |

---

## §6 本轮做了什么

| # | 内容 | 性质 |
|---|---|---|
| — | **零代码改动**(无 `.cs` / `.json` / `.cooked` 变更) | 用户裁定 |
| R-1 | 丙档 5 项的**可复算阻塞证据**(§1 / §2) | 登记 |
| R-2 | F-1 → `design/gdd/inventory-and-items.md` **新增 `OQ-20-11` 行**(20 侧欠账:搜箱是否持续交互 + 时长归属) | design 文档一处 |
| R-2b | F-3 → `design/ux/casebook-39.md` **新增 `OQ-CB-7` 行**(39 侧欠账:结案的交互形态未成文;含三个形态候选,不选、不预设) | design 文档一处 |
| R-6 | **两处上游文档回刷**(用户「接着回刷那两处文档」)<br>① `design/gdd/casebook.md` —— 3 处加注:`:141` 表行后(主注)+ `:310`(`Edge Cases` 行内)+ `:444`(`UI-39.2` 表后),均指向 `OQ-CB-7`<br>② `design/gdd/interaction-system.md` —— 3 处订正:`:1028`(`4-DC-6` 判据行内)+ `:1247`(`OQ-4-4` 行右列)+ `:1036` 段后(**订正块**,含实测四项) | design 文档六处,**均不改任何判据 / 数据** |
| R-3 | F-2 / F-3 / F-4 三条发现登记(§4) | 登记 |
| R-4 | 「为什么不能照抄 `CaseOpenedDriver`」的**先例边界辨析**(§3) | 登记 |
| R-5 | 三项用户裁定入档(§5) | 登记 |

本件**不产生**新测试、不改产品码、不重烘 ⇒ **无回归可跑**(见 §7)。

---

## §7 回归

**无回归可跑** —— 本件零代码 / 零数据 / 零产物改动(唯一的 `.md` 改动是 design 文档登记行)。

末次全量 EditMode = `7d563a2` 轮的 `total=4012 passed=3976 failed=0 inconclusive=0 skipped=36`
(截至 `2026-10-20 13:25 CST` · 批次 `sprint-15 甲档剩余项接线轮` · commit `001de75`)。
⚠️ **该数不得被引用为「本轮的回归结果」** —— 本件没有产生任何可回归的改动。
引用它仅表示「丙档登记前的最后已知基线」,不构成对本轮的证明。

---

## §8 完成度(Landed / Proven)

| # | 内容 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| R-1 | 丙档 5 项可复算证据 | ✅ | ✅ | §1.1 / §1.2 / §1.3 / §2.1 的命令及输出(可重跑) |
| R-2 | F-1 的 20 侧登记行 | ✅ | ✅ | `design/gdd/inventory-and-items.md` §Open Questions `OQ-20-11` 行在盘;判据 = `grep -n "OQ-20-11" design/gdd/inventory-and-items.md` 命中 |
| R-2b | F-3 的 39 侧登记行 | ✅ | ✅ | `design/ux/casebook-39.md` §14 `OQ-CB-7` 行在盘;判据 = `grep -n "OQ-CB-7" design/ux/casebook-39.md` 命中,且该行 5 个 cell 与表头同构(§11 校验命令) |
| R-6 | 两处上游文档回刷(39 GDD ×3 处 · 4 GDD ×3 处) | ✅ | ✅ | `grep -c "2026-10-20" design/gdd/casebook.md` = 4 · `grep -n "OQ-20-11" design/gdd/interaction-system.md` 命中;两张表列数与表头同构(§11 已跑) |
| R-3 | F-2 / F-3 / F-4 三条发现 | ✅ | ✅ | §4.2 / §4.3 / §4.4 逐条带命令或 `file:line` |
| R-4 | `CaseOpenedDriver` 先例边界辨析 | ✅ | ⚠️ **推理,非实测** | 由 37 规则八 + `AC-37-04` 的文本推出;本轮**未**构造自动结案实验去实测其后果 —— 如实标注 |
| R-5 | 三项裁定入档 | ✅ | ✅ | §5 表 + 本轮 AskUserQuestion 记录 |
| — | 代码 / 数据 / 产物改动 | **无** | — | 用户裁定 |

**R-4 如实标注的理由**:「自动结案会覆盖规则八 + 截断处置集」是从 GDD 规则与
`ConsequenceInputAggregator` 的窗口语义(`Aggregate` 只取 `Tick ≤ close.Tick`,见
`ConsequenceInputAggregator.cs:248`)推出的**逻辑结论**,本轮**没有**跑数值 / 集成实验。
不写「已实测」。

---

## §9 未结事项

1. **C-1 / C-2** —— 归 **4 的运行期装配轮**(载体齐备,装配链不存在)。
2. **C-3** —— 归 **45 的 P1b 轮**(上行通道);且 **GDD 侧无对应判据** ⇒ 若将来要立判据,须另裁。
3. **C-4 / C-5** —— 归 **39 的 UX 轮(F-3 规格缺口)+ 37/42 的交互装配轮 + 9 的
   `IDiseaseProjection` + 53 的 `eligible` 装配器**(五处,见 §2.4)。
4. **F-1 的 20 侧回填** —— 已落 `OQ-20-11`,归 20 的下一轮(与 `OQ-4-4` 的 20 半边同批)。
   ✅ **4 的 GDD 措辞已于本轮回刷**(R-6 ②)—— `:1028` / `:1247` / `:1036` 三处均加订正,
   明写「原『Container 未登记即构建期红』的预言**已被实测证伪**」,并把「门被无出处填值静默绕开」
   这一**更严重**的失效模式写清。**判据与数据均未改。**
5. **F-2 的文档漂移** —— 归 `stable_id_source` 的回刷轮;**本轮不裁哪边权威**。
6. **F-3 的 39 侧规格缺口** —— ✅ **已于同一轮落盘**:`design/ux/casebook-39.md` §14 新增 `OQ-CB-7`
   (用户「按建议继续」授权)。该行**只登记开放项,不改本 spec 任何既有判据**,并明写补形态时
   须同时回刷 §7 动作表 + §8 Events Fired。三个形态候选(页缘/折边 Submit · 合上纸 · 独立结案页)
   **只列不选** —— 且 §5.3 的「禁复选框」与 GDD 字面「勾选」是否等同,**该判定 39 从未作过**,
   本行不预设。
   ⚠️ **仍未做**:39 GDD 侧已于本轮回刷(R-6 ①)—— `:141` / `:310` / `:444` 三处加注并指向 `OQ-CB-7`。
   剩余缺口 = `OQ-CB-7` 本身的**形态裁定**(归 39 UX 轮);GDD 侧已不再有「未指向」的残留。
7. **ADR-012 IL2CPP 三格矩阵** —— NOT-RUN(本机仅 Mono),与本轮无关,照旧挂账。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。

---

## §10 落账

| 件 | 状态 |
|---|---|
| 本登记件 | ✅ |
| `design/gdd/inventory-and-items.md`(R-2:`OQ-20-11` 行) | ✅ |
| `design/ux/casebook-39.md`(R-2b:`OQ-CB-7` 行) | ✅ |
| `tests/integration/item_database/README.md`(账本) | ✅ §2026-10-20 更新⑧ |
| `production/session-state/active.md`(Session Extract) | ✅ 第七段 |
| 代码 / 数据 / 产物 | **零改动**(用户裁定) |
| commit | **未做**(不经用户指令不 commit) |

---

## §11 可复算校验(两处登记行的判据)

两行都是 Markdown 表格行,判据必须能机械核,不能只写「已落」。

```bash
# R-2:OQ-20-11 在盘,且与表头同构(5 cell)
grep -c "OQ-20-11" design/gdd/inventory-and-items.md            # 期望 ≥1
python3 -c "
import io
ls=io.open('design/gdd/inventory-and-items.md',encoding='utf-8').read().split(chr(10))
r=[l for l in ls if l.startswith('| **OQ-20-11**')][0]
print('cells =', len(r.strip().strip('|').split('|')))"          # 期望 cells = 5

# R-2b:OQ-CB-7 在盘,行序在 OQ-CB-6 之后,且 5 cell
grep -c "OQ-CB-7" design/ux/casebook-39.md                       # 期望 ≥1
python3 -c "
import io
ls=io.open('design/ux/casebook-39.md',encoding='utf-8').read().split(chr(10))
i7=[i for i,l in enumerate(ls) if l.startswith('| **OQ-CB-7**')][0]
i6=[i for i,l in enumerate(ls) if l.startswith('| ~~**OQ-CB-6**~~')][0]
print('order ok =', i7 > i6)
print('cells =', len(ls[i7].strip().strip('|').split('|')))"    # 期望 order ok = True · cells = 5

# 两行均不得把转义管符 / 四连星带进表格
grep -c '\\|' design/ux/casebook-39.md                           # 期望 0
```

**R-6(两处上游文档回刷)的判据**:

```bash
# ① 39 GDD:三处加注,且均指向 OQ-CB-7
grep -c "OQ-CB-7" design/gdd/casebook.md                          # 期望 ≥3
grep -n "⭑ 2026-10-20" design/gdd/casebook.md                     # 期望 3 行(141 后 / 310 行内 / 444 后)

# ② 4 GDD:三处订正,且均指向 Container 的实测
grep -c "2026-10-20" design/gdd/interaction-system.md             # 期望 ≥4(1028 / 1036 块 2 处 / 1247)
grep -c "OQ-20-11" design/gdd/interaction-system.md               # 期望 ≥2
grep -n "已被实测证伪" design/gdd/interaction-system.md            # 期望 ≥2

# ③ 两张被改的表列数不变(表头同构)
python3 -c "
import io
ls=io.open('design/gdd/casebook.md',encoding='utf-8').read().split(chr(10))
r=[l for l in ls if l.startswith('| **结案页「已处置」勾选**')][0]
print('casebook 141 cells =', len(r.strip().strip('|').split('|')))"   # 期望 3
awk 'NR>=1023 && NR<=1028 {n=gsub(/\|/,"|"); if(n!=4) print NR": BAD "n}' design/gdd/interaction-system.md
                                                                  # 期望无输出(6 行判据表均 4 pipe)
```

**⚠️ 一处必须如实说清**:上述判据证明的是「**登记行在盘且格式合法**」,
**不证明**「20 / 39 已就这两个问题作出裁定」—— 两行都是**开放项**,不是结论。
`R-2` / `R-2b` 的 Proven 只覆盖「行真的落上去了」这一件事。
