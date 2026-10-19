# S15-5 实现原件 —— TD-audio-029 + TD-audio-032 合并卫生批

**日期**:2026-10-18
**对象**:`docs/tech-debt-register.md` `TD-audio-029` · `TD-audio-032` ②③④⑤ · `sprint-15.md` S15-5
**类型**:Integration(44 渲染面 + 装配层生命周期)
**判定口径**:每条 [A] 须有**可复算判据**;禁借绿(没跑的写没跑)。

---

## 一、原判定(债行逐字)

| # | 债行原判 |
|---|---|
| A | **029**:`PlayOneShot` `Spawn` + `Play()` 后**无 Stop / Release 对偶** ⇒ `_free` 恒空(每次 one-shot 新建源)· `ActiveCount` 把已播完静音的源计入 |
| B | **032 ②**:`(int)tick` 截断(`PatientCueDriver` 三处)· 同函数签名收 long、簿记用 int = 口径不一 |
| C | **032 ③**:`OnTickEdge` ≈57 行 · `BuildAudioSeams` ≈47 行,且**无夹具钉界** |
| D | **032 ④**:`PruneAbsent` 只跟 `_breathHandles` 的键;每 tick 边沿 `new List<int>`;`PresentPatientIds() => _present.ToArray()` 每调用复制 |
| E | **032 ⑤**:`busName_sourceIsEventTableColumn_notCodeCopy` 无 `RowCount > 0` 前置 |

---

## 二、修复落点

| 原判 | 落点 | 形态 |
|---|---|---|
| A | `AudioSourcePool`:`EnqueueOneShot` / `MarkFinished` / `ReclaimExpired` / `PendingOneShotActors`;`AudioSourceRenderTarget.PlayOneShot` 改为 Spawn → Place → Play → 排队 | 一次性源由池持有;**归还判据 = 观测到播完**(显式面 `MarkFinished` / 探针面),**不是**估一个到期时刻 |
| A | `Gameplay.Boot/OneShotReclaimPump.cs`(新) | 每 tick 边沿(BootRoot.Update):回收(消耗「刚排队」标记)→ 观测(`isPlaying`)→ 回收。**只依赖池**,渲染目标走 `NullRenderTarget` 链时同样成立 |
| B | `PatientCueSchedule.Decide` 三形参 `int` → `long`;`PatientCueDriver` 两本簿记 `Dictionary<int,long>`;调用点去 `(int)` | 夹具跨 `int.MaxValue` 边界 |
| C | `PatientCueDriver.OnTickEdge` → `StepOnePatient` / `DecideFor` / `EmitOneShotIfDue`;`PruneAbsent` → `PruneIfAbsent` ×3 重载 / `PruneOneAbsent` | 每个方法体 ≤ 40 **物理行**,由夹具读源码量出 |
| D | `PresenceRegistry`:`_presentSnapshot`(成员变动才重建,`Add/Remove/ResetForLoad` 处失效) | 稳态每拍零分配 |
| D | `PatientCueDriver.PruneAbsent`:五本表并集 + 在场集白名单过滤,`_staleScratch` / `_presentScratch` 复用 buffer;新增 `TrackedPatientCount` 可观测面 | 发咳簿记(无呼吸句柄)离场后**也会**清 |
| E | `runtime_assembly_test.cs`:补 `Assert.That(table.RowCount, Is.GreaterThan(0), …)` | 一行前置 |

**未在本批范围内**:032 ①(`CueIntervals.Default` / `BehaviorBands.Default` 入生产)—— 归 13 数据轮,触发条件不变;
032 ③ 的 `BuildAudioSeams`(≈47 行)—— 本批只改 `PatientCueDriver` 一侧,BootRoot 的 `BuildAudioSeams` **仍超界**,
如实记入残留(见 §五)。

---

## 三、证伪轮(逐例转红)

**纪律**:改 → 跑 → 还原在**同一命令内**完成,`finally` 还原后 SHA-256 逐字节校验(`restored sha ok`)。
判据 = 对应断言**必须转红**;仍绿 ⇒ 夹具弱判别面,**须先修夹具**再记反证。
(filter = `DaYiJingCheng.Tests.Unit.Audio.S15_5HygieneTest`,最终 13 测。)

| # | 停用 / 改坏的守卫 | 转红用例 |
|---|---|---|
| F1 | 删 `PlayOneShot` 里的 `_pool.EnqueueOneShot(...)` | `oneShot_pending_afterPlay_…` · `oneShot_markFinished_…` · `pump_liveSourceStays_…`(**3 红**) |
| F2 | 删 `OneShotReclaimPump` 里的 `pool.MarkFinished(actor)` | `pump_liveSourceStays_…` · `loopSource_neverReclaimedByPump` |
| F3 | 删「刚排队」标记的**消耗**(`if (e.JustQueued) { e.JustQueued = false; continue; }` → `continue;`) | `oneShot_pending_afterPlay_…`(源永不归还 → Pump 归还数 0) |
| F4 | 删探针「**绑过**且已销毁」的区分(`bound &&` → 无) | `pump_liveSourceStays_…`(未绑探针的源被当成播完) |
| F5 | 删排队时的池键校验(`entry.Source != source`) | `enqueueWithForeignSource_throws` |
| F6 | 调用处 `(int)tick`(还原 B 的截断) | `driver_keepsTickPastIntMax` |
| F7 | `PresentPatientIds()` 改回每次 `ToArray()` | `presentIds_snapshotReused_…` |
| F8 | `PruneAbsent` 只清呼吸句柄(还原 D) | `pruneAbsent_clearsEmitOnlyPatientBookkeeping` |
| F9 | 给 `OnTickEdge` 垫 40 行 | `splitMethods_withinLineBudget` |
| F10 | `runtime_assembly_test` 的 `BakedTable()` 注入 `default`(空表) | `test_ac018_2_busName_sourceIsEventTableColumn_notCodeCopy` 红 |

### 证伪轮暴露的三处「期望绿、实得绿」(夹具弱判别面 —— 先修夹具再记反证)

| 用例 | 首版为何仍绿 | 修法 |
|---|---|---|
| F3 | `S15_5` 原 `oneShot_pending_…` 只断言 `PendingOneShotCount`/`ActiveCount`,而这两者在**不消耗标记**时同样成立(标记只挡回收,不影响计数) | 追加「下一次 `Pump` 必须归还已停的源」 |
| F6(首版) | 原 `driver_keepsTickPastIntMax` 在默认间隔 40 下,`(int)tick` 回绕后仍恒 due ⇒ 截断前后结果相同 | 改为间隔 3 + 相位前提断言,并要求「边界之后仍有发」 |
| F9 | 首版把「拆分前 57 行体」整段内联,`ExtractMethodText` 在**签名行 → 首个匹配的闭括号**处截断,把外层 `foreach` 的括号当成了方法体 ⇒ 实得行数偏小 | 改为「垫行」形态(在方法体开头插 40 行),并新增**计数器自校准**测(`budgetMeasuresThePreSplitBody`) |

⚠️ **F6 另一条被拒的反证**:在 `Decide` 里把 `firstDue` 两端各 `(int)` 截断 —— 夹具**仍绿**(截断后 became 小 ⇒ 恒 due;不在本夹具判据面内)。如实登记为「未转红 ⇒ 没用该反证」,判据由 F6(簿记侧)承担。

⚠️ **F10 的判别力收窄(禁借绿)**:`AudioClipLibrary` 构造**本身**对零行表 fail-loud ⇒ 删掉 ⑤ 的守卫、只注入空表,**该测仍红**(报「音频事件表为空」),所以守卫今天是**防御纵深**而非唯一判别面。删守卫 + 空表同时发生时无法单独归因 ⇒ ⑤ 记为 **Landed ✅ / Proven ◆ 防御纵深**。

---

## 四、回归对账

| 项 | 值 | 出处 |
|---|---|---|
| 本夹具单跑(最终) | **13 / 13 / 0** | `unity/Logs/s155-fixture.xml` |
| 全量 EditMode(本件后) | **3794 / 3748 / 1 / 45** | `unity/Logs/s155-full-editmode.xml` |
| 全量 EditMode(基线 S15-4) | `3781 / 3735 / 1 / 45` | `impl-s15-4-*` §四 |

**等式对账**:`3794 − 3781 = 13` = 本夹具条数;`passed 3748 − 3735 = 13`;`failed` 与 `skipped` 不变。

**唯一红**:`MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` —— 已知间歇红(`TD-player-01`),
**不用于抵扣计数**。

> ⚠️ **本轮误读登记**:首次全量跑时,我在一个临时脚本里**把 `result != 'Passed'` 全量打印**(含 `Skipped`),一度读成「47 红」;
> 以 XML 的聚合字段(`failed=1`)复算后才纠正。上文数字全部取自 XML `failed` 字段,不取自逐条打印。

---

## 五、如实登记(禁借绿 · 本件未证 / 未做的部分)

| 项 | 状态 | 说明 |
|---|---|---|
| **`AudioSource.isPlaying` 的真实时序** | **NOT-RUN** | EditMode 不可证(非播放态恒 false、播放态首帧是否已为 true 均未实测)。本批只证「收到播完事实之后是否真归还」。⇒ 归 PlayMode 夹具;**不得**据本件推定生产回收频率 |
| **032 ③ 的 `BuildAudioSeams`(≈47 行)** | **未拆** | 本批只拆 `PatientCueDriver`;`BootRoot.BuildAudioSeams` 仍超 40 行界,债行 ③ 的该一半**仍 open** |
| **032 ① 调参常量入生产** | **仍 open** | 触发条件不变(13 数据轮) |
| **`isPlaying` 轮询频率** | **20 Hz** | 挂在 tick 边沿;素材最短数百毫秒 ⇒ 归还最多晚一拍,不影响听感。数值面如需调整归用户 |
| **兜底窗口 3600 s** | 常量 | 只兜观测面全沉默;不是旋钮(`UnobservedOneShotWindowSeconds`) |
| **IL2CPP 三格**(ADR-012) | NOT-RUN | 本机仅 Mono |

---

## 六、测试清单(13 测 → 原判/子项映射)

| # | 测试 | 钉的判据 |
|---|---|---|
| 1 | `oneShot_pending_afterPlay_notYetReleased_sameTick` | 开播即计入在响;同帧回收不掐;标记被消耗后下一轮可归还(A) |
| 2 | `oneShot_markFinished_returnsSourceToFreeStack` | 显式面归还 ⇒ `_free` 回流(同实例复用)+ `ActiveCount` 回落(A) |
| 3 | `pump_liveSourceStays_finishedSourceIsReclaimed` | Pump 只归还确认播完的,不误伤在响的(A/F4) |
| 4 | `loopSource_neverReclaimedByPump` | 循环源(负键)不受一次性回收影响(A) |
| 5 | `probeFace_releasesWhenOwnerHandedItOver` | 探针面:绑过且已销毁 ⇒ 播完(A) |
| 6 | `nullProbe_isNotJudgedFinished` | 未绑探针 ⇒ 不可判 ⇒ 不回收(A/F4) |
| 7 | `enqueueWithForeignSource_throws` | 池键错位 fail-loud(A/F5) |
| 8 | `decide_afterIntMaxValue_stillDue` | 调度器自身 long 语义(B) |
| 9 | `driver_keepsTickPastIntMax` | 驱动簿记 long(跨 2^31 边界)(B/F6) |
| 10 | `presentIds_snapshotReused_untilMembershipChanges` | 快照复用 + 成员变更重建(D/F7) |
| 11 | `pruneAbsent_clearsEmitOnlyPatientBookkeeping` | 清扫面 = 五表并集(D/F8) |
| 12 | `splitMethods_withinLineBudget` | 每段 ≤ 40 物理行(C/F9) |
| 13 | `budgetMeasuresThePreSplitBody` | 行数计数器自校准(防恒小假绿) |

---

## 七、命令(第三方可重跑)

```bash
# 本夹具
rm -f test-results.xml
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Audio.S15_5HygieneTest" -- --log-file -
# 期望:total 13 / passed 13 / failed 0

# 全量 EditMode
rm -f test-results.xml
unity test unity --mode EditMode -- --log-file -
# 期望:total 3794 / passed 3748 / failed 1(唯一红 = TD-player-01 已知间歇)/ skipped 45

# 证伪单例(示例:F3 —— 不消耗「刚排队」标记)
python3 /tmp/fzrun.py unity/Assets/Gameplay.Presentation/Audio/AudioSourcePool.cs \
  "                if (e.JustQueued) { e.JustQueued = false; continue; }" "                if (e.JustQueued) continue;"
# 期望:restored sha ok; RED: test_td029_oneShot_pending_afterPlay_notYetReleased_sameTick
```
