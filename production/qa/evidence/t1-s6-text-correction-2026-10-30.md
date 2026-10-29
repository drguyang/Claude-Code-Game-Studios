# T1 · S6 —— §Decision ⑤ 的两处文本订正 + S9(构建路径)登记

- **对象**:`ADR-035 §Migration Plan` 的 **S6**;**同批追加 S9**(2026-10-30 用户裁定「加 S9」)
- **日期**:2026-10-30
- **判据(S6 行原文)**:代码 remarks 与卡面 Forbidden 不再自相矛盾

---

## 一、两处文本的**原文 → 新文**

> 原文按「原文不删」纪律**在此完整留档**;两个目标文件里已不再出现原句。

### ① `unity/Assets/Sim/Consequences/ConsequenceEmitter.cs:93`

| | 内容 |
|---|---|
| **原文** | 「只写 `ConsequenceResolved`(世界流;53 不写病史流 / 病例流 —— 卡面 Forbidden)。判定归 story-002,聚合归 story-001,呈现归 story-004。」 |
| **问题** | 这是**类级**陈述(「本类只写 `ConsequenceResolved`」),但被写成了**系统级**禁令(「53 不写病史流 / 病例流」)。ADR-035 §Decision ⑤ 已**显式扩张** 53 的写者面为「世界流 + 病史流**恰一支**」⇒ 系统级那句是假的,而类级那句仍然为真。**两者混在一句里,才产生了矛盾。** |
| **新文** | 「本类只写 `ConsequenceResolved`(世界流)。……」+ 一段 2026-10-30 订正注:说明前者是**故事边界**、后者是**架构裁决**;病史流那一支是 `TreatmentHarmOnset`,写者在 T2 的禁忌结算组件,**不是本类**;病例流仍然零写入 |
| **同批** | 文件头「权威来源」列表补一行 `ADR-035 §Decision ⑤` —— 否则读文件头的人看不到这次扩张 |

### ② `production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md:30`

| | 内容 |
|---|---|
| **原文** | `- Forbidden: …;53 写病史流/病例流(只写世界流)` |
| **新文** | `- Forbidden: …;**本 story 范围内**不得写病史流 / 病例流(本 story 只写世界流)` + 其下一段 2026-10-30 订正注 |
| **为什么不是「删掉这一条」** | 卡面的 Forbidden 是**故事边界**,对 story-003 **仍然成立**;错的是把边界当成了系统的能力上限。⇒ **收窄措辞,不删条目** —— 删了会让本 story 失去一条真实约束 |

---

## 二、V-8 判据的实跑

```
$ grep -c '只写 `ConsequenceResolved`(世界流;53 不写病史流' \
    unity/Assets/Sim/Consequences/ConsequenceEmitter.cs \
    production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md
unity/Assets/Sim/Consequences/ConsequenceEmitter.cs:0
production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md:0
```

**该串在全仓仍有两处命中,都在 `adr-035` 自己身上**:

| 位置 | 是什么 | 处置 |
|---|---|---|
| `adr-035:92` | §Problem Statement 的 **C-2 行** —— 「原冲突长什么样」的**历史记录** | **保留**(「原文不删」;它不属「两处文本」) |
| `adr-035:417` | V-8 判据**自己的引文** | 保留(判据文本) |

⇒ V-8 的判据面 = **两个目标文件**;ADR 内的历史引据不在面内。这一点已在 ADR 的 V-8 行明写,不靠读者推断。

---

## 三、S9 登记(**未实现**,只加进 §Migration Plan)

**为什么要加**:V-5 / V-6 / V-7 的字面判据都是「**构建** Failure / 构建拒收」,而今天
`53_cost_table.json` **零读方**——没有 `CostTable53Bake`、没有 `CostTableCookedWriter`、
没有 `DataBakeMenu` 入口、没有 `AssemblyGates` 门。⇒ **不存在能被"构建"执行到的路径**,
三条判据只能记「绑定器层拒收」(这正是 S3 / S4 证据件 §五 已登记的状态)。

**S9 交付面(两项)**:

| # | 件 | 先例 |
|---|---|---|
| ① | `CostTable53Bake`(compute)+ `CostTableCookedWriter`(encode)+ `DataBakeMenu` 菜单入口 | `DelayTableCookedWriter.cs` / `DataBakeMenu.BakeDelayTable()` |
| ② | `AssemblyGates` 新增 `CostTableGate`(`IPreprocessBuildWithReport`),对 repo 的 JSON + 9 的注册表跑 binder,`errs` 非空 ⇒ `BuildFailedException` | `AssemblyGates.RunAll` 的既有门链 |

**S9 的验收 = 探针轮三件套**(承 `CLAUDE.md` §构建前门):
注入 `lethal=true` 行 ⇒ **构建 `Result: Failure`** + 报文点名该行 + 栈帧给出该门的 `OnPreprocessBuild`;
**并且**还原后同路径**绿**(两侧都要 —— 只断「会红」是 S12-4 抓过的形态)。

**顺序**:S9 须在 **S7(证伪轮)之前** —— 证伪轮的靶场要含「构建门」这一面。

---

## 四、本件**不**主张

| 项 | 状态 |
|---|---|
| V-5 / V-6 / V-7 已过 | ❌ **未跑** —— 载体 S9 未落,今日仍不存在构建路径 |
| 卡面「不自相矛盾」有机械门 | ❌ **没有** —— 本处是**人工判读**;唯一机械判据是 V-8 那两条 grep,它只证明**原句不在了**,不证明**新句是对的** |
| S9 已实现 | ❌ 只登记 |
| ADR 的其它文本也扫过 | ⚠️ 只扫了两个目标文件 + 全仓 grep 该原句;**未对 53 全族的其它「写者面」措辞做普查** |
