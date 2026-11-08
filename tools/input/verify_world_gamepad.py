#!/usr/bin/env python3
"""真包 · 世界空间手柄路径的端到端判据（2026-11-07）

把三件事做成可跑的 PASS/FAIL:
  ① 走近 —— `[tutorialdiag]` 的 `cell=` 在时间线上真的前进(默认 ≥3 格)
  ② 就诊 —— `模态=Casebook` 出现过(且 `CaseOpened` 由它隐含)
  ③ 急救档位 —— `急救=Armed` → `档位=Treatment` → 松开后 `档位=Explore`

依赖已落地的读数(2026-11-07 同批加进 `WalkthroughDiagnostics.Report`):
`模态=`(SkeuoRuntimeDriver.CurrentModal)与 `档位=`(CameraRig.Mode)。

用法:
    python3 tools/input/verify_world_gamepad.py
    python3 tools/input/verify_world_gamepad.py --build unity/builds/Linux-x64/大医精诚.x86_64 \
        --trace tools/input/world_gamepad_trace.json --log /tmp/dyjc-vpad.log

前置: `sudo bash tools/input/install_uinput_permissions.sh` 已跑过。
"""

import argparse
import os
import re
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_BUILD = os.path.join(ROOT, "unity/builds/Linux-x64/大医精诚.x86_64")
DEFAULT_TRACE = os.path.join(ROOT, "tools/input/world_gamepad_trace.json")
DEFAULT_LOG = "/tmp/dyjc-vpad-world.log"
VPAD = os.path.join(ROOT, "tools/input/vpad.py")
VPAD_DIR = os.path.dirname(VPAD)

RE_TICK = re.compile(r"\[tutorialdiag\] t=([\d.]+) tick=(\d+)")
RE_CELL = re.compile(r"cell=\((-?\d+),(-?\d+),(-?\d+)\)")
RE_EMERG = re.compile(r"急救=(\S+)")
RE_MODAL = re.compile(r"模态=(\S+)")
RE_CAM = re.compile(r"档位=(\S+)")
RE_DIST = re.compile(r"距离=(-?\d+)")


# ── X11 焦点 ────────────────────────────────────────────────────────────────
# ⚠️ **实测(2026-11-07)**:真包一旦**失焦**,Input System 会按
#    `ResetAndDisableNonBackgroundDevices` **停用设备** —— 读数列会从
#    `手柄=Gamepad#3:` 变成 `手柄=无`,此后所有手柄输入静默失效(实测发生在约 21 s 处,
#    正好把「就诊」那一按吞掉)。PlayMode 夹具不会遇到这一层,所以只能在这里处理:
#    启动后把玩家窗口置顶并 set_input_focus,且**在注入期间周期性重取**。
def _x11():
    try:
        from Xlib import X, display
    except ImportError:
        return None, None
    return display.Display(":0"), X


def find_player_window(pid):
    disp, X = _x11()
    if disp is None:
        return None
    atom = disp.intern_atom("_NET_WM_PID")
    root = disp.screen().root

    def walk(w, depth=0):
        try:
            kids = w.query_tree().children
        except Exception:
            return None
        for c in kids:
            try:
                if c.get_attributes().map_state == X.IsViewable:
                    p = c.get_full_property(atom, X.AnyPropertyType)
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


def focus_window(win):
    if win is None:
        return False
    disp, X = _x11()
    if disp is None:
        return False
    try:
        win.configure(stack_mode=X.Above)
        win.set_input_focus(X.RevertToParent, X.CurrentTime)
        disp.sync()
        return True
    except Exception:
        return False



def parse(path):
    rows = []
    if not os.path.exists(path):
        return rows
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            if not line.startswith("[tutorialdiag] "):
                continue
            m_cell = RE_CELL.search(line)
            m_em = RE_EMERG.search(line)
            m_mo = RE_MODAL.search(line)
            m_cam = RE_CAM.search(line)
            # 周期行带全字段;「模态/档位变化」行只带 模态=/档位=(模态摊开 ⇒ tick 停,
            # 周期行不再出现 —— 那正是「就诊开了册」唯一可见的地方)。
            if not (m_mo and m_cam):
                continue
            m_du = RE_DIST.search(line)
            m_t = RE_TICK.search(line)
            rows.append({
                "t": float(m_t.group(1)) if m_t else -1.0,
                "cell": tuple(int(g) for g in m_cell.groups()) if m_cell else None,
                "emerg": m_em.group(1) if m_em else None,
                "modal": m_mo.group(1) if m_mo else None,
                "cam": m_cam.group(1) if m_cam else None,
                "dist": int(m_du.group(1)) if m_du else None,
            })
    return rows


def judge(rows, min_advance):
    results = []

    if not rows:
        return [("读数", False, "日志里没有任何 `[tutorialdiag]` 行 —— 启动加 `-tutorialdiag` 了吗?")]

    zs = [r["cell"][2] for r in rows if r["cell"]]
    advance = max(zs) - min(zs)
    results.append(("① 走近", advance >= min_advance,
                    f"cell.z 区间 [{min(zs)}, {max(zs)}],前进 {advance} 格(要求 ≥ {min_advance})"))

    armed = [i for i, r in enumerate(rows) if r["emerg"] == "Armed"]
    treat = [i for i, r in enumerate(rows) if r["cam"] == "Treatment"]
    explore_after = [i for i, r in enumerate(rows)
                     if r["cam"] == "Explore" and treat and i > treat[0]]
    ok_tier = bool(armed) and bool(treat) and bool(explore_after)
    results.append(("③ 急救档位", ok_tier,
                    f"Armed 样本 {len(armed)} · Treatment 样本 {len(treat)} · "
                    f"Treatment 之后回 Explore 样本 {len(explore_after)}"))

    casebook = [i for i, r in enumerate(rows) if r["modal"] == "Casebook"]
    ok_visit = bool(casebook)
    detail = f"模态=Casebook 样本 {len(casebook)}"
    if casebook:
        detail += f"(首个在第 {casebook[0]} 个读数,cell={rows[casebook[0]]['cell']})"
    results.append(("② 就诊", ok_visit, detail))

    return results


_SHOT_WIN = [None]


def _shot(out_dir, name, log):
    """抓玩家窗口(裁到窗口矩形)。⚠️ 图不进库:`.gitignore:346` 覆盖 evidence/**/*.png。"""
    from Xlib import X, display as _xdisplay
    from PIL import Image as _Image
    dsp = _xdisplay.Display(":0")
    w = _SHOT_WIN[0]
    root = dsp.screen().root
    g = root.get_geometry()
    if w is not None:
        try:
            w.configure(stack_mode=X.Above); dsp.sync(); time.sleep(0.35)
        except Exception:
            pass
    raw = root.get_image(0, 0, g.width, g.height, X.ZPixmap, 0xFFFFFFFF)
    img = _Image.frombytes("RGB", (g.width, g.height), raw.data, "raw", "BGRX")
    if w is not None:
        try:
            geo = w.get_geometry()
            tr = w.translate_coords(root, 0, 0)
            x, y = -tr.x, -tr.y
            img = img.crop((max(0, x), max(0, y),
                            min(g.width, x + geo.width), min(g.height, y + geo.height)))
        except Exception:
            pass
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, name)
    img.save(path)
    print(f"[verify][shot] {path} {img.width}x{img.height} 模态={_last_modal(log)}", flush=True)


def _last_modal(log):
    try:
        with open(log, "r", encoding="utf-8", errors="replace") as f:
            for line in reversed(f.readlines()):
                if line.startswith("[tutorialdiag]"):
                    m = RE_MODAL.search(line)
                    if m:
                        return m.group(1)
    except FileNotFoundError:
        pass
    return "?"


def main(argv=None):
    ap = argparse.ArgumentParser(description="真包世界空间手柄路径判据")
    ap.add_argument("--build", default=DEFAULT_BUILD)
    ap.add_argument("--trace", default=DEFAULT_TRACE)
    ap.add_argument("--log", default=DEFAULT_LOG)
    ap.add_argument("--timeout", type=float, default=90.0, help="整轮超时(秒)")
    ap.add_argument("--min-advance", type=int, default=3, help="走近判据:cell.z 至少前进几格")
    ap.add_argument("--keep-log", action="store_true", default=True)
    ap.add_argument("--walk3911", metavar="OUTDIR", default=None,
                    help="跑 AC-39-11 的 [L] 手柄走查(隐含 --windowed,要窗口才能截图)")
    ap.add_argument("--windowed", action="store_true",
                    help="开窗口跑(默认 -batchmode -nographics:无窗口 ⇒ 不受失焦停更影响)")
    args = ap.parse_args(argv)

    if args.walk3911:
        args.windowed = True

    if not os.path.exists(args.build):
        sys.stderr.write(f"[verify] 找不到 player: {args.build}\n")
        return 2

    if os.path.exists(args.log):
        os.remove(args.log)

    cmd = [args.build, "-tutorialdiag", "-logFile", args.log]
    if not args.windowed:
        # ⚠️ 实测:开窗口跑时,一旦失焦(别的窗口抢走)Unity 会**停更**(runInBackground=false)
        #    ⇒ 日志断流、输入静默失效(本轮连续两次把「就诊」那一按吞在窗口失焦后)。
        #    `-batchmode -nographics` 没有窗口 ⇒ 不受影响;逻辑 / 输入 / 开模态都在。
        cmd += ["-batchmode", "-nographics"]
    else:
        cmd += ["-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"]
    print("[verify] 启动 player:", " ".join(cmd), flush=True)
    player = subprocess.Popen(cmd, cwd=os.path.dirname(args.build),
                              stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)

    ui = None
    try:
        # ── 设备:进程内造(**不**用 `--trace` 子进程)──
        # 自适应驱动需要**边读读数边推输入**,静态时间线做不到(走路距离取决于玩家起始格)。
        sys.path.insert(0, VPAD_DIR)
        import vpad  # noqa: E402

        ui = vpad.build_device()
        print(f"[verify] 虚拟手柄 node={getattr(getattr(ui, 'device', None), 'path', '?')}", flush=True)

        # 等窗口 → 置焦(失焦 ⇒ Input System 停用设备 ⇒ 按键被静默吞掉,实测过)
        win = None
        window_deadline = time.time() + (20.0 if args.windowed else 4.0)
        while time.time() < window_deadline:
            win = find_player_window(player.pid)
            if win is not None:
                break
            time.sleep(0.3)
        if win is not None:
            focus_window(win)
            _SHOT_WIN[0] = win
            print("[verify] 已置焦玩家窗口", flush=True)
        else:
            print("[verify] ⚠️ 未找到玩家窗口 —— 不置焦(输入可能被停用)", flush=True)

        last_focus = [time.time()]

        def tick_focus():
            if time.time() - last_focus[0] > 2.0:
                focus_window(win)
                last_focus[0] = time.time()

        def wait(seconds):
            end = time.time() + seconds
            while time.time() < end:
                tick_focus()
                time.sleep(0.2)

        def last_row():
            # 只取**周期行**(带 cell 的那种);「模态/档位变化」行没有 cell,拿来判距离会永远 None
            rows = parse(args.log)
            for r in reversed(rows):
                if r["cell"] is not None:
                    return r
            return None

        # ── 进世界:主菜单确认首行 ──
        wait(4.0)                       # 等启动 + 世界预载
        vpad.press(ui, "south", True); wait(0.2); vpad.press(ui, "south", False)
        t0 = time.time()
        while time.time() - t0 < 20.0 and last_row() is None:
            tick_focus(); time.sleep(0.3)
        if last_row() is None:
            print("[verify] ⚠️ 20 s 内没有 tutorialdiag 读数 —— 世界可能没进", flush=True)
        else:
            print("[verify] 已进世界,读数在跑", flush=True)

        # ── ③ 急救档位:按住 West 3.5 s(> AbortIdleTicks 2.5 s)──
        vpad.press(ui, "west", True)
        wait(3.5)
        vpad.press(ui, "west", False)
        wait(1.0)

        # ── ① 走近:自适应 ──
        row = last_row()
        dist = row["dist"] if row else None
        if dist is not None and dist <= 4:
            # 起点已经挨着病人 ⇒ 先退开,否则「走近」没有可观测行程
            vpad.set_stick(ui, 0, -1); wait(2.0); vpad.set_stick(ui, 0, 0); wait(0.4)
        vpad.set_stick(ui, 0, 1)
        t0 = time.time()
        while time.time() - t0 < 15.0:
            row = last_row()
            if row and row.get("dist") is not None and row["dist"] <= 2:
                break
            tick_focus(); time.sleep(0.2)
        vpad.set_stick(ui, 0, 0); wait(0.5)
        row = last_row()
        print(f"[verify] 走完后 距离={row['dist'] if row else '?'} cell={row['cell'] if row else '?'}", flush=True)

        # ── ② 就诊:North(Unity 的 buttonNorth = Interact)──
        vpad.press(ui, "north", True); wait(1.2); vpad.press(ui, "north", False)
        wait(2.5)

        if args.walk3911:
            out = args.walk3911

            # 🔁 2026-11-08(TD-walkthrough-01 收口轮):本档按**修好的 vpad** 重写 ——
            #    ① `start` 现在真的落在 `Gamepad.startButton` 上(见 `vpad.BUTTONS` 的实测表);
            #    ② 摇杆方向按**实测效果**定名(推 `y=+1` ⇒ 焦点往「面色」走 = UI 的**上**);
            #    ③ 读数走 `焦点=` / `模态=` 两列(模态摊开时 tick 停,周期行不再出现)。

            def _lines():
                try:
                    with open(args.log, "r", encoding="utf-8", errors="replace") as f:
                        return f.readlines()
                except FileNotFoundError:
                    return []

            def since(n, key):
                return [l.split(key, 1)[1].rstrip("\n") for l in _lines()[n:] if key in l]

            def focus_now():
                t = since(0, " 焦点=")
                return t[-1].strip() if t else "?"

            def wait_focus(pred, timeout=8.0):
                t0 = time.time()
                while time.time() - t0 < timeout:
                    f = focus_now()
                    if pred(f):
                        return f
                    time.sleep(0.15)
                return focus_now()

            def move(direction, hold=0.18, settle=0.45):
                axis = {"up": (0, 1), "down": (0, -1),
                        "left": (-1, 0), "right": (1, 0)}[direction]
                vpad.set_stick(ui, axis[0], axis[1]); wait(hold)
                vpad.set_stick(ui, 0, 0); wait(settle)

            def submit(hold=0.25):
                vpad.press(ui, "south", True); wait(hold); vpad.press(ui, "south", False)
                wait(0.9)

            print(f"[verify][walk3911] 开册后 模态={_last_modal(args.log)} 焦点={focus_now()}", flush=True)
            _shot(out, "01-opened.png", args.log)

            # ── ① 竖向:一格一停,盯焦点名字 ──
            seen = [focus_now()]
            for i in range(9):
                move("up")
                seen.append(focus_now())
                _shot(out, f"02-vert-{i+1:02d}.png", args.log)
            print(f"[verify][walk3911] 竖推焦点序 = {seen}", flush=True)

            # ── ② 横向:回到「面色」,再右推(短按 = 一格)──
            for _ in range(12):
                if focus_now() == "channel-complexion":
                    break
                move("up")
            print(f"[verify][walk3911] 横推前 焦点={focus_now()}", flush=True)
            _shot(out, "03-right-before.png", args.log)
            move("right", hold=0.18, settle=0.6)
            print(f"[verify][walk3911] 右推一格后 焦点={focus_now()}", flush=True)
            _shot(out, "03-right-after.png", args.log)
            move("left", hold=0.18, settle=0.6)
            print(f"[verify][walk3911] 左推一格后 焦点={focus_now()}", flush=True)
            _shot(out, "03-left-after.png", args.log)

            # ── ③ 翻页:走到「下一页」页缘 → Submit → 两页对照 ──
            target = wait_focus(lambda f: f == "page-edge-next", timeout=1.0)
            if target != "page-edge-next":
                for _ in range(12):
                    move("down")
                    if focus_now() == "page-edge-next":
                        break
            print(f"[verify][walk3911] 翻页前 焦点={focus_now()} 模态={_last_modal(args.log)}", flush=True)
            _shot(out, "05-page1.png", args.log)
            submit()
            print(f"[verify][walk3911] 翻页后 焦点={focus_now()} 模态={_last_modal(args.log)}", flush=True)
            _shot(out, "06-page2.png", args.log)

            # ── ④ 合册:start(修好后真的到 `startButton`)──
            n = len(_lines())
            vpad.press(ui, "start", True); wait(0.3); vpad.press(ui, "start", False)
            wait(1.6)
            print(f"[verify][walk3911] 合册 手柄痕={since(n, ' 手柄=')[-3:]}", flush=True)
            print(f"[verify][walk3911] 合册后 模态={_last_modal(args.log)}", flush=True)
            _shot(out, "07-closed.png", args.log)

            # ── ⑤ 例 2:走到**另一个病人**跟前开册,验「七行访问序两页一致」──
            #    走法 = 4 向摇杆 + `cell=` 反馈闭环(先用两次短推标定「前推是 +z 还是 −z」)。
            def current_cell():
                r = last_row()
                return r["cell"] if r else None

            def push(direction, seconds):
                axis = {"up": (0, 1), "down": (0, -1),
                        "left": (-1, 0), "right": (1, 0)}[direction]
                vpad.set_stick(ui, axis[0], axis[1]); wait(seconds)
                vpad.set_stick(ui, 0, 0); wait(0.25)

            def others():
                """读 `在场=` 里的 `#id@(x,y,z)`,返回 [(id, (x,y,z))]。"""
                import re as _re
                for line in reversed(_lines()):
                    if "在场=" not in line:
                        continue
                    found = _re.findall(r"#(-?\d+)@\((-?\d+),(-?\d+),(-?\d+)\)", line)
                    if found:
                        return [(int(a), (int(b), int(c), int(d))) for a, b, c, d in found]
                return []

            # 标定:前推 0.7 s 看 z 怎么变;右推 0.7 s 看 x 怎么变
            c0 = current_cell()
            push("up", 0.7); c1 = current_cell()
            push("right", 0.7); c2 = current_cell()
            if not (c0 and c1 and c2):
                print("[verify][walk3911] ⚠️ 标定读不到 cell —— 例 2 NOT-RUN", flush=True)
                fwd = (0, 1); rgt = (1, 0)
            else:
                fwd = (0, 1) if c1[2] >= c0[2] else (0, -1)
                rgt = (1, 0) if c2[0] >= c1[0] else (-1, 0)
                print(f"[verify][walk3911] 标定 cell {c0} → {c1} → {c2} ⇒ 前推={fwd} 右推={rgt}", flush=True)

            def walk_dir(axis, seconds):
                vpad.set_stick(ui, axis[0], axis[1]); wait(seconds)
                vpad.set_stick(ui, 0, 0); wait(0.2)

            def walk_to(tx, tz, timeout=60.0):
                t0 = time.time()
                stuck = 0
                while time.time() - t0 < timeout:
                    c = current_cell()
                    if c is None:
                        tick_focus(); time.sleep(0.3); continue
                    dx, dz = tx - c[0], tz - c[2]
                    if abs(dx) <= 1 and abs(dz) <= 1:
                        return c
                    if abs(dz) >= abs(dx):
                        walk_dir((0, fwd[1] if dz > 0 else -fwd[1]), 0.7)
                    else:
                        walk_dir((rgt[0] if dx > 0 else -rgt[0], 0), 0.7)
                    c2_ = current_cell()
                    stuck = stuck + 1 if (c2_ == c) else 0
                    if stuck >= 4:      # 撞墙:换轴绕
                        walk_dir((rgt[0] if dx > 0 else -rgt[0], 0), 0.9)
                        stuck = 0
                    tick_focus()
                return current_cell()

            target = None
            cur = current_cell()
            best = -1
            for pid, cell in others():
                if cur is not None and cell == cur:
                    continue
                d = abs(cell[0] - (cur[0] if cur else 0)) + abs(cell[2] - (cur[2] if cur else 0))
                if d > best:            # 取**最远**的那个 = 刚看过的那位之外的另一个
                    best = d
                    target = (pid, cell)
            if target is None:
                print("[verify][walk3911] ⚠️ 场上只有一个病人 —— 例 2 NOT-RUN", flush=True)
            else:
                print(f"[verify][walk3911] 例 2 目标 = #{target[0]}@{target[1]}", flush=True)
                got = walk_to(target[1][0], target[1][2])
                print(f"[verify][walk3911] 走到 {got}(目标 {target[1]})", flush=True)
                vpad.press(ui, "north", True); wait(1.2); vpad.press(ui, "north", False)
                wait(2.5)
                print(f"[verify][walk3911] 例 2 开册后 模态={_last_modal(args.log)}", flush=True)
                _shot(out, "08-case2-opened.png", args.log)
                seen2 = []
                for i in range(9):
                    move("up")
                    seen2.append(focus_now())
                    _shot(out, f"09-case2-vert-{i+1:02d}.png", args.log)
                print(f"[verify][walk3911] 例 2 竖推焦点序 = {seen2}", flush=True)
                vpad.press(ui, "start", True); wait(0.3); vpad.press(ui, "start", False)
                wait(1.6)
                print(f"[verify][walk3911] 例 2 合册后 模态={_last_modal(args.log)}", flush=True)
                _shot(out, "10-case2-closed.png", args.log)
    finally:
        if ui is not None:
            try:
                ui.close()
            except Exception:
                pass
        if player.poll() is None:
            player.terminate()
            try:
                player.wait(timeout=10)
            except subprocess.TimeoutExpired:
                player.kill()

    rows = parse(args.log)
    print(f"[verify] 读到 {len(rows)} 条 tutorialdiag 读数(日志 {args.log})")
    results = judge(rows, args.min_advance)

    print("\n=== 判据 ===")
    failed = 0
    for name, ok, detail in results:
        print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}")
        if not ok:
            failed += 1

    print("\n判读提示:FAIL 时先看日志里的两段 —— "
          "`[vpad]`(设备是否造出来)与 `[tutorialdiag]`(读数是否在动)。")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
