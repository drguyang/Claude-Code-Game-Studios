# ADR-008: 病例事件流(Case Event Stream)

## Status

Accepted

> **2026-09-15 用户裁定转 Accepted。** 起草过程:三项设计点(跨流全序 = 流优先级 ·「已处置」=
> 处置事件为证 · 盐 = WorldSeed 派生)用户已选;框架经 Opus TD 复核「需调整非否决」,
> 五点修订已并入(Problem 补三条驱动约束 · 全序键补 `Patient` · 处置证据窗口化 +
> 快照 · 保密措辞收窄 · Dependencies 补 7a / ADR-001 / 13)。**2026-09-15 用户裁定 Accepted。**
> 依赖本 ADR 的 AC(AC-37-01…23 中的相关项)解除 `[BLOCKING]`。
> 残留实现义务:7a 病史流折叠谓词 `Folded(p)`(含「无未结案病例」条件)· `max(patient_id)`
> 重构扫三流并集(ADR-009 Accepted 后,含世界流)· 9 侧补登记「病人出现率上限」配置项(有界性硬界)。

## Date

2026-09-15

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core(确定性模拟 / 事件流) |
| **Knowledge Risk** | HIGH(Unity 6.3 post-cutoff;本裁决刻意不用 post-cutoff API —— 同 ADR-005) |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · 无模块参考(域 = 纯逻辑,无引擎 API) |
| **Post-Cutoff APIs Used** | None(全部为纯 C# 值类型与接口契约) |
| **Verification Required** | 事件流序列化探针(EditMode)· 跨流全序单测 · 有界性单测(写入率上限) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-005(确定性模拟 / 抽象点 / 折叠规则)· ADR-006(Amendment A–D:SimEvent 形状 · `patient_id` 重构 · `Seq` 发放域与跨流全序键 · 真源 = 两流并集)· ADR-007(IEventAuthority · WorldSeed 归 7a · PatientId.None 哨兵)—— **仅这三者是前置**,三者均须 Accepted。 |
| **Enables** | 37 病例系统 GDD 的 `CaseOpened` / `CaseClosed` / `PatternRecognized` 实现;F-37.1 fires-once · F-37.2 case_id 三元组 · F-37.3 载荷集合落盘;ADR-001 追加约束(联机选型须承载两条逻辑流 · ADR-009 Accepted 后为三条) |
| **Blocks** | 37 病例系统实现(依赖 AC-37-05/15 事件化的代码)· 53 医疗后果与责任(P0,订阅结案事件) |
| **Ordering Note** | ADR-008 是 37 复核 12 项 blocking 的前置。先 Accepted 本 ADR,再修订 case-system.md<br><br>**引用(非前置)—— 三段内容均为本 ADR 正文定稿之后追加,故记于本行而非 `Depends On`**(承 `architecture.md` §5.2 修复:后补的「引用」若留在 `Depends On`,会把本 ADR 拉进 `{008,009,010,014}` 强连通分量,使「谁是谁的前置」不可判定):<br>① **ADR-009**(🟢 2026-09-15 Accepted 后追加):世界流为第三条流;ADR-009 **扩展**本 ADR §一 的路由纯函数(`IEventSink.Append` 按 `Kind` 路由)。两 ADR 的先后即本行,<br>② **ADR-014**(🔴 2026-09-17 追加):`Judgment.lexicon_id` 的 ordinal 词表**经 ADR-014 烘焙管线产出**;`freehand_text` 例外登记于 ADR-006 Amendment A,<br>③ **ADR-010 §七**:`ConfigVersion` 不匹配非致命 —— 属**就地修订**(本 ADR §三 初稿的「拒载」措辞已据此划除作废),是「后件改前件」的**信息流**,不是「前件支撑后件」的**依赖流**。 |

## Context

### Problem Statement

37 病例系统的生命周期事件需要一个确定、可重放的存储与全序。现状有三个死结:

1. **终态折叠破坏 case_id 与 fires-once**:ADR-005 §Implementation Guidelines 5 规定终态病人折叠成一行并**丢弃流位置**。
   折叠后 case_id(原 = 流位置)不可重构;同病种病例被重数 3→1,第 4 例**重发** `PatternRecognized`
   ⇒ 双重世界后果。
2. **「已处置」无事件佐证**:若「可结案」只看玩家自述(复选框),则玩家可**空手连关三例**触发 Boss,
   与支柱四(进度挂处置不挂结果)冲突。**且病人终态折叠会删掉处置事件本身** —— 证据与折叠耦合(D-37-A 真身)。
3. **Kind 集不全**:37 的 GDD 只登记了三个 Kind,但复核 #5(AC-37-05)要求读数 / 落笔 / 改写史
   **全部事件化**。这些记录类事件落哪条流,决定病例流是否真有界。

**需要裁决**:病例事件落在哪条流?跨流如何全序?载荷形状?折叠与证据的关系?

### Constraints

- 历史事件流是唯一真源(ADR-005);不得破坏既有全序与重构不变量
- 掷骰的每个输入必须可从事件流重构(ADR-007 核心不变量)
- 事件流必须**有界**或有可论证的上限(ADR-005 动机:开放世界流无界 ⇒ 风险列概率 = 高)
- `disease_id` 不得泄漏到呈现层(规则九 · AC-37-15)—— 注意:本 ADR 不再把「保密」当技术目标
  (见 Decision 五,盐是防御纵深不是保密层)
- 零新全局计数器(沿 ADR-006 Amendment B「从流重建」口径)
- 联机 authority 迁移后一切可重放(ADR-005 · ADR-007)
- **8 铁律②**:读数 / 病名 / 置信度**不回写病史事件流**(diagnosis-system.md §Core Rules 铁律②)—— 玩法层记录
  不得污染 9 的模拟真源

### Requirements

- 必须支持病例生命周期:立案 → 结案 → 模式识别,三者均可从流重构
- 必须支持 fires-once:同病种 ≥3 例结案只触发一次
- 必须支持「已处置」为可结案前置,且可重放
- 必须满足 AC-37-05:判断记录(落笔 / 改写)全部事件化,跨迁移不丢
- 必须定义跨病史流 / 病例流两条流的全序

## Decision

**裁决:引入独立病例流(病史流之外的第二条逻辑流),定义其路由、跨流全序、
三 Kind + 判断记录 Kind、处置证据快照、有界性论证。病例流不物理折叠。**

### 一、第二条逻辑流与 `IEventSink` 路由

病例事件落入**病例流**(独立于病史流的第二条逻辑流)。`IEventSink` 增加**按 Kind 路由**:

```
IEventSink.Append(in SimEvent e)
  → 按 e.Kind 路由:
     Kind ∈ { 病史类(含处置事件) }                         → 病史流
     Kind ∈ { CaseOpened / CaseClosed / PatternRecognized
              / JudgmentRecorded / JudgmentRevised }      → 病例流
     Kind ∈ { Structure* / Drop* / Craft / ResourceHarvested } → 世界流(ADR-009 §三)
```

> **2026-09-15 ADR-009 扩展**:本 ADR 定义第二条逻辑流;ADR-009 追加**第三条逻辑流
> 「世界流」**(建造 / 掉落 / Craft / 资源点)。路由扩为三流纯函数白名单,`StreamId`
> 扩为 `{ History, Case, World }`。**本 ADR 的病例类路由不变** —— 世界类 Kind 与
> 病例类互不干扰,见 ADR-009 §二 域归属规则。

- **不改 ADR-005 的 `SimEvent` 形状**(沿用 ADR-006 Amendment A `{Tick, Patient, Seq, Kind, Payload}`)
- **路由是纯函数**:由 Kind 决定落流,主机在 Append 时执行;两条流**共享同一 `Seq` 发放器
  与同一 `(Tick, Patient)` 计数域**(`Seq` **不分流**)

  > **2026-09-15 修正(C-4)**:原句括号注为「计数器从 0 起,**按流分别单调**」—— **该注已作废**。
  > 若两流各自从 0 计数,`Seq` 跨流不可比 ⇒ `Patient` 之后的比较退化为无定义 ⇒
  > F-37.1「全序无平局」的前提失守。`Seq` 的发放域与跨流全序键以
  > **ADR-006 Amendment C** 为准:`(Tick, StreamPriority, Patient, Seq)`。
- **9 的处置事件仍在病史流**(9 自己的 F1/F4 求值需要 `polarity` / `Offset` / `τ_half`;
  病史流是它的唯一真源 —— 不改动)
- **读数事件不在本 ADR 定归属**:读数存档归 39(diagnosis-system.md §Core Rules 铁律② 的邻接段,39 无 GDD)——
  若 39 选择 SimEvent 化,走病例流;否则 39 自有持久化。留 39 GDD
  > ⚠️ **2026-09-21 历史注**:「39 无 GDD」是本 ADR(2026-09-15)时的状态;39 后已立
  > `diagnosis-system.md`。「留 39 GDD」的悬置已由下条 ⭑2026-09-19 裁定结清,本注只补记事实状态。
- **⭑ 2026-09-19(39 首轮评审裁定)**:**读数归属已定** —— 39 = **分册态层**(不进三流,
  跨存档承载于 ADR-010 的「分册态段」);`JudgmentRecorded` / `JudgmentRevised` 载荷
  **补 `author_player_id`**(作者轴,判定取法改 `J(c,p)` = 该案最后一条作者 = p 的判断事件);
  痕 = 病例流按作者过滤的**读时投影**。**本处原文「留 39 GDD」就此结清。**

### 二、跨流全序键

```
sort(a, b) := (Tick asc) → (流优先级:病史流 < 病例流) → (Patient asc) → (Seq asc)
```

> **2026-09-15 ADR-009 扩展**:`StreamPriority` 升为**三值** —— 病史流 < 病例流 < **世界流**
> (ADR-009 §三 / ADR-006 Amendment E)。键形状不变,取值域以 ADR-009 为准。

**`Patient` 不可省略**:`Seq` 只在同一 `(Tick, Patient)` 内单调(ADR-006 Amendment A),
同 tick 两位病人各有 `Seq=0` —— 缺 `Patient` 则平局,而 F-37.1 的全部正确性建在
「全序无平局」上(Opus 复核修正)。流水号仍复用 ADR-006 的 `Seq`,不新造计数器。

### 三、Kind 全集与载荷形状

| Kind | Payload | 说明 |
| --- | --- | --- |
| `CaseOpened` | `{ patient_id, disease_snapshot }` | 立案:快照 `patient_id` + 病种集快照。**立案 tick = 本事件自身的 `SimEvent.Tick`**(2026-09-17:`opened_tick` 字段已删,见下) |
| `CaseClosed` | `{ patient_id, case_id, disease_set, treated }` | 结案:**病种集**(可为空,一案进多组见 F-37.3)+ `treated` 证据快照(见 §四)。**结案 tick = 本事件的 `SimEvent.Tick`**(`opened_tick` 字段已删) |
| `PatternRecognized` | `{ patient_id: PatientId.None, anchor_case: CaseId, salted_key: ulong, member_set }` | 模式识别:用 `PatientId.None` 哨兵(不污染高水位,ADR-007 §四);`anchor_case` = 触发识别的那一例(锚点);**`salted_key`** = 加盐哈希键(**2026-09-17 补**,原只有前两字段 ⇒ 53 无法定位是哪个 `D`);载荷携带**冻结的 MemberSet 三元组** |
| `JudgmentRecorded` | `{ patient_id, case_id, judgment, author_player_id }` | 落笔(病名 + 置信度),锚定到具体病例;**⭑ 2026-09-19:补 `author_player_id`(作者轴,39 首轮评审裁定)** |
| `JudgmentRevised` | `{ patient_id, case_id, judgment, author_player_id }` | 改写史(永不进计分,AC-37-20)。**⚠️ 2026-09-17:语义 = 存新值**(`Judgment` 快照改写后的完整判断,不存旧值、不做差分);判定取法 = **全序最后一条胜**(`OQ-51-8` / 37 的 `AC-51-B3` 已裁)。**⭑ 2026-09-19:补 `author_player_id`;判定取法随之改判 `J(c,p)` = 该案最后一条作者 = p 的判断事件(按作者分轴);痕 = 按作者过滤的读时投影**。**`Judgment` 的形状**见下方注解 |

> **2026-09-15 就地修正(B-2,用户裁定取 (b) 降维)**:
> 原稿此处的字段名为 `anchor: long`,并在 §四 与 `case-system.md` 的「已处置」式写
> `c.anchor.Tick` ——
> **对一个「不透明 `long`」取成员,既非 `long` 的成员、也非 52 的枚举,无法实现**;
> 且与 `random-events.md` 的事件池表 `spawn_anchor` 行(生成点枚举)**同名异型**(改名前的旧名 `anchor`)。
> 现全部改名定型:`anchor` → `opened_tick`(立案 tick)/ `anchor_case`(触发识别的病例);
> 52 侧的生成点枚举同步改名 `spawn_anchor`。**「不透明」措辞一并删除** ——
> 它当初的动机是「13 无 GDD,故不定义内部形状」,但代价是引入一个
> **没有定义、没有消费者、却参与类型运算**的字段。
>
> **🔴 2026-09-17 再订正**:上条引入的 `opened_tick` **现予删除** ——
> 它**就是本事件自身的 `SimEvent.Tick`**,载荷中重复携带即「同一事实两份真相」
> (违 ADR-006 原则)。本轮同时删 `CaseClosedPayload` 的同名字段;
> `anchor_case: CaseId` **保留**(它是**跨事件引用**,不是本事件的时间戳)。
> **本 ADR 与 37 GDD 的 `opened_tick` 引用现统一为该事件的 `Tick`。**

> **📌 `Judgment` 的形状(2026-09-17 定型,用户裁定)** —— 此前**本 ADR 与 37 GDD
> 均未定义**该型,是**第六处泄漏面**(独立于 AC-37-15 的 DTO 检查):
> ```
> struct Judgment {
>     uint16  lexicon_id;      // 病名词表条目(版本化 ordinal,烘自 ADR-014 管线)
>     uint8   confidence;      // 置信度档(非浮点)
>     string  freehand_text;   // 玩家自书 —— 只进呈现层,【永不进任何判定】
> }
> ```
> **⭑ 2026-09-19:载荷外层补 `author_player_id`**(作者轴,不入 `Judgment` 本体 —— 词表 / 置信度 /
> 自由文本是「写了什么」,作者是「谁写的」,两轴正交)。
> - `lexicon_id` 取自 `case_judgment_lexicon`(经 ADR-014 两阶段烘焙);
>   **词表与 9 的病种注册表在构建期做「非一一对应」校验**(否则换名不换壳,泄漏照旧)。
> - `freehand_text` 是**自由文本**,对 ADR-006 Amendment A「Payload 无引用字段」
>   **作例外登记**:它是**纯呈现态**,不参与任何判定语义。
> - **空判断合法**(`S-8.3`):`lexicon_id = 0 ∧ freehand_text = ""` 表示空栏。

- **载荷全部为值 struct,禁 float,经 ADR-006 §五自定义编码器序列化**
- `disease_set` 是集合(可空):一案 overlap 到多病种时进多组(共病,F-37.3)
- `JudgmentRecorded` / `JudgmentRevised` 满足 AC-37-05(记录全部事件化),落病例流
  而非病史流 —— **不违反 8 铁律②**(那条禁的是回写 9 的病史流)
- 盐键:世界级 `PatternRecognized` 用 **逐 `D`** 派生的 `SplitMix64(WorldSeed, "case-salt", ordinal(D))`
  (见 §五 的 2026-09-17 口径订正 —— 原写「全局单值」已作废)

### 四、可结案与处置证据快照

```
可结案(c) := c.state = 开
           ∧ ∃ treatment event e ∈ 病史流: e.Patient = c.patient_id
             ∧ e.Tick ∈ [c.opened_tick, c.CloseTick]     // ✅ 2026-09-17 方向订正,见下
           ∧ 玩家已勾选「已处置」(UI 仪式,不产生事件)
```

> **🔴 2026-09-17 就地订正(方向写反)** —— 原文为
> `∧ c.opened_tick ∈ [e.Tick, c.CloseTick]`,读作「**立案 tick 落在处置 tick 与结案 tick
> 之间**」,即**「处置发生在立案之前也可以」**。该式恰好使
> **「复诊第二例凭首诊的处置通过前置」成为唯一可通过路径**(首诊的 `e.Tick`
> 早于第二例的 `opened_tick`)⇒ **本条紧接下一行自我宣称要堵的 Opus 复核 #4
> 后门,在公式层实际未堵**。37 GDD 的 `F-37.2` 照抄了同一错误。
>
> **正确式**:`∧ e.Tick ∈ [c.opened_tick, c.CloseTick]`
> —— 「**处置 tick 落在立案与结案之间**」。
>
> **为何长期未被发现**:2026-09-15 本节的修正记录只改了**字段名**
> (`c.anchor.Tick` → `c.opened_tick`),**未改区间方向** —— 修订痕迹看似完整,
> 逻辑实则相反。教训:涉及**区间方向**的订正必须独立复核,不能只看改名。

- **证据快照**:`treated = true/false` **快照进 `CaseClosed` 载荷**(同 `disease_id` 快照
  的理据,复核 #2)。重放永不跨流查询;折叠也无害
- 跨流验证(结案时查询病史流)只发生在**写事件当下**,快照后重放不再依赖跨流
- 「已处置」= 病史流存在处置事件 **∧** 该事件落在 `[CaseOpened.Tick, CloseTick]` 窗口内
  (堵「复诊第二例凭首诊处置通过前置」的后门,Opus 复核 #4 —— **订正后才真正成立**)
- **终态折叠删处置事件的耦合由此解掉**:结案发生在写 CaseClosed 当下,
  此时病史流尚未折叠该病人(折叠由 7a 在终态后执行);即便折叠先行,
  `treated` 已快照,重放不需要处置事件

### 五、保密语义与盐(收窄)

- 规则九保密 = **「player 不可见」**(呈现层 DTO 静态检查,AC-37-15),**不是**「client 不可知」
  (病史流明文 `onset` 事件与存档已含病种;ADR-005 客户端持流副本)
- `PatternRecognized` 载荷用**加盐哈希键**(`salted_key: ulong`)而非裸 `disease_id`;
  盐 = `SplitMix64(WorldSeed, "case-salt")` **派生**、**全局**。**盐的输入集**:WorldSeed + 病种枚举键
- **P0 ≤ 8 病种的枚举空间 ⇒ 加盐键必然可逆** ⇒ 盐**不是保密层**,是**防御纵深**
  (防误手/防脚本把 `disease_id` 顺进 UI)。规则九的**唯一**落点 = AC-37-15 的 DTO 静态检查
- 53 结算时用**重算** `salted(d)` 匹配 R3 表;构建期做**无碰撞断言**
- **🔴 2026-09-17 口径订正(自相矛盾择一)**:本节原文写「盐**全局**」而输入集又含
  **病种枚举键** —— 若盐是全局单值,则所有 `D` 加出**同一个** `salted_key`,
  **53 无法区分是哪个 `D` 触发**;若逐 `D` 派生,则「全局」措辞为误。
  **裁定:逐 `D` 派生** —— `salted_key(D) = SplitMix64(WorldSeed, "case-salt", ordinal(D))`。
  「全局」原意 = **盐不按存档 / 玩家分叉**(单一 `WorldSeed`),该意保留;
  但**每 `D` 一个键**。§三 载荷表与 37 的 AC-37-14 已同步。

### 六、有界性与折叠(拒绝物理折叠)

- **折叠豁免**:病人的终态折叠(ADR-005)只作用于**病史流**;**病例流不折叠**。
  病例事件(CaseOpened / CaseClosed / PatternRecognized / JudgmentRecorded / JudgmentRevised)
  在病例流中**永久保留**
- **有界性论证(本质差异,非「游玩时间」托词)**:
  病史流无界,是因为病人由 9 的环境模拟**自动生成**(无玩家动作约束);
  **病例流的写入者只有玩家** —— 立案 = 一次就诊交互,落笔 / 改写 = 一次脉案操作,
  PatternRecognized = 世界级,每次 fires-once 一次(≤ 病种数)。
  故病例流写入率受**玩家操作速率**约束,并以**立案率 ≤ 病人出现率**(9 配置上限)
  作硬界。**论证链**:`病例流增长率 ≤ 立案率 × 玩家数(≤ 4) ≤ 病人出现率(9 配置常数) × 4`
  (⭑ 2026-09-19:作者轴引入后重证 —— 每位玩家各发各的落笔 / 改写,事件总量乘玩家数常数倍,上界仍成立)。
- **拒绝病例流物理折叠**(Opus 的 Alternative D)三条理据:
  ① **未结案案例永不折叠**(规则六「系统永不自动结案」)⇒ 折叠省不下未结案集合;
  ② **改写史「为玩家回看」需保留完整历史**(规则四)⇒ 折叠行是定长,塞不下变长改写史,
  折叠 = 破坏回看内容 = 违反 AC-37-05;
  ③ 折叠行要承载 case_id 三元组 + disease_set + treated + MemberSet,不比事件小多少,
  收益不成立

### 七、重构与幂等

- `max(patient_id)` 重构**扫两流并集**(ADR-006 Amendment B;ADR-009 Accepted 后为**三流并集**),
  哨兵 `PatientId.None` 显式排除
- 幂等拒收(复核 #10):同一 `case_id` 第二条 `CaseClosed` 被拒,**不产生事件**;
  判定 = 流前缀的纯函数
- 判断记录类事件**不参与 F-37.1**(同源判定只用 CaseClosed 的 `disease_set`),
  但参与病例流的全序

### Architecture Diagram

```
                    ┌──────────────────────────────┐
                    │  7a 持久化                    │
                    │  存档头: WorldSeed · 配置版本  │
                    │  病史流折叠(Folded(p)) ← §六   │
                    └──────────────┬───────────────┘
                                   │
              ┌────────────────────▼────────────────────┐
              │  IEventSink.Append(in SimEvent)          │
              │  → 按 Kind 路由(§一)                      │
              └───────────┬────────────────┬─────────────┘
                          │ 病史类 Kind     │ 病例类 Kind
                          ▼                ▼
              ┌────────────────────┐  ┌─────────────────────────┐
              │  病史流(9 拥有)      │  │  病例流(37 拥有)          │
              │  处置事件 · 病史事件  │  │  CaseOpened · Closed ·   │
              │  终态折叠(AC-36 豁免) │  │  PatternRecognized ·    │
              └────────────────────┘  │  JudgmentRecorded/Revised│
                                      │  不折叠(§六)              │
                                      └─────────────────────────┘
  跨流全序键: (Tick, StreamPriority, Patient, Seq)   ← §二
  结案时跨流查询(处置证据)只在写当下;treated 快照入 CaseClosed(§四)
```

### Key Interfaces

```
// 新增(路由逻辑,不改 SimEvent 形状)
enum StreamId { History, Case, World }   // 病史流 / 病例流 / 世界流(ADR-009 §三)

public interface IEventSink        // 扩展:按 Kind 路由
{
    void Append(in SimEvent e);    // Kind → StreamId 纯函数路由(三流白名单)
}

// 载荷(值 struct,禁 float,经 ADR-006 §五编码器)
struct CaseOpenedPayload      { PatientId patient_id; DiseaseIdSet disease_snapshot; }
struct CaseClosedPayload      { PatientId patient_id; CaseId case_id;
                                DiseaseIdSet disease_set; bool treated; }
struct PatternRecognizedPayload { PatientId patient_id; CaseId anchor_case;   // PatientId.None 哨兵
                                  ulong salted_key;                           // 2026-09-17 新增
                                  CaseId[] member_set; }
struct JudgmentRecordedPayload { PatientId patient_id; CaseId case_id; PlayerId author_player_id; Judgment judgment; }
struct JudgmentRevisedPayload  { PatientId patient_id; CaseId case_id; PlayerId author_player_id; Judgment judgment; }
```

> **⭑ 2026-09-19(39 首轮评审裁定)**:两条判断记录载荷**补 `author_player_id`**(作者轴,
> 分册键 + `J(c,p)` 判定取法 + 痕投影的前提)。**有界性随之变** —— 改写史事件量仍受玩家操作
> 约束(§六),重证含 **× 玩家数(≤ 4)常数倍**。`PlayerId` 的定义与发号归 **45 的 GDD 轮**
> (本 ADR 只登记契约:加入 / 离开 / 主机迁移下**稳定且不复用**);P0 单机 = 常量 `0`。

> **🔴 2026-09-17 载荷表就地订正(三处)** —— 二轮 `/design-review`(统一由
> unity-specialist 的两条 BLOCKING 提出,37 GDD 照抄即崩):
>
> **① `FixSet` → `DiseaseIdSet`(类型口径错误)**。原写 `FixSet`,而该型
> **全库零定义**(仅本 ADR 的 Key Interfaces 出现两次)。更严重的是**类型本身错了**:
> `disease_id` 在 9 的 R1 表(`disease-simulation.md:277`)是 **`DIS_*` 前缀枚举**,
> **不是定点量**。把它塞进 Q16.16 算术域,正是 **ADR-006 二轮复核 D-21-17 修掉的
> 同一类错误**(「把整数计数塞进定点域」)。破坏面:
> ① ADR-006 §五 编码器按 `Fix` 写 raw `long`(8 字节 + 2¹⁶ 标度语义),
> 病种枚举被烘成定点数;② 病种名无法经 `FixParse` 进入管线(它只吃分数/整数字面量);
> ③ `D ∈ disease_set` 变成**定点相等比较** —— 语义已错却**逐位可回放**
> (**静默失败类**);④ 与 ADR-006 的 D-21-13 口径冲突。
> **改型**:`DiseaseIdSet` = **版本化整数 ordinal 集合**,编码按 `int` / `u16` 走
> ADR-006 §五(不走 `Fix` 编码路径);ordinal ↔ 病种名的映射表住 9 的病种注册表,
> 经 ADR-014 烘焙产出。**⚠️ 连带**:ordinal 与词表**进 `ConfigVersion` 内容哈希
> 覆盖集** ⇒ 表变更**可追溯**(`ConfigVersion` 变)。
> **🔴 2026-09-17 二次订正(与 ADR-010 §七 对齐)**:本条初稿写「表变更后旧存档**拒载**」
> —— **与 ADR-010 §七 / ADR-014 §五 的「不匹配非致命」直接冲突**,已作废。
> **真正的门是 append-only**(`adr-014 §五 D-21-13 承接条 ③`:既有条目号永不重用、
> 永不改义 + 构建期基线断言 ⇒ 改号 = **构建期硬失败**)。
> `ConfigVersion` 负责「改动可追溯」,append-only 门负责「买断静默错读」。
>
> **② `opened_tick` / `anchor` 从载荷中删除**。`opened_tick` **就是该事件的
> `SimEvent.Tick`** —— 载荷中重复携带即制造「**同一事实两份真相**」,
> 违 ADR-006「少一份可失步的状态」原则。引用时一律写 `CaseOpened.Tick`。
> (`CaseClosedPayload` 的 `opened_tick` 同此理删除;`case_id` 保留 ——
> 它是**跨事件引用**,不是本事件的时间戳。)
>
> **③ `PatternRecognizedPayload` 补 `salted_key`**。本 ADR §一/§三/§五 三处
> 都宣称「世界级事件用加盐哈希键」,而载荷 struct **根本没有该字段**
> ⇒ ① 37 的 AC-37-14 成**空断言**(「不含裸 `disease_id`」在字段不存在时恒真);
> ② **53 无法定位是哪个 `D` 触发** —— 共病(`F-37.3`,一案进多组)时
> `anchor_case.disease_set` **不唯一**。补 `ulong salted_key` 后,
> 53 以它匹配自己重算的盐键。**37 的 AC-37-14 随之改为正向合取。**
>
> **⚠️ 同 tick 多 `D` 达标的定序**(新增,与 37 的 AC-37-27 对应):同一
> `CaseClosed` 可让两个 `D` 同时达标 ⇒ 同 tick 两条 `PatternRecognized`。
> **写入序 = `salted_key` 整数升序**(`Seq` 随之 0,1,2…)——
> **不得**依赖 `disease_set` 的集合迭代序(C# 不保证跨平台一致 ⇒ 重放漂移)。

> **2026-09-15 修正(B-2)**:原稿此处 `PatternRecognizedPayload` **只有两个字段**
> (`patient_id` / `member_set`),**漏了锚点** —— 与 §三 表格「载荷携带 `anchor: CaseOpened.anchor`」
> 直接矛盾。现已补为 `anchor_case: CaseId`。

## Alternatives Considered

### Alternative 1: 单流承载一切 + 折叠成一行(现状延伸)

- **Description**:病例事件也进病史流,终态时与病人一起折叠
- **Pros**:无需新流 / 新路由
- **Cons**:折叠丢流位置 ⇒ case_id 不可重构 · fires-once 破坏(3→1 重数,第 4 例重发)·
  「已处置」证据也被折叠删掉
- **Rejection Reason**:复核 #1 / #2 / #6 四方同指;正是本 ADR 要解决的死结

### Alternative 2: 独立病例流 + 折叠豁免(本裁决)

- **Description**:病例事件落第二条流,病例流不折叠;有界性 = 写入者只有玩家 + 9 配置上限
- **Pros**:case_id 可重构 · fires-once 完整 · 与病史流折叠解耦 · 不破坏改写史回看
- **Cons**:需 `IEventSink` 路由扩展;跨流全序需测试覆盖
- **Decision**:**采用**(本 ADR)

### Alternative 3: 病例流可物理折叠,折叠行保留三元组 + 盐键 + 已处置快照

- **Description**:病例流也折叠,但折叠行保留 `(Tick, Patient, Seq)` 三元组 +
  盐键 + 处置证据快照(ADR-006 Amendment B 教训推广)
- **Pros**:已结案案例从 N 行缩到 1 行,物理有界
- **Cons**:① 未结案案例永不折叠(规则六)⇒ 折叠省不下未结案集合;
  ② 改写史「为玩家回看」是变长内容,折叠行塞不下 ⇒ 破坏规则四 / AC-37-05;
  ③ 折叠行要承载 case_id + disease_set + treated + MemberSet,不比事件小;
  ④ 引入「case_id 从折叠行读」的间接层 + fires-once 需另建「已触发」机制
- **Rejection Reason**:收益与复杂度不成比例;未结案集合 + 改写史回看是硬约束(Opus 复核未考虑)

### Alternative 4: 无事件流,病例状态 = 可序列化快照

- **Description**:病例状态定期快照,结案时一次性审计事件
- **Pros**:最简单;无流路由问题
- **Cons**:与 ADR-005「事件流唯一真源」冲突;联机换 authority 时病例状态整体传输;
  改写史等记录类内容无法回放
- **Rejection Reason**:违反确定性模拟核心哲学

## Consequences

### Positive

- case_id 三元组可重构、fires-once 完整(复核 #1 落点)
- 「已处置」有事件佐证 + 折叠耦合解掉(复核 #6 / D-37-A 落点)
- 判断记录事件化满足 AC-37-05,不违反 8 铁律②
- 病例流有界性有硬论证(写入者只有玩家 + 立案率 ≤ 病人出现率)
- 保密语义收窄为「player 不可见」+ 防御纵深盐(复核 #3 落点,措辞收窄)
- 幂等拒收可重放(复核 #10)

### Negative

- 需要 `IEventSink` 路由扩展(对 ADR-005 的接口修正 —— 本 ADR 承重)
- 病例流与病史流跨流全序需测试覆盖(新复杂度)
- `treated` 快照使结案写事件时依赖病史流(跨流查询只在当下,重放不需)
- 9 侧需显式配置「病人出现率上限」作为有界性硬界(新增一个配置项)

### Risks

- **风险 1**:跨流全序键被误写为缺 `Patient` 的键 ⇒ 平局 ⇒ fires-once 竞态
  - **缓解**:`(Tick, StreamPriority, Patient, Seq)` 写成硬契约,EditMode 单测守
- **风险 2**:判断记录事件误落病史流 ⇒ 违反 8 铁律②(污染 9 的真源)
  - **缓解**:路由表 = 纯函数白名单,AC-37-05 断言;8 的边界铁律②有测试
- **风险 3**:9 侧「病人出现率上限」未配置 ⇒ 有界性论证悬空
  - **缓解**:ADR-008 落盘后,9 的 GDD 登记该配置项(D 项)
- **风险 4**:`anchor` 语义被 13 GDD 重新定义后需迁移
  - **缓解**:**已消解**(2026-09-15 B-2 修正)。原 `anchor: long` 已拆为
    `opened_tick`(立案 tick)与 `anchor_case: CaseId`(触发识别的病例),
    两者均为**已定义、有消费者**的字段;52 侧的生成点枚举改名 `spawn_anchor`。
    13 若将来需要「立案上下文」,以**追加新字段**的方式引入,不改既有字段语义

> **2026-09-15 ADR-016 §六 后续**:13 与 37 的「在场实体 / 就诊交互」契约
> (`case-system.md:473` 原标「契约暂定」)**已定型** —— 13 出**只读**视图
> `IPresentPatients`(在场病人 `PatientId` + `WorldPos` 格 + 粗状态枚举),37 只读它立案;
> **13 不引用 37**(单向无环)。**本 ADR 的既有字段(`anchor_case`)一字未动** ——
> 与上条 B-2 修正的约定一致(13 的需求以**追加新接口**引入,不改既有字段语义)。
> **与 B-2 当时拒绝预留不矛盾**:B-2 拒绝的理由是 13「至今没有消费者」,
> 而此处 **37 已立案且明写需要它**。详见 ADR-016 §六。

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| case-system.md | F-37.1 fires-once(≥3 例结案触发一次) | 病例流不折叠 + 跨流全序 ⇒ 重数正确、不重发 |
| case-system.md | F-37.2 case_id 三元组 / 全序 / 可结案 | 三元组可重构;全序键补 `Patient`;处置证据快照 |
| case-system.md | F-37.3 一案多病种进多组 | `CaseClosed.disease_set` 为集合 |
| case-system.md | 规则二立案 · 规则四改写史回看 · 规则六结案 · 规则九保密 | CaseOpened/Closed 事件;改写史不折叠;treated 快照;盐 + DTO 静态检查 |
| case-system.md | AC-37-05 记录事件化 · AC-37-20 改写不进计分 | JudgmentRecorded / JudgmentRevised 事件 |
| disease-simulation.md | 处置事件携带求值参数(9 的 F1/F4) | 处置事件留病史流,不改 9 的求值 |
| disease-simulation.md | AC-36 终态折叠(plateau / relapse 豁免) | 折叠只作用于病史流,病例流豁免 |
| diagnosis-system.md | 8 铁律②(读数不回写病史流)· AC-8-47 冻结 | 判断记录落病例流不落病史流;结案事件冻结判断 |
| random-events.md | 52↔37:52 调度,37 医疗内容 | 病例流归 37;52 注入只触发立案 |
| technical-preferences.md | ADR-001(联机选型 · 2026-09-15 Accepted) | 追加约束:选型须承载两条逻辑流(ADR-009 Accepted 后为**三条**:病史 / 病例 / 世界) |

## Performance Implications

- **CPU**:两条流 Append 均为 O(1);结案时跨流窗口查询为 O(该病例处置数)
- **Memory**:病例流 = 立案数 × 每案事件数(开/结 + 判断记录 + 模式);有界性见 §六
- **Load Time**:重放扫两流并集,每流 O(n)(ADR-009 Accepted 后为三流)
- **Network**:权威迁移时传输两流;无额外同步

## Migration Plan

- 本 ADR 不改变已 Accepted ADR 的现有事件(病史流形状不动,仅 9 的处置事件
  载荷已含 `polarity` / `Offset` / `τ_half`,见 disease-simulation.md §规则六 病史元组)
- 7a 持久化承接病史流折叠(含 AC-36 豁免)义务(仿 ADR-007 §二),病例流不折叠
- 45 网络层:两流传输与合并排序遵循 `(Tick, StreamPriority, Patient, Seq)`
  (ADR-009 Accepted 后为**三流**,`StreamPriority` 三值 —— 病史 < 病例 < 世界)
- 9 侧补登记「病人出现率上限」配置项(有界性硬界)

## Validation Criteria

- [ ] EditMode 探针:两条流的 Append 路由按 Kind 白名单分发
- [ ] 单测:跨流全序 `(Tick, StreamPriority, Patient, Seq)` 无平局
- [ ] 单测:同病种 3 例结案触发一次;第 4 例不重发
- [ ] 单测:复诊第二例**不可**凭首诊处置通过「已处置」
- [ ] 单测:终态折叠不删病例流(改写史回看完整)
- [ ] 单测:盐键无碰撞(P0 ≤ 8 病种枚举)

## Related Decisions

- ADR-005(确定性模拟 / 抽象点 / 折叠规则)
- ADR-006(Amendment A–D:SimEvent 形状 · `patient_id` 重构 · `Seq` 发放域与跨流全序键 ·
  真源 = 两流并集)—— 本 ADR §一 的「`Seq` 不分流」与 §二 的全序键**均以 Amendment C 为准**,
  §六 的折叠豁免以 Amendment D 为准
- **ADR-009 世界状态的事件化边界**(Accepted,2026-09-15)—— 追加**第三条逻辑流「世界流」**,
  本 ADR 的 `StreamId` 扩为三值、§二 全序键 `StreamPriority` 升三值(病史 < 病例 < 世界);
  其 **Amendment E** 升格 ADR-006 Amendment C / D 至三流口径。**本 ADR 的病例流定义不受影响。**
- ADR-007(IEventAuthority · WorldSeed · PatientId.None)
- **ADR-016 AI 架构**(Accepted,2026-09-15)—— §六 的 `IPresentPatients` 只读视图结清
  `case-system.md:473` 的「契约暂定」;**本 ADR 的病例流定义与既有字段不受影响**
- 37 病例系统 GDD(`design/gdd/case-system.md`)
