# TD-persist-14 实现轮 —— 「会话未完整」标志位的误置

**截至时刻**: 2026-11-03 20:01 CST(12:01 UTC)
**批次**: sprint-24 收口批 · 用户指令「现在做」
**commit**: `1384dba3` + 工作树(未提交)

---

## 1. 病因(一句话)

`SaveOnExit` 的成败判据把**「写盘失败」**与**「写盘只是慢(join 超时)」**合并成同一个结论 ⇒
情形 (b) 也置「会话未完整」⇒ 下次启动报「上次会话未完整保存」,
**而那次会话正常退出、档最终也写成了**。

用户两次裁定:
1. 「上次会话未完整保存属于出现 bug,应该修复,不该作为常态提醒」
2. 「这种 bug 情况不该出现,为什么还要考虑这种情况的提示语」

## 2. 原判据(改前,逐字)

```csharp
// unity/Assets/Sim.Codec/SaveService.cs:710-711(改前行号)
bool drained = _writeQueue.WaitForIdle(_exitSaveRetryMillis);
bool written = drained && _writeQueue.LastFailure == null;
```

**两种情形合并**:
- `WaitForIdle`(`SaveWriteQueue.cs:202-214`)只在 `_outstanding == 0 && !_draining` 时返回 true,
  **超时即 false** —— 它不区分「还没跑完」与「跑完了但失败」。
- 代码自己承认了这个区分(`SaveService.cs:735` 改前,逐字):
  「⚠️ `lastFailure` 现在可能为 null(**join 超时 ≠ 写盘失败,可能只是慢**)。」
  注释**看见了**区分,但**置位动作没有据此分支**。

## 3. 修法

**判据锚在本次工作项自己的结局**,不读队列级粘性状态:

```csharp
Exception exitFailure = null;
bool exitCompleted = false;
var outcomeGate = new object();

_writeQueue.Enqueue(() =>
{
    try
    {
        byte[] signed = AddChecksum(bytes, _bufferPool);
        try { WriteExitSave(signed); }
        finally { _bufferPool.Return(signed); }
        _bufferPool.Return(bytes);
        lock (outcomeGate) { exitCompleted = true; exitFailure = null; }
    }
    catch (Exception e)
    {
        // 记录后仍要 throw —— 队列的 TryRunWithRetry 靠异常做退避重试
        lock (outcomeGate) { exitFailure = e; }
        throw;
    }
});

_writeQueue.WaitForIdle(_exitSaveRetryMillis);   // 返回值不参与成败判定

Exception writeFailure; bool completed;
lock (outcomeGate) { completed = exitCompleted; writeFailure = exitFailure; }

if (completed && writeFailure == null) { ClearIncompleteFlag(); return; }   // 成功 ⇒ 清位
if (writeFailure != null)             { RaiseIncompleteFlag(); … }          // 真失败 ⇒ 置位
// (b) 超时未观测失败 ⇒ **不置位**,只记日志
ReportExitSaveFailure(writeFailure: null, flagFailure: null);
```

**三条设计判断(逐条给理由)**:

| # | 判断 | 理由 |
|---|---|---|
| ① | 不用队列级 `LastFailure` 兜底 | 它**跨工作项粘性**;本次退出保存还没跑、而更早的 checkpoint 失败过 ⇒ 读到旧异常 ⇒ 置位归因错 |
| ② | worker 内 `catch` 记录后**仍 throw** | 队列的 `TryRunWithRetry` 靠异常做退避重试;吞掉异常会让「第一次失败、第二次成功」被记成一次干净成功 |
| ③ | 跨线程用显式锁,不靠 `WaitForIdle` 的锁边沿 | `drained == true` 时有锁边沿提供可见性;**`drained == false` 时没有** |

## 4. 夹具(改 1 条 + 增 1 条)

### 4.1 改写(语义反转)

`save_async_write_test.cs`:

| 改前 | 改后 |
|---|---|
| `test_saveOnExit_joinTimeout_raisesFlagInsteadOfThrowing` | `test_saveOnExit_joinTimeout_doesNotRaiseFlag` |

**改前断言的是错误行为**:「join 超时 ⇒ **须置**「会话未完整」标志位」。
**改后**:join 超时但未观测失败 ⇒ **不置位**,且**放行 worker 后须真的写成 + 校验通过**
(后者是「不置位」的现实依据 —— 否则只是把一条绿换成另一条绿)。

### 4.2 新增

`test_saveOnExit_staleCheckpointFailure_stillClearsFlag`

**为什么判据必须是「清位」而不是「不置位」**:两条错误路径的**可观测结局**不同 ——
置位与否都是「不置位」(第二分支 `writeFailure != null` 也不成立),
真正能分开它们的是「**成功分支会不会跑**」,而成功分支唯一的副作用 = **清位**。
只断「不置位」的夹具在两种实现下都绿 ⇒ **零判别力(F8)**。

构造:① 真失败 ⇒ 置位;② 修好,让一份 checkpoint 失败(留下粘性 `LastFailure`);
③ 退出保存成功;④ 断言**标志位被清掉**。

### 4.3 删除(重复)

本轮先写了一条 `test_saveOnExit_realWriteFailure_raisesFlag`,随后发现
`save_exit_retry_and_flag_test.cs:334` 的 `test_saveOnExit_writeFailure_raisesFlagAndDoesNotThrow`
已覆盖同一语义(且是同步路径、必走真失败)⇒ **删除自己那条**,不留重复。

## 5. 证伪轮(4 次尝试,1 次有效命中)

| 变异 | 内容 | 结果 | 定性 |
|---|---|---|---|
| **M1** | 第一个分支改用 `_writeQueue.WaitForIdle(0)` | **零红** | **等价变异** —— 同步调度器下 `WaitForIdle(0)` 恒 true,与 `completed` 行为相同 |
| **M1b** | 只把第一个分支还原成 `drained && LastFailure == null` | **零红** | **半个变异** —— 第二个 `if (writeFailure != null)` 仍在,超时时它也不成立 ⇒ 仍落 (b) |
| **M1c** | 在 (b) 分支补 `RaiseIncompleteFlag()`(忠实还原旧行为) | **1 红**:`test_saveOnExit_joinTimeout_doesNotRaiseFlag` | ✅ **有效命中,exact** |
| **M2** | 第一个分支加 `&& _writeQueue.LastFailure == null` | **零红** | **等价变异** —— `TryRunWithRetry` 成功时 `_lastFailure = null` ⇒ `completed && LastFailure != null` **不可达** |

**M2 的等价性证明**(源码依据,`SaveWriteQueue.cs:278-283`):

```csharp
try { work(); lock (_gate) { _lastFailure = null; } return; }
```

⇒ 本次工作项成功 ⇒ 队列级 `LastFailure` **必然为 null** ⇒ 加那个 `&&` 不改变任何行为。

⚠️ **M1 / M1b / M2 三次零红,前两次是我的变异打错靶,第三次是判据本身证明的等价**。
如实登记:本轮**只拿到 1 条有效变异**,而新判据共 2 条 ⇒
**`test_saveOnExit_staleCheckpointFailure_stillClearsFlag` 今日无专属有效变异**。
它的保护力由「(b) 分支若被删 ⇒ M1c 会红」间接覆盖,但**这不是它自己的**。
⇒ 如实记为本轮的**覆盖缺口**,见 §8。

**还原判据**:`SHA-256 逐字节`
- 改前 = 还原后 = `d5e9336a923e8ed82759bf6ee979760657c7477089e853f403a6bb4f7c3fd354`
- 零 `MUT-` 残留(`grep -c "MUT-"` ⇒ 0)

## 6. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6773 / 6736 / 0 / 0 / 37** | 2026-11-03 11:57:15 → 11:59:07 | `6772 / 6735 / 0 / 0 / 37` @ 2026-11-03 07:58:18Z |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 12:0x | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 08:01:06Z |

**等式**:`6773 − 6772 = 1` = 新增 `[Test]` 数(净 +1:改写 1 条(不增)+ 新增 1 条 − 删除重复 1 条)。

⚠️ `PM exit=2` 是本仓 PlayMode 常态退出码,判据取 XML 的 `failed` 计数(**0**)。

## 7. 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 判据已锚在本次工作项(不再读队列级 LastFailure)
grep -n "if (completed && writeFailure == null)" unity/Assets/Sim.Codec/SaveService.cs

# ② 三个分支各在其位
grep -n "RaiseIncompleteFlag()\|ReportExitSaveFailure(writeFailure: null" unity/Assets/Sim.Codec/SaveService.cs

# ③ 旧判据已消失(**活代码里**)
# ⚠️ 必须剥注释 —— 病因注释(`:693`)逐字引用了旧判据,按裸串扫会命中它(本轮实测踩到)
grep -n "drained && _writeQueue.LastFailure" unity/Assets/Sim.Codec/SaveService.cs \
  | grep -v "^[0-9]*: *//"                                                          # 期望 0

# ④ 两条新判据都在
grep -c "test_saveOnExit_joinTimeout_doesNotRaiseFlag\|test_saveOnExit_staleCheckpointFailure_stillClearsFlag" \
  unity/Assets/Tests/EditMode/PersistenceService/save_async_write_test.cs             # 期望 2

# ⑤ 回归
python3 -c "
import re
s=open('unity/Logs/final-editmode.xml',encoding='utf-8',errors='replace').read()
print(re.search(r'total=\"(\d+)\"[^>]*passed=\"(\d+)\"[^>]*failed=\"(\d+)\"',s).groups())"
# 期望 ('6773','6736','0')
```

## 8. 如实登记的残留(不借绿)

| # | 残留 | 性质 |
|---|---|---|
| ① | `test_saveOnExit_staleCheckpointFailure_stillClearsFlag` **无专属有效变异** | 覆盖缺口。M2 被证明是等价变异 ⇒ 该夹具的「读队列级粘性状态」这条防线**今日没有直接的反证** |
| ② | **崩溃 / 强杀仍然检测不到** | 本债**只修了误报**(用户裁定的靶心)。真正「会话没走完」的检测需「会话开始置位 + 成功保存清位」的会话级标记,**输入源与今日完全不同** ⇒ 若要做须先裁定,见 `TD-persist-14` 解除动作 ② |
| ③ | `SaveOnExit` 的 join 语义仍是「主线程 `Thread.Sleep(1)` 自旋」 | 与本债正交。退出路径最多阻塞 1 s(预算内),符合 GDD「不卡退」 |
| ④ | 「上次会话未完整保存」的**提示 UI 仍不存在** | 归 `TD-skeuo-05`(且已加注**硬前置 = 本债**) |

⚠️ **本债的 `closed` 只覆盖「误置」这一半**。
`TD-skeuo-05`(提示承载方无 AC)与「崩溃检测」两个半边**均未动**。
