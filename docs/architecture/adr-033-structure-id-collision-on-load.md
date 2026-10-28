# ADR-033: 基线诞生前存档的结构 id 撞号处置 (Structure Id Collision on Load)

## Status

Accepted

> **2026-10-28 起草并转 Accepted。** 用户裁定(2026-10-28)**取方案 A ——「两边都留」**:
> 基线恒占 `1..N`;**存档里的结构 id 在载入期确定性平移 `+N`** 到基线号段之外;
> 二者永不互相覆盖。**不采用**「基线赢(fail-closed 拒收)」,**也不采用**「存档赢(覆盖)」。
>
> 本 ADR 结清 `TD-building-02` 的**语义未裁**半边(该债的「解除动作」逐字要求「须裁(ADR 级),
> 不是实现动作」)。**实现半边不随本件落地** —— 见 §Decision ⑦ 与 §Risks R1:
> 实现须与 v2 存档布局(`TD-persist-09`)同批,否则「固化」无载体。

## Date

2026-10-28

## Last Verified

2026-10-28

## Decision Makers

用户裁定(方案 A)· API/引擎侧勘察(本件起草)

## Summary

`StructureInstanceRegistry.Restore` 对同号 `structure_id` 是**覆盖**语义,因此「基线诞生之前写下的存档」
(其玩家放置从 `id = 1` 起号)在今天重放时会**静默顶替关卡基线的初始家具**。本 ADR 裁定
**两边都留**:以「存在 `Placed` 且其 `id ∈ [1, N]`」为**充要判据**,命中时把该存档**全部**结构 id
整体平移 `K := N`(基线件数)到基线号段之外,基线号段与新号段互不相交;
平移结果**必须固化进存档**才幂等。**不得**实现为 `ISaveMigration`(迁移链逐字节禁改正文)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.24f1) |
| **Domain** | Core(确定性模拟 / 事件重放) |
| **Knowledge Risk** | **LOW** —— 纯整数与纯 C# 接口契约,**不触任何 post-cutoff API** |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`;`ADR-005`(定点域)/`ADR-009`(事件化边界)/`ADR-025`(程序集清单) |
| **Post-Cutoff APIs** | None |
| **Verification Required** | None(实现面为纯 BCL 整数运算;`long` 加法的溢出须 `checked` 显式判,见 §Decision ③) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(唯一真源 / 主机唯一 `Step`)、**ADR-006** Amendment B(机制 A「计数器 + 高水位可重构」)、**ADR-009** §七(三段式)与 §四(派生态边界)、**ADR-010**(存档布局)、**ADR-025** §①(`Sim` 引用集恰 = `{BCL, Sim.Contracts}`) |
| **Enables** | 无(本件不新开抽象点、不新开 `Kind`) |
| **Blocks** | 无。`TD-building-02` 的 `P0 面 = N`(今日不可达)**不因本件改变** |
| **Ordering Note** | ⚠️ **实现须与 `TD-persist-09` 的 v2 存档布局同批落**。理由见 §Decision ④ / ④之补 与 §Risks R1:① 「固化」依赖「存档携带载荷字节」,而 v1 布局**不携带**(见 §Context 实查①);② 「固化」还要**写回事件流**,而载入路径今天**只读**(C4)。⇒ 今日实现「只做重映射不做固化」会精确复现 §Alternatives Alt 5 的**漂移 + 删错结构** |

## Context

### Problem Statement

GDD `modular-building.md` 规则三把开档占用定义为:

```
Overlay(0) := BakedInitial
Overlay(t) := BakedInitial ⊕ Structure* 事件序列
```

`BakedInitial` 与事件流**可能产生同号 `structure_id`** —— 此时 `⊕` 的语义**无定义**。
而实现侧对此形态的既有行为是**静默覆盖**:

- `StructureInstanceRegistry.Restore` 的落库语句是 `_instances[structureId] = …`(`StructureKinds.cs:228-229`);
- `StructureRestorer.Apply` 的 `Placed` 支又在 2026-10-28 的 B-1 修复里**先释放旧足迹**再落库
  (`StructureRestore.cs:210-245`)⇒ 覆盖**更彻底**:基线家具的实例**与其占用格一并消失**,
  且**没有任何信号**(既不抛,也不写事件)。

⇒ 后果是「关卡设计者在 `world_buildslots.json` 里摆的家具,在这一个存档上**凭空没了**」,
而 `Overlay` 模型的第三个真源(`BakedInitial`)在事后**不可观测地被删掉了一部分**。

### Current State

**① 今日零可达(实查,逐条可复跑)。** 三条独立理由,任一成立即足够:

| # | 判据 | 命令 |
|---|---|---|
| a | 盘上**无任何真实存档** | `find ~/.config/unity3d -name 'save*.bin' -o -name '*.dyjq'` ⇒ 零命中(只有 `prefs`) |
| b | **零个已注册迁移** | `SaveService.cs:162` 的 `migrations` 默认 `Array.Empty`;`grep -rn ': ISaveMigration' unity/Assets --include=*.cs \| grep -v '/Tests/'` ⇒ **零命中**(不加 `grep -v` 时命中 2 行,**全在 `Tests/`**:`save_migration_chain_test.cs:70` / `save_slot_contract_test.cs:539`) |
| c | **`CurrentSaveVersion` 自 `3491198d` 起从未变过** | `SaveHeader.cs:23` = `1` |

**② 同一会话内也不可达。** `CompositionRoot.cs:918` 应用基线、`:941` 构造写者,二者**共用同一个**
`structures` 注册表;基线 `Restore` 把 `_nextStructureId` 推高到 `N+1`(`StructureKinds.cs:233-235`),
`Register` 再取 `_nextStructureId++`(`:290`)⇒ **基线之后**玩家拿到的必然是 `N+1, N+2 …`。
⇒ **撞号只可能出现在「基线诞生(`d230eb36`,2026-10-28)之前写下的存档被今天的代码重放」时。**

**③ ⚠️ 一处流传的错误前提(就地订正)。** 该形态在多份件里被写成「**schema-1 存档**里玩家放置从 `1` 起号」。
**这是把两个不相干的量混成了一个词**:「schema 1」在本仓**专指 `world_buildslots.json` 的作者态 JSON 格式**
(旧 schema 1 行只有 `{cell, slot_type, baked_initial}`,无 `module_id`,见
`production/qa/evidence/fix-s20-20-baked-initial-baseline-2026-10-28.md` §一 ③ / §二),
而**存档格式**是 `SaveHeader.CurrentSaveVersion`。真实的类别是**时序**(基线引入前后),不是版本号。
本 ADR 一律用「**基线诞生前的存档**」表述。

### Constraints

| # | 约束 | 出处 |
|---|---|---|
| **C1** | 迁移链**逐字节**断言事件正文不变(尾部锚定 `FixedTimeEquals`)⇒ **本处置不可能做成 `ISaveMigration`** | `SaveMigrationChain.cs:127-133` |
| **C2** | 玩家对初始家具的**第一次**改动 = 第一条 `StructureRemoved`(基线对应行)⇒ `Removed` / `Modified` 引用基线号是**正常后基线行为**,不得当作撞号信号 | GDD `modular-building.md:150-151` |
| **C3** | 结构 id 的发号是**机制 A**(计数器 + 高水位可重构),全案**只有一个计数器**(注册表)⇒ 本处置不得引入第二个发号器 | ADR-006 Amendment B;`StructureKinds.cs:233-235`;`StructureWriter.PeekNextId` 的 2026-10-28 订正 |
| **C4** | 重放入口是**只读消费者**(零 `IEventSink.Append`) | `WorldLoadService.cs` 头注 |
| **C5** | 「**不迁移结构位置** —— 迁移 = 改写玩家资产,不做」 | GDD `EC-23-05` |
| **C6** | `Sim` 引用集**恰 = `{BCL, Sim.Contracts}`**,看不见 `Sim.Codec` ⇒ 任何重映射逻辑必须是**纯整数、零 codec 依赖** | ADR-025 §①(b2 门) |
| **C7** | 载荷里 `structure_id` 是 `long`,且 `Restore` 拒收 `≤ 0` | `WorldPayloads.cs:112`;`StructureKinds.cs:217-221` |

> **C5 与本件不冲突**:C5 约束的是**位置**(玩家可见的几何 / 资产),本件只动 **`structure_id`**
> (内部身份)。两者的差别在玩家侧不可感知,但正是这个差别让本件**不**触发 C5 的禁令。

### Requirements

| # | 要求 |
|---|---|
| **R1** | **两边都留** —— 基线家具与存档结构**都不消失**(用户裁定) |
| **R2** | **确定性** —— 同一份存档字节 ⇒ 同一次载入 ⇒ 同一个世界(ADR-005) |
| **R3** | **幂等** —— 固化之后的存档,再次载入时判据**不再触发**(否则 id 跨会话漂移) |
| **R4** | **不引入第二发号器**(C3) |
| **R5** | **可证伪** —— 判据的**必要性**与**充分性**各有一条能转红的夹具 |

## Decision

### ① 裁定:两边都留 —— 以**平移**代替「谁赢」

**基线恒占 `1..N`**(`N = World.BakedInitialStructureCount`)。命中撞号形态时,**存档里的结构 id 整体平移**
到基线号段之外(`K := N`)。基线**不被覆盖**,存档结构**不被丢弃**。

### ② 触发判据(唯一):存在 `Placed` 且其 `StructureId ∈ [1, N]`

```text
触发  ⟺  ∃ op ∈ ops : op.Kind = Placed ∧ 1 ≤ op.StructureId ≤ N
```

**这是充要判据,不是启发式。** 必要性来自 C2(后基线存档的 `Removed`/`Modified` 合法引用基线号);
充分性来自 §Context ②:**后基线存档的 `Placed` 必取 `≥ N+1`**(发号器由基线推高),
所以 `Placed` 的 id ≤ N **当且仅当**该 `Placed` 写在基线诞生之前。

⚠️ **必须只扫 `Placed`**。若把判据放宽成「任一结构操作的 id ∈ `[1,N]`」,那么
**每一份正常的后基线存档**(玩家拆过任何一件初始家具)都会被误判为前基线存档并被整体平移 ——
玩家建造的 id 会无故跳号。这一条是**可用性**级别的错误,不是风格问题。

### ③ 平移量 `K := N`(基线件数),映射 `id → id + N`

**全部**结构操作(`Placed` / `Removed` / `Modified`)引用到的 id 走**同一个**映射 ——
`Removed` / `Modified` 引用的号**必须跟着平移**,否则它们指向的就不是同一条结构了。

三条性质(逐条可验):

| 性质 | 论证 |
|---|---|
| **单射** | 整体平移是双射 ⇒ 不同的旧 id 必得不同的新 id |
| **与基线不交** | 新号 = 旧号 + `N` ≥ `1 + N > N` ⇒ 一律落在基线号段 `1..N` **之外** |
| **原 id 稳定** | `K` **只依赖基线件数 `N`**,不依赖存档内容 ⇒ 同一份 `Placed(1..M)` **每次载入都得 `N+1..N+M`**,与存档怎么增长无关 |

> **⚠️ 起草期自查二 —— 平移量(2026-10-28 · 我自己的第二处错误,原文不删)**;第一处见 §④。
> 初稿裁的是
> `K := max(N, maxId)`(maxId = 存档里引用到的最大 id),理由是「新号段要高于**所有**旧号」。
> **该理由不成立**:既然**全部** id 都平移,就不存在「没被平移的旧号」需要避开 —— 整段平移
> 天然单射、天然与基线不交,`maxId` 是**多余条件**。
>
> **而且它更差**:`K` 依赖存档内容 ⇒ 每次读档 `K` 都变 ⇒ **连原 id 都漂**
> (离线复算,`N=3`、初始 id `[1,2]`、每轮放置一件):

| 轮 | `K := N`(现裁) | `K := max(N, maxId)`(初稿,已否) |
|---|---|---|
| 0 | `1→4, 2→5` | `1→4, 2→5` |
| 1 | `1→4, 2→5, 6→9` | `1→7, 2→8, 6→12` |
| 2 | `1→4, 2→5, 6→9, 10→13` | `1→14, 2→15, 6→19, 13→26` |
| 3 | `1→4, 2→5, 6→9, 10→13, 14→17` | `1→28, 2→29, 6→33, 13→40, 27→54` |

> ⇒ 现裁下**基线诞生前那些结构的 id 恒定**(永远 `N+1..N+M`);初稿下**全部 id 逐轮暴涨**。
> 复算脚本 = `python3 -I` 跑 `sim_remap`(本 ADR 起草期一次性校验,未入库;判据是上表两列,可手工复算)。
>
> **溢出**:`id + N` 须 `checked` 判(载入期一次性)。`long` 溢出现实中不可达,但不设守卫会让
> 「不可达」变成假设而不是判据。

### ④ 固化义务 —— 幂等的前提,**不是可选项**

平移结果**必须写回事件流**(存档写的是事件流,**不是**重建出的世界)。固化后的存档其 `Placed` 的
id 全部 `> N` ⇒ 判据**必然不再触发** ⇒ 二次读档是**恒等变换**,不产生漂移。
⚠️ **时序**要求见 §④之补:必须**同一次载入内**完成,「下次自然固化」**不够**。

**为什么这是硬义务**:不固化的路径有一个**精确**的反例(见 Alt 5):**载入后才放置**的结构,
其 id **每轮读档再涨 `N`**,高水位**无界上涨**。⚠️ 该漂移是**延迟发作**的 —— 只在「旧事件未被重写 **且** 又追加了新放置」
的存档上出现;而在 v1 布局下 ids 根本不进存档(实查见下),所以今天**恰好**够不着。
**一旦 `TD-persist-09` 落地,这个窗口立刻打开** ⇒ 两者必须同批。

> **⚠️ 起草期自查一 —— 固化载体(2026-10-28 · 我自己的第一处错误,原文不删,留作记录)**;第二处见 §③。
> 选项呈报时我写了
> 「结果写回内存事件流,**下次存档自然固化**」。**这句话是错的。** 实查:`SaveHeader`
> (`SaveHeader.cs:20-33`)只有 `MagicValue / Checksum / SaveVersion / WorldSeed / ConfigVersion / Tick /
> SnapshotOffset` —— **无载荷段**;`structure_id` 住在 `StructurePlacedPayload` 里,
> 而载荷字节**根本不进存档**(`TD-persist-09`)。⇒ v1 存档**连 id 都不携带**,
> 「下次存档自然固化」没有载体。**「固化」的能力随 v2 布局一并产生,不在此之前。**

### ④之补 **混合存档不变量 —— 固化必须「同会话」,不是「下次自然」**

上面只说了漂移。**更严重的是正确性**,反例(`N = 3`,离线可复算):

```text
存档 S1(基线诞生前)= [Placed(1), Placed(2)]
载入1(未固化):世界 = 基线{1,2,3} ∪ 平移{4,5}
玩家拆掉 基线家具 id=1         → 写 StructureRemoved(1)
玩家再放置一件                  → 写 Placed(6)
存档 S2 = [Placed(1), Placed(2), StructureRemoved(1), Placed(6)]   ← 混合态
载入2:判据命中(Placed 1,2 ≤ 3)⇒ 全量 +3
   Placed(1)→4  Placed(2)→5  StructureRemoved(1)→4(!)  Placed(6)→9
   ⇒ 被删的是**玩家结构 4**,而**基线家具 1 复活**                       ❌❌
```

**根因是歧义,不是参数**:`StructureRemoved(1)` 在**纯前基线存档**里指「玩家的 1 号结构」,
在**平移后的存档**里指「基线家具 1」—— 两者**同为整数 `1`**,事件流层面**无法区分**。
⇒ 任何「读时猜」的方案都不成立。

**⇒ 立一条不变量:**

> **混合存档不变量**:任何**被写出**的存档,其结构 id 空间**要么全在旧空间**
> (含 `Placed` 的 id ≤ `N`),**要么全在新空间**(无 `Placed` 的 id ≤ `N`)。
> **混合态存档不得被写出。**

⇒ **固化 = 载入期平移后,在 `WorldLoadService.Rebuild()` 的同一次调用内把平移结果写回事件流**
(不是「重建出的世界对了就行」;写存档读的是**事件流**,不是世界)。这样本会话之后写出的每一份存档
都**全在新空间**,判据永不复发。

⚠️ **该能力今天不存在**:`WorldLoadService` 是**只读**消费者(C4),事件流替换需要
`IPayloadEncoder`(ADR-029)+ 一个「整表替换」接缝,**两者在载入路径上都没有生产实现**。
⇒ **这正是本件实现必须与 `TD-persist-09` 同批的第二个理由**(第一个是载荷字节)。

### ⑤ 落点 = 重放入口,**不得**做成迁移脚本

| | |
|---|---|
| **落点** | `WorldLoadService.Rebuild()` 的解码之后、`RebuildFromDecoded` 之前 |
| **实现形态** | 纯函数 `StructureIdRemap`(住 `Sim` —— C6:零 codec 依赖,只吃 `StructureRestoreOp` 序列) |
| **不得** | 做成 `ISaveMigration`(C1:迁移链逐字节禁改正文,`SaveMigrationChain.cs:127-133` 会直接抛) |

> `SaveMigrationChain.cs:27-29` 自陈「若某条真实迁移**确实要改正文** …… 它**要么不该存在,
> 要么须先改 AC**」。**该自陈不适用于本件** —— 本件**不是迁移**,不经过那条链。

### ⑥ 代价:等于改写历史事件载荷(用户已明示接受)

第二次存盘写出的 `StructurePlaced` 与首次写下的**字节不同**(仅 `structure_id` 一项)。
这与 `AC-7a-14`「已发生事件原样保留」的**字面**相抵 ⇒ 须同时写明口径(见 §Consequences):

> **`AC-7a-14` 约束的是「迁移链对存档字节的处理」,不约束「重放入口对同号形态的一次性 id 归一化」
> —— 后者确实改写了已发生事件的 id,两者不可混读。**

### ⑦ 本件只裁语义与判据,实现推迟

**未落四件**:`StructureIdRemap` 的实现 · 接线(`WorldLoadService.Rebuild()`)·
**写回事件流的接缝**(`IPayloadEncoder` + 整表替换,见 §④之补)· 载荷持久化(v2 布局)。
四者**同批**,随 `TD-persist-09`。⚠️ **不得只落前两件** —— 那就是 Alt 5(漂移 + 删错结构)。

### Architecture

```text
读档(存档字节)
  └─ 解码三支结构事件 → StructureRestoreOp[]
       ├─ 无 Placed 的 id ∈ [1,N] ──────────────→ 原样重放(恒等,今日全部路径都走这支)
       └─ 命中 ─→ K = N
                  ├─ 全部 id += K          (纯整数,住 Sim —— ADR-025 §①)
                  └─ 重放(Restore 自行把高水位抬到 maxId+N)
                        └─ 世界 = 基线 1..N  ⊕  存档结构 N+1 .. N+maxId   —— 两段不交
同一次载入内 → **平移结果写回事件流**(需编码器 + 整表替换接缝)
下次存盘 → 写出的全是**新空间** id(载荷字节须进存档 = v2 布局)⇒ 判据不再触发 ⇒ 幂等
```

### Key Interfaces

```csharp
/// <summary>基线诞生前存档的结构 id 撞号处置(ADR-033)。纯整数,零 codec 依赖。</summary>
public static class StructureIdRemap
{
    /// <summary>
    /// 判定并应用平移。<paramref name="baselineCount"/> = 基线件数 N。
    /// 返回 true 表示**触发了**平移(<paramref name="remapped"/> 为新数组);
    /// false 表示恒等(<paramref name="remapped"/> 与输入逐项相同)。
    /// </summary>
    public static bool Apply(IReadOnlyList<StructureRestoreOp> ops, int baselineCount,
                             out StructureRestoreOp[] remapped);
}
```

### Implementation Guidelines

1. **`N` 的取得时序**:`N` 只有在 `World.Reset()` / `ApplyBakedInitial` **跑完之后**才有值
   (`World.cs:176`)。两处都可行,但**取不到时不得静默跳过**:
   - 若放 `StructureRestorer.RebuildFromDecoded` 内:紧跟 `_world.Reset()` 之后取 `BakedInitialStructureCount`,最稳;
   - 若放 `WorldLoadService.Rebuild()`:须显式依赖「基线已在装配期应用」这一前置,
     并在「`N == 0` 而 `ops` 里存在 `Placed` 且 `id ≥ 1`」时 **fail-closed 抛** ——
     静默跳过 = 判据失效而无人知(本项目抓过的「门没跑也是绿」同族)。
2. **三支同一映射**:`Placed` / `Removed` / `Modified` 必须用**同一个** `id → id + K`(见 §Decision ③)。
3. **不得引入第二发号器**(C3):平移后由 `Registry.Restore` 自行抬高高水位,
   `StructureWriter.Register` 继续用 `_nextStructureId++`。
4. **溢出**用 `checked`(§Decision ③)。
5. **触发时须可观测**:至少一条日志/返回值(禁静默),否则「平移发生过」在事后不可分辨。

## Alternatives Considered

### Alternative 1: 基线赢 —— 命中即 fail-closed 拒收

优点:不改写任何历史。**否决理由**:`RebuildFromDecoded` 是「先 `Reset()` 再重放」,
拒收 = **整个世界卡在读档失败**,玩家在该存档上的全部建造与进度一并不可达。对一个
**今日零可达**的形态付出「旧档永久打不开」的代价,不成比例。

### Alternative 2: 存档赢 —— 维持现状的覆盖语义

优点:零改动。**否决理由**:基线家具被**静默**删除(无事件、无日志、无异常),
`BakedInitial` 这个真源被事后不可观测地删掉了一部分 ⇒ 违反 R1。
若反过来把它写成规格(在 GDD 规则三补「基线可被存档覆盖」),那么**任何**关卡设计者摆的家具
都会在旧档上消失,且 `Overlay` 的「`BakedInitial` 是派生态、可重建」性质被破坏
(重建结果依赖存档,不再是派生态)。

### Alternative 3: **只平移撞号的那些**(`id ≤ N`),量取 `N`

表面上最省事 ——「只动与基线冲突的,其余原样」。**反例(离线复算,`N = 3`,存档 `Placed(1..10)`)**:

```
映射 = {1→4, 2→5, 3→6, 4→4, 5→5, 6→6, 7→7, 8→8, 9→9, 10→10}
结果 = [4, 5, 6, 4, 5, 6, 7, 8, 9, 10]        ⇒ 4/5/6 各被两组旧 id 争用
```

原因:被平移的低号 `1,2,3` 落到 `4,5,6`,而 `4,5,6` 本身**不满足 `id ≤ N`** ⇒ 原地不动 ⇒ 撞。
⚠️ **该方案在 `M ≤ N` 时完全正确** —— 这正是它看起来对的原因;但 `M > N`(玩家建得比基线件数多)
**完全可能**,判据不能只在一种情形下成立。**否决**。

### Alternative 4: 整体平移,量取 `K := max(N, maxId)`

见 §Decision ③ 的「起草期自查二」—— 这是本 ADR **初稿的裁定**,被自查推翻。**复算结论**:
恒无碰撞,但 `K` 随存档增长 ⇒ **全部** id 逐轮暴涨(`1→7→14→28…`)。**否决**。

### Alternative 5: 纯内存重映射,**不固化**

**反例(离线复算,`N = 3`,存档 = `Placed(1), Placed(2)`,每轮放置一件)**:

```
轮0  载入 1→4, 2→5                          世界 = 基线{1,2,3} ∪ {4,5}   ✅ 两边都在
     玩家放置 ⇒ 高水位 5 ⇒ id 6;存档 = [Placed(1), Placed(2), Placed(6)]  ← 旧事件从未重写
轮1  载入 1→4, 2→5, 6→9                     ← 6 是上一轮「已在平移空间里」的号,又被平移一次
     玩家放置 ⇒ 高水位 9 ⇒ id 10;存档 = [1, 2, 6, 10]
轮2  载入 1→4, 2→5, 6→9, 10→13
轮3  载入 1→4, 2→5, 6→9, 10→13, 14→17
```

**否决理由有两条,第二条是致命的:**

1. **漂移(次要)**:**每一个「载入后才放置」的结构,其 id 每轮读档再涨 `N`**
   ⇒ 同一结构**跨会话没有稳定身份**;**高水位无界上涨**(`6 → 10 → 14 → …`),
   违反 ADR-008 §六 同族的**有界性**纪律。
2. **正确性(致命)**:见 §Decision ④之补 —— 一旦玩家在**未固化**的会话里拆了一件基线家具,
   写出的就是**混合存档**;下次载入把 `StructureRemoved(基线号)` 一并平移
   ⇒ **删错结构 + 基线家具复活**(两个错误同时发生)。

⇒ 这就是 §Decision ④「固化是硬义务」与 ④之补「固化必须同会话」的全部理由。

### Alternative 6: 做成 `ISaveMigration`(v1 → v2 的迁移脚本顺手平移)

优点:看起来「归档」在迁移里,概念清楚。**否决理由**:**结构上不可实现** ——
`SaveMigrationChain.cs:127-133` 对每一步**逐字节**断言事件正文(`FixedTimeEquals`)不变,
而平移改的正是正文里的 `structure_id` ⇒ 链**必然抛**。要走通它须先推翻
`AC-7a-14` 的正文不变条款,而该条款是「已发生事件原样保留」的机械表达,推翻它 = 放弃一条既有 AC。
**否决**。

### Alternative 7: 基线改驻高号段(`K0..K0+N-1`),旧档保持 `1..M`

优点:**零载入期逻辑、天然幂等**,§Decision ③④ 的全部问题一并消失。**搁置理由**:
① 引入魔法常量与**第二套号段分区规则**(`N` 从此依赖 `K0`,而 `K0` 只有一个合法取值来源);
② `World.ApplyBakedInitialBaseline`(`World.cs:129-177`)及**全部「基线 id = 1」夹具**被推翻
(实测:`structure_restore_test.cs:335` 断言 `Structures.TryGet(1, …)` / `:344` 构造 `Placed(1, …)`);
③ 为一条**今日零可达**的形态付出全仓爆炸半径 —— 与本项目「当下主义优于未来主义」
(ADR-017 §四)同口径。**若将来**其他形态**也需要号段分区,可重开本 Alt。**

## Consequences

### Positive

- **R1 达成**:玩家建造与关卡基线都保住,无静默丢失。
- 判据**充要**,且可从存档字节**离线复算**(不需要跑游戏)。
- 与 `Overlay(t) := BakedInitial ⊕ 事件` 的模型**自洽**:`⊕` 在同号形态下的语义被定义为
  「存档侧平移后再并」,基线侧恒等。

### Negative

- **改写历史载荷的 `structure_id`**(用户已明示接受)。⇒ 须同时钉死 `AC-7a-14` 的口径:
  > `AC-7a-14` 约束**迁移链**;不约束**重放入口的一次性 id 归一化**。两者不可混读。
- ⚠️ **本件不改变 `TD-building-02` 的 `P0 面 = N`** —— 今日可达性仍为零。

### Neutral

- 今日落地**不改任何现有行为**(判据在任何现存路径上都不触发)。
- 不新增 `SimEvent.Kind`、不新增抽象点、不动 `entities.yaml`、不动 `Sim` 引用集。

## Risks

| # | 风险 | 缓解 |
|---|---|---|
| **R1** | **固化未落而先落重映射** ⇒ ① 漂移(Alt 5 之 1);② **正确性崩塌**:混合存档被写出后,`StructureRemoved(基线号)` 被误平移 ⇒ **删错结构 + 基线家具复活**(Alt 5 之 2 / §Decision ④之补) | **硬约束:重映射与固化同批落**(与 `TD-persist-09` v2 布局)。本条是 §Decision ⑦ 的全部意义 |
| **R2** | 判据用**当前** `N`;而 GDD `EC-23-05` 承诺「`BakedInitial` 随骨架版本化,旧存档重放用**旧**基线」⇒ 若旧 `N_old > N`,id 落在 `(N, N_old]` 的 `Placed` **漏判** | **已知局限,本件不解**:`BakedInitial` 今日**不存档**(GDD `:149`),取不到旧 `N`。随 v2 布局的「旧基线」义务另裁。⚠️ 在「基线只增不减」的常规演进下不漏判(增 ⇒ `N` 变大 ⇒ 判据更宽,不会漏) |
| **R3** | 平移只改 id,不改 `Removed` 的 `Cell` / `ModuleId` | 有意为之:那两个字段是**观测值**,不是身份;改它们会掩盖真实的载荷不一致 |
| **R4** | 后续消费者若把 `structure_id` 当**稳定外部标识**(遥测 / 存档外部引用) | 已由 §Decision ⑥ 登记为代价;`StructureId` 的契约是**会话内身份**,跨基线纪元不变性**不被承诺** |

## Performance Implications

载入期 O(#结构事件) 一次遍历 + 一次数组分配。**无热路径、无逐帧成本**。
`N` 是现成值(`BakedInitialStructureCount`),**不再需要算 `maxId`**(现裁的 `K` 与存档内容无关);也不需要排序。

## Migration Plan

**无迁移脚本** —— 本件**不是迁移**(§Decision ⑤)。
实现随 v2 存档布局(`TD-persist-09`)落,不单独立项。

## Validation Criteria

| # | 判据 | 状态 |
|---|---|---|
| **V-1** | **判据必要性**:构造「后基线存档 + 一条引用基线号的 `Removed`」⇒ **不触发**(id 不跳号) | **未跑** |
| **V-2** | **判据充分性**:构造「前基线存档(含 `Placed(1)`)」⇒ **触发** | **未跑** |
| **V-3** | **三方不交**:平移后号段与 `1..N`、与旧号段**两两不交** | **未跑** |
| **V-4** | **幂等**(需固化载体):固化后重跑 ⇒ 恒等 | **未跑** |
| **V-5** | **Alt 3 / Alt 5 反例各一条夹具**(证明「只平移撞号的那些」在 `M>N` 时撞、不固化会漂) | **未跑** |
| **V-6** | 全量回归无回退 | **未跑** |
| **V-7** | **混合存档不变量**:任何被写出的存档,结构 id 空间**非全旧即全新**(§④之补)。反例夹具 = 未固化路径下「拆基线家具 + 放置」⇒ 下次载入**删错结构** | **未跑** |

> ⚠️ **V-1…V-7 全部未跑,禁借绿。** `Accepted` 表示**方向已裁**,不表示任何判据已通过。
> V-1…V-5 / V-7 须随实现批落(它们需要 `StructureIdRemap` 存在);**V-4 还需要固化载体**
> ⇒ 在 v2 布局之前 **V-4 物理上不可跑**。

## GDD Requirements Addressed

| TR / AC | 关系 |
|---|---|
| **AC-23-02** | 「占用真相 = `BakedInitial ⊕ 事件` 纯函数」—— 本件给 `⊕` 在**同号形态**下的定义 |
| **EC-23-13** | 开档基线重放 —— 本件补其同号支 |
| **EC-23-14**(新增) | 本件在 `modular-building.md` 新立的边界行 |
| `AC-7a-14` | **不修改该 AC**,只在其旁钉死口径(§Consequences),避免与 §Decision ⑥ 字面相抵 |

## Related

- **债册**:`TD-building-02`(本件结清其「语义未裁」半边)· `TD-persist-09`(固化载体,同批)
- **ADR**:`adr-005` · `adr-006`(Amendment B)· `adr-009`(§四 派生态 / §七 三段式)·
  `adr-010`(存档布局)· `adr-017`(§四 当下主义)· `adr-025`(§① 程序集清单)·
  `adr-029` / `adr-031`(载荷编解码,本件**不经**它们)
- **GDD**:`design/gdd/modular-building.md` 规则三 / 规则四(三 `Kind`)/ 规则七 · `EC-23-05` / `EC-23-13`
- **实现件**:`StructureRestore.cs` · `StructureKinds.cs` · `World.cs` · `WorldLoadService.cs` ·
  `SaveMigrationChain.cs` · `SaveHeader.cs`
- **评审原件**:`production/qa/evidence/fix-s20-20-baked-initial-baseline-2026-10-28.md`(B-1 修复与 schema-1 订正)
