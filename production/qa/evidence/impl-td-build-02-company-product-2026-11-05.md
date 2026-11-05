# `TD-build-02` 解除动作 ① —— 设 company / product,并把旧存档搬过去

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05(晚) |
| **用户指令** | 「把 company/product 也设了(TD-build-02 ①)」;取值经 `AskUserQuestion` 裁定 = **`DaYiJingCheng` / `大医精诚`**,旧档**搬过去** |
| **三元组** | 截至 `2026-11-05 20:3x CST` · 批次 `TD-build-02 ① 轮` · commit `28e1dc19` + 工作树 |

---

## 1. 改了什么

| 面 | 改前 | 改后 |
|---|---|---|
| `unity/ProjectSettings/ProjectSettings.asset` | `companyName: DefaultCompany`<br>`productName: unity` | `companyName: DaYiJingCheng`<br>`productName: "大医精诚"`(= `大医精诚`,Unity 回写时转成 YAML 转义串) |
| 存档根 | `~/.config/unity3d/DefaultCompany/unity/saves` | `~/.config/unity3d/DaYiJingCheng/大医精诚/saves` |
| 产物名 | `unity.x86_64` + `unity_Data/` | `大医精诚.x86_64` + `大医精诚_Data/` |
| 窗口标题 | `unity` | `大医精诚` |

**旧存档已搬家**(用户裁定):整目录 `mv` 到新路径,搬迁前后 `diff -rq` **零差异**。
备份留 `/tmp/dyjc-costtable-probe/saves.old{6,7,8}`。

**两条陈旧注释同批订正**(它们逐字写着旧路径):
`Gameplay.Boot/WorldCatalog.cs`(「玩家只能去 `…/DefaultCompany/unity/saves/wN/` 手动删」)、
`Gameplay.Boot/BootRoot.cs`(同句)。**只改路径与加注,语义不动。**

## 2. 验证(真出包 + 真启动,不是夹具)

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -quit -nographics \
  -projectPath "$PWD" -executeMethod DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build \
  -logFile Logs/player-build-company.log
```

```text
[player-build] 产物 = …/unity/builds/Linux-x64/大医精诚.x86_64
[player-build] 形态 = Release;product = 大医精诚;company = DaYiJingCheng
[player-build] Result: Succeeded;错误数 = 0;耗时 = 00:00:23.89;输出大小 = 123546177 B
[player-build] Exit 0
```

| 判据 | 结果 |
|---|---|
| 产物名 | `大医精诚.x86_64` + `大医精诚_Data/`(日志 `Mono path[0] = …/大医精诚_Data/Managed`) |
| 窗口标题 | `大医精诚`(实测 WM_NAME,`xdrive` 因标题变化需按 PID 定位) |
| 存档根 | `~/.config/unity3d/DaYiJingCheng/大医精诚/saves/`;主菜单「继续诊治」**可点** ⇒ 旧档被找到 |
| 进世界 | 世界载入、3 个病人胶囊可见(与 `TD-build-03` 的修复一致) |
| 主菜单 / 世界 / 暂停壳 / 存档册 | 四屏有字,`Font not found` = 0 |

**回归**:EditMode **`6940 / 6903 / 0 / 0 / 37`**(`ed-company.xml`)· PlayMode **`341 / 337 / 0 / 2 / 2`**
(`pm-company.xml`)—— 与改名前的基线逐位相同。

## 3. 副作用与处置(逐条)

### ① 根目录那份遗留的 `save_auto.bin` 被游戏按设计归档进了 `w1/`

改名后首次真启动时,`saves/` 根下的单世界存档被多世界迁移搬进 `w1/save_auto.bin.migrated-639294783671934250`。
**存档没丢**,只是多了一个归档副本、根目录变干净。这是 1.7 迁移的**设计行为**,不是损坏 ⇒ 保留结果,不还原。

### ② 调试跑局对 `w4/` 的写入 —— 已还原

`save_17.bin` 等调试产物按运行前备份逐件还原;`diff -rq` 现只剩上面 ① 那一件 + `worlds.index` 的元数据差异。

### ③ 🔴 **`TD-persist-23`(本轮坐实)**:PlayMode 全量回归会往**真实存档根**写一份 `save_auto.bin`

跑全量 PlayMode 前后各做一次 `diff -rq`:

```bash
# 回归前
cp -a ~/.config/unity3d/DaYiJingCheng/大医精诚/saves /tmp/saves.before
# 跑全量 PlayMode …
diff -rq /tmp/saves.before ~/.config/unity3d/DaYiJingCheng/大医精诚/saves
# ⇒ 只在 live 中存在：save_auto.bin   （1622 B，魔数 DYJQ，版本字段 = 5）
```

**这同时订正了上一份证据件的猜测**:那里把同名文件读成「旧格式 1622 B」,**错了** ——
1622 B 是**当前版本**的空局存档尺寸(`0x24` 处 `05 00 00 00` = `SaveVersion 5`)。
⇒ 那个文件就是 **PlayMode 写的**,不是「更早某轮的遗留」。已立 `TD-persist-23`。
本轮把它删掉,`diff` 回到零。

## 4. 未做 / 边界

- **CI 的 `player-build.yml` 仍未跑过**(仓库未推送 / secrets 未配)⇒ `TD-build-02` 维持 **`partial`**。
- **PlayerPrefs 没有跟着搬**:旧的 `prefs` 留在 `DefaultCompany/unity/`。它装的是窗口/画质与
  **Input System 的 binding overrides**(ADR-011)。今日没有自定义改键 ⇒ 影响为零;将来若玩家改过键,
  换 company/product 会让改键**静默复位** —— 这是 ① 的**固有代价**,发布后不可再改。
- **`unity/builds/Linux-x64/unity_Data/`(旧产物的数据目录)已移出**到
  `/tmp/dyjc-costtable-probe/stale-unity_Data`,避免与 `大医精诚_Data/` 并存造成误判。
