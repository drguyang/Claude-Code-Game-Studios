# TD-persist-05 动作 ① 兑现 —— AC-7a-07 异步写盘 + 缓冲池 + IL 门 + 崩溃 harness

**日期**:2026-10-25
**债项**:`TD-persist-05` 动作 ①(`AC-7a-07`)
**权威来源**:`design/gdd/persistence-service.md:507-514`(AC-7a-07)· `:117-123`(规则五)·
`:230-245`(写入状态机)· `:417`(`WRITE_RETRY_DELAY`)· `:244`(退出保存 join)
**用户裁定**(2026-10-25,五项):注入调度器 · Compose 返回未签名 · 四项全做 ·
高水位受理时推 · `writeScheduler` null ⇒ fail-closed

---

## 〇、本轮范围(用户裁定「全做」)

| # | 内容 | 状态 |
|---|---|---|
| 1 | `AC-7a-07` 本体(异步写盘 + SHA256 后台 + `IOException` 退避) | ✅ |
| 2 | 缓冲池复用(`SaveBufferPool`) | ✅ |
| 3 | Unity API 零调用 IL 门(`SaveAsyncBoundaryGate`) | ✅ **含探针轮** |
| 4 | `AC-7a-06` 崩溃注入 harness(`tools/savecrash/`) | ✅ **含反证** |

---

## 一、旋钮归属订正(本轮最重要的口径修正)

`TD-persist-06` 轮把 `SAVE_EXIT_RETRY_TIMEOUT` 当成了**重试循环总预算**。
同步写下这恰好等价,异步化后立刻分叉。GDD 两处原文划清了它们:

| 旋钮 | 值 | GDD 出处 | 真实语义 |
|---|---|---|---|
| `SAVE_EXIT_RETRY_TIMEOUT` | 1000 ms | `:416` / `:244` | 退出保存**等 worker 的 join 上限** |
| `WRITE_RETRY_DELAY` | 100 ms | `:417` | `IOException` **单次退避间隔** |

GDD `:244` 逐字:「退出保存:从 `WritingAsync` 起限时 join」—— join 是**等**,
不是**重试**。本轮据此把两者分开实现。

---

## 二、实现落点

### 2.1 新增 `unity/Assets/Sim.Codec/SaveWriteQueue.cs`

后台写盘 worker。**持有 `Action` 工作项而非 `byte[]`** —— 一份存档在后台要做三件事,
且必须**同线程同序**:算 SHA256 → 原子写 → 推高水位。持 `byte[]` 会强迫这三步拆开、
重新跨线程交接。

```csharp
public const int DefaultRetryDelayMillis = 100;   // WRITE_RETRY_DELAY
public const int DefaultMaxAttempts = 5;          // 实现纪律,不立旋钮
public SaveWriteQueue(Action<Action> scheduler, int retryDelayMillis = ..., int maxAttempts = ...)
public void Enqueue(Action work)        // 锁内入队 + 单飞守卫,锁外调度
public bool WaitForIdle()               // 空参 —— 不违反 AC-7a-08 判据①
internal bool WaitForIdle(int timeoutMillis)
```

**三条设计约束,每条都有具体理由**:

1. **不碰 File API** —— `SaveWritePathGate`(`AC-7a-08③`)要求 `Sim.Codec` 内除
   `SaveService` 外零写盘调用。写动作必须以委托注入。
2. **锁外调度** —— 若在锁内调 `scheduler`,同步内联调度器会**自死锁**。
3. **顶层全兜 try/catch** —— GDD `:513` 要求后台 worker 不得让异常逃逸
   (否则 `Task.Run` 的未观察异常会在 GC 时终结进程)。

### 2.2 新增 `unity/Assets/Sim.Codec/SaveBufferPool.cs`

**不用 `ArrayPool<byte>.Shared`** 的理由是具体的:`ArrayPool` 返回的数组**可能超长**,
而 `AddChecksum` 会把**残留尾部**算进 SHA256 ⇒ **静默坏档**。故按**精确长度**分桶。

```csharp
public const int DefaultMaxRetainedBufferBytes = 8 * 1024 * 1024;
public const int DefaultMaxRetainedTotalBytes = 32 * 1024 * 1024;
public byte[] Rent(int length)      // 精确长度
public void Return(byte[] buffer)   // 超上限直接丢弃
public int RetainedBytes { get; }
```

**如实登记的覆盖范围**:覆盖 `Compose` 的 combined 与 `AddChecksum` 产出;
**不覆盖** `CodecWriter` 的内部增长缓冲。

### 2.3 改 `unity/Assets/Sim.Codec/SaveService.cs`

`Checkpoint` 改为「主线程组版 → 入队 → 立即返回」:

```csharp
byte[] bytes = _compose(slot) ?? throw ...;      // 主线程
_writeQueue.Enqueue(() =>
{
    byte[] signed = AddChecksum(bytes, _bufferPool);   // 后台
    try { SaveBytes(slot, signed); }
    finally { _bufferPool.Return(signed); }
    _bufferPool.Return(bytes);                   // ⚠️ 只在成功后归还
    _ledger.OnCheckpointCommitted(slot);         // 高水位:写成功才推
});
```

⚠️ **`bytes` 只在成功后归还** —— 首版把归还放进 `finally`,重试时
`AddChecksum(bytes, …)` 会读到**已被池回收覆写**的缓冲 ⇒ 静默坏档。
这是本轮我自己写出又自己抓到的缺陷。

`SaveOnExit` 改为 `Enqueue` + `WaitForIdle(_exitSaveRetryMillis)`,
成功清标志位 / 失败置标志位 + `ReportExitSaveFailure`。

### 2.4 改 `unity/Assets/Gameplay.Boot/SaveComposer.cs` / `CompositionRoot.cs`

`Compose` 签名加 `SaveBufferPool bufferPool = null`,`combined` 从池租,
**返回未签名**(SHA256 归 worker)。`CompositionRoot` 注入
`writeScheduler: action => Task.Run(action)`。

---

## 三、`AC-7a-06` 崩溃注入 harness —— 判别力是本轮最难的部分

### 3.1 为什么能脱离 Unity batch

实测 `Sim.Contracts` + `Sim.Codec` **零 `using UnityEngine`、零 `#if UNITY`**,
asmdef 声明 `noEngineReferences: true` ⇒ 纯 BCL 代码,可直接 dotnet 8 编译
(0 错 0 警告)。崩溃注入要反复起停进程,放 batch 里代价极高。

### 3.2 四版迭代 —— 前三版都失败了,失败方式各不相同

| 版本 | 做法 | 结果 | 根因 |
|---|---|---|---|
| v1 | 父进程轮询 `FileInfo` 抓中间态 | **非原子写也全绿** | 8 MiB write+fsync 仅 **10 ms**,轮询抓不到 |
| v2 | 子进程写 slot 1..2000 | `bak` 恒缺失 | 按 mtime 取槽,文件系统精度不足 |
| v3 | `FindCrashSlot` 优先「有 bak 无 final」 | 2/8 FAIL | 每槽只写一次 = **首次写不建 bak**,崩溃后零档是**合理**的 |
| v4 | 同 slot 反复写 | 8/8 PASS | ✓ |

**v3 的教训最值得记**:判据前提错了 —— AC-7a-06 的语义前提是
「**有一份已有的好档要被覆盖**」,不是「任意时刻崩溃都须有档」。

### 3.3 v4 仍不够 —— 反证暴露「命中」是自造的

v4 全绿后做反证(非原子写),结果**仍然全绿**。追下去发现两层问题:

1. **`Hit` 是假的**:`tmp-written` 分支只是 `Thread.Sleep(2..8)` 后 `return true` ——
   **它没有观测任何东西**。「命中=是」是自造的。这是本项目抓过的典型假绿形态:
   **自造断言顶替 AC 语义层**。
2. **写盘窗口被稀释**:子进程每轮循环里「建 8 MiB 载荷 + 算 SHA256」远长于写盘 10 ms
   ⇒ 20 轮里只有 **1 轮**真落在写盘窗口(判别力 5%),而报告却写「屏障命中 8/8」。

### 3.4 第三版修法 —— 让「命中」可证

- **载荷只建一次**(循环只做写盘)⇒ 写盘占循环时间绝大部分。
- **标记文件**:子进程每轮写盘**前后**建/删 `WRITING`。父进程见到标记即 kill,
  **杀后复核标记仍在**(进程已死,没人能删它)⇒ 这才构成证明。
  标记消失 ⇒ 本轮 `Hit = false`,**计 SKIP,不计入任何判据分子/分母**。

### 3.5 判别力实测(同一变异,前后对比)

| | 命中率 | 失败轮次 | 判别力 |
|---|---|---|---|
| 修复前(自造命中) | 20/20「命中」 | 1 | **5%**(1/20) |
| 修复后(可证命中) | 8/8 | **3** | **37.5%**(3/8) |

⚠️ **如实登记**:37.5% 仍是**概率性**判据,不是「必然抓到」。它的价值在于
**正确实现 8/8 全绿、双保护全缺 3/8 转红** —— 两边分得开。
单拆任一道保护**不转红**(见 3.6),这是**纵深冗余的正确表现**,不是缺陷。

### 3.6 反证的三种变异 —— 只拆一道不红,是设计不是 bug

| 变异 | 内容 | 结果 |
|---|---|---|
| A | 只改非原子写(保留 bak) | **PASS** — bak 仍有效 |
| B | 只拆 bak(保留原子写) | **PASS** — final 仍有效 |
| C | **两道全缺** | **FAIL**(3/8 转红)✓ |

AC-7a-06 判据是「final **或** bak 至少一份有效」⇒ 任一道单独存在即满足。
**保护是纵深冗余的**,故反证必须同时拆两道。这一发现本身证明了 harness 在
正确实现的保护结构上是**对得上**的。

---

## 四、`AC-7a-07` IL 门 + 探针轮

### 4.1 门的如实自陈

`SaveAsyncBoundaryGate` 文件头**明写**:在 b2 门绿时**本门不可能红**(它是 b2 的推论)。
立门理由三条(方法级判据面 / 搬家检测 / GDD 字面要求的是方法级集合),
盲区三条(深度 1 / 反射 / b2 推论)。

### 4.2 探针轮三件套(CLAUDE.md §构建前门)

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串在**构建日志** | `[PROBE-AC-7A-07]` 出现 2 次(`build-StandaloneLinux64-1792913448749.log:1218,1291`) |
| 2 | 栈帧归属 | `DaYiJingCheng.EditorTools.Gates.AssemblyGates/BuildGate:OnPreprocessBuild (…) (at Assets/Editor.Tools.Gates/AssemblyGates.cs:1291)` |
| 3 | 构建**因它失败** | `Build Finished, Result: Failure.` + `构建前门失败 **1 条**` |

**`1 条` 是关键** —— 门链无其他错误,失败的唯一来源就是这个探针。

⚠️ **栈帧给的是 `Debug.LogError` 行(`:1291`),不是接线行(`:171`)**。
「接线行被执行」是**论证**(`:171` 是 `OnPreprocessBuild` 内唯一的
`SaveAsyncBoundaryGate.RunAll()` 调用点),**不是观测** —— 不得写成直接证据。

### 4.3 还原判据

SHA-256 逐字节,不是 grep:

| 文件 | 探针轮时点基线 | 备注 |
|---|---|---|
| `SaveAsyncBoundaryGate.cs` | `4ed7075e245ca3efb00c895b30393c58bca097358f1babc1cb4002b13cadd3a0` | 探针轮后未再改动,现值即此 |
| `SaveService.cs` | `0fceb92e09e531ec8643c7821aa7f79451ac59c87725cdfe7b9f005db1de7edd` | ⚠️ **探针轮时点值,已过时** —— 探针轮之后 §6.3 的 B 类缺陷修复(written 判据)又动了本文件,现值 = `138c635f1cff693c68f401f274d1043cd519250030f11b2e966fbee14c642278`。证伪轮的还原判据以此现值为准(MA1–MA4 变异的是 `SaveWriteQueue.cs`,基线 `5ed8cf56…`) |

> ⚠️ **2026-10-25 证伪轮批次 2 的中断事故(如实登记)**:批次 2 首跑因外层 10 分钟超时被杀,
> 杀在 MA7 还原之前 ⇒ `SaveService.cs` 留下 MA7 变异残留(1 处 `FALSIFY-` 串,哈希 `f8f79349…`)。
> 处置:逐处还原(非 `git checkout`)+ 全文 diff 核对「每一处增删均为本轮已知实现改动」+
> `grep -c FALSIFY-` 归零。此事故正是「证伪轮窗口内停后必查残留」纪律的立项缘由的再验证。

---

## 五、测试面

新增 `unity/Assets/Tests/EditMode/PersistenceService/save_async_write_test.cs`
(**23 条**,8 个分组)。判据形状是**捕获 `ManagedThreadId` 并与主线程比对**,
不是「`Thread.Sleep` + 文件出现了」—— 后者既慢又不能区分「后台写的」与
「主线程写的但慢」。

**对照组是本文件的核心防线**:
`test_checkpoint_inlineScheduler_staysOnMainThread_controlGroup` 用同一观测手法、
换成同步内联调度器,要求写线程 **=** 主线程。缺了它,`≠` 可能只是调度器换了个线程。

机械改动的既有测试:`save_slot_contract_test` / `save_exit_retry_and_flag_test` /
`save_replay_roundtrip_test` / `save_composer_wiring_test` / `save_authority_orthogonality_test` /
`folded_predicate_test`(构造器形参 10 → 12)。

---

## 六、回归(三元组)

### 6.1 第一轮全量(暴露 13 条失败)

三元组:截至 `2026-10-25 15:34`(本地)· 批次 `td07-1 全量` · commit `3ca1920`(工作树含本轮未提交改动)

```
total="4963" passed="4915" failed="13" inconclusive="0" skipped="35"
```

**基线 = `4940 / 4905 / 0 / 35`**(`td06-6` · `2c65e26`)⇒ **13 条回归,不得放过**。

### 6.2 13 条失败的三类根因(逐条查过,不是「大概」)

| 类 | 条数 | 根因 | 性质 |
|---|---|---|---|
| **A** 门清单未登记 | 2 | `SaveAsyncBoundaryGate` 未写进 `gate_wiring_manifest_test` 的 `EntryWiringManifest` / `ClassVerdict` | 设计内的强制点,**必须补登记** |
| **B** 退出保存标志位不置 | 6 | **生产代码真缺陷**(见 6.3) | **本轮最重的发现** |
| **C** 存档头字节错位 | 5 | 测试夹具仍按旧口径**自己签名**,worker 又补一次 ⇒ 双重签名 | 语义变更未同步测试 |

### 6.3 B 类 —— 异步化引入的**静默失效**(生产缺陷)

`SaveOnExit` 首版只判 `WaitForIdle(...)`:

```csharp
bool written = _writeQueue.WaitForIdle(_exitSaveRetryMillis);
```

这在**同步**写下恰好等价于「写成功」(失败以异常穿栈)。异步化后信号**丢了**:

- worker 的 `TryRunWithRetry` 把异常**吞进 `_lastFailure`**,`Drain` 照常 `_outstanding--`;
- ⇒ `WaitForIdle` 返回 **true**;
- ⇒ 走**成功分支** ⇒ **清位**。

**后果**:写盘失败时「会话未完整」标志位**不置** ⇒ 下次启动**不提示** ⇒
AC-7a-17 的兜底**静默失效**。而且它**不报错** —— 正是本项目最忌的失效模式。

**修法**(`SaveService.cs`):

```csharp
bool drained = _writeQueue.WaitForIdle(_exitSaveRetryMillis);
bool written = drained && _writeQueue.LastFailure == null;
```

「队列空了」是**时序**事实,「没失败」是**结果**事实 —— 两个条件缺一不可。

⚠️ **这个缺陷是回归抓出来的,不是设计时想到的**。它证明全量回归不是走过场。

### 6.4 C 类 —— 夹具口径与生产契约分叉

`MinimalSaveBytes` 第 63 行自己调了 `AddChecksum`,而 `compose` 的契约已改为
**返回未签名**(用户裁定)。worker 再补一次 ⇒ 头错位,`Load` 报
「存档版本 3637505255 > 当前 1」。

修法:拆成两个夹具 —— `MinimalSaveBytes`(未签名,给 `compose`)与
`SignedSaveBytes`(已签名,给 `SaveBytes` 与 `SaveHeaderCodec.Read`)。
**一个夹具兼两职正是根因**,所以不是「改个返回值」而是拆开。

### 6.5 修复后回归(终态)

三元组:截至 `2026-10-25 07:40 UTC` · 批次 `td07-2 全量` · commit `3ca1920`(工作树含本轮未提交改动)

```
total="4963" passed="4928" failed="0" inconclusive="0" skipped="35"
```

**基线 = `4940 / 4905 / 0 / 35`**(`td06-6` · `2c65e26`)

| 项 | 基线 | 本轮 | 差 |
|---|---|---|---|
| total | 4940 | 4963 | **+23** |
| passed | 4905 | 4928 | **+23** |
| failed | 0 | **0** | 0 |
| skipped | 35 | 35 | 0 |

**等式校验**:`4963 − 4940 = 23` = `save_async_write_test.cs` 的 `[Test]` 数(23)✓
⇒ **零回归,新增 23 条全部通过**。

**复算命令**:
```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/td07-final2.xml | head -1
grep -c "\[Test\]" unity/Assets/Tests/EditMode/PersistenceService/save_async_write_test.cs
```

### 6.5b 门行为测试追加后的回归刷新(`td07-3` · 终态)

三元组:截至 `2026-10-25 08:39 UTC` · 批次 `td07-3 全量` · commit `3ca1920`(工作树含本轮未提交改动)

```
total="4966" passed="4931" failed="0" inconclusive="0" skipped="35"
```

**基线 = `4963 / 4928 / 0 / 35`**(`td07-2` · §6.5)

**等式校验**:`4966 − 4963 = 3` = `save_async_boundary_gate_test.cs` 的 `[Test]` 数(3)✓
⇒ 门守卫 ⓪ 修复 + 门行为测试 3 条**零回归**。

**复算命令**:
```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' \
  /home/gu/.claude/jobs/181888b1/tmp/td07-final3.xml | head -1
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Sim/save_async_boundary_gate_test.cs
```

### 6.6 中间轮(如实留档)

| 轮次 | 结果 | 说明 |
|---|---|---|
| `td07-1` 全量 | `4963/4915/13/35` | 13 条回归(见 6.2) |
| `td07-fix1` 持久化组 | `228/227/1` | 13 → 1(roundtrip 双重签名) |
| `td07-2` 全量 | **`4963/4928/0/35`** | 零失败 |

---

## 七、证伪轮

**纪律**:每条变异「改 → 跑 → 还原」在**同一进程内**完成(`falsify_run.py` / `falsify_extra.py`),
还原判据 = **SHA-256 逐字节**(不是 grep 变异串)。

### 7.0 结果总表(23 条测试 × 20 变异 + 2 门变异)

**被测夹具**:`save_async_write_test.cs` 23 条。批次 1–3 跑 MA1–MA19(除 MA10/MA11 首跑无效),
批次 4 补跑 MA10/MA11/MA20。

| ID | 变异 | 结果 | 转红条目 |
|---|---|---|---|
| MA1 | `Drain` 改并发(去 one-writer 单飞) | **9 红** | returnsBeforeDiskWrite / signedFileValidates / usesInjectedPool / highWaterMark / exhausts / isRetried / nonRetriable / **twoCheckpoints_neverWriteConcurrently** / writeFailure |
| MA2 | 对所有异常重试 | 1 红 | nonRetriableException_isNotRetried |
| MA3 | 去掉退避 | 1 红 | ioException_isRetriedWithWriteRetryDelay |
| MA4 | 尝试次数少一次 | 2 红 | exhaustsAttemptsThenGivesUp + isRetried(同源连带:第 3 次成功的场景只试了 2 次) |
| MA5 | 高水位受理时推 | 2 红 | highWaterMark + writeFailure |
| MA6 | 写失败被吞(高水位照推) | 1 红 | writeFailure_doesNotAdvanceHighWaterMark |
| MA7 | worker 不补校验和 | 2 红 | signedFileValidatesChecksum + joinsWorkerAndWritesFile |
| MA8 | Checkpoint 改回同步 | 6 红 | controlGroup / returnsBeforeDiskWrite / offMainThread / highWaterMark / joinsWorker / writeFailure |
| MA9 | 兜底不清 outstanding | 1 红 | joinsWorkerAndWritesFile(⚠️ 与计划预期 `workerFailure_outstandingDropsToZero` 不同名 —— 该名测试不存在,判据由 joinsWorker 的 join 超时路径承载) |
| MA10 | Drain 去顶层全兜 | **单拆不可红(纵深冗余的正确表现,非缺陷)**:内层 `TryRunWithRetry` 的全 catch 已兜住一切工作项异常 ⇒ 外层被遮蔽,单拆不改变可观测行为。组合反证 **MA10b**(内层 rethrow + 外层删)见下 |
| MA10b | **双层兜底全拆**(内层 rethrow + 外层 try/catch 删) | **3 红** ✓ | workerSwallowsException_queueStaysUsable + workerFailure_outstandingDropsToZero(设计目标)+ nonRetriableException_isNotRetried(连带) |
| MA11 | SaveOnExit 改回只判排空 | **首跑绿 = filter 漏**(判据组不在单 fixture filter 内)→ 批次 4 宽 filter 重跑 **5 红** | flag 组 3(clearWritesEmptyTombstone / raisedByFailure_thenCleared) + writeFailure 组 3 中 2(flagRaisedEvenWithoutSink / isIdempotent / raisesFlagAndDoesNotThrow) |
| MA12 | join 改无限等 | 1 红 | joinTimeout_raisesFlagInsteadOfThrowing |
| MA13 | 去 fail-closed | 2 红 | checkpoint/saveOnExit_withoutWriteScheduler_failsClosed |
| MA14 | 池不复用 | 1 红 | bufferPool_reusesSameLengthBuffer |
| MA15 | Rent 返回错误长度 | 1 红 | bufferPool_doesNotReuseDifferentLength |
| MA16 | 池不丢超限块 | 1 红 | bufferPool_dropsOversizedBuffer |
| MA17 | SaveOnExit 改回同步 | 7 红 | joinTimeout + flag 组 3 + writeFailure 组 3 |
| MA18 | 成功分支不清标志位 | 3 红 | joinsWorker + clearWritesEmptyTombstone + raisedByFailure_thenCleared |
| MA19 | 门禁用前缀表清空 | **绿(预登记预期内)** —— Sim.Codec 本就零 Unity 调用,门是 b2 推论,清空不可红。不冒充 |
| MA20 | 门 worker 清单清空 | **首跑绿 ⇒ 抓出真缺口**(见 7.2);修门(守卫 ⓪)+ 新增门行为测试后重跑 **4 红** | asyncBoundaryGate_greenOnRealCodecDll / workerList_coversRealTypes(新增门测试,正是设计载体)+ runAll_zeroErrors + adr031_v4_b5GateStillPassesOnRealTree |

### 7.1 假绿夹具 —— MA1 抓出的第二条真发现

`test_twoCheckpoints_neverWriteConcurrently` 首版把并发计数放在 **`compose`** 里。
而 `compose` 在**主线程串行**执行(两次 `Checkpoint` 是顺序调用)
⇒ `maxConcurrent` **恒为 1**,对 worker 的并发**零判别力**。

**实证**:MA1 首跑(`Drain` 改成并发跑)下,本测**仍然绿**,而同轮另 8 条转红。
首版注释里那句「本测观测的是写侧」是**自述**,实现没做到 —— 又一次
「自造断言顶替 AC 语义层」。

**修法**:改为直接对**队列**压两个工作项,在**工作项体内**计数。
工作项由 worker 执行 ⇒ 并发度就是 worker 的并发度。
**修复后 MA1 重跑:本测转红** ✓(批次 2,9 红清单含 twoCheckpoints)。

⚠️ 这条是**证伪轮的价值所在**:全量回归 23 条全绿时,它是假绿;
只有把生产代码改坏才暴露。

### 7.2 MA20 抓出的门真缺口 —— 空清单逃逸

MA20(清空 `WorkerTypeFullNames`)期望**覆盖守卫**转红,实测**绿**。

排查:覆盖守卫**存在**(`SaveAsyncBoundaryGate.cs:152-165`,seen 集合比对)——
不是「自述有门但门不存在」;缺口在**判据形状**:守卫判据是
「清单**每一项**都找得到」⇒ **清单被清空时 foreach 零项 ⇒ 零断言 ⇒ 逃逸**。
类型改名 / 搬走会红,「整表清空」不红。

**修法**(`SaveAsyncBoundaryGate.cs`,覆盖守卫前新增守卫 ⓪):

```csharp
if (WorkerTypeFullNames.Length == 0)
{
    errs.Add("[AC-7a-07] WorkerTypeFullNames 为空 —— 扫描清单被清空 ⇒ 本门对写盘路径\n" +
             "            零覆盖,拒以空清单冒充绿。…");
}
```

**连带补强**:门此前只有清单登记(`gate_wiring_manifest_test` 字符串条目),**零行为测试**
—— 新增 `unity/Assets/Tests/EditMode/Sim/save_async_boundary_gate_test.cs` **3 条**
(真实 dll 绿断言 / 产物缺失不静默绿 / 清单非空自检)。其中 `test_asyncBoundaryGate_greenOnRealCodecDll`
= **MA20 的转红载体**(清单被清空 ⇒ 门红 ⇒ 本测红)。

### 7.3 工具链三处登记(非产品缺陷)

- **MA10 首跑无效**:变异只删 `try` 留下孤儿 `catch` ⇒ 编译失败 ⇒ 无 XML。
  已改成对替换(删 try + 删 catch)。
- **MA10 二跑拒变异**:驱动器 `apply_mutation` 的 PAIRS 分支**硬编码 `path = SVC`**
  (为 MA8 设计)⇒ MA10 的 QUEUE 锚串在 SVC 里找,0 命中。修法:PAIRS entry 支持
  第 4 元指定目标文件。**证伪轮驱动器自身的 bug 也是证伪轮的一部分,如实登记。**
- **MA11 首跑绿**:根因 = `testFilter` 只含 `SaveAsyncWriteTest`,而 MA11 的判据组
  `SaveExitRetryAndFlagTest`(**AC-7a-17 面**)不在 filter 内 —— **未跑目标即绿**。
  批次 4 起证伪轮 filter 扩为 `DaYiJingCheng.Tests` 全前缀。教训:**证伪轮的 filter
  必须覆盖每个变异的判据测试所在 fixture**,预登记计划里的「期望转红」列就是核对依据。

### 7.3b MA10 单拆不红 = 纵深冗余,非假绿(与崩溃 harness §3.6 同构)

MA10 三跑(修好驱动器后)**绿,0 failed** —— 且这次不是工具问题:

`Drain` 有**双层兜底**:内层 `TryRunWithRetry` 的不可重试 `catch (Exception)` 已经
接住**一切**工作项异常 ⇒ 拆掉外层 try/catch **不改变任何可观测行为** ⇒ 变异无判别面。

这与 §3.6 崩溃 harness 的发现**同构**:AC-7a-06 判据是「final **或** bak 至少一份有效」,
单拆任一道保护不红 = **纵深冗余的正确表现**。此处同理:GDD `:513`「后台 worker 顶层
全兜」的**能力**由两层兜底的**并集**承载,任一层单独存在即满足。

**正确反证 = 组合变异 MA10b**(内层 rethrow + 外层 try/catch 全删):异常才真能逃逸
线程体 ⇒ `_outstanding` 卡在 1 ⇒ `WaitForIdle` 超时 ⇒
`test_workerSwallowsException_queueStaysUsable` 与 `test_workerFailure_outstandingDropsToZero`
两条应转红(结果见 7.0 表 MA10b 行)。

### 7.4 判别力结论(批次 1–5 合并)

- **21 条变异**:20 条红、1 条绿(MA19,**预登记预期内** —— 门是 b2 的推论,
  如实登记不冒充);0 条假绿。
- **23 条被测测试 + 3 条门行为测试 = 26 条,全部至少转红一次** ✓
  (门测试 3 条中 `greenOnRealCodecDll` / `workerList_coversRealTypes` 经 MA20 转红;
  `missingDll_isNotSilentlyGreen` 的专属变异面 = 删除 dll 存在性检查,本轮**未单独反证**
  —— 见 §八)
- 全部轮次还原 SHA-256 逐字节成功(基线:`SaveService 138c635f` / `SaveWriteQueue 5ed8cf56` /
  `SaveBufferPool bcbac6e1` / `SaveAsyncBoundaryGate 07fb95f5`(修门后新基线))

## 八、如实登记的缺口

1. `AC-7a-07` 的**「Unity API 零调用」在 b2 绿时不可能红** —— 本门是推论不是独立判据。
2. `SaveAsyncBoundaryGate` 是**深度 1** 的近似(看不到第二层被调方法内部)。
3. 崩溃 harness 判别力 **37.5%**,是概率性判据。
4. `SaveBufferPool` **不覆盖** `CodecWriter` 的内部增长缓冲。
5. `Sim.Codec` 之外的 `SaveBytes` 低层原语**未纳入本轮异步化**(承 TD-persist-04 裁定)。
6. 门行为测试 `test_asyncBoundaryGate_missingDll_isNotSilentlyGreen` 的**专属变异面**
   (删除 `CheckAsyncBoundaryIl` 开头的产物存在性检查)本轮**未单独反证** ——
   该测试的绿/红断言形态简单(参数直接注入),但按「每条守卫至少一条变异转红」的
   口径它今日是 **NOT-RUN**,不得计入「全部转红」的分子。
7. `Drain` 顶层兜底被内层 `TryRunWithRetry` 全 catch 遮蔽 ⇒ **外层在当前代码形态下
   对工作项异常不可达**(其存在意义 = 防 worker 框架代码自身抛)。MA10b 组合变异
   证明「双层全拆」可红,但外层单守卫的**独立**判别面今日为空 —— 若将来内层
   收窄(如只 catch 指定类型),外层即恢复可达,届时无需改测试。
