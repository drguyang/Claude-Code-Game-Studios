# ADR-005: 确定性模拟与状态同步模型

## Status

Accepted

## Date

2026-09-13

## Last Verified

2026-09-13

## Decision Makers

dr_guyang(用户)· technical-director(裁决)· systems-designer(公式复核)
· 系统 9 疾病与伤情模拟 GDD 作者

## Summary

系统 9「疾病与伤情模拟」是全案数据核心(7 个系统读它),它同时要求**确定性模拟**
(定 tick + 固定种子 + 状态可序列化)与 **P1b 的 1-4 人联机**(`systems-index.md` §9 C6)。
本 ADR 裁决:**全部模拟数学在整数定点域(int64 / Q16.16)**,**事件流是唯一真源**,
**主机唯一执行 Step / CatchUp**,并给出 **P0 必须预留的六个抽象点**。
不现在做,P1b 重构是三个月级灾难。

> **2026-09-15 复查轮就地修正(N-3 / N-4)**:
> ① 原稿写「**病史**事件流是唯一真源」—— ADR-008 引入病例流后该句在权威层已不准确,
> 现行口径见 **ADR-006 Amendment D**:真源 = **病史流 ∪ 病例流**,终态折叠**只作用于病史流**;
> ② 原稿写「**五个**抽象点」—— ADR-007 §一 之后为 **六个** =
> **五个接口**(`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery` / `IEventAuthority`)
> **+ `SimEvent` 值类型**;
> ③ **2026-09-15 ADR-009 追加**:真源口径再升格 —— 事件流 = **病史流 ∪ 病例流 ∪ 世界流**
> (三流并集,ADR-009 Amendment E),`StreamPriority` 三值(病史 < 病例 < 世界),
> 终态折叠仍只作用于病史流。本条以 ADR-009 §Decision 为准。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(模拟 / 持久化 / 联机同步边界) |
| **Knowledge Risk** | **HIGH — post-cutoff,必须验证** |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`(训练数据止于 ~2022 LTS)· `.claude/docs/technical-preferences.md` |
| **Post-Cutoff APIs Used** | **None** —— 本裁决刻意只用 `long` / 整数运算,不依赖任何 post-cutoff API |
| **Verification Required** | **IL2CPP 逐位性实测**:各目标平台(x86-64 / ARM64 / Apple Silicon)上 `Math.Exp` / `Math.Pow` / `float` 除法的逐位一致性,以及 `-ffp-contract` 在 IL2CPP 转译链上是否可控。**本裁决不依赖该实测结果**(见 Alternatives —— 即使实测乐观,libm 差异已足以否决浮点路线),但它决定「将来能否回退到浮点」 |

> **Note**: Knowledge Risk 为 HIGH,项目升级引擎版本时须重新验证本 ADR。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | **ADR-001 联机选型**(追加约束,非阻塞) |
| **Blocks** | 系统 9 的实现、系统 7a 持久化的实现、系统 25 的伤害施加接口 |
| **Ordering Note** | **本 ADR 必须在写 9 / 7a / 25 的代码之前 Accepted** —— 它定义的是这三者的数据形状,事后改 = 重写每个病种 |

## Context

### Problem Statement

系统 9 被要求**同时**是:病程模拟 · 病人 AI 基底 · 通用伤情注册表 · 公卫数据源,
**外加存档与未来联机的确定性**。`systems-index.md` §8 把它的爆点标在
**「第 3-4 个月做存档时」**。

不做决定的代价:**确定性是一条单行道。** 定 tick / 固定种子 / 状态可序列化
三件事现在做近乎免费;**第 4 个月再改 = 重写每个病种的曲线与每一个调用点**。

### Current State

`docs/architecture/` 此前只有 `tr-registry.yaml`,**零 ADR**。
`technical-preferences.md` 的 Architecture Decisions Log 记录了 ADR-001~004 待建,
其中 **ADR-004「是否需要 DOTS」已被用户延后**;本项目的对应决定是
**「不上 DOTS,只做零成本预防」**(状态写成纯数据,将来换写法只换外壳)。

### Constraints

- **技术**:Unity 6.3 LTS / C# / **IL2CPP**。训练数据止于约 Unity 2022 LTS,
  6.x 全属 post-cutoff —— 任何依赖 post-cutoff 行为的断言都必须标注「需实测」。
- **时间**:P0 的 `6-9 个月` 按 26 项系统定,现已 **30 项**(见 `systems-index.md` §4 警示)。
  本 ADR 是**省时间**的,不是加时间的:它把「一次性做对」换成「不做三个月返工」。
- **资源**:indie 规模,无专职网络工程师。**定点库约 200 行、零第三方依赖** —— 这条重要,
  因为它不能变成一个需要维护的外部组件。
- **兼容**:ADR-001(Netcode for GameObjects **vs** Photon Fusion)**尚未裁决**,
  本 ADR 必须**对两者都成立**。

### Requirements

- 模拟必须**逐位可复现**:同一病史事件流 → 任意机器 → 同一读数
- 状态必须**可序列化且 authority-agnostic**(C6)
- 离线补算复杂度 **O(类 A 边界 + 每子区间 log)**,不随离线时长线性增长
- 模拟层**零 `UnityEngine` 依赖**(规则七:状态是纯数据)
  > **2026-09-15 修正(E-1)**:本 ADR 两处(此处与 §Implementation Guidelines)原写
  > 「**编译期可验证**」—— 该判据**不完备**,两个漏洞:
  > ① asmdef 不设 `"noEngineReferences": true` 时,Unity 会**隐式注入** `UnityEngine.CoreModule`
  > 引用,「零引用」**静默假通过**;
  > ② 更隐蔽 —— **`System.Math.Exp/Pow` 在 `System.Runtime`,不在 `UnityEngine`**,
  > 程序集隔离**天然拦不到**;`Unity.Mathematics.math.exp/pow` 同样不在 `UnityEngine`，
  > 且带 SIMD 近似、跨后端不逐位。
  >
  > **可执行判据(两道互补门)**:
  > **门 A** —— sim 必须落**独立 asmdef**,且该 asmdef 设 `"noEngineReferences": true`;
  > **门 B(真正的守门)** —— 一条 EditMode 测试用 `System.Reflection` 加载 sim 程序集,断言
  > ① `GetReferencedAssemblies()` 无 `UnityEngine.*` / `Unity.Mathematics` / `Unity.Collections`;
  > ② 扫全部方法体 IL,无对 `Math.Exp/Pow/Sqrt/Log/Abs`、`MathF` 的 `call/callvirt`;
  > ③ sim 类型图上无 `float` / `double` 字段与局部签名。
  > 更早的拦截(Roslyn analyzer)在 Unity 6.3 的注册形态属 post-cutoff,须 spike ——
  > **先用零依赖的门 B 垫底**。
- 中途加入的玩家能重建一个已病了 N 天的病人
- **不阻塞 ADR-001 的选型**

## Decision

### 一、数值域:整数定点

**裁决:全整数域。** 具体形态 = **int64 承载 Q16.16 标度的 `Fix` 纯值 struct**,
中间乘法落 Q32.32 再移位回。哈希用 **SplitMix64**,插值用整数除法。

> **⭑ 2026-09-23 回写轮(D-1 执行面 · 承 U1 spike 批裁定)**:`SplitMix64` 的**多输入折叠形**
> 就地钉死 = **乙案链式 avalanche** —— `state = 首输入`;每后续权重 `w`:`state = Avalanche(state + w)`;
> 终态再 `Avalanche` 一次;全程 **mod 2⁶⁴ 回绕**(`unchecked`)。**首两输入可交换**
> (`Avalanche(a+b)` 对称)仅是**黄金测试探针性质**,**消费方不得依赖**;第三输入起有序。
> 落盘形态 = `Sim.Contracts/SplitMix64.cs`(`Gamma` / `Avalanche` / `NextValue` / `Fold` /
> `Hash(a,b|c|d)` / `HashTagged` / `FoldTag` 长度前缀大端块)。
> ⚠️ **一经落码即锁**:改折叠 = **全平台重签**(ADR-012 golden-vN)+ **9 / 52 / 7a 哈希语义作废**。
> 本条是**消费面**(9 病种种子 / 52 掷骰 / 7a 折叠键)的唯一口径出处;下游只读不改。

- **不选 int32 千分位**:乘法溢出风险,且 1/1000 精度对保守带太粗
- **不选裸 Q16.16 int32**:同样溢出

**舍入纪律**(必须写进代码规范,否则二分边界会漂移):

| 场景 | 舍入方向 |
| ---- | ---- |
| 保守带内的边界搜索比较 | **向下保守**(宁多扫一步) |
| 表现层输出 | 就近舍入 |

**误差预算**:每次运算 ≤ 1 ulp(2⁻¹⁶);保守带按 `ops × 2⁻¹⁶` 外扩,
可并入 σ 的安全裕量。

> **⚠️ 2026-09-15 修正(架构复核 C-3)**:原稿此处另写「(**实测 ≤ 3×2⁻¹⁶**)」——
> 该数字**无出处**,是自引,已撤销(`disease-simulation.md` §F0 纪律表亦已撤销同一引用)。
> 现行口径:**保守带宽度待 `Fix` / 定点 `Exp` 黄金文件对拍后标定**,
> 取 `max(k × max_err, ops × 2⁻¹⁶)`,`k` 与实测上界落 `design/registry/entities.yaml`。
> **不得在标定前写死任何「实测」数字** —— 若保守带窄于真实误差,边界扫描会错过真实穿界点
> ⇒ 状态转移漏判 ⇒ 逐位仍一致但**语义已错**(本项目反复警惕的静默失败类)。
>
> **另补一条比总误差上界更关键的判据**:对 `boundary_mode: scan` 的病种,
> 须做**单向性验证** —— 断言扫描返回的边界**含**真实穿界点(宁多扫一步,不得漏扫)。
> 这条进 AC 与 CI 门。

### 二、投影点:单一门面

**sim 层放独立程序集**,`Fix` **不定义到 float 的隐式转换**,
**唯一出口**是门面的显式 `ToFloat()`:

```
GetVitals(patient) → VitalsDto(float)
```

8 诊断 / 13 病人 AI / 24 医馆即机器**只能拿到 DTO,拿不到 `Fix`**。
「忍不住当浮点用」因此需要**显式写代码**,会在 code review 暴露。

### 三、权威模型

- **病史事件流 = 唯一真源**(规则六)
  > **2026-09-15 修正(C-5)**:ADR-008 引入**第二条逻辑流(病例流)**后,本句在权威层已不准确。
  > 现行口径见 **ADR-006 Amendment D**:**真源 = 病史流 ∪ 病例流**;
  > **终态折叠规则只作用于病史流**,病例流**永不物理折叠**。
  > **2026-09-15 再修正(ADR-009 Amendment E)**:ADR-009 引入**第三条逻辑流(世界流)**后,
  > 真源口径为 **病史流 ∪ 病例流 ∪ 世界流**;终态折叠仍只作用于病史流,
  > 病例流与世界流**永不物理折叠**。
- **主机唯一执行 `Step` / `CatchUp`**
- 客户端持**流副本 + 定期快照**;UI 展示可**本地求值 `Progress`**(纯函数,定点保证逐位一致)
  —— **这不算「跑模拟」**,因为绝不写回事件流
- **`patient_id`**:静态病人由**世界种子派生**;动态生成的病人由 **主机从单调计数器分配**,
  **首行流事件即 `(patient_id, patient_seed, 病因)`**,id 随流持久化
  > **⚠️ 修正(2026-09-14 · ADR-006 Amendment B / D-9-E)**:上句**缺了迁移不变量**。
  > 由于 `patient_seed = hash(world_seed, patient_id)`(`disease-simulation.md` §Dependencies「换 authority」段),
  > **id 一变则该病人全部病程被改写** —— 故 `patient_id` 必须在权威迁移前后**逐位稳定**。
  > 三条硬不变量:
  > 1. **计数器永不复位为 0**;
  > 2. **任何权威变更后,`next = max(全部已知 patient_id) + 1`** —— 由事件流**重构**,
  >    不作独立快照(少一份可失步的状态);
  > 3. **终态折叠行必须保留 `patient_id`**(见下方 Implementation Guidelines 第 5 条)——
  >    否则高水位**不可重构**(`patient_seed` 是单向哈希,推不回 id)。
  > **决策依据与备选(B 纯哈希派生)见 ADR-006 Appendix B。**
- **中途加入** = 主机发送完整事件流(可选 N tick 一 checkpoint + 尾部),
  客户端据此重建 —— **这正是规则六的设计红利,无需新机制**

### Architecture

```
                    ┌──────────────────────────────────────┐
                    │  主机 (authority)                     │
                    │                                      │
   ITickProvider ──▶│  Step / CatchUp   ← 唯一执行者        │
                    │        │                             │
                    │        ▼                             │
                    │ 事件流 = 病史 ∪ 病例 ∪ 世界(唯一真源)  │
                    │        │                             │
   IEventSink  ◀────┤        ├──▶ IIdAuthority              │
                    └────────┼─────────────────────────────┘
                             │
              ┌──────────────┴──────────────┐
              │  网络层 (P1b · ADR-001)      │
              │  自定义可靠有序消息           │
              └──────────────┬──────────────┘
                             │
                    ┌────────┴────────┐
                    │  客户端          │
                    │  流副本 + 快照    │
                    │  本地求值 Progress│  ← 只读展示,绝不写回
                    │        │          │
                    │        ▼          │
                    │   GetVitals()     │ ← 唯一浮点出口
                    │   VitalsDto       │
                    └──────────────────┘
                             │
              8 诊断 · 13 病人 AI · 24 医馆即机器
```

### Key Interfaces

```csharp
// ── 定点标量:无行为纯 struct ──
// ⚠️ 2026-09-15 修正(C-7):原注释写「序列化只存内部 long」——**该断言为假**,
// 已由 ADR-006 §五 判定并撤销。Unity 内置序列化器不会自动序列化这里的 _raw,
// 且 private readonly 字段即便标 [SerializeField] 也受反序列化赋值限制。
// 失败是**静默的**(字段归零,不抛异常)。序列化必须走自定义编码器。
public readonly struct Fix            // Q16.16, 内部 long
{
    private readonly long _raw;
    // 注意:不定义 implicit operator float —— 这是刻意的
    public float ToFloat();           // 仅门面程序集可调用
    // ⚠️ 2026-09-20 ADR-025 回写加注(原文不删):「门面程序集」称谓自 ADR-025 起作废,
    //   现名 = Sim.Contracts(`Fix` 住此,public struct);消费约束的执法形态 =
    //   ADR-025 §② 甲案 —— 构建期断言「ToFloat() 调用点 ∈ {Sim.Codec, Gameplay.*},
    //   Sim 内调用 = 构建失败」(白名单断言,非编译器可见性;`Sim.Codec` 住 codec 程序集)。
}

// ── P0 必须预留的抽象点(实现可为占位)──
// 五个 + 第六个 IEventAuthority(ADR-007 §一)
public interface ITickProvider  { long CurrentTick { get; } }        // 全案 tick 唯一来源
public interface IEventSink     { void Append(in SimEvent e); }       // P0 = 本地 list;ADR-008 扩展为按 Kind 路由
public interface IIdAuthority   { PatientId Next();                  // 防运行时 instance id
                                   ItemInstanceId Next(); }          // 物品 / 掉落实例 id(机制 A,同下)
                                                                      // ✅ 2026-09-23 QQ-02 结案回填(逐字承 ADR-010 §五 :313-314 —— 原「待办…尚无 ADR」已过期):
                                                                      //    ItemInstanceId.Next() = 机制 A(计数器永不复位 0,迁移后 next = max+1 由三流并集重构,
                                                                      //    Drop* 事件须保留 instance_id),与 PatientId 同模式(ADR-006 Amendment B)。
                                                                      //    TR-itemdb-019 同批 gap → covered(adr: ADR-010)。本 TODO 就此消解。
public interface IVitalsQuery   { VitalsDto GetVitals(PatientId p); } // 唯一浮点出口
public interface IEventAuthority { bool IsAuthority { get; }          // 第六抽象点,ADR-007 §一
                                   EventRollResult Roll(in RollRequest r); }

// ── 事件必须带逻辑 tick 且可全序 ──
// ⚠️ 2026-09-15 修正(C-6):原块只有 { Tick, Patient, Kind },与上一行注释
// 「可全序」自相矛盾(无 Seq 无从定序)。现行形状以 ADR-006 Amendment A 为准:
// ⚠️ 2026-09-15 复查轮修正(N-1):下方 Seq 的类型原先被本 ADR 误写为 int,
// 而权威件 ADR-006 Amendment A 定义其为 long(其 Consequences 亦按 8 字节计
// Seq 的流膨胀代价)。二者都能编译,但自定义编码器(ADR-006 五)写出的流宽
// 差 4 字节 ⇒ 读流错位。以 long 为准 —— 本块只是前向指针,不是第二份权威。
public readonly struct SimEvent      // 权威定义见 ADR-006 Amendment A
{
    public readonly long      Tick;
    public readonly PatientId Patient;
    public readonly long      Seq;    // (Tick, Patient) 内的单调流水号
    public readonly EventKind Kind;
    public readonly EventPayload Payload;
    // ⚠️ 2026-09-22 U0-b b1b 前向指针更新(原文不删):`EventPayload` 占位型已由
    //   ADR-006 **Amendment G-2** 改判 = `PayloadRef`(header/blob 分家,载荷经 Sim.Codec 解码)。
    //   字段序 / 前四字段不变(G-1 抄本单一化即此序)。本块只是前向指针,不是第二份权威。
}
```

### Implementation Guidelines

1. **先写 `Fix` 与 SplitMix64,再写任何病种。** 顺序反了会写出浮点版再改,等于重写。
2. **`SimEvent` 必须可全序** —— **跨流**按 `(Tick, StreamPriority, Patient, Seq)`,
   **单流内**按 `(Tick, Patient, Seq)`;否则回放无法检测乱序。
   > **2026-09-15 复查轮修正(N-2)**:原句只给 `(Tick, Patient, Seq)`。ADR-006
   > **Amendment C** 已将**跨流全序键升格**为 `(Tick, StreamPriority, Patient, Seq)`
   > (病史流 < 病例流)。缺 `StreamPriority` 则同 tick 的病史 / 病例事件之间
   > **顺序无定义** ⇒ 合并排序不稳定 ⇒ F-37.1 的 fires-once 前提失守(静默类)。
   > **2026-09-15 ADR-009 追加**:`StreamPriority` 升为**三值**(病史 < 病例 < 世界),
   > 键形状不变,取值域以 ADR-009 Amendment E 为准。
3. **注册表的曲线严禁写成 `AnimationCurve`** —— 那绑 `UnityEngine`,违反规则七且不可跨平台。
   必须是纯数据参数(见 9 的 F1:`A_peak` / `τ_rise` / `τ_fall` …)。
4. **注册表写入时做值域校验**(如 `relapse_interval > 0`)——
   配成 0 或负会让病人**每 tick 复发一次**。
5. **病史事件流要有上限 + 折叠规则**:终态(痊愈 / 死亡)病人折叠成一行
   `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)`。开放世界里事件流会无界增长。
   > **⚠️ 2026-09-14 修正(ADR-006 Amendment B / D-9-E)**:原折叠元组**漏了 `patient_id`**。
   > 折叠会**丢弃流位置**,而 `patient_seed = hash(world_seed, patient_id)` 是**单向**的 ——
   > 于是「所有高 id 病人都已折叠」后,`max(patient_id)` **无法重构**,
   > 新主机重建计数器时必然回退,导致 **id 复用 ⇒ 两个病人共用一个 `patient_seed` ⇒ 病程互写**。
   > **这是静默的正确性漏洞,不是性能问题。** 折叠行补一个 `patient_id` 字段即可堵住。

## Alternatives Considered

### Alternative 1: 浮点 + 接受微小不一致

- **Description**:全部用 `float` / `Math.Exp`,联机时容忍末位差异
- **Pros**:少写 200 行;数值直觉直接;无需定点库
- **Cons**:联机核对时「同一病人两台机器读数不同」。
  **基本四则运算在同指令序下逐位一致,但 `Math.Exp` / `Math.Pow` 走各平台 libm,跨平台不保证逐位**;
  FMA 融合取决于 IL2CPP 转译后的 C++ 编译器选项
- **Estimated Effort**: 更小(约 -200 行)
- **Rejection Reason**: **本项目需要客户端本地求值**(见 Decision 三),所以不一致会直接暴露给玩家。
  「浮点 + 接受漂移」只在客户端完全不回放时才成立 —— 那不是本项目的模型

### Alternative 2: int32 千分位定点

- **Description**:放大 1000 倍用 int32 承载
- **Pros**:比 Q16.16 直观,调试时肉眼可读
- **Cons**:中间乘法极易溢出(两个 10⁶ 量级相乘即超 int32);精度 1/1000 对**保守带算法太粗**
- **Estimated Effort**: 相近
- **Rejection Reason**: 保守带的正确性依赖精细的误差预算,1/1000 会让带的宽度失去意义

### Alternative 3: 引入第三方定点 / 数学库

- **Description**:用现成的高精度或定点数学库
- **Pros**:省事,通常经过测试
- **Cons**:引入外部依赖;本项目**尚未有任何已批准依赖**;
  定点需求本身很窄(只需四则 + 整数哈希 + 插值)
- **Estimated Effort**: 初期更小,长期维护成本不可控
- **Rejection Reason**: 200 行自研的维护成本低于引入并锁定一个第三方库。
  **且本项目一条硬规矩是未获批依赖不得登记**

## Consequences

### Positive

- 模拟逐位可复现 → 联机核对、回放、调试三者同时受益
- 状态可序列化为整数 → 7a 持久化**无端序 / 格式坑**
- 离线补算 O(类 A 边界) → 存档小、跨版本不漂移
- **不依赖任何 post-cutoff API** → 引擎知识缺口(HIGH)对本裁决无影响

### Negative

- 数值直觉变差:调参时要在定点域想问题
- 多 200 行 `Fix` + 一条 code review 检查项(门面是唯一浮点出口)
- 注册表曲线的参数化必须显式(不能用 `AnimationCurve` 图省事)

### Neutral

- `boundary_mode: monotone | scan` 成为每个病种的注册表字段 ——
  这**不是**坏事,它把「哪个病种不能闭式求解」从运行时意外变成**写下来的数据**

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 定点库写出溢出 / 舍入错误 | 中 | 高 | 中间乘法落 Q32.32;**保守带内向下舍入**;为 `Fix` 写 EditMode 单元测试(边界值 + 随机对拍浮点参考实现) |
| 某个病种的曲线无法闭式求解 | 中 | 中 | 注册表 `boundary_mode: scan` 兜底;**登记为数据**,不靠运行时发现 |
| 有人绕过门面直接用 `Fix` | 中 | 高 | 独立程序集 + 无隐式转换 + code review 检查项 |
| 病史事件流无界增长 | **高** | 中 | 终态折叠 + 上限;**P0 就要做**,不能等到存档爆掉 |
| IL2CPP 实测结果与预期不符 | 低 | 低 | **本裁决不依赖该实测** |

## Performance Implications

> **状态:临时值** —— 项目性能预算本身待定(见 `technical-preferences.md`)。
>
> **2026-09-15 注记 —— 本表「数十病人」自引已作废。** `disease-simulation.md:560-564` 已正式撤销
> 该数字(「『数十』在 `src/` 没有任何实现背书」),并把**病人数范围**登记为 **OQ-8**:
> ~~「在场才模拟 vs 全域模拟」未定~~ ⇒ 实体数是**性能与内存的前置输入**,须在写第一个 `Step` 之前标定。
> 下表 CPU 行的量纲按「**实体数 × tick 频率 × 单次求值**」读,~~具体系数待 OQ-8 标定~~ → **✅ 2026-09-20 前两因子已标定**。
> 该输入同时是 **R-11(是否 DOTS)** 的判据前置 —— ✅ 同日一并结清(见 ADR-017 §三:CAP 24 < 门阈 100 ⇒ 门在 sim 永不触发)。
>
> **2026-09-17 注记 —— 量纲的第二个因子同样无值(OQ-25-8 · 由 25 GDD 回填 · R16)。**
> 「tick 频率」本身是未标定项:`combat-and-weapon-lines.md` 的占用门 `cooldown_ticks`、感知事件率上界、
> `Decay_injury` 半衰期三处量纲全押在它之上,登记为 **OQ-25-8**。须与 OQ-8 同批标定。
>
> **✅ 2026-09-20 结案注(OQ-8 + OQ-25-8 同批裁定 —— 两个乘数就此有值)**:
> 模拟范围 = **在场才模拟** · `PATIENT_APPEARANCE_CAP = 24` · `TICK_SECONDS = 0.05`(**20 Hz**)。
> ⚠️ **改 `TICK_SECONDS` = 重导全部事件时间戳与存档**,属本 ADR / 9 级动作,不得由各系统自填。
> ⚠️ 20 Hz 与 60 fps(16.6 ms)**不整除** ⇒ `Step` 必须由 `ITickProvider` 驱动、**不得挂在渲染帧上**(相位差须由本 ADR 的 tick 边界吸收)。

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | **量纲已定(2026-09-20)= 24 实体 × 20 Hz × 单次求值**(原「数十病人」自引已撤销,现上界 24;⚠️ 残留 = **单次求值成本**,归实测,非裁决项) | 16.6 ms 平面 / 11.1 ms VR |
| Memory | — | 可忽略(事件流按病人数 × 事件数) | 待定 |
| Load Time | — | 无影响 | 待定 |
| Network(若适用) | — | 事件 ≈ 20 字节/条(P1b) | 待定 |

## Migration Plan

**本项目尚无 9 的实现,故无迁移 —— 本 ADR 是「第一次就做对」。**

1. 写 `Fix`(Q16.16)+ SplitMix64 + `Fix` 的 EditMode 单元测试
   —— *验证:边界值 + 随机对拍浮点参考实现*
2. 定义**五个接口**(`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery`
   / **`IEventAuthority`**),P0 实现为本地占位
   > **2026-09-15 复查轮修正(N-4)**:原稿写「四个接口」且不含 `IEventAuthority`。
   > ADR-007 §一 已将后者定为第六抽象点,而 52 的掷骰路径在 P0 就要接上它 ——
   > 照原清单做,`IEventAuthority` 会缺席,P1b 补接线 = 重构掷骰调用点。
3. 写 `SimEvent` 与病史事件流(含**终态折叠**)
4. 在此之上写 F1-F4(见 `design/gdd/disease-simulation.md`)

**Rollback plan**:定点 → 浮点是**逐点替换**的(所有 `Fix` 换成 `float`,删掉舍入纪律),
**但联机能力会随之失去**。若最终不做联机,回退成本可接受;若做联机,**本 ADR 不可回退**。

## Validation Criteria

- [ ] 同一病史事件流在**两个不同平台**上产出**逐位相同**的 `Fix` 读数
- [ ] 一个离线 30 天的病人,`CatchUp` 的求值次数**不随天数线性增长**(实测计数)
- [ ] `Fix` 的单元测试覆盖边界值,且与浮点参考实现的偏差 ≤ 误差预算
- [ ] 模拟层程序集**零 `UnityEngine` 引用** —— **双门判据**(2026-09-15 复查轮 N-4 补,与本文 §Context 一致):
      **门 A**:sim 程序集为独立 asmdef 且 `"noEngineReferences": true`;
      **门 B**:EditMode 反射测试断言 sim 程序集**不引用** `UnityEngine.*` / `Unity.Mathematics` /
      `Unity.Collections`,**不含**对 `Math.Exp` / `Pow` / `Sqrt` / `Log` / `Abs` 的 IL 调用,
      且 sim 类型签名中**不出现** `float` / `double`。
      > **只有门 A 不够**:`System.Math.Exp` 住在 `System.Runtime` 里,程序集隔离看不见它。
      > 原稿只写「编译期可验证」,而该判据**不可完成**(报告 E-1)。
- [ ] 存档中不出现任何 `float`(事件流全整数,可 grep 验证)
- [ ] 终态病人的事件流**折叠后不超过 1 行**
- [ ] **`patient_id` 迁移稳定性**(ADR-006 Amendment B):给定一个含已折叠终态病人的流,
      新 authority 重构出的 `next` **严格大于流中出现过的所有 `patient_id`**;
      且**全流回放后 `patient_id → patient_seed` 映射逐位一致**(无 id 复用、无病程改写)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | 规则一:一切推进走定 tick,禁止 `Time.deltaTime` / 帧数 / 真实墙钟 | `ITickProvider` 是全案 tick 唯一来源;`Fix` 域无时间浮点 |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | 规则二:病程是解析式,任意 t 可直接求值,离线 = O(处置次数) | F3 的类 A 闭式边界 + 类 B 保守带;`boundary_mode` 登记为数据 |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | 规则六:存档存病史事件流,不存逐帧状态;`patient_seed` 不可用 `Random.Range` | `SimEvent` + `IEventSink`;SplitMix64 整数哈希 |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | 规则七:状态是可序列化纯值类型,不挂脚本、不持 `GameObject` 引用 | `Fix` 无行为纯 struct;sim 层独立程序集零 `UnityEngine` 引用 |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | 规则八:34 公卫只订阅聚合事件(出向契约) | 聚合事件由**主机广播**,避免各机器 tick 漂移导致计数不同 |
| `design/gdd/systems-index.md` | — | §9 **C6**:9 的 tick 模型与 7 的持久化从第一天起 **authority-agnostic** | 本 ADR 的全部内容 |
| `design/gdd/systems-index.md` | — | §8:9 的爆点是「第 3-4 个月做存档时」 | 本 ADR 把该爆点前移到设计期,并用五条 Validation Criteria 守住 |

## Related

- **ADR-006 定点域边界数据契约**(Accepted 2026-09-14)—— **本 ADR 的边界补完**。
  它补齐了本 ADR 未定义的「域边界」:外部数据 → `Fix` 的唯一解析入口、
  存档禁浮点、单一舍入模式、守恒律的域内表达。
  **并以后续修正案窄修正本 ADR 的缺陷(Amendment A–D)并升格三流口径(Amendment E · ADR-009)**;
  **Amendment F(边界程序集)由本 ADR 引入(2026-09-16,见本节末)**:
  - **Amendment A(D-9-D)**:本 ADR 原 Key Interfaces 块的 `SimEvent{Tick, Patient, Kind}`
    **装不下本 ADR §Implementation Guidelines 2 自己要求的全序**,也无处安放载荷。
    ADR-006 补 `Seq` 与 `Payload` 两字段 —— **本 ADR 的 `SimEvent` 定义以 ADR-006 为准**
    (复查轮 N-1 已把本 ADR 抄错的 `Seq` 类型 `int` 对齐为 `long`)。
  - **Amendment B(D-9-E · ✅ 已裁决 2026-09-14)**:**采纳备选 A(计数器 + 高水位可重构)**。
    终态折叠行**补保留 `patient_id`**(见 Implementation Guidelines 第 5 条),
    迁移时 `next = max(patient_id) + 1`、**永不复位 0**。**备选 B(纯哈希派生)已否决** ——
    理由见 ADR-006 Appendix B。本 ADR §Decision 三 的 `patient_id` 条与折叠规则**已就地修正**。
  - **Amendment C(2026-09-15)**:`Seq` 的**发放域唯一** = 两条流共享 `(Tick, Patient)` 计数域;
    **跨流全序键升格为 `(Tick, StreamPriority, Patient, Seq)`** —— 见上 §Implementation Guidelines 2。
  - **Amendment D(2026-09-15)**:**真源 = 病史流 ∪ 病例流**;终态折叠**只作用于病史流**。
   本 ADR §Summary 与 §Decision 三 已据此修正;`:122` 之类的旧行号**不再可靠**。
  - **Amendment E(2026-09-15 · ADR-009)**:**真源 = 病史流 ∪ 病例流 ∪ 世界流**;
   `StreamPriority` 三值(病史 < 病例 < 世界);终态折叠仍只作用于病史流,
   病例流与世界流**永不物理折叠**。本 ADR §Summary / §Decision 三 / 架构图已据此修正。
  - **Amendment F(2026-09-16 · 边界程序集)**:**把「边界层」从「某个特定系统的领域」
    重定义为「任意两个住门 A 内外两侧的系统之间的通信契约」**,并新立**边界程序集**
    (boundary assembly)承载它 —— `WorldPos`(ADR-015 §三/§七)+ 六个 P0 抽象点
    (§Key Interfaces:`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery` /
    `IEventAuthority` + `SimEvent` 及其整数枚举 `PatientId` / `StreamId` / `EventKind`),
    **零 `UnityEngine` 引用**;**sim 实现程序集与表现层都引用它**。
    **根因**:ADR-005 §二 与 ADR-015 §Implementation Guidelines 2 把 `WorldPos` 定为
    **住 sim 程序集**,而 ADR-020(修订后)要求**表现层不引用 sim 程序集却要发含 `WorldPos` 的事件**
    ⇒ **表现层物理上拿不到它要求它构造的类型**。这是程序集划分的**缺失**,不是措辞分歧。
    **本修正案只做三件事**:① 新立边界程序集;② 明确**门 A(`"noEngineReferences": true`)
    只约束 sim 实现程序集**(它是**单向**约束 —— 「sim 不得依赖引擎」,不是「引擎侧不得引用 sim 契约」);
    ③ 明确**门 B(IL 反射扫描)的扫描面仍只覆盖 sim 实现程序集** —— 边界程序集**无需**门 B
    (它没有可执行的方法体,只有 `readonly struct` 与接口签名)。**不改变**整数定点域、
    事件流唯一真源、六个抽象点的语义。**同步修订**:ADR-015 §Implementation Guidelines 2 ·
    ADR-020 Status 的 **Amendment A 补注** + Verification Required ② · `architecture.yaml`
    的 `world_coordinate_lattice` 契约。
    > ⚠️ **本法不得被读作「表现层可以自由引用 sim」** —— 边界程序集是**白名单**,
    > 不是通道:表现层能碰的**只有**上面列举的类型。新增一个跨门类型须**追加进本法**,
    > 不得就地塞进边界程序集。
  - **本 ADR 的两项核心裁决不受影响**:整数定点域(**§Decision 一**)与事件流唯一真源
    (**§Decision 三**,对象已由单条病史流扩为**三流**并集)照旧 Accepted。

> **2026-09-15 复查轮修正(N-5 · 引用口径变更)**:
> 本 ADR 与 ADR-006 / 007 / 008 中指向本 ADR 的 **`\`:NNN\` 行号引用已全部废止**,
> 改为**章节锚点**(如「§Decision 一」/「§Decision 三」/「§Implementation Guidelines 2」)。
> 原因:本 ADR 因历次就地修正注已增长约 50 行,所有旧行号**集体失准** ——
> 复核者已两次据此误判段落。**后续新增引用一律用章节名,不得再写行号。**
- **ADR-001 联机选型**(待建)—— 本 ADR **追加一条约束**:
  选型须支持**自定义可靠有序消息流**。Netcode for GameObjects 与 Photon Fusion **均满足**,
  故本 ADR **不改变 ADR-001 的裁决空间**
- `design/gdd/disease-simulation.md` —— 本 ADR 是它的公式层实现基础
- `design/registry/entities.yaml` —— 待登记 `Fix` 相关常量与 F1-F4 公式
