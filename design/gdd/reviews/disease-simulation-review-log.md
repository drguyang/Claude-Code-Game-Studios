# 评审记录 · 系统 9《疾病与伤情模拟》

| 字段 | 值 |
| ---- | ---- |
| 目标文档 | `design/gdd/disease-simulation.md`(935 行 · 8 节齐全 + Visual/Audio · UI · Open Questions) |
| 评审日期 | 2026-09-14 |
| 评审轮次 | **首轮**(`design/gdd/reviews/` 此前只有 8 的记录) |
| 模式 | `/design-review` **full**(七名专家并行 + 债务审计 + 高阶综合) |
| **裁决** | **MAJOR REVISION NEEDED** |
| Scope Signal | **XL** —— 12 项 blocking 横跨数学、确定性、数据模型、跨系统契约、AC 五层,连带 ADR-005 与 8 两份文档回改 |

## 参评专家

`game-designer`(玩法/幻想)· `systems-designer`(公式/边界值)· `network-programmer`(确定性/全序)· `unity-specialist`(定点/引擎)· `qa-lead`(AC 可测性)· `performance-analyst`(复杂度)· `narrative-director`(支柱二/《他回来了》)· 债务审计子代理 · `creative-director`(高阶综合)

## 完整度

**8/8 节齐全**,另有 Visual/Audio、UI Requirements、Open Questions 三节超额;本轮新增 **§Debt Register**。

## 依赖图

- ✓ `skill-system.md`(30)· `diagnosis-system.md`(8)· `random-events.md`(52)—— 三条已闭合
- ✗ `prescription-system.md`(11)· 病人 AI(13)· 医馆即机器(24)· 格斗(25)· 公卫(34)· 病例(37)· 医疗后果(53)—— **七份下游 GDD 不存在**(设计顺序上属预期)
- ✗ **`7a` 持久化服务无 GDD** —— 9 的唯一硬上游,契约由 9 侧定义(五个抽象点)
- **笔误**:头声明「下游(8)」漏列 53 —— 已补

## 必改(blocking 12 项)

| # | 发现 | 来源 |
| --- | --- | --- |
| 1 | **F1 `Base` 默认分支未定义** —— `self_limit=false ∧ plateau=false` 时 `τ≥τ_peak` 无分支;上升段在 `τ_peak` 跳变,AC-6 为假 | systems |
| 2 | **F4 判定顺序退化** —— 按「潜伏→危殆→昏迷→死亡」判使昏迷/死亡成**死代码**;plateau 下 `position≡1` 使死亡不可达;过度治疗可把 plateau 压成痊愈 | systems · game |
| 3 | **`SimEvent` 装不下全序与载荷** —— 无 `Seq`、无 `polarity`/`Offset`/`τ_half`,与规则六元组对不上 | network · unity · systems · qa |
| 4 | **「CatchUp ≡ Step 逐位相同」不成立** —— 两条不同函数,违反规则五 | network |
| 5 | **定点 `exp` 是最大实现空洞** + Q16.16 下 `trend` **灾难性抵消**(恰在心衰段) | unity |
| 6 | **多病种/多伤情无合成;`compounds[]` 无触发语义** —— 即 **D-8-6**,用户已裁 P0 阻塞,9 却未提 | game · systems · network · narrative |
| 7 | **对因/对症在 9 侧不闭合** —— `treatable_by[]` 无极性、事件 schema 无极性,而 F1/F4 全靠它 | systems · game |
| 8 | **死亡窗口只认对因,七病里只有疟疾有对因** ⇒ 致死病越阈必死;痢疾标「可致死」却与 `self_limit=false` 打架 | game |
| 9 | **潜伏期 `Progress = Noise` 可为正** ⇒ 8 侧读到非中性体征,破 AC-22 | systems |
| 10 | **阻塞账零登记** —— grep 全文 `D-8-x` 命中 0;D-8-6 用户已裁 P0 阻塞而文档未提 | qa |
| 11 | **AC-6 与 F1 矛盾必挂 · AC-15 与叠加冲突 · AC-25 不可自动化** | qa |
| 12 | **AC-21「五通道没有第六条」与 D-8-2 正面冲突** | qa |

## 推荐修改(10 项)

写入期校验缺口(`SCALE>0` / `≥A_peak` / `τ_fall>0`)· 哨兵语义未定义 · 复发×终态交互 · F3「好转/迁延分界=导数零点」不存在 · F4 非因果读未来 · 复发边界数随时长线性(performance-analyst 判为**最锋利**)· 成本模型错位 · `ε_PRIME`/终态折叠 · 下游表漏 53 · 无 BLOCKING/ADVISORY 分级

## 锦上添花

`Project` 三重载改名 · AC-9 与 F3 索引统一 · 常数表 `1/2` 语域统一 · 34 聚合「冻 seam 不冻内容」

## 专家分歧与裁决

| 争点 | 一方 | 另一方 | creative-director 裁决 |
| --- | --- | --- | --- |
| 神经衰弱「不可做」 | game-designer:态机无位置 | 8 的裁定① | **夸大** —— 缺的是 R3 第 8 条数据(D-8-1),不是态机排斥;补一行豁免类即可 |
| 两份 GDD「互相否定」 | narrative-director:D-8-1 ↔ R3 判据互否 | 8 的裁定① | **半成立** —— 是「待办」非「否定」,新增豁免类解决 |
| ADR 验证判据对 P0 即不通过 | performance-analyst | 文档自述 | **成立且最锋利** —— 复发边界数随时长线性,`DIS_MALARIA` 已在 P0 |
| Q16.16×Q16.16 需 96 位装不进 long | network-programmer | F0 | **误报** —— 混淆中间表示与存储类型;但「未写明中间类型」是真缺口,已补 |

其余:systems-designer 与 game-designer 对「昏迷矛盾是否 blocking」分歧,总监判**同根两面,数学层与设计层各算一条**。

## 成熟度判断

> **不是支柱破裂,是规格未完成。** 规则一/二/五/六/七与 ADR-005 咬合紧密,R3.4 的医学诚实性是全文最见功力处;**但有三处「按现文不可实现」**(F1 默认分支 / F4 顺序 / SimEvent 全序)。8 的问题是「可裁决但未可实现」,9 更深一档。

## 处理

用户裁定(2026-09-14):**全修 blocking 12 项** · 四项设计裁定 · 本记录落盘 · 同步 `systems-index` §10/§11。

## 修订落盘(2026-09-14,同日)

**12 项 blocking 已全部兑现 + 四项用户裁定已并入正文。** 逐项落点:

| # | 落点 |
| --- | --- |
| 1 | **F1 重写** —— 上升段加归一化 `R_rise = 1 − e^(−(τ_peak−incubation)/τ_rise)`(消 τ_peak 跳变)+ 补第三分支「急性保持型 `Base ≡ A_peak`」;`Decay` 加 `[Δ≥0]` 门 |
| 2 | **F4 按严重度降序重写**(死亡→昏迷→危殆→潜伏→其余)+ `position_agg` + `DEATH_THRESHOLD<1` 入校验 |
| 3 | **`SimEvent` 改为 `{Tick, Patient, Seq, Kind, Payload}`**;事件元组必带 `polarity`/`Offset`/`tau_half` |
| 4 | **规则一定义 `Step ≡ CatchUp(patient, t, t+1)`**;F3 剪枝加「整带同侧才跳」;AC-3 重写 |
| 5 | **F0 明列 `Exp` 入库**(250–400 行,并撤「200 行」自述);F2 `trend` 在 Q32.32 求差 + `TREND_EPS` 死区;状态转移改用 `trend_backward` |
| 6 | **新增规则十 + F5**(`position_agg = max_d position_d` + `compounds` 显式加成);Edge Cases 改写;**D-8-6 登记并办结** |
| 7 | **R1 `treatable_by[]` 增 `polarity` 字段**;规则三补 blk #7;R3.2/R3.3 逐条标注(毛地黄明定**对症**) |
| 8 | **铁律一限定适用范围** + 「无对因手柄 ⇒ 越阈必死」写死;R3.3 逐病种标 `极性/尾部形态/lethal`;AC-30 |
| 9 | **F1 潜伏期抑制噪声**(`τ<incubation ⇒ Progress ≡ 0`)+ Edge Case + AC-26 |
| 10 | **新增 §Debt Register**(D-8-1…7 + D-9-A…G 全登记);立「上游须显式登记 D 项」的流程规矩 |
| 11 | **AC-6/AC-15/AC-25 重写**;AC-3/4/5/10/18/20/23 修订;新增**组七** AC-31…37 |
| 12 | **撤销「没有第六条」**(Visual/Audio §四 + AC-21)—— 通道集合归 8 拥有,**D-8-2 已办** |

**四项用户裁定的落点**:①共病合成 **取大 + 显式加成** → 规则十/F5 · ②神经衰弱 **新增豁免类** → R3.6 + `DIS_NEURASTHENIA`(第 8 位,触满上限) · ③**接受越阈必死** → 铁律一注 + AC-30 · ④**数值全冻结** —— 全文数值仍 `*待定*`,未动一个。

**连带修正(复核推荐项中已做的部分):**
- R1.3 写入期校验 **3 条 → 10 条**(补 `SCALE>0 ∧ SCALE≥A_peak` · `A_peak>0` · `self_limit⇒τ_fall>0` · `DEATH_THRESHOLD<1` · `compounds` 无环且目标存在 …)
- 三条铁律重写(铁律二限定为**病人**而非玩家;铁律三拆开复发 `<->` 新病种)
- AC 立 **BLOCKING/ADVISORY 分级**([L]/[I]/[V] 图例)
- 边界数线性问题入 **§Debt Register** 与 Tuning Knobs(`boundary_mode` 8 项)

**未做(留待后续):**
- **跨文档三笔**:`D-9-D` ✅ **已由 ADR-006 Amendment A 兑现(2026-09-14 同日)** ·
  `D-9-E` ✅ **已裁(机制 A「计数器 + 高水位可重构」,ADR-006 Amendment B);
  ADR-005 `:126-127` 与折叠规则已就地修正,残留实现项归 7a / 45** ·
  `D-9-B`(神经衰弱史实出处 —— 支柱五,**不得凭空落盘**)仍未办
- `D-8-7`(`TICK_SECONDS` 数值)仍由 9 持有定值权,未办
- `entities.yaml` 等价性登记(AC-25 / D-9-G)
- 锦上添花层(`Project` 改名 · 索引统一 · 语域统一)未在本轮
- 七份下游 GDD(11 / 13 / 24 / 25 / 34 / 37 / 53)仍不存在 —— D-9 系列待它们落盘时回收

**结论:9 由「可裁决的设计」进为「可实现的规格」**,但**三笔跨文档欠账未还**(ADR-005 / 8 / 史实),在 D-9-D 落盘前 9 不得进 Implement。
**建议下一步**:优先还清 **D-9-D**(它 gate 三份下游)与 **D-8-7**(数值),再启动 11 或 25。

> **✅ 追记(2026-09-14 同日)**:上段的两条前置**已办** ——
> **D-9-D 与 D-9-E 由新立的 `ADR-006`(Accepted)兑现**(Amendment A / B);
> ADR-005 的 `SimEvent` 形状与 `patient_id` 迁移不变量**均已就地修正**。
> 复核中还**新发现一个静默漏洞**:ADR-005 的终态折叠元组漏了 `patient_id`,
> 会使高水位不可重构 ⇒ id 复用 ⇒ 两病人共写病程。已补。
> **9 的 Implement 门已解除**;余欠 **D-9-B**(史实出处)与 **D-8-7**(数值)。

---

## Review — 2026-09-14 — Verdict: MAJOR REVISION NEEDED
Scope signal: XL
Specialists: game-designer · systems-designer · network-programmer · unity-specialist · qa-lead · performance-analyst · narrative-director · 债务审计 · creative-director
Blocking items: 12 | Recommended: 10
Summary: 规则骨架与 ADR-005 咬合紧密、R3.4 医学诚实性是全案最强项,但三处「按现文不可实现」(F1 默认分支 / F4 判定顺序 / `SimEvent` 全序)使其停留在「可裁决的设计」而非「可实现的规格」。裁定后 12 项 blocking 与四项用户裁定已全部落盘,9 升入可实现态;三笔跨文档欠账(D-9-D/B/E)未还前不得进 Implement。
Prior verdict resolved: First review

---

## Review — 2026-09-16 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: creative-director(opus 综合) · 议题 A–F 分裁 · 既有 review-log 对照
Blocking items: 0 新(集群 A–F 六项裁决) | Recommended: 3
Summary: 二轮重评审对照已采纳的 ADR-005…021 与三流架构复查 #9。**无支柱破裂、无架构裁决被推翻** —— 规则十 / F5 / 三条铁律 / R3.4 的骨架成立,裁决定位「补界与事件」而非重写求值器。六项用户裁定全部照准:B′ 照护 = 玩家主动护理动作(暂停语义收窄,不死场消失)· 照护事件化落病史流 `CareApplied`(ADR-021 式窄修正)· 剂量超限 = 饱和截断(clamp,不拒收)· 疟疾保留 lethal 删校验 11 · 豁免类永不入痊愈(feature)· 伤寒名保留「伤寒」。A–F 六集群已全部落盘(GDD 正文 + entities.yaml 注册表)。数值仍全 `*待定*`,一个未动(硬约束)。三轮重评审已启动以验证闭合。
Prior verdict resolved: Yes(首轮 MAJOR 的 12 项 blocking 均已在首轮修订兑现;本轮为二轮裁决的修订后复审)

---

## Review — 2026-09-16 — Verdict: NEEDS REVISION(三轮重评审)
Scope signal: L
Specialists: game-designer · systems-designer · network-programmer · performance-analyst · qa-lead · unity-specialist(×3 报告,均 haiku 并行)· creative-director(opus 综合) · 四项用户裁决
Blocking items: 8 集群(blocking 15 + REC 若干,按集群合并)| Recommended: 见集群内部
Summary: 对抗性验证二轮修订是否真正闭合(非背书)。creative-director(opus)终裁 **NEEDS REVISION** —— 确认三项二轮回归(clamp 上界误罩整式 · AC-30① 与 F4 冲突 · 旧痊愈门漏洞未修)+ 五处新洞(compounds 零登记 / 不可愈病种可被压痊愈 / 128 位中间表示 / 事件身份稳定派生键 / 性能联合约束)。**8 阻塞集群全部修订落盘**:① F1 Σ 语义 + clamp 层次 + ε_OFFSET 顺序 · ② 护理时间语义(派生 last_intervention + TW≥CARE_GAP 排序校验) · ③ compounds 双 Kind 登记 `entities.yaml` + `compound_max_triggers` 边界 · ④ 可愈性结构门(校验 16 + AC-32 子句 + σ 痊愈门联动) · ⑤ 128 位中间表示 + DTO/facade 移边界 asm + IL 拒集扩展 · ⑥ AC-15 稳定派生键 `(patient_id, 处置_id, 施予者, tick)` + AC-16 Append 顺序即权威 · ⑦ 性能联合不等式 + `scan_step` + `CARE_EVENTS_PER_TICK` + 预载 handle 常驻 · ⑧ AC 层清理(AC-31 字面量 / AC-36 Folded(p) 归 7a / AC-1 移除 Gate / AC-25 手算锚点 / AC-3c 分治 / AC-34 分治)。
**四项用户裁决(2026-09-16,均照准并落盘)**:① **P0 只建模「重症」** —— 100% 病死率 = P0 抽象级别产物,病历/叙事侧用「重症入院」口径(加 R3.3 注 + 文档口径,不加机制) · ② **通道 = 整数位域**(9 登记通道位 / 8 读取 / 13+44 呈现,AC-21 补 [L] BLOCKING 子句) · ③ **treatable_by 粒度 = 病种×处置**(二元关系,gate 语义,无贡献但记录为有效动作) · ④ **新旋钮三件套纪律**(Tuning Knobs 行 + F3 类 A/复杂度行 + AC,缺一不进注册表;立为 Debt Register 纪律)。
**待四轮验证闭合** —— 本轮修订需一轮对抗性验证(在修订后的全文上重裁),不自我背书。
Prior verdict resolved: Yes(二轮集群 A–F 六项裁决已落盘;本轮为新裁决的修订后复审)

---

## Review — 2026-09-16 — Verdict: NEEDS REVISION(四轮重评审)→ **用户接受修订并标记 Approved(免五轮验证)**
Scope signal: L
Specialists: game-designer · systems-designer · network-programmer · performance-analyst · qa-lead · unity-specialist(均 haiku 并行)· creative-director(opus 综合) · 两项用户设计裁决(K7/K8)
Blocking items: 8 阻塞簇 K1–K8(blocking 15 + REC 11,按簇合并)| Recommended: 11
Summary: 对抗性验证三轮修订闭合。creative-director(opus)终裁 **NEEDS REVISION** —— **K1**(sys MAJOR-1)F1 Σ 单向上界 = 死旋钮,过度治疗 Σ<0 时截断永不触发 ⇒ 对称 clamp `±(MAX_ACTIVE_DOSE×single_dose_max)` 只罩 Σ · **K2** 护理时间语义与 AC-30① 逐字矛盾 ⇒ 派生式 `last_intervention(照护) = max(止, 最后动作 + CARE_GAP)` + 排序校验 `TW ≥ CARE_GAP` · **K3** compounds 边界离线不可求 ⇒ 无噪声包络闭式自举 + `TENTATIVE` 标记 · **K4** AC-15 四元组与同 tick 多剂冲突 ⇒ 5 元组去重键补 `dose_seq` · **K5** 自限型上升段 `position` 尚低可「病刚发就判愈」⇒ 痊愈非上升期门 · **K6** signed long 带进位加法 = signed overflow = UB ⇒ hi/lo 两 `ulong` 128 位 + IL 类型谓词拒集 · **K7**「这病我有没有牌」无接口(用户裁定「补接口」)⇒ `handle` 枚举派生字段 + §Interactions 接口铁律四 + AC-43 · **K8** 破伤风照护与医学诚实冲突(用户裁定「收窄照护范围」)⇒ 护理暂停 = {伤寒 / 痢疾 / 心衰} 3 种,破伤风护理不刷窗口,手柄 = 发病前清创。**8 簇 + 11 项推荐全部修订落盘**;数值仍全 `*待定*`,一个未动(硬约束)。
**两项用户设计裁决(2026-09-16,均照准并落盘)**:① **K7 补接口** —— 9 侧添加可干预性真值(每病种 `handle` ∈ {causal / symptomatic_only / care / none},构建期从 `treatable_by` 极性派生防静默撒谎),8 侧设门槛呈现「这病我有没有牌」 · ② **K8 收窄照护** —— 护理暂停仅限 {伤寒 / 痢疾 / 心衰} 3 种,破伤风护理不刷新死亡窗口(与对症药同级);病史注:1900–1930s 伤寒护理 / 补液确有降死亡率证据,破伤风照护则无(医学诚实性)。
**旋钮三件套收尾(四轮)**:`scan_step` / `CARE_EVENTS_PER_TICK` / `ε_MIN` / `channel_mask` / `compound_max_triggers` 补登记 `entities.yaml`(此前仅 GDD 出现);AC 侧补载(AC-30 密度界子句 · AC-23 ε_MIN 必含 · AC-3c CAP 子句)。
**用户裁定接受修订并标记 Approved,免五轮验证** —— 追踪记录已更新(systems-index row 9 → Approved;本 log 追加四轮条目)。
Prior verdict resolved: Yes(三轮 8 阻塞集群已落盘;本轮为新裁决的修订后复审 → **结案 Approved**)
