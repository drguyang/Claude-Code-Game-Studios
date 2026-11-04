# 修复件 —— 读档后逻辑时钟未定位(「再进入已有存档,console 很多红色报错」)

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `0e916e8c` + 工作树
> **触发**: 用户报「再进入已有存档,会出现 console 很多红色报错」。
> **结论**: 不是读档失败,是**读档成功后时钟没回到存档时刻** ⇒ 之后每笔 sim 写入都被
> `EventStream.Append` 的「Tick 非降」拒收。

---

## 1. 现场(用户机器,`~/.config/unity3d/Editor.log`)

触发序列(日志原文,去掉栈帧):

```
[BootRoot][7a][ADR-040 D5] 切换世界目录:.../saves/w2 → .../saves/w1     ← 菜单里选了另一个世界
Loaded scene 'Assets/Scenes/World.unity'
# ⚠️ 版本不匹配:存档头 ConfigVersion = 2839353035,当前产物 = 3110338157(非致命,ADR-010 §七)
InvalidOperationException: BCD-码-3: 事件 tick 1 回退于已入流 tick 122 —— 流必须按 Tick 非降 …
InvalidOperationException: BCD-码-3: 事件 tick 3 回退于已入流 tick 122 …
InvalidOperationException: BCD-码-3: 事件 tick 4 回退于已入流 tick 122 …
…(逐条刷屏)
```

同一份日志里还有一条**不同性质**的红:`[7b][载入] 槽位 3 载入失败 … 槽位 slot=3 已锁
(AC-7a-19 (d) 读档即锁)` —— 那是**正常拒绝**(同一会话重复读同一槽),不是本件病灶,
见 §6 残留 ②。

## 2. 根因:三处结构缺口叠在一起

| # | 缺口 | 证据 |
|---|---|---|
| ① | `ITickProvider` **只有** `long CurrentTick { get; }`(只读,无定位入口) | `Sim.Contracts/Abstractions.cs:19-23` |
| ② | `SaveService.Load` 读了档头 `header.Tick`,**却没交出来**(只交 ConfigVersion / TimeParams / 逐份版本) | `SaveService.cs:1119-1130` |
| ③ | `LoadSequencer.Load` **没有定位这一步** —— 它的头注列了「派生态未重建清单」四项,却**没提 tick** | `LoadSequencer.cs`(修前) |

⇒ 读档把三流恢复到最后一条事件的 tick(用户那份 = 122),而 `SimTickDriver` 还停在 0。
`Update → PumpFrame → StepVitals` 写下的第一笔事件 tick 就是 1 ⇒ 被 BCD-码-3 拒收。
**后果不止刷屏**:拒收意味着 **sim 一个事件都写不进去**,读进来的世界形同停摆。

## 3. 修法(四处,均为最小面)

| 件 | 改动 |
|---|---|
| `Sim.Codec/SaveService.cs` | 新增 `public long LastLoadedTick { get; private set; }`;入口清 0,成功路径从**迁移后**的 header 回填 |
| `Gameplay.Boot/SimTickDriver.cs` | 新增 `public void Restore(long tick)` —— 定位 + **余量清零**;负值按 0(坏档头不崩)。头注明写「这是 `CurrentTick` 单调递增的唯一合法例外」 |
| `Gameplay.Boot/LoadSequencer.cs` | 新形参 `Action<long> restoreTick`;`Load` 内新增**第 1.5 步**(在 `afterRestore` **之前** —— 派生态重建读当前 tick) |
| `Gameplay.Boot/CompositionRoot.cs` | 接线 `restoreTick: tick => tickDriver.Restore(tick)`(与交给服务袋的是**同一个实例**) |

**为什么不是「每次写入容错一下」**:BCD-码-3 是流的核心不变量(`if (e.Tick > tick) break` 的游标
依赖它)。把它放宽 = 让乱序入流合法化,那是把「读档后刻度错」的锅甩给所有消费者。

## 4. 判据

| 层 | 载体 | 判什么 |
|---|---|---|
| EditMode | `load_sequencer_test.test_loadSequencer_load_restoresTickFromHeader` | 读档成功 ⇒ 恢复器收到**档头 tick**(夹具组版 `tick: 20`)且 `SaveService.LastLoadedTick == 20` |
| EditMode | `…test_loadSequencer_loadFails_tickNotRestored` | 读档失败 ⇒ **不得**动时钟(原子序) |
| EditMode | `sim_tick_driver_test.test_simTickDriver_restore_seeksAndClearsRemainder` | 定位到 122 → 余量清零 → 再 Advance 得 123 |
| EditMode | `…test_simTickDriver_restore_negativeClampsToZero` | 负值夹 0,不抛 |
| PlayMode | `resume_existing_save_playmode_test` | **两段式真启动序**:① 空根开局 → 等 tick ≥ 20 → 写自动档;② 拆世界重开 → 点「继续诊治」⇒ tick 必须 ≥ 存档时刻,且全程无未处理错误日志 |

⚠️ **PlayMode 那条为什么必须让世界先跑**:首份存档在 tick 0,拿它读回来**证明不了**定位
(0 与「没定位」在观测上同形)。所以第一段先等到 tick ≥ 20 再存档(实测存 20、读回 21)。

## 5. 证伪

把 `LoadSequencer` 里那行定位注掉(变异 `MUT-FALSIFY`)⇒ 同一夹具**直接复现用户报的那条红错**:

```
Unhandled log message: '[Exception] InvalidOperationException: BCD-码-3:
事件 tick 1 回退于已入流 tick 2 —— 流必须按 Tick 非降 …'
```

还原 = SHA-256 逐字节一致(`LoadSequencer.cs` `72c909cd…`),`grep -c MUT-FALSIFY` = **0**。
⇒ 判据不是「我以为它对」:它是**先复现了症状**,再证明修复把那症状消掉。

## 6. 回归(三元组)与残留

**截至** `2026-11-04T11:23:40Z` · **批次** sprint-24 读档时钟轮 · **commit** `0e916e8c` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6867 / 6830 通过 / 0 失败 / 37 跳过` | 上轮 `6863/6826` ⇒ **+4**(LoadSequencer 2 + TickDriver 2),零回归 |
| PlayMode | `339 / 335 通过 / 0 失败 / 2 不确定 / 2 跳过` | 上轮 `338/334` ⇒ **+1**(读档夹具),零回归 |

**残留(如实登记,不借绿)**:

1. **读档后的派生态重建仍是窄轮形态** —— `LoadSequencer` 头注的四项未重建清单
   (在场视图 `PresenceRegistry` · `PatientAppearedDriver._lastSpawnTick/_spawnedCount` ·
   `PatientVisualSpawner` · `RandomEventsDirectorDriver._lastDrivenTick`)按 2026-10-25 的用户裁定
   「全量派生态重建另立项」**维持不动**。⚠️ **本件之后它们第一次真正开始运行**(此前每笔写入都被拒,
   驱动跑不动)—— 若读档后出现重复出现病人 / 事件时序偏移,归那一轮,不归本件。
   登记面:`impl-td-persist04-load-2026-10-25.md` §五 + `LoadSequencer.cs` 头注。
2. **`slot=3 已锁` 打成红字** —— 那是 AC-7a-19 (d) 的**正常拒绝**(同一会话重复读同一槽),
   却走 `Debug.LogError`。是否降级成 `Log`/`LogWarning` 属 7b/7a 的**日志口径裁定**,
   本件不改(改了会动 `LogLoadFailure` 的两级分流,而那条分流是 2026-10-31 用户裁定「甲」的成果)。
3. **配置版本不匹配是既有非致命项**(用户日志里那行 `# ⚠️ 版本不匹配`)—— ADR-010 §七 明文,
   本件不涉及。
