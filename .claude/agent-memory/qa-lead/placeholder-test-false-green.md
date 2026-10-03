---
name: placeholder-test-false-green
description: 本仓测试的十一类「假绿」——NOT-RUN 占位断言硬编码 true 的 helper、断言常量工厂(重言式)、空集合 All() 真空真、桩生产码/孤儿求值器、只断言类型存在/引擎行为、扫描作用域过窄、IL 谓词永不命中、同变量重复断言/同起点差分、判据由测试自己行使(自守其门)、扫描错方法/载体错位、断言两端皆出自测试自己且生产默认值恰为违例值
metadata:
  type: feedback
---

复审 `unity/Assets/Tests/**` 时,逐条区分「测试名引用了 AC」与「测试真的验了 AC」。本仓已确认三类**假绿**:

1. **NOT-RUN 占位断言硬编码 true 的 helper** —— 测试名 `test_acXXXX_..._notRun`,体内只
   `Assert.IsTrue(JudgeEvaluator.IsGoldenFixtureAvailable())`,而该 helper 是 `return true;` 桩。
   测试恒绿且与 AC 零关系。**计数时这类不得算作「已验」**;正解 = 直接 `Assert.Ignore("NOT-RUN: <前置>")`,
   让 NUnit 显式报 Skip,而非绿灯冒充。已见:`judge_test.cs`(story 003)、`aggregate_stream_test.cs`(story 004)、
   `feel_latency_test.cs`(story 006)。

2. **断言常量工厂 = 重言式** —— `CreateKeyboardFallbackReading()` 硬返回 `magnitude: MAG_MAX`,
   再断言 `reading.Magnitude == MAG_MAX` 恒真。**要问「真源在哪」**:键鼠回退的真实载体是输入侧
   (`Gameplay.Input/EmergencyAggregator.cs`),测试从不触它 ⇒ AC 未被验。同理 `GetKeyboardFallbackCause()` 返回字面量 `1`。

3. **空集合上的 `All()` / 类型名过滤后的断言 = 真空真** —— `methods.All(...)` 在 `methods` 为空时恒真;
   用 `t.Name.Contains("Dto")` 过滤类型后只扫 3 个硬编码关键字、且只 `GetFields()` 不递归不扫嵌套 = 对真实闭包无覆盖。
   正解 = 复用既有递归载体 `unity/Assets/Editor.Tools.Gates/PresentationDtoGuard.cs`(已做递归 + 字段 + 属性 + 泛型实参展开 + 负夹具)。

4. **「桩生产码 / 孤儿求值器」—— 测的是死代码,不是出货路径** —— 生产类只实现「可测核心」
   (`LocomotionEvaluator.SteadyStateSpeed` 等),**没有任何运行期调用者**(无 `Update`/`Awake` 调用点,
   唯一调用者是测试),且**真实出货路径是另一个类**(`PlayerController.Move` 自己做
   `moveInput * _moveSpeed`,不含链、不含加速/转向/地貌乘数)。绿测试证明的是「一个从未被调用的
   函数返回了我喂进去的东西」⇒ 零覆盖。**判定法**:对每条 AC 问「生产码里谁在运行期调它?」
   grep 调用点;若只有 `Tests/**` 命中 ⇒ 该 AC 的**出货路径未被验证**。与第 2 类同源
   (真源在生产路径,测试却打自造的/离线的载体),但更隐蔽:测试**没有**用常量工厂,而是一个
   形状正确、逻辑正确、却**接错了线**的类。已见:`LocomotionEvaluator.cs`(player-controller story 003)。

5. **断言的对象是「类型存在」或「引擎自己的行为」,不是生产路径** ——
   `Assert.IsNotNull(typeof(CellTransitionDetector))` 只证类编译通过;`Assert.AreEqual(-1, Mathf.FloorToInt(-0.5f))`
   证的是 Unity 的 `Mathf`,**与实现无关**(换成手写 floor 也绿)。**判定法**:把生产实现整段删成空壳,
   测试是否仍绿?仍绿 ⇒ 该 AC 未验。已见:`cell_transition_test.cs` 的 `test_ac107_floorToIntUsed`(player-controller story 004)。

6. **反射/扫描类断言作用域过窄 ⇒ 真空真** —— 只扫 `typeof(Detector).GetFields()` 找帧计数字段,
   而目标代码(`Grounded` 进入条件)根本不在这个类里 ⇒ 无论生产码怎么写都绿。同 3 类的空集真空真变体。
   已见:`test_ac117_noFrameCounterForGrounded`(player-controller story 004)。

7. **IL 谓词永不命中(名匹配 + 漏操作码)** —— `ILBodyScanner.ContainsMethodCall(m, "SimEvent")` 追 `new SimEvent(...)`
   永远 false:① 该扫描器只认 `call`/`callvirt`,**不识别 `newobj`(0x73)**,而构造走 newobj;
   ② 即便识别,`ResolveMethodToken` 取 `Name`,struct 构造函数名恒为 **`.ctor`**,不等于 `"SimEvent"`;
   ③ `SimEvent.Kind` 是**字段**(`public readonly EventKind Kind;`),无 `get_Kind`。⇒ `Assert.AreEqual(0, hits)` 恒绿。
   **判定法**:凡「零命中」型 IL 断言,先 `grep` 扫描器是否认 `Newobj`/`Ldfld`/`Stfld` 等目标操作码,
   再用**突变**(在被扫程序集里真加一处目标调用)证明会变红。已见:`camera_presentation_discipline_test.cs`
   的 `test_ac205_zeroSimEventConstruction`(camera-viewpoint story 001)。

8. **同变量重复断言 / 「同起点差分」重言式** —— ① 反向半边写 `Assert.AreEqual(0, hits, ...)` 复用正向的**同一 `hits`**
   ⇒ 无独立扫描面、无因果,却给报告「反向也绿」的假象;② 「差分重算」用**相同起点 + 相同输入序列**比两实例末帧,
   对任何确定性实例状态实现(含带隐藏静态态者)必然绿 ⇒ 不可证伪。AC 原文若要求「**不同历史**的后半段收敛」,
   实现换成「同起点」就是**偷换 AC 语义**(开发者常以「修正」名义这么做 ⇒ 须回到 AC 原文/权威件,不得由测试单方改判)。
   已见:同上测试的 `test_ac201b_differentialRecompute_lastFrameBitIdentical`。

9. **判据由测试自己行使(自守其门)** —— 断言「冻结态不发起查询」,而测试体内写
   `if (!m.IsFrozen) counting.Cast(...)` —— **是测试在跳过**,生产码无该短路 ⇒ 断言恒真。
   同型:断言「每帧计数==1」但循环里 `Cast` 由测试自己调 ⇒ 「我调了一次所以计数是 1」。
   **判定法**:被守的**动作**若在测试体内被代执行/被省略 ⇒ 该 AC 零覆盖。
   正解 = 夹具必须**驱动生产对象跑帧**(生产码持有 query 抽象),而非测试自持计数器。
   已见:`camera_mode_machine_test.cs` 的 `test_ac227a/b`(camera-viewpoint story 005)。

10. **扫描错方法 / 载体错位** —— AC 说「**写入点**(`Settle` 里的 `Mode = best.Mode`)方法体不得读它系统」,
   测试却扫 `SetMode`(一行入队方法)⇒ 真写入点零覆盖;附带**名字黑名单**(5 串)无法覆盖类型集,
   且**未剥注释**(假红)。同型:AC 要「相机变换逐帧位移 ≤ EPS」,实现只持一个标量 `TransitionT`
   且 `TRANSITION_JUMP_EPS` 全仓零命中 ⇒ 判据**无承载物**。
   **判定法**:先定位 AC 点名的**那个符号/那个对象**,再确认测试扫的是它;载体(标量 vs 变换;
   字符串表 vs 符号集)与 AC 原文不同构 ⇒ 强度不可保([[ac-carrier-discipline]])。

11. **断言两端皆出自测试自己 / 生产默认值恰为违例值** —— 断言 `ExpectedWhitelist == p.CamCollideMask`,而 `p` 由本文件工厂构造、
    `expected` 也是本文件字面量 ⇒ 验的是「测试的字面量 == 测试的字面量」,**从未读 AC 点名的登记处**(项目层设置/ADR-015 层表)。
    更重的是**生产默认字段恰为违例值**:`CamCollideMask = 0`(=空掩码=相机永不收缩穿墙)且全库无任何非测试写入点
    ⇒ 出厂即违例,测试注入合法值后转绿。同型:断言 `ArmLen > 0` 而 `ArmLen` 只是测试默认 4f,生产无装载守卫。
    **判定法**:对「相等/序/不变量」型断言,先问**期望值的真源在哪**;若真源在测试文件内(工厂/字面量)⇒ 假绿。
    再 grep **生产写入点/装载路径**;若为零 ⇒ 该 AC 的出货路径未被验证。
    已见:`camera_arm_solver_test.cs` 的 `test_ac215a`(掩码)、`test_ac225d`(ArmLen)、`test_ac225c`(序,纯注入 + `Assert.Ignore`)。

12. **「同源 / 单一来源」类 AC:判据只验「可注入/类型存在」,不验「读取来源」;且生产侧零消费者** ——
    AC 要求「A 读 B 的**那一个**常量,不得自造第二份」,测试却只断言「字段可注入」+「两注入互异」+ 反射查 B 存在同名字段。
    **这四条对「A 自造第二份常量」零抓错力** —— AC 自陈的负向夹具(复制值)会照样通过;更重的是 grep 生产码
    `读 B.常量` 的调用点 = **0 处**(A 全程读自持字段)。⇒ AC 的实现与判据**双向未落地**。
    **判定法**:凡「同源/单一来源/不自定义第二份」型 AC,先 `grep 'B.常量'` 找**生产读取点**;为 0 ⇒ 未兑现;
    再看测试断言能否证伪「自造第二份」;不能 ⇒ 假绿。**「可注入」≠「同源」**(与第 11 类「期望值真源在测试内」同源)。
    已见:camera-viewpoint story 003 的 `test_ac212b_maxDt_singleSource_notSecondDefinition`。

13. **期望值耦合「当前实现」而非「规格」⇒ 按规格修正实现时测试反红** —— AC/规格写 `yaw += Look.x × SENS × dt`,
    而 prod `ApplyOrbit` **丢弃 `dt`**(`_ = dtSeconds;`),测试期望值恰取 `Dx × N`(= 丢弃 dt 的产物)。
    ⇒ 测试把**非规格实现**钉死;一旦按规格补 dt,测试**变红**。这类不是恒真,而是**判据锚错了权威件**。
    **判定法**:把期望值表达式与 GDD/规格公式逐项对照(此处少一因子 `SENS×dt`);不一致 ⇒ 怀疑测试耦合实现,
    回权威件裁决,不得让测试单方定义「正确」。已见:同 story 的 `test_ac213_pitchClamped_yawStillAccumulates`。

**Why**:本仓 `NOT-RUN 禁借绿` 是反复申明的纪律,但「绿灯冒充 NOT-RUN」与「断言常量」两类会静默稀释 BLOCKING 门;
story 006 三条 BLOCKING AC(AC-10-09/17/21)正是被这三类假绿覆盖的典型。

**How to apply**:复审每条 AC 时问三问 —— ①测试体内是否出现硬编码 `return true` / 字面量常量?
②断言的对象是**生产路径**还是**测试自造的常量工厂**?③集合断言是否可能在空集上真空真?
命中任一 ⇒ 该 AC 判 NOT-VERIFIED,story 不得 Complete。相关:[[ac-carrier-discipline]]
