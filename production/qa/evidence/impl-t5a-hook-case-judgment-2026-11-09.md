# T5a 钩子病例的判定 —— 实施与证据(2026-11-09)

> **范围**:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2 的**判定半边**
> —— 规则 1(每章 1–3 例)· 规则 2(必须可诊断)· 规则 3(必须指出缺哪门技法)。
> **不在本件**:规则 4(不点破地点,呈现层)· **收场**(见 §6 的待裁项)。
> **上游**:T1–T4(`027b065d` / `4951bb10` / `be3bf708` / `7fbb56a7`)。

---

## 1. 改动清单

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `unity/Assets/Gameplay.Boot/HookCasePolicy.cs` | **新** | 可诊断性判据 + 章内配额(`MaxPerChapter = 1`) |
| 2 | `unity/Assets/Gameplay.Boot/HookCaseLedger.cs` | **新** | 从事件流重建「已注入几例」—— 前向扫一遍,onset 那一刻技法全锁才算钩子 |
| 3 | `unity/Assets/Gameplay.Boot/PatientDrawEligibility.cs` | 改 | `PatientDrawDecision` 增 `MissingTechniques`(规则 3 的数据面);`Decide` 增 `hookAllowed` |
| 4 | `unity/Assets/Gameplay.Boot/PatientAppearedDriver.cs` | 改 | `admit` 由 `Func<…,bool>` 改为 `Func<…,PatientDrawDecision>`(钩子标记要传出来) |
| 5 | `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 改 | 装配账本 + 判据接进两路出生 |
| 6 | `unity/Assets/Tests/EditMode/DiseaseSimulation/hook_case_policy_test.cs` | **新** | **20 条** |
| 7 | `unity/Assets/Tests/EditMode/DiseaseSimulation/patient_draw_eligibility_test.cs` | 改 | 两处 `admit` 的返回类型随改(语义不变) |

**没有新增 Kind、没有改数据、没有改存档格式。**

---

## 2. 三条规则的落点

| 规则 | 判据 | 载体 |
|---|---|---|
| 1 每章 1–3 例 | `HookCasePolicy.MaxPerChapter`(P0 = **1**,§8 R4「P0 按章内 1 例起步」)+ 账本计数 | `AllowsInjection(row, injectedInChapter)` |
| 2 必须可诊断 | `signs[]` 非空,**且至少一条体征落在体格通道**(排除 `Subjective` / `History` —— 8 的 `MaskOf` 对这两位返回 0) | `IsDiagnosable(row)` |
| 3 必须指出缺哪门技法 | `PatientDrawDecision.MissingTechniques`(升序,技法全锁时回带) | `Decide` 的技门分支 |
| 4 不点破地点 | 呈现层(42/48)· **本件不管** | — |

**规则 2 的口径为什么是「有体格通道」而不是「条数够」**:两三条主观/病史体征仍然看不见 ——
判据要的是「有可看的东西」,不是「signs 数不为 0」。

---

## 3. 账本为什么从事件流重建

内存计数器读档后不可重建(存档体里没有它),重放会得到不同的钩子配额 —— 与
`ADR-039 §D2` 删掉两个内存字段的理由逐字相同。

判据:**一个病人是钩子病例 ⟺ 他的 `DiseaseOnset` 那一刻,该病种在轴里声明的技法一门都没解锁。**
前向扫一遍维护「截至此刻该 actor 已解锁的技法集」即可,**后解锁不追溯** ——
既不会把先前那例钩子数丢(配额凭空多一格),也不会把之后的同病种病人误判成钩子。

- 未被轴声明的病种**不算**钩子(它走 `Undeclared` 分支,是「数据未覆盖」而不是「缺技法」)。
- 解锁按 `actor` 区分(ADR-043 D7):别人的解锁不算。

---

## 4. 回归

**三元组**:`2026-11-09T10:57:16Z`(EditMode 结束)/ `10:58:34Z`(PlayMode 结束)·
**全量** · `7fbb56a7` + 工作树。

| 套件 | 本次 | 对照基线(T4 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7224 / 7187 / 0 / 37`** | `7204 / 7167 / 0 / 37` | **+20 / +20** |
| PlayMode(全量) | **`370 / 366 / 0 / 2 / 2`** | `370 / 366 / 0 / 2 / 2` | 0 |

**等式核对**:`7224 − 7204 = 20` = 新夹具 `[Test]` 数 **20**。✔

**两轮编译失败,均如实登记(都是我的疏忽,不是设计问题)**:

1. `PatientDrawEligibility.cs` 漏 `using System.Collections.Generic;` ⇒ `IReadOnlyList<>` 找不到。
2. 夹具里的 `Sink` 没实现 `IEventSink` ⇒ `PatientSpawner` 构造失败。

两轮都停在编译期(没有跑出任何判据),修好后第三轮全绿。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for p in ("unity/Logs/editmode-t5c.xml", "unity/Logs/playmode-t5.xml"):
    r = ET.parse(p).getroot(); at = r.attrib
    print(p, at["total"], at["passed"], at["failed"], at.get("inconclusive"), at["skipped"], at["end-time"])
PY
# 期望:
#   editmode-t5c.xml 7224 7187 0 0 37 2026-11-09 10:57:16Z
#   playmode-t5.xml   370  366 0 2 2  2026-11-09 10:58:34Z
```

---

## 5. 夹具覆盖(20 条)

| 组 | 条数 | 内容 | 反证 |
|---|---|---|---|
| 可诊断性 | 4 | 体格通道 true / 只有主观与病史 false / 无 signs false / 混合 true | 把「有体格通道」改成「signs 非空」⇒ 第 2 条转绿失败 |
| 配额 | 4 | 未满放行 / 已满拒 / 未满但不可诊断拒 / 负数 fail-loud | 删 `injectedInChapter < MaxPerChapter` ⇒ 第 2 条红 |
| 与门组合 | 5 | 技法全锁 + 允许 ⇒ `HookCase` 且打标 / 不允许 ⇒ 拒 / **等级不足不因钩子放行** / 投放时不报缺口 / 钩子回带缺失技法 | 把 `hookAllowed` 塞进等级分支 ⇒ 第 3 条红 |
| 账本 | 5 | onset 在解锁前算 / 解锁后不算 / 后解锁不追溯 / 未声明病种不算 / 别人的解锁不算 | 用「当前是否解锁」代替前向扫描 ⇒ 第 3 条红 |
| 驱动接线 | 2 | 配额未满 ⇒ 钩子被放出来 / 配额满 ⇒ 零病人零事件 | 驱动里不传 `hookAllowed` ⇒ 第 1 条红 |

**无自指判据(F3)**:期望值全为字面量。

---

## 6. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| **收场(转诊 / 送医 / 死亡)** | **待裁** —— 见下 |
| 「章」的运行时边界 | **不存在**(全仓零章节状态)⇒ 接线恒取章号 0,「每章 1 例」退化为**整局 1 例** |
| 呈现层「手册缺页」(规则 4 + 缺哪门的可视化) | **T6**(42/48) |
| 生产可达性 | 驱动上限 2 人、轮转首两格是伤情 ⇒ **今日真包里抽不到钩子**(机制在,暴露面缺)—— 与 T4 的等级门同格,如实登记 |

### 收场为什么没做(本件最重要的一条判断)

用户裁定「转诊 / 送医 / 死亡,三选一,不许病人自己好了」。三条路线都撞上了既有判据:

| 路线 | 撞什么 |
|---|---|
| **甲** 新增 `PatientOutcome.Transferred = 3` | `FoldRowCodec` 用 `{1,2}` 当**损坏哨兵**(`fold_row_codec_test.cs:93` 明写「未知 outcome(3) = 档损坏,拒收」)。加 3 会让旧档里的垃圾字节被读成合法转诊 ⇒ **存档格式级变更**,须版本 + 迁移 |
| **乙** 新增病例流 Kind(如 `CaseReferred`) | 须 ADR + kindgen + codec 一套(与 ADR-030/043 同量);语义最准:转诊是**病例侧结论**,不是 F4 九态 |
| **丙** 不新增载体 | 「不许自己好了」只能靠数据(`self_limit = false`)+ 收场留白;钩子病例的结局不可观测 |

⇒ 本件**不擅自选路**:三条的代价差一个数量级,而地图的 T5 判据行只写了
「每章计数上限 / 必须可诊断 / 必须给出缺失技法」三条,不含收场载体。
**建议 = 乙**(不碰折叠词表、不动存档格式,且「病人离开你的照护」本来就不是病程九态)。
