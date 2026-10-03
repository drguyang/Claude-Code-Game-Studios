# QA Lead 评审 —— camera-viewpoint Story 006(跨系统义务对账件)

**日期**: 2026-10-03 · **评审席**: QA Lead · **对象**: `production/epics/camera-viewpoint/story-006-obligations-comfort.md`
**被测件**: `unity/Assets/Tests/EditMode/CameraViewpoint/camera_obligations_reconciliation_test.cs`(实测 **12** 个 `[Test]`;故事自述 11 例 —— 计数不符,见 B-6)
**方法**: 静态判据审查(测试源码 + 逐份对方 GDD + 突变推演)。**未跑 Unity** ⇒ 编译/执行结果 **未验证**;以下判决均为判据强度层面的静态结论,与该测试"跑出几个绿"无关。

---

## 判决:不应维持 Complete

本件的**全部承重主张**是"五条反向引用实测 ✅ + 突变测试坐实"。**该主张不成立**:判据是**全文 `Contains`**,不是故事自己在 Implementation Notes(:64)与 Negative fixture(:97)里写死的"§Dependencies 节内**行级**断言"。突变推演证明:删掉对方 GDD §Dependencies 里的反向引用行,**五项全部仍然绿**。故 `AC-2-22①/②④/⑤⑥` 的 "✅ 实测" 是**假绿**,Complete 无证据支撑。

---

## 一、逐问问答(用户点名 6 项)

### 1. AC-2-22 六项反向引用 —— 是真查 §Dependencies 吗?→ **否,弱**

**判据本体**(`AssertReverseReference`,:34-42):

```csharp
Assert.IsTrue(text.Contains("camera-and-viewpoint"),
    $"[{obligation}] 「{gddName}.md」须在其 §Dependencies 内**反向引用** ...");
```

`text` = **整份 GDD 全文**(`File.ReadAllText`)。断言既不定位 §Dependencies 节,也不做行级匹配,更**不检 O 编号**(O-12…O-16 一个字没查)。消息里写着"§Dependencies 内",代码里**没有任何东西约束位置** —— 断言名与断言体不符。

**突变推演(实测)** —— 对方 GDD 中 `camera-and-viewpoint` 出现在 §Dependencies **之前**的散文/注记命中数:

| 对方 GDD | §Dependencies 起始行 | 节**前**命中 | 结论 |
|---|---|---|---|
| `emergency-procedures.md` | 743 | **3** | 删 deps 行仍绿 |
| `casebook.md` | 301 | **2** | 删 deps 行仍绿 |
| `player-controller-and-movement.md` | 1041 | **1** | 删 deps 行仍绿 |
| `skeuomorphic-ui.md` | 1013 | **1** | 删 deps 行仍绿 |
| `input-system.md` | 745 | (节内 2 处) | — |

即:**【能否被「文档里随便提一句」绕过】= 能,且已被自证**。故事 `:97` 自写的 Negative fixture("临时移除某节的反向引用行 ⇒ 该项红")**从未被执行** —— 若执行,五项全绿,fixture 直接反证判据无效。`:64` 明写"只 grep 文件名全文命中不算" —— 实现**恰好就是**它禁止的那一种。

**交叉验证(顺带发现,非本件判决依据)**:对方 GDD 里确实存在的、落在 §Dependencies 内且指向本 GDD 的行(如 `casebook.md:317`「档位意图 `Casebook`/`Explore`(结清 `O-12`)」)—— 说明"账"在**内容上**大体是齐的;但**判据没在验这件事**,它验的是"文件里出现过这个词"。这是典型的**判据弱于主张**:账齐是巧合被绿覆盖,不是被判据守住。

### 2. ③ 的 `Assert.Ignore` —— 处置恰当

`test_ac222_3_levelContent_hasNoGdd_notApplicable`(:81-100)。前置 `Assert` 断言 `world-and-ecozones.md` 存在,再断言其**当前不含** `camera-and-viewpoint`(漂移绊线:若日后被引用 ⇒ 说明接收方变了 ⇒ 该复核归属),最后 `Assert.Ignore` 登记"接收方无 GDD ⇒ 判据不可执行,归 AC-2-23 相机 spike"。

- **恰当**:理由与证据链一致 —— ③ 的接收方 = "关卡内容工作 + ADR-015 §一 烘焙逻辑层几何"(GDD:769),两者确实无 GDD、无独立系统条目,故"对方 GDD §Dependencies 反向引用"这一判据形式**物理上不可执行**。`world-and-ecozones.md` 全文确实零命中(实测),绊线当前为真。用 `Assert.Ignore` 而非删除/静默通过,**正确**(story 要求"不静默记绿")。
- ⚠️ **但③自身的"义务已落"未被任何东西验** —— 它只被"登记为不可签 + 改道 EXTERNAL"。③ 侧不能算绿,故事的 Completion Notes 也已如此声明(:145)。**与主张一致,不扣分**,仅登记:③ 的真实验收 = AC-2-23 的 spike,而 AC-2-23 的 spike **不存在**(见下 §3)。

### 3. EXTERNAL(21/23)与 ADVISORY(03/24/26)—— 有无"用 Ignore 冒充已验"?→ **方向反了:AC-2-21 的 Ignore 是死代码,所以它冒充的是"已验证绿"**

**AC-2-21 = 坏(比"冒充"更糟)**。`test_ac221_external_pitchMaxAwaitsUserDecision`(:115-131)的守卫:

```csharp
bool closed = pc.Contains("OQ-1-14") && (pc.Contains("已裁") || pc.Contains("已结案"));
if (!closed) { Assert.Ignore(...); }
```

实测 `player-controller-and-movement.md`:**`OQ-1-14` 命中 8 次**、**`已裁` 命中 19 次**、**`已结案` 命中 11 次** —— 两个 `Contains` 都是全文 OR,常量**恒真** ⇒ `closed` 恒 `true` ⇒ `Ignore` **永不触发** ⇒ 本测**恒 PASS**。

- `OQ-1-14` 的行状态实为 **"⏳ …本项结案尚缺一步:取值(取值归用户)"**(`:1607`)—— **未结案**。守卫本应在此 `Ignore`,却因匹配到**邻近无关**的"已裁"(如 `:1048` 它处裁定、`:1628` 等处的其它 OQ)而静默通过。
- **后果**:EXTERNAL 义务在测试报告里显示为 **PASS(绿)**,而非 NOT-RUN/Ignored。故事 Completion Notes(:146)声称"显式登记 …**不得记为本 Epic 的绿**" —— 测试**恰恰把它记成了绿**。这正是"禁借绿"红线(story `:28` Forbidden / `:104` Negative fixture)的**反向违反**:不是"把 EXTERNAL 计入绿",是"判据恒真导致 EXTERNAL 自动绿"。
- 修正方向:守卫应锚定 **`OQ-1-14` 那一行的状态列**(行级 parser,同 §1 的修法),或直接**无条件 `Assert.Ignore`** 登记(与 AC-2-23 写法对齐)。

**AC-2-23 = 可接受**。`test_ac223_external_spikeIsSoleAcceptance`(:133-148):断言 ADR-020 存在且含 `spike`,然后**无条件** `Assert.Ignore` 登记"验收归 spike,2 侧不可签署,不计入就绪度"。如实呈现 Ignored,不冒充绿。✔
(⚠️ 但只验 "ADR 里有 spike 这个词" ≠ 验到"spike = O-13 唯一验收依据"这层内容;属登记面,可接受,登记为弱。)

**AC-2-03 = 可接受**。`test_ac203_...`(:152-167):断言 GDD 含 `AC-2-03`,再**无条件** `Assert.Ignore` 登记"须 playtest 签核,不得用机械判据替代"。✔

**AC-2-24 / AC-2-26 = 无独立登记(缺口)**。两者**没有各自的 `[Test]`**、**没有各自的 `Assert.Ignore`** —— 仅被 `test_ac226`(:169-186)顺带检字符串。故事 Completion Notes(:147)称"三条舒适度签核面已验其标 ADVISORY" —— 实际**只有 AC-2-03 被登记为 NOT-RUN**;24/26 的 NOT-RUN 状态**未在测试里登记**(只存在于故事散文中)。三个 ADVISORY 门里两个**没有可执行的"未签核"痕迹**。登记为 **B-3**。

### 4. AC-2-03/24/26 的"标 ADVISORY"判据 —— 只查字符串位置,**不够(半验)**

`test_ac226`(:178-185):

```csharp
int i = doc.IndexOf(ac, StringComparison.Ordinal);
string line = doc.Substring(i, Math.Min(200, doc.Length - i));
Assert.IsTrue(line.Contains("ADVISORY"), ...);
```

- **验到的**:AC 编号后 200 字符窗口内含 `ADVISORY` ⇒ 标签**存在**。当前三处均在此窗口内命中(实测 GDD 里三行均为 `**AC-2-xx(ADVISORY)**` 形态),**通过**。
- **没验到的**:AC 声称的**后半句** —— "**禁混入 BLOCKING 计数**"。没有任何断言去读"BLOCKING 计数"或"就绪度表",因此"不混入"这半边**零覆盖**。一个只断言"标签在场"的检查,无法支撑"未被当作 BLOCKING 处理"的结论。
- **脆弱点**:`IndexOf` 取**首次出现**。若标号在更早的交叉引用处先出现(如 §Overview 的 TOC),窗口便锚到那里 —— 位置依赖,题面巧合。当前通过,但属"靠文档排版而非靠语义"。
- 结论:**"标 ADVISORY"= 半验**;"禁混入 BLOCKING 计数"= **未验**。

### 5. VR 接口面判据 —— 明显弱于故事自述

故事 `:113-118` QA Test Case 与 `:127` Test Evidence 要求两份件:

- `vr_interface_freeze_test.cs` —— "FirstPerson 夹具**四段零推进** + **计数 0** + **无写入**";
- `tests/unit/camera/obligation_ledger_test.cs` —— 六子项行级断言。

**两份均不存在**(实测 `find`:无处)。实交件只有本对账 `camera_obligations_reconciliation_test.cs`,其中 `test_vrInterface_firstPersonEnumExists_planeChainIndependent`(:190-204)**只做三件事**:

1. `Enum.IsDefined(typeof(CameraMode), "FirstPerson")` —— 枚举成员在场 ✔
2. `typeof(ICameraRig).GetProperty("YawBasis") != null` ✔
3. 同验 `Yaw` / `Pitch` 属性存在 ✔

即:**只验"类型/成员存在"**,不验任何"结构预留"语义。故事自称的"四段零推进 / 查询计数 0 / 链状态无写入"**一条都没有**。property 存在性也是**反射执符**,与行为无关。
(实测类型确在:`Gameplay.Presentation.Camera.CameraMode` 含 `FirstPerson`(ICameraRig.cs:43),`ICameraRig` 有 `YawBasis`/`Yaw`/`Pitch`(:52/57/62),测试 asmdef 已引 `Gameplay.Presentation` —— **类型面 未验证但预计可编译**;缺口在**断言强度**,不在编译。)登记为 **B-4**。

### 6. 全组:恒真/空转判据

- **`test_ac222_obligationsHaveDeclaredReceivers`(:102-110)= 空转**。断言"camera-and-viewpoint.md 内含 O-12…O-16" —— 即**GDD 含它自己登记的义务编号**。这是**自指断言**,与"义务**接收方**已声明"({:105 注释})毫无关系:它不读任何对方 GDD。只要本 GDD 文件在(GDD 表 768-772 确有这五个编号)即恒绿。且无 null guard(`File.ReadAllText`)—— 文件缺失时会**抛异常**而非给出有意义的失败,与 `AssertReverseReference` 的 "不借绿" 设计不一致。登记为 **B-5**。
- **`test_ac222_4`(④)的"陈旧措辞检测"缺失**。故事 `:96` 要求:①侧若仍写"一帧内恒定"而非"次序不变量"⇒ 须输出**差异行**。实测:`player-controller-and-movement.md:1048/1111/1628` **仍写"帧内恒定对侧确认"**(陈旧措辞**在场**)。测试只做 `Contains("camera-and-viewpoint")` ⇒ **完全不识别该差异**,④ 却被 Completion Notes(:145)记为 "④1 ✅"。**"次序不变量"这层在两边都未被验**。登记为 **B-2**。
- **AC-2-22① 的实质义务零覆盖**。"10 在急救**开始/结束(含跳过路径)**发 `Treatment`/`Explore`" —— 测试只查 10 的 GDD 里有没有本文件名;跳过路径、开始/结束语义**均未验**(故事 `:92-95` 要求(含跳过路径)的实质) 。
- **AC-2-22② 的"39 = 唯一请求方 / 8 无残留"零覆盖**。故事 `:95` 要求"额外断言 39 = 唯一请求方且 8 无相机请求残留" —— 无任何对应断言。
- **AC-2-22⑤ 的"42 声明读哪一档"零覆盖**。故事 `:95` 要求"F6 文本含档位名或'随档位'语义" —— 测试只查文件名;`skeuomorphic-ui.md` 的 F6/Tuning(实测 :707/712/725)确实有 `FOV_v` 声明,但**测试没验它**。

---

## 二、Bug / 缺口清单

| ID | 级别 | 判据 | 症状 | 证据 |
|---|---|---|---|---|
| **B-1** | **S1(阻断签核)** | AC-2-22①②④⑤⑥ | 反向引用判据为**全文 `Contains`**,非 §Dependencies 行级;删对侧 deps 行仍绿;故事自写 Negative fixture 若跑会**反证无效** | 测试 :39;突变命中表(§1);story :64/:97 |
| **B-2** | **S1** | AC-2-22④ + ⑤ | ④"陈旧措辞差异行"未实现(1 侧现仍写"帧内恒定");⑤"42 读哪一档"未验;两者被记为 ✅ | 测试 :67-72;`player-controller…:1048/1111/1628` |
| **B-3** | S2 | AC-2-21(EXTERNAL) | 守卫**恒真**(全文字符串 OR)⇒ `Ignore` 死代码 ⇒ EXTERNAL **恒 PASS(记绿)**,违反"不得记为绿"红线 | 测试 :122-130;实测 8×/19×/11× 命中;`OQ-1-14` 仍 ⏳(:1607) |
| **B-4** | S2 | VR 接口面 | 只验枚举/property **存在**;故事要求的"四段零推进 + 计数 0 + 无写入"**无任何断言**;另 `vr_interface_freeze_test.cs`·`obligation_ledger_test.cs` **两份声明件不存在** | 测试 :190-204;story :113-118/:127;`find` 无果 |
| **B-5** | S2 | ADVISORY 24/26 | 24·26 **无独立 `[Test]`/无 NOT-RUN 登记**;仅有标签字符串检查;AC-2-03 的 Ignore 是唯一登记点 | 测试 :169-186 |
| **B-6** | S4 | 计数/自指 | 故事称"11 例"实为 **12 个 `[Test]`**;`obligationsHaveDeclaredReceivers` 为**自指空转**且无 null guard | 测试 :102-110 |
| **B-7** | S3 | 就绪度口径 | "**禁混入 BLOCKING 计数**"这半句**零断言**(只验标签在场);`IndexOf` 取首次出现 ⇒ 位置依赖 | 测试 :178-185 |

**无 S1 之外的机器行为缺陷** —— 本件不写机器代码,风险全在"账的判据"上。

---

## 三、判据强度总评

| 主张 | 判据实际强度 |
|---|---|
| AC-2-22①②④⑤⑥ "反向引用已实测 + 突变坐实" | **不成立**(全文 Contains;突变未跑且会反证) |
| AC-2-22③ "不可签,已登记" | **成立**(Ignore 恰当) |
| AC-2-21 "EXTERNAL,不计入就绪度" | **反效**(恒 PASS,被记绿) |
| AC-2-23 "EXTERNAL,注册 spike" | 成立(登记面 OK,实质弱) |
| AC-2-03 "ADVISORY,须签核" | 成立 |
| AC-2-24/26 "ADVISORY" | **半成立**(24/26 无 NOT-RUN 登记) |
| VR 接口面 "结构预留" | **未验**(仅存在性) |
| 三条"标 ADVISORY,禁混 BLOCKING" | 半验(标签在,混入未验) |

**总:** 12 个 `[Test]` 中,**恒真/自指/位置脆弱 ≈ 5 个**(`ac221`·`obligationsHaveDeclaredReceivers`·`ac226` 的半·`ac222_1/2/4/5/6` 的判据面·`vrInterface`),**恰当登记 3 个**(`ac222_3`·`ac223`·`ac203`),**真正有判别力 ≈ 0**。这不是"绿得多不多"的问题 —— 是本件的**核心证据面(五条反向引用)没有判别力**。

---

## 四、复跑/放行条件(供 producer 与 dev 引用)

1. **B-1**:`AssertReverseReference` 改为**定位对方 §Dependencies 节**后做**行级**断言,并**至少**要求该行含 `camera-and-viewpoint` **或**「系统 2」+ 一个 O 编号(O-12…O-16 之一)。补跑故事 `:97` 的 Negative fixture(逐项移行 ⇒ 逐项红、余项不牵连)。
2. **B-2**:④ 增补"次序不变量"措辞差异检(1 侧现为陈旧"帧内恒定");⑤ 增补 42 侧 F6 的"读哪一档"断言。
3. **B-3**:AC-2-21 守卫改为锚定 **`OQ-1-14` 行状态列**,或**无条件 `Assert.Ignore`**。跑后须见 **Ignored ≠ Passed**。
4. **B-4**:补 `vr_interface_freeze_test.cs`(四段零推进 + 计数 0 + 链状态无写入)或将 VR 面的 Complete 依据降为"仅枚举存在",并在故事里同步降级主张。`obligation_ledger_test.cs` 与实交件重名不一致,择一并统一。
5. **B-5**:为 AC-2-24 / AC-2-26 各补 `Assert.Ignore` 登记(或明确声明其 NOT-RUN 由故事散文而非测试承载)。
6. **B-6/B-7**:修计数(11→12);`obligationsHaveDeclaredReceivers` 若要留,改名为"本 GDD 自登记面";"禁混入 BLOCKING"半句补一条就绪度表级断言或**从主张中删除**。

> **结论**:本件在 B-1/B-2/B-3/B-4 修正并复跑(且 Negative fixture 真正执行)之前,**不应维持 Complete**。当前"AC-2-22 六项实测 ✅"的措辞**不可保留** —— 判据不支撑该结论。
