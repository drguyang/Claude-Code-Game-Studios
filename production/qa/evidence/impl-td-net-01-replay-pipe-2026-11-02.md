# 实现原件 —— `TD-net-01` 收口:`IReplayPipe` / `ReorderBuffer` 的 P0 预埋

> **对象**:`docs/tech-debt-register.md` 的 `TD-net-01`
> **批次**:`S23-22`(用户指令「按你的建议继续修,一个个的不要停」)
> **截至时刻 / commit**:见 §五

---

## 一、病因(债册原文)

`ADR-001` 把 pipe 抽象的落地级别定为 **P0**,三处逐字:

- `:10` / `:59` / `:131` —— 「系统 45 = **P0 预埋** · P1b 实现」
- `:167` —— 「**P0 预埋**:接口与 `ReorderBuffer` 是纯 C# 逻辑,**P0 全实现为本地占位**」
- `:448` —— Implementation Plan **第 1 条**「**P0**:写 `IReplayPipe` + `ReorderBuffer`」
  + **第 2 条**「**P0**:八个抽象点(P0 全实现为本地占位)+ 三流全序键单测」

**实测(改前)**:`IReplayPipe` **全树零命中**;`ReorderBuffer` **生产零命中**,
测试树唯一一处是注释引用(`Tests/EditMode/Audio/net_derivation_test.cs:353`)。
八个抽象点落 **7** 个 —— `IReplayPipe` 是**唯一零落点**。

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Sim.Contracts/IReplayPipe.cs`(**新增**) | `IReplayPipe` 接口(`Publish` / `Subscribe`,ADR-001 `:137-141` 逐字)+ `sealed class ReorderBuffer`(`MaxLag` / `TryEnqueue` / `Drain` / `Count` / `Enqueued` / `Dropped`) |
| 2 | `Sim/LocalReplayPipe.cs`(**新增**) | `IReplayPipe` 的 **P0 本地占位实现**(同进程发布/订阅,零网络) |
| 3 | `Tests/EditMode/Networking/replay_pipe_contract_test.cs`(**新增**) | 8 条判据 |
| 4 | `Tests/EditMode/Networking/`(**新增目录**) | 测试落点(45 网络层在测试树此前无目录) |

### 为什么接口住 `Sim.Contracts`、实现住 `Sim`

- **接口 → `Sim.Contracts`**:ADR-025 §① 的装配清单是**封闭**的(未登记 asmdef = 构建失败)。
  消费者是 `Sim`(`IEventSink` 的对称面)与未来的 45 实现;`Sim` 的引用集**恰 =
  `{BCL, Sim.Contracts}`** ⇒ 只有住 `Sim.Contracts` 才被 `Sim` 看见,且**不开新程序集边**
  (门 A / b2 / ADR-025 §① 一字不改)。
- **实现 → `Sim`**:`Publish` 要算全序键,而**权威计算** = `EventOrder.KeyOf(e)`
  (`Sim` 程序集,依赖 kindgen 的 `StreamRouting`)。与 ADR-029 / ADR-031 的
  「契约支 + 实现支」同形。

### `TryEnqueue` 的键为什么是**形参**而非内部现算

全序键的权威计算在 `EventOrder.KeyOf`(`Sim`),而 `ReorderBuffer` 住 `Sim.Contracts`
—— 两程序集方向**不可倒**(ADR-025 §①)。若缓冲自己拼一份 `EventOrderKey`,
就成了**第二真源**(两份「怎么算全序键」迟早在某次修订里分叉)。

### 告警为什么走注入

`Sim.Contracts` 与 `Sim` 都在门 A 下(**零 `UnityEngine`**)⇒ 不能调 `Debug.LogWarning`。
`ReorderBuffer` 的 `overflowWarningSink` 与 `LocalReplayPipe` 的同名形参 = 注入点
(生产装配接 `Debug.LogWarning`,测试接收集列表)。`null` ⇒ 不告警,**但超界丢弃照常发生**
(不告警 ≠ 不丢弃)。

### 刻意**不做**的事

1. **不含第二 QoS 通道** —— ADR-001 §一之二的 `IPositionalChannel` / `WorldPosLatest`
   是**表现态位置**通道,与三流可靠 pipe 语义、QoS、消费者全不同;合并进同一文件会让
   「谁走哪条通道」在类型面不可分。落点归 45 的 P1b 轮(消费者 = 44 的远端空间化)。
2. **不做语义判断** —— 不校验 Kind 是否在白名单(那归 `EventOrder.KeyOf` 的
   `StreamRouting`,已构建期拒收);不去重(归 `EventStream`);排序归 `ReorderBuffer`。
3. **不改 ADR-001 的任何裁决,不动 P1b 的库选型时点** —— 债册解除动作逐字要求。

---

## 三、判据(8 条,逐条可证伪)

| # | 判据 | 测试名 |
|---|---|---|
| ① | `IReplayPipe` 在 `Sim.Contracts`,且**恰有** `Publish` / `Subscribe` 两个方法 | `test_tdnet01_interface_livesInContracts_withTwoMethods` |
| ② | `ReorderBuffer` 与接口同程序集(纯逻辑,零依赖) | `test_tdnet01_reorderBuffer_livesInContracts` |
| ③ | **主判据**:乱序注入(Seq 5/2/9/1/7)⇒ `Drain` 输出**按全序键升序**、**无丢**(5 入 5 出)、**无重放**(每条至多一次)、Drain 后清空 | `test_tdnet01_reorderBuffer_outOfOrderInput_drainsInTotalOrder` |
| ④ | 超界 ⇒ `TryEnqueue` 返回 `false`(**不抛**)、`Dropped` 递增、告警点名「超界丢弃」+「重连」 | `test_tdnet01_reorderBuffer_overflow_dropsAndWarns` |
| ⑤ | `MaxLag = 0` ⇒ 构造期抛 `ArgumentOutOfRangeException`,报文点名 `MaxLag` | `test_tdnet01_reorderBuffer_zeroMaxLag_throwsAtConstruction` |
| ⑥ | `LocalReplayPipe.Publish` ⇒ 订阅者收到**同一条**(同 Tick / Seq / Kind) | `test_tdnet01_localPipe_publish_reachesSubscriber` |
| ⑦ | `Subscribe(null)` ⇒ 抛(不静默接受) | `test_tdnet01_localPipe_subscribeNull_throws` |
| ⑧ | 两个订阅者 ⇒ **都**收到(追加,不是替换) | `test_tdnet01_localPipe_secondSubscribe_appends` |

⚠️ ⑧ 的**边界**:ADR-001 原文**未定**多订阅语义(接口注已写明「归实现方」)。
本条钉的是「**本实现**的行为与其文档一致」,**不宣称**它是 ADR 的裁决。

---

## 四、证伪轮

**方法**:一个 Python 脚本,每轮**同一进程内**「改 → 跑 → 还原」;还原判据 = **SHA-256 逐字节**
(不是 `grep` 变异串)。锚点预检 7/7 唯一命中(`count == 1`),任一锚点不唯一即该轮跳过并记 `anchor` 失败。

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 删掉 `TryEnqueue` 里的**有序插入**(退化为追加) | **1 红**:`..._outOfOrderInput_drainsInTotalOrder` | exact |
| **M2** | 超界分支从「丢弃 + 告警」改成**抛异常** | **1 红**:`..._overflow_dropsAndWarns` | exact |
| **M3** | 删掉 `maxLag < 1` 构造期守卫 | **1 红**:`..._zeroMaxLag_throwsAtConstruction` | exact |
| **M6** | `Drain` 不 `Clear`(⇒ 可重放) | **1 红**:`..._outOfOrderInput_drainsInTotalOrder` | exact |
| **M4** | 删掉 `Subscribe` 的 `null` 守卫 | **1 红**:`..._subscribeNull_throws` | exact |
| **M5** | `Subscribe` 的 `Add` 改成 `Clear(); Add()`(追加 → 替换) | **1 红**:`..._secondSubscribe_appends` | exact |
| **M7** | `Publish` 只 `Drain` 不派发 | **2 红**:`..._publish_reachesSubscriber` + `..._secondSubscribe_appends` | exact |

**结果:7/7 转红,7/7 SHA-256 逐字节还原**(每轮 `RESTORE OK` + 收尾 `FINAL-OK`)。

**变异表说明**:
- **M1 / M6 都打判据 ③**(不同断言面):M1 打「输出升序」,M6 打「Drain 后不重放」。
  两者各自独立转红 ⇒ 判据 ③ 的两半**都有判别力**,不是一条断言撑两件事。
- **M7 打 2 条**(因为它同时断了「收到」与「两个都收到」的共同前置)。
  ⚠️ 这**不是**「一条变异覆盖两 AC」的借绿 —— 两条断言**独立存在**,只是共用同一个执行体。
- **未做的变异**:判据 ① / ② 是**结构性断言**(命名空间 + 方法集合),没有可变异的生产守卫
  —— 它们打的是「类型住哪、有几个方法」,变异等于把文件搬走或改签名,那已不是证伪而是重写。
  如实登记:① / ② **无专打变异**。

---

## 五、回归

| 套 | 读数 | 文件 / 时刻 |
|---|---|---|
| **本件过滤(首绿)** | `8 / 8 / 0 / 0 / 0` | `unity/Logs/tdn01c.xml` |
| **EditMode 全量** | **`6606 / 6569 / 0 / 0 / 37`** | `unity/Logs/tdn01-em-full.xml` @ `2026-11-02 15:42:49 CST` |
| **PlayMode 全量** | **`299 / 295 / 0 / 2 / 2`** | `unity/Logs/tdn01-pm-full.xml` @ `2026-11-02 15:43:58 CST` |

**等式**:`6606 − 6598 = 8` = 本件新增 8 条 `[Test]`(基线 `6598 / 6561 / 0 / 0 / 37` =
上一批 `S23-21`)。`passed` 同步 `6561 → 6569`。**PlayMode `+0`**,`failed 0`。

⚠️ **`PM exit=2` 是本仓 PlayMode 常态退出码**(承 `S23-18` 的记录)⇒ 判据取 XML 的 `failed` 计数。
本次 `failed=0`,`exit=2` 与之一致(该码对 `failed=0` 与 `failed=2` 都出现,**零判别力**)。

---

## 六、证明不了的事(禁借绿)

1. **不测网络** —— `LocalReplayPipe` 零网络。本件证明的是「契约形状**可被实现**」,
   **不是**「网络层能工作」。P1b 接 NGO / Fusion 后须重测真实乱序面。
2. **不测「乱序在本地路径上出现」** —— 本地 `Publish` 按调用序入队,**路径上永远不乱序**。
   乱序面只在 `ReorderBuffer` 的直接夹具里造(判据 ③ 即如此)。⇒ 「本地路径无乱序」
   不是被测出来的,是**结构决定的**,本件不为它背书。
3. **`Subscribe` 的「排队到主线程」义务(ADR-001 F3)在本地是 no-op** —— 本来就在主线程。
   该义务**未被本件验证**,归 P1b。
4. **`MaxLag` 的默认值 32 不是裁定的值** —— ADR-001 `:145` 只说「缓冲有界(`MaxLag` 旋钮)」,
   **未给默认值**。本件的 32 是本地占位的取舍,数值归 P1b / 45 的调参轮。
5. **`Drain` 的「就绪前缀」口径不是最优算法** —— 本实现「入队即全量排出」(见 `Drain` 类注)。
   它满足 ADR-001 的三条契约(有界 / 全序 / 无丢无重放),但**不是**理想实现
   (理想实现会在已知窗口边界时提前输出)。算法归 P1b。

---

## 七、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | `IReplayPipe.cs:168` 写 `var out = new List<SimEvent>(...)` —— **`out` 是 C# 保留关键字** ⇒ 三处编译错(CS1002 / CS1513 / CS1525) | 改名 `ready` |
| 2 | 夹具 `:243` 写 `pipe.Publish(in MakeEvent(...))` —— `in` 实参**不能接返回引用的表达式**(CS8156) | 拆成局部变量 `SimEvent sent = ...; pipe.Publish(in sent);` |
