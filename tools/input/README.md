# tools/input —— 真包手柄注入（B 路）

> 2026-11-07 立，同日跑通。回答一个问题：**真包（独立进程）的手柄路径怎么验。**
> PlayMode 的 `InputSystem.AddDevice<Gamepad>()` 只能在**玩家进程内**用，真包是另一个进程，
> 进程外唯一入口是内核 evdev ⇒ 本目录走的就是那一层。

```
vpad.py ──► /dev/uinput ──► 内核建 /dev/input/eventN ──► Unity Linux 后端 ──► Gamepad
```

## 一次性授权（须 sudo，跑一次）

```bash
sudo bash tools/input/install_uinput_permissions.sh
```

按**用户组**授权（不是 0666）：

| 目标 | 改前（实测） | 改后 |
|---|---|---|
| `/dev/uinput` | `root:root 0600` | `root:gu 0660` ⇒ 注入器能造虚拟手柄 |
| `/dev/input/event*` | `root:input 0660`、无 ACL | `root:gu 0660` ⇒ 游戏能读手柄 |

⚠️ 键盘一直能读，是因为 Unity 在图形会话下**走 X11、不经 evdev** —— 所以键鼠走查正常，
手柄却从来没有人验过（也读不到）。

撤销：`sudo rm /etc/udev/rules.d/99-dyjc-input.rules && sudo udevadm control --reload-rules`

依赖：`python3 -m pip install evdev`（用清华源；本机实测源码编译通过）。

## 三个文件

| 文件 | 作用 |
|---|---|
| `vpad.py` | uinput 虚拟手柄：造设备 + 推摇杆/面键；创建即打印 `node=/dev/input/eventN` |
| `verify_world_gamepad.py` | 起 player（`-tutorialdiag -batchmode -nographics`）→ **边读读数边推输入**（自适应）→ 判 PASS/FAIL |
| `world_gamepad_trace.json` | 静态时间线，供 `vpad.py --trace` 手动复现（自适应脚本已不依赖它） |
| `install_uinput_permissions.sh` | 上面那条 sudo |

## 跑

```bash
python3 tools/input/vpad.py --info       # 1) 设备能造出来
python3 tools/input/verify_world_gamepad.py   # 2) 端到端三条判据
```

判据来自 `[tutorialdiag]` 的 `cell=` / `急救=` / `模态=` / `档位=`：

| # | 判据 | 读数 |
|---|---|---|
| ① 走近 | `cell.z` 行程 ≥ 3 格，且最终 `距离 ≤ 2` | `cell=` / `距离=` |
| ② 就诊 | 出现过 `模态=Casebook` | 变化行 `模态=` |
| ③ 急救档位 | `急救=Armed` → `档位=Treatment` → 松开后 `档位=Explore` | 周期行 + 变化行 |

**实测一轮的结果（2026-11-07）**：

```
PASS ① 走近    cell.z 区间 [0, 14]，前进 14 格
PASS ③ 急救档位 Armed 样本 3 · Treatment 样本 4 · Treatment 之后回 Explore 样本 6
PASS ② 就诊    模态=Casebook 样本 1
```

## 实测踩到的五个坑（**都是真的会静默失败的那种**）

1. **摇杆 Y 轴反向**：evdev 的 `ABS_Y` 最大值朝下，Unity 按原值映射 ⇒ 直接写 `y` 会让「前进」
   变成后退（第一轮把玩家推到 `cell.z = −5` 掉出地面）。`vpad.set_stick` 内部**取负**。
2. **`West` / `North` 在 evdev 与 Unity 之间是反的**：evdev `BTN_NORTH`(0x133) = Unity
   **`buttonWest`**；evdev `BTN_WEST`(0x134) = Unity **`buttonNorth`**（evdev 按 SNES 方位命名，
   Unity 按 Xbox 物理方位）。探针证据：按 0x133 ⇒ 读数 `手柄=…:W` 且 `急救=Armed`。
3. **窗口失焦 ⇒ 设备被停用 + 游戏停更**：`ResetAndDisableNonBackgroundDevices` +
   `runInBackground=false` ⇒ 读数列从 `手柄=Gamepad#3` 变 `手柄=无`，随后所有按键静默丢失
   （实测正好吞掉「就诊」那一按）。⇒ 验证器默认用 `-batchmode -nographics`（无窗口即无失焦）。
4. **模态摊开 ⇒ tick 停**（ADR-013 §十-C）⇒ 周期读数不再出现。第一版把「就诊」判成 FAIL，
   其实脉案**已经开了**。⇒ 新增**每帧、变化才打**的 `模态=… 档位=…` 行。
5. **世界是预载、sim 停在主菜单**：必须先按 South 确认首行才进世界；否则读数一行都没有
   （看起来像「设备没被识别」）。

## 如实边界

- **覆盖多一段**：evdev → Unity Linux 后端 → `Gamepad` 设备（PlayMode 夹具到不了这一层）。
- **仍不覆盖**：真实手柄的固件 / 死区 / 厂商映射差异；手感主观判断。
- `-batchmode -nographics` 下无窗口、无渲染 ⇒ **画面 / 布局 / 焦点可见性**都不在判据面内。
