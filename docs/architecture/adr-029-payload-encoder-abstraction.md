# ADR-029: 载荷编码抽象点 (`IPayloadEncoder`)

## Status

Draft —— 待用户裁定(2026-10-02 起草)

> **Ordering Note**: 本 ADR **新增** ADR-005 的**第七个 P0 抽象点**,并**就地订正** ADR-005
> 「六个抽象点」计数(五处)。同时**补 ADR-006 Amendment G-2 留下的洞** —— G-2 只定了
> 载荷的**读形**(`PayloadRef` 寻址 + blob 池),未定**写形**(谁编码、谁入池)。
> **不推翻**任何既有裁决:门 A / `Sim` 引用集白名单(ADR-025 §①)/ b2 门三者全部保留。

## Date

2026-10-02

## Last Verified

2026-10-02

## Decision Makers

dr_guyang(用户 · **2026-10-02 两项裁定**)· technical-director(起草)
· 6 世界与生态区(we B1 的提出方)· 6 建造(modular B4 的提出方)· 7a 持久化 / 45 网络(池归属)

## Summary

**系统 6 的两个写者(`PoiStateMachine` / `StructureKinds`)要产出一个 `PayloadRef`,
但它们物理上够不着编码面。** 这是 ADR-006 Amendment G-2 遗留的洞:

| 环节 | 住在哪 | `Sim` 能看见吗 |
|---|---|---|
| `PayloadCodec.Encode(payload)` → `byte[]` | `Sim.Codec` | ❌ |
| 不可变 blob 池(存这段字节) | 7a / 45 侧 | ❌ |
| `PayloadRef { BlobId, Offset, Length }` | `Sim.Contracts` | ✅ |

⇒ `Sim` 的写者**无法合法地产出 `PayloadRef`**。实测后果:全库**每一个**写者都在
**手搓 `PayloadRef`**,且**没有任何一段字节真的进过池**:

```csharp
// ① PoiStateMachine.cs:104(we B1)—— 业务 id 当 blobId、枚举当字节偏移
new PayloadRef(blobId: poiId, offset: (int)toState, length: 8)

// ② StructureKinds.cs:191-198(modular B4)—— bit-packing + 死代码
new PayloadRef(blobId: structureId, offset: moduleId | (orientation << 16) | (variant << 24), length: 8)

// ③ craft_event_payload_test.cs:280 —— 全库「最正确」的写路径,同样是假的
new PayloadRef(0, 0, blob.Length)     // blobId 恒 0,字节被丢弃
```

**「接 `Sim.Codec`」这条路是关闭的** —— 不是待裁空白:`Sim` 引用集白名单**恰 =
{BCL, `Sim.Contracts`}**(ADR-025 §①:111),且 b2 门已把它变成构建失败
(`AssemblyGates.cs:148-150`)。ADR-025 §一 的驳回理由是实质的:`Sim.Codec` 内含
`SaveService` / `SaveCodec` / `SaveHeader` 的**文件 I/O 面**,拖进来会污染热路径装配。

**本 ADR 裁决**:新增**第七个 P0 抽象点 `IPayloadEncoder`**(住 `Sim.Contracts`),
把「编码 + 入池」收口为一个 Sim 可见的整数域接口。`Sim` 的引用集**一字不改**。

## Engine Compatibility

**不依赖任何 post-cutoff API**。本件 = 纯 C# 接口契约与装配边界裁决,零引擎 API。
`Sim.Contracts` 保持 `noEngineReferences: true`。

**Engine Knowledge Risk**: **LOW**。

## ADR Dependencies

| 方向 | ADR | 关系 |
|---|---|---|
| Depends On | **ADR-005** | 抽象点集合的出处;本件追加第七个,并订正其计数 |
| Depends On | **ADR-006 Amendment G-2** | 载荷 header/blob 形态(读形);本件补其**写形** |
| Depends On | **ADR-025 §①** | `Sim` 引用集白名单 = {BCL, `Sim.Contracts`} —— 本件**遵守**,不修订 |
| Depends On | **ADR-017 §二** | 门 A(noEngineReferences);b2 门 |
| Depends On | **ADR-024 §⑤** | kindgen 生成器先例(本件的分派表拟走同一通道) |
| Depends On | **ADR-010 §三** | 义务汇总表;本件拟追加一条义务 |
| Enables | 6 世界与生态区(we B1) | 闭合 `PoiStateChanged` 的编码路径 |
| Enables | 6 建造(modular B4) | 闭合 `StructurePlaced` / `StructureModified` 的编码路径 |
| Relates | **ADR-008 §五** | 病例流不折叠;载荷字节的落盘由 7a 承载 |

## Context

### Problem Statement

`Sim` 的写者需要**三样**才能合法产出一条带载荷的事件:

1. 把强类型载荷编码成字节 —— 编码器住 `Sim.Codec`(够不着);
2. 把字节存进不可变 blob 池 —— 池住 7a/45(够不着);
3. 拿到 `PayloadRef` —— 住 `Sim.Contracts`(够得着)。

**第 1、2 步没有任何合法路径。** 结果不是「暂时没接」,而是**每个写者各自发明一种错法**,
且**三种错法彼此不兼容**:①② 把 `Offset` 当业务字段用(与 `PayloadRef` 契约
「`Offset` = 字节偏移」冲突),③ 干脆丢掉字节。

### Current State(实测 HEAD `1e8bdb7`)

| 事实 | 证据 |
|---|---|
| `Sim` 引用集 = `["Sim.Contracts"]` | `unity/Assets/Sim/Sim.asmdef` |
| b2 门把 `Sim.Codec` 列为违例 | `AssemblyGates.cs:148-150`(`.Except(new[]{"Sim.Contracts"})`) |
| `Sim` 内零 `PayloadCodec.Encode` 调用 | `grep -rn "PayloadCodec\.\(Encode\|Decode\)" unity/Assets/Sim/` = 空 |
| `IBlobPool` **只有读面** | `IBlobPool.cs`:`bool TryGetBlob(int, out ReadOnlyMemory<byte>)` |
| 池的分配/释放**明确未定** | `IBlobPool.cs` 头注:「池的分配/释放/重映射…**不在本批**」 |
| 全库**无**任何编码面抽象 | `grep IPayloadEncoder\|IBlobSink\|IPayloadSink` = 空 |
| 六个抽象点**不含**编码面 | `ADR-005:257-270` |

### Constraints

- **C1**:`Sim` 引用集不得变(ADR-025 §① + b2 门 + 门 A)。
- **C2**:载荷字节必须住**不可变** blob 池(ADR-006 G-2)。
- **C3**:载荷字段须 ∈ **整数域**,`SimEvent` 内禁装箱(ADR-006 G-2「无引用字段」意图保留并加强)。
- **C4**:`PayloadRef` 三字段全 `int`(ADR-006 G-2)。
- **C5**:`Sim.Contracts` 引用集 = BCL(ADR-025 §①:112)—— 新接口只能依赖 BCL + 本装配类型。
- **C6**:31 个 P0 系统里,写者不止 6 —— 本抽象点须对**所有** Sim 写者成立,不是 6 的专用补丁。

### Requirements

- **R1**:`Sim` 的写者能**合法**产出 `PayloadRef`,零手搓。
- **R2**:字节真的进池(不再出现 `PayloadRef(0,0,len)` 式假引用)。
- **R3**:不破 C1–C5 任一条。
- **R4**:分派不得引入装箱(C3)。

## Decision

### ① `IPayloadEncoder` = 第七个 P0 抽象点(住 `Sim.Contracts`)

```csharp
// Sim.Contracts —— 第七抽象点
// 权威来源:本 ADR §① · ADR-006 Amendment G-2(载荷 header/blob 写形)· ADR-005(抽象点集合)
namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 载荷编码器 —— 把强类型载荷编码进不可变 blob 池,返回其寻址引用。
    /// 实现者 = Sim.Codec 的 PayloadEncoder(它看得见 PayloadCodec);
    /// 消费者 = Sim 的全部事件写者(6 / 9 / 25 / 29 / 37 / 52 …)。
    /// </summary>
    public interface IPayloadEncoder
    {
        /// <summary>
        /// 编码 + 入池,返回 <see cref="PayloadRef"/>。
        /// 同一载荷多次编码**不保证**返回同一 BlobId(池去重是实现细节,非契约)。
        /// </summary>
        PayloadRef Encode<T>(in T payload) where T : struct, IPayload;
    }

    /// <summary>
    /// 载荷标记接口 —— 34 支 per-Kind payload struct 均须实现(纯标记,无成员)。
    /// 存在理由:给泛型约束一个可枚举的闭集,使分派可构建期生成并断言完备。
    /// </summary>
    public interface IPayload { }
}
```

**要点**:
- 返回 **`PayloadRef`** 而非 `byte[]` —— **乙案**(用户 2026-10-02 裁定):编码与入池**一体**,
  使 `Sim` 侧只持**一个**依赖。`byte[]` 半途形态不得暴露给 `Sim`(否则 C3 的禁装箱意图被绕)。
- `where T : struct, IPayload` —— **约束泛型,不装箱**(R4)。
- 命名 `IPayloadEncoder` 而非 `IEventEncoder`:它只做**载荷**;header 由 `SimEvent` 承载,不需编码面。

### ② blob 池的**写面**须定义(ADR-006 G-2 的遗留)

乙案要求编码器能**写入**池,而 `IBlobPool` 只有读面。故须补一个写面契约:

```csharp
// Sim.Codec —— 池的写面(与 IBlobPool 的读面配对)
public interface IBlobSink
{
    /// <summary>存入一段不可变字节,返回 BlobId。实现须保证同 BlobId 的内容此后不再变。</summary>
    int Store(ReadOnlySpan<byte> blob);
}
```

**归属**:`IBlobSink` 的实现者 = **池的所有者**。池的生命周期归 7a / 45(`IBlobPool.cs` 头注
「生命周期归流实现(7a / 45 侧)」)—— **本 ADR 不改该归属**,只补上此前缺的**写面签名**。
`PayloadEncoder` 经构造注入 `IBlobSink`(实现细节见 §Implementation Guidelines)。

⚠️ **`IBlobSink` 住 `Sim.Codec` 而非 `Sim.Contracts`**:`Sim` 的写者**不该**直接摸池 ——
它只调 `IPayloadEncoder`。池是 codec 与流实现之间的事,暴露给 `Sim` 会开第二条旁路(R1 失守)。

### ③ 新增判据:Sim 写者不得手搓 `PayloadRef`

```csharp
// 判据(构建期门,拟落 Editor.Tools.Gates 族 —— 与 b2/b4 同族)
// Sim/ 目录下的源文件内出现 `new PayloadRef(` = 违例,唯一合法路径 = IPayloadEncoder。
```

**理由**:本抽象点的价值全在「无旁路」。`PoiStateMachine` 与 `StructureKinds` 的现状
**正是旁路的证据** —— 若不设门,新写者会继续手搓,本 ADR 沦为纸面(与 `Fix.ToFloat()`
的 b4 门同构:接口归属与调用点**分开判**)。

⚠️ **已知漏报面**(与 b4 同款口径):源文本级扫描,注释与字符串字面量会误报(偏安全)。
**例外**:`IPayloadEncoder` 的实现体(住 `Sim.Codec`)当然要构造 `PayloadRef` —— 扫描面
**限 `Sim/` 目录**,不覆盖 `Sim.Codec/`。

### Key Interfaces

```csharp
// ── Sim 写者的合法写路径(6 / 9 / 25 / 29 / 37 / 52 通用)────────────────
// 构造注入(非单例 —— coding-standards「dependency injection over singletons」)
public sealed class PoiStateMachine
{
    private readonly IEventSink _eventSink;
    private readonly IPayloadEncoder _encoder;   // ← 新增,唯一编码路径

    public PoiStateTransferResult TryAdvance(int poiId, PoiState toState, long tick)
    {
        // ...
        var payload = new PoiStateChangedPayload(poiId, (int)toState);
        var evt = new SimEvent(tick, PatientId.None, 0, EventKind.PoiStateChanged,
                               _encoder.Encode(payload));   // ← 零手搓
        _eventSink.Append(evt);
        return PoiStateTransferResult.Success;
    }
}
```

### Implementation Guidelines

- **分派机制**:`PayloadCodec` 已有 34 个具名 `Encode(in XPayload)` 重载。泛型 `Encode<T>`
  的分派**拟走 kindgen 生成**(ADR-024 §⑤ 先例:编辑期 .NET 工具 → 生成 `.g.cs`),
  产出 `typeof(T)` → 具名重载的 switch,**禁装箱**(C3/R4)。
  ⚠️ **生成器须断言完备性**:`IPayload` 的实现集 ⊇ registry 的 34 支 `payload_schema`
  (ADR-024 §① 的真源 = `entities.yaml`),缺一 = 构建失败。
- **`PayloadEncoder` 落点**:`Sim.Codec`(它有 `PayloadCodec`),经构造注入 `IBlobSink`。
- **池的去重**:同内容重复编码**不要求**返回同 BlobId(§① 契约明写);去重是 7a/45 的优化面。
- **`Sim` 侧的测试缝**:`Sim` 的单测注入 `IPayloadEncoder` 的 fake
  (返回可预测的 `PayloadRef`),不依赖真池 —— 保持单测无 I/O(coding-standards)。

## Alternatives Considered

### Alternative 1: 放开 `Sim` → `Sim.Codec` 引用边
- **Pros**:零新增抽象点,直接调 `PayloadCodec`。
- **Rejection Reason**:🔴 **不是待裁项,是已裁的禁止项**。ADR-025 §①:111 明文
  「`Sim` 期望引用集 = BCL + `Sim.Contracts`(仅此一件)」,且 b2 门(`AssemblyGates.cs:148-150`)
  已把它变成构建失败。ADR-025 §一 的驳回理由实质:`Sim.Codec` 含
  `SaveService`/`SaveCodec`/`SaveHeader` 的**文件 I/O 面**,拖进 `Sim` 会污染热路径装配,
  并使 `Fix` 编码器守卫(D-21-18)失去「引用 codec 与否」的天然边界。
- **What Would Change Our Mind**:无 —— 除非推翻 ADR-025 的 L2/L3 分层本身。

### Alternative 2: 编码整体下沉到流实现(`Sim` 只出意图,不构造 `SimEvent`)
- **Pros**:`Sim` 完全不碰载荷;`PayloadRef` 由看得见 codec 的一侧构造。
- **Rejection Reason**:🔴 **改的是 ADR-009 §七 三段式在流层的物化位置** ——
  「意图事件 + 主机当下判距 + 效果进流」中,**意图事件本身**就是 `SimEvent`
  (如 `EmergencyAttempt`)。若 `Sim` 不能构造 `SimEvent`,则判定输入类事件无法进流,
  与 ADR-001 §一之三 裁决一(判定输入走可靠通道)**直接冲突**。改动面远大于新增一个抽象点。
- **What Would Change Our Mind**:若 `SimEvent` 构造被证明可整体外移而不破三段式。

### Alternative 3: 甲案 —— 接口只出 `byte[]`,入池由调用方另走池接口
- **Pros**:`IPayloadEncoder` 不依赖池,职责更纯。
- **Rejection Reason**:使 `Sim` 侧持**两个**依赖(`IPayloadEncoder` + `IBlobSink`),
  且把「拿到裸字节后自己入池」的步骤重新交给写者 —— **正是当前手搓的来源**。
  用户 2026-10-02 裁定取**乙案**(一体)。
- **What Would Change Our Mind**:若池的写面被证明不能安全地注入 codec 侧。

### Alternative 4: 维持现状(各写者手搓 `PayloadRef`,不引入抽象点)
- **Rejection Reason**:实测三种手搓法**彼此不兼容**(见 §Summary),且**零字节进池**
  ⇒ 存档 / 回放 / 跨平台确定性(ADR-012 字节级夹具)**全部不可实现**。
  这不是风格问题,是「回放分叉」级缺陷。

## Consequences

### Positive

- `Sim` 的写者获得**唯一合法**的编码路径,手搓面被门关掉。
- `Sim` 引用集**一字不改** —— b2 门、门 A、ADR-025 §① 全部继续成立。
- 补上 ADR-006 G-2 的写形洞:字节**真的**进池,`PayloadRef` 不再是假引用。
- 为 ADR-012 的**字节级**黄金夹具解锁前提(此前无真实 blob 可对拍)。

### Negative

- **抽象点计数从六变七** —— ADR-005 五处计数须订正(§Migration Plan 列明)。
- 34 支 payload struct 须加 `IPayload` 标记 + 一次 kindgen 生成。
- `IBlobSink` 是**新增契约面**,须 7a / 45 各自实现(此前只有读面)。

### Neutral

- 池的**归属**不变(仍 7a / 45);本件只补写面签名。
- `IBlobPool` 读面不变。

## Risks

| 风险 | 可能性 | 影响 | 缓解 |
|---|---|---|---|
| 分派经泛型引入装箱(C3 违) | 中 | 中 | 分派走 kindgen 生成的具名重载 switch;EditMode 探针断言无 `box` IL |
| kindgen 完备性失守(漏一支 payload) | 中 | 高 | 生成器对 `entities.yaml` 的 34 支 `payload_schema` 做双向差集断言(承 ADR-024 A5 口径) |
| `Sim` 写者绕过抽象点继续手搓 | 高 | 高 | §③ 新增门(`Sim/` 内 `new PayloadRef(` = 红) |
| `IBlobSink` 被 `Sim` 直接引用,开第二旁路 | 中 | 中 | `IBlobSink` 刻意住 `Sim.Codec`(§②),`Sim` 引用集白名单天然挡住 |
| 池实现方(7a/45)对写面理解不一 | 中 | 中 | §② 只定签名;`Store` 的并发 / 去重 / 容量归各自 ADR 轮 |

## Performance Implications

- **编码时机**:每 `Append` 一次编码(非每帧)。事件率上界已由 ADR-009 §六 / ADR-016 §三 钉为
  **tick 频率**(20 Hz),故编码开销 = O(事件数) 而非 O(帧数)。
- **分配**:`PayloadCodec.Encode` 现返 `byte[]`(每次分配)。乙案下字节立即进池 ⇒
  可优化为池内直写(池侧实现细节,不在本件)。
- **热路径**:`IPayloadEncoder.Encode` 在 `Sim` 的写路径上,但**不在 `Step` 的求值热路径**
  (仅事件产生时)。ADR-005 的 `L_eval ≤ TICK_PERIOD = 50 ms` 预算**不受影响**。

## Migration Plan

### 本 ADR(仅裁决)

本件**只写裁决**,不落实现 —— 用户 2026-10-02 明令「本轮只写 ADR-029」。

### 实现轮(须另立 story,建议拆两支)

1. **契约支**:`Sim.Contracts` 加 `IPayloadEncoder` + `IPayload`;34 支 payload struct 加标记;
   `Sim.Codec` 加 `IBlobSink` + `PayloadEncoder`;kindgen 生成分派表。
2. **接线支**:`PoiStateMachine`(we B1)· `StructureKinds`(modular B4)改走 `IPayloadEncoder`;
   同时删 `StructureKinds.cs:191-198` 的死代码;新增 §③ 的门。

### ADR-005 计数订正(五处)

| 位置 | 原文 | 改为 |
|---|---|---|
| `adr-005:25` | 「**六个**抽象点」 | 七个 |
| `adr-005:31` | 「原稿写『五个』… 之后为 **六个**」 | 追加注:ADR-029 后为**七**个 |
| `adr-005:257` | 「五个 + 第六个 `IEventAuthority`」 | 追加第七个 `IPayloadEncoder`(ADR-029) |
| `adr-005:474` | 「**六个** P0 抽象点」 | 七个 |
| `adr-005:485` | 「**六个**抽象点的语义」 | 七个 |

⚠️ 以上订正**归实现轮**,本件只登记(不偷改 —— 承 ADR-024 §② 的「就地降级但登记」纪律)。

### ADR-010 §三 义务追加

拟追加**义务 15**:blob 池的**写面**实现(`IBlobSink`)—— 归属 7a 持久化 / 45 网络,
与既有「池生命周期归流实现」口径一致。**本件只登记落点,不落表**。

## Validation Criteria

- [ ] **V-1** `Sim.Contracts` 的 `IPayloadEncoder` 引用集 ∈ {BCL, 本装配类型} —— 零 `Sim.Codec` 类型。
- [ ] **V-2** `Sim` 引用集实测仍 = `["Sim.Contracts"]`(本件不改 `Sim.asmdef`)。
- [ ] **V-3** `Sim/` 目录内 `new PayloadRef(` 零命中(§③ 门)。
- [ ] **V-4** 34 支 payload struct 全部实现 `IPayload`;与 `entities.yaml` 双向差集归零。
- [ ] **V-5** 泛型分派无装箱(`box` IL 零命中)。
- [ ] **V-6** `PoiStateChanged` / `StructurePlaced` / `StructureModified` 三条载荷经
  `IPayloadEncoder` 编码后 `Encode→Decode` 逐字段往返一致。
- [ ] **V-7** b2 门仍绿(`Sim` 引用集违例零)。
- [ ] **V-8** ADR-005 五处计数订正落盘。

⚠️ **本表全部未勾** —— 本件为 Draft,禁借绿。

## GDD Requirements Addressed

| GDD | 系统 | 本 ADR 覆盖 |
|---|---|---|
| `design/gdd/world-and-ecozones.md` | 6b 生态区与 POI | `PoiStateChanged` 载荷的合法编码路径(we B1 的根因) |
| `design/gdd/modular-building.md` | 6 建造 | `StructurePlaced` / `StructureModified` 载荷的合法编码路径(modular B4 的根因) |
| (通用) | 全部 Sim 写者 | 载荷编码的唯一合法路径(31 系统共用) |

> 本件**不新增 TR 条目** —— 它是横切契约面。we B1 / modular B4 的 TR 归各自 epic 轮补。

## Related

- `docs/architecture/adr-005-deterministic-sim.md` —— 抽象点集合(本件追加第七个)
- `docs/architecture/adr-006-fixed-point-boundary-contract.md` —— Amendment G-2(载荷读形)
- `docs/architecture/adr-025-contract-assembly-manifest.md` —— §① `Sim` 引用集白名单
- `docs/architecture/adr-024-kind-single-source.md` —— §⑤ kindgen 先例
- `docs/architecture/adr-010-persistence-save-format.md` —— §三 义务表 / 池归属
- `unity/Assets/Sim.Codec/IBlobPool.cs` —— 池读面(本件补写面)
- `production/qa/evidence/reconciliation-world-ecozones-2026-10-02.md` —— we B1
- `production/qa/evidence/reconciliation-modular-building-2026-10-02.md` —— modular B4
