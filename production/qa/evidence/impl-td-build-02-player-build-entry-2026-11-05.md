# `TD-build-02` 解除动作 ② —— 正式构建入口(菜单 + CI + 本地 batch,同一执行体)

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05(晚) |
| **用户指令** | 「按②再修 TD-build-02(正式构建入口)」(② = 解除动作的「立正式构建入口(菜单 + CI),场景清单取 `EditorBuildSettings`」) |
| **三元组** | 截至 `2026-11-05 19:2x CST` · 批次 `TD-build-02 兑现轮(②)` · commit `d0244b13` + 工作树 |

---

## 1. 改了什么

| # | 落点 | 内容 |
|---|---|---|
| ① | `unity/Assets/Editor.Tools.Bake/Cli/PlayerBuildCli.cs`(**新**) | 构建的**单一执行体**:`BuildPlayer(bool development)` 不退出编辑器(菜单用);`Build()` / `BuildDevelopment()` 是 batch 入口(结束时 `Exit`)。场景清单**只读 `EditorBuildSettings`**,空清单**拒绝构建**(`InvalidOperationException` → batch 退出码 2)。产物 `unity/builds/Linux-x64/<productName>.x86_64`。 |
| ② | `unity/Assets/Editor.Tools.Bake/PlayerBuildMenu.cs`(**新**) | `大医精诚/Build/出包 · Linux x64 (Release)`、`… (Development)`、`打开产物目录`。**不含构建逻辑**,只调 ①。**不调 `EditorApplication.Exit`**(那会杀掉编辑器;探针 CLI 能 Exit 是因为它只在 batch 下被调)。 |
| ③ | `.github/workflows/player-build.yml`(**新**) | CI 出包 job。触发面刻意窄(`workflow_dispatch` + `v*` tag)。末尾有一步「**产物不存在 ⇒ job 失败**」—— 防「跑完了但什么都没产出」记成绿。**本 job 未在本机跑过,已如实登记**(详见文件头注的三条未实测项)。 |

**为什么必须「同一个方法」**:菜单、batch、CI 三处若各自写一遍 `BuildPipeline.BuildPlayer`,
构建参数(场景清单 / target / 输出路径 / Development 位)必然分叉,而且分叉时**谁都不报错**。

**场景清单不另立第二份**(承 ADR-023 三场景制):`Boot.unity` 是 `EditorBuildSettings` 里**唯一**启用的场景
(`MainMenu` / `World` 走 Addressables additive,不进 Build Settings)—— 实测日志逐字
`[player-build] 场景数 = 1: Assets/Scenes/Boot.unity`。

## 2. 本地真跑(与 CI 调的是同一条命令)

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -quit -nographics \
  -projectPath "$PWD" \
  -executeMethod DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build \
  -logFile Logs/player-build.log
```

```text
[player-build] 开始构建 → …/unity/builds/Linux-x64/unity.x86_64
[player-build] 场景数 = 1:
  Assets/Scenes/Boot.unity
[player-build] 形态 = Release;product = unity;company = DefaultCompany
[player-build] Result: Succeeded;错误数 = 0;耗时 = 00:00:17.8469520;
               产物 = …/unity/builds/Linux-x64/unity.x86_64;输出大小 = 123546171 B
[player-build] Exit 0
```

## 3. 真启动冒烟(解除动作 ③,本轮执行一次)

`DISPLAY=:0` 跑产物,`xdrive` 发键 + 截图:

| 步骤 | 结果 |
|---|---|
| 启动 → 主菜单四行 | **有字**(24px,无方框) |
| 「继续诊治」→ 世界 | 世界载入,3 个病人胶囊可见(占位材质正常) |
| ESC → 暂停壳 → 「存档位」 | 存档册 6 槽 + 留档 + `3 / 3` 页码,**有字** |
| 暂停壳末行「返回主菜单」→ 主菜单 → 「退出游戏」 | 进程正常退出 |

日志判据(退出后读 `~/.config/unity3d/DefaultCompany/unity/Player.log`,67 行):

```bash
grep -c 'Font not found' <Player.log>                    # 0
grep -ci 'font' <Player.log>                             # 0
grep -c '找不到 URP Lit\|加载不到 Resources' <Player.log>  # 0
```

⚠️ **冒烟是新入口产物**做的(不是探针 CLI 的产物)—— 这是 ② 与 ③ 同轮取证的关键:
否则「入口能用」与「包能跑」仍是两条互不相干的断言。

## 4. 未做 / 残留(禁借绿)

| # | 解除动作 | 状态 |
|---|---|---|
| ① | `ProjectSettings` 设 company / product | **仍未做**。实测 `companyName: DefaultCompany` / `productName: unity` ⇒ 产物名 `unity.x86_64`、存档根仍与编辑器同一个(`DefaultCompany/unity/saves`)。**① 不做,「开发期一次误跑就动玩家档」这条风险仍在。** |
| ② | 立正式构建入口(菜单 + CI) | **本轮完成**。CI 侧 **NOT-RUN**(三条未实测项写在 workflow 头注)。 |
| ③ | 出包后至少跑一次真启动冒烟 | **本轮执行一次**(§3)。⚠️ 它**不是一次性的状态**:每次对外发放前都要重跑。 |
| ④ | 存档根写进文档 | **记录在案**(见 `TD-build-02` 正文的 2026-11-05 订正:存档根 = `Application.persistentDataPath/saves`)。未另建运维文档。 |

**本轮不动的两件事**:`CostTableProbeBuildCli` **保留**(它是 BuildGate 探针轮的载体,不能拿正式入口替);
`ProjectSettings` 一字未改。

## 5. 调试跑局对真实存档的写入(已还原)

两次真跑(探针包 + 正式入口包)都写过 `~/.config/unity3d/DefaultCompany/unity/saves/`:

- 探针包那轮:`w4/save_17.bin` 新增、`save_exit.bin`(+`.bak`)覆写、`worlds.index` 变化;
- 正式入口包那轮:`w4` 同上,且**根目录的 `save_auto.bin`(1622 B)被多世界迁移归档成 `w1/save_auto.bin.migrated-639294742794534440`**。

两轮都**对着运行前的整目录备份 `diff -rq` 逐件还原**,还原后 `diff` 零输出。
备份留 `/tmp/dyjc-costtable-probe/saves.old{4,5}`(`/tmp` 清理后即失效)。

⚠️ **一处未坐实、如实登记**:根目录那个 1622 B 的 `save_auto.bin` 是 **19:10**(PlayMode 全量跑的中途)
出现在备份里的,而它的大小等于**旧格式(v1)存档**的尺寸。本轮**没有**坐实它是谁写的 ——
候选 = PlayMode 夹具直接 `AddComponent<BootRoot>()`(`case_opened_wiring_test.cs:89` 即此形态,
未设 `SaveBasePathOverride` / `SaveRootOverride`)走 `FirstSessionSave`,或更早某轮的遗留。
**归因未决,不在本轮回写任何结论**。若要坐实:单跑该夹具一次,前后比对 `saves/` 根目录。

## 6. 回归

| 套件 | 结果 | 对照 |
|---|---|---|
| EditMode | **`6940 / 6903 / 0 / 0 / 37`**(`ed-tdb03.xml`,本轮未改测试件) | 上一轮同值 |
| PlayMode | **`341 / 337 / 0 / 2 / 2`**(`pm-tdb03.xml`) | 上一轮同值 |

⚠️ **本轮零测试件改动** —— 新增的两个 `.cs` 是**编辑期工具**(`Editor.Tools.Bake`,不进构建、
无 `[Test]` 引用)。它们由**真出包**验证(§2),不由夹具验证;把「构建入口能用」写成夹具判据
反而会变成自造断言(假绿 F1)。
