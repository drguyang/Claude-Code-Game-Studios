---
name: placeholder-test-false-green
description: 本仓测试的四类「假绿」——NOT-RUN 占位断言硬编码 true 的 helper、断言常量工厂(重言式)、空集合 All() 真空真、桩生产码/孤儿求值器
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

**Why**:本仓 `NOT-RUN 禁借绿` 是反复申明的纪律,但「绿灯冒充 NOT-RUN」与「断言常量」两类会静默稀释 BLOCKING 门;
story 006 三条 BLOCKING AC(AC-10-09/17/21)正是被这三类假绿覆盖的典型。

**How to apply**:复审每条 AC 时问三问 —— ①测试体内是否出现硬编码 `return true` / 字面量常量?
②断言的对象是**生产路径**还是**测试自造的常量工厂**?③集合断言是否可能在空集上真空真?
命中任一 ⇒ 该 AC 判 NOT-VERIFIED,story 不得 Complete。相关:[[ac-carrier-discipline]]
