# T22 批七 —— 「返回主菜单」的自动化判据(`TD-skeuo-14` 解除动作 ③)证据原件

**日期**:2026-11-11
**批次**:T22 批七(用户「开工 T22 批七」→ 从四条里选 **甲 · 回第一层**)
**债**:`TD-skeuo-14`(`partial` → `closed`)

---

## 0. 开工复勘:这张卡的三件裁定**早已裁过并实现**

用户选「甲」时我给的描述是「要你先裁三件」——**该前提已过期**。逐条实测:

| 债册 §解除动作 | 现状 | 判据 |
|---|---|---|
| ① 退菜单是否强制存盘 | **已裁**:先落盘再回 | `BootRoot.ReturnToMainMenuAsync` 第一步 `_saveSlotCommands.TrySave()` |
| ② 拆序怎么拆 | **已实现**(第 1 步关闸 + 第 5 步 `UnloadSceneAsync` + 病人视觉显式销毁) | 同方法 ②③ 段;装载体 `LoadWorldSceneAsync()` 与启动序共用 |
| ③ 重走启动序还是只重开菜单 | **已裁**:只重开菜单(世界与 sim 挂起不销毁) | `PresentMainMenuForReturnAsync` 注「与启动序共用行集与装配」 |
| ④ 暂停壳加行 | **已落**:第 4 行「返回主菜单」 | `IPauseCommandSink.ReturnToMainMenu` |

⇒ 债记 `partial` 的**唯一原因**就是解除动作 ③ 的**自动化 PlayMode 判据没写**(见该节
2026-11-05 的推进注),人工走查当时已跑过。本批 = 补这一条,不重裁。

---

## 1. 补了什么

### 1.1 夹具(新)`Tests/PlayMode/return_to_main_menu_wiring_test.cs` —— 2 条

| # | 测试 | 钉的性质 |
|---|---|---|
| 1 | `test_returnToMainMenu_unloadsWorld_andContinueResumes` | 进世界 → 经**生产意图出口** `PauseSink.ReturnToMainMenu()` → ① World 场景**真的卸了** ② 帧泵闸关 ③ 第一层摊开 ④ **落盘先发生**(等文件,不等帧)→ 按「继续诊治」→ World 装回、闸重开、第一层收掉 |
| 2 | `test_returnTwice_secondCallIsNoOp_continueStillWorks` | **负控**:第一层摊着时重复调 ⇒ 不重复拆、不卡死,之后「继续诊治」仍有效 |

装置与 `multi_world_boot_wiring_test` 同款(`SaveRootOverride` 指到临时**根**,走多世界形态;
**不用** `SaveBasePathOverride` —— 那是单世界缝,会绕开本债关心的路径)。

### 1.2 顺带修一处实现缺陷:`IsMainMenuOpen` 回程不置位

写夹具时撞见的真缺陷:该标志只在启动序 `PresentMainMenuAsync` 里被置,**回程路径漏了**
⇒ 「第一层是否摊开着」在公开面上**恒假**,装配级夹具只能反射读私有态。
已在 `PresentMainMenuForReturnAsync` 的 `ShowSceneLevelScreen` 成功支置 `true`、`Dismiss` 后置 `false`
(与开屏/收屏同进同出)。⚠️ **这不是为夹具造的缝**:它是「第一层在、世界不在」的唯一公开读数。

---

## 2. 判据映射

| 债册 §解除动作 | 载体 |
|---|---|
| ③ 自动化 PlayMode 判据 | 本件两条 |
| ① 先落盘 | 测试 1 的「等文件」断言 |
| ② 卸世界 + 关闸 | 测试 1 的 `WorldSceneLoaded()` / `ReadBooted()` 断言 |
| ④ 暂停壳第 4 行 | 测试经 `PauseSink.ReturnToMainMenu()` 走的就是该行接的那条链 |

---

## 3. 证伪轮(四枪,**全部在最终版夹具上重跑**)

| 枪 | 改法(均在 `BootRoot.ReturnToMainMenuAsync` / `PresentMainMenuForReturnAsync`) | 结果 |
|---|---|---|
| **M1** | 去掉返回前的 `_saveSlotCommands.TrySave()` | **1 红**:`test_returnToMainMenu_unloadsWorld_andContinueResumes`(落盘判据) |
| **M2** | 跳过 `UnloadWorldSceneForMenuAsync()` | **2 红**(世界没卸 ⇒ 两条都红) |
| **M3** | 回程不置 `IsMainMenuOpen = true` | **2 红**(第一层摊开判据无从成立) |
| **M4** | 回程不关 `_booted`(只置 `_worldPaused`) | **2 红**(闸门没关 ⇒ 世界仍在跑) |

四枪后 `BootRoot.cs` SHA-256 逐字节还原(脚本逐枪打印「还原 成功」,终态「一致」)。

### 3.1 M1 打了三枪才真红 —— 两次都是**我的判据**没牙(如实登记)

| 版本 | 判据 | M1 结果 | 错在哪 |
|---|---|---|---|
| v1 | 「世界目录里**有** `save_auto.bin`」 | **0 红** | **空洞正控(F4)**:启动序 8b 早就写了首份自动档 ⇒ 返回**之前**该断言就为真 |
| v2 | 「`save_auto.bin` 的写入时刻**变新**」 | **1 红** | 方向对了,但**盯错了文件**:暂停壳的 `TrySave()` 写的是**手动槽** `save_<NextWritableSeq>.bin`(`SaveSlotCommandRouter` 契约),自动档根本不动 |
| v3(终版) | 「世界目录里**任一** `save_*.bin` 新出现或写入时刻变新」 | **1 红** | ✓ |

⚠️ **v2 的红是「假红」**:它红在「自动档没变新」,而真实原因不是没落盘、是**写了另一个槽**。
如果就此收工,会留下一条**永远红**的夹具(或诱导实现去改错的地方)。⇒ 判据必须是**目录快照**,
不能点名某一份文件的某一种变化。

**M2–M4 在最终版夹具上重跑过**(旧结果是对 v1 夹具的,严格说不算数):三枪仍各 2 红。

---

## 4. 回归(三元组)

| 套件 | 结果 | 截至时刻(UTC) | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7428 / 7391 / 0 / 0 / 37` | `2026-11-11T01:33:46Z`–`01:36:28Z` | T22 批七 | `71fff91a` + 工作树 |
| PlayMode | `380 / 375 / 0 / 2 / 3` | `2026-11-11T01:36:28Z`–`01:37:30Z` | T22 批七 | `71fff91a` + 工作树 |

基线(T22 批六):EditMode `7428 / 7391 / 0 / 0 / 37` · PlayMode `378 / 373 / 0 / 2 / 3`。
**等式**:`380 − 378 = 2` = 本件两条 `return_to_main_menu_wiring_test`;EditMode **零变化**
(本批没加 EditMode 夹具 —— 判据面全在 PlayMode 装配级)。

---

## 5. 债册

| 动作 | 结果 |
|---|---|
| `TD-skeuo-14` | **`partial` → `closed`**(附闭合记录;解除动作 ③ 有载体) |
| `debtcount --check` | exit 0 · 条目 **207** · closed **174** · partial **10 → 9** · open 23 · **未闭 P0 面 13 → 12**(skeuomorphic 4 → 3) |

⚠️ **关闭的边界**:ADR-023 §⑤ 的拆序六步全量(V-1…V-7)仍 `NOT-RUN` —— 本条的判据只覆盖
「世界卸了 / 第一层开了 / 继续能回去」;不得据此称拆序按六步验过。

---

## 6. 本批没有做的(不得借绿)

| 项 | 状态 |
|---|---|
| **ADR-023 §⑤ 拆序六步全量**(V-1…V-7) | **仍 NOT-RUN** —— 本件只钉「世界卸了 / 第一层开了 / 继续能回去」三件事;第 3/4/6 步(`ReleaseInstance` / `Release` handle / 断言登记簿空)未验,**不得**据本件为绿称拆序按六步验过 |
| 真包人工走查 | **本轮未重跑**;2026-11-05 那次的人工结论见债册「实机验证」段(引用旧结论,不重盖时间戳) |
| 世界的 sim / 玩家销毁语义 | 不变 —— 回第一层是**挂起**,不是重开 |
