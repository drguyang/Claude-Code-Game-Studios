# `TD-persist-06` 兑现 —— `AC-7a-17`(退出保存失败:限时重试 + 会话未完整标志位)

**日期**:2026-10-25
**债**:`docs/tech-debt-register.md` §`### TD-persist-06`(全表唯一一条 `NOT_IMPLEMENTED` 的 AC)
**AC**:`design/gdd/persistence-service.md:581-586`(`AC-7a-17`,BLOCKING)· 规则十三 `:195-198`
**ADR**:ADR-010 §六(退出保存)
**性质**:本件是**实现轮证据**,不是评审报告;判据 = 测试 + 证伪轮,不是「代码看着对」。

---

## 一、用户裁定(两问,均在本轮开工前)

| 问 | 裁定 | 理由(摘要) |
|---|---|---|
| **Q1** 标志位形态 | **独立标志文件**(`session_incomplete.flag`) | 头字段 = 改 `SaveHeader` 七字段布局 ⇒ `FixedSize` 变 ⇒ 黄金夹具全红 ⇒ 应 bump `SaveVersion` ⇒ 触发迁移链义务;而 **v1 从未出货**,为标志位 bump 版本会逼出一条只为标志位存在的迁移脚本,正是 `TD-persist-05` 拒绝过的「造假 v0」同型。且标志位是**跨会话元信息**,不是世界状态 —— 进存档头会让 `bak` 回退把标志位一起回退(标志位本身失真)。 |
| **Q2** 范围 | **只做 `AC-7a-17`** | 不做 `AC-7a-07`(异步写盘)。今日写盘是同步的,没有后台 worker 可 join —— `07` 是另一条债。 |
| **Q3**(中途追加) 标志位撞 IL 门 | **走 `WriteAtomic`** | 见 §三。 |

---

## 二、`AC-7a-17` 逐句对账

GDD `:581-586` 原文四段,逐句落到执行体:

| GDD 字面 | 落点 | 判据 |
|---|---|---|
| 「`Application.wantsToQuit` 钩子」 | `BootRoot.cs:219-234` 注册 · `:957-961` 卸挂 | ⚠️ **PlayMode / 人工面**,本件**未自动化**(见 §六 缺口 ①) |
| 「按 `SAVE_EXIT_RETRY_TIMEOUT`(默认 1s)限时重试」 | `ExitSaveRetrier.TryWrite` · `DefaultTimeoutMillis = 1000` · `SaveService.SaveOnExit:409-415` | `SaveExitRetryAndFlagTest` 6 条(① 组) |
| 「仍失败 ⇒ **不阻塞退出**」 | `SaveOnExit` 失败分支**不抛**;`BootRoot` 的 handler **恒 `return true`** | `test_saveOnExit_writeFailure_raisesFlagAndDoesNotThrow` · `test_saveOnExit_sinkThrows_stillDoesNotThrow` |
| 「**记日志**」 | `exitSaveFailureSink` 注入委托 → `CompositionRoot.cs:524` 的 `UnityEngine.Debug.LogError` | ⚠️ 委托**被调用**有夹具;`LogError` 真的出现在日志里**无自动化**(见 §六 缺口 ②) |
| 「下次启动检测到**会话未完整标志位**」 | 置位:`SaveService.RaiseIncompleteFlag`;读位:`SaveIncompleteFlag.IsRaised` → `SaveService.IsSessionIncomplete` → `BootRoot.cs:217` | `test_flag_*` 5 条 · `test_isSessionIncomplete_reflectsFlagFile` · 端到端 `test_flag_raisedByFailure_thenClearedByNextSuccessfulExitSave` |
| 「**非阻塞提示**……**提示承载归 42 / 7b,7a 只断言标志位置位**」 | **7a 侧到此为止** —— 本件不产生任何 UI | ⚠️ **如实登记缺口**:`BootRoot._sessionWasIncomplete` 今日**零消费者**(见 §六 缺口 ③) |

---

## 三、⚠️ 本轮最大的一处返工:标志位撞 `AC-7a-08③` IL 门

### 3.1 事故

首版把置位 / 清位直接写在 `SaveIncompleteFlag` 里(`File.WriteAllBytes` / `File.Delete`)。
**全量回归 `td06-3` 实测 3 条红**:

```
[AC-7a-08③] 写盘调用越出白名单:DaYiJingCheng.Sim.Codec.SaveIncompleteFlag::Raise → Directory.CreateDirectory
[AC-7a-08③] 写盘调用越出白名单:DaYiJingCheng.Sim.Codec.SaveIncompleteFlag::Raise → File.WriteAllBytes
```

门 `Editor.Tools.Gates/SaveWritePathGate.cs:151` 的判据是「`Sim.Codec` 程序集内**仅
`SaveService::WriteAtomic` 一处**可达写盘 API」。**这不是门的 bug,是我新增的代码撞了既有 BLOCKING 门。**

### 3.2 三条出路与裁定

| 出路 | 代价 |
|---|---|
| **走 `WriteAtomic`(用户裁定 ✅)** | 标志位语义由「文件存在」变为「存在**且非空**」;会残留 1 字节 `.bak`。**门一字不改**,既有不变量从「存档体只有一个写入口」变成字面成立的「`Sim.Codec` 内只有一个写盘原语」—— 比放宽门更强 |
| 放宽 IL 门 | 须改写 `test_ac7a08c_whitelistShape_isSingleTypeSingleMethod`,**AC 判面变更** ⇒ 须另落评审原件 |
| 委托注入 | 委托缺失时置位不了 ⇒ 标志位 fail-open ⇒ AC-7a-17 的兜底整条失效 |

### 3.3 落地形态

```csharp
// SaveIncompleteFlag —— 纯命名 + 纯判据,零 I/O 写
public static readonly byte[] RaisedMarker  = { 0x01 };   // 置位
public static readonly byte[] ClearedMarker = Array.Empty<byte>();  // 清位 = 0 字节墓碑
public static bool IsRaised(string basePath)   // 存在 且 长度 > 0
    => File.Exists(path) && new FileInfo(path).Length > 0;

// SaveService —— 写面收在这里,走唯一原子写原语
private void RaiseIncompleteFlag() => WriteAtomic(flag, flag.tmp, flag.bak, RaisedMarker);
private void ClearIncompleteFlag() => WriteAtomic(flag, flag.tmp, flag.bak, ClearedMarker);
```

**代价如实登记**:清位不走 `File.Delete`(它也是写盘 API),而是写 0 字节墓碑 ⇒
`IsRaised` 必须判长度,否则清位后提示永不消失。该半条由 `test_flag_emptyFile_isNotRaised`
钉住,且 **MC4 反证实测 4 条转红**(见 §五)。

---

## 四、测试抓到的一处**真缺陷**(不是我改判据能绕过的)

`test_retrier_retriesUntilSuccess` 首跑**红**:

```
最终成功 ⇒ lastFailure 为 null(不得留残留)
  Expected: null
  But was:  <System.IO.IOException: 第 2 次失败>
```

`ExitSaveRetrier` 首版在 `try { write(); return true; }` 成功路径上**没有清 `lastFailure`**,
于是「失败两次、第三次成功」会**带着第 2 次的异常返回 `true`**。调用方若据
`lastFailure != null` 判成败,会把**已落盘的成功**读成**失败** ⇒ 置上不该置的标志位
⇒ 下次启动误报「上次会话未完整保存」。

**修法 = 修生产代码**(`ExitSaveRetrier.cs` 成功分支补 `lastFailure = null`),**不是改判据**。
这条同时被 **MC1 反证**(停用该句 ⇒ 恰好这一条转红)。

> ⚠️ 这是本轮的**方法学价值**:判据写对了,它会抓到真缺陷。若当初为了「跑绿」把
> `Assert.IsNull(failure)` 删掉,这个缺陷会以全绿的形式留在盘上。

---

## 五、证伪轮:15 轮,23 条测试**全部**至少转红一次,零假绿

**方法**:每轮「改 → 跑 → **立即**还原」,还原判据 = **SHA-256 逐字节**(`td06-baseline.sha256`,4 文件)。
变异窗口内**不读也不写**被变异文件。每轮只动**一个**守卫。

### 5.1 逐轮结果(可复算,`unity/Logs/td06-<轮>.xml`)

| 轮 | 变异 | 红数 | 转红的测试 |
|---|---|---|---|
| **MC1** | 成功分支删 `lastFailure = null` | 1 | `retrier_retriesUntilSuccess` |
| **MC2** | `Math.Min(delay, remaining)` → `(int)remaining`(退避不封顶) | 2 | `backoffGrowsAndIsCapped` · `retriesUntilSuccess` |
| **MC3** | 零预算分支不执行 `write()` | 8 | `negativeBudget` · `zeroBudget` + 6 条 `SaveOnExit`/flag 下游 |
| **MC4** | `IsRaised` 长度判据 → `return true` | 4 | `flag_emptyFile_isNotRaised` · 往返 · `isSessionIncomplete` · `success_doesNotRaiseFlag` |
| **MC5** | `if (written)` → `if (false)`(停用清位分支) | 3 | 墓碑 · 往返 · `success_doesNotRaiseFlag` |
| **MC6** | 停用 `RaiseIncompleteFlag()` | 5 | 5 条失败路径置位判据 |
| **MC7** | 观察者的 `catch` → `throw`(日志器异常外泄) | 1 | `sinkThrows_stillDoesNotThrow` |
| **MC8** | 删 `RequireCompose(nameof(SaveOnExit))` | 2 | `missingCompose_throws` · `missingCompose_doesNotRaiseFlag` |
| **MC9** | 把 `RequireCompose` **移进**重试循环(**不是删,是移**) | 2 | 同上 |
| **MC10** | `Math.Min(delay, remaining)` → `delay`(退避越过截止) | 1 | `givesUpWhenBudgetExhausted` |
| **MC11** | `GetPath` 改成 `save_incomplete.flag`(撞存档命名空间) | 4 | 命名碰撞 · 非空判据 · 路径派生 · `isSessionIncomplete` |
| **MC12** | `FlagFileName` 常量改名 | 5 | 上列 4 + `fileNameDoesNotCollide` |
| **MC13** | 成功路径也 `sleepMillis(1)` | 1 | `succeedsOnFirstTry_noSleep` |
| **MC14** | `IsRaised` 首行 → `return true`(默认已置位) | 6 | `absentFile` + 5 条下游 |
| **MC15** | 删 `RequireNarrativeContext()` | 1 | `missingNarrativeContext_throws` |

### 5.2 覆盖核对(可复算)

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
tags=['MC%d'%i for i in range(1,16)]
red={}
for n in tags:
    for tc in ET.parse(f'unity/Logs/td06-{n}.xml').getroot().iter('test-case'):
        if tc.get('result')=='Failed': red.setdefault(tc.get('name'),[]).append(n)
allt=[tc.get('name') for tc in ET.parse('unity/Logs/td06-4.xml').getroot().iter('test-case')]
print(f"测试总数 {len(allt)}, 曾转红 {len(red)}")
print("从未转红:", [x for x in allt if x not in red] or "无")
PY
```

**实跑输出**:

```
测试总数 23, 曾转红 23
从未转红: 无
```

> ⚠️ 首轮 12 轮跑完时**有 3 条从未转红**(`succeedsOnFirstTry_noSleep` ·
> `flag_absentFile_isNotRaised` · `missingNarrativeContext_throwsInsteadOfRetrying`),
> 因此补跑 MC13/MC14/MC15 三轮。**这三条不是白写的** —— 它们各自守着一个独立的失败形态,
> 只是先前那 12 个变异没有触到。按「禁借绿」纪律,未获反证的测试**不得**计入已证面。

### 5.3 还原判据(逐字节,不是 grep)

```
$ sha256sum -c /home/gu/.claude/jobs/181888b1/tmp/td06-baseline.sha256
Assets/Sim.Codec/ExitSaveRetrier.cs: 成功
Assets/Sim.Codec/SaveIncompleteFlag.cs: 成功
Assets/Sim.Codec/SaveService.cs: 成功
Assets/Tests/EditMode/PersistenceService/save_exit_retry_and_flag_test.cs: 成功

$ grep -rn "FALSIFY" Assets/Sim.Codec/ Assets/Tests/EditMode/PersistenceService/
（0 处）
```

**终态跑(`td06-6.xml`)是在还原校验通过之后跑的** ⇒ 「终态全绿」对应的是**基线字节**,
不是某个变异残留。

---

## 六、⚠️ 如实登记的缺口(禁借绿)

| # | 缺口 | 现状 | 归属 |
|---|---|---|---|
| **①** | `Application.wantsToQuit` 的**注册 / 卸挂**无自动化 | 代码在 `BootRoot.cs:219-234` / `:957-961`,但 EditMode 跑不了 Unity 生命周期事件 ⇒ **本件未验证**「退出时真的调到了 `SaveOnExit`」 | PlayMode / 人工验证面。**未立债**——它是既有 `TD-persist-04` 的「生产调用点 = 0」终态的一部分,不新增 |
| **②** | `Debug.LogError` 真的出现在日志里 | 委托**被调用**有夹具(`test_saveOnExit_writeFailure_...` 的 `reported` 非空);但「日志文本出现在 player log」无自动化 | 人工面。GDD 只要求「记日志」,不要求可自动判读 |
| **③** | **`BootRoot._sessionWasIncomplete` 零消费者** | GDD `:586` 逐字「提示承载归 **42 / 7b**,7a 只断言标志位置位」⇒ **7a 的义务到此为止**。`BootRoot` 读出来存进字段,但 42 / 7b 的提示 UI 尚未存在 ⇒ **字段今日无人读** | **不是本债的缺口**,是 42 / 7b 的实现轮。⚠️ **但必须写明**:读位已接线、消费未接线 —— 这不是「静默满足」,是**半条链**。若日后有人据 `IsSessionIncomplete` 已接线就宣称「提示功能已实现」,那是假绿 |
| **④** | `AC-7a-07`(异步写盘 + SHA256 在非主线程)**未动** | 用户 Q2 裁定本轮不做。`SaveOnExit` 今日**同步**写盘 ⇒ 「限时 join」实际是「同步写 + 同步重试」,不是「等后台 worker」 | `TD-persist-05` 的 `07` 半边,仍 open |

---

## 七、回归数(三元组)

| 项 | 值 |
|---|---|
| **截至时刻** | `2026-10-25 05:17 UTC` |
| **批次** | `td06-6`(**终态**,还原校验之后) |
| **commit** | `2c65e26` |
| **EditMode** | **`4940 / 4905 / 0 / 35`**(`unity/Logs/td06-6.xml`) |
| **基线** | `4917 / 4882 / 0 / 35`(`td05-3` · `2026-10-25 04:21 UTC` · `72bb234`) |
| **差值** | **+23 / +23 · 失败 0 · 跳过 35(不变)** |

**复算命令**:

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' \
  unity/Logs/td06-6.xml | head -1
# => total="4940" passed="4905" failed="0" inconclusive="0" skipped="35"
```

**新增 23 条 = 本文件的 `[Test]` 数**,可复算:

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/PersistenceService/save_exit_retry_and_flag_test.cs
# => 23
```

⚠️ **等式须成立**:`4940 − 4917 = 23` 且 `23 = 新增 [Test] 数` ⇒ **没有夹带未声明的新增**,
也**没有测试被删**(否则差值会小于 23)。

### 7.1 中途的两次红(如实登记,不掩盖)

| 跑 | 结果 | 原因 |
|---|---|---|
| `td06-3` | `4938 / 4897 / **6** / 35` | ① `SaveIncompleteFlag` 撞 `AC-7a-08③` IL 门(3 条)② `folded_predicate_test` 构造器形参 8 → 10 未同步(1 条)③ 另 2 条 = 同一 IL 门的 `AssemblyGates.RunAll` / `PayloadDecoderTest` 连带 |
| `td06-5` | `4940 / 4905 / 0 / 35` | 修完两处后的中间全绿 |
| `td06-6` | `4940 / 4905 / 0 / 35` | **终态**(证伪轮 15 轮还原之后) |

---

## 八、改动清单

| 文件 | 类型 | 内容 |
|---|---|---|
| `unity/Assets/Sim.Codec/ExitSaveRetrier.cs` | **新增** | 限时重试驱动(假时钟可注入);`DefaultTimeoutMillis = 1000` / `InitialDelayMillis = 25` / `MaxDelayMillis = 400` |
| `unity/Assets/Sim.Codec/SaveIncompleteFlag.cs` | **新增** | 标志文件的**命名与判据**(零 I/O 写);`FlagFileName` / `RaisedMarker` / `ClearedMarker` / `IsRaised`(存在**且非空**) |
| `unity/Assets/Sim.Codec/SaveService.cs` | 改 | `SaveOnExit` 加限时重试 + 置位/清位;新增 `RaiseIncompleteFlag` / `ClearIncompleteFlag`(走 `WriteAtomic`)/ `ReportExitSaveFailure` / `IsSessionIncomplete`;构造加两形参(`exitSaveRetryMillis` / `exitSaveFailureSink`) |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 改 | `Application.wantsToQuit` 注册(恒 `return true`)+ `OnDestroy` 卸挂 + 启动期读位存 `_sessionWasIncomplete` |
| `unity/Assets/Gameplay.Boot/CompositionRoot.cs` | 改 | `exitSaveFailureSink` → `UnityEngine.Debug.LogError`(全限定,本文件零 `using UnityEngine;`) |
| `unity/Assets/Tests/EditMode/PersistenceService/save_exit_retry_and_flag_test.cs` | **新增** | 23 条 |
| `unity/Assets/Tests/EditMode/PersistenceService/folded_predicate_test.cs` | 改 | 构造器形参断言 8 → 10(前八个位置不变) |

### 8.1 一处编译错与修法(留痕)

`CompositionRoot.cs` 首版写裸 `Debug.LogError` ⇒ `error CS0103: The name 'Debug' does not exist`。
该文件**零 `using UnityEngine;`**(只引 `UnityEngine.InputSystem`)。修法 = **全限定**
`UnityEngine.Debug.LogError`,**不加整条 using**(会把 `Object` / `Random` 等裸名一并带进来)。
同 `BootRoot.cs:483` 既有先例。

---

## 九、判据小结

| 判据 | 结果 |
|---|---|
| `AC-7a-17` 的**七句字面**逐句有落点 | ✅ 六句有自动化;「不阻塞退出」有夹具;⚠️ 退出钩子注册面归 PlayMode(缺口 ①) |
| 全量回归零失败 | ✅ `4940 / 4905 / 0 / 35`(三元组见 §七) |
| 新增测试数 = 回归增量 | ✅ `23 = 4940 − 4917` |
| 每条新测试**至少转红一次** | ✅ 23/23(15 轮,§五) |
| 还原判据 = SHA-256 逐字节 | ✅ 4 文件 `sha256sum -c` 全「成功」,零 `FALSIFY` 残留 |
| 无假绿 | ✅ 首轮 3 条未转红时**补跑了三轮**,未借前 12 轮的绿 |
| 生产缺陷被测试抓到并修在**生产代码** | ✅ `ExitSaveRetrier.lastFailure` 残留(§四) |

**未做的事(如实)**:`AC-7a-07` 异步写盘(用户裁定不做)· `wantsToQuit` 的 PlayMode 验证 ·
42 / 7b 的提示 UI。
