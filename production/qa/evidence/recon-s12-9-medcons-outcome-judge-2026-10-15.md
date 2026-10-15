# 勘察报告 —— S12-9:53 story-002 `Outcome = Judge(P,C,D,U)` 依赖面

- **对象**:`production/epics/medical-consequences/story-002-deterministic-outcome-judge.md`(Status: `Ready`)
- **日期**:2026-10-15 · **Sprint**:12 · **执行者**:Claude
- **性质**:本件 = **依赖面勘察**(承卡面 **S12-9-AC-a**「动手前先读,再裁」),**不是评审、不是实现**
- **勘察任务**(逐字承 `sprint-12.md` S12-9 验收条件):「先做**依赖面勘察**再动手,**若链比预期深即退回单独立项(不硬吃)**」
- **收缩判据**(逐字承卡面 `story-002:112`):「若『需补 / 不存在』**≥ 2 路** ⇒ **退回单独立项**,不在 S12-9 硬吃」

---

## 一、勘察判决

**触发收缩判据:四入向 P / C / D / U 全部为「需补」或「不存在」(4/4 ≥ 2)。**

**⇒ S12-9 应退回单独立项。** 且根因**不是四笔独立欠账,而是同一条结构性缺口** ——
`Sim` 物理上够不着载荷内容(§三),该缺口须**ADR 级裁决**方能补,不在 story 自决面内。

**若硬吃**,唯一可能的产物是「只跑通形状、判定永远走 default 行」的假实现 ——
与 **S12-4 新规**要治的「假绿夹具」同型(该新规正是本 sprint 的 Must Have)。

---

## 二、四入向逐项标注(卡面 S12-9-AC-a 要求的「已可用 / 需补 / 不存在」三类)

| 入向 | 需要的**内容** | 既有可复用面 | 判定 |
|---|---|---|---|
| **P** | `JudgmentRecordedPayload.LexiconId`(病名)+ `.Confidence` | 聚合器交出 `SimEvent` **头部**;内容住 `PayloadRef` | **需补** |
| **C** | 处置事件内容(`TreatmentId` / `polarity`) | 同上;且「哪些 Kind 算处置」**四处口径不一致**(§五) | **需补**(且**待裁**) |
| **D** | `treatable_by(d)` **命中位** | `IDiseaseProjection` 仅测试桩;`DiseaseProjectionSnapshot` **只有 `Tick` 一个字段** | **不存在** |
| **U** | `SplitMix64(WorldSeed,"case-salt",salted_key)` | 原语在 `Sim.Contracts` ✅ **可达**;但 `salted_key` 住 `PatternRecognizedPayload` ⇒ 需解码 | **需补** |

**计数:「需补」3 + 「不存在」1 = 4;「已可用」0 ⇒ 4 ≥ 2 ⇒ 触发收缩。**

> ⚠️ **U 的一条精确区分**:`SplitMix64` **原语本身已可用**(住 `Sim.Contracts/SplitMix64.cs`,
> `HashTagged` 形已实现,`PatternDetector.cs:406` 已在用同一盐契约)——
> **但 U 的输入 `salted_key` 是载荷内容**,须先从 `PatternRecognizedPayload` 解出。
> 故 U 判「需补」而非「已可用」。

---

## 三、根因:一条结构性缺口(非四笔独立欠账)

### 3.1 缺口的形状

```
unity/Assets/Sim/Sim.asmdef:4      "references": [ "Sim.Contracts" ]
unity/Assets/Sim/Sim.asmdef        "noEngineReferences": true
```

⇒ `Sim` 的引用集**恰 = {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门强制)。
而 **`PayloadCodec` 住 `Sim.Codec`** ⇒ `Sim` **物理上够不着任何载荷内容**;
`PayloadRef` 只有 `BlobId / Offset / Length` 三个字段,**不含字节**。

**story-001 已经绕开过这堵墙,且自陈在案** ——
`Sim/Consequences/ConsequenceInputAggregator.cs:11-13`:

> 「⚠️ **零载荷解码(门 A 的结构性后果)**:`Sim` 的引用集恰 = {BCL, `Sim.Contracts`}
> ⇒ 够不着 `Sim.Codec` ⇒ **本件只操作 `SimEvent` 头部**(Tick / Patient / Seq / Kind),
> **从不读 `Payload` 内容**。P / C 的**内容**(病名 / 置信度 / 处置适切性)归 story-002。」

**但 `Judge` 按定义就要读 P / C 的「内容」**(GDD `:263` 真值源登记:
「`P` = 判断记录(**病名 / 置信度**)· `C` = **处置适切性**」)——
⇒ story-001 把问题**原样交给了 story-002**,而 story-002 面对的是同一堵墙。

### 3.2 这是 ADR-029 的**对称缺口**(写侧已补,读侧未补)

| 侧 | 抽象点 | 状态 | 出处 |
|---|---|---|---|
| **写** | `IPayloadEncoder` | ✅ 已立(第七个 P0 抽象点) | ADR-029 §① · `Sim.Contracts/IPayloadEncoder.cs` |
| **读** | *(无)* | ❌ **不存在** | 全库 grep `interface IPayloadDecoder\|IPayloadReader\|IPayloadCodec` = **0 命中** |

ADR-029 的**存在理由逐字**是:「`Sim` 的写者要产出 `PayloadRef`,却够不着编码面 …
**本接口是 `Sim` 侧唯一合法的编码路径**」——
**读侧有完全同构的问题,却从未被裁决。**

**同型先例(同一堵墙,已被绕开两次,均留下「解码归调用方」的注)**:

- `Sim/CaseCloseDecider.cs:177-181`:
  > 「⚠️ **为何不解码载荷**:`CaseClosedPayload.CaseId` 是权威关联字段,但 `PayloadCodec` 住
  > `Sim.Codec`,而本类住 `Sim` —— 引用集恰 = {BCL, `Sim.Contracts`} …
  > **物理上够不着**(同 `PoiStateMachine` 的处置:**解码归调用方**)。故本处取流序关联。」
- `Sim/Consequences/ConsequenceInputAggregator.cs:11-13`(上引)

⇒ **两处都是「绕开」而非「解决」**;story-002 是**第三个**撞上这堵墙的,而它绕不开 ——
`Judge` 的语义就建立在载荷内容上。

### 3.3 两条候选解法(**均为 ADR 级**,不在 story 自决面内)

| | 甲案:新增读侧抽象点 | 乙案:装配层解码 + DTO 入 `Sim` |
|---|---|---|
| 形状 | 新 `IPayloadDecoder` 住 `Sim.Contracts`,实现住 `Sim.Codec` | 装配层(`Gameplay.Boot`)解码后,把内容以 DTO 喂进 `Sim` |
| 与 ADR-029 关系 | **对称** —— 补齐写/读一对 | 沿用既有「解码归调用方」口径 |
| 先例 | ADR-029 自身 | `Gameplay.Boot/DiseaseVitalsService.cs:205-223`(解码后调 `Courses.ApplyOnset/ApplyTreatment`) |
| ⚠️ 代价 | 须新开 ADR;`Sim` 引用集**不变**(接口住 Contracts) | `Judge` 是**纯函数**;喂 DTO 会把「解码 + 求值」拆成两段,须重新对齐 **AC-53-02**「同一事件流逐位一致」的口径 |
| 谁裁 | **用户**(ADR 级) | **用户**(ADR 级) |

> **两案都不改 `Sim` 的引用集** —— 甲案接口住 `Sim.Contracts`,乙案内容由外部传入。
> 故门 A / b2 / ADR-025 §① **均继续成立**,不构成障碍。

---

## 四、四条**独立于根因**的缺口(即便解了 §三 也仍在)

### 4.1 D 侧 `treatable_by` 门**自身结构性缺位**(9 侧未通)

`Sim/ProgressionEvaluator.cs:336-338` 自陈:

> 「⚠️ **门当前结构性缺位**:`DiseaseRegistryEntry.TreatableBy` 是**派生字段**,
> 写入期不填(RegistrySchema 头注 + GDD R1.3),且校验 R1-19 对非 null 直接
> `NotImplementedException` ⇒ 数据面上它**恒 null**。」

真源 `assets/data/disease_action_axis.json` 的**装载面未接进 sim 路径** ——
9 侧自己尚未通,**53 读不到**。(9 epic 虽已 `Complete`,该装载面缺口在
`ProgressionEvaluator` 内**以「不拦」的降级路径存在**。)

### 4.2 判定表数据**不存在**

`assets/data/53_outcome_table.json` —— **无此文件**(`assets/data/` 现有 16 个表,
无任何 53 / outcome / consequence 相关)。
卡面 §Implementation Notes 1 点名它是本 story 的**核心交付物**之一(「P/C/D 组合 → Outcome」)。

### 4.3 53 **零装配层**

全仓除**定义件自身**与**测试**外,**无任何代码引用** `ConsequenceInputAggregator` / `Inputs53`
(实测:`grep -rln … | grep -v /Tests/` = **1 命中 = 定义件本身**)。
⇒ 无「谁调 `Aggregate`、`careKinds` 传什么、`IDiseaseProjection` 谁实现」的落地处。

### 4.4 AC-53-03 的静态检查器与 AC-53-02/15 的黄金夹具**均无载体**

- AC-53-03(零浮点字面量)点名「静态检查入构建」—— 既有 `Editor.Tools.Gates` 下
  只有 10 的 `EmergencyIntegerGates` 与 6 的 `EcozoneIntegerGates`,**无 53 的门**。
- AC-53-02/15 点名 `tests/golden/medcons/*` —— **目录不存在**(`tests/golden/` 本身不存在;
  golden 基建以 `tests/unit/item_database/golden/` 的**分散形态**存在,无统一 `tests/golden/`)。

---

## 五、勘察中发现的**待裁口径**(与 S12-9 是否退回**无关**,但须登记)

**处置记录的 Kind 闭集 —— 四处口径不一致**(story-001 文件头张力 ① 已登记,本勘察复核为**仍在**):

| 处 | 内容 | 出处 |
|---|---|---|
| GDD 规则五 | `53.Inputs = { CaseClosed, PatternRecognized, JudgmentRecorded, **CareApplied**, 病程投影 }` | `design/gdd/medical-consequences.md:165` |
| 卡面 §Implementation Notes 4 | 「与 10/11 写入的 `EmergencyAttempt` / `DrugTreatmentApplied` 的映射表按 GDD 规则五逐字实现」 | `story-002:19` |
| 实现 | `careKinds` 为**显式参数**,由装配层传入,调用方决定 | `ConsequenceInputAggregator.cs:20, 122-125` |
| 测试 | 夹具取 `{ EventKind.CareApplied }`,并明写「**只**钉参数被尊重,不钉集本身该是什么」 | `medcons_aggregation_test.cs:23, 60` |

⇒ **真值集未裁**,且**无装配层**可承接(§4.3)⇒ 当前**无任何生产调用点**决定它。
**该口径归 53 的 GDD 轮**,不在 S12-9 面内。

---

## 六、建议的处置(供裁定)

**S12-9 退回单独立项**,并拆为下列前置件(建议顺序):

1. **【ADR 级 · 须用户裁】** 读侧解码:甲案(`IPayloadDecoder`)vs 乙案(装配层 DTO)——
   补 ADR-029 的对称缺口(§3.2)。**这是唯一的结构性阻塞。**
2. **【9 侧】** `treatable_by` 装载面接进 sim 路径(§4.1)—— 归 9 的装载轮。
3. **【53 GDD 轮】** 处置 Kind 闭集口径裁定(§五)+ 判定表数据(§4.2)。
4. **【基建】** 53 的零浮点门(AC-53-03)+ 黄金夹具目录(AC-53-02/15)(§4.4)。
5. **【最后】** story-002 实现 —— 前置 1–4 齐备后方可动手。

---

## 七、可复核判据(逐条可重跑)

```bash
# ① Sim 引用集(门 A 结构性来源)
grep -n '"references"' unity/Assets/Sim/Sim.asmdef                 # ⇒ ["Sim.Contracts"]

# ② 读侧抽象点不存在
grep -rn "interface IPayloadDecoder\|interface IPayloadReader\|interface IPayloadCodec" \
    --include=*.cs unity/Assets | wc -l                            # ⇒ 0

# ③ 53 零生产消费者(排除定义件与测试)
grep -rln "ConsequenceInputAggregator\|Inputs53" --include=*.cs unity/Assets \
    | grep -v "/Tests/"                                            # ⇒ 仅定义件本身

# ④ 判定表与黄金夹具不存在
test -f assets/data/53_outcome_table.json ; echo $?                # ⇒ 1(不存在)
test -d tests/golden/medcons ; echo $?                             # ⇒ 1(不存在)

# ⑤ DiseaseProjectionSnapshot 无命中位(仅 Tick)
sed -n '47,54p' unity/Assets/Sim/Consequences/ConsequenceInputAggregator.cs

# ⑥ 9 侧 treatable_by 门恒 null 的自陈
grep -n "门当前结构性缺位" unity/Assets/Sim/ProgressionEvaluator.cs # ⇒ :336
```

---

## 八、本件的边界(如实登记)

- **本件是勘察,不是评审** —— 不对 story-001 的实现作任何判定;不对 story-002 的**规格**作判定
  (规格本身自洽,阻塞在**外部前置**)。
- **未运行任何测试** —— 本件是静态勘察(读码 + grep + 文件存在性)。
  故**本件不产生任何「已绿」声称**,亦不涉「禁借绿」面。
- **§三 的两案均未做可行性验证** —— 甲案需新开 ADR 后方可评估实现成本;
  本件只登记「两案均不改 `Sim` 引用集」这一**结构性事实**(已由 3.3 的推理给出,可复核)。
- **未改动任何生产码 / 测试 / 文档**(本件为纯新增勘察件)。
