# 存档三路径验证 · 首份存档 · 「重开新局」勘察与 ADR-040 草案

> **日期**: 2026-11-04 · **sprint-24** · commit 基线 `9ec8f39f` + 工作树
> **触发**(按时间序,三轮同批):
> ① 用户「你试试能不能退出,自动,手动三种情况的正常存档」;
> ② 用户「是不是还差一个初始游戏开始的情况,作为第一份存档的初始建立」→ 裁定「**只做首份存档(A)**」;
> ③ 用户「遇到玩了一会又想重新开始新游戏的,目前的存档设计可以正常进行吗」→ 裁定「**先立债,等 `OQ-MM-5`
> 裁定再开工,按 D 起草一份 ADR**」。
> **判据面**: 每条给可跑命令与实跑输出;数字按三元组(截至时刻 · 批次 · commit)。

---

## 1. 三条存档路径 —— **都能正常存档,且都能读回来**

### 1.1 服务层(`save_three_paths_test.cs` · 4 条)

| 判据 | 断的是什么 |
|---|---|
| `test_autoSave_firesOnInterval_andReloads` | 未到间隔 ⇒ **盘上必须是空的**(正控前置,F4);到点 ⇒ `save_auto.bin` 出现且 `Load` 不抛 |
| `test_manualSave_writesNextWritableSlot_andReloads` | 「留档」路由器 → `save_{NextWritableSeq}.bin` 且可读 |
| `test_exitSave_writesDedicatedFile_andReloads` | `SaveOnExit()` → `save_exit.bin` 且可读 |
| `test_allThreePaths_coexist_withoutOverwritingEachOther` | 三条在同一目录各写各的,互不覆盖,三份都能读;`TryGetMostRecentSave` 不指向盘上不存在的槽 |

**实跑**: `4 / 4 passed, 0 failed`。

### 1.2 装配层(`save_three_paths_assembly_test.cs` · 3 条)—— 这一层才证明「接在袋子上」

服务层绿**不**代表生产里有人调用 —— 本仓反复抓到的形态(`TD-skeuo-06` 的
`IProcessingCommandSink` 生产实现为零,`_sink?.Ignite(...)` 静默跳过)。
本件用**生产装配根** `CompositionRoot.Assemble(dataProvider: …, saveBasePath: 临时目录)`:

| 判据 | 触发源 |
|---|---|
| `test_assembly_autoCheckpointFallback_writesAutoSlot` | 袋里的 `CheckpointFallback.OnTicks`(默认间隔 6000) |
| `test_assembly_manualRouter_writesNextWritableSlot` | 「留档」路由器 + 袋里的 `SaveService` |
| `test_assembly_exitSave_writesExitFile` | 袋里的 `SaveService.SaveOnExit()` |

**实跑**: `3 / 3 passed`。

### 1.3 两个必须写进夹具的**生产前提**(首轮全红换来的)

**① 不给 `dataProvider` = 在测一条生产上不存在的装配。** 首版没给,3/3 全红,且**红法各不相同**:

| 路径 | 症状 |
|---|---|
| 退出 | `InvalidOperationException [E-13][data-core] 无法派生 ConfigVersion 快照:dataProvider 为 null` |
| 自动 | **目录空、无声** —— 兜底把异常降级成 `LogError`(设计内行为) |
| 手动 | `TrySave()` 返回 `false` |

`BootRoot` 永远传 `DataCorePreloader.Provider`。注入面 = `AddressablesDataProvider` 的
**可注入构造**(`internal`,`Func<地址,字节>`)+ `DataCookedDisk.ReadAll`:
**不能自造 `IDataProvider` 实现** —— `ResolveConfigVersionSnapshot` / `ResolveGlobalConfigVersion`
会对 provider 做**类型断言**。见共享件 `Tests/EditMode/DiskBackedDataProvider.cs`。

**② `Checkpoint` 是异步的**(ADR-010 §六 / AC-7a-07):门与组版在主线程,写盘 + 推高水位在 worker。
不排空队列就断言 = 断言「还没写完」。⇒ 夹具必须 `WriteQueue.WaitForIdle(5000)`,
并同时断言 `LastFailure == null`(否则「worker 写失败」会伪装成「还没写完」)。

### 1.4 三条生产触发线(静态确认,行号可核)

| 路径 | 触发点 |
|---|---|
| 自动 | `CompositionRoot.cs:871` 构造兜底闭包 → `:879` `Checkpoint(AutoSaveSlot)`;`BootRoot.cs:2159` 逐 tick 驱动 |
| 手动 | `SaveSlotsScreen.cs:385` → `SaveSlotCommandRouter.TrySave()`;`BootRoot.cs:1015` 接线 |
| 退出 | `BootRoot.cs:466`(`Application.wantsToQuit`)→ `SaveOnExit()` |

---

## 2. 首份存档(用户裁定「只做首份存档(A)」)

### 2.1 补的是哪个窗口(不是设计缺陷,是权威件把它指派给了定时兜底)

`design/gdd/persistence-service.md:145-147` 规则六逐字:「事件长期缺席时(**初次启动无叙事事件** /
长时间探索期)由 `CHECKPOINT_INTERVAL`(默认 5 分钟)兜底触发 —— 保证『崩溃损失 ≤ 一个间隔』恒成立」。

⇒ 新档前 5 分钟内崩溃 / 强杀:事件触发没锚点、兜底没到点、退出没关窗、手动要玩家自己按
⇒ **盘上零存档**。补法 = 把「间隔已满」的判定在**全新档**时提前到 t = 0。

### 2.2 归类(决定它是否违反 `AC-7a-08`「恰三处」)

**是定时兜底的 t = 0 实例,不是第四类触发点**:同一个 `AutoSaveSlot`(槽 2)、同一个
`Checkpoint(SaveSlot)` 入口。`AC-7a-08` 的判定面是**接口签名反射**(公开可写方法参数集 = `{SaveSlot}`)
—— 逐字不变。GDD 规则六与 `AC-7a-08` 均已就地加注。

### 2.3 两条硬守卫(各对应一个已实测的失败形态)

1. **盘上已有任何档 ⇒ 一律不写。** 否则「读档失败回落新局」会在启动序末把玩家唯一能回的档
   覆盖成空世界 —— `BootRoot.TryContinueFromSave` 的注释早已把这条立成约束
   (「新局不写任何存档文件 ⇒ 不覆盖既有档」)。本类把「路径不发生」升级为「**写之前再查一次磁盘**」。
2. **查询失败 ⇒ 不写。** `_continueSlot == null` 有两种成因:真没档,或 `TryGetMostRecentSave` 抛了。
   后者的「有没有档」是**未知**,写下去可能覆盖上去 —— 「未知 ≠ 没有」(ADR-037 §④同一条纪律)。

### 2.4 落点

| 文件 | 内容 |
|---|---|
| `Gameplay.Boot/FirstSessionSave.cs`(新) | 决策体;不抛,失败经 `error` 交装配层记日志 |
| `BootRoot.cs` 启动序第 8b 步 | `_booted = true` 之后、`_continueSlot` 的 else 分支 |
| `design/gdd/persistence-service.md` | 规则六 + `AC-7a-08` 加注 |

### 2.5 夹具(4 条)

| 文件 | 判据 |
|---|---|
| `first_session_save_test.cs`(3) | 空档 ⇒ 真的写 + 读得回 + `TryGetMostRecentSave` 变 true;已有档 ⇒ **拒绝写且该档 SHA-256 不变**、不凭空多出自动档;`null` 服务 ⇒ 不算失败 |
| `first_session_save_wiring_test.cs`(1) | **接线**:`BootRoot` 的**非注释代码**里存在调用点,且排在 `_booted = true` 与「继续最近档」**之后** |

⚠️ **第二件是源码级断言,不是行为观测** —— 它证明「调用点在,且次序对」,**不证明**某次启动真的执行过
(`BootRoot` 整段今日无 PlayMode 判据,与其其余 `OnTickEdge` 同状态)。如实登记,不借绿。

### 2.6 证伪轮(证明那条源码级断言**会红**)

同命令内 改 → 跑 → 还原:

1. 备份 `BootRoot.cs`,记 `SHA-256 = d70b416f6b6c96e6d3ec3cabbed532e5d3a3ca86b1bcc0da3a99e340b521643e`;
2. 删掉整个「8b」`else` 分支(python 括号配平,移除 995 字符);
3. 跑 `FirstSessionSaveWiringTest` ⇒ **`Failed`**(`启动序里找不到首份存档的调用点 … Expected: >= 0 But was: -1`);
4. 从备份还原 ⇒ `SHA-256` **逐字节相同**(`MATCH True`),探针串在场。

---

## 3. 「重新开始新游戏」—— 勘察结论:**不能**,已立债 + ADR 草案

### 3.1 三条实测

| # | 事实 | 复算 |
|---|---|---|
| ① | **入口不存在**:主菜单一行 / 暂停壳三行 / 存档册只有载入与留档 | `grep -rnE "\bNewGame\b\|\bRestartGame\b\|\bNewRun\b\|新开一局\|重新开始\|重开新局\|新起一局" unity/Assets --include=*.cs \| grep -v /Tests/ \| wc -l` ⇒ **0**<br>⚠️ 首版命令写成 `-iE "newgame\|new game\|…"` 会命中 **40 处 `new GameObject`** —— 已订正 |
| ② | **「最近档」按游戏内 tick 排序**,不是写入时间 | `sed -n '285p;329p' unity/Assets/Sim.Codec/SaveService.cs` ⇒ `long bestTick = 0L;` / `if (!found \|\| tick > bestTick)` |
| ③ | 存档册只有两个动作,无删除 / 新建 | `sed -n '25,43p' unity/Assets/Gameplay.UI/Skeuomorphic/SaveSlotCommands.cs` ⇒ `TryLoad(uint)` / `TrySave()` |

**②是硬阻塞**:新局 tick 从 0 起 ⇒ 新局写下的任何档都排在旧局之后 ⇒ 下次启动仍载入旧局。
玩家点了「新游戏」、关了、再开 —— 回到上一个世界。

### 3.2 为什么这是「未裁」而不是「没实现」

`OQ-MM-5`(`main-menu-42.md:250`)「要不要『新开一局 / 设置 / 退出』? **未裁**」;
7b 的口径(`save-slot-ui.md:39-40`)逐字「重开游戏 = 重新开始『今天』」⇒ 现行模型是**单时间线的检查点册**。
⇒ 缺的不是按钮,是**「世界」这个对象**。

### 3.3 产物

- 债 **`TD-persist-20`**(`open` / P0 面 **Y** / 归属 7a + 7b + 42 / 形态裁定 = 用户);
- **`ADR-040`(Proposed)** `docs/architecture/adr-040-multi-world-save-selection.md` ——
  D 案:一个世界 = 一个存档目录 + 选择索引;**存档体一字节不改**(无 v6、无迁移链)。
- `.claude/docs/technical-preferences.md` 日志入册(本日志唯一一条 `Proposed`)。

⚠️ **ADR-040 前置 = `OQ-MM-5` 裁定**;未裁之前不转 Accepted、`TD-persist-20` 不解除、`V-1…V-8` 全部未跑。

---

## 4. 回归(三元组)

**截至** `2026-11-04T05:23:15Z` · **批次** sprint-24 存档轮(EditMode)· **commit** `9ec8f39f` + 工作树

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | `6842 / 6805 通过 / 0 失败 / 37 跳过` | 上轮 `6831/6794/0/37` ⇒ **+11**(4 服务 + 3 装配 + 3 首份 + 1 接线),零失败 |
| PlayMode | `332 / 328 通过 / 0 失败 / 2 不确定 / 2 跳过` @ `2026-11-04T05:30:12Z` | 上轮同值 ⇒ **零回归**(两条 `Inconclusive` 均为历史登记,理由串未变) |

---

## 5. 如实登记(不借绿)

1. **`BootRoot` 整段启动序无 PlayMode 判据** ⇒ 首份存档、三路径、读档都只有 EditMode 的
   装配级 / 源码级证据,没有「真启动一次」的证据。
2. **首份存档的接线断言是源码级**,证伪轮证明「删掉调用点会红」,但不证明它被执行过。
3. **崩溃仍不置「会话未完整」标志**(`TD-persist-14` ⑤,已登记)—— 本轮**未动**。
   首份存档把「崩溃丢失」从「永远」缩短到「≤ 5 分钟」,不是消除。
4. **`TD-persist-20` 未做任何代码改动**;`ADR-040` 是草案,`D8`(`WorldCatalog` 落点)与
   `D11`(联机 + 多世界)仍开放。
