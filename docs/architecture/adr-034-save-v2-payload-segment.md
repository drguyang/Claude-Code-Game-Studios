# ADR-034: 存档 v2 布局 —— 载荷段(blob 池持久化)

## Status

Accepted

> **2026-10-28 起草并转 Accepted。** 用户指令「**按建议走 ADR-034**」授权的是**总方向**
> ——「v2 存档布局带载荷段」。**§Decision ①–⑦ 的细项由本轮就地裁定,未经用户逐条确认**;
> 若任何一条不可接受,**在实现轮之前改本 ADR 即可** —— 但**不得**实现落地后再改(改已落地的
> 布局 = 再加一次版本号,正是本件要消灭的形态)。
>
> 本 ADR **修订 ADR-010 §一 的存档体布局**(v1 → v2)。ADR-010 的其余条款
> (§二 校验和 / §三 义务表 / §四 / §六 时机 / §七 迁移协议)**不变**。

## Date

2026-10-28

## Last Verified

2026-10-28

## Decision Makers

用户(总方向)· 实现侧勘察(本件起草)

## Summary

v1 存档**从不写载荷字节** —— `SimEvent` 头部只带 `PayloadRef` 三元组(`BlobId/Offset/Length`),
而 blob 池是**进程内存**(`InMemoryBlobPool`),进程一重启就是空的 ⇒ **跨进程读档今日不可用**
(`TD-persist-09`)。本 ADR 把存档升到 **v2**:**在折叠段之后追加一个「引用驱动的载荷段」**,
只写**入档事件实际引用到**的 blob(已折叠病人的载荷因此自动不写),读档时**先装池再解事件**。
**v1 存档不迁移** —— 载荷字节从未写出,信息论上补不出来。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.24f1) |
| **Domain** | Core(存档持久化 / 确定性) |
| **Knowledge Risk** | **LOW** —— 纯 BCL 字节布局与 C# 接口;不触任何 post-cutoff API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`;ADR-006 Amendment G-2(blob 池)/ ADR-010(存档)/ ADR-025(程序集清单)/ ADR-029(`IBlobSink`)/ ADR-031(`IPayloadDecoder`) |
| **Post-Cutoff APIs** | None |
| **Verification Required** | None(实现面 = `CodecWriter` / `CodecReader` / SHA-256,全为 BCL) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-006** Amendment G-2(载荷字节住不可变池;`PayloadRef` 寻址)/ **ADR-010**(存档格式总纲)/ **ADR-025** §①(程序集清单)/ **ADR-029** §②(`IBlobSink` 与池的归属)/ **ADR-031**(`IPayloadDecoder` —— 读侧存在的理由) |
| **Enables** | **ADR-033 §④之补**(结构 id 平移的「固化」需要载荷段作载体);`WorldLoadService` / `TelemetryReporter` / `CaseCloseDecider` 的跨进程可用性 |
| **Blocks** | 无 story。但 **`TD-persist-09` 与 `ADR-033` 的实现**都等这件 |
| **Ordering Note** | ⚠️ **本件是实现裁定,不含实现**。实现随下一轮(`TD-persist-09` 字节半),与 `ADR-033` 的三件同批落 —— 两侧单独落任一都不成立(见 ADR-033 §Risks R1)。 |

## Context

### Problem Statement

`SimEvent` 的载荷是**间接**的:`SimEventCodec` 只编解头部五字段
(`TagPayload` → `PayloadRef(BlobId, Offset, Length)`,`SimEventCodec.cs:24-41`),
**字节本体住在池里**(ADR-006 Amendment G-2)。而 v1 存档的组版器只写

```
[头部 | 事件段(count 前缀 + 逐事件) | 折叠段(count 前缀 + 定长行)]
```

(`SaveComposer.cs:33`;`SaveService.ParseBody:743-761`)—— **没有任何一段承载 blob 字节**。

于是:写档时 `PayloadRef` 被原样落盘,读档时池是空的。
**生产装配根的池 = `InMemoryBlobPool`**(`CompositionRoot.cs:132`),进程内存 ⇒ 重启即空。
**写面(`IBlobSink`)的唯一实现就是它**,而它**自陈是「测试与接线期载体,不是生产池」**
(`InMemoryBlobPool.cs:8-11`:「无容量上限、**无落盘**、**无跨进程重映射**」)⇒ **零落盘实现**。
⇒ **跨进程读档必撞「查无此 BlobId」。**

> ⚠️ **起草期自查(2026-10-28)**:本节初稿写的是「`IBlobSink` **零**生产实现」,并附命令
> `grep -rn ': IBlobSink' … | grep -v '/Tests/'` 声称「只命中 `InMemoryBlobPool` 自身」——
> **两句话互相矛盾,且命令跑不出那个结论**:`InMemoryBlobPool.cs:29` 的声明是
> `class InMemoryBlobPool : IBlobPool, IBlobSink`(逗号在前),**`": IBlobSink"` 这个模式根本匹配不到它**;
> 该命令的 4 行命中全在 `Tests/`(其中 2 行还是注释)。正确口径已按上段改写:
> **有写面实现,但是内存载体、无落盘**。复算用
> `grep -rn "IBlobSink" unity/Assets --include=*.cs | grep -v '/Tests/'`(不带冒号)。
> 📌 **同一条命令也在 `TD-persist-09` 的「实测」列里**(`tech-debt-register.md:4835` ③),
> 其文字却写「只有 `InMemoryBlobPool`(测试载体)+ 两个测试内 `MemoryBlobSink`」——
> **结论对、命令对不上**。按「原文不删、就地加注」,该格已在本轮**就地加注**(改复算命令、
> 保留结论),正文未重写。

### Current State(逐条可复跑)

| # | 事实 | 命令 / 落点 |
|---|---|---|
| ① | 存档**无载荷段** | `SaveComposer.cs:111-120` 只拼 `header + events + foldedSegment` |
| ② | 生产池是**进程内存** | `grep -n "InMemoryBlobPool" unity/Assets/Gameplay.Boot/CompositionRoot.cs` ⇒ `:132` |
| ③ | `IBlobSink` **零落盘实现** | `grep -rn "IBlobSink" unity/Assets --include=*.cs \| grep -v '/Tests/'` ⇒ 命中 `InMemoryBlobPool`(`:29` 声明 / `:20` 头注)/ `PayloadEncoder`(`:34,38`)/ `CompositionRoot`(`:141,196`)/ 接口自身。**唯一实现 = `InMemoryBlobPool`,其 `:9` 自陈「无落盘」** |
| ④ | 行为已被钉住(断言的是**失败**) | `test_ac9g_realSaveBytes_roundTripRebuildsWorld` 末段:用空池重跑同一份存档 ⇒ 断言抛 `InvalidOperationException`,报文含 `blob` / `BlobId` |
| ⑤ | **折叠段是「剩下的全给它」** | `SaveService.cs:760` `foldedSegment = r.ReadBytes(r.Remaining)` ⇒ **其后挂不了任何段** |

⑤ 是 v2 的**结构性前置**:不改它,载荷段无处可放。

### Constraints

| # | 约束 |
|---|---|
| **C1** | `PayloadRef` 三元组**不得改**(ADR-006 G-2;改它 = 所有既有事件字节作废 + ADR-012 金标准全重签) |
| **C2** | 载荷 blob **不可变**(ADR-006 G-2 / `IBlobSink` 头注)—— 给定 `BlobId` 后内容不得再改 |
| **C3** | 校验和覆盖 `checksum` 字段之后的**全部**字节(ADR-010 §四)⇒ 新段**自动**被覆盖,不需要额外登记 |
| **C4** | `SaveMigrationChain.TailBody` 按**末尾等长**取段(`SaveMigrationChain.cs:174-184`)⇒ **任何「追加一段」的迁移都会让断言③(事件正文逐位不变)失败** |
| **C5** | `Sim` 引用集恰 = `{BCL, Sim.Contracts}`(ADR-025 §①)`⇒ 池接口不得下沉到 `Sim`(否则开第二条旁路,ADR-029 §② 同一理由) |
| **C6** | 折叠段 = `int32 count + count × FoldRowCodec.RowSize(36)`(`FoldRowCodec.cs:54,94-104`)—— **它本来就是自描述的**,只是今天的读者没用这一点 |

### Requirements

| # | 要求 |
|---|---|
| **R1** | 读档后**所有入档事件的 `PayloadRef` 可解析**(跨进程) |
| **R2** | 载荷段**不写**已折叠病人的载荷(折叠病人的病史行已从事件段抹除,其载荷是垃圾) |
| **R3** | 同 `BlobId` **只写一次** |
| **R4** | 缺 blob ⇒ **fail-closed 抛**,不得写出一份不可读的存档 |
| **R5** | 门 A / b2 / b5 三条程序集门**保持不变** |

## Decision

### ① v2 布局:`头部 | 事件段 | 折叠段 | 载荷段`

`SaveHeader.CurrentSaveVersion` —— `SaveHeader.cs:23` —— 由 `1` 升到 **`2`**。
新增段**固定在末尾**;`SnapshotOffset` 仍写 `0`(哨兵 = 无快照段,ADR-010 §一)。

> ⚠️ **与 ADR-010 §一 ④ 的表面冲突,须显式调和**:该处(`adr-010:182-184`)的原文是
> 「**`SaveVersion` 仍为 1** —— v1 布局从未出货(零生产消费者、零存档文件),布局定稿属
> 1.0 定稿,**bump 会为「只存在于测试夹具的旧布局」造升链,纯仪式零收益**」。
> **本件不推翻它,而是它的适用前提变了**:
> - 那条裁定针对 **布局未变、只是定稿**;本件**真的改布局**(追加一段)⇒ v1/v2 字节**确实不同**,
>   不 bump 则 v1 档会被 v2 读者当「折叠段之后还有载荷段」解析(正是 Alt 4 的静默错读);
> - 它担心的**代价本身**(「造升链」)在本件**不发生** —— §⑥ 明确**不注册任何 `ISaveMigration`**。
>
> ⇒ 两条并存成立:ADR-010 §一 ④ 在「布局不变」时仍有效;§Decision ① 在「布局已变」时接管。
> 本件在 ADR-010 的 §一 就地不加注 —— 按「原文不删、就地加注」纪律,该注记随实现轮
> (与 §⑦ 同批)回填,本件先在此登记该债务。

### ② 载荷段 = **引用驱动** dump

```text
载荷段 := int32 count
        + count × { int32 blobId, int32 length, byte[length] }
   · 集合 = 入档事件段里出现过的 **distinct** PayloadRef.BlobId
   · 序   = blobId 升序(canonical —— 集合顺序不得依赖遍历序,否则两台上位机写出的
            同态存档字节不同,ADR-012 的字节级黄金夹具无从固定)
   · 编码 = **长度前缀**,不是位置打包。承 ADR-010 §一 `:186` 的同一裁定:
            该处已说明「`FoldRow` 六字段恒在、无一可选、次序本就是 §二 的位置语义 ⇒
            定长行位置编码是确定性等价选择」;而 blob 字节**长度可变**,位置打包
            根本不成立 ⇒ 必须走 `length` 前缀
```

> ⚠️ **起草期自查(2026-10-28)**:初稿把「禁位置打包」归给 **ADR-006 §五** —— **引错件**。
> 实测 ADR-006 §五 是「Unity 序列化边界:`Fix` 不可经内置序列化器」
> (`adr-006-fixed-point-boundary-contract.md:197`);「禁位置打包」及它与定长行的关系
> 在 **ADR-010 §一**:186`。已按实际出处改写。

**为什么是「引用驱动」而不是「整池 dump」**:`SaveComposer` 在写事件段前**已经**把折叠病人的
病史行抹掉了(`SaveComposer.cs:104-109`)。⇒ 那些病人的载荷**没有任何存活引用**,
引用驱动天然不写它们。整池 dump 会把它们一并写出,且**永不回收**
(blob 不可变、无释放接口)⇒ 存档体积随折叠次数单调上涨。

> 🔁 **与债册原话的差异(须明写,不得含糊)**:`TD-persist-09` 的「修法方向 ①」把代价写成
> 「存档体积随载荷线性涨(**载荷不可变 ⇒ 无法去重已折叠病人的载荷**)」
> (`tech-debt-register.md:4838`)。**那句话描述的是「整池 dump」**(本件 Alt 2),
> **不是 §②**:引用驱动只写存活引用 ⇒ 折叠病人的载荷**天然被排除**。
> ⇒ 本件**收窄**了债册预判的代价,不是接受它。
> ⚠️ 但「不写进存档」≠「被回收」—— 那份字节仍在进程内存直到退出;故 **R1 仍成立**,
> 只是上界从「全池」降到「存活引用」。**不得**把本段的「收窄」读成「R1 已消」。

### ③ 折叠段改为**自定长**读取(v2 读侧的必备前提)

今天 `ParseBody` 把「事件段之后的**全部剩余字节**」交给折叠段(`SaveService.cs:760`)。
v2 必须改成:按 `int32 count + count × 36` 读出**恰好**长度
(`FoldRowCodec.RowSize = 36`),余下的字节才是载荷段。

⚠️ **这不是新发明** —— 折叠段**本来**就是自描述的(`FoldRowCodec.EncodeSegment:94-104`),
今天不用只是因为「它是最后一段」。ADR-010 §一 增补 已经立过同一原则
(「折叠段自描述 ⇒ 无需头部偏移字段」),本条只是把它**用起来**。

### ④ 读档次序:**先装池,再解事件**

```text
Load: 头部 → 事件段 → 折叠段 → 载荷段 → **装池** → _replaySink(events) / _foldedRowSink(rows)
```

⚠️ 池必须**先于**两个 sink 就位。今天两个 sink 的顺序是硬约束
(`SaveService.cs:715-716` 的注释:恢复闭包依赖「先事件、后折叠」)—— v2 **不改这个相对次序**,
只是把「装池」插到它**前面**。

### ⑤ 新增 `IBlobRestoreSink` —— 按号装载

`IBlobSink.Store(ReadOnlySpan<byte>) → int` **只发号**,无法按**指定** id 装入;
而读档必须保号(否则落盘的 `PayloadRef` 全部失效)。⇒ 新增:

```csharp
/// <summary>blob 池的「按号装载」契约 —— 读档路径专用(ADR-034 §⑤)。</summary>
public interface IBlobRestoreSink
{
    /// <summary>把一段字节装到调用方指定的 <paramref name="blobId"/> 上。</summary>
    /// <remarks>实现须复制入参;同号重复装载 ⇒ 抛(池不可变,C2)。</remarks>
    void Restore(int blobId, ReadOnlySpan<byte> blob);
}
```

**住 `Sim.Codec`**(与 `IBlobSink` 同侧)—— **不得**进 `Sim.Contracts`:那会把池的写面
暴露给 `Sim`,正是 ADR-029 §② 刻意挡住的那条旁路(C5)。

### ⑥ **v1 存档不迁移** —— 显式拒绝,不是「脚本还没写」

`SaveVersion < 2` ⇒ **一律拒绝**,报文须点明「**v1 从未写出载荷字节,信息不存在,迁移在结构上
不可能**」。**不注册任何 v1→v2 的 `ISaveMigration`。**

两条独立理由,任一成立即够:

- **ⓐ 信息论**:v1 的事件段只有 `PayloadRef` 三元组,**字节从未落盘**。
  没有任何脚本能凭空造出它们 —— 这不是「实现未做」,是「**从未观测**」。
- **ⓑ 结构上被禁**:`SaveMigrationChain.TailBody` 按**末尾等长**取段比对新旧正文
  (`SaveMigrationChain.cs:174-184`)。「在尾部追加一段」会让新档的尾段从载荷段取
  ⇒ 断言③(事件正文逐位不变)必然失败(C4)。

**前提(实查)**:今日盘上**零真实存档**(`find ~/.config/unity3d -name 'save*.bin' -o -name '*.dyjq'`
⇒ 零命中,ADR-033 §Context ①a 同一命令)⇒ v1 不可读**无实际损失**。

> ⚠️ **该前提有保质期**:一旦 P0 首次对外发放存档,v1 不可读即**不可逆**。
> ⇒ 登记为**发布前门**:对外分发前必须已落 v2(见 §Risks R3)。

### ⑦ 本件不实现

`SaveComposer` / `SaveService.ParseBody` / `IBlobRestoreSink` / `InMemoryBlobPool` 的改动
与 `ADR-033` 的三件**同批**落(下一轮)。本件只裁布局与接口。

### Architecture

```text
写档(SaveComposer,主线程)
  finalEvents ──→ 收集 distinct PayloadRef.BlobId ──→ 升序
                    └─ IBlobPool.TryGetBlob(id) ──false──→ **抛**(缺 blob,R4)
  拼接: 头部 | 事件段 | 折叠段 | 载荷段
  → SaveService worker(后台线程)AddChecksum(覆盖到载荷段末字节,C3)→ 原子写

读档(SaveService.Load)
  头部 → 事件段 → 折叠段(自定长,§③)→ 载荷段
       → IBlobRestoreSink.Restore(id, bytes) 逐条装池
       → _replaySink(events) / _foldedRowSink(rows)      ← 次序不变(§④)
```

### Implementation Guidelines

1. **`SaveComposer.Compose` 需新增 `IBlobPool` 入参** —— 它今日**够不着池**
   (`SaveComposer.cs:82-91` 的入参表里没有池)。装配点 = `CompositionRoot`(已有池,`:132`)。
2. **收集引用的时机**:**拼接后**的 `finalEvents`(即 `SpliceBack` 的结果),不是 `allEvents`
   —— 折叠抹除的行不得贡献引用(R2)。
3. **缺 blob = 抛,不得跳过**。跳过 = 写出一份「今天能存、明天读不出」的存档,
   而失败点在**读档**、离现场很远(`TD-persist-09` 的教训正是这种延迟爆炸)。
4. **`SnapshotOffset` 仍写 0**;载荷段自描述 ⇒ **不加头部偏移字段**
   (与 ADR-010 §一 增补「折叠段自描述 ⇒ 无需偏移」同一原则)。
5. **体积上界**:载荷段 ≤ Σ(存活事件引用到的 distinct blob 字节)。每 blob 只写一次(R3)。
   ⚠️ **若 `IBlobSink` 实现每次 `Store` 都发新 id,同一内容会存成多份** —— 去重归池实现
   (ADR-029 §② 已声明「去重是实现细节,非契约」),本件不强制。
6. **`InMemoryBlobPool` 须实现 `IBlobRestoreSink`**(它是测试/装配载体);
   生产落盘池归 7a 实现轮。
7. **v1 拒绝的报文**:`SaveService.Load` 今天的 `<` 分支走迁移链,空链 ⇒ 抛
   「迁移链断裂:版本 1 没有对应迁移脚本」(`SaveMigrationChain.cs:101-105`)。**该报文会误导**
   (读起来像「漏写脚本」)。v2 须在进链**之前**加一个显式版本门,报文点明「v1 无载荷字节」。

### Key Interfaces

见 §Decision ⑤ 的 `IBlobRestoreSink`。**不新增**其他接口;`IBlobPool` / `IBlobSink` /
`PayloadRef` / `SimEventCodec` **一字不改**。

## Alternatives Considered

### Alternative 1:事件记录内联可变长载荷

把 `PayloadRef` 换成「长度前缀 + 字节」直接写进事件段。**否决**:① `PayloadRef` 是
ADR-006 G-2 的核心形状,改它 = 所有既有事件字节作废 + ADR-012 金标准全矩阵重签(C1);
② **同一 blob 被多事件引用时会被重复写**(`PayloadRef` 的共享语义消失),体积反而更差;
③ 事件段从定长头变成变长记录,`SimEventCodec` 的 tag 化严格解码要整体重做。

### Alternative 2:整池 dump

把池里**全部** blob 写出。**否决**:折叠病人的载荷无任何存活引用,却是池里的大头;
blob 不可变 + 无释放接口 ⇒ 该垃圾**永不回收**,存档体积随折叠次数单调上涨
—— `TD-persist-09` 点名的正是这一点(R2 未满足)。

### Alternative 3:旁挂 `<save>.blobs` 文件

主档不动,载荷写到同名旁挂文件。**否决**:① 破坏**单文件原子写**与**校验和耦合**(C3)——
主档校验和通过而旁挂缺失时,存档看起来完好、读起来崩;② `CheckpointFallback` 的
`.tmp/.bak` 原子替换语义要**重做一遍**(两个文件没有共同的原子边界);
③ 「丢一个文件 = 半状态」是新引入的失效模式。

### Alternative 4:不升版本,把载荷段塞在折叠段之后

**否决**:v1 读者把「事件段之后的全部剩余字节」当折叠段(`SaveService.cs:760`)⇒
`FoldRowCodec.DecodeSegment` 会因 count 不符而抛。**症状是「存档损坏」,不是「版本不符」**
—— 把一次**有意的格式演进**伪装成**数据损坏**,是可用性与可诊断性的双重损失。

### Alternative 5:读档后从别处重建池

**否决**:「别处」今天不存在。ADR-009 §四 的派生态源只有两类(种子纯函数 / 版本化烘焙数据),
载荷**都不是**;载荷是**事件内容**,唯一真源就是事件流自己(`TD-persist-09` 的 ③ 已判不可行)。

### Alternative 6:写一条 v1→v2 迁移脚本

**否决**(双理由,见 §Decision ⑥):信息不存在(ⓐ)+ 迁移链的末尾锚定禁追加(ⓑ)。
⚠️ 特别提示:即便将来 AC-7a-14 的口径被放宽成「允许追加」,**ⓐ 仍然成立** ——
放宽断言救不了一个从未被观测到的量。

## Consequences

### Positive

- **跨进程读档恢复可用**:`WorldLoadService` / `TelemetryReporter` / `CaseCloseDecider`
  三条 `IPayloadDecoder` 路径从「必然抛」变成「可用」。
- **`ADR-033` 的固化获得载体** —— 结构 id 平移的结果终于能持久化。
- 折叠垃圾不进存档(R2),体积 ∝ **存活**载荷。

### Negative

- **存档体积 ∝ 存活载荷字节**(今日无实测数据,登记为 §Risks R1)。
- **v1 存档永久不可读**。今日零损失;一旦对外发放即**不可逆**(§Risks R3)。

### Neutral

- `Sim` 引用集一字不变(C5);门 A / b2 / b5 三条门**不需要改**。
- 不新增 `SimEvent.Kind`、不动 `entities.yaml`、不动 `tr-registry`。
- `SnapshotOffset` 语义不变(恒 0 哨兵)。

## Risks

| # | 风险 | 缓解 |
|---|---|---|
| **R1** | 存档体积增长无实测基线 | 实现轮**实测**一份真实会话的载荷占比并登记;若超出可接受范围,再开 ADR 讨论(去重 / 压缩 / 分册) |
| **R2** | 缺 blob 被静默跳过 ⇒ 写出不可读存档 | §Implementation Guidelines 3:**fail-closed 抛** |
| **R3** | **v1 不可读在对外发放后不可逆** | **发布前门**:对外分发前必须已落 v2。⚠️ 与 `TD-persist-09` 的触发条件 ① 同一时刻 —— 该触发条件一旦成立,本条即从「零损失」变成「用户存档全废」 |
| **R4** | `IBlobRestoreSink` 被下沉到 `Sim.Contracts` ⇒ 开第二条旁路 | 住 `Sim.Codec`(C5);b2 门的引用集白名单在编译期挡住 |
| **R5** | 池实现「每次 Store 发新 id」⇒ 同内容多份 | 去重归池实现(ADR-029 §② 已声明非契约);本件不强制,但**登记** |

## Performance Implications

- **写档**:新增一次 `distinct + sort`(O(n log n),n = 入档事件数)+ 一次 blob 拷贝。
  `AddChecksum` 的 SHA-256 覆盖范围变大 ⇒ 后台线程耗时随之增长(仍在 worker,不占主线程)。
- **读档**:新增一次线性装载。
- **稳态**:**无逐帧成本** —— 两条路径都只在存档边界发生。

## Migration Plan

**无迁移脚本**(§Decision ⑥)。
实现随下一轮:① `IBlobRestoreSink` · ② `SaveComposer` 载荷段 · ③ `SaveService` 自定长折叠段
+ 装池 + 版本门 · ④ `InMemoryBlobPool.Restore` · ⑤ 把 `test_ac9g…` 末段的**失败**断言翻成**成功**。

## Validation Criteria

| # | 判据 | 状态 |
|---|---|---|
| **V-1** | **往返(主判据)**:写档 → 新进程语义(空池)读档 → 装池 → `WorldLoadService.Rebuild()` **成功**,结构与 id 逐条一致。**这条今天是反的**:`test_ac9g…` 末段断言**抛** | **未跑** |
| **V-2** | **引用驱动**:构造已折叠病人的载荷,断言载荷段**不含**其 BlobId | **未跑** |
| **V-3** | **折叠段自定长**:折叠段之后仍有字节时**不**判为损坏 | **未跑** |
| **V-4** | **v1 拒绝**:喂一份 v1 字节 ⇒ 报文含 `v1` 与「载荷字节」,**不是**「没有对应迁移脚本」 | **未跑** |
| **V-5** | **缺 blob fail-closed**:池里删掉一个被引用的 blob ⇒ 写档时抛(不是读档时) | **未跑** |
| **V-6** | **同 BlobId 只写一次**:两条事件引用同一 blob ⇒ 载荷段 count = 1 | **未跑** |
| **V-7** | 全量回归无回退 | **未跑** |

> ⚠️ **V-1…V-7 全部未跑,禁借绿。** `Accepted` 表示**方向已裁**,不表示任何判据已通过。
> ⚠️ **V-1 是唯一能证明「跨进程读档可用」的判据** —— 它必须**翻掉**一条既有断言,
> 因此本件的实现轮**必然**是一次「既有测试由红转绿」的改动,不是纯增量。

## GDD Requirements Addressed

| TR / AC | 关系 |
|---|---|
| **ADR-010 §一** | **修订**存档体布局(v1 → v2)。ADR-010 其余条款不变 |
| **ADR-010 §三 义务 15** | 兑现其「blob 池写面实现」的**载体**侧(`IBlobRestoreSink` + 载荷段);落盘池本体仍归 7a |
| **`AC-7a-14`** | **不修改**。追加一段**不改动**事件段的任何字节 ⇒ 不与「已发生事件原样保留」冲突(与 ADR-033 的情形**不同**:那里确实改写了载荷) |
| **ADR-033 §④之补** | 提供「固化」的持久化载体 |

## Related

- **债册**:`TD-persist-09`(本件结清其**裁定**半边;字节半随实现轮)· `ADR-033` 的实现链
- **ADR**:`adr-006`(G-2 池·PayRef)· `adr-009`(§四 派生态)· `adr-010`(存档总纲,本件修订其 §一)·
  `adr-025`(§① 程序集清单)· `adr-029`(§② `IBlobSink` 归属)· `adr-031`(`IPayloadDecoder`)
- **实现件**:`SaveComposer.cs` · `SaveService.cs` · `SaveHeader.cs` · `SaveHeaderCodec.cs` ·
  `FoldRowCodec.cs` · `SimEventCodec.cs` · `IBlobPool.cs` · `IBlobSink.cs` · `InMemoryBlobPool.cs` ·
  `SaveMigrationChain.cs` · `CompositionRoot.cs` · `WorldLoadService.cs`
- **夹具**:`test_ac9g_realSaveBytes_roundTripRebuildsWorld`(V-1 须翻)
