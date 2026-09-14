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
本 ADR 裁决:**全部模拟数学在整数定点域(int64 / Q16.16)**,**病史事件流是唯一真源**,
**主机唯一执行 Step / CatchUp**,并给出 **P0 必须预留的五个抽象点**。
不现在做,P1b 重构是三个月级灾难。

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
- 中途加入的玩家能重建一个已病了 N 天的病人
- **不阻塞 ADR-001 的选型**

## Decision

### 一、数值域:整数定点

**裁决:全整数域。** 具体形态 = **int64 承载 Q16.16 标度的 `Fix` 纯值 struct**,
中间乘法落 Q32.32 再移位回。哈希用 **SplitMix64**,插值用整数除法。

- **不选 int32 千分位**:乘法溢出风险,且 1/1000 精度对保守带太粗
- **不选裸 Q16.16 int32**:同样溢出

**舍入纪律**(必须写进代码规范,否则二分边界会漂移):

| 场景 | 舍入方向 |
| ---- | ---- |
| 保守带内的边界搜索比较 | **向下保守**(宁多扫一步) |
| 表现层输出 | 就近舍入 |

**误差预算**:每次运算 ≤ 1 ulp(2⁻¹⁶);保守带按 `ops × 2⁻¹⁶` 外扩
(实测 ≤ 3×2⁻¹⁶),可并入 σ 的安全裕量。

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
- **主机唯一执行 `Step` / `CatchUp`**
- 客户端持**流副本 + 定期快照**;UI 展示可**本地求值 `Progress`**(纯函数,定点保证逐位一致)
  —— **这不算「跑模拟」**,因为绝不写回事件流
- **`patient_id`**:静态病人由**世界种子派生**;动态生成的病人由 **主机从单调计数器分配**,
  **首行流事件即 `(patient_id, patient_seed, 病因)`**,id 随流持久化
  > **⚠️ 修正(2026-09-14 · ADR-006 Amendment B / D-9-E)**:上句**缺了迁移不变量**。
  > 由于 `patient_seed = hash(world_seed, patient_id)`(`disease-simulation.md:143`),
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
                    │   病史事件流 (唯一真源)                │
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
// ── 定点标量:无行为纯 struct,序列化只存内部 long ──
public readonly struct Fix            // Q16.16, 内部 long
{
    private readonly long _raw;
    // 注意:不定义 implicit operator float —— 这是刻意的
    public float ToFloat();           // 仅门面程序集可调用
}

// ── P0 必须预留的五个抽象点(实现可为占位)──
public interface ITickProvider  { long CurrentTick { get; } }        // 全案 tick 唯一来源
public interface IEventSink     { void Append(in SimEvent e); }       // P0 = 本地 list
public interface IIdAuthority   { PatientId Next(); }                 // 防运行时 instance id
public interface IVitalsQuery   { VitalsDto GetVitals(PatientId p); } // 唯一浮点出口

// ── 事件必须带逻辑 tick 且可全序 ──
public readonly struct SimEvent
{
    public readonly long      Tick;
    public readonly PatientId Patient;
    public readonly EventKind Kind;
}
```

### Implementation Guidelines

1. **先写 `Fix` 与 SplitMix64,再写任何病种。** 顺序反了会写出浮点版再改,等于重写。
2. **`SimEvent` 必须可全序**(按 `(Tick, Patient, Seq)`),否则回放无法检测乱序。
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

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 可忽略(数十病人 × 每处置一次求值) | 16.6 ms 平面 / 11.1 ms VR |
| Memory | — | 可忽略(事件流按病人数 × 事件数) | 待定 |
| Load Time | — | 无影响 | 待定 |
| Network(若适用) | — | 事件 ≈ 20 字节/条(P1b) | 待定 |

## Migration Plan

**本项目尚无 9 的实现,故无迁移 —— 本 ADR 是「第一次就做对」。**

1. 写 `Fix`(Q16.16)+ SplitMix64 + `Fix` 的 EditMode 单元测试
   —— *验证:边界值 + 随机对拍浮点参考实现*
2. 定义四个接口(`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery`),
   P0 实现为本地占位
3. 写 `SimEvent` 与病史事件流(含**终态折叠**)
4. 在此之上写 F1-F4(见 `design/gdd/disease-simulation.md`)

**Rollback plan**:定点 → 浮点是**逐点替换**的(所有 `Fix` 换成 `float`,删掉舍入纪律),
**但联机能力会随之失去**。若最终不做联机,回退成本可接受;若做联机,**本 ADR 不可回退**。

## Validation Criteria

- [ ] 同一病史事件流在**两个不同平台**上产出**逐位相同**的 `Fix` 读数
- [ ] 一个离线 30 天的病人,`CatchUp` 的求值次数**不随天数线性增长**(实测计数)
- [ ] `Fix` 的单元测试覆盖边界值,且与浮点参考实现的偏差 ≤ 误差预算
- [ ] 模拟层程序集**零 `UnityEngine` 引用**(编译期可验证)
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
  **并以两条修正案窄修正本 ADR 的两处自相矛盾**:
  - **Amendment A(D-9-D)**:本 ADR `:181-186` 的 `SimEvent{Tick, Patient, Kind}`
    **装不下 `:192` 自己要求的 `(Tick, Patient, Seq)` 全序**,也无处安放载荷。
    ADR-006 补 `Seq` 与 `Payload` 两字段 —— **本 ADR 的 `SimEvent` 定义以 ADR-006 为准**。
  - **Amendment B(D-9-E · ✅ 已裁决 2026-09-14)**:**采纳备选 A(计数器 + 高水位可重构)**。
    终态折叠行**补保留 `patient_id`**(见 Implementation Guidelines 第 5 条),
    迁移时 `next = max(patient_id) + 1`、**永不复位 0**。**备选 B(纯哈希派生)已否决** ——
    理由见 ADR-006 Appendix B。本 ADR `:126-127` 与折叠规则**已就地修正**。
  - **本 ADR 的两项核心裁决不受影响**:整数定点域(`:90-118`)与事件流唯一真源(`:122`)照旧 Accepted。
- **ADR-001 联机选型**(待建)—— 本 ADR **追加一条约束**:
  选型须支持**自定义可靠有序消息流**。Netcode for GameObjects 与 Photon Fusion **均满足**,
  故本 ADR **不改变 ADR-001 的裁决空间**
- `design/gdd/disease-simulation.md` —— 本 ADR 是它的公式层实现基础
- `design/registry/entities.yaml` —— 待登记 `Fix` 相关常量与 F1-F4 公式
