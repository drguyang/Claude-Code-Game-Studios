# ADR-024: 三流 `Kind` 单一登记真源 + 构建期校验体系

## Status

Accepted

> **2026-09-20 起草;同日用户裁定「全部照准,转 Accepted」**(逐份裁定轮 #2):
> **①真源 = `entities.yaml`(必填 `stream:`/`author:`/`payload_schema:`)· ②§三/§二 降级为路由注记 ·
> ③Amendment 追加通道退役(新 Kind 先建 registry 条目)· ④补齐 9 支 + 4 支家规 + 修 4 处陈旧计数 +
> `ConsequenceResolved` 幽灵引据订正 · ⑤构建期生成器 `tools/kindgen/`(A1–A5 断言,不引 Roslyn
> analyzer)· ⑥拒绝表执行体归 ADR-014 阶段 2 —— 六项全收。**
> ~~起草时注:①⑤ 须逐条照准方可转 Accepted~~(已由该裁定履行;本件**无引擎实测前置**,
> 纯数据边界裁决,转 Accepted 不欠任何 spike)。
> **TD 条件 C3 就此结案。** §Migration 四步与 §Validation V-1…V-6 自此成为可执行义务:
> registry 补条目、生成器工具位、`architecture.md` #3 小节改判,归回写轮逐件落地。
> **事实面**(三处登记集的集合运算)已两轮实测钉死,见 §Context。

## Date

2026-09-20

## Last Verified

2026-09-20(三处 Kind 登记集以 `yaml.safe_load` + 正则集合运算穷举复算;
`architecture.md` D-1 / #3 / QQ-05 与本件同步)

## Decision Makers

dr_guyang(用户 · **2026-09-20 全部照准,转 Accepted**)· technical-director(起草)·
9 疾病与伤情模拟(白名单规则的出处件 `disease-simulation.md:183-184`)·
52 随机事件导演(`EventRolled` 五支的写者;`TR-randomevents-010/031` 的 GDD 归属方)·
37 病例系统(病例流 5 支)· 7a 持久化(三流序列化的消费方)· 45 网络层(`IReplayPipe` 按流路由)

## Summary

`IEventSink.Append` 的路由是 **`Kind → StreamId` 纯函数白名单,列表外 `Kind` 构建期拒绝**
(`disease-simulation.md:183-184`,承 ADR-008 §一)。但「白名单从哪读」从未被裁决 ——
今天有**三个候选登记处,且三个都不完整**(实测):`entities.yaml` 24 支、ADR-009 §三骨架
15 支(世界流专属)、ADR-007 §三 5 支(零 registry 条目),并集 **33** 支。
本 ADR 裁决:**`entities.yaml` 的 `SimEvent.Kind.*` 是三流全集的唯一登记真源**;
ADR-009 §三 就地降级为世界流路由注记(不再是家);**新 `Kind` 的唯一追加通道 = 先建 registry 条目**;
并把「白名单如何生成」升格为构建期校验体系(生成器 + 断言清单),承载
`TR-randomevents-010` / `TR-randomevents-031` 两条 Foundation TR。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Knowledge Risk** | **LOW** —— 裁决本体是纯数据边界 + 编辑期工具(读 YAML、写 `.g.cs`),不触任何引擎 API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`(确认无 post-cutoff API 依赖)· ADR-014 §二(两阶段工具链 —— 生成器挂点复用其阶段 2) |
| **Post-Cutoff APIs Used** | **无**。生成器若用 Roslyn analyzer(`TR-randomevents-010` 字面提到),**本版刻意不引入** —— 生成器 = 编辑期 .NET 控制台工具,产物是普通 `.g.cs`;Roslyn analyzer 的引入须另行照准(见 Alternatives Alt E) |
| **Verified Against Engine Docs** | N/A(不涉引擎运行期) |
| **Deprecated API Check** | 通过 —— 不使用任何 `deprecated-apis.md` 条目 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— `SimEvent` 形状与流唯一真源)· **ADR-008**(Accepted —— 按 `Kind` 路由是纯函数)· **ADR-009**(Accepted —— 三流成立与本骨架)· **ADR-007**(Accepted —— §三 五支的出处)· **ADR-014**(Accepted —— 构建期烘焙管线,生成器复用其阶段 2 工具位) |
| **Blocks** | 任何 `IEventSink` 的实现故事(TD 条件 **C3**);9 / 52 / 37 的落流代码;ADR #2(程序集清单里 `Sim.StreamRouting.g.cs` 的归属程序集) |
| **Supersedes** | **部分取代 ADR-009 §三**(骨架从「登记处」降为「路由注记」—— 骨架文本保留,家规地位移交 registry);**取代 ADR-009 Amendment F–L 的「以 Amendment 追加 Kind」通道**(今后走 registry 直登) |
| **Related** | ADR-010 §三(三流序列化 —— 读同一 registry 判流别)· ADR-021 §③(首个以此通道追加的 Kind 的反例即本病) |

## Context

### Problem Statement

「白名单在哪」未裁决 ⇒ 每支 `Kind` 实际有 **0~3 个家**,而三个家各自漏人。
漏人的后果不是文档不齐,而是**构建期被自己的白名单拒收** —— 项目已为此付出两次
(`ActorCellEntered`、`CompoundTriggered` 均为「引用却无登记」失效模式,由后续轮追认),
且 `architecture.md` §5.5 D-1 证明**第三种形态已经成组发生**。

### Current State(2026-09-20 两轮实测,集合运算可复算)

| 登记处 | 支数 | 性质 |
|---|---|---|
| `design/registry/entities.yaml`(`name: SimEvent.Kind.*`) | **24**(世界 12 / 病史 7 / 病例 5,逐条按 `constraint:` 自述「落X流」归类) | 机器可读、有 schema、GDD 撰写流程先写这里 |
| ADR-009 §三 骨架 | **15** | **世界流专属** —— 结构上装不下三流全集 |
| ADR-007 §三 | **5**(`EventRolled` `EventArrived` `ThreatDeferred` `ThreatDeferralCleared` `HistoryFlagChanged`) | **零 registry 条目**,但有 12~20 处/支的下游引用 |
| ADR-009 §二 归属规则(bullet 具名) | **29** | 散文清单,不可机读 |
| **全集(并集)** | **33** | ≠ 任一处单读 |

**双向缺口**(2026-09-20 逐条对集合;`architecture.md` D-1 表同口径):

| 方向 | 支数 | 成员 |
|---|---|---|
| 只住 §三骨架、零 registry 条目 | **4** | `Craft`(载荷 Amendment J 已定稿 · `processing.md:306` 生产使用)· `DropSpawned` · `DropClaimed` · `DropDespawned` |
| 只住 ADR-007 §三、零 registry 条目 | **5** | 上表五支 |
| 只住 registry、§二/§三/ADR-007 家规文本均未收 | **4** | `CareApplied` · `CompoundTriggered` · `CompoundExpired`(病史流)· `ConsequenceResolved`(世界流) |
| 陈旧计数 | **4 处** | `entities.yaml:2002/2043/2077/2107` 写「§三 的 **9-Kind** 骨架」(今 15) |

**被拒集(白名单按哪个家生成的后果)**:按 registry 生成 ⇒ 拒收 **9**;按 §三骨架生成 ⇒ 拒收 **18**。
两个方向都会死。`entities.yaml:1908-1911` 自注「骨架在 ADR-009 §三 **即其登记处**」,
把「**双家**」写成了纪律 —— 这正是病根,不是某个数字错。

`ConsequenceResolved` 是病根的最深样本:其 registry 条目**自称**经 Amendment 通道入 ADR-009,
而该件最后一条 Amendment 是 **L**(2026-09-19)—— **Amendment M 不存在**;
其 `referenced_by` 指向「§三骨架(9-Kind)」,而 §三无它。**引据指向一个不含它的节,且通道上无此裁定。**

### Constraints

- ADR-008 §一:路由必须是 `Kind` 的纯函数 ⇒ 真源必须可机读、可生成为 switch 表。
- 门 A(`Sim` 程序集 `"noEngineReferences": true`,ADR-017 §二):生成物是纯 C# 枚举/switch,合法;
  **但生成器本体住编辑期**(不进构建的 `tools/`),与 ADR-022 Tooling 层同构。
- 一条 `Kind` 只能落一条流(ADR-008:「一个 Kind 无法同时落两条流」—— `EnemyInjuryOnset` 拆分先例)。
- 数值冻结铁律不适用于本件(无平衡量);但 **registry 谓词表的 17/18 条口径**须如实登记(见 §Decision ⑥)。

### Requirements

- `TR-randomevents-010` —— asmdef / Roslyn 构建期校验体系(Foundation,`adr: null`)
- `TR-randomevents-031` —— 构建期校验:17 条拒绝表(Foundation,`adr: null`)
  ⚠️ 实测 `random-events.md:1071-1079` 的拒绝表**逐条数是 18 个谓词**,「17 条」是该件自陈口径 ——
  本 ADR 登记落点与形状,**不改写 TR 文本**(TR 回写归 Accepted 后的回写轮,防借绿)。
- TD 条件 **C3**:「三流 `Kind` 单一登记真源须在第一个 `IEventSink` 实现之前裁定」。

## Decision

### ① 唯一登记真源 = `entities.yaml` 的 `SimEvent.Kind.*`

三流全集(补齐后 **33** 支)只在这里登记。每条**必填字段**扩三件:
`stream:`(`history` / `case` / `world` 之一,**枚举值,禁从散文解析**)·
`author:`(写者系统号)· `payload_schema:`(字段名表,类型只允许整数域:`i32/i64/Fix-string/枚举/格 WorldPos`)。

### ② ADR-009 §三 就地降级为「世界流路由注记」

骨架 15 支**不删**(历史与载荷归注保留),加节首声明:
「本节自 ADR-024 起**不再是登记处**;登记处 = `entities.yaml`;本节与 registry 不一致时以 registry 为准并触发 §Validation V-1 断言失败。」
§二 归属规则同样降级为注记,补收 ④ 的 4 支。

### ③ 新 `Kind` 追加通道改道

**今后唯一通道:先在 `entities.yaml` 建条目(带 `stream:`)再在任何 GDD/ADR 引用。**
ADR-009 Amendment 通道(F–L 用过的那条)**退役** —— 它正是 `ConsequenceResolved` 幽灵引据的生产线。
GDD 侧「追加 Kind」的登记义务措辞由实现轮统一回填,本 ADR 生效即挂账。

### ④ 补齐 9 支 registry 条目 + 4 支家规文本 + 4 处陈旧计数

- registry **+9**:`Craft` `DropSpawned` `DropClaimed` `DropDespawned`(世界流)+
  ADR-007 五支(病史流)—— 载荷按其出处件逐字搬入(`Craft` 用 Amendment J 定稿载荷)。
- ADR-009 §二 **+4** 具名:`CareApplied` `CompoundTriggered `CompoundExpired`(病史流 bullet)
  `ConsequenceResolved`(世界流 bullet)—— 并把「§二 的具名并集 = 29」改写为「= 全集」。
- `entities.yaml:2002/2043/2077/2107` 四处「9-Kind」计数改为「ADR-024 前历史值,现由 registry 机读」。
- `ConsequenceResolved` 条目头的「Amendment 通道」自述**就地订正**(幽灵引据清除,不删条目)。

### ⑤ 构建期校验体系(白名单生成器)

`tools/kindgen/`(编辑期 .NET 工具,与 ADR-022 的 Tooling 层同构,不进出货构建):

```
entities.yaml ──读取──▶ kindgen ──生成──▶ src/Sim/StreamRouting.g.cs
                              │             (Kind→StreamId 纯函数 switch)
                              └── 断言目录(任一失败 = 构建失败):
                                A1 每支 Kind 有且仅有一个 stream: 值(禁双家/无家)
                                A2 payload_schema 字段类型 ∈ 整数域白名单(禁 float —— ADR-006 边界)
                                A3 无重名键;A4 author 必填且 ∈ 已登记系统号
                                A5 §三骨架/§二注记 ∉ registry 的成员 → 报告(双向差集归零)
```

### ⑥ 17/18 条拒绝表的落点

`random-events.md` §Tuning Knobs 的拒绝表(自陈 17,实测 18 谓词)**由 ADR-014 阶段 2 校验器执行**,
生成器 A 系断言不重复实现它 —— 本 ADR 只登记「落点存在、出处件自陈数与实测数差 1」这一事实。

### Key Interfaces(生成物形状)

```csharp
// src/Sim/StreamRouting.g.cs —— 生成器产物,勿手改(门 A 程序集内,纯 BCL)
public static class StreamRouting {
    public static StreamId Of(SimEvent.Kind kind) => kind switch {
        SimEvent.Kind.CaseOpened          => StreamId.Case,
        SimEvent.Kind.InjuryOnset         => StreamId.History,
        SimEvent.Kind.DropSpawned         => StreamId.World,
        // …33 支,与 entities.yaml 逐支一致(§Validation V-2 断言)
        _ => throw new BuildContractException(kind), // 运行期不可达:白名单已穷举
    };
}
```

## Alternatives

### Alt B —— ADR-009 §三 扩为三流全集,registry 改为生成视图
- **Pros**:家规文本与机器面合一;ADR 读者不用跳文件。
- **Cons**:§三 的世界流专属是结构事实(`EnemyInjuryOnset` 拆分先例证明扩不动);
  且 GDD 撰写流程(/design-system 的 registry 回填)先写 YAML,反向生成会造成双写竞态。
- **Rejection Reason**:等于把病根(多处家)换了一个方向再长一遍。
- **What Would Change Our Mind**:若未来 registry 被拆成多文件导致单源不成立。

### Alt C —— 新立 `kind-registry.yaml` 第四个文件
- **Rejection Reason**:第四个登记处 = 病灶复制;registry 已有 24 支的家,迁移是纯 churn。

### Alt D —— 代码枚举为真源(`Sim` 里的 `enum Kind`),registry 从代码生成
- **Rejection Reason**:违反 ADR-014 作者态外部化纪律的对称面(内容注册不应藏在程序集里等编译);
  且 GDD 轮(设计期)先于代码轮,设计期无落点 ⇒ 「引用却无登记」以新形态复发。

### Alt E —— 用 Roslyn analyzer 做运行期项目内断言(`TR-randomevents-010` 字面含 Roslyn)
- **Rejection Reason**:本版**不引入** —— 生成器 + CI 断言已覆盖 A1–A5;analyzer 的收益(IDE 即时反馈)
  不抵其依赖面。若 TR 回写时用户坚持字面兑现,须另签(不改本裁决主体)。

## Consequences

- **Positive**:9/18 两个被拒集归零(补齐后任一家生成结果相同);`CompoundTriggered` 型失效模式
  **从根因关闭**(单源 + 通道改道),不再靠后续轮追认;`ConsequenceResolved` 幽灵引据清除;
  `TR-randomevents-010/031` 有 ADR 可依;TD 条件 C3 可结。
- **Negative**:`entities.yaml` 从「设计参考数据」升格为**构建输入** —— 它的损坏/误编辑 = 构建失败
  (这是本裁决的本意,但须向 GDD 作者流程显式广播);涟漪 6 文件(registry · adr-009 · adr-007 注 ·
  `architecture.md` · `requirements-traceability.md` · `traceability-index.md`)。
- **Neutral**:Amendment F–L 的历史文本不改写(它们是事实记录);退役只约束今后。

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| 补齐 9 支时载荷抄错(出处件 ↔ registry 手搬) | 中 | 高(静默形状错) | 逐支 diff 出处件原文;V-3 断言载荷字段名表与出处件一致 |
| YAML 作为构建输入的解析差异 | 低 | 高 | 生成器住编辑期 .NET SDK,**不进 IL2CPP 域** ⇒ 不入 ADR-012 逐位矩阵;产物 `.g.cs` 才是构建物 |
| 通道改道后 GDD 作者仍写 ADR Amendment | 中 | 中 | ADR-009 §三/§二 节首降级声明 + `docs/CLAUDE.md` 加一行路由 |

## Performance Implications

无运行期影响 —— 生成的 switch 与手写完全同形;编辑期构建 +~0.1 s(读一个 YAML)。

## Migration Plan

1. registry +9 支(④ 第一子项)→ 复算被拒集应 = 0 / 4(骨架方向仍差 4 支家规文本,预期内)
2. ADR-009 §二/§三 降级注记 + 补 4 支具名(④ 第二子项)→ 被拒集 = 0 / 0
3. 陈旧计数 4 处 + `ConsequenceResolved` 自述订正(④ 第三、四子项)
4. `tools/kindgen/` 与断言 A1–A5 落地(实现轮;本 ADR 只锁形状与落点)

**Rollback**:本 ADR 全部改动是登记面文本 + 一个不进构建的编辑期工具 —— 逐文件 git revert 即可,
无存档、无事件流、无出货构建影响(尚未有代码消费生成物)。

## Validation Criteria

- [ ] **V-1** 生成器跑通且 A1–A5 全绿(断言失败必须 `throw`,禁 `Debug.Assert` —— 承 ADR-022 C 系口径)
- [ ] **V-2** `StreamRouting.g.cs` 的 case 数 = registry `SimEvent.Kind.*` 条目数 = **33**(可复算)
- [ ] **V-3** 补齐 9 支的载荷字段名表逐支与出处件 diff 为空
- [ ] **V-4** 全库 `grep "9-Kind"` 命中 = 0(陈旧计数清零)
- [ ] **V-5** ADR-009 Amendment M 及以后**不存在**(通道退役的可证伪判据)
- [ ] **V-6** `architecture.md` D-1 / #3 / QQ-05 三处指向本件并改「已裁决(Proposed→Accepted 时)」

## GDD Requirements Addressed

| TR / 义务 | 来源 | 本 ADR 如何覆盖 |
|---|---|---|
| `TR-randomevents-010` | `random-events.md` / registry(Foundation,gap)| §Decision ⑤ 构建期校验体系(asmdef 面归 ADR #2,交叉引用)|
| `TR-randomevents-031` | `random-events.md:1071-1079`(Foundation,gap)| §Decision ⑥ 落点登记(执行体 = ADR-014 阶段 2)|
| TD 条件 C3 | `architecture.md` Document Status | 本件转 Accepted 即结 |
| `entities.yaml:1908-1911` 双家自注 | registry | ③ 通道改道使其作废 |
| `ConsequenceResolved` 幽灵引据 | registry ↔ ADR-009 | ④ 就地订正 |

## Related

`docs/architecture/architecture.md`(§5.5 D-1 / §Required ADRs #3 / QQ-05)·
`adr-009` · `adr-008` · `adr-007` · `adr-014` · `adr-022`(Tooling 层先例)·
`requirements-traceability.md`(6 条 Foundation gap 表的两行随之改判)
