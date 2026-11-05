# `TD-skeuo-17` 兑现轮 —— 模态开合驱动提前到主菜单闸门之前

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05(深夜) |
| **用户指令** | 「按①把 `_modalInput` 提前,然后首启复验」 |
| **三元组** | 截至 `2026-11-05 23:40 CST` · 批次 `TD-skeuo-17 兑现轮` · commit `9b6428a9` + 工作树 |

---

## 1. 改了什么

| 面 | 落点 | 内容 |
|---|---|---|
| 生产 | `Gameplay.Boot/BootRoot.cs` — 新增 `EnsureModalInput()` | **幂等**装配(`_modalInput != null \|\| _uiDriver == null` ⇒ 直接返回)。`canOpenPause: () => _booted` 语义不变(第一层只关不开) |
| 生产 | `PresentMainMenuAsync` 末尾(设完 `MainMenuSink` 之后、等确认之前) | 调一次 `EnsureModalInput()` —— **这是修复本体**:主菜单摊开的那一刻驱动已就绪 |
| 生产 | 启动序 6.3 | 原来直接 `new ModalInputDriver(...)` → 改为 `EnsureModalInput()`(同一份装配,不再两处各写一遍) |
| 夹具 | `Tests/PlayMode/main_menu_wiring_test` 新增 `test_mainMenu_modalInputDriver_isWiredWhenMenuAppears` | 断言**主菜单摊开时** `_modalInput` 非 null |

**为什么是「幂等的单一装配点」而不是「把构造语句搬到前面」**:搬语句会留下**两处构造**(菜单一处、6.3 一处),
两处的参数(尤其 `canOpenPause`)必然分叉且谁都不报错 —— 与本项目反复抓过的「第二实现」同型。

## 2. 证伪轮(1 变异,红在靶点;SHA-256 逐字节还原)

| 变异 | 做法 | 结果 |
|---|---|---|
| **M1** | 删掉 `PresentMainMenuAsync` 里的 `EnsureModalInput();`(保留 6.3 那处) | 新门**转红**,报文逐字:「主菜单摊开时 `_modalInput` 仍为 null ⇒ 第一层开出来的设置壳没有任何东西接 ESC」;其余 8 条**全绿**(零连带)。还原后 `sha256` 一致 |

⚠️ M1 复刻的正是**修复前**的形态 ⇒ 这条门的判别力就锚在病灶上,不是锚在「代码长什么样」。

## 3. 首启复验(用户要求的判据 —— 真包,首次启动路径)

```bash
# 出包
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -quit -nographics \
  -projectPath "$PWD" -executeMethod DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build \
  -logFile Logs/player-build-tdsk17.log
# ⇒ Result: Succeeded · 23.98 s · 123 546 209 B
```

| 步 | 观察 | 图 |
|---|---|---|
| 启动 → 主菜单 | 四行正常 | `09-tdsk17-firstlaunch-menu.png` |
| ↓↓ → Enter | 设置壳摊开(标题「调治」) | `10-tdsk17-firstlaunch-settings-open.png` |
| **ESC** | **壳关掉,回到主菜单,焦点停在「设置」行** | `11-tdsk17-firstlaunch-esc-CLOSES.png` |
| 退出游戏 | 进程正常退出(日志 `Input System … Shutdown`) | — |

⚠️ **判据必须是「首次启动」**:上一轮就是在这个点上被绕开的 —— 回程的第一层因为驱动已存在,
同一操作本来就正常。**本轮的复验刻意只走首启**,不复用回程结论。

## 4. 回归

| 套件 | 结果 | 等式 / 对照 |
|---|---|---|
| EditMode | **`6941 / 6904 / 0 / 0 / 37`**(`ed-tdsk17.xml`) | 与上一轮逐位相同(本轮零 EditMode 改动) |
| PlayMode | **`342 / 338 / 0 / 2 / 2`**(`pm-tdsk17.xml`) | `342 − 341 = 1` = 新增门数 ✅ |
| 构建 | `Result: Succeeded` · 错误数 0 · 23.98 s | — |

## 5. 未做 / 边界

1. **没有断言「按 ESC 真能关壳」** —— 那要真的注入键盘事件,而 `PlayerInputReader` 直读
   `Keyboard.current`,batch 下键盘注入受工程设置限制。本门断的是**根因**(驱动是否在场),
   真机上的端到端由 §3 的首启复验覆盖。**两者互补,不互相替代。**
2. **`TD-skeuo-17` 结案 ≠ smoke 第三轮转绿** —— 该轮 Verdict 仍为 **FAIL**,因为同轮还发现
   关键路径 #9(脉案 `ICasebookDtoSource` 未注入 ⇒ 值区恒空)未解。**smoke 必须重跑才谈得上 gate。**
3. 诊断期顺带确认:`?.` 的静默是本类缺陷的放大器(「驱动不存在」与「没有按键」同一种沉默)。
   **未加告警** —— 有了本门之后,这类回归会在夹具层就红,不必再靠运行期日志。
