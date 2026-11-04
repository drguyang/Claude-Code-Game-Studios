#!/usr/bin/env python3
"""九宫格切图冻结件的**四边延展**重测脚本(只读,零写盘)。

权威方法 = `design/assets/specs/nine-slice-freeze-2026-10-08.md` §一 的六步法,
本脚本是它的可执行载体 —— 冻结件 §七 原写「见会话日志」,即**判据没有仓内载体**,
2026-11-04 round-2 重冻时补齐。

用法:
    python3 tools/slice-measure/measure_slices.py                 # 全部 *-final.png
    python3 tools/slice-measure/measure_slices.py border_paper    # 只测文件名含该串的
    python3 tools/slice-measure/measure_slices.py --check         # 与冻结件机器块逐行比对

依赖:Pillow + numpy(清华源:`pip install -i https://pypi.tuna.tsinghua.edu.cn/simple pillow numpy`)

判读:
  · 四边延展 ≈ 0 或 = 短边 ⇒ 该图**没有可用硬边框**(满铺 / 件 / 渐变底),
    冻结值按规则 R2 取「现行渲染切片」,不按实测;
  · 有延展的框图按 R1:`冻结值 = max(ceil8(四边最大延展), 现行已验证 USS 值)`。
"""

import argparse
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[2]
TEX_DIR = REPO / "unity/Assets/Gameplay.UI/Skeuomorphic/Textures"
FREEZE = REPO / "design/assets/specs/nine-slice-freeze-2026-10-08.md"

# §一 六步法的常量(改它们等于改方法 ⇒ 须重开冻结轮)
BG_THRESHOLD = 12      # ③ |pixel − bg| > 12
MIN_PIXELS = 3         # ④ 一行/列至少这么多个掩膜像素才算「有内容」
MAX_GAP = 40           # ⑤ 带内容许的空隙行(吸收墨线间隙)


def measure(path: Path) -> tuple[float, int, int, int, int]:
    """返回 (背景中位, 上, 下, 左, 右) 四边延展(像素)。"""
    im = Image.open(path)
    if im.mode in ("RGBA", "LA"):
        canvas = Image.new("RGBA", im.size, (255, 255, 255, 255))
        canvas.paste(im, (0, 0), im)          # ① alpha 合成白底
        im = canvas
    g = np.asarray(im.convert("L")).astype(int)
    h, w = g.shape

    # ② 背景 = 中心 1/3 中位数
    bg = float(np.median(g[h // 3:2 * h // 3, w // 3:2 * w // 3]))
    mask = np.abs(g - bg) > BG_THRESHOLD       # ③

    # ④ 边带限制:上/下只统计**中 1/3 列**;左/右只统计**中 1/3 行**
    c0, c1 = w // 3, 2 * w // 3
    r0, r1 = h // 3, 2 * h // 3
    top = mask[:, c0:c1].sum(axis=1) >= MIN_PIXELS
    left = mask[r0:r1, :].sum(axis=0) >= MIN_PIXELS

    def band(flags: np.ndarray) -> int:
        """⑤ 从边向内取第一段连续带,容许 ≤MAX_GAP 的空隙;⑥ 返回带长度。"""
        n = len(flags)
        i = 0
        while i < n and not flags[i]:
            i += 1
        if i >= n:
            return 0
        last = j = i
        while j < n:
            if flags[j]:
                last = j
            elif j - last > MAX_GAP:
                break
            j += 1
        return last + 1

    return bg, band(top), band(top[::-1]), band(left), band(left[::-1])


def ceil8(x: int) -> int:
    return ((x + 7) // 8) * 8


def parse_freeze() -> tuple[dict[str, int], dict[str, str]]:
    """返回 (文件→冻结值, 文件→形态)。形态取自 §二 表,决定该行适用哪条取值规则。"""
    text = FREEZE.read_text(encoding="utf-8")
    fence = re.search(r"```freeze-v1\s*\r?\n(.*?)\r?\n```", text, re.S)
    if not fence:
        raise SystemExit("[FATAL] 冻结件里找不到 freeze-v1 机器块")
    frozen: dict[str, int] = {}
    for raw in fence.group(1).splitlines():
        line = raw.strip()
        if not line:
            continue
        parts = line.split("|")
        if len(parts) != 3:
            continue
        frozen[parts[0].strip()] = int(parts[1])

    # §二 表行形如:`| 1 | \`border_paper-final.png\` | 纸 | **框** | … |`
    shape: dict[str, str] = {}
    for raw in text.splitlines():
        m = re.match(r"^\|\s*\d+\s*\|\s*`([^`]+)`\s*\|\s*([^|]*)\|\s*([^|]*)\|", raw)
        if m:
            shape[m.group(1).strip()] = m.group(3).strip()
    return frozen, shape


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("filter", nargs="?", default="", help="只测文件名含该串的图")
    ap.add_argument("--check", action="store_true", help="与冻结件机器块比对(仅表格里的 4 项)")

    args = ap.parse_args(argv)
    files = sorted(TEX_DIR.rglob("*-final.png"))
    if args.filter:
        files = [f for f in files if args.filter in f.name]
    if not files:
        raise SystemExit(f"[FATAL] 没有匹配的贴图:{TEX_DIR}")

    frozen, shape = parse_freeze() if args.check else ({}, {})
    print(f"{'文件':34s} {'bg':>4s} {'T':>5s} {'B':>5s} {'L':>5s} {'R':>5s} {'ceil8':>6s}  冻结  规则")
    bad = 0
    for f in files:
        bg, t, b, l, r = measure(f)
        rel = f.relative_to(TEX_DIR).as_posix()
        mx = max(t, b, l, r)
        mark, rule = "", ""
        if args.check:
            v = frozen.get(rel)
            is_frame = "框" in shape.get(rel, "")
            if v is None:
                mark, rule = "  ✗ 未登记进 freeze-v1", "漏冻"
                bad += 1
            elif not is_frame:
                # R2(满铺/件/渐变)/ R3(规格出图):冻结值**不来自实测**,本法不适用
                mark, rule = "      (实测不适用)", "R2/R3"
            elif v < ceil8(mx):
                # R1:冻结值必须 ≥ ceil8(四边最大延展),否则切片会切掉框线
                mark, rule = f"  ✗ {v} < ceil8(max)={ceil8(mx)}", "R1 违例"
                bad += 1
            else:
                mark, rule = "  ✓", "R1"
        print(f"{rel:34s} {bg:4.0f} {t:5d} {b:5d} {l:5d} {r:5d} {ceil8(mx):6d}  "
              f"{frozen.get(rel, '-'):>4}  {rule}{mark}")

    if args.check:
        print(f"\n比对完成:{'全部通过' if bad == 0 else f'{bad} 项不符'}")
        return 1 if bad else 0
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
