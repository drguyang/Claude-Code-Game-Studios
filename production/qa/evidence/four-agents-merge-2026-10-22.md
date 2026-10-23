# 第四代理并行轮 · 合并报告(2026-10-22 第七轮)

> **本文是什么**:A / B / C / D 四个并行代理的勘察与落地报告**合并件**,
> 加上**编排方(主对话)对每条关键断言的独立核实**。
>
> **原文去哪了**:四份 `tmp-*-draft.md` 是过程草稿,**保留在盘上**(见下方 🔁 订正)。
> **代理的原话逐字保留**,编排方的核实另起「✅ 已核 / ❌ 证伪 / ⚠️ 部分」标记,
> 两者不混排 —— 代理的自述不等于已核实的事实。
>
> **落盘时刻** = 2026-10-22 21:0x(见 §七 回归三元组:最终判据 = **round23**,
> `4614 / 4578 / 0 / 36` @ `20:59:32 (+0800)`)。

---

## 零、四份草稿的去向

| 草稿 | 代理 | 主题 | 去向 |
|---|---|---|---|
| `tmp-care-writer-draft.md` | A | `CareAppliedWriter` 落地 | §一(结论)+ 草稿保留(锚点) |
| `tmp-w-trend-eps-draft.md` | B | `W` / `TREND_EPS` 数据管道 | §二(结论)+ 草稿保留(锚点) |
| `tmp-f5-survey-draft.md` | C | F5 多病聚合阻塞面勘察 | §三(结论)+ 草稿保留(锚点) |
| `tmp-care-phase-draft.md` | D | `CareApplied.phase` 序数定义权 | §四(结论)+ 草稿保留(锚点) |

> 🔁 **2026-10-22 订正:原声明「已合并进本文后删除」= 失实,未执行,且不应执行**
> (原文不删,此处保留原判的推翻理由)。
>
> **原计划**(承 `code-review` skill Phase 7b ③「草稿随后删除,不留两份并行的分析段」)
> 是:合并完成后删掉四份草稿。**删前实测比对**,结论是**不能删**:
>
> 比对法 = 抽草稿里两类高信息量 token(① `*.cs|md|yaml|json|py` 路径 + 行号锚点
> ② 两位以上数字),逐 token 查是否在本文中出现:
>
> | 草稿 | token 总数 | 本文**缺席** |
> |---|---|---|
> | `tmp-care-writer-draft.md` | 102 | **48** |
> | `tmp-w-trend-eps-draft.md` | 42 | **14** |
> | `tmp-f5-survey-draft.md` | 100 | **59** |
> | `tmp-care-phase-draft.md` | 73 | **38** |
>
> 缺席的不是无关噪声,而是**细粒度 `file:line` 锚点** —— 例如 C 的草稿列了
> `EmergencyTreatmentApplied` / `DrugTreatmentApplied` 两个载荷的**全部 ~20 处构造点**
> (`aggregate_stream_test.cs:79,106` · `medcons_aggregation_test.cs:83,93` ·
> `sim_codec_roundtrip_test.cs:85,99` · `vitals_chain_test.cs:86,97` ·
> `drug_event_test.cs:586,618,626,671` · `medcons_outcome_judge_test.cs:75` ·
> `f4_wiring_test.cs:95,235` …),而本文只写了「~20 处构造点」这个**计数**。
> 将来 F5 路径一旦裁出「加 `disease_id` 字段」,**这 20 处就是改动清单** ——
> 删掉草稿 = 把清单换成一句计数,须重新勘察一遍。
>
> ⇒ **本文是「判定 + 核实」的摘要级合并件,草稿是「锚点清单」的勘察级记录**,
> 两者**不是两份并行的分析段**(前者不重复后者的清单,后者不含前者的核实标记)。
> Phase 7b ③ 的删除条款针对的是「同一份分析的两次誊写」,本组不属该形态。
>
> ⚠️ **本文上方「代理的原话逐字保留」一句同样偏强** —— 实际是**结论与关键理由**逐字,
> **清单类内容只留计数**。以草稿为准的场合:凡需要**逐处改动清单**时。

---

## 一、A · `CareAppliedWriter` 落地

### 1.1 交付清单

| 文件 | 性质 | 内容 |
|---|---|---|
| `unity/Assets/Sim/CareAppliedWriter.cs` | **新建** | sealed class,`IEventSink` + `IPayloadEncoder` + `IPayloadDecoder` + `IReadOnlyList<SimEvent>` + `careEventsPerTickCap` 构造注入;结果码 enum **七值**;载荷走 `_encoder.Encode(EventKind.CareApplied, payload)`(零 `new PayloadRef(`);事件头 `Seq = -1`;`NoDensityCap = -1` 哨兵 |
| `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs` | 改 | 撤 `CareApplied` 豁免条目 + 撤表注释 |
| `unity/Assets/Tests/EditMode/Sim/writer_existence_gate_test.cs` | 改(**任务外但必须**) | `:186-187` 原以 `CareApplied` 作「陈旧豁免」探针样本 ⇒ 撤表后必红且红的理由与被测判据无关 ⇒ 改点名仍挂表的 `CompoundTriggered` |
| `unity/Assets/Tests/EditMode/DiseaseSimulation/care_applied_writer_test.cs` | **新建** | **10 个 `[Test]`** |

### 1.2 六个前置条件与出处

| 前置 | 出处 | 编排方核实 |
|---|---|---|
| ① 病人合法(非 `None`) | ADR-006 Amendment B + ADR-007 §四(高水位不得被 None 污染);GDD 无直接条文,属流不变量 | ✅ |
| ② `phase ∈ {0,1}` | `entities.yaml:2186`「phase: 枚举(起 止)」——**序数全库无定义**;0/1 出处 = `DiseaseCourse.CarePhaseStart/Stop`(写者**引用**常量,不复制第二份) | ✅ 见 §四 |
| ③ `actionId ≥ 0` | GDD `:344`「`K ≤ \|护理动作表\|`」;上界无表可查 | ✅ 全库无护理动作表本体 |
| ④ `caregiver ≥ 0` | **全库无任何定义**(无枚举 / 无表 / 无上界) | ✅ |
| ⑤ 密度界 | GDD `:1784` + `entities.yaml:1776`「每病人每 tick 护理动作**事件数**上界 … 超界拒收(丢弃 + 记账),不静默」;值 `*待定*` | ✅ **A 担心的 `:1031` 冲突不存在** —— 原文就是「护理动作事件数」,与 A 的口径一致 |
| ⑥ 止须有起 | GDD `:344`「`t(止) ≥ t(起)`」 | ⚠️ **「配对」是 A 的推导,不是 GDD 明写** —— 见 1.4 |

### 1.3 编排方独立核实的三条(A 自己标为「未验证」)

**❌ A 的疑点 2.2 之一「`Clear()` 可能让视图变陈旧」—— 证伪。**

A 担心 `EventStream.Clear()` 若写成 `_events = new List<>()` 而非 `_events.Clear()`,
则构造期捕获的 `Events` 视图会**静默变陈旧**,密度界与配对扫描同时永不命中,
且**测试发现不了**(`Setup` 每次新 `Assemble`)。

实测 `unity/Assets/Sim/EventStream.cs:164-173`:

```csharp
public void Clear()
{
    _events.Clear();          // ← 原地清空,不是重新赋值
    _dedupKeys.Clear();
    _lastSeqTick = -1;
    ...
}
```

⇒ **视图不会陈旧**。A 标为风险的这条不成立。**但 A 提出的「测试覆盖不到」这一点仍然成立**
(即便实现是对的,也没有测试钉住它)—— 归 §六 未做项。

**✅ A 的疑点 2.3「`:1031` 可能冲突」—— 不冲突。** 见上表 ⑤。

**⚠️ A 的疑点 2.1「前置 ⑥ 是推导」—— 成立,且需要用户裁。** 见 1.4。

### 1.4 ⚠️ 需要用户裁:前置 ⑥「止须有可配对的起」

A 的原话:

> GDD 全文没有一句说「没有起就不许有止」,也没有定义「起止如何配对」(按什么键配对、
> 是否跨 tick 可配、caregiver 是否入键)。⇒ 我的 ⑥ 是**把「比较式无从求值」推成「拒收」**,
> 属**推导 + 一种取舍**,不是转录。
> 风险:若 24 的真实动作生命周期允许「只发止」(例如重连后补记 / 动作开始时玩家已离场),
> 本写者会拒掉合法写入,而**没有任何门会提醒**。

编排方核实:GDD `:344` 逐字只有「② **护理动作事件**:`t(止) ≥ t(起)`,`K ≤ |护理动作表|`」。
「配对」二字确实不存在。

**两案**:
- **保留 ⑥** ⇒ 须在 24 的 GDD 轮补一句「止须有起」,把它从推导升成规则;
- **降级为「不拒 + 记警告」** ⇒ 写者永不拒合法写入,但「止无起」会进流,`last_intervention` 的照护支可能取到一个孤立的「止」。

**未裁。代码现状 = 保留 ⑥(拒收,具名结果码 `StopWithoutStart`)。**

### 1.5 A 自查的其它可疑点(如实登记)

| 项 | 状态 |
|---|---|
| `actionId` 上界 | 全库找不到护理动作表本体(`clinic-machine.md` 只有一句上游契约)⇒ 只做 `≥ 0`。**若表存在于未扫路径,登记项就是错的** |
| `caregiver` 语义 | 是否 = `player_id` / 是否须共用 id 空间 —— **完全未查证** |
| 载荷 `Tick` 与事件头 `Tick` 双存 | registry 明列 `tick: i64` ⇒ 照登记落字段。**未改 registry** |
| 密度界默认不生效 | 旋钮值仍 `*待定*` ⇒ 默认 `NoDensityCap` ⇒ **⑤ 永不触发**。不应被读成「密度界已落地」 |
| 测试 namespace | 取 `DaYiJingCheng.Tests.Unit.Sim`(该目录两种 namespace 混存,取多数派) |

### 1.6 编排方补做的(A 未做)

- **两处失实注释**:`DiseaseVitalsService.cs` 原 `:365` / `:415` 仍写「写者未落 · WriterExistenceGate 具名登记」。
  写者已落、豁免已撤 ⇒ 该句失效。已按「原文不删,就地加注」改写为:
  **结论不变(恒 None),理由从「没人写」变成「没人调」**(生产触发点未接线)。
- **三处硬编码条数**:`WriterExistenceGate.cs` 类头与 `writer_existence_gate_test.cs` 三处写「24 条豁免」。
  **实测当前 = 16 条**(复算命令已写进类头注释)。其中一处「基线随之 24 → 23 条」**两个数字都错**
  (24 是 2026-10-10 的基线,中间已撤 CaseClosed / PatternRecognized / JudgmentRevised / SkillGrown)。
  已全部改为不写数字 + 说明「撤表是常态,硬编码必然过期」。

### 1.7 A 明确未做项(禁借绿)

- **A 自己没跑测试、没编译**(按指令不跑 Unity batch)⇒ A 交付时所有判据 = `NOT-RUN`。
- A 未跑构建前门 / 探针轮 ⇒ 它说的「门现在绿」是**源码级论证**,不是构建日志证据。
- 编排方已代为跑通(见 §七),但**探针轮仍未做** —— 见 §六。

---

## 二、B · `W` / `TREND_EPS` 数据管道

### 2.1 一句话结论(B 原话)

> **管道只通了 4 段中的 2 段。`W` / `TREND_EPS` 仍为 0 ⇒ 生产路径上痊愈支 / 迁延支仍不求值。**

### 2.2 改动清单

| 文件 | 改动 |
|---|---|
| `unity/Assets/Sim/RegistrySchema.cs` | 新增 `R1_CHECK_36` 常量 + `public static void ValidateTrendWindow(long, long)` |
| `unity/Assets/Editor.Tools.Bake/DiseaseRegistryBinder.cs` | `RootKeys` 2 键 → 4 键;`BindResult` 加 `TrendWindowTicks` / `TrendEpsRaw` 两字段 + 构造 3 参 → 5 参;`Bind()` 读两键并调 `ValidateTrendWindow` |
| `assets/data/disease_registry.json` | 顶层加 `"trend_window_ticks": 0` 与 `"trend_eps": "0"` |
| `unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs` | 加两 readonly 字段;构造签名尾部加带默认值的两形参 |

### 2.3 载体裁定:全局(不是每病种)—— B 自述「是推断,不是明文」

B 的依据(全部实测):
- GDD `:1564` 明写「本节是**全局旋钮** + 跨旋钮约束」;
- `W` / `TREND_EPS` 位于 §四「噪声与读数」`:1737-1744`,**不属** §二「病程曲线(**逐病种** · R1.3 schema)」;
- R1.3 字段表 `:316-332` 逐字段列了 `curve.*` / `relapse.*` / `scale` / `handle`,**没有** `w` / `trend_eps`。

⚠️ B 明写:「这是**从 GDD 结构推断**出来的,不是 GDD 明文写『全局』二字。」

### 2.4 ⚠️ `0 = 未配置` 是工程约定,GDD 无此条

B 原话:

> **GDD 对应条 = Tuning §四 `:1743-1744`;但「放行 0 作未配置哨兵」一条 GDD 无对应** ——
> GDD 的安全区间是 `W > 0` 且 `TREND_EPS > 0`。这是我在常量注释里明写的工程约定,
> 理由:数值轮未裁,不放行 0 则 JSON 必须填非 0 值 = 代拍数值。**该偏离应由用户追认。**

**编排方核实**:`F4OutcomeEvaluator.RecoveryWindowInput.IsConfigured => WindowTicks > 0 && TrendEpsRaw >= 0`
(`:91`)**本来就存在**,本来就以 `W == 0` 为「未配置」哨兵。⇒ B 的约定**不是新发明**,
是与既有实现对齐。但「对齐既有实现」不等于「GDD 授权」—— 该偏离仍待追认。

### 2.5 接线断在哪(B 自述 + 编排方核实)

链路:`JSON` → `BindResult` → `DiseaseVitalsService` → `RecoveryWindowInput` → 求值器

| 段 | 状态 |
|---|---|
| ① JSON → BindResult | **通** |
| ② BindResult → DiseaseVitalsService | **★ 断** —— `CompositionRoot.cs:182-183` 仍 `new DiseaseVitalsService(stream, blobPool, fallbackRegistry, worldSeed)`,不传两值 |
| ③ Service → RecoveryWindowInput | **通** |
| ④ → 求值器 | **通**(既有) |

**断点 ② 是结构性阻塞,两条独立理由**(B 原话):
1. `CompositionRoot` 走 `CreateFallbackRegistry()` —— 内联手写的 1 条病种,**完全不经过 `DiseaseRegistryBinder`**,运行时 `BindResult` 对象压根不存在;
2. `DiseaseRegistryCookedWriter.cs:48-65` 的 `WriteRow` 逐字段写死,**没有**这两个全局量的位置;文件头(`FinishHeader`,20 字节)也没有。补它须改 cooked 布局 ⇒ 撞 `CookedFormat.FormatVersion`,影响 `cooked_drift_all_artifacts_test` 等对拍。

### 2.6 ⚠️ B 挖出的、比任务书更严重的一条:`LowPositionHeldTicks` 恒传 0

B 原话:

> `RecoveryWindowInput` 第三参 `LowPositionHeldTicks` 我**恒传 0**。它不是配置量,是**跨 tick 状态**,
> 需要一个按病人保存的累计器,而全库没有。后果:`F4OutcomeEvaluator.cs:280` 的
> `if (window.LowPositionHeldTicks < window.WindowTicks) return null;` 在 `0 < W` 下恒真
> ⇒ **即使数值轮填了 W > 0,痊愈支仍不会成立,只有迁延支会生效**。
> 「数值一填即生效」对迁延支成立,对痊愈支**不成立**。

**编排方核实 ✅ 成立**:全库 `LowPositionHeldTicks` 只有三处 ——
`F4OutcomeEvaluator.cs:78`(声明)/ `:84`(赋值)/ `:280`(判据),加上
`DiseaseVitalsService.cs:445-447`(传 0 的注释)。**没有任何地方递增它。**

### 2.7 B 的疑点 7:`W` 偶数守卫与消费者语义可能对不上 —— **成立**

B 原话:

> `ComputeTrendBackward` 实际用 `(P(t) − P(t−W))/W`,**不做 `W/2`**;
> 但 `W` 的「须为偶数」约束理由是「取 `W/2` 做中心差分」。
> ⇒ 我实现的偶数守卫与消费者实际语义**可能对不上**。

**编排方核实 ✅ 成立,并定位到根因**:

| 消费者 | 公式 | 用 W/2? | 现状 |
|---|---|---|---|
| `ComputeTrendBackward`(F4 吃) | `(P(t) − P(t−W)) / W` | **否** | **已接真**(2026-10-22 第四轮) |
| `ComputeTrend`(F2 展示用) | `(P(t+W/2) − P(t−W/2)) / W` | **是** | **tripwire 恒 0**(`ProgressionEvaluator.cs:570`) |

⇒ GDD `:1743` 的「须为偶数(取 W/2)」是为**中心差分(F2)**写的,而 F2 目前恒 0;
真正在跑的 F4 后向差分**不需要**偶数。**偶数守卫目前守的是一个不存在的消费者。**
这不是错(GDD 明文要求),但**不能读成「F4 需要偶数 W」**。

### 2.8 B 明确未做项(禁借绿,B 原话)

1. **一次都没编译。**
2. `BindResult` 构造从 3 参改 5 参,调用点未 grep 全。
3. `R1_CHECK_36` 编号未全库查重(只查了常量定义段的最大值)。
4. cooked 布局未改 ⇒ 烘焙产物丢值。
5. `CompositionRoot` 未改 ⇒ 生产路径上两值恒 0。
6. 痊愈支在 `W > 0` 后仍不成立(见 2.6)。
7. 测试 **0/3**。

### 2.9 编排方补做的(B 未做)

- **修 5 个编译错误**(B 自己预言了 2 个,实际 5 个):
  - **4 × CS0120**:`_trendWindowTicks` / `_trendEpsRaw` 是实例字段,却在 `private static EvaluateOutcome` 里直接用。
    修法 = **加两个形参**,不改方法静态性(静态是刻意的:放开它会允许求值体偷读服务状态)。
  - **1 × CS7036**:`BindResult` 3 参 → 5 参,构造点未跟。
- **补 3 条测试**(B 明确报「0/3 未跑」):
  - `test_trendWindow_absent_meansUnconfiguredZero` —— 两键可省 ⇒ 0
  - `test_trendWindow_present_isReadBack` —— **正向**:给 120 / "1966" ⇒ 原样读回
  - `test_trendWindow_outOfRange_isRejected` —— 负 W / 奇数 W / 负 TREND_EPS 各自拒
  - **⚠️ 这三条是编排方写的,不是 B 写的。** B 的交付里测试数为 0。
  - 三条一组的设计理由(缺一即留盲区):只测「可省」⇒「两键被整个忽略」与「正确读成 0」同效;
    只测「负值拒」⇒ 无法区分「真校验」与「任何非零都拒」。
    ⇒ 必须有「给非零值 ⇒ 读回非零」这条正向判据。
- **打通 `BakeOutput`**:B 加了 `BindResult` 两字段,但 `DiseaseRegistryBaker.BakeOutput` 未透传
  ⇒ 测试拿不到值。已加两字段(带默认值 0,不破既有构造点)并在注释里明写
  **「不透传进 cooked 字节 —— 产物布局没有这两个全局量的位置,这是已知缺口」**。

---

## 三、C · F5 多病聚合阻塞面勘察

> C 是**只读勘察**,未改任何仓库文件。

### 3.1 核心结论(C 原话)

> 「`DiseaseOnset` 单次」是 `DiseaseCourseBook.cs:56` 的**实现自造**,不是 ADR-030 的裁决
> (ADR-030 只裁 Kind 归属,全文零「多病 / 多次」字样)。它与 GDD **AC-31(BLOCKING·Gate C2)**
> 直接冲突。`compounds` **不是**多病聚合的替代 —— GDD `:1342` 明写加成「不是在聚合量上加」。
> **两者共用一个阻塞:档案簿的单 course 假设。**

### 3.2 阻塞面逐条(C 的表,编排方抽核三条)

| # | 问题 | 结论 | 证据 |
|---|---|---|---|
| 1 | 二次 onset 是否 fail-loud | **是**,抛 `InvalidOperationException`,理由串写「病人出现是单次事件(ADR-030 ① 甲案)」 | `DiseaseCourseBook.cs:55-57` |
| 2 | 档案簿索引结构 | `Dictionary<int, DiseaseCourse> _byPatient` —— 每病人**一条**;`DiseaseCourse` 单 `DiseaseId` | `DiseaseCourseBook.cs:32-34,59-60` |
| 3 | 求值侧 `D` 恒单元素 | 注释自陈:`D` 恒单元素集,`max_d` 退化恒等;刻意不调 `DiseaseAggregator.Aggregate`(避免「看起来已接线」) | `DiseaseVitalsService.cs:311-319` |
| 4 | `DiseaseAggregator` 本身 | **已完整实现且正确** —— 两个 max 独立求、argmax 并列取 `DiseaseId` 最小者、空集 `HasAny=false`。**唯一缺的是没人喂 >1 元素** | `DiseaseAggregator.cs:121-175` |
| 5 | 两个处置载荷不带 `disease_id` | **确认**。`EmergencyTreatmentAppliedPayload` 9 字段、`DrugTreatmentAppliedPayload` 7 字段,均无 | `HistoryPayloads.cs:170-208` |
| 6 | 处置归档按病人寻址 | `ApplyTreatment(PatientId headerPatient, ...)` 用 `Require(headerPatient)` 取**唯一** course | `DiseaseCourseBook.cs:66-76` |
| 8 | codec tag 表已满 | `Emergency` tag 1–9 = `(1u<<9)-1`;`Drug` tag 1–7 = `(1u<<7)-1`。加字段 = 新 tag 10 / 8 | `PayloadCodec.History.cs:307-395` |
| 13 | 有无「同病人第二种病」的 P0 路径 | **代码侧无**。`SpawnNext` 每次发**新** id,无「给已有病人加病种」的入口 | `PatientSpawner.cs:59-77` |
| 14 | 7a 折叠是否已支持多病 | **已支持** —— `HistoryFolder.Collect` 的 `onsets` 键是 **`DiseaseId`** 不是 `patient_id` | `HistoryFolder.cs:94-131` |
| 15 | ⚠️ 去重键口径冲突 | `entities.yaml` 的 `DiseaseOnset` 去重键写 **`(onset_tick, patient_id)`**;而 `HistoryFolder` 去重按 **`DiseaseId`**。**同一 tick 同时 onset 两种病会被前者拒、后者接受** | `entities.yaml:2460` vs `HistoryFolder.cs:110-112` |
| 16 | `compounds` 的 C# 落地 | **桩实现**。`ComputeCompounds` 从 `ActorCellEntered` 硬编码推 `patient_1`,忽略 `when`/`bonus`,无 `compoundMap` 真实数据源 | `CatchUp.cs:90-127` |
| 17 | `compounds` schema 落地 | **`DiseaseRegistryEntry` 无 `compounds` 字段** | `RegistrySchema.cs:172-235` |

### 3.3 `compounds` ≠ 多病聚合(C 的论证,编排方认可)

| 维度 | 多病聚合(`position_agg = max`) | `compounds` 显式加成 |
|---|---|---|
| 作用面 | **出口合成量**(9 的阈值判定 / 8 的读数) | **目标病种自己的 `Progress` 曲线** |
| 数学形态 | `max_{d∈D} position_d(t)` —— 取大,幂等 | 向 `Progress_{target} += bonus.Δ_progress` —— **加性阶跃** |
| 是否产生新病种 | 否 | **是** |
| 事件 | 无(纯求值,不进流) | `CompoundTriggered` / `CompoundExpired` |

GDD 原文两处钉死:

> 「**`compounds` 的加成是「向目标病种的 Progress 加一项」**,不是在聚合量上加 —— 这样共病恶化对
> **目标病种自己的曲线**可见,也保持了「取大」的语义。」 —— `:1342-1345`

> 「**为什么合成只在出口**:各 `d` 的病程**必须独立**(否则「治好了关节、心脏也跟着好」——
> 恰是 R3.4 明令禁止的医学错误)。**合成只发生在 ①8 的读数 ②9 的阈值判定两处。**」 —— `:1340-1342`

**⇒ `compounds` 不能替代多病聚合,也不隐含它。** 但两者有一个耦合点:
`compounds` 触发后 `target` 病种需要**进入 `D`** ⇒ 需要一条「给已有病人新增病种」的路径。
**这条路径同时是多病聚合的前置 ⇒ 两者的共同阻塞是同一个:档案簿的单 course 假设。**

### 3.4 ⚠️ AC-31 / AC-32 是假绿(C 实测 + 编排方独立复核)

| AC | 声称 | 实际测试体 | 判定 |
|---|---|---|---|
| **AC-31**(多病取大 · BLOCKING · Gate C2) | 「病人同时有 `3/10` 与 `4/5` 两个病,`position_agg = 4/5`」 | `catchup_replay_test.cs:66-79` —— **本地造 `List<Fix>{100,300,200}` 手写 for 循环取大**。零调用 `DiseaseAggregator.Aggregate`,零构造 `DiseaseCourseBook`,零 `DiseaseOnset` | **假绿** |
| **AC-32**(compounds 触发 · BLOCKING · Gate C2) | 风湿热持续 → `DIS_HEART_FAILURE` 的 `Progress` 出现 `bonus` 增量 + 落 `CompoundTriggered` | `catchup_replay_test.cs:81-99` —— `Assert.GreaterOrEqual(compounds.Count, 0)`(**恒真**)+ `Assert.IsNotNull(compounds)`(**恒真**,桩函数必返回非 null List) | **假绿** |

**编排方独立复核 ✅**:已直接读 `catchup_replay_test.cs` 源码确认两条。这是本项目已抓过的
「自造断言顶替 AC 语义层」形态在 F5 上的实例。

**⚠️ 必须精确区分两个层次**(C 原话):

- **单元层 = 真的、且测得好**:`DiseaseAggregator.Aggregate` 有 10 条真单测
  (`care_lever_and_aggregator_test.cs:130,144,156,157,168,180,191,199,209,218`),覆盖取大、
  argmax 并列取小 id、两个 max 独立、空集 / null。**这一层是绿的、可信的。**
- **集成层 = 假绿**:AC-31 的测试根本没走 `CourseBook → Aggregator` 这条路。
  ⇒ **未被验证的是「档案簿能产出 >1 元素」这件事**,而这恰是 F5 唯一的真缺口。

### 3.5 路径对比(C 不替用户选)

**路径 A:档案簿改多 course(最小可证)**
- 改动面:`DiseaseCourseBook` 的 `_byPatient` → `Dictionary<int, List<DiseaseCourse>>`;去重键改 `(patientId, diseaseId)`;`ApplyTreatment` 增靶病种参数;`DiseaseVitalsService.cs:311-319` 改为调 `DiseaseAggregator.Aggregate`
- **`DiseaseOnset` 载荷 / codec / entities.yaml:零改动**(载荷已带 `disease_id`)
- 权威件动作:GDD 补「`D` 如何增长」规则;`entities.yaml:2460` **订正去重键**;ADR-030 **加注不改写**
- 风险:低。不触 `Sim` 引用集 / 门 A / codec 字节格式
- **代价**:处置事件仍**无法路由到具体 course** ⇒ 路径 A 只解 `position_agg`,不解处置路由

**路径 B:路径 A + 两个处置载荷加 `disease_id`**
- 改动面:`HistoryPayloads.cs:170-208` 两 struct 加字段;codec 新 tag 10 / 8;~20 处构造点
- **风险:高,且有两处硬阻塞**(见 3.6)
- 字节格式变更 ⇒ 所有既有存档 / 黄金夹具失效(ADR-012 集成级字节夹具须重签 `golden-vN`)

**路径 C:仅解除 fail-loud(最小改动,不解决问题)**
- 只改 `DiseaseCourseBook.cs:55-57` 去掉抛
- **风险:高 —— 静默丢病种,且 `position_agg` 仍退化。这是假绿路径。不建议。**

**C 的判定**:**路径 A 是最小可证** —— 用一条 AC-31 真单测即可证伪/证实,且不动字节格式、不动抽象点、不动程序集边。路径 B 是 A 的必要后继,可拆为独立 story。

### 3.6 ⚠️ 路径 B 的两处硬阻塞(编排方独立复核 ✅)

**① `DiseaseNameForbidden` 逐字禁 `"DiseaseId"`**

`PrescriptionWriterGates.cs:68-82` 的词表**逐字**含 `"DiseaseId"` / `"DiseaseIdSet"`,
扫描面 = `PrescriptionNamespacePrefix`(**实现面,间接引用 / 只读一处也算**)。
出处 = `TR-prescription-001`「零 diagnosis / disease_id / tier_named 引用」+ 11 规则一。
⇒ 给 `DrugTreatmentAppliedPayload` 加 `DiseaseId`,11 侧 `PrescribeFlow` 构造它时即触红。
**这不是可以顺手绕的门 —— 它是 11 的设计原则:11 按症状开方,不按病种。**

**② `TenSideOnlyFields = { "Method", "Cause" }`**

`PrescriptionWriterGates.cs:96` 要求 11 / 10 两侧载荷**除这两项外字段名 / 类型 / 序数完全一致**。
⇒ 只给 10 侧加 `DiseaseId` 也会触红。

**⇒ 路径 B 须拆为 B1 / B2**:
- **B1(只解 10 侧)**:`EmergencyTreatmentAppliedPayload` 加 `DiseaseId`;须同步改 `TenSideOnlyFields` 为 `{ "Method", "Cause", "DiseaseId" }` 并写明理由。**这本身是一次对 11/10 载荷对称性的裁决,须 ADR 窄修订。**
- **B2(11 侧的替代路由)**:11 不加 `DiseaseId`,改用事件头 + 时间就近推断。**代价:路由是推断而非事实,与 AC-16「Append 顺序即权威」的确定性取向相悖。**

### 3.7 C 明确没查清的事(C 原话)

1. **GDD 未定义 `D` 如何增长** —— 谁在什么条件下为已有病人发第二条 `DiseaseOnset`?52 的疫情事件?`compounds` 触发时自动建?**全库未找到答案。这是最大的缺口,路径 A / B 都依赖它。**
2. `compoundMap` 的真实数据源 —— `ComputeCompounds` 的参数无任何生产调用点。
3. ADR-032 已预设逐病种终态(`:178`「每病人每病种至多一次终态」)—— **这是多病为设计预期的强旁证**,但未通读全文确认它是否也预设了多 `DiseaseOnset`。
4. 测试构造点 ~20 处的具体实参语义未逐个确认。
5. `DiseaseVitalsService.cs:238-248` 多 course 时如何改,未逐行读。
6. `DiseaseCourse` 是否可承载「同病种二次 onset」(复发 vs 新发)—— 若走 `relapse.*` 则 `(patientId, diseaseId)` 去重键仍成立;未核。

---

## 四、D · `CareApplied.phase` 序数定义权

> D 是**纯勘察**,未落盘任何仓库改动,明确「不替用户选」。

### 4.1 现状:全库无序数

> 🔁 **2026-10-22 已失效(原文不删)** —— 用户裁定「甲+乙 都做」后序数已三处齐备,
> 见 **§11.1**。本节保留的是**裁定前的勘察快照**,它的价值在于记录
> 「当时确实零序数」这个事实 —— 没有它,§11.1 的改动就没有可对照的起点。

`entities.yaml:2186` 逐字(**裁定前**):

```
payload_schema: "tick: i64; action_id: i32(护理动作表 ordinal,K ≤ |护理动作表|); caregiver: i32; phase: 枚举(起 止)。一条事件表示起或止,写入期校验 t(止) ≥ t(起);语义 = 玩家主动护理动作;放医馆本身不计数,EnvMod 不得推出本事件"
```

⇒ 只写「枚举(起 止)」,**没有序数**。GDD `:164` 只给了一个中文词 `起/止` 占据一个字段位,
**没有序数、没有字段名 `phase`、没有类型**。GDD `:208-224`(照护事件化)连 `phase` 都**没点名**,
只列了「tick + 动作 id」两项。**author(24)自己的 `clinic-machine.md` 也没有任何起/止语义或序数定义。**

### 4.2 同型「裸 int 枚举」字段的序数解决方案对照(D 的表)

| 字段 | 序数定义在哪 | 载体形态 | 越界门 |
|---|---|---|---|
| `CareApplied.phase` | ~~**无权威定义**~~ 🔁 **2026-10-22 已三处齐备**(见 §11.1) | — → **枚举 + registry 写死序数 + 转发常量** | `DiseaseCourse.cs:154` fail-loud(**保留**) |
| `polarity`(处置) | `PrescribeFlow.cs:29` `enum PrescriptionPolarity`(**住 `Sim`,非 Contracts**) | 载荷字段是 `int` | 载荷侧**无**门 |
| `method`(急救) | **无权威序数**。GDD `:206` 只写 `∈ { Manual, Skip }` | 私有 const **两处副本** | 无门 |
| `cause`(急救) | **无权威序数**。GDD `:207` | 无枚举,测试按 0/1 硬写 | 无门 |
| `DiseaseKind` | `RegistrySchema.cs:151-167` | **C# 枚举**,含 `Unspecified = 0` 哨兵 | **注册表加载期**校验 |
| `PoiState` | `PoiStateMachine.cs:26-31` 枚举 + **entities.yaml 散文写死序数**(`:2123`) | 枚举 + 字段 `int` | 转移表拒逆转移;**解码侧无范围门** |
| **`PatientOutcome`(最强先例)** | `HistoryPayloads.cs:55-65` 枚举**住 Contracts** + 载荷字段仍 `int`(`:91`)+ **registry 写死序数**(`entities.yaml:3276`) | 三处齐备 | **codec 解码门**(`PayloadCodec.History.cs:141-146`) |

### 4.3 两案

> ✅ **2026-10-22 用户裁定 =「甲+乙 都做」**(两案**同时采纳**,不是二选一)。
> 下面是 D 当时列的两案原文(保留作决策依据),实际落地见 **§11.1**。
> ⚠️ 注意:两案各自的「代价」栏都写了「**仍是两处真源**」—— 这正是用户选「都做」的理由:
> 单做任一案都消不掉「两处真源」,只有两案叠加 + 机械断言才能。

**甲案 —— `entities.yaml` 把序数写死**(`phase: 枚举(起 止)` → `phase: 枚举(起=0, 止=1)`)
- 改动面:**1 文件 1–2 行**
- kindgen:**不需重跑改产物**。A2 只扫 C# struct 字段类型(`INT_PRIMITIVES` 白名单,`:197-211`);A2b 只扫 registry 散文里的 float/double 字样与数组界证据 —— **都不解析枚举序数**
- `DiseaseCourse` 常量保留 0/1,但 remarks 现写「序数出处:GDD `:164`」**是失实的**,须改引 `entities.yaml:2186`
- 优点:最小;权威落在 ADR-024 认定的 registry 真源;与 `PoiState` / `PatientOutcome` 的 registry 半边一致
- 代价:**纯散文,机器不可检** —— 仍是「两处真源」(registry 散文 + C# 常量),无机械门钉住

**乙案 —— 升格具名 C# 枚举 `CarePhase { Start = 0, Stop = 1 }`,载荷仍传 int**
- 改动面:① 新增枚举(建议 `Sim.Contracts/Payloads/HistoryPayloads.cs`,与 `PatientOutcome` 同文件);② `DiseaseCourse` 两常量改引用或删除;③ `NoteCareAction` 门改与枚举比较;④ `DiseaseCourseBook` 参数注释;⑤ 测试 4 处引用;⑥ 可选 codec 解码范围门;⑦ 可选 registry 补「见 CarePhase」
- **⚠️ ADR-024 A2 不触碰** —— 载荷字段仍是 `int`
- **⚠️ 若把字段类型改成枚举 ⇒ 会红**:kindgen A2 分支是 `if elem in INT_PRIMITIVES: continue / if elem in structs: ... / else fail` —— 枚举类型名**既不 ∈ 整数白名单也不 ∈ struct 集 ⇒ fail**。改类型前必须先扩 kindgen(另立 ADR-024 修订)
- 优点:序数单一 C# 定义;可实现 codec 解码门,把越界从「运行时靠消费者抛」提前到「解码即拒」
- 代价:新增 `Sim.Contracts` 公共类型面;5–7 文件联动;若不同步改 registry 散文,**仍是两处真源**

### 4.4 D 的两个额外问答

**Q1(甲案)`NoteCareAction` 的值域门保留还是改引用 entities 生成物?**
答:**目前不存在可引用的 entities 生成物**。`kindgen` 的产物只有 `Sim/StreamRouting.g.cs`(Kind→Stream 路由),A1–A5 全是断言,不产出任何枚举 / 序数常量。⇒ 只有 (a) 保留硬编码门 + 改引 `entities.yaml:2186`(成本≈0);(b) 扩 kindgen 从散文抽序数(**新机制**,需自己的探针轮,且散文解析脆弱 —— 与 ADR-024「禁从散文解析」精神相抵)。

**Q2(乙案)`CareAppliedPayload.Phase` 类型要不要改成枚举?**
答:**不改,保持 `int`**。三条理由:① ADR-024 A2 + kindgen 会红;② 先例 `PatientOutcome` / `PoiState` 都是枚举住类型面、载荷字段仍是 `int`;③ codec 整数域形状不需枚举类型。
⇒ **乙案的正确形态 = 枚举只做常量来源 + 门,载荷形状一字不改。**

### 4.5 ⚠️ D 的推断,编排方核实

D 明写:「**未实跑 kindgen**,『枚举类型名 ⇒ A2 fail』是从 `kindgen.py:197-211` 的判据读出来的**推断**,不是观测。」

**编排方独立复核 ✅ 推断成立**。`tools/kindgen/kindgen.py:190-211` 实测:

```python
for fname, ftype in fields:
    elem = ftype[:-2] if is_array else ftype
    if elem in INT_PRIMITIVES: continue
    if elem in structs: ...continue
    fail(rep, f"A2 {cur}: 字段 {path} 类型 {elem!r} ∉ 整数域白名单 …")
```

⇒ 枚举类型名既不命中 `INT_PRIMITIVES` 也不命中 `structs` ⇒ **走 fail 分支**。D 的推断正确。

### 4.6 D 明确没查清的事

> ✅ **2026-10-22 编排方补查 4 / 5 条**(第 5 条仍缺,如实留白)。

| # | D 的未查清项 | 补查结果 | 判据 |
|---|---|---|---|
| 1 | `PrescriptionPolarity` 枚举体 | ✅ **确认** `Symptomatic = 0` / `Causal = 1`,与 `ProgressionEvaluator.cs:77-85` 的注释转述一致 | `Sim/Prescription/PrescribeFlow.cs:29-35` |
| 2 | `R1_CHECK_31` 的校验体 | ✅ **确认**:它管的是 `DiseaseKind.Unspecified = 0`「一律拒收」,**与 `phase` 无关**(D 当时可能误以为它是通用值域门) | `Sim/RegistrySchema.cs:143,153` |
| 3 | `DecodeCareApplied` 有无 phase 范围门 | ✅ **确认:无范围门** —— 只做 tag 完整掩码校验(`(1u<<4)-1`),`phase` 原样读出,**越界值不在此拒** | `Sim.Codec/PayloadCodec.History.cs:232-252` |
| 4 | `clinic-machine.md` 有无「护理动作表」定义 | ✅ **确认无** —— 该 GDD 零命中「护理动作表」/`care_action`。⇒ 写者的 `actionId` 上界**至今无权威**(见 §11.2 与 `CareAppliedWriter.cs` 的「登记项」) | `grep -n "护理动作表\|care_action" design/gdd/clinic-machine.md` → 空 |
| 5 | ADR-021 正文「具体值归 6 的 GDD」精确行号 | ❌ **仍未核** | — |

**额外实跑(把 D 的「推断」升级为「实测」)**:

D 的 kindgen A2 结论当时是**读源码推断**,未实跑。本轮补跑:

```
$ python3 tools/kindgen/kindgen.py --check
kindgen: A1–A5 全过(--check 不写文件)· A2 严格形:40 个载荷 struct / 162 个字段类型受判(非空跑)
```

⇒ ① **推断成立**:A2 确实在扫字段类型,且当前全过(证明「载荷字段保持 `int`」这一决定是对的);
② **顺带证伪一个隐忧**:A2 **不是空跑**(162 个字段受判),所以「全过」有信息量。

---

## 五、需要用户裁决的事项汇总

| # | 事项 | 来源 | 现状 | 不裁的后果 |
|---|---|---|---|---|
| **1** | **`phase` 序数定义权归甲案(registry 写死)还是乙案(具名枚举)** | D | **✅ 已裁 2026-10-22 =「甲+乙 都做」**(用户裁定),已全部落地,见 §11 | — |
| **2** | **前置 ⑥「止须有可配对的起」保留还是降级** | A | **✅ 已裁 2026-10-22 =「先不裁,我去查 24 的生命周期」** ⇒ **代码保持拒收**,前提缺口登记为 `OQ-24-8`,见 §11.2 | 若 24 允许「只发止」,本写者会静默拒掉合法写入 —— 已登记 OQ,不再是无提醒的洞 |
| **3** | **`0 = 未配置` 哨兵是工程约定,GDD 无此条** | B | **未裁**。代码现状 = 已落 | 若用户认为 0 不合法,JSON 必须填非 0 值 = 代拍数值 |
| **4** | **`W` 载体是全局还是逐病种** | B | **B 推断为全局**(GDD 结构),非明文 | 若本意是逐病种,改动面 = JSON 顶层 2 键 → 13 行各 2 键 |
| **5** | **F5 路径 A / B / C 选哪条** | C | **未裁** | F5 无法推进;AC-31 假绿持续 |
| **6** | **`D` 如何增长(GDD 缺口)** | C | **GDD 未定义** | 路径 A / B 都依赖它 —— 这是最大缺口 |

---

## 六、明确未做 / 未验证(禁借绿)

### 6.1 代理自己报的

- **A**:测试未跑、未编译;未跑构建前门 / 探针轮 ⇒ 它的「门现在绿」是源码级论证
- **B**:一次都没编译;测试 **0/3**;cooked 布局未改;`CompositionRoot` 未接线
- **C**:只读勘察,未改任何文件;6 条未查清(见 3.7)
- **D**:纯勘察;未实跑 kindgen(推断);5 条未查清(见 4.6)

### 6.0 四代理问题的处置台账(2026-10-22 复核后)

> 用户问:「4 个代理的问题都分析修正了吗」。**答案是「部分」** —— 下表逐条给状态,
> 不按「轮次完成」笼统记账。判据一律是可跑命令,不是自述。

| # | 代理 | 问题 | 状态 | 判据 |
|---|---|---|---|---|
| A1 | A | `CareAppliedWriter` 无生产触发点 | ❌ **未修** | `grep -rn "new CareAppliedWriter" unity/Assets/ \| grep -v /Tests/` → 空 |
| A2 | A | 前置 ⑥ 隐含未裁决前提 | ✅ **已裁并登记** | 用户裁定「先不裁」+ `OQ-24-8` |
| A3 | A | 写者未编译/未跑测试 | ✅ **已代跑** | round23 `4614/4578/0` |
| A4 | A | 构建前门探针轮 | ❌ **未做** | 无构建日志 |
| B1 | B | `W`/`TREND_EPS` **生产路径恒 0** | ✅ **已修(round24 · 用户裁定方案 B)** | 全管线:布局扩展 + Binder 透传 + 新 codec + 新 DataSet + provider 装载 + 预载地址 + 组条目 + 产物落盘 + `CompositionRoot` 三级分支。判据 = `drift[disease_registry.cooked.bytes]` + `test_wiring_loadBranch_diseaseRegistry_exists` + 四处集合 22/22/22/22。⚠️ **端到端「值流到 vitals」仍无夹具**(§6.2.2) |
| B2 | B | `LowPositionHeldTicks` **无递增点** | ✅ **已修(round24)** | `ProgressionEvaluator.ComputeLowPositionHeldTicks`(**t 的纯函数**,非计数器)+ `DiseaseVitalsService.cs:453` 接线 + 8 条测试。边界登记 `OQ-9-1` |
| B3 | B | `0 = 未配置` 是工程约定 | ⚠️ **未裁** | 用户未裁(§五 #3) |
| B4 | B | `W` 载体全局 vs 逐病种 | ⚠️ **未裁** | 用户未裁(§五 #4) |
| B5 | B | 三条 `trendWindow` 测试 | ✅ **已落且绿** | round23 全绿,名字在册 |
| C1 | C | **AC-31 假绿** | ❌ **未修** | `catchup_replay_test.cs:66-79` 手写 `foreach` 求 max,**零调用 `DiseaseAggregator`** |
| C2 | C | **AC-32 假绿** | ❌ **未修** | 同文件 `:80-98` 只断言 `compounds.Count >= 0`(**恒真**) |
| C3 | C | `D` 如何增长(GDD 缺口) | ⚠️ **GDD 未定义** | 全库无答案 —— 路径 A/B 都依赖它 |
| C4 | C | F5 路径 A/B/C | ⚠️ **未裁** | 用户未裁(§五 #5) |
| C5 | C | **AC-16 乱序重放 = 假绿 + 非确定**(本轮新发现) | ❌ **未修** | `catchup_replay_test.cs:150` 用 `Guid.NewGuid()` 洗牌(违反确定性纪律);`:156` 只断言 `result1.Count == result2.Count` —— 而 `CatchUp.cs:63` 内部**先按全序键排序**,故条数**恒等** ⇒ 断言恒真 |
| D1 | D | `phase` 序数无权威 | ✅ **已修** | §11.1 三处齐备 + 变异 ①② 双证 |
| D2 | D | 5 条未查清 | ✅ **补查 4/5** | 见 §4.6 表;第 5 条(ADR-021 行号)仍缺 |
| D3 | D | kindgen A2 只是推断 | ✅ **已实跑** | `kindgen.py --check` 全过,162 字段受判 |

**统计**(可复算,勿手数):

```bash
python3 - <<'EOF'
import re
s = open('production/qa/evidence/four-agents-merge-2026-10-22.md', encoding='utf-8').read()
sec = s[s.index('### 6.0'):s.index('### 6.2')]
rows = [l for l in sec.splitlines() if re.match(r'\| [ABCD]\d ', l)]
for tag in ('✅', '❌', '⚠️'):
    print(tag, sum(1 for l in rows if tag in l.split('|')[4]))
print('合计', len(rows))
EOF
# 实测输出(2026-10-22 round24 后): ✅ 8 · ❌ 5 · ⚠️ 4 · 合计 17
# 🔁 上一版实测输出(复核轮): ✅ 6 · ❌ 7 · ⚠️ 4 · 合计 17
#    B1 / B2 两行由 ❌ 转 ✅ ⇒ 6+2=8 / 7-2=5,其余不变(合计恒 17)。
```

> 🔁 **订正记录**:初稿此处手写「✅ 8 项」,实跑得 **✅ 6 项**。**手数错了 2 项。**
> 错在哪一项、为什么错,我**没有留存当时的算式**,故**不追认原因** ——
> 只登记「初稿数字为 8,复算为 6,以复算为准」。这正是「数字须带可跑复算命令」要防的:
> 手数 17 行里的一列,**错了也不会有人发现**,直到有人真跑一遍。
>
> **口径**(写在此处以便复算时对得上):**✅ = 该问题已闭合**,含三种闭合形态 ——
> ① 改了代码(D1)· ② 已裁定并登记(D1 之外 A2)· ③ 已实跑取证(A3 / B5 / D3)。
> **不是**「只算改了代码的」。按此口径恰为 **A2 / A3 / B5 / D1 / D2 / D3 = 6**。

**统计**:✅ 已闭合 **8** 项 · ❌ 未修 **5** 项 · ⚠️ 待用户裁/待定义 **4** 项(合计 17)。
🔁 原文「✅ 6 · ❌ 7 · ⚠️ 4」= round24 之前的值(原文不删);差异恰为 B1/B2 两行由 ❌ 转 ✅。

> **2026-10-22 复核增补(B1 的深层根因)**:复核时把 B1 的判据从「传参缺失」追到
> 「数据面根本没这个量」,发现**真修法比 B 报告的更重** —— 不是加两个实参,
> 是改 cooked 布局版本号(`DiseaseRegistryBaker.cs:47-50` 自陈「不透传进 cooked」)。
> **B1 的状态不变(❌ 未修)**,但工作量评估必须按新根因重算,否则下一轮会按
> 「加两个参数」估时,做完发现值还是 0。这正是「自述 vs 实测」的差别。

**⚠️ 为什么 ❌ 那 7 项没修**:它们**不是「漏了」,是「没轮到」** ——
本轮的授权范围是「处理完四个代理的报告」= 把它们的**勘察结论**收成判定并落实**已裁的那两条**。
B1/B2/C1/C2 都是**实现级改动**(接生产路径、写累计器、重写测试断言),
各自需要独立的裁定与验证轮;C3/C4 更是**设计缺口**,须用户裁。

> **不得把本表读成「代理的问题都解决了」** —— 恰恰相反:四份报告里**最重的几条**
> (B1 管道断、B2 累计器不存在、C1/C2 假绿、C3 设计缺口)**全部仍未解决**。
> 本轮真正闭合的只有 **D 的全部** + **A 的裁定面**。
>
> 🔁 **2026-10-22(同日·round24)就地订正**:上段是**复核轮**的结论,当时为真。
> 用户随后裁定「修 B1 和 B2」⇒ **B1 / B2 已在 round24 闭合**(§6.2.1),
> **C1 / C2 / C3 仍未解决**。原文不删 —— 它记录的是那个时点的真实状态,
> 删掉会让「为什么当时没修」这个判断失去依据。

### 6.2 编排方补做后的剩余缺口

> 🔁 **2026-10-22(同日·round24)就地订正前两行** —— B1 / B2 已由用户裁定「修 B1 和 B2」
> (B1 取**方案 B:按 telemetry 模板补全注册表管线**)。原文保留在下方「原文」块,
> 两行状态改为已闭合,判据见 §6.2.1。

| 缺口 | 状态 |
|---|---|
| **`W` / `TREND_EPS` 生产路径恒 0** | ✅ **已接真(round24)** —— 见 §6.2.1 ① |
| **`LowPositionHeldTicks` 累计器不存在** | ✅ **已实现(round24)** —— 见 §6.2.1 ② |
| **F5 多病聚合** | **未接** —— 档案簿单 course 假设仍在 |
| **AC-31 / AC-32 假绿** | **未订正** —— 测试仍在跑自造断言 |
| **`CareApplied` 生产触发点** | **未接线** —— 有写者 ≠ 已接线 |
| **构建前门探针轮** | **未做** —— 本轮只跑了 EditMode 全量,未验证门在构建期真的被调用 |
| **`EventStream.Clear()` 视图陈旧** | **A 的疑点已证伪**(源码是 `_events.Clear()`),但**无测试钉住** —— 若将来有人改成重新赋值,静默失效 |
| **`OQ-24-8`(24 是否允许只发止)** | **未裁** —— 用户 2026-10-22 裁定「先不裁,我去查 24 的生命周期」;代码保持拒收(保守方向) |
| **`test_carePhase_enumMatchesForwardingConstants` 的源码层判据是文本级** | **已知边界** —— 抓不住别名/中间常量绕过;不是语义级(§11.3 末段) |

> **原文(2026-10-22 复核轮,保留不删)**:
> 「**`W` / `TREND_EPS` 生产路径恒 0** | **仍未接** —— `CompositionRoot` 走
> `CreateFallbackRegistry()`,不经过 Binder;cooked 布局无这两个全局量的位置」
> 「**`LowPositionHeldTicks` 累计器不存在** | **仍未实现** ⇒ **即使 W > 0,痊愈支仍不会成立**」

### 6.2.1 round24 —— B1 / B2 修复轮(2026-10-22 · 用户裁定「修 B1 和 B2」· B1 取方案 B)

**授权原话**:「修 B1 和 B2」· B1 方案选择「选 B」。

**B1 方案 B 的已知后果(用户选择时已被明确告知并接受)**:
真注册表进生产后,`PatientAppearedDriver` 取的 `registry[0].DiseaseId` 从 `1`(fallback 占位)
变成 **`2`(`INJ_STAB`,刺伤,`lethal: true`)** ⇒ **生产里病人开始真的会死**。
这是「接真」的应有之义,但是**玩法可见的变化**,不是纯管道改动。

#### ① B1 —— `W` / `TREND_EPS` 生产路径(方案 B 全管线)

| 环节 | 落点 | 判据 |
|---|---|---|
| 载荷布局扩展 | `DiseaseRegistryCookedWriter.cs:51-57`(u32 schema 回声 + i64 W + i64 eps,置于行数之前) | 漂移门 `drift[disease_registry.cooked.bytes]` 逐字节比 baker 现烘输出 |
| Binder 透传 | `DiseaseRegistryBaker.cs:84-88`(`bound.TrendWindowTicks` / `TrendEpsRaw` 进 `Write`) | 同上(改 Binder 不透传 ⇒ 字节变 ⇒ 红) |
| 读方 codec | **新** `Gameplay.Presentation/DiseaseRegistryCookedCodec.cs`(与写方逐字段镜像 + 加载期 R1-36 门) | `DiseaseRegistryBaker.Materialize` 真往返(唯一自动兜现两侧布局分叉) |
| 运行期数据集 | **新** `Gameplay.Presentation/DiseaseRegistryDataSet.cs` | 同上 |
| 装载面 | `AddressablesDataProvider.cs`(常量 + `EnsureDiseaseRegistry` + `Load<T>` 分支 + `Preload()` 调用) | `ProviderWiringGateTest.test_wiring_loadBranch_diseaseRegistry_exists` |
| 预载地址表 | `DataCorePreloader.cs`(第 22 条) | 四处集合相等门:`*Address` 常量 = `DataCoreAddresses` = `Preload()` 的 `Ensure*` = 组条目 = **22 / 22 / 22 / 22** |
| Addressables 组 | `data-core.asset`(GUID `e67ef82b2b120c5bdbbd246d0179fa7e`,与 `.meta` 逐字一致) | `test_wiring_dataCoreGroup_containsEveryProviderAddress` + `..._entriesResolveToExistingArtifacts` |
| 产物落盘 | `unity/Assets/DataCooked/disease_registry.cooked.bytes`(1869 B,`ConfigVersion = 0x3436F40A`) | `cooked_drift_all_artifacts_test` 门① |
| 装配接线 | `CompositionRoot.cs:169-215` 三级优先(caller 表 > provider 真表 > fallback)+ `W`/`eps` 灌进 `DiseaseVitalsService` | 见下方**接线判据的诚实边界** |
| 菜单入口 | `DataBakeMenu.BakeDiseaseRegistry` | batch 实跑成功(日志 `unity/Logs/bake-disease-registry-20261022_215348.log:794`) |

**接线判据的诚实边界(重要)**:`CompositionRoot` 的三级分支**没有独立的单元测试**
直接断言「provider != null ⇒ 走真表」。现有证据是**间接**的:
① `ProviderWiringGateTest` 的两个 `MultiTableProvider` 夹具在补 `DiseaseRegistryDataSet`
之前会因「无该类型」当场抛 ⇒ 装配面确实向 provider 要了这张表(缺则红);
② `DiseaseVitalsService` 的构造实参从 4 个变 6 个,`W`/`eps` 由此而来。
⇒ **「值真的从产物流到了 vitals」这一步没有端到端夹具**,登记为缺口(见 §6.2.2)。

#### ② B2 —— `LowPositionHeldTicks`

| 环节 | 落点 |
|---|---|
| 实现 | `ProgressionEvaluator.ComputeLowPositionHeldTicks`(**t 的纯函数**,反查历史 tick 的 Progress,非跨 tick 计数器) |
| 接线 | `DiseaseVitalsService.cs:453-456` 第三参由该函数求值 |
| 取舍理由 | 积分量不满足 ADR-016 §九「三源重放」——存档不携带它 ⇒ 读档后同刻判出两种终态 |
| 测试 | **新** `Tests/EditMode/DiseaseSimulation/low_position_held_test.cs`(8 条) |
| 已知边界 | 潜伏期起算点 → **`OQ-9-1`**(本轮新立,`design/gdd/disease-simulation.md` Open Questions 段;此前该编号是**悬空引用**) |

#### ③ 回归(三元组 · round24)

| 项 | 值 |
|---|---|
| **截至时刻** | 2026-10-22 **22:03:33 (+0800)**(Unity 启动 `22:01:18`) |
| **批次** | **round24** · 全量 EditMode · B1/B2 修复轮最终判据 |
| **commit** | `f2747b3` + 工作树改动(**未提交**) |

`total=4624 passed=4588 failed=0 inconclusive=0 skipped=36` · `error CS = 0`

**基线 round23** = `4614 / 4578 / 0 / 36` ⇒ **Δtotal = Δpassed = +10**,全名级差集 **新增 10 / 消失 0**:

```
+ LowPositionHeldTest.test_held_alwaysHigh_isZero
+ LowPositionHeldTest.test_held_atOnset_isAtMostOne
+ LowPositionHeldTest.test_held_beforeOnset_isZero
+ LowPositionHeldTest.test_held_cappedAtWindow
+ LowPositionHeldTest.test_held_isPure_sameTickIndependentOfHistory
+ LowPositionHeldTest.test_held_matchesManualBackwardCount
+ LowPositionHeldTest.test_held_nonPositiveScale_doesNotThrow
+ LowPositionHeldTest.test_held_unconfiguredWindow_returnsZero
+ ProviderWiringGateTest.test_wiring_loadBranch_diseaseRegistry_exists
+ cooked_drift_all_artifacts_test.drift[disease_registry.cooked.bytes]
```

复算命令:
```bash
python3 - <<'EOF'
import xml.etree.ElementTree as ET
def names(p):
    r = ET.parse(p).getroot()
    return {(tc.get('fullname') or tc.get('name')) for tc in r.iter('test-case')}, r
cur, rc = names("/home/gu/.claude/jobs/181888b1/tmp/editmode-20261022_220118.xml")
old, ro = names("unity/Logs/test-editmode-round23.xml")
print("本轮   : %s/%s/%s/%s/%s" % tuple(rc.get(k) for k in ('total','passed','failed','inconclusive','skipped')))
print("round23: %s/%s/%s/%s/%s" % tuple(ro.get(k) for k in ('total','passed','failed','inconclusive','skipped')))
print("Δtotal = %+d ; 新增 %d / 消失 %d" % (len(cur)-len(old), len(cur)-len(old), len(old-cur)))
EOF
```

**时间窗核对(逐项过)**:窗口 `22:01:18 → 22:03:33` 内,`unity/Assets` / `design` / `assets` /
`docs` / `production` 下 `.cs` / `.md` / `.json` / `.asset` / `.yaml` 的 mtime 变动**只有一处**
`AddressableAssetsData/AddressableAssetSettings.asset` —— 而它是 Unity 自己 touch 的:
`git status` 干净、`sha256` 与 `HEAD` 逐位相同(`2b1dfd96…8da3`)。**编排方零写盘 ✅**。

> ⚠️ **上一轮(round23.5)被本纪律废掉**:`21:57:36` 启动、`21:59:43` XML 落盘,而我在
> `21:59:38` 改了 `cooked_drift_all_artifacts_test.cs`(类头注释)—— **落在窗口内**。
> 那一轮数字虽同为 `4624/4588/0/36`,但**不能作为判据**;本节的 round24 才是干净轮。
> 这正是 `active.md` 记的「batch 从启动到 XML 落盘期间编排方零写盘」纪律的**第二次实战命中**。

### 6.2.2 round24 之后仍未闭合的项(禁借绿)

| 缺口 | 状态 | 为什么不算闭合 |
|---|---|---|
| **`W`/`eps` 值真的流到 vitals 的端到端夹具** | ❌ **无** | 见 §6.2.1 ① 的「接线判据的诚实边界」—— 现有证据只到「装配向 provider 要了这张表」 |
| **`W = 0` 仍是未配置值** | ⚠️ **数值轮未裁** | 管线通了 ≠ 功能可用。`W = 0` ⇒ `IsConfigured = false` ⇒ **痊愈支 / 迁延支在生产里仍不求值** |
| **`OQ-9-1` 潜伏期边界** | ✅ **2026-10-23 裁:乙案** | ~~本轮只**登记**了它(此前是悬空引用);填 `W` 前必须先裁,否则 `W` 一非零就激活一个未裁边界~~ —— **已结案**:起算点 = `onset + incubation`,落 `unity/Assets/Sim/ProgressionEvaluator.cs:ComputeLowPositionHeldTicks`,判据 `low_position_held_incubation_boundary_test.cs`(6 条),证伪 M4/M5/M6 三轮全红。⚠️ 本行状态订正于 2026-10-23,原文保留留痕;**`W` 的数值本身仍未裁**(数值轮),与本行无关 |
| **`Relapse` / `TreatableBy` / `Signs` / `Handle` 仍未编码** | ❌ **欠账** | 见 `DiseaseRegistryCookedWriter` 文件尾 ②③。当前数据全为 `[]`/`null`,影响面已核实(`Handle == null` 不制造假红,但一致性判据在真表上恒不触发) |
| **`PlayerOutcomeChanged` 写者** | ❌ **未落** | `WriterExistenceGate.cs:105` 仍在册 —— 与 B1/B2 无关,不在本轮授权面 |
| **PlayMode 全量** | ❌ **未跑** | 本轮只跑 EditMode。生产行为变化(病人开始会死)的**玩法侧**影响未经 PlayMode 验证 |

---

## 七、回归(三元组)

| 项 | 值 |
|---|---|
| **截至时刻** | 2026-10-22 **20:59:32 (+0800)**(XML 落盘时刻;Unity 启动 `20:57:31`) |
| **批次** | **round23** · 全量 EditMode · **最终判据** |
| **commit** | `f2747b3` + 工作树改动(**未提交**) |

**EditMode**:`total=4614 passed=4578 failed=0 inconclusive=0 skipped=36`
· `error CS = 0`

**基线**:`round18` = `4609 / 4573 / 0 / 36` @ 2026-10-22(第六轮裁定落地后全绿)

**时间窗核对(必须逐项过,不能只看「启动前有没有改」)**:

```
Unity 启动(日志 Date 行)           20:57:31
design/registry/entities.yaml      20:50:04   ✅ 早 7 分 27 秒(变异 ① 的还原)
care_applied_writer_test.cs        20:51:19   ✅ 早 6 分 12 秒(断言修复)
DiseaseCourse.cs                   20:52:57   ✅ 早 4 分 34 秒(变异 ② 的还原)
HistoryPayloads.cs                 20:57:17   ✅ 早 14 秒(doc 注释订正 —— 见下)
Unity 写出 XML                     20:59:32
```

> ⚠️ **`HistoryPayloads.cs` 只早 14 秒** —— 那是**最后一次改动**(把枚举 doc 里
> 「此前全库无序数」标成历史陈述,避免被读成现状)。它**只改 doc 注释**,
> 但仍触发了 round23。**若它再晚 15 秒,round23 就会像 round20 一样成为废轮。**
> ⇒ **教训:任何写盘(含注释、含还原)都要在启动 batch 之前留足余量,至少数分钟。**

> **本批共跑了 5 轮全量(round19–23)**。每轮都真实需要(不是重复劳动),
> 但**根因收敛为一条**:改动没有一次性收敛,而每次「再改一点」都要重跑。
> 详见 §8.6 的两条可操作教训。

**差分对账(round18 → round23,按测试名逐条)**:新增 **5** / 消失 **0** / 净 **+5**
= `4614 − 4609` ✅

```
+ CareAppliedWriterTest.test_carePhase_enumMatchesForwardingConstants   ← §11.1 丙
+ CareAppliedWriterTest.test_carePhase_ordinalsMatchRegistry            ← §11.1 丙
+ DiseaseRegistryBakePipelineTest.test_trendWindow_absent_meansUnconfiguredZero  ← B 的三条
+ DiseaseRegistryBakePipelineTest.test_trendWindow_outOfRange_isRejected         ← B 的三条
+ DiseaseRegistryBakePipelineTest.test_trendWindow_present_isReadBack             ← B 的三条
```

**round20 / round21 / round22 / round23 四轮的名字集完全相同**(各 4614,差分新增 0 / 消失 0)
—— 这佐证「round20 的两处后改、round21 之后的断言修改」**都不改变测试清单**,
只有 **round23** 的**时间窗**能自证覆盖到了全部改动。

**差分对账的复算命令(两段独立,勿混)**:

```bash
# ① round18 → round21(本轮的 +5)
python3 - <<'EOF'
import xml.etree.ElementTree as ET
def load(p):
    return {tc.get('fullname') for tc in ET.parse(p).getroot().iter('test-case')}
a, b = load('unity/Logs/test-editmode-round18.xml'), load('unity/Logs/test-editmode-round21.xml')
print(f'round18={len(a)} round21={len(b)}')
print('新增:', sorted(b - a)); print('消失:', sorted(a - b))
EOF

# ② round15 → round18(第六轮裁定那批,历史对账,保留原文)
python3 - <<'EOF'
import xml.etree.ElementTree as ET
def load(p):
    return {tc.get('fullname') for tc in ET.parse(p).getroot().iter('test-case')}
a, b = load('unity/Logs/test-editmode-round15.xml'), load('unity/Logs/test-editmode-round18.xml')
print('新增:', sorted(b - a)); print('消失:', sorted(a - b))
EOF
```

实测输出(round15 → round18,**历史记录,非本轮**):

```
新增 (14):
  F4WiringTest.test_careRestartAfterStop_usesNewActionNotOldStop
  F4WiringTest.test_careStop_endsWindowImmediately
  CareAppliedWriterTest.* × 10
  F4OutcomeEvaluatorTest.test_lastIntervention_careRestartAfterStop_usesNewAction
  F4OutcomeEvaluatorTest.test_lastIntervention_careStop_endsWindowImmediately
消失 (3):
  F4WiringTest.test_careStop_isRedundantUnderDerivedFormula
  F4OutcomeEvaluatorTest.test_lastIntervention_careDerivedLaterThanStop_wins
  F4OutcomeEvaluatorTest.test_lastIntervention_careStopLaterThanDerived_wins
净变化: 14 − 3 = 11  ✅ 与 4609 − 4598 = 11 相符
```

---

## 八、逐轮记录(含假失败的归因)

| 轮 | total | passed | failed | 归因 |
|---|---|---|---|---|
| round15 | 4598 | 4562 | 0 | 第五轮基线 |
| round16 | 4609 | 4564 | **9** | **8 条假失败 + 1 条真红**,见下 |
| round17 | — | — | — | **编译红**,无 XML |
| round18 | 4609 | 4573 | 0 | 全绿 |
| round19 | 4612 | 4575 | **1** | 补 B 的三条测试后;唯一红 = **编排方自己写错的测试**,见 §8.4 |
| round20 | 4614 | 4578 | 0 | 甲+乙 两条新测落地后;⚠️ **但有两处注释改动未进本轮编译窗口**,见 §8.5 |
| round21 | 4614 | 4578 | 0 | 干净窗口;⚠️ **但 §11.3 的断言修复在其之后** ⇒ 不作最终判据 |
| round22 | 4614 | 4578 | 0 | 断言修复后的干净窗口;⚠️ **但之后又订正了 `HistoryPayloads.cs` 的 doc 注释** ⇒ 不作最终判据 |
| round23 | **4614** | **4578** | **0** | **最终判据** —— 全部改动(断言修复 + 两处变异还原 + doc 订正)之后的干净窗口(§七 有时间窗核对) |

### 8.1 round16 的 8 条假失败 —— **竞态,不是真失败**

```
round16 启动      20:23:17
B 改 json         20:24:56   ← 加了 trend_window_ticks / trend_eps 两键
B 改 Binder 白名单 20:25:20   ← 太晚,测试跑的是旧编译产物
```

json 是**运行时读**的,Binder 是**编译期**加载的 ⇒ 旧 Binder 不认识新键 ⇒ 8 条全是「未知键」红。**重跑即消失。**

> ⚠️ **这条要写进纪律**:并行代理改盘与 batch 测试**必须互斥**。
> 本轮编排方在启动 round16 时不知道 B 还在写盘 —— 这是编排方的调度失误,不是 B 的问题。

### 8.2 round16 的 1 条真红 —— 编排方漏改

`F4OutcomeEvaluatorTest.test_lastIntervention_careStopLaterThanDerived_wins`
夹具 `止 9999 / 动作 4000`,断言 `9999`,实测 `4600`。

**根因**:第六轮裁定改了 `max` 式,但 `f4_outcome_evaluator_test.cs` 里还有两条沿用旧口径的测试没同步。
**更严重的是**:旧夹具「止 9999 > 动作 4000」在新口径下**结构性不可能** ——
「止」本身是一条护理动作 ⇒ `最后动作 ≥ 最后止` 恒成立 ⇒ 该夹具描述的是一个**不存在的状态**。
旧断言之所以过,恰恰是因为旧 `max` 式让「止」项恒被压过 —— 也就是说,**这条测试当时没在测它声称的东西**。

已改写为 `careStop_endsWindowImmediately`(夹具 `止 9999 / 动作 9999`,真实可构造)。

### 8.3 round17 的编译红 —— B 的半截活

5 个错误(4 × CS0120 + 1 × CS7036),见 §2.9。**B 自己预言了其中 2 个。**

### 8.4 round19 的 1 条红 —— **编排方自己的测试写错了**(不是实现错)

`DiseaseRegistryBakePipelineTest.test_trendWindow_present_isReadBack`
期望 `TrendEpsRaw == 1966`,实测 `128843776`。

**根因 = 我把 `FixParse` 的字面量语义搞反了**:夹具写 `"trend_eps": "1966"`,
而 `FixParse` 把裸整数 `"1966"` 读成**值 1966**(raw = `1966 × 65536` = `128843776`)。
要表达 raw 恰为 1966,须写 `"1966/65536"`。

⇒ **实现是对的,测试是错的。** 已把夹具改为 `"1966/65536"`,并在该测试的 doc 注释里
把这条单位语义陷阱写死(它会在任何读 `Fix` 字段的地方重犯)。

⚠️ **这条值得单独记**:它证明「测试红 ≠ 实现错」。若当时图快把实现改成迁就测试
(即让 `FixParse` 把裸整数读成 raw),就会**静默改掉 ADR-006 的边界契约**。

### 8.5 round20 的绿 —— **有 5 秒的窗口没覆盖到**,故不作最终判据

```
Unity 启动(日志 Date 行)      20:42:53
我改 DiseaseCourse.cs          20:40:59   ✅ 在窗口内
我改 HistoryPayloads.cs        20:40:48   ✅ 在窗口内
我改 entities.yaml             20:40:38   ✅ 在窗口内
我改 f4_wiring_test.cs         20:42:22   ✅ 在窗口内
我改 CareAppliedWriter.cs      20:44:55   ❌ 启动后才改(纯注释)
我改 care_applied_writer_test.cs 20:44:59 ❌ 启动后才改(纯注释)
Unity 写出 XML                 20:45:01
```

**两条后改的都是注释**,不影响编译产物 —— 但**我无法自证这一点**,而「注释改动也会进编译」
在 C# 里是真的(注释不进 IL,但**行号映射会变**,且 `[CallerFilePath]` 类测试依赖源码位置)。
⇒ **不辩,重跑**(round21,见 §7)。

> 这与 §8.1 是**同一个病的第二次**:并行写盘与 batch 测试未互斥。
> 差别只在于 §8.1 是**代理**在写,§8.5 是**我自己**在写 —— 纪律必须约束编排方,不只约束代理。
> **本轮立规矩**:batch 测试从启动到 XML 落盘期间,**编排方零写盘**(含注释)。

### 8.6 round21 → round22 → round23 —— 断言修复与 doc 订正带来的后两轮

`§11.3` 的断言修复发生在 round21 之后 ⇒ round21 的绿**不能**当本批判据(借绿)。
round22 = 断言修复后的干净窗口;round23 = **doc 订正后的最终判据**。

**本批一共跑了 4 轮全量(round19–22)**,每一轮都不是重复劳动,但值得记下**为什么**:

| 轮 | 触发原因 | 是否可避免 |
|---|---|---|
| round19 | 补 B 的三条测试 + 打通 `BakeOutput` | 否(内容新增) |
| round20 | 甲+乙 两条新测落地 | 否(用户裁定后新增) |
| round21 | round20 有 5 秒窗口未覆盖(§8.5) | **是** —— 若启动 batch 前不写盘 |
| round22 | round21 之后又改了断言(§11.3 证伪轮发现假判据) | **部分** —— 证伪轮本身不可省,但它本可**排在第一次全量之前** |
| round23 | round22 之后又订正了 `HistoryPayloads.cs` 的 doc 注释(避免「此前无序数」被读成现状) | **是** —— 该订正本该与 §11.1 的改动**同批写完** |

⇒ **两条可操作的教训**:

1. **batch 测试从启动到 XML 落盘期间,编排方零写盘**(含注释)—— 这条已写进 §8.5。
2. **证伪轮应先于全量回归** —— 本轮顺序是「写断言 → 全量 → 证伪 → 发现断言假 → 改 → 再全量」。
   正确顺序是「写断言 → **证伪** → 改到能抓住 → 全量」。证伪是**断言的自检**,
   它比全量便宜(单 fixture 过滤跑),且**发现的缺陷更根本**。
   §11.3 的假判据若在全量前被发现,round22 就省了。
3. **「加注」类改动要一次做完** —— round23 的根因是 §11.1 落地时漏了
   `HistoryPayloads.cs` 枚举 doc 里的那一处历史陈述,事后补改又触发一轮全量。
   **判据**:凡本轮新增/改写的 doc 段,收尾前**统一 grep 一遍**其涉及的历史措辞。

---

## 九、改动清单(本合并件落盘时的工作树)

```
 M assets/data/disease_registry.json                     ← B
 M design/gdd/clinic-machine.md                              ← §11.2(OQ-24-8 登记行)
 M design/registry/entities.yaml                             ← §11.1 甲(phase 序数 + constraint 裁定式)
 M production/session-state/active.md                        ← 本轮收口记录
 M unity/Assets/Editor.Tools.Bake/DiseaseRegistryBinder.cs   ← B + 编排方(构造点)
 M unity/Assets/Editor.Tools.Bake/DiseaseRegistryBaker.cs    ← 编排方(BakeOutput 透传)
 M unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs    ← A + 编排方(条数文案)
 M unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs        ← B + 编排方(形参/注释)
 M unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs    ← §11.1 乙①(CarePhase 枚举)
 M unity/Assets/Sim/DiseaseSimulation/DiseaseCourse.cs       ← §11.1 乙②(常量改转发 + 加注)
 M unity/Assets/Sim/LastInterventionAggregator.cs            ← 第六轮裁定
 M unity/Assets/Sim/RegistrySchema.cs                        ← B
 M unity/Assets/Tests/EditMode/DiseaseSimulation/f4_outcome_evaluator_test.cs  ← 编排方
 M unity/Assets/Tests/EditMode/DiseaseSimulation/f4_wiring_test.cs             ← 第六轮裁定 + §11.1 加注
 M unity/Assets/Tests/EditMode/DiseaseSimulation/disease_registry_bake_pipeline_test.cs ← 编排方(3 条)
 M unity/Assets/Tests/EditMode/Sim/writer_existence_gate_test.cs               ← A + 编排方
?? unity/Assets/Sim/CareAppliedWriter.cs                    ← A(+ §11.1/§11.2 加注)
?? unity/Assets/Tests/EditMode/DiseaseSimulation/care_applied_writer_test.cs  ← A(+ §11.1 两条断言)
?? production/qa/evidence/four-agents-merge-2026-10-22.md    ← 本文
?? production/qa/evidence/tmp-*-draft.md (×4)                ← 保留,见 §零 的订正
```

> ⚠️ **`design/gdd/clinic-machine.md` 也在本批改动内**(§11.2 的 `OQ-24-8` 登记行)
> —— 它不在 `unity/` 子树的视野里,容易漏记。
>
> ⚠️ **`CareAppliedWriter.cs` / `care_applied_writer_test.cs` 是 `??`(未跟踪)** ——
> 本批对它们的改动**不会出现在 `git diff` 里**,只存在于工作树。
> 提交前须 `git add` 才能被审阅者看到。
>
> **复算命令**(上面这张表须与它逐行相符,不得凭记忆写):
> ```bash
> git status --short
> # 实测 2026-10-22 21:0x:16 个 M + 9 个 ??(其中 2 个是 .meta,见下)
> ```
> ⚠️ **两个 `.meta` 是 Unity 自动生成的**(`CareAppliedWriter.cs.meta` /
> `care_applied_writer_test.cs.meta`)—— 它们**必须随 `.cs` 一起入库**,
> 否则同事 clone 后 GUID 不一致。这不是派生物噪音。

---

## 十、复算命令(全部可当场跑)

```bash
# ① 当前豁免条数(替代任何写死的「24 条」)
#    ⚠️ 锚串必须限定到**声明行** —— `ExemptKinds` 在类头注释里也出现,
#       只判 `'ExemptKinds' in l and '=' in l` 会锚到注释行,数出 0(实测踩过)。
python3 - <<'EOF'
import re
L = open('unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs', encoding='utf-8').read().splitlines()
s = next(i for i, l in enumerate(L)
         if 'ExemptKinds' in l and '=' in l and not l.strip().startswith('//'))
d = 0; e = None
for i in range(s, len(L)):
    d += L[i].count('{') - L[i].count('}')
    if d == 0 and i > s: e = i; break
print(sum(1 for l in L[s:e+1]
          if not l.strip().startswith('//') and re.match(r'\(\"\w+\",', l.strip())))
EOF
# 实测输出: 16   (声明行 87 · 收尾行 161)

# ② `LowPositionHeldTicks` 是否有递增点(空 = 累计器不存在)
grep -rn "LowPositionHeldTicks" --include=*.cs unity/Assets/ | grep -v "readonly\|= lowPositionHeldTicks\|window.LowPositionHeldTicks < \|<see cref"
# 预期: 仅 DiseaseVitalsService 的传 0 注释 ⇒ 无递增点

# ③ `EventStream.Clear()` 是原地清空还是重新赋值
grep -A2 "public void Clear()" unity/Assets/Sim/EventStream.cs
# 预期: _events.Clear();

# ④ kindgen A2 对枚举类型名的判定
sed -n '197,211p' tools/kindgen/kindgen.py
# 预期: 见 §4.5,枚举名走 fail 分支

# ⑤ AC-31 / AC-32 假绿
sed -n '60,100p' unity/Assets/Tests/EditMode/DiseaseSimulation/catchup_replay_test.cs
grep -rn "DiseaseAggregator.Aggregate" --include=*.cs unity/Assets/Tests
# 预期: catchup_replay_test 内零命中

# ⑥ `W` / `TREND_EPS` 生产路径断点
grep -n "new DiseaseVitalsService(" unity/Assets/Gameplay.Boot/CompositionRoot.cs
# 预期: 不传 trendWindowTicks / trendEpsRaw ⇒ 取默认 0

# ⑦ §11 甲案:registry 的 phase 序数行(唯一权威文本)
grep -n "phase: 枚举" design/registry/entities.yaml
# 预期: 2186 行含 `phase: 枚举(起=0, 止=1)`
# 反向(须为 0):
grep -c "phase: 枚举(起 止)" design/registry/entities.yaml

# ⑧ §11 乙案:C# 序数唯一出处 + 转发
grep -n "Start = 0\|Stop = 1" unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs
# 预期: 151 / 154 —— 枚举定义(唯一 C# 序数出处)

#    ⚠️ 下面这条**必须剥注释**。`DiseaseCourse.cs:176` 的 doc 注释里保留了原文
#       `<c>public const int CarePhaseStart = 0;</c>`(「原文不删」纪律)——
#       直接 grep `CarePhaseStart =` 会锚到注释行,把「已改为转发」误判成「仍是硬编码」。
#       (与 §十 ① 的 `ExemptKinds` 锚串不唯一是**同型**陷阱,第二次踩。)
python3 - <<'EOF'
import re
L = open('unity/Assets/Sim/DiseaseSimulation/DiseaseCourse.cs', encoding='utf-8').read().splitlines()
for i, l in enumerate(L, 1):
    if l.strip().startswith('//'):
        continue                      # 剥注释行
    if re.search(r'const int CarePhase(Start|Stop)\s*=', l):
        print(i, l.strip())
EOF
# 预期: 两行,且右值均为 `(int)CarePhase.Start` / `(int)CarePhase.Stop`
#       若出现字面 `0` / `1` ⇒ 序数已分叉(这是本条命令真正要抓的东西)

# ⑨ §11 机械断言是否真在跑(不是「文件里有」)
python3 - <<'EOF'
import xml.etree.ElementTree as ET
t = ET.parse('unity/Logs/test-editmode-round23.xml')
for tc in t.getroot().iter('test-case'):
    if 'carePhase' in (tc.get('fullname') or ''):
        print(tc.get('result'), tc.get('fullname'))
EOF
# 预期: 两行 Passed —— 缺任一 ⇒ 该断言没被跑过(NOT-RUN),不得按「已证」计
# ⚠️ 判据必须指向**最后一次改过测试文件之后**的那轮 —— 这里 = round23
#    (round21 在 §11.3 的断言修复之前,拿它的绿是借绿)

# ⑨b §11.3 的两条证伪记录(可重放)
#  变异 ①: sed -i 's/phase: 枚举(起=0, 止=1)/phase: 枚举(起 止)/' design/registry/entities.yaml
#          ⇒ test_carePhase_ordinalsMatchRegistry 红(实测 EXIT=2)
#  变异 ②: 把 DiseaseCourse.cs 两条 const 的右值改为字面 0 / 1
#          ⇒ test_carePhase_enumMatchesForwardingConstants 红(实测 1 failed)
#  ⚠️ 还原用「备份 → cp 回 → sha256sum -c」,**禁** git checkout / git stash
#  ⚠️ 变异 ② 在**初版断言**下是绿的 —— 那次绿是假绿,见 §11.3

# ⑩ `OQ-24-8` 是否已登记
grep -n "OQ-24-8" design/gdd/clinic-machine.md unity/Assets/Sim/CareAppliedWriter.cs
# 预期: GDD 有 OQ 表行 + 写者前置 ⑥ 注释里点名
```

---

## 十一、用户 2026-10-22 两条裁定的落地

> 来源:§五 的第 1 / 2 项。两项**同批裁定、方向相反** ——
> 第 1 项是**加大改动**(甲+乙 都做),第 2 项是**明确不改**(先不裁)。
> 勿混读。

### 11.1 第 1 项 = 「**甲+乙 都做**」(`phase` 序数)

**用户原话**:「甲+乙 都做 …… 代价:改动最大,但**序数从此不可能分叉**」。

这是 D 在 §4.3 列的两案**同时采纳**,构成 `PatientOutcome` 式的「三处齐备」形态:

| # | 落点 | 内容 | 性质 |
|---|---|---|---|
| 甲 | `design/registry/entities.yaml:2186` | `phase: 枚举(起 止)` → `phase: 枚举(起=0, 止=1)` | **权威件写死序数** |
| 乙① | `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs` | 新增 `public enum CarePhase : int { Start = 0, Stop = 1 }` | **序数唯一 C# 定义** |
| 乙② | `unity/Assets/Sim/DiseaseSimulation/DiseaseCourse.cs` | `CarePhaseStart/Stop` 由**硬编码 `0`/`1`** 改为 `(int)CarePhase.Start/Stop` **转发** | 消除第二份序数 |
| 丙 | `care_applied_writer_test.cs` | `test_carePhase_enumMatchesForwardingConstants` + `test_carePhase_ordinalsMatchRegistry` | **机械断言钉住三处一致** |

**⚠️ 三处缺一不可的理由**(为什么不是「测一处就够」):

- 只测 甲乙①(枚举 == 转发常量)⇒ **registry 改了但代码没改**不会红;
- 只测 甲丙(registry == 枚举)⇒ **转发常量被改回硬编码**不会红;
- 三条全测才兑现「序数从此不可能分叉」。

**⚠️ `CareAppliedPayload.Phase` 字段**仍保持 `int`,**刻意不改枚举** ——
`tools/kindgen/kindgen.py` 的 A2 判据只接受整数白名单与已登记 struct,
枚举类型名**两者都不命中** ⇒ 走 `fail` 分支 ⇒ 构建红。改类型须先扩 kindgen。
(与 `PatientOutcome` / `PoiState` 同构:枚举住类型面,载荷字段仍整数。)

**⚠️ 为什么不需要 `Unspecified = 0` 哨兵**(与 `PatientOutcome` 的关键差别):
终态的零值漏填会**静默撒谎**(死亡病人被标成痊愈),故那里必须留哨兵;
而本枚举的 `0` 是**合法值「起」** —— 漏填的 phase 会被读成「起」,
后果是照护支少记一个「止」,**不会**产生「把 A 说成 B」的错误陈述。

**连带订正的三处陈旧自述**(均按「原文不删,就地加注」):

| 文件 | 原文 | 处置 |
|---|---|---|
| `Sim/CareAppliedWriter.cs` 类头 | 「全库没有序数定义 … 若将来 registry 补出序数定义,须同时改 …」 | 🔁 标已失效 + 列三处权威 |
| `Tests/.../f4_wiring_test.cs:463` | 「全库没有序数定义 … 是本实现的落地约定」 | 🔁 标已失效 + **保留值域门的独立理由**(序数有权威 ≠ 调用方不会传越界值) |
| `Sim/DiseaseSimulation/DiseaseCourse.cs:150-156` | 「是落地约定,不是权威件」 | 🔁 已在第七轮就地加注 |

### 11.2 第 2 项 = 「**先不裁,我去查 24 的生命周期**」(前置 ⑥)

**用户原话**:「先不裁,我去查 24 的生命周期」。

| 项 | 处置 |
|---|---|
| **代码** | **不改** —— 保持拒收「止」无「起」的情形(结果码 `StopWithoutStart`) |
| **登记** | 新立 `OQ-24-8`(`design/gdd/clinic-machine.md` OQ 表),问题 = 「24 是否允许只发止(重连补记 / 玩家离场)」 |
| **注释** | `CareAppliedWriter.cs` 前置 ⑥ + `test_careApplied_stopWithoutStart_noWrite` 各加一条「本前置隐含一个未裁决前提」的注 |

**为什么登记而不直接改**:前置 ⑥ 的拒收是**保守方向** —— 少写一条事件,
**不会伪造「止」**;而放行需要先知道 24 是否真有「只发止」的路径,
否则就是替 24 发明规则(违反 ADR-027 式的「不越权」纪律)。

**⚠️ 双向义务已写死**:若 `OQ-24-8` 裁出「允许只发止」,则**前置 ⑥ 与
`test_careApplied_stopWithoutStart_noWrite` 须同批改** —— 不得只改一边
(只改代码不改测试 ⇒ 测试红但理由错;只改测试不改代码 ⇒ 测试假绿)。

### 11.3 证伪轮 —— **我的第一条断言是假的**(已修,已重证)

> 这一节记的是一次**自己抓自己**的过程。写在最前面:
> **`test_carePhase_enumMatchesForwardingConstants` 的初版抓不住它注释里声称的东西。**

**初版断言**(只有值层):

```csharp
Assert.AreEqual((int)CarePhase.Start, DiseaseCourse.CarePhaseStart,
    "DiseaseCourse.CarePhaseStart 须**转发** CarePhase.Start,不得自成一份硬编码");
```

**证伪法**:把 `DiseaseCourse.cs` 的两条常量从
`(int)CarePhase.Start` 改回**字面量 `0` / `1`**(值完全不变,只是不再转发),
重跑该类测试。

| 变异 | 期望 | 实测 |
|---|---|---|
| **①** registry 序数 `枚举(起=0, 止=1)` → `枚举(起 止)` | `ordinalsMatchRegistry` 红 | ✅ **红**(`EXIT=2`) |
| **②** 转发常量 → 硬编码 `0` / `1`(值不变) | `enumMatchesForwardingConstants` 红 | ❌ **全绿通过**(`EXIT=0`) |

⇒ **② 是一次真实的假判据**:注释写「不得自成一份硬编码」,而判据只比值 ——
值相等时硬编码**必然通过**。这正是「**注释超出判据能力**」,与我此前记过的
「自造断言顶替 AC 语义层」是同一族失败模式。

**修法**(不是改注释迁就断言,而是**加强断言到注释声称的强度**):

新增**源码层判据** —— 读 `DiseaseCourse.cs`,**剥掉注释行**后找 `const int CarePhase*`,
断言 ① 恰有 2 条 ② 每条右值含 `(int)CarePhase.`。

> ⚠️ **必须剥注释**:该文件的 doc 注释里按「原文不删」纪律保留了
> `<c>public const int CarePhaseStart = 0;</c>` —— 不剥会把**历史原文**当成**现行代码**误判。
> 这与 §十 ① 的 `ExemptKinds` 锚串不唯一是**同型陷阱**,本轮第二次踩。

**重证**:

| 项 | 结果 |
|---|---|
| 修好后正常态 | `CareAppliedWriterTest` **12 / 12 passed · 0 failed** |
| 重放变异 ② | **1 failed** = `test_carePhase_enumMatchesForwardingConstants` ✅ **新判据抓住了** |
| 还原 | `sha256sum -c` 三件**全部成功**(未用 `git checkout` / `git stash`) |

**⚠️ 新判据的已知边界**(如实登记,不夸大):
它是**文本级**不是**语义级** —— 抓不住「用别名 / 中间常量绕过」这类写法。
对本场景够用(序数分叉的实际形态就是「有人把转发改回字面量」),
但**不能声称「序数从此不可能分叉」是绝对的** —— 用户裁定的原话是意图,不是可证的定理。

### 11.4 本批的禁借绿声明

| 项 | 状态 |
|---|---|
| 甲(registry) | **已落**(可复算命令 §十 ⑦)· **已证**(变异 ① 转红) |
| 乙①②(枚举 + 转发) | **已落**(§十 ⑧)· **已证**(变异 ② 转红,**初版抓不住、修后抓住**,见 §11.3) |
| 丙(机械断言) | **已落且已跑绿** —— 判据是 **round23 的 XML**(最后一次改动之后),§十 ⑨ |
| `OQ-24-8` | **已登记**(§十 ⑩) |
| **`CareApplied` 生产触发点** | **仍未接线** —— 本轮不涉及,见 §6.2 |
| **构建前门探针轮** | **仍未做** —— 本轮不涉及,见 §6.2 |

> ⚠️ **round21 的绿不能用作本批最终判据** —— §11.3 的修断言发生在 round21 之后。
> 最终判据是 **round23**(见 §七)。这是「借绿」禁令的直接应用:
> 绿是**某一次跑**的属性,不是**某批改动**的属性。
