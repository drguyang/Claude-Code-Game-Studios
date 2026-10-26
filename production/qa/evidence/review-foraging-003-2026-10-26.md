# 评审原件 —— foraging-003「三条世界流事件 + 主机铸造身份 + 恰好一条 SkillGrown」

> **日期**:2026-10-26(判定当日)· **原件落盘**:2026-10-26(同日)
> **对象**:`production/epics/foraging/story-003-three-events-and-identity-mint.md`
> **被评件**:
> - `unity/Assets/Sim/Foraging/HarvestOrchestrator.cs`(新建 · 291 行 · 生产件)
> - `unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs`(新建 · 12 `[Test]` + 5 `[TestCase]` = 17 条夹具)
> **评审方式**:`lead-programmer` 子代理,`/code-review` 口径(只读)
> **裁定**:**CHANGES REQUIRED** —— **BLOCKING 2**(B-1 / B-2)· WARNING 4(W-1..W-4)· MINOR 3(M-1..M-3)
> **纪律**:本件按 Phase 8b 双列结账(`Landed` / `Proven` / `Branch`);未跑判据的项**不得**标 `Proven ✅`。
> **本轮无任何修复** ⇒ 结账表全部 `Landed=✗ / Proven=✗`。

---

## 〇、本件的性质声明(必读)

⚠️ **本件是评审当场产出的原件,不是事后补落。** 分析段由 `lead-programmer` 子代理在
2026-10-26 的增量草稿(`tmp-review-foraging-003-lead.md`,已按轮次纪律逐条 Append)整理而成;
其中 **B-1 / B-2 两条 BLOCKING 经编排方独立复核确认成立**(复核口径见 §二 / §三 的「复核确认」行)。

⚠️ **本法只读** —— 未改动任何生产代码或测试文件。

---

## 一、裁定摘要

| 级别 | 编号 | 一句话 |
|---|---|---|
| **BLOCKING** | **B-1** | 「任何前置失败都在任何写入之前抛出」**为假**:`Validate` 不核 `Novelty` / `XpGain`,而这两项的校验在**第 ④ 步(最后一次写入之后)**,且第 ② 步已 `Append` ⇒ **写了一半可观测** |
| **BLOCKING** | **B-2** | 事件序与 story 相反:`ResourceHarvested` 先于其出生事件 `DropSpawned` 落流;代码给出的理据(「`gather_seq` 须先算」)**不成立** |
| WARNING | W-1 | GDD/registry/ADR 三方写者口径冲突的处置**判为恰当**(照 ADR-024 取 registry),但**未留裁决锚** |
| WARNING | W-2 | 成长入口有**三套**抽象(`SkillGrownEmitter` 静态 / `ISkillGrowthPort` / 新造 `IGrowthEmitter`),且两处适配器方法体近逐字重复 |
| WARNING | W-3 | `HarvestRequest.Novelty` **无生产者** —— 全仓无 novelty 求值方,照既有端口写法落地则「首次 ×3.0」永不触发,且无夹具会红 |
| WARNING | W-4 | story 声明的两条 Test Evidence 路径**不存在**;实际载体是另一棵树、另一个文件名 |
| MINOR | M-1 | `IDropWriter.Spawn` 的 doc 说「同时是 `instance_id` 的铸造点」,但 `NullDropWriter.Spawn` 不铸任何东西 —— doc 描述的是 20 落地**后**的语义 |
| MINOR | M-2 | `novelty` 域界 / `XpGain` 负值两条边界无夹具(与 B-1 同源) |
| MINOR | M-3 | story 卡 AC-17-05 / 05b 已具备证据却仍为 `- [ ]`(AC-17-01 不勾**正确**) |

### 通过项(逐条实测,见 §六)

门 A ✅ · 公有 API 文档注释 ✅ · 方法 ≤40 行 / CC<10 ✅ · 无 `Math.Round` / libm / 浮点 ✅ ·
无硬编码游戏数值 ✅ · 无自指判据 ✅ · `NullDropWriter` 语义诚实 ✅ · 回归数可复算 ✅

---

## 二、B-1(BLOCKING)—— 原子性自述为假

### 原判定

`HarvestOrchestrator.cs:30-32` 与 `:196` 两处自述:

> 「编排器先算出**全部**四条载荷(含 id 铸造),再依次提交。任何一步的前置校验失败都在
> **任何写入之前**抛出 ⇒「写了一半」在结构上不可达。」

### 实测:假

**第一层事实 — `Validate` 只核 8 项,不核 2 项。**

`HarvestOrchestrator.cs:263-288` 逐项枚举(Tick / ActorId / NodeId / ItemKeyOrdinal /
Qty / OutQuality / SkillId / ObjectId),而 `HarvestRequest`(`:122-175`)共 **11 个字段**
(`NodeCell` 是 `WorldPos` 值类型,无域界需求;`Novelty` 与 `XpGain` **两个有域界的字段被漏)**:

| `HarvestRequest` 字段 | `Validate` 核? | 下游写者另核? | 下游核在哪一步 |
|---|---|---|---|
| `Tick` | ✅ `:265` | `Record.cs:105` | ② |
| `ActorId` | ✅ `:267` | `EmitGrowth`(Contracts)`:48` | **④** |
| `NodeId` | ✅ `:270` | `Record.cs:110` | ② |
| `ItemKeyOrdinal` | ✅ `:273` | — | — |
| `Qty` | ✅ `:276` | `Record.cs:114` | ② |
| `OutQuality` | ✅ `:279` | `Record.cs:116` | ② |
| `SkillId` | ✅ `:282` | `EmitGrowth:51` | **④** |
| `ObjectId` | ✅ `:285` | `EmitGrowth:54` | **④** |
| **`Novelty`** | **✗ 无** | `EmitGrowth:59`(`(int)novelty < 0 \|\| > Normal`) | **④** |
| **`XpGain`** | **✗ 无** | `EmitGrowth:65`(`xpGain.Raw < 0`) | **④** |

**第二层事实 — 第 ② 步立即落盘,不是延后提交。**

`ResourceHarvestWriter.Record`(`Sim/World/ResourceHarvestWriter.cs`)在方法尾部**立即**
`_eventSink.Append(...)`(`:135-137`),无缓冲、无回滚。

⇒ **可复现的半写路径**:

```csharp
orch.Resolve(Req(novelty: (NoveltyClass)7));   // 或 Req(xpGain: new Fix(-1))
// ① ItemInstanceId 已从 IIdAuthority 铸造(计数器已推进)
// ② ResourceHarvested 已在世界流里(不可撤回)
// ③ DropSpawned / DropClaimed 已向 20 提出(真实现下亦已落)
// ④ SkillGrownEmitter.EmitGrowth 抛 ArgumentOutOfRangeException
```

净结果:**世界流里有一条 `ResourceHarvested` 引用一个永无出生事件的 `instance_id`**,
且 `IIdAuthority` 计数器被空耗。这正是 ADR-023 ⑤ / ADR-009 §七 反复强调的
「二有一」非法序列。

**复核确认**(编排方独立复核,2026-10-26):成立。`Validate` 守卫集与
`SkillGrownEmitter.EmitGrowth` 守卫集的**差集恰为 `{Novelty, XpGain}`**,且
`Record` 的 `Append` 确实在 `Resolve` 第 ② 步内同步发生。

### 为什么这不是「推演」而是「实况」

测试 `test_invalidRequest_throwsBeforeAnyWrite`(`:432-472`)的 5 条 `TestCase`
(`:427-431`)覆盖 tick / actorId / nodeId / qty / outQuality —— **恰是 `Validate` 已覆盖的字段**。
两个漏项没有任何夹具。⇒ 现有 17 条夹具**对该属性零判别力**:把 `Validate` 整段删掉,
grep 也找不出一条会因此转红的用例(除 tick/nodeId/qty/outQuality 那四条,它们由下游
`Record` 接住 —— 这正是 M5 曾借绿的同一形态)。

### 修法(Required)

`Validate`(`:263-288`)追加两项,置于 `ObjectId` 之后:

```csharp
if ((int)r.Novelty < 0 || (int)r.Novelty > (int)NoveltyClass.Normal)
    throw new ArgumentOutOfRangeException(nameof(r), r.Novelty,
        "novelty_class ∈ [First, Normal](30 定义的三值域)");
if (r.XpGain.Raw < 0)
    throw new ArgumentOutOfRangeException(nameof(r), r.XpGain,
        "xp_gain 不得为负(XP_gain 三因子均非负 ⇒ 负值 = 调用方算错)");
```

**为何不改成「把第 ④ 步挪到最前」**(它是唯一能靠改序修复的另一条路):

1. **语义**:GDD 规则五 / AC-17-05b 要求成长「挂在裁决尾部」—— 它是**产出侧**的派生,
   不是前置。挪到最前会把「一次采集」变成「先发成长、再定成败」。
2. **全序**:`Seq` 由 `EventStream` 按 `(Tick, Patient)` 组内 append 序发号
   (`EventStream.cs:223-228`)。采集三条与世界流同 `Patient = None`、`SkillGrown` 是病史流
   但共享同一 `(Tick, None)` 计数域(ADR-009 §三)—— 改动 append 序 = 改动**跨流全序键的
   实际取值**,影响 7a 重放与 45 的 `ReorderBuffer` 对齐。
3. **不彻底**:即使前置,`Emit` 内部仍可能因编码器失败抛出 —— 修法只是把**可判定**的
   域界错误前置,不是把副作用链改成事务。

⇒ **补齐前置校验是唯一既不动语义、又不动全序的修法。**

---

## 三、B-2(BLOCKING)—— 事件序与 story 相反

### 原判定

story `§Implementation Notes` #1(`story-003-...md:45`)明定:

> `DropSpawned`(铸 id,`spawn_anchor = node.cell`,`item_key` = 品种,`qty` 由 F-17-2)→
> `ResourceHarvested`(… 引用刚铸 id …)→ `DropClaimed`

代码 `HarvestOrchestrator.cs:245-254` 实际次序:

```
② _harvestWriter.Record(...)      → ResourceHarvested
③ _dropWriter.Spawn(...)          → DropSpawned
   _dropWriter.Claim(...)         → DropClaimed
```

### 后果

`Seq` 依 append 序发号 ⇒ 世界流中 `ResourceHarvested{instance_id = N}` 的 `Seq`
**严格小于**其出生事件 `DropSpawned{instance_id = N}` 的 `Seq`。即:**引用先于出生**。

对下游 20 的 `InventoryOf` fold 而言,这意味着按 `(Tick, StreamPriority, Patient, Seq)`
顺序单遍扫描时,会先遇到一个指向**尚未存在实例**的采集事实。当前 20 侧 fold 未落,
故**今天不会炸**;但它使「自空流单遍重放一次到位」不再成立,须二遍或延迟绑定 —— 而这正是
`ADR-008` 全序键设计要消除的东西。

### 代码给出的理据不成立

`HarvestOrchestrator.cs:246-247`:

> 「写者自算 `gather_seq`(计数源 = 本 Kind 的节点计数),**故它必须先于 ③ ④ 执行** ——
> 后续两条不参与计数。」

`ResourceHarvestWriter.CountHarvestsOfNode` **只遍历 `EventKind.ResourceHarvested`**
(`:135` 起的循环首行即 `if (e.Kind != EventKind.ResourceHarvested) continue;`)。
`Drop*` 两条根本不在计数面上 —— 这是 ADR-009 Amendment K ④ 的**明定**设计
(「`gather_seq` 的计源 = `ResourceHarvested`,只数 17 自己的 Kind」,正是为消除
「死亡掉落 / 18 溢出 / 52 spawn 点污染序列」)。⇒ **`Record` 与两条 `Drop` 的调用先后
对 `gather_seq` 的取值毫无影响**,该理据是从「计数源」错推出来的。

**复核确认**(编排方独立复核,2026-10-26):成立。

### 修法(Required)

`Resolve` 的 ② ③ 对调,并在 `Claim` 之后取 `gatherSeq` 供返回值:

```csharp
// ── ② 20 的身份出生(铸 id 的出生事件;规则三)──
_dropWriter.Spawn(request.Tick, instanceId, request.NodeCell,
                  request.ItemKeyOrdinal, request.Qty);

// ── ③ 17 的采集事实 ──
int gatherSeq = _harvestWriter.Record(
    request.Tick, instanceId, request.NodeId, request.Qty, request.OutQuality);

// ── ③' 20 的立即归属 ──
_dropWriter.Claim(request.Tick, instanceId, request.ActorId);
```

同时**删掉** `:246-247` 那句「故它必须先于 ③ ④ 执行」,替换为:

> `gather_seq` 的计源是 `ResourceHarvested` 自身的节点计数(Amendment K ④),
> `Drop*` 两条不参与 ⇒ 调用先后对取值无影响。次序由 story 的全序要求决定。

**对调后是否引入新的半写路径?** 不引入 —— `Record` 的全部入参守卫
(`tick < 0` / `nodeId < 0` / `qty < 1` / `outQuality < 1` / `instanceId < 0`)
在 B-1 修法补齐后**全部被 `Validate` 先行覆盖**(`instanceId` 由 `IIdAuthority`
铸出,按机制 A 构造非负)。⇒ `Spawn` 之后 `Record` 不可能因入参抛。
**残留(须登记,不属本件)**:若 20 的**真实** `Spawn` 实现自己抛式校验,那属 20 的实现义务,
由 b7 写者门正面管辖;本件不为其兜底。

---

## 四、WARNING / MINOR 明细

### W-1 —— 写者口径冲突的处置:判为**恰当**,但缺裁决锚

三方原文:

| 出处 | 写的写者 |
|---|---|
| `design/gdd/foraging.md:189-190` | **17(主机)** —— `DropSpawned` / `DropClaimed` 两行 |
| `design/registry/entities.yaml:2547 / :2561` | **20 库存与物品** |
| `docs/architecture/adr-009-world-state-event-boundary.md:288` | `(20 / 25 / 死亡掉落)` |

**判定:本件的处置不构成「单方面改跨域」(不违 `coordination-rules` 5)。** 理由:

1. **ADR-024 §① 已把裁量权交出去**:`entities.yaml` 是 `Kind` 的**唯一登记真源**,
   且明写「不一致时以 registry 为准」并触发 V-1 断言失败。⇒ 实现期取 **20**
   **不是新裁**,是**执行既有 Accepted ADR**。
2. **本件没有单方面抹掉 GDD**:`HarvestOrchestrator.cs:5-13` 把三方原文**逐字并列**
   记录在案,并注明 registry 自身的歧义警告(`:9-11` 原样引用
   「实现期若 17 GDD 改口径回改本条」),再说明本件选哪一侧。这是**呈报**,不是**改判**。
3. **形态是「形状先行」而非「假装接线」**:`IDropWriter` + `NullDropWriter`
   (`:47-77`)把 17 的**要求面**与 20 的**落流面**分开,AC-17-01 因此**不勾** ——
   这正是「未接线」的诚实形态(见 N-6)。

**但缺一条可追溯的锚**:story 卡(`:17`)的 `ADR Governing Implementation` 只列
ADR-009 / 010 / 024 / 005,**未登记「GDD↔registry 写者冲突及取 20 的裁决出处」**。
下一个读者打开 story 卡看到「三条恰发」,无法从卡面得知「其中两条今天不会发」。
⇒ **建议补登**(不阻塞):story 卡加一行「已知口径分歧 + 裁决依据 = ADR-024 §①」。
真正的收口仍在 17 GDD / 20 GDD 的下一轮 —— 与 `adr-009:288` 的 `(20 / 25 …)`
写法对齐,或由 17 GDD 明写「17 要求、20 落流」。**本件不是该收口的正确落点**,拒绝在此裁定。

### W-2 —— 成长入口三套抽象并存

| 抽象 | 落点 | 形状 |
|---|---|---|
| `SkillGrownEmitter.EmitGrowth`(静态) | `Sim.Contracts/SkillSystem/SkillGrownEmitter.cs` | 只造 `SkillGrownPayload`,**不 Append**(30 明写) |
| `ISkillGrowthPort` | `Sim/Prescription/PrescribeFlow.cs:99-108` | `EmitGrowth(actor, skill, object, noveltyHint, kDifficulty, tick, patientId)` |
| **`IGrowthEmitter`(本件新造)** | `HarvestOrchestrator.cs:84-89` | `Emit(tick, actor, skill, object, novelty, level?, xpGain)` |

**问题不在「新造接口」本身**(见 §五 的判定:作为可测性 seam,它**成立**),而在于:

1. **方法体重复**:`SkillGrownEmitterAdapter.Emit`(`:110-119`)与
   `Gameplay.Boot/PrescriptionPorts.cs` 的 `AssemblySkillGrowthPort.EmitGrowth`
   (~`:155-172`)是**同一段逻辑** —— `EmitGrowth(...)` → `_encoder.Encode` →
   `new SimEvent(tick, patientId, -1, SkillGrown, ref)` → `Append`。两份各自演化 = 下一处
   修 (如 `patient_id` 口径变更) 必有一份被漏。
2. **`SkillGrown` 现在有两个生产写者**:`WriterExistenceGate` 扫 `Assets/Sim` 的
   `Encode(EventKind.` —— `HarvestOrchestrator.cs:115` 是新命中面。当前不红
   (SkillGrown 的豁免早已撤),但**「SkillGrown 写者」从一处变成两处**这件事没有任何门
   或注释记录。
3. **签名不一致**:`ISkillGrowthPort` 传 `kDifficulty`(30 侧算 XP),`IGrowthEmitter`
   传 `xpGain`(调用方算好)。两者都合法(丙案 / 丙-3 口径),但**同一件事两种单位**散落两处。

**建议**(不阻塞):让 `SkillGrownEmitterAdapter` 实现 `ISkillGrowthPort` 的 `EmitGrowth`
或反向 —— 复用一个适配器,而不是在两个程序集里各写一份。若 17 确实需要 `xpGain` 直传
(它自己算,符合丙案「发射方算好推入」),则应在 doc 里明写**为什么不能复用**
`ISkillGrowthPort`(差异 = `novelty` 来源与 `kDifficulty` vs `xpGain`),否则第三次复制会被
当成合法先例。

### W-3 —— `HarvestRequest.Novelty` 无生产者

本件把 `novelty` 作为**入参收下、自己不算**(`:152-153` + `:257-258` 原样透传)——
这个分工是**对的**(GDD 规则五:`novelty_class` 由 30 定义)。问题是**谁算**:

```bash
grep -rn "NoveltyTracker" --include=*.cs unity/Assets/ | grep -v Tests
# 生产侧无实例化点(既有端口自陈见下)
```

既有先例 `Gameplay.Boot/PrescriptionPorts.cs` 的 `AssemblySkillGrowthPort` 在 doc 里自陈:

> 「⚠️ `noveltyHint` **仍不采信** …… 本端口无 tracker 实例,故 novelty 仍取
> `NoveltyClass.Normal`」

⇒ 若 17 的装配轮照抄这套写法,采集的**「首次遇见该品种 ×3.0」永远不会触发**,
所有采集成长按 ×1.0 结算。这不是本件的缺陷(本件正确地只做透传),但它是
**AC-17-05b 之外的一条静默失值路径**,且**无任何夹具会因此变红**。

**建议**(不阻塞):登记为具名债(命名建议 `17-采集新颖度无求值方`,归 30 / 17 装配轮),
并在 `HarvestRequest.Novelty` 的 doc 里加一句「本件只透传,求值归装配轮(当前无实现)」——
参照 `AssemblySkillGrowthPort` 的诚实自陈体例。

### W-4 —— story 声明的 Test Evidence 路径不存在

story `:96-98` 要求:

```
- Integration: unity/Assets/Tests/PlayMode/foraging_stream_events_test.cs — must exist and pass
- Logic(AC-17-05 反射面): unity/Assets/Tests/EditMode/Foraging/gather_identity_reflection_test.cs
```

实测两条**均不存在**(`ls` 两份路径 ⇒ `No such file or directory`)。实际载体 =
`unity/Assets/Tests/EditMode/Foraging/harvest_orchestrator_test.cs`(单文件承担 4 条 AC)。

违 `.claude/docs/coding-standards.md` §Testing Standards 的注:
> 「路径写明**实际落地**的那棵树,且允许一条 AC 多载体」

且 story `§Test Evidence` 的 `Status` 仍为 `[ ] Not yet created`。⇒ **须回填**(不阻塞评审,
但阻塞 story 转 `Complete`)。

### M-1 —— `IDropWriter.Spawn` 的 doc 描述了当前不存在的行为

`:49-50` 写「**同时是 `instance_id` 的铸造点**(规则三)」。
作为 **20 的契约**这句是对的;但在**当前装配**下 `NullDropWriter.Spawn`(`:73`)是空方法,
真正铸造在 `Resolve` 第 ① 步。⇒ 读者可能从 `IDropWriter.Spawn` 的 doc 推断「id 由 20 铸」,
与 AC-17-05(17 侧零本地铸造、唯一来源 `IIdAuthority`)读起来矛盾。

**建议**:doc 改为「20 落地后,本调用即该 id 的**出生事件**;id 本身由 17 在调用前经
`IIdAuthority` 铸造(`HarvestOrchestrator.Resolve` 第 ① 步)」。

### M-2 —— 两条边界无夹具

与 B-1 同源:`novelty` ∈ {`(NoveltyClass)-1`, `(NoveltyClass)3`} 与 `XpGain = new Fix(-1)`
三条非法输入无 `TestCase`。修 B-1 时**必须同批补**,否则新增的两条守卫同样无判别力
(承 `feedback_falsify_one_guard_per_property`:每条新守卫须有专属变异)。

### M-3 —— story 卡 AC 勾选未回填

`:34-37` 四条仍为 `- [ ]`。其中 AC-17-05 / 05b 的代码与证据均已就位(见 §六 N 项),
应回填为 `- [x]`;AC-17-01 保持 `- [ ]`(**正确**,理由见 N-6);
R11 邻接写「半边 / NOT-RUN」。

---

## 五、专项判定(评审方点名要求的三问)

### 5.1 `IGrowthEmitter` 抽象是否必要?—— **成立,是可测性 seam,不是「为测试造抽象」**

判据 = **接口的两侧是否都真实存在**:

| 侧 | 是否真实 |
|---|---|
| 生产实现 | ✅ `SkillGrownEmitterAdapter`(`:95-120`),且测试 `:115` 真的跑了它 |
| 测试替身 | ✅ `SpyGrowthEmitter`(`:512-521`) |
| 30 侧对应物 | ✅ `SkillGrownEmitter.EmitGrowth` 是**静态方法** —— 静态方法**无法**被替换,故薄适配层是唯一能让「恰一条」可断言的手段 |

⇒ 它不是「为测试造抽象」(那需要接口两侧都只为测试存在),而是**把无法 mock 的静态调用
包成可注入依赖**。**判定:接受。**

**但**:接受的是**「有一层适配」**,不是「这一层要自己写一遍」(见 W-2)。

### 5.2 06b 的分布(1 条真链路 + 3 条 Spy)—— **合理**

| 夹具 | 问的是 | 该用什么 |
|---|---|---|
| `:73` 恰一条 / `:89` 计数随采集次数缩放 / `:105` tick 归位 | 编排器**提出了什么请求** | Spy ✅ |
| `:115` 生产适配器真的落流 + 载荷逐字段可解码 | 适配器**是否真的写进流** | 生产件 ✅ |

反例才是坏味道:用真链路去断「请求了几次」(会被流里其它事件污染),或用 Spy 去断
「载荷解码正确」(Spy 里没有编码器)。**当前分布与问题层次一一对应。**

⚠️ 但必须记明其**边界**:`:73/:89/:105` 三条**不构成**「SkillGrown 已落流」的证据 ——
它们只证「17 请求了成长」。`:117-121` 的注释已把这一点写死在夹具里(「若没人跑它,
『成长已落流』就是一句**从未被执行过的自述**」),这是本件最值得肯定的写法之一。

### 5.3 `NullDropWriter` 是「显式空实现」还是「假装能跑的桩」?—— **前者,AC-17-01 的不勾诚实**

三条独立判据:

1. **注释体例**:`:59-64`「✗ 本类**不**『先欠着以后补』:它是**显式**的空实现,
   `AC-17-01` 因此不勾」「不是一个假装能跑的桩」。
2. **测试反向钉住**:`test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot`
   (`:344-373`)断言世界流 `delta == 1` 且 kind 集 == `{ResourceHarvested}` ——
   即**把「今天只能发一条」写成红灯条件**:

   > 「若将来 20 落地后有人把 `NullDropWriter` 换成真实现,这条测试**会红** ——
   > 那时应当更新它,而不是把 AC-17-01 悄悄改成绿:红的那一刻正是『三连发终于能跑了』
   > 的信号,值得被看见。」

   这是**反向夹具**(钉住当前缺陷,换代时强制复审),比「留 TODO」强一个量级。
3. **类头 AC 记分**:`:24-28` 明确写出 `AC-17-01 ⚠️ 不勾`、`R11 邻接 ⚠️ 半边`,
   且给出禁区「禁把『形状已备』读成『三连发能跑』」。

**唯一残余风险**(不构成缺陷):`NullDropWriter.Instance` 是 `public static readonly`
单例 —— 一旦 20 落地,装配层若忘了换,系统会**静默只发一条事件**而没有任何运行期信号。
当前由 `:344` 的夹具在**测试期**兜住(换实现 ⇒ 红)。建议在 20 落地轮的 story 卡里
显式登记「撤 `NullDropWriter` 装配点 + 更新 `:344` 夹具」为交付项。

---

## 六、常规项逐条实测

| # | 项 | 结果 | 判据(可复算) |
|---|---|---|---|
| N-1 | **门 A**(`HarvestOrchestrator.cs` 住 `Sim`) | ✅ | `unity/Assets/Sim/Sim.asmdef`:`references: ["Sim.Contracts"]` · `noEngineReferences: true`;被测件 `using` 面 = `System` + `Sim.Contracts` + `Sim.Contracts.SkillSystem` + `Sim.World`(同装配)· 零 `UnityEngine` |
| N-2 | 方法 ≤40 行 / CC<10 | ✅ | `Resolve`(`:235-261`)= 27 行、CC=1(无分支);`Validate`(`:263-288`)= 26 行、CC=9(8 个 `if`,全平铺、无嵌套)< 10 |
| N-3 | 公有 API 文档注释 | ✅ | 5 个公有类型(`IDropWriter` / `NullDropWriter` / `IGrowthEmitter` / `SkillGrownEmitterAdapter` / `HarvestOrchestrator`)+ `HarvestRequest` / `HarvestResult` 全部字段均带 `<summary>`;`Resolve` 带 `param` / `returns` / `exception` |
| N-4 | 无 `Math.Round` / libm / 浮点 | ✅ | 通读全文:`Math.` / `double` / `float` **零命中**;`Fix` 只作值传递与 `.Raw` 比较/透传,不做算术 ⇒ ADR-006 `ROUND_HALF_AWAY_FROM_ZERO` 无触发面 |
| N-5 | 无硬编码游戏数值 | ✅ | 常量仅 `-1`(Seq 未发号哨兵,ADR 既有约定 O-1)与 `null`(level 未升级哨兵);无阈值 / 无系数 / 无表 |
| N-6 | ADR-029 §③(载荷唯一编码路径) | ✅ | `HarvestOrchestrator.cs` 内 **零** `new PayloadRef(`;唯一编码 = `_encoder.Encode(EventKind.SkillGrown, payload)`(`:115`) |
| N-7 | ADR-005(主机唯一 Append)/ ADR-007 §四(`PatientId.None`) | ✅ | `:118` `new SimEvent(tick, PatientId.None, -1, …)`;`ResourceHarvestWriter` 同格;两处均不污染 `max(patient_id)` 高水位 |
| N-8 | ADR-024(真源 = `entities.yaml`) | ✅ | 类头 `:5-13` 逐字引 registry 三条 `author`;实现侧的取舍与该表一致(见 W-1 的判定) |
| N-9 | 自指判据扫描 | ✅ | 唯一近似:`:84` `call.SkillId == (int)SkillId.采集`,而夹具 `Req()` 用同一表达式构造 —— 但它是**透传断言**(证编排不重写 `skillId`),`objectId` 侧用字面量 `3` / `9` 做正控 ⇒ 接受 |
| N-10 | 恒真断言扫描 | ✅ | 未发现。`:100-101` 的 `SpawnCalls/ClaimCalls == 3` 是**正控**(证明 Spy 真的被调用),与 `:96` 的成长数 3 构成「不随事件数放大」的对照,非恒真 |
| N-11 | 「只测 Fake 不测生产件」残留 | ✅(边界已记明) | 生产件 `SkillGrownEmitterAdapter` 被 `:115` 覆盖;`ResourceHarvestWriter` 是既有生产件(foraging-002 已评),本件通过它写作 = 真链路。**唯一无生产的组件 = `IDropWriter`(20 未落)** —— 已由 `:344` 反向夹具与类头记分显式挂起 |
| N-12 | 回归数可复算 | ✅ | `unity/Logs/f3-full.xml`:`total="5213" passed="5178" failed="0" inconclusive="0"`;基线 `unity/Logs/f2-full.xml`:`total="5196" passed="5161"` ⇒ **+17 / +17**,等于新增夹具数,零回归 |
| N-13 | 相关门未被本次新增触发 | ✅ | `DiagnosisBoundaryGates` 的 `[D-EXIT]`(「`EmitGrowth` 唯一调用点」)扫描面限定在 `DaYiJingCheng.Gameplay.Presentation.Diagnosis` 前缀(`:48-49` / `:186-190`),`Sim/Foraging` 不在其内 ⇒ 不红。`WriterExistenceGate` 的 `DropSpawned` / `DropClaimed` 豁免条目仍在册(`:152-153`),与本件「未接线」一致 ⇒ 不红 |

---

## 七、Phase 8b 结账表(双列 + Branch)

> 🔁 **2026-10-26 修复轮回填**(同日后半轮)。原判定(§二 / §三 / §四)**一字未改** ——
> 本节只按 Phase 8b 的双列要求结清。**B-1 / B-2 / M-2 三条已转 ✅ / ✅**;
> W-1 / W-2 / W-3 / W-4 / M-1 / M-3 **仍未修**,逐行如实标注。

| ID | Landed(已落) | Proven(已证) | Branch(分支覆盖) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| **B-1** | ✅ | ✅ | ✅ 3/3(`novelty<0` · `novelty>Normal` · `xpGain<0`) | `HarvestOrchestrator.cs:305` / `:310` | 三条守卫**各自独立证伪**:M11 停 `> Normal` ⇒ `test_b1_growthFieldsValidatedBeforeAnyWrite` 红;M12 停 `< 0` ⇒ 同测红;M13 停 `xpGain<0` ⇒ 同测红。日志 `tmp/falsify_f3_review_fixes.log` / `falsify_f3_m12.log` / `falsify_f3_m13.log` |
| **B-2** | ✅ | ✅ | ✅ 1/1(`DropSpawned`→`ResourceHarvested`→`DropClaimed`) | `HarvestOrchestrator.cs:263/267/271` | M10 把 `Spawn` 挪回 `Record` 之后 ⇒ `test_eventOrder_dropSpawnedPrecedesResourceHarvested_precedesDropClaimed` 红(且**仅**它红)。日志 `tmp/falsify_f3_review_fixes.log` |
| **W-1** | ✗ | ✗ | — | `story-003-...md:17` | **未修**。须补登裁决锚(ADR-024 §① + `WriterExistenceGate.cs:152-154` 的既有登记) |
| **W-2** | ✗ | ✗ | — | `HarvestOrchestrator.cs:95-120` · `Gameplay.Boot/PrescriptionPorts.cs` | **未修**(二选一:复用适配器 / doc 明写不可复用理由) |
| **W-3** | ✗ | ✗ | — | `HarvestOrchestrator.cs:155` doc | **未修**。⚠️ 修复轮实测坐实:全仓 `grep NoveltyClass.` 只余常量定义 + 守卫说明串;`HarvestOrchestrator` **零生产调用点** ⇒ 本件是「形状完备、未接线」 |
| **W-4** | ✗ | ✗ | — | `story-003-...md:96-100` | **未修**(回填实际路径 + `Status`) |
| **M-1** | ✗ | ✗ | — | `HarvestOrchestrator.cs:49-50` | **未修**(doc 措辞)。无可执行判据 |
| **M-2** | ✅ | ✅ | ✅ 3/3(与 B-1 三项同源) | `harvest_orchestrator_test.cs:474-478` | 三条 `TestCase`(`-1` / `7` / `xpGain=-1`)已落;B-1 的 M11/M12/M13 三轮变异均**只红本条**,证明三项分支各自被钉住 |
| **M-3** | ✗ | ✗ | — | `story-003-...md:34-37` | **未修**(AC-17-05 / 05b 回填 `[x]`) |

### 7.1 修复轮的测试三元组(可复算)

| 级别 | total | passed | failed | skipped | 出处 |
|---|---|---|---|---|---|
| 夹具级(修复前) | 20 | 20 | 0 | 0 | `unity/Logs/f3-fix.xml` |
| 夹具级(补下界用例后) | **21** | **21** | 0 | 0 | `unity/Logs/f3-fix2.xml` |
| **全量 EditMode** | **5217** | **5182** | 0 | 35 | `unity/Logs/f3-full-final.xml` |
| 基线(003 初版) | 5213 | 5178 | 0 | 35 | `unity/Logs/f3-full.xml` |
| **增量** | **+4** | **+4** | 0 | 0 | = 3 个 B-1 `TestCase` + 1 个 B-2 |

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' unity/Logs/f3-full-final.xml | head -1
# 预期: total="5217" passed="5182" failed="0" inconclusive="0" skipped="35"
grep -c 'result="Failed"' unity/Logs/f3-full-final.xml    # 预期 0
grep -c "error CS" unity/Logs/f3-full-final.log           # 预期 0
```

### 7.2 修复轮的三条发现(须带回 story 收口)

1. **`HarvestOrchestrator` 全仓零生产调用点** —— 除自身与测试件外无一处引用
   (`grep -rn "HarvestOrchestrator\|IDropWriter\|IGrowthEmitter" --include=*.cs unity/Assets/`)。
2. **`Novelty` 无生产者** —— `PrescriptionPorts.cs:152` 自陈「novelty 仍取 `Normal`」;
   照抄则采集的「首次 ×3.0」**永不触发**,且现在 B-1 补了域界守卫后**更隐蔽**:
   守卫通过,但传进来的值恒为 `Normal`。
3. **本件不在 sprint-20 计划内** —— `production/sprint-status.yaml` 只有 S20-1..S20-7,
   无 `foraging-003` 条目;sprint 计划文档亦无。属计划外插入工作。

---

## 八、未及核查项(如实登记,禁借绿)

1. **未重跑测试** —— 全量回归 `5213/5178/0/35` 与「17/17 绿」均**引自现有 XML**
   (`unity/Logs/f3-full.xml` 与 impl 证据 `production/qa/evidence/impl-foraging-003-2026-10-26.md`),
   本评审未启动新的测试轮。数字本身可复算(见 N-12),但其**当日有效性**未复验。
2. **证伪轮 9/9 未重放** —— impl 证据 §四 记载「首轮 7/8(M5 假绿)→ 修后 9/9」,
   本评审**未独立执行任何变异**。⇒ 「9/9 转红」这一事实**本件不背书**,只如实转述其存在。
3. **M5 修复的两条断言未独立变异复验** —— 即
   `ParamName == "r"`(`:459`)与 `CountingIdAuthority.CallCount == 0`(`:463`)。
   **静态分析结论**(供修复轮参考,非证明):`CallCount == 0` 是强判据 —— 它不依赖参数名,
   只依赖「前置校验在发号之前」这一真实次序,变异 `Validate` 的任一守卫即可打红;
   `ParamName == "r"` 是**弱判据**(`nameof(r)` 是实现细节,把参数改名即失效),它只是
   把「异常来自哪一层」变得可读。⇒ **两条中只有一条真正钉住属性**,另一条是诊断性断言。
   这不改变「M5 已修」的结论,但**不能让 `ParamName == "r"` 被后来的复制者当作守卫范式**。
4. **引擎专家评审未 spawn** —— 按 Phase 7 应由 `unity-specialist` 并行复核。本轮受轮次预算限制
   未执行。**影响评估 = LOW**:被评件住 `noEngineReferences: true` 的程序集,零引擎 API 面
   (N-1 已实测),引擎专家的增量价值仅剩「C# 语言惯例」,已由 N-2..N-5 覆盖。
5. **PlayMode 集成面未覆盖** —— 与 W-4 同源:story 要求的 PlayMode 载体不存在,
   故「从空流重放等值」(story `§Implementation Notes` #6 指定的最小确定性回归面)**本件零证据**。

---

## 九、正面观察(必须保留 —— 本件有多处值得作为后续范本)

1. **⚠️ 记分区写得极好**。`HarvestOrchestrator.cs:24-32` 把三条 AC 的达成度**逐条分档**
   (✅ / ⚠️ 不勾 / ⚠️ 半边),并明写禁区「禁把『形状已备』读成『三连发能跑』」。
   这直接对冲了本项目反复出现的假绿形态之一 —— **用一句自述顶替一条 AC**
   (承 `coding-standards.md` §「某件有门」的立规缘由)。建议作为 17 后续 story 的模板。

2. **`test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot`(`:344-373`)是反向夹具的范本**。
   它不测「能力」,而是**钉住「今天做不到什么」**,并把「换代时会红」写成**期望行为**
   而非故障。这是把「未接线」从一句注释变成一个**会被触发的提醒**。

3. **`:115` 那条「本组唯一走生产适配器」的注释,准确诊断了自己这一组夹具的边界**:
   > 「其余 05b 夹具注入 `SpyGrowthEmitter` —— 它们只证『编排器**请求**了成长』,
   > **不证**『`SkillGrown` 真的进流』。两者之间隔着 `SkillGrownEmitterAdapter`(生产类),
   > 若没人跑它,『成长已落流』就是一句**从未被执行过的自述**。」

   这与 `coding-standards.md:§某件有门` 立规缘由里那句「用一句自述顶替了一个从未存在的门」
   是同一条洞察,而且是在**写代码时**自发做出的。

4. **`test_ac1705_simAssembly_hasExactlyOneItemInstanceIdCtorSite`(`:227-302`)的
   「上下界双向断言」形态**:

   - **下界** `:287-289`:`sites` 为空 ⇒ 红(「拒以空集冒充绿」);
   - **上界** `:299-301`:白名单条目零命中 ⇒ 红(「白名单是死的」)。

   这是本项目 Cecil 类扫描夹具里少见的**双向自检**;且 `:236-237` 的注释把
   「struct 的构造走 `call .ctor` 而非 `newobj`」这个曾把 AC-11-22 坑成假绿的陷阱写在了
   就近位置。**建议提炼为工具层范本**(当前散落在 `AC-11-22` / `AC-3-B3` / 本条三处)。

5. **`test_ac1705_harvestOrchestratorType_hasNoLocalCounter`(`:180-200`)用反射而非 grep**
   判「无可变字段」——
   > 「判据形态用反射而非 grep:改字段名逃不掉。」

   正确的判据选择。同类:`test_ac1705_simAssembly_hasExactlyOneIdAuthorityImplementation`
   (`:203-224`)明写它**不证**什么(「不证『没有别处构造 `ItemInstanceId`』——
   后者在 C# 里无法用反射轻量判定」),把判据边界交还给下一条夹具。**「声明自己能证什么、
   不能证什么」是本案最强的可信度信号。**

6. **`SpyGrowthEmitter` 的 doc**(`:506-511`)主动**拒绝**记录 `InstanceId` / `PatientId`,
   理由 = 「断言寄存器里的常量 —— 恒真且不测任何东西(自指判据的变体)」。
   在**写夹具时**就识别出自指判据并主动绕开,这在仓内不多见。

7. **`Validate` 的异常消息带语义**(`"qty ≥ 1(F-17-2 结构保证;0 或负 = 调用方算错)"`)——
   把「为什么这是编程错误而不是业务失败」写进消息,读者不必回查 GDD。

---

## 十、ADR 合规汇总

| ADR | 相关条款 | 结果 |
|---|---|---|
| **ADR-005** | 主机唯一 `Append` · 病史流唯一真源 | ✅ 全部 `Append` 经注入的 `IEventSink`;无旁路 |
| **ADR-007 §四** | `PatientId.None` 哨兵,不污染 `max(patient_id)` | ✅ `:118` + `ResourceHarvestWriter` |
| **ADR-009 §二/§三/§六 + Amendment K** | 三条世界流事件 · `gather_seq` 计源 = `ResourceHarvested` · 有界性 | ✅ 计源正确(Amendment K ④);⚠️ **事件序** 与 story 不符 ⇒ **B-2** |
| **ADR-009 §七 Guidelines 4** | 写者 = 17 | ✅ `ResourceHarvested` 由 17 直写 |
| **ADR-024 §①** | 真源 = `entities.yaml`;不一致以 registry 为准 | ✅ 实现照 registry 取 20(见 W-1) |
| **ADR-029 §③** | 载荷唯一编码路径 = `IPayloadEncoder` | ✅ 零 `new PayloadRef(` |
| **ADR-006** | 定点边界 · 单一舍入 · 禁浮点入载荷 | ✅ 无浮点、无 `Math.Round`;`Fix` 仅透传 |
| **ADR-016 §一** | 三源不变量(输入 ∈ {事件流, 烘焙数据, 二者纯函数}) | ✅ `HarvestRequest` 全整数域,无墙钟 / 无 RNG |

**Architectural Violation:0** —— B-1 / B-2 均为**实现与自述/规格不符**,不涉及禁用的模式。
**ADR Drift:1(B-2)** —— 偏离 story 明定的裁决,但未使用任何被否决的形态。
**Minor Deviation:1(M-1)** —— doc 描述超前于实现。

---

## 十一、结论

### 11.1 原判定(评审轮当日,2026-10-26 前半)

| 项 | 结果 |
|---|---|
| **BLOCKING** | **2**(B-1 原子性自述为假 · B-2 事件序与 story 相反) |
| WARNING | 4(W-1 缺裁决锚 · W-2 三套成长入口 · W-3 novelty 无生产者 · W-4 证据路径不符) |
| MINOR | 3(M-1 doc 超前 · M-2 边界无夹具 · M-3 AC 未回填) |
| 通过项 | 13 条(N-1..N-13,逐条可复算) |
| 未及核查 | 5 条(§八,如实登记) |
| **原裁定** | **CHANGES REQUIRED** |

### 11.2 修复轮后(同日后半轮)

| 项 | 结果 |
|---|---|
| **BLOCKING** | **0**(B-1 / B-2 均已修,且**各自经独立变异证伪**) |
| MINOR 已修 | M-2(与 B-1 同源,3/3 分支覆盖) |
| **仍开放** | WARNING **4**(W-1..W-4)· MINOR **2**(M-1 / M-3) —— **均未修** |
| 夹具 | 21/21 绿 |
| 全量回归 | **5217 / 5182 / 0 / 35**(基线 5213/5178 ⇒ **+4** 精确等于新增用例) |
| Falsifiability | M10 / M11 / M12 / M13 **四条变异各自命中且只红靶测** |
| **现行裁定** | **BLOCKING 清零**(可进 story 收口)· **WARNING/MINOR 6 条仍开放** |

⚠️ **BLOCKING 清零 ≠ 本件可用。** §7.2 三条发现(零调用点 / novelty 无生产者 / 不在 sprint 计划内)
讲的是**同一件事**:当前交付的是「形状完备的实现 + 自足的夹具」,不是「跑得起来的采集链路」。
story 收口文案须照此写,不得写成「采集功能已可用」。

⚠️ **B-1 与 B-2 未修前不得判 APPROVED。** 两者的共同性质值得记一笔:
**它们都不是「功能没做」,而是「做对了却没被证明,而自述把它说成了已证明」。**
B-1 的自述是「写了一半在结构上不可达」,B-2 的自述是「故它必须先于 ③ ④ 执行」——
两句都是**从错前提推出来的正确语气**。这提示修复轮的两条纪律:

1. **凡「结构上不可达 / 不可能发生」类自述,须给出该不可达性的具体判据**(这里是
   「`Validate` 的守卫集 ⊇ 全部下游守卫集」),否则它只能是愿望;
2. **凡「故必须先于 X」类理据,须指到被依赖的那一行** —— B-2 的理据若真去读一眼
   `CountHarvestsOfNode` 的首行 `if (e.Kind != EventKind.ResourceHarvested) continue;`,
   当场就会自证不成立。

---

**评审人**:`lead-programmer`(子代理·只读)
**落盘**:`production/qa/evidence/review-foraging-003-2026-10-26.md`
