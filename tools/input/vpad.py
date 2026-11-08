#!/usr/bin/env python3
"""uinput 虚拟手柄 —— **真包(独立进程)** 的手柄注入器（2026-11-07）

## 为什么不是既有那台「虚拟手柄」

`InputSystem.AddDevice<Gamepad>()` 只能在**玩家进程内**调用（PlayMode / EditMode 夹具）；
真包是独立进程，进程外唯一的入口是**内核 evdev**。本工具走的就是这一层：

```
本工具 → /dev/uinput → 内核新建 /dev/input/eventN → Unity Linux 后端(evdev) → Gamepad
```

## 前置（一次性，须 sudo）

    sudo bash tools/input/install_uinput_permissions.sh

它做两件事：`/dev/uinput` 对 `gu` 可写；新建的 `event*` 节点对 `gu` 可读。

## 用法

    python3 tools/input/vpad.py --trace tools/input/world_gamepad_trace.json
    python3 tools/input/vpad.py --hold                      # 只造设备并保持（手动注入）
    python3 tools/input/vpad.py --stick 0 1 --seconds 3     # 前推 3 s 后归零
    python3 tools/input/vpad.py --button north --seconds 0.15
    python3 tools/input/vpad.py --info                      # 只造设备、打印节点名

设备一创建就打印 `[vpad] node=/dev/input/eventN`（授权限 / 排查都靠它）。

## 与 PlayMode 夹具的差别（如实登记）

- 覆盖**多出**一段：evdev → Unity Linux 后端 → `Gamepad` 设备。这段只有真设备才验得到。
- 覆盖**少**一段：真实手柄的固件 / 死区 / 厂商映射差异（本工具发的是标准轴值）。
- 轴值：左摇杆 `[-1,1]` → `ABS_X/ABS_Y`（`-32768..32767`，0 = 中位）。
"""

import argparse
import json
import os
import sys
import time

try:
    import evdev
    from evdev import AbsInfo, ecodes
except ImportError:  # pragma: no cover - 环境未装时给可执行的提示
    sys.stderr.write(
        "[vpad] 缺 python-evdev —— python3 -m pip install evdev\n")
    raise SystemExit(2)

NAME = "DYJC Virtual Gamepad"

# Unity `Gamepad` 布局需要的面键名 → evdev 码。
#
# 🔁 **2026-11-08(`TD-walkthrough-01` 收口轮):本表按「**Unity 侧实测落点**」重排。**
#    上一版按「码名对得上」写(`start` → `BTN_START`),实测**全错** —— Unity 的 Linux
#    evdev 后端在本设备的**能力声明顺序**上建 `Gamepad` 布局,同一码在这台虚拟设备上与
#    真手柄落点不同。实测手段:每人按住 0.7 s,读真包 `[tutorialdiag] 手柄=` 的
#    `_P(<control>)` 落点转储(`probe_map.py`)。
#
#    | 本表键(= Unity 意图) | 发这个码 | Unity 实测落点 |
#    |---|---|---|
#    | south / east / west / north | `BTN_SOUTH` / `BTN_EAST` / `BTN_NORTH` / `BTN_WEST` | 同位(west/north **交换**见下) |
#    | **start** | **`BTN_TR2`(313)** | `start` ✅(旧版发 `BTN_START` ⇒ 落到 `leftStickPress`) |
#    | **select** | **`BTN_TL2`(312)** | `select` ✅(旧版发 `BTN_SELECT` ⇒ **什么都不是**) |
#    | l1 / r1 | `BTN_TL` / `BTN_TR` | `leftShoulder` / `rightShoulder` ✅ |
#    | **l3 / r3** | **`BTN_START`(315)** / **`BTN_MODE`(316)** | `leftStickPress` / `rightStickPress` ✅ |
#
#    **不可达(本虚拟设备无码可达,发原码等于什么都没按)**:`leftTrigger` / `rightTrigger`
#    (`BTN_TL2` / `BTN_TR2` 被 start/select 占用)、`guide`(`BTN_MODE` 被 r3 占用)、
#    以及 `BTN_SELECT`(314)/`BTN_THUMBL`(317)/`BTN_THUMBR`(318) 三个码 —— 实测落点为「无」。
#
# ⚠️ **实测(2026-11-07 真包探针)**:`West` / `North` 在 evdev 与 Unity 之间**是反的** ——
#    evdev 的 `BTN_NORTH`(= BTN_X,0x133)在 Unity 里是 **`buttonWest`**;
#    evdev 的 `BTN_WEST`(= BTN_Y,0x134)在 Unity 里是 **`buttonNorth`**。
#    (evdev 按 SNES 方位命名,Unity 按 Xbox 物理方位。)
#    探针证据:`BTN_NORTH` 按下 ⇒ 读数 `手柄=…:W` 且 `急救=Armed`、`档位=Treatment`;
#               `BTN_WEST`  按下 ⇒ 读数 `手柄=…:N`。
#    ⇒ 本表按 **Unity 语义**命名,内部发的是 Unity 认的那个码。
BUTTONS = {
    "south": ecodes.BTN_SOUTH,
    "east": ecodes.BTN_EAST,
    "west": ecodes.BTN_NORTH,     # ← Unity buttonWest(见上)
    "north": ecodes.BTN_WEST,     # ← Unity buttonNorth(见上)
    "start": ecodes.BTN_TR2,      # ← Unity startButton(2026-11-08 实测;非同名的 BTN_START)
    "select": ecodes.BTN_TL2,     # ← Unity selectButton(同上)
    "l1": ecodes.BTN_TL,
    "r1": ecodes.BTN_TR,
    "l3": ecodes.BTN_START,       # ← Unity leftStickPress
    "r3": ecodes.BTN_MODE,        # ← Unity rightStickPress
}

ABS = {
    "x": ecodes.ABS_X,
    "y": ecodes.ABS_Y,
    "rx": ecodes.ABS_RX,
    "ry": ecodes.ABS_RY,
    "z": ecodes.ABS_Z,
    "rz": ecodes.ABS_RZ,
}


def _abs_info(lo, hi, flat=0, fuzz=0):
    """evdev 2.x 的 `AbsInfo(value, min, max, fuzz, flat, resolution)`。"""
    return AbsInfo(0, lo, hi, fuzz, flat, 0)


def build_device():
    caps = {
        ecodes.EV_KEY: list(BUTTONS.values()),
        ecodes.EV_ABS: [
            (ecodes.ABS_X, _abs_info(-32768, 32767, flat=16)),
            (ecodes.ABS_Y, _abs_info(-32768, 32767, flat=16)),
            (ecodes.ABS_RX, _abs_info(-32768, 32767, flat=16)),
            (ecodes.ABS_RY, _abs_info(-32768, 32767, flat=16)),
            (ecodes.ABS_Z, _abs_info(0, 255)),
            (ecodes.ABS_RZ, _abs_info(0, 255)),
            (ecodes.ABS_HAT0X, _abs_info(-1, 1)),
            (ecodes.ABS_HAT0Y, _abs_info(-1, 1)),
        ],
    }
    # 🔁 2026-11-08(TD-walkthrough-01):vendor/product 可经环境变量覆盖,用来分辨
    #   「Unity 的 evdev 映射按型号选表」与「设备本身缺能力」。实测记录见该债。
    #   `0x045E/0x02EA` = Xbox One S(默认);`0x045E/0x028E` = Xbox 360 有线(另一张常见表)。
    ui = evdev.UInput(
        caps,
        name=NAME,
        vendor=int(os.environ.get("VPAD_VENDOR", "0x045E"), 0),
        product=int(os.environ.get("VPAD_PRODUCT", "0x02EA"), 0),
        version=0x0301,
        bustype=ecodes.BUS_USB,
    )
    # 初始化所有轴到中位 / 0，避免首帧出现随机值
    for code in (ecodes.ABS_X, ecodes.ABS_Y, ecodes.ABS_RX, ecodes.ABS_RY,
                 ecodes.ABS_HAT0X, ecodes.ABS_HAT0Y, ecodes.ABS_Z, ecodes.ABS_RZ):
        ui.write(ecodes.EV_ABS, code, 0)
    ui.syn()
    return ui


def _to_abs(v):
    v = max(-1.0, min(1.0, float(v)))
    return int(round(v * 32767))


def set_stick(ui, x, y):
    """`(x, y)` 用 **Unity 约定**:`y > 0` = 前进(+z)。

    ⚠️ 2026-11-07 实测(真包):evdev 的 `ABS_Y` **最大值朝下**,Unity 后端按 evdev 原值映射
    (min = 摇杆向上)⇒ 直接把 `y` 写进 `ABS_Y` 会**反向前后**(推「前进」实际后退:
    探针里 `stick=(0,+1)` 让玩家 cell.z 从 0 走到 −5 并掉出地面)。故此处**取负**。
    """
    ui.write(ecodes.EV_ABS, ecodes.ABS_X, _to_abs(x))
    ui.write(ecodes.EV_ABS, ecodes.ABS_Y, _to_abs(-y))
    ui.syn()


def press(ui, name, down=True):
    code = BUTTONS[name.lower()]
    ui.write(ecodes.EV_KEY, code, 1 if down else 0)
    ui.syn()


def run_steps(ui, steps):
    """按顺序执行时间线。每步:`seconds` + 可选 `stick` / `button`。

    - `stick` 会**保持**到下一次设置（不自动归零）—— 与真摇杆一致；
    - `button` 在本步 `seconds` 之后**自动抬起**（`"release": false` 可关掉）。
    """
    for i, st in enumerate(steps):
        sec = float(st.get("seconds", 0))
        if "stick" in st:
            x, y = st["stick"]
            set_stick(ui, x, y)
            print(f"[vpad] #{i} stick=({x},{y}) hold={sec}s", flush=True)
        if "button" in st:
            name = st["button"]
            press(ui, name, True)
            print(f"[vpad] #{i} button={name} hold={sec}s", flush=True)
        if sec > 0:
            time.sleep(sec)
        if "button" in st and st.get("release", True):
            press(ui, st["button"], False)
            print(f"[vpad] #{i} button={st['button']} released", flush=True)
        if "note" in st:
            print(f"[vpad] #{i} note={st['note']}", flush=True)


def main(argv=None):
    ap = argparse.ArgumentParser(description="uinput 虚拟手柄（真包注入）")
    ap.add_argument("--trace", help="时间线 JSON（见 tools/input/world_gamepad_trace.json）")
    ap.add_argument("--hold", action="store_true", help="造设备后保持（Ctrl-C 退出）")
    ap.add_argument("--info", action="store_true", help="造设备、打印节点名后退出")
    ap.add_argument("--stick", nargs=2, type=float, metavar=("X", "Y"), help="推一次摇杆")
    ap.add_argument("--button", help="按一个面键（south/east/west/north/start/...）")
    ap.add_argument("--seconds", type=float, default=0.0, help="--stick/--button 的保持时长")
    args = ap.parse_args(argv)

    try:
        ui = build_device()
    except evdev.UInputError as exc:
        # 实测形态:`evdev.uinput.UInputError: "/dev/uinput" cannot be opened for writing`
        sys.stderr.write(
            f"[vpad] 造设备失败: {exc}\n"
            "[vpad] 多半是 /dev/uinput 权限 —— 先跑: "
            "sudo bash tools/input/install_uinput_permissions.sh\n")
        return 3
    except OSError as exc:
        sys.stderr.write(f"[vpad] 造设备失败: {exc}\n")
        return 3

    node = getattr(getattr(ui, "device", None), "path", "?")
    print(f"[vpad] node={node}", flush=True)
    print(f"[vpad] name={NAME}", flush=True)

    try:
        if args.trace:
            with open(args.trace, "r", encoding="utf-8") as f:
                doc = json.load(f)
            steps = doc.get("steps", doc) if isinstance(doc, dict) else doc
            run_steps(ui, steps)
        elif args.stick:
            set_stick(ui, *args.stick)
            if args.seconds:
                time.sleep(args.seconds)
            set_stick(ui, 0, 0)
        elif args.button:
            press(ui, args.button, True)
            if args.seconds:
                time.sleep(args.seconds)
            press(ui, args.button, False)
        elif args.hold:
            print("[vpad] hold —— Ctrl-C 退出", flush=True)
            while True:
                time.sleep(1.0)
    except KeyboardInterrupt:
        pass
    finally:
        try:
            ui.close()
        except Exception:
            pass
    print("[vpad] done", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
