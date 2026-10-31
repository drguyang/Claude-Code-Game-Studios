# 主菜单实现轮 · 判据执行原件(AC-MM-01…10)

| 字段 | 值 |
|---|---|
| **对象** | `design/ux/main-menu-42.md`(**Approved** 2026-11-01)`AC-MM-01…10` 的实现与执行 |
| **执行轮** | 2026-11-01 · 用户指令「下一轮做,接主菜单」 |
| **引擎** | Unity 6000.3.24f1(4e7b9b5b6244)· Linux · batchmode `-nographics`(截图轮除外,见 §5) |
| **工程路径** | `/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity` |
| **判据载体** | `unity/Assets/Tests/PlayMode/main_menu_wiring_test.cs`(7 条)<br>`unity/Assets/Tests/EditMode/SkeuomorphicUI/main_menu_static_test.cs`(3 条) |

---

## 1. 落盘清单(生产侧)

| 文件 | 作用 |
|---|---|
| `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/MainMenuScreen.cs`(新) | 主菜单屏:纸面 + 一行;`IMainMenuCommandSink` 出口;`SetHasContinueSave` 切文案 |
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | 场景级屏入口 `ShowSceneLevelScreen` / `DismissSceneLevelScreen`(非 `ModalId`);焦点单栈门抽出共用实现 |
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` | `.menu-host` / `.menu-column` |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 启动序 **2.95 主菜单闸门**;`BootWithMainMenu` / `SaveBasePathOverride`;第 8 步「继续最近档」 |
| `unity/Assets/Gameplay.Boot/DriverMainMenuCommandSink.cs`(新) | 42 → 装配层的单向出口 |
| `unity/Assets/Scenes/MainMenu.unity`(+ `.meta`)(新) | ADR-023 §① 的第二个场景(空场景 = 场景级标记) |
| `unity/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset` | 新增条目 `m_Address: mainmenu` |
| `unity/Assets/Scenes/Boot.unity` | `_bootWithMainMenu: 1`(生产启动即进菜单) |
| `unity/Assets/Sim.Codec/SaveService.cs` | `TryGetMostRecentSave(out SaveSlot)` + `LastSaveQueryError`(门 A 下 7a 无日志面) |

---

## 2. AC → 载体映射

| AC | 载体 | 断言要点 |
|---|---|---|
| AC-MM-01 | `main_menu_wiring_test.test_bootWithMenu_showsSingleRow_andLandsFocus` | 纸面在;行文字 = 「开始诊治」;文字非空且可见 |
| AC-MM-02 | 同上 | `focusController.focusedElement == row`(引擎真源,非自陈位) |
| AC-MM-03 | `test_keyboardSubmit_activatesMenuExactlyOnce_andEntersWorld` | 真注入 `Enter`;计数 sink 恰 1 次;世界装载 |
| AC-MM-04 | `test_gamepadSouth_activatesMenuExactlyOnce_andEntersWorld` | 真注入手柄南键;同一 `IMainMenuCommandSink` 出口,恰 1 次 |
| AC-MM-05 | 同 AC-MM-01 | 菜单子树内**恰一个** `Label` 且等于行文案;文案不含「按 / Press / Enter / 键」 |
| AC-MM-06 | `test_menuHasNoTransition_andBootSurvivesWorldLoad` | 菜单子树内每个元素 `resolvedStyle.transitionDuration` 全零 |
| AC-MM-07 | 同上 + `main_menu_static_test.test_bootAssembly_neverUsesLoadSceneModeSingle` + `test_mainMenuScene_registeredInAddressables` | Boot 场景仍加载 / `BootRoot` 仍在原场景 / 相机 + 单 `AudioListener` + tick 驱动仍在;`Gameplay.Boot` 全树零 `LoadSceneMode.Single`(剥注释后扫代码);`MainMenu.unity` guid 与 `m_Address: mainmenu` 同组共现 |
| AC-MM-08 | `test_bootWithMenu_…`(无档)+ `test_hasSave_showsContinueText_andAttemptsMostRecentSave`(有档)+ `test_saveQueryFailure_fallsBackToNoSave`(查询失败注入) | 两态文案;两态下行 `focusable` 且获焦(无禁用态);失败注入走 `chmod 000` ⇒ `LastSaveQueryError != null` 且按无档处理 |
| AC-MM-09 | `test_newGameBranch_continueLoadsWorldWithoutReadingAnySave` + `test_hasSave_…` | 无档 ⇒ `ContinuedFromSlot == null`;有档 ⇒ `ContinuedFromSlot == 退出保存槽` 且读档**被尝试**(非法字节 ⇒ `ContinueError != null`) |
| AC-MM-10 | `main_menu_static_test.test_uiAssembly_hasZeroSaveDirectoryKnowledge` | `Gameplay.UI` 全树零 `Directory.` / `DirectoryInfo` / `persistentDataPath` / `"save_` |

---

## 3. 执行命令与结果

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.main_menu_wiring_test" \
  -testResults "$PWD/Logs/playmode-mainmenu.xml" -logFile "$PWD/Logs/playmode-mainmenu.log"
```

**PlayMode 7/7 Passed · 编译错 0**(2026-11-01 06:38 CST,@ 工作树)。

```bash
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.SkeuomorphicUI.main_menu_static_test" \
  -testResults "$PWD/Logs/editmode-mainmenu.xml" -logFile "$PWD/Logs/editmode-mainmenu.log"
```

**EditMode 3/3 Passed · 编译错 0**(2026-11-01 06:44 CST,@ 工作树)。

> ⚠️ **本原件如实登记一处翻车**:第二轮 PlayMode 跑出「7 条里 4 红」的同一份结果**两次**,
> 实为 `CameraRig` 类型名未加命名空间导致 `error CS0246` ⇒ **编译失败,跑的是上一份陈旧程序集**。
> 教训:凡 `-runTests` 结果,须先 `grep -c "error CS" <log>` = 0 再判红绿(本轮之后每条命令都带这一步)。

---

## 4. 证伪轮(改坏 → 要求对应夹具转红 → SHA-256 逐字节还原)

工具:`/home/gu/.claude/jobs/6001633c/tmp/falsify_once.py`(同一命令内「改 → 跑 → 还原」,
还原后与改动前 **SHA-256 逐字节比对**;禁用 `git checkout` / `git stash`)。

| # | 变异(停用什么) | 期望转红 | 实测 |
|---|---|---|---|
| **M1** | `ShowSceneLevelScreen` 删「开屏落焦」两行 | 落焦 AC | ✅ 4 红:落焦 / 键盘 / 手柄 / 有档态落焦;`before=after=974d08a5…` |
| **M2** | `TryContinueFromSave` 删 `LoadSequencer.Load(slot)` | AC-MM-09 继续分支 | ✅ 恰 1 红(且有档态**只**该条红);`db2ce65e…` |
| **M3** | `MainMenuScreen` 把 `NavigationSubmitEvent` **注册两次** | 「恰好一次」 | ❌ **存活(7/7 绿)** —— 见下方说明 |
| **M3′** | `Activate()` 调 sink **两次** | 「恰好一次」 | ✅ 2 红(键盘 / 手柄,均报「实际 2」);`66a6ea94…` |
| **M4** | 世界装载 `LoadSceneMode.Additive` → `Single` | AC-MM-07 静态半 | ✅ 恰 1 红;`db2ce65e…` |
| **M5** | `MainMenuScreen.Activate()` 里加一行 `Directory.Exists(...)` | AC-MM-10 | ✅ 恰 1 红(只扫目录那条,另两条不动);`66a6ea94…` |
| **M6** | 菜单闸门去掉 `await`(菜单落到世界装载之后) | AC-MM-01 的定序断言 | ✅ 恰 1 红:「主菜单必须出现在世界装载之前」;`db2ce65e…` |

**M3 存活的处理(如实)**:双注册**没有**产生第二次 `Activate()` 回调 —— 首个回调里
`OnMainMenuContinue` 完成、启动序续跑并拆掉屏,后续回调不再到达该元素。
⇒ 该变异**未改变可观测行为**(属「变异打错靶」,不是夹具假绿);故改用 **M3′**(双发)
重新验证「恰好一次」断言的判别力,结果转红。**M3 不删、照登**,理由同本项目「八种假存活」纪律。

---

## 5. 实拍复核(AC-MM-05 的「目视」半边)

| 项 | 值 |
|---|---|
| 图 | `production/qa/evidence/skeuomorphic-ui/main-menu-2026-11-01.png` |
| 脚本副本 | `.../main-menu-2026-11-01.cs.txt`(临时探针,**用完已从 `Assets/` 删除**) |
| 跑法 | **GUI 模式**(`-batchmode` 出不了图,见 `ScreenshotTool` 头注两次实测):`Unity -projectPath unity -executeMethod DaYiJingCheng.EditorTools.Capture.TmpMainMenuShot.Run` |
| 判读 | **模型原样读图**(不取色、不算 bbox、不算对比度) |

### 5.1 首拍发现的两处缺陷(都已修,原文留档)

**① 纸面不居中(落到了屏幕下方 2/3 处)**
读数:`host 1680×96 at(0,802)`(面板 1680×1215)。
根因:场景级屏的容器**高 = 内容高** ⇒ `.menu-host` 的 `flex-grow` 没有可分配空间。
修:新增 `.scene-host`(绝对定位铺满面板)并在 `ShowSceneLevelScreen` 给容器加该类。

**② 常驻脉案屏整屏透出**
首拍里菜单纸面周围是脉案的八个值区(面色 / 语声 / 姿态 / 呼吸 / 触感 / 问诊 / 前案 / 后案)
与「病名 / ？ / 折」列 —— 与 spec §5「纸面下是纯色底(不是游戏画面)」相悖。
修:`ShowSceneLevelScreen` 期间把常驻屏容器 `display: none`,关屏时还原
(⚠️ **不**给它叠底色:那要在 42 侧发明一个色值,而色值归美术轮)。

### 5.2 重拍读数(修后)

`host 1680×1215 at(0,0)` · `paper 479×96 at(601,561) centerY=609`(面板中心 607.5)
· `row 439×57 at(620,581)` · `focused=main-menu-continue isRow=True ringOnRow=True`
· `label="继续诊治"` · `labelCount=1`。

图上可见:一张竖排纸面上**唯一一行**「继续诊治」,外框为黄铜焦点环;无任何按键提示文字 / 图标。

### 5.3 ⚠️ 与 spec §5 的一处**剩余偏差**(如实登记,未修)

纸面下**不是纯色底**,而是 Boot 场景的**天空盒 + 地面**(空世界的第一帧)。
- 「不是游戏画面」这半句成立(没有任何 gameplay 对象);
- 「纯色底」这半句**未兑现** —— 兑现它需要把相机 `clearFlags` 换成 `SolidColor`,
  而**那个色值是 spec §5 末「刻意留白,归美术轮」的一项**。⇒ 本轮不发明色值,
  登记为**美术轮的前置项**,不计入 AC-MM 任一条(§5 的表格是**布局规格**,不是 AC)。

---

## 6. 未做 / 残余

- **OQ-MM-3**(主菜单上按 `Escape` / `start`):本轮**未做动作** —— 菜单期 `_modalInput`
  尚未装配(它在启动序第 6.3 步,世界装载之后)⇒ 按键自然无反应,与 spec 的「安全默认」一致。
- **加载中的表现**:无(spec §6 显式声明「无加载中态」)。
- **「继续」读档的字节级正确性**:本轮只证「**尝试**了最近一档且失败被记录」;
  字节级正确性归 7a 的 EditMode 夹具(ADR-010 / ADR-034 / ADR-037)。

---

## 7. 全量回归(三元组:截至时刻 · 批次 · commit)

| 套件 | 结果 | 前次基线 | 等式 |
|---|---|---|---|
| **EditMode** | `6326 / 6289 passed / 0 failed / 37 skipped` | `6323 / 6286 / 0 / 0 / 37` | `6326 − 6323 = 3` = 新增 `main_menu_static_test` 的 3 条 ✅ |
| **PlayMode** | `269 / 265 passed / 0 failed / 2 skipped (+2 inconclusive)` | `262 / 258 / 0 / 2 / 2` | `269 − 262 = 7` = 新增 `main_menu_wiring_test` 的 7 条 ✅ |

- 执行时刻:**2026-11-01 07:2x / 07:4x CST**;批次:**主菜单实现轮**;commit:**`04f69ba8`**(本轮代码提交;本文件随其后一笔文档提交)。
- 引擎 `6000.3.24f1` · Linux · `-nographics`;两条命令均先 `grep -c "error CS"` = 0。
- 2 条 **Inconclusive** 是既有的 `U1Assump6GamepadFocusSpikeTest.*_notRun`(探针件,按设计不跑),非本轮引入。

**回归后的一次零影响清理**:`SaveService.HasAnySave()`(布尔投影)**零调用**(装配层用
`TryGetMostRecentSave`),按「死代码 = 第二种判据」删除;删除后重跑本轮两条新增套件
(PlayMode 7/7 · EditMode 3/3)确认行为面为零。全量数跑的是含该方法的树 —— **如实登记**。

---

## 8. 补轮(同日)· 纸面下纯色底(spec §5)

**用户裁定**:复用调色板既有 token **`--skeuo-implement-bg`(`#6B4423`)**,不新增任何字面色值
(三选项:木棕 `--skeuo-implement-bg` / 深棕 `--skeuo-scroll-rod` / 新增 `--skeuo-stage-bg`;
用户取第一项)。

**形态**:42 侧全屏背板 `.menu-backdrop`(绝对定位铺满 `.scene-host`,绘制在纸面之前,
`pickingMode = Ignore`);**不动相机** —— 菜单期启动序第 5 步的相机尚未建出来,
此时渲染的是场景预摆的占位相机(改它要么改不到,要么在关屏后留下一台被改过清屏色的相机)。

**判据**:`main_menu_wiring_test.test_menuBackdrop_isOpaqueCoversPanel_andPaintsBehindPaper`
—— 刻意**不断言色值字面量**(色值归美术轮,断言字面量会把一次调色变成一次测试红),
只钉四条属性:不透明 / 铺满面板 / 绘制在纸面之前 / 不参与拾取。

**⚠️ 同轮把夹具补成生产形状**:此前的 PlayMode 夹具**不挂样式表**(自建空壳 `_styleSheets` 为空)
⇒ `.ruled` / `.menu-backdrop` 这类 USS 规则在夹具里**根本不生效**,「背板不透明」这类判据
在夹具里只能假绿。现按 `#if UNITY_EDITOR` 挂九份真 USS(`casebook_reading_inline_test` 同款守卫;
方法体也加守卫 —— 只守调用点会 CS0234)。

**证伪**:

| # | 变异 | 期望 | 实测 |
|---|---|---|---|
| **M7** | 背板不进树(`_root.Add(backdrop)` 删掉) | 背板判据红 | ✅ 恰 1 红:「背板必须**不透明**(实际 alpha=0)」 |
| **M8** | USS 底色改 `rgba(107,68,35,0.5)` | 同上 | ✅ 恰 1 红:「实际 alpha=0.5」 |
| **M9** | 背板 `Add` 挪到纸面**之后**(仍在树上、仍不透明) | 次序断言红 | ✅ 恰 1 红:「背板必须绘制在纸面**之前**」 |

（另有 M9a「背板挂载点直接删除」= 与 M7 同形,跑出来同样是 alpha=0 —— **重复件,如实登记**,
不另计。）

**全量回归**(三元组:截至时刻 · 批次 · commit):

| 套件 | 结果 | 本轮基线(`04f69ba8`) | 等式 |
|---|---|---|---|
| **EditMode** | `6326 / 6289 passed / 0 failed / 37 skipped` | `6326 / 6289 / 0 / 37` | **+0**(本轮未加 EditMode 件)✅ |
| **PlayMode** | `270 / 266 passed / 0 failed / 2 skipped (+2 inconclusive)` | `269 / 265 / 0 / 2 (+2)` | `270 − 269 = 1` = 新增背板判据 ✅ |

- 执行时刻 **2026-11-01 07:38 / 07:40 CST**(utp `start-time` 23:38:29Z / 23:40:40Z);批次 **主菜单补轮**;两条命令均先 `grep -c "error CS"` = 0。
- 2 条 **Inconclusive** 仍是既有的 `U1Assump6GamepadFocusSpikeTest.*_notRun`(探针件,按设计不跑),非本轮引入。
- ⚠️ **本文件不自引本笔提交的哈希**(哈希取决于本文件内容)⇒ 记为「工作树;基线 = `04f69ba8`」。

**实拍对照**(GUI 模式,同一探针改输出名;脚本副本 `main-menu-backdrop-2026-11-01.cs.txt`):

| 图 | 纸面下是什么 |
|---|---|
| `skeuomorphic-ui/main-menu-2026-11-01.png`(补轮前) | Boot 场景的**天空盒 + 地面**(空世界第一帧)—— 与 §5「不是游戏画面」的主句不冲突,但「纯色底」未兑现 |
| `skeuomorphic-ui/main-menu-backdrop-2026-11-01.png`(补轮后) | **整屏纯木棕 `#6B4423`**;纸面居中、焦点环在行上、无按键提示 |

读数一致:`paper 479×96 at(601,561) centerY=609`(面板 1680×1215 中心 607.5)· `labelCount=1` · `ringOnRow=True`。
