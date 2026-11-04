# 实现原件 — 主菜单固定四行 + 删除世界能力移除(2026-11-04)

> **对象**:`MainMenuScreen` / `IMainMenuCommandSink` / `DriverMainMenuCommandSink` / `BootRoot` /
> `SkeuoRuntimeDriver` / `WorldCatalog`
> **缘起**:用户三轮裁定 —— ①「无论多少世界,主菜单暂时固定三行,和世界多少无关」;
> ②「删除不留」;③「第一行的世界列表也删掉,退出游戏这行同意增加」。
> **同批落地**:「设置」行(复用 `ModalId.SettingsShell`,闭集不增员)·「退出游戏」行(全仓首个
> `Application.Quit` 调用点)。
> **关联**:ADR-040(D6 撤销注)· `TD-persist-21`(新立)· `main-menu-42.md`(§5 / §6 / §13 就地改写)。

---

## 1. 变更面

### 1.1 代码

| 文件 | 变更 |
|---|---|
| `Gameplay.UI/Skeuomorphic/Screens/MainMenuScreen.cs` | 重写:固定四行;删 `MainMenuWorldView` / `MainMenuMode` / 世界列表 / 删除态 / `_focusIndex` 焦点复位(行集恒定 ⇒ 无重画路径);新增 `ShellEntryText` / `QuitText` / `FixedRowCount` |
| `Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | 删 `SetMainMenuWorlds`;`ShowSceneLevelScreen` 去掉 `worlds` 形参 |
| `Gameplay.Boot/DriverMainMenuCommandSink.cs` | 接口面 = `Continue` / `NewWorld` / `OpenSettings` / `Quit` |
| `Gameplay.Boot/BootRoot.cs` | 删 `OnMainMenuDeleteWorld` 与 `BuildWorldRows`;新增 `AnyWorldHasSaves()`(第一行文案的唯一输入);接「设置」(开 `SettingsShell`)与「退出」( `Application.Quit()` ) |
| `Gameplay.Boot/WorldCatalog.cs` | 删 `DeleteWorld(int)` |

### 1.2 夹具

- `main_menu_worlds_test` 9 → 5 条(删 5 条删除态,新增固定行集 / 串线 / 未接线三面)
- `world_catalog_test` −2(`DeleteWorld` 两条)
- `multi_world_boot_wiring_test`:删真启动序删除件,新增 `test_settingsRow_opensSettingsShell_andClosesBackToMenu`
- `main_menu_wiring_test`:文本元素计数 1 → 4;`CountingSink` 实现新接口
- `resume_existing_save_playmode_test`:行数断改 `FixedRowCount`
- `multi_world_visual_walkthrough`:删除态两帧 → 设置壳一帧 + 合壳回菜单一帧

### 1.3 文档

- `design/ux/main-menu-42.md`:§5 线框与行表 · §6 状态表(删除态/多世界标撤销)· `AC-MM-11/12` 改写 · `AC-MM-14` 作废 · 新增 `AC-MM-15`
- `docs/architecture/adr-040-...md`:D6 就地加撤销注(原文保留)
- `docs/tech-debt-register.md`:`TD-persist-21` 立债 + 七十二订 + §五 重生成
- `docs/architecture/adr-041-save-slot-overwrite.md`(同日另案,未实现)

---

## 2. 如实登记:**能力被移除**,不是「暂时不显示」

| 能力 | 现状 | 后果 |
|---|---|---|
| 选世界 | **无入口**(`SelectWorld` 已从接口删除) | 「继续诊治」恒进最近游玩的世界;其余世界目录**游戏内不可达** |
| 删除世界 | **无入口**(含 `WorldCatalog.DeleteWorld`) | 只能手动删 `~/.config/unity3d/DefaultCompany/unity/saves/wN/`;世界目录只增不减 |

⇒ 两条合并登记为 **`TD-persist-21`**(`open` / P0 面 **Y**),解除动作 = 恢复行与目录能力,
**不得**用「去文件管理器删」当出路(那正是 ADR-040 §Context 第 1 条点名的病灶)。

> 🔁 **2026-11-05 补注**:`TD-persist-21` 已由用户裁定「**不做,解除债**」**结案** ——
> 即上述两条能力缺失**被确认为当前形态**,不再是待办。复开须新立债。

---

## 3. 判据与回归

```bash
# EditMode
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode -testResults "$PWD/Logs/editmode-menu-refactor2.xml" \
  -logFile "$PWD/Logs/editmode-menu-refactor2.log" -nographics
# PlayMode
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode -testResults "$PWD/Logs/playmode-menu-refactor.xml" \
  -logFile "$PWD/Logs/playmode-menu-refactor.log" -nographics
```

| 套件 | 结果 | 三元组 |
|---|---|---|
| EditMode | `6861/6824/0/37` | 截至 `2026-11-04T13:15:17Z` · sprint-24 主菜单固定行轮 · `238eef15` + 工作树 |
| PlayMode | `339/335/0/2/2` | 截至 `2026-11-04T13:18:10Z` · 同批次 · `238eef15` + 工作树 |
| 对照基线 | EditMode `6867/6830/0/37` · PlayMode `339/335/0/2/2` | `引用自 production/session-state/active.md` @ `2026-11-04T11:23:40Z`(读档时钟轮,`0e916e8c`) |

**差值核算**:EditMode `6867 → 6861` = **−6**,恰为 `main_menu_worlds_test` 9→5(−4)与
`world_catalog_test` −2 之和,通过数同步 −6;**PlayMode 339/335 与基线**逐位相同**
(删 1 条真启动序删除件、加 1 条设置壳件,净 0)。⇒ **零回归**。

### 3.1 首轮 PlayMode 的 3 红 —— **全是夹具侧**,已修

| 红 | 真因 | 处置 |
|---|---|---|
| `test_firstRun_newWorldLandsInW1_notW2` | 遗留断言「没有世界 ⇒ 单行」(`Expected 1, was 4`) | 改断 `FixedRowCount` |
| `walkthrough_firstRun_menuAndRealSaveSlots` | 同上 | 同上 |
| `test_settingsRow_opensSettingsShell_andClosesBackToMenu` | 我用错访问器:`CurrentScreen` 是**场景级屏**(主菜单),模态在 `Screen` | 改读 `_driver.Screen`,并在夹具里写明「是夹具错,不是实现错」 |

⚠️ 三条都是**我的夹具**没跟上形态变更 / 读错访问器,**没有一条指向实现缺陷**;
第二轮 `339/335/0/2/2` = 基线逐位相同。

> ⚠️ **如实登记**:PlayMode 那一轮之后,`main_menu_wiring_test` 里**只追加了一段注释**
> (说明方法名 `SingleRow` 是历史名字、被三份既有证据件按名引用),**零行为变化**;
> 未重跑。除此之外工作树与该轮逐字节一致。

---

## 4. 中途抓到的一处门命中(已修)

`settings_shell_test.test_settingsShell_hasNoSettings` 扫 `Skeuomorphic/**` 成员名含
`settings / config / preference` 的**引用类型成员** ⇒ 我首版把行文案命名为
`public const string SettingsText`(`string` 是引用类型)当场转红。
**修法 = 改名** `ShellEntryText`(不改门)—— 与 `.claude/docs/coding-standards.md`
§「命名 — 机械门避让表」同一条纪律。
