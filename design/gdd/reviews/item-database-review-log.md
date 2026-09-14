# 评审记录 · 系统 21a《物品与配方数据库(契约层)》

| 字段 | 值 |
| ---- | ---- |
| 目标文档 | `design/gdd/item-database.md`(修订前 432 行 · 8 节齐全 + Visual/Audio · UI · Open Questions) |
| 评审日期 | 2026-09-14 |
| 评审轮次 | **首轮**(`design/gdd/reviews/` 此前只有 8 与 9 的记录) |
| 模式 | `/design-review` **full**(七名专家并行 + 高阶综合) |
| **裁决** | **NEEDS REVISION** |
| Scope Signal | **M/L** —— 9 项 blocking 横跨数据形状、定点边界、公式、AC 可测性四层,连带新立 **ADR-006** |

## 参评专家

`game-designer`(幻想/锚点)· `systems-designer`(公式/边界值)· `technical-director`(定点/ADR 边界)· `unity-specialist`(引擎/序列化)· `qa-lead`(AC 可测性)· `economy-designer`(配方经济)· `creative-director`(高阶综合)

## 完整度

**8/8 节齐全**,另有 Visual/Audio、UI Requirements、Open Questions 三节;本轮新增 **§Schema** 与 **§Debt Register**。

## 依赖图

- ✓ `skill-system.md`(30)—— 常量 `SKILL_CAP` 已闭合
- ✓ `ADR-005`(Accepted)—— 但**它自称定义了本系统赖以生存的定点域,却未定义域边界**
- ✗ 九条下游 GDD 全部不存在(11 处方 · 12 药物槽 · 16 中药选项 · 17 采集 · 18 炮制 · 19 制作 · 20 库存 · 42 拟物 UI · 7a 持久化)—— 设计顺序上属预期
- **笔误**:头声明「零依赖」措辞不准确 —— `entities.yaml` 登记的是**两条入向**(`SKILL_CAP` 与 9 的伤情枚举),原文自述「唯一入向是 9」与自己的依赖表矛盾。已改为「零**工作流**依赖」

## 必改(blocking 9 项)

| # | 发现 | 来源 |
| --- | --- | --- |
| 1 | **契约层没有契约** —— 文档自称「21a = 契约 / Schema」,却**没有一处列出完整 schema**;`gather_profile` / `tcm_profile` / `ItemInstance` / `Recipe` 的形状散落在散文里,下游无法照写 | systems · technical |
| 2 | **品级没有机制出口,且 F2 是阶跃函数** —— `quality` 只调 F1 的产量与 F2 的保值,**全是数量性的**;§Player Fantasy 锚点二在机制上落空。且 F2 在 `SKILL_CAP = 60` 上只落 2 档(`Q = 1` 时品级零效果) | game · systems |
| 3 | **`float` 直通 ADR-005** —— `drug_profile` 的 `Offset` / `τ_half` 未定类型;数据文件一旦写浮点,定点域**从边界处漏空**,而域内所有守护照常通过 | technical · unity |
| 4 | **F1 的四个修正项只有名字没有公式** —— `SkillMod` / `QualityMod` / `EquipMod` / `EnvMod` 全文无定义,求解器无法实现 | systems |
| 5 | **「顺序无关」无前提** —— F1 断言「相加后乘消除叠加顺序依赖」,但那**只在定点域成立**,文档未写此前提 | systems · unity |
| 6 | **`weight` 浮点累加会翻转阈值** —— 20 的负重判定随累加顺序翻转,是「同一存档两样结果」的典型来源 | systems |
| 7 | **无守恒律** —— 配方可产出多于投入,「转化是有代价」没有机制落地,也无任何校验拦「凭空造物」 | game · economy |
| 8 | **确定性 AC 是同义反复** —— 「连续调用十次结果一致」对纯函数**没有任何实现能让它失败**;它证明不了 ADR-005 想要的跨编译器确定性。**与 8 的 AC-8-1 是同一缺陷** | qa |
| 9 | **AC 体系不可执行** —— 无编号(无法在 review log / bug 单引用)· 无 BLOCKING/ADVISORY 分级 · **「校验拒绝」类 AC 只验合法值,非法值无人证明会被抓** | qa |

## 推荐修改

- `processing_state` 以 int 编码持久化的静默重映射风险(应**存稳定字符串名**)
- `quality_distribution` 的支撑须显式约束于 `[1, MAX_QUALITY]`(17 ↔ 21 双向耦合未登记)
- **命名铁律自毁且越权** —— ✅ 列的「麻黄碱」本身含「麻黄」二字;且「判定法」不可判定(未指名哪部药典、哪版)
- **黄铜天平越权** —— 21a 是数据层,却为 42 指定了呈现装置;且重量单位未定,是为不存在的数值设计容器
- `entities.yaml` 的 `output_range` 两条笔误(F1 下界 `0.0` 与自身 clamp 矛盾;F2 上界 `MAX_QUALITY` 与自身 clamp 矛盾)
- 重量「存储单位 vs 展示单位」未分离;数值冻结令缺失
- 缺 §Debt Register —— 下游九条边的欠账无处登记

## 专家分歧与裁决

| 争点 | 一方 | 另一方 | creative-director 裁决 |
| --- | --- | --- | --- |
| `entities.yaml` 的 `output_range` 笔误是否 blocking | systems-designer:算 blocking(数据源错不可接受) | — | **降为登记笔误** —— 不改裁决空间,但**必须修**,已在同轮修 |
| 品级出口取「药效整数档位」还是「`min_quality` 闸门」 | creative-director:推荐 `min_quality` 闸门(实现成本低) | game-designer:档位更能支撑锚点二 | **用户裁定取「药效整数档位」**(F5)—— 用户选了**回报**而非**取舍**;`min_quality` 保留为**准入闸**,两者理由不同,不可互相替代 |
| F2 阶跃函数的严重性 | systems-designer:blocking | — | **成立但非 blocking** —— 已登记批评与修法(EFF 曲线接管技能主出口),数值层留待用户调 |

## 成熟度判断

> **不是支柱破裂,是「契约层没有契约」。** R1 复合主键、`drug_profile` 可空扩展块、通用 `inputs[] → outputs[]` 配方三点判断准确;但「21a 是 Schema 文件」这句自述与正文内容**不符** —— 它写了规则,没写 Schema。加上定点边界敞开、品级无出口、AC 不可执行,共 9 项 blocking。

## 处理

用户裁定(2026-09-14):**全修 9 项 blocking + 五项设计裁定** · 新立 **ADR-006** · 本记录落盘 · 同步 `systems-index` §4/§10/§11 与 `entities.yaml`。

## 修订落盘(2026-09-14,同日)

**9 项 blocking 已全部兑现 + 五项用户裁定已并入正文。** 逐项落点:

| # | 落点 |
| --- | --- |
| 1 | **新增 §Schema A–F** —— `ItemDef`(11 字段)· `drug_profile`(含 `half_life` 标为 9 的 `τ_half` 来源)· `gather_profile` · `tcm_profile`(P0 空)· `ItemInstance`(**闭合纯 POD,无 `UnityEngine` 类型**,容器 = 子实例 id 列表)· `Recipe` |
| 2 | **新增 F5 品级 → 药效** —— `Offset_effective = Offset_base + offset_by_quality[quality − 1]`,**整数档位**;并补 `EFF = EFF_MIN + (EFF_MAX − EFF_MIN) × (S / SKILL_CAP)` 接管技能主出口;F2 阶跃批评与修法已登记 |
| 3 | **D-21-9 整数化** —— F1/F2/F4 出参 `int`;`Offset` / `τ_half` 为 Q16.16 整数字面量;**契约落 ADR-006**(§Decision 一/二/三) |
| 4 | **F1 补四式** —— `SkillMod = cap × Level / SKILL_CAP` 等;并补 `Σ(正的 cap) ≤ QTY_MULT_MAX − 1` 校验 |
| 5 | **F1 补前提** —— 「顺序无关**仅在定点域成立**」写明;AC-21a-4 用打乱施加顺序验证 |
| 6 | **`weight` 改 `int`** —— 最小单位个数;**数值冻结令**(单位未定前 21b 不得填值);AC-21a-15 |
| 7 | **新增规则八 守恒律** —— `Σ outputs ≤ Σ inputs × EFF_MAX` 且 `EFF_MAX ≤ 1`;AC-21a-39/40 |
| 8 | **组四重写** —— 金标准哈希比对 + **IL2CPP vs Mono 逐位实测**(AC-21a-29)+ **全中间变量无 float 静态扫描**(AC-21a-30) |
| 9 | **AC 全量重写** —— **49 条**、**编号 + `[L]/[I]/[V]/[U]/[D]` 图例 + BLOCKING/ADVISORY**、**负向夹具铁律**(`tests/unit/item_database/fixtures/invalid_*.json`)、十组分组、欠账 owner 检查(AC-21a-49) |

**五项用户裁定的落点**:①命名铁律 **移入 21b**(本文件只留指针)· ②品级出口 **取药效整数档位**(F5)· ③守恒律 **硬约束 `EFF_MAX ≤ 1`**(规则八)· ④**新立 ADR-006**(已 Accepted)· ⑤21b 考据成本 **并入 §范围债**(D-21b-3,已同步 `systems-index.md` §4)。

**同轮并入的推荐项:**
- `processing_state` 跨持久化**只用稳定字符串名**(D-21-13 · AC-21a-26)
- `quality_distribution` 支撑 ⊆ `[1, MAX_QUALITY]`(F3 注 · AC-21a-14)
- **§命名规范指针**替代原 §命名铁律;21b 须落定三件事(药典版本 · 药品名豁免 · 保留词表)
- **删除黄铜天平装置指定** —— 抽象为「重量须拟物呈现、不得数字 HUD」的上游约束
- **§UI Requirements 新增 U-1…U-4 呈现契约**(品级禁数字化 · 禁线性刻度 · 重量禁 HUD · 同 base 不同 state 须可区分)
- **修正 `entities.yaml`** 两条 `output_range` 笔误;登记 **F5 公式**与 **13 个新常量**(含 `ROUND_MODE`)
- **新增 §Debt Register**(D-21-6 … D-21b-3 + D-9-D/E/B 全登记)

**本次新立的 ADR**(用户裁定④):
- **`ADR-006 定点域边界数据契约`** —— **Accepted 2026-09-14**。
  补齐 ADR-005 未定义的**域边界**:唯一解析入口 `FixParse`(拒浮点字面量)·
  存档与事件流禁 `float` · 单一舍入模式 `ROUND_HALF_AWAY_FROM_ZERO`(禁 `Math.Round` 默认 ties-to-even)·
  守恒律的整数域内表达。并以两条修正案回填 ADR-005 的自相矛盾:
  **Amendment A(D-9-D)** `SimEvent` 补 `Seq` + `Payload` —— ✅ 已定,ADR-005 正文已加前向指针;
  **Amendment B(D-9-E)** `patient_id` 跨权威稳定性 —— ✅ **同日已裁**:取机制 A「计数器 + 高水位可重构」
  (计数器永不复位 0 · 迁移后 `next = max(patient_id) + 1` 由事件流重构 · **终态折叠行必须保留 `patient_id`**)。
  备选 B(纯哈希派生)**已否决** —— 它要求「出生上下文」唯一,而唯一就必然含序号,等于把计数器藏进哈希输入。
  **复核副产品**:ADR-005 原折叠元组 `(onset, 病种_id, patient_seed, outcome, t_end)` **漏了 `patient_id`**,
  而折叠丢弃流位置、`patient_seed` 又是单向哈希 ⇒ 高 id 病人全折叠后高水位**不可重构** ⇒
  **静默 id 复用 ⇒ 两病人共写病程**。该失败不崩溃、不回放失配,**只表现为「病程悄悄变了」**。
  已就地补上该字段(ADR-005 `Implementation Guidelines` 第 5 条 + Validation Criteria)。

**未做(留待后续):**
- **跨文档三笔**:`D-9-D` ✅ 已由 ADR-006 Amendment A 兑现 · `D-9-E` ✅ **同日已裁** —— 用户取机制 A
  「计数器 + 高水位可重构」,并在复核中**新发现 ADR-005 折叠元组漏掉 `patient_id`** 的静默漏洞(已就地修正);
  残留实现项归 7a / 45 · `D-9-B`(神经衰弱史实出处 —— 世界构建审计确认**非时代错误**,
  缺口是可引用的中文一手来源,考据,**不得凭空落盘**)
- `D-8-7`(`TICK_SECONDS` 数值)仍由 9 持有定值权,未办
- 21a 的 21 条负向夹具**尚未实际编写**(AC 已要求,夹具是撰写后的工作)
- **AC-21a-29 的 IL2CPP 逐位实测**未做 —— ADR-005 自述「需实测」,**未过则 21a 不得签署 Determinism 结论**
- 21b 的考据量**仍未估算**(D-21b-3)
- 九份下游 GDD(11 / 12 / 16 / 17 / 18 / 19 / 20 / 42 / 7a)仍不存在 —— D-21 系列待它们落盘时回收

**结论:21a 由「规则文档」进为「契约文档」**,§Schema 补齐后下游可照写;但**门控项未清**(21 条负向夹具 · IL2CPP 实测 · D-21-11/12),故 **In Review,不得标 Approved**。
**建议下一步**:先裁 **D-9-E**(它是唯一还堵着系统 9 的债),或按 §6 设计顺序启动 **3 输入与设备**。

---

## Review — 2026-09-14 — Verdict: NEEDS REVISION
Scope signal: M/L
Specialists: game-designer · systems-designer · technical-director · unity-specialist · qa-lead · economy-designer · creative-director
Blocking items: 9 | Recommended: 7
Summary: 21a 自称「契约 / Schema」全文却无一处完整 schema,加上定点边界敞开、品级无机制出口、AC 体系无编号无分级无负向夹具,共 9 项 blocking。裁定后全部落盘,并新立 ADR-006 补 ADR-005 未定义的定点域边界(含两条 ADR-005 修正案)。门控项(21 条负向夹具 / IL2CPP 逐位实测 / D-21-11·12)未清前不得标 Approved。
Prior verdict resolved: First review

---

## Review — 2026-09-14(二轮)— Verdict: MAJOR REVISION NEEDED
Scope signal: L
Specialists: game-designer · systems-designer · technical-director · unity-specialist · qa-lead · economy-designer · creative-director ·(高阶综合 Opus · TD 裁决 Haiku)
Blocking items: 10 | Recommended: 6
Summary: 首轮修订**自身引入了新缺陷** —— ①「非零产出」不变量为**假**(`Round(1 × 0.3) = 0`),且已冻结进 `entities.yaml`;② 首轮为修 F2 阶跃而引入的 `EFF` 是**死变量**(只进构建期检查,运行期无人消费);③ 新 AC-21a-41 把 `weight` **误列为 `Fix`**;④ F5(锚点二的修法)**不 deliver 幻想** —— 它调的是药效**幅值**,一个可被玩家换算成数值的维度。三条设计轴经用户裁定后全文大修。
Prior verdict resolved: Partially — 首轮 9 项 blocking 全部落盘,但其中 3 项(伪不变量 / 死变量 / F5 失效)是新引入的缺陷

### 二轮 blocking(10 项)与落点

| # | 发现 | 落点 |
| --- | --- | --- |
| 1 | **F1 非零不变量为假** —— `Round(1 × 0.3) = 0`;且错误断言已冻结进 `entities.yaml:156-157` | 结构化 `OutputQty = max(1, Round(...))`(D-21-1 重写);`entities.yaml` 的 `output_range` / `notes` / `QTY_MULT_MIN.constraint` 就地修正;AC-21a-1 重写 |
| 2 | **`EFF` 是死变量** —— 首轮把它设为技能主出口,却只写进构建期守恒检查,F1/F2 运行期均不消费 | **投入端可变**(D-21-15):`ActualConsumed = Ceil(base / EFF)`;`inputs[].qty` 明确为**基数**;AC-21a-52 守住「实耗只走历史事件,不落静态数据」 |
| 3 | **`weight` 口径错** —— 新 AC-21a-41 把它列入 `Fix` 解析集,实为 `int` 计数 | D-21-17;AC-21a-41 删除 `weight`、改列 `axis_offset_by_quality[]`;§Decision 二 增补 |
| 4 | **`weight` 累加量纲不齐** —— 守恒律 `Σ outputs ≤ Σ inputs × EFF_MAX` 把不同重量的条目直接相加 | 守恒律改 `Σ(weight × ·)`(D-21-19③);AC-21a-39 / Rule 8 重写;ADR-006 §Decision 四 修正 |
| 5 | **`Fix` 不可经 Unity 序列化器承载** —— 原 ADR-006 `:124` 断言「`Fix` 序列化为内部 `long`」为假;`private readonly long _raw` 会被内置序列化器**静默归零** | D-21-18;ADR-006 新增 **§Decision 五**;AC-21a-53(EditMode 序列化探针) |
| 6 | **F5 不 deliver 锚点二** —— 调药效**幅值**,是可被玩家换算成数值的维度 | **换轴为时间轴 / 作用点**(D-21-14):`Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]`;组六 AC 全重写 |
| 7 | **AC-21a-37 自毁** —— 「全零 ⇒ 通过」等于给「幻想落空」盖章 | 改为「**至少一档非零,且作用于非产量轴**」 |
| 8 | **品级缺**「身份」**载体** —— 只有档位数值,没有可供 42 呈现的定性标签 | 新增 `quality_character[]`(D-21-16),string[],长度 = `MAX_QUALITY`;新增 U-5 与 AC-21a-54 |
| 9 | **AC 不可执行处** —— AC-21a-3 写「`ΣM = … − ...`」**省略号非合法表达式** | AC-21a-3 补全为上界式 `Σ(正的 cap) + max(0, ENV_MOD_MAX) ≤ QTY_MULT_MAX − 1` |
| 10 | **AC-21a-38 越权** —— 断言「极性不变 且 `τ_half` 不变」,而这两个字段**不归 21a 所有** | 改为「仅 `quality_axis` 所指那条轴偏移,**其余三条时间轴不变**」 |

### 二轮推荐项(6 项,已并入)

- 依赖表补 **6 世界与生态区**(F3 的 `ecosystem` FK)与 **52 随机事件导演**(F4 的负重)—— 两条此前**未登记**
- `ENV_MOD_MAX` 若为正须计入 `QTY_MULT_MAX` 约束(AC-21a-9 / Tuning Knobs)
- `QTY_MULT_MIN` 安全范围放宽至 `≥ 0`(非零性已由 `max(1, ·)` 结构性保证)
- §Edge Cases 校验族补:`axis_offset_by_quality[]` / `quality_character[]` 长度;`ActualConsumed` 误入静态数据
- §Schema E 补**递归类型图断言**(禁 `[SerializeReference]` / 接口 / 抽象字段)
- §Debt Register 补 **D-21-19**(ADR-006 修正案待撰写)/ **D-21-20**(`Fix` 序列化编码器待实现)

### 二轮用户裁定(三项设计轴)

| # | 争点 | 裁定 |
| --- | --- | --- |
| ⑥ | F5 作用轴 | **时间轴 / 作用点**(`onset`/`peak`/`half_life`/`elimination` 之一),**不再调药效幅值** |
| ⑦ | 技能回报落在哪一侧 | **投入端可变**:`Recipe.inputs[]` 是基数,结算按 `EFF` 实耗 —— **技能高 = 省料**,`EFF` 由此从死变量变为运行期出口 |
| ⑧ | 品级的「身份」怎么落 | 新增 **`quality_character[]`**(每档一个定性修饰),由 42 以**外观 / 药签措辞**呈现 |

### 二轮综合判断

> **首轮修好了「契约层没有契约」,但没修好「锚点二在机制上落空」,反而在修的过程中注入了三个更深的缺陷。**
> 三者共同点是**静默**:伪不变量不会让任何测试变红(它只是**不成立**);死变量不报错(它只是**没人调用**);
> F5 换不换轴,AC 都过(因为「全零 ⇒ 通过」替它背书)。
> 本轮的三条设计轴裁定都指向同一个方向:**让机制里那件事真的发生** —— 非零产出要结构性成立、
> 技能回报要在运行期被消费、品级要在时间轴上被感到。

**结论:21a 仍 **In Review,不得标 Approved**。**新增门控项:AC-21a-53(`Fix` 序列化探针)+ D-21-19 / D-21-20。
**建议下一步**:先清 D-21-19(ADR-006 三处修正案已在本轮**就地落盘**,仅余状态回填),
再按 §6 设计顺序启动下游系统。

---

## Review — 2026-09-14(三轮)— Verdict: MAJOR REVISION NEEDED
Scope signal: L
Specialists: game-designer · systems-designer · technical-director · unity-specialist · qa-lead · economy-designer · network-programmer · creative-director(高阶综合)
Blocking items: 9 | Recommended: 6
Summary: 性质**从「轴错了」变为「兑现没做完」** —— 三轮**不动任何已裁轴**,收敛到「冻结裁决表跑在可执行体前面」。最重的一条:守恒律**构建期上界漏产出侧 `QtyMultiplier`**(5/7 专家命中),于是构建期 9≤10 通过、运行期 18>10 击穿(凭空造物),且同一旋钮 `QTY_MULT_MAX` 上被两条互相冲突的约束夹住而只有一条写下来。其余:§Schema 不含自身公式变量(`BaseQty`/`Offset`)、AC/注册表/门控三方漂移、`instance_id` 权威缺契约(ADR-005 的 `IIdAuthority` 只有 `PatientId Next()`)、锚点二/三无可感知层(F5 偏移小于 9 的噪声带 = 未发生,而成药侧摸不到 `gather_profile`)。三条设计轴**均未再动**。
Prior verdict resolved: Partially — 二轮 10 项落盘,但其中「F5 换轴」在换的过程中**误删了 9 点名的 `Offset` 来源**(21a↔9 契约断裂),三轮补回并改名 `drug_potency`

### 三轮 blocking(9 项)与落点

| # | 发现 | 落点 |
| --- | --- | --- |
| 1 | **守恒律构建期上界漏产出侧乘子** —— 原式两侧基数,运行期 `QtyMultiplier` 可击穿(反例 `w_in=10,w_out=9,EFF_MAX=1,QTY_MULT_MAX=2`) | **D-21-21**(两侧都封):规则八 / F1 重写构建期上界为 `QTY_MULT_MAX × Σ(w×产出基数) ≤ EFF_MAX × Σ(w×输入基数)`;AC-21a-8/39 重写;`QTY_MULT_MAX` 旋钮行补两侧约束;`entities.yaml` F1 notes 同步 |
| 2 | **§Schema 不含自身公式变量** —— `Recipe.BaseQty` 不在 Schema F(F1 却用它);`Offset` 不在 Schema B(规则九/AC-41 却引它);`stackable` 被消费却无定义;容器子 id 列表要求但 Schema E 缺字段;AC-50 指错块 | F1 改**逐条 `outputs_i.qty`**;Schema B 补 **`drug_potency`**(D-21-22);Schema A 补 `stackable`(派生,不存储);Schema E 补 **`children: long[]`** + 容器细则;AC-50 **拆为两条**(`drug_profile.axis_offset_by_quality[]` / `gather_profile.quality_character[]`) |
| 3 | **AC/注册表/门控三方漂移** —— AC-8 旧无权重式;AC-9 缺 `max(0, ENV_MOD_MAX)`;AC-37 与 Tuning Knobs 矛盾;门控列 D-21-9 为「未清」而其标 ✅;`entities.yaml` 的 F2/EFF 注释与 `quality_axis` 作用域漂移 | AC-8/9/37 重写;门控结论重列;`entities.yaml` F2/EFF notes 改加权式、`quality_axis` 由「全案单值」改**逐条药物** |
| 4 | **`instance_id` 权威缺契约** —— ADR-005 的 `IIdAuthority` 只有 `PatientId Next()`;主机迁移后计数器复位 ⇒ **静默重号**(两件物品共用一个 id,不崩溃,只表现为「悄悄合并/丢失」);规则九主机唯一清单**漏了 17 采集** | 新增 **D-21-26** · **D-21-27**;Schema E + 规则九补指针;新增 **AC-21a-63** |
| 5 | **锚点二/三无可感知层** —— F5 偏移无地板(小于 9 噪声带 = 未发生);成药侧摸不到 `gather_profile`;`ActualConsumed` 是不可见税;F5 无域钳制(负 half_life → 9 的 Decay 除零) | **D-21-24**:新增 `drug_quality_character[]` + **U-6** + **可感知地板**;F5 补域钳制(AC-21a-38b);Tuning Knobs 补地板旋钮 |
| 6 | **整数域修正曲线退化为阶跃** —— `cap × Level / SKILL_CAP` 在 `int` 域对 `Level < 60` 恒为 0 | F1 补「修正曲线须在 Q16.16 原始整数域求值」条;`entities.yaml` F1 notes 同步 |
| 7 | **`EFF_MIN ≤ 0` 无人守** —— `EFF` 是 F1 的除数,`0` ⇒ 除零,`< 0` ⇒ 实耗为负 | §Edge Cases 三轮族 + **AC-21a-56**;`entities.yaml` `EFF_MIN` constraint 改正 |
| 8 | **AC-53 把引擎缺陷当通过条件** —— 「探针断言字段丢失/归零」若 Unity 某日修好,该 AC **因引擎变好而失败** | AC-53 **保留但改型**:断言「自定义编码器往返正确」;内置序列化器行为降为**观察记录**(creative-director 裁定:不删,改型) |
| 9 | **AC-28 金标准自指** —— 「首次通过后冻结」⇒ 任何实现首次跑都能冻结成金标准 ⇒ 恒过 | AC-28 改为「金标准须由独立参考实现/手算产出、经评审签字」,参数表须**枚举边界组合** |

### 三轮推荐项(6 项,已并入)

- F1 补**多产出逐条**说明(`QtyMultiplier` 配方级、`max(1,·)` 逐条)
- §Schema F `outputs[]` 注由 `Recipe.BaseQty` 改 `outputs_i.qty`
- §Edge Cases 补:`drug_potency`/时间轴字段浮点守门、容器闭包、`stackable` 派生性
- §Debt Register 补 **D-21-25**(9 侧字段名对齐 `drug_potency`/`half_life`)/ **D-21-28**(Craft 事件总序键)
- Tuning Knobs 补 `drug_potency` / `drug_quality_character[]` / `F5 偏移可感知地板` 三行;`EFF_MIN` 下端硬约束注
- `entities.yaml` 新增 `drug_potency` / `drug_quality_character` / `F5_PERCEPTIBILITY_FLOOR` 三常量

### 三轮用户裁定(四项)

| # | 争点 | 裁定 |
| --- | --- | --- |
| ⑨ | 守恒律口径 | **两侧都封(严格质量守恒)** —— 产出侧含 `QtyMultiplier`,构建期上界显式写出 |
| ⑩ | `Offset` 归属 | **21a 拥有、改名 `drug_potency` 入 Schema B** —— 它是 9 处置事件 `Offset` 的唯一来源 |
| ⑪ | F5 可交付轴 | **P0 收窄为 `half_life`** —— 其余三轴在 9 的载荷无落点,产出即被丢弃 |
| ⑫ | 锚点二感知层 | **补成药标签(`drug_quality_character[]`)+ 可感知地板** —— 偏移须 > 9 的噪声带 |

### 三轮综合判断

> **本轮与前两轮性质不同。** 首轮是「契约层没有契约」,二轮是「修的过程中注入了更深的缺陷」,
> 三轮**不动任何已裁轴** —— 它攻击的是**执行的一致性**:冻结的裁决表(§D-21-x / §Tuning Knobs / §AC / `entities.yaml`)
> **跑在可执行体(§Schema / §Formulas / §Edge Cases)前面**。最刺眼的一例是守恒律:
> 它同时被两条方向相反的约束夹住(「抬 `QTY_MULT_MAX` 让高投入有回报」vs「守恒律禁止凭空造物」),
> 而**只有前一条被写下来**;于是文档在构建期自洽、在运行期破裂。
> 五项 blocking 中的四项(①②③④⑤)都是同一个形状:**一处声明、另一处漏改**。
> 这解释了为什么前两轮修完仍不能签 —— 不是判断错,是**判断没有传导到底**。

**结论:21a 仍 **In Review,不得标 Approved**。**新增门控项:D-21-26 / D-21-27 / D-21-28 + AC-21a-53(改型后)+ AC-21a-56…62。**
**建议下一步**:下一轮复核应以「**逐条核对冻结裁决表 ↔ 可执行体**」为唯一靶子(而非再找新缺陷);
若 21a 再不能签,应考虑**冻结 §D-21-x 表本身**(不再增删),只在可执行体侧收敛。
