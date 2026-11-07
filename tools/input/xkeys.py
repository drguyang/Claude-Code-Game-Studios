#!/usr/bin/env python3
"""X11 XTest 键鼠注入(真包窗口模式)· 2026-11-07

## 为什么不用 uinput 键盘

实测:给 `/dev/input/event*` 授权**之前**,游戏就能读键盘;而当时那些节点是
`root:input 0660`、无 ACL ⇒ Unity 在图形会话下的**键盘走 X11**、不经 evdev。
⇒ 键鼠注入用 XTest(既有走查一直用、被证明有效的那条),不用 uinput。

## 代价

XTest 只把事件发给**焦点窗口** ⇒ 必须先 `set_input_focus`,且注入期间要**周期性重置**。
(手柄那一侧不需要焦点,uinput 是设备级 —— 两条路的前提不同,不要互推。)

## 用法

    from xkeys import focus, hold, tap
    focus(find_window(pid))
    hold("w", 2.0)
    tap("Return")
"""

import time

from Xlib import X, XK, display
from Xlib.ext import xtest

_DISPLAY = display.Display(":0")
_PID_ATOM = _DISPLAY.intern_atom("_NET_WM_PID")


def find_window(pid):
    """按 `_NET_WM_PID` 找玩家窗口(与 `verify_world_gamepad.find_player_window` 同法)。"""
    root = _DISPLAY.screen().root

    def walk(w, depth=0):
        try:
            kids = w.query_tree().children
        except Exception:
            return None
        for c in kids:
            try:
                if c.get_attributes().map_state == X.IsViewable:
                    p = c.get_full_property(_PID_ATOM, X.AnyPropertyType)
                    if p and p.value and int(p.value[0]) == pid:
                        return c
            except Exception:
                pass
            if depth < 3:
                hit = walk(c, depth + 1)
                if hit is not None:
                    return hit
        return None

    return walk(root)


def focus(win):
    if win is None:
        return False
    try:
        win.configure(stack_mode=X.Above)
        win.set_input_focus(X.RevertToParent, X.CurrentTime)
        _DISPLAY.sync()
        return True
    except Exception:
        return False


def _code(name):
    ks = XK.string_to_keysym(name)
    if ks == 0:
        raise ValueError(f"未知 keysym: {name}")
    return _DISPLAY.keysym_to_keycode(ks)


def key_down(name):
    xtest.fake_input(_DISPLAY, X.KeyPress, _code(name))
    _DISPLAY.sync()


def key_up(name):
    xtest.fake_input(_DISPLAY, X.KeyRelease, _code(name))
    _DISPLAY.sync()


def hold(name, seconds):
    key_down(name)
    time.sleep(seconds)
    key_up(name)


def tap(name, ms=120):
    key_down(name)
    time.sleep(ms / 1000.0)
    key_up(name)
