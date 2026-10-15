# 裁定原件:AC-11-22 半边甲 —— 文本背离的收窄裁定

> **对象**:`AC-11-22`(GDD `prescription-and-medication.md`)与 `TR-prescription-018`
> 的**「构造点仅存在于 11 的程序集」**字面,与实现的**结构性背离**。
> **日期**:2026-10-15
> **裁定人**:`technical-director`(技术口径)· `producer`(流程面复核)
> **裁定**:**(甲) 收窄 AC 文本** —— **改文本,不改代码**。
> **性质**:本件**不是**「评审」,是**裁定记录**;但它产出 BLOCKING 级判定(决定
> 「判据与实现谁是对的」),故按 `.claude/docs/review-workflow.md` 义务 1 落盘。
> **触发**:`prescription-medication/story-003` §Completion Notes 的 M-2 行自 2026-10-06 起
> 即写「本件不改 AC 文本 —— 收窄裁定**归 producer / TD**」。本件是该悬置项的结项。

---

## 一、问题(逐字引原文,未加工)

**AC-11-22 原文**(GDD `:980`,2026-09-19 二轮 BL-4 新立,**订正前**):

> `GIVEN` 全案程序集,`WHEN` **反射 / 引用集断言**,`THEN` `SimEvent.Kind.DrugTreatmentApplied`
> 的**构造点仅存在于 11 的程序集**……且 7a 的序列化器**两个 Kind 均在白名单内**。

**实据(背离点)**:

| 构造点 | 位置 | 性质 |
|---|---|---|
| 写入 | `unity/Assets/Sim/Prescription/PrescribeFlow.cs:327` | ✅ 11 的程序集(AC 的**正题**) |
| 解码重建 | `unity/Assets/Sim.Codec/PayloadCodec.History.cs:332` | ⚠️ **在 11 之外**构造同一类型 |

**门早已显式记账**(`PrescriptionWriterGates.cs`,2026-10-06 结构侧评审 M-2 修复轮):
`PayloadCodec` 从 `ProductionCtorSites` **移出**,新立 `DecoderCtorSites` **分离登记**,
`RunAll` §⑤ 摘要报警(「判据-文本背离……收窄裁定未落盘(归 producer / TD)」)。
⇒ **本背离从未被静默放过** —— 这是本裁定得以走「甲案」的重要前提。

---

## 二、裁定(甲案)与三条口径

**改 AC 文本,使判据与实现一致。** 三条口径同批落盘:

1. **保真订正:「反射 / 引用集断言」⇒「IL / metadata 扫描」**。
   理由:`System.Reflection` **看不见** `newobj` / `call .ctor` 指令,引用集断言只到
   **程序集**粒度 —— 两者**在原理上都无法**表达「构造点**集合** ⊆ 11」。
   原文写了一种**做不到该判据**的机制,而实现用的是 IL/metadata 扫描(Mono.Cecil,见门文件)。
   **这是纯保真订正,不改变判据强度。**
2. **收窄范围:「构造点仅存在于 11」⇒「写入路径构造点仅存在于 11」**。
3. **豁免具名**:唯一豁免点 = `DaYiJingCheng.Sim.Codec.PayloadCodec`(在门内以
   `DecoderCtorSites` **具名**登记,**非**前缀/通配豁免 —— 与门自身「豁免须具名有界」的纪律一致)。

---

## 三、为什么这是「文本缺陷」而不是「实现缺陷」(裁定核心)

**载荷 `DrugTreatmentAppliedPayload` 是 `readonly struct` 且只有唯一 ctor。**
⇒ **任何**从字节还原该载荷的解码器,**必然**要调用那个 ctor(否则无法得到该类型的值)。
⇒ 只要「7a 的序列化器两个 Kind 均在白名单内」(**AC 同一句的下半句,且是本条真正想要的**)
成立,**解码器就必须在 11 之外构造该类型**。

⇒ **AC 的原字面对*任何*满足其下半句的合法实现恒假。**

**这是本工程已命名的一类失效模式 —— 「判据对合法输入类误判」**
(`systems-index.md:47`,与 **BL-7** 同族;对偶 = 「判据空转」):

> 一条**无条件**要求对**合法输入类**恒成立的判据,会把**合法实现**判红。

**因此本条的修法是改判据文本,不是改实现。** 反过来说:若强行让实现服从原文
(把解码器搬进 11 / 让门把 `PayloadCodec` 判红),代价是**拆掉 ADR-025 §① 的引用集边界**
(见 §四),即**用一条写错的判据去推倒一条对的架构** —— 不可接受。

---

## 四、被否的备选(留档,供复审)

| 备选 | 内容 | 否决理由 |
|---|---|---|
| **乙** | 机制到位即可,**不改 AC 文本**(门已有分离登记 + 摘要报警) | ① **机制到位 ≠ 权威到位**:AC 是判据本身,门是执行体;执行体单方面放宽判据,正是 M-2 修复轮**已经判为违例**的行为(门注原文:「等于用注释改写 AC」)。② 留一个**已知恒假**的 BLOCKING 判据在权威件里,下一轮评审会**再踩一次**,且这次没有账。 |
| **丙-1** | 把解码构造点**搬进 11**(让实现服从原文) | 违 **ADR-025 §①**:`Sim` 的引用集**恰 = `{BCL, Sim.Contracts}`**,`Sim` **够不着** `Sim.Codec`。搬进去须拆 b2 门 / 门 A —— 为一条写错的判据拆架构边界。 |
| **丙-2** | 让解码器**不经 ctor** 重建(如手工填字段) | ① `readonly struct` + 唯一 ctor ⇒ 只能靠反射/`Unsafe` 之类绕开,引入**字节序与内存布局依赖** ⇒ 违 **ADR-012**(跨平台逐位确定性);② 把一条**判断句**换成一条**更危险的实现技巧**,得不偿失。 |
| **丙-3** | 把载荷类型改 `internal` + `InternalsVisibleTo` | **不移动调用点** —— 索引器仍在 `Sim.Codec` 内构造,原文照旧恒假;**白做**。 |

---

## 五、连锁面(本次实际落盘清单)

> 权威链**必须同批**改 —— 只改 GDD 会让 registry / traceability-index 逐字复述旧句,
> 生产**新的**同文件矛盾(义务 5 的失效模式)。

| # | 文件 | 落点 | 性质 |
|---|---|---|---|
| 1 | `design/gdd/prescription-and-medication.md` | `AC-11-22` 表行 | **权威文本**(原文保留 + 就地 ⚠️ 订正注) |
| 2 | `design/gdd/prescription-and-medication.md` | 规则三 BL-4 注 | **权威文本**(同上,指向表行) |
| 3 | `docs/architecture/tr-registry.yaml` | `TR-prescription-018` 的 `requirement` + `note` | **权威文本**;`status` **维持 `partial`**(理由见 §七) |
| 4 | `docs/architecture/traceability-index.md` | `TR-prescription-018` 行 | **权威文本** |
| 5 | `unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs` | 文件头权威来源段 | 门注(**零逻辑改动**) |
| 6 | 同上 | `DecoderCtorSites` 的 `<summary>` | 门注(含**陈旧行号订正** `:295` ⇒ `:332`) |
| 7 | 同上 | `RunAll` §⑤ 摘要 | 门注 —— 由「判据-文本背离」改称「**解码重建豁免点**」 |
| 8 | `production/epics/prescription-medication/EPIC.md` | `:6` 状态行 | **新增「状态口径」块**(§八 ①) |
| 9 | 同上 | 故事表 story-003 行 · §GDD Requirements `TR-018` 行 · §Epic Status 未闭项表 · 2026-10-07 增量段末 | 登记回刷 |
| 10 | `production/epics/prescription-medication/story-003-….md` | `:36` AC-11-22 · §QA Test Cases 独占扫描 · M-2 行 · §Completion Notes 未闭项段 | 登记回刷(**AC 保持勾选**,理由见 §八 ③) |
| 11 | `production/qa/qa-plan-sprint-11-2026-10-14.md` | `:62` | **就地加注**(原文保留 —— 义务 5 口径) |
| 12 | `production/sprints/sprint-11.md` | S11-4 订正框 ⑦ + **新增「尾巴 B 结项框」** | 台账 |

**新建收口件**:本件。

⚠️ **本裁定零代码改动** ⇒ **不跑 Unity**;`Sim/` 与 `Sim.Codec/` 生产代码**一字未动**
⇒ S11-4 的证伪三步(`fix-s11-4-prescription-dod-residuals-2026-10-15.md` §四)仍为其时的有效记录,**不重跑**。
⚠️ **`Control Manifest Version` 不触发**:实测 `control-manifest.md` 对「构造点」**零命中**
(`story-003` 头部的 `Control Manifest Rules` 那两条是 **story 级抽取**,非 manifest 规则条目),
且 `TR-prescription-018` 的 `adr: null` ⇒ **207 份 story 不受影响**,无需重生成。

---

## 六、可证伪条件(本裁定若错,如何看出)

1. **载荷若不再是 `readonly struct` / 不再只有唯一 ctor** ⇒ §三 的「必然调用 ctor」论证失效,
   收窄的前提消失,须重裁。
2. **若 `DecoderCtorSites` 出现第二项** ⇒ 豁免面扩大,须复核是否仍属「解码重建」语义
   (门摘要保留条目数,即为使此可见)。
3. **若 AC 同一句的下半句(「两个 Kind 均在白名单内」)被删除或改写** ⇒ 本条与下半句的耦合断裂,
   须重评。
4. **若 ADR-025 §① 的引用集边界日后被拆**(`Sim` 可引 `Sim.Codec`)⇒ 备选丙-1 的否决理由消失,
   须重评(而非自动沿用本裁定)。
5. **若 `RunAll` 摘要不再报出豁免点** ⇒ 「非静默」前提失效,本裁定的「甲案」前提
   (背离已被显式记账)不再成立。

---

## 七、未闭项(明确不在本裁定面内)

| # | 项 | 归属 |
|---|---|---|
| 1 | **`TR-prescription-018` 维持 `partial`** —— 其第二条理由(「作者侧『未注册 Kind 构建期失败』的全局防线仍缺」)**疑已陈旧但本轮未核**:ADR-024 ⑤ 的 kindgen 通道**已实测存在**(`tools/kindgen/kindgen.py` + 生成物 `unity/Assets/Sim/StreamRouting.g.cs`),其是否已**接线**到构建期拒绝**未核** ⇒ 登记为**待核**,既不借绿也不借红 | 下一次 `/architecture-review` |
| 2 | 影子装配注入半边(**需动态生成程序集**) | story-003 未闭项 |
| 3 | `CheckPayloadPairing` 不覆盖 codec tag 序 | 归 ADR-014 阶段 2 |
| 4 | `AC-11-15` 三格矩阵 / 跨进程 | ADR-012 矩阵未激活 |
| 5 | **epic DoD 第 2 / 第 4 条仍不绿** | 见 §八 ① |

⚠️ **本件不使 `prescription-medication` 的 DoD 转绿**,也不使任何 AC 新转 RUN。

---

## 八、同轮 producer 复核的三项结论(非本裁定,但同批落盘)

### ① 「story 全 Complete」vs「epic DoD 未达」的表述矛盾 —— 已修

`EPIC.md:6` 顶着 `Complete ✅ (5/5)`,而同文件 §Epic Status 写「**但 epic 未达 DoD**」——
**同文件自相矛盾**。同侪 `emergency-procedures/EPIC.md` §未闭项登记有显式一句
「未闭项**均结构性归属其他 epic**,**不阻塞本 epic**」,而本 epic **没有这句**。
**处置**:在 `EPIC.md:6` 后补「状态口径」块,明写两件事不冲突 + 未闭项的结构性归属。

### ② `story-003:36` 的 `[x]` 是否借绿 —— **不是**

TD 裁甲 ⇒ **代码没错、文本错** ⇒ `AC-11-22` 的复选框**保持勾选**是**据裁定**,
不构成借绿。**story 状态不回退**(若裁「文本成立」则须按 `case-system` 先例
从 `Complete` 退回 `In Review` —— 本裁定不触发该分支)。

### ③ 计划件口径冲突的处置惯例 —— 就地加注,不抹除

`qa-plan-sprint-11:62` 把「7a 白名单」读作**一张需登记的条目表**(实测该产物不存在),
且其第二半「无白名单外**字段**」是**字段级**命题,与 AC-11-22 的 **Kind 级**不是同一命题
—— **两半皆不成立**。
**处置惯例(实测先例)**:`e67b33b`(2026-10-04)改 `qa-plan-sprint-02/03.md` 时
「**四处均保留原名并附订正注记(不抹除历史读数)**」;`b1f32ca`(2026-10-06)对
`qa-plan-sprint-03.md` 的 `Remaining` 行就地追加。
⇒ **本次照此办理**。
⚠️ **纠正**:S11-4 证据原件 §8.3 曾以「改动它 = 改判据」为由**不动**该行 —— 该理由**过严**。
`b1f32ca` 恰是在**判据未变**的前提下加的解除性注 ⇒ **「加注 ≠ 改判据」**。
正确次序 = **先裁定、后加注**。

---

## 九、过程面沉淀(两条,均**不做 hook**)

| # | 落点 | 内容 |
|---|---|---|
| 1 | `.claude/skills/sprint-plan/SKILL.md` §Phase 1 第 5 步 | **硬前置**:纳入 sprint 的每个 epic,须**重读** `EPIC.md` §Epic Status + 目标 story 的 `Status:` 行,**不得沿用旧快照**(sprint-11 的 should/nice 两项任务书前提**均已过时**,见 `s11-5-…-2026-10-15.md` §2.1) |
| 2 | `.claude/docs/review-workflow.md` **义务 5「回刷收口」** | 改完一处口径,须对**同一关键词**全仓 grep,逐条判「动 / 不动」并落纸(样板 = S11-4 原件 §8.2);计划件 / 历史读数件用「原文保留 + 就地加注」 |

⚠️ **两条均刻意不做 hook**:机械 hook 判不出「这个命中该不该动」「这条前提是否过时」
(需读懂该行是**声明**还是**表 / 件**,以及是否属域外)⇒ 做了就是**假门**。
与 `review-workflow.md` 义务 4 同一条纪律。
