# T4 病人投放资格 —— 实施与证据(2026-11-09)

> **范围**:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.1 / §5.2 的落地
> —— 抽取改「技法门(硬)+ 等级门(软)」。
> **上游**:T1(`027b065d` 拆表)· T2(`4951bb10` 两视图 + 两列)· T3(`be3bf708` 技法解锁载体 / ADR-043)。
> **本件不声称 T5 已落**:钩子病例的**判定**(谁是钩子、每章几例、收场三选一)归 T5;
> 本件只给「它可以被放行,且调用方能看见它是特例」这一个位。

---

## 1. 改动清单

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `unity/Assets/Gameplay.Boot/TechniqueDrawIndex.cs` | **新** | 病种 key → 能治它的技法 id 集(反向索引)。`ITreatableByLookup` 只有正向查询(`(病种,处置) → 极性`),投放门要的是「这个病种有哪些技法」 |
| 2 | `unity/Assets/Gameplay.Boot/PatientDrawEligibility.cs` | **新** | 纯函数判据 + 七值判定枚举(`Admissible` / `HookCase` / `Undeclared` / `NoUnlockedTechnique` / `LevelTooLow` / `PlayerNotReady` / `NotWired`) |
| 3 | `unity/Assets/Gameplay.Boot/PatientAppearedDriver.cs` | 改 | 轮转先过投放资格;全表落选 ⇒ 本拍不产病人(不抛、不写半条) |
| 4 | `unity/Assets/Gameplay.Boot/EventPoolPatientSpawner.cs` | 改 | `CanSpawnPatient` 增第二道判据(52 路) |
| 5 | `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 改 | 建反向索引;两路共用**同一份**判据;`playerActor` 迟绑定槽前移;`P0PrescriptionLevel` 抽常量 |
| 6 | `unity/Assets/Editor.Tools.Bake/DiseaseRegistryBinder.cs` | 改 | 分区门**双向化**(T2 只查 `injuries` 收非伤;现补 `diseases` 不收伤) |
| 7 | `unity/Assets/Tests/EditMode/DiseaseSimulation/patient_draw_eligibility_test.cs` | **新** | **15 条**(9 条判据 + 3 条边界 + 3 条两路接线) |
| 8 | `unity/Assets/Tests/EditMode/DiseaseSimulation/disease_registry_partition_test.cs` | 改 | +1 条(反向分区门的负例) |
| 9 | `unity/Assets/Tests/EditMode/DiseaseSimulation/disease_registry_bake_pipeline_test.cs` | 改 | `MinimalJson` 的伤行由 `diseases` 搬到 `injuries`(`diseases` 留空数组)—— 反向门收紧后原形态不再合法 |

**没有新增产物、没有改数据、没有改 ADR** —— 本件是把 T2 的两列与 T3 的解锁查询**接上消费方**。

---

## 2. 判据的三个状态(本件最关键的一处判断)

只看「有没有已解锁技法」会把**三种处境**压成一个 bool:

| # | 处境 | 判定 | 是否投放 |
|---|---|---|---|
| ① | 轴里声明了 N≥1 门技法,且至少一门已解锁 | `Admissible` | ✅ |
| ② | 轴里声明了,N 门都没解锁 | `NoUnlockedTechnique` | ❌ **这就是用户裁的「未开启前不出现」** |
| ③ | 轴里**没有**这个病种(数据未覆盖) | `Undeclared` | ✅ **但计数**(`IsUndeclared`,观测面) |

**为什么 ③ 不拒**:今日轴只覆盖 9 个 `DIS_*`,伤情 7 行(含第一个病人「刺伤」)**零命中**;
而 9 个 `DIS_*` 的技法 10/11/12 又都不在起步集 `{0, 1}` 里。严格两态 ⇒ 生产上
**一个病人都投放不出来**,而那是**静默**的(世界没病人 ≠ 报错)。
处置与 `ITreatableByLookup` 的 `null` 语义同源:「未接线 / 未覆盖 ≠ 没有可用处置」。

⚠️ **这不是给门开后门**:② 一旦成立就照拒 —— 今日实测有 **7 个病种真的被拒**(见 §3)。

---

## 3. 今日生产面实测(不是预测,是读数)

逐行对比 `disease_action_axis.json` 的 `treatable_by` 与注册表轮转序(`id` 升序):

| 段落 | 病种 | 判定 |
|---|---|---|
| **会被硬门拒收(7)** | 7 `DIS_MALARIA` · 8 `DIS_RHEUMATIC_FEVER` · 9 `DIS_HEART_FAILURE` · 10 `DIS_TYPHOID` · 11 `DIS_MEASLES` · 12 `DIS_DYSENTERY` · 17 `DIS_FEBRILE_SEIZURE` | 技法 10 / 11 / 12 **都不在起步集 `{0, 1}`** |
| **按未覆盖放行(9)** | 2 `INJ_STAB` · 3 `INJ_LACERATION` · 4 `INJ_HEMORRHAGE` · 5 `INJ_CLAW` · 6 `INJ_BITE` · 15 `INJ_BLUNT` · 16 `INJ_DRUG_HARM` · 13 `DIS_TETANUS` · 14 `DIS_NEURASTHENIA` | 轴里无行(伤情)/ 作者态 `[]`(破伤风、神经衰弱) |
| **轮转首两格** | 2 `INJ_STAB` · 3 `INJ_LACERATION` | 都属放行 ⇒ **既有 PlayMode 走查零变化**(实测 `PatientAppearedWiringTest` 仍 Passed) |

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import json
d = json.load(open("assets/data/disease_registry.json", encoding="utf-8"))
a = json.load(open("assets/data/disease_action_axis.json", encoding="utf-8"))
rows = sorted((r["id"], r["key"]) for k in ("diseases","injuries") for r in d[k])
blocked = [r for r in rows if len(a["treatable_by"].get(r[1], [])) > 0]
undec   = [r for r in rows if len(a["treatable_by"].get(r[1], [])) == 0]
print("拒收", len(blocked), blocked)
print("放行", len(undec), undec)
PY
# 期望:拒收 7 · 放行 9;首两格 = INJ_STAB / INJ_LACERATION
```

---

## 4. 回归

**三元组**:`2026-11-09T09:53:29Z`(EditMode 结束)/ `09:54:47Z`(PlayMode 结束)·
**全量** · `be3bf708` + 工作树。

| 套件 | 本次 | 对照基线(T3 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7204 / 7167 / 0 / 37`** | `7188 / 7151 / 0 / 37` | **+16 / +16** |
| PlayMode(全量) | **`370 / 366 / 0 / 2 / 2`** | `370 / 366 / 0 / 2 / 2` | 0 |

**等式核对**:`7204 − 7188 = 16` = 新夹具 **15** 条 + 反向分区门负例 **1** 条。✔

**两轮红,均如实登记**:

1. **首轮(`editmode-t4.xml`,7203 条)1 红** —— `test_driver_skipsRejectedDiseaseAndSpawnsAdmissibleOne`
   期望 `SpawnedCount == 1`、实得 2。是**我的期望值写错**(上限哨 2 人时两拍都会投放
   同一个可用户者,被拒那格永不补位),不是实现缺陷。判据已改为「两拍都必须是 8 号、
   7 号不得出现在流里」。
2. **次轮(`editmode-t4b.xml`,7203 条)0 红**,随后加反向分区门 ⇒ 末轮 **7204 条 0 红**。

> ⚠️ 第 1 条只改断言不改实现 —— 实现语义(跳过被拒格子、继续找下一个可用户者)是对的。
> ⚠️ PlayMode 在加反向门**之前**跑过一次(`playmode-t4.xml`,`370/366/0/2/2`);末轮
> `playmode-t4b.xml` 是加完之后**重跑**的,数字相同。反向门只动编辑器烘焙与 EditMode
> 夹具,结构上够不到 PlayMode —— 但按「禁借绿」纪律仍以重跑值为准。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for p in ("unity/Logs/editmode-t4c.xml", "unity/Logs/playmode-t4b.xml"):
    r = ET.parse(p).getroot(); at = r.attrib
    print(p, at["total"], at["passed"], at["failed"], at.get("inconclusive"), at["skipped"], at["end-time"])
PY
# 期望:
#   editmode-t4c.xml 7204 7167 0 0 37 2026-11-09 09:53:29Z
#   playmode-t4b.xml  370  366  0 2 2  2026-11-09 09:54:47Z
```

---

## 5. 夹具覆盖(15 条)

| # | 判据 | 反证(改什么会红) |
|---|---|---|
| 1 | 声明且全未解锁 ⇒ 拒收 | 删 `if (!anyUnlocked)` 分支 |
| 2 | 多门里**任一门**解锁 ⇒ 投放(∃ 语义) | 把 `any` 写成 `all` |
| 3 | 解锁的是**别人家**的技法 ⇒ 不投放 | 把技法集改成全局并集 |
| 4 | `min_skill_level >` 等级 ⇒ 拒 | 删等级门 |
| 5 | `min_skill_level ==` 等级 ⇒ 过(`≤` 不是 `<`) | 把 `>` 改 `>=` |
| 6 | 两道门都不过时报**技法**原因(归因指向硬门) | 交换两道门的判定次序 |
| 7 | 轴里无该病种 ⇒ 放行且 `IsUndeclared` 可读 | 把 ③ 改成拒收 |
| 8 | 空 key ⇒ 不炸,按未覆盖 | 让 `IsDeclared("")` 变真 |
| 9 | 钩子病例跳过两道门且 `IsHookCase` 为真 | 删 `if (asHookCase)` |
| 10 | `index == null` ⇒ `NotWired` 放行 | 让未接线拒收 |
| 11 | 玩家未绑定 ⇒ `PlayerNotReady` 放行 | 让未绑定拒收 |
| 12 | 谓词为 null ⇒ 构造期抛 | 容忍 null 谓词(静默放行) |
| 13 | 驱动:首格被拒 ⇒ 轮到下一格;被拒者**不进流** | 删驱动里的 `_admit` 调用 |
| 14 | 驱动:全表被拒 ⇒ 零病人零事件 | 同上,或让落选时仍 spawn |
| 15 | 52 路:`CanSpawnPatient` 真的判 false | 删 `isDiseaseAdmissible` 分支 |
| 16 | 分区门反向:伤行进 `diseases` ⇒ 构建期拒收(`disease_registry_partition_test`) | 删 `else if (… Kind == Injury)` 分支 |

**没有自指判据(F3)**:全部期望值是字面量,不引用被测类的常量或方法名。

### 补充:分区门双向化(原定「收紧归 T4」的清偿)

T2 的判据只写了单向(`injuries` 只收伤),并在代码注释里明写「反向刻意不查,等 T4 收紧」。
本件兑现:

- **判据**:`diseases` 数组出现 `kind: injury` ⇒ 构建期 `BakeValidationException`,报文点名数组名。
- **为什么现在可以收**:当年的唯一理由是「既有合成源仍用拆表前形态」;实测全仓
  **零处**合成源把 `kind: injury` 写进 `diseases`(`grep -rn '"kind": *"injury"'
  unity/Assets/Tests unity/Assets/Editor.Tools.Bake` 只命中 `MinimalJson` 一处)。
- **唯一连带改动**:`MinimalJson()` 的伤行由 `diseases` 搬到 `injuries`(行内容一字未改,
  只换顶层键;`diseases` 留空数组 —— `required: true` 只查**键存在**,不查非空)。
- **为什么投放门需要它**:候选面是 `Rows`(两数组并集),伤行若留在病侧,
  「病人池抽到伤情」与「病种口径」会静默混流 —— 与投放门判据无关,但同一次改动里
  它是最小且可证的收口。

---

## 6. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| **钩子病例的判定与收场**(谁是钩子 / 每章 1–3 例 / 必须可诊断 / 必须指出缺哪门 / 转诊-送医-死亡三选一) | **T5** —— 本件只提供 `IsHookCase` 位 |
| `min_skill_level` 的**真值** | 全 0(内容轮)· 等级门此刻恒过 ⇒ **它的拒绝分支只有夹具可达** |
| 玩家「处方用药」的**真等级** | 装配常量 `0`(P0 接线载体);`ISkillLevelQuery.QueryLevel` 仍是唯一真等级来源,本件不越界 |
| 伤情写进 `treatable_by`(止血包扎 → `INJ_*`) | **T7** —— 补上后「未覆盖」的 7 行会变成真门 |
| 蛇毒行 / 破伤风治疗项 / 毛地黄金鸡纳奇遇 | **T7** |
| 联机侧(45)的解锁权威 | 未做 —— ADR-043 明确不裁 |

### 已知数据形状缺口(本轮实测,如实登记)

**「作者态声明为 `[]`」与「完全不写这个 key」在运行期不可区分**:
`treatable_by` 只以**行**的形式存在,`"DIS_TETANUS": []` 与缺省 key 烘出来的行集
**逐字节相同** ⇒ 两者都落进 `Undeclared`。要区分,须让烘焙器另存一份「声明过但为空」
的 key 集。今日两行(TETANUS / NEURASTHENIA)实际影响相同,故本件**不改产物、不伪造区分**。
⚠️ 本缺口**不影响**技法门的核心判据 —— 「声明了 N≥1 门且都没解锁」是可表达的,
而那正是「未开启前不出现」所依赖的那一半。
