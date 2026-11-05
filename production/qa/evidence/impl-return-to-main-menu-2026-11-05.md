# 暂停壳第四行改「返回主菜单」+ 第一层退出游戏补编辑器分支

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05(晚) |
| **用户指令** | 「第一层退出游戏要能退出游戏,第二层退出游戏改一下名字为返回主菜单回到第一层,你调试确认一下」 |
| **三元组** | 截至 `2026-11-05 18:12 CST` · 批次 `返回主菜单轮` · commit `5c2bfdd1` + 工作树 |

---

## 1. 第一层「退出游戏」——本来就是对的,**缺的是编辑器分支**

真包实测:点它 ⇒ 日志 `[BootRoot][主菜单] 玩家选择退出游戏` ⇒ 引擎 shutdown ⇒ 进程消失(已用 `ps` 核对)。
**但在编辑器 Play 模式下 `Application.Quit()` 什么都不做** —— 玩家在编辑器里点会以为坏了。

修复:抽出 `QuitApplication()`

```csharp
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
```

## 2. 第二层「退出游戏」→「返回主菜单」

| 层 | 改动 |
|---|---|
| 屏 | `PauseScreen.RowLabels[4]` = 「返回主菜单」;`IPauseCommandSink.Quit()` → `ReturnToMainMenu()` |
| 装配 sink | `DriverPauseCommandSink(quit:)` → `(returnToMainMenu:)` |
| 装配层 | 新增 `BootRoot.ReturnToMainMenuAsync` / `UnloadWorldSceneForMenuAsync` / `PresentMainMenuForReturnAsync` / `ResumeWorldFromReturnMenuAsync` / `LoadWorldSceneAsync` |

**行为**:落盘(与「保存」行同一出口 `SaveSlotCommandRouter.TrySave`)→ `_booted = false`(关帧泵闸)
→ `CloseAllModals` → **卸 World 场景**(`Addressables.UnloadSceneAsync`;病人视觉先 `DestroyAll`)
→ 重载 `MainMenu` 场景并摊开第一层 → 玩家按「继续诊治」→(必要时 `ReassembleForWorld` + **补首份存档**)
→ 重载 World → 重新开闸。

⚠️ **sim 与玩家都不销毁** —— 回第一层是「挂起」,不是「重开」。

## 3. 实机验证(真 player,`DISPLAY=:0` + XTEST 发键 + XGetImage 截图)

```text
进世界 → ESC → 暂停壳末行「返回主菜单」        （player_pause_return_row.png）
按它     → 世界卸掉、第一层四行摊开            （player_back_to_menu.png）
按继续诊治 → 回世界、闸门重开                  （player_resume_from_menu.png）
再返回 → 新起一局 → 切袋 w4→w5、进新局          （日志 + 目录逐条核）
主菜单按退出游戏 → 进程真的退（ps 核对）
```

**新起一局的连带修复**:首版回程**没有**跑启动序第 8b 步的 `FirstSessionSave` ⇒ 新世界目录建了却是空的
(实测 `w5/` 里零文件)。补上后重跑:日志 `[BootRoot][返回主菜单][7a] 新世界:已受理初始存档`、
`w5/save_auto.bin` 1622 B 落盘。

## 4. 同轮抓到的两件事(如实登记)

### ① 构建与编辑器**共用同一份真实存档目录** —— `TD-build-02` 已就地订正

我原以为 player 存到 `unknown/unknown`。实测:`unknown/unknown` 只是 **PlayerPrefs** 目录;
**存档根 = `Application.persistentDataPath/saves` = `~/.config/unity3d/DefaultCompany/unity/saves`,
与编辑器同一个目录**(player 日志逐字:`切换世界目录:…/DefaultCompany/unity/saves/w4 → …/w5`)。
⇒ 真问题不是「打包版看不到档」,而是**构建与编辑器共用玩家真实档** —— 调试跑局会直接写进去。
`TD-build-02` 的该句已就地订正(原文不删)。

**我这轮的写入与清理**(不销毁,只移出):
- 造出 `w5/`(新起一局两次)⇒ 已 `mv` 到 `/tmp/dyjc-costtable-probe/{w5.removed,w5.removed2}`,
  并**截掉 `worlds.index` 的第 5 条目** → 恢复为 `[1,2,3,4]`(原件备份 `worlds.index.before-cleanup`)。
- `w4/` 里我这几局追加的 `save_13.bin` / `save_14.bin` / `save_exit.bin` / `session_incomplete.flag`
  **保留未删** —— 它们是合法存档,删数据比留着风险大。**用户如需可自行清理。**

### ② 病人物体的占位材质在 player 里**一直**建不出来(与本轮无关,既有缺陷)

player 日志刷 2489 条:
`[patient-visual] 找不到 URP Lit 也找不到 Standard —— 无法建占位材质`(栈 = `PatientVisualSpawner.OnTickEdge`)。
**首次出现于第 59 行,早于任何「返回主菜单」动作** ⇒ 与本轮改动无关,是**构建期着色器剥离**
(着色器只从代码 `Shader.Find` 引用 ⇒ 没进包)。**未登记债、未修**(不在本轮范围)。

## 5. 回归

| 套件 | 结果 |
|---|---|
| EditMode | **`6940 / 6903 / 0 / 0 / 37`**(`ed-rm.xml`) |
| PlayMode | **`341 / 337 / 0 / 2 / 2`**(`pm-rm.xml`) |

## 6. 仍未做(禁借绿)

1. **自动化 PlayMode 判据没写** —— `TD-skeuo-14` 的解除动作 ③ 要求一条能在回归里守住「回得去」的夹具;
   本轮只有人工驱动的实拍 ⇒ 该债记 **`partial`**,不记 `closed`。
2. **ADR-023 拆序六步只做两步**:第 1 步(停 Step)+ 第 5 步(`UnloadSceneAsync`);
   第 3/4/6 步(`ReleaseInstance` / `Release` handle / 断言登记簿空)仍归 ADR-023 的 V-1…V-7(NOT-RUN)。
3. 设置壳 / 存档册在「返回主菜单」形态下未逐屏走查(它们在启动序下已验过)。
