# ADR-032: 病程终态的 Kind 归属 (PatientOutcomeChanged Kind Ownership)

## Status

Accepted

> **2026-10-21 起草并转 Accepted。** 用户裁定(2026-10-21,S18-2 实施期)**取方案 A**:
> **立新病史流 `Kind` `PatientOutcomeChanged`**,载荷 `{patient_id, disease_id, outcome, seq}`,
> 写者 = **9**,落**病史流**。
> 本 ADR **结清 `PRE-S18-C`** —— 7a 的 `Folded(p)` 条件 ①「终态」在病史流里**没有任何事件载体**,
> 终态不可从流重算(同 ADR-030 结清的「引用却无登记」失效模式,但**更重**:
> 这里连"引用"都是隐式的 —— `persistence-service.md:320` 把 `终态(p)` 归给 9 的 AC-36,
> 而 AC-36 只说「终态(痊愈/死亡)可折叠」,**不给事件载体**)。

## Date

2026-10-21

## Last Verified

2026-10-21

## Decision Makers

dr_guyang(用户 · **2026-10-21 方案 A 裁定**)
· technical-director(起草与裁决)· 9 疾病与伤情模拟(Kind 登记轮所有者 · 唯一写者)
· 7a 持久化服务(`Folded(p)` 条件 ① 的消费者)· 37 病例系统(条件 ③ 的既有载体方,不受影响)

## Summary

7a 的折叠谓词 `Folded(p) = 终态(p) ∧ ¬豁免(p) ∧ ¬未结案(p)`(`persistence-service.md:316`)
三个条件里,**条件 ① 的输入恒为空** —— 病史流 14 支 `Kind` 实测逐条枚举
(`CareApplied` / `CompoundTriggered` / `CompoundExpired` / `DiseaseOnset` / `DrugTreatmentApplied` /
`EmergencyAttempt` / `EmergencyTreatmentApplied` / `EventArrived` / `EventRolled` /
`HistoryFlagChanged` / `InjuryOnset` / `SkillGrown` / `ThreatDeferralCleared` / `ThreatDeferred`),
**零个是终态标记**。最接近的 `InjuryStateChanged` 是 **`stream: world`**(`entities.yaml:2279`),
且其 `author` 注明写「病人侧由 9 的病史流**既有 Kind** 承载」—— **那个「既有 Kind」不存在**
(这正是本条最刺眼处:一条既有注记**指向了一个不存在的载体**,而它从未被任何门扫出来)。

本 ADR 裁决:**立一个 `Kind` `SimEvent.Kind.PatientOutcomeChanged`**,落**病史流**,写者 = **9**,
载荷 `(patient_id, disease_id, outcome, seq)`;`outcome ∈ {Recovered=1, Died=2}`(0 留哨兵);
**`t_end` 不进载荷** —— 它就是事件头部的 `Tick`(见 ④)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(事件边界 · Kind 登记 · 所有权裁决) |
| **Knowledge Risk** | **LOW** —— 纯数据边界裁决,零引擎 API,零 post-cutoff 依赖 |
| **References Consulted** | `design/registry/entities.yaml`(35 支 `SimEvent.Kind` · ADR-024 真源)· `docs/architecture/adr-024-kind-single-source.md`(registry 单一真源 + kindgen 断言 A1–A5)· `docs/architecture/adr-008-case-event-stream.md`(§二 按 Kind 路由 · §六 有界性)· `docs/architecture/adr-005-deterministic-sim.md`(病史流唯一真源 · 主机唯一 `Append`)· `docs/architecture/adr-016-ai-architecture.md`(§一 三源不变量)· `docs/architecture/adr-030-disease-onset-kind.md`(姊妹件:onset 侧的同一裁决形态)· `design/gdd/disease-simulation.md`(F4 九态 :1093-1115 · argmax `d` 补注 :1120 · AC-36 :1924)· `design/gdd/persistence-service.md`(规则八 :151 · F-7a-4 :314-330) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-024**(Accepted —— `entities.yaml` = `Kind` 单一登记真源;新 Kind 须先改 registry 再重跑 kindgen)· **ADR-008**(Accepted —— §二 按 `Kind` 纯函数路由 · §六 有界性)· **ADR-005**(Accepted —— 病史流唯一真源 · 主机唯一 `Append`)· **ADR-016**(Accepted —— §一 三源不变量,本件拒绝 Alt 3 的判据来源) |
| **Enables** | **S18-2 的 `Folded(p)` 四条件实现**(条件 ① 自此有纯流查询载体)· **7a 折叠行 `outcome` / `t_end` 两列有来源** · **9 的终态写者义务具名化** |
| **Blocks** | **S18-2 的四条件实现**(条件 ① 输入恒为空 ⇒ 谓词只能恒 `false` ⇒ 折叠永不发生) |
| **Ordering Note** | 本 ADR **只裁 Kind 归属与登记**(名称 / 流别 / 写者 / 载荷形状 / 有界性);**9 侧终态写者代码**归 9 实现轮(经 `WriterExistenceGate` 具名豁免挂债)。**不重开** `Folded(p)` 的三条件语义 —— 条件 ②③ 原样不动 |

## Context

### Problem Statement

**7a 的折叠谓词条件 ① 没有输入。** `persistence-service.md:320` **在 2026-10-21 补注前**逐字写(原文保留;该单元格现已就地补载体):

> | `终态(p)` | 9 的 AC-36 | 痊愈 / 死亡 |

而 AC-36(`disease-simulation.md:1924`)只说「终态病人(痊愈 / 死亡)可折叠」,**不给事件载体**。
后果不是文档不齐:折叠谓词是**流前缀的纯函数**(ADR-008 §六 的可重建要求),
条件 ① 若不能从流里读出,`Folded(p)` 就只能恒 `false` —— **折叠永不发生**,
ADR-006 Amendment B 的高水位重构、ADR-008 §六 的「病史流无界」问题全部悬空。

### Current State

- **35 支 `SimEvent.Kind`**,病史流 **14 支**(实测逐条枚举,见 Summary)—— **零个终态标记**。
- **`InjuryStateChanged` 不是它**:`stream: world`(`entities.yaml:2279`),
  其 `payload_schema` / `constraint` 注(`:2281` / `:2282`)写「病人侧由 9 的病史流**既有 Kind** 承载」——
  **该 Kind 不存在**。这是本条的**旁证**:一处既有注记指向了不存在的载体,而零机械门扫出它
  (同 `CompoundTriggered` / `ActorCellEntered` / ADR-022 关卡工具的失效模式)。
- **`threshold_transition` 不是 `Kind`**:`entities.yaml:648` 是 **mechanism 条目**
  (`unit: kind` 之外,`output_range` = 九态含「痊愈|死亡」,`expression` = F4 判定式),
  它描述**判定机制**,不是**事件**(Alt 2 已否,见下)。
- **折叠行形状早已含 `outcome` / `t_end`**:`persistence-service.md:161`
  `折叠行 = (onset, 病种_id, patient_id, patient_seed, outcome, t_end)` ——
  **这两列的规格存在,来源不存在**。本条裁决正是给它补来源。

### Constraints

- **`Kind` 唯一真源 = `entities.yaml`**(ADR-024 ①);追加通道 = **registry 直登**
  (ADR-024 ③;Amendment 追加通道已退役)。
- **一个 `Kind` 无法同时落两条流**(ADR-008 §一)。
- **载荷必须 ∈ 整数域**(ADR-024 ② A2;ADR-006 边界 —— 禁 `float`)。
- **条件 ① 的全部输入 ∈ {事件流 + 烘焙数据}**(ADR-016 §一 三源不变量;Alt 3 的判据来源)。
- **`Append` 权 = 主机唯一**(ADR-005);病人侧事件落病史流(与 `DiseaseOnset` / `InjuryOnset` 同流)。

### Requirements

- 立一个 `Kind`,使 9 能在**判出终态的那一 tick**写出一条**可路由、可折叠、可重建**的事件。
- 载荷须使**折叠行 `outcome` / `t_end` 两列可填**(7a F-7a-4 折叠行形状)。
- 载荷须保留 `patient_id`(ADR-006 Amendment B 高水位重构依赖;承 `DiseaseOnset` 先例)。
- **不得**破坏 ADR-008 §六 的有界性论证(Alt 2 的否决理由即此项)。

## Decision

### ① 立一个 `Kind` `SimEvent.Kind.PatientOutcomeChanged`,落**病史流**

`PatientOutcomeChanged` 这一名字**全库零出现**(grep 确认)—— 本 ADR 是它的**命名首现**。

**流别 = 病史流**,判据 = 病人侧事件 + 需随该病人折叠(与 `DiseaseOnset` / `InjuryOnset` /
`CareApplied` 同流)。**不落世界流** —— 世界流永不折叠(ADR-008 §六),
终态若落世界流则折叠谓词读不到它(Alt 3 的变体,已否)。

### ② `outcome` 枚举:`{Recovered=1, Died=2}`,**0 留哨兵**

```csharp
public enum PatientOutcome : int
{
    Unspecified = 0,   // 不是合法终态;谓词 / 解码遇到必须 fail-loud
    Recovered   = 1,   // 痊愈(F4 痊愈支)
    Died        = 2,   // 死亡(F4 死亡支)
}
```

**零值方向**:`Unspecified = 0` 是**漏填哨兵**,不是合法数据形态。理由与 `DiseaseKind` /
`ActorClass` 同款但更硬:**终态是折叠谓词的条件 ①**,一个默认落成 `Recovered` 的漏填值
会**静默地把死亡病人标成痊愈** —— 折叠行 `outcome` 从此撒谎,而折叠**不可逆**(病史行已删)。
故 0 必须是"未声明",并由 **R1 级校验 + 解码 mask 校验**双重拒收。

### ③ 写者(`Append` 调用者)= **9**

判据:
- **9 是终态的唯一判定方** —— F4 九态转移(`disease-simulation.md:1078-1146`)在 9 的 `Step` 里求值,
  终态是它的输出之一;
- **`Append` 权 = 主机唯一**(ADR-005),而 `Step` 亦主机唯一 —— 二者天然同源;
- 与 `DiseaseOnset`(写者 9,ADR-030 ③)**完全同构**:内容归属与 `Append` 调用者重合。

**⚠️ 原子性义务(本裁决的硬代价)**:终态判定与事件写入**必须同 tick 原子**。
若 9 判出终态而不写事件,则「流里有终态」与「F4 重放说终态」**会漂移** ——
而条件 ① 的消费口径是**存在性查询**(见 ⑤),漂移的直接后果是**该病人永不折叠**。
本条登记为 **9 实现轮的具名义务**,并由「条件 ① = 存在性查询」的判据反向逼出:
**任何 `Folded(p) = false` 而 F4 重放为终态的病人,都是 9 漏写事件的证据**。

### ④ 载荷 = `(patient_id, disease_id, outcome, seq)` —— **`t_end` 不进载荷**

```csharp
public readonly struct PatientOutcomeChangedPayload
{
    public readonly int PatientId;    // IIdAuthority 机制 A(折叠行 patient_id 必留)
    public readonly int DiseaseId;    // 取得 position_agg 的那个 d(argmax,见下)
    public readonly int Outcome;      // PatientOutcome 枚举值(Recovered=1 / Died=2;0 非法)
    public readonly long Seq;         // 主机 Append 时分配(ADR-006 Amendment A/C)
}
```

**两处设计判断,逐条给理由:**

**(a) `t_end` 不进载荷 —— 它就是事件头部的 `Tick`。**
折叠行要的 `t_end` = 「终态发生的那一 tick」,而 `SimEvent.Tick`(`SimEvent.cs:32`)
逐字定义即「逻辑 tick(20 Hz)」,且 `PatientOutcomeChanged` 按 ③ 在**判出终态的那一 tick**写出
⇒ `payload.t_end ≡ event.Tick` 恒成立。把它塞进载荷 = **同一量的第二处存储**
(第二真源,ADR-016 §一 的同型病)。折叠行读 `t_end` 时取 `event.Tick`。
⚠️ 这与 `DiseaseOnsetPayload` 的 `OnsetTick` **不同**:`OnsetTick` 是「病程开始的 tick」,
可能早于病人实体被创建的那一 tick(潜伏期),故必须显式携带;`t_end` 没有这种偏移。

**(b) 载荷带 `disease_id` —— 终态是**逐病种**的结论。**
`disease-simulation.md:1120` 的 2026-10-21 补注已钉死:**F4 各支的 `d` = 取得 `position_agg`
的那个 `d`(argmax;并列取 `DiseaseId` 最小者)**。也就是说「谁死了」在 F4 里**不是病人级的**,
而是**病种级的**:病人患麻疹(position 0.9)+ 一道轻刺伤(position 0.2,`lethal=true` 急性保持型)
⇒ `position_agg = 0.9 ≥ DEATH ∧ LethalFor(刺伤)` 的**旧读法会判死**,而 argmax 补注正是为了
消灭这个假绿 —— **致死的是麻疹,不是刺伤**。

若载荷不带 `disease_id`,折叠行 `outcome` 就填不出「死于哪个病」,而 `patient_seed` 不是
`disease_id` 的函数(它 = `hash(world_seed, patient_id)`,规则六 :164)⇒ **不可重建**。
代价 = 4 字节。

**(c) 有界性由此直接给出**(见 ⑤):正因为终态是**逐病种**的,每病人每病种至多一次终态
⇒ 上界 = `Σ_p |D_p|`。

### ⑤ 有界性 —— 每病人 ≤ `|D_p|` 条,**与 tick 频率无关**

`PatientOutcomeChanged` 的事件率 = **终态判定率**,而终态**不可逆**(F4 九态里
`痊愈` / `死亡` 是吸收态,无出边)⇒ **每病人每病种至多写一次**。

**上界**:`Σ_{p} |D_p| ≤ PATIENT_APPEARANCE_CAP × |D_max|`
(`PATIENT_APPEARANCE_CAP = 24`,`TR-disease-021`;`|D_max|` = 单病人同时携带病种数上界,归 9 的 GDD)。
**与 tick 频率无关** —— 终态是**病程演进**的离散事件,不随帧率增长。

本 ADR **扩展** ADR-008 §六 的有界性论证(新增一支病史流 Kind 的界),**不重写**。
⚠️ 与 Alt 2 的对照:**Alt 2(升格 `threshold_transition`)的界是 `≤9 态 × 每病人`**,
因为它写的是**每一次状态转移**;本方案写的是**终态**(吸收态),界小一个量级 ——
这是本方案相对 Alt 2 的**结构性**优势,不是调参差异。

### ⑥ 条件 ① 的消费口径 = **存在性查询**,不是「重放算出来」

`Folded(p)` 条件 ① 的实现 = **「病史流里是否存在该病人的 `PatientOutcomeChanged` 事件」**,
纯流前缀查询,**不需要重放 F4**。

> 🔁🔁 **2026-10-24 口径收紧(`OQ-7a-10` 裁定 A · 用户裁定)—— 原文不删**
>
> 上面那句的「存在」原读作 **∃(任一病种终态即可)**,现收紧为 **∀(每个病种皆终态)**:
>
> ```
> 终态(p) = ∀ d ∈ D_p : ∃ PatientOutcomeChanged(p, d)
> ```
>
> `D_p` = 该病人的 `DiseaseOnset` 病种集合(ADR-030)。**空集 `D_p = ∅` ⇒ false**
> (不取真空为真 —— 误折不可逆)。
>
> **为什么**:多病种**当下可达**,不是将来假设 —— `D-8-6` 共病合成已由用户提级
> **P0 阻塞**,`AC-31` 原文即举「病人同时有 `position = 3/10` 与 `4/5` 两个病」。
> 在 ∃ 口径下「A 已终态、B 仍在演进」会通过条件 ①,`HistoryFolder.Erase` 随即抹掉
> 该病人**全部**病史行(减 `SkillGrown`)⇒ ① B 的病程与 onset 被静默抹除且不产 B 的
> 折叠行;② B 日后终态时流里「有 outcome 无 onset」⇒ 再折必抛 `InvalidDataException`;
> ③ 折叠连 A 的 outcome 一并抹掉而条件 ① 读的是**流** ⇒ 该病人此后再不可折、
> B 的新行**无界增长**。
>
> **本 § 的核心主张不变**:仍是**逐病种存在性查询**、**不重放 F4** —— 收紧的是
> 「查询几个病种」,不是「查询还是重算」。**代价**:一个长期病程会拖住整个病人不折,
> 但 §⑤ 的上界 `Σ|D_p| ≤ PATIENT_APPEARANCE_CAP × |D_max|` 已覆盖,收紧后界只会更小。
>
> **落点**:`Sim/FoldedPredicate.cs` `Terminal()` · `persistence-service.md` F-7a-4 条件① 格
> · 新增 **6** 条测试(谓词层 5 条:`multiDisease_partialTerminal` / `multiDisease_allTerminal` /
> `noOnsetAtAll_notVacuouslyTrue` / `orphanOutcome_throwsInsteadOfBeingSwallowed` /
> `orphanOutcomeAlongsideValidOnset_throws`;端到端 1 条:`fold_multiDisease_partialTerminal_erasesNothing`,
> 断言「不折 ⇒ 流逐条保留」)。
>
> **证伪轮 3/3 全红**(轮次前缀 `OQ-M1..M3`,与 `Erase` 批 / `EntityDeathGates` 批的 `M1–M4` 分名;
> 还原判据 = SHA-256 逐字节 `f676b109…819ed`)。
> ⚠️ **收紧过程实测抓到一处本次引入的真缺陷(已修)**:空集短路会把「只有 `outcome`、无 `onset`」
> 的损坏流静默吞掉 ⇒ 孤儿检查**上移 `Terminal()` 且先于空集短路**。由既有测试
> `test_fold_outcomeWithoutOnset_throws` 抓到,非既有 bug。

这正是本方案相对 Alt 4(只发死亡、痊愈重放)的核心价值:**终态从「重算」变成「记录」**,
与 `DiseaseOnset` 完全同构(onset 也是记录,不是重算)。

**代价如实登记**:该口径把「终态」的权威**从 F4 求值转移到了流内容**。二者若漂移,
**流是权威**(ADR-005「病史事件流是唯一真源」)—— 故 ③ 的原子性义务是**硬**的,
不是风格建议。

### Key Interfaces

```csharp
// Sim.Contracts —— 载荷结构(ADR-024 A2:全整数域)
public readonly struct PatientOutcomeChangedPayload
{
    public readonly int  PatientId;   // IIdAuthority 机制 A
    public readonly int  DiseaseId;   // argmax d(disease-simulation.md:1120)
    public readonly int  Outcome;     // PatientOutcome 枚举值
    public readonly long Seq;         // 主机 Append 时分配
}

// Sim.Contracts —— 终态枚举(0 = 漏填哨兵)
public enum PatientOutcome : int { Unspecified = 0, Recovered = 1, Died = 2 }

// 9 的写路径(唯一写者;判出终态的那一 tick 原子写出)
IEventSink.Append(new SimEvent {
    Kind = EventKind.PatientOutcomeChanged,   // → 纯函数路由到病史流
    Patient = patientId,                       // 病人侧事件(非 PatientId.None)
    Tick = tick,                               // = 折叠行的 t_end
    Seq = seq,
    Payload = encoder.Encode(EventKind.PatientOutcomeChanged, payload)  // ADR-029 IPayloadEncoder
});

// 7a 的条件 ①(纯流前缀查询,ADR-008 §六 可重建)
bool Terminal(int patientId) =>
    events.Any(e => e.Patient.Value == patientId
                 && e.Kind == EventKind.PatientOutcomeChanged);
```

### Implementation Guidelines

- **`entities.yaml` 条目**照 `DiseaseOnset` / `InjuryOnset` 格式(`stream` / `author` /
  `payload_schema` 三字段必填,ADR-024 ①)。
- **⚠️ `EventKind.cs` 的成员必须追加在文件末尾的「追加区」**,**禁止**插到按流分组的位置
  —— 该枚举是隐式值,插入中间成员会让其后全部成员值 +1 ⇒ 历史存档读错 Kind ⇒ 静默数据损坏。
  实测后果见 §Risks 的实施期教训。分组的语义价值由 `StreamRouting.g.cs` 承载,不靠手排。
- **kindgen 重跑**后断言 A1–A5 全过;`PatientOutcomeChanged` 出现在 `StreamRouting.g.cs` 的病史流分支。
- **`WriterExistenceGate` 豁免表**须同批加一条(9 侧写者未落,具名挂债)—— 判据:
  「已存在写者的 Kind 绝不在此表」,故**写者落地的那一批须同批撤表**。
- **`drug_event_test.cs` 的闭集支数哨兵** 35 → 36(该断言逐字写「变更哨兵 —— 若本次变更
  **有意**增删 Kind,须先改 registry 再重跑 kindgen」)。

## Alternatives Considered

### Alternative 1: 立两个 `Kind`(`PatientRecovered` + `PatientDied`)—— **否决**

两条事件的载荷**除 `outcome` 外完全相同**,`outcome` 是 2 值枚举 ⇒ **零独立信息**
(同 ADR-030 Alt 1 的否决理由)。且折叠谓词条件 ① 只问「是否终态」,**不区分**两种终态
(区分只影响折叠行 `outcome` 的取值)⇒ 拆成两条会让条件 ① 变成两次查询,无收益。

### Alternative 2: 升格 `threshold_transition` 为 `Kind` —— **否决**

`threshold_transition`(`entities.yaml:648`)是 **mechanism 条目**:`output_range` = 九态、
`expression` = F4 判定式、`variables` = 阈值清单。升格为 `Kind` 的两处硬伤:

1. **registry 结构冲突** —— 同一 entry 将同时承担「机制描述」与「事件登记」两职,
   `unit` 字段语义打架(`kindgen` A4 要求 `author` 首整数 ∈ 已登记系统号,mechanism 条目无此约束)。
2. **有界性破坏** —— 它写的是**每一次状态转移**,事件量从「每病人 ≤2 条」变成「每病人 ≤9 条」,
   **ADR-008 §六 的有界性论证须重做**(而 ADR-008 §六 明写「拒绝物理折叠」的三条理据之一
   正是「折叠省不下未结案集合」—— 事件量翻 4 倍会反向侵蚀这条论证)。

### Alternative 3: 改折叠读侧,`终态` 由状态机**快照**判定 —— **否决**

两处硬伤:

1. **循环依赖** —— 快照正是 `Folded(p)` 求值的**产物**(折叠后写快照),用它当条件 ① 的输入 = 自指。
2. **违反 ADR-016 §一 三源不变量** —— 决策只能来自 {事件流, 烘焙数据, 二者纯函数};
   快照是**第四来源**(ADR-009 §一 Q1 判据同源)。

### Alternative 4: 只发**死亡**事件,痊愈由 `position_agg < RECOVER_THRESHOLD` 从流重放得出 —— **否决**

1. **不对称** —— AC-36(`disease-simulation.md:1924`)明写「终态病人(**痊愈 / 死亡**)可折叠」,
   两态并列。不对称实现会让折叠行 `outcome` 的 `Recovered` 分支**无来源**。
2. **痊愈重放需要完整 `position` 历史** —— 而完整历史正是折叠要**删掉**的东西
   (折叠的动机就是病史流无界)⇒ 逻辑上自相矛盾:折叠越成功,重放越不可能。

### Alternative 5: 复用 `InjuryStateChanged`(世界流)—— **否决**

1. **流别冲突** —— 它是 `stream: world`,而世界流**永不折叠**(ADR-008 §六)⇒
   条件 ① 读它,则该病人**永不折叠** —— 与折叠的目的相反。
2. **载荷语义不符** —— `InjuryStateChanged` 描述**伤情**状态,不是病程终态;
   疾病(非伤情)的痊愈 / 死亡无字段可承载。
3. **旁证**:其 `payload_schema` / `constraint` 注(`entities.yaml:2281` / `:2282`)自称「病人侧由 9 的病史流既有 Kind 承载」
   —— 而那个 Kind **不存在**。本条裁决恰好**兑现了那句注记**,而非复用它。

## Consequences

### Positive

- **`Folded(p)` 条件 ① 有载体** —— 三条件自此全部可从流前缀求值,折叠谓词可落。
- **折叠行 `outcome` / `t_end` 两列有来源** —— `persistence-service.md:161` 的规格不再是悬空文字。
- **兑现 `entities.yaml:2281-2282` 的既有注记** —— 「病人侧由 9 的病史流既有 Kind 承载」从**假陈述**变**真陈述**。
- **与 `DiseaseOnset` 完全同构** —— onset / 终态两端对称,9 的病史流首尾闭合。
- **有界性界小** —— 吸收态 ⇒ 每病人每病种至多一条,远小于 Alt 2 的 `≤9 态`。

### Negative

- **原子性义务是硬的** —— 9 必须**同 tick** 判终态 + 写事件,否则流与 F4 重放漂移。
  这是本裁决最实的一笔债,且**没有机械门能扫**(只能由「`Folded(p)=false` 而 F4 说终态」反查)。
- **多一条 Kind 的完整接线面** —— payload struct / 编解码 / encoder 分派 / registry /
  kindgen / 豁免表 / 闭集哨兵,七处。
- **`|D_max|` 未定** —— 有界性上界里 `Σ_p |D_p|` 的第二因子归 9 的 GDD,
  本 ADR 只能给出形状不能给出数值(与 `PATIENT_APPEARANCE_CAP` 已定形成对照)。

### Neutral

- 「内容归属」(9)与「`Append` 调用者」(9)在 `PatientOutcomeChanged` 上**重合** ——
  与 `DiseaseOnset` 同格,非新形态。
- 折叠行的 `t_end` 取自事件头部 `Tick` 而非载荷字段 —— 与 `DiseaseOnsetPayload.OnsetTick`
  看似不一致,但理由明确(见 ④a),不是风格分歧。

## Risks

> ⚠️ **实施期实测教训(2026-10-22,本件落地时踩到)—— 枚举成员必须追加在末尾**
>
> `EventKind` 是**隐式值枚举**(成员无显式 `= N`),值 = 声明序号,而序列化按 `int` 落盘
> (`SimEventCodec` + ADR-012 的黄金字节夹具)。起草时把 `PatientOutcomeChanged` 插在
> `DiseaseOnset` 之后(按病史流分组排列,「看起来更整齐」)⇒ **其后 33 个成员值全部 +1**
> ⇒ `SkillGrown` 由 8 变 9 ⇒ 两条黄金夹具**同时转红**:
> `test_simCodec_simEventHeader_matchesGoldenBytes`(字节 24:`Expected: 8 But was: 9`)与
> `test_ac21a28_allScenarioHashes_matchGoldenV1`(`B03_sim_event_header_bytes` 哈希不符)。
>
> **这两条是 ADR-012 的确定性回归门,红了先查实现、禁改期望值**(ADR-012 裁决③)。
> **修法 = 移到枚举末尾**(零位移 ⇒ 黄金字节不变 ⇒ 无需重签金标准),
> 并在 `EventKind.cs` 立「追加区」注释块把纪律写进代码。
>
> **教训**:「按语义分组排列」与「枚举值稳定性」在这里**直接冲突**,后者优先 ——
> 成员序的语义价值由 `StreamRouting.g.cs`(kindgen 产物)承载,不靠手排。
> 本件**未**刷新任何黄金夹具(若当时刷新了,就是拿 ADR-012 的纪律换一时的绿)。

> ⚠️ **实施期实测教训之二(2026-10-22)—— 通用往返夹具的隐含前提被本件打破**
>
> `payload_encoder_test.cs` / `payload_decoder_test.cs` 各有一条「全种类通用往返」夹具
> (`test_ac2908_allEncodableKinds_roundTrip` / `test_adr031_allKinds_roundTripThroughDecoder`),
> 其 `SamplePayloadFor` 的 `default` 分支注释逐字写:**「余 N 支无数组字段 ——
> default 构造即合法(零值全部通过约束)」**。本件的零值守卫(②)**有意打破**该前提:
> `PatientOutcomeChangedPayload` 的 `default` 恰是 `Outcome = 0`(Unspecified)⇒ 被拒。
>
> **处置 = 给该 Kind 补具名样本**(不是放宽守卫):两条夹具各加一个 `case`,造合法终态。
> **判据**:守卫本身另有**独立夹具**覆盖(`PatientOutcomeKindTest.test_codec_unspecifiedOutcome_isRejectedAtDecode`),
> 故「补样本」不构成绕过 —— 若哪天守卫被删,那条独立夹具仍会红。
>
> **教训**:「全种类通用夹具」隐含「所有载荷的 default 都合法」这条**从未写下的前提**。
> 任何新增零值守卫都会撞上它。正确反应是**补样本**,错误反应是**放宽守卫**。



| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 9 判出终态却漏写事件 ⇒ 该病人永不折叠 | 中 | 中 | ③ 的原子性义务具名化;反查判据 = 「`Folded(p)=false` 而 F4 重放为终态」 |
| `outcome` 漏填落成 `Unspecified=0` 而解码放过 | 低 | 高(折叠行撒谎且不可逆) | ② 的 0 哨兵 + R1 级校验 + 解码 mask 校验双重拒收 |
| 折叠行 `outcome` 被读成「病人级结论」而非「病种级结论」 | 中 | 中 | ④b 明文:终态是**逐病种**的(argmax `d`,`disease-simulation.md:1120`);载荷带 `disease_id` 即该读法的结构约束 |
| 与 `InjuryStateChanged` 混淆(测试 / 实现误标) | 中 | 中 | Alt 5 明文否决 + 载荷字段零重叠(本件无 `injury_id` / `magnitude`) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a | 无变化(每病人每病种一次 `Append`) | — |
| Memory | n/a | 无变化(载荷 = 3 整数 + 1 long,ADR-029 `IPayloadEncoder` 编码入池) | — |
| Load Time | n/a | 无变化 | — |
| Network | n/a | 病史流事件走主机权威通道(ADR-005 主机唯一 `Append`) | — |

## Migration Plan

1. **本批(2026-10-21)**:
   - `entities.yaml` 追加 `SimEvent.Kind.PatientOutcomeChanged` 条目
     (`stream: history` / `author: "9 …"` / `payload_schema` 四字段);
   - `EventKind.cs` 加成员 + 头注 35 → 36;
   - `PatientOutcomeChangedPayload` struct + `PatientOutcome` 枚举;
   - `PayloadCodec.History.cs` 编解码 + `PayloadCodec.cs` / `PayloadEncoder.cs` 分派;
   - kindgen 重跑 → `StreamRouting.g.cs`;
   - `WriterExistenceGate` 豁免表加一条;
   - 闭集支数哨兵 35 → 36;
   - `disease-simulation.md` 规则六清单 + 边界表 + AC-36 加注;
   - `persistence-service.md` F-7a-4 条件 ① 加注;
   - `adr-008 §六` 有界性扩展注记。
2. **9 实现轮**:F4 终态判定处 `Append(PatientOutcomeChanged)`(原子性义务,见 ③);
   同批**撤** `WriterExistenceGate` 豁免条目。
3. **7a 实现轮**:`Folded(p)` 条件 ① 按 ⑥ 的存在性查询落地。

**Rollback plan**:纯 `Kind` 登记与所有权裁决,回滚 = 撤销 `entities.yaml` 条目 + 恢复枚举与
codec 分派 + 恢复 ADR 日志;无数据受影响(9 写者尚未实现)。

## Validation Criteria

- [ ] `entities.yaml` 含 `SimEvent.Kind.PatientOutcomeChanged` 条目,三字段齐备且 `stream: history`(ADR-024 A1 / A4)
- [ ] 载荷全字段 ∈ 整数域(无 `float` / 引用类型 —— ADR-024 A2 / ADR-006)
- [ ] kindgen 重跑后 `PatientOutcomeChanged` 落 `StreamRouting.g.cs` 病史流分支,断言 A1–A5 全过
- [ ] 编解码往返无损(`Encode` → `Decode` 逐字段相等)
- [ ] `outcome = 0`(Unspecified)被解码 mask 校验拒收
- [ ] 载荷含 `patient_id`(7a F-7a-4 折叠行「`patient_id` 必留」成立)
- [ ] 闭集支数哨兵 35 → 36 同步(`drug_event_test.cs`)
- [ ] `WriterExistenceGate` 豁免表含本 Kind 且 registry 有 `author` 声明
- [ ] **未及**:9 的终态写者(归 9 实现轮)· `Folded(p)` 本体(归 S18-2 / 7a)· `|D_max|` 数值(归 9 的 GDD)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | AC-36 :1924「终态病人(痊愈 / 死亡)可折叠」—— 须有事件载体 | 立 `PatientOutcomeChanged`,`outcome` 两值对应两终态(①②) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | 规则六 :174 边界表「病程类(onset / 处置 / … / **终态折叠**)」 | 「终态折叠」挂具名 `PatientOutcomeChanged`(①) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | :1120 argmax `d` 补注 —— F4 各支的 `d` = 取得 `position_agg` 的那个 | 载荷带 `disease_id` 使终态的**病种级**语义可落(④b) |
| `design/gdd/persistence-service.md` | 7a 持久化服务 | F-7a-4 :316 `Folded(p) = 终态(p) ∧ ¬豁免(p) ∧ ¬未结案(p)`,条件 ① 归 9 的 AC-36 | 条件 ① 的输入载体 = `PatientOutcomeChanged`;消费口径 = 存在性查询(⑥) |
| `design/gdd/persistence-service.md` | 7a 持久化服务 | :161 折叠行 `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)` | `outcome` ← 载荷 `outcome`;`t_end` ← 事件头部 `Tick`(④a) |
| `design/registry/entities.yaml` | registry | `InjuryStateChanged` 的 `author` 注(:2280)「病人侧由 9 的病史流既有 Kind 承载」 | 该「既有 Kind」**此前不存在**;本件使其成立(Alt 5 的旁证) |

## Related

- **姊妹件**:`docs/architecture/adr-030-disease-onset-kind.md` —— onset 侧的同一裁决形态
  (命名首现 / 流别 / 写者 / 载荷 / 有界性)。本件是**终态侧**的对称补口。
- **结清**:`production/sprint-status.yaml` 的 `PRE-S18-C`(S18-2 实施期实测的四条前置之一)。
- **被否**:Alt 2(升格 `threshold_transition` —— registry 结构冲突 + 有界性破坏)·
  Alt 3(读快照 —— 循环依赖 + 违反 ADR-016 §一)·
  Alt 4(只发死亡 —— AC-36 两态并列 + 痊愈重放与折叠目的矛盾)。
- **不改动**:ADR-008 §六 的折叠豁免与「病例流不折叠」裁决原样;**只扩展**其有界性论证。
