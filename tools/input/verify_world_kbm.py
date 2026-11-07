#!/usr/bin/env python3
"""真包 · 世界空间**键鼠**路径的端到端判据(2026-11-07)

与 `verify_world_gamepad.py` 同口径、同读数,只是注入层换成 **X11 XTest**:

    走近 = 按住 W     就诊 = 按 E     急救档位 = 按住 F(3.5 s)

⚠️ 与手柄那一侧的两处结构性差别(不得互推):
  1. **必须有窗口 + 焦点** —— XTest 只发给焦点窗口;手柄(uinput)是设备级、不需要焦点。
     故本脚本**不能**用 `-batchmode -nographics`(无 X 连接 ⇒ 无法注入)。
  2. **注入期间要周期性重置焦点** —— 别的窗口抢焦会让按键全部打到别处。

判据来自 `[tutorialdiag]` 的 `cell=` / `急救=` / `模态=` / `档位=` / `键=`
(后四列里的 `键=` 是本轮为排查加的:区分「键没进 Input System」与「没被消费」)。
"""

import argparse
import os
import re
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import verify_world_gamepad as gw  # noqa: E402  复用 parse/judge
import xkeys                     # noqa: E402

ROOT = gw.ROOT
DEFAULT_BUILD = gw.DEFAULT_BUILD
DEFAULT_LOG = "/tmp/dyjc-kbm-world.log"
RE_KEY = re.compile(r"键=(\S+)")


def main(argv=None):
    ap = argparse.ArgumentParser(description="真包世界空间键鼠路径判据")
    ap.add_argument("--build", default=DEFAULT_BUILD)
    ap.add_argument("--log", default=DEFAULT_LOG)
    ap.add_argument("--min-advance", type=int, default=3)
    args = ap.parse_args(argv)

    if not os.path.exists(args.build):
        sys.stderr.write(f"[kbm] 找不到 player: {args.build}\n")
        return 2
    if os.path.exists(args.log):
        os.remove(args.log)

    cmd = [args.build, "-tutorialdiag", "-logFile", args.log,
           "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"]
    print("[kbm] 启动 player:", " ".join(cmd), flush=True)
    player = subprocess.Popen(cmd, cwd=os.path.dirname(args.build),
                              stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)

    try:
        win = None
        deadline = time.time() + 20.0
        while time.time() < deadline:
            win = xkeys.find_window(player.pid)
            if win is not None:
                break
            time.sleep(0.3)
        if win is None:
            print("[kbm] ⚠️ 找不到玩家窗口 —— XTest 无处可发", flush=True)
            return 2
        xkeys.focus(win)
        print("[kbm] 已置焦玩家窗口", flush=True)

        last_focus = [time.time()]

        def tick_focus():
            if time.time() - last_focus[0] > 1.5:
                xkeys.focus(win)
                last_focus[0] = time.time()

        def wait(seconds):
            end = time.time() + seconds
            while time.time() < end:
                tick_focus()
                time.sleep(0.2)

        def last_row():
            rows = gw.parse(args.log)
            for r in reversed(rows):
                if r["cell"] is not None:
                    return r
            return None

        def key_readout():
            try:
                with open(args.log, "r", encoding="utf-8", errors="replace") as f:
                    for line in reversed(f.readlines()):
                        m = RE_KEY.search(line)
                        if m and line.startswith("[tutorialdiag]"):
                            return m.group(1)
            except FileNotFoundError:
                pass
            return "?"

        # ── 进世界:主菜单确认首行(Return)──
        wait(4.0)
        xkeys.tap("Return")
        t0 = time.time()
        while time.time() - t0 < 12.0 and last_row() is None:
            tick_focus(); time.sleep(0.3)
        if last_row() is None:
            # 首行可能没焦点:试一次 Down + Return(第二行 = 新起一局)
            print("[kbm] 首行 Return 没进世界,试 Down + Return", flush=True)
            xkeys.tap("Down"); wait(0.3); xkeys.tap("Return")
            t0 = time.time()
            while time.time() - t0 < 12.0 and last_row() is None:
                tick_focus(); time.sleep(0.3)
        if last_row() is None:
            print("[kbm] ⚠️ 没进世界 —— 后面判据都会是 FAIL", flush=True)
        else:
            print("[kbm] 已进世界,读数在跑", flush=True)

        # ── ③ 急救档位:F 按住 3.5 s ──
        xkeys.key_down("f")
        wait(3.5)
        print(f"[kbm] 按住 F 期间 读数 键={key_readout()}", flush=True)
        xkeys.key_up("f")
        wait(1.0)

        # ── ① 走近:按住 W 自适应 ──
        row = last_row()
        if row and row.get("dist") is not None and row["dist"] <= 4:
            # 起点已挨着病人 ⇒ 先退开(S 2 s),否则没有可观测行程
            xkeys.key_down("s"); wait(2.0); xkeys.key_up("s"); wait(0.4)
        xkeys.key_down("w")
        t0 = time.time()
        while time.time() - t0 < 15.0:
            row = last_row()
            if row and row.get("dist") is not None and row["dist"] <= 2:
                break
            tick_focus(); time.sleep(0.2)
        xkeys.key_up("w")
        wait(0.5)
        row = last_row()
        print(f"[kbm] 走完后 距离={row['dist'] if row else '?'} cell={row['cell'] if row else '?'} 键={key_readout()}",
              flush=True)

        # ── ② 就诊:E ──
        xkeys.tap("e", ms=200)
        wait(2.5)
    finally:
        if player.poll() is None:
            player.terminate()
            try:
                player.wait(timeout=10)
            except subprocess.TimeoutExpired:
                player.kill()

    rows = gw.parse(args.log)
    print(f"[kbm] 读到 {len(rows)} 条 tutorialdiag 读数(日志 {args.log})")
    results = gw.judge(rows, args.min_advance)

    print("\n=== 判据 ===")
    failed = 0
    for name, ok, detail in results:
        print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}")
        if not ok:
            failed += 1
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
