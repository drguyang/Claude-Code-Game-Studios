# 评审原件 —— `TD-medcons-15`(禁忌 / 过量代价表)三路专家意见 + 裁定落点(2026-10-29)

- **对象**:`docs/tech-debt-register.md` 的 `TD-medcons-15`(W-4:AC-5 / OQ-53-8 的代价表**从未存在**)
- **产出**:`docs/architecture/adr-035-treatment-harm-kind-and-no-lethal-gate.md`(2026-10-29 Accepted,**只钉形状,不实现**)
- **性质**:本条不是常规「评审 → 修复」轮,而是**裁定轮**。三路专家各自出意见(有分歧),
  由编排方逐条复核分歧、用户四项裁定,结论落 ADR-035。
- **原件构成**:本文件 = 三份草稿(`tmp-tdmedcons15-{systems-designer,game-designer,technical-director}.md`)
  的**合并件**。按 `code-review` skill Phase 7b ③,草稿在合并后删除(不留两份并行的分析段)。

> ⚠️ **轮次纪律的实际执行情况(如实登记)**:Phase 7b ③ 要求「每完成一项勘察就 Append 一行到草稿」。
> **三份草稿全部声明为「一次性收口落盘,非增量」**(systems-designer 未声明,但文件形态同为整篇;
> technical-director 明写「受 ≤15 次工具调用硬预算约束,草稿为一次性收口」)。
> ⇒ **S13-6 的收窄判据(草稿在轮次耗尽前已非空且覆盖过半)本轮未被真正检验** ——
> 三份都及时交付了,但交付形态是「一次写完整篇」,不是「增量 Append」。
> **这不构成判据通过**,只构成「没撞轮次上限」。禁借绿。

---

## 一、三份意见的**一致项**(分歧不在这些上面)

| # | 一致结论 | 三份来源 |
|---|---|---|
| U-1 | 债是真的:53 **零代价表载体**,`AC-5` 的 Then 句「致死行在 P0 表 = 构建期拒收」**没有宾语** | 三份均列 |
| U-2 | 「永不致死」**不能靠运行期 clamp、不能靠设计约定**;主判据须是**构建期跨注册表**比对(9 的 `lethal` 位) | systems-designer §二 · technical-director K-16 · ADR-035 ③ |
| U-3 | 53 的表**只能外键引用** 9 的伤情符号,禁内联伤害词表(`disease-simulation.md:671-672` 接口铁律二) | systems-designer §一 · technical-director K-12 |
| U-4 | **后遗已有载体**(回响轴 `Residual`,`53_outcome_table` 今天就在产)⇒ 不得为「后遗」造第二套 id | systems-designer §三 · ADR-035 ④ |
| U-5 | 53 的 `Outcome` 出口是**枚举、零后果数值字段**(`AC-53-01`)⇒ 代价表**不得**装分值 | systems-designer §一 · technical-director K-08 |
| U-6 | 代价要「有牙齿」就会撞 `ConsequenceEmitter.cs:93` 的**卡面 Forbidden**(53 只写世界流)⇒ 须**显式裁**,不能由实现顺手扩 | technical-director K-09 |

---

## 二、**分歧**与采纳(逐条给裁法与理由)

### D-1 「伤」支的 Kind 载体 —— **systems-designer 的主张被否**

| 方 | 主张 |
|---|---|
| systems-designer | 代价物化为**既有 `InjuryOnset`**,**写者归 25**;53 发 `ConsequenceResolved` 扩 `{consequence_kind=CostInjury, injury_id, severity_band}`,25 订阅后查自己的 `band → magnitude` 写 `InjuryOnset` |
| technical-director | 未直接提替代,但 K-11 指出病史流「有牙齿」的先例都是**施加方写、9 消费** |
| **用户裁定 ②** | **开伤支,新 Kind `TreatmentHarmOnset`**(写者 = 53) |

**驳回 systems-designer 的三条理由(编排方)**:

1. **载荷语义不合**。`InjuryOnset` 载荷 `{actor_id, target_id, injury_id, magnitude, dose_seq}` 的语义是
   「**被谁打的**」;药害**无攻击者**。这与 **ADR-030 驳回 Alt 2** 的判法是**逐字同型**
   (「会把病人出现伪装成有人打了他」)。
2. **会把 `band → magnitude` 表造在第二个真源上**。伤情词表归 **9**
   (`disease-simulation.md:671-672`「任何别的系统**不得自建病种表或伤情表**」)。
   systems-designer 把 `BAND_TO_MAGNITUDE` 明确判给 25(其 §四 槽 7)⇒ 25 将持有一张
   「档 → 分值」表 = 9 的词表的**第二真源**。ADR-035 把该换算判给 **9**(§Implementation 5)。
3. **他自己也承认该边不能私开**:「**这是新增的跨域边,不能由本表偷偷引入**」(其 §三)。
   用户裁定**不开**这条边。

> ⚠️ 顺带订正一处**出处归属**:systems-designer 写「25 的非致命模型现成」并引 `OQ-53-8`;
> 但 25 在**本路径上不参与**(无武器、无攻击者)。`OQ-53-8` 那句的意思是
> 「9 的注册表里已有非致命的伤情符号可直接引用」,**不是**「25 这个系统参与结算」。

### D-2 `INJ_COMA`(昏迷)是否排除 —— **两份专家意见与用户裁定相反**

| 方 | 主张 |
|---|---|
| systems-designer(N-2) | **排除**,但自陈「这是创作判断,不由我定」 |
| game-designer(G-07) | **排除**,理由「昏迷 = 事实死亡:倒地,出口只有苏醒 / 死亡;玩家对『躺着不动』与『走了』感知等价」 |
| **用户裁定 ④** | **不排除,昏迷合法** |

**处理**:ADR-035 §Risks **R3** 如实登记「与两份专家意见相反」,并把残余风险落成 **O-1**(9 侧须为
药源性昏迷给出时长上界或状态机出口约束)。**ADR 不推翻该裁定,只登记风险。**
⚠️ **O-1 今天未核实**(9 是否已有此约束)—— 在它落实前,**不得声称「永不作死亡代价」已完整成立**,
它只覆盖「直接致死」,未覆盖「长期昏迷」。

### D-3 「过量」到底有没有材料 —— **systems-designer 输,且实况比 game-designer 说的更糟**

| 方 | 主张 |
|---|---|
| systems-designer | 给了 `OVERDOSE_RATIO_CUTS[]` 槽(「剂量 / `dose_range.max` 比值切点」),归 11 / 21a 求值 |
| game-designer | 9 的过量为**饱和截断** ⇒ 字面零负面后果;53 只能看到「吃过药」,看不到「过量了」 |
| **编排方复核结论** | **game-designer 对,且不止于此** —— 见下 |

**实测(本人独立复核,非转述)**:

1. F1 式体 `disease-simulation.md:785-791` 四项 = `Base ⊔ Relapse` + 共病加成 + 药物项(clamp) + 战伤项;
   **零毒性项**。`:349` 校验 15:「超限 = **饱和截断(clamp),不拒收**」⇒ 过量 = **no-op**。
2. **更糟的一层**:`dose_range.hi` 在 11 侧是**戥子的物理限位**
   (`prescription-and-medication.md:318-323`,2026-09-18 R-4 改判「不得静默 clamp」)
   ⇒ **11 的 P0 面根本不产生「超了多少」这个值**。
   ⇒ systems-designer 的 `OVERDOSE_RATIO_CUTS` 槽**不是「没接线」,是「没有输入源」** ——
   比「欠一笔接线」严重一档。

**落点**:ADR-035 **§⑥ 过量支挂起** + 债册 `TD-sim-02`(9 缺毒性项)/ `TD-prescription-09`(11 缺禁忌命中;
且**不建议**为超量补 `dose` 字段,见该条 C-5′)。

### D-4 「后遗」有没有载体 —— **不是分歧,是两个层**

systems-designer 说「后遗已有载体(`Residual`)」;game-designer 说「效果层无」。
**准确表述**:事件层**有**(`53_outcome_table` 今天已在产 `Residual`),世界效果层**无**
(53 的 `ConsequenceResolved` **P0 零订阅者**,systems-designer R-13 / game-designer G-16 同证)。
⇒ ADR-035 ④ 据此**不为后遗造第二载体**,也不把「后遗的牙齿」算进本件。

### D-5 「现在就建空表 + 门」—— **专家主张,用户裁定不建**

| 方 | 主张 |
|---|---|
| systems-designer §五 | **要建,但只建「空表 + 门」**:表文件(空 `rows`)+ schema 校验器 + N-1…N-4 + 4 条变异;理由「空表 = 惰性,零回归,`AC-5` 首次取得宾语」 |
| **用户裁定 ①** | **本轮立 ADR 钉形状,不做实现** |

**如实登记**:用户裁定**未给理由**;本报告**不代拟理由**。
**可复核的后果**:`ADR-035 §Validation` **V-1…V-8 全部未跑**,`AC-5` 的表级半边**仍 PARTIAL** ——
**形状已裁 ≠ 判据已过**(ADR-035 §Status 首段已明写)。

---

## 三、用户四项裁定 → ADR 落点

| # | 裁定 | ADR-035 落点 |
|---|---|---|
| ① | 立 ADR 钉形状,不做实现 | §Decision ⑦ / §Migration Plan(全未落盘)/ §Status |
| ② | 伤支开,新 Kind `TreatmentHarmOnset` | §Decision ④(载荷六项)+ §Decision ⑤(53 写者面扩张) |
| ③ | 立两条上游债,过量支挂起 | §Decision ⑥ + 债册 `TD-sim-02` / `TD-prescription-09` |
| ④ | `INJ_COMA` 不排除,昏迷合法 | §Risks R3 + O-1(残余风险与缓解义务) |

---

## 四、编排方的复核台账(区分「我复核过」与「转述」)

| 断言 | 来源 | 我是否复核 | 复核命令 / 结果 |
|---|---|---|---|
| F1 无毒性项 | game-designer G-03 / systems-designer R-3 | ✅ | `sed -n '782,795p' design/gdd/disease-simulation.md` ⇒ 四项,零毒性项 |
| 过量 = 饱和截断不拒收 | 同上 | ✅ | `grep -n "校验 15"` ⇒ `:349` / `:877`,原文「饱和截断,不拒收」 |
| 伤情词表归 9(铁律二) | technical-director K-12 | ✅ | `disease-simulation.md:671-672` 逐字 |
| 11 载荷七项 | systems-designer / game-designer | ✅ | `prescription-and-medication.md:968` + `HistoryPayloads.cs:227-241` 结构逐字段 |
| `drug_potency` = 剂量修正后的值 | **我新查的**(两份专家均未查到这一层) | ✅ | `prescription-and-medication.md:197` |
| `dose_range.hi` = 物理限位 | **我新查的** | ✅ | `:318-323` |
| Kind 现役 37 支 | technical-director K-14 | ✅ | `grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml` ⇒ 37 |
| codec 硬编码「37 支」 | technical-director K-13 | ✅ | `SimEventCodec.cs:84`;扩查得生产码共 **8 处** ⇒ 债册 `TD-persist-11` |
| 53 只写世界流(卡面 Forbidden) | technical-director K-09 | 转述 | 未逐字回读 `ConsequenceEmitter.cs:93` 的完整 remarks;ADR-035 ⑤ 实现轮同批改该文本 |
| `Outcome53` 构造器拒结局轴 | systems-designer R-4 | 转述 | 本会话早期已复核(`:239`),本轮未再跑 |
| `DelayTable` 恰 3 行 / 缺行 ⇒ 静默跳过 | systems-designer R-7 | ❌ 未复核 | 其自陈「取自 story-003 `:182-183` 的自述,未在代码中亲自复验」 |
| `compounds[]` 能否绕到致命病 | systems-designer N-4 | ❌ **未普查** | ADR-035 §Risks **R2** 已把它登记为「必要性未证」;实现轮 S1 须先普查 |
| 25 的非致命模型 `LethalFor` | systems-designer R-9 | 转述 | 未回读 `:1156-1161` |
| 42 / 44 是否真订阅 `ConsequenceResolved` | game-designer G-16 | ❌ 未复核 | 其自陈「未独立验证」 |

---

## 五、未及核查项总表(三份自陈,逐条留档 —— 禁借绿)

| 来自 | 项 |
|---|---|
| systems-designer §六 | ① 未读 `DelayTable53.cs` / `ConsequenceEmitter.cs` / `TrialHistoryPredicate.cs`(其「缺行 ⇒ 静默跳过」取自 story 自述)<br>② 未核查 21a 的 `contraindications[]` **是否有程度字段**(若无,槽 #4 与禁忌档切点须 21a 修订)<br>③ 未核查 `compounds[]` 是否能使非致命伤通向致命病<br>④ 未核查 `DelayTableBinder.AssertDomainDeclarationsMatch` 在新增行时是否额外转红<br>⑤ 未核查 `ConsequenceResolved` 载荷 schema 现状<br>⑥ **未做任何变异实验**(报告内「须配变异」是**要求**,不是已完成的证据)<br>⑦ 未验证 `OQ-53-9` 试药史短路与代价路径的干扰(短路在 `Judge.Evaluate` 首行) |
| technical-director | ① 未跑任何测试 / 构建 / 烘焙;未打开 `DelayTableBinder.cs` / `DataBakeMenu` 的实现体<br>② 未核查 `assets/data/_ordinal_baseline.json` 的 ordinal 基线机制<br>③ 未核查 9 侧是否已有消费「病史流新 Kind」的 D 集重建入口 |
| game-designer | ① 未读 `unity/Assets/Scripts` 实现(全部依据 GDD 规格)<br>② 未独立验证 42/44 是否真订阅 `ConsequenceResolved`<br>③ 未核查:9 的迁延 / 昏迷停留上界 · 25 的 `HALF_LIFE_MAX` · 13 是否读后遗 · `PatientLoses` 与死亡是否同义 · CAP=24 与永久病人占槽的相互作用 |

> ⚠️ 其中 **game-designer ③ 的「迁延 / 昏迷停留上界」与 ADR-035 的 O-1 直接相关** ——
> 它是 O-1 的**前置未决项**,不是可选项。

---

## 六、结账表(Phase 8b 双列:已落 / 已证)

| ID | Landed(已落) | Proven(已证) | Branch(分支覆盖) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| D-1 | ✅ | ✅(仅「方向已裁」) | — | `adr-035…md` §④/§⑤ · Alternatives 1 | 三支候选逐条驳回理由在 ADR 内;`InjuryOnset` 载荷字段见 `HistoryPayloads.cs`(实证语义不匹配) |
| D-2 | ✅ | ✗ | — | `adr-035…md` §Risks R3 / O-1 | 用户裁定 ≠ 已证;**O-1 的前置(9 的昏迷停留上界)未核查** ⇒ 不得标已证 |
| D-3 | ✅ | ✅ | — | `adr-035…md` §⑥ · 债册 `TD-sim-02` / `TD-prescription-09` | 本文件 §四 的 7 条复核命令(均本轮实跑) |
| D-4 | ✅ | ✅ | — | `adr-035…md` §④ | 「事件层有 / 效果层无」两半各有一处实测(R-13 零订阅者 + `53_outcome_table` 已产 `Residual`) |
| D-5 | ✅ | **N/A** | — | `adr-035…md` §Status / §Validation | 用户裁定不实现 ⇒ 无可证对象;**V-1…V-8 全未跑** |

> ⚠️ **D-2 的 `Proven = ✗` 是诚实状态,不得补成 ✅**:用户已裁定「昏迷合法」,但「昏迷不会退化成
> 事实死亡」这件事**没有任何夹具**,且其前置未核查。按 §Phase 8b「多分支判定而 Branch 缺分支 ⇒
> 不得标已证」同族处理。

---

## 七、本件**不**主张

| 项 | 状态 |
|---|---|
| `TD-medcons-15` 已结案 | ❌ **未结** —— 只结**形状半边**;实现半边卡 `TD-sim-02` + `TD-prescription-09` |
| `AC-5` 已转绿 | ❌ **仍 PARTIAL** —— 表级半边无载体(V-1…V-8 全未跑) |
| 「永不作死亡代价」已完整成立 | ❌ **只覆盖「直接致死」**,未覆盖「长期昏迷」(O-1 未落实) |
| N-4 是一条有效的门 | ❌ **必要性未证**(`compounds[]` 未普查);若确无该形态,须登记为「零输入可达的门」 |
| 三路专家意见互相独立且无盲区 | ❌ **两面**:①三份都**没查到** `drug_potency` = 剂量修正后的值(编排方补查,并据此订正 ADR C-5);②三份都**未做**变异 / 未跑任何构建 |
| 草稿增量落盘纪律已实践 | ❌ **三份均为一次性收口**,S13-6 收窄判据本轮**未被真正检验**(见首段) |
