# 实现轮 —— 暂停壳「保存」行(参照英灵神殿第二层)

**日期**:2026-11-05 · **轮次**:sprint-24 收口后 · **commit**(回归时点):`3f639bb3` + 工作树
**触发**:用户给出 `ref_valheim/启动第一层.png`、`启动第二层.jpg`,指出英灵神殿的**两层启动结构**;
追问后裁定 =「加『保存』行(带时间)」+「返回主菜单另裁」+「第一层 CREDITS/CINEMATICS 先不补,登记债册」。

---

## 一、两层结构对照(勘察结论)

| 层 | 英灵神殿 | 本项目 |
|---|---|---|
| 第一层 · 程序启动 | START GAME · CINEMATICS · SETTINGS · CREDITS · QUIT | `MainMenuScreen`(`MainMenu.unity` 场景):继续诊治 · 新起一局 · 设置 · 退出游戏 |
| 第二层 · 进入游戏后 | 继续 · 保存(带「上次保持:19分钟前」)· 设置 · **返回主菜单** · 退出游戏 | `PauseScreen`(`ModalId.Pause` 模态):继续 · 存档位 · 调治 · 退出游戏 → **本轮增「保存」= 五行** |

**结构性结论**:项目**已经是两层**(场景级主菜单 + 世界内 42 模态),不需要新增层级。
缺的是第二层的两行与第一层的两行 —— 逐条处置见 §四。

---

## 二、本轮实现(只有「保存」行)

| 文件 | 改什么 |
|---|---|
| `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/PauseScreen.cs` | 五行;`IPauseCommandSink.RequestSave()`;`LastSaveAgeMinutes` 提供者;`SaveHintText` / `RefreshSaveHint()` / `ReportSaveRejected()` / `SavePending` / `CompletePendingSave()` |
| `unity/Assets/Gameplay.Boot/DriverPauseCommandSink.cs` | 可选 `Func<bool> requestSave`(null ⇒ 恒 false ⇒ 屏上如实写「未能落笔」) |
| `unity/Assets/Gameplay.Boot/BootRoot.cs` | 注入 `_saveSlotCommands.TrySave()`;提供 `SaveAgeMinutes`(读当前世界目录最新 `save_*.bin` 的 mtime) |
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | 三处绑定收口成 `BindPause()`;`OnFrameEdge` 在写落定后刷新副标题 |
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` | `.pause-save-hint`(字号走 `--skeuo-font-size-xs`,承 `AC-42-C5` 禁硬编码) |
| `design/ux/pause-shell-42.md` | §5 四行 → **五行** + 新 `AC-PS-12` + `AC-PS-11` 口径订正 |

**语义分工(刻意保留两者,不是替代)**:
- **「保存」** = 一键落进台账 `NextWritableSeq`(不看页、不选槽);
- **「存档位」** = 开册逐行(可覆写指定槽 / 读档)。

**「上次保存」的时间来源 = 墙钟,只用于呈现** —— 不进存档头 / 三流 / sim,
与 ADR-040 §D4「墙钟只用于选择面」同一条边界(已在代码注释与 UX spec 双处写明)。

---

## 三、两道门各拦下一件事(如实登记)

### ① `AC-7a-09` 忘词令 —— 我的命名撞了「quicksave」

第一版把接口方法命名为 `QuickSave`,三个运行期符号(接口方法 / 实现方法 / 私有字段)同时被
`AssemblyGates.RunAll` 判红,报文逐字:

> 「7a 禁 `quicksave` / `quickload` —— 『再覆盖回去』的闭合回路 = 事实上的 quicksave,
> 玩家 NEVER 丢过进度。处置:删除该符号或更名」

⇒ 改名为 **`RequestSave`**。**这是门按设计工作**,不是误报:一键保存确实容易被读成 quicksave,
而本项目的裁定是「保存与读档的闭合回路」要收窄。

### ⚠️ ② 改名脚本**误伤了忘词令自己的负例夹具**

全局 `sed` 式替换把 `save_vocabulary_gate_test.cs` 里的
`DoQuickSave` / `test_vocabularyGate_quickSaveSymbol_injected_isRed` 一起改了 ——
而那个类型**正是「门会红」的负例靶子**(它的存在意义就是含违禁词)。
改名后负例会变绿 ⇒ 忘词令失去判别力。

**已还原**:`git show HEAD:<path>` 整篇写回,`git diff` 复验为空。
⇒ 教训:**违禁词/负例夹具在做全局改名时必须被排除**;按文件清单改而不是按模式扫。

### ③ 行序后移打红三条 PlayMode 导航夹具

第二行插入「保存」⇒「存档位」由行 1 移到行 2,而 `AC-PS-05` / `AC-PS-09` 是**按方向键次数**
走到目标行的。三处各补一次下按 + `RowAt(1) → RowAt(2)`,并在注释里写明「行序后移一位」。

---

## 四、两条新债(用户裁定「先登记」)

| 债 | 内容 | P0 面 |
|---|---|---|
| **`TD-skeuo-14`** | **进入世界后回不到第一层**:启动序是一趟一次性线性 async,主菜单场景只 additive 拆、从不重进(全仓零 `ReloadMainMenu`)。要做须先裁三件(退菜单是否强制存盘 / 拆序六步 / 重走启动序还是只重开屏) | **Y** |
| **`TD-skeuo-15`** | 第一层缺 **CREDITS / CINEMATICS** 两行,且两者在 P0 **零内容**(全仓 `Credits`/`制作名单`/`Cinematic` 零命中,无过场系统)⇒ 现在补行 = **死行** | N |

债面:`条目 186 → 188` · `未闭 P0 面 10 → 11` · `epic 分布 6/31` 不变
(`skeuomorphic-ui` 由 2 → 3)。复算 `python3 tools/debtcount/debtcount.py --print`。

---

## 五、回归(三元组)

| 项 | 值 |
|---|---|
| **本轮 EditMode** | **`6932 / 6895 / 0 / 0 / 37`** |
| 基线 EditMode | `6929 / 6892 / 0 / 0 / 37` @ `2026-11-05` · smoke 修复轮 · `3f639bb3` |
| 等式 | `6932 − 6929 = 3` = 新增 `[Test]` 数(保存行三条)· `6895 − 6892 = 3` · **failed 0** |
| **本轮 PlayMode** | **`341 / 337 / 0 / 2 / 2`**(与基线逐位相同 —— 三条导航夹具是**改步数**,不是加用例) |
| 复算 | `python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/ed-final3-2026-11-05.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

**中途两轮红**如实登记:第一次 3 条(忘词令)+ 3 条(导航夹具),第二次 1 条(「调治」行索引);
全部由本轮的**改名**与**行序后移**引起,不是新缺陷。

---

## 六、本件**没有**证明的事

1. **不证明副标题在真机上的观感** —— 字号 / 行高 / 与主标题的对比度都要人眼走查
   (`AC-PS-12` 的 `[ ]` 未勾)。
2. **不证「保存」在真实叙事窗口内点得动** —— `TrySave()` 的门(c)要求处于叙事上下文中,
   夹具用的是可变 sink,没有走过 `SaveService` 的真门。
3. **不证返回主菜单 / CREDITS / CINEMATICS** —— 三条按裁定**不做**,只登记(`TD-skeuo-14` / `-15`)。
