# 存档位「落笔」接线轮 · 判据执行原件(7b:202 的存盘半)

| 字段 | 值 |
|---|---|
| **对象** | `design/ux/save-slots-7b.md:202`「存盘(落笔)」行的**出口接线**(42 新行 → `Checkpoint` → 磁盘) |
| **执行轮** | 2026-11-01 · 用户指令「sprint-22 剩余 6 卡 ③ 存档位 UI 接线」+ 裁定「存档位屏的**存盘/落笔**半未接」 |
| **引擎** | Unity 6000.3.24f1(4e7b9b5b6244)· Linux · batchmode |
| **工程路径** | `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity` |
| **判据载体** | `unity/Assets/Tests/EditMode/Boot/save_slot_save_wiring_test.cs`(4 条)<br>`unity/Assets/Tests/PlayMode/save_slot_save_wiring_test.cs`(1 条,真启动序 + 真派发) |

---

## 1. 病根(开工时的实测)

7b 交互表有两行:「载入」与「存盘(落笔)」。载入那半已于 2026-10-31 接通
(`SaveSlotsScreen.Activate` → `ISaveSlotCommandSink.TryLoad` → `LoadSequencer.Load`)。
落笔这半**整条不存在**,不是「有代码没调用」,是**契约里没有那个方法**:

```
改前:unity/Assets/Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs:29
      interface ISaveSlotCommandSink { bool TryLoad(uint slotSeq); }   ← 只有载入
```

即用户原话的口径:42 的「末行后 Submit」按下去**没有 sink 方法可调**。

---

## 2. 落盘清单

| 文件 | 改动 |
|---|---|
| `unity/Assets/Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs` | 契约加 `bool TrySave();`(**不带槽号**)+ 文件头注从「本契约只有载入」改为「两个方法 = 7b 表两行」 |
| `unity/Assets/Gameplay.Boot/SaveSlotCommandRouter.cs` | 实现 `TrySave()`:`Ledger.NextWritableSeq` → `Checkpoint(new SaveSlot(seq))`;异常接住交 `failureSink` 并返回 `false`;新增 `IsSaveWired` / `LastSavedSlot` |
| `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/SaveSlotsScreen.cs` | 槽位行之后建**新行**(`name = "save-slot-new"`);Submit / Click 两路 → `ActivateWritable()` → `_sink?.TrySave()`;`tabIndex = _rows.Count`;**不计入** `SlotRowCount` / `RowAt`;空册时 `FirstFocusable` 回落新行 |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 装配第三形参 `_services.SaveService`(未接存档目录 ⇒ `null` ⇒ `TrySave` 恒 false) |
| `unity/Assets/Tests/EditMode/Boot/save_slot_save_wiring_test.cs`(新) | 42 侧 4 条判据 |
| `unity/Assets/Tests/PlayMode/save_slot_save_wiring_test.cs`(新) | 装配级 1 条:真 `BootRoot` + 真 `NavigationSubmitEvent` 派发 + 真写盘 |
| `unity/Assets/Tests/EditMode/Boot/save_slot_load_wiring_test.cs` | 测试替身 `CapturingSink` 补 `TrySave()`(契约变了,替身必须跟上) |

**目标槽不经过 UI 的依据**(不是本轮新裁):`CompositionRoot.cs:723` 与 `:745` 两条**既有生产**
路径(`AC-7a-20` 的定时兜底 / 锚点触发)已经是
`saveService.Checkpoint(new SaveSlot(saveService.Ledger.NextWritableSeq))`。
本类照抄同一形状,不新立规则。

---

## 3. 判据面(5 条)

| # | 测试 | 断言要点 |
|---|---|---|
| A | `EditMode…test_unwired_trySave_returnsFalseWithoutThrowing` | 未接 `SaveService` ⇒ `IsSaveWired == false`、`TrySave()` 返回 false、**不抛**(UI 回调逃异常 = 界面卡住)、`LastSavedSlot` 仍 `null` |
| B | `EditMode…test_newRow_exists_andActivating_requestsSaveExactlyOnce` | 新行在;`ActivateWritable()` **恰一次**请求;再按一次 = 再一次(**非一次性入口**);**零** `TryLoad` 调用 |
| C | `EditMode…test_newRow_isNotCountedAsSlotRow` | `SlotRowCount == 3`(新行不算槽位行)、`RowAt(3) == null`、新行 `tabIndex == 3`(焦点排最后) |
| D | `EditMode…test_emptySlots_firstFocusableIsTheNewRow` | 空册时 `FirstFocusable` **就是**新行(没有指针的游戏,焦点悬空 = 一步走不了) |
| E | `PlayMode…test_newRow_requestsSave_andActuallyWritesSlotFile` | 真 `BootRoot` 启动序 ⇒ 开 `ModalId.SaveSlots` ⇒ **`Focus()` + `NavigationSubmitEvent` 真派发**给新行 ⇒ 等台账高水位推进 ⇒ 断言 `save_0.bin` 在盘上、`LastSavedSlot == 0` |

E 的两处刻意设计:

- **不打直调**:`screen.ActivateWritable()` 证明不了「handler 挂上了」。派发事件才证明
  (`NavigationSubmitEvent` 属 `NavigationEventBase` 族,**必须先 `Focus()`**,否则事件被丢弃
  —— 承 `casebook_lexicon_book_test.cs` ⑦ 的实测结论)。
- **不等文件出现,等台账推进**:`WriteAtomic`(tmp → rename)让文件**先**可见,
  `OnCheckpointCommitted` 在同一个 worker 里**之后**才推高水位
  (`SaveService.cs:545-551` 自陈「断言 `NextWritableSeq` 的夹具须先 `WaitForIdle`」)。
  首跑实测就是这个形态:文件已在、`Expected 1 But was 0` ⇒ 判据改为**等更晚的信号**,
  两个信号最终都要断言(不是二选一)。

---

## 4. 证伪轮(7 变异,7/7 转红)

| 变异 | 停用了什么 | 期望 | 实测 |
|---|---|---|---|
| M1 | 删新行的 `RegisterCallback<NavigationSubmitEvent>`(保留 Click) | PlayMode E 红 | ✅ E 红(1/1) |
| M2 | `FirstFocusable` 空册回落改成 `null` | EditMode D 红 | ✅ D 红(其余 3 绿 —— 定位精确) |
| M3 | 停用 `TrySave` 的 `_saveService == null` 守卫 | EditMode A 红 | ✅ A 红(其余 3 绿) |
| M4 | `ActivateWritable()` 里把 `_sink?.TrySave()` 调两次 | EditMode B 红 | ✅ B 红(其余 3 绿) |
| M5 | `SlotRowCount => _rows.Count + 1` | EditMode C 红 | ✅ C 红 + D 红(D 的**前置**断言 `SlotRowCount == 0` 先命中 —— 附带红,非判据本体;如实登记) |
| M6 | 不调 `Checkpoint`(受理但零写盘) | PlayMode E 红(高水位不推进) | ✅ E 红 |
| M7 | 不记 `LastSavedSlot` | PlayMode E 红 | ✅ E 红 |

**还原判据**:每轮 `改 → 跑 → 还原` 在**同一脚本内**完成,还原后 SHA-256 逐字节比对
(`[M#] … 还原=OK` × 7)。终态哈希:

```
7ec0eec83dfef6087d40e289f6ddd847ad8f30462300f6728248b93ea6ca67f5  Screens/SaveSlotsScreen.cs
ce6de9912ad5dc8bd62585d31a9fbac041d9aa08fe30027f5b91e0c086859f27  SaveSlotCommandRouter.cs
```

复算:`bash /home/gu/.claude/jobs/6001633c/tmp/falsify_saveslot.sh`(脚本随 job 目录,不进仓库;
每轮产物在 `unity/Logs/falsify-M*.{xml,log}`)。

---

## 5. 未做项(**不借绿**)

7b:202 那一行有四个要素,本轮只兑现**前两个**:

| 要素(7b:202 原文) | 状态 |
|---|---|
| 「空行 / 末行后 Submit」→ 触发 | ✅ 本轮 |
| 「`Checkpoint(SaveSlot)`」 | ✅ 本轮 |
| 「**不跳页**,新行即第 1 焦点」 | ❌ **未做**:要槽位清单在落笔后**重取** |
| 「新行墨迹由淡入浓」 | ❌ **未做**:美术轮 |

**未做的原因是同一处数据缺口(本轮实测,不是推测)**:

```
① SlotProjections 生产消费方 = 0        grep -rn "SlotProjections\." unity/Assets --include=*.cs | grep -v /Tests/ | wc -l
② 槽位清单源 = 常量 {1u,2u,3u}          unity/Assets/Gameplay.Boot/BootRoot.cs:81
③ 台账初值 = 0                          Sim.Codec/SaveService.cs:194(默认形参)
```

⇒ **生产现状:屏上列出 1/2/3,新行写的是 0 号** —— 新写出的槽**不在清单里**,
「不跳页 + 新行即第 1 焦点」在数据上没有落点。这不是本轮引入的形态,是本轮让它**可观测**了。
归 7b 的槽位清单源轮(与 `review-s19-1-2026-10-24.md` §C-4 / 债册 G-4 行同一条根:
7a 的 `Writable`/`Loadable` 投影零生产消费者、生产侧另有两份手写等价物)。

**其余未做,同样不借绿**:
- 落笔**失败**的玩家可见反馈 —— 7b 纪律是「无弹窗」,今日失败只落 `failureSink`(引擎日志);
  `Checkpoint` 的写盘失败**不在调用点上报**(异步,经 `SaveWriteQueue.LastFailure`),未接 UI。
- 新行的行内文字 / 格线(空行有格线无字)—— 要槽位元数据源。
- `AC-42-F1`② 的手柄走查 —— 本轮**没有**跑走查,仍 `NOT-RUN`。

---

## 6. 回归(三元组)

**截至 `2026-11-01 08:17:56 CST` · 批次「存档位落笔轮」· commit `a3b7784e` + 未提交工作树**

| 平台 | 本次(实测) | 前次基线 | 差 | 等式 |
|---|---|---|---|---|
| EditMode | `6330 / 6293 / 0 / 37` | `6326 / 6289 / 0 / 37` | **+4 / +4** | `6330 − 6326 = 4` = 新文件 `[Test]` 计数 ✅ |
| PlayMode | `271 / 267 / 0 / 2 skip / 2 inconclusive` | `270 / 266 / 0 / 2` | **+1 / +1** | `271 − 270 = 1` = 新文件唯一 `[UnityTest]` ✅ |

- 两条命令**均先** `grep -c "error CS"` = **0**(08:15 / 08:17 两轮;`exit` 分别是 `0` / `2` ——
  PlayMode 的 `exit=2` 来自 2 条**既有 `Inconclusive`**,`failed=0`,与基线同形)。
- 基线出处:**引用自 `production/session-state/active.md` @ `2026-11-01 07:38–07:40 CST`**
  (主菜单背板轮),不是我重新盖的时间戳。
- 复算命令见 §7;结果件 `unity/Logs/full-{EditMode,PlayMode}-saveslot.xml`。

---

## 7. 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# 全量回归(两条各一次,先看编译错)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD/unity" \
  -runTests -testPlatform EditMode -testResults "$PWD/unity/Logs/full-EditMode-saveslot.xml" \
  -logFile "$PWD/unity/Logs/full-EditMode-saveslot.log"
grep -c "error CS" unity/Logs/full-EditMode-saveslot.log      # 期望 0
```

---

## 8. 本轮教训(切实测)

1. **首跑是红的,而且红得有价值。** PlayMode 夹具首版「等文件出现」——文件先可见、
   高水位后推进,断言在高水位上打空。改成等**更晚的**信号才稳定。判据写「同一条链上
   哪一步先发生」时,要挑**最后**那个可观测量。
2. **证伪脚本第一版 7 轮全废**(`NameError: name 'sys' is not defined`)—— 内联 python 少了 `import sys`,
   7 次 Unity 启动全在报错上浪费掉。教训:证伪脚本也要**先干跑变异串命中**,再启动 Unity。
3. **`BootRoot.cs` 只改 3 行**(多传一个形参),但它决定了整条链是否可用 —— 装配点的改动量
   与它的重要性不成比例,评审时容易漏看。
