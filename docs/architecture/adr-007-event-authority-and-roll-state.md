# ADR-007: 事件权威与掷骰状态 (Event Authority & Roll State)

## Status

Accepted

> **2026-09-15 用户裁定转 Accepted。** 本 ADR 由 52 随机事件导演的二轮复核(2026-09-15)提出 ——
> 它是 **52 单方面断言了两个跨域决定**,而 ADR-005 / ADR-006 均未覆盖。
> 四项裁决(`IEventAuthority` 第六抽象点 · `WorldSeed` 归 7a 存档头 ·
> 掷骰输入可从事件流重构 · `PatientId.None` 哨兵)**均照准**。
> **52 的代码实现阻塞解除**;`WorldSeed` 的存档头契约转由 7a 撰写时兑现。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 裁定 Accepted**)· technical-director(签:架构边界)
· network-programmer(签:掷骰权归属与主机迁移)· systems-designer(签:事件种类)
· 系统 52 随机事件导演 GDD 作者 · 系统 7a 持久化(待撰写 —— 承接 §二 的存档头义务)

## Summary

系统 52「随机事件导演」的确定性契约(DC-1)有两个断言**超出了 ADR-005 / ADR-006 的覆盖范围**:
① 它引入了 **`IEventAuthority`** —— 这**不是** ADR-005 定义的那五个抽象点之一,而是**第六个**;
② 它断言 `WorldSeed` 在「存档创建时生成一次」—— 而**其生成时机与持久化归属在两份 ADR 中均未定义**,
那是 7a 持久化拥有的跨域决定。本 ADR 正式定义二者,并钉死**掷骰的全部输入必须可从事件流重构**这条不变量。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(模拟 / 持久化 / 联机同步边界) |
| **Knowledge Risk** | **LOW** —— 本裁决只用到整数哈希、接口与事件流,**不依赖任何 post-cutoff API** |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `docs/architecture/adr-005-deterministic-sim.md` · `docs/architecture/adr-006-fixed-point-boundary-contract.md` |
| **Post-Cutoff APIs Used** | **None** |
| **Verification Required** | **主机迁移回放实测**:构造一个已跑过 N 个预算窗口的存档,在任意 tick 迁移权威并继续,后续抽取逐位不变。本裁决**依赖**该实测(与 ADR-005 的 IL2CPP 逐位性实测不同 —— 那条是不依赖的) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted 2026-09-13 —— 事件流唯一真源、`SimEvent` / `IEventSink` / `IIdAuthority`)· **ADR-006**(Accepted 2026-09-14 —— `SimEvent` 的 `Seq` / `Payload` 修正案、定点域边界) |
| **Enables** | 系统 52 的实现 · 系统 7a 持久化的种子契约 |
| **Blocks** | ~~52 的代码实现~~ —— ✅ **2026-09-15 转 Accepted,阻塞解除**。52 可进实现(仍须还清 AC 夹具与 IL2CPP 实测两项实现期前置) |
| **Ordering Note** | 本 ADR **不阻塞** 37 病例系统的内容撰写 —— 37 只撰内容字段,不碰权重行为(见 §GDD Requirements 的收窄口径)。 |

## Context

### Problem Statement

52 是 P0 **唯一既是随机数消费者、又是事件生产者**的系统。它的链条是
`21a(数据) → 52(掷骰) → 9(模拟)`:21a 堵死了自己的出口(禁 `Random.Range`),
9 堵死了自己的门(`patient_seed = hash(world_seed, patient_id)`),**52 是中间那段敞开的管子**。

52 的首轮修订补上了 DC-1…DC-4,方向正确。但**二轮复核发现其中两条越权**:

1. **`IEventAuthority` 被当作「ADR-005 派生」写进依赖表** —— 而 ADR-005
   §Key Interfaces 明确**罗列了五个**抽象点(`ITickProvider` / `IEventSink` / `IIdAuthority` /
   `IVitalsQuery` / `SimEvent`)。`IEventAuthority` 是**第六个**,且**从未被任何 ADR 批准**。
   52 的原文声称「DC-1…DC-4 由 ADR-005/006 直接推出,不是新裁决」—— **该声称为假**。

2. **`WorldSeed` 被 52 单方面断言「存档创建时生成一次,持久化」** ——
   但**种子的生成时机、归属、持久化位置在两份 ADR 中均无定义**,而那是
   **7a 持久化**的领域。52 是一个 GDD,**无权为一个尚未撰写的系统定义它的数据契约**。

不决定的代价:**静默分叉**。若 `WorldSeed` 由各机器各自生成,或 `IEventAuthority` 的实现
被两台机器各自持有掷骰权,则两台机器抽出**不同事件** → 生成不同病人 →
`IIdAuthority` 序列错位 → `patient_seed` 全变 → **病史事件流从第一条起永久分叉**。
不崩溃、可能仍自洽 —— 这正是 ADR-006 反复警告的那一类失败。

### Current State

- `docs/architecture/` 有 ADR-005(整数定点域 + 事件流唯一真源 + 五抽象点)
  与 ADR-006(定点域边界、`FixParse`、`Seq` / `Payload` 修正案)。
- **52 的 DC-1 已经在用 `IEventAuthority` 与 `WorldSeed`,但两者都无 ADR 依据。**
- 系统 7a 持久化**尚未撰写** —— 它的种子契约尚无归属。

### Constraints

- **技术**:须与 ADR-005 的「主机唯一执行 Step / CatchUp」同构;
  须对 ADR-001 的两个候选(Netcode for GameObjects / Photon Fusion)**都成立**。
- **兼容**:ADR-006 的 `SimEvent` 形状为 `{ Tick, Patient, Seq, Kind, Payload }` —— 本 ADR 不得改动它。
  (字段序以 ADR-006 Amendment A 为准;早前本 ADR 两处抄作 `{ Tick, Patient, Kind, Seq, Payload }`,
  2026-09-15 复查轮 N-8 已对齐 —— 值 struct 若被位置化编码器沿用,两种序会写出两种字节流。)
- **时间**:P0 工期基线 6-9 个月钉死。本 ADR 是**省时间**的(与 ADR-005 同理)。
- **资源**:indie 规模,无专职网络工程师。

### Requirements

- 同一存档在**任意机器、任意 tick 迁移**后,后续抽取**逐位相同**
- `WorldSeed` 的归属与持久化位置**唯一定义**,不得由消费方各自解释
- 52 **不自分配** `Seq` / `patient_id`(与 ADR-006 Amendment B 一致)
- 掷骰的**每一个输入**都必须能**从事件流重构**(无隐藏可变状态)
- 世界级事件**不得**污染 `IIdAuthority` 的高水位

## Decision

### 一、`IEventAuthority` 是第六个抽象点(P0 预留)

**裁决:`IEventAuthority` 正式成为 ADR-005 五抽象点之外的第六个 P0 抽象点。**

```
public interface IEventAuthority   // P0 = 本地占位(永远是主机)
{
    bool IsAuthority { get; }                    // P0 恒 true
    EventRollResult Roll(in RollRequest r);      // 只有权威侧可调用
}
```

它**不合并进 `IEventSink`** —— 两者的权属语义不同:
`IEventSink` 是**写入通道**(任何有事件的一方都写,虽然 P0 只有主机),
`IEventAuthority` 是**掷骰权**(天然唯一,只有主机有)。

> **为什么不能「白嫖 ADR-005」**:ADR-005 的「主机唯一执行 Step / CatchUp」讲的是
> **模拟推进**的权属,它对「谁有权掷一个世界级事件的骰」**没有表态**。
> 而 52 的掷骰**必须先于**任何 `SimEvent` 存在 —— 它是**事件流的上游**。
> 把这条留白,P1b 会有两种都「符合 ADR-005」但互相分叉的实现。

### 二、`WorldSeed` 归 7a 持久化,52 只消费

**裁决:`WorldSeed` 由 7a 持久化拥有。**

| 项 | 归属 | 说明 |
| --- | --- | --- |
| **生成** | **7a 持久化** | 世界创建时生成一次,平台密码学随机源(仅此一次**允许**非确定性) |
| **持久化位置** | **7a 存档头** | 与存档同生命周期;不是「一条 `SimEvent`」(见下) |
| **读权** | 52 / 9(只读) | 双方**只读**,不得改写 |
| **跨版本** | 7a | 存档迁移时**必须原样保留**;改了 = 全案病程与事件流同时作废 |

> **⚠️ 为什么不是「创建时写一条 `SimEvent`」**:
> 那需要 `WorldSeed` 在**第一条事件之前**就存在,而事件流本身又要靠它派生 —— **鸡生蛋**。
> 故它必须在**流之外**(存档头)。这与 ADR-005 的「事件流是唯一真源」**不冲突**:
> `WorldSeed` 不是**模拟状态**,而是模拟状态的**参数**(等同系外生常数)。
> 类比:`patient_seed = hash(world_seed, patient_id)` 里的 `world_seed` 同样是外生参数。

### 三、掷骰输入必须可从事件流重构(核心不变量)

**裁决:52 掷骰所依赖的每一份状态,要么是外生常数(如 `WorldSeed`),
要么必须能从事件流重构。不得存在任何只活在可变运行时对象里的掷骰输入。**

| 输入 | 类别 | 持久化 |
| --- | --- | --- |
| `WorldSeed` | **外生常数** | 7a 存档头(§二) |
| `win` / `tier` / `ordinal` | **抽取坐标** | 由「抽取记录」`SimEvent` 记录 |
| 已用预算 / 已消费 `ordinal` | **派生** | 由抽取记录重构 |
| `DeferredThreatSlot` | **状态** | 进流(`ThreatDeferred` / `ThreatDeferralCleared`) |
| 历史标记集(52 的 `cause_flag`) | **状态** | 进流(`HistoryFlagChanged`) |
| 配置表(档占比 / 各 `Mult` / 各 Cap) | **外生常数** | `assets/data/*.json`(版本号进存档头) |
| `MIN_tier` 进度缩放 | **派生** | 由流中进度重构 |

新事件种类(均为 `SimEvent.Kind` 的成员,**不改 ADR-006 的形状**):

```
EventRolled             Payload = { win, tier, ordinal, chosen_key }
EventArrived            Payload = { event_key, tier, spawn_anchor, cause_clue_key? }
ThreatDeferred          Payload = { slot_index, event_key }
ThreatDeferralCleared   Payload = { slot_index, reason }   // reason = 补发 | 作废
HistoryFlagChanged      Payload = { flag_id, new_value }
```

> **这是本 ADR 的承重条款。** 若任何一项留在可变对象里,
> 主机迁移后 `HistoryMult` / 已用预算会**静默改变** ⇒ 抽出不同事件,不崩溃,回放对不上。
> 52 的 AC-52-46 直接守这条。

### 四、世界级事件用 `PatientId.None` 哨兵,不污染高水位

**裁决:世界级事件的 `SimEvent.Patient` 取显式哨兵 `PatientId.None`,且该值不参与
ADR-006 Amendment B 的 `next = max(patient_id) + 1` 重构。**

```
PatientId.None ≡ -1     // 显式负数哨兵,与任何合法 id(≥ 0)可区分
```

> **为什么不能用 `default(PatientId)`**:`PatientId` 的默认值很可能是 `0`,
> 而 **`0` 是合法病人 id**。伪造的 `0` 会进 `max(patient_id)` 的重构,
> 更糟的是「所有病人都折叠后」的流里只余伪造 `0` ⇒ 新主机高水位回退 ⇒ **id 复用**
> ⇒ 两个病人共用 `patient_seed` ⇒ 病程互写。**ADR-006 Amendment B 的静默漏洞从后门回归。**

### Architecture

```
         ┌──────────────────────────────────────────────┐
         │  7a 持久化                                    │
         │  存档头: WorldSeed · 配置版本号    ← §二 生成  │
         └───────────────────┬──────────────────────────┘
                             │ (只读)
                    ┌────────▼────────┐
                    │  IEventAuthority│  ← §一 第六抽象点
                    │  IsAuthority    │     P0 恒 true(本地)
                    │  Roll(req)      │
                    └────────┬────────┘
                             │ EventRollResult {win,tier,ordinal,chosen_key}
                             ▼
              ┌──────────────────────────────┐
              │  52 随机事件导演               │
              │  掷骰输入 ← 全部从流重构 (§三)  │
              │  不持 patient_id / Seq (§一·四) │
              └──────────────┬───────────────┘
                             │ IEventSink.Append
                             ▼
              ┌──────────────────────────────┐
              │  病史事件流 (唯一真源, ADR-005)│
              │  EventRolled / EventArrived   │
              │  ThreatDeferred / …           │
              │  Patient = PatientId.None (§四)│
              └──────────────┬───────────────┘
                             ▼
                       9 疾病与伤情模拟
```

### Key Interfaces

```csharp
// ── 第六抽象点(§一)──
public interface IEventAuthority
{
    bool IsAuthority { get; }                       // P0 恒 true
    EventRollResult Roll(in RollRequest r);         // 仅权威侧
}

public readonly struct RollRequest
{
    public readonly long Win;      // 窗口起点 tick
    public readonly int  Tier;     // 档 id
    public readonly int  Ordinal;  // 本窗口本档第 n 次
}

public readonly struct EventRollResult
{
    public readonly long Win;
    public readonly int  Tier;
    public readonly int  Ordinal;
    public readonly int  ChosenKey;                // 池条目 key
}

// ── 病人 id(值类型)与哨兵(§四)—— 哨兵不参与 max(patient_id) 重构 ──
// 注意:PatientId 必须能作字段 / 参数 / 返回类型(ADR-005 的
// IIdAuthority.Next() / IVitalsQuery.GetVitals(p) / ADR-006 的 SimEvent.Patient /
// ADR-008 的各载荷 patient_id 字段),故不能是 static class。
// 2026-09-15 架构复核就地修正:原稿写作 public static class,与上述四处用法冲突(编译错误)。
public readonly struct PatientId : IEquatable<PatientId>
{
    public readonly int Value;

    public PatientId(int value) { Value = value; }

    /// <summary>显式负数哨兵,与任何合法 id(≥ 0)可区分。</summary>
    public static readonly PatientId None = new PatientId(-1);

    public bool IsNone => Value < 0;

    public bool Equals(PatientId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is PatientId p && Equals(p);
    public override int GetHashCode() => Value;      // 仅用于字典键,不落盘(见下)
    public static bool operator ==(PatientId a, PatientId b) => a.Value == b.Value;
    public static bool operator !=(PatientId a, PatientId b) => a.Value != b.Value;
}
```

> **不得用 `GetHashCode()` 落盘**:它只作内存字典键。持久化与全序一律走 `Value`(报告 E-6)。

### Implementation Guidelines

1. **先写 `IEventAuthority` 的本地占位(`IsAuthority => true`),再写抽取逻辑。**
   P0 的占位实现即「编辑器/主机直接 `Roll`」,P1b 只换实现,不换调用点。
2. **`Roll` 必须是纯函数**:给定 `(WorldSeed, 配置, RollRequest, 候选集)`
   → 同一 `EventRollResult`,**不读任何运行时对象**。这是 §三 的实现判据。
3. **`Win` 由调用方从 `ITickProvider` 取,不在 `Roll` 内部取** ——
   否则 `Roll` 不再是纯函数(它会读全局 tick)。
4. **哨兵 `PatientId.None` 必须在所有 `max(patient_id)` 遍历中被显式排除** ——
   写成 `if (p == PatientId.None) continue;`,不是依赖「负数天然更小」的巧合。
5. **配置版本号进存档头** —— 若 `档占比` / 各 `Mult` 被改,旧存档的**已发生**事件不受影响,
   但**后续窗口**会用新值。这是有意的(允许平衡调整),但版本号必须留痕以便排查回放不符。

## Alternatives Considered

### Alternative 1: 把 `IEventAuthority` 合并进 `IEventSink`

- **Description**:不新增抽象点,让 `IEventSink` 同时承担「写入」与「掷骰权」
- **Pros**:接口更少;ADR-005 的五个抽象点不动
- **Cons**:**权属语义混淆** —— `IEventSink` 的语义是「任何人可写」(P1b 客户端可能上报叙事事件),
  而掷骰权**天然唯一**。合并后,任何拿到 `IEventSink` 的代码在类型上就有掷骰权,
  P1b 会有「客户端以为自己能掷骰」的实现
- **Estimated Effort**: 更小(少一个接口)
- **Rejection Reason**:**类型系统应当编码权属**。ADR-005 引入五个抽象点正是因为
  「P1b 重构是三个月级灾难」;第六个同类边界的成本(约 10 行接口)远低于事后拆分的成本

### Alternative 2: `WorldSeed` 作为「创建时的第一条 `SimEvent`」

- **Description**:不放进存档头,而是世界创建时 `Append` 一条 `WorldSeedCreated` 事件
- **Pros**:严格符合「事件流是唯一真源」;无需 7a 特殊处理存档头
- **Cons**:**鸡生蛋** —— 事件流的位置/序号本身在 P1b 需要种子来校验,
  而 `WorldSeed` 又要在第一条事件里。且第 0 条事件的 `Seq` 由谁分配?
- **Estimated Effort**: 相近
- **Rejection Reason**:`WorldSeed` 是**外生参数**,不是模拟状态。
  把它硬塞进流会制造一个循环依赖,而循环依赖正是静默分叉的温床

### Alternative 3: `default(PatientId)` 作为世界级事件的病人字段

- **Description**:不给哨兵,世界级事件用 `default(PatientId)`
- **Pros**:零成本;不引入新常量
- **Cons**:`default` 可能是 `0`,而 `0` 是合法 id(见 §四)
- **Estimated Effort**: 零
- **Rejection Reason**:**会静默击穿 ADR-006 Amendment B**。
  这属于「不报错、回放对不上」的失败类别,正是本项目反复拒绝的那一类

## Consequences

### Positive

- 52 的掷骰契约有了 ADR 依据,不再是 GDD 越权断言
- P1b 的联机接入变成「换 `IEventAuthority` 实现」,不是「重构掷骰路径」
- 主机迁移可回放(§三)在实现前就是一条可测的验收标准
- 世界级事件与病人系统的边界清晰(`PatientId.None`)

### Negative

- 多一个接口与一次间接调用(`Roll` 经接口)—— 但 P0 是本地占位,开销可忽略
- 抽取路径多写一条 `EventRolled` 事件 —— 事件流条目数增加(见 Risks)
- 7a 持久化被**提前**绑定了一项契约(`WorldSeed`),而 7a 尚未撰写

### Neutral

- 「掷骰输入进事件流」把 52 的调试信息变成**永久的存档内容** ——
  这既是负担(流更大)也是红利(可精确重放任意窗口的抽取)

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| `EventRolled` 事件流膨胀 | 中 | 低 | 每条约 20 字节;一个窗口至多 `WINDOW_SIZE` 条(默认个位数)。可随终态折叠一并处理 |
| 7a 尚不存在,契约悬空 | **高** | 中 | 本 ADR 明写 7a 的**义务**;7a 撰写时必须兑现(登记为门控项) |
| 实现者把 `Roll` 写成非纯函数 | 中 | 高 | §Implementation Guidelines 2/3 + 52 的 AC-52-46 |
| 哨兵被 `max(patient_id)` 重构误纳 | 低 | 高 | 显式排除语句(Guidelines 4)+ ADR-006 Amendment B 的既有 Validation Criteria |
| `WorldSeed` 跨版本被改 | 低 | **高** | 存档迁移须原样保留;改了 = 全案病程 + 事件流同时作废 |

## Performance Implications

> **状态:临时值** —— 项目性能预算本身待定(`technical-preferences.md`)。

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 可忽略(每子区间一次 `Roll`) | 16.6 ms 平面 / 11.1 ms VR |
| Memory | — | 可忽略(无 PRNG 状态) | 待定 |
| Load Time | — | 无影响 | 待定 |
| Network(若适用) | — | 抽取结果 ≈ 20 字节/条(P1b) | 待定 |

## Migration Plan

**本项目尚无 52 的实现,故无迁移 —— 本 ADR 是「第一次就做对」。**

1. 定义 `IEventAuthority` / `RollRequest` / `EventRollResult` / `PatientId.None`,
   P0 实现为本地占位(`IsAuthority => true`)
2. 写 SplitMix64 的 `CDF 游走`抽取(F1 步骤②),并以纯函数形式实现 `Roll`
3. 定义五个新 `SimEvent.Kind`(§三),接入 `IEventSink.Append`
4. **7a 撰写时**兑现 `WorldSeed` 的存档头契约 —— *验证:存档往返后 `WorldSeed` 逐位不变*
5. 写 AC-52-46 的迁移回放测试

**Rollback plan**:若最终不做联机,`IEventAuthority` 可退化为直接调用
(删接口、留纯函数 `Roll`);`WorldSeed` 的存档头归属**不可回退**
(它已进存档格式,改了会让旧存档作废)。若做联机,**本 ADR 不可回退**。

## Validation Criteria

- [ ] `IEventAuthority` 在 P0 的实现为纯函数:同一 `(WorldSeed, 配置, RollRequest, 候选集)` 返回同一结果
- [ ] 构造一个已跑过 N 个预算窗口的存档,**在任意 tick 迁移权威并继续**,后续窗口抽取**逐位不变**
- [ ] 抽取路径**不读任何运行时对象**(`Roll` 的入参即其全部输入 —— 静态可验证)
- [ ] 世界级事件的 `SimEvent.Patient == PatientId.None`,且 `PatientId.None` **不出现在**
      任何 `max(patient_id)` 重构结果中
- [ ] 存档往返(`save → load`)后 `WorldSeed` **逐位不变**
- [ ] 52 **零** `IIdAuthority.Next()` 调用(病人创建唯一归 9)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/random-events.md` | 52 随机事件导演 | DC-1:唯一 RNG = SplitMix64,禁 `UnityEngine.Random`;P0 预留 `IEventAuthority` | §一 正式定义该抽象点;§三 钉死掷骰输入的持久化 |
| `design/gdd/random-events.md` | 52 随机事件导演 | DC-4:事件降临是一条 `SimEvent`,只经主机 `IEventSink.Append`;52 零 `patient_id` 分配 | §四 哨兵 `PatientId.None`;Validation Criteria 末条 |
| `design/gdd/random-events.md` | 52 随机事件导演 | 规则六之三:`DeferredThreatSlot` 须进事件流(否则主机迁移静默丢失 / 重复补发) | §三 的 `ThreatDeferred` / `ThreatDeferralCleared` |
| `design/gdd/random-events.md` | 52 随机事件导演 | AC-52-46:任意 tick 迁移主机后抽取逐位不变 | 本 ADR 的承重条款(§三) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | `patient_seed = hash(world_seed, patient_id)`;`WorldSeed` 须跨权威稳定 | §二 归属 7a;§四 防止高水位被伪造 `0` 污染 |
| `design/gdd/systems-index.md` | — | §9 C6:9 的 tick 模型与 7 的持久化从第一天起 authority-agnostic | §一 的 `IEventAuthority` 与 §三 的流可重构性 |

> **⚠️ 收窄口径(承二轮复核)**:本 ADR 解锁 52 的 **schema 与边界**,
> **不解锁 52 的 F1 公式**。37 病例系统撰写内容时可引用本 ADR 的事件种类与哨兵契约,
> **不得**引用 F1 的权重行为(`ReputationMult` / `HistoryMult` / 配额换算在二轮刚被第三次重写)。

## Related

- **ADR-005 确定性模拟与状态同步模型**(Accepted 2026-09-13)—— 本 ADR **补齐**其未定义的第六抽象点
  与种子归属。它定义的**五个**抽象点(ADR-005 §Key Interfaces)不受影响;本 ADR **新增**第六个。
- **ADR-006 定点域边界数据契约**(Accepted 2026-09-14)—— 本 ADR **不改动**其 `SimEvent`
  形状(`{ Tick, Patient, Seq, Kind, Payload }`,Amendment A),只在既有 `Kind` 枚举上**追加成员**;
  其 Amendment B(`patient_id` 迁移稳定性)是本 ADR §四 的直接动因;
  其 Amendment C(跨流全序键)**限定了本 ADR §三 新增事件的排序语义** ——
  世界级事件的 `Patient = PatientId.None`,故其 `Seq` 与任何病人的 `Seq` **不同域**,
  两流合并时的相对次序由 `StreamPriority` 与 `Patient` 承担,不依赖 `Seq` 的跨流可比性
  (**2026-09-15 ADR-009 追加**:`StreamPriority` 升为三值 —— 病史 < 病例 < 世界;
  本 ADR 的世界级事件若为世界状态类,路由归世界流,见 ADR-009 §二)。
- `design/gdd/random-events.md` —— 本 ADR 是它 DC-1 / DC-4 的正式依据
- `design/registry/entities.yaml` —— 待登记 `WorldSeed` 与 `PatientId.None` 的边界约束
- **待撰写的 7a 持久化** —— 必须兑现 §二(存档头)与 §三(配置版本号)
