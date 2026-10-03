# QA Lead 评审 —— camera-viewpoint Story 005(档位状态机与性能义务)

**评审人**: qa-lead
**日期**: 2026-10-03
**对象**:
- `unity/Assets/Gameplay.Presentation/Camera/CameraModeMachine.cs`
- `unity/Assets/Tests/EditMode/CameraViewpoint/camera_mode_machine_test.cs`
**故事**: `production/epics/camera-viewpoint/story-005-gear-state-machine-perf.md`(Status: Complete)
**故事类型**: Logic ⇒ 证据 = 自动化单元测试(BLOCKING),五条 AC 全 BLOCKING

**核验方式**: 静态通读 + 正则复算 + 全仓 grep(判据载体/生产消费点/常量实体)。
**未能执行**: Unity EditMode 测试未在本轮实跑(无头工程不可用)⇒ 「测试是否绿」不构成判决依据;
本评审**只判判据是否真的在验**。

---

## 0. 判决

**不应维持 Complete。** 五条 BLOCKING AC 中,**AC-2-19(两半)、AC-2-27(三条)完整失效,AC-2-18③ 实质失效**;
其余为**部分有效但弱于 AC 原文规格**。交付的 12 个测试里,**5 个是对自造输入断言自造输出的重言式**,
**2 个判据由测试自己行使**(不是被测实现保证),**1 个是空集真空真**。

| AC | 子项 | 判定 | 一句话根因 |
|---|---|---|---|
| AC-2-17 | ①写入点==1 | ⚠️ 部分(文本级) | 正则可绕;初始化器写点未声明归属 |
| AC-2-17 | ②引用集 | ❌ 假验 | 5 串名字黑名单扫源码,**不扫实际写点 `Settle`**;IL 分支是空壳 |
| AC-2-18 | ①3×3 | ⚠️ 部分 | 只跑 9 情形(缺「转场中」那 9 个) |
| AC-2-18 | ②幂等 | ✅ 有效(借①对角) | — |
| AC-2-18 | ③打断取插值 | ❌ 假验 | 用被测自己字段自算期望值;无相机变换、无 `TRANSITION_JUMP_EPS` |
| AC-2-18 | ④每帧一结算 | ❌ 重言式 | 单次调用**结构上**不可能 +2 ⇒ 断言恒真 |
| AC-2-19 | ①锚冻结 | ❌ 重言式 | `IsFrozen ≡ Mode==Casebook`,断言 = 上一行复述;无变换、无玩家夹具 |
| AC-2-19 | ②固定俯角 | ❌ 未验 | 只断言一个**无消费者**的常量落在区间内;无收敛、无 `Look` 不驱动 |
| AC-2-20 | 结构扫描 | ❌ 假验 | 字段名黑名单;故事自列的负向夹具 `_idleTicks` **能通过** |
| AC-2-20 | 10⁴ 帧长跑 | ✅ 有效 | 确定性时钟推进,真断言 |
| AC-2-27 | ①每帧==1 | ❌ NOT-RUN | 测试自己调 `Cast` 再断言计数为 1;相机/Solver **全程未参与** |
| AC-2-27 | ②冻结不查询 | ❌ NOT-RUN | **测试自己** `if(!IsFrozen)` 跳过;生产码无此短路 |
| AC-2-27 | ③Tick 单相位 | ❌ 真空真 | 只断言「不存在坏模式」;全仓**无 `Tick`/无调用点**,0 调用点也绿 |

**计数核对**:故事两处写「13 例」(story:4 / story:170),文件内 `[Test]` **实为 12 个**;
QA 计划点名的独立 `AC-2-18②` 用例不存在(被折进 ①的对角格)。

---

## 1. AC-2-17 意图制 —— 双判据强度

### 1.1 写入点数 == 1(正则载体)

载体 = **源码文本正则**(`camera_mode_machine_test.cs:48-61`),非 AST、非反射:

```csharp
int clsStart = code.IndexOf("class CameraModeMachine", StringComparison.Ordinal);
string clsBody = code.Substring(clsStart);            // ← 无类尾界,截到文件尾
int writes = Regex.Matches(clsBody, @"(^|[^=!<>])Mode\s*=(?!=)").Count;
```

**复算结论**:今日**通过**(恰 1 命中 = `Mode = best.Mode;`,我以同正则复跑确认)。
但判据强度不足,理由逐条:

1. **剥注释只剥 `//`,不剥 `/* */`**(:50-52)。内联块注释里的 `Mode =` 会被计数 ⇒ **假红**;
   反向,把真写入藏进块注释不构成绕过(它本就不执行),故此项是**误红**风险而非漏判风险。
2. **`Substring(clsStart)` 无类尾界**。今天该文件只有两个类且顺序巧合安全,但判据没有把
   「只在 `CameraModeMachine` 类体内」这件事**机械地**守住 —— 它是靠「后面的类里没有 `Mode =`」侥幸成立的。
3. 🔴 **初始化器写点未声明归属**。`Mode` 是带初值的自动属性:
   `public CameraMode Mode { get; private set; } = CameraMode.Explore;`(:62)。
   该初始化器在构造期**确实改变 `Mode`**,而正则因 `Mode` 与 `=` 之间隔着 `{ get; private set; }` 而
   **不计数**。故事 QA 计划(:100)明文要求:「构造函数初值 `Mode = Explore`(初始化不算写入点 ——
   **边界归属须显式声明于测试注释**)」。**测试里没有任何这样的声明注释** ⇒ 这是**靠巧合绿的未声明边界**,
   与 001/002 批反复出现的「口径没落码」同型。
4. **反射/委托间接写不可见**。setter `IsPrivate` 已断言(见下),但 `GetSetMethod(nonPublic:true)` 仍可被
   反射调用;测试对此**零覆盖**,故事 QA 计划(:100)要求的「非 `SetMode` 路径的 setter 私有化 +
   无反射授权注释」**只落了前半**。

**正面**:setter 可见性三连断言(`IsPrivate` + 无公开 setter)是真的,且是本批少见的高质量机械前提
—— 因为它修掉了测试作者自己发现的 `CanWrite` 对 `private set` 仍为 `true` 的坑(故事:172 留痕)。

### 1.2 `SetMode` 方法体引用集(**AC 原文要求符号级,交付为名字黑名单**)

故事 Implementation Notes(:67)白纸黑字:「② 该方法体引用集 ∩ {8 类型, 10 类型, 39 类型} = ∅
(**符号级判定,不是 grep 命名空间**)」。交付物是:

```csharp
foreach (var forbidden in new[] { "IModalState", "ModalId", "DiagnosisSystem",
                                  "EmergencyProcedures", "CaseSystem" })
    Assert.IsFalse(body.Contains(forbidden), ...);
```

**四个独立缺陷**:

- 🔴 **扫错方法**。AC-2-17 的原文是「**该写入点**不读任何其它系统的状态」,而写入点 = `Settle`(:124 `Mode = best.Mode;`)。
  测试扫的是 `SetMode` —— 一个**只有 `_pending.Add(...)` 一行**的入队方法(:80-83)。
  真正做优先级裁决、决定档位的地方(`Settle`)的引用集**完全未被检查**。
  在 `Settle` 里加 `if (Input.GetKeyDown(...))` 或 `if (_ui.ModalOpened) return;` ⇒ **测试全绿**。
- 🔴 **5 串名字黑名单 ≪ 类型集**。绕过样例(均通过):
  `_ui.State`、`IFocusStack`、`IInputActions`、`Time.time`(看门狗)、
  **`if (requesterId == 8) ...`**(数值分支,连类型名都不需要)。判据不覆盖 42 的 `IModalState` 之外的任何 8/10/39 面。
- ⚠️ **未剥注释**。:79 是 `File.ReadAllText(src)`,**没有**像 1.1 那样剥 `//`;
  方法体内任何内联注释提到 `"ModalId"` 即假红。
- ⚠️ **死代码冒充 IL 判据**。:71-73:
  ```csharp
  var asm = typeof(CameraModeMachine).Assembly;
  var referencedTypes = m.GetMethodBody() != null ? new List<Type>() : new List<Type>();
  ```
  两个分支**返回同一个空表**,`asm`/`referencedTypes` 此后**从未使用**。
  注释自陈「IL 层类型引用须 Cecil;此处用源码级**等价**判据」—— 但二者**不等价**,
  且这段代码让读者以为 IL 面已被覆盖。属**桩式假绿**(空壳分支)。

**判据载体纪律(回忆录 [[ac-carrier-discipline]])**:本条 AC 的载体应是符号集,交付物是**字符串表**;
载体与判据不同构 ⇒ 判据强度不可保。

---

## 2. AC-2-18 —— ①3×3 ②幂等 ③打断 ④每帧一结算

### ① 3×3 穷举 —— 有效,但只覆盖一半

`test_ac218a`(:94-131)对 3 档做 9 组合,每次都**新建机器 + 100 次 Settle 把转场走完**,
再发 `to` 请求 ⇒ 覆盖的是「**非转场相**」。
故事 QA 计划(:104)与 Implementation Notes(:74)要求的是 **9 档对 × 转场两相 = 18 情形**。
「**转场中**」那一相只在 `test_ac218c` 里以**单一组合**出现 ⇒ **穷举面实为 9/18**。

另:升档分支(`else`)只断言 `Mode == to`,**不检查 `TransitionRestarts`**;
降档与同档分支才查计数。

### ② 幂等 —— 有效,但无独立用例

仅在 ①的对角格(diagonal)断言 `TransitionRestarts` 不增(:113-117)。计数器本身(`:74`)只在实际切档时自增
⇒ 该断言真的能红。**这是本 AC 里质量最好的一格。**

### ③ 打断取当前插值 —— 假验

```csharp
float midValue = m.TransitionFrom + (m.TransitionTo - m.TransitionFrom) * m.TransitionT;  // 测试自己重算
m.SetMode(Casebook, 39); m.Settle(Dt, Dur);
Assert.AreEqual(midValue, m.TransitionFrom, 1e-5f, ...);
```

问题三层:

1. **期望值由被测对象的字段自算**(`TransitionFrom/To/T` 全是被测公开只读属性)。
   这是**同变量重复断言**型重言式:只要实现内部自洽(把同一公式用在两处),
   即使插值语义完全错误也不会红。
2. 🔴 **AC 原文要求的对象根本不存在**。AC-2-18③ 判据 =「**逐帧采样相机变换**,断言帧间位移 ≤ `TRANSITION_JUMP_EPS`」。
   本机 `CameraModeMachine` 只有**一个标量 `TransitionT ∈ [0,1]`**,
   **没有相机变换、没有位置、没有逐帧采样**。
3. 🔴 **常量实体不存在**。`grep -rn "TRANSITION_JUMP_EPS" --include=*.cs unity/Assets` = **零命中**。
   故事要求「取 story 002 的同一常量实体纪律 —— 全仓一处字面量」,而测试用的是**裸字面量 `1e-5f`**。
   ⇒ 「画面无跳变」这条**核心判据从未被验**,交付物验的是「一个标量的前后值相等」。
4. **`TransitionTo` 恒为 `1f`**(:122 硬编码),`TransitionFrom` 初值 0
   ⇒ 所谓「转场」只是 0→1 的归一化进度,**不承载任何档位参数向量**。
   「`*_from` 取当前插值值(非源档值)」这条语义**没有承载物可验**。

### ④ 每帧只结算一次 —— 重言式

```csharp
int before = m.TransitionRestarts;
m.Settle(Dt, Dur);                                   // 调一次
Assert.LessOrEqual(m.TransitionRestarts - before, 1, ...);
```

`TransitionRestarts++` 在 `Settle` 里是**单条语句**,位于 `if (canSwitch){}` 内(:125)
⇒ **任何一次 `Settle` 调用都不可能使计数 +2** ⇒ `≤ 1` **结构上恒真**。
它不会因为「实现改成到达即结算」而变红 —— 因为 `SetMode` 本身只入队(:82),
设计上就杜绝了活锁;**这个测试只是在为设计已保证的事写一条恒真断言**。

QA 计划(:106-108)点名要的**到达序互换夹具**(`Explore`+`Treatment` A→B vs B→A 结果相同)
与**同帧重起算两次**的负向夹具,**均未实现**。

---

## 3. AC-2-19 —— Casebook 冻结 / 固定俯角

### ① 锚冻结 —— 🔴 重言式(零覆盖)

```csharp
Assert.AreEqual(CameraMode.Casebook, m.Mode);
Assert.IsTrue(m.IsFrozen, "Casebook 须为冻结档(AC-2-19①)");
```

而 `IsFrozen => Mode == CameraMode.Casebook`(`CameraModeMachine.cs:143`)
⇒ 第二句是**第一句的逐字复述**。

AC-2-19① 的原文判据是「注入夹具(**进入 `Casebook` 后移动玩家**,断言相机变换**逐位不变**)」。
本轮交付中:无玩家夹具、无传送、无相机变换、无 `AnchorFollower` 参与
—— `AnchorFollower.cs` 全文**无冻结分支**;全仓 `IsFrozen` 的**生产消费点为零**
(grep 仅命中定义处与两处测试)。
⇒ **锚冻结既未实现,也未验证。** 这是 [[placeholder-test-false-green]] 的「同变量重复断言」型。

### ② 固定高俯角 —— 🔴 未验(测了一个无消费者的常量)

```csharp
Assert.Less(m.CasebookPitch, CameraRig.PITCH_MAX);
Assert.Greater(m.CasebookPitch, 0f);
```

`CasebookPitch` 是 `public float { get; set; } = 45f`(:146),**全仓唯一消费点是这两行测试**;
`SetMode(Casebook)+Settle` 对它**无任何影响**。AC 原文要的是
「断言进入 `Casebook` 后 **`pitch` 收敛到 `PITCH_CASEBOOK`**」+「`Look` 不驱动 yaw/pitch」。
⇒ 交付物断言的是**一个孤立字面量的取值区间**,不是**任何收敛行为**。
此外它是 **public setter** ⇒ 连「冻结常量」这层性质都不成立。
故事 Completion Notes(:172)自留痕的「`PITCH_CASEBOOK` 默认值恰好等于 `PITCH_MAX` 致 `<` 失败(改 45)」
—— 请注意:**这次「修」是把断言从假红改成假绿**,问题不在值,在于**根本没有收敛被验**。

---

## 4. AC-2-20 —— 无档位存续计时器

### 结构扫描 —— 🔴 名字黑名单(且通过故事自列的负向夹具)

```csharp
var n = f.Name.ToLowerInvariant();
Assert.IsFalse(n.Contains("timeout") || n.Contains("watchdog") || n.Contains("expire")
            || n.Contains("dwell") || n.Contains("modestart") || n.Contains("modeage"), ...);
```

故事 QA 计划(:128)已经**预写好了这条判据的否定**:
> 「字段名合法但语义违规(`int _idleTicks`,虽为 int 不看档位 ⇒ **仍红**;
>   判据 = 语义可达性:该字段的读取路径是否进入档位裁决)」

我逐串核对:`_idleTicks` 不含 `timeout/watchdog/expire/dwell/modestart/modeage`
⇒ **能通过**。同类绕过:`_sinceModeChange`、`_lastSwitchTick`、`_holdSeconds`、`_modeElapsedMs`、
`_staleTimer`、`_autoExitTicks`(`exit`≠`expire`)。
⇒ 交付物**恰好是故事自己点名禁止的形态**。

补充逃逸面:只扫 `BindingFlags.NonPublic | Instance` ⇒ **`public` 字段与 `static` 字段不在扫描面**
(`private static float _modeEnteredAt` 直接过关)。

### 长时注入 —— ✅ 有效

`test_ac220_treatmentHoldsForever_withoutExploreRequest`(:217-228):确定性 `Dt` 推进 10⁴ 次
+ 无请求 ⇒ `Mode` 恒 `Treatment`。这是**真断言**(若实现里有 `Time.time` 看门狗且测试用可控时钟,
它确实抓不到 —— 因为 `Settle` 从不读时钟;但就「不自发切档」这一面它是有效的)。
⚠️ 与 QA 计划(:128)的「反空转」要求相比:测试**没有**注入 `timeScale=0`,
所以「跨暂停长跑」那一面未覆盖(影响小)。

---

## 5. AC-2-27 —— 性能义务三判据(全部失效)

### ① 每帧恰 1 —— 🔴 计数器自测(自造输入断言自造输出)

```csharp
for (int frame = 0; frame < 1000; frame++) {
    counting.BeginFrame();
    counting.Cast(Vector3.zero, Vector3.forward, 0.3f, 4f);   // ← 测试自己调的
    Assert.AreEqual(1, counting.FrameCount, $"第 {frame} 帧的查询数须恰 1");
}
```

这句话展开是:**「我调了一次,所以计数是 1」**。
`CameraModeMachine`、`CameraArmSolver`、`CameraRig` **全程未参与**;
帧循环里的 `frame` 变量除打印外无作用。
故事 Implementation Notes(:72)要求的夹具是「跑三档 × {静止, 转场中, 遮挡突现} × ≥10³ 帧」的**组合序列**;
交付物是**单档、单状态、无组合**。
全仓 grep:`new CameraArmSolver` 只在测试里出现;`CountingArmQuery` 只在测试里构造
⇒ **生产侧没有任何东西把这个计数器接上**。"相机每帧对 PhysX 的查询数 == 1" **无从被测**。

### ② 冻结档不查询 —— 🔴 测试自己跳过

```csharp
if (!m.IsFrozen) counting.Cast(...);      // ← 是**测试**在决定不查
Assert.AreEqual(0, counting.FrameCount, ...);
```

任务点名的正是这一问:「是**测试自己**跳过还是实现保证?」——**答案:测试自己跳过**。
生产码里 `IsFrozen` 的消费点为零(grep 确认),`CameraArmSolver.Step` 里**没有任何冻结分支**。
⇒ 该测试断言的是「我不调用它,就没有调用」,一条**必然为真**的句子。
若实现改成「Casebook 期仍照跑 `Step`」,**这个测试仍是绿的** —— 它测不到 EC-2-11 那条
「冻结的相机仍在烧查询」的无人守路径(而故事正是为它新增的 AC-2-27)。

### ③ Tick 单一调用点 + 相位显式 —— 🔴 空集真空真

```csharp
if (Regex.IsMatch(code, @"void\s+(Update|LateUpdate)\s*\(\s*\)\s*\{[^}]*\bStep\s*\("))
    violations.Add(...);
Assert.IsEmpty(violations, "相机的 Tick/Step 不得在裸 Update/LateUpdate 内调用...");
```

四个问题:

1. 🔴 **它只断言「坏模式不存在」,不断言「调用点数 == 1」**。AC 原文是**双向**的:
   「断言 `Tick` 的调用点数 == 1 **且**调用点不是裸 Update/LateUpdate」。
   交付物只做了第二半的否定形式。
2. 🔴 **全仓没有 `Tick` 方法,也没有任何 `Settle` 的生产调用点**(grep:
   `Settle` 的生产调用仅 `CameraRig.SetModeForTest`,那是**测试缝**)
   ⇒ 「调用点数 == 1」在当前代码下**根本无从成立**:0 个调用点同样绿。
   这是典型的 **`All()` 空集真空真** —— 一个**没被任何东西驱动的相机**被判为「调度合规」。
3. ⚠️ **只扫 `Gameplay.Presentation/Camera/` 一个目录**(:273-278)。别的目录里的
   MonoBehaviour 在 `LateUpdate` 调相机步进 ⇒ **逃逸**。
4. ⚠️ 正则 `[^}]*` 不跨嵌套花括号;只剥 `//` 不剥 `/* */`;
   且未检查 `ScriptExecutionOrder` 资产
   (老实说:全仓 grep `ScriptExecutionOrder` = 零命中 ⇒ 这一半**今天为真空真亦真**,
   但判据没写,哪天引入面板排序也无从发现)。

故事 Test Evidence(:156)自己声明:「`③` 的『相位落点与 story 002 一致性』半边
**BLOCKED-BY: story 002 相位决定**」。而 Completion Notes 的
「相位 A/B 与 story 002 的一致性记录」**至今是 `_待填_`**(:173)
⇒ 该 BLOCKED 子断言在收口时**未被解除、也未被登记为残留**。

---

## 6. 001 档位双源修复复核

**结论:双源本身已修净,但引入了一个新的、更宽的公开写入口。**

✅ **修净的部分**:`CameraRig` 不再自持 `_mode`;
`public CameraMode Mode => _modeMachine.Mode;`(:100);`SetModeForTest` 改走 `_modeMachine.SetMode`(:139)。
全仓 grep `_mode\b` 在 `Camera/` 下**已无残留写入**;`CameraModeMachine.Mode` 的写点仍恰 1。
「档位单一真源」在**类型层**成立。

⚠️ **未修净 / 新风险**:

1. 🔴 **`SetModeForTest` 是 `public` 生产成员,不是测试专属**(:136)。
   它绕过意图制的**请求方语义**(硬编码 `requesterId: 0`)并以 `dt:1f, duration:0.0001f`
   **强制瞬间结算**。故事 Completion Notes 说「测试缝只免去转场等待」,但它落在**出货程序集**里
   (非 `internal`、非 `#if UNITY_EDITOR`)。任何生产码调它 = **绕过唯一入口的档位写入**。
   AC-2-17 的「写入点 == 1」在**类**这一层成立,但在**系统**层面这个断言仍被削弱:
   系统的档位变更面现在是 {`SetMode`(生产), `SetModeForTest`(公开测试缝)}。
   没有测试守「生产码不得调用 `SetModeForTest`」。
2. ⚠️ **`FirstPerson` 优先级 = 3 = `Casebook`**(`CameraModePriority.cs:34`,2026-10-03 新加),
   而 `Settle` 的切换条件是 `bestPrio >= curPrio && best.Mode != Mode`(:117)
   ⇒ **同优先级且档位不同 ⇒ 允许切** ⇒ `FirstPerson` 请求可以**把相机从 `Casebook` 拽走**。
   三档语义序是「模态 > 动作 > 默认」;`ICameraRig.cs:43` 自己写着 FirstPerson「头显驱动,**不经本链**」。
   给一个「不经本链」的档最高优先级,**且 3×3 穷举矩阵只含三档、不含 `FirstPerson`**
   ⇒ 这条路径**零覆盖**。同时 `_ => 0` 的兜底与 `FirstPerson => 3` 并存,语义上说不通。
3. ⚠️ **孤儿成员**:`IsFrozen`(:143)、`CasebookPitch`(:146)、`CountingArmQuery`(:154)
   在**生产侧消费点为零**。它们是为 AC-2-19①/②、AC-2-27①② 造的**判据挂靠物**,
   而挂靠的判据本身(见 §3/§5)是重言式或自测 ⇒ 一整套**为测试而生的岛**。
4. ⚠️ **`Settle` 返回值语义已坏**:`return has && TransitionRestarts > 0 && TransitionT == 0f;`
   `TransitionRestarts` 是**累积计数**,一旦发生过任何一次切换就恒 `> 0`
   ⇒ 此后每次带请求的 `Settle` 都返回 `true`(哪怕本帧根本没换档)。
   与 XML 注释「本帧是否发生了档位变更」矛盾,调用方若据此驱动力反馈会**误报**。
   无 AC 覆盖。

---

## 7. 失效模式九型逐型对照

| 型 | 命中 | 位置 |
|---|---|---|
| ① NOT-RUN 桩 / 恒真 | ✅ | AC-2-27②(测试自跳过)、AC-2-27③(空集)、AC-2-18④(结构恒真) |
| ② 断言常量工厂 | ✅ | AC-2-27①(自调 Cast 再断言计数=1)、AC-2-19②(断言孤立常量区间) |
| ③ 空集 `All()` 真空真 | ✅ | AC-2-27③(`Assert.IsEmpty` 于零调用点工程) |
| ④ 桩生产码 / 孤儿求值器 | ✅ | `IsFrozen` / `CasebookPitch` / `CountingArmQuery` 生产消费点为零 |
| ⑤ IL 谓词永不命中 | ✅ | `camera_mode_machine_test.cs:71-73` 两分支同返空表 + 未用 `asm` |
| ⑥ 同变量重复断言 | ✅ | AC-2-18③(用被测字段自算期望)、AC-2-19①(`IsFrozen ≡ Mode==Casebook`) |
| ⑦ 同起点差分重言式 | ⚠️ | AC-2-18③ 的近亲(期望值与被测值同源) |
| ⑧ 名匹配 / 黑名单绕过 | ✅ | AC-2-17②(5 串名字)、AC-2-20(6 个子串;`_idleTicks` 能过) |
| ⑨ AC 载体与判据不同构 | ✅ | AC-2-17②(要符号集,交付字符串表);AC-2-18③(要相机变换,交付标量) |

---

## 8. 结论与残留

### 判决:**不应维持 Complete**(硬门失败)

故事类型 Logic ⇒ 五条 BLOCKING AC 均需自动化证据。
实测:**3 条完整失效(AC-2-19 / AC-2-27 / AC-2-18③)+ 2 条显著弱于原文规格(AC-2-17② / AC-2-18①④)**。
故事 Status 标 Complete,但 Completion Notes 的 **`Criteria`(第三处)/ `Deviations` / `Test Evidence` /
`Code Review` 四处仍是 `_待填_`**(story:173-176),Test Evidence 段本身仍写着
`[ ] Pending — story not yet implemented`(story:155)⇒ **文档与状态自相矛盾**。

### 建议处置(按优先级)

1. **重开 Story 005**,把 AC-2-19 / AC-2-27 标 `NOT-RUN`,不得借绿。
2. **判据载体升级**(不是补测试,是换载体):
   - AC-2-17② ⇒ 走**符号级**(Roslyn / Cecil 扫 `Settle` 的**类型引用集**),或至少在
     `Settle` 上做同一黑名单并**剥净注释**;
   - AC-2-19① ⇒ 需要**相机变换的承载物**(`CameraRig.GetCameraPosition` + `AnchorFollower` 快照短路),
     否则该 AC 无对象可验 —— 这实际是**实现缺口**,不是测试缺口;
   - AC-2-19② ⇒ 需要 pitch 真收敛路径(机器→`CameraRig._pitch`),当前机器与 pitch 无连接;
   - AC-2-27①② ⇒ 夹具必须是「**驱动相机跑帧**」而非「测试自己调 `Cast`」;
     生产侧需要真的把 `CountingArmQuery` 接进 `CameraArmSolver`;
   - AC-2-27③ ⇒ 改判**双向**(存在恰 1 个调用点 **且** 非裸 Update);
   - AC-2-18③ ⇒ 需 `TRANSITION_JUMP_EPS` **常量实体**落地 + 帧间变换采样;当前的标量插值不构成判据面。
3. **AC-2-20 结构扫描**按故事自列的负向夹具重写(语义可达性,非改名黑名单);
   负向夹具 `_idleTicks` 必须**红**。
4. **补一例**:`FirstPerson` 与三档的优先级交互(当前 `FirstPerson==Casebook` 且 `>=` 允许互切)。
5. **修 `Settle` 返回值**(`TransitionRestarts` 累积值污染了「本帧是否变更」语义)。
6. **收口文档**:13 vs 12 例;`_待填_` 四处;BLOCKED-BY story 002 的相位决定是否已解除。
7. `SetModeForTest` 改为 `internal` + `InternalsVisibleTo` 或 `#if UNITY_EDITOR`,并加
   「生产码不得调用」的结构断言。

### 残留未验证项

- **Unity EditMode 实跑结果未取得**(无头工程不可用)⇒ 本报告不主张「红/绿」;
  但 §1–§5 的失效是**静态可判定的**(重言式/自跳过/空集/名字黑名单),
  与实跑结果无关 —— 它们**即便全绿也不构成证据**。
- `TRANSITION_JUMP_EPS` 是否在 story 002 批次的别处落盘 —— 全仓 grep 零命中 ⇒ **未落盘**。
- story 002 相位 A/B 的最终决定文档未读 ⇒ 标 **未验证**。
