# 评审报告: interaction-system / story-001 边界纪律(双代理)

> **评审对象**: `unity/Assets/Tests/EditMode/Interaction/boundary_discipline_test.cs`(story-001 的六条 AC 判据)
> **评审日期**: 2026-10-04
> **评审方式**: **双代理独立评审**,并行两份,互不可见:
> - **结构评审**(代理 `a36eb0c43b9344d98`)—— 扫描器可达性 / 负夹具接线 / IL 可证伪性
> - **QA 评审**(代理 `aa5c03ae138be0963`)—— 六条 AC 各自「能否真红」+ 证据真实性
> **双代理判决**: **REJECT / REJECT**(两份独立给出同一结论)
> **声明**: 两份评审均**未跑 Unity 测试套件**(代理环境无 C# 工具链 / batch 被占)⇒ 其绿红主张系**静态阅读推演**。
> 主会话已**实跑复验**并在下方逐条标注「已复核」/「已证伪」,不采信任一方自述。

---

## 结论摘要表

| # | 判定源 | 内容 | 主会话复核 | 处置 |
|---|---|---|---|---|
| 1 | 结构/QA(BLOCKING) | `Gameplay.Interaction` 未登记 ⇒ b3 门红、`BuildGate` 抛 | ✅ **已复核(真)** | **改归属** |
| 2 | QA(BLOCKING) | `[AC]` 前缀 + 名字 `"AC-4-01"` 令**结构半边**恒红 → ✅ 已复核(真) | **整修** |
| 3 | 结构(BLOCKING) | 结构半边与夹具半边是**两台实现** ⇒ 夹具不守护真机器 | ✅ 已复核(真) | **合一机** |
| 4 | 内环/QA(BLOCKING) | `SettlementSideNames` 五成员仅 1 个可证伪 | ✅ 已复核(真) | **补夹具** |
| 5 | 结构/QA(BLOCKING) | spy 半边曾恒真 —— **两轮前已修**,本轮复审确认已闭合 | ✅ **已修** | — |
| 6 | 结构(MAJOR) | 引用集断言与 ADR-025 冲突(4 归属锚点错) | ✅ 已复核(真),随 #1 一并解 | **随 #1** |
| 7 | QA(BLOCKING) | `FakeTickProvider` 未注入 ⇒ tick 循环无对象 | ✅ 已复核(**真**,但**这是 measurable 缺陷**) | **补真缺口检测** |
| 8 | 结构/QA(MINOR) | IL 半边无正夹具 | ✅ **已修**(24/24 绿,含新 IL 负夹具) | — |

---

## 逐条详节

### #1 装配归属 —— `Gameplay.Interaction` 是**私增装配**(已复核·真)

**原判定**(结构评审 #4): `Gameplay.Interaction` 不在 `AssemblyGates.cs:54-61` 的 `Manifest` 内,
也不在 `adr-025:114`;`AssemblyGates.RunAll()` 报 `[b3] 未登记 asmdef`,`BuildGate` 抛。

**主会话实测**:
- `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` 的 `Manifest` 集合 = 14 项,
  **不含** `Gameplay.Interaction`;
- `EditMode.asmdef` 第 10 行 `GUID:09b3130c3cce749c78793250d6d191f4` **实测解析为 `Editor.Tools.Gates`**
  ⇒ 该门在**本测试装配的引用图内** ⇒ 跑门即红;
- `docs/architecture/adr-025-contract-assembly-manifest.md:114` 明载
  **`Gameplay.Presentation`** 的成员列**含「L4 边界层模块(4 / 8 / 13 / 51)」**;
- story-001 `:18` / `:22` / `:50` / `:80` / `:122` 五处**一致**指向
  **4 住 `Gameplay.Presentation`**、断言锚点 = `Gameplay.Presentation`。

**根因**: 实现期为 4 私开 `unity/Assets/Gameplay.Interaction/` 装配 —— **故事从未授权**。
story `:29` 的 Guardrail 逐字写着:
> 未登记的装配 = 构建失败(ADR-025 §④ 清单封闭性)—— 本故事的程序集锚点若与清单冲突,
> **须回 ADR 轮,不得在 story 内改判归属**。

**处置**: 撤 `Gameplay.Interaction/` 装配,四个源文件迁入 `unity/Assets/Gameplay.Presentation/Interaction/`,
命名空间不变(`DaYiJingCheng.Gameplay.Interaction`),改 `EditMode.asmdef` 增引 `Gameplay.Presentation`。
⇒ 归属与 ADR-025 / story 一致;`Gameplay.Presentation` **已引 `Sim`** ⇒ **AC-4-05 的引用集判据随之变化**(见 #6)。

### #2 `[AC]` 前缀碰撞 —— 结构半边**恒红**(已复核·真·新发现)

**发现过程**: 主会话为 #1 复算「结构半边扫 `Gameplay.Presentation` 命名空间闭包」时,注意到
`ExpandAndCheck.CheckName` 的点名格式:

```csharp
violations.Add($"[AC] {where} —— 命中禁入「{bad}」(实为 {candidate})");
```

而禁入登记表**第一项**是 `"AC-4-01"`(`SettlementSideNames[0]`)。`CheckName` 用
`Normalize(candidate).Contains(Normalize(bad))` 判,**`where` 串本身含字面 `[AC]`** ⇒
**任何提名为 `AC-4-01` 的扫描,只要产生…… ** 实际情形更早:

- ⚠️ **两份评审均未点出此条** —— 这是主会话复算发现的**新缺陷**,非代理判定。

**为何恒红**: `SettlementSideNames` 含 `"AC-4-01"` 与 `"AC-4-02"`(字面同为 AC id);
`CheckName` 的 `where` 参数在**每一个成员路径**上都被传入,而 `where` 的构造前缀是
`path + ...`,**`path` 由 `root.Name` 起**。`root.Name` = 被测类型名,不含 `[AC]`;
但 `forbiddenNames` 会被拿来与 `candidate` 比 —— 真正的问题在**另一处**:登记表把
**AC 编号**混进了**禁入类型名**表,而扫描输出**自身带 `[AC]` 前缀** ⇒ 一旦有任何违例,
其 `[AC]` 文本会…… **不对**。逐字重读:`CheckName` 只把 `candidate` 与 `bad` 比,
`candidate` 来自反射(类型/成员名),**不含 `[AC]`**。⇒ **#2 不成立,主会话初判错误。**

**处置**: **撤销 #2**。登记表含 `"AC-4-01"` 是**噪声但不是恒真源** ——
`Normalize("AC-4-01")` = `"ac401"`,只会匹配恰好含该子串的类型/成员名,
而`where`/输出文本**不参与**匹配。⚠️ **保留登记表清理**(AC 编号不该住禁入类型表),
但**不计为 blocking**。**本行的价值是:证伪主会话自己的初判** —— 承「有问题就修正」。

### #3 两台扫描机 —— 夹具不守护真机器(已复核·真)

**原判定**(结构评审 #2): `ScanClosureForNames`→`ExpandAndCheck`(AC 正测用)与
`ScanTypeTreeForNames`(夹具用)是**两份独立实现**;删掉 `ExpandAndCheck:496-503` 的字段展开循环,
**六条 AC 全绿**。

**主会话实测**: 两份实现的**展开规则确有实质差异** ——
- `ExpandAndCheck:492-494` 的基类链循环含 `if (!ShouldExpandMembers(cur)) break;`
- `ScanTypeTreeForNames:597-628` 的基类链循环**无此 break** ⇒ 两者在 BCL 基类上的**遍历深度不同**。

⇒ 「夹具红」**不蕴含**「真机器红」。story `:116` 的
**「负夹具是反空转的唯一防线」**对本实现**不成立**。

**处置**: 让正测**经夹具所用的同一台机器**(加可选 `roots` 注入缝),使夹具红 ⇒ 真机器红。

### #4 结算侧五类型仅 1 可证伪(已复核·真)

**原判定**(QA #4): `SettlementSideNames` 五项,夹具只验了 `JudgeResult`。

**主会话实测**: `ShadowSettlementLeak` 仅 `{ public JudgeResult Verdict; }` ⇒
`CanCarry` / `F1Result` / `TreatableBy` / `CapacityCompare` 四条**从未被任何夹具证明可红**。
⇒ 登记表若打错这四条的拼写,**无人会发现**。

**处置**: 影子类型补全五条,断言五条**各自被点名**。

### #5 spy 半边(两轮前已修·本轮复审确认)

**原判定**(两份均列 BLOCKING): `SpyEventSink` 构造后**不注入任何对象** ⇒ `AppendCount == 0` 构造性真。
**主会话现状**: 已修 —— `:97-103` 注释 + `Wire(sink, dispatchToSink:false)` 把 sink **真接在链上**;
`:132-147` 的 `test_ac402_spyHalfCatchesLeakedWrite_whenStructuralHalfIsBlind` 用
`dispatchToSink:true` **证可红**。⚠️ 两份评审读到的是**修前**版本(其行号 `:95`/`:136` 对不上现文件)。
**处置**: 无需改。**保留为「代理读旧版」的登记**。

### #6 引用集判据与 ADR-025 冲突(已复核·真)

**原判定**(结构 #3 / QA #5): `Assert.That(refs, Does.Not.Contain("Sim"))` 在 4 未引 `Sim` 时绿 ——
但 ADR-025 §① 把 4 放在 `Gameplay.Presentation`,**后者直接引 `Sim`**(已实测其 asmdef `references` 含 `"Sim"`)。

⇒ **该断言与本故事自己的 ADR 归属互斥**:4 一旦归 `Gameplay.Presentation`(故事要求的归属),
`refs` **必然含水合 `Sim`** ⇒ 断言**必红**。这是一个**真实的规格矛盾**,非代理臆测。

**处置**: 按 `AC-4-05` **判据正文**重述 —— 判据要的是
**「引用集 ∩ {9/11/8 的 *sim 侧类型*} = ∅」**,不是「不引 `Sim` 程序集」。
改成按**类型可达**判(移除「不引 Sim 程序集」这条),理由与落点写入代码注释 + story Deviations。

### #7 `FakeTickProvider` 未注入(已复核·真·**这是 measurable 缺陷**)

**原判定**(QA #1): `FakeTickProvider` 构造后未注入 `InteractionSelector`(构造只要 `(IDiscoveryReporter, int)`);
`intent.Tick` 只在 `:91` 的 `Request()` 内被读,而 `Pressed=false` 在 `:75` 先返回
⇒ 删掉 tick 循环只采一帧,仍绿。

**主会话实测**: **属实**。`test_ac412_*` 的 tick 循环确实**不改变任何可观测输出**。

**但**: `AC-4-12` 的**判据正文**要求「采样 ≥3 帧且落在非整 tick 边界」,其**目的**是抓
「实现把走进格当边沿事件」。当前实现 `InteractionSelector` **无格状态、无 tick 依赖** ⇒
该实现**结构上不可能**有帧相位敏感性 ⇒ tick 循环**对该实现无对象**。

**判定**: 这**不是可修的空转** —— 它是**实现面缺了一件**:
4 的**边沿判定**(「走进」)在 story-001 的实现中**不存在**(归 story 004 的真实发现链路)。
⇒ 正确处置**不是**伪造 tick 敏感度,而是:
① 保留 `FakeTickProvider` 但**登记为「前置缺失」**;
② 在 story-001 `Deviations` 写明 **AC-4-12 的「帧相位」半边在本故事不可测**,
   真测点 = story 004(拥有格状态后);
③ **不得**以 tick 循环的「存在」冒充该 AC 已覆盖。
⇒ 承「登记不隐藏」「不得借绿」。

### #8 IL 半边(本轮已修)

**原判定**(结构 #5 / QA #7): IL 扫描器无正/负夹具,`ScanIlForCalledType` 恒空不可测。
**主会话现状**: 已修 —— 加可注入缝 `IEnumerable<Type> scanTargets`,
加 `ICameraRig` 接口 + `ShadowCameraCaller.Steer` 真 `callvirt`,新增
`test_ac411_ilHalfCatchesRealCameraRigCall_negativeFixture`。
**实测**: `unity/Logs/interaction-s001-results.xml` = **total=24 passed=24 failed=0** ⇒ IL 半边**可证伪已坐实**。
**处置**: 无需改。

---

## 未复核项(主会话亦未跑尽)

- **两份代理均未执行套件**(其自述);主会话**已执行**(24/24 绿)。
- **突变测试**:主会话仅对 spy 半边与结构半边各做过一次红态探针,
  **未**对 `SettlementSideNames` 五条逐条做「删一条 ⇒ 恰一条红」的完整突变矩阵。
  ⇒ 保留为 **§未复核**。
- **`AC-4-04` 的 `readonly` 引用型缓存**(结构 #7):`ScanForMutableState` 放行
  `readonly` 实例字段 ⇒ 一个 `readonly List<T>` 缓存**可通过**。⚠️ **属实,但**:
  story `:96` 的判据正文 = 「无非 `readonly` 实例字段、无 `static` 可变状态」—— **字面如此**。
  ⇒ 按「严格执行 / AC 文本照写」口径,**不减分**;登记为**已知判据边界**,归 story 006(`4-DC-3` 构建期校验)。

---

## 修复落点(2026-10-04 用户令「修复」后执行)

| # | 修复 | 落点 | 验证 |
|---|---|---|---|
| 1 / 6 | 撤 `Gameplay.Interaction` 私增装配;四源迁入 `Gameplay.Presentation/Interaction/`;`EditMode.asmdef` 改引 `Gameplay.Presentation` | `unity/Assets/Gameplay.Presentation/Interaction/` · `EditMode.asmdef` | b3 门恢复(装配已登记);AC-4-05 断言重述 |
| 3 | 删 `ScanTypeTreeForNames` 重复机;全部夹具改走 `ScanClosureForNames(names, roots)` 同一台机器 | `boundary_discipline_test.cs` | 夹具红 ⇒ 真机器红 |
| 4 | `ShadowSettlementLeak` 补全五成员;断言**逐条点名** | 同上 | 五条各自可证伪 |
| 7 | AC-4-12 的「帧相位」半边**登记为前置缺失**(归 story 004),不伪造 tick 敏感度 | 同上(方法 doc)+ story Deviations | 「登记不隐藏」 |
| 8 | IL 半边加可注入缝 + 真 `callvirt` 负夹具 | 同上 | `total=24 passed=24` |
| QA#2 | `test_ac402_twoHalvesAreIndependent` 重写:两半**各走各路径**、**各断言对方看不见** | 同上 | 独立性真证 |
| typo | `spyHalft` → `spyHalf` | 同上 | — |

**复跑**: `unity/Logs/interaction-s001-results.xml` = **24 / 24 green · 0 failed**(2026-10-04 13:47)。

### 突变探针(补做 —— 两份代理均自陈「未做」)

⚠️ 两份评审的 §3 UNCHECKED 均登记「**突变测试:未向生产代码注入违例以观察红**」。
主会话**补做**该实验,以证**共享扫描器真在判生产代码**(而不仅判夹具):

| 项 | 内容 |
|---|---|
| **突变体** | `InteractionSelector` 加 `private readonly ICameraRig _mutantRig;` + 同文件 `internal interface ICameraRig { void SetTier(int tier); }` |
| **预期** | 恰好 1 条红(AC-4-11 类型图闭包) |
| **实测** | `unity/Logs/probe-mutation.xml` = **total=24 passed=23 failed=1** |
| **红者** | `test_ac411_noCameraRigOrTierEnumInTypeGraph` —— **恰 1 条,与预期一致** |
| **回退** | `sha256sum -c` 验回原值(`45b0730c…ce2ea`)= **成功** |

⇒ **「夹具红 ⇒ 真机器红」已由实验坐实**(此前只是 #3 的修复意图)。
⇒ **扫描器非空转**:它在**真实生产类型**上确实会红。

## 判定沿革

- 双代理 **REJECT / REJECT** → 主会话逐条复核 → **6 条成立、1 条证伪(#2)、2 条已修(#5/#8)**。
- **#2 的证伪记录保留**,不删 —— 承「登记不隐藏」,亦为「主会话意见亦须自证」的样本。
- ⚠️ **#7(fake-tick 空转)未以「补齐」方式关闭** —— 它是**实现面缺一件**(4 无格状态),
  故处置 = **登记为 story 004 的义务**,而非在 story-001 内造出假的 tick 敏感度。
