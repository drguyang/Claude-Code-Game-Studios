# ADR-031: 载荷解码抽象点 (`IPayloadDecoder`)

## Status

**Accepted** —— 2026-10-15(用户逐项确认后转 Accepted)

> **本件的由来**:S12-9(53 `Outcome = Judge(P,C,D,U)` 实现)的**依赖面勘察**实测四入向
> 4/4「需补 / 不存在」,触发该 story 卡面自带的收缩判据(`story-002:112`)⇒ **S12-9 退回单独立项**。
> 勘察定性其**根因不是四笔独立欠账,而是同一条结构性缺口** —— 本件即该缺口的补法。
> 勘察原件:`production/qa/evidence/recon-s12-9-medcons-outcome-judge-2026-10-15.md`。
>
> **用户裁定(2026-10-15 · 两轮)**:
> - **第一轮**(S12-9 处置):① S12-9 退回单独立项;② 读侧解码取**甲案** ——
>   新增 `IPayloadDecoder`(住 `Sim.Contracts` / 实现住 `Sim.Codec`);③ 方法形状取**抛形** `Decode<T>`。
> - **第二轮**(正文逐项确认,转 Accepted 同批):**§Decision ①②③④⑤ 五项全部确认** ——
>   ② 抛形(与 `Decode<T>` / `Encode<T>` 对称)· ③ **池在实现构造函数**(逐字承 ADR-029 §② 模式)·
>   ④ **接口不含 freehand 路径**(依 ADR-006 G-3,不对称是设计)·
>   ⑤ **b5 门补两项**(`IPayloadEncoder` + `IPayloadDecoder`,结清 ADR-029 漏项)。
>
> ⚠️ **起草期两处自查修正(2026-10-15,转 Accepted 前)**:① 初稿把 payload struct 数写作
> **34**(照抄 ADR-029 当年的数字),实测现为 **35**(`grep -c "name: SimEvent.Kind\."` = 35,
> 与 `Encode` 重载数一致;ADR-030 追加 `DiseaseOnset` 后 +1)⇒ 已改;② 初稿漏了
> 模板必需的 `### Architecture` 节 ⇒ 已补(程序集归属图 + 运行时数据流图)。
>
> ⚠️ **本件的实现面全未落** —— §Validation V-1…V-10 **全部未勾,禁借绿**。
> Accepted 表示**方向已裁**,不表示任何一条判据已通过。

## Date

2026-10-15

## Last Verified

2026-10-15(勘察实测 + **实现轮落盘** —— 契约支 / 接线支 / S12-4 证伪轮三探针全红;证据原件 `impl-adr031-payload-decoder-2026-10-15.md`)

## Decision Makers

dr_guyang(用户 · **2026-10-15 两项裁定**)· technical-director(起草)
· 53 医疗后果与责任(S12-9 退回的提出方)· 37 病例系统 / 9 疾病与伤情模拟 / 6 世界与生态区(同型绕行方)

## Summary

**本 ADR 新增 ADR-005 的第八个 P0 抽象点 `IPayloadDecoder`,补 ADR-029 的读侧对称缺口。**

ADR-029(2026-10-02)立了**写侧**抽象点 `IPayloadEncoder`,其**存在理由逐字**是:

> 「`Sim` 的写者要产出 `PayloadRef`,却够不着编码面 —— `PayloadCodec` 住 `Sim.Codec`,
> 不可变 blob 池住 7a/45。实测后果:全库每一个写者都在手搓 `PayloadRef`,
> 且零字节真的进池。**本接口是 `Sim` 侧唯一合法的编码路径**。」

**读侧有完全同构的问题,却从未被裁决**:`PayloadCodec.TryGetPayload<T>` 同样住 `Sim.Codec`,
`Sim` 同样够不着。实测已出现**同型绕行**,且**无一处是「解决」**:

| # | 位置 | 绕行形态 |
|---|---|---|
| ① | `Sim/CaseCloseDecider.cs:177-181` | 取**流序关联**(「其前最近一条 `CaseOpened`」)代替解码 `CaseClosedPayload.CaseId`;自陈「物理上够不着 … **解码归调用方**」 |
| ② | `Sim/Consequences/ConsequenceInputAggregator.cs:11-13` | **只操作 `SimEvent` 头部**,从不读 `Payload` 内容;自陈「P / C 的**内容** … 归 story-002」 |
| ③ | `Sim/PatternDetector.cs:182,197` | 以**调用方委托** `diseaseExtractor` 代替解码 `CaseClosedPayload.DiseaseSet` |

> ⚠️ **2026-10-15 实现轮就地订正(原稿只列三处)** —— 全库实测 **11 处**绕行/注入面
> (`grep -rln "够不着" unity/Assets/Sim/` 命中 10 文件 + `PatternDetector` 的委托注入;
> 另有 `StructureKinds.cs` / `EmergencyAttemptAggregator.cs` 两处**仅注释引用**、非绕行)。
> 原稿的「三次」是**抽样**,不是普查。逐处裁定见 §V-8(承 §Validation)。
> **其余 8 处**:`CaseCloseWriter`(disease_set 调用方喂入)· `CaseOpenWriter`(disease_snapshot 调用方喂入)·
> `CaseOpenDecider`(流序关联)· `PatternRecognitionWriter`(双解码委托透传)·
> `PoiStateMachine.RebuildFromDecoded`(已解码序列喂入)· `ResourceHarvestWriter`(gather_seq 调用方求值)·
> `DiseaseCourse` / `DiseaseCourseBook`(状态在 Sim、解码在 Boot 组合根)。

**S12-9 的勘察证明:53 是第四个撞上这堵墙的,而它绕不开** ——
`Judge` 的语义**就建立在载荷内容上**(GDD `medical-consequences.md:263` 真值源登记:
「`P` = 判断记录(**病名 / 置信度**)· `C` = **处置适切性**」)。
②的绕行把问题**原样**交给了 story-002,而 story-002 面对的是同一堵墙。

**本 ADR 的裁决**:新增 `IPayloadDecoder`(住 `Sim.Contracts`,实现 `PayloadDecoder` 住 `Sim.Codec`),
方法 `T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct`。
**池依赖在实现的构造函数,不在接口签名** —— 逐字承 ADR-029 §② 的模式
⇒ **`IBlobPool` 一字不改、不搬家,`Sim.Contracts → Sim.Codec` 零新增边**,
门 A / `Sim` 引用集白名单(ADR-025 §①)/ b2 门三者**全部保留**。

## Engine Compatibility

**不引入任何 post-cutoff 引擎 API。** 本件是纯 C# 接口契约与程序集边界裁决
(与 ADR-029 同型)。`Sim.Contracts` 的 `noEngineReferences: true` 不变。

**Engine Knowledge Risk**: **LOW**。

## ADR Dependencies

| 方向 | ADR | 说明 |
|---|---|---|
| Depends On | **ADR-005** | 抽象点集合的出处;本件追加第八个,并订正其计数 |
| Depends On | **ADR-029** | **写侧对称件** —— 本件逐字复用其「池依赖在实现构造函数」的模式;本件补其**读侧** |
| Depends On | **ADR-006 Amendment G-2** | 载荷 header/blob 形态(读形);本件补其**读访问面** |
| Depends On | **ADR-006 Amendment G-3** | `freehand_text` 变长段 —— **本件刻意不承载**(见 §Decision ④) |
| Depends On | **ADR-025 §①** | `Sim` 引用集恰 = {BCL, `Sim.Contracts`};本件**不改**该边 |
| Depends On | **ADR-017 §二** | 门 A 白名单(b2 门) |
| Relates To | **ADR-024** | Kind 单一登记真源 —— 解码分派集须与 `entities.yaml` 双向差集归零 |

## Context

### Problem Statement

**`Sim` 的读方要产出判定,却够不着载荷内容。**

`Sim` 的引用集**恰 = {BCL, `Sim.Contracts`}**(`unity/Assets/Sim/Sim.asmdef:4`
的 `"references": [ "Sim.Contracts" ]` + `"noEngineReferences": true`;ADR-025 §① / b2 门强制),
而 `PayloadCodec` 住 `Sim.Codec` ⇒ **`Sim` 物理上够不着任何载荷内容** ——
`PayloadRef` 只有 `BlobId / Offset / Length` 三个整数字段,**不含字节**。

**这与 ADR-029 的写侧问题逐字同构**,而 ADR-029 **只裁决了写侧**。

### Current State(实测 2026-10-15)

```bash
# ① 读侧抽象点不存在
$ grep -rn "interface IPayloadDecoder\|interface IPayloadReader\|interface IPayloadCodec" \
    --include=*.cs unity/Assets | wc -l
0

# ② 写侧抽象点存在(对照组)
$ ls unity/Assets/Sim.Contracts/IPayloadEncoder.cs
unity/Assets/Sim.Contracts/IPayloadEncoder.cs

# ③ 既有读访问签名(全部住 Sim.Codec ⇒ Sim 够不着)
$ grep -n "public static" unity/Assets/Sim.Codec/PayloadCodec.cs
41:  public static T Decode<T>(EventKind kind, ReadOnlySpan<byte> payloadBytes) where T : struct
125: public static bool TryGetPayload<T>(in SimEvent e, IBlobPool pool, out T payload)
140: public static bool TryGetFreehandText(in SimEvent e, IBlobPool pool, out string text)
```

### Constraints

- **`Sim` 引用集不得变** —— 门 A(`noEngineReferences`)/ b2 门 / ADR-025 §① 三重强制。
  ⇒ 任何解法**不得**引入 `Sim → Sim.Codec` 的引用边。
- **`IBlobPool` 住 `Sim.Codec`**(`IBlobPool.cs:25`)—— 与 `IBlobSink` 同处。
  ⇒ 接口签名**不得**出现 `IBlobPool`(否则 `Sim.Contracts` 须引用 `Sim.Codec`,造出非法边)。
- **G-3 禁 sim 读自由文本** —— `TryGetFreehandText` 的 docstring 逐字:
  「**sim 消费面调用本方法即违例纪律**」。⇒ 解码器接口**不得**含 freehand 路径。
- **ADR-006 G-2 不可变性** —— 池在给定 `BlobId` 后字节内容不得再改(违反 ⇒ 回放分叉)。

### Requirements

- **R-1** `Sim` 侧须有**唯一合法**的载荷读取路径(对称于 ADR-029 的「唯一合法编码路径」)。
- **R-2** 该路径**不得**改动 `Sim` / `Sim.Contracts` 的引用集。
- **R-3** 该路径须能表达 `Decode<T>` 的既有分派集(与 `EncodeBoxed` 双向对称)。
- **R-4** 该路径须**结构性**排除 freehand 读取(G-3),而非靠纪律。
- **R-5** 既有绕行须**可退掉**(实测 11 处;本 ADR 不强制退,但须提供退路 —— 见 §Validation V-8)。

## Decision

### ① `IPayloadDecoder` = 第八个 P0 抽象点(住 `Sim.Contracts`)

**用户裁定(2026-10-15)。** 与 `IPayloadEncoder` **严格对称** ——
同住 `Sim.Contracts`、同收 `EventKind` 形参、同用 `where T : struct`。

```csharp
// Sim.Contracts —— 第八抽象点
// 权威来源:本 ADR §① · ADR-029 §①(写侧对称件)· ADR-006 G-2(载荷读形)· ADR-005(抽象点集合)
namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 载荷解码器 —— 从 <see cref="PayloadRef"/> 寻址并解码为强类型载荷。
    /// 实现者 = <c>Sim.Codec</c> 的 <c>PayloadDecoder</c>(它看得见 <c>PayloadCodec</c>);
    /// 消费者 = <c>Sim</c> 的全部判定方(9 / 37 / 53 / 6 …)。
    /// </summary>
    /// <remarks>
    /// <para><b>为何收 <see cref="PayloadRef"/> 而非 <c>SimEvent</c></b>:既有
    /// <c>PayloadCodec.TryGetPayload&lt;T&gt;</c> 收整个 <c>in SimEvent</c>,而它实际只用
    /// <c>e.Kind</c> + <c>e.Payload</c> 两个字段 ⇒ 接口面承担了不必要的耦合。
    /// 本接口收**最小充分集**。</para>
    /// <para><b>池依赖在实现构造函数,不在本签名</b> —— 逐字承 ADR-029 §② 的模式
    /// (<c>PayloadEncoder(IBlobSink)</c> 同理):<c>IBlobPool</c> 住 <c>Sim.Codec</c>,
    /// 若出现在本签名则 <c>Sim.Contracts</c> 须引用 <c>Sim.Codec</c> ⇒ 造出非法边。</para>
    /// <para><b>刻意不含 freehand 路径</b>(ADR-006 G-3):写侧有两个 Judgment 具名重载,
    /// 读侧**没有** —— 该不对称是设计。G-3 禁 sim 读自由文本 ⇒ 接口面不含 = 编译期不可表达。</para>
    /// </remarks>
    public interface IPayloadDecoder
    {
        /// <summary>解码为强类型载荷。</summary>
        /// <typeparam name="T">per-Kind 载荷 struct(须与 <paramref name="kind"/> 匹配)。</typeparam>
        /// <param name="kind">事件种类 —— 决定 codec 分派与 schema 校验。</param>
        /// <param name="payloadRef">载荷寻址引用(blob 池内偏移段)。</param>
        /// <exception cref="InvalidOperationException">池未驻留该 blob / 引用越界。</exception>
        /// <exception cref="InvalidDataException">字节在但违反该 Kind 的 schema(损坏)。</exception>
        T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct;
    }
}
```

### ② 形状 = **抛形**,与 `Decode<T>` 对称、与 `TryGetPayload` 刻意分家

**用户裁定(2026-10-15)。** 既有绕行形状**互不一致**(原稿举三处为代表;全量实测 11 处):

| 形状 | 签名 | 失败语义 |
|---|---|---|
| `PayloadCodec.Decode<T>` | `T Decode<T>(EventKind, ReadOnlySpan<byte>)` | **抛** |
| `PayloadCodec.TryGetPayload<T>` | `bool TryGetPayload<T>(in SimEvent, IBlobPool, out T)` | **宽容**(false + default) |
| `IPayloadEncoder.Encode<T>` | `PayloadRef Encode<T>(EventKind, in T)` | **抛** |

**裁决取抛形**,理由:

1. **与字节级 `Decode<T>` 对称** —— 本接口就是「`Decode<T>` 加上池寻址」,失败语义应一致。
2. **与 `Encode<T>` 对称** —— 写抛、读也抛,一对抽象点形状一致。
3. **「未驻留」与「损坏」必须分两层**(核心):既有 `TryGetPayload` 把两者混进同一个 `false`
   —— 池未命中返回 `false`,而**字节违反 schema 时它抛**(`PayloadCodec.cs:126-128` 的 docstring:
   「池未命中 / 引用越界 ⇒ false + default(**宽容**);字节内容违反 schema ⇒ **抛**
   (损坏不是「查无此数据」)」)。⇒ **既有 `TryGetPayload` 本就不是纯 bool 形**,
   它的 bool 只承载「查无」,损坏走异常。
   本接口把「查无」也**显式化**为异常(`InvalidOperationException`),使两种失败在类型上可分。

> ⚠️ **未驻留 blob 是否该宽容降级,归调用方** —— 本接口只保证「要么给你正确的值,
> 要么告诉你为什么给不了」,不替调用方决定降级策略。
> (既有 `TryGetPayload` 的宽容语义**保留不动** —— 它服务的是呈现侧等其它消费者,本件不改它。)

### ③ 池依赖在**实现的构造函数**,不在接口签名

**用户裁定(2026-10-15 第二轮确认)** —— 依 ADR-029 §② 先例。

ADR-029 §② **刻意**把 `IBlobSink` 放在 `Sim.Codec` 而非 `Sim.Contracts`,理由逐字:

> 「`IBlobSink` 刻意住 **Sim.Codec 而非 Sim.Contracts** —— `Sim` 的写者**不该**直接摸池,
> 它只调 `IPayloadEncoder`。池是 codec 与流实现之间的事;暴露给 `Sim` 会开第二条旁路。」

**它怎么让 `Sim` 用上池?答案:池在实现的构造函数,接口签名里零池引用** ——
`PayloadEncoder(IBlobSink blobSink)`(`PayloadEncoder.cs:37`)。

⇒ **读侧照搬**:`PayloadDecoder(IBlobPool blobPool)`。

**结构后果**:

- `IBlobPool` **一字不改、不搬家**(仍住 `Sim.Codec`)。
- **零新增程序集边** —— `Sim.Contracts` 只多一个接口类型,不引用任何新装配。
- `Sim` 侧的消费者经**构造注入**拿到 `IPayloadDecoder` 实例(与它拿 `IPayloadEncoder` 同款)。
- **不开第二条旁路** —— `Sim` 仍摸不到池(池只在 `Sim.Codec` 的实现里)。

### ④ 刻意**不含** freehand 路径(不对称是设计)

**用户裁定(2026-10-15 第二轮确认)** —— 依 ADR-006 G-3。

写侧 `IPayloadEncoder` 有**两个** Judgment 具名重载(`EncodeJudgmentRecorded` /
`EncodeJudgmentRevised`),因为它们多收 `freehandText`。读侧**不得**有对应物:

- `PayloadCodec.TryGetFreehandText` 的 docstring 逐字:「**sim 消费面调用本方法即违例纪律**」。
- G-3 禁 sim **读**自由文本;接口面不含 ⇒ **编译期即不可表达**,不靠纪律守。

⇒ **本接口只有一个方法**。`Sim` 侧即便拿到 `IPayloadDecoder`,也**无法**表达 freehand 读取。

### ⑤ b5 门补 `IPayloadEncoder` + `IPayloadDecoder`(结清 ADR-029 漏项)

**本件勘察新发现**(§Context 未列,此处登记)。

`unity/Assets/Editor.Tools.Gates/AssemblyGates.cs:381-385` 的 b5 门禁用类型数组:

```csharp
public static readonly string[] AudioForbiddenContractTypes =
{
    "IEventSink", "IEventAuthority", "ITickProvider", "IIdAuthority",
    "IVitalsQuery", "ITeleportCommandSink", "SimEvent", "VitalsDto",
};
```

**八项 = 六个抽象点 + `SimEvent` + `VitalsDto` —— 不含 ADR-029 的 `IPayloadEncoder`。**
⇒ 实测后果:**44 今天可以合法引用 `IPayloadEncoder`**。
(错误串(`AssemblyGates.cs:729` —— ⚠️ 原记 `:720` 有误,见 §Migration Plan A 组注)也仍写「六抽象点/SimEvent/VitalsDto」。)

**裁决**:本批把数组补至**十项**(+ `IPayloadEncoder` + `IPayloadDecoder`),错误串同步订正为
「八抽象点/SimEvent/VitalsDto」。**这是 ADR-029 的漏项,非本件新裁决** ——
本件只是补上,并把它记在 §Migration Plan。

> ⚠️ **归属**:补 `IPayloadEncoder` 一项严格属 ADR-029 面(它 2026-10-02 就该补);
> 补 `IPayloadDecoder` 一项属本件。二者同批落盘,但**出处不同**,不合并记账。

### Architecture

**程序集归属(本件的核心结构断言 —— 与 ADR-029 逐字同构)**:

```
┌──────────────────────────────────────────────────────────────────┐
│  Sim.Contracts          (L3 BOUNDARY · references = [] · 零引擎)   │
│    ├─ WorldPos / SimEvent / Fix / …(既有)                        │
│    ├─ IPayloadEncoder   ← ADR-029 §①(第七抽象点)                 │
│    └─ IPayloadDecoder   ← 本件 §①(第八抽象点)          ★新增     │
│         · 方法签名零池引用(池不进契约面 —— §③)                │
└──────────────────────────────────────────────────────────────────┘
              ▲ 引用(唯一允许方向)        ▲
              │                            │
┌─────────────┴────────────┐   ┌───────────┴──────────────────────┐
│  Sim                     │   │  Sim.Codec                       │
│    references =          │   │    references = [Sim.Contracts]  │
│      ["Sim.Contracts"]   │   │    ├─ IBlobPool / IBlobSink(既有)│
│    ★本件不改此边★        │   │    ├─ PayloadCodec(既有)         │
│                          │   │    ├─ PayloadEncoder(既有)       │
│    Sim 侧判定方(9/37/53)│   │    └─ PayloadDecoder   ★新增     │
│    经构造注入拿           │   │         ctor(IBlobPool)  ← §③     │
│      IPayloadDecoder      │   │         · 池在这里,不在接口面   │
└──────────────────────────┘   └──────────────────────────────────┘
```

**三条结构性事实**(可复核,见 §Validation V-1…V-3):

1. **`Sim.Contracts` 的 `references` 仍为 `[]`** —— 新增的是**接口类型**,不是引用。
2. **`Sim` 的 `references` 仍为 `["Sim.Contracts"]`** —— 一字不改。
3. **`IBlobPool` 仍住 `Sim.Codec`** —— 不搬、不改;它只出现在 `PayloadDecoder` 的**构造函数**,
   **不出现**在 `IPayloadDecoder` 的方法签名里(§③)。

⇒ **零新增程序集边,零 `Sim → Sim.Codec` 边** ⇒ 门 A / b2 / ADR-025 §① 三重约束全部保留。

**运行时数据流**(以 53 为例):

```
SimEvent(头部) ──┬─ Kind ──────────────┐
                 └─ Payload ─→ PayloadRef ─→ IPayloadDecoder.Decode<T>(kind, ref)
                                                    │
                                            实现内部:_blobPool.TryGetBlob(BlobId)
                                                    │
                                            PayloadCodec.Decode<T>(kind, span)
                                                    │
                                             强类型载荷 struct
```

### Key Interfaces

```csharp
// ── 第八抽象点(§①)──
public interface IPayloadDecoder
{
    T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct;
}

// ── 实现(住 Sim.Codec;§③ 池在构造函数)──
public sealed class PayloadDecoder : IPayloadDecoder
{
    private readonly IBlobPool _blobPool;          // ← 池住 Sim.Codec,不进接口签名
    public PayloadDecoder(IBlobPool blobPool) { _blobPool = blobPool ?? throw new ArgumentNullException(nameof(blobPool)); }

    public T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct
    {
        // ① 池寻址(未驻留 / 越界 ⇒ InvalidOperationException)
        // ② 分派:镜像既有 PayloadCodec.DecodeBoxed(不改 35 个 struct、不写 kindgen)
        // ③ schema 校验失败 ⇒ 由 PayloadCodec.Decode<T> 抛(损坏)
    }
}
```

### Implementation Guidelines

1. **分派镜像既有 `DecodeBoxed`** —— 与 ADR-029 §① 对 `EncodeBoxed` 的处置**逐字同款**:
   接受瞬时装箱(与既有解码路径同款),**不改 35 个 struct、不写 kindgen**。
2. **`Decode<T>` 的实现体 = 池寻址 + 委托 `PayloadCodec.Decode<T>`** ——
   本件**不重写**任何 codec 逻辑,只补「够得着」这一层。
3. **freehand 路径不存在**(§④)—— 接口面只有一个方法。
4. **`Sim` 侧消费者经构造注入** —— 与 `IPayloadEncoder` 同款(见 `PatternRecognitionWriter.cs:78` 先例)。

## Alternatives Considered

### Alternative 1: 放开 `Sim` → `Sim.Codec` 引用边

**Rejection Reason**:直接击穿 ADR-025 §① 与门 A 的结构性排除。
门 A(`noEngineReferences`)的意义正是「sim 不见引擎、不见 codec」;
开此边 ⇒ b2 门红、ADR-025 §① 失效 ⇒ **须先推翻三份 Accepted ADR**。
与 ADR-029 Alt 1 被否的**同一条理由**(ADR-029:253)。

### Alternative 2: 装配层解码 + DTO 入 `Sim`(乙案)

**形态**:`Gameplay.Boot` 侧解码后把内容以 DTO 喂进 `Sim`
(先例:`Gameplay.Boot/DiseaseVitalsService.cs:205-223` 解码后调 `Courses.ApplyOnset/ApplyTreatment`)。

**Rejection Reason**(**用户 2026-10-15 裁定否决**):
`Judge` 是**纯函数**,喂 DTO 会把「解码 + 求值」**拆成两段** ⇒
须**重新对齐 AC-53-02**「同一事件流在 Mono 与 IL2CPP 跑 Judge ⇒ Outcome 逐位一致」的口径
(分段后「同一事件流」的边界变得依赖装配顺序,而装配顺序不在三源不变量内 ——
承 ADR-016 §一:任何第四来源都是**静默不可重建通道**)。
且乙案**每多一个消费者就要多写一份装配层解码**(53 / 37 / 6 各一份)⇒ 正是 ADR-029
要消灭的「每个写者手搓 `PayloadRef`」的**读侧翻版**。

### Alternative 3: 把 `IBlobPool` 搬进 `Sim.Contracts`

**Rejection Reason**:ADR-029 §② 已裁过同型 ——
「池是 codec 与流实现之间的事;**暴露给 `Sim` 会开第二条旁路**」。
搬运 ⇒ `Sim` 可绕开解码器直接摸池 ⇒ R-1「唯一合法路径」失效。
且搬运牵动 `IBlobPool` / `IBlobSink` / `InMemoryBlobPool` 三个既有类型的归属,
涟漪远大于本件收益。

### Alternative 4: 把既有 `TryGetPayload` 提升为接口

**Rejection Reason**:两处不合 ——
① 它收整个 `in SimEvent`,而实际只用两字段(接口面不必要地宽);
② 它的 `bool` 把「未驻留」与「损坏」混在一层(尽管损坏实际走抛),语义不纯。
**且它是既有生产路径的签名,改它 = 改所有既有调用点** ⇒ 范围外。

### Alternative 5: 维持现状(各判定方自寻绕行)

**Rejection Reason**:实测**已有三次绕行,且无一处是解决**;S12-9 的勘察证明
**第四次绕不开**(`Judge` 的语义建立在载荷内容上)。
继续绕 ⇒ 每个新判定方都要**重新发明一次**流序关联 / 委托注入 / 头部-only 投影,
而这些绕行**各自引入新的等价性前提**(如 `CaseCloseDecider.cs:183-185` 自陈的
「本口径在**合法流**上与载荷解码等价」—— 等价性本身成了每个绕行点各自的举证负担)。

## Consequences

### Positive

- **`Sim` 侧有了唯一合法的载荷读取路径**,与 ADR-029 的写侧**成对**。
- **零引用集变更** —— 门 A / b2 / ADR-025 §① 三重约束全部保留。
- **既有绕行有了退路**(§Migration Plan;实测 **11 处**,非原稿的三处),可逐处退掉,不必一次改完。
- **G-3 的 freehand 禁令升为编译期**(§④)。
- **结清 ADR-029 的 b5 门漏项**(§⑤)。

### Negative

- **新增第八个抽象点** ⇒ 计数订正涟漪(§Migration Plan;AD R-029 的「六 → 七」先例
  实测牵动**数十处**,且当年**未扫干净** —— 本件须一次列全)。
- **`Sim` 的消费者多一个构造依赖**(`IPayloadDecoder`)—— 与 `IPayloadEncoder` 同款,
  装配层负担对称增加,非新问题。

### Neutral

- **既有 `PayloadCodec.TryGetPayload` / `TryGetFreehandText` 全部保留不动** ——
  本件**不删不改**任何既有读访问签名;它们服务呈现侧等其它消费者。
- **绕行不强制退**(实测 11 处)—— 退与不退归各系统自己的实现轮;
  本件只保证「**退路存在**」。`CaseCloseDecider` 的流序关联在合法流上与解码等价,
  退与不退**不影响正确性**,只影响「等价性前提由谁举证」。
- **`Sim.Contracts` 的引用集不变** ⇒ 本件不改任何 `.asmdef`。

## Risks

| Risk | 概率 | 影响 | 缓解 |
|---|---|---|---|
| 计数订正涟漪**又一次扫不干净**(重演 ADR-029) | **高** | 中 | §Migration Plan 给出**全清单 + 分类**(现行态断言 → 改 / 历史陈述 → 加注),承 `control-manifest.md:40-46` 的二分先例 |
| 解码器成为**新的旁路**(消费者直接摸池) | 低 | 高 | §③ 池不进接口签名 + `IBlobPool` 仍住 `Sim.Codec` ⇒ 结构性挡住 |
| freehand 经**其它路径**泄进 `Sim` | 低 | 中 | 本件只封本接口面;`TryGetFreehandText` 的纪律面**仍靠 docstring + 评审**,登记为残余 |
| 绕行**长期不退** ⇒ 两套读法并存(实测 11 处) | 中 | 低 | 各自系统实现轮处置;本件在 §Validation V-8 逐处点名裁定,使其**可追踪** |

## Performance Implications

**可忽略。** 事件率上界 = tick 频率(20 Hz,ADR-009 §六 / `TECHNICAL_PREFERENCES`)。
每次解码一条 alloc(瞬时装箱,与既有 `Decode<T>` 同款;ADR-029 §① 修正① 已明载「接受瞬时装箱」)。
**本件不引入新的热路径** —— 它只是把既有的 `PayloadCodec` 调用**搬到一个够得着的地方**。

## Migration Plan

### 本 ADR(仅裁决)

**本件只写裁决,不落实现** —— 承 ADR-029 的先例(「用户 2026-10-02 明令『本轮只写 ADR-029』」)。

### 实现轮(须另立 story,建议拆两支)

1. **契约支**:`Sim.Contracts` 加 `IPayloadDecoder`;`Sim.Codec` 加 `PayloadDecoder`
   (**镜像既有 `DecodeBoxed`,不改 35 个 struct、不写 kindgen**)。
2. **接线支**:b5 门数组补两项(§⑤);既有绕行**逐处评估退与不退** ——
   **原稿写三处,实测 11 处**(§Summary 就地订正;逐处裁定见 §Validation V-8 登记表)——
   **不强制退**,但须逐处**登记**决定与理由。**本件只保证退路存在,不执行接线**(归各归属系统实现轮)。

### ADR-005 计数订正(全清单 —— **本清单不声称已复核**)

> ⚠️ **与 ADR-029 的关键差异**:ADR-029 只登记了 ADR-005 的**五处**,其余留作独立批次;
> 实测**该批次未扫干净** —— 全库仍散落「五个 / 六个」的陈旧计数(见下表 B 组)。
> **本件一次列全**,并按 `control-manifest.md:40-46` 的二分处置。

**A 组:现行态断言(「七个」→「八个」)—— 须改**

| 位置 | 现状 |
|---|---|
| `adr-005-deterministic-sim.md:25` | 「P0 必须预留的**七个**抽象点」 |
| `adr-005-deterministic-sim.md:31` | 注:「…ADR-029 再追加第七个 ⇒ 现行**七个**」 |
| `adr-005-deterministic-sim.md:478` | 「`WorldPos` + **七个** P0 抽象点」 |
| `adr-005-deterministic-sim.md:489` | 「**七个**抽象点的语义」 |
| `control-manifest.md:106` | 「**七个** P0 抽象点必须预留:`ITickProvider` / … / `IPayloadEncoder`」 |
| `architecture.md:258` / `:439` / `:561` / `:816` / `:1222` | 「**七个** P0 抽象点」(五处) |
| `adr-025-contract-assembly-manifest.md:63` / `:112` | 「**七个** P0 抽象点」(两处;`:112` 是类型清单行) |
| `adr-020-player-controller-and-camera.md:39` | 「`WorldPos` + **七个** P0 抽象点」 |
| `adr-015-world-geometry-fixed-world-lattice.md:341` | 「`WorldPos` 与**七个** P0 抽象点同住于此」 |
| `adr-023-scene-lifecycle-rendering.md:78` | 「`ITickProvider` 是**七个** P0 抽象点之一」 |
| `design/gdd/player-controller-and-movement.md:125` / `:386` / `:1087` / `:1692` | 「**七个** P0 抽象点」(四处) |
| `design/gdd/enemy-ai.md:976` | 「**七个** P0 抽象点」 |
| `production/u0-assembly-checklist.md:320` | 「`WorldPos` · **七抽象点** · …」 |
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs:729` | 错误串「**六抽象点**/SimEvent/VitalsDto」**(⚠️ 2026-10-15 实现轮订正:原记 `:720`,实为普通 doc 注释;真错误串在 `:729`)** |
| `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisGrowthExit.cs:7` | 「**六个** P0 抽象点」 |

**B 组:更老的「五个 / 六个」残留(ADR-029 批次未扫)** —— 同批订正或加注

| 位置 | 现状 |
|---|---|
| `adr-006-fixed-point-boundary-contract.md:75` / `:524` | 「**五个**抽象点」(两处;`:524` 明写「本 ADR 起草时」= 可加注) |
| `adr-001-networking-pipe-abstraction.md:78` / `:321` / `:488` | 「**五个**抽象点」(三处) |
| `adr-007-event-authority-and-roll-state.md:30` / `:84` / `:111` / `:293` / `:298` | 「**五个**抽象点」(五处;**但「第六个」本身仍准确**) |
| `.claude/docs/technical-preferences.md:166` | 「P0 必须预留**五个**抽象点」 |
| `design/gdd/diagnosis-system.md:880` | 「`IIdAuthority`(ADR-005 **五个**抽象点之一)」 |
| `design/gdd/disease-simulation.md:1345` | 复查轮注(已标「原节名『五个抽象点』」= 历史) |
| `production/epics/item-database/EPIC.md:17` · `skill-system/EPIC.md:18` | 「**五个** P0 抽象点」 |
| `production/epics/disease-simulation/EPIC.md:53` | 「**五抽象点** + SimEvent 形状」 |
| `production/epics/random-events/EPIC.md:14` / `:29` · `story-002:18` | 「**第六个** P0 抽象点」(**仍准确**,不改) |
| `unity/Assets/Sim.Contracts/IPresenceQuery.cs:4` | 「**五抽象点**之一」 |

**C 组:历史陈述** —— 按 `adr-006` 先例**加注不改写**

`architecture-review-2026-09-15.md` · `architecture-review-2026-09-20.md` ·
`design/gdd/reviews/*` · `traceability-index.md:1366` · `systems-index.md:720` ·
`sprint-02.md:84` · `sprint-04.md` —— **保留原值 + 加「ADR-031 后为八个」注**。

### ADR-010 §三 义务追加

拟追加**义务 16**:读侧解码实现(`IPayloadDecoder` / `PayloadDecoder`)—— 归属同 `IBlobPool`
的既有口径(实现住 `Sim.Codec`,零流实现侧义务)。**本件只登记落点,不落表。**

## Validation Criteria

> **✅ 2026-10-15 实现轮全部勾选** —— 证据见
> `production/qa/evidence/impl-adr031-payload-decoder-2026-10-15.md`
> (契约支 + 接线支 + S12-4 证伪轮三探针)。以下逐条为**实测**结论,非自述。

- [x] **V-1** `Sim.Contracts` 的 `IPayloadDecoder` 引用集 ∈ {BCL, 本装配类型} —— **零 `Sim.Codec` 类型**。
  ✅ 实测:`using` 仅 `System`;`IBlobPool` / `PayloadCodec` 字样**仅在注释**。
- [x] **V-2** `Sim` 引用集实测仍 = `["Sim.Contracts"]`(**本件不改 `Sim.asmdef`**)。
  ✅ 实测 `Sim.asmdef` `references == ["Sim.Contracts"]`。
- [x] **V-3** `Sim.Contracts.asmdef` 的 `references` 仍为 `[]`(**本件不改**)。✅ 实测。
- [x] **V-4** b5 门数组含 `IPayloadEncoder` + `IPayloadDecoder`(十项),且门仍绿。
  ✅ 实测 10 项;探针 1(删 `IPayloadDecoder`)⇒ 该测试**转红**(证其有鉴别力)。
- [x] **V-5** `IPayloadDecoder.Decode<T>` 与 `IPayloadEncoder.Encode<T>` 的约束与分派形态对称
  (`where T : struct`;`DecodeBoxed` ↔ `EncodeBoxed`)。
  ✅ 实测:`Encode<T>(EventKind, in T) where T : struct` ↔ `Decode<T>(EventKind, in PayloadRef) where T : struct`;
  实现委托既有 `PayloadCodec.Decode<T>`,不新增分派表。
- [x] **V-6** `Decode<T>` 的分支集与 `entities.yaml` 的 35 支 `payload_schema` 双向差集归零。
  ✅ 实测:registry 35 支 vs codec 35 `case`,双向差集皆空(往返测试覆盖 **35/35**)。
- [x] **V-7** `IPayloadDecoder` **不含** freehand 路径(反射断言:接口方法数 = 1)。
  ✅ 实测:接口泛型方法 = `[Decode]`;探针 2(接口加 `TryGetFreehandText`)⇒ 该测试**转红**。
- [x] **V-8** 既有绕行**逐处登记**了「退 / 不退 + 理由」(不要求全退,要求全登记)。
  ✅ **原稿写「三处」,实测 11 处**(§Summary 已就地订正)。逐处裁定见下 §V-8 登记表。
- [x] **V-9** b2 门仍绿(`Sim` 引用集违例零)。✅ 实测 `AssemblyGateRunAllTest.test_assemblyGates_runAll_zeroErrors_b3_b2_b6_b7` 过。
- [x] **V-10** ADR-005 计数订正 A 组全部落盘;B 组按「改 / 加注」二分处置;C 组加注。
  ✅ A 组 **25 处**改毕 · B 组 **3 处**改 + 4 处加注 · C 组历史件加注 6 处 + 现行件 9 处。
  ⚠️ **原清单有错**:`AssemblyGates.cs:720` 被记为「错误串」实为普通 doc 注释(真错误串在 `:729`,已改);
  且清单漏列 `production/session-logs/`(自动审计日志,不属订正面)。此即「**本清单不声称已复核**」之兑现。

### V-8 逐处裁定(实测 11 处 · 2026-10-15)

| # | 位置 | 绕行机制 | 裁定 | 理由 |
|---|---|---|---|---|
| ① | `Sim/CaseCloseDecider.cs` | 流序关联(前最近 `CaseOpened`) | **退** | 载荷 `CaseClosedPayload.CaseId` 是**权威关联字段**;流序关联引入「合法流才等价」前提(自陈 `:183-185`),`Decode` 可消。**归 37 接线轮**。 |
| ② | `Sim/Consequences/ConsequenceInputAggregator.cs` | 只操作事件头 | **退** | 本件正是为 53 而设;story-002 `Judge` 的 P/C 入向须读载荷内容 ⇒ 必须退。**归 S12-9 重开**。 |
| ③ | `Sim/PatternDetector.cs` | `diseaseExtractor` 委托注入 | **退** | `CaseClosedPayload.DiseaseSet` 可直接 `Decode` ⇒ 注入面消失。**归 37 接线轮**。 |
| ④ | `Sim/CaseOpenWriter.cs` | `disease_snapshot` 调用方喂入 | **暂不退** | 写侧入参,非读侧绕行;调用方持有 9 的病种状态,写者收已解码字段是**正当边界**(与 §③ 池在实现构造函数同型)。**登记,不强制退**。 |
| ⑤ | `Sim/CaseCloseWriter.cs` | `disease_set` 调用方喂入 | **暂不退** | 同 ④。 |
| ⑥ | `Sim/CaseOpenDecider.cs` | 流序关联(`JudgmentRecordedPayload.CaseId`) | **退** | 同 ①。**归 37 接线轮**。 |
| ⑦ | `Sim/PatternRecognitionWriter.cs` | 双解码委托透传 | **退** | 与 ③ 同源;`Decode` 后委托面消失。**归 37 接线轮**。 |
| ⑧ | `Sim/World/PoiStateMachine.cs` | `RebuildFromDecoded` 已解码序列喂入 | **暂不退** | 重建路径;调用方(7a/45)本就持 blob 池。`Decode` **可**在此使用,但重建是**边界层**职责(ADR-009 派生态)。**登记,可 P1a 优化**。 |
| ⑨ | `Sim/World/ResourceHarvestWriter.cs` | `gather_seq` 调用方求值 | **退** | `gather_seq` 计源 = 本 Kind 节点计数 ⇒ 需解码 `ResourceHarvestedPayload.NodeId`。**归 17 装配轮**。 |
| ⑩ | `Sim/DiseaseSimulation/DiseaseCourse.cs` | 状态在 Sim、解码在 Boot | **暂不退** | 组合根分工(ADR-025 §①);解码发生在同时可见 Sim 与 Codec 的装配。`Decode` 不改变此分工。 |
| ⑪ | `Sim/DiseaseSimulation/DiseaseCourseBook.cs` | 同 ⑩ | **暂不退** | 同 ⑩。 |

**裁定口径**:「退」= 该处**应**改用 `IPayloadDecoder`,但**本件不接线**(归各系统实现轮);
「暂不退」= 该处的绕行是**正当边界分工**,`Decode` 不消除它。**本件只保证退路存在**(§Consequences)。
**⚠️ 无一处在本轮接线** —— 本件是契约支 + 门面支,接线支的**执行**归各归属系统。

**✅ 2026-10-15 同日接线轮(用户裁定「6 处全做,含改 Inputs53」)**:①③④⑥⑦⑨ 六处「退」
**全部执行完毕**;② 亦执行(其「归 S12-9 重开」由用户裁定提前 —— `Inputs53` 的 P/C 两路改
解码行 `JudgmentRow53` / `CareRow53`,字段名与数量不变,AC-53-05 五路闭集反射断言原样绿)。
⑤⑧⑩⑪ 维持「暂不退」。回归 `3421/3375/0/46` 零回归(证据原件 §九:
`production/qa/evidence/impl-adr031-payload-decoder-2026-10-15.md`)。

⚠️ **本表于 2026-10-15 实现轮勾选完毕**;证伪轮(改→跑→还原)三探针全红,见证据原件。

## GDD Requirements Addressed

| TR-ID | 说明 | 本件的作用 |
|---|---|---|
| `TR-medcons-002` | 结算 = 三源纯函数可重建 | **前置** —— 无载荷读取路径则 `Judge` 的 P/C 入向不可得 |
| `TR-medcons-007` | 零可变态零订阅 31(partial) | 前置(同因) |
| `TR-case-*`(37) | 病例流投影 | 退掉 `CaseCloseDecider` 的流序关联绕行(可选) |
| `TR-disease-*`(9) | 病程投影 | 退掉 `PatternDetector` 的委托注入绕行(可选) |

> ⚠️ **本件不直接兑现任何 TR** —— 它是**前置基础设施**。
> 兑现关系(如 `TR-medcons-002` 的 covered)**须待 story-002 实现后方能声明**。

## Related

- `docs/architecture/adr-029-payload-encoder-abstraction.md` —— **写侧对称件**(本件逐字复用其 §② 模式)
- `docs/architecture/adr-005-deterministic-sim.md` —— 抽象点集合(本件追加第八个)
- `docs/architecture/adr-006-fixed-point-boundary-contract.md` —— Amendment G-2(载荷读形)/ G-3(freehand 禁令)
- `docs/architecture/adr-025-contract-assembly-manifest.md` —— §① `Sim` 引用集
- `docs/architecture/adr-017-dots-decision.md` —— §二 门 A(b2 门)
- `docs/architecture/adr-024-kind-single-source.md` —— Kind 单一登记真源(分派集对拍)
- `docs/architecture/control-manifest.md` —— §2026-10-02 传播范围(计数订正的二分先例)
- `production/qa/evidence/recon-s12-9-medcons-outcome-judge-2026-10-15.md` —— 本件的**提出件**(S12-9 勘察)
- `production/epics/medical-consequences/story-002-deterministic-outcome-judge.md` —— 触发件
