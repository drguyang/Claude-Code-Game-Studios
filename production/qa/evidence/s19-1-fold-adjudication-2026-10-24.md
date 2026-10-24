# S19-1 折叠接线 · 三家专家裁定合成原件

**日期**:2026-10-24
**对象**:`S19-1` 的判据 ④(折叠在 `Checkpoint` 内生效)在现行装配结构下的可兑现性
**裁定人**:用户(2026-10-24,两项:① 字节半**推迟并入 `TD-persist-03`**;② 写本原件 + 改判据)
**合成方**:编排会话(基于三份专家草稿 + 12 处自核)

**专家来源**(三份草稿已合并入本件,原草稿随后删除,不留两份并行分析段):
- `technical-director` — D-01…D-17
- `unity-specialist` — Q1-1…Q5-1
- `lead-programmer` — Q1-甲…未核-3

---

## 一、结论摘要

| # | 结论 | 处置 |
|---|---|---|
| **C-1** | 判据 ④ 的现措辞「该行在 `Sim.Codec/SaveService.cs` 内」**字面不可兑现,且原因是结构性的** | **作废该措辞**,改判行为判据(见 §四) |
| **C-2** | 乙案(给 `Sim.Codec` 加 `Sim` 引用)**否决**,但它**不撞任何机械门** —— 是**无声漂移** | 记入本件 §三,作为后续同类裁定的判例 |
| **C-3** | 判据 ④ 的**字节半**今天**无落点**(不是「没写」,是「格式缺一段」) | **用户裁定:推迟,并入 `TD-persist-03`**;本 sprint 标 `NOT-RUN` |
| **C-4** | 接线形态取 **lead-programmer 甲-a**(`Func<SaveSlot, byte[]> compose` 注入) | 不取 TD 戊案、不取 unity 第五案(见 §二) |
| **C-5** | 判据 ③ / ⑥ / ⑦ 三处措辞须同批订正 | 已落 `sprint-19.md` + `qa-plan-sprint-19-2026-10-24.md` |

---

## 二、接线形态:三家给了三个「第五案」,裁定取甲-a

### 三家分歧

| 案 | 提出方 | 形态 | 否决 / 采纳理由 |
|---|---|---|---|
| **戊案** | technical-director | `HistoryFolder` + `FoldRow` + `FoldResult` **全搬 `Sim.Codec`**;`SaveService` ctor 收 `Func<IReadOnlyList<SimEvent>> historySource` + `IPayloadDecoder`,`Checkpoint` 内部调 `Fold` | **否**。① 须 ADR-025 §① 窄修订,而 `adr-025:112` **已明文记过相反结论**(「折叠动作与折叠行,**不进本表** —— 它们是 `Sim` 内部类型」)⇒ 不是「未授权」,是**已有相反记录**;② 仍需另解病史流来源(`EventStream` 住 `Sim`),搬完类型也搬不完数据源;③ 搬运面 = 3 类型 + 全库 23 处测试调用点 |
| **第五案** | unity-specialist | 只搬 `FoldRow` + `FoldResult` + 一个具名委托到 `Sim.Contracts`;`HistoryFolder` 留 `Sim`;由 `Gameplay.Boot` 装配 | **否**。同样要动 ADR-025;且把**数据 DTO** 塞进契约集,`Sim.Contracts` 从「纯契约」变成承载数据结构 ⇒ 比甲-a 多两处搬动、多一次契约面扩张 |
| **甲-a** | lead-programmer | `Func<SaveSlot, byte[]> compose` 注入;折叠 + 组版 + 编码**全在组合根闭包内**;`SaveService` 只认「拿到一坨字节」 | ✅ **取此案** |

### 甲-a 成立的根据

`technical-director` 的 D-08 论证「`Func<…, FoldResult>` 在 `Sim.Codec` 内**不可命名**」——
**这条是对的**,因为 `FoldResult` / `FoldRow` 住 `Sim`,而 `Sim.Codec` 看不见 `Sim`。

但它**只推翻了「委托返回 `FoldResult`」这一种委托**,没有推翻委托本身:
**委托返回 `byte[]`,`byte[]` 是 BCL**。折叠行怎么变成字节,是闭包内部的事,
`SaveService` 永远不必命名 `FoldRow`。

三条独立优势:

1. **零 ADR 修订、零装配边、零 `Sim.Contracts` 新增成员** —— 严格守住 `sprint-19.md` ⑦ 段约束 ①;
2. 把「谁同时看得见 `Sim` 和 `Sim.Codec`」**一次性收敛到 `Gameplay.Boot`**
   (`CompositionRoot.cs:186` 已在做同款事:构造 `PayloadEncoder` / `PayloadDecoder`),不再每加一个消费者就重开一轮;
3. `Checkpoint` 变成 **门 → 组版 → 原子写** 三段,单一职责,比戊案干净。

### 甲-a 的落地形状(实施期按此写)

```csharp
// Sim.Codec/SaveService.cs —— 构造形参 3 → 6
public SaveService(
    string basePath,
    ISaveCodec codec,
    IFoldedPredicate folded = null,                    // 第 3 位不动 —— 保住 ps[2] 类型断言
    Func<bool> narrativeContext = null,                // ⑧ 段裁定(PRE-S19-A)
    Action<IReadOnlyList<SimEvent>> replaySink = null, // ⑦ 段裁定(PRE-S19-D)
    Func<SaveSlot, byte[]> compose = null)             // 本件新增:字节数据源
```

`Checkpoint` 体内:

```
Gating(叙事上下文门,fail-closed)
  → SlotProjections.Writable / SaveSlotLedger.EnsureWritable   // (b) 覆盖既有槽的拒
  → _compose(slot)                                             // ← 委托调用点
  → SaveBytes(slot, bytes)
```

`compose` 为 `null` ⇒ `Checkpoint` **抛具名异常**(禁静默跳过)。

`Gameplay.Boot/CompositionRoot.cs`(在 `:186` 附近)闭包内:
`HistoryFolder.Fold` 逐病人 → `FoldRowCodec.Write`(六原始类型)→ `SaveBodyCodec.Compose` → `SaveService.AddChecksum`。

> ⚠️ **闭包内那三个编码器今天都不存在**(`FoldRowCodec` / `SaveBodyCodec` / 存档体组版器)
> ⇒ 这是 §三 的缺口,归 `TD-persist-03`。本件只落**委托形状**,闭包体在字节格式裁定后才可写完。

---

## 三、判据 ④ 的字节半:今天无落点(实测)

**这不是「实现偷懒」,是「存档体字节布局从未被造出来」。**

| # | 事实 | 命令 / 出处 |
|---|---|---|
| **B-1** | `SaveHeader` 七字段**无折叠段偏移**(只有 `SnapshotOffset`) | `unity/Assets/Sim.Codec/SaveHeader.cs:25-31` |
| **B-2** | `Sim.Codec` 内**零 `FoldRow` 编码器** | `grep -rn "FoldRow" unity/Assets/Sim.Codec` ⇒ **0**;`ls unity/Assets/Sim.Codec/` 无 `FoldCodec.cs` |
| **B-3** | 全库**无任何存档体生产器** | `grep -rln "SaveCodec\|SerializeStream\|SaveBody\|BuildSave" unity/Assets/Gameplay.Boot/` ⇒ **0** |
| **B-4** | `SimEventCodec.Decode` **只解单事件头部**,无列表容器编解码器 | `unity/Assets/Sim.Codec/SimEventCodec.cs:48`;`grep -rn "IReadOnlyList<SimEvent>" unity/Assets/Sim.Codec` ⇒ **0** |
| **B-5** | `CodecWriter` 的数组写只有**定长** `WriteFieldArrayInt32/Int64` | `unity/Assets/Sim.Codec/CodecWriter.cs:149/:158` |
| **B-6** | `SaveHeader` **无读回方法**(只有构造器 = 写形) | `unity/Assets/Sim.Codec/SaveHeader.cs:33`;`grep -rn "SaveHeader" unity/Assets --include=*.cs \| grep -v Tests/` ⇒ 6 行,零读回 |

**与既有债的关系**:B-3 + ADR-010 §一 的「头部 + 三流」布局 vs 现实现的单 `List<SimEvent>`
(`Sim/EventStream.cs:38`)—— **是 `TD-persist-03` 实测 ③ 登记的同一个洞**。

⇒ **用户裁定(2026-10-24):本件不造字节格式,判据 ④ 的字节半标 `NOT-RUN`,
并入 `TD-persist-03` 一并裁**(该条的解除动作 ③ 已登记「三流字节布局须走 `/architecture-decision`」,
本条为其**新增的一个段**)。

---

## 四、判据订正(逐条,已落 `sprint-19.md` 与 QA plan)

### ④ 位置半 —— 作废原措辞

**原文(作废)**:
> 「`HistoryFolder.Fold` 的调用点须出现于**非 `Tests/`** 路径 … 且该行在 `Sim.Codec/SaveService.cs` 内」

**订正后**:

```
④ 折叠在 Checkpoint 内生效 ⇒ 折叠动作有生产调用链(非 Tests/),两条同时成立:
   (i)  grep -rn "HistoryFolder\.Fold" unity/Assets --include=*.cs | grep -v "Tests/"
        ⇒ ≥1 行,且该行**不在 `Sim.Codec/` 内**(必在 `Sim/` 或装配根 `Gameplay.Boot/`);
   (ii) `SaveService.cs` 内 `_compose(` 调用点落在 `Checkpoint` 方法体内,
        且 `Checkpoint` 的组版分支**由构造注入驱动** —— 未注入时 fail-closed 抛具名异常。
   ⚠️ 原措辞「该行在 `Sim.Codec/SaveService.cs` 内」作废:Sim 与 Sim.Codec 的 asmdef
   references 互不相含(both = {Sim.Contracts}),该行物理上不可能落在 SaveService.cs;
   要求它成立 = 要求违反 b2 门(AssemblyGates.cs:214 的 `if (asmName == "Sim")` 守卫
   使 Codec 只吃引擎前缀检查,故该违例会**静默通过所有机械门**)。
```

### ④ 字节半 —— 标 `NOT-RUN`

**订正后**:

```
④ 字节半(`Checkpoint` 产出的字节里折叠行出现):**NOT-RUN**(2026-10-24 用户裁定)
   前置缺口 = 存档体字节布局不存在(见 adjudication 原件 §三 B-1…B-6);
   归 `TD-persist-03`。**不得标绿,不得以「折叠在生产调用链上生效」顶替。**
```

### ③ 纯函数 —— 限定判据对象

**原文**:
> 「反射查**该类型**无可变实例字段」

**订正后**:

```
③ 判据对象 = `Sim.Codec/SlotProjections`(静态类,零实例字段)+ `SlotContext`
   (readonly struct,全 readonly 字段);反射断言**只扫这两个类型**。
   状态宿主 `SaveSlotLedger`(持 Locked 集 / next_writable_seq)**不在本判据面内**,
   另以 (a)(b)(c)(d) 四例的状态迁移夹具覆盖。各配独立灰置通道(不得共用一个 bool)。
```

**理由**:`Writable` 公式里的 `next_writable_seq` 与 `Locked(slot)` 按 GDD
`save-slot-ui.md:212-214` **归 7a 且只进不退**(`persistence-service.md:203-206`)——
是**跨调用累积的状态**,不可能是纯函数。但判据 ③ 的对象是**两个函数**,不是它们读的那个量。
⇒ 拆成 `SlotProjections`(纯)+ `SaveSlotLedger`(有状态)即无矛盾。

### ⑥ 形参算术 —— 3 → 5 订正为 3 → 6

**原文**:
> 「构造形参 **3 → 5**」

**订正后**:

```
⑥ 构造形参 3 → 6,顺序:
   (basePath, codec, folded = null, narrativeContext = null, replaySink = null, compose = null)
   `folded` 保持第 3 位 ⇒ folded_predicate_test.cs:590 的**类型断言 ps[2] 仍成立,只改计数 3→6**;
   `:588`(ctors.Length)不动。
```

**原算术为何不够**:「3 → 5」只覆盖了「两门 + 重放」,**漏了 `Checkpoint` 的数据源** ——
而 ④ 与 ② 都强制 `Checkpoint` 必须产出真字节。加甲-a 的 `compose` 后实为 6。

### ⑦ 头部 / 列表 —— 补两处「须新写」

**订正后追加**:

```
⑦ 补两条实测:
   (i)  `SaveHeader` **无读回方法**(只有构造器)⇒ 头部解析须新写 `SaveHeaderCodec.Read`
        (落 `Sim.Codec`,与 `SaveHeader` 同装配);
   (ii) **无列表容器编解码器** —— `SimEventCodec.Decode` 只解单事件头部
        ⇒ 「字节 → IReadOnlyList<SimEvent>」须新写;`SaveCodec.WriteEvent/ReadEvent` 逐条 + 外部长度前缀是唯一在盘基元。
   残余(归 TD-persist-03,不在本件修):blob 池段在 ADR-010 §一 布局中**无位置**
   ⇒ 交出的列表携带悬空 `PayloadRef`,任何消费方都解不出载荷。
```

---

## 五、乙案为何是「无声漂移」(判例登记)

`Sim.Codec` 加 `"Sim"` 引用后,**不会撞任何机械门**:

| 门 | 位置 | 为何不红 |
|---|---|---|
| **b2 门 A** | `AssemblyGates.cs:192-222` | `:205-213` 只查引擎程序集前缀(`UnityEngine`/`UnityEditor`/`Unity.*`);`:214` 的 `if (asmName == "Sim")` **守卫**了整个 BCL∪`{Sim.Contracts}` 白名单块 ⇒ 白名单**只对 `Sim` 生效**,`Sim.Codec` 不吃 |
| **b3 封闭性** | `AssemblyGates.cs:168-187` | 只比 asmdef 的 **`name` 字段集合**(`Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"")`),**从不读 `references`** |
| **AC-10-02 输入边界** | `EmergencyIntegerGates.cs:107-112` | 扫的是**装配 "3"**(输入系统)的引用面,查它**不得**引 `Sim`/`Sim.Contracts` —— 与 `Sim.Codec` 的引用集无关 |
| **全仓测试** | `grep -rn "Sim.Codec.asmdef\|Codec.asmdef" unity/Assets/Tests` | **0 命中** —— 无任何测试断言 `Sim.Codec` 的引用集 |

⇒ 乙案的唯一拦截是 **ADR-025 §① 的文本**,而非执行体。
**这是本项目「无声漂移」的标准形态:改动能过全部门,但违一份 Accepted ADR。**
后续同类裁定须以此为例:门绿 ≠ ADR 合规。

---

## 六、本件**不交**什么(禁借绿)

| 项 | 状态 | 归 |
|---|---|---|
| 折叠行的字节产出(④ 字节半) | **NOT-RUN** | `TD-persist-03`(用户 2026-10-24 裁定) |
| `FoldRowCodec` / `SaveBodyCodec` / 存档体组版器 | 不存在 | `TD-persist-03` |
| `SaveHeaderCodec.Read`(头部读回) | 不存在,须新写 | 本件(`Load` 的头部解析段) |
| 列表级事件编解码器 | 不存在,须新写 | 本件(`Load` 的 `Replaying` 前半) |
| `SlotProjections` / `SlotContext` / `SaveSlotLedger` | 不存在,须新写 | 本件 |
| 叙事上下文真实驱动源 | 零落点 | `TD-persist-04`(归 8 / 37 / 52) |
| 真重放(列表 → sim 状态) | 零实现 | `TD-persist-03` |

---

## 七、自核清单(12 处,逐条可跑)

| # | 核了什么 | 命令 / 落点 | 结果 |
|---|---|---|---|
| 1 | 三装配引用集 | `grep -n '"references"' unity/Assets/{Sim,Sim.Codec,Sim.Contracts}/*.asmdef` | `Sim`/`Sim.Codec` = `["Sim.Contracts"]`;`Sim.Contracts` = `[]` |
| 2 | b2 门守卫 | `sed -n '200,225p' unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | `:214` `if (asmName == "Sim")` 确认 |
| 3 | b3 门只读 name | `AssemblyGates.cs:168-187` | 确认 |
| 4 | `_folded` 零消费 | `grep -n "_folded" unity/Assets/Sim.Codec/SaveService.cs` | 仅 `:30` 声明 / `:51` 赋值 / `:59` 暴露 |
| 5 | `SaveHeader` 无读回 | `grep -rn "SaveHeader" unity/Assets --include=*.cs \| grep -v Tests/` | 6 行,零读回 |
| 6 | 无 `FoldRow` 编码器 | `grep -rn "FoldRow" unity/Assets/Sim.Codec` | **0** |
| 7 | 反射断言精确行 | `sed -n '583,608p' unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs` | `:588` ctors.Length · `:590` ps.Length=3 · `:591` ps[2] 类型 · `:606` CollectionAssert |
| 8 | 生产零构造点 | `grep -rn "new SaveService(\|new FoldedPredicate(" unity/Assets --include=*.cs \| grep -v Tests/` | 双双 **0** |
| 9 | 测试侧构造点 | `grep -rn "new SaveService(" unity/Assets/Tests` | `save_service_test.cs` **3 处**(全部两参形态 ⇒ 加可选形参不破) |
| 10 | Boot 双边可见 | `cat unity/Assets/Gameplay.Boot/Gameplay.Boot.asmdef` | references 含 `Sim` + `Sim.Codec` + `Sim.Contracts` |
| 11 | `HistoryFolder` 无折全部入口 | `grep -n "public static" unity/Assets/Sim/HistoryFolder.cs` | 只有 `Fold(history, patientId, folded, decoder)` 单病人 |
| 12 | `EmergencyIntegerGates` 扫的是装配 3 | `sed -n '75,112p' unity/Assets/Editor.Tools.Gates/EmergencyIntegerGates.cs` | `AC-10-02 —— 3 侧零 Judge/JudgeResult/SimEvent 引用` ⇒ 与 `Sim.Codec` 无关 |

**一处我方早期误读(留痕)**:`technical-director` D-08 的「甲案不可实现」结论方向正确,
但**只覆盖了返回 `FoldResult` 的委托形态**;我最初据此把「委托注入」整类判死,是**越权推广**。
甲-a 的 `Func<SaveSlot, byte[]>` 不受该结论约束。

---

## 八、三份专家草稿的处置

按 `code-review` skill §Phase 7b ③ 增量落盘纪律:**草稿合入本件后删除,不留两份并行分析段**。

| 草稿 | 行数 | 处置 |
|---|---|---|
| `production/qa/evidence/tmp-s19-1-fold-td.md` | 21 | 合入 §二 / §五 / §七 → **删除** |
| `production/qa/evidence/tmp-s19-1-fold-unity.md` | 15 | 合入 §二 / §五 → **删除** |
| `production/qa/evidence/tmp-s19-1-fold-lead.md` | 27 | 合入 §二 / §四 / §三 → **删除** |

**未核项承继**(三份草稿共 9 条,均**未跑**,禁借绿):
ADR-010 §一 布局图全文的分册态段 / 尾部段位约定 · b5 门 `AudioForbiddenContractTypes` 十项行号与丙案误伤面 ·
`CodecWriter` 列表级写入的充分性 · `FoldedPredicate` 三个 `Func` 在装配根的可得性 ·
`SaveSlotLedger` 锁生命周期与 `Verifying` 后置位的时序相容性 · `FoldRowCodec` 六元组与 ADR-010 §二 六字段的字节序/tag 对应。
